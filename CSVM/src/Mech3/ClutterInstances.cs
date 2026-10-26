using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// One clutter kind's placements as drawn: the faithful path's single MultiMesh, or Enhanced
/// Graphics' map cells, each ranged at its farthest fade (docs/architecture/Mech3.md). Indices are
/// the kind's placement list, read and written in the clutter root's frame, so
/// <see cref="ClutterActivation"/> never sees the cells or a <see cref="Recut"/>. Both layouts'
/// buffers are kept once made, so a recut only swaps nodes and writes the stamps moved since.
/// ⚠ Keep the faithful path on one whole MultiMesh. Cells blend a kind's cards in another order,
/// which the pinned goldens would read as a moved pixel.
/// A listed vegetation kind also carries a Rich variant, index-aligned with the authored stamps,
/// which the cells draw while <see cref="VegetationQualitySetting.RichOn"/>.
/// </summary>
public sealed class ClutterInstances
{
    // TUNE: a cell's edge as a multiple of the kind's farthest fade, clamped to the metres below.
    // A pane in range of a kind then reaches a handful of its cells rather than the whole map.
    private const float CellPerFar = 2f;
    private const float MinCellM = 512f;
    private const float MaxCellM = 4096f;

    // Floats per instance in a buffer: a 3x4 transform, the colour when the buffer has one, then the
    // custom data.
    private const int Stride = 16;
    private const int ColorStride = 20;

    // The whole node as the builder made it, kept so a recut can make it again. Null for an
    // instance a suite made from a bare MultiMesh, which has nothing to recut.
    private readonly Prototype? _proto;

    // Every placement the world has moved off its authored transform (hidden or flattened), in the
    // clutter root's frame. The drawn layout always holds it; another catches up when it is drawn.
    private readonly Dictionary<int, Transform3D> _moved = new();

    private Layout _drawn;
    private Layout? _whole;
    private Layout? _cells;
    private Layout? _richCells;

    // The Rich variant, null for a kind no vegetation entry lists.
    private Stamps? _richVariant;

    // Whether the authored stamps, and so _moved, are the Rich variant's.
    private bool _rich;

    // The near model over the Rich variant, and its cells, drawn exactly while the Rich variant is. Every
    // write to a stamp reaches them too, so a model follows its card's hide, crater and show.
    private Near? _near;
    private Prototype? _nearProto;
    private Layout? _nearCells;

    private ClutterInstances(Prototype? proto, Layout drawn, int count)
    {
        _proto = proto;
        _drawn = drawn;
        InstanceCount = count;
    }

    /// <summary>The kind's placement count, which is every index this reads and writes.</summary>
    public int InstanceCount { get; }

    /// <summary>How many nodes draw the kind: 1 for the whole MultiMesh.</summary>
    public int CellCount => _drawn.Meshes.Length;

    /// <summary>How many nodes draw the kind's near model now, 0 while none does.</summary>
    public int ModelCellCount => _nearCells?.Node?.GetChildCount() ?? 0;

    // RichOn already requires Enhanced, so the whole layout is always the Original variant.
    private bool RichWanted => _richVariant != null && VegetationQualitySetting.RichOn;

    private IReadOnlyList<Transform3D> AuthoredList => _rich ? _richVariant!.Placements : _proto!.Placements;

    /// <summary>The kind drawn by its one MultiMesh, as the faithful path draws it.</summary>
    public static ClutterInstances Whole(MultiMesh mm) =>
        new(null, Layout.Single(mm, null), mm.InstanceCount);

