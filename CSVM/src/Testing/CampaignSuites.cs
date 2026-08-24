using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites over the campaign's persistence: what a mission leaves destroyed, what survives
/// the profile file, and what a later mission of the same chapter starts with.</summary>
internal static class CampaignSuites
{
    // How many persisted objects the first mission destroys. Three is enough to prove the log
    // carries a set rather than one lucky node, and cheap enough to leave the world build dominant.
    private const int TargetCount = 3;

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
}
