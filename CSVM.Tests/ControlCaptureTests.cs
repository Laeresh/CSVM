using System.Collections.Generic;
using CSVM.Bindings;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// What a rebinding screen may capture and how: nothing that was already held when the capture
/// armed, nothing on an axis until it has been seen at rest and then moved decisively, nothing on a
/// hat ever (a d-pad direction arrives as its button), and every pad control stamped with the seat's
/// own identity rather than a hardware GUID the seat would never resolve. Engine-free, over the same
/// kind of fake device state <see cref="ActionMapTests"/> uses.
/// </summary>
public class ControlCaptureTests
{
    private static readonly DeviceId Seat = DeviceId.Joypad("menu-seat");

    [Fact]
    public void AFreshKeyPressIsCapturedAsAKeyboardBinding()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Keys.Add((int)Key.J);
        var captured = capture.Poll(state);

        Assert.NotNull(captured);
        var binding = captured!.Value;
        Assert.Equal(DeviceId.Keyboard, binding.Device);
        Assert.Equal(ControlKind.Key, binding.Control.Kind);
        Assert.Equal((int)Key.J, binding.Control.Index);
    }

    [Fact]
    public void AControlAlreadyHeldWhenTheCaptureArmsIsNotTheAnswerToIt()
    {
        var state = new FakeDevices();
        state.Keys.Add((int)Key.Enter);
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        Assert.Null(capture.Poll(state));

        state.Keys.Remove((int)Key.Enter);
        Assert.Null(capture.Poll(state));

        state.Keys.Add((int)Key.Enter);
        Assert.Equal((int)Key.Enter, capture.Poll(state)!.Value.Control.Index);
    }

    [Fact]
    public void APadButtonIsCapturedOnTheSeatsOwnIdentityAndNotOnAHardwareGuid()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Buttons.Add((Seat, (int)JoyButton.X));
        state.Buttons.Add((DeviceId.Joypad("030000005e040000e002000000007801"), (int)JoyButton.Y));
        var binding = capture.Poll(state)!.Value;

        Assert.Equal(Seat, binding.Device);
        Assert.Equal((int)JoyButton.X, binding.Control.Index);
    }

    [Fact]
    public void AnAxisIdlingInsideItsDriftIsNeverCaptured()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        foreach (float drift in new[] { 0.05f, -0.09f, ControlCapture.RestBand - 0.01f })
        {
            state.Axes[(Seat, (int)JoyAxis.LeftY)] = drift;
            Assert.Null(capture.Poll(state));
        }
    }

    [Fact]
    public void AnAxisPastTheRestBandButShortOfTheMoveThresholdIsNotCapturedEither()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Axes[(Seat, (int)JoyAxis.LeftY)] = ControlCapture.MoveThreshold - 0.01f;

        Assert.Null(capture.Poll(state));
    }

    [Fact]
    public void AnAxisMovedDecisivelyIsCapturedWithTheSignItMoved()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Axes[(Seat, (int)JoyAxis.LeftY)] = -0.85f;
        var binding = capture.Poll(state)!.Value;

        Assert.Equal(Seat, binding.Device);
        Assert.Equal(ControlKind.Axis, binding.Control.Kind);
        Assert.Equal((int)JoyAxis.LeftY, binding.Control.Index);
        Assert.Equal(-1, binding.Control.Sign);
        Assert.Equal(ControlCapture.CapturedDeadzone, binding.Control.Deadzone);
    }

    [Fact]
    public void AnAxisDeflectedWhenTheCaptureArmedMustCentreBeforeItCanBeCaptured()
    {
        var state = new FakeDevices();
        state.Axes[(Seat, (int)JoyAxis.LeftX)] = 0.9f;
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        Assert.Null(capture.Poll(state));

        state.Axes[(Seat, (int)JoyAxis.LeftX)] = 1f;
        Assert.Null(capture.Poll(state));

        state.Axes[(Seat, (int)JoyAxis.LeftX)] = 0.02f;
        Assert.Null(capture.Poll(state));

        state.Axes[(Seat, (int)JoyAxis.LeftX)] = 0.9f;
        Assert.Equal(1, capture.Poll(state)!.Value.Control.Sign);
    }

    [Fact]
    public void APressedButtonBeatsAStickAThumbIsRestingOn()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Axes[(Seat, (int)JoyAxis.LeftY)] = 1f;
        state.Buttons.Add((Seat, (int)JoyButton.A));

        Assert.Equal(ControlKind.Button, capture.Poll(state)!.Value.Control.Kind);
    }

    [Fact]
    public void ADpadDirectionIsCapturedAsItsButtonAndAHatIsNotCapturedAtAll()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        // Godot reports a d-pad as four buttons, so the reported hat is a second encoding of the
        // same control and binding it would put one direction on two actions (DefaultBindings).
        state.Hats[(Seat, 0)] = HatDirection.Up;
        Assert.Null(capture.Poll(state));

        state.Buttons.Add((Seat, (int)JoyButton.DpadUp));
        var binding = capture.Poll(state)!.Value;
        Assert.Equal(ControlKind.Button, binding.Control.Kind);
        Assert.Equal((int)JoyButton.DpadUp, binding.Control.Index);
    }

    [Fact]
    public void ACapturedAxisResolvesTheBooleanAnActionDesignedAsAButtonExpects()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);
        state.Axes[(Seat, (int)JoyAxis.TriggerRight)] = 1f;
        var captured = capture.Poll(state)!.Value;

        var map = new ActionMap();
        map.Assign(InputAction.FireGuns, captured);
        var actions = new PlayerActions(map, readsKeyboard: true);

        actions.Poll(state);
        Assert.True(actions.Held(InputAction.FireGuns));

        state.Axes[(Seat, (int)JoyAxis.TriggerRight)] = 0f;
        actions.Poll(state);
        Assert.False(actions.Held(InputAction.FireGuns));
    }

    [Fact]
    public void EscapeAndPadBCancelRatherThanBinding()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Keys.Add((int)Key.Escape);
        Assert.True(capture.Cancelled(state));
        Assert.Null(capture.Poll(state));

        state.Keys.Clear();
        state.Buttons.Add((Seat, (int)JoyButton.B));
        Assert.True(capture.Cancelled(state));
        Assert.Null(capture.Poll(state));
    }

    [Fact]
    public void AnEscapeStillHeldFromOpeningTheCaptureDoesNotCancelIt()
    {
        var state = new FakeDevices();
        state.Keys.Add((int)Key.Escape);
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        Assert.False(capture.Cancelled(state));

        state.Keys.Clear();
        Assert.False(capture.Cancelled(state));

        state.Keys.Add((int)Key.Escape);
        Assert.True(capture.Cancelled(state));
    }

    [Fact]
    public void APadOnlySeatCapturesNoKeyAndNoMouseButtonButStillCapturesItsPad()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: false);
        capture.Arm(state);

        state.Keys.Add((int)Key.J);
        state.Mouse.Add((int)MouseButton.Right);
        Assert.Null(capture.Poll(state));

        state.Buttons.Add((Seat, (int)JoyButton.A));
        Assert.Equal(Seat, capture.Poll(state)!.Value.Device);
    }

    [Fact]
    public void AMouseButtonPastThePointersOwnIsCapturedOnTheOneMouse()
    {
        var state = new FakeDevices();
        var capture = new ControlCapture(Seat, readsKeyboard: true);
        capture.Arm(state);

        state.Mouse.Add((int)MouseButton.Left);
        Assert.Null(capture.Poll(state));

        state.Mouse.Add((int)MouseButton.Right);
        var binding = capture.Poll(state)!.Value;
        Assert.Equal(DeviceId.Mouse, binding.Device);
        Assert.Equal(ControlKind.Mouse, binding.Control.Kind);
    }

    private sealed class FakeDevices : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public HashSet<int> Mouse { get; } = new();

        public HashSet<(DeviceId Device, int Index)> Buttons { get; } = new();

        public Dictionary<(DeviceId Device, int Index), float> Axes { get; } = new();

        public Dictionary<(DeviceId Device, int Index), HatDirection> Hats { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => Buttons.Contains((device, button));

        public bool IsMouseButtonDown(DeviceId device, int button) =>
            device == DeviceId.Mouse && Mouse.Contains(button);

        public float AxisValue(DeviceId device, int axis) =>
            Axes.TryGetValue((device, axis), out float value) ? value : 0f;

        public HatDirection HatState(DeviceId device, int hat) =>
            Hats.TryGetValue((device, hat), out var state) ? state : HatDirection.None;
    }
}
