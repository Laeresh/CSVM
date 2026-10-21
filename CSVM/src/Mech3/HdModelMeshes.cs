using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The replacement mesh for a gamez node, imported once per pack key from its <see cref="HdModels"/>
/// file and shared by every caller, a failed import as null. The mesh comes back in the original's winding, which glTF cannot
/// carry, so every triangle surface is reversed on the way in (docs/formats/gotchas.md). Surface
/// materials stay as the file set them; a builder draws a copy under its own (<see cref="SurfacesOf"/>).
/// Which meshes of a file merge, and how they are placed, is the rule in docs/architecture/Mech3.md.
/// </summary>
public sealed class HdModelMeshes
{
    private static readonly object CurrentLock = new();
    private static readonly System.Text.RegularExpressions.Regex BlenderRepeat = new(@"\.\d{3,}$");
    private static HdModelMeshes? _current;

    private readonly HdModels _models;
    private readonly Dictionary<string, ArrayMesh?> _byKey = new();
    // Each imported mesh's surfaces as built, so a builder copies them without reading the GPU back.
    private readonly Dictionary<ArrayMesh, HdSurface[]> _surfaces = new(ReferenceEqualityComparer.Instance);
    // Each imported base-colour texture's mip-mapped copy, made on first use.
    private readonly Dictionary<Texture2D, ImageTexture?> _albedos = new(ReferenceEqualityComparer.Instance);

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
    /// the pack has none or its file failed to import. An empty mesh means the file draws nothing
    /// for the node. Keys resolve as <see cref="HdModels.For"/> resolves them, and
    /// <paramref name="owned"/> names the gamez node's descendants, whose glTF subtrees are left out.
    /// ⚠ Do not edit the result; every node replaced from that key draws it.</summary>
    public ArrayMesh? For(string node, string? chapter = null, string? airframe = null, IEnumerable<string>? owned = null) =>
        _models.KeyFor(node, chapter, airframe) is { } key ? Cached(key, NodeOf(key), null, owned) : null;

    /// <summary>The replacement for the gamez node named <paramref name="node"/> out of the pack file
    /// of its ancestor <paramref name="scope"/>. Null when the scope has no file or the file names no
    /// such node, since a scoped lookup has no whole-file fallback. The gamez names between scope and
    /// node are <paramref name="path"/>. Of several glTF nodes of the name, the one whose ancestors
    /// follow most of the path wins. Otherwise as <see cref="For"/>.</summary>
    public ArrayMesh? Scoped(string scope, IEnumerable<string> path, string node, string? chapter = null,
        string? airframe = null, IEnumerable<string>? owned = null) =>
        _models.KeyFor(scope, chapter, airframe) is { } key
            ? Cached(key, HdModels.NodeKey(node), path.Select(HdModels.NodeKey).ToArray(), owned)
            : null;

    /// <summary><paramref name="source"/> as a mesh with every triangle surface's winding flipped,
    /// attributes and materials carried across. An unindexed surface gets the identity index list
    /// first. Other primitives pass through unflipped, and blend shapes and LODs are dropped.</summary>
    internal static ArrayMesh Reversed(ImporterMesh source) => Build(ReversedSurfaces(source, Transform3D.Identity).ToArray());

    /// <summary>The surfaces <paramref name="mesh"/> was built from, in surface order, or null for a
    /// mesh this cache did not import. ⚠ Do not edit them; they are the shared mesh's own.</summary>
    internal IReadOnlyList<HdSurface>? SurfacesOf(ArrayMesh mesh)
    {
        lock (_byKey)
        {
            return _surfaces.TryGetValue(mesh, out var surfaces) ? surfaces : null;
        }
    }

    /// <summary>A mip-mapped copy of <paramref name="material"/>'s base-colour texture, made once and
    /// shared, or null when it has none. The importer decodes a glTF image without mipmaps, and the
    /// remake's shaders sample through them. The imported texture is left as it is.</summary>
    internal ImageTexture? AlbedoOf(BaseMaterial3D? material)
    {
        if (material?.AlbedoTexture is not { } source)
        {
            return null;
        }

        lock (_byKey)
        {
            if (!_albedos.TryGetValue(source, out var copy))
            {
                copy = Mipmapped(source);
                _albedos[source] = copy;
            }

            return copy;
        }
    }

