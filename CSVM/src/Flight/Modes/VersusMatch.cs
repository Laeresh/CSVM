using System;
using System.Collections.Generic;

namespace CSVM.Flight.Modes;

/// <summary>One player's ranked line in <see cref="VersusMatch.Standings"/>: score, kills, deaths,
/// and a competition rank (tied scores share a rank; the next distinct score skips ahead by the
/// number of players it passed), enough for a results board to pick out the winner (rank 1, no
/// tie) or draw (rank 1, tied) and render score + kills + deaths per row.</summary>
public readonly record struct VersusStanding(int PlayerIndex, int Kills, int Deaths, int Rank, int Score);

/// <summary>One team's ranked line in <see cref="VersusMatch.TeamStandings"/>: its lobby team
/// number and name, and its members' summed score, kills and deaths. It is ranked as
/// <see cref="VersusStanding"/> ranks a player.</summary>
public readonly record struct VersusTeamStanding(int Team, string Name, int Kills, int Deaths, int Rank, int Score);

/// <summary>
/// Deathmatch scorekeeping for splitscreen "Dogfight": per-player kills and deaths plus the match
/// clock, host-fed exactly like <see cref="StuntRace"/>. A caller reports facts
/// (<see cref="RegisterKill"/>, <see cref="RegisterDeath"/>, <see cref="Advance"/>) and this class
/// turns them into standings and one completion event. It completes once, on the first of
/// <see cref="KillTarget"/>, <see cref="TimeLimit"/> (either disabled at 0) and
/// <see cref="AllAlone"/>, fewer than two pilots left with lives. It is not a Node and is freed with
/// the session; <see cref="AssignTeams"/> makes it a team match (docs/org/multiplayer-scoring.md,
/// "Teams").
/// ⚠ Zero engine dependency of any kind, not even <c>Log</c>, a
/// <c>GD.Print</c> in this family once crashed the xUnit host. Keep it engine-free by construction.</summary>
public sealed class VersusMatch
{
    private readonly Row[] _scores;
    private readonly Dictionary<int, string> _teamNames = new();
    private readonly Dictionary<int, int> _teamBonus = new();

    /// <summary>A match of <paramref name="playerCount"/> seats scored by <paramref name="scores"/>,
    /// <see cref="MatchScores.Fallback"/> when none is given.</summary>
    public VersusMatch(int playerCount, int killTarget = 5, float timeLimit = 300f, int lives = 0,
        MatchScores? scores = null)
    {
        playerCount = Math.Max(1, playerCount); // 2-4 in practice; a solo session still scores
        KillTarget = Math.Max(0, killTarget);
        TimeLimit = Math.Max(0f, timeLimit);
        Lives = Math.Max(0, lives);
        Scores = scores ?? MatchScores.Fallback;
        _scores = new Row[playerCount];
        for (int i = 0; i < playerCount; i++)
            _scores[i] = new Row();
    }

    /// <summary>Fired exactly once, the moment the match completes, the results board's cue.</summary>
    public event Action? MatchCompleted;

    /// <summary>How many players are being scored.</summary>
    public int PlayerCount => _scores.Length;

    /// <summary>What each scoring event is worth in this match, the same on every machine since
    /// every machine reads the same <c>player.zrd</c>.</summary>
    public MatchScores Scores { get; }

    /// <summary>The score that ends the match, 0 = no kill target (time is then the only way to
    /// end it). Named for the menu row that sets it. The original compares that row against a
    /// running score, so suicides push a pilot back from it. A replicated match takes this from
    /// the host, whose lobby row is the only one in the match.</summary>
    public int KillTarget { get; private set; }

    /// <summary>Seconds on the match clock, 0 = no time limit (kills are then the only way to end it).</summary>
    public float TimeLimit { get; private set; }

    /// <summary>How many deaths a pilot has before it stays down for the rest of the match, 0 for
    /// no limit. The lobby's Limited Lives box; only the lobby's options message carries it, since
    /// every machine launches with the same lobby rules.</summary>
    public int Lives { get; }

    /// <summary>True on a guest: the clock, the limits and the ending arrive from the host through
    /// <see cref="ApplyState"/> and nothing local writes them. Scores still arrive as scores, so
    /// the standings are derived here rather than sent.</summary>
    public bool Replicated { get; private set; }

