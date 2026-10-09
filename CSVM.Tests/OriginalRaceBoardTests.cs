using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original presentation's race board off engine. The tests cover the reusable race table on the
/// lobby's scores page, the frozen results sheet, and the end-of-race screen over the lobby's art.
/// The geometry is MULTIPLAYERLOBBY_STATS.SCRIPT's and the lobby's (docs/architecture/UI.md).
/// </summary>
public class OriginalRaceBoardTests
{
    private const float PageX = 314f;
    private const float PageY = 26f;

    [Fact]
    public void TheRaceTableListsPlacePilotAircraftBestGapAndRunsInRaceOrder()
    {
        var race = EndedRace();
        var rows = OriginalRaceTable.Rows(race.Standings(), race.ZoneCount);

        Assert.Equal(new[] { "1st  P2", "2nd  P1", "3rd  P3" }, rows.Select(r => r.Pilot));
        Assert.Equal(new[] { "Kestrel", "Bloodhawk", "Hoplite" }, rows.Select(r => r.Aircraft));
        Assert.Equal(new[] { "0:08.0", "0:09.5", "1/3 ZONES" }, rows.Select(r => r.Best));
        Assert.Equal(new[] { "", "+1.5", "at 0:04.0" }, rows.Select(r => r.Gap));
        Assert.Equal(new[] { "1/1", "1/2", "0/1" }, rows.Select(r => r.Runs));
    }

    [Fact]
    public void TheRaceTableStandsOnTheScoresPageAtTheScriptsColumnsAndRows()
    {
        var race = EndedRace();
        var layers = new BoardLayers();
        OriginalRaceTable.Compose(OriginalRaceTable.Rows(race.Standings(), race.ZoneCount), PageX, PageY, UiStrings.Empty, layers);

        var page = Assert.Single(layers.Backdrop);
        Assert.Equal((OriginalRaceTable.PageArt, PageX, PageY), (page.Art.Name, page.X, page.Y));

        // Pilot left and Aircraft right in the name column's header box, then the next three
        // boxes centred; the fifth stays empty.
        Assert.Equal(new[] { "Pilot", "Aircraft", "Best", "Gap", "Runs" }, layers.Lines.Take(5).Select(l => l.Text));
        Assert.Equal(new[] { 335f, 335f, 493f, 556f, 617f }, layers.Lines.Take(5).Select(l => l.X));
        Assert.Equal(new[] { 69f, 69f, 70f, 70f, 70f }, layers.Lines.Take(5).Select(l => l.Y));
        Assert.Equal(new[] { BoardJustify.Left, BoardJustify.Right, BoardJustify.Center, BoardJustify.Center, BoardJustify.Center },
            layers.Lines.Take(5).Select(l => l.Justify));

        // Each row from the row corner (+24, +69), 20 apart. The name column carries place and
        // pilot at its left and the aircraft at its right, then come the next three cells.
        var second = layers.Lines.Skip(5 + 5).Take(5).ToList();
        Assert.Equal(new[] { "2nd  P1", "Bloodhawk", "0:09.5", "+1.5", "1/2" }, second.Select(l => l.Text));
        Assert.Equal(new[] { 338f, 338f, 492f, 554f, 615f }, second.Select(l => l.X));
        Assert.Equal(new[] { 154f, 150f, 62f, 61f, 60f }, second.Select(l => l.Width));
        Assert.Equal(new[] { BoardJustify.Left, BoardJustify.Right, BoardJustify.Center, BoardJustify.Center, BoardJustify.Center },
            second.Select(l => l.Justify));
        Assert.All(second, l => Assert.Equal(PageY + 69f + 20f, l.Y));
        Assert.Equal(5 + (3 * 5), layers.Lines.Count);
    }

    [Fact]
    public void TheRaceTableShowsTheScoresPagesTenRowsAndNoMore()
    {
        var rows = Enumerable.Range(0, 12).Select(i => new RaceTableRow($"P{i}", "Fury", "0:10.0", "", "1/1")).ToList();
        var layers = new BoardLayers();
        OriginalRaceTable.Compose(rows, 0f, 0f, UiStrings.Empty, layers);

        Assert.Equal(5 + (OriginalRaceTable.VisibleRows * 5), layers.Lines.Count);
        Assert.Equal("P9", layers.Lines[^5].Text);
        Assert.Equal(69f + (9 * 20f), layers.Lines[^5].Y);
    }

