using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>A team action a lobby member asks of its host, by the original's own <c>0x1a</c>
/// subtype values (<c>docs/org/multiplayer-messages.md</c>, "Lobby teams").</summary>
public enum NetTeamAction : byte
{
    /// <summary>Join a team by its number.</summary>
    Join = 1,

    /// <summary>Leave the team the member is on. A captain's leave disbands the team.</summary>
    Leave = 2,

    /// <summary>Create a team under a name, the creator its captain and first member.</summary>
    Create = 4,

    /// <summary>Disband a team: every member leaves it and it is gone.</summary>
    Disband = 8,
}

/// <summary>What one team action did, for the lobby notice it posts.</summary>
public enum NetTeamEventKind
{
    /// <summary>A member joined a team, a creator included (langui 10503).</summary>
    Joined,

    /// <summary>A member left a team (langui 10504).</summary>
    Left,

    /// <summary>A team was disbanded (langui 10505).</summary>
    Disbanded,
}

/// <summary>Why a launch with teams is refused, in the order <see cref="NetTeamBook.Check"/> asks.
/// </summary>
public enum TeamLaunchRefusal
{
    /// <summary>The launch may go.</summary>
    None,

    /// <summary>A team match with fewer than two players (langui 10520).</summary>
    NotEnoughPlayers,

    /// <summary>Fewer teams than Restrict Number of Teams' minimum, or a team match with one team
    /// (langui 10519).</summary>
    TooFewTeams,

    /// <summary>More teams than Restrict Number of Teams' maximum (langui 10518).</summary>
    TooManyTeams,

    /// <summary>A team match with a player on no team. The remake's own refusal.</summary>
    Teamless,

    /// <summary>Two teams whose sizes differ by more than one player. The remake's own refusal.
    /// </summary>
    Unbalanced,
}

/// <summary>One thing a team action did: who, to which team, and how. A disband names no member.
/// </summary>
public readonly record struct NetTeamEvent(NetTeamEventKind Kind, int Member, byte Team, string TeamName);

/// <summary>One team as its host holds it. It carries its number from 1, its name and its captain.
/// Its members stand in the order they joined, the captain first.</summary>
public sealed record NetTeam(byte Number, string Name, int Captain, IReadOnlyList<int> Members);

/// <summary>
/// A host's teams, engine-free: free-form named teams any lobby member creates, joins and leaves,
/// as the original's lobby forms them (<c>docs/org/multiplayer-messages.md</c>, "Lobby teams"). A
/// member is whatever key the caller seats a player by, a peer in the Dogfight lobby. Only the host
/// holds a book; its guests learn the teams from the lobby's team list. Every Dogfight team mode reads
/// the same book, so a mode adds its rules on top rather than a book of its own. Each action returns
/// what it did, which is the lobby's notice.
/// </summary>
public sealed class NetTeamBook
{
    /// <summary>How many characters of a team's name are kept, the original's own 16.</summary>
    public const int NameChars = 16;

    /// <summary>The most teams a lobby holds, one per seat, the original's 16-entry team array.
    /// </summary>
    public const int MaxTeams = NetSeats.SeatCapacity;

    /// <summary>The name a blank one becomes, the original's own at <c>0x61f614</c>.</summary>
    public const string DefaultName = "Default team name";

    private readonly List<Team> _teams = new();

    /// <summary>The teams in the order they were created.</summary>
    public IReadOnlyList<NetTeam> Teams
    {
        get
        {
            var teams = new NetTeam[_teams.Count];
            for (int i = 0; i < teams.Length; i++)
            {
                var team = _teams[i];
                teams[i] = new NetTeam(team.Number, team.Name, team.Members[0], team.Members.ToArray());
            }

            return teams;
        }
    }

    /// <summary>How many teams stand.</summary>
    public int Count => _teams.Count;

    /// <summary>A team's name as the book keeps it: trimmed, cut to <see cref="NameChars"/>, and
    /// <see cref="DefaultName"/> for a blank one.</summary>
    public static string Kept(string? name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length > NameChars)
        {
            trimmed = trimmed[..NameChars].TrimEnd();
        }

