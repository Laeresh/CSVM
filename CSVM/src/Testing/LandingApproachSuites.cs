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
    // The story position flown. Every node, animation and objective name below is read out of that
    // mission's own data, so nothing here names a shipped world node by hand.
    private const int FirstSeq = 0;
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

    /// <summary>Drives the campaign's first mission's approach triggers against its BUILT world:
    /// the chapter's rows resolve to real cone/half-cone/sphere volumes, a mission carrying none of
    /// the animations arms none of them, the drop-off rows start disarmed, the mission's own
    /// objective chain arms them, and flying one cone starts the drop cutscene, which completes the
    /// primary objective gated on it.</summary>
    internal static void LandingApproachTrigger(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), FirstSeq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {FirstSeq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder);
        ctx.RequireData(missionZrdr, $"{chapter}/{folder} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");

        var script = ObjectiveScript.Load(missionZrdr);
        var report = new StringBuilder();
        report.AppendLine($"seq {FirstSeq} -> {chapter}/{folder}");
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
                world => Drive(ctx, world, director, script, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-landing-approach-{chapter}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder}'s landings.zrd approach triggers against a built world");
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, StringBuilder report)
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

        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        cutscene.BindWorld(world.Runtime);
        cutscene.HostDefinitions(ClosureOf(world, armed));
        // What a session wires before the bootstrap; the suite harness's world build does not.
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
            ctx.Same(armed.Count, trigger.Armed, $"every resolved row binds to a node this world built");
            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = world.Runtime.Sounds,
                ListenerPosition = () => craft.WorldPosition,
                PlayerAircraft = () => craft,
                Projectiles = pool,
                Rng = new Random(1),
            });
            RunTheDrop(ctx, world, director.Graph!, script, armed, trigger, cutscene, rig, report);
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
