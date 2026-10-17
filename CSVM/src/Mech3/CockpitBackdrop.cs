using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The stereo interior's dark backdrop: the instrument panel extruded away from the authored eye
/// into a solid. Each panel polygon becomes a cone from the eye, reaching back from its own depth
/// toward one plane behind the panel (<see cref="Back"/>). From the authored eye a cone hides behind
/// its polygon, so nothing the flat game shows changes. A tracked head that sees past a panel edge
/// finds the cone's dark side, not the world. It writes the interior's stencil, so the whiteout leaves
/// it clear, and the gauges' own bounds choose the panel.
/// </summary>
public static class CockpitBackdrop
{
    /// <summary>How far behind the panel's deepest polygon the cones end, in interior units. TUNE: a
    /// dial's recess and a protruding centre stack's open sides close against it. Deeper, a leaning
    /// head sees the panel's top edge smeared sideways by the extra depth.</summary>
    public const float BackMargin = 1.5f;

    /// <summary>How far past the gauges' widest side the panel reaches, in interior units. It stops
    /// short of the cockpit's side coamings, whose cones a leaning head would see past.</summary>
    public const float ReachX = 3f;

    /// <summary>How far before and behind the gauges' depth range a panel polygon may lie, in
    /// interior units. Long canopy members and tub walls leave it.</summary>
    public const float SlabZ = 4f;

    /// <summary>How far below the gauges' lowest point the panel reaches, in interior units. The tub's
    /// floor, which nothing can be seen past, stays out.</summary>
    public const float BelowY = 6f;

    /// <summary>The highest draw priority a panel polygon may carry. The decals above it lie on what
    /// is already extruded.</summary>
    public const int MaxPriority = 1;

    /// <summary>How squarely a panel polygon must face the eye (<see cref="Facing"/>). A cone behind a
    /// culled polygon would show to the authored eye. One behind a grazing sill or rim shows its side
    /// to an eye half an IPD off the head. A leaning head would see it cover the view past the panel.</summary>
    public const float MinFacing = 0.9f;

    /// <summary>The furthest a cone reaches, as a multiple of its corner's distance from the eye. It
    /// still closes a centre stack standing 30 % nearer than the panel behind it. It also bounds how far
    /// a deep panel's cones smear sideways for a leaning head.</summary>
    public const float MaxReach = 1.35f;

    /// <summary>The backdrop's colour, near black: the panel's unlit back, and darker than every dash.</summary>
    public static readonly Color Colour = new(0.03f, 0.03f, 0.035f);

    private static Shader? _shader;

    /// <summary>The panel polygons of the interior rooted at <paramref name="interior"/>, as
    /// triangles in that node's frame (eye at the origin, view down -Z). A panel polygon lies in
    /// <see cref="InPanel"/>'s box, is drawn opaque, and is neither a gauge nor a driven state, both
    /// of which move. Empty when the interior has no <c>gauges</c> group.
    /// <paramref name="seeThrough"/> names the textures the interior draws see-through.</summary>
    public static List<Vector3> Panel(GameZ gamez, GameZNode interior, IReadOnlySet<GameZPolygon>? omitted,
        Func<string, bool> seeThrough)
    {
        var polygons = new List<(Vector3[] Corners, GameZPolygon Polygon, bool Panel)>();
        Aabb? gauges = null;
        foreach (int child in interior.Children)
        {
            Walk(gamez, child, Transform3D.Identity, false, polygons, ref gauges, seeThrough);
        }

        var triangles = new List<Vector3>();
        if (gauges is not { } g)
        {
            return triangles;
        }

        foreach (var (corners, polygon, panel) in polygons)
        {
            if (panel && omitted?.Contains(polygon) != true && polygon.Priority <= MaxPriority && InPanel(g, corners)
                && (polygon.ShowBackface ? Mathf.Abs(Facing(corners)) : Facing(corners)) >= MinFacing)
            {
                Triangulate(corners, polygon.TriangleStrip, triangles);
            }
        }

        return triangles;
    }

    /// <summary>Whether every one of <paramref name="corners"/> lies in the panel box around the
    /// gauges' bounds <paramref name="gauges"/>. The box stops at the eye's height and reaches
    /// <see cref="ReachX"/>, <see cref="SlabZ"/> and <see cref="BelowY"/> past the gauges.</summary>
    public static bool InPanel(Aabb gauges, IReadOnlyList<Vector3> corners)
    {
        float halfWidth = Mathf.Max(Mathf.Abs(gauges.Position.X), Mathf.Abs(gauges.End.X)) + ReachX;
        float near = Mathf.Min(gauges.End.Z + SlabZ, 0f);
        foreach (var c in corners)
        {
            if (c.Y > 0f || c.Y < gauges.Position.Y - BelowY || Mathf.Abs(c.X) > halfWidth
                || c.Z < gauges.Position.Z - SlabZ || c.Z > near)
            {
                return false;
            }
        }

        return corners.Count >= 3;
    }

    /// <summary>The plane the cones of <paramref name="triangles"/> end on: <see cref="BackMargin"/>
    /// behind the deepest corner, as a z in the interior's frame.</summary>
    public static float Back(IReadOnlyList<Vector3> triangles)
    {
        float deepest = 0f;
        foreach (var v in triangles)
        {
            deepest = Mathf.Min(deepest, v.Z);
        }

        return deepest - BackMargin;
    }

