using Godot;

namespace CSVM.Flight;

/// <summary>The non-player <see cref="FlightModel"/> driver: standing orders in, one
/// <see cref="FlightInput"/> per sim step out. A <see cref="FlightController"/> with
/// <see cref="FlightController.Pilot"/> set reads this instead of the keyboard/pad, so an AI
/// aircraft flies the exact same flight model, collision sweep and damage path a player does.
/// Orders are plain mutable fields by design: the mission script retargets an AI at runtime
/// (<c>SET_AI_NET</c>, <c>ADD_OTHER_TARGET</c>, …), so nothing here is read once at spawn.
/// The steering law is the original's own,
/// <see cref="AiControlLaw"/> (docs/org/aiControlLaw.md); this class only drives it.</summary>
public sealed class AiPilot
{
    /// <summary>Invented: how fast lay off walks the throttle toward the ease-off speed, per
    /// second (the decoded constant is the speed factor, not a lever rate).</summary>
    public const float LayOffThrottleRatePerS = 0.4f;

    /// <summary>Invented: the throttle floor while laying off — the pilot slows down, it does
    /// not park in mid-air.</summary>
    public const float LayOffMinThrottle = 0.3f;

    /// <summary>Ordered heading, degrees — the mission-data convention
    /// (<c>SpawnPoint.HeadingDeg</c>): the nose (−Z) yawed about world up by this angle.</summary>
    public float TargetHeadingDeg;

    /// <summary>The patrol net being flown, or null for bare heading/altitude orders (assigning a
    /// different follower is <c>SET_AI_NET</c>'s seam; null returns to the last derived course).
    /// Each <see cref="Next"/> re-derives heading/altitude from the follower's current target node
    /// when set; branch choices draw from its own seeded rng, so a fixed-dt run stays deterministic.
    /// ⚠ Null (netless) is a configuration the original never reaches — it hunts in roll and does
    /// not settle through <see cref="AiControlLaw"/>, including AvoidCrash (`BL-387`).</summary>
    public AiNetFollower? Patrol;

    /// <summary>The forward-gun gunnery, or null for an unarmed pilot. When its target is live and
    /// there is no <see cref="Machine"/>, each <see cref="Next"/> re-derives orders from the
    /// target's position, a plain pursuit through <see cref="AiControlLaw"/> that takes precedence
    /// over <see cref="Patrol"/>. The host <see cref="FlightController"/> decides fire; this only
    /// steers.</summary>
    public AiGunner? Gunner;

    /// <summary>The ordnance employment, or null for a pilot that launches nothing. Armed together
    /// with <see cref="Gunner"/> and useless without it: it holds no target of its own, so the host
    /// hands it the gunner's. Steering never reads it.</summary>
    public AiRocketeer? Rocketeer;

    /// <summary>The nine-mode state machine, or null for the bare-orders pilot above.
    /// When set, each <see cref="Next"/> steps the machine and dispatches on its mode: patrol
    /// flies <see cref="Patrol"/>, pursue chases the gunner's target, lay off holds its entry
    /// course at eased throttle so the pursuer catches up, evade and avoid crash fly the
    /// machine's own orders, an evasive maneuver plays its <see cref="ManeuverExecutor"/> until
    /// done, and stunned holds the controls neutral. Mutable like every other order.</summary>
    public AiModeMachine? Machine;

    /// <summary>Ordered altitude, metres (world Y).</summary>
    public float TargetAltitude = 400f;

    /// <summary>The commanded throttle lever, 0–1 — the lever's CURRENT state, walked toward
    /// <see cref="AiControlLaw"/>'s own desired speed each step, not a standing order the pilot
    /// holds (the original's lever, <c>obj+0x124</c>, works the same way). ⚠ Do not reintroduce an
    /// altitude-leash throttle rule here: the real law's wings-level rule and elevator deadband
    /// replaced that placeholder. Presetting still seeds the walk; <see cref="AiMode.LayOff"/> is
    /// the one mode that overrides the law's answer (<see cref="FlyLayOff"/>).</summary>
    public float Throttle = 0.85f;

    /// <summary>The player's world position, when the owner knows it, or null. The law's far-field
    /// throttle branch is keyed on range to the player, so leaving this null keeps the closed-loop
    /// lever walk, which is the near-player behaviour. Mutable like every other order.</summary>
    public Vector3? PlayerPosition;

