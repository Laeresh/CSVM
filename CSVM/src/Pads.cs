using System;
using System.Collections.Generic;
using Godot;

namespace CSVM;

/// <summary>
/// Single source of truth for which gamepads exist. Every input reader in the project goes
/// through here rather than calling <see cref="Input.GetConnectedJoypads"/> directly, for two
/// reasons that have both already cost debugging time:
///
/// - The phantom-device policy: reads span EVERY connected pad (button-OR, axis-max) instead of
///   trusting <c>pads[0]</c>, because this machine's early joypad slots are occupied by devices
///   that are not the player's pad (the 8BitDo dongle enumerates twice while asleep, a Razer HID
///   exposes a joypad interface). Concentrating enumeration here keeps that policy in one place.
/// - <see cref="Disabled"/> (<c>--no-pads</c>): a connected pad with any stick drift silently
///   perturbs a scripted run — it steers the free camera and nudges the flight model — which
///   makes "deterministic" screenshots not. SDL's own hints do not help: Godot 4.7 enumerates
///   the pad regardless of SDL_JOYSTICK_XINPUT/RAWINPUT/WGI, so the switch has to live in our
///   own code.
///
/// <para><b>Two different questions, deliberately kept apart.</b>
/// <see cref="Connected"/> answers "which pads exist" — the <i>roster</i>; <see cref="For"/>
/// answers "which pads may this consumer read right now" — the <i>input gate</i>. They differ
/// only in that the input gate also follows window focus, but conflating them breaks things:
/// <c>LaunchMenu.SyncDevices</c> reads the roster to drop a player <i>whose pad disconnected</i>,
/// so an empty roster while alt-tabbed would un-join every joined player, and
/// <c>PlaneViewer.AssignPads</c> reads it once at session build, so alt-tabbing during a chapter
/// load would leave the whole session pad-less until relaunch. A pad that is merely unfocused has
/// not gone away.</para>
/// </summary>
public static class Pads
{
    /// <summary>--no-pads: report no gamepads at all, so keyboard/scripted input is the only
    /// thing that can move anything. Debug/verification aid; never set in normal play.</summary>
    public static bool Disabled;

    /// <summary>Whether the game window currently has focus, maintained by
    /// <c>PlaneViewer._Notification</c>. Pad <i>reads</i> are gated on it, so a
    /// stick held (or drifting) while the player is alt-tabbed cannot fly the plane, steer the
    /// free camera or scroll the launchscreen.
    ///
    /// <para>Only pads need this. Godot releases held keys when the window loses focus, but
    /// joypads are polled from SDL regardless of focus — which is exactly the asymmetry that made
    /// this necessary.</para>
    ///
    /// <para><b>Defaults to true and fails open</b>: a run that never receives a focus
    /// notification (headless, or a window that never gains focus) behaves exactly as it did
    /// before this existed, so no scripted-verification path changes.</para></summary>
    public static bool Focused = true;

    /// <summary>Whether pad <i>input</i> is currently suppressed — the gate <see cref="For"/>
    /// applies. Not a statement about which devices exist; see <see cref="Connected"/>.</summary>
    public static bool InputBlocked => Disabled || !Focused;

    private static readonly Godot.Collections.Array<int> NoPads = new();

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

    /// <summary>Splits the connected gamepads across the players: P1 gets the first
    /// pad (plus the keyboard, wired separately), P2–P4 the next ones in roster order. Null for a
    /// single player — that keeps the any-pad reads, so every pad flies the one plane. A player
    /// with no pad left gets an empty list and simply sits still (logged) — P1 still has the
    /// keyboard, so a 2P session with no controller at all is still half-flyable.</summary>
    public static int[][]? AssignPads(int players)
    {
        if (players <= 1)
            return null;
        var pads = Connected();
        var assignment = new int[players][];
        for (int i = 0; i < players; i++)
            assignment[i] = i < pads.Count ? new[] { pads[i] } : Array.Empty<int>();
        LogPads(assignment);
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
