using System.Collections.Generic;
using CSVM.Flight.Weapons;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The multiplayer door and the wire a flight carries across the launcher's sessions. It builds
/// the door the menu host registers and opens the command line's socket on a headless run. A menu
/// launch's wire it takes with the field <see cref="SeatFields"/> builds. In flight it steps
/// the door, and at the end it hands the wire back to the door or closes it. The launcher reads
/// the session's network context off it and routes the two exits it asks for. Module notes:
/// docs/architecture/Launch.md.
/// </summary>
internal sealed class NetFlight
{
    // How long a --net-host/--net-join launch waits at the socket for the other end before it
    // gives up and flies alone. Wall seconds, because ENet is on the wall clock. Long enough for
    // a second process to reach its own launch, short enough to bound a scripted run. TUNE.
    private const double NetLinkWaitSeconds = 30.0;

    // The launcher's two exits, which a flight's upkeep takes when the other end ends it.
    private readonly System.Action<MenuReturnDestination> _returnToMenu;
    private readonly System.Action _exitSession;

    // The open wire the last launch carried, which end of it this machine is, and, on a host,
    // the field the door saw. Null on every local launch.
    private Net.INetTransport? _wire;
    private Net.NetSeat[]? _roster;
    private bool _isHost;

    // Whether the flight under way is a co-op campaign's, whose door keeps stepping in flight.
    private bool _coopFlight;

    // Whether the flight under way came out of the Dogfight lobby, whose seats carry picked fits.
    private bool _lobbyFlight;

    // Set by a finished lobby match on its way out, so the door keeps the wire for the lobby.
    private bool _keepLobby;

    // A host's fit and custom plane for each seat, by seat, as its launch told the guests. A
    // guest reads its host's word off the door instead.
    private Net.CoopFit[] _seatFits = System.Array.Empty<Net.CoopFit>();
    private Net.NetPlaneBuild?[] _seatBuilds = System.Array.Empty<Net.NetPlaneBuild?>();
    private StockLoadouts? _stock;

    /// <summary>A flight's network state over the launcher's two exits, the menu at a destination
    /// and the session's own exit.</summary>
    public NetFlight(System.Action<MenuReturnDestination> returnToMenu, System.Action exitSession)
    {
        _returnToMenu = returnToMenu;
        _exitSession = exitSession;
    }

    /// <summary>Whether the flight under way came out of the Dogfight lobby with a pilot on a team,
    /// which picks the load screen's team dialog.</summary>
    public bool TeamedLobbyFlight => _lobbyFlight && Door?.Dogfight is { Teamed: true };

    // The multiplayer door, held here as well as on the menu host because the socket it opens
    // outlives the board. Null until OpenDoor, and on a launch with no menu.
    private NetPlayFeature? Door { get; set; }
    /// <summary>Whether a co-op guest's flight is over. Its host named another board or restarted
    /// the mission, or the link to the host is gone and the door has failed.</summary>
    public static bool CoopGuestFlightOver(NetPlayFeature door) => door.CoopFlightOver;

    /// <summary>A co-op host's restart, the door's half. The door takes the wire back from the
    /// flight that ends, then launches again under a new round. That round ends every guest's
    /// flight and holds the next opener for it. Null when the door will not launch.</summary>
    public static MenuNetLaunch? CoopRelaunch(NetPlayFeature door)
    {
        door.Reclaim();
        return door.IsCoopHost ? door.BuildLaunch() : null;
    }

