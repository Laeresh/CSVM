using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>Pilot inputs, all in [-1, 1] except Throttle in [0, 1].
/// Pitch + = pull (nose up), Roll + = bank left, Yaw + = nose left.</summary>
public struct FlightInput
{
    public float Pitch, Roll, Yaw, Throttle;

    /// <summary>Ground blow's probe result, filled by the caller because only it has the world:
    /// the WORLD-frame surface normal of the nearest hit on a ray cast forward along the nose,
    /// and the distance to it in metres. <see cref="Vector3.Zero"/> means no hit — and it is the
    /// only "no hit" signal, matching the original, which leaves its zero-initialised output vector
    /// alone on a miss, a back-facing surface or a filtered emitter and so adds nothing.</summary>
    public Vector3 GroundBlowNormal;

    /// <inheritdoc cref="GroundBlowNormal"/>
    public float GroundBlowDistM;

    /// <summary>Multiplier for both AI ground-blow terms during the post-drop settling window.
    /// A negative value suppresses the term; zero keeps the normal factor, preserving callers
    /// that do not set it.</summary>
    public float AiGroundBlowScale;
}

/// <summary>
/// Arcade flight dynamics parameterised by the original's zrdr stats and decoded from its own
/// force path. Thrust, drag, gravity and lift integrate on the velocity VECTOR, so speed passes
/// through zero. Lift is a demanded load factor rather than a fraction of gravity, drag a
/// parabolic polar in MACH with no induced term, thrust a Mach curve times a linear lever.
/// Each axis carries the authored speed-authority curve, which scales the stick command only and
/// never the bank coupling or the weathervane.
/// Decode: docs/org/flightModel.md. The two force paths, the plumbing and the standing
/// decode-vs-footage gaps: this module's entry in docs/architecture.md.
/// ⚠ Do not retune the constants marked TUNE by feel. The three *Tune rates are pinned to
/// measured video of the original, and every other coefficient here is the binary's own.
/// </summary>
public sealed class FlightModel
{
    public Vector3 Position;
    public Basis Attitude = Basis.Identity;       // body→world; nose −Z, up +Y (Godot frame)
    public Vector3 BodyRates;                     // rad/s: x pitch(+up), y yaw(+left), z roll(+left)
    public Vector3 VelocityDir = Vector3.Forward;
    public float Speed;                           // m/s along VelocityDir
    public float Throttle;
    // deg: angle(nose, VelocityDir) at frame start, i.e. before this step's forces move
    // VelocityDir — see Step()'s "α" comment. An emergent LAG from the nose-chase, not a modelled
    // aerodynamic incidence. Reported for instruments only — no force term reads it.
    public float Alpha;
    // The demanded load factor in G at this step — the length of the lift demand's body X/Y
    // projection over StandardG — reported BEFORE the ±5/9 clamp and the aerodynamic ceiling below
    // it. Instruments only; no force term reads it. Pre-clamp is deliberate: it is the most
    // generous reading of "the G this aircraft is pulling", which makes it the right quantity to
    // measure the authored highGs/lowGs control limiters against (ControlLimiterTests).
    public float LoadFactorDemand;

    // Thrust available: the parasite drag force the airframe would feel at a Mach-tracking
    // reference speed, divided by Mach, scaled by EnginePower × RefArea. Nothing here is fitted —
    // every number is the original's (docs/org/flightModel.md, "Thrust available").
    // ⚠ Read EnginePower from the plane's stock Lvl-2 engine row; the Lvl-1 row inflates it ~32 %.
    // ⚠ Do not remove the Mach floor. The 1/M is a real singularity at rest, and the floor is the
    // original's own guard rather than one invented here.
    private const float ThrustMachFloor = 0.1f;
    private const float ThrustVRefSlope = 0.84f;
    private const float ThrustVRefMach = 0.112f;
    private const float ThrustMachTrim = 1f / 60f;
    private const float ThrustPowMach = 1.41f;
    // Base of the Mach-dependent divisor: 1.33 × the atmosphere's own scale factor, which for the
    // dense band (the operative one — see the atmosphere constants) is 0.98842078.
    private const float ThrustPowBase = 1.33f * 0.98842078f;

    // Available thrust is then scaled by the nose's attitude, before the engine-power and
    // reference-area scaling: a climb LOSES thrust (0.6612× straight up) and a dive GAINS it
    // (1.24× straight down). Two immediates in the original's force accumulator, the second
    // one-sided — see AttitudeThrustScale. docs/org/flightModel.md, "Attitude thrust".
    private const float AttitudeThrustBoth = 0.24f;
    private const float AttitudeThrustUp = 0.13f;

    // The hard lift clamp, in G — a LOAD FACTOR, never an angle (docs/org/flightModel.md, "Step 3").
    // ⚠ Do not re-derive this as an incidence limit. It clamps the demanded acceleration, so an
    // angle form looks right at small inputs and diverges at the limits.
    // ⚠ Do not fold this together with the authored highGs/lowGs control limiters. Those are a
    // wider pair sitting at or past this clamp, unreachable on all eleven airframes and therefore
    // not implemented. The negative bound here is likewise unreachable, the demand being a length.
    private const float LiftGMin = -5f;
    private const float LiftGMax = 9f;

    // The aerodynamic ceiling on the delivered lift coefficient: C_L ≤ ClMaxStatic − ClMaxMach·Mach.
    // This — not the load clamp — is what makes a slow aircraft unable to carry its own weight, so
    // it is the term the stall speed falls out of, and it is the only place RefArea and air density
    // enter lift at all (below the ceiling lift is independent of both).
    private const float ClMaxStatic = 0.75f;
    private const float ClMaxMach = 0.15f;