    // How far ahead an ordered heading is projected to make the aim point the law wants. A PORT
    // ARTIFACT, not a game value: the original never carries heading/altitude orders, only points
    // (a net node, a target, or its own position plus a climb-out offset), so this distance exists
    // only to convert our order representation. It sets the vertical angle of an altitude capture
    // and nothing else; 1000 m matches the one climb-out offset the original does carry.
    private const float OrderAimRangeM = 1000f;

    // The original's own avoid-crash aim point: straight up from the aircraft by this much.
    private const float ClimbOutAimM = 1000f;

    // INVENTED: how far right of its own ground track the climb-out is displaced, making the
    // 1000 m pull-up a 45° break to the right rather than a vertical one. See ClimbOutAim.
    private const float ClimbOutBreakM = 1000f;

    // The merge rule's five decoded constants (FUN_0041d9f0 at 0x0041e130): the range it arms
    // inside, the fraction of each party's own speed its closure test wants along the line of
    // sight, the flat speed the aim velocity collapses to, and the vertical-bias endpoints.
    private const float MergeRangeM = 400f;
    private const float MergeClosureFraction = 0.8f;
    private const float MergeSpeedMps = 31.292799f;
    private const float MergeDiveSpeedMps = 22.352f;
    private const float MergeClimbSpeedMps = 40.2336f;
    private const float MergeVerticalBiasM = 0.3f;
    private const float MergeVerticalBiasPerMps = 0.033554047f;

    /// <summary>Whether the LAST <see cref="Next"/> actually steered to <see cref="Patrol"/>'s
    /// node, as opposed to pursuing, evading or holding the bare orders. Reported rather than
    /// re-derived from the mode: the dispatch in <see cref="Next"/> is the only thing that
    /// decides it, so an observer (the debug overlay's leashes) that asked the mode machine
    /// instead could drift from it.</summary>
    public bool SteeringPatrol { get; private set; }

    /// <summary>Aims the standing orders at holding the given spawn pose: heading from the
    /// pos→look-at pair, altitude from the position — what a freshly spawned patrol-less AI
    /// flies until something retargets it.</summary>
    public static AiPilot HoldingCourse(Vector3 pos, Vector3 lookAt)
    {
        var dir = lookAt - pos;
        return new AiPilot
        {
            TargetHeadingDeg = HeadingDegOf(dir),
            TargetAltitude = pos.Y,
        };
    }

    /// <summary>The heading (degrees, mission-data convention) whose forward vector is
    /// <paramref name="dir"/>'s horizontal projection.</summary>
    public static float HeadingDegOf(Vector3 dir) =>
        Mathf.RadToDeg(Mathf.Atan2(-dir.X, -dir.Z));

    /// <summary>The decoded aspect test (<c>FUN_0041d9f0</c> at <c>0x0041dd49</c>): whether the
    /// pursuer sits on its victim's own axis, within the pilot's <c>quick_draw_angle</c> of it.
    /// Both cones count, since the original compares the magnitude of the dot against the victim's
    /// backward axis (docs/org/aiPilot.md); only the reversed arm this port omits ever tells the
    /// two apart.</summary>
    public static bool IsOnGunAxis(Vector3 toQuarry, Vector3 quarryNose, float quickDrawAngleDeg)
    {
        if (toQuarry.LengthSquared() < 1f || quarryNose.LengthSquared() < 1e-6f)
            return false;
        float cos = toQuarry.Normalized().Dot(quarryNose.Normalized());
        return Mathf.Abs(cos) > Mathf.Cos(Mathf.DegToRad(quickDrawAngleDeg));
    }

    /// <summary>Avoid crash's aim point. ⚠ Invented: the original climbs out at its own position
    /// plus 1000 m of altitude and nothing else. This adds a <see cref="ClimbOutBreakM"/>
    /// displacement to the RIGHT of the aeroplane's own ground track, the aviation right-of-way
    /// convention, measured to cut merges (docs/org/aiPilot.md). Off the track, not the airframe's
    /// right axis, so a rolled or inverted aeroplane breaks the same way as a level one.</summary>
    public static Vector3 ClimbOutAim(Vector3 pos, Vector3 velocity)
    {
        var track = new Vector3(velocity.X, 0f, velocity.Z);
        var right = track.LengthSquared() > 1e-4f
            ? new Vector3(-track.Z, 0f, track.X).Normalized()
            : Vector3.Right;
        return pos + (Vector3.Up * ClimbOutAimM) + (right * ClimbOutBreakM);
    }

