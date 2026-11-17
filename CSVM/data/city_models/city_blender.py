"""The shared city building builder: a hull's measured profile to near models and a far box.

A hull is a PROFILE, measured from the original mesh by analysis/city-hulls/tiers.py: a list of
(y, [(x0, x1, z0, z1), ...]) from the base up, in the gamez mesh's own frame (metres, Y up, which
is glTF's frame). Two entries at one height are a flat roof between two footprints; two entries
with one footprint are a vertical prism; two single rectangles at different heights are a slope
(a zero-size rectangle is an apex). The far box is that profile and nothing else. A near model
is the profile with a variant's edits (parapets, a top floor set back, a stepped crown) and roof
dressing (water tanks, bulkheads, machine rooms, vents, chimneys), inside the margin the plan
sets: at most about 3 m carved in, at most about 4 m of dressing above the roof, nothing past the
hull sideways.

What a model file holds (docs/tooling.md, "City models"):
- Mesh nodes near0, near1 and far, one material named after the model, opaque, single sided,
  outward winding, flat normals, no bottom faces. baseColorFactor is the grade that puts the
  far box's mean atlas colour on the original's mean unlit colour (its painted lit windows left
  out, c5.py).
- TEXCOORD_0: facade tile coordinates. On a wall, u runs along the wall plane, left to right seen
  from outside, plus a whole number per wall plane, so every window column of a building has its
  own integer u; v is metres up over the tile height. A plane holds a whole number of the family's
  common bays between its ends (Planes), and a wall end off that grid is a solid strip, so no
  window is cut at a corner. A wall piece
  whose top or foot falls inside a floor's glass band is mapped onto the solid band round a floor
  line instead, so no window is cut. On a roof, slope or dressing, (u, v) is a planar map in
  tile units.
- TEXCOORD_1: (family index, surface class), class 0 wall, 1 roof, 2 light metal (posts, rails,
  slab edges; the look shader takes it ungraded, so it stays pale on a dark building) and 3 a
  walk-up's cornice line (the light metal cell, ungraded and brightened by CityModels.CorniceGain),
  4 a board (a roof billboard's poster or back, a red brick gable: city_atlas.py's board strips,
  ungraded), whose TEXCOORD_1.x is the board's format instead of the family (Mesh.face's family).
  city_atlas.py holds the atlas layout the two index.
- A hull with gables (gables=True) maps a slope's vertical faces, its gable ends, onto the wall's
  solid band, so they wear the facade's wall and take no window.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "vegetation_models"))
import city_atlas as A  # noqa: E402
import veg_blender as vb  # noqa: E402

TW = A.TILE_M
FLOOR = A.FLOOR_M
SOLID = A.SOLID_M
WALL, ROOF, METAL, CORNICE, BOARD = 0, 1, 2, 3, 4
EPS = 0.01


def _v(p):
    return np.array(p, dtype=float)


class Mesh:
    """Faces in the gamez frame: (points, uv0 per point, surface class)."""

    def __init__(self):
        self.faces = []
        # Cornice bands, cornice_bands()'s: the wall faces they overlap are split and the overlap is class CORNICE.
        self.cornice = []
        # Face index to the TEXCOORD_1.x it carries instead of the model's family (a board's format).
        self.family = {}

    def face(self, pts, uvs, cls, outward, family=None):
        pts = [_v(p) for p in pts]
        n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        if len(pts) == 4 and np.linalg.norm(n) < 1e-9:
            n = np.cross(pts[2] - pts[0], pts[3] - pts[0])
        if np.dot(n, outward) < 0:
            pts, uvs = pts[::-1], uvs[::-1]
        # A quad with two coincident corners is a triangle.
        keep = [i for i in range(len(pts)) if np.linalg.norm(pts[i] - pts[i - 1]) > 1e-6]
        if len(keep) >= 3:
            if family is not None:
                self.family[len(self.faces)] = family
            self.faces.append(([pts[i] for i in keep], [uvs[i] for i in keep], cls))

    def triangles(self):
        return sum(len(f[0]) - 2 for f in self.faces)

    def area(self, cls):
        a = 0.0
        for pts, _, c in self.faces:
            if c == cls:
                for i in range(1, len(pts) - 1):
                    a += 0.5 * np.linalg.norm(np.cross(pts[i] - pts[0], pts[i + 1] - pts[0]))
        return a


# ---- footprints: unions of rectangles on their own compressed grid

def _grid(rects, extra=()):
    xs = sorted({round(c, 3) for r in list(rects) + list(extra) for c in r[:2]})
    zs = sorted({round(c, 3) for r in list(rects) + list(extra) for c in r[2:]})
    return xs, zs


def _inside(rects, x, z):
    return any(r[0] - 1e-6 <= x <= r[1] + 1e-6 and r[2] - 1e-6 <= z <= r[3] + 1e-6 for r in rects)


def outline(rects, covered=()):
    """The footprint's outline as merged straight runs: (axis, coord, sign, s0, s1), the plane
    x = coord (axis 0) or z = coord (axis 2), outward along +axis when sign is 1, spanning s0..s1
    along the other horizontal axis. Runs whose inside lies under `covered` are left out."""
    xs, zs = _grid(rects, covered)
    unit = []
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            if not _inside(rects, cx, cz) or _inside(covered, cx, cz):
                continue
            for axis, sign, coord, s0, s1, nx, nz in (
                    (0, -1, xs[i], zs[j], zs[j + 1], xs[i] - 1e-3, cz), (0, 1, xs[i + 1], zs[j], zs[j + 1], xs[i + 1] + 1e-3, cz),
                    (2, -1, zs[j], xs[i], xs[i + 1], cx, zs[j] - 1e-3), (2, 1, zs[j + 1], xs[i], xs[i + 1], cx, zs[j + 1] + 1e-3)):
                if not _inside(rects, nx, nz):
                    unit.append((axis, coord, sign, s0, s1))
    unit.sort()
    runs = []
    for u in unit:
        if runs and runs[-1][:3] == u[:3] and abs(runs[-1][4] - u[3]) < 1e-6:
            runs[-1] = runs[-1][:4] + (u[4],)
        else:
            runs.append(u)
    return runs


def inset(rect, d):
    return (rect[0] + d, rect[1] - d, rect[2] + d, rect[3] - d)


def _normal(axis, sign):
    n = np.zeros(3)
    n[axis] = sign
    return n


def _run_points(axis, coord, s0, s1, y0, y1):
    """The four corners of a wall run, bottom then top, s0 first."""
    if axis == 0:
        return [(coord, y0, s0), (coord, y0, s1), (coord, y1, s1), (coord, y1, s0)]
    return [(s0, y0, coord), (s1, y0, coord), (s1, y1, coord), (s0, y1, coord)]


def _right(axis, sign):
    """The horizontal direction that runs left to right seen from outside: (-n) x up."""
    n = _normal(axis, sign)
    return np.cross(-n, (0, 1, 0))


# ---- the vertical facade map

def _glass_zone(y):
    """The glass band (g0, g1) of the floor y falls in, if y cuts it by more than a frame: within
    0.2 m of the band's foot a wall shows only frame and sill, within 0.3 m of its head only the
    top of the glass, and neither is worth a split."""
    k = math.floor(y / FLOOR)
    g0, g1 = k * FLOOR + SOLID, (k + 1) * FLOOR - SOLID
    return (g0, g1) if g0 + 0.2 < y < g1 - 0.3 else None


def v_pieces(y0, y1):
    """A wall's height split so no piece cuts a window: (ya, yb, va, vb) in tile units."""
    band = lambda ya, yb: (ya, yb, (round(ya / FLOOR) * FLOOR - SOLID) / TW,
                           (round(ya / FLOOR) * FLOOR - SOLID + min(2 * SOLID, yb - ya)) / TW)
    z0, z1 = _glass_zone(y0), _glass_zone(y1)
    c0 = z0[1] if z0 else y0
    c1 = z1[0] if z1 else y1
    if c0 >= c1 - 0.05:
        return [band(y0, y1)]
    out = []
    if c0 > y0:
        out.append(band(y0, c0))
    out.append((c0, c1, c0 / TW, c1 / TW))
    if c1 < y1:
        out.append(band(c1, y1))
    return out


