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
/// <para>The control law is the original's, <see cref="AiControlLaw"/> (decoded as plan D31 in
/// docs/org/aiControlLaw.md, ported as E41). This class is the DRIVER around it, standing in for
/// the original's own two: each mode picks an aim point, that point's velocity and one of the four
/// decoded parameter tables, and the law turns those into stick and throttle. The placeholder
/// bank-to-turn law it replaced, and the altitude leash that law needed, are both gone — the leash
/// existed because a sustained placeholder turn ratcheted altitude without bound, which the real
/// law's own wings-level rule and elevator deadband handle instead.</para></summary>
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

    /// <summary>The patrol net being flown, or null for bare heading/altitude orders. When set,
    /// each <see cref="Next"/> re-derives the heading and altitude orders from the follower's
    /// current target node, so a net assignment IS the standing order, and it stays mutable
    /// like the rest (assigning a different follower is <c>SET_AI_NET</c>'s seam; null returns
    /// to the last derived course). Branch choices draw from the follower's own seeded rng, so a
    /// fixed-dt, fixed-seed run is still deterministic.</summary>
    public AiNetFollower? Patrol;

    /// <summary>The forward-gun gunnery, or null for an unarmed pilot. When its target is
    /// live and there is no <see cref="Machine"/>, each <see cref="Next"/> re-derives the
    /// heading/altitude orders from the target's position — a plain pursuit through
    /// <see cref="AiControlLaw"/>, taking precedence over <see cref="Patrol"/> — so the plane
    /// turns onto its victim and the gunner's cones get geometry to pass. The host
    /// <see cref="FlightController"/> drives the gunner's fire decision itself; this class only
    /// steers. Mutable like every other order.</summary>
    public AiGunner? Gunner;

    /// <summary>The nine-mode state machine, or null for the bare-orders pilot above.
    /// When set, each <see cref="Next"/> steps the machine and dispatches on its mode: patrol
    /// flies <see cref="Patrol"/>, pursue chases the gunner's target, lay off holds its entry
    /// course at eased throttle so the pursuer catches up, evade and avoid crash fly the
    /// machine's own orders, an evasive maneuver plays its <see cref="ManeuverExecutor"/> until
    /// done, and stunned holds the controls neutral. Mutable like every other order.</summary>
    public AiModeMachine? Machine;

    /// <summary>Ordered altitude, metres (world Y).</summary>
    public float TargetAltitude = 400f;

    /// <summary>The commanded throttle lever, 0–1. ⚠ Since E41 this is the lever's CURRENT state,
    /// which <see cref="AiControlLaw"/> walks toward its own desired speed each step, not a
    /// standing order the pilot holds — the original's lever (<c>obj+0x124</c>) works the same way.
    /// Presetting it still seeds the walk, and <see cref="AiMode.LayOff"/> is the one mode that
    /// overrides the law's answer (see <see cref="SteerLayOff"/>).</summary>
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
    /// decides it, so an observer (F13's leashes) that asked the mode machine instead could
    /// drift from it.</summary>
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
    /// pursuer sits on its victim's own axis, within the pilot's <c>quick_draw_angle</c> (vehicle
    /// <c>+0x960</c>, a cosine) of it. BOTH cones count — the original compares the magnitude of
    /// the dot against the victim's backward axis, so a victim coming at us and a victim we are
    /// sitting behind read alike, and each aims the law at the victim itself rather than at the
    /// lead point ahead of it. Only the reversed arm this port omits ever tells the two apart.
    ///
    /// <para>The original also sets the flag outright for a victim that is not an aircraft;
    /// every quarry reaching <see cref="FlyPursuit"/> is one, so that arm has nothing to
    /// port.</para></summary>
    public static bool IsOnGunAxis(Vector3 toQuarry, Vector3 quarryNose, float quickDrawAngleDeg)
    {
        if (toQuarry.LengthSquared() < 1f || quarryNose.LengthSquared() < 1e-6f)
            return false;
        float cos = toQuarry.Normalized().Dot(quarryNose.Normalized());
        return Mathf.Abs(cos) > Mathf.Cos(Mathf.DegToRad(quickDrawAngleDeg));
    }

    /// <summary>Avoid crash's aim point. ⚠ INVENTED in one respect: the original climbs out at its
    /// own position plus 1000 m of altitude and nothing else, which is why two aeroplanes that both
    /// detect each other both pull straight up along converging tracks and merge anyway (measured,
    /// docs/org/aiPilot.md). This adds a <see cref="ClimbOutBreakM"/> displacement to the RIGHT of
    /// the aeroplane's own ground track, which is the aviation right-of-way convention and the
    /// point of it: two aircraft meeting head-on that each break right diverge, where two picking a
    /// side at random still merge half the time. The right is taken off the TRACK rather than the
    /// airframe's right axis, so a rolled or inverted aeroplane breaks the same way as a level
    /// one.</summary>
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

    /// <summary>The merge's vertical aim bias per unit of the line of sight's HORIZONTAL
    /// magnitude, metres: a dive below <see cref="MergeDiveSpeedMps"/> (50 mph), a climb above
    /// <see cref="MergeClimbSpeedMps"/> (90 mph), linear between. ⚠ The line of sight is a unit
    /// vector where the original applies this, so the bias it can produce is at most
    /// <see cref="MergeVerticalBiasM"/>, 0.3 m. That is the decode and not a port artifact: the
    /// original's merge is a speed collapse, and this term does effectively nothing. It is ported
    /// because a decoded term left out invites re-deriving it later as a bug.</summary>
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
        // without one the pre-D11 priority stands (gunner target, then patrol, then orders).
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
                    // The original's own avoid-crash aim point is straight up from the aircraft,
                    // flown on its own table with the emergency arm; ClimbOutAim adds the invented
                    // break to the right. The machine's climb-out altitude stays the ORDER so its
                    // release test and callers still read it.
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

    /// <summary>Runs the original's law for this step's aim point and keeps the lever it walked.
    /// The skill factor is the machine's <c>sixth_sense_factor</c>, which the decode shows
    /// multiplies all three channels every step; a pilot with no machine gets a neutral 1.</summary>
    private FlightInput Fly(FlightModel model, float dt, Vector3 aimPoint, Vector3 aimVelocity,
        in AiLawParams p, bool emergency = false, bool engaged = false, bool gunLead = false)
    {
        var input = AiControlLaw.Steer(model, aimPoint, aimVelocity, p, Throttle, dt,
            emergency, engaged, gunLead, Machine?.SixthSenseFactor ?? 1f, PlayerPosition);
        Throttle = input.Throttle;
        return input;
    }

    /// <summary>Pursuit: the aim point is the decoded lead offset ahead of the victim along the
    /// victim's own facing, flown on the engaged table with its authority bonus. Sitting on the
    /// victim's own axis (<see cref="IsOnGunAxis"/>) aims at the victim itself instead and lets
    /// the law solve the firing problem rather than a fly-to.
    ///
    /// <para>On top of that, a merge (<see cref="IsMerging"/>) replaces the aim VELOCITY the law
    /// is handed: the victim's own velocity gives way to a flat <see cref="MergeSpeedMps"/>
    /// (70 mph) along the line of sight, which collapses the law's speed demand into the pass —
    /// the law then walks the lever down against its own ±26.82 m/s clamp. This is the original's
    /// entire answer to two aircraft closing nose to nose; it does not break off.</para></summary>
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

    /// <summary>Lay off (the rubber-band assist): let the pursuer catch up. Steers the course
    /// captured at mode entry on the cruise table, staying ahead of the pursuer rather than
    /// turning back into a head-on, and then OVERRIDES the law's lever with D15's own walk toward
    /// <see cref="AiModeMachine.SixthSenseFactor"/> × the pursuer's speed.
    /// ⚠ That override is this engine's assist, not the original's lay-off: the decode has the
    /// break-off arm flying the same cruise table with a 0.8 lever floor and no speed match, and
    /// reading the factor as a pursuer-speed match is D15's invention. It is kept because D15 is a
    /// landed, playtested feature with its own suite; revisit it at F52, not here.</summary>
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

    /// <summary>Patrol: a net node is already the point-with-no-velocity shape the law wants, so it
    /// is flown directly; without a net the standing heading/altitude orders are projected into
    /// one. Both on the cruise table, which is what the original's patrol arm uses.</summary>
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

    /// <summary>The aim point a bare heading/altitude order becomes (see
    /// <see cref="OrderAimRangeM"/> for why the distance is a port artifact).</summary>
    private Vector3 OrderAim(FlightModel model)
    {
        var dir = new Basis(Vector3.Up, Mathf.DegToRad(TargetHeadingDeg)) * Vector3.Forward;
        var aim = model.Position + (dir * OrderAimRangeM);
        aim.Y = TargetAltitude;
        return aim;
    }
}
