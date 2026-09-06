using System;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A <c>mode ship</c> hull's own gun (BL-687): the acquisition, the mount and the fire
/// decision a patrol boat runs in the original, against C1B/M03's four authored boats with one
/// hand-built hostile aeroplane to shoot at. The aircraft half of the roster stays unbuilt, so the
/// only aeroplane in the world is the one this suite places.</summary>
internal static class SurfaceVehicleGunSuites
{
    private const string BoatChapter = "C1B";
    private const string BoatMission = "M03";
    private const float StepDt = 1f / 30f;

    // Long enough for the mount to slew onto any bearing (it closes 1/7.5 of what is left per
    // step at this dt) and for several 0.3 s refire intervals to come round.
    private const int FiringSteps = 150;

    // Inside the def's 1-500 m window and a few degrees up: the case that must fire.
    private static readonly Vector3 InRange = new(280f, 40f, 0f);

    // Inside the 2500 m activation but outside the 500 m window: acquires, never fires.
    private static readonly Vector3 OutOfRange = new(900f, 60f, 0f);

    // 60 degrees up at 300 m: inside every range gate and outside the mount's +30 degree
    // elevation ceiling, so the guard costs the shot its aim quality.
    private static readonly Vector3 Overhead = new(0f, 260f, -150f);

    [Suite("surface-vehicle-guns",
        "a mode ship hull fires the weapon its own def authors (BL-687): C1B/M03's four "
        + "patrolboat blocks each build a gun from the def's wep_29 tuple with 9000 rounds and "
        + "a muzzle on the model's own turret>gun>firepoint marker rather than the hull origin; "
        + "with only hulls in the world no boat acquires anything, so a boat never shoots a "
        + "boat; against one hostile aeroplane 280 m off the beam a boat acquires it, traverses "
        + "both mount nodes off their rest pose, opens fire and spends rounds without damaging "
        + "itself; and it holds fire in the three decoded cases - an aeroplane on its own team, "
        + "one inside the activation radius but past the 500 m engagement window, and one 60 "
        + "degrees overhead where the mount's elevation guard costs more aim quality than the "
        + "gun's gate allows")]
    internal static void SurfaceVehicleGuns(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, BoatChapter, BoatMission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, BoatChapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, BoatChapter);
        ctx.RequireData(missionZrdr, $"{BoatChapter}/{BoatMission} zrdr");
        ctx.RequireData(chapterZrdr, $"{BoatChapter} zrdr");
        ctx.RequireData(texturesPath, $"{BoatChapter} textures");

        var mission = MissionOf(ctx, BoatChapter, BoatMission);
        var script = ObjectiveScript.Load(missionZrdr);
        var defs = VehicleDefs.Load(ctx.ZrdrPath);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Guns"), null);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        try
        {
            ctx.WithWorld(BoatChapter, collision: false, BoatMission, world =>
            {
                var worldRoot = world.Runtime.WorldRoot ?? world.Stage;
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);

                var vessels = new SurfaceVehicleRuntime(world.Gamez, world.Session.Builder.Scene,
                    world.Runtime, defs, worldRoot)
                {
                    Projectiles = live,
                    Weapons = weapons,
                };
                live.SurfaceVehicles = vessels;
                worldRoot.AddChild(vessels);
                director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = chapterZrdr,
                    MissionZrdrPath = missionZrdr,
                    ZrdrPath = ctx.ZrdrPath,
                    MinAiActiveDist = skills.MinAiActiveDist,
                    FindNodes = name => world.Runtime.FindNodes(name),
                    Spawn = (_, _, _, _) => null,   // the aircraft half stays unbuilt on purpose
                    SpawnSurface = (plan, pos, forward) => vessels.Spawn(plan, pos, forward),
                    Rng = new Random(1),
                });
                foreach (var hull in director.Vessels.Values)
                {
                    hull.Wake();
                }

                var boats = director.Vessels.Values.ToList();
                ctx.Check(boats.Count > 0, $"{BoatChapter}/{BoatMission} builds hulls to arm: {boats.Count}");
                if (boats.Count == 0)
                {
                    return;
                }

                // ---- the fit and the mount ---------------------------------------------------
                int armed = boats.Count(b => b.Gunner != null);
                ctx.Same(boats.Count, armed, $"every hull builds a gun from its def's weapons block");
                var boat = boats[0];
                if (boat.Gunner is not { } gun)
                {
                    return;
                }
                ctx.Same(9000, gun.Ammo, $"the magazine is the def's authored round count, not a default");
                float muzzleOffset = gun.MuzzlePosition.DistanceTo(boat.Position);
                ctx.Check(muzzleOffset > 0.5f,
                    $"the muzzle is the model's own firepoint marker, {muzzleOffset:0.##} m off the hull origin");

                // ---- a boat does not shoot a boat --------------------------------------------
                // Four hulls and nothing else: the vehicle pool is non-empty and every gunner still
                // finds nothing, since the acquisition drops non-aircraft vehicle candidates.
                var scan = new AimCandidateSet();
                live.CollectVehicleList(scan);
                ctx.Same(boats.Count, scan.Vehicles.Count, $"the pool's VehicleList is the hulls alone");
                Step(boats, 30);
                int lockedOnAHull = boats.Count(b => b.Gunner?.Target is SurfaceVehicle);
                ctx.Same(0, lockedOnAHull, $"no boat targets another boat");
                ctx.Same(0, boats.Sum(b => b.Gunner?.ShotsFired ?? 0),
                    $"and with nothing hostile in the world, no boat has fired");

