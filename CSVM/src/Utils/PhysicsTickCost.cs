using System.Diagnostics;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The wall cost of ONE whole Godot physics tick, and how many ticks a wall second actually got.
/// Godot's <c>TIME_PHYSICS_PROCESS</c> monitor cannot answer either question: the engine refreshes
/// it about once a second and holds the WORST step of that second in it, so a window's reading
/// routinely exceeds the worst frame in the same window. This measures the tick directly instead,
/// with two bracket nodes at the ends of the physics priority order, and counts the ticks the
/// engine managed. Rules and the misreading it replaces: docs/verification.md PERF-21.
/// ⚠ Main thread only, and the two brackets must both be in the tree: a missing tail leaves the
/// open stamp standing and the next open silently replaces it, undercounting rather than crashing.
/// </summary>
public static class PhysicsTickCost
{
    /// <summary>Sim seconds a wall second is worth at a measured tick rate: Godot advances the
    /// physics-stepped sim one <c>1/60</c> step per tick, so 60 ticks a second is real time and
    /// half that is a sim running at half speed.</summary>
    public const double NominalHz = 60.0;

    private static long _openedAt;
    private static double _accumMs;
    private static double _maxMs;
    private static long _ticks;

    /// <summary>Ticks completed since the last <see cref="Take"/>.</summary>
    public static long Ticks => _ticks;

    /// <summary>Stamps the start of a physics tick. Called by the head bracket.</summary>
    public static void Open() => _openedAt = Stopwatch.GetTimestamp();

    /// <summary>Closes the tick the head bracket opened and banks its wall cost. A close with no
    /// open standing is dropped rather than charged, so the first tick after a rebuild cannot
    /// bank the whole build.</summary>
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
        _ticks++;
        _openedAt = 0;
    }

    /// <summary>Drains the window: total banked milliseconds, the worst single tick in it, and the
    /// tick count, then resets all three. Returns zeros when no tick closed, which is what a fully
    /// parent-driven session reads. The worst tick is here because it is the quantity Godot's
    /// own <c>TIME_PHYSICS_PROCESS</c> actually holds, so the two can be compared side by side.</summary>
    public static (double Ms, double MaxMs, long Ticks) Take()
    {
        var taken = (_accumMs, _maxMs, _ticks);
        _accumMs = 0;
        _maxMs = 0;
        _ticks = 0;
        return taken;
    }

    /// <summary>Drops everything, including a half-open tick — a session teardown, where the
    /// standing open would otherwise be closed by the next session's first tail.</summary>
    public static void Reset()
    {
        _openedAt = 0;
        _accumMs = 0;
        _maxMs = 0;
        _ticks = 0;
    }
}

/// <summary>One end of <see cref="PhysicsTickCost"/>'s bracket. Two of these sit at the extremes
/// of Godot's physics priority order, so the pair spans every <c>_PhysicsProcess</c> callback in
/// the tree whatever subtree it lives in. <see cref="ProcessModeEnum.Always"/> on both, or a pause
/// would stop one end and not the other.</summary>
public sealed partial class PhysicsTickBracket : Node
{
    private bool _tail;

    /// <summary>Builds the head (stamps the start) or the tail (banks the cost).</summary>
    public static PhysicsTickBracket Make(bool tail)
    {
        var node = new PhysicsTickBracket
        {
            Name = tail ? "PhysicsTickTail" : "PhysicsTickHead",
            ProcessMode = ProcessModeEnum.Always,
            ProcessPhysicsPriority = tail ? int.MaxValue : int.MinValue,
        };
        node._tail = tail;
        return node;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_tail)
        {
            PhysicsTickCost.Close();
        }
        else
        {
            PhysicsTickCost.Open();
        }
    }
}
