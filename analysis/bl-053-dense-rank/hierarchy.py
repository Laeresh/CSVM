"""BL-053 — does the cross-node tie-break stay UNDER one authored priority level?

`docs/architecture.md` recorded the current answer as an accepted corner case: "a prio-0
node >~4000 indices later can out-bias a prio-1 overlay; no such pair observed in C1". This
measures it instead of assuming it, over every cross-node coplanar overlapping surface pair
in all eight chapters, and re-measures it for a dense conflict rank at a candidate step.

Two verdicts per pair:
  CONFLICT  — same priority, same subface: only the tie-break separates them, and the later
              node (higher flat index = drawn later by the original) must win.
  HIERARCHY — different priority or different subface: the authored layering decides, and
              the tie-break must not be able to overturn it.

Usage:  python hierarchy.py [CHAPTER ...] [--step 1.2e-5]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dense_lib import *  # noqa: F401,F403

STEP = 1.2e-5


def intended_front(sa, sb):
    """Which of the two the original draws on top: +1 = sb, -1 = sa.
    Priority first, then SUBFACE (coplanar-and-contained draws over its base), then the
    flat node index (nodes.json is a DFS serialization = draw order, later wins)."""
    if sa[1][1] != sb[1][1]:
        return 1 if sb[1][1] > sa[1][1] else -1
    if sa[1][2] != sb[1][2]:
        return 1 if sb[1][2] else -1
    return 1 if sb[0] > sa[0] else -1


def total_bias(s, cross):
    _idx, (_mat, pri, subface, _ds), rank = s
    b = max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05) + rank * SURFACE_RANK_BIAS
    if subface:
        b += SUBFACE_BIAS
    return b + cross


def score(pairs, cross_of):
    out = dict(conflict=0, conflict_wrong=0, conflict_tied=0,
               hierarchy=0, hierarchy_wrong=0, hierarchy_tied=0,
               worst=None)
    for _np, surfs in pairs.items():
        for (sa, sb), area in surfs.items():
            want = intended_front(sa, sb)
            got = total_bias(sb, cross_of(sb[0])) - total_bias(sa, cross_of(sa[0]))
            kind = "conflict" if is_conflict(sa, sb) else "hierarchy"
            out[kind] += 1
            if got == 0:
                out[kind + "_tied"] += 1
            elif (got > 0) != (want > 0):
                out[kind + "_wrong"] += 1
                if kind == "hierarchy" and (out["worst"] is None or area > out["worst"][0]):
                    out["worst"] = (area, sa, sb)
    return out


def run(chname, step):
    t0 = time.time()
    ch = Chapter(chname)
    nodes, inactive, by_root = built_nodes_today(ch)
    parked_roots = parked_at_origin_roots(ch, by_root)
    parked = {i for r in parked_roots for i, _ in by_root[r]}
    pairs, ntri = coplanar_pairs(ch, nodes)

    # Visible pairs only: WorldBuilder.HideUnplacedEntities switches the origin-parked pile
    # off at bootstrap, so nothing in it is on screen to fight.
    vis = {k: v for k, v in pairs.items() if k[0] not in parked and k[1] not in parked}
    edges = {k: {sp: a for sp, a in v.items() if is_conflict(*sp)} for k, v in vis.items()}
    edges = {k: v for k, v in edges.items() if v}
    rank = dense_ranks(edges)

    today = score(vis, lambda i: i * NODE_ORDER_BIAS)
    dense = score(vis, lambda i: rank.get(i, 0) * step)
    maxrank = max(rank.values(), default=0)
    print(f"== {chname} == ({time.time()-t0:.1f}s) built {len(nodes):,} nodes "
          f"({len(parked):,} parked+hidden), {ntri:,} triangles")
    print(f"   visible cross-node coplanar overlapping surface pairs: "
          f"{today['conflict'] + today['hierarchy']:,} "
          f"({today['conflict']:,} conflict / {today['hierarchy']:,} hierarchy)")
    print(f"   dense ranks needed: {maxrank + 1} (max rank {maxrank}) -> tie-break span "
          f"{maxrank * step + SURFACE_RANK_CAP * SURFACE_RANK_BIAS:.2e} "
          f"of the {DEPTH_BIAS_PER_LEVEL:.0e} level")
    for label, r in (("today", today), ("dense", dense)):
        print(f"   {label}: conflict wrong {r['conflict_wrong']:5d} "
              f"(+{r['conflict_tied']} tied) | hierarchy wrong {r['hierarchy_wrong']:5d} "
              f"(+{r['hierarchy_tied']} tied)")
        if r["worst"]:
            a, sa, sb = r["worst"]
            print(f"       worst hierarchy break: {a:,.0f} m2  "
                  f"{ch.nodes[sa[0]]['name']}(idx {sa[0]}, pri {sa[1][1]}, "
                  f"sub {sa[1][2]}, rank {sa[2]}) vs "
                  f"{ch.nodes[sb[0]]['name']}(idx {sb[0]}, pri {sb[1][1]}, "
                  f"sub {sb[1][2]}, rank {sb[2]})")
    return dict(chapter=chname, nodes=len(nodes), parked=len(parked), tris=ntri,
                maxrank=maxrank, today=today_clean(today), dense=today_clean(dense),
                secs=round(time.time() - t0, 1))


def today_clean(r):
    return {k: v for k, v in r.items() if k != "worst"}


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    step = STEP
    if "--step" in sys.argv:
        step = float(sys.argv[sys.argv.index("--step") + 1])
    rows = [run(c, step) for c in (args or CHAPTERS)]
    print()
    print(f"dense step {step:.1e}")
    print("chapter | pairs  | conflict wrong today -> dense | hierarchy wrong today -> dense")
    for r in rows:
        t, d = r["today"], r["dense"]
        print(f"{r['chapter']:7s} | {t['conflict'] + t['hierarchy']:6d} | "
              f"{t['conflict_wrong']:9d} (+{t['conflict_tied']:3d} tied) -> {d['conflict_wrong']:4d} "
              f"(+{d['conflict_tied']:3d}) | {t['hierarchy_wrong']:9d} -> {d['hierarchy_wrong']:4d}")
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "hierarchy.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(dict(step=step, rows=rows), f, indent=1)
    print(f"-> {out}")
