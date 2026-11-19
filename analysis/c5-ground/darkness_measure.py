"""PLAN-c5-ground A3: classify the captures darkness.ps1 took and measure each class's rendered luma.

Usage: darkness_measure.py <capture dir> <WxH> "<view>=<pos>;<lookat> ..."

Luma is Rec. 601 on the sRGB bytes, 0 to 255, as in the plan's survey. Classes per view:
  ground   the grey-16 and grey-128 ground overrides differ by more than GROUND_DIFF (SHOT-38)
  street   ground whose effective texture luma (E0 inverted through the pixel's own grey curve) is >= STREET_LUMA,
           the survey's street threshold; floor is the rest of the ground
  building not ground, and E128 differs from its --no-clutter twin by more than BUILD_DIFF
  roof     building that gains more than ROOF_GAIN from the zenith sun (EL1 over EL0); wall is the rest
Distance is the ground ray's range to y=5 from the pose at a vertical FOV of 60 degrees.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

GROUND_DIFF = 4.0
BUILD_DIFF = 6.0
ROOF_GAIN = 10.0
STREET_LUMA = 55.0
GREYS = (16, 24, 32, 48, 64, 128)
BANDS = ((0, 400, "near"), (400, 1000, "mid"), (1000, 1e9, "far"))
GROUND_Y = 5.0
FOV_DEG = 60.0


def load(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)


def luma(img):
    return img[..., 0] * 0.299 + img[..., 1] * 0.587 + img[..., 2] * 0.114


def ground_range(w, h, pos, lookat):
    """Range from the camera to the y=5 plane along each pixel's ray, inf where the ray misses."""
    pos, lookat = np.array(pos, float), np.array(lookat, float)
    fwd = (lookat - pos) / np.linalg.norm(lookat - pos)
    right = np.cross(fwd, [0.0, 1.0, 0.0])
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    t = np.tan(np.radians(FOV_DEG) / 2)
    xs = (np.arange(w) + 0.5) / w * 2 - 1
    ys = 1 - (np.arange(h) + 0.5) / h * 2
    dx = xs[None, :, None] * t * w / h * right
    dy = ys[:, None, None] * t * up
    rays = fwd + dx + dy
    rays /= np.linalg.norm(rays, axis=2, keepdims=True)
    with np.errstate(divide="ignore", invalid="ignore"):
        s = (GROUND_Y - pos[1]) / rays[..., 1]
    return np.where(s > 0, s, np.inf)


def invert(curve_l, value):
    """Effective texture luma per pixel: value read back through the pixel's piecewise-linear grey curve."""
    g = np.array(GREYS, np.float32)
    out = np.empty_like(value)
    # Segment by segment, extrapolating past both ends; a flat segment (fogged out) gives NaN.
    seg = np.zeros(value.shape, int)
    for i in range(1, len(GREYS) - 1):
        seg[value > curve_l[i]] = i
    lo = np.take_along_axis(curve_l, seg[None], 0)[0]
    hi = np.take_along_axis(curve_l, (seg + 1)[None], 0)[0]
    with np.errstate(divide="ignore", invalid="ignore"):
        out = g[seg] + (value - lo) / (hi - lo) * (g[seg + 1] - g[seg])
    out[np.abs(hi - lo) < 0.5] = np.nan
    return np.clip(out, 0, 255)


def stats(x):
    x = x[np.isfinite(x)]
    if x.size == 0:
        return "-", "-", 0
    return f"{x.mean():.1f}", f"{np.median(x):.1f}", x.size


