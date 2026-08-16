using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The cross-node draw-order tie-break: a dense rank over the world's conflict graph, ranking
/// only nodes with a genuine coplanar, same-priority, same-<c>no_clutter</c> conflict.
/// ⚠ Ranking by flat node index instead (<see cref="SceneBuilder.NodeOrderBias"/>) spends the
/// whole index range and lets a within-mesh surface rank out-bid it — see analysis/bl-053-dense-rank.
/// Every edge runs low node index → high node index, so the longest-path layering is a
/// topological order of the original's own draw order and cannot invert authored layering.
/// The bias-value derivation lives on <see cref="SceneBuilder.ConflictRankBias"/>.
/// </summary>
internal static class ConflictRank
{
    // Plane quantisation. The conflicting surfaces this exists for are coplanar to machine
    // precision (measured: over the C5 repro's 61 y=5 polygons the distinct |n.y| set is {1.0}
    // and the distinct |d| set is {5.0}), so these tolerances only absorb float noise.
    private const float NormalTolerance = 1e-4f;
    private const float PlaneTolerance = 1e-3f;

    // Overlap below this is a shared edge or a corner touch, not two surfaces fighting.
    private const float MinOverlapArea = 1.0f;

    /// <summary>
    /// Ranks the nodes of one world. <paramref name="nodes"/> is every node the world build will
    /// reach, with the world transform it will be built at; <paramref name="excluded"/> names the
    /// walk roots whose subtrees take no part (the origin-parked pile — see the caller).
    /// </summary>
    public static Report Compute(GameZ gamez, IReadOnlyList<(GameZNode Node, Transform3D World)> nodes,
        int excludedRoots)
    {
        var buckets = new Dictionary<(int, int, int, int), List<Tri>>();
        int triangles = 0;
        foreach (var (node, world) in nodes)
        {
            if (node.MeshIndex < 0 || node.MeshIndex >= gamez.Meshes.Count || gamez.IsMarkerGizmo(node.MeshIndex))
                continue;
            var mesh = gamez.Meshes[node.MeshIndex];
            foreach (var poly in mesh.Polygons)
            {
                int n = poly.VertexIndices.Count;
                if (n < 3)
                    continue;
                // Triangulated exactly as SceneBuilder.EmitPolygon does. A tri_strip's raw index
                // list is NOT a polygon outline — fanning it measures a shape nothing draws, and
                // manufactured a 6.8 million m² phantom overlap the first time this was measured.
                if (poly.TriangleStrip)
                {
                    for (int i = 0; i + 2 < n; i++)
                    {
                        if ((i & 1) == 0)
                            Add(buckets, ref triangles, node, world, mesh, poly, i, i + 1, i + 2);
                        else
                            Add(buckets, ref triangles, node, world, mesh, poly, i, i + 2, i + 1);
                    }
                }
                else
                {
                    for (int i = 1; i + 1 < n; i++)
                        Add(buckets, ref triangles, node, world, mesh, poly, 0, i, i + 1);
                }
            }
        }

        var successors = new Dictionary<int, HashSet<int>>();
        int pairs = 0;
        var merged = new List<Tri>();
        foreach (var (key, bucket) in buckets)
        {
            // A pair can be coplanar yet land either side of a quantisation boundary: the two
            // sides reach the shared plane through different transforms. Pairing each bucket
            // with the next plane offset up recovers those splits (C5: 294 -> 326 pairs).
            var neighbour = (key.Item1, key.Item2, key.Item3, key.Item4 + 1);
            if (!buckets.TryGetValue(neighbour, out var above))
            {
                pairs += Pair(bucket, successors);
                continue;
            }
            merged.Clear();
            merged.AddRange(bucket);
            merged.AddRange(above);
            pairs += Pair(merged, successors);
        }

        var ranks = Layer(successors);
        int max = 0;
        foreach (int r in ranks.Values)
            max = Math.Max(max, r);
        return new Report(ranks, pairs, max, triangles, excludedRoots);
    }

