using System;
using System.Collections.Generic;
using System.Linq;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The flying aircraft's collision silhouette (M2-polish item 8): a handful of
/// plane-frame boxes — fuselage, wing slab(s), tail — that FlightController sweeps
/// along each physics frame's motion (PhysicsDirectSpaceState3D.CastMotion), so a
/// wingtip or tail fin clips a building corner like the original. The old test was a
/// single center-line ray, which let everything but the nose pass through obstacles.
///
/// Built once from the plane model's actual mesh triangles (PropAnimator-style tree
/// walk), transformed into the FlightController's frame. Hidden subtrees (the wing
/// flares) are not part of the airframe; the nose propeller blur discs (PropParts
/// PropMain/PropGhost) are excluded too — they are translucent air, and their disc
/// height would sink the axis-aligned fuselage box's belly line for the whole
/// airframe length. The autogyro's overhead rotor discs (RotorMain/RotorGhost) stay
/// in: the rotor is that plane's wing.
///
/// Classification is geometric, not name-based, so it needs no per-plane data:
/// global extents give half-span and length; triangles outboard of WingBandFrac ×
/// half-span are wing (split at the widest chord gap into separate slabs, should a
/// plane's outboard geometry cluster fore/aft); the aft part of the plane is tail
/// (fins, stabilizers, twin booms); a narrow central band ahead of the tail is the
/// fuselage. Each region's box is then refined by greedy volume-guided splitting
/// (Run-2 item 10a): cut at the axis plane that most shrinks the summed enclosed
/// volume, while a cut still removes a real share of bridged air — so the
/// Bloodhawk's full-span tail slab (the thin wing trailing edge AABB'd together
/// with the tall center fins = an 11.6 × 2.4 m barn door of mostly air) becomes
/// slim fin boxes plus flat outboard strips that hug the geometry, and a biplane's
/// stacked wings separate at a horizontal cut instead of bridging the interplane
/// gap. A well-filled conventional region finds no worthwhile cut and stays one
/// box. Everything is partitioned by whole triangles (a cut assigns each triangle
/// by centroid but the boxes enclose all its corners), so large sparse panels —
/// wings whose vertices sit only at the ribs — never lose surface coverage between
/// boxes. Overlap between the boxes is harmless. All thresholds are TUNE.
/// </summary>
public sealed class PlaneCollider
{
    public readonly record struct Part(string Name, BoxShape3D Shape, Transform3D Local);

