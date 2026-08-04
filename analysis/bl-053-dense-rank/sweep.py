"""BL-053 — the sizing question: which dense-rank step both DOMINATES the within-mesh rank
term and still fits inside one priority level.

Two constraints pull against each other:

  dominance   step > SurfaceRankCap * SurfaceRankBias   (else a within-mesh rank of 5 can
                                                         still out-bid a one-slot cross-node
                                                         step, which is the BL-053 defect)
  budget      maxDenseRank * step + SurfaceRankCap * SurfaceRankBias < DepthBiasPerLevel
                                                        (else the tie-break leaks into a real
                                                         authored priority step)

`maxDenseRank` is the longest index-ordered chain in the conflict DAG, which is dominated by
the origin-parked entity pile (WorldBuilder.HideUnplacedEntities switches those off at
bootstrap). Reported both ways.

Usage:  python sweep.py [CHAPTER ...]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dense_lib import *  # noqa: F401,F403

STEPS = (2e-6, 5e-6, 1e-5, 1.5e-5, 2e-5, 2.5e-5)


def measure(chname):
    t0 = time.time()
    ch = Chapter(chname)
    nodes, inactive, by_root = built_nodes_today(ch)
    parked = parked_at_origin_roots(ch, by_root)
    parked_nodes = {i for r in parked for i, _ in by_root[r]}
    edges, ntri = conflict_edges(ch, nodes)
    kept = {k: v for k, v in edges.items()
            if k[0] not in parked_nodes and k[1] not in parked_nodes}
    row = dict(chapter=chname, nodes=len(nodes), inactive=inactive, tris=ntri,
               parked_roots=len(parked), parked_nodes=len(parked_nodes),
               pairs_all=len(edges), pairs_kept=len(kept),
               chain_all=longest_chain(edges), chain_kept=longest_chain(kept),
               secs=round(time.time() - t0, 1))
    rank_all = dense_ranks(edges)
    rank_kept = dense_ranks(kept)
    row["maxrank_all"] = max(rank_all.values(), default=0)
    row["maxrank_kept"] = max(rank_kept.values(), default=0)
    row["today"] = inversions(edges)
    row["today_kept"] = inversions(kept)
    row["by_step"] = {}
    for s in STEPS:
        row["by_step"][f"{s:.1e}"] = dict(
            all=inversions(edges, rank=rank_all, step=s),
            kept=inversions(kept, rank=rank_kept, step=s))
    print(f"== {chname} == ({row['secs']}s) nodes {len(nodes):,} tris {ntri:,} "
          f"| parked roots {len(parked)} ({len(parked_nodes)} nodes)")
    print(f"   pairs {len(edges):,} (chain {row['chain_all']}) -> excluding parked "
          f"{len(kept):,} (chain {row['chain_kept']})")
    print(f"   wrong way round today: {row['today']['inverted']} of {len(edges):,} "
          f"| excluding parked {row['today_kept']['inverted']} of {len(kept):,}")
    for s in STEPS:
        r = row["by_step"][f"{s:.1e}"]
        print(f"     step {s:.1e}: dense inv {r['all']['inverted']:4d} "
              f"(+{r['all']['tied']} tied) | excl. parked {r['kept']['inverted']:4d} "
              f"(+{r['kept']['tied']} tied)")
    return row


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    rows = [measure(c) for c in (args or CHAPTERS)]
    cap_budget = SURFACE_RANK_CAP * SURFACE_RANK_BIAS
    print()
    print(f"dominance needs step > {cap_budget:.1e} (SurfaceRankCap x SurfaceRankBias)")
    print(f"budget is {DEPTH_BIAS_PER_LEVEL:.1e} (one priority level)")
    print()
    print("chapter | pairs | chain | chain-noparked | inv today | inv today-noparked")
    for r in rows:
        print(f"{r['chapter']:7s} | {r['pairs_all']:5d} | {r['chain_all']:5d} | "
              f"{r['chain_kept']:14d} | {r['today']['inverted']:9d} | "
              f"{r['today_kept']['inverted']:18d}")
    print()
    mx_all = max(r["maxrank_all"] for r in rows)
    mx_kept = max(r["maxrank_kept"] for r in rows)
    print(f"worst dense rank across chapters: {mx_all} (all) / {mx_kept} (excluding parked)")
    for s in STEPS:
        fits_all = mx_all * s + cap_budget < DEPTH_BIAS_PER_LEVEL
        fits_kept = mx_kept * s + cap_budget < DEPTH_BIAS_PER_LEVEL
        inv_all = sum(r["by_step"][f"{s:.1e}"]["all"]["inverted"] for r in rows)
        inv_kept = sum(r["by_step"][f"{s:.1e}"]["kept"]["inverted"] for r in rows)
        print(f"  step {s:.1e}: dominates {'yes' if s > cap_budget else 'NO '} | "
              f"budget all {'fits' if fits_all else 'OVER'} "
              f"({mx_all * s + cap_budget:.2e}) inv {inv_all:5d} | "
              f"budget no-parked {'fits' if fits_kept else 'OVER'} "
              f"({mx_kept * s + cap_budget:.2e}) inv {inv_kept:5d}")
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sweep.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(rows, f, indent=1)
    print(f"-> {out}")
