using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using Godot;

namespace CSVM.Testing;

/// <summary>BL-740: the whole torpedo run end to end, on a free-flying aeroplane rather than a
/// held rig, the acquisition's gasbag gate, the ranking's -0.5, the pursue arm closing the range,
/// the rocketeer's DAMAGES_ZEPPELIN match and its engagement band. C4/M03's own geometry: a Black
/// Hat Warhawk 1800 m out and 75 m above the Pandora's nearest gasbag, flying past rather than in.
/// The held-rig gate suite (<c>gasbag-ordnance-gate</c>) proves the admission rule; this proves an
/// aeroplane actually gets a torpedo away.</summary>
internal static class WarhawkTorpedoRunSuites
{
    private const float StepDt = 1f / 60f;
    private const int Seconds = 90;

    // MP3's cruise, the torpedo's dumbfire VELOCITY (wep_14) and wep_70's gun round.
    private const float HullSpeed = 30f;
    private const float TorpedoSpeed = 60f;
    private const float GunRoundSpeed = 750f;
    private const float ShotRange = 600f;

    // The torpedo-lead run starts beam-on, inside the bot's 900 m band, so the decoded steering
    // launches only once pursuit has swung it astern. The off-seat launch is pinned to the decoded run.
    private const float CrossingStartM = 900f;
    private const float BotLaunchFloorM = 500f;
    private const float ControlLaunchM = 192f;
    private const float ControlToleranceM = 10f;

    [Suite("warhawk-torpedo-run",
        "a free-flying Black Hat Warhawk torpedoes a gasbag (BL-740): from C4/M03's own opening "
        + "geometry it takes the gasbag over the engine beside it, holds pursue while the flight "
        + "law closes the range, and once inside its torpedo slot's authored 350-800 m band its "
        + "rocketeer takes the torpedo pylon and gets at least one away")]
    internal static void WarhawkTorpedoRun(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane("player_warhawk");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var stats = PlaneStats.LoadForAi(ctx.ZrdrPath, "player_warhawk", "bhatwarhawk");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai = null;
        Node3D? gasbagNode = null;
        Node3D? engineNode = null;
        var report = new StringBuilder();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The airship's own side, which the state-child rule gives its zones, against the
            // Warhawk on the other: the mission's Pandora is the player's and Black Hat is not.
            var gasbagPos = new Vector3(0f, 625f, 0f);
            gasbagNode = new Node3D { Name = "gasbag4", Position = gasbagPos };
            engineNode = new Node3D { Name = "leng12", Position = gasbagPos + new Vector3(0f, -40f, 60f) };
            ctx.Host.AddChild(gasbagNode);
            ctx.Host.AddChild(engineNode);
            var registry = new DestructibleRegistry();
            var gasbag = registry.Register(
                new AnimDefinition { Name = "gasbag4", AnimName = "zep_zone_gasbag4" }, gasbagNode, 120f);
            gasbag.Team = AimAssist.PlayerTeam;
            gasbag.Owner = "piratezep";
            gasbag.Gasbag = true;
            var engine = registry.Register(
                new AnimDefinition { Name = "leng12", AnimName = "zep_zone_leng12" }, engineNode, 200f);
            engine.Team = AimAssist.PlayerTeam;
            engine.Owner = "piratezep";

            var aiPos = gasbagPos + new Vector3(1790f, 75f, 0f);
            var pilot = AiPilot.HoldingCourse(aiPos, aiPos + Vector3.Forward);
            pilot.Gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260906 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(6),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(6),
            };
            pilot.Rocketeer = new AiRocketeer(new RandomNumberGenerator { Seed = 4242 }.Randf)
            {
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(6),
                QuickDrawChance = skills.QuickDrawChance(6),
            };
            var machine = new AiModeMachine(new System.Random(7))
            {
                ActivationRange = skills.MinAiActiveDist,
                AttackRange = stats.AiAttackRange,
                ReturnRange = stats.AiReturnRange,
                SteadyHandExponent = 0f,
                ProbeBlocked = (_, _) => null,
            };
            pilot.Machine = machine;

