using System.Collections.Generic;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Dogfight match bookkeeping (<see cref="VersusMatch"/>), off-engine: kill/death tallies, the
/// two ways a match ends (threshold, time-out), the tie-draw rule, a rematch's re-arm, and each
/// end condition disabled on its own. <see cref="VersusMatch"/> is a plain class with no engine
/// dependency at all (unlike its sibling <see cref="StuntRace"/>, it needs no fake-collaborator or
/// console-sink dance, there is nothing here that could reach <c>GD.*</c>).
/// </summary>
public class VersusMatchTests
{
    [Fact]
    public void RegisterKillScoresTheShooterAndTalliesTheVictimsDeath()
    {
        var match = new VersusMatch(playerCount: 2, killTarget: 5, timeLimit: 300f);

        match.RegisterKill(shooter: 0, victim: 1);

        Assert.Equal(1, match.KillsOf(0));
        Assert.Equal(0, match.DeathsOf(0));
        Assert.Equal(0, match.KillsOf(1));
        Assert.Equal(1, match.DeathsOf(1));
        Assert.False(match.Completed);
    }

    [Fact]
    public void ReachingTheKillTargetCompletesTheMatchOnce()
    {
        var match = new VersusMatch(playerCount: 3, killTarget: 5, timeLimit: 300f);
        int completedCount = 0;
        match.MatchCompleted += () => completedCount++;

        for (int i = 0; i < 5; i++)
            match.RegisterKill(shooter: 0, victim: 1);

        Assert.True(match.Completed);
        Assert.Equal(5, match.KillsOf(0));
        Assert.Equal(1, completedCount);
    }

    [Fact]
    public void TimingOutCompletesTheMatchWithTheHighestKillerAsLeader()
    {
        var match = new VersusMatch(playerCount: 3, killTarget: 5, timeLimit: 10f);
        int completedCount = 0;
        match.MatchCompleted += () => completedCount++;

        match.RegisterKill(shooter: 0, victim: 2);
        match.RegisterKill(shooter: 0, victim: 2);
        match.RegisterKill(shooter: 0, victim: 2);
        match.RegisterKill(shooter: 1, victim: 2);

        match.Advance(6f);
        Assert.False(match.Completed);
        match.Advance(4f);

        Assert.True(match.Completed);
        Assert.Equal(1, completedCount);
        var order = new List<VersusStanding>(match.Standings());
        Assert.Equal(0, order[0].PlayerIndex);
        Assert.Equal(3, order[0].Kills);
        Assert.Equal(1, order[0].Rank);
        Assert.NotEqual(1, order[1].Rank); // the sole leader, not a shared rank 1
    }

    [Fact]
    public void EqualTopKillsAtTimeOutIsADraw()
    {
        var match = new VersusMatch(playerCount: 3, killTarget: 0, timeLimit: 10f);

        match.RegisterKill(shooter: 0, victim: 2);
        match.RegisterKill(shooter: 0, victim: 2);
        match.RegisterKill(shooter: 1, victim: 2);
        match.RegisterKill(shooter: 1, victim: 2);

        match.Advance(10f);

        Assert.True(match.Completed);
        var order = new List<VersusStanding>(match.Standings());
        var byIndex = new Dictionary<int, VersusStanding>();
        foreach (var row in order)
            byIndex[row.PlayerIndex] = row;

        Assert.Equal(1, byIndex[0].Rank);
        Assert.Equal(1, byIndex[1].Rank); // tied at the top, a draw, not a winner
        Assert.Equal(3, byIndex[2].Rank); // shares nothing, the tied pair pushed it down
    }

    [Fact]
    public void EventsAfterCompletionAreIgnoredNoScoreChangeNoSecondEvent()
    {
        var match = new VersusMatch(playerCount: 2, killTarget: 2, timeLimit: 300f);
        int completedCount = 0;
        match.MatchCompleted += () => completedCount++;

        match.RegisterKill(shooter: 0, victim: 1);
        match.RegisterKill(shooter: 0, victim: 1);
        Assert.True(match.Completed);
        Assert.Equal(1, completedCount);

        match.RegisterKill(shooter: 0, victim: 1);
        match.RegisterDeath(victim: 0);
        match.Advance(9999f);

        Assert.Equal(2, match.KillsOf(0));
        Assert.Equal(2, match.DeathsOf(1));
        Assert.Equal(0, match.DeathsOf(0));
        Assert.Equal(1, completedCount); // still just the one firing
    }

    [Fact]
    public void RestartClearsScoresAndClockAndReArmsCompletion()
    {
        var match = new VersusMatch(playerCount: 2, killTarget: 2, timeLimit: 300f);
        int completedCount = 0;
        match.MatchCompleted += () => completedCount++;

        match.RegisterKill(shooter: 0, victim: 1);
        match.RegisterKill(shooter: 0, victim: 1);
        Assert.True(match.Completed);

        match.Restart();

        Assert.False(match.Completed);
        Assert.Equal(0, match.KillsOf(0));
        Assert.Equal(0, match.DeathsOf(1));
        Assert.Equal(0f, match.Elapsed);

        // Completion must re-arm, not just the fields: reaching the threshold again fires again.
        match.RegisterKill(shooter: 1, victim: 0);
        match.RegisterKill(shooter: 1, victim: 0);

        Assert.True(match.Completed);
        Assert.Equal(2, completedCount);
    }

    [Fact]
    public void KillTargetDisabledRunsOnTimeOnly()
    {
        var match = new VersusMatch(playerCount: 2, killTarget: 0, timeLimit: 5f);

        for (int i = 0; i < 50; i++)
            match.RegisterKill(shooter: 0, victim: 1);
        Assert.False(match.Completed); // no amount of kills ends it with the target disabled
        Assert.Equal(50, match.KillsOf(0));

        match.Advance(5f);
        Assert.True(match.Completed);
    }

    [Fact]
    public void TimeLimitDisabledRunsOnKillsOnly()
    {
        var match = new VersusMatch(playerCount: 2, killTarget: 3, timeLimit: 0f);

        match.Advance(1_000_000f);
        Assert.False(match.Completed); // the clock never moves, an untimed match never times out
        Assert.Equal(0f, match.Elapsed);

        match.RegisterKill(shooter: 0, victim: 1);
        match.RegisterKill(shooter: 0, victim: 1);
        match.RegisterKill(shooter: 0, victim: 1);
        Assert.True(match.Completed);
    }

    [Fact]
    public void RegisterDeathTalliesDeathsButNeverCompletesAKillTargetMatch()
    {
        var match = new VersusMatch(playerCount: 2, killTarget: 3, timeLimit: 300f);

        for (int i = 0; i < 10; i++)
            match.RegisterDeath(victim: 0);

        Assert.Equal(10, match.DeathsOf(0));
        Assert.Equal(0, match.KillsOf(0));
        Assert.Equal(0, match.KillsOf(1));
        Assert.False(match.Completed);
    }
}
