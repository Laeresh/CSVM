using System;
using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.Flight.Weapons;
using CSVM.Sticks;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>One seat's controls: its keymap, the readers that resolve it over the seat's keyboard,
/// pads and flight sticks, and what flight reads off them. Those are the discrete commands through the
/// re-entry latch, the weapon selectors and the attitude axes. So are the commanded lever, its
/// takeover and its <c>--lever=</c> schedule, and the weapon lab's orbit swing. It polls at most once
/// per rendered frame. It holds no aircraft state: the live lever, the respawn and the director's hold stay with
/// <see cref="FlightController"/>, which passes what it decides per call.</summary>
internal sealed class SeatControls
{
    private readonly Func<int[]?> _padDevices;
    private readonly Func<int> _localPlayer;
    private readonly Func<int> _playerIndex;
    private readonly Action _promptsMoved;

    // Swallows a discrete flight command's next read when a cutscene skip or a pause-sheet dismiss
    // hands input back. The control that confirmed it may still be down.
    private readonly FlightReentryLatch _reentryLatch = new();

    // The discrete commands read as held last time, so the log carries one line per press.
    private readonly HashSet<InputAction> _pressesLogged = new();

    // This seat's keymap, and several views of it. The halves of an attitude action are processed
    // differently here and then summed. The keys ramp through StickRamp, a pad bends through
    // StickCurve, a flight stick passes linearly. A single OR-ed read cannot express that, so the
    // halves stay separable while the bindings stay shared.
    private readonly BindingProfile _bindings;
    private readonly PlayerActions _actions;     // keyboard, mouse and pad together
    private readonly PlayerActions _keyActions;   // the keyboard and mouse half alone
    private readonly PlayerActions _padActions;   // the pad half alone, flight sticks included

    // The pad half's flight axes, split by device. A pad bends through the pad curve and a flight
    // stick flies linearly (AnalogAxes), so neither reads the other's bindings.
    private readonly PlayerActions _padAxes;
    private readonly PlayerActions _stickAxes;
    private readonly LeverTakeover _leverTakeover = new(); // when a bound throttle lever commands
    private readonly SeatDeviceState _seatState;
    private readonly SeatDeviceState _padMutedState;
    private readonly StickSplit _padsAlone;
    private readonly StickSplit _sticksAlone;

    // The stick half _stickAxes last polled. The lever reads its bindings from it one by one, since a
    // resolved row cannot tell a centred stick from an unplugged one.
    private IDeviceState _stickSide;

    private ulong _inputFrame = ulong.MaxValue;  // the rendered frame the readers above hold
    private int _stickRevision = -1;             // the StickProfiles revision last merged into _bindings
    private float _keyPitch;                     // the three keyboard axes' own deflection, ramped
    private float _keyRoll;                      // by StickRamp; a gamepad's analogue axis adds on
    private float _keyYaw;                       // top and is never ramped

    // The --lever= schedule: commanded-lever presses at their own sim-seconds, the scripted twin of
    // the digit row. It lets a headless capture slam the throttle. Null on every ordinary run.
    private IReadOnlyList<(float At, float Lever)>? _leverSteps;
    private float _leverElapsed;                 // sim-s the keyboard arm has read, for the schedule
    private int _leverNext;                      // the next scheduled press not yet made

