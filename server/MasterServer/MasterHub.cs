using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using CSVM.Net;

namespace CSVM.Master;

/// <summary>One open socket as the hub sees it. Sending only queues, so the hub may send while it
/// holds its lock; a socket that cannot keep up closes itself.</summary>
public interface IMasterClient
{
    /// <summary>The address the socket came from, without a port.</summary>
    string Address { get; }

    /// <summary>Queues one message for the other end.</summary>
    void Send(MasterMessage message);

    /// <summary>Tells the other end why, then closes the socket.</summary>
    void Close(string why);
}

/// <summary>
/// Everything the server knows, with no socket in it: the listed games, each one's host socket and
/// the guests negotiating with it, and the routing between them. A host registers on its socket and
/// keeps its game listed by repeating it; the socket closing, or a silence past
/// <see cref="MasterWire.ExpirySeconds"/>, ends the listing. A guest names a code, is given a peer
/// id, and from then on the hub relays SDP and ICE candidates between that guest and its host alone.
/// The server sees no game traffic: once the WebRTC link stands, the guest's socket closes.
/// </summary>
public sealed class MasterHub
{
    /// <summary>The peer id a host goes by, the one every guest signals to.</summary>
    public const int HostPeer = 1;

    /// <summary>The first peer id a game hands a guest.</summary>
    public const int FirstGuestPeer = 2;

    /// <summary>What a build below <see cref="Oldest"/> is told on every message it sends.</summary>
    public const string OutdatedWhy = "This version of CSVM is too old for the master server. Update CSVM to host or join internet games.";

    private readonly object _gate = new();
    private readonly Dictionary<string, Game> _games = new(StringComparer.Ordinal);
    private readonly Dictionary<IMasterClient, Role> _roles = new(ReferenceEqualityComparer.Instance);

    // A refused host keeps its socket and heartbeat, and would reopen a closed one every few
    // seconds. So the socket stays open and every later message gets the same words, which keeps
    // its lobby on the update request rather than on "only a host updates its game".
    private readonly HashSet<IMasterClient> _outdated = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, int> _seen = new();
    private readonly MasterOptions _options;
    private readonly TimeProvider _time;
    private readonly TurnCredentials _ice;
    private readonly Func<int, int> _pick;

    /// <summary>A hub over <paramref name="options"/>' limits and ICE settings, timed by
    /// <paramref name="time"/>. <paramref name="pick"/> draws a code character's place below its
    /// argument; a suite passes its own to make codes predictable.</summary>
    public MasterHub(MasterOptions options, TimeProvider time, Func<int, int>? pick = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _ice = new TurnCredentials(options, time);
        _pick = pick ?? RandomNumberGenerator.GetInt32;
    }

    /// <summary>The oldest <see cref="MasterWire.ProtocolVersion"/> this server serves, from
    /// <see cref="MasterOptions.OldestProtocol"/> and never below 1.</summary>
    public int Oldest => Math.Max(1, _options.OldestProtocol);

