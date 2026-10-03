using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Net;

/// <summary>
/// <see cref="INetTransport"/> over Godot's WebRTC peer, negotiated through the master server, so
/// a guest reaches a host with no port opened on either router. The server numbers the guest and
/// relays the SDP and the ICE candidates. STUN finds each end's public address, and TURN relays
/// when two NATs refuse a punched hole. A host lists on the same socket and reopens it after a
/// drop; a guest closes its socket once linked. Payloads ride <see cref="WebRtcFraming"/>.
/// ⚠ Construct nothing here unless <see cref="Available"/>: it needs the webrtc-native extension.
/// </summary>
public sealed class WebRtcTransport : INetTransport, INetLink, INetPeerAddress, INetListing, IDisposable
{
    /// <summary>The class the webrtc-native extension registers, whose presence is the test for it.
    /// </summary>
    public const string ExtensionClass = "WebRTCLibPeerConnection";

    /// <summary>How long a guest waits for its link before it gives up, in seconds. A relayed link
    /// takes a TURN allocation on top of the punch, so this is longer than a direct join's.</summary>
    public const double JoinTimeoutSeconds = 20.0;

    /// <summary>How long a host keeps a guest's negotiation that has not linked, in seconds.</summary>
    public const double NegotiationSeconds = 30.0;

    /// <summary>How long a host keeps an unlinked guest after the master says it left, in seconds.
    /// A guest closes its socket once its own end links. The host's end can report that link later,
    /// so the master's word alone must not cut it.</summary>
    public const double LeftGraceSeconds = 10.0;

    /// <summary>How long a host waits before it reopens a master socket that closed, in seconds.
    /// </summary>
    public const double ReopenSeconds = 10.0;

    private readonly bool _host;
    private readonly int _maxPeers;
    private readonly Func<IMasterSocket>? _reopen;
    private readonly Dictionary<int, Remote> _remotes = new();
    private readonly List<int> _peers = new();
    private readonly List<(int Peer, bool Joined)> _roster = new();
    private readonly List<(int Peer, int Channel, byte[] Bytes)> _held = new();
    private readonly WebRtcFraming _framing = new();
    private readonly WebRtcMultiplayerPeer _peer = new();
    private IMasterSocket? _socket;
    private MasterRegistration? _registration;
    private MasterGame? _listing;
    private INetTransportListener? _listener;
    private int _local;
    private double _clock;
    private double _closedAt = double.NaN;
    private bool _linked;
    private bool _closed;

    private WebRtcTransport(bool host, IMasterSocket socket, int maxPeers, Func<IMasterSocket>? reopen)
    {
        _host = host;
        _socket = socket;
        _maxPeers = maxPeers;
        _reopen = reopen;
        _peer.PeerConnected += OnPeerConnected;
        _peer.PeerDisconnected += OnPeerDisconnected;
    }

    /// <summary>Whether the webrtc-native extension is loaded, which every other member needs.
    /// </summary>
    public static bool Available => ClassDB.ClassExists(ExtensionClass);

    /// <inheritdoc/>
    public int LocalPeer => _local;

    /// <inheritdoc/>
    public IReadOnlyList<int> Peers => _peers;

    /// <inheritdoc/>
    /// <remarks>A guest stands at <see cref="NetLinkState.Connecting"/> until the host's data
    /// channels open, and at <see cref="NetLinkState.Down"/> for good once the join failed or the
    /// link dropped. A host is up until it closes.</remarks>
    public NetLinkState LinkState => _closed || Fault.Length > 0
        ? NetLinkState.Down
        : _host || _linked ? NetLinkState.Up : NetLinkState.Connecting;

    /// <inheritdoc/>
    public int PendingPayloads => _held.Count;

    /// <summary>Why a guest's join failed or its link dropped, as a player reads it, or "".</summary>
    public string Fault { get; private set; } = "";

    /// <inheritdoc/>
    public string LinkFault => Fault;

