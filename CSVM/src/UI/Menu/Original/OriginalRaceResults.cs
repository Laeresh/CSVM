using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>The three lists the Original race board scrolls: the standings on the scores page, the
/// pilots down the splits and the zones across them.</summary>
public enum RaceResultsList
{
    /// <summary>The standings, under <see cref="OriginalRaceResults.ScoresBar"/>.</summary>
    Scores,

    /// <summary>The splits' pilots, under <see cref="OriginalRaceResults.SplitsBar"/>.</summary>
    Splits,

    /// <summary>The splits' zones, under <see cref="OriginalRaceResults.ZonesBar"/>.</summary>
    Zones,
}

/// <summary>One pilot's best-run splits as drawn: the callsign and one cell per zone in course
/// order, "-" where that run never cleared the zone. A pilot who left carries
/// <paramref name="Left"/>.</summary>
public sealed record RaceSplitRow(string Pilot, IReadOnlyList<string> Cells, bool Left = false);

/// <summary>What one ended race's Original board shows, frozen at the race's end so a restart's
/// cleared field never redraws it. It holds the standings, the zone names in course order, each
/// pilot's splits in race order, the context line and the exit row's words. A network guest's sheet
/// carries its line in place of Restart as <paramref name="Withheld"/>, null where Restart stands.</summary>
public sealed record RaceResultsSheet(
    IReadOnlyList<RaceTableRow> Standings,
    IReadOnlyList<string> ZoneNames,
    IReadOnlyList<RaceSplitRow> Splits,
    string Context,
    string ExitLabel,
    string? Withheld = null)
{
    /// <summary>The sheet of an ended <paramref name="race"/>. <paramref name="zoneNames"/> names
    /// the course's zones in course order; a missing or empty name reads "Zone n".</summary>
    public static RaceResultsSheet Of(StuntRace race, IReadOnlyList<string> zoneNames, string context, string exitLabel,
        string? withheld = null)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(zoneNames);
        var standings = race.Standings();
        var names = new List<string>(race.ZoneCount);
        for (int zone = 0; zone < race.ZoneCount; zone++)
        {
            names.Add(zone < zoneNames.Count && zoneNames[zone].Length > 0 ? zoneNames[zone] : $"Zone {zone + 1}");
        }

        var splits = new List<RaceSplitRow>(standings.Count);
        foreach (var racer in standings)
        {
            var cells = new List<string>(race.ZoneCount);
            for (int zone = 0; zone < race.ZoneCount; zone++)
            {
                cells.Add(racer.Splits[zone] is { } at ? StuntMission.FormatTime(at) : "-");
            }

            splits.Add(new RaceSplitRow(racer.Callsign, cells, racer.Left));
        }

        return new RaceResultsSheet(OriginalRaceTable.Rows(standings, race.ZoneCount), names, splits, context, exitLabel, withheld);
    }
}

/// <summary>
/// The Original presentation's end-of-race screen, engine-free: a Dogfight's landing screen, the
/// Multiplayer Lobby on its Game Scores tab, with the race in it. Its background, scores
/// page, title box, player list, chat pane, chat line and plaques are the lobby's art and positions.
/// The standings fill the scores page through <see cref="OriginalRaceTable"/> and the splits the chat
/// pane, each under that list's own scroll bar. The zone key takes the player list, the context the
/// chat line. Photo Mode, Restart and the exit take the Create Team, Send and Leave Game plaques;
/// every word but the tab's is remake-only.
/// Module entry: docs/architecture/UI.md on src/UI/Menu/Original/OriginalRaceResults.cs.
/// </summary>
public static class OriginalRaceResults
{
    /// <summary>Photo Mode's row, first so a stray confirm on a board that just appeared is harmless.
    /// </summary>
    public const int PhotoRow = 0;

    /// <summary>Restart's row.</summary>
    public const int RestartRow = 1;

    /// <summary>The exit row.</summary>
    public const int ExitRow = 2;

    /// <summary>The scores page scroll bar's up arrow, a slot past the plaques. Each arrow slot is a
    /// menu row only while its list scrolls.</summary>
    public const int ScoresUpRow = 3;

    /// <summary>The scores page scroll bar's down arrow.</summary>
    public const int ScoresDownRow = 4;

    /// <summary>The chat pane scroll bar's up arrow, over the splits.</summary>
    public const int SplitsUpRow = 5;

    /// <summary>The chat pane scroll bar's down arrow.</summary>
    public const int SplitsDownRow = 6;

    /// <summary>The zone bar's left arrow, under the splits.</summary>
    public const int ZonesLeftRow = 7;