    // Atmosphere. A single band with no altitude gradient: the aerodynamic intermediates run in
    // imperial (ft/s, slug/ft³, lb/ft²) and only the results come back to metric.
    // ⚠ The thin band the original also carries is NOT the operative one — it puts the stall of a
    // 3500/335 airframe at 309 mph against this band's 75.5 mph, which is what settles the choice.
    private const float AirDensitySlugPerFt3 = 2.2688e-3f;
    private const float SpeedOfSoundFps = 1109.5f;
    private const float FeetPerMetre = 3.28084f;
    private const float MetresPerFoot = 0.3048f;
    // The force/acceleration conversion the lift and gravity terms share: lift force is
    // (load factor × Weight) and acceleration is (force × StandardG / Weight), so a demanded load
    // factor of 1 is StandardG of acceleration. Gravity enters the demand as nom_gravity and leaves
    // it as nom_gravity, which is why level flight at zero incidence cancels weight identically.
    private const float StandardG = 9.82f;

    // The two stall thresholds are DIFFERENT numbers and neither is a TUNE. The nose-drop threshold
    // (isStalled, below) is the aircraft's own computed StallSpeed; the STALL lamp lights at a fixed
    // fraction of fd_speed, unrelated to the nose-drop mechanism.
    // docs/org/flightModel.md, "The two stall cues".
    private const float StallWarnFrac = 0.30f;    // STALL lamp lights below this fraction of fd_speed

    // Numerical backstop, NOT a terminal speed: it catches a loop energy pump or a dt spike.
    // ⚠ Do not tighten this until it binds. A cap that binds replaces a measured terminal with a
    // guess, and the aerodynamics already terminate the Bloodhawk's 71° dive at 1.11 × fd_speed.
    private const float MaxDiveSpeedFrac = 1.75f;

    // The measured resting altitude cap, a clamp on altitude rather than an energy limit: it deletes
    // climbing velocity outright instead of fading thrust, lift or drag toward it.
    // docs/org/flightModel.md, "The resting altitude cap".
    // ⚠ Traced to ONE mission and one airframe. Do not assume it is global, per-chapter/zone or
    // per-aircraft.
    private const float AltitudeCapM = 2003f;

    // Numerical backstop (~140 ft), NOT a modelled spring: it bounds a runaway frame to the measured
    // ballistic overshoot rather than shaping the overshoot.
    private const float AltitudeCapOvershootM = 42.8f;
    private const float StallNoseRate = 1.0f;     // TUNE: rad/s toward world-down at full stall depth (× stall_mag)

    // Fraction of the nose-chase that survives at 90° bank; the chase weakens with wing verticality,
    // which is what deepens the knife-edge sag. Kept on a measurement rather than on the decode,
    // which is silent here. docs/org/flightModel.md, "Bank-independent lift vs the measured
    // knife-edge sag".
    // ⚠ Do not retire wingVert by raising this to 1; every knife-edge observable moves the wrong way.
    // ⚠ Do not lower it either. The excess is in the rotation rate, and 0.10 walks α past liftAOAs[0].
    private const float KnifeAlignFloor = 0.35f;  // TUNE

    // Ground blow's two constants that are NOT in player.json (the three that are live on
    // PlaneStats). Both are decoded, neither is a TUNE: the 0.05 is an immediate in the player
    // branch, and the 2.0 is a global whose only writer is the original's `gbc` debug console
    // command, so no data key can move it.
    private const float GroundBlowIntoFactor = 0.05f;  // what a command INTO the obstacle is met with
    private const float GroundBlowVelocitySteer = 2f;  // 1/s at contact: velocity steered onto the nose

    // The collision impulse's linear weight (see BounceNormalSpeed): a hardcoded literal with no
    // data origin, so no key in player.json can move it. It sets where the rebound/spin partition
    // sits and is NOT a TUNE.
    // ⚠ Do not raise it to reach the original's measured flat-ground rebound. That rebound comes
    // from the doubled contact-point term, which is not restitution and is unbounded.
    private const float BounceLeverScale = 2.25f;

    // Drag is a parabolic polar in MACH: C_D = DragPolarScale · (parasite + linear·M + quad·M²),
    // opposing the velocity in weight units. All four numbers are shared by every aircraft, which
    // differ only through the authored drag_factor and ref_area. docs/org/flightModel.md, "Drag".
    // ⚠ The variable is Mach, not the lift coefficient. This model has no induced-drag term at all,
    // and reading these as a C_L polar gives a drag floor that is speed-independent at fixed load.
    // ⚠ Do not refit them to close the recorded decode-vs-footage conflict; they are the binary's.
    private const float DragPolarScale = 0.73f;
    private const float DragPolarParasite = 0.12f;
    private const float DragPolarLinear = 0.8f;
    private const float DragPolarQuad = 0.5f;

    // Per-axis control-rate calibration: steady rate = torque · recInertia · Tune / ang_momentum_damp
    // (× eff on yaw). Pinned to stopwatch and cockpit-gauge video of the original, Bloodhawk-measured.
    // docs/org/flightModel.md, "The three *Tune rates".
    // ⚠ Do not move these to chase the transient response, which is a narrowed but open divergence.
    // They set the STEADY rate, which matches, and a transient chased through them breaks that.
    // ⚠ Re-pin one only when a decoded mechanism moves the steady rate it holds, never on feel.
    private const float PitchTune = 0.89f;        // TUNE: pinned to measured video of the original
    private const float YawTune = 1.57f;          // TUNE: pinned against the authored yaw curve below
    private const float RollTune = 2.12f;         // TUNE: pinned — untouched, the weathervane cannot
                                                  // reach the roll axis (its torque is ⊥ the nose)

