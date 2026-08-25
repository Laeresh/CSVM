using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>BL-500: a mission re-commanding aircraft it already spawned. <c>SET_AI_NET</c>,
/// <c>SET_AI_TEAM</c> and <c>SET_AI_ATTACK_RADIUS</c> are one lookup with three writes, and CM02
/// is the worked case: OBJECTIVE4 puts the three Balmorals on <c>M5Bombrun</c> and OBJECTIVE68
/// puts the woken ace squad on <c>M5Escort</c> two seconds after it appears. Everything driven
/// here is read off the LIVE followers over C3/M05's own built world, never off the objective
/// having run. The two verbs with no shipped clause are driven through a synthetic objective
/// appended to this mission's own parsed script, which is the only way to reach them at all.
/// ⚠ Both verbs also take a zeppelin name; that arm has no seam and is not covered here.</summary>
internal static class CampaignSetAiSuites
{
    private const string Chapter = "C3";
    private const string Mission = "M05";

    private const int BombRunObjective = 4;
    private const int GateObjective = 5;
    private const int EscortObjective = 68;
    private const int PostpickObjective = 23;
    private const float NapSeconds = 15f;
    private const float NetSwapSeconds = 2f;
    private const float StepDt = 1f / 60f;

    private const string BombRunNet = "M5Bombrun";

    /// <summary>The net the Balmoral blocks author for themselves (aiv net id 19), which is a
    /// different route from the bomb run the mission moves them onto. It is what they fly for the
    /// whole mission while <c>SET_AI_NET</c> is a no-op.</summary>
    private const string StartNet = "M5Bombers";

    private const string EscortNet = "M5Escort";
    private const string PostpickNet = "M5Postpick";

    // The synthetic clause's writes: a team the Balmoral's own block does not author (it ships 2)
    // and an attack radius nothing in the install authors, so neither can be read off the roster.
    private const int SyntheticTeam = 1;
    private const float SyntheticRadiusM = 1234f;
    private const string AbsentName = "no_such_aircraft";

    private static readonly string[] Balmorals =
        { "britbalmoral_1", "britbalmoral_2", "britbalmoral_3" };

    private static readonly string[] Squad = { "britpeace_7", "britpeace_8", "britpeace_9" };
    private static readonly string[] FirstSquad = { "britpeace_1", "britpeace_2", "britpeace_3" };

    internal static void CampaignSetAiNet(TestContext ctx)
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

        var shipped = ObjectiveScript.Load(missionZrdr);
        var blocks = AiSkills.LoadRoster(missionZrdr);
        var nets = AiNets.Load(chapterZrdr);
        var report = new StringBuilder();
        CheckAuthored(ctx, shipped, blocks, nets, report);

        // The shipped script plus one appended objective carrying the two verbs no mission
        // authors. Appended past the last number, so the shipped ones keep their numbering and
        // the parser's contiguous-from-1 walk still reaches every one of them.
        int synthetic = shipped.Objectives.Count + 1;
        var script = ObjectiveScript.Parse(
            WithSyntheticObjective(Zrdr.LoadFileOrEmpty(missionZrdr, "objectives.json"), synthetic));
        ctx.Same(synthetic, script.Objectives.Count,
            $"the driven script is the shipped one with a single objective appended");

