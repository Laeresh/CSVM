using System.IO;
using CSVM.Flight;
using CSVM.Testing;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The attitude-dependent thrust scale, and the sustained climb that settled it.
///
/// <para><b>The sign is the whole risk here.</b> The original reads the world-up component of the
/// body Z axis, and the nose points along −Z, so the quantity is NEGATIVE in a climb. Flip it and
/// the model still flies — it merely swaps climb for dive, which no casual look at a flight would
/// catch. <see cref="TheScaleIsReadOffTheNoseAndNotItsMirror"/> is the able-to-fail form: it reads
/// the thrust term out of the integrator by differencing full throttle against zero at an otherwise
/// identical state, so it fails under the flip rather than trusting the formula's argument name.
/// </para>
///
/// <para><b>Why there is no climb-gravity constant.</b> A fitted <c>ClimbGravityScale = 0.6</c>
/// would spare a climbing aircraft, on the reasoning that the original holds speed better in a climb
/// than plain energy exchange predicts. The decoded terms do the opposite. Measured against the
/// original's own sustained full-throttle climb, all four combinations rank unambiguously (plateau
/// speed against a measured 163.05 mph): constant alone 276.7, neither 257.7, both mechanisms 232.2,
/// attitude terms alone <b>204.0</b>. On the current force shapes the constant makes the climb
/// WORSE. See <c>docs/org/flightModel.md</c>.</para>
/// </summary>
public class AttitudeThrustTests
{
    private const float Dt = 1f / 60f;

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>The decoded numbers themselves: two coefficients, the second one-sided, so a
    /// vertical climb keeps (1 − 0.24)(1 − 0.13) = 0.6612 of available thrust and a vertical dive
    /// gets 1.24 — not 1.24 and 1/1.24, and not symmetric.</summary>
    [Fact]
    public void TheScaleIsTwoCoefficientsAndOnlyOneOfThemIsTwoSided()
    {
        Assert.Equal(1.0f, FlightModel.AttitudeThrustScale(0f), 5);
        Assert.Equal(0.6612f, FlightModel.AttitudeThrustScale(-1f), 4);
        Assert.Equal(1.24f, FlightModel.AttitudeThrustScale(1f), 4);

        // One-sided, so the dive side is the bare linear term while the climb side carries both.
        // A symmetric reading would put the dive at 1/0.6612 = 1.51.
        Assert.True(FlightModel.AttitudeThrustScale(0.5f) > 1f);
        Assert.True(FlightModel.AttitudeThrustScale(-0.5f) < 1f);
        Assert.Equal(1.12f, FlightModel.AttitudeThrustScale(0.5f), 4);
    }

    /// <summary>The sign, read out of the integrator rather than off the formula. Thrust is the ONLY
    /// force term the throttle touches, so differencing one step at full throttle against one step
    /// at zero — same attitude, same speed, same flight path — isolates it exactly: drag, gravity
    /// and lift cancel to the last bit.
    ///
    /// <para>⚠ This is the test that fails under a dropped sign. Flipping the argument's sense swaps
    /// the two ratios below (climb 1.24, dive 0.661), and every assertion here breaks — where the
    /// flown behaviour would look entirely plausible.</para></summary>
    [Fact]
    public void TheScaleIsReadOffTheNoseAndNotItsMirror()
    {
        float level = ThrustTermAt(0f);
        float climb = ThrustTermAt(90f);
        float dive = ThrustTermAt(-90f);

        Assert.True(climb < level,
            $"a vertical climb must LOSE thrust: {climb:0.000} vs level {level:0.000} m/s² — "
            + "the argument's sign is inverted, and the flown result would still look plausible");
        Assert.True(dive > level,
            $"a vertical dive must GAIN thrust: {dive:0.000} vs level {level:0.000} m/s²");
        Assert.Equal(0.6612f, climb / level, 3);
        Assert.Equal(1.24f, dive / level, 3);
    }