    // Bank coupling, the original's coordinated-turn cheat and the only part of its rotation that no
    // airframe authors: banking yaws the nose the way the wings point and pulls it up, with a further
    // pull once the wings are past vertical. docs/org/flightModel.md, "Bank coupling".
    // ⚠ The inverted term reuses the YAW constant on the PITCH axis. It is not a third number, and it
    // peaks wings-level inverted where the bank term is exactly zero.
    // ⚠ Do not scale either term by the axis' *Tune; those calibrate STICK authority and are ours.
    private const float BankYawCoupling = 0.205f;
    private const float BankPitchCoupling = 0.165f;

    // The weathervane: the authored return_rate is a RESTORING TORQUE toward the velocity vector,
    // not extra damping, which makes the rotation a spring-damper rather than a first-order lag.
    // docs/org/flightModel.md, "Weathervane centring".
    // ⚠ The angle is halved, matching the original's quaternion-log conversion; reading it as the
    // full misalignment doubles the spring rate.
    // ⚠ It is PLAYER-only, and Step holds that gate — WeathervaneTorque itself is deliberately not.
    private const float WeathervaneHalfAngle = 0.5f;

    // The AI's forward-velocity floor: after integration, and on the AI path only, the velocity's
    // component along the NOSE is raised to at least 10 mph by adding along the nose, leaving the
    // perpendicular components untouched. One-sided; it only ever raises.
    // docs/org/flightModel.md, "The integrator".
    // ⚠ It is not a floor on Speed. A plane dropping at 20 m/s with its nose on the horizon has
    // ample speed and no forward velocity, and a Speed clamp would do nothing there.
    private const float AiNoseSpeedFloor = 4.4704f;

    // The reverse-authority factor's floor: max(yawAuthority, 0.2) above yaw_max, 1.0 at or below.
    // ⚠ It is NOT a force term — see ReverseAuthorityAt for where the trace leads.
    private const float ReverseAuthorityFloor = 0.2f;

    /// <param name="aiForcePath">Which of the original's two force paths this instance flows — see
    /// <see cref="UsesAiForcePath"/>. ⚠ Optional, and it defaults to the PLAYER path, so a
    /// production construction site added later gets the player plant silently. Two sites pass it
    /// today (<c>HumanFlightAdapter</c>, <c>FlightRoster</c>); a third one must pass it too.
    /// The default exists for the ~18 test sites that construct a plant with no session around
    /// them, not as a statement about what a new caller wants.</param>
    public FlightModel(PlaneStats stats, bool aiForcePath = false)
    {
        Stats = stats;
        UsesAiForcePath = aiForcePath;
        StallSpeed = ComputeStallSpeed(stats);
    }

    public PlaneStats Stats { get; }

    /// <summary>Whether this plant flows the original's AI force path rather than its player one,
    /// chosen once at construction from <c>IsHumanPiloted</c> because the original's own selection
    /// presumes a single player and this engine flies four. The four divergences it drives are all
    /// in <see cref="Step"/> and are listed in this module's docs/architecture.md entry.
    /// ⚠ Named for the PATH, not for the pilot; <see cref="FlightController.IsHumanPiloted"/>
    /// answers who is flying. ⚠ Immutable — a mid-flight switch breaks reproducibility.</summary>
    public bool UsesAiForcePath { get; }

    /// <summary>Airspeed as a fraction of fd_speed — the single stall-proximity scale both stall
    /// thresholds are measured on, and the one the STALL lamp's blink rate ramps over. Every stall
    /// cue derives from this; nothing recomputes its own margin.</summary>
    public float StallFraction => Stats.FdSpeed > 0f ? Speed / Stats.FdSpeed : 0f;

    /// <summary>The speed (m/s) below which the wings' maximum available lift — the same aerodynamic
    /// ceiling <see cref="Step"/> caps lift with — can no longer equal the aircraft's weight: the
    /// solution of <c>clMax(V)·q(V)·RefArea = VehWeight</c> at a LOAD FACTOR OF EXACTLY 1.
    /// ⚠ Do not rescale it by <c>nom_gravity / StandardG</c> to close the Bloodhawk's computed
    /// 56.5 mph against its filmed ~76 mph nose-drop. The decode's worked example settles the 1 G
    /// read, and the gap stands recorded in docs/org/flightModel.md, "Stall".</summary>
    public float StallSpeed { get; }

    /// <summary>Seconds left on the choker's engine-dead timer (<c>TANGLER</c>, the victim's
    /// <c>+0x2e0</c> behind the disabled-systems bit): while it runs the thrust term is zero and
    /// every other force is untouched, so the aircraft bleeds speed on drag rather than snapping to
    /// stall. The lever is left where the pilot put it, as the original leaves it, so the engine
    /// comes back at the throttle setting it died on. Set through <see cref="ChokeEngine"/>.</summary>
    public float EngineDeadRemainingS { get; private set; }

    /// <summary>Whether the engine is dead — the disabled-systems bit's one flight-side reader. No
    /// AI code reads it: a choked pilot is never told, and simply flies an aircraft with no
    /// thrust.</summary>
    public bool EngineDead => EngineDeadRemainingS > 0f;

