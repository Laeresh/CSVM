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

    /// <summary>The look rolls single windows and whole floors on the shader clock, never Godot's TIME, so a fixed
    /// step pins its frame. Its text is the same at every call, and the <c>city-buildings</c> suite checks that both
    /// materials carry it.</summary>
    [Fact]
    public void TheLookRollsWindowsAndFloorsOnTheShaderClock()
    {
        string look = CityModels.LookCode();
        Assert.Contains("csky_city_look(uint seed", look, System.StringComparison.Ordinal);
        Assert.Contains("csky_time", look, System.StringComparison.Ordinal);
        Assert.DoesNotContain("TIME", look, System.StringComparison.Ordinal);
        Assert.Contains("LIT_FLOORS)", look, System.StringComparison.Ordinal);
        Assert.Contains("albedo *= csky_city_roof(csky_city_rand(s + 6u))", look, System.StringComparison.Ordinal);
        // Class 3, the cornice line, takes the ungraded metal cell (every class over 1.5) and its own gain.
        Assert.Contains("bool metal = uv2.y > 1.5;", look, System.StringComparison.Ordinal);
        Assert.Contains("if (uv2.y > 2.5) {", look, System.StringComparison.Ordinal);
        Assert.Equal(look, CityModels.LookCode());
    }
}
