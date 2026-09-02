using System;

namespace CSVM.Bindings;

/// <summary>What every action reads on one tick, resolved once and then read as often as anyone
/// likes. It exists so two consumers polling the same action in one tick get the same answer, which
/// a direct hardware read cannot promise: a button released between two reads would fire one site
/// and not the other.
/// ⚠ It is not an event queue and holds no history. There is no went-down and no went-up here;
/// edge detection stays in the consumer that already owns its own previous-frame slot, because the
/// sim is a level-read on a fixed tick and scripted runs depend on that.
/// The instance is reused every tick, so keeping a reference does not freeze the values.</summary>
public sealed class ActionSnapshot
{
    private static readonly int Count = Enum.GetValues(typeof(InputAction)).Length;

    private readonly bool[] _held = new bool[Count];
    private readonly float[] _value = new float[Count];

    /// <summary>Whether the action is held, which is the read that replaces a key poll.</summary>
    public bool Held(InputAction action) => _held[(int)action];

    /// <summary>How far the action is deflected, in [0, 1]. A button reads 1 while held, an axis
    /// its travel past the deadzone.</summary>
    public float Value(InputAction action) => _value[(int)action];

    /// <summary>The two halves of a control axis as one signed number, the shape a stick or a
    /// key pair feeds a flight input. Both ends held reads zero, as a key pair does today.</summary>
    public float Axis(InputAction positive, InputAction negative) =>
        _value[(int)positive] - _value[(int)negative];

    internal void Reset()
    {
        Array.Clear(_held, 0, _held.Length);
        Array.Clear(_value, 0, _value.Length);
    }

    internal void Store(InputAction action, ControlValue read)
    {
        _held[(int)action] = read.Pressed;
        _value[(int)action] = read.Value;
    }
}
