using Godot;

namespace CSVM.Flight;

/// <summary>The non-player <see cref="FlightModel"/> driver: standing orders in, one
/// <see cref="FlightInput"/> per sim step out. A <see cref="FlightController"/> with
/// <see cref="FlightController.Pilot"/> set reads this instead of the keyboard/pad, so an AI
/// aircraft flies the exact same flight model, collision sweep and damage path a player does —
/// no new flight code, only a different input source.
///
/// <para><b>Orders are plain mutable fields, deliberately.</b> The original's mission script
/// retargets and re-nets an AI at runtime (<c>SET_AI_NET</c>, <c>ADD_OTHER_TARGET</c>,
/// <c>SET_AI_ATTACK_RADIUS</c>…), so nothing here is read-once at spawn: any owner may rewrite
/// the orders between sim steps and the next <see cref="Next"/> flies them.</para>
///
/// <para>The control law is a placeholder good for straight-and-level and ordered heading/altitude
/// holds — proportional bank-to-turn with rate damping, path-angle altitude hold with a pull term
/// covering the lift lost to bank. The behavioural waves (patrol nets, the nine-mode state
/// machine, the shipped maneuver programs) replace this law; the seam they replace it through is
/// exactly this class.</para></summary>
public sealed class AiPilot
{
    /// <summary>The throttle order a net assignment should come with. Invented, not an original
    /// value: at the 0.85 default (~125 m/s) the placeholder law's turn radius exceeds the
    /// tightest fighter rings and the plane limit-cycles around a node forever (measured on C1's
    /// M4ReinfAce); at 0.5 it laps them. Callers assigning <see cref="Patrol"/> set it
    /// explicitly, so it stays a visible order rather than a hidden override.</summary>
    public const float PatrolThrottle = 0.5f;

    /// <summary>Invented: how fast lay off walks the throttle toward the ease-off speed, per
    /// second (the decoded constant is the speed factor, not a lever rate).</summary>
    public const float LayOffThrottleRatePerS = 0.4f;

    /// <summary>Invented: the throttle floor while laying off — the pilot slows down, it does
    /// not park in mid-air.</summary>
    public const float LayOffMinThrottle = 0.3f;

    /// <summary>Ordered heading, degrees — the mission-data convention
    /// (<c>SpawnPoint.HeadingDeg</c>): the nose (−Z) yawed about world up by this angle.</summary>
    public float TargetHeadingDeg;

    /// <summary>The patrol net being flown, or null for bare heading/altitude orders. When set,
    /// each <see cref="Next"/> re-derives the heading and altitude orders from the follower's
    /// current target node, so a net assignment IS the standing order, and it stays mutable
    /// like the rest (assigning a different follower is <c>SET_AI_NET</c>'s seam; null returns
    /// to the last derived course). Branch choices draw from the follower's own seeded rng, so a
    /// fixed-dt, fixed-seed run is still deterministic.</summary>
    public AiNetFollower? Patrol;

    /// <summary>The forward-gun gunnery (D14), or null for an unarmed pilot. When its target is
    /// live, each <see cref="Next"/> re-derives the heading/altitude orders from the target's
    /// position — a plain pursuit through the placeholder law, taking precedence over
    /// <see cref="Patrol"/> — so the plane turns onto its victim and the gunner's cones get
    /// geometry to pass. The host <see cref="FlightController"/> drives the gunner's fire
    /// decision itself; this class only steers. Mutable like every other order.</summary>
    public AiGunner? Gunner;

    /// <summary>The nine-mode state machine (D11), or null for the bare-orders pilot above.
    /// When set, each <see cref="Next"/> steps the machine and dispatches on its mode: patrol
    /// flies <see cref="Patrol"/>, pursue chases the gunner's target, lay off holds its entry
    /// course at eased throttle so the pursuer catches up (D15), evade and avoid crash fly the
    /// machine's own orders, an evasive maneuver plays its <see cref="ManeuverExecutor"/> until
    /// done, and stunned holds the controls neutral. Mutable like every other order.</summary>
    public AiModeMachine? Machine;

    /// <summary>Ordered altitude, metres (world Y).</summary>
    public float TargetAltitude = 400f;

    /// <summary>Ordered throttle, 0–1.</summary>
    public float Throttle = 0.85f;

