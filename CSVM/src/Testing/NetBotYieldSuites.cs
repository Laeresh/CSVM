using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.UI.Menu;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>A person taking a lobby bot's seat, between real doors over the loopback. A guest who
/// joins a full lobby takes the newest bot's place at once. One who joins mid-match waits in the
/// lobby through the match and its Restart. It takes the newest bot's place once the match lands
/// there. The host's flight is a whole session, its door stepped as the launcher steps it.
/// </summary>
internal static class NetBotYieldSuites
{
    private const ulong HostSeed = 0xB07CEDE5UL;

    // Steps between one event and the reading on it. A lobby message crosses this link in one.
    private const int SettleSteps = 20;

    // Steps the host flies before and after the late joiner arrives, and after its Restart.
    private const int FlightSteps = 60;

    // A guest's wait for the host's opener after the second launch.
    private const int OpenerSteps = 60;

    // The match's whole clock, --vs-time=1's minute, wound on at once to end a round.
    private const float MatchSeconds = 60f;

    private const string HostName = "Zachary";
    private const string EarlyName = "Sheila";
    private const string LateName = "Nathan";

    [Suite("net-bot-yield",
        "a lobby host fills its field with fifteen bots over a clean loopback: a guest joining the "
        + "full lobby takes the newest bot's place at once on both ends, and its leave hands the seat "
        + "to nobody; launched with fifteen bots, a guest joining mid-match lands in the lobby reading "
        + "In mission, hears no state of the match and is no seat of it, and the bots stay put; a "
        + "Restart flies the same field while it waits; when the match lands, its Game Scores name "
        + "each seat by the session's callsign and tag the bots, the newest bot's row leaves the list "
        + "with a notice in both chats, the late guest holds the freed place on both ends, and the next launch "
        + "seats it at seat 1 with fourteen bots on the host's roster and on its own copy")]
    internal static void ALateJoinerTakesTheNewestBotsSeat(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _, "--vs-time=1");
        ctx.RequireData(ctx.MessagesPath, $"message table");
        // Fifteen Random bots draw from every stock airframe.
        ctx.RequirePlane(StockAirframes.Nodes.ToArray());
        var pool = BotSeats.CallsignPool(CSVM.Mech3.Messages.Load(ctx.MessagesPath));
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(5121));
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature((_, _, _) => gate, (_, _) => throw new InvalidOperationException("the host does not join"));
        hostDoor.Take(new NetPlayerInfo { Callsign = HostName, GameName = "Bots" }, game: true);
        var lateDoor = GuestDoor(gate, mesh[1], LateName);
        var earlyDoor = GuestDoor(gate, mesh[2], EarlyName);
        var ambient = NetCombatSuites.Ambient.Save();
        NetCombatSuites.Ends? host = null;
        try
        {
            hostDoor.OpenDogfightHost(NetSeats.MaxPlayers - 1);
            var lobby = hostDoor.Dogfight!;
            lobby.CallsignPool = pool;
            ctx.Check(lobby.FillTo(NetSeats.MaxPlayers) == NetSeats.MaxPlayers - 1 && lobby.BotRoom == 0,
                $"Fill to sixteen seats fifteen bots beside the host ({lobby.Bots.Count}, room {lobby.BotRoom})");
            var full = lobby.Bots.ToArray();
            JoinsTheFullLobby(ctx, hostDoor, earlyDoor, full, mesh[2], mesh[0].LocalPeer);

            ctx.Check(lobby.AddBot() && lobby.Bots.Count == NetSeats.MaxPlayers - 1,
                $"the host adds a bot back into the freed seat ({lobby.Bots.Count})");
            var field = lobby.Bots.ToArray();
            var launch = hostDoor.BuildLaunch();
            if (launch == null)
            {
                ctx.Check(false, $"the host's door hands out a launch ({hostDoor.Stage})");
                return;
            }

            var roster = Field(launch, lobby, pool);
            ctx.Check(roster.Length == NetSeats.MaxPlayers && roster.Count(s => s.IsBot) == NetSeats.MaxPlayers - 1,
                $"the first launch seats the host and fifteen bots ({roster.Length}, {roster.Count(s => s.IsBot)} bots)");
            host = NetCombatSuites.Ends.Open(ctx, spec, launch.Transport, isHost: true, HostSeed, roster, StockAirframes.Nodes);
            ctx.Check(host.Built, $"the host's session builds with fifteen bot seats ({host.Built})");
            if (!host.Built)
            {
                return;
            }

            var wire = (NetLobby)launch.Transport;
            Fly(FlightSteps, host, hostDoor, lateDoor);
            lateDoor.OpenJoin();
            Fly(SettleSteps, host, hostDoor, lateDoor);
            lateDoor.Dogfight?.Show();
            int latePeer = mesh[1].LocalPeer;
            var seats = Callsigns(host);
            WaitsInTheLobby(ctx, "while the match runs", host, hostDoor, lateDoor, wire, latePeer, field, seats);

            var director = host.Session.Dogfight!;
            director.Match.Advance(MatchSeconds);
            Fly(SettleSteps, host, hostDoor, lateDoor);
            ctx.Check(director.Match.Completed, $"the match runs to its end ({director.Match.Completed})");
            director.Restart();
            Fly(FlightSteps, host, hostDoor, lateDoor);
            ctx.Check(!director.Match.Completed, $"the host's Restart flies the match again ({director.Match.Completed})");
            WaitsInTheLobby(ctx, "after the host's Restart", host, hostDoor, lateDoor, wire, latePeer, field, seats);

            director.Match.Advance(MatchSeconds);
            Fly(SettleSteps, host, hostDoor, lateDoor);
            var landing = Launcher.LobbyLanding(true, lobby, director.Match, host.Session.NetSeats);
            ctx.Check(landing != null && hostDoor.Reclaim(), $"the finished match lands the host on its lobby and the door takes its wire back");
            var lines = landing?.Scores ?? Array.Empty<DogfightScore>();
            ctx.Check(lines.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(seats.OrderBy(n => n, StringComparer.Ordinal))
                      && lines.Count(l => l.IsBot) == NetSeats.MaxPlayers - 1 && lines.Single(l => !l.IsBot).Name == HostName,
                $"its Game Scores name every seat by the session's callsign and tag the fifteen bots ({lines.Count(l => l.IsBot)} tagged, {string.Join(", ", lines.Take(4).Select(l => l.Name))} ...)");
            host.Close();
            lobby.Land(landing?.Scores ?? Array.Empty<DogfightScore>());
            StepDoors(SettleSteps, hostDoor, lateDoor);
            HoldsTheFreedSeat(ctx, lobby, lateDoor, field);
            LaunchesInTheSeat(ctx, hostDoor, lateDoor, lobby, pool, field, latePeer);
        }
        finally
        {
            host?.Close();
            earlyDoor.Discard();
            lateDoor.Discard();
            hostDoor.Discard();
            ambient.Restore();
        }
    }

    // A guest joining the full lobby takes the newest bot's place at once. Its leave refills
    // nothing: the seat stays free for the host to fill.
    private static void JoinsTheFullLobby(TestContext ctx, NetPlayFeature hostDoor, NetPlayFeature guestDoor,
        DogfightBot[] full, LoopbackTransport guestEnd, int hostPeer)
    {
        var lobby = hostDoor.Dogfight!;
        guestDoor.OpenJoin();
        StepDoors(SettleSteps, hostDoor, guestDoor);
        guestDoor.Dogfight?.Show();
        StepDoors(SettleSteps, hostDoor, guestDoor);
        var heard = guestDoor.Dogfight?.Players ?? Array.Empty<DogfightLobbySeat>();
        ctx.Check(lobby.Bots.SequenceEqual(full.Take(NetSeats.MaxPlayers - 2)) && lobby.FieldSeats == NetSeats.MaxPlayers
                  && lobby.Players.Count == NetSeats.MaxPlayers && lobby.Players[1].Name == EarlyName,
            $"a guest joining the full lobby takes the newest bot's place on the host ({lobby.Bots.Count} bots, {string.Join(", ", lobby.Players.Take(3).Select(p => p.Name))})");
        ctx.Check(heard.Count == NetSeats.MaxPlayers && guestDoor.Dogfight!.You == 1 && heard[1] is { IsBot: false, Name: EarlyName }
                  && heard.All(row => row.Name != full[^1].Callsign),
            $"and on its own lobby, with the newest bot '{full[^1].Callsign}' gone ({heard.Count} rows, you {guestDoor.Dogfight?.You})");

        guestEnd.Disconnect(hostPeer);
        StepDoors(SettleSteps, hostDoor, guestDoor);
        ctx.Check(lobby.FieldSeats == NetSeats.MaxPlayers - 1 && lobby.Bots.Count == NetSeats.MaxPlayers - 2 && lobby.BotRoom == 1,
            $"ABLE-TO-FAIL CONTROL: the guest who leaves is not replaced by a bot ({lobby.FieldSeats} pilots, {lobby.Bots.Count} bots)");
    }

    // A late joiner waits in the lobby: linked, In mission, with no options, no opener and no seat
    // of the running match. The host's lobby lets no bot go, and the session flies the same seats.
    private static void WaitsInTheLobby(TestContext ctx, string when, NetCombatSuites.Ends host, NetPlayFeature hostDoor,
        NetPlayFeature lateDoor, NetLobby wire, int latePeer, DogfightBot[] field, string[] seats)
    {
        var waiting = lateDoor.Dogfight;
        ctx.Check(lateDoor.IsDogfightGuest && waiting is { HasOptions: false } && !lateDoor.DogfightLaunchDue
                  && lateDoor.Advert?.Status == NetSessionStatus.InMission && waiting.WaitsOnMatch(lateDoor.Advert?.Status),
            $"{when}: the late guest stands in the lobby reading In mission and waiting on the match, with no launch due ({lateDoor.Stage}, {lateDoor.Advert?.Status}, options {waiting?.HasOptions})");
        ctx.Check(!lateDoor.HostStarted && !wire.Peers.Contains(latePeer) && wire.AllPeers.Contains(latePeer),
            $"{when}: it holds a peer on the host's wire but no seat of the match, and no match payload reached it");
        ctx.Check(hostDoor.Dogfight!.Bots.SequenceEqual(field),
            $"ABLE-TO-FAIL CONTROL: {when}: the host's lobby keeps all fifteen bots while the match is out of it ({hostDoor.Dogfight.Bots.Count})");
        var flying = Callsigns(host);
        ctx.Check(flying.SequenceEqual(seats) && host.Session.NetSeats.Count(s => s.IsBot) == NetSeats.MaxPlayers - 1,
            $"{when}: the match flies the same sixteen seats ({string.Join(", ", flying)})");
    }

    // Back in the lobby the newest bot's row leaves, and the late guest stands in the freed place
    // on both ends.
    private static void HoldsTheFreedSeat(TestContext ctx, DogfightLobby lobby, NetPlayFeature lateDoor, DogfightBot[] field)
    {
        var heard = lateDoor.Dogfight?.Players ?? Array.Empty<DogfightLobbySeat>();
        ctx.Check(lobby.Bots.SequenceEqual(field.Take(NetSeats.MaxPlayers - 2)) && lobby.Players.Count == NetSeats.MaxPlayers
                  && lobby.Players[1] is { IsBot: false, Name: LateName },
            $"back in the lobby the newest bot '{field[^1].Callsign}' leaves the host's list and the late guest holds the freed place ({lobby.Bots.Count} bots, {lobby.Players[1].Name})");
        ctx.Check(lateDoor.Dogfight is { HasOptions: true, You: 1 } && heard.Count == NetSeats.MaxPlayers
                  && heard[1].Name == LateName && heard.All(row => row.Name != field[^1].Callsign)
                  && lateDoor.Advert?.Status == NetSessionStatus.Waiting && !lateDoor.Dogfight.WaitsOnMatch(lateDoor.Advert?.Status),
            $"and the late guest's own lobby reads the same field with it in that place, no longer waiting ({heard.Count} rows, you {lateDoor.Dogfight?.You}, {lateDoor.Advert?.Status})");
        string yielded = DogfightLobby.YieldLine(field[^1].Callsign);
        ctx.Check(lobby.Chat.Any(line => line.Text == yielded) && lateDoor.Dogfight!.Chat.Any(line => line.Text == yielded),
            $"both chats say the newest bot left the game to make room ('{yielded}')");
    }

    // The next launch carries the late guest at seat 1, the bots after it, on both ends.
    private static void LaunchesInTheSeat(TestContext ctx, NetPlayFeature hostDoor, NetPlayFeature lateDoor, DogfightLobby lobby,
        IReadOnlyList<string> pool, DogfightBot[] field, int latePeer)
    {
        var launch = hostDoor.BuildLaunch();
        if (launch == null)
        {
            ctx.Check(false, $"the host's door hands out the second launch ({hostDoor.Stage})");
            return;
        }

        var roster = Field(launch, lobby, pool);
        var kept = field.Take(NetSeats.MaxPlayers - 2).Select(b => b.Callsign);
        ctx.Check(roster.Length == NetSeats.MaxPlayers && roster[1] is { IsBot: false, Callsign: LateName } && roster[1].PeerId == latePeer
                  && roster.Skip(2).All(s => s.IsBot) && roster.Skip(2).Select(s => s.Callsign).SequenceEqual(kept),
            $"the second launch seats the late guest at seat 1 and fourteen bots after it ({string.Join(", ", roster.Select(s => s.Callsign))})");
        _ = NetSession.Host((NetLobby)launch.Transport, roster, HostSeed, null, StockAirframes.Nodes);
        for (int step = 0; step < OpenerSteps && !lateDoor.DogfightLaunchDue; step++)
        {
            StepDoors(1, lateDoor);
        }

        var followed = lateDoor.DogfightLaunchDue ? lateDoor.BuildLaunch() : null;
        if (followed == null)
        {
            ctx.Check(false, $"the late guest follows the host's second launch ({lateDoor.Stage})");
            return;
        }

        var session = NetSession.Guest(followed.Transport, StockAirframes.Nodes);
        for (int step = 0; step < OpenerSteps && !session.Joined; step++)
        {
            session.Step(GameClock.FixedDt);
        }

        var seats = session.Seats;
        ctx.Check(session.Joined && seats.Count == NetSeats.MaxPlayers && seats[1] is { FlownHere: true, IsBot: false, Callsign: LateName }
                  && seats.Skip(2).All(s => s is { IsBot: true, FlownHere: false })
                  && seats.Skip(2).Select(s => s.Callsign).SequenceEqual(kept),
            $"and the late guest joins it flying seat 1, the bots after it ({string.Join(", ", seats.Select(s => s.Callsign))})");
    }

    // The host's field as the launcher builds it off a lobby launch.
    private static NetSeat[] Field(MenuNetLaunch launch, DogfightLobby lobby, IReadOnlyList<string> pool)
    {
        var planes = new[] { StockAirframes.Node(lobby.Airframe) };
        var (roster, _) = Launcher.VersusLaunchField(launch.Transport, planes, new LoadoutChoice?[] { null }, StockLoadouts.Load(),
            lobby.Rules, lobby.TeamOfPeer, lobby.LaunchBots, pool, new Random(3));
        return roster;
    }

    private static string[] Callsigns(NetCombatSuites.Ends host) => host.Session.NetSeats.Select(s => s.Callsign).ToArray();

    private static NetPlayFeature GuestDoor(ArrivalGate gate, LoopbackTransport end, string callsign)
    {
        var door = new NetPlayFeature((_, _, _) => throw new InvalidOperationException("a guest does not host"), (_, _) =>
        {
            gate.Arrive(end.LocalPeer);
            return end;
        });
        door.Take(new NetPlayerInfo { Callsign = callsign }, game: false);
        return door;
    }

    // The host's session and both doors through the same steps, the host's door stepped as the
    // launcher steps it in a lobby flight.
    private static void Fly(int steps, NetCombatSuites.Ends host, params NetPlayFeature[] doors)
    {
        for (int i = 0; i < steps; i++)
        {
            host.Session._PhysicsProcess(GameClock.FixedDt);
            StepDoors(1, doors);
        }
    }

    private static void StepDoors(int steps, params NetPlayFeature[] doors)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var door in doors)
            {
                door.Step(GameClock.FixedDt);
            }
        }
    }

    // The host's end of a mesh whose guests arrive one at a time. A peer is listed, and its
    // payloads cross, only once it has arrived.
    private sealed class ArrivalGate : INetTransport, INetTransportListener
    {
        private readonly INetTransport _inner;
        private readonly HashSet<int> _arrived = new();
        private INetTransportListener? _listener;

        public ArrivalGate(INetTransport inner) => _inner = inner;

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers.Where(_arrived.Contains).ToList();

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
