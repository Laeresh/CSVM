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
/// A velocity-vector model in arcade clothing: thrust, drag and gravity
/// integrate on the velocity vector (so a vertical zoom tail-slides out through
/// zero speed instead of hanging), lift cancels gravity's cross-path component
/// only when the plane is fast enough AND the wings carry vertically (lift ∝
/// speed² × |up·Y|, so knife-edge flight is near-ballistic and a slow plane
/// sinks), and the arcade handling is the flight path chasing the nose
/// (alignment lag, exposed each step as <see cref="Alpha"/> — observation-only for now, BL-247
/// B11). In a knife-edge the nose itself also sags to a bounded angle
/// below the horizon, so the plane noses down as it sinks rather than descending
/// wings-level-nosed — ⚠ the original's sag is NOT bounded (a known divergence —
/// see <see cref="KnifeNoseSag"/>). Below stall speed the nose is additionally pulled toward
/// world-down and cannot be raised over the horizon. Thrust (sublinear in the
/// throttle lever) vs drag (a power law in V/fd_speed, with a measured slope
/// change at fd) gives the level-speed equilibrium at fd_speed. The torque/
/// damping/inertia/speed numbers come straight from vehicle.json 'dynamics';
/// the scale constants marked TUNE are ours, adjusted against playtests — except
/// the three *Tune rates, which are pinned to measurements of the original
/// decoded from cockpit-gauge video and must not be retuned by feel, and the
/// ThrustConst/DragExp*/ThrottleExp group, which is pinned the same way but only
/// <i>jointly</i> — see <see cref="ThrustConst"/> before moving any of them.
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
    // aerodynamic incidence (BL-247 B11); observation-only until B12/C21 key lift/drag on it.
    public float Alpha;

    // m/s² per engine-power unit per tonne. NOT a free TUNE, but NOT independently measurable
    // either — it is only ever pinned *jointly with the drag shape below*, and that is the trap the
    // old value fell into. 184 was solved from the level acceleration (150 → 290 mph in 3.76 sim s)
    // and cross-checked against the terminal dive, both of which it fits — but only while drag was
    // the lerp(x², x, 0.35) blend. `CAP-05`'s zero-thrust clip then measured the low-speed drag
    // directly (0.36/1.11/2.82/3.74 m/s² at x = 0.25/0.35/0.46/0.50, with NO thrust term assumed),
    // and at A = 60 no drag curve satisfies that AND the acceleration: weak low-speed drag
    // accelerates the plane in ~2.0 s against the measured 3.76.
    // Refitting the three together — CAP-05's four points, the acceleration, and the dive —
    // converges at A = 34.9 m/s² on the Bloodhawk, i.e. 34.9·1.9/0.62. All four drag points then
    // land within 4% and the acceleration within 0.01 s.
    // ⚠ Solve it from the plane's OWN stock engine power, not the level-1 row: the Bloodhawk's
    // 'engine' is 11 (Lvl-2, 0.62), and using 0.47 back-derives a constant 32% too big.
    // ⚠ Do not retune this alone. Moving it without refitting DragExpLow/DragExpHigh silently
    // breaks whichever of the three measurements you were not looking at.
    private const float ThrustConst = 107f;
    // Thrust is SUBLINEAR in throttle: thrust = A · throttle^ThrottleExp. Determined, not guessed —
    // with the drag curve pinned by CAP-05 (which contains no thrust term at all), the original's
    // measured 1/8-throttle level equilibrium of 137.9 mph fixes this exponent as the only free
    // quantity left. Full throttle is 1^k = 1 for any k, so every full-throttle scenario the suite
    // asserts is untouched by construction; this term is observable only at part throttle.
    // (The old model was linear, which is why 1/8 throttle settled at 86 mph against 137.9.)
    private const float ThrottleExp = 1.236f;
    private const float AlignRate = 4f;           // TUNE: how fast velocity chases the nose at lift speed, 1/s
    private const float MinControlEff = 0.25f;    // TUNE: control authority floor at low speed
    private const float MaxControlEff = 1.15f;    // TUNE: authority ceiling in a dive
    private const float LiftSpeedFrac = 0.40f;    // TUNE: full lift at/above this fraction of fd_speed
                                                  // (0.40·135 = 54 m/s keeps the 120 mph spawn fully lifted)

    // The two stall thresholds are DIFFERENT numbers and both are measured, not TUNEs. The nose does
    // not break until 0.25 fd (the original's "Stall 0% Thrust no input" clip: the nose holds +4.2°
    // all the way down to 76 mph, then falls), while the STALL lamp lights at 0.30 fd
    // (0.2989–0.2996 across four clips). Confirmed inside a single clip — the warning leads the break
    // by 2.64 sim s / 14.9 mph — so any model driving both cues off one number is wrong by
    // construction.
    private const float StallSpeedFrac = 0.25f;   // nose-drop begins below this fraction of fd_speed
    private const float StallWarnFrac = 0.30f;    // STALL lamp lights below this fraction of fd_speed
    private const float MaxDiveSpeedFrac = 1.75f; // numerical backstop, NOT a terminal speed. The
                                                  // terminal dive is emergent from the drag curve
                                                  // and lands within 0.3% of the original, so a cap
                                                  // that binds would replace a measured value with a
                                                  // guess. Set above every airframe's own emergent
                                                  // terminal — the worst is the Balmoral, a bomber
                                                  // at A = 7.6 m/s², which reaches 1.590 in a 71°
                                                  // dive (--dump-flight=player_balmoral) — so it
                                                  // only ever catches the loop energy pump or a dt
                                                  // spike. (Was 1.678 at A = 13 under the old
                                                  // lerp(x², x, 0.35) blend; the refitted DragExpHigh
                                                  // is steeper above fd, so every airframe's
                                                  // terminal moved DOWN and this cap sits further
                                                  // clear than it did.)
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
                                                  // Drag is a power law in x = V/fd_speed, normalized so D(fd) = A — which is what makes the
                                                  // full-throttle level equilibrium exactly fd_speed for ANY exponent, and therefore what makes
                                                  // that scenario useless for validating the shape (the low-throttle end is the only place the
                                                  // shape is observable). The exponent DIFFERS either side of fd, and both halves are measured:
                                                  //
                                                  //   below fd — `CAP-05 Stall 0% Thrust no input` is a zero-thrust deceleration, so dV/dt is
                                                  //   drag plus gravity and nothing else: the cleanest drag probe in the set. Measured D is
                                                  //   0.36 / 1.11 / 2.82 / 3.74 m/s² at x = 0.25 / 0.35 / 0.46 / 0.50. Fitted jointly with
                                                  //   ThrustConst and the acceleration, that gives 3.278 and lands all four within 4%.
                                                  //
                                                  //   above fd — the terminal dive: 70.7° at full throttle settles at 355.2 mph = 1.176 × fd.
                                                  //
                                                  // ⚠ These two CANNOT be one exponent. Forcing the low-speed value everywhere puts the terminal
                                                  // dive at 344.5 mph against a measured 355.2 ± 6 — outside by 1.8× the tolerance. The seam sits
                                                  // at x = 1 precisely because that is the normalization point, where D = A from either side, so
                                                  // the curve is continuous across it; only its slope steps. What the step MEANS is unexplained —
                                                  // the original's own acceleration fall-off near fd is likewise steeper than a single power law
                                                  // fits, and a soft governor near fd_speed is the standing hypothesis (BL-148). Recorded as a
                                                  // measurement, not as a mechanism.
                                                  //
                                                  // ⚠ This curve is far weaker below cruise than the lerp(x², x, 0.35) blend it replaced (4–21×
                                                  // at the measured points). That blend existed to answer a user report that a throttled-back
                                                  // plane barely decelerated — so this is the direction of that complaint, and it is owed a
                                                  // re-playtest against it specifically. The defence is that the ORIGINAL decelerates slowly too:
                                                  // 290 → 150 mph at 1/8 throttle takes it 7.04 sim s where the old model took 2.47.
    private const float DragExpLow = 3.278f;      // below fd_speed — CAP-05's thrust-free probe
    private const float DragExpHigh = 2.663f;     // at/above fd_speed — the terminal dive

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
    private const float YawTune = 1.32f;          // TUNE: pinned (at cruise eff)
    private const float RollTune = 2.12f;         // TUNE: pinned

    private readonly float _maxThrustAccel;       // m/s² at full throttle

    public FlightModel(PlaneStats stats)
    {
        Stats = stats;
        _maxThrustAccel = stats.EnginePower * Config.GetFloat("flightModel.thrustConst", ThrustConst)
                          / (stats.VehWeight / 1000f);
    }

    public PlaneStats Stats { get; }

    /// <summary>Airspeed as a fraction of fd_speed — the single stall-proximity scale both stall
    /// thresholds are measured on, and the one the STALL lamp's blink rate ramps over. Every stall
    /// cue derives from this; nothing recomputes its own margin.</summary>
    public float StallFraction => Stats.FdSpeed > 0f ? Speed / Stats.FdSpeed : 0f;

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
        float minControlEff = Config.GetFloat("flightModel.minControlEff", MinControlEff);
        float maxControlEff = Config.GetFloat("flightModel.maxControlEff", MaxControlEff);
        float pitchTune = Config.GetFloat("flightModel.pitchTune", PitchTune);
        float yawTune = Config.GetFloat("flightModel.yawTune", YawTune);
        float rollTune = Config.GetFloat("flightModel.rollTune", RollTune);
        float stallSpeedFrac = Config.GetFloat("flightModel.stallSpeedFrac", StallSpeedFrac);
        // Read here as well as at its own site (IsStallWarned, which Step never calls) purely so the
        // key registers on a launch that never flies — --dump-config's template and the orphan check.
        _ = Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);
        float stallNoseRate = Config.GetFloat("flightModel.stallNoseRate", StallNoseRate);
        float climbGravityScale = Config.GetFloat("flightModel.climbGravityScale", ClimbGravityScale);
        float knifeAlignFloor = Config.GetFloat("flightModel.knifeAlignFloor", KnifeAlignFloor);
        float knifeNoseSag = Config.GetFloat("flightModel.knifeNoseSag", KnifeNoseSag);
        float knifeNoseRate = Config.GetFloat("flightModel.knifeNoseRate", KnifeNoseRate);
        float dragExpLow = Config.GetFloat("flightModel.dragExpLow", DragExpLow);
        float dragExpHigh = Config.GetFloat("flightModel.dragExpHigh", DragExpHigh);
        float throttleExp = Config.GetFloat("flightModel.throttleExp", ThrottleExp);
        float liftSpeedFrac = Config.GetFloat("flightModel.liftSpeedFrac", LiftSpeedFrac);
        float alignRate = Config.GetFloat("flightModel.alignRate", AlignRate);
        float altitudeCapM = Config.GetFloat("flightModel.altitudeCapM", AltitudeCapM);
        float altitudeCapOvershootM = Config.GetFloat("flightModel.altitudeCapOvershootM", AltitudeCapOvershootM);

        // --- rotation: torque·recInertia vs momentum damping (all from the dynamics block).
        // Control surfaces bite proportionally to airspeed; return_rate adds extra
        // centering on an axis while its stick is released.
        // Inverted eff. Turns faster the slower the plane is. Still not same as original
        float eff = 1.4f - Mathf.Clamp(Speed / s.FdSpeed, minControlEff, maxControlEff);
        var cmd = new Vector3(
            Mathf.Clamp(input.Pitch, -1f, 1f) * s.PitchTorque * s.RecInertia.X * pitchTune,
            //eff only works on Yaw like the original
            Mathf.Clamp(input.Yaw, -1f, 1f) * s.RudderTorque * s.RecInertia.Y * yawTune * eff,
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
        float stallSpeed = stallSpeedFrac * s.FdSpeed;
        bool stalled = isStalled();
        float noseYBefore = (-Attitude.Z).Y;  // the nose's world elevation entering this frame
        if (stalled)
        {
            float depth = 1f - Speed / stallSpeed;
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

        // knife-edge nose sag: with the wings vertical they carry nothing, and the nose
        // falls as well as the flight path — the original drops it, and without this block
        // the plane would descend wings-level-nosed because BOTH knife-edge terms (liftFrac
        // and the nose-chase) act on VelocityDir and nothing else touches Attitude. Same great-circle
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
        // so lift, drag and align could all key on the SAME value once B12/C21 re-key them (they
        // don't yet: this is observation-only). Hoisted out of the near-parallel pathDot check
        // below rather than replacing it: that one re-reads pathDot AFTER the translation update,
        // to decide whether Slerp's cross-product axis is well-conditioned for THIS frame's actual
        // chase, which is a distinct question from what α reports here.
        // ⚠ α is an emergent LAG in this model (the flight path chasing the nose at a finite rate),
        // not an aerodynamic state — do not describe it as modelled incidence. Near-parallel nose
        // and path is the normal cruise state, so this must stay well-defined as α → 0: Acos of a
        // clamped dot product is, unlike Slerp's axis, safe at zero.
        Alpha = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f)));

        // lift fraction: quadratic in speed up to the lift speed, scaled by how much of
        // the wings' lift points vertically — |up·Y| is 1 level OR inverted (arcade:
        // inverted flight still carries), 0 in knife-edge (near-ballistic, nose sags).
        float liftSpeed = liftSpeedFrac * s.FdSpeed;
        float speedLift = Mathf.Min(1f, (Speed / liftSpeed) * (Speed / liftSpeed));
        float wingVert = Mathf.Abs(Attitude.Y.Dot(Vector3.Up));
        float liftFrac = speedLift * wingVert;

        // thrust pulls along the nose (its along-path share falls out of the vector sum —
        // a stalled plane falling nose-high needs no special case), and is sublinear in the
        // throttle lever. Drag opposes the motion: a power law in x = V/fd_speed, normalized so
        // drag(fd_speed) = max thrust, with a measured slope change at fd (see the constants).
        // At v ≈ 0 drag → 0 with speed, so the stale direction there is harmless.
        // Gravity splits about the path: the
        // cross-path component is what lift cancels (its deficit is the sink — vanishes at
        // full lift, drops the plane when slow or knife-edge); the along-path component
        // bleeds/returns speed — reduced climbing (climb retention: the original bleeds
        // noticeably less speed in a sustained climb), full when diving.
        float xSpd = Speed / s.FdSpeed;
        float dragAccel = _maxThrustAccel
                          * Mathf.Pow(xSpd, xSpd < 1f ? dragExpLow : dragExpHigh);
        var gravity = Vector3.Down * s.Gravity;
        var gAlong = VelocityDir * gravity.Dot(VelocityDir);
        var gAcross = gravity - gAlong;
        var accel = nose * (Mathf.Pow(Throttle, throttleExp) * _maxThrustAccel)
                    - VelocityDir * dragAccel
                    + gAlong * (VelocityDir.Y > 0f ? climbGravityScale : 1f)
                    + gAcross * (1f - liftFrac);
        var vel = VelocityDir * Speed + accel * dt;
        Speed = Mathf.Min(vel.Length(), MaxDiveSpeedFrac * s.FdSpeed);
        if (vel.LengthSquared() > 1e-8f)
            VelocityDir = vel.Normalized();

        // velocity chases the nose, weakening with airspeed (controls mush as the
        // airflow dies, and a stalled plane keeps falling wherever momentum takes it)
        // and with wing verticality (in knife-edge the wings can't lift the path back
        // to the nose, so the sag equilibrium sits visibly below it — the nose-drop).
        // (Skip when path ≈ opposite the nose — slerp axis degenerates; gravity will
        // swing the path around within a few frames anyway.)
        float align = alignRate * Mathf.Clamp(Speed / liftSpeed, 0f, 1f)
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

    /// <summary>Below the nose-drop threshold (0.25 fd) — the aerodynamic stall the flight model
    /// flies. NOT the cue the STALL lamp shows: that one lights earlier, see IsStallWarned.</summary>
    public bool isStalled() =>
        StallFraction < Config.GetFloat("flightModel.stallSpeedFrac", StallSpeedFrac);

    /// <summary>Below the warning threshold (0.30 fd) — the STALL lamp, which leads the break by a
    /// measured 2.64 sim s / 14.9 mph.</summary>
    public bool IsStallWarned() =>
        StallFraction < Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);
}
