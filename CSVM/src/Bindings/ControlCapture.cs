using System.Collections.Generic;
using Godot;

namespace CSVM.Bindings;

/// <summary>The controls a rebinding screen can capture, and the scan that turns a press into a
/// <see cref="Binding"/>. It reads the seat's own <see cref="IDeviceState"/>, so a captured pad
/// control carries the identity that seat's map is authored on rather than a hardware GUID the
/// seat would never resolve: a seat reads a SET of pads through a placeholder
/// (<see cref="DefaultBindings.AnyPad"/> and its siblings), and a real GUID beside a placeholder
/// row would be two controls to <see cref="ActionMap.SameControl"/> and one to the player.
/// ⚠ Axes and hats are deliberately not scanned. A resting stick drifts, so an axis needs a
/// movement rule rather than a threshold, and no hat may be authored on this backend at all
/// (<see cref="BindingControl.Hat"/>).</summary>
public sealed class ControlCapture
{
    /// <summary>The key that cancels a capture instead of being captured. A screen with no way out
    /// is worse than an unbindable Escape, and Escape keeps its shipped bindings either way.
    /// </summary>
    public const Key CancelKey = Key.Escape;

    /// <summary>The pad button that cancels, on the same reasoning as <see cref="CancelKey"/>. It
    /// is the seat's own Back, so the gesture is the one every other screen uses.</summary>
    public const JoyButton CancelButton = JoyButton.B;

    // Every key a player may bind: the letters and digits, F1-F12, the numpad, the arrows, the
    // editing and modifier keys, and the punctuation the shipped defaults already use. F13 upward
    // is left out on purpose, because that block is the debug overlays' (docs/controls.md) and a
    // player action landing there would fight a diagnostic.
    private static readonly Key[] Keys = BuildKeys();

    // Godot's SDL button range, which is every button it can report. Guide is included: a pad that
    // exposes it can bind it, and one that does not never reports it.
    private static readonly JoyButton[] Buttons = BuildButtons();

    // The mouse buttons past the pointer's own. Left is left out because it is how a pointer-driven
    // presentation confirms the very row being rebound.
    private static readonly MouseButton[] MouseButtons =
    {
        MouseButton.Right, MouseButton.Middle,
    };

    private readonly HashSet<int> _maskedKeys = new();
    private readonly HashSet<int> _maskedButtons = new();
    private readonly HashSet<int> _maskedMouse = new();

    private readonly DeviceId _pad;
    private readonly bool _readsKeyboard;

    /// <summary>A capture for one seat: <paramref name="pad"/> is the identity that seat's pad
    /// bindings sit on, and <paramref name="readsKeyboard"/> is false for a pad-only splitscreen
    /// seat, which must not bind the one shared keyboard.</summary>
    public ControlCapture(DeviceId pad, bool readsKeyboard)
    {
        _pad = pad;
        _readsKeyboard = readsKeyboard;
    }

    /// <summary>Masks everything currently held, so a control still down from the press that opened
    /// the capture is not read as the player's choice. A masked control has to be released before
    /// it can be captured.</summary>
    public void Arm(IDeviceState state)
    {
        _maskedKeys.Clear();
        _maskedButtons.Clear();
        _maskedMouse.Clear();
        foreach (var key in Keys)
        {
            if (KeyDown(state, key))
                _maskedKeys.Add((int)key);
        }

        foreach (var button in Buttons)
        {
            if (state.IsButtonDown(_pad, (int)button))
                _maskedButtons.Add((int)button);
        }

        foreach (var button in MouseButtons)
        {
            if (MouseDown(state, button))
                _maskedMouse.Add((int)button);
        }
    }

    /// <summary>Whether the player is asking to abandon the capture rather than to bind something.
    /// Checked once a frame before <see cref="Poll"/>, since neither cancel control is ever
    /// captured and both are masked by <see cref="Arm"/> like any other.</summary>
    public bool Cancelled(IDeviceState state)
    {
        bool key = Fresh(_maskedKeys, (int)CancelKey, KeyDown(state, CancelKey));
        bool button = Fresh(_maskedButtons, (int)CancelButton, state.IsButtonDown(_pad, (int)CancelButton));
        return key || button;
    }

    /// <summary>The control the player pressed since <see cref="Arm"/>, or null while none has
    /// been. Keys first, then pad buttons, then the mouse, so a frame holding several answers the
    /// same way twice.</summary>
    public Binding? Poll(IDeviceState state)
    {
        foreach (var key in Keys)
        {
            if (key != CancelKey && Fresh(_maskedKeys, (int)key, KeyDown(state, key)))
                return new Binding(DeviceId.Keyboard, BindingControl.Key((int)key));
        }

        foreach (var button in Buttons)
        {
            if (button != CancelButton
                && Fresh(_maskedButtons, (int)button, state.IsButtonDown(_pad, (int)button)))
                return new Binding(_pad, BindingControl.Button((int)button));
        }

        foreach (var button in MouseButtons)
        {
            if (Fresh(_maskedMouse, (int)button, MouseDown(state, button)))
                return new Binding(DeviceId.Mouse, BindingControl.Mouse((int)button));
        }

        return null;
    }

    private static bool Fresh(HashSet<int> masked, int index, bool down)
    {
        if (!down)
        {
            masked.Remove(index);
            return false;
        }

        return !masked.Contains(index);
    }

    private static Key[] BuildKeys()
    {
        var keys = new List<Key>();
        for (var k = Key.A; k <= Key.Z; k++)
            keys.Add(k);
        for (var k = Key.Key0; k <= Key.Key9; k++)
            keys.Add(k);
        for (var k = Key.F1; k <= Key.F12; k++)
            keys.Add(k);
        for (var k = Key.Kp0; k <= Key.Kp9; k++)
            keys.Add(k);
        keys.AddRange(new[]
        {
            Key.KpAdd, Key.KpSubtract, Key.KpMultiply, Key.KpDivide, Key.KpPeriod, Key.KpEnter,
            Key.Up, Key.Down, Key.Left, Key.Right,
            Key.Space, Key.Enter, Key.Tab, Key.Backspace,
            Key.Shift, Key.Ctrl, Key.Alt, Key.Escape,
            Key.Comma, Key.Period, Key.Slash, Key.Backslash, Key.Semicolon, Key.Apostrophe,
            Key.Bracketleft, Key.Bracketright, Key.Minus, Key.Equal, Key.Quoteleft,
        });
        return keys.ToArray();
    }

    private static JoyButton[] BuildButtons()
    {
        var buttons = new List<JoyButton>();
        for (int i = 0; i < (int)JoyButton.SdlMax; i++)
            buttons.Add((JoyButton)i);
        return buttons.ToArray();
    }

    private bool KeyDown(IDeviceState state, Key key) =>
        _readsKeyboard && state.IsKeyDown(DeviceId.Keyboard, (int)key);

    private bool MouseDown(IDeviceState state, MouseButton button) =>
        _readsKeyboard && state.IsMouseButtonDown(DeviceId.Mouse, (int)button);
}
