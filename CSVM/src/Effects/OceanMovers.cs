using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// Which world nodes an animation carries across the sea, and which of those are hulls. A mover is
/// the target of a played event that changes where a node stands. That is an OBJECT_MOTION_FROM_TO
/// with a translate channel, an SI script, or an OBJECT_TRANSLATE_STATE in a sequence. A hull's
/// origin sits on sea level over sea and its meshes reach the waterline. The mask leaves a hull out,
/// and the ocean calms the waves around it wherever it goes. Debris, reset states and spins are not
/// movers: none carries a hull away from its load pose.
/// </summary>
internal static class OceanMovers
{
    /// <summary>A hull's origin sits within this many metres of sea level; a hoisted lifeboat's does
    /// not.</summary>
    public const float OriginBand = 3f;

    /// <summary>A hull's meshes reach within this many metres of sea level.</summary>
    public const float WaterlineBand = 2f;

    /// <summary>The node names the definition's played sequences move, each once, in event order.
    /// A scoped name gives its last path element.</summary>
    public static List<string> MovedNames(AnimDefinition def)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seq in def.Sequences)
        {
            foreach (var ev in seq.Events)
            {
                foreach (var name in MovedNames(ev))
                {
                    if (seen.Add(name))
                        names.Add(name);
                }
            }
        }
        return names;
    }

    /// <summary>The node names one event moves; empty for an event that leaves every node where it
    /// stands.</summary>
    public static IEnumerable<string> MovedNames(AnimEvent ev)
    {
        switch (ev.Kind)
        {
            case "ObjectMotionFromTo" when ev.Data.Has("translate") || ev.Data.Has("translate_delta"):
            case "ObjectMotionSiScript":
            case "ObjectTranslateState":
                if (Target(ev.Data) is { } name)
                    yield return name;
                break;
            case AnimDefinition.AllNamesKind:
                foreach (var motion in ev.Data.Objects("motions"))
                {
                    if (Target(motion) is { } one)
                        yield return one;
                }
                break;
        }
    }

    /// <summary>Whether a triangle under a mover reaches the waterline.</summary>
    public static bool ReachesWaterline(Vector3 a, Vector3 b, Vector3 c) =>
        Math.Min(a.Y, Math.Min(b.Y, c.Y)) <= WaterlineBand && Math.Max(a.Y, Math.Max(b.Y, c.Y)) >= -WaterlineBand;

    /// <summary>Whether a world X/Z point lies on the triangle's horizontal shadow, edges included.</summary>
    public static bool Covers(Vector3 a, Vector3 b, Vector3 c, Vector2 p)
    {
        float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    /// <summary>The indices of the <paramref name="keep"/> points nearest <paramref name="eye"/>,
    /// in their original order; all of them when there are no more than that.</summary>
    public static List<int> Nearest(IReadOnlyList<Vector3> points, Vector3 eye, int keep)
    {
        var order = new List<int>(points.Count);
        Nearest(points, eye, keep, order);
        return order;
    }

    /// <summary><see cref="Nearest(IReadOnlyList{Vector3}, Vector3, int)"/> into a list the caller
    /// keeps, so the frame path allocates nothing while the slots suffice.</summary>
    public static void Nearest(IReadOnlyList<Vector3> points, Vector3 eye, int keep, List<int> order)
    {
        order.Clear();
        for (int i = 0; i < points.Count; i++)
            order.Add(i);
        if (points.Count <= keep)
            return;
        order.Sort((x, y) =>
        {
            int c = points[x].DistanceSquaredTo(eye).CompareTo(points[y].DistanceSquaredTo(eye));
            return c != 0 ? c : x.CompareTo(y);
        });
        order.RemoveRange(keep, order.Count - keep);
        order.Sort();
    }

    private static string? Target(AnimData data)
    {
        if (data.List("node_path") is { Count: > 0 } path && path[^1] is string leaf)
            return leaf;
        return data.Str("node") ?? data.Str("name");
    }

    private static float Cross(Vector3 a, Vector3 b, Vector2 p) =>
        ((b.X - a.X) * (p.Y - a.Z)) - ((b.Z - a.Z) * (p.X - a.X));
}
