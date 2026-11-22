using System;

namespace CSVM.Utils;

/// <summary>
/// The gate the load screen holds closed while it stands. No game sound and no input reach the
/// game, the pause board or the menus behind it. <c>UI.Screens.LoadBoard</c> closes it on
/// entering the tree and opens it on leaving. A network flight's board held for the other machines
/// therefore holds the gate too. Its readers are listed in docs/architecture/Utils.md. Engine-free,
/// so the bindings' units read it without a window.
/// </summary>
public static class LoadGate
{
    // Boards standing, a count rather than a flag. A graphics switch's board can stand over the load
    // screen, and a theater rehost moves a board out of the tree and back.
    private static int _standing;

    /// <summary>Raised on every close and open, for the sound that follows the gate.</summary>
    public static event Action? Changed;

    /// <summary>Whether a load screen stands.</summary>
    public static bool Closed => _standing > 0;

    /// <summary>A load screen went up.</summary>
    public static void Close()
    {
        _standing++;
        Changed?.Invoke();
    }

    /// <summary>A load screen came down. An open with none standing changes nothing.</summary>
    public static void Open()
    {
        if (_standing == 0)
        {
            return;
        }

        _standing--;
        Changed?.Invoke();
    }
}
