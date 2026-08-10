r"""Where are C5's UNFLAGGED `cblock*` polygons, in world space, relative to the FLAGGED ones?
(BL-305 / PLAN-clutter-uv-placement, the "next step is spatial, not statistical" question.)

Context. Polygon bit `0x800` — mech3ax's `unk3`, exposed as `GameZPolygon.Subface` in `GameZ.cs` —
is the original's `no_clutter` face attribute (plan §"CLOSED (2026-08-10)": single writer in the
whole binary, fed from `strstr(name, "no_clutter")`). The original's clutter walk (`FUN_004de2c0`)
skips flagged polygons. But a debug recolour of C5 at BL-305's pose (`--pos=-9490,230,-3300`,
straight down) shows 100 % of the visible downtown ground flagged — so the original would stamp
nothing there — while the original plainly has a downtown skyline. Meanwhile the census counted,
for `cblock1.tif`, 102 flagged and 256 unflagged polygons, none of the unflagged ones visible from
above at that pose.

Exactly two candidate answers, with opposite consequences:

  (A) COPLANAR UNDERNEATH. The unflagged quads sit at the same XZ as flagged ones, a small Y below,
      same texture. Then the original stamps on those, its buildings inherit that texture's UV
      layout, they land correctly inside the blocks — and our bug is that we stamp on the flagged
      overlay instead.
  (B) SPATIALLY DISJOINT. They are elsewhere on the map. Then the original genuinely places no
      clutter at this downtown, and C5's towers are not clutter at all.

This script settles which, by measuring — for every world polygon whose material texture (PER
LAYER, not layer 0 unconditionally) is one of `cblock1/2/3/7.tif` — the world-space centroid, the
XZ bounding box, the mean Y, the XZ (plan-projected) area, the `unk3` flag, the `priority` field
and the material-layer count. Then, per texture:

  1. XZ overlap between the flagged and unflagged sets: what fraction of flagged polygons have an
     unflagged same-texture partner whose XZ footprint overlaps, and the SIGNED Y difference
     (partner minus flagged) when they do. (A) predicts near-total overlap with a small consistent
     offset of one sign; (B) predicts almost none.
  2. The two sets' XZ bounding boxes and centroids side by side. Disjoint boxes settle it at once.
  3. Everything of these textures within `--radius` of BL-305's pose (`-9490, -3300`), flagged and
     unflagged, with Y — the specific spot the contradiction lives at.
  4. If (B): where the unflagged ones actually are — the owning nodes and their world centroids.

No engine run; static analysis over `extracted/<C>/gamez/{nodes,models,materials,textures}.json`
only. Verdict in the exit code: 0 = ran clean, 1 = a self-check or a per-texture consistency check
failed, 2 = could not run (missing extraction).

The walk mirrors `ClutterBuilder.PlaceOnWorld` (`CSVM/src/Mech3/Clutter.cs:635-667`) field for
field, and is lifted from `analysis/bl-305-clutter-uv/uv_repeat.py` rather than reinvented:

  - Roots: `world1`'s `child_indices` PLUS its partition grid's referenced node indices, each
    subtree walked from identity. `PlaceOnWorld` deduplicates neither list, and applies no
    transform of `world1`'s own (it has none in the shipped data). Polygons reachable twice are
    deduplicated HERE by (node index, polygon index) so a quad is not counted as its own partner.
  - `WorldBuilder.SkipWorldNode` (`horizon` / `dzpaths` / `fvol*`) and non-nearest `Lod` levels are
    skipped, subtree included. The `active` flag is NOT checked — `PlaceOnWorld` does not check it.
  - Each node's local transform composes onto the accumulated one BEFORE recursing, so a mesh is
    measured in the transform state at its own point in the walk (`xf *= local;`).
  - WORLD SPACE, NOT LOCAL. This whole question is about position (WORLD-15): two quads that share
    a local XZ under different parents are nowhere near each other in the world, and a local-space
    answer here would be plausible and wrong with nothing to flag it. `GameZ.ParseTransform`'s
    Euler branch is reimplemented in full for the nodes that rotate without an explicit matrix.

Traps this script is written against:

  - NEVER conclude from one texture. `cblock7` has behaved differently from `cblock1/2/3` in every
    measurement so far (5.1 % flagged area vs 18.7/53.3/63.0 %); all four are reported separately
    and the verdict line names any texture that disagrees with the others.
  - Degenerate and zero-XZ-area polygons exist in this data. They are COUNTED AND REPORTED, never
    silently dropped (DIAG-15). A vertical wall quad has zero XZ area but a real XZ bbox, so it is
    kept for the overlap test and flagged separately in the counts.
  - Texture is read per material layer.

METHOD-9: `--self-check` (always run, before any measurement) builds a synthetic coplanar pair with
a KNOWN Y offset and a synthetic deliberately-disjoint pair, and requires the overlap instrument to
report "overlaps, dY = the known value" for the first and "disjoint" for the second. If the
instrument cannot fail on the disjoint case it would rubber-stamp answer (A) from any input.

Run from the repo root. This worktree has no `extracted/` (git-ignored), so pass the main copy:

    python analysis/bl-305-clutter-uv/flag_geometry.py --extracted Z:/CSVM/extracted
"""
import argparse
import json
import math
import os
import sys

TARGET_TEXTURES = ["cblock1", "cblock2", "cblock3", "cblock7"]

# BL-305's debug pose: --pos=-9490,230,-3300 looking straight down.
POSE_X, POSE_Z = -9490.0, -3300.0

EPS_AREA = 1e-6