        return trimmed.Length > 0 ? trimmed : DefaultName;
    }

    /// <summary>Whether a launch of <paramref name="players"/> may go, each a team number (0 for
    /// none) and the seats it flies. A player's splitscreen seats count as players on its team.
    /// Restrict Number of Teams, when <paramref name="restrict"/>, bounds the team count by
    /// <paramref name="min"/> and <paramref name="max"/>. With no team standing the match is a
    /// free-for-all, refused only by a restrict whose minimum is above zero.</summary>
    public static TeamLaunchRefusal Check(IReadOnlyList<(byte Team, int Seats)> players, bool restrict, int min, int max)
    {
        ArgumentNullException.ThrowIfNull(players);
        var sizes = new Dictionary<byte, int>();
        int total = 0;
        bool teamless = false;
        foreach (var (team, seats) in players)
        {
            int weight = Math.Max(1, seats);
            total += weight;
            if (team == 0)
            {
                teamless = true;
                continue;
            }

            sizes[team] = sizes.TryGetValue(team, out int size) ? size + weight : weight;
        }

        // The Mission Options' own check at LAUNCH!, before any other (MULTIPLAYERLOBBY_MISSION.SCRIPT).
        if (restrict && sizes.Count < min)
        {
            return TeamLaunchRefusal.TooFewTeams;
        }

        if (restrict && sizes.Count > max)
        {
            return TeamLaunchRefusal.TooManyTeams;
        }

        if (sizes.Count == 0)
        {
            return TeamLaunchRefusal.None;
        }

        if (total < 2)
        {
            return TeamLaunchRefusal.NotEnoughPlayers;
        }

        if (sizes.Count < 2)
        {
            return TeamLaunchRefusal.TooFewTeams;
        }

        if (teamless)
        {
            return TeamLaunchRefusal.Teamless;
        }

        int smallest = int.MaxValue, largest = 0;
        foreach (int size in sizes.Values)
        {
            smallest = Math.Min(smallest, size);
            largest = Math.Max(largest, size);
        }

        return largest - smallest > 1 ? TeamLaunchRefusal.Unbalanced : TeamLaunchRefusal.None;
    }

    /// <summary>The team <paramref name="member"/> is on, 0 for none.</summary>
    public byte TeamOf(int member) => FindMember(member)?.Number ?? 0;

    /// <summary>Whether <paramref name="member"/> captains a team.</summary>
    public bool IsCaptain(int member) => FindMember(member) is { } team && team.Members[0] == member;

    /// <summary>The team known by <paramref name="number"/>, or null.</summary>
    public NetTeam? Find(byte number)
    {
        foreach (var team in Teams)
        {
            if (team.Number == number)
            {
                return team;
            }
        }

        return null;
    }

    /// <summary>A new team under <paramref name="name"/> (<see cref="Kept"/>), its creator the
    /// captain and first member. It takes the lowest number no team holds, from 1, as the original's
    /// <c>FUN_0046f250</c> mints it. A creator already on a team leaves that one first. Nothing
    /// happens once <see cref="MaxTeams"/> stand.</summary>
    public IReadOnlyList<NetTeamEvent> Create(int member, string? name)
    {
        var events = new List<NetTeamEvent>();
        if (_teams.Count >= MaxTeams)
        {
            return events;
        }

        events.AddRange(Leave(member));
        byte number = 1;
        while (FindNumber(number) != null)
        {
            number++;
        }

        var team = new Team(number, Kept(name));
        team.Members.Add(member);
        _teams.Add(team);
        events.Add(new NetTeamEvent(NetTeamEventKind.Joined, member, number, team.Name));
        return events;
    }

    /// <summary><paramref name="member"/> joins team <paramref name="number"/>. Nothing happens for a
    /// team that does not stand. Nor for a member already on a team, whom the lobby offers only Leave
    /// Team.</summary>
    public IReadOnlyList<NetTeamEvent> Join(int member, byte number)
    {
        if (FindNumber(number) is not { } team || FindMember(member) != null)
        {
            return Array.Empty<NetTeamEvent>();
        }

        team.Members.Add(member);
        return new[] { new NetTeamEvent(NetTeamEventKind.Joined, member, number, team.Name) };
    }

    /// <summary><paramref name="member"/> leaves its team. A captain's leave disbands it, as the
    /// original's <c>FUN_00413390</c> does. Nothing happens for a member on no team.</summary>
    public IReadOnlyList<NetTeamEvent> Leave(int member)
    {
        if (FindMember(member) is not { } team)
        {
            return Array.Empty<NetTeamEvent>();
        }

        if (team.Members[0] == member)
        {
            return Disband(team.Number);
        }

        team.Members.Remove(member);
        return new[] { new NetTeamEvent(NetTeamEventKind.Left, member, team.Number, team.Name) };
    }

    /// <summary>Disbands team <paramref name="number"/>: every member leaves it, the captain last,
    /// then it is gone, the original's subtype 8.</summary>
    public IReadOnlyList<NetTeamEvent> Disband(byte number)
    {
        if (FindNumber(number) is not { } team)
        {
            return Array.Empty<NetTeamEvent>();
        }

        var events = new List<NetTeamEvent>();
        for (int i = team.Members.Count - 1; i >= 0; i--)
        {
            events.Add(new NetTeamEvent(NetTeamEventKind.Left, team.Members[i], number, team.Name));
        }

        _teams.Remove(team);
        events.Add(new NetTeamEvent(NetTeamEventKind.Disbanded, -1, number, team.Name));
        return events;
    }

    /// <summary>Takes out every member not in <paramref name="present"/>, a leaver or a booted
    /// player. A captain's going disbands its team, as a boot does in the original's
    /// <c>FUN_00413090</c>.</summary>
    public IReadOnlyList<NetTeamEvent> Keep(IReadOnlyCollection<int> present)
    {
        ArgumentNullException.ThrowIfNull(present);
        var events = new List<NetTeamEvent>();
        var gone = new List<int>();
        foreach (var team in _teams)
        {
            foreach (int member in team.Members)
            {
                if (!Contains(present, member))
                {
                    gone.Add(member);
                }
            }
        }

        foreach (int member in gone)
        {
            events.AddRange(Leave(member));
        }

        return events;
    }

    /// <summary>Every team gone, for a lobby that closes.</summary>
    public void Clear() => _teams.Clear();

    private static bool Contains(IReadOnlyCollection<int> present, int member)
    {
        foreach (int kept in present)
        {
            if (kept == member)
            {
                return true;
            }
        }

        return false;
    }

    private Team? FindNumber(byte number)
    {
        foreach (var team in _teams)
        {
            if (team.Number == number)
            {
                return team;
            }
        }

        return null;
    }

    private Team? FindMember(int member)
    {
        foreach (var team in _teams)
        {
            if (team.Members.Contains(member))
            {
                return team;
            }
        }

        return null;
    }

    private sealed class Team
    {
        public Team(byte number, string name)
        {
            Number = number;
            Name = name;
        }

        public byte Number { get; }

        public string Name { get; }

        public List<int> Members { get; } = new();
    }
}
