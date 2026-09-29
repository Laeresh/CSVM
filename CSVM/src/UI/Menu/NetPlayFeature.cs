using System;
using System.Collections.Generic;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>What the door is doing right now, the one word a board draws its state from.</summary>
public enum NetDoorStage
{
    /// <summary>No socket is open. The port and the address are editable.</summary>
    Shut,

    /// <summary>A listen server is open and admitting guests.</summary>
    Hosting,

    /// <summary>A join is on its way out and has not landed yet.</summary>
    Joining,

    /// <summary>A join landed: this end is linked to a host.</summary>
    Joined,

    /// <summary>The socket would not open, or the join was refused or timed out.</summary>
    Failed,
}

/// <summary>One guest a co-op host seated: its peer and its player number. It says whether the
/// guest is Ready this round. It names the hangar plane the host settled for it, with that plane's
/// airframe, fit and build. It carries the guest's callsign and its chosen voice's place, -1 for
/// none. <see cref="Left"/> says it walked out of the flight under way.</summary>
public readonly record struct CoopGuest(
    int Peer, int Slot, bool Ready, byte Airframe, CoopFit Fit = default, string Name = "", bool Left = false,
    int Plane = Session.Campaign.CoopPlanePool.Stock, NetPlaneBuild? Build = null, int Voice = -1);

/// <summary>
/// The multiplayer door as a shared feature. It owns the port and the address a board edits, the
/// socket it opens, and the link readouts it shows. The wire a launch carries away comes from here
/// too. Nothing here names a carrier or an engine type: the two factories and the LAN socket arrive
/// as delegates, and the router as a <see cref="RouterAccess"/>. The launcher therefore passes the
/// real ENet carrier and a suite passes a loopback one. A co-op host's boards are its
/// <see cref="HostFlow"/>, a co-op guest's pick its <see cref="Pick"/>, and the roster, the seats
/// and the session are the launcher's.
/// </summary>
public sealed class NetPlayFeature : IMenuFeature
{
    /// <summary>The port a host opens on unless the board is stepped off it. Unregistered and
    /// arbitrary: the original carried no port of its own, since DirectPlay chose one.</summary>
    public const int DefaultPort = 47500;

    /// <summary>The address a join opens on. This machine, so a board with nothing typed into it
    /// still names something that can answer.</summary>
    public const string DefaultAddress = "127.0.0.1";

    /// <summary>The longest an address may be. The widest IPv6 address, the IPv4-mapped form, is 45
    /// characters and 53 in brackets with a port. The rest is room for a zone.</summary>
    public const int AddressLimit = 64;

    /// <summary>How long a join may stand at <see cref="NetDoorStage.Joining"/> before the door
    /// gives up on it. ENet's own connect attempt gives up first on a routable address; this
    /// bounds the case where nothing answers at all.</summary>
    public const double JoinTimeoutSeconds = 12.0;

    /// <summary>How many humans a campaign mission flown together seats, local and remote alike.
    /// A guest past it is told the game is full and hung up on.</summary>
    public const int CoopHumans = 4;

    /// <summary>How long a closing host keeps stepping its socket after the close notices, so
    /// they leave before the socket's close discards what is still queued.</summary>
    public const double LingerSeconds = 0.5;

    /// <summary>How long a refused guest has to hang up on its own after the full notice, before
    /// the host hangs up on it.</summary>
    public const double RefuseGraceSeconds = 1.0;

    /// <summary>What a LAN search asks at unless a suite points it elsewhere.</summary>
    public const string BroadcastAddress = LanBroadcast.Limited;

    private readonly Func<int, int, string, INetTransport> _openHost;
    private readonly Func<string, int, INetTransport> _openJoin;
    private readonly Func<string, int, ILanSocket>? _lan;
    private readonly List<int> _admitted = new();
    private readonly List<(int Peer, double Waited)> _refused = new();

    private NetLobby? _transport;
    private DogfightLobby? _dogfight;
    private byte _epoch = 1;

    // A guest back from a flight while its host still names that flight. Cleared once the host
    // names any other board or a flight under another round, which a restart is.
    private bool _flownFlow;

    // The round of the co-op flight this guest launched into, null before its first launch. A flow
    // naming another round is a new flight.
    private byte? _flightEpoch;

    // A Dogfight guest back from a match: the round it launched under, until the host names a new
    // one. Until then, whatever arrives is the old match's tail and never a launch.
    private byte? _flownEpoch;
    private byte _launchEpoch;
    private NetLobby? _closing;
    private double _lingered;
    private LanResponder? _responder;
    private LanSearch? _search;
    private int _hostPeer = -1;
    private INetLink? _link;
    private NetSessionKind _kind = NetSessionKind.Dogfight;
    private byte _missionSeq = SessionAdvertMessage.NoMission;
    private string _hostName = "";
    private int _localPlayers = 1;
    private bool _released;
    private double _joining;
    private DoorReading _seen;

    /// <summary>A door over the carrier <paramref name="openHost"/> and <paramref name="openJoin"/>
    /// build. The first takes a port, a guest count and a bind address, the second an address
    /// and a port. A host asks <paramref name="router"/> for its port, and with none the board shows
    /// no mapping. The LAN search's socket is bound by <paramref name="lan"/> on an address and a
    /// port. With none there is no search and an open door answers none.</summary>
    public NetPlayFeature(
        Func<int, int, string, INetTransport> openHost,
        Func<string, int, INetTransport> openJoin,
        RouterAccess? router = null,
        Func<string, int, ILanSocket>? lan = null)
    {
        _openHost = openHost ?? throw new ArgumentNullException(nameof(openHost));
        _openJoin = openJoin ?? throw new ArgumentNullException(nameof(openJoin));
        Router = router ?? new RouterAccess();
        _lan = lan;
    }

    /// <summary>Where the door stands.</summary>
    public NetDoorStage Stage { get; private set; } = NetDoorStage.Shut;

    /// <summary>Moves on at the end of any <see cref="Step"/> that finds the door's state changed.
    /// That is a peer, a lobby message, the stage, a fault, the port mapping or the games heard. A
    /// menu repaints the screen showing when it moves, since no input event follows network news.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>This build's version as the door names it to every peer and every LAN search. A
    /// host refuses a guest whose version does not play with it, and a guest refuses such a host.
    /// Unknown unless the launcher sets it, which is what every suite's pair of doors shares.
    /// </summary>
    public NetBuildVersion Version { get; init; }

