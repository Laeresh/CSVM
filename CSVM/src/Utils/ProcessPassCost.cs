using System.Diagnostics;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The wall cost of ONE whole Godot <c>_Process</c> pass, and how many passes a window held.
/// Godot's <c>TIME_PROCESS</c> monitor answers neither question: the engine refreshes it about once
/// a second and holds the WORST pass of that second, so a window's reading routinely exceeds the
/// worst frame in the same window and repeats byte-for-byte across consecutive windows. This
/// measures the pass directly instead, with two bracket nodes at the ends of the process priority
/// order. Rules and the misreading it replaces: docs/verification.md PERF-1.
/// ⚠ Main thread only, and the two brackets must both be in the tree: a missing tail leaves the
/// open stamp standing and the next open silently replaces it, undercounting rather than crashing.
/// </summary>
public static class ProcessPassCost
{
    private static long _openedAt;
    private static double _accumMs;
    private static double _maxMs;
    private static long _passes;

    /// <summary>Passes completed since the last <see cref="Take"/>.</summary>
    public static long Passes => _passes;

    /// <summary>Stamps the start of a process pass. Called by the head bracket.</summary>
    public static void Open() => _openedAt = Stopwatch.GetTimestamp();

    /// <summary>Closes the pass the head bracket opened and banks its wall cost. A close with no
    /// open standing is dropped rather than charged, so the first pass after a rebuild cannot bank
    /// the whole build.</summary>
    public static void Close()
    {
        if (_openedAt == 0)
        {
            return;
        }
        double ms = (Stopwatch.GetTimestamp() - _openedAt) * 1000.0 / Stopwatch.Frequency;
        _accumMs += ms;
        if (ms > _maxMs)
        {
            _maxMs = ms;
        }
        _passes++;
        _openedAt = 0;
    }

    /// <summary>Drains the window: total banked milliseconds, the worst single pass in it, and the
    /// pass count, then resets all three. A caller reading from inside the pass gets the frame in
    /// progress in its NEXT window, because the tail has not run yet. The worst pass is here
    /// because it is the quantity Godot's own <c>TIME_PROCESS</c> holds, so the two can be compared
    /// side by side.</summary>
    public static (double Ms, double MaxMs, long Passes) Take()
    {
        var taken = (_accumMs, _maxMs, _passes);
        _accumMs = 0;
        _maxMs = 0;
        _passes = 0;
        return taken;
    }

    /// <summary>Drops everything, including a half-open pass — a session build or teardown, whose
    /// stall belongs to no frame and would otherwise be banked whole by the next close.</summary>
    public static void Reset()
    {
        _openedAt = 0;
        _accumMs = 0;
        _maxMs = 0;
        _passes = 0;
    }
}

/// <summary>One end of <see cref="ProcessPassCost"/>'s bracket. Two of these sit at the extremes of
/// Godot's process priority order, so the pair spans every <c>_Process</c> callback in the tree
/// whatever subtree it lives in. <see cref="ProcessModeEnum.Always"/> on both, or a pause would
/// stop one end and not the other.</summary>
public sealed partial class ProcessPassBracket : Node
{
    private bool _tail;

    /// <summary>Builds the head (stamps the start) or the tail (banks the cost).</summary>
    public static ProcessPassBracket Make(bool tail)
    {
        var node = new ProcessPassBracket
        {
            Name = tail ? "ProcessPassTail" : "ProcessPassHead",
            ProcessMode = ProcessModeEnum.Always,
            ProcessPriority = tail ? int.MaxValue : int.MinValue,
        };
        node._tail = tail;
        return node;
    }

    public override void _Process(double delta)
    {
        if (_tail)
        {
            ProcessPassCost.Close();
        }
        else
        {
            ProcessPassCost.Open();
        }
    }
}