    [Fact]
    public void SixteenRacersScrollTheScoresPageToTheSixteenthRowUnderTheScriptsScrollBar()
    {
        var race = FieldOf(16);
        var rows = OriginalRaceTable.Rows(race.Standings(), race.ZoneCount);
        var bar = OriginalRaceTable.ScrollBar(PageX, PageY);
        Assert.Equal((PageX + 419f, PageY + 66f, 195f, 10), (bar.X, bar.Y, bar.Height, bar.Rows));

        var layers = new BoardLayers();
        int top = bar.Clamp(99, rows.Count);
        OriginalRaceTable.Compose(rows, PageX, PageY, UiStrings.Empty, layers, top);

        Assert.Equal(6, top);
        Assert.Equal("7th  P7", layers.Lines[5].Text);
        Assert.Equal("16th  P16", layers.Lines[^5].Text);
        Assert.Equal(PageY + 69f + (9 * 20f), layers.Lines[^5].Y);

        // The thumb sits flush at the foot of the track between the arrows; KF is clear, so no fill.
        var thumb = Assert.Single(layers.Pictures, p => p.Art.Name == "MP_B_SCROLLBAR.PNG");
        Assert.Equal(PageX + 419f, thumb.X);
        Assert.Equal(PageY + 66f + 195f - 11f, thumb.Y + thumb.Height, 3);
        Assert.Empty(layers.Fills);
    }

    [Fact]
    public void ATenPilotFieldFillsTheScoresPageWithNoScrollBar()
    {
        // ABLE-TO-FAIL CONTROL: ten rows fit the page, so the scripts keep the control deactivated.
        var race = FieldOf(10);
        var layers = new BoardLayers();
        OriginalRaceTable.Compose(OriginalRaceTable.Rows(race.Standings(), race.ZoneCount), PageX, PageY, UiStrings.Empty, layers, 3);

        Assert.DoesNotContain(layers.Pictures, p => p.Art.Name == "MP_B_SCROLLBAR.PNG");
        Assert.Equal("1st  P1", layers.Lines[5].Text);
        var sheet = RaceResultsSheet.Of(race, new[] { "a" }, "", "Back");
        Assert.DoesNotContain(OriginalRaceResults.ScoresUpRow, OriginalRaceResults.Slots(sheet));
    }

    [Fact]
    public void TheRaceBoardsArrowsScrollTheStandingsAndTheSplitsToTheSixteenthPilot()
    {
        var sheet = RaceResultsSheet.Of(FieldOf(16), new[] { "a" }, "", "Back");
        var slots = OriginalRaceResults.Slots(sheet);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, slots);

        // Each arrow is a menu row under the pointer at its bar's corner or foot.
        var (ux, uy, _, _) = OriginalRaceResults.SlotRect(OriginalRaceResults.ScoresUpRow);
        Assert.Equal((314f + 419f, 26f + 66f), (ux, uy));
        var (sx, sy, _, _) = OriginalRaceResults.SlotRect(OriginalRaceResults.SplitsDownRow);
        Assert.Equal((757f, 368f + 175f - 11f), (sx, sy));
        Assert.Equal(6, OriginalRaceResults.MenuRowAt(sheet, sx + 1f, sy + 1f));

        // Down on each list until it stops: the scores at 6, the splits at 9.
        (int scores, int splits) = (0, 0);
        for (int i = 0; i < 20; i++)
        {
            (scores, splits) = OriginalRaceResults.Scrolled(sheet, OriginalRaceResults.ScoresDownRow, scores, splits);
            (scores, splits) = OriginalRaceResults.Scrolled(sheet, OriginalRaceResults.SplitsDownRow, scores, splits);
        }

        Assert.Equal((6, 9), (scores, splits));
        var board = OriginalRaceResults.Compose(sheet, UiStrings.Empty, 0, false, scores, splits);
        Assert.Contains(board.Lines, l => l.Text == "16th  P16" && l.Y == 26f + 69f + (9 * 20f));
        Assert.Contains(board.Lines, l => l.Text == "P16" && l.X == 38f && l.Y == 373f + (7 * 20f));
        Assert.DoesNotContain(board.Lines, l => l.Text == "P9" && l.X == 38f);

