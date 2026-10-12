using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Net;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class StuntBotObjectiveTests
{
    // Two zones on a straight east-west course 2 km apart, each ribbon 200 m long along -Z.
    private static readonly Vector3 FirstNear = new(0, 100, 0);
    private static readonly Vector3 FirstFar = new(0, 100, -200);
    private static readonly Vector3 SecondNear = new(0, 100, -2000);
    private static readonly Vector3 SecondFar = new(0, 100, -2200);

    // Flying north (-Z) at 70 m/s.
    private static readonly Vector3 North = new(0, 0, -70);

    /// <summary>The shipped stunt courses, each chapter's IA1, and their zone counts
    /// (docs/formats/missions.md).</summary>
    public static TheoryData<string, int> StuntCourses => new()
    {
        { "C1", 5 },
        { "C1B", 5 },
        { "C2", 9 },
        { "C3", 4 },
        { "C4", 14 },
        { "C5", 17 },
    };

    [Fact]
    public void ABotSteersForTheLeadInThenHandsTheZoneToItsPilot()
    {
        var zones = Course();
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99));

        // Far off to the side: it steers for the lead-in short of the nearer end, high while far.
        var far = new Vector3(3000, 400, 2000);
        var steer = bot.Decide(zones, far, North, zoneRun: false);
        Assert.Equal(StuntBotLeg.Steer, steer.Leg);
        Assert.Equal(0, steer.Zone);
        Assert.False(steer.FarEnd);
        var leadIn = FirstNear + new Vector3(0, 0, StuntBotObjective.LeadInM);
        Assert.Equal(leadIn.X, steer.Point.X, 3);
        Assert.Equal(leadIn.Z, steer.Point.Z, 3);
        Assert.Equal(FirstNear.Y + StuntBotObjective.CruiseAboveM, steer.Point.Y, 3);

        // ABLE-TO-FAIL CONTROL: in the cone, tracking toward the entry, it starts the run there.
        var run = bot.Decide(zones, FirstNear + new Vector3(10, 0, 500), North, zoneRun: false);
        Assert.True(run is { Leg: StuntBotLeg.Run, StartsRun: true, Zone: 0, FarEnd: false });
        Assert.Equal(FirstNear, run.Point);

        // While the pilot flies it the order holds without starting another.
        Assert.True(bot.Decide(zones, FirstFar, North, zoneRun: true) is { Leg: StuntBotLeg.Run, StartsRun: false });
    }

    [Fact]
    public void TheConeWantsTheBotShortOfTheEntryAndTrackingToward()
    {
        var zones = Course();
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99));
        Assert.Equal(StuntBotLeg.Steer, bot.Decide(zones, FirstNear + new Vector3(10, 0, 500), -North, zoneRun: false).Leg);
        Assert.Equal(StuntBotLeg.Steer, bot.Decide(zones, FirstNear + new Vector3(400, 0, 500), North, zoneRun: false).Leg);
        Assert.Equal(StuntBotLeg.Steer, bot.Decide(zones, FirstNear + new Vector3(0, 0, 50), North, zoneRun: false).Leg);
        Assert.Equal(StuntBotLeg.Run, bot.Decide(zones, FirstNear + new Vector3(0, 0, 500), North, zoneRun: false).Leg);
    }

    [Fact]
    public void ZonesGoInCourseOrderEachFromItsNearerEnd()
    {
        var zones = Course();
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99));
        zones[0].Completed = true;
        var next = bot.Decide(zones, new Vector3(0, 100, -3000), -North, zoneRun: false);
        Assert.True(next is { Zone: 1, FarEnd: true });

        // ABLE-TO-FAIL CONTROL: with the first zone still open it is the first, whatever is nearer.
        zones[0].Completed = false;
        Assert.Equal(0, new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)).Decide(zones, new Vector3(0, 100, -3000), -North, zoneRun: false).Zone);
    }

    [Fact]
    public void AFluffPassesOverTheEntryAndTriesAgain()
    {
        var zones = Course();
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.0));
        var fluff = bot.Decide(zones, FirstNear + new Vector3(0, 0, 500), North, zoneRun: false);
        Assert.Equal(StuntBotLeg.Fluff, fluff.Leg);
        Assert.Equal(FirstNear + (Vector3.Up * StuntBotObjective.FluffAboveM), fluff.Point);
        Assert.Equal(StuntBotLeg.Fluff, bot.Decide(zones, FirstNear + new Vector3(0, 50, 200), North, zoneRun: false).Leg);

        // Past the entry the next try picks its end afresh, and steers for it.
        var after = bot.Decide(zones, FirstFar + new Vector3(0, 100, -300), North, zoneRun: false);
        Assert.True(after is { Leg: StuntBotLeg.Steer, FarEnd: true });

        // ABLE-TO-FAIL CONTROL: a draw above the tier's odds flies the zone.
        Assert.Equal(StuntBotLeg.Run, new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.06)).Decide(zones, FirstNear + new Vector3(0, 0, 500), North, zoneRun: false).Leg);
        Assert.Equal(StuntBotLeg.Fluff, new StuntBotObjective(NetBotSkill.Novice, new Fixed(0.06)).Decide(zones, FirstNear + new Vector3(0, 0, 500), North, zoneRun: false).Leg);
    }

    [Fact]
    public void ATierSetsTheLeverAndTheOdds()
    {
        var ace = new StuntBotObjective(NetBotSkill.Ace, new Random(1));
        var veteran = new StuntBotObjective(NetBotSkill.Veteran, new Random(1));
        var novice = new StuntBotObjective(NetBotSkill.Novice, new Random(1));
        Assert.True(ace.CruiseThrottle > veteran.CruiseThrottle && veteran.CruiseThrottle > novice.CruiseThrottle);
        Assert.True(ace.FluffChance < veteran.FluffChance && veteran.FluffChance < novice.FluffChance);
    }

    [Fact]
    public void AZoneWithNoRibbonIsPassedOverAndNoneLeftHoldsCourse()
    {
        var zones = Course();
        zones[0].Route = null;
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99));
        Assert.Equal(1, bot.Decide(zones, FirstNear, North, zoneRun: false).Zone);

        // ABLE-TO-FAIL CONTROL: with every zone flown or unflyable there is nothing to order.
        zones[1].Completed = true;
        Assert.Equal(StuntBotLeg.None, bot.Decide(zones, FirstNear, North, zoneRun: false).Leg);
    }

    /// <summary>Every zone of a shipped course builds the route ribbon a bot runs, so no zone lacks
    /// the rail a bot rides through it.</summary>
    [ExtractedDataTheory]
    [MemberData(nameof(StuntCourses))]
    public void EveryShippedCourseZoneBuildsARibbon(string chapter, int zones)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var course = StuntMission.Load(gamez, SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, "IA1"),
            Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json")));
        Assert.NotNull(course);
        Assert.Equal(zones, course!.TotalCount);
        Assert.All(course.Zones, zone => Assert.NotNull(zone.Route));
    }

    private static List<StuntZone> Course() => new()
    {
        Zone("dzpath1", 1, FirstNear, FirstFar),
        Zone("dzpath2", 2, SecondNear, SecondFar),
    };

    private static StuntZone Zone(string name, int index, Vector3 near, Vector3 far) => new()
    {
        PathName = name,
        Route = DangerZoneRibbon.FromPolyline(name, index, new[] { near, (near + far) * 0.5f, far }),
    };

    // A draw that always answers the same, so a fluff roll is chosen rather than rolled.
    private sealed class Fixed(double value) : Random
    {
        public override double NextDouble() => value;

        protected override double Sample() => value;
    }
}
