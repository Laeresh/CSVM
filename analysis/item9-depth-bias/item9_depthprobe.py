"""A software depth probe for the recorded z-fight poses.

Every previous diagnosis of this bug started from a geometric hypothesis (coarse sheet vs
partition ground / a mesh fighting itself / nine stacked World children) and then looked for
data to fit it. This does the opposite: it reproduces the renderer's depth values at the
recorded camera and asks which surfaces actually land within the depth-resolution floor of
each other, on which pixels, with no coplanarity assumption at all.

Replicates:
  - Camera3D { Fov = 50 (viewer), Far = 40000 }, Godot KEEP_HEIGHT (fov is vertical)
  - SceneBuilder's bias, applied exactly as the shader does:
        VERTEX *= 1.0 - (depth_bias + node_bias)      [view space, skip_vertex_transform]
        depth_bias = clamp(priority * 2e-4, +-0.05) + min(rank,5) * 2e-6
        node_bias  = node.Index * 5e-8
  - one surface id per (node, material, priority) — i.e. per Godot surface, which is the
    granularity that carries one bias.

Reports the fraction of covered pixels where the frontmost surface and the nearest DIFFERENT
surface behind it are within a relative depth epsilon (the measured ~1e-6 floor), and which
surface pairs contribute.
"""
import sys, os, math
from collections import defaultdict
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from item9_withinmesh import surface_ranks, CAP

POSES = {
    "C5":  dict(cam=(-9533.178, 76.319, -3367.413), look=(-9451.281, 28.148, -3398.597)),
    "C1B": dict(cam=(-7698.844, 48.763, -5797.924), look=(-7749.957, -20.093, -5849.367)),
    "C3":  dict(cam=(-6151.614, 136.079, -3198.714), look=(-6150.76, 135.796, -3199.151)),
}
W, H = 640, 360
FOV_V = 50.0
FAR, NEAR = 40000.0, 0.05
EPS = [1e-7, 3e-7, 1e-6, 3e-6, 1e-5]


def view_matrix(cam, look):
    f = np.array(look, float) - np.array(cam, float)
    f /= np.linalg.norm(f)
    up = np.array([0.0, 1.0, 0.0])
    r = np.cross(f, up)
    if np.linalg.norm(r) < 1e-8:
        up = np.array([0.0, 0.0, 1.0])
        r = np.cross(f, up)
    r /= np.linalg.norm(r)
    u = np.cross(r, f)
    # Godot camera looks down -Z
    R = np.stack([r, u, -f])          # world -> view rotation
    t = -R @ np.array(cam, float)
    return R, t


def clip_near(v):
    """Clip a view-space triangle against the near plane (z <= -NEAR) and fan-triangulate
    what survives. Returns a list of 3x3 arrays. Empty when fully in front of the camera."""
    pts = []
    n = len(v)
    for i in range(n):
        cur, prv = v[i], v[i - 1]
        ci = cur[2] <= -NEAR
        pi = prv[2] <= -NEAR
        if ci:
            if not pi:
                a = (prv[2] + NEAR) / (prv[2] - cur[2])
                pts.append(prv + a * (cur - prv))
            pts.append(cur)
        elif pi:
            a = (prv[2] + NEAR) / (prv[2] - cur[2])
            pts.append(prv + a * (cur - prv))
    if len(pts) < 3:
        return []
    return [np.array([pts[0], pts[i], pts[i + 1]]) for i in range(1, len(pts) - 1)]


