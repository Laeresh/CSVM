using System;
using System.Collections.Generic;
using CSVM.Bindings;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The binding model's resolution rules, engine-free: a set fires on any one of its bindings, an
/// axis fires only strictly past its own deadzone and on its own half of the travel, and one hat
/// direction reads independently of the other three. The fake state below is the whole hardware
/// side, which is the point of <see cref="IDeviceState"/> being an interface.
/// </summary>
public class BindingModelTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("030000005e040000e002000000007801");
    private static readonly DeviceId OtherPad = DeviceId.Joypad("03000000100800000100000000000000");

    [Fact]
    public void AnActionFiresOnAnyOneOfItsThreeBindings()
    {
        var set = new BindingSet(new[]
        {
            new Binding(DeviceId.Keyboard, BindingControl.Key(32)),
            new Binding(Pad, BindingControl.Button(0)),
            new Binding(OtherPad, BindingControl.Button(5)),
        });
        var state = new FakeDevices();

        Assert.False(set.Resolve(state).Pressed);

        state.Keys.Add(32);
        Assert.True(set.Resolve(state).Pressed);

        state.Keys.Clear();
        state.Buttons.Add((Pad, 0));
        Assert.True(set.Resolve(state).Pressed);

        state.Buttons.Clear();
        state.Buttons.Add((OtherPad, 5));
        Assert.True(set.Resolve(state).Pressed);
    }

    [Fact]
    public void ADeviceIdentityIsPartOfTheBindingSoTheSameButtonOnAnotherPadIsSilent()
    {
        var set = new BindingSet(new[] { new Binding(Pad, BindingControl.Button(0)) });
        var state = new FakeDevices();
        state.Buttons.Add((OtherPad, 0));

        Assert.False(set.Resolve(state).Pressed);
    }

    [Fact]
    public void AnAxisAtPointFourWithDeadzonePointFiveDoesNotFireAndAtPointSixDoes()
    {
        var binding = new Binding(Pad, BindingControl.Axis(1, 1, 0.5f));
        var state = new FakeDevices();

        state.Axes[(Pad, 1)] = 0.4f;
        Assert.False(binding.Resolve(state).Pressed);

        state.Axes[(Pad, 1)] = 0.6f;
        var read = binding.Resolve(state);
        Assert.True(read.Pressed);

        // The raw travel, not the remainder rescaled onto [0, 1]: no polling site this seam replaced
        // rescaled, so keeping the deadzone number was not enough to keep the behaviour.
        Assert.Equal(0.6f, read.Value, 5);
    }

    [Fact]
    public void AnAxisFiresOnlyOnTheHalfOfTheTravelItsSignNames()
    {
        var positive = new Binding(Pad, BindingControl.Axis(1, 1, 0.2f));
        var negative = new Binding(Pad, BindingControl.Axis(1, -1, 0.2f));
        var state = new FakeDevices();
        state.Axes[(Pad, 1)] = -0.9f;

        Assert.False(positive.Resolve(state).Pressed);
        Assert.True(negative.Resolve(state).Pressed);
    }

    [Fact]
    public void AButtonDrivesAnAnalogueReadAndAnAxisDrivesADigitalOne()
    {
        var state = new FakeDevices();
        state.Buttons.Add((Pad, 3));
        state.Axes[(Pad, 2)] = 1f;

        Assert.Equal(1f, new Binding(Pad, BindingControl.Button(3)).Resolve(state).Value);
        Assert.True(new Binding(Pad, BindingControl.Axis(2, 1, 0f)).Resolve(state).Pressed);
    }

    [Fact]
    public void AMouseButtonResolvesLikeAnyOtherDigitalControl()
    {
        var binding = new Binding(DeviceId.Mouse, BindingControl.Mouse(2));
        var state = new FakeDevices();

        Assert.False(binding.Resolve(state).Pressed);

        state.MouseButtons.Add(2);
        var read = binding.Resolve(state);
        Assert.True(read.Pressed);
        Assert.Equal(1f, read.Value);
    }

    [Fact]
    public void AHatDirectionResolvesIndependentlyOfTheOtherThreeOnTheSameHat()
    {
        var state = new FakeDevices();
        state.Hats[(Pad, 0)] = HatDirection.Up | HatDirection.Right;

        Assert.True(new Binding(Pad, BindingControl.Hat(0, HatDirection.Up)).Resolve(state).Pressed);
        Assert.True(new Binding(Pad, BindingControl.Hat(0, HatDirection.Right)).Resolve(state).Pressed);
        Assert.False(new Binding(Pad, BindingControl.Hat(0, HatDirection.Down)).Resolve(state).Pressed);
        Assert.False(new Binding(Pad, BindingControl.Hat(0, HatDirection.Left)).Resolve(state).Pressed);
    }

    [Fact]
    public void ASetTakesTheDeepestDeflectionAnyBindingReports()
    {
        var set = new BindingSet(new[]
        {
            new Binding(Pad, BindingControl.Axis(0, 1, 0f)),
            new Binding(Pad, BindingControl.Button(1)),
        });
        var state = new FakeDevices();
        state.Axes[(Pad, 0)] = 0.3f;
        state.Buttons.Add((Pad, 1));

        Assert.Equal(1f, set.Resolve(state).Value);
    }

    [Fact]
    public void AnAbsentDeviceLeavesTheBindingInPlaceAndResolvesFalse()
    {
        var set = new BindingSet(new[] { new Binding(Pad, BindingControl.Button(0)) });

        Assert.False(set.Resolve(new FakeDevices()).Pressed);
        Assert.Single(set.Bindings);
    }

    [Fact]
    public void TheSameControlAddedTwiceIsOneBinding()
    {
        var set = new BindingSet();
        var binding = new Binding(DeviceId.Keyboard, BindingControl.Key(65));

        Assert.True(set.Add(binding));
        Assert.False(set.Add(binding));
        Assert.Equal(1, set.Count);
        Assert.True(set.Remove(binding));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void ACloneEditsIndependentlyOfItsSource()
    {
        var set = new BindingSet(new[] { new Binding(DeviceId.Keyboard, BindingControl.Key(65)) });
        var copy = set.Clone();
        copy.Add(new Binding(DeviceId.Keyboard, BindingControl.Key(66)));

        Assert.Equal(1, set.Count);
        Assert.Equal(2, copy.Count);
    }

    [Fact]
    public void TwoIdenticalBindingsAreEqualAndTwoDevicesAreNot()
    {
        Assert.Equal(
            new Binding(Pad, BindingControl.Axis(1, -1, 0.25f)),
            new Binding(DeviceId.Joypad("030000005e040000e002000000007801"), BindingControl.Axis(1, -1, 0.25f)));
        Assert.NotEqual(new Binding(Pad, BindingControl.Button(0)), new Binding(OtherPad, BindingControl.Button(0)));
        Assert.NotEqual(DeviceId.Keyboard, default);
    }

    [Fact]
    public void TheFactoriesRejectAControlThatCannotBeResolved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BindingControl.Axis(0, 0, 0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => BindingControl.Axis(0, 1, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => BindingControl.Hat(0, HatDirection.Up | HatDirection.Right));
        Assert.Throws<ArgumentOutOfRangeException>(() => BindingControl.Hat(0, HatDirection.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => BindingControl.Key(-1));
        Assert.Throws<ArgumentException>(() => DeviceId.Joypad("  "));
    }

    private sealed class FakeDevices : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public HashSet<(DeviceId Device, int Index)> Buttons { get; } = new();

        public HashSet<int> MouseButtons { get; } = new();

        public Dictionary<(DeviceId Device, int Index), float> Axes { get; } = new();

        public Dictionary<(DeviceId Device, int Index), HatDirection> Hats { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => Buttons.Contains((device, button));

        public bool IsMouseButtonDown(DeviceId device, int button) =>
            device == DeviceId.Mouse && MouseButtons.Contains(button);

        public float AxisValue(DeviceId device, int axis) =>
            Axes.TryGetValue((device, axis), out float value) ? value : 0f;

        public HatDirection HatState(DeviceId device, int hat) =>
            Hats.TryGetValue((device, hat), out var state) ? state : HatDirection.None;
    }
}