class Planes:
    """Wall planes of a building and the u map of each. A plane's grid marks stand a whole number
    of grid widths apart between its ends: stretched by at most STRETCH, or else unstretched and
    centred, the rest a solid strip at each end. The grid width is the family's common bay
    (city_atlas.grid_m): every facade row of the family has a pier centred on each mark, so a wall
    that ends on a mark cuts no window whichever row the shader picks. The whole-number base of each
    plane keeps window columns unique round the building. Built from the hull, so the near models
    and the far box agree; a plane only a near model has (a set-back top floor, a light well, a
    parapet's inner face) is fitted to the first face mapped on it."""

    STRETCH = 0.10
    # A near-only plane's tiles: its first face's grid starts NEAR_ROOM tiles into them.
    NEAR_SPAN, NEAR_ROOM = 16, 4
    # A face end this close to a mark ends on it.
    SNAP = 0.02

    def __init__(self, grid=TW):
        self.grid, self.map, self.lo, self.hi, self.next = grid, {}, {}, {}, 0

    def add(self, axis, coord, sign, s0, s1):
        k = (axis, round(coord, 2), sign)
        ends = [float(np.dot(_right(axis, sign), q)) for q in _run_points(axis, coord, s0, s1, 0, 0)[:2]]
        self.lo[k] = min([self.lo.get(k, math.inf)] + ends)
        self.hi[k] = max([self.hi.get(k, -math.inf)] + ends)

    def _fit(self, k, lo, hi, room=0, span=0):
        """Plane k's (base, origin, metres of wall per mark, tiles per mark) for lo..hi along its right direction."""
        g, w = self.grid, hi - lo
        n = round(w / g)
        if n >= 1 and abs(n * g / w - 1) <= self.STRETCH:
            step, origin = w / n, lo
        else:
            n = math.floor(w / g + 1e-6)
            step, origin = g, lo + (w - n * g) / 2
        self.map[k] = (self.next + room, origin, step, g / TW)
        self.next += max(math.ceil(n * g / TW - 1e-6) + 1, span)

    def seal(self):
        for k in sorted(self.lo):
            self._fit(k, self.lo[k], self.hi[k])

    def bay(self, key, s0, s1):
        """Maps the near-only plane key unstretched with a tile edge at s0 and marks every |s1 - s0|
        metres, so a dormer front s0..s1 two metres wide shows one 2 m bay: one whole window of
        every walk-up row."""
        axis, coord, sign = key
        k = (axis, round(coord, 2), sign)
        d = float(_right(axis, sign)[2 if axis == 0 else 0])
        if k not in self.map:
            self.map[k] = (self.next + self.NEAR_ROOM, min(d * s0, d * s1), abs(s1 - s0), abs(s1 - s0) / TW)
            self.next += self.NEAR_SPAN

    def u(self, axis, coord, sign, p):
        k = (axis, round(coord, 2), sign)
        t = float(np.dot(_right(axis, sign), p))
        if k not in self.map:
            self._fit(k, t, t, self.NEAR_ROOM, self.NEAR_SPAN)
        base, origin, step, q = self.map[k]
        return base + (t - origin) / step * q

    def pieces(self, key, s0, s1):
        """The face s0..s1 on plane key split at the grid marks next to its ends: (a, b, u), u None
        for the windowed part between marks and the mark's u for a solid strip outside them."""
        axis, coord, sign = key
        k = (axis, round(coord, 2), sign)
        d = float(_right(axis, sign)[2 if axis == 0 else 0])
        t0, t1 = sorted((d * s0, d * s1))
        if k not in self.map:
            self._fit(k, t0, t1, self.NEAR_ROOM, self.NEAR_SPAN)
        base, origin, step, q = self.map[k]
        j0 = math.ceil((t0 - origin - self.SNAP) / step)
        j1 = math.floor((t1 - origin + self.SNAP) / step)
        m0, m1 = origin + j0 * step, origin + j1 * step
        c0 = m0 if m0 - t0 > self.SNAP else t0
        c1 = m1 if t1 - m1 > self.SNAP else t1
        if j0 >= j1 or c1 - c0 <= self.SNAP:
            return [(s0, s1, base + q * round(((t0 + t1) / 2 - origin) / step))]
        out = [(t0, c0, base + q * j0), (c0, c1, None), (c1, t1, base + q * j1)]
        return [(min(d * a, d * b), max(d * a, d * b), u) for a, b, u in out if b - a > 1e-6]