    /// <summary>Puts <paramref name="whole"/> under <paramref name="root"/> as the standing mode
    /// draws it. The faithful path keeps it whole, and Enhanced cuts it into cells at
    /// <see cref="EffectsLevel.RegisteredScaleSq"/>. The MultiMesh holds the authored
    /// <paramref name="placements"/>, <paramref name="rich"/> is their Rich variant, and
    /// <paramref name="near"/> the model drawn over it near the camera.</summary>
    public static ClutterInstances Draw(Node3D root, MultiMeshInstance3D whole,
        IReadOnlyList<Transform3D> placements, IReadOnlyList<Color> fades, Stamps? rich = null, Near? near = null)
    {
        var proto = new Prototype(root, whole.Name, whole.Multimesh!.Mesh, whole.MaterialOverride,
            whole.CastShadow, whole.ExtraCullMargin, whole.GetInstanceShaderParameter("node_bias"),
            placements, fades);
        var single = Layout.Single(whole.Multimesh!, whole.MaterialOverride);
        var drawn = new ClutterInstances(proto, single, placements.Count) { _whole = single, _richVariant = rich, _near = near };
        drawn.Own(single);
        if (GraphicsMode.Enhanced)
        {
            drawn._rich = drawn.RichWanted;
            var cells = drawn.CellsAt(EffectsLevel.RegisteredScaleSq, nodes: true);
            whole.Free();
            drawn.Show(cells, -1);
            drawn.FollowNear();
        }
        else
        {
            single.Node = whole;
            root.AddChild(whole);
        }
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
        var proto = new Prototype(null, whole.Name, whole.Multimesh!.Mesh, whole.MaterialOverride,
            whole.CastShadow, whole.ExtraCullMargin, whole.GetInstanceShaderParameter("node_bias"),
            placements, fades);
        var layout = Layout.Cut(proto, proto.Original, CellFor(KindFarM(fades, fadeScaleSq)), fadeScaleSq);
        var instances = new ClutterInstances(null, layout, placements.Count);
        instances.Own(layout);
        whole.Free();
        return (layout.Attach(proto), instances);
    }

    /// <summary>The visibility range of a node whose stamps lie within <paramref name="radius"/> of its
    /// bounds' centre. Past it every stamp has faded, the farthest at squared fade
    /// <paramref name="far2"/>. It is 0, no range, on the faithful path or where a stamp never
    /// fades.</summary>
    public static float RangeEnd(bool enhanced, bool neverFades, float far2, float radius, float fadeScaleSq) =>
        enhanced && !neverFades && fadeScaleSq > 0f ? Mathf.Sqrt(far2 / fadeScaleSq) + radius : 0f;

    /// <summary>Draws the kind under the mode and the fade scale standing now, whole or in cells, as
    /// a fresh build draws it. A layout made before is drawn again from its kept buffers, and only
    /// the stamps the world has moved since are written. A stamp the world has hidden or a crater has
    /// flattened keeps its transform. The Rich variant is drawn exactly while
    /// <see cref="VegetationQualitySetting.RichOn"/> and the kind has one.</summary>
    public void Recut()
    {
        if (_proto == null || _drawn.Node == null)
            return;
        // The variant is part of the layout: a live Vegetation Quality move is a new layout too.
        bool rich = RichWanted;
        if (rich != _rich)
            MoveTo(rich ? _richVariant!.Placements : _proto.Placements, rich);
        var want = GraphicsMode.Enhanced ? CellsAt(EffectsLevel.RegisteredScaleSq) : _whole!;
        if (ReferenceEquals(want, _drawn))
        {
            if (want.IsCut)
                want.Range(EffectsLevel.RegisteredScaleSq);
        }
        else
        {
            // The new node takes the old one's place among the root's children. Draw order between
            // kinds that overlap follows it, so a recut kind draws where a fresh build puts it.
            Show(want, _drawn.Node.GetIndex());
        }
        FollowNear();
    }

    /// <summary>Replaces the Rich variant, null for none, after the vegetation entry changed. Its
    /// cells are cut again at the next <see cref="Recut"/>, which the caller runs.</summary>
    public void SetRich(Stamps? rich)
    {
        if (_proto == null)
            return;
        bool drawnRich = _rich && rich != null;
        MoveTo(drawnRich ? rich!.Placements : _proto.Placements, drawnRich);
        _richVariant = rich;
        if (_richCells != null && !ReferenceEquals(_richCells, _drawn))
            _richCells.Release();
        _richCells = null;
        _nearCells?.Release();
        _nearCells = null;
    }

    /// <summary>Placement <paramref name="i"/>'s authored transform in the variant drawn, the one a
    /// stamp the world shows again returns to.</summary>
    public Transform3D Authored(int i) => _proto == null ? GetInstanceTransform(i) : AuthoredList[i];

    /// <summary>Placement <paramref name="i"/>'s transform in the clutter root's frame.</summary>
    public Transform3D GetInstanceTransform(int i)
    {
        if (_proto != null)
            return _moved.TryGetValue(i, out var moved) ? moved : AuthoredList[i];
        int c = _drawn.CellOf?[i] ?? 0;
        var local = _drawn.Meshes[c].GetInstanceTransform(_drawn.SlotOf?[i] ?? i);
        return new Transform3D(local.Basis, local.Origin + _drawn.Offsets[c]);
    }

