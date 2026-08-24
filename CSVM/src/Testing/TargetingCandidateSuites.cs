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
            // parked so the gunner's intercept sees a static target, on WorldTeam — hostile to every
            // real team (AimAssist.WorldTeam), exactly the aim assist's own structure candidates.
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

            // --- same-team structure: refused outright (the decoded team gate, unchanged by D36).
            ai.Team = AimAssist.WorldTeam;
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(gunner.GroundTarget == null && gunner.Target == null,
                $"a same-team structure is never a candidate (WorldTeam vs WorldTeam)");

            // --- widened acquisition: a real team now sees the structure with no aircraft in the
            // scan at all — BL-363's TargetStruct pool, previously unreachable.
            ai.Team = AimAssist.PlayerTeam + 1; // any real team distinct from WorldTeam/Neutral
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.GroundTarget, gasbagInst),
                $"the widened acquisition (D36, BL-363) routes a zeppelin structure into GroundTarget");
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
}
