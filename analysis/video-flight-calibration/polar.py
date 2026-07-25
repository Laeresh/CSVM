"""Polar unwrap of a fitted dial, in the dial texture's own frame.

Given the affine from fitdial, sample the screen image on a (theta, r) grid so a
needle becomes a vertical ridge at constant theta. Angles are measured the way
the face art is drawn: 0 at the top, increasing clockwise.
"""
import numpy as np

CACHE = ".scratch/vidcal/cache"
NTH = 720   # 0.5 deg per bin
NR = 64


def bilin(img, x, y):
    h, w = img.shape
    ok = (x >= 0) & (x <= w - 1) & (y >= 0) & (y <= h - 1)
    x = np.clip(x, 0, w - 1)
    y = np.clip(y, 0, h - 1)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    x1 = np.minimum(x0 + 1, w - 1)
    y1 = np.minimum(y0 + 1, h - 1)
    fx = x - x0
    fy = y - y0
    v = (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x1] * fx * (1 - fy)
         + img[y1, x0] * (1 - fx) * fy + img[y1, x1] * fx * fy)
    return np.where(ok, v, np.nan)


def polar_grid(affine, r0=0.10, r1=1.00, nth=NTH, nr=NR):
    """Screen (x, y) sample positions for the (theta, r) grid."""
    cx, cy, a, b, c, d = affine
    th = np.arange(nth) * (2 * np.pi / nth)          # 0 at top, clockwise
    r = np.linspace(r0, r1, nr)
    TH, R = np.meshgrid(th, r, indexing="ij")
    u = R * np.sin(TH)
    v = R * np.cos(TH)
    x = cx + a * u + b * (-v)
    y = cy + c * u + d * (-v)
    return x, y, th, r


def unwrap(img, gx, gy):
    return bilin(img.astype(np.float32), gx, gy)
