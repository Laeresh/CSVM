using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The force-path seam (PLAN-ai-flight C21): a <see cref="FlightModel"/> flows either the original's
/// player force path or its AI one, chosen once at construction.
///
/// <para><b>Why a test for a flag nothing reads yet.</b> C21 lands the seam alone so wave C's diff is
/// readable, which means the whole regression suite passes whether the flag is wired correctly, wired
/// backwards, or not wired at all — a green run here verifies nothing about the change on its own
/// (<c>verification.md</c> METHOD-10). These assertions are what make the wiring falsifiable before
/// anything downstream depends on it.</para>
///
/// <para><b>C22 turned this class around.</b> C21's <c>BothPathsStillIntegrateIdentically</c> pinned
/// the claim that the seam changed no arithmetic, and was written to fail the moment the first
/// divergence landed. It has become <see cref="TheTwoPathsNoLongerIntegrateIdentically"/> over the
/// same 60-step probe, and the three divergences C22 hung off the flag are asserted one at a time
/// below — each isolated so that a mis-wired flag cannot show up as "no change" in an aggregate
/// (<c>verification.md</c> METHOD-10: three of the three are SKIPS on the AI side, and an unchanged
/// number proves nothing unless it was able to fail).</para>
///
/// <para>There is deliberately no density assertion. A1 disproved the AI density band — the
/// atmosphere call is shared and unbranched — so both paths fly the dense band and the "near-zero
/// AI aerodynamic forces" outcome C22 was originally written around is not what happens.</para>
/// </summary>
public class ForcePathSeamTests
{
    private const float Dt = 1f / 60f;

    /// <summary>The default is the PLAYER path, and it is the trap the constructor's own ⚠ names: the
    /// ~18 test sites want a plant with no session around them, so the argument is optional — and a
    /// production site added later therefore gets the player plant in silence.</summary>
    [Fact]
    public void TheDefaultIsThePlayerPath()
    {
        Assert.False(new FlightModel(Bhawk()).UsesAiForcePath);
        Assert.False(new FlightModel(Bhawk(), aiForcePath: false).UsesAiForcePath);
    }

    /// <summary>The AI path is carried through construction — the able-to-fail form of "the argument
    /// reaches the field", which a backwards or dropped assignment breaks.</summary>
    [Fact]
    public void TheAiPathIsCarried()
    {
        Assert.True(new FlightModel(Bhawk(), aiForcePath: true).UsesAiForcePath);
    }

    /// <summary>The selection rule the two production sites encode, stated once: the AI path is taken
    /// when nobody is at the controls. Both sites read <c>IsHumanPiloted</c> directly.</summary>
    [Theory]
    [InlineData(true, false)]    // a person flying: player path
    [InlineData(false, true)]    // nobody flying: AI path
    public void TheSelectionRuleIsHumanPiloted(bool isHuman, bool expectAi)
    {
        Assert.Equal(expectAi, new FlightModel(Bhawk(), !isHuman).UsesAiForcePath);
    }

    /// <summary>C21's probe, with its assertion turned around by C22: the same second of sim with a
    /// deflected stick and part throttle, so rotation (the stick, the bank coupling, the
    /// weathervane) and translation (thrust, drag, gravity, lift, the nose-chase) all run — and the
    /// two paths now separate. Kept as the aggregate that would catch the flag being dropped
    /// wholesale; the three tests below are what say WHICH divergence is present.</summary>
    [Fact]
    public void TheTwoPathsNoLongerIntegrateIdentically()
    {
        var player = new FlightModel(Bhawk());
        var ai = new FlightModel(Bhawk(), aiForcePath: true);
        foreach (var m in new[] { player, ai })
            m.Reset(Vector3.Zero, Basis.Identity, 135f, 0.7f);

        var input = new FlightInput { Pitch = 0.6f, Roll = -0.4f, Yaw = 0.2f, Throttle = 0.7f };
        for (int i = 0; i < 60; i++)
        {
            player.Step(input, Dt);
            ai.Step(input, Dt);
        }

        // Proof the probe is loaded rather than sitting at the trim it started from — a divergence
        // read off two aircraft that never flew would say nothing about the force paths.
        Assert.True(player.Position.Length() > 100f);
        Assert.True(player.BodyRates.Length() > 0.1f);

        // The weathervane skip reaches rotation, the airflow skip reaches lift, and both reach the
        // trajectory. Metres and rad/s, not last-digit noise: this is a mechanism divergence.
        float rateGap = (player.BodyRates - ai.BodyRates).Length();
        float posGap = (player.Position - ai.Position).Length();
        Assert.True(rateGap > 0.01f, $"body-rate gap {rateGap} rad/s");
        Assert.True(posGap > 1f, $"position gap {posGap} m");
        Assert.NotEqual(player.Alpha, ai.Alpha, 2);
    }

