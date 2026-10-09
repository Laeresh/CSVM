using CSVM.Flight.Camera;
using CSVM.Flight.Hangar;
using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The staged Options settings both presentations share. Every numbered row steps, labels
/// and details from its one table entry. Borderless pins the size, FSR 2.2 clamps the scale, and
/// the Enhanced-only rows stand dead under Original. The apply exit hands back exactly what was
/// loaded. Engine-free, the screens and sizes handed in as lists.</summary>
public class OptionsChoicesTests
{
    /// <summary>Every row draws "title: value", says what it does, and moves its value one way or
    /// the other once every row is live.</summary>
    [Fact]
    public void EveryRowStepsLabelsAndDetails()
    {
        Assert.Equal(19, OptionsChoices.Count);
        for (int row = 0; row < OptionsChoices.Count; row++)
        {
            var choices = Enhanced();
            string before = choices.Label(row);
            Assert.Contains(": ", before, System.StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(choices.Detail(row)));
            choices.Step(row, 1);
            bool movedUp = choices.Label(row) != before;
            choices = Enhanced();
            choices.Step(row, -1);
            Assert.True(movedUp || choices.Label(row) != before, $"row {row} ({before}) steps neither way");
        }
    }

    /// <summary>Under Original the view distance, shadow and water rows take no step and say why,
    /// each keeping its word for a later flip to Enhanced.</summary>
    [Theory]
    [InlineData(6)]
    [InlineData(13)]
    [InlineData(14)]
    public void TheEnhancedOnlyRowsStandDeadUnderOriginal(int row)
    {
        var choices = new OptionsChoices();
        choices.Load(null);
        string before = choices.Label(row);
        choices.Step(row, 1);
        choices.Step(row, -1);
        Assert.Equal(before, choices.Label(row));
        Assert.StartsWith("Enhanced Graphics only", choices.Detail(row), System.StringComparison.Ordinal);
        Assert.Null(choices.ToExit().ViewDistance);
        Assert.Null(choices.ToExit().ShadowQuality);
        Assert.Null(choices.ToExit().WaterQuality);
    }

    /// <summary>Borderless owns the size: the row reads the screen's own size, takes no step, says
    /// why, and the saved size rides the exit unchanged.</summary>
    [Fact]
    public void BorderlessPinsTheSize()
    {
        var choices = new OptionsChoices();
        choices.Load(new OptionsDef { DisplayMode = DisplayWords.Borderless, Resolution = "1024x768" });
        Assert.True(choices.ResolutionPinned);
        Assert.Equal($"Resolution: {ResolutionSetting.Unknown.Fallback}", choices.Label(8));
        choices.Step(8, 1);
        Assert.Equal("1024x768", choices.ToExit().Resolution);
        Assert.StartsWith("Borderless", choices.Detail(8), System.StringComparison.Ordinal);
    }

    /// <summary>Stepping onto FSR 2.2 pulls a scale above native down to native and narrows the scale
    /// list; a scale at or below native stands.</summary>
    [Fact]
    public void FsrTwoClampsTheRenderScale()
    {
        var choices = new OptionsChoices();
        choices.Load(new OptionsDef { RenderScale = "200", AntiAliasing = DisplayWords.AntiAliasingOff });
        choices.Step(12, -1);
        Assert.Equal(DisplayWords.AntiAliasingFsr2, choices.AntiAliasing);
        Assert.Equal("100", choices.RenderScale);
        Assert.Equal(4, choices.RenderScaleWords.Count);
        choices.Load(new OptionsDef { RenderScale = "67" });
        choices.PickAntiAliasing(DisplayWords.AntiAliasingFsr2);
        Assert.Equal("67", choices.RenderScale);
    }

    /// <summary>A volume step clamps at the ends, and one that moves nothing leaves a never-set level
    /// never set.</summary>
    [Fact]
    public void AVolumeStepClampsAndKeepsNeverSet()
    {
        var choices = new OptionsChoices();
        choices.Load(new OptionsDef { AudioVoice = AudioMix.MinLevel });
        choices.Step(18, -1);
        Assert.Equal(AudioMix.MinLevel, choices.AudioVoice);
        choices.Load(new OptionsDef { AudioMaster = AudioMix.MaxLevel });
        choices.Step(15, 1);
        Assert.Equal(AudioMix.MaxLevel, choices.AudioMaster);
        choices.Load(null);
        for (int i = 0; i <= AudioMix.MaxLevel; i++)
        {
            choices.Step(15, AudioMix.DefaultMaster == AudioMix.MaxLevel ? 1 : -1);
        }

        Assert.Equal(AudioMix.DefaultMaster == AudioMix.MaxLevel ? null : AudioMix.MinLevel, choices.AudioMaster);
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
            RenderScale = "67",
            AntiAliasing = DisplayWords.AntiAliasingSmaa,
            ShadowQuality = ShadowQualitySetting.Medium,
            AudioMaster = 10,
            AudioMusic = 20,
            AudioEffects = 30,
            AudioVoice = 40,
            NearestAfterKill = true,
            Rumble = false,
            DefaultView = PilotView.Name(PilotViewMode.Cockpit),
            AutoHeadTurn = true,
            ViewDistance = ViewDistance.Words[^1],
            WaterQuality = WaterQualitySetting.Words[^1],
        };
        var choices = new OptionsChoices();
        choices.Load(saved);
        Assert.Equal(
            new OptionsApplyExit(saved.GraphicsMode, saved.Difficulty, saved.MonitorIndex, saved.Resolution,
                saved.DisplayMode, saved.VSync, saved.RenderScale, saved.AntiAliasing, saved.ShadowQuality,
                saved.AudioMaster, saved.AudioMusic, saved.AudioEffects, saved.AudioVoice, saved.NearestAfterKill,
                saved.Rumble, saved.DefaultView, saved.AutoHeadTurn, saved.ViewDistance, saved.WaterQuality),
            choices.ToExit());

        choices.Load(null);
        Assert.Equal(
            new OptionsApplyExit(GraphicsMode.Default, Difficulty.Word(Difficulty.Normal), null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, null, null),
            choices.ToExit());
    }

    // Choices with every row live: Enhanced, windowed, and two screens to step between.
    private static OptionsChoices Enhanced()
    {
        var choices = new OptionsChoices(() => ResolutionSetting.Unknown,
            () => new ScreenList(new[] { "Screen 0", "Screen 1" }, 0));
        choices.Load(new OptionsDef { GraphicsMode = GraphicsMode.EnhancedWord, DisplayMode = DisplayWords.Windowed });
        return choices;
    }
}
