namespace CSVM.Bindings;

/// <summary>What a control reads this tick, as both answers at once: held, and how far. A button
/// resolves 1, an axis resolves its travel past the deadzone rescaled onto [0, 1], so an axis can
/// drive an action written as digital and a button can drive one written as analogue. The original
/// cannot express either direction (`docs/org/input.md`), which is the reason the pair is here.
/// <see cref="Pressed"/> holds exactly when <see cref="Value"/> is above zero.</summary>
public readonly record struct ControlValue(bool Pressed, float Value)
{
    /// <summary>Nothing held, nothing deflected: what an unbound or absent control reads.</summary>
    public static ControlValue None => default;

    /// <summary>A key, a button or a hat direction: on is fully on.</summary>
    public static ControlValue Digital(bool pressed) => new(pressed, pressed ? 1f : 0f);

    /// <summary>An axis past its deadzone, the travel already rescaled onto [0, 1].</summary>
    public static ControlValue Analogue(float value) => new(value > 0f, value);
}

/// <summary>One control on one named device: everything needed to read a binding, and nothing
/// about which action holds it. Device identity is part of the value rather than a nullable field
/// beside it, so two pads with the same button layout are two different bindings.</summary>
public readonly record struct Binding(DeviceId Device, BindingControl Control)
{
    /// <summary>What this binding reads from that tick's hardware state. An axis fires strictly
    /// past its deadzone and only on the half of the travel its sign names; a hat direction reads
    /// only its own flag, so the other three directions of the same hat are independent.</summary>
    public ControlValue Resolve(IDeviceState state)
    {
        switch (Control.Kind)
        {
            case ControlKind.Key:
                return ControlValue.Digital(state.IsKeyDown(Device, Control.Index));
            case ControlKind.Button:
                return ControlValue.Digital(state.IsButtonDown(Device, Control.Index));
            case ControlKind.Hat:
                return ControlValue.Digital((state.HatState(Device, Control.Index) & Control.Direction) != 0);
            default:
                float travel = state.AxisValue(Device, Control.Index) * Control.Sign;
                if (travel <= Control.Deadzone)
                    return ControlValue.None;
                float span = 1f - Control.Deadzone;
                float scaled = (travel - Control.Deadzone) / span;
                return ControlValue.Analogue(scaled > 1f ? 1f : scaled);
        }
    }

    public override string ToString() => $"{Device}/{Control}";
}
