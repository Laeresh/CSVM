using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the campaign's objective sites: the store that says where the mission
/// wants the player to go, and the target cycle that finally consumes it. The sites go through the
/// ordinary selection, so what is asserted is a pool, a cycle and a sticky selection.</summary>
internal static class CampaignMarkerSuites
{
    // The story position flown. Chapter, mission folder and every node name below come out of
    // cm_sequence and that mission's own data, so nothing here names a shipped world node.
    private const int FirstSeq = 0;

    // The story position whose report this is (C1/M04): the hull's own child and a ground node
    // share the child's name, which is what a path has to tell apart.
    private const int PathSeq = 8;
    private const string PathParent = "piratezep";
    private const string PathChild = "rock_zeppelin";
    private const string PathKey = "piratezep/rock_zeppelin";
    private const string PathLabel = "MSG_OBJ_DEFEND";

    // How far a site's world node is moved to prove the candidate follows it.
    private static readonly Vector3 Shove = new(600f, 0f, -400f);

    /// <summary>Drives the campaign's first mission against its BUILT world: the objective sites
    /// its <c>targets.zrd</c> flags reach the player's Enemy cycle carrying the mission's objective
    /// flag, exactly one is selected at a time, a site under a node that moves is marked where it
    /// now is, and flying a site's own <c>TRAVELERS</c> approach retires it and leaves the
    /// rest.</summary>
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
        ctx.Note($"drove {chapter}/{folder}'s objective sites through the target cycle");
    }

    /// <summary>A path-authored site over the mission whose report this is: with two
    /// <c>rock_zeppelin</c> nodes in the world, <c>ADD_OBJECTIVE_TARGET [[piratezep,
    /// rock_zeppelin]]</c> offers ONE site, standing on the hull's own child, with the
    /// <c>SET_HELP_LABEL</c> written against the same path on it; the hull's root and the ground
    /// node stay unmarked.</summary>
    internal static void CampaignObjectiveTargetPath(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), PathSeq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {PathSeq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder);
        ctx.RequireData(missionZrdr, $"{chapter}/{folder} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");

        var shipped = ObjectiveScript.Load(missionZrdr);
        ctx.Check(AuthorsPath(shipped), $"{chapter}/{folder} authors ADD_OBJECTIVE_TARGET [[{PathParent}, {PathChild}]]");

        // The same directives the shipped objective carries, on a conditionless objective so
        // they run on the first tick instead of after the hatch animations it waits on.
        var script = ObjectiveScript.Parse(new List<object?>
        {
            new List<object?>
            {
                "OBJECTIVE1", new List<object?>
                {
                    "ADD_OBJECTIVE_TARGET", new List<object?> { new List<object?> { PathParent, PathChild } },
                    "SET_HELP_LABEL", new List<object?> { new List<object?> { PathParent, PathChild }, PathLabel },
                },
            },
        });
        var targets = MissionTargets.Load(missionZrdr);
        var messages = Messages.Load(ctx.MessagesPath);
        var report = new StringBuilder();
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var director = CampaignDirector.Create(script, mission, profile, null);
        ctx.WithWorld(chapter, collision: false, folder, world =>
            DrivePath(ctx, world, director, targets, messages, report));

        ctx.WriteArtifact($"test-campaign-objective-target-path-{chapter}.txt", report.ToString());
        ctx.Note($"{chapter}/{folder}: [{PathParent}, {PathChild}] marks the hull's own child alone");
    }

    private static void DrivePath(TestContext ctx, TestWorld world, CampaignDirector director,
        MissionTargets targets, Messages messages, StringBuilder report)
    {
        var listener = ctx.Camera.GlobalPosition;
        director.Attach(new CampaignDirector.WorldInputs
        {
            Runtime = world.Runtime,
            Sounds = world.Runtime.Sounds,
            ListenerPosition = () => listener,
            Rng = new Random(1),
        });
        var graph = director.Graph!;
        graph.Step(0.1f);
        ctx.Check(graph.IsObjectiveTarget(PathKey), $"the graph holds the path as one key '{PathKey}'");
        ctx.Check(!graph.IsObjectiveTarget(PathParent) && !graph.IsObjectiveTarget(PathChild),
            $"and neither bare name on its own");

        var all = world.Runtime.FindNodes(PathChild, null);
        var hull = ObjectiveSites.ResolveTarget(world.Runtime, new ObjectiveTarget(new[] { PathParent }));
        var child = ObjectiveSites.ResolveTarget(world.Runtime, ObjectiveTarget.Parse(PathKey));
        report.AppendLine($"{all.Count} '{PathChild}' nodes; hull {hull?.GlobalPosition.ToString() ?? "-"}, " +
            $"its child {child?.GlobalPosition.ToString() ?? "-"}");
        ctx.Check(all.Count >= 2, $"the world carries more than one '{PathChild}' ({all.Count})");
        ctx.Check(hull != null && child != null && hull.IsAncestorOf(child),
            $"'{PathKey}' resolves to the node inside '{PathParent}'");

        var sites = new ObjectiveSites(director, messages, targets, world.Runtime);
        var offered = new List<AimCandidate>();
        sites.Collect(offered);
        var marked = new List<ObjectiveSite>();
        foreach (var candidate in offered)
        {
            if (candidate.Source is ObjectiveSite site
                && site.Target.Node.Equals(PathChild, StringComparison.OrdinalIgnoreCase))
            {
                marked.Add(site);
                report.AppendLine($"site '{site.Node}' at {site.Position} category '{site.Category}'");
            }
        }

        ctx.Same(1, marked.Count, $"exactly one '{PathChild}' site is offered");
        ctx.Check(marked.Count == 1 && marked[0].Node == PathKey,
            $"and it is the path's own key, not a bare name");
        ctx.Check(marked.Count == 1 && child != null && marked[0].Position.IsEqualApprox(child.GlobalPosition),
            $"standing on the hull's child, not on a ground '{PathChild}'");
        ctx.Check(marked.Count == 1 && marked[0].Category == messages.Get(PathLabel).Trim(),
            $"with the help label written against the same path ('{marked[0].Category}')");
    }

    private static bool AuthorsPath(ObjectiveScript script)
    {
        foreach (var def in script.Objectives)
        {
            foreach (var target in def.AddObjectiveTarget)
            {
                if (target.Is(PathKey))
                {
                    return true;
                }
            }
        }

        return false;
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
        var sites = new ObjectiveSites(director, messages, targets, world.Runtime);
        var pilot = new Pilot(sites);
        pilot.Fly(listener[0]);
        foreach (var target in pilot.Selection.Pool.Enemy)
        {
            report.AppendLine($"enemy cycle: '{target.Name}' objective={target.Objective} " +
                $"\"{target.CategoryLine}\" / \"{target.DisplayName}\" at {target.Position}");
        }

        ctx.Check(ObjectiveCount(pilot.Selection.Pool.Enemy) > 0,
            $"the mission's objective sites reach the pilot's Enemy cycle");
        ctx.Check(NamesOf(pilot.Selection.Pool.NonAircraft, script, graph, targets) == 0,
            $"and none of them landed on the Non-Aircraft cycle instead");
        CheckSelection(ctx, pilot, report);
        CheckCycle(ctx, pilot, report);
        CheckPointSite(ctx, world, script, pilot, report);
        CheckMovingSite(ctx, world, script, pilot, report);
        RetireOneSite(ctx, script, graph, pilot, listener, report);
    }

    // The one selected site: the head of the cycle, drawn with the original's category line over
    // the site's own name, in the blue a non-destructive objective earns.
    private static void CheckSelection(TestContext ctx, Pilot pilot, StringBuilder report)
    {
        if (pilot.Selection.Current is not { } current)
        {
            ctx.Check(false, $"the pilot has a selected target with objective sites in the pool");
            return;
        }

        report.AppendLine($"selected '{current.Name}': objective={current.Objective} " +
            $"class={current.Class} colour {TargetHud.MarkerColor(current, AimAssist.PlayerTeam)}");
        ctx.Check(current.Objective && current.Class == TargetClass.Enemy,
            $"an objective site is what the auto-acquire selects ('{current.Name}')");
        ctx.Check(pilot.Selection.Ordered.Count > 0 && pilot.Selection.Ordered[0].Objective,
            $"and it sorts ahead of every sector, so it heads the cycle");
        ctx.Check(current.DisplayName.Length > 0
            && !current.DisplayName.StartsWith("MSG_", StringComparison.Ordinal),
            $"the marker's name line is resolved, not a raw message key ('{current.DisplayName}')");
        ctx.Check(current.CategoryLine.EndsWith("-", StringComparison.Ordinal),
            $"with the original's category line above it ('{current.CategoryLine}')");
        ctx.Check(TargetHud.MarkerColor(current, AimAssist.PlayerTeam) == MarkerDraw.HudBlue,
            $"in the marker blue the original uses for a non-destructive objective");
    }

    // One at a time, stepped like an enemy: d-pad up's NextEnemy moves the selection to another
    // site, and an untouched rebuild keeps the one it landed on.
    private static void CheckCycle(TestContext ctx, Pilot pilot, StringBuilder report)
    {
        if (pilot.Selection.Current is not { } first || ObjectiveCount(pilot.Selection.Ordered) < 2)
        {
            report.AppendLine("fewer than two objective sites are selectable, so no step to check");
            return;
        }

        pilot.Selection.NextEnemy();
        pilot.Fly(pilot.Position);
        var stepped = pilot.Selection.Current;
        report.AppendLine($"next enemy: '{first.Name}' -> '{stepped?.Name ?? "-"}'");
        ctx.Check(stepped is { Objective: true } && !stepped.Value.IsSameTarget(first),
            $"stepping the enemy cycle moves to another objective site");
        pilot.Fly(pilot.Position);
        ctx.Check(stepped is { } held && pilot.Selection.Current is { } after
            && after.IsSameTarget(held),
            $"and a rebuild holds it, so the selection survives a frame");
    }

    // A site the mission names by a bare TRAVELERS point: it belongs on that point, not on the
    // world node of the same name, which this mission parks at the origin.
    private static void CheckPointSite(TestContext ctx, TestWorld world, ObjectiveScript script,
        Pilot pilot, StringBuilder report)
    {
        foreach (var target in pilot.Selection.Pool.Enemy)
        {
            if (!target.Objective || ObjectiveSites.PointFor(script, target.Name) is not { } point)
            {
                continue;
            }

            var found = ObjectiveSites.ResolveTarget(world.Runtime, ObjectiveTarget.Parse(target.Name));
            report.AppendLine($"'{target.Name}' point {point} vs node " +
                $"{(found != null ? found.GlobalPosition.ToString() : "unresolved")}");
            ctx.Check(target.Position.IsEqualApprox(point),
                $"'{target.Name}' sits at the point its objective tests, not at its node");
            ctx.Check(found == null || !found.GlobalPosition.IsEqualApprox(point),
                $"and that point is somewhere the node itself is not, so the choice matters");
            return;
        }

        report.AppendLine("no offered site is named by a bare TRAVELERS point");
    }

    // The frozen-marker check: a site standing on a world node is rebuilt from that node every
    // frame, so moving the node (or its parent) moves the candidate with it.
    private static void CheckMovingSite(TestContext ctx, TestWorld world, ObjectiveScript script,
        Pilot pilot, StringBuilder report)
    {
        if (NodeSite(world, script, pilot) is not { } pick)
        {
            report.AppendLine("no offered site stands on a resolvable world node");
            return;
        }

        var (target, mover) = pick;
        var before = target.Position;
        var origin = mover.GlobalPosition;
        mover.GlobalPosition = origin + Shove;
        pilot.Fly(pilot.Position);
        var moved = Find(pilot.Selection.Pool.Enemy, target.Name);
        // Restore and rebuild together: a pool left holding the shoved position is a world state
        // every later check would read, and the mission's own approach tests would miss by 720 m.
        mover.GlobalPosition = origin;
        pilot.Fly(pilot.Position);
        report.AppendLine($"moved '{mover.Name}' by {Shove}: '{target.Name}' {before} -> " +
            $"{(moved is { } m ? m.Position.ToString() : "gone")}");
        ctx.Check(moved is { } after && after.Position.IsEqualApprox(before + Shove),
            $"'{target.Name}' tracks the node it stands on when that node moves");
        ctx.Check(moved is { } still && still.Source is ObjectiveSite,
            $"and it is still the same site object, so a selection on it would hold");
    }

    // Flies the site's own TRAVELERS approach by putting the listener on it: the objective
    // completes, its REMOVE_OBJECTIVE_TARGET fires, and that site alone leaves the cycle.
    private static void RetireOneSite(TestContext ctx, ObjectiveScript script, ObjectiveGraph graph,
        Pilot pilot, Vector3[] listener, StringBuilder report)
    {
        if (Approachable(script, pilot) is not { } site)
        {
            report.AppendLine("no site this mission offers is retired by a TRAVELERS approach");
            return;
        }

        int before = ObjectiveCount(pilot.Selection.Pool.Enemy);
        listener[0] = site.Position;
        for (float t = 0f; t < 6f; t += 0.1f)
        {
            graph.Step(0.1f);
        }

        pilot.Fly(pilot.Position);
        int after = ObjectiveCount(pilot.Selection.Pool.Enemy);
        report.AppendLine($"flew the approach to '{site.Name}': {before} sites -> {after}");
        ctx.Check(Find(pilot.Selection.Pool.Enemy, site.Name) == null,
            $"flying '{site.Name}'s approach retires its marker");
        ctx.Check(after > 0 && after < before,
            $"and leaves the mission's remaining sites offered ({after} still selectable)");
    }

    // The first offered site whose approach an awake objective both flies (TRAVELERS on that node)
    // and removes when it completes, which is the mission's own "you have been here" pair.
    private static TargetRef? Approachable(ObjectiveScript script, Pilot pilot)
    {
        foreach (var target in pilot.Selection.Pool.Enemy)
        {
            foreach (var def in script.Objectives)
            {
                if (target.Objective && def.Travelers is { } spec
                    && string.Equals(spec.WhereNode, target.Name, StringComparison.OrdinalIgnoreCase)
                    && Removes(def, target.Name))
                {
                    return target;
                }
            }
        }

        return null;
    }

    // The first offered site that stands on a world node rather than a bare point, with the node
    // to move: its PARENT where that parent is itself parented, which is the under-a-moving-hull
    // case, and the site's own node where the parent is the chapter world's own root.
    private static (TargetRef Target, Node3D Mover)? NodeSite(TestWorld world,
        ObjectiveScript script, Pilot pilot)
    {
        foreach (var target in pilot.Selection.Pool.Enemy)
        {
            if (!target.Objective || ObjectiveSites.PointFor(script, target.Name) != null)
            {
                continue;
            }

            var found = ObjectiveSites.ResolveTarget(world.Runtime, ObjectiveTarget.Parse(target.Name));
            if (found == null || !found.IsInsideTree())
            {
                continue;
            }

            var parent = found.GetParent() as Node3D;
            return (target, parent?.GetParent() is Node3D ? parent : found);
        }

        return null;
    }

    private static TargetRef? Find(IReadOnlyList<TargetRef> cycle, string name)
    {
        foreach (var target in cycle)
        {
            if (string.Equals(target.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return null;
    }

    private static int ObjectiveCount(IReadOnlyList<TargetRef> cycle)
    {
        int found = 0;
        foreach (var target in cycle)
        {
            if (target.Objective)
            {
                found++;
            }
        }

        return found;
    }

    // How many of the mission's live site names ended up on a cycle they do not belong to.
    private static int NamesOf(IReadOnlyList<TargetRef> cycle, ObjectiveScript script,
        ObjectiveGraph graph, MissionTargets targets)
    {
        var live = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, targets, live);
        int found = 0;
        foreach (var target in cycle)
        {
            if (Names(live, target.Name))
            {
                found++;
            }
        }

        return found;
    }

    private static bool Removes(ObjectiveDef def, string key)
    {
        foreach (var target in def.RemoveObjectiveTarget)
        {
            if (target.Is(key))
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

    // A pilot's targeting frame with no aircraft: the same pool feed and the same per-frame
    // rebuild FlightController.StepTargeting runs, driven a frame at a time by the suite.
    private sealed class Pilot
    {
        private readonly ObjectiveSites _sites;
        private readonly List<AimCandidate> _offered = new();
        private readonly AimCandidateSet _scan = new();

        internal Pilot(ObjectiveSites sites) => _sites = sites;

        internal TargetSelection Selection { get; } = new();

        internal Vector3 Position { get; private set; }

        internal void Fly(Vector3 position)
        {
            Position = position;
            _offered.Clear();
            _sites.Collect(_offered);
            Selection.Rebuild(_scan, null, AimAssist.PlayerTeam, null, position, Basis.Identity,
                _offered);
        }
    }
}