def wall(mesh, planes, run, y0, y1, band_only=False, collect=None):
    """A wall run from y0 to y1. Straight-mapped pieces go to collect when given, so pieces of
    one plane from several tiers can be merged before they become faces."""
    axis, coord, sign, s0, s1 = run
    pieces = [(y0, y1, (round(y0 / FLOOR) * FLOOR - SOLID) / TW,
               (round(y0 / FLOOR) * FLOOR - SOLID + min(2 * SOLID, y1 - y0)) / TW)] if band_only else v_pieces(y0, y1)
    # The run's ends off the plane's grid are solid strips, cut here so merging a plane's pieces
    # across tiers never moves a cut off a mark.
    for a, b, solid in planes.pieces((axis, coord, sign), s0, s1):
        for ya, yb, va, vb in pieces:
            if solid is None and collect is not None and abs(va - ya / TW) < 1e-9 and abs(vb - yb / TW) < 1e-9:
                collect.append((axis, coord, sign, a, b, ya, yb))
                continue
            _wall_face(mesh, planes, (axis, coord, sign), a, b, ya, yb, va, vb, solid=solid)


def _wall_face(mesh, planes, key, s0, s1, ya, yb, va, vb, cls=WALL, solid=None):
    axis, coord, sign = key
    for (b_axis, b_coord, b_sign), b0, b1, y0, y1 in mesh.cornice if cls == WALL else ():
        lo, hi, y0, y1 = max(s0, b0), min(s1, b1), max(ya, y0), min(yb, y1)
        if (b_axis, b_sign) != (axis, sign) or abs(b_coord - coord) > 1e-3 or hi - lo < 1e-6 or y1 - y0 < 1e-6:
            continue
        v = lambda y: va + (y - ya) / (yb - ya) * (vb - va)
        # The rest of the face round the overlap: the full-height sides, then below and above it.
        for a, b, c, d in ((s0, lo, ya, yb), (hi, s1, ya, yb), (lo, hi, ya, y0), (lo, hi, y1, yb)):
            if b - a > 1e-6 and d - c > 1e-6:
                _wall_face(mesh, planes, key, a, b, c, d, v(c), v(d), solid=solid)
        _wall_face(mesh, planes, key, lo, hi, y0, y1, v(y0), v(y1), CORNICE)
        return
    # A solid strip is held on its grid mark's u, the pier between two window columns of every row.
    pts = _run_points(axis, coord, s0, s1, ya, yb)
    uvs = [(planes.u(axis, coord, sign, _v(p)) if solid is None or cls != WALL else solid, va if p[1] == ya else vb)
           for p in pts]
    mesh.face(pts, uvs, cls, _normal(axis, sign))


