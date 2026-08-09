using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>Pilot inputs, all in [-1, 1] except Throttle in [0, 1].
/// Pitch + = pull (nose up), Roll + = bank left, Yaw + = nose left.</summary>
public struct FlightInput
{
    public float Pitch, Roll, Yaw, Throttle;
}

/// <summary>
/// Arcade flight dynamics parameterized by the original game's zrdr stats.
/// A velocity-vector model in arcade clothing: thrust, drag, gravity and lift
/// integrate on the velocity vector (so a vertical zoom tail-slides out through
/// zero speed instead of hanging). Lift is a DEMAND rather than a fraction of
/// gravity: the airflow is blended toward the nose over the authored
/// <c>liftAOAs</c> cosine window, the difference against the true velocity times
/// <c>lift_accel_rate</c> plus <c>nom_gravity</c> on world-up is projected onto
/// the body X/Y plane, and the wings deliver that acceleration up to a hard load
/// factor and the aerodynamic ceiling (see <see cref="LiftGMax"/>). Level flight
/// at zero incidence therefore cancels weight as an algebraic identity, not as a
/// tuned equilibrium. The arcade handling on top is the flight path chasing the
/// nose (alignment lag, exposed each step as <see cref="Alpha"/>).
/// In a knife-edge the nose itself also sags to a bounded angle
/// below the horizon, so the plane noses down as it sinks rather than descending
/// wings-level-nosed — ⚠ the original's sag is NOT bounded (a known divergence —
/// see <see cref="KnifeNoseSag"/>). Below stall speed the nose is additionally pulled toward
/// world-down and cannot be raised over the horizon. Drag is the original's
/// parabolic polar in MACH (see <see cref="DragPolarScale"/>) — there is no
/// induced-drag term at all — and thrust is its Mach curve times a LINEAR throttle
/// lever (see <see cref="ThrustAccelAt"/>). Neither carries a fitted constant, and
/// the level-speed equilibrium is simply where the two cross: that lands within 1%
/// of the authored fd_speed for nine of the eleven airframes without anything being
/// tuned to make it. Rudder authority follows the original's own authored speed
/// table (see <see cref="YawAuthorityAt"/>) — YAW ONLY; pitch and roll carry
/// their own, different authority curves. The torque/
/// damping/inertia/speed numbers come straight from vehicle.json 'dynamics';
/// the scale constants marked TUNE are ours, adjusted against playtests — except
/// the three *Tune rates, which are pinned to measurements of the original
/// decoded from cockpit-gauge video and must not be retuned by feel.
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

    // Thrust available, per unit of engine power and reference area. There is NO fitted constant
    // here: every number is read out of the original's own thrust-available routine.
    //
    //   V_ref  = (ThrustVRefSlope·M + ThrustVRefMach) · a        a reference speed, ft/s
    //   q_ref  = ½·ρ·V_ref²                                      lb/ft², same dense band as lift
    //   C_ref  = DragPolarScale · (DragPolarParasite − M/60)     the parasite drag coefficient,
    //                                                            with a small linear Mach trim
    //   avail  = q_ref · C_ref / (M · ThrustPowBase^(ThrustPowMach·M))
    //   Thrust = EnginePower · RefArea · avail · throttle        in weight units
    //
    // Read it as "the parasite drag force the airframe would feel at V_ref, divided by Mach" — a
    // constant-power propeller form, but with the reference speed TRACKING the current speed rather
    // than sitting at a fixed design point. That is why the curve does not fall like P/V: q_ref
    // grows as M², so the net is roughly linear in M — available thrust RISES with speed (≈ +35 %
    // between 150 and 500 mph on this atmosphere). What falls with speed in this model is the
    // thrust MARGIN, because drag grows as M² faster than thrust grows as M.
    //
    // Scaling is EnginePower × RefArea — the original's own arrangement, and the dimensionally
    // consistent one: drag carries the same q·RefArea, so RefArea cancels out of the level
    // equilibrium and top speed depends only on EnginePower/DragFactor. Force → acceleration is
    // × StandardG / Weight, the conversion lift and drag already share.
    // ⚠ EnginePower is the plane's OWN stock engine row, which is always the Lvl-2 row (the
    // Bloodhawk's `engine` is 11, power 0.62). Deriving anything from the Lvl-1 row inflates it
    // ~32 %.
    // ⚠ Mach is floored at ThrustMachFloor before ANY of this — the 1/M is a real singularity at
    // rest, and this floor is the original's own guard, not one invented here.
    private const float ThrustMachFloor = 0.1f;
    private const float ThrustVRefSlope = 0.84f;
    private const float ThrustVRefMach = 0.112f;
    private const float ThrustMachTrim = 1f / 60f;
    private const float ThrustPowMach = 1.41f;
    // Base of the Mach-dependent divisor: 1.33 × the atmosphere's own scale factor, which for the
    // dense band (the operative one — see the atmosphere constants) is 0.98842078.
    private const float ThrustPowBase = 1.33f * 0.98842078f;

    // The hard lift clamp, in G — a LOAD FACTOR, never an angle. The wings will not deliver more
    // than this however hard the demand asks, and the clamp is on the demanded acceleration, so
    // re-deriving it as an incidence limit gives a model that looks right at small inputs and
    // diverges at the limits.
    // ⚠ Distinct from the authored highGs/lowGs control limiters, which are a WIDER pair
    // ([9, 15] / [−6, −9] in this install) and therefore sit at or past this clamp — they can never
    // engage before lift is already capped here. Do not fold the two together.
    // ⚠ The demanded load factor is the LENGTH of the projected demand, so it is never negative and
    // the lower bound is unreachable in practice. It is written out because it is what the model
    // clamps to, not because this code path can reach it.
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
    // (isStalled(), below) is now the aircraft's OWN computed stall speed — see StallSpeed — in place
    // of the fixed fraction this replaces (B15). The STALL lamp still lights at a fixed 0.30 fd
    // (0.2989–0.2996 across four clips) — that split is unrelated to the nose-drop mechanism, so it
    // stays a fraction: the original's "Stall 0% Thrust no input" clip measured the warning leading
    // the Bloodhawk's break by 2.64 sim s / 14.9 mph, and nothing here touches that lamp or its
    // threshold.
    private const float StallWarnFrac = 0.30f;    // STALL lamp lights below this fraction of fd_speed
    private const float MaxDiveSpeedFrac = 1.75f; // numerical backstop, NOT a terminal speed — it is
                                                  // meant to catch only the loop energy pump or a dt
                                                  // spike, because a cap that binds replaces a
                                                  // measured terminal with a guess.
                                                  // It does NOT bind: the Bloodhawk's full-throttle
                                                  // 71° dive terminates at 1.11 × fd_speed on the
                                                  // aerodynamics alone, so the terminal dive is an
                                                  // emergent measurement again.
                                                  // Measured resting altitude cap — C1B IA1 footage, Bloodhawk only. NOT an energy
                                                  // limit: level full-throttle equilibrium is flat to ±0.3 mph right up to 15 m under this line,
                                                  // and holding a 22° nose-up pull against it gains no altitude at all (sub-foot over the clip's
                                                  // last 5 s) while airspeed bleeds instead — so the clamp deletes climbing velocity outright
                                                  // rather than fading thrust/lift/drag toward it.
                                                  // ⚠ Traced to ONE mission — do not assume this is global, per-chapter/zone, or per-aircraft.
    private const float AltitudeCapM = 2003f;
    // Numerical backstop (~140 ft), NOT a modelled spring — same role as MaxDiveSpeedFrac below.
    // The footage's zoom entries coast past the resting cap on their own pre-existing momentum before the
    // clamp above ever catches them, so this only needs to be at least as generous as the measured
    // 6712 ft apex; it exists to bound a runaway frame, not to shape the overshoot.
    private const float AltitudeCapOvershootM = 42.8f;
    private const float StallNoseRate = 1.0f;     // TUNE: rad/s toward world-down at full stall depth (× stall_mag)
    private const float ClimbGravityScale = 0.6f; // TUNE: climb retention — a climb bleeds less speed than
                                                  // plain energy exchange (the original holds speed better)
    private const float KnifeAlignFloor = 0.35f;  // TUNE: fraction of the nose-chase that survives at 90°
                                                  // bank — the chase is the lift force turning the velocity,
                                                  // so it weakens with wing verticality (deeper knife-edge sag)
    private const float KnifeNoseSag = 0.07f;     // rad (≈4°) the NOSE settles below the horizon at full
                                                  // knife-edge. The MAGNITUDE is measured — the original's
                                                  // roll-in decodes as an immediate ≈4° step
                                                  // (fitted intercepts −3.4°/−4.2° across two takes at 143 and
                                                  // 300 mph) — but the BOUND is wrong: the original then keeps
                                                  // sagging linearly at 0.69–0.89 °/sim-s with no equilibrium,
                                                  // reaching −27° nose / −18.7° path / 28 m/s sink by +36 s and
                                                  // still steepening. It spirals in, which is exactly what the
                                                  // bound was introduced to avoid. Ours instead settles inside a
                                                  // second at −4° nose / −10° path / 19.4 m/s.
                                                  // ⚠ Do NOT retune this to close the gap — a bounded sag cannot
                                                  // produce a 36-second linear drift, and raising the bound
                                                  // destroys the first 3 s, where the original holds altitude to
                                                  // 0.5 ft/sim-s and we do not. The shape needs replacing, with
                                                  // the lift keying, under BL-247; the sag carries into the path
                                                  // roughly 1:1 because the path chases the nose.
    private const float KnifeNoseRate = 0.2f;     // rad/s toward that sag at full knife-edge, ×(1−wingVert)
                                                  // — exactly 0 wings-level or inverted, so cruise is untouched by
                                                  // construction. A RATE CAP, not an exponential approach: an
                                                  // exponential's rate scales with the displacement, which at a
                                                  // +62° stalled-zoom nose came out ~32°/s — rivalling the 33°/s
                                                  // full elevator, and it measurably rewrote the stall recovery
                                                  // (nose +62°→+28°, wv 0.47→0.88). Clamped to the remaining
                                                  // angle so it approaches the sag and stops, never overshoots.

    // Drag is a parabolic polar in MACH:
    //
    //   C_D  = DragPolarScale · (DragPolarParasite + DragPolarLinear · M + DragPolarQuad · M²)
    //   Drag = q · RefArea · DragFactor · C_D          opposing the velocity, in weight units
    //
    // All three coefficients and the 0.73 scale are the same for every aircraft — airframes differ
    // only through the authored `drag_factor` and `ref_area`.
    //
    // ⚠ The variable is Mach, NOT the lift coefficient. The original's drag routine takes the lift
    // result as an argument and never reads it — so this model has **no induced drag whatsoever**,
    // and a pull costs speed only through the lift vector's own backward tilt in the force sum.
    // Reading the same three coefficients as a C_L polar is the natural mistake (they look exactly
    // like one) and it gives a drag FLOOR that is speed-independent at fixed load factor, which no
    // measurement of the original supports.
    //
    // ⚠ The curve conflicts with `CAP-05`'s four zero-thrust points (0.36/1.11/2.82/3.74 m/s² at
    // x = V/fd_speed = 0.25/0.35/0.46/0.50): in Mach it gives 1.3/3.0/6.2/7.7 — the right *shape*
    // (a 5.9× rise over the span against the measured 10.4×) but ≈2–3.6× too strong. That is NOT a
    // units error: the force→acceleration conversion was re-read from the executable and is exactly
    // this file's — veh_weight parsed, copied and divided raw, no lb/kg factor anywhere — so there
    // is no missing constant to implement and none to tune (a scale that fixed the decel would
    // break the accel row the same footage pins). The deficit is a near-constant ΔC_D ≈ 0.11, and
    // it stands RECORDED as a decode-vs-footage conflict (docs/org/flightModel.md, "The force
    // scale — settled"); the coefficients are the binary's and are not refitted to close it.
    //
    // Because thrust grows as M and this grows as M², the two cross sharply, and that crossing —
    // not a normalization — is what puts the level equilibrium where it is. It lands within 1 % of
    // the authored fd_speed for nine of the eleven player airframes with nothing fitted.
    private const float DragPolarScale = 0.73f;
    private const float DragPolarParasite = 0.12f;
    private const float DragPolarLinear = 0.8f;
    private const float DragPolarQuad = 0.5f;

    // Per-axis control-rate calibration. Steady rate = torque · recInertia · Tune /
    // ang_momentum_damp (× eff on yaw), and a full 360° takes ≈ 1/damp spin-up + 2π/rate.
    // Fitted to stopwatch timings of the original, then confirmed against cockpit-gauge video of
    // it: 360° roll 2.05 s, sustained pitch ~33 °/s, full-rudder 360° 28.6 s — all three within a
    // few percent of what these values already gave, and none of the verdicts moves anywhere
    // inside the video's clock uncertainty. The video also settles what was an open question: the
    // original's pitch rate does NOT fall off with speed (37.9 / 33.7 / 30.7 / 36.5 °/s binned
    // over 120–280 mph round a loop, flat within the noise), so speed-independent pitch is right.
    // ⚠ The STEADY rates above are pinned; the TRANSIENT shape is a known divergence. A square-wave
    // pitch-cadence sweep of the original rolls off 3.5× steeper than
    // the τ → ∞ ceiling of the single first-order lag this integrator implements, so `1/damp` is the
    // wrong shape for the original's pitch transient even though it gives the right steady rate.
    // Open as BL-147; do not "fix" it by moving these Tune constants, which set the steady rate.
    private const float PitchTune = 0.75f;        // TUNE: pinned to the measurements above
    private const float YawTune = 1.33f;          // TUNE: pinned against the authored yaw curve below
    private const float RollTune = 2.12f;         // TUNE: pinned

    public FlightModel(PlaneStats stats)
    {
        Stats = stats;
        StallSpeed = ComputeStallSpeed(stats);
    }

    public PlaneStats Stats { get; }

    /// <summary>Airspeed as a fraction of fd_speed — the single stall-proximity scale both stall
    /// thresholds are measured on, and the one the STALL lamp's blink rate ramps over. Every stall
    /// cue derives from this; nothing recomputes its own margin.</summary>
    public float StallFraction => Stats.FdSpeed > 0f ? Speed / Stats.FdSpeed : 0f;

    /// <summary>The speed (m/s) below which the wings' maximum available lift — the SAME aerodynamic
    /// ceiling <see cref="Step"/> caps lift with, <c>ClMaxStatic − ClMaxMach·Mach</c> — can no longer
    /// equal the aircraft's own weight: the solution of <c>clMax(V)·q(V)·RefArea = VehWeight</c>, a
    /// LOAD FACTOR OF EXACTLY 1 (not <c>nom_gravity / StandardG</c> ≈ 2.04, what level flight itself
    /// demands to cancel this install's arcade gravity — the two conventions differ by
    /// √(nom_gravity/StandardG) ≈ 1.43×). The decode's own worked example settles which one the
    /// binary actually compares against: it reproduces 75.5 mph for the fallback aircraft
    /// (veh_weight 3500, ref_area 335) and 309 mph for the same aircraft under the wrong atmosphere
    /// band — BOTH numbers match only the bare-Weight (1 G) read; the nom_gravity-scaled read gives
    /// 109/447 mph instead, which the decode never quotes. So 1 G is what is coded, not a
    /// simplification of it.
    /// ⚠ Evaluated against a REAL airframe rather than the fallback numbers, this surfaces a residual
    /// the coincidence was hiding: the Bloodhawk's own data (1900/330) computes a 56.5 mph stall
    /// against the video-measured ~76 mph nose-drop ("Stall 0% Thrust no input" clip) — the fixed
    /// 0.25·fd_speed this replaces only matched that footage because 0.25 × the BLOODHAWK's fd_speed
    /// happens to sit close to the FALLBACK aircraft's own stall speed, not the Bloodhawk's (see the
    /// B15 landing note in docs/org/flightModel.md). Recorded as a decode-vs-footage conflict, not
    /// papered over by switching the G convention to fit one clip.</summary>
    public float StallSpeed { get; }

    /// <summary>Thrust acceleration along the nose, m/s², at an airspeed and lever position — the
    /// original's thrust-available curve times the lever, LINEARLY. Exposed so an instrument can
    /// report the curve without re-deriving it; the step below calls the same method.</summary>
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

    /// <summary>Rudder authority at an airspeed — the original's authored piecewise speed table
    /// (<see cref="PlaneStats.YawFadeIn"/>/<see cref="PlaneStats.YawMax"/>/
    /// <see cref="PlaneStats.YawFadeOut"/>/<see cref="PlaneStats.YawLowSpeed"/>/
    /// <see cref="PlaneStats.YawHighSpeed"/>): a low-speed floor, ramping LINEARLY to full
    /// authority at yaw_max, then declining LINEARLY to a high-speed floor at yaw_fade_out and
    /// holding there. Deliberately NOT monotone, and NOT flat past the knee — the executable's
    /// compiled fallbacks (knee at 45 mph, flat 0.1 beyond) are a different, wrong reading; this
    /// install authors a curve that keeps declining out to 400 mph. YAW ONLY — pitch and roll run
    /// their own, different authority curves and are untouched here.
    /// Exposed so an instrument can sample the curve without re-deriving it; Step calls the same
    /// method.</summary>
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

    public void Reset(Vector3 position, Basis attitude, float speed, float throttle)
    {
        Position = position;
        Attitude = attitude.Orthonormalized();
        BodyRates = Vector3.Zero;
        VelocityDir = -Attitude.Z;
        Speed = speed;
        Throttle = throttle;
    }

    public void Step(FlightInput input, float dt)
    {
        Throttle = Mathf.Clamp(input.Throttle, 0f, 1f);
        var s = Stats;

        // Tunable scale constants, read through Config so config.json can override them without a
        // recompile; each falls back to the in-code const default (and the rationale beside it)
        // above. Read unconditionally here — once per step — so every key registers even on a frame
        // that never enters the stall or knife-edge branches below, which keeps --dump-config's
        // template complete and the orphan/missing checks honest.
        float pitchTune = Config.GetFloat("flightModel.pitchTune", PitchTune);
        float yawTune = Config.GetFloat("flightModel.yawTune", YawTune);
        float rollTune = Config.GetFloat("flightModel.rollTune", RollTune);
        // Read here as well as at its own site (IsStallWarned, which Step never calls) purely so the
        // key registers on a launch that never flies — --dump-config's template and the orphan check.
        _ = Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);
        float stallNoseRate = Config.GetFloat("flightModel.stallNoseRate", StallNoseRate);
        float climbGravityScale = Config.GetFloat("flightModel.climbGravityScale", ClimbGravityScale);
        float knifeAlignFloor = Config.GetFloat("flightModel.knifeAlignFloor", KnifeAlignFloor);
        float knifeNoseSag = Config.GetFloat("flightModel.knifeNoseSag", KnifeNoseSag);
        float knifeNoseRate = Config.GetFloat("flightModel.knifeNoseRate", KnifeNoseRate);
        float liftGMin = Config.GetFloat("flightModel.liftGMin", LiftGMin);
        float liftGMax = Config.GetFloat("flightModel.liftGMax", LiftGMax);
        float altitudeCapM = Config.GetFloat("flightModel.altitudeCapM", AltitudeCapM);
        float altitudeCapOvershootM = Config.GetFloat("flightModel.altitudeCapOvershootM", AltitudeCapOvershootM);

        // --- rotation: torque·recInertia vs momentum damping (all from the dynamics block).
        // Control surfaces bite proportionally to airspeed; return_rate adds extra
        // centering on an axis while its stick is released.
        // Yaw authority follows the original's authored speed table (see YawAuthorityAt) — a
        // declining function of speed, same as the interim curve it replaces, but the original's
        // own shape rather than a fitted stand-in. Pitch and roll carry no such fade here (roll
        // never fades in the original; the pitch fade is authored unreachable).
        float yawEff = YawAuthorityAt(Speed);
        var cmd = new Vector3(
            Mathf.Clamp(input.Pitch, -1f, 1f) * s.PitchTorque * s.RecInertia.X * pitchTune,
            Mathf.Clamp(input.Yaw, -1f, 1f) * s.RudderTorque * s.RecInertia.Y * yawTune * yawEff,
            Mathf.Clamp(input.Roll, -1f, 1f) * s.RollTorque * s.RecInertia.Z * rollTune);
        var damp = new Vector3(
            s.AngMomentumDamp + s.ReturnRate * (1f - Mathf.Min(1f, Mathf.Abs(input.Pitch))),
            s.AngMomentumDamp + s.ReturnRate * (1f - Mathf.Min(1f, Mathf.Abs(input.Yaw))),
            s.AngMomentumDamp + s.ReturnRate * (1f - Mathf.Min(1f, Mathf.Abs(input.Roll))));
        BodyRates += (cmd - BodyRates * damp) * dt;

        // stall: below stall speed the nose is pulled toward WORLD-down (a great-circle
        // rotation about the nose×down axis — no twist about the nose, works at any
        // attitude including inverted). Deep-stall rate exceeds full-elevator authority
        // (~0.58 rad/s steady after the item-12 calibration), so the drop is decisive
        // until airspeed recovers.
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

        // while stalled the nose can NOT be raised over the horizon, at any bank angle
        // (original behavior, user-observed): cap its world elevation at the
        // horizon — or where the frame started, if the stall caught it nose-high, so it
        // can only come down from there. Same great-circle rotation as the stall drop.
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

        // knife-edge nose sag: the original drops the nose as well as the flight path, and without
        // this block the plane would descend wings-level-nosed because the only other knife-edge
        // term (the nose-chase, weakened by wing verticality)
        // acts on VelocityDir and nothing else touches Attitude. Same great-circle
        // rotation about nose×down as the stall drop, so it is attitude-independent and
        // adds no twist about the nose; at 90° bank that axis is the plane's own up, i.e.
        // this reads as the body YAW that top rudder is flown to cancel — which is exactly
        // the real knife-edge control the pilot now has to hold.
        //
        // It targets a BOUNDED elevation rather than chasing world-down or the flight path.
        // Both of those are unbounded and neither settles: the path chases the nose
        // (`align`, below), so a nose that keeps falling drags the path down with it and
        // the pair spirals into the ground instead of reaching the sag equilibrium.
        //
        // Sits here — after the stall block, before the translation — so `nose`, `wingVert`,
        // the thrust direction and the nose-chase all read one consistent attitude this
        // frame. It can only ever LOWER the nose (skipped once the nose is at or below the
        // target), and it is **off entirely while stalled**: below stall speed the stall
        // block owns the nose outright, so gating on `!stalled` is what makes the
        // stall/sag interaction provably empty rather than merely benign.
        // (Measured: WITHOUT the gate, an exponential approach reached ~32°/s at a +62°
        // nose and moved the stalled zoom apex to +28°, wv 0.47→0.88. The two never
        // pulled against each other — both drive the nose down — but they compounded,
        // which is its own kind of wrong. WITH the gate the stalled phase itself is
        // untouched; a stall-into-knife-edge run still differs slightly overall, by ~4°
        // at the apex, because the *pre*-stall banked zoom is legitimately in scope for
        // this term, and converges to within 1° after recovery.)
        if (!stalled)
        {
            float knife = 1f - Mathf.Abs(Attitude.Y.Dot(Vector3.Up));
            if (knife > 1e-4f)
            {
                var noseKnife = -Attitude.Z;
                float noseElev = Mathf.Asin(Mathf.Clamp(noseKnife.Y, -1f, 1f));
                float sagTarget = -knifeNoseSag * knife;
                if (noseElev > sagTarget + 1e-5f)
                {
                    var axis = noseKnife.Cross(Vector3.Down);
                    if (axis.LengthSquared() > 1e-8f)
                    {
                        float angle = Mathf.Min(knifeNoseRate * knife * dt,
                                                noseElev - sagTarget);
                        Attitude = Attitude.Rotated(axis.Normalized(), angle).Orthonormalized();
                    }
                }
            }
        }

        // --- translation: forces integrate on the velocity VECTOR (v = VelocityDir·Speed),
        // so the speed can pass through zero — a vertical zoom tail-slides out downward
        // instead of freezing mid-air at a clamped 0 (a plane visibly stopped in the air
        // while the HUD mph crept back up, user-reported).
        var nose = -Attitude.Z;

        // α = angle(nose, VelocityDir), read here — before this step's forces move VelocityDir —
        // so lift (below) and anything else keyed on the pull read the SAME value for the frame.
        // Hoisted out of the near-parallel pathDot check
        // below rather than replacing it: that one re-reads pathDot AFTER the translation update,
        // to decide whether Slerp's cross-product axis is well-conditioned for THIS frame's actual
        // chase, which is a distinct question from what α reports here.
        // ⚠ α is an emergent LAG in this model (the flight path chasing the nose at a finite rate),
        // not an aerodynamic state — do not describe it as modelled incidence. Near-parallel nose
        // and path is the normal cruise state, so this must stay well-defined as α → 0: Acos of a
        // clamped dot product is, unlike Slerp's axis, safe at zero.
        Alpha = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f)));

        // --- lift: a DEMANDED acceleration the wings deliver, not a fraction of gravity.
        //
        // Step 1 — the oncoming airflow is faked toward the nose across the authored liftAOAs
        // window. Below the low edge the true velocity vector is used; past the high edge the real
        // airflow direction is discarded entirely and the wind is taken as coming straight down the
        // nose; between the two it is a LINEAR BLEND ON cos α, because the authored degrees are
        // cosined at load and the window is a cosine window (blending on the angle instead is a
        // different, subtly wrong curve). This is the large arcade assist that lets a hard-
        // manoeuvring aircraft behave as though it has no sideslip.
        var velocity = VelocityDir * Speed;
        float cosAlpha = Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f);
        float cosSpan = s.LiftAoaCosLo - s.LiftAoaCosHi;
        float windBlend = cosSpan > 1e-6f
            ? Mathf.Clamp((s.LiftAoaCosLo - cosAlpha) / cosSpan, 0f, 1f)
            : (cosAlpha < s.LiftAoaCosLo ? 1f : 0f);
        var relativeWind = velocity.Lerp(nose * Speed, windBlend);

        // Step 2 — the demand: swing the velocity onto that airflow at the authored rate, and carry
        // weight on top. Only the part of it the wings can act through counts, so it is projected
        // onto the body X/Y plane (the nose axis is body Z), and the length of that projection is
        // the demanded LOAD FACTOR in G. ⚠ G, not degrees — this is an acceleration the wings are
        // asked to produce, and nothing here is an incidence angle.
        var demand = (relativeWind - velocity) * s.LiftAccelRate;
        demand.Y += s.Gravity;
        var noseAxis = Attitude.Z;
        var liftDir = demand - noseAxis * demand.Dot(noseAxis);
        float loadFactor = Mathf.Clamp(liftDir.Length() / StandardG, liftGMin, liftGMax);

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

        // How much of the wings' lift points vertically — 1 level OR inverted (arcade: inverted
        // flight still carries), 0 in knife-edge. Lift itself no longer reads it (the demand's body
        // X/Y projection is bank-independent by construction), but it is still the carrier for the
        // nose-chase below and the same quantity the knife-edge nose-sag runs on.
        float wingVert = Mathf.Abs(Attitude.Y.Dot(Vector3.Up));

        // thrust pulls along the nose (its along-path share falls out of the vector sum —
        // a stalled plane falling nose-high needs no special case), and is LINEAR in the throttle
        // lever. Drag opposes the motion: the parabolic polar in Mach (see the constants). At
        // v ≈ 0 drag → 0 and the stale direction there is harmless.
        // Gravity acts in FULL here — the lift demand above already carries weight, and subtracting
        // it twice is the trap the old cross-path fraction was one half of. It is still split about
        // the path only so the along-path share can be scaled: a climb bleeds less speed than plain
        // energy exchange (the original holds speed better), full when diving.
        //
        // The polar's variable is MACH. A pull therefore costs speed only through the lift vector's
        // own backward tilt in the force sum below — there is no induced-drag term here at all, and
        // adding one (α-keyed, C_L-keyed or otherwise) is not this model.
        float cd = DragPolarScale
                   * (DragPolarParasite + DragPolarLinear * mach + DragPolarQuad * mach * mach);
        // Force (weight units) → acceleration is × StandardG / Weight, the same conversion lift uses.
        float dragAccel = s.VehWeight > 1e-3f
            ? qRefArea * s.DragFactor * cd * StandardG / s.VehWeight
            : 0f;
        var gravity = Vector3.Down * s.Gravity;
        var gAlong = VelocityDir * gravity.Dot(VelocityDir);
        var gAcross = gravity - gAlong;
        var accel = nose * ThrustAccelAt(Speed, Throttle)
                    - VelocityDir * dragAccel
                    + gAlong * (VelocityDir.Y > 0f ? climbGravityScale : 1f)
                    + gAcross
                    + liftAccel;
        var vel = VelocityDir * Speed + accel * dt;
        Speed = Mathf.Min(vel.Length(), MaxDiveSpeedFrac * s.FdSpeed);
        if (vel.LengthSquared() > 1e-8f)
            VelocityDir = vel.Normalized();

        // velocity chases the nose, weakening with wing verticality (in knife-edge the wings can't
        // lift the path back to the nose, so the sag equilibrium sits visibly below it — the
        // nose-drop). The RATE is the authored lift_accel_rate, the same quantity the lift demand
        // above swings the velocity onto the faked airflow with — it is data, not a tuning knob,
        // and it is speed-independent because the demand it stands for is (the acceleration grows
        // with speed, the resulting angular rate does not).
        // (Skip when path ≈ opposite the nose — slerp axis degenerates; gravity will
        // swing the path around within a few frames anyway.)
        float align = s.LiftAccelRate
                      * (knifeAlignFloor + (1f - knifeAlignFloor) * wingVert);
        // Near-parallel is the normal cruise state, and there Slerp is unusable: it builds its
        // rotation axis from the cross product, whose float error swamps a sub-degree angle, and
        // Godot then throws "Argument is not normalized" — which aborts the whole physics frame,
        // so a plane holding straight and level simply stopped flying (found while verifying
        // splitscreen; it bit single player exactly the same). Under ~2.5° a normalized
        // lerp is the same rotation to well under a thousandth of a degree, and needs no axis.
        float pathDot = nose.Dot(VelocityDir);
        if (align > 0f && pathDot > -0.999f)
        {
            float t = 1f - Mathf.Exp(-align * dt);
            VelocityDir = (pathDot > 0.999f
                ? VelocityDir + (nose - VelocityDir) * t
                : VelocityDir.Slerp(nose, t)).Normalized();
        }

        // hard altitude clamp: once at or above the resting cap, this frame's
        // climbing velocity is deleted outright rather than redirected into more horizontal speed —
        // a clamp on altitude, not an energy limit, so a sustained pull against it bleeds airspeed
        // instead of gaining height. A no-op below the cap by construction: thrust/lift/drag and
        // every branch above are untouched. Whatever follows once that bleed reaches the existing
        // stall thresholds is the model's own consequence, not a mechanism built here.
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
}
