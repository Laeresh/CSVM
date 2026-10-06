using System;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The one pointer every polled reader takes: the desktop mouse, or the theater screen's laser
/// pointer while one stands in for it (<see cref="TheaterPointer"/>). A stereo root lays its 2D out
/// at the eye's size, where the desktop cursor cannot reach. So the readers that poll a position
/// rather than take mouse events read here, and event readers get the laser's clicks as pushed mouse
/// events. Module entry: docs/architecture/UI.md.
/// ⚠ With no override set every read is the desktop mouse's, call for call, so flat play reads
/// exactly what it read before.
/// </summary>
public static class ScreenPointer
{
    /// <summary>The pointer standing in for the desktop mouse, or null for the mouse itself. Its
    /// reading is in the root's 2D layout pixels, with the primary button; null aims off the screen.
    /// </summary>
    public static Func<(Vector2 At, bool Pressed)?>? Override { get; set; }

    /// <summary>Whether an override stands in for the mouse, and its reading if so. A reader
    /// keeps its own desktop read for the false case, untouched.</summary>
    public static bool Overrides(out (Vector2 At, bool Pressed)? reading)
    {
        if (Override is { } source)
        {
            reading = source();
            return true;
        }

        reading = null;
        return false;
    }

    /// <summary>The pointer over <paramref name="viewport"/> and whether its primary button is
    /// down: the override's reading, else the mouse's viewport position and left button.</summary>
    public static (Vector2 At, bool Pressed)? Read(Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        return Overrides(out var reading)
            ? reading
            : (viewport.GetMousePosition(), Input.IsMouseButtonPressed(MouseButton.Left));
    }
}
