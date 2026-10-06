using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The gunsight reticle of the stereo interior, drawn collimated as a reflector sight shows it.
/// Each eye sees each line where the authored eye sees it, so the reticle reads at infinity, on a
/// distant target without a double image. Every sight glass is one polygon and one texture,
/// holding the tint, the frame line, the sight body and the reticle together. The split
/// (<see cref="Split"/>) lifts the reticle off, so the glass keeps the rest at its true depth.
/// An overlay in the glass's plane draws the reticle, looking each eye's ray up through the authored
/// eye (<see cref="Look"/>). Survey and measurements: docs/org/cameraViews.md, "The gunsight under a
/// headset".
/// </summary>
public static class GunsightReticle
{
    /// <summary>How far a texel's alpha may stand from the tint's and still be tint. The ringing
    /// beside a line dips to 26 against the tint's 38, and a texel further off joins the reticle.</summary>
    public const int TintTolerance = 6;

    /// <summary>The add gain on the reticle's colour under Enhanced, whose AgX tonemap compresses the
    /// add that Original's linear one leaves as it is. TUNE: chosen so the reticle reads over the sky
    /// at least as strongly as the flat cockpit pass's; measured in docs/org/cameraViews.md.</summary>
    public const float EnhancedGain = 6.0f;

    /// <summary>How far the overlay reaches past the glass's edge, in metres: half of a 100 mm IPD.
    /// An eye beside the head centre still finds every line the head centre sees.</summary>
    public const float EyeReachM = 0.05f;

    /// <summary>The overlay's sorting offset, metres of sort depth over the glass's. The two share
    /// a blend priority, and a lifted interior ties every geometry's sort depth. Without the offset
    /// the renderer picks their order, and any other transparent can swap it. It must exceed the
    /// interior's own depth extent.</summary>
    public const float SortAfterGlassM = 10f;

    // The two names the one sight-glass texture ships under, the Warhawk's being crosshair.
    private static readonly string[] GlassTextures = { "cockpit6", "crosshair" };

    private static ModeShader? _shader;

    /// <summary>Whether <paramref name="textureName"/> is the sight glass's texture, extension or not.</summary>
    public static bool IsGlass(string textureName)
    {
        string stem = System.IO.Path.GetFileNameWithoutExtension(textureName);
        return Array.Exists(GlassTextures, g => g.Equals(stem, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Splits an RGBA8 glass texture into its two layers. Returns the reticle's texel count,
    /// zero (and both layers null) when the texture holds none. The glass layer has each reticle
    /// texel repainted with the nearest tint's colour at the tint's alpha. The reticle layer keeps
    /// the reticle texels and clears the rest, carrying the nearest reticle colour so filtering
    /// grows no dark fringe. Engine-free, so it unit-tests.</summary>
    public static int Split(byte[] rgba, int width, int height, out byte[]? glass, out byte[]? reticle, out byte tint)
    {
        var mask = ReticleMask(rgba, width, height, out tint);
        int count = 0;
        foreach (bool m in mask)
        {
            count += m ? 1 : 0;
        }

        glass = reticle = null;
        if (count == 0)
        {
            return 0;
        }

        glass = (byte[])rgba.Clone();
        reticle = (byte[])rgba.Clone();
        var fromTint = Nearest(mask, width, height, fromMarked: false);
        var fromReticle = Nearest(mask, width, height, fromMarked: true);
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i])
            {
                CopyColour(rgba, fromTint[i], glass, i);
                glass[(4 * i) + 3] = tint;
            }
            else
            {
                CopyColour(rgba, fromReticle[i], reticle, i);
                reticle[(4 * i) + 3] = 0;
            }
        }

        return count;
    }

