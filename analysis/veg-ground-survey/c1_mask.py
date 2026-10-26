"""Writes C1's vegetation mask (CSVM/data/vegetation/C1.bin, PLAN-enhanced-vegetation C22): hedgerows along the
crop fields' edges, sparse field interiors, bare ground around the landing strips, neutral everywhere else.

usage: python c1_mask.py [out.bin] [interior_byte=2] [hedge_byte=255] [side_m=150] [end_m=600]

The mask is 1024 x 1024 bytes over world1's area (-12288..0 on Godot X and Z, row 0 at the lowest Z), each texel
classed by the ground texture under its centre (layer 0, up-facing, the walk survey.py takes).

- Fields. A crop04/crop05 texel is an edge, at hedge_byte, where it borders other ground, where the world's 256 m grid
  crosses it (the crops' texture repeats sit on that grid, their painted border along it), or where u = k + 0.5 crosses
  it (the field line painted down each repeat's middle, wherever the repeat is turned). Every other crop texel is
  interior_byte.
- Strips. Each tarmac01 (the runway texture: centre line, edge marks) piece of at least STRIP_TEXELS is a strip, boxed
  along its principal axis. A texel inside the box grown by side_m to each side and end_m off each end is 0 (bare), so
  no added stamp stands beside a strip or under its approach.
- Every other texel is 128. A texel goes below neutral only where neither it nor any of its eight neighbours is ground
  a template stamps (the forest, the river banks), so no bilinear sample there thins an original stamp, while picked
  ground beside the aprons, hangars and roads, which nothing stamps, clears with the strip.
"""
import json
import math
import os
import sys

import numpy as np
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "bl-305-clutter-uv"))
import eligibility as E  # noqa: E402

REPO = os.path.join(HERE, "..", "..")
EX = os.path.join(os.environ.get("CSVM_DATA_ROOT", REPO), "extracted")  # a worktree reads the main checkout's
N, X0, SIZE = 1024, -12288.0, 12288.0
TEXEL = SIZE / N
CROPS = ("crop04.tif", "crop05.tif")
STRIP, STRIP_TEXELS = "tarmac01.tif", 300  # about 4 ha; the smaller tarmac01 pieces are joins inside the margins


