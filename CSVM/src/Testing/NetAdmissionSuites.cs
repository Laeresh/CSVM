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

    [Suite("net-admission-over-cap",
        "a Dogfight host capped at two players refuses the third arrival as full; LAUNCH! pressed "
        + "while the refused guest is still on the wire, its hang-up not yet landed, builds a field "
        + "of exactly the two seats the lobby lists, and the carrier's next step lets the guest go; "
        + "a campaign host capped at two refuses the same way, and a session bound inside that "
        + "window binds the seated guest alone")]
    internal static void ALaunchSeatsOnlyTheListedPeers(TestContext ctx)
    {
        DogfightLaunch(ctx);
        CoopBind(ctx);
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

    private static NetPlayFeature HostDoor(INetTransport end)
    {
        var carrier = new LateHangUp(end);
        var door = new NetPlayFeature((_, _, _) => carrier, (_, _) => throw new InvalidOperationException("the host does not join"));
        door.Identity.Take(new NetPlayerInfo { Callsign = HostName, GameName = "Cap", MaxPlayers = 2 }, game: true);
        return door;
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
    // up inside the call.
    private sealed class LateHangUp : INetTransport
    {
        private readonly INetTransport _inner;
        private readonly List<int> _asked = new();

        public LateHangUp(INetTransport inner) => _inner = inner;

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers;

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