    private static void Add(Dictionary<(int, int, int, int), List<Tri>> buckets, ref int count,
        GameZNode node, Transform3D world, GameZMesh mesh, GameZPolygon poly, int a, int b, int c)
    {
        var va = world * mesh.Vertices[poly.VertexIndices[a]];
        var vb = world * mesh.Vertices[poly.VertexIndices[b]];
        var vc = world * mesh.Vertices[poly.VertexIndices[c]];
        // In double: a world coordinate runs to ~1e4, where a float has barely the plane
        // tolerance's worth of precision left and the quantisation would be noise.
        double ux = vb.X - va.X, uy = vb.Y - va.Y, uz = vb.Z - va.Z;
        double wx = vc.X - va.X, wy = vc.Y - va.Y, wz = vc.Z - va.Z;
        double nx = (uy * wz) - (uz * wy);
        double ny = (uz * wx) - (ux * wz);
        double nz = (ux * wy) - (uy * wx);
        double len = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
        if (len <= 1e-9)
            return;
        nx /= len;
        ny /= len;
        nz /= len;
        // Canonical orientation: the two sides of one plane are the same plane here, so flip so
        // the largest-magnitude axis is positive. Otherwise a back-to-back pair lands in two
        // buckets and never meets.
        double ax = Math.Abs(nx), ay = Math.Abs(ny), az = Math.Abs(nz);
        double dominant = ax >= ay ? (ax >= az ? nx : nz) : (ay >= az ? ny : nz);
        if (dominant < 0)
        {
            nx = -nx;
            ny = -ny;
            nz = -nz;
        }
        double d = (nx * va.X) + (ny * va.Y) + (nz * va.Z);
        var key = ((int)Math.Round(nx / NormalTolerance), (int)Math.Round(ny / NormalTolerance),
            (int)Math.Round(nz / NormalTolerance), (int)Math.Round(d / PlaneTolerance));
        if (!buckets.TryGetValue(key, out var list))
            buckets[key] = list = new List<Tri>();
        list.Add(new Tri(node.Index, poly.Priority, poly.NoClutter, va, vb, vc));
        count++;
    }

    // One bucket = one world plane. Projects it to 2D and finds the cross-node pairs that
    // genuinely overlap there — an AABB touch is not an overlap (verification.md rule 9, which
    // has already cost this bug two wrong diagnoses), so the area is clipped for real.
    private static int Pair(List<Tri> tris, Dictionary<int, HashSet<int>> successors)
    {
        if (tris.Count < 2)
            return 0;
        int first = tris[0].Node;
        bool oneNode = true;
        for (int i = 1; i < tris.Count && oneNode; i++)
            oneNode = tris[i].Node == first;
        if (oneNode)
            return 0; // within-mesh order is the surface rank's business, not this one's

        var n = (tris[0].B - tris[0].A).Cross(tris[0].C - tris[0].A).Normalized();
        var u = Mathf.Abs(n.Z) < 0.9f ? n.Cross(Vector3.Back) : n.Cross(Vector3.Right);
        u = u.Normalized();
        var v = n.Cross(u);

        var flat = new List<(int Node, int Priority, bool NoClutter, Vector2 A, Vector2 B, Vector2 C,
            float MinX, float MaxX, float MinY, float MaxY)>(tris.Count);
        foreach (var t in tris)
        {
            var a = new Vector2(t.A.Dot(u), t.A.Dot(v));
            var b = new Vector2(t.B.Dot(u), t.B.Dot(v));
            var c = new Vector2(t.C.Dot(u), t.C.Dot(v));
            flat.Add((t.Node, t.Priority, t.NoClutter, a, b, c,
                Mathf.Min(a.X, Mathf.Min(b.X, c.X)), Mathf.Max(a.X, Mathf.Max(b.X, c.X)),
                Mathf.Min(a.Y, Mathf.Min(b.Y, c.Y)), Mathf.Max(a.Y, Mathf.Max(b.Y, c.Y))));
        }
        flat.Sort((p, q) => p.MinX.CompareTo(q.MinX));

        int found = 0;
        for (int i = 0; i < flat.Count; i++)
        {
            var a = flat[i];
            for (int j = i + 1; j < flat.Count; j++)
            {
                var b = flat[j];
                if (b.MinX >= a.MaxX)
                    break; // sorted by MinX: nothing further along can reach back into a
                if (a.Node == b.Node || a.Priority != b.Priority || a.NoClutter != b.NoClutter)
                    continue;
                if (a.MaxY <= b.MinY || b.MaxY <= a.MinY)
                    continue;
                int lo = Math.Min(a.Node, b.Node);
                int hi = Math.Max(a.Node, b.Node);
                if (successors.TryGetValue(lo, out var already) && already.Contains(hi))
                    continue; // this pair is already an edge; the area does not need re-measuring
                if (Overlap(a.A, a.B, a.C, b.A, b.B, b.C) <= MinOverlapArea)
                    continue;
                if (!successors.TryGetValue(lo, out var set))
                    successors[lo] = set = new HashSet<int>();
                set.Add(hi);
                found++;
            }
        }
        return found;
    }

