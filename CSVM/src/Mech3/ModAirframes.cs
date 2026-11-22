using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Plane mods' airframes, grafted into the aircraft archive, so every planes.zbd reader reads a mod
/// as a stock airframe. The tree of a mod's <c>plane.glb</c> goes under a root named its id (<see cref="Add"/>).
/// Each glTF node becomes a gamez node at its own transform. A meshed one holds its triangles under one
/// untextured material, which the colliders, hit body and boxes read. The drawn mesh is the file's
/// own, as the airframe's root file (<see cref="MeshesFor"/>), so its materials follow the model pack
/// rules. Format: docs/formats/mod-planes.md.
/// </summary>
public static class ModAirframes
{
    private const string Interior = "cockpit1";

    // Each grafted mod's own file, by id, which every build of that airframe draws from.
    private static readonly Dictionary<string, HdModelMeshes> Files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Grafts the airframe in <paramref name="glb"/> into <paramref name="planes"/> under the root
    /// <paramref name="id"/>. A file holding no <c>cockpit1</c> under its root gets a copy of
    /// <paramref name="cockpitFrom"/>'s. Throws <see cref="InvalidDataException"/> for a file that does not
    /// import or an id the archive already holds, leaving the archive as it was.</summary>
    public static void Add(GameZ planes, string id, string cockpitFrom, byte[] glb)
    {
        string key = $"planes/{id.ToLowerInvariant()}/{id.ToLowerInvariant()}";
        var meshes = new HdModelMeshes(HdModels.Holding(key, new HdModel(glb, IsGlb: true, GltfPath: null)));
        var state = meshes.StateOf(key) ?? throw new InvalidDataException("its model does not import as glTF");
        var nodes = state.GetNodes();
        var fileMeshes = state.GetMeshes();
        var parts = new List<Part>();
        foreach (var node in nodes)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            if (node.Mesh >= 0 && node.Mesh < fileMeshes.Count && fileMeshes[node.Mesh].Mesh is { } mesh)
            {
                // Already in the original's winding, as the drawn mesh is.
                foreach (var surface in HdModelMeshes.ReversedSurfaces(mesh, Transform3D.Identity))
                {
                    if (surface.Primitive != Mesh.PrimitiveType.Triangles)
                    {
                        continue;
                    }

                    int first = vertices.Count;
                    vertices.AddRange(surface.Arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array());
                    triangles.AddRange(surface.Arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Select(i => first + i));
                }
            }

            parts.Add(new Part(HdModelMeshes.WithoutRepeat(HdModelMeshes.NameOf(node)), node.Xform, node.Children, vertices, triangles));
        }

        Graft(planes, id, cockpitFrom, parts, state.RootNodes);
        lock (Files)
        {
            Files[id] = meshes;
        }
    }

    /// <summary>The file a build of the airframe <paramref name="node"/> draws from, or null for any
    /// airframe <see cref="Add"/> has not grafted.</summary>
    public static HdModelMeshes? MeshesFor(string node)
    {
        lock (Files)
        {
            return Files.GetValueOrDefault(node);
        }
    }

    /// <summary>The engine-free half of <see cref="Add"/>: appends <paramref name="parts"/> under a new
    /// root <paramref name="id"/>, starting from <paramref name="roots"/>. A lone root named like a stock
    /// airframe or the id (an export's <c>player_brigand</c>) stands for that root, its transform dropped.</summary>
    internal static void Graft(GameZ planes, string id, string cockpitFrom, IReadOnlyList<Part> parts, IReadOnlyList<int> roots)
    {
        if (planes.FindByName(id) != null)
        {
            throw new InvalidDataException($"the aircraft archive already holds a node named {id}");
        }

        var tops = roots.Count == 1 && roots[0] >= 0 && roots[0] < parts.Count
            && (AirframeRegistry.Stock.Any(a => a.Node.Equals(parts[roots[0]].Name, StringComparison.OrdinalIgnoreCase))
                || parts[roots[0]].Name.Equals(id, StringComparison.OrdinalIgnoreCase))
            ? parts[roots[0]].Children : roots;
        int material = planes.Materials.Count;
        planes.Materials.Add(new GameZMaterial());
        var root = new GameZNode { Kind = "Object3d", Name = id, Index = planes.Nodes.Count };
        planes.Nodes.Add(root);
        // ⚠ Keep the placed stop: a file whose children loop, or list one node twice, would graft forever.
        var placed = new HashSet<int>();
        int Place(int at)
        {
            var part = parts[at];
            var node = new GameZNode { Kind = "Object3d", Name = part.Name, Local = part.Local, Index = planes.Nodes.Count };
            planes.Nodes.Add(node);
            if (part.Triangles.Count >= 3)
            {
                var mesh = new GameZMesh();
                mesh.Vertices.AddRange(part.Vertices);
                for (int t = 0; t + 2 < part.Triangles.Count; t += 3)
                {
                    var polygon = new GameZPolygon { MaterialIndex = material };
                    polygon.VertexIndices.AddRange(new[] { part.Triangles[t], part.Triangles[t + 1], part.Triangles[t + 2] });
                    mesh.Polygons.Add(polygon);
                }

                node.MeshIndex = planes.Meshes.Count;
                planes.Meshes.Add(mesh);
            }

            foreach (int child in part.Children)
            {
                if (child >= 0 && child < parts.Count && placed.Add(child))
                {
                    node.Children.Add(Place(child));
                }
            }

            return node.Index;
        }

        foreach (int top in tops)
        {
            if (top >= 0 && top < parts.Count && placed.Add(top))
            {
                root.Children.Add(Place(top));
            }
        }

        if (!planes.ChildrenOf(root).Exists(IsInterior)
            && planes.FindByName(cockpitFrom) is { } donor
            && planes.ChildrenOf(donor).Find(IsInterior) is { } interior)
        {
            root.Children.Add(Copy(planes, interior));
        }

        planes.TablesGrew();
    }

    private static bool IsInterior(GameZNode node) => node.Name.Equals(Interior, StringComparison.OrdinalIgnoreCase);

    // A copy of `node`'s subtree appended to the tables, sharing its meshes; the copy's index.
    private static int Copy(GameZ gamez, GameZNode node)
    {
        var copy = new GameZNode
        {
            Kind = node.Kind,
            Name = node.Name,
            MeshIndex = node.MeshIndex,
            IntersectSurface = node.IntersectSurface,
            Active = node.Active,
            ZoneId = node.ZoneId,
            Local = node.Local,
            LodRangeMin = node.LodRangeMin,
            Index = gamez.Nodes.Count,
        };
        gamez.Nodes.Add(copy);
        foreach (var child in gamez.ChildrenOf(node))
        {
            copy.Children.Add(Copy(gamez, child));
        }

        return copy.Index;
    }

    /// <summary>One glTF node as the graft reads it: its name without a repeat suffix, its transform and
    /// its children by file index. Its own mesh's triangles come in the original's winding.</summary>
    internal sealed record Part(string Name, Transform3D Local, IReadOnlyList<int> Children, IReadOnlyList<Vector3> Vertices, IReadOnlyList<int> Triangles);
}
