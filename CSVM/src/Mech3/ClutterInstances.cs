using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// One clutter kind's placements as drawn. The faithful path draws the kind's single MultiMesh;
/// Enhanced Graphics cuts it into square map cells, one node each. A cell carries its own bounds
/// and a visibility range at its farthest fade, so a pane draws only the cells near it
/// (docs/architecture/Mech3.md). Indices are the kind's placement list, and
/// transforms read and write in the clutter root's frame, so <see cref="ClutterActivation"/> never
/// sees the cells.
/// ⚠ Keep the faithful path on <see cref="Whole"/>. Cells blend a kind's cards in another order,
/// which the pinned goldens would read as a moved pixel.
/// </summary>
public sealed class ClutterInstances
{
    // TUNE: a cell's edge as a multiple of the kind's farthest fade, clamped to the metres below.
    // A pane in range of a kind then reaches a handful of its cells rather than the whole map.
    private const float CellPerFar = 2f;
    private const float MinCellM = 512f;
    private const float MaxCellM = 4096f;

    private readonly MultiMesh[] _cells;
    private readonly Vector3[] _offsets;
    private readonly int[]? _cellOf;
    private readonly int[]? _slotOf;

    private ClutterInstances(MultiMesh[] cells, Vector3[] offsets, int[]? cellOf, int[]? slotOf, int count)
    {
        _cells = cells;
        _offsets = offsets;
        _cellOf = cellOf;
        _slotOf = slotOf;
        InstanceCount = count;
    }

    /// <summary>The kind's placement count, which is every index this reads and writes.</summary>
    public int InstanceCount { get; }

    /// <summary>How many nodes draw the kind: 1 for <see cref="Whole"/>.</summary>
    public int CellCount => _cells.Length;

    /// <summary>The kind drawn by its one MultiMesh, as the faithful path draws it.</summary>
    public static ClutterInstances Whole(MultiMesh mm) =>
        new(new[] { mm }, new[] { Vector3.Zero }, null, null, mm.InstanceCount);

    /// <summary>Replaces <paramref name="whole"/> with one node per occupied cell under a group
    /// named as it was, and returns the group and the index map. Each cell copies the whole's
    /// material, cull margin, shadow setting and <c>node_bias</c>. <paramref name="fadeScaleSq"/> is
    /// the fade shader's distance scale. At 0 nothing ever fades, and no cell carries a
    /// range.</summary>
    public static (Node3D Group, ClutterInstances Instances) Cells(MultiMeshInstance3D whole,
        IReadOnlyList<Transform3D> placements, IReadOnlyList<Color> fades, float fadeScaleSq)
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
        return (group, new ClutterInstances(cells, offsets, cellOf, slotOf, placements.Count));
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

    // A kind that never fades takes the largest cell, since no range cuts it anyway.
    private static float CellFor(float farM) =>
        farM <= 0f ? MaxCellM : Mathf.Clamp(farM * CellPerFar, MinCellM, MaxCellM);
}
