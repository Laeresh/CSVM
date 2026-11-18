"""Generates C5's block floor for the Enhanced city (PLAN-c5-ground B12): per combination of visible
paint and standing building set, two to four floor variants, each a 512 x 512 sRGB image covering
one 256 m tile in the template's own UV (+U right, +V down the image), seamless across the tile
edges, and beside each the layout it was painted from, for the dressing stamper.

Reads only C5/ground_layout.json (analysis/c5-ground/export_layout.py); no game file. Texels in a
street band or under a lot are don't-care: the engine draws the original street there and a building
stands on the rest. They hold the sidewalk and the yard surfaces carried on, so a mip level at a kerb
or a wall blends floor with floor.

A block is laid out the way a 1930s Manhattan block reads from the air: a sidewalk verge along the
kerb, a ring of lots behind it, each building on a lot of its own width with a paved apron and a yard
behind it, the vacant lots between cut 8 to 14 m wide, lot-line fences and rear walls, and the core
behind the ring cut into service yards, courts and gardens, with a service alley through or into it
where no building stands in the way. A combination's character (WALKUP, DOWNTOWN, INNER_WALKUP) sets
the surfaces and the green share: about a quarter of the free floor in the walk-up districts
(Decision 12), about a tenth in the inner city, which is paved courts, service yards with loading
docks, plazas and parking (Decision 18).

The diagonal paints (cblock2, cblock3: one 45 degree street per tile, A2) are laid out the same
way in each strip's own frame (generate_strip), a strip being 181 m between centrelines with no
cross street, so it has two kerbed rings, a core and cross alleys; the engine picks a variant per
strip (Decision 7), and the layout JSON carries the frame that turns it into UV.

Every variant keeps the same buildings and streets and rolls its own ring depths, lot cuts, alley,
core cuts and surfaces from a fixed seed, so a rerun writes the same bytes. The layout is built in a
frame rolled so that the paint's street crossing sits on the tile corner and its block in the middle
of the image, then rolled back; a block crosses the tile edges, and the roll keeps it seamless there.

  py -I CSVM\\data\\city_models\\c5_ground.py [--luma 30 --out <dir>]

Outputs, in C5/ground unless --out names another folder:
- <paint>-<set>-<n>.png.bin, the floor (.bin, as every image under data/, so Godot imports none and
  the export's data/*.bin filter ships it).
- <paint>-<set>-<n>.json, the layout: rects and lines in the same UV as ground_layout.json, see
  write_layout.
- index.json, each combination's variant count and file names.
"""

import argparse
import json
import os
import struct
import sys
import zlib

# No __pycache__ beside the shipped data.
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np  # noqa: E402

import city_atlas as A  # noqa: E402

LAYOUT = os.path.join(HERE, "C5", "ground_layout.json")
OUT = os.path.join(HERE, "C5", "ground")
SIZE = 512
TILE_M = 256.0
PX = SIZE / TILE_M
# The free floor's mean Rec. 601 luma on sRGB texels: set at the controls under C5's lighting, where
# 20 left the paving's structure unreadable.
LUMA = 35.0
# A diagonal street family (A2): its across axis alpha, c = alpha . uv, whose whole values plus the
# centre are the centrelines, and its along axis beta, d = beta . uv. Both pairs turn the uv frame
# without mirroring it, so a dressing yaw stays a yaw.
FAMILY_AXES = {"u+v": ((1, 1), (1, -1)), "u-v": ((1, -1), (-1, -1))}

SIDEWALK_M = 3.0
ALLEY_M = 5.0
RING_M = (28.0, 40.0)
YARD_MIN_M = 6.0
CORE_CELL_M = (35.0, 70.0)

# Per surface kind: its colour (only the hue counts, each is normalised to luma 1) and its level
# against the floor's mean. The levels span about 0.75 to 1.25, so at a mean of 20 the surfaces
# and lines stay within about 6 levels of it (Decision 3), dim but apart.
KINDS = {
    "sidewalk": ((0.50, 0.50, 0.48), 1.16),
    "concrete": ((0.52, 0.51, 0.48), 1.10),
    "flagstone": ((0.50, 0.47, 0.43), 1.03),
    "asphalt": ((0.40, 0.40, 0.43), 0.82),
    "alley": ((0.40, 0.40, 0.43), 0.86),
    "gravel": ((0.50, 0.46, 0.40), 0.98),
    "dirt": ((0.48, 0.40, 0.31), 0.90),
    "lawn": ((0.31, 0.42, 0.24), 0.88),
    "garden": ((0.31, 0.42, 0.24), 0.88),
    "allotment": ((0.31, 0.42, 0.24), 0.88),
    "bed": ((0.37, 0.33, 0.28), 0.76),
}
# The share of each colour's chroma kept: night washes colour out, and a lifted montage shows a
# fully saturated lawn as a poster green.
CHROMA = 0.7
FENCE, WALL, WALL_SHADOW, PATH, STALL = 0.72, 1.26, 0.82, 1.14, 1.10

# A district's character. yard: a ring lot's surface; vacant: a ring lot with no building (None
# takes yard); core: what a cell behind the ring is (back yards carried on from the ring, a service
# yard, a parking lot, a paved court, an open lot); green: the free floor's lawn and garden share;
# apron: the paving behind a building; lot_w: a vacant lot's width; alley: through, dead end, none.
WALKUP = dict(
    yard=(("concrete", 0.24), ("flagstone", 0.22), ("gravel", 0.22), ("dirt", 0.20), ("asphalt", 0.12)),
    vacant=None,
    core=(("strips", 0.40), ("service", 0.25), ("court", 0.20), ("open", 0.15)),
    green=0.25, greens=(("garden", 0.4), ("lawn", 0.35), ("allotment", 0.25)),
    apron=2.5, lot_w=(8.0, 14.0), alley=(("through", 0.5), ("dead", 0.35), ("none", 0.15)))
# Decision 18, the author after flying the walk-ups: "for the inner city use more pavement less
# gardens". Paved courts, service yards with loading docks behind the towers, plazas and parking on
# the vacant frontage, alleys; about a tenth green, as small lawns and planted courts.
DOWNTOWN = dict(
    yard=(("concrete", 0.34), ("asphalt", 0.30), ("flagstone", 0.22), ("gravel", 0.14)),
    vacant=(("flagstone", 0.40), ("asphalt", 0.35), ("concrete", 0.25)),
    core=(("service", 0.35), ("parking", 0.25), ("court", 0.30), ("strips", 0.10)),
    green=0.10, greens=(("lawn", 0.55), ("garden", 0.45)),
    apron=4.0, lot_w=(12.0, 22.0), alley=(("through", 0.6), ("dead", 0.3), ("none", 0.1)))
# The walk-ups standing on downtown paint lie inside the tower districts: downtown paving and green
# share, with the walk-ups' back-yard strips and narrow lots kept.
INNER_WALKUP = dict(DOWNTOWN, core=(("strips", 0.30), ("service", 0.30), ("parking", 0.15), ("court", 0.25)),
                    apron=2.5, lot_w=(8.0, 14.0))