            var aiModel = new PlaneBuilder(planesGamez, textures).Build(stats.NodeName);
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
            ai.Loadout = Loadout.BindAi(stats.AiWeapons, "bhatwarhawk", aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, aiPos + Vector3.Forward);
            ctx.Host.AddChild(ai);
            ai.Team = AimAssist.PlayerTeam + 1;

            var torpedo = ai.Loadout.Hardpoints.First(h => h.Weapon.DamagesZeppelin);
            int torpedoesAtStart = torpedo.Ammo;
            // Its ordnance is torpedoes and nothing else: the def's other slot is a gun, so the
            // DAMAGES_ZEPPELIN match is what stands between this fit and an empty pylon walk.
            ctx.Check(torpedoesAtStart > 0 && ai.Loadout.FirableGuns.Any(),
                $"the shipped bhatwarhawk fit carries torpedoes and a gun ({torpedoesAtStart} on pylon{torpedo.Index})");

            string lastKey = string.Empty;
            int taken = 0;
            bool tookGasbag = false;
            var modes = new List<string>();
            machine.ModeChanged += (from, to, _) =>
                modes.Add($"{AiModeMachine.NameOf(from)}>{AiModeMachine.NameOf(to)}");
            for (int i = 0; i < 60 * Seconds; i++)
            {
                ai.SimStep(StepDt);
                live.SimStep(StepDt);
                tookGasbag |= ReferenceEquals(pilot.Gunner.Target, gasbag);
                var rocketeer = pilot.Rocketeer;
                if (rocketeer.LastVerdictKey.Length == 0 || rocketeer.LastVerdictKey == lastKey)
                    continue;
                lastKey = rocketeer.LastVerdictKey;
                taken += lastKey.StartsWith("taken:", System.StringComparison.Ordinal) ? 1 : 0;
                report.AppendLine($"t={i / 60f:0.0}s mode={AiModeMachine.NameOf(machine.Mode)} " +
                    $"target={TargetPool.NameOf(pilot.Gunner.Target)} " +
                    $"range={ai.WorldPosition.DistanceTo(gasbagPos):0} m: {rocketeer.LastVerdict}");
            }

