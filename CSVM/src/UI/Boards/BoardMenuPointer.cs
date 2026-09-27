using System;

namespace CSVM.UI.Boards;

/// <summary>
/// The menu owner's pointer over a <see cref="BoardMenu"/>, sharing the cursor the pad and the
/// keyboard drive. Entering a row moves the cursor onto it, and a press takes hold of the row it
/// lands on. That row fires through the menu's own confirm when the button comes up still on it, so
/// a press let go anywhere else fires nothing. A pointer resting on a row leaves the pad free to move
/// the cursor away. Engine-free: the board hands it one frame's pointer and its own hit test, which
/// is how both pause boards keep one rule.
/// </summary>
public sealed class BoardMenuPointer
{
    private int _armed = -1;
    private int _over = -1;
    private bool _wasPressed;
    private bool _settling;

    /// <summary>Whether the first pointer a fresh board sees only marks the row it rests on, rather
    /// than entering it. ⚠ Keep it set on a board whose rows stand at the window's middle. A flight's
    /// released capture leaves the pointer there, and the cursor must still start on the first row.</summary>
    public bool SettlesFirstSight { get; init; }

    /// <summary>Where the pointer stood last frame, in the board's own space, or null while nobody
    /// is pointing at the board.</summary>
    public (float X, float Y)? At { get; private set; }

    /// <summary>Whether the row a press took hold of is drawing held: the button is still down and
    /// the pointer is still on that row.</summary>
    public bool Held { get; private set; }

    /// <summary>Forgets the pointer and any row a press had taken hold of, and reads the button as
    /// it stands. ⚠ Call it whenever the board appears or returns from a screen that stood over it.
    /// Otherwise a button still down from that screen's last click reads as a fresh press here.</summary>
    public void Prime(bool pressed)
    {
        At = null;
        _armed = -1;
        _over = -1;
        Held = false;
        _wasPressed = pressed;
        _settling = SettlesFirstSight;
    }

    /// <summary>One frame of the pointer over <paramref name="menu"/>'s rows: null for no pointer
    /// this frame, and <paramref name="rowAt"/> answers the row at a point, -1 for none. Answers
    /// whether the board should repaint.</summary>
    public bool Step(BoardMenu menu, (float X, float Y, bool Pressed)? pointer, Func<float, float, int> rowAt)
    {
        if (pointer is not { } at)
        {
            bool had = At != null || Held;
            At = null;
            _armed = -1;
            _over = -1;
            Held = false;
            return had;
        }

        bool changed = At != (at.X, at.Y);
        At = (at.X, at.Y);
        int over = rowAt(at.X, at.Y);
        if (_settling)
        {
            _settling = false;
            _over = over;
        }
        else if (over != _over)
        {
            _over = over;
            changed |= menu.MoveTo(over);
        }

        bool clicked = at.Pressed && !_wasPressed;
        bool released = !at.Pressed && _wasPressed;
        _wasPressed = at.Pressed;
        if (clicked && over >= 0)
        {
            _armed = over;
            changed |= menu.MoveTo(over);
        }

        bool held = _armed >= 0 && at.Pressed && over == _armed;
        changed |= held != Held;
        Held = held;
        if (!released)
        {
            return changed;
        }

        bool fires = _armed >= 0 && over == _armed;
        _armed = -1;
        if (!fires)
        {
            return changed;
        }

        // The pad may have moved the cursor while the button was down, so it goes back onto this
        // row first. The shared confirm then fires it, one path for the pad and the pointer alike.
        menu.MoveTo(over);
        menu.Handle(0, accept: true, back: false);
        return true;
    }
}
