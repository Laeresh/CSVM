using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>CM13's six racers over C2/M03's real world with its colliders up: spawned from the
/// mission's own aiv roster, handed the world's dzpath ribbons by the director, they fly
/// <c>dzpath1</c> and then <c>dzpath2</c> on rails off their net's tags and come back to the net
/// without one of them ramming the dbase arch <c>dzpath2</c> threads at rail height.</summary>
internal static class CampaignRacerSuites
{
    private const string Chapter = "C2";
    private const string Mission = "M03";
    private const float StepDt = 1f / 60f;

    // The sortie log has every racer through dzpath2 inside 150 s of the spawn; the ceiling leaves
    // room for the slower approach a collision world's ground blow gives the net legs.
    private const float RunS = 240f;

    private const float LeaderThrottle = 0.3f;
    private const float LeaderSpeedMps = 55f;

    private static readonly string[] Racers =
    {
        "hafury_1", "hafury_2", "hafury_3", "hafury_4", "hafury_5", "hafury_6",
    };

    private static readonly string[] Course = { "dzpath1", "dzpath2" };

    internal static void CampaignRacers(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(texturesPath, $"{Chapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(Chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(Mission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{Chapter}/{Mission} is not in cm_sequence");
        }

        var blocks = AiSkills.LoadRoster(missionZrdr);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var script = ObjectiveScript.Load(missionZrdr);
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null, missionZrdr);
        var report = new StringBuilder();
        report.AppendLine($"{Chapter}/{Mission} seq={mission.Seq}: {blocks.Count} roster block(s), colliders up");

        ctx.WithWorld(Chapter, collision: true, Mission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            var rigs = new List<FlightController>();
            ProjectilePool? pool = null;
            // The net followers draw their branch picks off the session's AI stream, which every
            // earlier suite in the process has advanced; a fresh stream makes the flown course the
            // same one whatever ran before, and the state is handed back afterwards.
            var aiStream = Utils.Rng.Stream(Utils.Rng.Ai);
            ulong aiState = aiStream.State;
            aiStream.Seed = Utils.Rng.SeedFor(Utils.Rng.Ai);
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);

                var playerPose = PlayerPose(blocks);
                var playerStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                var player = Rig(ctx, world.Runtime, planesGamez, textures, playerStats, live, ctx.PlaneName,
                    playerPose.Position, playerPose.Position + playerPose.Forward, human: true,
                    pilot: null, FlightRoster.ShooterIdBase, AimAssist.PlayerTeam);
                rigs.Add(player);
                ctx.Same(6, playerStats.CollisionProbes.Count,
                    $"the player def resolves its six authored collision probes");

                int shooterId = FlightRoster.ShooterIdBase + 1;
                director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter),
                    MissionZrdrPath = missionZrdr,
                    ZrdrPath = ctx.ZrdrPath,
                    MinAiActiveDist = skills.MinAiActiveDist,
                    Player = () => player,
                    NetTrailers = new NetTrailerTargets(
                        () => player.WorldPosition,
                        name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                    FindNodes = name => world.Runtime.FindNodes(name),
                    Spawn = (plan, pos, look, pilot) =>
                    {
                        var stats = StatsFor(ctx, plan);
                        // A machine so the node-tag entry has a mode to enter; no gunner, so
                        // nobody engages and every racer's run is its net's alone.
                        pilot.Machine = new AiModeMachine(new System.Random(7))
                        {
                            ActivationRange = skills.MinAiActiveDist,
                            AttackRange = stats.AiAttackRange,
                            ReturnRange = stats.AiReturnRange,
                        };
                        var rig = Rig(ctx, world.Runtime, planesGamez, textures, stats, live, plan.PlaneNode, pos, look,
                            human: false, pilot, shooterId++, plan.Team ?? AimAssist.PlayerTeam);
                        rig.Inert = plan.Inert;
                        rigs.Add(rig);
                        return rig;
                    },
                    Rng = new System.Random(1),
                });
                director.Attach(new CampaignDirector.WorldInputs { Runtime = world.Runtime, Gamez = world.Gamez });

                var racers = new List<FlightController>();
                foreach (var name in Racers)
                {
                    if (director.Roster.TryGetValue(name, out var rig))
                        racers.Add(rig);
                }
                ctx.Same(Racers.Length, racers.Count, $"every hafury racer block has a rig");
                foreach (var racer in racers)
                {
                    ctx.Check(racer.Pilot?.DangerZones != null, $"{racer.Name} was handed the world's ribbons");
                    ctx.Same(1, racer.Stats?.CollisionProbes.Count ?? 0,
                        $"{racer.Name}'s AI def resolves basic_airplane's single origin probe");
                }

                Fly(ctx, racers, rigs, live, director, world.Runtime, report);
            }
            finally
            {
                foreach (var rig in rigs)
                {
                    rig.Free();
                }
                pool?.Free();
                textures.Dispose();
                aiStream.State = aiState;
            }
        });

        ctx.WriteArtifact($"test-campaign-racers-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"flew {Chapter}/{Mission}'s six racers through dzpath1 and dzpath2 with the world's colliders up");
    }

    // The flown run: every racer's rail runs are followed through the pilot's ZoneRun, a run
    // that ends with the pilot back on the net and the rig still in play counts as flown.
    private static void Fly(TestContext ctx, List<FlightController> racers, List<FlightController> rigs,
        ProjectilePool live, CampaignDirector director, AnimRuntime runtime, StringBuilder report)
    {
        var flown = new Dictionary<FlightController, HashSet<string>>();
        var onRail = new Dictionary<FlightController, string?>();
        var crashedAt = new Dictionary<FlightController, string>();
        foreach (var racer in racers)
        {
            flown[racer] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            onRail[racer] = null;
        }

        int steps = (int)(RunS / StepDt);
        int step = 0;
        for (; step < steps; step++)
        {
            live.SimStep(StepDt);
            foreach (var rig in rigs)
            {
                if (rig.InPlay)
                {
                    rig.SimStep(StepDt);
                }
            }
            // The mission's own opening: OBJECTIVE1 wakes kkgate_destruction a second in, which
            // blows the propane tanks hung in dzpath1's second gate before any racer gets there.
            director.Step(StepDt);
            runtime.Advance(StepDt);

            bool allDone = true;
            foreach (var racer in racers)
            {
                var pilot = racer.Pilot!;
                string? current = pilot.Machine?.Mode == AiMode.NavigatingDangerZone ? pilot.ZoneRun?.Ribbon.Name : null;
                if (current != null && onRail[racer] == null)
                {
                    report.AppendLine($"t={step * StepDt:0.0}s {racer.Name} locked '{current}' at {racer.WorldPosition}");
                }
                if (current == null && onRail[racer] is { } left)
                {
                    if (racer.Crashed)
                    {
                        crashedAt.TryAdd(racer, $"on '{left}' at {racer.WorldPosition}");
                    }
                    else
                    {
                        flown[racer].Add(left);
                        report.AppendLine($"t={step * StepDt:0.0}s {racer.Name} flew '{left}' and left at {racer.WorldPosition}");
                    }
                }
                else if (racer.Crashed)
                {
                    crashedAt.TryAdd(racer, $"off the rail at {racer.WorldPosition}");
                }
                onRail[racer] = current;
                allDone &= flown[racer].Contains(Course[^1]);
            }
            if (allDone)
            {
                break;
            }
        }
        report.AppendLine($"ran {step * StepDt:0} s");

        foreach (var racer in racers)
        {
            ctx.Check(!crashedAt.ContainsKey(racer),
                $"{racer.Name} never rammed anything ({(crashedAt.TryGetValue(racer, out var where) ? where : "in play")})");
            foreach (var zone in Course)
            {
                ctx.Check(flown[racer].Contains(zone), $"{racer.Name} flew '{zone}' end to end and returned to its net");
            }
        }
    }

    private static (Vector3 Position, Vector3 Forward) PlayerPose(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks)
    {
        foreach (var (name, fields) in blocks)
        {
            if (name.Equals(CampaignRosterPlan.PlayerBlock, StringComparison.OrdinalIgnoreCase)
                && AiSkills.RosterSpawnPose(fields) is { } pose)
            {
                return (pose.Position, new Basis(Vector3.Up, Mathf.DegToRad(pose.YawDeg)) * Vector3.Forward);
            }
        }
        return (new Vector3(0f, 500f, 0f), Vector3.Forward);
    }

    private static PlaneStats StatsFor(TestContext ctx, RosterSpawnPlan plan)
    {
        try
        {
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode, plan.AiDef);
        }
        catch (ArgumentException)
        {
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode);
        }
    }

    // One aircraft on the suite's own stage, the shape CampaignRosterSuites.Rig builds.
    private static FlightController Rig(TestContext ctx, AnimRuntime runtime, GameZ planesGamez,
        TextureArchive textures, PlaneStats stats, ProjectilePool live, string planeNode, Vector3 pos,
        Vector3 lookAt, bool human, AiPilot? pilot, int shooterId, int team)
    {
        var model = new PlaneBuilder(planesGamez, textures).Build(planeNode);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f ? PlaneDamage.For(stats) : null,
            // The session's wiring: a WeaponOrCollideHit destructible shatters and the aircraft
            // flies through, as the live game resolves it.
            CollideDamageSink = runtime.CollideDamageAt,
            PlayerIndex = shooterId,
            IsHumanPiloted = human,
            Pilot = pilot,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = team,
        };
        rig.AddChild(model);
        if (human)
        {
            rig.Setup(new FlightModel(stats), null, new CamParams(), pos, lookAt, LeaderThrottle, LeaderSpeedMps);
        }
        else
        {
            rig.Setup(new FlightModel(stats, aiForcePath: true), null, new CamParams(), pos, lookAt);
        }
        rig.Name = $"{planeNode}_{shooterId}";
        ctx.Host.AddChild(rig);
        return rig;
    }
}
