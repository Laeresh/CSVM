using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// One clutter kind's placements as drawn. The faithful path draws the kind's single MultiMesh;
/// Enhanced Graphics cuts it into square map cells, one node each. A cell carries its own bounds
/// and a visibility range at its farthest fade, so a pane draws only the cells near it
/// (docs/architecture/Mech3.md). Indices are the kind's placement list, read and written in the
/// clutter root's frame, so <see cref="ClutterActivation"/> never sees the cells or a
/// <see cref="Recut"/>.
/// ⚠ Keep the faithful path on one whole MultiMesh. Cells blend a kind's cards in another order,
/// which the pinned goldens would read as a moved pixel.
/// </summary>
public sealed class ClutterInstances
{
    // TUNE: a cell's edge as a multiple of the kind's farthest fade, clamped to the metres below.
    // A pane in range of a kind then reaches a handful of its cells rather than the whole map.
    private const float CellPerFar = 2f;
    private const float MinCellM = 512f;
    private const float MaxCellM = 4096f;

    // The whole node as the builder made it, kept so a recut can make it again. Null for an
    // instance a suite made from a bare MultiMesh, which has nothing to recut.
    private readonly Prototype? _proto;

    private MultiMesh[] _cells;
    private Vector3[] _offsets;
    private int[]? _cellOf;
    private int[]? _slotOf;

    // The node under the clutter root: the whole MultiMesh, or the group of cells.
    private Node3D? _node;

    private ClutterInstances(Prototype? proto, MultiMesh[] cells, Vector3[] offsets, int[]? cellOf,
        int[]? slotOf, int count)
    {
        _proto = proto;
        _cells = cells;
        _offsets = offsets;
        _cellOf = cellOf;
        _slotOf = slotOf;
        InstanceCount = count;
    }

    /// <summary>The kind's placement count, which is every index this reads and writes.</summary>
    public int InstanceCount { get; }

    /// <summary>How many nodes draw the kind: 1 for the whole MultiMesh.</summary>
    public int CellCount => _cells.Length;

    /// <summary>The kind drawn by its one MultiMesh, as the faithful path draws it.</summary>
    public static ClutterInstances Whole(MultiMesh mm) =>
        new(null, new[] { mm }, new[] { Vector3.Zero }, null, null, mm.InstanceCount);

    /// <summary>Puts <paramref name="whole"/> under <paramref name="root"/> as the standing mode
    /// draws it. The faithful path keeps it whole; Enhanced cuts it into cells at the registered fade
    /// scale (<see cref="EffectsLevel.RegisteredScaleSq"/>). <paramref name="placements"/> are the
    /// authored stamps the MultiMesh holds.</summary>
    public static ClutterInstances Draw(Node3D root, MultiMeshInstance3D whole,
        IReadOnlyList<Transform3D> placements, IReadOnlyList<Color> fades)
    {
        var proto = new Prototype(root, whole.Name, whole.Multimesh!.Mesh, whole.MaterialOverride,
            whole.CastShadow, whole.ExtraCullMargin, whole.GetInstanceShaderParameter("node_bias"),
            placements, fades);
        var drawn = new ClutterInstances(proto, System.Array.Empty<MultiMesh>(), System.Array.Empty<Vector3>(),
            null, null, placements.Count);
        drawn.Place(whole);
        return drawn;
    }

    /// <summary>Replaces <paramref name="whole"/> with one node per occupied cell under a group
    /// named as it was, and returns the group and the index map. Each cell copies the whole's
    /// material, cull margin, shadow setting and <c>node_bias</c>. <paramref name="fadeScaleSq"/> is
    /// the fade shader's distance scale. At 0 nothing ever fades, and no cell carries a
    /// range.</summary>
    public static (Node3D Group, ClutterInstances Instances) Cells(MultiMeshInstance3D whole,
        IReadOnlyList<Transform3D> placements, IReadOnlyList<Color> fades, float fadeScaleSq)
    {
        var cut = Cut(whole, placements, fades, fadeScaleSq);
        return (cut.Group, new ClutterInstances(null, cut.Cells, cut.Offsets, cut.CellOf, cut.SlotOf,
            placements.Count));
    }

