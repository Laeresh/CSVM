using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>
/// A plane mod's wreck and effect anchors when its model ships neither the stock <c>destroyed</c>
/// subtree nor the <c>pdpN</c> panels. Every drawn triangle under <c>healthy</c> goes to the damage
/// zone of the collider hull holding its centroid (<see cref="PlaneDamage.MapStruckPart"/>). The
/// pieces are therefore the zones a hit is charged to. The wreck is a <c>destroyed</c> group of
/// <c>piece1</c>..<c>piece4</c>, the names the stock crash and destroy defs fling. Each piece's
/// origin is its zone's centre, where a missing stock effect anchor is also placed.
/// Format: docs/formats/mod-planes.md.
/// </summary>
public sealed class AutoWreck
{
    /// <summary>The zones in piece order. The nose, the fuselage forward of the origin, is
    /// <c>piece1</c>, the piece the stock destroy def flies on the aircraft's momentum alone.</summary>
    public static readonly string[] Zones = { "nose", "tail", "leftwing", "rightwing" };

    /// <summary>Stock anchors the AI damage stages and the damage shims site fire at, with the zone each
    /// stands in for (docs/org/vehicleDamage.md, "The AI stage anchors"). A <c>pdpN</c> anchor takes its
    /// zone from the ladder instead.</summary>
    public static readonly IReadOnlyList<(string Node, string Zone)> NamedAnchors = new[]
    {
        ("nose", "nose"), ("tail", "tail"), ("leftwing", "leftwing"), ("rightwing", "rightwing"),
        ("prop1", "nose"), ("lailer1", "leftwing"), ("railer1", "rightwing"), ("lft_elev", "tail"), ("rt_elev", "tail"),
    };

    private readonly Node3D _model;
    private readonly List<Slice>[] _slices = Zones.Select(_ => new List<Slice>()).ToArray();
    private readonly Aabb?[] _bounds = new Aabb?[Zones.Length];

    private AutoWreck(Node3D model) => _model = model;

    /// <summary>Each zone's centre in the plane frame below the model root, the bounds centre of its
    /// triangles; null for a zone no triangle fell in.</summary>
    public IReadOnlyList<Vector3?> Centres => _bounds.Select(b => b?.GetCenter()).ToList();

    /// <summary>Each zone's triangle count.</summary>
    public IReadOnlyList<int> TriangleCounts => _slices.Select(z => z.Sum(s => s.Surfaces.Sum(i => i.Count / 3))).ToList();

    /// <summary>Cuts <paramref name="model"/>'s drawn triangles by the hulls in <paramref name="parts"/>,
    /// which are in the model's parent frame as <see cref="PlaneCollider.Build"/> made them.</summary>
    public static AutoWreck Cut(Node3D model, IReadOnlyList<PlaneCollider.Part> parts)
    {
        var cut = new AutoWreck(model);
        var source = CockpitTransplant.FindNamed(model, "healthy") ?? model;
        cut.Walk(source, parts);
        return cut;
    }

    /// <summary>The zone index, into <see cref="Zones"/>, of a point in the hulls' frame: the first hull
    /// holding it, else the nearest one.</summary>
    public static int ZoneOf(Vector3 point, IReadOnlyList<PlaneCollider.Part> parts)
    {
        string region = "fuselage";
        float best = float.PositiveInfinity;
        foreach (var part in parts)
        {
            float d = part.Hull.Distance(point - part.Local.Origin, out _);
            if (d < best)
            {
                best = d;
                region = part.Name;
            }

            if (d <= 0f)
            {
                break;
            }
        }

        return System.Array.IndexOf(Zones, PlaneDamage.MapStruckPart(region, point));
    }

    /// <summary>The wreck: a hidden-ready <c>destroyed</c> group at the model root's frame holding one
    /// <c>pieceN</c> per zone, each drawing its triangles with the airframe's own materials.</summary>
    public Node3D Build()
    {
        var destroyed = new Node3D { Name = "destroyed" };
        for (int z = 0; z < Zones.Length; z++)
        {
            var piece = new Node3D { Name = $"piece{z + 1}", Position = _bounds[z]?.GetCenter() ?? Vector3.Zero };
            foreach (var slice in _slices[z])
            {
                piece.AddChild(slice.Instance(piece.Transform));
            }

            destroyed.AddChild(piece);
        }

        return destroyed;
    }