    /// <summary>Whether the world has hidden or flattened placement <paramref name="i"/>. A Rich stamp the
    /// vegetation mask dropped draws nothing too, and is not moved until the world moves it.</summary>
    public bool Moved(int i) => _proto != null ? _moved.ContainsKey(i) : GetInstanceTransform(i).Basis.Determinant() == 0f;

    /// <summary>Puts placement <paramref name="i"/> back on its authored transform, the world showing it again.</summary>
    public void Restore(int i)
    {
        _moved.Remove(i);
        _drawn.Write(i, Authored(i));
        if (_nearCells?.Node != null)
            _nearCells.Write(i, Authored(i));
    }

    /// <summary>Writes placement <paramref name="i"/>'s transform, given in the clutter root's
    /// frame. A zero basis is always a move, even onto a stamp the mask dropped.</summary>
    public void SetInstanceTransform(int i, Transform3D placed)
    {
        if (_proto != null)
        {
            if (placed == AuthoredList[i] && placed.Basis.Determinant() != 0f)
                _moved.Remove(i);
            else
                _moved[i] = placed;
        }
        _drawn.Write(i, placed);
        if (_nearCells?.Node != null)
            _nearCells.Write(i, placed);
    }

    // Placement i in the frame of the drawn MultiMesh that holds it, exactly as its buffer holds it.
    internal Transform3D LocalTransform(int i)
    {
        int c = _drawn.CellOf?[i] ?? 0;
        if (_proto == null)
            return _drawn.Meshes[c].GetInstanceTransform(_drawn.SlotOf?[i] ?? i);
        var placed = GetInstanceTransform(i);
        return new Transform3D(placed.Basis, placed.Origin - _drawn.Offsets[c]);
    }

    // A kind that never fades takes the largest cell, since no range cuts it anyway.
    private static float CellFor(float farM) =>
        farM <= 0f ? MaxCellM : Mathf.Clamp(farM * CellPerFar, MinCellM, MaxCellM);

    // The kind's farthest fade in metres at the scale, 0 where nothing fades.
    private static float KindFarM(IReadOnlyList<Color> fades, float fadeScaleSq)
    {
        float kindFar2 = 0f;
        foreach (var f in fades)
            kindFar2 = Mathf.Max(kindFar2, f.G);
        return fadeScaleSq > 0f ? Mathf.Sqrt(kindFar2 / fadeScaleSq) : 0f;
    }

    // Carries every moved stamp across to the other variant. A hidden or flattened stamp is a zero
    // basis at its authored origin, so it becomes one at the other's. ⚠ Never invert the authored
    // transform: a stamp the mask dropped has a zero basis there.
    private void MoveTo(IReadOnlyList<Transform3D> to, bool rich)
    {
        foreach (int i in new List<int>(_moved.Keys))
            _moved[i] = new Transform3D(to[i].Basis.Scaled(Vector3.Zero), to[i].Origin);
        _rich = rich;
    }

    // The cells layout of the variant _rich names at the scale: the kept one, ranged again, when its
    // cell edge still holds. A new cut makes its nodes with its buffers when asked.
    private Layout CellsAt(float fadeScaleSq, bool nodes = false)
    {
        var proto = _proto!;
        ref var cells = ref _rich ? ref _richCells : ref _cells;
        float cellM = CellFor(KindFarM(proto.Fades, fadeScaleSq));
        if (cells == null || cells.CellM != cellM)
        {
            if (cells != null && !ReferenceEquals(cells, _drawn))
                cells.Release();
            cells = Layout.Cut(proto, _rich ? _richVariant! : proto.Original, cellM, nodes ? fadeScaleSq : null);
            Own(cells);
        }
        cells.Range(fadeScaleSq);
        return cells;
    }

    // Draws `want` in place of the drawn layout at child index `at` (-1 appends), first bringing its
    // buffers up to the moved stamps.
    private void Show(Layout want, int at)
    {
        var proto = _proto!;
        want.CatchUp(_moved, AuthoredList);
        var old = _drawn;
        _drawn = want;
        var node = want.Attach(proto);
        proto.Root!.AddChild(node);
        if (at >= 0)
            proto.Root.MoveChild(node, at);
        if (!ReferenceEquals(old, want))
            old.Detach();
        if (old.IsCut && !ReferenceEquals(old, _cells) && !ReferenceEquals(old, _richCells))
            old.Release();
    }

