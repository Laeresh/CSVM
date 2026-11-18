using System;
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

/// <summary>The turret gunner's second pass. A gun takes an enemy emplacement at weight 1.0 with
/// 37.5 m on its distance, and never a friendly one (docs/org/targeting.md).</summary>
internal static class TurretDuelSuites
{
    private const float Dt = 1f / 60f;
    private const string PlaneNode = "player_pfighter";
    private const float EnemyGunRange = 300f; // m from the gun to the enemy emplacement
    private const float FriendRange = 60f;    // m to the friendly emplacement, nearer than either
    private const float Straddle = 10f;       // m either side of the turret term the aeroplane stands
    private static readonly Vector3 GunAt = new(6000f, 3000f, 0f);

    [Suite("turret-duel",
        "a turret gunner takes an enemy emplacement 300 m off over an AI aeroplane 347.5 m off and " +
        "the aeroplane at 327.5 m, the 37.5 m turret term between them; aircraft-first withdraws " +
        "the emplacement while an aircraft is in reach; the range gate reads the true distance, so an " +
        "enemy gun 20 m inside DETECTION_RANGE is still taken; a nearer friendly emplacement is never taken")]
    internal static void TurretDuel(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequirePlane(PlaneNode);
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.Zrdr, null);
        var turretDefs = TurretDefs.Load(ctx.Zrdr);
        var planesGamez = ctx.Planes;
        var stock = StockLoadouts.Load().All.Values.First(d => d.Model == PlaneNode);
        var aiStats = PlaneStats.LoadForAi(ctx.Zrdr, PlaneNode);
        ctx.Check(aiStats.AiTargetBias == 0f, $"the AI aeroplane carries no target_bias={aiStats.AiTargetBias:0}");

        var gunDef = turretDefs.All.FirstOrDefault(d => !d.Carried && weapons.Get(d.WeaponName) is { IsGun: true });
        ctx.Check(gunDef != null, $"a standalone turret entry with a gun exists");
        if (gunDef == null)
        {
            return;
        }

        gunDef.DetectionRange = 1200f;
        var textures = new TextureArchive(texturesPath);
        bool preferenceWas = AiTargetRanking.AircraftFirst;
        ProjectilePool? live = null;
        Node3D? gunSite = null;
        Node3D? enemySite = null;
        Node3D? friendSite = null;
        FlightController? plane = null;
        try
        {
            live = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(live);
            // Each call repoints the one entry at a fresh site, so the three guns share a def and its range.
            var gun = TorpedoInterceptSuites.BuildGun(ctx, turretDefs, gunDef, weapons, live, GunAt, out gunSite);
            var enemy = TorpedoInterceptSuites.BuildGun(ctx, turretDefs, gunDef, weapons, live,
                GunAt + Vector3.Right * EnemyGunRange, out enemySite);
            var friend = TorpedoInterceptSuites.BuildGun(ctx, turretDefs, gunDef, weapons, live,
                GunAt + Vector3.Forward * FriendRange, out friendSite);
            if (gun == null || enemy == null || friend == null)
            {
                return;
            }
            enemy.SetTeam(InstantActionRuntime.EnemyTeam);
            live.RegisterWorldTurrets(new[] { gun, enemy, friend });
            ctx.Check(AimAssist.Hostile(gun.Team, enemy.Team) && !AimAssist.Hostile(gun.Team, friend.Team),
                $"teams: gun {gun.Team}, enemy emplacement {enemy.Team}, friendly {friend.Team}");

            var model = new PlaneBuilder(planesGamez, textures).Build(PlaneNode);
            plane = new FlightController
            {
                PlaneModel = model,
                Collider = PlaneCollider.Build(model),
                Damage = new PlaneDamage(aiStats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = Array.Empty<int>(),
                AllowPause = false,
            };
            plane.AddChild(model);
            plane.Loadout = Loadout.Bind(stock, model, weapons);
            var parked = GunAt + Vector3.Back * 5000f;
            plane.Setup(new FlightModel(aiStats), null, new CamParams(), parked, parked + Vector3.Forward);
            ctx.Host.AddChild(plane);
            plane.Team = InstantActionRuntime.EnemyTeam;
            plane.Held = true;

            object? Pick(float planeRange, bool aircraftFirst)
            {
                AiTargetRanking.AircraftFirst = aircraftFirst;
                var at = planeRange > 0f ? GunAt + Vector3.Left * planeRange : parked;
                plane.PlaceHeld(at, at + Vector3.Forward);
                gun.SimStep(Dt);
                return gun.TargetSource;
            }

            float inside = EnemyGunRange + TurretController.TurretDistanceTerm + Straddle;
            float outside = EnemyGunRange + TurretController.TurretDistanceTerm - Straddle;
            var turretWins = Pick(inside, aircraftFirst: false);
            ctx.Check(ReferenceEquals(turretWins, enemy),
                $"decoded order: the gun takes the enemy emplacement {EnemyGunRange:0} m off over the aeroplane at {inside:0.0} m target={TargetPool.NameOf(turretWins)}");
            var planeWins = Pick(outside, aircraftFirst: false);
            ctx.Check(ReferenceEquals(planeWins, plane),
                $"and the aeroplane at {outside:0.0} m, inside the {TurretController.TurretDistanceTerm} m turret term target={TargetPool.NameOf(planeWins)}");
            var preferred = Pick(inside, aircraftFirst: true);
            ctx.Check(ReferenceEquals(preferred, plane),
                $"aircraft-first: the aeroplane at {inside:0.0} m withdraws the emplacement target={TargetPool.NameOf(preferred)}");
            var alone = Pick(0f, aircraftFirst: true);
            ctx.Check(ReferenceEquals(alone, enemy),
                $"with the aeroplane out of reach the gun takes the enemy emplacement, not the friendly one {FriendRange:0} m off target={TargetPool.NameOf(alone)}");

            // The range gate reads d, not d + 37.5: a gun 20 m inside the edge is still taken.
            float nearEdge = gunDef.DetectionRange - 20f;
            enemySite.Position = GunAt + Vector3.Right * nearEdge;
            var atEdge = Pick(0f, aircraftFirst: false);
            ctx.Check(ReferenceEquals(atEdge, enemy),
                $"the enemy emplacement {nearEdge:0} m off, inside DETECTION_RANGE {gunDef.DetectionRange:0} m by less than the turret term, is still taken target={TargetPool.NameOf(atEdge)}");

            enemySite.Position = GunAt + Vector3.Right * (gunDef.DetectionRange + 100f);
            var none = Pick(0f, aircraftFirst: false);
            ctx.Check(none == null,
                $"with the enemy emplacement past DETECTION_RANGE the friendly one is no target target={TargetPool.NameOf(none)}");
        }
        finally
        {
            AiTargetRanking.AircraftFirst = preferenceWas;
            plane?.Free();
            gunSite?.Free();
            enemySite?.Free();
            friendSite?.Free();
            live?.Free();
            textures.Dispose();
        }
    }

