using System.Collections.Generic;
using System.Linq;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The source order every word-valued graphics setting resolves through: the flag beats
/// the saved word, which beats the config key, which beats the fallback. An unknown word falls
/// through, and an unknown config word warns once the lookup reaches it.</summary>
public class WordSettingTests
{
    private static readonly WordSetting Lookup = new("test.key", new[] { "low", "mid", "high" }, "--test");

    [Fact]
    public void EachSourceBeatsTheOnesBelowIt()
    {
        Assert.Equal(new ResolvedWord("low", SettingSource.Flag), Lookup.Resolve("low", "mid", "high", "mid"));
        Assert.Equal(new ResolvedWord("mid", SettingSource.Saved), Lookup.Resolve(null, "mid", "high", "low"));
        Assert.Equal(new ResolvedWord("high", SettingSource.Config), Lookup.Resolve(null, null, "high", "low"));
        Assert.Equal(new ResolvedWord("low", SettingSource.Default), Lookup.Resolve(null, null, null, "low"));
    }

    /// <summary>A setting's own fallback rule rides through as the source, which is how shadow
    /// quality's GPU and pane rules reach the log line.</summary>
    [Fact]
    public void TheFallbackKeepsTheSourceItWasGiven()
    {
        var resolved = Lookup.Resolve(null, null, null, "low", SettingSource.IntegratedGpuPanesDefault);
        Assert.Equal(new ResolvedWord("low", SettingSource.IntegratedGpuPanesDefault), resolved);
    }

    [Fact]
    public void AConfigKeySpellingTheFallbackReadsAsTheFallback()
    {
        Assert.Equal(new ResolvedWord("mid", SettingSource.Default), Lookup.Resolve(null, null, "mid", "mid"));
    }

    [Theory]
    [InlineData("ultra", "epic", "potato")]
    [InlineData("", "", "")]
    [InlineData("LOW", "Mid", "HIGH")]
    public void AnUnknownWordAtEverySourceFallsThroughToTheFallback(string flag, string saved, string config)
    {
        var lines = new List<string>();
        using (Log.PushConsoleSink(lines.Add))
        {
            Assert.Equal(new ResolvedWord("mid", SettingSource.Default), Lookup.Resolve(flag, saved, config, "mid"));
        }

        Assert.Single(lines, l => l.Contains($"config test.key={config} is not one of low/mid/high; using mid"));
    }

    /// <summary>The warning names a config word only where that word would have decided the run.
    /// A known word, an absent key, or a word a higher source beats stays quiet.</summary>
    [Theory]
    [InlineData(null, null, "high")]
    [InlineData(null, null, null)]
    [InlineData(null, "high", "potato")]
    [InlineData("high", null, "potato")]
    public void OnlyAnUnknownConfigWordTheLookupReachesWarns(string? flag, string? saved, string? config)
    {
        var lines = new List<string>();
        using (Log.PushConsoleSink(lines.Add))
        {
            Lookup.Resolve(flag, saved, config, "mid");
        }

        Assert.DoesNotContain(lines, l => l.Contains("is not one of"));
    }

    [Fact]
    public void EachSourceIsNamedAsTheLogLineSpellsIt()
    {
        Assert.Equal("--test", Lookup.SourceName(SettingSource.Flag));
        Assert.Equal("options.json", Lookup.SourceName(SettingSource.Saved));
        Assert.Equal("test.key", Lookup.SourceName(SettingSource.Config));
        Assert.Equal("default", Lookup.SourceName(SettingSource.Default));
        Assert.Equal("default_integrated_gpu", Lookup.SourceName(SettingSource.IntegratedGpuDefault));
        Assert.Equal("default_integrated_gpu_panes", Lookup.SourceName(SettingSource.IntegratedGpuPanesDefault));
    }

    [Fact]
    public void AWordIsFoundByItsPositionAndNullIsNoWord()
    {
        Assert.Equal(new[] { 0, 1, 2 }, Lookup.Words.Select(Lookup.IndexOf));
        Assert.Equal(-1, Lookup.IndexOf(null));
        Assert.False(Lookup.IsWord(null));
        Assert.False(Lookup.IsWord("medium"));
    }
}