    /// <summary>Divergence 1, the airflow blend, stated as an IDENTITY rather than as an inequality:
    /// the AI is exactly the player would be with the <c>liftAOAs</c> window forced fully open. A
    /// third plant with the window collapsed to nothing is flown on the PLAYER path and must land on
    /// the AI's lift demand to five places at every incidence — which "they differ" would not say,
    /// and which pins the direction too. The sign of the difference against the real window is not
    /// fixed: at a climbing flight path the fully-nose-aligned swing OPPOSES weight and the AI's
    /// demand comes out smaller, so an assertion written as "the AI pulls harder" would be reading a
    /// geometry, not the mechanism.
    /// <para>Isolated by <c>return_rate = 0</c> hands-off at 135 m/s: the weathervane term is
    /// identically zero on every path here and the speed floor is nowhere near, so the lift demand
    /// is the only thing that can move. The 0° and 30° rows are the controls — inside neither is the
    /// window open (on the nose there is nothing to blend; past <c>liftAOAs[1]</c> the player is
    /// fully nose-aligned as well), so the real window is the only place the two paths separate at
    /// all.</para></summary>
    [Theory]
    [InlineData(0f, false)]     // control: on the nose, nothing to blend, both paths agree
    [InlineData(8f, true)]      // below liftAOAs[0] (11.5°): the player uses the TRUE airflow
    [InlineData(14f, true)]     // inside the window: the player blends partially
    [InlineData(30f, false)]    // control: past liftAOAs[1] (16.3°), the player is nose-aligned too
    public void AiAlwaysFliesNoseAlignedAirflow(float alphaDeg, bool expectDivergence)
    {
        var stats = Bhawk();
        stats.ReturnRate = 0f;
        // The same plant with the cosine window collapsed to a hair either side of 1, so any
        // misalignment at all is "past the high edge" and the blend saturates at fully nose-aligned.
        var forced = Bhawk();
        forced.ReturnRate = 0f;
        forced.LiftAoaCosLo = 0.9999f;
        forced.LiftAoaCosHi = 0.9998f;

        var player = new FlightModel(stats);
        var ai = new FlightModel(stats, aiForcePath: true);
        var noseAligned = new FlightModel(forced);
        foreach (var m in new[] { player, ai, noseAligned })
        {
            m.Reset(Vector3.Zero, Basis.Identity, 135f, 0f);
            // Nose is −Z; swing the flight path off it about the pitch axis, leaving the attitude
            // alone, so the misalignment is the one variable.
            m.VelocityDir = (-Basis.Identity.Z).Rotated(Vector3.Right, Mathf.DegToRad(alphaDeg));
        }

        var input = default(FlightInput);
        player.Step(input, Dt);
        ai.Step(input, Dt);
        noseAligned.Step(input, Dt);

        Assert.Equal(noseAligned.LoadFactorDemand, ai.LoadFactorDemand, 5);
        Assert.Equal(noseAligned.Speed, ai.Speed, 4);

        if (expectDivergence)
        {
            Assert.True(Mathf.Abs(ai.LoadFactorDemand - player.LoadFactorDemand) > 0.01f,
                $"ai {ai.LoadFactorDemand} vs player {player.LoadFactorDemand}");
        }
        else
        {
            Assert.Equal(player.LoadFactorDemand, ai.LoadFactorDemand, 5);
        }

        // Whatever the blend did, it cannot have reached rotation on this plant.
        Assert.Equal(player.BodyRates.Length(), ai.BodyRates.Length(), 6);
    }

