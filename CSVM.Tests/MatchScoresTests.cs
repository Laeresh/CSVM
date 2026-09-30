using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The network match's scoring values off-engine. <see cref="MatchScores"/> takes each
/// <c>score_*</c> key <c>player.zrd</c> authors and keeps the executable's fallback for the rest.
/// A match scores with the set it was built on.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class MatchScoresTests
{
    [Fact]
    public void AnAuthoredKeyReplacesItsFallbackAndAMissingOneKeepsIt()
    {
        var player = ZrdrDict.FromAlternating(new List<object?>
        {
            "score_kill", new List<object?> { 2f },
            "score_zep", new List<object?> { 10f },
            "respawn_rad", new List<object?> { 1200f },
        });

        var scores = MatchScores.Read(player);

        Assert.Equal(MatchScores.Fallback with { Kill = 2, HullLoss = 10 }, scores);

        // ABLE-TO-FAIL CONTROL: a file that authors none of the keys scores the executable's own.
        Assert.Equal(MatchScores.Fallback, MatchScores.Read(ZrdrDict.FromAlternating(new List<object?>())));
        Assert.Equal(new MatchScores(1, -1, 1, 100, 1, 10, -10, 1, 5), MatchScores.Fallback);
    }

    [Fact]
    public void AMatchScoresWithTheSetItWasBuiltOn()
    {
        var scores = MatchScores.Fallback with { Kill = 2, Suicide = -2 };
        var match = new VersusMatch(2, killTarget: 4, timeLimit: 0f, scores: scores);

        match.RegisterKill(shooter: 0, victim: 1);
        match.RegisterDeath(victim: 1);
        Assert.Equal(2, match.ScoreOf(0));
        Assert.Equal(-2, match.ScoreOf(1));
        Assert.False(match.Completed);

        match.RegisterKill(shooter: 0, victim: 1);
        Assert.True(match.Completed);

        // ABLE-TO-FAIL CONTROL: a match built on nothing scores the fallbacks.
        var bare = new VersusMatch(2, killTarget: 0, timeLimit: 0f);
        bare.RegisterKill(shooter: 0, victim: 1, turret: true);
        Assert.Equal(MatchScores.Fallback.TurretKill, bare.ScoreOf(0));
    }

    [Fact]
    public void AnUnreadableArchiveKeepsTheFallbacksAndSaysWhy()
    {
        string? why = null;
        var scores = MatchScores.Load(Path.Combine(TestData.TempRoot, "no-such-zrdr"), reason => why = reason);
        Assert.Equal(MatchScores.Fallback, scores);
        Assert.NotNull(why);
    }

    [ExtractedDataFact]
    public void TheShippedPlayerZrdAuthorsKillSuicideHullAndBothFlags()
    {
        string zrdr = SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

        var shipped = MatchScores.Load(zrdr);

        Assert.Equal(MatchScores.Fallback with
        {
            Kill = 2, Suicide = -2, HullLoss = 10, FlagReturn = 8, FlagCapture = 10,
        }, shipped);
    }
}