    /// <summary>How much of the available thrust the nose's attitude leaves: 1 wings-level, 0.6612
    /// pointing straight up, 1.24 pointing straight down. A climb is PENALISED and a dive rewarded.
    /// ⚠ The argument is the world-up component of the BODY Z AXIS and the nose points along −Z, so
    /// pass <c>Attitude.Z.Y</c>, which is negative in a climb. A dropped sign here merely swaps
    /// climb for dive and still looks plausible, which is why <c>AttitudeThrustTests</c> reads the
    /// term back out of the integrator rather than leaving it to review.</summary>
    public static float AttitudeThrustScale(float bodyZUp)
    {
        float a = Mathf.Clamp(bodyZUp, -1f, 1f);
        float scale = 1f + AttitudeThrustBoth * a;
        return a <= 0f ? scale * (1f + AttitudeThrustUp * a) : scale;
    }

    /// <summary>Thrust acceleration along the nose, m/s², at an airspeed and lever position — the
    /// original's thrust-available curve times the lever, LINEARLY. ⚠ The attitude scale is NOT
    /// included: <see cref="AttitudeThrustScale"/> is applied by the caller, so this stays the bare
    /// curve an instrument can sample. Exposed so an instrument can report the curve without
    /// re-deriving it; the step below calls the same method.</summary>
    public float ThrustAccelAt(float speed, float throttle)
    {
        float mach = Mathf.Max(ThrustMachFloor, speed / (SpeedOfSoundFps * MetresPerFoot));
        float vRefFps = (ThrustVRefSlope * mach + ThrustVRefMach) * SpeedOfSoundFps;
        float qRef = 0.5f * AirDensitySlugPerFt3 * vRefFps * vRefFps;
        float cRef = DragPolarScale * (DragPolarParasite - mach * ThrustMachTrim);
        float avail = qRef * cRef / (mach * Mathf.Pow(ThrustPowBase, ThrustPowMach * mach));
        return Stats.VehWeight > 1e-3f
            ? Stats.EnginePower * Stats.RefArea * avail * throttle * StandardG / Stats.VehWeight
            : 0f;
    }

    /// <summary>Rudder authority at an airspeed — the original's authored piecewise speed table: a
    /// low-speed floor, a linear ramp to full authority at <c>yaw_max</c>, then a linear decline to
    /// a high-speed floor. Exposed so an instrument can sample it; Step calls the same method.
    /// ⚠ Deliberately NOT monotone and NOT flat past the knee. The executable's compiled fallbacks
    /// are a different, wrong reading of this curve (docs/org/flightModel.md, "Authored values vs
    /// the executable's fallbacks"). ⚠ YAW ONLY; pitch and roll run their own curve.</summary>
    public float YawAuthorityAt(float speed)
    {
        var s = Stats;
        if (speed <= s.YawFadeIn)
            return s.YawLowSpeed;
        if (speed <= s.YawMax)
            return s.YawMax > s.YawFadeIn
                ? Mathf.Lerp(s.YawLowSpeed, 1f, (speed - s.YawFadeIn) / (s.YawMax - s.YawFadeIn))
                : 1f;
        if (speed <= s.YawFadeOut)
            return s.YawFadeOut > s.YawMax
                ? Mathf.Lerp(1f, s.YawHighSpeed, (speed - s.YawMax) / (s.YawFadeOut - s.YawMax))
                : s.YawHighSpeed;
        return s.YawHighSpeed;
    }

    /// <summary>Roll and pitch authority at an airspeed — the original's shared base ramp, taken
    /// from AIRSPEED ALONE: 0 at or below <c>turn_fade_in</c>, linear to 1 at <c>turn_fade_out</c>,
    /// held at 1 above. Exposed so an instrument can sample it; Step calls the same method.
    /// docs/org/flightModel.md, "The low-speed ramp".
    /// ⚠ ROLL AND PITCH ONLY, and keyed on airspeed rather than on stall. Extending it to the rudder
    /// double-fades an axis that has its own, and gating it on stall makes it vanish.</summary>
    public float RollPitchAuthorityAt(float speed)
    {
        var s = Stats;
        if (speed <= s.TurnFadeIn)
            return 0f;
        return speed < s.TurnFadeOut && s.TurnFadeOut > s.TurnFadeIn
            ? (speed - s.TurnFadeIn) / (s.TurnFadeOut - s.TurnFadeIn)
            : 1f;
    }

    /// <summary>The reverse-authority factor: <c>max(yawAuthority, 0.2)</c> above <c>yaw_max</c> and
    /// 1.0 at or below it, so it engages across the whole of normal flight. Its one consumer here is
    /// <see cref="ControlSurfaceAnimator"/>. docs/org/flightModel.md, "Control authority vs speed".
    /// ⚠ Nothing in the force path may read this. It scales the VISIBLE rudder deflection and
    /// touches no torque; what softens opposing control inside the original's force function is a
    /// different, local quantity, and this method exists to record that misattribution.</summary>
    public float ReverseAuthorityAt(float speed) =>
        speed > Stats.YawMax ? Mathf.Max(YawAuthorityAt(speed), ReverseAuthorityFloor) : 1f;

