using System.Collections.Generic;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.Roster;

/// <summary>
/// A time-attack race's watchers. A pane whose pilot has no run in the final run (lost,
/// finished, or out at time up) spectates after its crash camera's time. It locks onto a racer
/// still in a run, moves on when that racer leaves its run, and its target key cycles between
/// those racers. A seat flown elsewhere is watched through the copy this machine flies. Before
/// time up no pane is handed over, and a new window hands every pane back. Panes only.
/// </summary>
internal sealed class RaceSpectate
{
    private readonly StuntRace _race;
    private readonly IReadOnlyList<PlayerRig> _panes;
    private readonly IReadOnlyList<PlayerRig> _seats;
    private readonly Node3D _worldRoot;
    private readonly List<SpectatorCamera> _cameras;

    // Seconds each pane's pilot has stood out of a run in the final run.
    private readonly Dictionary<PlayerRig, float> _outFor = new();

    // The spectator each handed-over pane watches through.
    private readonly Dictionary<PlayerRig, SpectatorCamera> _watchers = new();

    internal RaceSpectate(StuntRace race, IReadOnlyList<PlayerRig> panes, IReadOnlyList<PlayerRig> seats,
        Node3D worldRoot, List<SpectatorCamera> cameras)
    {
        _race = race;
        _panes = panes;
        _seats = seats;
        _worldRoot = worldRoot;
        _cameras = cameras;
        // On the event, not in a step: a race that ends on a leaving seat halts the clock first.
        race.RaceCompleted += () => SpectateHandoff.ReleaseControls(_panes);
    }

    /// <summary>One sim step, after the race's own.</summary>
    internal void Step(float dt)
    {
        foreach (var pane in _panes)
        {
            if (pane.Controller is not { IsBotSeat: false } pilot)
                continue;
            if (pilot.Spectating)
            {
                MoveOn(pane);
                continue;
            }

            if (_race.Phase != StuntRacePhase.FinalRun || _race.Of(pilot.PlayerIndex) is not { InRun: false })
            {
                _outFor.Remove(pane);
                continue;
            }

            float outFor = _outFor.GetValueOrDefault(pane) + dt;
            _outFor[pane] = outFor;
            var running = Running();
            // With nobody left in a run the race is ending, so no pane is handed over to watch nothing.
            if (outFor < pilot.CrashCameraSeconds || running.Count == 0)
                continue;
            if (!SpectateHandoff.Begin(pane, running, _worldRoot, Candidates, _cameras, out var follow))
                continue;
            var watcher = _cameras[^1];
            _watchers[pane] = watcher;
            pilot.SetSpectatorHud(true, () => watcher.Follow is FlightController watched ? _race.Of(watched.PlayerIndex) : null);
            Log.Info("flight", $"stunt race: P{pilot.PlayerIndex + 1} is out of a run in the final run, spectating P{follow!.PlayerIndex + 1}");
        }
    }

    /// <summary>Hands every watcher's pane back to its pilot, ahead of a new window.</summary>
    internal void HandBack()
    {
        SpectateHandoff.HandBack(_panes, _cameras);
        _watchers.Clear();
        _outFor.Clear();
    }

    // A watcher locked onto a racer who has left its run moves on to one still in a run. One whose
    // lock the pilot released stays free.
    private void MoveOn(PlayerRig pane)
    {
        if (!_watchers.TryGetValue(pane, out var watcher) || !GodotObject.IsInstanceValid(watcher)
            || watcher.Follow is not FlightController watched || IsRunning(watched))
            return;
        var running = Running();
        if (running.Count > 0)
            watcher.FollowNode(running[0].Controller);
    }

    private bool IsRunning(FlightController racer) =>
        racer.InPlay && _race.Of(racer.PlayerIndex) is { InRun: true };

    // The seats still in a run with an aeroplane in play, the only ones a watcher may follow.
    private List<PlayerRig> Running()
    {
        var running = new List<PlayerRig>();
        foreach (var seat in _seats)
        {
            if (seat.Controller is { } racer && IsRunning(racer))
                running.Add(seat);
        }

        return running;
    }

    // The target key's candidates, read afresh on each press.
    private IReadOnlyList<Node3D> Candidates() => Running().ConvertAll<Node3D>(seat => seat.Controller!);
}
