using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>One pilot's best-run splits as drawn: the callsign and one cell per zone in course
/// order, "-" where that run never cleared the zone.</summary>
public sealed record RaceSplitRow(string Pilot, IReadOnlyList<string> Cells);

/// <summary>What one ended race's Original board shows, frozen at the race's end so a restart's
/// cleared field never redraws it. It holds the standings, the zone names in course order, each
/// pilot's splits in race order, the context line and the exit row's words.</summary>
public sealed record RaceResultsSheet(
    IReadOnlyList<RaceTableRow> Standings,
    IReadOnlyList<string> ZoneNames,
    IReadOnlyList<RaceSplitRow> Splits,
    string Context,
    string ExitLabel)
{
    /// <summary>The sheet of an ended <paramref name="race"/>. <paramref name="zoneNames"/> names
    /// the course's zones in course order; a missing or empty name reads "Zone n".</summary>
    public static RaceResultsSheet Of(StuntRace race, IReadOnlyList<string> zoneNames, string context, string exitLabel)
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

            splits.Add(new RaceSplitRow(racer.Callsign, cells));
        }

        return new RaceResultsSheet(OriginalRaceTable.Rows(standings, race.ZoneCount), names, splits, context, exitLabel);
    }
}

/// <summary>
/// The Original presentation's end-of-race screen, engine-free. It is the screen a Dogfight's end
/// lands on, the Multiplayer Lobby on its Game Scores tab, with the race in it. Its background, scores
/// page, title box, player list, chat pane, chat line and plaques are the lobby's art and positions.
/// The standings fill the scores page through <see cref="OriginalRaceTable"/>, the zone key the
/// player list, the splits the chat pane and the context the chat line. Photo Mode, Restart and the
/// exit take the Create Team, Send and Leave Game plaques; every word but the tab's is remake-only.
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
    /// holds after it; a larger field is cut off.</summary>
    public const int SplitRows = 7;

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

    // A split column is never wider than the scores page's own numeric columns.
    private const float SplitColumnMax = 62f;

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

    private static readonly BoardTint Black = new(0, 0, 0);

    /// <summary>The plaque at an authored point, or -1 for none: the pointer's whole hit test.
    /// </summary>
    public static int RowAt(float x, float y)
    {
        for (int row = 0; row < Plaques.Length; row++)
        {
            var p = Plaques[row];
            if (x >= p.X && x < p.X + p.Width && y >= p.Y && y < p.Y + p.Height)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>A plaque's rectangle in authored pixels, for a suite pointing at it.</summary>
    public static (float X, float Y, float Width, float Height) PlaqueRect(int row) =>
        (Plaques[row].X, Plaques[row].Y, Plaques[row].Width, Plaques[row].Height);

    /// <summary>The screen for one frame. <paramref name="focus"/> is the plaque the cursor stands
    /// on and <paramref name="pressed"/> whether the pointer holds it. The two pick the strip frame
    /// and the label tint as the lobby's plaques do.</summary>
    public static ComposedBoard Compose(RaceResultsSheet sheet, UiStrings strings, int focus, bool pressed)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(strings);
        var layers = new BoardLayers();
        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, Background), 0f, 0f));
        OriginalRaceTable.Compose(sheet.Standings, PageX, PageY, strings, layers);
        layers.Lines.Add(Line(strings, 10046, Title, 60f, 22f, 0f));
        layers.Lines.Add(Line(strings, 10507, MultiplayerBoardText.Word(strings, 10507, "Game Scores"),
            ScoresTabX, 30f, ScoresTabWidth, BoardJustify.Center));
        ComposeZoneKey(sheet, strings, layers);
        ComposeSplits(sheet, strings, layers);
        var face = MultiplayerBoardText.Regular(strings, 10575);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        layers.Lines.Add(new BoardLine(sheet.Context, LineBoxX + 4f, LineBoxY + ((LineBoxHeight - size) / 2f) - 1f,
            LineBoxWidth - 8f, size, BoardInk.Row, -1, Face: face, Colour: Black));
        for (int row = 0; row < Plaques.Length; row++)
        {
            ComposePlaque(row, row == focus, row == focus && pressed, Label(sheet, row), strings, layers);
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

    // The splits in the chat pane. The zone numbers stand on its first line, then one row per
    // pilot in race order, a centred cell per zone.
    private static void ComposeSplits(RaceResultsSheet sheet, UiStrings strings, BoardLayers layers)
    {
        int zones = sheet.ZoneNames.Count;
        if (zones == 0)
        {
            return;
        }

        float column = Math.Min(SplitColumnMax, ChatTextWidth / zones);
        float left = ChatX + ChatNameColumn;
        for (int zone = 0; zone < zones; zone++)
        {
            layers.Lines.Add(Line(strings, 10575, (zone + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                left + (column * zone), ChatY, column, BoardJustify.Center));
        }

        for (int i = 0; i < sheet.Splits.Count && i < SplitRows; i++)
        {
            var row = sheet.Splits[i];
            float y = ChatY + (OriginalRaceTable.RowPitch * (i + 1));
            layers.Lines.Add(Line(strings, 10575, row.Pilot, ChatX + 4f, y, ChatNameColumn - 8f));
            for (int zone = 0; zone < zones && zone < row.Cells.Count; zone++)
            {
                layers.Lines.Add(Line(strings, 10575, row.Cells[zone], left + (column * zone), y, column, BoardJustify.Center));
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

    // One line in a string's face, in the lobby's black.
    private static BoardLine Line(UiStrings strings, int faceId, string text, float x, float y, float width,
        BoardJustify justify = BoardJustify.Left)
    {
        var face = MultiplayerBoardText.Regular(strings, faceId);
        return new BoardLine(text, x, y, width, face?.Pixels ?? MultiplayerBoardText.TextFallback, BoardInk.Row, -1,
            Justify: justify, Face: face, Colour: Black);
    }
}