    /// <summary>Seconds of match clock consumed so far via <see cref="Advance"/>. Stops moving once
    /// <see cref="Completed"/>, a completed match's clock is frozen for display.</summary>
    public float Elapsed { get; private set; }

    /// <summary>Seconds left before a time-limited match times out, 0 once reached or when
    /// <see cref="TimeLimit"/> is disabled.</summary>
    public float TimeRemaining => TimeLimit > 0f ? Math.Max(0f, TimeLimit - Elapsed) : 0f;

    /// <summary>True once the match has ended (threshold or time-out), every further
    /// <see cref="RegisterKill"/>/<see cref="RegisterDeath"/>/<see cref="Advance"/> call is a no-op.</summary>
    public bool Completed { get; private set; }

    /// <summary>True when the match ended because fewer than two pilots with lives were left, the
    /// original's end reason 4 "No Enemies Left" (docs/org/multiplayer-scoring.md). In a
    /// <see cref="Teamed"/> match, fewer than two teams.</summary>
    public bool AllAlone { get; private set; }

    /// <summary>True once <see cref="AssignTeams"/> put some seat on a lobby team. The original's
    /// team flag: the score target is then read against a team's total and the per-pilot check is
    /// skipped (docs/org/multiplayer-scoring.md, "Teams").</summary>
    public bool Teamed { get; private set; }

    /// <summary>The lobby team that won on a mode's objective, 0 for a match not ended that way. It is
    /// the Zeppelin vs Zeppelin side whose hull survived, the original's end reason 3.</summary>
    public int ObjectiveWinner { get; private set; }

    /// <summary>The seat an out-of-lives pilot watches. It keeps the one it watches while that one
    /// flies, else takes the next flying seat after <paramref name="self"/>, else none. The
    /// original binds its chase camera to the next aircraft still in flight.</summary>
    public static int? NextWatched(int self, IReadOnlyList<bool> flying, int? current)
    {
        if (current is { } now && now != self && now >= 0 && now < flying.Count && flying[now])
            return now;
        for (int step = 1; step < flying.Count; step++)
        {
            int seat = (self + step) % flying.Count;
            if (flying[seat])
                return seat;
        }
        return null;
    }

    /// <summary>Puts each seat on its lobby team, 0 for none, and names the teams. A lobby team
    /// number is never a hostility id (CONTEXT.md, "Lobby team"). Every machine assigns the same
    /// teams from the seat roster, so a guest derives the same totals from the scores it is sent.
    /// </summary>
    public void AssignTeams(IReadOnlyList<int> teams, IReadOnlyDictionary<int, string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(teams);
        Teamed = false;
        for (int i = 0; i < _scores.Length; i++)
        {
            _scores[i].Team = i < teams.Count ? Math.Max(0, teams[i]) : 0;
            Teamed |= _scores[i].Team > 0;
        }

        _teamNames.Clear();
        if (names != null)
        {
            foreach (var (team, name) in names)
                _teamNames[team] = name;
        }
    }

    /// <summary>The lobby team <paramref name="playerIndex"/> flies on, 0 for none.</summary>
    public int TeamOf(int playerIndex) => RowOf(playerIndex)?.Team ?? 0;

    /// <summary>A team's name as its lobby named it, else "Team" and its number.</summary>
    public string TeamName(int team) =>
        _teamNames.TryGetValue(team, out var name) && name.Length > 0
            ? name
            : "Team " + team.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The sum of a team's members' scores, the number its score target is read against.
    /// </summary>
    public int TeamScoreOf(int team)
    {
        int total = 0;
        foreach (var row in _scores)
        {
            if (team > 0 && row.Team == team)
                total += row.Score;
        }

        return total;
    }

    /// <summary>A team's total as the board shows it: <see cref="TeamScoreOf"/> plus the team's own
    /// term, which only Zeppelin vs Zeppelin writes (<see cref="EndOnHullLoss"/>,
    /// <see cref="RegisterZeppelinKill"/>).</summary>
    public int TeamTotalOf(int team) => TeamScoreOf(team) + TeamTermOf(team);

