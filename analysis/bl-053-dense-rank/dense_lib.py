"""BL-053 — the cross-node conflict relation as TODAY's build path produces it.

Re-measurement of `analysis/item9-depth-bias/`'s conflict graph, updated for the two
build-path changes that landed after it (2026-07-22 → today):

  * `WorldBuilder.Add` now skips a root whose gamez `flags.active` is false (A1 / BL-051),
    so the built-node set is smaller than item9 measured.
  * `SceneBuilder` now separates a SUBFACE polygon from its base by `SubfaceBias`
    (0.5 of a priority level = 50 rank steps). A base/subface pair is therefore NOT a
    conflict any more, and item9's counts still include those pairs.

Also unlike item9: the surface key is the real one (`material, priority, subface,
doubleSided`), and the winner test uses the two SPECIFIC conflicting surfaces' ranks
rather than each node's max rank — item9's §5 table was explicitly approximate there.

Read-only. Nothing is built, no Godot is launched.
"""
import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                "..", "item9-depth-bias"))
from item9_lib import *  # noqa: F401,F403  (loader + clipping, shared with item9)

# SceneBuilder.cs, today
SUBFACE_BIAS = DEPTH_BIAS_PER_LEVEL * 0.5

NORM_TOL = 1e-4
D_TOL = 1e-3
MIN_OVERLAP = 1.0        # m^2 — same threshold item9 used

# WorldBuilder.SkipWorldNode
SKIP_EXACT = {"horizon", "dzpaths"}
SKIP_PREFIX = ("fvol",)


def lod_range_min(node):
    """A Lod node's near range, from the shape the unified extraction actually ships:
    `data.Lod.range.min`.

    ⚠ `item9_lib.built_nodes` reads `range_near`/`range_min`, neither of which exists — so it
    returns None and keeps EVERY level. Every conflict count `analysis/item9-depth-bias/`
    reports therefore includes far-LOD geometry the engine never builds; that is most of the
    gap between its numbers and this directory's."""
    body = next(iter(node["data"].values()))
    rng = body.get("range")
    if isinstance(rng, dict):
        return rng.get("min", 0.0)
    return body.get("range_near", body.get("range_min", 0.0)) or 0.0


def _skipped(node):
    nm = (node.get("name") or "").lower()
    return nm in SKIP_EXACT or nm.startswith(SKIP_PREFIX)


def built_nodes_today(ch):
    """(node_index, world_transform) for every node the world build actually reaches.

    Mirrors WorldBuilder.Build -> Add -> SceneBuilder.BuildSubtree: the `active` skip is
    applied at ROOT level only (that is where WorldBuilder.Add tests it), the name skip and
    the non-nearest-LOD skip prune whole subtrees.
    """
    out = []
    seen = set()
    by_root = {}

    def rec(idx, xf, bucket):
        if idx in seen:
            return
        seen.add(idx)
        n = ch.nodes[idx]
        if _skipped(n):
            return
        if node_kind(n) == "Lod" and lod_range_min(n) != 0.0:
            return  # SceneBuilder.BuildSubtree keeps only the nearest level
        loc = node_local(n)
        cur = mat_mul(xf, loc) if loc else xf
        out.append((idx, cur))
        bucket.append((idx, cur))
        for c in (n.get("child_indices") or n.get("children") or []):
            if 0 <= c < len(ch.nodes):
                rec(c, cur, bucket)

    inactive_roots = 0
    for r in ch.walk_roots():
        if not (0 <= r < len(ch.nodes)):
            continue
        if (ch.nodes[r].get("flags") or {}).get("active", True) is False:
            inactive_roots += 1
            continue
        bucket = by_root.setdefault(r, [])
        rec(r, mat_identity(), bucket)
    return out, inactive_roots, by_root


def parked_at_origin_roots(ch, nodes_by_root):
    """Walk roots WorldBuilder.HideUnplacedEntities switches off at bootstrap:
    no authored local transform, and a subtree AABB that strictly straddles the origin in
    x and z (the map's corner — real terrain only ever touches it). Mirrors
    WorldBuilder.IsParkedAtOrigin."""
    parked = set()
    for root, members in nodes_by_root.items():
        if node_local(ch.nodes[root]) is not None:
            continue
        lo = [float("inf")] * 3
        hi = [float("-inf")] * 3
        for idx, xf in members:
            for _k, _r, tri in triangles_with_surface(ch, idx, xf):
                for v in tri:
                    for a in range(3):
                        lo[a] = min(lo[a], v[a])
                        hi[a] = max(hi[a], v[a])
        if lo[0] == float("inf"):
            continue
        if lo[0] < 0 < hi[0] and lo[2] < 0 < hi[2]:
            parked.add(root)
    return parked