def run(chapter, apply_bias=True, verbose=True):
    pose = POSES[chapter]
    ch = Chapter(chapter)
    bn = ch.built_nodes()
    R, t = view_matrix(pose["cam"], pose["look"])
    ty = 1.0 / math.tan(math.radians(FOV_V) / 2.0)
    tx = ty / (W / H)

    zbuf = np.full((H, W), np.inf, dtype=np.float64)
    idbuf = np.full((H, W), -1, dtype=np.int64)
    z2buf = np.full((H, W), np.inf, dtype=np.float64)
    id2buf = np.full((H, W), -1, dtype=np.int64)

    surf_key = {}
    surf_list = []
    ntri = 0
    for idx, xf in bn:
        node = ch.nodes[idx]
        if node["name"].startswith("fvol"):
            continue
        mi = model_index(node)
        if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
            continue
        rk, _ = surface_ranks(ch.models[mi])
        for pri, matidx, tri in triangles_world(ch, idx, xf):
            rank = min(rk.get((matidx, pri), 0), CAP)
            bias = (max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05)
                    + rank * SURFACE_RANK_BIAS + idx * NODE_ORDER_BIAS) if apply_bias else 0.0
            key = (idx, matidx, pri)
            sid = surf_key.get(key)
            if sid is None:
                sid = surf_key[key] = len(surf_list)
                surf_list.append((idx, node["name"], ch.tex_name(matidx), pri, rank))
            v0 = (np.array(tri, float) @ R.T) + t         # view space
            v0 = v0 * (1.0 - bias)                        # the shader's pull toward the eye
            if np.all(v0[:, 2] < -FAR):
                continue
            # ⚠ near-plane CLIP, not cull. Skipping triangles that cross the near plane
            # silently drops exactly the large ground quads the camera stands on, which is
            # the geometry under test (docs/verification.md §4: never silently skip a case).
            for v in clip_near(v0):
                ntri += 1
                zc = -v[:, 2]
                sx = (v[:, 0] * tx / zc * 0.5 + 0.5) * W
                sy = (0.5 - v[:, 1] * ty / zc * 0.5) * H
                x0 = max(int(math.floor(sx.min())), 0); x1 = min(int(math.ceil(sx.max())) + 1, W)
                y0 = max(int(math.floor(sy.min())), 0); y1 = min(int(math.ceil(sy.max())) + 1, H)
                if x0 >= x1 or y0 >= y1:
                    continue
                xs = np.arange(x0, x1) + 0.5
                ys = np.arange(y0, y1) + 0.5
                PX, PY = np.meshgrid(xs, ys)
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
                # perspective-correct depth
                iz = l1 / zc[0] + l2 / zc[1] + l3 / zc[2]
                with np.errstate(divide="ignore", invalid="ignore"):
                    z = 1.0 / iz
                sub_z = zbuf[y0:y1, x0:x1]
                sub_id = idbuf[y0:y1, x0:x1]
                sub_z2 = z2buf[y0:y1, x0:x1]
                sub_id2 = id2buf[y0:y1, x0:x1]
                closer = m & (z < sub_z)
                # the old winner becomes the runner-up when it is a different surface
                demote = closer & (sub_id != sid) & (sub_z < sub_z2)
                sub_z2[demote] = sub_z[demote]
                sub_id2[demote] = sub_id[demote]
                sub_z[closer] = z[closer]
                sub_id[closer] = sid
                # not closer, but nearer than the current runner-up and a different surface
                second = m & (~closer) & (z < sub_z2) & (sub_id != sid)
                sub_z2[second] = z[second]
                sub_id2[second] = sid

    covered = np.isfinite(zbuf)
    both = covered & np.isfinite(z2buf)
    if verbose:
        print(f"== {chapter} depth probe ({'with' if apply_bias else 'WITHOUT'} bias) ==")
        print(f"  triangles rasterised {ntri:,}, surfaces {len(surf_list):,}")
        print(f"  covered pixels {covered.sum():,}/{W*H:,} ({covered.mean():.1%}); "
              f"with a second surface behind: {both.sum():,} ({both.mean():.1%})")
    rel = np.zeros_like(zbuf)
    rel[both] = (z2buf[both] - zbuf[both]) / zbuf[both]
    out = {}
    for e in EPS:
        m = both & (rel < e)
        out[e] = m.sum() / (W * H)
        if verbose:
            print(f"  pixels where the two nearest surfaces are within {e:.0e} of view "
                  f"distance: {m.sum():,} ({m.sum()/(W*H):.2%} of frame)")
    if verbose:
        m = both & (rel < 3e-6)
        pairs = defaultdict(int)
        ii = idbuf[m]; jj = id2buf[m]
        for a, b in zip(ii, jj):
            pairs[(min(a, b), max(a, b))] += 1
        print("  top contributing surface pairs at the 3e-6 threshold "
              f"({m.sum():,} px, {m.sum()/(W*H):.2%}):")
        for (a, b), c in sorted(pairs.items(), key=lambda kv: -kv[1])[:12]:
            sa, sb = surf_list[a], surf_list[b]
            if sa[0] == sb[0]:
                same = ("SAME NODE, same surface" if sa[2] == sb[2] and sa[3] == sb[3]
                        else f"SAME NODE, rank step {abs(sa[4]-sb[4])} -> {abs(sa[4]-sb[4])*SURFACE_RANK_BIAS:.1e}")
            else:
                dd = abs(sa[0]-sb[0])
                same = f"cross-node delta {dd} -> {dd*NODE_ORDER_BIAS:.2e} ({dd*NODE_ORDER_BIAS/DEPTH_FLOOR:.2f}x floor)"
            print(f"    {c:6d} px  {sa[1]}/{sa[2]} pri{sa[3]} rank{sa[4]}  vs  "
                  f"{sb[1]}/{sb[2]} pri{sb[3]} rank{sb[4]}   [{same}]")
    return out


if __name__ == "__main__":
    for c in (sys.argv[1:] or ["C5", "C1B"]):
        run(c, apply_bias=True)
        print()
