using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>What <see cref="AssistedFlight.Steer"/> carries from one step to the next. The default
/// value is a seat the assist has not flown yet, and the first step primes it from the plane's own
/// state. Hand a fresh default to an aircraft that has just spawned, respawned or switched scheme.
/// </summary>
public struct AssistedState
{
    /// <summary>The flight-path angle a centred stick holds, radians, climb positive.</summary>
    public float HeldClimb;

    /// <summary>The nose climb rate the hold has integrated to carry its bias, rad/s.</summary>
    public float ClimbTrim;

    /// <summary>The turn rate full sideways stick asks for, rad/s: the vertical-speed loop's output.</summary>
    public float TurnCap;

    /// <summary>The flight-path angle one step ago, for the climb rate.</summary>
    public float PreviousClimb;

    /// <summary>False until the first step has read the plane.</summary>
    public bool Primed;
}

/// <summary>The Assisted control scheme's law: the stick read as a turn and a climb, and the
/// aileron, elevator and rudder that fly them. Its only output is deflections into the decoded
/// plant, and it is pure over its arguments. Design and tuning: <c>docs/assisted-flight.md</c>.</summary>
public static class AssistedFlight
{
    /// <summary>TUNE. The share of sideways travel that turns on rudder alone, wings level.</summary>
    public const float RudderBand = 0.2f;

    /// <summary>TUNE. The rudder deflection the flat turn reaches at the band's edge.</summary>
    public const float FlatRudder = 0.5f;

    /// <summary>TUNE. Roll rate asked per radian of bank error, 1/s.</summary>
    public const float BankGain = 4f;

    /// <summary>TUNE. The roll rate the bank loop asks for at most, rad/s.</summary>
    public const float MaxRollRate = 3f;

    /// <summary>TUNE. The climb rate full stick back asks for, rad/s. It is past what any elevator
    /// reaches, so full stick is full pull.</summary>
    public const float MaxClimbRate = 1.5f;

    /// <summary>TUNE. Nose climb rate asked per radian the nose sits off its target, 1/s.</summary>
    public const float HoldGain = 2f;

    /// <summary>TUNE. How far the nose target leans past the held climb per radian the path sits off it.</summary>
    public const float PathGain = 1f;

    /// <summary>TUNE. Nose climb rate integrated per radian-second the nose sits off its target, 1/s².</summary>
    public const float TrimGain = 2f;

    /// <summary>TUNE. How hard the roll and pitch loops correct the measured body rate onto the target.</summary>
    public const float RateGain = 1f;

    /// <summary>TUNE. Turn rate cut per second for each m/s of sink below the held climb at full
    /// stick, (rad/s)/(m/s)/s.</summary>
    public const float CapGain = 0.05f;

    /// <summary>TUNE. Seconds of the vertical speed's own rate the full-stick loop adds to the sink,
    /// so it stops cutting once the path is already coming back.</summary>
    public const float CapLead = 1.5f;

    /// <summary>TUNE. How fast the turn cap rises at full stick while no surface is saturated, rad/s².
    /// Off full stick it returns to its estimate at the same rate.</summary>
    public const float CapRecover = 0.2f;

    // Nose verticality past which bank means nothing, so the bank loop stands down.
    private const float NearVerticalNoseY = 0.9f;

