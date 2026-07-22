using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The original engine's clutter system: standing trees on forest-textured hillsides,
/// bushes along the rivers. The gamez has no placed tree nodes — instead each chapter's
/// boot script (interp.zbd → support\&lt;chapter&gt;\adjust.gw) registers clutter
/// TEMPLATES ("AddClutterTemplates terpat02"), which the same script loads into the
/// gamez as parentless, unreferenced subtrees (why WorldBuilder's placed-node walk
/// never sees them). A template is a flat ground quad (its texture names the terrain
/// texture it decorates: terpat02.tif = the forest texture; its size the tiling period —
/// 512 m for C1's terpat02, and the "-128" template variants elsewhere are 128 m ones)
/// with decoration sprites scattered on it: single vertical quads, each a tree/bush
/// billboard with a local position on the patch.
///
/// <para>The engine then dresses every world polygon textured with a template's ground
/// texture. The exact original alignment is undecoded (the world's UV tiling is wildly
/// non-uniform on hillsides — 256..1280 m per repeat — so UV-space placement would
/// stretch the clutter with it); the remake tiles each template on a fixed world-space
/// X/Z grid of its authored period instead, which keeps the authored density everywhere,
/// is seam-consistent across adjacent polygons (one global grid), and plants every
/// decoration at the polygon's interpolated surface height.</para>
///
/// <para>Rendering: one MultiMeshInstance3D per decoration kind (all firtree1 share one
/// draw call), a hand-rolled Y-axis-billboard shader (upright, spins toward the camera —
/// the source quads are single one-sided cards, so the original must do the same),
/// fullbright like the rest of the world, alpha-scissor cutout, and the same cylindrical
/// distance fog as SceneBuilder's world shader.</para>
///
/// <para><b>Clutter is NOT collidable</b> (user decision, 2026-07-22). It used to get a
/// crossed-quad trimesh per sprite, justified by a claim that "trees are hittable like the
/// original, `spruce_destroy` anims exist". <b>That was a misreading and the anims are not
/// trees.</b> The two strings in the data are
/// <c>..\data\common\zrdr\planes\spruce_destroy{1,2}.zrd</c> — the <c>planes\</c> folder —
/// and the file's contents are an aircraft's engine/propeller destruction sequence
/// (<c>g_engine*</c>, <c>prop_part</c>, <c>spin</c>/<c>counterspin</c>,
/// <c>snd_propstart</c>, engine puffers). This is the <b>Spruce Goose</b>, Howard Hughes'
/// flying boat and the C2/M01 mission object. There is no spruce-<i>tree</i> animation
/// anywhere in the install, and no tree-destruction animation of any kind. A billboard has
/// no solid side to hit anyway — its collider is a phantom wall wherever the card happens to
/// be facing — which is the same reason every gamez billboard is exempt in WorldBuilder.</para>
/// </summary>
public sealed class ClutterBuilder
{
    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;

    /// <summary>Total decoration sprites placed by the last Build.</summary>
    public int InstanceCount { get; private set; }
    /// <summary>Per-kind counts of the last Build, e.g. "firtree1.tif ×4980".</summary>
    public string Summary { get; private set; } = "";

    /// <summary>One decoration kind of the last Build, exported for the map-edge
    /// extension (see MapEdgeExtender): the shared sprite mesh + billboard material
    /// (safe to reuse across MultiMesh instances), the quad extents, and every planted
    /// world position. The extender mirrors these positions past the map edge so the
    /// forest continues out there, as in the original. <c>Width</c> is still needed after
    /// the 2026-07-22 collision removal — it sizes the MultiMesh's ExtraCullMargin, since
    /// the billboard shader swings vertices outside the static AABB.</summary>
    public sealed class KindExport
    {
        public string Texture = "";
        public ArrayMesh Mesh = null!;
        public Material Material = null!;
        public float Width, Height;
        public IReadOnlyList<Vector3> Positions = null!;
    }

    /// <summary>The decoration kinds of the last Build (null until Build placed something).</summary>
    public IReadOnlyList<KindExport>? ExportedKinds { get; private set; }

