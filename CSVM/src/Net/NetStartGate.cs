using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>What a <see cref="StartGateMessage"/> says.</summary>
public enum NetStartWord : byte
{
    /// <summary>A word this build does not know.</summary>
    Unknown = 0,

    /// <summary>A guest's world is built and its aeroplane spawned, sent to the host.</summary>
    Loaded = 1,

    /// <summary>The host's word that the flight starts, sent to every guest.</summary>
    Start = 2,

    /// <summary>The host's round for this flight, sent to every machine it waits on and in answer
    /// to a loaded word under another round. A guest answers it with loaded under that round.
    /// </summary>
    Hold = 3,
}

/// <summary>Why a <see cref="NetStartGate"/> opened, <see cref="Held"/> while it has not.</summary>
public enum NetStartRelease
{
    /// <summary>Still waiting.</summary>
    Held = 0,

    /// <summary>A host with no seat flown elsewhere, which has nobody to wait for.</summary>
    Alone,

    /// <summary>Every machine the host waited on reported its world built.</summary>
    Everyone,

    /// <summary>The last machine still loading dropped its link instead.</summary>
    Left,

    /// <summary>The host's start word reached this guest.</summary>
    Started,

    /// <summary>Nothing answered within <see cref="NetStartGate.TimeoutSeconds"/>.</summary>
    TimedOut,
}

/// <summary>
/// One word of the start barrier, which the lobby passes to a bound session. It names the round of
/// its host's flight (<see cref="NetStartGate.Round"/>, 0 for none). A guest sends it
/// only once its handlers stand, so the host's answer is never unknown.
/// </summary>
public readonly record struct StartGateMessage(NetStartWord Word, byte Round = 0) : INetMessage<StartGateMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.StartGate;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out StartGateMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        var word = (NetStartWord)reader.ReadByte();
        message = new StartGateMessage(word, reader.ReadByte());
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)Word);
        writer.WriteByte(Round);
        writer.WriteUInt16(0);
        return writer.Close();
    }
}

/// <summary>
/// The start barrier of a network flight, one per session. A host waits on every machine that
/// flies a seat until each reports its world built, leaves, or the wait times out. A guest waits
/// for the host's start word. Pure state: the session sends the words and holds its own clock.
/// ⚠ A word under another round than the host's is dropped. A restart binds a new session on the
/// same link, so the old flight's loaded word could otherwise start the new one before it loads.
/// </summary>
public sealed class NetStartGate
{
    /// <summary>The longest any machine waits, in seconds. Under the transport's keepalive
    /// ceiling, so a guest whose load blocks is released before its link is dropped.</summary>
    public const double TimeoutSeconds = 120.0; // TUNE

    private static int _lastRound;

    private readonly HashSet<int> _waiting;

    private NetStartGate(bool isHost, IEnumerable<int> waiting, byte round)
    {
        IsHost = isHost;
        Round = round;
        _waiting = new HashSet<int>(waiting);
        if (isHost && _waiting.Count == 0)
        {
            Release = NetStartRelease.Alone;
        }
    }

    /// <summary>Whether this is the host's barrier, which decides, rather than a guest's.</summary>
    public bool IsHost { get; }

    /// <summary>The host's round for this flight, never 0 on a host. A guest's is 0 until the
    /// host's hold word names it.</summary>
    public byte Round { get; private set; }

    /// <summary>Why the gate opened, <see cref="NetStartRelease.Held"/> while it has not.</summary>
    public NetStartRelease Release { get; private set; }

    /// <summary>Whether the flight may run.</summary>
    public bool Open => Release != NetStartRelease.Held;

    /// <summary>How long the gate was held, in seconds of steps taken while it was.</summary>
    public double WaitedSeconds { get; private set; }

    /// <summary>The peers still waited on: the loading guests on a host, the host on a guest.
    /// </summary>
    public IReadOnlyCollection<int> Waiting => _waiting;

    /// <summary>A host's barrier over <paramref name="peers"/>, the distinct machines flying a
    /// seat, under <paramref name="round"/> (0 takes <see cref="NextRound"/>). Opens at once when
    /// there are none.</summary>
    public static NetStartGate Host(IEnumerable<int> peers, byte round = 0)
    {
        ArgumentNullException.ThrowIfNull(peers);
        return new NetStartGate(isHost: true, peers, round != 0 ? round : NextRound());
    }

    /// <summary>A guest's barrier, opened by the start word from <paramref name="hostPeer"/>, or
    /// by that link dropping.</summary>
    public static NetStartGate Guest(int hostPeer) => new(isHost: false, new[] { hostPeer }, 0);

    /// <summary>A round no host gate of this process took last, 1 to 255. Two flights in a row on
    /// one link never share one.</summary>
    public static byte NextRound()
    {
        int next = System.Threading.Interlocked.Increment(ref _lastRound);
        return (byte)(((next - 1) % byte.MaxValue) + 1);
    }

    /// <summary>A guest's loaded word under <paramref name="round"/>, on the host. True when it
    /// opened the gate. A word under another round is an earlier flight's and changes nothing.
    /// </summary>
    public bool TakeLoaded(int peer, byte round) =>
        IsHost && Current(round) && Strike(peer, NetStartRelease.Everyone);

    /// <summary>A peer's link dropped: a loading guest on the host, or the host on a guest. True
    /// when it opened the gate.</summary>
    public bool TakeLeft(int peer) => Strike(peer, NetStartRelease.Left);

    /// <summary>The host's hold word, on a guest still held. The first round heard is this
    /// flight's. True when the guest should answer with its loaded word under it.</summary>
    public bool TakeHold(byte round)
    {
        if (IsHost || Open || round == 0)
        {
            return false;
        }

        if (Round == 0)
        {
            Round = round;
        }

        return round == Round;
    }

    /// <summary>The host's start word under <paramref name="round"/>, on a guest. True when it
    /// opened the gate. It must name the round this guest answered.</summary>
    public bool TakeStart(byte round)
    {
        if (IsHost || Open || !Current(round))
        {
            return false;
        }

        _waiting.Clear();
        Release = NetStartRelease.Started;
        return true;
    }

    /// <summary>Whether a word under <paramref name="round"/> belongs to this flight.</summary>
    public bool Current(byte round) => round != 0 && round == Round;

    /// <summary>Counts <paramref name="dt"/> seconds of waiting. True when the timeout opened
    /// the gate on this step.</summary>
    public bool Step(double dt)
    {
        if (Open)
        {
            return false;
        }

        WaitedSeconds += Math.Max(0.0, dt);
        if (WaitedSeconds < TimeoutSeconds)
        {
            return false;
        }

        Release = NetStartRelease.TimedOut;
        return true;
    }

    private bool Strike(int peer, NetStartRelease why)
    {
        if (!_waiting.Remove(peer) || Open || _waiting.Count > 0)
        {
            return false;
        }

        Release = why;
        return true;
    }
}