    /// <summary>The weathervane's restoring torque for the current attitude and flight path, in BODY
    /// axes and in the same units as the stick command — <c>return_rate · (α/2)</c> about the axis
    /// that swings the nose onto the velocity vector. Zero when the two are aligned, and its roll
    /// component is zero always. Ungated, so an instrument or a test can read it on either path;
    /// Step calls the same method and applies it on the player path only. Decode and traps:
    /// <see cref="WeathervaneHalfAngle"/>.</summary>
    public Vector3 WeathervaneTorque()
    {
        var nose = -Attitude.Z;
        var axis = nose.Cross(VelocityDir);
        float sin = axis.Length();
        if (sin < 1e-6f)
            return Vector3.Zero;
        // atan2 of the cross/dot pair, so the angle stays exact out to a fully reversed flight path
        // where an Acos of the dot alone loses precision and a small-angle read is simply wrong.
        float angle = Mathf.Atan2(sin, nose.Dot(VelocityDir));
        var world = axis * (Stats.ReturnRate * WeathervaneHalfAngle * angle / sin);
        return Attitude.Transposed() * world;
    }

    public void Reset(Vector3 position, Basis attitude, float speed, float throttle)
    {
        Position = position;
        Attitude = attitude.Orthonormalized();
        BodyRates = Vector3.Zero;
        VelocityDir = -Attitude.Z;
        Speed = speed;
        Throttle = throttle;
    }

    /// <summary>Kills the engine for <paramref name="seconds"/>, the choker's effect
    /// (<c>FUN_004b1690</c>). The timer only ever EXTENDS: a shorter choke landing on a running one
    /// changes nothing, which is what makes a stream of hits build rather than reset. Non-positive
    /// seconds are ignored; the victim guards live in <c>FlightController.TryChokeEngine</c>.</summary>
    public void ChokeEngine(float seconds)
    {
        if (seconds > EngineDeadRemainingS)
            EngineDeadRemainingS = seconds;
    }

    /// <summary>Restarts a dead engine outright: the respawn reset, so a fresh airframe never flies
    /// with the last one's choke still running.</summary>
    public void ClearChoke() => EngineDeadRemainingS = 0f;

    /// <summary>Seeds the complete world velocity after a reset. A carrier launch inherits its
    /// host's motion, so its direction must not be reconstructed from the launch attitude.</summary>
    public void SetVelocity(Vector3 velocity)
    {
        Speed = velocity.Length();
        VelocityDir = Speed > 1e-6f ? velocity / Speed : -Attitude.Z;
    }

    public void Step(FlightInput input, float dt)
    {
        Throttle = Mathf.Clamp(input.Throttle, 0f, 1f);
        var s = Stats;

        // Spent before the forces below read it, so the frame the timer runs out already has thrust.
        if (EngineDeadRemainingS > 0f)
            EngineDeadRemainingS = Mathf.Max(0f, EngineDeadRemainingS - dt);

        // Read through Config so config.json can override them without a recompile, and read
        // unconditionally once per step so every key registers even on a frame that never enters the
        // stall or knife-edge branches — --dump-config's template and the orphan check need that.
        float pitchTune = Config.GetFloat("flightModel.pitchTune", PitchTune);
        float yawTune = Config.GetFloat("flightModel.yawTune", YawTune);
        float rollTune = Config.GetFloat("flightModel.rollTune", RollTune);
        // Read here as well as at its own site (IsStallWarned, which Step never calls) purely so the
        // key registers on a launch that never flies — --dump-config's template and the orphan check.
        _ = Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);
        float stallNoseRate = Config.GetFloat("flightModel.stallNoseRate", StallNoseRate);
        float knifeAlignFloor = Config.GetFloat("flightModel.knifeAlignFloor", KnifeAlignFloor);
        float liftGMin = Config.GetFloat("flightModel.liftGMin", LiftGMin);
        float liftGMax = Config.GetFloat("flightModel.liftGMax", LiftGMax);
        float altitudeCapM = Config.GetFloat("flightModel.altitudeCapM", AltitudeCapM);
        float altitudeCapOvershootM = Config.GetFloat("flightModel.altitudeCapOvershootM", AltitudeCapOvershootM);

        // --- rotation: torque·recInertia against momentum damping, plus the bank coupling and the
        // weathervane into the same accumulator. Each axis carries its own authored authority curve
        // from airspeed alone, which is why an aeroplane goes mushy as it slows.
        float yawEff = YawAuthorityAt(Speed);
        float rollPitchEff = RollPitchAuthorityAt(Speed);

        // ⚠ The authority scalars multiply the STICK COMMAND only. The bank coupling, the
        // weathervane and the ground blow are summed in below carrying none, which is the original's
        // arrangement: a slow aircraft keeps the full coupling and the full restoring torque.
        var cmd = new Vector3(
            Mathf.Clamp(input.Pitch, -1f, 1f) * s.PitchTorque * s.RecInertia.X * pitchTune * rollPitchEff,
            Mathf.Clamp(input.Yaw, -1f, 1f) * s.RudderTorque * s.RecInertia.Y * yawTune * yawEff,
            Mathf.Clamp(input.Roll, -1f, 1f) * s.RollTorque * s.RecInertia.Z * rollTune * rollPitchEff);

        // Bank coupling (see BankYawCoupling), read off the attitude this frame ENTERED with,
        // alongside the stick command and before anything rotates it. That ordering is the
        // original's own.
        float bankComponent = Attitude.X.Dot(Vector3.Up);
        float bodyUpComponent = Attitude.Y.Dot(Vector3.Up);
        cmd.Y += BankYawCoupling * bankComponent * s.RecInertia.Y;
        cmd.X += (BankPitchCoupling * Mathf.Abs(bankComponent)
                  + (bodyUpComponent < 0f ? -BankYawCoupling * bodyUpComponent : 0f))
                 * s.RecInertia.X;

