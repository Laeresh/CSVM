using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>An emitter's whole life on one runtime: the keying rule, the start, all four stops, the
/// respawn wipe, the per-frame follow, and a census of what exists.
///
/// <para>The four stops stay four. They vary on two independent axes — SELECTOR (key / host subtree /
/// owning instance) × DISPOSITION (pause-revivable / pause-and-forget) — and the axes really are
/// independent: <see cref="EndFor"/> and <see cref="Discard"/> share a selector and differ only in
/// disposition. One named method per selector, disposition implied by the name. ⚠ Do not collapse
/// them into one parameterised stop: every shipped bug in this family was a SELECTOR error, so the
/// selectors are the distinctions worth naming at the call
/// site.</para>
///
/// <para>Handed already-resolved host and anchor nodes: the effect-template pool is NOT a keying
/// scheme and stays out of here (`SlotOf`/`NextPooledAnchors`/`PlaceTemplateAt` remain on
/// <see cref="AnimRuntime"/>). Distinct emitters per pooled call fall out of each slot's host being
/// a different node.</para></summary>
public sealed class EmitterDirector
{
    /// <summary>One emitter per (puffer name, emitter node[, owning def]). Definitions re-assert
    /// their PUFFER_STATE every loop iteration — C1's waterfall is [PufferState ×3, Loop{-1}] — so
    /// the start has to be idempotent: re-asserting an already-running emitter must be a no-op, not
    /// a second emitter. Safe by the data: all 2,774 compiled PUFFER_STATE events reference only
    /// puffers their own def declares (measured install-wide).
    ///
    /// <para>⚠ The def is in the key only where <c>defScopedKeys</c> says so, and both cases are
    /// measured. REQUIRED on the world-effects runtime: two effect defs can declare same-named
    /// puffers on one host — the two damage-stage sputters both call theirs `black_smoke`, and a
    /// shared key let the stage-1 smoke emitter mask the stage-2 fire build. FORBIDDEN on the world
    /// runtime: C5's six `m_crane_go(#N)` twins all name-resolve `man_spark` onto one node, and
    /// def-scoped keys there stacked six spark emitters on it and moved the `c5-city-night`
    /// golden — the collapsed key doubles as the de-dup for that name-resolution artifact.</para>
    ///
    /// <para>The value carries the owning (def, anchor) so a stop can reach exactly the emitters a
    /// stopped instance created.</para></summary>
    private readonly Dictionary<(string Name, Node3D Node, AnimDefinition? Def), Entry> _emitters = new();

    /// <summary>The emitting emitters, each stamped with the INSTANT it started — the runtime
    /// batch (one <see cref="AnimRuntime.Advance"/> pass) that dispatched its <c>PUFFER_STATE 1</c>.
    /// <see cref="EndOn"/> reads the stamp; nothing else does. See <see cref="_instant"/>.</summary>
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
            // Re-asserting a RUNNING emitter is a no-op (the loop idiom). A SustainEnd'ed one
            // REVIVES instead: the damage-stage `puffit` loop cycles ACTIVE_STATE 0/1 on a 50%
            // dice every pass, and reading "stopped" as "still running" collapsed the authored
            // sputter to at most one burst per stage.
            if (!Emitting(existing.Emitter))
                _active.Add((existing.Emitter, host, _instant));
            // The re-asserting instance TAKES OWNERSHIP (same def, later anchor). Ownership is what
            // every stop resolves through — End, the TTL sweep and EndFor all ask "which emitters
            // does (def, anchor) own?" — so an emitter left attributed to the FIRST asserter
            // outlives every later one: three torpedoes' `fire_n_smoke` share a host (their pooled
            // `torp_effects` copies all resolve through the def's own node index), so calls 2 and 3
            // revived call 1's emitter, and when they ended they owned nothing to stop. Guarded on
            // the def: a name colliding across two defs on an un-def-scoped runtime is the "builds
            // beside" case below, not one def's own pool, and must not change hands.
            if (existing.Def == def && existing.Anchor != anchor)
                _emitters[key] = existing with { Anchor = anchor };
            return;
        }

        // PLAN-perf-hitches C9: no pre-built emitter for this key — something had to be made.
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
    ///
    /// <para>⚠ Falls back to the same-named emitter this INSTANCE owns when the key misses. The two
    /// halves of an authored on/off pair do not always name the same host: measured on a C1 crash,
    /// all four defs whose stop missed start the emitter at their OWN template root and
    /// stop it with no AT_NODE at all, which falls to the anchor — `large_10sec_fire` ON at
    /// `fire_here` / OFF at `destroyed`, `large_fireball` ON at `flame_ball_01` / OFF at `healthy`,
    /// likewise `small_yellow_sparks` and `large_black_smokeball`. Two keys, one emitter: the stop
    /// looked up a key nothing ever wrote and returned silently. Narrowed on the name as well as the
    /// owner, so a def running several emitters (`he_trails`' five spurt columns) still stops only
    /// the one the event names. Every miss is counted out loud — all four were masked by a backstop
    /// that happened to fire at the authored moment, and a future def without that luck would just
    /// leak.</para></summary>
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

    /// <summary>Ends emission for every emitter hosted on <paramref name="root"/> or inside its
    /// subtree — what an <c>OBJECT_ACTIVE_STATE false</c> means for emitters, and the only AUTHORED
    /// stop a stop-less PUFFER_STATE has. `he_trails`' five spurt columns carry no
    /// ACTIVE_STATE 0; what the data turns off is the HOST — without honouring that, a rocket's
    /// smoke ends only at the 32 s runtime TTL or when the next rocket restarts the def.
    ///
    /// <para>⚠ Visibility is NOT the test, unlike the light sweep. The world-effects stage keeps
    /// every template root hidden deliberately and its emitters still show, because particles go
    /// TopLevel into world space — gating emission on <c>IsVisibleInTree</c> would silence every
    /// staged impact effect. Only an explicit deactivation of the host counts.</para>
    ///
    /// <para>The entry stays, so a later <c>PUFFER_STATE 1</c> revives it — the sputter loop cycles
    /// 0/1 forever and relies on that. Leaving <see cref="_active"/> is not optional though:
    /// <see cref="IEmitter.SustainAt"/> re-arms emission on its own, so a still-ticked emitter would
    /// resume on the very next frame.</para>
    ///
    /// <para>⚠ It does NOT reach an emitter that started in this same instant, because
    /// the data writes both halves of a one-tick idiom and means only the second: the four splash
    /// definitions activate <c>sp_1</c>, call an emitter definition onto it, and switch it off again
    /// with no START_TIME anywhere, while the callee authors a 0.5 s run. Censused install-wide
    /// (`analysis/bl-229-emitter-host-deactivation/`): 414 activate/emit/deactivate pairs, and the
    /// split is total — the 32 same-instant ones are those four shapes, every one of which authors a
    /// run this stop would cut to nothing, and the other 382 sit a median 3.5 s later, which is the
    /// whole population this stop exists for (`m_build*`'s debris trails end when their
    /// flying part is switched off after its 5 s OBJECT_MOTION). No pair sits in between.
    /// <paramref name="sparingSameInstant"/> is false only on the RESET_STATE path, where every op
    /// is a base state and "last write wins" is the whole semantics.</para></summary>
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

    /// <summary>The host's emission point in its own frame, cached per node. Zero — and the emission
    /// point exactly the node origin, byte-identical with the pre-cache behaviour — for every node
    /// whose origin sits inside its mesh bounds; the offset to the bounds centre for
    /// absolute-modelled world subtrees, whose origin is the map corner. Local-frame, so
    /// a motion-driven host carries its emission point along.</summary>
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
