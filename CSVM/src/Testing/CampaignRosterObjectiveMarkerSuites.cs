using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>CM15's Tex, <c>balmoral_1</c>, is the shipped block that authors all three
/// objective-marker slots at once: the flag (aiv slot 37), a category label (slot 38,
/// <c>MSG_BOMBER_NAME</c>, the only one in the install) and a help label (slot 39,
/// <c>MSG_OBJ_DEFEND</c>), on top of a slot-20 pilot name and <c>deactivated</c>. Drives C2/M05's
/// own roster and objective graph and checks the marker rides the aeroplane's own candidate: no
/// candidate at all while the block is still asleep, exactly ONE once OBJECTIVE21's
/// <c>WAKEUP_ENEMIES</c> puts it in the world, named and labelled off its own block, and none
/// again once it is shot down.</summary>
internal static class CampaignRosterObjectiveMarkerSuites
{
    private const string Chapter = "C2";
    private const string Mission = "M05";
    private const string Block = "balmoral_1";
    private const string PilotName = "Tex";
    private const string TypeLabel = "Bomber";
    private const string Category = "Defend";

    // OBJECTIVE21, the shipped dormant objective whose WAKEUP_ENEMIES names exactly this block.
    private const int WakeObjective = 21;

    private const string RelabelChapter = "C3";
    private const string RelabelMission = "M05";
    // Enough ticks for the graph's per-tick scan slice to reach all three woken writers.
    private const int ScanTicks = 24;

    private static readonly string[] Balmorals = { "britbalmoral_1", "britbalmoral_2", "britbalmoral_3" };

    [Suite("campaign-cm15-roster-objective-marker",
        "CM15's Tex over C2/M05's own BUILT roster and graph: balmoral_1 authors the roster's "
        + "objectiveTarget flag with both label slots and a slot-20 pilot name, and its marker "
        + "rides the aeroplane's own candidate — nothing offered while the block sleeps, exactly "
        + "one Objective-ranked target named Tex once OBJECTIVE21 wakes it, nothing after it dies")]
    internal static void CampaignCm15RosterObjectiveMarker(TestContext ctx)
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

        var blocks = AiSkills.LoadRoster(missionZrdr);
        var report = new StringBuilder();
        CheckAuthored(ctx, blocks, report);

