using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Destroys the decorations a crater swallows, and counts the ones still standing in it. A
/// decoration dies outright, with no health test, no animation and no model swap, because the
/// original's crater path reads no template field at all (docs/org/craters.md, "What a crater
/// destroys"). <see cref="ClutterBuilder"/> bakes every placement of one kind into one MultiMesh, so
/// dying here means the instance's basis collapses to zero and its shared collision shape is
/// switched off on the region body it was attached to by RID.
/// </summary>
internal static class ClutterCull
{
    /// <summary>How many live decorations stand inside <paramref name="shape"/>. The census a
    /// crater's destruction is measured by, before and after.</summary>
    internal static int Within(Node? root, in CraterShape shape) => Walk(root, shape, destroy: false);

    /// <summary>Destroys every live decoration inside <paramref name="shape"/> and returns how many
    /// there were. Idempotent: a collapsed instance is not counted again.</summary>
    internal static int Destroy(Node? root, in CraterShape shape) => Walk(root, shape, destroy: true);

    private static int Walk(Node? node, in CraterShape shape, bool destroy)
    {
        if (node == null)
        {
            return 0;
        }
        int found = 0;
        foreach (var child in node.GetChildren())
        {
            found += child switch
            {
                MultiMeshInstance3D mmi => Cards(mmi, shape, destroy),
                StaticBody3D body when destroy => Bodies(body, shape),
                Node3D nested => Walk(nested, shape, destroy),
                _ => 0,
            };
        }
        return found;
    }

    // One kind's MultiMesh. A zero basis leaves the instance in the buffer with the AABB it always
    // had and rasterises nothing, which is what keeps the draw call and its custom data intact.
    private static int Cards(MultiMeshInstance3D mmi, in CraterShape shape, bool destroy)
    {
        if (mmi.Multimesh is not { } mm)
        {
            return 0;
        }
        var toWorld = mmi.GlobalTransform;
        int found = 0;
        for (int i = 0; i < mm.InstanceCount; i++)
        {
            var placed = mm.GetInstanceTransform(i);
            if (placed.Basis.Determinant() == 0f || !shape.Covers(toWorld * placed.Origin))
            {
                continue;
            }
            found++;
            if (destroy)
            {
                mm.SetInstanceTransform(i, new Transform3D(placed.Basis.Scaled(Vector3.Zero), placed.Origin));
            }
        }
        return found;
    }

    // The 3D decorations' collision, which is shared shapes attached to a region body by RID. ⚠ Do
    // not call a ShapeOwner* method here; the node does not know about RID-attached shapes and would
    // clear the body. Disabling the slot is the one edit that leaves the other placements standing.
    private static int Bodies(StaticBody3D body, in CraterShape shape)
    {
        var rid = body.GetRid();
        var toWorld = body.GlobalTransform;
        int count = PhysicsServer3D.BodyGetShapeCount(rid);
        for (int i = 0; i < count; i++)
        {
            var placed = PhysicsServer3D.BodyGetShapeTransform(rid, i);
            if (shape.Covers(toWorld * placed.Origin))
            {
                PhysicsServer3D.BodySetShapeDisabled(rid, i, true);
            }
        }
        return 0;
    }
}