    /// <summary>What a team was given as a team, the original's team <c>+0x14</c> term. ⚠ Never add
    /// it to <see cref="TeamScoreOf"/>: the Score limit does not read it
    /// (docs/org/multiplayer-zvz.md).</summary>
    public int TeamTermOf(int team) => _teamBonus.TryGetValue(team, out int term) ? term : 0;

    /// <summary>Zeppelin vs Zeppelin's end: <paramref name="losingTeam"/>'s hull is gone, every other
    /// team's term takes <see cref="MatchScores.HullLoss"/> and <paramref name="winningTeam"/> wins
    /// (end reason 3). Once per match. A replicated match takes it from the host's state and
    /// completes there.</summary>
    public void EndOnHullLoss(int losingTeam, int winningTeam)
    {
        if (ObjectiveWinner != 0 || (Completed && !Replicated))
            return;
        ObjectiveWinner = Math.Max(1, winningTeam);
        foreach (int team in TeamNumbers())
        {
            if (team != losingTeam)
                _teamBonus[team] = TeamTermOf(team) + Scores.HullLoss;
        }

        if (!Replicated)
            Complete();
    }

    /// <summary>The original's event 9, a pilot downed by the zeppelin of <paramref name="team"/>.
    /// The team's term is set to <see cref="MatchScores.ZeppelinKill"/> rather than added to, so
    /// only the first such kill moves it. The victim takes a death and no score. A replicated match sets the term
    /// alone, its deaths arriving as the host's scores. No-op once <see cref="Completed"/>.</summary>
    public void RegisterZeppelinKill(int victim, int team)
    {
        if (Completed)
            return;
        if (team > 0)
            _teamBonus[team] = Scores.ZeppelinKill;
        if (Replicated || RowOf(victim) is not { } victimRow)
            return;
        victimRow.Deaths++;
        CheckAlone();
    }

    /// <summary>Every team some seat flies on, ranked by <see cref="TeamTotalOf"/> as
    /// <see cref="Standings"/> ranks players, equal totals sharing a rank and in team number order.
    /// Empty unless <see cref="Teamed"/>.</summary>
    public IEnumerable<VersusTeamStanding> TeamStandings()
    {
        var lines = new List<VersusTeamStanding>();
        foreach (int team in TeamNumbers())
        {
            int kills = 0, deaths = 0;
            foreach (var row in _scores)
            {
                if (row.Team != team)
                    continue;
                kills += row.Kills;
                deaths += row.Deaths;
            }

            lines.Add(new VersusTeamStanding(team, TeamName(team), kills, deaths, 0, TeamTotalOf(team)));
        }

        // Insertion order is team number order, and a stable sort keeps it among equal totals.
        var ranked = new List<VersusTeamStanding>(lines.Count);
        foreach (var line in lines)
        {
            int at = ranked.Count;
            while (at > 0 && ranked[at - 1].Score < line.Score)
                at--;
            ranked.Insert(at, line);
        }

        int rank = 0;
        int lastScore = int.MinValue;
        for (int i = 0; i < ranked.Count; i++)
        {
            if (ranked[i].Score != lastScore)
            {
                rank = i + 1;
                lastScore = ranked[i].Score;
            }

            yield return ranked[i] with { Rank = rank };
        }
    }

    public int KillsOf(int playerIndex) => RowOf(playerIndex)?.Kills ?? 0;

    public int DeathsOf(int playerIndex) => RowOf(playerIndex)?.Deaths ?? 0;

    /// <summary>Whether <paramref name="playerIndex"/> has spent its last life. Its deaths are the
    /// host's count, so every machine reads the same answer once the score arrives.</summary>
    public bool OutOfLives(int playerIndex) => Lives > 0 && DeathsOf(playerIndex) >= Lives;

    /// <summary>The ranked number: <see cref="Scores"/>' kill value per kill plus its suicide value
    /// per death with no killer, plus a mode's own points (<see cref="AddScore"/>). May go negative.
    /// </summary>
    public int ScoreOf(int playerIndex) => RowOf(playerIndex)?.Score ?? 0;

    /// <summary>Score still needed by <paramref name="playerIndex"/> to hit the threshold, 0 once
    /// there or when <see cref="KillTarget"/> is disabled. In a <see cref="Teamed"/> match it is
    /// its team's total that must reach it.</summary>
    public int KillsRemaining(int playerIndex) =>
        KillTarget > 0 ? Math.Max(0, KillTarget - Counted(playerIndex)) : 0;

