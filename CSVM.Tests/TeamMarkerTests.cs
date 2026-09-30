using System.IO;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The team modes' target labels off-engine, all read through <see cref="AimAssist.Friendly"/>.
/// A Capture the Flag flag's three markers and its carrier's tag read "Your" to its side and
/// "Enemy" to the other. A Zeppelin vs Zeppelin hull reads "Defend" and "Destroy" the same way.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class TeamMarkerTests
{
    private static readonly int Red = AimAssist.LobbyTeam(1)!.Value;
    private static readonly int Blue = AimAssist.LobbyTeam(2)!.Value;

    [Fact]
    public void AFriendIsTheSameTeamOrAnUnaffiliatedOneAndEveryOtherTeamIsAFoe()
    {
        Assert.True(AimAssist.Friendly(Red, Red));
        Assert.True(AimAssist.Friendly(Red, AimAssist.NeutralTeam));
        Assert.True(AimAssist.Friendly(AimAssist.NeutralTeam, Blue));

        // ABLE-TO-FAIL CONTROL: two lobby teams are foes both ways.
        Assert.False(AimAssist.Friendly(Red, Blue));
        Assert.False(AimAssist.Friendly(Blue, Red));
    }

    [Fact]
    public void TheMarkerKeysAreTheThreeNamesTheFlagBuilderFormats()
    {
        Assert.Equal((FlagMarker.Base, 1), FlagMarkers.MarkerOf("ctf_1"));
        Assert.Equal((FlagMarker.AtBase, 2), FlagMarkers.MarkerOf("cs_flag_2"));
        Assert.Equal((FlagMarker.Away, 1), FlagMarkers.MarkerOf("CS_FLG_LIGHT1"));
        Assert.Null(FlagMarkers.MarkerOf("racmplx"));
        Assert.Null(FlagMarkers.MarkerOf("ctf_"));
        Assert.Null(FlagMarkers.MarkerOf("multiplayer1zep"));
    }

    [Fact]
    public void TheAtBaseMarkerStandsWhileTheFlagIsHomeAndTheAwayMarkerWhileItIsNot()
    {
        var home = new FlagRow(1, FlagState.Home, FlagMatch.NoHolder);
        var held = new FlagRow(1, FlagState.Held, 2);
        var floating = new FlagRow(1, FlagState.Floating, FlagMatch.NoHolder);

        Assert.True(Side(FlagMarker.AtBase, home).Shown);
        Assert.False(Side(FlagMarker.Away, home).Shown);
        Assert.False(Side(FlagMarker.AtBase, held).Shown);
        Assert.True(Side(FlagMarker.Away, held).Shown);
        Assert.False(Side(FlagMarker.AtBase, floating).Shown);
        Assert.True(Side(FlagMarker.Away, floating).Shown);

        // The base itself never leaves the cycle.
        Assert.True(Side(FlagMarker.Base, held).Shown);
    }

    [ExtractedDataFact]
    public void EveryMarkerReadsYourToItsTeamAndEnemyToTheOther()
    {
        var strings = Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json"));
        var held = new FlagRow(1, FlagState.Held, 2);
        var site = new ObjectiveSite { Side = FlagMarkers.Side(FlagMarker.Away, held, Red, "Red Squadron", "Ace", strings) };

        Assert.Equal("Your Flag Captured by Ace", site.CategoryFor(Red));
        Assert.Equal("Enemy Flag Captured by Ace", site.CategoryFor(Blue));

        var home = new FlagRow(1, FlagState.Home, FlagMatch.NoHolder);
        Assert.Equal("Your Base", Category(FlagMarker.Base, home, Red, strings));
        Assert.Equal("Enemy Base", Category(FlagMarker.Base, home, Blue, strings));
        Assert.Equal("Your Flag At Base", Category(FlagMarker.AtBase, home, Red, strings));
        Assert.Equal("Enemy Flag At Base", Category(FlagMarker.AtBase, home, Blue, strings));
        var floating = new FlagRow(1, FlagState.Floating, FlagMatch.NoHolder);
        Assert.Equal("Your Flag Floating", Category(FlagMarker.Away, floating, Red, strings));
        Assert.Equal("Enemy Flag Floating", Category(FlagMarker.Away, floating, Blue, strings));

        // The carrier's tag is row 198 with its data's double space.
        Assert.Equal("Ace  Holds Your flag", FlagMarkers.HolderTag(strings, "Ace", Red, Red));
        Assert.Equal("Ace  Holds Enemy flag", FlagMarkers.HolderTag(strings, "Ace", Blue, Red));
    }

    [ExtractedDataFact]
    public void AHullIsDefendToItsSideAndDestroyToTheOtherUnderItsTeamName()
    {
        var strings = Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json"));
        var site = new ObjectiveSite
        {
            Category = "Destroy",
            Side = ZeppelinVersus.HullSide(Blue, "Blue Angels", strings),
        };

        Assert.Equal("Defend", site.CategoryFor(Blue));
        Assert.Equal("Destroy", site.CategoryFor(Red));
        Assert.Equal("Blue Angels", site.Side!.Value.Name);

        // ABLE-TO-FAIL CONTROL: with no side the record's own line stands for every pane.
        site.Side = null;
        Assert.Equal("Destroy", site.CategoryFor(Blue));
    }

    private static SiteSide Side(FlagMarker marker, FlagRow row) =>
        FlagMarkers.Side(marker, row, Red, "Red Squadron", "Ace", null, Vector3.Zero);

    private static string? Category(FlagMarker marker, FlagRow row, int ownTeam, Messages strings) =>
        new ObjectiveSite { Side = FlagMarkers.Side(marker, row, Red, "Red Squadron", "Ace", strings) }
            .CategoryFor(ownTeam);
}
