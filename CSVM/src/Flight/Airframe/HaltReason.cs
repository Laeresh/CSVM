using System;

namespace CSVM.Flight.Airframe;

/// <summary>
/// Why the sim clock is stopped. The clock advances only when no reason is set, so two systems can
/// halt it at once without either one resuming it out from under the other.
/// </summary>
[Flags]
public enum HaltReason
{
    /// <summary>The clock is free to advance.</summary>
    None = 0,

    /// <summary>A player asked for it. Only that player may drop it again.</summary>
    Paused = 1,

    /// <summary>A results board is up. Dropped by a rerun, never by a player's pause key.</summary>
    Ended = 2,

    /// <summary>A live graphics switch is drawn over (<c>Launch.SwitchCover</c>). Dropped by
    /// the cover once the frames after the switch settle.</summary>
    Switching = 4,

    /// <summary>The HUD layout editor is open over a pane (<c>Flight.Hud.HudLayoutEditor</c>).
    /// Dropped by the editor's own save or cancel, never by a player's pause key.</summary>
    Editing = 8,
}
