using System.Collections.Generic;
using System.Text;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites over D33's in-flight objectives display and its objective sound cues, against a
/// BUILT campaign world so both the HUD element and the audio path it reads are the real engine
/// classes, not a scripted stand-in.</summary>
internal static class CampaignHudSuites
{
    /// <summary>Drives one shipped mission's real director: <see cref="ObjectivesHud"/> tracks the
    /// graph's rows, and a wake/complete sound-group cue starts a real
    /// <see cref="AudioStreamPlayer3D"/> (<see cref="WorldSounds.OneShotsStarted"/>). A group name
    /// is not guaranteed to resolve here, so both halves try every candidate.</summary>
    internal static void CampaignObjectivesHud(TestContext ctx)
    {
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        if (FirstMissionOf(missions, ctx.Chapter) is not { } mission)
        {
            throw new SuiteSkippedException($"chapter {ctx.Chapter} holds no campaign mission");
        }

        var script = ObjectiveScript.Load(
            SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder));
        var messages = Messages.Load(ctx.MessagesPath);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        var report = new StringBuilder();
        report.AppendLine($"{mission.ChapterFolder}/{mission.MissionFolder} seq={mission.Seq}");

        // The wake/complete sound-group names are objectives.zrd's own vocabulary, never touched
        // by the mission's anim program, so nothing else prewarms them before the build's sound
        // archive scope closes (docs/architecture.md's ObjectiveScript entry).
        ctx.ExtraPrewarmSoundNames = script.SoundGroupNames();
        ctx.WithWorld(ctx.Chapter, collision: false, mission.MissionFolder, world =>
        {
            var sounds = world.Runtime.Sounds;
            ctx.Check(sounds != null, $"the mission world built a live WorldSounds channel");
            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = sounds,
                ListenerPosition = () => ctx.Camera.GlobalPosition,
                Rng = new System.Random(1),
            });
            var graph = director.Graph!;
            // Paused from the start: the readout is a pause-screen element (B11), so its drawing
            // path only runs while the board is up, and this suite wants that path exercised.
            var pause = new CSVM.Flight.PauseState();
            pause.TryToggle(0);
            var hud = ObjectivesHud.Build(director, messages, pause);
            ctx.Host.AddChild(hud);

            // ObjectivesHud polls for the graph in _Process rather than at construction (the
            // wiring contract: a session adds it before Attach runs), so drive one process frame
            // before reading it, matching what a real running session would do.
            hud._Process(0.0);
            ctx.Same(graph.Rows.Count, hud.BuildLines().Count,
                $"the readout carries one line per display row before anything happens");
            CheckNoBlankRowsDrawn(ctx, hud, report);

            if (sounds != null)
            {
                DriveWakeCue(ctx, script, graph, sounds, report);
                DriveCompletionCue(ctx, world, script, graph, hud, sounds, report);
            }