    // A Kestrel's rear gunner with an enemy aeroplane in reach. Under a person it weighs the
    // emplacement and the engine at 1.25 against the aeroplane's 1.0. Under the AI the filter
    // withdraws both (#360).
    [Suite("human-gunner-targeting",
        "a turret carried by a human-flown plane keeps enemy emplacements and zeppelin engines in " +
        "its ranking while an enemy aeroplane is in reach, 300 m of distance behind the aeroplane; " +
        "the same gunner under an AI pilot keeps the aircraft-first filter, and under " +
        "--ai-targeting=decoded neither takes the penalty")]
    internal static void HumanGunnerTargeting(TestContext ctx)
    {
        const string HostPlane = "player_kestrel";
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequirePlane(PlaneNode);
        ctx.RequirePlane(HostPlane);
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.Zrdr, null);
        var turretDefs = TurretDefs.Load(ctx.Zrdr);
        var planesGamez = ctx.Planes;
        var hostStats = PlaneStats.Load(ctx.Zrdr, HostPlane);
        var aiStats = PlaneStats.LoadForAi(ctx.Zrdr, PlaneNode);
        var gunDef = turretDefs.All.FirstOrDefault(d => !d.Carried && weapons.Get(d.WeaponName) is { IsGun: true });
        ctx.Check(gunDef != null, $"a standalone turret entry with a gun exists");
        if (gunDef == null)
        {
            return;
        }

