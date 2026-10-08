using System;
using System.Collections.Generic;
using System.Threading;
using CSVM.Net;
using CSVM.Session.Campaign;

namespace CSVM.UI.Menu;

/// <summary>What an aid host's master server does. None is set, or it lists the host under
/// <see cref="NetDoorAid.SampleCode"/>, or it is set and the host has no WebRTC carrier.</summary>
public enum AidInternet
{
    /// <summary>No master server.</summary>
    None,

    /// <summary>Listed under the sample code.</summary>
    Code,

    /// <summary>A master server with no WebRTC carrier, so no code.</summary>
    Offline,
}

/// <summary>
/// The multiplayer doors a screenshot aid stands on in place of the launcher's own. Both run over
/// the in-process loopback, so an aid opens no socket, raises no firewall dialog and asks no
/// router for a port. The host door's router answer is a fixed mapping at a documentation
/// address. The guest door is already linked to a loopback host holding a campaign mission open.
/// Nothing here is reachable outside the <c>--menu=</c> aids and the suites.
/// </summary>
public static class NetDoorAid
{
    /// <summary>The address the aid's router reports, from the range set aside for examples.
    /// </summary>
    public const string ExternalAddress = "203.0.113.24";

    /// <summary>The name the aid's campaign host advertises under.</summary>
    public const string HostName = "Zachary";

    /// <summary>The join code a <see cref="AidInternet.Code"/> host is listed under.</summary>
    public const string SampleCode = "K7Q-X3M";

    // The mapping lands on a worker thread, so the aid waits a bounded while for it. A shot of
    // a band still asking the router would show a state no player sees for long.
    private const int MappingWaitMs = 2000;

    /// <summary>The build version the aid's search door and its sample games run, fixed so the
    /// aid reads the same whatever this build's own version is.</summary>
    public static NetBuildVersion SampleVersion { get; } = new(0, 1);

    /// <summary>The version of the one sample game this build does not play with.</summary>
    public static NetBuildVersion OtherVersion { get; } = new(0, 2);

    /// <summary>The callsigns a co-op aid's guests go by, in player order after the host.</summary>
    public static IReadOnlyList<string> GuestNames { get; } = new[] { "Nathan", "Sheila", "Lucy" };

    /// <summary>The games the aid's LAN answers with, each at its own documentation address. They
    /// are a campaign waiting with room, one full, one in the air, a Dogfight, and a Dogfight on a
    /// build of another version.</summary>
    public static IReadOnlyList<LanGame> SampleGames { get; } = new[]
    {
        new LanGame("192.0.2.10", NetPlayFeature.DefaultPort, new SessionAdvertMessage(
            NetSessionKind.CampaignCoop, 2, 2, HostName, NetSessionStatus.Waiting, NetPlayFeature.CoopHumans), SampleVersion),
        new LanGame("192.0.2.11", NetPlayFeature.DefaultPort, new SessionAdvertMessage(
            NetSessionKind.CampaignCoop, 14, 4, "Nathan", NetSessionStatus.Full, NetPlayFeature.CoopHumans), SampleVersion),
        new LanGame("192.0.2.12", NetPlayFeature.DefaultPort, new SessionAdvertMessage(
            NetSessionKind.CampaignCoop, 30, 3, "Sheila", NetSessionStatus.InMission, NetPlayFeature.CoopHumans), SampleVersion),
        new LanGame("192.0.2.13", NetPlayFeature.DefaultPort, new SessionAdvertMessage(
            NetSessionKind.Dogfight, 3, 5, "Lucy", NetSessionStatus.Waiting, NetSeats.MaxPlayers), SampleVersion),
        new LanGame("192.0.2.14", NetPlayFeature.DefaultPort, new SessionAdvertMessage(
            NetSessionKind.Dogfight, 5, 2, "Oskar", NetSessionStatus.Waiting, NetSeats.MaxPlayers), OtherVersion),
    };

    /// <summary>The answers the network boxes' aids are posed with. The host's game stands at the
    /// spinner's opening eight, and its callsign on the Gruff Male voice.</summary>
    public static NetPlayerInfo SamplePlayer() => new()
    {
        GameName = HostName,
        Callsign = HostName,
        Voice = 5,
        MaxPlayers = NetPlayerInfo.DefaultPlayers,
    };

