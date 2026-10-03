using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace CSVM.Net;

/// <summary>
/// A carrier with the loopback's wire conditions laid over it, so a real link can be flown under a
/// soak cell. Both directions are shaped at this end under one <see cref="LoopbackConditions"/>.
/// A send waits before it reaches the carrier, an arrival before the session sees it, and each
/// draws its own loss. The loopback's rules hold: loss takes only unreliable classes, reliable
/// streams keep their order per peer, and an overtaken sequenced payload is discarded. Every draw
/// comes from this wrapper's own generator, timed on the clock it is given.
/// ⚠ Do not drop an arrival whose carrier names no class; it is carried as reliable, as it may be.
/// </summary>
public sealed class ShapedTransport : INetTransport, INetLink, INetPeerAddress, IDisposable
{
    private readonly INetTransport _inner;
    private readonly Func<double> _clock;
    private readonly Direction _outgoing;
    private readonly Direction _incoming;
    private readonly Dictionary<int, double> _leaving = new();
    private INetTransportListener? _listener;
    private bool _closed;

    /// <summary>Shapes <paramref name="inner"/> under <paramref name="conditions"/> both ways,
    /// drawing from <paramref name="rng"/> and timing every delay on <paramref name="clock"/>, in
    /// seconds.</summary>
    public ShapedTransport(INetTransport inner, LoopbackConditions conditions, Random rng, Func<double> clock)
    {
        ArgumentNullException.ThrowIfNull(rng);
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Conditions = conditions;
        _outgoing = new Direction(conditions, rng);
        _incoming = new Direction(conditions, rng);
    }

    /// <summary>The names <see cref="TryParse"/> takes for the soak's shaped cells.</summary>
    public static IEnumerable<string> CellNames => SoakCells.Named.Select(cell => cell.Name);

    /// <summary>The conditions each direction is shaped under.</summary>
    public LoopbackConditions Conditions { get; }

    /// <summary>Payloads the loss model threw away, sends and arrivals together.</summary>
    public int Lost => _outgoing.Lost + _incoming.Lost;

    /// <summary>Sequenced payloads overtaken on their channel and discarded, either way.</summary>
    public int DiscardedStale => _outgoing.Discarded + _incoming.Discarded;

    /// <inheritdoc/>
    public int LocalPeer => _inner.LocalPeer;

    /// <inheritdoc/>
    public IReadOnlyList<int> Peers => _inner.Peers;

    /// <inheritdoc/>
    public NetLinkState LinkState => (_inner as INetLink)?.LinkState ?? NetLinkState.Up;

    /// <inheritdoc/>
    public int PendingPayloads => (_inner as INetLink)?.PendingPayloads ?? 0;

    /// <inheritdoc/>
    public string LinkFault => (_inner as INetLink)?.LinkFault ?? "";

    /// <summary>Shapes <paramref name="inner"/> on the wall clock with a generator of its own, as
    /// a launch does. A real link's delay is real seconds, whatever the simulation's step.</summary>
    public static ShapedTransport OnWallClock(INetTransport inner, LoopbackConditions conditions)
    {
        var wall = Stopwatch.StartNew();
        return new ShapedTransport(inner, conditions, new Random(), () => wall.Elapsed.TotalSeconds);
    }

