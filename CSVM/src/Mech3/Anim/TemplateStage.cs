using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// The effect-template stage as one module: pool-slot arithmetic, template placement, the
/// "which copy is mine" identity rule and the reveal/retire/sweep ritual. Generic over the node
/// type like <see cref="NameResolver{TNode}"/>.
/// ⚠ Do not move slot arithmetic into <see cref="EmitterDirector"/> or <see cref="AnimRuntime"/>;
/// the pool must never become a third emitter-keying scheme.
/// ⚠ Per-event root resolution (<see cref="RootsFor"/>, <see cref="IsAt"/>) must use <c>findAll</c>
/// only, never <see cref="NameResolver{TNode}.Anchors"/>, which records a census once per
/// definition; per-call paths (<see cref="TakeNextSlot"/>, <see cref="AssignCallerSlot"/>) may use
/// the <c>anchors</c> hook once each. Do not "clean up" this asymmetry.
/// </summary>
public sealed class TemplateStage<TNode>
    where TNode : class
{
    /// <summary>Squared-metres tolerance below which a placed template already sits "at" a call
    /// site (0.5 m). Separates a poll-loop re-issue (leave it) from a genuinely new site
    /// (relocate). One named constant so <c>IsAt</c> and the CallAnimation arm's move test
    /// cannot drift.</summary>
    public const float MoveToleranceSq = 0.25f;

    // Stands in for the authored event when a caller names no call site, so every claim has a key
    // and a site-less caller keeps one slot per anchor rather than one per call.
    private static readonly object UnkeyedCallSite = new();

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
    // scene node rather than a staged template copy (the Devastator's crash defs resolve to the
    // aircraft's own model root). Sealed at construction with the flags; docs/architecture.md.
    private readonly HashSet<string> _placeExempt;

    // A relocating CALL_ANIMATION whose anchor sits in no slot container claims a slot per
    // (template root, anchor, authored call site) here on its first call and keeps it. Keyed per
    // root, since one root's callers are a subset of all anchors. The list is in claim order, so
    // entry 0 is the anchor's own slot: the site a repeat call splits off from.
    private readonly Dictionary<string, Dictionary<TNode, List<(object Site, int Slot)>>> _callerSlots =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _callerSlotCursor = new(StringComparer.OrdinalIgnoreCase);

    // Effects whose instance has ended but whose template copy is still revealed — something is
    // still animating it, or another def is still playing on it (see RetireWhenIdle, and
    // Sweep for when they drain).
    private readonly List<(AnimDefinition Def, TNode? Anchor)> _hidesPending = new();

    // Placed roots that re-place themselves on a live call site every frame (PlaceFollowing): a
    // death's call on a carried node. The offset is the site's own frame, so a yawing hull
    // carries the fire around with it. A re-placement or a hide of the root ends the follow.
    private readonly List<(TNode Root, TNode Site, Vector3 LocalOffset)> _follows = new();

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
        // ⚠ Sealed here on purpose — no setter exists. A later write (the old post-Bind
        // mirror) let two roles read different configs off one flag; do not reintroduce it.
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

    /// <summary>Whether staged effect templates exist in more than one copy, one per pool slot, so
    /// each call takes the next copy instead of relocating the shared original. Plumbing and the
    /// slot-scoping rules: this module's entry in docs/architecture.md.
    /// ⚠ Off on the world runtime, deliberately: the world's templates are its own nodes, not
    /// staged copies, and there is nothing to pool.</summary>
    public bool Pooled { get; }

    /// <summary>Whether this runtime's templates are staged hidden, so the stage reveals a root
    /// while an effect plays and hides it again after. Only the root's own visibility is touched;
    /// what shows inside stays the data's decision. Set on the world-effects runtime; docs/
    /// architecture.md.</summary>
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

    /// <summary>How many placed roots are following a site right now (<see cref="PlaceFollowing"/>).</summary>
    public int Following => _follows.Count;

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
    /// container, so a re-call from the same site restarts its own copy, never a sibling's.
    /// Sticky per (template root, anchor, call site); wraps and counts through
    /// <see cref="Recycles"/>. Returns the copy a REPEAT site must run ON, null for the anchor's
    /// own first site and off the pool. Plumbing: docs/architecture.md.</summary>
    public TNode? AssignCallerSlot(AnimDefinition callee, TNode anchor, object? callSite = null)
    {
        if (!Pooled || string.IsNullOrEmpty(callee.Name) || SlotOf(anchor) >= 0
            || _placeExempt.Contains(callee.Name))
            return null;
        if (!_callerSlots.TryGetValue(callee.Name, out var byAnchor))
            _callerSlots[callee.Name] = byAnchor = new Dictionary<TNode, List<(object, int)>>(_identity);
        if (!byAnchor.TryGetValue(anchor, out var claims))
            byAnchor[anchor] = claims = new List<(object Site, int Slot)>();
        object site = callSite ?? UnkeyedCallSite;
        for (int i = 0; i < claims.Count; i++)
            if (ReferenceEquals(claims[i].Site, site))
                return i == 0 ? null : CopyInSlot(callee, claims[i].Slot);
        int copies = _findAll(callee.Name, null).Count;
        if (copies <= 1)
            return null;
        int next = _callerSlotCursor.TryGetValue(callee.Name, out int cur) ? cur : 0;
        _callerSlotCursor[callee.Name] = next + 1;
        claims.Add((site, next));
        // The per-site claim, log-visible beside the retarget line it pairs with — a debug
        // run reads this to confirm a new tear takes a fresh copy instead of the live one.
        if (_debug())
        {
            _print($"anim: caller slot {next % copies} of '{callee.Name}' ({copies} cop(ies)) "
                   + $"claimed by '{_nameOf(anchor)}'"
                   + (claims.Count > 1 ? $" (call {claims.Count} from this anchor)" : string.Empty));
        }
        if (next >= copies)
        {
            Recycles++;
            if (_recyclesLogged.Add(callee.AnimName ?? callee.Name))
            {
                _print($"anim: caller pool for '{callee.AnimName ?? callee.Name}' wrapped — "
                       + $"{next + 1} call site(s) over {copies} staged cop(ies) share again");
            }
        }
        return claims.Count == 1 ? null : CopyInSlot(callee, next);
    }

    /// <summary>The copies of a definition's own template root(s) that belong with
    /// <paramref name="inSlotOf"/>'s pool slot. Off the pool, or for a single-copy root, this is
    /// every match. The resolver's <c>ownRootsOf</c> hook. Resolved through <c>findAll</c>, never
    /// <c>Anchors</c> (docs/architecture.md).
    /// ⚠ Must return exactly one call's copies, picked by modulo when the callee is staged
    /// shallower than the caller's slot; falling back to "all of them" collapses the pool.</summary>
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

    /// <summary>Takes the next pool slot for one <c>PlayEffectAt</c> call and returns that slot's
    /// copy of the definition's roots, so overlapping calls animate different copies at different
    /// sites. Unpooled runtimes return every anchor unchanged.
    /// ⚠ The cursor wraps: a burst deeper than the pool recycles its oldest slot, counted once
    /// through <see cref="Recycles"/> (docs/architecture.md).</summary>
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

    /// <summary>Moves an effect template's own root(s) to a call site so its puffers emit there
    /// instead of at the template's gamez origin — the remake's stand-in for the original's
    /// per-call template copy. Pooled, only the call's own slot moves. With <paramref name="follow"/>
    /// the root keeps riding the site (<see cref="PlaceFollowing"/>). Docs: architecture.md.</summary>
    public void PlaceAt(AnimDefinition callee, TNode site, Vector3 offset, bool follow = false)
    {
        // An airframe-scoped NAME is a live scene node, never a staged template — placing it
        // would TopLevel-pin the aircraft itself (see _placeExempt). Resolution is untouched:
        // the callee's own node ops still run on that root, exactly as authored.
        if (!string.IsNullOrEmpty(callee.Name) && _placeExempt.Contains(callee.Name))
            return;
        var xf = _transformOf(site);
        var origin = xf.Origin + xf.Basis * offset;
        if (follow)
            PlaceFollowing(RootsFor(callee, site), site, origin, _levels(callee));
        else
            PlaceOn(RootsFor(callee, site), origin, _levels(callee));
    }

    /// <summary>Places the given root(s) at an absolute world origin, with <paramref name="orient"/>
    /// as the whole basis when given (an impact's surface orientation) and the basis left as it
    /// stands when null. The engine write behind the <c>placeAt</c> hook world-stages each root once
    /// placed: <c>TopLevel</c> decouples it from a moving/rotating caller (a flying plane's pdpN
    /// panel) so it holds this pose instead of being dragged and re-yawed every later frame the
    /// caller moves. A later call (or a recycled pool slot) simply overwrites this transform.</summary>
    public void PlaceOn(IEnumerable<TNode?> roots, Vector3 origin, bool level = false, Basis? orient = null)
    {
        foreach (var root in roots)
        {
            if (root == null || !_isValid(root))
                continue;
            // A placement is a new site: whatever this root was following is over.
            _follows.RemoveAll(f => _identity.Equals(f.Root, root));
            var xf = _transformOf(root);
            xf.Origin = origin;
            // ⚠ Named crash-def templates only (docs/architecture.md); leveling every template
            // would fight debris scatter authored in its own co-rotating frame.
            if (level)
                xf.Basis = Basis.Identity;
            if (orient is { } basis)
                xf.Basis = basis;
            _placeAt(root, xf);
        }
    }

    /// <summary>Places the root(s) at <paramref name="origin"/> as <see cref="PlaceOn"/> does, then
    /// keeps them there RELATIVE to <paramref name="site"/>: <see cref="FollowSites"/> re-places each
    /// root every frame at the site's live pose plus the offset the origin had in the site's frame.
    /// The placement for a death's effects on a site that moves (a zeppelin's gun ring); a site that
    /// never moves reads exactly as a <see cref="PlaceOn"/>. The root's basis is left as placed;
    /// only the site's translation and yaw carry the offset (docs/architecture.md).</summary>
    public void PlaceFollowing(IEnumerable<TNode?> roots, TNode site, Vector3 origin, bool level = false)
    {
        var siteXf = _transformOf(site);
        var local = siteXf.AffineInverse() * origin;
        var placed = roots.Where(r => r != null && _isValid(r)).ToList();
        PlaceOn(placed, origin, level);
        foreach (var root in placed)
            _follows.Add((root!, site, local));
    }

    /// <summary>Re-places every following root on its site's current pose, once per frame from the
    /// runtime's advance, BEFORE the emitters read their hosts. A root whose site was freed stops
    /// where it is.</summary>
    public void FollowSites()
    {
        for (int i = _follows.Count - 1; i >= 0; i--)
        {
            var (root, site, local) = _follows[i];
            if (!_isValid(root) || !_isValid(site))
            {
                _follows.RemoveAt(i);
                continue;
            }
            var xf = _transformOf(root);
            xf.Origin = _transformOf(site) * local;
            _placeAt(root, xf);
        }
    }

    /// <summary>Whether a placed template already sits where a call wants it, within
    /// <see cref="MoveToleranceSq"/>. Resolved the same way the resolver's own-root tier is, never
    /// through <c>Anchors</c> (this runs per event). Pooled, only the call's own slot is asked.
    /// Docs: architecture.md.</summary>
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
    /// runtime stages templates hidden (<see cref="Shown"/>). Paired with the effect's life, not
    /// its instance: hiding on instance-finish would cut a motion that outlives its sequence.
    /// Pooled, only the call's own copy is touched. The one shared entry point both
    /// <c>PlayEffectAt</c> and the CALL_ANIMATION arm reveal through (docs/architecture.md).</summary>
    public void Reveal(AnimDefinition def, TNode? anchor, bool visible)
    {
        if (!Shown)
            return;
        // Either direction settles a deferred hide: a replay re-reveals this copy (its old hide is
        // about an effect that is over), and an explicit hide has already done the job.
        _hidesPending.RemoveAll(p => p.Def == def && NodesEqual(p.Anchor, anchor));
        // ⚠ Only staged copies (nodes in a pool slot) take the visibility write; a crash-rig
        // "root" that is the aircraft's own model must never be blanked here.
        foreach (var root in RootsOf(def, anchor))
        {
            if (root == null || !_isValid(root) || (Pooled && SlotOf(root) < 0))
                continue;
            _setVisible(root, visible);
            // A hidden copy has nothing left to carry along; its next play places it afresh.
            if (!visible)
                _follows.RemoveAll(f => _identity.Equals(f.Root, root));
        }
        // ⚠ A def whose t=0 events finish it never reaches the retire walk (Start removes it
        // itself), so schedule its hide here too or a reveal stands for the rest of the session.
        if (visible && !_isLive(def, anchor))
            RetireWhenIdle(def, anchor);
    }

    /// <summary>Hides an ended effect's template root, the other half of <see cref="Reveal"/> —
    /// why a staged template does not stay lit at the last hit site for the rest of the session.
    /// Deferred while the instance's motions still run, since the reveal is paired with the
    /// effect's life, not its instance's. <see cref="Sweep"/> drains the deferrals. Docs:
    /// architecture.md.</summary>
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

    // Whether an ended effect's template may go dark now: nothing is still animating the
    // copy, and no other live instance is playing on it.
    private bool ReadyToHide(AnimDefinition def, TNode? anchor)
    {
        var roots = RootsOf(def, anchor);
        return !_stillAnimated(roots) && !SharedWithLiveInstance(def, anchor, roots);
    }

    // The slot AssignCallerSlot gave this (template root, anchor) pair, or -1 when it never
    // claimed one. ⚠ The anchor's FIRST claim, never a later site's: a repeat site runs on its own
    // copy AS anchor, so it is answered by SlotOf and never asks here.
    private int AssignedCallerSlot(AnimDefinition callee, TNode? anchor)
    {
        if (anchor == null || !_isValid(anchor) || string.IsNullOrEmpty(callee.Name))
            return -1;
        return _callerSlots.TryGetValue(callee.Name, out var byAnchor)
               && byAnchor.TryGetValue(anchor, out var claims) && claims.Count > 0
            ? claims[0].Slot : -1;
    }

    // The staged copy sitting in one pool slot, mapped onto the copies that exist the same way
    // RootsFor maps a caller's slot — so a claim past the staged count wraps rather than missing.
    private TNode? CopyInSlot(AnimDefinition callee, int slot)
    {
        var roots = _findAll(callee.Name, null);
        var staged = roots.Select(SlotOf).Where(s => s >= 0).Distinct().OrderBy(s => s).ToList();
        if (staged.Count == 0)
            return null;
        int want = staged.Contains(slot) ? slot : staged[((slot % staged.Count) + staged.Count) % staged.Count];
        foreach (var root in roots)
            if (SlotOf(root) == want)
                return root;
        return null;
    }

    private bool NodesEqual(TNode? a, TNode? b) =>
        a == null ? b == null : b != null && _identity.Equals(a, b);
}
