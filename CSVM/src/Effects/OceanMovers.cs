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

    /// <summary>The events of the definition's played sequences that move a node, in event order,
    /// for <see cref="AnimRuntime.TargetsOf"/> to resolve. Its reset state is not played.</summary>
    public static List<AnimEvent> MovingEvents(AnimDefinition def)
    {
        var events = new List<AnimEvent>();
        foreach (var seq in def.Sequences)
        {
            foreach (var ev in seq.Events)
            {
                if (Moves(ev))
                    events.Add(ev);
            }
        }
        return events;
    }

    /// <summary>Whether an event changes where its target stands. An ALL_NAMES SI script does, for
    /// each of its records.</summary>
    public static bool Moves(AnimEvent ev) => ev.Kind switch
    {
        "ObjectMotionFromTo" => ev.Data.Has("translate") || ev.Data.Has("translate_delta"),
        "ObjectMotionSiScript" or "ObjectTranslateState" or AnimDefinition.AllNamesKind => true,
        _ => false,
    };

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

    /// <summary>Fills <paramref name="order"/> with the indices of the <paramref name="keep"/> points
    /// nearest <paramref name="eye"/>, in their original order; all of them when there are no more
    /// than that. The caller keeps the list, so the frame path allocates nothing while the slots
    /// suffice.</summary>
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

    private static float Cross(Vector3 a, Vector3 b, Vector2 p) =>
        ((b.X - a.X) * (p.Y - a.Z)) - ((b.Z - a.Z) * (p.X - a.X));
}
