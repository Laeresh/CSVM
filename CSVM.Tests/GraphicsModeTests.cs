using System;
using System.Linq;
using System.Reflection;
using CSVM.Session.World;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The enhanced-lighting mode's two words and its flag-beats-saved-option-beats-config resolution
/// order. Mirrors <see cref="ClutterFadeTests"/>'s EffectsLevel coverage. The Config-backed
/// fallback branch of <see cref="GraphicsMode.Resolve"/> is not exercised here: <c>Config</c> is
/// process-wide static state shared with every other test class, so only the layers above it
/// (which never read it) are safe to assert without a test order dependency.
/// </summary>
public class GraphicsModeTests
{
    [Theory]
    [InlineData("original", false)]
    [InlineData("ORIGINAL", false)]
    [InlineData(" enhanced ", true)]
    [InlineData("Enhanced", true)]
    public void TheTwoWordsParseToTheirBoolean(string word, bool expected)
    {
        Assert.True(GraphicsMode.TryParse(word, out bool enhanced));
        Assert.Equal(expected, enhanced);
    }

    [Fact]
    public void AnUnknownWordFailsAndDefaultsFalse()
    {
        Assert.False(GraphicsMode.TryParse("ultra", out bool enhanced));
        Assert.False(enhanced);
    }

    [Fact]
    public void AnExplicitCliOverrideWinsAndNeedsNoConfig()
    {
        Assert.True(GraphicsMode.Resolve("enhanced"));
        Assert.True(GraphicsMode.Enhanced);

        Assert.False(GraphicsMode.Resolve("original"));
        Assert.False(GraphicsMode.Enhanced);
    }

    [Fact]
    public void TheSavedOptionDecidesWhenNoFlagIsGiven()
    {
        Assert.True(GraphicsMode.Resolve(null, "enhanced"));
        Assert.True(GraphicsMode.Enhanced);

        Assert.False(GraphicsMode.Resolve(null, "original"));
        Assert.False(GraphicsMode.Enhanced);
    }

    [Fact]
    public void TheFlagBeatsTheSavedOption()
    {
        Assert.True(GraphicsMode.Resolve("enhanced", "original"));
        Assert.True(GraphicsMode.Enhanced);

        Assert.False(GraphicsMode.Resolve("original", "enhanced"));
        Assert.False(GraphicsMode.Enhanced);
    }

    /// <summary>An empty saved option is "never set", not a request for anything, so it falls
    /// through to the config layer exactly as a null one does.</summary>
    [Fact]
    public void AnEmptySavedOptionFallsThroughRatherThanDeciding()
    {
        GraphicsMode.Resolve("enhanced");
        Assert.Equal(GraphicsMode.Resolve(null, null), GraphicsMode.Resolve(null, string.Empty));
    }

    [Fact]
    public void AnUnknownCliOverrideFallsBackToOriginal()
    {
        Assert.False(GraphicsMode.Resolve("ultra"));
        Assert.False(GraphicsMode.Enhanced);
    }

    [Fact]
    public void EnhancedFogScaleIsIdentityInOriginalAndThePushInEnhanced()
    {
        GraphicsMode.Resolve("original");
        Assert.Equal(1f, WeatherRig.EnhancedFogScale());

        GraphicsMode.Resolve("enhanced");
        Assert.Equal(2f, WeatherRig.EnhancedFogScale());

        GraphicsMode.Resolve("original");
    }

    /// <summary>Enhanced pushes a day zone's fog out and leaves a night zone's authored, so a night
    /// city fades with its ground. Original keeps both authored.</summary>
    [Fact]
    public void EnhancedPushesADayZonesFogAndLeavesANightZonesAuthored()
    {
        var authored = new Godot.Vector2(1500f, 2250f);
        var day = Zone(Godot.Colors.Gray);
        var night = Zone(Godot.Colors.Black);
        try
        {
            GraphicsMode.Resolve("enhanced");
            Assert.Equal(authored * 2f, WeatherRig.FogRangeFor(authored, day));
            Assert.Equal(authored, WeatherRig.FogRangeFor(authored, night));
            Assert.Equal(authored * 2f, WeatherRig.FogRangeFor(authored, null));
            GraphicsMode.Resolve("original");
            Assert.Equal(authored, WeatherRig.FogRangeFor(authored, day));
        }
        finally
        {
            GraphicsMode.Resolve("original");
        }
    }

