using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>How a Dogfight lobby's match is won: by the clock, by a score, or by whichever of the
/// two is reached first.</summary>
public enum DogfightVictory : byte
{
    /// <summary>The match runs for a set number of minutes.</summary>
    Time = 0,

    /// <summary>The match ends when a pilot, or in a team match a team, reaches a set score.</summary>
    Score = 1,

    /// <summary>Both limits stand, and the first one reached ends the match. The original's two
    /// radios arm one at a time; the remake arms both.</summary>
    Both = 2,
}

/// <summary>
/// A Dogfight host's Mission Options as one guest reads them, under the round they belong to. They
/// are the environment, the mission type, the victory condition, Restrict Number of Teams with its
/// minimum and maximum, and the lives rule. Capture the Flag adds its own-flag-home rule. The host
/// sends one to each guest whenever an option moves. Kept in the lobby and never passed to a session.
/// </summary>
public readonly record struct DogfightOptionsMessage(
    byte Epoch, byte Environment, byte MissionType, DogfightVictory Victory, byte TimeMinutes,
    ushort Score, bool LimitedLives, byte Lives, bool AutoRespawn,
    bool RestrictTeams = false, byte MinTeams = DogfightOptionsMessage.DefaultMinTeams,
    byte MaxTeams = DogfightOptionsMessage.DefaultMaxTeams, bool FlagHomeToCapture = false)
    : INetMessage<DogfightOptionsMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 12;

    /// <summary>The minimum team count box's opening value, the script's <c>SBA.XF</c>.</summary>
    public const byte DefaultMinTeams = 2;

    /// <summary>The maximum team count box's opening value, the script's <c>TBA.XF</c>.</summary>
    public const byte DefaultMaxTeams = 4;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.DogfightOptions;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out DogfightOptionsMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte epoch = reader.ReadByte();
        byte environment = reader.ReadByte();
        byte type = reader.ReadByte();
        byte flags = reader.ReadByte();
        byte minutes = reader.ReadByte();
        byte lives = reader.ReadByte();
        ushort score = reader.ReadUInt16();
        byte minTeams = reader.ReadByte();
        byte maxTeams = reader.ReadByte();
        var victory = (flags & 8) != 0 ? DogfightVictory.Both : (flags & 1) != 0 ? DogfightVictory.Score : DogfightVictory.Time;
        message = new DogfightOptionsMessage(epoch, environment, type, victory, minutes, score,
            (flags & 2) != 0, lives, (flags & 4) != 0, (flags & 16) != 0, minTeams, maxTeams, (flags & 32) != 0);
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Epoch);
        writer.WriteByte(Environment);
        writer.WriteByte(MissionType);
        int flags = (Victory == DogfightVictory.Score ? 1 : 0) | (LimitedLives ? 2 : 0) | (AutoRespawn ? 4 : 0)
            | (Victory == DogfightVictory.Both ? 8 : 0) | (RestrictTeams ? 16 : 0) | (FlagHomeToCapture ? 32 : 0);
        writer.WriteByte((byte)flags);
        writer.WriteByte(TimeMinutes);
        writer.WriteByte(Lives);
        writer.WriteUInt16(Score);
        writer.WriteByte(MinTeams);
        writer.WriteByte(MaxTeams);
        writer.WriteUInt16(0);
        return writer.Close();
    }
}

/// <summary>One row of a Dogfight lobby's player list: the pilot's name, its stock airframe, its
/// Ready and whether it is the host. It also names the team the pilot is on (0 for none) and whether
/// it captains that team.</summary>
public readonly record struct DogfightLobbySeat(string Name, byte Airframe, bool Ready, bool IsHost, byte Team = 0, bool Captain = false);

