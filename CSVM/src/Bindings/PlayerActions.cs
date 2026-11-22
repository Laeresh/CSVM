using System;
using CSVM.Utils;

namespace CSVM.Bindings;

/// <summary>One player's actions as a polling site sees them: a map, the tick's snapshot, and the
/// keyboard gate. Call <see cref="Poll"/> once per tick and then <see cref="Held"/> as often as the
/// consumer likes, which is the same shape a direct key poll has and keeps the sim a level read.
/// ⚠ <see cref="ReadsKeyboard"/> is the existing player-1-only keyboard rule, kept here rather than
/// in the map: splitscreen players 2 to 4 share the shipped defaults and must still be pad-only, so
/// the gate belongs to the seat and not to the bindings. Turning an identity into live hardware is
/// the device registry's job; this type only reads the state it is handed.
/// Several of these over one <see cref="ActionMap"/> is the supported shape where the keyboard and
/// pad halves of an action are processed apart and then summed rather than ORed. Give each reader
/// the half it owns, through this gate or a pad-muted <see cref="SeatDeviceState"/>.</summary>
public sealed class PlayerActions
{
    private static readonly InputAction[] Actions = Enum.GetValues<InputAction>();

    private readonly ActionSnapshot _snapshot = new();
    private readonly MutedKeyboard _muted = new();

    // The actions down as the load gate opened, read released until each is let go. A throttle or
    // trigger held through the load then does nothing on the first flying frame.
    private readonly HandoverLatch _heldThrough = new();

    // Built or polled while the gate was closed: the first poll after it opens arms the latch.
    private bool _behindGate = LoadGate.Closed;

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

    /// <summary>Whether this seat reads the keyboard and mouse at all. False for a pad-only
    /// splitscreen player, whose keyboard and mouse bindings then resolve false rather than being
    /// deleted.</summary>
    public bool ReadsKeyboard { get; set; }

    /// <summary>The tick's resolved values. Reused every <see cref="Poll"/>, never a copy.
    /// </summary>
    public ActionSnapshot Current => _snapshot;

    /// <summary>Resolves every bound action out of this tick's hardware. The one place the map
    /// touches device state, so a consumer's reads cost nothing and cannot drift apart. Nothing
    /// reads while <see cref="LoadGate"/> is closed, and a control held through it reads released
    /// until it is let go.</summary>
    public void Poll(IDeviceState state)
    {
        if (LoadGate.Closed)
        {
            _behindGate = true;
            _snapshot.Reset();
            return;
        }

        if (!ReadsKeyboard)
        {
            _muted.Source = state;
            state = _muted;
        }

        Map.ResolveInto(_snapshot, state);
        _muted.Source = null;
        ReleaseHeldThrough();
    }

    public bool Held(InputAction action) => _snapshot.Held(action);

    public float Value(InputAction action) => _snapshot.Value(action);

    public float Axis(InputAction positive, InputAction negative) => _snapshot.Axis(positive, negative);

    // ⚠ Never hold the absolute lever through. It rests anywhere on its travel, and LeverTakeover
    // already waits for it to move. Down is PressTravel, not any travel: the pad axes bind with no
    // deadzone, so a resting stick's drift would otherwise stay held through until pushed.
    private void ReleaseHeldThrough()
    {
        if (_behindGate)
        {
            _behindGate = false;
            foreach (var action in Actions)
            {
                if (!AxisPairs.IsAbsolute(action))
                    _heldThrough.Arm(action);
            }
        }

        if (!_heldThrough.Any)
            return;
        foreach (var action in Actions)
        {
            bool down = _snapshot.Value(action) >= ActiveDevice.PressTravel;
            if (!_heldThrough.Read(action, down) && down)
                _snapshot.Store(action, ControlValue.Digital(false));
        }
    }

    // The pad-only seat's view of the tick: keyboard and mouse read as idle, every other device
    // passes straight through. A filter rather than a second state, so the registry stays the only
    // thing that talks to hardware. Mouse is silenced alongside the keyboard because both name the
    // one desktop player's own hardware, the same reasoning DeviceId gives them a singleton identity.
    private sealed class MutedKeyboard : IDeviceState
    {
        public IDeviceState? Source { get; set; }

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            !Silent(device) && Source!.IsKeyDown(device, keyCode);

        public bool IsButtonDown(DeviceId device, int button) =>
            !Silent(device) && Source!.IsButtonDown(device, button);

        public bool IsMouseButtonDown(DeviceId device, int button) =>
            !Silent(device) && Source!.IsMouseButtonDown(device, button);

        public float AxisValue(DeviceId device, int axis) =>
            Silent(device) ? 0f : Source!.AxisValue(device, axis);

        public HatDirection HatState(DeviceId device, int hat) =>
            Silent(device) ? HatDirection.None : Source!.HatState(device, hat);

        private static bool Silent(DeviceId device) =>
            device.Kind == DeviceKind.Keyboard || device.Kind == DeviceKind.Mouse;
    }
}
