using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Volumetric Clouds and Global Illumination options. Each defaults off, resolves as its
/// siblings do, round-trips through the options file and takes an unsaved lab word.</summary>
public class VolumetricCloudsAndGiSettingTests
{
    public static TheoryData<string> Settings => new() { VolumetricCloudsSetting.Key, GlobalIlluminationSetting.Key };

    /// <summary>Both resolve off with nothing set, under <c>--det</c> or not, and are registered.</summary>
    [Theory]
    [MemberData(nameof(Settings))]
    public void BothDefaultOffAndAreRegistered(string key)
    {
        var setting = Of(key);
        Assert.Contains(setting, WordSettings.All);
        Assert.True(setting.EnhancedOnly);
        Assert.Equal("off", setting.Fallback(new SettingContext(Det: false, Enhanced: true, Panes: 1)).Word);
        Assert.Equal("off", setting.Fallback(new SettingContext(Det: true, Enhanced: true, Panes: 1)).Word);
    }

    /// <summary>The flag beats the saved word, which beats the config key, which beats the fallback.</summary>
    [Theory]
    [MemberData(nameof(Settings))]
    public void TheFlagBeatsTheSavedWordWhichBeatsTheConfigKey(string key)
    {
        var setting = Of(key);
        Assert.Equal(("off", SettingSource.Flag), Pair(setting.Resolve("off", "on", "on", "off")));
        Assert.Equal(("on", SettingSource.Saved), Pair(setting.Resolve(null, "on", "off", "off")));
        Assert.Equal(("on", SettingSource.Config), Pair(setting.Resolve(null, null, "on", "off")));
        Assert.Equal(("off", SettingSource.Default), Pair(setting.Resolve(null, null, null, "off")));
        Assert.Equal(("off", SettingSource.Default), Pair(setting.Resolve("maybe", "sometimes", "always", "off")));
    }

    /// <summary>The command line takes <c>--volumetric-clouds=</c> and <c>--global-illumination=</c>
    /// and drops an unknown word; the prototype flags are gone.</summary>
    [Fact]
    public void TheCommandLineParsesBothFlags()
    {
        var spec = SessionSpec.Parse(new[] { "--volumetric-clouds=on", "--global-illumination=off" });
        Assert.Equal("on", spec.WordFlags[VolumetricCloudsSetting.Lookup]);
        Assert.Equal("off", spec.WordFlags[GlobalIlluminationSetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--volumetric-clouds=yes" }).WordFlags[VolumetricCloudsSetting.Lookup]);
        Assert.Equal("--volumetric-clouds", VolumetricCloudsSetting.Lookup.Flag);
        Assert.Equal("--global-illumination", GlobalIlluminationSetting.Lookup.Flag);
    }

    /// <summary>The options file keeps a known word under its field and drops an unknown one.</summary>
    [Fact]
    public void TheOptionsFileKeepsAKnownWordAndDropsAnUnknownOne()
    {
        var kept = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef
        {
            Words = { [VolumetricCloudsSetting.Lookup] = "on", [GlobalIlluminationSetting.Lookup] = "on" },
        }));
        Assert.Equal("on", kept?.Words[VolumetricCloudsSetting.Lookup]);
        Assert.Equal("on", kept?.Words[GlobalIlluminationSetting.Lookup]);
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
        VolumetricCloudsSetting.TakeUnsaved(true);
        GlobalIlluminationSetting.TakeUnsaved(true);
        Assert.True(VolumetricCloudsSetting.Enabled && GlobalIlluminationSetting.Enabled);
        VolumetricCloudsSetting.Lookup.Retake();
        GlobalIlluminationSetting.Lookup.Retake();
        Assert.False(VolumetricCloudsSetting.Enabled || GlobalIlluminationSetting.Enabled);
    }

    private static WordSetting Of(string key) =>
        key == VolumetricCloudsSetting.Key ? VolumetricCloudsSetting.Lookup : GlobalIlluminationSetting.Lookup;

    private static (string, SettingSource) Pair(ResolvedWord resolved) => (resolved.Word, resolved.Source);
}
