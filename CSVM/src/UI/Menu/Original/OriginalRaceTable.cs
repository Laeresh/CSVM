using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// A stunt race's standings drawn as the original's multiplayer scores page. The page art is
/// <c>MP_LOBBY_STATSCREEN.PNG</c>, and its columns, headers and ten rows stand where
/// <c>MULTIPLAYERLOBBY_STATS.SCRIPT</c> puts them. The original has no race table, so the race
/// borrows that page under remake-only headers. Place and callsign stand at the name column's left,
/// the aircraft at its right, then best, gap and runs, the fifth column empty. Engine-free; any
/// Original board composes it at its own page corner. Module entry: docs/architecture/UI.md.
/// </summary>
public static class OriginalRaceTable
{
    /// <summary>The scores page art, drawn at the page corner the caller names.</summary>
    public const string PageArt = "MP_LOBBY_STATSCREEN.PNG";

    /// <summary>The rows the page shows, the script's <c>RDA</c>; a longer field scrolls under
    /// <see cref="ScrollBar"/>.</summary>
    public const int VisibleRows = 10;

    /// <summary>The row pitch, the script's <c>SDA</c>.</summary>
    public const float RowPitch = 20f;

    /// <summary>The first row's corner relative to the page, the script's <c>+24, +69</c>.</summary>
    public const float RowX = 24f;

    /// <summary>The first row's top relative to the page.</summary>
    public const float RowY = 69f;

    // The script's row faces: the name column's and the numeric columns'.
    private const int NameFace = 10573;
    private const int NumberFace = 10574;

    // The aircraft's header, right-aligned in the name column's header box. No string of the
    // install words a race column, so every header here is remake-only.
    private const string AircraftHeader = "Aircraft";

    // The aircraft keeps this far clear of the name column's right-hand rule.
    private const float AircraftInset = 4f;

    // The page's own column headers: id, corner relative to the page and box width. The first is
    // left-justified, the rest centred, as the script's just_left and just_all say.
    private static readonly (int Id, float X, float Y, float Width)[] HeaderBoxes =
    {
        (10542, 21f, 43f, 150f), (10543, 179f, 44f, 60f), (10544, 242f, 44f, 58f),
        (10545, 303f, 44f, 56f), (10546, 362f, 44f, 54f),
    };

    // The race's header in each page column. The fifth is empty: the race has no fifth figure.
    private static readonly string[] Headers = { "Pilot", "Best", "Gap", "Runs", "" };

    // Each row cell's offset from the row corner and width: the name column then four centred ones.
    private static readonly (float X, float Width)[] Cells =
    {
        (0f, 154f), (154f, 62f), (216f, 61f), (277f, 60f), (337f, 57f),
    };

    // The script's row ink, black, and the grey (0xffbbbbbb) of a row its own flag marks. Here the
    // flag is a pilot who left the race (docs/org/menu-inventory.md, the lobby's Game Scores).
    private static readonly BoardTint Ink = new(0, 0, 0);
    private static readonly BoardTint FlaggedInk = new(0xbb, 0xbb, 0xbb);

    /// <summary>The field in <paramref name="standings"/>' order as table rows, each column
    /// <see cref="RaceRows"/>' words. A pilot who left goes unsuffixed, since the page greys the row.
    /// <paramref name="zoneCount"/> is the course's.</summary>
    public static IReadOnlyList<RaceTableRow> Rows(IReadOnlyList<Racer> standings, int zoneCount)
    {
        var rows = new List<RaceTableRow>();
        foreach (var row in RaceRows.Of(standings, zoneCount))
        {
            rows.Add(new RaceTableRow($"{row.Place}  {row.Racer.Callsign}", row.Aircraft, row.Best, row.Gap, row.Runs, row.Left));
        }

        return rows;
    }

    /// <summary>The page's scroll bar, the script's <c>HEA</c>: at (+419, +66), 195 tall, over
    /// <see cref="VisibleRows"/>. Its <c>KF</c> is 0x00000000, so no track is painted.</summary>
    public static OriginalScrollBar ScrollBar(float pageX, float pageY) => new(pageX + 419f, pageY + 66f, 195f, VisibleRows);

    /// <summary>The page at (<paramref name="pageX"/>, <paramref name="pageY"/>) in authored pixels.
    /// Its art goes into the backdrop, and <see cref="ComposeRows"/> writes the rest.</summary>
    public static void Compose(IReadOnlyList<RaceTableRow> rows, float pageX, float pageY, UiStrings strings, BoardLayers layers,
        int top = 0)
    {
        ArgumentNullException.ThrowIfNull(layers);
        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, PageArt), pageX, pageY));
        ComposeRows(rows, pageX, pageY, strings, layers, top);
    }

    /// <summary>The headers and <see cref="VisibleRows"/> rows from <paramref name="top"/> into the
    /// lines, and the scroll bar's thumb once the field is longer. The arrows are the caller's rows.
    /// The page stands drawn already at (<paramref name="pageX"/>,
    /// <paramref name="pageY"/>), as on the lobby's Game Scores tab. The string table supplies the
    /// faces, else the fallback size holds.</summary>
    public static void ComposeRows(IReadOnlyList<RaceTableRow> rows, float pageX, float pageY, UiStrings strings, BoardLayers layers,
        int top = 0)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(layers);
        var name = HeaderBoxes[0];
        layers.Lines.Add(Line(strings, name.Id, Headers[0], pageX + name.X, pageY + name.Y, name.Width, BoardJustify.Left));
        layers.Lines.Add(Line(strings, name.Id, AircraftHeader, pageX + name.X, pageY + name.Y, name.Width, BoardJustify.Right));
        for (int column = 1; column < HeaderBoxes.Length; column++)
        {
            var (id, x, y, width) = HeaderBoxes[column];
            if (Headers[column].Length > 0)
            {
                layers.Lines.Add(Line(strings, id, Headers[column], pageX + x, pageY + y, width, BoardJustify.Center));
            }
        }

        var bar = ScrollBar(pageX, pageY);
        top = bar.Clamp(top, rows.Count);
        for (int i = 0; top + i < rows.Count && i < VisibleRows; i++)
        {
            var row = rows[top + i];
            float x = pageX + RowX;
            float y = pageY + RowY + (RowPitch * i);
            var ink = row.Left ? FlaggedInk : Ink;
            layers.Lines.Add(Line(strings, NameFace, row.Pilot, x, y, Cells[0].Width, BoardJustify.Left, ink));
            layers.Lines.Add(Line(strings, NameFace, row.Aircraft, x, y, Cells[0].Width - AircraftInset, BoardJustify.Right, ink));
            string[] figures = { row.Best, row.Gap, row.Runs };
            for (int column = 0; column < figures.Length; column++)
            {
                var cell = Cells[column + 1];
                layers.Lines.Add(Line(strings, NumberFace, figures[column], x + cell.X, y, cell.Width, BoardJustify.Center, ink));
            }
        }

        bar.Compose(pageX + RowX, rows.Count, top, layers);
    }

    // One cell in a string's face and an ink, the script's black unless named. The words are the
    // race's, never the string's own, which for the row faces is empty and for a header names a
    // Dogfight column.
    private static BoardLine Line(UiStrings strings, int faceId, string text, float x, float y, float width, BoardJustify justify,
        BoardTint? ink = null)
    {
        var face = MultiplayerBoardText.Regular(strings, faceId);
        return new BoardLine(text, x, y, width, face?.Pixels ?? MultiplayerBoardText.TextFallback, BoardInk.Row, -1,
            Justify: justify, Face: face, Colour: ink ?? Ink);
    }
}
