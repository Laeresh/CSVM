using System.Collections.Generic;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The decoded egen timing law (docs/formats/mission-entities.md "The generator cycle"), pure
/// and engine-free: period composition, the hold-not-cancel altitude gate, max_active blocking,
/// the decoded capacity rule where a positive value exists, and the documented capacity-zero
/// stand-in (the capacity puzzle).
/// </summary>
public class GeneratorCycleTests
{
    private const float Dt = 0.1f;
    private const float HighAltitude = 1000f;

    [Fact]
    public void TheGapBetweenWavesIsIndPlusWaveNotWaveAlone()
    {
        // wave_size 1: every spawn completes a wave, so consecutive spawns sit a full
        // ind_period + wave_period apart (20 s here), never wave_period alone.
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 99, waveSize: 1,
            wavePeriod: 10f, indPeriod: 10f, minAltitude: null);
        var times = SpawnTimes(cycle, seconds: 65f);
        Assert.Equal(new[] { 20f, 40f, 60f }, times);
    }

    [Fact]
    public void InsideAWaveTheGapIsIndPeriodAlone()
    {
        // wave_size 3, ind 2, wave 10: a wave's individuals arrive 2 s apart, then the next
        // wave starts a composed 12 s after the wave's last spawn.
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 99, waveSize: 3,
            wavePeriod: 10f, indPeriod: 2f, minAltitude: null);
        var times = SpawnTimes(cycle, seconds: 30f);
        Assert.Equal(new[] { 12f, 14f, 16f, 28f, 30f }, times);
    }

    [Fact]
    public void TheAltitudeGateHoldsAndDoesNotCancel()
    {
        // Below the gate nothing spawns, but the timer keeps running (hold, not cancel): the
        // held spawn fires on the FIRST step at altitude, not a full period later.
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 99, waveSize: 1,
            wavePeriod: 10f, indPeriod: 10f, minAltitude: 150f);
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
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 99, waveSize: 3,
            wavePeriod: 10f, indPeriod: 2f, minAltitude: 150f);
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
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 1, waveSize: 1,
            wavePeriod: 1f, indPeriod: 1f, minAltitude: null);
        Assert.Single(SpawnTimes(cycle, seconds: 30f));
        Assert.Equal(1, cycle.Active);
        cycle.SpawnRemoved();
        // The timer accumulated the whole while (hold, not cancel): the next spawn is immediate.
        Assert.True(cycle.Step(Dt, HighAltitude));
    }

    [Fact]
    public void APositiveCapacityAppliesTheDecodedBudgetRule()
    {
        var cycle = new GeneratorCycle(capacity: 2, maxActive: 99, waveSize: 1,
            wavePeriod: 1f, indPeriod: 1f, minAltitude: null);
        Assert.Equal(2, SpawnTimes(cycle, seconds: 60f).Count);
    }

    [Fact]
    public void CapacityZeroDisablesTheCheckAsTheDocumentedStandIn()
    {
        // The decoded rule with the shipped capacity 0 would block forever on the first tick.
        // The stand-in (capacity <= 0 disables the capacity check, pending the egen.zbd
        // raw-byte read) is what this asserts; it is NOT a decode of "0 means unlimited".
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 99, waveSize: 1,
            wavePeriod: 1f, indPeriod: 1f, minAltitude: null);
        Assert.True(SpawnTimes(cycle, seconds: 30f).Count > 10);
    }

    [Fact]
    public void HostDeathDisablesPermanently()
    {
        var cycle = new GeneratorCycle(capacity: 0, maxActive: 99, waveSize: 1,
            wavePeriod: 1f, indPeriod: 1f, minAltitude: null);
        cycle.HostDied();
        Assert.Empty(SpawnTimes(cycle, seconds: 30f));
        Assert.True(cycle.Disabled);
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
