using System;
using System.Linq;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The headset's comfort layers, engine-free: the fade through black (<see cref="XrFade"/>) and the
/// vignette's curve (<see cref="XrVignette"/>). Also the two settings' words, store fields and
/// flags (<see cref="VrRenderScaleSetting"/>, <see cref="VrVignetteSetting"/>).
/// </summary>
public class XrComfortTests
{
    private const float Dt = 1f / 90f;

    [Fact]
    public void ACutLandsOnBlackHoldsItForItsOwnFrameThenClearsOverTheFadeIn()
    {
        var fade = new XrFade();
        fade.Cut();
        Assert.Equal(1f, fade.Alpha);
        fade.Step(Dt);
        Assert.Equal(1f, fade.Alpha);
        Assert.Equal(XrFade.Phase.Black, fade.State);

        int frames = 0;
        float last = fade.Alpha;
        while (fade.State != XrFade.Phase.Clear && frames < 1000)
        {
            fade.Step(Dt);
            Assert.True(fade.Alpha < last, $"the fade clears every frame ({fade.Alpha} after {last})");
            last = fade.Alpha;
            frames++;
        }

        Assert.Equal(0f, fade.Alpha);
        Assert.InRange(frames * Dt, XrFade.DefaultInS - Dt, XrFade.DefaultInS + Dt);
    }

    [Fact]
    public void ACoverDarkensOverTheFadeOutAndAnswersOnlyOnceTheViewIsBlack()
    {
        var fade = new XrFade();
        int frames = 0;
        while (!fade.Cover())
        {
            Assert.True(fade.Alpha < 1f);
            fade.Step(Dt);
            frames++;
            Assert.True(frames < 1000);
        }

        Assert.Equal(1f, fade.Alpha);
        Assert.InRange(frames * Dt, XrFade.DefaultOutS - Dt, XrFade.DefaultOutS + Dt);

        // The caller cuts on the frame Cover answered true; the cut holds the black through it.
        fade.Cut();
        fade.Step(Dt);
        Assert.Equal(1f, fade.Alpha);
        fade.Step(Dt);
        Assert.True(fade.Alpha < 1f && fade.State == XrFade.Phase.In);
    }

    [Fact]
    public void ACoverNobodyAsksForAgainClearsRatherThanLeavingTheHeadsetDark()
    {
        var fade = new XrFade();
        fade.Cover();
        fade.Step(Dt);
        fade.Cover();
        fade.Step(Dt);
        float darkened = fade.Alpha;
        Assert.True(darkened > 0f);
        fade.Step(Dt);
        Assert.True(fade.Alpha < darkened && fade.State == XrFade.Phase.In);

        var black = new XrFade(outS: 0f);
        Assert.True(black.Cover(), "a zero fade-out covers at once");
        black.Step(Dt);
        black.Step(Dt);
        Assert.True(black.Alpha < 1f, "and a black nobody holds clears on the next frame");
    }

    [Fact]
    public void AGameCutDuringAFadeInStartsTheBlackOver()
    {
        var fade = new XrFade();
        fade.Cut();
        for (int i = 0; i < 10; i++)
        {
            fade.Step(Dt);
        }

        Assert.True(fade.Alpha < 1f);
        fade.Cut();
        fade.Step(Dt);
        Assert.Equal(1f, fade.Alpha);
    }

    [Fact]
    public void TheVignetteCurveIsZeroBelowTheOnsetFullPastFullRateAndRisesBetween()
    {
        Assert.Equal(0f, XrVignette.TargetStrength(0f));
        Assert.Equal(0f, XrVignette.TargetStrength(XrVignette.OnsetDegS));
        Assert.Equal(1f, XrVignette.TargetStrength(XrVignette.FullDegS));
        Assert.Equal(1f, XrVignette.TargetStrength(400f));
        float last = 0f;
        for (float rate = XrVignette.OnsetDegS; rate <= XrVignette.FullDegS; rate += 5f)
        {
            float s = XrVignette.TargetStrength(rate);
            Assert.True(s >= last);
            last = s;
        }

        Assert.Equal(XrVignette.OpenHalfAngleDeg, XrVignette.HalfAngleDeg(VrVignette.Mild, 0f));
        Assert.Equal(40f, XrVignette.HalfAngleDeg(VrVignette.Mild, 1f));
        Assert.Equal(28f, XrVignette.HalfAngleDeg(VrVignette.Strong, 1f));
        Assert.Equal(XrVignette.OpenHalfAngleDeg, XrVignette.HalfAngleDeg(VrVignette.Off, 1f));
    }