        // Weathervane (see WeathervaneHalfAngle), read off the same entering attitude and velocity
        // direction, the original's ordering again. The AI path skips the whole block, so an AI nose
        // is never pulled back onto its flight path and its rotation stays a first-order lag.
        if (!UsesAiForcePath)
            cmd += WeathervaneTorque() * s.RecInertia;

        // The AI branch writes its fixed response into persistent angular velocity, not this tick's torque.
        // It is per-tick, not dt-scaled; ordinary torque weakens recovery by the simulation rate.
        // The player branch remains a command bias.
        Vector3 groundBlow = GroundBlowTerm(input, cmd, out float groundBlowSteer);

        // The original's order: accumulate this tick's torque first, then decay the whole result,
        // the fresh torque included. ⚠ Keep the decay EXPONENTIAL. A linear subtraction agrees only
        // to first order and flips BodyRates' sign every tick once dt·damp exceeds 2.
        BodyRates += (cmd + (UsesAiForcePath ? Vector3.Zero : groundBlow)) * dt;
        if (UsesAiForcePath)
            BodyRates += groundBlow;
        BodyRates *= Mathf.Exp(-dt * s.AngMomentumDamp);

        // Stall: below stall speed the nose is pulled toward WORLD-down on a great-circle rotation,
        // with no twist about the nose, so it works at any attitude including inverted. The
        // deep-stall rate exceeds full-elevator authority, which makes the drop decisive.
        bool stalled = isStalled();
        float noseYBefore = (-Attitude.Z).Y;  // the nose's world elevation entering this frame
        if (stalled)
        {
            float depth = 1f - Speed / StallSpeed;
            var noseNow = -Attitude.Z;
            var axis = noseNow.Cross(Vector3.Down);
            if (axis.LengthSquared() > 1e-8f)
            {
                float angle = Mathf.Min(s.StallMag * stallNoseRate * depth * dt,
                                        noseNow.AngleTo(Vector3.Down));
                Attitude = Attitude.Rotated(axis.Normalized(), angle).Orthonormalized();
            }
        }

        var omegaWorld = Attitude * BodyRates;
        float omega = omegaWorld.Length();
        if (omega > 1e-6f)
            Attitude = Attitude.Rotated(omegaWorld / omega, omega * dt).Orthonormalized();

        // While stalled the nose can NOT be raised over the horizon at any bank angle: cap its world
        // elevation there, or at where the frame started if the stall caught it nose-high.
        if (stalled)
        {
            var noseAfter = -Attitude.Z;
            float capY = Mathf.Max(0f, noseYBefore);
            if (noseAfter.Y > capY + 1e-5f)
            {
                var axis = noseAfter.Cross(Vector3.Down);
                if (axis.LengthSquared() > 1e-8f)
                {
                    float angle = Mathf.Asin(Mathf.Clamp(noseAfter.Y, -1f, 1f))
                                - Mathf.Asin(Mathf.Clamp(capY, -1f, 1f));
                    Attitude = Attitude.Rotated(axis.Normalized(), angle).Orthonormalized();
                }
            }
        }

        // ⚠ Do not add a knife-edge nose-sag term here. The decoded bank→yaw coupling already does
        // that job and gives the original's own shape, and a second term keyed on wing verticality
        // double-counts it and fights every wings-level pull too. docs/org/flightModel.md.

        // --- translation: forces integrate on the velocity VECTOR, so speed can pass through zero
        // and a vertical zoom tail-slides out downward instead of freezing mid-air at a clamped 0.
        var nose = -Attitude.Z;

