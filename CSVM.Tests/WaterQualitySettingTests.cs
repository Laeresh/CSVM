using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The water quality's ladder and its GPU fallback. Engine-free: the GPU class is handed in
/// rather than asked of a renderer, which the unit host does not have.</summary>
public class WaterQualitySettingTests
{
    /// <summary>A discrete GPU runs waves on any OS; an integrated GPU, the Deck's among them, runs
    /// flat.</summary>
    [Theory]
    [InlineData(false, WaterQualitySetting.Waves)]
    [InlineData(true, WaterQualitySetting.Flat)]
    public void FallbackFollowsTheGpu(bool integrated, string expected)
    {
        Assert.Equal(expected, WaterQualitySetting.FallbackFor(integrated));
    }

    /// <summary>A --det run falls back to waves whatever the machine, so a capture does not depend
    /// on where it runs.</summary>
    [Fact]
    public void ADetRunFallsBackToWaves()
    {
        Assert.Equal(WaterQualitySetting.Waves, WaterQualitySetting.DefaultFor(det: true));
    }

    /// <summary>The flag beats the saved word, which beats the config key, which beats the fallback.
    /// </summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordWhichBeatsTheConfigKey()
    {
        var flag = WaterQualitySetting.Pick(WaterQualitySetting.Flat, WaterQualitySetting.Waves, WaterQualitySetting.Waves);
        Assert.Equal((WaterQualitySetting.Flat, SettingSource.Flag), (flag.Word, flag.Source));
        Assert.Equal("--water-quality", WaterQualitySetting.Lookup.SourceName(flag.Source));

        var saved = WaterQualitySetting.Pick(null, WaterQualitySetting.Flat, WaterQualitySetting.Waves);
        Assert.Equal((WaterQualitySetting.Flat, SettingSource.Saved), (saved.Word, saved.Source));

        var key = WaterQualitySetting.Pick(null, null, WaterQualitySetting.Flat);
        Assert.Equal((WaterQualitySetting.Flat, SettingSource.Config), (key.Word, key.Source));
        Assert.Equal(WaterQualitySetting.Key, WaterQualitySetting.Lookup.SourceName(key.Source));

        var fallback = WaterQualitySetting.Pick(null, null, WaterQualitySetting.Waves);
        Assert.Equal((WaterQualitySetting.Waves, SettingSource.Default), (fallback.Word, fallback.Source));
    }

    /// <summary>The flat fallback names the rule that chose it. A choice of waves still beats it
    /// there, since the rule moves a default, never a choice.</summary>
    [Fact]
    public void TheLowPowerFallbackHasItsOwnSourceAndAChoiceBeatsIt()
    {
        string fallback = WaterQualitySetting.FallbackFor(integrated: true);
        var none = WaterQualitySetting.Pick(null, null, fallback, fallback);
        Assert.Equal((WaterQualitySetting.Flat, SettingSource.Default, WaterQualitySetting.LowPowerRule), (none.Word, none.Source, none.Rule));
        Assert.Equal("default_integrated_gpu", WaterQualitySetting.Lookup.SourceName(none.Source, none.Rule));

        var saved = WaterQualitySetting.Pick(null, WaterQualitySetting.Waves, fallback, fallback);
        Assert.Equal((WaterQualitySetting.Waves, SettingSource.Saved), (saved.Word, saved.Source));

        var key = WaterQualitySetting.Pick(null, null, WaterQualitySetting.Waves, fallback);
        Assert.Equal((WaterQualitySetting.Waves, SettingSource.Config), (key.Word, key.Source));
    }

    /// <summary>An unknown word at every layer falls through to the fallback.</summary>
    [Fact]
    public void AnUnknownWordFallsThrough()
    {
        var unknown = WaterQualitySetting.Pick("choppy", "stormy", "glassy");
        Assert.Equal((WaterQualitySetting.Waves, SettingSource.Default), (unknown.Word, unknown.Source));
    }

    /// <summary>The command line takes the flag and drops an unknown word with a note.</summary>
    [Fact]
    public void TheCommandLineParsesTheFlagAndDropsAnUnknownWord()
    {
        Assert.Equal(WaterQualitySetting.Flat, SessionSpec.Parse(new[] { "--water-quality=flat" }).WordFlags[WaterQualitySetting.Lookup]);
        Assert.Equal(WaterQualitySetting.Waves, SessionSpec.Parse(new[] { "--water-quality=waves" }).WordFlags[WaterQualitySetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--water-quality=choppy" }).WordFlags[WaterQualitySetting.Lookup]);
    }

    /// <summary>The options file keeps a known word and drops an unknown one as never set.</summary>
    [Fact]
    public void TheOptionsFileKeepsAKnownWordAndDropsAnUnknownOne()
    {
        var kept = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { Words = { [WaterQualitySetting.Lookup] = WaterQualitySetting.Flat } }));
        Assert.Equal(WaterQualitySetting.Flat, kept?.Words[WaterQualitySetting.Lookup]);
        var dropped = OptionsStore.Deserialize("{\"version\":1,\"waterQuality\":\"choppy\"}");
        Assert.NotNull(dropped);
        Assert.Null(dropped!.Words[WaterQualitySetting.Lookup]);
    }
}
