using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Tooling;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// That a model pack replacement swaps only the mesh a built node draws. The world half builds a
/// small chapter world of its own, so it runs the same over an extraction and the synthetic tree.
/// The plane half builds the run's airframe, and the flyout half builds a weapon's flyout body on the
/// world's scene builder. Each replaced node is compared with the same node built without a pack.
/// <see cref="HdModels.Current"/> is restored after every build.
/// </summary>
internal static class HdModelSwapSuites
{
    private const string Chapter = "C1";
    private const string ReplacedWorld = "probe_shed_body";
    private const string Unrelated = "probe_tank";
    private const string Terrain = "probe_ground";
    private const string Facade = "probe_facade";
    private const string Glow = "probe_glow";
    private const string FlyoutBody = "probe_flyout_body";
    private const string FlyoutFin = "probe_flyout_fin";

    // The partition grid of the suite's world: four cells of 100 m a side.
    private const float Cell = 100f;
    private const int Cells = 4;

    // A box's six faces as indices into its eight corners, each wound as the data winds a face seen
    // from outside (the synthetic airframes' rule).
    private static readonly int[][] BoxFaces =
    {
        new[] { 0, 3, 2, 1 },
        new[] { 4, 5, 6, 7 },
        new[] { 0, 4, 7, 3 },
        new[] { 1, 2, 6, 5 },
        new[] { 3, 7, 6, 2 },
        new[] { 0, 1, 5, 4 },
    };

    [Suite("hd-model-swap",
        "a model pack replacement swaps the mesh SceneBuilder draws for a world node by its chapter key, "
        + "a plane node by its airframe key and a flyout body node by its bare name; each keeps its "
        + "name and index metas, transform, colliders and node_bias as built without the pack; a "
        + "terrain tile, a facade and a glow sprite named in the pack keep their gamez models, an "
        + "unrelated node is untouched, and a builder with no chapter or airframe replaces nothing")]
    internal static void SwapSuite(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(texturesPath, $"{Chapter} textures");

        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var weapon = weapons.All.FirstOrDefault(w => w.Flyout?.Model != null);
        ctx.Check(weapon != null, $"the weapon catalogue holds a weapon with a flyout model");
        if (weapon == null)
        {
            return;
        }

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        using var textures = new TextureArchive(texturesPath);
        string planeNode = PlaneMeshNode(planesGamez, textures, ctx.PlaneName);
        ctx.Check(planeNode.Length > 0, $"{ctx.PlaneName} carries a mesh node to replace");
        if (planeNode.Length == 0)
        {
            return;
        }

        string root = Path.Combine(ctx.ScratchDir, "hd-model-swap");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string pack = Path.Combine(root, "HDModels", "pack");
        foreach (string key in new[]
        {
            $"{Chapter}/{ReplacedWorld}", $"{Chapter}/{Terrain}", $"{Chapter}/{Facade}", $"{Chapter}/{Glow}",
            $"planes/{ctx.PlaneName}/{planeNode}", FlyoutBody,
        })
        {
            ctx.Same((long)Error.Ok, (long)WriteModel(Path.Combine(pack, key + ".glb"), key[(key.LastIndexOf('/') + 1)..]),
                $"{key}.glb write result");
        }

        using var none = HdModels.Open(root, "no-such-pack");
        using var models = HdModels.Open(root, "HDModels");
        CheckWorld(ctx, textures, weapon, none, models);
        CheckPlane(ctx, planesGamez, textures, planeNode, none, models);
    }

