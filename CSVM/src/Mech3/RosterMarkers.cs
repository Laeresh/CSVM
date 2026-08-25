using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>The marker scaffolding a chapter authors on a vehicle it never places. A mission's
/// roster spawns its aircraft from the shared airframe record, so the chapter's own copy of that
/// vehicle stays a library root with no parent and the world build never reaches it. Everything
/// the chapter added to that copy goes with it: the approach volumes of
/// <c>Mech3/LandingApproaches.cs</c> and any other mark an index-addressed definition writes.
/// This grafts those additions onto the spawned rig, under the airframe's own mark of the same
/// name, so they move with the aircraft they belong to and carry the chapter gamez indices a
/// compiled definition's symbol table binds by.</summary>
public static class RosterMarkers
{
    /// <summary>The gamez child that holds a vehicle's authored marks (firepoints, pylons, the
    /// target point, and whatever a chapter hangs off them). Attaching is scoped to this subtree:
    /// the rest of a vehicle node is its geometry, which the spawned rig already has.</summary>
    public const string MarkersNode = "markers";

    /// <summary>Grafts <paramref name="blockName"/>'s authored marker scaffolding onto
    /// <paramref name="rig"/> and indexes it on <paramref name="runtime"/>, returning how many
    /// subtrees were added. Zero unless the chapter carries a library-root node of that name whose
    /// <c>markers</c> subtree adds something the airframe's own does not: a placed node has
    /// nothing to graft, and a copy of one would be a second vehicle. ⚠ The rig must already be in
    /// the tree, since indexing re-applies RESET_STATE.</summary>
    public static int Attach(
        GameZ gamez, SceneBuilder scene, AnimRuntime runtime, string blockName, Node3D rig)
    {
        if (gamez.FindByName(blockName) is not { } vehicle || !gamez.IsLibraryRoot(vehicle))
        {
            return 0;
        }

        var built = MarksOf(rig);
        if (ChildNamed(gamez, vehicle, MarkersNode) is not { } authored
            || !built.TryGetValue(authored.Name, out var host))
        {
            return 0;
        }

        int added = 0;
        var pending = new Stack<(GameZNode Authored, Node3D Host)>();
        pending.Push((authored, host));
        while (pending.Count > 0)
        {
            var (node, into) = pending.Pop();
            foreach (int childIndex in node.Children)
            {
                if (childIndex < 0 || childIndex >= gamez.Nodes.Count)
                {
                    continue;
                }

                var child = gamez.Nodes[childIndex];
                // A mark the airframe already carries is the same mark, so descend into the rig's
                // own node rather than growing a second one beside it. Marker names are unique
                // within a plane, which is what lets one flat map stand in for the walk.
                if (built.TryGetValue(child.Name, out var mine))
                {
                    pending.Push((child, mine));
                    continue;
                }

                if (scene.BuildSubtree(child) is not { } grown)
                {
                    continue;
                }

                ApplyAuthoredActive(gamez, grown);
                into.AddChild(grown);
                runtime.IndexStage(grown);
                added++;
            }
        }

        return added;
    }

    // The rig's own marks by their gamez name. The plane model is built by the same SceneBuilder
    // code the world is, so every node carries AnimRuntime.NameMeta; Godot's own Name is sanitized
    // and de-duplicated and cannot be matched against the chapter's names.
    private static Dictionary<string, Node3D> MarksOf(Node3D rig)
    {
        var map = new Dictionary<string, Node3D>(System.StringComparer.OrdinalIgnoreCase);
        void Walk(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta))
                {
                    map.TryAdd(n3d.GetMeta(AnimRuntime.NameMeta).AsString(), n3d);
                }

                Walk(child);
            }
        }

        Walk(rig);
        return map;
    }

    // ⚠ The authored `active` bit has to be applied here. The world walk honours it by refusing to
    // build the node at all (WorldBuilder.Add), which is not open to us: a definition addresses
    // these by index and an index with no node behind it is dropped rather than name-matched. So
    // the node is built and switched off instead, which is the state the original starts it in —
    // an arming volume that shipped inactive must not read as armed on the first frame.
    private static void ApplyAuthoredActive(GameZ gamez, Node3D built)
    {
        if (built.HasMeta(AnimRuntime.IndexMeta))
        {
            int index = (int)built.GetMeta(AnimRuntime.IndexMeta);
            if (index >= 0 && index < gamez.Nodes.Count)
            {
                built.Visible = gamez.Nodes[index].Active;
            }
        }

        foreach (var child in built.GetChildren())
        {
            if (child is Node3D n3d)
            {
                ApplyAuthoredActive(gamez, n3d);
            }
        }
    }

    private static GameZNode? ChildNamed(GameZ gamez, GameZNode parent, string name)
    {
        foreach (int childIndex in parent.Children)
        {
            if (childIndex >= 0 && childIndex < gamez.Nodes.Count
                && string.Equals(gamez.Nodes[childIndex].Name, name,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return gamez.Nodes[childIndex];
            }
        }

        return null;
    }
}