# Decision 8: flagged ground with no layer beneath stands no building. It is the city's open ground:
# a park on every block, the rest parks, parking lots, plazas and fenced vacant lots. parcel: the
# longest side a use keeps before it is cut again.
OPEN = dict(open=True, parcel=(80.0, 150.0), uses=(("park", 0.3), ("parking", 0.3), ("plaza", 0.2), ("vacant", 0.2)))
# (visible paint, standing set, variants, character). Decision 4: 2 to 4 per combination.
COMBOS = (("cblock7", "cblock7", 4, WALKUP), ("cblock7", "cblock4", 2, WALKUP),
          ("cblock1", "cblock1", 4, DOWNTOWN), ("cblock1", "cblock4", 3, INNER_WALKUP),
          ("cblock2", "cblock2", 3, DOWNTOWN), ("cblock2", "cblock5", 3, INNER_WALKUP),
          ("cblock3", "cblock3", 2, DOWNTOWN), ("cblock3", "cblock6", 3, INNER_WALKUP),
          ("cblock7", "none", 3, OPEN), ("cblock1", "none", 3, OPEN), ("cblock2", "none", 3, OPEN),
          ("cblock3", "none", 3, OPEN))


def _pick(rng, table):
    names, w = zip(*table)
    return names[rng.choice(len(names), p=np.array(w) / sum(w))]


def px(m, pxm=PX):
    return int(round(m * pxm))


# ---------------------------------------------------------------- masks, in the rolled frame

def _fill_convex(mask, pts):
    """Sets the pixels whose centres lie inside a convex polygon (pixel coordinates, either winding)."""
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    x0, x1 = max(int(np.floor(min(xs))), 0), min(int(np.ceil(max(xs))), mask.shape[1])
    y0, y1 = max(int(np.floor(min(ys))), 0), min(int(np.ceil(max(ys))), mask.shape[0])
    if x0 >= x1 or y0 >= y1:
        return
    X, Y = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
    pos = np.ones(X.shape, bool)
    neg = np.ones(X.shape, bool)
    for (xa, ya), (xb, yb) in zip(pts, pts[1:] + pts[:1]):
        cr = (xb - xa) * (Y - ya) - (yb - ya) * (X - xa)
        pos &= cr >= -1e-9
        neg &= cr <= 1e-9
    mask[y0:y1, x0:x1] |= pos | neg


def lot_mask(lots, shift):
    """Every lot's footprint, drawn again shifted by a tile each way (ground_layout's convention).
    shift is the (u, v) texel offset of the frame: 0 for the image, the roll for the rolled frame."""
    mask = np.zeros((SIZE, SIZE), bool)
    for lot in lots:
        for poly in lot["poly"]:
            base = [(poly[i] * SIZE - shift[0], poly[i + 1] * SIZE - shift[1]) for i in range(0, len(poly), 2)]
            for du in range(-2, 3):
                for dv in range(-2, 3):
                    _fill_convex(mask, [(x + du * SIZE, y + dv * SIZE) for x, y in base])
    return mask


def street_mask(streets, shift):
    c = (np.arange(SIZE) + 0.5)
    mask = np.zeros((SIZE, SIZE), bool)
    for st in streets:
        if st["family"] in FAMILY_AXES:
            # A diagonal street, in the image frame only (shift 0).
            alpha = FAMILY_AXES[st["family"]][0]
            d = (alpha[0] * c[None, :] + alpha[1] * c[:, None]) / SIZE - st["centre"]
            mask |= np.abs(d - np.round(d)) <= st["half"]
            continue
        axis = {"u": 1, "v": 0}[st["family"]]
        d = (c + shift[1 - axis]) / SIZE - st["centre"]
        band = np.abs(d - np.round(d)) <= st["half"]
        mask |= band[None, :] if axis == 1 else band[:, None]
    return mask


# ---------------------------------------------------------------- the layout, in rolled metres

def _bbox(lot, shift):
    """A lot's bounding box in rolled metres, its origin wrapped into the rolled tile."""
    ou, ov = lot["o"]
    su = (ou - shift[0] / SIZE) % 1.0
    sv = (ov - shift[1] / SIZE) % 1.0
    us = [su + x - ou for p in lot["poly"] for x in p[0::2]]
    vs = [sv + y - ov for p in lot["poly"] for y in p[1::2]]
    return [min(us) * TILE_M, min(vs) * TILE_M, max(us) * TILE_M, max(vs) * TILE_M]


class Layout:
    """Cells (kind, rect, region, lot) in paint order, lines (kind, a0, b0, a1, b1) and paths
    (polylines), all in rolled metres: a across the image, b down it."""

    def __init__(self):
        self.cells, self.lines, self.paths = [], [], []

    def cell(self, kind, rect, region, lot=None):
        if rect[2] - rect[0] > 0.25 and rect[3] - rect[1] > 0.25:
            self.cells.append(dict(kind=kind, rect=list(rect), region=region, lot=lot))
            return self.cells[-1]
        return None

    def line(self, kind, a0, b0, a1, b1):
        if max(abs(a1 - a0), abs(b1 - b0)) > 0.5:
            self.lines.append((kind, a0, b0, a1, b1))

    def flip(self):
        for c in self.cells:
            r = c["rect"]
            c["rect"] = [r[1], r[0], r[3], r[2]]
        self.lines = [(k, b0, a0, b1, a1) for k, a0, b0, a1, b1 in self.lines]
        self.paths = [[(b, a) for a, b in p] for p in self.paths]


def _side_rect(side, block, al0, al1, d0, d1):
    """A rect from a ring side's own frame (along the kerb, depth in from the kerb) to rolled metres."""
    A0, B0, A1, B1 = block
    return {"T": (al0, B0 + d0, al1, B0 + d1), "B": (al0, B1 - d1, al1, B1 - d0),
            "L": (A0 + d0, al0, A0 + d1, al1), "R": (A1 - d1, al0, A1 - d0, al1)}[side]


def _side_local(side, block, r):
    """A rolled rect in a side's frame: (along0, along1, depth0, depth1)."""
    A0, B0, A1, B1 = block
    return {"T": (r[0], r[2], r[1] - B0, r[3] - B0), "B": (r[0], r[2], B1 - r[3], B1 - r[1]),
            "L": (r[1], r[3], r[0] - A0, r[2] - A0), "R": (r[1], r[3], A1 - r[2], A1 - r[0])}[side]