def _sweep(ps):
    """One partition of a plane's pieces (s0, s1, y0, y1): split along s at every piece's ends,
    join touching heights per span, then join neighbouring spans whose heights match."""
    cuts = sorted({round(c, 4) for p in ps for c in p[:2]})
    spans = []
    for a, b in zip(cuts, cuts[1:]):
        merged = []
        for y0, y1 in sorted((p[2], p[3]) for p in ps if p[0] <= a + 1e-6 and p[1] >= b - 1e-6):
            if merged and y0 <= merged[-1][1] + 1e-6:
                merged[-1] = (merged[-1][0], max(merged[-1][1], y1))
            else:
                merged.append((y0, y1))
        if spans and spans[-1][1] == a and spans[-1][2] == merged:
            spans[-1] = (spans[-1][0], b, merged)
        else:
            spans.append((a, b, merged))
    return [(a, b, y0, y1) for a, b, ys in spans for y0, y1 in ys]


def flush_walls(mesh, planes, pieces):
    """Merges straight wall pieces per plane into the fewest rectangles of three partitions: the
    pieces as built, swept along the plane first, or swept up the plane first."""
    by_plane = {}
    for axis, coord, sign, s0, s1, y0, y1 in pieces:
        by_plane.setdefault((axis, round(coord, 3), sign), []).append((s0, s1, y0, y1))
    for key in sorted(by_plane):
        ps = by_plane[key]
        across = _sweep(ps)
        up = [(a, b, y0, y1) for y0, y1, a, b in _sweep([(p[2], p[3], p[0], p[1]) for p in ps])]
        for a, b, y0, y1 in min((ps, across, up), key=len):
            _wall_face(mesh, planes, key, a, b, y0, y1, y0 / TW, y1 / TW)


