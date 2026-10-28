using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The vegetation quality's ladder and its GPU and pane fallback. Engine-free: the GPU is
/// handed in rather than asked of a renderer, which the unit host does not have. The graphics-mode
/// gate is in <see cref="GraphicsModeTests"/>.</summary>
public class VegetationQualitySettingTests
{
    /// <summary>A discrete GPU runs Rich at any pane count, on any OS, Linux included, since the GPU
    /// alone is read. An integrated GPU, the Deck's among them, runs Rich on one pane and Original
    /// from two up.</summary>
    [Theory]
    [InlineData(false, 1, VegetationQualitySetting.Rich)]
    [InlineData(false, 2, VegetationQualitySetting.Rich)]
    [InlineData(false, 4, VegetationQualitySetting.Rich)]
    [InlineData(true, 1, VegetationQualitySetting.Rich)]
    [InlineData(true, 2, VegetationQualitySetting.Original)]
    [InlineData(true, 4, VegetationQualitySetting.Original)]
    public void FallbackFollowsTheGpuAndThePanes(bool integrated, int panes, string expected)
    {
        Assert.Equal(expected, VegetationQualitySetting.FallbackFor(integrated, panes));
    }

    /// <summary>A --det run falls back to Rich at any pane count, so a capture does not depend on the
    /// machine it runs on. The det context never asks the renderer.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void ADetRunFallsBackToRich(int panes)
    {
        var none = VegetationQualitySetting.Lookup.Fallback(new SettingContext(true, true, panes));
        Assert.Equal(new ResolvedWord(VegetationQualitySetting.Rich, SettingSource.Default), none);
    }

    /// <summary>The flag beats the saved word, which beats the config key. The split screen's Original
    /// fallback names its rule, and a choice of Rich still beats it.</summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordAndTheLowPowerFallbackNamesItsRule()
    {
        var flag = VegetationQualitySetting.Pick(VegetationQualitySetting.Original, VegetationQualitySetting.Rich, VegetationQualitySetting.Rich);
        Assert.Equal((VegetationQualitySetting.Original, SettingSource.Flag), (flag.Word, flag.Source));
        Assert.Equal("--vegetation-quality", VegetationQualitySetting.Lookup.SourceName(flag.Source));

        var saved = VegetationQualitySetting.Pick(null, VegetationQualitySetting.Original, VegetationQualitySetting.Rich);
        Assert.Equal((VegetationQualitySetting.Original, SettingSource.Saved), (saved.Word, saved.Source));

        string fallback = VegetationQualitySetting.FallbackFor(integrated: true, panes: 2);
        var none = VegetationQualitySetting.Pick(null, null, null, fallback);
        Assert.Equal((VegetationQualitySetting.Original, SettingSource.Default, VegetationQualitySetting.IntegratedSplitRule), (none.Word, none.Source, none.Rule));
        Assert.Equal("default_integrated_gpu_panes", VegetationQualitySetting.Lookup.SourceName(none.Source, none.Rule));
        var solo = VegetationQualitySetting.Pick(null, null, null, VegetationQualitySetting.FallbackFor(integrated: true, panes: 1));
        Assert.Equal((VegetationQualitySetting.Rich, SettingSource.Default, (string?)null), (solo.Word, solo.Source, solo.Rule));
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
