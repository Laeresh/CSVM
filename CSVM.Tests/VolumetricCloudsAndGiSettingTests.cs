using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Volumetric Clouds and Global Illumination options. Clouds default off and GI on, off on
/// an integrated GPU. Each resolves as its siblings do, round-trips through the options file and takes
/// an unsaved lab word. The clouds' words but off name a march resolution, and an older build's on reads half.</summary>
public class VolumetricCloudsAndGiSettingTests
{
    public static TheoryData<string> Settings => new() { VolumetricCloudsSetting.Key, GlobalIlluminationSetting.Key };

    /// <summary>Both resolve off under <c>--det</c> with nothing set, and are registered.</summary>
    [Theory]
    [MemberData(nameof(Settings))]
    public void BothFallBackOffUnderDetAndAreRegistered(string key)
    {
        var setting = Of(key);
        Assert.Contains(setting, WordSettings.All);
        Assert.True(setting.EnhancedOnly);
        Assert.Equal("off", setting.Fallback(new SettingContext(Det: true, Enhanced: true, Panes: 1)).Word);
    }

    /// <summary>The clouds fall back off on every machine.</summary>
    [Fact]
    public void TheCloudsDefaultOff() =>
        Assert.Equal("off", VolumetricCloudsSetting.Lookup.Fallback(new SettingContext(Det: false, Enhanced: true, Panes: 1)).Word);

    /// <summary>The clouds' row offers off, quarter, half and full in that order, and each word but off
    /// draws the clouds at its own march divisor.</summary>
    [Fact]
    public void TheCloudsWordsNameTheMarchResolution()
    {
        Assert.Equal(new[] { "off", "quarter", "half", "full" }, VolumetricCloudsSetting.Lookup.Words);
        Assert.Equal(new[] { "Off", "Quarter", "Half", "Full" }, VolumetricCloudsSetting.Lookup.Labels);
        try
        {
            foreach (var (word, enabled, divisor) in new[] { ("off", false, 2), ("quarter", true, 4), ("half", true, 2), ("full", true, 1) })
            {
                VolumetricCloudsSetting.TakeUnsaved(word);
                Assert.Equal((word, enabled, divisor), (VolumetricCloudsSetting.Word, VolumetricCloudsSetting.Enabled, VolumetricCloudsSetting.MarchDivisor));
            }

            VolumetricCloudsSetting.TakeUnsaved("on");
            Assert.Equal("half", VolumetricCloudsSetting.Word);
            VolumetricCloudsSetting.TakeUnsaved("eighth");
            Assert.Equal("off", VolumetricCloudsSetting.Word);
        }
        finally
        {
            VolumetricCloudsSetting.TakeUnsaved(VolumetricCloudsSetting.Default);
        }
    }

    /// <summary>GI falls back on for a discrete GPU, off for an integrated one, and off under
    /// <c>--det</c> on either, each fallback naming its rule.</summary>
    [Fact]
    public void GiFallsBackOnForADiscreteGpuOnly()
    {
        Assert.Equal((GlobalIlluminationSetting.On, (string?)null), Rule(GlobalIlluminationSetting.FallbackFor(det: false, integrated: false)));
        Assert.Equal((GlobalIlluminationSetting.Off, GlobalIlluminationSetting.IntegratedRule),
            Rule(GlobalIlluminationSetting.FallbackFor(det: false, integrated: true)));
        Assert.Equal((GlobalIlluminationSetting.Off, GlobalIlluminationSetting.DetRule), Rule(GlobalIlluminationSetting.FallbackFor(det: true, integrated: false)));
        Assert.Equal((GlobalIlluminationSetting.Off, GlobalIlluminationSetting.DetRule), Rule(GlobalIlluminationSetting.FallbackFor(det: true, integrated: true)));
    }

    /// <summary>The flag beats the saved word, which beats the config key, which beats the fallback.</summary>
    [Theory]
    [MemberData(nameof(Settings))]
    public void TheFlagBeatsTheSavedWordWhichBeatsTheConfigKey(string key)
    {
        var setting = Of(key);
        string lit = Lit(key);
        Assert.Equal(("off", SettingSource.Flag), Pair(setting.Resolve("off", lit, lit, "off")));
        Assert.Equal((lit, SettingSource.Saved), Pair(setting.Resolve(null, lit, "off", "off")));
        Assert.Equal((lit, SettingSource.Config), Pair(setting.Resolve(null, null, lit, "off")));
        Assert.Equal(("off", SettingSource.Default), Pair(setting.Resolve(null, null, null, "off")));
        Assert.Equal(("off", SettingSource.Default), Pair(setting.Resolve("maybe", "sometimes", "always", "off")));
    }

    /// <summary>The clouds' retired on reads as half from the flag, the saved word and the config key.</summary>
    [Fact]
    public void TheCloudsOldOnReadsAsHalfFromEverySource()
    {
        var setting = VolumetricCloudsSetting.Lookup;
        Assert.Equal(("half", SettingSource.Flag), Pair(setting.Resolve("on", "full", "full", "off")));
        Assert.Equal(("half", SettingSource.Saved), Pair(setting.Resolve(null, "on", "full", "off")));
        Assert.Equal(("half", SettingSource.Config), Pair(setting.Resolve(null, null, "on", "off")));
        Assert.Equal("half", setting.Known("on"));
        Assert.Null(GlobalIlluminationSetting.Lookup.Known("half"));
    }

