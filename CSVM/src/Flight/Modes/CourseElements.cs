using Godot;

namespace CSVM.Flight.Modes;

/// <summary>Which part of a course gate a touch struck. Left and right are as seen flying the gate
/// forward: -X and +X in its frame.</summary>
public enum CourseElement
{
    LeftCoursePylon,
    RightCoursePylon,
    RingRim,
    Balloon,
}

/// <summary>One touch of a course gate's element. Gate is the pool index into <see cref="Course.Gates"/>.
/// Balloon says which balloon, and is 0 for any other element. Point is the world point of the
/// element's surface nearest the plane. PylonHeight is that point's height above a course pylon's
/// base, and 0 for any other element. TimeAdded is false before the first entry and on the finishing step.</summary>
public readonly record struct CourseTouch(
    int Gate, CourseElement Element, int Balloon, Vector3 Point, float PylonHeight, bool TimeAdded);

/// <summary>
/// The touch volumes of a course gate's elements, in the gate's own frame, so they sit wherever
/// <c>Rest * motion</c> draws the gate. Every size is <see cref="CourseGateShape"/>'s, the one the
/// meshes are built from. A course pylon is its tapered cone, a ring rim the tube about its
/// centreline circle, and a balloon its upright-stretched sphere. None of them is a collider.
/// </summary>
public static class CourseElements
{
    /// <summary>Seconds one touch adds to the run, the Red Bull inflatable-pylon rule, not game data.</summary>
    public const float TouchPenaltySeconds = 3f;

    /// <summary>How many elements a gate has. A pylon gate's are its course pylons, 0 left and 1
    /// right. A ring's are its rim, 0, and its balloons, 1 on.</summary>
    public static int Count(CourseGate gate) =>
        gate.Kind == CourseGateKind.PylonGate ? 2 : 1 + CourseGateShape.BalloonAnglesDeg.Length;

    /// <summary>A sphere in the gate's frame holding every element's volume, the per-gate reject
    /// before the element tests.</summary>
    internal static (Vector3 Centre, float Radius) Bounds(CourseGate gate)
    {
        if (gate.Kind == CourseGateKind.PylonGate)
        {
            var half = new Vector2((gate.Width / 2f) + CourseGateShape.PylonBaseRadius(gate), gate.Height / 2f);
            return (new Vector3(0f, gate.Height / 2f, 0f), half.Length());
        }

        float reach = CourseGateShape.TubeCentreline(gate) + CourseGateShape.TubeRadius(gate);
        float balloon = CourseGateShape.BalloonStretch * CourseGateShape.BalloonRadius(gate);
        for (int i = 0; i < CourseGateShape.BalloonAnglesDeg.Length; i++)
        {
            reach = Mathf.Max(reach, CourseGateShape.BalloonCentre(gate, i).Length() + balloon);
        }

        return (Vector3.Zero, reach);
    }

    /// <summary>The point of element <paramref name="element"/>'s volume nearest
    /// <paramref name="local"/>, a point in the gate's frame, and that point itself when inside it.
    /// Element numbers are <see cref="Count"/>'s.</summary>
    internal static Vector3 Nearest(CourseGate gate, int element, Vector3 local)
    {
        if (gate.Kind == CourseGateKind.PylonGate)
        {
            return NearestOnCone(gate, CourseGateShape.PylonBase(gate, element), local);
        }

        if (element > 0)
        {
            return NearestOnBalloon(gate, CourseGateShape.BalloonCentre(gate, element - 1), local);
        }

        float radius = CourseGateShape.TubeCentreline(gate);
        var radial = new Vector2(local.X, local.Y);
        var onCircle = radial.LengthSquared() > 1e-12f ? radial.Normalized() * radius : new Vector2(radius, 0f);
        return ToBall(local, new Vector3(onCircle.X, onCircle.Y, 0f), CourseGateShape.TubeRadius(gate));
    }

    // The solid tapered cone, from the base radius at its foot to the top radius at Height. It is
    // worked in the (radial, height) half plane, where its section is a trapezoid.
    private static Vector3 NearestOnCone(CourseGate gate, Vector3 basePoint, Vector3 local)
    {
        var across = new Vector2(local.X - basePoint.X, local.Z - basePoint.Z);
        float r = across.Length();
        float h = local.Y - basePoint.Y;
        float bottom = CourseGateShape.PylonBaseRadius(gate);
        float top = CourseGateShape.PylonTopRadius(gate);
        float height = gate.Height;
        if (h >= 0f && h <= height && r <= bottom + ((top - bottom) * h / height))
        {
            return local;
        }

        var q = new Vector2(r, h);
        var best = Closer(q, Closest(q, Vector2.Zero, new Vector2(bottom, 0f)), Closest(q, new Vector2(bottom, 0f), new Vector2(top, height)));
        best = Closer(q, best, Closest(q, new Vector2(top, height), new Vector2(0f, height)));
        var way = r > 1e-6f ? across / r : new Vector2(1f, 0f);
        return basePoint + new Vector3(way.X * best.X, best.Y, way.Y * best.X);
    }

    // ponytail: this is the stretched sphere's point toward p in squashed space, not the exact nearest.
    // At stretch 1.15 it is a few percent of the radius off. Solve it exactly if that ever matters.
    private static Vector3 NearestOnBalloon(CourseGate gate, Vector3 centre, Vector3 local)
    {
        var squash = new Vector3(1f, 1f / CourseGateShape.BalloonStretch, 1f);
        var onSphere = ToBall(squash * local, squash * centre, CourseGateShape.BalloonRadius(gate));
        return onSphere / squash;
    }

    private static Vector2 Closer(Vector2 q, Vector2 a, Vector2 b) => a.DistanceSquaredTo(q) <= b.DistanceSquaredTo(q) ? a : b;

    private static Vector2 Closest(Vector2 q, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp((q - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return a + (ab * t);
    }

    // The point of the ball nearest p, p itself when inside.
    private static Vector3 ToBall(Vector3 p, Vector3 centre, float radius)
    {
        var offset = p - centre;
        float distance = offset.Length();
        return distance <= radius ? p : centre + (offset * (radius / distance));
    }
}
