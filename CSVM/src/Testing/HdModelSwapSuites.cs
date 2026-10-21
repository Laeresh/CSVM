using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.Utils;
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
    private const string Crate = "probe_crate";

    // An original texture of the synthetic C1 archive, as a gamez material names it.
    private const string SkinTexture = "probe_skin.tif";

    // The replaced shed's glTF materials, one surface each, in file order.
    private const string SkinClamped = "PROBE_SKIN";
    private const string SkinTiled = "Probe_Skin.TGA";
    private const string PackPaint = "pack_paint";
    private const string PackGlass = "pack_glass";
    private const string PackLamp = "pack_lamp";

    // The side of pack_paint's own base-colour texture.
    private const int PaintSide = 8;

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
        + "unrelated node is untouched, and a builder with no chapter or airframe replaces nothing. "
        + "A replaced surface wears the remake's shader: a glTF material named after an original "
        + "texture takes the very material an original surface of it takes, clamped by its own UVs, "
        + "and repaints with it; any other takes the file's base colour, two-sided, cutout, blended "
        + "or unlit as glTF says, follows a graphics-mode switch, and the shared import keeps its own materials")]
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
            var materials = key.EndsWith(ReplacedWorld, StringComparison.Ordinal) ? ShedMaterials() : null;
            string file = Path.Combine(pack, key + ".glb");
            ctx.Same((long)Error.Ok, (long)WriteModel(file, key[(key.LastIndexOf('/') + 1)..], materials),
                $"{key}.glb write result");
            if (materials != null)
            {
                RestoreDot(file, SkinTiled);
            }
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
            CheckMaterials(ctx, gamez, textures, models, packed);

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

    // The shed's replacement surfaces against the materials the same builder gives the original crate.
    private static void CheckMaterials(TestContext ctx, GameZ gamez, TextureArchive textures, HdModels models, Built packed)
    {
        var shared = HdModelMeshes.Current.For(ReplacedWorld, Chapter);
        var drawn = DrawnMesh(ByIndex(packed.World, Index(gamez, ReplacedWorld)));
        var crate = DrawnMesh(ByIndex(packed.World, Index(gamez, Crate)));
        ctx.Check(shared != null && drawn != null && crate != null && drawn.GetSurfaceCount() == 5 && shared.GetSurfaceCount() == 5,
            $"the shed draws its five replacement surfaces and the crate its gamez model");
        if (shared == null || drawn == null || crate == null || drawn.GetSurfaceCount() != 5)
        {
            return;
        }

        var mats = Enumerable.Range(0, 5).Select(i => drawn.SurfaceGetMaterial(i)).OfType<ShaderMaterial>().ToArray();
        var skin = crate.SurfaceGetMaterial(0) as ShaderMaterial;
        ctx.Check(mats.Length == 5 && mats.All(ShaderTwins.IsTracked), $"every replacement surface wears a tracked remake shader");
        if (mats.Length != 5 || skin == null)
        {
            return;
        }

        ctx.Check(ReferenceEquals(mats[0], skin),
            $"{SkinClamped} wears the very material the crate's {SkinTexture} surface wears");
        ctx.Check(Code(mats[0]).Contains("cull_front", StringComparison.Ordinal),
            $"a one-sided replacement surface culls as a one-sided original surface does");
        ctx.Check(!ReferenceEquals(mats[1], skin) && ReferenceEquals(Albedo(mats[1]), Albedo(skin)) && Albedo(skin) != null,
            $"{SkinTiled} matches {SkinTexture} without case or extension and samples the archive's texture");
        ctx.Check(Code(mats[0]).Contains("repeat_disable", StringComparison.Ordinal) && Code(mats[1]).Contains("repeat_enable", StringComparison.Ordinal),
            $"each replacement surface clamps by its own UVs: inside the unit square clamps, tiling repeats");

        var paint = Albedo(mats[2]);
        ctx.Check(paint != null && paint.GetWidth() == PaintSide && paint.GetHeight() == PaintSide && paint.GetImage().HasMipmaps(),
            $"{PackPaint} samples the pack's own {PaintSide}x{PaintSide} base-colour texture, mip-mapped");
        ctx.Check(Code(mats[2]).Contains("cull_disabled", StringComparison.Ordinal) && Code(mats[2]).Contains("ALPHA_SCISSOR_THRESHOLD", StringComparison.Ordinal)
                && packed.Scene.AlphaOf(mats[2]) == SceneBuilder.TransparencyClass.ScissorSurface,
            $"a doubleSided MASK material is two-sided and scissored");
        ctx.Check(packed.Scene.AlphaOf(mats[3]) == SceneBuilder.TransparencyClass.BlendSurface
                && mats[3].GetShaderParameter("albedo_color").AsColor().A < 1f,
            $"a BLEND material blends, at its own base colour");
        ctx.Check(Unlit(mats[4]) && !Unlit(mats[3]), $"a KHR_materials_unlit material is unlit, any other lit");

        CheckSharedUntouched(ctx, shared, drawn, new HdModelMeshes(models).For(ReplacedWorld, Chapter));
        CheckModeSwitch(ctx, mats);
        CheckRepaint(ctx, gamez, textures, models);
    }

    // The shared import against a fresh import of the same file: no builder edited it.
    private static void CheckSharedUntouched(TestContext ctx, ArrayMesh shared, ArrayMesh drawn, ArrayMesh? fresh)
    {
        ctx.Check(fresh != null && fresh.GetSurfaceCount() == shared.GetSurfaceCount(), $"the pack file imports again");
        if (fresh == null)
        {
            return;
        }

        var names = new[] { SkinClamped, SkinTiled, PackPaint, PackGlass, PackLamp };
        bool same = true;
        for (int i = 0; i < shared.GetSurfaceCount(); i++)
        {
            var a = shared.SurfaceGetMaterial(i) as StandardMaterial3D;
            var b = fresh.SurfaceGetMaterial(i) as StandardMaterial3D;
            same &= a != null && b != null && a.ResourceName == names[i] && a.ResourceName == b.ResourceName
                && a.CullMode == b.CullMode && a.Transparency == b.Transparency && a.ShadingMode == b.ShadingMode
                && a.AlbedoColor == b.AlbedoColor && (a.AlbedoTexture == null) == (b.AlbedoTexture == null)
                && a.AlbedoTexture?.GetImage().HasMipmaps() != true
                && !ReferenceEquals(a, drawn.SurfaceGetMaterial(i));
        }

        ctx.Check(same, $"the shared mesh keeps the importer's materials, unmipped, as a fresh import has them");
        var paint = shared.SurfaceGetMaterial(2) as StandardMaterial3D;
        var glass = shared.SurfaceGetMaterial(3) as StandardMaterial3D;
        var lamp = shared.SurfaceGetMaterial(4) as StandardMaterial3D;
        ctx.Check(paint?.CullMode == BaseMaterial3D.CullModeEnum.Disabled && paint.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor
                && glass?.Transparency is BaseMaterial3D.TransparencyEnum.Alpha or BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass
                && lamp?.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded,
            $"the importer maps doubleSided, MASK, BLEND and KHR_materials_unlit as the builder reads them");
    }

    // A graphics-mode switch moves every replacement material onto its key's shader for the new mode.
    private static void CheckModeSwitch(TestContext ctx, ShaderMaterial[] mats)
    {
        bool wasEnhanced = GraphicsMode.Enhanced;
        bool wasDrawn = ShaderTwins.EnhancedDrawn;
        var before = mats.Select(m => m.Shader).ToArray();
        try
        {
            // As the shader-twins suite does: a twin per mode, never a text rewritten in place.
            ShaderTwins.EnhancedDrawn = true;
            GraphicsMode.Set(!wasEnhanced);
            ShaderTwins.Regenerate();
            ctx.Check(mats.All(m => !ShaderTwins.IsStale(m)), $"after a switch no replacement material is left on the other mode's shader");
            ctx.Check(new[] { 0, 2, 3 }.All(i => !ReferenceEquals(mats[i].Shader, before[i])),
                $"the switch moves the lit replacement materials onto the other mode's twin");
        }
        finally
        {
            GraphicsMode.Set(wasEnhanced);
            ShaderTwins.Regenerate();
            ShaderTwins.EnhancedDrawn = wasDrawn;
        }
    }

    // A live repaint re-resolves an original-texture surface of a replacement through the builder's
    // substitute, the path PlaneBuilder.Repaint takes for a livery.
    private static void CheckRepaint(TestContext ctx, GameZ gamez, TextureArchive textures, HdModels models)
    {
        var livery = TextureUpload.Create(Image.CreateEmpty(4, 4, false, Image.Format.Rgba8));
        bool painted = false;
        var builder = new SceneBuilder(gamez, textures, fullbright: true, cullBackfaces: true,
            textureSubstitute: (name, tex) => painted && name == SkinTexture ? livery : tex)
        { HdChapter = Chapter };
        var saved = HdModels.Current;
        HdModels.Current = models;
        Node3D? shed = null;
        try
        {
            shed = builder.BuildSubtree(gamez.Nodes[Index(gamez, ReplacedWorld)]);
            var mat = DrawnMesh(shed)?.SurfaceGetMaterial(0) as ShaderMaterial;
            var shipped = mat != null ? Albedo(mat) : null;
            painted = true;
            builder.Repaint();
            ctx.Check(mat != null && shipped != null && !ReferenceEquals(shipped, livery) && ReferenceEquals(Albedo(mat), livery),
                $"a repaint swaps the skin of a replacement surface named after an original texture");
        }
        finally
        {
            HdModels.Current = saved;
            shed?.Free();
        }
    }

    // The shed's glTF materials, in surface order: two named after an original texture, three not.
    private static (StandardMaterial3D, float)[] ShedMaterials()
    {
        var image = Image.CreateEmpty(PaintSide, PaintSide, false, Image.Format.Rgba8);
        image.Fill(new Color(0.9f, 0.6f, 0.1f));
        return new[]
        {
            (new StandardMaterial3D { ResourceName = SkinClamped }, 1f),
            (new StandardMaterial3D { ResourceName = SkinTiled }, 2f),
            (new StandardMaterial3D
            {
                ResourceName = PackPaint, AlbedoTexture = TextureUpload.Create(image),
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            }, 1f),
            (new StandardMaterial3D
            {
                ResourceName = PackGlass, AlbedoColor = new Color(0.2f, 0.4f, 0.9f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            }, 1f),
            (new StandardMaterial3D { ResourceName = PackLamp, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }, 1f),
        };
    }

    // Godot's glTF writer turns a material name's dot into an underscore, where Blender keeps it. The
    // spelling is the same length, so the binary's chunk lengths stay valid.
    private static void RestoreDot(string file, string name)
    {
        var bytes = File.ReadAllBytes(file);
        var written = System.Text.Encoding.ASCII.GetBytes(name.Replace('.', '_'));
        int at = bytes.AsSpan().IndexOf(written);
        if (at >= 0)
        {
            System.Text.Encoding.ASCII.GetBytes(name).CopyTo(bytes, at);
            File.WriteAllBytes(file, bytes);
        }
    }

    private static string Code(ShaderMaterial material) => material.Shader?.Code ?? string.Empty;

    private static Texture2D? Albedo(ShaderMaterial material) => material.GetShaderParameter("albedo_tex").As<Texture2D>();

    // The world's unlit variant: Original drops the SUNLIGHT dimming, Enhanced keeps the unshaded arm.
    private static bool Unlit(ShaderMaterial material) => GraphicsMode.Enhanced
        ? Code(material).Contains("unshaded", StringComparison.Ordinal)
        : !Code(material).Contains("ALBEDO *= csky_world_light_at", StringComparison.Ordinal);

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

        var drawn = DrawnMesh(b);
        ctx.Check(expected != null && drawn != null && !ReferenceEquals(drawn, expected)
                && drawn.GetSurfaceCount() == expected.GetSurfaceCount() && drawn.GetAabb() == expected.GetAabb(),
            $"{label}: draws the builder's copy of the pack's mesh");
        ctx.Check(drawn != null && Enumerable.Range(0, drawn.GetSurfaceCount())
                .All(i => drawn.SurfaceGetMaterial(i) is ShaderMaterial m && ShaderTwins.IsTracked(m)),
            $"{label}: every replaced surface wears a tracked remake shader, no StandardMaterial3D");
        ctx.Check(DrawnMesh(a)?.GetAabb() != expected?.GetAabb(), $"{label}: draws the gamez model without the pack");
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
            return new Built(world, flyout, replaced, builder.Scene.ReplacedMeshCount - replaced, builder.Scene);
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

    // One tetrahedron under a node named for the key, the shape SceneBuilder builds and the exporter
    // writes. Given materials, one tetrahedron surface each, with UVs inside the unit square or tiling.
    private static Error WriteModel(string path, string node, IReadOnlyList<(StandardMaterial3D Material, float UvScale)>? materials = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var mesh = new ArrayMesh();
        materials ??= new[] { (new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.3f, 0.2f) }, 1f) };
        foreach (var (material, uvScale) in materials)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = new[] { new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(0, 3, 0), new Vector3(0, 0, 1) };
            arrays[(int)Mesh.ArrayType.TexUV] = new[] { new Vector2(0, 0), new Vector2(uvScale, 0), new Vector2(0, uvScale), new Vector2(uvScale, uvScale) };
            arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 3, 1, 0, 2, 3, 1, 3, 2 };
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
        }

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
        gamez.Materials.Add(new GameZMaterial { TextureName = SkinTexture });
        int shed = AddMesh(gamez, Box(new Vector3(-4, 0, -3), new Vector3(4, 5, 3)));
        int crate = AddMesh(gamez, Box(new Vector3(-1, 0, -1), new Vector3(1, 2, 1), material: 1));
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
        Child(world, AddNode(gamez, Crate, crate, new Vector3(-210, 0, -130)));
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

    // A textured box maps the whole texture once onto every face, as the synthetic airframes do.
    private static GameZMesh Box(Vector3 lo, Vector3 hi, int material = 0)
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
            var poly = new GameZPolygon { MaterialIndex = material };
            poly.VertexIndices.AddRange(face.Select(i => order[i]));
            if (material > 0)
            {
                poly.UvCoords = new List<Vector2> { new(0, 1), new(1, 1), new(1, 0), new(0, 0) };
            }

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
    private sealed record Built(Node3D World, Node3D Flyout, int WorldReplaced, int FlyoutReplaced, SceneBuilder Scene)
    {
        internal void Free()
        {
            World.Free();
            Flyout.Free();
        }
    }
}
