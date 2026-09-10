using System.Diagnostics;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The open / close / drain cycle behind <see cref="ProcessPassCost"/>, the bracket that replaces
/// Godot's <c>TIME_PROCESS</c> as the process pass's cost (docs/verification.md PERF-1). What the
/// drain reports, that the mean and the maximum are different quantities, and the property the
/// <c>--perf</c> window depends on: a pass still open at the drain is carried into the next window
/// rather than dropped, because the reader sits inside the pass it measures.
///
/// <para>⚠ This is the ONLY class that writes <see cref="ProcessPassCost"/>'s ambient statics.
/// xUnit runs test classes in parallel but the tests within one class serially, so that is what
/// keeps these deterministic; a second class bracketing passes would have to join this one.</para>
/// </summary>
public class ProcessPassCostTests
{
    public ProcessPassCostTests() => ProcessPassCost.Reset();

    [Fact]
    public void ADrainReportsOneEntryPerClosedPass()
    {
        for (int i = 0; i < 3; i++)
        {
            ProcessPassCost.Open();
            ProcessPassCost.Close();
        }
        Assert.Equal(3, ProcessPassCost.Passes);

        var (ms, maxMs, passes) = ProcessPassCost.Take();
        Assert.Equal(3, passes);
        Assert.True(ms >= 0);
        Assert.True(maxMs >= 0);
        Assert.True(ms >= maxMs, $"three passes must total at least the worst of them: {ms} vs {maxMs}");
    }

    [Fact]
    public void BTheMaximumIsTheWorstPassAndTheTotalIsEveryPass()
    {
        ProcessPassCost.Open();
        Spin(20);
        ProcessPassCost.Close();
        for (int i = 0; i < 5; i++)
        {
            ProcessPassCost.Open();
            ProcessPassCost.Close();
        }

        var (ms, maxMs, passes) = ProcessPassCost.Take();
        Assert.Equal(6, passes);
        // The point PERF-1 turns on: one slow pass among five fast ones sets the maximum, while the
        // mean stays near the fast ones. Godot's monitor reports the first; the pass cost is the second.
        double mean = ms / passes;
        Assert.True(maxMs >= 15, $"the deliberately slow pass should dominate the maximum: {maxMs} ms");
        Assert.True(mean < maxMs, $"the mean must sit below the worst pass: {mean} vs {maxMs}");
    }

    [Fact]
    public void CADrainResetsBothTheTotalAndTheMaximum()
    {
        ProcessPassCost.Open();
        Spin(10);
        ProcessPassCost.Close();
        ProcessPassCost.Take();

        var (ms, maxMs, passes) = ProcessPassCost.Take();
        Assert.Equal(0, passes);
        Assert.Equal(0, ms);
        Assert.Equal(0, maxMs);
    }

    [Fact]
    public void DACloseWithNoOpenStandingBanksNothing()
    {
        ProcessPassCost.Close();
        ProcessPassCost.Close();

        var (ms, _, passes) = ProcessPassCost.Take();
        Assert.Equal(0, passes);
        Assert.Equal(0, ms);
    }

    [Fact]
    public void EAResetDropsAHalfOpenPassRatherThanChargingItToTheNextClose()
    {
        ProcessPassCost.Open();
        Spin(15);
        ProcessPassCost.Reset();
        ProcessPassCost.Close();

        var (ms, _, passes) = ProcessPassCost.Take();
        Assert.Equal(0, passes);
        Assert.Equal(0, ms);
    }

    [Fact]
    public void FADrainInsideAPassCarriesThatPassIntoTheNextWindow()
    {
        // The --perf reader's own shape: two passes close, a third opens, and the drain happens
        // before its tail runs. The open pass belongs to the next window, not to this one.
        for (int i = 0; i < 2; i++)
        {
            ProcessPassCost.Open();
            ProcessPassCost.Close();
        }
        ProcessPassCost.Open();
        Spin(15);

        var (firstMs, _, firstPasses) = ProcessPassCost.Take();
        Assert.Equal(2, firstPasses);
        Assert.True(firstMs < 15, $"the open pass must not be banked by the drain: {firstMs} ms");

        ProcessPassCost.Close();
        var (secondMs, secondMaxMs, secondPasses) = ProcessPassCost.Take();
        Assert.Equal(1, secondPasses);
        Assert.True(secondMs >= 15, $"the carried pass lands whole in the next window: {secondMs} ms");
        Assert.Equal(secondMs, secondMaxMs);
    }

    // Burns wall time without sleeping, so the slow pass is slow for the same reason a real one is.
    private static void Spin(double ms)
    {
        long start = Stopwatch.GetTimestamp();
        while ((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency < ms)
        {
        }
    }
}
