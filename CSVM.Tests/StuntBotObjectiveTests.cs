using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hangar;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Utils;
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

    private static string ZrdrPath => SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

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

        // Far off to the side and flying west, it cannot meet the line short of the entry. It heads out
        // along the line, aiming 400 m beyond its own place on it, high while far.
        var far = new Vector3(3000, 400, 2000);
        var steer = bot.Decide(zones, far, new Vector3(-70, 0, 0), zoneRun: false);
        Assert.Equal(StuntBotLeg.Steer, steer.Leg);
        Assert.Equal(0, steer.Zone);
        Assert.False(steer.FarEnd);
        var outward = FirstNear + new Vector3(0, 0, 2000 + StuntBotObjective.LookAheadM);
        Assert.Equal(AiPilot.HeadingDegOf(outward - far), AiPilot.HeadingDegOf(steer.Point - far), 3);
        Assert.Equal(FirstNear.Y + StuntBotObjective.CruiseAboveM, steer.Point.Y, 3);

        // ABLE-TO-FAIL CONTROL: in the cone, tracking toward the entry, it starts the run there.
        var run = bot.Decide(zones, FirstNear + new Vector3(10, 0, 500), North, zoneRun: false);
        Assert.True(run is { Leg: StuntBotLeg.Run, StartsRun: true, Zone: 0, FarEnd: false });
        Assert.Equal(FirstNear, run.Point);

        // While the pilot flies it the order holds without starting another.
        Assert.True(bot.Decide(zones, FirstFar, North, zoneRun: true) is { Leg: StuntBotLeg.Run, StartsRun: false });
    }

    [Fact]
    public void ABotCrossingTheLeadInSettlesOntoItsLineRatherThanCirclingIt()
    {
        // Over the lead-in point flying east, across the line, it turns in along the line toward the
        // entry. It does not come round to the point it crossed.
        var zones = Course();
        var east = new Vector3(70, 0, 0);
        var at = FirstNear + new Vector3(0, 0, StuntBotObjective.LeadInM);
        var steer = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)).Decide(zones, at, east, zoneRun: false);
        Assert.Equal(StuntBotLeg.Steer, steer.Leg);
        Assert.Equal(AiPilot.HeadingDegOf(FirstNear - at), AiPilot.HeadingDegOf(steer.Point - at), 3);

        // ABLE-TO-FAIL CONTROL: 500 m aside, outside the cone, it aims at the line as far ahead as it
        // is off, not at the entry.
        var aside = at + new Vector3(500, 0, 0);
        var settle = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)).Decide(zones, aside, North, zoneRun: false);
        var ahead = FirstNear + new Vector3(0, 0, StuntBotObjective.LeadInM - 500);
        Assert.Equal(AiPilot.HeadingDegOf(ahead - aside), AiPilot.HeadingDegOf(settle.Point - aside), 3);
        Assert.NotEqual(AiPilot.HeadingDegOf(FirstNear - aside), AiPilot.HeadingDegOf(settle.Point - aside), 0);
    }

    [Fact]
    public void AnAimAsternTurnsTheBotNinetyDegreesAndHoldsTheSide()
    {
        // Just past the entry, flying on along the ribbon: the line's far point is dead astern.
        var zones = Course();
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99));
        var at = FirstNear + new Vector3(0, 0, -50);
        float first = AiPilot.HeadingDegOf(bot.Decide(zones, at, North, zoneRun: false).Point - at);
        Assert.Equal(90f, Mathf.Abs(first), 3);

        // A step later, drifted a little the other way, the turn keeps its side.
        var drifted = at + new Vector3(-5, 0, 0);
        Assert.Equal(first, AiPilot.HeadingDegOf(bot.Decide(zones, drifted, North, zoneRun: false).Point - drifted), 3);

        // ABLE-TO-FAIL CONTROL: with the aim ahead of the track it steers straight for it.
        var behind = FirstNear + new Vector3(0, 0, 2000);
        Assert.Equal(0f, AiPilot.HeadingDegOf(bot.Decide(zones, behind, North, zoneRun: false).Point - behind), 3);
    }

    [Fact]
    public void EachBotFliesItsOwnApproach()
    {
        // The spread is the seeded stream's, so a field repeats, and it stays inside its bounds.
        Assert.Equal(StuntBotObjective.Spread(new Random(7)), StuntBotObjective.Spread(new Random(7)));
        var rng = new Random(7);
        var drawn = Enumerable.Range(0, 64).Select(_ => StuntBotObjective.Spread(rng)).ToList();
        Assert.All(drawn, d => Assert.InRange(d.BearingDeg, -StuntBotObjective.ApproachSpreadDeg, StuntBotObjective.ApproachSpreadDeg));
        Assert.All(drawn, d => Assert.InRange(d.LeadInM, StuntBotObjective.LeadInM - StuntBotObjective.LeadInSpreadM, StuntBotObjective.LeadInM + StuntBotObjective.LeadInSpreadM));

        // Two bots from one point steer for different lines into the same entry.
        var zones = Course();
        var at = FirstNear + new Vector3(0, 0, 2000);
        var own = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Approach = (StuntBotObjective.ApproachSpreadDeg, 750f) };
        var plain = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99));
        Assert.NotEqual(AiPilot.HeadingDegOf(plain.Decide(zones, at, North, zoneRun: false).Point - at),
            AiPilot.HeadingDegOf(own.Decide(zones, at, North, zoneRun: false).Point - at), 0);

        // ABLE-TO-FAIL CONTROL: two bots on the default approach steer alike.
        Assert.Equal(AiPilot.HeadingDegOf(plain.Decide(zones, at, North, zoneRun: false).Point - at),
            AiPilot.HeadingDegOf(new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)).Decide(zones, at, North, zoneRun: false).Point - at), 3);
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
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Runs = StuntBotObjective.RunsFrom(zones, (_, to) => to == FirstNear) };
        Assert.True(bot.Decide(zones, FirstNear + new Vector3(0, 0, 700), North, zoneRun: false).FarEnd);

        // ABLE-TO-FAIL CONTROL: with nothing blocked it enters from the nearer first vertex.
        var open = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Runs = StuntBotObjective.RunsFrom(zones, blocked: null) };
        Assert.False(open.Decide(zones, FirstNear + new Vector3(0, 0, 700), North, zoneRun: false).FarEnd);
    }

    [Fact]
    public void AShutEndSendsTheBotRoundToTheOtherUntilItOpens()
    {
        var zones = new List<StuntZone> { Zone("dzpath1", 1, FirstNear, FirstFar) };
        bool standing = true;
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Shut = (_, far) => standing && !far };
        Assert.True(bot.Decide(zones, FirstNear + new Vector3(0, 0, 700), North, zoneRun: false).FarEnd);

        // ABLE-TO-FAIL CONTROL: the structure down, a fresh pick enters from the nearer first vertex.
        standing = false;
        bot.Returned();
        Assert.False(bot.Decide(zones, FirstNear + new Vector3(0, 0, 700), North, zoneRun: false).FarEnd);
    }

    [Fact]
    public void AnEndMissingFromTheTableRunsItsOwnRoute()
    {
        // A table without the second zone: the bot warns and flies that zone's route as it is.
        var zones = Course();
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Runs = StuntBotObjective.RunsFrom(zones.Take(1), blocked: null) };
        var run = bot.Decide(zones, SecondNear + new Vector3(0, 0, 500), North, zoneRun: false);
        Assert.True(run is { Leg: StuntBotLeg.Run, Zone: 1 });
        Assert.Same(zones[1].Route, run.Ribbon);
    }

    [Fact]
    public void NearestReadsTheRunAPickTakesARefusedEndIncluded()
    {
        // The first zone's nearer end is refused, so its run enters from the far end, whose lead-in
        // point is some 1500 m off. The second zone's is 500 m off.
        var zones = new List<StuntZone>
        {
            Zone("dzpath1", 1, FirstNear, FirstFar, gateHalfM: 4f, gateAtM: 30f),
            Zone("dzpath2", 2, new Vector3(300, 100, -300), new Vector3(300, 100, -500)),
        };
        var at = new Vector3(0, 100, 700);
        var refused = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Runs = StuntBotObjective.RunsFrom(zones, (_, to) => to == FirstNear) };
        Assert.Equal(1, refused.Decide(zones, at, North, zoneRun: false).Zone);

        // ABLE-TO-FAIL CONTROL: with that end open, the first zone's lead-in point is the nearer.
        var open = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99)) { Runs = StuntBotObjective.RunsFrom(zones, blocked: null) };
        Assert.Equal(0, open.Decide(zones, at, North, zoneRun: false).Zone);
    }

    /// <summary>Flown on the shipped stats: a go-round's U-turn width is twice the widest turn radius under
    /// the 90 degree order. That covers every stock plane at every tier's lever. The stick is the
    /// weakest a rating gives. The width is rounded up to 5 m, no more.</summary>
    [ExtractedDataFact]
    public void TheUTurnWidthIsTheWidestPlanesUTurn()
    {
        var radii = new List<(string Plane, NetBotSkill Tier, float Radius)>();
        foreach (string plane in StockAirframes.Nodes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            foreach (var tier in new[] { NetBotSkill.Novice, NetBotSkill.Veteran, NetBotSkill.Ace })
            {
                radii.Add((plane, tier, TurnRadius(stats, new StuntBotObjective(tier, new Random(1)).CruiseThrottle)));
            }
        }

        var widest = radii.MaxBy(r => r.Radius);
        Assert.True(2f * widest.Radius <= StuntBotObjective.UTurnWidthM && StuntBotObjective.UTurnWidthM - (2f * widest.Radius) < 5f,
            string.Create(CultureInfo.InvariantCulture, $"widest U-turn {2f * widest.Radius:0.0} m ({widest.Plane} {widest.Tier}) against {StuntBotObjective.UTurnWidthM} m"));
    }

    /// <summary>Flown on the shipped stats: an ace bot of every stock plane goes round from beside or past
    /// the entry. An ace's lever turns widest. Its lead-in is the shortest. It turns in once and reaches
    /// the cone. ABLE-TO-FAIL CONTROL: allowing for no U-turn, it turns in at its lead-in. The same
    /// flights then go round again, since the U-turn ends short of the fly-out.</summary>
    [ExtractedDataFact]
    public void ABotGoingRoundTurnsInOnceAndReachesTheCone()
    {
        var starts = new (float X, float Z, float HeadingDeg)[] { (0, 0, 180), (300, 0, 0), (-300, -300, 180), (0, -300, 0), (600, 0, 270), (0, 600, 90) };
        var flown = new List<string>();
        var control = new List<string>();
        foreach (string plane in StockAirframes.Nodes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            foreach (float bearing in new[] { -StuntBotObjective.ApproachSpreadDeg, StuntBotObjective.ApproachSpreadDeg })
            {
                foreach (var (x, z, heading) in starts)
                {
                    var at = new Vector3(x, 250, z);
                    var (outs, reached) = GoRound(stats, at, heading, bearing, StuntBotObjective.UTurnWidthM);
                    if (outs > 0 || !reached)
                    {
                        flown.Add(string.Create(CultureInfo.InvariantCulture, $"{plane} {bearing} {at} {heading}: out again {outs}, reached {reached}"));
                    }

                    if (GoRound(stats, at, heading, bearing, 0f).OutAgain > 0)
                    {
                        control.Add(plane);
                    }
                }
            }
        }

        Assert.True(flown.Count == 0, string.Join("; ", flown));
        Assert.NotEmpty(control);
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

    // A pilot at `throttle` on the weakest stick any rating gives (sixth_sense_factor scales all three channels).
    private static AiPilot Pilot(float heading, float altitude, float throttle)
    {
        var skills = AiSkills.Load(ZrdrPath);
        float weakest = Math.Min(skills.At("sixth_sense_factor", 0f), skills.At("sixth_sense_factor", 9f));
        return new AiPilot
        {
            TargetHeadingDeg = heading,
            TargetAltitude = altitude,
            CruiseThrottle = throttle,
            Machine = new AiModeMachine(new Random(1)) { SixthSenseFactor = weakest },
        };
    }

    // The steady radius under an order held 90 degrees off the track, metres. It is the path flown over
    // the turn after its first quarter, from 20 s of level flight at `throttle`.
    private static float TurnRadius(PlaneStats stats, float throttle)
    {
        var model = new FlightModel(stats);
        model.Reset(new Vector3(0, 500, 0), Basis.Identity, 80f, throttle);
        var pilot = Pilot(0f, 500f, throttle);
        for (int i = 0; i < Steps(20f); i++)
        {
            model.Step(pilot.Next(model, GameClock.FixedDt), GameClock.FixedDt);
        }

        float turned = 0f;
        float path = 0f;
        float last = Track(model);
        for (int i = 0; i < Steps(120f) && Mathf.Abs(turned) < 2f * Mathf.Tau; i++)
        {
            pilot.TargetHeadingDeg = AiPilot.HeadingDegOf(new Basis(Vector3.Up, -Mathf.Pi / 2f) * model.VelocityDir);
            model.Step(pilot.Next(model, GameClock.FixedDt), GameClock.FixedDt);
            turned += Mathf.Wrap(Track(model) - last, -Mathf.Pi, Mathf.Pi);
            last = Track(model);
            path += Mathf.Abs(turned) > Mathf.Pi / 2f ? model.Speed * GameClock.FixedDt : 0f;
        }

        return path / (Mathf.Abs(turned) - (Mathf.Pi / 2f));
    }

    // An ace bot from `at` flying `heading` for Course()'s first zone, on the shortest lead-in and a line
    // `bearing` off the ribbon's. It flies 2 minutes or until it hands the zone over. Answers how often it
    // flew out again once it had turned in from going round, and whether it reached the cone.
    private static (int OutAgain, bool Reached) GoRound(PlaneStats stats, Vector3 at, float heading, float bearing, float uTurn)
    {
        var bot = new StuntBotObjective(NetBotSkill.Ace, new Fixed(0.99))
        {
            Approach = (bearing, StuntBotObjective.ShortestLeadInM),
            UTurnWidth = uTurn,
        };
        var zones = Course();
        var model = new FlightModel(stats);
        model.Reset(at, Basis.LookingAt(new Basis(Vector3.Up, Mathf.DegToRad(heading)) * Vector3.Forward, Vector3.Up), 80f, bot.CruiseThrottle);
        var pilot = Pilot(heading, at.Y, bot.CruiseThrottle);
        bool turnedIn = false;
        int outAgain = 0;
        string? why = null;
        for (int i = 0; i < Steps(120f); i++)
        {
            var order = bot.Decide(zones, model.Position, model.VelocityDir * model.Speed, zoneRun: false);
            if (order.Leg == StuntBotLeg.Run)
            {
                return (outAgain, true);
            }

            bool steersIn = order.Why == StuntBotObjective.SteersIn;
            bool steeredIn = why == StuntBotObjective.SteersIn;
            bool turnsIn = steersIn && why != null && !steeredIn;
            outAgain += turnedIn && steeredIn && !steersIn ? 1 : 0;
            turnedIn |= turnsIn;
            why = order.Why;
            pilot.TargetHeadingDeg = AiPilot.HeadingDegOf(order.Point - model.Position);
            pilot.TargetAltitude = order.Point.Y;
            model.Step(pilot.Next(model, GameClock.FixedDt), GameClock.FixedDt);
        }

        return (outAgain, false);
    }

    private static float Track(FlightModel model) => Mathf.Atan2(model.VelocityDir.X, model.VelocityDir.Z);

    private static int Steps(float seconds) => Mathf.RoundToInt(seconds / GameClock.FixedDt);

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