    /// <summary>The approach to a fog volume blends C5's black outer fog into its grey volume fog.
    /// Each end is exact, and halfway the range already sits well inside the outer near.</summary>
    [Fact]
    public void TheVolumeApproachBlendsTheRangeInReciprocalMetres()
    {
        var outside = new WeatherRig.FogWritten(Godot.Vector3.Zero, new Godot.Vector2(1500f, 2250f), new Godot.Vector2(9000f, 10000f), 1f);
        var inside = new WeatherRig.FogWritten(Godot.Vector3.One * 0.005f, new Godot.Vector2(50f, 250f), new Godot.Vector2(9000f, 10000f), 1f);
        Assert.Equal(outside.Range, WeatherRig.BlendFog(outside, inside, 0f).Range);
        Assert.Equal(inside.Range.X, WeatherRig.BlendFog(outside, inside, 1f).Range.X, 3);
        Assert.Equal(inside.Range.Y, WeatherRig.BlendFog(outside, inside, 1f).Range.Y, 3);
        var half = WeatherRig.BlendFog(outside, inside, 0.5f);
        Assert.Equal(96.8f, half.Range.X, 1);
        Assert.Equal(450f, half.Range.Y, 1);
        Assert.Equal(0.0025f, half.ColorLinear.X, 5);
    }

    /// <summary>The View Distance layers as the graphics mode does: the flag beats the saved word,
    /// which beats the config key, which beats the default. A word outside the four falls through.
    /// Kept in this class because the resolve sets the process-wide reach.</summary>
    [Fact]
    public void TheViewDistanceFlagBeatsTheSavedWordWhichBeatsTheConfigKey()
    {
        try
        {
            Assert.Equal(new ResolvedWord("far", SettingSource.Flag), ViewDistance.Resolve("far", "unlimited", "veryfar"));
            Assert.Equal(new ResolvedWord("unlimited", SettingSource.Saved), ViewDistance.Resolve(null, "unlimited", "veryfar"));
            Assert.Equal(new ResolvedWord("veryfar", SettingSource.Config), ViewDistance.Resolve(null, null, "veryfar"));
            Assert.Equal(new ResolvedWord(ViewDistance.Default, SettingSource.Default), ViewDistance.Resolve(null, null, null));
            Assert.Equal(new ResolvedWord("far", SettingSource.Saved), ViewDistance.Resolve("farther", "far", null));
            Assert.Equal(SettingSource.Default, ViewDistance.Resolve(null, "farthest", ViewDistance.Default).Source);
            Assert.Equal("--view-distance", ViewDistance.Lookup.SourceName(SettingSource.Flag));
            Assert.Null(WordSettings.ReadSaved(det: true)[ViewDistance.Lookup]);
        }
        finally
        {
            ViewDistance.Set(null);
        }
    }

    /// <summary>The View Distance reaches the clutter only in enhanced mode, both ways across a
    /// live switch. The faithful fade stays the decoded one whatever is saved.</summary>
    [Fact]
    public void TheViewDistanceReachFollowsTheLiveSwitch()
    {
        try
        {
            ViewDistance.Set("veryfar");
            GraphicsMode.Set(true);
            Assert.Equal(4f, ViewDistance.ClutterReach());
            GraphicsMode.Set(false);
            Assert.Equal(1f, ViewDistance.ClutterReach());
            GraphicsMode.Set(true);
            Assert.Equal(4f, ViewDistance.ClutterReach());
        }
        finally
        {
            GraphicsMode.Resolve("original");
            ViewDistance.Set(null);
        }
    }

