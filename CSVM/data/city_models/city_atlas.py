"""The city facade atlas and its window ids texture, painted with numpy. No bpy, so the preview
and check scripts under analysis/city-hulls import the layout from here.

Nothing here reads an original texture: the families are drawn after what the original facades
show (grey stone walk-ups, brick tenements, warm and dark office towers, the walk-ups again under
tile roofs), and each family is normalised to one mean brightness so a hull's grade (its
material's baseColorFactor) holds for every row the shader picks.

Layout (ATLAS x ATLAS, image row 0 at the top, as glTF and Godot read a texture):
- A grid of CELL x CELL cells. Atlas row f is facade family f; columns 0 to VARIANTS - 1 are its
  facade rows (variants), column ROOF_COL is its roof and METAL_COL its light metal (posts, rails,
  slab edges), which the shader takes ungraded.
- A cell is a TILE x TILE tile with a GUTTER of wrapped copies on every side, so bilinear
  filtering and the first mip levels never read a neighbour.
- A tile is TILE_M x TILE_M metres: FLOORS floors of FLOOR_M, its bays across. Within
  FLOOR_M of every floor line no tile has glass below SOLID_M or above FLOOR_M - SOLID_M, so a
  wall top that falls between floor lines can be mapped onto that solid band.
- Facade tile coordinates (TEXCOORD_0) map into a cell as
  uv = (col * CELL + GUTTER + fract(u) * TILE, row * CELL + GUTTER + (1 - fract(v)) * TILE) / ATLAS.

The ids texture has the same layout: R the window id within the tile (1..N, 0 is wall), G the
floor index within the tile (0 the lower floor), B the glass weight (0 none, BLIND where a blind
covers the glass, 255 clear glass), A 255. Roof and metal cells and free cells are all zero.
"""

import copy
import math

import numpy as np

ATLAS = 1024
TILE = 128
GUTTER = 16
CELL = TILE + 2 * GUTTER
VARIANTS = 4
ROOF_COL = VARIANTS
METAL_COL = VARIANTS + 1
TILE_M = 8.0
FLOOR_M = 4.0
FLOORS = int(TILE_M / FLOOR_M)
PX_M = TILE / TILE_M
SOLID_M = 0.8
BLIND = 140

FAMILY_NAMES = ("grey stone walk-up", "brick tenement", "warm office tower", "dark steel office tower",
                "grey stone walk-up, tile roof", "brick tenement, tile roof")

# Per family: the mean Rec. 601 luma (0..255, over sRGB texels) every tile of the family is scaled
# to, and the roof's. Bright enough that every hull's grade down to its original stays at most 1.
# The tile roofs stand brighter than the tar ones, as the original's roof02 (L56) does beside roof01 (L38).
WALL_LUMA = (118.0, 104.0, 124.0, 100.0, 118.0, 104.0)
ROOF_LUMA = (82.0, 76.0, 86.0, 78.0, 100.0, 100.0)

