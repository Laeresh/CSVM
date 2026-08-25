using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Live, mutable per-instance state for the world's destructible objects: current/max HP and
/// coarse lifecycle state, keyed by <c>(def, anchor)</c> since one wildcard <c>NAME</c> or a
/// compiler expansion can bind several independent instances to one def. Applies no damage
/// itself; the <c>WeaponHit</c> path decrements HP. Decode: <c>docs/formats/destructibles.md</c>.
/// </summary>
public sealed class DestructibleRegistry
{
    // Keyed by (definition, anchor instance id) — the exact pair EvaluateCondition holds when it
    // reaches an ANIM_HEALTH branch, so the live read is O(1) on the hot poll path. Godot object
    // identity is by native pointer, so key on the instance id, not the Node3D itself.
    private readonly Dictionary<(AnimDefinition Def, ulong Anchor), Instance> _byKey = new();
    private readonly List<Instance> _all = new();

    private readonly HashSet<ulong> _anchors = new();

    // The ONE authoritative instance per anchor node, for resolving a struck world node back to a
    // destructible. A node can carry several instances (reader wildcard + compiled
    // per-instance); the compiled def is the better data, so it wins.
    private readonly Dictionary<ulong, Instance> _authoritative = new();

    /// <summary>Coarse lifecycle state of one destructible instance. Everything sits at
    /// <see cref="Healthy"/> until damage is applied.</summary>
    public enum State
    {
        Healthy,   // at full HP, undamaged
        Damaged,   // below max, above zero — a DAMAGE_SEQUENCE stage is showing
        Destroyed, // HP reached zero, death sequence run
    }

    /// <summary>Number of live destructible instances — one per <c>(def, anchor)</c> pair.
    /// Exceeds <see cref="DistinctAnchors"/> when more than one def binds a node (the reader
    /// wildcard def and the compiler's per-instance defs both resolve to the same towers; each
    /// keeps its own HP pool, which is why keying is per pair, not per node).</summary>
    public int Count => _all.Count;

    /// <summary>Number of distinct world node groups covered — the count of physical destructible
    /// objects, ignoring how many defs bind each.</summary>
    public int DistinctAnchors => _anchors.Count;

    public IReadOnlyList<Instance> All => _all;

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

    /// <summary>Every pool anchored on exactly this node — normally one, but a node carrying both
    /// the compiler's per-instance def and a reader wildcard's carries two independent pools with
    /// their own HP. Use it with <see cref="Resolve"/>, which names the one a weapon hit reaches;
    /// the others cannot be damaged through the hit path at all. Empty for an ordinary node.</summary>
    public List<Instance> PoolsOn(Node? node)
    {
        var found = new List<Instance>();
        if (node == null)
        {
            return found;
        }
        foreach (var inst in _all)
        {
            if (ReferenceEquals(inst.Anchor, node))
            {
                found.Add(inst);
            }
        }
        return found;
    }

    /// <summary>The destructible instance a struck world node belongs to: climbs the parent chain
    /// from the raycast-hit collider to a registered anchor, nearest compiled anchor first, then
    /// nearest reader (docs/formats/destructibles.md). Null off the destructible chain
    /// entirely.</summary>
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

    /// <summary>One live destructible node group: its authored definition, the world node it
    /// binds to, and the HP that actually falls as it takes fire.</summary>
    public sealed class Instance
    {
        public Instance(AnimDefinition def, Node3D anchor, float maxHealth)
        {
            Def = def;
            Anchor = anchor;
            MaxHealth = maxHealth;
            Health = maxHealth;
        }

        public AnimDefinition Def { get; }
        public Node3D Anchor { get; }
        public float MaxHealth { get; private set; }
        public float Health { get; set; }
        public State Status { get; set; } = State.Healthy;

        /// <summary>The owning side, where the mission data names one, or null where nothing does.
        /// Only a zeppelin record authors one today (docs/org/targeting.md "The team space"); a null
        /// reaches the candidate builder as neutral, so the object is nobody's target.
        /// ⚠ Never write a literal here for an unauthored object. Doing so is what made every
        /// crate and every gasbag hostile to all comers.</summary>
        public int? Team { get; set; }

        /// <summary>Out of play: the pool exists, but its object is not in the world yet — a
        /// mission's <c>deactivated</c> zeppelin before its script wakes it. Refused as a target
        /// while set. ⚠ Not a death state: <see cref="Status"/> stays healthy and the HP stands.</summary>
        public bool Dormant { get; set; }

        /// <summary>The name of the mission entity this pool is a PART of, where one owns it — a
        /// zeppelin record's node name on each of its zones. Null for scenery, which belongs to
        /// nothing. A zone's own anchor is named `gasbag1`/`leng11`, so this is the only identity a
        /// `rating_biases` pattern naming the airship can match
        /// (<see cref="Flight.AiTargetRanking.ObjectiveBiasFor"/>).</summary>
        public string? Owner { get; set; }

        /// <summary>How many of the DAMAGE_SEQUENCE's descending health thresholds this instance
        /// has fallen past — the deepest progressive-damage stage it has escalated to. Only ever
        /// increases (damage escalates, never heals), so a stage effect fires exactly once; a
        /// reset puts it back to 0.</summary>
        public int DamageStage { get; set; }

        /// <summary>Set when the death CHAIN authors the healthy→destroyed swap in a
        /// <c>CALL_ANIMATION</c> target (gate2's <c>blockit2</c>) rather than in this def's own
        /// sequences — so <c>ApplyDeathSwap</c>'s fallback yielded and the swap, wreck colliders,
        /// fireball and flying debris all arrive when the chained call fires. A reset must
        /// stop this def too (its own pending scheduled call, or its already-run motions) and
        /// restore the pose of whatever it moved.</summary>
        public AnimDefinition? ChainedDeathDef { get; set; }

        /// <summary>Every <c>CALL_ANIMATION</c> target this death dispatched onto its own anchor
        /// (docs/formats/destructibles.md), populated at dispatch time so it names only what
        /// actually ran. A reset stops and restores each of these too, or a called def's own
        /// motions can outlive the reset.</summary>
        public HashSet<(AnimDefinition Def, Node3D Anchor)> LocalCallTargets { get; } = new();

        /// <summary>Re-seeds this pool from a mission record — the zeppelin case (M4 F18):
        /// <c>zeppelins.json</c> authors per-part hp (<c>gasbags</c> 80–400,
        /// <c>cannon_health</c> 200) that overrides the def's own <c>HEALTH</c> where present.
        /// Wire-up time only: refuses once the instance has been damaged, so a late re-seed
        /// cannot silently heal a fight in progress.</summary>
        public void Reseed(float maxHealth)
        {
            if (Status != State.Healthy || Health < MaxHealth)
                return;
            MaxHealth = maxHealth;
            Health = maxHealth;
        }
    }
}
