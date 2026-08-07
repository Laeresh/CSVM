using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Utils;
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
/// <para><b>Two kinds of decoration, two rendering paths.</b>
/// The split is <see cref="SceneBuilder.ClassifyBillboard"/>, i.e. the gamez model's own
/// <c>ModelType</c>:</para>
/// <list type="bullet">
/// <item><b>Facade → sprite.</b> One MultiMeshInstance3D per kind (all firtree1 share one
/// draw call), a hand-rolled Y-axis-billboard shader (upright, spins toward the camera — the
/// source quads are single one-sided cards, so the original must do the same), fullbright
/// like the rest of the world, alpha-scissor cutout, and the same cylindrical distance fog as
/// SceneBuilder's world shader. Never collidable.</item>
/// <item><b>Default → 3D decoration.</b> C2's <c>filmblock*</c>/<c>resblock*</c> studio and
/// residential buildings, its <c>parklot*</c> parked Studebakers, and C5's <c>cblock*</c> city
/// blocks: genuine multi-polygon geometry up to 108 m tall. These are instanced through
/// <see cref="SceneBuilder.SharedMesh"/> — the SAME mesh and materials the placed world uses,
/// so they are fullbright, fogged and depth-biased identically and, being real meshes, cannot
/// billboard. They keep their authored local basis, and they ARE collidable.</item>
/// </list>
///
/// <para><b>Sprites are NOT collidable; 3D decorations are</b> (both user decisions).
/// Do NOT give sprites a crossed-quad trimesh on the claim that "trees are
/// hittable like the original, `spruce_destroy` anims exist" — <b>that claim is a misreading and
/// the anims are not trees.</b> The two strings in the data are
/// <c>..\data\common\zrdr\planes\spruce_destroy{1,2}.zrd</c> — the <c>planes\</c> folder —
/// and the file's contents are an aircraft's engine/propeller destruction sequence
/// (<c>g_engine*</c>, <c>prop_part</c>, <c>spin</c>/<c>counterspin</c>,
/// <c>snd_propstart</c>, engine puffers). This is the <b>Spruce Goose</b>, Howard Hughes'
/// flying boat and the C2/M01 mission object. There is no spruce-<i>tree</i> animation
/// anywhere in the install, and no tree-destruction animation of any kind. A billboard has
/// no solid side to hit anyway — its collider is a phantom wall wherever the card happens to
/// be facing — which is the same reason every gamez billboard is exempt in WorldBuilder.
/// A city block is the opposite case on every count: it has real sides, it does not turn, and
/// flying through a skyscraper is not something the original permits.</para>
/// </summary>
public sealed class ClutterBuilder
{
    /// <summary>Node metadata key under which a clutter root (or a map-edge extension cell)
    /// holds the shared <see cref="ConcavePolygonShape3D"/>s its bodies reference by RID.
    /// The physics server holds RIDs, not Refs — without this anchor the shapes would be
    /// collected while bodies still point at them.</summary>
    public const string SharedShapeMeta = "csvm_clutter_shapes";

    // The sprite shader's distance-fog block, emitted only into the fogged variant.
    private const string FogLines =
        "    vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;\n"
        + "    float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);\n"
        + "    ALBEDO = mix(ALBEDO, csky_fog_color, csky_fog_on * fog_amt);\n";

    // Steeper than ~75° (XZ footprint under a quarter of the true area) grows no trees —
    // an upright billboard on a near-cliff face floats off it.
    private const float MinSlopeCos = 0.25f;

