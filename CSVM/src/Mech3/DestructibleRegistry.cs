using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Live, mutable per-instance state for the world's destructible objects — the single owner of
/// "how much health is this particular tower down to right now".
///
/// A destructible is not a file type: it is an <see cref="AnimDefinition"/> carrying
/// <c>HEALTH &gt; 0</c> (the whole test — see <c>docs/formats/destructibles.md</c>). One
/// definition binds to many world nodes — its <c>NAME</c> is a wildcard, so the reader's
/// <c>ap_h2otwr*</c> covers four towers, and even the compiler's expanded per-instance defs can
/// resolve to more than one node — and each of those node groups is an <b>independent</b>
/// instance with its own hit points. Keying on <c>(def, anchor)</c> is what keeps one tower's
/// damage from breaking its siblings.
///
/// This registry is the C21 foundation: it holds current/max HP and coarse lifecycle state, and
/// makes <c>ANIM_HEALTH</c> condition evaluation read the live value instead of the static
/// authored one. It applies no damage itself — nothing decrements HP until C23's
/// <c>WeaponHit</c> path — so a freshly built world reads identically to before (every instance
/// sits at full health). C22's damage-stage evaluation and C24's death sequence hang off the
/// same instances.
/// </summary>
public sealed class DestructibleRegistry
{
    /// <summary>Coarse lifecycle state of one destructible instance. Populated from C22/C24 on;
    /// C21 leaves everything <see cref="Healthy"/> because no damage is applied yet.</summary>
    public enum State
    {
        Healthy,   // at full HP, undamaged
        Damaged,   // below max, above zero — a DAMAGE_SEQUENCE stage is showing (C22)
        Destroyed, // HP reached zero, death sequence run (C24)
    }

    /// <summary>One live destructible node group: its authored definition, the world node it
    /// binds to, and the HP that actually falls as it takes fire.</summary>
    public sealed class Instance
    {
        public AnimDefinition Def { get; }
        public Node3D Anchor { get; }
        public float MaxHealth { get; }
        public float Health { get; set; }
        public State Status { get; set; } = State.Healthy;

        /// <summary>How many of the DAMAGE_SEQUENCE's descending health thresholds this instance
        /// has fallen past — the deepest progressive-damage stage it has escalated to. Only ever
        /// increases (C22 escalates, never heals), so a stage effect fires exactly once; a reset
        /// (C28) puts it back to 0.</summary>
        public int DamageStage { get; set; }

        public Instance(AnimDefinition def, Node3D anchor, float maxHealth)
        {
            Def = def;
            Anchor = anchor;
            MaxHealth = maxHealth;
            Health = maxHealth;
        }
    }

    // Keyed by (definition, anchor instance id) — the exact pair EvaluateCondition holds when it
    // reaches an ANIM_HEALTH branch, so the live read is O(1) on the hot poll path. Godot object
    // identity is by native pointer, so key on the instance id, not the Node3D itself.
    private readonly Dictionary<(AnimDefinition Def, ulong Anchor), Instance> _byKey = new();
    private readonly List<Instance> _all = new();

    /// <summary>Number of live destructible instances — one per <c>(def, anchor)</c> pair.
    /// Exceeds <see cref="DistinctAnchors"/> when more than one def binds a node (the reader
    /// wildcard def and the compiler's per-instance defs both resolve to the same towers; each
    /// keeps its own HP pool, which is why keying is per pair, not per node).</summary>
    public int Count => _all.Count;

    /// <summary>Number of distinct world node groups covered — the count of physical destructible
    /// objects, ignoring how many defs bind each.</summary>
    public int DistinctAnchors => _anchors.Count;

    public IReadOnlyList<Instance> All => _all;

    private readonly HashSet<ulong> _anchors = new();

    // The ONE authoritative instance per anchor node, for resolving a struck world node back to a
    // destructible (C23). A node can carry several instances (reader wildcard + compiled
    // per-instance); the compiled def is the better data, so it wins.
    private readonly Dictionary<ulong, Instance> _authoritative = new();

    /// <summary>Registers one destructible node group at full health. Idempotent: a repeated
    /// <c>(def, anchor)</c> returns the existing instance without resetting its HP, so a second
    /// bootstrap pass or a re-index cannot silently heal a damaged object.</summary>
    public Instance Register(AnimDefinition def, Node3D anchor, float maxHealth)
    {
        var key = (def, anchor.GetInstanceId());
        if (_byKey.TryGetValue(key, out var existing))
            return existing;
        var inst = new Instance(def, anchor, maxHealth);
        _byKey[key] = inst;
        _all.Add(inst);
        ulong aid = anchor.GetInstanceId();
        _anchors.Add(aid);
        // Compiled beats reader as the authoritative instance for this anchor (compiled defs load
        // first, so this normally just fills an empty slot, but the check makes it order-proof).
        if (!_authoritative.TryGetValue(aid, out var current)
            || (def.Archive != null && current.Def.Archive == null))
        {
            _authoritative[aid] = inst;
        }
        return inst;
    }

    /// <summary>The live instance for a <c>(def, anchor)</c> pair, or null when that pair is not
    /// a registered destructible — the common case, since most conditions evaluated are not on
    /// destructibles at all. A null anchor is never a destructible.</summary>
    public Instance? Get(AnimDefinition def, Node3D? anchor)
    {
        if (anchor == null)
            return null;
        return _byKey.TryGetValue((def, anchor.GetInstanceId()), out var inst) ? inst : null;
    }

    /// <summary>The destructible instance a struck world node belongs to. The struck node is a
    /// raycast-hit collider deep under the anchor's subtree, so this climbs the parent chain to
    /// find a registered anchor. It walks the WHOLE chain rather than stopping at the first hit,
    /// because a compiled def and a reader wildcard can anchor to DIFFERENT nodes of one object —
    /// the water tower's compiled def roots on <c>ap_h2otwr1</c> while its reader def's <c>*</c>
    /// also grabs the inner <c>ap_h2otwr.flt</c>, which is nearer the collider — and the compiled
    /// def is the authoritative one (its DAMAGE_SEQUENCE and death sequence are the real ones). So
    /// the nearest COMPILED anchor wins; failing any compiled, the nearest reader. Null when
    /// nothing up the chain is a destructible (terrain, water, clutter, the sky).</summary>
    public Instance? Resolve(Node? struck)
    {
        Instance? nearestReader = null;
        for (var n = struck; n != null; n = n.GetParent())
        {
            if (!_authoritative.TryGetValue(n.GetInstanceId(), out var inst))
                continue;
            if (inst.Def.Archive != null)
                return inst;               // compiled = authoritative, take the nearest
            nearestReader ??= inst;        // fallback if no compiled anchor is found up the chain
        }
        return nearestReader;
    }

    /// <summary>Drops every instance. Used when a runtime is torn down and rebuilt.</summary>
    public void Clear()
    {
        _byKey.Clear();
        _all.Clear();
        _anchors.Clear();
        _authoritative.Clear();
    }
}
