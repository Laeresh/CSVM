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

/// <summary>One seat a co-op host gave a guest: the guest's peer, the seat's player number, and
/// <see cref="Local"/>, its place among that machine's own seats. It says whether the seat is Ready
/// this round. It names the hangar plane the host settled for it, with that plane's airframe, fit
/// and build. It carries the seat's callsign and voice's place, -1 for none. The Left flag says the
/// guest walked out of the flight under way.</summary>
public readonly record struct CoopGuest(
    int Peer, int Slot, bool Ready, byte Airframe, CoopFit Fit = default, string Name = "", bool Left = false,
    int Plane = Session.Campaign.CoopPlanePool.Stock, NetPlaneBuild? Build = null, int Voice = -1, int Local = 0);

/// <summary>
/// The multiplayer door as a shared feature. It owns the port and the address a board edits, the
/// socket it opens, and the link readouts it shows. The wire a launch carries away comes from here
/// too. Nothing here names a carrier or an engine type: the two factories and the LAN socket arrive
/// as delegates, and the router as a <see cref="RouterAccess"/>. It composes the boxes' answers
/// (<see cref="Identity"/>), the master server link (<see cref="Internet"/>), the host's address
/// (<see cref="Reach"/>) and LAN discovery (<see cref="Lan"/>). Admission, a co-op host's boards
/// (<see cref="HostFlow"/>) and a co-op guest's pick (<see cref="Pick"/>) are its own types too.
/// </summary>
public sealed class NetPlayFeature : IMenuFeature
{
    /// <summary>The shipped game port, the one every build fills in for a bare address. A door
    /// opens on <see cref="NetPorts.Game"/>, which equals it unless a test process moved it.
    /// </summary>
    public const int DefaultPort = NetPorts.ShippedGame;

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

    private readonly Func<int, int, string, INetTransport> _openHost;
    private readonly Func<string, int, INetTransport> _openJoin;
    private readonly NetAdmission _admission = new();
    private readonly CoopGuestPick[] _picks = NewPicks();

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
    private int _hostPeer = -1;
    private INetLink? _link;
    private NetSessionKind _kind = NetSessionKind.Dogfight;
    private byte _missionSeq = SessionAdvertMessage.NoMission;
    private string _hostName = "";
    private int _localPlayers = 1;
    private int _localSeats = 1;
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
        Lan = new LanDoor(lan);
        Identity = new NetIdentity(() => _kind);
        Internet = new InternetDoor(() => IsHost);
        Reach = new HostReach(Router, Internet, () => Port, () => IsHost);
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

    /// <summary>The port a host opens on, and the port a join is aimed at. It starts on
    /// <see cref="NetPorts.Game"/> as the door is built.</summary>
    public int Port { get; private set; } = NetPorts.Game;

    /// <summary>The address a join is aimed at, as typed. It may name its own port, which beats
    /// <see cref="Port"/> for the join (<see cref="NetEndpoint.Parse"/>).</summary>
    public string Address { get; private set; } = DefaultAddress;

    /// <summary>The host and port a join opens on: <see cref="Address"/> split, with
    /// <see cref="Port"/> where the address names none. Its text is the address a player writes.
    /// </summary>
    public NetEndpoint JoinTarget => NetEndpoint.Parse(Address, Port);

    /// <summary>What a board names the join by: the join code for a join by code, else
    /// <see cref="JoinTarget"/> as a player writes it.</summary>
    public string JoinName =>
        Internet.GuestCode ?? (Internet.TypedCode(Address, out string code) ? code : JoinTarget.ToString());

    /// <summary>What a guest's status names its host by: the join code for a join by code, else
    /// <see cref="Address"/> as typed.</summary>
    public string LinkedTo => Internet.GuestCode ?? Address;

    /// <summary>The master server link: the internet games list, the join by code, and a host's
    /// listing with its code.</summary>
    public InternetDoor Internet { get; }

    /// <summary>What a host hands its guests to reach it: its address and the clipboard copy.
    /// </summary>
    public HostReach Reach { get; }

    /// <summary>The LAN search and a host's answers to other machines' searches.</summary>
    public LanDoor Lan { get; }

    /// <summary>What the Game and Player Information boxes answered: the callsign, the voice, the
    /// game's name and cap, the password and the listing.</summary>
    public NetIdentity Identity { get; }

    /// <summary>Why the last open failed, or "" when none has. Shown on the board rather than
    /// thrown: a taken port and a refused join are both things a player fixes and retries.</summary>
    public string Fault { get; private set; } = "";

