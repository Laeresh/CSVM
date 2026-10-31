using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>One textured world polygon under a point: its texture, its <c>no_clutter</c> flag and its
/// height there.</summary>
public readonly record struct GroundLayer(string Texture, bool NoClutter, float Y);

/// <summary>One world triangle as <see cref="VegetationGround.PaintedSurface"/> gives it.</summary>
public readonly record struct SurfaceTriangle(string Texture, bool Lit, Corner A, Corner B, Corner C);

/// <summary>One corner of a <see cref="SurfaceTriangle"/>, in world space.</summary>
public readonly record struct Corner(Vector3 At, Vector2 Uv, Color Color, Vector3 Normal);

/// <summary>One world triangle in world space with its texture, its corners' UVs and their baked colours.</summary>
public readonly record struct CoverTriangle(string Texture, Vector3 A, Vector3 B, Vector3 C, Vector2 UvA, Vector2 UvB, Vector2 UvC,
    Color ColA, Color ColB, Color ColC);

/// <summary>
/// The ground textures under a world point, what the vegetation lab's texture pick reads. It walks the
/// world node's tree as the clutter does, skipping the same nodes and every LOD but the nearest.
/// A pick therefore names a texture the clutter matches on. The same walk gives the lab's ground under
/// the cursor (<see cref="Triangles"/>, <see cref="RayHit"/>). ⚠ Do not let a pick skip the <c>no_clutter</c> gate:
/// <see cref="Decorating"/> answers the coplanar layer that takes stamps, never a flagged overlay.
/// </summary>
public static class VegetationGround
{
    /// <summary>How far across from a foot, and how far above it, a polygon of another texture covers it
    /// (<see cref="Overlaid"/>).</summary>
    public const float OverlayReachM = 0.5f, OverlayRiseM = 1.5f;

    /// <summary>How far below a foot an overlay may sit and still cover it: a coplanar layer's rounding.</summary>
    public const float OverlaySinkM = 0.25f;

    // Coplanar layers lie within this of each other; a decal over its base sits a few centimetres up.
    private const float CoplanarM = 0.5f;

    // The foot and four points the reach around it, each tested against an overlay.
    private static readonly (float X, float Z)[] Probes =
        { (0f, 0f), (OverlayReachM, 0f), (-OverlayReachM, 0f), (0f, OverlayReachM), (0f, -OverlayReachM) };

    /// <summary>Every textured world polygon whose XZ shadow holds the point, nearest in height to
    /// <paramref name="point"/> first.</summary>
    public static List<GroundLayer> LayersAt(GameZ gamez, Vector3 point, string worldName = "world1")
    {
        var layers = new List<GroundLayer>();
        WalkWorld(gamez, worldName, (_, mesh, xf) => AddLayers(gamez, mesh, xf, point, layers));
        layers.Sort((a, b) => Math.Abs(a.Y - point.Y).CompareTo(Math.Abs(b.Y - point.Y)));
        return layers;
    }

