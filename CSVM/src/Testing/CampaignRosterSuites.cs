using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The campaign roster spawner over a shipped story mission's <c>aiv</c> roster and its
/// built chapter world: every block gets a rig, the decoded net-versus-escort fork is applied per
/// block, the leader pass names the player rig, a deactivated block is inert, the taxi-path
/// vehicles are placed, and the escorting wingman then holds the player within the
/// <c>wingman-station</c> leash over a two-minute flown run.</summary>
internal static class CampaignRosterSuites
{
    // The story mission with the richest roster shape in one file: a player-escorting wingman_1,
    // two wingmen escorting AI leaders, netted enemies, deactivated blocks and four taxi-path
    // vehicles (docs/formats/ai-rosters.md).
    private const string RosterChapter = "C1";
    private const string RosterMission = "M04";

    private const float StepDt = 1f / 60f;
    private const float RunS = 120f;
    private const float HoldFromS = 60f;

    // The scripted player: no pilot, stick centred, a cruise lever, the same leader the
    // wingman-station suite flies against, so the leash below is that suite's.
    private const float LeaderThrottle = 0.3f;
    private const float LeaderSpeedMps = 55f;
    private const float LeashM = 600f;
    private const float MeanHoldM = 250f;
    private const float FlownLegLiftM = 800f;