# ---------------------------------------------------------------- data model (from uv_repeat.py)

class Mesh:
    __slots__ = ("vertices", "polygons")

    def __init__(self):
        self.vertices = []
        self.polygons = []


class Polygon:
    __slots__ = ("vertex_indices", "tri_strip", "layers", "unk3", "priority")

    def __init__(self):
        self.vertex_indices = []
        self.tri_strip = False
        self.layers = []      # (material_index, uv_coords or None) per layer
        self.unk3 = False     # raw bit 0x800 == the original's no_clutter attribute
        self.priority = None  # mech3ax `priority` (asserted -50..=50); the subface hypothesis


class Node:
    __slots__ = ("name", "kind", "model_index", "children", "lod_range_min", "local",
                 "partition_roots")

    def __init__(self):
        self.name = ""
        self.kind = ""
        self.model_index = -1
        self.children = []
        self.lod_range_min = -1.0
        self.local = None
        self.partition_roots = None


class Transform:
    """basis: 3 rows (row_k . v == world component k); translate: (x, y, z)."""
    __slots__ = ("basis", "translate")

    IDENTITY_BASIS = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))

    def __init__(self, basis, translate):
        self.basis = basis
        self.translate = translate

    def xform(self, v):
        b, t = self.basis, self.translate
        return (
            b[0][0] * v[0] + b[0][1] * v[1] + b[0][2] * v[2] + t[0],
            b[1][0] * v[0] + b[1][1] * v[1] + b[1][2] * v[2] + t[1],
            b[2][0] * v[0] + b[2][1] * v[1] + b[2][2] * v[2] + t[2],
        )

    def basis_xform(self, v):
        b = self.basis
        return (
            b[0][0] * v[0] + b[0][1] * v[1] + b[0][2] * v[2],
            b[1][0] * v[0] + b[1][1] * v[1] + b[1][2] * v[2],
            b[2][0] * v[0] + b[2][1] * v[1] + b[2][2] * v[2],
        )

    def compose(self, local):
        """self * local: local applied first — Godot's Transform3D operator*, Clutter.cs's
        `xf *= local;`."""
        new_basis = [[0.0, 0.0, 0.0] for _ in range(3)]
        for col in range(3):
            col_vec = (local.basis[0][col], local.basis[1][col], local.basis[2][col])
            out = self.basis_xform(col_vec)
            for row in range(3):
                new_basis[row][col] = out[row]
        return Transform(tuple(tuple(r) for r in new_basis), self.xform(local.translate))


IDENTITY = Transform(Transform.IDENTITY_BASIS, (0.0, 0.0, 0.0))


# ---------------------------------------------------------------- loading (mirrors GameZ.cs)

def _vec3(d):
    return (d["x"], d["y"], d["z"])


def _parse_transform(tf):
    """Mirrors GameZ.ParseTransform (GameZ.cs:210-238): explicit matrix (stored transposed) or
    Euler angles composed R = Ry(y)*Rx(x)*Rz(z). Scale never read (unit everywhere)."""
    tr = _vec3(tf["translation"] if "translation" in tf else tf["translate"])
    m = tf.get("matrix") or tf.get("original")
    if isinstance(m, dict):
        def M(legacy, unified):
            return float(m[legacy] if legacy in m else m[unified])
        a, b, c = M("a", "r00"), M("b", "r01"), M("c", "r02")
        d, e, f = M("d", "r10"), M("e", "r11"), M("f", "r12")
        g, h, i = M("g", "r20"), M("h", "r21"), M("i", "r22")
        basis = ((a, d, g), (b, e, h), (c, f, i))
    else:
        rot = tf["rotation"] if "rotation" in tf else tf["rotate"]
        rx, ry, rz = _vec3(rot)
        cx, sx = math.cos(rx), math.sin(rx)
        cy, sy = math.cos(ry), math.sin(ry)
        cz, sz = math.cos(rz), math.sin(rz)
        rx_m = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
        ry_m = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
        rz_m = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))

        def matmul(p, q):
            return tuple(tuple(sum(p[r][k] * q[k][c] for k in range(3)) for c in range(3))
                         for r in range(3))
        basis = matmul(matmul(ry_m, rx_m), rz_m)
    return Transform(basis, tuple(float(x) for x in tr))


def _base_texture(name):
    if not name:
        return None
    low = name.lower()
    return name[:-4] if low.endswith(".tif") else name


def _first_property(d):
    for k, v in d.items():
        return k, v
    raise ValueError("empty enum wrapper object")