    private static void CheckWorld(TestContext ctx, TextureArchive textures, WeaponDef weapon, HdModels none, HdModels models)
    {
        var gamez = WorldGamez(weapon.Flyout!.Model!);
        var plain = BuildWorld(gamez, textures, weapon, none);
        var packed = BuildWorld(gamez, textures, weapon, models);
        var saved = HdModels.Current;
        HdModels.Current = models;
        try
        {
            ctx.Same(0, plain.WorldReplaced + plain.FlyoutReplaced, $"a build without a pack replaces nothing");
            ctx.Same(1, packed.WorldReplaced, $"the world build replaces one mesh instance");
            ctx.Same(1, packed.FlyoutReplaced, $"the flyout body build replaces one mesh instance");
            var replaced = Index(gamez, ReplacedWorld);
            SameNode(ctx, plain.World, packed.World, replaced, HdModelMeshes.Current.For(ReplacedWorld, Chapter), ReplacedWorld);
            foreach (string kept in new[] { Terrain, Facade, Glow })
            {
                ctx.Check(HdModelMeshes.Current.For(kept, Chapter) != null, $"the pack holds a replacement for {kept}");
                KeptNode(ctx, plain.World, packed.World, Index(gamez, kept), HdModelMeshes.Current.For(kept, Chapter), kept);
            }

            KeptNode(ctx, plain.World, packed.World, Index(gamez, Unrelated), null, Unrelated);
            SameNode(ctx, plain.Flyout, packed.Flyout, Index(gamez, FlyoutBody), HdModelMeshes.Current.For(FlyoutBody, Chapter), $"flyout {FlyoutBody}");
            KeptNode(ctx, plain.Flyout, packed.Flyout, Index(gamez, FlyoutFin), null, $"flyout {FlyoutFin}");
            ctx.Note($"replacement surface material as imported: {HdModelMeshes.Current.For(ReplacedWorld, Chapter)?.SurfaceGetMaterial(0)?.GetType().Name ?? "none"}");

            // A builder given neither a chapter nor an airframe opts out, as the cockpit interior does.
            var bare = new SceneBuilder(gamez, textures, fullbright: true, cullBackfaces: true);
            var shed = bare.BuildSubtree(gamez.Nodes[Index(gamez, ReplacedWorld)]);
            ctx.Check(shed != null && bare.ReplacedMeshCount == 0
                    && !ReferenceEquals(DrawnMesh(shed), HdModelMeshes.Current.For(ReplacedWorld, Chapter)),
                $"a builder with no chapter or airframe draws the gamez model");
            shed?.Free();
        }
        finally
        {
            HdModels.Current = saved;
            plain.Free();
            packed.Free();
        }
    }

    private static void CheckPlane(TestContext ctx, GameZ planesGamez, TextureArchive textures, string planeNode, HdModels none, HdModels models)
    {
        var plain = BuildPlane(planesGamez, textures, ctx.PlaneName, none);
        var packed = BuildPlane(planesGamez, textures, ctx.PlaneName, models);
        var saved = HdModels.Current;
        HdModels.Current = models;
        try
        {
            var mesh = HdModelMeshes.Current.For(planeNode, airframe: ctx.PlaneName);
            var indices = Nodes(packed).Where(n => AnimRuntime.NameOf(n) == planeNode && DrawnMesh(n) != null)
                .Select(n => n.GetMeta(AnimRuntime.IndexMeta).AsInt32()).ToList();
            ctx.Check(indices.Count > 0, $"{ctx.PlaneName} builds {planeNode} with a mesh");
            foreach (int index in indices)
            {
                SameNode(ctx, plain, packed, index, mesh, $"{ctx.PlaneName} {planeNode}");
            }

            var others = Nodes(packed).Where(n => DrawnMesh(n) != null && AnimRuntime.NameOf(n) != planeNode).ToList();
            ctx.Check(others.Count > 0 && others.All(n => !ReferenceEquals(DrawnMesh(n), mesh)),
                $"the {others.Count} other mesh nodes of {ctx.PlaneName} keep their gamez models");
        }
        finally
        {
            HdModels.Current = saved;
            plain.Free();
            packed.Free();
        }
    }

    // A replaced node: drawn from the pack, everything else as built without it.
    private static void SameNode(TestContext ctx, Node3D plain, Node3D packed, int index, ArrayMesh? expected, string label)
    {
        var a = ByIndex(plain, index);
        var b = ByIndex(packed, index);
        ctx.Check(a != null && b != null, $"{label}: built in both builds");
        if (a == null || b == null)
        {
            return;
        }

        ctx.Check(expected != null && ReferenceEquals(DrawnMesh(b), expected), $"{label}: draws the pack's mesh");
        ctx.Check(!ReferenceEquals(DrawnMesh(a), expected), $"{label}: draws the gamez model without the pack");
        ctx.Check(MeshChild(b)?.Position == Vector3.Zero, $"{label}: the replacement's instance sits at the node origin");
        SameRest(ctx, a, b, label);
    }

