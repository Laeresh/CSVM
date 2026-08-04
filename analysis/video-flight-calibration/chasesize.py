"""How big the player's aircraft draws in a chase clip, frame by frame (`CAP-21`).

`BL-248` asks whether the original's chase camera distance moves with airspeed --
whether `camparam.zrd.json`'s `dist_factor` / `dist_vary` / `dist_min` / `dist_max`
drive the radius from speed, or are something else entirely.  With the field of
view fixed and the camera untouched, apparent size is inversely proportional to
distance, so the aircraft's SPAN IN PIXELS against the speedometer in the same
frame is a direct read of the law.  `run2chase.py` supplies the abscissa.

WHY A COLOUR KEY AND NOT A DIFFERENCE.  The usual trick of differencing against
the clip median does not work here: in a chase clip the aircraft is the one thing
that holds still (the world slides past behind it), so it is *in* the median.
The player's Fury is saturated crimson and nothing in this scene -- pale sky,
navy sea, tan sand -- comes close, so `R - max(G, B)` separates it cleanly with a
wide margin either side of the threshold.

⚠ THE KEY MEASURES THE RED PART OF THE AIRCRAFT, NOT ITS TRUE SPAN.  The wingtips
are painted black and white, so they are not keyed and the measured span is a few
per cent short of the real wingspan.  That is harmless because every number here
is used as a RATIO against another frame of the same aircraft -- but it is why no
absolute wingspan should be quoted off this file.

⚠ APPARENT SIZE IS NOT DISTANCE ALONE.  Span also shortens when the aircraft rolls
or yaws relative to the camera.  `AREA` is the more attitude-sensitive of the two
measures and `SPAN` the less, so a real distance change must move both together
and in proportion (area ~ span^2); a manoeuvre artifact typically does not.
"""
import os
import sys

import imageio_ffmpeg as iio
import numpy as np

from extract import VID, LAYOUTS, clip_hud

OUT = ".scratch/vidcal/cache"

# Measurement window, canonical game (1280x720) coords.  It sits BETWEEN the two
# chase dial columns (hud.py puts them at game x 165..265 and 1020..1120), so no
# instrument art -- the speedometer's red arc, the STALL lamp -- can ever enter
# the key, and below y=400 so the sky and the compass tape are excluded.
ROI = (270, 400, 1015, 719)

# R - max(G, B).  The crimson runs ~120-160 here and the scene's warmest natural
# surface (sunlit sand) reaches ~15, so the threshold sits in a wide empty gap.
RED_MIN = 45
# ...and a floor on R itself, so deep-shadow pixels that merely happen to be
# red-biased do not join the mask.
LUM_MIN = 60


def measure(short):
    path, _ = clip_hud(short)
    p = os.path.join(VID, path)
    if not os.path.exists(p) and os.path.exists(path):
        p = path
    g = iio.read_frames(p)
    meta = next(g)
    w, h = meta["size"]
    if (w, h) not in LAYOUTS:
        raise SystemExit(f"unknown capture geometry {w}x{h}; see extract.LAYOUTS")
    gx, gy, k = LAYOUTS[(w, h)]
    x0, y0, x1, y1 = ROI
    cx0, cy0 = gx + x0 * k, gy + y0 * k
    cx1, cy1 = gx + x1 * k, gy + y1 * k

    rec = []
    for buf in g:
        fr = np.frombuffer(buf, np.uint8).reshape(h, w, 3)[cy0:cy1, cx0:cx1]
        r = fr[:, :, 0].astype(np.int16)
        m = (r - np.maximum(fr[:, :, 1], fr[:, :, 2])) > RED_MIN
        m &= r > LUM_MIN
        cols = m.any(axis=0)
        rows = m.any(axis=1)
        n = int(m.sum())
        if n < 200 or not cols.any():
            rec.append((np.nan,) * 5)
            continue
        xs = np.flatnonzero(cols)
        ys = np.flatnonzero(rows)
        # Per-column counts give a weighted span that a single stray pixel cannot
        # stretch: trim 0.5% of the mask off each side before taking the extent.
        cnt = m.sum(axis=0).astype(float)
        cum = np.cumsum(cnt) / cnt.sum()
        lo = float(np.searchsorted(cum, 0.005))
        hi = float(np.searchsorted(cum, 0.995))
        rec.append((n, xs[-1] - xs[0] + 1, hi - lo, ys[-1] - ys[0] + 1,
                    float((np.arange(len(cnt)) * cnt).sum() / cnt.sum())))
    g.close()
    a = np.array(rec, np.float64)
    # back to canonical game px / px^2, so clips of different capture geometry
    # are directly comparable
    a[:, 0] /= k * k
    a[:, 1:] /= k
    np.save(f"{OUT}/{short}_size.npy", a)
    ok = np.isfinite(a[:, 0])
    print(f"{short:14s} n={len(a)} keyed={ok.sum()} | area {a[ok,0].min():.0f}.."
          f"{a[ok,0].max():.0f} px^2 | span {a[ok,2].min():.1f}..{a[ok,2].max():.1f} px "
          f"(median {np.median(a[ok,2]):.2f}, sd {a[ok,2].std():.2f})")
    return a


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for s in sys.argv[1:] or ["cap21up", "cap21down", "cap21upfull", "cap21downfull"]:
        measure(s)
