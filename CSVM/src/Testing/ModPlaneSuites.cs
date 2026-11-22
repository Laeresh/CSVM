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

    // Metres: the file keeps float32 positions, so only the glTF round trip separates the two builds.
    private const float Tolerance = 1e-3f;

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
}
