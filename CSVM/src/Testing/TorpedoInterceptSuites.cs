using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.InstantAction;
using CSVM.Session.Roster;
using Godot;

namespace CSVM.Testing;

/// <summary>The fourth candidate list on the AI side. A <c>TARGETABLE</c> round in flight is a
/// target for a world emplacement and for an AI pilot, and their rounds can shoot it down.
/// Decode: docs/org/aiPilot.md "Target acquisition", docs/org/targeting.md "The picker's cost".</summary>
internal static class TorpedoInterceptSuites
{
    private const float Dt = 1f / 60f;
    private const float PassLead = 250f;     // m past RANGE_MINIMUM the gun stands along the line
    private const float PassAbeam = 40f;     // m the line passes the gun by
    private const float PilotLead = 350f;    // m past RANGE_MINIMUM the pilot is held, nose down the line
    private const float PilotAbove = 25f;    // m above the line, clear of the torpedo's own fuse
    private static readonly Vector3 Origin = new(6000f, 3000f, 0f);
    private static readonly Vector3 Line = Vector3.Forward;

    [Suite("torpedo-intercept",
        "an AI turret and an AI pilot take a torpedo in flight as a target and shoot it down: a " +
        "world emplacement beside wep_14's line acquires it and spends its FLYOUT_HEALTH, a held AI " +
        "pilot on the line does the same, and a wep_06 rocket down the same line is taken by " +
        "neither; a round TARGETABLE without FLYOUT_HEALTH is acquired, one with FLYOUT_HEALTH " +
        "alone is not, and a zeppelin's gun takes the torpedo over a structure 220 m nearer " +
        "where an ordinary emplacement takes the structure")]
    internal static void TorpedoIntercept(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        if (OrdnanceSuites.PickWeapon(ctx, weapons, "wep_14", w => w.Torpedo && w.Targetable) is not { } torpedo
            || OrdnanceSuites.PickWeapon(ctx, weapons, "wep_06", w => w.IsRocket && !w.Targetable) is not { } rocket)
        {
            ctx.Check(false, $"wep_14 and wep_06 both resolve");
            return;
        }

        var gunDef = turretDefs.All.FirstOrDefault(d => !d.Carried && d.DetectionRange >= 400f
            && weapons.Get(d.WeaponName) is { IsGun: true, HealthDamage: > 0f });
        ctx.Check(gunDef != null, $"a standalone turret entry with a damaging gun and 400 m of DETECTION_RANGE exists");
        if (gunDef == null)
        {
            return;
        }

        float reveal = torpedo.RangeMinimum ?? 0f;
        EffectStageSuiteHelper.WithAnimSource(ctx, "C1", source =>
        {
            var textures = new TextureArchive(texturesPath);
            ProjectilePool? live = null;
            Node3D? site = null;
            try
            {
                live = new ProjectilePool(textures, null, null,
                    flyoutGamez: source.Gamez, flyoutScene: source.Scene, flyoutAnims: source.Program);
                ctx.Host.AddChild(live);
                var gunAt = Origin + Line * (reveal + PassLead) + Vector3.Right * PassAbeam;
                var gun = BuildGun(ctx, turretDefs, gunDef, weapons, live, gunAt, out site);
                if (gun == null)
                {
                    return;
                }

                TwoFlags(ctx, live, gun);
                TurretControl(ctx, live, gun, rocket);
                TurretIntercept(ctx, live, gun, torpedo);
                live.Clear();
                PilotIntercept(ctx, live, textures, weapons, torpedo, rocket, reveal);
            }
            finally
            {
                site?.Free();
                live?.Free();
                textures.Dispose();
            }
        });
    }

