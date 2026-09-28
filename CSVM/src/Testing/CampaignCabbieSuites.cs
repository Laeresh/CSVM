using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>CM21's Cabbie over C5/M01's built world with its colliders up. Its rooftop taxi path
/// seats its patrol walk. After take-off it flies <c>M1Cabbie</c> round to the tagged node and runs
/// <c>dzpath33</c> low between the buildings.</summary>
internal static class CampaignCabbieSuites
{
    private const string Chapter = "C5";
    private const string Mission = "M01";
    private const string Cabbie = "autogyro_1";
    private const string StreetRun = "dzpath33";
    private const float StepDt = 1f / 60f;

    // The objective that wakes the Cabbie and releases its taxi, one directive after the other.
    private const int TaxiObjective = 28;

    // The seat of the rooftop placement and the far end of its one edge. Node 2 is nearest to
    // pp1_aip0 in M1Cabbie, and its only edge leads to node 11.
    private const int SeatNode = 2;
    private const int FirstTarget = 11;

    // Take-off, the 400 m circuit to node 4 and the street run fit well inside this. The loop
    // leaves as soon as the run has been flown.
    private const float RunS = 240f;

    // The street run's route sits 23 to 82 m up in world Y between the buildings, where the net's
    // nodes are 350 to 400 m. Anything under this is the run, never the net.
    private const float LowRunClearanceM = 120f;

    private const float SampleEveryS = 0.5f;

    [Suite("campaign-cabbie-run",
        "CM21's Cabbie (autogyro_1) spawned from C5/M01's own aiv roster into its built world with "
        + "the colliders up: placed on its rooftop taxi path pp1, its M1Cabbie walk is seated at the "
        + "placement (node 2, flying toward node 11) and survives the wake and the take-off, so it "
        + "flies the 400 m circuit to the tagged node 4, locks dzpath33 and runs it low between the "
        + "buildings, sampled as altitude above the ground under it")]
    internal static void CampaignCabbieRun(TestContext ctx)
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

        ctx.WithWorld(Chapter, collision: true, Mission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            var rigs = new List<FlightController>();
            ProjectilePool? pool = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);

                var playerPose = PlayerPose(blocks);
                var playerStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                var player = Rig(ctx, world.Runtime, planesGamez, textures, playerStats, live, ctx.PlaneName,
                    playerPose.Position, playerPose.Position + playerPose.Forward, human: true, pilot: null,
                    FlightRoster.ShooterIdBase, AimAssist.PlayerTeam);
                player.Held = true;
                rigs.Add(player);