    // Draws the near model's cells exactly while the Rich variant is drawn and the word asks for models,
    // brought up to the moved stamps. They are cut once and ranged again at the word's far edge.
    private void FollowNear()
    {
        if (_near == null || _proto == null)
            return;
        if (!_rich || !VegetationModels.On)
        {
            _nearCells?.Detach();
            return;
        }
        _nearProto ??= _proto with
        {
            Root = _near.Root,
            Name = _proto.Name + "_model",
            Mesh = _near.Mesh,
            Material = _near.Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            NodeBias = default
        };
        _nearCells ??= Layout.Cut(_nearProto, _richVariant! with { Material = _near.Material }, VegetationModels.CellM);
        _nearCells.RangeTo(VegetationModels.Band.Y);
        _nearCells.CatchUp(_moved, AuthoredList);
        if (_nearCells.Node == null)
            _near.Root.AddChild(_nearCells.Attach(_nearProto));
    }

    // Has a crater cull read and write this layout's stamps through here.
    private void Own(Layout layout)
    {
        for (int c = 0; c < layout.Meshes.Length; c++)
            ClutterCull.Own(layout.Meshes[c], this, layout.Members?[c]);
    }

    // One way of drawing the kind: its MultiMeshes, where each sits, and which placement is where.
    // The buffers outlive the nodes, which are made each time the layout is drawn.
    private sealed class Layout
    {
        private Layout(MultiMesh[] meshes, Material? material, Vector3[] offsets, int[]? cellOf, int[]? slotOf,
            int[][]? members, float cellM, float[] far2, float[] radius, bool[] neverFades)
        {
            Meshes = meshes;
            Material = material;
            Offsets = offsets;
            CellOf = cellOf;
            SlotOf = slotOf;
            Members = members;
            CellM = cellM;
            Far2 = far2;
            Radius = radius;
            NeverFades = neverFades;
            Ranges = new float[meshes.Length];
        }

        public MultiMesh[] Meshes { get; }

        // The variant's material: the Rich card shader reads the mirror the Original one never sees.
        public Material? Material { get; }

        public Vector3[] Offsets { get; }

        public int[]? CellOf { get; }

        public int[]? SlotOf { get; }

        // Each mesh's placements by slot; null for the whole layout, whose slot is the placement.
        public int[][]? Members { get; }

        // The cell edge the layout was cut at, 0 for the whole one.
        public float CellM { get; }

        public bool IsCut => CellM > 0f;

        public Node3D? Node { get; set; }

        private float[] Far2 { get; }

        private float[] Radius { get; }

        private bool[] NeverFades { get; }

        private float[] Ranges { get; }

        // The placements this layout's buffers hold off their authored transform.
        private Dictionary<int, Transform3D> Shown { get; } = new();

        public static Layout Single(MultiMesh mm, Material? material) => new(new[] { mm }, material,
            new[] { Vector3.Zero }, null, null, null, 0f, new float[1], new float[1], new bool[1]);

        // The variant cut into square cells of edge cellM. Given a scale, it is a load's cut: each
        // cell's node is made right after its buffer and ranged at that scale.
        // ⚠ Keep a load's cut making each node beside its buffer, before the whole node is freed.
        // Making the buffers first moves c1-rocket-hit-enhanced, though every float is the same.
        public static Layout Cut(Prototype proto, Stamps variant, float cellM, float? nodesAt = null)
        {
            var placements = variant.Placements;
            var fades = proto.Fades;
            var meshBox = proto.Mesh.GetAabb();
            var byCell = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < placements.Count; i++)
            {
                var o = placements[i].Origin;
                var key = (Mathf.FloorToInt(o.X / cellM), Mathf.FloorToInt(o.Z / cellM));
                if (!byCell.TryGetValue(key, out var list))
                    byCell[key] = list = new List<int>();
                list.Add(i);
            }

