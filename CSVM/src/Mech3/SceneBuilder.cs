using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Turns GameZ node subtrees into renderable Godot node trees: recursion over the
/// node hierarchy, n-gon triangulation, and a shared material cache. Callers
/// (PlaneBuilder, WorldBuilder) supply what to skip; the LOD rule (keep only the
/// nearest level of each LOD group) is common and lives here.
/// Polygons carry a draw priority (GameZPolygon.Priority) the original engine uses to
/// layer coplanar geometry (terrain-transition patches, road/shadow decals, plane
/// logos over the fuselage, skydome behind everything); non-zero priorities become a
/// per-surface depth bias — a view-space pull toward the eye — so those layers
/// resolve without z-fighting at any viewing distance.
/// </summary>
public sealed class SceneBuilder
{
    /// <summary>Meta key a collider carries when its dominant surface is water or buildings.</summary>
    public const string SurfaceMeta = "csky_surface";

    public const string OpacityParam = "csky_opacity";

    // Equal-priority tie-break applied per-instance (nodes.json DFS order = draw order):
    // the airfield's apron detail over its base tile, road decals over rail decals. 2000x
    // smaller than DepthBiasPerLevel below, so it never competes with a real priority step.
    public const float NodeOrderBias = 5e-8f;

    /// <summary>Surfaces given a CLAMPed sampler because their UVs never leave the unit square
    /// (the hairline-seam fix — see <see cref="UvsWithinUnitSquare"/>). Process-wide across every
    /// builder, purely for the load log; it is also what tells a capture which build made it.</summary>
    public static int ClampedSurfaceTotal;

    /// <summary>Where a material's own texture flipbook (the gamez `cycle` block) is delivered.
    /// Set by the caller before building; null leaves cycling materials static.</summary>
    public TextureCycler? Cycler;

    // Shared shader source lives in res://shaders/*.gdshaderinc, pulled in by Godot's shader
    // preprocessor. Verified that #include resolves in a Shader whose Code is assigned
    // at RUNTIME from C#, not only in a .gdshader loaded from disk — the probe declared a uniform
    // inside the include and read it back through GetShaderUniformList().
    //
    // Why files rather than C# const strings: these blocks are shared by four independently
    // generated shaders (this one, the cloud billboards, the cylindrical facades, and Clutter's
    // trees), and three of them used to be copy-pasted duplicates. A const string
    // single-sources the text but still lets each shader choose whether and where to emit it;
    // for the instance-uniform block that choice is exactly the bug (see the ordering contract in
    // csky_instance_uniforms.gdshaderinc). The combinatorial parts — render_mode, the
    // blend/scissor/scroll/clamp variants — stay generated here, because they are 128 shader
    // variants rather than one file.
    internal const string InstanceUniformsInclude =
        "#include \"res://shaders/csky_instance_uniforms.gdshaderinc\"";
    internal const string SrgbInclude =
        "#include \"res://shaders/csky_srgb.gdshaderinc\"";
    internal const string AtmosphereInclude =
        "#include \"res://shaders/csky_atmosphere.gdshaderinc\"";
    internal const string LightsInclude =
        "#include \"res://shaders/csky_lights.gdshaderinc\"";
    internal const string TimeInclude =
        "#include \"res://shaders/csky_time.gdshaderinc\"";

    /// <summary>The animation runtime's <c>OBJECT_OPACITY_STATE</c> translucency, as a
    /// per-instance multiplier on ALPHA (C1 fades its cloud deck to 0.6, C5 its window glows
    /// to 0.4). Per-instance rather than per-material because materials are cached and shared
    /// — two nodes on one material can be told different opacities, and unlike a scroll rate
    /// this one changes at runtime, so it cannot live in the cache key the way texture_scroll
    /// does.
    /// <para><b>Declaration and use are deliberately split.</b> The
    /// <i>declaration</i> lives in the shared ordered preamble
    /// (<c>csky_instance_uniforms.gdshaderinc</c>), so every shader that carries any instance
    /// uniform declares it — that is what makes the indices agree. The <i>use</i>, this term, is
    /// still emitted ONLY into variants that already write ALPHA: an opaque variant has no alpha
    /// path to multiply, and adding one would move it into the transparent pass. Declaring the
    /// uniform does not create an alpha path, so nothing moved passes.</para>
    /// <para>The old note that omitting the declaration kept opaque materials "off the
    /// instance-uniform buffer that had to be enlarged for C4/C5" does not apply to the
    /// bias shader: it always declares <c>node_bias</c> and <c>csky_fog_on</c>, so those
    /// instances were already on that buffer, and Godot's per-instance allocation is a fixed
    /// 16-vec4 block regardless of how many uniforms a shader declares. It DOES still apply to
    /// the opaque sprite variants, which declare nothing — and those deliberately do not take
    /// the preamble.</para>
    /// <para>⚠ <see cref="AnimRuntime"/> tests for this term, not the uniform name: since the
    /// declaration is now everywhere, only the term distinguishes a shader that actually reads
    /// opacity.</para></summary>
    internal const string OpacityTerm = " * csky_opacity";

