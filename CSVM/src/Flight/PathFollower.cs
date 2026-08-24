using System.Collections.Generic;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The engine's SECOND movement law: a placed vehicle driven along an authored waypoint path
/// instead of through <see cref="FlightModel"/>. The two are exclusive, and the dispatcher picks
/// between them before any flight law runs, so nothing here is a steering input. Reaching the last
/// waypoint clears <see cref="Following"/>, which is the handoff back to the flight model. Pure
/// state and maths, so a test drives it with no engine. Decode and constants:
/// <c>docs/org/flightModel.md</c>, "The scripted-path follower".
/// </summary>
public sealed class PathFollower
{
    /// <summary>Ground speed held on every leg but the last: 40 mph, exactly.</summary>
    public const float TaxiSpeed = 17.8816f;

    /// <summary>Acceleration on the final leg, replacing the fixed taxi speed. Read out of the
    /// binary but not identified as any authored quantity.</summary>
    public const float FinalLegAcceleration = 4.0302024f;

    /// <summary>Heading error normaliser, 3/pi: error is divided by 60 degrees and clamped to
    /// +/-1, so 60 degrees or more of error turns at a full radian per second.</summary>
    public const float HeadingErrorNormaliser = 0.95492965f;

    /// <summary>Reciprocal of 110 mph, the speed the final leg's climb term is measured in.</summary>
    public const float ClimbSpeedInverse = 0.020335784f;

    /// <summary>Fraction of 110 mph (44 mph) the speed must pass before the final leg's target
    /// starts rising.</summary>
    public const float ClimbSpeedFraction = 0.4f;

    /// <summary>Metres of target altitude per unit of speed over that fraction. Read out of the
    /// binary but not identified as any authored quantity.</summary>
    public const float ClimbGain = 83.3f;

    /// <summary>How far past the last waypoint the final leg's steering target sits, so the
    /// vehicle stops turning and flies the runway heading out.</summary>
    public const float FinalLegOvershoot = 300f;

    /// <summary>Remaining distance along the leg direction at which the leg is finished.</summary>
    public const float LegAdvanceDistance = 5f;

    /// <summary>The ride height movement classes other than 0 and 4 take. ⚠ Classes 0 and 4 (the
    /// aircraft classes, which is every shipped path vehicle) take the vehicle type's own field
    /// instead, and that field is not identified in <c>vehicle.json</c>; see the docs page.</summary>
    public const float OtherClassRideHeight = 0.2f;

    private readonly IReadOnlyList<Vector3> _waypoints;

    /// <summary>Starts a follower at a pose, on the leg into waypoint 1. It begins
    /// <see cref="Following"/> and, unless released, <see cref="Frozen"/>: that is the state a
    /// spawner with a path in its record leaves a vehicle in.</summary>
    public PathFollower(IReadOnlyList<Vector3> waypoints, Vector3 position, float heading)
    {
        _waypoints = waypoints;
        Position = position;
        Heading = heading;
        Speed = TaxiSpeed;
        Leg = 1;
        Following = true;
        Frozen = true;
    }

    /// <summary>Where the vehicle is. The caller writes it onto whatever it is driving.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Yaw in radians, Godot's own convention: forward is -Z at heading 0.</summary>
    public float Heading { get; private set; }

    /// <summary>Current ground speed. Held at <see cref="TaxiSpeed"/> until the final leg.</summary>
    public float Speed { get; private set; }

    /// <summary>The waypoint being flown toward. The last one is the final leg.</summary>
    public int Leg { get; private set; }

    /// <summary>Whether the path still owns this vehicle. It goes false once, on reaching the last
    /// waypoint, and that is the handoff to the flight model. ⚠ A separate flag from
    /// <see cref="Frozen"/>: folding the two together cannot express "placed and waiting", which is
    /// what most authored path vehicles spend a mission in.</summary>
    public bool Following { get; private set; }

    /// <summary>Whether the vehicle is held at its first waypoint. A frozen follower neither moves
    /// nor completes; the mission goal's release clears it.</summary>
    public bool Frozen { get; set; }

    /// <summary>Ride height added to the waypoint the vehicle steers at. Left at zero because the
    /// aircraft classes read a vehicle-type field this project has not identified.</summary>
    public float RideHeight { get; init; }

    /// <summary>Whether the vehicle is on its last leg, where it accelerates and climbs out.</summary>
    public bool OnFinalLeg => Leg >= _waypoints.Count - 1;

    /// <summary>The point the vehicle is steering at this instant: the leg's waypoint raised by the
    /// ride height, or, on the final leg, a point past it that also rises with speed.</summary>
    public Vector3 SteerTarget
    {
        get
        {
            var target = _waypoints[Leg] + (Vector3.Up * RideHeight);
            if (!OnFinalLeg)
            {
                return target;
            }

            target += LegDirection * FinalLegOvershoot;
            float over = (Speed * ClimbSpeedInverse) - ClimbSpeedFraction;
            if (over > 0f)
            {
                target.Y += over * ClimbGain;
            }

            return target;
        }
    }

    // The leg's own straight line, from the waypoint behind to the one ahead. Both the steering
    // overshoot and the finish test are measured along it, never along the bearing to the target.
    private Vector3 LegDirection
    {
        get
        {
            var leg = _waypoints[Leg] - _waypoints[Leg - 1];
            leg.Y = 0f;
            return leg.LengthSquared() > 0f ? leg.Normalized() : Vector3.Forward;
        }
    }

    /// <summary>One tick. A frozen or finished follower does nothing at all, which is what leaves a
    /// placed vehicle sitting on its first waypoint until a goal releases it.</summary>
    public void Step(float dt)
    {
        if (!Following || Frozen || dt <= 0f)
        {
            return;
        }

        var target = SteerTarget;
        var to = target - Position;
        float error = Mathf.Wrap(Mathf.Atan2(-to.X, -to.Z) - Heading, -Mathf.Pi, Mathf.Pi);
        float turn = Mathf.Clamp(error * HeadingErrorNormaliser, -1f, 1f);
        Heading = Mathf.Wrap(Heading + (turn * dt), -Mathf.Pi, Mathf.Pi);
        Speed = OnFinalLeg ? Speed + (FinalLegAcceleration * dt) : TaxiSpeed;

        // It barely advances while turning hard: a full-rate turn stops the vehicle dead, which is
        // what keeps a taxiing aeroplane on the tarmac through a corner.
        float step = Speed * (1f - Mathf.Abs(turn)) * dt;
        var forward = new Vector3(-Mathf.Sin(Heading), 0f, -Mathf.Cos(Heading));
        var moved = Position + (forward * step);
        // Altitude is the one part the decode does not pin: the target's height is what the climb
        // term raises, so the vehicle is taken to it over the horizontal distance still to run.
        float remaining = new Vector2(target.X - Position.X, target.Z - Position.Z).Length();
        moved.Y = remaining > step && step > 0f
            ? Position.Y + ((target.Y - Position.Y) * (step / remaining))
            : target.Y;
        Position = moved;

        var end = _waypoints[Leg];
        var legDir = LegDirection;
        if (new Vector3(end.X - Position.X, 0f, end.Z - Position.Z).Dot(legDir) > LegAdvanceDistance)
        {
            return;
        }

        if (OnFinalLeg)
        {
            Following = false;
        }
        else
        {
            Leg++;
        }
    }
}
