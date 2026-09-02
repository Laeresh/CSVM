namespace CSVM.Bindings;

/// <summary>Which set of controls a seat is reading at the moment. A seat holds one
/// <see cref="ActionMap"/> per context rather than one map overall, because the shipped keymap gives
/// one control different meanings in different modes: <c>W</c> pitches the nose down in flight,
/// moves a menu cursor up on a board, and flies the spectator camera forward. A single map cannot
/// hold that, since a control belongs to at most one action there (<see cref="ActionMap.Assign"/>).
/// The steal rule therefore runs inside a context, which is also the scope a rebinding screen edits:
/// a conflict is two flight actions wanting one button, not flight and menu sharing it.</summary>
public enum InputContext
{
    /// <summary>Flying: attitude, throttle, weapons, targeting, the views and the head.</summary>
    Flight,

    /// <summary>Menus and boards: the cursor, accept and back, and the screen-specific buttons.
    /// </summary>
    Menu,

    /// <summary>The free-flying spectator and anim-lab camera.</summary>
    Camera,
}