    /// <summary>A weapon kill: +1 kill and <see cref="MatchScores.Kill"/> to the shooter, or
    /// <see cref="MatchScores.TurretKill"/> to a <paramref name="turret"/>'s owner, +1 death to the
    /// victim. Completes the match once the shooter's score reaches <see cref="KillTarget"/>.
    /// ⚠ A teammate's kill counts no kill and costs the shooter <see cref="MatchScores.Suicide"/>:
    /// the original charges <c>score_suicide</c> to a killer on the victim's team slot.
    /// No-op once <see cref="Completed"/>.</summary>
    public void RegisterKill(int shooter, int victim, bool turret = false)
    {
        if (Completed)
            return;
        var shooterRow = RowOf(shooter);
        bool teammate = shooter != victim && shooterRow is { Team: > 0 } && shooterRow.Team == TeamOf(victim);
        if (shooterRow != null && teammate)
        {
            shooterRow.Score += Scores.Suicide;
        }
        else if (shooterRow != null)
        {
            shooterRow.Kills++;
            shooterRow.Score += turret ? Scores.TurretKill : Scores.Kill;
        }
        var victimRow = RowOf(victim);
        if (victimRow != null)
            victimRow.Deaths++;
        if (Reached(shooter))
            Complete();
        CheckAlone();
    }

    /// <summary>A death with no killer, terrain or mid-air: +1 death and <see cref="MatchScores.Suicide"/>
    /// to the pilot who died, the original's own penalty. Never completes the match by itself, a
    /// falling score cannot reach the target. No-op once <see cref="Completed"/>.</summary>
    public void RegisterDeath(int victim)
    {
        if (Completed)
            return;
        var victimRow = RowOf(victim);
        if (victimRow != null)
        {
            victimRow.Deaths++;
            victimRow.Score += Scores.Suicide;
        }
        CheckAlone();
    }

    /// <summary>Points a mode's own event scores <paramref name="playerIndex"/>, a flag brought home
    /// in Capture the Flag. Completes the match once the seat's count reaches
    /// <see cref="KillTarget"/>, as a kill does. No-op once <see cref="Completed"/>.</summary>
    public void AddScore(int playerIndex, int points)
    {
        if (Completed || RowOf(playerIndex) is not { } row)
            return;
        row.Score += points;
        if (Reached(playerIndex))
            Complete();
    }

    /// <summary>A pilot who dropped out of the session. It stays on the scoreboard but no longer
    /// counts as an opponent. The drop can end the match on reason 4 as a death can.</summary>
    public void Leave(int playerIndex)
    {
        if (RowOf(playerIndex) is not { Left: false } row)
            return;
        row.Left = true;
        CheckAlone();
    }

    /// <summary>Writes one player's line as the host reports it, in place of deriving it from
    /// events. A guest scores nothing of its own. It shows what the host counted, so a board reads
    /// the same on every machine whatever each of them saw. The completion test runs as it does
    /// after a kill, and a host that has ended the match sends nothing more.</summary>
    public void ApplyScore(int playerIndex, int score, int kills, int deaths)
    {
        if (Completed)
            return;
        var row = RowOf(playerIndex);
        if (row == null)
            return;
        row.Score = score;
        row.Kills = kills;
        row.Deaths = deaths;
        if (Reached(playerIndex))
            Complete();
    }

    /// <summary>Advance the host-fed match clock by <paramref name="dt"/> seconds; completes the
    /// match once it reaches <see cref="TimeLimit"/>. No-op once <see cref="Completed"/>, when
    /// <see cref="TimeLimit"/> is disabled (an untimed match never times out), or on a
    /// <see cref="Replicated"/> match, whose clock is the host's.</summary>
    public void Advance(float dt)
    {
        if (Completed || Replicated || TimeLimit <= 0f)
            return;
        Elapsed += dt;
        if (Elapsed >= TimeLimit)
            Complete();
    }

    /// <summary>Hands the clock, the limits and the ending to a remote host. From here
    /// <see cref="Advance"/> moves nothing and no score can end the match. One way: a match never
    /// takes them back.</summary>
    public void Replicate() => Replicated = true;