        // α is read here, before this step's forces move VelocityDir, so everything keyed on the
        // pull reads the same value for the frame. ⚠ It is an emergent LAG, not modelled incidence,
        // and a clamped Acos keeps it well-defined at α → 0 where Slerp's axis below is not.
        Alpha = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f)));

        // --- lift: a DEMANDED acceleration, not a fraction of gravity. Step 1 fakes the airflow
        // toward the nose across the authored liftAOAs window, blending on cos α because the
        // authored degrees are cosined at load. ⚠ Player-only — the AI wind is always nose-aligned.
        var velocity = VelocityDir * Speed;
        float cosAlpha = Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f);
        float cosSpan = s.LiftAoaCosLo - s.LiftAoaCosHi;
        float windBlend = UsesAiForcePath
            ? 1f
            : cosSpan > 1e-6f
                ? Mathf.Clamp((s.LiftAoaCosLo - cosAlpha) / cosSpan, 0f, 1f)
                : (cosAlpha < s.LiftAoaCosLo ? 1f : 0f);
        var relativeWind = velocity.Lerp(nose * Speed, windBlend);

        // Step 2 — the demand: swing the velocity onto that airflow at the authored rate and carry
        // weight on top, projected onto the body X/Y plane because only that part acts through the
        // wings. ⚠ The projection's length is a load factor in G, never an incidence angle.
        var demand = (relativeWind - velocity) * s.LiftAccelRate;
        demand.Y += s.Gravity;
        var noseAxis = Attitude.Z;
        var liftDir = demand - noseAxis * demand.Dot(noseAxis);
        LoadFactorDemand = liftDir.Length() / StandardG;
        float loadFactor = Mathf.Clamp(LoadFactorDemand, liftGMin, liftGMax);

        // Step 3 — cap the delivered force at the aerodynamic ceiling, C_L·q·RefArea in weight
        // units, which is the same as capping the load factor at C_L·q·RefArea / Weight. This is
        // where a slow aircraft stops being able to carry itself.
        float speedFps = Speed * FeetPerMetre;
        float dynPressure = 0.5f * AirDensitySlugPerFt3 * speedFps * speedFps;
        float mach = Speed / (SpeedOfSoundFps * MetresPerFoot);
        float clMax = Mathf.Max(0f, ClMaxStatic - ClMaxMach * mach);
        // q·RefArea, in weight units (lb/ft² × ft²) — the scale a coefficient converts to a force,
        // and the divisor the delivered C_L (drag, below) comes back out through.
        float qRefArea = dynPressure * s.RefArea;
        float loadCap = s.VehWeight > 1e-3f ? clMax * qRefArea / s.VehWeight : 0f;
        loadFactor = Mathf.Clamp(loadFactor, -loadCap, loadCap);
        var liftAccel = liftDir.LengthSquared() > 1e-12f
            ? liftDir.Normalized() * (loadFactor * StandardG)
            : Vector3.Zero;

        // How much of the wings' lift points vertically — 1 level or inverted, 0 in knife-edge.
        // ⚠ Lift itself must not read this; the demand's body X/Y projection is bank-independent by
        // construction. Its one reader is the nose-chase below (see KnifeAlignFloor).
        float wingVert = Mathf.Abs(Attitude.Y.Dot(Vector3.Up));

        // Thrust pulls along the nose and drag opposes the motion; the along-path shares fall out of
        // the vector sum. ⚠ Gravity acts at full strength in every attitude and carries no
        // climb-retention scale — the lift demand above already carries weight.
        float cd = DragPolarScale
                   * (DragPolarParasite + DragPolarLinear * mach + DragPolarQuad * mach * mach);
        // Force (weight units) → acceleration is × StandardG / Weight, the same conversion lift uses.
        float dragAccel = s.VehWeight > 1e-3f
            ? qRefArea * s.DragFactor * cd * StandardG / s.VehWeight
            : 0f;
        // A choked engine contributes no thrust and nothing else: no drag term, no lift term and no
        // airspeed clamp are touched (docs/org/ordnanceTypes.md, "The choker, settled").
        float thrustAccel = EngineDead ? 0f : ThrustAccelAt(Speed, Throttle) * AttitudeThrustScale(Attitude.Z.Y);
        var accel = nose * thrustAccel
                    - VelocityDir * dragAccel
                    + Vector3.Down * s.Gravity
                    + liftAccel;
        var vel = VelocityDir * Speed + accel * dt;

        // The AI's nose-axis floor (see AiNoseSpeedFloor) sits here because the original applies it
        // here: after the velocity integration and before |v| is recomputed and the position steps.
        if (UsesAiForcePath)
        {
            float alongNose = vel.Dot(nose);
            if (alongNose < AiNoseSpeedFloor)
                vel += nose * (AiNoseSpeedFloor - alongNose);
        }

        Speed = Mathf.Min(vel.Length(), MaxDiveSpeedFrac * s.FdSpeed);
        if (vel.LengthSquared() > 1e-8f)
            VelocityDir = vel.Normalized();

        // The velocity chases the nose at the authored lift_accel_rate, weakened by wing verticality
        // (see KnifeAlignFloor). Ground blow's second effect rides the same chase unweakened; adding
        // the two rates is exact, since exp(−a·dt)·exp(−b·dt) = exp(−(a+b)·dt).
        float align = s.LiftAccelRate
                      * (knifeAlignFloor + (1f - knifeAlignFloor) * wingVert)
                      + groundBlowSteer;
        // ⚠ Keep the near-parallel lerp branch, which is the normal cruise state. Slerp builds its
        // axis from a cross product whose float error swamps a sub-degree angle, and Godot then
        // throws "Argument is not normalized", aborting the physics frame: the plane stops flying.
        float pathDot = nose.Dot(VelocityDir);
        if (align > 0f && pathDot > -0.999f)
        {
            float t = 1f - Mathf.Exp(-align * dt);
            VelocityDir = (pathDot > 0.999f
                ? VelocityDir + (nose - VelocityDir) * t
                : VelocityDir.Slerp(nose, t)).Normalized();
        }

        // Hard altitude clamp (see AltitudeCapM): at or above the resting cap this frame's climbing
        // velocity is deleted outright rather than redirected, so a sustained pull against it bleeds
        // airspeed instead of gaining height. A no-op below the cap by construction.
        if (Position.Y >= altitudeCapM && VelocityDir.Y > 0f)
        {
            var levelVel = VelocityDir * Speed;
            levelVel.Y = 0f;
            Speed = levelVel.Length();
            if (Speed > 1e-6f)
            {
                VelocityDir = levelVel.Normalized();
            }
        }

        Position += VelocityDir * Speed * dt;
        // Backstop, not a modelled spring (same role as MaxDiveSpeedFrac): bounds a runaway frame to
        // the measured ballistic overshoot rather than ever reproducing its shape.
        Position.Y = Mathf.Min(Position.Y, altitudeCapM + altitudeCapOvershootM);
    }

    /// <summary>Below the airframe's own computed <see cref="StallSpeed"/> — the aerodynamic stall
    /// the flight model flies. NOT the cue the STALL lamp shows: that one lights earlier (a fixed
    /// fraction of fd_speed), see IsStallWarned.</summary>
    public bool isStalled() => Speed < StallSpeed;

    /// <summary>Below the warning threshold (0.30 fd) — the STALL lamp, which leads the break by a
    /// measured 2.64 sim s / 14.9 mph.</summary>
    public bool IsStallWarned() =>
        StallFraction < Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);

    /// <summary>The decoded collision restitution: the velocity's component along the contact normal
    /// AFTER the original's impulse (docs/org/flightModel.md, "Collision response").
    /// ⚠ Nothing here depends on the struck surface — no verticality test, no material lookup, no
    /// friction — so a flat-versus-vertical split must not be implemented as one.</summary>
    /// <param name="contactArm">NOT normalised: its length sets the rebound/spin partition, and a
    /// LONG arm rebounds harder than a short one.</param>
    public float BounceNormalSpeed(Vector3 velocity, Vector3 normal, Vector3 contactArm)
    {
        float vn = normal.Dot(velocity);
        var omegaWorld = Attitude * BodyRates;
        float vpn = vn + 2f * normal.Dot(omegaWorld.Cross(contactArm));
        var j = -vpn * normal;
        float r2 = contactArm.LengthSquared();
        // Δω is the impulse's angular share, in body axes here — |Δω| is all this needs, and a
        // rotation back to world could not change it.
        var dOmega = r2 > 1e-12f
            ? Attitude.Transposed() * (contactArm.Cross(j) / r2) * Stats.RecInertia
            : Vector3.Zero;
        float l = BounceLeverScale * j.Length();
        float a = dOmega.Length();
        float fLin = l + a > 0f ? l / (l + a) : 0f;   // L == 0 → 0, the original's own degenerate arm
        return vn - vpn * (1f + fLin * Stats.BounceFactor);
    }

    // Solves clMax(V)·q(V)·RefArea = VehWeight for V at a load factor of 1 (see StallSpeed's doc for
    // why 1, not nom_gravity/StandardG). clMax's Mach term makes this implicit; a handful of
    // fixed-point passes converge to float precision because stall speeds sit well below the speed
    // of sound, so the correction off the Mach-free start is only a percent or two. Computed once per
    // instance — VehWeight/RefArea never change after construction.
    private static float ComputeStallSpeed(PlaneStats stats)
    {
        if (stats.VehWeight <= 1e-3f || stats.RefArea <= 1e-3f)
            return 0f;
        float vFps = Mathf.Sqrt(2f * stats.VehWeight / (ClMaxStatic * AirDensitySlugPerFt3 * stats.RefArea));
        for (int i = 0; i < 5; i++)
        {
            float mach = vFps / SpeedOfSoundFps;
            float clMax = Mathf.Max(0.05f, ClMaxStatic - ClMaxMach * mach);
            vFps = Mathf.Sqrt(2f * stats.VehWeight / (clMax * AirDensitySlugPerFt3 * stats.RefArea));
        }
        return vFps * MetresPerFoot;
    }


    // Ground blow: the original's bias of control response away from anything large the nose is
    // closing on (docs/org/flightModel.md, "Ground blow"). The caller owns the probe. Two laws share
    // it — the player's biases the STICK, the AI's is a fixed per-tick push. The caller applies
    // dt only to the player command bias; the AI return goes straight to persistent angular speed.
    // ⚠ The AI factor is ai_groundblow · groundblow_mag, never ai_groundblow alone. The caller gates
    // carrier drops for 1.5 s, then applies their ×0.15 final-second response.
    private Vector3 GroundBlowTerm(in FlightInput input, Vector3 cmd, out float velocitySteerRate)
    {
        velocitySteerRate = 0f;
        var n = input.GroundBlowNormal;
        float elev = Stats.GroundBlowElev;
        if (n.LengthSquared() < 1e-12f || elev <= 0f)
            return Vector3.Zero;
        var b = Attitude.Z;                        // backward body axis, in world coordinates
        float c = b.Dot(n);
        if (c <= 0f)
            return Vector3.Zero;
        float proximity = Mathf.Sqrt(c) * (elev - input.GroundBlowDistM) / elev;
        if (proximity <= 0f)
            return Vector3.Zero;
        var axis = n.Cross(b);
        // ⚠ A dead-on approach must get nothing on either path; the original never saves a head-on.
        if (axis.LengthSquared() < 1e-12f)
            return Vector3.Zero;
        // ⚠ Convert to the body frame here, once: the escape axis is built from a WORLD normal, and
        // skipping this gives a term that is right wings-level and wrong at every other attitude.
        // Used twice on the player path, which is where the second power of proximity comes from.
        var v = Attitude.Transposed() * (axis.Normalized() * proximity);

        if (UsesAiForcePath)
        {
            if (input.AiGroundBlowScale < 0f)
                return Vector3.Zero;
            // AI law (0x0048c317): a fixed push, independent of the AI's own command, never
            // suppressed by command direction — unlike the player law below.
            float scale = input.AiGroundBlowScale == 0f ? 1f : input.AiGroundBlowScale;
            velocitySteerRate = GroundBlowVelocitySteer * proximity * scale;
            return v * (Stats.AiGroundBlow * Stats.GroundBlowMag * scale);
        }

        float p = cmd.Dot(v);
        velocitySteerRate = p < 0f ? 0f : GroundBlowVelocitySteer * proximity;
        // Both branches push along +v, away from the surface: commanding away is amplified by
        // 1 + mag·S², commanding into is cut to 1 − 0.05·mag·S² (halved at contact with the
        // authored 10) and never reversed.
        return v * ((p >= 0f ? p : GroundBlowIntoFactor * -p) * Stats.GroundBlowMag);
    }


}