    private const string LightShaderCode = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform float size_scale = 1.0;   // data unk08 (0 -> 1)
uniform float max_size_px = 30.0; // data unk64
uniform float range_far = 0.0;    // data unk68/unk52; 0 = no distance fade
#include ""res://shaders/csky_instance_uniforms.gdshaderinc""
// ^ reads csky_light_fade (0 = skydome stars, no range fade) and csky_opacity
//   (OBJECT_OPACITY_STATE); node_bias and csky_fog_on come along unused, which is the
//   price of one shared ordered block and costs nothing per instance.
void vertex() {
    float dist = max(length((MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz), 1.0);
    // A fixed world diameter projected to pixels, clamped: far lights stay visible
    // star-sized dots, near lights cap at the data's max sprite size. TUNE: 6 m glow.
    float px = 6.0 * size_scale * PROJECTION_MATRIX[1][1] * VIEWPORT_SIZE.y / (2.0 * dist);
    POINT_SIZE = clamp(px, 3.0, max_size_px);
    float fade = (range_far > 0.0)
        ? 1.0 - smoothstep(0.6 * range_far, range_far, dist) : 1.0;
    COLOR.a = mix(1.0, fade, csky_light_fade);
}
void fragment() {
    float r = length(POINT_COORD - vec2(0.5)) * 2.0;
    // Soft star-like glow: hot gaussian core, no hard edge.
    float glow = exp(-r * r * 5.0) * smoothstep(1.0, 0.6, r);
    ALBEDO = COLOR.rgb * 1.6; // brightness gain, TUNE
    ALPHA = glow * COLOR.a * csky_opacity;
}";

    // The source UVs are exact 0.0/1.0 at the fold, so this only absorbs float noise.
    private const float UvEpsilon = 1e-6f;

    // Depth-bias fraction per priority level. Polygons are pulled toward the eye by
    // priority × this fraction of their view distance — same projected position, nearer
    // depth — replicating the original's coplanar-decal layering (terrain patches,
    // road/shadow decals, plane logos) without z-fighting at any range.
    // TUNE: big enough to beat coplanar interpolation noise at 10 km, small enough that
    // the ±49 extremes (cockpit gauges, skydome) stay well under 1% of view distance.
    private const float DepthBiasPerLevel = 2e-4f;
    // Equal-priority tie-breaks, reproducing the original's draw order (drawn later =
    // on top). Both strictly subordinate to one priority level:
    // — within a mesh, by surface rank (first-occurrence order of the (material,
    //   priority) group in the polygon list): tile roads/shoreline blends over the
    //   tile's base grass; contribution capped so it stays below cross-node steps.
    // — across nodes, by the node's flat index (nodes.json is a DFS serialization =
    //   draw order), applied as a per-instance shader parameter: the airfield's apron
    //   detail over its base tile, road decals over rail decals.
    private const float SurfaceRankBias = 2e-6f;
    private const int SurfaceRankCap = 5;
    // The OpenFlight SUBFACE offset (GameZPolygon.Subface): a face marked coplanar-with-and-
    // contained-in the one beneath it draws on top of it. The original applies ONE WHOLE
    // priority level to this — `GameGenSetSubfacePriorityOffset 1` in support\init.gw, global
    // to every mission. We deliberately apply HALF a level instead: priority 1 is a genuinely
    // authored value (955 C5 polygons, 2207 in C1), and a full level would make a subface tie
    // with a real priority-1 overlay. Measured across every subface/base overlap in C5, 0.5 and
    // 1.0 of a level resolve identically (25,732,146 m² front / 120,999 behind either way), so
    // the smaller offset is free. Still 50x SurfaceRankBias and 2000x NodeOrderBias.
    private const float SubfaceBias = DepthBiasPerLevel * 0.5f;

    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly bool _fullbright;
    private readonly bool _generateCollision;
    private readonly bool _cullBackfaces;
    private readonly Func<string, bool>? _blendTexture;
    private readonly Func<string, bool>? _billboardTexture;
    private readonly Func<string, bool>? _glowTexture;
    private readonly Func<string, ImageTexture?, ImageTexture?>? _textureSubstitute;
    private readonly IReadOnlyDictionary<int, Vector2>? _scrollOverrides;
    // Keyed by scroll rate as well as the usual four, because a scrolling model can share its
    // material with non-scrolling geometry (C1B's `con_scroll` shares oildock1.tif with five
    // static dock models) and even with a model scrolling at a DIFFERENT rate (C1B's three
    // wakefronts all use wakefront1.tif at 1.0 / 0.7 / 0.7 u/s). Non-scrolling surfaces all
    // key on (0,0), so the common path's cache behaviour is exactly what it was.
    // ClampUv is part of the key because it selects a different sampler wrap mode, and two
    // surfaces on the same material can disagree about it (a 0..1 mapped tile and a tiling
    // one share plenty of textures) — see UvsWithinUnitSquare.
    // Subface is part of the key for the same reason Priority is: it selects a different depth
    // bias, and one material legitimately skins both roles (C5's cblock1/2/3 appear 236/91/52
    // times as a plain face and 96/121/98 times as a subface over the cblock4/5/6 base).
    private readonly Dictionary<(int Material, int Priority, int Rank, bool Subface, bool DoubleSided, float ScrollU, float ScrollV, bool ClampUv), Material> _materialCache = new();
    private readonly Dictionary<int, Shader> _biasShaderCache = new(); // keyed by feature bits
    private readonly Dictionary<int, Shader> _billboardShaderCache = new(); // cloud sprites, keyed by blend/scissor bits
    private readonly Dictionary<int, Shader> _cylindricalShaderCache = new(); // Y/X-axis facades, keyed by axis/blend/scissor/glow bits
    private readonly Dictionary<int, ArrayMesh?> _meshCache = new();
    private readonly Dictionary<int, Vector3> _meshPivotCache = new(); // billboard meshes only: local quad center
    private readonly Dictionary<int, ArrayMesh> _lightMeshCache = new();
    private readonly Dictionary<int, ConcavePolygonShape3D?> _shapeCache = new();
    private readonly Dictionary<(float Size, float MaxPx, float Range), ShaderMaterial> _lightMaterialCache = new();
    private readonly Dictionary<int, string?> _surfaceCache = new();
    // Billboard glow material for a flare sprite quad: always alpha-blended (the soft ramp
    // must never scissor into a hard star cutout), no night dimming (it's a light source).
    private readonly Dictionary<int, Material> _glowMaterialCache = new();
    // Cylindrical (Y- or X-axis) billboard material: unlike glow flares this respects the
    // texture's own alpha classification (trees/cables are hard-edge cutouts — Clutter's
    // tiled trees already scissor, and these individually-placed Facade trees should read
    // the same way; fire/flame sprites are soft and blend), and dims with the world's
    // SUNLIGHT UNLESS the texture is a light source itself (the caller's glowTexture
    // predicate — the same delegate the legacy spherical fallback uses, so one rule governs
    // every light-vs-scenery billboard in the renderer; WorldBuilder widened it
    // to also catch the refinery's own gas flame, fire101.tif, which isn't "*flare*"-named).
    private readonly Dictionary<(int Material, int Axis), Material> _cylindricalMaterialCache = new();
    // Every textured material this builder made, paired with the texture name it resolved
    // from — the registry a live repaint needs (the viewer's livery lab re-runs the paint
    // and swaps each material's albedo in place, instead of rebuilding the whole aircraft
    // for every slider pixel). Only shader materials carrying an `albedo_tex` are listed.
    private readonly List<(ShaderMaterial Material, string TextureName)> _texturedMaterials = new();
    private Shader? _lightShader;

    /// <param name="fullbright">Render unshaded, like the original engine's world pass:
    /// texture × baked vertex color, ignoring scene lights. Used for world geometry.</param>
    /// <param name="generateCollision">Attach a static trimesh collider to each mesh, so
    /// the flight loop can raycast against it (used for the flyable world; a per-subtree
    /// predicate can still exempt non-solid geometry like clouds).</param>
    /// <param name="blendTexture">Given a material's texture name, true to alpha-BLEND it
    /// (soft transparency, no depth write) instead of the default alpha-SCISSOR (1-bit
    /// cutout). Used for the cloud layers, whose soft sprites AlphaScissor binarizes into
    /// hard gray facets. Only consulted for textures that actually carry alpha; opaque and
    /// non-blended alpha textures (fences, trees) keep AlphaScissor.</param>
    /// <param name="billboardTexture">Given a material's texture name, true to render its
    /// meshes as camera-facing billboards (the cloud sprites — flat 2D cards in the source).
    /// Such a mesh is recentered on its own quad center so the billboard pivots there, not at
    /// the node origin (the source quads sit offset from it). Use only for genuinely 2D,
    /// origin-local sprites; NOT for the horizontal cloudlayer deck, which must stay flat.</param>
    /// <param name="cullBackfaces">Backface-cull polygons not flagged SHOW_BACKFACE
    /// ("unk2"), like the original engine — hides inward-facing interior structure (the
    /// autogyro's frame lattice behind its fuselage openings). Used for aircraft; the
    /// world keeps rendering double-sided until validated the same way.</param>
    /// <param name="glowTexture">Given a material's texture name, true for light-source flare
    /// sprites (lamp/beacon glow quads, `*flare*` textures): billboarded like the cloud
    /// sprites, always alpha-blended, and exempt from the `csky_world_light` night dimming —
    /// a lamp emits light, it doesn't get darker at night.</param>
    /// <param name="textureSubstitute">Given a material's texture name and the texture the
    /// archive resolved for it, returns the texture to actually use. The aircraft paint pass
    /// (<see cref="PlanePainter"/>) hooks in here to hand back a recoloured skin or a swapped
    /// decal — per plane instance, so the shared archive cache is never mutated. The
    /// substitute must keep the original's alpha class (opaque vs cutout vs soft), since the
    /// blend/scissor decision is already made from the archive's classification.</param>
    /// <param name="scrollOverrides">Per-MODEL UV scroll rates (units/second) that replace the
    /// model's own <see cref="GameZMesh.TextureScroll"/>. This is where the interp boot script's
    /// <c>Object3DSetScroll</c> arrives: the verb writes the selected node's model scroll field —
    /// which is why a chapter's <c>tex_fx.gw</c> rates are already baked into its gamez while the
    /// per-mission ones are not — so a per-model table is the engine's own granularity. See
    /// <see cref="MissionSetup.ScrollByModel"/> and <c>docs/formats/interp.md</c>.</param>
    public SceneBuilder(GameZ gamez, TextureArchive textures, bool fullbright = false,
        bool generateCollision = false, Func<string, bool>? blendTexture = null,
        Func<string, bool>? billboardTexture = null, bool cullBackfaces = false,
        Func<string, bool>? glowTexture = null,
        Func<string, ImageTexture?, ImageTexture?>? textureSubstitute = null,
        IReadOnlyDictionary<int, Vector2>? scrollOverrides = null)
    {
        _textureSubstitute = textureSubstitute;
        _scrollOverrides = scrollOverrides;
        _gamez = gamez;
        _textures = textures;
        _fullbright = fullbright;
        _generateCollision = generateCollision;
        _blendTexture = blendTexture;
        _billboardTexture = billboardTexture;
        _cullBackfaces = cullBackfaces;
        _glowTexture = glowTexture;
    }

    /// <summary>How the original engine spins one model toward the camera: not at all, fully
    /// (a light-source glow), or about one fixed axis (an upright tree card, a flame on a
    /// horizontal pipe). This is the gamez model's OWN classification, not a guess of ours.</summary>
    public enum BillboardKind { None, Spherical, CylindricalY, CylindricalX }

    // The rendering-side spelling of ClassifyBillboard's two cylindrical cases: the mesh
    // spins about one fixed world axis to face the camera on the other two, so a tree card or
    // a flame stays upright instead of tipping over like a light glow. Kept as its own enum
    // because it also keys the shader/material caches below. None on a legacy extraction (no
    // per-axis fallback ever existed — those meshes rendered static, as they always did).
    private enum CylAxis { None, Y, X }

    public int MeshInstanceCount { get; private set; }
    public int ColliderCount { get; private set; }

    /// <summary>Models built with a non-zero UV scroll rate, from either source (the model's own
    /// <c>texture_scroll</c> or the boot script). Logged per world build: it is the one-line
    /// evidence that a chapter animates exactly the surfaces the data says and no others.</summary>
    public int ScrollingModelCount { get; private set; }

    /// <summary>The textured materials built here, each with its source texture name, so a
    /// caller can re-resolve and swap them without a rebuild. See <see cref="Repaint"/>.</summary>
    public IReadOnlyList<(ShaderMaterial Material, string TextureName)> TexturedMaterials => _texturedMaterials;

    /// <summary>
    /// The one billboard rule, read straight from the gamez model.
    /// Every caller that needs to know "is this a flat card
    /// the engine turns toward the camera?" goes through this — the two rendering paths
    /// below, the world's collision exemption (<c>WorldBuilder.NoCollisionNode</c>) and the
    /// clutter template's sprite-vs-3D-decoration split (<c>ClutterBuilder</c>).
    ///
    /// <para><b>Returns null when the extraction carries no <c>ModelType</c></b> — a legacy
    /// v0.6.1 tree, where these fields do not exist at all. Null means "no data, apply your
    /// own fallback": the callers' fallbacks differ because they encode different intents
    /// (a flare texture name for the glow path, a flat-quad shape for clutter), and folding
    /// them together here would silently change what a legacy tree renders. See the
    /// GameZ.cs bullet in docs/architecture.md for the two extraction shapes.</para>
    ///
    /// <para><b><c>ModelType=="Facade"</c> is the discriminator, NOT <c>FacadeMode</c>
    /// alone.</b> A Default-typed model can carry a stale <c>FacadeMode</c>: C1's multi-poly
    /// <c>flare_green</c> "strings" are Default/CylindricalY and must stay static (as one
    /// sprite they would swing around a shared centroid), and every 3D city-block building
    /// in C2/C5's clutter templates is Default/CylindricalY too. Gating on the type first is
    /// what excludes both without a polygon-count guess. Surveyed across all 8 chapters:
    /// no Facade model anywhere exceeds 3 polygons, so this can never catch real geometry.</para>
    /// </summary>
    public static BillboardKind? ClassifyBillboard(GameZMesh mesh)
    {
        if (mesh.ModelType == null)
            return null; // legacy extraction — caller falls back
        if (mesh.ModelType != "Facade")
            return BillboardKind.None;
        return mesh.FacadeMode switch
        {
            "SphericalY" => BillboardKind.Spherical,
            "CylindricalY" => BillboardKind.CylindricalY,
            "CylindricalX" => BillboardKind.CylindricalX,
            _ => BillboardKind.None,
        };
    }

    /// <summary>Builds the subtree rooted at <paramref name="node"/>; null if skipped entirely.</summary>
    /// <param name="skip">Subtrees to drop entirely (not rendered, no collision).</param>
    /// <param name="collisionSkip">Subtrees to render but exempt from collision (e.g. clouds);
    /// the exemption applies to the node and all its descendants.</param>
    public Node3D? BuildSubtree(GameZNode node, Predicate<GameZNode>? skip = null,
        Predicate<GameZNode>? collisionSkip = null) =>
        BuildSubtree(node, skip, collisionSkip, _generateCollision);

    /// <summary>Re-resolves every textured material through the current substitution hook and
    /// writes the result back into the live material. Used by the viewer's livery lab: the
    /// substitute closure reads a mutable painter, so changing the scheme and calling this
    /// repaints the built aircraft in place.</summary>
    public void Repaint()
    {
        foreach (var (mat, texName) in _texturedMaterials)
            if (Resolve(texName) is { } tex)
                mat.SetShaderParameter("albedo_tex", tex);
    }

    /// <summary>The built <see cref="ArrayMesh"/> for one gamez model index, from this
    /// builder's shared cache and carrying this builder's materials (so a world builder hands
    /// back fullbright, fogged, correctly depth-biased world geometry).
    ///
    /// <para>Exists for <see cref="ClutterBuilder"/>'s 3D-decoration path: a city-block
    /// building is placed tens of thousands of times, so it is drawn from ONE MultiMesh over
    /// this single mesh rather than a node per copy. Everything the node path adds around the
    /// mesh — the transform, the <c>node_bias</c> instance uniform, the collider — is the
    /// caller's to supply, which is why this returns the mesh and not a node.</para></summary>
    internal ArrayMesh? SharedMesh(int meshIndex) =>
        meshIndex >= 0 && meshIndex < _gamez.Meshes.Count ? GetMesh(meshIndex) : null;

    private static string? ClassifySurface(string? texture)
    {
        if (string.IsNullOrEmpty(texture))
            return null;
        var t = texture.ToLowerInvariant();
        // 'bldgshadow'/'bld_shadow' are baked ground shadow decals, not building geometry.
        if (t.Contains("shadow"))
            return t.StartsWith("water") || t.Contains("splash") ? "water" : null;
        if (t.StartsWith("water") || t.StartsWith("wtr") || t.StartsWith("srf")
            || t.Contains("wakefront") || t.Contains("watersquirt"))
            return "water";
        if (t.Contains("build") || t.StartsWith("hangar") || t.StartsWith("bld")
            || t.Contains("cblock") || t.Contains("warehouse") || t.Contains("roof")
            || t.Contains("filmblock"))
            return "buildings";
        return null;
    }

    /// <summary>
    /// True when every UV of this surface lies inside the unit square, i.e. the texture is
    /// mapped once and never tiled — which is what makes a CLAMPed sampler safe for it.
    /// <para>
    /// This is the hairline-seam fix. The terrain's UVs are a
    /// <b>mirrored triangle wave</b>: U rises to exactly 1.0 and folds back rather than
    /// wrapping to 0, which is how the artists tiled non-seamless textures seamlessly (it is
    /// also the mirror symmetry visible across C4's river). Under <c>repeat_enable</c> the
    /// bilinear filter's second tap at the fold wraps to texel 0 — the opposite edge of the
    /// texture — and blends it in over a band one texel wide. On C4's <c>river3.tif</c>
    /// (column 0 tan, column 63 blue-green) that is the tan hairline crossing blue water.
    /// Measured: the seam peaks at exactly the 50/50 blend of the two edge columns
    /// (predicted (86.5, 91.0, 74.0), measured (89.5, 92.5, 77.2)), and the background is
    /// identical on both sides of it — the signature of a fold, not of a texture
    /// discontinuity, which would have to step.
    /// </para>
    /// <para>
    /// Clamping is safe <b>by construction</b> when this returns true: with no UV outside
    /// [0,1] the wrap is never exercised, so CLAMP and REPEAT can only differ within half a
    /// texel of the edge — exactly the artifact. A blanket clamp is NOT safe and was measured
    /// to be wrong: 54% of this install's surfaces genuinely tile (U reaches 407), and
    /// forcing clamp on them changes 80% of the C5 city pose.
    /// </para>
    /// </summary>
    private static bool UvsWithinUnitSquare(List<GameZPolygon> polys)
    {
        bool any = false;
        foreach (var poly in polys)
        {
            if (poly.UvCoords == null)
                continue;
            foreach (var uv in poly.UvCoords)
            {
                any = true;
                if (uv.X < -UvEpsilon || uv.X > 1f + UvEpsilon ||
                    uv.Y < -UvEpsilon || uv.Y > 1f + UvEpsilon)
                    return false;
            }
        }
        return any; // no UVs at all ⇒ nothing to clamp; keep the surface on the old path
    }

    private static CylAxis GetCylindricalAxis(GameZMesh mesh) => ClassifyBillboard(mesh) switch
    {
        BillboardKind.CylindricalY => CylAxis.Y,
        BillboardKind.CylindricalX => CylAxis.X,
        _ => CylAxis.None,
    };

    private static void EmitPolygon(SurfaceTool st, GameZMesh mesh, GameZPolygon poly, Vector3 offset)
    {
        int n = poly.VertexIndices.Count;
        if (n < 3)
            return;
        if (poly.TriangleStrip)
        {
            for (int i = 0; i + 2 < n; i++)
            {
                // alternate winding so all triangles of the strip face the same way
                if ((i & 1) == 0)
                    EmitTriangle(st, mesh, poly, i, i + 1, i + 2, offset);
                else
                    EmitTriangle(st, mesh, poly, i, i + 2, i + 1, offset);
            }
        }
        else
        {
            for (int i = 1; i + 1 < n; i++)
                EmitTriangle(st, mesh, poly, 0, i, i + 1, offset);
        }
    }

    private static void EmitTriangle(SurfaceTool st, GameZMesh mesh, GameZPolygon poly, int a, int b, int c, Vector3 offset)
    {
        Vector3 Pos(int corner) => mesh.Vertices[poly.VertexIndices[corner]] - offset;
        // flat normal fallback for polygons without normal data
        var flat = (Pos(b) - Pos(a)).Cross(Pos(c) - Pos(a));
        flat = flat.LengthSquared() > 1e-12f ? flat.Normalized() : Vector3.Up;

        foreach (var corner in stackalloc[] { a, b, c })
        {
            var normal = flat;
            if (poly.NormalIndices != null && corner < poly.NormalIndices.Count)
            {
                int ni = poly.NormalIndices[corner];
                if (ni >= 0 && ni < mesh.Normals.Count && mesh.Normals[ni].LengthSquared() > 1e-12f)
                    normal = mesh.Normals[ni].Normalized();
            }
            st.SetNormal(normal);
            st.SetColor(poly.VertexColors != null && corner < poly.VertexColors.Count
                ? poly.VertexColors[corner]
                : Colors.White);
            if (poly.UvCoords != null && corner < poly.UvCoords.Count)
                st.SetUV(poly.UvCoords[corner]);
            st.AddVertex(Pos(corner));
        }
    }

    private static string Sanitize(string name)
    {
        // Godot node names must not contain . : @ / " %
        Span<char> bad = stackalloc[] { '.', ':', '@', '/', '"', '%' };
        foreach (var ch in bad)
            name = name.Replace(ch, '_');
        return name.Length == 0 ? "node" : name;
    }

    private Node3D? BuildSubtree(GameZNode node, Predicate<GameZNode>? skip,
        Predicate<GameZNode>? collisionSkip, bool collidable)
    {
        if (skip != null && skip(node))
            return null;
        // Of each LOD group, keep only the highest-detail level (range starts at 0).
        if (node.Kind == "Lod" && node.LodRangeMin != 0f)
            return null;
        if (collidable && collisionSkip != null && collisionSkip(node))
            collidable = false;

        var n3d = new Node3D { Name = Sanitize(node.Name) };
        // The ORIGINAL gamez name, for name-based resolution (AnimRuntime): Godot both
        // sanitizes ('.'→'_') and auto-renames duplicate siblings, so Name is unreliable.
        n3d.SetMeta(AnimRuntime.NameMeta, node.Name);
        // The node's flat gamez list position. Compiled animation definitions reference
        // their objects by exactly this index (verified: 136,048 refs across all 8 chapters
        // resolve exactly), which binds them unambiguously — names alone are duplicated and
        // carry a '.flt' suffix inconsistently. See AnimRuntime.
        n3d.SetMeta(AnimRuntime.IndexMeta, node.Index);
        if (node.Local is { } local)
            n3d.Transform = local;

        if (node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count)
        {
            var mesh = GetMesh(node.MeshIndex);
            if (mesh != null)
            {
                var mi = new MeshInstance3D { Mesh = mesh, Name = "mesh" };
                // Cross-node draw-order tie-break for equal-priority coplanar surfaces:
                // the node's flat index is the original's draw order (later = on top).
                mi.SetInstanceShaderParameter("node_bias", node.Index * NodeOrderBias);
                // Billboard meshes were recentered on their quad center; put the instance
                // there so the material's billboard pivots at the center, not the node origin.
                if (_meshPivotCache.TryGetValue(node.MeshIndex, out var pivot))
                    mi.Position = pivot;
                n3d.AddChild(mi);
                MeshInstanceCount++;
                if (collidable)
                    AttachCollision(n3d, node.MeshIndex, mesh);
            }
            // Point-sprite lights (night-sky stars, nav/tower beacons): rendered by the
            // original engine as small glowing dots. Never collidable, never shadowed.
            if (_gamez.Meshes[node.MeshIndex].Lights.Count > 0)
            {
                n3d.AddChild(new MeshInstance3D
                {
                    Mesh = GetLightPoints(node.MeshIndex),
                    Name = "lights",
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
                MeshInstanceCount++;
            }
        }

        foreach (var childIndex in node.Children)
        {
            if (childIndex < 0 || childIndex >= _gamez.Nodes.Count)
                continue;
            var child = BuildSubtree(_gamez.Nodes[childIndex], skip, collisionSkip, collidable);
            if (child != null)
                n3d.AddChild(child);
        }
        return n3d;
    }

    // A static trimesh body in the mesh's own space; the parent node carries the world
    // transform, so the collider lines up with the rendered surface. The concave shape
    // is cached per mesh and shared across instances (shapes are resources).
    private void AttachCollision(Node3D parent, int meshIndex, ArrayMesh mesh)
    {
        if (!_shapeCache.TryGetValue(meshIndex, out var shape))
        {
            shape = mesh.CreateTrimeshShape();
            // The source winding is inconsistent (why rendering culls nothing), so make the
            // trimesh solid from both sides — otherwise raycasts pass through down-wound faces.
            if (shape != null)
                shape.BackfaceCollision = true;
            _shapeCache[meshIndex] = shape;
        }
        if (shape == null)
            return;
        var body = new StaticBody3D { Name = "col" };
        body.AddChild(new CollisionShape3D { Shape = shape });
        // Stamp the struck-surface class (water / buildings), so a weapon impact can pick the
        // right IMPACT variant (B15). Derived once per mesh from its dominant material texture;
        // 'default' (terrain / anything unclassified) is the common case and stamps nothing.
        var surface = SurfaceForMesh(meshIndex);
        if (surface != null)
            body.SetMeta(SurfaceMeta, surface);
        parent.AddChild(body);
        ColliderCount++;
    }

    /// <summary>The dominant surface class of a mesh's colliding geometry — the majority material
    /// texture, classified by name (water: <c>water*</c>/<c>wtr*</c>/<c>srf*</c>/<c>wakefront</c>;
    /// buildings: <c>hangar*</c>/<c>*build*</c>/<c>cblock</c>/<c>warehouse</c>/<c>roof</c>). Null =
    /// terrain / unclassified (the default IMPACT variant). Cached per mesh index.</summary>
    private string? SurfaceForMesh(int meshIndex)
    {
        if (_surfaceCache.TryGetValue(meshIndex, out var cached))
            return cached;
        var counts = new Dictionary<string, int>();
        string? best = null;
        int bestN = 0;
        foreach (var poly in _gamez.Meshes[meshIndex].Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
                continue;
            var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
            var tag = ClassifySurface(tex);
            if (tag == null)
                continue;
            counts.TryGetValue(tag, out int n);
            counts[tag] = ++n;
            if (n > bestN)
            {
                bestN = n;
                best = tag;
            }
        }
        _surfaceCache[meshIndex] = best;
        return best;
    }

    // Point-sprite lights: camera-facing soft radial glows, additive blend so they shine
    // over whatever is behind them (and pure-black lights become invisible). The original
    // renders these as distance-sized sprites — user-verified look: soft
    // star-like falloff (no hard cutoff) and clearly brighter than plain fixed dots; the
    // yellow tarmac lamps, blue pier lights and the lighthouse all take this path.
    // Per-light data drives size and reach: SizeScale (unk08) scales the sprite,
    // MaxSizePx (unk64 = 30) caps it on approach, Range (unk68/unk52 = 1500–4000 m)
    // fades it out with distance — which also stops in-map beacons punching through the
    // fog from kilometres outside. One POINTS surface per (size, range)
    // group per mesh (uniform per mesh in practice); materials cached per param set. The
    // camera-anchored skydome's stars sit past any data range, so BuildHorizon exempts
    // them via the csky_light_fade instance uniform.
    private ArrayMesh GetLightPoints(int meshIndex)
    {
        if (_lightMeshCache.TryGetValue(meshIndex, out var cached))
            return cached;

        // Group the mesh's lights by their size/range params → one POINTS surface +
        // shared material per group (a mesh's lights are uniform in practice).
        var groups = new Dictionary<(float Size, float MaxPx, float Range), (List<Vector3> P, List<Color> C)>();
        foreach (var l in _gamez.Meshes[meshIndex].Lights)
        {
            var key = (l.SizeScale <= 0f ? 1f : l.SizeScale,
                       l.MaxSizePx <= 0f ? 30f : l.MaxSizePx,
                       l.Range);
            if (!groups.TryGetValue(key, out var g))
                groups[key] = g = (new List<Vector3>(), new List<Color>());
            g.P.Add(l.Position);
            g.C.Add(l.Color);
        }

        var mesh = new ArrayMesh();
        foreach (var (key, g) in groups)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = g.P.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = g.C.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);

            if (!_lightMaterialCache.TryGetValue(key, out var mat))
            {
                _lightShader ??= new Shader { Code = LightShaderCode };
                mat = new ShaderMaterial { Shader = _lightShader };
                mat.SetShaderParameter("size_scale", key.Size);
                mat.SetShaderParameter("max_size_px", key.MaxPx);
                mat.SetShaderParameter("range_far", key.Range);
                _lightMaterialCache[key] = mat;
            }
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, mat);
        }
        _lightMeshCache[meshIndex] = mesh;
        return mesh;
    }