    // Collision for the 3D decorations only (user decision): a city block is real
    // geometry with real sides, unlike the sprite cards, which deliberately have no colliders.
    //
    // <b>Shapes are shared, not expanded.</b> Each distinct decoration
    // MESH gets ONE <see cref="ConcavePolygonShape3D"/>, built once in the mesh's own local
    // space; every placement then attaches that same shape to its region body with its own
    // transform, via <see cref="PhysicsServer3D.BodyAddShape(Rid, Rid, Transform3D?, bool)"/>.
    // That is what dissolves the objection the original comment here raised — "80k StaticBody3D
    // nodes would swamp the broadphase and the scene tree" is true, but a shape attached to a
    // body needs no scene-tree node of its own, so neither the node count nor the body count
    // moves. The region body split is kept anyway: it bounds the bodies at ~200 and gives the
    // crash log a locating name (`clutter_bld_<cx>_<cz>`).
    //
    // What this replaced: the first version transformed every vertex of every placement into
    // world space and concatenated the lot into one giant trimesh per region — 2,554,455
    // triangles in C5 from ~2,300 distinct ones, roughly 1,100x redundancy. Measured cost of
    // that build: 3,796 ms, of which only 271 ms was the vertex transform and
    // **3,403 ms was ConcavePolygonShape3D's BVH build** over 207 multi-hundred-thousand-triangle
    // regions. Sharing the shapes deletes essentially all of it, because the BVHs being built
    // are now ~40 triangles each.
    //
    // ⚠ These shapes are attached to the body's RID directly, NOT through CollisionShape3D
    // children or CollisionObject3D's shape-owner API. The node therefore does not know about
    // them, and any later call to a `ShapeOwner*` method on one of these bodies would run
    // `CollisionObject3D::_update_shapes()`, which clears the body and re-adds only what the
    // node knows — i.e. nothing. Do not mix the two APIs on these bodies.
    private const float CollisionRegion = 1024f;

