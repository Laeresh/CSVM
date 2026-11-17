using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A building's seed and its near variant (<see cref="CityModels"/>). The seed is a pure function of the stamped
/// point, an integer a float holds exactly, and the variant reads it.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CityModelsTests
{
    /// <summary>One point gives one seed every time, below 2^24 and integral; another point gives another.</summary>
    [Fact]
    public void TheSeedIsAnExactFunctionOfThePoint()
    {
        var p = new Vector3(-7124.25f, 12f, -11732.5f);
        float seed = CityModels.Seed(p);
        Assert.Equal(seed, CityModels.Seed(new Vector3(p.X, 99f, p.Z)));
        Assert.Equal(seed, (float)(uint)seed);
        Assert.InRange(seed, 0f, 16777215f);
        Assert.NotEqual(seed, CityModels.Seed(new Vector3(p.X + 256f, p.Y, p.Z)));
        Assert.NotEqual(seed, CityModels.Seed(new Vector3(p.X, p.Y, p.Z + 256f)));
    }

    /// <summary>The variant stays in range, is 0 for one variant or none, and both of two variants occur on a lattice
    /// of stamps about evenly.</summary>
    [Fact]
    public void TheVariantStaysInRangeAndBothOccur()
    {
        var seeds = Enumerable.Range(0, 400).Select(i => CityModels.Seed(new Vector3(-12288f + (i % 20 * 256f), 0f, -(i / 20 * 256f)))).ToList();
        Assert.All(seeds, s => Assert.InRange(CityModels.VariantOf(s, 2), 0, 1));
        Assert.All(seeds, s => Assert.InRange(CityModels.VariantOf(s, 3), 0, 2));
        Assert.All(seeds, s => Assert.Equal(0, CityModels.VariantOf(s, 1)));
        Assert.All(seeds, s => Assert.Equal(0, CityModels.VariantOf(s, 0)));
        int ones = seeds.Count(s => CityModels.VariantOf(s, 2) == 1);
        Assert.InRange(ones, 150, 250);
    }

    /// <summary>The city hands over across the vegetation band of each word but Extreme, which it holds at Very Far's.</summary>
    [Theory]
    [InlineData(Utils.VegetationModelsSetting.Off)]
    [InlineData(Utils.VegetationModelsSetting.Near)]
    [InlineData(Utils.VegetationModelsSetting.Far)]
    [InlineData(Utils.VegetationModelsSetting.VeryFar)]
    [InlineData(Utils.VegetationModelsSetting.Extreme)]
    public void TheCityBandStopsAtVeryFar(string word)
    {
        var veryFar = VegetationModels.BandFor(Utils.VegetationModelsSetting.VeryFar);
        Assert.Equal(word == Utils.VegetationModelsSetting.Extreme ? veryFar : VegetationModels.BandFor(word), CityModels.BandFor(word));
        Assert.True(CityModels.BandFor(word).Y <= veryFar.Y);
    }

    /// <summary>Only the slow mode's look reads the shader clock, so the fixed modes' frames pin; the scatter mode
    /// lights no whole floor. The <c>city-buildings</c> suite checks that both materials carry the one look.</summary>
    [Theory]
    [InlineData(CityModels.WindowMode.Scatter)]
    [InlineData(CityModels.WindowMode.Slow)]
    [InlineData(CityModels.WindowMode.Floors)]
    public void OnlyTheSlowModeReadsTheClock(CityModels.WindowMode mode)
    {
        string look = CityModels.LookCode(mode);
        Assert.Contains("csky_city_look(uint seed", look, System.StringComparison.Ordinal);
        Assert.Equal(mode == CityModels.WindowMode.Slow, look.Contains("csky_time", System.StringComparison.Ordinal));
        Assert.Equal(mode != CityModels.WindowMode.Scatter, look.Contains("LIT_FLOORS)", System.StringComparison.Ordinal));
        Assert.Equal(look, CityModels.LookCode(mode));
    }
}