    internal static void CampaignRoster(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RosterChapter, RosterMission);
        ctx.RequireData(missionZrdr, $"{RosterChapter}/{RosterMission} zrdr");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, RosterChapter);
        ctx.RequireData(texturesPath, $"{RosterChapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(RosterChapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(RosterMission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{RosterChapter}/{RosterMission} is not in cm_sequence");
        }

        var blocks = AiSkills.LoadRoster(missionZrdr);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var script = ObjectiveScript.Load(missionZrdr);
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null);
        var report = new StringBuilder();
        report.AppendLine($"{RosterChapter}/{RosterMission} seq={mission.Seq}: {blocks.Count} roster block(s)");

        ctx.WithWorld(RosterChapter, collision: false, RosterMission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            var rigs = new List<FlightController>();
            ProjectilePool? pool = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);

                // The human rig at the player block's own pose: a scripted leader, as the
                // wingman-station suite's is.
                var playerPose = PlayerPose(blocks);
                var playerStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                var player = Rig(ctx, planesGamez, textures, playerStats, live, ctx.PlaneName,
                    playerPose.Position, playerPose.Position + playerPose.Forward, human: true,
                    pilot: null, FlightRoster.ShooterIdBase, AimAssist.PlayerTeam);
                rigs.Add(player);

                int shooterId = FlightRoster.ShooterIdBase + 1;
                string what = director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, RosterChapter),
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
                        var stats = StatsFor(ctx, plan, report);
                        // A machine so the volumes have somewhere to land and the escort dispatch
                        // runs the decoded fork order; no gunner, so nothing in this run engages.
                        pilot.Machine = new AiModeMachine(new System.Random(7))
                        {
                            ActivationRange = skills.MinAiActiveDist,
                            AttackRange = stats.AiAttackRange,
                            ReturnRange = stats.AiReturnRange,
                        };
                        var rig = Rig(ctx, planesGamez, textures, stats, live, plan.PlaneNode, pos, look,
                            human: false, pilot, shooterId++, plan.Team ?? AimAssist.PlayerTeam);
                        rig.Inert = plan.Inert;
                        rigs.Add(rig);
                        return rig;
                    },
                    Rng = new System.Random(1),
                });
                report.AppendLine($"build summary suffix: '{what}'");

                var roster = director.Roster;
                ctx.Same(blocks.Count - 1, roster.Count, $"every non-player roster block has a rig");
                CheckFork(ctx, roster, player, report);
                CheckPaths(ctx, director, report);
                CheckVolumes(ctx, roster, skills, report);
                FlyEscort(ctx, roster, player, rigs, live, director, report);
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

        ctx.WriteArtifact($"test-campaign-roster-{RosterChapter}-{RosterMission}.txt", report.ToString());
        ctx.Note($"spawned {RosterChapter}/{RosterMission}'s roster from its aiv blocks and flew wingman_1 on the player");
    }

    // The decoded fork per block: wingman_1 escorts the player with no net; a wingman whose
    // primary_target is another block escorts THAT rig; a netted block patrols and never escorts;
    // a deactivated block is inert and out of play.
    private static void CheckFork(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        FlightController player, StringBuilder report)
    {
        int escorts = 0, netted = 0, inert = 0;
        foreach (var (name, rig) in roster)
        {
            var pilot = rig.Pilot;
            bool hasEscort = pilot?.Escort != null;
            bool hasNet = pilot?.Patrol != null;
            ctx.Check(!(hasEscort && hasNet), $"'{name}' never carries both an escort and a patrol net");
            escorts += hasEscort ? 1 : 0;
            netted += hasNet ? 1 : 0;
            inert += rig.Inert ? 1 : 0;
            report.AppendLine($"  {name}: escort={(hasEscort ? pilot!.Escort!.Leader?.Name.ToString() : "-")} " +
                              $"net={(hasNet ? pilot!.Patrol!.Net.Id.ToString() : "-")} inert={rig.Inert} team={rig.Team}");
        }
        report.AppendLine($"{escorts} escort(s), {netted} netted, {inert} inert");

        ctx.Check(roster.TryGetValue(CampaignDirector.WingmanName, out var wingman1),
            $"'{CampaignDirector.WingmanName}' is spawned");
        if (wingman1?.Pilot is { } w1)
        {
            ctx.Check(w1.Escort is { } e && ReferenceEquals(e.Leader, player),
                $"wingman_1 carries an escort whose leader is the player rig");
            ctx.Check(w1.Patrol == null, $"wingman_1 (netless mode wingman) carries no patrol net");
        }

        if (roster.TryGetValue("wingman_3", out var wingman3) && roster.TryGetValue("devastator_3", out var dev3))
        {
            ctx.Check(wingman3.Pilot?.Escort is { } e3 && ReferenceEquals(e3.Leader, dev3),
                $"wingman_3's primary_target names devastator_3 and resolves to that rig");
        }

        if (roster.TryGetValue("blakepeace_2_1", out var netBlock))
        {
            ctx.Check(netBlock.Pilot?.Patrol is { Net.Id: 15 },
                $"blakepeace_2_1 (netids 15) carries that net as its patrol: {netBlock.Pilot?.Patrol?.Net.Id}");
            ctx.Check(netBlock.Pilot?.Escort == null, $"…and no escort (a net demotes any wingman)");
        }

        if (roster.TryGetValue("blakebloodhawk_8", out var parked))
        {
            ctx.Check(parked.Inert && !parked.InPlay, $"blakebloodhawk_8 (deactivated 1) is inert and out of play");
        }
        ctx.Check(escorts == 3, $"the three wingman blocks are the mission's three escorts: {escorts}");
        ctx.Check(inert == 9, $"the nine deactivated blocks are inert: {inert}");
    }

    // The four taxi-path vehicles are placed, frozen on their authored paths in the built world.
    private static void CheckPaths(TestContext ctx, CampaignDirector director, StringBuilder report)
    {
        var paths = director.Paths;
        ctx.Check(paths != null, $"the roster build created the scripted-path registry");
        if (paths == null)
        {
            return;
        }
        ctx.Same(4, paths.Count, $"the four blocks authoring a taxiPath are placed");
        ctx.Check(paths.IsFrozen("blakepeace_2_3"), $"blakepeace_2_3 (pp1) is frozen on its path until START_TAXI");
        report.AppendLine($"{paths.Count} vehicle(s) on a path");
    }

    // The volume order: a net's authored 700 m return radius reaches a block that authors none of
    // its own, and the activation radius is floored at min_ai_active_dist.
    private static void CheckVolumes(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        AiSkills skills, StringBuilder report)
    {
        if (roster.TryGetValue("devastator_2", out var dev2) && dev2.Pilot?.Machine is { } m)
        {
            report.AppendLine($"devastator_2 volumes: act={m.ActivationRange:0} att={m.AttackRange:0} ret={m.ReturnRange:0}");
            ctx.Check(Mathf.IsEqualApprox(m.ReturnRange, 700f),
                $"devastator_2 takes its net's authored 700 m return radius: {m.ReturnRange:0} m");
            ctx.Check(m.ActivationRange >= skills.MinAiActiveDist,
                $"…and its activation radius is floored at min_ai_active_dist: {m.ActivationRange:0} m");
        }
    }

    // The flown half: wingman_1 stays with the scripted player rig over two minutes, judged over
    // the second minute against the wingman-station suite's own leash.
    private static void FlyEscort(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        FlightController player, List<FlightController> rigs, ProjectilePool live,
        CampaignDirector director, StringBuilder report)
    {
        if (!roster.TryGetValue(CampaignDirector.WingmanName, out var wing) || wing.Pilot?.Escort is not { } escort)
        {
            return;
        }
        // The flown leg is lifted well clear of the chapter's terrain: the roster poses (110 m
        // at the player block) are the spawner's business, and under a collision world a
        // scripted leader at that height flies into the ground.
        var up = Vector3.Up * FlownLegLiftM;
        player.Activate(player.WorldPosition + up, player.WorldPosition + up + player.NoseDirection);
        wing.Activate(wing.WorldPosition + up, wing.WorldPosition + up + wing.NoseDirection);
        float worstLate = 0f, meanRange = 0f;
        int lateSamples = 0;
        bool joined = false;
        for (int i = 0; i < (int)(RunS / StepDt); i++)
        {
            live.SimStep(StepDt);
            foreach (var rig in rigs)
            {
                if (rig.InPlay)
                {
                    rig.SimStep(StepDt);
                }
            }
            director.Paths?.Step(StepDt);
            float range = wing.WorldPosition.DistanceTo(player.WorldPosition);
            joined |= escort.State == EscortState.Station;
            if (i % 1800 == 0)
            {
                ctx.Note($"t={i * StepDt:0}s range={range:0} escort={escort.State} mode={AiModeMachine.NameOf(wing.Pilot!.Machine!.Mode)} wingV={wing.WorldVelocity.Length():0} leadV={player.WorldVelocity.Length():0}");
            }
            if (i * StepDt >= HoldFromS)
            {
                lateSamples++;
                worstLate = Mathf.Max(worstLate, range);
                meanRange += range;
            }
        }
        meanRange /= Mathf.Max(1, lateSamples);
        report.AppendLine($"wingman_1 hold: mean {meanRange:0} m, worst {worstLate:0} m over the last minute, joined={joined}");
        ctx.Check(joined, $"wingman_1 joins the formation state on the player");
        ctx.Check(wing.InPlay && player.InPlay, $"both the player and wingman_1 are still flying after {RunS:0} s");
        ctx.Check(worstLate > 0f && worstLate < LeashM,
            $"wingman_1 stays with the player: worst {worstLate:0} m of {LeashM:0} over the last minute");
        ctx.Check(meanRange < MeanHoldM,
            $"…averaging inside {MeanHoldM:0} m of the player: {meanRange:0} m");
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

    // The AI flavour of the plan's airframe; a def PlaneStats cannot resolve as a variant of that
    // airframe falls back to the plain base def, which is what the session's own spawner does.
    private static PlaneStats StatsFor(TestContext ctx, RosterSpawnPlan plan, StringBuilder report)
    {
        try
        {
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode, plan.AiDef);
        }
        catch (ArgumentException e)
        {
            report.AppendLine($"  {plan.Name}: '{plan.AiDef}' not loadable as a variant of {plan.PlaneNode} ({e.Message}); base def used");
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode);
        }
    }

    // One aircraft on the suite's own stage, the shape WingmanSuites.Rig builds: no camera, no
    // HUD, no devices.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        PlaneStats stats, ProjectilePool live, string planeNode, Vector3 pos, Vector3 lookAt, bool human,
        AiPilot? pilot, int shooterId, int team)
    {
        var model = new PlaneBuilder(planesGamez, textures).Build(planeNode);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f ? PlaneDamage.For(stats) : null,
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