    /// <summary>Divergence 2, the weathervane. Isolated on rotation alone: hands-off with the flight
    /// path off the nose, the player picks up a restoring body rate and the AI picks up exactly
    /// none. <see cref="FlightModel.WeathervaneTorque"/> itself is asserted UNGATED and equal on
    /// both, which is what separates "Step does not sum it in" from "the law was deleted for AI" —
    /// the original skips the block, it does not zero <c>return_rate</c>.</summary>
    [Fact]
    public void AiSkipsTheWeathervane()
    {
        var player = new FlightModel(Bhawk());
        var ai = new FlightModel(Bhawk(), aiForcePath: true);
        foreach (var m in new[] { player, ai })
        {
            m.Reset(Vector3.Zero, Basis.Identity, 135f, 0f);
            m.VelocityDir = (-Basis.Identity.Z).Rotated(Vector3.Right, Mathf.DegToRad(8f));
        }

        // The law, before either steps: same misalignment, same return_rate, same torque.
        Assert.True(player.WeathervaneTorque().Length() > 1e-3f);
        Assert.Equal(player.WeathervaneTorque().Length(), ai.WeathervaneTorque().Length(), 6);

        var input = default(FlightInput);
        player.Step(input, Dt);
        ai.Step(input, Dt);

        Assert.True(player.BodyRates.Length() > 1e-4f);
        Assert.Equal(0f, ai.BodyRates.Length(), 7);
    }

    /// <summary>Divergence 3, the 10 mph nose-axis floor — and the trap in it. The probe is a plane
    /// with its nose on the horizon falling straight down at 20 m/s: its SPEED is 20 m/s, four times
    /// the floor, so a floor written on <c>Speed</c> would be a no-op here. The floor is on the
    /// velocity's nose component, which is zero, so the AI is pushed forward and the player is
    /// not.</summary>
    [Fact]
    public void AiGetsTheNoseAxisSpeedFloorAndItIsNotAFloorOnSpeed()
    {
        var player = new FlightModel(Bhawk());
        var ai = new FlightModel(Bhawk(), aiForcePath: true);
        foreach (var m in new[] { player, ai })
        {
            m.Reset(Vector3.Zero, Basis.Identity, 20f, 0f);
            m.VelocityDir = Vector3.Down;
        }

        var input = default(FlightInput);
        player.Step(input, Dt);
        ai.Step(input, Dt);

        float playerNose = (player.VelocityDir * player.Speed).Dot(-player.Attitude.Z);
        float aiNose = (ai.VelocityDir * ai.Speed).Dot(-ai.Attitude.Z);

        // A Speed clamp could not have fired: both start well above the floor.
        Assert.True(player.Speed > 4.4704f);
        Assert.True(playerNose < 4.4704f);
        Assert.True(aiNose >= 4.4704f, $"ai nose component {aiNose}");

        // One-sided, and it adds along the nose rather than replacing the vector: the descent
        // survives it, so the AI ends up FASTER than the player rather than redirected.
        Assert.True(ai.Speed > player.Speed);
        Assert.True(ai.VelocityDir.Y < -0.9f);
    }

    /// <summary>The floor only ever raises. In level cruise well above 10 mph it is not reachable,
    /// so the AI path must leave the trajectory alone — the able-to-fail form of "one-sided",
    /// which a clamp written as an assignment rather than a minimum would break.</summary>
    [Fact]
    public void TheNoseAxisFloorNeverSlowsAnAiAircraft()
    {
        var stats = Bhawk();
        stats.ReturnRate = 0f;
        var player = new FlightModel(stats);
        var ai = new FlightModel(stats, aiForcePath: true);
        foreach (var m in new[] { player, ai })
            m.Reset(Vector3.Zero, Basis.Identity, 135f, 0.7f);

        var input = new FlightInput { Throttle = 0.7f };
        for (int i = 0; i < 60; i++)
        {
            player.Step(input, Dt);
            ai.Step(input, Dt);
        }

        // Hands-off, wings level, on the nose: none of the three divergences can reach this state,
        // so the two paths must still agree to the last digit.
        Assert.Equal(player.Speed, ai.Speed, 5);
        Assert.Equal(player.Position.Y, ai.Position.Y, 4);
        Assert.Equal(player.Position.Z, ai.Position.Z, 4);
        Assert.True(player.Speed > 4.4704f);
    }

    /// <summary>The Bloodhawk's real dynamics, the same fixture <c>AttitudeThrustTests</c> flies.</summary>
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