    /// <summary>The decoded merge test (<c>FUN_0041d9f0</c> at <c>0x0041e130</c>): the victim is
    /// inside <see cref="MergeRangeM"/> and the two are flying AT each other — each party's own
    /// velocity lies within <see cref="MergeClosureFraction"/> of its speed along the line of
    /// sight, which is about 37° of it. The original also tests that the victim is a
    /// <c>jet</c>/<c>wingman</c> and never arms this against a ground target; every quarry that
    /// reaches <see cref="FlyPursuit"/> is an aircraft, so that arm has nothing to port.</summary>
    public static bool IsMerging(Vector3 toQuarry, Vector3 ownVelocity, Vector3 quarryVelocity)
    {
        float range = toQuarry.Length();
        float ownSpeed = ownVelocity.Length();
        float quarrySpeed = quarryVelocity.Length();
        if (range >= MergeRangeM || range < 1e-3f || ownSpeed < 1e-3f || quarrySpeed < 1e-3f)
            return false;
        var u = toQuarry / range;
        return ownVelocity.Dot(u) > MergeClosureFraction * ownSpeed
            && quarryVelocity.Dot(u) < -MergeClosureFraction * quarrySpeed;
    }

    /// <summary>The merge's vertical aim bias per unit of the line of sight's horizontal
    /// magnitude, metres: a dive below <see cref="MergeDiveSpeedMps"/>, a climb above
    /// <see cref="MergeClimbSpeedMps"/>, linear between. ⚠ Worth at most <see
    /// cref="MergeVerticalBiasM"/>, 0.3 m (docs/org/aiPilot.md); ported anyway so a decoded term
    /// left out does not get re-derived later as a bug.</summary>
    public static float MergeVerticalBias(float ownSpeed) => ownSpeed switch
    {
        <= MergeDiveSpeedMps => -MergeVerticalBiasM,
        >= MergeClimbSpeedMps => MergeVerticalBiasM,
        _ => (ownSpeed - MergeSpeedMps) * MergeVerticalBiasPerMps,
    };

    /// <summary>One sim step's stick and throttle for the current orders. Pure over the model's
    /// state and this instance's fields (no clocks, no node reads, and the only randomness is
    /// <see cref="Patrol"/>'s own seeded branch draw), so a fixed-dt run is deterministic.</summary>
    public FlightInput Next(FlightModel model, float dt)
    {
        var quarry = Gunner is { Target: { InPlay: true } t } ? t : null;
        SteeringPatrol = false;   // SteerPatrol sets it when it actually flies the net

        // The mode machine, when present, decides which input source flies this step;
        // without one the bare priority stands (gunner target, then patrol, then orders).
        if (Machine is { } machine)
        {
            var mode = machine.Update(model.Position, model.VelocityDir * model.Speed,
                quarry?.WorldPosition, quarry?.Pilot?.Machine?.Mode, dt,
                quarry?.WorldVelocity, quarry?.IsHumanPiloted ?? false,
                nose: -model.Attitude.Z);
            switch (mode)
            {
                case AiMode.Stunned:
                    // Controls neutral for the stun (the throttle lever is not a control
                    // surface and stays where it was).
                    return new FlightInput { Throttle = Mathf.Clamp(Throttle, 0f, 1f) };

                case AiMode.EvasiveManeuver when machine.Executor is { } executor:
                    return executor.Next(model, dt);

                case AiMode.Evade:
                    TargetHeadingDeg = machine.EvadeHeadingDeg;
                    TargetAltitude = machine.EvadeAltitude;
                    return Fly(model, dt, OrderAim(model), Vector3.Zero, AiLawParams.Cruise);

                case AiMode.AvoidCrash:
                    // The machine's climb-out altitude stays the order so its release test still
                    // reads it; ClimbOutAim only adds the invented break to the right.
                    var climbOut = ClimbOutAim(model.Position, model.VelocityDir * model.Speed);
                    TargetHeadingDeg = HeadingDegOf(climbOut - model.Position);
                    TargetAltitude = machine.ClimbOutAltitude;
                    return Fly(model, dt, climbOut, Vector3.Zero, AiLawParams.AvoidCrash,
                        emergency: true);

                case AiMode.Pursue when quarry != null:
                    return FlyPursuit(model, dt, quarry);

                case AiMode.LayOff when quarry != null:
                    return FlyLayOff(model, dt, machine, quarry);

                default: // patrol, the danger-zone modes, and any mode whose quarry went away
                    return FlyPatrol(model, dt);
            }
        }

        return quarry != null ? FlyPursuit(model, dt, quarry) : FlyPatrol(model, dt);
    }