            int n = byCell.Count;
            var meshes = new MultiMesh[n];
            var offsets = new Vector3[n];
            var members = new int[n][];
            var far2 = new float[n];
            var radius = new float[n];
            var neverFades = new bool[n];
            var cellOf = new int[placements.Count];
            var slotOf = new int[placements.Count];
            var group = nodesAt != null ? new Node3D { Name = proto.Name } : null;
            int c = 0;
            foreach (var (_, list) in byCell)
            {
                Aabb? bounds = null;
                foreach (int i in list)
                {
                    var box = placements[i] * meshBox;
                    bounds = bounds?.Merge(box) ?? box;
                    far2[c] = Mathf.Max(far2[c], fades[i].G);
                    neverFades[c] |= fades[i].G <= 0f;
                }
                var offset = bounds!.Value.GetCenter();
                var local = new List<Transform3D>(list.Count);
                for (int s = 0; s < list.Count; s++)
                {
                    int i = list[s];
                    var placed = placements[i];
                    local.Add(new Transform3D(placed.Basis, placed.Origin - offset));
                    cellOf[i] = c;
                    slotOf[i] = s;
                }
                meshes[c] = Fill(proto.Mesh, local, list, fades, variant.Colors, perInstance: group != null);
                ClutterCull.Index(meshes[c], local);
                offsets[c] = offset;
                members[c] = list.ToArray();
                radius[c] = (bounds.Value.Size * 0.5f).Length() + proto.ExtraCullMargin;
                if (group != null)
                {
                    var cell = Instance(proto, variant.Material, $"{proto.Name}_{c}", meshes[c], offset);
                    cell.VisibilityRangeEnd = RangeEnd(true, neverFades[c], far2[c], radius[c], nodesAt!.Value);
                    group.AddChild(cell);
                }
                c++;
            }
            var layout = new Layout(meshes, variant.Material, offsets, cellOf, slotOf, members, cellM, far2, radius, neverFades)
            {
                Node = group,
            };
            if (nodesAt is { } scale)
                layout.Range(scale);
            return layout;
        }

        // Each cell's visibility range at the scale, on its node too when it is drawn.
        public void Range(float fadeScaleSq)
        {
            for (int c = 0; c < Meshes.Length; c++)
            {
                Ranges[c] = RangeEnd(true, NeverFades[c] || fadeScaleSq <= 0f, Far2[c], Radius[c], fadeScaleSq);
                if (Node != null && Node.GetChild(c) is MultiMeshInstance3D cell)
                    cell.VisibilityRangeEnd = Ranges[c];
            }
        }

        // Each cell's visibility range at a plain distance in metres past its bounds, on its node too.
        public void RangeTo(float metres)
        {
            for (int c = 0; c < Meshes.Length; c++)
            {
                Ranges[c] = metres + Radius[c];
                if (Node?.GetChild(c) is MultiMeshInstance3D cell)
                    cell.VisibilityRangeEnd = Ranges[c];
            }
        }

        // The node that draws the layout: the whole MultiMesh, or a group of one node per cell.
        public Node3D Attach(Prototype proto)
        {
            if (Node != null)
                return Node;
            if (!IsCut)
            {
                Node = Instance(proto, Material, proto.Name, Meshes[0], Vector3.Zero);
                return Node;
            }
            var group = new Node3D { Name = proto.Name };
            for (int c = 0; c < Meshes.Length; c++)
            {
                var cell = Instance(proto, Material, $"{proto.Name}_{c}", Meshes[c], Offsets[c]);
                cell.VisibilityRangeEnd = Ranges[c];
                group.AddChild(cell);
            }
            Node = group;
            return Node;
        }

        // Frees the nodes and keeps the buffers.
        public void Detach()
        {
            if (Node == null)
                return;
            Node.GetParent()?.RemoveChild(Node);
            Node.QueueFree();
            Node = null;
        }

        // Drops the buffers of a layout nothing will draw again.
        public void Release()
        {
            Detach();
            System.Array.Clear(Meshes);
        }

        // Brings the buffers up to `moved`: every stamp moved since it last drew, and every stamp
        // back on its authored transform since.
        public void CatchUp(Dictionary<int, Transform3D> moved, IReadOnlyList<Transform3D> placements)
        {
            foreach (var (i, placed) in moved)
            {
                if (!Shown.TryGetValue(i, out var held) || held != placed)
                    Write(i, placed);
            }
            var restored = new List<int>();
            foreach (var i in Shown.Keys)
            {
                if (!moved.ContainsKey(i))
                    restored.Add(i);
            }
            foreach (int i in restored)
                Write(i, placements[i]);
            Shown.Clear();
            foreach (var (i, placed) in moved)
                Shown[i] = placed;
        }

        // One placement's transform, given in the clutter root's frame.
        public void Write(int i, Transform3D placed)
        {
            int c = CellOf?[i] ?? 0;
            Meshes[c].SetInstanceTransform(SlotOf?[i] ?? i, new Transform3D(placed.Basis, placed.Origin - Offsets[c]));
            Shown[i] = placed;
        }

