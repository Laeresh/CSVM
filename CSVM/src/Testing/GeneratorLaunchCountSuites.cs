using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>A generator launch is a roster member to the mission script: the original's spawn
/// copies the template's group onto the new vehicle, so a <c>DEDG</c> over that group counts it.
/// C5/M04 is the shipped case that cannot do without it: Lucas Miles is the only member of group
/// 5, launched off the Dante by the <c>dantezep</c> generator, and OBJECTIVE38's
/// <c>DEDG [5, 0]</c> is the fuse on the instant loss. A launch the campaign roster does not
/// hold reads that group as wiped out the moment he leaves the ship, and the mission is lost
/// 15 s later with Miles alive in the player's brackets. The launch itself must survive the
/// Dante's kill: a torpedo salvo kills the fourth gasbag inside OBJECTIVE10's 0.5 s nap, before
/// OBJECTIVE11 credits the bay, and a bay disabled on the kill tick never launches him.</summary>
internal static class GeneratorLaunchCountSuites
{
    private const string Chapter = "C5";
    private const string Mission = "M04";
    private const string Generator = "dantezep";
    private const string Template = "stihellhound_5_7";
    private const string Launch = "stihellhound_5_eg0";
    private const int MilesGroup = 5;

    // The loss fuse as the mission authors it: 38 completes on group 5 at zero and naps 39, the
    // instant loss, awake 15 s later.
    private const int FuseObjective = 38;
    private const int LossObjective = 39;
    private const float FuseSeconds = 15f;

    private const float StepDt = 1f / 60f;
    private const float SpawnWaitS = 30f;
    private const float SettleS = 2f;

    // How far the kill leads the credit in the logged torpedo run: the fourth gasbag at 40.0 s,
    // OBJECTIVE11 at 40.3 s. Inside GeneratorCycle.HostDeathGraceSeconds by design.
    private const float KillLeadS = 0.3f;
    private const float PreRollS = 5f;

    [Suite("generator-launch-dedg",
        "C5/M04's Miles over the mission's own BUILT world: the dantezep generator's launch "
        + "stihellhound_5_eg0 still fires when the credit lands 0.3 s after the Dante's kill, "
        + "enters the campaign roster under its launch name carrying the "
        + "template's group 5, DEDG over that group counts 0 before the launch and 1 after it, "
        + "OBJECTIVE38 (DEDG [5, 0], the fuse on the INSTANTLOSS) stays incomplete while Miles "
        + "flies, and completes once he is shot down, napping OBJECTIVE39 awake")]
    internal static void GeneratorLaunchDedg(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
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

        var script = ObjectiveScript.Load(missionZrdr);
        var blocks = AiSkills.LoadRoster(missionZrdr);
        var nets = AiNets.Load(chapterZrdr);
        var defs = VehicleDefs.Load(ctx.ZrdrPath);
        var templates = CampaignRosterPlan.GeneratorTemplates(missionZrdr, defs, nets);
        var report = new StringBuilder();
        var def = CheckAuthored(ctx, script, blocks, templates, missionZrdr, report);
        if (def == null)
        {
            return;
        }

        var skills = AiSkills.Load(ctx.ZrdrPath);
        var director = CampaignDirector.Create(script, mission,
            CampaignProfileDef.NewProfile("Zachary"), null);
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
            Drive(ctx, world, director, def, templates, blocks, skills, nets, missionZrdr,
                chapterZrdr, texturesPath, report));

        ctx.WriteArtifact($"test-generator-launch-dedg-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"{Chapter}/{Mission}: the Dante's launch counts for DEDG over group {MilesGroup}");
    }

