namespace CSVM.Flight;

using System.Collections.Generic;
using Godot;

/// <summary>Every pane's camera, session-owned and bound once — the pattern
/// <see cref="ScreenSize.NearestFloor"/>/`ProjectilePool.Viewers` proved 2026-08-10
/// (docs/org/tracers.md), promoted so every draw rule that needs "what do the cameras see" shares
/// one registration instead of re-deriving it from `_rigs`. <c>GameSession</c> binds it once, right
/// after the rigs are built (single player: one entry wrapping the main camera, same as every other
/// rig loop); nothing here re-queries `_rigs` itself.
///
/// <para>Two read shapes, one per named consumer, and no more: <see cref="Positions"/> (B13's
/// world-light budgeting — position only) and <see cref="Poses"/> (B11's puffer fade — needs each
/// camera's forward too, for a view-space depth comparison a Euclidean distance can't make). Both
/// skip a freed camera rather than throw. What "nearest" MEANS — screen-space pixel floor, view-space
/// depth, or plain Euclidean range — stays the consumer's own arithmetic (`ScreenSize`'s entry in
/// docs/architecture.md); this class only carries cameras, not that math.</para>
///
/// <para>⚠ Not `PlayerPositions` (`GameSession`'s gameplay seam, fed to `WorldSession.Options`):
/// that one answers "where are the humans" off each rig's `Controller`/camera fallback for
/// proximity gameplay rules; this one answers "what do the cameras see" for draw rules. Keep them
/// separate — PLAN-splitscreen-polish.md A3's own trap.</para></summary>
public sealed class ViewerSet
{
    private readonly List<Camera3D> _cameras = new();

    /// <summary>The raw bound cameras, unfiltered — for a consumer (the tracer floor) that needs
    /// each viewer's own FOV and pane height alongside its position, not just the position, and
    /// that already skips a freed instance itself while building its per-viewer sample.</summary>
    public IReadOnlyList<Camera3D> Cameras => _cameras;

    /// <summary>Replaces the bound set. Called once per rig build (session start; splitscreen panes
    /// are not rebuilt on respawn) — never incrementally.</summary>
    public void Bind(IEnumerable<Camera3D> cameras)
    {
        _cameras.Clear();
        _cameras.AddRange(cameras);
    }

    /// <summary>Every live viewer's position only — B13's nearest-rig query, where forward is
    /// irrelevant (a world light is budgeted by range, not by which way a camera is looking).</summary>
    public List<Vector3> Positions()
    {
        var result = new List<Vector3>(_cameras.Count);
        foreach (var cam in _cameras)
        {
            if (cam == null || !GodotObject.IsInstanceValid(cam))
                continue;
            result.Add(cam.GlobalPosition);
        }
        return result;
    }

    /// <summary>Every live viewer's position and forward — B11's per-particle view-space depth
    /// comparison needs both; a Euclidean nearest can pick the wrong pane for a fade that is
    /// authored along the camera's own forward axis, not straight-line range.</summary>
    public List<ViewerPose> Poses()
    {
        var result = new List<ViewerPose>(_cameras.Count);
        foreach (var cam in _cameras)
        {
            if (cam == null || !GodotObject.IsInstanceValid(cam))
                continue;
            result.Add(new ViewerPose(cam.GlobalPosition, -cam.GlobalTransform.Basis.Z));
        }
        return result;
    }

    /// <summary>One viewer's world position and forward direction (`-Z`, the engine's camera-local
    /// forward) — B11's view-space depth query.</summary>
    public readonly record struct ViewerPose(Vector3 Position, Vector3 Forward);
}
