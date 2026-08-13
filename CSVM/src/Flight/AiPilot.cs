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
    /// <summary>Ordered heading, degrees — the mission-data convention
    /// (<c>SpawnPoint.HeadingDeg</c>): the nose (−Z) yawed about world up by this angle.</summary>
    public float TargetHeadingDeg;

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
    private const float BankPull = 0.8f;            // extra pull at full bank (lift loss)

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
    /// state and this instance's fields — no clocks, no randomness, no node reads — so a fixed-dt
    /// run is deterministic.</summary>
    public FlightInput Next(FlightModel model, float dt)
    {
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
        // error so the pull relaxes as the course captures; the path term above then re-levels.
        float turnPull = Mathf.Sin(Mathf.Abs(Mathf.DegToRad(bankDeg))) * BankPull
            * Mathf.Clamp(Mathf.Abs(headingErrDeg) / 8f, 0f, 1f);
        float pitch = Mathf.Clamp(
            (wantPathDeg - pathDeg - pitchRateDegS * PitchRateLead) * PitchGain + turnPull,
            -1f, 1f);

        return new FlightInput
        {
            Pitch = pitch,
            Roll = roll,
            Yaw = 0f, // bank carries the turn; the placeholder law never uses rudder
            Throttle = Mathf.Clamp(Throttle, 0f, 1f),
        };
    }
}
