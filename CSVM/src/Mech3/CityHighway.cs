using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The lanes of C5's highway, a deck of <see cref="Paint"/> no city floor covers, for <see cref="CityTraffic"/> to drive.
/// <see cref="Chain"/> joins its pieces into decks by the ends they share. Where a two-way deck splits into two
/// carriageways side by side, each is one-way: the one on the right of travel takes that direction's lanes.
/// Each lane drives from one open end to the other and back (<see cref="Loops"/>), so cars U-turn only where the deck
/// ends. The deck's decode: docs/formats/world-structure.md, C5's highway deck.
/// </summary>
internal static class CityHighway
{
    /// <summary>The highway's paint.</summary>
    public const string Paint = "bridgeroad";

    // TUNE. The deck centre's radius round a bend, its lanes' concentric about it. Then how far inside a deck's open end a
    // U-turn's apex stays, a car's half length.
    private const float TurnM = 40f, EndM = 3f;

    // Two ends whose corners lie this close are one end, and a corner this close to an end's edge lies on it.
    private const float PortM = 0.5f;

    // Two triangles of one piece share a corner's UV, two of neighbouring pieces do not.
    private const float UvSlack = 1e-3f;

    /// <summary>Each lane's share of a two-way deck's width right of its centre, from the paint's lane lines
    /// (docs/formats/world-structure.md, C5's highway deck). A one-way
    /// carriageway is the right half of such a deck, so there a lane lies twice its share across from the left kerb.</summary>
    public static IReadOnlyList<float> Lanes { get; } = new[] { 0.16f, 0.322f };

