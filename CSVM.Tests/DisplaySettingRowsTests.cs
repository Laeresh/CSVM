using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The row rules the two Options screens share for the four display settings: a label per
/// vocabulary entry, the two forgiving reads (an unknown word reads as the setting's own default,
/// a size the screen does not offer reads as the screen's own size) and the wrap a sideways step
/// takes. Engine-free, so the multi-screen and other-screen cases this machine cannot show are
/// proved by handing the rules a list.</summary>
public class DisplaySettingRowsTests
{
    /// <summary>One label per store word, in that order: a row reads and writes by index, so a
    /// vocabulary widened without a label would index past the end of the labels.</summary>
    [Fact]
    public void EveryDisplayWordHasExactlyOneLabel()
    {
        Assert.Equal(DisplayWords.DisplayModes.Count, DisplaySettingRows.DisplayModeLabels.Count);
        Assert.Equal(DisplayWords.VSyncChoices.Count, DisplaySettingRows.VSyncLabels.Count);
    }

    [Theory]
    [InlineData(DisplayWords.Windowed, 0)]
    [InlineData(DisplayWords.Borderless, 1)]
    [InlineData(DisplayWords.Fullscreen, 2)]
    public void ASavedDisplayModeReadsAsItsOwnPosition(string word, int expected) =>
        Assert.Equal(expected, DisplaySettingRows.WordIndex(DisplayWords.DisplayModes, word, DisplayModeSetting.Default));

    /// <summary>A word the vocabulary does not know, and no saved word at all, both read as the
    /// setting's own default, the behaviour with no options file, which is not the vocabulary's
    /// first value for either display setting.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("maximised")]
    public void AnUnknownDisplayModeReadsAsTheDefault(string? word)
    {
        int at = DisplaySettingRows.WordIndex(DisplayWords.DisplayModes, word, DisplayModeSetting.Default);
        Assert.Equal(DisplayModeSetting.Default, DisplayWords.DisplayModes[at]);
        Assert.NotEqual(0, at);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("240")]
    public void AnUnknownVSyncChoiceReadsAsTheDefault(string? word)
    {
        int at = DisplaySettingRows.WordIndex(DisplayWords.VSyncChoices, word, VSyncSetting.Default);
        Assert.Equal(VSyncSetting.Default, DisplayWords.VSyncChoices[at]);
        Assert.NotEqual(0, at);
    }

    [Fact]
    public void ASavedSizeTheScreenOffersReadsAsItsOwnPosition()
    {
        var offered = ResolutionSetting.SizesUnder(1920, 1080);

        Assert.Equal(offered.Words.Count - 1, DisplaySettingRows.ResolutionIndex(offered, "1920x1080"));
        Assert.Equal("1920x1080", offered.Words[DisplaySettingRows.ResolutionIndex(offered, "1920x1080")]);
    }

    /// <summary>A size the screen no longer offers reads as the list's own fallback, the screen's
    /// size, never as the first size in the list, which is the smallest the monitor holds rather
    /// than the size a launch with no options file runs at. The row has to agree with
    /// `ResolutionSetting.Resolve`'s fallback or it would name a size the window is not standing at.</summary>
    [Theory]
    [InlineData("3840x2160")]
    [InlineData(null)]
    [InlineData("800x600")]
    public void ASizeTheScreenDoesNotOfferReadsAsTheScreensOwnSize(string? saved)
    {
        var offered = ResolutionSetting.SizesUnder(1600, 900);

        Assert.DoesNotContain(saved ?? "", offered.Words);
        Assert.Equal(offered.Fallback, offered.Words[DisplaySettingRows.ResolutionIndex(offered, saved)]);
        Assert.Equal("1600x900", offered.Fallback);
    }

    /// <summary>The resolution row's words are enumerated from a screen rather than shipped as a
    /// list, so the same saved size reads as itself on the screen that holds it and as that screen's
    /// own size on the one that does not. This is what a monitor step has to re-enumerate.</summary>
    [Fact]
    public void TheSameSavedSizeReadsDifferentlyOnTwoScreens()
    {
        var wide = ResolutionSetting.SizesUnder(3840, 2160);
        var small = ResolutionSetting.SizesUnder(1280, 800);

        Assert.Equal("2560x1440", wide.Words[DisplaySettingRows.ResolutionIndex(wide, "2560x1440")]);
        Assert.Equal(small.Fallback, small.Words[DisplaySettingRows.ResolutionIndex(small, "2560x1440")]);
    }

    /// <summary>A monitor index no screen answers to reads as the screen the window already stands
    /// on, so a row over a machine that lost a monitor names the screen the apply would leave the
    /// window on. Proved over a fabricated list, this machine having the screens it has.</summary>
    [Fact]
    public void AMonitorIndexNoScreenAnswersToReadsAsTheStandingScreen()
    {
        var screens = new ScreenList(new[] { "Screen 0", "Screen 1", "Screen 2" }, 1);

        Assert.Equal(2, MonitorSetting.Resolve("2", screens).Screen);
        Assert.Equal(1, MonitorSetting.Resolve("7", screens).Screen);
        Assert.Equal(1, MonitorSetting.Resolve(null, screens).Screen);
        Assert.Equal(1, MonitorSetting.Resolve("primary", screens).Screen);
    }

    /// <summary>A step wraps in both directions over any row's own count, since every display row on
    /// either screen is a stepper with no end.</summary>
    [Theory]
    [InlineData(0, 1, 3, 1)]
    [InlineData(2, 1, 3, 0)]
    [InlineData(0, -1, 3, 2)]
    [InlineData(0, 1, 1, 0)]
    [InlineData(0, -1, 1, 0)]
    [InlineData(0, 1, 0, 0)]
    public void AStepWrapsInBothDirections(int index, int direction, int count, int expected) =>
        Assert.Equal(expected, DisplaySettingRows.Step(index, direction, count));

    /// <summary>Stepping every value of a vocabulary returns to where it started, which is what lets
    /// a row be walked without a cursor of its own.</summary>
    [Fact]
    public void SteppingAWholeVocabularyReturnsToTheStart()
    {
        int at = 0;
        for (int i = 0; i < DisplayWords.VSyncChoices.Count; i++)
        {
            at = DisplaySettingRows.Step(at, 1, DisplayWords.VSyncChoices.Count);
        }

        Assert.Equal(0, at);
    }
}