    /// <summary>How many games are hosted, unlisted ones included.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _games.Count;
            }
        }
    }

    /// <summary>How many sockets named each protocol version on their first host or join since the
    /// server started; a reopened socket counts again. A sender naming none counts as 1, and every
    /// version past this server's as one past it. It says when the old versions are gone.</summary>
    public IReadOnlyDictionary<int, int> Seen()
    {
        lock (_gate)
        {
            return new SortedDictionary<int, int>(_seen);
        }
    }

    /// <summary>Every listed game, by name, each with its code. An unlisted game is left out; a
    /// join names its code without the list.</summary>
    public MasterGameList List()
    {
        lock (_gate)
        {
            var games = _games.Values
                .Where(game => !game.Listing.Unlisted)
                .OrderBy(game => game.Listing.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.Code, StringComparer.Ordinal)
                .Select(game =>
                {
                    var listed = MasterWire.Clean(game.Listing);
                    listed.Code = game.Code;
                    return listed;
                })
                .ToList();
            return new MasterGameList { Games = games, Protocol = MasterWire.ProtocolVersion, Oldest = Oldest };
        }
    }

    /// <summary>Acts on one message from <paramref name="from"/>. A message the sender may not
    /// send in its role is answered with an error and changes nothing.</summary>
    public void Receive(IMasterClient from, MasterMessage message)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            // Only a socket's first host or join names the build. A socket holding a role is
            // refused that message anyway, and must not be marked outdated by a stray one.
            if ((message.T is MasterWire.Host or MasterWire.Join) && !_roles.ContainsKey(from) && !_outdated.Contains(from))
            {
                // The count's key is the sender's word, so it is held to a few buckets: one past
                // this server's own version stands for every newer one.
                int protocol = message.Protocol ?? 1;
                int bucket = Math.Clamp(protocol, 0, MasterWire.ProtocolVersion + 1);
                _seen[bucket] = _seen.GetValueOrDefault(bucket) + 1;
                if (protocol < Oldest)
                {
                    _outdated.Add(from);
                }
            }

            if (_outdated.Contains(from))
            {
                Refuse(from, OutdatedWhy);
                return;
            }

            switch (message.T)
            {
                case MasterWire.Host:
                    Register(from, message.Game);
                    break;
                case MasterWire.Update:
                    Refresh(from, message.Game);
                    break;
                case MasterWire.Join:
                    Admit(from, message.Code);
                    break;
                case MasterWire.Signal:
                    Relay(from, message);
                    break;
                default:
                    Refuse(from, $"unknown message '{message.T}'");
                    break;
            }
        }
    }

    /// <summary>The socket <paramref name="client"/> closed. A host's game leaves the list and its
    /// negotiating guests are told; a guest's host is told it left.</summary>
    public void Closed(IMasterClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_gate)
        {
            _outdated.Remove(client);
            if (!_roles.Remove(client, out var role) || !_games.TryGetValue(role.Code, out var game))
            {
                return;
            }

            if (role.Peer == HostPeer)
            {
                Drop(game, null);
                return;
            }

            game.Guests.Remove(role.Peer);
            game.Host.Send(new MasterMessage { T = MasterWire.Left, Peer = role.Peer });
        }
    }

    /// <summary>Drops every game whose host has been silent past <see cref="MasterWire.ExpirySeconds"/>
    /// and closes every guest socket open past <see cref="MasterOptions.GuestSocketSeconds"/>.
    /// Returns how many games were dropped.</summary>
    public int Sweep()
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            var silent = _games.Values.Where(game => (now - game.Heard).TotalSeconds > MasterWire.ExpirySeconds).ToList();
            foreach (var game in silent)
            {
                Drop(game, "the game was not heard from and was dropped from the list");
            }

            foreach (var game in _games.Values)
            {
                foreach (var (peer, guest) in game.Guests.ToList())
                {
                    if (_roles.TryGetValue(guest, out var role) && (now - role.Since).TotalSeconds > _options.GuestSocketSeconds)
                    {
                        guest.Close("the negotiation took too long");
                    }
                }
            }

            return silent.Count;
        }
    }

    private static void Refuse(IMasterClient to, string why) =>
        to.Send(new MasterMessage { T = MasterWire.Error, Why = why });

    private void Register(IMasterClient from, MasterGame? listing)
    {
        if (_roles.ContainsKey(from))
        {
            Refuse(from, "this socket already holds a game or a join");
            return;
        }

        if (listing == null)
        {
            Refuse(from, "a host message carries its game");
            return;
        }

        if (_games.Count >= _options.MaxGames)
        {
            Refuse(from, "the server lists as many games as it can; try again later");
            return;
        }

        int hosted = _games.Values.Count(game => game.Host.Address == from.Address);
        if (hosted >= _options.MaxGamesPerAddress)
        {
            Refuse(from, "this address already hosts as many games as it may");
            return;
        }

        string code = NewCode();
        var game = new Game(code, from, MasterWire.Clean(listing), _time.GetUtcNow());
        _games.Add(code, game);
        _roles.Add(from, new Role(code, HostPeer, game.Heard));
        from.Send(new MasterMessage { T = MasterWire.Hosted, Code = code });
    }

    private void Refresh(IMasterClient from, MasterGame? listing)
    {
        if (!_roles.TryGetValue(from, out var role) || role.Peer != HostPeer || !_games.TryGetValue(role.Code, out var game))
        {
            Refuse(from, "only a host updates its game");
            return;
        }

        if (listing != null)
        {
            game.Listing = MasterWire.Clean(listing);
        }

        game.Heard = _time.GetUtcNow();
    }

    private void Admit(IMasterClient from, string? typed)
    {
        if (_roles.ContainsKey(from))
        {
            Refuse(from, "this socket already holds a game or a join");
            return;
        }

        if (!MasterWire.TryCode(typed, out string code) || !_games.TryGetValue(code, out var game))
        {
            Refuse(from, "no game is listed under that code");
            return;
        }

        if (game.Guests.Count >= _options.MaxPendingGuests)
        {
            Refuse(from, "too many players are joining that game at once; try again");
            return;
        }

        int peer = game.NextPeer++;
        game.Guests.Add(peer, from);
        _roles.Add(from, new Role(code, peer, _time.GetUtcNow()));
        from.Send(new MasterMessage { T = MasterWire.Joined, Code = code, Peer = peer, Ice = _ice.For($"{code}.{peer}") });
        game.Host.Send(new MasterMessage
        {
            T = MasterWire.Incoming, Peer = peer, Addr = from.Address, Ice = _ice.For($"{code}.host"),
        });
    }

    // A signal is rebuilt from its own fields rather than forwarded whole, so a sender cannot put
    // anything else in front of the other end, and its From is the server's word, not the sender's.
    private void Relay(IMasterClient from, MasterMessage signal)
    {
        if (!_roles.TryGetValue(from, out var role) || !_games.TryGetValue(role.Code, out var game))
        {
            Refuse(from, "a signal needs a game or a join first");
            return;
        }

        if (signal.Kind is not (MasterWire.Offer or MasterWire.Answer or MasterWire.Candidate))
        {
            Refuse(from, "a signal is an offer, an answer or a candidate");
            return;
        }

        IMasterClient? to = role.Peer == HostPeer
            ? signal.To is { } guest && game.Guests.TryGetValue(guest, out var found) ? found : null
            : signal.To == HostPeer ? game.Host : null;
        if (to == null)
        {
            Refuse(from, "no such peer is negotiating");
            return;
        }

        to.Send(new MasterMessage
        {
            T = MasterWire.Signal, From = role.Peer, To = signal.To, Kind = signal.Kind, Sdp = signal.Sdp,
            Mid = signal.Mid, Index = signal.Index,
        });
    }

    private void Drop(Game game, string? why)
    {
        _games.Remove(game.Code);
        _roles.Remove(game.Host);
        foreach (var guest in game.Guests.Values)
        {
            guest.Send(new MasterMessage { T = MasterWire.Closed, Why = "the host's game is gone" });
            _roles.Remove(guest);
        }

        game.Guests.Clear();
        if (why != null)
        {
            game.Host.Close(why);
        }
    }

    private string NewCode()
    {
        while (true)
        {
            var letters = new char[MasterWire.CodeLength];
            for (int i = 0; i < letters.Length; i++)
            {
                letters[i] = MasterWire.CodeAlphabet[_pick(MasterWire.CodeAlphabet.Length)];
            }

            string code = MasterWire.Written(new string(letters));
            if (!_games.ContainsKey(code))
            {
                return code;
            }
        }
    }

    private readonly record struct Role(string Code, int Peer, DateTimeOffset Since);

    private sealed class Game
    {
        public Game(string code, IMasterClient host, MasterGame listing, DateTimeOffset heard)
        {
            Code = code;
            Host = host;
            Listing = listing;
            Heard = heard;
        }

        public string Code { get; }

        public IMasterClient Host { get; }

        public MasterGame Listing { get; set; }

        public DateTimeOffset Heard { get; set; }

        public int NextPeer { get; set; } = FirstGuestPeer;

        public Dictionary<int, IMasterClient> Guests { get; } = new();
    }
}