        var textures = new TextureArchive(texturesPath);
        bool preferenceWas = AiTargetRanking.AircraftFirst;
        ProjectilePool? live = null;
        Node3D? enemySite = null;
        Node3D? engineNode = null;
        FlightController? host = null;
        FlightController? plane = null;
        try
        {
            live = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(live);
            var registry = new DestructibleRegistry();
            live.Structures = registry;

            FlightController Rig(string node, PlaneStats st, int shooterId, bool human, int team, Vector3 at)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(node);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(st.DestroyableParts),
                    PlayerIndex = shooterId,
                    IsHumanPiloted = human,
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(st), null, new CamParams(), at, at + Vector3.Forward);
                ctx.Host.AddChild(rig);
                rig.Held = true;
                rig.PlaceHeld(at, at + Vector3.Forward);
                rig.Team = team;
                return rig;
            }

            host = Rig(HostPlane, hostStats, 0, human: true, AimAssist.PlayerTeam, GunAt);
            host.Turrets = TurretController.BuildCarried(turretDefs, hostStats, host.PlaneModel!, weapons, host, live);
            ctx.Check(host.Turrets.Length == 1, $"the Kestrel's rear mount resolves turrets={host.Turrets.Length}");
            if (host.Turrets.Length != 1)
            {
                return;
            }
            var gunner = host.Turrets[0];
            gunner.Def.DetectionRange = 1200f;
            var here = gunner.WorldPosition;

            var parked = GunAt + Vector3.Back * 5000f;
            plane = Rig(PlaneNode, aiStats, FlightRoster.ShooterIdBase, human: false, InstantActionRuntime.EnemyTeam, parked);

            object? Pick(float planeRange, bool aircraftFirst)
            {
                AiTargetRanking.AircraftFirst = aircraftFirst;
                var at = here + Vector3.Left * planeRange;
                plane.PlaceHeld(at, at + Vector3.Forward);
                gunner.SimStep(Dt);
                return gunner.TargetSource;
            }

            float penalty = (TurretController.HumanGunnerStructureWeight - 1f) * AiTargetRanking.WeightScale;
            ctx.Check(Mathf.IsEqualApprox(penalty, 300f), $"the penalty is 300 m of distance, read {penalty:0.0}");

            // The enemy emplacement first, the turret term on its distance.
            var enemy = TorpedoInterceptSuites.BuildGun(ctx, turretDefs, gunDef, weapons, live,
                here + Vector3.Right * EnemyGunRange, out enemySite);
            if (enemy == null)
            {
                return;
            }
            enemy.SetTeam(InstantActionRuntime.EnemyTeam);
            live.RegisterWorldTurrets(new[] { enemy });
            float gunCost = here.DistanceTo(enemy.WorldPosition) + TurretController.TurretDistanceTerm;

            var penalised = Pick(gunCost + penalty + Straddle, aircraftFirst: true);
            ctx.Check(ReferenceEquals(penalised, enemy),
                $"human-flown: the enemy emplacement beats an aeroplane {penalty + Straddle:0} m behind it target={TargetPool.NameOf(penalised)}");
            var nearer = Pick(gunCost + penalty - Straddle, aircraftFirst: true);
            ctx.Check(ReferenceEquals(nearer, plane),
                $"and the aeroplane wins inside the {penalty:0} m penalty target={TargetPool.NameOf(nearer)}");
            var decoded = Pick(gunCost + Straddle, aircraftFirst: false);
            ctx.Check(ReferenceEquals(decoded, enemy),
                $"decoded order: no penalty, the emplacement beats an aeroplane {Straddle:0} m behind it target={TargetPool.NameOf(decoded)}");
            host.IsHumanPiloted = false;
            var filtered = Pick(gunCost + penalty + Straddle, aircraftFirst: true);
            ctx.Check(ReferenceEquals(filtered, plane),
                $"AI-flown: the aircraft-first filter withdraws the emplacement target={TargetPool.NameOf(filtered)}");
            host.IsHumanPiloted = true;

            // A zeppelin engine on the enemy team, with the emplacement out of reach.
            enemySite.Position = here + Vector3.Right * 5000f;
            engineNode = new Node3D { Name = "leng12", Position = here + Vector3.Right * EnemyGunRange };
            ctx.Host.AddChild(engineNode);
            var engine = registry.Register(
                new AnimDefinition { Name = "leng12", AnimName = "zep_zone_leng12" }, engineNode, 200f);
            engine.Team = InstantActionRuntime.EnemyTeam;
            float engineCost = here.DistanceTo(engine.Centre);

            var engineWins = Pick(engineCost + penalty + Straddle, aircraftFirst: true);
            ctx.Check(ReferenceEquals(engineWins, engine),
                $"human-flown: the zeppelin engine reaches the player's gunner and beats an aeroplane {penalty + Straddle:0} m behind it target={TargetPool.NameOf(engineWins)}");
            var planeWins = Pick(engineCost + penalty - Straddle, aircraftFirst: true);
            ctx.Check(ReferenceEquals(planeWins, plane),
                $"and the aeroplane wins inside the penalty target={TargetPool.NameOf(planeWins)}");
        }
        finally
        {
            AiTargetRanking.AircraftFirst = preferenceWas;
            plane?.Free();
            host?.Free();
            enemySite?.Free();
            engineNode?.Free();
            live?.Free();
            textures.Dispose();
        }
    }
}
