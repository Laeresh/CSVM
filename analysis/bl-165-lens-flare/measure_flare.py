"""Measure our lens-flare render the way playtest/CAP-13/measure.py measures the original.

Given a screenshot and the sun's screen position, walk the sun->centre vector to each ring's
centre, scan a radial profile, and report the ring's peak luminance delta over the local sky.

The comparison is only meaningful at a matched sun-to-centre distance: CAP-13's ring deltas were
read off frames that already carried the wash, and the wash composites toward white, so a true
difference d reads as d*(1-alpha). Solved back from that script's own probe coordinates, its two
measured poses sit at 311 px and 340 px from centre.

Targets (CAP-13, delta-luminance over sky):  ring 0.5 -> 10-12,  ring 0.9 -> 14-21,  ring 2.0 -> 3-9
"""
import sys
import numpy as np
from PIL import Image

REF_H = 720.0
FRACS = [0.50, 0.90, 2.00]
DIAS = [102.0, 45.0, 164.0]   # px at REF_H
TARGETS = [(10, 12), (14, 21), (3, 9)]


def load(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(float)


def find_sun(img):
    """Centroid of the saturated core, not its brightest pixel.

    argmax on a saturated disc lands wherever the plateau happens to break ties — measured 16 px
    off the true centre here — and that error propagates doubled into ring 2.0's predicted centre,
    which is enough to make a thin annulus unmeasurable.
    """
    lum = img.mean(axis=2)
    iy, ix = np.unravel_index(np.argmax(lum), lum.shape)
    peak = lum[iy, ix]
    h, w = lum.shape
    r = 60
    y0, y1 = max(0, iy - r), min(h, iy + r)
    x0, x1 = max(0, ix - r), min(w, ix + r)
    win = lum[y0:y1, x0:x1]
    mask = win >= (peak - 4.0)
    ys, xs = np.nonzero(mask)
    return float(x0 + xs.mean()), float(y0 + ys.mean())


def ring_delta(img, cx, cy, radius):
    """Peak mean-luminance delta of the annulus at `radius` over the sky just inside and outside.

    Averaging around the full circle beats a single probe point: it suppresses whatever the ring
    happens to be lying over (a cloud, the deck murk) instead of reading it as ring brightness.
    """
    h, w = img.shape[:2]
    lum = img.mean(axis=2)

    def ring_mean(r):
        th = np.linspace(0, 2 * np.pi, 720, endpoint=False)
        xs = np.clip((cx + r * np.cos(th)).astype(int), 0, w - 1)
        ys = np.clip((cy + r * np.sin(th)).astype(int), 0, h - 1)
        return float(np.median(lum[ys, xs]))

    # Scan radius AND a small centre offset. The predicted centre carries the sun-position error
    # times the fraction, so ring 2.0 inherits double; without the offset search a thin annulus
    # reads as pure sky.
    def ring_mean_at(ox, oy, r):
        th = np.linspace(0, 2 * np.pi, 720, endpoint=False)
        xs = np.clip((cx + ox + r * np.cos(th)).astype(int), 0, w - 1)
        ys = np.clip((cy + oy + r * np.sin(th)).astype(int), 0, h - 1)
        return float(np.median(lum[ys, xs]))

    best, best_r, best_off = None, None, (0.0, 0.0)
    for ox in np.arange(-24, 25, 4.0):
        for oy in np.arange(-24, 25, 4.0):
            for r in np.arange(radius * 0.80, radius * 1.20, 1.0):
                v = ring_mean_at(ox, oy, r)
                if best is None or v > best:
                    best, best_r, best_off = v, r, (ox, oy)
    ox, oy = best_off
    inner = ring_mean_at(ox, oy, best_r * 0.55)
    outer = ring_mean_at(ox, oy, best_r * 1.45)
    sky = (inner + outer) / 2.0

    # CAP-13's measure.py reports the BRIGHTEST pixel in a window on the ring (its `sample()`),
    # not a median around it. The median above is the robust locator; this is the statistic the
    # published targets are in, so calibration must use it or it overshoots.
    th = np.linspace(0, 2 * np.pi, 720, endpoint=False)
    xs = np.clip((cx + ox + best_r * np.cos(th)).astype(int), 0, w - 1)
    ys = np.clip((cy + oy + best_r * np.sin(th)).astype(int), 0, h - 1)
    peak = float(np.max(lum[ys, xs]))
    return peak - sky, best_r, sky, best - sky


def main(path):
    img = load(path)
    h, w = img.shape[:2]
    scale = h / REF_H
    cx, cy = w / 2.0, h / 2.0
    sx, sy = find_sun(img)
    dist_ref = np.hypot(cx - sx, cy - sy) / scale
    print(f'{path}')
    print(f'  frame {w}x{h}  sun at ({sx:.0f},{sy:.0f})  '
          f'sun->centre {dist_ref:.0f} px @{REF_H:.0f}  '
          f'(CAP-13 ring poses: 311 and 340)')
    for frac, dia, (lo, hi) in zip(FRACS, DIAS, TARGETS):
        rx = sx + (cx - sx) * frac
        ry = sy + (cy - sy) * frac
        radius = dia * scale / 2.0
        d, r_at, sky, d_med = ring_delta(img, rx, ry, radius)
        verdict = 'IN BAND' if lo <= d <= hi else ('LOW' if d < lo else 'HIGH')
        factor = '' if verdict == 'IN BAND' else f'  scale x{((lo + hi) / 2) / max(d, 0.01):.2f}'
        print(f'  ring {frac:.2f}  centre ({rx:.0f},{ry:.0f})  r={r_at:.1f}px  '
              f'sky={sky:.1f}  peak_delta={d:+.1f} (median {d_med:+.1f})  '
              f'target {lo}-{hi}  {verdict}{factor}')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'shot.png')
