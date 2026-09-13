using System;
using System.Collections.Generic;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;

namespace CSVM.Tests;

/// <summary>The stateless seams of <see cref="IOriginalScreenHost"/> as every module's test fake
/// answers them: the three sectionless-page readings and the film a cinema is played through. They
/// are here rather than in each fake because they carry no per-family behaviour: a fake that lets a
/// module run its no-section path wants the same plaque column, the same heading line and the same
/// focus outline the shell gives it.</summary>
internal static class OriginalTestHost
{
    // The shell's own sectionless column and row pitch, and a plaque's fallback size, so a row
    // placed here stands where the shell would place it and has a rectangle to be hit in.
    private const float ColumnX = 319f;
    private const float FirstY = 300f;
    private const float Pitch = 40f;
    private const float PlaqueWidth = 162f;
    private const float PlaqueHeight = 28f;

    internal static OriginalRow PlaqueRow(string key, string label, int row, bool enabled, int column) =>
        new(key, label, OriginalRowKind.TextButton, ColumnX, FirstY + (row * Pitch),
            PlaqueWidth, PlaqueHeight, enabled, column, null);

    internal static void ComposePlainPage(
        string heading, IReadOnlyList<OriginalRow> rows, int focus, List<BoardLine> lines)
    {
        lines.Add(new BoardLine(heading, ColumnX, FirstY - 44f, 0f, 20f, BoardInk.Heading));
        for (int i = 0; i < rows.Count; i++)
        {
            lines.Add(new BoardLine(rows[i].Label, rows[i].X, rows[i].Y, rows[i].Width, 16f,
                i == focus ? BoardInk.RowFocused : BoardInk.Row, i));
        }
    }

    // The mission NEXT MISSION launches with no cheat behind it. The three typed latches are the
    // shell's, so no module's fake carries one and the ordinary mission is always the answer; what
    // a cheated buffer does to that mission is the shell's own test.
    internal static int CheatedMission(int ordinary) => ordinary;

    internal static BoardFill FocusMark(OriginalRow row) =>
        new(row.X, row.Y, row.Width, row.Height, 188, 188, 188, 0.75f, Border: true);

    // A cinema played in front of a fake's screen. The film is per call rather than per fake because
    // no fake runs a frame loop, so nothing ever reads whether one still stands; what the flow needs
    // is that a play whose cinema hands back at once opens its screen at once, and a deferred one
    // opens it on the callback, which one film does either way.
    internal static void PlayFilm(Action<Action> play, Action then) => new CinemaFilm().Play(play, then);
}
