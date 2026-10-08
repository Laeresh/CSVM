using System;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// Steam's on-screen keyboard, raised for a text field that a pad press or a touch armed. Steam
/// draws it over the game only in Game Mode on a SteamOS device, so everywhere else
/// <see cref="Available"/> is false and every call here does nothing. Its keys arrive as ordinary
/// key events, so the fields read them as they read a real keyboard. One field holds it at a time,
/// named by an owner and an id, and only that owner closes it.
/// ⚠ Do not raise it on focus. The pad sends nothing while it is up, so a field merely crossed
/// would strand the player behind a keyboard they never asked for.
/// </summary>
public static class ScreenKeyboard
{
    /// <summary>Asks the running Steam client to show its keyboard.</summary>
    public const string OpenUrl = "steam://open/keyboard";

    /// <summary>Asks the running Steam client to hide its keyboard.</summary>
    public const string CloseUrl = "steam://close/keyboard";

    /// <summary>Whether this run can raise the keyboard: Game Mode (<see cref="SteamOs.InGameMode"/>),
    /// since Desktop Mode opens it behind a fullscreen window. A suite sets it to drive the fields.</summary>
    public static bool Available { get; set; } = SteamOs.InGameMode;

    /// <summary>Hands a URL to the system's handler. A seam, so a suite records the calls rather
    /// than opening anything.</summary>
    public static Action<string> Shell { get; set; } = url => OS.ShellOpen(url);

    /// <summary>The field the keyboard was raised for, or null while it is down.</summary>
    public static ScreenKeyboardField? Shown { get; private set; }

    /// <summary>Raises the keyboard for <paramref name="field"/>, or does nothing where none can be
    /// raised. Raising it again for the field already holding it is the way back after the player
    /// hid it with Steam's own button. True when it was raised.</summary>
    public static bool Show(ScreenKeyboardField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Available)
        {
            return false;
        }

        Shell(OpenUrl);
        Shown = field;
        Log.Info("ui", $"screen keyboard: up for {field.Owner}/{field.Id}");
        return true;
    }

    /// <summary>Lowers the keyboard if <paramref name="owner"/> raised it.</summary>
    public static void Hide(string owner)
    {
        if (Shown is not { } shown || shown.Owner != owner)
        {
            return;
        }

        Shell(CloseUrl);
        Shown = null;
        Log.Info("ui", $"screen keyboard: down from {shown.Owner}/{shown.Id}");
    }

    /// <summary>Lowers the keyboard if <paramref name="owner"/> raised it for a field other than
    /// <paramref name="armed"/>, which is the field still taking text, or null for none. An owner
    /// calls it each frame, so leaving the field by any door closes the keyboard.</summary>
    public static void Follow(string owner, string? armed)
    {
        if (Shown is { } shown && shown.Owner == owner && shown.Id != armed)
        {
            Hide(owner);
        }
    }
}
