using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites over the campaign runtime: what a mission's objectives graph does with the
/// shipped choreography, what a mission leaves destroyed, what survives the profile file, and what
/// a later mission of the same chapter starts with.</summary>
internal static class CampaignSuites
{
    // How many persisted objects the first mission destroys. Three is enough to prove the log
    // carries a set rather than one lucky node, and cheap enough to leave the world build dominant.
    private const int TargetCount = 3;

    // The decoded worked mission the headless graph suite drives (docs/formats/objectives.md's
    // vocabulary, walked in full for C2/M01; C1/M02 is the same vocabulary at a size that fits one
    // suite). Its chapter world is never built here: the graph runs against a scripted world.
    private const string GraphChapter = "C1";

    private const string GraphMission = "M02";

    // The one mission with a bespoke intro definition, and the worked case of the whole cutscene
    // decode (docs/formats/anim-definitions/cutscenes.md). The other twelve share `generic_intro`,
    // which authors the same eight codes in the same two places.
    private const string IntroChapter = "C1";

    private const string IntroMission = "M04";

    private const string IntroAnim = "mission_intro_animation";

    // BL-458: the mission whose SECONDARY (OBJECTIVE3, IDENTITY SECONDARY 11) and OBJECTIVE11
    // both gate on DANGER_ZONES_COMPLETED (dzpath1, dzpath4) — the worked case that was
    // unreachable before a campaign session armed its own dzpathN gates.
    private const string DangerZoneChapter = "C3";

    private const string DangerZoneMission = "M01";

    // What the definition's `callback_sequence` authors, and what its RESET_STATE asserts as the
    // gameplay end state. Both lists are the shipped data, not this engine's choice.
    private static readonly int[] MovieCodes = { 20, 2, 11, 14, 913 };

    private static readonly int[] RestoreCodes = { 1, 10, 914, 667 };

    internal static void CampaignPersistence(TestContext ctx)
    {
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        var second = LastMissionOf(missions, ctx.Chapter);
        if (second is not { } later
            || CampaignSequence.PreviousInSameChapter(missions, later.Seq) is not { } earlier)
        {
            throw new SuiteSkippedException(
                $"chapter {ctx.Chapter} holds fewer than two campaign missions");
        }

        int chapter = later.Campaign;
        var profile = CampaignProfileDef.NewProfile("Zachary");
        string profileDir = Path.Combine(ctx.ScratchDir, "campaign-persistence");
        var store = new CampaignProfileStore(profileDir);
        var report = new StringBuilder();
        report.AppendLine($"chapter {ctx.Chapter} ({chapter}) mission {earlier.MissionFolder} -> {later.MissionFolder}");

        var carried = new List<int>();
        int saveOnly = -1;
        ctx.WithWorld(ctx.Chapter, collision: false, earlier.MissionFolder, world =>
        {
            var registry = world.Runtime.Destructibles;
            var persistedNodes = PersistedNodes(registry);
            foreach (int node in persistedNodes.Keys)
            {
                if (carried.Count >= TargetCount)
                {
                    break;
                }

                if (Live(registry, persistedNodes[node]) is { } live && live.MaxHealth > 0f)
                {
                    world.Runtime.DamageAt(persistedNodes[node], live.MaxHealth);
                    carried.Add(node);
                    report.AppendLine($"destroyed persisted node={node} name={persistedNodes[node].Name}");
                }
            }

            saveOnly = DestroySaveOnly(world.Runtime, persistedNodes, report);
            ctx.Check(carried.Count > 0, $"chapter {ctx.Chapter} has PERSIST_LOG destructibles to destroy");

            var captured = CampaignPersistLog.Capture(world.Runtime);
            var capturedNodes = new HashSet<int>();
            foreach (var state in captured)
            {
                capturedNodes.Add(state.Node);
                ctx.Check(state.Destroyed, $"captured state is the destroyed one node={state.Node}");
            }

            foreach (int node in carried)
            {
                ctx.Check(capturedNodes.Contains(node), $"the log carries the destroyed node node={node}");
            }

            ctx.Check(saveOnly < 0 || !capturedNodes.Contains(saveOnly),
                $"a destructible with no PERSIST_LOG def stays out of the log node={saveOnly}");
            // The director commits a capture only on a won mission, which is when the original
            // writes its world-state carrier; the merge below stands in for that commit.
            ctx.Check(CampaignPersistLog.CommitsOn(MissionOutcome.Won)
                && !CampaignPersistLog.CommitsOn(MissionOutcome.Lost),
                $"only a won mission commits its capture to the log");
            profile.PersistLog.Merge(chapter, captured);
            store.Save(profile);
        });

        // A second store instance over the same directory stands in for the process restart the
        // original's log survives; the world below is built from the bootstrap, as every CSVM
        // session is, so anything destroyed in it came from the log and from nothing else.
        var reloaded = new CampaignProfileStore(profileDir).Load("Zachary");
        ctx.Check(reloaded != null, $"the profile round-trips the log through its file");
        ctx.Same(profile.PersistLog.Count, reloaded?.PersistLog.Count ?? -1, $"log entries after the reload");

        ctx.WithWorld(ctx.Chapter, collision: false, later.MissionFolder, world =>
        {
            var registry = world.Runtime.Destructibles;
            var nodes = AllNodes(registry);
            int present = 0;
            foreach (int node in carried)
            {
                if (!nodes.TryGetValue(node, out var anchor) || Live(registry, anchor) is not { } live)
                {
                    continue;
                }

                present++;
                ctx.Check(live.Status == DestructibleRegistry.State.Healthy,
                    $"the bootstrap alone leaves it intact node={node}");
            }

            ctx.Check(present > 0, $"the later mission carries at least one of the destroyed objects");
            int applied = reloaded!.PersistLog.ApplyTo(world.Runtime, chapter);
            report.AppendLine($"applied {applied} of {reloaded.PersistLog.For(chapter).Count} in {later.MissionFolder}");
            ctx.Same(present, applied, $"every carried object present in the later mission is applied");

            foreach (int node in carried)
            {
                if (nodes.TryGetValue(node, out var anchor) && Live(registry, anchor) is { } live)
                {
                    ctx.Check(live.Status == DestructibleRegistry.State.Destroyed,
                        $"it starts the later mission destroyed node={node}");
                }
            }

            if (saveOnly >= 0 && nodes.TryGetValue(saveOnly, out var control)
                && Live(registry, control) is { } controlLive)
            {
                ctx.Check(controlLive.Status == DestructibleRegistry.State.Healthy,
                    $"the save-only destructible starts the later mission intact node={saveOnly}");
            }

            ctx.Same(0, reloaded.PersistLog.For(chapter == 1 ? 2 : 1).Count,
                $"nothing leaks into another chapter's log");
        });

        ctx.WriteArtifact($"test-campaign-persistence-{ctx.Chapter}.txt", report.ToString());
        ctx.Note($"carried {carried.Count} persisted objects across {earlier.MissionFolder} -> {later.MissionFolder}");
    }

