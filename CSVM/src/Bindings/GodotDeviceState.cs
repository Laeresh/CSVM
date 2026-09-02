using Godot;

namespace CSVM.Bindings;

/// <summary>The live <see cref="IDeviceState"/> over Godot's <c>Input</c> singleton. A binding
/// carries a stable <see cref="DeviceId"/>, never a connection index, so every read goes through
/// the owned <see cref="DeviceRegistry"/> first; an identity the registry cannot resolve this tick
/// answers false, zero or <see cref="HatDirection.None"/> rather than throwing, per the interface's
/// contract. Godot has no raw hat API: a controller's d-pad arrives as four <see cref="JoyButton"/>
/// values, so hat index 0 is that d-pad and every other hat index resolves
/// <see cref="HatDirection.None"/>.</summary>
public sealed class GodotDeviceState : IDeviceState
{
    private readonly DeviceRegistry _registry;

    public GodotDeviceState(DeviceRegistry registry) => _registry = registry;

    /// <summary>Refreshes the owned registry from this tick's connected pads. Call once at launch
    /// and again on every <c>joy_connection_changed</c> signal (<c>Input.Singleton</c> raises it);
    /// this is the whole hot-plug wiring, kept out of the constructor so a caller controls when the
    /// first read happens.</summary>
    public void RefreshDevices()
    {
        var connected = new System.Collections.Generic.List<(int, string, string)>();
        foreach (int index in Input.GetConnectedJoypads())
            connected.Add((index, Input.GetJoyGuid(index), Input.GetJoyName(index)));
        _registry.Refresh(connected);
    }

    public bool IsKeyDown(DeviceId device, int keyCode) =>
        device.Kind == DeviceKind.Keyboard && Input.IsKeyPressed((Key)keyCode);

    public bool IsButtonDown(DeviceId device, int button) =>
        _registry.IndexOf(device) is { } index && Input.IsJoyButtonPressed(index, (JoyButton)button);

    public bool IsMouseButtonDown(DeviceId device, int button) =>
        device.Kind == DeviceKind.Mouse && Input.IsMouseButtonPressed((MouseButton)button);

    public float AxisValue(DeviceId device, int axis) =>
        _registry.IndexOf(device) is { } index ? Input.GetJoyAxis(index, (JoyAxis)axis) : 0f;

    public HatDirection HatState(DeviceId device, int hat)
    {
        if (hat != 0 || _registry.IndexOf(device) is not { } index)
            return HatDirection.None;
        var state = HatDirection.None;
        if (Input.IsJoyButtonPressed(index, JoyButton.DpadUp)) state |= HatDirection.Up;
        if (Input.IsJoyButtonPressed(index, JoyButton.DpadRight)) state |= HatDirection.Right;
        if (Input.IsJoyButtonPressed(index, JoyButton.DpadDown)) state |= HatDirection.Down;
        if (Input.IsJoyButtonPressed(index, JoyButton.DpadLeft)) state |= HatDirection.Left;
        return state;
    }
}
