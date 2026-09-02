namespace CSVM.Bindings;

/// <summary>This tick's raw hardware state, addressed by device identity rather than by connection
/// index. A binding resolves through one of these and touches no engine call itself, which is what
/// makes the whole model testable without a running engine.
/// ⚠ Every member answers for an unknown or absent device rather than throwing: false, zero and
/// <see cref="HatDirection.None"/>. A player who unplugs a pad keeps their bindings and the
/// actions on it go quiet, instead of the map losing the rows.</summary>
public interface IDeviceState
{
    /// <summary>Whether that key is held. The code is the engine's own key constant.</summary>
    bool IsKeyDown(DeviceId device, int keyCode);

    /// <summary>Whether that button is held.</summary>
    bool IsButtonDown(DeviceId device, int button);

    /// <summary>The axis at rest-relative travel in [-1, 1], with no deadzone applied: the deadzone
    /// belongs to the binding, so two bindings can gate the same axis differently.</summary>
    float AxisValue(DeviceId device, int axis);

    /// <summary>Every direction that hat currently reports, which is two of them on a diagonal.
    /// </summary>
    HatDirection HatState(DeviceId device, int hat);
}