    /// <summary>The solid each triangle of <paramref name="triangles"/> fills away from the eye, with
    /// a far cap and a wall on each edge. A far corner lies on its corner's ray from the eye, on the
    /// plane z = <paramref name="back"/> or at <see cref="MaxReach"/>. The authored eye sees none of it.</summary>
    public static List<Vector3> Extrude(IReadOnlyList<Vector3> triangles, float back)
    {
        var solid = new List<Vector3>(triangles.Count * 7);
        for (int t = 0; t + 2 < triangles.Count; t += 3)
        {
            var near = new[] { triangles[t], triangles[t + 1], triangles[t + 2] };
            var far = new[] { Far(near[0], back), Far(near[1], back), Far(near[2], back) };
            solid.AddRange(far);
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                solid.AddRange(new[] { near[i], near[j], far[j], near[i], far[j], far[i] });
            }
        }

        return solid;
    }

    /// <summary>The backdrop for <paramref name="triangles"/>, to add as a direct child of the
    /// interior's root, or null for none.</summary>
    public static MeshInstance3D? Build(IReadOnlyList<Vector3> triangles)
    {
        if (triangles.Count < 3)
        {
            return null;
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var v in Extrude(triangles, Back(triangles)))
        {
            st.AddVertex(v);
        }

        _shader ??= new Shader { Code = ShaderCode() };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("colour", Colour);
        return new MeshInstance3D
        {
            Name = "cockpit_backdrop",
            Mesh = st.Commit(),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    /// <summary>The backdrop's shader: the stereo interior's placement (SceneBuilder's stereo
    /// variant), unbiased so every interior surface stays in front. Both sides draw, and it writes
    /// the interior's stencil.</summary>
    public static string ShaderCode() =>
        "shader_type spatial;\n"
        + "render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled, fog_disabled;\n"
        + $"stencil_mode write, compare_always, {SceneBuilder.XrInteriorStencil};\n"
        + SceneBuilder.XrInteriorGlobals + "\n"
        + "uniform vec3 colour : source_color;\n"
        + "void vertex() {\n"
        + "    vec3 csky_rel = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;\n"
        + $"    vec3 csky_eye = VIEW_INDEX == 0 ? {SceneBuilder.XrEyeLeftGlobal} : {SceneBuilder.XrEyeRightGlobal};\n"
        + "    VERTEX = mat3(VIEW_MATRIX) * (csky_rel - csky_eye) + EYE_OFFSET;\n"
        + "}\n"
        + "void fragment() {\n"
        + "    ALBEDO = colour;\n"
        + "}\n";

    /// <summary>The cosine between a polygon's drawn side and its direction to the eye at the origin,
    /// negative when the eye sees its back. The Newell normal points out of the drawn side
    /// (<see cref="CockpitPlate"/>'s aft-facing plate is the case the eye never sees).</summary>
    public static float Facing(IReadOnlyList<Vector3> corners)
    {
        var normal = Vector3.Zero;
        for (int i = 0; i < corners.Count; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Count];
            normal += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }

        return normal.Normalized().Dot(-corners[0].Normalized());
    }

    // Where a corner's ray from the eye meets the back plane, but no further than MaxReach.
    private static Vector3 Far(Vector3 v, float back) => v.Z > back ? v * Mathf.Min(back / v.Z, MaxReach) : v;

    // Triangulated as SceneBuilder.EmitPolygon does: a strip's index list is no outline.
    private static void Triangulate(Vector3[] corners, bool strip, List<Vector3> into)
    {
        for (int i = 0; i + 2 < corners.Length; i++)
        {
            into.AddRange(strip
                ? new[] { corners[i], corners[i + 1], corners[i + 2] }
                : new[] { corners[0], corners[i + 1], corners[i + 2] });
        }
    }

    // Collects every drawable polygon with its corners in the interior's frame, and the gauges' bounds.
    // Panel is false for a texture the interior blends and for a gauge. A driven state is skipped.
    private static void Walk(GameZ gamez, int index, Transform3D parent, bool inGauges,
        List<(Vector3[], GameZPolygon, bool)> into, ref Aabb? gauges, Func<string, bool> seeThrough)
    {
        if (index < 0 || index >= gamez.Nodes.Count)
        {
            return;
        }

        var node = gamez.Nodes[index];
        if ((node.Kind == "Lod" && node.LodRangeMin != 0f) || !node.Active || PlaneBuilder.IsInteriorDrivenState(node.Name))
        {
            return;
        }

        inGauges |= node.Name.Equals("gauges", StringComparison.OrdinalIgnoreCase);
        var at = parent * (node.Local ?? Transform3D.Identity);
        if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
        {
            var mesh = gamez.Meshes[node.MeshIndex];
            foreach (var polygon in mesh.Polygons)
            {
                var corners = new Vector3[polygon.VertexIndices.Count];
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = at * mesh.Vertices[polygon.VertexIndices[i]];
                    if (inGauges)
                    {
                        gauges = gauges is { } box ? box.Expand(corners[i]) : new Aabb(corners[i], Vector3.Zero);
                    }
                }

                string? texture = polygon.MaterialIndex >= 0 && polygon.MaterialIndex < gamez.Materials.Count
                    ? gamez.Materials[polygon.MaterialIndex].TextureName
                    : null;
                into.Add((corners, polygon, !inGauges && (texture == null || !seeThrough(texture))));
            }
        }

        foreach (int child in node.Children)
        {
            Walk(gamez, child, at, inGauges, into, ref gauges, seeThrough);
        }
    }
}