    // The authored shape, read off the shipped files: the generator's label resolves the disabled
    // group-5 template, no enabled block shares that group, and 38/39 are the fuse and the loss.
    private static EnemyGeneratorDef? CheckAuthored(TestContext ctx, ObjectiveScript script,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks,
        IReadOnlyDictionary<string, RosterSpawnPlan> templates, string missionZrdr,
        StringBuilder report)
    {
        EnemyGeneratorDef? def = null;
        foreach (var d in EnemyGenerators.Load(missionZrdr))
        {
            if (d.Node.Equals(Generator, StringComparison.OrdinalIgnoreCase))
            {
                def = d;
            }
        }
        ctx.Check(def != null, $"{Chapter}/{Mission} authors the generator '{Generator}'");
        var kind = CampaignRosterPlan.ResolveGeneratorLaunch(templates, def?.VehicleParams, out var plan);
        ctx.Check(kind == GeneratorLaunch.Template && plan is { Name: Template, Group: MilesGroup },
            $"'{def?.VehicleParams}' resolves the template {Template} in group {MilesGroup} ({kind}, group {plan?.Group})");

        int enabledInGroup = 0;
        foreach (var (name, fields) in blocks)
        {
            if (AiSkills.RosterEnabled(fields) && AiSkills.RosterGroup(fields) == MilesGroup)
            {
                enabledInGroup++;
                report.AppendLine($"enabled group-{MilesGroup} block: {name}");
            }
        }
        ctx.Same(0, enabledInGroup,
            $"no enabled roster block is in group {MilesGroup}: the launch is the group's only member");

        var fuse = ObjectiveNumbered(script, FuseObjective);
        var loss = ObjectiveNumbered(script, LossObjective);
        ctx.Check(fuse?.Dedg is { Group: MilesGroup, Max: 0 },
            $"OBJECTIVE{FuseObjective} completes on group {MilesGroup} reaching zero");
        ctx.Check(fuse?.NapWhenComplete is { Target: LossObjective } nap
                  && Mathf.IsEqualApprox(nap.Seconds, FuseSeconds),
            $"…and naps OBJECTIVE{LossObjective} awake {FuseSeconds:0} s later");
        ctx.Check(loss is { InstantLoss: true, BeginDormant: true },
            $"OBJECTIVE{LossObjective} is the dormant INSTANTLOSS");
        return def;
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        EnemyGeneratorDef def, IReadOnlyDictionary<string, RosterSpawnPlan> templates,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, AiSkills skills,
        IReadOnlyList<AiNet> nets, string missionZrdr, string chapterZrdr, string texturesPath,
        StringBuilder report)
    {
        var textures = new TextureArchive(texturesPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        ProjectilePool? pool = null;
        FlightRoster? roster = null;
        FlightController? player = null;
        AiGeneratorRuntime? generators = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            roster = Spawner(ctx, planesGamez, textures, live);

            var pose = PlayerPose(blocks);
            player = HumanRig(ctx, planesGamez, textures, live, pose.Position, pose.Position + pose.Forward);
            var human = player;
            var spawner = roster;

            director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = chapterZrdr,
                MissionZrdrPath = missionZrdr,
                ZrdrPath = ctx.ZrdrPath,
                MinAiActiveDist = skills.MinAiActiveDist,
                Player = () => human,
                NetTrailers = new NetTrailerTargets(
                    () => human.WorldPosition,
                    name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                FindNodes = name => world.Runtime.FindNodes(name),
                Spawn = (plan, pos, look, pilot) => spawner.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                Rng = new Random(1),
            });
            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = world.Runtime.Sounds,
                Projectiles = live,
                ListenerPosition = () => human.WorldPosition,
                PlayerAircraft = () => human,
                Rng = new Random(1),
            });

            int? before = director.GroupLiveCount(MilesGroup);
            report.AppendLine($"dedg: group {MilesGroup} counts {before?.ToString() ?? "-"} before the launch");
            ctx.Same(0, before ?? -1, $"DEDG counts no member of group {MilesGroup} before the Dante launches");

            // The session's own spawn shape: the template through SpawnFor under the decoded launch
            // name, then booked into the campaign roster, which is the seam under test.
            AiGeneratorRuntime? runtime = null;
            LaunchedVehicle SpawnFromGenerator(EnemyGeneratorDef d, Vector3 pos, Vector3 look, AiPilot pilot)
            {
                if (CampaignRosterPlan.ResolveGeneratorLaunch(templates, d.VehicleParams, out var plan)
                    != GeneratorLaunch.Template || plan == null)
                {
                    return default;
                }
                string name = EnemyGenerators.LaunchName(EnemyGenerators.LaunchBase(plan.Name), runtime?.LaunchOrdinal ?? 0);
                var launched = spawner.SpawnAi(CampaignRosterPlan.SpawnFor(plan, pos, look, pilot, name));
                CampaignRosterPlan.ApplyPlan(pilot, plan, skills.MinAiActiveDist);
                director.RegisterGeneratorLaunch(name, launched, plan);
                return launched;
            }
            runtime = new AiGeneratorRuntime(new[] { def },
                (name, scope) => world.Runtime.FindNodes(name, scope) is { Count: > 0 } hits ? hits[0] : null,
                nets, ctx.PlaneName, SpawnFromGenerator);
            generators = runtime;
            ctx.Same(1, runtime.LiveCount, $"'{Generator}' is live over the built world");

            // The Dante at the zeppelin record's authored pose, where ZeppelinRuntime.Place puts it
            // in a session: the gamez node's own pose sits under the 150 m launch gate.
            var host = world.Runtime.FindNodes(Generator) is { Count: > 0 } hosts ? hosts[0] : null;
            ZeppelinDef? record = null;
            foreach (var z in Zeppelins.Load(missionZrdr))
            {
                if (z.Node.Equals(Generator, StringComparison.OrdinalIgnoreCase))
                {
                    record = z;
                }
            }
            ctx.Check(host != null && record != null, $"'{Generator}' is a world node with a zeppelin record");
            if (host == null || record == null)
            {
                return;
            }
            report.AppendLine($"host: '{Generator}' gamez pose y={host.GlobalPosition.Y:0}, record pose y={record.Position.Y:0}, launch gate {def.MinAltitude?.ToString("0") ?? "-"} m");
            host.GlobalPosition = record.Position;

            // The bay has run uncredited since load (40 s in the logged run), so it is past its
            // first due point and a credit launches on the step it lands.
            for (int i = 0; i < (int)(PreRollS / StepDt); i++)
            {
                runtime.SimStep(StepDt);
            }
            ctx.Same(0, runtime.LaunchOrdinal, $"nothing launches uncredited in {PreRollS:0} s");
            // The torpedo shape: the fourth gasbag dies inside OBJECTIVE10's 0.5 s nap, so the
            // Dante's kill reaches the bay before OBJECTIVE11's credit does.
            ctx.Same(1, runtime.NotifyHostDied(Generator), $"the Dante's kill puts '{Generator}' on the launch grace");
            for (int i = 0; i < (int)(KillLeadS / StepDt); i++)
            {
                runtime.SimStep(StepDt);
            }
            ctx.Same(1, runtime.GrantWaveCapacity(Generator, 1), $"the script's WAKEUP_GENERATOR credit is granted");
            float waited = 0f;
            while (waited < SpawnWaitS && !director.Roster.ContainsKey(Launch))
            {
                runtime.SimStep(StepDt);
                waited += StepDt;
            }
            report.AppendLine($"launch: '{Launch}' in the roster after {waited:0.00} s (ordinal {runtime.LaunchOrdinal})");
            ctx.Check(director.Roster.TryGetValue(Launch, out var miles),
                $"the launch enters the campaign roster as '{Launch}' within {SpawnWaitS:0} s, the credit landing {KillLeadS:0.0} s after the kill");
            if (miles == null)
            {
                return;
            }
            ctx.Same(MilesGroup, miles.Group ?? -1, $"…carrying the template's group {MilesGroup}");
            int? after = director.GroupLiveCount(MilesGroup);
            report.AppendLine($"dedg: group {MilesGroup} counts {after?.ToString() ?? "-"} with Miles flying");
            ctx.Same(1, after ?? -1, $"DEDG counts the launch as group {MilesGroup}'s one live member");

            var graph = director.Graph;
            ctx.Check(graph != null, $"the world phase armed the objective graph");
            if (graph == null)
            {
                return;
            }
            graph.Wake(FuseObjective);
            for (int i = 0; i < (int)(SettleS / StepDt); i++)
            {
                director.Step(StepDt);
            }
            report.AppendLine($"fuse: OBJECTIVE{FuseObjective} completed={graph.CompletedOf(FuseObjective)} with Miles flying");
            ctx.Check(!graph.CompletedOf(FuseObjective),
                $"OBJECTIVE{FuseObjective} stays incomplete while Miles flies: the loss fuse is not lit");
            ctx.Check(graph.StateOf(LossObjective) == ObjectiveState.Dormant,
                $"…and OBJECTIVE{LossObjective} is still dormant");

            miles.DebugForceCrash();
            for (int i = 0; i < (int)(SettleS / StepDt); i++)
            {
                director.Step(StepDt);
            }
            int? down = director.GroupLiveCount(MilesGroup);
            report.AppendLine($"dedg: group {MilesGroup} counts {down?.ToString() ?? "-"} with Miles down; "
                + $"OBJECTIVE{FuseObjective} completed={graph.CompletedOf(FuseObjective)}, OBJECTIVE{LossObjective} {graph.StateOf(LossObjective)}");
            ctx.Same(0, down ?? -1, $"DEDG drops the launch once Miles is shot down");
            ctx.Check(graph.CompletedOf(FuseObjective),
                $"OBJECTIVE{FuseObjective} completes on Miles going down");
            ctx.Check(graph.StateOf(LossObjective) == ObjectiveState.Napping,
                $"…and OBJECTIVE{LossObjective} is napping towards the loss");
        }
        finally
        {
            generators?.Free();
            player?.Free();
            var members = new List<FlightController>(
                roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            foreach (var rig in members)
            {
                rig.Free();
            }
            pool?.Free();
            textures.Dispose();
        }
    }

    private static ObjectiveDef? ObjectiveNumbered(ObjectiveScript script, int number) =>
        number >= 1 && number <= script.Objectives.Count ? script.Objectives[number - 1] : null;

    private static List<object?>? Fields(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, string name)
    {
        foreach (var (block, fields) in blocks)
        {
            if (block.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return fields;
            }
        }
        return null;
    }

    private static (Vector3 Position, Vector3 Forward) PlayerPose(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks)
    {
        if (Fields(blocks, CampaignRosterPlan.PlayerBlock) is { } fields
            && AiSkills.RosterSpawnPose(fields) is { } pose)
        {
            return (pose.Position, new Basis(Vector3.Up, Mathf.DegToRad(pose.YawDeg)) * Vector3.Forward);
        }
        return (new Vector3(0f, 800f, 0f), Vector3.Forward);
    }

    // The human rig the leader pass and the conditions resolve 'player' against; it flies nothing.
    private static FlightController HumanRig(TestContext ctx, GameZ planesGamez,
        TextureArchive textures, ProjectilePool live, Vector3 pos, Vector3 lookAt)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                ? PlaneDamage.For(stats) : null,
            PlayerIndex = FlightRoster.ShooterIdBase - 1,
            IsHumanPiloted = true,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = AimAssist.PlayerTeam,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), pos, lookAt);
        rig.Name = "player1";
        ctx.Host.AddChild(rig);
        return rig;
    }

    // The session's own AI spawner with no world effects, the shape CampaignSquadWakeSuites uses.
    private static FlightRoster Spawner(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        ProjectilePool live)
    {
        var spec = SessionSpec.Parse(Array.Empty<string>());
        var resources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
            AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
            PaintRng = new RandomNumberGenerator(),
            ZrdrPath = ctx.ZrdrPath,
            StockLoadouts = StockLoadouts.Load(),
            WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
            WeaponMessages = Messages.Load(ctx.MessagesPath),
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            null!, ctx.Host, resources,
            new FlightWorldBindings { Projectiles = live, Gamez = planesGamez },
            new HumanRosterBindings());
    }
}
