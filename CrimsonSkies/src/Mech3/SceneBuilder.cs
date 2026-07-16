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
    private readonly Dictionary<(int Material, int Priority, int Rank, bool DoubleSided), Material> _materialCache = new();
    private readonly Dictionary<int, Shader> _biasShaderCache = new(); // keyed by feature bits
    private readonly Dictionary<int, ArrayMesh?> _meshCache = new();
    private readonly Dictionary<int, Vector3> _meshPivotCache = new(); // billboard meshes only: local quad center
    private readonly Dictionary<int, ArrayMesh> _lightMeshCache = new();
    private readonly Dictionary<int, ConcavePolygonShape3D?> _shapeCache = new();
    private StandardMaterial3D? _lightPointMaterial;

    public int MeshInstanceCount { get; private set; }
    public int ColliderCount { get; private set; }

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
    public SceneBuilder(GameZ gamez, TextureArchive textures, bool fullbright = false,
        bool generateCollision = false, Func<string, bool>? blendTexture = null,
        Func<string, bool>? billboardTexture = null, bool cullBackfaces = false)
    {
        _gamez = gamez;
        _textures = textures;
        _fullbright = fullbright;
        _generateCollision = generateCollision;
        _blendTexture = blendTexture;
        _billboardTexture = billboardTexture;
        _cullBackfaces = cullBackfaces;
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

    // One POINTS-primitive surface per mesh; fixed screen-size dots, additive blend so
    // they glow over whatever is behind them (and pure-black lights become invisible).
    private ArrayMesh GetLightPoints(int meshIndex)
    {
        if (_lightMeshCache.TryGetValue(meshIndex, out var cached))
            return cached;

        var lights = _gamez.Meshes[meshIndex].Lights;
        var points = new Vector3[lights.Count];
        var colors = new Color[lights.Count];
        for (int i = 0; i < lights.Count; i++)
        {
            points[i] = lights[i].Position;
            colors[i] = lights[i].Color;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points;
        arrays[(int)Mesh.ArrayType.Color] = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);
        _lightPointMaterial ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            UsePointSize = true,
            PointSize = 3f, // TUNE: source size params (0.17/30/4000/6000) not yet decoded
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, // transparent pass: no depth write
        };
        mesh.SurfaceSetMaterial(0, _lightPointMaterial);
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

        // Billboard sprites (clouds) are recentered on their quad center: the material
        // billboards around the mesh origin, but the source quads sit offset from it, so
        // without this they would swing around the node as the camera turns. The offset is
        // stored per mesh; every instance re-applies it as its local position.
        var offset = Vector3.Zero;
        if (UsesBillboardTexture(mesh) && mesh.Vertices.Count > 0)
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

        var arrayMesh = new ArrayMesh();
        for (int rank = 0; rank < groups.Count; rank++)
        {
            var (materialIndex, priority, doubleSided, polys) = groups[rank];
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var poly in polys)
                EmitPolygon(st, mesh, poly, offset);
            st.SetMaterial(GetMaterial(materialIndex, priority, rank, doubleSided));
            st.Commit(arrayMesh);
        }
        return arrayMesh;
    }

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

    private Material GetMaterial(int materialIndex, int priority, int rank, bool doubleSided)
    {
        rank = Math.Min(rank, SurfaceRankCap);
        var key = (materialIndex, priority, rank, doubleSided);
        if (_materialCache.TryGetValue(key, out var cached))
            return cached;
        var mat = BuildMaterial(materialIndex, priority, rank, doubleSided);
        _materialCache[key] = mat;
        return mat;
    }

    private Material BuildMaterial(int materialIndex, int priority, int rank, bool doubleSided)
    {
        var src = materialIndex >= 0 && materialIndex < _gamez.Materials.Count
            ? _gamez.Materials[materialIndex]
            : null;

        if (src?.TextureName is { } texName)
        {
            var tex = _textures.Find(texName);
            if (tex == null)
                return NewStandard(albedoColor: Colors.Magenta); // make missing textures obvious

            // Soft-alpha textures (baked shadow decals, clouds, prop blur, waterfalls,
            // smoke — detected from the pixels, see TextureArchive.LastAlphaIsSoft) and
            // the caller's explicit blend list alpha-blend; every other alpha texture
            // scissors (hard cutout — right for fences, trees, railings).
            bool blend = _textures.LastHadAlpha
                && (_textures.LastAlphaIsSoft || (_blendTexture != null && _blendTexture(texName)));
            bool scissor = _textures.LastHadAlpha && !blend;
            // Cloud sprites face the camera (their meshes are recentered to match). They
            // keep StandardMaterial3D: the bias shader has no billboard support, and
            // free-floating sprites have nothing to z-fight with.
            if (_billboardTexture != null && _billboardTexture(texName))
            {
                var mat = NewStandard();
                mat.AlbedoTexture = tex;
                if (blend)
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                else if (scissor)
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
                mat.BillboardKeepScale = true;
                return mat;
            }
            return BiasMaterial(priority, rank, doubleSided, tex, null, blend, scissor);
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
    private ShaderMaterial BiasMaterial(int priority, int rank, bool doubleSided, ImageTexture? tex, Color? color, bool blend, bool scissor)
    {
        var mat = new ShaderMaterial
        {
            Shader = GetBiasShader(shaded: !_fullbright, textured: tex != null, blend, scissor, doubleSided),
        };
        float bias = Mathf.Clamp(priority * DepthBiasPerLevel, -0.05f, 0.05f) + rank * SurfaceRankBias;
        mat.SetShaderParameter("depth_bias", bias);
        if (tex != null)
            mat.SetShaderParameter("albedo_tex", tex);
        if (color is { } c)
            mat.SetShaderParameter("albedo_color", c);
        return mat;
    }

    private Shader GetBiasShader(bool shaded, bool textured, bool blend, bool scissor, bool doubleSided)
    {
        int key = (shaded ? 1 : 0) | (textured ? 2 : 0) | (blend ? 4 : 0) | (scissor ? 8 : 0) | (doubleSided ? 16 : 0);
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
        // Distance fog (weather.json FOG_COLOR/FOG_RANGES): globals set once per flight so all
        // world + aircraft surfaces share them without per-material updates; no-op range when
        // not flying. The camera-anchored skydome opts out per instance (csky_fog_on = 0) — at
        // ~22 km it is past FOG_FAR and would otherwise fog the whole sky solid gray.
        sb.AppendLine("global uniform vec3 csky_fog_color;");
        sb.AppendLine("global uniform vec2 csky_fog_range;"); // x = near (clear), y = far (full fog)
        sb.AppendLine("instance uniform float csky_fog_on = 1.0;");
        if (textured)
            sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;");
        else
            sb.AppendLine("uniform vec4 albedo_color : source_color = vec4(1.0);");
        sb.AppendLine(@"
void vertex() {
    VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    NORMAL = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
    // Scale toward the eye (the view-space origin): identical projected position,
    // depth nudged nearer by bias × distance — a scale-invariant polygon offset.
    VERTEX *= 1.0 - (depth_bias + node_bias);
}

void fragment() {");
        sb.AppendLine(textured
            ? "    vec4 col = COLOR * texture(albedo_tex, UV);"
            : "    vec4 col = COLOR * albedo_color;");
        sb.AppendLine("    ALBEDO = col.rgb;");
        if (shaded)
        {
            sb.AppendLine("    ROUGHNESS = 0.85;");
            sb.AppendLine("    METALLIC = 0.0;");
            sb.AppendLine("    SPECULAR = 0.5;");
        }
        // Distance fog: VERTEX is the view-space position here (set in vertex() under
        // skip_vertex_transform), so length(VERTEX) is the distance from the camera. The
        // aircraft is always within the near range at chase distance, so this is a no-op on it.
        sb.AppendLine("    ALBEDO = mix(ALBEDO, csky_fog_color, csky_fog_on * smoothstep(csky_fog_range.x, csky_fog_range.y, length(VERTEX)));");
        if (blend || scissor)
            sb.AppendLine("    ALPHA = col.a;");
        if (scissor)
            sb.AppendLine("    ALPHA_SCISSOR_THRESHOLD = 0.5;");
        sb.AppendLine("}");

        var shader = new Shader { Code = sb.ToString() };
        _biasShaderCache[key] = shader;
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
