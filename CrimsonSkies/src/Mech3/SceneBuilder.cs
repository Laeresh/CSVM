using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

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
    private readonly Dictionary<(int Material, int Priority, int Rank, bool DoubleSided, float ScrollU, float ScrollV), Material> _materialCache = new();
    private readonly Dictionary<int, Shader> _biasShaderCache = new(); // keyed by feature bits
    private readonly Dictionary<int, Shader> _billboardShaderCache = new(); // cloud sprites, keyed by blend/scissor bits
    private readonly Dictionary<int, Shader> _cylindricalShaderCache = new(); // Y/X-axis facades, keyed by axis/blend/scissor/glow bits
    private readonly Dictionary<int, ArrayMesh?> _meshCache = new();
    private readonly Dictionary<int, Vector3> _meshPivotCache = new(); // billboard meshes only: local quad center
    private readonly Dictionary<int, ArrayMesh> _lightMeshCache = new();
    private readonly Dictionary<int, ConcavePolygonShape3D?> _shapeCache = new();
    private readonly Dictionary<(float Size, float MaxPx, float Range), ShaderMaterial> _lightMaterialCache = new();
    private Shader? _lightShader;

    // sRGB EOTF (Godot's exact texture-linearization curve) for the DX7 gamma-space vertex
    // modulate on the fullbright world/cloud shaders — see the usage in GetBiasShader. Also
    // duplicated verbatim in Clutter's shader (trees share the world's baked-lighting model).
    internal const string SrgbToLinearFn = @"
vec3 csky_srgb_to_linear(vec3 c) {
    vec3 higher = pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4));
    vec3 lower = c * (1.0 / 12.92);
    // per component: c <= 0.04045 -> linear toe, else the power curve
    return mix(higher, lower, step(c, vec3(0.04045)));
}";

    public int MeshInstanceCount { get; private set; }
    public int ColliderCount { get; private set; }

    /// <summary>Models built with a non-zero UV scroll rate, from either source (the model's own
    /// <c>texture_scroll</c> or the boot script). Logged per world build: it is the one-line
    /// evidence that a chapter animates exactly the surfaces the data says and no others.</summary>
    public int ScrollingModelCount { get; private set; }

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

    /// <summary>Builds the subtree rooted at <paramref name="node"/>; null if skipped entirely.</summary>
    /// <param name="skip">Subtrees to drop entirely (not rendered, no collision).</param>
    /// <param name="collisionSkip">Subtrees to render but exempt from collision (e.g. clouds);
    /// the exemption applies to the node and all its descendants.</param>
    public Node3D? BuildSubtree(GameZNode node, Predicate<GameZNode>? skip = null,
        Predicate<GameZNode>? collisionSkip = null) =>
        BuildSubtree(node, skip, collisionSkip, _generateCollision);

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
        parent.AddChild(body);
        ColliderCount++;
    }

    // Point-sprite lights: camera-facing soft radial glows, additive blend so they shine
    // over whatever is behind them (and pure-black lights become invisible). The original
    // renders these as distance-sized sprites — user-verified look (2026-07-18): soft
    // star-like falloff (no hard cutoff) and clearly brighter than plain fixed dots; the
    // yellow tarmac lamps, blue pier lights and the lighthouse all take this path.
    // Per-light data drives size and reach: SizeScale (unk08) scales the sprite,
    // MaxSizePx (unk64 = 30) caps it on approach, Range (unk68/unk52 = 1500–4000 m)
    // fades it out with distance — which also stops in-map beacons punching through the
    // fog from kilometres outside (the item-7 nit). One POINTS surface per (size, range)
    // group per mesh (uniform per mesh in practice); materials cached per param set. The
    // camera-anchored skydome's stars sit past any data range, so BuildHorizon exempts
    // them via the csky_light_fade instance uniform.
    private const string LightShaderCode = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform float size_scale = 1.0;   // data unk08 (0 -> 1)