def load_chapter(extracted, chapter):
    base = os.path.join(extracted, chapter, "gamez")
    with open(os.path.join(base, "nodes.json"), encoding="utf-8") as f:
        raw_nodes = json.load(f)
    with open(os.path.join(base, "models.json"), encoding="utf-8") as f:
        raw_models = json.load(f)
    with open(os.path.join(base, "textures.json"), encoding="utf-8") as f:
        raw_textures = json.load(f)
    with open(os.path.join(base, "materials.json"), encoding="utf-8") as f:
        raw_materials = json.load(f)

    # `textures.json` stores names WITH the `.tif` suffix (`cblock1.tif`); the census and this
    # script key on the base name, so normalise once here rather than at every comparison.
    texture_names = [_base_texture(t.get("name") or t.get("original")) for t in raw_textures]
    materials = []
    for wrapper in raw_materials:
        if "Textured" in wrapper:
            ti = wrapper["Textured"].get("texture_index")
            materials.append(texture_names[ti] if ti is not None and 0 <= ti < len(texture_names)
                             else None)
        else:
            materials.append(None)

    meshes = []
    for m in raw_models:
        if not isinstance(m, dict):
            meshes.append(None)
            continue
        mesh = Mesh()
        mesh.vertices = [_vec3(v) for v in m.get("vertices", [])]
        for p in m.get("polygons", []):
            poly = Polygon()
            poly.vertex_indices = list(p.get("vertex_indices", []))
            flags = p.get("flags", {})
            poly.tri_strip = bool(flags.get("triangle_strip") or flags.get("tri_strip"))
            poly.unk3 = bool(flags.get("unk3"))
            poly.priority = p.get("priority")
            for layer in p.get("materials") or []:
                mi = layer.get("material_index", -1)
                uv_raw = layer.get("uv_coords")
                uv = [(c["u"], c["v"]) for c in uv_raw] if isinstance(uv_raw, list) else None
                poly.layers.append((mi, uv))
            mesh.polygons.append(poly)
        meshes.append(mesh)

    nodes = []
    for i, wrapper in enumerate(raw_nodes):
        data = wrapper.get("data")
        if not isinstance(data, dict):
            raise ValueError(f"{chapter} node {i}: not the unified extraction shape")
        kind, body = _first_property(data)
        n = Node()
        n.kind = kind
        n.name = wrapper.get("name", "") or ""
        mi = wrapper.get("model_index", -1)
        n.model_index = mi if mi is not None else -1
        n.children = list(wrapper.get("child_indices") or [])
        if kind == "Lod":
            n.lod_range_min = body.get("range", {}).get("min", -1.0)
        if kind == "World":
            roots, seen = [], set()
            for row in body.get("partitions") or []:
                for cell in row:
                    for ref in (cell.get("values") or cell.get("nodes") or []):
                        idx = ref.get("node_index", ref.get("index"))
                        if idx is not None and idx not in seen:
                            seen.add(idx)
                            roots.append(idx)
                    for idx in cell.get("node_indices") or []:
                        if idx not in seen:
                            seen.add(idx)
                            roots.append(idx)
            n.partition_roots = roots
        tf = body.get("transformation")
        if isinstance(tf, dict):
            n.local = _parse_transform(tf)
        else:
            tf2 = body.get("transform")
            if isinstance(tf2, dict):
                _, inner = _first_property(tf2)
                n.local = _parse_transform(inner)
        nodes.append(n)

    return nodes, meshes, materials


def skip_world_node(node):
    n = node.name.lower()
    return n == "horizon" or n == "dzpaths" or n.startswith("fvol")


# ---------------------------------------------------------------- the record

class PolyRec:
    """One world polygon of a target texture, measured in WORLD space."""
    __slots__ = ("texture", "flagged", "node", "node_index", "poly_index", "priority",
                 "layer_count", "layer_pos", "cx", "cy", "cz", "x0", "x1", "z0", "z1",
                 "y_mean", "xz_area", "nverts", "vertical")

    def label(self):
        return f"{self.node}[{self.node_index}]#{self.poly_index}"


def polygon_records(nodes, meshes, materials, targets):
    """Walks world1 as ClutterBuilder.PlaceOnWorld does and returns (records, diagnostics)."""
    recs = []
    diag = {"visited_polys": 0, "dup_skipped": 0, "too_few_verts": 0, "bad_vertex_index": 0,
            "zero_xz_area": 0, "nan": 0}
    world_idx = next((i for i, n in enumerate(nodes)
                      if n.kind == "World" and n.name.lower() == "world1"), None)
    if world_idx is None:
        return recs, diag
    world = nodes[world_idx]
    seen_polys = set()

    def process(node_index, mesh, xf):
        for pi, poly in enumerate(mesh.polygons):
            hits = []
            for layer_pos, (mi, _uv) in enumerate(poly.layers):
                tex = materials[mi] if 0 <= mi < len(materials) else None
                if tex is not None and tex in targets:
                    hits.append((layer_pos, tex))
            if not hits:
                continue
            key = (node_index, pi)
            if key in seen_polys:
                diag["dup_skipped"] += len(hits)
                continue
            seen_polys.add(key)
            diag["visited_polys"] += 1
            n = len(poly.vertex_indices)
            if n < 3:
                diag["too_few_verts"] += 1
                continue
            pts = []
            bad = False
            for vi in poly.vertex_indices:
                if not (0 <= vi < len(mesh.vertices)):
                    bad = True
                    break
                pts.append(xf.xform(mesh.vertices[vi]))
            if bad:
                diag["bad_vertex_index"] += 1
                continue
            if any(not all(map(math.isfinite, p)) for p in pts):
                diag["nan"] += 1
                continue
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            zs = [p[2] for p in pts]
            # Plan-projected (XZ) area by the shoelace formula over the polygon ring. Signed area
            # is |sum| / 2; a vertical wall quad projects to a line and gives 0 — that is real,
            # not a failure, so it is counted and kept (DIAG-15).
            s = 0.0
            for k in range(n):
                x0, z0 = xs[k], zs[k]
                x1, z1 = xs[(k + 1) % n], zs[(k + 1) % n]
                s += x0 * z1 - x1 * z0
            xz_area = abs(s) * 0.5
            if xz_area < EPS_AREA:
                diag["zero_xz_area"] += 1
            for layer_pos, tex in hits:
                r = PolyRec()
                r.texture = tex
                r.flagged = poly.unk3
                r.node = nodes[node_index].name
                r.node_index = node_index
                r.poly_index = pi
                r.priority = poly.priority
                r.layer_count = len(poly.layers)
                r.layer_pos = layer_pos
                r.cx = sum(xs) / n
                r.cy = sum(ys) / n
                r.cz = sum(zs) / n
                r.x0, r.x1 = min(xs), max(xs)
                r.z0, r.z1 = min(zs), max(zs)
                r.y_mean = sum(ys) / n
                r.xz_area = xz_area
                r.nverts = n
                r.vertical = xz_area < EPS_AREA
                recs.append(r)

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes):
            return
        node = nodes[idx]
        if skip_world_node(node) or (node.kind == "Lod" and node.lod_range_min != 0.0):
            return
        if node.local is not None:
            xf = xf.compose(node.local)
        if 0 <= node.model_index < len(meshes) and meshes[node.model_index] is not None:
            process(idx, meshes[node.model_index], xf)
        for c in node.children:
            walk(c, xf)

    for c in world.children:
        walk(c, IDENTITY)
    for p in (world.partition_roots or []):
        walk(p, IDENTITY)
    return recs, diag