    // A node the pack names but must not replace, or one it does not name at all.
    private static void KeptNode(TestContext ctx, Node3D plain, Node3D packed, int index, ArrayMesh? offered, string label)
    {
        var a = ByIndex(plain, index);
        var b = ByIndex(packed, index);
        ctx.Check(a != null && b != null && DrawnMesh(b) != null, $"{label}: built with a mesh in both builds");
        if (a == null || b == null)
        {
            return;
        }

        var mesh = DrawnMesh(b);
        ctx.Check(mesh != null && (offered == null || !ReferenceEquals(mesh, offered))
                && mesh.GetSurfaceCount() == DrawnMesh(a)?.GetSurfaceCount() && mesh.GetAabb() == DrawnMesh(a)?.GetAabb(),
            $"{label}: keeps its gamez model");
        ctx.Check(MeshChild(a)?.Position == MeshChild(b)?.Position, $"{label}: keeps its mesh instance's position");
        SameRest(ctx, a, b, label);
    }

    private static void SameRest(TestContext ctx, Node3D a, Node3D b, string label)
    {
        ctx.Check(AnimRuntime.NameOf(a) == AnimRuntime.NameOf(b), $"{label}: the name meta is unchanged");
        ctx.Same(a.GetMeta(AnimRuntime.IndexMeta).AsInt32(), b.GetMeta(AnimRuntime.IndexMeta).AsInt32(), $"{label}: the index meta");
        ctx.Check(a.Transform.IsEqualApprox(b.Transform) && a.Transform == b.Transform, $"{label}: the transform is unchanged");
        ctx.Check(a.Visible == b.Visible, $"{label}: the visibility is unchanged");
        var ma = MeshChild(a);
        var mb = MeshChild(b);
        ctx.Check(ma != null && mb != null
                && ma.GetInstanceShaderParameter("node_bias").AsSingle() == mb.GetInstanceShaderParameter("node_bias").AsSingle()
                && ma.Layers == mb.Layers && ma.CastShadow == mb.CastShadow,
            $"{label}: node_bias, layer and shadow setting unchanged");
        var ca = Colliders(a);
        var cb = Colliders(b);
        ctx.Check(ca.SequenceEqual(cb), $"{label}: the {cb.Count} collider shapes are unchanged");
    }

    // Each collider body's name with one shape's face list, flattened in child order.
    private static List<string> Colliders(Node3D node)
    {
        var shapes = new List<string>();
        foreach (var body in node.GetChildren().OfType<StaticBody3D>())
        {
            foreach (var shape in body.GetChildren().OfType<CollisionShape3D>())
            {
                var faces = (shape.Shape as ConcavePolygonShape3D)?.GetFaces() ?? Array.Empty<Vector3>();
                shapes.Add($"{body.Name}:{string.Join(";", faces)}");
            }
        }

        return shapes;
    }

    private static Built BuildWorld(GameZ gamez, TextureArchive textures, WeaponDef weapon, HdModels pack)
    {
        var saved = HdModels.Current;
        HdModels.Current = pack;
        try
        {
            var builder = new WorldBuilder(gamez, textures, collision: true) { HdChapter = Chapter };
            var world = builder.Build("world1");
            world.AddChild(builder.Cycler);
            int replaced = builder.Scene.ReplacedMeshCount;
            var pool = new ProjectilePool(textures, null, null, flyoutGamez: gamez, flyoutScene: builder.Scene);
            var flyout = pool.BuildFlyoutBody(weapon) ?? new Node3D();
            pool.Free();
            return new Built(world, flyout, replaced, builder.Scene.ReplacedMeshCount - replaced);
        }
        finally
        {
            HdModels.Current = saved;
        }
    }

