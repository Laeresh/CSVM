"""BL-053 — per-chapter conflict census and the wrong-way-round count, today vs a dense rank.

Usage:  python census.py [CHAPTER ...] [--step 5e-6]

Prints, per chapter:
  built nodes / triangles, conflicting cross-node pairs, the longest index-ordered chain
  (= dense rank slots needed), and how many pairs resolve the WRONG way round today and
  under a dense conflict rank at the candidate step.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dense_lib import *  # noqa: F401,F403

STEP = 5e-6


def run(chname, step):
    t0 = time.time()
    ch = Chapter(chname)
    nodes, inactive, _by_root = built_nodes_today(ch)
    edges, ntri = conflict_edges(ch, nodes)
    chain = longest_chain(edges)
    rank = dense_ranks(edges)
    today = inversions(edges)
    dense = inversions(edges, rank=rank, step=step)
    row = dict(chapter=chname, nodes=len(nodes), inactive_roots=inactive, tris=ntri,
               pairs=len(edges), chain=chain,
               max_dense=max(rank.values(), default=0),
               inv_today=today["inverted"], tie_today=today["tied"],
               inv_dense=dense["inverted"], tie_dense=dense["tied"],
               surf=today["surfaces"], inv_surf_today=today["inv_surf"],
               tie_surf_today=today["tie_surf"], inv_surf_dense=dense["inv_surf"],
               tie_surf_dense=dense["tie_surf"], secs=round(time.time() - t0, 1))
    print(f"== {chname} ==  ({row['secs']}s)")
    print(f"   built nodes {row['nodes']:,} (inactive roots skipped: {inactive}), "
          f"triangles {ntri:,}")
    print(f"   conflicting cross-node pairs: {row['pairs']:,}   "
          f"longest chain {chain} (dense ranks used: {row['max_dense'] + 1})")
    print(f"   wrong way round TODAY : {row['inv_today']:,} pairs "
          f"({row['inv_today'] / max(row['pairs'], 1):.1%}), tied {row['tie_today']:,}"
          f"   | surface pairs {row['inv_surf_today']:,}+{row['tie_surf_today']:,} tied "
          f"of {row['surf']:,}")
    print(f"   wrong way round DENSE : {row['inv_dense']:,} pairs "
          f"({row['inv_dense'] / max(row['pairs'], 1):.1%}), tied {row['tie_dense']:,}"
          f"   | surface pairs {row['inv_surf_dense']:,}+{row['tie_surf_dense']:,} tied "
          f"of {row['surf']:,}")
    return row


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    step = STEP
    if "--step" in sys.argv:
        step = float(sys.argv[sys.argv.index("--step") + 1])
    rows = [run(c, step) for c in (args or CHAPTERS)]
    print()
    print(f"dense step {step:.0e}")
    print("chapter | nodes | tris    | pairs | chain | inv today    | inv dense")
    for r in rows:
        print(f"{r['chapter']:7s} | {r['nodes']:5d} | {r['tris']:7d} | {r['pairs']:5d} | "
              f"{r['chain']:5d} | {r['inv_today']:5d} ({r['inv_today']/max(r['pairs'],1):5.1%}) | "
              f"{r['inv_dense']:5d} ({r['inv_dense']/max(r['pairs'],1):5.1%})")
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "census.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(dict(step=step, rows=rows), f, indent=1)
    print(f"-> {out}")
