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
/// global extents give half-span and length; verts outboard of WingBandFrac ×
/// half-span are wing (split at the widest chord gap into separate slabs, should a
/// plane's outboard geometry cluster fore/aft); the aft part of the plane is tail
/// (fins, stabilizers, twin booms); a narrow central band ahead of the tail is the
/// fuselage. Overlap between the boxes is harmless. All thresholds are TUNE.
///
/// Accepted limit (measured on the Bloodhawk): its nose canards span only 1.63 m
/// while the wing band starts at 2.03 m, so an ~0.8 m canard-tip sliver per side is
/// uncovered. Lowering the band far enough to catch it would pull the long inboard
/// wing-root chord into the full-span wing slab, giving the wingtips ~2.4 m of
/// phantom chord — false crashes are worse than a rare missed canard graze.
/// </summary>
public sealed class PlaneCollider
{
    public readonly record struct Part(string Name, BoxShape3D Shape, Transform3D Local);

    /// <summary>The airframe boxes; Local places each box's center in the
    /// FlightController's frame (the plane model's parent).</summary>
    public IReadOnlyList<Part> Parts { get; }

    /// <summary>One-line description of the boxes for the load log.</summary>
    public string Summary => string.Join(", ",
        Parts.Select(p => $"{p.Name} {p.Shape.Size.X:0.0}×{p.Shape.Size.Y:0.0}×{p.Shape.Size.Z:0.0} m"));

    private PlaneCollider(List<Part> parts) => Parts = parts;

    private const float WingBandFrac = 0.35f;        // |x| beyond this × half-span = wing verts
                                                     // (not lower — see the canard note above)
    private const float TailStartFrac = 0.7f;        // z beyond this × length (nose −Z → tail +Z) = tail verts
    private const float FuselageBandFrac = 0.15f;    // |x| within this × half-span = fuselage verts
    private const float FuselageMinHalfWidth = 0.7f; // m — band floor for narrow-span planes
    private const float WingSplitGap = 2f;           // m of empty chord between wing clusters ⇒ split (canards)
    private const float MinThickness = 0.3f;         // m — floor per box dimension (thin fins/slabs)

    /// <summary>Derives the collision boxes from the built plane model; null if it
    /// has no usable geometry.</summary>
    public static PlaneCollider? Build(Node3D planeRoot)
    {
        var verts = new List<Vector3>();
        Collect(planeRoot, planeRoot.Transform, verts);
        if (verts.Count == 0)
            return null;

        var all = Enclose(verts);
        float halfSpan = Mathf.Max(Mathf.Abs(all.Position.X), Mathf.Abs(all.End.X));
        float length = all.Size.Z;
        if (halfSpan < 0.1f || length < 0.1f)
            return null;

        float tailStartZ = all.Position.Z + TailStartFrac * length;
        float wingBand = WingBandFrac * halfSpan;
        float fuselageHalf = Mathf.Max(FuselageBandFrac * halfSpan, FuselageMinHalfWidth);

        var wing = new List<Vector3>();
        var tail = new List<Vector3>();
        var fuselage = new List<Vector3>();
        foreach (var v in verts)
        {
            if (v.Z >= tailStartZ)
                tail.Add(v); // aft region, full width: fins, stabilizers, twin booms
            if (Mathf.Abs(v.X) >= wingBand)
                wing.Add(v);
            else if (Mathf.Abs(v.X) <= fuselageHalf && v.Z < tailStartZ)
                fuselage.Add(v);
        }

        var parts = new List<Part>();
        AddBox(parts, "fuselage", fuselage);
        AddBox(parts, "tail", tail);
        foreach (var (name, cluster) in WingClusters(wing))
            AddBox(parts, name, cluster);
        return parts.Count > 0 ? new PlaneCollider(parts) : null;
    }

    /// <summary>Splits the wing verts at the widest chord (z) gap: a canard plane's
    /// outboard verts form two clusters (nose canards, aft main wing) that would
    /// otherwise merge into one nose-to-tail slab. The forward cluster is the canard.</summary>
    private static IEnumerable<(string Name, List<Vector3> Verts)> WingClusters(List<Vector3> wing)
    {
        if (wing.Count == 0)
            yield break;
        wing.Sort((a, b) => a.Z.CompareTo(b.Z));
        float bestGap = 0f;
        int splitAt = -1;
        for (int i = 1; i < wing.Count; i++)
        {
            float gap = wing[i].Z - wing[i - 1].Z;
            if (gap > bestGap)
            {
                bestGap = gap;
                splitAt = i;
            }
        }
        if (bestGap < WingSplitGap)
        {
            yield return ("wing", wing);
            yield break;
        }
        yield return ("canard", wing.GetRange(0, splitAt));
        yield return ("wing", wing.GetRange(splitAt, wing.Count - splitAt));
    }

    private static void AddBox(List<Part> parts, string name, List<Vector3> verts)
    {
        if (verts.Count == 0)
            return; // e.g. the autogyro may lack a matching region — tolerate absences
        var box = Enclose(verts);
        var size = box.Size;
        size.X = Mathf.Max(size.X, MinThickness);
        size.Y = Mathf.Max(size.Y, MinThickness);
        size.Z = Mathf.Max(size.Z, MinThickness);
        parts.Add(new Part(name, new BoxShape3D { Size = size },
            new Transform3D(Basis.Identity, box.GetCenter())));
    }

    private static Aabb Enclose(List<Vector3> verts)
    {
        var box = new Aabb(verts[0], Vector3.Zero);
        for (int i = 1; i < verts.Count; i++)
            box = box.Expand(verts[i]);
        return box;
    }

    /// <summary>Gathers every visible mesh triangle vertex, transformed by the
    /// accumulated node transforms (xf already includes node's own).</summary>
    private static void Collect(Node node, Transform3D xf, List<Vector3> verts)
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
                // GetFaces yields triangle surfaces only, so the point-sprite
                // "lights" instances contribute nothing anyway.
                if (mi.Mesh is { } mesh)
                    foreach (var v in mesh.GetFaces())
                        verts.Add(xf * v);
                return;
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D c)
                Collect(c, xf * c.Transform, verts);
            else
                Collect(child, xf, verts);
        }
    }
}
