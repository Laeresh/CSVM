using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// What a moving thing LOOKS like between two simulation steps: the render half of a fixed-tick
/// simulation, shared by every subsystem that moves something visible. Roles, wiring and the two
/// consumption shapes are in this module's docs/architecture.md entry.
/// ⚠ Realtime clocks only. Every other mode steps the simulation once per rendered frame, so
/// <see cref="Fraction"/> is 1 and every path here is an identity rewrite: fixed-step captures and
/// goldens stay byte-identical, and the animation debugger keeps its own smoothing.
/// ⚠ <see cref="Draw"/> has exactly ONE caller, the session's frame callback. A suite that steps
/// the simulation by hand never draws, so it always reads the true simulation pose.
/// </summary>
public static class RenderPoses
{
    private static readonly Dictionary<Node3D, Pose> Book = new();
    private static readonly List<Node3D> Expired = new();

    private static ulong _tick = ulong.MaxValue;

    /// <summary>True while the simulation runs on the physics tick and the display runs on its own
    /// clock, which is the only condition under which a drawn pose and a simulation pose differ.
    /// A halt and a mission-ending hold both leave it false, so a frozen picture cannot wobble
    /// between two stale poses.</summary>
    public static bool Active =>
        GameClock.Current is { Mode: GameClock.RunMode.Realtime, Halted: false, SimHeld: false };

    /// <summary>Where this rendered frame sits between the last two simulation steps, 0 to 1.
    /// Always 1 outside a live realtime session, which makes every consumer an identity
    /// rewrite there.</summary>
    public static float Fraction =>
        Active ? Mathf.Clamp((float)Engine.GetPhysicsInterpolationFraction(), 0f, 1f) : 1f;

    /// <summary>Live entries, for a suite asserting the book's membership rather than a pose it
    /// produced.</summary>
    public static int Count => Book.Count;

    /// <summary>Takes the pose <paramref name="node"/> was just given as this tick's simulation
    /// pose. Call it immediately after writing the node, in whichever coordinates the writer used;
    /// the local transform is what is stored, so a node whose parent also moves is carried by that
    /// parent as before.</summary>
    public static void Record(Node3D node)
    {
        if (!Active || !GodotObject.IsInstanceValid(node))
            return;
        var now = node.Transform;
        if (Book.TryGetValue(node, out var pose))
        {
            pose.Curr = now;
            pose.Fresh = true;
            return;
        }
        Book[node] = new Pose { Prev = now, Curr = now, Fresh = true };
    }

    /// <summary>Puts every recorded node back on its exact simulation pose, and rolls the pose pair
    /// once per physics tick. Call it FIRST in every physics callback that steps the simulation:
    /// the steps about to run, and anything they seed from a node's live transform, must never
    /// observe a drawn pose, or the interpolation feeds back into the simulation.
    /// Idempotent within one tick, so several callbacks may each open with it.</summary>
    public static void Restore() => Restore(Engine.GetPhysicsFrames());

    /// <summary>Draws every recorded node between its last two simulation poses. A node nothing
    /// rewrote over the last tick has stopped moving, so it is put back on its final simulation
    /// pose and dropped rather than interpolated towards a stale one.</summary>
    public static void Draw() => Draw(Fraction, Active);

    /// <summary>Drops one node, for a subsystem taking its pose back under its own control.</summary>
    public static void Forget(Node3D node) => Book.Remove(node);

    /// <summary>Empties the book. The session teardown calls it beside clearing the clock, so a
    /// later session in the same process cannot inherit a freed world's nodes.</summary>
    public static void Clear()
    {
        Book.Clear();
        Expired.Clear();
        _tick = ulong.MaxValue;
    }

    /// <summary>The restore, against an explicit tick number. The tick is what decides whether the
    /// call opens a new step or is the second callback inside one already open, so it is a
    /// parameter rather than a reading of engine state a caller cannot choose.</summary>
    internal static void Restore(ulong tick)
    {
        if (Book.Count == 0)
            return;
        bool rolled = tick != _tick;
        _tick = tick;
        foreach (var (node, pose) in Book)
        {
            if (!GodotObject.IsInstanceValid(node))
            {
                Expired.Add(node);
                continue;
            }
            if (rolled)
            {
                pose.Prev = pose.Curr;
                pose.Fresh = false;
            }
            node.Transform = pose.Curr;
        }
        Sweep();
    }

    /// <summary>The draw, against an explicit fraction. The engine's own fraction is whatever the
    /// frame happens to sit at, including zero on a headless host, so the rule is stated against a
    /// fraction the caller chooses and the parameterless form supplies the frame's.</summary>
    internal static void Draw(float fraction, bool active)
    {
        if (Book.Count == 0)
            return;
        foreach (var (node, pose) in Book)
        {
            if (!GodotObject.IsInstanceValid(node))
            {
                Expired.Add(node);
                continue;
            }
            if (!active || !pose.Fresh || fraction >= 1f)
            {
                // ⚠ The whole-step case takes the simulation pose itself, never an interpolation
                // to 1: a slerp that lands on its endpoint is not bit-identical to that endpoint,
                // and a scripted capture must reproduce the simulation pose exactly.
                node.Transform = pose.Curr;
                if (!active || !pose.Fresh)
                    Expired.Add(node);
                continue;
            }
            node.Transform = pose.Prev.InterpolateWith(pose.Curr, fraction);
        }
        Sweep();
    }

    private static void Sweep()
    {
        if (Expired.Count == 0)
            return;
        foreach (var node in Expired)
            Book.Remove(node);
        Expired.Clear();
    }

    // A reference type, so Restore and Draw mutate the pair in place: rewriting a dictionary
    // entry from inside its own enumeration is legal on this runtime but reads as a hazard.
    private sealed class Pose
    {
        public Transform3D Prev;
        public Transform3D Curr;
        public bool Fresh;
    }
}
