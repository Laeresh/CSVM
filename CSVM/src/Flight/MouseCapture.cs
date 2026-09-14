using Godot;

namespace CSVM.Flight;

/// <summary>The mouse a flight seat takes from the desktop, and the virtual cursor that stands in
/// for the OS one while it holds it. A captured pointer stops reporting a position, so the absolute
/// reads the stick and head-look grew up on are fed from relative motion instead: this accumulates
/// that motion into a cursor confined to the pane, which the stick then reads exactly as it read the
/// real one, dead bands and all. Pure arithmetic with no device and no display in it, so a unit
/// drives the whole law; <see cref="FlightController"/> owns the mode write and the release.
/// </summary>
public sealed class MouseCapture
{
    private Vector2 _pendingCursor;
    private Vector2 _pendingLook;

    /// <summary>Whether the mouse is held right now.</summary>
    public bool Holding { get; private set; }

    /// <summary>Where the virtual cursor stands in the pane, in pane pixels, meaningless while
    /// nothing is held.</summary>
    public Vector2 Cursor { get; private set; }

    /// <summary>Whether a session may take the mouse at all. ⚠ Guarded on a real display and on
    /// nobody being at the controls: the hidden test desktop and every <c>--det</c> run must leave
    /// the mouse mode exactly as the harness set it, or a suite reads a pointer its own launch moved.
    /// <paramref name="scripted"/> is <c>SessionSpec.IsScripted</c>, which covers <c>--run-tests</c>,
    /// the screenshot goldens and every <c>--dump-*</c> report.</summary>
    public static bool Allowed(bool realDisplay, bool det, bool scripted) =>
        realDisplay && !det && !scripted;

    /// <summary>The mode a board that swapped in its own drawn pointer puts back on its close.
    /// ⚠ Never <c>Captured</c>: the pause sheet opens inside the seat's own poll, a frame before the
    /// seat lets go, so it saves the seat's capture, and its close runs at the session's deferred free,
    /// after the menu is already up. Restoring that capture leaves a menu nobody can point at. A resume
    /// needs no capture restored, since the seat takes it again on its next unhalted frame.</summary>
    public static Input.MouseModeEnum Restorable(Input.MouseModeEnum saved) =>
        saved == Input.MouseModeEnum.Captured ? Input.MouseModeEnum.Visible : saved;

    /// <summary>Takes the mouse, seeding the virtual cursor where the real one stood, so the stick
    /// reads the same deflection on the frame the capture starts as on the frame before it.</summary>
    public void Take(Vector2 cursor)
    {
        Cursor = cursor;
        _pendingCursor = Vector2.Zero;
        _pendingLook = Vector2.Zero;
        Holding = true;
    }

    /// <summary>Gives the mouse back. The pending travel goes with it, so a later capture starts
    /// from where the real cursor then is rather than replaying a release's last motion.</summary>
    public void Release()
    {
        Holding = false;
        _pendingCursor = Vector2.Zero;
        _pendingLook = Vector2.Zero;
    }

    /// <summary>One motion event's relative travel, banked for both consumers. Ignored while nothing
    /// is held, which is what keeps an uncaptured session reading byte for byte as before.</summary>
    public void Moved(Vector2 relative)
    {
        if (!Holding)
            return;
        _pendingCursor += relative;
        _pendingLook += relative;
    }

    /// <summary>Folds this frame's travel into the virtual cursor and returns where it now stands.
    /// Confined to <paramref name="pane"/> the way the OS pointer was confined to the window, so
    /// pushing past an edge saturates there instead of running up a debt to travel back.</summary>
    public Vector2 StepCursor(Vector2 pane)
    {
        Cursor = new Vector2(
            Mathf.Clamp(Cursor.X + _pendingCursor.X, 0f, Mathf.Max(pane.X, 0f)),
            Mathf.Clamp(Cursor.Y + _pendingCursor.Y, 0f, Mathf.Max(pane.Y, 0f)));
        _pendingCursor = Vector2.Zero;
        return Cursor;
    }

    /// <summary>The travel since the last read, for head-look's relative law. Consumed on every
    /// call, so a frame nobody looks on banks nothing towards the next one.</summary>
    public Vector2 TakeLook()
    {
        var look = _pendingLook;
        _pendingLook = Vector2.Zero;
        return look;
    }
}