        // One cell's MultiMesh. A recut fills it with one buffer upload. A load fills it one instance
        // at a time, whose first frame carries a different motion history under TAA.
        // ⚠ Keep a load on the per-instance fill; the buffer moves c5-city-night-enhanced.
        // ⚠ Keep the one per-instance write ahead of the buffer. It gives the renderer a CPU copy, so
        // a later stamp write never reads the buffer back from the GPU.
        private static MultiMesh Fill(Mesh mesh, List<Transform3D> local, List<int> members,
            IReadOnlyList<Color> fades, IReadOnlyList<Color>? colors, bool perInstance)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = colors != null,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = local.Count,
            };
            if (perInstance)
            {
                for (int s = 0; s < local.Count; s++)
                {
                    mm.SetInstanceTransform(s, local[s]);
                    if (colors != null)
                        mm.SetInstanceColor(s, colors[members[s]]);
                    mm.SetInstanceCustomData(s, fades[members[s]]);
                }
                return mm;
            }
            int stride = colors != null ? ColorStride : Stride;
            var buffer = new float[local.Count * stride];
            for (int s = 0; s < local.Count; s++)
                Pack(buffer, s * stride, local[s], colors?[members[s]], fades[members[s]]);
            mm.SetInstanceCustomData(0, fades[members[0]]);
            mm.Buffer = buffer;
            return mm;
        }

        // One instance in MultiMesh buffer order: the basis by rows, each closed by an origin component.
        // The colour follows when there is one, and the custom data last. Godot multiplies the colour
        // into the card shader's COLOR.
        private static void Pack(float[] buffer, int at, Transform3D xf, Color? color, Color custom)
        {
            var b = xf.Basis;
            buffer[at] = b.X.X;
            buffer[at + 1] = b.Y.X;
            buffer[at + 2] = b.Z.X;
            buffer[at + 3] = xf.Origin.X;
            buffer[at + 4] = b.X.Y;
            buffer[at + 5] = b.Y.Y;
            buffer[at + 6] = b.Z.Y;
            buffer[at + 7] = xf.Origin.Y;
            buffer[at + 8] = b.X.Z;
            buffer[at + 9] = b.Y.Z;
            buffer[at + 10] = b.Z.Z;
            buffer[at + 11] = xf.Origin.Z;
            if (color is { } c)
            {
                buffer[at + 12] = c.R;
                buffer[at + 13] = c.G;
                buffer[at + 14] = c.B;
                buffer[at + 15] = c.A;
                at += 4;
            }
            buffer[at + 12] = custom.R;
            buffer[at + 13] = custom.G;
            buffer[at + 14] = custom.B;
            buffer[at + 15] = custom.A;
        }

        private static MultiMeshInstance3D Instance(Prototype proto, Material? material, string name, MultiMesh mm, Vector3 at)
        {
            var node = new MultiMeshInstance3D
            {
                Name = name,
                Multimesh = mm,
                MaterialOverride = material,
                CastShadow = proto.CastShadow,
                ExtraCullMargin = proto.ExtraCullMargin,
                Position = at,
            };
            if (proto.NodeBias.VariantType != Variant.Type.Nil)
                node.SetInstanceShaderParameter("node_bias", proto.NodeBias);
            return node;
        }
    }

    /// <summary>One way of placing a kind's stamps, index-aligned with the authored ones. It holds
    /// their transforms, their instance colours (null for none) and the material that draws them.</summary>
    public sealed record Stamps(IReadOnlyList<Transform3D> Placements, IReadOnlyList<Color>? Colors, Material? Material);

    /// <summary>A kind's near model (<see cref="VegetationModels"/>): the mesh and material drawing it on the Rich
    /// stamps. Its cells stand under the given node, never inside the clutter root.</summary>
    public sealed record Near(Node3D Root, Mesh Mesh, Material Material);

    // Everything the builder's whole node carried, and the authored stamps it held.
    private sealed record Prototype(Node3D? Root, StringName Name, Mesh Mesh, Material? Material,
        GeometryInstance3D.ShadowCastingSetting CastShadow, float ExtraCullMargin, Variant NodeBias,
        IReadOnlyList<Transform3D> Placements, IReadOnlyList<Color> Fades)
    {
        public Stamps Original { get; } = new(Placements, null, Material);
    }
}
