using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>D36's widened AI acquisition (<c>BL-363</c>): the decoded candidate pool covers
/// aircraft, turrets and structures, not aircraft alone, so an escort/defend AI can be ordered
/// against a zeppelin part and actually engage it. Inventory: docs/architecture.md.</summary>
internal static class TargetingCandidateSuites
{
    // A stock-armed AI-piloted rig HELD dead-on at a registered destructible (a zeppelin gasbag's
    // own shape: DestructibleRegistry.Instance, D36's TargetStruct pool) with no aircraft in the
    // scan at all. Proves the widened SelectRankedTarget routes a non-aircraft winner into
    // AiGunner.GroundTarget (never Target, so AiPilot's flight law is untouched) and that the
    // gunner then fires real rounds at it; and that a same-team structure is refused.
    internal static void TargetingCandidates(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        LoadoutDef? stock = null;
        foreach (var def in StockLoadouts.Load().All.Values)
        {
            if (def.Model == ctx.PlaneName)
            {
                stock = def;
                break;
            }
        }
        ctx.Check(stock != null, $"stock loadout found for plane={ctx.PlaneName}");
        if (stock == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai = null;
        Node3D? gasbagNode = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The registered structure: a zeppelin gasbag's own shape (docs/formats/destructibles.md),
            // parked so the gunner's intercept sees a static target. Its team is left unauthored
            // here and authored below, which is the whole of the fall-through arm.
            var structurePos = new Vector3(0f, 500f, 0f);
            gasbagNode = new Node3D { Name = "gasbag1", Position = structurePos };
            ctx.Host.AddChild(gasbagNode);
            var registry = new DestructibleRegistry();
            var gasbagInst = registry.Register(
                new AnimDefinition { Name = "gasbag1", AnimName = "zep_zone_gasbag1" }, gasbagNode, 200f);

            var gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260824 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9),
            };
            var aiPos = structurePos + new Vector3(0f, 0f, 400f); // dead ahead of the gasbag's nose
            var pilot = AiPilot.HoldingCourse(aiPos, structurePos);
            pilot.Gunner = gunner;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                Destructibles = registry,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.Bind(stock, aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, structurePos);
            ctx.Host.AddChild(ai);
            ai.Held = true;
            ai.PlaceHeld(aiPos, structurePos);

