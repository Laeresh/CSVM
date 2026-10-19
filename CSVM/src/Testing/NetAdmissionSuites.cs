using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Net;
using CSVM.UI.Menu;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>Who a host's launch seats, between a real door and loopback guests. The host's carrier
/// hangs up on its next step rather than inside the call, as ENet's does.</summary>
internal static class NetAdmissionSuites
{
    private const string HostName = "Zachary";

    // The cap of the lobby the host-seat legs fill.
    private const int Cap = 4;

    [Suite("net-admission-over-cap",
        "a Dogfight host capped at two players refuses the third arrival as full; LAUNCH! pressed "
        + "while the refused guest is still on the wire, its hang-up not yet landed, builds a field "
        + "of exactly the two seats the lobby lists, and the carrier's next step lets the guest go; "
        + "a campaign host capped at two refuses the same way, and a session bound inside that "
        + "window binds the seated guest alone; a Dogfight host capped at four and full with three "
        + "guests sits out the second seat it signs on, as a guest's added seat sits out, and that seat "
        + "takes the place a leaving guest frees before a later arrival, which is refused as full; "
        + "the host's added seat never takes a guest's granted second seat, and wins a place freed in "
        + "the same step a newcomer arrives; a pad a guest drops frees its place to the host's waiting "
        + "seat ahead of a newcomer arriving in the same step and ahead of another guest's waiting seat")]
    internal static void ALaunchSeatsOnlyTheListedPeers(TestContext ctx)
    {
        DogfightLaunch(ctx);
        CoopBind(ctx);
        HostSeatPastTheCap(ctx);
    }

