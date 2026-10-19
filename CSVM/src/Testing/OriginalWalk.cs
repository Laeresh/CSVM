using System;
using System.Collections.Generic;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// The cursor presses a suite or a unit test makes to reach one row of an Original screen. The
/// screen's walk is spatial, so a fixed run of Downs only reaches a row by the layout's accident.
/// This plans the shortest walk over the shell's own step instead, one press at a time.
/// </summary>
internal static class OriginalWalk
{
    /// <summary>Walks the shell's focus onto the row carrying <paramref name="key"/>, handing each
    /// planned press to <paramref name="press"/>, which is the suite's own frame. A sideways press
    /// that left the focus where it stood changed a value, so the plan stops offering it there.
    /// Stops where no walk reaches the row, and the caller's check on the focused key then fails.</summary>
    internal static void WalkTo(OriginalShell shell, string key, Action<MenuCommands> press, bool pad = false)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(press);
        var valued = new HashSet<(string Key, int MoveX)>();
        for (int guard = 0; guard < 96 && shell.FocusedKey != key; guard++)
        {
            var rows = shell.Rows;
            int target = -1;
            for (int i = 0; i < rows.Count && target < 0; i++)
            {
                target = rows[i].Key == key ? i : -1;
            }

            if (target < 0 || NextPress(rows, shell.Lists, shell.Focus, target, pad, valued) is not { } step)
            {
                return;
            }

            string from = shell.FocusedKey;
            press(step);
            if (step.MoveX != 0 && shell.FocusedKey == from)
            {
                valued.Add((from, step.MoveX));
            }
        }
    }

    /// <summary>The first press of the shortest walk from <paramref name="from"/> to
    /// <paramref name="target"/>, searched breadth first over <see cref="OriginalFocus.Step"/>.
    /// The pad's directions (up, left) are tried before the keyboard's when <paramref name="pad"/>.
    /// A row off its list's window is reached through the list's nearest live row, from which a
    /// press on towards it scrolls the list. Null when no walk reaches it.</summary>
    internal static MenuCommands? NextPress(
        IReadOnlyList<OriginalRow> rows, IReadOnlyList<OriginalList> lists, int from, int target, bool pad = false,
        ISet<(string Key, int MoveX)>? valued = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var moves = pad
            ? new[] { (X: 0, Y: -1), (X: -1, Y: 0), (X: 0, Y: 1), (X: 1, Y: 0) }
            : new[] { (X: 0, Y: 1), (X: 1, Y: 0), (X: 0, Y: -1), (X: -1, Y: 0) };
        int goal = target;
        if (!rows[target].Visible)
        {
            goal = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Visible && rows[i].Enabled && (goal < 0 || Math.Abs(i - target) < Math.Abs(goal - target)))
                {
                    goal = i;
                }
            }

            if (goal == from)
            {
                return new MenuCommands { MoveY = target > from ? 1 : -1 };
            }
        }

        // A radio may take a sideways press as a value too, so a walk round it is tried first.
        var first = Search(rows, lists, from, moves, valued, OriginalFocus.TakesValue);
        if (!first.ContainsKey(goal))
        {
            first = Search(rows, lists, from, moves, valued, row => OriginalFocus.TakesValue(row) && row.Kind != OriginalRowKind.Radio);
        }

        // A dropdown the screen moves off sideways, the local lobby's map box, is tried only where
        // no other walk reaches the row. A press that takes a value is learned in valued.
        if (!first.ContainsKey(goal))
        {
            first = Search(rows, lists, from, moves, valued, row => row.Kind == OriginalRowKind.Slider);
        }

        return goal != from && first.TryGetValue(goal, out var press) ? new MenuCommands { MoveX = press.X, MoveY = press.Y } : null;
    }

    /// <summary>The rows arrow presses reach from <paramref name="from"/>. A dropdown, a slider or
    /// a radio takes its sideways press as a value. The rows in <paramref name="movesSideways"/>
    /// are the exception: their screen moves the focus off them sideways.</summary>
    internal static IReadOnlyCollection<int> Reachable(
        IReadOnlyList<OriginalRow> rows, IReadOnlyList<OriginalList> lists, int from, IReadOnlySet<string> movesSideways)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(movesSideways);
        return Search(rows, lists, from, new[] { (0, 1), (1, 0), (0, -1), (-1, 0) }, null,
            row => OriginalFocus.TakesValue(row) && !movesSideways.Contains(row.Key)).Keys;
    }

    // Every row the shell's own step reaches from one, breadth first, each with the first press of
    // its shortest walk.
    private static Dictionary<int, (int X, int Y)> Search(
        IReadOnlyList<OriginalRow> rows, IReadOnlyList<OriginalList> lists, int from,
        (int X, int Y)[] moves, ISet<(string Key, int MoveX)>? valued, Func<OriginalRow, bool> takesValue)
    {
        var first = new Dictionary<int, (int X, int Y)> { [from] = (0, 0) };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int at = queue.Dequeue();
            foreach (var move in moves)
            {
                // A value row takes a sideways press, as does a row a sideways press already left the focus on.
                if (move.X != 0 && (takesValue(rows[at]) || (valued?.Contains((rows[at].Key, move.X)) ?? false)))
                {
                    continue;
                }

                int next = OriginalFocus.Step(rows, lists, at, move.X, move.Y);
                if (first.TryAdd(next, at == from ? move : first[at]))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return first;
    }
}