    /// <summary>Level flight is untouched — the scale is exactly 1 with the nose on the horizon, so
    /// every wings-level measurement in the pinned envelope is inert by construction, and
    /// this change cannot have bought its climb behaviour by moving the top speed.</summary>
    [Fact]
    public void LevelFlightIsUntouched()
    {
        var m = new FlightModel(Bhawk());
        m.Reset(Vector3.Zero, Basis.Identity, 135f, 1f);
        Assert.Equal(1f, FlightModel.AttitudeThrustScale(m.Attitude.Z.Y), 6);
    }

    /// <summary>The sustained full-throttle climb the original was filmed holding for forty seconds,
    /// against the model. The bound is deliberately placed where it separates the four candidate
    /// arrangements rather than merely passing the one in place: on the same build the arrangement
    /// with the climb-gravity constant and no attitude terms settles at 276.7 mph, neither mechanism
    /// at 257.7, both mechanisms together at 232.2, and only the landed arrangement — attitude terms,
    /// no constant — reaches 204.0 against a measured 163.05.
    ///
    /// <para>The residual is real and is recorded, not tuned away: see
    /// <c>docs/org/flightModel.md</c>, "The sustained climb". The clamp check is not decoration —
    /// a run that reaches the altitude clamp reports the clamp's speed, not the climb's.</para>
    /// </summary>
    [ExtractedDataFact]
    public void TheSustainedClimbSettlesFarBelowEitherFittedArrangement()
    {
        var r = Probes.SustainedClimb(ZrdrPath, "player_bhawk");
        Assert.True(r.Error == null, $"{r.Error ?? "-"}");
        Assert.True(r.ClampedAt < 0,
            $"the altitude clamp bound at +{r.ClampedAt:0.0} s — this run measures the clamp, "
            + "not the climb");
        Assert.True(r.PlateauMph < 220.0,
            $"sustained climb settles at {r.PlateauMph:0.00} mph against a measured 163.05 — every "
            + "arrangement that keeps the climb-gravity constant or drops the attitude terms sits "
            + "above 230 here");
        Assert.True(r.PlateauMph > 163.05,
            $"sustained climb settles at {r.PlateauMph:0.00} mph, BELOW the original's 163.05 — the "
            + "residual has always been on the fast side, so this is a different defect");
    }

    /// <summary>One step's thrust contribution along the nose, m/s², at a nose-up angle: the
    /// difference between a full-throttle step and a zero-throttle step from the same state.</summary>
    private static float ThrustTermAt(float noseDeg)
    {
        var attitude = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(noseDeg));
        var full = new FlightModel(Bhawk());
        var idle = new FlightModel(Bhawk());
        full.Reset(Vector3.Zero, attitude, 135f, 1f);
        idle.Reset(Vector3.Zero, attitude, 135f, 0f);
        var before = full.VelocityDir * full.Speed;
        full.Step(new FlightInput { Throttle = 1f }, Dt);
        idle.Step(new FlightInput { Throttle = 0f }, Dt);
        var delta = (full.VelocityDir * full.Speed) - (idle.VelocityDir * idle.Speed);
        _ = before;
        return delta.Length() / Dt;
    }

    /// <summary>The Bloodhawk's real dynamics — the placeholder <c>PlaneStats()</c> defaults are the
    /// executable's fallback aircraft and carry a different weight and reference area.</summary>
    private static PlaneStats Bhawk() => new()
    {
        PitchTorque = 3.3f,
        RollTorque = 7.5f,
        RudderTorque = 2f,
        ReturnRate = 3f,
        AngMomentumDamp = 5f,
        RecInertia = new Vector3(1.18f, 1f, 1.1f),
        FdSpeed = 135f,
        VehWeight = 1900f,
        RefArea = 330f,
        DragFactor = 0.37f,
        EnginePower = 0.62f,
    };
}
