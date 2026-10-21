using CSVM.Flight.Camera;
using CSVM.Flight.Hangar;
using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The staged Options settings the Options pages share. Borderless pins the size, FSR 2.2
/// clamps the scale, and the Enhanced-only settings stand dead under Original. The apply exit hands
/// back exactly what was loaded. Engine-free, the screens and sizes handed in as lists.</summary>
public class OptionsChoicesTests
{
    /// <summary>Under Original the view distance, shadow and water settings take no pick, each
    /// keeping its word for a later flip to Enhanced. Under Enhanced every word setting is live.</summary>
    [Fact]
    public void TheEnhancedOnlySettingsStandDeadUnderOriginal()
    {
        var choices = new OptionsChoices();
        choices.Load(null);
        Assert.False(choices.Live(ViewDistance.Lookup));
        Assert.False(choices.Live(ShadowQualitySetting.Lookup));
        Assert.False(choices.Live(WaterQualitySetting.Lookup));
        Assert.False(choices.Live(VegetationQualitySetting.Lookup));
        Assert.True(choices.Live(RenderScaleSetting.Lookup));
        Assert.Equal(new WordValues(), choices.ToExit().Words);

        choices.Load(new OptionsDef { GraphicsMode = GraphicsMode.EnhancedWord });
        Assert.All(WordSettings.All, setting => Assert.True(choices.Live(setting), setting.Key));
    }

    /// <summary>Borderless owns the size: the choices read it as pinned, and the saved size rides
    /// the exit unchanged.</summary>
    [Fact]
    public void BorderlessPinsTheSize()
    {
        var choices = new OptionsChoices();
        choices.Load(new OptionsDef { DisplayMode = DisplayWords.Borderless, Resolution = "1024x768" });
        Assert.True(choices.ResolutionPinned);
        Assert.Equal("1024x768", choices.ToExit().Resolution);
    }

    /// <summary>Picking FSR 2.2 pulls a scale above native down to native and narrows the scale
    /// list; a scale at or below native stands.</summary>
    [Fact]
    public void FsrTwoClampsTheRenderScale()
    {
        var choices = new OptionsChoices();
        choices.Load(new OptionsDef
        {
            Words = { [RenderScaleSetting.Lookup] = "200", [AntiAliasingSetting.Lookup] = DisplayWords.AntiAliasingOff },
        });
        choices.Pick(AntiAliasingSetting.Lookup, DisplayWords.AntiAliasingFsr2);
        Assert.Equal(DisplayWords.AntiAliasingFsr2, choices.Words[AntiAliasingSetting.Lookup]);
        Assert.Equal("100", choices.Words[RenderScaleSetting.Lookup]);
        Assert.Equal(4, choices.Offered(RenderScaleSetting.Lookup).Count);
        choices.Load(new OptionsDef { Words = { [RenderScaleSetting.Lookup] = "67" } });
        choices.Pick(AntiAliasingSetting.Lookup, DisplayWords.AntiAliasingFsr2);
        Assert.Equal("67", choices.Words[RenderScaleSetting.Lookup]);
    }
    /// <summary>The apply exit carries back every setting Load read, unchanged. A never-set file
    /// comes back as nulls beside the two settings with a shipped word.</summary>
    [Fact]
    public void ToExitRoundTripsLoad()
    {
        var saved = new OptionsDef
        {
            GraphicsMode = GraphicsMode.EnhancedWord,
            Difficulty = Difficulty.Word(Difficulty.Hard),
            MonitorIndex = "1",
            Resolution = "1024x768",
            DisplayMode = DisplayWords.Fullscreen,
            VSync = "144",
            AudioMaster = 10,
            AudioMusic = 20,
            AudioEffects = 30,
            AudioVoice = 40,
            NearestAfterKill = true,
            Rumble = false,
            DefaultView = PilotView.Name(PilotViewMode.Cockpit),
            AutoHeadTurn = true,
            VrMode = true,
            VrRenderScale = VrRenderScaleSetting.Words[0],
            VrVignette = VrVignetteSetting.Words[^1],
        };
        foreach (var setting in WordSettings.All)
        {
            saved.Words[setting] = setting.Words[^1];
        }

        var choices = new OptionsChoices();
        choices.Load(saved);
        Assert.Equal(
            new OptionsApplyExit(saved.GraphicsMode, saved.Difficulty, saved.MonitorIndex, saved.Resolution,
                saved.DisplayMode, saved.VSync, saved.AudioMaster, saved.AudioMusic, saved.AudioEffects,
                saved.AudioVoice, saved.NearestAfterKill, saved.Rumble, saved.DefaultView, saved.AutoHeadTurn,
                saved.Words, saved.VrMode, saved.VrRenderScale, saved.VrVignette),
            choices.ToExit());

        choices.Load(null);
        Assert.Equal(
            new OptionsApplyExit(GraphicsMode.Default, Difficulty.Word(Difficulty.Normal), null, null, null, null,
                null, null, null, null, null, null, null, null, new WordValues(), null, null, null),
            choices.ToExit());
    }
}
