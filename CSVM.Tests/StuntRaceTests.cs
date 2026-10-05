using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CSVM.Flight.Modes;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The time-attack stunt race's bookkeeping, off-engine. The tests cover the window clock and its
/// opening, best-run ranking, the final run and its cap, and the local feed.
/// The race logs through <c>Log</c>, safe under <c>TestHostLogSink</c>.
/// ⚠ Never call <see cref="StuntMission"/>'s <c>Load</c> directly: it reaches the engine and
/// crashes the test host. A fake mission comes from its private constructor via reflection.
/// </summary>
public class StuntRaceTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void TheWindowClockStartsAtTheOpeningCountsGo()
    {
        var race = new StuntRace(60f, 3);
        race.Add(0, "Bloodhawk");
        race.BeginOpening(5f); // READY 2 s then 3, 2, 1, the session's opening

        for (int step = 1; step < 300; step++)
        {
            race.Advance(Dt);
            Assert.Equal(StuntRacePhase.Opening, race.Phase);
            Assert.False(race.MayStartRun);
            Assert.False(race.RunStarted(0));
        }
        race.Advance(Dt);
        Assert.Equal(StuntRacePhase.Open, race.Phase);
        Assert.Equal(0f, race.WindowElapsed);
        Assert.Equal(60f, race.TimeLeft);

        Assert.True(race.RunStarted(0));
        race.Advance(Dt);
        Assert.Equal(Dt, race.WindowElapsed);
    }

    [Fact]
    public void NoOpeningOpensTheWindowAtOnce()
    {
        var race = new StuntRace(60f, 3);
        race.Add(0, "Bloodhawk");
        race.BeginOpening(0f);
        Assert.Equal(StuntRacePhase.Open, race.Phase);
        Assert.True(race.RunStarted(0));
    }

    [Fact]
    public void FinishersRankByBestRunNotByWhenTheyFinished()
    {
        var race = OpenRace(3, out var a, out var b, out var c);
        FlyRun(race, 2, new[] { 4f, 8f, 12f }); // C first, and slowest
        FlyRun(race, 0, new[] { 3f, 6f, 9f });
        FlyRun(race, 1, new[] { 2f, 5f, 10f });

        Assert.Equal(new[] { a, b, c }, race.Standings());
        Assert.Equal(1, race.PlaceOf(0));
        Assert.Equal(3, race.PlaceOf(2));
    }

    [Fact]
    public void PilotsWithNoCompletedRunRankBelowEveryFinisherByMostZonesThenTimeToThem()
    {
        var race = new StuntRace(600f, 3);
        var finisher = race.Add(0, "Bloodhawk");
        var two = race.Add(1, "Kestrel");
        var twoFaster = race.Add(2, "Hoplite");
        var one = race.Add(3, "Autogyro");
        race.BeginOpening(0f);

        FlyRun(race, 0, new[] { 50f, 90f, 99f }); // slow, but the only completed run
        PartialRun(race, 1, new[] { (0, 10f), (1, 30f) });
        // Two zones in an earlier run, faster to them than P2, then a rerun that gets only one.
        PartialRun(race, 2, new[] { (2, 8f), (0, 20f) });
        race.RunAbandoned(2);
        PartialRun(race, 2, new[] { (1, 4f) });
        PartialRun(race, 3, new[] { (2, 1f) });

        Assert.Equal(new[] { finisher, twoFaster, two, one }, race.Standings());
        Assert.Equal(2, twoFaster.MostZones);
        Assert.Equal(20f, twoFaster.TimeToMostZones);
        Assert.Equal(new float?[] { 20f, null, 8f }, twoFaster.Splits);
    }

    [Fact]
    public void ARerunKeepsTheBestAndOnlyAFasterRunReplacesIt()
    {
        var race = OpenRace(3, out var a, out _, out _);
        FlyRun(race, 0, new[] { 3f, 6f, 9f });
        race.RunAbandoned(0); // the rerun's reset after a completed run throws nothing away
        Assert.Equal(9f, a.BestTime);

        PartialRun(race, 0, new[] { (0, 1f) });
        race.RunAbandoned(0); // a held rerun mid-run
        Assert.Equal(9f, a.BestTime);
        Assert.Equal(new float?[] { 3f, 6f, 9f }, a.Splits);

        FlyRun(race, 0, new[] { 4f, 8f, 12f }); // slower: the best stands
        Assert.Equal(9f, a.BestTime);
        Assert.Equal(new float?[] { 3f, 6f, 9f }, a.Splits);

        int improved = 0;
        race.BestImproved += _ => improved++;
        FlyRun(race, 0, new[] { 2f, 4f, 7f });
        Assert.Equal(7f, a.BestTime);
        Assert.Equal(new float?[] { 2f, 4f, 7f }, a.Splits);
        Assert.Equal(1, improved);
        Assert.Equal(4, a.RunsStarted);
        Assert.Equal(3, a.RunsFinished);
    }

    [Fact]
    public void NoRunStartsOrRerunsAfterTimeUp()
    {
        var race = new StuntRace(1f, 3);
        var a = race.Add(0, "Bloodhawk");
        var b = race.Add(1, "Kestrel");
        race.BeginOpening(0f);
        Assert.True(race.RunStarted(0));
        Advance(race, 1f);

        Assert.Equal(StuntRacePhase.FinalRun, race.Phase);
        Assert.False(race.MayStartRun);
        Assert.False(race.RunStarted(1));
        Assert.False(b.InRun);
        Assert.Equal(0, b.RunsStarted);
        Assert.True(a.InRun);
        Assert.StartsWith("FINAL RUN 2:00   ", race.LeaderboardLine(1), StringComparison.Ordinal);
    }

    [Fact]
    public void ARunInProgressAtTimeUpMayFinishInsideTheCapAndStillCounts()
    {
        var race = new StuntRace(1f, 3);
        var a = race.Add(0, "Bloodhawk");
        race.Add(1, "Kestrel");
        race.BeginOpening(0f);
        race.RunStarted(0);
        race.ZoneCleared(0, 0, 0.5f);
        Advance(race, 1f + StuntRace.FinalRunCap - 0.5f);
        Assert.Equal(StuntRacePhase.FinalRun, race.Phase);
        Assert.True(race.FinalRunLeft > 0f);

        int completed = 0;
        race.RaceCompleted += () => completed++;
        race.ZoneCleared(0, 1, 100f);
        race.ZoneCleared(0, 2, 120f);
        race.RunFinished(0, 120f);

        Assert.Equal(120f, a.BestTime);
        Assert.True(race.Ended);
        Assert.Equal(1, completed); // the last run in progress finishing ends it early
    }

    [Fact]
    public void TheFinalRunEndsAtTheCapAndARunStillGoingThenDoesNotCount()
    {
        var race = new StuntRace(1f, 3);
        var a = race.Add(0, "Bloodhawk");
        race.BeginOpening(0f);
        race.RunStarted(0);
        race.ZoneCleared(0, 0, 0.5f);
        int completed = 0;
        race.RaceCompleted += () => completed++;

        Advance(race, 1f + StuntRace.FinalRunCap - 0.1f);
        Assert.Equal(StuntRacePhase.FinalRun, race.Phase);
        Advance(race, 0.2f);
        Assert.True(race.Ended);
        Assert.Equal(1, completed);

        race.ZoneCleared(0, 1, 121f);
        race.RunFinished(0, 122f);
        Assert.Null(a.BestTime);
        Assert.False(a.InRun);
        Assert.Equal(1, a.MostZones);
    }

    [Fact]
    public void TimeUpWithNoRunInProgressEndsTheRaceAtOnce()
    {
        var race = new StuntRace(1f, 3);
        race.Add(0, "Bloodhawk");
        race.BeginOpening(0f);
        FlyRun(race, 0, new[] { 0.1f, 0.2f, 0.3f });
        Advance(race, 1f);
        Assert.True(race.Ended);
    }

    [Fact]
    public void ARerunRaceClearsEveryRunAndWaitsForItsOpening()
    {
        var race = OpenRace(3, out var a, out _, out _);
        FlyRun(race, 0, new[] { 3f, 6f, 9f });
        race.Rerun();

        Assert.Equal(StuntRacePhase.Opening, race.Phase);
        Assert.Null(a.BestTime);
        Assert.Equal(0, a.MostZones);
        Assert.Equal(0, a.RunsStarted);
        Assert.All(a.Splits, split => Assert.Null(split));
        Assert.False(race.RunStarted(0));
        race.BeginOpening(0f);
        Assert.True(race.RunStarted(0));
    }

    [Fact]
    public void TheLeaderboardLineShowsTheClockThePlaceTheLeadersBestAndTheGap()
    {
        var race = OpenRace(3, out _, out _, out _);
        Assert.Equal("TIME 1:00   1st/3   LEADER --   NO TIME", race.LeaderboardLine(0));
        FlyRun(race, 1, new[] { 1f, 2f, 5f });
        FlyRun(race, 0, new[] { 1f, 2f, 6.3f });
        Advance(race, 10.5f);

        Assert.Equal("TIME 0:50   2nd/3   LEADER P2 0:05.0   +1.3", race.LeaderboardLine(0));
        Assert.Equal("TIME 0:50   1st/3   LEADER P2 0:05.0   -1.3", race.LeaderboardLine(1));
        Assert.Equal("TIME 0:50   3rd/3   LEADER P2 0:05.0   NO TIME", race.LeaderboardLine(2));
    }

    [Fact]
    public void FollowFeedsTheRaceFromARunsOwnClockZonesFinishAndReset()
    {
        var race = new StuntRace(60f, 2);
        var a = race.Add(0, "Bloodhawk");
        race.BeginOpening(0f);
        var run = FakeMission(2);
        race.Follow(0, run);

        run.Tick(Dt);
        Assert.True(a.InRun);
        SetElapsed(run, 3f);
        Complete(run, run.Zones[1]);
        Assert.Equal(1, a.CurrentZones);
        run.Reset();
        Assert.False(a.InRun);

        run.Tick(Dt);
        SetElapsed(run, 2f);
        Complete(run, run.Zones[0]);
        SetElapsed(run, 4f);
        Complete(run, run.Zones[1]);
        Assert.Equal(4f, a.BestTime);
        Assert.Equal(new float?[] { 2f, 4f }, a.Splits);
        Assert.Equal(2, a.RunsStarted);
    }

    [Fact]
    public void APilotWhoLeftKeepsTheirBestAndRanksAsItStoodMarkedOnEveryNameAndCountingNothingMore()
    {
        var race = OpenRace(3, out var a, out var b, out _);
        FlyRun(race, 0, new[] { 1f, 2f, 5f });
        FlyRun(race, 1, new[] { 1f, 2f, 6f });
        PartialRun(race, 0, new[] { (0, 1f) });
        Assert.True(race.MarkLeft(0));
        Assert.False(race.MarkLeft(0));

        // Their best still leads, and their run in progress stopped with them.
        Assert.Equal(new[] { 0, 1, 2 }, race.Standings().Select(r => r.Index));
        Assert.Equal((5f, false, true), (a.BestTime, a.InRun, a.Left));
        Assert.Equal("P1 (left)", StuntRace.NameText(a));
        Assert.Equal("P2", StuntRace.NameText(b));
        Assert.Contains("LEADER P1 (left) 0:05.0", race.LeaderboardLine(1), StringComparison.Ordinal);
        Assert.True(race.Racers[0].Line().Left);

        // Nothing more counts for them, and a later faster run of a rival still outranks them.
        Assert.False(race.RunStarted(0));
        race.ZoneCleared(0, 1, 2f);
        race.RunFinished(0, 3f);
        Assert.Equal((5f, 2, 3, 1), (a.BestTime, a.RunsStarted, a.MostZones, a.RunsFinished));
        FlyRun(race, 1, new[] { 1f, 2f, 4f });
        Assert.Equal(new[] { 1, 0, 2 }, race.Standings().Select(r => r.Index));

        // A new window leaves them out; the rest go again.
        race.Rerun();
        Assert.Equal(new[] { 1, 2 }, race.Racers.Select(r => r.Index));
    }

    [Fact]
    public void APilotLeavingInTheFinalRunEndsItWhenTheirsWasTheLastRunAndAReplicaOnlyMarksThem()
    {
        var race = OpenRace(3, out _, out _, out _);
        PartialRun(race, 2, new[] { (0, 1f) });
        Advance(race, 61f);
        Assert.Equal(StuntRacePhase.FinalRun, race.Phase);
        race.MarkLeft(2);
        Assert.True(race.Ended);

        // ABLE-TO-FAIL CONTROL: a replica keeps waiting for the host's ending.
        var copy = OpenRace(3, out _, out _, out _);
        copy.Replicate();
        copy.TakeLine(2, new RacerLine(true, 1, 0, null, 1, 1f, 1, new float?[3]));
        Advance(copy, 61f);
        Assert.True(copy.MarkLeft(2));
        Assert.Equal(StuntRacePhase.FinalRun, copy.Phase);
        copy.TakeLine(1, new RacerLine(false, 0, 0, null, 0, 0f, 0, new float?[3], Left: true));
        Assert.True(copy.Of(1)!.Left);
    }

    [Fact]
    public void GapsAndClocksFormatInvariantly()
    {
        Assert.Equal("+1.3", StuntRace.FormatGap(1.25f + 0.04f));
        Assert.Equal("-0.5", StuntRace.FormatGap(-0.5f));
        Assert.Equal("+1:02.5", StuntRace.FormatGap(62.5f));
        Assert.Equal("5:00", StuntRace.FormatClock(300f));
        Assert.Equal("0:01", StuntRace.FormatClock(0.2f));
        Assert.Equal("0:00", StuntRace.FormatClock(0f));
    }

    // ---- helpers ----

    private static StuntRace OpenRace(int zones, out Racer a, out Racer b, out Racer c)
    {
        var race = new StuntRace(60f, zones);
        a = race.Add(0, "Bloodhawk");
        b = race.Add(1, "Kestrel");
        c = race.Add(2, "Hoplite");
        race.BeginOpening(0f);
        return race;
    }

    // A whole run, zones cleared in course order at the given run times, the last its finish.
    private static void FlyRun(StuntRace race, int index, float[] at)
    {
        Assert.True(race.RunStarted(index));
        for (int zone = 0; zone < at.Length; zone++)
            race.ZoneCleared(index, zone, at[zone]);
        race.RunFinished(index, at[^1]);
    }

    private static void PartialRun(StuntRace race, int index, (int Zone, float At)[] cleared)
    {
        Assert.True(race.RunStarted(index));
        foreach (var (zone, at) in cleared)
            race.ZoneCleared(index, zone, at);
    }

    private static void Advance(StuntRace race, float seconds)
    {
        int steps = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < steps; i++)
            race.Advance(Dt);
    }

    private static StuntMission FakeMission(int zoneCount)
    {
        var zones = new List<StuntZone>();
        for (int i = 0; i < zoneCount; i++)
            zones.Add(new StuntZone { DzName = $"dz{i}" });
        var ctor = typeof(StuntMission).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(List<StuntZone>) }, null)!;
        return (StuntMission)ctor.Invoke(new object[] { zones });
    }

    private static void SetElapsed(StuntMission mission, float elapsed) =>
        typeof(StuntMission).GetProperty(nameof(StuntMission.Elapsed))!
            .GetSetMethod(nonPublic: true)!.Invoke(mission, new object[] { elapsed });

    private static void Complete(StuntMission mission, StuntZone zone) =>
        typeof(StuntMission).GetMethod("Complete", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(mission, new object[] { zone });
}