    /// <inheritdoc/>
    public string? JoinCode => _registration?.Code;

    /// <inheritdoc/>
    public string ListingFault => _registration?.Fault ?? "";

    /// <summary>How many sequenced payloads arrived stale and were dropped.</summary>
    public int DiscardedStale => _framing.DiscardedStale;

    /// <summary>How many guests a host is negotiating with that have not linked.</summary>
    internal int Negotiating
    {
        get
        {
            int count = 0;
            foreach (var remote in _remotes.Values)
            {
                count += remote.Linked ? 0 : 1;
            }

            return count;
        }
    }

    /// <summary>Lists a game on the master server <paramref name="open"/> connects to and admits up
    /// to <paramref name="maxPeers"/> guests negotiating through it. The host is peer 1. A socket
    /// that closes is reopened through <paramref name="open"/> after <see cref="ReopenSeconds"/>.
    /// </summary>
    public static WebRtcTransport Host(Func<IMasterSocket> open, int maxPeers)
    {
        ArgumentNullException.ThrowIfNull(open);
        RequireExtension();
        var transport = new WebRtcTransport(true, open(), maxPeers, open) { _local = 1 };
        transport._registration = new MasterRegistration(transport._socket!);
        var error = transport._peer.CreateServer();
        if (error != Error.Ok)
        {
            transport.Dispose();
            throw new InvalidOperationException($"cannot open a WebRTC host: {error}");
        }

        Log.Info("core", $"net: hosting over WebRTC through the master server");
        return transport;
    }

    /// <summary>Starts a join to the game the master server lists under <paramref name="code"/>
    /// over <paramref name="socket"/>, naming this build's <paramref name="version"/>. The host
    /// arrives as peer 1 on a later <see cref="Step"/>, or the link ends at
    /// <see cref="NetLinkState.Down"/> with <see cref="Fault"/> saying why.</summary>
    public static WebRtcTransport Join(IMasterSocket socket, string code, NetBuildVersion version)
    {
        ArgumentNullException.ThrowIfNull(socket);
        RequireExtension();
        if (!MasterWire.TryCode(code, out string written))
        {
            socket.Dispose();
            throw new ArgumentException($"'{code}' is not a join code", nameof(code));
        }

        socket.Send(new MasterMessage { T = MasterWire.Join, Code = written, Version = version.ToString(), Protocol = MasterWire.ProtocolVersion });
        return new WebRtcTransport(false, socket, 1, null);
    }

    /// <inheritdoc/>
    public void Bind(INetTransportListener listener)
    {
        if (_listener != null)
        {
            throw new InvalidOperationException($"peer {_local} already has a listener bound");
        }

        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        foreach (int peer in _peers.ToArray())
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

    /// <inheritdoc/>
    public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0)
    {
        if (channel is < 0 or >= NetChannels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, $"a channel is 0 to {NetChannels.Count - 1}");
        }

        if (_closed || !_peers.Contains(peer))
        {
            return;
        }

