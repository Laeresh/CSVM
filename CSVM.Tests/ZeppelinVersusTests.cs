using System;
using CSVM.Flight.Modes;
using CSVM.Net;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Zeppelin vs Zeppelin off-engine. The first two lobby teams in seat order take hull 0 and hull 1
/// and the blocks around them. A gas bag scores once, 10 to an enemy and -10 to its own side, and a
/// broadside cannon scores its bound bag. A lost hull ends the match with the other side the winner
/// and 100 to every other team that the Score limit never reads. The return lands halfway between
/// the field and the pilot's own hull, and the two messages round-trip.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class ZeppelinVersusTests
{
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
        var rules = new ZeppelinVersus(1, 2);
        var cannon = new HullPartLoss(1, "gasbag1", Cannon: true, Seat: 0);
        var bag = new HullPartLoss(1, "GASBAG1", Cannon: false, Seat: 2);

        Assert.True(rules.Counts(cannon));
        Assert.Equal(ZeppelinVersus.GasbagScore, rules.Points(cannon, killerTeam: 1));
        Assert.False(rules.Counts(bag));

        // Hull 0's bag of the same name is its own bag, and a side's own bag costs.
        var own = new HullPartLoss(0, "gasbag1", Cannon: false, Seat: 1);
        Assert.True(rules.Counts(own));
        Assert.Equal(ZeppelinVersus.OwnGasbagScore, rules.Points(own, killerTeam: 1));

        // ABLE-TO-FAIL CONTROL: a part no seat killed scores nothing.
        Assert.Equal(0, rules.Points(own with { Seat = -1 }, killerTeam: 1));
    }

    [Fact]
    public void ALostHullEndsTheMatchForTheOtherSideAndItsBonusIsNotTheScoreLimit()
    {
        var match = new VersusMatch(4, killTarget: 50, timeLimit: 0f);
        match.AssignTeams(new[] { 1, 1, 2, 3 });
        match.AddScore(0, ZeppelinVersus.GasbagScore);

        match.EndOnHullLoss(losingTeam: 2, winningTeam: 1);

        Assert.True(match.Completed);
        Assert.Equal(1, match.ObjectiveWinner);
        Assert.Equal(ZeppelinVersus.GasbagScore, match.TeamScoreOf(1));
        Assert.Equal(ZeppelinVersus.GasbagScore + VersusMatch.HullLossBonus, match.TeamTotalOf(1));
        Assert.Equal(0, match.TeamTotalOf(2));
        Assert.Equal(VersusMatch.HullLossBonus, match.TeamTotalOf(3));
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
        Assert.Equal(VersusMatch.HullLossBonus, match.TeamTotalOf(2));

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