    /// <summary>Where a finished lobby Dogfight or stunt race lands: its lobby's Game Scores. A
    /// match's seat is named by its callsign on <paramref name="seats"/>, the session's own roster,
    /// and a bot's line is marked. It is read while the session lives, before the lobby can move a
    /// seat. A launch with no roster names seats off the lobby's list at the launch. A race lands
    /// its board's own table. Null for any other flight, one left before its end, or a Built-in
    /// board.</summary>
    public static LobbyReturn? LobbyLanding(bool lobbyFlight, DogfightLobby? lobby, Flight.Modes.VersusMatch? match,
        IReadOnlyList<Net.NetSeat>? seats = null, Flight.Modes.StuntRace? race = null)
    {
        if (!lobbyFlight || lobby is not { Shown: true })
        {
            return null;
        }

        if (match is { Completed: true })
        {
            return new LobbyReturn(seats is { Count: > 0 }
                ? DogfightLobby.ScoresOf(match, seats)
                : DogfightLobby.ScoresOf(match, lobby.LaunchNames));
        }

        return RaceLanding(race) is { } table ? new LobbyReturn(System.Array.Empty<DogfightScore>(), table) : null;
    }

    /// <summary>Whether a lobby flight's match ends on Game Scores rather than on a results board.
    /// The host's presentation decides for every machine. A host lands when a lobby screen stands on
    /// its lobby, and a guest when its host's options say so.</summary>
    public static bool LandsOnScores(bool isHost, DogfightLobby? lobby) =>
        lobby != null && (isHost ? lobby.Shown : lobby.Options.HostLandsOnScores);

    /// <summary>An ended stunt race's table as the lobby's Game Scores draws it, a pilot who left
    /// marked; null for no race or one still running.</summary>
    public static IReadOnlyList<RaceTableRow>? RaceLanding(Flight.Modes.StuntRace? race) =>
        race is { Ended: true } ? UI.Menu.Original.OriginalRaceTable.Rows(race.Standings(), race.ZoneCount) : null;

    /// <summary>Whether a lobby Dogfight guest's host left its flight. The door, stepped in
    /// flight, has failed on a close notice or a lost link.</summary>
    public static bool VersusGuestFlightOver(NetPlayFeature door) =>
        door.Stage == NetDoorStage.Failed;

    /// <summary>The door's half of a network flight's end. A co-op door, or a lobby whose match
    /// ran to its end, takes <paramref name="wire"/> back. A host otherwise closes through its
    /// door, which tells every guest first. Anything else disposes the wire and shuts the door.
    /// </summary>
    public static void EndNetWire(NetPlayFeature? door, Net.INetTransport wire, bool keepLobby)
    {
        if (door != null && (door.IsCoopHost || door.IsCoopGuest || keepLobby) && door.Reclaim())
        {
            return;
        }

        // ⚠ Do not dispose a host's wire here. The door's close sends every guest the close notice
        // first; a bare dispose leaves a guest flying on until its link drops.
        if (door is { IsHost: true } && door.Reclaim())
        {
            Log.Info("core", $"net: left the flight as host, telling {door.Peers} guest(s) the session closed");
            door.Close();
            return;
        }

        if (wire is System.IDisposable open)
        {
            open.Dispose();
        }

        // A door that failed keeps its fault, which the Connection page then names.
        if (door is { Stage: not NetDoorStage.Failed } shut)
        {
            shut.Close();
        }
    }

