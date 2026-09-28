using CSVM.Net;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The start barrier's decisions off-engine: which words open it, on which end, and when a wait
/// gives up. The session only sends the words and holds its clock on the answer.
/// </summary>
[Trait("Tier", "Quick")]
public class NetStartGateTests
{
    [Fact]
    public void AHostOpensOnlyOnceEveryLoadingMachineHasReported()
    {
        var gate = NetStartGate.Host(new[] { 1, 2, 2 });
        Assert.False(gate.Open);
        Assert.Equal(2, gate.Waiting.Count);

        Assert.False(gate.TakeLoaded(1));
        Assert.False(gate.Open);
        Assert.False(gate.TakeLoaded(1));
        Assert.True(gate.TakeLoaded(2));
        Assert.Equal(NetStartRelease.Everyone, gate.Release);

        // A word after the gate opened changes nothing.
        Assert.False(gate.TakeLoaded(2));
        Assert.Equal(NetStartRelease.Everyone, gate.Release);
    }

    [Fact]
    public void AMachineThatDropsWhileLoadingReleasesTheHost()
    {
        var gate = NetStartGate.Host(new[] { 1, 2 });
        gate.TakeLoaded(1);
        Assert.True(gate.TakeLeft(2));
        Assert.Equal(NetStartRelease.Left, gate.Release);

        // ABLE-TO-FAIL CONTROL: a peer nobody waited on releases nothing.
        var other = NetStartGate.Host(new[] { 1 });
        Assert.False(other.TakeLeft(5));
        Assert.False(other.Open);
    }

    [Fact]
    public void AHostWithNobodyToWaitForIsOpenFromTheStart()
    {
        var gate = NetStartGate.Host(System.Array.Empty<int>());
        Assert.True(gate.Open);
        Assert.Equal(NetStartRelease.Alone, gate.Release);
    }

    [Fact]
    public void AWaitGivesUpAtTheTimeoutAndNotBefore()
    {
        var gate = NetStartGate.Host(new[] { 1 });
        Assert.False(gate.Step(NetStartGate.TimeoutSeconds - 1.0));
        Assert.False(gate.Open);
        Assert.True(gate.Step(1.0));
        Assert.Equal(NetStartRelease.TimedOut, gate.Release);
        Assert.Equal(NetStartGate.TimeoutSeconds, gate.WaitedSeconds, 6);

        // Once open, the wait stops counting.
        Assert.False(gate.Step(5.0));
        Assert.Equal(NetStartGate.TimeoutSeconds, gate.WaitedSeconds, 6);
    }

    [Fact]
    public void AGuestOpensOnTheHostsWordOrItsLinkDropping()
    {
        var started = NetStartGate.Guest(0);
        Assert.False(started.Open);
        // A loaded word is the host's to take; on a guest it opens nothing.
        Assert.False(started.TakeLoaded(0));
        Assert.True(started.TakeStart());
        Assert.Equal(NetStartRelease.Started, started.Release);
        Assert.False(started.TakeStart());

        var dropped = NetStartGate.Guest(0);
        Assert.True(dropped.TakeLeft(0));
        Assert.Equal(NetStartRelease.Left, dropped.Release);
    }

    [Fact]
    public void AStartHeldClockRunsNoStepsInAnyModeAndStaysOffTheParentPath()
    {
        foreach (var mode in new[] { GameClock.RunMode.Realtime, GameClock.RunMode.FixedStep, GameClock.RunMode.FixedAccum })
        {
            var clock = new GameClock { Mode = mode, StartHeld = true };
            clock.BeginFrame(0.5);
            Assert.Equal(0, clock.Steps);
            Assert.Equal(0L, clock.Frame);
            Assert.Equal(0.0, clock.Time);
            Assert.Equal(0f, clock.PhysicsDt(GameClock.FixedDt));
            Assert.Equal(mode != GameClock.RunMode.Realtime, clock.ParentDriven);

            // ABLE-TO-FAIL CONTROL: released, the same clock steps.
            clock.StartHeld = false;
            clock.BeginFrame(0.1);
            Assert.True(clock.Steps > 0);
        }
    }

    [Fact]
    public void AStartWordOnTheHostOpensNothing()
    {
        var gate = NetStartGate.Host(new[] { 1 });
        Assert.False(gate.TakeStart());
        Assert.False(gate.Open);
    }
}
