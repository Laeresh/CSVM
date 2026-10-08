using CSVM.Flight.Airframe;
using Godot;

namespace CSVM.Flight.Camera;

/// <summary>The desktop mouse one flight seat holds while it flies, and the two readings it gives.
/// One is the cursor offset the mouse-flying stick reads, the other the travel head-look pans by.
/// It owns the engine half <see cref="MouseCapture"/> leaves out: the mouse-mode write and release,
/// the pane it measures, and the desktop pointer read while nothing is held. The host decides each
/// frame whether the seat wants the mouse, <see cref="Allowed"/> first. It hands over its own node,
/// whose viewport is the pane, and the pane is measured only on a frame that needs it.</summary>
public sealed class SeatMouse
{
    private readonly MouseCapture _capture = new();
    private Vector2 _lookPrev;

    /// <summary>Whether this seat may take the desktop mouse at all, under either mouse scheme.
    /// Resolved once per session by <see cref="MouseCapture.Allowed"/>, whose ⚠ says why the answer
    /// is no on the hidden test desktop and in every <c>--det</c> run. False, the default, leaves
    /// <c>Input.MouseMode</c> untouched from first frame to last.</summary>
    public bool Allowed { get; set; }

    /// <summary>The cursor offset <see cref="Stick"/> reads while set, +x right and +y down over the
    /// pane's half extent. A suite can then fly the scheme with no window and no cursor. Null, the
    /// default, reads the seat's real pane.</summary>
    public Vector2? StickForTest { get; set; }

    /// <summary>Whether this seat holds the mouse right now.</summary>
    public bool Holding => _capture.Holding;

    /// <summary>Banks one input event's relative travel. A captured pointer reports nothing else,
    /// and a seat that holds nothing banks nothing.</summary>
    public void Moved(InputEvent @event)
    {
        if (_capture.Holding && @event is InputEventMouseMotion motion)
            _capture.Moved(motion.Relative);
    }

    /// <summary>One frame of the hold: taken on a <paramref name="wanted"/> frame, given back on any
    /// other, and the virtual cursor stepped through the pane while held. A board that draws its own
    /// pointer halts the session, and the halt is what hands the pointer back to it.</summary>
    public void Step(bool wanted, Node seat, float sensitivity)
    {
        if (wanted && !_capture.Holding)
        {
            _capture.Take(PaneOf(seat)?.GetVisibleRect().Size ?? Vector2.Zero);
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (!wanted)
        {
            Release();
        }

        if (_capture.Holding)
            _capture.StepCursor(PaneOf(seat)?.GetVisibleRect().Size ?? Vector2.Zero, sensitivity);
    }

    /// <summary>Puts back only what this seat took. ⚠ Leave a mode alone that a board already
    /// swapped for its own drawn cursor. The release would otherwise show the OS pointer over a
    /// pause sheet.</summary>
    public void Release()
    {
        if (!_capture.Holding)
            return;
        _capture.Release();
        if (Input.MouseMode == Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    /// <summary>Where the cursor stands in <paramref name="seat"/>'s pane as a stick offset, or the
    /// suite's stand-in while one is set. A seat outside the tree has no pane to measure, and reads
    /// centred rather than guessing one.</summary>
    public Vector2 Stick(Node seat)
    {
        if (StickForTest is { } pinned)
            return new Vector2(Mathf.Clamp(pinned.X, -1f, 1f), Mathf.Clamp(pinned.Y, -1f, 1f));
        if (PaneOf(seat) is not { } pane)
            return Vector2.Zero;
        var half = pane.GetVisibleRect().Size * 0.5f;
        // A captured pointer reports one fixed position, so the virtual cursor stands in for it,
        // with its own wider centre band. Off capture this is the pane's own cursor and decoded law.
        if (_capture.Holding)
            return MouseCapture.Centred(MouseFlight.Offset(_capture.Cursor, half, half));
        return MouseFlight.Offset(pane.GetMousePosition(), half, half);
    }

    /// <summary>How far the mouse moved since the last read, or zero unless
    /// <paramref name="freeLookHeld"/>. Polled every frame the head is aimed, so an idle mouse reads
    /// exactly zero. A still mouse under a held control is not the released case. The head tells
    /// those two apart by the held control itself.</summary>
    public Vector2 LookTravel(bool freeLookHeld)
    {
        var pos = (Vector2)DisplayServer.MouseGetPosition();
        var absolute = pos - _lookPrev;
        // Refreshed whether or not it is the reading used. Otherwise the frame capture ends on
        // would hand head-look the whole span the pointer stood still for as one delta.
        _lookPrev = pos;
        var delta = _capture.Holding ? _capture.TakeLook() : absolute;
        return freeLookHeld && delta.LengthSquared() > 1f ? delta : Vector2.Zero;
    }

    private static Viewport? PaneOf(Node seat) => seat.IsInsideTree() ? seat.GetViewport() : null;
}
