using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CSVM.Utils;

/// <summary>
/// The live graphics-mode switch's own stopwatch: named steps, each the wall time since the one
/// before, written as one line when the switch ends. Main thread only. A step that blocks on the
/// render thread shows the wait there, which is how a queued shader compile surfaces.
/// </summary>
public static class SwitchProfile
{
    private static readonly List<(string Step, double Ms)> Steps = new();
    private static long _mark;

    /// <summary>Starts a new profile.</summary>
    public static void Begin()
    {
        Steps.Clear();
        _mark = Stopwatch.GetTimestamp();
    }

    /// <summary>Closes the step that ran since the last mark under <paramref name="step"/>.</summary>
    public static void Mark(string step)
    {
        if (_mark == 0)
            return;
        long now = Stopwatch.GetTimestamp();
        Steps.Add((step, Stopwatch.GetElapsedTime(_mark, now).TotalMilliseconds));
        _mark = now;
    }

    /// <summary>The steps so far in order, those that cost a millisecond or more.</summary>
    public static string Line() => string.Join(" ", Steps.Where(s => s.Ms >= 1.0).Select(s =>
        s.Step + "=" + s.Ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Ends the profile, so a mark outside a switch records nothing.</summary>
    public static void End() => _mark = 0;
}
