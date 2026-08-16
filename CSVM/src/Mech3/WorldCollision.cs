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

    /// <summary>Re-derives every tracked collider under <paramref name="node"/>. Used by the fade
    /// channel, which changes an ancestor state Godot emits no signal for.</summary>
    public static void SyncSubtree(Node node)
    {
        if (node is Node3D n3d)
        {
            Sync(n3d);
        }
        foreach (var child in node.GetChildren())
        {
            SyncSubtree(child);
        }
    }

    // The owner's own bodies only — every other collider-bearing node is tracked in its own
    // right and gets its own signal, so a subtree is never walked twice for one toggle.
    private static void Sync(Node3D owner)
    {
        if (!owner.IsInsideTree())
        {
            return; // resolved for real when the built world joins the tree
        }
        bool enabled = owner.IsVisibleInTree() && !FadedAbove(owner);
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

    private static bool FadedAbove(Node3D owner)
    {
        if (_fadedRoots == 0)
        {
            return false;
        }
        for (Node? n = owner; n != null; n = n.GetParent())
        {
            if (n.HasMeta(FadedMeta))
            {
                return true;
            }
        }
        return false;
    }
}