    /// <summary>One step's deflections from the pilot's summed stick. Its signs are the plant's own:
    /// Roll + turns left, Pitch + climbs, Yaw + noses left. Every field other than the three
    /// deflections passes through. <paramref name="state"/> belongs to this seat.</summary>
    public static FlightInput Steer(FlightInput stick, FlightModel model, ref AssistedState state, float dt)
    {
        var stats = model.Stats;
        var att = model.Attitude;
        var path = model.VelocityDir;
        float speed = Mathf.Max(model.Speed, 1f);

        float climb = Mathf.Asin(Mathf.Clamp(path.Y, -1f, 1f));
        float bank = Mathf.Atan2(att.X.Y, att.Y.Y); // + left wing low, ±π inverted
        bool inverted = att.Y.Y < 0f;
        bool nearVertical = Mathf.Abs(att.Z.Y) > NearVerticalNoseY;

        // Steady body rate per full deflection: the plant damps torque exponentially, so a held
        // deflection settles at torque over damping.
        float damp = Mathf.Max(stats.AngMomentumDamp, 1e-3f);
        float unitRoll = 2f * stats.RollTorque * stats.RecInertia.Z * model.RollAuthorityAt(speed) / damp;
        float unitPitch = 2f * stats.PitchTorque * stats.RecInertia.X * model.PitchAuthorityAt(speed) / damp;
        float unitYaw = 2f * stats.RudderTorque * stats.RecInertia.Y * model.YawAuthorityAt(speed) / damp;
        float ceiling = Mathf.Sqrt((unitPitch * unitPitch) + (unitYaw * unitYaw));

        // Part stick scales from this until full stick has measured the real turn. The path chases
        // the nose at lift_accel_rate, and maxAOA bounds how far the nose may lead it.
        float estimate = Mathf.Min(ceiling, stats.LiftAccelRate * Mathf.Sqrt(2f * (1f - stats.MaxAoaCos)));
        if (!state.Primed)
            state = new AssistedState { HeldClimb = climb, TurnCap = estimate, PreviousClimb = climb, Primed = true };
        float climbRate = dt > 0f ? (climb - state.PreviousClimb) / dt : 0f;
        state.PreviousClimb = climb;
        var rates = model.PhysicalBodyRates;

        float sx = Mathf.Clamp(stick.Roll, -1f, 1f);
        float sy = Mathf.Clamp(stick.Pitch, -1f, 1f);
        float travel = Mathf.Abs(sx);
        float bankShare = travel > RudderBand ? (travel - RudderBand) / (1f - RudderBand) : 0f;
        float flatShare = Mathf.Min(travel / RudderBand, 1f) * (1f - bankShare);
        bool pulling = sy != 0f;
        bool holding = !pulling && !inverted && !nearVertical;

        // The turn asked for, in world terms: the flat rudder turn at the band's edge, blending into
        // the cap at full stick. The bank tilts as a real wing would carry that turn.
        float bankTurn = bankShare * state.TurnCap;
        float turn = Mathf.Sign(sx) * ((flatShare * FlatRudder * unitYaw) + bankTurn);
        float bankTarget = Mathf.Sign(sx) * Mathf.Atan(speed * bankTurn / stats.Gravity);

        // Over the top of a loop the bank is measured from inverted, so a held pull flies on.
        if (inverted && pulling)
            bankTarget = Wrap(Mathf.Pi + bankTarget);
        float rollRate = nearVertical ? 0f : Mathf.Clamp(BankGain * Wrap(bankTarget - bank), -MaxRollRate, MaxRollRate);
        float roll = Deflect(rollRate + (RateGain * (rollRate - rates.Z)), unitRoll);

        // The climb asked for: a held stick's rate, or a centred stick's hold on the angle it was left at.
        float climbAsk;
        if (holding)
        {
            // The loop closes on the nose. The path ignores a nose inside liftAOAs' low edge, so a
            // loop on the path alone cycles across that dead band.
            float noseClimb = Mathf.Asin(Mathf.Clamp(-att.Z.Y, -1f, 1f));
            float noseTarget = state.HeldClimb + (PathGain * (state.HeldClimb - climb));
            climbAsk = (HoldGain * (noseTarget - noseClimb)) + state.ClimbTrim;
            state.ClimbTrim = Mathf.Clamp(state.ClimbTrim + (TrimGain * (noseTarget - noseClimb) * dt), -MaxClimbRate, MaxClimbRate);
        }
        else
        {
            climbAsk = sy * MaxClimbRate;
            state.HeldClimb = climb;
        }

        // World turn and climb onto body pitch and yaw through the bank. Over the top, or with the
        // nose near vertical, the pull is the plane's own and nothing turns.
        float pitchRate, yawRate;
        if (inverted || nearVertical)
        {
            pitchRate = climbAsk;
            yawRate = 0f;
        }
        else
        {
            float cos = Mathf.Cos(bank), sin = Mathf.Sin(bank);
            pitchRate = (climbAsk * cos) + (turn * sin);
            yawRate = (turn * cos) - (climbAsk * sin);
        }

        float pitchRaw = (pitchRate + (RateGain * (pitchRate - rates.X))) / Mathf.Max(unitPitch, 1e-4f);
        float yawRaw = yawRate / Mathf.Max(unitYaw, 1e-4f);
        bool saturated = Mathf.Abs(pitchRaw) > 1f || Mathf.Abs(yawRaw) > 1f;

        // Full stick is the hardest turn that holds height, so the vertical-speed loop moves its
        // turn rate both ways. A surface saturating at part stick may only cut it.
        float excess = speed * (Mathf.Sin(climb) - Mathf.Sin(state.HeldClimb)); // m/s above the held climb
        excess += CapLead * speed * Mathf.Cos(climb) * climbRate;
        if (holding && bankShare >= 1f)
        {
            float rise = saturated ? 0f : CapRecover;
            state.TurnCap = Mathf.Clamp(state.TurnCap + (((CapGain * excess) + rise) * dt), 0f, ceiling);
        }
        else if (holding && bankShare > 0f && saturated)
        {
            state.TurnCap = Mathf.Clamp(state.TurnCap + (CapGain * Mathf.Min(excess, 0f) * dt), 0f, ceiling);
        }
        else
        {
            state.TurnCap = Mathf.MoveToward(state.TurnCap, estimate, CapRecover * dt);
        }

        var output = stick;
        output.Roll = roll;
        output.Pitch = Mathf.Clamp(pitchRaw, -1f, 1f);
        // The separate yaw input lands after the loops, and no loop reads yaw back to fight it.
        output.Yaw = Mathf.Clamp(Mathf.Clamp(yawRaw, -1f, 1f) + stick.Yaw, -1f, 1f);
        return output;
    }

    // A rate as a deflection on an axis whose full travel buys `unit`. With no authority at this
    // speed the axis takes full travel toward the rate.
    private static float Deflect(float rate, float unit) =>
        unit > 1e-4f ? Mathf.Clamp(rate / unit, -1f, 1f) : Mathf.Sign(rate);

    private static float Wrap(float angle) => Mathf.Wrap(angle, -Mathf.Pi, Mathf.Pi);
}
