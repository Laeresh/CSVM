"""Checks every windowed wall of each city model (CSVM/data/city_models/<chapter>/<model>.glb.bin)
for windows cut at a vertical edge: an outer or inner corner, the end of a set-back wall, a light
well's edge, a step in the massing.

A wall triangle is windowed when it is class 0, its v is height over the tile (not the solid band),
its u runs across it, and its height reaches a floor's glass. Each vertical edge of such a triangle
where the wall does not go on with the same u (a corner, a roof, a strip of solid wall) must fall
on a bay boundary of every facade row of the building's family (city_atlas.py's FAMILIES), within
TOL metres, since the shader picks the row from the building's seed, or at least CLEAR metres from
every window frame of a row (a dormer front, two metres of the tile, ends inside the paired row's
gap); those are counted as clear. It also checks that no window column (integer u) is shared by two
wall planes of one mesh, and prints each kind's largest u stretch against 1 m of wall per metre of
tile and its solid strips (class 0 wall at one u that reaches a floor's glass) with --strips.
Exits 1 on any edge or shared column.

    py -I analysis/city-hulls/corners.py [--dir=<folder of .glb.bin>] [--verbose] [--strips] [cb12a ...]
"""
import math
import os
import sys

sys.dont_write_bytecode = True

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(REPO, "CSVM", "data", "city_models"))
import city_atlas as A  # noqa: E402
from margin import read_glb  # noqa: E402

TW, FLOOR, SOLID = A.TILE_M, A.FLOOR_M, A.SOLID_M
TOL = 0.05
CLEAR = 0.1


def glassy(y0, y1):
    k0, k1 = int(math.floor(y0 / FLOOR)), int(math.floor(y1 / FLOOR))
    return any(min(y1, (k + 1) * FLOOR - SOLID) - max(y0, k * FLOOR + SOLID) > 0.1 for k in range(k0, k1 + 1))


def walls(pos, uv0, uv1, tri):
    """Per horizontal-normal triangle of class 0 or 3: (plane key, right, s, y, u, whether the wall goes on
    through it (windowed, or the cornice line), family, windowed, a straight-mapped wall)."""
    out = []
    for t in tri:
        P, U, C = pos[t], uv0[t], uv1[t]
        cls = int(round(C[0, 1]))
        n = np.cross(P[1] - P[0], P[2] - P[0])
        ln = np.linalg.norm(n)
        if cls not in (0, 3) or ln < 1e-9 or abs(n[1]) > 1e-4 * ln:
            continue
        n = n / ln
        right = np.cross(-n, (0.0, 1.0, 0.0))
        s, y, u = P @ right, P[:, 1], U[:, 0]
        straight = np.all(np.abs(U[:, 1] - y / TW) < 1e-4)
        windowed = cls == 0 and straight and np.ptp(u) > 1e-4 and glassy(y.min(), y.max())
        key = (round(float(n[0]), 3), round(float(n[2]), 3), round(float(P[0] @ n), 2))
        out.append((key, right, s, y, u, windowed or cls == 3, int(round(C[0, 0])), windowed, cls == 0 and straight))
    return out


def bary(s, y, qs, qy):
    d = (y[1] - y[2]) * (s[0] - s[2]) + (s[2] - s[1]) * (y[0] - y[2])
    if abs(d) < 1e-12:
        return None
    a = ((y[1] - y[2]) * (qs - s[2]) + (s[2] - s[1]) * (qy - y[2])) / d
    b = ((y[2] - y[0]) * (qs - s[2]) + (s[0] - s[2]) * (qy - y[2])) / d
    w = np.array((a, b, 1 - a - b))
    return w if np.all(w >= -1e-6) else None


