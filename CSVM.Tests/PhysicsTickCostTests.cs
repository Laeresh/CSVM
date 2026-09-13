using System.Diagnostics;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The open / close / drain cycle behind <see cref="PhysicsTickCost"/>, the bracket that replaces
/// Godot's <c>TIME_PHYSICS_PROCESS</c> as the physics step's cost (docs/verification.md PERF-1).
/// What the drain reports, that the mean and the maximum are different quantities, and the two
/// properties an unbalanced bracket must have: a close with no open standing banks nothing, and a
/// reset drops a half-open tick rather than letting the next close charge it.
///
/// <para>⚠ This is the ONLY class that writes <see cref="PhysicsTickCost"/>'s ambient statics.
/// xUnit runs test classes in parallel but the tests within one class serially, so that is what
/// keeps these deterministic; a second class bracketing ticks would have to join this one.</para>
/// </summary>
public class PhysicsTickCostTests
{
    public PhysicsTickCostTests() => PhysicsTickCost.Reset();

    [Fact]
    public void ADrainReportsOneEntryPerClosedTick()
    {
        for (int i = 0; i < 3; i++)
        {
            PhysicsTickCost.Open();
            PhysicsTickCost.Close();
        }
        Assert.Equal(3, PhysicsTickCost.Ticks);

        var (ms, maxMs, ticks) = PhysicsTickCost.Take();
        Assert.Equal(3, ticks);
        Assert.True(ms >= 0);
        Assert.True(maxMs >= 0);
        Assert.True(ms >= maxMs, $"three ticks must total at least the worst of them: {ms} vs {maxMs}");
    }

    [Fact]
    public void BTheMaximumIsTheWorstTickAndTheTotalIsEveryTick()
    {
        PhysicsTickCost.Open();
        Spin(20);
        PhysicsTickCost.Close();
        for (int i = 0; i < 5; i++)
        {
            PhysicsTickCost.Open();
            PhysicsTickCost.Close();
        }

        var (ms, maxMs, ticks) = PhysicsTickCost.Take();
        Assert.Equal(6, ticks);
        // The point PERF-1 turns on: one slow tick among five fast ones sets the maximum, while the
        // mean stays near the fast ones. Godot's monitor reports the first; the step cost is the second.
        double mean = ms / ticks;
        Assert.True(maxMs >= 15, $"the deliberately slow tick should dominate the maximum: {maxMs} ms");
        Assert.True(mean < maxMs, $"the mean must sit below the worst tick: {mean} vs {maxMs}");
    }

    [Fact]
    public void CADrainResetsBothTheTotalAndTheMaximum()
    {
        PhysicsTickCost.Open();
        Spin(10);
        PhysicsTickCost.Close();
        PhysicsTickCost.Take();

        var (ms, maxMs, ticks) = PhysicsTickCost.Take();
        Assert.Equal(0, ticks);
        Assert.Equal(0, ms);
        Assert.Equal(0, maxMs);
    }

    [Fact]
    public void DACloseWithNoOpenStandingBanksNothing()
    {
        PhysicsTickCost.Close();
        PhysicsTickCost.Close();

        var (ms, _, ticks) = PhysicsTickCost.Take();
        Assert.Equal(0, ticks);
        Assert.Equal(0, ms);
    }

    [Fact]
    public void EAResetDropsAHalfOpenTickRatherThanChargingItToTheNextClose()
    {
        PhysicsTickCost.Open();
        Spin(15);
        PhysicsTickCost.Reset();
        PhysicsTickCost.Close();

        var (ms, _, ticks) = PhysicsTickCost.Take();
        Assert.Equal(0, ticks);
        Assert.Equal(0, ms);
    }

    // Burns wall time without sleeping, so the slow tick is slow for the same reason a real one is.
    private static void Spin(double ms)
    {
        long start = Stopwatch.GetTimestamp();
        while ((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency < ms)
        {
        }
    }
}
