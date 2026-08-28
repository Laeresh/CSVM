using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
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
    private const int DockingSeq = 5;
    private const int TrainPickupSeq = 6;
    private const int TrailerPickupSeq = 10;
    private const float StepDt = 1f / 60f;
    private const string PlaneNode = "player_bhawk";

    // The definition the hookup calls to put the flown airframe on the trapeze, and the two
    // airframes it is flown on: the one the defect was reported on, and one whose authored mount
    // offset differs from it in every axis, so no single pose could satisfy both.
    private const string ExtendHookAnim = "player_extend_hook";
    private const string HookupPlayerSeq = "move_player";

    // How close a pose has to land on its authored value to count as that value.
    private const float PoseEpsilon = 1e-3f;

    // How long the mission's own intro is given to run out before a hookup is flown, and how long
    // the airframe's own wing-fold turn is given after the episode ends.
    private const float IntroSettleS = 60f;
    private const float FoldSettleS = 3f;

    // The flown approach: how far along the cone's own axis the aircraft starts, how long it is
    // given to reach the trigger, and the speed it flies at, which sits inside every band the
    // shipped table authors (50-320 mph).
    private const float AxisFraction = 0.6f;
    private const float ApproachSpeedMps = 45f;
    private const float ApproachThrottle = 0.5f;
    private const float ApproachBudgetS = 6f;

    // How long the parked rig waits at CM11's trailer for its range-armed definition to run the
    // authored hatch time and raise the actor the pickup requires, before the cone is flown.
    private const float TrailerApproachS = 4f;

    // How long the graph is stepped for a nap chain to run out, and how long a started cutscene
    // definition is given to reach EXECUTED.
    private const float ArmBudgetS = 12f;
    private const float PlayBudgetS = 45f;

    // How long the auto row's own WAKE_ANIM is given to reach the node write, polled rather than
    // assumed instant.
    private const float AutoArmBudgetS = 30f;

    // How long the trigger is ticked after a handoff to catch the row re-firing.
    private const int RestartFrames = 30;

    // The docking episode: how long the row's definition is given to play out through its whole
    // call chain (hookup, drop, hook state, unhook), how far past the handoff the aeroplane is
    // watched, the largest single-frame move a flying aeroplane can make against a teleport, and
    // how far the released aeroplane may sit from the marker its re-placement code read.
    private const float DockBudgetS = 150f;
    private const float AfterReleaseS = 2f;
    private const float TeleportM = 20f;
    private const float PlacedToleranceM = 2f;

    // The mission-script host's handoff code, the one that gives the player flight back.
    private const int HandoffCode = 1;

    // How far outside its band the hangar drop is called from. Anything past the authored 75 m
    // does; this is far enough that no bounds reading of the hangar could land inside it.
    private const float AwayM = 2000f;

    // What a landings suite does with the built world: the harness owns the build, the suite owns
    // the drive. The mission's own zrdr path comes with it, for the readers that are not the
    // objective script.
    private static readonly string[] HookupPlanes = { "player_balmoral", "player_pfighter" };

    private delegate void MissionDrive(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report);

    /// <summary>Drives the campaign's first mission's approach triggers against its BUILT world:
    /// the chapter's rows resolve to real cone/half-cone/sphere volumes, a mission carrying none of
    /// the animations arms none of them, the drop-off rows start disarmed, the mission's own
    /// objective chain arms them, and flying one cone starts the drop cutscene, which completes the
    /// primary objective gated on it.</summary>
    internal static void LandingApproachTrigger(TestContext ctx) =>
        DriveMission(ctx, FirstSeq, "test-landing-approach", Drive);

    /// <summary>Drives CM02's wing-walk capture gate against its BUILT world with its own roster
    /// spawned: three symmetric Balmoral rows, each authored under its plane's gamez node, grafted
    /// onto the rig the roster spawns so all three bind; one pair of definitions switching all
    /// three <c>land_on</c> nodes by gamez index, which arms and un-arms the rows it now reaches;
    /// an arming objective that waits on those planes' aiv group being down to one; and a driven
    /// approach at an armed Balmoral starting the capture.</summary>
    internal static void WingWalkCaptureGate(TestContext ctx) =>
        DriveMission(ctx, WingWalkSeq, "test-wingwalk-gate", DriveWingWalk);

    /// <summary>Drives CM07's caboose pickup through its range-triggered start animation: the
    /// passenger's library-root rig appears on the train, the train's own pickup timing opens the
    /// late approach cone, and flying that cone starts the pickup cutscene and clears its objective.</summary>
    internal static void TrainPickupGate(TestContext ctx) =>
        DriveMission(ctx, TrainPickupSeq, "test-train-pickup-gate", DriveTrainPickup);

    /// <summary>Drives CM07's caboose pickup the way the original runs it: the staged passenger
    /// rides the moving train as its child, waves with the lit flare once the pickup timing opens
    /// the switch, the rope ladder drops on a level approach inside the sensor, and the pickup
    /// cutscene's call to <c>caboosepickup</c> holds a live instance for the person's climb.</summary>
    internal static void TrainPickupRide(TestContext ctx)
    {
        // The harness retires the world's puffer factory with the build's texture archive, where a
        // game session keeps it (WorldSession.Options.TexturesOutliveBuild), so the flare trail
        // asserted on the passenger's hand at run time is counted by a fake instead of dropped.
        ctx.EmitterFactory = new CountingEmitterFactory();
        DriveMission(ctx, TrainPickupSeq, "test-train-pickup-ride", DriveTrainPickupRide);
    }

    /// <summary>Drives CM11's trailer pickup through the objective script's own <c>WAKE_ANIM</c>:
    /// the dock objective's definition stages the approach cone from its library root under the
    /// trailer's sensor, the landing trigger discovers it, and flying that cone starts the pickup
    /// cutscene and clears the dock objective.</summary>
    internal static void TrailerPickupGate(TestContext ctx) =>
        DriveMission(ctx, TrailerPickupSeq, "test-trailer-pickup-gate", DriveTrailerPickup);

    /// <summary>Drives the auto-land button over the campaign's first mission's BUILT world: flying
    /// into the chapter's <c>auto</c> row lights <see cref="LandingApproachRuntime.AutoLandOffered"/>
    /// but starts nothing on its own, pressing the button starts the row's animation the way the
    /// manual row would, and holding the button past the handoff does not re-fire it.</summary>
    internal static void AutoLandButton(TestContext ctx) =>
        DriveMission(ctx, FirstSeq, "test-autoland-button", DriveAutoLand);

    /// <summary>Drives the hookup on two airframes and reads what it did to each: the flown
    /// aircraft's own subtree is in the runtime's node table, so the definition's per-airframe
    /// branches are decidable, and the episode ends with that airframe's docking hook extended, its
    /// authored mount offset applied, and its wings folded where the airframe authors a fold.
    /// </summary>
    internal static void HookupAirframe(TestContext ctx) =>
        DriveMission(ctx, FirstSeq, "test-hookup-airframe", DriveHookupAirframe);

    /// <summary>Drives CM06's docking onto the Workers' Voyage, the one shipped row whose
    /// definition raises no code of its own and calls the ones that do: the episode belongs to the
    /// row's definition rather than the callee that raised the first code, control stays locked
    /// from the hookup to the authored handoff, and the aeroplane is left where the re-placement
    /// code put it rather than teleported when the definition runs out.</summary>
    internal static void DockingHold(TestContext ctx) =>
        DriveMission(ctx, DockingSeq, "test-docking-hold", DriveDockingHold);

    /// <summary>Drives CM07's zeppelin-hangar drop, the mission's other cutscene: the depot chain
    /// reaction's <c>CALL_ANIMATION</c> only ARMS it, because the definition is range-gated;
    /// reaching the hangar runs it; and the authored <c>RESET_STATE</c> at the handoff is what
    /// clears the objective node the mission gates "Fly Through Zeppelin Hangar" on.</summary>
    internal static void HangarDropGate(TestContext ctx) =>
        DriveMission(ctx, TrainPickupSeq, "test-hangar-drop-gate", DriveHangarDrop);

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

    private static void DriveAutoLand(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var armed = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        if (AutoFor(armed) is not { } auto)
        {
            ctx.Check(false, $"the chapter's landings.zrd carries an auto row to fly");
            return;
        }

        WithTrigger(ctx, world, director, armed, (trigger, cutscene, rig, graph) =>
            RunAutoLandButton(ctx, world, graph, script, trigger, cutscene, rig, auto, report));
    }

    // Arms the row the mission's own way (whatever objective's WAKE_ANIM writes its land_on),
    // flies the sphere with the button up (offered, but inert), presses it (starts the SAME
    // animation the manual row would), then holds it past the handoff to prove the row does not
    // re-fire while the aircraft is still parked inside it, the manual row's own guard, reused.
    private static void RunAutoLandButton(
        TestContext ctx, TestWorld world, ObjectiveGraph graph, ObjectiveScript script,
        LandingApproachRuntime trigger, CutsceneController cutscene, FlightController rig,
        LandingApproach auto, StringBuilder report)
    {
        var nodes = world.Runtime.FindNodes(auto.Node);
        ctx.Check(nodes.Count > 0, $"the auto row's own node '{auto.Node}' is built");
        if (nodes.Count == 0)
        {
            return;
        }

        ArmRow(ctx, world, graph, script, auto, report);

        var frame = nodes[0].GlobalTransform;
        var centre = frame * auto.Apex;
        report.AppendLine($"'{auto.Node}' sphere centre world pos=({centre.X:0},{centre.Y:0}," +
            $"{centre.Z:0}) r={auto.Radius:0.#}");
        rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null, new CamParams(),
            frame * Lerp(auto, AxisFraction), frame * auto.Apex, ApproachThrottle, ApproachSpeedMps);

        bool offered = false;
        for (int i = 0; i < RestartFrames; i++)
        {
            rig.SimStep(StepDt);
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
            offered |= trigger.AutoLandOffered;
        }

        report.AppendLine($"auto row offered={offered} with the button up, started=" +
            $"'{trigger.LastStarted ?? "(none)"}'");
        ctx.Check(offered, $"flying into '{auto.Node}' lights AutoLandOffered");
        ctx.Check(trigger.LastStarted == null, $"and starts nothing while the button is up");

        // Nothing above this line calls the flown rig's OWN _Process, so a suite this shape never
        // exercises GameSession's feed or FlightHud's draw (INSTR-26: drive the node's own callback
        // under a Realtime clock rather than SimStep). Mirror that one feed line, then let it draw.
        var savedClock = GameClock.Current;
        GameClock.Current = new GameClock { Mode = GameClock.RunMode.Realtime };
        try
        {
            rig.AutoLandOffered = trigger.AutoLandOffered;
            for (int i = 0; i < RestartFrames; i++)
            {
                rig._Process(StepDt);
            }
        }
        finally
        {
            GameClock.Current = savedClock;
        }

        report.AppendLine($"drawn on a realtime frame: DrawsTextBlock={rig.PilotHud.DrawsTextBlock}, " +
            $"text='{rig.PilotHud.DrawnText}'");
        ctx.Check(rig.PilotHud.DrawsTextBlock, $"the flown pane still holds a text block to draw into");
        ctx.Check(rig.PilotHud.DrawnText is { Length: > 0 } drawn && drawn.Contains("AUTO-LAND"),
            $"…and a realtime frame actually puts the auto-land prompt on it");

        rig.AutoLand = true;
        for (int i = 0; i < RestartFrames && !cutscene.Playing; i++)
        {
            rig.SimStep(StepDt);
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
        }

        report.AppendLine($"button pressed: started='{trigger.LastStarted}', playing={cutscene.Playing}");
        ctx.Check(trigger.LastStarted == auto.Anim,
            $"pressing the button starts '{auto.Anim}', the row's own animation");
        ctx.Check(cutscene.Playing, $"which the cutscene host runs the same as the manual row's");

        float played = 0f;
        for (float t = 0f; t < PlayBudgetS && cutscene.Playing; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
            graph.Step(StepDt);
            played += StepDt;
        }

        report.AppendLine($"episode ran {played:0.##} s before handoff, playing={cutscene.Playing}");
        ctx.Check(!cutscene.Playing, $"and the auto-land episode completes within budget");
        CheckNoRestart(ctx, world, trigger, cutscene, graph, rig, auto, report);
    }

    // The hookup as the aircraft archive authors it: one branch per airframe in the extend-hook
    // definition, and one more inside the hookup's own move_player sequence for the airframes that
    // fold their wings. Every name below is read out of those definitions.
    private static void DriveHookupAirframe(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var armed = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        if (AutoFor(armed) is not { } auto)
        {
            ctx.Check(false, $"the chapter's landings.zrd carries an auto row to fly");
            return;
        }

        var stage = world.Session.Aircraft;
        report.AppendLine($"aircraft stage: {(stage != null ? $"base {stage.PointerBase}" : "none")}");
        ctx.Check(stage != null,
            $"the mission stages the aircraft archive, which is the frame the hookup poses in");
        if (stage == null)
        {
            return;
        }

        foreach (var plane in HookupPlanes)
        {
            report.AppendLine($"--- {plane} ---");
            WithTrigger(ctx, world, director, armed,
                (trigger, cutscene, rig, graph) => RunHookupAirframe(
                    ctx, world, graph, script, trigger, cutscene, rig, auto, plane, report),
                planeNode: plane, aircraft: stage);
        }
    }

    // Arms the auto row, flies it and presses the button (the path landings-auto-land-button
    // already proves), then reads what the episode did to the airframe.
    private static void RunHookupAirframe(
        TestContext ctx, TestWorld world, ObjectiveGraph graph, ObjectiveScript script,
        LandingApproachRuntime trigger, CutsceneController cutscene, FlightController rig,
        LandingApproach auto, string planeNode, StringBuilder report)
    {
        if (MountBranch(world, planeNode) is not { } branch)
        {
            ctx.Check(false, $"'{ExtendHookAnim}' authors a branch for '{planeNode}'");
            return;
        }

        if (rig.PlaneModel is not { } model)
        {
            ctx.Check(false, $"the rig built a '{planeNode}' model");
            return;
        }

        var hook = branch.HookAnim is { } hookAnim ? NamedIn(world, hookAnim, model) : null;
        var fold = FoldOf(world, auto.Anim, planeNode);
        report.AppendLine($"authored mount offset {branch.Offset}, hook '{branch.HookAnim}' " +
            $"group {(hook != null ? $"'{AnimRuntime.NameOf(hook)}' built" : "absent")}, " +
            $"fold '{fold?.Anim ?? "(none)"}'");
        bool reachable = false;
        foreach (var found in world.Runtime.FindNodes(planeNode))
        {
            reachable |= ReferenceEquals(found, model);
        }

        ctx.Check(reachable,
            $"the flown '{planeNode}' is in the animation runtime's node table, which is what the hookup's per-airframe branches read");
        ctx.Check(hook != null,
            $"and carries its own '{branch.HookAnim}' hook group rather than a skipped subtree");
        ctx.Check(hook is not { Visible: true }, $"which starts retracted");

        // Which definitions the episode actually reached, so a check that fails says whether the
        // pose was wrong or the branch that writes it never ran at all (DIAG-20).
        var started = new List<string>();
        void Record(AnimDefinition def, Node3D? anchor)
        {
            if (def.AnimName is { Length: > 0 } name && !started.Contains(name))
            {
                started.Add(name);
            }
        }
        world.Runtime.OnInstanceStarted += Record;
        try
        {
            FlyTheAutoRow(ctx, world, graph, script, trigger, cutscene, rig, auto, planeNode, report);
        }
        finally
        {
            world.Runtime.OnInstanceStarted -= Record;
        }

        // The Balmoral branch ends its own sequence two seconds after calling the fold, so the
        // episode is over while the authored two-second turn is still running. Let it finish.
        for (float t = 0f; t < FoldSettleS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
        }

        report.AppendLine($"started: {string.Join(", ", started)}");
        report.AppendLine($"after the episode: mount {model.Position}, hook visible={hook?.Visible}");
        ctx.Check(hook is { Visible: true },
            $"the hookup extends '{planeNode}'s own docking hook");
        ctx.Check(Near(model.Position, branch.Offset),
            $"and mounts it at the offset '{ExtendHookAnim}' authors for it, {branch.Offset}");
        CheckWingFold(ctx, world, fold, model, report);

        // ⚠ Last, and not optional: the rig this episode flew is freed when the body returns, and a
        // motion still running on one of its nodes ticks into a disposed object on the next drive.
        foreach (string anim in started)
        {
            world.Runtime.Stop(anim);
        }
    }

    // The flight half, exactly as the auto row runs it: arm through the mission's own objective,
    // fly the sphere, press the button, then step to the handoff.
    private static void FlyTheAutoRow(
        TestContext ctx, TestWorld world, ObjectiveGraph graph, ObjectiveScript script,
        LandingApproachRuntime trigger, CutsceneController cutscene, FlightController rig,
        LandingApproach auto, string planeNode, StringBuilder report)
    {
        // The mission's own intro is still playing at t=0 and raises the same out-of-flight code
        // the hookup does, so its handoff would land in the middle of this episode and give the
        // aircraft flight back mid-hookup. A player reaches the klondike minutes later.
        for (float t = 0f; t < IntroSettleS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
        }

        report.AppendLine($"intro settled after {IntroSettleS:0}s, playing={cutscene.Playing}");
        ArmRow(ctx, world, graph, script, auto, report);
        var frame = world.Runtime.FindNodes(auto.Node)[0].GlobalTransform;
        rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, planeNode)),
            null, new CamParams(), frame * Lerp(auto, AxisFraction), frame * auto.Apex,
            ApproachThrottle, ApproachSpeedMps);
        rig.AutoLand = true;
        for (int i = 0; i < RestartFrames && !cutscene.Playing; i++)
        {
            rig.SimStep(StepDt);
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
        }

        ctx.Check(cutscene.Playing, $"pressing the button starts '{auto.Anim}' under the host");
        float played = 0f;
        for (float t = 0f; t < PlayBudgetS && cutscene.Playing; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
            graph.Step(StepDt);
            played += StepDt;
        }

        report.AppendLine($"episode ran {played:0.##} s, playing={cutscene.Playing}");
    }

    // CM06's docking, a shape no other shipped row has: the row's own definition authors no
    // CALLBACK and calls the hookup, the drop, the hook state and the unhook in turn, waiting on the
    // first and the last. The hookup raises the first code and ends with the aeroplane still on the
    // hook; the unhook raises the handoff and the re-placement at its own end.
    private static void DriveDockingHold(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var rows = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        var dock = SilentRowOf(world, rows);
        report.AppendLine($"silent row: '{dock?.Anim ?? "-"}' on '{dock?.Node ?? "-"}'");
        ctx.Check(dock != null,
            $"the chapter's landings.zrd carries a manual row whose own definition raises no code while its call closure does");
        var stage = world.Session.Aircraft;
        ctx.Check(stage?.PlayerMarker != null,
            $"the mission stages the aircraft archive's '{AircraftStage.PlayerNode}' marker, the pose the docking flies");
        if (dock == null || stage?.PlayerMarker is not { } marker)
        {
            return;
        }

        WithTrigger(ctx, world, director, rows, (trigger, cutscene, rig, graph) =>
            RunTheDocking(ctx, world, graph, script, trigger, cutscene, rig, dock, marker, report),
            aircraft: stage);
    }

    private static void RunTheDocking(
        TestContext ctx, TestWorld world, ObjectiveGraph graph, ObjectiveScript script,
        LandingApproachRuntime trigger, CutsceneController cutscene, FlightController rig,
        LandingApproach dock, Node3D marker, StringBuilder report)
    {
        for (float t = 0f; t < IntroSettleS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
        }

        report.AppendLine($"intro settled after {IntroSettleS:0}s, playing={cutscene.Playing}");
        ArmRow(ctx, world, graph, script, dock, report);

        float now = 0f;
        var started = new List<string>();
        void Record(AnimDefinition def, Node3D? anchor)
        {
            if (def.AnimName is { Length: > 0 } name && !started.Contains(name))
            {
                started.Add(name);
                report.AppendLine($"  t={now,6:0.00} started '{name}'");
            }
        }

        // The handoff code as the runtime raises it, read in front of the host so the moment is
        // known whichever episode the host books it to.
        float handoffAt = -1f;
        string? handoffRaiser = null;
        string? firstRaiser = null;
        world.Runtime.CallbackHost = (code, anim, root) =>
        {
            firstRaiser ??= anim;
            report.AppendLine($"  t={now,6:0.00} code {code} from '{anim}'");
            if (code == HandoffCode && handoffAt < 0f)
            {
                handoffAt = now;
                handoffRaiser = anim;
            }

            return cutscene.Host(code, anim, root);
        };
        void Finished(AnimDefinition def, Node3D? anchor) =>
            report.AppendLine($"  t={now,6:0.00} finished '{def.AnimName}'");
        world.Runtime.OnInstanceStarted += Record;
        world.Runtime.OnInstanceFinished += Finished;
        world.Runtime.OpenResolutionCensus();
        try
        {
            ctx.Check(Fly(ctx, world, trigger, cutscene, graph, rig, dock, report),
                $"flying '{dock.Node}' starts '{dock.Anim}'");
            report.AppendLine($"episode: playing={cutscene.Playing} anim='{cutscene.Anim}' " +
                $"first code from '{firstRaiser}' codes=[{string.Join(", ", cutscene.Codes)}]");
            ctx.Check(cutscene.Playing, $"and the cutscene host takes the session on it");
            ctx.Check(firstRaiser != null && firstRaiser != dock.Anim,
                $"the first code is raised by a callee, not by '{dock.Anim}' itself");
            ctx.Check(string.Equals(cutscene.Anim, dock.Anim, StringComparison.OrdinalIgnoreCase),
                $"yet the episode belongs to '{dock.Anim}', the row the trigger started, which is the original's landings slot (read '{cutscene.Anim ?? "-"}')");
            WatchTheDocking(ctx, world, graph, trigger, cutscene, rig, dock, marker, report,
                () => now, dt => now += dt, () => handoffAt, () => handoffRaiser);
        }
        finally
        {
            foreach (string line in world.Runtime.ResolutionLines())
            {
                report.AppendLine($"resolution: {line}");
            }

            world.Runtime.CloseResolutionCensus();
            world.Runtime.OnInstanceStarted -= Record;
            world.Runtime.OnInstanceFinished -= Finished;
            world.Runtime.CallbackHost = cutscene.Host;
            foreach (string anim in started)
            {
                world.Runtime.Stop(anim);
            }
        }
    }

    // The played leg, watched frame by frame: control locked until the handoff code, the handoff
    // at the row definition's own end, and no teleport once the aeroplane is flying again.
    private static void WatchTheDocking(
        TestContext ctx, TestWorld world, ObjectiveGraph graph, LandingApproachRuntime trigger,
        CutsceneController cutscene, FlightController rig, LandingApproach dock, Node3D marker,
        StringBuilder report, Func<float> now, Action<float> tick, Func<float> handoffAt,
        Func<string?> handoffRaiser)
    {
        float releasedAt = -1f;
        float endedAt = -1f;
        float handedBackAt = -1f;
        float unlockedBeforeHandoffAt = -1f;
        float biggestStepM = 0f;
        float biggestStepAt = -1f;
        float placedOffM = -1f;
        var prev = rig.WorldPosition;
        int frame = 0;
        while (now() < DockBudgetS && (releasedAt < 0f || now() < releasedAt + AfterReleaseS))
        {
            rig.SimStep(StepDt);
            world.Runtime.Advance(StepDt);
            trigger.Tick();
            cutscene.Tick();
            graph.Step(StepDt);
            tick(StepDt);
            frame++;
            bool locked = rig.Held && rig.Inert;
            var markerPose = AnimRuntime.WorldTransform(marker, out _);
            if (!locked && releasedAt < 0f)
            {
                releasedAt = now();
                placedOffM = rig.WorldPosition.DistanceTo(markerPose.Origin);
                report.AppendLine($"  t={now(),6:0.00} released: {placedOffM:0.#} m off the '{AircraftStage.PlayerNode}' marker, " +
                    $"handoff code {(handoffAt() < 0f ? "not raised" : $"raised at t={handoffAt():0.00}")}");
                if (handoffAt() < 0f)
                {
                    unlockedBeforeHandoffAt = now();
                }
            }

            if (releasedAt >= 0f && now() > releasedAt)
            {
                float step = rig.WorldPosition.DistanceTo(prev);
                if (step > biggestStepM)
                {
                    biggestStepM = step;
                    biggestStepAt = now();
                }
            }

            prev = rig.WorldPosition;
            if (endedAt < 0f && world.Runtime.AnimStateOf(dock.Anim) != 2)
            {
                endedAt = now();
                report.AppendLine($"  t={now(),6:0.00} '{dock.Anim}' ended, playing={cutscene.Playing}");
            }

            if (handedBackAt < 0f && !cutscene.Playing)
            {
                handedBackAt = now();
                report.AppendLine($"  t={now(),6:0.00} the host handed the session back");
            }

            if (frame % (int)(1f / StepDt) == 0)
            {
                report.AppendLine($"t={now(),6:0.0} held={rig.Held} inert={rig.Inert} " +
                    $"playing={cutscene.Playing} anim='{cutscene.Anim ?? "-"}' " +
                    $"rig {rig.WorldPosition} marker {markerPose.Origin}");
            }
        }

        report.AppendLine($"released at t={releasedAt:0.00}, handoff code at t={handoffAt():0.00} " +
            $"from '{handoffRaiser() ?? "-"}', '{dock.Anim}' ended at t={endedAt:0.00}, " +
            $"session handed back at t={handedBackAt:0.00}, " +
            $"biggest step after release {biggestStepM:0.##} m at t={biggestStepAt:0.00}");
        ctx.Check(handoffAt() >= 0f, $"the docking raises its handoff code within {DockBudgetS:0} s");
        ctx.Check(handoffRaiser() != null && handoffRaiser() != dock.Anim,
            $"from a callee of '{dock.Anim}', the unhook, rather than from the row's own definition");
        // The row's trailing WAIT_FOR_COMPLETION holds no runner open (docs/org/sequences.md), so
        // its definition ends before the unhook does: the episode has to outlive it.
        ctx.Check(endedAt >= 0f && handoffAt() >= 0f && endedAt < handoffAt(),
            $"'{dock.Anim}' itself ends before the handoff, its last call being a trailing wait");
        ctx.Check(handedBackAt < 0f || handoffAt() < 0f || handedBackAt >= handoffAt() - StepDt,
            $"and the host keeps the session past that end, until the code (handed back at t={handedBackAt:0.00})");
        ctx.Check(unlockedBeforeHandoffAt < 0f,
            $"the player is held out of flight from the hookup until that code, not released when the first callee ends (unlocked at t={unlockedBeforeHandoffAt:0.00})");
        ctx.Check(releasedAt >= 0f && handoffAt() >= 0f && releasedAt >= handoffAt() - StepDt,
            $"and gets flight back the frame the code lands");
        ctx.Check(handedBackAt >= 0f && handoffAt() >= 0f && handedBackAt - handoffAt() < AfterReleaseS,
            $"and the host hands the session back within {AfterReleaseS:0} s of it, the unhook being the last code-authoring definition");
        ctx.Check(placedOffM >= 0f && placedOffM < PlacedToleranceM,
            $"the released aeroplane flies out of the '{AircraftStage.PlayerNode}' marker's pose, where the re-placement code read it, within {PlacedToleranceM:0} m");
        ctx.Check(biggestStepM < TeleportM,
            $"and is never teleported after the release: no frame moves it {TeleportM:0} m or more, the end of the definition included");
    }

    // The manual row whose own definition raises no CALLBACK while something in its call closure
    // does, which is the docking's shape and the case the landings slot exists for.
    private static LandingApproach? SilentRowOf(TestWorld world, IReadOnlyList<LandingApproach> rows)
    {
        foreach (var row in rows)
        {
            if (row.Auto || RaisesCode(world, row.Anim))
            {
                continue;
            }

            foreach (string callee in ClosureOf(world, row.Anim))
            {
                if (callee != row.Anim && RaisesCode(world, callee))
                {
                    return row;
                }
            }
        }

        return null;
    }

    private static bool RaisesCode(TestWorld world, string anim)
    {
        foreach (var def in world.Runtime.DefsFor(anim))
        {
            foreach (var seq in def.Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind == "Callback")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // The wing fold's own two movers, checked against the rotations the fold definition authors.
    // An airframe that authors no fold is a coverage statement, not a failure (DIAG-22).
    private static void CheckWingFold(TestContext ctx, TestWorld world,
        (string Anim, AnimDefinition Def)? fold, Node3D model, StringBuilder report)
    {
        if (fold is not { } authored)
        {
            report.AppendLine("no fold definition is rooted on this airframe");
            return;
        }

        int checkedPairs = 0;
        foreach (var seq in authored.Def.Sequences)
        {
            foreach (var ev in seq.Events)
            {
                if (ev.Kind != "ObjectMotionFromTo" || ev.Data.Obj("rotate") is not { } rotate
                    || ev.Data.Str("name") is not { } name)
                {
                    continue;
                }

                var want = rotate.Vec3("to");
                var node = NamedNode(model, name);
                var got = node?.Basis.GetEuler(EulerOrder.Yxz) ?? Vector3.Zero;
                report.AppendLine($"fold '{name}': authored {want}, measured {got}");
                ctx.Check(node != null, $"'{authored.Anim}' reaches the flown airframe's '{name}'");
                ctx.Check(node != null && Near(got, want),
                    $"and turns it to the {want} the definition authors");
                checkedPairs++;
            }
        }

        ctx.Check(checkedPairs > 0, $"'{authored.Anim}' authors the movers this reads");
    }

    // The airframe's branch of the extend-hook definition: the OBJECT_TRANSLATE_STATE on its own
    // node is the offset it hangs at, and the CALL_ANIMATION after it is that airframe's hook.
    private static (Vector3 Offset, string? HookAnim)? MountBranch(TestWorld world, string planeNode)
    {
        foreach (var def in world.Runtime.DefsFor(ExtendHookAnim))
        {
            foreach (var seq in def.Sequences)
            {
                for (int i = 0; i < seq.Events.Count; i++)
                {
                    if (seq.Events[i].Kind != "ObjectTranslateState"
                        || !string.Equals(seq.Events[i].Data.Str("node"), planeNode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? hook = null;
                    for (int j = i + 1; j < seq.Events.Count && hook == null; j++)
                    {
                        if (seq.Events[j].Kind == "CallAnimation")
                        {
                            hook = seq.Events[j].Data.Str("name");
                        }
                    }

                    return (seq.Events[i].Data.Vec3("state"), hook);
                }
            }
        }

        return null;
    }

    // The airframe's own wing fold, if it authors one: a CALL_ANIMATION inside the hookup's
    // move_player sequence whose definition is rooted on this airframe's node.
    private static (string Anim, AnimDefinition Def)? FoldOf(
        TestWorld world, string hookupAnim, string planeNode)
    {
        foreach (var hookup in world.Runtime.DefsFor(hookupAnim))
        {
            foreach (var seq in hookup.Sequences)
            {
                if (!string.Equals(seq.Name, HookupPlayerSeq, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var ev in seq.Events)
                {
                    if (ev.Kind != "CallAnimation" || ev.Data.Str("name") is not { } called)
                    {
                        continue;
                    }

                    foreach (var def in world.Runtime.DefsFor(called))
                    {
                        if (string.Equals(def.Name, planeNode, StringComparison.OrdinalIgnoreCase))
                        {
                            return (called, def);
                        }
                    }
                }
            }
        }

        return null;
    }

    // The node a definition anchors on, found inside one aircraft rather than the world index, so
    // a second rig built for the next airframe cannot answer for the first.
    private static Node3D? NamedIn(TestWorld world, string animName, Node3D model)
    {
        foreach (var def in world.Runtime.DefsFor(animName))
        {
            if (NamedNode(model, def.Name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool Near(Vector3 a, Vector3 b) => (a - b).Length() <= PoseEpsilon;

    private static Node3D? NamedNode(Node3D root, string name)
    {
        if (string.Equals(AnimRuntime.NameOf(root), name, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d && NamedNode(n3d, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // The session wiring a flown story mission has and the suite harness's world build does not:
    // the cutscene host over the table's own definition closure, the trigger bound to the resolved
    // rows, a rig for it to fly, and the campaign director attached to that rig.
    private static void WithTrigger(
        TestContext ctx,
        TestWorld world,
        CampaignDirector director,
        IReadOnlyList<LandingApproach> armed,
        Action<LandingApproachRuntime, CutsceneController, FlightController, ObjectiveGraph> body,
        Action<ProjectilePool>? beforeBind = null,
        string planeNode = PlaneNode,
        AircraftStage? aircraft = null)
    {
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        cutscene.BindWorld(world.Runtime, aircraft);
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
            rig = BuildRig(ctx, world, pool, planeNode);
            var craft = rig;
            // The session's nearest-human seam: without it the EXECUTION_BY_RANGE poll reads the
            // test camera kilometres off, and a range-armed definition on the flown approach
            // never fires under the rig. A drive wanting the player elsewhere overrides it.
            world.Runtime.PlayerPositions = () => new[] { craft.WorldPosition };
            cutscene.BindRigs(new[]
            {
                new PlayerRig
                {
                    Index = 0,
                    Camera = ctx.Camera,
                    HudParent = ctx.Host,
                    Controller = craft,
                },
            }, () => Array.Empty<FlightController>());
            cutscene.WorldHeld = director.HoldForCutscene;
            // The mission's own actors, before the bind: a row whose approach node arrives with a
            // roster spawn is only there to bind once that spawn has happened, which is the whole
            // ordering the session repeats when it re-binds after its roster build.
            beforeBind?.Invoke(pool);
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
            // ⚠ The roster spawner parents its rigs to the shared suite host, so this mission's
            // roster outlives its own suite unless freed here: a later spawn of a block both
            // missions carry (wingman_4) then collides on the node name and Godot renames it.
            foreach (var spawned in director.Roster.Values)
            {
                spawned.Free();
            }

            world.Runtime.PlayerPositions = null;
            rig?.Free();
            pool.Free();
            textures.Dispose();
            trigger.Free();
            cutscene.Free();
        }
    }

    // CM02's capture gate, which is not a per-aircraft one: the mission arms and disarms its three
    // Balmoral approaches from one pair of ON_CALL definitions covering all three at once, and the
    // objective that calls the arming one waits on those planes' own aiv group being down to one.
    // Each row's approach node is a child of that plane's gamez node, so the volume follows the
    // aircraft it belongs to.
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
        CheckGateCondition(ctx, script, blocks, owners, report);
        WithTrigger(ctx, world, director, rows, (trigger, cutscene, rig, graph) =>
            {
                CheckGrafted(ctx, world, carried, report);
                var gate = CheckGateReach(ctx, world, script, rows, carried, report);
                ctx.Same(rows.Count, trigger.Armed,
                    $"every resolved row binds, the roster-carried ones included: their approach nodes came with the rigs that own them");
                RunTheCapture(ctx, world, trigger, cutscene, rig, carried, gate, report);
            },
            beforeBind: pool => SpawnRoster(ctx, world, director, missionZrdr, pool, report));
    }

    private static void DriveTrainPickup(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var rows = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        var pickup = RowFor(rows, "lookat_copilotpkup")
            ?? throw new InvalidOperationException("CM07 carries no copilot pickup approach row");
        Node3D? caboose = null;
        foreach (var def in world.Session.Program.ByAnimName("trigger_copilot"))
        {
            var anchors = world.Runtime.AnchorsOf(def);
            if (anchors.Count > 0)
            {
                caboose = anchors[0];
                break;
            }
        }
        ctx.Check(caboose != null, $"CM07's trigger_copilot resolves its authored caboose anchor");
        if (caboose == null)
        {
            return;
        }

        WithTrigger(ctx, world, director, rows, (trigger, cutscene, rig, graph) =>
        {
            ctx.Same(0, world.Runtime.FindNodes(pickup.Node).Count,
                $"the pickup approach starts outside the world as library content");
            int armedBefore = trigger.Armed;
            world.Runtime.PlayerPositions = () => new[] { caboose.GlobalPosition };
            rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null,
                new CamParams(), caboose.GlobalPosition, caboose.GlobalPosition + Vector3.Forward,
                0f, 0f);
            world.Runtime.Advance(StepDt);
            trigger.Tick();

            var agents = world.Runtime.FindNodes("pickup_agent");
            var sensors = world.Runtime.FindNodes("ladder_pickup_sensor");
            var approaches = world.Runtime.FindNodes(pickup.Node);
            report.AppendLine($"near caboose: agent={agents.Count} sensor={sensors.Count} " +
                $"approach={approaches.Count} armed={armedBefore}->{trigger.Armed}");
            ctx.Same(1, agents.Count,
                $"trigger_copilot stages the passenger and flare rig on the caboose");
            ctx.Same(1, sensors.Count,
                $"the same call stages the ladder pickup sensor");
            ctx.Same(1, approaches.Count,
                $"and attaches the authored docking approach under that sensor");
            ctx.Same(armedBefore + 1, trigger.Armed,
                $"the landing trigger discovers the approach created after its initial bind");
            if (approaches.Count == 0)
            {
                return;
            }

            // The train's own definition started pickup_timing at the world build; its first
            // open phase runs 12.36 s to 16.45 s into the run.
            var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, approaches[0]);
            for (float t = 0f; t < 14f; t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                trigger.Tick();
            }
            ctx.Check(arm.Count > 0 && arm[0].Visible,
                $"the train's pickup_timing opens land_on in its first flyable phase");
            ctx.Check(Fly(ctx, world, trigger, cutscene, graph, rig, pickup, report),
                $"flying CM07's staged train approach starts '{pickup.Anim}'");
            ctx.Check(cutscene.Playing,
                $"'{pickup.Anim}' takes ownership of the session instead of ending immediately");
            var gated = ObjectiveForInactive(script, "pickup_objective");
            ctx.Check(gated != null,
                $"CM07 gates an objective on pickup_objective becoming inactive");
            if (gated == null)
            {
                return;
            }

            var objectiveNodes = world.Runtime.FindNodes("pickup_objective");
            ctx.Check(objectiveNodes.Count > 0 && !objectiveNodes[0].Visible,
                $"the pickup calls got_the_pilot and deactivates pickup_objective");
            var authoredCameras = world.Runtime.FindNodes(CutsceneController.CameraNode);
            var authoredCamera = authoredCameras.Count > 0 ? authoredCameras[0] : null;
            var cameraParent = authoredCamera?.GetParent() as Node3D;
            float agentFacing = authoredCamera != null && agents.Count > 0
                ? -authoredCamera.GlobalBasis.Z.Dot(
                    authoredCamera.GlobalPosition.DirectionTo(agents[0].GlobalPosition))
                : -1f;
            report.AppendLine($"pickup camera=({ctx.Camera.GlobalPosition.X:0.#}," +
                $"{ctx.Camera.GlobalPosition.Y:0.#},{ctx.Camera.GlobalPosition.Z:0.#}) " +
                $"authored-parent={cameraParent?.Name} parent-distance=" +
                $"{(cameraParent?.GlobalPosition.DistanceTo(ctx.Camera.GlobalPosition) ?? -1f):0.#} " +
                $"agent-facing={agentFacing:0.##}");
            ctx.Check(cameraParent?.Name == "caboose"
                    && cameraParent.GlobalPosition.DistanceTo(ctx.Camera.GlobalPosition) < 100f
                    && agentFacing > 0f,
                $"the player view follows the pickup camera beside the caboose and faces the passenger");

            float played = 0f;
            for (float t = 0f; t < PlayBudgetS
                    && (cutscene.Playing || !graph.CompletedOf(gated.Number)); t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                cutscene.Tick();
                director.Step(StepDt);
                if (cutscene.Playing)
                {
                    played += StepDt;
                }
            }

            report.AppendLine($"pickup episode ran {played:0.##} s; " +
                $"OBJECTIVE{gated.Number} completed={graph.CompletedOf(gated.Number)}");
            ctx.Check(!cutscene.Playing && played > 2f,
                $"the authored pickup camera episode runs to its handoff");
            ctx.Check(graph.CompletedOf(gated.Number),
                $"finishing the train pickup clears OBJECTIVE{gated.Number}");
        });
    }

    // CM07's pickup as the passenger, the flare and the ladder see it. The train is the mission's
    // own moving consist, so "rides the train" is measured as a constant offset from the caboose
    // while the caboose itself moves; the ladder switch is the session's own runtime, bound here
    // the way GameSession binds it, and driven by the rig's attitude and position.
    private static void DriveTrainPickupRide(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var rows = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        var pickup = RowFor(rows, "lookat_copilotpkup")
            ?? throw new InvalidOperationException("CM07 carries no copilot pickup approach row");
        // The consist's caboose is the one caboosewave's own symbol table binds (the chapter also
        // carries an unrelated caboose.flt in the rail yard, which name matching would reach).
        Node3D? caboose = null;
        foreach (var def in world.Session.Program.ByAnimName("caboosewave"))
        {
            if (def.NodeRefs.TryGetValue("caboose", out int index))
            {
                caboose = world.Runtime.FindNodeByIndex(index);
                break;
            }
        }
        ctx.Check(caboose != null, $"caboosewave's symbol table binds the consist's caboose");
        if (caboose == null)
        {
            return;
        }

        var pickups = Pickups.Load(missionZrdr);
        WithTrigger(ctx, world, director, rows, (trigger, cutscene, rig, graph) =>
        {
            var ladder = new LadderSwitchRuntime();
            ctx.Host.AddChild(ladder);
            try
            {
                ladder.Bind(world.Runtime, cutscene, () => rig, pickups);
                world.Runtime.PlayerPositions = () => new[] { caboose.GlobalPosition };
                rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null,
                    new CamParams(), caboose.GlobalPosition, caboose.GlobalPosition + Vector3.Forward,
                    0f, 0f);
                world.Runtime.Advance(StepDt);
                trigger.Tick();

                var agents = world.Runtime.FindNodes("pickup_agent");
                ctx.Same(1, agents.Count, $"trigger_copilot stages the passenger on the caboose");
                if (agents.Count == 0)
                {
                    return;
                }

                var agent = agents[0];
                report.AppendLine($"passenger chain: {ChainOf(agent)}; caboose chain: {ChainOf(caboose)}; " +
                    $"top-level={agent.TopLevel} states: caboosewave={world.Runtime.AnimStateOf("caboosewave")} " +
                    $"train_on_track={world.Runtime.AnimStateOf("train_on_track")} " +
                    $"pickup_timing={world.Runtime.AnimStateOf("pickup_timing")} " +
                    $"unhandled add-child={(world.Runtime.UnhandledEventCounts.TryGetValue("ObjectAddChild", out int addChild) ? addChild : 0)}");
                ctx.Check(IsUnder(agent, caboose),
                    $"the passenger is a child of the caboose, not a free node beside it");
                ctx.Check(agent.Visible, $"and the passenger is active");
                var cabooseBefore = caboose.GlobalPosition;
                var offsetBefore = caboose.GlobalTransform.AffineInverse() * agent.GlobalPosition;
                for (float t = 0f; t < 3f; t += StepDt)
                {
                    world.Runtime.Advance(StepDt);
                    trigger.Tick();
                }
                float travelled = caboose.GlobalPosition.DistanceTo(cabooseBefore);
                var offsetAfter = caboose.GlobalTransform.AffineInverse() * agent.GlobalPosition;
                report.AppendLine($"caboose travelled {travelled:0.#} m in 3 s; passenger offset " +
                    $"{offsetBefore.DistanceTo(offsetAfter):0.###} m from where it stood");
                ctx.Check(travelled > 1f, $"the train is moving on its track");
                ctx.Check(offsetBefore.DistanceTo(offsetAfter) < 0.5f,
                    $"the passenger rides the caboose instead of staying where it spawned");

                // The pickup timing, started by the train's own definition, opens the switch in
                // the phases of the track loop where the pickup is flyable; the wave and its flare
                // are what the switch being active selects, and the passenger lies flat otherwise.
                float waited = 0f;
                for (; waited < 60f && world.Runtime.AnimStateOf("waveloop") != 2; waited += StepDt)
                {
                    world.Runtime.Advance(StepDt);
                    trigger.Tick();
                }
                report.AppendLine($"waveloop live after {waited:0.#} s more");
                var switches = world.Runtime.FindNodes("copilot_pickup_switch");
                var sensors = world.Runtime.FindNodes("ladder_pickup_sensor");
                var cones = world.Runtime.FindNodes("agent_approach_cone");
                report.AppendLine($"switch chain: {(switches.Count > 0 ? ChainOf(switches[0]) : "-")} " +
                    $"top-level={(switches.Count > 0 ? switches[0].TopLevel.ToString() : "-")}; sensor chain: " +
                    $"{(sensors.Count > 0 ? ChainOf(sensors[0]) : "-")} top-level=" +
                    $"{(sensors.Count > 0 ? sensors[0].TopLevel.ToString() : "-")} caboose-distance=" +
                    $"{(sensors.Count > 0 ? sensors[0].GlobalPosition.DistanceTo(caboose.GlobalPosition) : -1f):0.#}; " +
                    $"cone chain: {(cones.Count > 0 ? ChainOf(cones[0]) : "-")} caboose-distance=" +
                    $"{(cones.Count > 0 ? cones[0].GlobalPosition.DistanceTo(caboose.GlobalPosition) : -1f):0.#}");
                report.AppendLine($"then: switch={switches.Count} visible=" +
                    $"{(switches.Count > 0 ? switches[0].Visible.ToString() : "-")} sensor={sensors.Count} visible=" +
                    $"{(sensors.Count > 0 ? sensors[0].Visible.ToString() : "-")} " +
                    $"states: caboosewave={world.Runtime.AnimStateOf("caboosewave")} waveloop={world.Runtime.AnimStateOf("waveloop")} " +
                    $"hit_the_deck={world.Runtime.AnimStateOf("hit_the_deck")} get_up={world.Runtime.AnimStateOf("get_up")} " +
                    $"pickup_timing={world.Runtime.AnimStateOf("pickup_timing")}; rig inplay={rig.InPlay} held={rig.Held} " +
                    $"level={LadderSwitch.IsLevel(rig.Attitude)} sensor-distance=" +
                    $"{(sensors.Count > 0 ? sensors[0].GlobalPosition.DistanceTo(rig.WorldPosition) : -1f):0.#}");
                ctx.Same(2, world.Runtime.AnimStateOf("waveloop"),
                    $"with the pickup switch open the passenger waves (waveloop live)");
                ctx.Same(2, world.Runtime.AnimStateOf("pickup_flare"),
                    $"and holds the lit flare (pickup_flare live)");
                var flares = world.Runtime.FindNodes("ballflare.flt");
                var flare = flares.Count > 0 ? flares[0] : null;
                var trails = world.Runtime.Emitters.Census.Where(r =>
                    string.Equals(r.Name, "flaretrail", StringComparison.OrdinalIgnoreCase)).ToList();
                report.AppendLine($"flare={flares.Count} under passenger=" +
                    $"{(flare != null && IsUnder(flare, agent))} visible={flare?.Visible} " +
                    $"trail={(trails.Count == 0 ? "absent" : trails[0].Emitting ? "emitting" : "built")}" +
                    $"{(trails.Count > 0 ? $" on '{trails[0].Host}'" : "")}; pickup_flare=" +
                    $"{world.Runtime.AnimStateOf("pickup_flare")} unhandled: " + string.Join(", ",
                        world.Runtime.UnhandledEventCounts
                            .Where(kv => kv.Key.StartsWith("PufferState", StringComparison.Ordinal)
                                || kv.Key.StartsWith("ObjectAddChild", StringComparison.Ordinal))
                            .Select(kv => $"{kv.Key}={kv.Value}")));
                ctx.Check(flare != null && IsUnder(flare, agent) && flare.Visible,
                    $"the flare disc hangs from the passenger's hand and is drawn");
                ctx.Check(trails.Count > 0,
                    $"the flare's smoke trail emitter is asserted on the passenger's hand");

                // The ladder: level inside the sensor drops it, the settle callback lands it. The
                // train has travelled on since the rig was parked, so the rig is re-parked level
                // beside the sensor as it stands now.
                if (sensors.Count > 0)
                {
                    var beside = sensors[0].GlobalPosition + Vector3.Up * 10f;
                    rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null,
                        new CamParams(), beside, beside + Vector3.Forward, 0f, 0f);
                }
                ladder.Tick();
                report.AppendLine($"ladder after a level tick inside the sensor: " +
                    $"{ladder.LastStarted ?? "-"} ({ladder.State})");
                ctx.Check(string.Equals(ladder.LastStarted, LadderSwitch.DropAnim, StringComparison.Ordinal),
                    $"a level aircraft inside the pickup sensor starts drop_ladder");
                ctx.Same(2, world.Runtime.AnimStateOf(LadderSwitch.DropAnim),
                    $"drop_ladder has a live instance");
                for (float t = 0f; t < 3f && ladder.State != LadderState.Deployed; t += StepDt)
                {
                    world.Runtime.Advance(StepDt);
                    ladder.Tick();
                }
                var ladders = world.Runtime.FindNodes("rope_ladder");
                report.AppendLine($"ladder settled: {ladder.State}; rope_ladder nodes={ladders.Count} " +
                    $"visible={(ladders.Count > 0 ? ladders[0].Visible.ToString() : "-")} " +
                    $"ladder_pos nodes={world.Runtime.FindNodes("ladder_pos").Count} (the harness rig " +
                    $"carries no airframe-stage ladder_pos, so the rungs are the session's to show)");
                ctx.Check(ladder.State == LadderState.Deployed,
                    $"the drop's own CALLBACK 123 settles the switch deployed");

                // The docking cone, then the cutscene's own call chain.
                int waitsBefore = world.Runtime.WaitsInstalled;
                ctx.Check(Fly(ctx, world, trigger, cutscene, graph, rig, pickup, report),
                    $"flying CM07's staged train approach starts '{pickup.Anim}'");
                ctx.Same(2, world.Runtime.AnimStateOf("caboosepickup"),
                    $"'{pickup.Anim}' calls caboosepickup and it has a live instance");
                ctx.Same(waitsBefore + 1, world.Runtime.WaitsInstalled,
                    $"so the WAIT_FOR_COMPLETION on it holds instead of finding nothing");
                ctx.Check(IsUnder(agent, caboose),
                    $"the passenger is still the caboose's child through the pickup");

                float played = 0f;
                float climb = 0f;
                for (float t = 0f; t < PlayBudgetS && cutscene.Playing; t += StepDt)
                {
                    world.Runtime.Advance(StepDt);
                    cutscene.Tick();
                    director.Step(StepDt);
                    played += StepDt;
                    if (world.Runtime.AnimStateOf("caboosepickup") == 2)
                    {
                        climb += StepDt;
                    }
                }
                report.AppendLine($"pickup episode ran {played:0.##} s, caboosepickup live for " +
                    $"{climb:0.##} s of it");
                ctx.Check(!cutscene.Playing && played > 2f,
                    $"the authored pickup camera episode runs to its handoff");
                ctx.Check(climb > 1f, $"the person's climb plays for its scripted length");
            }
            finally
            {
                ladder.Free();
            }
        });
    }

    private static string ChainOf(Node node)
    {
        var names = new List<string>();
        for (Node? at = node; at != null && names.Count < 8; at = at.GetParent())
        {
            names.Add(at.Name);
        }
        return string.Join(" < ", names);
    }

    private static bool IsUnder(Node node, Node ancestor)
    {
        for (var at = node.GetParent(); at != null; at = at.GetParent())
        {
            if (at == ancestor)
            {
                return true;
            }
        }
        return false;
    }

    // CM11's trailer pickup: unlike CM07's, the cone is staged by an objective's WAKE_ANIM rather
    // than a range-triggered call, so this is the director's own trigger path. The staging
    // definition and the objective that wakes it are read out of the mission's data.
    private static void DriveTrailerPickup(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter);
        var rows = LandingApproaches.Resolve(
            chapterZrdr, world.Gamez, name => world.Runtime.Handles(name));
        var pickup = RowFor(rows, "lookat_pickfordpkup")
            ?? throw new InvalidOperationException("CM11 carries no trailer pickup approach row");
        var staging = StagerOf(world, pickup.Node);
        var caller = staging?.Anim is { } stagerAnim ? CallerOf(script, stagerAnim) : null;
        report.AppendLine($"'{pickup.Node}' is staged by '{staging?.Anim ?? "-"}' under " +
            $"'{staging?.Parent ?? "-"}', woken by OBJECTIVE{caller?.Number.ToString() ?? "?"}");
        ctx.Check(staging != null, $"a definition's OBJECT_ADD_CHILD stages '{pickup.Node}'");
        ctx.Check(caller != null, $"and an objective's WAKE_ANIM wakes that definition");
        if (staging is not { } stager || caller is not { } wakes)
        {
            return;
        }

        WithTrigger(ctx, world, director, rows, (trigger, cutscene, rig, graph) =>
        {
            ctx.Same(0, world.Runtime.FindNodes(pickup.Node).Count,
                $"the pickup approach starts outside the world as library content");
            int armedBefore = trigger.Armed;
            graph.Wake(wakes.Number);
            world.Runtime.Advance(StepDt);
            graph.Step(StepDt);
            trigger.Tick();

            var approaches = world.Runtime.FindNodes(pickup.Node);
            var parent = approaches.Count > 0 ? approaches[0].GetParent() as Node3D : null;
            report.AppendLine($"after the wake: approach={approaches.Count} parent=" +
                $"{parent?.Name ?? "-"} armed={armedBefore}->{trigger.Armed}");
            ctx.Same(1, approaches.Count,
                $"OBJECTIVE{wakes.Number}'s WAKE_ANIM '{stager.Anim}' stages the approach cone from its library root");
            ctx.Check(parent != null && string.Equals(parent.Name, stager.Parent, StringComparison.OrdinalIgnoreCase),
                $"under '{stager.Parent}', the node the event names, so the cone rides the trailer");
            ctx.Same(armedBefore + 1, trigger.Armed,
                $"the landing trigger discovers the approach staged after its initial bind");
            if (approaches.Count == 0)
            {
                return;
            }

            var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, approaches[0]);
            ctx.Check(arm.Count > 0 && arm[0].Visible,
                $"the staged cone's land_on is open, this mission authoring no pickup timing in front of it");
            // The approach the original flies: closing on the trailer fires its range-armed
            // definition, whose authored hatch time runs before it raises the actor the pickup's
            // prerequisite requires. The parked wait is the one the train drive gives its timing.
            var site = approaches[0].GlobalPosition;
            rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null,
                new CamParams(), site, site + Vector3.Forward, 0f, 0f);
            for (float t = 0f; t < TrailerApproachS; t += StepDt)
            {
                world.Runtime.Advance(StepDt);
            }

            var prereqs = PrerequisitesOf(world, pickup.Anim);
            report.AppendLine($"'{pickup.Anim}' requires " + string.Join(", ",
                prereqs.Select(p => $"{p.Node}={(p.Active ? "active" : "inactive")} (reads {p.Met})")));
            ctx.Check(prereqs.Count > 0 && prereqs.All(p => p.Met),
                $"closing on the trailer meets '{pickup.Anim}'s own node-state prerequisite");
            ctx.Check(Fly(ctx, world, trigger, cutscene, graph, rig, pickup, report),
                $"flying CM11's staged trailer approach starts '{pickup.Anim}'");
            ctx.Check(cutscene.Playing,
                $"'{pickup.Anim}' takes ownership of the session instead of ending immediately");

            float played = 0f;
            for (float t = 0f; t < PlayBudgetS
                    && (cutscene.Playing || !graph.CompletedOf(wakes.Number)); t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                cutscene.Tick();
                director.Step(StepDt);
                if (cutscene.Playing)
                {
                    played += StepDt;
                }
            }

            report.AppendLine($"pickup episode ran {played:0.##} s; " +
                $"OBJECTIVE{wakes.Number} completed={graph.CompletedOf(wakes.Number)}");
            ctx.Check(!cutscene.Playing && played > 2f,
                $"the authored pickup episode runs to its handoff");
            ctx.Check(graph.CompletedOf(wakes.Number),
                $"finishing the trailer pickup clears OBJECTIVE{wakes.Number}, the dock");
        });
    }

    // CM07's hangar drop, every name read out of the mission's own data: the one cutscene
    // definition it range-gates, whatever calls that, and the objective node the drop's own reset
    // block flips through its CALL_ANIMATION.
    private static void DriveHangarDrop(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, string missionZrdr, StringBuilder report)
    {
        var cutscenes = MissionCutscenes.AnimNames(missionZrdr);
        report.AppendLine($"cutscene definitions: {string.Join(", ", cutscenes)}");
        var drop = RangeGatedOf(world, cutscenes);
        ctx.Check(drop?.AnimName != null,
            $"CM07 loads a range-gated cutscene definition out of its own cutscenes directory");
        if (drop?.AnimName is not { } dropAnim)
        {
            return;
        }

        string? caller = CallerAnimOf(world, dropAnim);
        string? node = ResetObjectiveNodeOf(world, script, drop);
        var gated = node != null ? ObjectiveForInactive(script, node) : null;
        report.AppendLine($"'{dropAnim}' range={Mathf.Sqrt(drop.RangeMax):0} m " +
            $"called by '{caller ?? "-"}', reset clears '{node ?? "-"}' " +
            $"gating OBJECTIVE{gated?.Number ?? -1}");
        ctx.Check(caller != null,
            $"an ambient definition calls '{dropAnim}', which is the only thing that arms it");
        ctx.Check(gated != null,
            $"and '{dropAnim}' clears the node an objective waits on, through its own RESET_STATE");
        if (caller == null || node == null || gated == null)
        {
            return;
        }

        RunTheDrop(ctx, world, director, drop, caller, node, gated, report);
    }

    // The armed-then-flown drive. The player is parked well outside the band for the call, then
    // put on the hangar, which is the only difference between the two halves.
    private static void RunTheDrop(
        TestContext ctx, TestWorld world, CampaignDirector director, AnimDefinition drop,
        string caller, string node, ObjectiveDef gated, StringBuilder report)
    {
        var anchors = world.Runtime.AnchorsOf(drop);
        var anchor = anchors.Count > 0 ? anchors[0] : null;
        ctx.Check(anchor != null, $"'{drop.AnimName}' resolves its authored hangar anchor");
        if (anchor == null)
        {
            return;
        }

        var site = AnimRuntime.VisualOriginOf(anchor);
        WithTrigger(ctx, world, director, Array.Empty<LandingApproach>(),
            (trigger, cutscene, rig, graph) =>
        {
            cutscene.HostDefinitions(ClosureOf(world, drop.AnimName!));
            graph.Wake(gated.Number);
            world.Runtime.PlayerPositions = () => new[] { site + (Vector3.Right * AwayM) };
            world.Runtime.Play(caller);
            for (float t = 0f; t < ArmBudgetS; t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                director.Step(StepDt);
            }

            report.AppendLine($"{AwayM:0} m away: '{drop.AnimName}' state=" +
                $"{world.Runtime.AnimStateOf(drop.AnimName!)} playing={cutscene.Playing} " +
                $"'{node}' built={world.Runtime.FindNodes(node).Count} " +
                $"OBJECTIVE{gated.Number} completed={graph.CompletedOf(gated.Number)}");
            ctx.Same(0, world.Runtime.AnimStateOf(drop.AnimName!),
                $"'{caller}' arms the drop without running it, the player being outside its band");
            ctx.Check(!graph.CompletedOf(gated.Number),
                $"so OBJECTIVE{gated.Number} stays open with the objective awake and stepping");

            world.Runtime.PlayerPositions = () => new[] { site };
            float played = 0f;
            for (float t = 0f; t < PlayBudgetS && (played == 0f || cutscene.Playing); t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                cutscene.Tick();
                director.Step(StepDt);
                played += cutscene.Playing ? StepDt : 0f;
            }

            var placed = world.Runtime.FindNodes(node);
            report.AppendLine($"at the hangar: episode ran {played:0.##} s, " +
                $"'{node}' built={placed.Count} active={Active(world, node)}, " +
                $"OBJECTIVE{gated.Number} completed={graph.CompletedOf(gated.Number)}");
            ctx.Check(played > 0f,
                $"reaching the hangar runs the armed drop and hands it to the cutscene host");
            ctx.Check(placed.Count > 0 && !Active(world, node),
                $"and its RESET_STATE at the handoff stands '{node}' up and clears it");
            ctx.Check(graph.CompletedOf(gated.Number),
                $"which completes OBJECTIVE{gated.Number}, the fly-through the mission asks for");
        });
    }

    // The mission cutscene definition whose EXECUTION_BY_RANGE is an arming gate: a call is its
    // only way in, so a definition the mission also lists in startanims is not this one.
    private static AnimDefinition? RangeGatedOf(TestWorld world, IReadOnlyList<string> cutscenes)
    {
        foreach (string name in cutscenes)
        {
            foreach (var def in world.Session.Program.ByAnimName(name))
            {
                if (def.ByRange && !System.Linq.Enumerable.Contains(world.Session.Program.StartAnims, name))
                {
                    return def;
                }
            }
        }

        return null;
    }

    // The definition whose OBJECT_ADD_CHILD parents the named node, and the parent it names.
    private static (string Anim, string Parent)? StagerOf(TestWorld world, string child)
    {
        foreach (var def in world.Session.Program.Defs)
        {
            foreach (var seq in def.Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind == "ObjectAddChild" && ev.Data.Str("child") is { } named
                        && named.Equals(child, StringComparison.OrdinalIgnoreCase)
                        && ev.Data.Str("parent") is { } parent
                        && def.AnimName is { Length: > 0 } anim)
                    {
                        return (anim, parent);
                    }
                }
            }
        }

        return null;
    }

    private static string? CallerAnimOf(TestWorld world, string called)
    {
        foreach (var def in world.Session.Program.Defs)
        {
            foreach (var seq in def.Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } name
                        && name.Equals(called, StringComparison.OrdinalIgnoreCase)
                        && def.AnimName is { Length: > 0 } caller)
                    {
                        return caller;
                    }
                }
            }
        }

        return null;
    }

    // What the drop's own RESET_STATE calls, resolved to the node that call deactivates: the
    // objective flag definitions name their node, and the mission gates an INACTIVE on it.
    private static string? ResetObjectiveNodeOf(
        TestWorld world, ObjectiveScript script, AnimDefinition drop)
    {
        if (drop.ResetState == null)
        {
            return null;
        }

        foreach (var ev in drop.ResetState.Events)
        {
            if (ev.Kind != "CallAnimation" || ev.Data.Str("name") is not { } called)
            {
                continue;
            }

            foreach (var def in world.Session.Program.ByAnimName(called))
            {
                if (def.Name is { Length: > 0 } named
                    && ObjectiveForInactive(script, named) != null)
                {
                    return named;
                }
            }
        }

        return null;
    }

    private static bool Active(TestWorld world, string node)
    {
        var found = world.Runtime.FindNodes(node);
        return found.Count > 0 && found[0].Visible;
    }

    private static IReadOnlyList<string> ClosureOf(TestWorld world, string root)
    {
        var names = new List<string>();
        foreach (var def in world.Session.Program.Subset(root).Defs)
        {
            if (def.AnimName is { } name && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    // The three rows are symmetric and each belongs to a plane the mission's roster spawns. Read
    // BEFORE the roster is spawned, which is where the reach used to end: the approach nodes hang
    // under a gamez library root the world build never places.
    private static void CheckCarriedRows(
        TestContext ctx, TestWorld world, IReadOnlyList<LandingApproach> carried,
        IReadOnlyList<string> owners, StringBuilder report)
    {
        report.AppendLine($"{carried.Count} row(s) carried by a roster vehicle: " +
            $"{string.Join(", ", owners)}");
        ctx.Same(3, carried.Count,
            $"CM02 authors one approach row per Balmoral, each under that plane's own gamez node");
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
            $"the world build alone reaches none of their approach nodes, a vehicle's gamez node being a library root it never places");
    }

    // What the graft put in the world: one approach node per carried row, each a descendant of the
    // rig its own block spawned, so the volume moves with the aircraft rather than standing where
    // the plane happened to start.
    private static void CheckGrafted(
        TestContext ctx, TestWorld world, IReadOnlyList<LandingApproach> carried,
        StringBuilder report)
    {
        int built = 0;
        int onRig = 0;
        foreach (var row in carried)
        {
            var found = world.Runtime.FindNodes(row.Node);
            built += found.Count;
            var owner = found.Count > 0 ? RigAbove(found[0]) : null;
            onRig += owner != null ? 1 : 0;
            report.AppendLine($"  '{row.Node}' built={found.Count} " +
                $"on rig '{owner?.Name.ToString() ?? "-"}' at " +
                $"{(found.Count > 0 ? found[0].GlobalPosition : Vector3.Zero)}");
        }

        ctx.Same(carried.Count, built,
            $"the roster spawn carries each Balmoral's own approach node into the world");
        ctx.Same(carried.Count, onRig,
            $"and each one hangs under the rig its block spawned, so it follows the aircraft under SET_AI_NET rather than drifting away from it");
    }

    // The mission's own gate pair, found from the compiled definitions rather than named here: the
    // two definitions that write every carried row's land_on node, and what they can reach today.
    private static List<string> CheckGateReach(
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
            $"the chapter carries static approach rows beside these, so the two kinds of owner are told apart in one run");
        int reached = 0;
        foreach (int index in arms)
        {
            reached += index >= 0 && BuiltCount(world, index) == 1 ? 1 : 0;
        }

        ctx.Same(arms.Count, reached,
            $"and every land_on the pair writes is a built node carrying that gamez index, which is what makes an index-addressed write land");
        return gate;
    }

    // The gate driven on the nodes it now reaches: the pair's own two definitions, told apart by
    // what they do rather than by name, and the capture flown at an armed Balmoral.
    private static void RunTheCapture(
        TestContext ctx, TestWorld world, LandingApproachRuntime trigger, CutsceneController cutscene,
        FlightController rig, IReadOnlyList<LandingApproach> carried, IReadOnlyList<string> gate,
        StringBuilder report)
    {
        ctx.Same(0, ArmedCount(world, carried),
            $"the capture starts un-armed: every land_on ships inactive, so it is not offered while more than one Balmoral flies");
        ctx.Check(!Fly(ctx, world, trigger, cutscene, null, rig, carried[0], report),
            $"and flying '{carried[0].Node}' before the gate opens starts nothing");

        var arming = GateRun(world, gate, carried, wantArmed: carried.Count, report);
        if (arming == null)
        {
            ctx.Check(false, $"one of the pair arms all {carried.Count} rows");
            return;
        }

        ctx.Check(Fly(ctx, world, trigger, cutscene, null, rig, carried[0], report),
            $"flying '{carried[0].Node}' once the gate has armed it starts '{trigger.LastStarted}'");
        report.AppendLine($"armed by '{arming}', started '{trigger.LastStarted}'");
        var clearing = GateRun(world, gate, carried, wantArmed: 0, report, skip: arming);
        ctx.Check(clearing != null,
            $"and the pair's other definition takes the same three rows back off, so the capture can go away again");
    }

    // Plays each gate definition in turn until the carried rows' arm bits reach `wantArmed`,
    // returning the one that did it. The pair is not named here: which of the two arms is read off
    // what the run does to the nodes.
    private static string? GateRun(
        TestWorld world, IReadOnlyList<string> gate, IReadOnlyList<LandingApproach> carried,
        int wantArmed, StringBuilder report, string? skip = null)
    {
        foreach (string name in gate)
        {
            if (string.Equals(name, skip, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            world.Runtime.Play(name);
            for (float t = 0f; t < ArmBudgetS && ArmedCount(world, carried) != wantArmed; t += StepDt)
            {
                world.Runtime.Advance(StepDt);
            }

            int armed = ArmedCount(world, carried);
            report.AppendLine($"'{name}' run: {armed} of {carried.Count} land_on armed");
            if (armed == wantArmed)
            {
                return name;
            }
        }

        return null;
    }

    private static int ArmedCount(TestWorld world, IReadOnlyList<LandingApproach> carried)
    {
        int armed = 0;
        foreach (var row in carried)
        {
            var found = world.Runtime.FindNodes(row.Node);
            if (found.Count == 0)
            {
                continue;
            }

            var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, found[0]);
            armed += arm.Count > 0 && arm[0].Visible ? 1 : 0;
        }

        return armed;
    }

    // The aircraft a grafted node hangs under, or null when it stands in the world on its own.
    private static FlightController? RigAbove(Node3D node)
    {
        for (Node? at = node; at != null; at = at.GetParent())
        {
            if (at is FlightController rig)
            {
                return rig;
            }
        }

        return null;
    }

    // The mission's roster, spawned through the session's own spawner and the director's own
    // roster phase, with the marker graft wired exactly as GameSession wires it.
    private static void SpawnRoster(TestContext ctx, TestWorld world, CampaignDirector director,
        string missionZrdr, ProjectilePool pool, StringBuilder report)
    {
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        try
        {
            var spawner = CampaignRosterSuites.Spawner(ctx, planesGamez, textures, pool);
            int grafted = 0;
            string what = director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, world.Chapter),
                MissionZrdrPath = missionZrdr,
                ZrdrPath = ctx.ZrdrPath,
                FindNodes = name => world.Runtime.FindNodes(name),
                Spawn = (plan, pos, look, pilot) =>
                    spawner.SpawnAi(CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                AttachMarkers = (block, node) => grafted += RosterMarkers.Attach(
                    world.Gamez, world.Session.Builder.Scene, world.Runtime, block, node),
                Rng = new Random(1),
            });
            report.AppendLine($"roster: {director.Roster.Count} rig(s), {grafted} marker graft(s){what}");
            ctx.Check(grafted > 0,
                $"the mission's roster spawn grafts the scaffolding its blocks author: {grafted} subtree(s)");
        }
        finally
        {
            textures.Dispose();
        }
    }

    // What the arming objective waits on: the DEDG the objective ahead of it authors, over the
    // group the three planes' own roster blocks are in.
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

        report.AppendLine($"the carried rows' planes are aiv group {group}");
        ctx.Check(oneGroup && group > 0,
            $"the three Balmorals share one aiv group, which is what a DEDG condition counts");
        var gate = DedgAhead(script, group);
        report.AppendLine(gate is { } found
            ? $"OBJECTIVE{found.Number} waits on DEDG [{found.Dedg!.Value.Group}, " +
              $"{found.Dedg.Value.Max}] and wakes the objective that calls the arming animation"
            : "no DEDG objective wakes an objective that calls a gate animation");
        ctx.Check(gate?.Dedg is { Max: 1 },
            $"and the capture waits on that group being down to one, so it is not offered while more than one Balmoral flies");
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

    // Arms a row generically: whichever objective's WAKE_ANIM writes its land_on gamez node,
    // found from the compiled definitions rather than named here, woken directly rather than
    // waited on, since the row's own site is not necessarily on the mission's main path.
    private static void ArmRow(TestContext ctx, TestWorld world, ObjectiveGraph graph,
        ObjectiveScript script, LandingApproach approach, StringBuilder report)
    {
        var node = world.Runtime.FindNodes(approach.Node)[0];
        int armIndex = ArmIndexOf(world.Gamez, approach.Node);
        var gate = armIndex >= 0 ? GateDefs(world, new[] { armIndex }) : new List<string>();
        var caller = gate.Count > 0 ? CallerOf(script, gate[0]) : null;
        report.AppendLine($"'{approach.Node}' land_on gamez node {armIndex}, gate def(s) " +
            $"[{string.Join(", ", gate)}], caller OBJECTIVE{caller?.Number.ToString() ?? "?"}");
        ctx.Check(caller != null, $"an objective's WAKE_ANIM arms '{approach.Node}'");
        if (caller is { } found)
        {
            graph.Wake(found.Number);
        }

        // Polled rather than a fixed budget: WAKE_ANIM's own animation runs its authored timeline
        // before it touches the node state, and that duration is not this suite's to guess at.
        var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, node);
        float armedAt = -1f;
        for (float t = 0f; t < AutoArmBudgetS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            graph.Step(StepDt);
            if (arm.Count > 0 && arm[0].Visible)
            {
                armedAt = t;
                break;
            }
        }

        report.AppendLine($"armed: '{approach.Node}' land_on " +
            $"{(arm.Count > 0 ? arm[0].Visible.ToString() : "absent")} at t={armedAt:0.#}s");
        ctx.Check(arm.Count > 0 && arm[0].Visible,
            $"the mission's own objective chain arms '{approach.Node}'");
    }

    private static FlightController BuildRig(TestContext ctx, TestWorld world, ProjectilePool pool,
        string planeNode = PlaneNode)
    {
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        try
        {
            var stats = PlaneStats.Load(ctx.ZrdrPath, planeNode);
            var model = new PlaneBuilder(GameZ.Load(ctx.PlanesGamezPath), textures,
                dockingHook: true).Build(planeNode);
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

    // The REQUIRED node-state prerequisites of every definition under an animation name, each with
    // whether the built world reads it met now. The node is the path's leaf, resolved globally.
    private static List<(string Node, bool Active, bool Met)> PrerequisitesOf(TestWorld world, string anim)
    {
        var found = new List<(string, bool, bool)>();
        foreach (var def in world.Session.Program.ByAnimName(anim))
        {
            foreach (var prereq in def.PrereqNodes)
            {
                if (!prereq.Required)
                {
                    continue;
                }

                string leaf = prereq.Path[prereq.Path.Count - 1];
                var nodes = world.Runtime.FindNodes(leaf);
                bool met = nodes.Count > 0 && nodes.All(n => n.Visible == prereq.Active);
                found.Add((leaf, prereq.Active, met));
            }
        }

        return found;
    }

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

    private static ObjectiveDef? ObjectiveForInactive(ObjectiveScript script, string node)
    {
        foreach (var def in script.Objectives)
        {
            foreach (var path in def.Inactive)
            {
                if (path.Count > 0 && string.Equals(path[0], node,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return def;
                }
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

    private static LandingApproach? AutoFor(IReadOnlyList<LandingApproach> armed)
    {
        foreach (var approach in armed)
        {
            if (approach.Auto)
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
