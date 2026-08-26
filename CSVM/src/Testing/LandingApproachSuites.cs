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
    private const int TrainPickupSeq = 6;
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

    // How long the auto row's own WAKE_ANIM is given to reach the node write, polled rather than
    // assumed instant.
    private const float AutoArmBudgetS = 30f;

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

    /// <summary>Drives CM02's wing-walk capture gate against its BUILT world with its own roster
    /// spawned: three symmetric Balmoral rows, each authored under its plane's gamez node, grafted
    /// onto the rig the roster spawns so all three bind; one pair of definitions switching all
    /// three <c>land_on</c> nodes by gamez index, which arms and un-arms the rows it now reaches;
    /// an arming objective that waits on those planes' aiv group being down to one; and a driven
    /// approach at an armed Balmoral starting the capture.</summary>
    internal static void WingWalkCaptureGate(TestContext ctx) =>
        DriveMission(ctx, WingWalkSeq, "test-wingwalk-gate", DriveWingWalk);

    /// <summary>Drives CM07's caboose pickup through its range-triggered start animation: the
    /// passenger's library-root rig appears on the train, its 100 m pickup timing opens the late
    /// approach cone, and flying that cone starts the pickup cutscene and clears its objective.</summary>
    internal static void TrainPickupGate(TestContext ctx) =>
        DriveMission(ctx, TrainPickupSeq, "test-train-pickup-gate", DriveTrainPickup);

    /// <summary>Drives the auto-land button over the campaign's first mission's BUILT world: flying
    /// into the chapter's <c>auto</c> row lights <see cref="LandingApproachRuntime.AutoLandOffered"/>
    /// but starts nothing on its own, pressing the button starts the row's animation the way the
    /// manual row would, and holding the button past the handoff does not re-fire it.</summary>
    internal static void AutoLandButton(TestContext ctx) =>
        DriveMission(ctx, FirstSeq, "test-autoland-button", DriveAutoLand);

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
        IReadOnlyList<PickupSpec>? pickups = null)
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
            trigger.Bind(world.Runtime, armed, cutscene, () => craft, pickups);
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

            var arm = world.Runtime.FindNodes(LandingApproaches.ArmNode, approaches[0]);
            for (float t = 0f; t < 14f; t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                trigger.Tick();
            }
            ctx.Check(arm.Count > 0 && arm[0].Visible,
                $"entering the 100 m pickup sensor runs pickup_timing and opens land_on");
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
        }, pickups: Pickups.Load(missionZrdr));
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