# One facade row per entry. Colours are sRGB 0..1. bays: windows groups per tile width; pair: two
# windows per bay; win: (width, height, sill) in metres, the sill above the floor line. Every row's
# bay count is even, so each family's rows share a pier every 4 m (grid_m) and a wall fitted to that
# grid ends between windows whichever row a building takes; stream_bays is the count a row was
# first painted with (_paint_wall).
FAMILIES = (
    (  # 0, grey stone walk-up
        dict(wall=(0.52, 0.52, 0.50), finish="ashlar", bays=4, stream_bays=3, win=(1.1, 2.0, 1.0), frame=(0.80, 0.80, 0.76),
             muntins="2x2", sill=(0.66, 0.65, 0.60), lintel=0.22),
        dict(wall=(0.60, 0.56, 0.47), finish="ashlar", bays=4, win=(1.0, 2.1, 0.9), frame=(0.82, 0.78, 0.66),
             muntins="sash", sill=(0.70, 0.67, 0.58), lintel=0.25, keystone=True, course=(0.68, 0.64, 0.55)),
        dict(wall=(0.48, 0.49, 0.42), finish="render", bays=2, pair=True, win=(0.9, 2.0, 1.0), frame=(0.30, 0.33, 0.30),
             muntins="sash", sill=(0.60, 0.60, 0.54)),
        dict(wall=(0.42, 0.42, 0.43), finish="ashlar", bays=4, stream_bays=3, win=(1.2, 2.0, 1.0), frame=(0.78, 0.78, 0.76),
             muntins="6over6", sill=(0.58, 0.58, 0.56), lintel=0.3, course=(0.55, 0.55, 0.54)),
    ),
    (  # 1, brick tenement
        dict(wall=(0.55, 0.30, 0.22), finish="brick", bays=4, stream_bays=3, win=(1.1, 2.0, 1.0), frame=(0.80, 0.78, 0.72),
             muntins="sash", sill=(0.70, 0.66, 0.58), lintel=0.25),
        dict(wall=(0.42, 0.27, 0.20), finish="brick", bays=4, win=(1.0, 2.0, 1.0), frame=(0.25, 0.22, 0.20),
             muntins="sash", sill=(0.55, 0.40, 0.32), arch="segment"),
        dict(wall=(0.62, 0.36, 0.25), finish="brick", bays=2, pair=True, win=(0.9, 2.0, 1.0), frame=(0.82, 0.78, 0.68),
             muntins="sash", sill=(0.72, 0.68, 0.60), sillcourse=True),
        dict(wall=(0.45, 0.30, 0.28), finish="brick", bays=4, stream_bays=3, win=(1.1, 1.7, 0.9), frame=(0.80, 0.76, 0.66),
             muntins="2x2", sill=(0.62, 0.55, 0.48), arch="round"),
    ),
    (  # 2, warm office tower
        dict(wall=(0.66, 0.58, 0.46), finish="cladding", bays=4, stream_bays=5, win=(1.0, 2.2, 0.85), frame=(0.40, 0.34, 0.27),
             muntins="sash", spandrel=(0.45, 0.38, 0.30)),
        dict(wall=(0.62, 0.48, 0.35), finish="brick", bays=4, pair=True, win=(0.75, 2.1, 0.9), frame=(0.72, 0.66, 0.55),
             muntins="sash", sill=(0.74, 0.68, 0.56), pier=(0.70, 0.60, 0.46)),
        dict(wall=(0.72, 0.66, 0.55), finish="cladding", bays=6, win=(0.85, 2.3, 0.85), frame=(0.50, 0.44, 0.36),
             muntins="deco", pier=(0.80, 0.74, 0.62), spandrel=(0.50, 0.42, 0.34), chevron=True),
        dict(wall=(0.60, 0.55, 0.48), finish="cladding", bays=4, win=(1.4, 2.1, 0.9), frame=(0.70, 0.66, 0.58),
             muntins="tri", sill=(0.68, 0.63, 0.55), course=(0.68, 0.62, 0.53)),
    ),
    (  # 3, dark steel office tower
        dict(wall=(0.36, 0.38, 0.42), finish="cladding", bays=4, stream_bays=5, win=(1.1, 2.2, 0.85), frame=(0.55, 0.57, 0.60),
             muntins="sash", spandrel=(0.25, 0.26, 0.29)),
        dict(wall=(0.34, 0.35, 0.30), finish="render", bays=4, pair=True, win=(0.8, 2.2, 0.85), frame=(0.28, 0.24, 0.20),
             muntins="sash", pier=(0.40, 0.41, 0.36)),
        dict(wall=(0.45, 0.47, 0.50), finish="cladding", bays=6, win=(0.8, 2.3, 0.85), frame=(0.36, 0.38, 0.42),
             muntins="deco", pier=(0.60, 0.62, 0.66), spandrel=(0.32, 0.34, 0.37), ribs=True),
        dict(wall=(0.33, 0.30, 0.30), finish="brick", bays=4, stream_bays=5, win=(1.1, 2.0, 1.0), frame=(0.62, 0.62, 0.60),
             muntins="2x2", sill=(0.60, 0.58, 0.55)),
    ),
)
# 4 and 5, the walk-ups of cblock4 and cblock7 under a brown tile roof (the original's roof02): the
# stone and brick facade rows again (painted after the first four families, so those stay as they
# were), with a tile roof. TUNE: their walls differ more, so a street shows different houses: grey
# stone, buff limestone, brownstone and grey-green render; red, brown and buff brick and a painted wall.
TILE_WALLS = (((0.44, 0.46, 0.47), (0.52, 0.48, 0.39), (0.49, 0.43, 0.38), (0.43, 0.49, 0.45)),
              ((0.63, 0.32, 0.24), (0.50, 0.36, 0.32), (0.55, 0.42, 0.27), (0.47, 0.36, 0.35)))
