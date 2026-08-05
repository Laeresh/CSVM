using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CSVM.Mech3.Anim;

/// <summary>Name → node resolution: the index, the wildcard matcher, the memoized <see
/// cref="FindAll"/>, and <see cref="ResolvePath"/> — generic over the node type so the whole rule
/// set is testable without an engine (a plain token type in <c>CSVM.Tests</c>) and shared, unchanged,
/// by the one engine instantiation (<c>AnimRuntime</c>'s <c>NameResolver&lt;Node3D&gt;</c>).
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
/// <para>⚠ Node identity is caller-supplied, not inherited: the constructor takes an <see
/// cref="IEqualityComparer{T}"/> that answers identity, not value equality. The engine
/// instantiation keys by instance id — Godot object equality is unreliable inside a dictionary/
/// tuple key across proxy instances of the same native node — rather than trusting
/// <typeparamref name="TNode"/>'s inherited <c>Equals</c>.</para></summary>
public sealed class NameResolver<TNode>
    where TNode : class
{
    private readonly List<IndexRow> _index = new();

    private readonly Dictionary<TNode, TNode?> _parentOf;

    private readonly Dictionary<string, Func<string, bool>> _matcherCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<(string Pattern, TNode? Scope), List<TNode>> _findCache;

    private readonly IEqualityComparer<TNode> _identity;

    public NameResolver(IEqualityComparer<TNode>? identity = null)
    {
        _identity = identity ?? EqualityComparer<TNode>.Default;
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
    /// its compiled gamez-index slot when it has one. <paramref name="gamezIndex"/> is recorded but
    /// not otherwise consulted by this class; it exists so a symbol-table authority keyed on it has
    /// somewhere to read from without a second indexing pass.</summary>
    public void Add(TNode node, string srcName, TNode? parent, int? gamezIndex = null)
    {
        _index.Add(new IndexRow(node, srcName, gamezIndex));
        _parentOf[node] = parent;
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

    /// <summary>Resolves a parent→child NAME path: the first element within <paramref
    /// name="scope"/> (falling back to the whole index when that misses and <paramref
    /// name="localOnly"/> is false), then each further element inside the previous matches.
    /// </summary>
    public List<TNode> ResolvePath(IReadOnlyList<string> path, TNode? scope, bool localOnly)
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
