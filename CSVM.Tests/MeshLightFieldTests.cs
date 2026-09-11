using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The GameZ reader's mesh point-light fields, against the shapes the shipped chapters actually
/// author (<c>docs/formats/world-structure.md</c>: the blink gate, the fade's pre-divided slope,
/// and the words the original's draw never reads). Input is <c>fixtures/gamez-lights/</c>: a
/// steady star, a blinking beacon, and a lamp with an authored fade band.
/// </summary>
[Trait("Tier", "Quick")]
public class MeshLightFieldTests
{
    private const float Tolerance = 1e-4f;

    // The alpha range the original's slope is expressed in.
    private const float AlphaUnits = 255f;

    [Fact]
    public void ThePeriodIsGatedBehindItsOwnFlag()
    {
        // Both lights carry a two-second period; only the second one has the flag that makes the
        // original's draw ever look at it. A reader taking the field unconditionally makes the
        // star blink, which is the misread this pins.
        var lights = Load();
        Assert.Equal(0f, lights[0].BlinkPeriod);
        Assert.Equal(2f, lights[1].BlinkPeriod);
    }

    [Fact]
    public void AnUnfadedLightReadsUnfadedRatherThanBorrowingTheFlareReach()
    {
        // The star authors no far edge at all, while carrying a 4000 m lens-flare reach and a
        // slope for a fade the original's draw skips outright.
        var lights = Load();
        Assert.Equal(0f, lights[0].FadeFar);
        Assert.True(lights[0].FadeSlope > 0f);
    }

    [Fact]
    public void TheFadeSlopeIsTheReciprocalOfTheAuthoredBand()
    {
        // The lamp fades from 1000 m to 2000 m, and the slope is the 255-unit alpha range divided
        // by that 1000 m band. Reading it as anything else puts the band's near edge elsewhere.
        var lamp = Load()[2];
        Assert.Equal(2000f, lamp.FadeFar);
        Assert.Equal(0.255f / AlphaUnits, lamp.FadeSlope, Tolerance);
        Assert.Equal(1000f, lamp.FadeFar - (1f / lamp.FadeSlope), 0.5f);
    }

    [Fact]
    public void EveryAuthoredLightKeepsItsOwnPositionAndColour()
    {
        // The control: three lights are read, in order, with distinct positions — so the checks
        // above are reading three different records and not one repeated.
        var lights = Load();
        Assert.Equal(3, lights.Count);
        Assert.Equal(0f, lights[0].Position.X);
        Assert.Equal(1f, lights[1].Position.X);
        Assert.Equal(4f, lights[2].Position.X);
        Assert.Equal(1f, lights[1].Color.R, Tolerance);
        Assert.Equal(170f / 255f, lights[2].Color.G, Tolerance);
    }

    private static System.Collections.Generic.List<GameZLight> Load() =>
        GameZ.Load(TestData.Fixture("gamez-lights")).Meshes[0].Lights;
}