    public ClutterBuilder(GameZ gamez, TextureArchive textures)
    {
        _gamez = gamez;
        _textures = textures;
    }

    /// <summary>Reads the chapter's boot script out of the interp extraction
    /// (extracted/interp.json) and returns its registered clutter template names
    /// (the "AddClutterTemplates X" lines of support\&lt;chapter&gt;\adjust.gw).
    /// Empty when the file or script is missing.</summary>
    public static List<string> TemplateNames(string interpPath, string chapter)
    {
        var names = new List<string>();
        if (!File.Exists(interpPath))
            return names;
        var wanted = $"support\\{chapter.ToLowerInvariant()}\\adjust.gw";
        using var doc = JsonDocument.Parse(File.ReadAllBytes(interpPath));
        foreach (var script in doc.RootElement.EnumerateArray())
        {
            if (!script.TryGetProperty("name", out var n)
                || !string.Equals(n.GetString(), wanted, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var line in script.GetProperty("lines").EnumerateArray())
            {
                var parts = (line.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] == "AddClutterTemplates")
                    for (int i = 1; i < parts.Length; i++)
                        names.Add(parts[i]);
            }
        }
        return names;
    }

    // One decoration kind: every template instance of the same sprite mesh (all 13
    // firtree1 placements share mesh + texture), plus where it sits in each grid cell.
    private sealed class Kind
    {
        public int MeshIndex;
        public string Texture = "";
        public float Width, Height;              // sprite quad extents (collision shape)
        public readonly List<Vector2> CellOffsets = new(); // XZ within the template cell [0, period)
        public readonly List<Vector3> Instances = new();   // world positions (planted points)
    }

    private sealed class Template
    {
        public string GroundTexture = "";
        public float Period;                     // world-space tiling period (the ground quad's side)
        public readonly List<Kind> Kinds = new();
    }

    /// <summary>Builds the clutter for the given template names; null when nothing was
    /// placed (no templates, or none of their ground textures appear in the world).
    /// Never collidable — see the class remarks.</summary>
    public Node3D? Build(IReadOnlyList<string> templateNames, string worldName = "world1")
    {
        var templates = new Dictionary<string, Template>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in templateNames)
            if (ParseTemplate(name) is { } t)
                templates[t.GroundTexture] = t;
        if (templates.Count == 0)
            return null;

        PlaceOnWorld(templates, worldName);

