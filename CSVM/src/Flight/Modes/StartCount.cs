using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>What one step of a <see cref="StartCount"/> asks its presentation to sound.</summary>
public enum StartCountCue
{
    /// <summary>Nothing new this step.</summary>
    None,

    /// <summary>A figure came up: the beep that goes with it.</summary>
    Beat,

    /// <summary>The count handed over: the higher GO tone.</summary>
    Go,
}

/// <summary>One figure of a start count: the text it shows and how long it stands, seconds.</summary>
public readonly record struct StartCountPhase(string Label, float Seconds);

/// <summary>
/// A run's start count: figures in turn, then GO. Engine-free, one per seat, and seats begun on
/// the same step stay in lockstep because each advances on the same sim dt.
/// While it runs, the aircraft rides a kinematic walk along its spawn heading and nothing is
/// simulated. The walk ends on the spawn pose exactly on the GO step, whatever the spawn speed.
/// It starts the spawn speed times the count's length back, so its velocity is the one the
/// flight model takes at GO. The controls are held up to and including the GO step, and the
/// run clock starts on the step after (<see cref="StuntMission.Elapsed"/>).
/// </summary>
public sealed class StartCount
{
    /// <summary>How long GO stands after the hand-over, seconds. TUNE at the controls.</summary>
    public const float GoSeconds = 1f;

    /// <summary>The text GO shows.</summary>
    public const string GoLabel = "GO";

    /// <summary>The count a restart opens with: 3, 2, 1, a second each.</summary>
    public static readonly IReadOnlyList<StartCountPhase> Restart = new StartCountPhase[]
    {
        new("3", 1f), new("2", 1f), new("1", 1f),
    };

    // A step landing this close short of a boundary counts as on it. Summed 1/60 s steps drift a
    // few microseconds off a whole second, which would otherwise add a frame to each figure.
    private static readonly float BoundaryTolerance = 1e-4f;

    private IReadOnlyList<StartCountPhase> _phases = Array.Empty<StartCountPhase>();
    private float _elapsed;
    private int _cued = -1;
    private float _sinceGo = float.PositiveInfinity;

    /// <summary>Whether the count is running: the walk drives the aircraft and the controls are held.</summary>
    public bool Running { get; private set; }

    /// <summary>The whole count's length, seconds: every figure's, GO excluded.</summary>
    public float Duration { get; private set; }

    /// <summary>Seconds left before GO; zero once the count has handed over or was never begun.</summary>
    public float Remaining => Running ? Mathf.Max(0f, Duration - _elapsed) : 0f;

    /// <summary>Which figure stands, an index into the phases <see cref="Begin"/> took. −1 when not running.</summary>
    public int Phase => Running ? PhaseAt(_elapsed) : -1;

    /// <summary>The text to draw: the standing figure, GO for <see cref="GoSeconds"/> after the
    /// hand-over, or null.</summary>
    public string? Figure => Running ? _phases[PhaseAt(_elapsed)].Label
        : _sinceGo < GoSeconds ? GoLabel : null;

    /// <summary>Seconds since <see cref="Figure"/> came up, for its fade.</summary>
    public float FigureAge => Running ? _elapsed - PhaseStart(PhaseAt(_elapsed)) : _sinceGo;

    /// <summary>The count a race window opens with: READY for <paramref name="readySeconds"/>
    /// while the field rolls in, then <see cref="Restart"/>'s figures.</summary>
    public static IReadOnlyList<StartCountPhase> Opening(float readySeconds)
    {
        var phases = new List<StartCountPhase> { new("READY", readySeconds) };
        phases.AddRange(Restart);
        return phases;
    }

    /// <summary>Starts the count from its first figure. The first <see cref="Advance"/> sounds that
    /// figure's beat, so a count begun while the session is still building beeps when it moves.</summary>
    public void Begin(IReadOnlyList<StartCountPhase> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        if (phases.Count == 0)
            throw new ArgumentException("a start count needs at least one figure", nameof(phases));
        _phases = phases;
        Duration = 0f;
        foreach (var phase in phases)
            Duration += phase.Seconds;
        _elapsed = 0f;
        _cued = -1;
        _sinceGo = float.PositiveInfinity;
        Running = true;
    }

    /// <summary>Drops the count, figure and GO alike. A respawn does this, since it places the
    /// aircraft somewhere the walk would drag it back from.</summary>
    public void Cancel()
    {
        Running = false;
        _sinceGo = float.PositiveInfinity;
    }

    /// <summary>One sim step. Running, it moves the count on and answers <see cref="StartCountCue.Go"/>
    /// on the step that reaches the end, or a beat when a figure comes up. Otherwise it only ages GO.</summary>
    public StartCountCue Advance(float dt)
    {
        if (!Running)
        {
            _sinceGo += dt;
            return StartCountCue.None;
        }
        _elapsed += dt;
        if (Duration - _elapsed <= BoundaryTolerance)
        {
            Running = false;
            _sinceGo = 0f;
            return StartCountCue.Go;
        }
        int phase = PhaseAt(_elapsed);
        if (phase == _cued)
            return StartCountCue.None;
        _cued = phase;
        return StartCountCue.Beat;
    }

    /// <summary>Where the walk puts the aircraft now: back along the spawn heading by the spawn speed
    /// times <see cref="Remaining"/>, at the spawn attitude. ⚠ Answers the spawn pose itself, not a
    /// sum that lands on it, once nothing remains. GO must be today's spawn state bit for bit.</summary>
    public (Vector3 Position, Basis Attitude) WalkPose(Vector3 spawnPos, Basis spawnAttitude, float spawnSpeed)
    {
        float remaining = Remaining;
        if (remaining <= 0f)
            return (spawnPos, spawnAttitude);
        // The flight model's own nose, so the walk runs down the velocity it takes at GO.
        var nose = -spawnAttitude.Orthonormalized().Z;
        return (spawnPos - (nose * (spawnSpeed * remaining)), spawnAttitude);
    }

    private int PhaseAt(float elapsed)
    {
        float boundary = 0f;
        for (int i = 0; i < _phases.Count; i++)
        {
            boundary += _phases[i].Seconds;
            if (elapsed < boundary - BoundaryTolerance)
                return i;
        }
        return _phases.Count - 1;
    }

    private float PhaseStart(int phase)
    {
        float start = 0f;
        for (int i = 0; i < phase; i++)
            start += _phases[i].Seconds;
        return start;
    }
}