/// <summary>
/// A Dogfight host's player list as one guest reads it, under the round it belongs to. The host
/// sends the whole list to each guest whenever it changes, with that guest's own row marked. Fixed
/// width, since a lobby list is short and sent rarely. Kept in the lobby.
/// </summary>
public readonly struct DogfightRosterMessage : INetMessage<DogfightRosterMessage>, IEquatable<DogfightRosterMessage>
{
    /// <summary>The most rows the list carries, the widest field a Dogfight seats.</summary>
    public const int MaxRows = NetSeats.MaxPlayers;

    /// <summary>The bytes one row takes.</summary>
    public const int RowSize = 4 + NameBytes;

    /// <summary>How many name bytes a row carries, the roster's own callsign width.</summary>
    public const int NameBytes = SeatRosterMessage.CallsignBytes;

    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4 + (RowSize * MaxRows);

    private readonly DogfightLobbySeat[] _rows;

    /// <summary>The list <paramref name="rows"/> under round <paramref name="epoch"/>, read by the
    /// guest at row <paramref name="you"/>.</summary>
    public DogfightRosterMessage(byte epoch, byte you, IReadOnlyList<DogfightLobbySeat> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Epoch = epoch;
        You = you;
        int count = Math.Min(rows.Count, MaxRows);
        _rows = new DogfightLobbySeat[count];
        for (int i = 0; i < count; i++)
            _rows[i] = rows[i];
    }

    // The reader's own array, taken as it stands rather than copied a second time.
    private DogfightRosterMessage(DogfightLobbySeat[] rows, byte epoch, byte you)
    {
        Epoch = epoch;
        You = you;
        _rows = rows;
    }

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.DogfightRoster;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <summary>The round the list's Ready marks belong to.</summary>
    public byte Epoch { get; }

    /// <summary>The row that is the reading guest's own.</summary>
    public byte You { get; }

    /// <summary>The rows in player order, the host first.</summary>
    public IReadOnlyList<DogfightLobbySeat> Rows => _rows ?? Array.Empty<DogfightLobbySeat>();

    /// <summary>Whether two lists name the same rows under the same round.</summary>
    public static bool operator ==(DogfightRosterMessage left, DogfightRosterMessage right) => left.Equals(right);

    /// <summary>Whether two lists differ.</summary>
    public static bool operator !=(DogfightRosterMessage left, DogfightRosterMessage right) => !left.Equals(right);

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out DogfightRosterMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte epoch = reader.ReadByte();
        int count = Math.Min((int)reader.ReadByte(), MaxRows);
        byte you = reader.ReadByte();
        _ = reader.ReadByte();
        var rows = new DogfightLobbySeat[count];
        for (int i = 0; i < count; i++)
        {
            byte flags = reader.ReadByte();
            byte airframe = reader.ReadByte();
            byte team = reader.ReadByte();
            _ = reader.ReadByte();
            rows[i] = new DogfightLobbySeat(reader.ReadText(NameBytes), airframe, (flags & 1) != 0, (flags & 2) != 0,
                team, (flags & 4) != 0);
        }

        message = new DogfightRosterMessage(rows, epoch, you);
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var rows = Rows;
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Epoch);
        writer.WriteByte((byte)rows.Count);
        writer.WriteByte(You);
        writer.WriteByte(0);
        for (int i = 0; i < MaxRows; i++)
        {
            var row = i < rows.Count ? rows[i] : default;
            writer.WriteByte((byte)((row.Ready ? 1 : 0) | (row.IsHost ? 2 : 0) | (row.Captain ? 4 : 0)));
            writer.WriteByte(row.Airframe);
            writer.WriteByte(row.Team);
            writer.WriteByte(0);
            writer.WriteText(row.Name ?? "", NameBytes);
        }

        return writer.Close();
    }

    /// <inheritdoc/>
    public bool Equals(DogfightRosterMessage other)
    {
        if (Epoch != other.Epoch || You != other.You || Rows.Count != other.Rows.Count)
            return false;

        for (int i = 0; i < Rows.Count; i++)
        {
            if (Rows[i] != other.Rows[i])
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DogfightRosterMessage other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Epoch, You, Rows.Count);
}

/// <summary>
/// One line of lobby chat and the name of the pilot who typed it. A guest sends its line to the
/// host, and the host relays it to every other guest under the same name. Kept in the lobby, so a
/// line never reaches a session.
/// </summary>
public readonly record struct LobbyChatMessage(string Name, string Text) : INetMessage<LobbyChatMessage>
{
    /// <summary>The longest line a player may type, the original's own edit box width.</summary>
    public const int MaxChars = 80;

    /// <summary>How many name bytes the message carries.</summary>
    public const int NameBytes = SeatRosterMessage.CallsignBytes;

    /// <summary>How many text bytes the message carries, room for a typed line in UTF-8.</summary>
    public const int TextBytes = 84;

    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + NameBytes + TextBytes;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.LobbyChat;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out LobbyChatMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        string name = reader.ReadText(NameBytes);
        message = new LobbyChatMessage(name, reader.ReadText(TextBytes));
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteText(Name ?? "", NameBytes);
        writer.WriteText(Text ?? "", TextBytes);
        return writer.Close();
    }
}

