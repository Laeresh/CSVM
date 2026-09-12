using System.Diagnostics;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// One <c>--perf</c> cost meter: an open/close bracket around a repeated span, banking the wall
/// milliseconds, the worst single span, the spans that closed, and a tally the caller carries
/// alongside them (the aircraft an AI walk stepped). One instance per meter behind a static facade
/// that names its own terms, so the three readouts share one drain semantics instead of three
/// copies of it. A facade reports only the terms its readout prints; the unused slots stay zero.
/// <see cref="Label"/> names the meter, and a bracket node takes its node name from it.
/// ⚠ Main thread only, and the pair must both run: a span whose close never happens leaves the open
/// stamp standing, and the next open silently replaces it, undercounting rather than crashing.
/// </summary>
public sealed class WallCostBank
{
    private long _openedAt;
    private double _accumMs;
    private double _maxMs;
    private long _spans;
    private long _tally;

    public WallCostBank(string label) => Label = label;

    /// <summary>Names the meter. A bracket node is this plus <c>Head</c> or <c>Tail</c>.</summary>
    public string Label { get; }

    /// <summary>Spans closed since the last <see cref="Take"/>.</summary>
    public long Spans => _spans;

    /// <summary>Stamps the start of a span.</summary>
    public void Open() => _openedAt = Stopwatch.GetTimestamp();

    /// <summary>Closes the span, banking its wall cost and <paramref name="tally"/>. A close with no
    /// open standing is dropped rather than charged, so a span that never opened cannot bank the
    /// time since the last one.</summary>
    public void Close(long tally = 0)
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
        _spans++;
        _tally += tally;
        _openedAt = 0;
    }

    /// <summary>Drains the window and resets every accumulator. A span still open is carried whole
    /// into the next window, which is what a reader sitting inside the span it measures depends
    /// on.</summary>
    public (double Ms, double MaxMs, long Spans, long Tally) Take()
    {
        var taken = (_accumMs, _maxMs, _spans, _tally);
        _accumMs = 0;
        _maxMs = 0;
        _spans = 0;
        _tally = 0;
        return taken;
    }

    /// <summary>Drops everything, including a half-open span: a session build or teardown, whose
    /// stall belongs to no frame and would otherwise be banked whole by the next close.</summary>
    public void Reset()
    {
        _openedAt = 0;
        _accumMs = 0;
        _maxMs = 0;
        _spans = 0;
        _tally = 0;
    }
}

/// <summary>One end of a <see cref="WallCostBank"/>'s bracket. Two of these sit at the extremes of
/// Godot's process or physics priority order, so the pair spans every callback of that kind in the
/// tree whatever subtree it lives in. <see cref="Node.ProcessModeEnum.Always"/> on both, or a pause
/// would stop one end and not the other.</summary>
public sealed partial class WallCostBracket : Node
{
    private WallCostBank _bank = null!;
    private bool _physics;
    private bool _tail;

    /// <summary>Builds the head (stamps the start) or the tail (banks the cost) of the pair around
    /// <paramref name="bank"/>'s span, on the physics clock or the process clock.</summary>
    public static WallCostBracket Make(WallCostBank bank, bool physics, bool tail)
    {
        var node = new WallCostBracket
        {
            Name = bank.Label + (tail ? "Tail" : "Head"),
            ProcessMode = ProcessModeEnum.Always,
        };
        node._bank = bank;
        node._physics = physics;
        node._tail = tail;
        if (physics)
        {
            node.ProcessPhysicsPriority = tail ? int.MaxValue : int.MinValue;
        }
        else
        {
            node.ProcessPriority = tail ? int.MaxValue : int.MinValue;
        }
        return node;
    }

    /// <summary>Switches off the clock this bracket does not measure. Here rather than in
    /// <see cref="Make"/> because Godot turns on every callback the class overrides as the node
    /// becomes ready, which would undo an earlier switch-off.</summary>
    public override void _Ready()
    {
        SetProcess(!_physics);
        SetPhysicsProcess(_physics);
    }

    public override void _Process(double delta)
    {
        // Guarded as well as switched off above, so a physics bracket cannot bank a process pass
        // whatever the engine does with the callbacks it finds on the class.
        if (!_physics)
        {
            Fire();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_physics)
        {
            Fire();
        }
    }

    private void Fire()
    {
        if (_tail)
        {
            _bank.Close();
        }
        else
        {
            _bank.Open();
        }
    }
}