    // C5's boot script registers cblock1..cblock7, but cblock4/5/6 dress a ground layer the
    // player can never see: cblock1/2/3's subface polygons cover their base polygons at
    // 97.0/99.9/100.0% and win the ground z-fight unconditionally — no day/night/zone/LOD
    // state ever picks the low-res pass (analysis/item9-depth-bias/CBLOCK-LOD.md §1c/§2, the
    // r≈0.70 same-scene-two-fidelities pairing). Stamping their buildings anyway doubled the
    // city's clutter and interpenetrated the visible district's buildings. CAP-22 settled the
    // original's side: its downtown lattice carries the 59–108 m towers that exist only in
    // cblock1/2/3's templates, and shows no interpenetration anywhere — so the original draws
    // the cblock1/2/3 city and not this set (closing commit of BL-250, and
    // playtest/CAP-22/README.md while it lives). A curated, measured list like
    // TextureArchive.KnownAbsentFromGameData, not a runtime overlap computation: the base
    // polygon carries no flag of its own (Subface marks the OVERLAY), so a live rule would
    // need CBLOCK-LOD.md's coplanar-overlap computation at every load.
    // ⚠ cblock7 stays: same artwork family, but a VISIBLE subface district (78 C5 nodes) — and
    // it places cb12a/13a/14a, so those names must NOT read as proof this exemption failed.
    private static readonly HashSet<string> BuriedClutterDistricts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cblock4", "cblock5", "cblock6", // C5 only
    };

    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly SceneBuilder? _scene;

    // One sprite shader per (lit, fogged) pair the decoration models actually ask for.
    private readonly Dictionary<int, Shader> _shaders = new();

    // Shared collision shapes of the last collidable Build, keyed by decoration MeshIndex.
    private Dictionary<int, ConcavePolygonShape3D>? _solidShapes;

    /// <param name="scene">The world's SceneBuilder, for the 3D-decoration path (its meshes
    /// and its fullbright world materials). Null disables that path and leaves only the
    /// sprite one.</param>
    public ClutterBuilder(GameZ gamez, TextureArchive textures, SceneBuilder? scene = null)
    {
        _gamez = gamez;
        _textures = textures;
        _scene = scene;
    }

    /// <summary>Total decoration sprites (billboard cards) placed by the last Build.</summary>
    public int InstanceCount { get; private set; }

    /// <summary>Total 3D decorations (city-block buildings, parked cars) placed by the last
    /// Build. Zero on every chapter whose templates carry only sprites.</summary>
    public int SolidCount { get; private set; }

    /// <summary>Collision triangles built for the 3D decorations by the last Build (0 when the
    /// build was not collidable). Since the shapes are shared this counts the DISTINCT
    /// triangles — ~2.3k in C5, not the 2.55M a per-placement expansion would produce.</summary>
    public int SolidCollisionTriangles { get; private set; }

    /// <summary>Per-kind counts of the last Build, e.g. "firtree1.tif ×4980".</summary>
    public string Summary { get; private set; } = "";

    /// <summary>The decoration kinds of the last Build (null until Build placed something).</summary>
    public IReadOnlyList<KindExport>? ExportedKinds { get; private set; }

    /// <summary>Distinct collision shapes built by the last Build (one per decoration mesh).</summary>
    public int SolidCollisionShapes { get; private set; }

    /// <summary>Shape attachments made by the last Build — one per collidable 3D decoration.</summary>
    public int SolidCollisionInstances { get; private set; }

    /// <summary>Reads the chapter's boot script out of the interp extraction
    /// (extracted/interp.json) and returns its registered clutter template names
    /// (the "AddClutterTemplates X" lines of support\&lt;chapter&gt;\adjust.gw),
    /// minus <see cref="BuriedClutterDistricts"/>.
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
                        if (!BuriedClutterDistricts.Contains(parts[i]))
                            names.Add(parts[i]);
            }
        }
        return names;
    }

    /// <summary>Template roots are parentless (they hang off nothing; the boot script
    /// LoadGameGen's them by name), so only match nodes no other node lists as a child — the
    /// world also contains unrelated same-named leaf nodes (g4/g5 …). Null when the gamez ships
    /// no such root, which is retail-data-normal: C2B registers three templates it does not
    /// carry, and C1B/C2/C3's <c>fogvol.zrd</c> names a <c>cloudsprite</c> no chapter carries.
    ///
    /// <para>Static and shared, because the fog-volume cloud clutter
    /// (<see cref="CSVM.Effects.FogVolumeClutter"/>) resolves its templates by exactly this rule
    /// from a different reader — one lookup, so the two cannot diverge.</para></summary>
    public static GameZNode? FindTemplateRoot(GameZ gamez, string name)
    {
        var isChild = new bool[gamez.Nodes.Count];
        foreach (var n in gamez.Nodes)
            foreach (var c in n.Children)
                if (c >= 0 && c < isChild.Length)
                    isChild[c] = true;
        foreach (var n in gamez.Nodes)
            if (!isChild[n.Index] && n.Kind == "Object3d"
                && string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))
                return n;
        return null;
    }

    // Any billboard kind counts as a placeable card: C1's trees/bushes are CylindricalY, and
    // C5's cblock templates additionally carry SphericalY `poleflare` glows beside their
    // CylindricalY `lightpole` posts. Both are one-quad cards and both are placed, which is
    // exactly what the old shape test did.
    /// <summary>Builds the clutter for the given template names; null when nothing was
    /// placed (no templates, or none of their ground textures appear in the world).</summary>
    /// <param name="collision">Attach static colliders to the 3D decorations (the city-block
    /// buildings and parked cars). Sprites are never collidable whatever this says — see the
    /// class remarks. Off for static viewing, on in flight.</param>
    public Node3D? Build(IReadOnlyList<string> templateNames, bool collision = false,
        string worldName = "world1")
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
        var exportedMesh = new List<int>();   // parallel: each export's decoration MeshIndex
        var solidKinds = new List<Kind>();
        InstanceCount = SolidCount = SolidCollisionTriangles = 0;
        SolidCollisionShapes = SolidCollisionInstances = 0;
        _solidShapes = null;
        foreach (var template in templates.Values)
            foreach (var kind in template.Kinds)
            {
                if (kind.Instances.Count == 0)
                    continue;
                var mmi = kind.Solid ? BuildSolidInstance(kind) : BuildKindInstance(kind);
                if (mmi == null)
                    continue;
                root.AddChild(mmi);
                exported.Add(new KindExport
                {
                    Texture = kind.Label,
                    Mesh = (ArrayMesh)mmi.Multimesh!.Mesh,
                    Material = mmi.MaterialOverride,
                    Solid = kind.Solid,
                    NodeBias = NodeBiasOf(kind),
                    Width = kind.Width,
                    Height = kind.Height,
                    Placements = kind.Instances,
                });
                exportedMesh.Add(kind.MeshIndex);
                if (kind.Solid)
                {
                    SolidCount += kind.Instances.Count;
                    solidKinds.Add(kind);
                }
                else
                {
                    InstanceCount += kind.Instances.Count;
                }
                parts.Add($"{kind.Label} ×{kind.Instances.Count}");
            }
        if (collision && solidKinds.Count > 0)
        {
            BuildSolidCollision(root, solidKinds);
            // Hand each solid export its shared shape, so MapEdgeExtender can attach the same
            // one to its mirrored placements past the map edge.
            if (_solidShapes != null)
                for (int i = 0; i < exported.Count; i++)
                    if (exported[i].Solid && _solidShapes.TryGetValue(exportedMesh[i], out var s))
                        exported[i].CollisionShape = s;
        }
        Summary = string.Join(", ", parts);
        ExportedKinds = exported.Count > 0 ? exported : null;
        return InstanceCount == 0 && SolidCount == 0 ? null : root;
    }

    /// <summary>Template roots are parentless (they hang off nothing; the boot script
    /// LoadGameGen's them by name), so only match nodes no other node lists as a child — the
    /// world also contains unrelated same-named leaf nodes (g4/g5 …). Null when the gamez ships
    /// no such root, which is retail-data-normal: C2B registers three templates it does not
    /// carry, and C1B/C2/C3's <c>fogvol.zrd</c> names a <c>cloudsprite</c> no chapter carries.
    ///
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
                    foreach (var cell in kind.CellPlacements)
                    {
                        float px = gx * p + cell.Origin.X, pz = gz * p + cell.Origin.Z;
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
                        // A sprite is planted flat ON the surface: its basis and its authored
                        // Y are both dropped, because its shader re-faces it from the instance
                        // origin and its own mesh already carries the card's vertical extent.
                        // A 3D decoration keeps both — the authored basis is its orientation,
                        // and the Y is its height above the block's ground plane.
                        kind.Instances.Add(kind.Solid
                            ? new Transform3D(cell.Basis, new Vector3(px, y + cell.Origin.Y, pz))
                            : new Transform3D(Basis.Identity, new Vector3(px, y, pz)));
                    }
                }
    }

    // The mesh's triangles in its own local space, using the same fan/strip rule as
    // SceneBuilder.EmitPolygon and PlaceOnMesh, so the collider matches what is drawn.
    private static void AppendTriangles(GameZMesh mesh, List<Vector3> into)
    {
        foreach (var poly in mesh.Polygons)
        {
            int n = poly.VertexIndices.Count;
            if (poly.TriangleStrip)
            {
                for (int i = 0; i + 2 < n; i++)
                {
                    into.Add(mesh.Vertices[poly.VertexIndices[i]]);
                    into.Add(mesh.Vertices[poly.VertexIndices[i + 1]]);
                    into.Add(mesh.Vertices[poly.VertexIndices[i + 2]]);
                }
            }
            else
            {
                for (int i = 1; i + 1 < n; i++)
                {
                    into.Add(mesh.Vertices[poly.VertexIndices[0]]);
                    into.Add(mesh.Vertices[poly.VertexIndices[i]]);
                    into.Add(mesh.Vertices[poly.VertexIndices[i + 1]]);
                }
            }
        }
    }

    private static string Sanitize(string name)
    {
        Span<char> bad = stackalloc[] { '.', ':', '@', '/', '"', '%' };
        foreach (var ch in bad)
            name = name.Replace(ch, '_');
        return name.Length == 0 ? "clutter_kind" : name;
    }

    // Upright billboard: the quad spins about its planted point's vertical axis toward
    // the camera (the source decorations are single one-sided cards — the original engine
    // must face them too, or trees would vanish edge-on). Fullbright like the world, hard
    // scissor cutout, and the same cylindrical distance fog as SceneBuilder's shader.
    // `lit`/`fogged` are the decoration model's own authored render flags: every tree
    // and bush card in this install is `lighting: false` — a camera-facing card has no
    // meaningful normal to light — so the sprites are not dimmed by the mission SUNLIGHT,
    // exactly as the placed world's self-lit models are. Emitted as shader VARIANTS, so a
    // lit, fogged kind's code is byte-for-byte the base form.
    private static string ShaderCode(bool lit, bool fogged, bool clampUv) => $$"""
        shader_type spatial;
        render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, {{(clampUv ? "repeat_disable" : "repeat_enable")}};

        // Fog globals + csky_world_light, and the DX7 gamma-space vertex modulate (trees share
        // the world's baked-lighting model). Both were once duplicated verbatim from
        // SceneBuilder; they are now single-sourced files.
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"

        // The shared ORDERED instance-uniform block. This shader reads only `csky_fog_on`, but
        // it must declare the whole block in the canonical order: Godot assigns instance-uniform
        // indices by declaration order within each shader and merges the mapping across every
        // material on one GeometryInstance3D, so two shaders that disagree silently read each
        // other's slots — the unfogged-hilltops bug, when `csky_fog_on` was index 0
        // here and index 1 in SceneBuilder's bias shader.
        //
        // An earlier attempt hand-padded this shader with an unused `node_bias` and was dropped
        // because it enforced nothing — the next shared uniform still had to be added to both
        // shaders by hand. The include is that fix done structurally: there is one declaration
        // site, so the orders cannot drift apart. Never declare an instance uniform below this.
        #include "res://shaders/csky_instance_uniforms.gdshaderinc"

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
            ALBEDO = col.rgb{{(lit ? " * csky_world_light" : "")}};
        {{(fogged ? FogLines : "")}}{{SceneBuilder.TintLine}}
            ALPHA = col.a;
            ALPHA_SCISSOR_THRESHOLD = 0.5;
        }
        """;

    // A template subtree: root → ground node (first descendant with a mesh; its texture
    // + quad size define what gets decorated and the tiling period) → decoration nodes
    // (a local translation each, sprite mesh on the child below).
    private Template? ParseTemplate(string name)
    {
        var root = FindTemplateRoot(name);
        if (root == null)
        {
            Log.Info("world", $"clutter template not in gamez template={name}");
            return null;
        }
        var ground = FirstWithMesh(root);
        if (ground == null || GroundInfo(ground) is not { } info)
        {
            Log.Info("world", $"clutter template has no textured ground quad template={name}");
            return null;
        }
        var template = new Template { GroundTexture = info.Texture, Period = info.Period };

        var kinds = new Dictionary<int, Kind>();
        // What is left in the skip list is only genuinely unusable:
        // a decoration node with no mesh anywhere under it, or a sprite card whose material
        // resolves no texture. 3D building/car decorations take the solid path below.
        // Collected and logged as ONE summary line.
        List<string>? skipped = null;
        foreach (var childIndex in ground.Children)
        {
            var deco = _gamez.Nodes[childIndex];
            var decoMesh = FirstWithMesh(deco, includeSelf: false);
            if (decoMesh == null)
            {
                (skipped ??= new List<string>()).Add(deco.Name);
                continue;
            }
            // The local transform is relative to the ground quad; its XZ becomes the offset
            // within the tiling cell. The basis and Y are kept whole for the solid path.
            var local = deco.Local ?? Transform3D.Identity;
            var cell = new Transform3D(local.Basis, new Vector3(
                local.Origin.X - info.Min.X, local.Origin.Y, local.Origin.Z - info.Min.Y));

            if (!kinds.TryGetValue(decoMesh.MeshIndex, out var kind))
            {
                if (SpriteInfo(decoMesh.MeshIndex) is { } s)
                {
                    kind = new Kind
                    {
                        MeshIndex = decoMesh.MeshIndex,
                        NodeIndex = decoMesh.Index,
                        Label = s.Texture,
                        Width = s.Width,
                        Height = s.Height,
                        Lit = _gamez.Meshes[decoMesh.MeshIndex].Lighting,
                        Fogged = _gamez.Meshes[decoMesh.MeshIndex].Fog,
                    };
                }
                else if (IsSolidDecoration(decoMesh.MeshIndex))
                {
                    kind = new Kind
                    {
                        MeshIndex = decoMesh.MeshIndex,
                        NodeIndex = decoMesh.Index,
                        Label = deco.Name,
                        Solid = true,
                    };
                }
                else
                {
                    (skipped ??= new List<string>()).Add(deco.Name);
                    continue;
                }
                kinds[decoMesh.MeshIndex] = kind;
                template.Kinds.Add(kind);
            }
            kind.CellPlacements.Add(cell);
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
            Log.Info("world", $"clutter template skipped decorations template={name} skipped={skipped.Count} examples='{shown}'");
        }
        return template.Kinds.Count > 0 ? template : null;
    }

    /// <inheritdoc cref="FindTemplateRoot(GameZ, string)"/>
    private GameZNode? FindTemplateRoot(string name) => FindTemplateRoot(_gamez, name);

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

    // A 3D decoration: anything with real geometry that is NOT a billboard card. C2's
    // filmblock/resblock buildings and parklot Studebakers, C5's cblock city blocks (2-27
    // polygons, up to 108 m tall). Requires a SceneBuilder to render through — without one
    // these fall back to the skip list.
    private bool IsSolidDecoration(int meshIndex)
    {
        if (_scene == null || meshIndex < 0 || meshIndex >= _gamez.Meshes.Count)
            return false;
        var mesh = _gamez.Meshes[meshIndex];
        if (mesh.Polygons.Count == 0 || mesh.Vertices.Count == 0)
            return false;
        // Not a card by the shared rule (nulls — a legacy extraction — fall through to the
        // shape heuristic in IsSpriteCard, which SpriteInfo already applied and rejected).
        return !IsSpriteCard(mesh);
    }

    // Only genuine sprite cards billboard. That question is answered by the
    // gamez model itself, through the shared SceneBuilder.ClassifyBillboard — the same rule
    // the renderer and the collision exemption use — instead of this file's own shape guess.
    // The two agree exactly on the shipped data: every template decoration is either a
    // Facade (1 polygon, 4 vertices, flat in local Z) or a Default 3D building (2-27
    // polygons). The Default ones are not skipped — they take
    // the solid path (IsSolidDecoration above).
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

    // ---------------------------------------------------------------- rendering

    private Shader SpriteShader(bool lit, bool fogged, bool clampUv)
    {
        int key = (lit ? 1 : 0) | (fogged ? 2 : 0) | (clampUv ? 4 : 0);
        if (!_shaders.TryGetValue(key, out var shader))
            _shaders[key] = shader = new Shader { Code = ShaderCode(lit, fogged, clampUv) };
        return shader;
    }

    // All instances of one kind as a single MultiMesh draw call.
    private MultiMeshInstance3D BuildKindInstance(Kind kind)
    {
        var tex = _textures.Find(kind.Label);
        // Clamp when the card's UVs never leave the unit square — the same data-driven rule as
        // SceneBuilder's world surfaces; wrapping bleeds the texture's opposite edge in at the
        // UV border (the hairline-seam / tracer-tail artifact).
        bool clampUv = SceneBuilder.UvsWithinUnitSquare(_gamez.Meshes[kind.MeshIndex].Polygons, pass: 0);
        var mat = new ShaderMaterial { Shader = SpriteShader(kind.Lit, kind.Fogged, clampUv) };
        if (tex != null)
            mat.SetShaderParameter("albedo_tex", tex);

        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = BuildSpriteMesh(kind.MeshIndex),
            InstanceCount = kind.Instances.Count,
        };
        for (int i = 0; i < kind.Instances.Count; i++)
            mm.SetInstanceTransform(i, kind.Instances[i]);

        return new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = kind.Width, // the shader may swing verts outside the static AABB
            Name = Sanitize(kind.Label),
        };
    }

    // A 3D decoration kind: the SAME mesh and materials the placed world uses, drawn once per
    // placement through a MultiMesh. Nothing here is a billboard — the world shader has no
    // camera-facing term at all — so a 45 m city block stands still while a tree card beside
    // it turns, which is the whole point of the split.
    //
    // `node_bias` is the world's cross-node draw-order tie-break (SceneBuilder.NodeBiasOf). Every
    // instance of a kind necessarily shares one value, since a MultiMesh has a single
    // instance-uniform set; the decoration node's own gamez index is the honest thing to ask
    // about, and keeps these layered against the terrain the same way the placed world's nodes
    // are against each other.
    // Through SceneBuilder so a decoration and the world node it was stamped from land on the
    // same cross-node tie-break — the conflict rank where the world build computed one, the flat
    // node index otherwise.
    private float NodeBiasOf(Kind kind)
        => _scene?.NodeBiasOf(kind.NodeIndex) ?? (kind.NodeIndex * SceneBuilder.NodeOrderBias);

    private MultiMeshInstance3D? BuildSolidInstance(Kind kind)
    {
        var mesh = _scene?.SharedMesh(kind.MeshIndex);
        if (mesh == null)
            return null;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = kind.Instances.Count,
        };
        for (int i = 0; i < kind.Instances.Count; i++)
            mm.SetInstanceTransform(i, kind.Instances[i]);

        var mmi = new MultiMeshInstance3D
        {
            Multimesh = mm,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Name = Sanitize(kind.Label),
        };
        mmi.SetInstanceShaderParameter("node_bias", NodeBiasOf(kind));
        return mmi;
    }

    private void BuildSolidCollision(Node3D root, List<Kind> kinds)
    {
        // One shape per distinct decoration mesh. Keyed by MeshIndex, which over-counts a
        // little (C5 ships the same building model as up to 4 separate gamez meshes, one per
        // template that uses it — 58 meshes for 32 distinct models), but at ~40 triangles a
        // shape that is not worth a geometry hash.
        var shapes = _solidShapes = new Dictionary<int, ConcavePolygonShape3D>();
        var tris = new List<Vector3>();
        foreach (var kind in kinds)
        {
            if (shapes.ContainsKey(kind.MeshIndex))
                continue;
            tris.Clear();
            AppendTriangles(_gamez.Meshes[kind.MeshIndex], tris);
            if (tris.Count == 0)
                continue;
            // Backface collision for the same reason SceneBuilder's world colliders use it:
            // the source winding is inconsistent, so a one-sided trimesh lets raycasts
            // through the down-wound faces.
            var shape = new ConcavePolygonShape3D { Data = tris.ToArray(), BackfaceCollision = true };
            shapes[kind.MeshIndex] = shape;
            SolidCollisionTriangles += tris.Count / 3;
        }
        SolidCollisionShapes = shapes.Count;
        if (shapes.Count == 0)
            return;

        // The shapes are referenced by the physics server through their RIDs only, which does
        // not keep the Godot Ref alive. Anchor them on the clutter root so their lifetime is
        // the scene tree's — without this the GC can free a shape out from under live bodies.
        var anchor = new Godot.Collections.Array();
        foreach (var s in shapes.Values)
            anchor.Add(s);
        root.SetMeta(SharedShapeMeta, anchor);

        var regions = new Dictionary<(int, int), StaticBody3D>();
        foreach (var kind in kinds)
        {
            if (!shapes.TryGetValue(kind.MeshIndex, out var shape))
                continue;
            var shapeRid = shape.GetRid();
            foreach (var xf in kind.Instances)
            {
                var key = (Mathf.FloorToInt(xf.Origin.X / CollisionRegion),
                           Mathf.FloorToInt(xf.Origin.Z / CollisionRegion));
                if (!regions.TryGetValue(key, out var body))
                {
                    // Named to locate the cell in a crash log.
                    regions[key] = body = new StaticBody3D { Name = $"clutter_bld_{key.Item1}_{key.Item2}" };
                    root.AddChild(body);
                }
                PhysicsServer3D.BodyAddShape(body.GetRid(), shapeRid, xf);
                SolidCollisionInstances++;
            }
        }
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

    /// <summary>One decoration kind of the last Build, exported for the map-edge
    /// extension (see MapEdgeExtender): the shared mesh (safe to reuse across MultiMesh
    /// instances), the billboard material for a sprite kind, and every planted world
    /// placement. The extender mirrors these past the map edge so the forest — and, in C5,
    /// the city — continues out there, as in the original.
    ///
    /// <para><c>Placements</c> carries full transforms rather than positions (changed
    /// with the 3D-decoration path): a sprite's is always identity-basis and the
    /// billboard shader re-faces it from the instance origin, but a building's authored basis
    /// is part of the placement and mirroring one means mirroring the whole transform.</para>
    ///
    /// <para><c>Material</c> is null for a solid kind: its mesh carries per-surface world
    /// materials from SceneBuilder, so there is nothing to override. <c>Width</c> is only
    /// meaningful for a sprite kind — it sizes the MultiMesh's ExtraCullMargin, since the
    /// billboard shader swings vertices outside the static AABB.</para>
    ///
    /// <para><c>CollisionShape</c> is the kind's SHARED collision shape, in the decoration
    /// mesh's own local space — non-null only for a solid kind of a collidable build. Because
    /// it is shared rather than pre-transformed, the extender can attach it to a mirrored
    /// placement for the cost of one <c>BodyAddShape</c> call, which is what made extension
    /// buildings collidable; merging a region trimesh at a boundary
    /// crossing, the old shape of this code, could not be done without a hitch.</para></summary>
    public sealed class KindExport
    {
        public string Texture = "";
        public ArrayMesh Mesh = null!;
        public Material? Material;
        public bool Solid;
        public float NodeBias;      // solid kinds: the `node_bias` instance uniform value
        public float Width, Height;
        public Shape3D? CollisionShape;
        public IReadOnlyList<Transform3D> Placements = null!;
    }

    // One decoration kind: every template instance of the same decoration mesh (all 13
    // firtree1 placements share mesh + texture), plus where it sits in each grid cell.
    private sealed class Kind
    {
        // Where each decoration of this kind sits within one template cell. Origin XZ is
        // relative to the ground quad's min corner, i.e. in [0, period); origin Y and the
        // basis are the decoration node's own, and are used by the solid path only (a sprite
        // is planted flat on the surface and re-faced by its shader — see PlaceOnTriangle).
        public readonly List<Transform3D> CellPlacements = new();
        public readonly List<Transform3D> Instances = new(); // world placements

        public int MeshIndex;
        public int NodeIndex;                    // a representative decoration node (draw order)
        public string Label = "";                // texture (sprites) or node name (solids)
        public bool Solid;                       // a 3D decoration, not a billboard card
        public float Width, Height;              // sprite quad extents (sprites only)
        // The decoration model's own render flags (sprites only — a solid decoration draws
        // through SceneBuilder's materials, which read them themselves).
        public bool Lit = true;
        public bool Fogged = true;
    }

    private sealed class Template
    {
        public readonly List<Kind> Kinds = new();

        public string GroundTexture = "";
        public float Period;                     // world-space tiling period (the ground quad's side)
    }
}
