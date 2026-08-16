using Godot;

namespace CSVM.Flight;

/// <summary>One row of the original's four AI steering parameter tables, read out of the image at
/// <c>0x61fb08</c>/<c>0x61fb28</c>/<c>0x61fb48</c>/<c>0x61fb68</c> and decoded in
/// <c>docs/org/aiControlLaw.md</c>. Eight floats there; the eighth is 0.0 in all four tables and is
/// never read, so it has no field here.</summary>
public readonly struct AiLawParams
{
    /// <summary>The engaged-pursuit table (<c>0x61fb08</c>): the widest throttle band and the
    /// loosest deadbands, used while the combat driver is actually chasing.</summary>
    public static readonly AiLawParams Engaged = new(0.3f, 1.3f, 0.08f, 0.01f, 0.2f, 0.025f, 0.9f);

    /// <summary>The wingman table (<c>0x61fb28</c>), the class-4 driver's.</summary>
    public static readonly AiLawParams Wingman = new(0.4f, 1.5f, 0.06f, 0.06f, 0.15f, 0.025f, 0.35f);

    /// <summary>The avoid-crash table (<c>0x61fb48</c>), always paired with the emergency arm.</summary>
    public static readonly AiLawParams AvoidCrash = new(0.6f, 1.3f, 0.06f, 0.06f, 0.15f, 0.025f, 0.35f);

    /// <summary>The cruise table (<c>0x61fb68</c>): patrol, and the combat driver while breaking
    /// off. A high throttle floor and a narrow speed cap.</summary>
    public static readonly AiLawParams Cruise = new(0.8f, 1.1f, 0.06f, 0.06f, 0.15f, 0.025f, 0.35f);

    public AiLawParams(float throttleMin, float speedCap, float crossDeadband, float levelDeadband,
        float leadAlong, float leadCross, float flipSpeed)
    {
        ThrottleMin = throttleMin;
        SpeedCap = speedCap;
        CrossDeadband = crossDeadband;
        LevelDeadband = levelDeadband;
        LeadAlong = leadAlong;
        LeadCross = leadCross;
        FlipSpeed = flipSpeed;
    }

    /// <summary><c>params[0]</c>: the throttle lever's floor.</summary>
    public float ThrottleMin { get; }

    /// <summary><c>params[1]</c>: the desired speed's cap as a multiple of <c>fd_speed</c>, AND the
    /// throttle lever's ceiling. One slot doing both jobs is the original's, not a shortcut here.
    /// Three of the four tables put it above 1.0, so an AI's commanded lever can exceed full.</summary>
    public float SpeedCap { get; }

    /// <summary><c>params[2]</c>: how small the roll command must be before the secondary channel
    /// (elevator, or rudder astern) is allowed to move at all.</summary>
    public float CrossDeadband { get; }

    /// <summary><c>params[3]</c>: the vertical half of the wings-level rule's gate.</summary>
    public float LevelDeadband { get; }

    /// <summary><c>params[4]</c>: desired speed added per metre of separation ALONG the aim point's
    /// own velocity.</summary>
    public float LeadAlong { get; }

    /// <summary><c>params[5]</c>: desired speed added per metre of separation ACROSS it.</summary>
    public float LeadCross { get; }

    /// <summary><c>params[6]</c>: below this horizontal aim magnitude, a below-the-nose aim point
    /// gets its lateral term mirrored rather than saturated.</summary>
    public float FlipSpeed { get; }
}

/// <summary>The original's AI steering law (<c>FUN_0041b560</c>), decoded in
/// <c>docs/org/aiControlLaw.md</c> (plan D31) and ported here. An aim point and that point's
/// velocity in, one <see cref="FlightInput"/> out: desired speed from the aim point's own speed
/// plus range-weighted lead terms, an intercept solve for the direction, bank-to-turn with an
/// elevator pull once the bank is nearly satisfied, and a per-axis scale/limit output stage.
/// Engine-free and pure over its arguments, so a fixed-dt run is deterministic. Two pieces of the
/// original are deliberately unported (docs/architecture.md): the emergency arm's altitude/velocity
/// assist and the intercept solver's second-root preference.</summary>
public static class AiControlLaw
{
    /// <summary>The desired speed's floor, 50 mph.</summary>
    public const float MinDesiredSpeed = 22.352f;