            int launched = torpedoesAtStart - torpedo.Ammo;
            report.AppendLine($"torpedoes {torpedoesAtStart} -> {torpedo.Ammo}, pylon taken {taken}x");
            report.AppendLine($"modes: {string.Join(" ", modes)}");
            ctx.Check(tookGasbag,
                $"the sweep takes the gasbag over the engine beside it target={TargetPool.NameOf(pilot.Gunner.Target)}");
            ctx.Check(taken >= 2,
                $"the rocketeer reaches its 350-800 m band in pursue and takes the torpedo pylon there ({taken} time(s) in {Seconds} s)");
            ctx.Check(launched >= 1,
                $"and gets at least one torpedo away ({launched} of {torpedoesAtStart}, the rest held by the per-launch dice)");
            ctx.Note($"{launched} torpedo(es) away, pylon taken {taken}x");
        }
        finally
        {
            pool?.Free();
            ai?.Free();
            gasbagNode?.Free();
            engineNode?.Free();
            textures.Dispose();
        }
        ctx.WriteArtifact($"test-warhawk-torpedo-run.txt", report.ToString());
    }

    [Suite("bot-torpedo-lead",
        "a Black Hat Warhawk on a bot seat, armed as a bot is, steers on its ready torpedo's own lead "
        + "against a gasbag crossing at 30 m/s and launches far out, where the ABLE-TO-FAIL CONTROL, "
        + "the same aeroplane and arming off a bot seat as every campaign and Instant Action AI "
        + "flies, keeps the decoded gun-lead steering and its close launch")]
    internal static void BotTorpedoLead(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane("player_warhawk");
        float? bot = LaunchRange(ctx, texturesPath, botSeat: true);
        float? control = LaunchRange(ctx, texturesPath, botSeat: false);
        ctx.Check(bot is > BotLaunchFloorM,
            $"the bot seat launches its torpedo at the crossing gasbag from {bot:0} m, beyond {BotLaunchFloorM:0} m");
        ctx.Check(control is { } close && Mathf.Abs(close - ControlLaunchM) <= ControlToleranceM,
            $"ABLE-TO-FAIL CONTROL: off a bot seat the same aeroplane launches at {control:0} m, the decoded steering's {ControlLaunchM:0} +- {ControlToleranceM:0} m");
        ctx.Note($"launch range: bot seat {bot:0} m, off a bot seat {control:0} m");
    }

    [Suite("zeppelin-part-lead",
        "a gasbag pool riding a hull at 30 m/s reports the hull's velocity to the AI (#217): the "
        + "rocketeer's torpedo and the gunner's intercept point lead it along the hull's track and "
        + "meet it there, where a still hull's gasbag is aimed at itself")]
    internal static void ZeppelinPartLead(TestContext ctx)
    {
        var gasbagPos = new Vector3(0f, 625f, 0f);
        var gasbagNode = new Node3D { Name = "gasbag4", Position = gasbagPos };
        ctx.Host.AddChild(gasbagNode);
        try
        {
            var gasbag = new DestructibleRegistry().Register(
                new AnimDefinition { Name = "gasbag4", AnimName = "zep_zone_gasbag4" }, gasbagNode, 120f);
            gasbag.Gasbag = true;
            var shooter = gasbagPos + new Vector3(0f, 0f, ShotRange);
            var hullVel = new Vector3(HullSpeed, 0f, 0f);   // crossing the line of fire

            gasbag.Velocity = hullVel;
            CheckLead(ctx, "moving hull", shooter, gasbag, hullVel);

            gasbag.Velocity = Vector3.Zero;
            CheckLead(ctx, "CONTROL: still hull", shooter, gasbag, Vector3.Zero);
        }
        finally
        {
            gasbagNode.Free();
        }
    }

    // Both solves, fed what the AI reads off the pool, from a shooter pointed where the torpedo
    // must go. Each round must pass within a few metres of the gasbag's TRUE path. On the moving
    // hull the torpedo's lead is about 30 degrees, so a shot at where the gasbag stood misses by
    // hundreds of metres.
    private static void CheckLead(TestContext ctx, string label, Vector3 shooter,
        DestructibleRegistry.Instance gasbag, Vector3 hullVel)
    {
        TargetPool.TryTargetGeometry(gasbag, out var pos, out var vel, out var fwd, out bool live);
        ctx.Check(live && vel.IsEqualApprox(hullVel),
            $"{label}: the pool reports its carrier's velocity, ({vel.X:0},{vel.Y:0},{vel.Z:0}) m/s");
        AimAssist.TryIntercept(shooter, TorpedoSpeed, pos, hullVel, out var lead, out _);
        var nose = Basis.LookingAt(lead, Vector3.Up);
        var rocketeer = new AiRocketeer(() => 0f);
        var pylon = new RocketPylonView
        {
            Index = 0,
            DamagesZeppelin = true,
            Armed = true,
            MountPos = shooter,
            RoundSpeed = TorpedoSpeed,
            MinRangeM = 350f,
            MaxRangeM = 800f,
        };
        rocketeer.Solve(shooter, Vector3.Zero, nose, pos, vel, fwd, targetIsGasbag: true, new[] { pylon });
        float torpedoMiss = rocketeer.SelectedPylon == 0
            ? Miss(shooter, rocketeer.LaunchDirWorld * TorpedoSpeed, pos, hullVel)
            : float.NaN;
        ctx.Check(torpedoMiss < 5f,
            $"{label}: the rocketeer takes the torpedo pylon and the torpedo meets the gasbag, missing by {torpedoMiss:0.0} m (verdict '{rocketeer.LastVerdict}')");

        var gunner = new AiGunner(new RandomNumberGenerator { Seed = 217 });
        gunner.Solve(shooter, Vector3.Zero, nose, pos, vel, fwd, GunRoundSpeed);
        float gunMiss = Miss(shooter, gunner.AimDirWorld * GunRoundSpeed, pos, hullVel);
        float ahead = (gunner.InterceptPoint - pos).Dot(hullVel.Normalized());
        ctx.Check(gunMiss < 1f,
            $"{label}: the gunner's round meets the gasbag, missing by {gunMiss:0.0} m, intercept {ahead:0.0} m along the hull's track");
    }

    // One free-flying Black Hat Warhawk, armed as AiFlightAssembler arms a bot, against a lone gasbag
    // crossing its nose at HullSpeed. The range to the gasbag at its first torpedo launch, or null.
    private static float? LaunchRange(TestContext ctx, string texturesPath, bool botSeat)
    {
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var stats = PlaneStats.LoadForAi(ctx.ZrdrPath, "player_warhawk", "bhatwarhawk");
        var textures = new TextureArchive(texturesPath);
        var gasbagNode = new Node3D { Name = "gasbag4", Position = new Vector3(0f, 625f, 0f) };
        ProjectilePool? pool = null;
        FlightController? ai = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            ctx.Host.AddChild(gasbagNode);
            var registry = new DestructibleRegistry();
            var gasbag = registry.Register(
                new AnimDefinition { Name = "gasbag4", AnimName = "zep_zone_gasbag4" }, gasbagNode, 120f);
            gasbag.Team = AimAssist.PlayerTeam;
            gasbag.Gasbag = true;
            var hullVel = new Vector3(HullSpeed, 0f, 0f);
            gasbag.Velocity = hullVel;

            var aiPos = gasbagNode.Position + new Vector3(0f, 75f, CrossingStartM);
            var pilot = AiPilot.HoldingCourse(aiPos, gasbagNode.Position);
            pilot.Gunner = new AiGunner(new RandomNumberGenerator { Seed = 232 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(6),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(6),
            };
            pilot.Rocketeer = new AiRocketeer(new RandomNumberGenerator { Seed = 4242 }.Randf)
            {
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(6),
                QuickDrawChance = skills.QuickDrawChance(6),
                FiresOnFailedRoll = true,
            };
            pilot.Rocketeer.UseWingmanRule();
            pilot.Machine = new AiModeMachine(new System.Random(7))
            {
                ActivationRange = skills.MinAiActiveDist,
                AttackRange = stats.AiAttackRange,
                ReturnRange = stats.AiReturnRange,
                SteadyHandExponent = 0f,
                ProbeBlocked = (_, _) => null,
            };
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(stats.NodeName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                IsBotSeat = botSeat,
                Pilot = pilot,
                Projectiles = live,
                Destructibles = registry,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.BindAi(stats.AiWeapons, "bhatwarhawk", aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, gasbagNode.Position);
            ctx.Host.AddChild(ai);
            ai.Team = AimAssist.PlayerTeam + 1;

            var torpedo = ai.Loadout.Hardpoints.First(h => h.Weapon.DamagesZeppelin);
            int carried = torpedo.Ammo;
            for (int i = 0; i < 60 * Seconds && torpedo.Ammo == carried; i++)
            {
                gasbagNode.Position += hullVel * StepDt;
                ai.SimStep(StepDt);
                live.SimStep(StepDt);
            }

            return torpedo.Ammo < carried ? ai.WorldPosition.DistanceTo(gasbag.Centre) : null;
        }
        finally
        {
            pool?.Free();
            ai?.Free();
            gasbagNode.Free();
            textures.Dispose();
        }
    }

    // Closest approach between a round from origin at roundVel and a target from pos at vel.
    private static float Miss(Vector3 origin, Vector3 roundVel, Vector3 pos, Vector3 vel)
    {
        var r0 = origin - pos;
        var w = roundVel - vel;
        float t = Mathf.Max(0f, -r0.Dot(w) / w.LengthSquared());
        return (r0 + (w * t)).Length();
    }
}
