using CSVM.Bindings;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The index-to-identity table, engine-free: <see cref="DeviceRegistry.Refresh"/> takes synthetic
/// (index, guid, name) tuples in place of a real joypad, so the hot-plug rule the plan's Verify
/// asks for (unplug, replug into another port, restart) is checkable here as three refreshes rather
/// than at a physical pad. What still needs a real pad: that Godot actually reports this GUID/name
/// pair and raises <c>joy_connection_changed</c> on a real unplug/replug, which
/// <see cref="GodotDeviceState"/> reads but this suite does not exercise.
/// </summary>
public class DeviceRegistryTests
{
    private const string GuidA = "030000005e040000e002000000007801";
    private const string GuidB = "03000000100800000100000000000000";

    [Fact]
    public void AConnectedPadResolvesByItsGuidRegardlessOfIndex()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (2, GuidA, "Pad A") });

        Assert.Equal(2, registry.IndexOf(DeviceId.Joypad(GuidA)));
    }

    [Fact]
    public void UnpluggingLeavesTheBindingResolvableToNullRatherThanThrowing()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, GuidA, "Pad A") });
        Assert.Equal(0, registry.IndexOf(DeviceId.Joypad(GuidA)));

        registry.Refresh(System.Array.Empty<(int, string, string)>());

        Assert.Null(registry.IndexOf(DeviceId.Joypad(GuidA)));
    }

    [Fact]
    public void ReplugIntoADifferentPortResolvesAgainAtTheNewIndex()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, GuidA, "Pad A") });
        registry.Refresh(System.Array.Empty<(int, string, string)>());

        registry.Refresh(new[] { (3, GuidA, "Pad A") });

        Assert.Equal(3, registry.IndexOf(DeviceId.Joypad(GuidA)));
    }

    [Fact]
    public void ARestartRebuildsTheSameIdentityFromAFreshRegistry()
    {
        var afterRestart = new DeviceRegistry();
        afterRestart.Refresh(new[] { (1, GuidA, "Pad A") });

        Assert.Equal(1, afterRestart.IndexOf(DeviceId.Joypad(GuidA)));
    }

    [Fact]
    public void ABlankGuidFallsBackToTheName()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, "", "Generic USB Joystick") });

        Assert.Equal(0, registry.IndexOf(DeviceId.Joypad("Generic USB Joystick")));
    }

    [Fact]
    public void APadWithNeitherGuidNorNameIsDroppedRatherThanGivenABlankIdentity()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, "", "  ") });

        Assert.Equal(default, registry.IdentityOf(0));
    }

    [Fact]
    public void TwoPadsSharingOneGuidLeaveTheSecondUnresolved()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, GuidA, "Pad A"), (1, GuidA, "Pad A (clone)") });

        Assert.Equal(0, registry.IndexOf(DeviceId.Joypad(GuidA)));
        Assert.Equal(default, registry.IdentityOf(1));
    }

    [Fact]
    public void IdentityOfAnUnknownIndexIsTheNoneDevice()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, GuidA, "Pad A") });

        var identity = registry.IdentityOf(5);

        Assert.True(identity.IsNone);
    }

    [Fact]
    public void IndexOfNeverResolvesForTheKeyboard()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, GuidA, "Pad A") });

        Assert.Null(registry.IndexOf(DeviceId.Keyboard));
    }

    [Fact]
    public void SecondPadStaysIndependentOfTheFirstByGuid()
    {
        var registry = new DeviceRegistry();
        registry.Refresh(new[] { (0, GuidA, "Pad A"), (1, GuidB, "Pad B") });

        Assert.Equal(0, registry.IndexOf(DeviceId.Joypad(GuidA)));
        Assert.Equal(1, registry.IndexOf(DeviceId.Joypad(GuidB)));
        Assert.Equal(DeviceId.Joypad(GuidA), registry.IdentityOf(0));
        Assert.Equal(DeviceId.Joypad(GuidB), registry.IdentityOf(1));
    }
}