    /// <summary>The zone bar's right arrow.</summary>
    public const int ZonesRightRow = 8;

    /// <summary>The words the Restart plaque carries.</summary>
    public const string RestartLabel = "Restart";

    /// <summary>The words the Photo Mode plaque carries.</summary>
    public const string PhotoLabel = "Photo Mode";

    /// <summary>The title in the lobby's title box, where the lobby writes its own name.</summary>
    public const string Title = "STUNT RACE RESULTS";

    /// <summary>The zone key's heading, where the lobby counts its players.</summary>
    public const string ZoneHeading = "Danger Zones";

    /// <summary>The zone key's rows per column, the lobby's eleven visible players.</summary>
    public const int ZoneRows = 11;

    /// <summary>The splits' rows under their heading line, as many 20-pixel rows as the chat pane
    /// holds after it; a larger field scrolls under <see cref="SplitsBar"/>.</summary>
    public const int SplitRows = 7;

    /// <summary>The split column's width, a remake-only width: the width of the scores page's
    /// Best column (<see cref="OriginalRaceTable"/>'s first figure cell). Every time under an hour
    /// fits it. The width is fixed, so a course past <see cref="ZoneColumns"/> zones scrolls even where narrower columns would fit.</summary>
    public const float SplitColumn = 62f;

    /// <summary>The zones the chat pane shows side by side; a longer course scrolls across under
    /// <see cref="ZonesBar"/>.</summary>
    public const int ZoneColumns = (int)(ChatTextWidth / SplitColumn);

    // The last slot, the zones' right arrow; the pointer's hit test walks every slot to it.
    private const int LastSlot = ZonesRightRow;

    private const string Background = "MP_LOBBY_BACKGROUND.JPG";
    private const string LargeArt = "MP_B_LARGE.PNG";
    private const string SmallArt = "MP_B_SMALL.PNG";

    // The lobby's geometry (OriginalLobbyScreen): the tab page's corner, the player list and the
    // chat pane, its line box and its name column.
    private const float PageX = 314f;
    private const float PageY = 26f;
    private const float ListX = 34f;
    private const float ListY = 83f;
    private const float ListWidth = 276f;
    private const float ChatX = 34f;
    private const float ChatY = 373f;
    private const float ChatWidth = 735f;
    private const float ChatNameColumn = 100f;
    private const float ChatTextWidth = ChatWidth - ChatNameColumn - 10f;
    private const float LineBoxX = 88f;
    private const float LineBoxY = 552f;
    private const float LineBoxWidth = 481f;
    private const float LineBoxHeight = 18f;

    // The Game Scores tab, the fourth, which the scores page art draws picked.
    private const float ScoresTabX = 656f;
    private const float ScoresTabWidth = 100f;

    // The plaques: art, corner and size, and the string whose face labels the lobby's own plaque
    // there. Indexed by row.
    private static readonly (string Art, float X, float Y, float Width, float Height, int FaceId)[] Plaques =
    {
        (LargeArt, 105f, 325f, 131f, 37f, 10056),
        (SmallArt, 577f, 548f, 74f, 37f, 10061),
        (LargeArt, 655f, 548f, 131f, 37f, 10062),
    };

    // A left pilot's splits, the scores page's grey for a row its flag marks.
    private static readonly BoardTint Grey = new(0xbb, 0xbb, 0xbb);

    // The plaques the menu's rows take, in menu order, with Restart standing and withheld.
    private static readonly int[] AllSlots = { PhotoRow, RestartRow, ExitRow };
    private static readonly int[] WithheldSlots = { PhotoRow, ExitRow };

    private static readonly BoardTint Black = new(0, 0, 0);

    /// <summary>The scores page's scroll bar, <see cref="OriginalRaceTable.ScrollBar"/> at the page.</summary>
    public static OriginalScrollBar ScoresBar => OriginalRaceTable.ScrollBar(PageX, PageY);

    /// <summary>The splits' scroll bar, the lobby's chat pane bar borrowed with the pane.</summary>
    public static OriginalScrollBar SplitsBar => OriginalLobbyScreen.ChatBar(SplitRows);

    /// <summary>The zones' scroll bar under the splits, remake-only: the chat bar's control turned
    /// to read across. ⚠ Do not read its fields as authored points; it is held transposed. Its X is
    /// the bar's authored y, and its Y and Height are the run along x. Its foot is the splits bar's,
    /// under the splits' last line; the quarter-turned art stands 16 tall.</summary>
    public static OriginalScrollBar ZonesBar =>
        new(SplitsBar.Y + SplitsBar.Height - OriginalScrollBar.ArrowWidth, ChatX + ChatNameColumn, SplitColumn * ZoneColumns,
            ZoneColumns, SplitsBar.Track);

