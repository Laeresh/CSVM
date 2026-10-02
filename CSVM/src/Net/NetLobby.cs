using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>
/// A carrier's first listener, standing between a socket and the session that later binds it.
/// A carrier binds once and the menu reads the advert before any session exists, so this binds it
/// at once as the session's transport. Its <see cref="Advertise"/> reaches every peer on connect and on
/// each change, and a lobby message that arrives is kept here and never passed on. Any other payload
/// is held until a listener binds, then replayed behind the roster announcement. A bound session
/// sees only the peers present when it bound, and <see cref="Unbind"/> frees the carrier for the
/// next. A clashing build, a banned address or a wrong password leaves every peer list, and only
/// its close notice is read.
/// </summary>
public sealed class NetLobby : INetTransport, INetTransportListener, IDisposable
{
    /// <summary>How many payloads are held for a session that has not bound yet. The socket's own
    /// depth, for the same reason: deep enough for the join answer and the openers behind it.
    /// </summary>
    public const int HeldPayloads = 64;

    /// <summary>How long a host with a password waits for a peer's answer before turning it away.
    /// A build a patch older never answers, so it is refused rather than admitted.</summary>
    public const double PasswordWaitSeconds = 10.0;

    private readonly INetTransport _inner;
    private readonly NetBuildVersion _version;
    private readonly string _hostPassword;
    private readonly string? _joinPassword;
    private readonly Dictionary<int, NetBuildVersion> _heard = new();
    private readonly List<int> _clashing = new();
    private readonly Dictionary<int, double> _waiting = new();
    private readonly List<(int Peer, NetCloseReason Why)> _turnedAway = new();
    private readonly HashSet<string> _banned = new(StringComparer.Ordinal);
    private readonly HashSet<int> _answered = new();
    private readonly List<(int Peer, int Channel, byte[] Bytes)> _held = new();
    private readonly List<int> _bound = new();
    private readonly Dictionary<int, CoopPickMessage> _picks = new();
    private readonly Dictionary<int, CoopPickMessage?[]> _seatPicks = new();
    private readonly Dictionary<int, CoopFit> _seatFits = new();
    private readonly Dictionary<int, NetPlaneBuild> _pickBuilds = new();
    private readonly Dictionary<int, NetPlaneBuild?> _seatBuilds = new();
    private readonly List<int> _unpicked = new();
    private readonly List<CoopHangarMessage?> _hangar = new();

    private readonly List<(int Peer, LobbyChatMessage Line)> _chat = new();
    private readonly List<(int Peer, LobbyTeamActionMessage Action)> _teamActions = new();

    // Wide enough for the widest lobby message, the Dogfight team list.
    private readonly byte[] _scratch = new byte[Math.Max(DogfightRosterMessage.Size, LobbyTeamsMessage.Size)];
    private INetTransportListener? _listener;
    private SessionAdvertMessage? _advertising;

    // The co-op flow a guest's session bound under, and whether the host has since opened another
    // flight. ⚠ Past that point nothing reaches or leaves the bound session. The host's next
    // opener is held for the next bind, and the old flight says nothing into the new one.
    private CoopFlowMessage? _boundFlow;
    private bool _flightOver;

    /// <summary>A lobby over <paramref name="inner"/>, which it binds at once. It names this build
    /// to every peer as <paramref name="version"/>. A host's <paramref name="hostPassword"/>, when
    /// not empty, is asked of every peer before it is admitted. A guest's
    /// <paramref name="joinPassword"/> answers a host whose advert asks one; null answers nothing.
    /// </summary>
    public NetLobby(INetTransport inner, NetBuildVersion version = default, string hostPassword = "", string? joinPassword = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _version = version;
        _hostPassword = hostPassword ?? "";
        _joinPassword = joinPassword;
        _inner.Bind(this);
    }

    /// <summary>The carrier under this lobby, for the link readout a real socket offers.</summary>
    public INetTransport Inner => _inner;