    /// <summary>Every solid world polygon's triangles in world space, three corners each, for
    /// <see cref="RayHit"/>. Walked as <see cref="LayersAt"/> walks, past what builds no collider (clouds,
    /// sky, billboards). Build it once: a ray is then one linear scan instead of a tree walk.</summary>
    public static Vector3[] Triangles(GameZ gamez, string worldName = "world1")
    {
        var corners = new List<Vector3>();
        WalkWorld(gamez, worldName, (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = Corners(poly, i);
                    corners.Add(xf * mesh.Vertices[a]);
                    corners.Add(xf * mesh.Vertices[b]);
                    corners.Add(xf * mesh.Vertices[c]);
                }
            }
        }, n => WorldBuilder.NoCollisionNode(gamez, n));
        return corners.ToArray();
    }

    /// <summary>Every world triangle textured with a ground texture <paramref name="picked"/> accepts, three
    /// corners each in world space, whatever its <c>no_clutter</c> flag: painted canopy shows on either layer.
    /// The subtrees <paramref name="switchedOff"/> names are left out (<see cref="ClutterBuilder.SwitchedOff"/>).</summary>
    public static Vector3[] Painted(GameZ gamez, Func<string, bool> picked, string worldName = "world1", Func<GameZNode, bool>? switchedOff = null)
    {
        var corners = new List<Vector3>();
        WalkWorld(gamez, worldName, (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (poly.MaterialIndex < 0 || poly.MaterialIndex >= gamez.Materials.Count
                    || gamez.Materials[poly.MaterialIndex].TextureName is not { } tex || !picked(tex))
                    continue;
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = Corners(poly, i);
                    corners.Add(xf * mesh.Vertices[a]);
                    corners.Add(xf * mesh.Vertices[b]);
                    corners.Add(xf * mesh.Vertices[c]);
                }
            }
        }, switchedOff);
        return corners.ToArray();
    }

    /// <summary>As <see cref="Painted"/>, each triangle with what the world draws it with: its texture and lit flag.
    /// Each corner carries its UV, baked vertex colour (white for none) and world normal.</summary>
    public static List<SurfaceTriangle> PaintedSurface(GameZ gamez, Func<string, bool> picked, string worldName = "world1",
        Func<GameZNode, bool>? switchedOff = null)
    {
        var found = new List<SurfaceTriangle>();
        WalkWorld(gamez, worldName, (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (poly.MaterialIndex < 0 || poly.MaterialIndex >= gamez.Materials.Count
                    || gamez.Materials[poly.MaterialIndex].TextureName is not { } tex || !picked(tex) || poly.UvCoords is not { } uvs)
                    continue;
                var ids = poly.VertexIndices;
                for (int i = 0; i + 2 < ids.Count; i++)
                {
                    var slots = poly.TriangleStrip ? new[] { i, i + 1, i + 2 } : new[] { 0, i + 1, i + 2 };
                    var corners = new Corner[3];
                    for (int k = 0; k < 3; k++)
                    {
                        int s = slots[k];
                        var n = poly.NormalIndices is { } ni && s < ni.Count && ni[s] >= 0 && ni[s] < mesh.Normals.Count
                            ? (xf.Basis * mesh.Normals[ni[s]]).Normalized() : Vector3.Up;
                        corners[k] = new Corner(xf * mesh.Vertices[ids[s]], s < uvs.Count ? uvs[s] : Vector2.Zero,
                            poly.VertexColors is { } vc && s < vc.Count ? vc[s] : Colors.White, n);
                    }
                    found.Add(new SurfaceTriangle(tex, mesh.Lighting, corners[0], corners[1], corners[2]));
                }
            }
        }, switchedOff);
        return found;
    }

    /// <summary>Every unflagged world triangle textured with a texture <paramref name="picked"/> accepts, with its UVs
    /// and its baked corner colours, for the ground cover (<see cref="GroundCover"/>). The <c>no_clutter</c> gate
    /// holds as for a stamp, so one coplanar layer grows cover. ⚠ Read UVs and colours by corner position, never by
    /// vertex id: C1 model 953 lists one vertex at two corners with two UVs.</summary>
    public static List<CoverTriangle> Covered(GameZ gamez, Func<string, bool> picked, string worldName = "world1",
        Func<GameZNode, bool>? switchedOff = null)
    {
        var found = new List<CoverTriangle>();
        WalkWorld(gamez, worldName, (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (poly.NoClutter || poly.UvCoords is not { } uvs || uvs.Count < poly.VertexIndices.Count
                    || poly.MaterialIndex < 0 || poly.MaterialIndex >= gamez.Materials.Count
                    || gamez.Materials[poly.MaterialIndex].TextureName is not { } tex || !picked(tex))
                    continue;
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = Positions(poly, i);
                    Color Col(int at) => poly.VertexColors is { } cols && at < cols.Count ? cols[at] : Colors.White;
                    found.Add(new CoverTriangle(tex, xf * mesh.Vertices[poly.VertexIndices[a]], xf * mesh.Vertices[poly.VertexIndices[b]],
                        xf * mesh.Vertices[poly.VertexIndices[c]], uvs[a], uvs[b], uvs[c], Col(a), Col(b), Col(c)));
                }
            }
        }, switchedOff);
        return found;
    }

    /// <summary>The gamez index of every world node, switched off or not, whose own mesh holds a polygon textured
    /// with a texture <paramref name="picked"/> accepts. Switching one moves what <see cref="Painted"/> gives.</summary>
    public static HashSet<int> Holders(GameZ gamez, Func<string, bool> picked, string worldName = "world1")
    {
        var found = new HashSet<int>();
        WalkWorld(gamez, worldName, (node, mesh, _) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (poly.MaterialIndex >= 0 && poly.MaterialIndex < gamez.Materials.Count
                    && gamez.Materials[poly.MaterialIndex].TextureName is { } tex && picked(tex))
                {
                    found.Add(node.Index);
                    return;
                }
            }
        });
        return found;
    }

    /// <summary>Every world triangle whose texture <paramref name="ground"/> refuses, whatever its <c>no_clutter</c> flag,
    /// binned for <see cref="Overlays.Covers"/>: the polygons a plant on that ground is kept off.</summary>
    public static Overlays OverlaysOn(GameZ gamez, Func<string, bool> ground, string worldName = "world1") =>
        new(Painted(gamez, tex => !ground(tex), worldName));

    /// <summary>Whether a texture blends one ground into another, by the install's naming: <c>terpat02_trans1</c>,
    /// <c>cliff01-trans1</c>, <c>hilltrans2</c>. Every chapter texture holding "trans" is such a blend.</summary>
    public static bool IsTransition(string texture) => texture.Contains("trans", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether any of <paramref name="triangles"/> (three corners each) covers a foot at <paramref name="at"/>:
    /// it holds the foot or a point <see cref="OverlayReachM"/> beside it, under <see cref="OverlayRiseM"/> above it.
    /// That is a rail, a road, an apron or a decal drawn as its own polygon over the ground, which no plant stands on.</summary>
    public static bool Overlaid(IReadOnlyList<Vector3> triangles, Vector3 at)
    {
        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            Vector3 a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (at.X + OverlayReachM < MathF.Min(a.X, MathF.Min(b.X, c.X)) || at.X - OverlayReachM > MathF.Max(a.X, MathF.Max(b.X, c.X))
                || at.Z + OverlayReachM < MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) || at.Z - OverlayReachM > MathF.Max(a.Z, MathF.Max(b.Z, c.Z)))
                continue;
            foreach (var (dx, dz) in Probes)
            {
                if (HeightIn(a, b, c, new Vector3(at.X + dx, 0f, at.Z + dz)) is { } y && y > at.Y - OverlaySinkM && y < at.Y + OverlayRiseM)
                    return true;
            }
        }
        return false;
    }

    /// <summary>The nearest point ahead of <paramref name="from"/> along <paramref name="dir"/> on any of
    /// <paramref name="triangles"/> (from <see cref="Triangles"/>), either face; null when the ray meets none.
    /// The lab's ground under the cursor, since a <c>--freecam</c> world builds no colliders to ray.</summary>
    public static Vector3? RayHit(Vector3[] triangles, Vector3 from, Vector3 dir)
    {
        float nearest = float.PositiveInfinity;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            // Moller-Trumbore.
            Vector3 a = triangles[i], e1 = triangles[i + 1] - a, e2 = triangles[i + 2] - a;
            Vector3 p = dir.Cross(e2);
            float det = e1.Dot(p);
            if (Math.Abs(det) < 1e-9f)
                continue;
            float inv = 1f / det;
            Vector3 s = from - a;
            float u = s.Dot(p) * inv;
            if (u < 0f || u > 1f)
                continue;
            Vector3 q = s.Cross(e1);
            float v = dir.Dot(q) * inv;
            if (v < 0f || u + v > 1f)
                continue;
            float t = e2.Dot(q) * inv;
            if (t > 0f && t < nearest)
                nearest = t;
        }
        return float.IsPositiveInfinity(nearest) ? null : from + (dir * nearest);
    }

    /// <summary>The layer that takes stamps among those coplanar with the nearest: the first not flagged
    /// <c>no_clutter</c>. Null when every one is flagged, ground the original leaves bare, or none is there.</summary>
    public static GroundLayer? Decorating(IReadOnlyList<GroundLayer> layers)
    {
        if (layers.Count == 0)
            return null;
        float y = layers[0].Y;
        foreach (var layer in layers)
        {
            if (Math.Abs(layer.Y - y) > CoplanarM)
                break;
            if (!layer.NoClutter)
                return layer;
        }

        return null;
    }

    // The triangle's height at the point's XZ when its XZ shadow holds the point; null for a wall.
    internal static float? HeightIn(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    {
        float d = ((b.Z - c.Z) * (a.X - c.X)) + ((c.X - b.X) * (a.Z - c.Z));
        if (Math.Abs(d) < 1e-6f)
            return null;
        float w0 = (((b.Z - c.Z) * (p.X - c.X)) + ((c.X - b.X) * (p.Z - c.Z))) / d;
        float w1 = (((c.Z - a.Z) * (p.X - c.X)) + ((a.X - c.X) * (p.Z - c.Z))) / d;
        float w2 = 1f - w0 - w1;
        if (w0 < 0f || w1 < 0f || w2 < 0f)
            return null;
        return (w0 * a.Y) + (w1 * b.Y) + (w2 * c.Y);
    }

    private static void AddLayers(GameZ gamez, GameZMesh mesh, Transform3D xf, Vector3 point, List<GroundLayer> into)
    {
        foreach (var poly in mesh.Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= gamez.Materials.Count
                || gamez.Materials[poly.MaterialIndex].TextureName is not { } tex)
                continue;
            for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
            {
                var (a, b, c) = Corners(poly, i);
                if (HeightIn(xf * mesh.Vertices[a], xf * mesh.Vertices[b], xf * mesh.Vertices[c], point) is { } y)
                {
                    into.Add(new GroundLayer(tex, poly.NoClutter, y));
                    break;
                }
            }
        }
    }

    // The clutter's walk of the world node's tree: the same skipped nodes, the nearest LOD only, and
    // past any subtree the prune predicate names.
    private static void WalkWorld(GameZ gamez, string worldName, Action<GameZNode, GameZMesh, Transform3D> visit, Func<GameZNode, bool>? prune = null)
    {
        GameZNode? world = gamez.Nodes.Find(n => n.Kind == "World" && string.Equals(n.Name, worldName, StringComparison.OrdinalIgnoreCase));
        if (world == null)
            return;

        void Walk(int nodeIndex, Transform3D xf)
        {
            if (nodeIndex < 0 || nodeIndex >= gamez.Nodes.Count)
                return;
            var node = gamez.Nodes[nodeIndex];
            if (WorldBuilder.SkipWorldNode(node) || (node.Kind == "Lod" && node.LodRangeMin != 0f) || prune?.Invoke(node) == true)
                return;
            if (node.Local is { } local)
                xf *= local;
            if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
                visit(node, gamez.Meshes[node.MeshIndex], xf);
            foreach (var c in node.Children)
                Walk(c, xf);
        }

        foreach (var c in world.Children)
            Walk(c, Transform3D.Identity);
        if (world.PartitionNodes != null)
        {
            foreach (var idx in world.PartitionNodes)
                Walk(idx, Transform3D.Identity);
        }
    }

    // The clutter's own enumeration: a strip walks consecutive triples, a fan pivots on corner 0.
    private static (int A, int B, int C) Corners(GameZPolygon poly, int i)
    {
        var (a, b, c) = Positions(poly, i);
        return (poly.VertexIndices[a], poly.VertexIndices[b], poly.VertexIndices[c]);
    }

    // The same triangle's corner positions in the polygon, which its UVs and colours are indexed by.
    private static (int A, int B, int C) Positions(GameZPolygon poly, int i) =>
        poly.TriangleStrip ? (i, i + 1, i + 2) : (0, i + 1, i + 2);

    /// <summary>Overlay triangles binned by square cell, so a query reads only the cell its foot stands in.</summary>
    public sealed class Overlays
    {
        private const float CellM = 64f;
        private readonly Dictionary<(int, int), List<Vector3>> _cells = new();

        public Overlays(Vector3[] corners)
        {
            for (int i = 0; i + 2 < corners.Length; i += 3)
            {
                Vector3 lo = corners[i].Min(corners[i + 1]).Min(corners[i + 2]), hi = corners[i].Max(corners[i + 1]).Max(corners[i + 2]);
                for (int cx = Cell(lo.X - OverlayReachM); cx <= Cell(hi.X + OverlayReachM); cx++)
                {
                    for (int cz = Cell(lo.Z - OverlayReachM); cz <= Cell(hi.Z + OverlayReachM); cz++)
                    {
                        if (!_cells.TryGetValue((cx, cz), out var list))
                            _cells[(cx, cz)] = list = new List<Vector3>();
                        list.Add(corners[i]);
                        list.Add(corners[i + 1]);
                        list.Add(corners[i + 2]);
                    }
                }
            }
        }

        /// <summary>Whether an overlay covers a foot at <paramref name="at"/> (<see cref="Overlaid"/>).</summary>
        public bool Covers(Vector3 at) => _cells.TryGetValue((Cell(at.X), Cell(at.Z)), out var list) && Overlaid(list, at);

        /// <summary>The overlay triangles binned in the cells an XZ box from <paramref name="lo"/> to <paramref name="hi"/>
        /// touches, each once, three corners each.</summary>
        public List<Vector3> Near(Vector2 lo, Vector2 hi)
        {
            var found = new List<Vector3>();
            var seen = new HashSet<(Vector3, Vector3, Vector3)>();
            for (int cx = Cell(lo.X); cx <= Cell(hi.X); cx++)
            {
                for (int cz = Cell(lo.Y); cz <= Cell(hi.Y); cz++)
                {
                    if (!_cells.TryGetValue((cx, cz), out var list))
                        continue;
                    for (int i = 0; i + 2 < list.Count; i += 3)
                    {
                        if (seen.Add((list[i], list[i + 1], list[i + 2])))
                            found.AddRange(new[] { list[i], list[i + 1], list[i + 2] });
                    }
                }
            }
            return found;
        }

        private static int Cell(float v) => (int)MathF.Floor(v / CellM);
    }
}
