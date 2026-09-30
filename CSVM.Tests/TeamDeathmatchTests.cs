using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.UI.Menu;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Team Deathmatch off-engine. <see cref="VersusMatch"/> scores a teammate kill as a suicide, reads
/// the target against a team's total and ends on reason 4 by team. Each seat opens and comes back in
/// its team's <c>net.zrd</c> block. A lobby team flies in its own hostility band, and the Game
/// Scores page lists each team over its pilots.
/// </summary>
public class TeamDeathmatchTests
{
    private static readonly Dictionary<int, string> Names = new() { [1] = "Red", [2] = "Blue" };

    [Fact]
    public void ATeammateKillCostsTheKillerAPointAndCountsNoKill()
    {
        var match = Teamed(new[] { 1, 1, 2 }, killTarget: 0);

        match.RegisterKill(shooter: 0, victim: 1);

        Assert.Equal(match.Scores.Suicide, match.ScoreOf(0));
        Assert.Equal(0, match.KillsOf(0));
        Assert.Equal(1, match.DeathsOf(1));
        Assert.Equal(0, match.ScoreOf(1));

        // ABLE-TO-FAIL CONTROL: the same shooter on the other team's seat scores a kill.
        match.RegisterKill(shooter: 0, victim: 2);
        Assert.Equal(0, match.ScoreOf(0));
        Assert.Equal(1, match.KillsOf(0));
    }

    [Fact]
    public void TheTargetIsReachedByATeamsTotalThoughNoPilotReachesItAlone()
    {
        var match = Teamed(new[] { 1, 1, 2, 2 }, killTarget: 3);

        match.RegisterKill(0, 2);
        match.RegisterKill(1, 3);
        Assert.False(match.Completed);
        Assert.Equal(2, match.TeamScoreOf(1));

        match.RegisterKill(1, 2);
        Assert.True(match.Completed);
        Assert.False(match.AllAlone);
        Assert.True(match.ScoreOf(0) < 3 && match.ScoreOf(1) < 3);

        // ABLE-TO-FAIL CONTROL: the same kills in a free-for-all leave every pilot short.
        var free = new VersusMatch(4, killTarget: 3, timeLimit: 0f);
        free.RegisterKill(0, 2);
        free.RegisterKill(1, 3);
        free.RegisterKill(1, 2);
        Assert.False(free.Completed);
    }

    [Fact]
    public void ReasonFourComesWhenOneTeamIsLeftThoughTwoPilotsStand()
    {
        var match = Teamed(new[] { 1, 1, 2 }, killTarget: 0, lives: 1);

        match.RegisterKill(0, 2);

        Assert.True(match.Completed);
        Assert.True(match.AllAlone);

        // ABLE-TO-FAIL CONTROL: a free-for-all with the same two pilots left runs on.
        var free = new VersusMatch(3, killTarget: 0, timeLimit: 600f, lives: 1);
        free.RegisterKill(0, 2);
        Assert.False(free.Completed);
    }

    [Fact]
    public void TeamStandingsRankTheTotalsAndCarryTheLobbyNames()
    {
        var match = Teamed(new[] { 1, 2, 2 }, killTarget: 0);
        match.RegisterKill(1, 0);
        match.RegisterKill(2, 0);
        match.RegisterKill(0, 1);

        var teams = match.TeamStandings().ToList();

        Assert.Equal(new[] { 2, 1 }, teams.Select(t => t.Team));
        Assert.Equal(new VersusTeamStanding(2, "Blue", 2, 1, 1, 2), teams[0]);
        Assert.Equal(new VersusTeamStanding(1, "Red", 1, 2, 2, 1), teams[1]);
        Assert.Equal("Team 7", match.TeamName(7));
        Assert.Empty(new VersusMatch(2).TeamStandings());
    }

    [Fact]
    public void EachSeatOpensOnItsTeamsBlockByItsPlaceInTheTeam()
    {
        var (openings, blocks) = SpawnPoints.TeamBlocks(48, new[] { 2, 1, 2, 0 }, spawnBase: 5);

        Assert.Equal(new[] { 32, 16, 33, 5 }, openings);
        Assert.Equal((32, 16), blocks[0]);
        Assert.Equal((16, 16), blocks[1]);
        Assert.Equal((0, 16), blocks[3]);

        // A team whose block the map does not author walks the free-for-all block instead.
        var (short48, _) = SpawnPoints.TeamBlocks(48, new[] { 3 }, spawnBase: 2);
        Assert.Equal(new[] { 2 }, short48);
    }

    [Fact]
    public void ABlockRotationBringsASeatBackInsideItsOwnBlock()
    {
        var table = new List<SpawnPoint>();
        for (int i = 0; i < 48; i++)
        {
            table.Add(new SpawnPoint(new Vector3(i * 1000f, 300f, 0f), 0f));
        }

        var (openings, blocks) = SpawnPoints.TeamBlocks(table.Count, new[] { 1, 2 }, 0);
        var rotation = VersusSpawnRotation.ForBlocks(table, openings, blocks, new[] { 1, 2 }, new Random(7))!;
        var field = new Vector3?[] { null, table[32].Position };

        for (int round = 0; round < 20; round++)
        {
            rotation.Choose(0, field, killer: 1);
            Assert.InRange(rotation.IndexOf(0), 16, 31);
        }
    }

    [Fact]
    public void ALobbyTeamIsBandedClearOfEveryAuthoredSide()
    {
        Assert.Null(AimAssist.LobbyTeam(0));
        Assert.Equal(AimAssist.LobbyTeamBand + 1, AimAssist.LobbyTeam(1));
        Assert.NotEqual(AimAssist.PlayerTeam, AimAssist.LobbyTeam(1));
        Assert.NotEqual(TurretDef.DefaultTeamId, AimAssist.LobbyTeam(2));
        Assert.True(AimAssist.LobbyTeam(1) > AimAssist.TeamOfPilot(15));
        Assert.True(AimAssist.LobbyTeam(DogfightLobby.MaxTeams) < AimAssist.WorldTeam);
    }

    [Fact]
    public void GameScoresListEachTeamWithItsPilotsUnderIt()
    {
        var match = Teamed(new[] { 1, 2, 1 }, killTarget: 0);
        match.RegisterKill(1, 0);
        match.RegisterKill(1, 2);

        var lines = DogfightLobby.ScoresOf(match, new[] { "Ann", "Bob", "Cid" });

        Assert.Equal(new[] { "Blue", "Bob", "Red" }, lines.Take(3).Select(l => l.Name));
        Assert.Equal(new[] { "Ann", "Cid" }, lines.Skip(3).Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new DogfightScore("Blue", 2, 2, 0, IsTeam: true), lines[0]);
        Assert.False(lines[1].IsTeam);

        // ABLE-TO-FAIL CONTROL: a free-for-all lands with pilot lines alone.
        var free = new VersusMatch(3);
        free.RegisterKill(1, 0);
        Assert.DoesNotContain(DogfightLobby.ScoresOf(free, new[] { "Ann", "Bob", "Cid" }), l => l.IsTeam);
    }

    private static VersusMatch Teamed(int[] teams, int killTarget, int lives = 0)
    {
        var match = new VersusMatch(teams.Length, killTarget, timeLimit: 0f, lives);
        match.AssignTeams(teams, Names);
        return match;
    }
}