    // Runs the original's law for this step's aim point and keeps the lever it walked.
    // The skill factor is the machine's `sixth_sense_factor`, which the decode shows
    // multiplies all three channels every step; a pilot with no machine gets a neutral 1.
    private FlightInput Fly(FlightModel model, float dt, Vector3 aimPoint, Vector3 aimVelocity,
        in AiLawParams p, bool emergency = false, bool engaged = false, bool gunLead = false)
    {
        var input = AiControlLaw.Steer(model, aimPoint, aimVelocity, p, Throttle, dt,
            emergency, engaged, gunLead, Machine?.SixthSenseFactor ?? 1f, PlayerPosition);
        Throttle = input.Throttle;
        return input;
    }

    // Pursuit: aim at the decoded lead offset ahead of the victim's facing, flown on the engaged
    // table; on the victim's own axis (IsOnGunAxis) aim at it directly and let the law solve the
    // firing problem. A merge (IsMerging) replaces the aim velocity with a flat MergeSpeedMps
    // along the line of sight (docs/org/aiPilot.md) — the original's whole answer to a head-on.
    private FlightInput FlyPursuit(FlightModel model, float dt, FlightController quarry)
    {
        var toQuarry = quarry.WorldPosition - model.Position;
        if (new Vector2(toQuarry.X, toQuarry.Z).LengthSquared() > 1f)
            TargetHeadingDeg = HeadingDegOf(toQuarry);
        TargetAltitude = quarry.WorldPosition.Y;

        var nose = quarry.NoseDirection;
        var vel = quarry.WorldVelocity;
        bool onAxis = Gunner is { } g && IsOnGunAxis(toQuarry, nose, g.QuickDrawAngleDeg);
        var aim = onAxis
            ? quarry.WorldPosition
            : quarry.WorldPosition + (nose * AiControlLaw.LeadOffsetFor(vel.Length()));
        var aimVelocity = vel;
        if (IsMerging(toQuarry, model.VelocityDir * model.Speed, vel))
        {
            var u = toQuarry.Normalized();
            aimVelocity = u * MergeSpeedMps;
            aim.Y += new Vector2(u.X, u.Z).Length() * MergeVerticalBias(model.Speed);
        }
        return Fly(model, dt, aim, aimVelocity, AiLawParams.Engaged, engaged: true, gunLead: onAxis);
    }

    // Lay off (the rubber-band assist): steers the course captured at mode entry on the cruise
    // table, then OVERRIDES the law's lever with a walk toward SixthSenseFactor × the pursuer's
    // speed. ⚠ That override is a remake-only assist, not the original's lay-off, which flies the
    // same table with a flat 0.8 lever floor and no speed match — keep it, landed and playtested.
    private FlightInput FlyLayOff(FlightModel model, float dt, AiModeMachine machine,
        FlightController pursuer)
    {
        TargetHeadingDeg = machine.LayOffHeadingDeg;
        TargetAltitude = machine.LayOffAltitude;
        var input = Fly(model, dt, OrderAim(model), Vector3.Zero, AiLawParams.Cruise);
        float desired = machine.SixthSenseFactor * pursuer.WorldVelocity.Length();
        float step = LayOffThrottleRatePerS * dt;
        Throttle = model.Speed > desired
            ? Mathf.Max(LayOffMinThrottle, Throttle - step)
            : Mathf.Min(1f, Throttle + step);
        input.Throttle = Throttle;
        return input;
    }

    // Patrol: a net node is already the point-with-no-velocity shape the law wants, so it
    // is flown directly; without a net the standing heading/altitude orders are projected into
    // one. Both on the cruise table, which is what the original's patrol arm uses.
    private FlightInput FlyPatrol(FlightModel model, float dt)
    {
        if (Patrol is not { } patrol)
            return Fly(model, dt, OrderAim(model), Vector3.Zero, AiLawParams.Cruise);

        SteeringPatrol = true;
        patrol.Update(model.Position);
        var toNode = patrol.CurrentTarget - model.Position;
        if (new Vector2(toNode.X, toNode.Z).LengthSquared() > 1f)
            TargetHeadingDeg = HeadingDegOf(toNode);
        TargetAltitude = patrol.CurrentTarget.Y;
        return Fly(model, dt, patrol.CurrentTarget, Vector3.Zero, AiLawParams.Cruise);
    }

    // The aim point a bare heading/altitude order becomes (see
    // OrderAimRangeM for why the distance is a port artifact).
    private Vector3 OrderAim(FlightModel model)
    {
        var dir = new Basis(Vector3.Up, Mathf.DegToRad(TargetHeadingDeg)) * Vector3.Forward;
        var aim = model.Position + (dir * OrderAimRangeM);
        aim.Y = TargetAltitude;
        return aim;
    }
}
