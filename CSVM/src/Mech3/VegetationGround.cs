using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>One textured world polygon under a point: its texture, its <c>no_clutter</c> flag and its
/// height there.</summary>
public readonly record struct GroundLayer(string Texture, bool NoClutter, float Y);

/// <summary>
/// The ground textures under a world point, what the vegetation lab's texture pick reads. It walks the
/// world node's tree as the clutter does, skipping the same nodes and every LOD but the nearest.
/// A pick therefore names a texture the clutter matches on. The same walk gives the lab's ground under
/// the cursor (<see cref="Triangles"/>, <see cref="RayHit"/>). ⚠ Do not let a pick skip the <c>no_clutter</c> gate:
/// <see cref="Decorating"/> answers the coplanar layer that takes stamps, never a flagged overlay.
/// </summary>
public static class VegetationGround
{
    // Coplanar layers lie within this of each other; a decal over its base sits a few centimetres up.
    private const float CoplanarM = 0.5f;

    /// <summary>Every textured world polygon whose XZ shadow holds the point, nearest in height to
    /// <paramref name="point"/> first.</summary>
    public static List<GroundLayer> LayersAt(GameZ gamez, Vector3 point, string worldName = "world1")
    {
        var layers = new List<GroundLayer>();
        WalkWorld(gamez, worldName, (mesh, xf) => AddLayers(gamez, mesh, xf, point, layers));
        layers.Sort((a, b) => Math.Abs(a.Y - point.Y).CompareTo(Math.Abs(b.Y - point.Y)));
        return layers;
    }

    /// <summary>Every solid world polygon's triangles in world space, three corners each, for
    /// <see cref="RayHit"/>. Walked as <see cref="LayersAt"/> walks, past what builds no collider (clouds,
    /// sky, billboards). Build it once: a ray is then one linear scan instead of a tree walk.</summary>
    public static Vector3[] Triangles(GameZ gamez, string worldName = "world1")
    {
        var corners = new List<Vector3>();
        WalkWorld(gamez, worldName, (mesh, xf) =>
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
    private static void WalkWorld(GameZ gamez, string worldName, Action<GameZMesh, Transform3D> visit, Func<GameZNode, bool>? prune = null)
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
                visit(gamez.Meshes[node.MeshIndex], xf);
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
        var ids = poly.VertexIndices;
        return poly.TriangleStrip ? (ids[i], ids[i + 1], ids[i + 2]) : (ids[0], ids[i + 1], ids[i + 2]);
    }

    // The triangle's height at the point's XZ when its XZ shadow holds the point; null for a wall.
    private static float? HeightIn(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
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
}