    private static void DogfightLaunch(TestContext ctx)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(2));
        var hostDoor = HostDoor(mesh[0]);
        int seated = mesh[1].LocalPeer;
        int refused = mesh[2].LocalPeer;
        string Wire() => string.Join(" ", mesh[0].Peers);
        try
        {
            hostDoor.OpenDogfightHost(NetSeats.MaxPlayers - 1);
            hostDoor.Step(GameClock.FixedDt);
            var lobby = hostDoor.Dogfight!;
            ctx.Check(lobby.FieldSeats == 2 && lobby.Players.Count == 2 && hostDoor.Peers == 1 && mesh[0].Peers.Contains(refused),
                $"the Dogfight host seats the first guest and refuses the second as full, which is still on the wire ({lobby.FieldSeats} seats, {hostDoor.Peers} guests, wire {Wire()})");

            var launch = hostDoor.BuildLaunch();
            if (launch == null)
            {
                ctx.Check(false, $"the Dogfight host's door hands out a launch ({hostDoor.Stage})");
                return;
            }

            ctx.Check(mesh[0].Peers.Contains(refused),
                $"ABLE-TO-FAIL CONTROL: LAUNCH! lands inside the hang-up's grace, the refused guest still on the carrier ({Wire()})");
            var planes = new[] { StockAirframes.Node(lobby.Airframe) };
            var (roster, _) = SeatFields.VersusLaunchField(launch.Transport, planes, new LoadoutChoice?[] { null }, StockLoadouts.Load(),
                lobby.Rules, lobby.TeamOfPeer, lobby.LaunchBots);
            var peers = roster.Select(seat => seat.PeerId).ToArray();
            ctx.Check(roster.Length == lobby.FieldSeats && peers.SequenceEqual(new[] { mesh[0].LocalPeer, seated }),
                $"the launch's field seats exactly the peers the lobby lists ({roster.Length} seats for {lobby.FieldSeats} rows, peers {string.Join(" ", peers)})");

            launch.Transport.Step(GameClock.FixedDt);
            ctx.Check(!mesh[0].Peers.Contains(refused), $"the carrier's next step lets the refused guest go ({Wire()})");
        }
        finally
        {
            hostDoor.Discard();
        }
    }

    // A campaign host's session binds its carrier as a co-op launch does. The bind copies the wire's
    // peers, which must not hold a refused guest still on the carrier.
    private static void CoopBind(TestContext ctx)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(3));
        var hostDoor = HostDoor(mesh[0]);
        int seated = mesh[1].LocalPeer;
        int refused = mesh[2].LocalPeer;
        string Wire() => string.Join(" ", mesh[0].Peers);
        try
        {
            hostDoor.OpenCoopHost(NetSeats.MaxPlayers - 1);
            hostDoor.Offer(3, HostName, 1);
            hostDoor.Step(GameClock.FixedDt);
            ctx.Check(hostDoor.Peers == 1 && mesh[0].Peers.Contains(refused),
                $"the campaign host seats the first guest and refuses the second as full, which is still on the wire ({hostDoor.Peers} guests, wire {Wire()})");

            var launch = hostDoor.BuildLaunch();
            if (launch == null)
            {
                ctx.Check(false, $"the campaign host's door hands out a launch ({hostDoor.Stage})");
                return;
            }

            var heard = new Arrivals();
            launch.Transport.Bind(heard);
            ctx.Check(mesh[0].Peers.Contains(refused) && heard.Peers.SequenceEqual(new[] { seated }) && launch.Transport.Peers.SequenceEqual(new[] { seated }),
                $"a session bound inside the grace binds the seated guest alone ({string.Join(" ", heard.Peers)} announced, wire {Wire()})");
            ((NetLobby)launch.Transport).Unbind();
        }
        finally
        {
            hostDoor.Discard();
        }
    }

    // The host's own seats count against the cap with every seat a guest holds. A pad signed on once
    // the lobby is full sits out as a guest's does, and waits ahead of any later arrival.
    private static void HostSeatPastTheCap(TestContext ctx)
    {
        InAFullLobby(ctx, 4, new[] { 1, 1, 1 }, (rig, lobby) =>
        {
            var guests = rig.Guests;
            rig.Host.LocalSeats = 2;
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && lobby.SeatsFlown == 1 && rig.Host.SeatsShort.Length > 0,
                $"a second seat the full host signs on sits out, the field still {Cap} ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, short \"{rig.Host.SeatsShort}\")");

            guests[0].LocalSeats = 2;
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && guests[0].Dogfight?.SeatsFlown == 1 && guests[0].SeatsShort.Length > 0,
                $"a second seat a guest signs on in the full lobby sits out too ({lobby.SeatOwners.Count} seats, guest flies {guests[0].Dogfight?.SeatsFlown}, short \"{guests[0].SeatsShort}\")");

            rig.Leave(2);
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && lobby.SeatsFlown == 2 && rig.Host.SeatsShort.Length == 0 && guests[0].Dogfight?.SeatsFlown == 1,
                $"a guest's leaving frees a place the host's waiting seat takes ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, first guest {guests[0].Dogfight?.SeatsFlown})");

            rig.Carrier.Unarrived.Clear();
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && rig.Host.Peers == 2 && !lobby.SeatOwners.Any(owner => owner.Peer == rig.Late),
                $"a guest arriving afterwards is refused as full ({lobby.SeatOwners.Count} seats, {rig.Host.Peers} guests)");
        });

        InAFullLobby(ctx, 5, new[] { 2, 1 }, (rig, lobby) =>
        {
            var first = rig.Guests[0];
            rig.Host.LocalSeats = 2;
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && lobby.SeatsFlown == 1 && first.Dogfight?.SeatsFlown == 2,
                $"the host's added seat sits out rather than take a guest's granted second ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, guest {first.Dogfight?.SeatsFlown})");

            // In one host step, so the leave and the arrival contend for the one freed place.
            rig.Leave(1);
            rig.Carrier.Unarrived.Clear();
            rig.Host.Step(0.0);
            ctx.Check(rig.Host.Peers == 1 && lobby.SeatsFlown == 2 && lobby.SeatOwners.Count == Cap && !lobby.SeatOwners.Any(owner => owner.Peer == rig.Late),
                $"the host's waiting seat takes the freed place ahead of the newcomer, refused as full ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, {rig.Host.Peers} guests)");
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && first.Dogfight?.SeatsFlown == 2,
                $"and the guest keeps both its seats ({lobby.SeatOwners.Count} seats, guest {first.Dogfight?.SeatsFlown})");
        });

        InAFullLobby(ctx, 6, new[] { 2, 1 }, (rig, lobby) =>
        {
            var first = rig.Guests[0];
            rig.Host.LocalSeats = 2;
            rig.Settle();

            // The guest's dropped pad reaches the host in the step the newcomer arrives.
            first.LocalSeats = 1;
            first.Step(0.0);
            rig.Carrier.Unarrived.Clear();
            rig.Host.Step(0.0);
            ctx.Check(rig.Host.Peers == 2 && lobby.SeatsFlown == 2 && lobby.SeatOwners.Count == Cap && !lobby.SeatOwners.Any(owner => owner.Peer == rig.Late),
                $"a pad a guest drops frees its place to the host's waiting seat ahead of the newcomer, refused as full ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, {rig.Host.Peers} guests)");
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && lobby.SeatsFlown == 2 && first.Dogfight?.SeatsFlown == 1,
                $"and the host keeps that seat ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, guest {first.Dogfight?.SeatsFlown})");
        });

        InAFullLobby(ctx, 7, new[] { 2, 1 }, (rig, lobby) =>
        {
            var guests = rig.Guests;
            rig.Host.LocalSeats = 2;
            guests[1].LocalSeats = 2;
            rig.Settle();
            ctx.Check(lobby.SeatsFlown == 1 && guests[1].Dogfight?.SeatsFlown == 1,
                $"ABLE-TO-FAIL CONTROL: the host's and the later guest's added seats both wait (host flies {lobby.SeatsFlown}, guest {guests[1].Dogfight?.SeatsFlown})");

            guests[0].LocalSeats = 1;
            rig.Settle();
            ctx.Check(lobby.SeatOwners.Count == Cap && lobby.SeatsFlown == 2 && guests[1].Dogfight?.SeatsFlown == 1,
                $"a pad a guest drops frees its place to the host's waiting seat ahead of a guest's waiting seat ({lobby.SeatOwners.Count} seats, host flies {lobby.SeatsFlown}, later guest {guests[1].Dogfight?.SeatsFlown})");
        });
    }

    // A cap-four Dogfight host flying one seat and filled by guests joined with the given seats each.
    // The carrier holds one more guest off the wire until a leg lets it arrive.
    private static void InAFullLobby(TestContext ctx, int seed, int[] guestSeats, Action<FullLobby, DogfightLobby> legs)
    {
        var mesh = LoopbackTransport.Mesh(guestSeats.Length + 2, LoopbackConditions.Perfect, new Random(seed));
        var carrier = new LateHangUp(mesh[0]) { Unarrived = { mesh[^1].LocalPeer } };
        var rig = new FullLobby(mesh, carrier, HostDoor(carrier, Cap), new List<NetPlayFeature>());
        for (int i = 0; i < guestSeats.Length; i++)
        {
            var end = mesh[i + 1];
            rig.Guests.Add(new NetPlayFeature((_, _, _) => throw new InvalidOperationException("a guest does not host"), (_, _) => end)
            {
                LocalSeats = guestSeats[i],
            });
        }

        try
        {
            rig.Host.OpenDogfightHost(NetSeats.MaxPlayers - 1);
            rig.Guests.ForEach(guest => guest.OpenJoin());
            rig.Settle();
            rig.Guests.ForEach(guest => guest.Dogfight?.Show());
            rig.Settle();
            var lobby = rig.Host.Dogfight!;
            ctx.Check(lobby.SeatOwners.Count == Cap && rig.Host.Peers == guestSeats.Length && lobby.SeatsFlown == 1,
                $"ABLE-TO-FAIL CONTROL: the host's seat and guests of {string.Join("+", guestSeats)} seats fill the cap of {Cap} ({lobby.SeatOwners.Count} seats, {rig.Host.Peers} guests)");
            legs(rig, lobby);
        }
        finally
        {
            rig.Guests.ForEach(guest => guest.Discard());
            rig.Host.Discard();
        }
    }

    private static NetPlayFeature HostDoor(INetTransport end) => HostDoor(new LateHangUp(end), cap: 2);

    private static NetPlayFeature HostDoor(LateHangUp carrier, int cap)
    {
        var door = new NetPlayFeature((_, _, _) => carrier, (_, _) => throw new InvalidOperationException("the host does not join"));
        door.Identity.Take(new NetPlayerInfo { Callsign = HostName, GameName = "Cap", MaxPlayers = cap }, game: true);
        return door;
    }

    // The doors of a full lobby leg, and its wire.
    private sealed record FullLobby(IReadOnlyList<LoopbackTransport> Mesh, LateHangUp Carrier, NetPlayFeature Host, List<NetPlayFeature> Guests)
    {
        public int Late => Mesh[^1].LocalPeer;

        public void Settle()
        {
            NetDoorAid.SettleDogfight(Host, Guests);
            NetDoorAid.SettleDogfight(Host, Guests);
        }

        // A loopback end hangs up only when asked, not when its door lets it go.
        public void Leave(int guest)
        {
            Mesh[guest + 1].Disconnect(Mesh[0].LocalPeer);
            Guests[guest].Discard();
        }
    }

    // The peers a bind announces, in order.
    private sealed class Arrivals : INetTransportListener
    {
        public List<int> Peers { get; } = new();

        public void OnPeerConnected(int peer) => Peers.Add(peer);

        public void OnPeerDisconnected(int peer) => Peers.Remove(peer);

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload)
        {
        }
    }

    // A carrier whose hang-up lands on its next step, as ENet's does, over a loopback end that hangs
    // up inside the call. A peer on Unarrived is held off the list until it is taken off.
    private sealed class LateHangUp : INetTransport
    {
        private readonly INetTransport _inner;
        private readonly List<int> _asked = new();

        public LateHangUp(INetTransport inner) => _inner = inner;

        public HashSet<int> Unarrived { get; } = new();

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers.Where(peer => !Unarrived.Contains(peer)).ToList();

        public void Bind(INetTransportListener listener) => _inner.Bind(listener);

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            _inner.Send(peer, payload, reliability, channel);

        public void Disconnect(int peer) => _asked.Add(peer);

        public void Step(double dt)
        {
            foreach (int peer in _asked)
            {
                _inner.Disconnect(peer);
            }

            _asked.Clear();
            _inner.Step(dt);
        }
    }
}
