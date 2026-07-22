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
/// (alignment lag). Below stall speed the nose is additionally pulled toward
/// world-down and cannot be raised over the horizon. Thrust vs drag (quadratic
/// + linear blend) gives the level-speed equilibrium at fd_speed. The torque/
/// damping/inertia/speed numbers come straight from vehicle.json 'dynamics';
/// only the scale constants marked TUNE are ours, adjusted against playtests.
/// </summary>
public sealed class FlightModel
{
    public Vector3 Position;
    public Basis Attitude = Basis.Identity;       // body→world; nose −Z, up +Y (Godot frame)
    public Vector3 BodyRates;                     // rad/s: x pitch(+up), y yaw(+left), z roll(+left)
    public Vector3 VelocityDir = Vector3.Forward;
    public float Speed;                           // m/s along VelocityDir
    public float Throttle;

    public PlaneStats Stats { get; }

    private readonly float _maxThrustAccel;       // m/s² at full throttle

    private const float ThrustConst = 40f;        // TUNE: m/s² per engine-power unit per tonne
    private const float AlignRate = 4f;           // TUNE: how fast velocity chases the nose at lift speed, 1/s
    private const float MinControlEff = 0.25f;    // TUNE: control authority floor at low speed
    private const float MaxControlEff = 1.15f;    // TUNE: authority ceiling in a dive
    private const float LiftSpeedFrac = 0.40f;    // TUNE: full lift at/above this fraction of fd_speed
                                                  // (0.40·135 = 54 m/s keeps the 120 mph spawn fully lifted)
    private const float StallSpeedFrac = 0.30f;   // TUNE: nose-drop begins below this fraction
    private const float MaxDiveSpeedFrac = 1.7f;  // hard cap (≈ terminal dive from the drag curve)
    private const float StallNoseRate = 1.0f;     // TUNE: rad/s toward world-down at full stall depth (× stall_mag)
    private const float ClimbGravityScale = 0.6f; // TUNE: climb retention — a climb bleeds less speed than
                                                  // plain energy exchange (the original holds speed better)
    private const float KnifeAlignFloor = 0.35f;  // TUNE: fraction of the nose-chase that survives at 90°
                                                  // bank — the chase is the lift force turning the velocity,
                                                  // so it weakens with wing verticality (deeper knife-edge sag)
    private const float LowSpeedDragBlend = 0.35f;// TUNE: fraction of the drag that is linear in speed. A pure
                                                  // v² curve dies off so fast below cruise that a throttled-back
                                                  // plane barely decelerated (user report); the linear share
                                                  // keeps air resistance biting at low speed. The full-throttle
                                                  // equilibrium stays exactly fd_speed for any blend value.

    // Per-axis control-rate calibration (Run-2 item 12), replacing the old global ×2.
    // Steady rate = torque · recInertia · Tune / ang_momentum_damp (× eff on yaw), and a
    // full 360° takes ≈ 1/damp spin-up + 2π/rate. Solved against the user's stopwatch
    // measurements of the original (Bloodhawk, full throttle): 360° roll in 2 s
    // (3.49 rad/s), sustained full-pitch 360° at 90° bank in 11 s (0.58 rad/s — the
    // original also bleeds speed in that turn; whether its pitch rate slows with speed
    // is an open fidelity question, ours is constant), full-rudder 360° in 30 s
    // (0.21 rad/s at cruise, where eff = 0.4).
    private const float PitchTune = 0.75f;        // TUNE: calibrated 2026-07-19
    private const float YawTune = 1.32f;          // TUNE: calibrated 2026-07-19 (at cruise eff)
    private const float RollTune = 2.12f;         // TUNE: calibrated 2026-07-19