/// <summary>
/// A guest's team action, sent to its host alone, which acts on it or ignores it. It is the
/// original's <c>0x1a</c> team subtypes minted anew: the action, the team it names (join) and the
/// name a new team takes (create). The guest learns the outcome from the player list and the team
/// list, never from its own word. Kept in the lobby.
/// </summary>
public readonly record struct LobbyTeamActionMessage(NetTeamAction Action, byte Team, string Name)
    : INetMessage<LobbyTeamActionMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4 + LobbyTeamsMessage.NameBytes;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.LobbyTeamAction;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out LobbyTeamActionMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        var action = (NetTeamAction)reader.ReadByte();
        byte team = reader.ReadByte();
        _ = reader.ReadUInt16();
        message = new LobbyTeamActionMessage(action, team, reader.ReadText(LobbyTeamsMessage.NameBytes));
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)Action);
        writer.WriteByte(Team);
        writer.WriteUInt16(0);
        writer.WriteText(Name ?? "", LobbyTeamsMessage.NameBytes);
        return writer.Close();
    }
}

/// <summary>One team as a guest's list reads it: its number from 1 and its name.</summary>
public readonly record struct LobbyTeamName(byte Number, string Name);

/// <summary>
/// A Dogfight host's teams as one guest reads them: each team's number and name, in the order they
/// were created. Membership rides the player list's rows. It stands in for the original's
/// <c>0x27</c> team roster, whose id is the remake's seat roster. The host sends it to each guest
/// whenever it changes. Fixed width. Kept in the lobby.
/// </summary>
public readonly struct LobbyTeamsMessage : INetMessage<LobbyTeamsMessage>, IEquatable<LobbyTeamsMessage>
{
    /// <summary>The most teams the list carries, one per seat.</summary>
    public const int MaxTeams = NetTeamBook.MaxTeams;

    /// <summary>How many name bytes a team takes: the Create Team box's 12 characters in UTF-8 and
    /// the terminator, with room to spare. ⚠ Do not widen the list past 664 bytes; the message fuzz
    /// writes every message into a buffer twice the widest seat roster.</summary>
    public const int NameBytes = 39;

    /// <summary>The bytes one team takes: its number and its name.</summary>
    public const int EntrySize = 1 + NameBytes;

    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4 + (EntrySize * MaxTeams);

    private readonly LobbyTeamName[] _teams;

    /// <summary>The list of <paramref name="teams"/>, cut at <see cref="MaxTeams"/>.</summary>
    public LobbyTeamsMessage(IReadOnlyList<LobbyTeamName> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        int count = Math.Min(teams.Count, MaxTeams);
        _teams = new LobbyTeamName[count];
        for (int i = 0; i < count; i++)
            _teams[i] = teams[i];
    }

    // The reader's own array, taken as it stands.
    private LobbyTeamsMessage(LobbyTeamName[] teams, bool _) => _teams = teams;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.LobbyTeams;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <summary>The teams in the order they were created.</summary>
    public IReadOnlyList<LobbyTeamName> Teams => _teams ?? Array.Empty<LobbyTeamName>();

    /// <summary>Whether two lists name the same teams.</summary>
    public static bool operator ==(LobbyTeamsMessage left, LobbyTeamsMessage right) => left.Equals(right);

    /// <summary>Whether two lists differ.</summary>
    public static bool operator !=(LobbyTeamsMessage left, LobbyTeamsMessage right) => !left.Equals(right);

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out LobbyTeamsMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        int count = Math.Min((int)reader.ReadByte(), MaxTeams);
        _ = reader.ReadByte();
        _ = reader.ReadUInt16();
        var teams = new LobbyTeamName[count];
        for (int i = 0; i < count; i++)
        {
            byte number = reader.ReadByte();
            teams[i] = new LobbyTeamName(number, reader.ReadText(NameBytes));
        }

        message = new LobbyTeamsMessage(teams, true);
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var teams = Teams;
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)teams.Count);
        writer.WriteByte(0);
        writer.WriteUInt16(0);
        for (int i = 0; i < MaxTeams; i++)
        {
            var team = i < teams.Count ? teams[i] : default;
            writer.WriteByte(team.Number);
            writer.WriteText(team.Name ?? "", NameBytes);
        }

        return writer.Close();
    }

    /// <inheritdoc/>
    public bool Equals(LobbyTeamsMessage other)
    {
        if (Teams.Count != other.Teams.Count)
            return false;

        for (int i = 0; i < Teams.Count; i++)
        {
            if (Teams[i] != other.Teams[i])
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LobbyTeamsMessage other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Teams.Count;
}

/// <summary>
/// A Capture the Flag pilot's ask, sent reliably to its host alone. It carries the flag (its team's
/// number), the ask (1 take, 2 home) and the seat asking. The host decides and answers with its
/// <see cref="FlagTableMessage"/>. The original's <c>0x1c</c> with the seat in place of the sender's
/// player id (<c>docs/org/multiplayer-ctf.md</c>).
/// </summary>
public readonly record struct FlagRequestMessage(byte Team, byte Ask, byte Seat) : INetMessage<FlagRequestMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.FlagRequest;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out FlagRequestMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte team = reader.ReadByte();
        byte ask = reader.ReadByte();
        message = new FlagRequestMessage(team, ask, reader.ReadByte());
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Team);
        writer.WriteByte(Ask);
        writer.WriteByte(Seat);
        writer.WriteByte(0);
        return writer.Close();
    }
}

