using System;
using System.Collections.Generic;
using CSVM.Net;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The address a hosting door names to its guests, the words its boards and lobby rows show for
/// it, and its copy onto the clipboard. The addresses and the clipboard are seams, so every case here
/// names its own and records what was copied.
/// </summary>
public class HostAddressDoorTests
{
    private const string Stable = "2a04:6ec0:232:6640:feb1:ff80:9ed7:dd90";
    private const string Lan = "192.168.178.20";
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void AHostOnTheDefaultPortNamesItsStableAddressBareAndCopiesIt()
    {
        var copied = new List<string>();
        var door = Door(() => Stable, () => Lan, copied.Add);
        door.OpenHost(7);

        Assert.Equal(Stable, door.Reach.HostIpv6);
        Assert.Equal(Lan, door.Reach.HostLanIpv4);
        Assert.Equal(Stable, door.Reach.GuestAddress);
        Assert.Equal($"IPv6  {Stable}  {CoopDoorText.CopyPress}", CoopDoorText.HostAddressLine(door));
        Assert.Equal($"Guests type {Stable}, or {Lan} on this network. {CoopDoorText.CopyPress} copies {Stable}.",
            CoopDoorText.HostAddressStatus(door));

        Assert.True(door.Reach.CopyGuestAddress());
        Assert.Equal(new[] { Stable }, copied);
        Assert.Equal(1, door.Reach.Copies);
        Assert.Equal($"IPv6  {Stable}  copied", CoopDoorText.HostAddressLine(door));
        Assert.EndsWith($"{Stable} is copied.", CoopDoorText.HostAddressStatus(door), StringComparison.Ordinal);
    }

    [Fact]
    public void AHostOffTheDefaultPortBracketsTheIpv6AddressWithIt()
    {
        var copied = new List<string>();
        var door = Door(() => Stable, () => Lan, copied.Add);
        door.StepPort(3);
        door.OpenHost(7);
        int port = NetPlayFeature.DefaultPort + 3;

        Assert.Equal($"[{Stable}]:{port}", door.Reach.GuestAddress);
        Assert.Contains($"[{Stable}]:{port}", CoopDoorText.HostAddressLine(door), StringComparison.Ordinal);
        Assert.Contains($"or {Lan}:{port} on this network", CoopDoorText.HostAddressStatus(door), StringComparison.Ordinal);
        Assert.True(door.Reach.CopyGuestAddress());
        Assert.Equal(new[] { $"[{Stable}]:{port}" }, copied);
    }

    [Fact]
    public void AHostWithNoGlobalIpv6SaysSoAndOffersItsLanAddress()
    {
        var copied = new List<string>();
        var door = Door(() => null, () => Lan, copied.Add);
        door.OpenHost(7);

        Assert.Null(door.Reach.HostIpv6);
        Assert.Equal(Lan, door.Reach.GuestAddress);
        Assert.Equal($"{CoopDoorText.NoIpv6}  LAN {Lan}  {CoopDoorText.CopyPress}", CoopDoorText.HostAddressLine(door));
        Assert.StartsWith($"This machine has no global IPv6 address; guests on this network type {Lan}.",
            CoopDoorText.HostAddressStatus(door), StringComparison.Ordinal);

        // ABLE-TO-FAIL CONTROL: with neither address there is nothing to copy, and the line says so.
        var bare = Door(() => null, () => null, copied.Add);
        bare.OpenHost(7);
        Assert.Equal("", bare.Reach.GuestAddress);
        Assert.False(bare.Reach.CopyGuestAddress());
        Assert.Equal(CoopDoorText.NoIpv6, CoopDoorText.HostAddressLine(bare));
        Assert.Empty(copied);

        Assert.True(door.Reach.CopyGuestAddress());
        Assert.Equal(new[] { Lan }, copied);
    }

    [Fact]
    public void ARouterMappingOutranksTheLanAddressButNotTheStableIpv6One()
    {
        var mapped = new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, NetPlayFeature.DefaultPort, "203.0.113.9", "mapped");
        var door = Door(() => null, () => Lan, _ => { }, mapped);
        door.OpenHost(7);
        WaitForMapping(door);
        // A mapping's external port can differ from the local one, so it is always written out.
        Assert.Equal($"203.0.113.9:{NetPlayFeature.DefaultPort}", door.Reach.GuestAddress);