def is_marker_gizmo(ch, mdl):
    """One flat-coloured untextured triangle: a level-editor anchor mark the original never
    draws and `SceneBuilder` builds no mesh for. Mirrors `GameZ.IsMarkerGizmo`."""
    polys = mdl.get("polygons") or []
    if len(mdl.get("vertices") or []) != 3 or len(polys) != 1:
        return False
    p = polys[0]
    if len(p.get("vertex_indices") or []) != 3:
        return False
    mats = p.get("materials")
    mi = mats[0].get("material_index") if mats else p.get("material_index")
    return ch.tex_name(mi) is None


def surface_key(poly):
    """SceneBuilder's base-pass group key: (material, priority, subface, doubleSided).

    World builds run `cullBackfaces: true` with `forceDoubleSided` only on cloud-deck
    nodes, so doubleSided == the polygon's own SHOW_BACKFACE flag.
    """
    fl = poly.get("flags") or {}
    mats = poly.get("materials")
    matidx = mats[0].get("material_index") if mats else poly.get("material_index")
    pri = poly.get("priority", poly.get("unk04", 0))
    subface = fl.get("unk3", False) is True
    ds = (fl.get("unk2") or fl.get("show_backface") or False) is True
    return (matidx, pri, subface, ds)


def surface_ranks_today(mdl):
    """First-occurrence order of each base-pass group in the polygon list, as
    SceneBuilder.BuildMesh assigns it. Returns {key: rank} uncapped."""
    order = {}
    for p in (mdl.get("polygons") or []):
        if len((p.get("vertex_indices") or [])) < 3:
            continue
        k = surface_key(p)
        if k not in order:
            order[k] = len(order)
    return order


def triangles_with_surface(ch, node_idx, xf):
    """(surface_key, rank_capped, triangle) in world space, triangulated as
    SceneBuilder.EmitPolygon does. Base pass only — an overlay pass draws on the same
    triangles as its base and so adds no new cross-node geometry."""
    n = ch.nodes[node_idx]
    mi = model_index(n)
    if mi is None or mi < 0 or mi >= len(ch.models):
        return
    mdl = ch.models[mi]
    if not isinstance(mdl, dict):
        return
    if is_marker_gizmo(ch, mdl):
        return
    ranks = surface_ranks_today(mdl)
    verts = mdl.get("vertices") or []
    wv = [mat_xform(xf, (v["x"], v["y"], v["z"])) for v in verts]
    for p in (mdl.get("polygons") or []):
        idxs = p.get("vertex_indices") or []
        m = len(idxs)
        if m < 3:
            continue
        k = surface_key(p)
        rank = min(ranks[k], SURFACE_RANK_CAP)
        fl = p.get("flags") or {}
        strip = bool(fl.get("tri_strip") or fl.get("triangle_strip"))
        try:
            if strip:
                for i in range(m - 2):
                    a, b, c = (i, i + 1, i + 2) if (i & 1) == 0 else (i, i + 2, i + 1)
                    yield k, rank, (wv[idxs[a]], wv[idxs[b]], wv[idxs[c]])
            else:
                for i in range(1, m - 1):
                    yield k, rank, (wv[idxs[0]], wv[idxs[i]], wv[idxs[i + 1]])
        except IndexError:
            continue


def quant(n, d):
    return (round(n[0] / NORM_TOL), round(n[1] / NORM_TOL), round(n[2] / NORM_TOL),
            round(d / D_TOL))