uniform float max_size_px = 30.0; // data unk64
uniform float range_far = 0.0;    // data unk68/unk52; 0 = no distance fade
instance uniform float csky_light_fade = 1.0; // 0 = skydome stars (no range fade)
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
    ALPHA = glow * COLOR.a;
}";

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

        // One Godot surface per (material, draw priority, sidedness): polygons of
        // different priority need different materials (the priority becomes a depth
        // bias — see GetMaterial), and SHOW_BACKFACE polygons need a different cull
        // mode when backface culling is on. Groups are kept in first-occurrence order
        // = the original's within-mesh draw order; the group's rank is the
        // equal-priority tie-break (later polygons drew over earlier ones — e.g. the
        // tile meshes' roads and shoreline blends over their base grass).
        var groups = new List<(int Material, int Priority, bool DoubleSided, List<GameZPolygon> Polys)>();
        var groupIndex = new Dictionary<(int, int, bool), int>();
        foreach (var poly in mesh.Polygons)
        {
            bool doubleSided = !_cullBackfaces || poly.ShowBackface;
            var key = (poly.MaterialIndex, poly.Priority, doubleSided);
            if (!groupIndex.TryGetValue(key, out int gi))
            {
                groupIndex[key] = gi = groups.Count;
                groups.Add((poly.MaterialIndex, poly.Priority, doubleSided, new List<GameZPolygon>()));
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
            var (materialIndex, priority, doubleSided, polys) = groups[rank];
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var poly in polys)
                EmitPolygon(st, mesh, poly, offset);
            st.SetMaterial(glowSprite ? GetGlowMaterial(materialIndex)
                : cylAxis != CylAxis.None ? GetCylindricalMaterial(materialIndex, cylAxis)
                : GetMaterial(materialIndex, priority, rank, doubleSided, scroll));
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
    // night-dimmed (it's a light source, not lit scenery).
    //
    // The real discriminator is the gamez model's own data (2026-07-21): every Facade model
    // whose FacadeMode is "SphericalY" and isn't a cloud sprite is one of these — verified
    // across all 8 chapters, where that bucket is cloud1/cloud2 (the OTHER SphericalY case,
    // handled separately via _billboardTexture) plus flare/lamp/muzzle-flash/ammo-tip/splash
    // sprites and nothing else. This supersedes the former single-polygon + texture-name-
    // "flare" heuristic, which is now the LEGACY fallback for a v0.6.1 extraction (no
    // ModelType data at all): that heuristic under-matched (fire101.tif's refinery flame
    // doesn't contain "flare" and was rendered as static, non-billboarded geometry) and
    // relied on a single-polygon restriction that data now explains directly — C1's 11
    // multi-poly `flare_green` "strings" (which must NOT billboard as one sprite, or they'd
    // swing around a shared centroid) carry ModelType "Default", not "Facade", even though
    // they still carry a leftover FacadeMode value; gating on ModelType=="Facade" first is
    // what correctly excludes them without a polygon-count guess.
    private bool IsGlowSpriteMesh(GameZMesh mesh)
    {
        if (mesh.ModelType != null)
            return mesh.ModelType == "Facade" && mesh.FacadeMode == "SphericalY" && !UsesBillboardTexture(mesh);

        // Legacy fallback (no ModelType data in this extraction).
        if (_glowTexture == null || mesh.Polygons.Count != 1)
            return false;
        var poly = mesh.Polygons[0];
        if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
            return false;
        var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
        return tex != null && _glowTexture(tex);
    }

    // Y-axis or X-axis cylindrical billboard: the mesh spins about one fixed world axis to
    // face the camera on the other two, like a tree card or a flame that should stay upright
    // (as opposed to SphericalY's full camera-facing rotation). Same ModelType=="Facade" gate
    // as IsGlowSpriteMesh; None on a legacy extraction (no per-axis fallback existed before,
    // so nothing regresses — these meshes simply rendered static, as they always did).
    private enum CylAxis { None, Y, X }

    private static CylAxis GetCylindricalAxis(GameZMesh mesh) =>
        mesh.ModelType != "Facade" ? CylAxis.None :
        mesh.FacadeMode switch { "CylindricalY" => CylAxis.Y, "CylindricalX" => CylAxis.X, _ => CylAxis.None };

    // Billboard glow material for a flare sprite quad: always alpha-blended (the soft ramp
    // must never scissor into a hard star cutout), no night dimming (it's a light source).
    private readonly Dictionary<int, Material> _glowMaterialCache = new();

    private Material GetGlowMaterial(int materialIndex)
    {
        if (_glowMaterialCache.TryGetValue(materialIndex, out var cached))
            return cached;
        var texName = _gamez.Materials[materialIndex].TextureName;
        var tex = texName != null ? Resolve(texName) : null;
        Material mat = tex != null
            ? BillboardMaterial(tex, blend: true, scissor: false, glow: true)
            : GetMaterial(materialIndex, 0, 0, true);
        _glowMaterialCache[materialIndex] = mat;
        return mat;
    }

    // Cylindrical (Y- or X-axis) billboard material: unlike glow flares this respects the
    // texture's own alpha classification (trees/cables are hard-edge cutouts — Clutter's
    // tiled trees already scissor, and these individually-placed Facade trees should read
    // the same way; fire/flame sprites are soft and blend), and dims with the world's
    // SUNLIGHT UNLESS the texture is a light source itself (the caller's glowTexture
    // predicate — the same delegate the legacy spherical fallback uses, so one rule governs
    // every light-vs-scenery billboard in the renderer; WorldBuilder widened it 2026-07-21
    // to also catch the refinery's own gas flame, fire101.tif, which isn't "*flare*"-named).
    private readonly Dictionary<(int Material, int Axis), Material> _cylindricalMaterialCache = new();

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
            mat = GetMaterial(materialIndex, 0, 0, true);
        }
        _cylindricalMaterialCache[key] = mat;
        return mat;
    }

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
    public const float NodeOrderBias = 5e-8f;

    private Material GetMaterial(int materialIndex, int priority, int rank, bool doubleSided,
        Vector2 scroll = default)
    {
        rank = Math.Min(rank, SurfaceRankCap);
        var key = (materialIndex, priority, rank, doubleSided, scroll.X, scroll.Y);
        if (_materialCache.TryGetValue(key, out var cached))
            return cached;
        var mat = BuildMaterial(materialIndex, priority, rank, doubleSided, scroll);
        _materialCache[key] = mat;
        return mat;
    }

    // Every textured material this builder made, paired with the texture name it resolved
    // from — the registry a live repaint needs (the viewer's livery lab re-runs the paint
    // and swaps each material's albedo in place, instead of rebuilding the whole aircraft
    // for every slider pixel). Only shader materials carrying an `albedo_tex` are listed.
    private readonly List<(ShaderMaterial Material, string TextureName)> _texturedMaterials = new();

    /// <summary>The textured materials built here, each with its source texture name, so a
    /// caller can re-resolve and swap them without a rebuild. See <see cref="Repaint"/>.</summary>
    public IReadOnlyList<(ShaderMaterial Material, string TextureName)> TexturedMaterials => _texturedMaterials;

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

    // The archive lookup every material goes through, plus the caller's optional
    // substitution (aircraft paint). Find() must still run even when a substitute exists:
    // it is what sets LastHadAlpha/LastAlphaIsSoft, which the blend/scissor choice reads.
    private ImageTexture? Resolve(string texName)
    {
        var tex = _textures.Find(texName);
        return _textureSubstitute != null ? _textureSubstitute(texName, tex) : tex;
    }

    /// <summary>Where a material's own texture flipbook (the gamez `cycle` block) is delivered.
    /// Set by the caller before building; null leaves cycling materials static, which is what
    /// every pre-2026-07-21 caller got.</summary>
    public TextureCycler? Cycler;

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

    private Material BuildMaterial(int materialIndex, int priority, int rank, bool doubleSided,
        Vector2 scroll)
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
            // (Run-2 item 4) instead of punching through as crisp white. (Glow flares don't
            // branch here — their billboard treatment is per-MESH, see BuildMesh: the same
            // flare texture also skins polys inside regular geometry, which must stay put.)
            if (_billboardTexture != null && _billboardTexture(texName))
                return BillboardMaterial(tex, blend, scissor);
            var textured = BiasMaterial(priority, rank, doubleSided, tex, null, blend, scissor, scroll);
            _texturedMaterials.Add((textured, texName)); // for a live repaint, see Repaint()
            RegisterCycle(src, textured);
            return textured;
        }

        var color = src?.Color ?? Colors.White;
        return BiasMaterial(priority, rank, doubleSided, null, color, blend: color.A < 1f, scissor: false);
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
    private ShaderMaterial BiasMaterial(int priority, int rank, bool doubleSided, ImageTexture? tex,
        Color? color, bool blend, bool scissor, Vector2 scroll = default)
    {
        // Only a textured surface can scroll its UVs (a Colored material has no sampler).
        bool scrolls = tex != null && scroll != Vector2.Zero;
        var mat = new ShaderMaterial
        {
            Shader = GetBiasShader(shaded: !_fullbright, textured: tex != null, blend, scissor, doubleSided, scrolls),
        };
        float bias = Mathf.Clamp(priority * DepthBiasPerLevel, -0.05f, 0.05f) + rank * SurfaceRankBias;
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
        bool scroll = false)
    {
        int key = (shaded ? 1 : 0) | (textured ? 2 : 0) | (blend ? 4 : 0) | (scissor ? 8 : 0) | (doubleSided ? 16 : 0)
            | (scroll ? 32 : 0);
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
        sb.AppendLine("instance uniform float node_bias = 0.0;"); // per-node draw-order tie-break
        // Distance fog (weather.json FOG_COLOR/FOG_RANGES/FOG_ALTITUDE): globals set once per
        // flight so all world + aircraft surfaces share them without per-material updates; no-op
        // ranges when not flying. The original's fog volume is a vertical CYLINDER around the
        // camera, not a sphere: only horizontal (x/z) distance fogs, scaled by an altitude
        // fade-out — full fog below FOG_ALTITUDE.x, none above FOG_ALTITUDE.y — so the overcast
        // deck / anything at sky altitude overhead stays clear instead of graying out
        // (user-diagnosed; corroborated by zone1's altitudes 970→1047 = exactly cloud-band
        // bottom → whiteout-band centre). The camera-anchored skydome still opts out per
        // instance (csky_fog_on = 0) — its below-horizon skirt sits at low altitude ~22 km out
        // and would otherwise fog solid gray.
        sb.AppendLine("global uniform vec3 csky_fog_color;");
        sb.AppendLine("global uniform vec2 csky_fog_range;"); // x = near (clear), y = far (full fog), horizontal metres
        sb.AppendLine("global uniform vec2 csky_fog_alt;");   // fragment altitude: full fog below x, fades to none at y
        sb.AppendLine("instance uniform float csky_fog_on = 1.0;");
        // Per-mission world brightness from the weather's SUNLIGHT (item 6): the fullbright world
        // is dimmed by this scalar before fog, matching the original's ambient+diffuse lighting of
        // the baked-vertex world. Fullbright only — shaded (plane) surfaces are lit for real.
        if (!shaded)
        {
            sb.AppendLine("global uniform float csky_world_light = 1.0;");
            // The animated world's point lights (LIGHT_STATE). The world is unshaded, so a real
            // Godot light contributes nothing to it — the original's DX7 point lights modulated
            // the same baked vertex lighting this shader reads, i.e. what they do is spill onto
            // nearby geometry. Packed as a 2×N data texture because global uniforms cannot be
            // arrays; see WorldLights for the layout. csky_light_count = 0 skips the loop, which
            // is what keeps an unlit world bit-identical to the pre-lights renderer.
            sb.AppendLine("global uniform sampler2D csky_light_data;");
            sb.AppendLine("global uniform int csky_light_count;");
            sb.AppendLine(@"
// Measured with --perf on C1's harbour (16 lights live): this loop costs nothing worth
// optimising — the whole viewport's GPU time is 0.26 ms with lights on. An earlier
// bounding-sphere early-out here was removed as unmeasurable complexity; the real cost of
// LIGHT_STATE was always C# (see AnimRuntime's host-resolution cache).
vec3 csky_light_spill(vec3 world_pos, vec3 normal) {
    vec3 spill = vec3(0.0);
    for (int i = 0; i < csky_light_count; i++) {
        vec4 pr = texelFetch(csky_light_data, ivec2(0, i), 0); // xyz = position, w = range max
        vec4 cr = texelFetch(csky_light_data, ivec2(1, i), 0); // rgb = linear colour, a = range min
        vec3 to_light = pr.xyz - world_pos;
        float dist = length(to_light);
        float ndl = max(dot(normal, to_light / max(dist, 0.0001)), 0.0);
        spill += cr.rgb * ndl * (1.0 - smoothstep(cr.a, pr.w, dist));
    }
    return spill;
}");
        }
        if (textured)
            // Anisotropic mipmap filtering: the world is viewed at grazing angles from the
            // air, where plain isotropic mipmap selection blurs the ground to mush (the C5
            // "blurry city ground" report — and it is NOT a missing hi-res archive: the base
            // texture set is already max-res, rtexture2/4/6/8 are downscaled quality tiers and
            // rtexture14 == base). Anisotropic sharpens the receding ground without new assets.
            sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;");
        // UV animation (the model's texture_scroll / the boot script's Object3DSetScroll): the
        // waterfalls' falling sheet, the boats' wake fronts, the oil-dock conveyor and the
        // daytime sky layer. Emitted only for surfaces that actually scroll, so every other
        // material's shader text is byte-for-byte what it always was. `repeat_enable` above is
        // what makes the offset wrap instead of clamping at the UV edge.
        //
        // TIME wraps at Godot's rendering/limits/time/time_rollover_secs (3600 by default), and
        // every rate in this install (0.07 / 0.4 / 0.5 / 0.7 / 1.0) times 3600 is a whole number
        // of texture repeats, so the wrap lands on the identical frame — no visible jump.
        if (scroll)
            sb.AppendLine("uniform vec2 scroll_rate = vec2(0.0);");
        if (!textured)
            sb.AppendLine("uniform vec4 albedo_color : source_color = vec4(1.0);");
        // Fullbright world only: the DX7 fixed-function pipeline multiplied texture × baked
        // vertex colour (D3DTOP_MODULATE) in GAMMA (sRGB) space; we render in linear space, so
        // our multiply comes out too bright / desaturated wherever the baked colour is < 1
        // (shadows/AO — 30% of the C1 world's corners). Linearising the vertex colour before
        // the (already-linear) texture multiply reproduces the gamma-space product. (item 6.)
        if (!shaded)
            sb.AppendLine(SrgbToLinearFn);
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
            : scroll ? "    vec4 base_col = texture(albedo_tex, UV + scroll_rate * TIME);"
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
        sb.AppendLine("    float fog_amt = smoothstep(csky_fog_range.x, csky_fog_range.y, distance(fog_world.xz, CAMERA_POSITION_WORLD.xz))");
        sb.AppendLine("        * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));");
        sb.AppendLine("    ALBEDO = mix(ALBEDO, csky_fog_color, csky_fog_on * fog_amt);");
        if (blend || scissor)
            sb.AppendLine("    ALPHA = col.a;");
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
        // Same global distance-fog params as GetBiasShader's world shader. No csky_fog_on
        // instance uniform — clouds always fog, and omitting it keeps the sprites off the
        // instance-uniform buffer (see the Run-2 item-2 buffer note).
        sb.AppendLine("global uniform vec3 csky_fog_color;");
        sb.AppendLine("global uniform vec2 csky_fog_range;");
        sb.AppendLine("global uniform vec2 csky_fog_alt;");
        sb.AppendLine("global uniform float csky_world_light = 1.0;"); // per-mission SUNLIGHT dimming (item 6)
        sb.AppendLine(SrgbToLinearFn); // DX7 gamma-space vertex modulate (world/cloud pass)
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
    float fog_amt = smoothstep(csky_fog_range.x, csky_fog_range.y, distance(fog_world.xz, CAMERA_POSITION_WORLD.xz))
        * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));");
        // Glow flares are light sources: no SUNLIGHT night dimming (a lamp doesn't get
        // darker at night — it's what lights the scene). Clouds ride the world brightness.
        sb.AppendLine(glow
            ? "    ALBEDO = mix(col.rgb, csky_fog_color, fog_amt);"
            : "    ALBEDO = mix(col.rgb * csky_world_light, csky_fog_color, fog_amt);");
        if (blend || scissor)
            sb.AppendLine("    ALPHA = col.a;");
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
        sb.AppendLine("global uniform vec3 csky_fog_color;");
        sb.AppendLine("global uniform vec2 csky_fog_range;");
        sb.AppendLine("global uniform vec2 csky_fog_alt;");
        if (!glow)
            sb.AppendLine("global uniform float csky_world_light = 1.0;"); // per-mission SUNLIGHT dimming (item 6)
        sb.AppendLine(SrgbToLinearFn);
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
    float fog_amt = smoothstep(csky_fog_range.x, csky_fog_range.y, distance(fog_world.xz, CAMERA_POSITION_WORLD.xz))
        * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));");
        sb.AppendLine(glow
            ? "    ALBEDO = mix(col.rgb, csky_fog_color, fog_amt);"
            : "    ALBEDO = mix(col.rgb * csky_world_light, csky_fog_color, fog_amt);");
        if (blend || scissor)
            sb.AppendLine("    ALPHA = col.a;");
        if (scissor)
            sb.AppendLine("    ALPHA_SCISSOR_THRESHOLD = 0.5;");
        sb.AppendLine("}");

        var shader = new Shader { Code = sb.ToString() };
        _cylindricalShaderCache[key] = shader;
        return shader;
    }

    private static string Sanitize(string name)
    {
        // Godot node names must not contain . : @ / " %
        Span<char> bad = stackalloc[] { '.', ':', '@', '/', '"', '%' };
        foreach (var ch in bad)
            name = name.Replace(ch, '_');
        return name.Length == 0 ? "node" : name;
    }
}