    /// <summary>The clutter reads Rich vegetation only under Enhanced and the rich word, both ways
    /// across a live switch. The faithful path stays Original whatever is saved. Here rather than
    /// in its own class, since the graphics mode is process-wide and this class owns its flips.</summary>
    [Fact]
    public void RichVegetationNeedsEnhancedAndTheRichWord()
    {
        string launched = VegetationQualitySetting.Word;
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            GraphicsMode.Set(true);
            Assert.True(VegetationQualitySetting.RichOn);
            GraphicsMode.Set(false);
            Assert.False(VegetationQualitySetting.RichOn);
            GraphicsMode.Set(true);
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Original, null, null);
            Assert.False(VegetationQualitySetting.RichOn);
        }
        finally
        {
            GraphicsMode.Resolve("original");
            VegetationQualitySetting.Resolve(launched, null, null);
        }
    }

    /// <summary>A scissored painted coast's world shader is an unflagged surface's in both modes. Here
    /// because the shader text follows the process-wide mode.</summary>
    [Fact]
    public void AScissoredPaintedCoastTakesNoFade()
    {
        try
        {
            GraphicsMode.Set(false);
            Assert.Equal(WorldText(false, true, paintedCoast: false), WorldText(false, true, paintedCoast: true));
            GraphicsMode.Set(true);
            Assert.Equal(WorldText(false, true, paintedCoast: false), WorldText(false, true, paintedCoast: true));
        }
        finally
        {
            GraphicsMode.Resolve("original");
        }
    }

    /// <summary>A painted coast fades along its own UV only while the ocean is on and never for a spyglass
    /// disc. An opaque ramp fades by a dither, a blended strip by its alpha. Neither takes the mask hide, both
    /// keep the matte arm, and under Original each text is an unflagged surface's.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APaintedCoastFadesByItsOwnUv(bool blend)
    {
        try
        {
            GraphicsMode.Set(false);
            Assert.Equal(WorldText(blend, false, paintedCoast: false), WorldText(blend, false, paintedCoast: true));
            GraphicsMode.Set(true);
            string plain = WorldText(blend, false, paintedCoast: false);
            string coast = WorldText(blend, false, paintedCoast: true);
            Assert.Contains("uniform vec4 coast_fade = vec4(0.0);", coast);
            Assert.Contains($"    if (csky_ocean_on > 0.5 && coast_fade.w > 0.0 && !{CSVM.Mech3.SceneBuilder.FlatSeaEye}) {{", coast);
            Assert.Equal(!blend, coast.Contains("if (!csky_clutter_dither_keep_frame(FRAGCOORD.xy, coast_keep, int(floor(csky_time * 60.0 + 0.5)))) { discard; }", StringComparison.Ordinal));
            Assert.Equal(!blend, coast.Contains(CSVM.Mech3.SceneBuilder.TimeInclude, StringComparison.Ordinal));
            Assert.DoesNotContain("csky_clutter_dither_keep(FRAGCOORD.xy, coast_keep)", coast);
            Assert.Equal(blend, coast.Contains("        ALPHA *= coast_keep;", StringComparison.Ordinal));
            Assert.DoesNotContain("csky_ocean_hides_sea(", coast);
            Assert.Contains("    ROUGHNESS = 1.0;", coast);
            // Every unflagged line survives in order, so the fade adds lines and changes none.
            var lines = coast.Split('\n');
            int at = 0;
            foreach (var line in plain.Split('\n'))
            {
                while (at < lines.Length && lines[at] != line)
                    at++;
                Assert.True(at < lines.Length, $"the coast text lost the line '{line.TrimEnd()}'");
                at++;
            }
        }
        finally
        {
            GraphicsMode.Resolve("original");
        }
    }

    /// <summary>A sea tile's Original text is an unflagged water surface's. Its Enhanced arm, the base sheet's
    /// mask hide, reads the water specular the unit-test host cannot build; graphics-coast-hide holds it.</summary>
    [Fact]
    public void APaintedSeaTileKeepsItsOriginalText()
    {
        try
        {
            GraphicsMode.Set(false);
            Assert.Equal(WorldText(false, false, paintedCoast: false, water: true), WorldText(false, false, paintedCoast: true, water: true));
        }
        finally
        {
            GraphicsMode.Resolve("original");
        }
    }

    /// <summary>The fade a profile gives a painted coast: its axis, water end and crossing. Its width is
    /// the chosen share of the stretch from the crossing to the water end, the whole stretch.</summary>
    [Fact]
    public void ACoastFadeComesFromItsProfile()
    {
        var fade = CSVM.Mech3.SceneBuilder.CoastFade(new CSVM.Mech3.CoastProfile(true, true, 0.75f, 0.6f));
        Assert.Equal(new Godot.Vector4(1f, 1f, 0.75f, 0.25f * CSVM.Mech3.SceneBuilder.CoastFadeShare), fade);
        var strip = CSVM.Mech3.SceneBuilder.CoastFade(new CSVM.Mech3.CoastProfile(false, false, 0.25f, 0.1f));
        Assert.Equal(new Godot.Vector4(0f, 0f, 0.25f, 0.25f * CSVM.Mech3.SceneBuilder.CoastFadeShare), strip);
        Assert.Equal(1f, CSVM.Mech3.SceneBuilder.CoastFadeShare);
        Assert.True(CSVM.Mech3.SceneBuilder.IsFadedStrip("beach1.tif"));
        Assert.False(CSVM.Mech3.SceneBuilder.IsFadedStrip("shore1.tif") || CSVM.Mech3.SceneBuilder.IsFadedStrip("cliff01_trans2"));
    }

    /// <summary>C3's painted shallows are a painted coast that fades out whole. With no profile they take a
    /// crossing past every UV, so coast_keep is 0 at any coordinate the texture reaches.</summary>
    [Fact]
    public void PaintedShallowsFadeOutWhole()
    {
        var profiles = new System.Collections.Generic.Dictionary<string, CSVM.Mech3.CoastProfile>
        {
            ["shore1"] = new CSVM.Mech3.CoastProfile(false, false, 0.188f, 0.1f),
        };
        Assert.True(CSVM.Mech3.SceneBuilder.IsPaintedCoastTexture("shore2.tif") && CSVM.Mech3.SceneBuilder.IsFadedStrip("shore2.tif"));
        var fade = CSVM.Mech3.SceneBuilder.FadeFor("shore2.tif", profiles);
        Assert.NotNull(fade);
        // The shader's own keep: 1 - clamp(sea / width), sea = crossing - u along U from a water end at 0.
        foreach (float u in new[] { -4f, 0f, 0.5f, 1f, 8f })
            Assert.Equal(0f, 1f - Math.Clamp((fade.Value.Z - u) / fade.Value.W, 0f, 1f));
        Assert.Equal(CSVM.Mech3.SceneBuilder.CoastFade(profiles["shore1"]), CSVM.Mech3.SceneBuilder.FadeFor("shore1.tif", profiles));
        Assert.Null(CSVM.Mech3.SceneBuilder.FadeFor("cliff1_watertrans3.tif", profiles));
    }

    /// <summary>The cliff tiles' per-frame dither, csky_clutter_dither_keep_frame spelled in C#. Each frame
    /// keeps the ordered pattern's share of a 4x4 tile. Over 16 frames every pixel keeps exactly alpha's
    /// share, what TAA's history averages to.</summary>
    [Fact]
    public void ThePerFrameDitherAveragesToItsAlpha()
    {
        static bool Keep(int x, int y, float alpha, int frame)
        {
            int v = (((x ^ y) & 1) << 3) | ((y & 1) << 2) | ((x ^ y) & 2) | ((y & 2) >> 1);
            v = (v + (7 * (frame & 15))) & 15;
            return alpha > (v + 0.5f) / 16f;
        }

        string include = System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.RepoRoot, "CSVM", "shaders", "csky_clutter_fade.gdshaderinc"));
        Assert.Contains("v = (v + 7 * (frame & 15)) & 15;", include);
        for (int k = 0; k <= 16; k++)
        {
            float alpha = k / 16f;
            for (int frame = 0; frame < 16; frame++)
            {
                int tile = 0;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        tile += Keep(x, y, alpha, frame) ? 1 : 0;
                Assert.Equal(k, tile);
            }
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int kept = 0;
                    for (int frame = 0; frame < 16; frame++)
                        kept += Keep(x, y, alpha, frame) ? 1 : 0;
                    Assert.Equal(k, kept);
                }
            }
        }
    }

    // A lit, fogged, textured world surface, the painted coasts' case.
    private static string WorldText(bool blend, bool scissor, bool paintedCoast, bool water = false)
    {
        var method = typeof(CSVM.Mech3.SceneBuilder).GetMethod("BiasShaderCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = method.GetParameters().Select(p => p.ParameterType == typeof(bool)
            ? (object)(p.Name switch
            {
                "textured" or "lit" or "fogged" => true,
                "blend" => blend,
                "scissor" => scissor,
                "paintedCoast" => paintedCoast,
                "water" => water,
                _ => false,
            })
            : Activator.CreateInstance(p.ParameterType)!).ToArray();
        return (string)method.Invoke(null, args)!;
    }

    private static CSVM.Effects.WeatherState.ZoneWeather Zone(Godot.Color fog)
        => new(fog, 1500f, 2250f, 0f, 1f, 2500f, 1f, Godot.Vector3.Zero, 1.5f, 0.5f, Godot.Colors.White, Godot.Colors.White);
}