def edge_faults(faces):
    """Vertical edges of windowed triangles that end the windowed wall off every row's bay grid."""
    by_plane = {}
    for f in faces:
        by_plane.setdefault(f[0], []).append(f)
    faults, seen = [], set()
    for key, right, s, y, u, _, family, windowed, _ in faces:
        if not windowed:
            continue
        for i, j in ((0, 1), (1, 2), (2, 0)):
            if abs(s[i] - s[j]) > 1e-4 or abs(y[i] - y[j]) < 1e-4:
                continue
            k = 3 - i - j
            out = 1.0 if s[i] > s[k] else -1.0
            ue = (u[i] + u[j]) / 2
            ya, yb = sorted((y[i], y[j]))
            tag = (key, round(s[i], 3), round(ya, 3), round(yb, 3))
            if tag in seen:
                continue
            seen.add(tag)
            go_on = True
            for f in (0.2, 0.5, 0.8):
                qs, qy = s[i] + out * 0.02, ya + f * (yb - ya)
                hit = False
                for _, _, s2, y2, u2, cont, _, _, _ in by_plane[key]:
                    w = bary(s2, y2, qs, qy)
                    if cont and w is not None and abs(float(w @ u2) - ue) < 2e-3 + 0.02 / TW * 1.2:
                        hit = True
                        break
                if not hit:
                    go_on = False
                    break
            if go_on:
                continue
            x = (ue - math.floor(ue)) * TW
            bad, clear = [], False
            for r, spec in enumerate(A.FAMILIES[family]):
                bw = TW / spec["bays"]
                if abs(x - round(x / bw) * bw) <= TOL:
                    continue
                px = x * A.PX_M
                if all(min(abs(px - a), abs(px - b), abs(px - a - A.TILE), abs(px - b + A.TILE)) >= CLEAR * A.PX_M
                       and not a <= px <= b for a, b in A._windows(spec)):
                    clear = True
                else:
                    bad.append(r)
            if bad or clear:
                faults.append((key, s[i], ya, yb, x, bad))
    return faults


def strips(faces):
    """Solid strips: (plane key, width) per run of one-u class 0 wall that reaches a floor's glass."""
    out = {}
    for key, _, s, y, u, _, _, windowed, wall in faces:
        if wall and not windowed and np.ptp(u) < 1e-6 and np.ptp(s) > 1e-3 and glassy(y.min(), y.max()) and np.all(np.abs(u - u[0]) < 1e-6):
            out.setdefault((key, round(float(u[0]), 3), round(float(s.min()), 2), round(float(s.max()), 2)), 0)
    return sorted({(k[0], round(k[3] - k[2], 2)) for k in out})


def shared_columns(faces):
    owner, clash = {}, set()
    for key, _, _, _, u, _, _, windowed, _ in faces:
        if not windowed:
            continue
        for c in range(int(math.floor(u.min() + 1e-6)), int(math.floor(u.max() - 1e-6)) + 1):
            if owner.setdefault(c, key) != key:
                clash.add(c)
    return clash


def stretch(faces):
    worst = 0.0
    for _, _, s, y, u, _, _, windowed, _ in faces:
        if windowed and np.ptp(s) > 0.5:
            i, j = int(np.argmin(s)), int(np.argmax(s))
            worst = max(worst, abs(abs(u[j] - u[i]) * TW / (s[j] - s[i]) - 1))
    return worst


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    opts = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in sys.argv[1:] if a.startswith("--"))
    folder = opts.get("dir", os.path.join(REPO, "CSVM", "data", "city_models", "C5"))
    # The gd_ ground dressing (c5_ground_dressing.py) has no windowed wall.
    names = args or sorted(f[:-8] for f in os.listdir(folder) if f.endswith(".glb.bin") and not f.startswith("gd_"))
    total = 0
    print("%-6s %-6s %6s %7s %8s %5s" % ("model", "node", "edges", "shared", "stretch", "clear"))
    for m in names:
        meshes, _ = read_glb(os.path.join(folder, m + ".glb.bin"))
        for node in sorted(meshes, key=lambda n: (n == "far", n)):
            faces = walls(*meshes[node])
            found, clash = edge_faults(faces), shared_columns(faces)
            faults = [f for f in found if f[5]]
            total += len(faults) + len(clash)
            print("%-6s %-6s %6d %7d %7.1f%% %5d" % (m, node, len(faults), len(clash), 100 * stretch(faces), len(found) - len(faults)))
            if "strips" in opts and strips(faces):
                print("    solid strips: " + ", ".join("%s %.2f m" % (k, w) for k, w in strips(faces)))
            if "verbose" in opts:
                for key, s, ya, yb, x, bad in found:
                    print("    plane %s s %.2f y %.1f..%.1f: %.2f m into the tile, rows %s" % (key, s, ya, yb, x, bad))
    print("%d faults" % total)
    sys.exit(1 if total else 0)


if __name__ == "__main__":
    main()
