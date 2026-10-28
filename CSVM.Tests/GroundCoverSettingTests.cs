using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Ground Cover word: its ladder, its GPU fallback and <c>--det</c>'s, and its flag and saved field.
/// Engine-free: the GPU is handed in rather than asked of a renderer.</summary>
[Trait("Tier", "Quick")]
public class GroundCoverSettingTests
{
    /// <summary>A discrete GPU on any OS, Linux included, runs Cards. An integrated GPU, the Deck's among them,
    /// runs Off and names its rule.</summary>
    [Fact]
    public void FallbackFollowsTheGpu()
    {
        Assert.Equal(GroundCoverSetting.Cards, GroundCoverSetting.FallbackFor(integrated: false));
        Assert.Equal(GroundCoverSetting.Off, GroundCoverSetting.FallbackFor(integrated: true));
        var none = GroundCoverSetting.Lookup.Resolve(null, null, null, GroundCoverSetting.FallbackFor(integrated: true), GroundCoverSetting.IntegratedRule);
        Assert.Equal("default_integrated_gpu", GroundCoverSetting.Lookup.SourceName(none.Source, none.Rule));
    }

    /// <summary>The flag beats the saved word, which beats the config key. With none set, a <c>--det</c> run
    /// falls back to Cards at any pane count, never asking the renderer.</summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordAndCardsIsTheFallback()
    {
        string launched = GroundCoverSetting.Word;
        try
        {
            Assert.Equal(new[] { "off", "shells", "cards" }, GroundCoverSetting.Words);
            Assert.Equal(GroundCoverSetting.Cards, GroundCoverSetting.Default);

            var flag = GroundCoverSetting.Resolve(GroundCoverSetting.Shells, GroundCoverSetting.Off, GroundCoverSetting.Off);
            Assert.Equal((GroundCoverSetting.Shells, SettingSource.Flag), (flag.Word, flag.Source));
            Assert.Equal("--ground-cover", GroundCoverSetting.Lookup.SourceName(flag.Source));

            var saved = GroundCoverSetting.Resolve(null, GroundCoverSetting.Off, GroundCoverSetting.Shells);
            Assert.Equal((GroundCoverSetting.Off, SettingSource.Saved), (saved.Word, saved.Source));
            Assert.Equal(GroundCoverSetting.Off, GroundCoverSetting.Word);

            var none = GroundCoverSetting.Resolve(null, null, null);
            Assert.Equal((GroundCoverSetting.Cards, SettingSource.Default), (none.Word, none.Source));
            foreach (int panes in new[] { 1, 2, 4 })
                Assert.Equal(new ResolvedWord(GroundCoverSetting.Cards, SettingSource.Default), GroundCoverSetting.Lookup.Fallback(new SettingContext(true, true, panes)));
            Assert.True(GroundCoverSetting.Lookup.EnhancedOnly);
            Assert.Contains(GroundCoverSetting.Lookup, WordSettings.All);
        }
        finally
        {
            GroundCoverSetting.Resolve(launched, null, null);
        }
    }

    /// <summary>The command line and the options file each keep a known word and drop an unknown one; the
    /// prototype flags are gone.</summary>
    [Fact]
    public void TheFlagAndTheOptionsFileKeepAKnownWordAndDropAnUnknownOne()
    {
        Assert.Equal(GroundCoverSetting.Shells, SessionSpec.Parse(new[] { "--ground-cover=shells" }).WordFlags[GroundCoverSetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--ground-cover=on" }).WordFlags[GroundCoverSetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--ground-cover-cards" }).WordFlags[GroundCoverSetting.Lookup]);
        var kept = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { Words = { [GroundCoverSetting.Lookup] = GroundCoverSetting.Off } }));
        Assert.Equal(GroundCoverSetting.Off, kept?.Words[GroundCoverSetting.Lookup]);
        Assert.Contains("\"groundCover\"", OptionsStore.Serialize(new OptionsDef { Words = { [GroundCoverSetting.Lookup] = GroundCoverSetting.Off } }));
        var dropped = OptionsStore.Deserialize("{\"version\":1,\"groundCover\":\"meadow\"}");
        Assert.NotNull(dropped);
        Assert.Null(dropped!.Words[GroundCoverSetting.Lookup]);
    }

    /// <summary>A <c>--det</c> launch reads no saved word, so a saved Ground Cover never reaches a scripted capture.</summary>
    [Fact]
    public void ADetLaunchReadsNoSavedWord() => Assert.Null(WordSettings.ReadSaved(det: true)[GroundCoverSetting.Lookup]);
}