    // Placeholder-law gains, hand-settled against the shipped airframes (AiPilotTests sweeps a
    // hard turn and an altitude capture on real stats). Wave D replaces the whole law.
    private const float MaxBankDeg = 70f;
    private const float BankPerHeadingDeg = 4f;     // deg of bank per deg of heading error
    private const float RollGain = 0.05f;           // stick per deg of bank error
    private const float RollRateLead = 0.35f;       // s of roll rate anticipated
    private const float MaxPathDeg = 14f;
    private const float PathPerMeter = 0.12f;       // deg of path per m of altitude error
    private const float PitchGain = 0.10f;          // stick per deg of path error
    private const float PitchRateLead = 0.40f;      // s of pitch rate anticipated
    // 1.2 saturates the stick near full bank on purpose: in this model the climb per DEGREE of
    // turn falls as pull rises (measured 1.5 m/deg at 0.4 pull, 0.55 at 1.0), so a hard turn at
    // full pull finishes well inside the altitude leash where a soft one ratchets past it.
    private const float BankPull = 1.2f;            // pull at full bank; carries the turn
    private const float AltLeashEnterM = 150f;      // this far above orders, break off the turn
    private const float AltLeashExitM = 50f;        // and hold off until back within this
    private const float LeashBankDeg = 20f;         // bank allowed while recovering altitude

    private bool _altRecovering;

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

    /// <summary>One sim step's stick and throttle for the current orders. Pure over the model's
    /// state and this instance's fields (no clocks, no node reads, and the only randomness is
    /// <see cref="Patrol"/>'s own seeded branch draw), so a fixed-dt run is deterministic.</summary>
    public FlightInput Next(FlightModel model, float dt)
    {
        var quarry = Gunner is { Target: { } t } && !t.Crashed ? t : null;

        // The mode machine (D11), when present, decides which input source flies this step;
        // without one the pre-D11 priority stands (gunner target, then patrol, then orders).
        if (Machine is { } machine)
        {
            var mode = machine.Update(model.Position, model.VelocityDir * model.Speed,
                quarry?.WorldPosition, quarry?.Pilot?.Machine?.Mode, dt,
                quarry?.WorldVelocity, quarry?.IsHumanPiloted ?? false);
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
                    Throttle = 1f;
                    break;

                case AiMode.AvoidCrash:
                    // Climb out on the current heading, flat out; the law's leash is what
                    // levels the wings for the pull.
                    TargetHeadingDeg = HeadingDegOf(-model.Attitude.Z);
                    TargetAltitude = machine.ClimbOutAltitude;
                    Throttle = 1f;
                    break;

                case AiMode.Pursue:
                    if (quarry != null)
                        SteerPursuit(model, quarry);
                    break;

                case AiMode.LayOff:
                    if (quarry != null)
                        SteerLayOff(model, machine, quarry, dt);
                    break;

                default: // patrol and the never-entered danger-zone modes fly the net/orders
                    SteerPatrol(model);
                    break;
            }
        }
        else if (quarry != null)
        {
            SteerPursuit(model, quarry);
        }
        else
        {
            SteerPatrol(model);
        }

        var att = model.Attitude;
        var nose = -att.Z;

        // Signed heading error about world up: + = the ordered course is to the LEFT of the
        // nose, which is the sign of the stick that banks left (FlightInput Roll + = bank left).
        var target = new Basis(Vector3.Up, Mathf.DegToRad(TargetHeadingDeg)) * Vector3.Forward;
        var noseH = new Vector3(nose.X, 0f, nose.Z);
        noseH = noseH.LengthSquared() > 1e-6f ? noseH.Normalized() : target;
        float headingErrDeg = Mathf.RadToDeg(Mathf.Atan2(noseH.Cross(target).Y, noseH.Dot(target)));

