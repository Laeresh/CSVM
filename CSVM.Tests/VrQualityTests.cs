using CSVM.Spec;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The VR quality profile's selection, engine-free (<see cref="VrQuality"/>). It covers the words a
/// headset session flies at, what a flat session keeps and which flags beat the profile. Also the
/// flags that reach it (<c>--xr-quality</c>, <c>--anti-aliasing</c>, <c>--msaa</c>,
/// <c>--xr-foveation</c>).
/// </summary>
public class VrQualityTests
{
    [Fact]
    public void AFlatSessionReplacesNothing()
    {
        var plan = VrQuality.Resolve(headset: false, null, null, null, null, null);
        Assert.Equal(VrQuality.None, plan.Profile);
        Assert.Null(plan.AntiAliasing);
        Assert.Null(plan.Msaa);
        Assert.Null(plan.ShadowQuality);
        Assert.False(plan.Foveation);
        Assert.Null(VrQuality.Describe(plan));
    }

    [Fact]
    public void AFlatSessionKeepsItsOwnFlagsAndNeverFoveates()
    {
        var plan = VrQuality.Resolve(headset: false, VrQuality.Vr, "fxaa", "2x", "medium", "on");
        Assert.Equal(VrQuality.None, plan.Profile);
        Assert.Equal("fxaa", plan.AntiAliasing);
        Assert.Equal("2x", plan.Msaa);
        Assert.Equal("medium", plan.ShadowQuality);
        Assert.False(plan.Foveation);
    }

    [Fact]
    public void AHeadsetFliesTheProfileByDefault()
    {
        var plan = VrQuality.Resolve(headset: true, null, null, null, null, null);
        Assert.Equal(VrQuality.Vr, plan.Profile);
        Assert.Equal(DisplayWords.AntiAliasingOff, plan.AntiAliasing);
        Assert.Equal("4x", plan.Msaa);
        Assert.Equal(ShadowQualitySetting.Low, plan.ShadowQuality);
        Assert.Equal(VrQuality.Foveation, plan.Foveation);
        Assert.Contains("profile=vr", VrQuality.Describe(plan));
    }

    [Fact]
    public void TheProfileStaysOffEveryPenumbraLevel()
    {
        // Godot 4.7.0's soft sun shadow grows with the distance from the world origin, which a VR
        // frame at the runtime's own size cannot carry.
        Assert.Equal(0f, ShadowQualitySetting.PlanFor(VrQuality.ShadowQuality).AngularDistance);
        Assert.True(AntiAliasingSetting.TryParse(VrQuality.AntiAliasing, out var method));
        Assert.Equal(AntiAliasingMethod.Off, method);
    }

    [Fact]
    public void FlatQualityUnderAHeadsetKeepsTheFlatWords()
    {
        var plan = VrQuality.Resolve(headset: true, VrQuality.Flat, null, null, null, null);
        Assert.Equal(VrQuality.Flat, plan.Profile);
        Assert.Null(plan.AntiAliasing);
        Assert.Null(plan.Msaa);
        Assert.Null(plan.ShadowQuality);
        Assert.False(plan.Foveation);
    }

    [Fact]
    public void ASettingsOwnFlagBeatsTheProfile()
    {
        var plan = VrQuality.Resolve(headset: true, null, "taa", "2x", "ultra", "on");
        Assert.Equal(VrQuality.Vr, plan.Profile);
        Assert.Equal("taa", plan.AntiAliasing);
        Assert.Equal("2x", plan.Msaa);
        Assert.Equal("ultra", plan.ShadowQuality);
        Assert.True(plan.Foveation);
        Assert.False(VrQuality.Resolve(headset: true, VrQuality.Flat, null, null, null, "off").Foveation);
        Assert.True(VrQuality.Resolve(headset: true, VrQuality.Flat, null, null, null, "on").Foveation);
    }

    [Fact]
    public void TheMsaaWordsSpellGodotsLevels()
    {
        Assert.Equal(Viewport.Msaa.Disabled, VrQuality.MsaaOf("off"));
        Assert.Equal(Viewport.Msaa.Msaa2X, VrQuality.MsaaOf("2x"));
        Assert.Equal(Viewport.Msaa.Msaa4X, VrQuality.MsaaOf(VrQuality.Msaa));
        Assert.Equal(Viewport.Msaa.Msaa8X, VrQuality.MsaaOf("8x"));
        Assert.Null(VrQuality.MsaaOf("16x"));
        Assert.Null(VrQuality.MsaaOf(null));
    }

    [Fact]
    public void TheFlagsReachTheSpecAndAnUnknownWordIsDropped()
    {
        var spec = SessionSpec.Parse(new[] { "--fly", "--xr-quality=flat", "--anti-aliasing=fxaa", "--msaa=2x", "--xr-foveation=on" });
        Assert.Equal(VrQuality.Flat, spec.XrQuality);
        Assert.Equal("fxaa", spec.WordFlags[AntiAliasingSetting.Lookup]);
        Assert.Equal("2x", spec.Msaa);
        Assert.Equal("on", spec.XrFoveation);

        var none = SessionSpec.Parse(new[] { "--fly" });
        Assert.Null(none.XrQuality);
        Assert.Null(none.WordFlags[AntiAliasingSetting.Lookup]);
        Assert.Null(none.Msaa);
        Assert.Null(none.XrFoveation);

        var bad = SessionSpec.Parse(new[] { "--fly", "--xr-quality=ultra", "--anti-aliasing=msaa", "--msaa=3x", "--xr-foveation=yes" });
        Assert.Null(bad.XrQuality);
        Assert.Null(bad.WordFlags[AntiAliasingSetting.Lookup]);
        Assert.Null(bad.Msaa);
        Assert.Null(bad.XrFoveation);
    }

    [Fact]
    public void TheAntiAliasingFlagWordBeatsTheSavedWord()
    {
        var resolved = AntiAliasingSetting.Lookup.Resolve(DisplayWords.AntiAliasingOff, DisplayWords.AntiAliasingTaa, null,
            AntiAliasingSetting.DefaultFor(true));
        Assert.Equal(DisplayWords.AntiAliasingOff, resolved.Word);
        Assert.Equal(SettingSource.Flag, resolved.Source);
    }
}
