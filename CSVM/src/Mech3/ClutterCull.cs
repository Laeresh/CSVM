using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Destroys the decorations a crater swallows, and counts the ones still standing in it. A
/// decoration dies outright, with no health test, no animation and no model swap, because the
/// original's crater path reads no template field at all (docs/org/craters.md, "What a crater
/// destroys"). <see cref="ClutterBuilder"/> bakes every placement of one kind into one MultiMesh, so
/// dying here means the instance's basis collapses to zero and its shared collision shape is
/// switched off on the region body it was attached to by RID.
/// ⚠ One MultiMesh holds a kind's every placement across the whole map, so a cull that read them
/// all cost a physics tick per crater (47 ms in a campaign fight). The builder hands each
/// MultiMesh's placements to <see cref="Index"/>, which bins their origins, and a cull reads only
/// the bins a crater's footprint can reach. A MultiMesh nobody indexed is still scanned whole.
/// </summary>
internal static class ClutterCull
{
    // Bin edge in metres. A crater's footprint is a 20 m radius, so a cull touches four bins at
    // most, and a bin holds a few dozen placements of one kind at the densest.
    private const float BinM = 64f;

    private static readonly ConditionalWeakTable<MultiMesh, Bins> s_bins = new();

    /// <summary>How many live decorations stand inside <paramref name="shape"/>. The census a
    /// crater's destruction is measured by, before and after.</summary>
    internal static int Within(Node? root, in CraterShape shape) => Walk(root, shape, destroy: false);

    /// <summary>Destroys every live decoration inside <paramref name="shape"/> and returns how many
    /// there were. Idempotent: a collapsed instance is not counted again.</summary>
    internal static int Destroy(Node? root, in CraterShape shape) => Walk(root, shape, destroy: true);

    /// <summary>Bins one MultiMesh's placements by origin, in the MultiMesh's own space, so a cull
    /// reads only the instances near a crater. Called by the builder as the MultiMesh is filled.</summary>
    internal static void Index(MultiMesh mm, IReadOnlyList<Transform3D> placements)
    {
        var bins = new Bins();
        for (int i = 0; i < placements.Count; i++)
        {
            bins.Add(placements[i].Origin, i);
        }
        s_bins.AddOrUpdate(mm, bins);
    }

    private static int Walk(Node? node, in CraterShape shape, bool destroy)
    {
        if (node == null)
        {
            return 0;
        }
        int found = 0;
        // By index rather than GetChildren(): one crater walks the whole clutter tree (PERF-20).
        for (int i = 0, count = node.GetChildCount(); i < count; i++)
        {
            found += node.GetChild(i) switch
            {
                MultiMeshInstance3D mmi => Cards(mmi, shape, destroy),
                StaticBody3D body when destroy => Bodies(body, shape),
                Node3D nested => Walk(nested, shape, destroy),
                _ => 0,
            };
        }
        return found;
    }

    // One kind's MultiMesh: the bins the footprint reaches when the builder indexed it, the whole
    // buffer otherwise.
    private static int Cards(MultiMeshInstance3D mmi, in CraterShape shape, bool destroy)
    {
        if (mmi.Multimesh is not { } mm)
        {
            return 0;
        }
        var toWorld = mmi.GlobalTransform;
        int found = 0;
        if (s_bins.TryGetValue(mm, out var bins))
        {
            var local = toWorld.AffineInverse() * shape.Impact;
            float r = shape.Radius;
            int x0 = Mathf.FloorToInt((local.X - r) / BinM), x1 = Mathf.FloorToInt((local.X + r) / BinM);
            int z0 = Mathf.FloorToInt((local.Z - r) / BinM), z1 = Mathf.FloorToInt((local.Z + r) / BinM);
            for (int bx = x0; bx <= x1; bx++)
            {
                for (int bz = z0; bz <= z1; bz++)
                {
                    if (bins.TryGet(bx, bz, out var indices))
                    {
                        foreach (int i in indices)
                        {
                            found += Card(mm, toWorld, i, shape, destroy);
                        }
                    }
                }
            }
            return found;
        }

        if (!Reaches(toWorld * mmi.GetAabb(), shape))
        {
            return 0;
        }
        for (int i = 0; i < mm.InstanceCount; i++)
        {
            found += Card(mm, toWorld, i, shape, destroy);
        }
        return found;
    }

    // One placement. A zero basis leaves the instance in the buffer with the AABB it always had
    // and rasterises nothing, which is what keeps the draw call and its custom data intact.
    private static int Card(MultiMesh mm, in Transform3D toWorld, int i, in CraterShape shape, bool destroy)
    {
        var placed = mm.GetInstanceTransform(i);
        if (placed.Basis.Determinant() == 0f || !shape.Covers(toWorld * placed.Origin))
        {
            return 0;
        }
        if (destroy)
        {
            mm.SetInstanceTransform(i, new Transform3D(placed.Basis.Scaled(Vector3.Zero), placed.Origin));
        }
        return 1;
    }

    // Whether a world-space box overlaps the crater's footprint circle at all, read on the box's
    // xz extent against the circle's bounding square, which over-includes a corner and never
    // excludes a placement the circle covers.
    private static bool Reaches(Aabb box, in CraterShape shape)
    {
        var impact = shape.Impact;
        float r = shape.Radius;
        var end = box.End;
        return box.Position.X <= impact.X + r && end.X >= impact.X - r
            && box.Position.Z <= impact.Z + r && end.Z >= impact.Z - r;
    }

    // The 3D decorations' collision, which is shared shapes attached to a region body by RID. ⚠ Do
    // not call a ShapeOwner* method here; the node does not know about RID-attached shapes and would
    // clear the body. Disabling the slot is the one edit that leaves the other placements standing.
    private static int Bodies(StaticBody3D body, in CraterShape shape)
    {
        // The region's cell first: a cell holds its placements' origins, which is exactly what
        // Covers tests, so a cell the footprint misses has nothing to read.
        if (body.HasMeta(ClutterBuilder.CollisionCellMeta))
        {
            var cell = body.GetMeta(ClutterBuilder.CollisionCellMeta).AsRect2();
            var impact = shape.Impact;
            float r = shape.Radius;
            if (cell.Position.X > impact.X + r || cell.End.X < impact.X - r
                || cell.Position.Y > impact.Z + r || cell.End.Y < impact.Z - r)
            {
                return 0;
            }
        }
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

    // Instance indices by the bin their origin falls in.
    private sealed class Bins
    {
        private readonly Dictionary<(int, int), List<int>> _cells = new();

        public void Add(Vector3 origin, int index)
        {
            var key = (Mathf.FloorToInt(origin.X / BinM), Mathf.FloorToInt(origin.Z / BinM));
            if (!_cells.TryGetValue(key, out var list))
            {
                _cells[key] = list = new List<int>();
            }
            list.Add(index);
        }

        public bool TryGet(int bx, int bz, out List<int> indices) => _cells.TryGetValue((bx, bz), out indices!);
    }
}
