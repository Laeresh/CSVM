using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The door's boot and optional password over the loopback. A host removes a guest from a Dogfight
/// lobby or a co-op session. The guest is told why and cannot return from the same address. A
/// session with a password admits only a guest that answers it.
/// </summary>
public class NetPlayFeatureBootTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void AHostBootsADogfightGuestAndEveryoneElseReadsTheNotice()
    {
        // The loopback links every end to every other, and a real guest links only to its host.
        var mesh = LoopbackTransport.Mesh(3, Clean, new Random(101));
        mesh[1].Disconnect(mesh[2].LocalPeer);
        var host = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]) { Identity = { PlayerName = "Zachary" } };
        var booted = Guest(mesh[1], "Nathan");
        var stays = Guest(mesh[2], "Sheila");
        host.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        booted.OpenJoin();
        stays.OpenJoin();
        Pump(host, booted, stays);
        booted.Dogfight!.Show();
        stays.Dogfight!.Show();
        Pump(host, booted, stays);
        Assert.Equal(new[] { "Zachary", "Nathan", "Sheila" }, host.Dogfight!.Players.Select(p => p.Name));

        int peer = mesh[1].LocalPeer;
        Assert.Equal(peer, host.Dogfight.PeerAt(1));
        Assert.True(host.Boot(peer));
        Pump(host, booted, stays);

        Assert.Equal(NetDoorStage.Failed, booted.Stage);
        Assert.Equal(CoopDoorText.Booted, booted.Fault);
        Assert.Equal(new[] { "Zachary", "Sheila" }, host.Dogfight.Players.Select(p => p.Name));
        Assert.Contains(new DogfightChatLine("", "[Nathan was booted from the game.]"), host.Dogfight.Chat);
        Assert.Contains(new DogfightChatLine("", "[Nathan was booted from the game.]"), stays.Dogfight.Chat);
        Assert.Equal(new[] { "Zachary", "Sheila" }, stays.Dogfight.Players.Select(p => p.Name));

        // The host hangs up once the grace has passed, and cannot boot itself or a stranger.
        host.Step(NetAdmission.RefuseGraceSeconds + 0.1);
        host.Step(0.016);
        Assert.DoesNotContain(peer, mesh[0].Peers);
        Assert.False(host.Boot(peer));
        Assert.False(host.Boot(mesh[0].LocalPeer));
        Assert.Equal(-1, host.Dogfight.PeerAt(0));

        // ABLE-TO-FAIL CONTROL: a guest cannot boot anyone.
        Assert.False(stays.Boot(mesh[0].LocalPeer));
        Assert.Equal(NetDoorStage.Joined, stays.Stage);
    }

    [Fact]
    public void AHostBootsACoopGuestFromTheCabin()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(103));
        var host = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]);
        var guest = Guest(mesh[1], "Nathan");
        host.OpenCoopHost(NetSeats.MaxPlayers - 1);
        host.Offer(3, "Zachary", 1);
        host.ShowCoop(NetCoopScreen.Cabin, 3, 3, 0);
        guest.OpenJoin();
        Pump(host, guest);
        Assert.True(guest.IsCoopGuest);
        Assert.Single(host.CoopGuests);

        Assert.True(host.Boot(host.CoopGuests[0].Peer));
        Assert.Empty(host.CoopGuests);
        Pump(host, guest);

        Assert.Equal(NetDoorStage.Failed, guest.Stage);
        Assert.Equal(CoopDoorText.Booted, guest.Fault);
        Assert.Equal(0, host.Peers);
        Assert.Equal(1, host.Advertising!.Value.Players);
    }

    [Fact]
    public void ABootedGuestIsRefusedOnARejoinUntilTheSessionCloses()
    {
        var mesh = LoopbackTransport.Mesh(4, Clean, new Random(107));
        mesh[1].Disconnect(mesh[2].LocalPeer);
        mesh[1].Disconnect(mesh[3].LocalPeer);
        mesh[2].Disconnect(mesh[3].LocalPeer);
        var gate = new ArrivalGate(mesh[0]);
        mesh[3].Address = "10.0.0.9";
        var host = new NetPlayFeature((_, _, _) => gate, (_, _) => gate);
        host.OpenDogfightHost(NetSeats.MaxPlayers - 1);

        var first = Arriving(gate, mesh[1], "Nathan");
        first.OpenJoin();
        Pump(host, first);
        Assert.True(host.Boot(mesh[1].LocalPeer));
        Pump(host, first);
        Assert.Equal(CoopDoorText.Booted, first.Fault);

        // The same machine comes back on a new connection and is told it was booted.
        var again = Arriving(gate, mesh[2], "Nathan");
        again.OpenJoin();
        Pump(host, again);
        Assert.Equal(NetDoorStage.Failed, again.Stage);
        Assert.Equal(CoopDoorText.Booted, again.Fault);
        Assert.Equal(0, host.Peers);

        // ABLE-TO-FAIL CONTROL: a guest from another machine joins the same session.
        var other = Arriving(gate, mesh[3], "Sheila");
        other.OpenJoin();
        Pump(host, other);
        Assert.Equal(NetDoorStage.Joined, other.Stage);
        Assert.Equal(1, host.Peers);
    }

    [Fact]
    public void ASessionWithAPasswordAdmitsOnlyTheRightAnswer()
    {
        var mesh = LoopbackTransport.Mesh(3, Clean, new Random(109));
        mesh[1].Disconnect(mesh[2].LocalPeer);
        var host = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]);
        host.Identity.Take(new NetPlayerInfo { GameName = "Friday Fliers", Callsign = "Zachary", Password = "swordfish" }, game: true);
        host.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        Assert.True(host.Advertising!.Value.Password);

        var wrong = Guest(mesh[1], "Nathan", password: "sword");
        var right = Guest(mesh[2], "Sheila", password: "swordfish");
        wrong.OpenJoin();
        right.OpenJoin();
        Pump(host, wrong, right);

        Assert.Equal(NetDoorStage.Failed, wrong.Stage);
        Assert.Equal(CoopDoorText.WrongPassword, wrong.Fault);
        Assert.Equal(NetDoorStage.Joined, right.Stage);
        Assert.True(right.IsDogfightGuest);
        Assert.False(right.AwaitingAdmission);
        Assert.Equal(1, host.Peers);
        Assert.Equal(2, host.Dogfight!.Players.Count);
    }

    [Fact]
    public void AGuestWaitsOnItsPasswordBeforeItFollowsTheHost()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(113));
        var host = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]);
        host.Identity.Take(new NetPlayerInfo { GameName = "Friday Fliers", Callsign = "Zachary", Password = "swordfish" }, game: true);
        host.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        var guest = Guest(mesh[1], "Nathan", password: "swordfish");
        guest.OpenJoin();

        // The guest has read the advert and answered, and the host has not stepped yet.
        guest.Step(0.016);
        Assert.Equal(NetDoorStage.Joined, guest.Stage);
        Assert.True(guest.AwaitingAdmission);
        Assert.False(guest.IsDogfightGuest);
        Assert.Null(guest.Dogfight);
        Assert.Equal(0, host.Peers);

        Pump(host, guest);
        Assert.False(guest.AwaitingAdmission);
        Assert.True(guest.IsDogfightGuest);
        Assert.Equal(1, host.Peers);
    }

    [Fact]
    public void ASessionsPasswordAndListingEndWithItSoNoLaterOpenInheritsThem()
    {
        // Every open takes a fresh end, since a carrier binds one lobby in its life.
        var door = new NetPlayFeature(
            (_, _, _) => LoopbackTransport.Mesh(1, Clean, new Random(127))[0], (_, _) => LoopbackTransport.Mesh(1, Clean, new Random(127))[0]);
        door.Identity.Take(new NetPlayerInfo { GameName = "Friday Fliers", Callsign = "Zachary", Password = "swordfish", Private = true }, game: true);
        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        Assert.True(door.Advertising!.Value.Password);
        Assert.True(door.Identity.Private);
        door.Close();

        // A co-op host opened with no box asks nothing and takes its own kind's listing.
        Assert.Equal("", door.Identity.Password);
        door.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        Assert.False(door.Advertising!.Value.Password);
        Assert.True(door.Identity.Private);
        door.Close();
        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        Assert.False(door.Identity.Private);
        door.Close();

        // A join's answer ends with the join, here one nobody answers.
        var nobody = LoopbackTransport.Mesh(1, Clean, new Random(131))[0];
        var silent = new NetPlayFeature((_, _, _) => nobody, (_, _) => nobody);
        silent.Identity.Take(new NetPlayerInfo { Callsign = "Zachary", Password = "kestrel" }, game: false);
        silent.OpenJoin();
        silent.Step(NetPlayFeature.JoinTimeoutSeconds + 1.0);
        Assert.Equal(NetDoorStage.Failed, silent.Stage);
        Assert.Equal("", silent.Identity.Password);

        // ABLE-TO-FAIL CONTROL: answers given to a failed door survive its Close. The boards close a
        // failed door between the box's OK and the open.
        silent.Identity.Take(new NetPlayerInfo { Callsign = "Zachary", Password = "kestrel" }, game: false);
        silent.Close();
        Assert.Equal("kestrel", silent.Identity.Password);
    }

    private static NetPlayFeature Guest(LoopbackTransport end, string callsign, string password = "")
    {
        var door = new NetPlayFeature((_, _, _) => end, (_, _) => end);
        door.Identity.Take(new NetPlayerInfo { Callsign = callsign, Password = password }, game: false);
        return door;
    }

    private static NetPlayFeature Arriving(ArrivalGate gate, LoopbackTransport end, string callsign)
    {
        var door = new NetPlayFeature((_, _, _) => end, (_, _) =>
        {
            gate.Arrive(end.LocalPeer);
            return end;
        });
        door.Identity.Take(new NetPlayerInfo { Callsign = callsign }, game: false);
        return door;
    }

    private static void Pump(params NetPlayFeature[] doors)
    {
        for (int frame = 0; frame < 4; frame++)
        {
            foreach (var door in doors)
            {
                door.Step(0.016);
            }
        }
    }

    // A host's end on a mesh connected from the start, reached by a guest only once it joins.
    private sealed class ArrivalGate : INetTransport, INetTransportListener, INetPeerAddress
    {
        private readonly LoopbackTransport _inner;
        private readonly HashSet<int> _arrived = new();
        private INetTransportListener? _listener;

        public ArrivalGate(LoopbackTransport inner) => _inner = inner;

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers.Where(_arrived.Contains).ToList();

        public string? AddressOf(int peer) => _arrived.Contains(peer) ? _inner.AddressOf(peer) : null;

        public void Arrive(int peer)
        {
            if (_arrived.Add(peer) && _inner.Peers.Contains(peer))
            {
                _listener?.OnPeerConnected(peer);
            }
        }

        public void Bind(INetTransportListener listener)
        {
            _listener = listener;
            _inner.Bind(this);
        }

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0)
        {
            if (_arrived.Contains(peer))
            {
                _inner.Send(peer, payload, reliability, channel);
            }
        }

        public void Disconnect(int peer) => _inner.Disconnect(peer);

        public void Step(double dt) => _inner.Step(dt);

        public void OnPeerConnected(int peer)
        {
            if (_arrived.Contains(peer))
            {
                _listener?.OnPeerConnected(peer);
            }
        }

        public void OnPeerDisconnected(int peer)
        {
            if (_arrived.Contains(peer))
            {
                _listener?.OnPeerDisconnected(peer);
            }
        }

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload)
        {
            if (_arrived.Contains(peer))
            {
                _listener?.OnPayload(peer, channel, payload);
            }
        }
    }
}
