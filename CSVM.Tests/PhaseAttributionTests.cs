using System.Collections.Generic;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="PhaseAttribution"/>, the B11 categorizer that turns a <c>StartupProfile</c>'s raw
/// phase names into archive/decode, sound preparation and runtime/world construction, plus the
/// suite-level rest/overrun arithmetic. Godot-free by construction, so the closure identity B11's
/// verify step needs is provable here rather than only read off a live engine report.
/// </summary>
[Trait("Tier", "Quick")]
public class PhaseAttributionTests
{
    [Fact]
    public void CategoriesSumToTheSuppliedBuildTime()
    {
        var phases = new Dictionary<string, double>
        {
            ["gamez"] = 10,
            ["textures"] = 5,
            ["sounds"] = 3,
            ["world"] = 20,
            ["clutter"] = 2,
            ["anim"] = 4,
            ["bind"] = 6,
            ["prewarm"] = 1,
        };
        // buildMs deliberately exceeds the phase sum (51) by 9 ms of unmarked bookkeeping.
        var c = PhaseAttribution.Categorize(phases, buildMs: 60);
        Assert.Equal(19, c.ArchiveDecodeMs);      // gamez + textures + anim + zrdr(none)
        Assert.Equal(4, c.SoundPrepMs);           // sounds + prewarm
        Assert.Equal(28, c.RuntimeConstructionMs); // world + clutter + bind
        Assert.Equal(9, c.OtherMs);
        Assert.Equal(60, c.BuildMs);
    }

    [Fact]
    public void AnUnrecognisedPhaseNameFallsIntoOtherRatherThanVanishing()
    {
        var phases = new Dictionary<string, double> { ["a_future_phase"] = 7 };
        var c = PhaseAttribution.Categorize(phases, buildMs: 7);
        Assert.Equal(0, c.ArchiveDecodeMs);
        Assert.Equal(0, c.SoundPrepMs);
        Assert.Equal(0, c.RuntimeConstructionMs);
        Assert.Equal(7, c.OtherMs);
    }

    [Fact]
    public void AnInjectedDelayInOneNamedPhaseShowsUpOnlyInThatCategory()
    {
        // The plan's own trap check: a deliberate Thread.Sleep-shaped delay in "world" must move
        // RuntimeConstructionMs and nothing else.
        var baseline = PhaseAttribution.Categorize(
            new Dictionary<string, double> { ["world"] = 10, ["gamez"] = 5 }, buildMs: 15);
        var withDelay = PhaseAttribution.Categorize(
            new Dictionary<string, double> { ["world"] = 60, ["gamez"] = 5 }, buildMs: 65);
        Assert.Equal(50, withDelay.RuntimeConstructionMs - baseline.RuntimeConstructionMs);
        Assert.Equal(baseline.ArchiveDecodeMs, withDelay.ArchiveDecodeMs);
        Assert.Equal(baseline.SoundPrepMs, withDelay.SoundPrepMs);
        Assert.Equal(baseline.OtherMs, withDelay.OtherMs);
    }

    [Fact]
    public void CategorizedAddsComponentwise()
    {
        var a = PhaseAttribution.Categorize(new Dictionary<string, double> { ["gamez"] = 1 }, 1);
        var b = PhaseAttribution.Categorize(new Dictionary<string, double> { ["world"] = 2 }, 2);
        var sum = a + b;
        Assert.Equal(1, sum.ArchiveDecodeMs);
        Assert.Equal(2, sum.RuntimeConstructionMs);
        Assert.Equal(3, sum.BuildMs);
    }

    [Fact]
    public void RestIsWallMinusBuildMinusDisposal()
    {
        Assert.Equal(2.0, PhaseAttribution.Rest(suiteWallSeconds: 10, buildSeconds: 7, disposalSeconds: 1), 3);
    }

    [Fact]
    public void RestNeverGoesNegativeAndOverrunReportsTheGapInstead()
    {
        // build+disposal exceeding wall time is a measurement anomaly, not a claim of negative
        // assertion time, Rest clamps to 0 and Overrun names the actual gap.
        double rest = PhaseAttribution.Rest(suiteWallSeconds: 5, buildSeconds: 4, disposalSeconds: 3);
        double overrun = PhaseAttribution.Overrun(suiteWallSeconds: 5, buildSeconds: 4, disposalSeconds: 3);
        Assert.Equal(0, rest);
        Assert.Equal(2.0, overrun, 3);
    }

    [Fact]
    public void ACleanMeasurementReportsZeroOverrun()
    {
        Assert.Equal(0, PhaseAttribution.Overrun(suiteWallSeconds: 10, buildSeconds: 4, disposalSeconds: 1));
    }
}