def measure(d, view, pos, lookat, w, h):
    c = {k: load(d / f"{view}-{k}.png") for k in
         ("E0", "E016", "E024", "E032", "E048", "E064", "E128", "EN128", "EL0", "EL1", "F0", "F016", "F128", "FN128")
         if (d / f"{view}-{k}.png").exists()}
    L = {k: luma(v) for k, v in c.items()}
    rng = ground_range(w, h, pos, lookat)
    rows = []
    out = {}
    for look in ("E", "F"):
        if not all(f"{look}{k}" in L for k in ("0", "016", "128", "N128")):
            continue
        ground = np.abs(L[f"{look}128"] - L[f"{look}016"]) > GROUND_DIFF
        build = (np.abs(c[f"{look}N128"] - c[f"{look}128"]).max(axis=2) > BUILD_DIFF) & ~ground
        greys = [g for g in GREYS if f"{look}{g:03d}" in L]
        curve = np.stack([L[f"{look}{g:03d}"] for g in greys])
        if len(greys) == len(GREYS):
            teff = invert(curve, L[f"{look}0"])
        else:  # Faithful: a two-point line (its tonemap is linear, SHOT-46)
            a, b = L[f"{look}016"], L[f"{look}128"]
            with np.errstate(divide="ignore", invalid="ignore"):
                teff = np.clip(16 + (L[f"{look}0"] - a) / (b - a) * 112, 0, 255)
        street = ground & (teff >= STREET_LUMA)
        floor = ground & ~street
        classes = {"floor": floor, "street": street}
        if look == "E" and "EL1" in L:
            gain = L["EL1"] - L["EL0"]
            roof = build & (gain > ROOF_GAIN)
            classes.update({"wall": build & ~roof, "roof": roof})
            # The split is only sound if the gain is bimodal: walls near 0 (the sun 2 degrees off the zenith), roofs well over.
            out["ground_gain"] = (np.median(gain[build & ~roof]), np.median(gain[roof]) if roof.any() else np.nan)
        else:
            classes["building"] = build
        out[look] = classes
        out[look + "teff"] = teff
        # A building pixel takes the range of the first non-building pixel below it in its column, its base's range;
        # its own ray's ground hit lies behind it.
        base = rng.copy()
        for y in range(h - 2, -1, -1):
            base[y] = np.where(build[y], base[y + 1], base[y])
        for lo, hi, band in BANDS + ((0, 1e9, "all"),):
            inband = (rng >= lo) & (rng < hi)
            binband = (base >= lo) & (base < hi)
            row = {"view": view, "look": look, "band": band}
            for name, m in classes.items():
                row[name] = stats(L[f"{look}0"][m & (inband if name in ("floor", "street") else binband)])
            gsel = ground & inband
            for g, cl in zip(greys, curve):
                row[f"g{g}"] = stats(cl[gsel])
            row["floor_teff"] = stats(teff[floor & inband])
            row["street_teff"] = stats(teff[street & inband])
            row["ground_px"] = int(gsel.sum())
            row["fogged_px"] = int((np.isfinite(rng) & inband & ~ground & ~build).sum())
            rows.append(row)
    montage(d, view, c, out)
    return rows, out.get("ground_gain")


COLOURS = {"floor": (40, 90, 255), "street": (255, 220, 0), "wall": (255, 40, 40), "roof": (40, 220, 60),
           "building": (255, 40, 40)}


def montage(d, view, c, out):
    tiles = []
    for look in ("E", "F"):
        if look not in out:
            continue
        img = c[f"{look}0"]
        over = img * 0.35
        for name, m in out[look].items():
            over[m] = np.array(COLOURS[name], np.float32) * 0.6 + img[m] * 0.4
        tiles += [img, over]
    h, w = tiles[0].shape[:2]
    half = [Image.fromarray(t.clip(0, 255).astype(np.uint8)).resize((w // 2, h // 2), Image.BILINEAR) for t in tiles]
    sheet = Image.new("RGB", (w, h // 2 * ((len(half) + 1) // 2)))
    for i, t in enumerate(half):
        sheet.paste(t, ((i % 2) * (w // 2), (i // 2) * (h // 2)))
    sheet.save(d / f"montage-{view}.png")


def main():
    d = Path(sys.argv[1])
    w, h = map(int, sys.argv[2].split("x"))
    allrows = []
    for spec in sys.argv[3].split():
        view, poses = spec.split("=")
        p, l = poses.split(";")
        rows, gain = measure(d, view, [float(v) for v in p.split(",")], [float(v) for v in l.split(",")], w, h)
        allrows += rows
        if gain is not None:
            print(f"{view}: zenith-sun gain, median wall {gain[0]:.1f}, median roof {gain[1]:.1f}")
    cols = ["floor", "street", "wall", "roof", "building"] + [f"g{g}" for g in GREYS] + ["floor_teff", "street_teff"]
    print("view look band  " + "  ".join(f"{k}(mean/med/n)" for k in cols) + "  ground_px fogged_px")
    for r in allrows:
        cells = ["/".join(map(str, r[k])) if k in r else "." for k in cols]
        print(f"{r['view']} {r['look']} {r['band']}  " + "  ".join(cells) + f"  {r['ground_px']} {r['fogged_px']}")


if __name__ == "__main__":
    # Self-check of the inversion: a pixel whose curve is 2x the grey reads back its own texture luma.
    _c = np.array(GREYS, np.float32)[:, None, None] * 2 * np.ones((1, 1, 2), np.float32)
    assert np.allclose(invert(_c, np.array([[20.0, 100.0]], np.float32)), [[10.0, 50.0]])
    assert np.allclose(invert(_c, np.array([[20.0, 300.0]], np.float32)), [[10.0, 150.0]])
    main()
