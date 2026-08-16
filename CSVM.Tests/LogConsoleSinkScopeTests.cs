using System;
using System.Collections.Generic;
using System.Threading;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="Log.PushConsoleSink"/> is per-flow, not process-global. Keeps dead the bug where a
/// single mutable static sink let a concurrent xunit test class swap it between another class's
/// install and its assertions.
/// The interleaving is forced with a barrier rather than hunted for by hammering, since the
/// failure is rare enough that a green run proves nothing. Both replacement and contamination
/// are asserted; against a global static this fails every run, not intermittently.
/// </summary>
public class LogConsoleSinkScopeTests
{
    // A wedged barrier would hang RunTests.ps1 with no output at all, which is a worse failure
    // than the one being guarded against. Generous enough that a loaded CI machine cannot trip it.
    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void AConcurrentFlowsSinkCannotStealOrPolluteThisFlowsCapture()
    {
        var linesA = new List<string>();
        var linesB = new List<string>();
        using var barrier = new Barrier(2);
        Exception? failureA = null;
        Exception? failureB = null;

        // A opens its capture FIRST and logs LAST — so a sink stored process-wide is B's by the
        // time A writes, and A's line lands in B's list instead of its own.
        var threadA = new Thread(() =>
        {
            try
            {
                using (Log.PushConsoleSink(linesA.Add))
                {
                    Wait(barrier);   // A's scope is open
                    Wait(barrier);   // B's scope is open too
                    Log.Info("test", $"BL-306 alpha");
                    Wait(barrier);   // both have logged
                }
            }
            catch (Exception e)
            {
                failureA = e;
                RemoveSelfFromBarrier(barrier);
            }
        });

        var threadB = new Thread(() =>
        {
            try
            {
                Wait(barrier);       // A's scope is open
                using (Log.PushConsoleSink(linesB.Add))
                {
                    Wait(barrier);   // B's scope is open too
                    Log.Info("test", $"BL-306 beta");
                    Wait(barrier);   // both have logged
                }
            }
            catch (Exception e)
            {
                failureB = e;
                RemoveSelfFromBarrier(barrier);
            }
        });

        threadA.Start();
        threadB.Start();
        Assert.True(threadA.Join(BarrierTimeout), "thread A did not finish");
        Assert.True(threadB.Join(BarrierTimeout), "thread B did not finish");
        Assert.Null(failureA);
        Assert.Null(failureB);

        // Join is the memory barrier that makes both lists safe to read here.
        Assert.Single(linesA);
        Assert.Contains("alpha", linesA[0]);
        Assert.Single(linesB);
        Assert.Contains("beta", linesB[0]);
    }

    [Fact]
    public void AScopeRestoresWhatItFoundAndDisposingTwiceIsHarmless()
    {
        var outer = new List<string>();
        var inner = new List<string>();
        var before = Log.ConsoleSink;

        using (Log.PushConsoleSink(outer.Add))
        {
            var innerScope = Log.PushConsoleSink(inner.Add);
            Log.Info("test", $"BL-306 innermost");
            innerScope.Dispose();
            innerScope.Dispose();   // a second dispose must not pop the OUTER scope
            Log.Info("test", $"BL-306 back in the outer scope");
        }

        Assert.Single(inner);
        Assert.Single(outer);
        Assert.Contains("innermost", inner[0]);
        Assert.Contains("outer scope", outer[0]);

        // The process-wide default is a separate tier and a scope must never disturb it: it is
        // what keeps every OTHER test off the GD.Print fallthrough that kills the host.
        Assert.Same(before, Log.ConsoleSink);
    }

    private static void Wait(Barrier barrier)
    {
        if (!barrier.SignalAndWait(BarrierTimeout))
        {
            throw new TimeoutException("the other flow never reached the barrier");
        }
    }

    // A thread that threw is never coming back to the barrier; without this the other one blocks
    // until its own timeout and the real failure is buried under a second, meaningless one.
    private static void RemoveSelfFromBarrier(Barrier barrier)
    {
        try
        {
            barrier.RemoveParticipant();
        }
        catch (InvalidOperationException)
        {
            // Already torn down by the other thread's failure path — nothing left to remove.
        }
    }
}