    // A hand-placed emplacement: an authored standalone entry repointed at nodes built here. Its arcs
    // are freed, its scatter zeroed and its spell held open. The suite proves the candidate class,
    // not an entry's authored behaviour.
    private static TurretController? BuildGun(TestContext ctx, TurretDefs defs, TurretDef def,
        WeaponDefs weapons, ProjectilePool pool, Vector3 at, out Node3D site)
    {
        const string pattern = "test_torpedo_gun";
        site = new Node3D { Name = pattern, Position = at };
        ctx.Host.AddChild(site);
        foreach (var name in new[] { "pitch", "muzzle" })
        {
            var node = new Node3D();
            node.SetMeta(AnimRuntime.NameMeta, name);
            site.AddChild(node);
        }

        def.YawNode = null;
        def.PitchNode = "pitch";
        def.Firepoints = new[] { "muzzle" };
        def.HealthyNode = null;
        def.Team = AimAssist.PlayerTeam;
        def.YawMinDeg = def.YawMaxDeg = def.PitchMinDeg = def.PitchMaxDeg = null;
        def.InaccuracyDeg = 0f;
        def.AttackMin = def.AttackMax = 600f;
        def.NodePatterns = new List<IReadOnlyList<string>> { new List<string> { pattern } };
        foreach (var other in defs.All.Where(d => !ReferenceEquals(d, def)))
        {
            other.NodePatterns = new List<IReadOnlyList<string>>();
        }

        var placed = site;
        var guns = TurretController.BuildEmplacements(defs, weapons,
            (name, _) => name == pattern ? new[] { placed } : Array.Empty<Node3D>(), pool);
        ctx.Check(guns.Length == 1, $"the hand-placed gun resolves turrets={guns.Length}");
        if (guns.Length != 1)
        {
            return null;
        }

        guns[0].SetActivated(true);
        return guns[0];
    }

    // TARGETABLE admits a round, FLYOUT_HEALTH makes it shootable; each lab round carries one key.
    private static void TwoFlags(TestContext ctx, ProjectilePool live, TurretController gun)
    {
        var start = new Transform3D(Basis.LookingAt(Line, Vector3.Up), gun.WorldPosition - Line * 200f);
        live.Spawn(new WeaponDef { Id = "lab_targetable", Targetable = true, Velocity = 60f, Range = 4000f },
            start, Vector3.Zero, shooterId: 7, team: InstantActionRuntime.EnemyTeam);
        gun.SimStep(Dt);
        ctx.Check(gun.TargetSource is ProjectilePool.Flyout { HealthMax: ProjectilePool.Flyout.NotShootable },
            $"a round TARGETABLE without FLYOUT_HEALTH is acquired, holding the -1.0 sentinel ({TargetPool.TargetLabel(gun.TargetSource)}, {gun.Gate})");
        live.Clear();
        live.Spawn(new WeaponDef { Id = "lab_health", FlyoutHealth = 10, Velocity = 60f, Range = 4000f },
            start, Vector3.Zero, shooterId: 7, team: InstantActionRuntime.EnemyTeam);
        gun.SimStep(Dt);
        ctx.Check(gun.Gate == TurretGate.NoTarget && gun.TargetSource == null,
            $"a round with FLYOUT_HEALTH and no TARGETABLE is no target ({gun.Gate})");
        live.Clear();
    }

    // The control: a fused rocket reaches the fourth list with its admission byte clear.
    private static void TurretControl(TestContext ctx, ProjectilePool live, TurretController gun, WeaponDef rocket)
    {
        live.Spawn(rocket, new Transform3D(Basis.LookingAt(Line, Vector3.Up), Origin), Vector3.Zero,
            shooterId: 7, team: InstantActionRuntime.EnemyTeam);
        var rounds = new List<(Vector3 Pos, Vector3 Velocity)>();
        float nearest = float.MaxValue;
        bool acquired = false;
        for (int i = 0; i < 60 * 5; i++)
        {
            rounds.Clear();
            live.CollectLiveRounds(rounds);
            if (rounds.Count == 0)
            {
                break;
            }
            nearest = Mathf.Min(nearest, rounds[0].Pos.DistanceTo(gun.WorldPosition));
            gun.SimStep(Dt);
            live.SimStep(Dt);
            acquired |= gun.Gate != TurretGate.NoTarget;
        }

        ctx.Check(nearest < gun.Def.DetectionRange && !acquired,
            $"CONTROL: {rocket.Id} passes {nearest:0} m from the gun, inside its {gun.Def.DetectionRange:0} m, and is never acquired");
        ctx.Note($"turret control: {rocket.Id} nearest {nearest:0} m of {gun.Def.DetectionRange:0} m ({gun.Def.Title}, {gun.Weapon.Id})");
        live.Clear();
    }

