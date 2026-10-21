using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The vegetation quality's ladder and its platform fallback. Engine-free: the platform and
/// the GPU are handed in rather than asked of a renderer, which the unit host does not have. The
/// graphics-mode gate is in <see cref="GraphicsModeTests"/>.</summary>
public class VegetationQualitySettingTests
{
    /// <summary>A desktop on a discrete GPU runs Rich. Linux, which covers the Deck, and an
    /// integrated GPU each run Original.</summary>
    [Theory]
    [InlineData(false, false, VegetationQualitySetting.Rich)]
    [InlineData(true, false, VegetationQualitySetting.Original)]
    [InlineData(false, true, VegetationQualitySetting.Original)]
    [InlineData(true, true, VegetationQualitySetting.Original)]
    public void FallbackFollowsThePlatformAndTheGpu(bool linux, bool integrated, string expected)
    {
        Assert.Equal(expected, VegetationQualitySetting.FallbackFor(linux, integrated));
    }

    /// <summary>A --det run falls back to Rich whatever the machine, so a capture does not depend on
    /// where it runs.</summary>
    [Fact]
    public void ADetRunFallsBackToRich()
    {
        Assert.Equal(VegetationQualitySetting.Rich, VegetationQualitySetting.DefaultFor(det: true));
    }

    /// <summary>The flag beats the saved word, which beats the config key; the Original fallback names
    /// its rule, and a choice of Rich still beats it.</summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordAndTheLowPowerFallbackNamesItsRule()
    {
        var flag = VegetationQualitySetting.Pick(VegetationQualitySetting.Original, VegetationQualitySetting.Rich, VegetationQualitySetting.Rich);
        Assert.Equal((VegetationQualitySetting.Original, SettingSource.Flag), (flag.Word, flag.Source));
        Assert.Equal("--vegetation-quality", VegetationQualitySetting.Lookup.SourceName(flag.Source));

        var saved = VegetationQualitySetting.Pick(null, VegetationQualitySetting.Original, VegetationQualitySetting.Rich);
        Assert.Equal((VegetationQualitySetting.Original, SettingSource.Saved), (saved.Word, saved.Source));

        string fallback = VegetationQualitySetting.FallbackFor(linux: true, integrated: false);
        var none = VegetationQualitySetting.Pick(null, null, fallback, fallback);
        Assert.Equal((VegetationQualitySetting.Original, SettingSource.Default, VegetationQualitySetting.LowPowerRule), (none.Word, none.Source, none.Rule));
        var rich = VegetationQualitySetting.Pick(null, VegetationQualitySetting.Rich, fallback, fallback);
        Assert.Equal((VegetationQualitySetting.Rich, SettingSource.Saved), (rich.Word, rich.Source));
    }

    /// <summary>The command line and the options file each keep a known word and drop an unknown one.</summary>
    [Fact]
    public void TheFlagAndTheOptionsFileKeepAKnownWordAndDropAnUnknownOne()
    {
        Assert.Equal(VegetationQualitySetting.Original, SessionSpec.Parse(new[] { "--vegetation-quality=original" }).WordFlags[VegetationQualitySetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--vegetation-quality=lush" }).WordFlags[VegetationQualitySetting.Lookup]);
        var kept = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { Words = { [VegetationQualitySetting.Lookup] = VegetationQualitySetting.Original } }));
        Assert.Equal(VegetationQualitySetting.Original, kept?.Words[VegetationQualitySetting.Lookup]);
        var dropped = OptionsStore.Deserialize("{\"version\":1,\"vegetationQuality\":\"lush\"}");
        Assert.NotNull(dropped);
        Assert.Null(dropped!.Words[VegetationQualitySetting.Lookup]);
    }
}