    /// <summary>How far the desired speed may sit either side of the aim point's own speed, 60 mph.</summary>
    public const float DesiredSpeedBand = 26.8224f;

    /// <summary>The desired speed used when the aim point is not moving, 180 mph.</summary>
    public const float StaticAimSpeed = 80.4672f;

    /// <summary>The hard ceiling on desired speed, 250 mph. ⚠ Not per-airframe: the def slots
    /// behind it carry no parser token and the def initialiser fixes them at 0 and this, so every
    /// AI aircraft in the original shares one 250 mph ceiling regardless of how fast its airframe
    /// is. On the quicker fighters this binds well before <c>fd_speed · SpeedCap</c> does.</summary>
    public const float SpeedCeiling = 111.76f;

    /// <summary>The floor the aim point's altitude is held above, a compiled 20 m global.</summary>
    public const float AimAltitudeFloor = 20f;

    /// <summary>How fast the commanded throttle walks toward the desired speed, per second.</summary>
    public const float ThrottleRatePerS = 0.35f;

    /// <summary>Past this range from the player the throttle is set open loop from the desired
    /// speed instead of walked toward it.</summary>
    public const float OpenLoopPlayerRange = 2000f;

    /// <summary>The projectile speed the head-on firing solution is solved at, a hard immediate in
    /// the original rather than a real round's speed.</summary>
    public const float GunSolutionSpeed = 860f;

    /// <summary>Nose-below-horizon component that arms the low-speed recovery.</summary>
    public const float RecoveryNoseY = -0.5f;

    /// <summary>Speed below which the low-speed recovery arms, 60 mph.</summary>
    public const float RecoverySpeed = 26.8224f;

    /// <summary>The roll authority the wings-level rule commands.</summary>
    public const float LevelAuthority = 0.2f;

    /// <summary>Nose verticality above which the wings-level rule stands down.</summary>
    public const float NearVerticalNoseY = 0.9f;

    /// <summary>Added to every per-axis scale while engaged.</summary>
    public const float EngagedScaleBonus = 0.5f;

    /// <summary>Added to every per-axis limit while engaged.</summary>
    public const float EngagedLimitBonus = 0.25f;

    /// <summary>Added to the skill factor while engaged.</summary>
    public const float EngagedSkillBonus = 0.08f;

    private const float LeadNear = 106.68f;         // 350 ft
    private const float LeadFar = 259.08f;          // 850 ft
    private const float LeadSpeedLo = 20.576f;      // 46 mph
    private const float LeadSpeedHi = 102.880005f;  // 230 mph
    private const float LeadSlope = 1.8516719f;     // metres of offset per m/s between the two

    /// <summary>How far ahead of a target, along the target's own facing, the combat driver puts
    /// the aim point (<c>FUN_0041d9f0</c>): 350 ft at or below 46 mph, ramping linearly to 850 ft
    /// at or above 230 mph. This is the driver's, not the law's, and lives here because it is the
    /// same decode.</summary>
    public static float LeadOffsetFor(float targetSpeed) =>
        targetSpeed <= LeadSpeedLo ? LeadNear
        : targetSpeed >= LeadSpeedHi ? LeadFar
        : LeadNear + (targetSpeed - LeadSpeedLo) * LeadSlope;