    /// <summary>The build version this end names to its peers.</summary>
    public NetBuildVersion Version => _version;

    /// <summary>The connected peers whose named version does not play with this one, in the order
    /// they named it. None of them is on any other peer list.</summary>
    public IReadOnlyList<int> Clashing => _clashing;

    /// <summary>Whether this host asks a password of every peer before admitting it.</summary>
    public bool AsksPassword => _hostPassword.Length > 0;

    /// <summary>The connected peers this host has not admitted yet because their password has not
    /// arrived. None of them is on any other peer list.</summary>
    public IReadOnlyCollection<int> AwaitingPassword => _waiting.Keys;

    /// <summary>The connected peers this host turned away, in order, each with the reason its close
    /// notice gives: a banned address or a wrong password. None of them is on any other peer list.
    /// </summary>
    public IReadOnlyList<(int Peer, NetCloseReason Why)> TurnedAway => _turnedAway;

    /// <summary>How many addresses a boot banned from this session.</summary>
    public int Banned => _banned.Count;

    /// <summary>Whether this guest's host has admitted it after its password. False on a host, and
    /// on a guest of a session that asks none.</summary>
    public bool Admitted { get; private set; }

    /// <summary>The last advert a peer sent here, or null while none has arrived.</summary>
    public SessionAdvertMessage? Advert { get; private set; }

    /// <summary>The close notice a host sent here, or null while none has arrived. Kept here like the
    /// advert, so the notice reaches a guest board whether or not a session has bound.</summary>
    public SessionClosedMessage? Closed { get; private set; }

    /// <summary>The co-op host's latest word about its boards, or null while none has arrived.
    /// </summary>
    public CoopFlowMessage? Flow { get; private set; }

    /// <summary>How many co-op flows have arrived, so a board can tell a repeat from news.</summary>
    public int Flows { get; private set; }

    /// <summary>Each connected guest's latest co-op pick for its first seat, by peer.</summary>
    public IReadOnlyDictionary<int, CoopPickMessage> Picks => _picks;

    /// <summary>The co-op host's hangar as its latest words name it, in hangar order. Empty until
    /// every plane of it has arrived.</summary>
    public IReadOnlyList<CoopHangarMessage> Hangar
    {
        get
        {
            var planes = new List<CoopHangarMessage>(_hangar.Count);
            foreach (var plane in _hangar)
            {
                if (plane is not { } word)
                {
                    return Array.Empty<CoopHangarMessage>();
                }

                planes.Add(word);
            }

            return planes;
        }
    }

    /// <summary>Each seat's fit as the co-op host last launched it, by seat. A launch names every
    /// seat again, so an entry from an earlier flight is always overwritten before it is read.
    /// </summary>
    public IReadOnlyDictionary<int, CoopFit> SeatFits => _seatFits;

    /// <summary>Each connected guest's custom plane, by peer. A guest on a stock pick has none.
    /// </summary>
    public IReadOnlyDictionary<int, NetPlaneBuild> PickBuilds => _pickBuilds;

    /// <summary>Each seat's custom plane as the host last launched it, by seat, null for a stock
    /// seat. A launch names every seat again, as it does its fit.</summary>
    public IReadOnlyDictionary<int, NetPlaneBuild?> SeatBuilds => _seatBuilds;

    /// <summary>The Dogfight host's latest plane rules, or null while none has arrived.</summary>
    public LobbyPlaneRulesMessage? PlaneRules { get; private set; }

    /// <summary>The campaign wingman's aeroplane as the co-op host last launched it, or null while
    /// no host has named one. Every launch names it again before its opener.</summary>
    public CoopWingmanMessage? Wingman { get; private set; }

    /// <summary>The co-op host's latest film word, a start or an end, or null while none has
    /// arrived.</summary>
    public CoopFilmMessage? Film { get; private set; }

    /// <summary>The Dogfight host's latest Mission Options, or null while none has arrived.</summary>
    public DogfightOptionsMessage? DogfightOptions { get; private set; }