        var both = Door(() => Stable, () => Lan, _ => { }, mapped);
        both.OpenHost(7);
        WaitForMapping(both);
        Assert.Equal(Stable, both.Reach.GuestAddress);
    }

    [Fact]
    public void ThePinholeClauseLeavesOutTheAddressTheStatusAlreadyNames()
    {
        var door = PinholeDoor(() => Stable, new UpnpPinholeResult(UpnpPinholeOutcome.Opened, 0, Stable, ""));
        int port = NetPlayFeature.DefaultPort;
        Assert.Equal($"IPv6: router opened UDP port {port}.", CoopDoorText.HostPinholeStatus(door));
        Assert.DoesNotContain(Stable, CoopDoorText.HostPinholeStatus(door), StringComparison.Ordinal);
        Assert.Contains(Stable, CoopDoorText.HostAddressStatus(door), StringComparison.Ordinal);

        var none = PinholeDoor(() => null, new UpnpPinholeResult(UpnpPinholeOutcome.NoAddress, 0, "", ""));
        Assert.Equal("", CoopDoorText.HostPinholeStatus(none));

        // ABLE-TO-FAIL CONTROL: a door that names no address keeps the pinhole's own full clause.
        var plain = PinholeDoor(null, new UpnpPinholeResult(UpnpPinholeOutcome.Opened, 0, Stable, ""));
        Assert.Equal(CoopDoorText.PinholeStatus(plain.Router.Pinhole!.Value), CoopDoorText.HostPinholeStatus(plain));
        Assert.Contains(Stable, CoopDoorText.HostPinholeStatus(plain), StringComparison.Ordinal);
    }

    [Fact]
    public void ADoorWithoutTheSeamsNamesNoAddressAndCopiesNothing()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(3));
        var door = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]);
        door.OpenHost(7);

        Assert.False(door.Reach.NamesHostAddress);
        Assert.Equal("", CoopDoorText.HostAddressLine(door));
        Assert.Equal("", CoopDoorText.HostAddressStatus(door));
        Assert.Empty(CoopDoorText.HostLobbyLines(door));
        Assert.False(door.Reach.CopyGuestAddress());

        // ABLE-TO-FAIL CONTROL: an address seam with no clipboard names the address but copies nothing.
        var named = Door(() => Stable, () => Lan, null);
        named.OpenHost(7);
        Assert.NotEqual("", CoopDoorText.HostAddressLine(named));
        Assert.False(named.Reach.CopyGuestAddress());
        Assert.Equal(0, named.Reach.Copies);
    }

    [Fact]
    public void AnAddressIsForgottenWhenTheHostClosesAndReadAgainWhenItReopens()
    {
        string? now = Stable;
        var door = Door(() => now, () => Lan, _ => { });
        door.OpenHost(7);
        Assert.True(door.Reach.CopyGuestAddress());
        door.Close();

        Assert.Null(door.Reach.HostIpv6);
        Assert.Equal("", door.Reach.GuestAddress);
        Assert.Equal("", CoopDoorText.HostAddressLine(door));

        now = null;
        door.OpenHost(7);
        Assert.Null(door.Reach.HostIpv6);
        Assert.Equal(0, door.Reach.Copies);
    }

    [Fact]
    public void ACoopHostsBandCarriesTheAddressOnASecondLine()
    {
        var door = Door(() => Stable, () => Lan, _ => { });
        door.OpenCoopHost(NetSeats.MaxPlayers - 1);

        string[] lines = CoopDoorText.HostBand(door).Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("NETWORK OPEN", lines[0], StringComparison.Ordinal);
        Assert.Equal($"IPv6  {Stable}  {CoopDoorText.CopyPress}", lines[1]);

        // ABLE-TO-FAIL CONTROL: a band with no address seam stays one line.
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(5));
        var plain = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]);
        plain.OpenCoopHost(NetSeats.MaxPlayers - 1);
        Assert.DoesNotContain('\n', CoopDoorText.HostBand(plain));
    }

    [Fact]
    public void ADogfightHostWithNoMasterServerPinsTheAddressAndPostsNoChatNote()
    {
        var door = Door(() => Stable, () => Lan, _ => { });
        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);

        Assert.Equal(new[] { $"IPv6  {Stable}  {CoopDoorText.CopyPress}" }, CoopDoorText.HostLobbyLines(door));
        Assert.Empty(door.Dogfight!.Chat);

        var none = Door(() => null, () => null, _ => { });
        none.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        Assert.Equal(new[] { CoopDoorText.NoIpv6 }, CoopDoorText.HostLobbyLines(none));
        Assert.Empty(none.Dogfight!.Chat);
    }

    private static NetPlayFeature Door(Func<string?> ipv6, Func<string?> lan, Action<string>? copy, UpnpPortMapResult? map = null)
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(17));
        Func<int, UpnpPortMapResult>? mapper = map is { } m ? port => m with { Port = port } : null;
        return new NetPlayFeature(
            (_, _, _) => mesh[0],
            (_, _) => mesh[0],
            new RouterAccess(mapper, map is null ? null : _ => { }))
        {
            Reach = { StableIpv6 = ipv6, LanIpv4 = lan, CopyText = copy },
        };
    }

    // A host whose router answered the pinhole with the given result, waited for as a mapping is.
    private static NetPlayFeature PinholeDoor(Func<string?>? ipv6, UpnpPinholeResult pinhole)
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(19));
        var router = new RouterAccess(openPinhole: port => pinhole with { Port = port }, closePinhole: _ => { });
        var door = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0], router)
        {
            Reach = { StableIpv6 = ipv6, LanIpv4 = () => Lan },
        };
        door.OpenHost(7);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (door.Router.Pinhole == null && DateTime.UtcNow < deadline)
        {
            door.Step(0.016);
        }

        Assert.NotNull(door.Router.Pinhole);
        return door;
    }

    // The mapping lands on a step from a pool thread, so the wait is a wall-clock deadline.
    private static void WaitForMapping(NetPlayFeature door)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (door.Router.PortMap == null && DateTime.UtcNow < deadline)
        {
            door.Step(0.016);
        }

        Assert.NotNull(door.Router.PortMap);
    }
}