# ---------------------------------------------------------------- the overlap instrument

def bbox_overlap(a, b, margin=0.0):
    """True when two records' XZ bounding boxes intersect (with an optional slack margin)."""
    return not (a.x1 + margin < b.x0 or b.x1 + margin < a.x0
                or a.z1 + margin < b.z0 or b.z1 + margin < a.z0)


def overlap_area(a, b):
    ox = min(a.x1, b.x1) - max(a.x0, b.x0)
    oz = min(a.z1, b.z1) - max(a.z0, b.z0)
    return max(0.0, ox) * max(0.0, oz)


def best_partner(flagged, candidates, margin):
    """The unflagged candidate with the largest XZ bbox intersection, or None."""
    best, best_area = None, 0.0
    fallback = None
    for c in candidates:
        if not bbox_overlap(flagged, c, margin):
            continue
        a = overlap_area(flagged, c)
        if a > best_area:
            best, best_area = c, a
        elif fallback is None:
            fallback = c  # degenerate (zero-area) intersection but boxes touch
    if best is not None:
        return best, best_area
    if fallback is not None:
        return fallback, 0.0
    return None, 0.0


class Grid:
    """Uniform XZ bucket index over the unflagged set — the world is ~40 km across and an
    all-pairs test over thousands of quads is needlessly slow."""

    def __init__(self, recs, cell=512.0):
        self.cell = cell
        self.buckets = {}
        for r in recs:
            for key in self._keys(r.x0, r.x1, r.z0, r.z1):
                self.buckets.setdefault(key, []).append(r)

    def _keys(self, x0, x1, z0, z1):
        c = self.cell
        for i in range(int(math.floor(x0 / c)), int(math.floor(x1 / c)) + 1):
            for j in range(int(math.floor(z0 / c)), int(math.floor(z1 / c)) + 1):
                yield (i, j)

    def near(self, r, margin):
        out, seen = [], set()
        for key in self._keys(r.x0 - margin, r.x1 + margin, r.z0 - margin, r.z1 + margin):
            for c in self.buckets.get(key, ()):
                k = id(c)
                if k not in seen:
                    seen.add(k)
                    out.append(c)
        return out


# ---------------------------------------------------------------- self-verification (METHOD-9)

def _synthetic(x0, x1, z0, z1, y, flagged):
    r = PolyRec()
    r.texture = "synthetic"
    r.flagged = flagged
    r.node = "synthetic"
    r.node_index = -1
    r.poly_index = -1
    r.priority = None
    r.layer_count = 1
    r.layer_pos = 0
    r.x0, r.x1, r.z0, r.z1 = x0, x1, z0, z1
    r.cx, r.cz = (x0 + x1) / 2.0, (z0 + z1) / 2.0
    r.cy = r.y_mean = y
    r.xz_area = (x1 - x0) * (z1 - z0)
    r.nverts = 4
    r.vertical = False
    return r


def run_self_checks(margin):
    """The overlap instrument must find a coplanar pair with a KNOWN Y offset, and must report a
    deliberately disjoint pair as disjoint. Without the second half this script would answer (A)
    from any input at all."""
    msgs = []
    ok = True

    flagged = _synthetic(0.0, 100.0, 0.0, 100.0, 10.0, True)
    below = _synthetic(0.0, 100.0, 0.0, 100.0, 9.0, False)   # exactly 1.0 m under, same XZ
    partner, area = best_partner(flagged, [below], margin)
    dy = None if partner is None else partner.y_mean - flagged.y_mean
    passed = partner is not None and abs(dy + 1.0) < 1e-9 and abs(area - 10000.0) < 1e-6
    ok = ok and passed
    msgs.append(f"coplanar pair, known offset -1.0 m -> "
                f"{'no partner' if partner is None else f'dY={dy:+.3f} overlapArea={area:.1f}'} "
                f"({'OK' if passed else 'FAIL'})")

    far = _synthetic(5000.0, 5100.0, 5000.0, 5100.0, 9.0, False)
    partner2, _ = best_partner(flagged, [far], margin)
    passed2 = partner2 is None
    ok = ok and passed2
    msgs.append(f"deliberately disjoint pair -> "
                f"{'disjoint' if passed2 else 'PARTNERED — instrument is inert'} "
                f"({'OK, check can fail' if passed2 else 'FAIL'})")

    # A pair separated by slightly more than the margin must still read disjoint, or the margin
    # would manufacture answer (A) out of merely adjacent city blocks.
    justfar = _synthetic(100.0 + margin + 1.0, 200.0, 0.0, 100.0, 9.0, False)
    partner3, _ = best_partner(flagged, [justfar], margin)
    passed3 = partner3 is None
    ok = ok and passed3
    msgs.append(f"pair {margin + 1.0:.1f} m apart (margin {margin:.1f} m) -> "
                f"{'disjoint' if passed3 else 'PARTNERED — margin too loose'} "
                f"({'OK' if passed3 else 'FAIL'})")

    # An adjacent, edge-sharing pair (city blocks side by side) must NOT count as a coplanar
    # partner when the margin is 0: their boxes touch on a line, intersection area 0.
    touching = _synthetic(100.0, 200.0, 0.0, 100.0, 9.0, False)
    _, area4 = best_partner(touching, [flagged], 0.0)
    passed4 = area4 == 0.0
    ok = ok and passed4
    msgs.append(f"edge-touching pair -> intersection area {area4:.1f} "
                f"({'OK, reported as zero-area contact' if passed4 else 'FAIL'})")

    return ok, msgs