    /// <summary>Drives one shipped mission's objectives graph headless against a scripted world:
    /// the wake timings, the chains, the target-list edits, the display rows, and both endings the
    /// script authors. No world is built, so this suite costs nothing but the parse.</summary>
    internal static void CampaignObjectives(TestContext ctx)
    {
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, GraphChapter, GraphMission);
        ctx.RequireData(zrdr, $"{GraphChapter}/{GraphMission} zrdr");
        var script = ObjectiveScript.Load(zrdr);
        var report = new StringBuilder();
        report.AppendLine($"{GraphChapter}/{GraphMission}: {script.Objectives.Count} objectives");
        ctx.Same(50, script.Objectives.Count, $"the shipped {GraphChapter}/{GraphMission} objective count");

        var world = new ScriptedWorld();
        var graph = new ObjectiveGraph(script, world);
        var woke = new List<int>();
        var done = new List<int>();
        graph.Woke += e => woke.Add(e.Number);
        graph.Completed += e => done.Add(e.Number);

        ctx.Same(5, graph.Rows.Count, $"one display row per unique IDENTITY priority");
        ctx.Same(1, graph.Rows[0].Priority, $"the lowest priority sorts first, and is the primary");
        ctx.Same(11, graph.Rows[^1].Priority, $"the secondary's priority is the last row");
        ctx.Check(graph.StateOf(1) == ObjectiveState.Dormant, $"OBJECTIVE1 begins dormant");
        ctx.Check(graph.StateOf(2) == ObjectiveState.Awake, $"OBJECTIVE2's null body begins awake");

        Advance(graph, 3f);
        ctx.Check(done.Contains(2), $"the null block completes as a no-op on its first eligible tick");
        ctx.Check(woke.Contains(1), $"OBJECTIVE1 wakes at its BEGIN_DORMANT 2 s");
        ctx.Check(world.SoundGroups.Contains("snd_NW2Start"), $"its WAKEUP_SOUND_GROUP fired");
        ctx.Check(world.TurretPatterns.Contains("aagun**"), $"its WAKEUP_TURRETS pattern reached the world");
        Advance(graph, 3f);
        ctx.Check(woke.IndexOf(48) > woke.IndexOf(1), $"OBJECTIVE1's completion woke OBJECTIVE48 after it");
        ctx.Check(world.SoundGroups.Contains("music_prebattle_sg"),
            $"the woken cue's COMPLETED_SOUND_GROUP carries the music group D37 reads");

