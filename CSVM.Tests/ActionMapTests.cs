using System.Collections.Generic;
using CSVM.Bindings;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The named-action layer over the binding model: a control belongs to one action at a time and
/// changing owner takes it off the loser, a tick's answers are resolved once and stay put until the
/// next poll, and two players' maps are independent. Engine-free, over the same kind of fake device
/// state <see cref="BindingModelTests"/> uses.
/// </summary>
public class ActionMapTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("030000005e040000e002000000007801");
    private static readonly DeviceId OtherPad = DeviceId.Joypad("03000000100800000100000000000000");

    [Fact]
    public void AssigningAControlTakesItOffThePreviousActionAndNamesTheLoser()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));

        Assert.Null(map.Assign(InputAction.FireGuns, space));
        Assert.Equal(InputAction.FireGuns, map.Assign(InputAction.FireRockets, space));

        Assert.Empty(map.Bindings(InputAction.FireGuns));
        Assert.Single(map.Bindings(InputAction.FireRockets));
    }

    [Fact]
    public void AStolenControlFiresOnlyItsNewAction()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        map.Assign(InputAction.FireGuns, space);
        map.Assign(InputAction.FireRockets, space);

        var state = new FakeDevices();
        state.Keys.Add(32);
        var snapshot = map.Resolve(state);

        Assert.False(snapshot.Held(InputAction.FireGuns));
        Assert.True(snapshot.Held(InputAction.FireRockets));
    }

    [Fact]
    public void TheStealLeavesTheLosersOtherBindingsAlone()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        var padB = new Binding(Pad, BindingControl.Button(1));
        map.Assign(InputAction.FireGuns, space);
        map.Assign(InputAction.FireGuns, padB);

        Assert.Equal(InputAction.FireGuns, map.Assign(InputAction.Nitro, space));

        Assert.Equal(new[] { padB }, map.Bindings(InputAction.FireGuns));
    }

    [Fact]
    public void ReassigningToTheSameActionIsNotASteal()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        map.Assign(InputAction.FireGuns, space);

        Assert.Null(map.Assign(InputAction.FireGuns, space));
        Assert.Single(map.Bindings(InputAction.FireGuns));
    }

    [Fact]
    public void RebindingTheSameAxisAdjustsItsDeadzoneRatherThanStackingACopy()
    {
        var map = new ActionMap();
        map.Assign(InputAction.ThrottleUp, new Binding(Pad, BindingControl.Axis(4, 1, 0.2f)));
        map.Assign(InputAction.ThrottleUp, new Binding(Pad, BindingControl.Axis(4, 1, 0.6f)));

        var only = Assert.Single(map.Bindings(InputAction.ThrottleUp));
        Assert.Equal(0.6f, only.Control.Deadzone);
    }

    [Fact]
    public void TheOtherHalfOfAnAxisIsADifferentControlAndIsNotStolen()
    {
        var map = new ActionMap();
        map.Assign(InputAction.ThrottleUp, new Binding(Pad, BindingControl.Axis(4, 1, 0.2f)));

        Assert.Null(map.Assign(InputAction.ThrottleDown, new Binding(Pad, BindingControl.Axis(4, -1, 0.2f))));
        Assert.Single(map.Bindings(InputAction.ThrottleUp));
        Assert.Single(map.Bindings(InputAction.ThrottleDown));
    }

    [Fact]
    public void AScreenCanAskWhoOwnsAControlBeforeCommittingTheSteal()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        map.Assign(InputAction.FireGuns, space);

        Assert.True(map.TryFindOwner(space, out var owner));
        Assert.Equal(InputAction.FireGuns, owner);
        Assert.False(map.TryFindOwner(new Binding(Pad, BindingControl.Button(3)), out _));
        Assert.Single(map.Bindings(InputAction.FireGuns));
    }

    [Fact]
    public void UnassigningDropsOneControlAndTouchesNoOtherAction()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        var padB = new Binding(Pad, BindingControl.Button(1));
        map.Assign(InputAction.FireGuns, space);
        map.Assign(InputAction.FireRockets, padB);

        Assert.True(map.Unassign(InputAction.FireGuns, space));
        Assert.False(map.Unassign(InputAction.FireGuns, space));
        Assert.Empty(map.Bindings(InputAction.FireGuns));
        Assert.Single(map.Bindings(InputAction.FireRockets));
    }

    [Fact]
    public void TheSnapshotAnswersTheSameTwiceInOneTickWhateverTheHardwareDoes()
    {
        var map = new ActionMap();
        map.Assign(InputAction.FireGuns, new Binding(DeviceId.Keyboard, BindingControl.Key(32)));
        var state = new FakeDevices();
        state.Keys.Add(32);
        var player = new PlayerActions(map, true);
        player.Poll(state);

        Assert.True(player.Held(InputAction.FireGuns));
        state.Keys.Clear();
        Assert.True(player.Held(InputAction.FireGuns));

        player.Poll(state);
        Assert.False(player.Held(InputAction.FireGuns));
    }

    [Fact]
    public void TwoPlayersMapsResolveTheSameControlIndependently()
    {
        var one = new ActionMap();
        var two = new ActionMap();
        var padB = new Binding(Pad, BindingControl.Button(1));
        one.Assign(InputAction.FireGuns, padB);
        two.Assign(InputAction.Nitro, padB);

        var state = new FakeDevices();
        state.Buttons.Add((Pad, 1));
        var first = one.Resolve(state);
        var second = two.Resolve(state);

        Assert.True(first.Held(InputAction.FireGuns));
        Assert.False(first.Held(InputAction.Nitro));
        Assert.False(second.Held(InputAction.FireGuns));
        Assert.True(second.Held(InputAction.Nitro));
    }

    [Fact]
    public void AStealOnOnePlayersMapLeavesTheOtherPlayersUntouched()
    {
        var one = new ActionMap();
        var two = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        one.Assign(InputAction.FireGuns, space);
        two.Assign(InputAction.FireGuns, space);

        two.Assign(InputAction.FireRockets, space);

        Assert.Single(one.Bindings(InputAction.FireGuns));
        Assert.Empty(two.Bindings(InputAction.FireGuns));
    }

    [Fact]
    public void APadOnlySeatKeepsItsKeyboardBindingsAndReadsNoneOfThem()
    {
        var map = new ActionMap();
        map.Assign(InputAction.FireGuns, new Binding(DeviceId.Keyboard, BindingControl.Key(32)));
        map.Assign(InputAction.FireGuns, new Binding(Pad, BindingControl.Button(1)));
        var state = new FakeDevices();
        state.Keys.Add(32);
        var player = new PlayerActions(map, false);

        player.Poll(state);
        Assert.False(player.Held(InputAction.FireGuns));
        Assert.Equal(2, map.Bindings(InputAction.FireGuns).Count);

        state.Buttons.Add((Pad, 1));
        player.Poll(state);
        Assert.True(player.Held(InputAction.FireGuns));
    }

    [Fact]
    public void AnAxisPairReadsAsOneSignedNumberAndBothEndsHeldReadsZero()
    {
        var map = new ActionMap();
        map.Assign(InputAction.PitchUp, new Binding(DeviceId.Keyboard, BindingControl.Key(87)));
        map.Assign(InputAction.PitchDown, new Binding(DeviceId.Keyboard, BindingControl.Key(83)));
        var state = new FakeDevices();
        var player = new PlayerActions(map, true);

        state.Keys.Add(87);
        player.Poll(state);
        Assert.Equal(1f, player.Axis(InputAction.PitchUp, InputAction.PitchDown));

        state.Keys.Add(83);
        player.Poll(state);
        Assert.Equal(0f, player.Axis(InputAction.PitchUp, InputAction.PitchDown));
    }

    [Fact]
    public void AnAxisBoundToADigitalActionReadsItsTravelAndAButtonReadsOne()
    {
        var map = new ActionMap();
        map.Assign(InputAction.ThrottleUp, new Binding(Pad, BindingControl.Axis(4, 1, 0f)));
        map.Assign(InputAction.ThrottleDown, new Binding(Pad, BindingControl.Button(2)));
        var state = new FakeDevices();
        state.Axes[(Pad, 4)] = 0.25f;
        state.Buttons.Add((Pad, 2));

        var snapshot = map.Resolve(state);

        Assert.True(snapshot.Held(InputAction.ThrottleUp));
        Assert.Equal(0.25f, snapshot.Value(InputAction.ThrottleUp), 5);
        Assert.Equal(1f, snapshot.Value(InputAction.ThrottleDown));
    }

    [Fact]
    public void AnUnboundActionReadsNothingAndClearingOneUnbindsIt()
    {
        var map = new ActionMap();
        map.Assign(InputAction.Nitro, new Binding(Pad, BindingControl.Button(3)));
        var state = new FakeDevices();
        state.Buttons.Add((Pad, 3));

        Assert.False(map.Resolve(state).Held(InputAction.Respawn));
        Assert.Equal(0f, map.Resolve(state).Value(InputAction.Respawn));

        map.Clear(InputAction.Nitro);
        Assert.False(map.Resolve(state).Held(InputAction.Nitro));
        Assert.Empty(map.BoundActions);
    }

    [Fact]
    public void ACloneEditsIndependentlyOfTheMapItCameFrom()
    {
        var map = new ActionMap();
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key(32));
        map.Assign(InputAction.FireGuns, space);

        var copy = map.Clone();
        copy.Assign(InputAction.FireRockets, space);

        Assert.Single(map.Bindings(InputAction.FireGuns));
        Assert.Empty(copy.Bindings(InputAction.FireGuns));
        Assert.Empty(map.Bindings(InputAction.FireRockets));
    }

    [Fact]
    public void TheSameButtonOnAnotherPadIsADifferentControlAndStealsNothing()
    {
        var map = new ActionMap();
        map.Assign(InputAction.FireGuns, new Binding(Pad, BindingControl.Button(1)));

        Assert.Null(map.Assign(InputAction.FireRockets, new Binding(OtherPad, BindingControl.Button(1))));
        Assert.Single(map.Bindings(InputAction.FireGuns));
        Assert.Single(map.Bindings(InputAction.FireRockets));
    }

    private sealed class FakeDevices : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public HashSet<(DeviceId Device, int Index)> Buttons { get; } = new();

        public Dictionary<(DeviceId Device, int Index), float> Axes { get; } = new();

        public Dictionary<(DeviceId Device, int Index), HatDirection> Hats { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => Buttons.Contains((device, button));

        public float AxisValue(DeviceId device, int axis) =>
            Axes.TryGetValue((device, axis), out float value) ? value : 0f;

        public HatDirection HatState(DeviceId device, int hat) =>
            Hats.TryGetValue((device, hat), out var state) ? state : HatDirection.None;
    }
}
