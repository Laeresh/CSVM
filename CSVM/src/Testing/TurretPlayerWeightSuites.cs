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

/// <summary>The turret gunner's cost on a live pick: a human weighs 0.8, and each aeroplane carries
/// its def's <c>target_bias</c>. A gun takes the human over a nearer AI aeroplane inside the margin
/// the two make (docs/org/targeting.md "The picker's cost").</summary>
internal static class TurretPlayerWeightSuites
{
    private const float Dt = 1f / 60f;
    private const string PlaneNode = "player_pfighter";
    private const string WingmanDef = "wingman";
    private const float HumanRange = 700f; // m from the gun to the human
    private const float Straddle = 40f;    // m either side of the margin the AI aeroplane stands
    private static readonly Vector3 GunAt = new(6000f, 3000f, 0f);

    [Suite("turret-player-weight",
        "a turret gunner ranks a human at weight 0.8 with its def's target_bias: a human " +
        "player_pfighter loads -300, a plain AI one 0 and one on the wingman def -100, so a gun " +
        "takes the human 700 m out over an AI aeroplane up to 540 m nearer and over a wingman-def " +
        "one up to 440 m nearer, and the AI aeroplane 40 m past either margin")]
    internal static void TurretPlayerWeight(TestContext ctx)
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

        // Each through the path its pilot spawns by: GameSession's human load and the AI one. The
        // -300 sits on player_airplane, the -100 on the wingman defs, and a plain AI def has none.
        var humanStats = PlaneStats.Load(ctx.Zrdr, PlaneNode);
        var aiStats = PlaneStats.LoadForAi(ctx.Zrdr, PlaneNode);
        var wingStats = PlaneStats.LoadForAi(ctx.Zrdr, PlaneNode, WingmanDef);
        ctx.Check(humanStats.AiTargetBias == -300f && aiStats.AiTargetBias == 0f && wingStats.AiTargetBias == -100f,
            $"target_bias: human {humanStats.AiTargetBias:0} [{humanStats.DefName}], AI {aiStats.AiTargetBias:0} [{aiStats.DefName}], wingman def {wingStats.AiTargetBias:0} [{wingStats.DefName}]");

        var gunDef = turretDefs.All.FirstOrDefault(d => !d.Carried && weapons.Get(d.WeaponName) is { IsGun: true });
        ctx.Check(gunDef != null, $"a standalone turret entry with a gun exists");
        if (gunDef == null)
        {
            return;
        }

        // The suite fixes the field rather than the shipped one, so neither aeroplane falls outside it.
        gunDef.DetectionRange = 1200f;
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? live = null;
        Node3D? site = null;
        FlightController? human = null;
        FlightController? ai = null;
        FlightController? wing = null;
        try
        {
            live = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(live);
            var gun = TorpedoInterceptSuites.BuildGun(ctx, turretDefs, gunDef, weapons, live, GunAt, out site);
            if (gun == null)
            {
                return;
            }

            FlightController Rig(PlaneStats stats, Vector3 at, bool isHuman, int shooterId)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(PlaneNode);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = shooterId,
                    IsHumanPiloted = isHuman,
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Loadout = Loadout.Bind(stock, model, weapons);
                rig.Setup(new FlightModel(stats), null, new CamParams(), at, at + Vector3.Forward);
                ctx.Host.AddChild(rig);
                rig.Team = InstantActionRuntime.EnemyTeam;
                rig.Held = true;
                rig.PlaceHeld(at, at + Vector3.Forward);
                return rig;
            }

            human = Rig(humanStats, GunAt + Vector3.Right * HumanRange, true, 1);
            ctx.Check(human.Stats?.AiTargetBias == -300f, $"the live human rig spends target_bias={human.Stats?.AiTargetBias:0}");
            // A rig is never freed mid-suite, since the pool keeps it listed: the idle one parks out of range.
            var parked = GunAt + Vector3.Back * 5000f;
            ai = Rig(aiStats, parked, false, FlightRoster.ShooterIdBase);
            wing = Rig(wingStats, parked + Vector3.Right * 50f, false, FlightRoster.ShooterIdBase + 1);
            foreach (var (rival, idle, what) in new[] { (ai, wing, "an AI aeroplane"), (wing, ai, "one on the wingman def") })
            {
                idle.PlaceHeld(parked, parked + Vector3.Forward);
                var stats = rival.Stats!;

                // How much nearer the AI aeroplane must stand to win: 240 m plus the biases' difference.
                float margin = TurretController.Cost(0f, 1f, stats.AiTargetBias)
                    - TurretController.Cost(0f, TurretController.PlayerWeight, humanStats.AiTargetBias);

                object? PickWithAiAt(float range)
                {
                    var at = GunAt + Vector3.Left * range;
                    rival.PlaceHeld(at, at + Vector3.Forward);
                    gun.SimStep(Dt);
                    return gun.TargetSource;
                }

                float inside = HumanRange - margin + Straddle;
                float outside = HumanRange - margin - Straddle;
                var near = PickWithAiAt(inside);
                ctx.Check(ReferenceEquals(near, human),
                    $"the gun takes the human {HumanRange:0} m out over {what} at {inside:0} m, inside the {margin:0} m margin target={TargetPool.NameOf(near)}");
                var nearer = PickWithAiAt(outside);
                ctx.Check(ReferenceEquals(nearer, rival),
                    $"and {what} once it stands at {outside:0} m target={TargetPool.NameOf(nearer)}");
                ctx.Note($"turret-player-weight: {what} target_bias {stats.AiTargetBias:0}, margin {margin:0} m");
            }
        }
        finally
        {
            human?.Free();
            ai?.Free();
            wing?.Free();
            site?.Free();
            live?.Free();
            textures.Dispose();
        }
    }
}
