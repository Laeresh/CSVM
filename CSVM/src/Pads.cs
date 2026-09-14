using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM;

/// <summary>
/// Single source of truth for which gamepads exist. Every input reader goes through here rather
/// than <see cref="Input.GetConnectedJoypads"/> directly: it owns the phantom-device policy (span
/// every pad, never trust <c>pads[0]</c>) and <see cref="Disabled"/> (<c>--no-pads</c>).
/// ⚠ <see cref="Connected"/> (the roster) and <see cref="For"/> (the input gate) answer different
/// questions and must stay apart. Gating the roster on focus would un-join menu players and
/// leave a session pad-less after an alt-tab.
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

    // ⚠ Built on first use, never as a field initialiser. A Godot.Collections.Array cannot be
    // constructed without a running engine, and a static initialiser here would run on the first
    // touch of ANY static field on this class, taking every off-engine reader of Disabled, Focused
    // and For down with it.
    private static Godot.Collections.Array<int>? _noPads;

    /// <summary>Whether pad <i>input</i> is currently suppressed, the gate <see cref="For"/>
    /// applies. Not a statement about which devices exist; see <see cref="Connected"/>.</summary>
    public static bool InputBlocked => Disabled || !Focused;

    /// <summary>The pads that <b>exist</b>, the roster, for binding players to devices and for
    /// noticing a disconnect. Empty when <see cref="Disabled"/>. Deliberately NOT gated on focus:
    /// see the class remarks.</summary>
    public static Godot.Collections.Array<int> Connected() =>
        Disabled ? _noPads ??= new Godot.Collections.Array<int>() : Input.GetConnectedJoypads();

    /// <summary>The pads a given consumer may <b>read</b>: its explicit binding when it has one
    /// (a splitscreen player owns exactly one pad), otherwise every connected pad, and nothing
    /// at all when <see cref="InputBlocked"/>. Every per-frame stick/button read goes through
    /// here, which is what makes the focus gate a single switch. A plain empty array on the blocked
    /// path, not an engine one, so a seat holding an explicit binding resolves off-engine.</summary>
    public static IEnumerable<int> For(IEnumerable<int>? bound) =>
        InputBlocked ? Array.Empty<int>() : bound ?? Connected();

    /// <summary>Splits the connected gamepads across the players: P2–P4 each get the next roster
    /// slot, P1 gets every pad none of them claimed (docs/architecture.md). Null for a
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

    /// <summary>Log the connected roster and who flies what, for either source of the binding (the
    /// roster split above or the launchscreen's join flow), a silent plane is otherwise hard to
    /// diagnose.
    /// ⚠ Through the run log, not <c>GD.Print</c>: the binding is settled once at build and is
    /// unrecoverable afterwards, so a player reporting two pilots on one plane has nothing to hand
    /// over unless it survives in the file sink. What each line answers: docs/architecture.md.</summary>
    public static void LogPads(int[][] assignment)
    {
        // The roster POSITIONS, not just the ids: AssignPads seats P2-P4 by position, so a device
        // holding one without producing input takes that seat and the real pad falls back to P1
        // (docs/architecture.md). The per-seat lines below cannot show that device.
        var roster = new List<string>();
        int slot = 0;
        foreach (int pad in Connected())
        {
            roster.Add($"[{slot++}] pad {pad} \"{Input.GetJoyName(pad)}\"");
        }

        Log.Info("core", $"pad roster: {(roster.Count > 0 ? string.Join(", ", roster) : "none connected")}");
        for (int i = 0; i < assignment.Length; i++)
        {
            var pads = new List<string>(assignment[i].Length);
            foreach (int pad in assignment[i])
                pads.Add($"pad {pad} \"{Input.GetJoyName(pad)}\"");
            string devices = pads.Count > 0
                ? $"{(i == 0 ? "keyboard + " : "")}{string.Join(" + ", pads)}"
                : i == 0 ? "keyboard" : "NO DEVICE (connect a pad and relaunch)";
            Log.Info("core", $"player {i + 1} input: {devices}");
        }
    }
}