    private static void TurretIntercept(TestContext ctx, ProjectilePool live, TurretController gun, WeaponDef torpedo)
    {
        live.Spawn(torpedo, new Transform3D(Basis.LookingAt(Line, Vector3.Up), Origin), Vector3.Zero,
            shooterId: 7, team: InstantActionRuntime.EnemyTeam);
        if (!StepUntilRevealed(live))
        {
            ctx.Check(false, $"the torpedo passes RANGE_MINIMUM");
            return;
        }

        // The 0.6 weight behind +0x6f: a structure 30 m off against the torpedo about 250 m out.
        var anchor = new Node3D { Name = "test_torpedo_rival", Position = gun.WorldPosition + Vector3.Left * 30f };
        ctx.Host.AddChild(anchor);
        var registry = new DestructibleRegistry();
        var rival = registry.Register(new AnimDefinition { Name = "test_torpedo_rival" }, anchor, 200f);
        rival.Team = InstantActionRuntime.EnemyTeam;
        live.Structures = registry;
        gun.SimStep(Dt);
        bool plainTakesStructure = ReferenceEquals(gun.TargetSource, rival);
        gun.PrefersOrdnance = true;
        gun.SimStep(Dt);
        bool zeppelinTakesTorpedo = gun.TargetSource is ProjectilePool.Flyout;
        float torpedoAt = gun.TargetPosition.DistanceTo(gun.WorldPosition);
        ctx.Check(plainTakesStructure && zeppelinTakesTorpedo,
            $"an ordinary gun takes the structure 30 m off, a zeppelin's takes the torpedo {torpedoAt:0} m off (plain {plainTakesStructure}, preferring {zeppelinTakesTorpedo})");
        gun.PrefersOrdnance = false;
        live.Structures = null;
        anchor.Free();

        bool acquired = false;
        var round = FlyTorpedo(live, 60 * 20, out int steps, () =>
        {
            gun.SimStep(Dt);
            acquired |= gun.TargetSource is ProjectilePool.Flyout;
            live.SimStep(Dt);
        });

        ctx.Check(acquired, $"an emplacement beside the line acquires the torpedo");
        ctx.Check(round is { Destroyed: true, Live: false },
            $"…and its rounds shoot it down: health {round?.Health:0.#} of {torpedo.FlyoutHealth}, gone {round?.Live == false}, after {steps} step(s), {gun.ShotsFired} round(s)");
        ctx.Note($"turret: structure-vs-torpedo at {torpedoAt:0} m, torpedo health {round?.Health:0.#} after {steps} step(s), {gun.ShotsFired} round(s) fired");
        live.Clear();
    }

    // Steps until the one torpedo in flight leaves the pool. Its own state then says how: a kill
    // leaves health exactly 0 (Destroyed), an expiry leaves what it had.
    private static ProjectilePool.Flyout? FlyTorpedo(ProjectilePool live, int maxSteps, out int steps,
        Action step)
    {
        var flyouts = new List<ProjectilePool.Flyout>();
        live.CollectFlyouts(flyouts);
        var round = flyouts.Count > 0 ? flyouts[0] : null;
        for (steps = 0; round is { Live: true } && steps < maxSteps; steps++)
        {
            step();
        }

        return round;
    }

