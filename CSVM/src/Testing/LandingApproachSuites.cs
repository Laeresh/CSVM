using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the mid-mission cutscene trigger: the chapter's <c>landings.zrd</c>
/// approach table resolved against a built world, armed by the mission's own objective chain, and
/// flown until it starts the drop.</summary>
internal static class LandingApproachSuites
{
    // The story positions flown. Every node, animation and objective name below is read out of that
    // mission's own data, so nothing here names a shipped world node by hand.
    private const int FirstSeq = 0;
    private const int WingWalkSeq = 1;
    private const float StepDt = 1f / 60f;
    private const string PlaneNode = "player_bhawk";

    // The flown approach: how far along the cone's own axis the aircraft starts, how long it is
    // given to reach the trigger, and the speed it flies at, which sits inside every band the
    // shipped table authors (50-320 mph).
    private const float AxisFraction = 0.6f;
    private const float ApproachSpeedMps = 45f;
    private const float ApproachThrottle = 0.5f;
    private const float ApproachBudgetS = 6f;

    // How long the graph is stepped for a nap chain to run out, and how long a started cutscene
    // definition is given to reach EXECUTED.
    private const float ArmBudgetS = 12f;
    private const float PlayBudgetS = 45f;

    // How long the trigger is ticked after a handoff to catch the row re-firing.
    private const int RestartFrames = 30;

    // What a landings suite does with the built world: the harness owns the build, the suite owns
    // the drive. The mission's own zrdr path comes with it, for the readers that are not the
    // objective script.
    private delegate void MissionDrive(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report);

    /// <summary>Drives the campaign's first mission's approach triggers against its BUILT world:
    /// the chapter's rows resolve to real cone/half-cone/sphere volumes, a mission carrying none of
    /// the animations arms none of them, the drop-off rows start disarmed, the mission's own
    /// objective chain arms them, and flying one cone starts the drop cutscene, which completes the
    /// primary objective gated on it.</summary>
    internal static void LandingApproachTrigger(TestContext ctx) =>
        DriveMission(ctx, FirstSeq, "test-landing-approach", Drive);

    /// <summary>Reads CM02's wing-walk capture gate against its BUILT world: three symmetric
    /// Balmoral rows, each under its own airship's gamez node, one pair of definitions switching
    /// all three <c>land_on</c> nodes, and an arming objective that waits on the airships' aiv
    /// group being down to one. It also pins BL-492, that none of those nodes is built, an
    /// airship's gamez node being a library root the roster spawns as an aircraft, so all three
    /// rows are dropped when the trigger binds.</summary>
    internal static void WingWalkCaptureGate(TestContext ctx) =>
        DriveMission(ctx, WingWalkSeq, "test-wingwalk-gate", DriveWingWalk);