    /// <summary>Reads <c>latency ms,jitter ms,loss %</c>, or one of <see cref="CellNames"/>. False
    /// for anything else, a negative value, or a loss above 100 per cent.</summary>
    public static bool TryParse(string? text, out LoopbackConditions conditions)
    {
        conditions = LoopbackConditions.Perfect;
        string value = (text ?? "").Trim();
        foreach (var (name, cell) in SoakCells.Named)
        {
            if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
            {
                conditions = cell;
                return true;
            }
        }

        string[] parts = value.Split(',');
        if (parts.Length != 3)
        {
            return false;
        }

        var numbers = new double[3];
        for (int i = 0; i < 3; i++)
        {
            if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])
                || !double.IsFinite(numbers[i]) || numbers[i] < 0.0)
            {
                return false;
            }
        }

        if (numbers[2] > 100.0)
        {
            return false;
        }

        conditions = new LoopbackConditions(numbers[0] / 1000.0, numbers[1] / 1000.0, numbers[2] / 100.0);
        return true;
    }

    /// <summary>The conditions as a log line reads them, in milliseconds and per cent.</summary>
    public static string Describe(LoopbackConditions conditions) => string.Format(
        CultureInfo.InvariantCulture, "latency {0:0.#} ms, jitter {1:0.#} ms, loss {2:0.#} %",
        conditions.Latency * 1000.0, conditions.Jitter * 1000.0, conditions.Loss * 100.0);

    /// <inheritdoc/>
    /// <remarks>The carrier is bound here rather than at construction, so a carrier stepped before
    /// this holds what lands and replays it, as it would unshaped.</remarks>
    public void Bind(INetTransportListener listener)
    {
        if (_listener != null)
        {
            throw new InvalidOperationException("a shaped transport already has a listener bound");
        }

        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _inner.Bind(new Arrivals(this));
    }

    /// <inheritdoc/>
    public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
        _outgoing.Admit(peer, channel, reliability, payload, _clock());

    /// <inheritdoc/>
    /// <remarks>The reliable sends still waiting for <paramref name="peer"/> go to the carrier
    /// first, unshaped and in order, since a carrier hanging up refuses what follows.</remarks>
    public void Disconnect(int peer)
    {
        foreach (var pending in _outgoing.TakeReliable(peer))
        {
            _inner.Send(pending.Peer, pending.Payload, pending.Reliability, pending.Channel);
        }

        _inner.Disconnect(peer);
    }

    /// <inheritdoc/>
    /// <remarks>The sends now due go to the carrier first, so they leave on this step's poll. The
    /// arrivals now due are reported after the carrier's step, so an undelayed one lands at once. A
    /// payload due between steps moves on the next, so each direction adds up to a step.</remarks>
    public void Step(double dt)
    {
        if (dt < 0.0 || double.IsNaN(dt))
        {
            throw new ArgumentOutOfRangeException(nameof(dt), dt, "a transport does not step backwards");
        }

        _outgoing.Drain(_clock(), pending => _inner.Send(pending.Peer, pending.Payload, pending.Reliability, pending.Channel));
        _inner.Step(dt);
        double now = _clock();
        _incoming.Drain(now, Hand);
        foreach (int peer in _leaving.Where(leaving => leaving.Value <= now).Select(leaving => leaving.Key).ToList())
        {
            ReportLeft(peer);
        }
    }

    /// <inheritdoc/>
    public string? AddressOf(int peer) => (_inner as INetPeerAddress)?.AddressOf(peer);

    /// <summary>Hands the carrier every reliable send still waiting, unshaped and in order. One
    /// poll then puts them on the wire, as the unshaped link's last poll would have, before the
    /// carrier closes. The waiting unreliable sends and every waiting arrival are forgotten.
    /// </summary>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        foreach (var pending in _outgoing.TakeReliable(null))
        {
            _inner.Send(pending.Peer, pending.Payload, pending.Reliability, pending.Channel);
        }

        _inner.Step(0.0);
        (_inner as IDisposable)?.Dispose();
        _outgoing.Clear();
        _incoming.Clear();
        _leaving.Clear();
    }

    // A reliable payload the carrier delivered before the hang-up is still owed to the session, so
    // the departure waits behind the last of them. The waiting unreliable ones go with the peer.
    private void PeerLeft(int peer)
    {
        _outgoing.Forget(peer);
        if (_incoming.ReliableDueBy(peer) is double owed)
        {
            _incoming.ForgetUnreliable(peer);
            _leaving[peer] = owed;
            return;
        }

        _incoming.Forget(peer);
        _listener?.OnPeerDisconnected(peer);
    }

    // A peer id can come back while its old departure is held. The old peer's owed arrivals and
    // its departure then go first, so nothing of the old link is heard under the new one.
    private void PeerJoined(int peer)
    {
        if (_leaving.ContainsKey(peer))
        {
            foreach (var pending in _incoming.TakeReliable(peer))
            {
                Hand(pending);
            }

            ReportLeft(peer);
        }

        _listener?.OnPeerConnected(peer);
    }

    private void ReportLeft(int peer)
    {
        _leaving.Remove(peer);
        _incoming.Forget(peer);
        _listener?.OnPeerDisconnected(peer);
    }

    private void Hand(Pending pending)
    {
        if (_listener != null)
        {
            INetClassedListener.Deliver(_listener, pending.Peer, pending.Channel, pending.Reliability, pending.Payload);
        }
    }

    // One payload waiting in one direction, with the peer it goes to or came from.
    private readonly record struct Pending(
        int Peer, int Channel, NetReliability Reliability, int Sequence, byte[] Payload, double Deadline);

    // One direction's queue under the loopback's rules. A reliable deadline never falls before the
    // one ahead of it for the same peer. A sequence is spent even on a lost payload.
    private sealed class Direction
    {
        private readonly LoopbackConditions _conditions;
        private readonly Random _rng;
        private readonly List<Pending> _waiting = new();
        private readonly Dictionary<int, double> _reliableTail = new();
        private readonly Dictionary<(int Peer, int Channel), int> _numbered = new();
        private readonly Dictionary<(int Peer, int Channel), int> _newest = new();

        public Direction(LoopbackConditions conditions, Random rng)
        {
            _conditions = conditions;
            _rng = rng;
        }

        public int Lost { get; private set; }

        public int Discarded { get; private set; }

        // Numbers a sequenced payload, draws its loss, and queues a survivor behind its delay.
        public void Admit(int peer, int channel, NetReliability reliability, ReadOnlySpan<byte> payload, double now)
        {
            int sequence = 0;
            if (reliability == NetReliability.UnreliableSequenced)
            {
                _numbered.TryGetValue((peer, channel), out sequence);
                _numbered[(peer, channel)] = ++sequence;
            }

            if (reliability != NetReliability.Reliable && _conditions.Drops(_rng))
            {
                Lost++;
                return;
            }

            double deadline = now + _conditions.Delay(_rng);
            if (reliability == NetReliability.Reliable)
            {
                if (_reliableTail.TryGetValue(peer, out double tail))
                {
                    deadline = Math.Max(deadline, tail);
                }

                _reliableTail[peer] = deadline;
            }

            _waiting.Add(new Pending(peer, channel, reliability, sequence, payload.ToArray(), deadline));
        }

        // Hands on everything due, in deadline order, less any sequenced payload overtaken on its
        // channel. The sort is stable and each peer's reliable deadlines are monotonic.
        public void Drain(double now, Action<Pending> deliver)
        {
            var due = new List<Pending>();
            int kept = 0;
            for (int i = 0; i < _waiting.Count; i++)
            {
                if (_waiting[i].Deadline <= now)
                {
                    due.Add(_waiting[i]);
                }
                else
                {
                    _waiting[kept++] = _waiting[i];
                }
            }

            _waiting.RemoveRange(kept, _waiting.Count - kept);
            foreach (var pending in due.OrderBy(pending => pending.Deadline))
            {
                if (Overtaken(pending))
                {
                    Discarded++;
                    continue;
                }

                deliver(pending);
            }
        }

        // Every waiting reliable payload for one peer, or for all when null, in the order it would
        // have been handed on. That peer's other waiting payloads are forgotten.
        public List<Pending> TakeReliable(int? peer)
        {
            bool Mine(Pending pending) => peer == null || pending.Peer == peer;
            var reliable = _waiting.Where(pending => Mine(pending) && pending.Reliability == NetReliability.Reliable)
                .OrderBy(pending => pending.Deadline).ToList();
            _waiting.RemoveAll(Mine);
            return reliable;
        }

        // When the last reliable payload waiting from or to this peer falls due, or null for none.
        public double? ReliableDueBy(int peer) =>
            _waiting.Any(pending => pending.Peer == peer && pending.Reliability == NetReliability.Reliable)
                ? _reliableTail[peer]
                : null;

        public void ForgetUnreliable(int peer) =>
            _waiting.RemoveAll(pending => pending.Peer == peer && pending.Reliability != NetReliability.Reliable);

        public void Forget(int peer)
        {
            _waiting.RemoveAll(pending => pending.Peer == peer);
            _reliableTail.Remove(peer);
            foreach (var key in _numbered.Keys.Where(key => key.Peer == peer).ToList())
            {
                _numbered.Remove(key);
            }

            foreach (var key in _newest.Keys.Where(key => key.Peer == peer).ToList())
            {
                _newest.Remove(key);
            }
        }

        public void Clear()
        {
            _waiting.Clear();
            _reliableTail.Clear();
            _numbered.Clear();
            _newest.Clear();
        }

        private bool Overtaken(Pending pending)
        {
            if (pending.Reliability != NetReliability.UnreliableSequenced)
            {
                return false;
            }

            _newest.TryGetValue((pending.Peer, pending.Channel), out int newest);
            if (pending.Sequence <= newest)
            {
                return true;
            }

            _newest[(pending.Peer, pending.Channel)] = pending.Sequence;
            return false;
        }
    }

    // The carrier's listener. A join passes through once any held departure of its id is settled.
    // A departure and every payload go through the shaping. Once closed, it reaches nobody.
    private sealed class Arrivals : INetClassedListener
    {
        private readonly ShapedTransport _owner;

        public Arrivals(ShapedTransport owner) => _owner = owner;

        public void OnPeerConnected(int peer)
        {
            if (!_owner._closed)
            {
                _owner.PeerJoined(peer);
            }
        }

        public void OnPeerDisconnected(int peer)
        {
            if (!_owner._closed)
            {
                _owner.PeerLeft(peer);
            }
        }

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload) =>
            OnPayload(peer, channel, NetReliability.Reliable, payload);

        public void OnPayload(int peer, int channel, NetReliability reliability, ReadOnlySpan<byte> payload)
        {
            if (!_owner._closed)
            {
                _owner._incoming.Admit(peer, channel, reliability, payload, _owner._clock());
            }
        }
    }
}
