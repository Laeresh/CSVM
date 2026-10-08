using System;
using System.Runtime.CompilerServices;
using System.Threading;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>The <c>--debug-finalizers</c> instrument (docs/cli.md). It holds the .NET finalizer thread
/// through one suite and collects every few milliseconds, so a dropped Godot wrapper dies promptly.
/// The queue is drained before the next suite starts. A RefCounted object Godot reached natively
/// after its only wrapper died is left with a released handle. Its finalizer then logs "Handle is
/// not initialized" at that suite's boundary, which makes the error attributable. The binding
/// states behind it are docs/verification.md INSTR-99.</summary>
internal sealed class FinalizerGate : IDisposable
{
    // Short enough that a collection lands between a wrapper being dropped and the engine's next
    // touch of the object. The floor of the wait between collections, see Pump.
    private const int PumpMilliseconds = 20;

    // The finalizer thread is let go after this even if the suite never returns, so a hung suite
    // cannot also wedge the process's exit.
    private static readonly TimeSpan HoldLimit = TimeSpan.FromMinutes(60);

    private readonly ManualResetEventSlim _entered = new(false);
    private readonly ManualResetEventSlim _release = new(false);
    private readonly Thread _pump;
    private volatile bool _stop;
    private int _handleErrors;

    private FinalizerGate()
    {
        Drain();
        AppDomain.CurrentDomain.FirstChanceException += CountHandleError;
        Plant(_entered, _release);
        GC.Collect();
        Held = _entered.Wait(TimeSpan.FromSeconds(10));
        _pump = new Thread(Pump) { IsBackground = true, Name = "finalizer-gate-pump" };
        _pump.Start();
    }

    /// <summary>Gets a value indicating whether the finalizer thread was actually parked. False means
    /// the sentinel never reached it in time and the suite ran with finalizers live.</summary>
    public bool Held { get; }

    /// <summary>Gets how many collections the pump forced while the suite ran.</summary>
    public int Collections { get; private set; }

    /// <summary>Parks the finalizer thread for one suite. Dispose it when the suite ends.</summary>
    public static FinalizerGate Hold() => new();

    /// <summary>Stops the pump, releases the finalizer thread and waits for every queued finalizer.
    /// Returns how many "Handle is not initialized" throws the drain produced.</summary>
    public int ReleaseAndDrain()
    {
        _stop = true;
        _pump.Join();
        _release.Set();
        Drain();
        AppDomain.CurrentDomain.FirstChanceException -= CountHandleError;
        return Volatile.Read(ref _handleErrors);
    }

    public void Dispose()
    {
        if (!_stop)
        {
            ReleaseAndDrain();
        }
        _entered.Dispose();
    }

    // ⚠ Do not call it while a gate is open. The wait is on the finalizer thread that gate holds, so
    // it lasts until the hold limit.
    private static void Drain()
    {
        // Twice: a finalizer can free the last reference to another finalizable object.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    // A separate frame, so no local on the caller's stack keeps the sentinel reachable.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Plant(ManualResetEventSlim entered, ManualResetEventSlim release) =>
        _ = new Sentinel(entered, release);

    private void Pump()
    {
        var watch = new System.Diagnostics.Stopwatch();
        while (!_stop)
        {
            watch.Restart();
            GC.Collect();
            Collections++;
            // A world build grows the heap until one collection outlasts the period. The wait scales
            // with the collection, so the suite still spends most of its time running.
            Thread.Sleep(Math.Max(PumpMilliseconds, (int)watch.ElapsedMilliseconds * 4));
        }
    }

    // The exact throw Godot's binding makes when it repoints a released handle, counted on any
    // thread. The engine catches and logs it, so a first-chance hook is the only place to count it.
    private void CountHandleError(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is InvalidOperationException { Message: "Handle is not initialized." })
        {
            Interlocked.Increment(ref _handleErrors);
        }
    }

    private sealed class Sentinel
    {
        private readonly ManualResetEventSlim _entered;
        private readonly ManualResetEventSlim _release;

        public Sentinel(ManualResetEventSlim entered, ManualResetEventSlim release)
        {
            _entered = entered;
            _release = release;
        }

        ~Sentinel()
        {
            _entered.Set();
            if (!_release.Wait(HoldLimit))
            {
                Log.Warn("test", $"finalizer gate held past its limit of {HoldLimit.TotalMinutes:0} min, releasing the finalizer thread");
            }
        }
    }
}