        byte[] frame = _framing.Frame(peer, channel, reliability, payload);
        _peer.SetTargetPeer(peer);
        _peer.TransferChannel = 0;
        _peer.TransferMode = reliability == NetReliability.Reliable
            ? MultiplayerPeer.TransferModeEnum.Reliable
            : MultiplayerPeer.TransferModeEnum.Unreliable;
        var error = _peer.PutPacket(frame);
        if (error != Error.Ok)
        {
            Log.Warn("core", $"net send refused peer={peer} channel={channel} bytes={payload.Length} error={error} (webrtc)");
        }
    }

    /// <inheritdoc/>
    public void Disconnect(int peer)
    {
        if (!_closed && _remotes.ContainsKey(peer))
        {
            Drop(peer);
        }
    }

    /// <inheritdoc/>
    public string? AddressOf(int peer) => _remotes.TryGetValue(peer, out var remote) ? remote.Address : null;

    /// <inheritdoc/>
    public void List(MasterGame listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        _listing = listing;
        _registration?.List(listing);
    }

    /// <summary>Reads the master socket, sends what the listing owes, polls every connection and
    /// reports the roster changes and then the payloads, as ENet's step does.</summary>
    public void Step(double dt)
    {
        if (dt < 0.0 || double.IsNaN(dt))
        {
            throw new ArgumentOutOfRangeException(nameof(dt), dt, "a transport does not step backwards");
        }

        if (_closed)
        {
            return;
        }

        _clock += dt;
        ReadSocket();
        _registration?.Step(dt);
        KeepSocket();
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            _peer.Poll();
        }

        GiveUpStale();
        AnnounceRoster();
        Drain();
    }

    /// <summary>Hangs up on everyone, closes the master socket and releases the peer. The
    /// departures are not reported, because this end is the one leaving.</summary>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        foreach (int peer in new List<int>(_remotes.Keys))
        {
            Forget(peer);
        }

        _peer.PeerConnected -= OnPeerConnected;
        _peer.PeerDisconnected -= OnPeerDisconnected;
        _peer.Close();
        _peer.Dispose();
        _socket?.Dispose();
        _socket = null;
        _peers.Clear();
        _held.Clear();
    }

    private static void RequireExtension()
    {
        if (!Available)
        {
            throw new InvalidOperationException("this build has no WebRTC library: run InstallWebRtc.ps1, then open the project once to import it");
        }
    }

    // A guest's dictionary for WebRtcPeerConnection.Initialize, in the shape its options name.
    private static Godot.Collections.Dictionary IceOf(List<MasterIceServer>? servers)
    {
        var list = new Godot.Collections.Array();
        foreach (var server in servers ?? new List<MasterIceServer>())
        {
            var urls = new Godot.Collections.Array();
            foreach (string url in server.Urls)
            {
                urls.Add(url);
            }

            var entry = new Godot.Collections.Dictionary { ["urls"] = urls };
            if (server.Username != null && server.Credential != null)
            {
                entry["username"] = server.Username;
                entry["credential"] = server.Credential;
            }

            list.Add(entry);
        }

        return new Godot.Collections.Dictionary { ["iceServers"] = list };
    }

    private void ReadSocket()
    {
        while (_socket != null && _socket.TryReceive(out var message))
        {
            if (_registration?.Take(message) == true)
            {
                continue;
            }

            switch (message.T)
            {
                case MasterWire.Incoming when _host && message.Peer is int guest:
                    Admit(guest, message.Addr, message.Ice);
                    break;
                case MasterWire.Joined when !_host && message.Peer is int self:
                    Joined(self, message.Ice);
                    break;
                case MasterWire.Signal when message.From is int from:
                    Apply(from, message);
                    break;
                case MasterWire.Left when _host && message.Peer is int gone && _remotes.TryGetValue(gone, out var remote) && !remote.Linked:
                    remote.LeftAt = double.IsNaN(remote.LeftAt) ? _clock : remote.LeftAt;
                    break;
                case MasterWire.Error or MasterWire.Closed when !_host && !_linked:
                    Fail(message.Why ?? "the host's game is gone");
                    break;
            }
        }
    }

    // A host reopens a socket that closed, after a pause, and lists its game afresh under a new
    // code. A guest's socket closing before its link stands ends the join.
    private void KeepSocket()
    {
        if (_socket?.State != MasterSocketState.Closed)
        {
            return;
        }

        if (!_host)
        {
            if (!_linked)
            {
                Fail(_socket.Fault.Length > 0 ? _socket.Fault : "the master server closed the join");
            }

            _socket.Dispose();
            _socket = null;
            return;
        }

        if (double.IsNaN(_closedAt))
        {
            _closedAt = _clock;
            Log.Warn("core", $"net: the master server socket closed ({_socket.Fault}); reopening in {ReopenSeconds:0} s");
            return;
        }

        if (_clock - _closedAt < ReopenSeconds || _reopen == null)
        {
            return;
        }

        _socket.Dispose();
        _socket = _reopen();
        _registration = new MasterRegistration(_socket);
        if (_listing != null)
        {
            _registration.List(_listing);
        }

        _closedAt = double.NaN;
    }

    private void Admit(int guest, string? address, List<MasterIceServer>? ice)
    {
        if (_remotes.ContainsKey(guest) || guest < 2 || _remotes.Count >= _maxPeers)
        {
            return;
        }

        var remote = Connect(guest, ice);
        if (remote == null)
        {
            return;
        }

        remote.Address = address;
        remote.Connection.CreateOffer();
    }

    private void Joined(int self, List<MasterIceServer>? ice)
    {
        if (_local != 0)
        {
            return;
        }

        var error = _peer.CreateClient(self);
        if (error != Error.Ok)
        {
            Fail($"cannot open a WebRTC guest: {error}");
            return;
        }

        _local = self;
        if (Connect(1, ice) == null)
        {
            Fail("cannot open a WebRTC connection to the host");
        }
    }

    // One connection to one other end, added to the peer, its description and candidates going
    // out through the master socket as the engine raises them.
    private Remote? Connect(int peer, List<MasterIceServer>? ice)
    {
        var connection = new WebRtcPeerConnection();
        var error = connection.Initialize(IceOf(ice));
        if (error != Error.Ok)
        {
            Log.Warn("core", $"net: a WebRTC connection would not initialise ({error})");
            connection.Dispose();
            return null;
        }

        var remote = new Remote(connection, _clock);
        remote.Described = (type, sdp) =>
        {
            connection.SetLocalDescription(type, sdp);
            _socket?.Send(new MasterMessage { T = MasterWire.Signal, To = peer, Kind = type, Sdp = sdp });
        };
        remote.Candidate = (media, index, name) =>
            _socket?.Send(new MasterMessage { T = MasterWire.Signal, To = peer, Kind = MasterWire.Candidate, Sdp = name, Mid = media, Index = (int)index });
        connection.SessionDescriptionCreated += remote.Described;
        connection.IceCandidateCreated += remote.Candidate;
        error = _peer.AddPeer(connection, peer);
        if (error != Error.Ok)
        {
            Log.Warn("core", $"net: a WebRTC peer {peer} would not join the mesh ({error})");
            connection.SessionDescriptionCreated -= remote.Described;
            connection.IceCandidateCreated -= remote.Candidate;
            connection.Dispose();
            return null;
        }

        _remotes[peer] = remote;
        return remote;
    }

    private void Apply(int from, MasterMessage signal)
    {
        if (!_remotes.TryGetValue(from, out var remote) || signal.Sdp == null)
        {
            return;
        }

        var error = signal.Kind switch
        {
            MasterWire.Offer when !_host => remote.Connection.SetRemoteDescription(MasterWire.Offer, signal.Sdp),
            MasterWire.Answer when _host => remote.Connection.SetRemoteDescription(MasterWire.Answer, signal.Sdp),
            MasterWire.Candidate => remote.Connection.AddIceCandidate(signal.Mid ?? "", signal.Index ?? 0, signal.Sdp),
            _ => Error.Ok,
        };
        if (error != Error.Ok)
        {
            Log.Warn("core", $"net: a WebRTC {signal.Kind} from peer {from} was refused ({error})");
        }
    }

    private void GiveUpStale()
    {
        if (!_host)
        {
            if (!_linked && Fault.Length == 0 && _clock >= JoinTimeoutSeconds)
            {
                Fail($"no link to the host in {JoinTimeoutSeconds:0} seconds");
            }

            return;
        }

        foreach (var (peer, remote) in new List<KeyValuePair<int, Remote>>(_remotes))
        {
            if (remote.Linked)
            {
                continue;
            }

            if (_clock - remote.Since > NegotiationSeconds)
            {
                Log.Info("core", $"net: WebRTC guest {peer} did not link in {NegotiationSeconds:0} s; dropped");
                Drop(peer);
            }
            else if (_clock - remote.LeftAt > LeftGraceSeconds)
            {
                Log.Info("core", $"net: WebRTC guest {peer} left the master and did not link in {LeftGraceSeconds:0} s; dropped");
                Drop(peer);
            }
        }
    }

    // Raised inside a poll, so they only queue. The step reports them in order.
    private void OnPeerConnected(long id) => _roster.Add(((int)id, true));

    private void OnPeerDisconnected(long id) => _roster.Add(((int)id, false));

    private void AnnounceRoster()
    {
        foreach (var (peer, joined) in _roster)
        {
            if (joined && !_peers.Contains(peer) && _remotes.TryGetValue(peer, out var remote))
            {
                remote.Linked = true;
                _peers.Add(peer);
                if (!_host)
                {
                    _linked = true;
                    _socket?.Dispose();
                    _socket = null;
                    Log.Info("core", $"net: linked to the host over WebRTC as peer {_local}");
                }

                _listener?.OnPeerConnected(peer);
            }
            else if (!joined)
            {
                Forget(peer);
                if (_peers.Remove(peer))
                {
                    _framing.Forget(peer);
                    _listener?.OnPeerDisconnected(peer);
                }

                if (!_host && _linked)
                {
                    Fail("the link to the host dropped");
                }
            }
        }

        _roster.Clear();
    }

    private void Drain()
    {
        while (!_closed && _peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected
               && _peer.GetAvailablePacketCount() > 0)
        {
            // ⚠ Read the source before taking the packet; it answers about the one at the head.
            int from = _peer.GetPacketPeer();
            byte[] frame = _peer.GetPacket();
            if (!_peers.Contains(from) || !_framing.TryOpen(from, frame, out int channel, out var payload))
            {
                continue;
            }

            if (_listener is { } listener)
            {
                listener.OnPayload(from, channel, payload);
            }
            else
            {
                if (_held.Count >= INetLink.HeldPayloads)
                {
                    _held.RemoveAt(0);
                }

                _held.Add((from, channel, payload.ToArray()));
            }
        }
    }

    // The peer leaves the mesh. A linked one is reported gone on the next step, never from inside
    // the caller's own call, as ENet reports a hang-up.
    private void Drop(int peer)
    {
        if (_peer.HasPeer(peer))
        {
            _peer.RemovePeer(peer);
        }

        if (_peers.Contains(peer))
        {
            _roster.Add((peer, false));
        }

        Forget(peer);
    }

    private void Forget(int peer)
    {
        if (!_remotes.Remove(peer, out var remote))
        {
            return;
        }

        remote.Connection.SessionDescriptionCreated -= remote.Described;
        remote.Connection.IceCandidateCreated -= remote.Candidate;
        remote.Connection.Close();
    }

    private void Fail(string why)
    {
        if (Fault.Length > 0)
        {
            return;
        }

        Fault = why;
        Log.Warn("core", $"net: WebRTC join failed: {why}");
        _socket?.Dispose();
        _socket = null;
    }

    // One other end: its connection, the handlers this transport hooked onto it, and where it is.
    private sealed class Remote
    {
        public Remote(WebRtcPeerConnection connection, double since)
        {
            Connection = connection;
            Since = since;
        }

        public WebRtcPeerConnection Connection { get; }

        public double Since { get; }

        public string? Address { get; set; }

        public bool Linked { get; set; }

        // When the master said this guest left, NaN until it does.
        public double LeftAt { get; set; } = double.NaN;

        public WebRtcPeerConnection.SessionDescriptionCreatedEventHandler Described { get; set; } = null!;

        public WebRtcPeerConnection.IceCandidateCreatedEventHandler Candidate { get; set; } = null!;
    }
}