def exposed(lower, upper):
    """The part of footprint lower not under footprint upper, as rectangles."""
    xs, zs = _grid(lower, upper)
    occ = [[_inside(lower, (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2)
            and not _inside(upper, (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2)
            for j in range(len(zs) - 1)] for i in range(len(xs) - 1)]
    used = [[False] * len(r) for r in occ]
    rects = []
    for i in range(len(occ)):
        for j in range(len(occ[i])):
            if not occ[i][j] or used[i][j]:
                continue
            j1 = j
            while j1 + 1 < len(occ[i]) and occ[i][j1 + 1] and not used[i][j1 + 1]:
                j1 += 1
            i1 = i
            while i1 + 1 < len(occ) and all(occ[i1 + 1][k] and not used[i1 + 1][k] for k in range(j, j1 + 1)):
                i1 += 1
            for a in range(i, i1 + 1):
                for b in range(j, j1 + 1):
                    used[a][b] = True
            rects.append((xs[i], xs[i1 + 1], zs[j], zs[j1 + 1]))
    return rects


def cap(mesh, rects, y):
    for r in rects:
        if r[1] - r[0] < 1e-6 or r[3] - r[2] < 1e-6:
            continue
        pts = [(r[0], y, r[2]), (r[1], y, r[2]), (r[1], y, r[3]), (r[0], y, r[3])]
        mesh.face(pts, [(p[0] / TW, p[2] / TW) for p in pts], ROOF, (0, 1, 0))


def slope(mesh, ra, ya, rb, yb, gable_planes=None):
    """The four faces between rectangle ra at ya and rb at yb (rb may be a point). With
    gable_planes, a vertical face (a gable end) is a wall on the solid band round ya's floor line."""
    ca = [(ra[0], ra[2]), (ra[1], ra[2]), (ra[1], ra[3]), (ra[0], ra[3])]
    cb = [(rb[0], rb[2]), (rb[1], rb[2]), (rb[1], rb[3]), (rb[0], rb[3])]
    centre = _v(((ra[0] + ra[1]) / 2, (ya + yb) / 2, (ra[2] + ra[3]) / 2))
    for i in range(4):
        j = (i + 1) % 4
        pts = [(ca[i][0], ya, ca[i][1]), (ca[j][0], ya, ca[j][1]), (cb[j][0], yb, cb[j][1]), (cb[i][0], yb, cb[i][1])]
        mid = np.mean([_v(p) for p in pts], axis=0)
        n = np.cross(_v(pts[1]) - _v(pts[0]), _v(pts[3]) - _v(pts[0]))
        if gable_planes is not None and np.linalg.norm(n) > 1e-9 and abs(n[1]) < 1e-6 * np.linalg.norm(n):
            axis = 0 if abs(pts[0][0] - pts[1][0]) < 1e-6 else 2
            sign = 1 if mid[axis] > centre[axis] else -1
            v0 = (round(ya / FLOOR) * FLOOR - SOLID) / TW
            uvs = [(gable_planes.u(axis, pts[0][axis], sign, _v(p)), v0 + (p[1] - ya) / (yb - ya) * 2 * SOLID / TW)
                   for p in pts]
            mesh.face(pts, uvs, WALL, mid - centre)
            continue
        edge = _v(pts[1]) - _v(pts[0])
        el = np.linalg.norm(edge)
        # u along the eave, v up the slant, both in tile units.
        uvs = []
        for p in pts:
            q = _v(p) - _v(pts[0])
            along = float(np.dot(q, edge / el)) if el > 1e-6 else 0.0
            uvs.append((along / TW, float(np.linalg.norm(q - edge / max(el, 1e-6) * along)) / TW))
        mesh.face(pts, uvs, ROOF, mid - centre)


# ---- a profile to geometry

def planes_for(profile, family):
    planes = Planes(A.grid_m(family))
    for (ya, ra), (yb, rb) in zip(profile, profile[1:]):
        if yb > ya and _same(ra, rb):
            for run in outline(ra):
                planes.add(*run)
    planes.seal()
    return planes


def _same(a, b):
    return len(a) == len(b) and all(max(abs(p - q) for p, q in zip(r, s)) < 1e-6 for r, s in zip(a, b))


def _degenerate(rects):
    return all(r[1] - r[0] < 1e-6 and r[3] - r[2] < 1e-6 for r in rects)


def cornice_bands(profile, c):
    """The wall strips c metres tall under every roof edge open to the sky and every eave, above
    the street floor (a light well's foot, where the awnings sit): (plane key, s0, s1, y0, y1)."""
    bands = []
    for i, ((ya, ra), (yb, rb)) in enumerate(zip(profile, profile[1:])):
        if yb - ya < 1e-6 or not _same(ra, rb) or yb <= FLOOR + 1e-6:
            continue
        nxt = profile[i + 2] if i + 2 < len(profile) else None
        covered = nxt[1] if nxt and abs(nxt[0] - yb) < 1e-6 else ()
        for axis, coord, sign, s0, s1 in outline(ra, covered):
            bands.append(((axis, coord, sign), s0, s1, max(ya, yb - c), yb))
    return bands


def hip_band(outline, dirs, y0, y1, d, end_face=None):
    """A roof band from the polygon outline [(x, z), ...] at y0 up to y1, each corner moved d
    metres along its dirs (dx, dz) entry: an edge whose corners both move inward is a roof slope,
    one whose corners move only along it is a vertical end on the wall's solid band (a gable, a
    step's side), one whose corners stay is a wall. A ridged roof over wings, or a hipped skirt
    round a notched plan, which a stack of rectangles cannot hold. end_face(mesh, pts, outward), when given,
    builds the vertical ends instead."""
    def build(mesh, planes):
        n = len(outline)
        turn = 1 if sum(outline[i][0] * outline[(i + 1) % n][1] - outline[(i + 1) % n][0] * outline[i][1]
                        for i in range(n)) > 0 else -1
        q = [(x + d * a, z + d * b) for (x, z), (a, b) in zip(outline, dirs)]
        v0 = (round(y0 / FLOOR) * FLOOR - SOLID) / TW
        for i in range(n):
            j = (i + 1) % n
            (x0, z0), (x1, z1) = outline[i], outline[j]
            out = _v((z1 - z0, 0.0, x0 - x1)) * turn
            out /= np.linalg.norm(out)
            axis = 0 if abs(x1 - x0) < 1e-6 else 2
            k = 0 if axis == 0 else 1
            coord, sign = outline[i][k], int(round(out[axis]))
            pts = [(x0, y0, z0), (x1, y0, z1), (q[j][0], y1, q[j][1]), (q[i][0], y1, q[i][1])]
            if q[i] == outline[i] and q[j] == outline[j]:
                s = sorted((z0, z1) if axis == 0 else (x0, x1))
                wall(mesh, planes, (axis, coord, sign, s[0], s[1]), y0, y1)
            elif abs(q[i][k] - coord) < 1e-6 and abs(q[j][k] - coord) < 1e-6 and end_face:
                end_face(mesh, pts, out)
            elif abs(q[i][k] - coord) < 1e-6 and abs(q[j][k] - coord) < 1e-6:
                uvs = [(planes.u(axis, coord, sign, _v(p)), v0 + (p[1] - y0) / (y1 - y0) * 2 * SOLID / TW) for p in pts]
                mesh.face(pts, uvs, WALL, out)
            else:
                edge = _v(pts[1]) - _v(pts[0])
                el = np.linalg.norm(edge)
                uvs = []
                for p in pts:
                    r = _v(p) - _v(pts[0])
                    along = float(np.dot(r, edge / el))
                    uvs.append((along / TW, float(np.linalg.norm(r - edge / el * along)) / TW))
                mesh.face(pts, uvs, ROOF, out + _v((0, 1, 0)))
    return build


def build_profile(mesh, planes, profile, roofs=None, gables=False, cornice=0.0, bands=None):
    """Walls, slopes and caps of a profile, with a cornice line cornice metres tall when given.
    Every flat roof is reported to roofs as (y, lower footprint, upper footprint or None) for
    parapets and dressing. A band that is neither a prism nor a one-rectangle slope is built by
    bands[its foot's y](mesh, planes)."""
    if cornice:
        mesh.cornice = cornice_bands(profile, cornice)
    pieces = []
    for (ya, ra), (yb, rb) in zip(profile, profile[1:]):
        if abs(yb - ya) < 1e-6:
            # A flat roof: the part open to the sky, or the whole lower footprint where that takes
            # fewer rectangles (its rest is hidden under the next tier).
            open_sky = exposed(ra, rb)
            cap(mesh, open_sky if len(open_sky) < len(ra) else ra, ya)
            if roofs is not None:
                roofs.append((ya, ra, rb))
        elif _same(ra, rb):
            for run in outline(ra):
                wall(mesh, planes, run, ya, yb, collect=pieces)
        elif len(ra) == 1 and len(rb) == 1:
            slope(mesh, ra[0], ya, rb[0], yb, planes if gables else None)
        elif bands and ya in bands:
            bands[ya](mesh, planes)
        else:
            raise ValueError("profile band %.2f..%.2f is neither a prism nor a slope" % (ya, yb))
    flush_walls(mesh, planes, pieces)
    y, top = profile[-1]
    if not _degenerate(top):
        cap(mesh, top, y)
        if roofs is not None:
            roofs.append((y, top, None))


def box(mesh, x0, x1, z0, z1, y0, y1, top=True, cls=ROOF):
    """A closed box without a bottom, roof class unless given, planar mapped."""
    for axis, sign in ((0, -1), (0, 1), (2, -1), (2, 1)):
        if axis == 0:
            c = x0 if sign < 0 else x1
            pts = _run_points(0, c, z0, z1, y0, y1)
        else:
            c = z0 if sign < 0 else z1
            pts = _run_points(2, c, x0, x1, y0, y1)
        r = _right(axis, sign)
        mesh.face(pts, [(float(np.dot(r, p)) / TW, p[1] / TW) for p in pts], cls, _normal(axis, sign))
    if top:
        cap(mesh, [(x0, x1, z0, z1)], y1)


def tank(mesh, cx, cz, y, r, sides=8):
    """A rooftop water tank: a frame under a barrel with a conical lid, 3.7 m tall in all."""
    legs, barrel, lid = 1.1, 2.1, 0.5
    box(mesh, cx - r * 0.75, cx + r * 0.75, cz - r * 0.75, cz + r * 0.75, y, y + legs, top=False)
    y0, y1 = y + legs, y + legs + barrel
    ring = [(cx + r * math.cos(2 * math.pi * k / sides), cz + r * math.sin(2 * math.pi * k / sides)) for k in range(sides)]
    for k in range(sides):
        a, b = ring[k], ring[(k + 1) % sides]
        pts = [(a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])]
        out = _v(((a[0] + b[0]) / 2 - cx, 0, (a[1] + b[1]) / 2 - cz))
        mesh.face(pts, [(k * 0.5 / TW * r, y0 / TW), ((k + 1) * 0.5 / TW * r, y0 / TW),
                        ((k + 1) * 0.5 / TW * r, y1 / TW), (k * 0.5 / TW * r, y1 / TW)], ROOF, out)
        apex = (cx, y1 + lid, cz)
        mesh.face([(a[0], y1, a[1]), (b[0], y1, b[1]), apex], [(a[0] / TW, a[1] / TW), (b[0] / TW, b[1] / TW),
                                                                 (cx / TW, cz / TW)], ROOF, out + _v((0, 1, 0)))


def parapet(mesh, planes, run, y, h, t):
    """A parapet on a roof edge: the wall carried up on the hull plane (solid band), its inner
    face t inside, and a coping on top."""
    axis, coord, sign, s0, s1 = run
    wall(mesh, planes, run, y, y + h, band_only=True)
    inner = (axis, coord - sign * t, -sign, s0, s1)
    wall(mesh, planes, inner, y, y + h, band_only=True)
    a, b = (coord - sign * t, coord) if sign > 0 else (coord, coord - sign * t)
    r = (a, b, s0, s1) if axis == 0 else (s0, s1, a, b)
    cap(mesh, [r], y + h)


class Spots:
    """Non-overlapping rectangles on a roof, placed by the variant's random stream."""

    def __init__(self, area, rng, blocked=()):
        self.area, self.rng, self.used = area, rng, list(blocked)

    def take(self, w, d, margin=0.6, where=None):
        x0, x1, z0, z1 = self.area
        if x1 - x0 < w + 2 * margin or z1 - z0 < d + 2 * margin:
            return None
        for _ in range(60):
            if where is None:
                fx, fz = self.rng.uniform(), self.rng.uniform()
            else:
                fx, fz = where[0] + self.rng.uniform(-0.08, 0.08), where[1] + self.rng.uniform(-0.08, 0.08)
            fx, fz = min(1, max(0, fx)), min(1, max(0, fz))
            cx = x0 + margin + w / 2 + fx * (x1 - x0 - w - 2 * margin)
            cz = z0 + margin + d / 2 + fz * (z1 - z0 - d - 2 * margin)
            r = (cx - w / 2, cx + w / 2, cz - d / 2, cz + d / 2)
            if all(r[1] + margin <= u[0] or u[1] + margin <= r[0] or r[3] + margin <= u[2] or u[3] + margin <= r[2]
                   for u in self.used):
                self.used.append(r)
                return r
        return None


def dress_roof(mesh, rect, y, kit, rng, blocked=()):
    """Roof dressing on one flat roof rectangle, inside a parapet's thickness and a margin, off
    the blocked rectangles (the tier standing on it)."""
    spots = Spots(inset(rect, 0.4), rng, blocked)
    w, d = rect[1] - rect[0], rect[3] - rect[2]
    for item in kit:
        kind = item[0]
        if kind == "tank":
            r = min(2.2, max(1.3, 0.12 * min(w, d)))
            s = spots.take(2 * r, 2 * r, where=item[1] if len(item) > 1 else None)
            if s:
                tank(mesh, (s[0] + s[1]) / 2, (s[2] + s[3]) / 2, y, r)
        elif kind == "machine":
            s = spots.take(max(3.0, 0.45 * w), max(3.0, 0.35 * d), where=item[1] if len(item) > 1 else None)
            if s:
                box(mesh, *s, y, y + 3.2)
        elif kind == "bulkhead":
            s = spots.take(2.6, 2.2, where=item[1] if len(item) > 1 else None)
            if s:
                box(mesh, *s, y, y + 2.6)
        elif kind == "vent":
            s = spots.take(0.6, 0.6, margin=0.4)
            if s:
                box(mesh, *s, y, y + 0.9)
        elif kind == "chimney":
            s = spots.take(0.8, 1.2, margin=0.3, where=item[1] if len(item) > 1 else None)
            if s:
                box(mesh, *s, y, y + 1.8)


# ---- variants

def far_model(profile, family, gables=False, cornice=0.0, bands=None):
    mesh = Mesh()
    build_profile(mesh, planes_for(profile, family), profile, gables=gables, cornice=cornice, bands=bands)
    return mesh


def near_model(profile, family, near, rng, gables=False, cornice=0.0, bands=None):
    """A near model: the hull profile, or the variant's own profile of the same hull, with the
    variant's edits, parapets on every flat roof edge that is open to the sky, and the variant's
    dressing kit on the top roof. A variant's own bands replace the hull's."""
    bands = near.get("bands", bands)
    planes = planes_for(profile, family)
    prof = list(near.get("profile", profile))
    if near.get("notch"):
        prof = _notch(prof, near["notch"])
    if near.get("setback"):
        prof = _set_back(prof, near["setback"])
    if near.get("steps"):
        prof = _steps(prof, near["steps"])
    if near.get("crown"):
        prof = prof[:-1] + near["crown"]
    mesh = Mesh()
    roofs = []
    build_profile(mesh, planes, prof, roofs, gables, cornice, bands)
    for y, lower, upper in roofs:
        top = upper is None
        h = near.get("parapet", 1.0) if top else near.get("ledge", 0.6)
        if h <= 0:
            continue
        for run in outline(lower, covered=upper or ()):
            parapet(mesh, planes, run, y, h, 0.3)
            if top and near.get("raised"):
                _raised(mesh, planes, run, y + h, near["raised"])
    y, top = prof[-1]
    if not _degenerate(top):
        dress_roof(mesh, max(top, key=lambda r: (r[1] - r[0]) * (r[3] - r[2])), y, near.get("kit", ()), rng)
    for y, lower, upper in roofs:
        if upper is not None and near.get("ledge_kit"):
            for r in lower:
                dress_roof(mesh, r, y, near["ledge_kit"], rng, blocked=upper)
    for extra in near.get("extras", ()):
        extra(mesh, planes)
    return mesh


def _set_back(prof, d):
    """The top floor of the top prism stands d metres in, on a ledge at its floor line."""
    (ya, ra), (yb, rb) = prof[-2], prof[-1]
    if not (_same(ra, rb) and len(ra) == 1 and yb - ya >= 2 * FLOOR):
        return prof
    r = ra[0]
    if min(r[1] - r[0], r[3] - r[2]) < 4 * d + 6:
        return prof
    yc = yb - FLOOR
    return prof[:-1] + [(yc, ra), (yc, [inset(r, d)]), (yb, [inset(r, d)])]


def _steps(prof, steps):
    """The top prism's upper floors stepped in: each (drop, d) sets the part above its top minus
    drop d metres in, the deepest step last, on ledges at those heights."""
    (ya, ra), (yb, rb) = prof[-2], prof[-1]
    if not (_same(ra, rb) and len(ra) == 1 and yb - ya > max(s[0] for s in steps)):
        return prof
    out, cur = prof[:-1], ra
    for drop, d in steps:
        out += [(yb - drop, cur), (yb - drop, [inset(ra[0], d)])]
        cur = [inset(ra[0], d)]
    return out + [(yb, cur)]


def _notch(prof, notches):
    """Light wells: every tier on the base footprint loses a slot cut in from the middle of a side,
    each notch (side, width, depth) with side one of x-, x+, z-, z+."""
    base = prof[0][1]
    if len(base) != 1:
        return prof
    x0, x1, z0, z1 = base[0]
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    cuts = []
    for side, w, d in notches:
        cuts.append({"x-": (x0, x0 + d, cz - w / 2, cz + w / 2), "x+": (x1 - d, x1, cz - w / 2, cz + w / 2),
                     "z-": (cx - w / 2, cx + w / 2, z0, z0 + d), "z+": (cx - w / 2, cx + w / 2, z1 - d, z1)}[side])
    cut = exposed(base, cuts)
    return [(y, cut if _same(rects, base) else rects) for y, rects in prof]


def turn(mesh, deg):
    """Turns a built model about Y by deg, for a building that stands turned in its own mesh frame
    (analysis/city-hulls/tiers.py --turn measures it turned back)."""
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    mesh.faces = [([_v((c * p[0] + s * p[2], p[1], -s * p[0] + c * p[2])) for p in pts], uvs, cls)
                  for pts, uvs, cls in mesh.faces]
    return mesh


def _raised(mesh, planes, run, y, h):
    """A raised parapet panel over the middle third of a long roof edge."""
    axis, coord, sign, s0, s1 = run
    if s1 - s0 < 20:
        return
    m0, m1 = s0 + (s1 - s0) / 3, s1 - (s1 - s0) / 3
    parapet(mesh, planes, (axis, coord, sign, m0, m1), y, h, 0.3)


# ---- Blender export

def to_object(name, mesh, family, mat):
    bm = bmesh.new()
    uv0 = bm.loops.layers.uv.new("UV0")
    uv1 = bm.loops.layers.uv.new("UV1")
    for n, (pts, uvs, cls) in enumerate(mesh.faces):
        fam = mesh.family.get(n, family)
        # gamez (X, Y, Z) is glTF's frame; Blender is Z up, so (X, -Z, Y), which the exporter undoes.
        vs = [bm.verts.new((p[0], -p[2], p[1])) for p in pts]
        f = bm.faces.new(vs)
        f.smooth = False
        for loop, uv in zip(f.loops, uvs):
            # The exporter writes v as 1 - v.
            loop[uv0].uv = (uv[0], 1.0 - uv[1])
            loop[uv1].uv = (float(fam), 1.0 - float(cls))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def grade_material(name, factor):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*factor, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def grade(far, family, rgb, means):
    """The linear per-channel factor that puts the far box's area-weighted mean atlas colour
    (sRGB) on the original's mean unlit colour, as census.py measures it (sRGB texel means)."""
    wall_mean, roof_mean = means[family]
    aw, ar = far.area(WALL), far.area(ROOF)
    mean = (wall_mean * aw + roof_mean * ar) / (aw + ar)
    f = tuple(float((c / m) ** 2.2) for c, m in zip(rgb, mean))
    if max(f) > 1.0:
        raise SystemExit("grade above 1 (%s): the atlas family %d is darker than the original" % (f, family))
    return f


def export(objs, out_dir, chapter, model):
    folder = os.path.join(out_dir, chapter)
    os.makedirs(folder, exist_ok=True)
    tmp = os.path.join(folder, model + ".glb")
    for o in bpy.context.scene.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(
        filepath=tmp, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
        export_normals=True, export_texcoords=True, export_materials="EXPORT",
        export_vertex_color="NONE", export_animations=False, export_extras=False)
    dst = tmp + ".bin"
    os.replace(tmp, dst)
    return dst


def write_atlas(out_dir, chapter):
    """Paints the atlas and the ids texture into <chapter>/ and returns the families' means."""
    atlas, ids, means = A.paint()
    folder = os.path.join(out_dir, chapter)
    os.makedirs(folder, exist_ok=True)
    vb.write_png(os.path.join(folder, "facade.png.bin"), atlas)
    vb.write_png(os.path.join(folder, "facade_ids.png.bin"), ids)
    return means
