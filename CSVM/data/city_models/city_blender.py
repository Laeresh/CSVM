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
  far box's mean atlas colour on the original's mean colour.
- TEXCOORD_0: facade tile coordinates. On a wall, u is metres along the wall plane, left to right
  seen from outside, over the tile width, plus a whole number per wall plane, so every window
  column of a building has its own integer u; v is metres up over the tile height. A wall piece
  whose top or foot falls inside a floor's glass band is mapped onto the solid band round a floor
  line instead, so no window is cut. On a roof, slope or dressing, (u, v) is a planar map in
  tile units.
- TEXCOORD_1: (family index, surface class), class 0 wall and 1 roof. city_atlas.py holds the
  atlas layout the two index.
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
WALL, ROOF = 0, 1
EPS = 0.01


def _v(p):
    return np.array(p, dtype=float)


class Mesh:
    """Faces in the gamez frame: (points, uv0 per point, surface class)."""

    def __init__(self):
        self.faces = []

    def face(self, pts, uvs, cls, outward):
        pts = [_v(p) for p in pts]
        n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        if len(pts) == 4 and np.linalg.norm(n) < 1e-9:
            n = np.cross(pts[2] - pts[0], pts[3] - pts[0])
        if np.dot(n, outward) < 0:
            pts, uvs = pts[::-1], uvs[::-1]
        # A quad with two coincident corners is a triangle.
        keep = [i for i in range(len(pts)) if np.linalg.norm(pts[i] - pts[i - 1]) > 1e-6]
        if len(keep) >= 3:
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
    """Wall planes of a building and the whole-number u base of each, so window columns are
    unique round the building. Built from the hull, so the near models and the far box agree."""

    # A plane only a near model has (a parapet's inner face, a set-back top floor) takes this many
    # tile widths of its own, starting this far left of the first point it maps.
    NEAR_SPAN, NEAR_LEFT = 16, 64.0

    def __init__(self):
        self.base, self.lo, self.hi, self.next = {}, {}, {}, 0

    def add(self, axis, coord, sign, s0, s1):
        k = (axis, round(coord, 2), sign)
        ends = [float(np.dot(_right(axis, sign), q)) for q in _run_points(axis, coord, s0, s1, 0, 0)[:2]]
        self.lo[k] = min([self.lo.get(k, math.inf)] + ends)
        self.hi[k] = max([self.hi.get(k, -math.inf)] + ends)

    def seal(self):
        for k in sorted(self.lo):
            self.base[k] = self.next
            self.next += math.ceil((self.hi[k] - self.lo[k]) / TW - 1e-6) + 1

    def u(self, axis, coord, sign, p):
        k = (axis, round(coord, 2), sign)
        s = float(np.dot(_right(axis, sign), p))
        if k not in self.base:
            self.lo[k], self.base[k] = s - self.NEAR_LEFT, self.next
            self.next += self.NEAR_SPAN
        return self.base[k] + (s - self.lo[k]) / TW


def wall(mesh, planes, run, y0, y1, band_only=False, collect=None):
    """A wall run from y0 to y1. Straight-mapped pieces go to collect when given, so pieces of
    one plane from several tiers can be merged before they become faces."""
    axis, coord, sign, s0, s1 = run
    pieces = [(y0, y1, (round(y0 / FLOOR) * FLOOR - SOLID) / TW,
               (round(y0 / FLOOR) * FLOOR - SOLID + min(2 * SOLID, y1 - y0)) / TW)] if band_only else v_pieces(y0, y1)
    for ya, yb, va, vb in pieces:
        if collect is not None and abs(va - ya / TW) < 1e-9 and abs(vb - yb / TW) < 1e-9:
            collect.append((axis, coord, sign, s0, s1, ya, yb))
            continue
        _wall_face(mesh, planes, (axis, coord, sign), s0, s1, ya, yb, va, vb)