    /// <summary>The host's router: the port mapping and the IPv6 pinhole it asks for as it opens,
    /// and gives back on <see cref="Close"/>.</summary>
    public RouterAccess Router { get; }

    /// <summary>What this co-op host names to its guests about its boards and its films.</summary>
    public CoopHostFlow HostFlow { get; } = new();

    /// <summary>This co-op guest's pick from its host's hangar for its first seat. It lasts the
    /// joined session across flights, and a new join starts on the starter.</summary>
    public CoopGuestPick Pick => _picks[0];

    /// <summary>How many seats this co-op guest's machine asks its host for, one per local player,
    /// 1 to <see cref="CoopHumans"/>. Each sends its own pick (<see cref="PickOf"/>).</summary>
    public int LocalSeats
    {
        get => _localSeats;
        set => _localSeats = Math.Clamp(value, 1, _picks.Length);
    }

    /// <summary>How many seats the host gave this co-op guest's machine. That is its first, then as
    /// many more as it asked (<see cref="LocalSeats"/>) and the cap had room for.</summary>
    public int CoopSeats => CoopFlow is { } flow ? Math.Min(_localSeats, 1 + flow.Extra) : 1;

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
                peers += _admission.Refused(peer) ? 0 : 1;
            }

            return peers;
        }
    }

    /// <summary>Whether this door can search the LAN or ask a master server for games.</summary>
    public bool CanSearch => Lan.CanSearch || Internet.Master != null;

    /// <summary>Whether a LAN search or a master server's list is open.</summary>
    public bool Searching => Lan.Searching || Internet.Master is { Asking: true };

    /// <summary>The open doors the LAN search heard, then the games the master server lists, empty
    /// while neither is open.</summary>
    public IReadOnlyList<LanGame> Games
    {
        get
        {
            var lan = Lan.Games;
            if (Internet.Master is not { Asking: true, Games.Count: > 0 } master)
            {
                return lan;
            }

            var games = new List<LanGame>(lan);
            games.AddRange(master.Games);
            return games;
        }
    }

    /// <summary>The link as the carrier reports it, or null for a carrier with no word for it.
    /// </summary>
    public NetLinkState? Link => _link?.LinkState;

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
    public bool IsCoopGuest =>
        Stage == NetDoorStage.Joined && Advert is { Kind: NetSessionKind.CampaignCoop } && !AwaitingAdmission;

    /// <summary>Whether this door is a guest linked to a host holding a Dogfight open.</summary>
    public bool IsDogfightGuest =>
        Stage == NetDoorStage.Joined && Advert is { Kind: NetSessionKind.Dogfight } && !AwaitingAdmission;

    /// <summary>Whether this guest is linked to a host whose advert asks a password and has not
    /// admitted it yet. It follows none of the host's boards meanwhile.</summary>
    public bool AwaitingAdmission =>
        Stage == NetDoorStage.Joined && Advert is { Password: true } && _transport is { Admitted: false };

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

    /// <summary>Whether every seat this co-op host gave a guest is Ready under the current round,
    /// each of a machine's seats on its own. True with no guest seated, since a host may fly its
    /// campaign alone.</summary>
    public bool CoopAllReady
    {
        get
        {
            foreach (int peer in _admission.Admitted)
            {
                for (int local = 0; local < _admission.GrantedTo(peer); local++)
                {
                    if (!ReadyNow(peer, local))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>Every seat this co-op host gave its guests in player order, a guest's own side by
    /// side. Each names its player number, its Ready mark under the current round, and the plane it
    /// flies. A seat flies its own fit on the plane it picked, and the plane's stored fit on one the
    /// host's settling gave it instead.</summary>
    public IReadOnlyList<CoopGuest> CoopGuests
    {
        get
        {
            var guests = new List<CoopGuest>(_admission.Admitted.Count);
            int slot = _localPlayers;
            foreach (int peer in _admission.Admitted)
            {
                for (int local = 0; local < _admission.GrantedTo(peer); local++, slot++)
                {
                    var pick = _transport!.PickAt(peer, local) ?? default;
                    int plane = HostFlow.PlaneOf(slot);
                    var word = HostFlow.HangarAt(plane);
                    byte airframe = word?.Airframe ?? CoopGuestPick.StarterAirframe;
                    var fit = word is { } flown && pick.PlaneIndex != plane ? flown.Fit : pick.Fit;
                    guests.Add(new CoopGuest(peer, slot, ReadyNow(peer, local), airframe, fit, pick.Name ?? "",
                        pick.Left && pick.Epoch == _epoch, plane, word?.Build, PilotVoices.FromWire(pick.Voice), local));
                }
            }

            return guests;
        }
    }

    /// <summary>The plane of the hangar each seat this co-op host gave its guests picked, in player
    /// order, as <see cref="CoopGuestPick.Plane"/> names one. What the host settles every seat's
    /// pick from.</summary>
    public IReadOnlyList<int> CoopGuestPlanes
    {
        get
        {
            var planes = new List<int>(_admission.Admitted.Count);
            foreach (int peer in _admission.Admitted)
            {
                for (int local = 0; local < _admission.GrantedTo(peer); local++)
                {
                    planes.Add(_transport!.PickAt(peer, local) is { } pick
                        ? pick.PlaneIndex
                        : Session.Campaign.CoopPlanePool.Unpicked);
                }
            }

            return planes;
        }
    }

    /// <summary>The line this co-op guest's band shows when the host gave its machine fewer seats
    /// than its players, or "" when every player has one.</summary>
    public string CoopSeatsShort => IsCoopGuest && CoopSeats < _localSeats ? CoopDoorText.SeatsShort(CoopSeats, _localSeats) : "";

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

    /// <summary>Whether this co-op guest's first seat is Ready under the host's current round.</summary>
    public bool CoopReady => CoopReadyAt(0);

    /// <summary>Whether every seat the host gave this co-op guest's machine is Ready under the host's
    /// current round.</summary>
    public bool CoopSeatsReady
    {
        get
        {
            for (int local = 0; local < CoopSeats; local++)
            {
                if (!CoopReadyAt(local))
                {
                    return false;
                }
            }

            return CoopFlow != null;
        }
    }

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
    private int HostCap =>
        Identity.MaxPlayers > 0 ? NetPlayerInfo.ClampPlayers(_kind, Identity.MaxPlayers) : NetPlayerInfo.PlayerCap(_kind);

    /// <summary>The callsign of co-op player <paramref name="slot"/>, counted from 0 in player order,
    /// or "" for a seat with none, which goes by its player tag. A seat's first player at its machine
    /// goes by <see cref="NetIdentity.PlayerName"/> there. A host reads each guest's from its pick,
    /// and a guest reads every other seat's from the host's player list.</summary>
    public string CoopSeatName(int slot)
    {
        if (IsCoopHost)
        {
            if (slot < _localPlayers)
            {
                return slot == 0 ? Identity.PlayerName : "";
            }

            foreach (var guest in CoopGuests)
            {
                if (guest.Slot == slot)
                {
                    return guest.Name;
                }
            }

            return "";
        }

        if (CoopFlow is not { } flow)
        {
            return "";
        }

        int local = slot - flow.Slot;
        if (local >= 0 && local < CoopSeats)
        {
            return local == 0 ? Identity.PlayerName : "";
        }

        var rows = _transport?.DogfightRoster?.Rows;
        return rows != null && slot >= 0 && slot < rows.Count ? rows[slot].Name : "";
    }

    /// <summary>This co-op guest's pick for its seat <paramref name="local"/>, counted from 0 among
    /// its machine's own. Seat 0's is <see cref="Pick"/>.</summary>
    public CoopGuestPick PickOf(int local) => _picks[Math.Clamp(local, 0, _picks.Length - 1)];

    /// <summary>Whether this co-op guest's seat <paramref name="local"/> is Ready under the host's
    /// current round. False for a seat the host did not give it.</summary>
    public bool CoopReadyAt(int local) =>
        CoopFlow is { } flow && local >= 0 && local < CoopSeats && PickOf(local).ReadyUnder(flow.Epoch);

    /// <summary>Removes the connected guest at <paramref name="peer"/> from this host's session, as
    /// the original's Boot does. The guest is told why and hung up on. Its address is banned until
    /// the session closes, and a Dogfight lobby posts the original's notice. False on a door that is
    /// not hosting and for a peer that is not a seated guest.</summary>
    public bool Boot(int peer)
    {
        if (!IsHost || _transport == null)
        {
            return false;
        }

        string name = _transport.Picks.TryGetValue(peer, out var pick) && pick.Name is { Length: > 0 } named ? named : "";
        if (!_admission.Boot(_transport, peer))
        {
            return false;
        }

        _dogfight?.Announce(CoopDoorText.BootedLine(name));
        return true;
    }

    /// <summary>The voice a guest's latest pick carried to this host, as its place in
    /// <see cref="PilotVoices.All"/>, or -1 when the guest sent none.</summary>
    public int PickedVoice(int peer) =>
        _transport != null && _transport.Picks.TryGetValue(peer, out var pick) ? PilotVoices.FromWire(pick.Voice) : -1;

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
        foreach (var pick in _picks)
        {
            pick.Leave();
        }

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

        foreach (int peer in IsCoopHost ? _admission.Admitted : _transport.AllPeers)
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

        foreach (int peer in IsCoopHost ? _admission.Admitted : _transport.AllPeers)
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

        foreach (int peer in _admission.Admitted)
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
            _dogfight = new DogfightLobby(_transport, () => Identity.PlayerName) { Seated = peer => !_admission.Refused(peer) };
        }
    }

    /// <summary>Opens a listen server with the Multiplayer Lobby standing on it, the Connection
    /// page's Host. It is <see cref="OpenHost(int)"/> with the lobby shown, whose environment the
    /// advert names. The host's pilot goes by <see cref="NetIdentity.PlayerName"/>.</summary>
    public void OpenDogfightHost(int maxGuests)
    {
        OpenHost(maxGuests);
        if (_transport != null && _dogfight != null)
        {
            _hostName = Identity.PlayerName;
            _dogfight.Show();
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
    public void OpenJoin() => OpenJoin(Internet.TypedCode(Address, out string code) ? code : null);

    /// <summary>Starts a join to the game the master server lists under <paramref name="typed"/>,
    /// read as <see cref="MasterWire.TryCode"/> reads it, and leaves <see cref="Address"/> as typed.
    /// False, with nothing opened, for text that is not a code or a door with no code opener.
    /// </summary>
    public bool JoinByCode(string typed)
    {
        if (!Internet.TryCode(typed, out string code))
        {
            return false;
        }

        OpenJoin(code);
        return true;
    }

    /// <summary>Whether a game the LAN search heard runs a build this one plays with.</summary>
    public bool PlaysWith(LanGame game) => Version.PlaysWith(game.Version);

    /// <summary>Joins a game the LAN search heard, at the address it answered from and the game
    /// port it named. A game of a version this build does not play with is refused before any
    /// socket opens, with both versions on <see cref="Fault"/>. The search stays open; the page
    /// that ran it closes it.</summary>
    public void JoinGame(LanGame game)
    {
        bool byCode = game.Code != null && Internet.OpenCode != null;
        if (_transport != null || string.IsNullOrWhiteSpace(game.Address) || (!byCode && game.Port is < 1 or > 65535))
        {
            return;
        }

        if (!PlaysWith(game))
        {
            Fail(CoopDoorText.VersionMismatch(game.Version, Version));
            return;
        }

        Address = game.Code ?? game.Address;
        Port = byCode ? Port : game.Port;
        OpenJoin();
    }

    /// <summary>Asks the LAN for open doors, opening the search on its first call. Each call is a
    /// new round, and a game that answers neither this round nor the last leaves
    /// <see cref="Games"/>. The answers land on later steps.</summary>
    public void Search()
    {
        Internet.Master?.Ask();
        Lan.Ask(BindAddress);
    }

    /// <summary>Closes the LAN search and the master server's list, and forgets what both heard.
    /// </summary>
    public void StopSearch()
    {
        Lan.StopSearch();
        Internet.Master?.Forget();
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

        _admission.DisconnectRefused(_transport);
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
        _transport.LocalCallsign = Identity.PlayerName;
        _transport.LocalVoice = PilotVoices.Wire(Identity.Voice);
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
        // An open session takes its password and listing with it. A shut or failed door keeps them,
        // since the boards close one between the box's OK and the open it confirmed.
        if (Stage is NetDoorStage.Hosting or NetDoorStage.Joining or NetDoorStage.Joined)
        {
            Identity.ForgetAnswers();
        }

        Lan.StopAnswering();
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
        Internet.Shut();
        _released = false;
        _admission.Clear();
        _dogfight = null;
        _flownEpoch = null;
        ForgetCoop();
        _hostPeer = -1;
        _kind = NetSessionKind.Dogfight;
        Offer(SessionAdvertMessage.NoMission, "", 1);
        Stage = NetDoorStage.Shut;
        Reach.Forget();
        Router.Close();
    }

    /// <summary>Drops everything transient: the socket goes with the presentation that opened it,
    /// since no board is left to show what it is doing. A closing socket and a search go too.
    /// </summary>
    public void Discard()
    {
        Close();
        Identity.ForgetAnswers();
        EndLinger();
        StopSearch();
        Fault = "";
        Lan.ClearFault();
    }

    private static CoopGuestPick[] NewPicks()
    {
        var picks = new CoopGuestPick[CoopHumans];
        for (int i = 0; i < picks.Length; i++)
        {
            picks[i] = new CoopGuestPick();
        }

        return picks;
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
        Lan.Poll();
        Internet.Master?.Poll(dt);
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

            // A host's advert reaches its connected peers too. A player who joins mid-match then
            // reads In mission, not the lobby's last word.
            var flying = CurrentAdvert();
            if (IsHost)
            {
                _transport.Advertise(flying);
            }

            Lan.AnswerWith(flying, Port);
            Internet.List(flying, Version, Identity.Private);
            return;
        }

        if (!_released)
        {
            _transport.Step(dt);
        }

        Router.Poll();
        if (Stage == NetDoorStage.Hosting)
        {
            _admission.RefuseClashing(_transport);
            _admission.RefuseTurnedAway(_transport);
            if (_kind == NetSessionKind.CampaignCoop)
            {
                _admission.Admit(_transport, _localPlayers, HostCap);
                SendFlows();
            }
            else
            {
                _admission.RefuseOverCap(_transport, _localPlayers, HostCap);
            }

            _admission.HangUpRefused(_transport, dt);
            _dogfight?.Step();
            var advert = CurrentAdvert();
            _transport.Advertise(advert);
            Lan.AnswerWith(advert, Port);
            Internet.List(advert, Version, Identity.Private);
            return;
        }

        // A host that says why it is sending this guest away is believed before its link drops.
        if (_transport.Closed is { } closed)
        {
            Fail(closed.Reason switch
            {
                NetCloseReason.Full => CoopDoorText.GameFull,
                NetCloseReason.VersionMismatch => CoopDoorText.VersionMismatch(closed.Host, closed.Guest),
                NetCloseReason.Booted => CoopDoorText.Booted,
                NetCloseReason.WrongPassword => CoopDoorText.WrongPassword,
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
            if (_link?.LinkState == NetLinkState.Down || hostGone)
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
                _dogfight ??= new DogfightLobby(_transport, () => Identity.PlayerName, _hostPeer)
                {
                    Voice = () => PilotVoices.Wire(Identity.Voice),
                };
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
        double timeout = Internet.GuestCode != null ? InternetDoor.CodeJoinTimeoutSeconds : JoinTimeoutSeconds;
        if (_link?.LinkState == NetLinkState.Up || (_link == null && _transport.AllPeers.Count > 0))
        {
            Stage = NetDoorStage.Joined;
            _hostPeer = _transport.AllPeers.Count > 0 ? _transport.AllPeers[0] : -1;
        }
        else if (_link?.LinkState == NetLinkState.Down)
        {
            Fail(_link.LinkFault is { Length: > 0 } why ? $"{JoinName}: {why}" : $"{JoinName} refused the join");
        }
        else if (_joining >= timeout)
        {
            Fail($"{JoinName} did not answer in {timeout:0} seconds");
        }
    }

    // A join by code when one is given, else to the typed address.
    private void OpenJoin(string? code)
    {
        if (_transport != null)
        {
            return;
        }

        EndLinger();
        try
        {
            var (host, port) = JoinTarget;
            var carrier = code != null ? Internet.Join(code) : _openJoin(host, port);
            _transport = new NetLobby(carrier, Version, joinPassword: Identity.Password);
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
            _transport = new NetLobby(_openHost(Port, maxGuests, BindAddress), Version, Identity.Password);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            Fail(e.Message);
            return;
        }

        _link = _transport.Inner as INetLink;
        Internet.Host(_transport.Inner);
        _kind = kind;
        Fault = "";
        Stage = NetDoorStage.Hosting;
        Reach.Open();
        _transport.Advertise(CurrentAdvert());
        Lan.Answer(BindAddress, Version);

        // Asked for where hosting opens, and away from the frame. The router renews its own
        // leases until Close.
        Router.Open(Port);
    }

    // The player count is this machine's seats plus every guest on the wire, and in co-op every
    // further seat a guest was given. It moves as guests arrive, and the step re-sends it.
    private SessionAdvertMessage CurrentAdvert()
    {
        bool coop = _kind == NetSessionKind.CampaignCoop;
        int extra = 0;
        foreach (int peer in coop ? _admission.Admitted : Array.Empty<int>())
        {
            extra += _admission.GrantedTo(peer) - 1;
        }

        int players = Math.Min(_localPlayers + Peers + extra, byte.MaxValue);
        // A Built-in host picks its map after the lobby opened, so its advert names none.
        byte seq = coop ? _missionSeq : _dogfight is { Shown: true } lobby ? lobby.Options.Environment : SessionAdvertMessage.NoMission;
        int cap = HostCap;
        var status = players >= cap
            ? NetSessionStatus.Full
            : _released || (coop && HostFlow.Screen == NetCoopScreen.InMission) ? NetSessionStatus.InMission : NetSessionStatus.Waiting;
        string name = Identity.GameName.Length > 0 ? Identity.GameName : _hostName;
        return new SessionAdvertMessage(_kind, seq, (byte)players, name, status, (byte)cap, _transport?.AsksPassword ?? false);
    }

    private bool ReadyNow(int peer, int local) =>
        _transport != null && _transport.PickAt(peer, local) is { } pick && pick.Epoch == _epoch && pick.Ready;

    // Zero is skipped, so a pick a guest never sent (epoch 0) is never counted as current.
    private void NextRound() => _epoch = (byte)(_epoch == byte.MaxValue ? 1 : _epoch + 1);

    private void SendFlows()
    {
        if (_transport == null)
        {
            return;
        }

        var seated = new List<(int Peer, int Seats)>(_admission.Admitted.Count);
        int humans = _localPlayers;
        foreach (int peer in _admission.Admitted)
        {
            seated.Add((peer, _admission.GrantedTo(peer)));
            humans += _admission.GrantedTo(peer);
        }

        var names = new string[humans];
        for (int slot = 0; slot < humans; slot++)
        {
            names[slot] = CoopSeatName(slot);
        }

        HostFlow.Send(_transport, seated, _localPlayers, _epoch, ReadyNow, names);
    }

    // At once rather than on the next step, so a film's end reaches a guest before the board after it.
    private void TellFilm(CoopFilmMessage film)
    {
        foreach (int peer in _admission.Admitted)
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

        // One pick per local player. Only the first has a Player Information answer, so it alone
        // carries a name and voice; the rest go by their player numbers.
        for (int local = 0; local < _localSeats; local++)
        {
            var seat = _picks[local];
            string name = local == 0 ? Identity.PlayerName : "";
            byte voice = local == 0 ? PilotVoices.Wire(Identity.Voice) : CoopPickMessage.NoVoice;
            if (seat.Follow(flow.Epoch, name, voice, local, local + 1 < _localSeats) is { } pick && _hostPeer >= 0)
            {
                _transport.Tell(_hostPeer, pick);
                seat.MarkSent(pick);
            }
        }
    }

    private void ForgetCoop()
    {
        HostFlow.Forget();
        NextRound();
        foreach (var pick in _picks)
        {
            pick.Forget();
        }

        _flownFlow = false;
    }

    private bool HostGone() =>
        _transport!.Closed != null || _link?.LinkState == NetLinkState.Down
        || (_hostPeer >= 0 && !Contains(_transport.AllPeers, _hostPeer));

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

        Lan.StopAnswering();
        _transport = null;
        _link = null;
        Internet.Shut();
        _released = false;
        _admission.Clear();
        _dogfight = null;
        _flownEpoch = null;
        ForgetCoop();
        _hostPeer = -1;
        Fault = why;
        Stage = NetDoorStage.Failed;
        Reach.Forget();
        Identity.ForgetAnswers();
    }

    private DoorReading Read() => new(
        _transport, _transport?.Changes ?? 0, _transport?.Held ?? 0, Stage, Fault, Router.PortMap, Router.Pinhole, Lan.Search,
        Lan.Search?.Changes ?? 0, Lan.SearchFault, Link, _admission.Admitted.Count, _dogfight, Reach.Copies,
        Internet.Master?.Answers ?? 0, Internet.Master?.Fault ?? "", Internet.JoinCode, Internet.ListingFault);

    // Everything a board draws from this door that can move without an input event. The lobby's
    // and the search's own counters stand for what arrived through them.
    private readonly record struct DoorReading(
        NetLobby? Wire, int WireChanges, int Held, NetDoorStage Stage, string Fault, UpnpPortMapResult? PortMap,
        UpnpPinholeResult? Pinhole, LanSearch? Search, int SearchChanges, string SearchFault, NetLinkState? Link,
        int Admitted, DogfightLobby? Dogfight, int Copies, int MasterAnswers, string MasterFault, string? JoinCode,
        string ListingFault);
}
