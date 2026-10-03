using System;
using System.Collections.Generic;

namespace CSVM.Bindings;

/// <summary>
/// The snap-look rows of the original's Views 2 page: eight directions round Look Forward, one
/// numpad key each (`OriginalScreenshots/Keybinds Views 2.png`). The bottom row looks rearward.
/// So a negative Y is the rear, never down. The held rows fold into the head's one direction in
/// <see cref="Compose"/>. A keymap saved under the earlier four direction rows, where a diagonal was
/// one key on two of them, moves onto these in <see cref="MigrateSaved"/>.
/// </summary>
public static class SnapLookRows
{
    /// <summary>The token a keymap saved before the diagonal rows existed wrote for
    /// <see cref="InputAction.LookRear"/>.</summary>
    public const string LegacyRearToken = "LookDown";

    /// <summary>Each direction row with its components: X +1 right, Y +1 up and -1 rear.</summary>
    public static readonly (InputAction Action, int X, int Y)[] Directions =
    {
        (InputAction.LookUpLeftRear, -1, -1),
        (InputAction.LookRear, 0, -1),
        (InputAction.LookUpRightRear, 1, -1),
        (InputAction.LookLeft, -1, 0),
        (InputAction.LookRight, 1, 0),
        (InputAction.LookUpLeft, -1, 1),
        (InputAction.LookUp, 0, 1),
        (InputAction.LookUpRight, 1, 1),
    };

    /// <summary>The held rows as one direction. Each side takes its strongest row, so a diagonal
    /// held with its own flank reads as the diagonal, and opposite sides cancel.</summary>
    public static (float X, float Y) Compose(Func<InputAction, float> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        float right = 0f, left = 0f, up = 0f, rear = 0f;
        foreach (var (action, x, y) in Directions)
        {
            float v = value(action);
            right = x > 0 ? Math.Max(right, v) : right;
            left = x < 0 ? Math.Max(left, v) : left;
            up = y > 0 ? Math.Max(up, v) : up;
            rear = y < 0 ? Math.Max(rear, v) : rear;
        }

        return (right - left, up - rear);
    }

    /// <summary>Rewrites one context's saved rows when they predate the diagonal rows, which a file
    /// naming none of them does. A control on one up-or-rear row and one side row was that diagonal,
    /// so it moves to the diagonal's row. Every diagonal row the move leaves empty is saved empty,
    /// since such a file bound no key there and a default must not appear.</summary>
    public static void MigrateSaved(List<(InputAction Action, List<Binding> Bindings)> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var rows = new Dictionary<InputAction, List<Binding>>();
        foreach (var (action, bindings) in saved)
        {
            rows[action] = bindings;
        }

        if (!NamesAny(rows, diagonal: false) || NamesAny(rows, diagonal: true))
        {
            return;
        }

        foreach (var (diagonal, x, y) in Directions)
        {
            if (x == 0 || y == 0)
            {
                continue;
            }

            var moved = new List<Binding>();
            if (rows.TryGetValue(y > 0 ? InputAction.LookUp : InputAction.LookRear, out var vertical)
                && rows.TryGetValue(x > 0 ? InputAction.LookRight : InputAction.LookLeft, out var side))
            {
                foreach (var binding in vertical.ToArray())
                {
                    if (side.Remove(binding))
                    {
                        vertical.Remove(binding);
                        moved.Add(binding);
                    }
                }
            }

            saved.Add((diagonal, moved));
        }
    }

    // Whether the rows name any diagonal row, or with diagonal false any of the four straight ones.
    private static bool NamesAny(Dictionary<InputAction, List<Binding>> rows, bool diagonal)
    {
        foreach (var (action, x, y) in Directions)
        {
            if ((x != 0 && y != 0) == diagonal && rows.ContainsKey(action))
            {
                return true;
            }
        }

        return false;
    }
}
