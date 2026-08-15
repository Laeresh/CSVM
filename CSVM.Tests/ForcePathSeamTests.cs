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
/// <para><b>⚠ <see cref="BothPathsStillIntegrateIdentically"/> is meant to fail in C22.</b> It pins
/// C21's claim that the seam changes no arithmetic. The moment C22 hangs the AI's nose-aligned
/// airflow, skipped weathervane and speed floor off the flag, this becomes false BY DESIGN — rewrite
/// it there into the per-divergence assertions. It exists so that C22's first behavioural divergence
/// is a deliberate edit rather than something nobody notices.</para>
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
    /// when nobody is at the controls AND the temporary A/B switch is off. Both sites read
    /// <c>IsHumanPiloted</c>, so a player aircraft is on the player path in every combination — the
    /// switch moves AI aircraft only, which is what makes an A/B a comparison of plants rather than
    /// of two different sessions.</summary>
    [Theory]
    [InlineData(true, false, false)]   // a person flying: player path
    [InlineData(true, true, false)]    // ...and --no-ai-plant does not touch them
    [InlineData(false, false, true)]   // nobody flying: AI path
    [InlineData(false, true, false)]   // ...put back on the player path for the A/B
    public void TheSelectionRuleIsHumanAndTheSwitch(bool isHuman, bool noAiPlant, bool expectAi)
    {
        Assert.Equal(expectAi, new FlightModel(Bhawk(), !isHuman && !noAiPlant).UsesAiForcePath);
    }

    /// <summary>C21's actual claim: selecting the AI path changes no arithmetic yet. Stepped over a
    /// second of sim with a deflected stick and part throttle, so rotation (the stick, the bank
    /// coupling, the weathervane) and translation (thrust, drag, gravity, lift, the nose-chase) all
    /// run — not a hands-off cruise, which the seam could pass while diverging under load.
    /// ⚠ Rewritten by C22, see this class' summary.</summary>
    [Fact]
    public void BothPathsStillIntegrateIdentically()
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

        Assert.Equal(player.Speed, ai.Speed, 6);
        Assert.Equal(player.Alpha, ai.Alpha, 6);
        Assert.Equal(player.Position.X, ai.Position.X, 4);
        Assert.Equal(player.Position.Y, ai.Position.Y, 4);
        Assert.Equal(player.Position.Z, ai.Position.Z, 4);
        Assert.Equal(player.BodyRates.X, ai.BodyRates.X, 6);
        Assert.Equal(player.BodyRates.Y, ai.BodyRates.Y, 6);
        Assert.Equal(player.BodyRates.Z, ai.BodyRates.Z, 6);

        // Proof the probe is loaded rather than sitting at the trim it started from — an identity
        // that held because nothing moved would pass whatever the two paths did.
        Assert.True(player.Position.Length() > 100f);
        Assert.True(player.BodyRates.Length() > 0.1f);
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