    /// <summary>Which interface a host binds. Every one of them by default, which is what a
    /// player on a network needs. ⚠ A scripted run sets the loopback address instead: a wildcard
    /// bind is what makes Windows put a firewall dialog on somebody's screen.</summary>
    public string BindAddress { get; set; } = "*";

    /// <summary>The port a host opens on, and the port a join is aimed at.</summary>
    public int Port { get; private set; } = DefaultPort;

    /// <summary>The address a join is aimed at, as typed. It may name its own port, which beats
    /// <see cref="Port"/> for the join (<see cref="NetEndpoint.Parse"/>).</summary>
    public string Address { get; private set; } = DefaultAddress;

    /// <summary>The host and port a join opens on: <see cref="Address"/> split, with
    /// <see cref="Port"/> where the address names none. Its text is the address a player writes.
    /// </summary>
    public NetEndpoint JoinTarget => NetEndpoint.Parse(Address, Port);

    /// <summary>Why the last open failed, or "" when none has. Shown on the board rather than
    /// thrown: a taken port and a refused join are both things a player fixes and retries.</summary>
    public string Fault { get; private set; } = "";

    /// <summary>The host's router: the port mapping and the IPv6 pinhole it asks for as it opens,
    /// and gives back on <see cref="Close"/>.</summary>
    public RouterAccess Router { get; }

    /// <summary>What this co-op host names to its guests about its boards and its films.</summary>
    public CoopHostFlow HostFlow { get; } = new();

    /// <summary>This co-op guest's pick from its host's hangar. It lasts the joined session across
    /// flights, and a new join starts on the starter.</summary>
    public CoopGuestPick Pick { get; } = new();

    /// <summary>Where a LAN search sends its query: the broadcast address by default. A suite sets
    /// the loopback, since a broadcast on the loopback proves nothing on Windows.</summary>
    public string SearchAddress { get; set; } = BroadcastAddress;

    /// <summary>The IPv4 networks this machine sits on, as address and mask, read each round. A
    /// search at the broadcast address also asks at each one's directed broadcast. Null asks at
    /// <see cref="SearchAddress"/> alone.</summary>
    public Func<IReadOnlyList<(string Address, string Mask)>>? LanNetworks { get; init; }

    /// <summary>Reads this machine's stable global IPv6 address when a host opens, or null for a
    /// carrier that is not reached by address. With none, a board names no address at all.</summary>
    public Func<string?>? StableIpv6 { get; init; }

    /// <summary>Reads this machine's address on its local IPv4 network when a host opens.</summary>
    public Func<string?>? LanIpv4 { get; init; }

    /// <summary>Puts text on the system clipboard, the host's copy of its address. A seam, so a
    /// suite reads the copy without writing the pilot's own clipboard.</summary>
    public Action<string>? CopyText { get; init; }

    /// <summary>This host's stable global IPv6 address as read when it opened, or null.</summary>
    public string? HostIpv6 { get; private set; }

    /// <summary>This host's local IPv4 address as read when it opened, or null.</summary>
    public string? HostLanIpv4 { get; private set; }

    /// <summary>Whether this door can name its host's address, which a board shows only then.
    /// </summary>
    public bool NamesHostAddress => StableIpv6 != null;

    /// <summary>How many times this host's address was copied since it opened.</summary>
    public int Copies { get; private set; }

    /// <summary>What a guest types to reach this host: the stable IPv6 address, else the router's
    /// mapped IPv4 address, else the LAN address. The port is written when it is not
    /// <see cref="DefaultPort"/>, and always for a mapping. Empty while not hosting or when none is
    /// known.</summary>
    public string GuestAddress
    {
        get
        {
            if (!IsHost)
            {
                return "";
            }

            if (HostIpv6 is { } v6)
            {
                return Dial(v6);
            }

            if (Router.PortMap is { IsMapped: true } map)
            {
                return new NetEndpoint(map.ExternalAddress, map.Port).ToString();
            }

            return HostLanIpv4 is { } lan ? Dial(lan) : "";
        }
    }

    /// <summary>How many other peers are on the wire: the guests a host has, or 1 once a guest
    /// has reached its host. A guest a campaign host refused as full is not counted.</summary>
    public int Peers
    {
        get
        {
            if (_transport == null)
            {
                return 0;
            }

            int peers = 0;
            foreach (int peer in _transport.AllPeers)
            {
                peers += Refused(peer) ? 0 : 1;
            }

            return peers;
        }
    }

    /// <summary>Whether this door can search the LAN and answer a search.</summary>
    public bool CanSearch => _lan != null;

    /// <summary>Whether a LAN search is open.</summary>
    public bool Searching => _search != null;

    /// <summary>How many rounds the open search has asked, 0 while none is open.</summary>
    public int SearchRounds => _search?.Rounds ?? 0;

    /// <summary>The open doors the LAN search heard, empty while none is open.</summary>
    public IReadOnlyList<LanGame> Games => _search?.Games ?? (IReadOnlyList<LanGame>)Array.Empty<LanGame>();

    /// <summary>Why the LAN search would not open, or "" when it did.</summary>
    public string SearchFault { get; private set; } = "";

    /// <summary>Whether this host is answering LAN searches.</summary>
    public bool Answering => _responder != null;

    /// <summary>The link as the carrier reports it, or null for a carrier with no word for it.
    /// </summary>
    public EnetLinkState? Link => _link?.LinkState;

    /// <summary>Whether the host has answered this guest already. A guest's own launch waits on
    /// this, rather than timing out against a host that has not flown yet. The answer is held
    /// by the lobby until a session binds it, so the held count is the sign.</summary>
    public bool HostStarted => _transport is { Held: > 0 };

    /// <summary>Whether this end owns the match, meaningful once the door is open.</summary>
    public bool IsHost => Stage == NetDoorStage.Hosting;

    /// <summary>What a host's door holds open: a Dogfight from the Network board, or a campaign
    /// mission from the campaign's own boards.</summary>
    public NetSessionKind HostKind => _kind;

    /// <summary>Whether this door is hosting a campaign mission.</summary>
    public bool IsCoopHost => IsHost && _kind == NetSessionKind.CampaignCoop;

    /// <summary>Whether this door is a guest linked to a host holding a campaign mission open.
    /// </summary>
    public bool IsCoopGuest => Stage == NetDoorStage.Joined && Advert is { Kind: NetSessionKind.CampaignCoop };