def coplanar_pairs(ch, nodes):
    """Every cross-node coplanar OVERLAPPING surface pair, whatever their priorities.

    Bucketed by world plane alone — priority and subface stay on the record instead of
    joining the key, so the result covers both questions this item has to answer:

      * equal priority + equal subface  -> a CONFLICT the tie-break must resolve
      * anything else                   -> a HIERARCHY pair, where the authored layering
        (higher priority in front; a subface in front of its base) must survive whatever
        the tie-break adds

    Returns (pairs, triangle_count) where pairs is
    {(lo_node, hi_node): {(surface_a, surface_b): area}} and a surface is
    (node_index, (material, priority, subface, doubleSided), capped_rank).
    """
    buckets = defaultdict(list)
    ntri = 0
    for idx, xf in nodes:
        for k, rank, tri in triangles_with_surface(ch, idx, xf):
            ntri += 1
            pl = plane_of(tri)
            if pl is None:
                continue
            n, d = pl
            buckets[quant(n, d)].append((idx, tri, n, k, rank))

    pairs = defaultdict(lambda: defaultdict(float))
    for key, items in buckets.items():
        if len({it[0] for it in items}) < 2:
            continue
        u, v = project_basis(items[0][2])
        recs = []
        for (idx, tri, _n, k, rank) in items:
            t2 = ensure_ccw(to2d(tri, u, v))
            xs = [p[0] for p in t2]
            ys = [p[1] for p in t2]
            recs.append((idx, t2, min(xs), min(ys), max(xs), max(ys), k, rank))
        recs.sort(key=lambda r: r[2])
        for i in range(len(recs)):
            ai, a2, ax0, ay0, ax1, ay1, ak, ar = recs[i]
            for j in range(i + 1, len(recs)):
                bi, b2, bx0, by0, bx1, by1, bk, br = recs[j]
                if bx0 >= ax1:
                    break
                if ai == bi or ay1 <= by0 or by1 <= ay0:
                    continue
                ov = clip_area(a2, b2)
                if ov <= MIN_OVERLAP:
                    continue
                sa, sb = (ai, ak, ar), (bi, bk, br)
                if ai > bi:
                    sa, sb = sb, sa
                pairs[(sa[0], sb[0])][(sa, sb)] += ov
    return pairs, ntri


def is_conflict(sa, sb):
    """Same priority and same subface flag: nothing but the tie-break separates them."""
    return sa[1][1] == sb[1][1] and sa[1][2] == sb[1][2]


def conflict_edges(ch, nodes):
    """The conflict subset of coplanar_pairs — what the dense rank is computed over."""
    pairs, ntri = coplanar_pairs(ch, nodes)
    edges = defaultdict(lambda: defaultdict(float))
    for np_, surfs in pairs.items():
        for (sa, sb), area in surfs.items():
            if is_conflict(sa, sb):
                edges[np_][(sa, sb)] += area
    return edges, ntri


def dense_ranks(edges):
    """Longest-path layering over the conflict DAG. Every edge runs low node index -> high
    node index, so this is a topological layering and cannot invert authored order."""
    succ = defaultdict(list)
    nodes = set()
    for a, b in edges:
        succ[a].append(b)
        nodes.add(a)
        nodes.add(b)
    rank = {}
    for n in sorted(nodes):
        rank.setdefault(n, 0)
        for m in succ[n]:
            rank[m] = max(rank.get(m, 0), rank[n] + 1)
    return rank


def inversions(edges, rank=None, step=None, node_order_bias=NODE_ORDER_BIAS,
               surface_rank_bias=SURFACE_RANK_BIAS):
    """How many conflicting pairs resolve the wrong way round.

    The later node (higher flat index = drawn later by the original) must end up IN FRONT,
    i.e. carry the larger total bias. Priority and subface are equal within a conflict by
    construction, so only the rank term and the cross-node term decide it.

    `rank`/`step` given -> the cross-node term is denseRank * step; otherwise it is the
    current node.Index * NodeOrderBias.
    """
    def cross(idx):
        if rank is not None:
            return rank.get(idx, 0) * step
        return idx * node_order_bias

    inv_pairs = tie_pairs = 0
    inv_surf = tie_surf = total_surf = 0
    for (a, b), surfs in edges.items():
        worst = None
        for (sa, sb), area in surfs.items():
            total_surf += 1
            da = sa[2] * surface_rank_bias + cross(sa[0])
            db = sb[2] * surface_rank_bias + cross(sb[0])
            if db < da:
                inv_surf += 1
            elif db == da:
                tie_surf += 1
            if worst is None or area > worst[0]:
                worst = (area, db - da)
        if worst[1] < 0:
            inv_pairs += 1
        elif worst[1] == 0:
            tie_pairs += 1
    return dict(pairs=len(edges), inverted=inv_pairs, tied=tie_pairs,
                surfaces=total_surf, inv_surf=inv_surf, tie_surf=tie_surf)


def longest_chain(edges):
    succ = defaultdict(list)
    nodes = set()
    for a, b in edges:
        succ[a].append(b)
        nodes.add(a)
        nodes.add(b)
    best = {}
    for n in sorted(nodes, reverse=True):
        best[n] = max((best[m] + 1 for m in succ[n]), default=1)
    return max(best.values(), default=0)