    /// <summary>Draws the kind again under the mode and the fade scale standing now, whole or in
    /// cells, as a fresh build draws it. A stamp the world has since hidden or a crater has
    /// flattened keeps its transform.</summary>
    public void Recut()
    {
        if (_proto == null || _node == null)
            return;
        var current = new Transform3D[InstanceCount];
        for (int i = 0; i < InstanceCount; i++)
            current[i] = GetInstanceTransform(i);
        // The new node takes the old one's place among the root's children. Draw order between
        // kinds that overlap follows it, so a recut kind draws where a fresh build puts it.
        int at = _node.GetIndex();
        _node.GetParent()?.RemoveChild(_node);
        _node.QueueFree();
        _node = null;
        Place(_proto.Build());
        _proto.Root.MoveChild(_node!, at);
        for (int i = 0; i < InstanceCount; i++)
        {
            if (current[i] != _proto.Placements[i])
                SetInstanceTransform(i, current[i]);
        }
    }

    /// <summary>Placement <paramref name="i"/>'s transform in the clutter root's frame.</summary>
    public Transform3D GetInstanceTransform(int i)
    {
        int c = _cellOf?[i] ?? 0;
        var local = _cells[c].GetInstanceTransform(_slotOf?[i] ?? i);
        return new Transform3D(local.Basis, local.Origin + _offsets[c]);
    }

    /// <summary>Writes placement <paramref name="i"/>'s transform, given in the clutter root's
    /// frame.</summary>
    public void SetInstanceTransform(int i, Transform3D placed)
    {
        int c = _cellOf?[i] ?? 0;
        _cells[c].SetInstanceTransform(_slotOf?[i] ?? i, new Transform3D(placed.Basis, placed.Origin - _offsets[c]));
    }

    private static CutResult Cut(MultiMeshInstance3D whole, IReadOnlyList<Transform3D> placements,
        IReadOnlyList<Color> fades, float fadeScaleSq)
    {
        var mesh = whole.Multimesh!.Mesh;
        var meshBox = mesh.GetAabb();
        float kindFar2 = 0f;
        foreach (var f in fades)
        {
            kindFar2 = Mathf.Max(kindFar2, f.G);
        }
        float cellM = CellFor(fadeScaleSq > 0f ? Mathf.Sqrt(kindFar2 / fadeScaleSq) : 0f);
        var byCell = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < placements.Count; i++)
        {
            var o = placements[i].Origin;
            var key = (Mathf.FloorToInt(o.X / cellM), Mathf.FloorToInt(o.Z / cellM));
            if (!byCell.TryGetValue(key, out var list))
            {
                byCell[key] = list = new List<int>();
            }
            list.Add(i);
        }

        var group = new Node3D { Name = whole.Name };
        var cells = new MultiMesh[byCell.Count];
        var offsets = new Vector3[byCell.Count];
        var cellOf = new int[placements.Count];
        var slotOf = new int[placements.Count];
        var nodeBias = whole.GetInstanceShaderParameter("node_bias");
        int c = 0;
        foreach (var (_, members) in byCell)
        {
            Aabb? bounds = null;
            float far2 = 0f;
            bool neverFades = fadeScaleSq <= 0f;
            foreach (int i in members)
            {
                var box = placements[i] * meshBox;
                bounds = bounds?.Merge(box) ?? box;
                far2 = Mathf.Max(far2, fades[i].G);
                neverFades |= fades[i].G <= 0f;
            }

            var offset = bounds!.Value.GetCenter();
            var local = new List<Transform3D>(members.Count);
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = members.Count,
            };
            for (int s = 0; s < members.Count; s++)
            {
                int i = members[s];
                var placed = placements[i];
                var shifted = new Transform3D(placed.Basis, placed.Origin - offset);
                mm.SetInstanceTransform(s, shifted);
                mm.SetInstanceCustomData(s, fades[i]);
                local.Add(shifted);
                cellOf[i] = c;
                slotOf[i] = s;
            }
            ClutterCull.Index(mm, local);

