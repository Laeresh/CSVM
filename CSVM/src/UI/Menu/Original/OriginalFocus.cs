using System;
using System.Collections.Generic;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Where a pad or arrow press moves the focus on an Original screen, the shell's one stepper. The
/// live rows drawn on screen are the stops, read through <see cref="SpatialFocus.Step"/>, so
/// neither row order nor a row's column decides a walk. A list with rows to walk takes no stop on
/// its scroll arrows. A press leaving a list with more rows that way scrolls it instead. A band of
/// value rows alone lets a wrapping press out. A sideways step that changes a value is the shell's
/// business and never reaches this engine-free stepper.
/// </summary>
internal static class OriginalFocus
{
    /// <summary>The row a press moves the focus to from <paramref name="focus"/>. A vertical press
    /// past a list's last row on screen scrolls the list one row. It lands on the row now standing
    /// where the focus stood. At the list's end the press leaves the list. A scroll rebuilds the
    /// rows, so <paramref name="rowsOf"/> reads them afresh.</summary>
    internal static int Move(
        Func<IReadOnlyList<OriginalRow>> rowsOf, IReadOnlyList<OriginalList> lists, int focus, int moveX, int moveY)
    {
        ArgumentNullException.ThrowIfNull(rowsOf);
        var rows = rowsOf();
        int to = Step(rows, lists, focus, moveX, moveY);
        if (focus < 0 || focus >= rows.Count)
        {
            return to;
        }

        var at = Box(rows[focus]);
        if (moveY == 0 || ListUnder(lists, at) is not { } list
            || (to != focus && Inside(list.Window, Box(rows[to])) && SpatialFocus.Ahead(at, Box(rows[to]), 0, moveY)))
        {
            return to;
        }

        int top = list.Window.TopAfterWheel(Math.Sign(moveY));
        if (top == list.Window.Top)
        {
            return to;
        }

        list.ScrollTo(top);
        rows = rowsOf();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Enabled && rows[i].Visible && rows[i].X == at.X && rows[i].Y == at.Y)
            {
                return i;
            }
        }

        return Math.Min(focus, rows.Count - 1);
    }

    /// <summary>The live row on screen a press moves the focus to, scrolling nothing. It is
    /// <see cref="Move"/> without a list's scroll, so a walk can be planned without side effects.</summary>
    internal static int Step(IReadOnlyList<OriginalRow> rows, IReadOnlyList<OriginalList> lists, int focus, int moveX, int moveY)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(lists);
        if (focus < 0 || focus >= rows.Count || (moveX == 0 && moveY == 0))
        {
            return focus;
        }

        var stops = new (float X, float Y, float Width, float Height)?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            stops[i] = IsStop(rows[i], rows, lists) ? Box(rows[i]) : null;
        }

        var at = Box(rows[focus]);
        int to = SpatialFocus.Step(stops, focus, at, moveX, moveY);
        if (moveY == 0 || to == focus || SpatialFocus.Ahead(at, Box(rows[to]), 0, moveY))
        {
            return to;
        }

        // The press wrapped within its column band. Left and Right change a value row's value, so a
        // band of value rows alone would trap the cursor. There the press leaves the band: the
        // centre decides which stops lie ahead, the edge gap the nearest.
        if (!TakesValue(rows[focus]))
        {
            return to;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (i != focus && stops[i] is { } stop && SpatialFocus.InBand(at, stop, vertical: true))
            {
                if (!TakesValue(rows[i]))
                {
                    return to;
                }

                stops[i] = null;
            }
        }

        int away = SpatialFocus.Leave(stops, focus, at, 0, moveY);
        return away != focus ? away : to;
    }

    /// <summary>Whether a row takes the cursor: live, on screen, and no arrow on the bar of a list
    /// that has rows to walk.</summary>
    internal static bool IsStop(OriginalRow row, IReadOnlyList<OriginalRow> rows, IReadOnlyList<OriginalList> lists) =>
        row.Enabled && row.Visible && !OnScrollBar(row, rows, lists);

    /// <summary>Whether a row's sideways press changes a value rather than moving the focus.</summary>
    internal static bool TakesValue(OriginalRow row) =>
        row.Kind is OriginalRowKind.Dropdown or OriginalRowKind.Radio or OriginalRowKind.Slider;

    private static (float X, float Y, float Width, float Height) Box(OriginalRow row) => (row.X, row.Y, row.Width, row.Height);

    private static bool Inside(ListWindow window, (float X, float Y, float Width, float Height) box) =>
        SpatialFocus.Centre(box) is var (x, y) && window.Contains(x, y);

    private static OriginalList? ListUnder(IReadOnlyList<OriginalList> lists, (float X, float Y, float Width, float Height) box)
    {
        foreach (var list in lists)
        {
            if (Inside(list.Window, box))
            {
                return list;
            }
        }

        return null;
    }

    // A scroll arrow stands on its list's bar: inside the window, in the thumb's column. The
    // pointer still clicks one; the keyboard scrolls by walking off the list's end instead. A
    // window of text with no row of its own to walk keeps its arrows, the keyboard's only scroll.
    private static bool OnScrollBar(OriginalRow row, IReadOnlyList<OriginalRow> rows, IReadOnlyList<OriginalList> lists)
    {
        foreach (var list in lists)
        {
            if (OnBar(list.Window, row))
            {
                foreach (var other in rows)
                {
                    if (other.Enabled && other.Visible && Inside(list.Window, Box(other)) && !OnBar(list.Window, other))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool OnBar(ListWindow window, OriginalRow row)
    {
        float x = SpatialFocus.Centre(Box(row)).X;
        return Inside(window, Box(row)) && x >= window.ThumbX && x < window.ThumbX + window.ThumbWidth;
    }
}