    /// <summary>Whether this door is a guest linked to a host holding a Dogfight open.</summary>
    public bool IsDogfightGuest => Stage == NetDoorStage.Joined && Advert is { Kind: NetSessionKind.Dogfight };

    /// <summary>The Multiplayer Lobby this door stands in, or null. A host has one once
    /// <see cref="OpenDogfightHost"/> opened it. A Dogfight guest has one once linked.</summary>
    public DogfightLobby? Dogfight => _dogfight;

    /// <summary>Whether this Dogfight guest's host has launched from its lobby: the options are
    /// heard and the session's opener is waiting here.</summary>
    public bool DogfightLaunchDue =>
        IsDogfightGuest && !_released && _flownEpoch == null && _dogfight is { HasOptions: true } && _transport!.Held > 0;

    /// <summary>The host's word about its session as this guest last heard it, or null while
    /// none has arrived. A join board names the session from this.</summary>
    public SessionAdvertMessage? Advert => _transport?.Advert;

    /// <summary>The word this host hands out, or null while the door is not hosting.</summary>
    public SessionAdvertMessage? Advertising => _transport?.Advertising;

    /// <summary>Whether a launch may leave through this door. A host may fly alone and wait for
    /// nobody; a guest may not fly before its link stands.</summary>
    public bool CanLaunch => Stage == NetDoorStage.Hosting || Stage == NetDoorStage.Joined;

    /// <summary>Whether a launch took this door's wire and has not handed it back yet.</summary>
    public bool Released => _released;

    /// <summary>The round of picks under way on this host. It moves on whenever the host names a
    /// new mission or leaves the briefing and flight check, and a pick counts only under it.
    /// </summary>
    public byte CoopEpoch => _epoch;