    private readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C)
    {
        public Vector3 Centroid => (A + B + C) / 3f;
    }

    /// <summary>The airframe boxes; Local places each box's center in the
    /// FlightController's frame (the plane model's parent).</summary>
    public IReadOnlyList<Part> Parts { get; }

    /// <summary>One-line description of the boxes for the load log.</summary>
    public string Summary => string.Join(", ",
        Parts.Select(p => $"{p.Name} {p.Shape.Size.X:0.0}×{p.Shape.Size.Y:0.0}×{p.Shape.Size.Z:0.0} m"));

    private PlaneCollider(List<Part> parts) => Parts = parts;

    private const float WingBandFrac = 0.35f;        // |x| beyond this × half-span = wing verts
                                                     // (not lower — see the canard note below)
    private const float TailStartFrac = 0.7f;        // z beyond this × length (nose −Z → tail +Z) = tail verts
    private const float FuselageBandFrac = 0.15f;    // |x| within this × half-span = fuselage verts
    private const float FuselageMinHalfWidth = 0.7f; // m — band floor for narrow-span planes
    private const float WingSplitGap = 2f;           // m of empty chord between wing clusters ⇒ split (canards)
    private const float MinThickness = 0.3f;         // m — floor per box dimension (thin fins/slabs)
    private const float VolumeSplitFrac = 0.3f;      // a cut must remove ≥ this share of a box's volume
    private const int MaxBoxes = 8;                  // total box budget per plane
    private const float MinCutWidth = 0.35f;         // m — cut planes keep this far from the cluster rim
    private const int Bins = 64;                     // cut-plane candidates per axis (bin edges)

    // Accepted limit (measured on the Bloodhawk): its nose canards span only 1.63 m
    // while the wing band starts at 2.03 m, so an ~0.8 m canard-tip sliver per side is
    // uncovered. Lowering the band far enough to catch it would pull the long inboard
    // wing-root chord into the full-span wing slab, giving the wingtips ~2.4 m of
    // phantom chord — false crashes are worse than a rare missed canard graze.

    /// <summary>Derives the collision boxes from the built plane model; null if it
    /// has no usable geometry.</summary>
    public static PlaneCollider? Build(Node3D planeRoot)
    {
        var tris = new List<Tri>();
        Collect(planeRoot, planeRoot.Transform, tris);
        if (tris.Count == 0)
            return null;

        var all = Enclose(tris);
        float halfSpan = Mathf.Max(Mathf.Abs(all.Position.X), Mathf.Abs(all.End.X));
        float length = all.Size.Z;
        if (halfSpan < 0.1f || length < 0.1f)
            return null;

        float tailStartZ = all.Position.Z + TailStartFrac * length;
        float wingBand = WingBandFrac * halfSpan;
        float fuselageHalf = Mathf.Max(FuselageBandFrac * halfSpan, FuselageMinHalfWidth);

        // Regions are CLIPPED geometry, not vertex picks: each triangle is cut at
        // the region's boundary planes and only the inside pieces join, so region
        // boxes end exactly at their boundaries — a giant wing-root triangle can't
        // drag the wing box to the centerline just because one corner pokes across.
        // tail = everything aft of tailStartZ (full width: fins, stabilizers, twin
        // booms — outboard pieces sit in both tail and wing, harmless); wing =
        // everything outboard of the wing band; fuselage = the narrow central band
        // ahead of the tail.
        var tail = ClipAxis(tris, 2, tailStartZ, keepGreater: true);
        var wing = ClipAxis(tris, 0, wingBand, keepGreater: true);
        wing.AddRange(ClipAxis(tris, 0, -wingBand, keepGreater: false));
        var fuselage = ClipAxis(
            ClipAxis(ClipAxis(tris, 2, tailStartZ, keepGreater: false),
                0, fuselageHalf, keepGreater: false),
            0, -fuselageHalf, keepGreater: true);

        var clusters = new List<(string Name, List<Tri> Tris)>();
        if (fuselage.Count > 0)
            clusters.Add(("fuselage", fuselage));
        if (tail.Count > 0)
            clusters.Add(("tail", tail));
        clusters.AddRange(WingClusters(wing));
        var parts = new List<Part>();
        foreach (var (name, cluster) in Refine(clusters))
            AddBox(parts, name, cluster);
        return parts.Count > 0 ? new PlaneCollider(parts) : null;
    }

    /// <summary>Splits the wing triangles at the widest chord (z) gap: a canard
    /// plane's outboard geometry forms two clusters (nose canards, aft main wing)
    /// that would otherwise merge into one nose-to-tail slab. The forward cluster is
    /// the canard.</summary>
    private static IEnumerable<(string Name, List<Tri> Tris)> WingClusters(List<Tri> wing)
    {
        if (wing.Count == 0)
            yield break;
        wing.Sort((a, b) => a.Centroid.Z.CompareTo(b.Centroid.Z));
        float bestGap = 0f;
        int splitAt = -1;
        float coveredZ = Mathf.Max(wing[0].A.Z, Mathf.Max(wing[0].B.Z, wing[0].C.Z));
        for (int i = 1; i < wing.Count; i++)
        {
            float minZ = Mathf.Min(wing[i].A.Z, Mathf.Min(wing[i].B.Z, wing[i].C.Z));
            float gap = minZ - coveredZ;
            if (gap > bestGap)
            {
                bestGap = gap;
                splitAt = i;
            }
            coveredZ = Mathf.Max(coveredZ, Mathf.Max(wing[i].A.Z, Mathf.Max(wing[i].B.Z, wing[i].C.Z)));
        }
        if (bestGap < WingSplitGap)
        {
            yield return ("wing", wing);
            yield break;
        }
        yield return ("canard", wing.GetRange(0, splitAt));
        yield return ("wing", wing.GetRange(splitAt, wing.Count - splitAt));
    }

    /// <summary>Greedy volume-guided refinement: repeatedly cut the cluster whose
    /// best cut removes the most enclosed volume (bridged air), until no cut removes
    /// at least VolumeSplitFrac of its box or the MaxBoxes budget is reached.
    /// Cutting the biggest offender first spends the budget where the misfit is
    /// worst.</summary>
    private static List<(string Name, List<Tri> Tris)> Refine(
        List<(string Name, List<Tri> Tris)> clusters)
    {
        var result = new List<(string Name, List<Tri> Tris)>(clusters);
        while (result.Count < MaxBoxes)
        {
            int bestIdx = -1;
            float bestGain = 0f;
            List<List<Tri>>? bestPieces = null;
            for (int i = 0; i < result.Count; i++)
            {
                if (!BestCut(result[i].Tris, out var pieces, out float gain) || gain <= bestGain)
                    continue;
                if (result.Count + pieces.Count - 1 > MaxBoxes)
                    continue; // a triple would bust the budget; a pair may still fit
                bestIdx = i;
                bestGain = gain;
                bestPieces = pieces;
            }
            if (bestIdx < 0)
                break;
            var name = result[bestIdx].Name;
            result.RemoveAt(bestIdx);
            for (int p = 0; p < bestPieces!.Count; p++)
                result.Insert(bestIdx + p, (name, bestPieces[p]));
        }
        return result;
    }

    /// <summary>Finds the axis cut of this cluster that most reduces the summed
    /// volume of the resulting boxes vs the whole box (dimensions clamped to
    /// MinThickness so flat slabs still count area; x = twin fins/booms, y = biplane
    /// wing stacks, z = fin vs boom). Considers one OR two parallel cut planes per
    /// axis — the double cut is what separates bilateral pairs: slicing one Kestrel
    /// tail fin off alone gains nothing because the remainder still holds the other
    /// fin's height, but two cuts drop the middle to the thin stabilizer in a single
    /// decision. Candidate planes are Bins bin edges; triangles bin by centroid while
    /// bins enclose all their corners, so surfaces crossing a cut stay covered (the
    /// pieces overlap a little instead of leaking). An empty middle range (twin booms
    /// bridged over air) yields no piece at all. True when the best cut removes at
    /// least VolumeSplitFrac of the whole and every cut plane is at least MinCutWidth
    /// from the cluster's rim; gain is the removed volume (m³) for cross-cluster
    /// ranking.</summary>
    private static bool BestCut(List<Tri> tris, out List<List<Tri>> pieces, out float gain)
    {
        pieces = null!;
        gain = 0f;
        if (tris.Count < 8)
            return false; // below ~8 triangles a cut just chases mesh noise
        var whole = Enclose(tris);
        float wholeVol = ClampedVolume(whole);
        bool found = false;
        int bestAxis = 0, bestLo = 0, bestHi = 0; // bin-edge cut planes (hi 0 = single cut)
        for (int axis = 0; axis < 3; axis++)
        {
            float min = whole.Position[axis], size = whole.Size[axis];
            if (size < 2f * MinCutWidth)
                continue;
            // bin the triangles by centroid; each bin's AABB encloses full corners
            var binBox = new Aabb[Bins];
            var occupied = new bool[Bins];
            foreach (var t in tris)
            {
                int b = Mathf.Clamp((int)((t.Centroid[axis] - min) / size * Bins), 0, Bins - 1);
                var box = EncloseOne(t);
                binBox[b] = occupied[b] ? binBox[b].Merge(box) : box;
                occupied[b] = true;
            }
            // prefix[k] = box of bins [0, k), suffix[k] = box of bins [k, Bins)
            var prefix = new Aabb[Bins + 1];
            var prefixAny = new bool[Bins + 1];
            var suffix = new Aabb[Bins + 1];
            var suffixAny = new bool[Bins + 1];
            for (int k = 0; k < Bins; k++)
            {
                prefixAny[k + 1] = prefixAny[k] || occupied[k];
                prefix[k + 1] = !occupied[k] ? prefix[k]
                    : prefixAny[k] ? prefix[k].Merge(binBox[k]) : binBox[k];
            }
            for (int k = Bins - 1; k >= 0; k--)
            {
                suffixAny[k] = suffixAny[k + 1] || occupied[k];
                suffix[k] = !occupied[k] ? suffix[k + 1]
                    : suffixAny[k + 1] ? suffix[k + 1].Merge(binBox[k]) : binBox[k];
            }
            bool CutOk(int k) // cut plane at bin edge k, MinCutWidth off the rim
            {
                float at = min + size * k / Bins;
                return at - min >= MinCutWidth && min + size - at >= MinCutWidth;
            }
            // single cut at bin edge k; then for each k a sweep of second cuts j > k
            // whose middle range [k, j) accumulates incrementally
            for (int k = 1; k < Bins; k++)
            {
                if (!CutOk(k))
                    continue;
                if (prefixAny[k] && suffixAny[k])
                {
                    float g = wholeVol - (ClampedVolume(prefix[k]) + ClampedVolume(suffix[k]));
                    if (g > gain)
                    {
                        gain = g;
                        found = true;
                        (bestAxis, bestLo, bestHi) = (axis, k, 0);
                    }
                }
                if (!prefixAny[k])
                    continue; // an empty head piece reduces to a single cut at j
                var mid = default(Aabb);
                bool midAny = false;
                float headVol = ClampedVolume(prefix[k]);
                for (int j = k + 1; j < Bins; j++)
                {
                    if (occupied[j - 1])
                    {
                        mid = midAny ? mid.Merge(binBox[j - 1]) : binBox[j - 1];
                        midAny = true;
                    }
                    if (!CutOk(j) || !suffixAny[j])
                        continue;
                    float g = wholeVol - (headVol + (midAny ? ClampedVolume(mid) : 0f)
                                          + ClampedVolume(suffix[j]));
                    if (g > gain)
                    {
                        gain = g;
                        found = true;
                        (bestAxis, bestLo, bestHi) = (axis, k, j);
                    }
                }
            }
        }
        if (!found || gain < VolumeSplitFrac * wholeVol)
            return false;
        float bMin = whole.Position[bestAxis], bSize = whole.Size[bestAxis];
        var parts = new List<Tri>[3] { new(), new(), new() };
        foreach (var t in tris)
        {
            int b = Mathf.Clamp((int)((t.Centroid[bestAxis] - bMin) / bSize * Bins), 0, Bins - 1);
            parts[b < bestLo ? 0 : bestHi == 0 || b < bestHi ? 1 : 2].Add(t);
        }
        pieces = parts.Where(p => p.Count > 0).ToList();
        return pieces.Count >= 2;
    }

    private static float ClampedVolume(Aabb box) =>
        Mathf.Max(box.Size.X, MinThickness) *
        Mathf.Max(box.Size.Y, MinThickness) *
        Mathf.Max(box.Size.Z, MinThickness);

    private static void AddBox(List<Part> parts, string name, List<Tri> tris)
    {
        if (tris.Count == 0)
            return; // e.g. the autogyro may lack a matching region — tolerate absences
        var box = Enclose(tris);
        var size = box.Size;
        size.X = Mathf.Max(size.X, MinThickness);
        size.Y = Mathf.Max(size.Y, MinThickness);
        size.Z = Mathf.Max(size.Z, MinThickness);
        parts.Add(new Part(name, new BoxShape3D { Size = size },
            new Transform3D(Basis.Identity, box.GetCenter())));
    }

    /// <summary>Clips every triangle against an axis-aligned plane (axis 0=x, 2=z),
    /// keeping the pieces on the requested side — Sutherland–Hodgman against one
    /// plane, re-fanned into triangles. Degenerate slivers are dropped.</summary>
    private static List<Tri> ClipAxis(List<Tri> tris, int axis, float plane, bool keepGreater)
    {
        var result = new List<Tri>();
        Span<Vector3> poly = stackalloc Vector3[4];
        foreach (var t in tris)
        {
            int n = 0;
            Span<Vector3> corners = stackalloc[] { t.A, t.B, t.C };
            for (int i = 0; i < 3; i++)
            {
                var cur = corners[i];
                var next = corners[(i + 1) % 3];
                float dCur = keepGreater ? cur[axis] - plane : plane - cur[axis];
                float dNext = keepGreater ? next[axis] - plane : plane - next[axis];
                if (dCur >= 0f)
                    poly[n++] = cur;
                if (dCur > 0f != dNext > 0f && Mathf.Abs(dCur - dNext) > 1e-6f)
                    poly[n++] = cur.Lerp(next, dCur / (dCur - dNext));
            }
            for (int i = 2; i < n; i++)
            {
                var tri = new Tri(poly[0], poly[i - 1], poly[i]);
                if ((tri.B - tri.A).Cross(tri.C - tri.A).LengthSquared() > 1e-8f)
                    result.Add(tri);
            }
        }
        return result;
    }

    private static Aabb EncloseOne(Tri t) =>
        new Aabb(t.A, Vector3.Zero).Expand(t.B).Expand(t.C);

    private static Aabb Enclose(List<Tri> tris)
    {
        var box = EncloseOne(tris[0]);
        for (int i = 1; i < tris.Count; i++)
            box = box.Merge(EncloseOne(tris[i]));
        return box;
    }

    /// <summary>Gathers every visible mesh triangle, transformed by the accumulated
    /// node transforms (xf already includes node's own).</summary>
    private static void Collect(Node node, Transform3D xf, List<Tri> tris)
    {
        if (node is Node3D n3d)
        {
            if (!n3d.Visible)
                return; // hidden subtrees (wing flares) are not part of the airframe
            var kind = PropParts.Classify(n3d.Name);
            if (kind is PropParts.Kind.PropMain or PropParts.Kind.PropGhost)
                return; // translucent nose blur discs (see class doc)
            if (n3d is MeshInstance3D mi)
            {
                // GetFaces yields triangle surfaces only (3 verts per face), so the
                // point-sprite "lights" instances contribute nothing anyway.
                if (mi.Mesh is { } mesh)
                {
                    var faces = mesh.GetFaces();
                    for (int i = 0; i + 2 < faces.Length; i += 3)
                        tris.Add(new Tri(xf * faces[i], xf * faces[i + 1], xf * faces[i + 2]));
                }
                return;
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D c)
                Collect(c, xf * c.Transform, tris);
            else
                Collect(child, xf, tris);
        }
    }
}