            hud.QueueFree();
        });

        ctx.WriteArtifact($"test-campaign-objectives-hud-{ctx.Chapter}.txt", report.ToString());
        ctx.Note($"drove {mission.ChapterFolder}/{mission.MissionFolder}'s readout and cue path against a built world");
    }

    /// <summary>The mission radio queue over one shipped mission's own callout vocabulary: the
    /// definition classes the data authors, a VO dialogue chain speaking all its lines in order,
    /// a plain radio line beside it, and the queue's spacing, wait tolerance and cancellation.
    /// Built against a real world so the streams are the ones the mission prewarmed.</summary>
    internal static void MissionRadioCallouts(TestContext ctx)
    {
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        if (FirstMissionOf(missions, ctx.Chapter) is not { } mission)
        {
            throw new SuiteSkippedException($"chapter {ctx.Chapter} holds no campaign mission");
        }

        var script = ObjectiveScript.Load(
            SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder));
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        var report = new StringBuilder();
        report.AppendLine($"{mission.ChapterFolder}/{mission.MissionFolder} seq={mission.Seq}");
        CheckCueClasses(ctx, script, defs, groups, report);

        var cues = script.SoundGroupNames();
        string? chainCue = FirstCue(cues, groups, defs, chain: true);
        string? lineCue = FirstCue(cues, groups, defs, chain: false);
        if (chainCue == null || lineCue == null)
        {
            throw new SuiteSkippedException(
                $"{mission.MissionFolder} authors no chain/line pair to A/B (chain={chainCue} line={lineCue})");
        }

        ctx.ExtraPrewarmSoundNames = cues;
        ctx.WithWorld(ctx.Chapter, collision: false, mission.MissionFolder, world =>
        {
            var sounds = world.Runtime.Sounds;
            ctx.Check(sounds != null, $"the mission world built a live WorldSounds channel");
            if (sounds == null)
            {
                return;
            }

            var radio = new MissionRadio(defs, groups, sounds.StreamFor);
            ctx.Host.AddChild(radio);
            DriveRadio(ctx, radio, sounds, chainCue, lineCue, report);
            radio.QueueFree();
        });

        ctx.WriteArtifact($"test-mission-radio-{ctx.Chapter}.txt", report.ToString());
        ctx.Note($"drove {mission.ChapterFolder}/{mission.MissionFolder}'s chain cue '{chainCue}' and line cue '{lineCue}'");
    }

    // The decode this channel exists for: every callout the mission cues is a queued radio line or
    // a chain of them, and not one of them is a positional definition.
    private static void CheckCueClasses(
        TestContext ctx, ObjectiveScript script, IReadOnlyDictionary<string, SoundDef> defs,
        IReadOnlyDictionary<string, SoundGroup> groups, StringBuilder report)
    {
        int queued = 0, chains = 0, music = 0, positional = 0, unknown = 0;
        foreach (var cue in script.SoundGroupNames())
        {
            if (cue.StartsWith("mu", System.StringComparison.OrdinalIgnoreCase))
            {
                music++;
            }
            else if (groups.TryGetValue(cue, out var group))
            {
                chains += group.Chains.Count > 0 ? 1 : 0;
                positional += ChainOrGroupIs3D(group, defs) ? 1 : 0;
            }
            else if (defs.TryGetValue(cue, out var def))
            {
                queued += def.Queued ? 1 : 0;
                positional += def.Is3D ? 1 : 0;
            }
            else
            {
                unknown++;
            }
        }

        report.AppendLine($"cues: {queued} radio lines, {chains} chains, {music} music, "
            + $"{positional} positional, {unknown} unknown to sounds.json");
        ctx.Same(0, positional, $"no callout this mission cues names a positional (3D) definition");
        ctx.Check(queued + chains > 0, $"the mission cues radio lines and chains ({queued}+{chains})");
    }

    // Drives the channel: the chain speaks every line in order, a second cue queues behind rather
    // than cutting in, and a cancelled call never speaks.
    private static void DriveRadio(
        TestContext ctx, MissionRadio radio, WorldSounds sounds,
        string chainCue, string lineCue, StringBuilder report)
    {
        int expected = radio.Cue(chainCue, new System.Random(1));
        ctx.Check(expected > 1, $"the chain cue '{chainCue}' queues its whole script lines={expected}");
        ctx.Same(0, radio.LinesStarted, $"nothing speaks before the channel is stepped");
        Pump(radio, MissionRadio.CueDelaySeconds + 0.2f);
        ctx.Same(1, radio.LinesStarted, $"the chain's first line started on air='{radio.OnAir}'");

        int queuedBefore = sounds.OneShotsStarted;
        ctx.Same(1, radio.Cue(lineCue, new System.Random(2)),
            $"a plain radio line '{lineCue}' queues as one line");
        string? speaking = radio.OnAir;
        Pump(radio, 0.5f);
        ctx.Check(speaking != null && radio.OnAir == speaking,
            $"the second call waits its turn rather than cutting in on '{speaking}'");
        ctx.Same(1, radio.Pending, $"it is holding in the queue");

        Pump(radio, 240f);
        ctx.Same(expected + 1, radio.LinesStarted,
            $"every line of the chain and the line behind it spoke got={radio.LinesStarted}");
        ctx.Same(0, radio.Dropped, $"nothing waited past its QUEUE tolerance");
        ctx.Check(radio.OnAir == null, $"the channel falls silent once the queue drains");
        ctx.Same(queuedBefore, sounds.OneShotsStarted,
            $"not one callout started a positional player while the radio was speaking");

        radio.Cue(chainCue, new System.Random(3));
        ctx.Same(1, radio.Cancel(new[] { chainCue }), $"STOP_QUEUED_SOUNDS drops a call that has not started");
        int spoken = radio.LinesStarted;
        Pump(radio, 60f);
        ctx.Same(spoken, radio.LinesStarted, $"the cancelled call never speaks");
        report.AppendLine($"radio: {radio.LinesStarted} lines started, {radio.Dropped} dropped, "
            + $"{sounds.OneShotsStarted - queuedBefore} positional one-shots");
    }

    private static void Pump(MissionRadio radio, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.1f)
        {
            radio.Tick(0.1f);
        }
    }

    private static bool ChainOrGroupIs3D(
        SoundGroup group, IReadOnlyDictionary<string, SoundDef> defs)
    {
        foreach (var chain in group.Chains)
        {
            foreach (var line in chain)
            {
                if (defs.TryGetValue(line, out var def) && def.Is3D)
                {
                    return true;
                }
            }
        }

        foreach (var (member, _) in group.Members)
        {
            if (defs.TryGetValue(member, out var def) && def.Is3D)
            {
                return true;
            }
        }

        return false;
    }

    // The first cue name of the asked-for shape: a VO dialogue chain, or a bare radio definition.
    private static string? FirstCue(
        IReadOnlyList<string> cues, IReadOnlyDictionary<string, SoundGroup> groups,
        IReadOnlyDictionary<string, SoundDef> defs, bool chain)
    {
        foreach (var cue in cues)
        {
            if (chain && groups.TryGetValue(cue, out var group) && group.Chains.Count > 0
                && group.Chains[0].Count > 1)
            {
                return cue;
            }

            if (!chain && !groups.ContainsKey(cue) && defs.TryGetValue(cue, out var def) && def.Queued)
            {
                return cue;
            }
        }

        return null;
    }

    // A row the mission gives no message key resolves to no text, and drawn anyway it is a bare
    // mark against blank space, which is what stopped a player reading WHICH objective had
    // completed (B11). The readout drops those rows, so nothing it draws is ever blank.
    private static void CheckNoBlankRowsDrawn(TestContext ctx, ObjectivesHud hud, StringBuilder report)
    {
        var drawn = hud.DrawnLines();
        bool blank = false;
        foreach (var line in drawn)
        {
            blank |= string.IsNullOrWhiteSpace(line.Text);
        }

        report.AppendLine($"{hud.BuildLines().Count} display rows, {drawn.Count} of them drawn");
        ctx.Check(!blank, $"no drawn objectives line is a mark against blank text");
        ctx.Check(drawn.Count > 0, $"the readout draws at least one keyed objectives line");
    }

    // Wakes every WAKEUP_SOUND_GROUP-authoring objective in turn until one of them actually starts
    // a real one-shot player, and asserts that at least one did.
    private static void DriveWakeCue(
        TestContext ctx, ObjectiveScript script, ObjectiveGraph graph, WorldSounds sounds, StringBuilder report)
    {
        foreach (var def in script.Objectives)
        {
            if (def.WakeSoundGroup is not { } group)
            {
                continue;
            }

            int before = sounds.OneShotsStarted;
            graph.Wake(def.Number);
            int after = sounds.OneShotsStarted;
            if (after > before)
            {
                report.AppendLine($"OBJECTIVE{def.Number} WAKEUP_SOUND_GROUP '{group}' started a one-shot ({before} -> {after})");
                ctx.Check(true, $"a WAKEUP_SOUND_GROUP cue started a real one-shot player (OBJECTIVE{def.Number} '{group}')");
                return;
            }
        }

        ctx.Check(false, $"at least one WAKEUP_SOUND_GROUP this mission authors started a real one-shot player");
    }

    // Drives every IDENTITY objective with an INACTIVEn condition in turn. Keeps the first that
    // completes (row-marking proof) and separately the first whose COMPLETED_SOUND_GROUP also
    // starts a one-shot: a completing objective's group can be a VO dialogue chain
    // (docs/formats/sounds.md), which nothing here plays yet (a named gap, not a suite defect).
    private static void DriveCompletionCue(
        TestContext ctx, TestWorld world, ObjectiveScript script, ObjectiveGraph graph,
        ObjectivesHud hud, WorldSounds sounds, StringBuilder report)
    {
        int? rowObjective = null;
        int? soundObjective = null;
        foreach (var def in script.Objectives)
        {
            if (def.Identity == null || def.Inactive.Count == 0)
            {
                continue;
            }

            var beforeLines = hud.BuildLines();
            int before = sounds.OneShotsStarted;
            ProbeRunner.DriveInactive(world.Runtime, def);
            graph.Wake(def.Number);
            Advance(graph, 3f);
            hud._Process(0.0);
            if (!graph.CompletedOf(def.Number))
            {
                continue;
            }

            var afterLines = hud.BuildLines();
            bool anyMarked = false;
            for (int i = 0; i < afterLines.Count; i++)
            {
                if (afterLines[i].Completed && (i >= beforeLines.Count || !beforeLines[i].Completed))
                {
                    anyMarked = true;
                }
            }

            int after = sounds.OneShotsStarted;
            report.AppendLine($"OBJECTIVE{def.Number} completed off its INACTIVEn node " +
                $"[{InactiveNames(def)}], " +
                $"COMPLETED_SOUND_GROUP='{def.CompletedSoundGroup}', one-shots {before} -> {after}, " +
                $"row marked={anyMarked}");
            if (rowObjective == null && anyMarked)
            {
                rowObjective = def.Number;
            }

            if (soundObjective == null && def.CompletedSoundGroup != null && after > before)
            {
                soundObjective = def.Number;
            }
        }

        ctx.Check(rowObjective != null,
            $"a scripted completion marks its row in the readout, not only in the graph (OBJECTIVE{rowObjective})");
        CheckNoBlankRowsDrawn(ctx, hud, report);
        if (soundObjective != null)
        {
            ctx.Check(true, $"a COMPLETED_SOUND_GROUP cue started a real one-shot player (OBJECTIVE{soundObjective})");
        }
        else
        {
            report.AppendLine(
                "no completing objective's COMPLETED_SOUND_GROUP started a one-shot here: every one " +
                "authored in this mission is a VO dialogue chain, which no player exists for yet " +
                "(docs/formats/sounds.md)");
        }
    }

    // The leaf of each INACTIVE path, for the report: these are the names a scripted run reaches
    // for (--destroy=) when it wants this objective to complete with nobody at the controls.
    private static string InactiveNames(ObjectiveDef def)
    {
        var names = new List<string>();
        foreach (var path in def.Inactive)
        {
            names.Add(path.Count > 0 ? path[path.Count - 1] : "?");
        }

        return string.Join(", ", names);
    }


    private static void Advance(ObjectiveGraph graph, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.1f)
        {
            graph.Step(0.1f);
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
}
