using System;
using System.Collections.Generic;
using System.Reflection;
using CSVM.Flight;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Splitscreen stunt-race bookkeeping (<see cref="StuntRace"/>), off-engine: who placed in what
/// order, a rematch's reset, and the standings sort.
/// <see cref="StuntRace"/> is a plain sealed class with no
/// engine dependency — its own <c>GD.Print</c> calls route through <see cref="Log"/> —
/// but its collaborator <see cref="StuntMission"/> calls
/// <c>GD.Print</c>/<c>GD.PushWarning</c> directly from <c>Load</c> and <c>Complete</c> — either one
/// crashes the whole test host outside the engine (an unmanaged <c>AccessViolationException</c>,
/// verified empirically), not just fails the one test. So these tests never
/// call either: a fake mission comes from <see cref="StuntMission"/>'s private constructor
/// (reflection — it has no public one, and adding one only for tests would widen its
/// surface), and a finish is simulated by setting <c>Elapsed</c> and raising the private
/// <c>RunCompleted</c> backing delegate directly — exactly what <c>Complete()</c> itself does once
/// every zone is in, minus the call that would crash.
/// </summary>
public class StuntRaceTests
{
    [Fact]
    public void FinishOrderAssignsPlacingsInFinishOrderNotEntryOrder()
    {
        var lines = new List<string>();
        var was = Log.ConsoleSink;
        Log.ConsoleSink = lines.Add;
        try
        {
            var race = new StuntRace();
            var a = race.Add(0, FakeMission(1), "Bloodhawk");
            var b = race.Add(1, FakeMission(1), "Kestrel");
            var c = race.Add(2, FakeMission(1), "Hoplite");

            int raceCompletedCount = 0;
            race.RaceCompleted += () => raceCompletedCount++;

            // Finished out of entry order: C first, then A, then B.
            FinishAt(c.Mission, 12.3f);
            FinishAt(a.Mission, 34.5f);
            FinishAt(b.Mission, 56.7f);

            Assert.Equal(1, c.Rank);
            Assert.Equal(2, a.Rank);
            Assert.Equal(3, b.Rank);
            Assert.Equal(12.3f, c.FinishTime);
            Assert.Equal(34.5f, a.FinishTime);
            Assert.Equal(56.7f, b.FinishTime);
            Assert.Equal(3, race.FinishedCount);
            Assert.True(race.AllFinished);
            Assert.Equal(1, raceCompletedCount); // fired exactly once, on the last finisher

            // The seam this item exists to prove: the finish line reached the installed sink,
            // once per racer, plus the one race-complete line — not the real GD.Print.
            Assert.Equal(4, lines.Count);
            Assert.Contains(lines, l => l.Contains(a.Tag) && l.Contains("finished"));
            Assert.Contains(lines, l => l.Contains(b.Tag) && l.Contains("finished"));
            Assert.Contains(lines, l => l.Contains(c.Tag) && l.Contains("finished"));
            Assert.Contains(lines, l => l.Contains("RACE COMPLETE"));
        }
        finally
        {
            Log.ConsoleSink = was;
        }
    }

    [Fact]
    public void RestartClearsPlacingsClocksAndMissionProgressForARematch()
    {
        WithConsoleSink(() =>
        {
            var race = new StuntRace();
            var a = race.Add(0, FakeMission(2), "Bloodhawk");
            var b = race.Add(1, FakeMission(2), "Kestrel");

            FinishAt(a.Mission, 10f);
            FinishAt(b.Mission, 20f);
            Assert.True(race.AllFinished);

            race.Restart();

            Assert.Equal(0, a.Rank);
            Assert.Equal(0, b.Rank);
            Assert.Equal(0f, a.FinishTime);
            Assert.Equal(0f, b.FinishTime);
            Assert.Equal(0, race.FinishedCount);
            Assert.False(race.AllFinished);
            // Restart() runs the real, public StuntMission.Reset() — asserting through it is the
            // point: a rematch must not leave the previous run's clock or completions behind.
            Assert.False(a.Mission.AllComplete);
            Assert.Equal(0f, a.Mission.Elapsed);
            Assert.Equal(0, a.Mission.CompletedCount);
        });
    }

    [Fact]
    public void StandingsRanksAFinisherFirstThenStillFlyingByZonesThenByTheFasterClockOnATie()
    {
        WithConsoleSink(() =>
        {
            var race = new StuntRace();
            var winner = race.Add(0, FakeMission(3), "Bloodhawk");
            var ahead = race.Add(1, FakeMission(3), "Kestrel");      // 3 zones cleared, still flying
            var tiedSlower = race.Add(2, FakeMission(3), "Hoplite"); // 2 zones, slower clock
            var tiedFaster = race.Add(3, FakeMission(3), "Autogyro"); // 2 zones, faster clock — the tie

            FinishAt(winner.Mission, 99f);
            SetProgress(ahead.Mission, completedCount: 3, elapsed: 40f);
            SetProgress(tiedSlower.Mission, completedCount: 2, elapsed: 30f);
            SetProgress(tiedFaster.Mission, completedCount: 2, elapsed: 20f);

            var order = new List<Racer>(race.Standings());

            Assert.Equal(new[] { winner, ahead, tiedFaster, tiedSlower }, order);
        });
    }

    // ---- StuntMission off-engine test doubles (see the class doc-comment for why) ----

    /// <summary>Every <see cref="FinishAt"/> call reaches <see cref="StuntRace.OnFinished"/>,
    /// which now logs through <see cref="Log"/> — with no sink installed that falls through to
    /// the real <c>GD.Print</c>, which crashes the whole test host outside the engine. Any test
    /// that calls <see cref="FinishAt"/> must run inside this.</summary>
    private static void WithConsoleSink(Action body)
    {
        var was = Log.ConsoleSink;
        Log.ConsoleSink = _ => { };
        try
        {
            body();
        }
        finally
        {
            Log.ConsoleSink = was;
        }
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

    private static void SetProgress(StuntMission mission, int completedCount, float elapsed)
    {
        typeof(StuntMission).GetProperty(nameof(StuntMission.CompletedCount))!
            .GetSetMethod(nonPublic: true)!.Invoke(mission, new object[] { completedCount });
        typeof(StuntMission).GetProperty(nameof(StuntMission.Elapsed))!
            .GetSetMethod(nonPublic: true)!.Invoke(mission, new object[] { elapsed });
    }

    private static void FinishAt(StuntMission mission, float elapsed)
    {
        SetProgress(mission, mission.TotalCount, elapsed);
        var field = typeof(StuntMission).GetField(nameof(StuntMission.RunCompleted),
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((Action?)field.GetValue(mission))?.Invoke();
    }
}
