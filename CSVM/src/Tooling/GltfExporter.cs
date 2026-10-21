using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Tooling;

/// <summary>Exports a <c>Node3D</c> subtree to a glTF file. Static geometry only: no animation,
/// and live particle emitters (smoke/fire/trails) are not part of the tree it reads.
///
/// The viewer plane goes through the <c>--export-gltf=</c> CLI one-shot and F10; NodeLab can
/// export its current selection, or its export set as one file. The work happens on a throwaway <see cref="SceneCopy"/>, so the live
/// scene is never mutated. A file node takes the original gamez name, not Godot's
/// sanitised one, so a model pack built from an export matches node for node.</summary>
public sealed class GltfExporter
{
    // "glTF" read as a little-endian word, and the binary header's size (magic, version, length).
    private const uint GlbMagic = 0x46546C67;
    private const int GlbHeader = 12;

    // The export still owed from the launch spec, cleared once written; null when no --export-gltf=.
    private string? _pendingPath;

    public GltfExporter(SessionSpec spec)
    {
        _pendingPath = spec.ExportGltfPath;
    }

    /// <summary>Write the plane subtree to <paramref name="path"/> as glTF, format by extension:
    /// <c>.glb</c> binary, <c>.gltf</c> JSON with external buffers, anything else <c>.glb</c>. Reads the live scene through a duplicate, so the
    /// on-screen plane is untouched. With <paramref name="allStates"/> a hidden gamez node is kept
    /// and marked hidden, so a destructible's damage states reach the file, and the top gamez nodes
    /// are shown. Returns the write <see cref="Error"/>.</summary>
    public static Error Export(Node3D? plane, string path, bool allStates = false)
    {
        if (plane == null || string.IsNullOrEmpty(path))
        {
            Log.Error("core", $"gltf export failed: no node or no path");
            return Error.InvalidParameter;
        }
        // Copied so material overrides and node pruning below never touch the live tree. The copy
        // shares the ArrayMesh resources, so the winding fix builds a new mesh rather than editing
        // one, and skins are overridden on the NODE.
        var copy = SceneCopy.Of(plane);
        if (allStates)
        {
            ShowSubjects(copy);
        }
        return Write(copy, path, allStates);
    }

    /// <summary>Writes several live subtrees into one glTF under a single root, each at its world
    /// transform, so neighbouring terrain tiles line up in the file as they do in the world. A node
    /// with an ancestor also in the list is dropped, since the ancestor already carries it.</summary>
    public static Error ExportSet(IReadOnlyList<Node3D> nodes, string path)
    {
        var root = new Node3D { Name = "crimsonskies_set" };
        int kept = 0;
        foreach (var node in nodes)
        {
            if (!GodotObject.IsInstanceValid(node) || HasAncestorIn(node, nodes))
            {
                continue;
            }
            var copy = SceneCopy.Of(node);
            copy.Transform = node.GlobalTransform;
            root.AddChild(copy);
            kept++;
        }
        if (kept == 0)
        {
            root.Free();
            return Export(null, "");
        }
        return Write(root, path);
    }

    /// <summary><see cref="ExportSet"/> to a timestamped GLB in <c>Exports/</c>, as
    /// <see cref="ExportToExports"/> does for one node.</summary>
    public static Error ExportSetToExports(IReadOnlyList<Node3D> nodes, string setName) =>
        ExportSet(nodes, ExportsPath(setName));

    /// <summary>Exports <paramref name="node"/> to a timestamped GLB in the git-ignored
    /// <c>Exports/</c> directory beside the Godot project. <paramref name="nodeName"/> becomes a
    /// filesystem-safe portion of the filename.</summary>
    public static Error ExportToExports(Node3D? node, string nodeName)
    {
        if (node == null)
        {
            return Export(null, "");
        }
        return Export(node, ExportsPath(nodeName));
    }