/// <summary>One flag as a <see cref="FlagTableMessage"/> carries it: its team's number, its state
/// (1 held, 2 at home) and the seat holding it, <see cref="NetMessage.NoSeat"/> for none.</summary>
public readonly record struct NetFlagRow(byte Team, byte State, byte Holder);

/// <summary>
/// A Capture the Flag host's flag table, sent reliably to every guest whenever a flag moves. It
/// carries every flag's state and holder, the changed one among them. The original's <c>0x1d</c>.
/// Fixed width.
/// </summary>
public readonly struct FlagTableMessage : INetMessage<FlagTableMessage>, IEquatable<FlagTableMessage>
{
    /// <summary>The most flags the table carries, a map's <c>cs_flag_n</c> nodes (two ship).</summary>
    public const int MaxFlags = 4;

    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 4 + (4 * MaxFlags);

    private readonly NetFlagRow[] _rows;

    /// <summary>The table of <paramref name="rows"/>, cut at <see cref="MaxFlags"/>.</summary>
    public FlagTableMessage(IReadOnlyList<NetFlagRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int count = Math.Min(rows.Count, MaxFlags);
        _rows = new NetFlagRow[count];
        for (int i = 0; i < count; i++)
            _rows[i] = rows[i];
    }

    // The reader's own array, taken as it stands.
    private FlagTableMessage(NetFlagRow[] rows, bool _) => _rows = rows;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.FlagTable;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <summary>Every flag in the table, in team order.</summary>
    public IReadOnlyList<NetFlagRow> Rows => _rows ?? Array.Empty<NetFlagRow>();

    /// <summary>Whether two tables carry the same rows.</summary>
    public static bool operator ==(FlagTableMessage left, FlagTableMessage right) => left.Equals(right);

    /// <summary>Whether two tables differ.</summary>
    public static bool operator !=(FlagTableMessage left, FlagTableMessage right) => !left.Equals(right);

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out FlagTableMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        int count = Math.Min((int)reader.ReadByte(), MaxFlags);
        _ = reader.ReadByte();
        _ = reader.ReadUInt16();
        var rows = new NetFlagRow[count];
        for (int i = 0; i < count; i++)
        {
            byte team = reader.ReadByte();
            byte state = reader.ReadByte();
            rows[i] = new NetFlagRow(team, state, reader.ReadByte());
            _ = reader.ReadByte();
        }

        message = new FlagTableMessage(rows, true);
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var rows = Rows;
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)rows.Count);
        writer.WriteByte(0);
        writer.WriteUInt16(0);
        for (int i = 0; i < MaxFlags; i++)
        {
            var row = i < rows.Count ? rows[i] : default;
            writer.WriteByte(row.Team);
            writer.WriteByte(row.State);
            writer.WriteByte(row.Holder);
            writer.WriteByte(0);
        }

        return writer.Close();
    }

    /// <inheritdoc/>
    public bool Equals(FlagTableMessage other)
    {
        if (Rows.Count != other.Rows.Count)
            return false;

        for (int i = 0; i < Rows.Count; i++)
        {
            if (Rows[i] != other.Rows[i])
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FlagTableMessage other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Rows.Count;
}
