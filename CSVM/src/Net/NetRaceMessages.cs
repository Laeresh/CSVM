using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>What a <see cref="RaceRunMessage"/> reports about its owner's run.</summary>
public enum NetRaceRun : byte
{
    /// <summary>A kind this build does not know; the host counts nothing for it.</summary>
    Unknown = 0,

    /// <summary>The run's clock started, on the step after its count's GO.</summary>
    Started = 1,

    /// <summary>The run cleared one course zone for the first time, at the run time carried.</summary>
    Zone = 2,

    /// <summary>The run cleared every zone, in the run time carried.</summary>
    Finished = 3,

    /// <summary>The run was thrown away by a rerun.</summary>
    Abandoned = 4,
}

/// <summary>Where a host's stunt race stands, as a <see cref="RaceStateMessage"/> carries it. The
/// values are the race's own phases in order.</summary>
public enum NetRacePhase : byte
{
    /// <summary>The opening count runs and no run may start.</summary>
    Opening = 0,

    /// <summary>The window is open and any pilot may start a run.</summary>
    Open = 1,

    /// <summary>Time is up: a run in progress may still finish inside the cap.</summary>
    FinalRun = 2,

    /// <summary>The race is over and its board is due.</summary>
    Ended = 3,
}

/// <summary>What a <see cref="RaceCallMessage"/> asks of the machines in a stunt race once its
/// window is decided.</summary>
public enum NetRaceCall : byte
{
    /// <summary>A call this build does not know; it does nothing.</summary>
    Unknown = 0,

    /// <summary>The host opened a new window, the one carried, over the same course and field.</summary>
    Rerun = 1,

    /// <summary>The host took the race back to the lobby; every guest follows it there.</summary>
    Lobby = 2,

    /// <summary>A guest left the race, its seats with it.</summary>
    Leave = 3,
}

/// <summary>
/// One fact about a seat's own run, sent by the machine flying it to the host, which times nothing
/// of a remote seat itself. Reliable. <c>Run</c> numbers the owner's runs from 1 within a window,
/// and <c>Window</c> numbers the window. The host drops a repeated start and any report of a run or a
/// window it has moved past (<c>docs/org/multiplayer-messages.md</c>, "Stunt race").
/// </summary>
public readonly record struct RaceRunMessage(byte Seat, NetRaceRun Kind, byte Zone, byte Window, ushort Run, float RunTime)
    : INetMessage<RaceRunMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 12;

    /// <summary>The zone byte of a report that names no zone.</summary>
    public const byte NoZone = 0xFF;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.RaceRun;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out RaceRunMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte seat = reader.ReadByte();
        var kind = (NetRaceRun)reader.ReadByte();
        byte zone = reader.ReadByte();
        byte window = reader.ReadByte();
        ushort run = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        message = new RaceRunMessage(seat, kind, zone, window, run, reader.ReadSingle());
        return reader.Valid;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Seat);
        writer.WriteByte((byte)Kind);
        writer.WriteByte(Zone);
        writer.WriteByte(Window);
        writer.WriteUInt16(Run);
        writer.WriteUInt16(0);
        writer.WriteSingle(RunTime);
        return writer.Close();
    }
}

/// <summary>
/// The host's race clock: its phase, how far into it, the window's length and the host's session
/// clock at send. Reliable, sent once a second and at once on every change of phase, the ending
/// among them. <c>Elapsed</c> is the opening's seconds gone during the opening and the window's
/// after it. A guest reads it forward by its lateness, which is how its opening count starts on the
/// host's instant. <c>Window</c> numbers the window, so a guest can tell the host's next one.
/// </summary>
public readonly record struct RaceStateMessage(NetRacePhase Phase, byte Window, float Elapsed, float WindowSeconds, float HostClock)
    : INetMessage<RaceStateMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 16;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.RaceState;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out RaceStateMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        var phase = (NetRacePhase)reader.ReadByte();
        byte window = reader.ReadByte();
        _ = reader.ReadUInt16();
        float elapsed = reader.ReadSingle();
        float windowSeconds = reader.ReadSingle();
        message = new RaceStateMessage(phase, window, elapsed, windowSeconds, reader.ReadSingle());
        return reader.Valid;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)Phase);
        writer.WriteByte(Window);
        writer.WriteUInt16(0);
        writer.WriteSingle(Elapsed);
        writer.WriteSingle(WindowSeconds);
        writer.WriteSingle(HostClock);
        return writer.Close();
    }
}

/// <summary>
/// The window's end decided by one machine for the rest. The host sends every guest its new window
/// or its return to the lobby; a guest tells the host it left. Reliable, on the channel the race's
/// lines and clock take, so a rerun reaches a guest ahead of the new window's lines.
/// <c>Window</c> numbers the window the call opens or ends (<c>docs/org/multiplayer-messages.md</c>,
/// "Stunt race").
/// </summary>
public readonly record struct RaceCallMessage(NetRaceCall Call, byte Window) : INetMessage<RaceCallMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.RaceCall;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out RaceCallMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        var call = (NetRaceCall)reader.ReadByte();
        message = new RaceCallMessage(call, reader.ReadByte());
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)Call);
        writer.WriteByte(Window);
        writer.WriteUInt16(0);
        return writer.Close();
    }
}