        // Bank into the turn. Bank is measured + = banked LEFT (the right wingtip, body +X,
        // above the horizon); the model's coordinated-turn coupling converts bank to turn rate.
        float bankDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(att.X.Y, -1f, 1f)));
        float wantBankDeg = Mathf.Clamp(headingErrDeg * BankPerHeadingDeg, -MaxBankDeg, MaxBankDeg);

        // The altitude leash. In this model a sustained turn always climbs (pull is the turn,
        // and gravity is auto-cancelled), so a long turn ratchets altitude without bound. Past
        // the leash the pilot breaks off, levels near-wings-flat so the path hold below gets its
        // authority back, descends, and resumes the turn (hysteresis, so it does not chatter).
        float altErrM = model.Position.Y - TargetAltitude;
        if (_altRecovering ? altErrM < AltLeashExitM : altErrM > AltLeashEnterM)
            _altRecovering = !_altRecovering;
        if (_altRecovering)
            wantBankDeg = Mathf.Clamp(wantBankDeg, -LeashBankDeg, LeashBankDeg);
        float rollRateDegS = Mathf.RadToDeg(model.BodyRates.Z); // + = rolling left
        float roll = Mathf.Clamp(
            (wantBankDeg - bankDeg - rollRateDegS * RollRateLead) * RollGain, -1f, 1f);

        // Altitude hold: fly the flight-path angle toward the ordered altitude, with rate
        // damping, plus a pull covering the vertical lift lost to bank.
        float pathDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(model.VelocityDir.Y, -1f, 1f)));
        float wantPathDeg = Mathf.Clamp(
            (TargetAltitude - model.Position.Y) * PathPerMeter, -MaxPathDeg, MaxPathDeg);
        float pitchRateDegS = Mathf.RadToDeg(model.BodyRates.X); // + = pitching up
        // Pull carries the turn: banked, sustained pitch rate is what walks the nose around
        // (bank alone only yaws at the model's coupling rate). Scaled by the remaining heading
        // error so the pull relaxes as the course captures. The path-hold term is attenuated by
        // cos²(bank): at full strength it cancels the pull into a ~4°/s mush that orbits a
        // patrol node it can never reach (measured); the altitude the turn gains instead is what
        // the leash above pays back.
        float turnPull = _altRecovering ? 0f
            : Mathf.Sin(Mathf.Abs(Mathf.DegToRad(bankDeg))) * BankPull
            * Mathf.Clamp(Mathf.Abs(headingErrDeg) / 8f, 0f, 1f);
        float cosBank = Mathf.Cos(Mathf.DegToRad(bankDeg));
        float pitch = Mathf.Clamp(
            (wantPathDeg - pathDeg - pitchRateDegS * PitchRateLead) * PitchGain * cosBank * cosBank
            + turnPull,
            -1f, 1f);

        return new FlightInput
        {
            Pitch = pitch,
            Roll = roll,
            Yaw = 0f, // bank carries the turn; the placeholder law never uses rudder
            Throttle = Mathf.Clamp(Throttle, 0f, 1f),
        };
    }

    /// <summary>Plain pursuit: fly at the victim's position and altitude, flat out, and let the
    /// gunner's cones decide the trigger. Wave D's real maneuvering replaces this steering
    /// through the same seam as the rest of the law.</summary>
    private void SteerPursuit(FlightModel model, FlightController quarry)
    {
        var toQuarry = quarry.WorldPosition - model.Position;
        if (new Vector2(toQuarry.X, toQuarry.Z).LengthSquared() > 1f)
            TargetHeadingDeg = HeadingDegOf(toQuarry);
        TargetAltitude = quarry.WorldPosition.Y;
        Throttle = 1f; // a stern chase at cruise never closes; pursuit runs flat out
    }

    /// <summary>Lay off (D15, the rubber-band assist): let the pursuer catch up. Holds the
    /// course captured at mode entry — staying ahead of the pursuer instead of turning back
    /// into a head-on — and walks the throttle toward a speed of
    /// <see cref="AiModeMachine.SixthSenseFactor"/> × the pursuer's own speed. The factor is
    /// the decoded <c>sixth_sense_factor</c> ("the ease-off while being pursued"); reading it
    /// as a pursuer-speed match, and the lever rate/floor, are invented. Fire is held by the
    /// host's mode gate — only pursue shoots.</summary>
    private void SteerLayOff(FlightModel model, AiModeMachine machine, FlightController pursuer,
        float dt)
    {
        TargetHeadingDeg = machine.LayOffHeadingDeg;
        TargetAltitude = machine.LayOffAltitude;
        float desired = machine.SixthSenseFactor * pursuer.WorldVelocity.Length();
        float step = LayOffThrottleRatePerS * dt;
        Throttle = model.Speed > desired
            ? Mathf.Max(LayOffMinThrottle, Throttle - step)
            : Mathf.Min(1f, Throttle + step);
    }

    /// <summary>Patrol: the net follower turns the graph walk into this step's heading and
    /// altitude orders; without a net the standing orders fly unchanged.</summary>
    private void SteerPatrol(FlightModel model)
    {
        if (Patrol is not { } patrol)
            return;
        patrol.Update(model.Position);
        var toNode = patrol.CurrentTarget - model.Position;
        if (new Vector2(toNode.X, toNode.Z).LengthSquared() > 1f)
            TargetHeadingDeg = HeadingDegOf(toNode);
        TargetAltitude = patrol.CurrentTarget.Y;
    }
}
