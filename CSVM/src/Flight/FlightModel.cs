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
/// (alignment lag). In a knife-edge the nose itself also sags to a bounded angle
/// below the horizon, so the plane noses down as it sinks rather than descending
/// wings-level-nosed. Below stall speed the nose is additionally pulled toward
/// world-down and cannot be raised over the horizon. Thrust vs drag (quadratic
/// + linear blend) gives the level-speed equilibrium at fd_speed. The torque/
/// damping/inertia/speed numbers come straight from vehicle.json 'dynamics';
/// the scale constants marked TUNE are ours, adjusted against playtests — except
/// ThrustConst and the three *Tune rates, which are pinned to measurements of the
/// original decoded from cockpit-gauge video and must not be retuned by feel.
/// </summary>
public sealed class FlightModel
{
    public Vector3 Position;
    public Basis Attitude = Basis.Identity;       // body→world; nose −Z, up +Y (Godot frame)
    public Vector3 BodyRates;                     // rad/s: x pitch(+up), y yaw(+left), z roll(+left)
    public Vector3 VelocityDir = Vector3.Forward;
    public float Speed;                           // m/s along VelocityDir
    public float Throttle;

    // m/s² per engine-power unit per tonne. NOT a free TUNE — the original's own level
    // acceleration pins it: full throttle 150 → 290 mph in 3.76 sim s (decoded from cockpit-gauge
    // video) needs A = 60 m/s² on the Bloodhawk, which at 0.62 engine power and 1.9 t is 60·1.9/0.62.
    // The terminal dive is the independent check on that same number — it puts a 71° dive at
    // 1.178 × fd_speed against the video's measured 1.182, which is what says the drag SHAPE below
    // is right and this scale was the only thing wrong.
    // ⚠ Solve it from the plane's OWN stock engine power, not the level-1 row: the Bloodhawk's
    // 'engine' is 11 (Lvl-2, 0.62), and using 0.47 back-derives a constant 32% too big.
    private const float ThrustConst = 184f;
    private const float AlignRate = 4f;           // TUNE: how fast velocity chases the nose at lift speed, 1/s
    private const float MinControlEff = 0.25f;    // TUNE: control authority floor at low speed
    private const float MaxControlEff = 1.15f;    // TUNE: authority ceiling in a dive
    private const float LiftSpeedFrac = 0.40f;    // TUNE: full lift at/above this fraction of fd_speed
                                                  // (0.40·135 = 54 m/s keeps the 120 mph spawn fully lifted)
    private const float StallSpeedFrac = 0.30f;   // TUNE: nose-drop begins below this fraction
    private const float MaxDiveSpeedFrac = 1.75f; // numerical backstop, NOT a terminal speed. The
                                                  // terminal dive is emergent from the drag curve
                                                  // and lands within 0.3% of the original, so a cap
                                                  // that binds would replace a measured value with a
                                                  // guess. Set above every airframe's own emergent
                                                  // terminal — the worst is the Balmoral, a bomber
                                                  // at A = 13 m/s², which reaches 1.678 in a 71°
                                                  // dive (--dump-flight=player_balmoral) and ~1.71
                                                  // vertical — so it only ever catches the loop
                                                  // energy pump or a dt spike.
    private const float StallNoseRate = 1.0f;     // TUNE: rad/s toward world-down at full stall depth (× stall_mag)
    private const float ClimbGravityScale = 0.6f; // TUNE: climb retention — a climb bleeds less speed than
                                                  // plain energy exchange (the original holds speed better)
    private const float KnifeAlignFloor = 0.35f;  // TUNE: fraction of the nose-chase that survives at 90°
                                                  // bank — the chase is the lift force turning the velocity,
                                                  // so it weakens with wing verticality (deeper knife-edge sag)
    private const float KnifeNoseSag = 0.07f;     // TUNE: rad (≈4°) the NOSE settles below the horizon at full
                                                  // knife-edge. This is a BOUND, not a rate, and it has to be:
                                                  // the path chases the nose, so an unbounded nose-down term
                                                  // (toward world-down, or weathervaning onto the path) has no
                                                  // equilibrium at all — nose and path descend together at
                                                  // (g/v)·K/(K+align) forever and the plane spirals in. Bounding
                                                  // the nose bounds the path with it. The sag also carries into
                                                  // the path roughly 1:1 for that same reason, so this value is
                                                  // not free: 0.07 (4° nose) settles the path at exactly the
                                                  // −10° `docs/HISTORY.md` records as the designed knife-edge
                                                  // sink, where 0.14 (8° nose) took it to −13°.
    private const float KnifeNoseRate = 0.2f;     // TUNE: rad/s toward that sag at full knife-edge, ×(1−wingVert)
                                                  // — exactly 0 wings-level or inverted, so cruise is untouched by
                                                  // construction. A RATE CAP, not an exponential approach: an
                                                  // exponential's rate scales with the displacement, which at a
                                                  // +62° stalled-zoom nose came out ~32°/s — rivalling the 33°/s
                                                  // full elevator, and it measurably rewrote the stall recovery
                                                  // (nose +62°→+28°, wv 0.47→0.88). Clamped to the remaining
                                                  // angle so it approaches the sag and stops, never overshoots.
    private const float LowSpeedDragBlend = 0.35f;// TUNE: fraction of the drag that is linear in speed. A pure
                                                  // v² curve dies off so fast below cruise that a throttled-back
                                                  // plane barely decelerated (user report); the linear share
                                                  // keeps air resistance biting at low speed. The full-throttle
                                                  // equilibrium stays exactly fd_speed for any blend value.

