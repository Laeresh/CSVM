using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>One persisted world object's carried state: which node it is, what shape it was left
/// in, and the def that made it persistent. <see cref="Node"/> is the flat gamez node index
/// (<see cref="AnimRuntime.IndexMeta"/>), which is stable across every build of one chapter's world
/// and unambiguous where names repeat; <see cref="NodeName"/> and <see cref="Def"/> are carried for
/// reading a stored log, not for matching.</summary>
public readonly record struct PersistedObject(
    int Node, string Def, string NodeName, bool Destroyed, float Health);

/// <summary>
/// The cross-mission state log (`BL-243`): what a campaign mission left destroyed, carried into
/// later missions of the SAME chapter. The original walks its mission list backwards for the most
/// recent earlier entry in the same world folder and loads that mission's <c>Mission.NNN</c>
/// (docs/formats/saved-games.md); a log keyed by chapter is that walk's result, since every earlier
/// mission of a chapter has already merged into its chapter's entry. Only
/// <see cref="AnimDefinition.PersistLog"/> defs are carried, and a node counts as persisted when
/// ANY def bound to it carries the flag: the compiled per-instance def a weapon hit resolves to
/// never carries it, only the reader's wildcard twin does. What the log holds is destruction, not
/// the full <c>Mission.NNN</c> world state, so a running looping animation (the original carries
/// those too) is out of scope here.
/// </summary>
public sealed class CampaignPersistLog
{
    // Chapter (the cm_sequence 'campaign' world-folder number) -> node index -> its state.
    private readonly Dictionary<int, Dictionary<int, PersistedObject>> _byChapter = new();

    /// <summary>Every chapter this log holds state for.</summary>
    public IReadOnlyCollection<int> Chapters => _byChapter.Keys;

    /// <summary>How many object states are held across all chapters.</summary>
    public int Count
    {
        get
        {
            int total = 0;
            foreach (var chapter in _byChapter.Values)
            {
                total += chapter.Count;
            }

            return total;
        }
    }

    /// <summary>Reads the persisted-object states out of a live world: every node a
    /// <c>PERSIST_LOG</c> def binds that is no longer intact, reported from the pool the hit path
    /// actually damages (<see cref="DestructibleRegistry.Resolve"/>). A node with no gamez index is
    /// skipped, since nothing could key it back to the same object in a later build.</summary>
    public static List<PersistedObject> Capture(AnimRuntime runtime)
    {
        var registry = runtime.Destructibles;
        var seen = new HashSet<ulong>();
        var states = new List<PersistedObject>();
        foreach (var inst in registry.All)
        {
            if (!inst.Def.PersistLog || !seen.Add(inst.Anchor.GetInstanceId()))
            {
                continue;
            }

            int index = NodeIndex(inst.Anchor);
            var live = registry.Resolve(inst.Anchor) ?? inst;
            if (index < 0 || (live.Status == DestructibleRegistry.State.Healthy
                && live.Health >= live.MaxHealth))
            {
                continue;
            }

            states.Add(new PersistedObject(index, inst.Def.Name, inst.Anchor.Name,
                live.Status == DestructibleRegistry.State.Destroyed, live.Health));
        }

        return states;
    }

    /// <summary>Whether a mission that ended this way commits its capture to the log. The original
    /// writes the world-state carrier once, in the mission-end pass, and only for a won mission
    /// (docs/formats/saved-games.md). ⚠ Never commit a loss: <see cref="Merge"/> only ever
    /// escalates damage, so a failed attempt's wreckage could not be taken back on the retry.</summary>
    public static bool CommitsOn(MissionOutcome outcome) => outcome == MissionOutcome.Won;

    /// <summary>Every state held for one chapter, in insertion order of its nodes.</summary>
    public IReadOnlyList<PersistedObject> For(int chapter)
    {
        var states = new List<PersistedObject>();
        if (_byChapter.TryGetValue(chapter, out var byNode))
        {
            states.AddRange(byNode.Values);
        }

        return states;
    }

    /// <summary>Folds a mission's captured states into the chapter's log. Damage only ever
    /// escalates: a state already destroyed stays destroyed, and a repeat of the same node keeps
    /// the lower health. ⚠ Never let a fresh capture heal a node, or replaying a mission would
    /// undo what an earlier one left wrecked, which is the one thing the original's backwards walk
    /// cannot do.</summary>
    public void Merge(int chapter, IEnumerable<PersistedObject> states)
    {
        if (!_byChapter.TryGetValue(chapter, out var byNode))
        {
            _byChapter[chapter] = byNode = new Dictionary<int, PersistedObject>();
        }

        foreach (var state in states)
        {
            if (!byNode.TryGetValue(state.Node, out var held))
            {
                byNode[state.Node] = state;
                continue;
            }

            if (held.Destroyed || (!state.Destroyed && state.Health >= held.Health))
            {
                continue;
            }

            byNode[state.Node] = state;
        }
    }

    /// <summary>Applies a chapter's log to a freshly built world of that chapter, after its
    /// bootstrap: each recorded object is put into the state it was left in, silently, through
    /// <see cref="AnimRuntime.CarryState"/>. The original opens a later mission on the destroyed
    /// pose, not on a replayed death, so no effect, sound or choreography runs here; a later hit
    /// finds the pool destroyed and is a no-op. Returns how many objects it acted on. Objects the
    /// world does not carry are skipped, as with scenery a mission never places.</summary>
    public int ApplyTo(AnimRuntime runtime, int chapter)
    {
        var registry = runtime.Destructibles;
        var byIndex = new Dictionary<int, Node3D>();
        foreach (var inst in registry.All)
        {
            int index = NodeIndex(inst.Anchor);
            if (index >= 0)
            {
                byIndex[index] = inst.Anchor;
            }
        }

        int applied = 0;
        foreach (var state in For(chapter))
        {
            if (!byIndex.TryGetValue(state.Node, out var anchor)
                || registry.Resolve(anchor) is not { } live)
            {
                continue;
            }

            // ⚠ Never DamageAt here; it replays the death choreography at mission open.
            if (runtime.CarryState(live, state.Destroyed, state.Health))
            {
                applied++;
            }
        }

        Log.Info("campaign", $"persist log: {applied} of {For(chapter).Count} carried object(s) restored silently in chapter {chapter}");
        return applied;
    }

    /// <summary>Replaces this log's whole contents, for a profile load.</summary>
    public void Reset(IEnumerable<KeyValuePair<int, IReadOnlyList<PersistedObject>>> chapters)
    {
        _byChapter.Clear();
        foreach (var (chapter, states) in chapters)
        {
            Merge(chapter, states);
        }
    }

    private static int NodeIndex(Node3D node) =>
        node.HasMeta(AnimRuntime.IndexMeta) ? (int)node.GetMeta(AnimRuntime.IndexMeta) : -1;
}
