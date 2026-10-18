using System;
using System.Collections.Generic;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using Godot;

namespace CSVM.Session.Roster;

/// <summary>
/// What is done to a pane whose pilot is out, and undone when a race's new window flies it again.
/// The aeroplane is pinned so neither <c>R</c> nor an armed respawn timer flies it. The controller
/// stops writing this pane's camera, and a <see cref="SpectatorCamera"/> takes it, orbit-locked
/// onto a live aeroplane or free when none is left. Its callers differ only in what "out" is:
/// Instant Action's last life, a lost campaign human, a race's pilot with no run left
/// (<see cref="RaceSpectate"/>). The failure mode is shared too: a pane left on a camera nobody drives.
/// </summary>
internal static class SpectateHandoff
{
    /// <summary>Hands <paramref name="rig"/>'s pane over, answering the aircraft the spectator
    /// locked onto (null when it starts free at the crash camera). ⚠ False when this pilot is
    /// already spectating, which is what makes a second report of the same death a no-op rather
    /// than a second camera on the pane.</summary>
    internal static bool Begin(PlayerRig rig, IReadOnlyList<PlayerRig> rigs, Node3D worldRoot,
        Func<IReadOnlyList<Node3D>>? lockCandidates, List<SpectatorCamera>? tracked,
        out FlightController? follow)
    {
        follow = null;
        if (rig.Controller is not { Spectating: false } pilot)
        {
            return false;
        }

        pilot.Spectating = true;
        pilot.CameraOwned = true;   // the spectator owns this pane from here
        if (!pilot.Crashed)
        {
            // A pilot still flying (a race's finished run) leaves the cockpit and its HUD, as a wreck
            // does at the crash cut. Its aeroplane flies on as trimmed: the pane's controls are the camera's.
            pilot.SetViewedFromOutside(true);
            pilot.SetPilotHudVisible(false);
            pilot.ControlHold = FlightControlHold.All;
        }

        foreach (var other in rigs)
        {
            if (other.Controller is { InPlay: true } live && live != pilot)
            {
                follow = live;
                break;
            }
        }

        // ⚠ Give the spectator this pilot's OWN device filter: two downed pilots watching at once
        // otherwise move in lockstep. Any translation input releases the orbit lock.
        var eye = rig.Camera.Position;   // where the death or crash camera left it (CameraController)
        var spectator = new SpectatorCamera(rig.Camera, eye,
            follow != null ? follow.WorldPosition : eye - rig.Camera.Basis.Z,
            pilot.PadDevices, pilot.UseKeyboard, pilot.LocalPlayer)
        {
            ShowReadout = false,   // the freecam's own label would sit over a splitscreen pane
            LockCandidates = lockCandidates,
        };
        worldRoot.AddChild(spectator);
        tracked?.Add(spectator);   // tracked so a rerun can hand the panes back
        if (follow != null)
        {
            spectator.FollowNode(follow);
        }

        return true;
    }

    /// <summary>Gives every watcher of <paramref name="rigs"/> its controls back while its
    /// spectator stays, for an ended race whose board reads R.</summary>
    internal static void ReleaseControls(IReadOnlyList<PlayerRig> rigs)
    {
        foreach (var rig in rigs)
        {
            if (rig.Controller is { Spectating: true } pilot)
            {
                pilot.ControlHold = FlightControlHold.None;
            }
        }
    }

    /// <summary>Undoes <see cref="Begin"/> on every pane of <paramref name="rigs"/> that took it, and
    /// frees the spectators <paramref name="tracked"/> holds: a race's new window flies every pilot
    /// again.</summary>
    internal static void HandBack(IReadOnlyList<PlayerRig> rigs, List<SpectatorCamera> tracked)
    {
        foreach (var spectator in tracked)
        {
            spectator.QueueFree();
        }

        tracked.Clear();
        ReleaseControls(rigs);
        foreach (var rig in rigs)
        {
            if (rig.Controller is not { Spectating: true } pilot)
            {
                continue;
            }

            pilot.Spectating = false;
            pilot.CameraOwned = false;
            pilot.SetPilotHudVisible(true);
            pilot.SetViewedFromOutside(false);
        }
    }
}
