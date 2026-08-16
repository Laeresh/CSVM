using System.Collections.Generic;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Exports the viewer plane's <c>Node3D</c> subtree to a glTF file — mesh + the currently
/// painted livery texture, with the current damage state baked in. Static geometry only: no
/// animation, and the live particle emitters (smoke/fire/trails) are not part of the tree it reads.
///
/// <para>Two triggers share <see cref="Export"/>: the <c>--export-gltf=</c> CLI one-shot, driven by
/// the frame-stepped <see cref="Tick"/> state machine (waits for the plane to build, writes, quits),
/// and the interactive F10 key. The work happens on a throwaway <c>Duplicate()</c> so the live scene
/// is never mutated — the golden screenshots must be identical after an export.</para></summary>
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
            GD.PrintErr("gltf export failed: no plane or no path");
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
    // mesh's shader skins to a `StandardMaterial3D` glTF can serialize.
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

    // Replace each surface's custom `ShaderMaterial` skin with a
    // `StandardMaterial3D` that glTF understands: the painted `albedo_tex` as the albedo
    // map, the shader's `albedo_color` tint when present, and vertex color as albedo so the
    // baked per-vertex shading survives into glTF's `COLOR_0`. `StandardMaterial3D`
    // skins (flares, magenta-missing fallbacks) already serialize and pass through untouched.
    private static void ConvertMaterials(MeshInstance3D mesh)
    {
        int surfaces = mesh.GetSurfaceOverrideMaterialCount();
        for (int i = 0; i < surfaces; i++)
        {
            if (mesh.GetActiveMaterial(i) is not ShaderMaterial shader)
            {
                continue;
            }
            var std = new StandardMaterial3D
            {
                AlbedoTexture = shader.GetShaderParameter("albedo_tex").As<Texture2D>(),
                VertexColorUseAsAlbedo = true,
            };
            var tint = shader.GetShaderParameter("albedo_color");
            if (tint.VariantType == Variant.Type.Color)
            {
                std.AlbedoColor = tint.As<Color>();
            }
            mesh.SetSurfaceOverrideMaterial(i, std);
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
}
