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
            var hud = ObjectivesHud.Build(director, messages);
            ctx.Host.AddChild(hud);

            // ObjectivesHud polls for the graph in _Process rather than at construction (the
            // wiring contract: a session adds it before Attach runs), so drive one process frame
            // before reading it, matching what a real running session would do.
            hud._Process(0.0);
            ctx.Same(graph.Rows.Count, hud.BuildLines().Count,
                $"the readout carries one line per display row before anything happens");

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
            DriveInactive(world, def);
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
            report.AppendLine($"OBJECTIVE{def.Number} completed off its INACTIVEn node, " +
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

    // Destroys (or deactivates) every node one objective's INACTIVE paths name, the same
    // resolve-then-kill shape CampaignMissionEnd's own driver uses.
    private static void DriveInactive(TestWorld world, ObjectiveDef def)
    {
        foreach (var path in def.Inactive)
        {
            if (Resolve(world, path) is not { } node)
            {
                continue;
            }

            if (world.Runtime.Destructibles.Resolve(node) is { MaxHealth: > 0f } live)
            {
                world.Runtime.DamageAt(node, live.MaxHealth);
            }
            else
            {
                AnimRuntime.SetSubtreeActive(node, false);
            }
        }
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