    /// <summary>Builds the multiplayer door the menu host registers. The carrier and the router
    /// arrive as delegates, which keeps the feature and every board over it clear of the socket
    /// and the engine. Which carrier they open is <c>Net/NetCarrier.cs</c>'s.</summary>
    public NetPlayFeature OpenDoor(SessionSpec spec)
    {
        var version = Net.NetBuildVersion.Parse(BuildVersion.Current);
        var master = MasterServer(spec);
        Door = new NetPlayFeature(
            (port, guests, bind) => Net.NetCarrier.HostListed(port, guests, bind, master == null ? null : () => MasterServerLink.Open(master)),
            (address, port) => Net.NetCarrier.Join(address, port),
            new Net.RouterAccess(
                Net.NetCarrier.PortMap,
                Net.NetCarrier.PortUnmap,
                // The pinhole opens for the stable address, the one the IPv6 socket binds and the
                // board shows. A temporary address would rotate away from under the router's rule.
                Net.NetCarrier.Pinhole(HostAddress.StableGlobalIPv6),
                Net.NetCarrier.PinholeClose),
            Net.NetCarrier.Lan)
        {
            Version = version,
            Lan = { Networks = LocalNetworks.Ipv4 },
            Reach =
            {
                StableIpv6 = Net.NetCarrier.StableIpv6,
                LanIpv4 = Net.NetCarrier.LanIpv4,
                CopyText = DisplayServer.ClipboardSet,
            },
            Internet =
            {
                Master = master == null ? null : new Net.MasterDirectory(cancel => MasterServerLink.FetchGames(master, cancel)),
                OpenCode = master == null ? null : code => Net.NetCarrier.JoinCode(() => MasterServerLink.Open(master), code, version),
                WebRtcReady = Net.WebRtcTransport.Available,
            },
        };
        return Door;
    }

    /// <summary>The command line's own way onto a wire, for a scripted or headless run. It opens
    /// the socket, waits for the other end on the WALL clock, and leaves the fields a menu launch
    /// leaves. A socket that will not open leaves the launch local, with the reason logged. A
    /// smoke that flies alone reads better than one that never starts.</summary>
    public void OpenCli(SessionSpec spec, string messagesPath)
    {
        if (spec.NetHostPort == null && spec.NetJoin == null)
        {
            return;
        }

        try
        {
            if (spec.NetHostPort is { } port)
            {
                _wire = Net.NetCarrier.Host(port, Net.NetSeats.MaxPlayers - 1, spec.NetHostBind);
                _isHost = true;
            }
            else
            {
                var (address, joinPort) = SessionSpec.ParseJoin(spec.NetJoin!, Net.NetPorts.Game);
                _wire = Net.NetCarrier.Join(address, joinPort);
                _isHost = false;
            }
        }
        catch (System.Exception e) when (e is System.InvalidOperationException or System.ArgumentException)
        {
            Log.Error("core", $"net: the command line's socket would not open: {e.Message}");
            _wire = null;
            return;
        }

        if (spec.NetShape is { } shape)
        {
            _wire = Net.ShapedTransport.OnWallClock(_wire, shape);
            Log.Info("core", $"net: shaping this end's link both ways, {Net.ShapedTransport.Describe(shape)}, on top of the real link's own delay and loss");
        }

        AwaitCliLink(spec, messagesPath);
    }

    /// <summary>A local Dogfight with bots flies a seat roster with no wire. The roster outlives a
    /// restart, and the next launch's take clears it. A launch on a wire keeps the field it has.
    /// </summary>
    public void SeatLocalField(SessionSpec spec, string messagesPath) =>
        _roster = _wire == null ? SeatFields.LocalVersusField(spec, messagesPath) : _roster;

    /// <summary>The wire a menu launch carried, kept for the session build. A host also builds the
    /// match's field here: the transport's peer list is the field, and only the door has seen it.
    /// A guest builds none, since the host's roster replaces it. A lobby host sends every seat's
    /// fit and custom plane to every guest before the session's opener.</summary>
    public void TakeLaunch(MenuNetLaunch? net, IReadOnlyList<string> planes, IReadOnlyList<LoadoutChoice?> fits,
        IReadOnlyList<Flight.Hangar.CustomPlaneDef?> customs, string messagesPath)
    {
        _wire = net?.Transport;
        _isHost = net?.IsHost ?? false;
        _roster = null;
        _seatFits = System.Array.Empty<Net.CoopFit>();
        _seatBuilds = System.Array.Empty<Net.NetPlaneBuild?>();
        _lobbyFlight = _wire != null && Door is { Dogfight: not null };
        if (_wire == null || !_isHost)
        {
            return;
        }

        var rules = _lobbyFlight ? Door!.Dogfight!.Rules : (Net.NetPlaneRules?)null;
        System.Func<int, byte>? teamOf = _lobbyFlight ? Door!.Dogfight!.TeamOfPeer : null;
        var bots = _lobbyFlight ? Door!.Dogfight!.LaunchBots : System.Array.Empty<VsBotEntry>();
        var pool = bots.Count > 0 ? Session.Roster.BotSeats.CallsignPool(Mech3.Messages.Load(messagesPath)) : null;
        (_roster, _seatFits) = SeatFields.VersusLaunchField(_wire, planes, fits, _stock ??= StockLoadouts.Load(), rules, teamOf,
            bots, pool);
        _seatBuilds = SeatFields.SeatBuildsFor(_roster, customs, _wire, rules);
        if (_lobbyFlight)
        {
            Door!.TellSeatFits(_seatFits);
            Door.TellSeatBuilds(_seatBuilds);
        }
    }

