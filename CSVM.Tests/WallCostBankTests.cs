using System.Diagnostics;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="WallCostBank"/>'s banked arithmetic, driven by a clock this class advances rather
/// than by the wall clock. The static facades over the bank (physics tick, process pass, AI walk)
/// can only claim one-sided floors, because any span they measure grows when the machine is
/// oversubscribed; here every span is exactly as long as the test says, so the sum, the worst term
/// and the cost carried across a drain are asserted by value and no load can move them.
/// </summary>
public class WallCostBankTests
{
    // Stopwatch ticks, the unit the bank converts from. A zero stamp reads as no span open, so the
    // clock starts a whole second past it.
    private long _ticks = Stopwatch.Frequency;

    [Fact]
    public void TheTotalIsEverySpanSummedAndNotTheWorstOfThem()
    {
        var bank = new WallCostBank("Probe", () => _ticks);
        long expected = 0;
        foreach (double spanMs in new[] { 20.0, 1.0, 1.0, 1.0, 1.0, 1.0 })
        {
            bank.Open();
            expected += Advance(spanMs);
            bank.Close(8);
        }

        var (ms, maxMs, spans, tally) = bank.Take();
        Assert.Equal(6, spans);
        Assert.Equal(48, tally);
        Assert.Equal(Ms(expected), ms, 9);
        Assert.Equal(Ms(TicksFor(20.0)), maxMs, 9);
    }

    [Fact]
    public void ASpanOpenAtTheDrainIsAbsentFromThatWindowAndWholeInTheNext()
    {
        // The --perf reader's own shape: two spans close, a third opens, and the drain happens
        // before its close runs. The open span belongs to the next window, by value in both.
        var bank = new WallCostBank("Probe", () => _ticks);
        long closed = 0;
        for (int i = 0; i < 2; i++)
        {
            bank.Open();
            closed += Advance(1.0);
            bank.Close();
        }
        bank.Open();
        long carried = Advance(15.0);

        var (firstMs, _, firstSpans, _) = bank.Take();
        Assert.Equal(2, firstSpans);
        Assert.Equal(Ms(closed), firstMs, 9);

        bank.Close();
        var (secondMs, secondMaxMs, secondSpans, _) = bank.Take();
        Assert.Equal(1, secondSpans);
        Assert.Equal(Ms(carried), secondMs, 9);
        Assert.Equal(secondMs, secondMaxMs);
    }

    [Fact]
    public void AResetDropsAHalfOpenSpanRatherThanChargingItToTheNextClose()
    {
        var bank = new WallCostBank("Probe", () => _ticks);
        bank.Open();
        Advance(15.0);
        bank.Reset();
        Advance(15.0);
        bank.Close(5);

        var (ms, maxMs, spans, tally) = bank.Take();
        Assert.Equal(0, spans);
        Assert.Equal(0, tally);
        Assert.Equal(0, ms);
        Assert.Equal(0, maxMs);
    }

    private static long TicksFor(double ms) => (long)(ms * Stopwatch.Frequency / 1000.0);

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // Moves the injected clock on and reports the ticks it moved, so a caller sums the same
    // integers the bank converted rather than re-deriving them from the milliseconds.
    private long Advance(double ms)
    {
        long ticks = TicksFor(ms);
        _ticks += ticks;
        return ticks;
    }
}