            var gun = ai.Loadout.FirableGuns.First();
            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    ai!.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }

            // --- unauthored structure: refused by every team, because the fall-through is neutral
            // and neutral is symmetric and total (docs/org/targeting.md "The hostility predicate").
            // This is the arm that would pass on a hostile fall-through, so it is what BL-407 moved.
            int hostileTeam = AimAssist.PlayerTeam + 1; // any real team, distinct from the player's
            ai.Team = hostileTeam;
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(gunner.GroundTarget == null && gunner.Target == null,
                $"a structure whose pool authors no team is nobody's target (the neutral fall-through)");

            // --- same-team structure: refused outright (the decoded team gate, unchanged by D36).
            gasbagInst.Team = hostileTeam;
            ai.Team = hostileTeam;
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(gunner.GroundTarget == null && gunner.Target == null,
                $"a structure on the shooter's own authored team is never a candidate");

            // --- the owning-zeppelin identity (BL-476): a zone answers to its anchor name AND to
            // the hull that owns it, so an authored -1.0 on the HULL excludes the gasbag. Both arms
            // run from an idle gunner, because a standing pick is sticky and would answer for it.
            gasbagInst.Team = hostileTeam + 1; // an authored team the shooter is hostile to
            gasbagInst.Owner = "piratezep";
            gunner.RatingBiases = new[] { new AiRatingBias("piratezep", -1f, null) };
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(gunner.GroundTarget == null,
                $"an authored -1.0 naming the OWNING zeppelin excludes its zone, which the zone's own name 'gasbag1' could never match");

            // --- widened acquisition: a real team now sees the structure with no aircraft in the
            // scan at all — BL-363's TargetStruct pool, previously unreachable. Doubling as the
            // exclusion's control: the same -1.0 naming a hull this zone does not belong to.
            gunner.RatingBiases = new[] { new AiRatingBias("beowulfzep", -1f, null) };
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.GroundTarget, gasbagInst),
                $"the widened acquisition (D36, BL-363) routes a zeppelin structure into GroundTarget, and an exclusion naming a different hull does not block it");
            ctx.Check(gunner.Target == null,
                $"a non-aircraft winner never touches AiGunner.Target — AiPilot's flight law sees nothing new");

            // --- engagement: the gunner actually fires real rounds at it (the plan's own verify line).
            int ammoBefore = gun.Ammo;
            for (int guard = 0; ammoBefore - gun.Ammo == 0 && guard < 120; guard++)
                Step(1);
            ctx.Check(ammoBefore - gun.Ammo > 0,
                $"an AI ordered against a zeppelin structure engages it: rounds fired={ammoBefore - gun.Ammo}");
        }
        finally
        {
            pool?.Free();
            ai?.Free();
            gasbagNode?.Free();
            textures.Dispose();
        }
    }

    // AddRankedNonAircraft walked every turret with no discriminator on TurretController.Site, so
    // a carried gunner rode the ranked pool as a second entry beside its own aircraft's Vehicle
    // entry, one silhouette read as two candidates. Mirrors the guard TargetPool.Offer already
    // applies for the player (TargetPool.IsEmplacement), now shared by both pools.
    internal static void RankedPoolCarriedTurretDedup(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);

        // The Kestrel: one thirdp rear mount (see the carried-turrets suite for the decoded arc).
        const string HostPlane = "player_kestrel";
        var hostStats = PlaneStats.Load(ctx.ZrdrPath, HostPlane);
        var aiStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? carrier = null;
        FlightController? ai = null;
        Node3D? siteNode = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(string plane, PlaneStats st, int playerIndex, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(plane);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(st.DestroyableParts),
                    PlayerIndex = playerIndex,
                    Projectiles = live,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(st), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                rig.PlaceHeld(pos, pos + Vector3.Forward);
                return rig;
            }

            var carrierPos = new Vector3(0f, 500f, 0f);
            carrier = BuildRig(HostPlane, hostStats, 0, carrierPos);
            carrier.IsHumanPiloted = false;
            carrier.Team = TurretDef.DefaultTeamId;
            carrier.Turrets = TurretController.BuildCarried(
                turretDefs, hostStats, carrier.PlaneModel!, weapons, carrier, live);
            ctx.Check(carrier.Turrets.Length == 1,
                $"the carrier's crewed mount resolves turrets={carrier.Turrets.Length}");
            if (carrier.Turrets.Length != 1)
                return;
            var carriedTurret = carrier.Turrets[0];

            // A hand-placed emplacement: an authored standalone entry, its NODES/PARTS chain
            // repointed at nodes built here rather than a real chapter's placement, since this
            // suite proves the discriminator, not the census (world-turrets' own job).
            siteNode = new Node3D { Name = "test_emplacement", Position = new Vector3(400f, 500f, 0f) };
            ctx.Host.AddChild(siteNode);
            var pitchNode = new Node3D();
            pitchNode.SetMeta(AnimRuntime.NameMeta, "pitch");
            siteNode.AddChild(pitchNode);
            var muzzleNode = new Node3D();
            muzzleNode.SetMeta(AnimRuntime.NameMeta, "muzzle");
            siteNode.AddChild(muzzleNode);
            var emplacementDef = turretDefs.All.First(d => !d.Carried);
            emplacementDef.YawNode = null;
            emplacementDef.PitchNode = "pitch";
            emplacementDef.Firepoints = new[] { "muzzle" };
            emplacementDef.WeaponName = carriedTurret.Def.WeaponName;
            emplacementDef.HealthyNode = null;
            emplacementDef.Team = null; // TurretDefs.DefaultTeamId, the same enemy default the carrier took
            emplacementDef.NodePatterns = new List<IReadOnlyList<string>>
            {
                new List<string> { "test_emplacement" },
            };
            var emplacements = TurretController.BuildEmplacements(turretDefs, weapons,
                (name, _) => name == "test_emplacement"
                    ? new[] { siteNode }
                    : System.Array.Empty<Node3D>(),
                live);
            ctx.Check(emplacements.Length == 1 && emplacements[0].Site != null,
                $"the hand-placed emplacement resolves turrets={emplacements.Length}");
            if (emplacements.Length != 1)
                return;
            live.RegisterWorldTurrets(emplacements);
            var emplacementTurret = emplacements[0];

            // The assessing AI: hostile to both, never itself a candidate.
            var aiPos = carrierPos + new Vector3(0f, 0f, 400f);
            ai = BuildRig(ctx.PlaneName, aiStats, FlightRoster.ShooterIdBase, aiPos);
            ai.IsHumanPiloted = false;
            ai.Team = AimAssist.PlayerTeam;
            var gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260826 });

            var sources = ai.RankedPoolSourcesForTest(gunner);
            int carrierEntries = sources.Count(s => ReferenceEquals(s, carrier));
            int carriedTurretEntries = sources.Count(s => ReferenceEquals(s, carriedTurret));
            int emplacementEntries = sources.Count(s => ReferenceEquals(s, emplacementTurret));
            ctx.Check(carrierEntries == 1,
                $"the carrier's own aircraft rides the ranked pool as a Vehicle entry entries={carrierEntries}");
            ctx.Check(carriedTurretEntries == 0,
                $"a carried turret is not offered as a second entry beside its own aircraft entries={carriedTurretEntries}");
            ctx.Check(emplacementEntries == 1,
                $"a world emplacement still reaches the ranked pool entries={emplacementEntries}");
        }
        finally
        {
            pool?.Free();
            carrier?.Free();
            ai?.Free();
            siteNode?.Free();
            textures.Dispose();
        }
    }
}