# ---------------------------------------------------------------- reporting

def fmt_box(recs):
    if not recs:
        return "(empty)"
    x0 = min(r.x0 for r in recs)
    x1 = max(r.x1 for r in recs)
    z0 = min(r.z0 for r in recs)
    z1 = max(r.z1 for r in recs)
    ty = sum(r.xz_area for r in recs)
    if ty > 0:
        cx = sum(r.cx * r.xz_area for r in recs) / ty
        cz = sum(r.cz * r.xz_area for r in recs) / ty
        cy = sum(r.y_mean * r.xz_area for r in recs) / ty
    else:
        cx = sum(r.cx for r in recs) / len(recs)
        cz = sum(r.cz for r in recs) / len(recs)
        cy = sum(r.y_mean for r in recs) / len(recs)
    return (f"X[{x0:9.0f}, {x1:9.0f}] Z[{z0:9.0f}, {z1:9.0f}]  "
            f"centroid ({cx:8.0f}, {cy:7.1f}, {cz:8.0f})")


def percentiles(vals, ps):
    if not vals:
        return [None] * len(ps)
    s = sorted(vals)
    out = []
    for p in ps:
        k = min(len(s) - 1, max(0, int(round((p / 100.0) * (len(s) - 1)))))
        out.append(s[k])
    return out


def report_texture(tex, flagged, unflagged, margin, out):
    out.append(f"== {tex} ==")
    out.append(f"  flagged   n={len(flagged):5d}  {fmt_box(flagged)}")
    out.append(f"  unflagged n={len(unflagged):5d}  {fmt_box(unflagged)}")
    if not flagged or not unflagged:
        out.append("  one set is empty — no overlap question to answer")
        return None

    fx0, fx1 = min(r.x0 for r in flagged), max(r.x1 for r in flagged)
    fz0, fz1 = min(r.z0 for r in flagged), max(r.z1 for r in flagged)
    ux0, ux1 = min(r.x0 for r in unflagged), max(r.x1 for r in unflagged)
    uz0, uz1 = min(r.z0 for r in unflagged), max(r.z1 for r in unflagged)
    boxes_disjoint = (fx1 < ux0 or ux1 < fx0 or fz1 < uz0 or uz1 < fz0)
    out.append(f"  set bounding boxes {'DISJOINT' if boxes_disjoint else 'overlap'}")

    # Y extent per set. Answer (A) needs the unflagged set a SMALL Y BELOW the flagged one; if
    # both sets sit at one single Y there is no underlay to find and (A) is dead whatever the
    # XZ overlap says.
    fy = [r.y_mean for r in flagged]
    uy = [r.y_mean for r in unflagged]
    out.append(f"  flagged   Y: min={min(fy):.3f} max={max(fy):.3f} "
               f"distinct={len(set(round(v, 3) for v in fy))}")
    out.append(f"  unflagged Y: min={min(uy):.3f} max={max(uy):.3f} "
               f"distinct={len(set(round(v, 3) for v in uy))}")

    grid = Grid(unflagged)
    dys, matched, areas = [], 0, []
    # Three thresholds, because "the bounding boxes touch" is NOT stacking: two city-block quads
    # laid side by side on one ground plane share an edge and would otherwise be scored as a
    # coplanar partner, manufacturing answer (A) out of a tiling.
    contact = real = substantial = coincident = 0
    unmatched_examples = []
    for f in flagged:
        partner, area = best_partner(f, grid.near(f, margin), margin)
        if partner is None:
            if len(unmatched_examples) < 4:
                unmatched_examples.append(f)
            continue
        matched += 1
        contact += 1
        cov = area / f.xz_area if f.xz_area > EPS_AREA else float("nan")
        if area > EPS_AREA:
            real += 1
        if cov == cov and cov >= 0.5:
            substantial += 1
        if (abs(partner.x0 - f.x0) < 0.01 and abs(partner.x1 - f.x1) < 0.01
                and abs(partner.z0 - f.z0) < 0.01 and abs(partner.z1 - f.z1) < 0.01):
            coincident += 1
        dys.append(partner.y_mean - f.y_mean)
        areas.append(cov)
    n = len(flagged)
    frac = 100.0 * substantial / n
    out.append(f"  flagged polys, by strength of the best unflagged partner (margin "
               f"{margin:.0f} m), n={n}:")
    out.append(f"    bbox contact (incl. edge-only):    {contact:4d} = {100.0 * contact / n:5.1f}%")
    out.append(f"    non-zero intersection area:        {real:4d} = {100.0 * real / n:5.1f}%")
    out.append(f"    intersection >= 50% of flagged:    {substantial:4d} = {frac:5.1f}%   "
               f"<- the test for (A)")
    out.append(f"    XZ bbox coincident to 1 cm:        {coincident:4d} = "
               f"{100.0 * coincident / n:5.1f}%")
    if dys:
        neg = sum(1 for d in dys if d < -0.001)
        pos = sum(1 for d in dys if d > 0.001)
        zero = len(dys) - neg - pos
        p = percentiles(dys, [5, 25, 50, 75, 95])
        out.append(f"    signed dY (partner - flagged): min={min(dys):+.2f} "
                   f"p5={p[0]:+.2f} p25={p[1]:+.2f} median={p[2]:+.2f} p75={p[3]:+.2f} "
                   f"p95={p[4]:+.2f} max={max(dys):+.2f}")
        out.append(f"    sign split: below(partner lower)={neg}  same(+/-1mm)={zero}  "
                   f"above={pos}")
        small = sum(1 for d in dys if abs(d) <= 5.0)
        out.append(f"    |dY| <= 5 m: {small}/{len(dys)} = {100.0 * small / len(dys):.1f}%")
        cov = [a for a in areas if a == a]
        if cov:
            pc = percentiles(cov, [5, 50, 95])
            out.append(f"    partner bbox covers flagged bbox: p5={100 * pc[0]:.0f}% "
                       f"median={100 * pc[1]:.0f}% p95={100 * pc[2]:.0f}%")
    if unmatched_examples:
        out.append("    examples with NO partner:")
        for f in unmatched_examples:
            out.append(f"      {f.label():36s} c=({f.cx:8.0f},{f.y_mean:7.1f},{f.cz:8.0f}) "
                       f"xzArea={f.xz_area:10.0f}")
    return frac