    /// <summary>The slot each menu row stands on, in menu order: the plaques, then each scrolling
    /// list's two arrows. A withheld Restart leaves its plaque empty, so the exit keeps its own slot.
    /// </summary>
    public static IReadOnlyList<int> Slots(RaceResultsSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var slots = new List<int>(sheet.Withheld == null ? AllSlots : WithheldSlots);
        foreach (var (list, back, on) in new[]
                 {
                     (RaceResultsList.Scores, ScoresUpRow, ScoresDownRow), (RaceResultsList.Splits, SplitsUpRow, SplitsDownRow),
                     (RaceResultsList.Zones, ZonesLeftRow, ZonesRightRow),
                 })
        {
            if (BarOf(list).Scrolls(CountOf(sheet, list)))
            {
                slots.Add(back);
                slots.Add(on);
            }
        }

        return slots;
    }

    /// <summary>The three lists' first rows after a press on arrow <paramref name="slot"/>, each
    /// clamped to its list; any other slot leaves them as they stand.</summary>
    public static (int Scores, int Splits, int Zones) Scrolled(RaceResultsSheet sheet, int slot,
        (int Scores, int Splits, int Zones) tops)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (ArrowOf(slot) is not { } arrow)
        {
            return tops;
        }

        int top = BarOf(arrow.List).Clamp(TopOf(tops, arrow.List) + (arrow.Down ? 1 : -1), CountOf(sheet, arrow.List));
        return arrow.List switch
        {
            RaceResultsList.Scores => tops with { Scores = top },
            RaceResultsList.Splits => tops with { Splits = top },
            _ => tops with { Zones = top },
        };
    }

    /// <summary>One list's window at <paramref name="top"/> as the wheel and the thumb see it, read
    /// as the lobby reads that list. ⚠ Do not test the zones' window with the pointer as it stands.
    /// It is <see cref="ZonesBar"/>'s own box, transposed as that bar is, so it takes (y, x) and an x drag.</summary>
    public static ListWindow Window(RaceResultsSheet sheet, RaceResultsList list, int top)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return list switch
        {
            RaceResultsList.Scores => ScoresBar.Window(PageX + OriginalRaceTable.RowX, sheet.Standings.Count, top),
            RaceResultsList.Splits => SplitsBar.Window(ChatX, sheet.Splits.Count, top),
            _ => ZonesBar.Window(ZonesBar.X, sheet.ZoneNames.Count, top),
        };
    }

    /// <summary>The menu row whose plaque or arrow stands at an authored point, or -1 for none.</summary>
    public static int MenuRowAt(RaceResultsSheet sheet, float x, float y)
    {
        int plaque = RowAt(x, y);
        var slots = Slots(sheet);
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == plaque)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The plaque or scroll arrow at an authored point, or -1 for none: the pointer's whole
    /// hit test.</summary>
    public static int RowAt(float x, float y)
    {
        for (int row = 0; row <= LastSlot; row++)
        {
            var (rx, ry, w, h) = SlotRect(row);
            if (x >= rx && x < rx + w && y >= ry && y < ry + h)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>A slot's rectangle in authored pixels, a plaque's or a scroll arrow's, for a suite
    /// pointing at it.</summary>
    public static (float X, float Y, float Width, float Height) SlotRect(int slot)
    {
        if (ArrowOf(slot) is { } arrow)
        {
            var bar = BarOf(arrow.List);
            float along = arrow.Down ? bar.DownY : bar.Y;
            return arrow.List == RaceResultsList.Zones
                ? (along, bar.X, OriginalScrollBar.ArrowHeight, OriginalScrollBar.ArrowWidth)
                : (bar.X, along, OriginalScrollBar.ArrowWidth, OriginalScrollBar.ArrowHeight);
        }

        return (Plaques[slot].X, Plaques[slot].Y, Plaques[slot].Width, Plaques[slot].Height);
    }

    /// <summary>The screen for one frame. <paramref name="focus"/> is the menu row the cursor stands
    /// on and <paramref name="pressed"/> whether the pointer holds it. The two pick that plaque's
    /// strip frame and label tint as the lobby's plaques do. The standings show from
    /// <paramref name="scoresTop"/>, the splits' pilots from <paramref name="splitsTop"/> and their
    /// zones from <paramref name="zonesTop"/>.</summary>
    public static ComposedBoard Compose(RaceResultsSheet sheet, UiStrings strings, int focus, bool pressed, int scoresTop = 0,
        int splitsTop = 0, int zonesTop = 0)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(strings);
        var layers = new BoardLayers();
        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, Background), 0f, 0f));
        var tops = (Scores: ScoresBar.Clamp(scoresTop, sheet.Standings.Count), Splits: SplitsBar.Clamp(splitsTop, sheet.Splits.Count),
            Zones: ZonesBar.Clamp(zonesTop, sheet.ZoneNames.Count));
        OriginalRaceTable.Compose(sheet.Standings, PageX, PageY, strings, layers, tops.Scores);
        layers.Lines.Add(Line(strings, 10046, Title, 60f, 22f, 0f));
        layers.Lines.Add(Line(strings, 10507, MultiplayerBoardText.Word(strings, 10507, "Game Scores"),
            ScoresTabX, 30f, ScoresTabWidth, BoardJustify.Center));
        ComposeZoneKey(sheet, strings, layers);
        ComposeSplits(sheet, strings, tops.Splits, tops.Zones, layers);
        var face = MultiplayerBoardText.Regular(strings, 10575);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        string line = sheet.Withheld is { } withheld ? $"{sheet.Context}   ·   {withheld}" : sheet.Context;
        layers.Lines.Add(new BoardLine(line, LineBoxX + 4f, LineBoxY + ((LineBoxHeight - size) / 2f) - 1f,
            LineBoxWidth - 8f, size, BoardInk.Row, -1, Face: face, Colour: Black));
        var slots = Slots(sheet);
        for (int i = 0; i < slots.Count; i++)
        {
            if (ArrowOf(slots[i]) is { } arrow)
            {
                ComposeArrow(sheet, arrow, TopOf(tops, arrow.List), i == focus, i == focus && pressed, layers);
                continue;
            }

            ComposePlaque(slots[i], i == focus, i == focus && pressed, Label(sheet, slots[i]), strings, layers);
        }

        return new ComposedBoard(layers.Pictures, layers.Strokes, layers.Lines, layers.Plaques, layers.Notes,
            backdrop: layers.Backdrop, fills: layers.Fills, overlays: layers.Overlays);
    }

    private static string Label(RaceResultsSheet sheet, int row) => row switch
    {
        PhotoRow => PhotoLabel,
        RestartRow => RestartLabel,
        _ => sheet.ExitLabel,
    };

    // The zone key on the player list's lines, number and name. A course the list holds takes one
    // column, a longer one two side by side.
    private static void ComposeZoneKey(RaceResultsSheet sheet, UiStrings strings, BoardLayers layers)
    {
        layers.Lines.Add(Line(strings, 10048, ZoneHeading, ListX, 54f, 0f));
        int count = sheet.ZoneNames.Count;
        int columns = count > ZoneRows ? 2 : 1;
        int perColumn = Math.Min(ZoneRows, (count + columns - 1) / columns);
        float width = ListWidth / columns;
        for (int zone = 0; zone < count && zone < perColumn * columns; zone++)
        {
            float x = ListX + 2f + (width * (zone / perColumn));
            float y = ListY + (OriginalRaceTable.RowPitch * (zone % perColumn));
            layers.Lines.Add(Line(strings, 10575, $"{zone + 1}  {sheet.ZoneNames[zone]}", x, y, width - 6f));
        }
    }

    // The one reading of the arrow slots: which list a slot scrolls and which way, null for a plaque.
    private static (RaceResultsList List, bool Down)? ArrowOf(int slot) => slot switch
    {
        ScoresUpRow => (RaceResultsList.Scores, false),
        ScoresDownRow => (RaceResultsList.Scores, true),
        SplitsUpRow => (RaceResultsList.Splits, false),
        SplitsDownRow => (RaceResultsList.Splits, true),
        ZonesLeftRow => (RaceResultsList.Zones, false),
        ZonesRightRow => (RaceResultsList.Zones, true),
        _ => null,
    };

    private static OriginalScrollBar BarOf(RaceResultsList list) => list switch
    {
        RaceResultsList.Scores => ScoresBar,
        RaceResultsList.Splits => SplitsBar,
        _ => ZonesBar,
    };

    private static int CountOf(RaceResultsSheet sheet, RaceResultsList list) => list switch
    {
        RaceResultsList.Scores => sheet.Standings.Count,
        RaceResultsList.Splits => sheet.Splits.Count,
        _ => sheet.ZoneNames.Count,
    };

    private static int TopOf((int Scores, int Splits, int Zones) tops, RaceResultsList list) => list switch
    {
        RaceResultsList.Scores => tops.Scores,
        RaceResultsList.Splits => tops.Splits,
        _ => tops.Zones,
    };

    // A scroll arrow in its state, live while its list can move that way.
    private static void ComposeArrow(RaceResultsSheet sheet, (RaceResultsList List, bool Down) arrow, int top, bool focused,
        bool pressed, BoardLayers layers)
    {
        var bar = BarOf(arrow.List);
        int count = CountOf(sheet, arrow.List);
        var picture = bar.Arrow(arrow.Down, arrow.Down ? bar.CanDown(top, count) : bar.CanUp(top), focused, pressed);
        layers.Pictures.Add(arrow.List == RaceResultsList.Zones ? Across(picture) : picture);
    }

    // A picture of the transposed zone bar at its authored place, a quarter turn anticlockwise about
    // its corner. The up arrow then points left and the thumb lies along the run.
    private static BoardPicture Across(BoardPicture picture) =>
        picture with { X = picture.Y, Y = picture.X + OriginalScrollBar.ArrowWidth, Revs = -0.25f, FromCorner = true };

    // The splits in the chat pane, the zones from zonesTop across it. Their numbers stand on its
    // first line, then one row per pilot in race order from the window's top, a centred cell per
    // zone. A course wider than the pane draws the zone bar under the last row.
    private static void ComposeSplits(RaceResultsSheet sheet, UiStrings strings, int top, int zonesTop, BoardLayers layers)
    {
        SplitsBar.Compose(ChatX, sheet.Splits.Count, top, layers);
        int zones = sheet.ZoneNames.Count;
        var across = new BoardLayers();
        ZonesBar.Compose(ZonesBar.X, zones, zonesTop, across);
        layers.Fills.AddRange(across.Fills.Select(f => f with { X = f.Y, Y = f.X, Width = f.Height, Height = f.Width }));
        layers.Pictures.AddRange(across.Pictures.Select(Across));
        float left = ChatX + ChatNameColumn;
        int shown = Math.Min(ZoneColumns, zones - zonesTop);
        for (int column = 0; column < shown; column++)
        {
            layers.Lines.Add(Line(strings, 10575, (zonesTop + column + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                left + (SplitColumn * column), ChatY, SplitColumn, BoardJustify.Center));
        }

        for (int i = 0; top + i < sheet.Splits.Count && i < SplitRows; i++)
        {
            var row = sheet.Splits[top + i];
            float y = ChatY + (OriginalRaceTable.RowPitch * (i + 1));
            var ink = row.Left ? Grey : Black;
            // A pilot wider than the name column scrolls in place rather than wrapping.
            layers.Lines.Add(Line(strings, 10575, row.Pilot, ChatX + 4f, y, ChatNameColumn - 8f, ink: ink) with { Marquee = true });
            for (int column = 0; column < shown && zonesTop + column < row.Cells.Count; column++)
            {
                layers.Lines.Add(Line(strings, 10575, row.Cells[zonesTop + column], left + (SplitColumn * column), y, SplitColumn,
                    BoardJustify.Center, ink));
            }
        }
    }

    // A plaque as the lobby draws one: its strip frame for the state, then the label centred on it.
    // The label takes that slot's face and the scripts' label tint.
    private static void ComposePlaque(int row, bool focused, bool pressed, string label, UiStrings strings, BoardLayers layers)
    {
        var p = Plaques[row];
        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, p.Art, 4), p.X, p.Y,
            ComposedBoard.PlaqueFrame(4, focused, pressed)));
        var face = MultiplayerBoardText.Regular(strings, p.FaceId);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        layers.Lines.Add(new BoardLine(label, p.X, p.Y + ((p.Height - size) / 2f) - 1f, p.Width, size,
            BoardInk.Row, -1, Justify: BoardJustify.Center, Face: face,
            Colour: MultiplayerBoardText.LabelTint(true, focused, pressed)));
    }

    // One line in a string's face, in the lobby's black unless an ink is named.
    private static BoardLine Line(UiStrings strings, int faceId, string text, float x, float y, float width,
        BoardJustify justify = BoardJustify.Left, BoardTint? ink = null)
    {
        var face = MultiplayerBoardText.Regular(strings, faceId);
        return new BoardLine(text, x, y, width, face?.Pixels ?? MultiplayerBoardText.TextFallback, BoardInk.Row, -1,
            Justify: justify, Face: face, Colour: ink ?? Black);
    }
}