    public FlightModel(PlaneStats stats)
    {
        Stats = stats;
        _maxThrustAccel = stats.EnginePower * ThrustConst / (stats.VehWeight / 1000f);
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

        // --- rotation: torque·recInertia vs momentum damping (all from the dynamics block).
        // Control surfaces bite proportionally to airspeed; return_rate adds extra
        // centering on an axis while its stick is released.
        // Inverted eff. Turns faster the slower the plane is. Still not same as original
        float eff = 1.4f - Mathf.Clamp(Speed / s.FdSpeed, MinControlEff, MaxControlEff);
        var cmd = new Vector3(
            Mathf.Clamp(input.Pitch, -1f, 1f) * s.PitchTorque * s.RecInertia.X * PitchTune,
            //eff only works on Yaw like the original
            Mathf.Clamp(input.Yaw, -1f, 1f) * s.RudderTorque * s.RecInertia.Y * YawTune * eff,
            Mathf.Clamp(input.Roll, -1f, 1f) * s.RollTorque * s.RecInertia.Z * RollTune);
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
        float stallSpeed = StallSpeedFrac * s.FdSpeed;
        bool stalled = isStalled();
        float noseYBefore = (-Attitude.Z).Y;  // the nose's world elevation entering this frame
        if (stalled)
        {
            float depth = 1f - Speed / stallSpeed;
            var noseNow = -Attitude.Z;
            var axis = noseNow.Cross(Vector3.Down);
            if (axis.LengthSquared() > 1e-8f)
            {
                float angle = Mathf.Min(s.StallMag * StallNoseRate * depth * dt,
                                        noseNow.AngleTo(Vector3.Down));
                Attitude = Attitude.Rotated(axis.Normalized(), angle).Orthonormalized();
            }
        }

        var omegaWorld = Attitude * BodyRates;
        float omega = omegaWorld.Length();
        if (omega > 1e-6f)
            Attitude = Attitude.Rotated(omegaWorld / omega, omega * dt).Orthonormalized();

        // while stalled the nose can NOT be raised over the horizon, at any bank angle
        // (original behavior, user-observed 2026-07-17): cap its world elevation at the
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

        // --- translation: forces integrate on the velocity VECTOR (v = VelocityDir·Speed),
        // so the speed can pass through zero — a vertical zoom tail-slides out downward
        // instead of freezing mid-air at a clamped 0 (a plane visibly stopped in the air
        // while the HUD mph crept back up, user-reported).
        var nose = -Attitude.Z;

        // lift fraction: quadratic in speed up to the lift speed, scaled by how much of
        // the wings' lift points vertically — |up·Y| is 1 level OR inverted (arcade:
        // inverted flight still carries), 0 in knife-edge (near-ballistic, nose sags).
        float liftSpeed = LiftSpeedFrac * s.FdSpeed;
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
        float dragAccel = _maxThrustAccel * Mathf.Lerp(xSpd * xSpd, xSpd, LowSpeedDragBlend);
        var gravity = Vector3.Down * s.Gravity;
        var gAlong = VelocityDir * gravity.Dot(VelocityDir);
        var gAcross = gravity - gAlong;
        var accel = nose * (Throttle * _maxThrustAccel)
                    - VelocityDir * dragAccel
                    + gAlong * (VelocityDir.Y > 0f ? ClimbGravityScale : 1f)
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
        float align = AlignRate * Mathf.Clamp(Speed / liftSpeed, 0f, 1f)
                      * (KnifeAlignFloor + (1f - KnifeAlignFloor) * wingVert);
        // Near-parallel is the normal cruise state, and there Slerp is unusable: it builds its
        // rotation axis from the cross product, whose float error swamps a sub-degree angle, and
        // Godot then throws "Argument is not normalized" — which aborts the whole physics frame,
        // so a plane holding straight and level simply stopped flying (found 2026-07-19 while
        // verifying splitscreen; it bit single player exactly the same). Under ~2.5° a normalized
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
        float stallSpeed = StallSpeedFrac * Stats.FdSpeed;
        return Speed < stallSpeed;
    }
}
