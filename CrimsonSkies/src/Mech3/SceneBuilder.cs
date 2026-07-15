using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Turns GameZ node subtrees into renderable Godot node trees: recursion over the
/// node hierarchy, n-gon triangulation, and a shared material cache. Callers
/// (PlaneBuilder, WorldBuilder) supply what to skip; the LOD rule (keep only the
/// nearest level of each LOD group) is common and lives here.
/// </summary>
public sealed class SceneBuilder
{
    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly bool _fullbright;
    private readonly bool _generateCollision;
    private readonly Func<string, bool>? _blendTexture;
    private readonly Func<string, bool>? _billboardTexture;
    private readonly Dictionary<int, StandardMaterial3D> _materialCache = new();
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
    public SceneBuilder(GameZ gamez, TextureArchive textures, bool fullbright = false,
        bool generateCollision = false, Func<string, bool>? blendTexture = null,
        Func<string, bool>? billboardTexture = null)
    {
        _gamez = gamez;
        _textures = textures;
        _fullbright = fullbright;
        _generateCollision = generateCollision;
        _blendTexture = blendTexture;
        _billboardTexture = billboardTexture;
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

        // one Godot surface per material
        var byMaterial = new Dictionary<int, List<GameZPolygon>>();
        foreach (var poly in mesh.Polygons)
        {
            if (!byMaterial.TryGetValue(poly.MaterialIndex, out var list))
                byMaterial[poly.MaterialIndex] = list = new List<GameZPolygon>();
            list.Add(poly);
        }

        var arrayMesh = new ArrayMesh();
        foreach (var (materialIndex, polys) in byMaterial)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var poly in polys)
                EmitPolygon(st, mesh, poly, offset);
            st.SetMaterial(GetMaterial(materialIndex));
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

    private StandardMaterial3D GetMaterial(int materialIndex)
    {
        if (_materialCache.TryGetValue(materialIndex, out var cached))
            return cached;

        var mat = new StandardMaterial3D
        {
            // source winding/handedness not yet verified against Godot's — render both sides
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.85f,
            Metallic = 0.0f,
            VertexColorUseAsAlbedo = true, // baked lighting from the source data
        };
        if (_fullbright)
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (materialIndex >= 0 && materialIndex < _gamez.Materials.Count)
        {
            var src = _gamez.Materials[materialIndex];
            if (src.TextureName != null)
            {
                var tex = _textures.Find(src.TextureName);
                if (tex != null)
                {
                    mat.AlbedoTexture = tex;
                    if (_textures.LastHadAlpha)
                        // Clouds alpha-blend (soft); everything else scissors (hard cutout).
                        mat.Transparency = _blendTexture != null && _blendTexture(src.TextureName)
                            ? BaseMaterial3D.TransparencyEnum.Alpha
                            : BaseMaterial3D.TransparencyEnum.AlphaScissor;
                    // Cloud sprites face the camera (their meshes are recentered to match).
                    if (_billboardTexture != null && _billboardTexture(src.TextureName))
                    {
                        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
                        mat.BillboardKeepScale = true;
                    }
                }
                else
                {
                    mat.AlbedoColor = Colors.Magenta; // make missing textures obvious
                }
            }
            else
            {
                mat.AlbedoColor = src.Color;
                if (src.Color.A < 1f)
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            }
        }
        _materialCache[materialIndex] = mat;
        return mat;
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
