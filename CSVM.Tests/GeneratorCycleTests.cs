using System.Collections.Generic;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The decoded egen timing law (docs/formats/mission-entities/enemy-generators.md "The generator
/// cycle"), pure and engine-free: period composition, the hold-not-cancel altitude gate,
/// max_active blocking, and the credit rule under which an uncredited cycle never launches.
/// </summary>
public class GeneratorCycleTests
{
    private const float Dt = 0.1f;
    private const float HighAltitude = 1000f;

    // Enough credit that the budget never enters a timing assertion.
    private const int Plenty = 99;

    [Fact]
    public void TheGapBetweenWavesIsIndPlusWaveNotWaveAlone()
    {
        // wave_size 1: every spawn completes a wave, so consecutive spawns sit a full
        // ind_period + wave_period apart (20 s here), never wave_period alone.
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 10f, indPeriod: 10f);
        var times = SpawnTimes(cycle, seconds: 65f);
        Assert.Equal(new[] { 20f, 40f, 60f }, times);
    }

    [Fact]
    public void InsideAWaveTheGapIsIndPeriodAlone()
    {
        // wave_size 3, ind 2, wave 10: a wave's individuals arrive 2 s apart, then the next
        // wave starts a composed 12 s after the wave's last spawn.
        var cycle = Credited(maxActive: 99, waveSize: 3, wavePeriod: 10f, indPeriod: 2f);
        var times = SpawnTimes(cycle, seconds: 30f);
        Assert.Equal(new[] { 12f, 14f, 16f, 28f, 30f }, times);
    }

    [Fact]
    public void TheAltitudeGateHoldsAndDoesNotCancel()
    {
        // Below the gate nothing spawns, but the timer keeps running (hold, not cancel): the
        // held spawn fires on the FIRST step at altitude, not a full period later.
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 10f, indPeriod: 10f,
            minAltitude: 150f);
        for (float t = 0f; t < 60f; t += Dt)
        {
            Assert.False(cycle.Step(Dt, hostAltitude: 100f));
        }
        Assert.True(cycle.Step(Dt, hostAltitude: 200f));
    }

    [Fact]
    public void AHeldWaveResumesMidWaveNotFromTheTop()
    {
        // Block after the first individual of a 3-wave; on unblock the wave CONTINUES (the wave
        // counter was untouched), so the remaining gaps are ind_period, not the inter-wave gap.
        var cycle = Credited(maxActive: 99, waveSize: 3, wavePeriod: 10f, indPeriod: 2f,
            minAltitude: 150f);
        float t = 0f;
        var spawns = new List<float>();
        for (; t < 12.05f; t += Dt)
        {
            if (cycle.Step(Dt, HighAltitude))
            {
                spawns.Add(t + Dt);
            }
        }
        Assert.Single(spawns);   // wave started: one individual out at 12 s
        for (float held = 0f; held < 5f; held += Dt)
        {
            Assert.False(cycle.Step(Dt, hostAltitude: 100f));   // held below the gate
            t += Dt;
        }
        Assert.True(cycle.Step(Dt, HighAltitude));   // the held individual fires immediately
        // The wave's third individual follows one ind_period later, not one inter-wave gap.
        int steps = 0;
        while (!cycle.Step(Dt, HighAltitude))
        {
            steps++;
        }
        Assert.InRange(steps * Dt, 1.8f, 2.05f);
    }

    [Fact]
    public void MaxActiveBlocksAndADeathFrees()
    {
        var cycle = Credited(maxActive: 1, waveSize: 1, wavePeriod: 1f, indPeriod: 1f);
        Assert.Single(SpawnTimes(cycle, seconds: 30f));
        Assert.Equal(1, cycle.Active);
        cycle.SpawnRemoved();
        // The timer accumulated the whole while (hold, not cancel): the next spawn is immediate.
        Assert.True(cycle.Step(Dt, HighAltitude));
    }

    [Fact]
    public void AnUncreditedCycleNeverLaunches()
    {
        // The decoded rule: the authored capacity is never read, every cycle starts at zero
        // remaining, and the block holds (timer running, door shut) until something credits it.
        var cycle = new GeneratorCycle(maxActive: 99, waveSize: 1, wavePeriod: 1f, indPeriod: 1f,
            minAltitude: null);
        Assert.Empty(SpawnTimes(cycle, seconds: 60f));
        Assert.False(cycle.DoorOpen);
        Assert.Equal(0, cycle.CapacityRemaining);
    }

    [Fact]
    public void ACreditLaunchesExactlyThatManyThenHolds()
    {
        var cycle = new GeneratorCycle(maxActive: 99, waveSize: 1, wavePeriod: 1f, indPeriod: 1f,
            minAltitude: null);
        cycle.GrantCapacity(2);
        Assert.Equal(2, SpawnTimes(cycle, seconds: 60f).Count);
        Assert.Equal(0, cycle.CapacityRemaining);
        // A later credit releases the held spawn on its first step: the timer ran on.
        cycle.GrantCapacity(1);
        Assert.True(cycle.Step(Dt, HighAltitude));
    }

    [Fact]
    public void AHeldCreditLaunchesImmediatelyAndThenOnTheComposedPeriod()
    {
        // C4/M03's cargozep1: wave 1, ind 3 + wave 1. Credited 5 long after load, the first
        // launch fires on the crediting step (the timer is far past its threshold) and the
        // other four follow one every 4 s.
        var cycle = new GeneratorCycle(maxActive: 10, waveSize: 1, wavePeriod: 1f, indPeriod: 3f,
            minAltitude: null);
        Assert.Empty(SpawnTimes(cycle, seconds: 30f));
        cycle.GrantCapacity(5);
        var times = SpawnTimes(cycle, seconds: 30f);
        Assert.Equal(5, times.Count);
        Assert.Equal(0.1f, times[0]);
        for (int i = 1; i < times.Count; i++)
        {
            // One step of float accumulation slack on each 4 s gap.
            Assert.InRange(times[i] - times[i - 1], 3.95f, 4.25f);
        }
    }

    [Fact]
    public void AWaveNeedsCreditForItsWholeRemainder()
    {
        // wave_size 3 with credit 2: the block compares the wave's remainder against the
        // credit, so nothing launches until the third credit arrives.
        var cycle = new GeneratorCycle(maxActive: 99, waveSize: 3, wavePeriod: 1f, indPeriod: 1f,
            minAltitude: null);
        cycle.GrantCapacity(2);
        Assert.Empty(SpawnTimes(cycle, seconds: 30f));
        cycle.GrantCapacity(1);
        Assert.Equal(3, SpawnTimes(cycle, seconds: 30f).Count);
    }

    [Fact]
    public void TheDoorOpensFourSecondsBeforeADueSpawnAndSpawnsThroughIt()
    {
        // First spawn is due at ind + wave = 20 s, so the door opens at 16 s: closed through
        // 15.9 s, open at 16.0, spawn at 20.0 with the door still open.
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 10f, indPeriod: 10f);
        float openedAt = -1f, spawnedAt = -1f;
        for (float t = Dt; t <= 21f; t += Dt)
        {
            bool spawned = cycle.Step(Dt, HighAltitude);
            if (openedAt < 0f && cycle.DoorOpen)
            {
                openedAt = t;
            }
            if (spawned && spawnedAt < 0f)
            {
                spawnedAt = t;
                Assert.True(cycle.DoorOpen);   // the spawn drops through an open door
            }
        }
        Assert.InRange(openedAt, 15.95f, 16.1f);
        Assert.InRange(spawnedAt, 19.95f, 20.1f);
    }

    [Fact]
    public void TheDoorClosesEarlyOnlyWhenTheNextSpawnIsMoreThanEightSecondsAway()
    {
        // Gap 20 s: after a spawn the door holds its 4 s minimum, then closes early
        // (16 s still to go > 8) and reopens 4 s before the next spawn.
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 10f, indPeriod: 10f);
        RunUntilSpawn(cycle);
        float closedAt = -1f, reopenedAt = -1f;
        for (float t = Dt; t <= 20f; t += Dt)
        {
            bool spawned = cycle.Step(Dt, HighAltitude);
            if (closedAt < 0f && !cycle.DoorOpen)
            {
                closedAt = t;
            }
            if (closedAt > 0f && reopenedAt < 0f && cycle.DoorOpen)
            {
                reopenedAt = t;
            }
            if (spawned)
            {
                break;
            }
        }
        Assert.InRange(closedAt, 3.95f, 4.1f);      // the 4 s minimum open
        Assert.InRange(reopenedAt, 15.95f, 16.1f);  // 4 s before the 20 s spawn
    }

    [Fact]
    public void AFastCyclingGeneratorLeavesItsDoorOpen()
    {
        // Gap 7 s (C1/IA1's authored ind 5 + wave 2): at the 4 s minimum only 3 s remain to
        // the next spawn — not more than 8 — so the door never closes between spawns.
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 2f, indPeriod: 5f);
        RunUntilSpawn(cycle);
        for (float t = Dt; t <= 15f; t += Dt)
        {
            cycle.Step(Dt, HighAltitude);
            Assert.True(cycle.DoorOpen);
        }
    }

    [Fact]
    public void ABlockedGeneratorClosesOnlyTheDoorAndReopensToSpawnOnRelease()
    {
        // Block right after a spawn, with the door open and the since-spawn timer under the
        // 4 s minimum (the decoded rule measures the door's minimum on that same timer).
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 10f, indPeriod: 10f,
            minAltitude: 150f);
        RunUntilSpawn(cycle);
        Assert.True(cycle.DoorOpen);        // spawns drop through an open door
        for (float t = Dt; t <= 2f; t += Dt)
        {
            Assert.False(cycle.Step(Dt, hostAltitude: 100f));
        }
        Assert.True(cycle.DoorOpen);        // blocked 2 s in: still inside the 4 s minimum
        for (float t = Dt; t <= 3f; t += Dt)
        {
            Assert.False(cycle.Step(Dt, hostAltitude: 100f));
        }
        Assert.False(cycle.DoorOpen);       // past the minimum: ONLY the door closed
        // Stay blocked well past the 20 s due point: the timer ran on untouched (hold, not
        // cancel) but nothing opens or spawns while blocked.
        for (float t = Dt; t <= 16f; t += Dt)
        {
            Assert.False(cycle.Step(Dt, hostAltitude: 100f));
        }
        Assert.False(cycle.DoorOpen);
        // Release: the door reopens and the held spawn fires in the same step.
        Assert.True(cycle.Step(Dt, HighAltitude));
        Assert.True(cycle.DoorOpen);
    }

    [Fact]
    public void HostDeathDisablesPermanently()
    {
        var cycle = Credited(maxActive: 99, waveSize: 1, wavePeriod: 1f, indPeriod: 1f);
        cycle.HostDied();
        Assert.Empty(SpawnTimes(cycle, seconds: 30f));
        Assert.True(cycle.Disabled);
    }

    private static GeneratorCycle Credited(int maxActive, int waveSize, float wavePeriod,
        float indPeriod, float? minAltitude = null)
    {
        var cycle = new GeneratorCycle(maxActive, waveSize, wavePeriod, indPeriod, minAltitude);
        cycle.GrantCapacity(Plenty);
        return cycle;
    }

    private static void RunUntilSpawn(GeneratorCycle cycle)
    {
        for (int i = 0; i < 10_000; i++)
        {
            if (cycle.Step(Dt, HighAltitude))
            {
                return;
            }
        }
        Assert.Fail("no spawn within the budget");
    }

    private static List<float> SpawnTimes(GeneratorCycle cycle, float seconds)
    {
        var spawns = new List<float>();
        int steps = (int)(seconds / Dt);
        for (int i = 1; i <= steps; i++)
        {
            if (cycle.Step(Dt, HighAltitude))
            {
                // Report in whole tenths so float accumulation noise does not move an assert.
                spawns.Add((float)System.Math.Round(i * Dt, 1));
            }
        }
        return spawns;
    }
}
