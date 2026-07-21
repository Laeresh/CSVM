using System.Collections.Generic;
using Godot;

namespace CrimsonSkies;

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
/// </summary>
public static class Pads
{
    /// <summary>--no-pads: report no gamepads at all, so keyboard/scripted input is the only
    /// thing that can move anything. Debug/verification aid; never set in normal play.</summary>
    public static bool Disabled;

    private static readonly Godot.Collections.Array<int> NoPads = new();

    /// <summary>The connected pads, or none when <see cref="Disabled"/>.</summary>
    public static Godot.Collections.Array<int> Connected() =>
        Disabled ? NoPads : Input.GetConnectedJoypads();

    /// <summary>The pads a given consumer should read: its explicit binding when it has one
    /// (a splitscreen player owns exactly one pad), otherwise every connected pad — and
    /// nothing at all when <see cref="Disabled"/>.</summary>
    public static IEnumerable<int> For(IEnumerable<int>? bound) =>
        Disabled ? NoPads : bound ?? Connected();
}
