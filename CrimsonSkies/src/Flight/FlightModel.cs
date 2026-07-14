using Godot;

namespace CrimsonSkies.Flight;

/// <summary>Pilot inputs, all in [-1, 1] except Throttle in [0, 1].
/// Pitch + = pull (nose up), Roll + = bank left, Yaw + = nose left.</summary>
public struct FlightInput
{
    public float Pitch, Roll, Yaw, Throttle;
}

/// <summary>
/// Arcade flight dynamics parameterized by the original game's zrdr stats.
/// The plane flies where the nose points (with an alignment lag and low-speed
/// gravity sag); body rates are driven by control torque × reciprocal inertia
/// against angular-momentum damping. The torque/damping/inertia/speed numbers
/// come straight from vehicle.json 'dynamics'; only the scale constants marked
/// TUNE are ours, to be adjusted against playtests of the original.
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
    private const float AlignRate = 4f;           // TUNE: how fast velocity chases the nose, 1/s
    private const float MinControlEff = 0.25f;    // TUNE: control authority floor at low speed
    private const float MaxControlEff = 1.15f;    // TUNE: authority ceiling in a dive
    private const float LiftSpeedFrac = 0.45f;    // TUNE: full lift above this fraction of fd_speed
    private const float StallSpeedFrac = 0.30f;   // TUNE: nose-drop begins below this fraction
    private const float MaxDiveSpeedFrac = 1.7f;  // hard cap (≈ terminal dive from the drag curve)

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
        float eff = Mathf.Clamp(Speed / s.FdSpeed, MinControlEff, MaxControlEff);
        var cmd = new Vector3(
            Mathf.Clamp(input.Pitch, -1f, 1f) * s.PitchTorque * s.RecInertia.X,
            Mathf.Clamp(input.Yaw, -1f, 1f) * s.RudderTorque * s.RecInertia.Y,
            Mathf.Clamp(input.Roll, -1f, 1f) * s.RollTorque * s.RecInertia.Z) * eff;
        var damp = new Vector3(
            s.AngMomentumDamp + s.ReturnRate * (1f - Mathf.Min(1f, Mathf.Abs(input.Pitch))),
            s.AngMomentumDamp + s.ReturnRate * (1f - Mathf.Min(1f, Mathf.Abs(input.Yaw))),
            s.AngMomentumDamp + s.ReturnRate * (1f - Mathf.Min(1f, Mathf.Abs(input.Roll))));
        BodyRates += (cmd - BodyRates * damp) * dt;

        // stall: below stall speed the nose drops until airspeed recovers
        float stallSpeed = StallSpeedFrac * s.FdSpeed;
        if (Speed < stallSpeed)
            BodyRates.X -= s.StallMag * 2f * (1f - Speed / stallSpeed) * dt;

        var omegaWorld = Attitude * BodyRates;
        float omega = omegaWorld.Length();
        if (omega > 1e-6f)
            Attitude = Attitude.Rotated(omegaWorld / omega, omega * dt).Orthonormalized();

        // --- translation: thrust vs quadratic drag, equilibrium at fd_speed at full
        // throttle; gravity adds/removes speed along the flight path.
        var nose = -Attitude.Z;
        float thrust = Throttle * _maxThrustAccel;
        float drag = _maxThrustAccel * (Speed / s.FdSpeed) * (Speed / s.FdSpeed);
        Speed += (thrust - drag - s.Gravity * VelocityDir.Y) * dt;
        Speed = Mathf.Clamp(Speed, 0f, MaxDiveSpeedFrac * s.FdSpeed);

        if (Speed > 1f)
        {
            // velocity chases the nose; below lift speed gravity sags the flight path
            float t = 1f - Mathf.Exp(-AlignRate * dt);
            VelocityDir = VelocityDir.Slerp(nose, t).Normalized();
            float liftSpeed = LiftSpeedFrac * s.FdSpeed;
            float liftFrac = Mathf.Min(1f, (Speed / liftSpeed) * (Speed / liftSpeed));
            if (liftFrac < 1f)
                VelocityDir = (VelocityDir + Vector3.Down
                    * (s.Gravity * (1f - liftFrac) * dt / Mathf.Max(Speed, 5f))).Normalized();
        }
        else
        {
            VelocityDir = nose;
        }

        Position += VelocityDir * Speed * dt;
    }
}
