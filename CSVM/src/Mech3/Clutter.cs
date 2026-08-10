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
/// texture it decorates: terpat02.tif = the forest texture) with decoration sprites
/// scattered on it: single vertical quads, each a tree/bush billboard with a local
/// position on the patch. That position is stored as the quad's own interpolated
/// TEXTURE UV, in [0,1) — the quad is the domain the position is normalised against,
/// never a metric spacing.
///
/// <para>The engine then dresses every world polygon textured with a template's ground
/// texture, and it does so <b>in texture space</b>: per triangle, take the UV bounding
/// box, floor it to an integer lattice, and stamp each decoration once per integer
/// repeat of the texture across that triangle, testing containment in UV and recovering
/// the world position through the triangle's own affine UV→world map
/// (<c>FUN_004dd6e0</c> steps 4, 6 and 7 — see <see cref="UvTriangle"/>). The clutter
/// therefore rotates, mirrors and stretches with the painted ground texture, which is
/// the intent: a building sits in its painted block wherever that block lands.</para>
///
/// <para><b>Which polygons get dressed is a per-polygon decision, and the flag that makes it is
/// <c>no_clutter</c></b> (raw bit <c>0x800</c>, carried as <see cref="GameZPolygon.Subface"/>,
/// gated in <c>FUN_004de2c0</c>). It does not mean "leave this ground bare" — where two
/// COPLANAR layers are painted over each other, as C5's whole city is, it selects which layer
/// decorates: flagged skips the overlay so the layer beneath stamps instead. C5's flagged
/// ground is dressed by <c>cblock4/5/6</c> (low-rise, ≤52 m) and its clear ground by
/// <c>cblock1/2/3/7</c> (towers, ≤108 m). Getting this backwards is BL-305: dressing flagged
/// ground with towers buries the pavement between the blocks.</para>
///
/// <para><b>There is no world-space grid, and there is no global clutter origin to build
/// one on</b> — A2 looked and the original has neither
/// (<c>analysis/bl-305-clutter-uv/FINDINGS-A2.md</c>). The fixed X/Z grid this file used
/// until B12 was a fiction with no counterpart, and it cost C1 roughly 4× its trees:
/// C1's terrain is painted with <c>terpat02</c> at half the template quad's scale, so
/// one repeat spans ~260 m where the grid stepped 512 (A1).</para>
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
/// <para><b>What this does NOT do, deliberately and knowingly.</b> The original's stamper
/// (<c>FUN_004dd6e0</c>) has eleven steps and this reproduces 4, 6 and 7. The rest are driven by
/// <c>templates.zrd</c>, which every chapter ships and nothing here reads yet — so each of these
/// is a known deviation with a measured shape, not an oversight:</para>
/// <list type="bullet">
/// <item><b>Step 5, the per-axis UV jitter</b> (<c>translate_uv_range</c>), and step 10's
/// <c>rotation_range</c> / <c>align_normal</c> — <b>INERT, not missing.</b> No chapter in the
/// install authors any of the three, so they cannot move a decoration on retail data. This is why
/// the original's placement has no random input affecting position or orientation at all, and why
/// the user reports C1's tree positions as <i>exactly</i> the original's rather than merely as
/// dense. Implementing them would change nothing; treat them as decoded and closed.</item>
/// <item><b>Step 9, <c>substitute</c></b> — the weighted model roll, authored on 41 kinds and the
/// most visible real gap. C1's <c>firtree1</c> should
/// become <c>firtree2</c> nine times in ten and C3's palms roll three ways evenly; here every
/// stamp of a kind is the same model, so the species mix is wrong even where the count is
/// right.</item>
/// <item><b>Step 10's <c>scale_range</c></b>, authored on 148 kinds — every kind that authors
/// anything. Every instance here is exactly its authored size where the original varies it
/// (C1 0.9–1.5×, C2 up to 1.0–3.0×).</item>
/// <item><b>Step 11, <c>far_fade_range</c></b> — the per-kind distance fade. C5 authors
/// [[200,300],[300,350]]; here the cylindrical world fog is the only distance cue.</item>
/// <item><b>The whole build is unseeded.</b> The original wraps it in
/// <c>srand(0x8EA91836)</c> … <c>srand(time(0))</c> (<c>FUN_004df1d0</c>), so its placement is
/// deterministic and reproducible run to run. Nothing here draws a random number at all, which
/// on retail data is the same outcome by a different route — since the only authored random
/// inputs left are <c>substitute</c> and <c>scale_range</c>, and neither moves a decoration.
/// ⚠ The moment either of those lands, a seeded PRNG has to land with it or the clutter will
/// shimmer between runs.</item>
/// </list>
/// <para>Wave C of <c>docs/PLAN-clutter-uv-placement.md</c> owns all five. One remake-only rule
/// also survives here on purpose: the quarter-metre <c>seen</c> dedup in
/// <see cref="PlaceOnTriangle"/>, which exists only because this file's UV containment test is
/// inclusive on a shared edge where the original's is strict.</para>
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

    // The largest integer UV lattice one triangle may span before it is refused and counted.
    // 64 × 64 repeats of the ground texture across a single triangle; A1's widest measured span
    // is a handful, so this is a tripwire for a corrupt UV array, not a tuning knob.
    private const long MaxLatticeCells = 4096;

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

    // ⚠ THERE IS NO LONGER A `BuriedClutterDistricts` EXEMPTION, and reintroducing one would be
    // a regression. It suppressed cblock4/5/6 MAP-WIDE on the reasoning that cblock1/2/3's
    // subface polygons cover their base polygons at 97.0/99.9/100.0% and win the ground z-fight
    // unconditionally (analysis/item9-depth-bias/CBLOCK-LOD.md §1c/§2), corroborated by CAP-22's
    // downtown lattice carrying the 59–108 m towers that exist only in cblock1/2/3 (BL-250).
    // Both observations are still true and neither was ever the whole map: what the exemption
    // was actually standing in for is the `no_clutter` gate in PlaceOnMesh, which this file did
    // not have. With that gate in place the exemption is not merely unnecessary, it is wrong —
    // it deletes the low-rise district the original stamps wherever the overlay is flagged,
    // which is BL-305's reported symptom (pavement swallowed by oversized blocks).
    //
    // The two are ONE COUPLED CHANGE. The gate alone empties C5's downtown; the restore alone
    // doubles the city and interpenetrates it, which is what BL-250 measured. Neither may be
    // reverted without the other.

    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly SceneBuilder? _scene;

    // One sprite shader per (lit, fogged) pair the decoration models actually ask for.
    private readonly Dictionary<int, Shader> _shaders = new();

    // Shared collision shapes of the last collidable Build, keyed by decoration MeshIndex.
    private Dictionary<int, ConcavePolygonShape3D>? _solidShapes;

    // What the UV-lattice walk skipped and what it asserted, for the one summary line Build
    // logs (LOG-5 / DIAG-15). Replaced per Build, never accumulated across two.
    private LatticeStats _stats = new();

    /// <param name="scene">The world's SceneBuilder, for the 3D-decoration path (its meshes
    /// and its fullbright world materials). Null disables that path and leaves only the
    /// sprite one.</param>
    public ClutterBuilder(GameZ gamez, TextureArchive textures, SceneBuilder? scene = null)
    {
        _gamez = gamez;
        _textures = textures;
        _scene = scene;
    }

    /// <summary>Why <see cref="UvTriangle.Build"/> refused a triangle. The two faults are
    /// independent: a fan or strip artifact of an n-gon with repeated or collinear corners has
    /// zero WORLD area and a perfectly ordinary UV area, so a UV-area guard alone lets it through
    /// and its meaningless affine map — both axes collapsed onto a line — plants a row of trees
    /// along that line. A2 counted 1,655 of them on C1's <c>terpat02</c> alone.</summary>
    public enum UvTriangleFault
    {
        /// <summary>The triangle has world-space area but no invertible UV map.</summary>
        ZeroUvArea,

        /// <summary>The triangle's three world vertices are collinear or coincident.</summary>
        ZeroWorldArea,
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
    /// (the "AddClutterTemplates X" lines of support\&lt;chapter&gt;\adjust.gw).
    /// <b>Every registered name, unfiltered</b> — which districts actually decorate a given
    /// patch of ground is decided per polygon by the <c>no_clutter</c> gate in PlaceOnMesh,
    /// not by a curated exclusion list here (see the remarks above the fields).
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

    /// <summary><c>--clutter-templates=</c>'s replacement for <see cref="TemplateNames"/>: the
    /// caller's names, filtered to the ones this gamez actually carries a template root for.
    ///
    /// <para>Built to reach <c>cblock4/5/6</c> when a map-wide exemption made them unloadable;
    /// that exemption is gone, so the flag is now simply "build this template set instead of the
    /// chapter's", which is what makes it a district-by-district A/B instrument. It does
    /// <b>not</b> bypass the <c>no_clutter</c> gate — that is per polygon, not per template, and
    /// bypassing it would defeat the comparison.</para>
    ///
    /// <para>Prints one line naming what was asked for, what resolved and what did not. A name no
    /// chapter carries is retail-data-normal (see <see cref="FindTemplateRoot(GameZ, string)"/>)
    /// and is not an error — but it must be visible, or an absent district reads as an empty
    /// one.</para></summary>
    public static List<string> OverrideTemplateNames(GameZ gamez, IReadOnlyList<string> requested)
    {
        var resolved = new List<string>();
        var absent = new List<string>();
        foreach (var name in requested)
            (FindTemplateRoot(gamez, name) != null ? resolved : absent).Add(name);
        GD.Print($"clutter: --clutter-templates={string.Join(",", requested)} replaces the chapter's"
                 + " registered set (the per-polygon no_clutter gate still applies) — in gamez: "
                 + (resolved.Count > 0 ? string.Join(",", resolved) : "(none)")
                 + "; not carried by this chapter: "
                 + (absent.Count > 0 ? string.Join(",", absent) : "(none)")
                 + " (retail-data-normal, not an error)");
        return resolved;
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

    /// <summary>The first node at or under <paramref name="node"/> carrying a non-empty mesh —
    /// a template root's ground quad, or a decoration node's card/building. Public and static
    /// because it is the other half of resolving a template (with
    /// <see cref="FindTemplateRoot(GameZ, string)"/>), and the UV a decoration is stored at is
    /// only checkable against A2's worked example if both halves can be reached.</summary>
    public static GameZNode? FirstWithMesh(GameZ gamez, GameZNode node, bool includeSelf = true)
    {
        if (includeSelf && node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count
            && gamez.Meshes[node.MeshIndex].Polygons.Count > 0)
            return node;
        foreach (var c in node.Children)
            if (c >= 0 && c < gamez.Nodes.Count && FirstWithMesh(gamez, gamez.Nodes[c]) is { } found)
                return found;
        return null;
    }

    /// <summary>The template's ground quad as <c>FUN_004dd230</c> uses it: the polygon's plane,
    /// and the affine map from a local position on that plane to the polygon's own interpolated
    /// texture UV.
    ///
    /// <para>⚠ <b>NOT a scalar tiling period.</b> <c>max(extentX, extentZ)</c>, what this
    /// returned before, is an exact relabelling of the quad UV on 28 of the install's 32
    /// template quads and WRONG on the other four: <c>filmblock1</c> (64×128),
    /// <c>cliff1_sandtrans</c> (128×64) and <c>parklot1</c> (16×32) / <c>parklot2</c> (32×16),
    /// the last two additionally UV-MIRRORED — u = 0 at max X. Worst error 0.74 UV. Two axes
    /// with signs are the least that can describe them
    /// (<c>analysis/bl-305-clutter-uv/FINDINGS-A2.md</c>). Nothing here keys off corner order
    /// either: the winding differs between templates while the parameterisation does not.</para>
    /// </summary>
    public static GroundQuad? GroundInfo(GameZ gamez, GameZNode ground)
    {
        var mesh = gamez.Meshes[ground.MeshIndex];
        var tex = FirstTexture(gamez, mesh);
        if (tex == null || mesh.Vertices.Count == 0)
            return null;
        Vector3 min = mesh.Vertices[0], max = mesh.Vertices[0];
        foreach (var v in mesh.Vertices)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        float extX = max.X - min.X, extZ = max.Z - min.Z;
        if (Mathf.Max(extX, extZ) < 1f)
            return null;

        foreach (var poly in mesh.Polygons)
        {
            if (poly.UvCoords == null || poly.UvCoords.Count < 3 || poly.VertexIndices.Count < 3)
                continue;
            Vector3 a = mesh.Vertices[poly.VertexIndices[0]],
                    b = mesh.Vertices[poly.VertexIndices[1]],
                    c = mesh.Vertices[poly.VertexIndices[2]];

            // Everything from here down is DOUBLE, and TryUv rounds once at the very end. Not
            // fussiness: on the 28 square, axis-aligned templates a single rounding makes
            // `uv × extent` reproduce, BIT FOR BIT, the metres the old scalar rule stored (the
            // extents are powers of two, so that scaling is exact and rounding commutes with
            // it). Done in float, the intermediate `1 + (x − 256)/512` rounds separately and
            // moves a tree by up to one ulp — ~0.03 mm, invisible in an instance count and
            // enough to flip pixels in a golden of a forest. Measured: it moved `c1-flight`.
            double[] e1 = { b.X - (double)a.X, b.Y - (double)a.Y, b.Z - (double)a.Z };
            double[] e2 = { c.X - (double)a.X, c.Y - (double)a.Y, c.Z - (double)a.Z };
            double[] n = Cross(e1, e2);
            double d = (n[0] * n[0]) + (n[1] * n[1]) + (n[2] * n[2]);
            if (d < 1e-9)
                continue;   // a degenerate first triangle carries no plane and no UV map
            // The reciprocal basis of {e1, e2, n}: f1·e1 = f2·e2 = 1 and every other pairing 0,
            // since e1·(e2×n) = |n|². A gradient built from it is perpendicular to n, which is
            // exactly "project along the quad normal, then read the interpolated UV" — and it
            // carries the sign, so a mirrored quad comes out mirrored.
            double[] f1 = Cross(e2, n), f2 = Cross(n, e1);
            for (int k = 0; k < 3; k++)
            {
                f1[k] /= d;
                f2[k] /= d;
            }
            double len = Math.Sqrt(d);
            Vector2 uvA = poly.UvCoords[0], uvB = poly.UvCoords[1], uvC = poly.UvCoords[2];
            double du1 = uvB.X - (double)uvA.X, du2 = uvC.X - (double)uvA.X;
            double dv1 = uvB.Y - (double)uvA.Y, dv2 = uvC.Y - (double)uvA.Y;
            var quad = new GroundQuad
            {
                Texture = tex,
                ExtentX = extX,
                ExtentZ = extZ,
                NormalX = n[0] / len,
                NormalY = n[1] / len,
                NormalZ = n[2] / len,
                AnchorX = a.X,
                AnchorY = a.Y,
                AnchorZ = a.Z,
                AnchorU = uvA.X,
                AnchorV = uvA.Y,
                GradUX = (f1[0] * du1) + (f2[0] * du2),
                GradUY = (f1[1] * du1) + (f2[1] * du2),
                GradUZ = (f1[2] * du1) + (f2[2] * du2),
                GradVX = (f1[0] * dv1) + (f2[0] * dv2),
                GradVY = (f1[1] * dv1) + (f2[1] * dv2),
                GradVZ = (f1[2] * dv1) + (f2[2] * dv2),
            };
            // The polygon's own fan, for the containment half of the projection test.
            for (int i = 1; i + 1 < poly.VertexIndices.Count; i++)
            {
                quad.Faces.Add(mesh.Vertices[poly.VertexIndices[0]]);
                quad.Faces.Add(mesh.Vertices[poly.VertexIndices[i]]);
                quad.Faces.Add(mesh.Vertices[poly.VertexIndices[i + 1]]);
            }
            return quad;
        }
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

        _stats = new LatticeStats();
        PlaceOnWorld(templates, worldName);
        _stats.Report();

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

    // FUN_004dd6e0 steps 4, 6 and 7: the UV bounding box floored to an integer lattice, a
    // containment test in UV SPACE, and the world position recovered through the triangle's own
    // affine UV→world map. Steps 5 (translate_uv_range jitter), 9 (substitute), 10
    // (rotation/scale) and 11 (far_fade_range) are deliberately absent — Wave C owns them.
    //
    // <para>There is no world grid here and there must never be one again: A2 established that the
    // original has no global "the clutter grid starts here" origin at all. Each decoration is
    // stamped once per integer repeat of the ground texture across this triangle, so the clutter
    // rotates, mirrors and stretches WITH the painted texture — which is why C1's forest, painted
    // at half the template quad's scale, comes out ~4× denser than the grid made it.</para>
    private static void PlaceOnTriangle(Template template, Vector3 a, Vector3 b, Vector3 c,
        Vector2 uva, Vector2 uvb, Vector2 uvc, HashSet<(int, int, int)> seen, LatticeStats stats)
    {
        // Two independent degeneracy tests, because neither implies the other (A2): a fan or
        // strip artifact of an n-gon with repeated or collinear corners has ZERO world area and a
        // perfectly healthy UV area — 1,655 of them on C1's terpat02 alone — and its affine map
        // is finite but meaningless, both axes collapsed onto a line.
        var tri = UvTriangle.Build(a, b, c, uva, uvb, uvc, out var fault);
        if (tri == null)
        {
            if (fault == UvTriangleFault.ZeroWorldArea)
                stats.ZeroWorldArea++;
            else
                stats.ZeroUvArea++;
            return;
        }

        // A sub-half-square-metre XZ footprint carries no lattice cell worth walking. This is the
        // one remaining remake-only geometry rule here and it does fire — 14 triangles in C5,
        // 0 in every other chapter (B13's census).
        //
        // What used to sit beside it was `xzArea < trueArea * MinSlopeCos` with MinSlopeCos =
        // 0.25 — "steeper than ~75° grows no trees". B13 deleted it as an invention that never
        // fired. The original's slope cull is authored per kind (min_slope/max_slope → cosines
        // at kind+0x58/+0x5c, FUN_004deab0) and DEFAULTS TO ±1.0, i.e. no cull; no shipped
        // templates.zrd authors either key. And the constant was inert anyway: xzArea/trueArea
        // is exactly |Ny| of the plane normal, and the steepest clutter-eligible triangle in the
        // whole install is C1's at 0.4598 (~62.6°), against a 0.25 (~75.5°) threshold. Zero
        // triangles culled in every chapter; same instrument at 0.50 culls 3 in C1, so the zero
        // is a measurement. Do not reintroduce it — a cull belongs in templates.zrd's min_slope,
        // which no chapter authors.
        float area2 = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
        float xzArea = 0.5f * Mathf.Abs(area2);
        if (xzArea < 0.5f)
            return;

        // LOG-5: a hugely stretched triangle would make the lattice loop enormous. The original
        // has the same exposure and no bound; A1 measured every shipped span as modest (terpat02
        // runs 134–561 m per U over triangles a few hundred metres wide), so this should never
        // fire. If it does, the count in the summary line is a finding, not a nuisance.
        if (tri.CellCount > MaxLatticeCells)
        {
            stats.OverLargeLattice++;
            if (tri.CellCount > stats.WorstLatticeCells)
                stats.WorstLatticeCells = tri.CellCount;
            return;
        }

        for (int uInt = tri.MinU; uInt <= tri.MaxU; uInt++)
            for (int vInt = tri.MinV; vInt <= tri.MaxV; vInt++)
                for (int k = 0; k < template.Kinds.Count; k++)
                {
                    var kind = template.Kinds[k];
                    foreach (var cell in kind.CellPlacements)
                    {
                        // cell.Origin.X / .Z are the decoration's quad UV, in [0, 1); the lattice
                        // cell shifts it to this repeat of the texture (FUN_004dd6e0 step 5,
                        // minus the jitter). Kept in double for the same reason B11's quad map
                        // is: one extra rounding here is ~0.03 mm and enough to move a golden.
                        double cu = uInt + (double)cell.Origin.X;
                        double cv = vInt + (double)cell.Origin.Z;
                        if (!tri.Contains(cu, cv))
                            continue;
                        var p = tri.World(cu, cv);
                        // The affine map gives Y as well as XZ, which is what step 7 does. On a
                        // planar triangle — and a triangle cannot be anything else — that is the
                        // same number the old barycentric height produced (A2 checked the two
                        // routes agree to 1.8e-12 m), so nothing is lost by dropping the second
                        // computation.
                        // ⚠ The quarter-metre dedup, kept DELIBERATELY, and it is remake-only —
                        // the original has no such set. B13 measured what it was doing and found
                        // two jobs. The first is now gone: it stood in for the missing
                        // `no_clutter` gate, so that the walk did not reach both members of a
                        // coplanar overlay/base pair. PlaceOnMesh reads the flag itself now, so
                        // that job belongs to the gate and not to this set.
                        //
                        // The second job remains, and is why the set stays: it removes real
                        // Contains() is INCLUSIVE on the edge, so a lattice candidate landing
                        // exactly on the diagonal two triangles share is claimed by both. C1
                        // gains 38 such duplicates without the set, C4 139, C5 1,082 — mostly
                        // solid buildings, whose authored UVs sit on tidy fractions. The
                        // original's step-6 test is strict, so it claims such a point in
                        // NEITHER triangle; matching that is a change to B12's containment rule,
                        // not to this set.
                        var key = (kind.MeshIndex, Mathf.RoundToInt(p.X * 4f), Mathf.RoundToInt(p.Z * 4f));
                        if (!seen.Add(key))
                            continue;
                        // The mechanical check for "trees in the sea": a subtly wrong UV-space
                        // containment test still yields a finite world point, and only the source
                        // triangle can say it is the wrong one.
                        stats.Placed++;
                        if (!InSourceTriangle(a, b, c, p))
                            stats.OutsideSource++;
                        // A sprite is planted flat ON the surface: its basis and its authored
                        // Y are both dropped, because its shader re-faces it from the instance
                        // origin and its own mesh already carries the card's vertical extent.
                        // A 3D decoration keeps both — the authored basis is its orientation,
                        // and the Y is its height above the block's ground plane.
                        kind.Instances.Add(kind.Solid
                            ? new Transform3D(cell.Basis, new Vector3(p.X, p.Y + cell.Origin.Y, p.Z))
                            : new Transform3D(Basis.Identity, p));
                    }
                }
    }

    // Barycentric containment in the triangle's OWN plane (not an XZ projection — a hillside
    // triangle's XZ shadow is a different shape). The slack absorbs the single rounding the
    // affine map ends on; a genuinely misplaced instance misses by metres, not by 1e-3.
    private static bool InSourceTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    {
        const float slack = 1e-3f;
        var n = (b - a).Cross(c - a);
        float d = n.LengthSquared();
        if (d < 1e-9f)
            return false;
        float w0 = (b - a).Cross(p - a).Dot(n) / d;
        float w1 = (c - b).Cross(p - b).Dot(n) / d;
        float w2 = (a - c).Cross(p - c).Dot(n) / d;
        return w0 >= -slack && w1 >= -slack && w2 >= -slack;
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

    private static double[] Cross(double[] p, double[] q) => new[]
    {
        (p[1] * q[2]) - (p[2] * q[1]),
        (p[2] * q[0]) - (p[0] * q[2]),
        (p[0] * q[1]) - (p[1] * q[0]),
    };

    private static string? FirstTexture(GameZ gamez, GameZMesh mesh)
    {
        foreach (var poly in mesh.Polygons)
            if (poly.MaterialIndex >= 0 && poly.MaterialIndex < gamez.Materials.Count
                && gamez.Materials[poly.MaterialIndex].TextureName is { } tex)
                return tex;
        return null;
    }

    // Distinct example names for a one-line skip summary (many decorations share a name —
    // repeats would read like a bug); the caller keeps the true count beside it.
    private static string SkipExamples(List<string> names)
    {
        var distinct = new List<string>();
        foreach (var n in names)
            if (!distinct.Contains(n))
                distinct.Add(n);
        return distinct.Count > 5
            ? string.Join(", ", distinct.GetRange(0, 5)) + ", …"
            : string.Join(", ", distinct);
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
        var ground = FirstWithMesh(_gamez, root);
        if (ground == null || GroundInfo(_gamez, ground) is not { } quad)
        {
            Log.Info("world", $"clutter template has no textured ground quad template={name}");
            return null;
        }
        var template = new Template { GroundTexture = quad.Texture };

        var kinds = new Dictionary<int, Kind>();
        // What is left in the skip list is only genuinely unusable:
        // a decoration node with no mesh anywhere under it, or a sprite card whose material
        // resolves no texture. 3D building/car decorations take the solid path below.
        // Collected and logged as ONE summary line.
        List<string>? skipped = null;
        // FUN_004dd230's `template %s clutter %s does not project to polygon.` path. No
        // decoration in the retail install takes it (A2's notproj column is 0 everywhere), so
        // this is fidelity, not a case any chapter exercises — but a miss must skip and say so,
        // never quietly land at UV (0,0).
        List<string>? offQuad = null;
        foreach (var childIndex in ground.Children)
        {
            var deco = _gamez.Nodes[childIndex];
            var decoMesh = FirstWithMesh(_gamez, deco, includeSelf: false);
            if (decoMesh == null)
            {
                (skipped ??= new List<string>()).Add(deco.Name);
                continue;
            }
            // FUN_004dd230: project the decoration's local position onto the ground quad along
            // the quad normal, read the polygon's INTERPOLATED TEXTURE UV there, and wrap it
            // into [0, 1). The stored XZ is that UV pair, not metres — which is what makes the
            // four non-square/UV-mirrored templates come out right, since no scalar period can
            // describe them. The basis and Y stay exactly as authored: the solid path needs the
            // basis for orientation and the Y for height above the block's ground plane.
            var local = deco.Local ?? Transform3D.Identity;
            if (!quad.TryUv(local.Origin, out var uv))
            {
                (offQuad ??= new List<string>()).Add(deco.Name);
                continue;
            }
            var cell = new Transform3D(local.Basis, new Vector3(
                GroundQuad.Wrap(uv.X), local.Origin.Y, GroundQuad.Wrap(uv.Y)));

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
            Log.Info("world", $"clutter template skipped decorations template={name} skipped={skipped.Count} examples='{SkipExamples(skipped)}'");
        if (offQuad != null)
            Log.Info("world", $"clutter decorations do not project onto the ground quad template={name} skipped={offQuad.Count} examples='{SkipExamples(offQuad)}'");
        return template.Kinds.Count > 0 ? template : null;
    }

    /// <inheritdoc cref="FindTemplateRoot(GameZ, string)"/>
    private GameZNode? FindTemplateRoot(string name) => FindTemplateRoot(_gamez, name);

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
        var tex = FirstTexture(_gamez, mesh);
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
            // FUN_004de2c0's gate: a polygon flagged 0x800 grows no clutter. The bit is
            // `no_clutter`, authored by node name (gg_load.c's `strstr(name, "no_clutter")`,
            // single-writer at 005654f6) and parsed by mech3ax as `unk3` —
            // GameZPolygon.Subface is a misnomer this file cannot fix alone, because
            // SceneBuilder.SubfaceBias reads the same field for draw order and the two uses
            // are independent.
            //
            // ⚠ It does NOT mean "the original leaves this ground bare". C5's city is TWO
            // COPLANAR LAYERS at the same Y — a cblock1/2/3/7 overlay and a cblock4/5/6 base
            // — and the flag selects WHICH ONE decorates the ground: flagged means skip the
            // overlay, so the layer underneath stamps instead. Measured over every cblock1/2/3/7
            // polygon by exact XZ polygon-intersection area, odds ratio 1,036.8x, with the
            // user's own flyover matching (analysis/bl-305-clutter-uv/FINDINGS-layer-pairing.md).
            // That is why this gate MUST NOT land without the cblock4/5/6 exemption being
            // removed below: alone it deletes the low-rise city and leaves bare pavement.
            if (poly.Subface)
            {
                _stats.NoClutterFlagged++;
                continue;
            }
            // The UVs of the layer whose texture matched — here always materials[0], because A3
            // measured that no polygon in the install names a registered template on layer 1+,
            // so the original's per-layer loop (FUN_004de190) has nothing extra to find. Without
            // them there is no lattice at all: FUN_004de2c0 skips a polygon whose UV array is
            // null, and so does this. A3 found no such polygon either, so the counter is a
            // measurement rather than a blind spot.
            int n = poly.VertexIndices.Count;
            var uvs = poly.UvCoords;
            if (uvs == null || uvs.Count < n)
            {
                _stats.NoUvArray++;
                continue;
            }
            // Same triangle enumeration as SceneBuilder.EmitPolygon (fan, or strip when
            // flagged) so the surface heights match what is rendered — and the UVs are indexed
            // by CORNER POSITION, not by vertex id. That distinction is load-bearing rather than
            // pedantic: C1 model 953's polygon 3 lists vertex 5 at two different corners with two
            // different UVs, and reading the UV through the vertex id would stamp the second
            // corner's lattice in the first corner's texture frame.
            if (poly.TriangleStrip)
            {
                for (int i = 0; i + 2 < n; i++)
                    PlaceOnTriangle(template,
                        xf * mesh.Vertices[poly.VertexIndices[i]],
                        xf * mesh.Vertices[poly.VertexIndices[i + 1]],
                        xf * mesh.Vertices[poly.VertexIndices[i + 2]],
                        uvs[i], uvs[i + 1], uvs[i + 2], seen, _stats);
            }
            else
            {
                for (int i = 1; i + 1 < n; i++)
                    PlaceOnTriangle(template,
                        xf * mesh.Vertices[poly.VertexIndices[0]],
                        xf * mesh.Vertices[poly.VertexIndices[i]],
                        xf * mesh.Vertices[poly.VertexIndices[i + 1]],
                        uvs[0], uvs[i], uvs[i + 1], seen, _stats);
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

    /// <summary>One world triangle seen as the original's stamper sees it (<c>FUN_004dd6e0</c>
    /// steps 4, 6 and 7): the integer UV lattice its texture coordinates span, a containment test
    /// <b>in UV space</b>, and the affine UV→world map that turns a texture coordinate back into
    /// a position.
    ///
    /// <para>Everything here is <c>double</c> and rounds exactly once, in <see cref="World"/>.
    /// B11 measured what happens otherwise: a float intermediate moved a tree by one ulp
    /// (~0.03 mm), invisible in every instance count and enough to change the pixels of a forest
    /// golden in a chapter that must not move.</para>
    ///
    /// <para>⚠ <b>The map is valid only inside its own triangle.</b> Neighbouring triangles of
    /// the same polygon routinely carry different UV frames — A2 found eight of them on C1's
    /// <c>terpat02</c>, with handedness split almost evenly — so reusing one triangle's map for
    /// the next is not an optimisation, it is a wrong answer.</para></summary>
    public sealed class UvTriangle
    {
        // Below this the world triangle is a sliver with no usable plane. 1e-4 m² is a square
        // 1 cm on a side; the artifacts A2 counted are exactly zero, not merely small.
        private const double MinWorldArea2 = 1e-8;

        // Likewise in UV space, where the quantity is the map's determinant.
        private const double MinUvArea2 = 1e-12;

        private readonly double _u0, _v0, _u1, _v1, _u2, _v2;
        private readonly double _p0X, _p0Y, _p0Z;
        private readonly double _aX, _aY, _aZ;   // world displacement per +1 U
        private readonly double _bX, _bY, _bZ;   // world displacement per +1 V
        private readonly double _sign;           // the UV winding, for the edge test

        private UvTriangle(Vector3 a, Vector2 uva, Vector2 uvb, Vector2 uvc,
            double[] axisU, double[] axisV, double det)
        {
            _u0 = uva.X;
            _v0 = uva.Y;
            _u1 = uvb.X;
            _v1 = uvb.Y;
            _u2 = uvc.X;
            _v2 = uvc.Y;
            _p0X = a.X;
            _p0Y = a.Y;
            _p0Z = a.Z;
            _aX = axisU[0];
            _aY = axisU[1];
            _aZ = axisU[2];
            _bX = axisV[0];
            _bY = axisV[1];
            _bZ = axisV[2];
            _sign = det > 0 ? 1.0 : -1.0;
            MinU = (int)Math.Floor(Math.Min(_u0, Math.Min(_u1, _u2)));
            MaxU = (int)Math.Floor(Math.Max(_u0, Math.Max(_u1, _u2)));
            MinV = (int)Math.Floor(Math.Min(_v0, Math.Min(_v1, _v2)));
            MaxV = (int)Math.Floor(Math.Max(_v0, Math.Max(_v1, _v2)));
        }

        /// <summary>The UV bounding box floored to integers (step 4), inclusive on both ends.</summary>
        public int MinU { get; }

        /// <inheritdoc cref="MinU"/>
        public int MaxU { get; }

        /// <inheritdoc cref="MinU"/>
        public int MinV { get; }

        /// <inheritdoc cref="MinU"/>
        public int MaxV { get; }

        /// <summary>Integer lattice cells the walk would visit. <c>long</c> because a corrupt UV
        /// array could overflow an <c>int</c> before the caller's bound ever saw it.</summary>
        public long CellCount => ((long)MaxU - MinU + 1) * ((long)MaxV - MinV + 1);

        /// <summary>World displacement per +1 of U — A2's <c>A</c>. Public for the test that pins
        /// the worked example; the walk uses <see cref="World"/>.</summary>
        public Vector3 AxisU => new((float)_aX, (float)_aY, (float)_aZ);

        /// <summary>World displacement per +1 of V — A2's <c>B</c>.</summary>
        public Vector3 AxisV => new((float)_bX, (float)_bY, (float)_bZ);

        /// <summary>Null when the triangle is degenerate in either space, with
        /// <paramref name="fault"/> saying which — the caller counts both, because they are
        /// different defects in the source mesh and netting them hides one.</summary>
        public static UvTriangle? Build(Vector3 a, Vector3 b, Vector3 c,
            Vector2 uva, Vector2 uvb, Vector2 uvc, out UvTriangleFault fault)
        {
            double[] e1 = { b.X - (double)a.X, b.Y - (double)a.Y, b.Z - (double)a.Z };
            double[] e2 = { c.X - (double)a.X, c.Y - (double)a.Y, c.Z - (double)a.Z };
            double[] n = Cross(e1, e2);
            if ((n[0] * n[0]) + (n[1] * n[1]) + (n[2] * n[2]) < MinWorldArea2)
            {
                fault = UvTriangleFault.ZeroWorldArea;
                return null;
            }

            double du1 = uvb.X - (double)uva.X, dv1 = uvb.Y - (double)uva.Y;
            double du2 = uvc.X - (double)uva.X, dv2 = uvc.Y - (double)uva.Y;
            double det = (du1 * dv2) - (du2 * dv1);
            if (Math.Abs(det) < MinUvArea2)
            {
                fault = UvTriangleFault.ZeroUvArea;
                return null;
            }

            // Solve A·du1 + B·dv1 = e1 and A·du2 + B·dv2 = e2 for the two world axes. This is
            // the same map step 7 builds, written as a 2×2 inverse rather than a ray-cast.
            var axisU = new double[3];
            var axisV = new double[3];
            for (int k = 0; k < 3; k++)
            {
                axisU[k] = ((e1[k] * dv2) - (e2[k] * dv1)) / det;
                axisV[k] = ((e2[k] * du1) - (e1[k] * du2)) / det;
            }

            fault = UvTriangleFault.ZeroUvArea;   // unused on the success path
            return new UvTriangle(a, uva, uvb, uvc, axisU, axisV, det);
        }

        /// <summary>Step 6: the three edge cross-products in UV space, all on the side the
        /// triangle's own winding says is inside. Inclusive on the edge, which matches the world
        /// test this replaces — A3 traced C5's two off-by-one instances to exactly this boundary
        /// in float32, which is why the arithmetic here is double.</summary>
        public bool Contains(double u, double v)
        {
            double w0 = (((_u1 - _u0) * (v - _v0)) - ((_v1 - _v0) * (u - _u0))) * _sign;
            double w1 = (((_u2 - _u1) * (v - _v1)) - ((_v2 - _v1) * (u - _u1))) * _sign;
            double w2 = (((_u0 - _u2) * (v - _v2)) - ((_v0 - _v2) * (u - _u2))) * _sign;
            return w0 >= 0 && w1 >= 0 && w2 >= 0;
        }

        /// <summary>Step 7: the world position at a texture coordinate. The single rounding of
        /// the whole walk happens here.</summary>
        public Vector3 World(double u, double v)
        {
            double du = u - _u0, dv = v - _v0;
            return new Vector3(
                (float)(_p0X + (_aX * du) + (_bX * dv)),
                (float)(_p0Y + (_aY * du) + (_bY * dv)),
                (float)(_p0Z + (_aZ * du) + (_bZ * dv)));
        }
    }

    /// <summary>A template's ground quad, reduced to what placement needs: which terrain
    /// texture it decorates, its per-axis local extent, and the affine map from a local
    /// position on its plane to the polygon's own interpolated texture UV — which is the
    /// coordinate every decoration is stored in (<c>FUN_004dd230</c>).</summary>
    public sealed class GroundQuad
    {
        // The quad polygon's triangles, in the template's local space (containment only).
        public readonly List<Vector3> Faces = new();

        public string Texture = "";
        // The quad's own local X/Z extents. Descriptive only since B12 deleted the world grid:
        // NOTHING in placement reads them any more, and nothing should — the lattice spacing
        // comes from the WORLD polygon's UVs, not from the template's size.
        public float ExtentX, ExtentZ;

        // The plane and the UV map, in DOUBLE — see the note in GroundInfo for why the single
        // rounding at the end of TryUv is load-bearing rather than pedantry.
        public double NormalX, NormalY, NormalZ;         // unit plane normal
        public double AnchorX, AnchorY, AnchorZ;         // a point on the plane…
        public double AnchorU, AnchorV;                  // …and the UV there
        public double GradUX, GradUY, GradUZ;            // d(u) per metre of local displacement
        public double GradVX, GradVY, GradVZ;            // d(v) likewise

        // FUN_004dd230's fmod wrap, both branches. A negative coordinate maps to 1 − frac, and
        // an exact 1.0 (a frac that rounded away) collapses back to 0. No retail decoration
        // takes the negative branch — all 32 resolving quads span exactly 0..1, so the wrap
        // folds nothing — but the original takes it, so this does.
        public static float Wrap(float value)
        {
            float f = value % 1f;
            if (f < 0f)
            {
                f += 1f;
                if (f >= 1f)
                    f = 0f;
            }
            return f;
        }

        /// <summary>The quad's interpolated texture UV under <paramref name="local"/>, projected
        /// along the quad normal. False when the decoration does not project onto the polygon —
        /// the original logs and skips that case, and so does the caller.</summary>
        public bool TryUv(Vector3 local, out Vector2 uv)
        {
            // FUN_004dd230 ray-casts the decoration ±5 along the quad normal, so a decoration
            // further off the plane than that reaches nothing to project onto.
            const double rayReach = 5.0;

            double ox = local.X - AnchorX, oy = local.Y - AnchorY, oz = local.Z - AnchorZ;
            double dist = (NormalX * ox) + (NormalY * oy) + (NormalZ * oz);
            double fx = ox - (NormalX * dist), fy = oy - (NormalY * dist), fz = oz - (NormalZ * dist);
            uv = new Vector2(
                (float)(AnchorU + (GradUX * fx) + (GradUY * fy) + (GradUZ * fz)),
                (float)(AnchorV + (GradVX * fx) + (GradVY * fy) + (GradVZ * fz)));
            var foot = new Vector3(
                (float)(AnchorX + fx), (float)(AnchorY + fy), (float)(AnchorZ + fz));
            return Math.Abs(dist) <= rayReach && Contains(foot);
        }

        private bool Contains(Vector3 p)
        {
            // Barycentric slack: an authored decoration sitting exactly on the quad's edge
            // belongs to the quad.
            const float edgeSlack = 1e-4f;

            for (int i = 0; i + 2 < Faces.Count; i += 3)
            {
                Vector3 a = Faces[i], b = Faces[i + 1], c = Faces[i + 2];
                var n = (b - a).Cross(c - a);
                float d = n.LengthSquared();
                if (d < 1e-9f)
                    continue;
                // Barycentric weights via the triangle's own normal, so the test works on a
                // tilted quad as well as a flat one.
                float w0 = (b - a).Cross(p - a).Dot(n) / d;
                float w1 = (c - b).Cross(p - b).Dot(n) / d;
                float w2 = (a - c).Cross(p - c).Dot(n) / d;
                if (w0 >= -edgeSlack && w1 >= -edgeSlack && w2 >= -edgeSlack)
                    return true;
            }
            return false;
        }
    }

    // One decoration kind: every template instance of the same decoration mesh (all 13
    // firtree1 placements share mesh + texture), plus where it sits in each grid cell.
    private sealed class Kind
    {
        // Where each decoration of this kind sits on the template's ground quad, as the quad's
        // own TEXTURE UV: Origin.X is u and Origin.Z is v, both in [0, 1) — not metres, and not
        // relative to a corner (the winding differs between templates). Origin Y and the basis
        // are the decoration node's own, and are used by the solid path only (a sprite is
        // planted flat on the surface and re-faced by its shader — see PlaceOnTriangle).
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

    // A template carries no metric size at all any more. The ground quad's extents told the old
    // world grid how far apart to stamp; the lattice asks the WORLD POLYGON's UVs instead, so
    // the only thing a template needs to know about its quad is which texture it decorates.
    private sealed class Template
    {
        public readonly List<Kind> Kinds = new();

        public string GroundTexture = "";
    }

    // What one Build's lattice walk refused, and what it asserted about what it kept. Reported
    // as one line rather than per triangle: the counts are in the thousands, and an absent
    // category must be distinguishable from a truncated one (LOG-5, DIAG-15).
    private sealed class LatticeStats
    {
        public int ZeroWorldArea;      // fan/strip artifacts of n-gons with repeated corners
        public int ZeroUvArea;         // no invertible affine map to recover a position through
        public int NoUvArray;          // FUN_004de2c0's null-UV gate
        public int NoClutterFlagged;   // FUN_004de2c0's 0x800 gate — the OTHER layer decorates here
        public int OverLargeLattice;   // refused by MaxLatticeCells
        public long WorstLatticeCells; // the largest lattice seen among those refused
        public int Placed;             // instances the lattice produced (after the seen dedup)
        public int OutsideSource;      // …of which any that missed their own source triangle

        public void Report()
        {
            string worst = OverLargeLattice > 0 ? $" worst_lattice_cells={WorstLatticeCells}" : "";
            Log.Info("world", $"clutter uv lattice: placed={Placed} outside_source={OutsideSource} skipped_no_clutter_flag={NoClutterFlagged} skipped_zero_world_area={ZeroWorldArea} skipped_zero_uv_area={ZeroUvArea} skipped_no_uv_array={NoUvArray} skipped_over_large_lattice={OverLargeLattice}{worst}");
            if (OutsideSource > 0)
                Log.Warn("world", $"clutter instances landed OUTSIDE their source triangle count={OutsideSource} of {Placed} — the UV containment test disagrees with the affine map");
        }
    }
}