    // Per-axis control-rate calibration. Steady rate = torque · recInertia · Tune /
    // ang_momentum_damp (× eff on yaw), and a full 360° takes ≈ 1/damp spin-up + 2π/rate.
    // Fitted to stopwatch timings of the original, then confirmed against cockpit-gauge video of
    // it: 360° roll 2.05 s, sustained pitch ~33 °/s, full-rudder 360° 28.6 s — all three within a
    // few percent of what these values already gave, and none of the verdicts moves anywhere
    // inside the video's clock uncertainty. The video also settles what was an open question: the
    // original's pitch rate does NOT fall off with speed (37.9 / 33.7 / 30.7 / 36.5 °/s binned
    // over 120–280 mph round a loop, flat within the noise), so speed-independent pitch is right.
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
        float stallNoseRate = Config.GetFloat("flightModel.stallNoseRate", StallNoseRate);
        float climbGravityScale = Config.GetFloat("flightModel.climbGravityScale", ClimbGravityScale);
        float knifeAlignFloor = Config.GetFloat("flightModel.knifeAlignFloor", KnifeAlignFloor);
        float knifeNoseSag = Config.GetFloat("flightModel.knifeNoseSag", KnifeNoseSag);
        float knifeNoseRate = Config.GetFloat("flightModel.knifeNoseRate", KnifeNoseRate);
        float lowSpeedDragBlend = Config.GetFloat("flightModel.lowSpeedDragBlend", LowSpeedDragBlend);
        float liftSpeedFrac = Config.GetFloat("flightModel.liftSpeedFrac", LiftSpeedFrac);
        float alignRate = Config.GetFloat("flightModel.alignRate", AlignRate);

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
        // falls as well as the flight path — the original drops it, we used to descend
        // wings-level-nosed because BOTH knife-edge terms (liftFrac and the nose-chase)
        // act on VelocityDir and nothing ever touched Attitude. Same great-circle
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
        // interaction the plan warned about provably empty rather than merely benign.
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

        // lift fraction: quadratic in speed up to the lift speed, scaled by how much of
        // the wings' lift points vertically — |up·Y| is 1 level OR inverted (arcade:
        // inverted flight still carries), 0 in knife-edge (near-ballistic, nose sags).
        float liftSpeed = liftSpeedFrac * s.FdSpeed;
        float speedLift = Mathf.Min(1f, (Speed / liftSpeed) * (Speed / liftSpeed));
        float wingVert = Mathf.Abs(Attitude.Y.Dot(Vector3.Up));
        float liftFrac = speedLift * wingVert;

        // thrust pulls along the nose (its along-path share falls out of the vector sum —
        // a stalled plane falling nose-high no longer needs a special case). Drag opposes
        // the motion: quadratic + linear blend, normalized so drag(fd_speed) = max thrust —
        // the linear share is the low-speed bite (throttle back and the plane visibly slows
        // toward the stall instead of coasting on a near-zero v² tail; it → 0 with speed,
        // so the stale direction at v ≈ 0 is harmless). Gravity splits about the path: the
        // cross-path component is what lift cancels (its deficit is the sink — vanishes at
        // full lift, drops the plane when slow or knife-edge); the along-path component
        // bleeds/returns speed — reduced climbing (climb retention: the original bleeds
        // noticeably less speed in a sustained climb), full when diving.
        float xSpd = Speed / s.FdSpeed;
        float dragAccel = _maxThrustAccel * Mathf.Lerp(xSpd * xSpd, xSpd, lowSpeedDragBlend);
        var gravity = Vector3.Down * s.Gravity;
        var gAlong = VelocityDir * gravity.Dot(VelocityDir);
        var gAcross = gravity - gAlong;
        var accel = nose * (Throttle * _maxThrustAccel)
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

        Position += VelocityDir * Speed * dt;
    }

    public bool isStalled()
    {
        float stallSpeed = Config.GetFloat("flightModel.stallSpeedFrac", StallSpeedFrac) * Stats.FdSpeed;
        return Speed < stallSpeed;
    }
}