    /// <summary>A co-op campaign launch's wire. The host's roster is its own seats and then each
    /// guest the door seated, under its name. Every seat's fit and build, and the campaign
    /// wingman's aeroplane, go to every guest before the session's opener. The wingman given here
    /// is the cabin's, when it kept one off its saved plane.
    /// </summary>
    public void TakeCoopLaunch(MenuNetLaunch? net, IReadOnlyList<string> planes,
        IReadOnlyList<LoadoutChoice?> fits, IReadOnlyList<Flight.Hangar.CustomPlaneDef?> customs, string profile,
        string? profilesDir, Net.CoopWingmanMessage? wingman)
    {
        _wire = net?.Transport;
        _isHost = net?.IsHost ?? false;
        _roster = null;
        _seatFits = System.Array.Empty<Net.CoopFit>();
        _seatBuilds = System.Array.Empty<Net.NetPlaneBuild?>();
        _coopFlight = _wire != null && Door is { IsCoopHost: true } or { IsCoopGuest: true };
        if (_wire == null || !_isHost || Door == null)
        {
            return;
        }

        (_roster, _seatFits) = SeatFields.CoopLaunchField(Door, _wire, planes, fits, _stock ??= StockLoadouts.Load());
        _seatBuilds = SeatFields.CoopSeatBuilds(_roster, customs, Door, _wire);
        Door.TellSeatFits(_seatFits);
        Door.TellSeatBuilds(_seatBuilds);
        Door.TellCoopWingman(wingman ?? SeatFields.CoopWingmanFor(profile, profilesDir));
        Log.Info("core", $"net: co-op launch with {_roster.Length - planes.Count} guest(s)");
    }

    /// <summary>Whether a restart must leave the flight under way flying, said in the log. Only a
    /// local flight or a co-op host's restarts. A second session on a carrier the first still holds
    /// throws, and the load screen never comes down.</summary>
    public bool RefusesRestart()
    {
        if (_wire == null || (_coopFlight && _isHost && Door is { IsCoopHost: true }))
        {
            return false;
        }

        Log.Warn("core", $"restart: this network flight has no co-op door to relaunch through, it flies on");
        return true;
    }

    /// <summary>The network half of a restart, once the old session is freed. A co-op host's door
    /// takes the wire back and launches again on the co-op retry's own path. Every guest follows
    /// into the new round, flown from <paramref name="spec"/>'s settings. A door that will not
    /// launch sends this machine to the menu, and the answer is false.</summary>
    public bool Restart(SessionSpec spec)
    {
        if (_wire == null)
        {
            return true;
        }

        _wire = null;
        if (CoopRelaunch(Door!) is not { } launch)
        {
            Log.Warn("core", $"restart: the co-op door would not launch again, back at the menu");
            _returnToMenu(MenuReturnDestination.TopLevel);
            return false;
        }

        TakeCoopLaunch(launch, spec.PlaneNames, spec.MenuLoadouts, spec.MenuCustomPlanes, spec.CampaignProfile ?? "",
            spec.ProfilesDir, spec.CampaignWingman);
        return true;
    }

