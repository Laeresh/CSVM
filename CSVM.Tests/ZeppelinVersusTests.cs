using System;
using CSVM.Flight.Modes;
using CSVM.Net;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Zeppelin vs Zeppelin off-engine. The first two lobby teams in seat order take hull 0 and hull 1
/// and the blocks around them. A gas bag scores once, to an enemy and against its own side, and a
/// broadside cannon scores its bound bag. A lost hull ends the match, the other side the winner and
/// every other team given a term the Score limit never reads. A pilot a hull downs sets its side's
/// term. The return lands halfway between the field and the pilot's own hull, and the two messages
/// round-trip.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class ZeppelinVersusTests
{
    // Values unlike every fallback, so a rule that ignored the set it was given would fail.
    private static readonly MatchScores Authored = MatchScores.Fallback with
    {
        HullLoss = 10, ZeppelinKill = 3, GasbagKill = 7, OwnGasbagKill = -4,
    };

    [Fact]
    public void AHullsBroadsideRoundsCarryItsOwnShooterId()
    {
        Assert.Equal(0, ZeppelinVersus.HullOfShooter(ZeppelinVersus.BroadsideShooter(0)));
        Assert.Equal(1, ZeppelinVersus.HullOfShooter(ZeppelinVersus.BroadsideShooter(1)));
        Assert.True(ZeppelinVersus.BroadsideShooter(0) < CSVM.Flight.Weapons.ProjectilePool.NoShooter);

        // ABLE-TO-FAIL CONTROL: an unowned round and every seat's id name no hull.
        Assert.Equal(-1, ZeppelinVersus.HullOfShooter(CSVM.Flight.Weapons.ProjectilePool.NoShooter));
        Assert.Equal(-1, ZeppelinVersus.HullOfShooter(0));
        Assert.Equal(-1, ZeppelinVersus.HullOfShooter(100));
    }

    [Fact]
    public void APilotAHullDownsSetsItsSidesTermOnceAndCostsTheVictimNoScore()
    {
        var match = new VersusMatch(3, killTarget: 50, timeLimit: 0f, scores: Authored);
        match.AssignTeams(new[] { 1, 2, 2 });

        match.RegisterZeppelinKill(victim: 1, team: 1);
        match.RegisterZeppelinKill(victim: 2, team: 1);

        Assert.Equal(Authored.ZeppelinKill, match.TeamTotalOf(1));
        Assert.Equal(0, match.TeamScoreOf(1));
        Assert.Equal(0, match.ScoreOf(1));
        Assert.Equal(1, match.DeathsOf(1));
        Assert.Equal(1, match.DeathsOf(2));

        // The lost hull adds its term on top, as the original's event 3 adds to the same field.
        match.EndOnHullLoss(losingTeam: 2, winningTeam: 1);
        Assert.Equal(Authored.ZeppelinKill + Authored.HullLoss, match.TeamTotalOf(1));

        // ABLE-TO-FAIL CONTROL: a replicated match takes the term but leaves deaths to the host.
        var guest = new VersusMatch(2, killTarget: 0, timeLimit: 600f, scores: Authored);
        guest.AssignTeams(new[] { 1, 2 });
        guest.Replicate();
        guest.RegisterZeppelinKill(victim: 1, team: 1);
        Assert.Equal(Authored.ZeppelinKill, guest.TeamTermOf(1));
        Assert.Equal(0, guest.DeathsOf(1));

        // The host's running tick after it keeps the term; only a rematch of an ended match clears.
        guest.ApplyState(0, 600f, 300f, ended: false);
        Assert.Equal(Authored.ZeppelinKill, guest.TeamTermOf(1));
    }

    [Fact]
    public void TheSidesAreTheFirstTwoTeamsInSeatOrderWhateverTheirNumbers()
    {
        Assert.Equal((3, 1), ZeppelinVersus.Sides(new[] { 0, 3, 3, 1, 2 }));
        Assert.Equal((2, 0), ZeppelinVersus.Sides(new[] { 2, 0, 2 }));
        Assert.Equal(new[] { 1, 1, 2, 0, 0 }, ZeppelinVersus.SpawnBlocks(new[] { 3, 3, 1, 2, 0 }));

        var rules = new ZeppelinVersus(3, 1);
        Assert.Equal(3, rules.TeamOfHull(0));
        Assert.Equal(1, rules.TeamOfHull(1));
        Assert.Equal(0, rules.HullOf(3));
        Assert.Equal(1, rules.OtherTeam(3));
        Assert.Equal(-1, rules.HullOf(2));
        Assert.Equal(0, rules.OtherTeam(2));
    }

    [Fact]
    public void AGasBagScoresOnceAndItsCannonScoresItFirst()
    {
        var rules = new ZeppelinVersus(1, 2, Authored);
        var cannon = new HullPartLoss(1, "gasbag1", Cannon: true, Seat: 0);
        var bag = new HullPartLoss(1, "GASBAG1", Cannon: false, Seat: 2);

        Assert.True(rules.Counts(cannon));
        Assert.Equal(Authored.GasbagKill, rules.Points(cannon, killerTeam: 1));
        Assert.False(rules.Counts(bag));

        // Hull 0's bag of the same name is its own bag, and a side's own bag costs.
        var own = new HullPartLoss(0, "gasbag1", Cannon: false, Seat: 1);
        Assert.True(rules.Counts(own));
        Assert.Equal(Authored.OwnGasbagKill, rules.Points(own, killerTeam: 1));

        // ABLE-TO-FAIL CONTROL: a part no seat killed scores nothing.
        Assert.Equal(0, rules.Points(own with { Seat = -1 }, killerTeam: 1));
    }

    [Fact]
    public void ALostHullEndsTheMatchForTheOtherSideAndItsBonusIsNotTheScoreLimit()
    {
        var match = new VersusMatch(4, killTarget: 50, timeLimit: 0f, scores: Authored);
        match.AssignTeams(new[] { 1, 1, 2, 3 });
        match.AddScore(0, Authored.GasbagKill);

        match.EndOnHullLoss(losingTeam: 2, winningTeam: 1);

        Assert.True(match.Completed);
        Assert.Equal(1, match.ObjectiveWinner);
        Assert.Equal(Authored.GasbagKill, match.TeamScoreOf(1));
        Assert.Equal(Authored.GasbagKill + Authored.HullLoss, match.TeamTotalOf(1));
        Assert.Equal(0, match.TeamTotalOf(2));
        Assert.Equal(Authored.HullLoss, match.TeamTotalOf(3));
        Assert.Equal(1, Assert.Single(match.TeamStandings(), t => t.Rank == 1).Team);

        // Once only, and a rematch clears it.
        match.EndOnHullLoss(losingTeam: 1, winningTeam: 2);
        Assert.Equal(1, match.ObjectiveWinner);
        match.Restart();
        Assert.Equal(0, match.ObjectiveWinner);
        Assert.Equal(0, match.TeamTotalOf(3));
    }

    [Fact]
    public void AReplicatedMatchTakesTheHullEndingFromTheHostsState()
    {
        var match = new VersusMatch(2, killTarget: 0, timeLimit: 600f);
        match.AssignTeams(new[] { 1, 2 });
        match.Replicate();

        match.EndOnHullLoss(losingTeam: 1, winningTeam: 2);
        Assert.False(match.Completed);
        Assert.Equal(2, match.ObjectiveWinner);

        match.ApplyState(0, 600f, 300f, ended: true);
        Assert.True(match.Completed);
        Assert.Equal(MatchScores.Fallback.HullLoss, match.TeamTotalOf(2));

        // ABLE-TO-FAIL CONTROL: the host's running state after a rematch clears the ending.
        match.ApplyState(0, 600f, 600f, ended: false);
        Assert.Equal(0, match.ObjectiveWinner);
        Assert.Equal(0, match.TeamTotalOf(2));
    }

    [Fact]
    public void AReturnLandsHalfwayBetweenTheFieldAndItsOwnHullOnItsBearing()
    {
        var others = new[] { new Vector3(0f, 500f, 0f), new Vector3(100f, 700f, 0f) };
        var hull = new Vector3(1000f, 400f, 1000f);

        // Index 2's bearing is 1.5708, straight along +z, and its yaw is 1.5708 less that.
        var point = ZeppelinVersus.RespawnPoint(others, hull, 2, 1200f, 100f, null)!.Value;
        Assert.Equal(525f, point.Position.X, 1);
        Assert.Equal(500f + 1200f, point.Position.Z, 1);
        Assert.Equal(ZeppelinVersus.RespawnFloor, point.Position.Y);
        Assert.Equal(0f, point.HeadingDeg, 2);

        var high = ZeppelinVersus.RespawnPoint(others, hull, 2, 1200f, 100f, _ => 1000f)!.Value;
        Assert.Equal(1100f, high.Position.Y);
        var low = ZeppelinVersus.RespawnPoint(others, hull, 2, 1200f, 100f, _ => 800f)!.Value;
        Assert.Equal(ZeppelinVersus.RespawnFloor, low.Position.Y);

        // ABLE-TO-FAIL CONTROL: with no hull it is the field's centroid alone, and with no field none.
        var bare = ZeppelinVersus.RespawnPoint(others, null, 2, 1200f, 100f, null)!.Value;
        Assert.Equal(50f, bare.Position.X, 1);
        Assert.Null(ZeppelinVersus.RespawnPoint(Array.Empty<Vector3>(), hull, 2, 1200f, 100f, null));
    }

    [Fact]
    public void TheWinnerAndTheComputedReturnRoundTrip()
    {
        var state = new MatchStateMessage(10f, 600f, 40, NetMatchEnd.Objective, 12.5f, Winner: 3);
        var bytes = new byte[MatchStateMessage.Size];
        Assert.Equal(MatchStateMessage.Size, state.Write(bytes));
        Assert.True(MatchStateMessage.TryRead(bytes, out var heard));
        Assert.Equal(state, heard);

        var spawn = new SpawnAtMessage(4, new Vector3(-5831f, 900f, -1620f), 45f);
        var wire = new byte[SpawnAtMessage.Size];
        Assert.Equal(SpawnAtMessage.Size, spawn.Write(wire));
        Assert.True(SpawnAtMessage.TryRead(wire, out var placed));
        Assert.Equal(spawn, placed);
        Assert.False(SpawnAtMessage.TryRead(wire.AsSpan(0, SpawnAtMessage.Size - 1), out _));
        Assert.Equal(0x65, (int)NetMessageType.SpawnAt);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.SpawnAt));
    }

    [Fact]
    public void AMenuLaunchOfZeppelinVsZeppelinFliesMp3WithItsZeppelins()
    {
        var cli = SessionSpec.Parse(new[] { "--vs" });
        var spec = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus, zeppelinVsZeppelin: true);
        Assert.True(spec.ZeppelinVsZeppelin);
        Assert.Equal(SessionSpec.ZvzMission, spec.Mission);

        // ABLE-TO-FAIL CONTROL: Capture the Flag beside it wins, and the command line needs --vs.
        var ctf = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus, captureTheFlag: true, zeppelinVsZeppelin: true);
        Assert.False(ctf.ZeppelinVsZeppelin);
        Assert.Equal(SessionSpec.CtfMission, ctf.Mission);
        Assert.False(SessionSpec.Parse(new[] { "--zvz" }).ZeppelinVsZeppelin);
        Assert.True(SessionSpec.Parse(new[] { "--vs", "--zvz" }).ZeppelinVsZeppelin);
    }
}
