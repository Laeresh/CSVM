using System.Collections.Generic;
using CSVM;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="Pads.AssignPads(int, IReadOnlyList{int})"/> — the pure half of the fix, engine-
/// free so a phantom-device scenario can be asserted without real hardware. The IDs below are
/// arbitrary; what matters is which slot in the roster they occupy.
/// </summary>
public class PadsTests
{
    /// <summary>⚠ Null is the ANSWER for one player, not a missing one: <see cref="Pads.For"/>
    /// reads every connected pad for a null binding and none at all for an empty one, so a
    /// consumer that coalesces this to <c>Array.Empty&lt;int&gt;()</c> on the way to a flight rig
    /// flies that player pad-dead while their menus still work. That shipped once, in the
    /// FlightRoster unification, and this names the distinction so it cannot return quietly.</summary>
    [Fact]
    public void SinglePlayerReturnsNullWhichIsNotTheSameAsAnEmptyBinding()
        => Assert.Null(Pads.AssignPads(1, new List<int> { 5, 6, 7 }));

    [Fact]
    public void P2ToP4TakeTheNextRosterSlotEach()
    {
        var a = Pads.AssignPads(3, new List<int> { 10, 20, 30, 40 });
        Assert.Equal(new[] { 20 }, a![1]);
        Assert.Equal(new[] { 30 }, a[2]);
    }

    [Fact]
    public void P1GetsEveryPadNobodyElseClaimed()
    {
        // 4 connected devices, 3 players: P2/P3 claim slots 1/2, P1 gets slot 0 AND the
        // leftover slot 3 — not just the first slot, so P1 flies as long as ANY unclaimed
        // pad is real, exactly the union LaunchMenu already hands unclaimed player 1.
        var a = Pads.AssignPads(3, new List<int> { 10, 20, 30, 40 });
        Assert.Equal(new[] { 10, 40 }, a![0]);
    }

    [Fact]
    public void APhantomAtSlotZeroNoLongerStrandsP1()
    {
        // Slot 0 is a phantom device that never produces input; a pads[0]-only rule would
        // strand P1 on it, so P1 gets the whole leftover set instead.
        var a = Pads.AssignPads(2, new List<int> { 10, 20, 30 });
        Assert.Equal(new[] { 20 }, a![1]);
        Assert.Equal(new[] { 10, 30 }, a[0]);
        Assert.Contains(30, a[0]); // the real device is in P1's read set even though it's not pads[0]
    }

    [Fact]
    public void APlayerWithNoPadLeftGetsAnEmptyList()
    {
        var a = Pads.AssignPads(3, new List<int> { 10 });
        Assert.Equal(new[] { 10 }, a![0]); // unclaimed, so it still roams to P1
        Assert.Empty(a[1]);
        Assert.Empty(a[2]);
    }

    [Fact]
    public void NoPadsConnectedLeavesEveryoneEmpty()
    {
        var a = Pads.AssignPads(2, new List<int>());
        Assert.Empty(a![0]);
        Assert.Empty(a[1]);
    }
}