    [Fact]
    public void TheVignetteFollowsTheOriginsTurnRateAndIgnoresACut()
    {
        foreach (var level in new[] { VrVignette.Mild, VrVignette.Strong })
        {
            var vignette = new XrVignette();
            var basis = Basis.Identity;
            vignette.Step(level, basis, Dt, cut: false);
            // A 120 degree-a-second roll for half a second.
            for (int i = 0; i < 45; i++)
            {
                basis = new Basis(Vector3.Back, Mathf.DegToRad(120f * Dt)) * basis;
                vignette.Step(level, basis, Dt, cut: false);
            }

            Assert.InRange(vignette.RateDegS, 119f, 121f);
            Assert.True(vignette.Strength > 0.95f, $"{level}: a hard roll closes the tunnel ({vignette.Strength})");

            // A cut's jump is no turn, and a still view opens the tunnel again, slowly.
            vignette.Step(level, new Basis(Vector3.Up, 2.5f), Dt, cut: true);
            Assert.Equal(0f, vignette.RateDegS);
            float held = vignette.Strength;
            vignette.Step(level, new Basis(Vector3.Up, 2.5f), Dt, cut: false);
            Assert.True(vignette.Strength < held && vignette.Strength > held * 0.9f);
        }
    }

    [Fact]
    public void OffTheVignetteNeverDarkensWhateverTheRate()
    {
        var vignette = new XrVignette();
        var basis = Basis.Identity;
        for (int i = 0; i < 90; i++)
        {
            basis = new Basis(Vector3.Up, Mathf.DegToRad(300f * Dt)) * basis;
            vignette.Step(VrVignette.Off, basis, Dt, cut: false);
        }

        Assert.Equal(0f, vignette.Strength);
        Assert.Equal(0f, vignette.RateDegS);
    }

    [Fact]
    public void TheComfortWordsParseResolveAndLabel()
    {
        Assert.Equal(0.7f, VrRenderScaleSetting.ScaleOf("70"), 5);
        Assert.Equal(1.25f, VrRenderScaleSetting.ScaleOf("125"), 5);
        Assert.Equal(1f, VrRenderScaleSetting.ScaleOf("65"));
        Assert.Equal(1f, VrRenderScaleSetting.ScaleOf(null));
        Assert.Equal(VrVignette.Strong, VrVignetteSetting.LevelOf("strong"));
        Assert.Equal(VrVignette.Off, VrVignetteSetting.LevelOf("STRONG"));
        Assert.Equal(VrVignette.Off, VrVignetteSetting.LevelOf(null));
        Assert.Contains(VrRenderScaleSetting.Default, VrRenderScaleSetting.Words);
        Assert.Equal(VrVignetteSetting.Words.Count, Enum.GetValues<VrVignette>().Length);

        Assert.Equal(new ResolvedWord("60", SettingSource.Flag), VrRenderScaleSetting.Resolve("60", "80"));
        Assert.Equal(new ResolvedWord("80", SettingSource.Saved), VrRenderScaleSetting.Resolve(null, "80"));
        Assert.Equal(new ResolvedWord("100", SettingSource.Default), VrRenderScaleSetting.Resolve(null, "81"));
        Assert.Equal(new ResolvedWord("mild", SettingSource.Saved), VrVignetteSetting.Resolve(null, "mild"));
        Assert.Equal(new ResolvedWord("off", SettingSource.Default), VrVignetteSetting.Resolve(null, null));

        Assert.Equal("100%", VrRenderScaleSetting.Label(null));
        Assert.Equal("70%", VrRenderScaleSetting.Label("70"));
        Assert.Equal("Off", VrVignetteSetting.Label(null));
        Assert.Equal("Strong", VrVignetteSetting.Label("strong"));
        Assert.Equal(VrRenderScaleSetting.Words.Select(w => w + "%"), VrRenderScaleSetting.Labels());
    }

    [Fact]
    public void TheComfortFieldsRoundTripTheStoreAndAnUnknownWordReadsAsNeverSet()
    {
        var def = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { VrRenderScale = "70", VrVignette = "strong" }))!;
        Assert.Equal("70", def.VrRenderScale);
        Assert.Equal("strong", def.VrVignette);
        var bad = OptionsStore.Deserialize("{\"version\":1,\"vrRenderScale\":\"72\",\"vrVignette\":\"on\"}")!;
        Assert.Null(bad.VrRenderScale);
        Assert.Null(bad.VrVignette);
    }

    [Fact]
    public void TheComfortFlagsParseAndAnUnknownWordIsIgnoredWithANote()
    {
        var spec = SessionSpec.Parse(new[] { "--fly", "--xr-render-scale=70", "--xr-vignette=Strong" });
        Assert.Equal("70", spec.XrRenderScale);
        Assert.Equal("strong", spec.XrVignette);

        var none = SessionSpec.Parse(new[] { "--fly" });
        Assert.Null(none.XrRenderScale);
        Assert.Null(none.XrVignette);

        var bad = SessionSpec.Parse(new[] { "--fly", "--xr-render-scale=72", "--xr-vignette=on" });
        Assert.Null(bad.XrRenderScale);
        Assert.Null(bad.XrVignette);
        Assert.Contains(bad.Warnings, n => n.Message.Contains("--xr-render-scale=72", StringComparison.Ordinal));
        Assert.Contains(bad.Warnings, n => n.Message.Contains("--xr-vignette=on", StringComparison.Ordinal));
    }
}