    /// <summary>The host's match state, applied whole on a <see cref="Replicated"/> match: both
    /// its limits, its clock, and whether the round has ended. ⚠ This is the only thing that ends
    /// one, so a guest that reached the target itself flies on until the host says otherwise. An
    /// <paramref name="ended"/> of false re-arms a completed match, which is the host's rematch,
    /// and leaves the scores for the host to rewrite. Ignored on a host, the only writer of its
    /// own match.</summary>
    public void ApplyState(int killTarget, float timeLimit, float remainingSeconds, bool ended)
    {
        if (!Replicated)
            return;
        KillTarget = Math.Max(0, killTarget);
        TimeLimit = Math.Max(0f, timeLimit);
        if (TimeLimit > 0f)
            Elapsed = Math.Clamp(TimeLimit - remainingSeconds, 0f, TimeLimit);
        if (ended)
        {
            Complete();
        }
        else if (Completed)
        {
            // ⚠ Re-arm only a completed match. Every running tick says ended = false, and a clear
            // there would wipe a team's term as soon as the next tick landed.
            Completed = false;
            ObjectiveWinner = 0;
            _teamBonus.Clear();
        }
    }

    /// <summary>Rematch: every score/kill/death zeroed, the clock back to zero, completion
    /// re-armed.</summary>
    public void Restart()
    {
        foreach (var s in _scores)
        {
            s.Kills = 0;
            s.Deaths = 0;
            s.Score = 0;
        }
        Elapsed = 0f;
        Completed = false;
        AllAlone = false;
        ObjectiveWinner = 0;
        _teamBonus.Clear();
    }

    /// <summary>Every player ranked by score descending, ties sharing a rank, rank 1 alone is the
    /// winner, rank 1 shared is a draw. Kills and deaths ride along for the results board / HUD to
    /// render.</summary>
    public IEnumerable<VersusStanding> Standings()
    {
        var order = new int[_scores.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (a, b) => _scores[b].Score.CompareTo(_scores[a].Score));

        int rank = 0;
        int lastScore = int.MinValue;
        for (int i = 0; i < order.Length; i++)
        {
            int index = order[i];
            int score = _scores[index].Score;
            if (score != lastScore)
            {
                rank = i + 1;
                lastScore = score;
            }
            yield return new VersusStanding(index, _scores[index].Kills, _scores[index].Deaths, rank, score);
        }
    }

    // Whether this seat's line has taken the match. Never on a Replicated one: a guest arming the
    // target itself would hold its board before the host's.
    private bool Reached(int playerIndex) =>
        !Replicated && KillTarget > 0 && RowOf(playerIndex) != null && Counted(playerIndex) >= KillTarget;

    // The number the target is read against: the seat's own score, or its team's total in a team
    // match. There the original skips the per-pilot check (FUN_00499270).
    private int Counted(int playerIndex) =>
        TeamOf(playerIndex) is > 0 and var team ? TeamScoreOf(team) : ScoreOf(playerIndex);

    private Row? RowOf(int playerIndex) =>
        playerIndex >= 0 && playerIndex < _scores.Length ? _scores[playerIndex] : null;

    private List<int> TeamNumbers()
    {
        var teams = new List<int>();
        foreach (var row in _scores)
        {
            if (row.Team > 0 && !teams.Contains(row.Team))
                teams.Add(row.Team);
        }

        teams.Sort();
        return teams;
    }

    // Reason 4, run after every death and every drop as the original does. It asks for pilots with
    // lives on two different teams, a teamless seat counting as a team of its own (FUN_004999f0).
    // A match that opened with one pilot has no opponent to lose, and a replicated one hears its
    // ending from the host.
    private void CheckAlone()
    {
        if (Completed || Replicated || _scores.Length < 2)
            return;
        int? first = null;
        for (int i = 0; i < _scores.Length; i++)
        {
            if (_scores[i].Left || OutOfLives(i))
                continue;
            int team = _scores[i].Team > 0 ? _scores[i].Team : -(i + 1);
            if (first is { } seen && seen != team)
                return;
            first = team;
        }
        AllAlone = true;
        Complete();
    }

    private void Complete()
    {
        if (Completed)
            return;
        Completed = true;
        MatchCompleted?.Invoke();
    }

    private sealed class Row
    {
        public int Kills;
        public int Deaths;
        public int Score;
        public bool Left;
        public int Team;
    }
}