    /// <summary>Whether every guest this co-op host seated is Ready under the current round. True
    /// with no guest seated, since a host may fly its campaign alone.</summary>
    public bool CoopAllReady
    {
        get
        {
            foreach (int peer in _admitted)
            {
                if (!ReadyNow(peer))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The guests this co-op host seated, in player order: each one's player number, its
    /// Ready mark under the current round, and the plane it flies. A guest flies its own fit on the
    /// plane it picked, and the plane's stored fit on one the host's settling gave it instead.
    /// </summary>
    public IReadOnlyList<CoopGuest> CoopGuests
    {
        get
        {
            var guests = new List<CoopGuest>(_admitted.Count);
            for (int i = 0; i < _admitted.Count; i++)
            {
                int peer = _admitted[i];
                var pick = _transport!.Picks.TryGetValue(peer, out var sent) ? sent : default;
                int plane = HostFlow.PlaneOf(_localPlayers + i);
                var word = HostFlow.HangarAt(plane);
                byte airframe = word?.Airframe ?? CoopGuestPick.StarterAirframe;
                var fit = word is { } flown && pick.PlaneIndex != plane ? flown.Fit : pick.Fit;
                guests.Add(new CoopGuest(peer, _localPlayers + i, ReadyNow(peer), airframe,
                    fit, pick.Name ?? "", pick.Left && pick.Epoch == _epoch, plane, word?.Build, PilotVoices.FromWire(pick.Voice)));
            }

            return guests;
        }
    }

    /// <summary>The plane of the hangar each guest this co-op host seated picked, in player order,
    /// as <see cref="CoopGuestPick.Plane"/> names one. What the host settles every seat's pick from.
    /// </summary>
    public IReadOnlyList<int> CoopGuestPlanes
    {
        get
        {
            var planes = new int[_admitted.Count];
            for (int i = 0; i < planes.Length; i++)
            {
                planes[i] = _transport!.Picks.TryGetValue(_admitted[i], out var pick)
                    ? pick.PlaneIndex
                    : Session.Campaign.CoopPlanePool.Unpicked;
            }

            return planes;
        }
    }

    /// <summary>The co-op host's hangar as this guest last heard it, each plane with the seat that
    /// holds it. Empty on a door that is not a co-op guest's, and until the host has named it.
    /// </summary>
    public IReadOnlyList<CoopHangarMessage> CoopHangar =>
        IsCoopGuest && _transport != null ? _transport.Hangar : Array.Empty<CoopHangarMessage>();

    /// <summary>The host's latest word about its boards as this co-op guest heard it, or null.
    /// </summary>
    public CoopFlowMessage? CoopFlow => IsCoopGuest ? _transport?.Flow : null;

    /// <summary>How many co-op flows this guest has heard, so a board can tell news from a repeat.
    /// </summary>
    public int CoopFlows => _transport?.Flows ?? 0;

    /// <summary>The callsign this end's player goes by. A guest's pick carries it to the host's
    /// roster, and a host's own first seat takes it. Empty when the player has none, and the roster
    /// then uses the player number.</summary>
    public string PlayerName { get; set; } = "";

    /// <summary>The pilot voice this end's player chose, as its place in
    /// <see cref="PilotVoices.All"/>, or -1 for none. It rides every pick this end sends.</summary>
    public int Voice { get; set; } = -1;

    /// <summary>The name this host's advert gives its game. Empty advertises the host's own name,
    /// as a door opened without the Game Information box does.</summary>
    public string GameName { get; set; } = "";

    /// <summary>The Maximum # of Players this host chose, 0 for its kind's cap. It is held to
    /// <see cref="NetPlayerInfo.ClampPlayers"/> whenever it is read.</summary>
    public int MaxPlayers { get; set; }

    /// <summary>The optional password this host set. Kept here; no message carries it.</summary>
    public string Password { get; set; } = "";

    /// <summary>How many players the session this door stands in seats: this host's chosen cap,
    /// else the cap its host's advert names.</summary>
    public int SessionCap =>
        IsHost ? HostCap : Advert is { Cap: > 0 } advert ? advert.Cap : NetPlayerInfo.PlayerCap(Advert?.Kind ?? _kind);

    /// <summary>Each seat's fit as this co-op guest's host launched it, by seat.</summary>
    public IReadOnlyDictionary<int, CoopFit> CoopSeatFits =>
        _transport?.SeatFits ?? (IReadOnlyDictionary<int, CoopFit>)new Dictionary<int, CoopFit>();

    /// <summary>Each seat's custom plane as this guest's host launched it, by seat, null for a stock
    /// seat.</summary>
    public IReadOnlyDictionary<int, NetPlaneBuild?> SeatBuilds =>
        _transport?.SeatBuilds ?? (IReadOnlyDictionary<int, NetPlaneBuild?>)new Dictionary<int, NetPlaneBuild?>();

    /// <summary>The campaign wingman's aeroplane as this co-op guest's host launched it, or null
    /// while the host has named none.</summary>
    public CoopWingmanMessage? CoopWingman => IsCoopGuest ? _transport?.Wingman : null;

    /// <summary>This co-op guest's host's latest film word, a start or an end, or null while the
    /// host has shared no film.</summary>
    public CoopFilmMessage? CoopFilm => IsCoopGuest ? _transport?.Film : null;

    /// <summary>Whether this co-op guest is Ready under the host's current round.</summary>
    public bool CoopReady => CoopFlow is { } flow && Pick.ReadyUnder(flow.Epoch);

    /// <summary>Whether this co-op guest's host has launched the mission it is seated for: the host
    /// names the flight and its session's opener is waiting here. A guest that arrived mid-mission
    /// has no opener, since the flight's field was fixed before it came, and waits.</summary>
    public bool CoopLaunchDue =>
        IsCoopGuest && !_released && !_flownFlow && CoopFlow is { Screen: NetCoopScreen.InMission } && _transport!.Held > 0;

    /// <summary>Whether this co-op guest's flight is over. It is when the link is gone, or when the
    /// host names a board or a flight other than the one this guest launched into.</summary>
    public bool CoopFlightOver =>
        !IsCoopGuest || CoopFlow is not { Screen: NetCoopScreen.InMission } flow || (_flightEpoch is { } flown && flow.Epoch != flown);

    // The cap this host's advert names and its admission keeps: the chosen one inside its kind's.
    private int HostCap => MaxPlayers > 0 ? NetPlayerInfo.ClampPlayers(_kind, MaxPlayers) : NetPlayerInfo.PlayerCap(_kind);

    /// <summary>Takes what the Game and Player Information boxes answered. The callsign and voice
    /// are always taken. The game's name, cap and password are taken when <paramref name="game"/>
    /// says the host's box was shown.</summary>
    public void Take(NetPlayerInfo info, bool game)
    {
        ArgumentNullException.ThrowIfNull(info);
        PlayerName = info.Callsign.Trim();
        Voice = PilotVoices.Clamp(info.Voice);
        if (game)
        {
            GameName = info.GameName.Trim();
            MaxPlayers = info.MaxPlayers;
            Password = info.Password;
        }
    }

    /// <summary>The voice a guest's latest pick carried to this host, as its place in
    /// <see cref="PilotVoices.All"/>, or -1 when the guest sent none.</summary>
    public int PickedVoice(int peer) =>
        _transport != null && _transport.Picks.TryGetValue(peer, out var pick) ? PilotVoices.FromWire(pick.Voice) : -1;

    /// <summary><paramref name="host"/> as a guest types it for this door's port: bare on
    /// <see cref="DefaultPort"/>, which a join fills in, and with the port otherwise.</summary>
    public string Dial(string host) => Port == DefaultPort ? host : new NetEndpoint(host, Port).ToString();

    /// <summary>Copies <see cref="GuestAddress"/> to the clipboard. False, and nothing copied,
    /// while there is no address to give or no clipboard to put it on.</summary>
    public bool CopyGuestAddress()
    {
        string address = GuestAddress;
        if (address.Length == 0 || CopyText == null)
        {
            return false;
        }

        CopyText(address);
        Copies++;
        return true;
    }

    /// <summary>What this co-op host's boards show, named to every guest on the next step. A new
    /// mission starts a new round of picks, as does a move onto a board other than the briefing
    /// and flight check. Every Ready then clears.</summary>
    public void ShowCoop(NetCoopScreen screen, int missionSeq, int progress, ushort airframes)
    {
        if (HostFlow.Show(screen, missionSeq, progress, airframes))
        {
            NextRound();
        }
    }

    /// <summary>This co-op guest walks out of the flight under way. The host hears it at once and
    /// takes the guest's aeroplane out of its mission, rather than waiting for a link that never
    /// drops. The mark belongs to the flight's round and clears when the host names another.
    /// </summary>
    public void LeaveCoopMission()
    {
        if (CoopFlightOver)
        {
            return;
        }

        // The first follow settles the round, which would otherwise clear a mark set under it.
        FollowHost();
        Pick.Leave();
        FollowHost();
    }

    /// <summary>Sends every seated guest each seat's fit, <paramref name="bySeat"/> indexed by
    /// seat. A host calls this at its launch, before the session's opener, so a guest has
    /// every fit in hand when it builds the field.</summary>
    public void TellSeatFits(IReadOnlyList<CoopFit> bySeat)
    {
        ArgumentNullException.ThrowIfNull(bySeat);
        if (_transport == null || !IsHost)
        {
            return;
        }

        foreach (int peer in IsCoopHost ? _admitted : _transport.AllPeers)
        {
            for (int seat = 0; seat < bySeat.Count && seat <= byte.MaxValue; seat++)
            {
                _transport.Tell(peer, new CoopSeatFitMessage((byte)seat, bySeat[seat]));
            }
        }
    }

    /// <summary>Sends every seated guest each seat's custom plane, null for a stock seat,
    /// <paramref name="bySeat"/> indexed by seat. A host calls this at its launch beside
    /// <see cref="TellSeatFits"/>, so a guest builds every custom plane before it reports loaded.
    /// </summary>
    public void TellSeatBuilds(IReadOnlyList<NetPlaneBuild?> bySeat)
    {
        ArgumentNullException.ThrowIfNull(bySeat);
        if (_transport == null || !IsHost)
        {
            return;
        }

        foreach (int peer in IsCoopHost ? _admitted : _transport.AllPeers)
        {
            for (int seat = 0; seat < bySeat.Count && seat < PlaneBuildMessage.Mine; seat++)
            {
                _transport.Tell(peer, new PlaneBuildMessage((byte)seat, bySeat[seat]));
            }
        }
    }

    /// <summary>Sends every seated co-op guest the campaign wingman's aeroplane. A host calls this at
    /// its launch, before the session's opener, beside <see cref="TellSeatFits"/>.</summary>
    public void TellCoopWingman(CoopWingmanMessage wingman)
    {
        if (_transport == null || !IsCoopHost)
        {
            return;
        }

        foreach (int peer in _admitted)
        {
            _transport.Tell(peer, wingman);
        }
    }

    /// <summary>Tells every seated co-op guest that this host's campaign film
    /// <paramref name="film"/> has started, so each plays it too. A chapter film names its
    /// <paramref name="chapter"/>. Nothing is sent from a door that is not a co-op host's.</summary>
    public void ShowCoopFilm(NetCoopFilm film, int chapter = 0)
    {
        if (_transport == null || !IsCoopHost)
        {
            return;
        }

        TellFilm(HostFlow.StartFilm(film, chapter));
    }

    /// <summary>Tells every seated co-op guest that this host's film has stopped, played out or
    /// skipped, so each guest's stops with it. Nothing is sent while no film is shared.</summary>
    public void EndCoopFilm()
    {
        if (HostFlow.EndFilm() is { } ended && _transport != null && IsCoopHost)
        {
            TellFilm(ended);
        }
    }

    /// <summary>Takes back the wire a co-op or lobby launch carried away once its flight ends. The
    /// door then holds the link through the debrief or the lobby that follows. False when this door
    /// no longer holds that wire, in which case the caller disposes it.</summary>
    public bool Reclaim()
    {
        if (!_released || _transport == null)
        {
            return false;
        }

        _transport.Unbind();
        _released = false;
        HostFlow.ClearSent();
        _flownFlow = IsCoopGuest;
        _flownEpoch = IsDogfightGuest ? _launchEpoch : null;
        return true;
    }

    /// <summary>Steps the port by <paramref name="by"/>, wrapping inside the unprivileged range.
    /// Refused while a socket is open, since the open one is the port that matters.</summary>
    public void StepPort(int by)
    {
        if (_transport != null)
        {
            return;
        }

        int port = Port + by;
        Port = port < 1024 ? 65535 : port > 65535 ? 1024 : port;
    }

    /// <summary>Appends typed characters to the address, answering how many it took. Refused while
    /// a socket is open, past <see cref="AddressLimit"/>, and for anything outside the characters
    /// an address is written with.</summary>
    public int TypeAddress(string typed)
    {
        if (_transport != null || string.IsNullOrEmpty(typed))
        {
            return 0;
        }

        int taken = 0;
        foreach (char c in typed)
        {
            bool allowed = char.IsAsciiLetterOrDigit(c) || c is '.' or ':' or '%' or '-' or '[' or ']';
            if (allowed && Address.Length < AddressLimit)
            {
                Address += c;
                taken++;
            }
        }

        return taken;
    }

    /// <summary>Appends pasted text to the address, trimmed of the whitespace a copied address
    /// carries, under <see cref="TypeAddress"/>'s rule and cap. Answers how many characters it
    /// took and whether any were left out, refused or past the cap, which a box cues as a reject.
    /// </summary>
    public (int Taken, bool Dropped) PasteAddress(string? clipboard)
    {
        string text = clipboard?.Trim() ?? string.Empty;
        int taken = TypeAddress(text);
        return (taken, taken < text.Length);
    }

    /// <summary>Takes the last character off the address.</summary>
    public void EraseAddress()
    {
        if (_transport == null && Address.Length > 0)
        {
            Address = Address[..^1];
        }
    }

    /// <summary>Opens a listen server for <paramref name="maxGuests"/> guests on
    /// <see cref="BindAddress"/> and asks the router for the port. A socket that will not open
    /// leaves the door shut with the reason on <see cref="Fault"/>.</summary>
    /// <remarks>The lobby's host side runs behind it unshown, so an Original guest can pick, chat
    /// and ready up against a Built-in host.</remarks>
    public void OpenHost(int maxGuests)
    {
        OpenHost(maxGuests, NetSessionKind.Dogfight);
        if (_transport != null && Stage == NetDoorStage.Hosting)
        {
            _dogfight = new DogfightLobby(_transport, () => PlayerName) { Seated = peer => !Refused(peer) };
        }
    }

    /// <summary>Opens a listen server with the Multiplayer Lobby standing on it, the Connection
    /// page's Host. It is <see cref="OpenHost(int)"/> with the lobby shown, whose environment the
    /// advert names. The host's pilot goes by <see cref="PlayerName"/>.</summary>
    public void OpenDogfightHost(int maxGuests)
    {
        OpenHost(maxGuests);
        if (_transport != null && _dogfight != null)
        {
            _hostName = PlayerName;
            _dogfight.Show();
            foreach (string line in CoopDoorText.HostAddressNotes(this))
            {
                _dogfight.Note(CoopDoorText.NoteName, line);
            }

            _transport.Advertise(CurrentAdvert());
        }
    }

    /// <summary>Opens a listen server for a campaign mission flown together, the campaign
    /// boards' own door. It is <see cref="OpenHost(int)"/> with the advert naming the campaign;
    /// <see cref="Offer"/> fills in which mission and whose profile. The socket admits up to
    /// <see cref="CoopHumans"/> guests, so one past the cap can hear why it is refused. The step
    /// refuses every guest past the cap in arrival order.</summary>
    public void OpenCoopHost(int maxGuests) =>
        OpenHost(Math.Min(maxGuests, CoopHumans), NetSessionKind.CampaignCoop);

    /// <summary>What a coop host's advert names: its next mission, its host name, and its local
    /// player count. Guests are counted on top of
    /// <paramref name="localPlayers"/>. Reaches the peers on the next <see cref="Step"/>.</summary>
    public void Offer(int missionSeq, string hostName, int localPlayers)
    {
        _missionSeq = missionSeq is >= 0 and < SessionAdvertMessage.NoMission
            ? (byte)missionSeq
            : SessionAdvertMessage.NoMission;
        _hostName = hostName ?? "";
        _localPlayers = Math.Max(1, localPlayers);
    }

    /// <summary>This co-op host's hangar in order, each plane with the seat holding it and its
    /// build. The plane every seat flies is <paramref name="seatPlanes"/>, this machine's own seats
    /// first. Every guest hears the hangar on the next <see cref="Step"/>, and a launch seats every
    /// guest in its plane.</summary>
    public void OfferCoopHangar(IReadOnlyList<CoopHangarMessage> hangar, IReadOnlyList<int> seatPlanes)
    {
        ArgumentNullException.ThrowIfNull(hangar);
        ArgumentNullException.ThrowIfNull(seatPlanes);
        HostFlow.ShowHangar(hangar, seatPlanes);
    }

    /// <summary>Starts a join to the typed address. The join lands on a later
    /// <see cref="Step"/>; until then the door stands at <see cref="NetDoorStage.Joining"/>.
    /// </summary>
    public void OpenJoin()
    {
        if (_transport != null)
        {
            return;
        }

        EndLinger();
        try
        {
            var (host, port) = JoinTarget;
            _transport = new NetLobby(_openJoin(host, port), Version);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            Fail(e.Message);
            return;
        }

        _link = _transport.Inner as INetLink;
        _hostPeer = -1;
        Fault = "";
        Stage = NetDoorStage.Joining;
        _joining = 0.0;
    }

    /// <summary>Whether a game the LAN search heard runs a build this one plays with.</summary>
    public bool PlaysWith(LanGame game) => Version.PlaysWith(game.Version);

    /// <summary>Joins a game the LAN search heard, at the address it answered from and the game
    /// port it named. A game of a version this build does not play with is refused before any
    /// socket opens, with both versions on <see cref="Fault"/>. The search stays open; the page
    /// that ran it closes it.</summary>
    public void JoinGame(LanGame game)
    {
        if (_transport != null || string.IsNullOrWhiteSpace(game.Address) || game.Port is < 1 or > 65535)
        {
            return;
        }

        if (!PlaysWith(game))
        {
            Fail(CoopDoorText.VersionMismatch(game.Version, Version));
            return;
        }

        Address = game.Address;
        Port = game.Port;
        OpenJoin();
    }

    /// <summary>Asks the LAN for open doors, opening the search on its first call. Each call is a
    /// new round, and a game that answers neither this round nor the last leaves
    /// <see cref="Games"/>. The answers land on later steps.</summary>
    public void Search()
    {
        if (_lan == null)
        {
            return;
        }

        if (_search == null)
        {
            try
            {
                _search = new LanSearch(_lan(BindAddress, 0), SearchTargets, LanDiscovery.Port);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                SearchFault = e.Message;
                return;
            }
        }

        SearchFault = "";
        _search.Ask();
    }

    /// <summary>Closes the LAN search and forgets what it heard.</summary>
    public void StopSearch()
    {
        _search?.Dispose();
        _search = null;
    }

    /// <summary>Drives the socket while the board is up. This is the only place a join lands, and
    /// the only place a guest arrives on a host's board. ⚠ The step is what carries the link, so
    /// a board that stops calling this stops hearing about its own match. It also carries the
    /// LAN search and the answers to one, and the close notices of a host that just closed.
    /// </summary>
    public void Step(double dt)
    {
        StepDoor(dt);

        // Read against the last step's reading rather than this step's start: a loopback carrier
        // delivers on the sender's send, between this door's steps.
        var reading = Read();
        if (reading != _seen)
        {
            _seen = reading;
            Revision++;
        }
    }

    /// <summary>The wire a launch carries, or null when this door is shut or still joining. The
    /// transport goes with it: the door neither steps nor closes it afterwards, because the
    /// session does both. The port mapping stays up until <see cref="Close"/>.</summary>
    public MenuNetLaunch? BuildLaunch()
    {
        if (_transport == null || !CanLaunch)
        {
            return null;
        }

        // A guest refused as full has no seat in the match the session builds off the roster.
        foreach (var (peer, _) in _refused)
        {
            _transport.Disconnect(peer);
        }

        if (_dogfight is { } lobby)
        {
            // A host's options and list go out ahead of the session's opener, so a guest launches
            // on what the host flies. The list is kept to name the seats on Game Scores.
            if (IsHost)
            {
                lobby.Step();
            }

            lobby.Launched();
            _launchEpoch = lobby.Options.Epoch;
        }

        _released = true;
        _transport.LocalCallsign = PlayerName;
        _transport.LocalVoice = PilotVoices.Wire(Voice);
        if (IsCoopHost)
        {
            // A launch straight out of a flight is a restart, and a new round is how a guest in
            // that flight learns it is over. Until it answers under that round, what it sends is
            // the old flight's.
            if (HostFlow.Screen == NetCoopScreen.InMission)
            {
                NextRound();
                _transport.AwaitPicks();
            }

            // Named before the session's opener is sent, so a guest knows the opener is this
            // flight's and not a stale one.
            ShowCoop(NetCoopScreen.InMission, HostFlow.MissionSeq, HostFlow.Progress, HostFlow.Airframes);
            SendFlows();
        }
        else if (CoopFlow is { } flow)
        {
            _flightEpoch = flow.Epoch;
        }

        return new MenuNetLaunch(_transport, IsHost);
    }

    /// <summary>Shuts the door. The socket is closed unless a launch took it, the mapping is
    /// taken down, and the port and address are left as they were typed. This is also where a
    /// match that took the transport gives the router's port back. The launcher therefore calls
    /// it at the end of a network flight, as a board does on the way out. A host's guests are
    /// each sent a close notice first, and the socket lingers on later steps so that it leaves.
    /// </summary>
    public void Close()
    {
        _responder?.Dispose();
        _responder = null;
        if (_transport != null && !_released)
        {
            if (Stage == NetDoorStage.Hosting)
            {
                Linger(_transport);
            }
            else
            {
                _transport.Dispose();
            }
        }

        _transport = null;
        _link = null;
        _released = false;
        _admitted.Clear();
        _refused.Clear();
        _dogfight = null;
        _flownEpoch = null;
        ForgetCoop();
        _hostPeer = -1;
        _kind = NetSessionKind.Dogfight;
        Offer(SessionAdvertMessage.NoMission, "", 1);
        Stage = NetDoorStage.Shut;
        ForgetHostAddress();
        Router.Close();
    }

    /// <summary>Drops everything transient: the socket goes with the presentation that opened it,
    /// since no board is left to show what it is doing. A closing socket and a search go too.
    /// </summary>
    public void Discard()
    {
        Close();
        EndLinger();
        StopSearch();
        Fault = "";
        SearchFault = "";
    }

    private static bool Contains(IReadOnlyList<int> peers, int peer)
    {
        for (int i = 0; i < peers.Count; i++)
        {
            if (peers[i] == peer)
            {
                return true;
            }
        }

        return false;
    }

    // The step itself. Every return leaves the door in a state Step then reads.
    private void StepDoor(double dt)
    {
        StepLinger(dt);
        _search?.Poll();
        if (_transport == null)
        {
            return;
        }

        // A released wire is stepped by the session that carries it. A co-op door still seats,
        // advertises and follows its link here, since it holds the session together in flight.
        bool coop = _kind == NetSessionKind.CampaignCoop || IsCoopGuest;
        if (_released && !coop)
        {
            // A Dogfight guest in flight still watches its host. A close notice or a lost link
            // both mean the host left the match.
            if (Stage == NetDoorStage.Joined && HostGone())
            {
                Fail(CoopDoorText.HostLeft);
                return;
            }

            _responder?.Poll(CurrentAdvert(), Port);
            return;
        }

        if (!_released)
        {
            _transport.Step(dt);
        }

        Router.Poll();
        if (Stage == NetDoorStage.Hosting)
        {
            RefuseClashing();
            if (_kind == NetSessionKind.CampaignCoop)
            {
                Admit();
                SendFlows();
            }
            else
            {
                RefuseOverCap();
            }

            HangUpRefused(dt);
            _dogfight?.Step();
            var advert = CurrentAdvert();
            _transport.Advertise(advert);
            _responder?.Poll(advert, Port);
            return;
        }

        // A host that says why it is sending this guest away is believed before its link drops.
        if (_transport.Closed is { } closed)
        {
            Fail(closed.Reason switch
            {
                NetCloseReason.Full => CoopDoorText.GameFull,
                NetCloseReason.VersionMismatch => CoopDoorText.VersionMismatch(closed.Host, closed.Guest),
                _ => CoopDoorText.HostClosed,
            });
            return;
        }

        // A guest refuses a host of another version itself, whether or not the host says so.
        if (_transport.Clashing.Count > 0 && _transport.TryVersionOf(_transport.Clashing[0], out var theirs))
        {
            Fail(CoopDoorText.VersionMismatch(theirs, Version));
            return;
        }

        if (Stage == NetDoorStage.Joined)
        {
            bool hostGone = _hostPeer >= 0 && !Contains(_transport.AllPeers, _hostPeer);
            if (_link?.LinkState == EnetLinkState.Down || hostGone)
            {
                Fail(CoopDoorText.HostLeft);
                return;
            }

            if (IsCoopGuest)
            {
                FollowHost();
            }
            else if (IsDogfightGuest)
            {
                _dogfight ??= new DogfightLobby(_transport, () => PlayerName, _hostPeer) { Voice = () => PilotVoices.Wire(Voice) };
                if (_flownEpoch is { } flown)
                {
                    // The host names a new round only once its own match is freed, so everything
                    // held up to that word is the old match's.
                    _transport.DropHeld();
                    _flownEpoch = _dogfight.Options.Epoch == flown ? flown : null;
                }

                _dogfight.Step();
            }

            return;
        }

        if (Stage != NetDoorStage.Joining)
        {
            return;
        }

        _joining += dt;
        if (_link?.LinkState == EnetLinkState.Up || (_link == null && _transport.AllPeers.Count > 0))
        {
            Stage = NetDoorStage.Joined;
            _hostPeer = _transport.AllPeers.Count > 0 ? _transport.AllPeers[0] : -1;
        }
        else if (_link?.LinkState == EnetLinkState.Down)
        {
            Fail($"{JoinTarget} refused the join");
        }
        else if (_joining >= JoinTimeoutSeconds)
        {
            Fail($"{JoinTarget} did not answer in {JoinTimeoutSeconds:0} seconds");
        }
    }

    // Both host doors open the same socket; only the advert's kind tells them apart.
    private void OpenHost(int maxGuests, NetSessionKind kind)
    {
        if (_transport != null)
        {
            return;
        }

        // A reopen on the same port cannot wait for the last close to finish lingering.
        EndLinger();
        try
        {
            _transport = new NetLobby(_openHost(Port, maxGuests, BindAddress), Version);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            Fail(e.Message);
            return;
        }

        _link = _transport.Inner as INetLink;
        _kind = kind;
        Fault = "";
        Stage = NetDoorStage.Hosting;
        HostIpv6 = StableIpv6?.Invoke();
        HostLanIpv4 = LanIpv4?.Invoke();
        Copies = 0;
        _transport.Advertise(CurrentAdvert());
        OpenResponder();

        // Asked for where hosting opens, and away from the frame. The router renews its own
        // leases until Close.
        Router.Open(Port);
    }

    private IReadOnlyList<string> SearchTargets() =>
        SearchAddress == BroadcastAddress && LanNetworks != null
            ? LanBroadcast.Targets(LanNetworks())
            : new[] { SearchAddress };

    // A second door on this machine finds the discovery port taken. It still hosts; it only
    // goes unanswered on the LAN, and a guest can still type its address.
    private void OpenResponder()
    {
        if (_lan == null)
        {
            return;
        }

        try
        {
            _responder = new LanResponder(_lan(BindAddress, LanDiscovery.Port), Version);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            _responder = null;
        }
    }

    // The player count is this machine's seats plus every guest on the wire. It moves as guests
    // arrive, and the step re-sends it.
    private SessionAdvertMessage CurrentAdvert()
    {
        int players = Math.Min(_localPlayers + Peers, byte.MaxValue);
        bool coop = _kind == NetSessionKind.CampaignCoop;
        // A Built-in host picks its map after the lobby opened, so its advert names none.
        byte seq = coop ? _missionSeq : _dogfight is { Shown: true } lobby ? lobby.Options.Environment : SessionAdvertMessage.NoMission;
        int cap = HostCap;
        var status = players >= cap
            ? NetSessionStatus.Full
            : _released || (coop && HostFlow.Screen == NetCoopScreen.InMission) ? NetSessionStatus.InMission : NetSessionStatus.Waiting;
        string name = GameName.Length > 0 ? GameName : _hostName;
        return new SessionAdvertMessage(_kind, seq, (byte)players, name, status, (byte)cap);
    }

    // A Dogfight host seats guests up to its chosen cap in arrival order, and refuses one past it
    // as a campaign host does. The lobby never lists a refused guest.
    private void RefuseOverCap()
    {
        int seated = _localPlayers;
        foreach (int peer in _transport!.AllPeers)
        {
            if (Refused(peer))
            {
                continue;
            }

            if (seated < HostCap)
            {
                seated++;
                continue;
            }

            _transport.Farewell(peer, NetCloseReason.Full);
            _refused.Add((peer, 0.0));
        }
    }

    // A campaign host seats guests in arrival order up to the cap. One past it is told the game is
    // full, and is hung up on if it has not left by the end of the grace.
    private void Admit()
    {
        var peers = _transport!.AllPeers;
        _admitted.RemoveAll(peer => !Contains(peers, peer));
        int seats = Math.Max(0, HostCap - _localPlayers);
        for (int i = 0; i < peers.Count; i++)
        {
            int peer = peers[i];
            if (_admitted.Contains(peer) || Refused(peer))
            {
                continue;
            }

            if (_admitted.Count < seats)
            {
                _admitted.Add(peer);
                continue;
            }

            _transport.Farewell(peer, NetCloseReason.Full);
            _refused.Add((peer, 0.0));
        }
    }

    // Either kind of door tells a guest of another version so. It is hung up on after the grace a
    // guest refused as full gets. The lobby has already left it off every peer list.
    private void RefuseClashing()
    {
        foreach (int peer in _transport!.Clashing)
        {
            if (!Refused(peer))
            {
                _transport.Farewell(peer, NetCloseReason.VersionMismatch);
                _refused.Add((peer, 0.0));
            }
        }
    }

    // The clashing peers are off AllPeers, so what is still connected is asked of the carrier.
    private void HangUpRefused(double dt)
    {
        var connected = _transport!.Inner.Peers;
        _refused.RemoveAll(refused => !Contains(connected, refused.Peer));
        for (int i = 0; i < _refused.Count; i++)
        {
            var (peer, waited) = _refused[i];
            if (double.IsPositiveInfinity(waited))
            {
                continue;
            }

            waited += dt;
            if (waited >= RefuseGraceSeconds)
            {
                // Kept on the list until the carrier reports it gone, so it is not seated meanwhile.
                waited = double.PositiveInfinity;
                _transport.Disconnect(peer);
            }

            _refused[i] = (peer, waited);
        }
    }

    private bool ReadyNow(int peer) =>
        _transport != null && _transport.Picks.TryGetValue(peer, out var pick) && pick.Epoch == _epoch && pick.Ready;

    // Zero is skipped, so a pick a guest never sent (epoch 0) is never counted as current.
    private void NextRound() => _epoch = (byte)(_epoch == byte.MaxValue ? 1 : _epoch + 1);

    private void SendFlows()
    {
        if (_transport != null)
        {
            HostFlow.Send(_transport, _admitted, _localPlayers, _epoch, ReadyNow);
        }
    }

    // At once rather than on the next step, so a film's end reaches a guest before the board after it.
    private void TellFilm(CoopFilmMessage film)
    {
        foreach (int peer in _admitted)
        {
            _transport!.Tell(peer, film);
        }
    }

    // The pick follows the host's round. What trails in from a flight that ended is never the next
    // one's opener.
    private void FollowHost()
    {
        if (_transport!.Flow is not { } flow)
        {
            return;
        }

        if (flow.Screen != NetCoopScreen.InMission || (_flightEpoch is { } flown && flow.Epoch != flown))
        {
            _flownFlow = false;
        }

        // What arrives under the flight this guest already flew is that flight's tail, never an
        // opener for the next one.
        if (flow.Screen != NetCoopScreen.InMission || _flownFlow)
        {
            _transport.DropHeld();
        }

        if (Pick.Follow(flow.Epoch, PlayerName, PilotVoices.Wire(Voice)) is { } pick && _hostPeer >= 0)
        {
            _transport.Tell(_hostPeer, pick);
            Pick.MarkSent(pick);
        }
    }

    private void ForgetCoop()
    {
        HostFlow.Forget();
        NextRound();
        Pick.Forget();
        _flownFlow = false;
    }

    private bool HostGone() =>
        _transport!.Closed != null || _link?.LinkState == EnetLinkState.Down
        || (_hostPeer >= 0 && !Contains(_transport.AllPeers, _hostPeer));

    private bool Refused(int peer)
    {
        foreach (var (refused, _) in _refused)
        {
            if (refused == peer)
            {
                return true;
            }
        }

        return false;
    }

    // ⚠ The notices go before the close, and the socket keeps being stepped afterwards. A carrier's
    // close discards what it has queued, so a notice sent and closed on at once never leaves.
    private void Linger(NetLobby lobby)
    {
        EndLinger();
        var peers = new List<int>(lobby.AllPeers);
        foreach (int peer in peers)
        {
            lobby.Farewell(peer, NetCloseReason.Closed);
        }

        lobby.Step(0.0);
        _closing = lobby;
        _lingered = 0.0;
    }

    private void StepLinger(double dt)
    {
        if (_closing == null)
        {
            return;
        }

        _closing.Step(dt);
        _lingered += dt;
        if (_lingered >= LingerSeconds)
        {
            EndLinger();
        }
    }

    private void EndLinger()
    {
        _closing?.Dispose();
        _closing = null;
    }

    // A wire a launch carried away is the session's to close, so a failure in flight only lets go
    // of it. The launcher disposes it once Reclaim says the door no longer holds it.
    private void Fail(string why)
    {
        if (!_released)
        {
            _transport?.Dispose();
        }

        _responder?.Dispose();
        _responder = null;
        _transport = null;
        _link = null;
        _released = false;
        _admitted.Clear();
        _refused.Clear();
        _dogfight = null;
        _flownEpoch = null;
        ForgetCoop();
        _hostPeer = -1;
        Fault = why;
        Stage = NetDoorStage.Failed;
        ForgetHostAddress();
    }

    private void ForgetHostAddress()
    {
        HostIpv6 = null;
        HostLanIpv4 = null;
        Copies = 0;
    }

    private DoorReading Read() => new(
        _transport, _transport?.Changes ?? 0, _transport?.Held ?? 0, Stage, Fault, Router.PortMap, Router.Pinhole, _search,
        _search?.Changes ?? 0, SearchFault, Link, _admitted.Count, _dogfight, Copies);

    // Everything a board draws from this door that can move without an input event. The lobby's
    // and the search's own counters stand for what arrived through them.
    private readonly record struct DoorReading(
        NetLobby? Wire, int WireChanges, int Held, NetDoorStage Stage, string Fault, UpnpPortMapResult? PortMap,
        UpnpPinholeResult? Pinhole, LanSearch? Search, int SearchChanges, string SearchFault, EnetLinkState? Link,
        int Admitted, DogfightLobby? Dogfight, int Copies);
}
