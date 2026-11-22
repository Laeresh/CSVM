using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hangar;
using CSVM.Flight.Hud;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.UI.Boards;
using CSVM.UI.Hangar;
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
    private const string OwnDonor = "player_fury";
    private const string NamedDonor = "player_warhawk";
    private const string OwnMod = "suite_mod_own_cockpit";
    private const string NamedMod = "suite_mod_named_cockpit";
    private const string ParentMod = "suite_mod_parent_cockpit";
    private const string WreckId = "suite_mod_wreck";
    private const string OwnWreckId = "suite_mod_own_wreck";
    private const string CampaignModId = "suite_mod_campaign";
    private const string UnknownUnlockId = "suite_mod_unknown_unlock";
    private const string CampaignPilot = "Suite Mod Pilot";

    // The story position the campaign mod's unlock names, a mid-campaign mission.
    private const int CampaignUnlockSeq = 6;
    private const string NetModId = "suite_mod_net";
    private const string ArtMod = "suite_mod_art";
    private const string PortraitMod = "suite_mod_portrait";

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

    [Suite("mod-plane-bot",
        "a bot seat on a plane mod: a field seats the mod's id, named or drawn as Random, on a militia that flies " +
        "its parent, and a registry without the mod draws none; the seat builds from the mod's plane.glb, " +
        "its hull is the parent's def with fd_speed overridden, its AI flavour is the parent's AI def with the same " +
        "override and no turret, no militia def names the mod so it flies the stock arms, and the mod's fit binds " +
        "its turret slot as a forward gun on the model's firepoints")]
    internal static void ModPlaneBot(TestContext ctx)
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
        string root = Path.Combine(ctx.ScratchDir, "mod-plane-bot");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string folder = Path.Combine(ModPlanes.FolderIn(root), ModId);
        Directory.CreateDirectory(folder);
        var stock = Build(planes, textures, Parent, out _);
        try
        {
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(stock, Path.Combine(folder, ModPlanes.ModelFile)), $"the {Parent} export write result");
        }
        finally
        {
            stock.Free();
        }

        File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile),
            $$"""{ "kind_of": "{{Parent}}", "overrides": { "fd_speed": 123.5, "turrets": [] } }""");
        var mod = ModPlanes.Open(root).Find(ModId);
        ctx.Check(mod != null, $"the mod {ModId} loads from {folder}");
        if (mod == null)
        {
            return;
        }

        ModAirframes.Add(planes, mod.Id, mod.Cockpit ?? mod.KindOf, mod.Read(ModPlanes.ModelFile)!);
        var airframes = new AirframeRegistry();
        airframes.Register(mod.Id, mod.Name, mod.KindOf);
        CheckBotSeats(ctx, airframes);

        var model = Build(planes, textures, ModId, out var builder);
        try
        {
            var drawn = Meshes(model, builder.CockpitInterior).ToList();
            ctx.Check(drawn.Count > 0 && drawn.All(m => m.HasMeta(SceneBuilder.GamezMeshMeta)),
                $"the bot seat builds from the mod's plane.glb ({drawn.Count(m => m.HasMeta(SceneBuilder.GamezMeshMeta))} of {drawn.Count} meshes)");

            var ai = mod.Stats(ctx.Zrdr, forAi: true);
            var parentAi = PlaneStats.LoadForAi(ctx.Zrdr, Parent, null);
            ctx.Check(ai.AiDefName == parentAi.AiDefName && Mathf.IsEqualApprox(ai.FdSpeed, FdSpeed) && ai.TurretMounts.Count == 0,
                $"AI stats: def {ai.AiDefName} (parent {parentAi.AiDefName}), fd_speed {ai.FdSpeed} (parent {parentAi.FdSpeed}), turret mounts {ai.TurretMounts.Count} (parent {parentAi.TurretMounts.Count})");

            // HumanFlightAdapter arms a bot by its militia's def for the node, and none names a mod.
            var militias = MilitiaFits.Load(ctx.Zrdr, ctx.Data.Messages);
            var flying = BotSeats.Pairs.Where(p => p.Plane == Parent).Select(p => InstantAction.Militias[p.Militia].Name).ToList();
            var parentDefs = flying.Where(m => militias.DefFor(m, Parent) != null).ToList();
            ctx.Check(flying.All(m => militias.DefFor(m, ModId) == null) && BotSeats.ArmsFor(false, false, false) == SeatArms.Stock,
                $"no militia def names the mod, so it flies the stock arms (militias {string.Join(", ", flying)}; with a def for the parent: {string.Join(", ", parentDefs)})");
            CheckStatsAndFit(ctx, mod, model);
        }
        finally
        {
            model.Free();
            Directory.Delete(root, recursive: true);
        }
    }

    [Suite("mod-plane-cockpit",
        "a plane mod's cockpit view takes its own cockpit1, else plane.json's cockpit, else its parent's: three Brigand " +
        "mods, one whose plane.glb carries the Fury's interior as cockpit1 (and names the Warhawk), one naming the " +
        "Warhawk whose file keeps the Fury's interior under another name, one naming none, each mount at the mod's " +
        "cockpit_camera the interior of the Fury from the file, the Warhawk and the Brigand from the archive, drawing as " +
        "many triangles and binding as many gauges as that interior on its stock plane. Under the stereo build each " +
        "drops or cuts that interior's head plate, and a body cockpit rig builds its panel")]
    internal static void ModPlaneCockpit(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planes = ctx.PlanesCopy();
        foreach (string plane in new[] { Parent, OwnDonor, NamedDonor })
        {
            if (planes.FindByName(plane) == null)
            {
                throw new SuiteSkippedException($"{plane} is not in the aircraft archive");
            }
        }

        using var textures = new TextureArchive(texturesPath);
        string root = Path.Combine(ctx.ScratchDir, "mod-plane-cockpit");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var stock = new Dictionary<string, (int Triangles, string Gauges, int Plate)>();
        foreach (string plane in new[] { Parent, NamedDonor })
        {
            var model = Build(planes, textures, plane, out var builder);
            stock[plane] = (Triangles(builder.CockpitInterior), GaugeLine(builder), PlatePolygons(planes, plane).Count);
            model.Free();
        }

        var brigand = Build(planes, textures, Parent, out var brigandBuilder);
        var fury = Build(planes, textures, OwnDonor, out var furyBuilder);
        try
        {
            stock[OwnDonor] = (Triangles(furyBuilder.CockpitInterior), GaugeLine(furyBuilder), PlatePolygons(planes, OwnDonor).Sum(p => p.VertexIndices.Count - 2));
            string named = WriteCockpitMod(root, NamedMod, NamedDonor);
            string parent = WriteCockpitMod(root, ParentMod, null);
            string own = WriteCockpitMod(root, OwnMod, NamedDonor);
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(brigand, Path.Combine(parent, ModPlanes.ModelFile)), $"the {Parent} export write result");

            // The Fury's interior in interior units, every state kept, as cockpit1.glb exports it. Without
            // the name meta the export keeps it in the airframe file. As cockpit1 it is the file's own.
            // Under another name it is a leftover sharing generic names (gauges, horiz) with the Warhawk's.
            brigandBuilder.CockpitInterior!.Free();
            var interior = furyBuilder.CockpitInterior!;
            interior.GetParent().RemoveChild(interior);
            interior.RemoveMeta(AnimRuntime.NameMeta);
            interior.Transform = Transform3D.Identity;
            interior.Visible = true;
            interior.Name = "fury_reference";
            brigand.AddChild(interior);
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(brigand, Path.Combine(named, ModPlanes.ModelFile), allStates: true), $"the leftover interior export write result");
            interior.Name = "cockpit1";
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(brigand, Path.Combine(own, ModPlanes.ModelFile), allStates: true), $"the own cockpit1 export write result");
        }
        finally
        {
            brigand.Free();
            fury.Free();
        }

        var mods = ModPlanes.Open(root);
        XrCockpit.DeclareEyeGlobals();
        foreach (var (id, donor, fromFile) in new[] { (OwnMod, OwnDonor, true), (NamedMod, NamedDonor, false), (ParentMod, Parent, false) })
        {
            var mod = mods.Find(id);
            ctx.Check(mod != null, $"the mod {id} loads");
            if (mod == null)
            {
                continue;
            }

            ModAirframes.Add(planes, mod.Id, mod.Cockpit ?? mod.KindOf, mod.Read(ModPlanes.ModelFile)!);
            CheckCockpit(ctx, planes, textures, id, donor, fromFile, stock[donor], brigandBuilder.CockpitCameraOffset);
        }

        // A model pack's body cockpit rig over each mod, under the stereo build: the stereo body twins.
        string packRoot = Path.Combine(root, "pack");
        foreach (string id in new[] { OwnMod, ParentMod })
        {
            Directory.CreateDirectory(Path.Combine(packRoot, "HDModels", "body", "planes", id));
            File.WriteAllText(Path.Combine(packRoot, "HDModels", "body", "planes", id, "rig.json"), """{ "cockpit": { "from": "body" } }""");
        }

        using var pack = HdModels.Open(packRoot, "HDModels");
        var saved = HdModels.Current;
        HdModels.Current = pack;
        try
        {
            foreach (string id in new[] { OwnMod, ParentMod })
            {
                var builder = new PlaneBuilder(planes, textures, spinningProps: true, cockpitInterior: true, xrInterior: true);
                var model = builder.Build(id);
                ctx.Check(builder.Cockpit is { FromBody: true } && builder.CockpitPanel != null && builder.CockpitInterior != null,
                    $"{id}: a body cockpit rig builds its panel under the stereo build panel={builder.CockpitPanel != null}");
                model.Free();
            }
        }
        finally
        {
            HdModels.Current = saved;
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

    [Suite("mod-plane-wreck",
        "a plane mod without the stock destroyed or pdpN nodes breaks into its four damage zones: a hand-made " +
        "plane.glb of a fuselage, two wings and a fin cuts into piece1..piece4 under destroyed, nose, tail, " +
        "leftwing and rightwing, each piece's triangles in its own zone and on its own side, every drawn " +
        "triangle in exactly one piece. Each pdpN the parent's ladders name is added at its zone's centre, and " +
        "a stock name the model carries is not. A mod that ships a destroyed subtree builds that subtree as is")]
    internal static void ModPlaneWreck(TestContext ctx)
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
        string root = Path.Combine(ctx.ScratchDir, "mod-plane-wreck");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        foreach (var (id, ownWreck) in new[] { (WreckId, false), (OwnWreckId, true) })
        {
            string folder = Path.Combine(ModPlanes.FolderIn(root), id);
            Directory.CreateDirectory(folder);
            ctx.Same((long)Error.Ok, (long)WriteWreckFixture(folder, id, ownWreck), $"the {id} fixture's write result");
        }

        var mods = ModPlanes.Open(root);
        foreach (string id in new[] { WreckId, OwnWreckId })
        {
            var mod = mods.Find(id);
            ctx.Check(mod != null, $"the mod {id} loads from {root}");
            if (mod == null)
            {
                return;
            }

            ModAirframes.Add(planes, mod.Id, mod.KindOf, mod.Read(ModPlanes.ModelFile)!);
        }

        var model = Build(planes, textures, WreckId, out var builder);
        try
        {
            ctx.Check(builder.BuildDestroyed(WreckId) == null, $"{WreckId} ships no destroyed subtree");
            CheckAutoWreck(ctx, model, PlaneStats.Load(ctx.Zrdr, Parent));
        }
        finally
        {
            model.Free();
        }

        var own = new PlaneBuilder(planes, textures).BuildDestroyed(OwnWreckId);
        var piece = own != null ? CockpitTransplant.FindNamed(own, "piece1") : null;
        ctx.Check(piece != null && Meshes(piece, null).Any(m => m.HasMeta(SceneBuilder.GamezMeshMeta)),
            $"{OwnWreckId}'s own destroyed subtree builds with its piece1 drawn from the file destroyed={own != null} piece1={piece != null}");
        own?.Free();
    }

    [Suite("campaign-mod-plane",
        "a campaign profile owning a plane built on a mod keeps the mod id beside its parent's stock id: with the " +
        "mod installed a --campaign= launch over a --profiles= store seats the mod's node and that seat builds from " +
        "the mod's plane.glb; with it gone the seat is the parent's and a load and save of the profile leave its file " +
        "byte-identical, the id included. The mod's unlock, a mission id read from cm_sequence, offers it in the " +
        "campaign hangar from that mission's ordinal, and an id the campaign does not hold unlocks with the parent")]
    internal static void CampaignModPlane(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planes = ctx.PlanesCopy();
        var missions = CampaignSequence.Load(ctx.Zrdr);
        if (planes.FindByName(Parent) == null || missions.Count < CampaignUnlockSeq + 1)
        {
            throw new SuiteSkippedException($"{Parent} or cm_sequence seq {CampaignUnlockSeq} is not in the data");
        }

        using var textures = new TextureArchive(texturesPath);
        string root = Path.Combine(ctx.ScratchDir, "campaign-mod-plane");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var target = missions.First(m => m.Seq == CampaignUnlockSeq);
        foreach (var (id, unlock) in new[] { (CampaignModId, $"{target.ChapterFolder}/{target.MissionFolder}"), (UnknownUnlockId, "c9/m09") })
        {
            string folder = Path.Combine(ModPlanes.FolderIn(root), id);
            Directory.CreateDirectory(folder);
            ctx.Same((long)Error.Ok, (long)WriteWreckFixture(folder, id, ownWreck: false), $"the {id} fixture's write result");
            File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile), $$"""{ "kind_of": "{{Parent}}", "unlock": "{{unlock}}" }""");
        }

        var mods = ModPlanes.Open(root);
        var mod = mods.Find(CampaignModId);
        ctx.Check(mod != null && mods.Planes.Count == 2, $"both mods load from {root} ({mods.Planes.Count})");
        if (mod == null)
        {
            return;
        }

        foreach (var each in mods.Planes)
        {
            each.BindCampaign(missions);
        }

        int parent = HangarEconomy.Airframes[StockAirframes.IdOf(Parent)!.Value].Availability;
        ctx.Check(mod.CampaignAvailability == target.Ordinal && mods.Find(UnknownUnlockId)!.CampaignAvailability == parent,
            $"unlock {mod.Unlock} offers the mod from ordinal {target.Ordinal} ({mod.CampaignAvailability}), an unknown id at the parent's {parent} ({mods.Find(UnknownUnlockId)!.CampaignAvailability})");
        ModAirframes.Add(planes, mod.Id, mod.KindOf, mod.Read(ModPlanes.ModelFile)!);

        string dir = Path.Combine(root, "Profiles");
        var store = new CampaignProfileStore(dir);
        var profile = CampaignProfileDef.NewProfile(CampaignPilot);
        string planeName = "Suite Mod " + Guid.NewGuid().ToString("N")[..8];
        profile.Planes.Add(new OwnedPlane { Name = planeName, Airframe = StockAirframes.IdOf(Parent)!.Value, ModAirframe = CampaignModId });
        profile.SelectedPlane = 2;
        string path = store.Save(profile);
        byte[] saved = File.ReadAllBytes(path);
        var spec = SessionSpec.Parse(new[] { $"--campaign={CampaignPilot}:0", $"--profiles={dir}" });

        var previous = ModPlanes.Current;
        try
        {
            ModPlanes.Current = mods;
            var seated = CampaignDirector.ResolveSeatedPlane(spec);
            string node = seated.PlaneNames.Count > 0 ? seated.PlaneNames[0] : "none";
            ctx.Check(node == CampaignModId, $"the installed mod seats as its own node ({node})");
            var model = Build(planes, textures, node, out var builder);
            try
            {
                var drawn = Meshes(model, builder.CockpitInterior).ToList();
                ctx.Check(drawn.Count > 0 && drawn.All(m => m.HasMeta(SceneBuilder.GamezMeshMeta)),
                    $"the seat builds from the mod's plane.glb ({drawn.Count(m => m.HasMeta(SceneBuilder.GamezMeshMeta))} of {drawn.Count} meshes)");
            }
            finally
            {
                model.Free();
            }

            var wallet = new UI.Menu.CampaignWallet(store, profile, new CustomPlaneStore(Path.Combine(root, "Planes")));
            profile.MissionsCompleted = target.Seq - 1;
            bool before = wallet.IsAirframeAvailable(profile.Planes[2].Airframe, mod);
            profile.MissionsCompleted = target.Seq;
            bool at = wallet.IsAirframeAvailable(profile.Planes[2].Airframe, mod);
            ctx.Check(!before && at, $"the campaign hangar offers the mod from seq {target.Seq}, not before (before={before}, at={at})");

            ModPlanes.Current = ModPlanes.Open(Path.Combine(root, "none"));
            seated = CampaignDirector.ResolveSeatedPlane(spec);
            node = seated.PlaneNames.Count > 0 ? seated.PlaneNames[0] : "none";
            ctx.Check(node == Parent, $"without the mod the seat is the stored parent ({node})");
            store.Save(store.Load(CampaignPilot)!);
            ctx.Check(File.ReadAllBytes(path).SequenceEqual(saved) && File.ReadAllText(path).Contains($"\"modAirframe\": \"{CampaignModId}\""),
                $"a load and save without the mod leave the profile byte-identical, the mod id kept");
        }
        finally
        {
            ModPlanes.Current = previous;
            Directory.Delete(root, recursive: true);
        }
    }

    [Suite("net-mod-plane",
        "a network session over a plane mod: a host naming its mod set seats a guest on the mod, and a guest holding " +
        "the mod under the same hash joins, reads the mod's id as that seat's airframe, and both ends build the seat " +
        "from the mod's plane.glb; a guest holding the mod with another plane.json, so another hash, never joins and " +
        "names the mod, its lobby reads the host's set on connect, and the door's refusal names the mod to install")]
    internal static void NetModPlane(TestContext ctx)
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
        string root = Path.Combine(ctx.ScratchDir, "net-mod-plane");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        // The host's and the matched guest's copy, then a guest's copy whose plane.json differs.
        string stale = Path.Combine(root, "stale");
        foreach (string dataRoot in new[] { root, stale })
        {
            string folder = Path.Combine(ModPlanes.FolderIn(dataRoot), NetModId);
            Directory.CreateDirectory(folder);
            ctx.Same((long)Error.Ok, (long)WriteWreckFixture(folder, NetModId, ownWreck: false), $"the {NetModId} fixture's write result");
        }

        File.WriteAllText(Path.Combine(ModPlanes.FolderIn(stale), NetModId, ModPlanes.DefinitionFile),
            $$"""{ "kind_of": "{{Parent}}", "overrides": { "fd_speed": 99.5 } }""");
        try
        {
            var mods = ModPlanes.Open(root);
            var staleMods = ModPlanes.Open(stale);
            var mod = mods.Find(NetModId);
            ctx.Check(mod != null && staleMods.Find(NetModId) is { } other && other.Hash != mod.Hash,
                $"the mod loads from both folders under two hashes ({mod?.Hash[..12]}, {staleMods.Find(NetModId)?.Hash[..12]})");
            if (mod == null)
            {
                return;
            }

            ModAirframes.Add(planes, mod.Id, mod.KindOf, mod.Read(ModPlanes.ModelFile)!);
            var set = mods.Wire;
            var roster = new[]
            {
                new Net.NetSeat { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "host", PlaneNode = "player_fury" },
                new Net.NetSeat { PeerId = 1, SeatIndex = 1, Callsign = "guest", PlaneNode = NetModId },
                new Net.NetSeat { PeerId = 2, SeatIndex = 2, Callsign = "stale", PlaneNode = "player_fury" },
            };
            var mesh = Net.LoopbackTransport.Mesh(3, Net.LoopbackConditions.Perfect, new System.Random(34));
            var host = Net.NetSession.Host(mesh[0], roster, 0xD34UL, mods: set);
            var guest = Net.NetSession.Guest(mesh[1], set);
            var refused = Net.NetSession.Guest(mesh[2], staleMods.Wire);
            for (int i = 0; i < 8 && !guest.Joined; i++)
            {
                guest.Step(1.0 / 60.0);
                refused.Step(1.0 / 60.0);
            }

            string onHost = host.Seats[1].PlaneNode;
            string onGuest = guest.Joined ? guest.Seats[1].PlaneNode : "not joined";
            ctx.Check(guest.Joined && onHost == NetModId && onGuest == NetModId && guest.Seats[0].PlaneNode == "player_fury",
                $"the matched guest joins and both ends seat the guest on {NetModId} (host {onHost}, guest {onGuest})");
            foreach (var (end, node) in new[] { ("host", onHost), ("guest", onGuest) })
            {
                var model = Build(planes, textures, node, out var builder);
                try
                {
                    var drawn = Meshes(model, builder.CockpitInterior).ToList();
                    ctx.Check(drawn.Count > 0 && drawn.All(m => m.HasMeta(SceneBuilder.GamezMeshMeta)),
                        $"the {end}'s copy of the guest's seat builds from the mod's plane.glb ({drawn.Count(m => m.HasMeta(SceneBuilder.GamezMeshMeta))} of {drawn.Count} meshes)");
                }
                finally
                {
                    model.Free();
                }
            }

            string named = string.Join(", ", refused.RefusedMods.Select(m => m.Id));
            ctx.Check(!refused.Joined && named == NetModId, $"the guest holding another hash never joins and names {named}");

            // The lobby's half: a host's set reaches a guest on connect, and the door names the mod.
            var lobbyMesh = Net.LoopbackTransport.Mesh(2, Net.LoopbackConditions.Perfect, new System.Random(35));
            var hostLobby = new Net.NetLobby(lobbyMesh[0], mods: set);
            var guestLobby = new Net.NetLobby(lobbyMesh[1]);
            for (int i = 0; i < 4; i++)
            {
                hostLobby.Step(1.0 / 60.0);
                guestLobby.Step(1.0 / 60.0);
            }

            string notice = UI.Menu.CoopDoorText.ModsMissing(guestLobby.Mods.MissingFrom(staleMods.Wire));
            ctx.Check(guestLobby.Mods.RowOf(NetModId) == Net.NetModSet.StockRows && guestLobby.Mods.MissingFrom(set).Count == 0
                    && notice.Contains(NetModId, StringComparison.Ordinal),
                $"the guest's lobby holds the host's set (row {guestLobby.Mods.RowOf(NetModId)}) and the stale guest reads \"{notice}\"");
            hostLobby.Dispose();
            guestLobby.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Suite("mod-plane-art",
        "a plane mod's menu art: a Brigand export without a portrait renders a coloured three-quarter portrait " +
        "and black plan and head-on silhouettes once into the cache, named by its content hash, and binds them " +
        "from the cache after without rendering; a changed mod renders under a new name. A mod with a portrait " +
        "shows that image and renders only the silhouettes, a picture fits its slot with the aspect kept, and a " +
        "stock node has no art")]
    internal static void ModPlaneArtSuite(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planes = ctx.PlanesCopy();
        if (planes.FindByName(Parent) == null)
        {
            throw new SuiteSkippedException($"{Parent} is not in the aircraft archive");
        }

        string root = Path.Combine(ctx.ScratchDir, "mod-plane-art");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string cache = Path.Combine(root, "cache");
        string rendered = Path.Combine(ModPlanes.FolderIn(root), ArtMod);
        string pictured = Path.Combine(ModPlanes.FolderIn(root), PortraitMod);
        Directory.CreateDirectory(rendered);
        Directory.CreateDirectory(pictured);
        using (var textures = new TextureArchive(texturesPath))
        {
            var stock = Build(planes, textures, Parent, out _);
            ctx.Same((long)Error.Ok, (long)GltfExporter.Export(stock, Path.Combine(rendered, ModPlanes.ModelFile)), $"the {Parent} export write result");
            stock.Free();
        }

        File.Copy(Path.Combine(rendered, ModPlanes.ModelFile), Path.Combine(pictured, ModPlanes.ModelFile));
        File.WriteAllText(Path.Combine(rendered, ModPlanes.DefinitionFile), $$"""{ "kind_of": "{{Parent}}" }""");
        File.WriteAllText(Path.Combine(pictured, ModPlanes.DefinitionFile), $$"""{ "kind_of": "{{Parent}}", "portrait": "art/portrait.png" }""");
        Directory.CreateDirectory(Path.Combine(pictured, "art"));
        var portrait = Image.CreateEmpty(6, 4, false, Image.Format.Rgba8);
        portrait.Fill(Colors.Red);
        portrait.SavePng(Path.Combine(pictured, "art", "portrait.png"));

        var mods = ModPlanes.Open(root);
        foreach (var mod in mods.Planes)
        {
            ModAirframes.Add(planes, mod.Id, mod.KindOf, mod.Read(ModPlanes.ModelFile)!);
        }

        try
        {
            CheckArt(ctx, planes, texturesPath, mods, root, cache);
        }
        finally
        {
            ModPlaneArt.Bind(null, ModPlanes.Current, cache);
        }
    }

    private static void CheckAutoWreck(TestContext ctx, Node3D model, PlaneStats stats)
    {
        var parts = PlaneCollider.Build(model)?.Parts ?? Array.Empty<PlaneCollider.Part>();
        ctx.Check(parts.Count > 0, $"the fixture has {parts.Count} collider hulls");
        var cut = AutoWreck.Cut(model, parts);
        var destroyed = cut.Build();
        try
        {
            var healthyTris = new List<PlaneCollider.Triangle>();
            var healthy = CockpitTransplant.FindNamed(model, "healthy")!;
            PlaneCollider.Collect(healthy, NodeFrame.Under(healthy, model), healthyTris);
            int total = 0;
            var centres = new Vector3[AutoWreck.Zones.Length];
            var means = new float[AutoWreck.Zones.Length];
            for (int z = 0; z < AutoWreck.Zones.Length; z++)
            {
                string zone = AutoWreck.Zones[z];
                var piece = destroyed.GetNodeOrNull<Node3D>($"piece{z + 1}");
                var tris = new List<Vector3>();
                foreach (var mi in piece?.GetChildren().OfType<MeshInstance3D>() ?? Enumerable.Empty<MeshInstance3D>())
                {
                    var faces = mi.Mesh.GetFaces();
                    var xf = piece!.Transform * mi.Transform;
                    for (int i = 0; i + 2 < faces.Length; i += 3)
                    {
                        tris.Add(xf * ((faces[i] + faces[i + 1] + faces[i + 2]) / 3f));
                    }
                }

                total += tris.Count;
                centres[z] = piece?.Position ?? Vector3.Zero;
                means[z] = tris.Count > 0 ? tris.Average(c => c.Z) : 0f;
                int stray = tris.Count(c => AutoWreck.ZoneOf(model.Transform * c, parts) != z);
                bool side = zone switch
                {
                    "leftwing" => tris.All(c => c.X < 0f),
                    "rightwing" => tris.All(c => c.X > 0f),
                    _ => true,
                };
                ctx.Check(tris.Count > 0 && stray == 0 && side,
                    $"piece{z + 1} is the {zone}: {tris.Count} triangle(s), {stray} outside the zone, on its side={side}");
            }

            ctx.Same(healthyTris.Count, total, $"triangles in the pieces against the drawn airframe under healthy");
            ctx.Check(means[0] < means[1], $"the nose piece's triangles lie ahead of the tail's, mean z {means[0]} against {means[1]}");

            var ladder = stats.DestroyableParts.SelectMany(p => p.InjureAnims.Where(e => e.Anim.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase))
                .Select(e => (Node: "pdp" + e.Anim["pdpanel".Length..], Zone: Array.IndexOf(AutoWreck.Zones, p.Name)))).Distinct().ToList();
            var missing = ladder.Concat(AutoWreck.NamedAnchors.Select(a => (a.Node, Zone: Array.IndexOf(AutoWreck.Zones, a.Zone))))
                .Where(a => CockpitTransplant.FindNamed(model, a.Node) == null).Distinct().ToList();
            int added = cut.AddAnchors(stats.DestroyableParts);
            ctx.Check(ladder.Count > 0 && added == missing.Count && CockpitTransplant.FindNamed(model, "nose")?.GetChildCount() > 0,
                $"one anchor per missing name, {ladder.Count} pdpN on the parent's ladders among them, and the model's own nose kept added={added} missing={missing.Count}");
            foreach (var (node, zone) in missing)
            {
                var at = CockpitTransplant.FindNamed(model, node);
                var where = at != null ? NodeFrame.Under(at, model).Origin : Vector3.Inf;
                ctx.Check(zone >= 0 && Near(where, centres[zone]), $"{node} stands at the {AutoWreck.Zones[Math.Max(zone, 0)]} centre {centres[Math.Max(zone, 0)]}, at {where}");
            }
        }
        finally
        {
            destroyed.Free();
        }
    }

    // The wreck fixture: a fuselage, two wings outboard of the wing band and a fin, under the stock
    // zone groups. With ownWreck it also carries destroyed > piece1.
    private static Error WriteWreckFixture(string folder, string id, bool ownWreck)
    {
        File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile), $$"""{ "kind_of": "{{Parent}}" }""");
        var material = new StandardMaterial3D { ResourceName = "suite_skin" };
        var top = new Node3D { Name = id };
        var geometry = Child(top, top, new Node3D { Name = "geometry" });
        var nearest = Child(top, Child(top, geometry, new Node3D { Name = "healthy" }), new Node3D { Name = "nearest" });
        foreach (var (zone, size, centre) in new[]
        {
            ("nose", new Vector3(1f, 1f, 6f), Vector3.Zero),
            ("tail", new Vector3(0.1f, 1f, 1f), new Vector3(0f, 1f, 2.5f)),
            ("leftwing", new Vector3(3f, 0.2f, 1.5f), new Vector3(-3.5f, 0f, 0f)),
            ("rightwing", new Vector3(3f, 0.2f, 1.5f), new Vector3(3.5f, 0f, 0f)),
        })
        {
            Box(top, Child(top, nearest, new Node3D { Name = zone }), zone + "_mesh", size, centre, material);
        }

        if (ownWreck)
        {
            Box(top, Child(top, geometry, new Node3D { Name = "destroyed" }), "piece1", Vector3.One, Vector3.Zero, material);
        }

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

    private static Node3D Child(Node3D owner, Node3D parent, Node3D child)
    {
        parent.AddChild(child);
        child.Owner = owner;
        return child;
    }

    private static void Box(Node3D owner, Node3D parent, string name, Vector3 size, Vector3 centre, Material material)
    {
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, new BoxMesh { Size = size }.GetMeshArrays());
        mesh.SurfaceSetMaterial(0, material);
        Child(owner, parent, new MeshInstance3D { Name = name, Mesh = mesh, Position = centre });
    }

    private static void CheckArt(TestContext ctx, GameZ planes, TextureSet textures, ModPlanes mods, string root, string cache)
    {
        var art = mods.Find(ArtMod)!;
        var own = mods.Find(PortraitMod)!;
        var views = new[] { ModArtView.Portrait, ModArtView.Plan, ModArtView.HeadOn };
        ModPlaneArt.Bind(planes, mods, cache);
        ctx.Check(ModPlaneArt.For(ArtMod, ModArtView.Portrait) == null && ModPlaneArt.For(PortraitMod, ModArtView.Portrait) is { } early && early.GetWidth() == 6,
            $"before any render the bare mod has no art and the pictured mod already shows its portrait");
        ModPlaneArt.RenderPending(ctx.Host, () => textures);
        ctx.Check(ModPlaneArt.TakeLanded() && !ModPlaneArt.TakeLanded(), $"the renders landing are reported once");
        var files = views.Select(v => ModPlaneArt.CacheFile(cache, ArtMod, art.Hash, v)).ToList();
        ctx.Check(files.All(File.Exists), $"the three renders are cached under the mod's hash ({string.Join(", ", files.Select(Path.GetFileName))})");
        var face = ModPlaneArt.For(ArtMod, ModArtView.Portrait);
        var plan = ModPlaneArt.For(ArtMod, ModArtView.Plan);
        var head = ModPlaneArt.For(ArtMod, ModArtView.HeadOn);
        var (faceCovered, faceColoured) = Coverage(face);
        var (planCovered, planColoured) = Coverage(plan);
        var (headCovered, headColoured) = Coverage(head);
        ctx.Check(faceCovered > 0.1f && faceCovered < 0.95f && faceColoured > 0,
            $"the portrait is the plane in colour on a transparent ground covered={faceCovered:0.00} coloured={faceColoured}");
        ctx.Check(planCovered > 0.1f && headCovered > 0.05f && planColoured == 0 && headColoured == 0,
            $"the plan and head-on views are black silhouettes plan={planCovered:0.00} head={headCovered:0.00}");

        var written = files.Select(File.GetLastWriteTimeUtc).ToList();
        ModPlaneArt.Bind(planes, mods, cache);
        ModPlaneArt.RenderPending(ctx.Host, () => textures);
        ctx.Check(!ModPlaneArt.TakeLanded() && files.Select(File.GetLastWriteTimeUtc).SequenceEqual(written)
            && ModPlaneArt.For(ArtMod, ModArtView.Plan) is { } again && again.GetSize() == plan?.GetSize(),
            $"a second bind takes the cached files and renders nothing");

        ctx.Check(!File.Exists(ModPlaneArt.CacheFile(cache, PortraitMod, own.Hash, ModArtView.Portrait))
            && File.Exists(ModPlaneArt.CacheFile(cache, PortraitMod, own.Hash, ModArtView.Plan))
            && ModPlaneArt.For(PortraitMod, ModArtView.Portrait) is { } image && image.GetPixel(0, 0) == Colors.Red,
            $"the pictured mod shows its own image and renders only its silhouettes");
        ctx.Check(ModPlaneArt.For(Parent, ModArtView.Portrait) == null && ModPlaneArt.Picture(Parent, ModArtView.Plan, 0f, 0f, 80f, 80f) == null,
            $"a stock node has no mod art, so its screens keep the original's");
        var slot = ModPlaneArt.Picture(ArtMod, ModArtView.Plan, 10f, 20f, 80f, 80f);
        ctx.Check(slot is { Art.Library: BoardArtLibrary.Held } && plan != null && slot.Width <= 80.01f && slot.Height <= 80.01f
            && Math.Abs((slot.Width / slot.Height) - (plan.GetWidth() / (float)plan.GetHeight())) < 0.01f
            && Math.Abs(slot.X + (slot.Width / 2f) - 50f) < 0.01f && Math.Abs(slot.Y + (slot.Height / 2f) - 60f) < 0.01f,
            $"the plan fits an 80 px slot centred with its aspect kept {slot?.X:0.0},{slot?.Y:0.0} {slot?.Width:0.0}x{slot?.Height:0.0}");

        File.WriteAllText(Path.Combine(ModPlanes.FolderIn(root), ArtMod, ModPlanes.DefinitionFile), $$"""{ "kind_of": "{{Parent}}", "_note": "changed" }""");
        var reopened = ModPlanes.Open(root);
        var changed = reopened.Find(ArtMod)!;
        ModPlaneArt.Bind(planes, reopened, cache);
        ModPlaneArt.RenderPending(ctx.Host, () => textures);
        ctx.Check(changed.Hash != art.Hash && File.Exists(ModPlaneArt.CacheFile(cache, ArtMod, changed.Hash, ModArtView.Portrait)),
            $"a changed mod renders again under its new hash {changed.Hash[..16]} (was {art.Hash[..16]})");
    }

    // The share of the image's pixels the plane covers, and how many of those carry colour.
    private static (float Covered, int Coloured) Coverage(Image? image)
    {
        if (image == null)
        {
            return (0f, 0);
        }

        int covered = 0;
        int coloured = 0;
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                var c = image.GetPixel(x, y);
                if (c.A > 0.5f)
                {
                    covered++;
                    coloured += c.R + c.G + c.B > 0.05f ? 1 : 0;
                }
            }
        }

        return (covered / (float)(image.GetWidth() * image.GetHeight()), coloured);
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

    // One cockpit mod's flat and stereo builds against the interior it should carry.
    private static void CheckCockpit(TestContext ctx, GameZ planes, TextureArchive textures, string id, string donor, bool fromFile,
        (int Triangles, string Gauges, int Plate) want, Vector3 parentEye)
    {
        var model = Build(planes, textures, id, out var builder);
        try
        {
            var interior = builder.CockpitInterior;
            ctx.Check(interior != null && Near(builder.CockpitCameraOffset, parentEye)
                    && interior.Transform.IsEqualApprox(PlaneBuilder.FlatMount(builder.CockpitCameraOffset)),
                $"{id}: an interior is mounted at the mod's cockpit_camera {builder.CockpitCameraOffset}");
            var drawn = interior == null ? new List<MeshInstance3D>() : Meshes(interior, null).ToList();
            int own = drawn.Count(m => m.HasMeta(SceneBuilder.GamezMeshMeta));
            string other = string.Join(",", drawn.Where(m => m.HasMeta(SceneBuilder.GamezMeshMeta) != fromFile).Take(12).Select(m => AnimRuntime.NameOf(m.GetParent<Node3D>())));
            ctx.Check(drawn.Count > 0 && own == (fromFile ? drawn.Count : 0),
                $"{id}: {(fromFile ? "every" : "no")} interior mesh is drawn from the mod's file from_file={own} drawn={drawn.Count} other:{other}");
            int triangles = Triangles(interior);
            string gauges = GaugeLine(builder);
            ctx.Check(triangles == want.Triangles && gauges == want.Gauges,
                $"{id}: the interior is {donor}'s, {triangles} visible triangles (want {want.Triangles}), gauges {gauges} (want {want.Gauges})");
        }
        finally
        {
            model.Free();
        }

        var stereo = new PlaneBuilder(planes, textures, spinningProps: true, cockpitInterior: true, xrInterior: true);
        var stereoModel = stereo.Build(id);
        try
        {
            int plate = fromFile ? stereo.ReplacedPlateTriangles : stereo.InteriorOmittedPolygons;
            ctx.Check(stereo.CockpitInterior != null && want.Plate > 0 && plate == want.Plate,
                $"{id}: the stereo interior {(fromFile ? "cuts the file's head plate triangles" : "omits the head plate polygons")} {plate} (want {want.Plate})");
        }
        finally
        {
            stereoModel.Free();
        }
    }

    // A cockpit mod's folder: a Brigand, naming `cockpit` when given; the caller writes its plane.glb.
    private static string WriteCockpitMod(string root, string id, string? cockpit)
    {
        string folder = Path.Combine(ModPlanes.FolderIn(root), id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile),
            cockpit == null ? $$"""{ "kind_of": "{{Parent}}" }""" : $$"""{ "kind_of": "{{Parent}}", "cockpit": "{{cockpit}}" }""");
        return folder;
    }

    // The head plate of `plane`'s cockpit1 in the archive.
    private static HashSet<GameZPolygon> PlatePolygons(GameZ planes, string plane) =>
        planes.ChildrenOf(planes.FindByName(plane)!).Find(n => n.Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase)) is { } interior
            ? CockpitPlate.Find(planes, interior) : new HashSet<GameZPolygon>();

    private static string GaugeLine(PlaneBuilder builder) => CockpitGauges.Bind(builder) is { } g
        ? $"needles={g.NeedleCount} lamps={g.LampCount} readouts={g.ReadoutCount} belts={g.BeltCount} zones={g.DamageZoneCount}" : "none";

    // The triangles the interior draws once shown: every mesh under it whose nodes below the interior are visible.
    private static int Triangles(Node3D? interior)
    {
        int Count(Node node) => (node is MeshInstance3D { Mesh: ArrayMesh mesh } mi && mi.Name.ToString() == "mesh"
                ? Enumerable.Range(0, mesh.GetSurfaceCount()).Sum(s => mesh.SurfaceGetArrayIndexLen(s) is > 0 and var n ? n / 3 : mesh.SurfaceGetArrayLen(s) / 3)
                : 0)
            + node.GetChildren().Where(c => c is not Node3D { Visible: false }).Sum(Count);
        return interior == null ? 0 : Count(interior);
    }

    // The seats BotSeats makes of the mod, named or Random, in any field: every guest holds the host's mods.
    private static void CheckBotSeats(TestContext ctx, AirframeRegistry airframes)
    {
        var flying = BotSeats.Pairs.Where(p => p.Plane == Parent).Select(p => p.Militia).ToHashSet();
        var none = Array.Empty<string>();
        var named = BotSeats.Resolve(Enumerable.Repeat(new VsBotEntry(ModId, Net.NetBotSkill.Ace, 0, "x"), 50).ToArray(), none, none,
            new System.Random(3), airframes: airframes);
        ctx.Check(named.All(b => b.Plane == ModId && flying.Contains(b.Militia)),
            $"a named bot seats the mod on a militia that flies {Parent} ({string.Join(",", named.Select(b => b.Plane).Distinct())}, militias {string.Join(",", named.Select(b => b.Militia).Distinct())})");

        var randoms = Enumerable.Repeat(new VsBotEntry(null, Net.NetBotSkill.Veteran, 0, "x"), 400).ToArray();
        int drawn = BotSeats.Resolve(randoms, none, none, new System.Random(5), airframes: airframes).Count(b => b.Plane == ModId);
        int stock = BotSeats.Resolve(randoms, none, none, new System.Random(5), airframes: new AirframeRegistry()).Count(b => b.Plane == ModId);
        ctx.Check(drawn > 0 && stock == 0,
            $"Random draws the mod ({drawn} of {randoms.Length}), and never from a registry without it ({stock})");
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