    /// <summary>The CLI one-shot, ticked from the tail of <c>_Process</c>: wait for the session to
    /// land a plane, export it once, then quit with the write's success as the exit code (so a
    /// scripted run fails loudly). No-op once fired or when no <c>--export-gltf=</c> was given.</summary>
    public void Tick(Node3D? plane, SceneTree tree, SessionSpec spec)
    {
        if (_pendingPath == null || plane == null)
        {
            return;
        }
        // A --node= subject's own reset state may switch it off (a flyout template waits for its
        // launch). Its damage states wait hidden for their hits.
        var err = Export(plane, _pendingPath, allStates: spec.NodeName != null);
        _pendingPath = null;
        tree.Quit(err == Error.Ok ? 0 : 1);
    }

    private static string ExportsPath(string name)
    {
        string projectDir = ProjectSettings.GlobalizePath("res://");
        string directory = Path.GetFullPath(Path.Combine(projectDir, "..", "Exports"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,
            $"crimsonskies_{SafeFileName(name)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.glb");
    }

    private static bool HasAncestorIn(Node3D node, IReadOnlyList<Node3D> nodes)
    {
        foreach (var other in nodes)
        {
            if (!ReferenceEquals(other, node) && GodotObject.IsInstanceValid(other) && other.IsAncestorOf(node))
            {
                return true;
            }
        }
        return false;
    }

    // Bakes and writes a detached copy, then frees it. The copy is never in the scene tree.
    private static Error Write(Node3D copy, string path, bool allStates = false)
    {
        BakeAndConvert(copy, allStates);
        path = ResolvePath(NormalizeExtension(path));
        // Optional, so an importer without KHR_node_visibility still reads the file, all states shown.
        var doc = new GltfDocument { VisibilityMode = GltfDocument.VisibilityModeEnum.IncludeOptional };
        var state = new GltfState();
        var err = doc.AppendFromScene(copy, state);
        if (err == Error.Ok)
        {
            RestoreGamezNames(copy, state, state.GetNodes());
            err = doc.WriteToFilesystem(state, path);
        }
        if (err == Error.Ok)
        {
            err = RestoreMaterialNames(path, state.GetMaterials());
        }
        copy.QueueFree();
        if (err == Error.Ok)
        {
            Log.Info("core", $"gltf exported: {path}");
        }
        else
        {
            Log.Error("core", $"gltf export failed ({err}): {path}");
        }
        return err;
    }

    // Names each glTF node after the original gamez name its scene node carries in NameMeta. The
    // writer gives every node a sanitised name unique in the file and exports no metadata but
    // "extras". It does serialise a GltfNode's resource name verbatim, so a '.' or a repeated name
    // survives when set here. A node with no NameMeta (a builder's "mesh" child) keeps the writer's.
    private static void RestoreGamezNames(Node node, GltfState state, Godot.Collections.Array<GltfNode> nodes)
    {
        if (node.HasMeta(Mech3.AnimRuntime.NameMeta)
            && node.GetMeta(Mech3.AnimRuntime.NameMeta).AsString() is { Length: > 0 } original
            && state.GetNodeIndex(node) is var index && index >= 0 && index < nodes.Count)
        {
            nodes[index].ResourceName = original;
        }
        foreach (var child in node.GetChildren())
        {
            RestoreGamezNames(child, state, nodes);
        }
    }

    // Rewrites each material's name in the written file as the scene named it. The writer passes a
    // material name through its node-name rules: a '.' turns '_', and a name already taken gets a
    // number appended. Neither then names its texture.
    private static Error RestoreMaterialNames(string path, Godot.Collections.Array<Material> materials)
    {
        string file = ProjectSettings.GlobalizePath(path);
        try
        {
            byte[] bytes = File.ReadAllBytes(file);
            bool glb = bytes.Length >= GlbHeader + 8 && BitConverter.ToUInt32(bytes, 0) == GlbMagic;
            int jsonLength = glb ? BitConverter.ToInt32(bytes, GlbHeader) : bytes.Length;
            int jsonStart = glb ? GlbHeader + 8 : 0;
            var json = JsonNode.Parse(bytes.AsSpan(jsonStart, jsonLength))!;
            if (json["materials"] is not JsonArray written)
            {
                return Error.Ok;
            }
            for (int i = 0; i < written.Count && i < materials.Count; i++)
            {
                if (written[i] is JsonObject entry && materials[i]?.ResourceName is { Length: > 0 } name)
                {
                    entry["name"] = name;
                }
            }
            byte[] text = Encoding.UTF8.GetBytes(json.ToJsonString());
            if (!glb)
            {
                File.WriteAllBytes(file, text);
                return Error.Ok;
            }
            // A GLB chunk is 4-byte aligned, and a JSON chunk pads with spaces.
            int padded = (text.Length + 3) & ~3;
            byte[] rest = bytes[(jsonStart + jsonLength)..];
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(GlbMagic);
            writer.Write(BitConverter.ToUInt32(bytes, 4));
            writer.Write(GlbHeader + 8 + padded + rest.Length);
            writer.Write(padded);
            writer.Write(BitConverter.ToUInt32(bytes, GlbHeader + 4));
            writer.Write(text);
            writer.Write(Enumerable.Repeat((byte)' ', padded - text.Length).ToArray());
            writer.Write(rest);
            writer.Flush();
            File.WriteAllBytes(file, output.ToArray());
            return Error.Ok;
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        {
            Log.Error("core", $"gltf export failed to name its materials ({e.GetType().Name}: {e.Message}): {file}");
            return Error.FileCorrupt;
        }
    }

    // Bakes the current damage and flare state, which panel twins and wing flares hold in `Visible`:
    // frees every hidden `Node3D` and every point-sprite `"lights"` instance. Each
    // surviving mesh's shader skins become a `StandardMaterial3D` glTF can serialize. With allStates
    // a hidden gamez node stays, written hidden.
    private static void BakeAndConvert(Node copy, bool allStates)
    {
        var toFree = new List<Node>();
        Collect(copy, toFree, allStates);
        foreach (var n in toFree)
        {
            n.Free();
        }
    }

    // Shows each gamez node with no gamez ancestor, the subject the export was asked for.
    private static void ShowSubjects(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D shown && child.HasMeta(Mech3.AnimRuntime.NameMeta))
            {
                shown.Visible = true;
            }
            else
            {
                ShowSubjects(child);
            }
        }
    }

