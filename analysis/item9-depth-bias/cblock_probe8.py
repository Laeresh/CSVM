"""Repro-pose test of the subface rule.

Same software depth probe as item9_depthprobe.py (near-plane clipping and all), but with a
`subface` term: a polygon whose `unk3` flag is set gets an extra depth bias of
SUB_OFFSET * DEPTH_BIAS_PER_LEVEL -- i.e. exactly what `GameGenSetSubfacePriorityOffset 1`
in support\init.gw does in the original, applied to the flag the original's terrain
compiler wrote.

Reports, at the recorded pose: the front/second surface pairs within the depth floor, AND
which texture actually wins the pixel -- because the point is not the flicker number, it is
that today we show the wrong layer.
"""
import sys, os, math
from collections import defaultdict
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from item9_withinmesh import surface_ranks, CAP
from item9_depthprobe import POSES, W, H, FOV_V, FAR, NEAR, view_matrix, clip_near


def tris_with_flags(ch, node_idx, xf):
    """triangles_world, but also yielding the polygon's unk3 flag."""
    n = ch.nodes[node_idx]
    mi = model_index(n)
    if mi is None or mi < 0 or mi >= len(ch.models):
        return
    mdl = ch.models[mi]
    if not isinstance(mdl, dict):
        return
    wv = [mat_xform(xf, (v["x"], v["y"], v["z"])) for v in (mdl.get("vertices") or [])]
    for p in (mdl.get("polygons") or []):
        idxs = p.get("vertex_indices") or []
        m = len(idxs)
        if m < 3:
            continue
        pri = p.get("priority", 0)
        ms = p.get("materials")
        matidx = ms[0].get("material_index") if ms else p.get("material_index")
        fl = p.get("flags") or {}
        strip = bool(fl.get("tri_strip"))
        u3 = bool(fl.get("unk3"))
        try:
            if strip:
                for i in range(m - 2):
                    a, b, c = (i, i+1, i+2) if (i & 1) == 0 else (i, i+2, i+1)
                    yield pri, matidx, u3, (wv[idxs[a]], wv[idxs[b]], wv[idxs[c]])
            else:
                for i in range(1, m - 1):
                    yield pri, matidx, u3, (wv[idxs[0]], wv[idxs[i]], wv[idxs[i+1]])
        except IndexError:
            continue


def run(chapter, sub_offset=0.0, label=""):
    pose = POSES[chapter]
    ch = Chapter(chapter)
    bn = ch.built_nodes()
    R, t = view_matrix(pose["cam"], pose["look"])
    ty = 1.0 / math.tan(math.radians(FOV_V) / 2.0)
    tx = ty / (W / H)

    zbuf = np.full((H, W), np.inf); idbuf = np.full((H, W), -1, dtype=np.int64)
    z2buf = np.full((H, W), np.inf); id2buf = np.full((H, W), -1, dtype=np.int64)
    surf_key, surf_list = {}, []

    for idx, xf in bn:
        node = ch.nodes[idx]
        if node["name"].startswith("fvol"):
            continue
        mi = model_index(node)
        if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
            continue
        rk, _ = surface_ranks(ch.models[mi])
        for pri, matidx, u3, tri in tris_with_flags(ch, idx, xf):
            rank = min(rk.get((matidx, pri), 0), CAP)
            bias = (max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05)
                    + rank * SURFACE_RANK_BIAS + idx * NODE_ORDER_BIAS
                    + (sub_offset * DEPTH_BIAS_PER_LEVEL if u3 else 0.0))
            key = (idx, matidx, pri, u3)
            sid = surf_key.get(key)
            if sid is None:
                sid = surf_key[key] = len(surf_list)
                surf_list.append((idx, node["name"], ch.tex_name(matidx), pri, rank, u3))
            v0 = (np.array(tri, float) @ R.T) + t
            v0 = v0 * (1.0 - bias)
            if np.all(v0[:, 2] < -FAR):
                continue
            for v in clip_near(v0):
                zc = -v[:, 2]
                sx = (v[:, 0] * tx / zc * 0.5 + 0.5) * W
                sy = (0.5 - v[:, 1] * ty / zc * 0.5) * H
                x0 = max(int(math.floor(sx.min())), 0); x1 = min(int(math.ceil(sx.max()))+1, W)
                y0 = max(int(math.floor(sy.min())), 0); y1 = min(int(math.ceil(sy.max()))+1, H)
                if x0 >= x1 or y0 >= y1:
                    continue
                PX, PY = np.meshgrid(np.arange(x0, x1)+0.5, np.arange(y0, y1)+0.5)
                ax, ay = sx[0], sy[0]; bx, by = sx[1], sy[1]; cx, cy = sx[2], sy[2]
                den = (by-cy)*(ax-cx) + (cx-bx)*(ay-cy)
                if abs(den) < 1e-12:
                    continue
                l1 = ((by-cy)*(PX-cx) + (cx-bx)*(PY-cy)) / den
                l2 = ((cy-ay)*(PX-cx) + (ax-cx)*(PY-cy)) / den
                l3 = 1.0 - l1 - l2
                m = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
                if not m.any():
                    continue
                iz = l1/zc[0] + l2/zc[1] + l3/zc[2]
                with np.errstate(divide="ignore", invalid="ignore"):
                    z = 1.0/iz
                sz = zbuf[y0:y1, x0:x1]; si = idbuf[y0:y1, x0:x1]
                sz2 = z2buf[y0:y1, x0:x1]; si2 = id2buf[y0:y1, x0:x1]
                closer = m & (z < sz)
                demote = closer & (si != sid) & (sz < sz2)
                sz2[demote] = sz[demote]; si2[demote] = si[demote]
                sz[closer] = z[closer]; si[closer] = sid
                second = m & (~closer) & (z < sz2) & (si != sid)
                sz2[second] = z[second]; si2[second] = sid

    covered = np.isfinite(zbuf)
    both = covered & np.isfinite(z2buf)
    rel = np.zeros_like(zbuf)
    rel[both] = (z2buf[both] - zbuf[both]) / zbuf[both]
    tight = both & (rel < 3e-6)
    print(f"== {chapter}  subface offset {sub_offset} levels {label}")
    print(f"   frame within 3e-6 of another surface: {tight.sum():,} px "
          f"({tight.sum()/(W*H):.2%})")
    won = defaultdict(int)
    for sid in np.unique(idbuf[covered]):
        s = surf_list[sid]
        won[(s[2], s[5])] += int((idbuf == sid).sum())
    print("   pixels won, by texture (subface flag in brackets):")
    for k in sorted(won, key=lambda k: -won[k])[:8]:
        print(f"      {str(k[0]):<22} unk3={str(k[1]):<5} {won[k]:>8,} px")
    pairs = defaultdict(int)
    for a, b in zip(idbuf[tight], id2buf[tight]):
        pairs[(a, b)] += 1
    print("   top front/behind pairs inside 3e-6 (front listed first):")
    for (a, b), c in sorted(pairs.items(), key=lambda kv: -kv[1])[:6]:
        sa, sb = surf_list[a], surf_list[b]
        print(f"      {c:6,} px  FRONT {sa[1]}/{sa[2]} pri{sa[3]} rank{sa[4]} unk3={sa[5]}"
              f"   BEHIND {sb[1]}/{sb[2]} pri{sb[3]} rank{sb[4]} unk3={sb[5]}")
    print()


if __name__ == "__main__":
    ch = sys.argv[1] if len(sys.argv) > 1 else "C5"
    run(ch, 0.0, "(today: unk3 ignored)")
    run(ch, 1.0, "(the original's GameGenSetSubfacePriorityOffset 1)")
