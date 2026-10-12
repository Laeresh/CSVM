using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>What a stunt seat's respawn control asks of its aircraft this step.</summary>
public enum StuntRunCall
{
    /// <summary>Nothing: the aircraft flies on, or a wreck keeps falling.</summary>
    None,

    /// <summary>A tap: back through the zone cleared last, zones and clock kept.</summary>
    Return,

    /// <summary>A hold: a fresh run from the start.</summary>
    Rerun,
}

/// <summary>
/// A stunt seat's run control. It owns the respawn button split by hold length, the start count
/// with the clock it holds, and the pose a tap returns to. Engine-free, one per seat. The aircraft
/// asks it on the crashed path, the live path and the count's step, and performs the answer.
/// </summary>
public sealed class StuntRunControl
{
    private readonly TapHoldButton _split = new(TapHoldButton.PadHoldSeconds);
    private readonly StartCount _count = new();

    // A tap's pose, armed by ArmReturn and spent by the one respawn that takes it. ⚠ Never written
    // into the seat's spawn pose: the held rerun must still find the start line there.
    private (Vector3 Pos, Basis Attitude)? _zoneReturn;

    /// <summary>The seat's start count, for its readers (the HUD, the suites). Begun, cancelled
    /// and stepped only through this control.</summary>
    public StartCount Count => _count;

    /// <summary>Whether this step is the count's, as <see cref="StepClock"/> found it: the clock
    /// stands and the controls are held. A crashed or remote seat's count is not counting.</summary>
    public bool Counting { get; private set; }

    /// <summary>Whether a count is running at all, frozen by a crash or not.</summary>
    public bool CountRunning => _count.Running;

    /// <summary>Starts a count of <paramref name="phases"/> from its first figure.</summary>
    public void BeginCount(IReadOnlyList<StartCountPhase> phases) => _count.Begin(phases);

    /// <summary>Drops the count, figure and GO alike, as a respawn's new pose requires.</summary>
    public void CancelCount() => _count.Cancel();

    /// <summary>Moves a running count on by <paramref name="seconds"/> without stepping it.</summary>
    public void CatchUpCount(float seconds) => _count.CatchUp(seconds);

    /// <summary>Where the count's walk puts the aircraft now; the spawn pose itself once it has
    /// handed over.</summary>
    public (Vector3 Position, Basis Attitude) WalkPose(Vector3 spawnPos, Basis spawnAttitude, float spawnSpeed) =>
        _count.WalkPose(spawnPos, spawnAttitude, spawnSpeed);

    /// <summary>One crashed step. A hold reruns. A tap returns, as does the crash cam's own timer
    /// while the button is up. The timer is ticked through <paramref name="timerDue"/>, asked
    /// only when no press decided the step.</summary>
    public StuntRunCall StepCrashed(bool down, float dt, Func<float, bool> timerDue)
    {
        var press = _split.Step(down, dt);
        if (press == TapHold.Hold)
            return StuntRunCall.Rerun;
        return press == TapHold.Tap || (!down && timerDue(dt)) ? StuntRunCall.Return : StuntRunCall.None;
    }

    /// <summary>One flying step where the respawn is allowed; <paramref name="down"/> is the
    /// button's level.</summary>
    public StuntRunCall StepLive(bool down, float dt) => _split.Step(down, dt) switch
    {
        TapHold.Hold => StuntRunCall.Rerun,
        TapHold.Tap => StuntRunCall.Return,
        _ => StuntRunCall.None,
    };

    /// <summary>One flying step where the respawn is refused. The button is not read and nothing
    /// is answered.</summary>
    public void StepLiveRefused(float dt)
    {
        // ⚠ Step the split even where refused, reading up. A refused press must not live on in the
        // button and resolve after a crash.
        _split.Step(false, dt);
    }

    /// <summary>The step's run clock, read before anything steps: sets <see cref="Counting"/> and
    /// ticks <paramref name="run"/> unless the count holds it. The GO step is still the count's,
    /// so the clock starts on the step after.</summary>
    public void StepClock(StuntMission? run, bool crashed, bool remote, float dt)
    {
        Counting = _count.Running && !crashed && !remote;
        if (!Counting)
            run?.Tick(dt);
    }

    /// <summary>The step's count. While <see cref="Counting"/> it advances and answers its cue, and
    /// the aircraft rides the walk. Otherwise it answers null, having aged GO's figure, or left a
    /// count frozen by a crash where it stands.</summary>
    public StartCountCue? StepCount(float dt)
    {
        if (Counting)
            return _count.Advance(dt);
        if (!_count.Running)
            _count.Advance(dt);
        return null;
    }

    /// <summary>Arms a tap's return: on the route abeam the exit of the zone cleared last, heading
    /// the way it was flown. With no zone cleared nothing is armed, and the respawn is the start.</summary>
    public void ArmReturn(StuntMission? run)
    {
        if (run?.ReturnPose() is { } exit)
        {
            // A heading straight up or down has no wings-level roll off world up.
            var up = Mathf.Abs(exit.Heading.Y) > 0.999f ? Vector3.Back : Vector3.Up;
            _zoneReturn = (exit.Position, Basis.LookingAt(exit.Heading, up));
        }
    }

    /// <summary>The armed return, or null; taking it disarms it.</summary>
    public (Vector3 Pos, Basis Attitude)? TakeReturn()
    {
        var taken = _zoneReturn;
        _zoneReturn = null;
        return taken;
    }
}
