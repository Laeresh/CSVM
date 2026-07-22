"""Paper test of candidate bias schemes at both recorded poses.

Nothing here is built or run in Godot. The probe replicates the shader's depth maths, so
each scheme can be scored by the same statistic: the fraction of the frame whose two
nearest surfaces sit within a given relative depth of each other. Lower is better, but
WHICH surface ends up in front matters too (verification.md rule 4), so the winner of each
contested pair is reported as well.

Schemes:
  0  today                   node_bias = node.Index * 5e-8, SurfaceRankBias = 2e-6
  1  surface rank x10        SurfaceRankBias = 2e-5 (cap 5 -> max 1e-4, half a level)
  2  dense node rank         node_bias = denseRank(node) * 5e-6, denseRank = longest chain
                             of conflicting predecessors (order-preserving by construction)
  3  both 1 and 2
"""
import sys, os, math
from collections import defaultdict
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from item9_withinmesh import surface_ranks, CAP
import item9_depthprobe as P
from item9_conflicts import build, conflict_pairs

DENSE_STEP = 5e-6


def dense_ranks(chname):
    ch, keep, ntri, buckets, drop = build(chname)
    edges, _ = conflict_pairs(buckets)
    succ = defaultdict(list)
    for a, b in edges:
        succ[a].append(b)
    rank = {}
    for n in sorted({x for p in edges for x in p}):
        rank.setdefault(n, 0)
        for m in succ[n]:
            rank[m] = max(rank.get(m, 0), rank[n] + 1)
    return rank, len(edges)


def probe(chname, scheme, rank=None, verbose=True):
    pose = P.POSES[chname]
    ch = Chapter(chname)
    bn = ch.built_nodes()
    R, t = P.view_matrix(pose["cam"], pose["look"])
    W, H = P.W, P.H
    ty = 1.0 / math.tan(math.radians(P.FOV_V) / 2.0)
    tx = ty / (W / H)
    srb = 2e-5 if scheme in (1, 3) else SURFACE_RANK_BIAS

    zbuf = np.full((H, W), np.inf); idbuf = np.full((H, W), -1, dtype=np.int64)
    z2 = np.full((H, W), np.inf); id2 = np.full((H, W), -1, dtype=np.int64)
    skey, slist = {}, []
    for idx, xf in bn:
        node = ch.nodes[idx]
        if node["name"].startswith("fvol"):
            continue
        mi = model_index(node)
        if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
            continue
        rk, _ = surface_ranks(ch.models[mi])
        nb = ((rank.get(idx, 0) * DENSE_STEP) if scheme in (2, 3)
              else idx * (2e-6 if scheme == 4 else NODE_ORDER_BIAS))
        for pri, matidx, tri in triangles_world(ch, idx, xf):
            srank = min(rk.get((matidx, pri), 0), CAP)
            bias = max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05) + srank * srb + nb
            key = (idx, matidx, pri)
            sid = skey.get(key)
            if sid is None:
                sid = skey[key] = len(slist)
                slist.append((idx, node["name"], ch.tex_name(matidx), pri, srank))
            v0 = (np.array(tri, float) @ R.T) + t
            v0 = v0 * (1.0 - bias)
            if np.all(v0[:, 2] < -P.FAR):
                continue
            for v in P.clip_near(v0):
                zc = -v[:, 2]
                sx = (v[:, 0] * tx / zc * 0.5 + 0.5) * W
                sy = (0.5 - v[:, 1] * ty / zc * 0.5) * H
                x0 = max(int(math.floor(sx.min())), 0); x1 = min(int(math.ceil(sx.max())) + 1, W)
                y0 = max(int(math.floor(sy.min())), 0); y1 = min(int(math.ceil(sy.max())) + 1, H)
                if x0 >= x1 or y0 >= y1:
                    continue
                PX, PY = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
                ax, ay = sx[0], sy[0]; bx, by = sx[1], sy[1]; cx, cy = sx[2], sy[2]
                den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
                if abs(den) < 1e-12:
                    continue
                l1 = ((by - cy) * (PX - cx) + (cx - bx) * (PY - cy)) / den
                l2 = ((cy - ay) * (PX - cx) + (ax - cx) * (PY - cy)) / den
                l3 = 1.0 - l1 - l2
                m = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
                if not m.any():
                    continue
                iz = l1 / zc[0] + l2 / zc[1] + l3 / zc[2]
                with np.errstate(divide="ignore", invalid="ignore"):
                    z = 1.0 / iz
                sz = zbuf[y0:y1, x0:x1]; si = idbuf[y0:y1, x0:x1]
                sz2 = z2[y0:y1, x0:x1]; si2 = id2[y0:y1, x0:x1]
                closer = m & (z < sz)
                dem = closer & (si != sid) & (sz < sz2)
                sz2[dem] = sz[dem]; si2[dem] = si[dem]
                sz[closer] = z[closer]; si[closer] = sid
                sec = m & (~closer) & (z < sz2) & (si != sid)
                sz2[sec] = z[sec]; si2[sec] = sid
    both = np.isfinite(zbuf) & np.isfinite(z2)
    rel = np.full((H, W), np.inf)
    rel[both] = (z2[both] - zbuf[both]) / zbuf[both]
    res = {e: float((both & (rel < e)).sum()) / (W * H) for e in (1e-6, 3e-6, 1e-5, 3e-5)}
    m = both & (rel < 1e-5)
    pairs = defaultdict(int)
    for a, b in zip(idbuf[m], id2[m]):
        pairs[(a, b)] += 1
    top = []
    for (a, b), c in sorted(pairs.items(), key=lambda kv: -kv[1])[:3]:
        sa, sb = slist[a], slist[b]
        top.append(f"{c} px {sa[1]}/{sa[2]}(front) over {sb[1]}/{sb[2]}")
    return res, top


if __name__ == "__main__":
    for chname in ("C5", "C1B"):
        rank, nedge = dense_ranks(chname)
        print(f"=== {chname} === (conflict graph: {nedge} edges, "
              f"max dense rank {max(rank.values()) if rank else 0})")
        for scheme, label in ((0, "0 today                      "),
                              (1, "1 SurfaceRankBias 2e-6 -> 2e-5"),
                              (2, "2 dense node rank * 5e-6      "),
                              (3, "3 both                       "),
                              (4, "4 CONTROL NodeOrderBias 2e-6 ")):
            res, top = probe(chname, scheme, rank)
            print(f"  {label}  frame within 1e-6 {res[1e-6]:6.2%} | 3e-6 {res[3e-6]:6.2%} "
                  f"| 1e-5 {res[1e-5]:6.2%} | 3e-5 {res[3e-5]:6.2%}")
            for tline in top:
                print(f"        {tline}")
        print()
