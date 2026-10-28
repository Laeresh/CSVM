using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Ground Cover word: its ladder, its one fallback on every machine and under <c>--det</c>, and its
/// flag and saved field. Engine-free.</summary>
[Trait("Tier", "Quick")]
public class GroundCoverSettingTests
{
    /// <summary>The flag beats the saved word, which beats the config key, and with none set every machine runs
    /// Cards, under <c>--det</c> or not.</summary>
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
            foreach (bool det in new[] { true, false })
                Assert.Equal(GroundCoverSetting.Cards, GroundCoverSetting.Lookup.Fallback(new SettingContext(det, true, 1)).Word);
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
