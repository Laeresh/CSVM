using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>Which condition volume an approach node carries, named by the child node the trigger
/// looks for under it.</summary>
public enum ApproachShape
{
    /// <summary>A right circular cone, apex at the target, opening along the approach
    /// direction.</summary>
    Cone,

    /// <summary>A cone cut by the plane through its axis: the half the authored triangle's own
    /// perpendicular points into.</summary>
    HalfCone,

    /// <summary>A ball around the target point.</summary>
    Sphere,
}

/// <summary>A chapter's <c>landings.zrd</c>: the table that starts a mid-mission cutscene when the
/// player flies an authored approach. Each row names an animation and a world node; the node
/// carries a <c>cone</c>, <c>half_cone</c> or <c>sphere</c> child whose single authored triangle IS
/// the condition volume, and a <c>land_on</c> child the mission script activates to arm the row.
/// Resolving is done against the gamez, so a row the mission cannot satisfy is dropped here rather
/// than tested every frame. Decode: docs/formats/anim-definitions/cutscenes.md.</summary>
public static class LandingApproaches
{
    /// <summary>The reader file every chapter ships.</summary>
    public const string FileName = "landings.json";

    /// <summary>The child node whose active bit arms a row: the mission script's own
    /// <c>enable_dropoff</c>/<c>disable_dropoff</c> definitions write it.</summary>
    public const string ArmNode = "land_on";

    // The row's speed band is authored in mph; the record holds m/s.
    private const float MphToMps = 0.44704f;

    private static readonly (string Name, ApproachShape Shape)[] ShapeNodes =
    {
        ("cone", ApproachShape.Cone),
        ("half_cone", ApproachShape.HalfCone),
        ("sphere", ApproachShape.Sphere),
    };

    /// <summary>The rows of <paramref name="chapterZrdrPath"/> this world can actually run:
    /// <paramref name="carriesAnim"/> drops a row whose animation the mission does not carry,
    /// which is the original's own load-time rejection and the reason an Instant Action mission
    /// arms none of them. A missing or unreadable file resolves to nothing.</summary>
    public static List<LandingApproach> Resolve(
        string chapterZrdrPath, GameZ gamez, Func<string, bool> carriesAnim)
    {
        var resolved = new List<LandingApproach>();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFileOrEmpty(chapterZrdrPath, FileName);
        }
        catch (Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException)
        {
            return resolved;
        }

        foreach (var entry in root)
        {
            if (entry is not List<object?> fields)
            {
                continue;
            }

            var row = ZrdrDict.FromAlternating(fields);
            if (row.Str("anim") is not { } anim || row.Str("node") is not { } node
                || !carriesAnim(anim) || gamez.FindByName(node) is not { } approach)
            {
                continue;
            }

            if (Build(gamez, approach, anim, node, row) is { } built)
            {
                resolved.Add(built);
            }
        }

        return resolved;
    }

    /// <summary>The rotation between two orientations, radians, the shortest way round: the
    /// original takes the quaternion's own log map, so a rolled aircraft is off its approach by the
    /// roll as much as by the heading.</summary>
    public static float AngleBetween(Basis a, Basis b)
    {
        float dot = Mathf.Abs(a.GetRotationQuaternion().Dot(b.GetRotationQuaternion()));
        return 2f * Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
    }

    // The volume, in the approach node's own frame: the shape child's single triangle, put through
    // whatever static transforms sit between the approach node and it. The original re-reads the
    // shape node's world pose every frame; nothing in the install animates one, so folding the
    // chain in here costs nothing and leaves one node for the runtime to follow.
    private static LandingApproach? Build(
        GameZ gamez, GameZNode approach, string anim, string node, ZrdrDict row)
    {
        if (FindShape(gamez, approach, out var shape, out var toApproach) is not { } kind
            || shape.MeshIndex < 0 || shape.MeshIndex >= gamez.Meshes.Count)
        {
            return null;
        }

        var mesh = gamez.Meshes[shape.MeshIndex];
        if (mesh.Vertices.Count != 3 || mesh.Polygons.Count != 1)
        {
            return null;
        }

        var apex = toApproach * mesh.Vertices[0];
        var basis = toApproach * mesh.Vertices[1];
        var rim = toApproach * mesh.Vertices[2];
        var axis = (basis - apex).Normalized();
        var perpendicular = rim - basis;
        var speed = row.List("speed");
        return new LandingApproach
        {
            Anim = anim,
            Node = node,
            Shape = kind,
            Auto = row.Has("auto"),
            AngleRad = row.TryFloat("angle", out float degrees)
                ? Mathf.DegToRad(degrees)
                : float.PositiveInfinity,
            MinSpeedMps = speed is { Count: 2 } && speed[0] is float min
                ? min * MphToMps
                : float.MinValue,
            MaxSpeedMps = speed is { Count: 2 } && speed[1] is float max
                ? max * MphToMps
                : float.MaxValue,
            Apex = apex,
            BaseCentre = basis,
            Axis = axis,
            HalfNormal = perpendicular.Normalized(),
            Radius = kind == ApproachShape.Sphere
                ? apex.DistanceTo(basis)
                : perpendicular.Length(),
        };
    }

    // Depth-first for the first cone/half_cone/sphere under the approach node, accumulating the
    // local transforms on the way down.
    private static ApproachShape? FindShape(
        GameZ gamez, GameZNode approach, out GameZNode shape, out Transform3D toApproach)
    {
        var stack = new Stack<(int Index, Transform3D Local)>();
        foreach (int child in approach.Children)
        {
            stack.Push((child, Transform3D.Identity));
        }

        while (stack.Count > 0)
        {
            var (index, parent) = stack.Pop();
            if (index < 0 || index >= gamez.Nodes.Count)
            {
                continue;
            }

            var current = gamez.Nodes[index];
            var local = parent * (current.Local ?? Transform3D.Identity);
            foreach (var (name, kind) in ShapeNodes)
            {
                if (string.Equals(current.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    shape = current;
                    toApproach = local;
                    return kind;
                }
            }

            foreach (int child in current.Children)
            {
                stack.Push((child, local));
            }
        }

        shape = null!;
        toApproach = Transform3D.Identity;
        return null;
    }
}

