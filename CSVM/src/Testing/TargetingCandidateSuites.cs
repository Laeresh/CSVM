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
    // A stock-armed AI-piloted rig HELD dead-on at a registered destructible (a zeppelin engine's
    // own shape: DestructibleRegistry.Instance, the TargetStruct pool) with no aircraft in the
    // scan at all. Proves SelectRankedTarget makes a non-aircraft winner the gunner's one
    // standing target and that the gunner then fires real rounds at it; and that a same-team
    // structure is refused.
    [Suite("targeting-candidates",
        "the widened AI acquisition (BL-363): a registered structure whose pool authors no " +
        "team is nobody's target and a same-team one is refused, while " +
        "a real team's AI takes a winning structure candidate as its one standing target " +
        "and the gunner fires real rounds at a zeppelin structure with no aircraft in the scan " +
        "at all")]
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

            // The registered structure: a zeppelin engine's own shape, parked so the intercept sees
            // a static target, and not a gasbag, which a gun-only pilot is never offered. Its team
            // is left unauthored here and authored below, which is the whole fall-through arm.
            var structurePos = new Vector3(0f, 500f, 0f);
            gasbagNode = new Node3D { Name = "leng11", Position = structurePos };
            ctx.Host.AddChild(gasbagNode);
            var registry = new DestructibleRegistry();
            var gasbagInst = registry.Register(
                new AnimDefinition { Name = "leng11", AnimName = "zep_zone_leng11" }, gasbagNode, 200f);

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
            ctx.Check(gunner.Target == null,
                $"a structure whose pool authors no team is nobody's target (the neutral fall-through)");

            // --- same-team structure: refused outright (the decoded team gate, unchanged by D36).
            gasbagInst.Team = hostileTeam;
            ai.Team = hostileTeam;
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(gunner.Target == null,
                $"a structure on the shooter's own authored team is never a candidate");

            // --- the owning-zeppelin identity (BL-476): a zone answers to its anchor name AND to
            // the hull that owns it, so an authored -1.0 on the HULL excludes the gasbag. Both arms
            // run from an idle gunner, because a standing pick is sticky and would answer for it.
            gasbagInst.Team = hostileTeam + 1; // an authored team the shooter is hostile to
            gasbagInst.Owner = "piratezep";
            gunner.RatingBiases = new[] { new AiRatingBias("piratezep", -1f, null) };
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(gunner.Target == null,
                $"an authored -1.0 naming the OWNING zeppelin excludes its zone, which the zone's own name 'leng11' could never match");

            // --- widened acquisition: a real team now sees the structure with no aircraft in the
            // scan at all, BL-363's TargetStruct pool, previously unreachable. Doubling as the
            // exclusion's control: the same -1.0 naming a hull this zone does not belong to.
            gunner.RatingBiases = new[] { new AiRatingBias("beowulfzep", -1f, null) };
            gunner.AutoTarget = true;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, gasbagInst),
                $"the widened acquisition (BL-363) takes a zeppelin structure as the standing target, and an exclusion naming a different hull does not block it");

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

    // A pilot with the mode machine, holding a course past a hostile zeppelin engine 1500 m off,
    // with nothing else in the scan. The sweep takes the engine as its standing target, the
    // machine promotes to pursue on it, and the pursue arm flies the aeroplane AT the part: the
    // range closes and the guns open. This is what an Instant Action wingman does on CM02 in the
    // original, and what BL-740 found ours never did.
    [Suite("ai-pursues-structure",
        "an AI pilot pursues a zeppelin part (BL-740): with a hostile engine inside the " +
        "activation radius and no aircraft in the scan, the mode machine leaves patrol for pursue " +
        "on the structure, the flight law closes the range on it and the gunner opens fire")]
    internal static void AiPursuesStructure(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var stock = StockLoadouts.Load().All.Values.First(d => d.Model == ctx.PlaneName);

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai = null;
        Node3D? engineNode = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            int hostileTeam = AimAssist.PlayerTeam + 1;
            var enginePos = new Vector3(0f, 500f, 0f);
            engineNode = new Node3D { Name = "leng11", Position = enginePos };
            ctx.Host.AddChild(engineNode);
            var registry = new DestructibleRegistry();
            var engine = registry.Register(
                new AnimDefinition { Name = "leng11", AnimName = "zep_zone_leng11" }, engineNode, 200f);
            engine.Team = hostileTeam + 1;
            engine.Owner = "piratezep";

            // Abeam of the engine and flying past it, so closing the range is the pursue arm's
            // doing and not the entry course's.
            var aiPos = enginePos + new Vector3(1500f, 0f, 0f);
            var pilot = AiPilot.HoldingCourse(aiPos, aiPos + Vector3.Forward);
            pilot.Gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260905 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9),
            };
            var machine = new AiModeMachine(new System.Random(7))
            {
                ActivationRange = skills.MinAiActiveDist,
                AttackRange = stats.AiAttackRange,
                ReturnRange = stats.AiReturnRange,
                SteadyHandChance = 0f,
                ProbeBlocked = (_, _) => null,
            };
            pilot.Machine = machine;
            var transitions = new List<string>();
            machine.ModeChanged += (from, to, _) =>
                transitions.Add($"{AiModeMachine.NameOf(from)}>{AiModeMachine.NameOf(to)}");

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
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, aiPos + Vector3.Forward);
            ctx.Host.AddChild(ai);
            ai.Team = hostileTeam;

            var gun = ai.Loadout.FirableGuns.First();
            int ammoAtStart = gun.Ammo;
            float Dist() => ai!.WorldPosition.DistanceTo(enginePos);
            float startRange = Dist();
            float closest = startRange;
            bool fired = false;
            int pursueSteps = 0;
            for (int i = 0; i < 60 * 60; i++)
            {
                ai.SimStep(1f / 60f);
                live.SimStep(1f / 60f);
                closest = Mathf.Min(closest, Dist());
                fired |= gun.Ammo < ammoAtStart;
                if (machine.Mode == AiMode.Pursue)
                    pursueSteps++;
                if (i % 600 == 0)
                {
                    ctx.Note($"[pursue] t={i / 60}s mode={AiModeMachine.NameOf(machine.Mode)} target={TargetPool.NameOf(pilot.Gunner.Target)} range={Dist():0} m rounds={ammoAtStart - gun.Ammo}");
                }
            }

            ctx.Check(ReferenceEquals(pilot.Gunner.Target, engine) || fired,
                $"the sweep takes the engine as the standing target target={TargetPool.NameOf(pilot.Gunner.Target)}");
            ctx.Check(transitions.Contains("patrol>pursue"),
                $"the machine promotes to pursue on a structure transitions=[{string.Join(" ", transitions)}]");
            ctx.Check(pursueSteps > 60 * 10,
                $"…and stays there: pursue held {pursueSteps / 60f:0.0} s of 60");
            ctx.Check(closest < startRange * 0.5f,
                $"the pursue arm closes the range on the part: start={startRange:0} m closest={closest:0} m");
            ctx.Check(fired, $"the gunner opens fire on it rounds={ammoAtStart - gun.Ammo}");
        }
        finally
        {
            pool?.Free();
            ai?.Free();
            engineNode?.Free();
            textures.Dispose();
        }
    }

    // The gasbag admission gate: a gasbag is offered only to a pilot whose DAMAGES_ZEPPELIN
    // ordnance can launch now, and then carries the -0.5 in its favour; a gun-only pilot never
    // sees it and takes the engine beside it. Black Hat's Warhawk, eight torpedoes and a gun,
    // is the shipped case, and its rocketeer launches at the gasbag it picked.
    [Suite("gasbag-ordnance-gate",
        "the gasbag admission gate (BL-740): a gun-only AI beside a nearer hostile gasbag " +
        "takes the engine and never the gasbag, a torpedo-armed Warhawk takes the gasbag and " +
        "its rocketeer selects the torpedo pylon against it, and a Warhawk whose torpedoes are " +
        "spent is back to the engine")]
    internal static void GasbagOrdnanceGate(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var gunStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var stock = StockLoadouts.Load().All.Values.First(d => d.Model == ctx.PlaneName);
        var warhawkStats = PlaneStats.LoadForAi(ctx.ZrdrPath, "player_warhawk", "bhatwarhawk");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? gunOnly = null;
        FlightController? warhawk = null;
        Node3D? gasbagNode = null;
        Node3D? engineNode = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            int hostileTeam = AimAssist.PlayerTeam + 1;
            int zepTeam = hostileTeam + 1;
            var registry = new DestructibleRegistry();
            // The gasbag is the NEARER part, so distance alone would hand it to anyone.
            var gasbagPos = new Vector3(0f, 500f, 0f);
            gasbagNode = new Node3D { Name = "gasbag1", Position = gasbagPos };
            ctx.Host.AddChild(gasbagNode);
            var gasbag = registry.Register(
                new AnimDefinition { Name = "gasbag1", AnimName = "zep_zone_gasbag1" }, gasbagNode, 240f);
            gasbag.Team = zepTeam;
            gasbag.Owner = "piratezep";
            gasbag.Gasbag = true;
            var enginePos = new Vector3(0f, 500f, -120f);
            engineNode = new Node3D { Name = "leng11", Position = enginePos };
            ctx.Host.AddChild(engineNode);
            var engine = registry.Register(
                new AnimDefinition { Name = "leng11", AnimName = "zep_zone_leng11" }, engineNode, 200f);
            engine.Team = zepTeam;
            engine.Owner = "piratezep";

            FlightController Rig(Node3D model, PlaneStats st, Loadout loadout, Vector3 pos, AiPilot pilot)
            {
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(st.DestroyableParts),
                    PlayerIndex = FlightRoster.ShooterIdBase,
                    IsHumanPiloted = false,
                    Pilot = pilot,
                    Projectiles = live,
                    Destructibles = registry,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Loadout = loadout;
                rig.Setup(new FlightModel(st), null, new CamParams(), pos, gasbagPos);
                ctx.Host.AddChild(rig);
                rig.Held = true;
                rig.PlaceHeld(pos, gasbagPos);
                rig.Team = hostileTeam;
                return rig;
            }

            AiGunner Gunner() => new(new RandomNumberGenerator { Seed = 20260905 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9),
            };

            // --- the gun-only pilot: dead ahead of the gasbag, 400 m out, engine 120 m past it.
            var gunPos = gasbagPos + new Vector3(0f, 0f, 400f);
            var gunPilot = AiPilot.HoldingCourse(gunPos, gasbagPos);
            gunPilot.Gunner = Gunner();
            var gunModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            gunOnly = Rig(gunModel, gunStats, Loadout.Bind(stock, gunModel, weapons), gunPos, gunPilot);
            bool gunOnlyHasTorpedo = gunOnly.Loadout!.Hardpoints.Any(h => h.Weapon.DamagesZeppelin);
            ctx.Check(!gunOnlyHasTorpedo,
                $"the control fit carries no DAMAGES_ZEPPELIN ordnance (plane={ctx.PlaneName})");
            gunOnly.SimStep(1f / 60f);
            ctx.Check(ReferenceEquals(gunPilot.Gunner.Target, engine),
                $"a pilot without gasbag ordnance is never offered the nearer gasbag and takes the engine target={TargetPool.NameOf(gunPilot.Gunner.Target)}");

            // --- the Warhawk: the same station, torpedoes live.
            var whPos = gasbagPos + new Vector3(60f, 0f, 400f);
            var whPilot = AiPilot.HoldingCourse(whPos, gasbagPos);
            whPilot.Gunner = Gunner();
            whPilot.Rocketeer = new AiRocketeer(() => 0f)
            {
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9),
                QuickDrawChance = 1f,
            };
            var whModel = new PlaneBuilder(planesGamez, textures).Build(warhawkStats.NodeName);
            var whLoadout = Loadout.BindAi(warhawkStats.AiWeapons, "bhatwarhawk", whModel, weapons);
            warhawk = Rig(whModel, warhawkStats, whLoadout, whPos, whPilot);
            var torpedoPylon = whLoadout.Hardpoints.First(h => h.Weapon.DamagesZeppelin);
            int torpedoesBefore = torpedoPylon.Ammo;
            warhawk.SimStep(1f / 60f);
            ctx.Check(ReferenceEquals(whPilot.Gunner.Target, gasbag),
                $"a torpedo-armed pilot is offered the gasbag and the -0.5 hands it the pick target={TargetPool.NameOf(whPilot.Gunner.Target)}");
            // The selection is read the tick it happens and the launch it becomes, since a pylon
            // fired on the very first step is already behind its lockout by the next one.
            bool Selected() => whPilot.Rocketeer.SelectedPylon >= 0
                && whLoadout.Hardpoints[whPilot.Rocketeer.SelectedPylon].Weapon.DamagesZeppelin;
            bool Launched() => torpedoPylon.Ammo < torpedoesBefore;
            bool selected = Selected() || Launched();
            for (int i = 0; i < 120 && !selected; i++)
            {
                warhawk.SimStep(1f / 60f);
                live.SimStep(1f / 60f);
                selected = Selected() || Launched();
            }
            ctx.Check(selected,
                $"…and the rocketeer selects the torpedo pylon against it (selected={whPilot.Rocketeer.SelectedPylon}, torpedoes {torpedoesBefore}->{torpedoPylon.Ammo}, lockout={whPilot.Rocketeer.LockoutRemaining:0.0} s)");

            // --- torpedoes spent: the gate closes and a fresh acquisition is the engine again.
            torpedoPylon.Ammo = 0;
            whPilot.Gunner.Target = null;
            warhawk.SimStep(1f / 60f);
            ctx.Check(ReferenceEquals(whPilot.Gunner.Target, engine),
                $"with the torpedoes spent the gasbag is withdrawn and the engine is the pick target={TargetPool.NameOf(whPilot.Gunner.Target)}");
        }
        finally
        {
            pool?.Free();
            gunOnly?.Free();
            warhawk?.Free();
            gasbagNode?.Free();
            engineNode?.Free();
            textures.Dispose();
        }
    }

    // AddRankedNonAircraft walked every turret with no discriminator on TurretController.Site, so
    // a carried gunner rode the ranked pool as a second entry beside its own aircraft's Vehicle
    // entry, one silhouette read as two candidates. Mirrors the guard TargetPool.Offer already
    // applies for the player (TargetPool.IsEmplacement), now shared by both pools.
    [Suite("ranked-pool-carried-turret-dedup",
        "the AI ranked pool's carried-turret guard (BL-507): a hostile aircraft with a crewed " +
        "rear mount rides the pool as one Vehicle entry, never a second entry for its own " +
        "turret, while a world emplacement in the same scene still reaches the pool")]
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