        // The primary: the pickup going inactive is what completes it, and its completion is where
        // this mission's chaining, target edits and net reassignments all land.
        world.Inactive.Add("pickup_objective");
        Advance(graph, 6f);
        ctx.Check(done.Contains(3), $"the primary completes when its INACTIVE node loses its active bit");
        ctx.Same(4, world.AiNets.Count, $"its four SET_AI_NET entries reached the world");
        ctx.Check(graph.IsOtherTarget("ftank01"), $"ADD_OTHER_TARGET added the fuel tank");
        ctx.Check(!graph.IsObjectiveTarget("caboose_polys"), $"REMOVE_OBJECTIVE_TARGET dropped the caboose");
        ctx.Check(graph.Rows[0].Completed, $"the primary's display row is marked");
        ctx.Same(CampaignProgression.PrimaryObjectiveMask, graph.CompletedMask & 1,
            $"bit 0 of the recorded mask is that primary");
        for (int n = 19; n <= 24; n++)
        {
            ctx.Check(!graph.AliveOf(n), $"the primary's KILL list retired the reminder loop OBJECTIVE{n}");
        }

        ctx.Check(done.Contains(4), $"its WAKE list ran OBJECTIVE4, which is conditionless");
        ctx.Check(graph.StateOf(6) != ObjectiveState.Dormant, $"its NAP list moved OBJECTIVE6 out of dormancy");
        Advance(graph, 8f);
        ctx.Check(done.Contains(6), $"the napped objective woke on its own timer and completed");
        report.AppendLine($"win-path run: {done.Count} completions, {woke.Count} wakes, mask 0x{graph.CompletedMask:x}");