def raster():
    """Per texel centre: the index into the returned names of the ground texture there (0 none), and its u."""
    nodes, meshes, mats, _ = E.load_chapter("C1", EX)
    raw = json.load(open(os.path.join(EX, "C1", "gamez", "models.json"), encoding="utf-8"))
    names = [""]
    tex = np.zeros((N, N), np.int32)
    U = np.full((N, N), np.nan)
    centre = X0 + (np.arange(N) + 0.5) * TEXEL

    def tri(a, b, c, ua, ub, uc, t):
        i0, i1 = max(0, int((min(a[0], b[0], c[0]) - X0) / TEXEL)), min(N - 1, int((max(a[0], b[0], c[0]) - X0) / TEXEL))
        j0, j1 = max(0, int((min(a[2], b[2], c[2]) - X0) / TEXEL)), min(N - 1, int((max(a[2], b[2], c[2]) - X0) / TEXEL))
        det = (b[2] - c[2]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[2] - c[2])
        if i1 < i0 or j1 < j0 or abs(det) < 1e-9:
            return
        px, pz = np.meshgrid(centre[i0:i1 + 1], centre[j0:j1 + 1])
        l1 = ((b[2] - c[2]) * (px - c[0]) + (c[0] - b[0]) * (pz - c[2])) / det
        l2 = ((c[2] - a[2]) * (px - c[0]) + (a[0] - c[0]) * (pz - c[2])) / det
        inside = (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
        win = (slice(j0, j1 + 1), slice(i0, i1 + 1))
        if t not in names:
            names.append(t)
        tex[win][inside] = names.index(t)  # the last polygon drawn over a texel decides it, as for the survey's census
        U[win][inside] = (l1 * ua + l2 * ub + (1 - l1 - l2) * uc)[inside]

    per, ok = E.load_interp_templates(EX)
    assert ok
    stamped = set()
    for name in per["C1"]:
        t = E.parse_template(nodes, meshes, mats, name)
        if t is not None:
            stamped.add(t.ground_texture.lower())

    def walk(idx, xf):
        n = nodes[idx]
        if n.name.lower() in ("horizon", "dzpaths") or n.name.lower().startswith("fvol"):
            return
        if n.kind in ("Lod", "LoD") and n.lod_range_min != 0.0:
            return
        if n.local is not None:
            xf = E._compose(xf, n.local)
        mesh = meshes[n.model_index] if 0 <= n.model_index < len(meshes) else None
        if mesh is not None and mesh.model_type != "Facade":
            wv = [E._apply(xf, v) for v in mesh.vertices]
            rpolys = [p for p in raw[n.model_index]["polygons"] if len(p.get("vertex_indices") or []) >= 3]
            for poly, rp in zip(mesh.polygons, rpolys):
                t = (poly.layer_textures[0] or "").lower() if poly.layer_textures else ""
                uvs = rp["materials"][0].get("uv_coords") if rp.get("materials") else None
                if not t or "cloud" in t or not uvs or poly.subface:
                    continue
                vi = poly.vertex_indices
                fan = [(i, i + 1, i + 2) for i in range(len(vi) - 2)] if poly.tri_strip else [(0, i, i + 1) for i in range(1, len(vi) - 1)]
                for ia, ib, ic in fan:
                    a, b, c = wv[vi[ia]], wv[vi[ib]], wv[vi[ic]]
                    nrm = np.cross(np.subtract(b, a), np.subtract(c, a))
                    if abs(nrm[1]) >= 0.25 * math.sqrt(nrm @ nrm) > 0:
                        tri(a, b, c, uvs[ia]["u"], uvs[ib]["u"], uvs[ic]["u"], t)
        for ch in n.children:
            walk(ch, xf)

    world = next(n for n in nodes if n.kind == "World" and n.name.lower() == "world1")
    for i in list(world.children) + list(world.world_partition_roots or []):
        walk(i, E.IDENTITY)
    return tex, names, U, stamped


def ringed(m):
    """Whether a texel and all eight neighbours hold m."""
    pad = np.pad(m, 1)
    out = np.ones_like(m)
    for dj in range(3):
        for di in range(3):
            out &= pad[dj:dj + N, di:di + N]
    return out


def strips(tex, names, side, end):
    """The texels inside any strip's box grown by side metres to each side and end metres off each end, and the boxes."""
    centre = X0 + (np.arange(N) + 0.5) * TEXEL
    X, Z = np.meshgrid(centre, centre)
    parts, count = ndimage.label(tex == names.index(STRIP), structure=np.ones((3, 3)))
    zone, boxes = np.zeros((N, N), bool), []
    for p in range(1, count + 1):
        m = parts == p
        if m.sum() < STRIP_TEXELS:
            continue
        pts = np.stack([X[m], Z[m]], 1)
        mid = pts.mean(0)
        axis = np.linalg.eigh(np.cov((pts - mid).T))[1][:, 1]  # the long axis
        along, across = (pts - mid) @ axis, (pts - mid) @ np.array([-axis[1], axis[0]])
        a = (X - mid[0]) * axis[0] + (Z - mid[1]) * axis[1]
        c = -(X - mid[0]) * axis[1] + (Z - mid[1]) * axis[0]
        zone |= (a >= along.min() - end) & (a <= along.max() + end) & (c >= across.min() - side) & (c <= across.max() + side)
        boxes.append((mid.round(), np.degrees(np.arctan2(axis[1], axis[0])).round(1), round(np.ptp(along)), round(np.ptp(across))))
    return zone, boxes


def mask(tex, names, U, stamped, interior=2, hedge=255, side=150.0, end=600.0):
    crop = np.isin(tex, [names.index(t) for t in CROPS if t in names])
    lo = X0 + np.arange(N) * TEXEL
    edge = crop & ~ringed(crop)
    grid = np.floor(lo / 256.0) != np.floor((lo + TEXEL) / 256.0)
    edge |= crop & (grid[None, :] | grid[:, None])
    for axis in (0, 1):
        nxt = np.roll(U, -1, axis=axis)
        same = crop & np.roll(crop, -1, axis=axis) & (np.floor(U) == np.floor(nxt)) & (np.abs(U - nxt) < 0.2)
        same[(slice(None), -1) if axis == 1 else (-1, slice(None))] = False
        edge |= same & (np.floor(2 * U) != np.floor(2 * nxt))
    out = np.full((N, N), 128, np.uint8)
    out[crop] = interior
    out[edge] = hedge
    safe = ringed(~np.isin(tex, [names.index(t) for t in stamped if t in names]))
    zone, boxes = strips(tex, names, side, end)
    out[zone & safe] = 0
    assert not ((out < 128) & ~safe).any()
    return out, edge, zone & safe, boxes


if __name__ == "__main__":
    a = sys.argv[1:]
    path = a[0] if a else os.path.join(REPO, "CSVM", "data", "vegetation", "C1.bin")
    tex, names, U, stamped = raster()
    out, edge, cleared, boxes = mask(tex, names, U, stamped, *(int(v) for v in a[1:3]), *(float(v) for v in a[3:5]))
    out.tofile(path)
    print(f"{path}: {edge.sum()} edge texels, {cleared.sum()} cleared beside {len(boxes)} strips")
    for mid, deg, length, width in boxes:
        print(f"  strip at ({mid[0]:.0f}, {mid[1]:.0f}) heading {deg} deg, {length} x {width} m")
