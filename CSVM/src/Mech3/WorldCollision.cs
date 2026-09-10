using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Keeps every world collider's <c>Disabled</c> flag DERIVED from its owner's state: enabled
/// exactly while the owner is visible in the scene tree and no ancestor is faded out. Callers
/// that hide something write <c>Visible</c> (or <see cref="SetFaded"/>) and nothing else.
/// ⚠ Godot visibility is INHERITED, <c>Disabled</c> is not — do not write <c>Disabled</c>
/// independently, and do not replace this with a recursive collider walk; the two must derive
/// from one source or the built world grows solid-but-invisible colliders.
/// Only colliders <see cref="SceneBuilder"/> builds are tracked; other solid bodies (plane
/// hitboxes, the empty stage's ground) are untracked and keep working.
/// </summary>
internal static class WorldCollision
{
    // Marks a subtree root faded below the collision threshold. A meta rather than a
    // static registry so it dies with the node — sessions build and free whole worlds.
    private const string FadedMeta = "csky_col_faded";

    // Live faded roots, so the common case (none) skips the ancestor walk. Only a
    // perf hint: a world freed mid-fade leaves it high, which costs a walk and nothing else.
    private static int _fadedRoots;

    // Ancestor links the fade walk has followed. The instrument behind the bound the
    // fade-walk-bound suite pins; nothing else reads it and nothing branches on it.
    private static long _walkSteps;

    /// <summary>Binds <paramref name="owner"/>'s colliders to its own visibility. Called once per
    /// collider-bearing node as it is built. Both signals are needed: <c>VisibilityChanged</c>
    /// fires on every descendant when an ancestor toggles (Godot propagates it down), and
    /// <c>TreeEntered</c> covers the build itself — a world is assembled, bootstrapped and only
    /// then added to the tree, and a detached node's visibility writes emit nothing.</summary>
    public static void Track(Node3D owner)
    {
        owner.VisibilityChanged += () => Sync(owner);
        owner.TreeEntered += () => Sync(owner);
        Sync(owner);
    }

    /// <summary>Switches the whole subtree's collision off while it is faded out (an
    /// <c>OBJECT_OPACITY_*</c> event drives opacity as a shader parameter, which visibility knows
    /// nothing about, so a fade to alpha 0 would otherwise stay solid). Returns whether the state
    /// actually changed, so the caller can log the crossing rather than every tick.</summary>
    public static bool SetFaded(Node3D root, bool faded)
    {
        if (faded == root.HasMeta(FadedMeta))
        {
            return false;
        }
        if (faded)
        {
            root.SetMeta(FadedMeta, true);
            _fadedRoots++;
        }
        else
        {
            root.RemoveMeta(FadedMeta);
            _fadedRoots--;
        }
        SyncSubtree(root);
        return true;
    }

    /// <summary>The world object one collider body stands for. <see cref="SceneBuilder"/> carves a
    /// mesh node's collision into one body PER SURFACE CLASS, so sibling bodies under one node are
    /// one object; a body it did not build (a clutter region, a plane hull, a suite's bare plate)
    /// stands for itself. Callers that must count objects rather than bodies key on this.</summary>
    public static Node OwnerOf(Node body) =>
        body.HasMeta(SceneBuilder.SurfaceIdMeta) && body.GetParent() is { } parent ? parent : body;

    /// <summary>Re-derives every tracked collider under <paramref name="node"/>. Used by the fade
    /// channel, which changes an ancestor state Godot emits no signal for.
    /// ⚠ Do not answer <see cref="FadedAbove"/> per node here; carry it down instead. What is above
    /// the subtree cannot change during the descent, so a climb per node multiplies one effect's
    /// reset by the world's depth, and any fade anywhere clears the fast path that hid it.</summary>
    public static void SyncSubtree(Node node) => SyncSubtree(node, FadedAbove(node.GetParent()));

    /// <summary>The ancestor links the fade walk has followed since this was last called, and
    /// clears the tally. The suite pinning the walk's bound is the only reader.</summary>
    internal static long TakeWalkSteps()
    {
        long steps = _walkSteps;
        _walkSteps = 0;
        return steps;
    }

    // Descends with the answer the caller already climbed for, adding each node's own mark on the
    // way down: a node is faded exactly when it or anything above it is.
    private static void SyncSubtree(Node node, bool fadedAbove)
    {
        bool faded = fadedAbove || node.HasMeta(FadedMeta);
        if (node is Node3D n3d)
        {
            Sync(n3d, faded);
        }
        foreach (var child in node.GetChildren())
        {
            SyncSubtree(child, faded);
        }
    }

    // The signal path, where nothing has climbed for this node yet.
    private static void Sync(Node3D owner) => Sync(owner, FadedAbove(owner));

    // The owner's own bodies only — every other collider-bearing node is tracked in its own
    // right and gets its own signal, so a subtree is never walked twice for one toggle.
    private static void Sync(Node3D owner, bool fadedAbove)
    {
        if (!owner.IsInsideTree())
        {
            return; // resolved for real when the built world joins the tree
        }
        bool enabled = owner.IsVisibleInTree() && !fadedAbove;
        foreach (var child in owner.GetChildren())
        {
            if (child is not StaticBody3D body)
            {
                continue;
            }
            foreach (var shape in body.GetChildren())
            {
                if (shape is CollisionShape3D collision)
                {
                    collision.Disabled = !enabled;
                }
            }
        }
    }

    private static bool FadedAbove(Node? owner)
    {
        if (_fadedRoots == 0)
        {
            return false;
        }
        for (Node? n = owner; n != null; n = n.GetParent())
        {
            _walkSteps++;
            if (n.HasMeta(FadedMeta))
            {
                return true;
            }
        }
        return false;
    }
}
