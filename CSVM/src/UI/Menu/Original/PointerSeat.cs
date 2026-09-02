using System;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Seat 0 with a pointer: wraps the seat that already polls the keyboard and the unclaimed pads
/// and adds the mouse as this frame's <see cref="MenuPointer"/>, in window pixels, with the
/// click as a press edge. The two device reads are injected, so the seat itself is engine-free
/// and a test can move the pointer by hand. A presentation that reads no pointer ignores the
/// field; the Original presentation maps it into its authored space.
/// </summary>
public sealed class PointerSeat : IMenuInputSource
{
    private readonly IMenuInputSource _inner;
    private readonly Func<(float X, float Y)?> _position;
    private readonly Func<bool> _pressed;
    private bool _wasPressed;

    /// <summary>A seat over <paramref name="inner"/>, reading the pointer's window position from
    /// <paramref name="position"/> (null for no pointer this frame) and its primary button from
    /// <paramref name="pressed"/>.</summary>
    public PointerSeat(IMenuInputSource inner, Func<(float X, float Y)?> position, Func<bool> pressed)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _position = position ?? throw new ArgumentNullException(nameof(position));
        _pressed = pressed ?? throw new ArgumentNullException(nameof(pressed));
    }

    /// <summary>The wrapped seat, for the owner that still binds its devices.</summary>
    public IMenuInputSource Inner => _inner;

    public string DeviceLabel => _inner.DeviceLabel;

    public bool CapturingText
    {
        get => _inner.CapturingText;
        set => _inner.CapturingText = value;
    }

    public MenuCommands Poll(float dt)
    {
        var frame = _inner.Poll(dt);
        if (_position() is not { } at)
        {
            _wasPressed = _pressed();
            return frame;
        }

        bool pressed = _pressed();
        bool clicked = pressed && !_wasPressed;
        _wasPressed = pressed;
        return frame with { Pointer = new MenuPointer(at.X, at.Y, pressed, clicked) };
    }

    /// <summary>Primes the wrapped seat and reads the button, so a click still held from whatever
    /// showed the menu is not a fresh click on the next frame.</summary>
    public void Prime()
    {
        _inner.Prime();
        _wasPressed = _pressed();
    }
}
