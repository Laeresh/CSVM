using System.Diagnostics;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The slot row behind <c>--perf</c>'s <c>sim_ms=</c> and <c>proc_sites_ms=</c> splits: that
/// opening a slot closes the one before it, that a slot spanning several callbacks in one window
/// sums them, that the row divides by the caller's frames or ticks, and that a reset drops a
/// half-open slot. On an advanced fake clock, so the arithmetic is asserted by value.
/// ⚠ This is the ONLY class that writes <see cref="SimPhaseCost"/>'s and
/// <see cref="ProcessSiteCost"/>'s ambient statics; a second class would have to join it.
/// </summary>
public class PhaseCostTests
{
    private static readonly string[] Labels = { "A", "B", "C" };

    public PhaseCostTests()
    {
        SimPhaseCost.Reset();
        ProcessSiteCost.Reset();
    }

    [Fact]
    public void AOpeningTheNextSlotClosesThePreviousOne()
    {
        long now = 1;
        var row = new PhaseCost(Labels, () => now);
        row.Open(0);
        now += Ticks(4);
        row.Open(1);
        now += Ticks(6);
        row.CloseOpen();
        Assert.Equal(-1, row.OpenSlot);

        row.TakeRow(1);
        Assert.Equal(4.0, row.LastMs(0), 3);
        Assert.Equal(6.0, row.LastMs(1), 3);
        Assert.Equal(0.0, row.LastMs(2), 3);
    }

    [Fact]
    public void BSeveralSpansOfOneSlotSumAndTheRowDividesByTheCallerCount()
    {
        long now = 1;
        var row = new PhaseCost(Labels, () => now);
        for (int i = 0; i < 3; i++)
        {
            row.Open(2);
            now += Ticks(i == 1 ? 4 : 1);
            row.CloseOpen();
        }

        string printed = row.TakeRow(3);
        Assert.Equal(2.0, row.LastMs(2), 3);
        Assert.Equal(4.0, row.LastMaxMs(2), 3);
        Assert.Equal("A:0.000/0.0,B:0.000/0.0,C:2.000/4.0", printed);
    }

    [Fact]
    public void CAResetDropsAHalfOpenSlot()
    {
        long now = 1;
        var row = new PhaseCost(Labels, () => now);
        row.Open(0);
        now += Ticks(9);
        row.Reset();
        row.CloseOpen();

        row.TakeRow(1);
        Assert.Equal(0.0, row.LastMs(0), 3);
        Assert.Equal(-1, row.OpenSlot);
    }

    [Fact]
    public void DTheFacadesRunTheSameCycleOnTheWallClock()
    {
        SimPhaseCost.Enter(SimPhase.Projectiles);
        Spin(3);
        SimPhaseCost.Enter(SimPhase.HumanAircraft);
        Spin(1);
        SimPhaseCost.Leave();
        SimPhaseCost.TakeRow(1);
        Assert.True(SimPhaseCost.LastMs(SimPhase.Projectiles) >= 3, "the projectile phase banked its spin");
        Assert.True(SimPhaseCost.LastMs(SimPhase.HumanAircraft) >= 1, "the next phase banked its own spin");
        Assert.Equal("Projectiles", SimPhaseCost.Label(SimPhase.Projectiles));

        using (ProcessSiteCost.Enter(ProcessSite.Anim))
        {
            Spin(2);
        }
        ProcessSiteCost.TakeRow(1);
        Assert.True(ProcessSiteCost.LastMs(ProcessSite.Anim) >= 2, "the scope banked on dispose");
    }

    private static long Ticks(double ms) => (long)(ms / 1000.0 * Stopwatch.Frequency);

    private static void Spin(double ms)
    {
        long start = Stopwatch.GetTimestamp();
        while ((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency < ms)
        {
        }
    }
}