    private static void Collect(Node node, List<Node> toFree, bool allStates)
    {
        if (node is Node3D { Visible: false } && !(allStates && node.HasMeta(Mech3.AnimRuntime.NameMeta)))
        {
            toFree.Add(node);
            return; // its subtree goes with it, don't recurse into a pruned branch
        }
        if (node is MeshInstance3D mesh)
        {
            // Drop the point-sprite lights and any surface-less mesh (the viewer's hidden mesh-lab
            // overlays are visible MeshInstance3Ds with an empty mesh, glTF errors on those).
            if (mesh.Name.ToString() == "lights" || mesh.Mesh == null || mesh.Mesh.GetSurfaceCount() == 0)
            {
                toFree.Add(mesh);
                return;
            }
            ConvertMesh(mesh);
        }
        foreach (var child in node.GetChildren())
        {
            Collect(child, toFree, allStates);
        }
    }

    /// <summary>Convert one mesh instance for export: every surface skinned with one of the
    /// world/plane <c>ShaderMaterial</c>s is re-emitted with reversed triangle winding and takes a
    /// <see cref="StandardMaterial3D"/> glTF can serialize. A surface skinned with a plain
    /// <see cref="BaseMaterial3D"/> was built to Godot's own convention and is left alone.
    /// ⚠ Never trade the reversal for two-sided materials: glTF has no winding switch, and
    /// disabling culling hides an inside-out export behind z-fighting (docs/formats/gotchas.md).</summary>
    private static void ConvertMesh(MeshInstance3D instance)
    {
        var source = instance.Mesh!;
        int surfaces = source.GetSurfaceCount();
        var skins = new ShaderMaterial?[surfaces];
        var reversed = new bool[surfaces];
        for (int i = 0; i < surfaces; i++)
        {
            skins[i] = instance.GetActiveMaterial(i) as ShaderMaterial;
            reversed[i] = skins[i] != null;
        }
        if (Array.IndexOf(reversed, true) >= 0)
        {
            instance.Mesh = Reversed(source, reversed);
        }
        for (int i = 0; i < surfaces; i++)
        {
            if (skins[i] is not { } shader)
            {
                continue;
            }
            // Two-sided source polygons keep their disabled culling; so does a surface whose
            // winding could not be reversed, where two-sided is still the lesser wrong.
            bool twoSided = !reversed[i] || (shader.Shader?.Code ?? string.Empty).Contains("cull_disabled");
            var std = new StandardMaterial3D
            {
                // The original texture without its extension, the name a model pack maps back by.
                ResourceName = Path.GetFileNameWithoutExtension(shader.ResourceName),
                AlbedoTexture = shader.GetShaderParameter("albedo_tex").As<Texture2D>(),
                VertexColorUseAsAlbedo = true,
                CullMode = twoSided ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back,
            };
            var tint = shader.GetShaderParameter("albedo_color");
            if (tint.VariantType == Variant.Type.Color)
            {
                std.AlbedoColor = tint.As<Color>();
            }
            instance.SetSurfaceOverrideMaterial(i, std);
        }
    }

