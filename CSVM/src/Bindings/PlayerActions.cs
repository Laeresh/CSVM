namespace CSVM.Bindings;

/// <summary>One player's actions as a polling site sees them: a map, the tick's snapshot, and the
/// keyboard gate. Call <see cref="Poll"/> once per tick and then <see cref="Held"/> as often as the
/// consumer likes, which is the same shape a direct key poll has and keeps the sim a level read.
/// ⚠ <see cref="ReadsKeyboard"/> is the existing player-1-only keyboard rule, kept here rather than
/// in the map: splitscreen players 2 to 4 share the shipped defaults and must still be pad-only, so
/// the gate belongs to the seat and not to the bindings. Turning an identity into live hardware is
/// the device registry's job; this type only reads the state it is handed.</summary>
public sealed class PlayerActions
{
    private readonly ActionSnapshot _snapshot = new();
    private readonly MutedKeyboard _muted = new();

    public PlayerActions()
        : this(new ActionMap(), true)
    {
    }

    public PlayerActions(ActionMap map, bool readsKeyboard)
    {
        Map = map;
        ReadsKeyboard = readsKeyboard;
    }

    /// <summary>This player's keymap, editable in place by a rebinding screen.</summary>
    public ActionMap Map { get; }

    /// <summary>Whether this seat reads the keyboard at all. False for a pad-only splitscreen
    /// player, whose keyboard bindings then resolve false rather than being deleted.</summary>
    public bool ReadsKeyboard { get; set; }

    /// <summary>The tick's resolved values. Reused every <see cref="Poll"/>, never a copy.
    /// </summary>
    public ActionSnapshot Current => _snapshot;

    /// <summary>Resolves every bound action out of this tick's hardware. The one place the map
    /// touches device state, so a consumer's reads cost nothing and cannot drift apart.</summary>
    public void Poll(IDeviceState state)
    {
        if (!ReadsKeyboard)
        {
            _muted.Source = state;
            state = _muted;
        }

        Map.ResolveInto(_snapshot, state);
        _muted.Source = null;
    }

    public bool Held(InputAction action) => _snapshot.Held(action);

    public float Value(InputAction action) => _snapshot.Value(action);

    public float Axis(InputAction positive, InputAction negative) => _snapshot.Axis(positive, negative);

    // The pad-only seat's view of the tick: every keyboard device reads as idle, every other device
    // passes straight through. A filter rather than a second state, so the registry stays the only
    // thing that talks to hardware.
    private sealed class MutedKeyboard : IDeviceState
    {
        public IDeviceState? Source { get; set; }

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            !Silent(device) && Source!.IsKeyDown(device, keyCode);

        public bool IsButtonDown(DeviceId device, int button) =>
            !Silent(device) && Source!.IsButtonDown(device, button);

        public float AxisValue(DeviceId device, int axis) =>
            Silent(device) ? 0f : Source!.AxisValue(device, axis);

        public HatDirection HatState(DeviceId device, int hat) =>
            Silent(device) ? HatDirection.None : Source!.HatState(device, hat);

        private static bool Silent(DeviceId device) => device.Kind == DeviceKind.Keyboard;
    }
}
