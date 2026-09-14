using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The row rules the two Options screens share for the four display settings: a label per
/// vocabulary entry, the forgiving reads (an unknown word reads as the setting's own default, a
/// size the screen does not offer reads as the screen's own size, a size row borderless owns reads
/// the screen's own whatever is saved), the size the options file names standing in the picker as
/// an entry of its own, and the wrap a sideways step takes. Engine-free, so the
/// multi-screen and other-screen cases this machine cannot show are proved by handing the rules a
/// list.</summary>
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

        Assert.Equal(offered.Words.Count - 1, DisplaySettingRows.ResolutionIndex(offered, "1920x1080", DisplayWords.Windowed));
        Assert.Equal("1920x1080", offered.Words[DisplaySettingRows.ResolutionIndex(offered, "1920x1080", DisplayWords.Fullscreen)]);
    }

    /// <summary>A size the screen no longer offers reads as the list's own fallback, the screen's
    /// size, never as the first size in the list, which is the smallest the monitor holds rather
    /// than the size a launch with no options file runs at. The row has to agree with
    /// `ResolutionSetting.Resolve`'s fallback or it would name a size the window is not standing at.</summary>
    [Theory]
    [InlineData("3840x2160")]
    [InlineData(null)]
    [InlineData("1280x960")]
    public void ASizeTheScreenDoesNotOfferReadsAsTheScreensOwnSize(string? saved)
    {
        var offered = ResolutionSetting.SizesUnder(1600, 900);

        Assert.DoesNotContain(saved ?? "", offered.Words);
        Assert.Equal(offered.Fallback, offered.Words[DisplaySettingRows.ResolutionIndex(offered, saved, DisplayWords.Windowed)]);
        Assert.Equal("1600x900", offered.Fallback);
    }

    /// <summary>Borderless owns the size, so the row reads the screen's own whatever is saved and
    /// whatever the screen offers, and the two modes that leave the size to the player read the
    /// saved one. A mode the vocabulary does not know, and none saved, both read as borderless,
    /// which is the mode a launch with no options file runs in.</summary>
    [Theory]
    [InlineData(DisplayWords.Borderless, "1600x900")]
    [InlineData(null, "1600x900")]
    [InlineData("maximised", "1600x900")]
    [InlineData(DisplayWords.Windowed, "1280x720")]
    [InlineData(DisplayWords.Fullscreen, "1280x720")]
    public void TheDisplayModeDecidesWhetherTheSizeRowReadsTheSavedSize(string? mode, string expected)
    {
        var offered = ResolutionSetting.SizesUnder(1600, 900);

        Assert.Equal(expected, offered.Words[DisplaySettingRows.ResolutionIndex(offered, "1280x720", mode)]);
        Assert.Equal(expected == offered.Fallback, ResolutionSetting.Pinned(mode));
    }

    /// <summary>The plan the apply takes, which the row above has to agree with: the saved size
    /// under windowed and under exclusive fullscreen, the screen's own under borderless, and a
    /// source that names the mode as the layer that won rather than the file.</summary>
    [Theory]
    [InlineData(DisplayWords.Windowed, 1280, 720, "options.json")]
    [InlineData(DisplayWords.Fullscreen, 1280, 720, "options.json")]
    [InlineData(DisplayWords.Borderless, 1600, 900, DisplayWords.Borderless)]
    public void TheDisplayModeDecidesTheSizeTheApplyTakes(string mode, int width, int height, string source)
    {
        var plan = ResolutionSetting.Resolve("1280x720", ResolutionSetting.SizesUnder(1600, 900), mode);

        Assert.Equal((width, height), (plan.Width, plan.Height));
        Assert.Equal(source, plan.Source);
    }

    /// <summary>The resolution row's words are enumerated from a screen rather than shipped as a
    /// list, so the same saved size reads as itself on the screen that holds it and as that screen's
    /// own size on the one that does not. This is what a monitor step has to re-enumerate.</summary>
    [Fact]
    public void TheSameSavedSizeReadsDifferentlyOnTwoScreens()
    {
        var wide = ResolutionSetting.SizesUnder(3840, 2160);
        var small = ResolutionSetting.SizesUnder(1280, 800);

        Assert.Equal("2560x1440", wide.Words[DisplaySettingRows.ResolutionIndex(wide, "2560x1440", DisplayWords.Windowed)]);
        Assert.Equal(small.Fallback, small.Words[DisplaySettingRows.ResolutionIndex(small, "2560x1440", DisplayWords.Windowed)]);
    }

    /// <summary>The standard table carries a 4:3 ladder rather than one rung, the original game's own
    /// frame being 4:3, and every rung is offered where the screen holds it.</summary>
    [Theory]
    [InlineData("800x600")]
    [InlineData("1024x768")]
    [InlineData("1280x960")]
    [InlineData("1600x1200")]
    public void TheStandardSizesCarryTheFourByThreeLadder(string size)
    {
        Assert.Contains(size, ResolutionSetting.Unknown.Words);
        Assert.Contains(size, ResolutionSetting.SizesUnder(3840, 2160).Words);
    }

    /// <summary>The listed sizes ascend by width then height, which is the order a custom entry has to
    /// sort into.</summary>
    [Fact]
    public void TheOfferedSizesAscend()
    {
        var words = ResolutionSetting.SizesUnder(3840, 2160).Words;

        for (int i = 1; i < words.Count; i++)
        {
            Assert.True(OptionsStore.TryParseResolution(words[i - 1], out int width, out int height));
            Assert.True(OptionsStore.TryParseResolution(words[i], out int nextWidth, out int nextHeight));
            Assert.True(width < nextWidth || (width == nextWidth && height < nextHeight));
        }
    }

    /// <summary>A size written into the options file by hand stands in the picker as an entry of its
    /// own, where it sorts among the listed ones, and is what the row reads and the apply takes. The
    /// row's own word is the saved one, so a page that shows it and saves writes it back unchanged
    /// rather than replacing it with the nearest listed size.</summary>
    [Theory]
    [InlineData("640x480", 0)]
    [InlineData("1152x864", 2)]
    public void ASizeWrittenIntoTheFileStandsWhereItSorts(string custom, int at)
    {
        var screen = ResolutionSetting.SizesUnder(1920, 1080);
        var offered = screen.Including(custom);

        Assert.Equal(screen.Words.Count + 1, offered.Words.Count);
        Assert.Equal(custom, offered.Words[at]);
        Assert.Equal(at, DisplaySettingRows.ResolutionIndex(offered, custom, DisplayWords.Windowed));
        Assert.Equal(custom, offered.Words[DisplaySettingRows.ResolutionIndex(offered, custom, DisplayWords.Windowed)]);
        Assert.Equal(custom, ResolutionSetting.Resolve(custom, screen, DisplayWords.Windowed).Word);
        Assert.Equal("options.json", ResolutionSetting.Resolve(custom, screen, DisplayWords.Windowed).Source);
    }

    /// <summary>A listed size, a word that is not a size and nothing saved at all each leave the list
    /// as it stands, so the picker never doubles an entry or offers a word no window could take.</summary>
    [Theory]
    [InlineData("1024x768")]
    [InlineData("1152 x 864")]
    [InlineData("")]
    [InlineData(null)]
    public void AListedOrMalformedSizeLeavesThePickerAsItIs(string? word)
    {
        var screen = ResolutionSetting.SizesUnder(1920, 1080);

        Assert.Equal(screen.Words, screen.Including(word).Words);
    }

    /// <summary>The screen filter holds over a hand-written size too: one the screen cannot hold is
    /// not offered and falls back to the screen's own size, the same reading a listed size the screen
    /// lost gets. A window larger than the screen would put its own controls off the edge.</summary>
    [Fact]
    public void ASizeTheScreenCannotHoldIsNotOfferedHoweverItWasSaved()
    {
        var screen = ResolutionSetting.SizesUnder(1920, 1080);

        Assert.Equal(screen.Words, screen.Including("2560x1440").Words);
        Assert.Equal(screen.Fallback, screen.Words[DisplaySettingRows.ResolutionIndex(screen, "2560x1440", DisplayWords.Windowed)]);
        Assert.Equal(screen.Fallback, ResolutionSetting.Resolve("2560x1440", screen, DisplayWords.Windowed).Word);
    }

    /// <summary>The display mode decides what a hand-written size means as it decides a listed one:
    /// borderless owns the size, so the row reads the screen's own and the apply takes it, while
    /// windowed and exclusive fullscreen both take the file's.</summary>
    [Theory]
    [InlineData(DisplayWords.Windowed, "1152x864")]
    [InlineData(DisplayWords.Fullscreen, "1152x864")]
    [InlineData(DisplayWords.Borderless, "1920x1080")]
    public void TheDisplayModeDecidesWhatAHandWrittenSizeMeans(string mode, string expected)
    {
        var offered = ResolutionSetting.SizesUnder(1920, 1080).Including("1152x864");

        Assert.Equal(expected, offered.Words[DisplaySettingRows.ResolutionIndex(offered, "1152x864", mode)]);
        Assert.Equal(expected, ResolutionSetting.Resolve("1152x864", offered, mode).Word);
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