    /// <summary>The Dogfight host's latest player list, or null while none has arrived.</summary>
    public DogfightRosterMessage? DogfightRoster { get; private set; }

    /// <summary>The Dogfight host's latest team list, or null while none has arrived.</summary>
    public LobbyTeamsMessage? Teams { get; private set; }

    /// <summary>The advert this end hands out, or null while it hands out none.</summary>
    public SessionAdvertMessage? Advertising => _advertising;

    /// <summary>The callsign this machine's player flies under, which a host's roster names its own
    /// first seat by. Empty for a player with none, whose seat then takes its player tag. It is
    /// not the advert's name, which is the game's.</summary>
    public string LocalCallsign { get; set; } = "";

    /// <summary>The pilot voice this machine's player chose, in the pick's form, 0 for none. A
    /// Dogfight host's own first seat speaks in it.</summary>
    public byte LocalVoice { get; set; }

    /// <summary>Payloads waiting for a listener. A guest's first held payload is the host's join
    /// answer, which is how a guest board learns that the host has launched.</summary>
    public int Held => _held.Count;

    /// <summary>How many times a peer came or went, or a payload was kept here. A bound session's
    /// own traffic and a dropped payload do not count, so a board repaints on news alone.</summary>
    public int Changes { get; private set; }

    /// <summary>Whether a session has bound this lobby.</summary>
    public bool Bound => _listener != null;

    /// <summary>Whether the co-op host opened another flight while a session was bound here. That
    /// session then hears nothing more, and what arrives is held for the next bind. The mark stands
    /// until that bind, however often the carrier is unbound meanwhile.</summary>
    public bool FlightOver => _flightOver;

    /// <inheritdoc/>
    public int LocalPeer => _inner.LocalPeer;

    /// <summary>The peers a bound session flies with: those present when it bound and still
    /// connected. Every connected peer while nothing is bound. A peer arriving mid-flight waits
    /// here with the advert, since no guest joins a mission in flight.</summary>
    public IReadOnlyList<int> Peers => _listener != null ? _bound : AllPeers;

    /// <summary>Every connected peer, a bound session's or not: the count a host's advert reads. A
    /// peer on <see cref="Clashing"/>, <see cref="AwaitingPassword"/> or <see cref="TurnedAway"/>
    /// is left out.</summary>
    public IReadOnlyList<int> AllPeers
    {
        get
        {
            var peers = _inner.Peers;
            if (_clashing.Count == 0 && _waiting.Count == 0 && _turnedAway.Count == 0)
            {
                return peers;
            }

            var playing = new List<int>(peers.Count);
            for (int i = 0; i < peers.Count; i++)
            {
                if (!Outside(peers[i]))
                {
                    playing.Add(peers[i]);
                }
            }

            return playing;
        }
    }

    /// <summary>The version <paramref name="peer"/> named on connect. False while it has named
    /// none.</summary>
    public bool TryVersionOf(int peer, out NetBuildVersion version) => _heard.TryGetValue(peer, out version);

    /// <summary>How many seats <paramref name="peer"/>'s latest picks ask for: its first, and one
    /// more for each pick that says another follows. A peer that has picked nothing asks one.</summary>
    public int SeatsWanted(int peer)
    {
        if (!_seatPicks.TryGetValue(peer, out var picks))
        {
            return 1;
        }

        int seats = 1;
        while (seats <= CoopPickMessage.MaxLocal && picks[seats - 1] is { More: true })
        {
            seats++;
        }

        return seats;
    }

    /// <summary>The latest pick <paramref name="peer"/> sent for its seat <paramref name="local"/>,
    /// counted from 0 among its own, or null while none has arrived.</summary>
    public CoopPickMessage? PickAt(int peer, int local) =>
        _seatPicks.TryGetValue(peer, out var picks) && local >= 0 && local < picks.Length ? picks[local] : null;

