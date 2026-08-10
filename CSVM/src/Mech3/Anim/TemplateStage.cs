using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// The effect-template stage as one module: pool-slot arithmetic, template placement, the
/// "which copy is mine" identity rule and the reveal/retire/sweep ritual that <c>PlayEffectAt</c>
/// and the <c>CALL_ANIMATION</c> arm both perform. Generic over
/// the node type like <see cref="NameResolver{TNode}"/>: the engine hands adapter hooks over at
/// construction, together with the three policy flags that say what kind of stage this is
/// (<see cref="Pooled"/>, <see cref="Shown"/>, <see cref="Places"/>), and the runtime-dependent
/// hooks at the handover (<see cref="Wire"/> — they
/// cannot be construction arguments, because the factory that builds the stage exists before any
/// resolver or runtime does), so slot wrap, modulo fallback, recycle counting and caller-slot
/// stickiness are assertable off-engine against a token node type (<c>CSVM.Tests</c>).
///
/// <para>The three flags are sealed at construction and have
/// no setter anywhere: the stage is built by whoever knows what role its runtime plays
/// (<c>WorldEffectsFactory</c> for the world-effects and crash rigs, <c>WorldSession</c> for the
/// world runtime) and handed to <c>AnimRuntime</c> as a constructor argument, so there is no
/// post-seal write to get wrong.</para>
///
/// <para>⚠ Do not move slot arithmetic into <see cref="EmitterDirector"/>: the director is handed
/// already-resolved host and anchor nodes, and the pool must never become a third emitter-keying
/// scheme. That property holds by construction: the stage resolves roots and hands them out;
/// nothing here keys an emitter.</para>
///
/// <para>⚠ Root resolution goes through the <c>findAll</c> hook, never <c>Anchors</c> — with one
/// deliberate asymmetry. Per-EVENT paths (<see cref="RootsFor"/>, <see cref="IsAt"/>, and the
/// resolver's own-root tier this class feeds) run on poll loops every frame, and
/// <c>NameResolver.Anchors</c> is the one census-recording resolution call, which must not be
/// re-entered there. Per-CALL paths (<see cref="TakeNextSlot"/>, <see cref="RootsOf"/>) may use
/// the <c>anchors</c> hook: they run once per <c>PlayEffectAt</c>/hide decision and the census
/// records once per def identity.</para>
/// </summary>
public sealed class TemplateStage<TNode>
    where TNode : class
{
    /// <summary>The squared-metres tolerance below which a
    /// placed template already sits "at" a call site — 0.5 m. Separates "the data is re-issuing
    /// the same call from a poll loop" (leave the live instance alone) from "a second explosion
    /// needs this template somewhere else" (relocate and restart). Metre-scale on purpose:
    /// distinct impacts are metres apart, and an exact compare on kilometre-scale world
    /// coordinates would call a float round-trip a move. One named constant so the two distance
    /// tests that share it (<c>TemplateIsAt</c> and the <c>CallAnimation</c>
    /// arm's <c>libraryCopy</c> test) cannot drift apart.</summary>
    public const float MoveToleranceSq = 0.25f;

    private readonly IEqualityComparer<TNode> _identity;

    // The raw ancestry walk (parent chain to the first pool-slot mark, -1 = none). Unmemoized —
    // the memo below is this class's, so the cache behaviour is assertable off-engine.
    private readonly Func<TNode, int> _slotMarkOf;

    private readonly Func<TNode?, bool> _isValid;

    private readonly Func<TNode, Transform3D> _transformOf;

    // The one placement write: TopLevel + GlobalTransform on the engine (see PlaceOn's comment).
    private readonly Action<TNode, Transform3D> _placeAt;

    // The one visibility write. Only the template ROOT's own flag is touched; what shows inside it
    // stays the data's decision (see Reveal).
    private readonly Action<TNode, bool> _setVisible;

    private readonly Action<string> _print;

    private readonly Func<bool> _debug;

    // Which slot each PlayEffectAt call takes next, per template ROOT name (def.Name), not per
    // anim name: two effect defs that anchor on the same root must not both be handed slot 0 and
    // collapse onto one copy again. Advances once per call and wraps, so a burst longer than the
    // pool recycles its oldest slot — the shared-template behaviour, but only at the wrap.
    private readonly Dictionary<string, int> _poolCursor = new(StringComparer.OrdinalIgnoreCase);

    // Node → its pool slot (-1 = outside the pool), memoized on the same terms as the resolver's
    // own FindAll cache: slot containers are built before Bind and nothing is ever reparented.
    // IsAt asks per event on a poll loop, so the ancestor walk must not be repeated.
    private readonly Dictionary<TNode, int> _slotOfNode;

    // Named once per effect, not per wrap: a pool that recycles a slot whose instance is still
    // live is the pool being too small for the concurrency, which is a tuning fact worth seeing
    // and not an error. Recycles counts every one of them.
    private readonly HashSet<string> _recyclesLogged = new(StringComparer.OrdinalIgnoreCase);

    // Template-root NAMEs a placing stage must never relocate: an authored NAME that is a live
    // scene node rather than a staged template copy. The crash defs' player damage/reset family is
    // all authored NAME=player_pfighter — on ten airframes that resolves to nothing, but on the
    // Devastator it IS the aircraft's model root, and placing it TopLevel-pins the whole plane at
    // the call site while the FlightController flies on with only the crash rig's debris in tow.
    // Sealed at construction with the flags: which names are airframe-scoped is catalogue
    // knowledge the stage's builder has and the runtime must not rediscover per call.
    private readonly HashSet<string> _placeExempt;

    // A relocating CALL_ANIMATION on a pooled runtime whose anchor sits in no slot
    // container — the crash rig's pdpN panels, its wreck pieces, prop1: plane nodes, never pool
    // copies — claims a slot per (template root, anchor) here on its first call and keeps it, so
    // each panel's tear owns its own template copy while every other panel's burst flies on.
    // Keyed per root with its own cursor, because one root's callers are a SUBSET of all damage
    // anchors: a single shared numbering would fold two of its callers onto one copy while other
    // copies idle.
    private readonly Dictionary<string, Dictionary<TNode, int>> _callerSlots =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _callerSlotCursor = new(StringComparer.OrdinalIgnoreCase);

    // Effects whose instance has ended but whose template copy is still revealed — something is
    // still animating it, or another def is still playing on it (see RetireWhenIdle, and
    // Sweep for when they drain).
    private readonly List<(AnimDefinition Def, TNode? Anchor)> _hidesPending = new();

    // ---- runtime hooks (Wire) ----
    private Func<string, TNode?, List<TNode>> _findAll = null!;

    private Func<AnimDefinition, List<TNode?>> _anchors = null!;

    private Func<AnimDefinition, TNode?, bool> _isLive = null!;

    private Func<IEnumerable<(AnimDefinition Def, TNode? Anchor)>> _liveInstances = null!;

    private Func<AnimDefinition, bool> _levels = null!;

    private Func<IReadOnlyList<TNode?>, bool> _stillAnimated = null!;

    private Func<TNode, string> _nameOf = null!;

    private Action<TNode> _indexSubtree = null!;

    private Action _clearFindCache = null!;

    private Action<TNode> _applyResetStates = null!;

    public TemplateStage(
        IEqualityComparer<TNode> identity,
        Func<TNode, int> slotMarkOf,
        Func<TNode?, bool> isValid,
        Func<TNode, Transform3D> transformOf,
        Action<TNode, Transform3D> placeAt,
        Action<TNode, bool> setVisible,
        Action<string> print,
        Func<bool> debug,
        bool pooled = false,
        bool shown = false,
        bool placesCalled = false,
        IEnumerable<string>? placeExempt = null)
    {
        Pooled = pooled;
        Shown = shown;
        Places = placesCalled;
        _placeExempt = placeExempt == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(placeExempt, StringComparer.OrdinalIgnoreCase);
        _identity = identity;
        _slotMarkOf = slotMarkOf;
        _isValid = isValid;
        _transformOf = transformOf;
        _placeAt = placeAt;
        _setVisible = setVisible;
        _print = print;
        _debug = debug;
        _slotOfNode = new Dictionary<TNode, int>(identity);
    }

    /// <summary>Whether the staged effect templates exist in more than one copy, one per pool slot
    /// (<c>AnimRuntime.PoolSlotMeta</c>), so each call takes the next copy instead of relocating the
    /// one shared original. Set on the WORLD-EFFECTS runtime and the per-player crash rig:
    /// two rockets landing a second apart then keep their own trails at their own sites,
    /// where a single copy made the first blast's trails jump to the second's. Everything
    /// template-shaped becomes slot-scoped under it — which copy a call places
    /// (<see cref="PlaceAt"/>), reveals (<see cref="Reveal"/>), tests for a move
    /// (<see cref="IsAt"/>) and resolves its own node names in (the resolver's own-root tier, fed by
    /// <see cref="RootsFor"/>) — all keyed off the slot the call's anchor lives in, so a nested
    /// CALL_ANIMATION stays inside its caller's slot.
    ///
    /// <para>⚠ Off on the WORLD runtime, deliberately, and this is not a keying scheme layered on
    /// the puffer key: emitters there stay keyed by the collapsed <c>(name, host)</c> (see
    /// <c>AnimRuntime.DefScopedPufferKeys</c> and <see cref="EmitterDirector"/>'s keying remark) —
    /// the world's templates are the world's own nodes, not staged copies, and there is nothing to
    /// pool. A pooled call gets distinct emitters for free, because each slot's host node is a
    /// different node.</para></summary>
    public bool Pooled { get; }

    /// <summary>Whether this runtime's templates are staged hidden, so the stage reveals a root
    /// while an effect plays on it and hides it again when that effect is over. Set on the
    /// world-effects runtime, whose templates are staged hidden so nothing renders ambiently at the
    /// stage origin: without it the templates' own MESHES — the rocket's per-type explosion rings,
    /// the fireball facades, the splash models — never draw, only their puffers do. Only the
    /// root's own visibility is touched; what shows inside it stays the data's decision (the rings
    /// are reset INACTIVE or opacity-OFF and their defs turn them on). Off everywhere else — the
    /// crash rig included, whose templates hide by their own reset states — where the stage is
    /// visible anyway.</summary>
    public bool Shown { get; }

    /// <summary>Whether a CALL_ANIMATION relocates its callee's effect-template root onto the call
    /// site (<see cref="PlaceAt"/>). Off on the world runtime — the ambient world boot must stay
    /// byte-identical, and today's retarget only re-scopes name resolution. On for the
    /// world-effects runtime, the per-player crash rig and the animation debugger's stage, so a
    /// placeless effect template plays where it is staged instead of at its gamez origin.</summary>
    public bool Places { get; }

    /// <summary>Every wrap onto a still-occupied copy, both flavours (<see cref="TakeNextSlot"/>'s
    /// cursor recycling a live slot, <see cref="AssignCallerSlot"/>'s claims outnumbering the
    /// staged copies) — the shared-template collapse, bounded to the wrap instead of every call.
    /// Zero on the goldens; what <c>effect_pools.json</c> is sized against.</summary>
    public int Recycles { get; private set; }

    /// <summary>The runtime-dependent hooks, wired at the handover rather than construction:
    /// the stage is built before any resolver exists, and the resolver's own
    /// <c>ownRootsOf</c> hook is <see cref="RootsFor"/> — both directions are delegates.</summary>
    public void Wire(
        Func<string, TNode?, List<TNode>> findAll,
        Func<AnimDefinition, List<TNode?>> anchors,
        Func<AnimDefinition, TNode?, bool> isLive,
        Func<IEnumerable<(AnimDefinition Def, TNode? Anchor)>> liveInstances,
        Func<AnimDefinition, bool> levels,
        Func<IReadOnlyList<TNode?>, bool> stillAnimated,
        Func<TNode, string> nameOf,
        Action<TNode> indexSubtree,
        Action clearFindCache,
        Action<TNode> applyResetStates)
    {
        _findAll = findAll;
        _anchors = anchors;
        _isLive = isLive;
        _liveInstances = liveInstances;
        _levels = levels;
        _stillAnimated = stillAnimated;
        _nameOf = nameOf;
        _indexSubtree = indexSubtree;
        _clearFindCache = clearFindCache;
        _applyResetStates = applyResetStates;
    }

    /// <summary>The pool slot a node sits in — the nearest ancestor carrying the slot mark — or
    /// -1 for anything outside the pool (every node on a non-pooled runtime, and the runtime's
    /// own logic nodes). This is what makes "my slot" a property of the CALL rather than of the
    /// effect: a nested CALL_ANIMATION anchors on a node inside its caller's copy, so its own
    /// template resolves to the copy beside it.</summary>
    public int SlotOf(TNode? node)
    {
        if (node == null || !_isValid(node))
            return -1;
        if (_slotOfNode.TryGetValue(node, out int cached))
            return cached;
        return _slotOfNode[node] = _slotMarkOf(node);
    }

    /// <summary>Claims a pool slot for a relocating CALL_ANIMATION whose anchor sits in no slot
    /// container — the crash rig's damage-stage calls, anchored on the plane's own nodes
    /// (every `pdpanelN` tear CALLs `gimmeflakes` onto its own `pdpN`; a single
    /// shared `planeflakes` root would teleport to each new tear, restarting the previous panel's
    /// burst mid-flight). Sticky per (template root, anchor), so a re-call from the same anchor
    /// restarts ITS OWN copy and never a sibling's. More anchors than staged copies wrap through
    /// <see cref="RootsFor"/>'s modulo — the effects pool's own exhaustion behaviour — counted in
    /// <see cref="Recycles"/> and named once. No-op off the pool, for a template staged
    /// single-copy (those never ask for a slot), and for anchors already inside a slot (the
    /// world-effects runtime's calls, whose anchors are the pooled copies themselves).</summary>
    public void AssignCallerSlot(AnimDefinition callee, TNode anchor)
    {
        if (!Pooled || string.IsNullOrEmpty(callee.Name) || SlotOf(anchor) >= 0
            || _placeExempt.Contains(callee.Name))
            return;
        if (!_callerSlots.TryGetValue(callee.Name, out var byAnchor))
            _callerSlots[callee.Name] = byAnchor = new Dictionary<TNode, int>(_identity);
        if (byAnchor.ContainsKey(anchor))
            return;
        int copies = _findAll(callee.Name, null).Count;
        if (copies <= 1)
            return;
        int next = _callerSlotCursor.TryGetValue(callee.Name, out int cur) ? cur : 0;
        _callerSlotCursor[callee.Name] = next + 1;
        byAnchor[anchor] = next;
        // The per-anchor claim, log-visible beside the retarget line it pairs with — a debug
        // run reads this to confirm a new tear takes a fresh copy instead of the live one.
        if (_debug())
        {
            _print($"anim: caller slot {next % copies} of '{callee.Name}' ({copies} cop(ies)) "
                   + $"claimed by '{_nameOf(anchor)}'");
        }
        if (next >= copies)
        {
            Recycles++;
            if (_recyclesLogged.Add(callee.AnimName ?? callee.Name))
            {
                _print($"anim: caller pool for '{callee.AnimName ?? callee.Name}' wrapped — "
                       + $"{next + 1} call anchor(s) over {copies} staged cop(ies) share again");
            }
        }
    }

    /// <summary>The copies of a definition's own template root(s) that belong with
    /// <paramref name="inSlotOf"/> — the one pool slot that call is running in. Off the pool (or
    /// for a def whose root is staged in a single copy, like the shared gun family's, which is
    /// relocated on purpose) this is every match. Also the resolver's
    /// <c>ownRootsOf</c> hook (its middle tier — wired at the handover), which is the one route
    /// the pool takes into the resolver: it sees the resolved root list, never the slot
    /// arithmetic. Resolved through the <c>findAll</c> hook rather than <c>Anchors</c>: the
    /// callers run per event, and Anchors records a per-definition anchoring census that must not
    /// be re-entered.
    ///
    /// <para>Pool sizes are per ROOT (<c>data/effect_pools.json</c>), so a callee can be staged
    /// shallower than its caller's slot — a slot-5 blast calling a template with only 4 copies.
    /// That picks one copy by modulo rather than falling back to "all of them": every branch here
    /// must return ONE call's copies, or a nested call would drive every slot's nodes at once,
    /// which is the collapse the pool exists to end.</para></summary>
    public List<TNode> RootsFor(AnimDefinition callee, TNode? inSlotOf)
    {
        var roots = _findAll(callee.Name, null);
        if (!Pooled || roots.Count <= 1)
            return roots;
        int slot = SlotOf(inSlotOf);
        if (slot < 0)
            slot = AssignedCallerSlot(callee, inSlotOf); // a crash-rig call anchor's claim
        if (slot < 0)
            return roots;
        var staged = roots.Select(SlotOf).Where(s => s >= 0).Distinct().OrderBy(s => s).ToList();
        if (staged.Count == 0)
            return roots;
        int want = staged.Contains(slot) ? slot : staged[((slot % staged.Count) + staged.Count) % staged.Count];
        var mine = roots.Where(r => SlotOf(r) == want).ToList();
        return mine.Count > 0 ? mine : roots;
    }

    /// <summary>Takes the next pool slot for one <c>PlayEffectAt</c> call and returns that
    /// slot's copy of the definition's template root(s) — the anchor the new instance runs on, so
    /// two overlapping calls to one effect animate two different copies at two different sites.
    /// Empty (and unpooled) runtimes return every anchor unchanged.
    ///
    /// <para>The cursor wraps: a burst deeper than the pool recycles its oldest slot, relocating
    /// and restarting a copy that may still be live — the shared-template collapse, bounded to
    /// the wrap instead of every call. That is the exhaustion signal, so it is counted and named
    /// once (<see cref="Recycles"/>).</para></summary>
    public List<TNode?> TakeNextSlot(AnimDefinition def)
    {
        var anchors = _anchors(def).Where(a => a != null && _isValid(a)).ToList();
        if (!Pooled || anchors.Count <= 1 || string.IsNullOrEmpty(def.Name))
            return anchors;
        // The distinct slots this def's roots are staged in, in slot order — its pool size. A
        // root staged shared (gun family) has one slot and never cycles.
        var slots = anchors.Select(SlotOf).Where(s => s >= 0).Distinct().OrderBy(s => s).ToList();
        if (slots.Count <= 1)
            return anchors;
        int next = _poolCursor.TryGetValue(def.Name, out int cur) ? cur : 0;
        _poolCursor[def.Name] = (next + 1) % slots.Count;
        int slot = slots[next % slots.Count];
        var mine = anchors.Where(a => SlotOf(a) == slot).ToList();
        if (mine.Count == 0)
            return anchors;
        if (_isLive(def, mine[0]))
        {
            Recycles++;
            if (_recyclesLogged.Add(def.AnimName ?? def.Name))
            {
                _print($"anim: effect pool for '{def.AnimName ?? def.Name}' recycled slot {slot} of "
                       + $"{slots.Count} while it was still live — overlapping calls beyond the "
                       + "pool size share a copy again");
            }
        }
        return mine.Cast<TNode?>().ToList();
    }

    /// <summary>Moves an effect template's own root(s) to a call site so its puffers — which
    /// ride that root (<c>yellow_spark_01</c>, <c>fly_trailN</c>, …), NOT the caller's anchor —
    /// emit there instead of at the template's gamez origin. This is the template-instancing the
    /// original does by copying the template mesh per call; on a pooled runtime
    /// (<see cref="Pooled"/>) it moves only the copy in the CALL'S OWN slot, so a second
    /// call elsewhere leaves the first blast's copy where it is. Unpooled, the one shared template
    /// is relocated and overlapping calls collapse onto the last site. Only the world position is
    /// set — the puffers key off the host origin — and the offset is applied in the site's own
    /// frame.</summary>
    public void PlaceAt(AnimDefinition callee, TNode site, Vector3 offset)
    {
        // An airframe-scoped NAME is a live scene node, never a staged template — placing it
        // would TopLevel-pin the aircraft itself (see _placeExempt). Resolution is untouched:
        // the callee's own node ops still run on that root, exactly as authored.
        if (!string.IsNullOrEmpty(callee.Name) && _placeExempt.Contains(callee.Name))
            return;
        var xf = _transformOf(site);
        PlaceOn(RootsFor(callee, site), xf.Origin + xf.Basis * offset, _levels(callee));
    }

    /// <summary>Places the given root(s) at an absolute world origin. The engine write behind the
    /// <c>placeAt</c> hook world-stages each root once placed: <c>TopLevel</c> decouples it from a
    /// moving/rotating caller (a flying plane's pdpN panel) so it holds this pose instead of being
    /// dragged and re-yawed every later frame the caller moves — the same plane-parented-effect
    /// trap Puffer's TrailAdvance/Burst guards against. A caller that calls again later
    /// (or the pooled-copy path re-placing a recycled slot) simply overwrites this transform.</summary>
    public void PlaceOn(IEnumerable<TNode?> roots, Vector3 origin, bool level = false)
    {
        foreach (var root in roots)
        {
            if (root == null || !_isValid(root))
                continue;
            var xf = _transformOf(root);
            xf.Origin = origin;
            // A named crash-def template (the water splash's flat rings/spray column, the
            // dirt burst's dust plane) inherits crashRoot's rotation otherwise — the plane's
            // attitude at impact, not the struck surface. Basis only; the origin above still places
            // at the call site. Named, not blanket (see LevelPlacedTemplateNames's own ⚠): a crash
            // debris template's scatter is authored in ITS OWN frame and must keep co-rotating with
            // the impact attitude.
            if (level)
                xf.Basis = Basis.Identity;
            _placeAt(root, xf);
        }
    }

    /// <summary>Whether a placed template already sits where a call wants it — within
    /// <see cref="MoveToleranceSq"/>. Separates "the data is re-issuing the same call from a poll
    /// loop" (leave the live instance alone) from "a second explosion needs this template
    /// somewhere else" (relocate and restart). Resolves the roots the way the resolver's own-root
    /// tier does — this runs per event, and <c>Anchors</c> would re-enter its per-definition
    /// anchoring census on every frame of a poll loop. Pooled, the question is asked of the copy
    /// in the CALL's slot: another slot's copy sitting at another blast site is not this call
    /// being re-issued from somewhere new.</summary>
    public bool IsAt(AnimDefinition callee, TNode site, Vector3 offset)
    {
        // A place-exempt callee is never moved (PlaceAt), so it is never "moved away" either —
        // reporting a distance here would make every poll-idiom re-call restart it while live.
        if (string.IsNullOrEmpty(callee.Name) || _placeExempt.Contains(callee.Name))
            return true;
        var xf = _transformOf(site);
        var want = xf.Origin + xf.Basis * offset;
        foreach (var root in RootsFor(callee, site))
            if (_isValid(root) && _transformOf(root).Origin.DistanceSquaredTo(want) > MoveToleranceSq)
                return false;
        return true;
    }

    /// <summary>The template root(s) a definition's visibility is written through — the call's own
    /// pooled copy when this runtime pools them and the anchor sits in a slot, else the def's
    /// anchors.</summary>
    public IReadOnlyList<TNode?> RootsOf(AnimDefinition def, TNode? anchor) =>
        Pooled && (SlotOf(anchor) >= 0 || AssignedCallerSlot(def, anchor) >= 0)
            ? RootsFor(def, anchor).Cast<TNode?>().ToList()
            : _anchors(def);

    /// <summary>Whether another LIVE instance is playing on the same template root(s) — the reason
    /// a hide cannot be a private decision. Caller and callee routinely share one root: the rear
    /// muzzle flash's own def is two instantaneous events plus a <c>CALL_ANIMATION</c> back onto
    /// <c>rear_flash_control</c>, so it finishes on the tick it starts, and hiding on its own finish
    /// blanked the callee's flash mesh that had just been revealed there (measured — the mesh went
    /// from 1/2 to 0/2 the moment the hide landed, with nothing else changed).</summary>
    public bool SharedWithLiveInstance(AnimDefinition def, TNode? anchor,
        IReadOnlyList<TNode?> roots)
    {
        foreach (var (instDef, instAnchor) in _liveInstances())
        {
            if (instDef == def && NodesEqual(instAnchor, anchor))
                continue;
            foreach (var other in RootsOf(instDef, instAnchor))
                if (other != null && roots.Contains(other))
                    return true;
        }

        return false;
    }

    /// <summary>Shows or hides the effect-template root(s) a definition anchors on, when this
    /// runtime stages its templates hidden (<see cref="Shown"/>). Paired with the effect's life,
    /// not its instance: hiding on instance-finish would cut the ring off mid-flight, because the
    /// authored scale/opacity motions outlive the sequence that launched them.
    /// <paramref name="anchor"/> is the instance's own anchor, so on a pooled runtime only THAT
    /// call's copy is revealed or hidden — hiding the whole set would blank a sibling blast that
    /// is still burning.
    ///
    /// <para>The one place the ritual lives: <c>PlayEffectAt</c> and the <c>CALL_ANIMATION</c> arm
    /// both reveal through here after their Start, and the retire walk and instance teardown both
    /// hide through here — one shared spelling, so the entry points cannot drift apart.</para></summary>
    public void Reveal(AnimDefinition def, TNode? anchor, bool visible)
    {
        if (!Shown)
            return;
        // Either direction settles a deferred hide: a replay re-reveals this copy (its old hide is
        // about an effect that is over), and an explicit hide has already done the job.
        _hidesPending.RemoveAll(p => p.Def == def && NodesEqual(p.Anchor, anchor));
        // On a pooled stage only STAGED COPIES (nodes in a pool slot) take the visibility write.
        // Instance retirement runs this for every ended def, template or not — and a crash-rig
        // def whose "root" is the aircraft's own model (the place-exempt family), the `player`
        // crash scaffold or the wreck must never be blanked by the ritual that darkens an ended
        // effect's copy.
        foreach (var root in RootsOf(def, anchor))
            if (root != null && _isValid(root) && (!Pooled || SlotOf(root) >= 0))
                _setVisible(root, visible);
        // A def whose t=0 events complete it never reaches the retire walk — Start removes such an
        // instance itself — so a reveal for one would stand for the rest of the session.
        // `biggun_flying_parts` is exactly that shape: one CALL_ANIMATION, finished inside Start.
        // Scheduling the hide from here covers both entry points in one place, and the holds below
        // still apply, so a copy something is still animating stays lit.
        if (visible && !_isLive(def, anchor))
            RetireWhenIdle(def, anchor);
    }

    /// <summary>Hides an ended effect's template root — the other half of the reveal
    /// (<see cref="Reveal"/>), and the reason a staged template does not stay lit at the last hit
    /// site for the rest of the session. An explicit stop already hides what it tears down, but an
    /// instance that ends by reaching the end of its OWN sequences is removed without one: the
    /// ap/dum/mag gun hit authors an <c>ACTIVE_STATE 0</c> stop, finishes 0.3 s in and would leave
    /// its <c>dum_gunhit</c> chunk mesh visible at the impact point permanently (measured by
    /// <c>--effects-test</c>'s residual line), while the slug hit — which ships no stop and so runs
    /// to its TTL — is hidden by the Stop the sweep does.
    ///
    /// <para>Deferred while the instance's motions still run, because the reveal is paired with the
    /// EFFECT's life and not its instance's: the ring defs' scale/opacity motions outlive the
    /// sequence that launched them, and hiding on instance-finish cuts the explosion ring off
    /// mid-expansion. The <c>stillAnimated</c> hook carries which motions count, and
    /// <see cref="SharedWithLiveInstance"/> the other hold; <see cref="Sweep"/> drains the
    /// deferrals.</para></summary>
    public void RetireWhenIdle(AnimDefinition def, TNode? anchor)
    {
        if (!Shown)
            return;
        if (ReadyToHide(def, anchor))
        {
            Reveal(def, anchor, visible: false);
            return;
        }

        if (!_hidesPending.Any(p => p.Def == def && NodesEqual(p.Anchor, anchor)))
            _hidesPending.Add((def, anchor));
    }

    /// <summary>Retries every deferred hide — once a frame, from the runtime's own advance.</summary>
    public void Sweep()
    {
        for (int i = _hidesPending.Count - 1; i >= 0; i--)
        {
            var (def, anchor) = _hidesPending[i];
            // Reveal drops the entry itself, so the walk stays backwards and nothing else touches
            // the list here.
            if (ReadyToHide(def, anchor))
                Reveal(def, anchor, visible: false);
        }
    }

    /// <summary>Indexes a lazily-built pooled copy for name resolution and applies its RESET_STATE
    /// poses — the staging entry a copy passes through exactly once, when its provider builds it
    /// (`facdsticks` is the worked example). The three steps are runtime services supplied as hooks: the
    /// pointer-free index pass (a copy shares its source's compiled indices, so it must NOT join
    /// the by-index map), the resolver's find-cache clear (ancestry is snapshotted at index time),
    /// and the reset-state pass that puts the copy in its authored base pose.</summary>
    public void IndexPooledCopy(TNode subtree)
    {
        _indexSubtree(subtree);
        _clearFindCache();
        _applyResetStates(subtree);
    }

    /// <summary>Whether an ended effect's template may go dark now: nothing is still animating the
    /// copy, and no other live instance is playing on it.</summary>
    private bool ReadyToHide(AnimDefinition def, TNode? anchor)
    {
        var roots = RootsOf(def, anchor);
        return !_stillAnimated(roots) && !SharedWithLiveInstance(def, anchor, roots);
    }

    /// <summary>The slot <see cref="AssignCallerSlot"/> gave this (template root, anchor) pair,
    /// or -1 when it never claimed one.</summary>
    private int AssignedCallerSlot(AnimDefinition callee, TNode? anchor)
    {
        if (anchor == null || !_isValid(anchor) || string.IsNullOrEmpty(callee.Name))
            return -1;
        return _callerSlots.TryGetValue(callee.Name, out var byAnchor)
               && byAnchor.TryGetValue(anchor, out int s) ? s : -1;
    }

    private bool NodesEqual(TNode? a, TNode? b) =>
        a == null ? b == null : b != null && _identity.Equals(a, b);
}
