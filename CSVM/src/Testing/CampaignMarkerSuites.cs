using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the campaign's objective markers: the store that says where the mission
/// wants the player to go, and the HUD read model that finally consumes it.</summary>
internal static class CampaignMarkerSuites
{
    // The story position flown. Chapter, mission folder and every node name below come out of
    // cm_sequence and that mission's own data, so nothing here names a shipped world node.
    private const int FirstSeq = 0;

    /// <summary>Drives the campaign's first mission against its BUILT world: the objective sites
    /// its <c>targets.zrd</c> flags each carry a marker with the original's two label lines and its
    /// decoded colour, the marker sits on the world node it names, and flying the site's own
    /// <c>TRAVELERS</c> approach retires that marker and leaves the rest standing.</summary>
    internal static void CampaignObjectiveMarkers(TestContext ctx)
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
        var targets = MissionTargets.Load(missionZrdr);
        var messages = Messages.Load(ctx.MessagesPath);
        var report = new StringBuilder();
        report.AppendLine($"seq {FirstSeq} -> {chapter}/{folder}: " +
            $"{targets.Count} target entries, {script.Objectives.Count} objectives");
        ctx.Check(FlaggedCount(targets) > 0,
            $"the flown mission's target table flags at least one node as an objective");

        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        ctx.ExtraPrewarmSoundNames = script.SoundGroupNames();
        ctx.WithWorld(chapter, collision: false, folder, world =>
            Drive(ctx, world, director, script, targets, messages, report));

        ctx.WriteArtifact($"test-campaign-objective-markers-{chapter}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder}'s objective markers against a built world");
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        ObjectiveScript script, MissionTargets targets, Messages messages, StringBuilder report)
    {
        // A one-slot array, not a captured local: the driver below flies the aircraft by writing
        // the listener position the director reads, which a lambda cannot do to a `ref` local.
        var listener = new Vector3[1] { ctx.Camera.GlobalPosition };
        director.Attach(new CampaignDirector.WorldInputs
        {
            Runtime = world.Runtime,
            Sounds = world.Runtime.Sounds,
            ListenerPosition = () => listener[0],
            Rng = new Random(1),
        });
        var graph = director.Graph!;
        var hud = ObjectiveMarkerHud.Build(director, messages, targets, world.Runtime, () => null);
        ctx.Host.AddChild(hud);

        // The HUD polls for the graph in _Process rather than at construction (a session adds it
        // before Attach runs), so drive one frame before reading it, as a real session would.
        hud._Process(0.0);
        foreach (var marker in hud.Markers)
        {
            report.AppendLine($"marker '{marker.Node}': \"{marker.CategoryLine}\" / " +
                $"\"{marker.Name}\" at {marker.Position} colour {marker.Color}");
        }

        ctx.Check(hud.Markers.Count > 0,
            $"the mission's objective sites carry a marker before anything has completed");
        CheckOneMarker(ctx, world, hud, report);
        CheckPointSite(ctx, world, script, hud, report);
        RetireOneSite(ctx, script, graph, hud, listener, report);
        hud.QueueFree();
    }

    // The label and colour of the first marker that resolved: the original's category line over the
    // site's own name, in the blue a non-destructive objective earns.
    private static void CheckOneMarker(
        TestContext ctx, TestWorld world, ObjectiveMarkerHud hud, StringBuilder report)
    {
        if (hud.Markers.Count == 0)
        {
            return;
        }

        var marker = hud.Markers[0];
        ctx.Check(marker.Name.Length > 0 && !marker.Name.StartsWith("MSG_", StringComparison.Ordinal),
            $"'{marker.Node}' carries a resolved name line, not a raw message key ('{marker.Name}')");
        ctx.Check(marker.CategoryLine.EndsWith("-", StringComparison.Ordinal),
            $"and the original's category line above it ('{marker.CategoryLine}')");
        ctx.Check(marker.Color == MarkerDraw.HudBlue,
            $"drawn in the marker blue the original uses for a non-destructive objective");
        var found = world.Runtime.FindNodes(marker.Node, null);
        ctx.Check(found.Count > 0 && found[0].GlobalPosition.IsEqualApprox(marker.Position),
            $"and it sits on the world node the mission named, not on a guessed point");
        report.AppendLine($"checked '{marker.Node}' at {marker.Position}");
    }

    // A site the mission names by a bare TRAVELERS point: the marker belongs on that point, not on
    // the world node of the same name, which this mission parks at the origin.
    private static void CheckPointSite(TestContext ctx, TestWorld world, ObjectiveScript script,
        ObjectiveMarkerHud hud, StringBuilder report)
    {
        foreach (var marker in hud.Markers)
        {
            if (ObjectiveMarkerHud.PointFor(script, marker.Node) is not { } point)
            {
                continue;
            }

            var found = world.Runtime.FindNodes(marker.Node, null);
            report.AppendLine($"'{marker.Node}' point {point} vs node " +
                $"{(found.Count > 0 ? found[0].GlobalPosition.ToString() : "unresolved")}");
            ctx.Check(marker.Position.IsEqualApprox(point),
                $"'{marker.Node}' is marked at the point its objective tests, not at its node");
            ctx.Check(found.Count == 0 || !found[0].GlobalPosition.IsEqualApprox(point),
                $"and that point is somewhere the node itself is not, so the choice matters");
            return;
        }

        report.AppendLine("no drawn marker is named by a bare TRAVELERS point");
    }

    // Flies the site's own TRAVELERS approach by putting the listener on it: the objective
    // completes, its REMOVE_OBJECTIVE_TARGET fires, and that marker alone leaves the HUD.
    private static void RetireOneSite(TestContext ctx, ObjectiveScript script, ObjectiveGraph graph,
        ObjectiveMarkerHud hud, Vector3[] listener, StringBuilder report)
    {
        if (Approachable(script, hud) is not { } site)
        {
            report.AppendLine("no marker this mission draws is retired by a TRAVELERS approach");
            return;
        }

        int before = hud.Markers.Count;
        listener[0] = site.Position;
        for (float t = 0f; t < 6f; t += 0.1f)
        {
            graph.Step(0.1f);
        }

        hud._Process(0.0);
        report.AppendLine($"flew the approach to '{site.Node}': {before} markers -> {hud.Markers.Count}");
        ctx.Check(!Drawn(hud, site.Node),
            $"flying '{site.Node}'s approach retires its marker");
        ctx.Check(hud.Markers.Count < before,
            $"and leaves the mission's remaining sites marked ({hud.Markers.Count} still drawn)");
    }

    // The first drawn marker whose site an awake objective both approaches (TRAVELERS on that node)
    // and removes when it completes, which is the mission's own "you have been here" pair.
    private static ObjectiveMarker? Approachable(ObjectiveScript script, ObjectiveMarkerHud hud)
    {
        foreach (var marker in hud.Markers)
        {
            foreach (var def in script.Objectives)
            {
                if (def.Travelers is { } spec
                    && string.Equals(spec.WhereNode, marker.Node, StringComparison.OrdinalIgnoreCase)
                    && Names(def.RemoveObjectiveTarget, marker.Node))
                {
                    return marker;
                }
            }
        }

        return null;
    }

    private static bool Drawn(ObjectiveMarkerHud hud, string node)
    {
        foreach (var marker in hud.Markers)
        {
            if (string.Equals(marker.Node, node, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Names(IReadOnlyList<string> names, string node)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, node, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int FlaggedCount(MissionTargets targets)
    {
        int flagged = 0;
        foreach (var entry in targets.ByNode)
        {
            if (entry.Value.Objective)
            {
                flagged++;
            }
        }

        return flagged;
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
