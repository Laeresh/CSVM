using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// A clutter stamp draws exactly while its stamping world node is visible in the tree. A kind's
/// placements share one MultiMesh, so a switched-off subtree cannot hide its trees by inheritance.
/// A hidden stamp collapses its basis and loses its solid shape, as a crater's victim does.
/// ⚠ Derive the state from the stamping node's visibility, never from the record's ACTIVE bit. A
/// script can switch a subtree on later, and its trees must come back with it.
/// ⚠ Restore only what this hid, so a stamp a crater destroyed stays destroyed.
/// </summary>
public sealed class ClutterActivation
{
    // The builder's own list, so the added stamps it rebuilds at the tail are read here as they stand.
    private readonly IReadOnlyList<ClutterBuilder.KindExport> _exports;

    // Per export, per placement: whether this collapsed it. Parallel to each export's Placements.
    private readonly List<bool[]> _hidden = new();

    private readonly Node3D _world;

    // The first export holding the remake's added stamps, and the handlers bound for them, dropped on a rebind.
    private readonly int _addedFrom;
    private readonly List<(Node3D Owner, Action Sync)> _addedSyncs = new();

    private ClutterActivation(Node3D world, IReadOnlyList<ClutterBuilder.KindExport> exports, int addedFrom)
    {
        _world = world;
        _exports = exports;
        _addedFrom = addedFrom;
        foreach (var export in exports)
        {
            _hidden.Add(new bool[export.Placements.Count]);
        }
    }

    /// <summary>Bumped on every stamp this hides or shows, so a copy of the stamps elsewhere
    /// (<see cref="MapEdgeExtender"/>'s continuation) re-reads <see cref="IsHidden"/> only after a
    /// change.</summary>
    public int Version { get; private set; }

    /// <summary>Stamps currently hidden because their stamping node is not visible.</summary>
    public int HiddenCount { get; private set; }

    /// <summary>Stamping nodes bound to a built world node, and those the world build never
    /// created, whose stamps are left as they are.</summary>
    public int BoundOwners { get; private set; }

    /// <inheritdoc cref="BoundOwners"/>
    public int UnboundOwners { get; private set; }

    /// <summary>Binds every export's stamps to the built world node their stamping gamez node
    /// became, found under <paramref name="world"/> by <see cref="AnimRuntime.IndexMeta"/>. Syncs
    /// on the node's <c>VisibilityChanged</c>, which Godot propagates to descendants. Also syncs on
    /// <c>TreeEntered</c>: the bootstrap switches a world before it joins the tree, when a
    /// visibility write emits nothing. Null when nothing was exported. The exports from
    /// <paramref name="addedFrom"/> on are the remake's added stamps, which <see cref="RebindAdded"/> binds again.</summary>
    public static ClutterActivation? Bind(Node3D world, IReadOnlyList<ClutterBuilder.KindExport>? exports, int addedFrom = int.MaxValue)
    {
        if (exports == null || exports.Count == 0)
        {
            return null;
        }

        var activation = new ClutterActivation(world, exports, Math.Min(addedFrom, exports.Count));
        activation.BindRange(0, activation._addedFrom, null);
        activation.BindRange(activation._addedFrom, exports.Count, activation._addedSyncs);
        Log.Info("world", $"clutter activation: {activation.BoundOwners} stamping node(s) bound, {activation.UnboundOwners} never built");
        return activation;
    }

    /// <summary>The builder rebuilt its added stamps after a lab edit. What was bound for the old ones is
    /// dropped, and the new ones follow their stamping nodes as the rest do.</summary>
    public void RebindAdded()
    {
        foreach (var (owner, sync) in _addedSyncs)
        {
            owner.VisibilityChanged -= sync;
            owner.TreeEntered -= sync;
        }
        _addedSyncs.Clear();
        for (int k = _addedFrom; k < _hidden.Count; k++)
        {
            HiddenCount -= Array.FindAll(_hidden[k], h => h).Length;
        }
        _hidden.RemoveRange(_addedFrom, _hidden.Count - _addedFrom);
        for (int k = _addedFrom; k < _exports.Count; k++)
        {
            _hidden.Add(new bool[_exports[k].Placements.Count]);
        }
        BindRange(_addedFrom, _exports.Count, _addedSyncs);
        Version++;
    }

