using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over a plane mod built from its own <c>plane.glb</c>. The fixture is the parent's
/// flight build, exported as F10 exports it and wrapped as a mod in the scratch directory. It is grafted
/// into a private copy of the aircraft archive, so no user mod and no shared archive is touched.</summary>
internal static class ModPlaneSuites
{
    private const string Parent = "player_brigand";
    private const string ModId = "suite_mod_plane";
    private const float FdSpeed = 123.5f;
    private const string PaintId = "suite_mod_paint";
    private const string TintMaterial = "suite_tint";
    private const int PaintSize = 8;

    // Metres: the file keeps float32 positions, so only the glTF round trip separates the two builds.
    private const float Tolerance = 1e-3f;

    private static readonly Color BaseTexel = Color.Color8(40, 60, 200);

    [Suite("mod-plane-build",
        "a plane mod whose plane.glb is the Brigand's flight build exported and whose plane.json overrides " +
        "fd_speed and clears the turrets builds as an airframe of its own: every drawn mesh outside the " +
        "interior comes from the file with a gamez twin, the visible triangles, the collider hulls, the " +
        "control surfaces, the spinning props, every weapon and reference marker and the cockpit camera " +
        "match the Brigand's, the parent's cockpit1 is mounted, the stats are the parent's with fd_speed " +
        "overridden and no turret mount, and the parent's fit binds with its turret slot as a forward gun")]
    internal static void ModPlaneBuild(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planes = ctx.PlanesCopy();
        if (planes.FindByName(Parent) == null)
        {
            throw new SuiteSkippedException($"{Parent} is not in the aircraft archive");
        }

        using var textures = new TextureArchive(texturesPath);
        string root = Path.Combine(ctx.ScratchDir, "mod-plane-build");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string folder = Path.Combine(ModPlanes.FolderIn(root), ModId);
        Directory.CreateDirectory(folder);
        var stock = Build(planes, textures, Parent, out var stockBuilder);
        try
        {
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(stock, Path.Combine(folder, ModPlanes.ModelFile)), $"the {Parent} export write result");
            File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile),
                $$"""{ "kind_of": "{{Parent}}", "overrides": { "fd_speed": 123.5, "turrets": [] } }""");
            var mod = ModPlanes.Open(root).Find(ModId);
            ctx.Check(mod != null, $"the mod {ModId} loads from {folder}");
            if (mod == null)
            {
                return;
            }

            ModAirframes.Add(planes, mod.Id, mod.Cockpit ?? mod.KindOf, mod.Read(ModPlanes.ModelFile)!);
            var model = Build(planes, textures, ModId, out var builder);
            try
            {
                CheckModel(ctx, stock, model, builder);
                CheckMarkers(ctx, planes, stockBuilder, builder);
                CheckStatsAndFit(ctx, mod, model);
            }
            finally
            {
                model.Free();
            }
        }
        finally
        {
            stock.Free();
        }
    }

    [Suite("mod-plane-paint",
        "a plane mod with a tint takes livery colour on its own surfaces: a hand-made plane.glb whose body " +
        "material is tinted through a half-R mask paints the masked half in each scheme's colour 1 and keeps " +
        "the base texture on the other half under two schemes. The same holds for a livery lab repaint and an " +
        "AI build's shared painter, no livery leaves the base texture, and its *_noselogo surface takes each " +
        "scheme's nose decal")]
    internal static void ModPlanePaint(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planes = ctx.PlanesCopy();
        if (planes.FindByName(Parent) == null)
        {
            throw new SuiteSkippedException($"{Parent} is not in the aircraft archive");
        }

        using var textures = new TextureArchive(texturesPath);
        string root = Path.Combine(ctx.ScratchDir, "mod-plane-paint");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string folder = Path.Combine(ModPlanes.FolderIn(root), PaintId);
        Directory.CreateDirectory(folder);
        ctx.Same((long)Error.Ok, (long)WritePaintFixture(folder), $"the fixture's write result");
        var mod = ModPlanes.Open(root).Find(PaintId);
        ctx.Check(mod?.Tint != null, $"the mod {PaintId} loads with its tint from {folder}");
        if (mod?.Tint is not { } tint)
        {
            return;
        }

        ModAirframes.Add(planes, mod.Id, mod.KindOf, mod.Read(ModPlanes.ModelFile)!,
            ModTint.Decode(tint.Materials, mod.Read(tint.Mask)!, mod.Read(tint.Detail)!));
        var a = new PaintScheme { Pattern = "hughes", Color1 = Color.Color8(243, 194, 0), NoseDecal = 21 };
        var b = new PaintScheme { Pattern = "blackhat", Color1 = Color.Color8(177, 130, 66), NoseDecal = 36 };

        var flownA = new PlaneBuilder(planes, textures, scheme: a);
        var modelA = flownA.Build(PaintId);
        var flownB = new PlaneBuilder(planes, textures, scheme: b);
        var modelB = flownB.Build(PaintId);
        var ai = new PlaneBuilder(planes, textures, scheme: a,
            painter: new PlanePainter(textures, PatternLibrary.Empty, a, PlanePainter.PrefixFor(planes, planes.FindByName(PaintId)!)!, ModAirframes.TintFor(PaintId)));
        var modelAi = ai.Build(PaintId);
        try
        {
            CheckTint(ctx, "flown, scheme A", Body(modelA), a.Color1);
            CheckTint(ctx, "flown, scheme B", Body(modelB), b.Color1);
            CheckTint(ctx, "AI painter, scheme A", Body(modelAi), a.Color1);
            var decalA = textures.FindByDecalIndex(a.NoseDecal) is { } nameA ? textures.FindImage(nameA) : null;
            var decalB = textures.FindByDecalIndex(b.NoseDecal) is { } nameB ? textures.FindImage(nameB) : null;
            ctx.Check(Wears(modelA, decalA) && !Wears(modelA, decalB) && Wears(modelB, decalB) && !Wears(modelB, decalA),
                $"the noselogo surface wears scheme A's nose decal {a.NoseDecal} and scheme B's {b.NoseDecal}, each only its own");

            flownA.Repaint(b);
            CheckTint(ctx, "livery lab repaint to scheme B", Body(modelA), b.Color1);
            flownA.Repaint(null);
            var bare = Body(modelA);
            ctx.Check(bare != null && Is(Texel(bare, 1), BaseTexel) && Is(Texel(bare, 6), BaseTexel),
                $"no livery shows the base texture: masked {Show(bare, 1)}, unmasked {Show(bare, 6)}, base {BaseTexel}");
        }
        finally
        {
            modelA.Free();
            modelB.Free();
            modelAi.Free();
        }
    }

    // A human rig's build: spinning props, damage panels and the cockpit interior.
    private static Node3D Build(GameZ planes, TextureArchive textures, string plane, out PlaneBuilder builder)
    {
        builder = new PlaneBuilder(planes, textures, spinningProps: true, cockpitInterior: true);
        return builder.Build(plane);
    }

    private static void CheckModel(TestContext ctx, Node3D stock, Node3D model, PlaneBuilder builder)
    {
        var drawn = Meshes(model, builder.CockpitInterior).ToList();
        int fromFile = drawn.Count(m => m.HasMeta(SceneBuilder.GamezMeshMeta));
        ctx.Check(drawn.Count > 0 && fromFile == drawn.Count, $"every drawn airframe mesh comes from the file with a gamez twin from_file={fromFile} drawn={drawn.Count}");

        var stockTris = new List<PlaneCollider.Triangle>();
        var modTris = new List<PlaneCollider.Triangle>();
        PlaneCollider.Collect(stock, stock.Transform, stockTris);
        PlaneCollider.Collect(model, model.Transform, modTris);
        ctx.Same(stockTris.Count, modTris.Count, $"visible airframe triangles");

        var stockHulls = PlaneCollider.Build(stock)?.Parts ?? Array.Empty<PlaneCollider.Part>();
        var modHulls = PlaneCollider.Build(model)?.Parts ?? Array.Empty<PlaneCollider.Part>();
        ctx.Same(stockHulls.Count, modHulls.Count, $"collider hulls");
        for (int i = 0; i < Math.Min(stockHulls.Count, modHulls.Count); i++)
        {
            var (a, b) = (stockHulls[i], modHulls[i]);
            bool same = a.Name == b.Name && Near(a.Local.Origin + a.Hull.Bounds.Position, b.Local.Origin + b.Hull.Bounds.Position)
                && Near(a.Hull.Bounds.Size, b.Hull.Bounds.Size);
            ctx.Check(same, $"hull {i} {b.Name} {PlaneCollider.PartLine(b.Name, b.Hull)} matches the parent's {PlaneCollider.PartLine(a.Name, a.Hull)}");
        }

        int stockSurfaces = ControlSurfaceAnimator.Build(stock)?.Count ?? 0;
        int stockProps = PropAnimator.Build(stock)?.Count ?? 0;
        ctx.Check(stockSurfaces > 0 && stockProps > 0, $"the parent drives {stockSurfaces} surfaces and {stockProps} props");
        ctx.Same(stockSurfaces, ControlSurfaceAnimator.Build(model)?.Count ?? 0, $"control surfaces found on the mod");
        ctx.Same(stockProps, PropAnimator.Build(model)?.Count ?? 0, $"spinning props found on the mod");
        ctx.Check(builder.CockpitInterior != null, $"the parent's cockpit1 is mounted on the mod");
    }

    private static void CheckMarkers(TestContext ctx, GameZ planes, PlaneBuilder stockBuilder, PlaneBuilder builder)
    {
        var stock = MarkerRig.Extract(planes, Parent)!.Markers;
        var mod = MarkerRig.Extract(planes, ModId)?.Markers ?? Array.Empty<MarkerRig.Marker>();
        ctx.Check(stock.Count > 0, $"the parent carries {stock.Count} weapon markers");
        ctx.Same(stock.Count, mod.Count, $"weapon markers on the mod");
        foreach (var marker in stock)
        {
            var found = mod.FirstOrDefault(m => m.Name.Equals(marker.Name, StringComparison.OrdinalIgnoreCase));
            ctx.Check(found.Name != null && Near(found.Local, marker.Local), $"{marker.Name} at {found.Local} matches the parent's {marker.Local}");
        }

        foreach (string name in new[] { "ground_level", "target", "exhaust1" })
        {
            var at = MarkerRig.FindNamedMarker(planes, Parent, name);
            ctx.Check(Near(MarkerRig.FindNamedMarker(planes, ModId, name), at), $"{name} resolves on the mod at the parent's {at}");
        }

        ctx.Check(Near(builder.CockpitCameraOffset, stockBuilder.CockpitCameraOffset) && builder.CockpitCameraOffset != Vector3.Zero,
            $"cockpit_camera {builder.CockpitCameraOffset} matches the parent's {stockBuilder.CockpitCameraOffset}");
    }

    private static void CheckStatsAndFit(TestContext ctx, ModPlane mod, Node3D model)
    {
        var parent = PlaneStats.Load(ctx.Zrdr, Parent);
        var stats = mod.Stats(ctx.Zrdr);
        ctx.Check(Mathf.IsEqualApprox(stats.FdSpeed, FdSpeed) && stats.VehWeight == parent.VehWeight && stats.DefName == parent.DefName,
            $"stats: fd_speed {stats.FdSpeed} (parent {parent.FdSpeed}), weight {stats.VehWeight} (parent {parent.VehWeight}), def {stats.DefName}");
        ctx.Check(stats.TurretMounts.Count == 0, $"no turret mount (parent {parent.TurretMounts.Count})");

        var stockLoadouts = StockLoadouts.Load();
        var fit = mod.StockLoadout(stockLoadouts);
        ctx.Check(fit != null, $"the parent's stock fit is the mod's");
        if (fit == null)
        {
            return;
        }

        var loadout = Loadout.Bind(fit, model, WeaponDefs.Load(ctx.Zrdr, null));
        int firable = loadout.FirableGuns.Count();
        ctx.Check(firable == loadout.Guns.Count && loadout.Guns.All(g => g.MuzzleCount > 0),
            $"every bound gun group fires forward from the mod's firepoints firable={firable} groups={loadout.Guns.Count}");
        ctx.Same(fit.Hardpoints?.Count ?? 0, loadout.Hardpoints.Count, $"hardpoints bound on the mod's pylons");
    }

    // Every drawn mesh instance outside the cockpit interior.
    private static IEnumerable<MeshInstance3D> Meshes(Node node, Node3D? interior)
    {
        if (ReferenceEquals(node, interior))
        {
            yield break;
        }

        if (node is MeshInstance3D { Mesh: not null } mesh && mesh.Name.ToString() == "mesh")
        {
            yield return mesh;
        }

        foreach (var child in node.GetChildren())
        {
            foreach (var found in Meshes(child, interior))
            {
                yield return found;
            }
        }
    }

    private static bool Near(Vector3 a, Vector3 b) => (a - b).Length() <= Tolerance;

    // The paint fixture: a quad of the tinted body material on an 8 px base texture and a quad of a decal
    // material. The 4 px mask's left half is R, and the detail map is flat neutral grey.
    private static Error WritePaintFixture(string folder)
    {
        var baseImage = Image.CreateEmpty(PaintSize, PaintSize, false, Image.Format.Rgba8);
        baseImage.Fill(BaseTexel);
        var mask = Image.CreateEmpty(PaintSize / 2, PaintSize / 2, false, Image.Format.Rgb8);
        mask.FillRect(new Rect2I(0, 0, PaintSize / 4, PaintSize / 2), new Color(1f, 0f, 0f));
        var detail = Image.CreateEmpty(1, 1, false, Image.Format.L8);
        detail.Fill(Color.Color8(128, 128, 128));
        mask.SavePng(Path.Combine(folder, "mask.png"));
        detail.SavePng(Path.Combine(folder, "detail.png"));
        File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile),
            $$"""{ "kind_of": "{{Parent}}", "tint": { "mask": "mask.png", "detail": "detail.png", "materials": ["{{TintMaterial}}"] } }""");

        var mesh = new ArrayMesh();
        Quad(mesh, new StandardMaterial3D { ResourceName = TintMaterial, AlbedoTexture = TextureUpload.Create(baseImage) }, 0f);
        Quad(mesh, new StandardMaterial3D { ResourceName = "suite_noselogo", Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor }, 0.01f);
        var top = new Node3D { Name = PaintId };
        var nose = new MeshInstance3D { Name = "nose", Mesh = mesh };
        top.AddChild(nose);
        nose.Owner = top;
        var doc = new GltfDocument();
        var state = new GltfState();
        var err = doc.AppendFromScene(top, state);
        if (err == Error.Ok)
        {
            err = doc.WriteToFilesystem(state, Path.Combine(folder, ModPlanes.ModelFile));
        }

        top.Free();
        return err;
    }

    private static void Quad(ArrayMesh mesh, Material material, float y)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetMaterial(material);
        foreach (var (x, z) in new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 0f), (1f, 1f), (0f, 1f) })
        {
            st.SetNormal(Vector3.Up);
            st.SetUV(new Vector2(x, z));
            st.AddVertex(new Vector3(x, y, z));
        }

        st.Index();
        st.Commit(mesh);
    }

    // The built plane's surface textures under the mod's own materials: the body is the base texture's size.
    private static IEnumerable<Image> SurfaceImages(Node model) =>
        Meshes(model, null).SelectMany(m => Enumerable.Range(0, m.Mesh.GetSurfaceCount()).Select(m.Mesh.SurfaceGetMaterial))
            .OfType<ShaderMaterial>()
            .Select(s => s.GetShaderParameter("albedo_tex").As<Texture2D>()?.GetImage())
            .OfType<Image>();

    private static Image? Body(Node model) => SurfaceImages(model).FirstOrDefault(i => i.GetWidth() == PaintSize);

    // Whether a surface of the model wears `decal`, compared on the diagonal's colour (the upload may cut alpha).
    private static bool Wears(Node model, Image? decal) => decal != null && SurfaceImages(model).Any(i =>
        i.GetWidth() == decal.GetWidth() && i.GetHeight() == decal.GetHeight()
        && Enumerable.Range(0, Math.Min(decal.GetWidth(), decal.GetHeight())).All(x => i.GetPixel(x, x).ToHtml(false) == decal.GetPixel(x, x).ToHtml(false)));

    // Texel x of the body texture's top row, the mask's R half at x < 4.
    private static Color Texel(Image image, int x) => image.GetPixel(x, 0);

    private static bool Is(Color a, Color b) => a.ToHtml() == b.ToHtml();

    private static string Show(Image? image, int x) => image != null ? Texel(image, x).ToHtml() : "none";

    private static void CheckTint(TestContext ctx, string what, Image? body, Color colour)
    {
        ctx.Check(body != null && Is(Texel(body, 1), colour) && Is(Texel(body, 2), colour),
            $"{what}: the masked half is colour 1 {colour.ToHtml()}, texels {Show(body, 1)} {Show(body, 2)}");
        ctx.Check(body != null && Is(Texel(body, 5), BaseTexel) && Is(Texel(body, 6), BaseTexel),
            $"{what}: the unmasked half is the base texture {BaseTexel.ToHtml()}, texels {Show(body, 5)} {Show(body, 6)}");
    }
}