        var root = new Node3D { Name = "clutter" };
        var parts = new List<string>();
        var exported = new List<KindExport>();
        InstanceCount = 0;
        foreach (var template in templates.Values)
            foreach (var kind in template.Kinds)
            {
                if (kind.Instances.Count == 0)
                    continue;
                var mmi = BuildKindInstance(kind);
                root.AddChild(mmi);
                exported.Add(new KindExport
                {
                    Texture = kind.Texture,
                    Mesh = (ArrayMesh)mmi.Multimesh!.Mesh,
                    Material = mmi.MaterialOverride!,
                    Width = kind.Width,
                    Height = kind.Height,
                    Positions = kind.Instances,
                });
                InstanceCount += kind.Instances.Count;
                parts.Add($"{kind.Texture} ×{kind.Instances.Count}");
            }
        Summary = string.Join(", ", parts);
        ExportedKinds = exported.Count > 0 ? exported : null;
        return InstanceCount == 0 ? null : root;
    }

    // A template subtree: root → ground node (first descendant with a mesh; its texture
    // + quad size define what gets decorated and the tiling period) → decoration nodes
    // (a local translation each, sprite mesh on the child below).
    private Template? ParseTemplate(string name)
    {
        var root = FindTemplateRoot(name);
        if (root == null)
        {
            GD.Print($"clutter: template '{name}' not found in gamez");
            return null;
        }
        var ground = FirstWithMesh(root);
        if (ground == null || GroundInfo(ground) is not { } info)
        {
            GD.Print($"clutter: template '{name}' has no textured ground quad");
            return null;
        }
        var template = new Template { GroundTexture = info.Texture, Period = info.Period };

        var kinds = new Dictionary<int, Kind>();
        // A template like C5's cblock1 has dozens of non-sprite (3D building) decorations;
        // collect them and log ONE summary line rather than a warning per decoration.
        List<string>? skipped = null;
        foreach (var childIndex in ground.Children)
        {
            var deco = _gamez.Nodes[childIndex];
            var sprite = FirstWithMesh(deco, includeSelf: false);
            if (sprite == null || SpriteInfo(sprite.MeshIndex) is not { } s)
            {
                (skipped ??= new List<string>()).Add(deco.Name);
                continue;
            }
            if (!kinds.TryGetValue(sprite.MeshIndex, out var kind))
            {
                kinds[sprite.MeshIndex] = kind = new Kind
                {
                    MeshIndex = sprite.MeshIndex,
                    Texture = s.Texture,
                    Width = s.Width,
                    Height = s.Height,
                };
                template.Kinds.Add(kind);
            }
            var pos = deco.Local?.Origin ?? Vector3.Zero;
            kind.CellOffsets.Add(new Vector2(pos.X - info.Min.X, pos.Z - info.Min.Y));
        }
        if (skipped != null)
        {
            // Distinct example names (many decorations share a name — repeats would read
            // like a bug); the count stays the true number of skipped decoration nodes.
            var distinct = new List<string>();
            foreach (var s2 in skipped)
                if (!distinct.Contains(s2))
                    distinct.Add(s2);
            var shown = distinct.Count > 5
                ? string.Join(", ", distinct.GetRange(0, 5)) + ", …"
                : string.Join(", ", distinct);
            GD.Print($"clutter: template '{name}' skipped {skipped.Count} non-sprite decoration(s) ({shown})");
        }
        return template.Kinds.Count > 0 ? template : null;
    }

    // Template roots are parentless (they hang off nothing; the boot script LoadGameGen's
    // them by name), so only match nodes no other node lists as a child — the world also
    // contains unrelated same-named leaf nodes (g4/g5 …).
    private GameZNode? FindTemplateRoot(string name)
    {
        var isChild = new bool[_gamez.Nodes.Count];
        foreach (var n in _gamez.Nodes)
            foreach (var c in n.Children)
                if (c >= 0 && c < isChild.Length)
                    isChild[c] = true;
        foreach (var n in _gamez.Nodes)
            if (!isChild[n.Index] && n.Kind == "Object3d"
                && string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))
                return n;
        return null;
    }

    private GameZNode? FirstWithMesh(GameZNode node, bool includeSelf = true)
    {
        if (includeSelf && node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count
            && _gamez.Meshes[node.MeshIndex].Polygons.Count > 0)
            return node;
        foreach (var c in node.Children)
            if (c >= 0 && c < _gamez.Nodes.Count && FirstWithMesh(_gamez.Nodes[c]) is { } found)
                return found;
        return null;
    }

    private (string Texture, float Period, Vector2 Min)? GroundInfo(GameZNode ground)
    {
        var mesh = _gamez.Meshes[ground.MeshIndex];
        var tex = FirstTexture(mesh);
        if (tex == null || mesh.Vertices.Count == 0)
            return null;
        Vector3 min = mesh.Vertices[0], max = mesh.Vertices[0];
        foreach (var v in mesh.Vertices)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        float period = Mathf.Max(max.X - min.X, max.Z - min.Z);
        return period < 1f ? null : (tex, period, new Vector2(min.X, min.Z));
    }

    // Only genuine sprite cards billboard. Since 2026-07-22 that question is answered by the
    // gamez model itself, through the shared SceneBuilder.ClassifyBillboard — the same rule
    // the renderer and the collision exemption use — instead of this file's own shape guess.
    // The two agree exactly on the shipped data: every template decoration is either a
    // Facade (1 polygon, 4 vertices, flat in local Z) or a Default 3D building (2-27
    // polygons), so placement counts are unchanged, but the data now says WHY rather than
    // the geometry hinting at it. 3D decorations (C2's filmblock buildings, C5's cblock
    // city blocks — 7 polys / 64 m deep) still don't fit the billboard path and are skipped.
    private (string Texture, float Width, float Height)? SpriteInfo(int meshIndex)
    {
        var mesh = _gamez.Meshes[meshIndex];
        var tex = FirstTexture(mesh);
        if (tex == null || mesh.Vertices.Count == 0 || !IsSpriteCard(mesh))
            return null;
        Vector3 min = mesh.Vertices[0], max = mesh.Vertices[0];
        foreach (var v in mesh.Vertices)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        return (tex, max.X - min.X, max.Y - min.Y);
    }

    // Any billboard kind counts as a placeable card: C1's trees/bushes are CylindricalY, and
    // C5's cblock templates additionally carry SphericalY `poleflare` glows beside their
    // CylindricalY `lightpole` posts. Both are one-quad cards and both are placed, which is
    // exactly what the old shape test did.
    private static bool IsSpriteCard(GameZMesh mesh)
    {
        if (SceneBuilder.ClassifyBillboard(mesh) is { } kind)
            return kind != SceneBuilder.BillboardKind.None;

        // Legacy v0.6.1 extraction (no ModelType): keep the original shape heuristic, or a
        // rollback tree would place no clutter at all.
        if (mesh.Polygons.Count != 1 || mesh.Vertices.Count != 4)
            return false;
        Vector3 min = mesh.Vertices[0], max = mesh.Vertices[0];
        foreach (var v in mesh.Vertices)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        return max.Z - min.Z <= 0.1f * Mathf.Max(max.X - min.X, max.Y - min.Y);
    }

    private string? FirstTexture(GameZMesh mesh)
    {
        foreach (var poly in mesh.Polygons)
            if (poly.MaterialIndex >= 0 && poly.MaterialIndex < _gamez.Materials.Count
                && _gamez.Materials[poly.MaterialIndex].TextureName is { } tex)
                return tex;
        return null;
    }

    // Walk the placed world (the same set WorldBuilder renders: world children +
    // partition-referenced subtrees, nearest LOD only) and stamp each template onto
    // every polygon textured with its ground texture.
    private void PlaceOnWorld(Dictionary<string, Template> templates, string worldName)
    {
        GameZNode? world = null;
        foreach (var n in _gamez.Nodes)
            if (n.Kind == "World" && string.Equals(n.Name, worldName, StringComparison.OrdinalIgnoreCase))
            {
                world = n;
                break;
            }
        if (world == null)
            return;

        var seen = new HashSet<(int Kind, int X, int Z)>(); // dedup across decal-layered coplanar polys
        void Walk(int nodeIndex, Transform3D xf)
        {
            if (nodeIndex < 0 || nodeIndex >= _gamez.Nodes.Count)
                return;
            var node = _gamez.Nodes[nodeIndex];
            if (WorldBuilder.SkipWorldNode(node) || (node.Kind == "Lod" && node.LodRangeMin != 0f))
                return;
            if (node.Local is { } local)
                xf *= local;
            if (node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count)
                PlaceOnMesh(_gamez.Meshes[node.MeshIndex], xf, templates, seen);
            foreach (var c in node.Children)
                Walk(c, xf);
        }
        foreach (var c in world.Children)
            Walk(c, Transform3D.Identity);
        if (world.PartitionNodes != null)
            foreach (var idx in world.PartitionNodes)
                Walk(idx, Transform3D.Identity);
    }

    private void PlaceOnMesh(GameZMesh mesh, Transform3D xf,
        Dictionary<string, Template> templates, HashSet<(int, int, int)> seen)
    {
        foreach (var poly in mesh.Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
                continue;
            var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
            if (tex == null || !templates.TryGetValue(tex, out var template))
                continue;
            // Same triangle enumeration as SceneBuilder.EmitPolygon (fan, or strip when
            // flagged) so the surface heights match what is rendered.
            int n = poly.VertexIndices.Count;
            if (poly.TriangleStrip)
            {
                for (int i = 0; i + 2 < n; i++)
                    PlaceOnTriangle(template,
                        xf * mesh.Vertices[poly.VertexIndices[i]],
                        xf * mesh.Vertices[poly.VertexIndices[i + 1]],
                        xf * mesh.Vertices[poly.VertexIndices[i + 2]], seen);
            }
            else
            {
                for (int i = 1; i + 1 < n; i++)
                    PlaceOnTriangle(template,
                        xf * mesh.Vertices[poly.VertexIndices[0]],
                        xf * mesh.Vertices[poly.VertexIndices[i]],
                        xf * mesh.Vertices[poly.VertexIndices[i + 1]], seen);
            }
        }
    }

    // Steeper than ~75° (XZ footprint under a quarter of the true area) grows no trees —
    // an upright billboard on a near-cliff face floats off it.
    private const float MinSlopeCos = 0.25f;

    private static void PlaceOnTriangle(Template template, Vector3 a, Vector3 b, Vector3 c,
        HashSet<(int, int, int)> seen)
    {
        // Signed XZ area ×2 (for the containment test and barycentric heights below).
        float area2 = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
        float xzArea = 0.5f * Mathf.Abs(area2);
        if (xzArea < 0.5f)
            return;
        float trueArea = 0.5f * (b - a).Cross(c - a).Length();
        if (xzArea < trueArea * MinSlopeCos)
            return;

        float p = template.Period;
        float minX = Mathf.Min(a.X, Mathf.Min(b.X, c.X)), maxX = Mathf.Max(a.X, Mathf.Max(b.X, c.X));
        float minZ = Mathf.Min(a.Z, Mathf.Min(b.Z, c.Z)), maxZ = Mathf.Max(a.Z, Mathf.Max(b.Z, c.Z));
        int gx0 = Mathf.FloorToInt(minX / p), gx1 = Mathf.FloorToInt(maxX / p);
        int gz0 = Mathf.FloorToInt(minZ / p), gz1 = Mathf.FloorToInt(maxZ / p);
        for (int gx = gx0; gx <= gx1; gx++)
            for (int gz = gz0; gz <= gz1; gz++)
                for (int k = 0; k < template.Kinds.Count; k++)
                {
                    var kind = template.Kinds[k];
                    foreach (var off in kind.CellOffsets)
                    {
                        float px = gx * p + off.X, pz = gz * p + off.Y;
                        if (px < minX || px > maxX || pz < minZ || pz > maxZ)
                            continue;
                        // Barycentric in XZ: inside iff all weights share the area sign.
                        float w0 = (b.X - px) * (c.Z - pz) - (c.X - px) * (b.Z - pz);
                        float w1 = (c.X - px) * (a.Z - pz) - (a.X - px) * (c.Z - pz);
                        float w2 = (a.X - px) * (b.Z - pz) - (b.X - px) * (a.Z - pz);
                        if (area2 > 0 ? (w0 < 0 || w1 < 0 || w2 < 0) : (w0 > 0 || w1 > 0 || w2 > 0))
                            continue;
                        var key = (kind.MeshIndex, Mathf.RoundToInt(px * 4f), Mathf.RoundToInt(pz * 4f));
                        if (!seen.Add(key))
                            continue;
                        float y = (w0 * a.Y + w1 * b.Y + w2 * c.Y) / area2;
                        kind.Instances.Add(new Vector3(px, y, pz));
                    }
                }
    }

    // ---------------------------------------------------------------- rendering

    // Upright billboard: the quad spins about its planted point's vertical axis toward
    // the camera (the source decorations are single one-sided cards — the original engine
    // must face them too, or trees would vanish edge-on). Fullbright like the world, hard
    // scissor cutout, and the same cylindrical distance fog as SceneBuilder's shader.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;
        global uniform vec3 csky_fog_color;
        global uniform vec2 csky_fog_range;
        global uniform vec2 csky_fog_alt;
        // ⚠ `csky_fog_on` is instance-uniform index 0 HERE but index 1 in SceneBuilder's bias
        // shader, which declares `node_bias` first. Godot assigns these indices by declaration
        // order within each shader and merges the mapping across every material on one
        // GeometryInstance3D, so two shaders that disagree silently drop fog on the losing
        // surfaces — the 2026-07-17 unfogged-hilltops bug (docs/architecture.md, SceneBuilder).
        //
        // Checked 2026-07-22: the mismatch is LATENT, not live. Clutter renders through
        // MultiMeshInstance3D + MaterialOverride and never shares an instance with a
        // SceneBuilder material, and an 8-chapter run logs no `instance_uniforms.cpp` warning
        // at all. Padding this shader with an unused `node_bias` to line the indices up was
        // tried and dropped: it enforces nothing (the next shared instance uniform still has to
        // be added to both shaders by hand) while adding a uniform this shader cannot use. The
        // enforcing fix is a preamble constant shared with GetBiasShader, which belongs with
        // that shader. Until then: **declare any new instance uniform LAST, in both shaders.**
        instance uniform float csky_fog_on = 1.0;
        global uniform float csky_world_light = 1.0; // per-mission SUNLIGHT dimming (item 6)

        // DX7 gamma-space vertex modulate — see SceneBuilder.SrgbToLinearFn (trees share the
        // world's baked-lighting model; kept inline so this shader stays self-contained).
        vec3 csky_srgb_to_linear(vec3 c) {
            vec3 higher = pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4));
            vec3 lower = c * (1.0 / 12.92);
            return mix(higher, lower, step(c, vec3(0.04045)));
        }

        void vertex() {
            vec3 origin = MODEL_MATRIX[3].xyz;
            vec2 to_cam = CAMERA_POSITION_WORLD.xz - origin.xz;
            float len = length(to_cam);
            vec2 dir = len > 1e-4 ? to_cam / len : vec2(0.0, 1.0);
            mat3 spin = mat3(
                vec3(dir.y, 0.0, -dir.x),
                vec3(0.0, 1.0, 0.0),
                vec3(dir.x, 0.0, dir.y));
            VERTEX = (VIEW_MATRIX * vec4(origin + spin * VERTEX, 1.0)).xyz;
        }

        void fragment() {
            vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * texture(albedo_tex, UV);
            ALBEDO = col.rgb * csky_world_light;
            vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            float fog_amt = smoothstep(csky_fog_range.x, csky_fog_range.y, distance(fog_world.xz, CAMERA_POSITION_WORLD.xz))
                * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));
            ALBEDO = mix(ALBEDO, csky_fog_color, csky_fog_on * fog_amt);
            ALPHA = col.a;
            ALPHA_SCISSOR_THRESHOLD = 0.5;
        }
        """;

    private Shader? _shader;

    // All instances of one kind as a single MultiMesh draw call.
    private MultiMeshInstance3D BuildKindInstance(Kind kind)
    {
        var tex = _textures.Find(kind.Texture);
        var mat = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
        if (tex != null)
            mat.SetShaderParameter("albedo_tex", tex);

        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = BuildSpriteMesh(kind.MeshIndex),
            InstanceCount = kind.Instances.Count,
        };
        for (int i = 0; i < kind.Instances.Count; i++)
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, kind.Instances[i]));

        return new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = kind.Width, // the shader may swing verts outside the static AABB
            Name = Sanitize(kind.Texture),
        };
    }

    // The sprite's own source geometry (verts + UVs, fan-triangulated) in local space:
    // x spans ± half the width around the planted point, y up from 0 — the billboard
    // shader spins it about that local origin.
    private ArrayMesh BuildSpriteMesh(int meshIndex)
    {
        var mesh = _gamez.Meshes[meshIndex];
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var poly in mesh.Polygons)
        {
            int n = poly.VertexIndices.Count;
            for (int i = 1; i + 1 < n; i++)
                foreach (var corner in stackalloc[] { 0, i, i + 1 })
                {
                    st.SetNormal(Vector3.Back);
                    st.SetColor(poly.VertexColors != null && corner < poly.VertexColors.Count
                        ? poly.VertexColors[corner]
                        : Colors.White);
                    if (poly.UvCoords != null && corner < poly.UvCoords.Count)
                        st.SetUV(poly.UvCoords[corner]);
                    st.AddVertex(mesh.Vertices[poly.VertexIndices[corner]]);
                }
        }
        var arrayMesh = new ArrayMesh();
        st.Commit(arrayMesh);
        return arrayMesh;
    }

    private static string Sanitize(string name)
    {
        Span<char> bad = stackalloc[] { '.', ':', '@', '/', '"', '%' };
        foreach (var ch in bad)
            name = name.Replace(ch, '_');
        return name.Length == 0 ? "clutter_kind" : name;
    }
}