    /// <summary>Whether placement <paramref name="placement"/> of export <paramref name="export"/>
    /// is hidden because its stamping node is.</summary>
    public bool IsHidden(int export, int placement) => _hidden[export][placement];

    // The first built node per stamping index, in tree order, which is the claimant the animation
    // runtime's by-index lookup also keeps. By index rather than GetChildren() (PERF-20).
    private static void Collect(Node node, Dictionary<int, List<(int, int)>> wanted, Dictionary<int, Node3D> built)
    {
        if (node is Node3D n3d && n3d.HasMeta(AnimRuntime.IndexMeta))
        {
            int index = n3d.GetMeta(AnimRuntime.IndexMeta).AsInt32();
            if (wanted.ContainsKey(index))
            {
                built.TryAdd(index, n3d);
            }
        }
        for (int i = 0, count = node.GetChildCount(); i < count; i++)
        {
            Collect(node.GetChild(i), wanted, built);
        }
    }

    private static void SetCollider(ClutterBuilder.KindExport export, int i, bool enabled)
    {
        if (export.Colliders is { } colliders && colliders[i] is { } slot)
        {
            PhysicsServer3D.BodySetShapeDisabled(slot.Body, slot.Shape, !enabled);
        }
    }

    // Binds exports [from, to) to their stamping nodes. The added stamps' handlers are kept in `added` so a
    // rebind can drop them, and only the original's count toward the bound owners.
    private void BindRange(int from, int to, List<(Node3D, Action)>? added)
    {
        var stamps = new Dictionary<int, List<(int Export, int Placement)>>();
        for (int k = from; k < to; k++)
        {
            var owners = _exports[k].Owners;
            for (int i = 0; i < owners.Count; i++)
            {
                if (!stamps.TryGetValue(owners[i], out var list))
                {
                    stamps[owners[i]] = list = new List<(int, int)>();
                }
                list.Add((k, i));
            }
        }

        var built = new Dictionary<int, Node3D>();
        Collect(_world, stamps, built);
        foreach (var (index, list) in stamps)
        {
            if (!built.TryGetValue(index, out var owner))
            {
                UnboundOwners += added == null ? 1 : 0;
                continue;
            }
            BoundOwners += added == null ? 1 : 0;
            Action sync = () => Sync(owner, list);
            owner.VisibilityChanged += sync;
            owner.TreeEntered += sync;
            added?.Add((owner, sync));
            Sync(owner, list);
        }
    }

    private void Sync(Node3D owner, List<(int Export, int Placement)> stamps)
    {
        if (!owner.IsInsideTree())
        {
            return; // resolved for real when the built world joins the tree
        }
        bool shown = owner.IsVisibleInTree();
        foreach (var (k, i) in stamps)
        {
            Apply(k, i, shown);
        }
    }

    private void Apply(int k, int i, bool shown)
    {
        var export = _exports[k];
        if (export.Instances is not { } mm || shown != _hidden[k][i])
        {
            return;
        }
        if (shown)
        {
            mm.Restore(i);
            SetCollider(export, i, enabled: true);
            _hidden[k][i] = false;
            HiddenCount--;
        }
        else
        {
            if (mm.Moved(i))
            {
                return; // a crater's victim, which nothing brings back
            }
            // A stamp the vegetation mask dropped is hidden too, so a move to the Original variant keeps it hidden.
            var placed = mm.GetInstanceTransform(i);
            mm.SetInstanceTransform(i, new Transform3D(placed.Basis.Scaled(Vector3.Zero), placed.Origin));
            SetCollider(export, i, enabled: false);
            _hidden[k][i] = true;
            HiddenCount++;
        }
        Version++;
    }
}
