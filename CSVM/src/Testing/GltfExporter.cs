using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Exports a <c>Node3D</c> subtree to a glTF file. Static geometry only: no animation,
/// and live particle emitters (smoke/fire/trails) are not part of the tree it reads.
///
/// The viewer plane goes through the <c>--export-gltf=</c> CLI one-shot and F10; NodeLab can
/// export its current selection. The work happens on a throwaway <c>Duplicate()</c>, so the live
/// scene is never mutated.</summary>
public sealed class GltfExporter
{
    // The export still owed from the launch spec, cleared once written; null when no --export-gltf=.
    private string? _pendingPath;

    public GltfExporter(SessionSpec spec)
    {
        _pendingPath = spec.ExportGltfPath;
    }

    /// <summary>Write the plane subtree to <paramref name="path"/> as glTF, format chosen by the
    /// extension (<c>.glb</c> self-contained binary, <c>.gltf</c> JSON + external buffers/images;
    /// anything else defaults to <c>.glb</c>). Reads the live scene through a duplicate, so the
    /// on-screen plane is untouched. Returns the write <see cref="Error"/>.</summary>
    public static Error Export(Node3D? plane, string path)
    {
        if (plane == null || string.IsNullOrEmpty(path))
        {
            GD.PrintErr("gltf export failed: no node or no path");
            return Error.InvalidParameter;
        }
        // Duplicate so material overrides and node pruning below never touch the live tree. The
        // ArrayMesh resources are shared, which is fine — surfaces are overridden on the NODE.
        var copy = (Node3D)plane.Duplicate();
        BakeAndConvert(copy);
        path = ResolvePath(NormalizeExtension(path));
        var doc = new GltfDocument();
        var state = new GltfState();
        var err = doc.AppendFromScene(copy, state);
        if (err == Error.Ok)
        {
            err = doc.WriteToFilesystem(state, path);
        }
        copy.QueueFree();
        if (err == Error.Ok)
        {
            Log.Info("core", $"gltf exported: {path}");
        }
        else
        {
            GD.PrintErr($"gltf export failed ({err}): {path}");
        }
        return err;
    }

    /// <summary>Exports <paramref name="node"/> to a timestamped GLB in the git-ignored
    /// <c>Exports/</c> directory beside the Godot project. <paramref name="nodeName"/> becomes a
    /// filesystem-safe portion of the filename.</summary>
    public static Error ExportToExports(Node3D? node, string nodeName)
    {
        if (node == null)
        {
            return Export(null, "");
        }
        string projectDir = ProjectSettings.GlobalizePath("res://");
        string directory = Path.GetFullPath(Path.Combine(projectDir, "..", "Exports"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory,
            $"crimsonskies_{SafeFileName(nodeName)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.glb");
        return Export(node, path);
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
        var err = Export(plane, _pendingPath);
        _pendingPath = null;
        tree.Quit(err == Error.Ok ? 0 : 1);
    }

    // Bake the current damage/flare state and drop non-geometry: free every hidden
    // `Node3D` (torn/healthy panel twins and off wing-flares are toggled purely by
    // `Visible`) and every point-sprite `"lights"` instance, then convert each surviving
    // mesh's shader skins to a double-sided `StandardMaterial3D` glTF can serialize.
    private static void BakeAndConvert(Node copy)
    {
        var toFree = new List<Node>();
        Collect(copy, toFree);
        foreach (var n in toFree)
        {
            n.Free();
        }
    }

    private static void Collect(Node node, List<Node> toFree)
    {
        if (node is Node3D { Visible: false })
        {
            toFree.Add(node);
            return; // its subtree goes with it — don't recurse into a pruned branch
        }
        if (node is MeshInstance3D mesh)
        {
            // Drop the point-sprite lights and any surface-less mesh (the viewer's hidden mesh-lab
            // overlays are visible MeshInstance3Ds with an empty mesh — glTF errors on those).
            if (mesh.Name.ToString() == "lights" || mesh.Mesh == null || mesh.Mesh.GetSurfaceCount() == 0)
            {
                toFree.Add(mesh);
                return;
            }
            ConvertMaterials(mesh);
        }
        foreach (var child in node.GetChildren())
        {
            Collect(child, toFree);
        }
    }

    // Replace each surface's custom `ShaderMaterial` skin with a double-sided
    // `StandardMaterial3D` glTF understands. Duplicate an existing base material before changing
    // its culling, because resources are shared with the live tree.
    private static void ConvertMaterials(MeshInstance3D mesh)
    {
        int surfaces = mesh.GetSurfaceOverrideMaterialCount();
        for (int i = 0; i < surfaces; i++)
        {
            if (mesh.GetActiveMaterial(i) is ShaderMaterial shader)
            {
                var std = new StandardMaterial3D
                {
                    AlbedoTexture = shader.GetShaderParameter("albedo_tex").As<Texture2D>(),
                    VertexColorUseAsAlbedo = true,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                };
                var tint = shader.GetShaderParameter("albedo_color");
                if (tint.VariantType == Variant.Type.Color)
                {
                    std.AlbedoColor = tint.As<Color>();
                }
                mesh.SetSurfaceOverrideMaterial(i, std);
                continue;
            }
            if (mesh.GetActiveMaterial(i) is not BaseMaterial3D source)
            {
                continue;
            }
            var copy = (BaseMaterial3D)source.Duplicate();
            copy.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mesh.SetSurfaceOverrideMaterial(i, copy);
        }
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