    /// <summary>Hands <paramref name="advert"/> to every peer now and to every peer that connects
    /// later. Sent only when it differs from the last one, so a board may call this every frame.
    /// </summary>
    public void Advertise(SessionAdvertMessage advert)
    {
        if (_advertising == advert)
        {
            return;
        }

        _advertising = advert;
        var peers = _inner.Peers;
        for (int i = 0; i < peers.Count; i++)
        {
            SendAdvert(peers[i]);
        }
    }

    /// <inheritdoc/>
    public void Bind(INetTransportListener listener)
    {
        if (_listener != null)
        {
            throw new InvalidOperationException("a lobby hands its carrier to one session at a time");
        }

        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _boundFlow = Flow;
        _flightOver = false;
        _bound.Clear();
        _bound.AddRange(AllPeers);
        foreach (int peer in new List<int>(_bound))
        {
            listener.OnPeerConnected(peer);
        }

        var held = _held.ToArray();
        _held.Clear();
        foreach (var (peer, channel, bytes) in held)
        {
            listener.OnPayload(peer, channel, bytes);
        }
    }

    /// <summary>Takes the carrier back from the session that bound it, so the next flight can bind
    /// it again. What was held for the old session is dropped with it. What arrived after the host
    /// opened another flight is kept, since it is that flight's opener.</summary>
    public void Unbind()
    {
        _listener = null;
        _bound.Clear();
        if (!_flightOver)
        {
            _held.Clear();
        }

        // ⚠ The mark outlives this unbind, since a freed session unbinds and its door unbinds again.
        // Were it cleared here, that second unbind would drop the opener. The next bind clears it.
        _boundFlow = null;
    }

    /// <summary>Unbinds only when <paramref name="listener"/> is the session bound here. A session
    /// being freed calls this, so a carrier a door already took back is left alone.</summary>
    public void Release(INetTransportListener listener)
    {
        if (_listener != null && ReferenceEquals(_listener, listener))
        {
            Unbind();
        }
    }

    /// <summary>Hands over every chat line that arrived since the last call, with the peer that
    /// sent it, and forgets them.</summary>
    public IReadOnlyList<(int Peer, LobbyChatMessage Line)> TakeChat()
    {
        if (_chat.Count == 0)
        {
            return Array.Empty<(int, LobbyChatMessage)>();
        }

        var lines = _chat.ToArray();
        _chat.Clear();
        return lines;
    }

    /// <summary>Hands over every team action a guest asked since the last call, with the peer that
    /// asked it, and forgets them. Only a host acts on them.</summary>
    public IReadOnlyList<(int Peer, LobbyTeamActionMessage Action)> TakeTeamActions()
    {
        if (_teamActions.Count == 0)
        {
            return Array.Empty<(int, LobbyTeamActionMessage)>();
        }

        var actions = _teamActions.ToArray();
        _teamActions.Clear();
        return actions;
    }

    /// <summary>Drops every held payload. A guest does this whenever the host names a board. What
    /// trails in from a flight that ended is not the next flight's join answer.</summary>
    public void DropHeld() => _held.Clear();

