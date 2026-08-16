using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CSVM.Mech3.Anim;

/// <summary>Name → node resolution: the index, the wildcard matcher, the memoized <see
/// cref="FindAll"/>, the scoped tier chain (<see cref="Resolve"/>/<see cref="ResolveScoped"/>),
/// the symbol-table authority (<see cref="SymbolClaims"/>, the by-index map <see cref="Add"/>
/// builds), the anchoring rules (<see cref="Anchors"/>: NAME match, symbol narrowing, the
/// ANIMATION_ROOT_NAME lift) and the bind-time resolution census (<see cref="ResolutionLines"/>)
/// — generic over the node type so the whole rule set is testable without an engine (a plain
/// token type in <c>CSVM.Tests</c>) and shared, unchanged, by the one engine instantiation
/// (<c>AnimRuntime</c>'s <c>NameResolver&lt;Node3D&gt;</c>).
///
/// <para>⚠ The three-tier scope order — call anchor's subtree, then the definition's OWN template
/// roots (the constructor's <c>ownRootsOf</c> hook), then global unless <c>LOCAL_NODES_ONLY</c> —
/// is structural here: <see cref="ResolvePath"/> is private, so no consumer can compose the
/// primitives in a different order. It once diverged caller-side (the motion targets and the
/// puffer host took different routes to the same name and landed on different copies, so an
/// authored stop never reached its emitter); every consumer resolves through
/// <see cref="Resolve"/>/<see cref="ResolveScoped"/> by construction.</para>
///
/// <para>Rows enter through <see cref="Add"/> and are never removed — most build during one
/// bootstrap walk and every later query reads that as a fixed snapshot, including ancestry: <see
/// cref="Add"/>'s <c>parent</c> argument is recorded once, so a scoped <see cref="FindAll"/> never
/// re-touches the live tree to ask "is this node inside that one" — it walks the recorded parent
/// chain instead. ⚠ No node this resolver has already indexed may be reparented afterwards (into
/// or out of any indexed subtree) — the ancestry snapshot goes stale and <see cref="FindAll"/>'s
/// scope filter silently misreads it. A subtree staged after the bootstrap (an effect template, a
/// pooled copy) may still <see cref="Add"/> more rows, but the caller must also call <see
/// cref="ClearFindCache"/>, or a pattern <see cref="FindAll"/> already cached as "resolves to
/// nothing" stays stale even though the new rows would now answer it.</para>
///
/// <para>⚠ <see cref="Anchors"/> records the anchoring census ONCE per definition and is the only
/// census-recording entry point; everything else here (<see cref="FindAll"/>,
/// <see cref="ResolvePath"/>, the private census-free anchor computation it wraps) records
/// nothing, so a per-event caller — the runtime's own-template-root resolution runs per event on
/// poll loops — can resolve freely without re-entering the census. Keep it that way: a second
/// recording path double-counts definitions the bootstrap visits on more than one pass.</para>
///
/// <para>⚠ Node identity is caller-supplied, not inherited: the constructor takes an <see
/// cref="IEqualityComparer{T}"/> that answers identity, not value equality. The engine
/// instantiation keys by instance id — Godot object equality is unreliable inside a dictionary/
/// tuple key across proxy instances of the same native node — rather than trusting
/// <typeparamref name="TNode"/>'s inherited <c>Equals</c>.</para></summary>
public sealed class NameResolver<TNode>
    where TNode : class
{
    // ---- policy inputs. The owning runtime sets these before its bootstrap calls Add/Anchors;
    // they are construction-time facts about the runtime, not per-query switches. ----

    /// <summary>Resolve every node reference by NAME, ignoring the compiled gamez index:
    /// <see cref="Add"/> leaves the by-index map empty, so every <see cref="SymbolClaims"/> lookup
    /// reports "index not built" and symbol narrowing never decides. Set by a runtime whose
    /// subtree either carries non-portable node ptrs or MIXES two gamez index spaces that collide
    /// (the per-player crash rig — see <c>AnimRuntime.NameResolveFallback</c> for the measured
    /// cases); such a subtree has one node per name, so name resolution is the only correct
    /// choice.</summary>
    public bool NameResolveFallback;

    /// <summary>Refuse the <c>ANIMATION_ROOT_NAME</c> anchor lift, however few matches it finds.
    /// Set only by a caller that built part of the world, because <see cref="MaxRootLift"/>'s
    /// premise is a WHOLE-WORLD node population: a single-subtree stage drops under the cap, at
    /// which point unrelated definitions anchor onto whatever generic child the subtree happens to
    /// own (measured on C1's 20-node <c>ap_radiotwr</c>: 95 lifts and 91 phantom destructible
    /// instances). Suppressed lifts are counted and reported, never silently dropped.</summary>
    public bool SuppressRootLift;

    /// <summary>Collect a per-definition census of how the bind RESOLVED, reported through
    /// <see cref="ResolutionLines"/>. The census window itself is <see cref="OpenCensus"/>/<see
    /// cref="CloseCensus"/> — the owning runtime brackets its bootstrap passes with them, so a
    /// runtime miss after the bind reports itself elsewhere instead of restating the bind. Default
    /// false: a full-world session collects nothing.</summary>
    public bool ReportResolution;

    /// <summary>ANIMATION_ROOT_NAME matches above this count are generic per-object roots
    /// ('healthy' appears 217× in C1) — those defs belong to game objects (planes, zeppelin
    /// parts), not to world nodes. The genuine building templates lift ≤ 9 instances.</summary>
    public int MaxRootLift = 16;

    private const int CensusCap = 12;

    private readonly List<IndexRow> _index = new();

    private readonly Dictionary<TNode, TNode?> _parentOf;

    private readonly Dictionary<string, Func<string, bool>> _matcherCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<(string Pattern, TNode? Scope), List<TNode>> _findCache;

    private readonly IEqualityComparer<TNode> _identity;

    // The middle tier: the definition's own template roots, resolved by the OWNER (the pool and
    // its slot arithmetic stay AnimRuntime's — the resolver sees only the resolved root list).
    private readonly Func<AnimDefinition, TNode?, IReadOnlyList<TNode>> _ownRootsOf;

    // Liveness of an anchor node (the engine passes Godot's IsInstanceValid; tests pass _ => true).
    private readonly Func<TNode, bool> _isLive;

    // The compiled gamez index -> built node map SymbolClaims and NarrowToSymbolRoot read. See
    // Add for the two population rules (never on a fallback runtime, never for a pooled copy).
    private readonly Dictionary<int, TNode> _byIndex = new();

    // ---- the bind-time resolution census (see ResolutionLines) ----
    private readonly HashSet<(string Name, string Anim)> _censusSeen = new();

    private readonly List<string> _censusUnanchoredNames = new();

    private readonly List<string> _censusLiftedNames = new();

    private readonly List<string> _censusSuppressedNames = new();

    private readonly List<string> _censusMissingTargets = new();

    private bool _censusOpen;

    private int _censusAnchored, _censusNarrowed, _censusLifted, _censusSuppressed, _censusUnanchored, _censusMissing;

    /// <summary>
    /// <paramref name="ownRootsOf"/> supplies the tier chain's middle tier: the copies of a
    /// definition's own template root(s) that belong with the given anchor's pool slot. It runs
    /// per event on poll loops, so it must resolve through <see cref="FindAll"/> — never <see
    /// cref="Anchors"/>, whose once-per-definition census it would re-enter — and must not
    /// allocate beyond what a <see cref="FindAll"/> call already does. Null means "no own roots"
    /// (the tier always misses). <paramref name="isLive"/> answers whether an anchor node is
    /// still valid; a dead anchor drops the scoped tiers entirely (see
    /// <see cref="ResolveScoped"/>).
    /// </summary>
    public NameResolver(
        IEqualityComparer<TNode>? identity = null,
        Func<AnimDefinition, TNode?, IReadOnlyList<TNode>>? ownRootsOf = null,
        Func<TNode, bool>? isLive = null)
    {
        _identity = identity ?? EqualityComparer<TNode>.Default;
        _ownRootsOf = ownRootsOf ?? ((_, _) => Array.Empty<TNode>());
        _isLive = isLive ?? (_ => true);
        _parentOf = new Dictionary<TNode, TNode?>(_identity);
        _findCache = new Dictionary<(string, TNode?), List<TNode>>(new ScopeKeyComparer(_identity));
    }

    /// <summary>Every indexed row's node and source name, in <see cref="Add"/> order — for a
    /// caller that walks the whole index by some rule other than a NAME pattern (a substring sweep
    /// for uncovered destroyed-variant subtrees). Read-only.</summary>
    public IEnumerable<(TNode Node, string SrcName)> Rows
    {
        get
        {
            foreach (var row in _index)
            {
                yield return (row.Node, row.SrcName);
            }
        }
    }

    /// <summary>Adds one row to the index: a node's source (gamez) name, its parent — the ancestry
    /// snapshot <see cref="FindAll"/>'s scope filter reads instead of touching the live tree — and
    /// its compiled gamez-index slot when it has one.
    ///
    /// <para>⚠ The by-index map <see cref="SymbolClaims"/> reads is populated here, under two
    /// deliberate refusals ported from the measured bugs behind them: never when <see
    /// cref="NameResolveFallback"/> is set (that runtime's index spaces collide — every lookup must
    /// miss and fall through to the unique name), and never for a row added with
    /// <paramref name="indexByPointer"/> false — a POOLED copy of a staged template carries the
    /// SAME compiled indices as every other copy, so a shared index-keyed map can hold only the
    /// first copy that claims each slot and a second copy's events would silently resolve onto the
    /// first's nodes. Such rows still resolve by name, scoped to their own subtree.</para></summary>
    public void Add(TNode node, string srcName, TNode? parent, int? gamezIndex = null, bool indexByPointer = true)
    {
        _index.Add(new IndexRow(node, srcName, gamezIndex));
        _parentOf[node] = parent;
        if (indexByPointer && !NameResolveFallback && gamezIndex is { } gi)
        {
            _byIndex.TryAdd(gi, node);
        }
    }

    /// <summary>Drops every memoized <see cref="FindAll"/> answer — needed after <see cref="Add"/>
    /// grows the index post-bootstrap, so a pattern already cached as "resolves to nothing" is
    /// re-asked instead of standing stale.</summary>
    public void ClearFindCache() => _findCache.Clear();

    /// <summary>Every indexed node matching a NAME pattern, optionally restricted to one node's
    /// subtree (inclusive of that node itself). Wildcards: <c>*</c> matches any run of characters,
    /// <c>#</c> a run of digits including zero (<c>air_gen#</c> covers bare <c>air_gen</c>); a
    /// plain name compares case-insensitively. A name carrying a <c>.flt</c> model-file suffix also
    /// matches the pattern against its own name with the suffix stripped, so a definition that
    /// names a node without it still resolves.
    ///
    /// <para>Memoized on <c>(pattern, scope)</c>: the index never changes after <see cref="Add"/>
    /// stops being called, so the answer cannot change either. Callers must treat the returned list
    /// as read-only — the same instance is handed back on every repeat query.</para></summary>
    public List<TNode> FindAll(string pattern, TNode? scope)
    {
        var key = (pattern, scope);
        if (_findCache.TryGetValue(key, out var hit))
        {
            return hit;
        }
        var match = Matcher(pattern);
        var result = new List<TNode>();
        foreach (var row in _index)
        {
            var matches = match(row.SrcName)
                || (row.SrcName.EndsWith(".flt", StringComparison.OrdinalIgnoreCase)
                    && match(row.SrcName[..^4]));
            if (matches && (scope is null || IsWithin(row.Node, scope)))
            {
                result.Add(row.Node);
            }
        }
        _findCache[key] = result;
        return result;
    }

    /// <summary>Resolves a single node name for one definition — the compiled symbol table first
    /// (<see cref="SymbolClaims"/>), then the scoped tier chain. A name the symbol table claims
    /// but binds to nothing (index not built) still falls through to <see cref="ResolveScoped"/>:
    /// the caller decides what an unbuilt claim means (see <c>AnimRuntime.Targets</c>' strictly
    /// anchor-scoped rescue) — this convenience form keeps the pre-existing fall-through.</summary>
    public TNode? Resolve(string name, AnimDefinition def, TNode? anchor)
    {
        if (SymbolClaims(def, name, out var bound) && bound != null)
        {
            return bound;
        }
        var found = ResolveScoped(new List<string> { name }, def, anchor);
        return found.Count > 0 ? found[0] : null;
    }

    /// <summary>Resolves a NAME path for one definition, narrowest scope first: the call anchor's
    /// subtree, then the DEFINITION'S OWN template root(s) (the constructor's <c>ownRootsOf</c>
    /// hook), then — unless the definition carries <c>LOCAL_NODES_ONLY</c> — the whole index. A
    /// null or dead anchor (the <c>isLive</c> predicate) skips the scoped tiers and resolves
    /// against the whole index directly, regardless of <c>LOCAL_NODES_ONLY</c>.
    ///
    /// <para>The middle tier exists because the anchor is not always where the def's own nodes
    /// live: an effect callee is re-anchored onto the CALL SITE (<c>call_hetrails_up</c> onto
    /// <c>he_ring</c>) while its nodes ride its own template root, which the owner relocated to
    /// that site. Effect templates staged side by side reuse node names — <c>fly_trail1</c>-<c>5</c>
    /// belongs to <c>he_trails</c>, <c>ap_trails</c> AND <c>carnage_trails</c> — so falling
    /// straight to the global index animated every copy, two of them still parked at the stage
    /// origin, i.e. the world origin.</para>
    ///
    /// <para>Callers must treat the returned list as read-only — a single-element path hands back
    /// the memoized <see cref="FindAll"/> list itself.</para></summary>
    public List<TNode> ResolveScoped(IReadOnlyList<string> path, AnimDefinition def, TNode? anchor)
    {
        if (anchor == null || !_isLive(anchor))
        {
            return ResolvePath(path, null, localOnly: true);
        }
        var found = ResolvePath(path, anchor, localOnly: true);
        if (found.Count == 0)
        {
            found = ResolveInOwnRoot(path, def, anchor);
        }
        if (found.Count == 0 && !def.LocalNodesOnly)
        {
            found = ResolvePath(path, null, localOnly: true);
        }
        return found;
    }

    /// <summary>The world nodes a definition anchors to: NAME matches (wildcards = one per
    /// building/vehicle instance), narrowed to the instance holding the def's symbol-table ROOT
    /// node when the NAME is shared by twins; else ANIMATION_ROOT_NAME matches lifted to their
    /// parent (the instance root, so sibling healthy/destroyed both resolve locally — needed where
    /// instance roots have free names: <c>m_build**</c> instances are <c>apbuild01.flt</c>…),
    /// refused above <see cref="MaxRootLift"/> matches and under <see cref="SuppressRootLift"/>.
    /// Returns a fresh list each call.
    ///
    /// <para>⚠ This is the ONE census-recording resolution call (once per definition identity, see
    /// the class remarks) — every other query stays census-free by construction.</para></summary>
    public List<TNode?> Anchors(AnimDefinition def)
    {
        // Multi-target NAME1 definitions (zeppelin nacelles/turrets/gasbag wiring) parse with
        // an empty NAME but carry their own explicit (pattern, anchor path) pairs. They anchor
        // through THOSE paths — never the ANIMATION_ROOT_NAME lift, whose generic root names
        // ('healthy') would anchor them onto every building in the world; that refusal was the
        // pre-F18 rule and it still holds for an empty-NAME def with no recorded targets
        // (compiled camera/eject defs). Deliberately scoped to the parsed NAME1 paths and
        // nothing wider (M4 F18: each zeppelin sub-part becomes its own scalar destructible).
        if (string.IsNullOrEmpty(def.Name))
        {
            if (def.MultiTargets.Count == 0)
            {
                return new List<TNode?>();
            }
            var multi = MultiTargetAnchors(def);
            RecordAnchoring(def, multi.Count > 0 ? AnchorKind.ByName : AnchorKind.None);
            return multi;
        }
        var (anchors, how) = ComputeAnchors(def);
        RecordAnchoring(def, how);
        return anchors;
    }

    /// <summary>The symbol-table authority: true when the definition's symbol table CLAIMS
    /// <paramref name="name"/> — the caller must then not fall back to global name matching, which
    /// resolves C1's <c>caboose</c> to the real consist AND an unrelated <c>caboose.flt</c>.
    /// <paramref name="node"/> is the world node the compiled gamez index binds the name to, or
    /// null when that node was never built (LOD levels the builder drops, skipped subtrees — and
    /// every lookup on a <see cref="NameResolveFallback"/> runtime, whose by-index map is left
    /// empty on purpose). False means the name is not the symbol table's to answer (a reader def,
    /// an unlisted name) and name resolution proceeds as usual.</summary>
    public bool SymbolClaims(AnimDefinition def, string name, out TNode? node)
    {
        node = null;
        if (!def.NodeRefs.TryGetValue(name, out int idx))
        {
            return false;
        }
        if (_byIndex.TryGetValue(idx, out var bound))
        {
            node = bound;
        }
        return true;
    }

    /// <summary>Opens the bind-census window (a no-op unless <see cref="ReportResolution"/>): the
    /// owning runtime calls this before its bootstrap passes and <see cref="CloseCensus"/> after
    /// them, so the census is a statement about what the BIND could reach — a later runtime miss
    /// has its own reporting and must not restate it.</summary>
    public void OpenCensus() => _censusOpen = ReportResolution;

    public void CloseCensus() => _censusOpen = false;

    /// <summary>Records an event that named a node the bind could not reach (inside the census
    /// window only). <paramref name="why"/> separates the two causes, which otherwise read the
    /// same: the compiled symbol table bound the name to a gamez node this build never created,
    /// versus name resolution finding no match at all.</summary>
    public void RecordMissingTarget(AnimDefinition def, string refName, string why)
    {
        if (!_censusOpen)
        {
            return;
        }
        _censusMissing++;
        if (_censusMissingTargets.Count < CensusCap)
        {
            string label = def.AnimName is { Length: > 0 } anim ? $"{anim}@{def.Name}" : def.Name;
            _censusMissingTargets.Add($"{label} ref={refName} why={why}");
        }
    }

    /// <summary>
    /// The bind-time resolution census, ready to log — empty unless <see cref="ReportResolution"/>
    /// was set. It exists because <b>two different failures look identical from outside</b> a
    /// partial world: a definition that never instantiated (its NAME/ANIMATION_ROOT_NAME matches
    /// nothing here, so <i>no handler ever fires</i>) and a definition that IS running but whose
    /// event names a node <i>this subtree does not contain</i>. Both leave the object still. The
    /// lines name which happened, per definition.
    ///
    /// <para>The line nobody expects is <c>root_lift_suppressed</c>: an <c>ANIMATION_ROOT_NAME</c>
    /// lift is capped at <see cref="MaxRootLift"/> matches precisely so a generic root like
    /// <c>healthy</c> (217× in C1) cannot anchor a definition onto every building — and a
    /// single-subtree stage drops under that cap, so defs that never anchor in the full world would
    /// anchor here, onto whatever generic child the subtree happens to own.
    /// <see cref="SuppressRootLift"/> refuses them and this reports the refusal, because a
    /// silently-different anchor set is the trap.</para>
    /// </summary>
    public IReadOnlyList<string> ResolutionLines()
    {
        var lines = new List<string>();
        if (!ReportResolution)
        {
            return lines;
        }
        int defs = _censusAnchored + _censusNarrowed + _censusLifted + _censusSuppressed
                   + _censusUnanchored;
        lines.Add($"bind census defs={defs} anchored_by_name={_censusAnchored} "
                  + $"narrowed_by_symbol={_censusNarrowed} "
                  + $"anchored_by_root_lift={_censusLifted} root_lift_suppressed={_censusSuppressed} "
                  + $"unanchored={_censusUnanchored} target_missing_ops={_censusMissing}");
        if (_censusUnanchored > 0)
        {
            lines.Add($"bind unanchored={_censusUnanchored} — no handler ever fires for these: "
                      + Sample(_censusUnanchoredNames, _censusUnanchored));
        }
        if (_censusLifted > 0)
        {
            lines.Add($"bind root_lifted={_censusLifted} — anchored only because this subtree has "
                      + $"≤{MaxRootLift} of the def's ANIMATION_ROOT_NAME, which the full world does not: "
                      + Sample(_censusLiftedNames, _censusLifted));
        }
        if (_censusSuppressed > 0)
        {
            lines.Add($"bind root_lift_suppressed={_censusSuppressed} — these WOULD have anchored on "
                      + $"this subtree's generic ANIMATION_ROOT_NAME children, which the full world's "
                      + $"node count rules out; refused so the stage shows only defs that name it: "
                      + Sample(_censusSuppressedNames, _censusSuppressed));
        }
        if (_censusMissing > 0)
        {
            lines.Add($"bind target_missing={_censusMissing} — the def IS running, the node is not in "
                      + $"this subtree: " + Sample(_censusMissingTargets, _censusMissing));
        }
        return lines;
    }

    private static string Sample(List<string> shown, int total) =>
        string.Join(", ", shown) + (total > shown.Count ? $", … (+{total - shown.Count} more)" : "");

    // A parent->child NAME path: the first element within scope (falling back to the whole index
    // when that misses and localOnly is false), then each further element inside the previous
    // matches. Private on purpose — the tier order above is the ONLY route to it (⚠ class
    // remarks); every tier call passes localOnly: true, the global step being a tier of its own.
    private List<TNode> ResolvePath(IReadOnlyList<string> path, TNode? scope, bool localOnly)
    {
        var candidates = FindAll(path[0], scope);
        if (candidates.Count == 0 && scope is not null && !localOnly)
        {
            candidates = FindAll(path[0], null);
        }
        for (int i = 1; i < path.Count && candidates.Count > 0; i++)
        {
            var next = new List<TNode>();
            foreach (var c in candidates)
            {
                foreach (var n in FindAll(path[i], c))
                {
                    if (!_identity.Equals(n, c))
                    {
                        next.Add(n);
                    }
                }
            }
            candidates = next;
        }
        return candidates;
    }

    // The middle tier: the path resolved inside the definition's own template root(s) — the same
    // copies the owner places at a call site — de-duplicated in first-seen order. Census-free by
    // construction: the hook and this method go through FindAll only (⚠ class remarks).
    private List<TNode> ResolveInOwnRoot(IReadOnlyList<string> path, AnimDefinition def, TNode? anchor)
    {
        var found = new List<TNode>();
        if (string.IsNullOrEmpty(def.Name))
        {
            return found;
        }
        foreach (var root in _ownRootsOf(def, anchor))
        {
            foreach (var node in ResolvePath(path, root, localOnly: true))
            {
                if (!ContainsIdentity(found, node))
                {
                    found.Add(node);
                }
            }
        }
        return found;
    }

    private bool ContainsIdentity(List<TNode> list, TNode node)
    {
        foreach (var n in list)
        {
            if (_identity.Equals(n, node))
            {
                return true;
            }
        }
        return false;
    }

    // The NAME1 anchor set: each pair's authored path resolved parent→child against the whole
    // index, unioned in first-seen order. Census-free below Anchors, like ComputeAnchors.
    private List<TNode?> MultiTargetAnchors(AnimDefinition def)
    {
        var seen = new HashSet<TNode>(_identity);
        var anchors = new List<TNode?>();
        foreach (var (_, path) in def.MultiTargets)
        {
            foreach (var node in ResolvePath(path, null, localOnly: true))
            {
                if (seen.Add(node))
                {
                    anchors.Add(node);
                }
            }
        }
        return anchors;
    }

    // The census-free anchor computation Anchors wraps — the internal path for anything that must
    // not re-enter the census (⚠ class remarks); the tier chain stays census-free the same way,
    // through FindAll/ResolvePath only.
    private (List<TNode?> Anchors, AnchorKind How) ComputeAnchors(AnimDefinition def)
    {
        var anchors = FindAll(def.Name, null).Cast<TNode?>().ToList();
        var how = anchors.Count > 0 ? AnchorKind.ByName : AnchorKind.None;
        if (NarrowToSymbolRoot(def, anchors) is { } only)
        {
            anchors = only;
            how = AnchorKind.BySymbol;
        }
        if (anchors.Count == 0 && def.RootName != null)
        {
            var roots = FindAll(def.RootName, null);
            if (roots.Count > 0 && roots.Count <= MaxRootLift)
            {
                if (SuppressRootLift)
                {
                    how = AnchorKind.LiftSuppressed;
                }
                else
                {
                    // Lift each ROOT match to its parent (the instance root), de-duplicated in
                    // first-seen order — parents come from the Add-time snapshot, not the live tree.
                    var seen = new HashSet<TNode>(_identity);
                    var lifted = new List<TNode?>();
                    foreach (var root in roots)
                    {
                        if (ParentOf(root) is { } p && seen.Add(p))
                        {
                            lifted.Add(p);
                        }
                    }
                    anchors = lifted;
                    how = anchors.Count > 0 ? AnchorKind.ByRootLift : AnchorKind.None;
                }
            }
        }
        return (anchors, how);
    }

    // Picks the one instance a compiled definition actually belongs to, when its NAME
    // matches several. The compiler expands a multi-instance object into one def per instance but
    // leaves them all sharing a NAME — C1's two airfield hangars are both `air_gen`, telling
    // them apart only by their symbol tables (`air_gen` names the nodes under
    // `eairg32`, `air_gen#1` those under `eairg31`). Name matching hands BOTH defs
    // BOTH anchors, so the pair cross-binds: shooting one hangar resolved to the other def, whose
    // events then target its own hangar by exact index — destroy `eairg31` and
    // `eairg32` explodes.
    //
    // So resolve the def's ANIMATION_ROOT_NAME through the symbol table — the same
    // authority SymbolClaims gives every event — and keep only the anchors
    // containing that exact node. Returns null when it cannot decide: a reader def (no symbol
    // table), an index the builder never built, or a root outside every candidate — all of which
    // leave the name match standing. A NameResolveFallback runtime gets null for
    // free, since its by-index map is empty. ⚠ Null (undecidable) and an empty narrowing are
    // different outcomes — an empty result never leaves this method.
    private List<TNode?>? NarrowToSymbolRoot(AnimDefinition def, List<TNode?> anchors)
    {
        if (anchors.Count < 2
            || def.RootName is not { } root
            || !def.NodeRefs.TryGetValue(root, out int idx)
            || !_byIndex.TryGetValue(idx, out var exact))
        {
            return null;
        }
        var kept = new List<TNode?>();
        foreach (var a in anchors)
        {
            if (a != null && IsWithin(exact, a))
            {
                kept.Add(a);
            }
        }
        return kept.Count > 0 && kept.Count < anchors.Count ? kept : null;
    }

    // One census entry per definition identity (anchor name + animation name — AnimProgram's own
    // dedupe key), because the bootstrap asks for a def's anchors on more than one pass.
    private void RecordAnchoring(AnimDefinition def, AnchorKind how)
    {
        if (!_censusOpen || !_censusSeen.Add((def.Name, def.AnimName ?? "")))
        {
            return;
        }
        string label = def.AnimName is { Length: > 0 } anim ? $"{anim}@{def.Name}" : def.Name;
        switch (how)
        {
            case AnchorKind.ByName:
                _censusAnchored++;
                break;
            case AnchorKind.BySymbol:
                _censusNarrowed++;
                break;
            case AnchorKind.ByRootLift:
                _censusLifted++;
                if (_censusLiftedNames.Count < CensusCap)
                {
                    _censusLiftedNames.Add($"{label}→{def.RootName}");
                }
                break;
            case AnchorKind.LiftSuppressed:
                _censusSuppressed++;
                if (_censusSuppressedNames.Count < CensusCap)
                {
                    _censusSuppressedNames.Add($"{label}→{def.RootName}");
                }
                break;
            default:
                _censusUnanchored++;
                if (_censusUnanchoredNames.Count < CensusCap)
                {
                    _censusUnanchoredNames.Add(label);
                }
                break;
        }
    }

    // node is within scope's subtree, inclusive of scope itself — walks the recorded parent chain
    // rather than the live tree (see the class remarks).
    private bool IsWithin(TNode node, TNode scope)
    {
        for (TNode? p = node; p is not null; _parentOf.TryGetValue(p, out p))
        {
            if (_identity.Equals(p, scope))
            {
                return true;
            }
        }
        return false;
    }

    private TNode? ParentOf(TNode node) => _parentOf.TryGetValue(node, out var p) ? p : null;

    // Wildcard NAME -> predicate: '*' matches any run of characters, '#' a run of digits (including
    // zero). Plain names compare exactly, case-insensitively.
    private Func<string, bool> Matcher(string pattern)
    {
        if (_matcherCache.TryGetValue(pattern, out var cached))
        {
            return cached;
        }
        Func<string, bool> match;
        if (pattern.Contains('*') || pattern.Contains('#'))
        {
            var re = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\#", "[0-9]*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            match = re.IsMatch;
        }
        else
        {
            match = s => s.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
        return _matcherCache[pattern] = match;
    }

    private readonly record struct IndexRow(TNode Node, string SrcName, int? GamezIndex);

    private sealed class ScopeKeyComparer : IEqualityComparer<(string Pattern, TNode? Scope)>
    {
        private readonly IEqualityComparer<TNode> _inner;

        public ScopeKeyComparer(IEqualityComparer<TNode> inner) => _inner = inner;

        public bool Equals((string Pattern, TNode? Scope) x, (string Pattern, TNode? Scope) y) =>
            string.Equals(x.Pattern, y.Pattern, StringComparison.Ordinal)
            && (x.Scope is null ? y.Scope is null : y.Scope is not null && _inner.Equals(x.Scope, y.Scope));

        public int GetHashCode((string Pattern, TNode? Scope) obj) =>
            HashCode.Combine(obj.Pattern, obj.Scope is null ? 0 : _inner.GetHashCode(obj.Scope));
    }
}