    // Re-emit `source` as a fresh mesh with the triangle winding of every surface flagged in
    // `reverse` flipped, carrying each surface's own material across. A surface that cannot be
    // flipped (not a triangle list, or unindexed) is emitted untouched and its flag cleared, so the
    // caller can fall back to a two-sided skin for it.
    private static ArrayMesh Reversed(Mesh source, bool[] reverse)
    {
        var result = new ArrayMesh();
        for (int i = 0; i < source.GetSurfaceCount(); i++)
        {
            var arrays = source.SurfaceGetArrays(i);
            var primitive = source is ArrayMesh mesh
                ? mesh.SurfaceGetPrimitiveType(i)
                : Mesh.PrimitiveType.Triangles;
            var indices = arrays[(int)Mesh.ArrayType.Index].As<int[]>();
            if (indices.Length == 0)
            {
                // An unindexed surface (what SurfaceTool commits unless the builder called
                // `Index()`, which nothing here does) winds in vertex order; the identity index
                // list gives the swap below something to act on, leaving the vertex data alone.
                indices = new int[arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>().Length];
                for (int v = 0; v < indices.Length; v++)
                {
                    indices[v] = v;
                }
            }
            if (reverse[i] && primitive == Mesh.PrimitiveType.Triangles && indices.Length >= 3)
            {
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    (indices[t], indices[t + 2]) = (indices[t + 2], indices[t]);
                }
                arrays[(int)Mesh.ArrayType.Index] = indices;
            }
            else
            {
                reverse[i] = false;
            }
            result.AddSurfaceFromArrays(primitive, arrays);
            result.SurfaceSetMaterial(i, source.SurfaceGetMaterial(i));
        }
        return result;
    }

    // Force the path to a glTF extension the writer recognizes: keep an explicit
    // `.glb`/`.gltf`, otherwise write a self-contained `.glb`.
    private static string NormalizeExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".glb" || ext == ".gltf")
        {
            return path;
        }
        return path + ".glb";
    }

    // Give the glTF writer a path it can open: Godot's `res://`/`user://` schemes
    // pass through, everything else becomes an absolute OS path (a bare relative path fails to open
    // for write).
    private static string ResolvePath(string path)
    {
        if (path.StartsWith("res://") || path.StartsWith("user://"))
        {
            return path;
        }
        return Path.GetFullPath(path);
    }

    private static string SafeFileName(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return "node";
        }
        var safe = new System.Text.StringBuilder(nodeName.Length);
        foreach (char character in nodeName)
        {
            safe.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '_' : character);
        }
        return safe.Length == 0 ? "node" : safe.ToString();
    }
}