    private static IEnumerable<HdSurface> ReversedSurfaces(ImporterMesh source, Transform3D at)
    {
        // A mirroring transform reverses the winding itself.
        bool mirrored = at.Basis.Determinant() < 0;
        // Built from the importer's own arrays, not its GetMesh(). That compresses the attributes,
        // moving every vertex by up to 1/65536 of the mesh's extent.
        for (int i = 0; i < source.GetSurfaceCount(); i++)
        {
            var arrays = source.GetSurfaceArrays(i);
            var primitive = source.GetSurfacePrimitiveType(i);
            if (at != Transform3D.Identity)
            {
                Place(arrays, at, mirrored);
            }

            if (primitive == Mesh.PrimitiveType.Triangles && !mirrored)
            {
                arrays[(int)Mesh.ArrayType.Index] = ReversedIndices(arrays);
            }

            yield return new HdSurface(primitive, arrays, source.GetSurfaceName(i), source.GetSurfaceMaterial(i));
        }
    }

    // Moves a surface's positions, normals and tangents by `at`.
    private static void Place(Godot.Collections.Array arrays, Transform3D at, bool mirrored)
    {
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (int v = 0; v < vertices.Length; v++)
        {
            vertices[v] = at * vertices[v];
        }

        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        if (arrays[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.PackedVector3Array)
        {
            var normalBasis = at.Basis.Inverse().Transposed();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (int n = 0; n < normals.Length; n++)
            {
                normals[n] = (normalBasis * normals[n]).Normalized();
            }

            arrays[(int)Mesh.ArrayType.Normal] = normals;
        }

        if (arrays[(int)Mesh.ArrayType.Tangent].VariantType == Variant.Type.PackedFloat32Array)
        {
            var tangents = arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
            for (int t = 0; t + 3 < tangents.Length; t += 4)
            {
                var axis = (at.Basis * new Vector3(tangents[t], tangents[t + 1], tangents[t + 2])).Normalized();
                (tangents[t], tangents[t + 1], tangents[t + 2]) = (axis.X, axis.Y, axis.Z);
                // The bitangent sign flips with the handedness.
                tangents[t + 3] = mirrored ? -tangents[t + 3] : tangents[t + 3];
            }

            arrays[(int)Mesh.ArrayType.Tangent] = tangents;
        }
    }

    private static ArrayMesh Build(HdSurface[] surfaces)
    {
        var result = new ArrayMesh();
        for (int i = 0; i < surfaces.Length; i++)
        {
            result.AddSurfaceFromArrays(surfaces[i].Primitive, surfaces[i].Arrays);
            result.SurfaceSetName(i, surfaces[i].Name);
            result.SurfaceSetMaterial(i, surfaces[i].Material);
        }

        return result;
    }

    private static ImageTexture? Mipmapped(Texture2D source)
    {
        // ⚠ A copy: headless hands back the texture's own Image.
        if (source.GetImage() is not { } image || image.IsEmpty())
        {
            return null;
        }

        var copy = (Image)image.Duplicate();
        if (copy.IsCompressed())
        {
            copy.Decompress();
        }

        if (!copy.HasMipmaps())
        {
            copy.GenerateMipmaps();
        }

        return TextureUpload.Create(copy);
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

    // A glTF node's name as a gamez key. Blender's repeat suffix (`wing.001`) is dropped; no gamez
    // name ends in a dot and digits.
    private static string KeyOf(GltfNode node) => HdModels.NodeKey(BlenderRepeat.Replace(NameOf(node), string.Empty));

    // The first glTF node named for the gamez node `nodeKey`, an exact name before a suffixed one, or -1.
    private static int MatchedNode(Godot.Collections.Array<GltfNode> nodes, string nodeKey)
    {
        int suffixed = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (HdModels.NodeKey(NameOf(nodes[i])) == nodeKey)
            {
                return i;
            }

            if (suffixed < 0 && KeyOf(nodes[i]) == nodeKey)
            {
                suffixed = i;
            }
        }

        return suffixed;
    }

    // The glTF node for the gamez node `nodeKey` under a scope, or -1. Its ancestors following most of
    // `path` (the gamez names between scope and node) win, then an exact name, then file order.
    private static int ScopedNode(Godot.Collections.Array<GltfNode> nodes, string nodeKey, string[] path)
    {
        int best = -1, bestScore = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            bool exact = HdModels.NodeKey(NameOf(nodes[i])) == nodeKey;
            if (!exact && KeyOf(nodes[i]) != nodeKey)
            {
                continue;
            }

            // Walks up the file, matching the path from its nearest name, so a flat file scores 0.
            int score = 0, step = 0;
            for (int at = nodes[i].Parent, want = path.Length - 1; at >= 0 && at < nodes.Count && want >= 0 && step++ < nodes.Count; at = nodes[at].Parent)
            {
                if (KeyOf(nodes[at]) == path[want])
                {
                    want--;
                    score++;
                }
            }

            score = (score * 2) + (exact ? 1 : 0);
            if (score > bestScore)
            {
                (best, bestScore) = (i, score);
            }
        }

        return best;
    }