FAMILIES = FAMILIES + tuple(tuple(dict(spec, wall=w) for spec, w in zip(FAMILIES[f], TILE_WALLS[f])) for f in (0, 1))
# Family f's wall mean is held to the mean family MEAN_OF[f]'s rows give on the same noise, so its
# grades stay where they were.
MEAN_OF = {4: 0, 5: 1}
ROOFS = ((0.30, 0.30, 0.31), (0.33, 0.29, 0.26), (0.36, 0.34, 0.31), (0.29, 0.30, 0.32), (0.42, 0.27, 0.19),
         (0.42, 0.27, 0.19))
TILE_ROOF = (False, False, False, False, True, True)
# The light metal, a step darker than the mean of the original's aluminium trim (census.py's 149,
# 149, 151): at that mean the posts read twice as bright as the original's in C5's night light.
METAL = np.array((110, 110, 113)) / 255.0
GLASS = np.array((0.075, 0.085, 0.105))
BLIND_COL = np.array((0.17, 0.16, 0.14))


def luma(rgb):
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def _blur(a, r):
    """A wrapped box blur, so noise stays seamless across the tile's edges."""
    for ax in (0, 1):
        a = sum(np.roll(a, k, axis=ax) for k in range(-r, r + 1)) / (2 * r + 1)
    return a