    /// <summary>A shut door whose host opens onto a loopback wire with <paramref name="guests"/>
    /// peers already on it, and whose router maps any port asked for.</summary>
    public static NetPlayFeature Host(int guests, out Func<int> unmapped) => Host(guests, out unmapped, out _);

    /// <summary>A door as <see cref="Host(int, out Func{int})"/>, with <paramref name="guestEnds"/>
    /// the guests' own ends of its wire so an aid can answer for them. <paramref name="internet"/>
    /// says what its master server does.</summary>
    public static NetPlayFeature Host(
        int guests, out Func<int> unmapped, out IReadOnlyList<INetTransport> guestEnds, AidInternet internet = AidInternet.None)
    {
        var mesh = LoopbackTransport.Mesh(1 + Math.Max(0, guests), LoopbackConditions.Perfect, new Random(1));
        int given = 0;
        unmapped = () => given;
        var ends = new List<INetTransport>(mesh);
        ends.RemoveAt(0);
        guestEnds = ends;
        var carrier = Carrier(mesh[0], internet);
        return new NetPlayFeature(
            (port, maxGuests, bind) => carrier,
            (address, port) => throw new InvalidOperationException("the aid's host door joins nothing"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, ExternalAddress, "aid"),
                port => given = port))
        {
            Master = MasterOf(internet),
        };
    }

    /// <summary>Opens <paramref name="door"/> as a campaign host and waits for its mapping, so the
    /// band reads as it settles.</summary>
    public static void OpenCoopHost(NetPlayFeature door, int missionSeq, int localPlayers)
    {
        ArgumentNullException.ThrowIfNull(door);
        door.OpenCoopHost(NetSeats.MaxPlayers - localPlayers);
        door.Offer(missionSeq, HostName, localPlayers);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (door.Router.PortMap == null && waited.ElapsedMilliseconds < MappingWaitMs)
        {
            door.Step(0.0);
            Thread.Sleep(1);
        }
    }

    /// <summary>Answers Ready on <paramref name="airframe"/> for the guest at <paramref name="guest"/>,
    /// under the round <paramref name="host"/> has under way, and lets the host hear it. The pick
    /// carries <paramref name="name"/> as the guest's callsign.</summary>
    public static void AnswerReady(NetPlayFeature host, INetTransport guest, int airframe, string name = "")
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(guest);
        Answer(host, guest, new CoopPickMessage(host.CoopEpoch, true, (byte)airframe, default, name));
    }

    /// <summary>Has each of <paramref name="guests"/> answer <paramref name="host"/> with a pick that
    /// is not Ready, carrying its callsign from <see cref="GuestNames"/>, as a guest that answered
    /// Player Information does.</summary>
    public static void NameGuests(NetPlayFeature host, IReadOnlyList<INetTransport> guests)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(guests);
        for (int i = 0; i < guests.Count && i < GuestNames.Count; i++)
        {
            Answer(host, guests[i], new CoopPickMessage(host.CoopEpoch, false, CoopGuestPick.StarterAirframe, default, GuestNames[i]));
        }
    }

    /// <summary>A shut Dogfight host door and two shut guest doors, all on one loopback wire. The
    /// guests go by Nathan and Sheila, and the host by <see cref="HostName"/> until a lobby names it.
    /// </summary>
    public static (NetPlayFeature Host, IReadOnlyList<NetPlayFeature> Guests) DogfightDoors(AidInternet internet = AidInternet.None)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(1));
        var carrier = Carrier(mesh[0], internet);
        var host = new NetPlayFeature(
            (port, maxGuests, bind) => carrier,
            (address, port) => throw new InvalidOperationException("the aid's host door joins nothing"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, ExternalAddress, "aid"),
                port => { }))
        { PlayerName = HostName, Master = MasterOf(internet) };
        string[] names = { "Nathan", "Sheila" };
        var guests = new List<NetPlayFeature>();
        for (int i = 0; i < names.Length; i++)
        {
            var end = mesh[i + 1];
            guests.Add(new NetPlayFeature(
                (port, maxGuests, bind) => throw new InvalidOperationException("the aid's guest door hosts nothing"),
                (address, port) => end)
            { PlayerName = names[i] });
        }

        return (host, guests);
    }

    /// <summary>Poses an open Dogfight lobby with Time 5 and Limited Lives on the Hawaii map. The first
    /// guest is Ready on its third stock plane, and the host and that guest have each said one line.
    /// </summary>
    public static void PoseDogfight(NetPlayFeature host, IReadOnlyList<NetPlayFeature> guests)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(guests);
        SettleDogfight(host, guests);
        if (host.Dogfight is not { } lobby || guests.Count == 0 || guests[0].Dogfight is not { } first)
        {
            return;
        }

        // Each guest's own lobby screen would show its lobby, which is what sends its name.
        foreach (var guest in guests)
        {
            guest.Dogfight?.Show();
        }

        lobby.SetEnvironment(1);
        lobby.SetVictory(DogfightVictory.Time);
        lobby.SetTimeMinutes(5);
        lobby.SetLimitedLives(true);
        SettleDogfight(host, guests);
        first.Pick(2, first.Fit);
        first.SetReady(true);
        lobby.Say("Five minutes, three lives each.");
        SettleDogfight(host, guests);
        first.Say("Ready when you are.");
        SettleDogfight(host, guests);
    }

    /// <summary>The Game Scores lines of a finished three-pilot match on a posed lobby, named from
    /// its player list. The first guest leads on two kills, the host has one, and the second guest
    /// also crashed once.</summary>
    public static DogfightScore[] PlayedScores(NetPlayFeature host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var names = new List<string>();
        foreach (var player in host.Dogfight?.Players ?? Array.Empty<DogfightLobbySeat>())
        {
            names.Add(player.Name);
        }

        var match = new CSVM.Flight.Modes.VersusMatch(3, killTarget: 0, timeLimit: 300f);
        match.RegisterKill(1, 0);
        match.RegisterKill(1, 2);
        match.RegisterKill(0, 1);
        match.RegisterDeath(2);
        match.Advance(300f);
        return DogfightLobby.ScoresOf(match.Standings(), names);
    }

    /// <summary>The Game Scores lines of a finished match over every row of a posed lobby with bots.
    /// They are named off a seat roster built from those rows as a launch seats them. The first bot
    /// leads on three kills, the host has one and the first guest crashed once.</summary>
    public static DogfightScore[] PlayedBotScores(NetPlayFeature host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var rows = host.Dogfight?.Players ?? Array.Empty<DogfightLobbySeat>();
        var seats = new NetSeat[rows.Count];
        int firstBot = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            seats[i] = new NetSeat { SeatIndex = i, Callsign = rows[i].Name, Pilot = rows[i].IsBot ? NetPilot.Bot : NetPilot.Human };
            firstBot = firstBot < 0 && rows[i].IsBot ? i : firstBot;
        }

        var match = new CSVM.Flight.Modes.VersusMatch(Math.Max(rows.Count, 2), killTarget: 0, timeLimit: 300f);
        int leader = firstBot >= 0 ? firstBot : 0;
        for (int kill = 0; kill < 3; kill++)
        {
            match.RegisterKill(leader, (leader + 1 + kill) % match.PlayerCount);
        }

        match.RegisterKill(0, leader);
        match.RegisterDeath(1);
        match.Advance(300f);
        return DogfightLobby.ScoresOf(match, seats);
    }

    /// <summary>A Dogfight guest door joined to a host that is flying a match. The host's advert
    /// reads In mission and no options come, as for a player who joined too late for it.</summary>
    public static NetPlayFeature LateDogfightGuest()
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(1));
        var lobby = new NetLobby(mesh[0]);
        lobby.Advertise(new SessionAdvertMessage(
            NetSessionKind.Dogfight, 1, 6, HostName, NetSessionStatus.InMission, NetSeats.MaxPlayers));
        var door = new NetPlayFeature(
            (port, maxGuests, bind) => throw new InvalidOperationException("the aid's guest door hosts nothing"),
            (address, port) => mesh[1])
        { PlayerName = "Lucy" };
        door.OpenJoin();
        for (int step = 0; step < 4; step++)
        {
            lobby.Step(0.0);
            door.Step(0.0);
        }

        return door;
    }

    /// <summary>Steps every door of a posed lobby until what each sent has landed on the others.
    /// </summary>
    public static void SettleDogfight(NetPlayFeature host, IReadOnlyList<NetPlayFeature> guests)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(guests);
        for (int round = 0; round < 4; round++)
        {
            host.Step(0.0);
            foreach (var guest in guests)
            {
                guest.Step(0.0);
            }
        }
    }

    /// <summary>A shut door whose LAN search hears <see cref="SampleGames"/>, answered at once
    /// from documentation addresses. With <paramref name="silent"/> nothing answers, so the
    /// games list stands on its Searching box.</summary>
    public static NetPlayFeature Searching(bool silent = false) => new(
        (port, maxGuests, bind) => throw new InvalidOperationException("the aid's search door hosts nothing"),
        (address, port) => throw new InvalidOperationException("the aid's search door joins nothing"),
        lan: (bind, port) => new SampleLan(silent))
    {
        Version = SampleVersion,
    };

    /// <summary>A shut guest door with a master server set and a code opener, so the Connection
    /// page's Join by code way stands live. Its opener joins nothing.</summary>
    public static NetPlayFeature CodeGuest() => new(
        (port, maxGuests, bind) => throw new InvalidOperationException("the aid's code door hosts nothing"),
        (address, port) => throw new InvalidOperationException("the aid's code door joins nothing"))
    {
        Version = SampleVersion,
        Master = MasterOf(AidInternet.Code),
        OpenCode = code => throw new InvalidOperationException("the aid's code door joins nothing"),
    };

    /// <summary>A door joined over the loopback to a host advertising a campaign mission at
    /// <paramref name="missionSeq"/> with <paramref name="players"/> players in it. The advert
    /// has already landed when this returns.</summary>
    public static NetPlayFeature JoinedGuest(int missionSeq, int players) => Joined(missionSeq, players, out _);

    /// <summary>A door joined as <see cref="JoinedGuest"/> that has also heard its host name its
    /// boards as <paramref name="flow"/>, so a guest's campaign follows them. When given, the host
    /// has first named its hangar as <paramref name="hangar"/>. With <paramref name="ready"/> the
    /// guest has answered Ready under that round. The host goes by <see cref="HostName"/> and each
    /// guest by <see cref="GuestNames"/> in player order, this one included.</summary>
    public static NetPlayFeature CoopGuest(CoopFlowMessage flow, bool ready, IReadOnlyList<CoopHangarMessage>? hangar = null)
    {
        var door = Joined(flow.MissionSeq, flow.Humans, out var host);
        Span<byte> word = stackalloc byte[CoopHangarMessage.Size];
        foreach (var plane in hangar ?? Array.Empty<CoopHangarMessage>())
        {
            plane.Write(word);
            host.Send(host.Peers[0], word, NetReliability.Reliable);
        }

        var rows = new List<DogfightLobbySeat> { new(HostName, 0, false, true) };
        for (int slot = 1; slot < flow.Humans; slot++)
        {
            rows.Add(new DogfightLobbySeat(slot - 1 < GuestNames.Count ? GuestNames[slot - 1] : "", 0, false, false));
        }

        door.PlayerName = flow.Slot >= 1 && flow.Slot - 1 < GuestNames.Count ? GuestNames[flow.Slot - 1] : "";
        Span<byte> names = stackalloc byte[DogfightRosterMessage.Size];
        new DogfightRosterMessage(0, flow.Slot, rows).Write(names);
        host.Send(host.Peers[0], names, NetReliability.Reliable);

        Span<byte> bytes = stackalloc byte[CoopFlowMessage.Size];
        flow.Write(bytes);
        host.Send(host.Peers[0], bytes, NetReliability.Reliable);
        door.Step(0.0);
        if (ready)
        {
            door.Pick.Set(door.Pick.Airframe, true);
            door.Step(0.0);
        }

        return door;
    }

    /// <summary>The words a co-op host names <paramref name="host"/>'s hangar with while
    /// <paramref name="seats"/> humans fly it and none past the host has picked yet. Each plane
    /// carries the seat <see cref="CoopPlanePool.Resolve"/> gives it, no build, and its stored fit.
    /// </summary>
    public static CoopHangarMessage[] HangarWords(CampaignProfileDef? host, int seats)
    {
        var planes = host?.Planes ?? new List<OwnedPlane>();
        int count = Math.Min(planes.Count, byte.MaxValue);
        var names = new string[count];
        for (int at = 0; at < count; at++)
        {
            names[at] = planes[at].Name;
        }

        var picks = new int[Math.Max(1, seats)];
        Array.Fill(picks, CoopPlanePool.Unpicked);
        picks[0] = host?.SelectedPlane ?? CoopPlanePool.Unpicked;
        int[] flown = CoopPlanePool.Resolve(names, picks);
        var words = new CoopHangarMessage[count];
        for (int at = 0; at < count; at++)
        {
            int holder = Array.FindIndex(flown, plane => plane >= 0 && names[plane] == names[at]);
            words[at] = new CoopHangarMessage((byte)at, (byte)count, holder >= 0 ? (byte)holder : CoopHangarMessage.NoHolder,
                (byte)Math.Clamp(planes[at].Airframe, 0, byte.MaxValue), CoopFit.Of(planes[at].Ammo, planes[at].Ordnance),
                null, names[at]);
        }

        return words;
    }

    /// <summary><paramref name="end"/> as a host carrier the master server listed under
    /// <see cref="SampleCode"/>, so a pose or a suite shows a code with no server.</summary>
    public static INetTransport Listed(INetTransport end) => new ListedEnd(end ?? throw new ArgumentNullException(nameof(end)));

    /// <summary>The words an aid's internet argument is written with: <c>code</c> and
    /// <c>offline</c>, anything else none.</summary>
    public static AidInternet InternetOf(string word) => word switch
    {
        "code" => AidInternet.Code,
        "offline" => AidInternet.Offline,
        _ => AidInternet.None,
    };

    private static NetPlayFeature Joined(int missionSeq, int players, out INetTransport host)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(1));
        var lobby = new NetLobby(mesh[0]);
        lobby.Advertise(new SessionAdvertMessage(
            NetSessionKind.CampaignCoop, (byte)missionSeq, (byte)players, HostName));
        var door = new NetPlayFeature(
            (port, maxGuests, bind) => throw new InvalidOperationException("the aid's guest door hosts nothing"),
            (address, port) => mesh[1]);
        door.OpenJoin();
        door.Step(0.0);
        host = mesh[0];
        return door;
    }

    private static void Answer(NetPlayFeature host, INetTransport guest, CoopPickMessage pick)
    {
        Span<byte> bytes = stackalloc byte[CoopPickMessage.Size];
        pick.Write(bytes);
        guest.Send(guest.Peers[0], bytes, NetReliability.Reliable);
        host.Step(0.0);
    }

    private static INetTransport Carrier(INetTransport end, AidInternet internet) =>
        internet == AidInternet.Code ? Listed(end) : end;

    // The master server's list is never asked for by a host pose, so an empty one stands in.
    private static MasterDirectory? MasterOf(AidInternet internet) =>
        internet == AidInternet.None ? null : new MasterDirectory(_ => System.Threading.Tasks.Task.FromResult("{\"games\":[]}"));

    // A loopback end listed under SampleCode at once, as a WebRTC host is once the server answers.
    private sealed class ListedEnd : INetTransport, INetListing, IDisposable
    {
        private readonly INetTransport _inner;

        public ListedEnd(INetTransport inner) => _inner = inner;

        public string? JoinCode => SampleCode;

        public string ListingFault => "";

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers;

        public void List(MasterGame listing)
        {
        }

        public void Bind(INetTransportListener listener) => _inner.Bind(listener);

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            _inner.Send(peer, payload, reliability, channel);

        public void Disconnect(int peer) => _inner.Disconnect(peer);

        public void Step(double dt) => _inner.Step(dt);

        public void Dispose() => (_inner as IDisposable)?.Dispose();
    }

    // The aid's LAN: every query is answered by each sample game at once, from its own address.
    private sealed class SampleLan : ILanSocket
    {
        private readonly bool _silent;
        private readonly Queue<(byte[] Bytes, string Address)> _inbox = new();

        public SampleLan(bool silent) => _silent = silent;

        public void Send(string address, int port, ReadOnlySpan<byte> datagram)
        {
            if (_silent || !LanDiscovery.TryReadQuery(datagram, out uint token))
            {
                return;
            }

            foreach (var game in SampleGames)
            {
                byte[] reply = new byte[LanDiscovery.Size];
                LanDiscovery.WriteReply(reply, token, game.Port, game.Advert, game.Version);
                _inbox.Enqueue((reply, game.Address));
            }
        }

        public byte[]? Receive(out string address, out int port)
        {
            port = LanDiscovery.Port;
            if (_inbox.Count == 0)
            {
                address = "";
                return null;
            }

            var (bytes, from) = _inbox.Dequeue();
            address = from;
            return bytes;
        }

        public void Dispose() => _inbox.Clear();
    }
}
