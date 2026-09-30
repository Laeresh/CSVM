using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The multiplayer rearm bases off-engine. <see cref="RearmBases"/> serves any base in Deathmatch
/// and only the pilot's own team's in the other two team modes. It reaches a squared radius of
/// <c>rearm_rad</c> squared, or 625 when the file lacks the key, and fires once per entry.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class RearmBasesTests
{
    private static readonly Vector3 BaseAt = new(-6304.5f, 168.5f, -6727.6f);

    [Fact]
    public void DeathmatchServesEveryBaseAndTheTeamModesOnlyTheirOwn()
    {
        Assert.Equal(RearmRule.AnyBase, RearmBases.RuleFor(captureTheFlag: false, zeppelinVsZeppelin: false));
        Assert.Equal(RearmRule.OwnTeam, RearmBases.RuleFor(captureTheFlag: true, zeppelinVsZeppelin: false));
        Assert.Equal(RearmRule.OwnTeam, RearmBases.RuleFor(captureTheFlag: false, zeppelinVsZeppelin: true));

        var any = new RearmBases(RearmRule.AnyBase, RearmBases.InitialRadiusSquared);
        Assert.True(any.Serves(baseTeam: 1, pilotTeam: 2));
        Assert.True(any.Serves(baseTeam: 2, pilotTeam: 0));

        var own = new RearmBases(RearmRule.OwnTeam, RearmBases.InitialRadiusSquared);
        Assert.True(own.Serves(baseTeam: 2, pilotTeam: 2));
        Assert.False(own.Serves(baseTeam: 1, pilotTeam: 2));

        // ABLE-TO-FAIL CONTROL: a pilot on no team has no base of its own, even one numbered 0.
        Assert.False(own.Serves(baseTeam: 0, pilotTeam: 0));
    }

    [Fact]
    public void ASeatRearmsOncePerEntryAndAgainOnlyAfterLeaving()
    {
        var rules = new RearmBases(RearmRule.AnyBase, RearmBases.InitialRadiusSquared);
        var bases = new[] { new RearmBase(1, BaseAt), new RearmBase(2, BaseAt) };
        var inside = BaseAt + new Vector3(0f, -20f, 10f);
        var outside = BaseAt + new Vector3(0f, 0f, 30f);

        Assert.False(rules.Enters(0, outside, 0, bases));
        Assert.True(rules.Enters(0, inside, 0, bases));

        // Two nodes on one base, as every Deathmatch map lays them, still rearm once.
        Assert.False(rules.Enters(0, inside, 0, bases));
        Assert.False(rules.Enters(0, BaseAt, 0, bases));

        // Each seat keeps its own latch.
        Assert.True(rules.Enters(1, inside, 0, bases));

        Assert.False(rules.Enters(0, outside, 0, bases));
        Assert.True(rules.Enters(0, inside, 0, bases));
    }

    [Fact]
    public void TheRadiusIsInclusiveAndAnOfferlessBaseReachesNobody()
    {
        var rules = new RearmBases(RearmRule.AnyBase, RearmBases.InitialRadiusSquared);
        var bases = new[] { new RearmBase(1, BaseAt) };

        // 625 m squared is 25 m, and the original takes a distance equal to it (0x49b9ff).
        Assert.True(rules.Enters(0, BaseAt + new Vector3(25f, 0f, 0f), 0, bases));
        Assert.False(rules.Enters(1, BaseAt + new Vector3(25.01f, 0f, 0f), 0, bases));

        // ABLE-TO-FAIL CONTROL: a lost hull's base, with no position, offers nothing at its centre.
        Assert.False(rules.Enters(2, BaseAt, 0, new[] { new RearmBase(1, null) }));
    }

    [Fact]
    public void ATeamModeRefusesTheOtherTeamsBaseAndClearsTheLatchOnlyOutsideItsOwn()
    {
        var rules = new RearmBases(RearmRule.OwnTeam, RearmBases.InitialRadiusSquared);
        var enemyAt = BaseAt + new Vector3(500f, 0f, 0f);
        var bases = new[] { new RearmBase(1, BaseAt), new RearmBase(2, enemyAt) };

        Assert.False(rules.Enters(0, enemyAt, pilotTeam: 1, bases));
        Assert.True(rules.Enters(0, BaseAt, pilotTeam: 1, bases));
        Assert.True(rules.Enters(1, enemyAt, pilotTeam: 2, bases));
    }

    [Fact]
    public void TheRadiusIsRearmRadSquaredOrTheInitialValue()
    {
        var authored = ZrdrDict.FromAlternating(new List<object?> { "rearm_rad", new List<object?> { 40f } });
        Assert.Equal(1600f, RearmBases.ReadRadiusSquared(authored));

        // ABLE-TO-FAIL CONTROL: without the key the radius is 0x628f10's 625, not the 624 once read.
        Assert.Equal(625f, RearmBases.ReadRadiusSquared(ZrdrDict.FromAlternating(new List<object?>())));

        string? why = null;
        Assert.Equal(625f, RearmBases.LoadRadiusSquared(Path.Combine(TestData.TempRoot, "no-such-zrdr"), reason => why = reason));
        Assert.NotNull(why);
    }

    [Fact]
    public void TheNodesAreNamedByIndexAndMode()
    {
        Assert.Equal("rearm_node_1", RearmBases.NodeName(1, zeppelins: false));
        Assert.Equal("zep_rearm_node_2", RearmBases.NodeName(2, zeppelins: true));
    }

    [ExtractedDataFact]
    public void TheShippedPlayerZrdLeavesTheRadiusAtTwentyFiveMetres()
    {
        string zrdr = SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));
        string? why = null;
        Assert.Equal(RearmBases.InitialRadiusSquared, RearmBases.LoadRadiusSquared(zrdr, reason => why = reason));
        Assert.Null(why);

        var messages = Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json"));
        Assert.Equal("Rearmed!", messages.Get(HudMessages.RearmedKey));
    }
}