    /// <summary>One step's stick and throttle for an aim point. <paramref name="emergency"/> is the
    /// crash-recovery arm; <paramref name="engaged"/> is the combat driver's authority bonus;
    /// <paramref name="gunLead"/> swaps the fly-to solve for a firing solution. <paramref
    /// name="playerPosition"/>, when known, arms the far-field open-loop throttle.</summary>
    public static FlightInput Steer(FlightModel model, Vector3 aimPoint, Vector3 aimVelocity,
        in AiLawParams p, float throttle, float dt, bool emergency = false, bool engaged = false,
        bool gunLead = false, float skillFactor = 1f, Vector3? playerPosition = null)
    {
        var stats = model.Stats;
        var att = model.Attitude;
        var pos = model.Position;
        float noseY = -att.Z.Y;

        // The aim point is held inside the AI's altitude band, and an aim velocity that would carry
        // it further outside is flattened rather than followed.
        if (aimPoint.Y > stats.FlightCeiling)
        {
            aimPoint.Y = stats.FlightCeiling;
            if (aimVelocity.Y > 0f)
                aimVelocity.Y = 0f;
        }
        if (aimPoint.Y < AimAltitudeFloor)
        {
            aimPoint.Y = AimAltitudeFloor;
            if (aimVelocity.Y < 0f)
                aimVelocity.Y = 0f;
        }

        var delta = aimPoint - pos;
        float rangeSq = delta.LengthSquared();
        if (rangeSq < 1e-6f)
            return new FlightInput { Throttle = Mathf.Clamp(throttle, 0f, 1f) };

        float want = DesiredSpeed(stats, delta, rangeSq, aimVelocity, p, emergency, noseY);
        var aimDir = AimDirection(model, pos, aimPoint, aimVelocity, delta, want, gunLead);

        float bx = aimDir.Dot(att.X);   // + = the aim point is to the right
        float by = aimDir.Dot(att.Y);   // + = above
        float bz = aimDir.Dot(-att.Z);  // + = ahead

        // With the aim point ahead, the horizontal pair is renormalised and h pinned to 1. That is
        // what makes the rudder_tol test below select the ORDINARY branch for anything in front:
        // only a target within acos(rudder_tol) of dead astern can leave h small.
        float h = Mathf.Sqrt((bx * bx) + (by * by));
        if (bz > 0f)
        {
            if (h == 0f)
                by = 1f;
            else
            {
                bx /= h;
                by /= h;
            }
            h = 1f;
        }

        float lever = Throttle(model, stats, want, throttle, dt, p, pos, playerPosition);

        // ⚠ CLEARING rudder_tol picks the BANK branch, so a higher rudder_tol means MORE rudder.
        // At the 0.2 default h is 1 for anything ahead, so an aim point in front always banks;
        // balmoral's authored 1.0 can never be exceeded, so its lateral errors go on the rudder.
        float roll, pitch = 0f, yaw = 0f, absBx;
        if (h > stats.RudderTol || Mathf.Abs(bx) <= Mathf.Abs(by))
        {
            // Bank toward the lateral error and let the plant's own bank coupling turn; the
            // elevator only joins once the bank command is nearly satisfied.
            if (by < 0f)
                bx = emergency || h < p.FlipSpeed ? -bx : bx < 0f ? -1f : 1f;
            roll = -bx;
            absBx = Mathf.Abs(bx);
            if (absBx < p.CrossDeadband)
                pitch = by;
        }
        else
        {
            // Dead astern: nothing ahead to bank toward, so the vertical error drives the bank and
            // the lateral error goes on the rudder.
            absBx = Mathf.Abs(bx);
            if (bx < 0f)
                by = -by;
            roll = by;
            if (Mathf.Abs(by) < p.CrossDeadband)
                yaw = -bx;
        }

        // ⚠ The DEAD-ASTERN case, not the straight-ahead one — reads backwards at a glance
        // (docs/architecture.md); AiControlLawTests exists to keep it honest.
        if (!emergency && absBx < p.CrossDeadband && Mathf.Abs(by) < p.LevelDeadband
            && Mathf.Abs(noseY) < NearVerticalNoseY)
        {
            roll = (att.Y.Y >= 0f ? -att.X.Y : att.X.Y < 0f ? 1f : -1f) * LevelAuthority;
        }

        // Steeply nose-down and slow: push the nose further down and firewall the lever, either way
        // up. Unloading to regain flying speed, not a pull-out.
        if (noseY < RecoveryNoseY && model.Speed < RecoverySpeed)
        {
            pitch = att.Y.Y >= 0f ? -1f : 1f;
            lever = p.SpeedCap;
        }

        float scaleBonus = engaged ? EngagedScaleBonus : 0f;
        float limitBonus = engaged ? EngagedLimitBonus : 0f;
        roll = Limit(roll * (stats.AiInputScaleRoll + scaleBonus), stats.AiInputLimitRoll + limitBonus);
        pitch = Limit(pitch * (stats.AiInputScalePitch + scaleBonus), stats.AiInputLimitPitch + limitBonus);
        yaw = Limit(yaw * (stats.AiInputScaleYaw + scaleBonus), stats.AiInputLimitYaw + limitBonus);

        // The skill factor (sixth_sense_factor) eases every channel, and the crash-recovery arm is
        // the one place it does not apply.
        if (!emergency)
        {
            float s = skillFactor + (engaged ? EngagedSkillBonus : 0f);
            roll *= s;
            pitch *= s;
            yaw *= s;
        }

        return new FlightInput { Pitch = pitch, Roll = roll, Yaw = yaw, Throttle = lever };
    }