def report_layers_and_priority(tex, flagged, unflagged, out):
    def summarise(recs):
        if not recs:
            return "n/a"
        lc = {}
        for r in recs:
            lc[r.layer_count] = lc.get(r.layer_count, 0) + 1
        pr = {}
        for r in recs:
            pr[r.priority] = pr.get(r.priority, 0) + 1
        lp = {}
        for r in recs:
            lp[r.layer_pos] = lp.get(r.layer_pos, 0) + 1
        return (f"layers={dict(sorted(lc.items()))} matched_layer_pos={dict(sorted(lp.items()))} "
                f"priority={dict(sorted(pr.items(), key=lambda kv: (kv[0] is None, kv[0])))}")
    out.append(f"  {tex} flagged   {summarise(flagged)}")
    out.append(f"  {tex} unflagged {summarise(unflagged)}")


def report_pose(recs, radius, out):
    out.append(f"== within {radius:.0f} m of BL-305's pose ({POSE_X:.0f}, {POSE_Z:.0f}) ==")
    near = []
    for r in recs:
        # Distance from the pose to the polygon's XZ bbox (0 when the pose is inside it).
        dx = max(r.x0 - POSE_X, 0.0, POSE_X - r.x1)
        dz = max(r.z0 - POSE_Z, 0.0, POSE_Z - r.z1)
        d = math.hypot(dx, dz)
        if d <= radius:
            near.append((d, r))
    near.sort(key=lambda dr: dr[0])
    nf = sum(1 for _, r in near if r.flagged)
    out.append(f"  {len(near)} polygons: {nf} flagged, {len(near) - nf} unflagged")
    by_tex = {}
    for d, r in near:
        by_tex.setdefault(r.texture, [0, 0])[0 if r.flagged else 1] += 1
    for tex in sorted(by_tex):
        out.append(f"    {tex}: flagged={by_tex[tex][0]} unflagged={by_tex[tex][1]}")
    out.append("    dist  flag  texture   node                       y_mean    xzArea  "
               "centroid")
    for d, r in near:
        out.append(f"    {d:6.0f}  {'F' if r.flagged else '.'}     {r.texture:9s} "
                   f"{r.label():26s} {r.y_mean:7.1f} {r.xz_area:9.0f}  "
                   f"({r.cx:.0f}, {r.cz:.0f})")


