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
        var resolved = Lookup.Resolve(null, null, null, "low", "default_low_power");
        Assert.Equal(new ResolvedWord("low", SettingSource.Default, "default_low_power"), resolved);
        Assert.Equal("default_low_power", Lookup.SourceName(resolved.Source, resolved.Rule));
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

    }

    /// <summary>A registered resolve reads the fallback the run's context gives, and resolves again
    /// only when a moved mode or pane count moves that fallback. A setting never resolved is left
    /// alone, and a word a higher source chose stands.</summary>
    [Fact]
    public void ARefreshResolvesAgainOnlyWhenTheFallbackMoves()
    {
        int takes = 0;
        var setting = new WordSetting("test.refresh", new[] { "low", "mid", "high" })
        {
            Fallback = static context => new ResolvedWord(context.Enhanced ? "high" : "low", SettingSource.Default),
            Take = resolved =>
            {
                takes++;
                return resolved;
            },
        };

        Assert.False(setting.Refresh(enhanced: true, panes: 1));
        Assert.Equal(0, takes);
        Assert.Equal(new ResolvedWord("low", SettingSource.Default), setting.ResolveFrom(null, null, new SettingContext(false, false, 1)));
        Assert.False(setting.Refresh(enhanced: false, panes: 4));
        Assert.Equal(1, takes);
        Assert.True(setting.Refresh(enhanced: true, panes: 1));
        Assert.Equal("high", setting.Current.Word);

        setting.ResolveFrom(null, "mid", new SettingContext(false, false, 1));
        Assert.False(setting.Refresh(enhanced: true, panes: 1));
        Assert.Equal(new ResolvedWord("mid", SettingSource.Saved), setting.Current);
    }

    /// <summary>A setting whose stored word reads a partner's takes it again when the partner
    /// resolves, so the pair agrees whichever resolves last.</summary>
    [Fact]
    public void ARetakeFollowsThePartnerWhicheverResolvesLast()
    {
        string partnerWord = "off";
        var clamped = new WordSetting("test.clamped", new[] { "50", "100", "200" })
        {
            Fallback = static _ => new ResolvedWord("100", SettingSource.Default),
            Take = resolved => partnerWord == "cap" && resolved.Word == "200" ? resolved with { Word = "100" } : resolved,
        };
        var partner = new WordSetting("test.partner", new[] { "off", "cap" })
        {
            Fallback = static _ => new ResolvedWord("off", SettingSource.Default),
            Take = resolved =>
            {
                partnerWord = resolved.Word;
                clamped.Retake();
                return resolved;
            },
        };
        var context = new SettingContext(false, false, 1);

        clamped.ResolveFrom(null, "200", context);
        partner.ResolveFrom(null, "cap", context);
        Assert.Equal("100", clamped.Current.Word);
        partner.ResolveFrom(null, "off", context);
        Assert.Equal("200", clamped.Current.Word);
    }

    /// <summary>Two sets of words compare by what they hold, so an apply exit carrying one compares
    /// by value. A null clears a word rather than storing it.</summary>
    [Fact]
    public void WordValuesCompareByTheirWords()
    {
        var one = new WordValues { [Lookup] = "mid" };
        var two = new WordValues { [Lookup] = "mid" };
        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        two[Lookup] = null;
        Assert.Equal(new WordValues(), two);
        Assert.NotEqual(one, two);
        var copy = one.Copy();
        copy[Lookup] = "high";
        Assert.Equal("mid", one[Lookup]);
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