    /// <inheritdoc/>
    public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0)
    {
        if (!_flightOver)
        {
            _inner.Send(peer, payload, reliability, channel);
        }
    }

    /// <summary>Sends one lobby message to <paramref name="peer"/>, outside any session.</summary>
    /// <typeparam name="T">The message being sent.</typeparam>
    public void Tell<T>(int peer, in T message)
        where T : struct, INetMessage<T>
    {
        int length = message.Write(_scratch);
        _inner.Send(peer, _scratch.AsSpan(0, length), T.Reliability);
    }

    /// <inheritdoc/>
    public void Disconnect(int peer) => _inner.Disconnect(peer);

    /// <inheritdoc/>
    public void Step(double dt)
    {
        _inner.Step(dt);
        foreach (int peer in new List<int>(_waiting.Keys))
        {
            double waited = _waiting[peer] + dt;
            _waiting[peer] = waited;
            if (waited >= PasswordWaitSeconds)
            {
                TurnAway(peer, NetCloseReason.WrongPassword);
            }
        }
    }

    /// <summary>The address <paramref name="peer"/> reached this end from, or null when the carrier
    /// does not name one.</summary>
    public string? AddressOf(int peer) => (_inner as INetPeerAddress)?.AddressOf(peer);

    /// <summary>Turns <paramref name="peer"/> away as booted and bans the address it connected
    /// from, so a later connection from there is turned away too until this lobby closes. False
    /// when the carrier names no address, and the peer's return then cannot be refused.</summary>
    public bool Boot(int peer)
    {
        string? address = AddressOf(peer);
        if (address != null)
        {
            _banned.Add(address);
        }

        TurnAway(peer, NetCloseReason.Booted);
        return address != null;
    }

    /// <summary>Whether a connection from <paramref name="address"/> is turned away.</summary>
    public bool IsBanned(string address) => _banned.Contains(address ?? "");

    /// <inheritdoc/>
    public void OnPeerConnected(int peer)
    {
        // The version goes first, then the advert, so a guest names the session before the join
        // answer lands. Nothing more: a bound session's field was fixed when it bound, so a
        // newcomer never reaches it. An unbound lobby announces every peer when a session binds.
        Tell(peer, new BuildVersionMessage(_version));
        SendAdvert(peer);
        Changes++;

        // A banned address is refused before its password is asked, so the right password does
        // not buy a booted player back in.
        if (AddressOf(peer) is { } address && _banned.Contains(address))
        {
            TurnAway(peer, NetCloseReason.Booted);
        }
        else if (AsksPassword)
        {
            _waiting[peer] = 0.0;
        }
    }

    /// <inheritdoc/>
    public void OnPeerDisconnected(int peer)
    {
        Changes++;
        _heard.Remove(peer);
        _clashing.Remove(peer);
        _waiting.Remove(peer);
        _turnedAway.RemoveAll(away => away.Peer == peer);
        _answered.Remove(peer);
        _held.RemoveAll(held => held.Peer == peer);
        ForgetPicks(peer);
        _pickBuilds.Remove(peer);
        _unpicked.Remove(peer);
        _teamActions.RemoveAll(asked => asked.Peer == peer);
        if (_listener != null && _bound.Remove(peer))
        {
            _listener.OnPeerDisconnected(peer);
        }
    }

    /// <summary>A co-op host's restart: every guest's flight payloads are dropped until that guest
    /// picks under the new round. What its old session sent meanwhile never reaches the new one.
    /// </summary>
    public void AwaitPicks()
    {
        _unpicked.Clear();
        _unpicked.AddRange(AllPeers);
        _held.Clear();
    }

    /// <inheritdoc/>
    public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload)
    {
        if (Keep(peer, channel, payload))
        {
            Changes++;
        }
    }

    /// <summary>Tells <paramref name="peer"/> why it is being sent away, with this end's version and
    /// the one the peer named. The caller still hangs up; the notice only lets the guest's board
    /// name the reason.</summary>
    public void Farewell(int peer, NetCloseReason reason)
    {
        _heard.TryGetValue(peer, out var theirs);
        Tell(peer, new SessionClosedMessage(reason, _version, theirs));
    }

    /// <summary>Closes the carrier underneath, when it is one that can be closed.</summary>
    public void Dispose()
    {
        _held.Clear();
        (_inner as IDisposable)?.Dispose();
    }

    // Takes one payload into the lobby's state, and says whether it was kept here. A payload passed
    // to a bound session, or dropped, is not.
    private bool Keep(int peer, int channel, ReadOnlySpan<byte> payload)
    {
        if (BuildVersionMessage.TryRead(payload, out var named))
        {
            Heard(peer, named.Version);
            return true;
        }

        // A refused guest reads why it was refused, so a close notice is taken from any peer.
        if (SessionClosedMessage.TryRead(payload, out var closed))
        {
            Closed = closed;
            return true;
        }

        // The password step is read from a peer not yet admitted, since it is what admits it.
        if (JoinPasswordMessage.TryRead(payload, out var answer))
        {
            TakePassword(peer, answer);
            return true;
        }

        if (Outside(peer))
        {
            return false;
        }

        if (SessionAdvertMessage.TryRead(payload, out var advert))
        {
            Advert = advert;
            if (advert.Password && _joinPassword != null && _answered.Add(peer))
            {
                Tell(peer, new JoinPasswordMessage(false, _joinPassword));
            }

            return true;
        }

        if (CoopFlowMessage.TryRead(payload, out var flow))
        {
            Flow = flow;
            Flows++;
            // A flight under a new round is the host's restart. The ending of a flight names a
            // board instead, and a bound session still hears that flight's tail.
            _flightOver |= _listener != null && _boundFlow is { Screen: NetCoopScreen.InMission } under
                && flow.Screen == NetCoopScreen.InMission && flow.Epoch != under.Epoch;
            return true;
        }

        if (CoopHangarMessage.TryRead(payload, out var hangar))
        {
            // Each word names the hangar's size, so one from a smaller hangar drops the planes past it.
            while (_hangar.Count > hangar.Count)
            {
                _hangar.RemoveAt(_hangar.Count - 1);
            }

            while (_hangar.Count < hangar.Count)
            {
                _hangar.Add(null);
            }

            if (hangar.Index < _hangar.Count)
            {
                _hangar[hangar.Index] = hangar;
            }

            return true;
        }

        if (CoopPickMessage.TryRead(payload, out var pick))
        {
            // A guest picks only on a board, so whatever it sent before is a flight's that ended.
            TakePick(peer, pick);
            _held.RemoveAll(held => held.Peer == peer);
            _unpicked.Remove(peer);
            return true;
        }

        if (PlaneBuildMessage.TryRead(payload, out var built))
        {
            TakeBuild(peer, built);
            return true;
        }

        if (_unpicked.Contains(peer))
        {
            return false;
        }

        if (CoopSeatFitMessage.TryRead(payload, out var seatFit))
        {
            _seatFits[seatFit.Seat] = seatFit.Fit;
            return true;
        }

        if (CoopWingmanMessage.TryRead(payload, out var wingman))
        {
            Wingman = wingman;
            return true;
        }

        if (CoopFilmMessage.TryRead(payload, out var film))
        {
            Film = film;
            return true;
        }

        if (TakeDogfight(peer, payload))
        {
            return true;
        }

        if (_listener != null && !_flightOver)
        {
            if (_bound.Contains(peer))
            {
                _listener.OnPayload(peer, channel, payload);
            }

            return false;
        }

        // Past the depth the oldest goes. A lobby nobody ever binds must not grow without bound.
        if (_held.Count >= HeldPayloads)
        {
            _held.RemoveAt(0);
        }

        _held.Add((peer, channel, payload.ToArray()));
        return true;
    }

    // The Dogfight lobby's six messages. A chat or team action inbox past the held depth drops its
    // oldest entry, so a lobby nobody reads cannot grow without bound.
    private bool TakeDogfight(int peer, ReadOnlySpan<byte> payload)
    {
        if (DogfightOptionsMessage.TryRead(payload, out var options))
        {
            DogfightOptions = options;
            return true;
        }

        if (LobbyTeamsMessage.TryRead(payload, out var teams))
        {
            Teams = teams;
            return true;
        }

        if (LobbyTeamActionMessage.TryRead(payload, out var action))
        {
            if (_teamActions.Count >= HeldPayloads)
            {
                _teamActions.RemoveAt(0);
            }

            _teamActions.Add((peer, action));
            return true;
        }

        if (DogfightRosterMessage.TryRead(payload, out var roster))
        {
            DogfightRoster = roster;
            return true;
        }

        if (LobbyPlaneRulesMessage.TryRead(payload, out var rules))
        {
            PlaneRules = rules;
            return true;
        }

        if (!LobbyChatMessage.TryRead(payload, out var line))
        {
            return false;
        }

        if (_chat.Count >= HeldPayloads)
        {
            _chat.RemoveAt(0);
        }

        _chat.Add((peer, line));
        return true;
    }

    // Whether a connected peer stands off every peer list: a clashing build, one not yet admitted,
    // or one turned away.
    private bool Outside(int peer)
    {
        if (_clashing.Contains(peer) || _waiting.ContainsKey(peer))
        {
            return true;
        }

        foreach (var (away, _) in _turnedAway)
        {
            if (away == peer)
            {
                return true;
            }
        }

        return false;
    }

    // ⚠ Compared on the host alone and ordinally. A guest's word that it is admitted is never read
    // as its admission, so no guest can let itself in.
    private void TakePassword(int peer, JoinPasswordMessage answer)
    {
        if (answer.Admitted)
        {
            Admitted = true;
            return;
        }

        if (!_waiting.ContainsKey(peer))
        {
            return;
        }

        if (string.Equals(answer.Password, _hostPassword, StringComparison.Ordinal))
        {
            _waiting.Remove(peer);
            Tell(peer, new JoinPasswordMessage(true, ""));
            return;
        }

        TurnAway(peer, NetCloseReason.WrongPassword);
    }

    // A peer turned away leaves every peer list, as a clashing one does. The door sends it the close
    // notice and hangs up.
    private void TurnAway(int peer, NetCloseReason why)
    {
        _waiting.Remove(peer);
        if (Outside(peer))
        {
            return;
        }

        _turnedAway.Add((peer, why));
        _held.RemoveAll(held => held.Peer == peer);
        ForgetPicks(peer);
        _pickBuilds.Remove(peer);
        Changes++;
        if (_listener != null && _bound.Remove(peer))
        {
            _listener.OnPeerDisconnected(peer);
        }
    }

    // A peer that clashes leaves the peer lists at once, and the bound session with it. What it
    // sent before is dropped, since none of it was meant for a build of this version.
    private void Heard(int peer, NetBuildVersion version)
    {
        _heard[peer] = version;
        if (version.PlaysWith(_version) || _clashing.Contains(peer))
        {
            return;
        }

        _clashing.Add(peer);
        _held.RemoveAll(held => held.Peer == peer);
        ForgetPicks(peer);
        _pickBuilds.Remove(peer);
        if (_listener != null && _bound.Remove(peer))
        {
            _listener.OnPeerDisconnected(peer);
        }
    }

    // A pick is kept by peer and by its seat among that peer's own. The first seat's is also the
    // peer's pick every one-seat reader takes.
    private void TakePick(int peer, CoopPickMessage pick)
    {
        if (!_seatPicks.TryGetValue(peer, out var picks))
        {
            picks = new CoopPickMessage?[CoopPickMessage.MaxLocal + 1];
            _seatPicks[peer] = picks;
        }

        int local = Math.Min((int)pick.Local, CoopPickMessage.MaxLocal);
        picks[local] = pick;
        if (local == 0)
        {
            _picks[peer] = pick;
        }
    }

    private void ForgetPicks(int peer)
    {
        _picks.Remove(peer);
        _seatPicks.Remove(peer);
    }

    // A guest's own build goes with its pick, by peer. A host's build names a seat of its launch.
    private void TakeBuild(int peer, PlaneBuildMessage built)
    {
        if (built.Seat != PlaneBuildMessage.Mine)
        {
            _seatBuilds[built.Seat] = built.Build;
        }
        else if (built.Build is { } build)
        {
            _pickBuilds[peer] = build;
        }
        else
        {
            _pickBuilds.Remove(peer);
        }
    }

    private void SendAdvert(int peer)
    {
        if (_advertising is { } advert)
        {
            Tell(peer, advert);
        }
    }
}