    private static void PilotIntercept(TestContext ctx, ProjectilePool live, TextureArchive textures,
        WeaponDefs weapons, WeaponDef torpedo, WeaponDef rocket, float reveal)
    {
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var stock = StockLoadouts.Load().All.Values.FirstOrDefault(d => d.Model == ctx.PlaneName);
        ctx.Check(stock != null, $"stock loadout found for plane={ctx.PlaneName}");
        if (stock == null)
        {
            return;
        }

        var aiPos = Origin + Line * (reveal + PilotLead) + Vector3.Up * PilotAbove;
        var gunner = new AiGunner(new RandomNumberGenerator { Seed = 20261019 })
        {
            DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9),
            QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9),
            AutoTarget = true,
        };
        var pilot = AiPilot.HoldingCourse(aiPos, Origin);
        pilot.Gunner = gunner;
        var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var ai = new FlightController
        {
            PlaneModel = aiModel,
            Collider = PlaneCollider.Build(aiModel),
            Damage = new PlaneDamage(stats.DestroyableParts),
            PlayerIndex = FlightRoster.ShooterIdBase,
            IsHumanPiloted = false,
            Pilot = pilot,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
        };
        try
        {
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.Bind(stock, aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, Origin);
            ctx.Host.AddChild(ai);
            ai.Team = AimAssist.PlayerTeam;
            ai.Held = true;
            ai.PlaceHeld(aiPos, Origin);

            // The control, 200 m abeam so its fuse never meets the pilot.
            live.Spawn(rocket, new Transform3D(Basis.LookingAt(Line, Vector3.Up), Origin + Vector3.Right * 200f),
                Vector3.Zero, shooterId: 7, team: InstantActionRuntime.EnemyTeam);
            var rounds = new List<(Vector3 Pos, Vector3 Velocity)>();
            bool rocketTaken = false;
            int rocketSteps = 0;
            float rocketNearest = float.MaxValue;
            for (; rocketSteps < 60 * 5; rocketSteps++)
            {
                rounds.Clear();
                live.CollectLiveRounds(rounds);
                if (rounds.Count == 0)
                {
                    break;
                }
                rocketNearest = Mathf.Min(rocketNearest, rounds[0].Pos.DistanceTo(ai.WorldPosition));
                ai.SimStep(Dt);
                live.SimStep(Dt);
                rocketTaken |= gunner.Target != null;
            }

            // The pilot holds no mode machine, so its acquisition scores on the machine's unset radius.
            float attack = new AiModeMachine(new Random(0)).AttackRange;
            ctx.Check(rocketNearest < attack && !rocketTaken,
                $"CONTROL: {rocket.Id} passes {rocketNearest:0} m from the pilot, inside its {attack:0} m attack radius, and is never its target ({rocketSteps} step(s))");
            live.Clear();

            live.Spawn(torpedo, new Transform3D(Basis.LookingAt(Line, Vector3.Up), Origin), Vector3.Zero,
                shooterId: 7, team: InstantActionRuntime.EnemyTeam);
            bool acquired = false;
            var round = FlyTorpedo(live, 60 * 30, out int steps, () =>
            {
                ai.SimStep(Dt);
                acquired |= gunner.Target is ProjectilePool.Flyout;
                live.SimStep(Dt);
            });

            ctx.Check(acquired, $"a held AI pilot on the line takes the torpedo as its target");
            ctx.Check(round is { Destroyed: true, Live: false },
                $"…and its guns shoot it down: health {round?.Health:0.#} of {torpedo.FlyoutHealth}, gone {round?.Live == false}, after {steps} step(s)");
            ctx.Note($"pilot: control nearest {rocketNearest:0} m over {rocketSteps} step(s); torpedo health {round?.Health:0.#} after {steps} step(s)");
            live.Clear();
        }
        finally
        {
            ai.Free();
        }
    }

    private static bool StepUntilRevealed(ProjectilePool live)
    {
        var gate = new List<(float Travelled, bool Unhittable)>();
        for (int i = 0; i < 60 * 30; i++)
        {
            gate.Clear();
            live.CollectFlyoutIntersect(gate);
            if (gate.Count == 0)
            {
                return false;
            }
            if (!gate[0].Unhittable)
            {
                return true;
            }
            live.SimStep(Dt);
        }

        return false;
    }
}
