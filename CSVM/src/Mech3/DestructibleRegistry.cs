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
        _anchors.Add(anchor.GetInstanceId());
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

    /// <summary>Drops every instance. Used when a runtime is torn down and rebuilt.</summary>
    public void Clear()
    {
        _byKey.Clear();
        _all.Clear();
        _anchors.Clear();
    }
}