    /// <summary>Every polygon of the paint reached from world1, its world corners and UVs in order.</summary>
    internal static List<(Vector3[] Corners, Vector2[] Uvs)> Polygons(GameZ gamez)
    {
        var found = new List<(Vector3[], Vector2[])>();
        VegetationGround.WalkWorld(gamez, "world1", (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (poly.TriangleStrip || poly.UvCoords is not { } uvs || uvs.Count < poly.VertexIndices.Count
                    || poly.MaterialIndex < 0 || poly.MaterialIndex >= gamez.Materials.Count
                    || gamez.Materials[poly.MaterialIndex].TextureName is not { } tex || CityGround.Stem(tex) != Paint)
                    continue;
                found.Add((poly.VertexIndices.Select(i => xf * mesh.Vertices[i]).ToArray(), uvs.Take(poly.VertexIndices.Count).ToArray()));
            }
        });
        return found;
    }

    /// <summary>The decks, each the chain of ends its pieces share in order. Every end is its left and right corner for a
    /// car heading along the chain. A piece is a quad, or two triangles sharing an edge and its UVs. Its ends are the two
    /// opposite edges across which V, the paint's width, runs. Then the polygons the decks hold, and the orphans: a
    /// triangle with no partner, or a polygon of another corner count.</summary>
    internal static (List<List<(Vector3 L, Vector3 R)>> Decks, int Polygons, int Orphans) Chain(List<(Vector3[] Corners, Vector2[] Uvs)> polygons)
    {
        bool Near(Vector3 a, Vector3 b) => (a - b).Length() < PortM;
        var pieces = polygons.Where(p => p.Corners.Length == 4).Select(p => (p.Corners, p.Uvs, Polygons: 1)).ToList();
        var triangles = polygons.Where(p => p.Corners.Length == 3).ToList();
        int Corner((Vector3[] Corners, Vector2[] Uvs) t, Vector3 at, Vector2 uv) =>
            Enumerable.Range(0, 3).FirstOrDefault(c => Near(t.Corners[c], at) && (t.Uvs[c] - uv).Length() < UvSlack, -1);
        var paired = new bool[triangles.Count];
        for (int a = 0; a < triangles.Count; a++)
        {
            for (int b = a + 1; b < triangles.Count && !paired[a]; b++)
            {
                if (paired[b])
                    continue;
                var (ta, tb) = (triangles[a], triangles[b]);
                for (int i = 0; i < 3; i++)
                {
                    int j = (i + 1) % 3, k = (i + 2) % 3;
                    int bi = Corner(tb, ta.Corners[i], ta.Uvs[i]), bj = Corner(tb, ta.Corners[j], ta.Uvs[j]);
                    if (bi < 0 || bj < 0)
                        continue;
                    int x = 3 - bi - bj;
                    // Round the quad: the shared edge's far corner, the triangle's third, its near corner, then the other's third.
                    pieces.Add((new[] { ta.Corners[j], ta.Corners[k], ta.Corners[i], tb.Corners[x] }, new[] { ta.Uvs[j], ta.Uvs[k], ta.Uvs[i], tb.Uvs[x] }, 2));
                    paired[a] = paired[b] = true;
                    break;
                }
            }
        }
        int orphans = paired.Count(p => !p) + polygons.Count(p => p.Corners.Length is not (3 or 4));

        var ends = new List<(Vector3 P, Vector3 Q)[]>();
        foreach (var (c, uv, _) in pieces)
        {
            float Across(int e) => MathF.Abs(uv[(e + 1) % 4].Y - uv[e].Y);
            int first = Across(0) + Across(2) >= Across(1) + Across(3) ? 0 : 1;
            ends.Add(new[] { (c[first], c[first + 1]), (c[first + 2], c[(first + 3) % 4]) });
        }
        bool Shared((Vector3 P, Vector3 Q) e, (Vector3 P, Vector3 Q) o) =>
            (Near(e.P, o.P) && Near(e.Q, o.Q)) || (Near(e.P, o.Q) && Near(e.Q, o.P));
        // Each end's one partner on another piece, or none where no end or several meet it.
        var next = new Dictionary<(int Piece, int End), (int Piece, int End)>();
        for (int q = 0; q < ends.Count; q++)
        {
            for (int e = 0; e < 2; e++)
            {
                var meets = Enumerable.Range(0, ends.Count).Where(o => o != q)
                    .SelectMany(o => new[] { (o, 0), (o, 1) }).Where(o => Shared(ends[q][e], ends[o.Item1][o.Item2])).ToList();
                if (meets.Count == 1)
                    next[(q, e)] = meets[0];
            }
        }

        var decks = new List<List<(Vector3 L, Vector3 R)>>();
        var seen = new HashSet<int>();
        for (int start = 0; start < ends.Count; start++)
        {
            int open = !next.ContainsKey((start, 0)) ? 0 : !next.ContainsKey((start, 1)) ? 1 : -1;
            if (open < 0 || seen.Contains(start))
                continue;
            var chain = new List<(Vector3 P, Vector3 Q)> { ends[start][open] };
            var (q, e) = (start, open);
            while (seen.Add(q))
            {
                chain.Add(ends[q][1 - e]);
                if (!next.TryGetValue((q, 1 - e), out var on))
                    break;
                (q, e) = on;
            }
            var mids = chain.Select(p => Flat((p.P + p.Q) / 2f)).ToList();
            decks.Add(chain.Select((p, k) =>
            {
                var along = k + 1 < mids.Count ? mids[k + 1] - mids[k] : mids[k] - mids[k - 1];
                return Flat(p.Q - p.P).Dot(Right(along)) >= 0f ? (p.P, p.Q) : (p.Q, p.P);
            }).ToList());
        }
        return (decks, seen.Sum(p => pieces[p].Polygons), orphans);
    }

    /// <summary>One closed path per lane and pair of open ends. It runs along the lane from one end to the other, through
    /// each split on the carriageway to the right, then back the same way. It U-turns left inside each end. Bends turn
    /// concentric about their deck's centre. <paramref name="unclosed"/> counts the lanes a split or a short piece
    /// leaves without a loop.</summary>
    internal static List<CityTraffic.Path> Loops(List<List<(Vector3 L, Vector3 R)>> decks, out int unclosed)
    {
        (Vector3 L, Vector3 R) EndOf((int Deck, bool Last) e) => e.Last ? decks[e.Deck][^1] : decks[e.Deck][0];
        Vector2 MidOf((int Deck, bool Last) e) => Flat((EndOf(e).L + EndOf(e).R) / 2f);
        bool OnEdge(Vector3 p, (Vector3 L, Vector3 R) edge)
        {
            Vector2 a = Flat(edge.L), ab = Flat(edge.R) - a;
            float t = Math.Clamp((Flat(p) - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
            return (Flat(p) - (a + (ab * t))).Length() < PortM;
        }
        // A carriageway's end lies on a wider deck's end. That end is its whole, and the carriageway one of its halves.
        var all = Enumerable.Range(0, decks.Count).SelectMany(d => new[] { (d, false), (d, true) }).ToList();
        var whole = new Dictionary<(int, bool), (int, bool)>();
        var halves = new Dictionary<(int, bool), List<(int Deck, bool Last)>>();
        foreach (var h in all)
        {
            foreach (var w in all.Where(w => w.Item1 != h.Item1))
            {
                var (he, we) = (EndOf(h), EndOf(w));
                if (!OnEdge(he.L, we) || !OnEdge(he.R, we) || Flat(he.R - he.L).Length() > Flat(we.R - we.L).Length() - PortM)
                    continue;
                whole[h] = w;
                if (!halves.TryGetValue(w, out var list))
                    halves[w] = list = new();
                list.Add(h);
            }
        }
        bool OneWay(int deck) => whole.ContainsKey((deck, false)) || whole.ContainsKey((deck, true));

        // A lane's corners, with its piece's centre, from an open end to the end it arrives at. Null where it meets a split
        // it cannot read, a carriageway's open end, or a deck twice.
        List<(Vector3 At, Vector3 Mid)>? Drive((int Deck, bool Last) from, float share, out (int Deck, bool Last) arrived)
        {
            var corners = new List<(Vector3 At, Vector3 Mid)>();
            var seen = new HashSet<int>();
            var (deck, forward) = (from.Deck, !from.Last);
            arrived = from;
            while (seen.Add(deck))
            {
                bool oneWay = OneWay(deck);
                var chain = forward ? decks[deck] : decks[deck].AsEnumerable().Reverse().Select(e => (L: e.R, R: e.L)).ToList();
                foreach (var (l, r) in chain)
                {
                    var at = l.Lerp(r, oneWay ? 2f * share : 0.5f + share);
                    // Where a carriageway meets its whole the lanes meet too; the later piece's corner stands for both.
                    if (corners.Count > 0 && (corners[^1].At - at).Length() < PortM)
                        corners.RemoveAt(corners.Count - 1);
                    corners.Add((at, (l + r) / 2f));
                }
                var end = (deck, forward);
                arrived = end;
                if (halves.TryGetValue(end, out var split))
                {
                    var travel = Flat(corners[^1].At - corners[^2].At);
                    var onRight = split.Where(h => (MidOf(h) - MidOf(end)).Dot(Right(travel)) > 0f).ToList();
                    if (split.Count != 2 || onRight.Count != 1)
                        return null;
                    (deck, forward) = (onRight[0].Deck, !onRight[0].Last);
                }
                else if (whole.TryGetValue(end, out var w))
                    (deck, forward) = (w.Item1, !w.Item2);
                else
                    return oneWay ? null : corners;
            }
            return null;
        }

        var loops = new List<CityTraffic.Path>();
        var driven = new HashSet<(int, bool)>();
        unclosed = 0;
        foreach (var start in all)
        {
            if (driven.Contains(start) || halves.ContainsKey(start) || OneWay(start.Item1))
                continue;
            driven.Add(start);
            foreach (float share in Lanes)
            {
                var there = Drive(start, share, out var far);
                var home = far;
                var back = there != null ? Drive(far, share, out home) : null;
                driven.Add(far);
                if (there == null || back == null || home != start || Close(there, back) is not { } path)
                    unclosed++;
                else
                    loops.Add(path);
            }
        }
        return loops;
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    // A car's right in XZ, as Path.Turn takes it.
    private static Vector2 Right(Vector2 dir) => new(-dir.Y, dir.X);

    // Joins the lane there and the lane back by a left U-turn inside each open end. Null where a U-turn or a turn does
    // not fit.
    private static CityTraffic.Path? Close(List<(Vector3 At, Vector3 Mid)> there, List<(Vector3 At, Vector3 Mid)> back)
    {
        // The U-turn from the lane arriving at an open end heading dir to the lane leaving it. It returns the radius, then
        // how far the arriving lane stops short of its last corner and the leaving one starts past its first.
        (float Radius, float Arriving, float Leaving) UTurn(Vector3 arriving, Vector3 leaving, Vector2 dir)
        {
            float apex = MathF.Min(Flat(arriving).Dot(dir), Flat(leaving).Dot(dir)) - EndM;
            float r = Flat(arriving - leaving).Dot(Right(dir)) / 2f;
            return (r, Flat(arriving).Dot(dir) - apex + r, Flat(leaving).Dot(dir) - apex + r);
        }
        var startDir = Flat(there[1].At - there[0].At).Normalized();
        var endDir = Flat(there[^1].At - there[^2].At).Normalized();
        var (r0, to, from) = UTurn(back[^1].At, there[0].At, -startDir);
        var (rn, until, after) = UTurn(there[^1].At, back[0].At, endDir);
        if (r0 <= 0f || rn <= 0f)
            return null;
        var path = new CityTraffic.Path(Flat(there[0].At) + (startDir * from), startDir);
        bool fits = Leg(path, there, from, until);
        path.Turn(-rn, MathF.PI);
        fits &= Leg(path, back, after, to);
        path.Turn(-r0, MathF.PI);
        return fits ? path : null;
    }

    // Drives a lane's corners from `from` past the first to `until` short of the last. A straight climbs between each two,
    // and a turn takes each bend about the centre's radius. A lane right of its piece's centre turns tighter on a right
    // bend, wider on a left one. False where two turns overlap on a straight.
    private static bool Leg(CityTraffic.Path path, List<(Vector3 At, Vector3 Mid)> corners, float from, float until)
    {
        int n = corners.Count;
        var dirs = new Vector2[n - 1];
        var lengths = new float[n - 1];
        for (int k = 0; k < n - 1; k++)
        {
            lengths[k] = Flat(corners[k + 1].At - corners[k].At).Length();
            dirs[k] = Flat(corners[k + 1].At - corners[k].At) / lengths[k];
        }
        var radii = new float[n];
        var angles = new float[n];
        var cuts = new float[n];
        cuts[0] = from;
        cuts[n - 1] = until;
        for (int k = 1; k < n - 1; k++)
        {
            angles[k] = MathF.Acos(Math.Clamp(dirs[k - 1].Dot(dirs[k]), -1f, 1f));
            if (angles[k] < 1e-4f)
                continue;
            float right = MathF.Sign(dirs[k].Dot(Right(dirs[k - 1]))), half = MathF.Tan(angles[k] / 2f);
            float offset = Flat(corners[k].At - corners[k].Mid).Dot(Right(dirs[k - 1]));
            float r = MathF.Min(MathF.Max(1f, TurnM - (right * offset)), 0.45f * MathF.Min(lengths[k - 1], lengths[k]) / half);
            radii[k] = right * r;
            cuts[k] = r * half;
        }
        bool fits = true;
        for (int k = 0; k < n - 1; k++)
        {
            float straight = lengths[k] - cuts[k] - cuts[k + 1];
            fits &= straight >= 0f;
            var (a, b, dir, length) = (corners[k].At, corners[k + 1].At, dirs[k], lengths[k]);
            // Each straight starts on its own lane, so a deck widening under a U-turn or a skipped kink adds no drift.
            path.At = Flat(a) + (dir * cuts[k]);
            path.Dir = dir;
            path.Line(straight, w => Mathf.Lerp(a.Y, b.Y, Math.Clamp((w - Flat(a)).Dot(dir) / length, 0f, 1f)));
            if (k + 1 < n - 1 && radii[k + 1] != 0f)
                path.Turn(radii[k + 1], angles[k + 1]);
            else if (k + 1 < n - 1)
                path.Dir = dirs[k + 1];
        }
        return fits;
    }
}
