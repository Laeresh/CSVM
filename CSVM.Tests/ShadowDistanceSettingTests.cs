using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Shadow Distance word: its ladder, its scales and its flag. Engine-free. The mode gate
/// reads the process-wide <see cref="GraphicsMode"/>, so it is pinned in <see cref="GraphicsModeTests"/>.</summary>
[Trait("Tier", "Quick")]
public class ShadowDistanceSettingTests
{
    /// <summary>Normal keeps the zone's distance, Far and Extreme push it by 1.5 and 2.</summary>
    [Theory]
    [InlineData(ShadowDistanceSetting.Normal, 1f)]
    [InlineData(ShadowDistanceSetting.Far, 1.5f)]
    [InlineData(ShadowDistanceSetting.Extreme, 2f)]
    [InlineData("farther", 1f)]
    [InlineData(null, 1f)]
    public void EachWordNamesItsScale(string? word, float scale)
    {
        Assert.Equal(scale, ShadowDistanceSetting.ScaleFor(word));
    }

    /// <summary>The flag beats the saved word, which beats the config key, then Normal.</summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordAndNormalIsTheFallback()
    {
        var lookup = ShadowDistanceSetting.Lookup;
        Assert.Equal(new ResolvedWord(ShadowDistanceSetting.Far, SettingSource.Flag),
            lookup.Resolve(ShadowDistanceSetting.Far, ShadowDistanceSetting.Extreme, null, ShadowDistanceSetting.Default));
        Assert.Equal(new ResolvedWord(ShadowDistanceSetting.Extreme, SettingSource.Saved),
            lookup.Resolve(null, ShadowDistanceSetting.Extreme, ShadowDistanceSetting.Far, ShadowDistanceSetting.Default));
        Assert.Equal(ShadowDistanceSetting.Normal, lookup.Fallback(new SettingContext(true, true, 1)).Word);
        Assert.Equal("--shadow-distance", lookup.SourceName(SettingSource.Flag));
        Assert.Equal(ShadowDistanceSetting.Extreme, SessionSpec.Parse(new[] { "--shadow-distance=extreme" }).WordFlags[lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--shadow-distance=far-far" }).WordFlags[lookup]);
        Assert.Contains(lookup, WordSettings.All);
    }
}