    private static Node3D BuildPlane(GameZ gamez, TextureArchive textures, string plane, HdModels pack)
    {
        var saved = HdModels.Current;
        HdModels.Current = pack;
        try
        {
            return new PlaneBuilder(gamez, textures, spinningProps: true).Build(plane);
        }
        finally
        {
            HdModels.Current = saved;
        }
    }

    // The first visible node of the airframe drawing a mesh, by its gamez name.
    private static string PlaneMeshNode(GameZ gamez, TextureArchive textures, string plane)
    {
        var saved = HdModels.Current;
        HdModels.Current = HdModels.Open(Path.GetTempPath(), "csvm-no-such-pack");
        try
        {
            var model = new PlaneBuilder(gamez, textures, spinningProps: true).Build(plane);
            string name = Nodes(model).Where(n => n.Visible && DrawnMesh(n) != null).Select(AnimRuntime.NameOf).FirstOrDefault() ?? string.Empty;
            model.Free();
            return name;
        }
        finally
        {
            HdModels.Current.Dispose();
            HdModels.Current = saved;
        }
    }

    // One tetrahedron under a node named for the key, the shape SceneBuilder builds and the exporter writes.
    private static Error WriteModel(string path, string node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[] { new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(0, 3, 0), new Vector3(0, 0, 1) };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 3, 1, 0, 2, 3, 1, 3, 2 };
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.3f, 0.2f) });
        var scene = new Node3D { Name = "pack_scene" };
        var holder = new Node3D { Name = node };
        holder.SetMeta(AnimRuntime.NameMeta, node);
        holder.AddChild(new MeshInstance3D { Name = "mesh", Mesh = mesh });
        scene.AddChild(holder);
        try
        {
            return GltfExporter.Export(scene, path);
        }
        finally
        {
            scene.Free();
        }
    }

    // The suite's chapter world under world1's grid: a shed, a tank, a cell-sized ground tile, a facade
    // and a glow sprite. The weapon's flyout root sits beside it. Every node carries a transform, so
    // none reads as parked at the origin.
    private static GameZ WorldGamez(string flyoutModel)
    {
        var gamez = new GameZ();
        gamez.Materials.Add(new GameZMaterial { Color = new Color(0.5f, 0.5f, 0.5f) });
        int shed = AddMesh(gamez, Box(new Vector3(-4, 0, -3), new Vector3(4, 5, 3)));
        int tank = AddMesh(gamez, Box(new Vector3(-2.5f, 0, -2.5f), new Vector3(2.5f, 7, 2.5f)));
        int ground = AddMesh(gamez, Quad(new Vector3(-50, 0, -50), new Vector3(50, 0, -50), new Vector3(50, 0, 50), new Vector3(-50, 0, 50)));
        int facade = AddMesh(gamez, Quad(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 4, 0), new Vector3(-1, 4, 0)));
        gamez.Meshes[facade].ModelType = "Facade";
        gamez.Meshes[facade].FacadeMode = "CylindricalY";
        int glow = AddMesh(gamez, Quad(new Vector3(-0.5f, 3, 0), new Vector3(0.5f, 3, 0), new Vector3(0.5f, 4, 0), new Vector3(-0.5f, 4, 0)));
        gamez.Meshes[glow].ModelType = "Facade";
        gamez.Meshes[glow].FacadeMode = "SphericalY";
        int body = AddMesh(gamez, Box(new Vector3(-0.3f, -0.3f, -1.6f), new Vector3(0.3f, 0.3f, 1.6f)));
        int fin = AddMesh(gamez, Box(new Vector3(-0.3f, -0.03f, -0.2f), new Vector3(0.3f, 0.03f, 0.2f)));

        var world = AddNode(gamez, "world1", -1, Vector3.Zero, kind: "World");
        world.Local = null;
        world.HasArea = true;
        world.AreaLeft = -Cell * Cells;
        world.AreaTop = -Cell * Cells;
        world.AreaRight = 0f;
        world.AreaBottom = 0f;
        world.PartitionCols = world.PartitionRows = Cells;
        var shedRoot = AddNode(gamez, "probe_shed", -1, new Vector3(-220, 0, -120));
        Child(world, shedRoot);
        Child(shedRoot, AddNode(gamez, ReplacedWorld, shed, new Vector3(1, 0, 2)));
        Child(world, AddNode(gamez, Unrelated, tank, new Vector3(-260, 0, -140)));
        Child(world, AddNode(gamez, Terrain, ground, new Vector3(-150, 0, -150)));
        Child(world, AddNode(gamez, Facade, facade, new Vector3(-230, 0, -110)));
        Child(world, AddNode(gamez, Glow, glow, new Vector3(-240, 0, -110)));
        var flyout = AddNode(gamez, flyoutModel, -1, Vector3.Zero);
        Child(flyout, AddNode(gamez, FlyoutBody, body, Vector3.Zero));
        Child(flyout, AddNode(gamez, FlyoutFin, fin, new Vector3(0.6f, 0, 0.4f)));
        return gamez;
    }

    private static GameZNode AddNode(GameZ gamez, string name, int mesh, Vector3 at, string kind = "Object3d")
    {
        var node = new GameZNode
        {
            Kind = kind,
            Name = name,
            MeshIndex = mesh,
            Index = gamez.Nodes.Count,
            Local = new Transform3D(Basis.Identity, at),
        };
        gamez.Nodes.Add(node);
        return node;
    }

    private static void Child(GameZNode parent, GameZNode child) => parent.Children.Add(child.Index);

    private static int AddMesh(GameZ gamez, GameZMesh mesh)
    {
        gamez.Meshes.Add(mesh);
        return gamez.Meshes.Count - 1;
    }

    private static GameZMesh Box(Vector3 lo, Vector3 hi)
    {
        var mesh = new GameZMesh();
        for (int c = 0; c < 8; c++)
        {
            mesh.Vertices.Add(new Vector3((c & 1) == 0 ? lo.X : hi.X, (c & 2) == 0 ? lo.Y : hi.Y, (c & 4) == 0 ? lo.Z : hi.Z));
        }

        // The faces index corners counter-clockwise around each z end, so the y-high pair swaps.
        int[] order = { 0, 1, 3, 2, 4, 5, 7, 6 };
        foreach (var face in BoxFaces)
        {
            var poly = new GameZPolygon { MaterialIndex = 0 };
            poly.VertexIndices.AddRange(face.Select(i => order[i]));
            mesh.Polygons.Add(poly);
        }

        return mesh;
    }

    private static GameZMesh Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var mesh = new GameZMesh();
        mesh.Vertices.AddRange(new[] { a, b, c, d });
        var poly = new GameZPolygon { MaterialIndex = 0, ShowBackface = true };
        poly.VertexIndices.AddRange(new[] { 0, 1, 2, 3 });
        mesh.Polygons.Add(poly);
        return mesh;
    }

    private static int Index(GameZ gamez, string name) => gamez.FindByName(name)!.Index;

    private static IEnumerable<Node3D> Nodes(Node3D root) =>
        new[] { root }.Concat(root.FindChildren("*", "Node3D", recursive: true, owned: false).OfType<Node3D>())
            .Where(n => n.HasMeta(AnimRuntime.IndexMeta));

    private static Node3D? ByIndex(Node3D root, int index) =>
        Nodes(root).FirstOrDefault(n => n.GetMeta(AnimRuntime.IndexMeta).AsInt32() == index);

    private static MeshInstance3D? MeshChild(Node3D? node) => node?.GetNodeOrNull<MeshInstance3D>("mesh");

    private static ArrayMesh? DrawnMesh(Node3D? node) => MeshChild(node)?.Mesh as ArrayMesh;

    // One build's world and flyout body, freed together.
    private sealed record Built(Node3D World, Node3D Flyout, int WorldReplaced, int FlyoutReplaced)
    {
        internal void Free()
        {
            World.Free();
            Flyout.Free();
        }
    }
}