        var script = ObjectiveScript.Load(missionZrdr);
        var targets = MissionTargets.Load(missionZrdr, chapterZrdr);
        var messages = Messages.Load(ctx.MessagesPath);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
            Drive(ctx, world, director, blocks, missionZrdr, chapterZrdr, texturesPath, targets,
                messages, report));

        ctx.WriteArtifact($"test-campaign-cm15-roster-objective-marker-{Chapter}-{Mission}.txt",
            report.ToString());
        ctx.Note($"{Chapter}/{Mission}: '{Block}' carries its own marker on its own aircraft, named '{PilotName}'");
    }

    [Suite("campaign-cm02-roster-marker-relabel",
        "CM02's three Balmorals over C3/M05's own BUILT roster and graph: each authors the marker "
        + "flag with MSG_OBJ_DESTROY, and the script's own SET_HELP_LABEL objectives move every "
        + "bracket to Dock and then to Dock Escort on the aeroplane's own candidate, one offer "
        + "throughout")]
    internal static void CampaignCm02RosterMarkerRelabel(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RelabelChapter, RelabelMission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, RelabelChapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, RelabelChapter);
        ctx.RequireData(missionZrdr, $"{RelabelChapter}/{RelabelMission} zrdr");
        ctx.RequireData(chapterZrdr, $"{RelabelChapter} zrdr");
        ctx.RequireData(texturesPath, $"{RelabelChapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(RelabelChapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(RelabelMission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{RelabelChapter}/{RelabelMission} is not in cm_sequence");
        }

        var blocks = AiSkills.LoadRoster(missionZrdr);
        var report = new StringBuilder();
        var script = ObjectiveScript.Load(missionZrdr);
        var targets = MissionTargets.Load(missionZrdr, chapterZrdr);
        var messages = Messages.Load(ctx.MessagesPath);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        ctx.WithWorld(RelabelChapter, collision: false, RelabelMission, world =>
            DriveRelabel(ctx, world, director, blocks, missionZrdr, chapterZrdr, texturesPath,
                targets, messages, script, report));

        ctx.WriteArtifact(
            $"test-campaign-cm02-roster-marker-relabel-{RelabelChapter}-{RelabelMission}.txt",
            report.ToString());
        ctx.Note($"{RelabelChapter}/{RelabelMission}: the three Balmorals' brackets follow the script's SET_HELP_LABEL");
    }

    // The shipped block: all four authored slots, read off the real roster rather than assumed.
    private static void CheckAuthored(TestContext ctx,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, StringBuilder report)
    {
        if (Fields(blocks, Block) is not { } fields)
        {
            ctx.Check(false, $"{Chapter}/{Mission} authors a '{Block}' roster block");
            return;
        }

        report.AppendLine($"'{Block}': objectiveTarget={AiSkills.RosterObjectiveTarget(fields)} "
            + $"title='{AiSkills.RosterTitle(fields)}' categoryLabel='{AiSkills.RosterCategoryLabel(fields)}' "
            + $"helpLabel='{AiSkills.RosterHelpLabel(fields)}' deactivated={AiSkills.RosterDeactivated(fields)}");
        ctx.Check(AiSkills.RosterObjectiveTarget(fields),
            $"'{Block}' authors the roster's own objectiveTarget flag (slot 37)");
        ctx.Check(AiSkills.RosterCategoryLabel(fields) == "MSG_BOMBER_NAME",
            $"'{Block}' authors the install's one categoryLabel (slot 38)");
        ctx.Check(AiSkills.RosterHelpLabel(fields) == "MSG_OBJ_DEFEND",
            $"'{Block}' authors the MSG_OBJ_DEFEND help label (slot 39)");
        ctx.Check(AiSkills.RosterTitle(fields) == "MSG_TEX_NAME",
            $"'{Block}' authors the pilot name the marker prints (slot 20)");
        ctx.Check(AiSkills.RosterDeactivated(fields),
            $"'{Block}' ships deactivated, so the pre-wake case is the shipped one");
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks,
        string missionZrdr, string chapterZrdr, string texturesPath,
        MissionTargets targets, Messages messages, StringBuilder report)
    {
        var textures = new TextureArchive(texturesPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        ProjectilePool? pool = null;
        FlightRoster? roster = null;
        FlightController? player = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            roster = Spawner(ctx, planesGamez, textures, live);

            var pose = PlayerPose(blocks);
            player = HumanRig(ctx, planesGamez, textures, live, pose.Position, pose.Position + pose.Forward);
            var human = player;

            director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = chapterZrdr,
                MissionZrdrPath = missionZrdr,
                ZrdrPath = ctx.ZrdrPath,
                MinAiActiveDist = AiSkills.Load(ctx.ZrdrPath).MinAiActiveDist,
                Player = () => human,
                NetTrailers = new NetTrailerTargets(
                    () => human.WorldPosition,
                    name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                FindNodes = name => world.Runtime.FindNodes(name),
                // The production spawn and nothing else: whether the block's own slots survive the
                // assembler onto the aeroplane is the whole question here.
                Spawn = (plan, pos, look, pilot) => roster!.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                Rng = new Random(1),
            });

            var rigs = director.Roster;
            if (!rigs.TryGetValue(Block, out var tex))
            {
                ctx.Check(false, $"'{Block}' spawns into the roster");
                return;
            }

            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Gamez = world.Gamez,
                Strings = messages,
                Sounds = world.Runtime.Sounds,
                Projectiles = live,
                ListenerPosition = () => human.WorldPosition,
                PlayerAircraft = () => human,
                Rng = new Random(1),
            });
            var graph = director.Graph!;
            graph.Step(0.1f);
            var sites = new ObjectiveSites(director, messages, targets, world.Runtime);

            ctx.Check(tex.Inert && !tex.InPlay, $"'{Block}' is out of the world before its wake");
            var asleep = Offered(sites, rigs, report, "asleep");
            ctx.Same(0, asleep.Count, $"'{Block}' is not selectable at all before it wakes");

            graph.Wake(WakeObjective);
            ctx.Check(tex.InPlay, $"OBJECTIVE{WakeObjective}'s WAKEUP_ENEMIES puts '{Block}' in the world");

            var awake = Offered(sites, rigs, report, "awake");
            ctx.Same(1, awake.Count, $"'{Block}' is offered exactly once across all three cycles");
            if (awake.Count == 1)
            {
                var t = awake[0];
                ctx.Check(t.Objective && t.Class == TargetClass.Enemy && t.SortsFirst,
                    $"…as an Objective on the Enemy cycle, ahead of every Enemy Target: class={t.Class} sortsFirst={t.SortsFirst}");
                ctx.Check(t.DisplayName == PilotName,
                    $"…printing its block's own slot-20 name rather than the airframe's: '{t.DisplayName}'");
                ctx.Check(t.TypeLabel == TypeLabel && t.Category == Category,
                    $"…with both label halves off slots 38 and 39: \"{t.CategoryLine}\"");
                ctx.Check(ReferenceEquals(t.Source, tex),
                    $"…and the selection is held by the aeroplane itself, not a synthetic site");
            }

            tex.DebugForceCrash(human.PlayerIndex);
            var dead = Offered(sites, rigs, report, "shot down");
            ctx.Same(0, dead.Count, $"'{Block}' leaves the cycle once it is shot down");
        }
        finally
        {
            player?.Free();
            var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            foreach (var m in members)
            {
                m.QueueFree();
            }
            pool?.QueueFree();
            textures.Dispose();
        }
    }

    private static void DriveRelabel(TestContext ctx, TestWorld world, CampaignDirector director,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks,
        string missionZrdr, string chapterZrdr, string texturesPath,
        MissionTargets targets, Messages messages, ObjectiveScript script, StringBuilder report)
    {
        var textures = new TextureArchive(texturesPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        ProjectilePool? pool = null;
        FlightRoster? roster = null;
        FlightController? player = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            roster = Spawner(ctx, planesGamez, textures, live);

            var pose = PlayerPose(blocks);
            player = HumanRig(ctx, planesGamez, textures, live, pose.Position, pose.Position + pose.Forward);
            var human = player;

            director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = chapterZrdr,
                MissionZrdrPath = missionZrdr,
                ZrdrPath = ctx.ZrdrPath,
                MinAiActiveDist = AiSkills.Load(ctx.ZrdrPath).MinAiActiveDist,
                Player = () => human,
                NetTrailers = new NetTrailerTargets(
                    () => human.WorldPosition,
                    name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                FindNodes = name => world.Runtime.FindNodes(name),
                Spawn = (plan, pos, look, pilot) => roster!.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                Rng = new Random(1),
            });

            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Gamez = world.Gamez,
                Strings = messages,
                Sounds = world.Runtime.Sounds,
                Projectiles = live,
                ListenerPosition = () => human.WorldPosition,
                PlayerAircraft = () => human,
                Rng = new Random(1),
            });
            var graph = director.Graph!;
            graph.Step(0.1f);
            var sites = new ObjectiveSites(director, messages, targets, world.Runtime);
            var rigs = director.Roster;

            CheckBracket(ctx, sites, rigs, report, "at mission start", "Destroy");
            WakeWriters(ctx, graph, script, "MSG_OBJ_DOCK", report);
            CheckBracket(ctx, sites, rigs, report, "once the dock label is written", "Dock");
            WakeWriters(ctx, graph, script, "MSG_OBJ_DOCK_ESCORT", report);
            CheckBracket(ctx, sites, rigs, report, "once the escort label is written",
                "Dock: Destroy Escort first");
        }
        finally
        {
            player?.Free();
            var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            foreach (var m in members)
            {
                m.QueueFree();
            }
            pool?.QueueFree();
            textures.Dispose();
        }
    }

    // The mission's OWN objectives that write the wanted key, found in the script rather than
    // named by number here, then woken and ticked: each is conditionless past its dormancy, so it
    // completes on the first step and its SET_HELP_LABEL runs through the real directive path.
    private static void WakeWriters(TestContext ctx, ObjectiveGraph graph, ObjectiveScript script,
        string key, StringBuilder report)
    {
        int woken = 0;
        foreach (var def in script.Objectives)
        {
            if (def.HelpLabel is { } label && label.MessageKey == key)
            {
                graph.Wake(def.Number);
                woken++;
                report.AppendLine($"woke OBJECTIVE{def.Number}, which writes {key}");
            }
        }

        // The graph's scan resolves a bounded slice of the live rows per tick, the original's own
        // budget, so three woken conditionless objectives need more than one step to all complete.
        for (int i = 0; i < ScanTicks; i++)
        {
            graph.Step(0.1f);
        }

        ctx.Same(Balmorals.Length, woken, $"the mission authors one {key} writer per Balmoral");
    }

    private static void CheckBracket(TestContext ctx, ObjectiveSites sites,
        IReadOnlyDictionary<string, FlightController> rigs, StringBuilder report, string when,
        string expected)
    {
        foreach (var name in Balmorals)
        {
            var hits = OfferedFor(sites, rigs, name, report, when);
            ctx.Same(1, hits.Count, $"'{name}' is offered exactly once {when}");
            ctx.Check(hits.Count == 1 && hits[0].Objective
                      && string.Equals(hits[0].Category, expected, StringComparison.Ordinal),
                $"'{name}'s bracket reads '{expected}' {when}");
        }
    }

    // Every offer of the block, across all three cycles and both sources: the aeroplane's own
    // vehicle candidate and whatever the site collector still produces. Two entries here would be
    // the double offer this seam exists to prevent.
    private static List<TargetRef> Offered(ObjectiveSites sites,
        IReadOnlyDictionary<string, FlightController> rigs, StringBuilder report, string when) =>
        OfferedFor(sites, rigs, Block, report, when);

    private static List<TargetRef> OfferedFor(ObjectiveSites sites,
        IReadOnlyDictionary<string, FlightController> rigs, string block, StringBuilder report,
        string when)
    {
        var objectives = new List<AimCandidate>();
        sites.Collect(objectives);
        var scan = new AimCandidateSet();
        foreach (var rig in rigs.Values)
        {
            scan.AddVehicle(rig.WorldPosition, rig.WorldVelocity, rig.Team, rig.InPlay, rig);
        }

        var pool = new TargetPool();
        pool.Rebuild(scan, null, AimAssist.PlayerTeam, null, objectives);

        var hits = new List<TargetRef>();
        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            foreach (var target in pool.Of(cls))
            {
                if (string.Equals(target.Name, block, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(target);
                    report.AppendLine($"{when}: '{block}' on {cls} objective={target.Objective} "
                        + $"name='{target.DisplayName}' line1=\"{target.CategoryLine}\"");
                }
            }
        }
        if (hits.Count == 0)
        {
            report.AppendLine($"{when}: '{block}' not offered on any cycle");
        }
        return hits;
    }

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

    // The human rig this suite flies nothing with: it exists so the leader pass has a player and
    // the ranking has a human to resolve the authored 'player' role against.
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