def report_field_map(recs, cell, out, textures_label="cblock1/2/3/7"):
    """A coarse XZ map of the flagged/unflagged split over the WHOLE cblock field.

    Added after the orchestrator's mid-task correction: the comparison pose `-9490,-3300` was
    only ever SCALE-matched to the original's nadir chase-cam still (`playtest/CAP-22/README.md`),
    never position-matched, and the original's frame is wherever the mission path took it. So
    "the ground under our pose is 100 % flagged" is only a contradiction if the original's towers
    are over the SAME ground. If parts of C5's city are predominantly unflagged, the original's
    frame may simply be over one of those and there is nothing to explain.

    Each polygon's XZ area is distributed across the cells its bounding box covers, in proportion
    to the bbox/cell intersection — a 1024 m quad spanning four 512 m cells contributes a quarter
    of its area to each, rather than dumping all of it wherever its centroid happens to land."""
    if not recs:
        out.append("no polygons — nothing to map")
        return
    x0 = min(r.x0 for r in recs)
    x1 = max(r.x1 for r in recs)
    z0 = min(r.z0 for r in recs)
    z1 = max(r.z1 for r in recs)
    ci0, ci1 = int(math.floor(x0 / cell)), int(math.floor(x1 / cell))
    cj0, cj1 = int(math.floor(z0 / cell)), int(math.floor(z1 / cell))
    grid = {}  # (i, j) -> [flagged_area, unflagged_area, flagged_n, unflagged_n]

    for r in recs:
        bw = max(r.x1 - r.x0, 1e-9)
        bh = max(r.z1 - r.z0, 1e-9)
        for i in range(int(math.floor(r.x0 / cell)), int(math.floor(r.x1 / cell)) + 1):
            for j in range(int(math.floor(r.z0 / cell)), int(math.floor(r.z1 / cell)) + 1):
                ox = min(r.x1, (i + 1) * cell) - max(r.x0, i * cell)
                oz = min(r.z1, (j + 1) * cell) - max(r.z0, j * cell)
                if ox <= 0 or oz <= 0:
                    continue
                share = (ox * oz) / (bw * bh)
                slot = grid.setdefault((i, j), [0.0, 0.0, 0, 0])
                slot[0 if r.flagged else 1] += r.xz_area * share
        ci = int(math.floor(r.cx / cell))
        cj = int(math.floor(r.cz / cell))
        grid.setdefault((ci, cj), [0.0, 0.0, 0, 0])[2 if r.flagged else 3] += 1

    out.append(f"field extent ({textures_label}): X[{x0:.0f}, {x1:.0f}] Z[{z0:.0f}, {z1:.0f}] "
               f"= {(x1 - x0) / 1000.0:.1f} x {(z1 - z0) / 1000.0:.1f} km")
    pi, pj = int(math.floor(POSE_X / cell)), int(math.floor(POSE_Z / cell))
    out.append(f"BL-305 pose ({POSE_X:.0f}, {POSE_Z:.0f}) sits at "
               f"{100.0 * (POSE_X - x0) / (x1 - x0):.0f}% across X and "
               f"{100.0 * (POSE_Z - z0) / (z1 - z0):.0f}% across Z of that field, cell "
               f"({pi}, {pj}).")
    out.append("")
    out.append(f"Map: one character per {cell:.0f} m cell, X increasing rightwards (west->east "
               f"as -X..0), Z increasing downwards. Character = FLAGGED share of the cell's "
               f"cblock area:")
    out.append("  '#' >=90%   '+' 60-90%   'o' 30-60%   '-' 5-30%   '.' <5%   ' ' no cblock "
               "   '@' = the pose's cell (drawn over whatever it was)")
    out.append("")
    header = "        " + "".join(str(abs(i) % 10) for i in range(ci0, ci1 + 1))
    out.append(header)
    for j in range(cj0, cj1 + 1):
        row = []
        for i in range(ci0, ci1 + 1):
            slot = grid.get((i, j))
            if slot is None or (slot[0] + slot[1]) <= 0.0:
                ch = " "
            else:
                frac = slot[0] / (slot[0] + slot[1])
                ch = ("#" if frac >= 0.9 else "+" if frac >= 0.6 else "o" if frac >= 0.3
                      else "-" if frac >= 0.05 else ".")
            if (i, j) == (pi, pj):
                ch = "@"
            row.append(ch)
        out.append(f"  z={j * cell:7.0f} " + "".join(row))
    out.append("")

    occupied = [(k, v) for k, v in grid.items() if (v[0] + v[1]) > 0.0]
    tot_f = sum(v[0] for _, v in occupied)
    tot_u = sum(v[1] for _, v in occupied)
    out.append(f"cells with cblock area: {len(occupied)}   "
               f"flagged area {tot_f:.0f} ({100.0 * tot_f / (tot_f + tot_u):.1f}%)   "
               f"unflagged area {tot_u:.0f}")
    bands = {">=90": 0, "60-90": 0, "30-60": 0, "5-30": 0, "<5": 0}
    for _, v in occupied:
        frac = v[0] / (v[0] + v[1])
        key = (">=90" if frac >= 0.9 else "60-90" if frac >= 0.6 else "30-60" if frac >= 0.3
               else "5-30" if frac >= 0.05 else "<5")
        bands[key] += 1
    out.append("cells by flagged share of area: " + "  ".join(
        f"{k}%={v}" for k, v in bands.items()))

    # The biggest predominantly-UNFLAGGED cells: candidate districts the original could be over.
    mostly_unflagged = sorted(
        (v for v in occupied if v[1][0] / (v[1][0] + v[1][1]) < 0.05),
        key=lambda kv: -kv[1][1])[:10]
    out.append("largest cells that are <5% flagged (where the original WOULD stamp clutter):")
    for (i, j), v in mostly_unflagged:
        cx, cz = (i + 0.5) * cell, (j + 0.5) * cell
        out.append(f"  cell ({i:4d},{j:4d}) centre ({cx:8.0f},{cz:8.0f})  unflagged area "
                   f"{v[1]:10.0f}  distFromPose {math.hypot(cx - POSE_X, cz - POSE_Z):7.0f}")

    # Districts = 8-connected clumps of occupied cells. "Is there more than one cblock district?"
    seen = set()
    comps = []
    occ_keys = {k for k, _ in occupied}
    for key in occ_keys:
        if key in seen:
            continue
        stack, comp = [key], []
        seen.add(key)
        while stack:
            (i, j) = stack.pop()
            comp.append((i, j))
            for di in (-1, 0, 1):
                for dj in (-1, 0, 1):
                    nk = (i + di, j + dj)
                    if nk in occ_keys and nk not in seen:
                        seen.add(nk)
                        stack.append(nk)
        comps.append(comp)
    comps.sort(key=lambda c: -sum(grid[k][0] + grid[k][1] for k in c))
    out.append(f"connected cblock districts (8-connected {cell:.0f} m cells): {len(comps)}")
    for c in comps[:8]:
        fa = sum(grid[k][0] for k in c)
        ua = sum(grid[k][1] for k in c)
        xs = [k[0] for k in c]
        zs = [k[1] for k in c]
        contains_pose = (pi, pj) in set(c)
        out.append(f"  {len(c):4d} cells  X[{min(xs) * cell:8.0f},{(max(xs) + 1) * cell:8.0f}] "
                   f"Z[{min(zs) * cell:8.0f},{(max(zs) + 1) * cell:8.0f}]  "
                   f"flagged {100.0 * fa / (fa + ua):5.1f}% of {fa + ua:11.0f} m^2"
                   f"{'   <- contains the pose' if contains_pose else ''}")


