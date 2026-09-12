using System.Diagnostics;

namespace CSVM.Utils;

/// <summary>
/// The wall cost of the flight roster's AI walks in a window, and how many aircraft they walked.
/// The whole-frame terms cannot answer either question: <c>proc_ms</c> and <c>phys_tick_ms</c>
/// bracket whichever callback the clock mode makes the walk ride, so a plane-count sweep reads
/// only as a differential between two whole frames. This brackets the walk itself, which is what
/// lets <c>ai_ms</c> be divided by <c>ai_planes</c>. No worst-walk term, deliberately: a spike in
/// the walk is already visible in <c>proc_max_ms</c> and <c>max_ms</c>.
/// ⚠ Main thread only, and the pair must both run: a walk whose phase throws leaves the open
/// stamp standing, and the next open silently replaces it, undercounting rather than crashing.
/// ⚠ The roster walk alone. Presentation for the same aircraft (animators, audio, drawing) runs
/// in their own <c>_Process</c> callbacks and stays in <c>proc_ms</c>.
/// </summary>
public static class AiStepCost
{
    private static long _openedAt;
    private static double _accumMs;
    private static long _steps;
    private static long _planes;

    /// <summary>Walks completed since the last <see cref="Take"/>.</summary>
    public static long Steps => _steps;

    /// <summary>Stamps the start of one AI walk over the flight roster.</summary>
    public static void Open() => _openedAt = Stopwatch.GetTimestamp();

    /// <summary>Closes the walk and banks its wall cost and the <paramref name="planes"/> it
    /// stepped. A close with no open standing is dropped rather than charged, so a walk that never
    /// opened cannot bank the time since the last one.</summary>
    public static void Close(int planes)
    {
        if (_openedAt == 0)
        {
            return;
        }
        _accumMs += (Stopwatch.GetTimestamp() - _openedAt) * 1000.0 / Stopwatch.Frequency;
        _steps++;
        _planes += planes;
        _openedAt = 0;
    }

    /// <summary>Drains the window: total banked milliseconds, the walk count and the summed plane
    /// count, then resets all three. The milliseconds are per WINDOW, not per walk, because a
    /// parent-driven clock runs several walks in one rendered frame and the question <c>ai_ms</c>
    /// answers is what the AI cost that frame; divide the planes by the walks for the roster
    /// size.</summary>
    public static (double Ms, long Steps, long Planes) Take()
    {
        var taken = (_accumMs, _steps, _planes);
        _accumMs = 0;
        _steps = 0;
        _planes = 0;
        return taken;
    }

    /// <summary>Drops everything, including a half-open walk — a session build or teardown, whose
    /// stall belongs to no frame and would otherwise be banked whole by the next close.</summary>
    public static void Reset()
    {
        _openedAt = 0;
        _accumMs = 0;
        _steps = 0;
        _planes = 0;
    }
}