def _wall_face(mesh, planes, key, s0, s1, ya, yb, va, vb):
    axis, coord, sign = key
    pts = _run_points(axis, coord, s0, s1, ya, yb)
    uvs = [(planes.u(axis, coord, sign, _v(p)), va if p[1] == ya else vb) for p in pts]
    mesh.face(pts, uvs, WALL, _normal(axis, sign))


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


def slope(mesh, ra, ya, rb, yb):
    """The four faces between rectangle ra at ya and rb at yb (rb may be a point)."""
    ca = [(ra[0], ra[2]), (ra[1], ra[2]), (ra[1], ra[3]), (ra[0], ra[3])]
    cb = [(rb[0], rb[2]), (rb[1], rb[2]), (rb[1], rb[3]), (rb[0], rb[3])]
    centre = _v(((ra[0] + ra[1]) / 2, (ya + yb) / 2, (ra[2] + ra[3]) / 2))
    for i in range(4):
        j = (i + 1) % 4
        pts = [(ca[i][0], ya, ca[i][1]), (ca[j][0], ya, ca[j][1]), (cb[j][0], yb, cb[j][1]), (cb[i][0], yb, cb[i][1])]
        mid = np.mean([_v(p) for p in pts], axis=0)
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

def planes_for(profile):
    planes = Planes()
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


def build_profile(mesh, planes, profile, roofs=None):
    """Walls, slopes and caps of a profile. Every flat roof is reported to roofs as
    (y, lower footprint, upper footprint or None) for parapets and dressing."""
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
            slope(mesh, ra[0], ya, rb[0], yb)
        else:
            raise ValueError("profile band %.2f..%.2f is neither a prism nor a slope" % (ya, yb))
    flush_walls(mesh, planes, pieces)
    y, top = profile[-1]
    if not _degenerate(top):
        cap(mesh, top, y)
        if roofs is not None:
            roofs.append((y, top, None))


def box(mesh, x0, x1, z0, z1, y0, y1, top=True):
    """A closed box without a bottom, roof class, planar mapped."""
    for axis, sign in ((0, -1), (0, 1), (2, -1), (2, 1)):
        if axis == 0:
            c = x0 if sign < 0 else x1
            pts = _run_points(0, c, z0, z1, y0, y1)
        else:
            c = z0 if sign < 0 else z1
            pts = _run_points(2, c, x0, x1, y0, y1)
        r = _right(axis, sign)
        mesh.face(pts, [(float(np.dot(r, p)) / TW, p[1] / TW) for p in pts], ROOF, _normal(axis, sign))
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

def far_model(profile):
    mesh = Mesh()
    build_profile(mesh, planes_for(profile), profile)
    return mesh


def near_model(profile, near, rng):
    """A near model: the hull profile with the variant's edits, parapets on every flat roof edge
    that is open to the sky, and the variant's dressing kit on the top roof."""
    planes = planes_for(profile)
    prof = list(profile)
    if near.get("setback"):
        prof = _set_back(prof, near["setback"])
    if near.get("crown"):
        prof = prof[:-1] + near["crown"]
    mesh = Mesh()
    roofs = []
    build_profile(mesh, planes, prof, roofs)
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
        dress_roof(mesh, top[0], y, near.get("kit", ()), rng)
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
    for pts, uvs, cls in mesh.faces:
        # gamez (X, Y, Z) is glTF's frame; Blender is Z up, so (X, -Z, Y), which the exporter undoes.
        vs = [bm.verts.new((p[0], -p[2], p[1])) for p in pts]
        f = bm.faces.new(vs)
        f.smooth = False
        for loop, uv in zip(f.loops, uvs):
            # The exporter writes v as 1 - v.
            loop[uv0].uv = (uv[0], 1.0 - uv[1])
            loop[uv1].uv = (float(family), 1.0 - float(cls))
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
    (sRGB) on the original's mean colour, as census.py measures it (sRGB texel means)."""
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