    /// <summary>A copy of <paramref name="context"/> carrying the flight's network slice. That is the
    /// wire, the roster, the per-seat fit and plane lookups, a co-op guest's wingman and a lobby's
    /// team names. A match that lands on Game Scores also takes <paramref name="lobbyLanding"/>.
    /// </summary>
    public LauncherContext WithNet(LauncherContext context, System.Action? lobbyLanding) => context with
    {
        NetTransport = _wire,
        NetHost = _isHost,
        NetSeats = _roster,
        NetAirframes = _wire == null ? null : Flight.Hangar.StockAirframes.Nodes,
        NetSeatFit = _coopFlight || _lobbyFlight ? SeatFit : null,
        NetSeatBuild = _coopFlight || _lobbyFlight ? SeatBuild : null,
        NetCoopWingman = _coopFlight && !_isHost && Door is { } coopDoor ? () => coopDoor.CoopWingman : null,
        NetTeamNames = _lobbyFlight && Door?.Dogfight is { } teamLobby ? SeatFields.TeamNames(teamLobby.Teams) : null,
        VersusLobbyLanding = _lobbyFlight && LandsOnScores(_isHost, Door?.Dogfight) ? lobbyLanding : null,
    };

    /// <summary>Ctrl+C on a menu: a hosting door's join code, else its address, to the clipboard.
    /// </summary>
    public void CopyForGuests() => Door?.Reach.CopyForGuests();

    /// <summary>A co-op flight's upkeep. The door still seats, advertises and follows the host
    /// while the session carries its wire. A guest's flight ends when its host names any other
    /// board, or when the link to the host is gone.</summary>
    public void TickCoop(double delta, GameSession? session)
    {
        if (!_coopFlight || Door is not { } door || _wire == null || session is not { InSession: true })
        {
            return;
        }

        door.Step(delta);
        if (_isHost)
        {
            // A guest that walked out through its pause sheet keeps its link, so its word is the
            // only sign. Its seat leaves at once rather than flying on frozen.
            foreach (var guest in door.CoopGuests)
            {
                if (guest.Left)
                {
                    session.Wire.TakeGuestLeft(guest.Peer);
                }
            }

            return;
        }

        if (CoopGuestFlightOver(door))
        {
            Log.Info("core", $"net: co-op flight over, {(door.IsCoopGuest ? "the host left the mission" : $"the link ended ({door.Fault})")}");
            // The host's ending reaches the guest's director inside the host's own hold. A result
            // is therefore banked here whenever the host went on to its debrief.
            _returnToMenu(new CoopGuestReturn(session.Campaign?.Result?.Attempt));
        }
    }

    /// <summary>A lobby Dogfight's or stunt race's upkeep in flight; the session steps the wire,
    /// never the door. A host's door still advertises, so a player who joins mid-match waits in the
    /// lobby. A guest's door watches its host, and a host that leaves ends the flight here. A host
    /// that takes an ended race to the lobby takes this guest there, the race's table with it.
    /// </summary>
    public void TickVersus(double delta, GameSession? session)
    {
        if (!_lobbyFlight || Door is not { } door || _wire == null || session is not { InSession: true })
        {
            return;
        }

        door.Step(delta);
        if (!_isHost && VersusGuestFlightOver(door))
        {
            Log.Info("core", $"net: versus flight over, the host left ({door.Fault})");
            _returnToMenu(new LobbyReturn(System.Array.Empty<DogfightScore>()));
        }
        else if (!_isHost && session.Wire.Race is { LobbyCalled: true })
        {
            Log.Info("core", $"net: the host took the race back to the lobby");
            _exitSession();
        }
    }

