using System;
using System.Collections.Generic;
using System.Linq;
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
        feature.AddSeat(1, profile, OnePad(), true);
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
        feature.AddSeat(1, BindingProfile.Defaults(Pad, readsKeyboard: true), OnePad(), true);

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
        feature.AddSeat(1, profile, OnePad(), true);

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
        feature.AddSeat(1, profile, OnePad(), true);
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
        feature.AddSeat(1, one, OnePad(), true);
        feature.AddSeat(2, two, OnePad(), false);

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
        feature.AddSeat(1, one, OnePad(), true);
        feature.AddSeat(2, two, OnePad(), false);

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
        feature.AddSeat(1, profile, OnePad(), true);
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
        feature.AddSeat(1, BindingProfile.Defaults(Pad, true), OnePad(), true);
        feature.AddSeat(2, BindingProfile.Defaults(Pad, false), OnePad(), false);

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
        feature.AddSeat(1, BindingProfile.Defaults(Pad, true), OnePad(), true);
        feature.AddSeat(2, BindingProfile.Defaults(Pad, false), OnePad(), false);

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

    /// <summary>The screen's three contexts do not share a pad identity, and a pad control has to
    /// be capturable in every one of them. This is the fact the reported defect fails: the screen
    /// read every context through the menu seat's state, so Flight and Camera saw an idle pad while
    /// Menu worked and the keyboard worked everywhere.</summary>
    [Fact]
    public void APadButtonIsCapturedInEveryContextOnThatContextsOwnIdentity()
    {
        var menuSeat = DeviceId.Joypad("menu-seat");
        var maps = new Dictionary<InputContext, ActionMap>
        {
            [InputContext.Flight] = DefaultBindings.MapFor(InputContext.Flight, Pad),
            [InputContext.Menu] = DefaultBindings.MapFor(InputContext.Menu, menuSeat),
            [InputContext.Camera] = DefaultBindings.MapFor(InputContext.Camera, Pad),
        };
        var devices = new FakeCaptureDevices(c => c == InputContext.Menu ? menuSeat : Pad);
        var feature = new ControlsFeature();
        feature.AddSeat(1, new BindingProfile(maps, true), devices, true);

        var bound = new Dictionary<InputContext, (InputAction Action, Binding Binding)>();
        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            feature.Context = context;
            var action = FirstBound(feature);
            var free = FreePadButton(feature, devices.PadOf(context));
            bound[context] = (action, new Binding(devices.PadOf(context), BindingControl.Button((int)free)));
            feature.Focus(IndexOf(feature, action));
            feature.MoveSlot(9);
            feature.BeginCapture();
            devices.State(context).Buttons.Add((int)free);

            Assert.True(feature.Poll());
            Assert.False(feature.Capturing);
            Assert.Null(feature.Pending);
            Assert.Contains(bound[context].Binding, feature.Bindings(action));
            devices.State(context).Buttons.Clear();
        }

        // Through Accept, so the identity that reaches the map a polling site holds is checked too.
        feature.Accept();
        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            Assert.Contains(bound[context].Binding, maps[context].Bindings(bound[context].Action));
        }
    }

    /// <summary>The whole hop the author exercises, in one fact: a pad button captured on the
    /// Flight rows, accepted, saved through the real serializer, read back the way
    /// <c>FlightController.LoadSavedKeymap</c> reads it, and resolved through a device state
    /// answering for the flight seat's own identity. A fix that restores capture but writes a row
    /// flight cannot resolve would pass every other fact here.</summary>
    [Fact]
    public void ACapturedPadControlSurvivesTheRoundTripToTheSeatThatFlies()
    {
        BindingProfile? saved = null;
        var feature = new ControlsFeature((_, profile) => saved = profile);
        var devices = new FakeCaptureDevices(_ => Pad);
        feature.AddSeat(1, BindingProfile.Defaults(Pad, readsKeyboard: true), devices, true);
        feature.Context = InputContext.Flight;
        feature.Focus(IndexOf(feature, InputAction.Nitro));
        feature.MoveSlot(9);
        var free = FreePadButton(feature, Pad);
        feature.BeginCapture();
        devices.State(InputContext.Flight).Buttons.Add((int)free);

        Assert.True(feature.Poll());
        Assert.Null(feature.Pending);
        feature.Accept();
        Assert.NotNull(saved);

        // Through a real file, since the store is what the launch read opens.
        var store = new BindingStore(TestData.TempDir());
        store.Save(1, saved!);
        // FlightController passes `default` for the pad, which leaves a stored row on the identity
        // it was written with, and its own seat state answers for DefaultBindings.AnyPad.
        var reloaded = store.Load(1, default, readsKeyboard: true).Map(InputContext.Flight);
        var expected = new Binding(Pad, BindingControl.Button((int)free));
        Assert.Contains(expected, reloaded.Bindings(InputAction.Nitro));

        var hardware = new FakeDevices(Pad);
        var actions = new PlayerActions(reloaded, readsKeyboard: true);
        actions.Poll(hardware);
        Assert.False(actions.Held(InputAction.Nitro));

        hardware.Buttons.Add((int)free);
        actions.Poll(hardware);
        Assert.True(actions.Held(InputAction.Nitro));
    }

    /// <summary>The other half of the identity rule, and the failure that would be worse than the
    /// one being fixed: a captured control has to be the same control as the shipped pad row it
    /// lands on, or <see cref="ActionMap.SameControl"/> sees no conflict, the steal rule goes blind
    /// and the player silently holds one button on two actions.</summary>
    [Fact]
    public void ACapturedPadButtonConflictsWithTheShippedPadRowSoTheStealRuleSeesIt()
    {
        var (feature, _, devices) = FlightSeat();
        var owner = OwnerOfPadButton(feature, JoyButton.A);
        var target = InputAction.Nitro;
        Assert.NotEqual(target, owner);

        feature.Focus(IndexOf(feature, target));
        feature.MoveSlot(9);
        feature.BeginCapture();
        devices.State(InputContext.Flight).Buttons.Add((int)JoyButton.A);

        Assert.True(feature.Poll());
        Assert.NotNull(feature.Pending);
        Assert.Contains(owner, feature.Pending!.Losers);
    }

    [Fact]
    public void ACaptureOnAControlAnotherActionHoldsStopsToNameItRatherThanBinding()
    {
        var (feature, _, devices) = FlightSeat();
        feature.Focus(IndexOf(feature, InputAction.Respawn));
        feature.MoveSlot(9);
        feature.BeginCapture();

        devices.State(InputContext.Flight).Keys.Add((int)Godot.Key.Space);
        Assert.True(feature.Poll());

        Assert.False(feature.Capturing);
        Assert.Equal(new[] { InputAction.FireGuns }, feature.Pending!.Losers);
        Assert.Contains(Key(Godot.Key.Space), feature.Bindings(InputAction.FireGuns));
    }

    [Fact]
    public void NoShippedActionHidesABindingAtTheRowsOwnLimit()
    {
        var feature = new ControlsFeature();
        feature.AddSeat(1, BindingProfile.Defaults(Pad, true), OnePad(), true);

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
        feature.BeginCapture();

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
        feature.AddSeat(1, BindingProfile.Defaults(Pad, readsKeyboard: true), OnePad(), true);
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
        var (feature, profile, _) = FlightSeat();
        return (feature, profile.Map(InputContext.Flight));
    }

    private static (ControlsFeature Feature, BindingProfile Profile, FakeCaptureDevices Devices) FlightSeat()
    {
        var feature = new ControlsFeature();
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var devices = new FakeCaptureDevices(_ => Pad);
        feature.AddSeat(1, profile, devices, true);
        feature.Context = InputContext.Flight;
        return (feature, profile, devices);
    }

    // A seat whose three contexts all sit on the one pad identity, which is what a seat registered
    // from BindingProfile.Defaults holds.
    private static FakeCaptureDevices OnePad() => new(_ => Pad);

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

    // A pad button no action of the context on screen holds, so a capture on it binds rather than
    // raising a steal. Found rather than named, because the shipped set may grow another row.
    private static JoyButton FreePadButton(ControlsFeature feature, DeviceId seatPad)
    {
        for (int i = 0; i < (int)JoyButton.SdlMax; i++)
        {
            var candidate = new Binding(seatPad, BindingControl.Button(i));
            if ((JoyButton)i != ControlCapture.CancelButton
                && feature.Actions.All(a => !feature.Bindings(a).Contains(candidate)))
            {
                return (JoyButton)i;
            }
        }

        throw new InvalidOperationException("every pad button this context can report is already bound");
    }

    // Which action of the context on screen ships holding that pad button, so a test can capture a
    // control the shipped set already owns and watch the steal rule find it.
    private static InputAction OwnerOfPadButton(ControlsFeature feature, JoyButton button)
    {
        foreach (var action in feature.Actions)
        {
            if (feature.Bindings(action).Contains(Button(button)))
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

    /// <summary>A seat's capture readers in the shape production builds them
    /// (<see cref="SeatCaptureDevices"/>): one state per context, each answering for that context's
    /// own identity and for no other, which is what the shipping <see cref="SeatDeviceState"/>
    /// does. Both come from the one <c>padOf</c>, so a test cannot hand the feature a reader and an
    /// identity that disagree, and neither can a presentation.</summary>
    private sealed class FakeCaptureDevices : ICaptureDevices
    {
        private readonly Func<InputContext, DeviceId> _padOf;
        private readonly Dictionary<InputContext, FakeDevices> _states = new();

        public FakeCaptureDevices(Func<InputContext, DeviceId> padOf)
        {
            _padOf = padOf;
            foreach (var context in System.Enum.GetValues<InputContext>())
                _states[context] = new FakeDevices(padOf(context));
        }

        public DeviceId PadOf(InputContext context) => _padOf(context);

        public IDeviceState For(InputContext context) => _states[context];

        /// <summary>That context's hardware, for a test to press something on.</summary>
        public FakeDevices State(InputContext context) => _states[context];
    }

    /// <summary>One context's hardware, answering for one joypad identity and nothing else. A fake
    /// that answered for whichever pad it was asked about would be more permissive than
    /// <see cref="SeatDeviceState"/>, and a screen reading pads through a state built on another
    /// seat's identity would then pass here and capture nothing at the controls.</summary>
    private sealed class FakeDevices : IDeviceState
    {
        private readonly DeviceId _seatPad;

        public FakeDevices(DeviceId seatPad) => _seatPad = seatPad;

        public HashSet<int> Keys { get; } = new();

        public HashSet<int> Buttons { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) =>
            device == _seatPad && Buttons.Contains(button);

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) => 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}
