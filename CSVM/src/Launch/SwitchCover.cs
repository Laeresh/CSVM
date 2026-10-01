using System;
using System.Diagnostics;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// A live graphics switch drawn over, so the stall reads as work rather than a crash. The flight is
/// held first, then a cover goes over the whole window, and the switch runs once the cover has
/// presented. Both drop when the frames after it have settled, which is past the renderer's
/// pipeline and variant builds. The hold is its own <see cref="HaltReason"/>, so a player's pause
/// stays up after it. The stall's catch-up ticks pass while the clock is still held.
/// </summary>
public sealed class SwitchCover
{
    // Frames the cover stands before the switch runs, so the renderer has presented it.
    private const int ShowFrames = 2;

    // A frame counts toward settling at or under this many milliseconds, or under twice the fastest
    // frame since the switch. A machine whose ordinary frame runs past the fixed bar still settles.
    private const double SettledMs = 50.0;

    // No frame this long counts toward settling, however slow the frames around it.
    private const double StallMs = 500.0;

    // Settled frames in a row that drop the cover.
    private const int SettledRun = 3;

    // Wall milliseconds after the switch the cover stands at most, whatever the frames read.
    private const double MaxMs = 30000.0;

    private readonly CanvasLayer _layer;
    private readonly PauseState? _pause;
    private readonly GameClock? _clock;
    private readonly bool _clockWasHalted;
    private readonly string _why;
    private Action? _work;
    private int _frames;
    private int _settled;
    private double _workMs;
    private double _sinceMs;
    private double _fastestMs = double.PositiveInfinity;

    private SwitchCover(CanvasLayer layer, PauseState? pause, GameClock? clock, Action work, string why)
    {
        _layer = layer;
        _pause = pause;
        _clock = clock;
        _clockWasHalted = clock?.Halted ?? false;
        _work = work;
        _why = why;
    }

    /// <summary>Where the cover stands.</summary>
    public enum Phase
    {
        /// <summary>Up, before the switch.</summary>
        Showing,

        /// <summary>The switch has run, and the frames after it are being watched.</summary>
        Settling,

        /// <summary>Down, the hold released.</summary>
        Done,
    }

    /// <summary>Gets where the cover stands.</summary>
    public Phase Stage { get; private set; }

    /// <summary>Holds the flight and puts <paramref name="overlay"/> over the whole window under
    /// <paramref name="host"/>. <paramref name="work"/> runs on a later <see cref="Tick"/>. With no
    /// <paramref name="pause"/>, as in a viewer or freecam session, the clock is held directly and
    /// put back as it was.</summary>
    public static SwitchCover Begin(Node host, Control overlay, PauseState? pause, GameClock? clock,
        Action work, string why)
    {
        ArgumentNullException.ThrowIfNull(host);
        var layer = new CanvasLayer { Name = "switch_cover", Layer = UI.Boards.HudLayers.Board };
        var cover = new SwitchCover(layer, pause, clock, work, why);
        pause?.Raise(HaltReason.Switching);
        if (clock != null)
            clock.Halted = true;
        layer.AddChild(overlay);
        host.AddChild(layer);
        Log.Info("world", $"switch cover: up for {why} (the player's pause {(pause?.Paused == true ? "up" : "down")}, clock held {clock?.Halted ?? false})");
        return cover;
    }

    /// <summary>One frame, given its wall time. Runs the switch on the frame the cover has presented
    /// by, then drops the cover once the frames settle. Returns whether it is done.</summary>
    public bool Tick(double frameMs)
    {
        switch (Stage)
        {
            case Phase.Showing:
                if (++_frames < ShowFrames)
                    break;
                // The pump presents the cover now, so it is on screen however the frames queue.
                LoadProgress.Current?.Pump();
                var work = _work!;
                _work = null;
                long start = Stopwatch.GetTimestamp();
                work();
                _workMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Stage = Phase.Settling;
                _frames = 0;
                break;
            case Phase.Settling:
                _frames++;
                _sinceMs += frameMs;
                _fastestMs = Math.Min(_fastestMs, frameMs);
                double bar = Math.Min(StallMs, Math.Max(SettledMs, 2.0 * _fastestMs));
                _settled = frameMs <= bar ? _settled + 1 : 0;
                if (_settled >= SettledRun || _sinceMs >= MaxMs)
                    Drop();
                break;
        }
        return Stage == Phase.Done;
    }

    /// <summary>Takes the cover down and releases the hold now, the switch run or not. For a session
    /// torn down under it.</summary>
    public void Drop()
    {
        if (Stage == Phase.Done)
            return;
        bool ran = _work == null;
        Stage = Phase.Done;
        _layer.GetParent()?.RemoveChild(_layer);
        _layer.QueueFree();
        if (_pause != null)
            _pause.Clear(HaltReason.Switching);
        if (_clock != null)
            _clock.Halted = _pause?.ClockHeld ?? _clockWasHalted;
        Log.Info("world", $"switch cover: down after {_frames} frame(s), switch ran {ran} in {_workMs:0.0} ms (the player's pause {(_pause?.Paused == true ? "up" : "down")}, clock held {_clock?.Halted ?? false}) by {_why}");
    }
}