    // Sutherland-Hodgman: the area of triangle a clipped by triangle b (both convex).
    private static float Overlap(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 b0, Vector2 b1, Vector2 b2)
    {
        Span<Vector2> clipper = stackalloc Vector2[3] { b0, b1, b2 };
        if (Cross(b0, b1, b2) < 0f)
        {
            clipper[1] = b2;
            clipper[2] = b1;
        }
        var poly = new List<Vector2>(8) { a0, a1, a2 };
        var next = new List<Vector2>(8);
        for (int e = 0; e < 3; e++)
        {
            if (poly.Count == 0)
                return 0f;
            var p = clipper[e];
            var q = clipper[(e + 1) % 3];
            next.Clear();
            for (int k = 0; k < poly.Count; k++)
            {
                var cur = poly[k];
                var prev = poly[(k + poly.Count - 1) % poly.Count];
                float sc = (q.X - p.X) * (cur.Y - p.Y) - (q.Y - p.Y) * (cur.X - p.X);
                float sp = (q.X - p.X) * (prev.Y - p.Y) - (q.Y - p.Y) * (prev.X - p.X);
                if (sc >= 0f)
                {
                    if (sp < 0f)
                        next.Add(prev.Lerp(cur, sp / (sp - sc)));
                    next.Add(cur);
                }
                else if (sp >= 0f)
                {
                    next.Add(prev.Lerp(cur, sp / (sp - sc)));
                }
            }
            (poly, next) = (next, poly);
        }
        if (poly.Count < 3)
            return 0f;
        float area = 0f;
        for (int k = 0; k < poly.Count; k++)
        {
            var s = poly[k];
            var t = poly[(k + 1) % poly.Count];
            area += (s.X * t.Y) - (t.X * s.Y);
        }
        return Mathf.Abs(area) * 0.5f;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    // Longest-path layering: rank(b) >= rank(a) + 1 for every edge a -> b. Edges only ever run
    // low index -> high index, so visiting nodes in ascending index order settles each one before
    // it is read — no iteration to a fixed point is needed, and no cycle can exist.
    private static Dictionary<int, int> Layer(Dictionary<int, HashSet<int>> successors)
    {
        var ranks = new Dictionary<int, int>();
        var sources = new List<int>(successors.Keys);
        sources.Sort();
        foreach (int a in sources)
        {
            int ra = ranks.TryGetValue(a, out int r) ? r : ranks[a] = 0;
            foreach (int b in successors[a])
            {
                if (!ranks.TryGetValue(b, out int rb) || rb < ra + 1)
                    ranks[b] = ra + 1;
            }
        }
        return ranks;
    }

    /// <summary>What <see cref="Compute"/> found, for the build log and the tripwire.</summary>
    internal readonly struct Report
    {
        public readonly Dictionary<int, int> Ranks;
        public readonly int Pairs;
        public readonly int MaxRank;
        public readonly int Triangles;
        public readonly int ExcludedRoots;

        public Report(Dictionary<int, int> ranks, int pairs, int maxRank, int triangles, int excludedRoots)
        {
            Ranks = ranks;
            Pairs = pairs;
            MaxRank = maxRank;
            Triangles = triangles;
            ExcludedRoots = excludedRoots;
        }
    }

    private readonly struct Tri
    {
        public readonly int Node;
        public readonly int Priority;
        public readonly bool NoClutter;
        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly Vector3 C;

        public Tri(int node, int priority, bool noClutter, Vector3 a, Vector3 b, Vector3 c)
        {
            Node = node;
            Priority = priority;
            NoClutter = noClutter;
            A = a;
            B = b;
            C = c;
        }
    }
}
