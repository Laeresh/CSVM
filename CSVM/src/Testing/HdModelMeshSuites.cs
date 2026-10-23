using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Tooling;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// That a replacement model exported by <see cref="GltfExporter"/> imports through
/// <see cref="HdModelMeshes"/> exactly as it was built. The data's visible side is Godot's back
/// face, so the exporter reverses every surface and the import must reverse it back. An import
/// that skipped the reversal would draw every replaced model inside out.
/// </summary>
internal static class HdModelMeshSuites
{
    private const string ShaderCode =
        "shader_type spatial;\nuniform sampler2D albedo_tex;\nuniform vec4 albedo_color = vec4(1.0);\n";

    [Suite("hd-model-mesh-import",
        "a mesh written by the glTF exporter into a model pack comes back through HdModelMeshes with "
        + "the source's vertex positions and triangle winding on every surface, from a .glb and a "
        + "loose .gltf, picked out by node name from a file holding a second mesh, also when Blender "
        + "has suffixed that name .001, and imported once per pack key. Every mesh below the matched node "
        + "merges, placed by the transforms below it (a turned and a mirrored child), leaving out a subtree "
        + "named for a gamez descendant; a file with no node of the name merges whole from its roots, and "
        + "a matched node with no mesh below it is an empty mesh")]
    internal static void ImportRoundTrip(TestContext ctx)
    {
        string root = Path.Combine(ctx.ScratchDir, "hd-model-mesh-import");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string pack = Path.Combine(root, "HDModels", "pack");
        Directory.CreateDirectory(Path.Combine(pack, "c3"));
        Directory.CreateDirectory(Path.Combine(pack, "c5"));
        var source = ProbeMesh();
        var scene = PackScene(source);
        try
        {
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(scene, Path.Combine(pack, "probe.glb")), $"glb write result");
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(scene, Path.Combine(pack, "c3", "probe.gltf")), $"gltf write result");
            // The probe as Blender re-exports a repeated object: renamed with a numeric suffix.
            scene.GetNode("probe").SetMeta(AnimRuntime.NameMeta, "probe.001");
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(scene, Path.Combine(pack, "c5", "probe.glb")), $"suffixed glb write result");
        }
        finally
        {
            scene.Free();
        }

        var saved = HdModels.Current;
        using var models = HdModels.Open(root, "HDModels");
        try
        {
            var meshes = new HdModelMeshes(models);
            var glb = meshes.For("PROBE.flt");
            SameGeometry(ctx, source, glb, "glb");
            ctx.Check(glb != null && ReferenceEquals(glb, meshes.For("probe")),
                $"a second request for the key returns the cached mesh, not a second import");
            ctx.Check(meshes.For("decoy") == null, $"a node with no pack key gets no mesh");

            var gltf = meshes.For("probe", chapter: "C3");
            SameGeometry(ctx, source, gltf, "loose gltf");
            ctx.Check(gltf != null && !ReferenceEquals(gltf, glb), $"the chapter key imports its own file");
            SameGeometry(ctx, source, meshes.For("probe", chapter: "C5"), "a node named probe.001 beside a decoy");

            HdModels.Current = models;
            var shared = HdModelMeshes.Current.For("probe");
            ctx.Check(shared != null && ReferenceEquals(shared, HdModelMeshes.Current.For("probe")),
                $"the shared meshes over HdModels.Current cache across requests");
        }
        finally
        {
            HdModels.Current = saved;
        }

        var unindexed = HdModelMeshes.Reversed(Unindexed());
        var expected = new List<Vector3[]> { new[] { V(2, 0, 0), V(0, 1, 0), V(0, 0, 0) } };
        ctx.Check(SameTriangles(expected, Triangles(unindexed, 0)),
            $"an unindexed surface is reversed through an identity index list");
        CheckMerge(ctx, root, source);
    }

    // A file of several meshes under a node hierarchy. The matched node's subtree merges, placed by
    // the transforms below it. A file with no matching node merges whole from its roots.
    private static void CheckMerge(TestContext ctx, string root, ArrayMesh probe)
    {
        string pack = Path.Combine(root, "Merged", "pack");
        var part = DecoyMesh();
        var scene = AssemblyScene(probe, part, out var assembly, out var turned, out var mirrored);
        try
        {
            foreach (string file in new[] { "assembly.glb", "c7/unnamed.glb", "c8/empty_part.glb" })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(pack, file))!);
                ctx.Same((long)Error.Ok, (long)GltfExporter.Export(scene, Path.Combine(pack, file)), $"{file} write result");
            }
        }
        finally
        {
            scene.Free();
        }

        using var models = HdModels.Open(root, "Merged");
        var meshes = new HdModelMeshes(models);
        var merged = meshes.For("assembly", owned: new[] { "hinge" });
        ctx.Check(merged?.GetSurfaceCount() == 4, $"the matched node's own mesh (2 surfaces) and two children's merge, the gamez-owned child left out (got {merged?.GetSurfaceCount()})");
        if (merged?.GetSurfaceCount() == 4)
        {
            ctx.Check(SameTriangles(Triangles(probe, 0), Triangles(merged, 0)) && SameTriangles(Triangles(probe, 1), Triangles(merged, 1)),
                $"the matched node's own transform is not applied, so its mesh is exact");
            ctx.Check(Near(Placed(part, turned, mirrored: false), Triangles(merged, 2)),
                $"a rotated child's mesh is placed by its transform, winding kept");
            ctx.Check(Near(Placed(part, mirrored, mirrored: true), Triangles(merged, 3)),
                $"a mirrored child's mesh is placed by its transform, its visible side kept");
        }

        ctx.Same(5, meshes.For("assembly")?.GetSurfaceCount() ?? -1, $"without gamez descendants every child merges");
        var whole = meshes.For("unnamed", chapter: "C7");
        ctx.Check(whole?.GetSurfaceCount() == 5 && Near(Placed(probe, assembly, mirrored: false, surface: 0), Triangles(whole, 0)),
            $"a file with no node of the name merges every mesh, placed from the scene roots");
        var empty = meshes.For("empty_part", chapter: "C8");
        ctx.Check(empty != null && empty.GetSurfaceCount() == 0, $"a matched node with nothing below it is an empty mesh, not a failure");
    }

    // A part of several meshes: its own on the exporter's "mesh" child, then a turned and a mirrored
    // part. A child named for a gamez descendant comes last, and an empty node sits beside it.
    private static Node3D AssemblyScene(ArrayMesh probe, ArrayMesh part, out Transform3D assembly, out Transform3D turned, out Transform3D mirrored)
    {
        var root = new Node3D { Name = "assembly_scene" };
        var node = new Node3D { Name = "assembly", Position = V(7, 0, 0) };
        node.AddChild(new MeshInstance3D { Name = "mesh", Mesh = probe });
        var arm = new Node3D { Name = "arm", Position = V(0, 1, 0), Rotation = V(0, Mathf.Pi / 2, 0) };
        arm.AddChild(new MeshInstance3D { Name = "mesh", Mesh = part });
        var mirror = new Node3D { Name = "mirror", Position = V(0, 0, 2), Scale = V(-1, 1, 1) };
        mirror.AddChild(new MeshInstance3D { Name = "mesh", Mesh = part });
        var hinge = new Node3D { Name = "hinge" };
        hinge.AddChild(new MeshInstance3D { Name = "mesh", Mesh = part });
        node.AddChild(arm);
        node.AddChild(mirror);
        node.AddChild(hinge);
        root.AddChild(node);
        root.AddChild(new Node3D { Name = "empty_part" });
        (assembly, turned, mirrored) = (node.Transform, arm.Transform, mirror.Transform);
        return root;
    }

    // A surface's triangles in the data's winding after `at`. A mirror turns the corner order over.
    private static List<Vector3[]> Placed(ArrayMesh mesh, Transform3D at, bool mirrored, int surface = 0)
    {
        var placed = new List<Vector3[]>();
        foreach (var corners in Triangles(mesh, surface))
        {
            var moved = new[] { at * corners[0], at * corners[1], at * corners[2] };
            placed.Add(mirrored ? new[] { moved[2], moved[1], moved[0] } : moved);
        }

        return placed;
    }

    // Each wanted triangle against the got one at any rotation of its corners, within float noise.
    private static bool Near(List<Vector3[]> want, List<Vector3[]> got)
    {
        if (want.Count != got.Count)
        {
            return false;
        }

        for (int t = 0; t < want.Count; t++)
        {
            bool any = false;
            for (int r = 0; r < 3 && !any; r++)
            {
                any = want[t][0].DistanceTo(got[t][r]) < 1e-4f && want[t][1].DistanceTo(got[t][(r + 1) % 3]) < 1e-4f
                    && want[t][2].DistanceTo(got[t][(r + 2) % 3]) < 1e-4f;
            }

            if (!any)
            {
                return false;
            }
        }

        return true;
    }

    private static void SameGeometry(TestContext ctx, ArrayMesh source, ArrayMesh? imported, string label)
    {
        ctx.Check(imported != null, $"{label}: the pack's mesh imports");
        if (imported == null)
        {
            return;
        }

        ctx.Same(source.GetSurfaceCount(), imported.GetSurfaceCount(), $"{label}: surface count");
        int surfaces = Mathf.Min(source.GetSurfaceCount(), imported.GetSurfaceCount());
        for (int i = 0; i < surfaces; i++)
        {
            var want = Triangles(source, i);
            var got = Triangles(imported, i);
            ctx.Check(SameTriangles(want, got),
                $"{label}: surface {i} has the source's {want.Count} triangles, positions and winding exact (got {got.Count})");
            ctx.Check(imported.SurfaceGetMaterial(i) != null, $"{label}: surface {i} keeps the file's material");
        }
    }

    // Each triangle's corners in winding order, rotated to start at its least corner. A rotation keeps
    // the winding, so two meshes agree when these lists are equal.
    private static List<Vector3[]> Triangles(ArrayMesh mesh, int surface)
    {
        var arrays = mesh.SurfaceGetArrays(surface);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>();
        var indices = arrays[(int)Mesh.ArrayType.Index].As<int[]>() ?? System.Array.Empty<int>();
        int count = indices.Length > 0 ? indices.Length : vertices.Length;
        var triangles = new List<Vector3[]>();
        for (int t = 0; t + 2 < count; t += 3)
        {
            var corners = new Vector3[3];
            for (int c = 0; c < 3; c++)
            {
                corners[c] = vertices[indices.Length > 0 ? indices[t + c] : t + c];
            }

            triangles.Add(Rotated(corners));
        }

        return triangles;
    }

    private static Vector3[] Rotated(Vector3[] corners)
    {
        int least = 0;
        for (int c = 1; c < 3; c++)
        {
            if (Less(corners[c], corners[least]))
            {
                least = c;
            }
        }

        return new[] { corners[least], corners[(least + 1) % 3], corners[(least + 2) % 3] };
    }

    private static bool Less(Vector3 a, Vector3 b) =>
        a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;

    private static bool SameTriangles(List<Vector3[]> want, List<Vector3[]> got)
    {
        if (want.Count != got.Count)
        {
            return false;
        }

        for (int t = 0; t < want.Count; t++)
        {
            var a = Rotated(want[t]);
            for (int c = 0; c < 3; c++)
            {
                if (a[c] != got[t][c])
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Two surfaces in the data's winding: an indexed quad that is not symmetric under a flip, and an
    // unindexed triangle. Each wears a shader material, which is what the exporter reverses.
    private static ArrayMesh ProbeMesh()
    {
        var mesh = new ArrayMesh();
        AddSurface(mesh, new[] { V(0, 0, 0), V(1.5f, 0, 0), V(1.25f, 2, 0.5f), V(-0.25f, 1, 0) }, new[] { 0, 1, 2, 0, 2, 3 });
        AddSurface(mesh, new[] { V(0, 0, 1), V(0.75f, 0, 1.5f), V(0, 3, 1.25f) }, null);
        return mesh;
    }

    private static ArrayMesh DecoyMesh()
    {
        var mesh = new ArrayMesh();
        AddSurface(mesh, new[] { V(5, 0, 0), V(6, 0, 0), V(5, 1, 0) }, new[] { 0, 1, 2 });
        return mesh;
    }

    private static ImporterMesh Unindexed()
    {
        var mesh = new ImporterMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[] { V(0, 0, 0), V(0, 1, 0), V(2, 0, 0) };
        mesh.AddSurface(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void AddSurface(ArrayMesh mesh, Vector3[] vertices, int[]? indices)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        var normals = new Vector3[vertices.Length];
        System.Array.Fill(normals, Vector3.Back);
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        if (indices != null)
        {
            arrays[(int)Mesh.ArrayType.Index] = indices;
        }

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, new ShaderMaterial { Shader = new Shader { Code = ShaderCode } });
    }

    // The tree SceneBuilder builds: a node per gamez node, its mesh on a child named "mesh". The decoy
    // gives the file a second mesh, so the probe has to be found by name.
    private static Node3D PackScene(ArrayMesh probe)
    {
        var root = new Node3D { Name = "pack_scene" };
        var node = new Node3D { Name = "probe", Position = V(3, 0, 0) };
        node.AddChild(new MeshInstance3D { Name = "mesh", Mesh = probe });
        var decoy = new Node3D { Name = "decoy" };
        decoy.AddChild(new MeshInstance3D { Name = "mesh", Mesh = DecoyMesh() });
        root.AddChild(decoy);
        root.AddChild(node);
        return root;
    }

    private static Vector3 V(float x, float y, float z) => new(x, y, z);
}