    /// <summary>Builds the controls for one seat over its pads, its local player and the index its
    /// log lines name. The last argument runs whenever a binding or the side a prompt names moves.</summary>
    public SeatControls(Func<int[]?> padDevices, Func<int> localPlayer, Func<int> playerIndex,
        Action promptsMoved)
    {
        _padDevices = padDevices;
        _localPlayer = localPlayer;
        _playerIndex = playerIndex;
        _promptsMoved = promptsMoved;
        _seatState = new SeatDeviceState(DefaultBindings.AnyPad, padDevices, sticks: StickDeviceState.Live(localPlayer));
        _padMutedState = new SeatDeviceState(DefaultBindings.AnyPad, padDevices, readsPads: false);
        _bindings = BindingProfile.Defaults(default, true);
        _actions = _bindings.Actions(InputContext.Flight);
        var map = _bindings.Map(InputContext.Flight);
        _keyActions = new PlayerActions(map, true);
        _padActions = new PlayerActions(map, false);
        _padAxes = new PlayerActions(map, false);
        _stickAxes = new PlayerActions(map, false);
        _padsAlone = StickSplit.WithoutSticks(_seatState);
        _sticksAlone = StickSplit.SticksOnly(_seatState);
        _stickSide = _sticksAlone;
    }

    /// <summary>The seat's keymap, its mouse scheme and the side its prompts name.</summary>
    public BindingProfile Profile => _bindings;

    /// <summary>The whole seat's resolved actions: keyboard, mouse and pad together.</summary>
    public PlayerActions Seat => _actions;

    /// <summary>The keyboard and mouse half alone.</summary>
    public PlayerActions KeyHalf => _keyActions;

    /// <summary>The pad half alone, flight sticks included.</summary>
    public PlayerActions PadHalf => _padActions;