    /// <summary>Which texels are the reticle. The tint is the commonest alpha, and a texel off it by
    /// more than <see cref="TintTolerance"/> is marked, except on the outermost ring, the frame line.
    /// Marked texels group 8-connected; the reticle is the group nearest the centre and every group
    /// clear of the second ring. The frame corners and the sight body reach that ring.</summary>
    public static bool[] ReticleMask(byte[] rgba, int width, int height, out byte tint)
    {
        int n = width * height;
        var histogram = new int[256];
        for (int i = 0; i < n; i++)
        {
            histogram[rgba[(4 * i) + 3]]++;
        }

        tint = (byte)Array.IndexOf(histogram, Max(histogram));
        var marked = new bool[n];
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int i = (y * width) + x;
                marked[i] = Math.Abs(rgba[(4 * i) + 3] - tint) > TintTolerance;
            }
        }

        var group = new int[n];
        Array.Fill(group, -1);
        var reachesEdge = new List<bool>();
        int seed = -1;
        float seedDistance = float.MaxValue;
        var queue = new Queue<int>();
        for (int start = 0; start < n; start++)
        {
            if (!marked[start] || group[start] >= 0)
            {
                continue;
            }

            int id = reachesEdge.Count;
            bool edge = false;
            group[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % width;
                int y = i / width;
                edge |= x <= 1 || y <= 1 || x >= width - 2 || y >= height - 2;
                float d = Sq(x - ((width - 1) / 2f)) + Sq(y - ((height - 1) / 2f));
                if (d < seedDistance)
                {
                    (seedDistance, seed) = (d, id);
                }

                foreach (int j in Neighbours(x, y, width, height))
                {
                    if (marked[j] && group[j] < 0)
                    {
                        group[j] = id;
                        queue.Enqueue(j);
                    }
                }
            }

            reachesEdge.Add(edge);
        }

        var mask = new bool[n];
        for (int i = 0; i < n; i++)
        {
            mask[i] = group[i] >= 0 && (group[i] == seed || !reachesEdge[group[i]]);
        }

        return mask;
    }

    /// <summary>The overlay that draws <paramref name="glass"/>'s reticle collimated, in the
    /// interior's own frame, to add as a direct child of the interior's root. Null when the glass
    /// polygon is degenerate. It lies in the glass's plane, <paramref name="margin"/> units wider
    /// each way, for an eye half an IPD off the head. The bias, <paramref name="depthBias"/>, is the
    /// glass's own pull toward the eye, so the overlay hides where the glass hides.</summary>
    public static MeshInstance3D? Overlay(Glass glass, Layers layers, float margin, float depthBias)
    {
        if (Look.For(glass.Corners, glass.Uvs) is not { } look)
        {
            return null;
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var pane = Pane(glass.Corners, look.N, margin);
        foreach (int k in new[] { 0, 1, 2, 0, 2, 3 })
        {
            st.AddVertex(pane[k]);
        }

        _shader ??= ShaderTwins.Make(ShaderCode, "gunsight-reticle", "gunsight-reticle");
        var material = ShaderTwins.Follow(new ShaderMaterial(), _shader);
        material.RenderPriority = SceneBuilder.XrInteriorBlendPriority;
        material.SetShaderParameter("reticle_tex", layers.Reticle);
        material.SetShaderParameter("glass_tex", layers.Glass);
        material.SetShaderParameter("tint_alpha", layers.TintAlpha);
        material.SetShaderParameter("grad_u", look.GradU);
        material.SetShaderParameter("grad_v", look.GradV);
        material.SetShaderParameter("uv_origin", look.Origin);
        material.SetShaderParameter("plane_n", look.N);
        material.SetShaderParameter("plane_h", look.H);
        material.SetShaderParameter("depth_bias", depthBias);
        return new MeshInstance3D
        {
            Name = "gunsight_reticle",
            Mesh = st.Commit(),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            SortingOffset = SortAfterGlassM,
        };
    }

    /// <summary>The sight glass polygons of the interior rooted at <paramref name="interior"/>, read
    /// in that node's own frame (eye at the origin, view down -Z). LOD levels the build skips are
    /// skipped here too.</summary>
    public static List<Glass> Find(GameZ gamez, GameZNode interior)
    {
        var found = new List<Glass>();
        foreach (int child in interior.Children)
        {
            Walk(gamez, child, Transform3D.Identity, found);
        }

        return found;
    }

    /// <summary>Godot's straight-alpha <see cref="Image"/> of <paramref name="source"/> split into its
    /// glass and reticle layers, each with a box-filtered mip chain. Null when it holds no reticle,
    /// or no pixels.</summary>
    public static Layers? SplitTexture(ImageTexture source)
    {
        // A renderer that keeps no pixels (headless) hands back no picture to split.
        if (source.GetImage() is not { } image || image.IsEmpty())
        {
            return null;
        }

        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.ClearMipmaps();
        image.Convert(Image.Format.Rgba8);
        int w = image.GetWidth();
        int h = image.GetHeight();
        if (Split(image.GetData(), w, h, out var glass, out var reticle, out byte tint) == 0)
        {
            return null;
        }

        return new Layers(Texture(glass!, w, h), Texture(reticle!, w, h), tint / 255f);
    }

    // The overlay's shader. The placement is the stereo interior's (SceneBuilder's stereo variant).
    // Each eye's ray takes the texel the authored eye sees along it, so both eyes agree on direction.
    // It shows where the head centre looks through the glass along that ray, one window for both eyes.
    // The opaque sight body at the fragment's own place on the glass covers it.
    // No sample sits under a branch, for the mip slope.
    private static string ShaderCode() =>
        "shader_type spatial;\n"
        + "render_mode skip_vertex_transform, blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;\n"
        + SceneBuilder.XrInteriorGlobals + "\n"
        + "uniform sampler2D reticle_tex : source_color, filter_linear_mipmap, repeat_disable;\n"
        + "uniform sampler2D glass_tex : source_color, filter_linear_mipmap, repeat_disable;\n"
        + "uniform float tint_alpha = 0.15;\n"
        + "uniform vec3 grad_u;\nuniform vec3 grad_v;\nuniform vec2 uv_origin;\n"
        + "uniform vec3 plane_n;\nuniform float plane_h;\n"
        + "uniform float depth_bias = 0.0;\n"
        + "varying vec3 v_ray;\nvarying vec2 v_own;\nvarying vec3 v_head;\n"
        + "float csky_inside(vec2 uv) { return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0); }\n"
        + "void vertex() {\n"
        + "    vec3 csky_rel = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;\n"
        + $"    vec3 csky_eye = VIEW_INDEX == 0 ? {SceneBuilder.XrEyeLeftGlobal} : {SceneBuilder.XrEyeRightGlobal};\n"
        + "    mat4 to_local = inverse(MODEL_MATRIX);\n"
        + "    vec3 ray = VERTEX - (to_local * vec4(csky_eye, 1.0)).xyz;\n"
        + $"    v_head = (to_local * vec4(0.5 * ({SceneBuilder.XrEyeLeftGlobal} + {SceneBuilder.XrEyeRightGlobal}), 1.0)).xyz;\n"
        + "    v_ray = vec3(dot(grad_u, ray), dot(grad_v, ray), dot(plane_n, ray));\n"
        + "    v_own = vec2(dot(grad_u, VERTEX), dot(grad_v, VERTEX)) + uv_origin;\n"
        + "    VERTEX = mat3(VIEW_MATRIX) * (csky_rel - csky_eye) + EYE_OFFSET;\n"
        + "    VERTEX = EYE_OFFSET + (VERTEX - EYE_OFFSET) * (1.0 - depth_bias);\n"
        + "}\n"
        + "void fragment() {\n"
        + "    vec2 r = v_ray.xy / v_ray.z;\n"
        + "    vec2 uv = plane_h * r + uv_origin;\n"
        + "    vec2 seen = (plane_h - dot(plane_n, v_head)) * r + vec2(dot(grad_u, v_head), dot(grad_v, v_head)) + uv_origin;\n"
        + "    vec4 t = texture(reticle_tex, uv);\n"
        + "    float window = csky_inside(uv) * csky_inside(seen) * smoothstep(0.0, tint_alpha, texture(glass_tex, seen).a);\n"
        + "    float body = csky_inside(v_own) * clamp((texture(glass_tex, v_own).a - tint_alpha) / (1.0 - tint_alpha), 0.0, 1.0);\n"
        + "    ALBEDO = t.rgb * " + (GraphicsMode.Enhanced ? EnhancedGain : 1f).ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) + ";\n"
        + "    ALPHA = t.a * window * (1.0 - body);\n"
        + "}\n";

    // The glass polygon's rectangle in its own plane, along its first edge and across it, grown by
    // margin each way, as four corners in order.
    private static Vector3[] Pane(IReadOnlyList<Vector3> corners, Vector3 normal, float margin)
    {
        var a = (corners[1] - corners[0]).Normalized();
        var b = normal.Normalized().Cross(a);
        float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
        foreach (var corner in corners)
        {
            var at = corner - corners[0];
            (aMin, aMax) = (Mathf.Min(aMin, at.Dot(a)), Mathf.Max(aMax, at.Dot(a)));
            (bMin, bMax) = (Mathf.Min(bMin, at.Dot(b)), Mathf.Max(bMax, at.Dot(b)));
        }

        Vector3 At(float s, float t) => corners[0] + (a * s) + (b * t);
        return new[]
        {
            At(aMin - margin, bMin - margin), At(aMax + margin, bMin - margin),
            At(aMax + margin, bMax + margin), At(aMin - margin, bMax + margin),
        };
    }

    private static ImageTexture Texture(byte[] rgba, int width, int height)
    {
        var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return TextureUpload.Create(image);
    }

    private static void Walk(GameZ gamez, int index, Transform3D parent, List<Glass> found)
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
            foreach (var polygon in mesh.Polygons)
            {
                if (polygon.MaterialIndex < 0 || polygon.MaterialIndex >= gamez.Materials.Count
                    || gamez.Materials[polygon.MaterialIndex].TextureName is not { } name || !IsGlass(name)
                    || polygon.UvCoords is not { } uvs || uvs.Count != polygon.VertexIndices.Count)
                {
                    continue;
                }

                var corners = new List<Vector3>(polygon.VertexIndices.Count);
                foreach (int v in polygon.VertexIndices)
                {
                    corners.Add(at * mesh.Vertices[v]);
                }

                found.Add(new Glass(corners, uvs, polygon.Priority, name));
            }
        }

        foreach (int child in node.Children)
        {
            Walk(gamez, child, at, found);
        }
    }

    // For every texel, the nearest texel on one side of the mask, marked when fromMarked.
    // A breadth-first walk spreads out of that side, and a texel on that side names itself.
    private static int[] Nearest(bool[] mask, int width, int height, bool fromMarked)
    {
        var source = new int[mask.Length];
        Array.Fill(source, -1);
        var queue = new Queue<int>();
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] == fromMarked)
            {
                source[i] = i;
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            foreach (int j in Neighbours(i % width, i / width, width, height))
            {
                if (source[j] < 0)
                {
                    source[j] = source[i];
                    queue.Enqueue(j);
                }
            }
        }

        return source;
    }

    private static IEnumerable<int> Neighbours(int x, int y, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx;
                int yy = y + dy;
                if ((dx != 0 || dy != 0) && xx >= 0 && yy >= 0 && xx < width && yy < height)
                {
                    yield return (yy * width) + xx;
                }
            }
        }
    }

    private static void CopyColour(byte[] from, int source, byte[] to, int target)
    {
        if (source < 0)
        {
            return;
        }

        to[4 * target] = from[4 * source];
        to[(4 * target) + 1] = from[(4 * source) + 1];
        to[(4 * target) + 2] = from[(4 * source) + 2];
    }

    private static int Max(int[] values)
    {
        int max = 0;
        foreach (int v in values)
        {
            max = Math.Max(max, v);
        }

        return max;
    }

    private static float Sq(float v) => v * v;

    /// <summary>A glass polygon's plane, <c>N·p = H</c>, and its UVs over that plane,
    /// <c>uv = (GradU·p, GradV·p) + Origin</c>. Both are in the interior's frame, whose origin is the
    /// authored eye. Engine-free, so it unit-tests.</summary>
    public readonly record struct Look(Vector3 GradU, Vector3 GradV, Vector2 Origin, Vector3 N, float H)
    {
        /// <summary>The look of a flat polygon whose UVs are affine over it. It is fitted to the three
        /// corners spanning the largest triangle, so a strip's inner corners cannot skew it. Null
        /// when every corner is collinear or the plane passes through the eye.</summary>
        public static Look? For(IReadOnlyList<Vector3> corners, IReadOnlyList<Vector2> uvs)
        {
            int count = Math.Min(corners.Count, uvs.Count);
            if (count < 3)
            {
                return null;
            }

            (int A, int B, int C) best = (0, 1, 2);
            float area = -1f;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    for (int k = j + 1; k < count; k++)
                    {
                        float here = (corners[j] - corners[i]).Cross(corners[k] - corners[i]).LengthSquared();
                        (best, area) = here > area ? ((i, j, k), here) : (best, area);
                    }
                }
            }

            var e1 = corners[best.B] - corners[best.A];
            var e2 = corners[best.C] - corners[best.A];
            var n = e1.Cross(e2).Normalized();
            float det = e1.Dot(e2.Cross(n));
            float h = n.Dot(corners[best.A]);
            if (Mathf.Abs(det) < 1e-9f || Mathf.Abs(h) < 1e-6f)
            {
                return null;
            }

            // The UV gradient along the plane: matches both edges' UV steps, nothing along n.
            var step1 = uvs[best.B] - uvs[best.A];
            var step2 = uvs[best.C] - uvs[best.A];
            var gradU = ((step1.X * e2.Cross(n)) + (step2.X * n.Cross(e1))) / det;
            var gradV = ((step1.Y * e2.Cross(n)) + (step2.Y * n.Cross(e1))) / det;
            var origin = uvs[best.A] - new Vector2(gradU.Dot(corners[best.A]), gradV.Dot(corners[best.A]));
            return new Look(gradU, gradV, origin, n, h);
        }

        /// <summary>The UV at <paramref name="point"/>, a point on the plane.</summary>
        public Vector2 At(Vector3 point) => new Vector2(GradU.Dot(point), GradV.Dot(point)) + Origin;

        /// <summary>The UV an eye at <paramref name="eye"/> sees along <paramref name="direction"/>.</summary>
        public Vector2 Seen(Vector3 eye, Vector3 direction) =>
            At(eye + (direction * ((H - N.Dot(eye)) / N.Dot(direction))));
    }

    /// <summary>One sight glass polygon: its corners in the interior's frame, its UVs, its draw
    /// priority and the texture name it wears.</summary>
    public sealed record Glass(IReadOnlyList<Vector3> Corners, IReadOnlyList<Vector2> Uvs, int Priority, string TextureName);

    /// <summary>A sight glass texture's two layers (<see cref="Split"/>) and the tint's alpha, 0-1.</summary>
    public sealed record Layers(ImageTexture Glass, ImageTexture Reticle, float TintAlpha);
}