def _cuts(rng, s0, s1, fixed, lot_w):
    """Splits [s0, s1] into lots: each fixed interval (a building's width, an alley) stays whole, the
    gaps are cut lot_w wide, and a sliver under 4 m joins its neighbour."""
    out, pos = [], s0
    for f0, f1, tag in sorted(fixed) + [(s1, s1, None)]:
        f0, f1 = max(f0, s0), min(f1, s1)
        gap = f0 - pos
        if gap >= 4.0:
            n = max(1, int(round(gap / rng.uniform(*lot_w))))
            w = rng.uniform(0.75, 1.25, n)
            edges = pos + np.concatenate([[0], np.cumsum(w) / w.sum()]) * gap
            out += [(edges[i], edges[i + 1], None) for i in range(n)]
        elif out and out[-1][2] is None and gap > 0:
            out[-1] = (out[-1][0], f0, None)
        elif gap > 0:
            f0 = pos  # the sliver joins the fixed interval that follows
        if tag is not None:
            out.append((f0, f1, tag))
        pos = max(pos, f1)
    return out


def _core_cells(rng, rect, out):
    a0, b0, a1, b1 = rect
    lim = rng.uniform(*CORE_CELL_M)
    w, h = a1 - a0, b1 - b0
    if max(w, h) <= lim or min(w, h) < 2 * 10.0 and max(w, h) < 2 * lim:
        out.append(rect)
        return
    t = rng.uniform(0.35, 0.65)
    if w >= h:
        _core_cells(rng, (a0, b0, a0 + t * w, b1), out)
        _core_cells(rng, (a0 + t * w, b0, a1, b1), out)
    else:
        _core_cells(rng, (a0, b0, a1, b0 + t * h), out)
        _core_cells(rng, (a0, b0 + t * h, a1, b1), out)


def layout(block, lots, rng, ch, strip=False):
    """One variant's block in rolled metres, the alley (if any) running along b. lots are bboxes.
    A strip (the diagonal districts) has kerbs on T and B only and runs on past both ends of a, its
    ends the same ground a period apart: it gets no L or R ring and no sidewalk across its ends."""
    A0, B0, A1, B1 = block
    W = SIDEWALK_M
    L = Layout()
    sides = "TB" if strip else "TBLR"
    # Each lot fronts its nearest kerb; its side's ring is deep enough for it and a yard behind.
    near = {}
    for i, r in enumerate(lots):
        d = {"T": r[1] - B0, "B": B1 - r[3], "L": r[0] - A0, "R": A1 - r[2]}
        near[i] = min((x for x in d if x in sides), key=d.get)
    depth = {s: rng.uniform(*RING_M) for s in sides}
    for i, s in near.items():
        need = _side_local(s, block, lots[i])[3] + ch["apron"] + YARD_MIN_M
        if need < 60.0:
            depth[s] = max(depth[s], need)
    if strip:
        depth["L"] = depth["R"] = 0.0
    full = "TB" if strip or rng.random() < 0.5 else "LR"
    span = {}
    for s in sides:
        if strip:
            span[s] = (A0, A1)
        elif s in full:
            span[s] = ((A0 if s in "TB" else B0) + W, (A1 if s in "TB" else B1) - W)
        elif s in "TB":
            span[s] = (A0 + depth["L"], A1 - depth["R"])
        else:
            span[s] = (B0 + depth["T"], B1 - depth["B"])
    core = (A0 + depth["L"], B0 + depth["T"], A1 - depth["R"], B1 - depth["B"])
    # A lot whose centre stands in another side's ring (a corner) belongs to that ring.
    owner = {}
    for i, r in enumerate(lots):
        ca, cb = (r[0] + r[2]) / 2, (r[1] + r[3]) / 2
        owner[i] = None
        for s in [x for x in sides if x in full] + [x for x in sides if x not in full]:
            al0, al1, d0, d1 = _side_local(s, block, (ca, cb, ca, cb))
            if span[s][0] <= al0 <= span[s][1] and d0 <= depth[s]:
                owner[i] = s
                break

    # The alley runs along b at a, through the core and the T and B rings, where no lot stands.
    mode = _pick(rng, ch["alley"])
    alley = None
    if mode != "none":
        sideT = rng.random() < 0.5
        run = (B0, B1) if mode == "through" else ((B0, core[1] + 1.0) if sideT else (core[3] - 1.0, B1))
        half = ALLEY_M / 2 + 1.0
        free = [x for x in np.arange(max(core[0], span["T"][0], span["B"][0]) + 8.0,
                                      min(core[2], span["T"][1], span["B"][1]) - 8.0, 1.0)
                if not any(r[0] < x + half and r[2] > x - half and r[1] < run[1] and r[3] > run[0] for r in lots)]
        if free:
            x = float(rng.choice(free))
            alley = (x - ALLEY_M / 2, run[0], x + ALLEY_M / 2, run[1], mode, sideT)

    # The sidewalk verge along every kerb.
    L.cell("sidewalk", (A0, B0, A1, B0 + W), "verge")
    L.cell("sidewalk", (A0, B1 - W, A1, B1), "verge")
    if not strip:
        L.cell("sidewalk", (A0, B0 + W, A0 + W, B1 - W), "verge")
        L.cell("sidewalk", (A1 - W, B0 + W, A1, B1 - W), "verge")

    yards = []
    for s in sides:
        fixed = []
        for i, o in owner.items():
            if o == s:
                al0, al1 = _side_local(s, block, lots[i])[:2]
                fixed.append((al0, al1, ("lot", i)))
        if alley and s in "TB" and (alley[4] == "through" or alley[5] == (s == "T")):
            fixed.append((alley[0], alley[2], ("alley", None)))
        # Two fixed intervals that overlap merge, so cuts never cross a building.
        merged = []
        for f in sorted(fixed):
            if merged and f[0] < merged[-1][1]:
                prev = merged[-1]
                tag = prev[2] if prev[2][0] == "lot" else f[2]
                merged[-1] = (prev[0], max(prev[1], f[1]), tag)
            else:
                merged.append(f)
        D = depth[s]
        parts = _cuts(rng, span[s][0], span[s][1], merged, ch["lot_w"])
        for k, (al0, al1, tag) in enumerate(parts):
            if tag and tag[0] == "alley":
                continue
            kind = _pick(rng, ch["yard"] if tag or not ch["vacant"] else ch["vacant"])
            parcel = _side_rect(s, block, al0, al1, W, D)
            if tag and tag[0] == "lot":
                i = tag[1]
                _, _, d0, d1 = _side_local(s, block, lots[i])
                L.cell("sidewalk", _side_rect(s, block, al0, al1, W, max(W, d0)), "verge", i)
                L.cell("concrete", _side_rect(s, block, al0, al1, max(W, d0), min(D, d1 + ch["apron"])), "apron", i)
                y = L.cell(kind, _side_rect(s, block, al0, al1, min(D, d1 + ch["apron"]), D), "yard", i)
                door = (al0 + al1) / 2, d1
            else:
                y = L.cell(kind, parcel, "yard")
                if y and ch["vacant"] and kind == "asphalt":
                    y["stalls"] = True
                door = None
            if y:
                y["side"], y["door"], y["depth"] = s, door, (min(D, (door[1] + ch["apron"]) if door else W), D)
                y["along"] = (al0, al1)
                yards.append(y)
            # Lot-line fences between neighbours and along the alley, rear lines toward the core.
            if k > 0 and rng.random() < 0.85:
                a = _side_rect(s, block, al0, al0, W, D)
                L.line("fence", a[0], a[1], a[2], a[3])
            if k + 1 < len(parts) and parts[k + 1][2] and parts[k + 1][2][0] == "alley":
                a = _side_rect(s, block, al1, al1, W, D)
                L.line("fence", a[0], a[1], a[2], a[3])
            if rng.random() < 0.85:
                a = _side_rect(s, block, al0, al1, D, D)
                L.line("wall" if rng.random() < 0.35 else "fence", a[0], a[1], a[2], a[3])

    # The core: cut by the through alley first, then into yards, courts and gardens.
    halves = [core]
    if alley and alley[4] == "through":
        halves = [(core[0], core[1], alley[0], core[3]), (alley[2], core[1], core[2], core[3])]
    rects = []
    for h in halves:
        if h[2] - h[0] > 4.0:
            _core_cells(rng, h, rects)
    cores = []
    for r in rects:
        use = _pick(rng, ch["core"])
        if alley and alley[4] == "dead" and r[0] < alley[2] and r[2] > alley[0] and (
                (alley[5] and abs(r[1] - core[1]) < 0.01) or (not alley[5] and abs(r[3] - core[3]) < 0.01)):
            use = "service"  # the yard the alley leads to
        if use == "strips":
            # Back yards carried on from the ring: lots cut along the nearest kerb, fenced apart.
            dist = {"T": r[1] - B0, "B": B1 - r[3], "L": r[0] - A0, "R": A1 - r[2]}
            s = min((x for x in dist if x in sides), key=dist.get)
            al = (r[0], r[2]) if s in "TB" else (r[1], r[3])
            for k, (s0, s1, _) in enumerate(_cuts(rng, al[0], al[1], [], ch["lot_w"])):
                sr = (s0, r[1], s1, r[3]) if s in "TB" else (r[0], s0, r[2], s1)
                c = L.cell(_pick(rng, ch["yard"]), sr, "yard")
                if c:
                    cores.append(c)
                if k > 0 and rng.random() < 0.85:
                    L.line("fence", sr[0], sr[1], sr[0] if s in "TB" else sr[2], sr[3] if s in "TB" else sr[1])
        else:
            kind = {"service": ("asphalt", "gravel"), "court": ("flagstone", "concrete"), "open": ("dirt", "gravel"),
                    "parking": ("asphalt", "gravel")}[use]
            c = L.cell(kind[int(rng.random() < 0.4)], r, "core")
            if c and use not in ("service", "parking"):
                cores.append(c)
            if c and use == "parking":
                c["stalls"] = True
            if use == "service":
                # A concrete loading strip along one side of the yard.
                e = int(rng.integers(4))
                L.cell("concrete", ((r[0], r[1], r[2], r[1] + 3.0), (r[0], r[3] - 3.0, r[2], r[3]),
                                    (r[0], r[1], r[0] + 3.0, r[3]), (r[2] - 3.0, r[1], r[2], r[3]))[e], "core")
        for e in ((r[0], r[1], r[2], r[1]), (r[0], r[1], r[0], r[3])):
            on_edge = (e[1] == e[3] and abs(e[1] - core[1]) < 0.01) or (e[0] == e[2] and abs(e[0] - core[0]) < 0.01) \
                or (alley and e[0] == e[2] and abs(e[0] - alley[2]) < 0.01)
            if not on_edge and rng.random() < 0.6:
                L.line("wall" if rng.random() < 0.5 else "fence", *e)
    if alley:
        L.cell("alley", alley[:4], "alley")
    return L, yards, cores