    // The world build every landings suite needs: the story mission at this sequence position, its
    // objective script, an in-memory campaign profile, and the cutscene roots the definitions pose.
    private static void DriveMission(
        TestContext ctx,
        int seq,
        string artifactPrefix,
        MissionDrive drive)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), seq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {seq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder);
        ctx.RequireData(missionZrdr, $"{chapter}/{folder} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");

        var script = ObjectiveScript.Load(missionZrdr);
        var report = new StringBuilder();
        report.AppendLine($"seq {seq} -> {chapter}/{folder}");
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        ctx.ExtraPrewarmSoundNames = script.SoundGroupNames();
        // ⚠ The world has to carry the cutscene roots or nothing here sees the defect this suite
        // exists for: with no `camera1` the drop's own camera events resolve to nothing, the
        // definition reports zero length and the episode is over the frame it starts.
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder,
                world => drive(ctx, world, director, script, missionZrdr, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"{artifactPrefix}-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder}'s landings.zrd approach triggers against a built world");
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var armed = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        foreach (var approach in armed)
        {
            report.AppendLine($"row anim='{approach.Anim}' node='{approach.Node}' " +
                $"{approach.Shape} r={approach.Radius:0.0} auto={approach.Auto} " +
                $"angle={Mathf.RadToDeg(approach.AngleRad):0.#} " +
                $"speed={approach.MinSpeedMps:0.#}..{approach.MaxSpeedMps:0.#} m/s");
        }

        ctx.Check(armed.Count > 0, $"the chapter's landings.zrd resolves rows this mission can run");
        ctx.Same(0, LandingApproaches.Resolve(chapterZrdr, world.Gamez, _ => false).Count,
            $"and a mission carrying none of them resolves nothing, which keeps Instant Action out");
        CheckShapes(ctx, armed, report);
        WithTrigger(ctx, world, director, armed, (trigger, cutscene, rig, graph) =>
        {
            ctx.Same(armed.Count, trigger.Armed, $"every resolved row binds to a node this world built");
            RunTheDrop(ctx, world, graph, script, armed, trigger, cutscene, rig, report);
        });
    }

    // The session wiring a flown story mission has and the suite harness's world build does not:
    // the cutscene host over the table's own definition closure, the trigger bound to the resolved
    // rows, a rig for it to fly, and the campaign director attached to that rig.
    private static void WithTrigger(
        TestContext ctx,
        TestWorld world,
        CampaignDirector director,
        IReadOnlyList<LandingApproach> armed,
        Action<LandingApproachRuntime, CutsceneController, FlightController, ObjectiveGraph> body)
    {
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        cutscene.BindWorld(world.Runtime);
        cutscene.HostDefinitions(ClosureOf(world, armed));
        world.Runtime.CallbackHost = cutscene.Host;
        var trigger = new LandingApproachRuntime();
        ctx.Host.AddChild(trigger);

        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        var pool = new ProjectilePool(textures, null, null);
        FlightController? rig = null;
        ctx.Host.AddChild(pool);
        try
        {
            rig = BuildRig(ctx, world, pool);
            var craft = rig;
            trigger.Bind(world.Runtime, armed, cutscene, () => craft);
            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = world.Runtime.Sounds,
                ListenerPosition = () => craft.WorldPosition,
                PlayerAircraft = () => craft,
                Projectiles = pool,
                Rng = new Random(1),
            });
            body(trigger, cutscene, rig, director.Graph!);
        }
        finally
        {
            rig?.Free();
            pool.Free();
            textures.Dispose();
            trigger.Free();
            cutscene.Free();
        }
    }

    // CM02's capture gate, which is not a per-airship one: the mission arms and disarms its three
    // Balmoral approaches from one pair of ON_CALL definitions covering all three at once, and the
    // objective that calls the arming one waits on the airships' own aiv group being down to one.
    // Each row's approach node is a child of that airship's gamez node, so the volume follows the
    // airship it belongs to.
    private static void DriveWingWalk(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var rows = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        var blocks = AiSkills.LoadRoster(missionZrdr);
        var carried = new List<LandingApproach>();
        var owners = new List<string>();
        foreach (var row in rows)
        {
            string owner = TopAncestorOf(world.Gamez, row.Node);
            report.AppendLine($"row anim='{row.Anim}' node='{row.Node}' {row.Shape} " +
                $"r={row.Radius:0.0} auto={row.Auto} angle={Mathf.RadToDeg(row.AngleRad):0.#} " +
                $"speed={row.MinSpeedMps:0.#}..{row.MaxSpeedMps:0.#} m/s " +
                $"owner='{owner}' built={world.Runtime.FindNodes(row.Node).Count}");
            if (BlockOf(blocks, owner) != null)
            {
                carried.Add(row);
                owners.Add(owner);
            }
        }

        CheckCarriedRows(ctx, world, carried, owners, report);
        CheckGateReach(ctx, world, script, rows, carried, report);
        CheckGateCondition(ctx, script, blocks, owners, report);
        WithTrigger(ctx, world, director, rows, (trigger, cutscene, rig, graph) =>
            ctx.Same(rows.Count - carried.Count, trigger.Armed,
                $"only the rows on static world nodes bind, which is BL-492: a row carried by a roster vehicle has no built approach node"));
    }

    // The three rows are symmetric and each belongs to an airship the mission's roster spawns.
    private static void CheckCarriedRows(
        TestContext ctx, TestWorld world, IReadOnlyList<LandingApproach> carried,
        IReadOnlyList<string> owners, StringBuilder report)
    {
        report.AppendLine($"{carried.Count} row(s) carried by a roster vehicle: " +
            $"{string.Join(", ", owners)}");
        ctx.Same(3, carried.Count,
            $"CM02 authors one approach row per Balmoral, each under that airship's own gamez node");
        bool symmetric = true;
        int built = 0;
        foreach (var row in carried)
        {
            symmetric &= Mathf.IsEqualApprox(row.AngleRad, carried[0].AngleRad)
                && Mathf.IsEqualApprox(row.MinSpeedMps, carried[0].MinSpeedMps)
                && Mathf.IsEqualApprox(row.MaxSpeedMps, carried[0].MaxSpeedMps)
                && row.Shape == carried[0].Shape;
            built += world.Runtime.FindNodes(row.Node).Count;
        }

        ctx.Check(symmetric,
            $"and the three differ only by index, so no gate of theirs can admit one and not another");
        ctx.Same(0, built,
            $"BL-492: none of their approach nodes is built, an airship's gamez node being a library root the roster spawns instead");
    }

    // The mission's own gate pair, found from the compiled definitions rather than named here: the
    // two definitions that write every carried row's land_on node, and what they can reach today.
    private static void CheckGateReach(
        TestContext ctx, TestWorld world, ObjectiveScript script,
        IReadOnlyList<LandingApproach> rows, IReadOnlyList<LandingApproach> carried,
        StringBuilder report)
    {
        var arms = new List<int>();
        foreach (var row in carried)
        {
            int index = ArmIndexOf(world.Gamez, row.Node);
            arms.Add(index);
            report.AppendLine($"  '{row.Node}' land_on is gamez node {index}, " +
                $"built={(index >= 0 ? BuiltCount(world, index) : 0)}");
        }

        var gate = GateDefs(world, arms);
        report.AppendLine($"gate definitions writing all {arms.Count} land_on node(s): " +
            $"{string.Join(", ", gate)}");
        ctx.Same(2, gate.Count,
            $"the mission carries one pair of definitions that switch all three approaches together");
        int called = 0;
        foreach (string name in gate)
        {
            called += CallerOf(script, name) != null ? 1 : 0;
        }

        ctx.Same(gate.Count, called,
            $"and an objective's WAKE_ANIM calls each of them, so the gate is the mission's own");
        ctx.Check(rows.Count > carried.Count,
            $"the chapter's static approach rows do build, which is why one on a world node fires and one on an airship does not");
    }

    // What the arming objective waits on: the DEDG the objective ahead of it authors, over the
    // group the airships' own roster blocks are in.
    private static void CheckGateCondition(
        TestContext ctx, ObjectiveScript script,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, IReadOnlyList<string> owners,
        StringBuilder report)
    {
        int group = -1;
        bool oneGroup = owners.Count > 0;
        foreach (string owner in owners)
        {
            int mine = BlockOf(blocks, owner) is { } fields ? AiSkills.RosterGroup(fields) : -1;
            oneGroup &= group < 0 || mine == group;
            group = mine;
        }

        report.AppendLine($"the carried rows' airships are aiv group {group}");
        ctx.Check(oneGroup && group > 0,
            $"the three Balmorals share one aiv group, which is what a DEDG condition counts");
        var gate = DedgAhead(script, group);
        report.AppendLine(gate is { } found
            ? $"OBJECTIVE{found.Number} waits on DEDG [{found.Dedg!.Value.Group}, " +
              $"{found.Dedg.Value.Max}] and wakes the objective that calls the arming animation"
            : "no DEDG objective wakes an objective that calls a gate animation");
        ctx.Check(gate?.Dedg is { Max: 1 },
            $"and the capture waits on that group being down to one, so it is not offered while more than one airship flies");
    }

    // The shipped volumes, read back off the resolved rows: the six drop cones are cones with a
    // real opening angle, and the auto row is the ball the original offers its auto-land inside.
    private static void CheckShapes(
        TestContext ctx, IReadOnlyList<LandingApproach> armed, StringBuilder report)
    {
        LandingApproach? cone = null;
        LandingApproach? auto = null;
        foreach (var approach in armed)
        {
            cone ??= approach.Shape == ApproachShape.Cone ? approach : null;
            auto ??= approach.Auto ? approach : null;
        }

        ctx.Check(cone != null, $"the mission's drop-off rows carry a cone condition volume");
        if (cone is { } shape)
        {
            float half = Mathf.RadToDeg(
                Mathf.Atan2(shape.Radius, shape.Apex.DistanceTo(shape.BaseCentre)));
            report.AppendLine($"'{shape.Node}' cone: half-angle {half:0.0} deg, " +
                $"reach {shape.Apex.DistanceTo(shape.BaseCentre):0.0} m");
            ctx.Check(half is > 1f and < 89f,
                $"whose authored triangle opens a usable cone ({half:0.0} deg half-angle)");
            ctx.Check(shape.Contains(Lerp(shape, AxisFraction)),
                $"a point on that cone's own axis is inside it");
            ctx.Check(!shape.Contains(shape.Apex - ((shape.BaseCentre - shape.Apex) * 0.5f)),
                $"and a point behind its apex is not, so the volume has a front");
        }

        ctx.Check(auto is { Shape: ApproachShape.Sphere },
            $"and the auto-land row the chapter authors is a sphere, tested with no attitude cone");
    }

    // The mission's own route to the drop: the objective that names a landings animation starts
    // dormant with its approach disarmed, the objective chain arms it, and flying the cone then
    // starts the cutscene and completes that objective.
    private static void RunTheDrop(
        TestContext ctx, TestWorld world, ObjectiveGraph graph, ObjectiveScript script,
        IReadOnlyList<LandingApproach> armed, LandingApproachRuntime trigger,
        CutsceneController cutscene, FlightController rig, StringBuilder report)
    {
        var drop = ConeFor(armed);
        if (drop is not { } cone)
        {
            ctx.Check(false, $"the mission carries a cone approach to fly");
            return;
        }

        var closure = ClosureOf(world, new[] { cone });
        if (GatedObjective(script, closure) is not { } gated)
        {
            ctx.Check(false, $"the mission gates an objective on what that approach plays");
            return;
        }

        report.AppendLine($"OBJECTIVE{gated.Number} waits on ANIM_STATE " +
            $"'{gated.AnimStates[0].Name}' = {gated.AnimStates[0].State}");
        foreach (var row in armed)
        {
            var found = world.Runtime.FindNodes(LandingApproaches.ArmNode,
                world.Runtime.FindNodes(row.Node)[0]);
            report.AppendLine($"  '{row.Node}' land_on armed=" +
                $"{(found.Count > 0 ? found[0].Visible.ToString() : "absent")}");
        }

        ctx.Same(0, world.Runtime.AnimStateOf(gated.AnimStates[0].Name),
            $"the animation OBJECTIVE{gated.Number} waits on has not run at mission start");
        // The graph is deliberately NOT stepped for this one: the drop cones sit on the site the
        // first primary approaches, so a flight that also ran the mission would arm them itself.
        ctx.Check(!Fly(ctx, world, trigger, cutscene, null, rig, cone, report),
            $"and flying its approach before the mission arms it starts nothing");

        Arm(ctx, world, graph, script, rig, cone, report);
        ctx.Check(Fly(ctx, world, trigger, cutscene, graph, rig, cone, report),
            $"flying '{cone.Node}' once the mission has armed it starts '{trigger.LastStarted}'");
        report.AppendLine($"started '{trigger.LastStarted}', codes [{string.Join(", ", cutscene.Codes)}]");
        // ⚠ BL-470: an instantly-completing definition satisfies both the EXECUTED and the
        // objective check below, so those two are blind to a cutscene that is over the frame it
        // starts. Fly has already ticked the host once, so still Playing here means it outlived it.
        ctx.Check(cutscene.Playing,
            $"'{trigger.LastStarted}' still owns the session a frame on, rather than completing instantly");
        foreach (string name in closure)
        {
            report.AppendLine($"  reached '{name}' state {world.Runtime.AnimStateOf(name)}");
        }

        // 11 takes the player out of flight, 2 turns the presentation on
        // (docs/formats/anim-definitions/cutscenes.md).
        ctx.Check(Raised(cutscene.Codes, 11) && Raised(cutscene.Codes, 2),
            $"the cutscene host took the drop's own callbacks, out-of-flight and presentation");

        float played = 0f;
        for (float t = 0f; t < PlayBudgetS && !graph.CompletedOf(gated.Number); t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
            graph.Step(StepDt);
            if (cutscene.Playing)
            {
                played += StepDt;
            }
        }

        report.AppendLine($"episode ran {played:0.##} s past the frame it started on");
        report.AppendLine($"'{gated.AnimStates[0].Name}' state " +
            $"{world.Runtime.AnimStateOf(gated.AnimStates[0].Name)}, " +
            $"OBJECTIVE{gated.Number} completed={graph.CompletedOf(gated.Number)}");
        ctx.Same(3, world.Runtime.AnimStateOf(gated.AnimStates[0].Name),
            $"the definition it waits on runs to EXECUTED rather than being declared run");
        ctx.Check(graph.CompletedOf(gated.Number),
            $"which completes OBJECTIVE{gated.Number}, the primary the drop gates");
        CheckNextPrimary(ctx, world, trigger, cutscene, graph, script, armed,
            ClosureOf(world, armed), rig, report);
    }

    // ⚠ The handoff hands control back with the aircraft exactly where the cutscene left it, which
    // is inside the volume that started it, on a row the mission has armed. Without a latch the
    // trigger re-fires the same row on the next frame, over and over.
    private static void CheckNoRestart(
        TestContext ctx, TestWorld world, LandingApproachRuntime trigger,
        CutsceneController cutscene, ObjectiveGraph graph, FlightController rig,
        LandingApproach approach, StringBuilder report)
    {
        for (int i = 0; i < RestartFrames; i++)
        {
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
            graph.Step(StepDt);
        }

        var node = world.Runtime.FindNodes(approach.Node)[0];
        var frame = node.GlobalTransform;
        var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, node);
        report.AppendLine($"{RestartFrames} frames after the handoff: playing={cutscene.Playing} " +
            $"armed={(arm.Count > 0 ? arm[0].Visible.ToString() : "no land_on")} " +
            $"band={approach.SpeedInBand(rig.WorldVelocity.Length())} " +
            $"angle={Mathf.RadToDeg(LandingApproaches.AngleBetween(frame.Basis, rig.Attitude)):0.#} " +
            $"inside={approach.Contains(frame.AffineInverse() * rig.WorldPosition)}");
        ctx.Check(!cutscene.Playing,
            $"and the handoff does not re-fire the row the aircraft is still parked inside");
    }

    // The primary after the drop: another objective gated on a landings animation, whose own
    // approach the same trigger flies. Its wake stands in for the combat leg between them; what is
    // under test is that the condition can be met at all.
    private static void CheckNextPrimary(
        TestContext ctx, TestWorld world, LandingApproachRuntime trigger, CutsceneController cutscene,
        ObjectiveGraph graph, ObjectiveScript script, IReadOnlyList<LandingApproach> armed,
        IReadOnlyList<string> closure, FlightController rig, StringBuilder report)
    {
        if (LaterGated(script, closure, graph) is not { } later
            || RowFor(armed, later.AnimStates[0].Name) is not { } row)
        {
            report.AppendLine("no second objective is gated on a landings animation");
            return;
        }

        // Its wake stands in for the combat leg between the two primaries; the wake is also what
        // runs the objective's own WAKE_ANIM, which is what arms this approach.
        graph.Wake(later.Number);
        for (float t = 0f; t < ArmBudgetS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            graph.Step(StepDt);
        }

        report.AppendLine($"OBJECTIVE{later.Number} woken, waits on '{later.AnimStates[0].Name}'");
        ctx.Check(Fly(ctx, world, trigger, cutscene, graph, rig, row, report),
            $"flying '{row.Node}' starts '{later.AnimStates[0].Name}', what OBJECTIVE{later.Number} waits on");
        for (float t = 0f; t < PlayBudgetS && !graph.CompletedOf(later.Number); t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
            graph.Step(StepDt);
        }

        CheckNoRestart(ctx, world, trigger, cutscene, graph, rig, row, report);
        report.AppendLine($"'{later.AnimStates[0].Name}' state " +
            $"{world.Runtime.AnimStateOf(later.AnimStates[0].Name)}, " +
            $"OBJECTIVE{later.Number} completed={graph.CompletedOf(later.Number)}");
        ctx.Check(graph.CompletedOf(later.Number),
            $"and running it completes OBJECTIVE{later.Number}, the mission's route to its own end");
    }

    // Flies the approach: the aircraft starts on the volume's own axis, aimed at its apex, at a
    // speed inside the authored band. Returns whether the trigger fired inside the budget.
    private static bool Fly(
        TestContext ctx, TestWorld world, LandingApproachRuntime trigger, CutsceneController cutscene,
        ObjectiveGraph? graph, FlightController rig, LandingApproach approach, StringBuilder report)
    {
        var nodes = world.Runtime.FindNodes(approach.Node);
        if (nodes.Count == 0)
        {
            return false;
        }

        var frame = nodes[0].GlobalTransform;
        string? before = trigger.LastStarted;
        rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null, new CamParams(),
            frame * Lerp(approach, AxisFraction), frame * approach.Apex,
            ApproachThrottle, ApproachSpeedMps);
        for (float t = 0f; t < ApproachBudgetS; t += StepDt)
        {
            rig.SimStep(StepDt);
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
            graph?.Step(StepDt);
            if (trigger.LastStarted != before)
            {
                return true;
            }
        }

        var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, nodes[0]);
        report.AppendLine($"  no fire on '{approach.Node}': armed=" +
            $"{(arm.Count > 0 ? arm[0].Visible.ToString() : "no land_on")} " +
            $"speed={rig.WorldVelocity.Length():0.#} band={approach.SpeedInBand(rig.WorldVelocity.Length())} " +
            $"angle={Mathf.RadToDeg(LandingApproaches.AngleBetween(frame.Basis, rig.Attitude)):0.#} " +
            $"inside={approach.Contains(frame.AffineInverse() * rig.WorldPosition)} " +
            $"playing={cutscene.Playing}");
        return false;
    }

    // Arms the drop the way the mission does: the aircraft is parked over the drop site, which is
    // what the first primary's TRAVELERS condition approaches, and the graph's own nap chain runs
    // until the objective that arms it has fired its WAKE_ANIM.
    private static void Arm(TestContext ctx, TestWorld world, ObjectiveGraph graph,
        ObjectiveScript script, FlightController rig, LandingApproach cone, StringBuilder report)
    {
        var node = world.Runtime.FindNodes(cone.Node)[0];
        var site = node.GlobalTransform * cone.Apex;
        rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null,
            new CamParams(), site, site + Vector3.Forward, 0f, 0f);
        for (float t = 0f; t < ArmBudgetS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            graph.Step(StepDt);
        }

        var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, node);
        report.AppendLine($"armed through the mission's own chain: " +
            $"{CompletedCount(graph, script)} objective(s) complete, '{cone.Node}' land_on " +
            $"{(arm.Count > 0 ? arm[0].Visible.ToString() : "absent")}");
        ctx.Check(arm.Count > 0 && arm[0].Visible,
            $"the mission's own objective chain arms '{cone.Node}' by flying its site");
    }

    private static FlightController BuildRig(TestContext ctx, TestWorld world, ProjectilePool pool)
    {
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        try
        {
            var stats = PlaneStats.Load(ctx.ZrdrPath, PlaneNode);
            var model = new PlaneBuilder(GameZ.Load(ctx.PlanesGamezPath), textures).Build(PlaneNode);
            var rig = new FlightController
            {
                PlaneModel = model,
                Collider = PlaneCollider.Build(model),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = true,
                Projectiles = pool,
                UseKeyboard = false,
                PadDevices = Array.Empty<int>(),
                AllowPause = false,
                Team = AimAssist.PlayerTeam,
                Name = "LandingApproachPlayer",
            };
            rig.AddChild(model);
            ctx.Host.AddChild(rig);
            rig.Setup(new FlightModel(stats), null, new CamParams(), Vector3.Zero,
                Vector3.Forward, ApproachThrottle, ApproachSpeedMps);
            return rig;
        }
        finally
        {
            textures.Dispose();
        }
    }

    // The topmost gamez ancestor of a named node: the library root a vehicle instantiates when the
    // node belongs to one, and the world root's own placed content otherwise.
    private static string TopAncestorOf(GameZ gamez, string nodeName)
    {
        var parents = new int[gamez.Nodes.Count];
        for (int i = 0; i < parents.Length; i++)
        {
            parents[i] = -1;
        }

        foreach (var node in gamez.Nodes)
        {
            foreach (int child in node.Children)
            {
                if (child >= 0 && child < parents.Length)
                {
                    parents[child] = node.Index;
                }
            }
        }

        if (gamez.FindByName(nodeName) is not { } start)
        {
            return string.Empty;
        }

        var at = start;
        while (parents[at.Index] >= 0)
        {
            at = gamez.Nodes[parents[at.Index]];
        }

        return at.Name;
    }

    private static List<object?>? BlockOf(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, string name)
    {
        foreach (var (block, fields) in blocks)
        {
            if (string.Equals(block, name, StringComparison.OrdinalIgnoreCase))
            {
                return fields;
            }
        }

        return null;
    }

    // The gamez index of the land_on node under an approach node, or -1 when it carries none.
    private static int ArmIndexOf(GameZ gamez, string approachNode)
    {
        if (gamez.FindByName(approachNode) is not { } approach)
        {
            return -1;
        }

        var stack = new Stack<int>(approach.Children);
        while (stack.Count > 0)
        {
            int index = stack.Pop();
            if (index < 0 || index >= gamez.Nodes.Count)
            {
                continue;
            }

            var node = gamez.Nodes[index];
            if (string.Equals(node.Name, LandingApproaches.ArmNode, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            foreach (int child in node.Children)
            {
                stack.Push(child);
            }
        }

        return -1;
    }

    private static int BuiltCount(TestWorld world, int gamezIndex)
    {
        int built = 0;
        foreach (var node in world.Runtime.FindNodes(LandingApproaches.ArmNode))
        {
            built += node.HasMeta(AnimRuntime.IndexMeta)
                && (int)node.GetMeta(AnimRuntime.IndexMeta) == gamezIndex ? 1 : 0;
        }

        return built;
    }

    // The mission definitions whose symbol table binds every one of these gamez node indices: the
    // pair that switches a whole set of approach rows in one sequence.
    private static List<string> GateDefs(TestWorld world, IReadOnlyList<int> indices)
    {
        var names = new List<string>();
        foreach (var def in world.Session.Program.Defs)
        {
            if (def.AnimName is not { } name || indices.Count == 0)
            {
                continue;
            }

            bool all = true;
            foreach (int index in indices)
            {
                bool found = false;
                foreach (int bound in def.NodeRefs.Values)
                {
                    found |= bound == index;
                }

                all &= found;
            }

            if (all && !Names(names, name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    // The DEDG objective over this group that wakes an objective calling an animation: the gate the
    // capture waits behind.
    private static ObjectiveDef? DedgAhead(ObjectiveScript script, int group)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Dedg is not { } dedg || dedg.Group != group)
            {
                continue;
            }

            var woken = new List<int>(def.WakeWhenComplete);
            if (def.NapWhenComplete is { } nap)
            {
                woken.Add(nap.Target);
            }

            foreach (int number in woken)
            {
                foreach (var other in script.Objectives)
                {
                    if (other.Number == number && other.WakeAnim != null)
                    {
                        return def;
                    }
                }
            }
        }

        return null;
    }

    private static ObjectiveDef? CallerOf(ObjectiveScript script, string anim)
    {
        foreach (var def in script.Objectives)
        {
            if (def.WakeAnim is { } wake
                && string.Equals(wake.Anim, anim, StringComparison.OrdinalIgnoreCase))
            {
                return def;
            }
        }

        return null;
    }

    private static Vector3 Lerp(LandingApproach approach, float fraction) =>
        approach.Apex + ((approach.BaseCentre - approach.Apex) * fraction);

    private static IReadOnlyList<string> ClosureOf(
        TestWorld world, IReadOnlyList<LandingApproach> armed)
    {
        var roots = new List<string>(armed.Count);
        foreach (var approach in armed)
        {
            roots.Add(approach.Anim);
        }

        var names = new List<string>();
        foreach (var def in world.Session.Program.Subset(roots).Defs)
        {
            if (def.AnimName is { } name && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    // The first objective whose ANIM_STATE condition names an animation only the approach trigger
    // can play, which is exactly the objective BL-467 found unsatisfiable.
    private static ObjectiveDef? GatedObjective(
        ObjectiveScript script, IReadOnlyList<string> closure)
    {
        foreach (var def in script.Objectives)
        {
            if (def.AnimStates.Count > 0 && Names(closure, def.AnimStates[0].Name))
            {
                return def;
            }
        }

        return null;
    }

    private static ObjectiveDef? LaterGated(
        ObjectiveScript script, IReadOnlyList<string> closure, ObjectiveGraph graph)
    {
        foreach (var def in script.Objectives)
        {
            if (def.AnimStates.Count > 0 && Names(closure, def.AnimStates[0].Name)
                && !graph.CompletedOf(def.Number))
            {
                return def;
            }
        }

        return null;
    }

    private static LandingApproach? RowFor(IReadOnlyList<LandingApproach> armed, string anim)
    {
        foreach (var approach in armed)
        {
            if (string.Equals(approach.Anim, anim, StringComparison.OrdinalIgnoreCase))
            {
                return approach;
            }
        }

        return null;
    }

    private static LandingApproach? ConeFor(IReadOnlyList<LandingApproach> armed)
    {
        foreach (var approach in armed)
        {
            if (approach.Shape == ApproachShape.Cone && !approach.Auto)
            {
                return approach;
            }
        }

        return null;
    }

    // Where the mission's first awake TRAVELERS condition points, so the graph's own chain can be
    // started without naming a world node here.
    private static Vector3? ApproachSite(ObjectiveScript script, TestWorld world)
    {
        foreach (var def in script.Objectives)
        {
            if (def.BeginDormant || def.Travelers is not { } spec)
            {
                continue;
            }

            if (spec.WherePoint is { Length: 3 } point)
            {
                return new Vector3(point[0], point[1], point[2]);
            }

            var found = world.Runtime.FindNodes(spec.WhereNode ?? string.Empty);
            if (found.Count > 0)
            {
                return found[0].GlobalPosition;
            }
        }

        return null;
    }

    private static int CompletedCount(ObjectiveGraph graph, ObjectiveScript script)
    {
        int done = 0;
        foreach (var def in script.Objectives)
        {
            if (graph.CompletedOf(def.Number))
            {
                done++;
            }
        }

        return done;
    }

    private static bool Raised(IReadOnlyList<int> codes, int code)
    {
        foreach (int raised in codes)
        {
            if (raised == code)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Names(IReadOnlyList<string> names, string wanted)
    {
        foreach (string name in names)
        {
            if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static CampaignMission? MissionOf(IReadOnlyList<CampaignMission> missions, int seq)
    {
        foreach (var mission in missions)
        {
            if (mission.Seq == seq)
            {
                return mission;
            }
        }

        return null;
    }
}