/// <summary>
/// One racer's line of the host's leaderboard, sent reliably to every guest whenever the host's
/// record of that racer changes. It carries the counts the boards show and the best time, or the
/// furthest run's zones and time. The ranking run's splits follow, a split below zero being a zone
/// it never cleared. Fixed width, with room for <see cref="MaxZones"/> zones.
/// </summary>
public readonly struct RaceStandingMessage : INetMessage<RaceStandingMessage>, IEquatable<RaceStandingMessage>
{
    /// <summary>The most course zones a line carries. The longest shipped course has 17.</summary>
    public const int MaxZones = 24;

    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 20 + (4 * MaxZones);

    /// <summary>The split a line carries for a zone its ranking run never cleared.</summary>
    public const float NoSplit = -1f;

    private const byte InRunFlag = 0x01;
    private const byte CompletedFlag = 0x02;
    private const byte LeftFlag = 0x04;

    private readonly float[]? _splits;

    /// <summary>One racer's line. <paramref name="splits"/> is cut at <see cref="MaxZones"/>.</summary>
    public RaceStandingMessage(byte seat, byte window, bool inRun, bool completed, ushort runsStarted,
        ushort runsFinished, float bestTime, float timeToMostZones, byte mostZones, byte currentZones,
        IReadOnlyList<float> splits, bool left = false)
    {
        ArgumentNullException.ThrowIfNull(splits);
        Seat = seat;
        Left = left;
        Window = window;
        InRun = inRun;
        Completed = completed;
        RunsStarted = runsStarted;
        RunsFinished = runsFinished;
        BestTime = bestTime;
        TimeToMostZones = timeToMostZones;
        MostZones = mostZones;
        CurrentZones = currentZones;
        _splits = new float[Math.Min(splits.Count, MaxZones)];
        for (int i = 0; i < _splits.Length; i++)
            _splits[i] = splits[i];
    }

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.RaceStanding;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <summary>The seat this line describes.</summary>
    public byte Seat { get; }

    /// <summary>The window this line belongs to.</summary>
    public byte Window { get; }

    /// <summary>Whether the host counts a run of this racer in progress.</summary>
    public bool InRun { get; }

    /// <summary>Whether the racer has a completed run, so <see cref="BestTime"/> and the splits are its best.</summary>
    public bool Completed { get; }

    /// <summary>Whether the racer's pilot left the race; the line keeps the record it had.</summary>
    public bool Left { get; }

    public ushort RunsStarted { get; }

    public ushort RunsFinished { get; }

    /// <summary>The best completed run's time, zero without one.</summary>
    public float BestTime { get; }

    public float TimeToMostZones { get; }

    public byte MostZones { get; }

    public byte CurrentZones { get; }

    /// <summary>The ranking run's split per course zone, <see cref="NoSplit"/> where it cleared none.</summary>
    public IReadOnlyList<float> Splits => _splits ?? Array.Empty<float>();

    /// <summary>Whether two lines carry the same values.</summary>
    public static bool operator ==(RaceStandingMessage left, RaceStandingMessage right) => left.Equals(right);

    /// <summary>Whether two lines differ.</summary>
    public static bool operator !=(RaceStandingMessage left, RaceStandingMessage right) => !left.Equals(right);

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out RaceStandingMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte seat = reader.ReadByte();
        byte flags = reader.ReadByte();
        byte window = reader.ReadByte();
        int count = Math.Min((int)reader.ReadByte(), MaxZones);
        ushort started = reader.ReadUInt16();
        ushort finished = reader.ReadUInt16();
        float best = reader.ReadSingle();
        float toMost = reader.ReadSingle();
        byte most = reader.ReadByte();
        byte current = reader.ReadByte();
        _ = reader.ReadUInt16();
        var splits = new float[count];
        for (int i = 0; i < count; i++)
            splits[i] = reader.ReadSingle();

        message = new RaceStandingMessage(seat, window, (flags & InRunFlag) != 0, (flags & CompletedFlag) != 0,
            started, finished, best, toMost, most, current, splits, (flags & LeftFlag) != 0);
        return reader.Valid;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var splits = Splits;
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Seat);
        writer.WriteByte((byte)((InRun ? InRunFlag : 0) | (Completed ? CompletedFlag : 0) | (Left ? LeftFlag : 0)));
        writer.WriteByte(Window);
        writer.WriteByte((byte)splits.Count);
        writer.WriteUInt16(RunsStarted);
        writer.WriteUInt16(RunsFinished);
        writer.WriteSingle(BestTime);
        writer.WriteSingle(TimeToMostZones);
        writer.WriteByte(MostZones);
        writer.WriteByte(CurrentZones);
        writer.WriteUInt16(0);
        for (int i = 0; i < MaxZones; i++)
            writer.WriteSingle(i < splits.Count ? splits[i] : 0f);

        return writer.Close();
    }

    /// <inheritdoc/>
    public bool Equals(RaceStandingMessage other)
    {
        if (Seat != other.Seat || Window != other.Window || InRun != other.InRun || Completed != other.Completed || Left != other.Left
            || RunsStarted != other.RunsStarted || RunsFinished != other.RunsFinished
            || !BestTime.Equals(other.BestTime) || !TimeToMostZones.Equals(other.TimeToMostZones)
            || MostZones != other.MostZones || CurrentZones != other.CurrentZones
            || Splits.Count != other.Splits.Count)
        {
            return false;
        }

        for (int i = 0; i < Splits.Count; i++)
        {
            if (!Splits[i].Equals(other.Splits[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RaceStandingMessage other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Seat, Window, RunsStarted, RunsFinished, Splits.Count);
}