def report_where_unflagged(tex, unflagged, out, limit=12):
    out.append(f"  {tex}: unflagged polygons by owning node (top {limit} by count)")
    by_node = {}
    for r in unflagged:
        by_node.setdefault((r.node, r.node_index), []).append(r)
    ordered = sorted(by_node.items(), key=lambda kv: -len(kv[1]))[:limit]
    for (name, idx), rs in ordered:
        ty = sum(r.xz_area for r in rs)
        if ty > 0:
            cx = sum(r.cx * r.xz_area for r in rs) / ty
            cz = sum(r.cz * r.xz_area for r in rs) / ty
            cy = sum(r.y_mean * r.xz_area for r in rs) / ty
        else:
            cx = sum(r.cx for r in rs) / len(rs)
            cz = sum(r.cz for r in rs) / len(rs)
            cy = sum(r.y_mean for r in rs) / len(rs)
        dist = math.hypot(cx - POSE_X, cz - POSE_Z)
        out.append(f"    {name}[{idx}]{'':<{max(0, 22 - len(name))}} n={len(rs):4d} "
                   f"centroid=({cx:8.0f}, {cy:7.1f}, {cz:8.0f}) "
                   f"xzArea={ty:11.0f} distFromPose={dist:7.0f}")


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--extracted", default="extracted",
                    help="Path to the extracted/ tree. This worktree has none (git-ignored); "
                         "pass --extracted Z:/CSVM/extracted.")
    ap.add_argument("--chapter", default="C5")
    ap.add_argument("--textures", default=",".join(TARGET_TEXTURES),
                    help="Comma-separated texture base names (the extraction stores them without "
                         "the .tif suffix).")
    ap.add_argument("--margin", type=float, default=0.0,
                    help="XZ slack in metres when testing bbox overlap. 0 = strict.")
    ap.add_argument("--cell", type=float, default=512.0,
                    help="XZ cell size for the whole-field flagged/unflagged map.")
    ap.add_argument("--radius", type=float, default=600.0,
                    help="Radius around BL-305's pose for the local listing.")
    args = ap.parse_args()

    targets = {_base_texture(t.strip()) for t in args.textures.split(",") if t.strip()}

    print("== self-checks (METHOD-9) ==")
    ok, msgs = run_self_checks(args.margin)
    for m in msgs:
        print(f"  {m}")
    if not ok:
        print("SELF-CHECK FAILED - refusing to trust the measurement below.", file=sys.stderr)
        return 2
    print()

    path = os.path.join(args.extracted, args.chapter, "gamez", "nodes.json")
    if not os.path.exists(path):
        print(f"{args.chapter}: missing extraction at {path}", file=sys.stderr)
        return 2

    nodes, meshes, materials = load_chapter(args.extracted, args.chapter)
    recs, diag = polygon_records(nodes, meshes, materials, targets)

    print(f"== {args.chapter} walk diagnostics (DIAG-15: nothing skipped silently) ==")
    print(f"  target-texture polygons visited: {diag['visited_polys']}   "
          f"records (one per matching layer): {len(recs)}")
    print(f"  dropped: <3 verts={diag['too_few_verts']} bad_vertex_index="
          f"{diag['bad_vertex_index']} non-finite={diag['nan']}")
    print(f"  reachable twice (dedup by node+poly, NOT dropped from the first visit): "
          f"{diag['dup_skipped']}")
    print(f"  kept but zero XZ area (vertical faces; kept for the overlap test): "
          f"{diag['zero_xz_area']}")
    print()

    out = []
    out.append("== the whole-field map: is the flag uniform over C5's city? ==")
    report_field_map(recs, args.cell, out)
    for line in out:
        print(line)
    print()

    for tex in sorted(targets):
        sub = [r for r in recs if r.texture == tex]
        out = [f"== field map, {tex} alone =="]
        report_field_map(sub, args.cell, out, textures_label=tex)
        for line in out:
            print(line)
        print()

    frac_by_tex = {}
    for tex in sorted(targets):
        sub = [r for r in recs if r.texture == tex]
        flagged = [r for r in sub if r.flagged]
        unflagged = [r for r in sub if not r.flagged]
        out = []
        frac = report_texture(tex, flagged, unflagged, args.margin, out)
        report_layers_and_priority(tex, flagged, unflagged, out)
        report_where_unflagged(tex, unflagged, out)
        for line in out:
            print(line)
        print()
        if frac is not None:
            frac_by_tex[tex] = frac

    out = []
    report_pose(recs, args.radius, out)
    for line in out:
        print(line)
    print()

    print("== verdict inputs ==")
    for tex, frac in sorted(frac_by_tex.items()):
        print(f"  {tex}: {frac:.1f}% of flagged polygons have an overlapping unflagged partner")
    if frac_by_tex:
        lo, hi = min(frac_by_tex.values()), max(frac_by_tex.values())
        print(f"  spread across textures: {lo:.1f}% .. {hi:.1f}%"
              f"{'  <-- textures DISAGREE, do not average them' if hi - lo > 25.0 else ''}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