                // ---- against a hostile aeroplane ---------------------------------------------
                int hullTeam = boat.Team ?? AimAssist.NeutralTeam;
                ctx.Check(hullTeam != AimAssist.NeutralTeam,
                    $"the hull carries its block's team, so the gate can admit a target: {hullTeam}");
                var rest = gun.MountNodes;
                var turretRest = rest.Turret.Transform;
                var gunRest = rest.Gun.Transform;
                float healthBefore = boat.Health ?? 0f;

                var quarry = Rig(ctx, planesGamez, textures, live, 0, boat.Position + InRange,
                    AimAssist.PlayerTeam);
                Step(boats, FiringSteps);

                ctx.Check(ReferenceEquals(gun.Target, quarry),
                    $"the boat acquires the hostile aeroplane at {boat.Position.DistanceTo(quarry.WorldPosition):0} m");
                ctx.Check(rest.Turret.Transform != turretRest || rest.Gun.Transform != gunRest,
                    $"the mount's own turret and gun nodes traversed off their rest pose");
                ctx.Check(gun.ShotsFired > 0, $"the boat opened fire: {gun.ShotsFired} round(s)");
                ctx.Same(9000 - gun.ShotsFired, gun.Ammo, $"each round comes off the authored magazine");
                ctx.Check(Mathf.IsEqualApprox(healthBefore, boat.Health ?? 0f),
                    $"the hull took no damage from its own muzzle sitting on it: {boat.Health:0.##} of {healthBefore:0.##}");

                // ---- the three holds ---------------------------------------------------------
                quarry.Team = hullTeam;
                CheckHold(ctx, boats, gun, "an aeroplane on the boat's own team",
                    expectTarget: false);

                quarry.Team = AimAssist.PlayerTeam;
                Place(quarry, boat.Position + OutOfRange);
                CheckHold(ctx, boats, gun, "an aeroplane past the def's 500 m engagement window",
                    expectTarget: true);

                Place(quarry, boat.Position + Overhead);
                CheckHold(ctx, boats, gun, "an aeroplane above the mount's +30 degree elevation guard",
                    expectTarget: true);

                ctx.Note($"a patrolboat acquires, traverses and fires its own wep_29 ({gun.ShotsFired} rounds), and holds fire in all three decoded cases");
            });
        }
        finally
        {
            if (pool != null && GodotObject.IsInstanceValid(pool))
            {
                pool.Free();
            }

            textures.Dispose();
        }
    }

    // Steps every hull's gunner directly rather than the hull itself: the net follower would walk
    // the boats away from the geometry each case is built around, and the gun is what is under test.
    private static void Step(System.Collections.Generic.List<SurfaceVehicle> boats, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var boat in boats)
            {
                boat.Gunner?.Step(StepDt);
            }
        }
    }

    // One hold case: run the gunner long enough for several refire intervals and assert nothing
    // left the barrel, saying whether the target should still have been ACQUIRED. The distinction
    // matters — a gate that silently stopped acquiring would pass a shots-only check for the
    // wrong reason.
    private static void CheckHold(TestContext ctx,
        System.Collections.Generic.List<SurfaceVehicle> boats, SurfaceGunner gun,
        string what, bool expectTarget)
    {
        int before = gun.ShotsFired;
        Step(boats, FiringSteps);
        ctx.Same(before, gun.ShotsFired, $"the boat holds fire against {what}");
        if (expectTarget)
        {
            ctx.Check(gun.Target != null, $"and still acquires it: {what} stays a target");
        }
        else
        {
            ctx.Check(gun.Target == null, $"and drops it entirely: {what} is not a target");
        }
    }

    // ⚠ Through WarpTo, never the node's own transform: a rig's WorldPosition is its flight
    // model's, and the acquisition reads that, so writing GlobalPosition moves the drawn plane
    // and leaves the target exactly where it was.
    private static void Place(FlightController rig, Vector3 at) => rig.WarpTo(at, 0f, 0f);

    // A hostile aeroplane for the boat to shoot at, registered on the pool so it reaches the
    // VehicleList the acquisition walks. The manual-rig shape CampaignSuites and CombatSuites use.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        ProjectilePool live, int index, Vector3 at, int team)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                ? PlaneDamage.For(stats) : null,
            PlayerIndex = FlightRoster.ShooterIdBase + index,
            IsHumanPiloted = true,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = team,
            Name = $"Quarry{index}",
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), at, at + Vector3.Forward);
        ctx.Host.AddChild(rig);
        live.RegisterAircraft(rig.Body!);
        return rig;
    }

    private static CampaignMission MissionOf(TestContext ctx, string chapter, string folder)
    {
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(folder, StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }
        }

        throw new SuiteSkippedException($"{chapter}/{folder} is not in cm_sequence");
    }
}