    private ArrayMesh? GetMesh(int meshIndex)
    {
        if (_meshCache.TryGetValue(meshIndex, out var cached))
            return cached;
        var mesh = BuildMesh(_gamez.Meshes[meshIndex], meshIndex);
        _meshCache[meshIndex] = mesh;
        return mesh;
    }

    private ArrayMesh? BuildMesh(GameZMesh mesh, int meshIndex)
    {
        if (mesh.Polygons.Count == 0)
            return null;

        // Camera-FACING sprites (clouds, spherical glow flares) are recentered on their quad
        // center: the material billboards around the mesh origin, but the source quads sit
        // offset from it — the cloud sprites by up to ~650 m — so without this they would
        // swing around the node as the camera turns. The offset is stored per mesh; every
        // instance re-applies it as its local position.
        //
        // Single-axis (cylindrical) facades are deliberately NOT recentered: there the offset
        // is the authored effect, not an artifact. Their shader spins the quad about the model
        // origin, so a quad offset perpendicular to that axis ORBITS it — which is exactly how
        // the original makes C1's lighthouse beam sweep (`litehsflare`, a 19 m quad centred
        // 6 m off the tower axis, so the beam stays visible from every direction), how the
        // hangar/street lamps hang off their poles (`fireflare1`, 2.58-3.6 m) and how a muzzle
        // flash sits at the barrel tip rather than the gun's pivot (`nosegun1`, 2.4 m).
        // Recentering collapsed all of those to a sprite spinning in place. Surveyed across all
        // 8 chapters: exactly those three families are affected — 83 of 436 cylindrical facades,
        // and the other 353 have a zero (or purely on-axis) offset, so nothing else moves.
        bool glowSprite = IsGlowSpriteMesh(mesh);
        var cylAxis = GetCylindricalAxis(mesh);
        var offset = Vector3.Zero;
        if ((UsesBillboardTexture(mesh) || glowSprite) && mesh.Vertices.Count > 0)
        {
            foreach (var v in mesh.Vertices)
                offset += v;
            offset /= mesh.Vertices.Count;
            _meshPivotCache[meshIndex] = offset;
        }

        // One Godot surface per (material, draw priority, subface, sidedness): polygons of
        // different priority need different materials (the priority becomes a depth
        // bias — see GetMaterial), a subface takes an extra bias on top of its priority
        // (SubfaceBias), and SHOW_BACKFACE polygons need a different cull
        // mode when backface culling is on. Groups are kept in first-occurrence order
        // = the original's within-mesh draw order; the group's rank is the
        // equal-priority tie-break (later polygons drew over earlier ones — e.g. the
        // tile meshes' roads and shoreline blends over their base grass).
        var groups = new List<(int Material, int Priority, bool Subface, bool DoubleSided, List<GameZPolygon> Polys)>();
        var groupIndex = new Dictionary<(int, int, bool, bool), int>();
        foreach (var poly in mesh.Polygons)
        {
            bool doubleSided = !_cullBackfaces || poly.ShowBackface;
            var key = (poly.MaterialIndex, poly.Priority, poly.Subface, doubleSided);
            if (!groupIndex.TryGetValue(key, out int gi))
            {
                groupIndex[key] = gi = groups.Count;
                groups.Add((poly.MaterialIndex, poly.Priority, poly.Subface, doubleSided, new List<GameZPolygon>()));
            }
            groups[gi].Polys.Add(poly);
        }

        // The model's UV animation, if it has one — the boot script's rate where a mission set
        // one, else the model's own field (see EffectiveScroll). Only the bias path carries it:
        // every scrolling model in this install is ModelType "Default", so no glow flare or
        // cylindrical facade needs it, and those two caches stay keyed as they were.
        var scroll = EffectiveScroll(mesh, meshIndex);
        if (scroll != Vector2.Zero)
            ScrollingModelCount++;

        var arrayMesh = new ArrayMesh();
        for (int rank = 0; rank < groups.Count; rank++)
        {
            var (materialIndex, priority, subface, doubleSided, polys) = groups[rank];
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var poly in polys)
                EmitPolygon(st, mesh, poly, offset);
            // A surface whose UVs never leave the unit square never needs the sampler to wrap,
            // and wrapping it is what produces the hairline seams (see UvsWithinUnitSquare).
            // A scrolling surface is excluded: its UVs deliberately run past 1 and rely on
            // repeat to come back round.
            bool clampUv = scroll == Vector2.Zero && UvsWithinUnitSquare(polys);
            if (clampUv)
                ClampedSurfaceTotal++;
            st.SetMaterial(glowSprite ? GetGlowMaterial(materialIndex)
                : cylAxis != CylAxis.None ? GetCylindricalMaterial(materialIndex, cylAxis)
                : GetMaterial(materialIndex, priority, rank, subface, doubleSided, scroll, clampUv));
            st.Commit(arrayMesh);
        }
        return arrayMesh;
    }

    /// <summary>
    /// The UV scroll rate (units/second) for one model: the caller's per-model override if it
    /// has one, else the model's own <c>texture_scroll</c> field. The two are the same setting
    /// read at two different times — the engine's <c>Object3DSetScroll</c> writes this field, so
    /// the chapter-level <c>tex_fx.gw</c> rates are already baked into the shipped gamez (C1's
    /// <c>h_zone1scroll</c> = 0.07 in both places, C1B's wakefronts 0.7/1.0, its
    /// <c>con_scroll</c> −1.0) while the per-mission ones can only be applied at load, which is
    /// why C1's waterfall carries {0,0} in the gamez and still scrolls at −0.4 v/s in game.
    /// </summary>
    private Vector2 EffectiveScroll(GameZMesh mesh, int meshIndex) =>
        _scrollOverrides != null && _scrollOverrides.TryGetValue(meshIndex, out var rate)
            ? rate
            : mesh.TextureScroll;

    // True if any of the mesh's polygons is skinned with a billboard (cloud-sprite) texture.
    private bool UsesBillboardTexture(GameZMesh mesh)
    {
        if (_billboardTexture == null)
            return false;
        foreach (var poly in mesh.Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
                continue;
            var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
            if (tex != null && _billboardTexture(tex))
                return true;
        }
        return false;
    }

    // A glow flare SPRITE — the lamp/beacon light quads (refinery_flare 16 m, gen_flare_yellow
    // 4 m, docklight_flare, litehsflare, …): billboards fully toward the camera, never
    // night-dimmed (it's a light source, not lit scenery). That is the Spherical case of
    // ClassifyBillboard minus the cloud sprites, which are the OTHER SphericalY family and
    // are routed through _billboardTexture instead.
    //
    // The legacy branch (single polygon + a "flare"-ish texture name) is what a v0.6.1
    // extraction gets. It under-matches — fire101.tif's refinery flame carries no "flare" in
    // its name and rendered as static geometry — which is exactly why the data-driven rule
    // above replaced it; it is kept only so the documented v0.6.1 rollback still works.
    private bool IsGlowSpriteMesh(GameZMesh mesh)
    {
        if (ClassifyBillboard(mesh) is { } kind)
            return kind == BillboardKind.Spherical && !UsesBillboardTexture(mesh);

        // Legacy fallback (no ModelType data in this extraction).
        if (_glowTexture == null || mesh.Polygons.Count != 1)
            return false;
        var poly = mesh.Polygons[0];
        if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
            return false;
        var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
        return tex != null && _glowTexture(tex);
    }

    private Material GetGlowMaterial(int materialIndex)
    {
        if (_glowMaterialCache.TryGetValue(materialIndex, out var cached))
            return cached;
        var texName = _gamez.Materials[materialIndex].TextureName;
        var tex = texName != null ? Resolve(texName) : null;
        Material mat = tex != null
            ? BillboardMaterial(tex, blend: true, scissor: false, glow: true)
            : GetMaterial(materialIndex, 0, 0, subface: false, doubleSided: true);
        _glowMaterialCache[materialIndex] = mat;
        return mat;
    }

    private Material GetCylindricalMaterial(int materialIndex, CylAxis axis)
    {
        var key = (materialIndex, (int)axis);
        if (_cylindricalMaterialCache.TryGetValue(key, out var cached))
            return cached;
        var texName = _gamez.Materials[materialIndex].TextureName;
        var tex = texName != null ? Resolve(texName) : null;
        Material mat;
        if (tex != null)
        {
            bool blend = _textures.LastHadAlpha && _textures.LastAlphaIsSoft;
            bool scissor = _textures.LastHadAlpha && !blend;
            bool glow = texName != null && _glowTexture != null && _glowTexture(texName);
            mat = CylindricalBillboardMaterial(tex, axis, blend, scissor, glow);
        }
        else
        {
            mat = GetMaterial(materialIndex, 0, 0, subface: false, doubleSided: true);
        }
        _cylindricalMaterialCache[key] = mat;
        return mat;
    }

    private Material GetMaterial(int materialIndex, int priority, int rank, bool subface, bool doubleSided,
        Vector2 scroll = default, bool clampUv = false)
    {
        rank = Math.Min(rank, SurfaceRankCap);
        var key = (materialIndex, priority, rank, subface, doubleSided, scroll.X, scroll.Y, clampUv);
        if (_materialCache.TryGetValue(key, out var cached))
            return cached;
        var mat = BuildMaterial(materialIndex, priority, rank, subface, doubleSided, scroll, clampUv);
        _materialCache[key] = mat;
        return mat;
    }

    // The archive lookup every material goes through, plus the caller's optional
    // substitution (aircraft paint). Find() must still run even when a substitute exists:
    // it is what sets LastHadAlpha/LastAlphaIsSoft, which the blend/scissor choice reads.
    private ImageTexture? Resolve(string texName)
    {
        var tex = _textures.Find(texName);
        // A drop-in colour is the answer the run was launched to get; a composited paint scheme
        // would paint straight over it and the aircraft would be the one thing the census misses.
        if (_textureSubstitute != null && !TextureDropIn.Covers(texName))
        {
            return _textureSubstitute(texName, tex);
        }
        return tex;
    }

    /// <summary>Registers a built material against its source's flipbook, if it has one. Called
    /// per built material rather than per source material on purpose: the material cache is keyed
    /// by (material, priority, rank, sidedness), so one cycling source can legitimately produce
    /// several ShaderMaterials (C1B's water spans surfaces of different draw priority) and each
    /// needs its own frame swaps. Frames resolve now, while the TextureArchive is open.</summary>
    private void RegisterCycle(GameZMaterial src, ShaderMaterial mat)
    {
        if (Cycler == null || src.CycleTextures.Count < 2)
            return;
        var frames = new List<ImageTexture>(src.CycleTextures.Count);
        foreach (var name in src.CycleTextures)
        {
            if (Resolve(name) is not { } frame)
                return; // an incomplete flipbook would strobe a hole; leave it static instead
            frames.Add(frame);
        }
        Cycler.Add(mat, frames, src.CycleSpeed, src.CycleLooping, src.TextureName ?? "?");
    }

    private Material BuildMaterial(int materialIndex, int priority, int rank, bool subface, bool doubleSided,
        Vector2 scroll, bool clampUv = false)
    {
        var src = materialIndex >= 0 && materialIndex < _gamez.Materials.Count
            ? _gamez.Materials[materialIndex]
            : null;

        if (src?.TextureName is { } texName)
        {
            var tex = Resolve(texName);
            if (tex == null)
                // Genuine game-data gaps (pir_spinner, barngrill) get a neutral gray, like
                // the original engine; anything else is likely our lookup failing and stays
                // debug-magenta so it's obvious.
                return NewStandard(albedoColor: TextureArchive.IsKnownAbsent(texName)
                    ? new Color(0.5f, 0.5f, 0.5f)
                    : Colors.Magenta);

            // Soft-alpha textures (baked shadow decals, clouds, prop blur, waterfalls,
            // smoke — detected from the pixels, see TextureArchive.LastAlphaIsSoft) and
            // the caller's explicit blend list alpha-blend; every other alpha texture
            // scissors (hard cutout — right for fences, trees, railings).
            bool blend = _textures.LastHadAlpha
                && (_textures.LastAlphaIsSoft || (_blendTexture != null && _blendTexture(texName)));
            bool scissor = _textures.LastHadAlpha && !blend;
            // Cloud sprites face the camera (their meshes are recentered to match). They take
            // a billboard ShaderMaterial rather than the bias shader — no depth bias (free-
            // floating sprites have nothing coplanar to fight) but the SAME cylindrical distance
            // fog, so distant sprites fade into the fog wall like the terrain they float over
            // instead of punching through as crisp white. (Glow flares don't
            // branch here — their billboard treatment is per-MESH, see BuildMesh: the same
            // flare texture also skins polys inside regular geometry, which must stay put.)
            if (_billboardTexture != null && _billboardTexture(texName))
                return BillboardMaterial(tex, blend, scissor);
            var textured = BiasMaterial(priority, rank, subface, doubleSided, tex, null, blend, scissor, scroll, clampUv);
            _texturedMaterials.Add((textured, texName)); // for a live repaint, see Repaint()
            RegisterCycle(src, textured);
            return textured;
        }

        var color = src?.Color ?? Colors.White;
        return BiasMaterial(priority, rank, subface, doubleSided, null, color, blend: color.A < 1f, scissor: false);
    }

    private StandardMaterial3D NewStandard(Color? albedoColor = null)
    {
        var mat = new StandardMaterial3D
        {
            // Only used for billboards (cloud sprites) and the missing-texture fallback,
            // which should render from both sides regardless of source sidedness.
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.85f,
            Metallic = 0.0f,
            VertexColorUseAsAlbedo = true, // baked lighting from the source data
        };
        if (_fullbright)
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (albedoColor is { } c)
            mat.AlbedoColor = c;
        return mat;
    }

    // A ShaderMaterial that mirrors NewStandard's look but pulls the geometry toward the
    // eye (or pushes it away, negative priority) by a fraction of its view distance.
    // The per-node draw-order term is added at instance level (see BuildSubtree).
    private ShaderMaterial BiasMaterial(int priority, int rank, bool subface, bool doubleSided, ImageTexture? tex,
        Color? color, bool blend, bool scissor, Vector2 scroll = default, bool clampUv = false)
    {
        // Only a textured surface can scroll its UVs (a Colored material has no sampler).
        bool scrolls = tex != null && scroll != Vector2.Zero;
        var mat = new ShaderMaterial
        {
            Shader = GetBiasShader(shaded: !_fullbright, textured: tex != null, blend, scissor, doubleSided,
                scrolls, clampUv && tex != null),
        };
        float bias = Mathf.Clamp(priority * DepthBiasPerLevel, -0.05f, 0.05f) + rank * SurfaceRankBias;
        if (subface)
            bias += SubfaceBias;
        mat.SetShaderParameter("depth_bias", bias);
        if (tex != null)
            mat.SetShaderParameter("albedo_tex", tex);
        if (color is { } c)
            mat.SetShaderParameter("albedo_color", c);
        if (scrolls)
            mat.SetShaderParameter("scroll_rate", scroll);
        return mat;
    }

    private Shader GetBiasShader(bool shaded, bool textured, bool blend, bool scissor, bool doubleSided,
        bool scroll = false, bool clampUv = false)
    {
        int key = (shaded ? 1 : 0) | (textured ? 2 : 0) | (blend ? 4 : 0) | (scissor ? 8 : 0) | (doubleSided ? 16 : 0)
            | (scroll ? 32 : 0) | (clampUv ? 64 : 0);
        if (_biasShaderCache.TryGetValue(key, out var cached))
            return cached;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("shader_type spatial;");
        // Sidedness: the source's visible side is where its vertex loop runs counter-
        // clockwise (right-handed winding, verified on the autogyro's outward deck skin
        // vs inward frame lattice); Godot front faces are clockwise, so single-sided
        // source polygons keep Godot's BACK face — cull_front.
        sb.Append(doubleSided
            ? "render_mode skip_vertex_transform, cull_disabled"
            : "render_mode skip_vertex_transform, cull_front");
        if (!shaded)
            sb.Append(", unshaded");
        sb.AppendLine(";");
        sb.AppendLine("uniform float depth_bias = 0.0;");
        // The shared ordered instance-uniform block. This shader always carries instance
        // uniforms (node_bias, csky_fog_on), so it always takes the full preamble — see the
        // contract in the .gdshaderinc itself. The camera-anchored skydome opts out of fog per
        // instance (csky_fog_on = 0): its below-horizon skirt sits at low altitude ~22 km out
        // and would otherwise fog solid gray.
        sb.AppendLine(InstanceUniformsInclude);
        // Distance fog + the per-mission SUNLIGHT dimming.
        sb.AppendLine(AtmosphereInclude);
        if (!shaded)
            sb.AppendLine(LightsInclude); // LIGHT_STATE spill — fullbright passes only
        if (textured)
            // Anisotropic mipmap filtering: the world is viewed at grazing angles from the
            // air, where plain isotropic mipmap selection blurs the ground to mush (the C5
            // "blurry city ground" report — and it is NOT a missing hi-res archive: the base
            // texture set is already max-res, rtexture2/4/6/8 are downscaled quality tiers and
            // rtexture14 == base). Anisotropic sharpens the receding ground without new assets.
            // repeat_disable for a surface whose UVs never leave the unit square: the wrap is
            // unreachable there, so clamping costs nothing and removes the hairline seams the
            // wrap otherwise produces at a mirrored UV fold (see UvsWithinUnitSquare).
            sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, "
                + (clampUv ? "repeat_disable;" : "repeat_enable;"));
        // UV animation (the model's texture_scroll / the boot script's Object3DSetScroll): the
        // waterfalls' falling sheet, the boats' wake fronts, the oil-dock conveyor and the
        // daytime sky layer. Emitted only for surfaces that actually scroll, so every other
        // material's shader text is byte-for-byte what it always was. `repeat_enable` above is
        // what makes the offset wrap instead of clamping at the UV edge.
        //
        // The phase comes from csky_time — the sim clock's shader-side twin, not Godot's TIME —
        // so the scroll freezes with a halted clock and is a function of the frame count under a
        // fixed step. ⚠ csky_time keeps TIME's 3600 s wrap, and every rate in this install
        // (0.07 / 0.4 / 0.5 / 0.7 / 1.0) times 3600 is a whole number of texture repeats, so the
        // wrap lands on the identical frame — no visible jump. See ShaderTime.RolloverSecs.
        if (scroll)
        {
            sb.AppendLine(TimeInclude);
            sb.AppendLine("uniform vec2 scroll_rate = vec2(0.0);");
        }
        if (!textured)
            sb.AppendLine("uniform vec4 albedo_color : source_color = vec4(1.0);");
        // Fullbright world only: the DX7 fixed-function pipeline multiplied texture × baked
        // vertex colour (D3DTOP_MODULATE) in GAMMA (sRGB) space; we render in linear space, so
        // our multiply comes out too bright / desaturated wherever the baked colour is < 1
        // (shadows/AO — 30% of the C1 world's corners). Linearising the vertex colour before
        // the (already-linear) texture multiply reproduces the gamma-space product.
        if (!shaded)
            sb.AppendLine(SrgbInclude);
        // Normal sign — aircraft only; the fullbright world never reads NORMAL. Our source
        // normals point to the polygon's visible side, and that side is the CCW loop, which is
        // Godot's BACK face (the reason single-sided surfaces use cull_front above). So every
        // visible aircraft fragment is back-facing, and Godot negates NORMAL on back faces:
        // the normal reaches the light calculation pointing INTO the airframe, and every
        // upward-facing surface shades as though lit from underneath. Pre-negating cancels
        // that engine flip, and it is correct for both sidedness cases — single-sided shows
        // only the CCW side, and a double-sided (cull_disabled) polygon gets the engine's flip
        // on exactly the side that needs it.
        // Measured on the Fury via the mesh lab (ambient off, sun straight down, viewed from
        // above): wing top 6.4 → 73.8, and the light-grey "patches" at the wingtips and tail
        // — which were only the places a bright texture survived the inversion — disappear.
        string normalSign = shaded ? "-" : "";
        sb.AppendLine($@"
void vertex() {{
    VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    NORMAL = {normalSign}normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
    // Scale toward the eye (the view-space origin): identical projected position,
    // depth nudged nearer by bias × distance — a scale-invariant polygon offset.
    VERTEX *= 1.0 - (depth_bias + node_bias);
}}

void fragment() {{");
        // Shaded (planes) keeps raw COLOR for the real-lighting path; fullbright (world) applies
        // the gamma-space vertex modulate (see SrgbToLinearFn above).
        string vcol = shaded ? "COLOR" : "vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a)";
        // The surface's own albedo is kept separately from the modulated result: a point light's
        // spill is light falling ON the surface, so it must be modulated by the same albedo
        // rather than added to the final colour (an unlit black texture stays black under a lamp).
        sb.AppendLine(!textured ? "    vec4 base_col = albedo_color;"
            : scroll ? "    vec4 base_col = texture(albedo_tex, UV + scroll_rate * csky_time);"
            : "    vec4 base_col = texture(albedo_tex, UV);");
        sb.AppendLine($"    vec4 col = {vcol} * base_col;");
        sb.AppendLine("    ALBEDO = col.rgb;");
        if (!shaded)
            sb.AppendLine("    ALBEDO *= csky_world_light;"); // per-mission SUNLIGHT dimming (world/deck/dome)
        if (shaded)
        {
            sb.AppendLine("    ROUGHNESS = 0.85;");
            sb.AppendLine("    METALLIC = 0.0;");
            sb.AppendLine("    SPECULAR = 0.5;");
        }
        // Distance fog, cylindrical (see the uniform block above): VERTEX is the view-space
        // position here (set in vertex() under skip_vertex_transform); INV_VIEW_MATRIX lifts it
        // back to world space for the horizontal camera distance + the fragment-altitude fade.
        // The aircraft is always within the near range at chase distance, so this is a no-op on it.
        sb.AppendLine("    vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;");
        // Animated point lights, before the fog mix so a lit surface still fogs out with
        // distance. Deliberately NOT scaled by csky_world_light: a lamp is a light source, so it
        // does not dim with the mission's SUNLIGHT — the same rule the glow-flare sprites follow.
        // With no lights active csky_light_spill returns exactly vec3(0.0) and the add is an
        // identity, which is what keeps an unlit world bit-identical to the pre-lights renderer.
        // (Braces are load-bearing: emitting the ALBEDO line unguarded put it in the SHADED
        // shader too, where light_n does not exist — every aircraft silently fell back to
        // Godot's untextured default material.)
        if (!shaded)
        {
            // The world's normals reach here flipped for the same reason the aircraft's do: its
            // single-sided polygons render with cull_front, so every visible fragment is
            // back-facing and Godot negates NORMAL. Same cancelling minus as the shaded path,
            // and verified the same way — negated lights the pier deck and the boat decks (a
            // lamp above the pier); non-negated lights the pilings and hull sides instead.
            sb.AppendLine("    vec3 light_n = normalize(-(INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);");
            sb.AppendLine("    ALBEDO += base_col.rgb * csky_light_spill(fog_world, light_n);");
        }
        sb.AppendLine("    float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);");
        sb.AppendLine("    ALBEDO = mix(ALBEDO, csky_fog_color, csky_fog_on * fog_amt);");
        if (blend || scissor)
            sb.AppendLine($"    ALPHA = col.a{OpacityTerm};");
        if (scissor)
            sb.AppendLine("    ALPHA_SCISSOR_THRESHOLD = 0.5;");
        sb.AppendLine("}");

        var shader = new Shader { Code = sb.ToString() };
        _biasShaderCache[key] = shader;
        return shader;
    }

    // A camera-facing billboard material for the cloud sprites (cloud1/cloud2 — flat 2D cards
    // in the source). Unlike the bias shader it does no depth bias (free-floating sprites have
    // nothing coplanar to fight) but carries the identical cylindrical distance-fog term, so
    // distant sprites fade into the fog wall in step with the terrain they float over. blend /
    // scissor follow the alpha classification (cloud1/cloud2 are soft-alpha ⇒ blend).
    private ShaderMaterial BillboardMaterial(ImageTexture tex, bool blend, bool scissor, bool glow = false)
    {
        var mat = new ShaderMaterial { Shader = GetBillboardShader(blend, scissor, glow) };
        mat.SetShaderParameter("albedo_tex", tex);
        return mat;
    }

    private Shader GetBillboardShader(bool blend, bool scissor, bool glow = false)
    {
        int key = (blend ? 1 : 0) | (scissor ? 2 : 0) | (glow ? 4 : 0);
        if (_billboardShaderCache.TryGetValue(key, out var cached))
            return cached;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("shader_type spatial;");
        // Fullbright like the rest of the world, double-sided, no shadows. fog_disabled turns
        // OFF Godot's built-in fog (we roll our own csky_fog_* below); blend clouds render in
        // the transparent pass writing no depth (matches the old StandardMaterial3D), scissor/
        // opaque render normally.
        sb.Append("render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled");
        if (blend)
            sb.Append(", blend_mix, depth_draw_never");
        sb.AppendLine(";");
        sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;");
        // Same global distance-fog params as GetBiasShader's world shader. Clouds always fog,
        // so this shader never reads csky_fog_on — and an OPAQUE cloud sprite therefore declares
        // no instance uniform at all, which deliberately keeps it off the instance-uniform buffer
        // (see the buffer note on OpacityTerm, and the warning in csky_instance_uniforms.gdshaderinc).
        sb.AppendLine(AtmosphereInclude);
        // The alpha-writing variants already carry csky_opacity, i.e. they are on that buffer
        // regardless — so they take the shared ordered preamble and agree on indices with every
        // other shader, at no additional cost.
        if (blend || scissor)
            sb.AppendLine(InstanceUniformsInclude);
        sb.AppendLine(SrgbInclude); // DX7 gamma-space vertex modulate (world/cloud pass)
        sb.AppendLine(@"
void vertex() {
    // Camera-facing billboard keeping the instance scale (Godot's billboard_keep_scale, by
    // hand — the bias shader can't billboard, like CloudPuffs). The mesh was recentered on its
    // quad centre and the instance placed there, so the quad pivots at its centre.
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz);
    MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz);
    MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);
}

void fragment() {
    vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * texture(albedo_tex, UV);
    // Cylindrical distance fog, identical to the world shader: VERTEX is the view-space
    // position in fragment; INV_VIEW_MATRIX lifts it back to world for the horizontal camera
    // distance + the fragment-altitude fade.
    vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);");
        // Glow flares are light sources: no SUNLIGHT night dimming (a lamp doesn't get
        // darker at night — it's what lights the scene). Clouds ride the world brightness.
        sb.AppendLine(glow
            ? "    ALBEDO = mix(col.rgb, csky_fog_color, fog_amt);"
            : "    ALBEDO = mix(col.rgb * csky_world_light, csky_fog_color, fog_amt);");
        if (blend || scissor)
            sb.AppendLine($"    ALPHA = col.a{OpacityTerm};");
        if (scissor)
            sb.AppendLine("    ALPHA_SCISSOR_THRESHOLD = 0.5;");
        sb.AppendLine("}");

        var shader = new Shader { Code = sb.ToString() };
        _billboardShaderCache[key] = shader;
        return shader;
    }

    // A single-axis (Y or X) cylindrical billboard: the mesh spins about that one fixed
    // world axis to face the camera on the other two, rather than SphericalY's full
    // camera-facing rotation — trees/lampposts/flame/cable stay upright as the camera orbits
    // instead of tipping toward it like a light-source glow. Same technique as Clutter's
    // proven tree-billboard shader (skip_vertex_transform + a hand-built spin matrix), kept
    // as its own generator rather than shared code because the two differ in fog/dim/
    // instancing details already (Clutter is a MultiMesh with no per-mesh pivot cache).
    private ShaderMaterial CylindricalBillboardMaterial(ImageTexture tex, CylAxis axis, bool blend, bool scissor, bool glow)
    {
        var mat = new ShaderMaterial { Shader = GetCylindricalShader(axis, blend, scissor, glow) };
        mat.SetShaderParameter("albedo_tex", tex);
        return mat;
    }

    private Shader GetCylindricalShader(CylAxis axis, bool blend, bool scissor, bool glow)
    {
        int key = (axis == CylAxis.X ? 1 : 0) | (blend ? 2 : 0) | (scissor ? 4 : 0) | (glow ? 8 : 0);
        if (_cylindricalShaderCache.TryGetValue(key, out var cached))
            return cached;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("shader_type spatial;");
        sb.Append("render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled");
        if (blend)
            sb.Append(", blend_mix, depth_draw_never");
        sb.AppendLine(";");
        sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;");
        // csky_world_light arrives with the atmosphere include and is READ only when !glow
        // (a light source does not dim with the mission's SUNLIGHT); declaring it either way
        // costs nothing, since a global uniform is project-wide rather than per-instance.
        sb.AppendLine(AtmosphereInclude);
        // Only the alpha-writing variants carry an instance uniform at all, so only they take
        // the preamble — an opaque facade stays off the instance-uniform buffer entirely.
        if (blend || scissor)
            sb.AppendLine(InstanceUniformsInclude);
        sb.AppendLine(SrgbInclude);
        // Fixed axis + the plane the camera direction is measured in: Y keeps world-up fixed
        // and spins in XZ (Clutter's tree technique); X keeps the local-right axis fixed and
        // spins in YZ (a horizontal pipe's flame/muzzle flash facing the camera around its
        // own barrel axis).
        string toCam = axis == CylAxis.X
            ? "CAMERA_POSITION_WORLD.yz - origin.yz" : "CAMERA_POSITION_WORLD.xz - origin.xz";
        string spinRows = axis == CylAxis.X
            ? "vec3(1.0, 0.0, 0.0), vec3(0.0, dir.y, -dir.x), vec3(0.0, dir.x, dir.y)"
            : "vec3(dir.y, 0.0, -dir.x), vec3(0.0, 1.0, 0.0), vec3(dir.x, 0.0, dir.y)";
        sb.AppendLine($@"
void vertex() {{
    vec3 origin = MODEL_MATRIX[3].xyz;
    vec2 to_cam = {toCam};
    float len = length(to_cam);
    vec2 dir = len > 1e-4 ? to_cam / len : vec2(0.0, 1.0);
    mat3 spin = mat3({spinRows});
    VERTEX = (VIEW_MATRIX * vec4(origin + spin * VERTEX, 1.0)).xyz;
}}

void fragment() {{
    vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * texture(albedo_tex, UV);
    vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);");
        sb.AppendLine(glow
            ? "    ALBEDO = mix(col.rgb, csky_fog_color, fog_amt);"
            : "    ALBEDO = mix(col.rgb * csky_world_light, csky_fog_color, fog_amt);");
        if (blend || scissor)
            sb.AppendLine($"    ALPHA = col.a{OpacityTerm};");
        if (scissor)
            sb.AppendLine("    ALPHA_SCISSOR_THRESHOLD = 0.5;");
        sb.AppendLine("}");

        var shader = new Shader { Code = sb.ToString() };
        _cylindricalShaderCache[key] = shader;
        return shader;
    }
}