        var skills = AiSkills.Load(ctx.ZrdrPath);
        var director = CampaignDirector.Create(script, mission,
            CampaignProfileDef.NewProfile("Zachary"), null);
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
            Drive(ctx, world, director, blocks, nets, skills, synthetic,
                missionZrdr, chapterZrdr, texturesPath, report));

        ctx.WriteArtifact($"test-campaign-set-ai-net-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"{Chapter}/{Mission}: the mission's own SET_AI_* clauses reach the aircraft it spawned");
    }

    // The authored shape, read off the shipped files: the three clauses CM02 fires at its own
    // aircraft, the nets they name, and the fact that no Balmoral block authors a net of its own.
    private static void CheckAuthored(TestContext ctx, ObjectiveScript script,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks,
        IReadOnlyList<AiNet> nets, StringBuilder report)
    {
        ctx.Check(NamesOnto(script, BombRunObjective, BombRunNet, Balmorals),
            $"OBJECTIVE{BombRunObjective} puts exactly the three Balmorals onto '{BombRunNet}'");
        ctx.Check(NamesOnto(script, EscortObjective, EscortNet, Squad),
            $"OBJECTIVE{EscortObjective} puts exactly the ace squad onto '{EscortNet}'");
        ctx.Check(script.ByNumber(PostpickObjective)?.SetAiNet.Count == 6,
            $"OBJECTIVE{PostpickObjective} names all six Peacemakers for '{PostpickNet}'");

        int ownRoute = 0;
        foreach (var name in Balmorals)
        {
            var ids = Fields(blocks, name) is { } f ? AiSkills.RosterNetIds(f) : new List<int>();
            var own = ids.Count == 1 ? AiNets.ById(nets, ids[0]) : null;
            ownRoute += own != null
                && !own.Name.Equals(BombRunNet, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            report.AppendLine($"authored: {name} netids [{string.Join(", ", ids)}] = '{own?.Name ?? "-"}'");
        }
        ctx.Same(3, ownRoute,
            $"each Balmoral block authors one net of its own that is not '{BombRunNet}'");

        var bombrun = AiNets.ByName(nets, BombRunNet);
        ctx.Check(bombrun != null && AiNets.ByName(nets, EscortNet) != null,
            $"this chapter carries both '{BombRunNet}' and '{EscortNet}'");
        if (bombrun is { } net)
        {
            report.AppendLine($"authored: '{net.Name}#{net.Id}' {net.Nodes.Count} nodes, "
                + $"volumes act={net.Volumes.Activation.Radius:0.##} att={net.Volumes.Attack.Radius:0.##} "
                + $"ret={net.Volumes.Return.Radius:0.##}");
        }
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, IReadOnlyList<AiNet> nets,
        AiSkills skills, int synthetic, string missionZrdr, string chapterZrdr,
        string texturesPath, StringBuilder report)
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
                MinAiActiveDist = skills.MinAiActiveDist,
                Player = () => human,
                NetTrailers = new NetTrailerTargets(
                    () => human.WorldPosition,
                    name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                FindNodes = name => world.Runtime.FindNodes(name),
                Spawn = (plan, pos, look, pilot) => roster!.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                Rng = new Random(1),
            });

            var rigs = director.Roster;
            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = world.Runtime.Sounds,
                Projectiles = live,
                ListenerPosition = () => human.WorldPosition,
                PlayerAircraft = () => human,
                Rng = new Random(1),
            });
            ctx.Check(director.Graph != null, $"the world phase armed the objective graph");
            if (director.Graph is not { } graph)
            {
                return;
            }

            BombRun(ctx, director, graph, rigs, nets, skills, report);
            EscortSwap(ctx, director, graph, rigs, report);
            Synthetic(ctx, director, graph, rigs, synthetic, report);
        }
        finally
        {
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

    // OBJECTIVE4's clause, driven: the Balmorals start with no follower at all, and the objective
    // completing has to be what puts all three on the bomb run.
    private static void BombRun(TestContext ctx, CampaignDirector director, ObjectiveGraph graph,
        IReadOnlyDictionary<string, FlightController> rigs, IReadOnlyList<AiNet> nets,
        AiSkills skills, StringBuilder report)
    {
        int onOwnRoute = 0;
        foreach (var name in Balmorals)
        {
            var patrol = rigs.TryGetValue(name, out var rig) ? rig.Pilot?.Patrol : null;
            onOwnRoute += patrol?.Net.Name.Equals(StartNet, StringComparison.OrdinalIgnoreCase) == true
                ? 1 : 0;
            report.AppendLine($"start: {name} flies '{patrol?.Net.Name ?? "-"}'");
        }
        ctx.Same(3, onOwnRoute,
            $"the three Balmorals fly their own '{StartNet}' before the mission commands them");

        // Parked far down the net before the swap, so "seats where the aeroplane is" is a claim
        // the seat can fail: node 0 is over 4 km from here.
        var net = AiNets.ByName(nets, BombRunNet);
        int parkNode = net != null ? LastEdgedNode(net) : -1;
        if (net != null && parkNode >= 0 && rigs.TryGetValue(Balmorals[2], out var parked))
        {
            var at = net.Nodes[parkNode].Position;
            parked.Held = true;
            parked.PlaceHeld(at, at + Vector3.Forward);
            report.AppendLine($"park: {Balmorals[2]} put on '{BombRunNet}' node {parkNode} before the swap");
        }

        graph.Wake(BombRunObjective);
        for (int i = 0; i < 600 && !graph.CompletedOf(BombRunObjective); i++)
        {
            director.Step(StepDt);
        }
        ctx.Check(graph.CompletedOf(BombRunObjective),
            $"OBJECTIVE{BombRunObjective} completed through the real graph");

        int onBombRun = 0, seated = 0;
        foreach (var name in Balmorals)
        {
            var patrol = rigs.TryGetValue(name, out var rig) ? rig.Pilot?.Patrol : null;
            if (patrol == null || rig == null)
            {
                continue;
            }

            onBombRun += patrol.Net.Name.Equals(BombRunNet, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            patrol.Update(rig.WorldPosition);
            int want = NearestEdgedNode(patrol, rig.WorldPosition);
            seated += patrol.CurrentIndex == want ? 1 : 0;
            report.AppendLine($"net: {name} flies '{patrol.Net.Name}#{patrol.Net.Id}' "
                + $"seated at node {patrol.CurrentIndex} (nearest to it is {want})");
        }
        ctx.Same(3, onBombRun,
            $"all three Balmorals fly '{BombRunNet}' once OBJECTIVE{BombRunObjective} has run");
        ctx.Same(3, seated,
            $"each captures the new route at the node nearest where it is, not at node 0");
        if (parkNode > 0 && rigs.TryGetValue(Balmorals[2], out var late))
        {
            ctx.Same(parkNode, late.Pilot?.Patrol?.CurrentIndex ?? -1,
                $"the one parked down the net seats at node {parkNode} rather than restarting the route");
        }

        // The net's own volumes overwrite the vehicle's where it authors them, with no second
        // roster-block pass behind them the way the spawn has.
        if (net != null && net.Volumes.Attack.Radius != 0f
            && rigs.TryGetValue(Balmorals[0], out var lead) && lead.Pilot?.Machine is { } machine)
        {
            ctx.Check(Mathf.IsEqualApprox(machine.AttackRange, net.Volumes.Attack.Radius),
                $"'{BombRunNet}'s own attack radius reached the aeroplane: {machine.AttackRange:0.##} m");
            ctx.Check(machine.ActivationRange >= skills.MinAiActiveDist,
                $"…and the activation radius keeps its min_ai_active_dist floor: {machine.ActivationRange:0} m");
        }
    }

    // OBJECTIVE68's clause, driven through the mission's own wake chain: group 1 goes down,
    // OBJECTIVE5's nap wakes OBJECTIVE8, and that naps OBJECTIVE68 awake two seconds later.
    private static void EscortSwap(TestContext ctx, CampaignDirector director, ObjectiveGraph graph,
        IReadOnlyDictionary<string, FlightController> rigs, StringBuilder report)
    {
        foreach (var name in FirstSquad)
        {
            if (rigs.TryGetValue(name, out var rig))
            {
                rig.DebugForceCrash();
            }
        }

        float elapsed = 0f, woke = -1f;
        float limit = (NapSeconds + NetSwapSeconds) * 3f;
        while (elapsed < limit)
        {
            director.Step(StepDt);
            elapsed += StepDt;
            if (woke < 0f && InPlayCount(rigs) == Squad.Length)
            {
                woke = elapsed;
            }
            if (woke > 0f && elapsed > woke + (NetSwapSeconds * 2f))
            {
                break;
            }
        }
        ctx.Check(woke >= NapSeconds, $"the ace squad is in the world on its authored nap: {woke:0.00} s");
        ctx.Check(graph.CompletedOf(EscortObjective),
            $"OBJECTIVE{EscortObjective} ran {elapsed - woke:0.00} s after the squad appeared");

        int onEscort = 0;
        foreach (var name in Squad)
        {
            var patrol = rigs.TryGetValue(name, out var rig) ? rig.Pilot?.Patrol : null;
            onEscort += patrol?.Net.Name.Equals(EscortNet, StringComparison.OrdinalIgnoreCase) == true
                ? 1 : 0;
            report.AppendLine($"net: {name} flies '{patrol?.Net.Name ?? "-"}' after the wake chain");
        }
        ctx.Same(3, onEscort, $"the woken ace squad is on '{EscortNet}', read off its live followers");
    }

    // The two verbs no shipped mission authors, driven through the appended objective. The clause
    // also names an aircraft that is not there, which must not cost the rest of it.
    private static void Synthetic(TestContext ctx, CampaignDirector director, ObjectiveGraph graph,
        IReadOnlyDictionary<string, FlightController> rigs, int number, StringBuilder report)
    {
        if (!rigs.TryGetValue(Balmorals[0], out var lead) || lead.Pilot?.Machine is not { } machine)
        {
            return;
        }

        int before = lead.Team;
        if (lead.Pilot.Gunner is { } gunner)
        {
            gunner.Target = rigs.TryGetValue(Balmorals[1], out var other) ? other : null;
        }

        graph.Wake(number);
        for (int i = 0; i < 600 && !graph.CompletedOf(number); i++)
        {
            director.Step(StepDt);
        }
        ctx.Check(graph.CompletedOf(number), $"the appended OBJECTIVE{number} completed");
        report.AppendLine($"synthetic: {Balmorals[0]} team {before} -> {lead.Team}, "
            + $"attack radius {machine.AttackRange:0.##} m");

        ctx.Check(before != SyntheticTeam && lead.Team == SyntheticTeam,
            $"SET_AI_TEAM moved '{Balmorals[0]}' off its authored team {before} onto {SyntheticTeam}");
        ctx.Check(lead.Pilot.Gunner?.Target == null,
            $"…and dropped the target the vtable setter drops with it");
        ctx.Check(Mathf.IsEqualApprox(machine.AttackRange, SyntheticRadiusM),
            $"SET_AI_ATTACK_RADIUS wrote the attack volume: {machine.AttackRange:0.##} m");
        ctx.Check(rigs.TryGetValue(Balmorals[1], out var second)
            && second.Team == SyntheticTeam,
            $"'{AbsentName}' naming nothing does not cost the clause its other entries");
    }

    // The shipped body with one more objective on the end, carrying the two verbs authored
    // nowhere in the install. The file is a flat alternating key/value list, so this is an append.
    private static List<object?> WithSyntheticObjective(List<object?> root, int number)
    {
        if (root.Count == 0 || root[0] is not List<object?> body)
        {
            return root;
        }

        body.Add("OBJECTIVE" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        body.Add(new List<object?>
        {
            "BEGIN_DORMANT",
            new List<object?> { -1f },
            "SET_AI_TEAM",
            new List<object?>
            {
                new List<object?> { Balmorals[0], (float)SyntheticTeam },
                new List<object?> { AbsentName, (float)SyntheticTeam },
                new List<object?> { Balmorals[1], (float)SyntheticTeam },
            },
            "SET_AI_ATTACK_RADIUS",
            new List<object?> { new List<object?> { Balmorals[0], SyntheticRadiusM } },
        });
        return root;
    }

    private static bool NamesOnto(ObjectiveScript script, int number, string net,
        IReadOnlyList<string> names)
    {
        var entries = script.ByNumber(number)?.SetAiNet;
        if (entries == null || entries.Count != names.Count)
        {
            return false;
        }

        foreach (var want in names)
        {
            bool hit = false;
            foreach (var (name, onto) in entries)
            {
                hit |= name.Equals(want, StringComparison.OrdinalIgnoreCase)
                    && onto.Equals(net, StringComparison.OrdinalIgnoreCase);
            }
            if (!hit)
            {
                return false;
            }
        }
        return true;
    }

    // The follower's own seat rule, restated so the seat can be checked against it: nearest node
    // that carries an edge, in the follower's live (trailer-offset) space.
    private static int NearestEdgedNode(AiNetFollower patrol, Vector3 position)
    {
        int best = -1;
        float bestSq = float.MaxValue;
        for (int i = 0; i < patrol.Net.Nodes.Count; i++)
        {
            if (!HasEdge(patrol.Net, i))
            {
                continue;
            }

            float dSq = patrol.NodePosition(i).DistanceSquaredTo(position);
            if (dSq < bestSq)
            {
                bestSq = dSq;
                best = i;
            }
        }
        return best;
    }

    private static int LastEdgedNode(AiNet net)
    {
        int best = -1;
        for (int i = 0; i < net.Nodes.Count; i++)
        {
            best = HasEdge(net, i) ? i : best;
        }
        return best;
    }

    private static bool HasEdge(AiNet net, int index)
    {
        bool any = false;
        foreach (var (a, b) in net.Edges)
        {
            any |= (a == index || b == index) && a != b;
        }
        return any;
    }

    private static int InPlayCount(IReadOnlyDictionary<string, FlightController> rigs)
    {
        int n = 0;
        foreach (var name in Squad)
        {
            n += rigs.TryGetValue(name, out var rig) && rig.InPlay ? 1 : 0;
        }
        return n;
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

    // The human rig this suite flies nothing with: the leader pass needs a player and an anchored
    // net needs something to ride.
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

    // The session's own AI spawner with no world effects, the shape the sibling campaign suites use.
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
