using System;
using System.Collections.Generic;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The replacement mesh for a gamez node, imported once per pack key from its <see cref="HdModels"/>
/// file and shared by every caller. The mesh comes back in the original's winding, which glTF cannot
/// carry, so every triangle surface is reversed on the way in (docs/formats/gotchas.md). Surface
/// materials stay as the file set them, and a failed import is cached as null.
/// Inside a file the mesh is the one on the glTF node named like the gamez node, compared as
/// <see cref="HdModels"/> compares names. A named node without a mesh lends its one direct child's
/// mesh, since <see cref="SceneBuilder"/> and so the exporter put it there.
/// A file holding exactly one mesh is that mesh, whatever its nodes are named.
/// </summary>
public sealed class HdModelMeshes
{
    private static readonly object CurrentLock = new();
    private static HdModelMeshes? _current;

    private readonly HdModels _models;
    private readonly Dictionary<string, ArrayMesh?> _byKey = new();

    public HdModelMeshes(HdModels models)
    {
        _models = models;
    }

    /// <summary>The meshes over <see cref="HdModels.Current"/>, started afresh whenever that lookup is
    /// replaced, so a cache never outlives the folder it was read from.</summary>
    public static HdModelMeshes Current
    {
        get
        {
            lock (CurrentLock)
            {
                if (_current == null || !ReferenceEquals(_current._models, HdModels.Current))
                {
                    _current = new HdModelMeshes(HdModels.Current);
                }

                return _current;
            }
        }
    }

    /// <summary>The replacement mesh for the gamez node named <paramref name="node"/>, or null when
    /// the pack has none or its file failed to import. Keys resolve as <see cref="HdModels.For"/>
    /// resolves them. ⚠ Do not edit the result; every node replaced from that key draws it.</summary>
    public ArrayMesh? For(string node, string? chapter = null, string? airframe = null)
    {
        if (_models.KeyFor(node, chapter, airframe) is not { } key)
        {
            return null;
        }

        // ⚠ Do not import outside the lock; two builders asking at once would each import and
        // hand out two meshes for one key.
        lock (_byKey)
        {
            if (!_byKey.TryGetValue(key, out var mesh))
            {
                mesh = Load(key);
                _byKey[key] = mesh;
            }

            return mesh;
        }
    }

    /// <summary><paramref name="source"/> as a mesh with every triangle surface's winding flipped,
    /// attributes and materials carried across. An unindexed surface gets the identity index list
    /// first. Other primitives pass through unflipped, and blend shapes and LODs are dropped.</summary>
    internal static ArrayMesh Reversed(ImporterMesh source)
    {
        // Built from the importer's own arrays, not its GetMesh(). That compresses the attributes,
        // moving every vertex by up to 1/65536 of the mesh's extent.
        var result = new ArrayMesh { ResourceName = source.ResourceName };
        for (int i = 0; i < source.GetSurfaceCount(); i++)
        {
            var arrays = source.GetSurfaceArrays(i);
            var primitive = source.GetSurfacePrimitiveType(i);
            if (primitive == Mesh.PrimitiveType.Triangles)
            {
                arrays[(int)Mesh.ArrayType.Index] = ReversedIndices(arrays);
            }

            result.AddSurfaceFromArrays(primitive, arrays);
            result.SurfaceSetName(i, source.GetSurfaceName(i));
            result.SurfaceSetMaterial(i, source.GetSurfaceMaterial(i));
        }

        return result;
    }

    private static int[] ReversedIndices(Godot.Collections.Array arrays)
    {
        var indices = arrays[(int)Mesh.ArrayType.Index].As<int[]>() ?? Array.Empty<int>();
        if (indices.Length == 0)
        {
            indices = new int[arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>().Length];
            for (int v = 0; v < indices.Length; v++)
            {
                indices[v] = v;
            }
        }

        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            (indices[t], indices[t + 2]) = (indices[t + 2], indices[t]);
        }

        return indices;
    }

    // The node part of a pack key: what follows its chapter or airframe folder.
    private static string NodeOf(string key) => key[(key.LastIndexOf('/') + 1)..];

    // A glTF node's name as the file wrote it. The generated name replaces Godot's invalid
    // characters, which would lose the dot in a gamez name.
    private static string NameOf(GltfNode node) =>
        node.OriginalName is { Length: > 0 } original ? original : node.ResourceName;

    // The mesh index for the gamez node `nodeKey` under the rule on the type, or -1.
    private static int MeshIndexFor(Godot.Collections.Array<GltfNode> nodes, int meshCount, string nodeKey)
    {
        foreach (var node in nodes)
        {
            if (HdModels.NodeKey(NameOf(node)) != nodeKey)
            {
                continue;
            }

            if (node.Mesh >= 0)
            {
                return node.Mesh;
            }

            // The writer renames a repeated name, so the child is found by its mesh, not as "mesh".
            int lent = -1;
            foreach (int child in node.Children)
            {
                if (child >= 0 && child < nodes.Count && nodes[child].Mesh >= 0)
                {
                    lent = lent == -1 ? nodes[child].Mesh : -2;
                }
            }

            if (lent >= 0)
            {
                return lent;
            }
        }

        return meshCount == 1 ? 0 : -1;
    }

    // Read straight off the parsed state, not a generated scene. No node is built to be freed, and
    // the node names are the file's own.
    private static ArrayMesh? Import(HdModel model, string nodeKey, string file)
    {
        var document = new GltfDocument();
        var state = new GltfState();
        // The base path is the file itself: the reader resolves buffers against its folder.
        var error = document.AppendFromBuffer(model.Bytes, model.GltfPath ?? string.Empty, state);
        if (error != Error.Ok)
        {
            Log.Warn("world", $"HD model import failed ({error}): {file}");
            return null;
        }

        var meshes = state.GetMeshes();
        int index = MeshIndexFor(state.GetNodes(), meshes.Count, nodeKey);
        if (index < 0 || index >= meshes.Count || meshes[index].Mesh is not { } imported
            || imported.GetSurfaceCount() == 0)
        {
            Log.Warn("world", $"HD model import failed (no mesh for node {nodeKey} among {meshes.Count}): {file}");
            return null;
        }

        return Reversed(imported);
    }

    // ⚠ Never throws: a broken pack file costs its own replacement, not the build that asked.
    private ArrayMesh? Load(string key)
    {
        if (_models.Read(key) is not { } model)
        {
            Log.Warn("world", $"HD model import failed (unreadable): {key}");
            return null;
        }

        string file = model.GltfPath ?? key + (model.IsGlb ? ".glb" : ".gltf");
        try
        {
            return Import(model, NodeOf(key), file);
        }
        catch (Exception e)
        {
            Log.Warn("world", $"HD model import failed ({e.GetType().Name}: {e.Message}): {file}");
            return null;
        }
    }
}
