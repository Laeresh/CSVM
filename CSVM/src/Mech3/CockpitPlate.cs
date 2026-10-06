using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The flat head-and-shoulders plate every <c>cockpit1</c> carries just behind the eye, as the
/// polygons that draw it. The plate faces aft, so from the authored eye it is behind the camera and
/// back-facing, and the flat game never shows it. A tracked head leaning aft past it sees its front
/// between the eye and the panel, so the stereo interior leaves it out. It shares its node with
/// canopy structure, which is why the selection is by polygon. Engine-free, so it unit-tests.
/// Survey and counts: docs/architecture/Mech3.md on this file.
/// </summary>
public static class CockpitPlate
{
    /// <summary>How far aft of the eye a plate may lie, in interior units. Ten plates lie 0.16-1.80
    /// units aft and the Warhawk's 6.80. Nothing else in any interior meets the rest of the rule at
    /// any depth.</summary>
    public const float MaxAft = 8f;

    /// <summary>The draw priority every plate polygon carries.</summary>
    public const int Priority = 5;

    // How far a plate vertex may leave the plate's z plane, in interior units.
    private const float PlaneTolerance = 1e-3f;

    /// <summary>The plate polygons of the interior rooted at <paramref name="interior"/>. They are
    /// read in that node's own frame, eye at the origin and the view down -Z. LOD levels the build
    /// skips are skipped here too. Empty when the interior carries no plate.</summary>
    public static HashSet<GameZPolygon> Find(GameZ gamez, GameZNode interior)
    {
        var found = new HashSet<GameZPolygon>();
        foreach (int child in interior.Children)
        {
            Walk(gamez, child, Transform3D.Identity, found);
        }

        return found;
    }

    /// <summary>Whether one polygon, its corners already in the interior's frame, is part of the
    /// plate. Every corner lies on one z plane between the eye and <see cref="MaxAft"/>. The face
    /// normal points aft (+Z), and the polygon has a colour material and <see cref="Priority"/>.</summary>
    public static bool IsPlate(IReadOnlyList<Vector3> corners, GameZPolygon polygon, GameZMaterial? material)
    {
        if (polygon.Priority != Priority || material == null || material.TextureName != null || corners.Count < 3)
        {
            return false;
        }

        float z = corners[0].Z;
        if (z <= 0f || z > MaxAft)
        {
            return false;
        }

        foreach (var corner in corners)
        {
            if (Mathf.Abs(corner.Z - z) > PlaneTolerance)
            {
                return false;
            }
        }

        return Newell(corners).Z > 0f;
    }

    private static void Walk(GameZ gamez, int index, Transform3D parent, HashSet<GameZPolygon> found)
    {
        if (index < 0 || index >= gamez.Nodes.Count)
        {
            return;
        }

        var node = gamez.Nodes[index];
        if (node.Kind == "Lod" && node.LodRangeMin != 0f)
        {
            return;
        }

        var at = parent * (node.Local ?? Transform3D.Identity);
        if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
        {
            var mesh = gamez.Meshes[node.MeshIndex];
            var corners = new List<Vector3>();
            foreach (var polygon in mesh.Polygons)
            {
                corners.Clear();
                foreach (int v in polygon.VertexIndices)
                {
                    corners.Add(at * mesh.Vertices[v]);
                }

                var material = polygon.MaterialIndex >= 0 && polygon.MaterialIndex < gamez.Materials.Count
                    ? gamez.Materials[polygon.MaterialIndex]
                    : null;
                if (IsPlate(corners, polygon, material))
                {
                    found.Add(polygon);
                }
            }
        }

        foreach (int child in node.Children)
        {
            Walk(gamez, child, at, found);
        }
    }

    private static Vector3 Newell(IReadOnlyList<Vector3> corners)
    {
        var normal = Vector3.Zero;
        for (int i = 0; i < corners.Count; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Count];
            normal += new Vector3(
                (a.Y - b.Y) * (a.Z + b.Z),
                (a.Z - b.Z) * (a.X + b.X),
                (a.X - b.X) * (a.Y + b.Y));
        }

        return normal;
    }
}
