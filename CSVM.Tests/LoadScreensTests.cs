using System.IO;
using System.Linq;
using CSVM;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The mission load screen off engine: which dialog a mission type reads, the four texts that
/// dialog places and where, and what a mode of ours takes instead. The authored side is
/// <c>Loading.zrd</c> and <c>messages.json</c>, so these read the extraction rather than restating
/// it (docs/org/loading-screen.md).
/// </summary>
public class LoadScreensTests
{
    /// <summary>The mission-type letter that completes the dialog name, the exe's own five-entry
    /// table. Free flight and dogfight are ours and resolve to none.</summary>
    [Fact]
    public void EachMissionTypeNamesItsOwnDialogAndOurModesNameNone()
    {
        Assert.Equal('a', LoadScreens.LetterFor("dogfight_ace"));
        Assert.Equal('d', LoadScreens.LetterFor("dogfight_squadron"));
        Assert.Equal('s', LoadScreens.LetterFor("stunt_flying"));
        Assert.Equal('z', LoadScreens.LetterFor("zeppelin_run"));
        Assert.Null(LoadScreens.LetterFor(null));
        Assert.Null(LoadScreens.LetterFor("free_flight"));
    }

    /// <summary>A mode of ours takes the heading alone at the mode heading's authored place: no
    /// shipped dialog describes free flight or a dogfight, and a borrowed blurb would state a win
    /// condition neither has.</summary>
    [Fact]
    public void AModeOfOursWritesItsHeadingAloneAtTheAuthoredPlace()
    {
        var lines = LoadScreens.Texts(null, "FREE FLIGHT", "no-zrdr", "no-messages");

        var only = Assert.Single(lines);
        Assert.Equal("FREE FLIGHT", only.Text);
        Assert.Equal((70f, 35f), (only.X, only.Y));
        Assert.Equal(0f, only.Width);
        Assert.False(only.Bold);
    }

    /// <summary>The stunt screen, the one the reference shot names: four texts at the dialog's own
    /// positions, the blurb and the win condition in the body face, and the mission type in the
    /// dialog's empty default face, which is the one weight the board draws twice.</summary>
    [ExtractedDataFact]
    public void TheStuntDialogPlacesItsFourTextsAtTheirAuthoredPositions()
    {
        var lines = Texts("stunt_flying");

        Assert.Equal(4, lines.Count);
        Assert.Equal(
            new[] { (70f, 35f), (325f, 35f), (360f, 135f), (360f, 215f) },
            lines.Select(l => (l.X, l.Y)));
        Assert.Equal(new[] { 0f, 400f, 400f, 0f }, lines.Select(l => l.Width));
        Assert.Equal("INSTANT ACTION", lines[0].Text);
        Assert.Equal("STUNT FLYING", lines[1].Text);
        Assert.StartsWith("Any knucklehead", lines[2].Text);
        Assert.Equal("Fly through all the Danger Zones to win!", lines[3].Text);
        Assert.Equal(new[] { false, true, false, false }, lines.Select(l => l.Bold));
        Assert.Equal(new[] { BoardInk.Heading, BoardInk.Heading, BoardInk.Row, BoardInk.Row },
            lines.Select(l => l.Ink));
    }

    /// <summary>The other three families write the same four widgets, and the ace duel's heading
    /// sits at its own 365 rather than the 325 every other family authors.</summary>
    [ExtractedDataFact]
    public void EveryFamilyWritesItsOwnHeadingBlurbAndWinCondition()
    {
        var squadron = Texts("dogfight_squadron");
        var zeppelin = Texts("zeppelin_run");
        var ace = Texts("dogfight_ace");

        Assert.Equal("SQUADRON", squadron[1].Text);
        Assert.Equal("Destroy all enemy fighters to win!", squadron[3].Text);
        Assert.Equal("ZEPPELIN RUN", zeppelin[1].Text);
        Assert.Equal("Destroy the zeppelin's engines to win!", zeppelin[3].Text);
        Assert.Equal("DOGFIGHT AN ACE.", ace[1].Text);
        Assert.Equal(365f, ace[1].X);
        Assert.Equal(325f, squadron[1].X);
        Assert.All(new[] { squadron, zeppelin, ace }, f => Assert.Equal("INSTANT ACTION", f[0].Text));
        Assert.All(new[] { squadron, zeppelin, ace }, f => Assert.False(f[1].Bold));
    }

    /// <summary>The blackboard carries the dialog's texts and the campaign sheet does not: the
    /// campaign screen's own content is a separate decode, and it draws no photographs.</summary>
    [ExtractedDataFact]
    public void TheBlackboardTakesTheDialogAndTheSheetKeepsItsOwnTwoLines()
    {
        var blackboard = LoadScreens.For(false, "unused", "zeppelin_run", Zrdr(), MessagesPath());
        var sheet = LoadScreens.For(true, "C1   ·   Free Flight", null, Zrdr(), MessagesPath());

        Assert.Equal(4, blackboard.Lines.Count);
        Assert.Equal(6, blackboard.Pictures.Count);
        Assert.Equal(new[] { "LOADING", "C1   ·   Free Flight" }, sheet.Lines.Select(l => l.Text));
        Assert.Equal(2, sheet.Pictures.Count);
    }

    /// <summary>An absent extraction leaves the board standing with no words rather than throwing
    /// out of a screen that exists to be shown while everything else is still loading.</summary>
    [Fact]
    public void AMissingExtractionWritesNothingRatherThanThrowing()
    {
        Assert.Empty(LoadScreens.Texts("stunt_flying", "unused", "no-zrdr", "no-messages"));
    }

    private static System.Collections.Generic.IReadOnlyList<BoardLine> Texts(string missionType) =>
        LoadScreens.Texts(missionType, "unused", Zrdr(), MessagesPath());

    private static string Zrdr() =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    private static string MessagesPath() =>
        Path.Combine(TestData.ExtractedRoot!, "messages.json");
}