            var cell = new MultiMeshInstance3D
            {
                Name = $"{whole.Name}_{c}",
                Multimesh = mm,
                MaterialOverride = whole.MaterialOverride,
                CastShadow = whole.CastShadow,
                ExtraCullMargin = whole.ExtraCullMargin,
                Position = offset,
            };
            if (nodeBias.VariantType != Variant.Type.Nil)
            {
                cell.SetInstanceShaderParameter("node_bias", nodeBias);
            }
            // The farthest stamp's fade, plus the cell's half diagonal and the card's swing. Past
            // that distance every stamp in the cell has already faded to nothing.
            if (!neverFades)
            {
                float radius = (bounds.Value.Size * 0.5f).Length() + whole.ExtraCullMargin;
                cell.VisibilityRangeEnd = Mathf.Sqrt(far2 / fadeScaleSq) + radius;
            }
            group.AddChild(cell);
            cells[c] = mm;
            offsets[c] = offset;
            c++;
        }

        whole.Free();
        return new CutResult(group, cells, offsets, cellOf, slotOf);
    }

    // A kind that never fades takes the largest cell, since no range cuts it anyway.
    private static float CellFor(float farM) =>
        farM <= 0f ? MaxCellM : Mathf.Clamp(farM * CellPerFar, MinCellM, MaxCellM);

    // Takes the freshly built whole node: added as it is on the faithful path, cut under Enhanced.
    private void Place(MultiMeshInstance3D whole)
    {
        var proto = _proto!;
        if (!GraphicsMode.Enhanced)
        {
            proto.Root.AddChild(whole);
            _node = whole;
            (_cells, _offsets, _cellOf, _slotOf) = (new[] { whole.Multimesh! }, new[] { Vector3.Zero }, null, null);
            return;
        }
        var cut = Cut(whole, proto.Placements, proto.Fades, EffectsLevel.RegisteredScaleSq);
        proto.Root.AddChild(cut.Group);
        _node = cut.Group;
        (_cells, _offsets, _cellOf, _slotOf) = (cut.Cells, cut.Offsets, cut.CellOf, cut.SlotOf);
    }

    private readonly record struct CutResult(Node3D Group, MultiMesh[] Cells, Vector3[] Offsets, int[] CellOf, int[] SlotOf);

    // Everything the builder's whole node carried, and the authored stamps it held.
    private sealed record Prototype(Node3D Root, StringName Name, Mesh Mesh, Material? Material,
        GeometryInstance3D.ShadowCastingSetting CastShadow, float ExtraCullMargin, Variant NodeBias,
        IReadOnlyList<Transform3D> Placements, IReadOnlyList<Color> Fades)
    {
        // The whole node exactly as the builder fills it, from the authored stamps.
        public MultiMeshInstance3D Build()
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = Mesh,
                InstanceCount = Placements.Count,
            };
            for (int i = 0; i < Placements.Count; i++)
            {
                mm.SetInstanceTransform(i, Placements[i]);
                mm.SetInstanceCustomData(i, Fades[i]);
            }
            ClutterCull.Index(mm, Placements);
            var whole = new MultiMeshInstance3D
            {
                Name = Name,
                Multimesh = mm,
                MaterialOverride = Material,
                CastShadow = CastShadow,
                ExtraCullMargin = ExtraCullMargin,
            };
            if (NodeBias.VariantType != Variant.Type.Nil)
                whole.SetInstanceShaderParameter("node_bias", NodeBias);
            return whole;
        }
    }
}