def open_layout(block, rng, ch, strip=False):
    """A block with no building beneath its paint (Decision 8): the sidewalk verge, then the ground
    cut into a few open uses, the largest a park. Nothing stands up from it but fences, hedges,
    trees, benches, cans and parked cars: no wall, no shed, since no collider stands there."""
    A0, B0, A1, B1 = block
    W = SIDEWALK_M
    L = Layout()
    L.cell("sidewalk", (A0, B0, A1, B0 + W), "verge")
    L.cell("sidewalk", (A0, B1 - W, A1, B1), "verge")
    if not strip:
        L.cell("sidewalk", (A0, B0 + W, A0 + W, B1 - W), "verge")
        L.cell("sidewalk", (A1 - W, B0 + W, A1, B1 - W), "verge")
    rects = []

    def cut(r):
        a0, b0, a1, b1 = r
        w, h = a1 - a0, b1 - b0
        if max(w, h) <= rng.uniform(*ch["parcel"]) or max(w, h) < 60.0:
            rects.append(r)
            return
        t = rng.uniform(0.35, 0.65)
        if w >= h:
            cut((a0, b0, a0 + t * w, b1))
            cut((a0 + t * w, b0, a1, b1))
        else:
            cut((a0, b0, a1, b0 + t * h))
            cut((a0, b0 + t * h, a1, b1))

    cut((A0 if strip else A0 + W, B0 + W, A1 if strip else A1 - W, B1 - W))
    # The largest use is a park and the next a parking lot, so every block keeps green and paving.
    order = sorted(range(len(rects)), key=lambda i: -(rects[i][2] - rects[i][0]) * (rects[i][3] - rects[i][1]))
    for i, r in enumerate(rects):
        use = "park" if i == order[0] else "parking" if len(order) > 1 and i == order[1] else _pick(rng, ch["uses"])
        a0, b0, a1, b1 = r
        ca, cb = (a0 + a1) / 2, (b0 + b1) / 2
        if use == "park":
            # A lawn crossed by two paved paths meeting at a paved round with a flower bed, fenced or not.
            L.cell("lawn", r, "core")
            L.cell("flagstone", (ca - 1.5, b0, ca + 1.5, b1), "core")
            L.cell("flagstone", (a0, cb - 1.5, a1, cb + 1.5), "core")
            s = rng.uniform(5.0, 8.0)
            L.cell("flagstone", (ca - s, cb - s, ca + s, cb + s), "core")
            L.cell("bed", (ca - 2.0, cb - 2.0, ca + 2.0, cb + 2.0), "bed")
            if rng.random() < 0.5:
                for e in ((a0, b0, a1, b0), (a0, b1, a1, b1), (a0, b0, a0, b1), (a1, b0, a1, b1)):
                    L.line("fence", *e)
        elif use == "plaza":
            # Paving round a planted square.
            L.cell("flagstone", r, "core")
            s = 0.2 * min(a1 - a0, b1 - b0)
            L.cell("lawn", (ca - s, cb - s, ca + s, cb + s), "core")
            L.cell("bed", (ca - 2.0, cb - 2.0, ca + 2.0, cb + 2.0), "bed")
        elif use == "parking":
            c = L.cell("asphalt", r, "core")
            if c:
                c["stalls"] = True
            if a1 - a0 >= b1 - b0:
                L.cell("concrete", (a0, b0, a1, b0 + 3.0) if rng.random() < 0.5 else (a0, b1 - 3.0, a1, b1), "core")
            else:
                L.cell("concrete", (a0, b0, a0 + 3.0, b1) if rng.random() < 0.5 else (a1 - 3.0, b0, a1, b1), "core")
        else:
            # A vacant lot: gravel or dirt behind a fence, a worn path across it.
            L.cell("dirt" if rng.random() < 0.5 else "gravel", r, "core")
            for e in ((a0, b0, a1, b0), (a0, b1, a1, b1), (a0, b0, a0, b1), (a1, b0, a1, b1)):
                L.line("fence", *e)
            L.paths.append([(a0, b0), (ca + rng.uniform(-5, 5), cb + rng.uniform(-5, 5)), (a1, b1)])
    return L


