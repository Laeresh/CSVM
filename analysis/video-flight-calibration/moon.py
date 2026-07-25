"""Track the moon to recover roll angle.

The moon is a fixed direction in the world, so under a pure roll about the nose
it sweeps a circle around the screen point the nose projects to, at exactly the
roll rate. It is the brightest compact thing in the sky, isolated, and needs no
dial decoding at all - which makes it an attitude source independent of every
gauge reading.

Fitting a circle to the whole track gives the rotation centre and radius; the
radial residual is the check that the manoeuvre really was a pure roll.
"""
import numpy as np
import imageio_ffmpeg as iio

VID = "OriginalScreenshots/Videos/FlightModel"
GX = 640
SKY = (30, 0, 1250, 430)   # game coords: canopy area, above the panel


def track(fname, thresh=170, min_px=12):
    g = iio.read_frames(f"{VID}/{fname}")
    m = next(g)
    w, h = m["size"]
    fps = m["fps"]
    x0, y0, x1, y1 = SKY
    rows = []
    for i, buf in enumerate(g):
        fr = np.frombuffer(buf, np.uint8).reshape(h, w, 3)
        sub = fr[y0:y1, GX + x0:GX + x1]
        L = sub[:, :, 0] * 0.299 + sub[:, :, 1] * 0.587 + sub[:, :, 2] * 0.114
        mask = L > thresh
        n = int(mask.sum())
        if n < min_px:
            rows.append((i, np.nan, np.nan, n, 0.0))
            continue
        ys, xs = np.nonzero(mask)
        wgt = L[ys, xs] - thresh
        cx = float((xs * wgt).sum() / wgt.sum()) + x0
        cy = float((ys * wgt).sum() / wgt.sum()) + y0
        # spread: a compact blob is the moon, a smeared one is glare on the frame
        sp = float(np.sqrt(((xs - (cx - x0)) ** 2 + (ys - (cy - y0)) ** 2).mean()))
        rows.append((i, cx, cy, n, sp))
    g.close()
    return fps, np.array(rows)


def fit_circle(x, y):
    """Algebraic circle fit: x^2+y^2 + D x + E y + F = 0."""
    A = np.stack([x, y, np.ones_like(x)], 1)
    b = -(x ** 2 + y ** 2)
    sol, *_ = np.linalg.lstsq(A, b, rcond=None)
    D, E, F = sol
    cx, cy = -D / 2, -E / 2
    r = np.sqrt(max(cx ** 2 + cy ** 2 - F, 1e-9))
    return cx, cy, r


def unwrap(a):
    out = np.array(a, float)
    acc = 0.0
    for i in range(1, len(a)):
        if not (np.isfinite(a[i]) and np.isfinite(a[i - 1])):
            out[i] = a[i] + acc
            continue
        d = a[i] - a[i - 1]
        if d > 180:
            acc -= 360
        elif d < -180:
            acc += 360
        out[i] = a[i] + acc
    return out