    // Every mesh at or below `index`, placed by `at`, leaving out subtrees named in `owned`.
    private static void Collect(Godot.Collections.Array<GltfNode> nodes, Godot.Collections.Array<GltfMesh> meshes, int index,
        Transform3D at, ISet<string> owned, List<HdSurface> into, int depth = 0)
    {
        // ⚠ Keep the depth stop; a file whose children loop would otherwise overflow the stack.
        if (depth > nodes.Count)
        {
            return;
        }

        var node = nodes[index];
        if (node.Mesh >= 0 && node.Mesh < meshes.Count && meshes[node.Mesh].Mesh is { } mesh)
        {
            into.AddRange(ReversedSurfaces(mesh, at));
        }

        foreach (int child in node.Children)
        {
            if (child >= 0 && child < nodes.Count && !owned.Contains(KeyOf(nodes[child])))
            {
                Collect(nodes, meshes, child, at * nodes[child].Xform, owned, into, depth + 1);
            }
        }
    }

    // Read straight off the parsed state, not a generated scene. No node is built to be freed, and
    // the node names are the file's own.
    private static HdSurface[]? Import(HdModel model, string nodeKey, string[]? path, string file, ISet<string> owned)
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

        var nodes = state.GetNodes();
        var meshes = state.GetMeshes();
        var surfaces = new List<HdSurface>();
        int matched = path == null ? MatchedNode(nodes, nodeKey) : ScopedNode(nodes, nodeKey, path);
        if (matched >= 0)
        {
            // ⚠ Never apply the matched node's own transform. It is the gamez node's local
            // transform as exported, and the game places the node by it already.
            Collect(nodes, meshes, matched, Transform3D.Identity, owned, surfaces);
            return surfaces.ToArray();
        }

        // A scope file that does not name the node leaves it to its own key.
        if (path != null)
        {
            return null;
        }

        foreach (int root in state.RootNodes)
        {
            if (root >= 0 && root < nodes.Count && !owned.Contains(KeyOf(nodes[root])))
            {
                Collect(nodes, meshes, root, nodes[root].Xform, owned, surfaces);
            }
        }

        if (surfaces.Count == 0)
        {
            Log.Warn("world", $"HD model import failed (no mesh for node {nodeKey} among {meshes.Count}): {file}");
            return null;
        }

        return surfaces.ToArray();
    }

    private ArrayMesh? Cached(string key, string nodeKey, string[]? path, IEnumerable<string>? owned)
    {
        var ownedKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in owned ?? Array.Empty<string>())
        {
            ownedKeys.Add(HdModels.NodeKey(name));
        }

        string cacheKey = (path == null ? key : key + "\n@" + string.Join("/", path.Append(nodeKey)))
            + (ownedKeys.Count == 0 ? string.Empty : "\n" + string.Join("\n", ownedKeys));

        // ⚠ Do not import outside the lock; two builders asking at once would each import and
        // hand out two meshes for one key.
        lock (_byKey)
        {
            if (!_byKey.TryGetValue(cacheKey, out var mesh))
            {
                var surfaces = Load(key, nodeKey, path, ownedKeys);
                mesh = surfaces != null ? Build(surfaces) : null;
                if (mesh != null)
                {
                    _surfaces[mesh] = surfaces!;
                }

                _byKey[cacheKey] = mesh;
            }

            return mesh;
        }
    }

    // ⚠ Never throws: a broken pack file costs its own replacement, not the build that asked.
    private HdSurface[]? Load(string key, string nodeKey, string[]? path, ISet<string> owned)
    {
        if (_models.Read(key) is not { } model)
        {
            Log.Warn("world", $"HD model import failed (unreadable): {key}");
            return null;
        }

        string file = model.GltfPath ?? key + (model.IsGlb ? ".glb" : ".gltf");
        try
        {
            return Import(model, nodeKey, path, file, owned);
        }
        catch (Exception e)
        {
            Log.Warn("world", $"HD model import failed ({e.GetType().Name}: {e.Message}): {file}");
            return null;
        }
    }
}

/// <summary>One imported surface as <see cref="HdModelMeshes"/> built it: winding already reversed,
/// and the material as the glTF importer made it.</summary>
internal sealed record HdSurface(Mesh.PrimitiveType Primitive, Godot.Collections.Array Arrays, string Name, Material? Material);
