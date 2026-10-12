using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Per shipped course, the zone ends whose rail run needs a lead-in, and its metres.</summary>
    private static Dictionary<string, string> LeadIns => new()
    {
        ["C1"] = "",
        ["C1B"] = "dzpath6 50, dzpath7 50",
        ["C2"] = "dzpath4 50",
        ["C3"] = "dzpath4 100, dzpath4 far 50",
        ["C4"] = "dzpath7 50, dzpath9 50, dzpath11 150, dzpath11 far 100, dzpath12 50",
        ["C5"] = "dzpath14 200, dzpath14 far 150, dzpath15 200, dzpath15 far 150, dzpath16 50, dzpath16 far 50",
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
    public void ZonesGoNearestFirstEachFromItsNearerEnd()
    {
        var zones = Zigzag();
        zones[0].Completed = true;
        var leftFirst = new Vector3(0, 100, -300);
        Assert.True(Ace().Decide(zones, leftFirst, North, zoneRun: false) is { Zone: 2, FarEnd: false });

        // ABLE-TO-FAIL CONTROL: past the last zone in the list, that zone is nearest, from its far end.
        Assert.True(Ace().Decide(zones, new Vector3(0, 100, -5000), -North, zoneRun: false) is { Zone: 1, FarEnd: true });
    }

    [Fact]
    public void NearestIsMeasuredToTheLeadInPointNotTheZone()
    {
        // The first zone's middle is 200 m away, but its lead-in point is 500 m behind the bot. The
        // second's middle is 850 m away and its lead-in point some 320 m.
        var zones = new List<StuntZone>
        {
            Zone("dzpath1", 1, new Vector3(0, 100, -100), new Vector3(0, 100, -300)),
            Zone("dzpath2", 2, new Vector3(300, 100, -700), new Vector3(300, 100, -900)),
        };
        Assert.Equal(1, Ace().Decide(zones, new Vector3(0, 100, 0), North, zoneRun: false).Zone);

        // ABLE-TO-FAIL CONTROL: from the first zone's lead-in point, that zone is the nearer.
        Assert.Equal(0, Ace().Decide(zones, new Vector3(0, 100, 500), North, zoneRun: false).Zone);
    }

    [Fact]
    public void APickHoldsUntilATryEndsOrAReturn()
    {
        var zones = Zigzag();
        zones[0].Completed = true;
        var bot = Ace();
        Assert.Equal(2, bot.Decide(zones, new Vector3(0, 100, -300), North, zoneRun: false).Zone);

        // Nearer the other zone now, it keeps the one it picked.
        var beyond = new Vector3(0, 100, -5500);
        Assert.Equal(2, bot.Decide(zones, beyond, -North, zoneRun: false).Zone);

        // A ride and its end: the next pick is from where the ride ends.
        bot.Decide(zones, beyond, -North, zoneRun: true);
        Assert.Equal(1, bot.Decide(zones, beyond, -North, zoneRun: false).Zone);

        // ABLE-TO-FAIL CONTROL: nearer the first pick again, it holds the second until a return.
        var back = new Vector3(0, 100, -1300);
        Assert.Equal(1, bot.Decide(zones, back, North, zoneRun: false).Zone);
        bot.Returned();
        Assert.Equal(2, bot.Decide(zones, back, North, zoneRun: false).Zone);
    }

    [Fact]
    public void AFluffKeepsItsZoneWhateverIsNearer()
    {
        var zones = Zigzag();
        zones[0].Completed = true;
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.0));
        Assert.True(bot.Decide(zones, new Vector3(0, 100, -1500), North, zoneRun: false) is { Leg: StuntBotLeg.Fluff, Zone: 2 });

        // Past the entry and on toward the other zone, the fluffed one is still the order.
        Assert.True(bot.Decide(zones, new Vector3(0, 200, -3300), North, zoneRun: false) is { Zone: 2, FarEnd: true });

        // ABLE-TO-FAIL CONTROL: a bot with no fluff to keep takes the nearer zone from there.
        Assert.Equal(1, Ace().Decide(zones, new Vector3(0, 200, -3300), North, zoneRun: false).Zone);
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

    [Fact]
    public void AZoneTheRailMissesTakesTheShortestLeadInThatPasses()
    {
        // Narrow gates 30 m in: a lock 105 m out at a bearing has not closed its offset by then.
        var zone = Zone("dzpath1", 1, FirstNear, FirstFar, gateHalfM: 4f, gateAtM: 30f);
        var lead = StuntBotObjective.LeadInFor(zone, far: false, blocked: null);
        Assert.True(lead is > 0f);
        Assert.True(StuntBotObjective.RailClears(zone, zone.Route!.WithLeadIn(false, lead!.Value), far: false));
        Assert.False(StuntBotObjective.RailClears(zone, zone.Route.WithLeadIn(false, lead.Value - StuntBotObjective.LeadInStepM), far: false));

        // ABLE-TO-FAIL CONTROL: the same zone without the lead-in fails the check.
        Assert.False(StuntBotObjective.RailClears(zone, zone.Route, far: false));

        // The wide gates of the default zone need none.
        Assert.Equal(0f, StuntBotObjective.LeadInFor(Course()[0], far: false, blocked: null));
    }

    [Fact]
    public void ABlockedLeadInIsRefusedAndTheBotTakesTheOtherEnd()
    {
        var zones = new List<StuntZone> { Zone("dzpath1", 1, FirstNear, FirstFar, gateHalfM: 4f, gateAtM: 30f) };
        Assert.Null(StuntBotObjective.LeadInFor(zones[0], far: false, blocked: (_, to) => to == FirstNear));

        // Nearer the first vertex, but its straight is blocked: the run enters from the last.
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Blocked = (_, to) => to == FirstNear };
        Assert.True(bot.Decide(zones, FirstNear + new Vector3(0, 0, 700), North, zoneRun: false).FarEnd);

        // ABLE-TO-FAIL CONTROL: with nothing blocked it enters from the nearer first vertex.
        Assert.False(new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)).Decide(zones, FirstNear + new Vector3(0, 0, 700), North, zoneRun: false).FarEnd);
    }

    /// <summary>On every shipped course each zone has an end the rail check passes, with the shortest
    /// lead-in that passes. The zones that need one are pinned; every other zone keeps its own entry.</summary>
    [ExtractedDataTheory]
    [MemberData(nameof(StuntCourses))]
    public void EveryShippedZoneHasAnEntryTheRailCarriesThrough(string chapter, int zones)
    {
        var course = Load(chapter);
        Assert.Equal(zones, course.TotalCount);
        var led = new List<string>();
        foreach (var zone in course.Zones)
        {
            var leads = new[] { false, true }.Select(far => StuntBotObjective.LeadInFor(zone, far, blocked: null)).ToArray();
            Assert.True(leads.Any(l => l != null), $"{chapter} {zone.PathName} has no end the rail carries through");
            for (int end = 0; end < 2; end++)
            {
                if (leads[end] is > 0f and var metres)
                {
                    led.Add($"{zone.PathName}{(end == 1 ? " far" : "")} {metres:0}");
                    Assert.True(StuntBotObjective.RailClears(zone, zone.Route!.WithLeadIn(end == 1, metres), end == 1));

                    // ABLE-TO-FAIL CONTROL: without the lead-in the same zone fails the check.
                    Assert.False(StuntBotObjective.RailClears(zone, zone.Route!, end == 1));
                }
            }
        }

        Assert.Equal(LeadIns[chapter], string.Join(", ", led));
    }

    /// <summary>Every zone of a shipped course builds the route ribbon a bot runs, so no zone lacks
    /// the rail a bot rides through it.</summary>
    [ExtractedDataTheory]
    [MemberData(nameof(StuntCourses))]
    public void EveryShippedCourseZoneBuildsARibbon(string chapter, int zones)
    {
        var course = Load(chapter);
        Assert.Equal(zones, course.TotalCount);
        Assert.All(course.Zones, zone => Assert.NotNull(zone.Route));
    }

    private static StuntMission Load(string chapter)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var course = StuntMission.Load(gamez, SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, "IA1"),
            Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json")));
        Assert.NotNull(course);
        return course!;
    }

    private static List<StuntZone> Course() => new()
    {
        Zone("dzpath1", 1, FirstNear, FirstFar),
        Zone("dzpath2", 2, SecondNear, SecondFar),
    };

    // Three zones on one line whose list order zigzags: the second listed lies beyond the third.
    private static List<StuntZone> Zigzag() => new()
    {
        Zone("dzpath1", 1, FirstNear, FirstFar),
        Zone("dzpath2", 2, new Vector3(0, 100, -4000), new Vector3(0, 100, -4200)),
        Zone("dzpath3", 3, SecondNear, SecondFar),
    };

    private static StuntBotObjective Ace() => new(NetBotSkill.Ace, new Fixed(0.99));

    // A zone on a straight ribbon with its two gates either side of a point along it. By default they
    // are wide enough for the rail to pass them from any lock.
    private static StuntZone Zone(string name, int index, Vector3 near, Vector3 far, float gateHalfM = 200f, float gateAtM = 100f)
    {
        var along = (far - near).Normalized();
        var middle = near + (along * gateAtM);
        return new StuntZone
        {
            PathName = name,
            Route = DangerZoneRibbon.FromPolyline(name, index, new[] { near, (near + far) * 0.5f, far }),
            GreenGate = Gate(middle - (along * 5f), gateHalfM),
            RedGate = Gate(middle + (along * 5f), gateHalfM),
        };
    }

    // A square ring across a north-south ribbon.
    private static StuntGate Gate(Vector3 center, float half) => new()
    {
        Center = center,
        Normal = Vector3.Back,
        Vertices = new[]
        {
            center + new Vector3(-half, -half, 0), center + new Vector3(half, -half, 0),
            center + new Vector3(half, half, 0), center + new Vector3(-half, half, 0),
        },
    };

    // A draw that always answers the same, so a fluff roll is chosen rather than rolled.
    private sealed class Fixed(double value) : Random
    {
        public override double NextDouble() => value;

        protected override double Sample() => value;
    }
}
