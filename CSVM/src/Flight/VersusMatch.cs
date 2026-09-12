using System;
using System.Collections.Generic;

namespace CSVM.Flight;

/// <summary>One player's ranked line in <see cref="VersusMatch.Standings"/>: kills, deaths, and a
/// competition rank (tied kill counts share a rank; the next distinct count skips ahead by the
/// number of players it passed), enough for a results board to pick out the winner (rank 1, no
/// tie) or draw (rank 1, tied) and render kills + deaths per row.</summary>
public readonly record struct VersusStanding(int PlayerIndex, int Kills, int Deaths, int Rank);

/// <summary>
/// Deathmatch scorekeeping for splitscreen "Dogfight": per-player kills and deaths plus the match
/// clock, host-fed exactly like <see cref="StuntRace"/>. A caller reports facts
/// (<see cref="RegisterKill"/>, <see cref="RegisterDeath"/>, <see cref="Advance"/>) and this class
/// turns them into standings and one completion event. The match completes once, on whichever
/// comes first: a player reaching <see cref="KillTarget"/>, or the clock reaching
/// <see cref="TimeLimit"/>; with both disabled (0) it never completes on its own. Deliberately
/// not a Node, freed with the session, host-fed exactly like <see cref="StuntRace"/>'s timekeeping.
/// ⚠ Zero engine dependency of any kind, not even <c>Log</c>, a <c>GD.Print</c> in this family
/// once crashed the xUnit host. Keep it engine-free by construction.</summary>
public sealed class VersusMatch
{
    private readonly Score[] _scores;

    public VersusMatch(int playerCount, int killTarget = 5, float timeLimit = 300f)
    {
        playerCount = Math.Max(1, playerCount); // 2-4 in practice; a solo session still scores
        KillTarget = Math.Max(0, killTarget);
        TimeLimit = Math.Max(0f, timeLimit);
        _scores = new Score[playerCount];
        for (int i = 0; i < playerCount; i++)
            _scores[i] = new Score();
    }

    /// <summary>Fired exactly once, the moment the match completes, the results board's cue.</summary>
    public event Action? MatchCompleted;

    /// <summary>How many players are being scored.</summary>
    public int PlayerCount => _scores.Length;

    /// <summary>Kills that end the match, 0 = no kill target (time is then the only way to end it).</summary>
    public int KillTarget { get; }

    /// <summary>Seconds on the match clock, 0 = no time limit (kills are then the only way to end it).</summary>
    public float TimeLimit { get; }

    /// <summary>Seconds of match clock consumed so far via <see cref="Advance"/>. Stops moving once
    /// <see cref="Completed"/>, a completed match's clock is frozen for display.</summary>
    public float Elapsed { get; private set; }

    /// <summary>Seconds left before a time-limited match times out, 0 once reached or when
    /// <see cref="TimeLimit"/> is disabled.</summary>
    public float TimeRemaining => TimeLimit > 0f ? Math.Max(0f, TimeLimit - Elapsed) : 0f;

    /// <summary>True once the match has ended (threshold or time-out), every further
    /// <see cref="RegisterKill"/>/<see cref="RegisterDeath"/>/<see cref="Advance"/> call is a no-op.</summary>
    public bool Completed { get; private set; }

    public int KillsOf(int playerIndex) => ScoreOf(playerIndex)?.Kills ?? 0;

    public int DeathsOf(int playerIndex) => ScoreOf(playerIndex)?.Deaths ?? 0;

    /// <summary>Kills still needed by <paramref name="playerIndex"/> to hit the threshold, 0 once
    /// there or when <see cref="KillTarget"/> is disabled.</summary>
    public int KillsRemaining(int playerIndex) =>
        KillTarget > 0 ? Math.Max(0, KillTarget - KillsOf(playerIndex)) : 0;

    /// <summary>A weapon kill: +1 kill to the shooter, +1 death to the victim. Completes the match
    /// once the shooter reaches <see cref="KillTarget"/>. No-op once <see cref="Completed"/>.</summary>
    public void RegisterKill(int shooter, int victim)
    {
        if (Completed)
            return;
        var shooterScore = ScoreOf(shooter);
        if (shooterScore != null)
            shooterScore.Kills++;
        var victimScore = ScoreOf(victim);
        if (victimScore != null)
            victimScore.Deaths++;
        if (KillTarget > 0 && shooterScore != null && shooterScore.Kills >= KillTarget)
            Complete();
    }

    /// <summary>A death with no killer, terrain or mid-air: +1 death only, no score change, never
    /// completes the match by itself. No-op once <see cref="Completed"/>.</summary>
    public void RegisterDeath(int victim)
    {
        if (Completed)
            return;
        var victimScore = ScoreOf(victim);
        if (victimScore != null)
            victimScore.Deaths++;
    }

    /// <summary>Advance the host-fed match clock by <paramref name="dt"/> seconds; completes the
    /// match once it reaches <see cref="TimeLimit"/>. No-op once <see cref="Completed"/> or when
    /// <see cref="TimeLimit"/> is disabled (an untimed match never times out).</summary>
    public void Advance(float dt)
    {
        if (Completed || TimeLimit <= 0f)
            return;
        Elapsed += dt;
        if (Elapsed >= TimeLimit)
            Complete();
    }

    /// <summary>Rematch: every kill/death zeroed, the clock back to zero, completion re-armed.</summary>
    public void Restart()
    {
        foreach (var s in _scores)
        {
            s.Kills = 0;
            s.Deaths = 0;
        }
        Elapsed = 0f;
        Completed = false;
    }

    /// <summary>Every player ranked by kills descending, ties sharing a rank, rank 1 alone is the
    /// winner, rank 1 shared is a draw. Deaths ride along for the results board / HUD to render.</summary>
    public IEnumerable<VersusStanding> Standings()
    {
        var order = new int[_scores.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (a, b) => _scores[b].Kills.CompareTo(_scores[a].Kills));

        int rank = 0;
        int lastKills = -1;
        for (int i = 0; i < order.Length; i++)
        {
            int index = order[i];
            int kills = _scores[index].Kills;
            if (kills != lastKills)
            {
                rank = i + 1;
                lastKills = kills;
            }
            yield return new VersusStanding(index, kills, _scores[index].Deaths, rank);
        }
    }

    private Score? ScoreOf(int playerIndex) =>
        playerIndex >= 0 && playerIndex < _scores.Length ? _scores[playerIndex] : null;

    private void Complete()
    {
        if (Completed)
            return;
        Completed = true;
        MatchCompleted?.Invoke();
    }

    private sealed class Score
    {
        public int Kills;
        public int Deaths;
    }
}
