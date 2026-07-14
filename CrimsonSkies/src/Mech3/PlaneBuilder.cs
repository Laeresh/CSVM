using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Builds a renderable Godot node tree for one aircraft out of a planes.zbd GameZ model.
/// Exterior view only: picks the nearest LOD, skips cockpit/damage/destroyed variants.
/// </summary>
public sealed class PlaneBuilder
{
    // Subtrees that make no sense in a static exterior view. Cockpit interiors are
    // separate (differently-scaled) models; damage/destroyed are alternate states;
    // prop1/prop2/nitroprop are spinning-propeller animation frames (staticprop stays).
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "shadow", "player_damage_on", "blood_hook",
        "prop1", "prop1b", "prop2", "prop2b", "nitroprop1", "nitroprop2",
    };

    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly Dictionary<int, StandardMaterial3D> _materialCache = new();

    public int MeshInstanceCount { get; private set; }

    public PlaneBuilder(GameZ gamez, TextureArchive textures)
    {
        _gamez = gamez;
        _textures = textures;
    }

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk").</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        return BuildNode(root)!;
    }

    private Node3D? BuildNode(GameZNode node)
    {
        if (SkipNames.Contains(node.Name))
            return null;
        // Of each LOD group, keep only the highest-detail level (range starts at 0).
        if (node.Kind == "Lod" && node.LodRangeMin != 0f)
            return null;

        var n3d = new Node3D { Name = Sanitize(node.Name) };
        if (node.Local is { } local)
            n3d.Transform = local;

        if (node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count)
        {
            var mesh = BuildMesh(_gamez.Meshes[node.MeshIndex]);
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
            var child = BuildNode(_gamez.Nodes[childIndex]);
            if (child != null)
                n3d.AddChild(child);
        }
        return n3d;
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
        };
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
