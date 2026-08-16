using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>An emitter's whole life on one runtime: the keying rule, the start, all four stops, the
/// respawn wipe, the per-frame follow, and a census of what exists. Plumbing and the stop-selector
/// rules: this module's entry in docs/architecture.md.
/// ⚠ Do not collapse the four stops into one parameterised call; every shipped bug in this family
/// was a selector error.
/// Handed already-resolved host and anchor nodes: the effect-template pool is not a keying scheme
/// and stays out of here.
/// </summary>
public sealed class EmitterDirector
{
    // One emitter per (puffer name, emitter node[, owning def]). Assert is idempotent: a
    // re-assert of an already-running emitter is a no-op, matching data that loops PUFFER_STATE.
    // ⚠ Whether def is in the key is `_defScopedKeys`; both cases are load-bearing and covered in
    // this module's docs/architecture.md entry — do not hard-code either choice.
    private readonly Dictionary<(string Name, Node3D Node, AnimDefinition? Def), Entry> _emitters = new();

    // The emitting emitters, each stamped with the INSTANT it started — the runtime
    // batch (one AnimRuntime.Advance pass) that dispatched its `PUFFER_STATE 1`.
    // EndOn reads the stamp; nothing else does. See _instant.
    private readonly List<(IEmitter Emitter, Node3D Node, ulong Started)> _active = new();

    // Each host node's emission point in its own frame (see AnimRuntime.VisualOriginOf) — zero for
    // a node whose origin sits inside its mesh bounds. Computed lazily on the first tick, never at
    // dispatch: the bootstrap dispatches PUFFER_STATE before the world enters the tree, where a
    // GlobalTransform read only returns identity and an error. Local-frame, so it stays valid when
    // a motion drives the node.
    private readonly Dictionary<Node3D, Vector3> _hostOffsets = new();

    private readonly bool _defScopedKeys;

    private readonly bool _debug;

    private readonly Action<string> _count;

    // The current runtime batch, bumped once per Tick — i.e. once per AnimRuntime.Advance, which is
    // exactly the granularity at which the data's own instants exist: an event with no START_TIME is
    // "EVENT_OFFSET 0" and every zero-offset run of events fires in one Advance pass
    // (SequenceRunner). Emitters carry the value they started on so a host deactivation can tell
    // "this emitter has been running" from "this emitter started a moment ago in this very batch".
    private ulong _instant;

    private IEmitterFactory _factory;

    public EmitterDirector(IEmitterFactory factory, bool defScopedKeys, bool debug,
        Action<string> count)
    {
        _factory = factory;
        _defScopedKeys = defScopedKeys;
        _debug = debug;
        _count = count;
    }

    /// <summary>How many emitters this director has actually built (not just started the owning
    /// def). Verification checks this rather than "the def ran" — a started effect
    /// whose factory is retired or whose textures are missing builds nothing and renders
    /// nothing.</summary>
    public int Built { get; private set; }

    /// <summary>Every KNOWN emitter, emitting or not — the module's own answer to "what exists and
    /// what is running", which the `--debug-anim` line and the bootstrap line are both projections
    /// of rather than parallel re-derivations.</summary>
    public IReadOnlyList<EmitterCensusRow> Census
    {
        get
        {
            var rows = new List<EmitterCensusRow>(_emitters.Count);
            foreach (var (key, entry) in _emitters)
            {
                bool valid = entry.Emitter.IsValid;
                rows.Add(new EmitterCensusRow(
                    key.Name,
                    GodotObject.IsInstanceValid(key.Node) ? AnimRuntime.NameOf(key.Node) : "?",
                    entry.Def.AnimName ?? entry.Def.Name,
                    Emitting(entry.Emitter),
                    valid ? entry.Emitter.LiveCount : 0));
            }
            return rows;
        }
    }

