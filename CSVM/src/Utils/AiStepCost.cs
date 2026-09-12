namespace CSVM.Utils;

/// <summary>
/// The wall cost of the flight roster's AI walks in a window, and how many aircraft they walked.
/// The whole-frame terms cannot answer either question: <c>proc_ms</c> and <c>phys_tick_ms</c>
/// bracket whichever callback the clock mode makes the walk ride, so a plane-count sweep reads
/// only as a differential between two whole frames. This brackets the walk itself, which is what
/// lets <c>ai_ms</c> be divided by <c>ai_planes</c>. No worst-walk term, deliberately: a spike in
/// the walk is already visible in <c>proc_max_ms</c> and <c>max_ms</c>.
/// ⚠ The roster walk alone. Presentation for the same aircraft (animators, audio, drawing) runs
/// in their own <c>_Process</c> callbacks and stays in <c>proc_ms</c>.
/// </summary>
public static class AiStepCost
{
    // No bracket nodes on this one: the walk is bracketed by hand around the call that runs it,
    // since no priority order isolates it from the rest of the callback it rides.
    private static readonly WallCostBank Bank = new("AiStep");

    /// <summary>Walks completed since the last <see cref="Take"/>.</summary>
    public static long Steps => Bank.Spans;

    /// <summary>Stamps the start of one AI walk over the flight roster.</summary>
    public static void Open() => Bank.Open();

    /// <summary>Closes the walk and banks its wall cost and the <paramref name="planes"/> it
    /// stepped.</summary>
    public static void Close(int planes) => Bank.Close(planes);

    /// <summary>Drains the window: total banked milliseconds, the walk count and the summed plane
    /// count. The milliseconds are per WINDOW, not per walk, because a parent-driven clock runs
    /// several walks in one rendered frame and the question <c>ai_ms</c> answers is what the AI cost
    /// that frame; divide the planes by the walks for the roster size.</summary>
    public static (double Ms, long Steps, long Planes) Take()
    {
        var (ms, _, steps, planes) = Bank.Take();
        return (ms, steps, planes);
    }

    /// <summary>Drops everything, including a half-open walk.</summary>
    public static void Reset() => Bank.Reset();
}
