using System;

namespace CSVM.Bindings;

/// <summary>Which of the four control shapes a <see cref="BindingControl"/> carries. The tag is
/// the discriminator: the numeric members mean different things per kind and only the ones the
/// kind names are meaningful.</summary>
public enum ControlKind
{
    Key,
    Button,
    Axis,
    Hat,
    Mouse,
}

/// <summary>The four directions of one hat, as flags so a device can report several at once (a
/// diagonal is Up and Right together). A binding names exactly one of them.</summary>
[Flags]
public enum HatDirection
{
    None = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8,
}

/// <summary>One control on a device: a key, a button, a mouse button, one direction of one axis
/// past a deadzone, or one direction of a hat. The tagged shape is deliberate. Four typed slots is
/// what the original ships (`docs/org/input.md`), and it is why an axis cannot be bound there at
/// all.
/// ⚠ Build one through the five factories, never through the default value: they are the only
/// place the per-kind invariants (a sign of exactly ±1, a deadzone under 1, exactly one hat
/// direction) are enforced.</summary>
public readonly record struct BindingControl
{
    private BindingControl(ControlKind kind, int index, int sign, float deadzone, HatDirection direction)
    {
        Kind = kind;
        Index = index;
        Sign = sign;
        Deadzone = deadzone;
        Direction = direction;
    }

    public ControlKind Kind { get; }

    /// <summary>The key code, button index, axis index or hat index, per <see cref="Kind"/>. A key
    /// code is the engine's own key constant, not a DirectInput scancode.</summary>
    public int Index { get; }

    /// <summary>Which way the axis has to move, +1 or -1. Zero for every other kind, since a
    /// binding on a key or a button has no direction to choose.</summary>
    public int Sign { get; }

    /// <summary>How far past centre the axis has to travel before it resolves at all, in [0, 1).
    /// It is both the noise gate and the digital threshold; the model does not carry a second
    /// number for the two jobs.</summary>
    public float Deadzone { get; }

    /// <summary>The one hat direction this binding watches. <c>None</c> for every other kind.
    /// </summary>
    public HatDirection Direction { get; }

    public static BindingControl Key(int keyCode) =>
        new(ControlKind.Key, NonNegative(keyCode, nameof(keyCode)), 0, 0f, HatDirection.None);

    public static BindingControl Button(int index) =>
        new(ControlKind.Button, NonNegative(index, nameof(index)), 0, 0f, HatDirection.None);

    /// <summary>One mouse button, held. The original carries one in bits 26-27 of every command word
    /// (`FUN_00537150`, `docs/org/input.md`), the one input this model dropped until this factory
    /// existed.</summary>
    public static BindingControl Mouse(int index) =>
        new(ControlKind.Mouse, NonNegative(index, nameof(index)), 0, 0f, HatDirection.None);

    /// <summary>One direction of one axis. The sign picks the half of the travel that fires, so a
    /// stick's two ends are two bindings and can drive two different actions.</summary>
    public static BindingControl Axis(int index, int sign, float deadzone)
    {
        if (sign != 1 && sign != -1)
            throw new ArgumentOutOfRangeException(nameof(sign), sign, "An axis binding needs a sign of +1 or -1.");
        if (!(deadzone >= 0f) || deadzone >= 1f)
            throw new ArgumentOutOfRangeException(nameof(deadzone), deadzone, "A deadzone lies in [0, 1).");
        return new BindingControl(ControlKind.Axis, NonNegative(index, nameof(index)), sign, deadzone, HatDirection.None);
    }

    /// <summary>One direction of one hat. Exactly one direction, because a binding on a diagonal
    /// would be a second, hidden combining rule beside the one the binding list already has.
    /// ⚠ Do not author one on this backend. Godot reports a d-pad as four buttons, so a hat binding
    /// aliases a button binding that <see cref="ActionMap.SameControl"/> reads as different, and two
    /// actions could then hold one d-pad direction (`DefaultBindings`).</summary>
    public static BindingControl Hat(int index, HatDirection direction)
    {
        if (direction is not (HatDirection.Up or HatDirection.Right or HatDirection.Down or HatDirection.Left))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "A hat binding names exactly one direction.");
        return new BindingControl(ControlKind.Hat, NonNegative(index, nameof(index)), 0, 0f, direction);
    }

    public override string ToString() => Kind switch
    {
        ControlKind.Key => $"key:{Index}",
        ControlKind.Button => $"button:{Index}",
        ControlKind.Axis => $"axis:{Index}{(Sign < 0 ? "-" : "+")}@{Deadzone:0.###}",
        ControlKind.Mouse => $"mouse:{Index}",
        _ => $"hat:{Index}:{Direction}",
    };

    private static int NonNegative(int value, string name) =>
        value >= 0 ? value : throw new ArgumentOutOfRangeException(name, value, "A control index is never negative.");
}