    /// <summary>Starts (or revives, or re-homes) the emitter a <c>PUFFER_STATE 1</c> asks for.
    /// Takes the raw event payload rather than a parsed <see cref="PufferState"/> because the common
    /// case is a re-assert that builds nothing: 619 defs sit in an infinite LOOP re-asserting theirs
    /// every frame, and parsing eagerly would allocate for every one of them.</summary>
    public void Assert(string name, Node3D host, AnimDefinition def, Node3D? anchor, AnimData data)
    {
        var key = KeyFor(name, host, def);
        if (_emitters.TryGetValue(key, out var existing))
        {
            // ⚠ A SustainEnd'ed emitter REVIVES on re-assert; reading "stopped" as "still running"
            // collapses the sputter loop to one burst per stage.
            if (!Emitting(existing.Emitter))
                _active.Add((existing.Emitter, host, _instant));
            // ⚠ Ownership must move to the re-asserter (same def, later anchor); every stop
            // resolves through it, and leaving it with the first asserter strands later calls.
            if (existing.Def == def && existing.Anchor != anchor)
                _emitters[key] = existing with { Anchor = anchor };
            return;
        }

        // No pre-built emitter for this key — something had to be made.
        // MaterialCreate (EmitterRenderer.Attach, reached through _factory.Create below) is
        // nested inside this scope and is suppressed by it — its cost is folded into this one.
        using var _ = PerfSample.Scope(PerfSite.EffectPoolMiss);
        if (_factory.Create(PufferState.FromAnimEvent(data), out var miss) is not { } emitter)
        {
            if (miss != null)
                _count(miss);
            return;
        }
        if (_debug && _defScopedKeys)
        {
            foreach (var other in _emitters)
                if (other.Key.Name == name && other.Key.Node == host && other.Value.Def != def)
                    GD.Print($"anim: puffer '{name}' on '{AnimRuntime.NameOf(host)}' builds beside "
                             + $"'{other.Value.Def.AnimName}''s emitter [def {def.AnimName}]");
        }
        _emitters[key] = new Entry(emitter, def, anchor);
        _active.Add((emitter, host, _instant));
        Built++;
    }

    /// <summary>The authored stop: <c>PUFFER_STATE &lt;name&gt; 0</c>. Pauses the emitter on the
    /// event's own key, keeping the entry so a later re-assert revives it.
    /// ⚠ Falls back to the instance's same-named emitter when the key misses; an authored on/off
    /// pair does not always name the same host. Every miss is counted, never left silent.</summary>
    public void End(string name, Node3D host, AnimDefinition def, Node3D? anchor)
    {
        if (_emitters.TryGetValue(KeyFor(name, host, def), out var running))
        {
            running.Emitter.SustainEnd();
            _active.RemoveAll(a => a.Emitter == running.Emitter);
            return;
        }
        var owned = _emitters
            .Where(kv => kv.Key.Name == name && kv.Value.Def == def && kv.Value.Anchor == anchor)
            .Select(kv => kv.Value.Emitter)
            .ToList();
        foreach (var stray in owned)
        {
            stray.SustainEnd();
            _active.RemoveAll(a => a.Emitter == stray);
        }
        _count(owned.Count > 0
            ? $"PufferState(stop matched by owner, not host: {name})"
            : $"PufferState(stop reached no emitter: {name})");
        if (_debug)
            GD.Print($"anim: PUFFER_STATE 0 '{name}' missed its host '{AnimRuntime.NameOf(host)}' "
                     + $"[def {def.AnimName}] — "
                     + (owned.Count > 0
                         ? $"stopped {owned.Count} emitter(s) this instance owns instead"
                         : "this instance owns no emitter of that name"));
    }