                director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter),
                    MissionZrdrPath = missionZrdr,
                    ZrdrPath = ctx.ZrdrPath,
                    MinAiActiveDist = skills.MinAiActiveDist,
                    Player = () => player,
                    FindNodes = name => world.Runtime.FindNodes(name),
                    Spawn = (plan, pos, look, pilot) =>
                    {
                        if (!plan.Name.Equals(Cabbie, StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }

                        var stats = StatsFor(ctx, plan);
                        // A machine so the node-tag entry has a mode to enter; no gunner, so the
                        // Cabbie flies its net alone.
                        pilot.Machine = new AiModeMachine(new System.Random(7))
                        {
                            ActivationRange = skills.MinAiActiveDist,
                            AttackRange = stats.AiAttackRange,
                            ReturnRange = stats.AiReturnRange,
                        };
                        var rig = Rig(ctx, world.Runtime, planesGamez, textures, stats, live, plan.PlaneNode, pos,
                            look, human: false, pilot, FlightRoster.ShooterIdBase + 1, plan.Team ?? AimAssist.PlayerTeam);
                        rig.Inert = plan.Inert;
                        rigs.Add(rig);
                        return rig;
                    },
                    Rng = new System.Random(1),
                });
                director.Attach(new CampaignDirector.WorldInputs { Runtime = world.Runtime, Gamez = world.Gamez });

                if (!director.Roster.TryGetValue(Cabbie, out var cabbie) || cabbie.Pilot?.Patrol is not { } walk)
                {
                    ctx.Check(false, $"the roster spawned '{Cabbie}' on its net");
                    return;
                }

                ctx.Check(cabbie.Inert, $"'{Cabbie}' ships deactivated");
                ctx.Check(director.Paths?.IsFrozen(Cabbie) == true, $"'{Cabbie}' is placed frozen on its taxi path");
                ctx.Check(cabbie.Pilot.DangerZones != null, $"'{Cabbie}' was handed the world's ribbons");
                ctx.Same(SeatNode, walk.LegStartIndex, $"the placement seats the walk on node {SeatNode}");
                ctx.Same(FirstTarget, walk.CurrentIndex, $"…flying toward node {FirstTarget}");

                director.Graph?.Wake(TaxiObjective);
                Fly(ctx, cabbie, rigs, live, director, world.Runtime, report);
            }
            finally
            {
                foreach (var rig in rigs)
                {
                    rig.Free();
                }
                pool?.Free();
                textures.Dispose();
            }
        });

        ctx.WriteArtifact($"test-campaign-cabbie-run-{Chapter}-{Mission}.txt", report.ToString());
    }

    private static void Fly(TestContext ctx, FlightController cabbie, List<FlightController> rigs,
        ProjectilePool live, CampaignDirector director, AnimRuntime runtime, StringBuilder report)
    {
        var pilot = cabbie.Pilot!;
        var ground = new GodotWorldQuery(cabbie);
        bool released = false;
        bool locked = false;
        bool flown = false;
        float lowest = float.MaxValue;
        float lowestOnNet = float.MaxValue;
        float sinceSample = SampleEveryS;
        int steps = (int)(RunS / StepDt);
        int step = 0;
        for (; step < steps && !flown; step++)
        {
            live.SimStep(StepDt);
            foreach (var rig in rigs)
            {
                if (rig.InPlay)
                {
                    rig.SimStep(StepDt);
                }
            }
            director.Step(StepDt);
            runtime.Advance(StepDt);

            if (!released && director.Paths?.IsFrozen(Cabbie) == false && !cabbie.Held)
            {
                released = true;
                report.AppendLine(Log.Format($"t={step * StepDt:0.0}s handed off at {cabbie.WorldPosition}, walk {pilot.Patrol!.LegStartIndex} -> {pilot.Patrol.CurrentIndex}"));
            }

            bool onRail = pilot.Machine?.Mode == AiMode.NavigatingDangerZone
                && string.Equals(pilot.ZoneRun?.Ribbon.Name, StreetRun, StringComparison.OrdinalIgnoreCase);
            if (onRail && !locked)
            {
                locked = true;
                report.AppendLine(Log.Format($"t={step * StepDt:0.0}s locked '{StreetRun}' at {cabbie.WorldPosition}"));
            }
            else if (!onRail && locked)
            {
                flown = true;
                report.AppendLine(Log.Format($"t={step * StepDt:0.0}s left '{StreetRun}' at {cabbie.WorldPosition}"));
            }

            sinceSample += StepDt;
            if (!released || sinceSample < SampleEveryS)
            {
                continue;
            }

            sinceSample = 0f;
            var at = cabbie.WorldPosition;
            // From the aircraft down, so a street run between towers reads the street, not a roof.
            if (ground.Ray(at, at - (Vector3.Up * 3000f), CollisionLayers.World, null, out var hit))
            {
                float clearance = at.Y - hit.Position.Y;
                lowest = Mathf.Min(lowest, clearance);
                if (!locked)
                {
                    lowestOnNet = Mathf.Min(lowestOnNet, clearance);
                }
                report.AppendLine(Log.Format($"t={step * StepDt:0.0}s y={at.Y:0} ground={hit.Position.Y:0} clearance={clearance:0} walk {pilot.Patrol!.LegStartIndex}->{pilot.Patrol.CurrentIndex} mode={pilot.Machine?.Mode}"));
            }
        }
        report.AppendLine(Log.Format($"ran {step * StepDt:0} s, lowest clearance {lowest:0} m, lowest on the net {lowestOnNet:0} m"));

        ctx.Check(released, $"OBJECTIVE{TaxiObjective} woke '{Cabbie}' and its take-off handed it to the flight model");
        ctx.Check(!cabbie.Crashed, $"'{Cabbie}' flew without ramming anything (at {cabbie.WorldPosition})");
        ctx.Check(locked, $"'{Cabbie}' reached the tagged node and locked '{StreetRun}'");
        ctx.Check(flown, $"…and flew it end to end");
        ctx.Check(lowest < LowRunClearanceM,
            $"'{Cabbie}' ran low between the buildings: lowest clearance {lowest:0} m, under {LowRunClearanceM:0} m");
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

    // One aircraft on the suite's own stage, the shape CampaignRacerSuites.Rig builds.
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
        rig.Setup(new FlightModel(stats, aiForcePath: !human), null, new CamParams(), pos, lookAt);
        rig.Name = $"{planeNode}_{shooterId}";
        ctx.Host.AddChild(rig);
        return rig;
    }
}