    private static float DesiredSpeed(PlaneStats stats, Vector3 delta, float rangeSq,
        Vector3 aimVelocity, in AiLawParams p, bool emergency, float noseY)
    {
        if (emergency)
        {
            // Nose up holds the slow target; nose down raises it in proportion to how steep the
            // dive is. The altitude/velocity assist that rides beside this in the original is not
            // ported (see the class remarks).
            return noseY >= 0f ? MinDesiredSpeed : MinDesiredSpeed * (1f - noseY);
        }

        float aimSpeed = aimVelocity.Length();
        float want;
        if (aimSpeed <= 0f)
        {
            want = StaticAimSpeed;
        }
        else
        {
            float along = delta.Dot(aimVelocity / aimSpeed);
            float crossSq = rangeSq - (along * along);
            float cross = crossSq > 0f ? Mathf.Sqrt(crossSq) : 0f;
            want = aimSpeed + (along * p.LeadAlong) + (cross * p.LeadCross);
            want = Mathf.Clamp(want, aimSpeed - DesiredSpeedBand, aimSpeed + DesiredSpeedBand);
        }

        // The airframe cap wins outright when it bites; only under it does the 50 mph floor apply.
        // That order is the original's and it matters when fd_speed · SpeedCap is itself tiny.
        float cap = stats.FdSpeed * p.SpeedCap;
        want = want > cap ? cap : Mathf.Max(want, MinDesiredSpeed);
        return Mathf.Clamp(want, 0f, SpeedCeiling);
    }

    private static Vector3 AimDirection(FlightModel model, Vector3 pos, Vector3 aimPoint,
        Vector3 aimVelocity, Vector3 delta, float want, bool gunLead)
    {
        // gunLead solves the gun problem at a fixed round speed against RELATIVE velocity
        // (docs/org/aiControlLaw.md); a no-root geometry falls back to the straight line.
        var relative = gunLead ? aimVelocity - (model.VelocityDir * model.Speed) : aimVelocity;
        float speed = gunLead ? GunSolutionSpeed : want;
        return AimAssist.TryIntercept(pos, speed, aimPoint, relative, out var dir, out _)
            ? dir
            : delta.Normalized();
    }

    private static float Throttle(FlightModel model, PlaneStats stats, float want, float throttle,
        float dt, in AiLawParams p, Vector3 pos, Vector3? playerPosition)
    {
        float lever;
        bool farFromPlayer = playerPosition is { } pp
            && pos.DistanceSquaredTo(pp) > OpenLoopPlayerRange * OpenLoopPlayerRange;
        if (farFromPlayer && stats.FdSpeed > 1e-3f)
            lever = want / stats.FdSpeed;
        else
            lever = throttle + (want <= model.Speed ? -ThrottleRatePerS * dt : ThrottleRatePerS * dt);
        return lever > p.SpeedCap ? p.SpeedCap : Mathf.Max(lever, p.ThrottleMin);
    }

    private static float Limit(float v, float limit) => Mathf.Clamp(v, -limit, limit);
}
