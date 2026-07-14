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
    private readonly Dictionary<int, StandardMaterial3D> _materialCache = new();
    private readonly Dictionary<int, ArrayMesh?> _meshCache = new();

    public int MeshInstanceCount { get; private set; }

    /// <param name="fullbright">Render unshaded, like the original engine's world pass:
    /// texture × baked vertex color, ignoring scene lights. Used for world geometry.</param>
    public SceneBuilder(GameZ gamez, TextureArchive textures, bool fullbright = false)
    {
        _gamez = gamez;
        _textures = textures;
        _fullbright = fullbright;
    }

    /// <summary>Builds the subtree rooted at <paramref name="node"/>; null if skipped entirely.</summary>
    public Node3D? BuildSubtree(GameZNode node, Predicate<GameZNode>? skip = null)
    {
        if (skip != null && skip(node))
            return null;
        // Of each LOD group, keep only the highest-detail level (range starts at 0).
        if (node.Kind == "Lod" && node.LodRangeMin != 0f)
            return null;

        var n3d = new Node3D { Name = Sanitize(node.Name) };
        if (node.Local is { } local)
            n3d.Transform = local;

        if (node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count)
        {
            var mesh = GetMesh(node.MeshIndex);
            if (mesh != null)
            {
                n3d.AddChild(new MeshInstance3D { Mesh = mesh, Name = "mesh" });
                MeshInstanceCount++;
            }
        }

        foreach (var childIndex in node.Children)
        {
            if (childIndex < 0 || childIndex >= _gamez.Nodes.Count)
                continue;
            var child = BuildSubtree(_gamez.Nodes[childIndex], skip);
            if (child != null)
                n3d.AddChild(child);
        }
        return n3d;
    }

    private ArrayMesh? GetMesh(int meshIndex)
    {
        if (_meshCache.TryGetValue(meshIndex, out var cached))
            return cached;
        var mesh = BuildMesh(_gamez.Meshes[meshIndex]);
        _meshCache[meshIndex] = mesh;
        return mesh;
    }

    private ArrayMesh? BuildMesh(GameZMesh mesh)
    {
        if (mesh.Polygons.Count == 0)
            return null;

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
                EmitPolygon(st, mesh, poly);
            st.SetMaterial(GetMaterial(materialIndex));
            st.Commit(arrayMesh);
        }
        return arrayMesh;
    }

    private static void EmitPolygon(SurfaceTool st, GameZMesh mesh, GameZPolygon poly)
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
                    EmitTriangle(st, mesh, poly, i, i + 1, i + 2);
                else
                    EmitTriangle(st, mesh, poly, i, i + 2, i + 1);
            }
        }
        else
        {
            for (int i = 1; i + 1 < n; i++)
                EmitTriangle(st, mesh, poly, 0, i, i + 1);
        }
    }

    private static void EmitTriangle(SurfaceTool st, GameZMesh mesh, GameZPolygon poly, int a, int b, int c)
    {
        Vector3 Pos(int corner) => mesh.Vertices[poly.VertexIndices[corner]];
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
                        mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
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
