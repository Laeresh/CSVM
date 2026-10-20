using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.UI.Menu;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The headset's hand controllers as a device in the binding seam (<see cref="DeviceKind.Xr"/>).
/// Pinned here: the control table, the reader over a stand-in for the trackers, the shipped defaults,
/// a rebind and a capture. Then the keymap file's tokens and version rule, the captions and prompts,
/// and the rebinding screen showing the rows only under a stereo session. Engine-free: the trackers
/// are a fake <see cref="IXrHands"/>.
/// </summary>
public class XrBindingTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("*");

    [Fact]
    public void TheXrKindIsAppendedAndItsIdentityIsOneSingleton()
    {
        Assert.Equal(4, (int)DeviceKind.Xr);
        Assert.Equal(System.Enum.GetValues<DeviceKind>().Length - 1, (int)DeviceKind.Xr);
        Assert.Equal(DeviceId.Xr, DeviceId.Xr);
        Assert.Equal("xr", DeviceId.Xr.ToString());
        Assert.False(DeviceId.Xr.IsNone);
    }

    /// <summary>Every button and axis names a hand and one of the action map's actions. The two
    /// trigger clicks are the pointer's.</summary>
    [Fact]
    public void TheControlTableNamesEveryInputOnItsHand()
    {
        Assert.Equal(System.Enum.GetValues<XrButton>().Length, XrControls.ButtonCount);
        Assert.Equal(System.Enum.GetValues<XrAxis>().Length, XrControls.AxisCount);
        Assert.Equal((XrHand.Left, "menu_button"), XrControls.Button((int)XrButton.LeftMenu)!.Value);
        Assert.Equal((XrHand.Right, "by_button"), XrControls.Button((int)XrButton.RightUpperFace)!.Value);
        Assert.Equal((XrHand.Right, "primary", 1), XrControls.Axis((int)XrAxis.RightStickY)!.Value);
        Assert.Equal((XrHand.Left, XrControls.Trigger, -1), XrControls.Axis((int)XrAxis.LeftTrigger)!.Value);
        Assert.Null(XrControls.Button(XrControls.ButtonCount));
        Assert.Null(XrControls.Axis(-1));

        var triggers = System.Enum.GetValues<XrButton>().Where(b => XrControls.IsTrigger(ControlKind.Button, (int)b));
        Assert.Equal(new[] { XrButton.LeftTrigger, XrButton.RightTrigger }, triggers);
        Assert.Equal(XrButton.LeftTrigger, XrControls.TriggerOf(XrHand.Left));
        Assert.Equal(XrHand.Left, XrControls.Other(XrHand.Right));
    }

    [Fact]
    public void APresentHandReadsItsButtonsAndAxesAndNoOtherDevice()
    {
        var hands = new FakeHands();
        hands.Set(XrHand.Left, "menu_button", true);
        hands.Set(XrHand.Right, "primary", new Vector2(-0.4f, 1.6f));
        hands.Set(XrHand.Right, "grip", float.NaN);
        var xr = new XrControllers(hands);

        Assert.True(xr.IsButtonDown(DeviceId.Xr, (int)XrButton.LeftMenu));
        Assert.False(xr.IsButtonDown(DeviceId.Xr, (int)XrButton.RightMenu));
        Assert.Equal(-0.4f, xr.AxisValue(DeviceId.Xr, (int)XrAxis.RightStickX));
        Assert.Equal(1f, xr.AxisValue(DeviceId.Xr, (int)XrAxis.RightStickY));
        Assert.Equal(0f, xr.AxisValue(DeviceId.Xr, (int)XrAxis.RightGrip));
        Assert.False(xr.IsButtonDown(Pad, (int)XrButton.LeftMenu));
        Assert.False(xr.IsKeyDown(DeviceId.Keyboard, (int)Key.Escape));
        Assert.False(xr.IsButtonDown(DeviceId.Xr, 99));
    }

    /// <summary>The interface's contract for an absent device: a hand no profile binds reads
    /// nothing, however its last reading stood, and has no aim.</summary>
    [Fact]
    public void AnAbsentHandReadsZero()
    {
        var hands = new FakeHands();
        hands.Set(XrHand.Left, "menu_button", true);
        hands.Set(XrHand.Left, "primary", new Vector2(0f, 1f));
        hands.Aims[XrHand.Left] = Transform3D.Identity;
        hands.Here.Remove(XrHand.Left);
        var xr = new XrControllers(hands);

        Assert.False(xr.IsButtonDown(DeviceId.Xr, (int)XrButton.LeftMenu));
        Assert.Equal(0f, xr.AxisValue(DeviceId.Xr, (int)XrAxis.LeftStickY));
        Assert.Null(xr.Aim(XrHand.Left));
        Assert.Equal(HatDirection.None, xr.HatState(DeviceId.Xr, 0));
    }

    /// <summary>The three shipped XR rows, each on the left hand and last on its row. No other
    /// action in any context holds a hand control.</summary>
    [Fact]
    public void TheDefaultsPutPauseRecenterAndTheViewCycleOnTheLeftHand()
    {
        var expected = new Dictionary<InputAction, XrButton>
        {
            [InputAction.Pause] = XrButton.LeftMenu,
            [InputAction.RecenterHeadset] = XrButton.LeftStickClick,
            [InputAction.CycleCockpitViews] = XrButton.LeftUpperFace,
        };
        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            var map = DefaultBindings.MapFor(context, Pad);
            foreach (var action in DefaultBindings.ActionsIn(context))
            {
                var bindings = map.Bindings(action);
                var xr = bindings.Where(b => b.Device.Kind == DeviceKind.Xr).ToList();
                if (!expected.TryGetValue(action, out var button))
                {
                    Assert.Empty(xr);
                    continue;
                }

                Assert.Equal(new[] { new Binding(DeviceId.Xr, BindingControl.Button((int)button)) }, xr);
                Assert.Equal(xr[0], bindings[^1]);
                Assert.Equal(new[] { action }, map.OwnersOf(xr[0]));
            }
        }
    }

    [Fact]
    public void AHeldXrButtonResolvesItsActionAndAnAbsentHandResolvesNothing()
    {
        var hands = new FakeHands();
        hands.Set(XrHand.Left, "menu_button", true);
        var actions = new PlayerActions(DefaultBindings.MapFor(InputContext.Flight, Pad), readsKeyboard: false);

        actions.Poll(new XrControllers(hands));
        Assert.True(actions.Held(InputAction.Pause));
        Assert.False(actions.Held(InputAction.RecenterHeadset));

        hands.Here.Clear();
        actions.Poll(new XrControllers(hands));
        Assert.False(actions.Held(InputAction.Pause));
    }

    /// <summary>A rebind onto a hand control takes it off its shipped owner by the steal rule, and
    /// the action then fires from it.</summary>
    [Fact]
    public void RebindingAnXrControlStealsItAndResolvesOnTheNewAction()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var menu = new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftMenu));

        var losers = map.Assign(InputAction.Respawn, menu);

        Assert.Equal(new[] { InputAction.Pause }, losers);
        Assert.DoesNotContain(menu, map.Bindings(InputAction.Pause));
        var hands = new FakeHands();
        hands.Set(XrHand.Left, "menu_button", true);
        var actions = new PlayerActions(map, readsKeyboard: false);
        actions.Poll(new XrControllers(hands));
        Assert.True(actions.Held(InputAction.Respawn));
        Assert.False(actions.Held(InputAction.Pause));
    }

    /// <summary>A capture takes a hand button or a hand stick past its threshold and masks what was
    /// held when it armed.</summary>
    [Fact]
    public void ACaptureTakesHandButtonsAndSticks()
    {
        var hands = new FakeHands();
        var xr = new XrControllers(hands);
        hands.Set(XrHand.Right, "ax_button", true);
        var capture = new ControlCapture(Pad, readsKeyboard: true);
        capture.Arm(xr);

        Assert.Null(capture.Poll(xr));
        hands.Set(XrHand.Left, "by_button", true);
        Assert.Equal(new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftUpperFace)), capture.Poll(xr));

        var stick = new ControlCapture(Pad, readsKeyboard: true);
        hands.Clear();
        stick.Arm(xr);
        hands.Set(XrHand.Left, "primary", new Vector2(0f, -0.9f));
        Assert.Equal(
            new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.LeftStickY, -1, ControlCapture.CapturedDeadzone)),
            stick.Poll(xr));
    }

    /// <summary>A trigger binds as its axis, as a pad trigger does, and only on a pull begun after the
    /// capture opened. The pull that opened it is masked until the trigger is let go, its click
    /// included, so finishing that pull binds nothing.</summary>
    [Fact]
    public void ACaptureTakesATriggerOnlyOnAFreshPull()
    {
        var hands = new FakeHands();
        var xr = new XrControllers(hands);
        Trigger(hands, XrHand.Right, 0.1f, clicked: true);
        var capture = new ControlCapture(Pad, readsKeyboard: true);
        capture.Arm(xr);

        Trigger(hands, XrHand.Right, 1f, clicked: true);
        Assert.Null(capture.Poll(xr));
        Trigger(hands, XrHand.Right, 0.1f, clicked: true);
        Assert.Null(capture.Poll(xr));
        Trigger(hands, XrHand.Right, 1f, clicked: true);
        Assert.Null(capture.Poll(xr));

        Trigger(hands, XrHand.Right, 0f, clicked: false);
        Assert.Null(capture.Poll(xr));
        Trigger(hands, XrHand.Right, 0.4f, clicked: true);
        Assert.Null(capture.Poll(xr));
        Trigger(hands, XrHand.Right, 1f, clicked: true);
        Assert.Equal(
            new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.RightTrigger, 1, ControlCapture.CapturedDeadzone)),
            capture.Poll(xr));
        Assert.True(ControlCapture.TakeListening());

        var left = new ControlCapture(Pad, readsKeyboard: true);
        hands.Clear();
        left.Arm(xr);
        Trigger(hands, XrHand.Left, 0.9f, clicked: true);
        Assert.Equal(
            new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.LeftTrigger, 1, ControlCapture.CapturedDeadzone)),
            left.Poll(xr));
    }

    /// <summary>A bound trigger fires its action past half its pull, the pad trigger's threshold.
    /// </summary>
    [Fact]
    public void ABoundTriggerFiresItsActionPastHalfItsPull()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        map.Assign(InputAction.FireGuns, new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.RightTrigger, 1, ControlCapture.CapturedDeadzone)));
        var hands = new FakeHands();
        var actions = new PlayerActions(map, readsKeyboard: false);

        Trigger(hands, XrHand.Right, 0.3f, clicked: true);
        actions.Poll(new XrControllers(hands));
        Assert.False(actions.Held(InputAction.FireGuns));
        Trigger(hands, XrHand.Right, 0.8f, clicked: true);
        actions.Poll(new XrControllers(hands));
        Assert.True(actions.Held(InputAction.FireGuns));
    }

    [Fact]
    public void TheKeymapFileNamesHandControlsAndReadsThemBack()
    {
        var button = new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftMenu));
        var axis = new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.RightStickY, 1, 0.5f));

        Assert.Equal("xr/button:LeftMenu", BindingStore.Encode(button));
        Assert.Equal("xr/axis:RightStickY+@0.5", BindingStore.Encode(axis));
        Assert.Equal(button, BindingStore.Decode("xr/button:LeftMenu"));
        Assert.Equal(axis, BindingStore.Decode("xr/axis:RightStickY+@0.5"));
        Assert.Null(BindingStore.Decode("xr/button:Start"));
        Assert.Null(BindingStore.Decode("xr/key:Escape"));
        Assert.Null(BindingStore.Decode("xr/hat:0:Up"));
        Assert.Null(BindingStore.Decode("xr/fullaxis:2+@0"));
        Assert.Null(BindingStore.Decode("xr/mouse:Right"));
    }

    [Fact]
    public void ARebindOntoAHandControlSurvivesTheFile()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var face = new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.RightLowerFace));
        profile.Map(InputContext.Flight).Assign(InputAction.Nitro, face);
        profile.Map(InputContext.Flight).Unassign(InputAction.Pause, new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftMenu)));

        var loaded = BindingStore.Deserialize(BindingStore.Serialize(1, profile), Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Contains(face, loaded.Bindings(InputAction.Nitro));
        Assert.DoesNotContain(loaded.Bindings(InputAction.Pause), b => b.Device.Kind == DeviceKind.Xr);
    }

    /// <summary>A file older than the xr device could not name a hand control. A row it names
    /// keeps the shipped one, while a current file's row is the player's whole word.</summary>
    [Fact]
    public void AnOlderFileKeepsTheXrDefaultsOnTheRowsItNames()
    {
        const string Row = "\"contexts\": { \"flight\": { \"Pause\": [\"keyboard/key:P\"] } } }";
        var menu = new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftMenu));

        var older = BindingStore.Deserialize("{ \"version\": 3, " + Row, Pad, readsKeyboard: true).Map(InputContext.Flight);
        var current = BindingStore.Deserialize("{ \"version\": 4, " + Row, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.P)), menu }, older.Bindings(InputAction.Pause));
        Assert.Equal(new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.P)) }, current.Bindings(InputAction.Pause));
    }

    [Fact]
    public void AHandControlReadsAsTheTouchButtonItIs()
    {
        Assert.Equal("VR Menu", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftMenu))));
        Assert.Equal("VR Y", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftUpperFace))));
        Assert.Equal("VR L Stick Click", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftStickClick))));
        Assert.Equal("VR R Stick Up", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.RightStickY, 1, 0.5f))));
        Assert.Equal("VR L Stick Left", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.LeftStickX, -1, 0.5f))));
        Assert.Equal("VR R Trigger", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.RightTrigger, 1, 0.5f))));
        Assert.Equal("VR L Trigger", BindingLabels.Describe(new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.LeftTrigger, 1, 0.5f))));
    }

    /// <summary>A prompt names a key or a pad control before a hand control, and the hand control
    /// only where nothing else holds the action.</summary>
    [Fact]
    public void APromptNamesAHandControlOnlyWhereNothingElseHoldsTheAction()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var menu = new Binding(DeviceId.Xr, BindingControl.Button((int)XrButton.LeftMenu));

        Assert.Equal(new Binding(Pad, BindingControl.Button((int)JoyButton.Start)),
            ActiveDevice.PromptBinding(map.Bindings(InputAction.Pause), DeviceSide.Pad, readsKeyboard: true));
        Assert.Equal(new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Escape)),
            ActiveDevice.PromptBinding(map.Bindings(InputAction.Pause), DeviceSide.Keyboard, readsKeyboard: true));
        Assert.Equal(menu, ActiveDevice.PromptBinding(new[] { menu }, DeviceSide.Keyboard, readsKeyboard: true));
    }

    /// <summary>The rebinding screen lists hand controls only where it shows them: under a stereo
    /// session. Without one every row reads as flat play's, and a reset still restores them.</summary>
    [Fact]
    public void TheRebindingScreenListsHandControlsOnlyUnderAStereoSession()
    {
        var feature = new ControlsFeature { ShowsXr = () => false };
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        feature.AddSeat(1, profile, new XrCaptureDevices(), true);
        feature.Context = InputContext.Flight;
        int pause = feature.Actions.ToList().IndexOf(InputAction.Pause);

        Assert.Equal("Escape, Pad Start", feature.RowText(InputAction.Pause));
        Assert.Equal(2, feature.Bindings(InputAction.Pause).Count);

        feature.ShowsXr = () => true;
        Assert.Equal("Escape, Pad Start, VR Menu", feature.RowText(InputAction.Pause));
        feature.Focus(pause);
        feature.MoveSlot(2);
        feature.UnbindSlot();
        Assert.Equal("Escape, Pad Start", feature.RowText(InputAction.Pause));
        feature.ResetSeat();
        Assert.Equal("Escape, Pad Start, VR Menu", feature.RowText(InputAction.Pause));
    }

    // One hand's trigger at a pull, with its click as the runtime thresholds it.
    private static void Trigger(FakeHands hands, XrHand hand, float pull, bool clicked)
    {
        hands.Set(hand, XrControls.Trigger, pull);
        hands.Set(hand, XrControls.TriggerClick, clicked);
    }

    // The trackers as a dictionary: which hands are bound, each action's reading and each aim.
    private sealed class FakeHands : IXrHands
    {
        private readonly Dictionary<(XrHand, string), object> _inputs = new();

        public HashSet<XrHand> Here { get; } = new() { XrHand.Left, XrHand.Right };

        public Dictionary<XrHand, Transform3D> Aims { get; } = new();

        public void Set(XrHand hand, string action, object value) => _inputs[(hand, action)] = value;

        public void Clear() => _inputs.Clear();

        public bool Present(XrHand hand) => Here.Contains(hand);

        public float Read(XrHand hand, string action, int component) =>
            _inputs.TryGetValue((hand, action), out var value)
                ? value switch
                {
                    bool pressed => pressed ? 1f : 0f,
                    float f => f,
                    Vector2 v when component is 0 or 1 => v[component],
                    _ => 0f,
                }
                : 0f;

        public Transform3D? Aim(XrHand hand) => Aims.TryGetValue(hand, out var aim) ? aim : null;
    }

    // A seat's capture readers with nothing pressed, for a screen that only lists rows.
    private sealed class XrCaptureDevices : ICaptureDevices
    {
        private readonly XrControllers _state = new(new FakeHands());

        public DeviceId PadOf(InputContext context) => Pad;

        public IDeviceState For(InputContext context) => _state;
    }
}