/// <summary>One resolved <c>landings.zrd</c> row: the animation to start when the player flies the
/// approach, the world node whose frame the test is made in, the condition volume in that node's
/// own frame, and the attitude and speed bands the row authors. Decode:
/// docs/formats/anim-definitions/cutscenes.md.</summary>
public sealed class LandingApproach
{
    /// <summary>The <c>ANIMATION_NAME</c> the row starts, or offers as an auto-land.</summary>
    public required string Anim { get; init; }

    /// <summary>The approach node, whose world pose is both the volume's frame and the attitude
    /// the player is measured against.</summary>
    public required string Node { get; init; }

    /// <summary>Which condition volume the node carries.</summary>
    public required ApproachShape Shape { get; init; }

    /// <summary>An <c>auto</c> row: passing it offers the auto-land, it never starts the
    /// animation itself.</summary>
    public bool Auto { get; init; }

    /// <summary>Largest rotation between the player's attitude and the node's that still passes,
    /// radians. Infinity when the row authors no <c>angle</c>.</summary>
    public float AngleRad { get; init; } = float.PositiveInfinity;

    /// <summary>The <c>speed</c> band's floor, m/s (the row authors mph).</summary>
    public float MinSpeedMps { get; init; } = float.MinValue;

    /// <summary>The <c>speed</c> band's ceiling, m/s.</summary>
    public float MaxSpeedMps { get; init; } = float.MaxValue;

    /// <summary>Cone apex, and sphere centre, in the approach node's frame.</summary>
    public Vector3 Apex { get; init; }

    /// <summary>Centre of the cone's base disc, in the approach node's frame.</summary>
    public Vector3 BaseCentre { get; init; }

    /// <summary>Unit vector from apex to base centre.</summary>
    public Vector3 Axis { get; init; }

    /// <summary>Which half a <see cref="ApproachShape.HalfCone"/> keeps: the authored triangle's
    /// own perpendicular, normalised.</summary>
    public Vector3 HalfNormal { get; init; }

    /// <summary>Base-disc radius, and the sphere's radius.</summary>
    public float Radius { get; init; }

    /// <summary>Is <paramref name="local"/>, a point in the approach node's own frame, inside the
    /// condition volume?</summary>
    public bool Contains(Vector3 local)
    {
        if (Shape == ApproachShape.Sphere)
        {
            return local.DistanceTo(Apex) < Radius;
        }

        var offset = local - BaseCentre;
        if (Shape == ApproachShape.HalfCone && offset.Dot(HalfNormal) < 0f)
        {
            return false;
        }

        var span = Apex - BaseCentre;
        float along = Axis.Dot(span);
        if (along == 0f)
        {
            return false;
        }

        // Parameter along the axis: 0 at the base disc, 1 at the apex, where the radius runs out.
        float t = Axis.Dot(offset) / along;
        if (t < 0f || t > 1f)
        {
            return false;
        }

        float radius = (1f - t) * Radius;
        return (offset - (span * t)).LengthSquared() <= radius * radius;
    }

    /// <summary>Does <paramref name="speedMps"/> sit in the row's authored band?</summary>
    public bool SpeedInBand(float speedMps) => speedMps >= MinSpeedMps && speedMps <= MaxSpeedMps;
}