        // The splits' track takes the chat control's KF, and both thumbs stand.
        Assert.Contains(board.Fills, f => f.X == 757f && (f.R, f.G, f.B) == (0x20, 0x24, 0x18));
        Assert.Equal(2, board.Pictures.Count(p => p.Art.Name == "MP_B_SCROLLBAR.PNG"));

        // At the foot the down arrows draw disabled and the up arrows live.
        var arrows = board.Pictures.Where(p => p.Art.Name is "MP_B_SCROLLUP.PNG" or "MP_B_SCROLLDOWN.PNG").ToList();
        Assert.Equal(new[] { 1, 0, 1, 0 }, arrows.Select(p => p.Frame));
    }

    [Fact]
    public void SevenPilotsFitTheSplitsAndEightScrollThem()
    {
        var seven = RaceResultsSheet.Of(FieldOf(7), new[] { "a" }, "", "Back");
        Assert.Equal(new[] { 0, 1, 2 }, OriginalRaceResults.Slots(seven));

        var eight = RaceResultsSheet.Of(FieldOf(8), new[] { "a" }, "", "Back");
        Assert.Equal(new[] { 0, 1, 2, 5, 6 }, OriginalRaceResults.Slots(eight));
    }

    [Fact]
    public void TheSheetFreezesEachPilotsBestRunSplitsInRaceOrder()
    {
        var race = EndedRace();
        var sheet = RaceResultsSheet.Of(race, new[] { "Pier", "", "Tower" }, "C1   ·   Stunt Flying", "Back");

        Assert.Equal(new[] { "Pier", "Zone 2", "Tower" }, sheet.ZoneNames);
        Assert.Equal(new[] { "P2", "P1", "P3" }, sheet.Splits.Select(s => s.Pilot));
        Assert.Equal(new[] { "0:02.0", "0:05.0", "0:08.0" }, sheet.Splits[0].Cells);
        Assert.Equal(new[] { "0:03.0", "0:06.0", "0:09.5" }, sheet.Splits[1].Cells);
        Assert.Equal(new[] { "-", "-", "0:04.0" }, sheet.Splits[2].Cells);

        race.Rerun();
        Assert.Equal("0:08.0", sheet.Standings[0].Best);
    }

    [Fact]
    public void TheScreenIsTheLobbyOnGameScoresWithTheRaceInIt()
    {
        var sheet = RaceResultsSheet.Of(EndedRace(), new[] { "Pier", "Bridge", "Tower" }, "C1   ·   Stunt Flying", "Back");
        var board = OriginalRaceResults.Compose(sheet, UiStrings.Empty, OriginalRaceResults.PhotoRow, pressed: false);

        Assert.Equal(new[] { "MP_LOBBY_BACKGROUND.JPG", OriginalRaceTable.PageArt }, board.Backdrop.Select(p => p.Art.Name));
        Assert.Contains(board.Lines, l => l.Text == OriginalRaceResults.Title && l.X == 60f && l.Y == 22f);
        Assert.Contains(board.Lines, l => l.Text == "Game Scores" && l.X == 656f);
        Assert.Contains(board.Lines, l => l.Text == "C1   ·   Stunt Flying" && l.X == 92f);

        // The zone key down the player list from (34, 83) at the list's 20-pixel pitch.
        Assert.Contains(board.Lines, l => l.Text == OriginalRaceResults.ZoneHeading && l.X == 34f && l.Y == 54f);
        Assert.Contains(board.Lines, l => l.Text == "3  Tower" && l.X == 36f && l.Y == 123f);

        // The splits in the chat pane: the zone numbers on its first line, a pilot per line under it.
        var header = board.Lines.Where(l => l.Y == 373f && l.X >= 134f).ToList();
        Assert.Equal(new[] { "1", "2", "3" }, header.Select(l => l.Text));
        Assert.Equal(new[] { 134f, 196f, 258f }, header.Select(l => l.X));
        var p1 = board.Lines.Where(l => l.Y == 413f && l.X >= 34f && l.X < 800f && l.Text != "").Select(l => l.Text).ToList();
        Assert.Equal(new[] { "P1", "0:03.0", "0:06.0", "0:09.5" }, p1);
    }

    [Fact]
    public void ALongCourseTakesTwoKeyColumnsAndNarrowerSplitColumns()
    {
        var race = new StuntRace(60f, 17);
        race.Add(0, "Fury");
        race.BeginOpening(0f);
        race.RunStarted(0);
        race.ZoneCleared(0, 16, 4f);
        race.Advance(61f);
        var names = Enumerable.Range(1, 17).Select(i => $"Z{i}").ToList();
        var board = OriginalRaceResults.Compose(RaceResultsSheet.Of(race, names, "", "Back"), UiStrings.Empty, 0, false);

        Assert.Contains(board.Lines, l => l.Text == "9  Z9" && l.X == 36f && l.Y == 83f + (8 * 20f));
        Assert.Contains(board.Lines, l => l.Text == "10  Z10" && l.X == 36f + 138f && l.Y == 83f);
        Assert.Contains(board.Lines, l => l.Text == "17  Z17" && l.Y == 83f + (7 * 20f));
        var header = board.Lines.Where(l => l.Y == 373f && l.X >= 134f).ToList();
        Assert.Equal(17, header.Count);
        Assert.Equal(625f / 17f, header[0].Width, 3);
        Assert.Contains(board.Lines, l => l.Text == "0:04.0" && l.Y == 393f && System.Math.Abs(l.X - (134f + (16 * 625f / 17f))) < 0.01f);
    }

    [Fact]
    public void ThePlaquesAreTheLobbysCreateTeamSendAndLeaveGameSlotsInTheirStates()
    {
        var sheet = RaceResultsSheet.Of(EndedRace(), new[] { "a", "b", "c" }, "", "Back");
        var board = OriginalRaceResults.Compose(sheet, UiStrings.Empty, OriginalRaceResults.RestartRow, pressed: true);

        var plaques = board.Pictures.Where(p => p.Art.Frames == 4).ToList();
        Assert.Equal(new[] { "MP_B_LARGE.PNG", "MP_B_SMALL.PNG", "MP_B_LARGE.PNG" }, plaques.Select(p => p.Art.Name));
        Assert.Equal(new[] { (105f, 325f), (577f, 548f), (655f, 548f) }, plaques.Select(p => (p.X, p.Y)));
        Assert.Equal(new[] { 1, 3, 1 }, plaques.Select(p => p.Frame));

        var labels = board.Lines.Where(l => l.Text is OriginalRaceResults.PhotoLabel or OriginalRaceResults.RestartLabel or "Back").ToList();
        Assert.Equal(new[] { "Photo Mode", "Restart", "Back" }, labels.Select(l => l.Text));
        Assert.Equal(MultiplayerBoardText.LabelPressed, labels[1].Colour);
        Assert.Equal(MultiplayerBoardText.LabelNormal, labels[2].Colour);

        var focused = OriginalRaceResults.Compose(sheet, UiStrings.Empty, OriginalRaceResults.ExitRow, pressed: false);
        Assert.Equal(new[] { 1, 1, 2 }, focused.Pictures.Where(p => p.Art.Frames == 4).Select(p => p.Frame));
    }

    [Fact]
    public void ThePointerFindsEachPlaqueAndNothingBetweenThem()
    {
        for (int row = 0; row < 3; row++)
        {
            var (x, y, w, h) = OriginalRaceResults.SlotRect(row);
            Assert.Equal(row, OriginalRaceResults.RowAt(x + 1f, y + 1f));
            Assert.Equal(row, OriginalRaceResults.RowAt(x + w - 1f, y + h - 1f));
            Assert.Equal(-1, OriginalRaceResults.RowAt(x + w + 1f, y + 1f));
        }

        Assert.Equal(-1, OriginalRaceResults.RowAt(400f, 300f));
    }

    [Fact]
    public void APilotWhoLeftKeepsTheirPlaceInTheScoresPagesGreyRow()
    {
        var race = EndedRace(leaves: 0);
        var rows = OriginalRaceTable.Rows(race.Standings(), race.ZoneCount);
        Assert.Equal(new[] { "1st  P2", "2nd  P1", "3rd  P3" }, rows.Select(r => r.Pilot));
        Assert.Equal(new[] { false, true, false }, rows.Select(r => r.Left));

        var layers = new BoardLayers();
        OriginalRaceTable.Compose(rows, PageX, PageY, UiStrings.Empty, layers);
        var grey = new BoardTint(0xbb, 0xbb, 0xbb);
        Assert.All(layers.Lines.Skip(5 + 5).Take(5), l => Assert.Equal(grey, l.Colour));

        // ABLE-TO-FAIL CONTROL: the headers and every other row keep the script's black.
        Assert.All(layers.Lines.Take(5 + 5).Concat(layers.Lines.Skip(5 + 10)), l => Assert.Equal(new BoardTint(0, 0, 0), l.Colour));

        var sheet = RaceResultsSheet.Of(race, new[] { "a", "b", "c" }, "", "Lobby");
        Assert.Equal(new[] { false, true, false }, sheet.Splits.Select(s => s.Left));
        var board = OriginalRaceResults.Compose(sheet, UiStrings.Empty, 0, false);
        Assert.All(board.Lines.Where(l => l.Y == 413f && l.X >= 34f && l.Text.Length > 0 && l.X < 800f), l => Assert.Equal(grey, l.Colour));
    }

    [Fact]
    public void AGuestsBoardLeavesTheRestartPlaqueEmptyAndSaysItWaitsForTheHost()
    {
        var sheet = RaceResultsSheet.Of(EndedRace(), new[] { "a", "b", "c" }, "C1", "Leave", "Waiting for the host");
        var board = OriginalRaceResults.Compose(sheet, UiStrings.Empty, 1, pressed: false);

        var plaques = board.Pictures.Where(p => p.Art.Frames == 4).ToList();
        Assert.Equal(new[] { (105f, 325f), (655f, 548f) }, plaques.Select(p => (p.X, p.Y)));
        Assert.Equal(new[] { 1, 2 }, plaques.Select(p => p.Frame));
        Assert.DoesNotContain(board.Lines, l => l.Text == OriginalRaceResults.RestartLabel);
        Assert.Contains(board.Lines, l => l.Text == "Leave" && l.X == 655f);
        Assert.Contains(board.Lines, l => l.Text == "C1   ·   Waiting for the host" && l.X == 92f);

        // The pointer reaches the exit on its own plaque, and the empty Restart slot is no row.
        var (sx, sy, _, _) = OriginalRaceResults.SlotRect(OriginalRaceResults.RestartRow);
        var (ex, ey, _, _) = OriginalRaceResults.SlotRect(OriginalRaceResults.ExitRow);
        Assert.Equal(-1, OriginalRaceResults.MenuRowAt(sheet, sx + 1f, sy + 1f));
        Assert.Equal(1, OriginalRaceResults.MenuRowAt(sheet, ex + 1f, ey + 1f));

        // ABLE-TO-FAIL CONTROL: the host's sheet keeps all three, its exit on menu row 2.
        var host = RaceResultsSheet.Of(EndedRace(), new[] { "a", "b", "c" }, "C1", "Lobby");
        Assert.Equal(new[] { 0, 1, 2 }, OriginalRaceResults.Slots(host));
        Assert.Equal(2, OriginalRaceResults.MenuRowAt(host, ex + 1f, ey + 1f));
    }
    // Three pilots over three zones: P2 fastest, P1 second over two runs, P3 one zone and no finish.
    private static StuntRace EndedRace(int leaves = -1)
    {
        var race = new StuntRace(60f, 3);
        race.Add(0, "Bloodhawk");
        race.Add(1, "Kestrel");
        race.Add(2, "Hoplite");
        race.BeginOpening(0f);
        FlyRun(race, 0, new[] { 3f, 6f, 9.5f });
        race.RunStarted(0);
        race.RunAbandoned(0);
        FlyRun(race, 1, new[] { 2f, 5f, 8f });
        race.RunStarted(2);
        race.ZoneCleared(2, 2, 4f);
        race.RunAbandoned(2);
        if (leaves >= 0)
        {
            Assert.True(race.MarkLeft(leaves));
        }

        race.Advance(61f);
        Assert.True(race.Ended);
        return race;
    }

    // A one-zone field of `count` pilots finishing in seat order, P1 fastest.
    private static StuntRace FieldOf(int count)
    {
        var race = new StuntRace(60f, 1);
        for (int i = 0; i < count; i++)
        {
            race.Add(i, "Fury");
        }

        race.BeginOpening(0f);
        for (int i = 0; i < count; i++)
        {
            FlyRun(race, i, new[] { 1f + i });
        }

        race.Advance(61f);
        Assert.True(race.Ended);
        return race;
    }

    private static void FlyRun(StuntRace race, int index, IReadOnlyList<float> at)
    {
        Assert.True(race.RunStarted(index));
        for (int zone = 0; zone < at.Count; zone++)
        {
            race.ZoneCleared(index, zone, at[zone]);
        }

        race.RunFinished(index, at[^1]);
    }
}
