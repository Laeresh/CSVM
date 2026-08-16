using System;
using System.Collections.Generic;
using Godot;

namespace CSVM;

/// <summary>
/// Single source of truth for which gamepads exist. Every input reader goes through here rather
/// than <see cref="Input.GetConnectedJoypads"/> directly: it owns the phantom-device policy (span
/// every pad, never trust <c>pads[0]</c>) and <see cref="Disabled"/> (<c>--no-pads</c>).
/// ⚠ <see cref="Connected"/> (the roster) and <see cref="For"/> (the input gate) answer different
/// questions and must stay apart. Gating the roster on focus would un-join menu players and
/// leave a session pad-less after an alt-tab. Detail: this module's docs/architecture.md entry.
/// </summary>
public static class Pads
{
    /// <summary>--no-pads: report no gamepads at all, so keyboard/scripted input is the only
    /// thing that can move anything. Debug/verification aid; never set in normal play.</summary>
    public static bool Disabled;

    /// <summary>Whether the game window has focus, maintained by <c>Launcher._Notification</c>.
    /// Gates pad <i>reads</i> (<see cref="For"/>), since Godot releases held keys on focus loss
    /// but SDL keeps polling joypads regardless. Defaults true and fails open: a run that never
    /// gets a focus notification behaves as it did before this gate existed.</summary>
    public static bool Focused = true;

    private static readonly Godot.Collections.Array<int> NoPads = new();

    /// <summary>Whether pad <i>input</i> is currently suppressed — the gate <see cref="For"/>
    /// applies. Not a statement about which devices exist; see <see cref="Connected"/>.</summary>
    public static bool InputBlocked => Disabled || !Focused;

    /// <summary>The pads that <b>exist</b> — the roster, for binding players to devices and for
    /// noticing a disconnect. Empty when <see cref="Disabled"/>. Deliberately NOT gated on focus:
    /// see the class remarks.</summary>
    public static Godot.Collections.Array<int> Connected() =>
        Disabled ? NoPads : Input.GetConnectedJoypads();

    /// <summary>The pads a given consumer may <b>read</b>: its explicit binding when it has one
    /// (a splitscreen player owns exactly one pad), otherwise every connected pad — and nothing
    /// at all when <see cref="InputBlocked"/>. Every per-frame stick/button read goes through
    /// here, which is what makes the focus gate a single switch.</summary>
    public static IEnumerable<int> For(IEnumerable<int>? bound) =>
        InputBlocked ? NoPads : bound ?? Connected();

    /// <summary>Splits the connected gamepads across the players: P2–P4 each get the next roster
    /// slot, P1 gets every pad none of them claimed (BL-374, docs/architecture.md). Null for a
    /// single player, so every pad flies the one plane. A player with no pad left sits still,
    /// logged; P1 still has the keyboard.</summary>
    public static int[][]? AssignPads(int players)
    {
        var pads = Connected();
        var list = new List<int>(pads.Count);
        foreach (int pad in pads)
            list.Add(pad);
        var assignment = AssignPads(players, list);
        if (assignment != null)
            LogPads(assignment);
        return assignment;
    }

    /// <summary>The pure assignment rule behind <see cref="AssignPads(int)"/>, split out so it is
    /// testable without a running engine (<c>Input.GetJoyName</c> in <see cref="LogPads"/> is not).
    /// </summary>
    public static int[][]? AssignPads(int players, IReadOnlyList<int> pads)
    {
        if (players <= 1)
            return null;
        var assignment = new int[players][];
        var claimed = new HashSet<int>();
        for (int i = 1; i < players; i++)
        {
            assignment[i] = i < pads.Count ? new[] { pads[i] } : Array.Empty<int>();
            foreach (int pad in assignment[i])
                claimed.Add(pad);
        }
        var free = new List<int>(pads.Count);
        foreach (int pad in pads)
            if (!claimed.Contains(pad))
                free.Add(pad);
        assignment[0] = free.ToArray();
        return assignment;
    }

    /// <summary>Log who flies what, for either source of the binding (the roster split above or
    /// the launchscreen's join flow) — a silent plane is otherwise hard to diagnose.</summary>
    public static void LogPads(int[][] assignment)
    {
        for (int i = 0; i < assignment.Length; i++)
        {
            var pads = new List<string>(assignment[i].Length);
            foreach (int pad in assignment[i])
                pads.Add($"pad {pad} \"{Input.GetJoyName(pad)}\"");
            GD.Print($"player {i + 1} input: {(i == 0 ? "keyboard" : "")}" +
                     (pads.Count > 0
                         ? $"{(i == 0 ? " + " : "")}{string.Join(" + ", pads)}"
                         : i == 0 ? "" : "NO DEVICE (connect a pad and relaunch)"));
        }
    }
}
