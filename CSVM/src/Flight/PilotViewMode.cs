using System;

namespace CSVM.Flight;

/// <summary>The view a pilot has SELECTED, as opposed to the numpad views held for as long as a
/// key is down. The original's selector accepts exactly these three and nothing else
/// (<c>FUN_0042c210</c> stores the accepted value, <c>FUN_004414a0</c> falls back to Cockpit for
/// anything outside the set) — docs/org/cameraViews.md, "Only three views are player-selectable".
/// The numbers ARE the engine's camera modes (<c>camera + 0x14c</c>), kept so a log line or a
/// future decode reads against the same values the binary uses.</summary>
public enum PilotViewMode
{
    /// <summary>Mode 0: the following 3rd-person camera, this project's only view until A1.</summary>
    Chase = 0,

    /// <summary>Mode 6: first person with the <c>cockpit1</c> interior drawn (80° H in the
    /// original). Placement is A2's, FOV A3's, interior B11's — A1 lands the mode alone.</summary>
    Cockpit = 6,

    /// <summary>Mode 7: first person from the same <c>cockpit_camera</c> point with the interior
    /// and the <c>markers</c>/<c>dontmove</c> nodes hidden (60° H in the original).</summary>
    Nose = 7,
}

/// <summary>The rules a selected view obeys, as pure functions: which mode a cycle key press
/// lands on, whether a mode is one of the two first-person views, and what a held numpad key does
/// to the selection. Separate from <see cref="CameraController"/> (which owns a
/// <c>Camera3D</c> and cannot be built without an engine) so the decisions are testable headlessly
/// — <c>PilotViewTests</c> is that test.</summary>
public static class PilotView
{
    /// <summary>Whether this mode is one of the original's two first-person views. The answer the
    /// anim data's <c>PLAYER_1ST_PERSON</c> condition (id 120) is given, and the gate every later
    /// first-person behaviour hangs off.</summary>
    public static bool IsFirstPerson(PilotViewMode mode) =>
        mode is PilotViewMode.Cockpit or PilotViewMode.Nose;

    /// <summary>One press of the cycle key ("Cycle Cockpit Views", <c>MSG_LOOK_FORWARD</c>): it
    /// cycles the first-person PAIR, so Cockpit and Nose swap. From Chase it enters Cockpit,
    /// which is the fallback the original's selector applies to a request it does not
    /// recognise.</summary>
    public static PilotViewMode Cycle(PilotViewMode mode) =>
        mode == PilotViewMode.Cockpit ? PilotViewMode.Nose : PilotViewMode.Cockpit;

    /// <summary>Which view is actually in force this frame. A held numpad key (or the look-behind)
    /// is a momentary override and wins over the selected mode exactly as it wins over
    /// <c>--view=</c>'s pinned digit today; releasing it returns to the selection, because the
    /// selection is state and the key is not.</summary>
    public static PilotViewMode Effective(PilotViewMode selected, bool heldViewActive) =>
        heldViewActive ? PilotViewMode.Chase : selected;

    /// <summary>The <c>--view=</c> spelling of a selected mode, or null when the argument names
    /// something else (a numpad digit, <c>back</c>, a typo) and the caller should go on to parse
    /// it as one of those.</summary>
    public static PilotViewMode? Parse(string s) =>
        Named(s, PilotViewMode.Chase) ?? Named(s, PilotViewMode.Cockpit) ?? Named(s, PilotViewMode.Nose);

    /// <summary>The mode's name for a log line and for <c>--view=</c>, lower case, so the
    /// breadcrumb a scripted capture reads back matches the argument that asked for it.</summary>
    public static string Name(PilotViewMode mode) => mode switch
    {
        PilotViewMode.Cockpit => "cockpit",
        PilotViewMode.Nose => "nose",
        _ => "chase",
    };

    // One name matched case-insensitively, so --view=Cockpit reads like --view=BACK already does.
    private static PilotViewMode? Named(string s, PilotViewMode mode) =>
        string.Equals(s, Name(mode), StringComparison.OrdinalIgnoreCase) ? mode : null;
}
