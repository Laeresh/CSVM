namespace CSVM.Utils;

/// <summary>
/// The wall cost of ONE whole Godot <c>_Process</c> pass, and how many passes a window held.
/// Godot's <c>TIME_PROCESS</c> monitor answers neither question: the engine refreshes it about once
/// a second and holds the WORST pass of that second, so a window's reading routinely exceeds the
/// worst frame in the same window and repeats byte-for-byte across consecutive windows. This
/// measures the pass directly instead, with two bracket nodes at the ends of the process priority
/// order. Rules and the misreading it replaces: docs/verification.md PERF-1.
/// </summary>
public static class ProcessPassCost
{
    private static readonly WallCostBank Bank = new("ProcessPass");

    /// <summary>Passes completed since the last <see cref="Take"/>.</summary>
    public static long Passes => Bank.Spans;

    /// <summary>Builds the head or the tail of the pair spanning the process pass. Both ends must be
    /// in the tree: a missing tail leaves the open stamp standing and the next open replaces it,
    /// undercounting rather than crashing.</summary>
    public static WallCostBracket MakeBracket(bool tail) => WallCostBracket.Make(Bank, physics: false, tail: tail);

    /// <summary>Stamps the start of a process pass. Called by the head bracket.</summary>
    public static void Open() => Bank.Open();

    /// <summary>Closes the pass the head bracket opened and banks its wall cost.</summary>
    public static void Close() => Bank.Close();

    /// <summary>Drains the window: total banked milliseconds, the worst single pass in it, and the
    /// pass count. A caller reading from inside the pass gets the frame in progress in its NEXT
    /// window, because the tail has not run yet. The worst pass is here because it is the quantity
    /// Godot's own <c>TIME_PROCESS</c> holds, so the two can be compared side by side.</summary>
    public static (double Ms, double MaxMs, long Passes) Take()
    {
        var (ms, maxMs, passes, _) = Bank.Take();
        return (ms, maxMs, passes);
    }

    /// <summary>Drops everything, including a half-open pass: a session build or teardown, whose
    /// stall belongs to no frame and would otherwise be banked whole by the next close.</summary>
    public static void Reset() => Bank.Reset();
}