        LossFuse(ctx, script, report);
        NapClearsCompletion(ctx);
        ctx.WriteArtifact("test-campaign-objectives.txt", report.ToString());
        ctx.Note($"drove {GraphChapter}/{GraphMission}'s {script.Objectives.Count}-objective graph to both endings");
    }

    /// <summary>The mission-end flow against a built world: a scripted kill drives an
    /// <c>INACTIVEn</c> condition off real node state, the graph's own end is what ends the
    /// mission, and the result reaches the profile through <see cref="CampaignProgression"/> with
    /// the destruction log captured and the return-to-cabin exit raised.</summary>
    internal static void CampaignMissionEnd(TestContext ctx)
    {
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        if (FirstMissionOf(missions, ctx.Chapter) is not { } mission)
        {
            throw new SuiteSkippedException($"chapter {ctx.Chapter} holds no campaign mission");
        }

        var script = ObjectiveScript.Load(
            SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder));
        if (script.Objectives.Count == 0)
        {
            throw new SuiteSkippedException($"{mission.ChapterFolder}/{mission.MissionFolder} authors no objectives");
        }

        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        var report = new StringBuilder();
        report.AppendLine($"{mission.ChapterFolder}/{mission.MissionFolder} seq={mission.Seq}: "
            + $"{script.Objectives.Count} objectives");
        ctx.WithWorld(ctx.Chapter, collision: false, mission.MissionFolder, world =>
        {
            director.Attach(new CampaignDirector.WorldInputs { Runtime = world.Runtime });
            var graph = director.Graph!;
            ctx.Check(graph.Count == script.Objectives.Count, $"every objective is armed");
            ctx.Check(graph.Rows.Count > 0, $"the mission has a player-visible objectives display");

            int completed = DriveOneInactive(ctx, world, script, graph, report);
            ctx.Check(completed > 0, $"a scripted kill drove an INACTIVEn condition off real node state");

            int winner = EndObjective(script);
            ctx.Check(winner > 0, $"the mission authors an INSTANTWIN objective");
            graph.Wake(winner);
            Advance(graph, 2f);
            ctx.Check(director.ReturnToCabin, $"the mission end raised the return-to-cabin exit");
            var result = director.Result!.Value;
            ctx.Check(result.Outcome == MissionOutcome.Won, $"the outcome is the graph's own");
            ctx.Same(graph.CompletedMask, result.Attempt.CompletedMask, $"the recorded mask is the graph's rows");
            ctx.Check(CampaignProgression.ResultOf(profile, mission.Seq) != null,
                $"the attempt reached the profile's mission record");
            bool primary = (result.Attempt.CompletedMask & CampaignProgression.PrimaryObjectiveMask) != 0;
            ctx.Same(primary ? mission.Seq + 1 : 0, profile.MissionsCompleted,
                $"the position advances only on a completed primary (primary={primary})");
            report.AppendLine($"ended {result.Outcome}, mask 0x{result.Attempt.CompletedMask:x}, "
                + $"{result.Attempt.TimeMs} ms, persist log {profile.PersistLog.Count}");
        });

        ctx.WriteArtifact($"test-campaign-mission-end-{ctx.Chapter}.txt", report.ToString());
        ctx.Note($"flew {mission.ChapterFolder}/{mission.MissionFolder} to a graph-derived end");
    }

    /// <summary>BL-458: a campaign mission's own <c>dzpathN</c> gates, resolved against real
    /// world geometry, drive the exact objectives <c>objectives.zrd</c> gates on them. Proved
    /// over C3/M01's shipped SECONDARY (OBJECTIVE3, <c>dzpath1</c>) and OBJECTIVE11
    /// (<c>dzpath4</c>): both gate-crossing tests and completion through the director's real
    /// <c>NotifyDangerZoneCompleted</c> path.</summary>
    internal static void CampaignDangerZoneObjectives(TestContext ctx)
    {
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        if (MissionAt(missions, DangerZoneChapter, DangerZoneMission) is not { } mission)
        {
            throw new SuiteSkippedException($"{DangerZoneChapter}/{DangerZoneMission} is not in cm_sequence");
        }

        string missionZrdrPath = SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder);
        var script = ObjectiveScript.Load(missionZrdrPath);
        var secondary = script.Objectives.Find(d => d.DangerZones.Contains("dzpath1"));
        var worker = script.Objectives.Find(d => d.DangerZones.Contains("dzpath4"));
        ctx.Check(secondary?.Identity?.Class == ObjectiveClass.Secondary,
            $"OBJECTIVE{secondary?.Number} carries the mission's SECONDARY IDENTITY and gates on dzpath1");
        ctx.Check(worker != null, $"a second objective gates on dzpath4");
        if (secondary == null || worker == null)
        {
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"{DangerZoneChapter}/{DangerZoneMission}: SECONDARY OBJECTIVE{secondary.Number} "
            + $"on dzpath1, OBJECTIVE{worker.Number} on dzpath4");

        ctx.WithWorld(DangerZoneChapter, collision: false, DangerZoneMission, world =>
        {
            var zones = CampaignDangerZones.Load(script, world.Gamez, missionZrdrPath);
            ctx.Check(zones != null, $"the chapter world carries real dzpath1/dzpath4 gate geometry");
            if (zones == null)
            {
                return;
            }
            ctx.Same(2, zones.Count,
                $"exactly the two dzpathN names objectives.zrd references are armed");

            var completed = new List<string>();
            foreach (var name in new[] { "dzpath1", "dzpath4" })
            {
                // A fresh tracker per zone: Update tests every armed gate on every call, so one
                // shared instance's long "prime" jump between two zones' probe points can cross
                // the OTHER zone's gate plane too — a test-construction risk, not an engine one.
                var solo = CampaignDangerZones.Load(script, world.Gamez, missionZrdrPath)!;
                ctx.Check(solo.TryGateProbe(name, out var gc, out var gn, out var rc, out var rn),
                    $"'{name}' resolved a green/red gate pair");
                var half = new List<string>();
                solo.Update(gc - gn * 5f, half.Add);
                solo.Update(gc + gn * 5f, half.Add);
                // Some authored pairs sit only metres apart (a "thin slit" aperture), so crossing
                // green can carry this probe across red's plane too — completion, not a false one.
                solo.Update(rc - rn * 5f, half.Add);
                solo.Update(rc + rn * 5f, half.Add);
                ctx.Check(half.Contains(name), $"'{name}' completed once both authored gates were crossed");
                completed.AddRange(half);
            }
            report.AppendLine($"gate crossing completed: {string.Join(", ", completed)}");

            var profile = CampaignProfileDef.NewProfile("Zachary");
            var director = CampaignDirector.Create(script, mission, profile, null, missionZrdrPath);
            director.Attach(new CampaignDirector.WorldInputs { Runtime = world.Runtime, Gamez = world.Gamez });
            ctx.Same(zones.Count, director.ArmedDangerZones,
                $"Attach arms the same gate count off a real Gamez, the wiring BL-458 was missing");

            var graph = director.Graph!;
            graph.Wake(secondary.Number);
            graph.Wake(worker.Number);
            StepDirector(director, 0.1f);
            ctx.Check(!graph.CompletedOf(secondary.Number), $"the SECONDARY has not completed before any zone notify");

            // ScanForCompletion resolves one objective per tick, round robin (ObjectiveGraph.cs):
            // enough steps to cycle past every one of the mission's 39 armed objectives, not one.
            director.NotifyDangerZoneCompleted("dzpath1");
            StepDirector(director, 5f);
            ctx.Check(graph.CompletedOf(secondary.Number),
                $"OBJECTIVE{secondary.Number} (the SECONDARY) completes off the notify path BL-458 wired");

            director.NotifyDangerZoneCompleted("dzpath4");
            StepDirector(director, 5f);
            ctx.Check(graph.CompletedOf(worker.Number), $"OBJECTIVE{worker.Number} completes the same way");
            report.AppendLine($"OBJECTIVE{secondary.Number} and OBJECTIVE{worker.Number} completed via NotifyDangerZoneCompleted");
        });

        ctx.WriteArtifact("test-campaign-danger-zones.txt", report.ToString());
        ctx.Note($"{DangerZoneChapter}/{DangerZoneMission}'s SECONDARY completed through its own dzpathN gates");
    }

    /// <summary>The cutscene host over the shipped intro definition: the codes the definition
    /// raises reach it through the runtime's own dispatch, the world and the objectives update
    /// stop while it holds them, and the handoff puts every piece of session state back.</summary>
    internal static void CampaignCutscene(TestContext ctx)
    {
        var host = new CutsceneController();
        var held = new List<bool>();
        host.WorldHeld = h => held.Add(h);
        ctx.Host.AddChild(host);
        ctx.WithWorld(IntroChapter, collision: false, IntroMission, world =>
        {
            var stage = new Node3D { Name = "CutsceneStage" };
            var runtime = new AnimRuntime { AutoStart = false, ManualAdvance = true, SoundHandledElsewhere = true };
            runtime.CallbackHost = host.Host;
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(IntroAnim));
                host.BindWorld(runtime);
                ctx.Check(!host.Playing, $"nothing owns the session until a definition raises a code");
                runtime.Play(IntroAnim);

                ctx.Check(host.Playing && host.Anim == IntroAnim,
                    $"'{IntroAnim}' took the session through the runtime's own CALLBACK dispatch");
                ctx.Same(20, host.Codes.Count > 0 ? host.Codes[0] : 0,
                    $"the movie state opens by holding the world");
                foreach (int code in MovieCodes)
                {
                    ctx.Check(host.Codes.Contains(code), $"the definition's authored callback {code} was hosted");
                }

                ctx.Check(host.HoldsWorld && host.Presenting && host.OutOfFlight && host.AiParked,
                    $"the world is held, the chrome is off, the player is out of flight and the AI is parked");
                ctx.Check(held.Count == 1 && held[0], $"the world hold reached the session once");
                ctx.Check(!host.Host(15, IntroAnim) && !host.Host(16, IntroAnim),
                    $"the vehicle-death codes are declined, so they keep the seams they had");

                // The definition still running is not a handoff; its end is.
                host.Tick();
                ctx.Check(host.Playing, $"a tick while the definition runs holds the cutscene");
                runtime.Stop(IntroAnim);
                host.Tick();
                ctx.Check(!host.Playing && !host.HoldsWorld && !host.Presenting && !host.OutOfFlight
                          && !host.AiParked && !host.CamParamsFree,
                    $"the definition ending hands off: every piece of cutscene state is back");
                ctx.Check(held.Count == 2 && !held[1], $"and the world hold was released");
                foreach (int code in RestoreCodes)
                {
                    ctx.Check(host.Codes.Contains(code),
                        $"the handoff raised the gameplay state the definition's RESET_STATE asserts ({code})");
                }
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });

        CutsceneHoldsObjectives(ctx);
        ctx.Note($"hosted {IntroChapter}/{IntroMission}'s '{IntroAnim}' from its first code to the handoff");
    }

    /// <summary>The bars are data: the shared <c>letterbox</c> definition switches the node on and
    /// copies the cutscene camera's whole frame onto it every tick, so they hold their place in the
    /// frame through any camera path.</summary>
    internal static void CutsceneLetterbox(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var gamezBars = world.Gamez.FindByName(CutsceneController.BarsNode);
            ctx.Check(gamezBars != null && !gamezBars.Active,
                $"the chapter ships a {CutsceneController.BarsNode} node, switched off as its definition's base state");

            var stage = new Node3D { Name = "LetterboxStage" };
            var camera = new Node3D { Name = CutsceneController.CameraNode };
            var bars = new Node3D { Name = CutsceneController.BarsNode };
            stage.AddChild(camera);
            stage.AddChild(bars);
            var runtime = new AnimRuntime { AutoStart = false, ManualAdvance = true, SoundHandledElsewhere = true };
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(CutsceneController.BarsNode));
                ctx.Check(!bars.Visible, $"the bind applies the definition's INACTIVE base state");

                var pose = new Transform3D(
                    Basis.FromEuler(new Vector3(0.21f, 0.73f, 0f)), new Vector3(120f, 240f, -360f));
                camera.GlobalTransform = pose;
                runtime.Play(CutsceneController.BarsNode);
                ctx.Check(bars.Visible, $"calling the definition switches the bars on outright, with no reveal");
                ctx.Check(bars.GlobalPosition.IsEqualApprox(pose.Origin),
                    $"and pins them to the cutscene camera's position");
                ctx.Check(bars.GlobalBasis.Z.IsEqualApprox(pose.Basis.Z),
                    $"including its facing, which is what holds the bars square to the frame");

                // The re-assert loop is what keeps them there while the camera flies its splines.
                camera.GlobalTransform = new Transform3D(pose.Basis, new Vector3(900f, 30f, 40f));
                runtime.Advance(1f / 60f);
                ctx.Check(bars.GlobalPosition.IsEqualApprox(camera.GlobalPosition),
                    $"the LOOP re-assert follows the camera on the next tick");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });

        ctx.Note($"the {ctx.Chapter} letterbox card tracks the cutscene camera by transform copy");
    }

    // The objectives half of callback 20: a held director advances no dormancy timer, which is what
    // stops a mission's reminder fuses burning down behind the movie.
    private static void CutsceneHoldsObjectives(TestContext ctx)
    {
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        if (FirstMissionOf(missions, IntroChapter) is not { } mission)
        {
            return;
        }

        var script = ObjectiveScript.Load(
            SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder));
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null);
        director.Attach(new CampaignDirector.WorldInputs());
        director.HoldForCutscene(true);
        for (int i = 0; i < 600; i++)
        {
            director.Step(1f);
        }

        ctx.Same(0, (long)director.Graph!.Elapsed, $"a held objectives graph does not advance under the movie");
        director.HoldForCutscene(false);
        director.Step(1f);
        ctx.Check(director.Graph!.Elapsed > 0f, $"and runs again once the cutscene hands off");
    }

    // Runs the loss fuse the mission authors: nothing is satisfied, OBJECTIVE19 wakes at its 300 s
    // and naps the INSTANTLOSS objective 15 s later, which is what loses the mission.
    private static void LossFuse(TestContext ctx, ObjectiveScript script, StringBuilder report)
    {
        var graph = new ObjectiveGraph(script, new ScriptedWorld());
        Advance(graph, 305f);
        ctx.Check(graph.CompletedOf(19), $"the last reminder completes at its BEGIN_DORMANT 300 s");
        ctx.Check(graph.StateOf(24) == ObjectiveState.Napping, $"its NAP put the INSTANTLOSS objective on a timer");
        ctx.Check(!graph.Ended, $"the mission has not ended while that nap runs");
        Advance(graph, 20f);
        ctx.Check(graph.Outcome == MissionOutcome.Lost, $"the INSTANTLOSS objective ends the mission lost");
        report.AppendLine($"loss-fuse run: lost at {graph.Elapsed:0.0} s");
    }

    // NAP_OBJECTIVE_WHEN_I_COMPLETE clearing a completed flag is engine surface no shipped mission
    // cycles into, so it is driven from a script written in the same vocabulary.
    private static void NapClearsCompletion(TestContext ctx)
    {
        var script = ObjectiveScript.Parse(new List<object?>
        {
            new List<object?>
            {
                "OBJECTIVE1", new List<object?>
                {
                    "BEGIN_DORMANT", new List<object?> { 1.0f },
                    "NAP_OBJECTIVE_WHEN_I_COMPLETE", new List<object?> { 2.0f, 1.0f },
                },
                "OBJECTIVE2", null,
            },
        });
        var graph = new ObjectiveGraph(script, new ScriptedWorld());
        Advance(graph, 0.5f);
        ctx.Check(graph.CompletedOf(2), $"the conditionless objective completes first");
        Advance(graph, 0.7f);
        ctx.Check(!graph.CompletedOf(2), $"the nap cleared its completed flag");
        ctx.Check(graph.StateOf(2) == ObjectiveState.Napping, $"and left it napping");
        Advance(graph, 2f);
        ctx.Check(graph.CompletedOf(2), $"it ran again, which no plain wake could have done");
    }

    // Destroys (or deactivates) every node of one objective's INACTIVE paths, then ticks until it
    // completes. Two passes: an objective whose nodes are all real destructibles first, so the
    // condition is driven by a WEAPON kill through DamageAt rather than by a bare deactivation.
    private static int DriveOneInactive(
        TestContext ctx, TestWorld world, ObjectiveScript script, ObjectiveGraph graph, StringBuilder report)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            int found = DriveInactivePass(ctx, world, script, graph, report, pass == 0);
            if (found > 0)
            {
                return found;
            }
        }

        return 0;
    }

    private static int DriveInactivePass(
        TestContext ctx, TestWorld world, ObjectiveScript script, ObjectiveGraph graph,
        StringBuilder report, bool destructiblesOnly)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Inactive.Count == 0 || def.InstantLoss)
            {
                continue;
            }

            var nodes = new List<Node3D>();
            foreach (var path in def.Inactive)
            {
                if (Resolve(world, path) is { } node)
                {
                    nodes.Add(node);
                }
            }

            if (nodes.Count == 0 || nodes.Count < (def.InactiveCount ?? def.Inactive.Count)
                || (destructiblesOnly && !AllDestructible(world, nodes)))
            {
                continue;
            }

            int killed = 0;
            foreach (var node in nodes)
            {
                if (world.Runtime.Destructibles.Resolve(node) is { MaxHealth: > 0f } live)
                {
                    world.Runtime.DamageAt(node, live.MaxHealth);
                    killed++;
                }
                else
                {
                    AnimRuntime.SetSubtreeActive(node, false);
                }
            }

            graph.Wake(def.Number);
            Advance(graph, 10f);
            report.AppendLine($"OBJECTIVE{def.Number}: {nodes.Count} node(s), {killed} by weapon damage, "
                + $"completed={graph.CompletedOf(def.Number)}");
            ctx.Check(graph.CompletedOf(def.Number),
                $"OBJECTIVE{def.Number} completed once its nodes lost their active bit");
            return nodes.Count;
        }

        return 0;
    }

    private static bool AllDestructible(TestWorld world, List<Node3D> nodes)
    {
        foreach (var node in nodes)
        {
            if (world.Runtime.Destructibles.Resolve(node) is not { MaxHealth: > 0f })
            {
                return false;
            }
        }

        return true;
    }

    private static Node3D? Resolve(TestWorld world, IReadOnlyList<string> path)
    {
        Node3D? node = null;
        foreach (var name in path)
        {
            var found = world.Runtime.FindNodes(name, node);
            if (found.Count == 0)
            {
                return null;
            }

            node = found[0];
        }

        return node;
    }

    private static int EndObjective(ObjectiveScript script)
    {
        foreach (var def in script.Objectives)
        {
            if (def.InstantWin)
            {
                return def.Number;
            }
        }

        return 0;
    }

    private static void Advance(ObjectiveGraph graph, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.1f)
        {
            graph.Step(0.1f);
        }
    }

    // The director's own Step, not the graph's: it also runs the danger-zone tracker and the
    // scripted-path/music phases, the shape a real session drives.
    private static void StepDirector(CampaignDirector director, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.1f)
        {
            director.Step(0.1f);
        }
    }

    private static CampaignMission? FirstMissionOf(
        IReadOnlyList<CampaignMission> missions, string chapter)
    {
        CampaignMission? found = null;
        foreach (var m in missions)
        {
            if (m.ChapterFolder.Equals(chapter, System.StringComparison.OrdinalIgnoreCase)
                && (found == null || m.Seq < found.Value.Seq))
            {
                found = m;
            }
        }

        return found;
    }

    // The one mission at an exact (chapter, mission-folder) address, for a suite that names a
    // specific worked mission rather than "the chapter's first/last".
    private static CampaignMission? MissionAt(
        IReadOnlyList<CampaignMission> missions, string chapter, string missionFolder)
    {
        foreach (var m in missions)
        {
            if (m.ChapterFolder.Equals(chapter, System.StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(missionFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }
        }

        return null;
    }

    // The last campaign mission stored in a chapter's world folder. The suite needs two missions of
    // one folder, which four of the eight folders have.
    private static CampaignMission? LastMissionOf(
        IReadOnlyList<CampaignMission> missions, string chapter)
    {
        CampaignMission? found = null;
        foreach (var m in missions)
        {
            if (m.ChapterFolder.Equals(chapter, System.StringComparison.OrdinalIgnoreCase)
                && (found == null || m.Seq > found.Value.Seq))
            {
                found = m;
            }
        }

        return found;
    }

    // Node index -> anchor, for every node a PERSIST_LOG def binds.
    private static Dictionary<int, Node3D> PersistedNodes(DestructibleRegistry registry)
    {
        var nodes = new Dictionary<int, Node3D>();
        foreach (var inst in registry.All)
        {
            int index = NodeIndex(inst.Anchor);
            if (inst.Def.PersistLog && index >= 0)
            {
                nodes[index] = inst.Anchor;
            }
        }

        return nodes;
    }

    private static Dictionary<int, Node3D> AllNodes(DestructibleRegistry registry)
    {
        var nodes = new Dictionary<int, Node3D>();
        foreach (var inst in registry.All)
        {
            int index = NodeIndex(inst.Anchor);
            if (index >= 0)
            {
                nodes[index] = inst.Anchor;
            }
        }

        return nodes;
    }

    // The control: a destructible no PERSIST_LOG def binds, destroyed in the same mission. The
    // original's save-only defs are the transient layer, and must not cross a mission boundary.
    private static int DestroySaveOnly(
        AnimRuntime runtime, Dictionary<int, Node3D> persisted, StringBuilder report)
    {
        foreach (var inst in runtime.Destructibles.All)
        {
            int index = NodeIndex(inst.Anchor);
            if (index < 0 || persisted.ContainsKey(index)
                || Live(runtime.Destructibles, inst.Anchor) is not { } live
                || live.Status != DestructibleRegistry.State.Healthy || live.MaxHealth <= 0f)
            {
                continue;
            }

            runtime.DamageAt(inst.Anchor, live.MaxHealth);
            report.AppendLine($"destroyed save-only node={index} name={inst.Anchor.Name}");
            return index;
        }

        return -1;
    }

    private static DestructibleRegistry.Instance? Live(DestructibleRegistry registry, Node3D anchor) =>
        registry.Resolve(anchor);

    private static int NodeIndex(Node3D node) =>
        node.HasMeta(AnimRuntime.IndexMeta) ? (int)node.GetMeta(AnimRuntime.IndexMeta) : -1;

    // The graph's world seam with no world behind it: node activity and anim state answer what the
    // suite sets, and every world-touching action is recorded rather than performed. The two
    // families a session cannot answer at all report null here, exactly as the live adapter does.
    private sealed class ScriptedWorld : IObjectiveWorld
    {
        public HashSet<string> Inactive { get; } = new(System.StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> AnimStates { get; } = new(System.StringComparer.OrdinalIgnoreCase);

        public List<string> SoundGroups { get; } = new();

        public List<string> TurretPatterns { get; } = new();

        public List<string> AiNets { get; } = new();

        public List<string> Generators { get; } = new();

        public bool? NodeInactive(IReadOnlyList<string> path) => Inactive.Contains(path[^1]);

        public int AnimState(string anim) => AnimStates.TryGetValue(anim, out int state) ? state : 0;

        public int? GroupLiveCount(int group, string? generator) => null;

        public bool? TravelersMet(TravelersSpec spec) => null;

        public void WakeupEnemies(IReadOnlyList<string> names)
        {
        }

        public void WakeupTurrets(IReadOnlyList<string> patterns) => TurretPatterns.AddRange(patterns);

        public void WakeupZepTurrets(IReadOnlyList<string> nodes) => TurretPatterns.AddRange(nodes);

        public void WakeupGenerator(string name, int count) => Generators.Add(name);

        public void WakeAnim(string anim, string? node)
        {
        }

        public void PlaySoundGroup(string group) => SoundGroups.Add(group);

        public void StopQueuedSounds(IReadOnlyList<string> names)
        {
        }

        public void WarpVehicle(string vehicle, IReadOnlyList<WarpPoint> points)
        {
        }

        public void SetAiTeam(IReadOnlyList<(string Name, int Team)> entries)
        {
        }

        public void SetAiNet(IReadOnlyList<(string Name, string Net)> entries)
        {
            foreach (var entry in entries)
            {
                AiNets.Add(entry.Net);
            }
        }

        public void SetAiAttackRadius(IReadOnlyList<(string Name, float Radius)> entries)
        {
        }

        public void CompletedZepcannons(IReadOnlyList<(string Zeppelin, int Flag)> entries)
        {
        }

        public void CompletedStoppoint(IReadOnlyList<(string Net, int Stop, int Flag)> entries)
        {
        }

        public void StartTaxi(IReadOnlyList<string> names)
        {
        }
    }
}