    /// <summary>The command line takes <c>--volumetric-clouds=</c> over the four words, its retired
    /// <c>on</c> as half, and <c>--global-illumination=</c>. It drops an unknown word, and
    /// <c>--cloud-march=</c> is gone.</summary>
    [Fact]
    public void TheCommandLineParsesBothFlags()
    {
        foreach (string word in VolumetricCloudsSetting.Words)
            Assert.Equal(word, SessionSpec.Parse(new[] { $"--volumetric-clouds={word}" }).WordFlags[VolumetricCloudsSetting.Lookup]);
        var spec = SessionSpec.Parse(new[] { "--volumetric-clouds=on", "--global-illumination=off" });
        Assert.Equal("half", spec.WordFlags[VolumetricCloudsSetting.Lookup]);
        Assert.Equal("off", spec.WordFlags[GlobalIlluminationSetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--volumetric-clouds=yes" }).WordFlags[VolumetricCloudsSetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--cloud-march=full" }).WordFlags[VolumetricCloudsSetting.Lookup]);
        Assert.Equal("--volumetric-clouds", VolumetricCloudsSetting.Lookup.Flag);
        Assert.Equal("--global-illumination", GlobalIlluminationSetting.Lookup.Flag);
    }

    /// <summary>The options file keeps each known word under its field, reads an older build's clouds
    /// on as half, and drops an unknown word.</summary>
    [Fact]
    public void TheOptionsFileKeepsAKnownWordAndDropsAnUnknownOne()
    {
        foreach (string word in VolumetricCloudsSetting.Words)
        {
            var kept = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef
            {
                Words = { [VolumetricCloudsSetting.Lookup] = word, [GlobalIlluminationSetting.Lookup] = "on" },
            }));
            Assert.Equal(word, kept?.Words[VolumetricCloudsSetting.Lookup]);
            Assert.Equal("on", kept?.Words[GlobalIlluminationSetting.Lookup]);
        }

        var legacy = OptionsStore.Deserialize("{\"version\":1,\"volumetricClouds\":\"on\"}");
        Assert.Equal("half", legacy?.Words[VolumetricCloudsSetting.Lookup]);
        var dropped = OptionsStore.Deserialize("{\"version\":1,\"volumetricClouds\":\"yes\",\"globalIllumination\":\"lots\"}");
        Assert.NotNull(dropped);
        Assert.Null(dropped!.Words[VolumetricCloudsSetting.Lookup]);
        Assert.Null(dropped.Words[GlobalIlluminationSetting.Lookup]);
    }

    /// <summary>A lab's unsaved word stands until the resolved word is taken again, as a session's
    /// end does.</summary>
    [Fact]
    public void ALabWordLastsUntilTheResolvedWordIsTakenAgain()
    {
        var context = new SettingContext(Det: true, Enhanced: true, Panes: 1);
        VolumetricCloudsSetting.Lookup.ResolveFrom(null, null, context);
        GlobalIlluminationSetting.Lookup.ResolveFrom(null, null, context);
        VolumetricCloudsSetting.TakeUnsaved(VolumetricCloudsSetting.Full);
        GlobalIlluminationSetting.TakeUnsaved(true);
        Assert.True(VolumetricCloudsSetting.Enabled && GlobalIlluminationSetting.Enabled);
        VolumetricCloudsSetting.Lookup.Retake();
        GlobalIlluminationSetting.Lookup.Retake();
        Assert.False(VolumetricCloudsSetting.Enabled || GlobalIlluminationSetting.Enabled);
    }

    /// <summary>The VIDEO page's ACCEPT carries the saved or picked word, never a lab's unsaved one, which
    /// its row only shows while nothing is saved.</summary>
    [Fact]
    public void AnOptionsApplyNeverSavesALabsUnsavedWord()
    {
        try
        {
            VolumetricCloudsSetting.TakeUnsaved(VolumetricCloudsSetting.Full);
            var choices = new UI.Menu.OptionsChoices();
            choices.Load(new OptionsDef { Words = { [VolumetricCloudsSetting.Lookup] = VolumetricCloudsSetting.Quarter } });
            Assert.Equal(VolumetricCloudsSetting.Quarter, choices.ToExit().Words[VolumetricCloudsSetting.Lookup]);
            choices.Load(null);
            Assert.Equal(VolumetricCloudsSetting.Full, VolumetricCloudsSetting.Lookup.Words[choices.At(VolumetricCloudsSetting.Lookup)]);
            Assert.Null(choices.ToExit().Words[VolumetricCloudsSetting.Lookup]);
            choices.Pick(VolumetricCloudsSetting.Lookup, VolumetricCloudsSetting.Half);
            Assert.Equal(VolumetricCloudsSetting.Half, choices.ToExit().Words[VolumetricCloudsSetting.Lookup]);
        }
        finally
        {
            VolumetricCloudsSetting.TakeUnsaved(VolumetricCloudsSetting.Default);
        }
    }

    private static WordSetting Of(string key) =>
        key == VolumetricCloudsSetting.Key ? VolumetricCloudsSetting.Lookup : GlobalIlluminationSetting.Lookup;

    // A word that turns the option on: half for the clouds, on for GI.
    private static string Lit(string key) => key == VolumetricCloudsSetting.Key ? VolumetricCloudsSetting.Half : GlobalIlluminationSetting.On;

    private static (string, SettingSource) Pair(ResolvedWord resolved) => (resolved.Word, resolved.Source);

    private static (string, string?) Rule(ResolvedWord resolved)
    {
        Assert.Equal(SettingSource.Default, resolved.Source);
        return (resolved.Word, resolved.Rule);
    }
}
