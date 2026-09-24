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

    // The last raw roster Connected() deduplicated and the answer it gave. The rule reads
    // Input.GetJoyInfo per pad, which marshals a dictionary each time, and Connected runs several
    // times a frame; a roster that has not changed cannot change the answer.
    private static Godot.Collections.Array<int>? _deduped;
    private static int[] _rawSeen = Array.Empty<int>();

    /// <summary>Whether pad <i>input</i> is currently suppressed, the gate <see cref="For"/>
    /// applies. Not a statement about which devices exist; see <see cref="Connected"/>.</summary>
    public static bool InputBlocked => Disabled || !Focused;

    /// <summary>The pads that <b>exist</b>, the roster, for binding players to devices and for
    /// noticing a disconnect, one entry per physical controller (<see cref="KeepXInputView"/>).
    /// Empty when <see cref="Disabled"/>. Deliberately NOT gated on focus: see the class remarks.
    /// </summary>
    public static Godot.Collections.Array<int> Connected()
    {
        if (Disabled)
        {
            return _noPads ??= new Godot.Collections.Array<int>();
        }

        var raw = Input.GetConnectedJoypads();
        if (_deduped != null && SameRoster(raw, _rawSeen))
        {
            return _deduped;
        }

        _rawSeen = new int[raw.Count];
        for (int i = 0; i < raw.Count; i++)
        {
            _rawSeen[i] = raw[i];
        }

        var views = new List<(int Pad, bool XInput, string Model)>(raw.Count);
        foreach (int pad in raw)
        {
            views.Add((pad, IsXInputView(pad), ModelOf(pad)));
        }

        _deduped = new Godot.Collections.Array<int>();
        foreach (int pad in KeepXInputView(views))
        {
            _deduped.Add(pad);
        }

        return _deduped;
    }

    /// <summary>The roster with each controller's duplicate views dropped. A pad the platform
    /// reports through XInput and DirectInput both arrives twice (an 8BitDo Ultimate 2 arrives
    /// three times), and the extra view answers no input while holding a roster slot, so one Start
    /// joins two seats and a seat lands on a stick nobody is holding. Where a model has an XInput
    /// view, only its XInput views are kept; a model with none keeps every view it has, so a
    /// DirectInput-only stick still plays. Pure, so the rule is testable without a joypad.</summary>
    public static int[] KeepXInputView(IReadOnlyList<(int Pad, bool XInput, string Model)> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        var xinput = new HashSet<string>();
        foreach (var view in views)
        {
            if (view.XInput)
            {
                xinput.Add(view.Model);
            }
        }

        var kept = new List<int>(views.Count);
        foreach (var view in views)
        {
            if (view.XInput || !xinput.Contains(view.Model))
            {
                kept.Add(view.Pad);
            }
        }

        return kept.ToArray();
    }

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

    // Whether the platform reports this slot through XInput, which is the view that carries input.
    // Godot passes SDL's own device info through, so the key's presence is the whole test.
    private static bool IsXInputView(int pad) => Input.GetJoyInfo(pad).ContainsKey("xinput_index");

    // What counts as one controller for the rule above: the vendor and product the platform
    // reports, which every view of one device shares and which a Steam virtual pad inherits from
    // the device behind it.
    private static string ModelOf(int pad)
    {
        var info = Input.GetJoyInfo(pad);
        string vendor = info.TryGetValue("vendor_id", out var v) ? v.ToString() : string.Empty;
        string product = info.TryGetValue("product_id", out var p) ? p.ToString() : string.Empty;
        return $"{vendor}/{product}";
    }

    private static bool SameRoster(Godot.Collections.Array<int> raw, int[] seen)
    {
        if (raw.Count != seen.Length)
        {
            return false;
        }

        for (int i = 0; i < seen.Length; i++)
        {
            if (raw[i] != seen[i])
            {
                return false;
            }
        }

        return true;
    }
}