    /// <summary>Resolves this seat's named actions, at most once per rendered frame. Godot's input
    /// state does not move inside a frame, so one resolve answers every read a frame makes. That
    /// covers the sim step, the draw pass and the landing runtime alike. Whichever of them asks first pays for it,
    /// which is why there is no fixed call site.</summary>
    public void Poll(bool humanPiloted, bool keyboardFlies)
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _inputFrame)
            return;
        _inputFrame = frame;
        // An AI rig is bound to no keyboard and no pad, so its resolve always reads neutral, which
        // is the readers' state before any poll. A mission flies twenty of them.
        if (!humanPiloted)
            return;
        // Splitscreen P2-P4 are pad-only and the field can change after construction, so the gate is
        // re-read rather than captured. The pad-half reader is never given the keyboard.
        _bindings.ReadsKeyboard = keyboardFlies;
        _keyActions.ReadsKeyboard = keyboardFlies;
        // A stick plugged or unplugged mid-flight moves seat 1's active profiles; the merge edits the
        // maps every resolver here reads.
        if (humanPiloted && _localPlayer() == StickDeviceState.OwningSeat
            && StickProfiles.MergeIfChanged(_bindings, ref _stickRevision))
            _promptsMoved();
        _seatState.Refresh();
        _padMutedState.Refresh();
        _actions.Poll(_seatState);
        _keyActions.Poll(_padMutedState);
        _padActions.Poll(_seatState);
        _padAxes.Poll(_padsAlone);
        _stickSide = _sticksAlone;
        _stickAxes.Poll(_stickSide);
        // A prompt names the device the seat last took input from, so a handover recomposes it.
        if (_bindings.ObserveDevice(_keyActions.Current, _padActions.Current, _stickAxes.Current))
        {
            if (humanPiloted)
                LogHandover();
            _promptsMoved();
        }
    }

    /// <summary>One discrete flight command, as the whole seat holds it, through the re-entry latch
    /// and the director's <paramref name="commandsHeld"/>.
    /// ⚠ The latch is read even while the seat is held. Skipping it would leave a control that went
    /// down during the hold reading as a fresh press the moment the hold ends. One still-down control
    /// is swallowed once for whichever commands it is bound to (FlightReentryLatch.Latched).</summary>
    public bool Command(InputAction action, bool commandsHeld)
    {
        bool held = _actions.Held(action);
        LogPressEdge(action, held);
        bool down = _reentryLatch.Read(action, held);
        return down && !commandsHeld;
    }

    /// <summary>This tick's four selector readings, each side on its own field. One call per tick:
    /// the two key-half reads go through the re-entry latch, which answers once.</summary>
    public FireInputs Selectors(bool commandsHeld) => new()
    {
        GunSelectHeld = GunSelectPressed(commandsHeld),
        RocketSelectHeld = RocketSelectPressed(commandsHeld),
        GunSelectBackHeld = GunSelectBackPressed(commandsHeld),
        RocketSelectBackHeld = RocketSelectBackPressed(commandsHeld),
        GunSelectPadHeld = PadSelectorHeld(InputAction.SelectGunGroup, commandsHeld),
        RocketSelectPadHeld = PadSelectorHeld(InputAction.SelectOrdnance, commandsHeld),
    };

    /// <summary>Arms the re-entry latch at flight's own resume and skip re-entry points. A press that
    /// just confirmed a cutscene skip or a pause-sheet dismiss then cannot also read as a command.
    /// ⚠ Pass the latch no button reading from here. A skip is handled inside an input handler,
    /// and this frame's snapshot predates the press that caused it.</summary>
    public void ArmReentryLatch() => _reentryLatch.Arm();

    /// <summary>Which eighth the digit row is asking for, or null while none of the nine is held. The
    /// highest held wins, so two digits at once open the lever rather than fighting over it. The
    /// whole seat is read, so a stick button bound to an eighth sets it too.</summary>
    public float? RequestedThrottle()
    {
        float? requested = null;
        for (int eighths = 0; eighths <= 8; eighths++)
        {
            if (_actions.Held(InputAction.ThrottleSet0 + eighths))
                requested = eighths / 8f;
        }

        return requested;
    }

    /// <summary>This tick's stick off the seat's devices. The commanded lever is written into
    /// <paramref name="leverSetting"/>, and the three attitude axes come back summed: keys ramped, a
    /// pad curved and a flight stick linear. The mouse and the live lever are the caller's.</summary>
    public FlightInput ReadStick(float dt, ref float leverSetting)
    {
        // this player's gamepads and flight sticks fly the plane, each through its own reader.
        // Arcade-flight standard: stick back (+Y) = nose up, stick right = bank right.
        var pad = AnalogAxes.Pad(_padAxes.Current);
        var stick = AnalogAxes.Stick(_stickAxes.Current);

        // The commanded lever, as FUN_00487460 writes it. The up and down keys move it at 0.5/s,
        // and a digit puts it on its eighth. It stays there once the key is up. The handler never
        // reads the tank, so a dry engine still takes the command.
        float rate = _keyActions.Axis(InputAction.ThrottleUp, InputAction.ThrottleDown) + pad.ThrottleRate + stick.ThrottleRate;
        leverSetting = Mathf.Clamp(leverSetting + (rate * FlightController.ThrottleRate * dt), 0f, 1f);
        float? scheduled = ScheduledThrottle(dt);
        float? requested = RequestedThrottle();
        // An absolute lever writes over the rate step and under the schedule and the digits. That is
        // the rule a live digit beating the schedule already follows. Any of those three hands it back.
        if (LeverSetting(Mathf.Abs(rate) > LeverTakeover.Epsilon || scheduled != null || requested != null) is { } lever)
            leverSetting = lever;
        if (scheduled is { } step)
            leverSetting = step;
        if (requested is { } digit)
            leverSetting = digit;

        // pull = S/Down, push = W/Up; bank/yaw left = A/Left/Q
        _keyPitch = StickRamp.Step(
            _keyPitch, Mathf.Sign(_keyActions.Axis(InputAction.PitchUp, InputAction.PitchDown)), dt);
        _keyRoll = StickRamp.Step(
            _keyRoll, Mathf.Sign(_keyActions.Axis(InputAction.RollLeft, InputAction.RollRight)), dt);
        _keyYaw = StickRamp.Step(
            _keyYaw, _keyActions.Axis(InputAction.YawLeft, InputAction.YawRight), dt);

        return new FlightInput
        {
            Pitch = _keyPitch + pad.Pitch + stick.Pitch,
            Roll = _keyRoll + pad.Roll + stick.Roll,
            Yaw = _keyYaw + pad.Yaw + stick.Yaw,
        };
    }

    /// <summary>Centres the three keyboard axes, as a fresh airframe spawns.</summary>
    public void CentreKeys() => _keyPitch = _keyRoll = _keyYaw = 0f;

    /// <summary>Lets go of a bound throttle lever's command, for the writers that place an aircraft.
    /// It takes over again when it moves.</summary>
    public void ReleaseLever() => _leverTakeover.Release();

    /// <summary>Puts the <c>--lever=</c> schedule on this seat, timed from this call. Null clears it.</summary>
    public void ScheduleLever(IReadOnlyList<(float At, float Lever)>? steps)
    {
        _leverSteps = steps;
        _leverElapsed = 0f;
        _leverNext = 0;
    }

    /// <summary>The weapon lab's orbit camera, mixed from this player's keyboard and pads. Read here
    /// rather than in CameraController so the camera never learns about pad devices, window focus or
    /// the stick response curve. ⚠ The swing is not a named-action read, since these are lab
    /// controls. Its key pairs SUM rather than OR, so W and Up swing at double rate. The dolly is
    /// the player's zoom pair.</summary>
    public (float Yaw, float Pitch, float Zoom) Orbit(bool keyboardFlies)
    {
        float padYaw = StickCurve(PadAxis(JoyAxis.LeftX));
        float padPitch = -StickCurve(PadAxis(JoyAxis.LeftY)); // stick up = camera up
        float padZoom = PadAxis(JoyAxis.TriggerRight)
                      - PadAxis(JoyAxis.TriggerLeft);         // RT out, LT in
        return (KeyAxis(Key.D, Key.A, keyboardFlies) + KeyAxis(Key.Right, Key.Left, keyboardFlies) + padYaw,
                KeyAxis(Key.W, Key.S, keyboardFlies) + KeyAxis(Key.Up, Key.Down, keyboardFlies) + padPitch,
                _keyActions.Axis(InputAction.ZoomOut, InputAction.ZoomIn) + padZoom); // RT out, LT in
    }

    /// <summary>This tick's reading for one action, written over the readers after a real poll. The
    /// next sim step in the same rendered frame reuses it. The suites' stand-in for hardware
    /// nothing headless can hold down.</summary>
    public void StoreForTest(InputAction action, bool held)
    {
        var read = ControlValue.Digital(held);
        _actions.Current.Store(action, read);
        _keyActions.Current.Store(action, read);
        _padActions.Current.Store(action, read);
        _padAxes.Current.Store(action, read);
    }

    /// <summary>The tick's two halves as a suite supplies them. Each state answers for one side
    /// alone, the split a live poll gets from its own pad-muted reader. The whole-seat reader gets
    /// both at once, so a site reading through it sees what the suite pressed.</summary>
    public void ObserveForTest(IDeviceState keyboardSide, IDeviceState padSide)
    {
        _actions.Poll(new BothSides(keyboardSide, padSide));
        _keyActions.Poll(keyboardSide);
        _padActions.Poll(padSide);
        _padAxes.Poll(StickSplit.WithoutSticks(padSide));
        _stickSide = StickSplit.SticksOnly(padSide);
        _stickAxes.Poll(_stickSide);
        if (_bindings.ObserveDevice(_keyActions.Current, _padActions.Current, _stickAxes.Current))
            _promptsMoved();
    }

    // Deadzone + squared response for fine control around center. A flight stick's attitude axes bypass it.
    private static float StickCurve(float v) => AnalogAxes.PadCurve(v);

    // A key, but only for a player the keyboard flies (splitscreen P2–P4 are pad-only).
    private static bool KeyDown(Key key, bool keyboardFlies) => keyboardFlies && Input.IsKeyPressed(key);

    // A +/- key pair as an axis, honoring UseKeyboard.
    private static float KeyAxis(Key positive, Key negative, bool keyboardFlies) =>
        (KeyDown(positive, keyboardFlies) ? 1f : 0f) - (KeyDown(negative, keyboardFlies) ? 1f : 0f);

    // A press reaches the session log on its rising edge, so a run's log can answer whether the
    // player pressed Respawn or fired. Logged before the latch and the hold, which swallow presses.
    private void LogPressEdge(InputAction action, bool held)
    {
        if (!held)
        {
            _pressesLogged.Remove(action);
        }
        else if (_pressesLogged.Add(action))
        {
            Log.Debug("flight", $"press P{_playerIndex() + 1} {action}");
        }
    }

    // ⚠ One latch read per action per frame. The latch disarms on the first reading that says "up".
    // A second read of the same action in the same frame would clear it early. This is the same
    // read over the keyboard and mouse half alone. The action's pad half is dispatched by a
    // tap/hold slot of its own.
    private bool CommandKeys(InputAction action, bool commandsHeld)
    {
        bool down = _reentryLatch.Read(action, _keyActions.Held(action));
        return down && !commandsHeld;
    }

    // F3 ("Cycle guns clockwise"), the KEY half alone: cycles the gun selector forward through the
    // firable groups (1 → 2 → … → 1). Only ONE group fires at a time; the gun trigger fires the
    // selected one. Caller edge-detects.
    // ⚠ Do not widen this to the seat's whole reading. The pad half carries both directions through
    // its own tap/hold slot. A combined read would step forward on the press and back on release.
    private bool GunSelectPressed(bool commandsHeld) => CommandKeys(InputAction.SelectGunGroup, commandsHeld);

    // F4 ("Cycle guns counterclockwise"), the same walk the other way. It is its own bound action
    // because the original's keybind page carries one per direction per weapon class. Read whole
    // rather than key-only: nothing ships on the pad here. A pad control on this row is one a
    // player bound themselves and means exactly one step back.
    private bool GunSelectBackPressed(bool commandsHeld) => Command(InputAction.SelectGunGroupPrev, commandsHeld);

    // F5 ("Cycle rockets clockwise"), the KEY half alone for the same reason as the gun row above.
    // It moves the hardpoint selector forward to the next pylon that still carries ordnance. Each
    // pylon is its own selectable slot, whatever it loads, even on a plane with one uniform
    // ordnance type. The rocket trigger then launches from the selected pylon. Caller edge-detects.
    private bool RocketSelectPressed(bool commandsHeld) => CommandKeys(InputAction.SelectOrdnance, commandsHeld);

    // F6 ("Cycle rockets counterclockwise"), the hardpoint walk the other way, over the same
    // physical mount order and skipping the same empties. A press each way from one pylon returns
    // to it. Caller edge-detects.
    private bool RocketSelectBackPressed(bool commandsHeld) => Command(InputAction.SelectOrdnancePrev, commandsHeld);

    // ⚠ The D-pad side follows the cockpit dial it drives. The GUNS gauge sits in the right column
    // (above the speedometer) and ROCKETS in the left. Pressing away from the dial reads as a
    // mis-binding at the controls. This is the pad half of a forward selector, as a LEVEL.
    // FireControl splits it into a tap that steps forward and a hold that steps back, so one button
    // serves a class both ways.
    private bool PadSelectorHeld(InputAction action, bool commandsHeld) =>
        !commandsHeld && _padActions.Held(action);

    // Which actions each half held on the tick the prompt side moved. An unwanted handover is
    // then traced to the control that claimed it.
    private void LogHandover()
    {
        var device = _bindings.Device;
        string stick = device.OnStick ? " (stick)" : string.Empty;
        string keys = Held(_keyActions.Current);
        string pad = Held(_padActions.Current);
        string sticks = Held(_stickAxes.Current);
        Log.Info("core", $"prompt device P{_playerIndex() + 1}: {device.Side}{stick} keys=[{keys}] pad=[{pad}] sticks=[{sticks}]");

        static string Held(ActionSnapshot snapshot)
        {
            var held = new List<string>();
            foreach (var action in Enum.GetValues<InputAction>())
            {
                if (snapshot.Value(action) >= ActiveDevice.PressTravel)
                    held.Add(action.ToString());
            }

            return string.Join(",", held);
        }
    }

    // The largest-magnitude value of the axis across this player's gamepads (0 when
    // none), idle phantom devices read ~0 and never mask the real stick.
    private float PadAxis(JoyAxis axis)
    {
        float v = 0f;
        foreach (int pad in CSVM.Bindings.Pads.For(_padDevices()))
        {
            float a = Input.GetJoyAxis(pad, axis);
            if (Mathf.Abs(a) > Mathf.Abs(v))
                v = a;
        }
        return v;
    }

    // What a bound Throttle (lever) commands this tick, or null while the other controls hold the
    // throttle. A rate under the takeover epsilon is a resting trigger's noise, not a command. While
    // input is blocked every axis reads centred, so the lever is forgotten rather than read as half.
    // An unplugged stick's lever reads centred the same way, and is forgotten the same way.
    private float? LeverSetting(bool otherCommand)
    {
        if (CSVM.Bindings.Pads.InputBlocked)
        {
            _leverTakeover.Release();
            return null;
        }

        var sticks = _localPlayer() == StickDeviceState.OwningSeat ? StickPump.Roster : null;
        float? position = AnalogAxes.LeverPosition(
            _padActions.Map.Bindings(InputAction.ThrottleLever),
            _padAxes.Value(InputAction.ThrottleLever),
            _stickSide,
            sticks);
        return AnalogAxes.StepLever(_leverTakeover, position, otherCommand);
    }

    // Which lever the --lever= schedule presses on this step, or null while none is due. A step is
    // one press: the commanded lever jumps there and stays. The live one then slews to it at its own
    // rate, so the gap the exhaust smoke charges from is a real one. A live digit beats it, since
    // the caller who added the schedule is not the one at the keyboard.
    private float? ScheduledThrottle(float dt)
    {
        if (_leverSteps == null)
            return null;
        _leverElapsed += dt;
        float? pressed = null;
        while (_leverNext < _leverSteps.Count && _leverSteps[_leverNext].At <= _leverElapsed)
            pressed = _leverSteps[_leverNext++].Lever;
        return pressed;
    }

    // The two supplied sides of a suite's tick as ONE reading, for the seat's whole-seat reader.
    // Each side answers for its own devices, so the union is what a live poll of both would give.
    private sealed class BothSides : IDeviceState
    {
        private readonly IDeviceState _keyboard;
        private readonly IDeviceState _pad;

        public BothSides(IDeviceState keyboard, IDeviceState pad)
        {
            _keyboard = keyboard;
            _pad = pad;
        }

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            _keyboard.IsKeyDown(device, keyCode) || _pad.IsKeyDown(device, keyCode);

        public bool IsButtonDown(DeviceId device, int button) =>
            _keyboard.IsButtonDown(device, button) || _pad.IsButtonDown(device, button);

        public bool IsMouseButtonDown(DeviceId device, int button) =>
            _keyboard.IsMouseButtonDown(device, button) || _pad.IsMouseButtonDown(device, button);

        public float AxisValue(DeviceId device, int axis)
        {
            float key = _keyboard.AxisValue(device, axis);
            float pad = _pad.AxisValue(device, axis);
            return Mathf.Abs(pad) > Mathf.Abs(key) ? pad : key;
        }

        public HatDirection HatState(DeviceId device, int hat)
        {
            var hats = _keyboard.HatState(device, hat);
            return hats == HatDirection.None ? _pad.HatState(device, hat) : hats;
        }
    }
}