def _finish(spec, rng):
    """The wall's own texture over the whole tile, rows bottom up."""
    n = TILE
    base = np.ones((n, n, 3)) * np.array(spec["wall"])
    f = spec["finish"]
    shade = 1 + 0.05 * _blur(rng.standard_normal((n, n)), 3) * 3
    y = np.arange(n)[:, None]
    x = np.arange(n)[None, :]
    if f == "ashlar":
        # Blocks 1 m by 0.5 m in running bond; both divide the tile, so the bond is seamless.
        course, bw = int(0.5 * PX_M), int(PX_M)
        row = y // course
        sx = x + (row % 2) * (bw // 2)
        jit = rng.uniform(-0.05, 0.05, (n // course, n // bw))
        shade = shade + jit[row, (sx // bw) % (n // bw)]
        shade = np.where((y % course == 0) | (sx % bw == 0), shade * 0.86, shade)
    elif f == "brick":
        row = y // 2
        sx = x + (row % 2) * 2
        jit = rng.uniform(-0.09, 0.09, (n // 2, n // 4))
        shade = shade + jit[row, (sx // 4) % (n // 4)]
        shade = np.where(y % 2 == 0, shade * 0.93, shade)
    elif f == "cladding":
        streak = _blur(rng.standard_normal((1, n)), 1)[0] * 0.04
        shade = shade + streak[None, :]
        shade = np.where(np.arange(n)[:, None] % int(FLOOR_M * PX_M) == int(SOLID_M * PX_M / 2), shade * 0.9, shade)
    else:  # render: smooth, faint mottling only
        shade = 1 + 0.6 * (shade - 1)
    return base * shade[..., None]


def _wrap(a, b):
    """Slices covering a..b modulo TILE, so a box across a tile edge continues on the other side."""
    a, b = int(a), int(b)
    if b <= a:
        return []
    a0 = a % TILE
    if a0 + (b - a) <= TILE:
        return [slice(a0, a0 + b - a)]
    return [slice(a0, TILE), slice(0, a0 + b - a - TILE)]


def _box(img, x0, x1, y0, y1, col):
    for ys in _wrap(y0, y1):
        for xs in _wrap(x0, x1):
            img[ys, xs] = col


def _windows(spec):
    """Window rectangles (x0, x1) in px across the tile, left to right."""
    bays = spec["bays"]
    bw = TILE / bays
    ww = spec["win"][0] * PX_M
    out = []
    for b in range(bays):
        c = (b + 0.5) * bw
        if spec.get("pair"):
            gap = 0.3 * PX_M
            out += [(round(c - gap / 2 - ww), round(c - gap / 2)), (round(c + gap / 2), round(c + gap / 2 + ww))]
        else:
            out.append((round(c - ww / 2), round(c + ww / 2)))
    return out


def paint_tile(spec, rng):
    """One facade tile: albedo (TILE, TILE, 3) sRGB 0..1 and ids (TILE, TILE, 3) uint8, rows bottom up."""
    img = _finish(spec, rng)
    ids = np.zeros((TILE, TILE, 3), np.uint8)
    wins = _windows(spec)
    w_m, h_m, s_m = spec["win"]
    rise_m = {"round": w_m / 2, "segment": w_m * 0.175}.get(spec.get("arch"), 0.0)
    assert s_m >= SOLID_M and s_m + h_m + rise_m <= FLOOR_M - SOLID_M, spec
    fr = 2
    floor_px = int(FLOOR_M * PX_M)
    bw = TILE / spec["bays"]
    for fl in range(FLOORS):
        y0 = fl * floor_px + round(s_m * PX_M)
        y1 = y0 + round(h_m * PX_M)
        if spec.get("course"):
            _box(img, 0, TILE, fl * floor_px, fl * floor_px + 3, spec["course"])
        if spec.get("sillcourse"):
            _box(img, 0, TILE, y0 - 3, y0, spec["sill"])
        if spec.get("pier"):
            pw = round(0.3 * PX_M)
            for b in range(spec["bays"] + 1):
                cx = round(b * bw)
                _box(img, cx - pw // 2, cx + pw - pw // 2, fl * floor_px, (fl + 1) * floor_px, spec["pier"])
        for i, (x0, x1) in enumerate(wins):
            wid = 1 + fl * len(wins) + i
            if spec.get("spandrel"):
                # The panel from the head of the window below up to this sill, wrapping under floor 0.
                sp = y0 - round((FLOOR_M - h_m) * PX_M)
                _box(img, x0, x1, sp, y0, spec["spandrel"])
                if spec.get("chevron"):
                    for k in range(0, x1 - x0, 4):
                        _box(img, x0 + k, x0 + k + 2, sp + 10 + (k // 4) % 2 * 2, sp + 12 + (k // 4) % 2 * 2,
                             np.array(spec["spandrel"]) * 1.35)
                if spec.get("ribs"):
                    for yy in range(sp + 3, y0 - 1, 3):
                        _box(img, x0, x1, yy, yy + 1, np.array(spec["spandrel"]) * 1.4)
            if spec.get("lintel"):
                lh = round(spec["lintel"] * PX_M)
                _box(img, x0 - 2, x1 + 2, y1, y1 + lh, spec["sill"])
                if spec.get("keystone"):
                    c = (x0 + x1) // 2
                    _box(img, c - 2, c + 2, y1, y1 + lh + 2, np.array(spec["sill"]) * 1.08)
            arch_top = y1
            if spec.get("arch"):
                c, half = (x0 + x1) / 2, (x1 - x0) / 2
                rise = half if spec["arch"] == "round" else half * 0.35
                xs = np.arange(x0 - 2, x1 + 2)
                for x in xs:
                    t = min(1.0, abs(x + 0.5 - c) / (half + 2))
                    top = y1 + rise * np.sqrt(max(0.0, 1 - t * t))
                    _box(img, x, x + 1, y1, int(top) + 3, np.array(spec["sill"]) * 0.95)
                arch_top = y1 + int(rise)
            if spec.get("sill"):
                _box(img, x0 - 2, x1 + 2, y0 - 2, y0, spec["sill"])
            # The opening: frame, then glass with a blind at a height of its own.
            _box(img, x0, x1, y0, y1, spec["frame"])
            gx0, gx1, gy0, gy1 = x0 + fr, x1 - fr, y0 + fr, y1 - fr
            blind = rng.choice((0.0, 0.0, 0.25, 0.45, 0.7, 0.9))
            for y in range(gy0, gy1):
                t = (y - gy0) / max(1, gy1 - gy0 - 1)
                g = GLASS * (0.85 + 0.35 * t) + rng.uniform(-0.006, 0.006)
                covered = t > 1 - blind
                img[y, gx0:gx1] = BLIND_COL * (0.9 + 0.2 * t) if covered else g
                ids[y, gx0:gx1, 2] = BLIND if covered else 255
            ids[y0:y1, x0:x1, 0] = wid
            ids[y0:y1, x0:x1, 1] = fl
            if spec.get("arch"):
                # The arch's head is glass too, above the frame's rectangle.
                c, half = (x0 + x1) / 2, (x1 - x0) / 2
                rise = arch_top - y1
                for x in range(gx0, gx1):
                    t = abs(x + 0.5 - c) / half
                    top = y1 + int(rise * np.sqrt(max(0.0, 1 - t * t)))
                    _box(img, x, x + 1, y1 - fr, top, GLASS * 1.15)
                    ids[y1 - fr:top, x, 0] = wid
                    ids[y1 - fr:top, x, 1] = fl
                    ids[y1 - fr:top, x, 2] = 255
            _muntins(img, ids, spec["muntins"], gx0, gx1, gy0, gy1, spec["frame"])
    return np.clip(img, 0, 1), ids


def _muntins(img, ids, kind, x0, x1, y0, y1, col):
    """Bars across the glass, frame-coloured, glass weight 0 under them."""
    bars = []
    my = (y0 + y1) // 2
    mx = (x0 + x1) // 2
    if kind in ("sash", "2x2", "6over6"):
        bars.append((x0, x1, my - 1, my + 1))
    if kind in ("2x2", "deco"):
        bars.append((mx, mx + 1, y0, y1))
    if kind == "6over6":
        for k in (1, 2):
            x = x0 + (x1 - x0) * k // 3
            bars.append((x, x + 1, y0, y1))
    if kind == "tri":
        for k in (1, 2):
            x = x0 + (x1 - x0) * k // 3
            bars.append((x, x + 1, y0, y1))
        bars.append((x0, x1, y1 - (y1 - y0) // 4, y1 - (y1 - y0) // 4 + 1))
    for a, b, c, d in bars:
        img[c:d, a:b] = col
        ids[c:d, a:b, 2] = 0


def paint_roof(col, rng):
    """A tar roof: rolled strips a metre wide, patches, gravel speckle; seamless."""
    n = TILE
    shade = 1 + 0.12 * _blur(rng.standard_normal((n, n)), 4) * 4
    shade += rng.uniform(-0.06, 0.06, (n, n))
    x = np.arange(n)[None, :]
    shade = np.where(x % int(PX_M) == 0, shade * 0.8, shade)
    shade = np.broadcast_to(shade, (n, n))
    return np.clip(np.ones((n, n, 3)) * np.array(col) * shade[..., None], 0, 1)


def paint_tile_roof(col, rng):
    """A clay tile roof: courses 0.5 m high, tiles 0.5 m wide in half bond, each course lit at its
    head and shaded at its lap, a little colour spread per tile; seamless."""
    n = TILE
    course, tw = int(0.5 * PX_M), int(0.5 * PX_M)
    y = np.arange(n)[:, None]
    x = np.arange(n)[None, :]
    row = y // course
    sx = x + (row % 2) * (tw // 2)
    jit = rng.uniform(-0.10, 0.10, (n // course, n // tw))
    shade = 1 + jit[row, (sx // tw) % (n // tw)]
    shade = shade * (0.78 + 0.32 * (y % course) / (course - 1))
    shade = np.where(sx % tw == 0, shade * 0.75, shade)
    shade = shade + 0.06 * _blur(rng.standard_normal((n, n)), 4) * 4
    warm = rng.uniform(-0.04, 0.04, (n // course, n // tw))[row, (sx // tw) % (n // tw)]
    img = np.ones((n, n, 3)) * np.array(col) * shade[..., None]
    img[..., 0] += warm
    return np.clip(img, 0, 1)


def paint_metal(rng):
    """Light painted metal: faint vertical brushing and a few darker seams; seamless."""
    n = TILE
    streak = _blur(rng.standard_normal((1, n)), 1)[0] * 0.03
    shade = 1 + streak[None, :] + 0.02 * _blur(rng.standard_normal((n, n)), 2) * 2
    shade = np.where(np.arange(n)[None, :] % int(2 * PX_M) == 0, shade * 0.85, shade)
    shade = np.broadcast_to(shade, (n, n))
    return np.clip(np.ones((n, n, 3)) * METAL * shade[..., None], 0, 1)


def _mean(tiles):
    """The mean sRGB 0..1 colour of equal-sized tiles."""
    return np.mean([t.reshape(-1, 3).mean(0) for t in tiles], axis=0)


def _scale_to(img, target):
    """Scales a tile so its mean luma over sRGB 0..255 texels is target."""
    m = luma(img * 255).mean()
    return np.clip(img * (target / m), 0, 1)


def _paint_wall(spec, rng):
    """paint_tile, with the stream advanced as a row re-spaced to a common bay grid was first drawn
    (its stream_bays), so every cell painted after it keeps its noise."""
    if "stream_bays" not in spec:
        return paint_tile(spec, rng)
    out = paint_tile(spec, copy.deepcopy(rng))
    paint_tile(dict(spec, bays=spec["stream_bays"]), rng)
    return out


def paint(seed=11):
    """The atlas (ATLAS, ATLAS, 3) uint8 sRGB, the ids (ATLAS, ATLAS, 4) uint8, image row 0 on top,
    and per family the mean sRGB 0..255 of its wall tiles and of its roof."""
    rng = np.random.default_rng(seed)
    atlas = np.full((ATLAS, ATLAS, 3), 0.25)
    ids = np.zeros((ATLAS, ATLAS, 4), np.uint8)
    ids[..., 3] = 255
    means = []
    for f, rows in enumerate(FAMILIES):
        held = copy.deepcopy(rng)
        walls = []
        for c, spec in enumerate(rows):
            img, idt = _paint_wall(spec, rng)
            walls.append(_scale_to(img, WALL_LUMA[f]))
            _place(ids, idt, f, c)
        if f in MEAN_OF:
            ref = [_scale_to(_paint_wall(spec, held)[0], WALL_LUMA[f]) for spec in FAMILIES[MEAN_OF[f]]]
            gain = _mean(ref) / _mean(walls)
            walls = [w * gain for w in walls]
            assert max(w.max() for w in walls) <= 1, "a held wall clips: lower its colour"
        for c, img in enumerate(walls):
            _place(atlas, img, f, c)
        roof = _scale_to((paint_tile_roof if TILE_ROOF[f] else paint_roof)(ROOFS[f], rng), ROOF_LUMA[f])
        _place(atlas, roof, f, ROOF_COL)
        means.append((_mean(walls) * 255, roof.reshape(-1, 3).mean(0) * 255))
    # A stream of its own, so the cells above paint as they did before the metal joined.
    metal_rng = np.random.default_rng(seed + 1)
    for f in range(len(FAMILIES)):
        _place(atlas, paint_metal(metal_rng), f, METAL_COL)
    return np.round(atlas * 255).astype(np.uint8), ids, means


def _place(dst, tile, row, col):
    """A tile (rows bottom up) into its cell, flipped to image rows and wrapped into the gutter."""
    t = np.pad(tile[::-1], ((GUTTER, GUTTER), (GUTTER, GUTTER), (0, 0)), mode="wrap")
    y, x = row * CELL, col * CELL
    dst[y:y + CELL, x:x + CELL, :t.shape[2]] = t


def grid_m(family):
    """The family's common bay in metres: every row has a pier centred on each multiple of it."""
    return TILE_M / math.gcd(*(spec["bays"] for spec in FAMILIES[family]))


def cell_origin(row, col):
    """The tile's top-left texel (x, y) in the atlas image."""
    return col * CELL + GUTTER, row * CELL + GUTTER
