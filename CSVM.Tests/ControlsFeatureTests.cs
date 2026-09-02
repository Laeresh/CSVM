using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.UI.Menu;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The rebinding screen's model: edits are staged and only Accept writes them through, Cancel
/// abandons them whole (a reset included), a free control binds, a held one names every action it
/// would take the control from and moves nothing until the player says so, a seat's edits reach
/// nobody else's keymap, and a row never hides a binding without counting it. Engine-free, driven
/// the way a presentation drives it.
/// </summary>
public class ControlsFeatureTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("*");

    [Fact]
    public void AFreeControlBindsWithNothingToAskAndTheStatusNamesBoth()
    {
        var (feature, map) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9); // past the end: the empty slot that adds a control

        Assert.True(feature.Offer(Key(Godot.Key.M)));

        Assert.Null(feature.Pending);
        Assert.Contains(Key(Godot.Key.M), feature.Bindings(InputAction.Nitro));
        Assert.DoesNotContain(Key(Godot.Key.M), map.Bindings(InputAction.Nitro));
        Assert.Equal("Nitro is now M.", feature.Status);
    }

    [Fact]
    public void AcceptWritesTheStagedEditIntoTheMapThePollingSiteHolds()
    {
        var (feature, map) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));

        feature.Accept();

        Assert.Contains(Key(Godot.Key.M), map.Bindings(InputAction.Nitro));
        Assert.False(feature.Dirty);
    }

    [Fact]
    public void ACancelledEditReachesNeitherTheLiveMapNorTheSave()
    {
        var written = new List<int>();
        var feature = new ControlsFeature((player, _) => written.Add(player));
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        feature.AddSeat(1, profile, _ => Pad, true);
        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));

        feature.Cancel();

        Assert.DoesNotContain(Key(Godot.Key.M), feature.Bindings(InputAction.Nitro));
        Assert.DoesNotContain(Key(Godot.Key.M), profile.Map(InputContext.Flight).Bindings(InputAction.Nitro));
        Assert.Empty(written);
        Assert.False(feature.Dirty);
    }

    [Fact]
    public void AResetReachesEveryContextAndNotOnlyTheOneOnScreen()
    {
        var feature = new ControlsFeature();
        feature.AddSeat(1, BindingProfile.Defaults(Pad, readsKeyboard: true), _ => Pad, true);

        // One unbind in each context, so a per-page reset would leave two of them broken.
        var lost = new Dictionary<InputContext, Binding>();
        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            feature.Context = context;
            var action = FirstBound(feature);
            lost[context] = feature.Bindings(action)[0];
            feature.Focus(IndexOf(feature, action));
            feature.UnbindSlot();
            Assert.DoesNotContain(lost[context], feature.Bindings(action));
        }

        feature.Context = InputContext.Flight;
        feature.ResetSeat();

        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            feature.Context = context;
            var action = FirstBound(feature);
            Assert.Contains(lost[context], feature.Bindings(action));
        }
    }

    [Fact]
    public void CancelPutsBackEverythingAResetCleared()
    {
        var (feature, map) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));
        feature.Accept(); // the rebind is now what the polling site holds
        feature.ResetSeat();
        Assert.DoesNotContain(Key(Godot.Key.M), feature.Bindings(InputAction.Nitro));

        feature.Cancel();

        Assert.Contains(Key(Godot.Key.M), feature.Bindings(InputAction.Nitro));
        Assert.Contains(Key(Godot.Key.M), map.Bindings(InputAction.Nitro));
    }

    [Fact]
    public void AcceptCommitsEveryContextAtOnce()
    {
        var feature = new ControlsFeature();
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        feature.AddSeat(1, profile, _ => Pad, true);

        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));
        feature.Context = InputContext.Camera;
        feature.Focus(IndexOf(feature, InputAction.CameraLockTarget));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));

        feature.Accept();

        Assert.Contains(Key(Godot.Key.M), profile.Map(InputContext.Flight).Bindings(InputAction.Nitro));
        Assert.Contains(Key(Godot.Key.M), profile.Map(InputContext.Camera).Bindings(InputAction.CameraLockTarget));
    }

    [Fact]
    public void TheCommitFillsTheSameMapObjectSoAMenuPollerFeelsIt()
    {
        var feature = new ControlsFeature();
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        feature.AddSeat(1, profile, _ => Pad, true);
        var held = profile.Map(InputContext.Menu); // what a menu poller holds a reference to

        feature.Context = InputContext.Menu;
        feature.Focus(IndexOf(feature, InputAction.MenuLoadout));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));
        feature.Accept();

        Assert.Same(held, profile.Map(InputContext.Menu));
        Assert.Contains(Key(Godot.Key.M), held.Bindings(InputAction.MenuLoadout));
    }

    [Fact]
    public void AHeldControlNamesEveryOwnerAndMovesNothingUntilItIsConfirmed()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Respawn));
        feature.MoveSlot(9);

        // D-pad up is deliberately on two flight actions (C21's defaults, B15's stunt marker).
        Assert.True(feature.Offer(Button(JoyButton.DpadUp)));

        var pending = Assert.IsType<RebindSteal>(feature.Pending);
        Assert.Equal(
            new[] { InputAction.TargetNextEnemy, InputAction.CycleStuntTarget },
            pending.Losers);
        Assert.Contains("Target Next Enemy and Cycle Stunt Target", feature.Status);
        Assert.Contains(Button(JoyButton.DpadUp), feature.Bindings(InputAction.TargetNextEnemy));
        Assert.DoesNotContain(Button(JoyButton.DpadUp), feature.Bindings(InputAction.Respawn));
    }

    [Fact]
    public void ConfirmingTheStealTakesTheControlFromEveryOwner()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Respawn));
        feature.MoveSlot(9);
        feature.Offer(Button(JoyButton.DpadUp));

        feature.ConfirmSteal();

        Assert.Null(feature.Pending);
        Assert.DoesNotContain(Button(JoyButton.DpadUp), feature.Bindings(InputAction.TargetNextEnemy));
        Assert.DoesNotContain(Button(JoyButton.DpadUp), feature.Bindings(InputAction.CycleStuntTarget));
        Assert.Contains(Button(JoyButton.DpadUp), feature.Bindings(InputAction.Respawn));
        Assert.Contains("lost it", feature.Status);
    }

    [Fact]
    public void DiscardingTheStealLeavesEveryActionsControlsAlone()
    {
        var (feature, _) = Flight();
        var before = new List<Binding>(feature.Bindings(InputAction.TargetNextEnemy));
        feature.Focus(IndexOf(feature, InputAction.Respawn));
        feature.MoveSlot(9);
        feature.Offer(Button(JoyButton.DpadUp));

        feature.DiscardSteal();

        Assert.Null(feature.Pending);
        Assert.Equal(before, feature.Bindings(InputAction.TargetNextEnemy));
        Assert.DoesNotContain(Button(JoyButton.DpadUp), feature.Bindings(InputAction.Respawn));
    }

    [Fact]
    public void ARebindForOnePlayerLeavesTheOtherPlayersKeymapUntouched()
    {
        var feature = new ControlsFeature();
        var one = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var two = BindingProfile.Defaults(Pad, readsKeyboard: false);
        feature.AddSeat(1, one, _ => Pad, true);
        feature.AddSeat(2, two, _ => Pad, false);

        feature.Player = 2;
        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));
        feature.Accept();

        Assert.Contains(Key(Godot.Key.M), two.Map(InputContext.Flight).Bindings(InputAction.Nitro));
        Assert.DoesNotContain(Key(Godot.Key.M), one.Map(InputContext.Flight).Bindings(InputAction.Nitro));
    }

    [Fact]
    public void AResetOnOneSeatLeavesTheOtherSeatsStagedEditsAlone()
    {
        var feature = new ControlsFeature();
        var one = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var two = BindingProfile.Defaults(Pad, readsKeyboard: false);
        feature.AddSeat(1, one, _ => Pad, true);
        feature.AddSeat(2, two, _ => Pad, false);

        feature.Player = 1;
        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));

        feature.Player = 2;
        feature.Context = InputContext.Flight;
        feature.ResetSeat();
        feature.Accept();

        Assert.Contains(Key(Godot.Key.M), one.Map(InputContext.Flight).Bindings(InputAction.Nitro));
        Assert.DoesNotContain(Key(Godot.Key.M), two.Map(InputContext.Flight).Bindings(InputAction.Nitro));
    }

    [Fact]
    public void TheSlotCursorReplacesTheControlItIsOnRatherThanAddingOne()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.LookCenter));
        int before = feature.Bindings(InputAction.LookCenter).Count;

        feature.Offer(Key(Godot.Key.M));

        Assert.Equal(before, feature.Bindings(InputAction.LookCenter).Count);
        Assert.Contains(Key(Godot.Key.M), feature.Bindings(InputAction.LookCenter));
    }

    [Fact]
    public void TheEmptySlotPastTheEndAddsAControlWithoutLosingTheOnesAlreadyThere()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.LookCenter));
        var kept = feature.Bindings(InputAction.LookCenter)[0];
        feature.MoveSlot(9);

        feature.Offer(Key(Godot.Key.M));

        Assert.Contains(kept, feature.Bindings(InputAction.LookCenter));
        Assert.Contains(Key(Godot.Key.M), feature.Bindings(InputAction.LookCenter));
    }

    [Fact]
    public void UnbindingDropsOneControlAndTouchesNoOtherAction()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.TargetNextEnemy));
        var dropped = feature.Bindings(InputAction.TargetNextEnemy)[0];

        feature.UnbindSlot();

        Assert.DoesNotContain(dropped, feature.Bindings(InputAction.TargetNextEnemy));
        Assert.Contains(Button(JoyButton.DpadUp), feature.Bindings(InputAction.CycleStuntTarget));
        Assert.Contains("lost", feature.Status);
    }

    [Fact]
    public void ResettingOneContextRestoresItsDefaultsAndLeavesTheLiveMapAlone()
    {
        var (feature, map) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.Offer(Key(Godot.Key.M));

        feature.ResetContext();

        Assert.DoesNotContain(Key(Godot.Key.M), feature.Bindings(InputAction.Nitro));
        Assert.Equal(
            DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.Nitro),
            feature.Bindings(InputAction.Nitro));
        Assert.Equal(
            DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.Nitro),
            map.Bindings(InputAction.Nitro));
    }

    [Fact]
    public void EditingOneContextLeavesTheOtherTwoAlone()
    {
        var feature = new ControlsFeature();
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        feature.AddSeat(1, profile, _ => Pad, true);
        var menuBefore = new List<Binding>(profile.Map(InputContext.Menu).Bindings(InputAction.MenuAccept));

        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(new Binding(DeviceId.Keyboard, BindingControl.Key((int)Godot.Key.Space)));
        feature.Accept();

        Assert.Equal(menuBefore, profile.Map(InputContext.Menu).Bindings(InputAction.MenuAccept));
    }

    [Fact]
    public void TheSaveIsThePlayersOwnAndOnlyRunsOnceForOneChange()
    {
        var written = new List<int>();
        var feature = new ControlsFeature((player, _) => written.Add(player));
        feature.AddSeat(1, BindingProfile.Defaults(Pad, true), _ => Pad, true);
        feature.AddSeat(2, BindingProfile.Defaults(Pad, false), _ => Pad, false);

        feature.Accept();
        Assert.Empty(written);

        feature.Player = 2;
        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.Offer(Key(Godot.Key.M));
        feature.Accept();
        feature.Accept();

        Assert.Equal(new[] { 2 }, written);
    }

    [Fact]
    public void EditingTwoSeatsAndAcceptingOnceWritesBothOfThem()
    {
        var written = new List<int>();
        var feature = new ControlsFeature((player, _) => written.Add(player));
        feature.AddSeat(1, BindingProfile.Defaults(Pad, true), _ => Pad, true);
        feature.AddSeat(2, BindingProfile.Defaults(Pad, false), _ => Pad, false);

        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.Offer(Key(Godot.Key.M));
        feature.Player = 2;
        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.Offer(Key(Godot.Key.M));
        feature.Accept();

        Assert.Equal(new[] { 1, 2 }, written);
        Assert.False(feature.Dirty);
    }

    [Fact]
    public void ACapturedPadControlCarriesTheContextsOwnPadIdentity()
    {
        var feature = new ControlsFeature();
        var menuSeat = DeviceId.Joypad("menu-seat");
        var maps = new Dictionary<InputContext, ActionMap>
        {
            [InputContext.Flight] = DefaultBindings.MapFor(InputContext.Flight, Pad),
            [InputContext.Menu] = DefaultBindings.MapFor(InputContext.Menu, menuSeat),
            [InputContext.Camera] = DefaultBindings.MapFor(InputContext.Camera, Pad),
        };
        feature.AddSeat(1, new BindingProfile(maps, true),
            c => c == InputContext.Menu ? menuSeat : Pad, true);

        var state = new FakeDevices();
        feature.Context = InputContext.Menu;
        feature.Focus(IndexOf(feature, InputAction.MenuLoadout));
        feature.MoveSlot(9);
        feature.BeginCapture(state);
        state.Buttons.Add((menuSeat, (int)JoyButton.RightStick));
        Assert.True(feature.Poll(state));
        feature.Accept();

        var bound = maps[InputContext.Menu].Bindings(InputAction.MenuLoadout);
        Assert.Contains(new Binding(menuSeat, BindingControl.Button((int)JoyButton.RightStick)), bound);
    }

    [Fact]
    public void ACaptureOnAControlAnotherActionHoldsStopsToNameItRatherThanBinding()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Respawn));
        feature.MoveSlot(9);
        var state = new FakeDevices();
        feature.BeginCapture(state);

        state.Keys.Add((int)Godot.Key.Space);
        Assert.True(feature.Poll(state));

        Assert.False(feature.Capturing);
        Assert.Equal(new[] { InputAction.FireGuns }, feature.Pending!.Losers);
        Assert.Contains(Key(Godot.Key.Space), feature.Bindings(InputAction.FireGuns));
    }

    [Fact]
    public void NoShippedActionHidesABindingAtTheRowsOwnLimit()
    {
        var feature = new ControlsFeature();
        feature.AddSeat(1, BindingProfile.Defaults(Pad, true), _ => Pad, true);

        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            feature.Context = context;
            foreach (var action in feature.Actions)
            {
                Assert.DoesNotContain("more", feature.RowText(action));
            }
        }
    }

    [Fact]
    public void ARowLongerThanTheLimitSaysHowManyControlsItIsNotShowing()
    {
        var bindings = new[]
        {
            Key(Godot.Key.A), Key(Godot.Key.B), Key(Godot.Key.C), Key(Godot.Key.D), Key(Godot.Key.E),
            Key(Godot.Key.F),
        };

        Assert.Equal("A, B, C, D, +2 more", BindingLabels.Row(bindings, ControlsFeature.RowBindings));
        Assert.Equal(BindingLabels.Unbound, BindingLabels.Row(System.Array.Empty<Binding>(), 4));
    }

    [Fact]
    public void DiscardDropsTheCaptureAndThePendingStealAndKeepsTheStagedEdits()
    {
        var (feature, _) = Flight();
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        feature.Offer(Key(Godot.Key.M));
        feature.Focus(IndexOf(feature, InputAction.Respawn));
        feature.MoveSlot(9);
        feature.Offer(Button(JoyButton.DpadUp));
        feature.BeginCapture(new FakeDevices());

        feature.Discard();

        Assert.False(feature.Capturing);
        Assert.Null(feature.Pending);
        Assert.Equal(string.Empty, feature.Status);
        Assert.Contains(Button(JoyButton.DpadUp), feature.Bindings(InputAction.TargetNextEnemy));
        Assert.Contains(Key(Godot.Key.M), feature.Bindings(InputAction.Nitro));
        Assert.True(feature.Dirty);
    }

    [Fact]
    public void ACapturedTriggerNamesBothOwnersOfTheDoubleBoundTriggerWhateverDeadzoneEachHolds()
    {
        var feature = new ControlsFeature();
        feature.AddSeat(1, BindingProfile.Defaults(Pad, readsKeyboard: true), _ => Pad, true);
        feature.Context = InputContext.Camera;
        feature.Focus(IndexOf(feature, InputAction.CameraLockTarget));
        feature.MoveSlot(9);

        // The right trigger ships on boost at 0.5 and on the dolly at 0, and SameControl ignores the
        // deadzone, so a capture at a third one lands on both rather than stacking a copy.
        Assert.True(feature.Offer(Axis(JoyAxis.TriggerRight, 1, ControlCapture.CapturedDeadzone)));

        Assert.Equal(
            new[] { InputAction.CameraBoost, InputAction.CameraDollyOut },
            feature.Pending!.Losers);

        feature.ConfirmSteal();
        Assert.Empty(feature.Bindings(InputAction.CameraDollyOut));
        Assert.Contains(Key(Godot.Key.Shift), feature.Bindings(InputAction.CameraBoost));
    }

    private static (ControlsFeature Feature, ActionMap Map) Flight()
    {
        var feature = new ControlsFeature();
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        feature.AddSeat(1, profile, _ => Pad, true);
        feature.Context = InputContext.Flight;
        return (feature, profile.Map(InputContext.Flight));
    }

    // The first action of the context on screen that ships with a binding, so a context-blind test
    // has something real to break and put back in each of the three.
    private static InputAction FirstBound(ControlsFeature feature)
    {
        foreach (var action in feature.Actions)
        {
            if (feature.Bindings(action).Count > 0)
                return action;
        }

        return feature.Actions[0];
    }

    private static int IndexOf(ControlsFeature feature, InputAction action)
    {
        var actions = feature.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] == action)
                return i;
        }

        return 0;
    }

    private static Binding Key(Key key) => new(DeviceId.Keyboard, BindingControl.Key((int)key));

    private static Binding Button(JoyButton button) => new(Pad, BindingControl.Button((int)button));

    private static Binding Axis(JoyAxis axis, int sign, float deadzone) =>
        new(Pad, BindingControl.Axis((int)axis, sign, deadzone));

    private sealed class FakeDevices : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public HashSet<(DeviceId Device, int Index)> Buttons { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => Buttons.Contains((device, button));

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) => 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}