def _gardens(L, yards, cores, free_px, rng, ch):
    """Turns yards and core cells to lawn or garden, in a seeded order, until the character's share
    of the free floor is green; a garden gets beds along two edges and a path, a lawn a worn path."""
    total = sum(free_px(c["rect"]) for c in L.cells)
    green = 0.0
    pool = yards + cores
    for j in rng.permutation(len(pool)):
        c = pool[j]
        f = free_px(c["rect"])
        if green >= ch["green"] * total:
            break
        if (green + f) > (ch["green"] + 0.03) * total or f < 40:
            continue
        a0, b0, a1, b1 = c["rect"]
        allot = min(a1 - a0, b1 - b0) >= 12.0 and c["region"] == "core"
        c["kind"] = _pick(rng, [(k, w if allot or k != "allotment" else 0.0) for k, w in ch["greens"]])
        green += f
    for c in pool:
        a0, b0, a1, b1 = c["rect"]
        if c["kind"] == "allotment":
            # A community garden: beds 1.5 m wide on a 3 m pitch along the cell's long side.
            along_a = a1 - a0 >= b1 - b0
            lo, hi = (b0, b1) if along_a else (a0, a1)
            for t in np.arange(lo + 1.0, hi - 2.0, 3.0):
                r = (a0 + 1.0, t, a1 - 1.0, t + 1.5) if along_a else (t, b0 + 1.0, t + 1.5, b1 - 1.0)
                L.cell("bed", r, "bed", c["lot"])
        elif c["kind"] == "garden":
            bed = 1.5
            if a1 - a0 >= b1 - b0:
                L.cell("bed", (a0 + 0.5, b0 + 0.5, a1 - 0.5, b0 + 0.5 + bed), "bed", c["lot"])
                L.cell("bed", (a0 + 0.5, b1 - 0.5 - bed, a1 - 0.5, b1 - 0.5), "bed", c["lot"])
                m = (b0 + b1) / 2
                L.paths.append([(a0, m), (a1, m)])
            else:
                L.cell("bed", (a0 + 0.5, b0 + 0.5, a0 + 0.5 + bed, b1 - 0.5), "bed", c["lot"])
                L.cell("bed", (a1 - 0.5 - bed, b0 + 0.5, a1 - 0.5, b1 - 0.5), "bed", c["lot"])
                m = (a0 + a1) / 2
                L.paths.append([(m, b0), (m, b1)])
            if min(a1 - a0, b1 - b0) > 18.0:
                ca, cb = (a0 + a1) / 2, (b0 + b1) / 2
                L.cell("bed", (ca - 2.5, cb - 2.5, ca + 2.5, cb + 2.5), "bed", c["lot"])
        elif c["kind"] in ("lawn", "dirt", "gravel") and rng.random() < 0.6:
            # A worn path: from a building's back door to the rear line, else across the cell.
            if c.get("door"):
                s, (al0, al1), (d0, d1) = c["side"], c["along"], c["depth"]
                x = c["door"][0]
                pts = [(x, d0), (x + rng.uniform(-1, 1), d0 + (d1 - d0) * 0.5), (x + rng.uniform(-2, 2), d1)]
                pts = [_side_point(s, L.block, p) for p in pts]
            else:
                pts = [(a0, b0), ((a0 + a1) / 2 + rng.uniform(-3, 3), (b0 + b1) / 2 + rng.uniform(-3, 3)), (a1, b1)]
                if rng.random() < 0.5:
                    pts = [(a0, b1), pts[1], (a1, b0)]
            L.paths.append(pts)


def _side_point(side, block, p):
    r = _side_rect(side, block, p[0], p[0], p[1], p[1])
    return r[0], r[1]


# ---------------------------------------------------------------- painting