    /// <summary>Ends emission for every emitter on <paramref name="root"/> or its subtree — what
    /// <c>OBJECT_ACTIVE_STATE false</c> means for emitters. The same-instant carve-out, the
    /// entries-stay revival and the visibility caveat: this module's docs/architecture.md entry.
    /// ⚠ Only an explicit host deactivation counts; visibility is not the test, since particles go
    /// TopLevel into world space.</summary>
    public void EndOn(Node3D root, bool sparingSameInstant = true)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (emitter, node, started) = _active[i];
            if (!emitter.IsValid || !GodotObject.IsInstanceValid(node))
            {
                _active.RemoveAt(i);
                continue;
            }
            if (node != root && !root.IsAncestorOf(node))
                continue;
            if (sparingSameInstant && started == _instant)
            {
                _count("ObjectActiveState(spared an emitter started this instant)");
                if (_debug)
                    GD.Print($"anim: host '{AnimRuntime.NameOf(node)}' deactivated in the instant its "
                             + "emitter started — left emitting");
                continue;
            }
            emitter.SustainEnd();
            _active.RemoveAt(i);
            if (_debug)
                GD.Print($"anim: host '{AnimRuntime.NameOf(node)}' deactivated — emitter stopped "
                         + $"({_active.Count} still emitting)");
        }
    }

    /// <summary>Pauses what an ending instance owns, KEEPING the entries — the stop for a def
    /// carrying an ACTIVE_STATE 1 and neither authored stop (the torpedo's ground fire, C1's refuel
    /// tanks). Emission ends rather than the emitter being torn down, so live particles
    /// finish their authored LIFETIME_RANGE — the fire fades over its last 3–4 s instead of popping
    /// out — and a replay on this same pool slot revives the entry.</summary>
    public void EndFor(AnimDefinition def, Node3D? anchor)
    {
        if (_emitters.Count == 0)
            return;
        foreach (var entry in _emitters.Values.Where(v => v.Def == def && v.Anchor == anchor).ToList())
        {
            entry.Emitter.SustainEnd();
            _active.RemoveAll(a => a.Emitter == entry.Emitter);
        }
    }

    /// <summary>Pauses what a STOPPED instance owns and forgets the entries, so nothing of the
    /// definition can be revived after it. Same selector as <see cref="EndFor"/>, different
    /// disposition — the pair is why the four stops do not reconcile into one.</summary>
    public void Discard(AnimDefinition def, Node3D? anchor)
    {
        var keys = _emitters
            .Where(kv => kv.Value.Def == def && kv.Value.Anchor == anchor)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keys)
        {
            var emitter = _emitters[key].Emitter;
            emitter.SustainEnd();
            _active.RemoveAll(a => a.Emitter == emitter);
            _emitters.Remove(key);
        }
    }

    /// <summary>Destroys everything — the per-player crash rig's respawn. Particles are dropped at
    /// once rather than left to finish, because respawn is immediate and must not leave fire
    /// burning, and the emitters are released rather than kept, because the next crash builds fresh
    /// ones and they must not accumulate. Wipes the whole pool, not just what live instances own: an
    /// effect def whose sequence has already ended (`large_10sec_fire`, whose 10 s particles outlive
    /// its instance) owns no live instance yet is still emitting.</summary>
    public void Reset()
    {
        foreach (var entry in _emitters.Values)
        {
            entry.Emitter.SustainEnd();
            entry.Emitter.Clear();
            entry.Emitter.Destroy();
        }
        _emitters.Clear();
        _active.Clear();
        _hostOffsets.Clear();
    }

    /// <summary>Drives every emitting emitter from its host node's current world pose. Emitters
    /// follow moving nodes (the train's smokestack travels the whole track loop), so this is per
    /// frame.</summary>
    public void Tick(float dt)
    {
        // A new runtime batch begins here — everything the instances dispatch below this point
        // shares one instant, which is what EndOn's same-instant carve-out is keyed on.
        _instant++;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (emitter, node, _) = _active[i];
            if (!emitter.IsValid || !GodotObject.IsInstanceValid(node))
            {
                _active.RemoveAt(i);
                continue;
            }
            var xform = node.GlobalTransform;
            var offset = HostOffsetOf(node, xform);
            emitter.SustainAt(offset == Vector3.Zero ? xform.Origin : xform * offset, xform.Basis, dt);
        }
    }

    /// <summary>Swaps in the factory a runtime has once its texture archive is released. Nothing
    /// already built is disturbed; only further requests change answer, from "here is an emitter" to
    /// a named, once-warned miss.</summary>
    public void RetireFactory() => _factory = new SpentEmitterFactory();

    private (string, Node3D, AnimDefinition?) KeyFor(string name, Node3D host, AnimDefinition def) =>
        (name, host, _defScopedKeys ? def : null);

    private bool Emitting(IEmitter emitter) => _active.Any(a => a.Emitter == emitter);

    // The host's emission point in its own frame, cached per node. Zero — and the emission
    // point exactly the node origin, byte-identical with the pre-cache behaviour — for every node
    // whose origin sits inside its mesh bounds; the offset to the bounds centre for
    // absolute-modelled world subtrees, whose origin is the map corner. Local-frame, so
    // a motion-driven host carries its emission point along.
    private Vector3 HostOffsetOf(Node3D host, in Transform3D xform)
    {
        if (_hostOffsets.TryGetValue(host, out var offset))
            return offset;
        var visual = AnimRuntime.VisualOriginOf(host);
        offset = visual == xform.Origin ? Vector3.Zero : xform.AffineInverse() * visual;
        _hostOffsets[host] = offset;
        if (_debug && offset != Vector3.Zero)
            GD.Print($"anim: puffer host '{AnimRuntime.NameOf(host)}' origin {xform.Origin} is outside its mesh "
                     + $"bounds — emitting at {visual}");
        return offset;
    }

    private readonly record struct Entry(IEmitter Emitter, AnimDefinition Def, Node3D? Anchor);
}