    /// <summary>Where the live <paramref name="session"/> lands when it leaves: its lobby's Game
    /// Scores (<see cref="LobbyLanding"/>), or null for the launch's own destination. A landing
    /// keeps the wire for the lobby when the flight closes.</summary>
    public LobbyReturn? Landing(GameSession session)
    {
        var landing = LobbyLanding(_lobbyFlight, Door?.Dogfight, session.Dogfight?.Match, session.NetSeats, session.Race);
        _keepLobby = landing != null;
        return landing;
    }

    /// <summary>A co-op guest leaving a flight its host still flies says so before the wire goes
    /// back. The door ignores this once the host has named any other board.</summary>
    public void LeaveCoopMission(GameSession? session)
    {
        if (_coopFlight && !_isHost && session is { Campaign.Result: null })
        {
            Door?.LeaveCoopMission();
        }
    }

    /// <summary>The end of a network flight: the wire is dropped, and the door gives the router's
    /// forwarded port back. The door no longer closes a transport it handed over, so that half is
    /// here. A co-op door takes its wire back instead, since its session outlives a flight, and so
    /// does a lobby whose match ran to its end.</summary>
    public void Close()
    {
        if (_wire == null)
        {
            return;
        }

        var wire = _wire;
        bool keepLobby = _keepLobby;
        _wire = null;
        _roster = null;
        _isHost = false;
        _coopFlight = false;
        _lobbyFlight = false;
        _keepLobby = false;
        EndNetWire(Door, wire, keepLobby);
    }

    // The master server the door lists on. --master-server= beats the saved option, which beats the
    // project's default. A pinned run takes neither of the last two, so no suite or golden ever asks
    // a server anything.
    private static System.Uri? MasterServer(SessionSpec spec)
    {
        var master = spec.Det
            ? MasterAddress.Parse(spec.MasterServer)
            : MasterAddress.Choose(spec.MasterServer, OptionsStore.UserOptions().Load().NetMasterServer);
        if (master != null)
        {
            string webRtc = Net.WebRtcTransport.Available ? "loaded" : "not installed, so no internet host or join";
            Log.Info("core", $"net: master server {master} (WebRTC library {webRtc})");
        }

        return master;
    }

    // The wall-clock wait a command-line join needs. ENet times itself off real seconds, so the
    // session's own tight step loop cannot carry a handshake. The link is waited for here, once,
    // before anything builds. A host waits for its first guest, a guest for its host.
    private void AwaitCliLink(SessionSpec spec, string messagesPath)
    {
        if (_wire is not { } wire)
        {
            return;
        }

        // The link readout is the socket's. A carrier without one counts as linked once a peer is
        // on the roster, which is the door's own fallback rule.
        var link = wire as Net.INetLink;
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (waited.Elapsed.TotalSeconds < NetLinkWaitSeconds)
        {
            wire.Step(0.001);
            if (wire.Peers.Count > 0 && (link == null || link.LinkState == Net.NetLinkState.Up))
            {
                Log.Info("core", $"net: linked as {(_isHost ? "host" : "guest")} after {waited.Elapsed.TotalSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} s, {wire.Peers.Count} peer(s)");
                if (_isHost)
                {
                    _roster = SeatFields.CliHostField(spec, wire, messagesPath);
                }

                return;
            }

            OS.DelayMsec(1);
        }

        Log.Error("core", $"net: nobody on the wire after {NetLinkWaitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} s, flying this session alone");
        (wire as System.IDisposable)?.Dispose();
        _wire = null;
        _isHost = false;
    }

    // The fit a seat flown elsewhere carries: a host's own launch word, or a guest's door's.
    private LoadoutChoice? SeatFit(int seat) =>
        SeatFields.CoopSeatFitFor(seat, _isHost ? _seatFits : null, Door, _stock ??= StockLoadouts.Load());

    // The custom plane a seat flown elsewhere carries, by the same split.
    private Flight.Hangar.CustomPlaneDef? SeatBuild(int seat) =>
        SeatFields.SeatBuildFor(seat, _isHost ? _seatBuilds : null, Door);
}