    /// <summary>Adds an empty node at its zone's centre for each stock effect anchor the model lacks.
    /// Those are the named anchors and <c>pdpN</c> for each <c>pdpanelN</c> on a zone's ladder.
    /// They hang under <c>healthy</c>, so they leave with the airframe on a
    /// kill as a torn panel does. Returns how many it added.</summary>
    public int AddAnchors(IEnumerable<DestroyablePart> zones)
    {
        var wanted = NamedAnchors.ToList();
        foreach (var zone in zones)
        {
            foreach (var (_, anim) in zone.InjureAnims)
            {
                if (anim.StartsWith("pdpanel", System.StringComparison.OrdinalIgnoreCase))
                {
                    wanted.Add(("pdp" + anim["pdpanel".Length..], zone.Name));
                }
            }
        }

        var parent = CockpitTransplant.FindNamed(_model, "healthy") ?? _model;
        var toParent = (ReferenceEquals(parent, _model) ? Transform3D.Identity : NodeFrame.Under(parent, _model)).AffineInverse();
        int added = 0;
        foreach (var (node, zone) in wanted.Distinct())
        {
            int z = System.Array.FindIndex(Zones, n => n.Equals(zone, System.StringComparison.OrdinalIgnoreCase));
            if (z < 0 || _bounds[z] is not { } box || CockpitTransplant.FindNamed(_model, node) != null)
            {
                continue;
            }

            parent.AddChild(new Node3D { Name = node, Position = toParent * box.GetCenter() });
            added++;
        }

        return added;
    }

    // Collect's rule: a hidden subtree and the blur discs are not airframe.
    private void Walk(Node node, IReadOnlyList<PlaneCollider.Part> parts)
    {
        if (node is Node3D n3d)
        {
            if (!n3d.Visible || PropParts.Classify(n3d.Name) is PropParts.Kind.PropMain or PropParts.Kind.PropGhost)
            {
                return;
            }

            if (n3d is MeshInstance3D { Mesh: ArrayMesh mesh } instance)
            {
                CutInstance(instance, mesh, parts);
                return;
            }
        }

        foreach (var child in node.GetChildren())
        {
            Walk(child, parts);
        }
    }

    private void CutInstance(MeshInstance3D instance, ArrayMesh mesh, IReadOnlyList<PlaneCollider.Part> parts)
    {
        var frame = NodeFrame.Under(instance, _model);
        var toHulls = _model.Transform * frame;
        var slices = new Slice?[Zones.Length];
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            if (mesh.SurfaceGetPrimitiveType(s) != Mesh.PrimitiveType.Triangles)
            {
                continue;
            }

            var arrays = mesh.SurfaceGetArrays(s);
            var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indexArray = arrays[(int)Mesh.ArrayType.Index];
            var index = indexArray.VariantType == Variant.Type.Nil ? System.Array.Empty<int>() : indexArray.AsInt32Array();
            int count = index.Length > 0 ? index.Length : verts.Length;
            for (int t = 0; t + 2 < count; t += 3)
            {
                int a = index.Length > 0 ? index[t] : t, b = index.Length > 0 ? index[t + 1] : t + 1, c = index.Length > 0 ? index[t + 2] : t + 2;
                int z = ZoneOf(toHulls * ((verts[a] + verts[b] + verts[c]) / 3f), parts);
                if (z < 0)
                {
                    continue;
                }

                var slice = slices[z] ??= new Slice(instance, mesh, frame);
                slice.Add(s, a, b, c);
                foreach (int v in new[] { a, b, c })
                {
                    var p = frame * verts[v];
                    _bounds[z] = _bounds[z] is { } box ? box.Expand(p) : new Aabb(p, Vector3.Zero);
                }
            }
        }

        for (int z = 0; z < Zones.Length; z++)
        {
            if (slices[z] is { } slice)
            {
                _slices[z].Add(slice);
            }
        }
    }

    // One source instance's triangles in one zone, as index lists per surface. The vertex arrays stay
    // whole, so a piece shares no buffer edit with the airframe and needs no vertex remap.
    private sealed class Slice
    {
        private readonly MeshInstance3D _source;
        private readonly ArrayMesh _mesh;
        private readonly Transform3D _frame;

        public Slice(MeshInstance3D source, ArrayMesh mesh, Transform3D frame)
        {
            (_source, _mesh, _frame) = (source, mesh, frame);
            Surfaces = Enumerable.Range(0, mesh.GetSurfaceCount()).Select(_ => new List<int>()).ToArray();
        }

        public List<int>[] Surfaces { get; }

        public void Add(int surface, int a, int b, int c) => Surfaces[surface].AddRange(new[] { a, b, c });

        // ⚠ Never Duplicate() the source instance; under the separate render thread the process crashes.
        public MeshInstance3D Instance(Transform3D piece)
        {
            var own = new ArrayMesh();
            for (int s = 0; s < Surfaces.Length; s++)
            {
                if (Surfaces[s].Count == 0)
                {
                    continue;
                }

                var arrays = _mesh.SurfaceGetArrays(s);
                arrays[(int)Mesh.ArrayType.Index] = Surfaces[s].ToArray();
                CockpitTransplant.CopySurface(_mesh, s, own, arrays);
                own.SurfaceSetMaterial(own.GetSurfaceCount() - 1, _source.GetActiveMaterial(s));
            }

            var copy = new MeshInstance3D
            {
                Name = "mesh",
                Mesh = own,
                Layers = _source.Layers,
                CastShadow = _source.CastShadow,
                Transform = piece.AffineInverse() * _frame,
            };
            copy.SetInstanceShaderParameter(CockpitTransplant.NodeBias, _source.GetInstanceShaderParameter(CockpitTransplant.NodeBias));
            return copy;
        }
    }
}
