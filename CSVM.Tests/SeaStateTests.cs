using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// One chapter's sea (<see cref="SeaState"/>). Its defaults are the ocean's tune, and every value is
/// held in its range with the swell below folding. The <c>--debug-ocean</c> grammar keeps only what
/// it can apply.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class SeaStateTests
{
    [Fact]
    public void TheDefaultsAreTheOceansTune()
    {
        var d = SeaState.Default;
        Assert.Equal((1f, 1f, 20f, 0.55f), (d.Height, d.Length, d.WindDeg, d.Sharpness));
        Assert.Equal((60f, 14f, 0.08f, 1f), (d.BendMetres, d.BendRadians, d.DetailSlope, d.DetailDrift));
        Assert.Equal((1.3f, 0.12f, 1f, 0.3f), (d.FoamThreshold, d.FoamStrength, d.FoamPatch, d.FoamCover));
        Assert.Equal((24f, 160f, 12f, 64f), (d.SwellFrom, d.SwellFull, d.LookFrom, d.LookFull));
        Assert.Equal((0.2f, 0.3f), (d.RoughNear, d.RoughFar));
        Assert.False(d.Tinted);
        Assert.Equal(d, d.Clamped());
        Assert.Equal("defaults", d.Describe());
    }

    /// <summary>Every field has a unique key, a range holding its default, and a group the lab shows.</summary>
    [Fact]
    public void EveryFieldIsNamedOnceAndHoldsItsDefault()
    {
        Assert.Equal(SeaState.Fields.Count, SeaState.Fields.Select(f => f.Key).Distinct().Count());
        Assert.All(SeaState.Fields, f => Assert.InRange(f.Default, f.Min, f.Max));
        Assert.Equal(new[] { "Swell", "Bending + detail", "Foam", "Coast + colour" },
            SeaState.Fields.Select(f => f.Group).Distinct());
    }

    [Theory]
    [InlineData("length", 9f, 2f)]
    [InlineData("length", 0.1f, 0.5f)]
    [InlineData("wind", -10f, 0f)]
    [InlineData("swell_full", 400f, 160f)]
    [InlineData("look_from", 1f, 8f)]
    [InlineData("foam_cover", 0f, 0.1f)]
    [InlineData("sharpness", float.NaN, 0.55f)]
    public void AValueOutOfRangeIsHeldAtItsEdge(string key, float value, float held)
    {
        var field = SeaState.Find(key)!;
        Assert.Equal(held, field.Get(field.With(SeaState.Default, value).Clamped()));
    }

    /// <summary>Sharpness times height is held at the fold limit by lowering the height, so a crest
    /// never loops over itself. Length does not enter, since Q k A does not depend on it.</summary>
    [Fact]
    public void TheSwellIsHeldBelowFolding()
    {
        var steep = (SeaState.Default with { Height = 2f, Sharpness = 0.8f }).Clamped();
        Assert.Equal(0.8f, steep.Sharpness);
        Assert.Equal(1.25f, steep.Height, 4);
        var gentle = (SeaState.Default with { Height = 1.4f, Length = 1.5f }).Clamped();
        Assert.Equal(1.4f, gentle.Height);
        Assert.True(steep.Height * steep.Sharpness <= SeaState.FoldLimit + 1e-6f);
    }

    /// <summary>A coast ramp keeps its edges at least RampGap apart, the full edge winning.</summary>
    [Fact]
    public void ACoastRampKeepsItsWidth()
    {
        var sea = (SeaState.Default with { SwellFrom = 150f, SwellFull = 100f, LookFrom = 70f, LookFull = 30f }).Clamped();
        Assert.Equal((96f, 100f), (sea.SwellFrom, sea.SwellFull));
        Assert.Equal((26f, 30f), (sea.LookFrom, sea.LookFull));
    }

    [Fact]
    public void TheOverrideGrammarKeepsWhatItCanApply()
    {
        var rejected = new List<string>();
        string kept = SeaState.FilterOverrides(" height:1.4 ,open,bogus:2,length:x,LENGTH:1.5,wind", rejected);
        Assert.Equal("height:1.4,open,LENGTH:1.5", kept);
        Assert.Equal(new[] { "bogus:2", "length:x", "wind" }, rejected);
        Assert.True(SeaState.OverridesOpen(kept));
        Assert.False(SeaState.OverridesOpen("height:1"));
        Assert.False(SeaState.OverridesOpen(null));
    }

    /// <summary>Overrides apply left to right and come out clamped.</summary>
    [Fact]
    public void OverridesApplyInOrderAndClamp()
    {
        var sea = SeaState.Default.WithOverrides("height:1.2,height:1.4,length:5,open");
        Assert.Equal(1.4f, sea.Height);
        Assert.Equal(2f, sea.Length);
        Assert.Equal("height:1.40,length:2.00", sea.Describe());
        Assert.Equal(SeaState.Default, SeaState.Default.WithOverrides(null));
    }
}