def _pattern(kind, h, w, rng):
    """A surface's own shading over an h x w cell, around 1."""
    y = np.arange(h)[:, None]
    x = np.arange(w)[None, :]
    n = rng.standard_normal((h + 8, w + 8))
    fine = n[4:-4, 4:-4]
    if kind in ("concrete", "sidewalk"):
        step = 6 if kind == "concrete" else 4
        jit = rng.uniform(-0.04, 0.04, (h // step + 1, w // step + 1))
        s = 1 + jit[y // step, x // step] + 0.015 * fine
        return np.where((y % step == 0) | (x % step == 0), s * 0.94, s)
    if kind == "flagstone":
        row = y // 3
        sx = x + (row % 2) * 2
        jit = rng.uniform(-0.07, 0.07, (h // 3 + 1, w // 4 + 2))
        s = 1 + jit[row, sx // 4] + 0.015 * fine
        return np.where((y % 3 == 0) | (sx % 4 == 0), s * 0.92, s)
    if kind in ("asphalt", "alley"):
        s = 1 + 0.05 * _smooth(n, 3)[4:-4, 4:-4] * 3 + 0.015 * fine
        for _ in range(int(rng.integers(0, 1 + h * w // 600))):
            py, px_ = int(rng.integers(0, h)), int(rng.integers(0, w))
            s[py:py + int(rng.integers(3, 10)), px_:px_ + int(rng.integers(3, 10))] *= rng.uniform(0.94, 1.06)
        if kind == "alley":
            s[:, [0, -1]] *= 0.9
        return s
    if kind == "gravel":
        return 1 + 0.06 * fine + 0.03 * _smooth(n, 2)[4:-4, 4:-4] * 2.5
    if kind == "dirt":
        return 1 + 0.08 * _smooth(n, 3)[4:-4, 4:-4] * 3 + 0.03 * fine
    if kind in ("lawn", "garden", "allotment"):
        return 1 + 0.07 * _smooth(n, 2)[4:-4, 4:-4] * 2.5 + 0.03 * fine
    if kind == "bed":
        return 1 + 0.06 * fine + np.where(y % 2 == 0, 0.04, -0.04)
    raise KeyError(kind)


def _smooth(n, r):
    k = np.ones(2 * r + 1) / (2 * r + 1)
    n = np.apply_along_axis(lambda v: np.convolve(v, k, "same"), 0, n)
    return np.apply_along_axis(lambda v: np.convolve(v, k, "same"), 1, n)


def _unit(kind):
    col, level = KINDS[kind]
    col = np.array(col)
    y = A.luma(col)
    return (y + CHROMA * (col - y)) / y * level


def paint_floor(L, rng, shape=(SIZE, SIZE), pxm=PX):
    """The layout frame's raster (rows down b, columns along a, pxm texels a metre) in levels relative
    to the floor's mean (scaled later). Its columns wrap, as a strip's ends meet."""
    h, w = shape
    img = np.ones((h, w, 3)) * _unit("sidewalk")
    for c in L.cells:
        a0, b0, a1, b1 = c["rect"]
        x0, x1, y0, y1 = px(a0, pxm), px(a1, pxm), px(b0, pxm), px(b1, pxm)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, w), min(y1, h)
        if x1 <= x0 or y1 <= y0:
            continue
        img[y0:y1, x0:x1] = _unit(c["kind"]) * _pattern(c["kind"], y1 - y0, x1 - x0, rng)[..., None]
    for c in L.cells:
        if c.get("stalls"):
            _stalls(img, c["rect"], pxm)
    for pts in L.paths:
        _stroke(img, pts, 1.0 if len(pts) == 2 else 0.6, PATH, pxm)
    for kind, a0, b0, a1, b1 in L.lines:
        x0, x1, y0, y1 = px(min(a0, a1), pxm), px(max(a0, a1), pxm), px(min(b0, b1), pxm), px(max(b0, b1), pxm)
        if y0 == y1:
            sl, sh = np.s_[y0 % h, x0:x1], np.s_[(y0 + 1) % h, x0:x1]
        else:
            sl, sh = np.s_[y0:y1, x0 % w], np.s_[y0:y1, (x0 + 1) % w]
        if kind == "fence":
            img[sl] *= FENCE
        else:
            img[sl] *= WALL
            img[sh] *= WALL_SHADOW
    # A faint wrapped grime over everything, so neighbouring cells of one kind differ.
    grime = A._blur(A._blur(rng.standard_normal((h, w)), 6), 6)
    grime = grime / max(grime.std(), 1e-9) * 0.035
    return img * (1 + grime)[..., None]


def _stalls(img, rect, pxm=PX):
    """Painted parking stalls along both long sides of a lot: 5 m marks on a 2.6 m pitch. Paint only,
    never layout lines, which the dressing would stand fences on and keep its cars clear of."""
    a0, b0, a1, b1 = rect
    along_a = a1 - a0 >= b1 - b0
    lo, hi = (a0, a1) if along_a else (b0, b1)
    for t in np.arange(lo + 2.0, hi - 1.5, 2.6):
        for e0, e1 in (((b0, b0 + 5.0) if along_a else (a0, a0 + 5.0)), ((b1 - 5.0, b1) if along_a else (a1 - 5.0, a1))):
            if along_a:
                img[max(px(e0, pxm), 0):min(px(e1, pxm), img.shape[0]), px(t, pxm) % img.shape[1]] *= STALL
            else:
                img[px(t, pxm) % img.shape[0], max(px(e0, pxm), 0):min(px(e1, pxm), img.shape[1])] *= STALL


def _green_share(L, shift, free):
    """The share of the free floor (image frame) whose top cell is lawn, garden, allotment or bed."""
    green = np.zeros((SIZE, SIZE), bool)
    for c in L.cells:
        x0, x1, y0, y1 = (max(px(c["rect"][0]), 0), min(px(c["rect"][2]), SIZE), max(px(c["rect"][1]), 0),
                          min(px(c["rect"][3]), SIZE))
        if x1 > x0 and y1 > y0:
            green[y0:y1, x0:x1] = c["kind"] in ("lawn", "garden", "allotment", "bed")
    green = np.roll(green, (shift[1], shift[0]), axis=(0, 1))
    return float(green[free].mean())


def _stroke(img, pts, half_px, gain, pxm=PX):
    hit = np.zeros(img.shape[:2], bool)
    for (a0, b0), (a1, b1) in zip(pts, pts[1:]):
        n = int(max(abs(a1 - a0), abs(b1 - b0)) * pxm * 4) + 1
        for t in np.linspace(0, 1, n):
            x, y = (a0 + (a1 - a0) * t) * pxm, (b0 + (b1 - b0) * t) * pxm
            hit[max(int(y - half_px + 0.5), 0):int(y + half_px + 0.5), max(int(x - half_px + 0.5), 0):int(x + half_px + 0.5)] = True
    img[hit] *= gain


def write_png(path, rgb):
    """An 8-bit RGB PNG, row 0 at the top; no time or text chunk, so a rerun writes the same bytes
    (veg_blender.write_png's format, which imports bpy)."""
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    head = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", head) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def _dump(o, ind=0):
    p = "  " * ind
    if isinstance(o, dict):
        return "{\n" + ",\n".join("%s  %s: %s" % (p, json.dumps(k), _dump(v, ind + 1)) for k, v in o.items()) + "\n" + p + "}"
    if isinstance(o, list) and o and isinstance(o[0], dict):
        return "[\n" + ",\n".join(p + "  " + json.dumps(v, separators=(", ", ": ")) for v in o) + "\n" + p + "]"
    return json.dumps(o, separators=(", ", ": "))


def write_layout(path, paint, bset, n, L, uv, blocked, pxm=PX, frame=None):
    """The layout a variant was painted from, for the dressing (B14). UV as in ground_layout.json:
    u along the image's columns, v down its rows, one unit per tile; a rect is [u0, v0, u1, v1] and
    can run past 1, so draw it again shifted by -1 in u and v. cells are in paint order, a later one
    over an earlier one, and can run under a lot; lines and paths are cut where they cross a lot or
    a street. uv maps the layout's metres to the JSON's units; blocked is the layout frame's raster of
    lots and streets at pxm texels a metre, its columns wrapping where a frame is given (a strip).
    A frame (the diagonal districts) puts the layout in the strip's own frame: see conventions.frame."""
    h, w = blocked.shape

    def runs(pts):
        """A polyline sampled every half metre and cut into its runs over free floor."""
        out, cur = [], []
        for (a0, b0), (a1, b1) in zip(pts, pts[1:]):
            n_ = int(max(abs(a1 - a0), abs(b1 - b0)) * 2) + 1
            for t in np.linspace(0, 1, n_):
                a, b = a0 + (a1 - a0) * t, b0 + (b1 - b0) * t
                col = int(a * pxm) % w if frame else min(int(a * pxm), w - 1)
                if blocked[min(int(b * pxm), h - 1), col]:
                    if len(cur) > 2:
                        out.append(cur)
                    cur = []
                else:
                    cur.append((a, b))
        if len(cur) > 2:
            out.append(cur)
        return out

    cells = []
    for c in L.cells:
        e = dict(kind=c["kind"], region=c["region"], rect=uv(*c["rect"][:2]) + uv(*c["rect"][2:]))
        if c["lot"] is not None:
            e["lot"] = c["lot"]
        cells.append(e)
    lines = []
    for kind, a0, b0, a1, b1 in L.lines:
        for r in runs([(a0, b0), (a1, b1)]):
            lines.append(dict(kind=kind, line=uv(*r[0]) + uv(*r[-1])))
    paths = []
    for p in L.paths:
        for r in runs(p):
            keep = r[::8] + ([r[-1]] if (len(r) - 1) % 8 else [])
            paths.append(dict(kind="path", line=[x for q in keep for x in uv(*q)]))
    doc = dict(about="C5 block floor layout, %s paint with the %s set, variant %d, written by "
               "CSVM/data/city_models/c5_ground.py (PLAN-c5-ground B12) from ground_layout.json; "
               "regenerate with that script, never edit by hand." % (paint, bset, n),
               conventions=dict(
                   uv="as ground_layout.json: u along the image's columns, v down its rows, one unit per 256 m tile; "
                      "a rect [u0, v0, u1, v1] or line can run past 1, so draw each again shifted by -1 in u and v",
                   cells="the surfaces in paint order, a later one over an earlier one; region verge (sidewalk "
                         "between kerb and lot), apron (paving behind a building), yard (a ring lot behind its "
                         "building, or a vacant one), core (behind the ring), alley, bed (planting inside a "
                         "garden); lot is the ground_layout lot index the cell belongs to; a cell can run under "
                         "a lot, so test a stamp against the lots",
                   lines="fence or wall (a courtyard or rear wall), cut where it crosses a lot or a street",
                   paths="worn or paved footpaths as polylines, cut the same way"),
               cells=cells, lines=lines, paths=paths)
    if frame:
        doc["conventions"]["frame"] = (
            "the layout lies in the strip's own frame, x along the street, y across it from one centreline to the "
            "next, one unit per 256 m; a point (x, y) is uv = origin + x * x_axis + y * y_axis, a turn, and the "
            "ground repeats with the tile, so take uv modulo 1")
        doc["frame"] = frame
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(_dump(doc) + "\n")


def generate(layout_doc, paint, bset, n, luma, ch):
    """One variant: the image (uint8, image frame), its layout, the free-floor mask, the roll."""
    pdoc = layout_doc["paints"][paint]
    streets = pdoc["streets"]
    fam = {s["family"]: s for s in streets}
    assert set(fam) == {"u", "v"}, "only the square districts (Wave C adds the diagonal ones)"
    lots = layout_doc["templates"].get(bset, {"lots": []})["lots"]  # none: flagged ground with no buildings
    shift = (int(round(fam["u"]["centre"] * SIZE)), int(round(fam["v"]["centre"] * SIZE)))
    rng = np.random.default_rng([zlib.crc32(("%s-%s" % (paint, bset)).encode()), n])

    def block_edge(st, s):
        c = st["centre"] - s / SIZE
        c -= round(c)
        return (c + st["half"]) * TILE_M, (c + 1 - st["half"]) * TILE_M

    (A0, A1), (B0, B1) = block_edge(fam["u"], shift[0]), block_edge(fam["v"], shift[1])
    blocked = lot_mask(lots, shift) | street_mask(streets, shift)
    boxes = [_bbox(lot, shift) for lot in lots]
    if ch.get("open"):
        L, flip = open_layout((A0, B0, A1, B1), rng, ch), False
    else:
        flip = rng.random() < 0.5
        if flip:
            L, yards, cores = layout((B0, A0, B1, A1), [[r[1], r[0], r[3], r[2]] for r in boxes], rng, ch)
            L.block = (B0, A0, B1, A1)
        else:
            L, yards, cores = layout((A0, B0, A1, B1), boxes, rng, ch)
            L.block = (A0, B0, A1, B1)
        tb = blocked.T if flip else blocked

        def free_px(r):
            x0, x1, y0, y1 = max(px(r[0]), 0), min(px(r[2]), SIZE), max(px(r[1]), 0), min(px(r[3]), SIZE)
            return float((~tb[y0:y1, x0:x1]).sum()) if x1 > x0 and y1 > y0 else 0.0

        _gardens(L, yards, cores, free_px, rng, ch)
    if flip:
        L.flip()
    rel = paint_floor(L, rng)
    free = ~blocked
    k = luma / A.luma(rel)[free].mean()
    img = np.clip(np.round(rel * k), 0, 255).astype(np.uint8)
    img = np.roll(img, (shift[1], shift[0]), axis=(0, 1))
    free_img = np.roll(free, (shift[1], shift[0]), axis=(0, 1))
    # The roll and the analytic masks agree with the masks drawn in the image frame directly.
    direct = ~(lot_mask(lots, (0, 0)) | street_mask(streets, (0, 0)))
    assert (direct == free_img).mean() > 0.999, "rolled frame disagrees with the image frame"

    def uv(a, b):
        return [round((a * PX + shift[0]) / SIZE, 5), round((b * PX + shift[1]) / SIZE, 5)]

    return dict(img=img, L=L, uv=uv, blocked=blocked, free=free_img, green=_green_share(L, shift, free_img))


def generate_strip(layout_doc, paint, bset, n, luma, ch):
    """One variant of a diagonal paint (Decision 7): the layout is made in the strip's own frame, a
    across the strip's 362 m period along its street, b across from one centreline to the next, and
    the image samples it. A strip of the tile is the strip next to it moved a tile along u, so one
    period of one strip is the whole tile's ground. The raster holds 512 texels across the 181 m
    strip, so every image texel centre lands on a raster corner and takes the mean of the four
    texels round it."""
    st = layout_doc["paints"][paint]["streets"]
    assert len(st) == 1 and st[0]["family"] in FAMILY_AXES, "one diagonal street family per paint (A2)"
    st = st[0]
    alpha, beta = FAMILY_AXES[st["family"]]
    centre, half = st["centre"], st["half"]
    lots = layout_doc["templates"].get(bset, {"lots": []})["lots"]  # none: flagged ground with no buildings
    rng = np.random.default_rng([zlib.crc32(("%s-%s" % (paint, bset)).encode()), n])
    wm = TILE_M / np.sqrt(2.0)  # metres per unit of c or d: the strip's width
    lm = 2 * wm                 # the strip's period along its street
    pxm = SIZE / wm
    h, w = SIZE, 2 * SIZE
    sigma = beta[0]             # how far d moves when a tile step along u moves c by one

    def strip_m(u, v, k):
        """(along, across) metres of a uv point, in strip k carried back to strip 0."""
        c = alpha[0] * u + alpha[1] * v
        d = beta[0] * u + beta[1] * v
        return (d - sigma * k) * wm, (c - centre - k) * wm

    # Each lot in strip metres, its strip from its origin, then moved a period along so its middle
    # lies in [0, lm); the frame then starts in the widest gap between lots, so none crosses its ends.
    polys = []
    for lot in lots:
        k = int(np.floor(alpha[0] * lot["o"][0] + alpha[1] * lot["o"][1] - centre))
        ps = [[strip_m(p[i], p[i + 1], k) for i in range(0, len(p), 2)] for p in lot["poly"]]
        mid = np.mean([a for p in ps for a, _ in p])
        shift_a = -np.floor(mid / lm) * lm
        polys.append([[(a + shift_a, b) for a, b in p] for p in ps])
    spans = sorted((min(a for p in ps for a, _ in p), max(a for p in ps for a, _ in p)) for ps in polys)
    gaps = [((spans[i][1], spans[(i + 1) % len(spans)][0] + (lm if i + 1 == len(spans) else 0.0))) for i in range(len(spans))]
    g0, g1 = max(gaps, key=lambda g: g[1] - g[0]) if gaps else (0.0, 0.0)
    t0px = int(round((g0 + g1) / 2 * pxm)) % w
    t0 = t0px / pxm
    boxes, blocked = [], np.zeros((h, w), bool)
    for ps in polys:
        mid = np.mean([a for p in ps for a, _ in p])
        dt = -t0 - np.floor((mid - t0) / lm) * lm
        ps = [[(a + dt, b) for a, b in p] for p in ps]
        aa, bb = [a for p in ps for a, _ in p], [b for p in ps for _, b in p]
        boxes.append([min(aa), min(bb), max(aa), max(bb)])
        for p in ps:
            for j in (-1, 0, 1):
                _fill_convex(blocked, [(a * pxm + j * w, b * pxm) for a, b in p])
    rows = (np.arange(h) + 0.5) / pxm
    blocked |= ((rows < half * wm) | (rows > wm - half * wm))[:, None]
    block = (0.0, half * wm, lm, wm - half * wm)
    if ch.get("open"):
        L = open_layout(block, rng, ch, strip=True)
    else:
        L, yards, cores = layout(block, boxes, rng, ch, strip=True)
    L.block = block

    def free_px(r):
        x0, x1, y0, y1 = max(px(r[0], pxm), 0), min(px(r[2], pxm), w), max(px(r[1], pxm), 0), min(px(r[3], pxm), h)
        return float((~blocked[y0:y1, x0:x1]).sum()) if x1 > x0 and y1 > y0 else 0.0

    if not ch.get("open"):
        _gardens(L, yards, cores, free_px, rng, ch)
    rel = paint_floor(L, rng, (h, w), pxm)

    # Every image texel centre's raster corner: c and d times 512 are whole there.
    j, i = np.meshgrid(np.arange(SIZE) + 0.5, np.arange(SIZE) + 0.5)
    c512 = np.round(alpha[0] * j + alpha[1] * i).astype(np.int64) - int(round(centre * SIZE))
    d512 = np.round(beta[0] * j + beta[1] * i).astype(np.int64)
    k = np.floor_divide(c512, SIZE)
    row = c512 - k * SIZE
    col = (d512 - sigma * k * SIZE - t0px) % w
    r0, c0 = (row - 1) % h, (col - 1) % w
    img = (rel[r0, c0] + rel[r0, col] + rel[row % h, c0] + rel[row % h, col]) / 4
    free = ~(lot_mask(lots, (0, 0)) | street_mask([st], (0, 0)))
    assert (free == ~blocked[row % h, col]).mean() > 0.98, "strip frame disagrees with the image frame"
    kk = luma / A.luma(img)[free].mean()
    img = np.clip(np.round(img * kk), 0, 255).astype(np.uint8)

    green = np.zeros((h, w), bool)
    for c in L.cells:
        x0, x1, y0, y1 = max(px(c["rect"][0], pxm), 0), min(px(c["rect"][2], pxm), w), max(px(c["rect"][1], pxm), 0), min(px(c["rect"][3], pxm), h)
        if x1 > x0 and y1 > y0:
            green[y0:y1, x0:x1] = c["kind"] in ("lawn", "garden", "allotment", "bed")

    # The frame: uv of the layout's origin (strip 0, the frame's start) and of its unit steps.
    inv = np.linalg.inv(np.array([alpha, beta], float))
    o = inv @ np.array([centre, t0 / wm])
    ax, ay = inv @ np.array([0.0, np.sqrt(2.0)]), inv @ np.array([np.sqrt(2.0), 0.0])
    frame = dict(origin=[round(float(x), 6) for x in o], x_axis=[round(float(x), 6) for x in ax],
                 y_axis=[round(float(x), 6) for x in ay])

    def uv(a, b):
        return [round(a / TILE_M, 5), round(b / TILE_M, 5)]

    return dict(img=img, L=L, uv=uv, blocked=blocked, free=free, green=float(green[~blocked].mean()),
                pxm=pxm, frame=frame)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--luma", type=float, default=LUMA, help="the free floor's mean texture luma")
    ap.add_argument("--out", default=OUT, help="output folder (the level-30 comparison set goes outside the repo)")
    args = ap.parse_args()
    with open(LAYOUT, encoding="utf-8") as f:
        doc = json.load(f)
    os.makedirs(args.out, exist_ok=True)
    index = {}
    for paint_name, bset, count, ch in COMBOS:
        files = []
        for n in range(count):
            diagonal = any(s["family"] in FAMILY_AXES for s in doc["paints"][paint_name]["streets"])
            g = (generate_strip if diagonal else generate)(doc, paint_name, bset, n, args.luma, ch)
            name = "%s-%s-%d" % (paint_name, bset, n)
            write_png(os.path.join(args.out, name + ".png.bin"), g["img"])
            write_layout(os.path.join(args.out, name + ".json"), paint_name, bset, n, g["L"], g["uv"], g["blocked"],
                         g.get("pxm", PX), g.get("frame"))
            files.append(name + ".png.bin")
            y = A.luma(g["img"].astype(float))[g["free"]]
            print("%s  free floor luma mean %.2f median %.1f p5 %.1f p95 %.1f  green %.3f  cells %d  lines %d"
                  % (name, y.mean(), np.median(y), np.percentile(y, 5), np.percentile(y, 95), g["green"], len(g["L"].cells),
                     len(g["L"].lines)))
        index["%s-%s" % (paint_name, bset)] = dict(paint=paint_name, set=bset, variants=count, files=files)
    idoc = dict(about="C5 block floor variants per combination of visible paint and standing set, written by "
                "CSVM/data/city_models/c5_ground.py (PLAN-c5-ground B12). Each file is a 512 x 512 sRGB PNG "
                "covering one 256 m tile in the template's UV, +U right, +V down; street and lot texels are "
                "don't-care. Beside each <name>.png.bin is <name>.json, its layout.",
                luma=args.luma, combinations=index)
    with open(os.path.join(args.out, "index.json"), "w", encoding="utf-8", newline="\n") as f:
        f.write(_dump(idoc) + "\n")


if __name__ == "__main__":
    main()
