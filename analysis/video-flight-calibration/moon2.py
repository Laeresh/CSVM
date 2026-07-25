"""Moon tracking, second pass.

The canopy struts are brighter and far larger than the moon, and they are also
perfectly static in screen space - so subtracting the clip's temporal median
erases them and leaves the moon as the only bright moving blob. Connected
components then pick it out, and frames where a strut eats it come back as gaps.
"""
import numpy as np
import imageio_ffmpeg as iio
from scipy import ndimage

VID = "OriginalScreenshots/Videos/FlightModel"
GX = 640
SKY = (30, 0, 1250, 430)


def sky_stack(fname, step=1):
    g = iio.read_frames(f"{VID}/{fname}")
    m = next(g)
    w, h = m["size"]
    x0, y0, x1, y1 = SKY
    out = []
    for i, buf in enumerate(g):
        fr = np.frombuffer(buf, np.uint8).reshape(h, w, 3)
        sub = fr[y0:y1, GX + x0:GX + x1]
        out.append((sub[:, :, 0] * 0.299 + sub[:, :, 1] * 0.587
                    + sub[:, :, 2] * 0.114).astype(np.float32))
    g.close()
    return float(m["fps"]), np.stack(out)


def find_moon(stack, rel=40.0, min_px=40, max_px=4000, max_spread=14.0):
    med = np.median(stack, axis=0)
    x0, y0 = SKY[0], SKY[1]
    rows = []
    for i in range(len(stack)):
        d = stack[i] - med
        mask = d > rel
        lab, n = ndimage.label(mask)
        best = None
        for j in range(1, n + 1):
            sel = lab == j
            npx = int(sel.sum())
            if npx < min_px or npx > max_px:
                continue
            ys, xs = np.nonzero(sel)
            w = d[ys, xs]
            cx = float((xs * w).sum() / w.sum())
            cy = float((ys * w).sum() / w.sum())
            sp = float(np.sqrt(((xs - cx) ** 2 + (ys - cy) ** 2).mean()))
            if sp > max_spread:
                continue
            score = float(w.sum())
            if best is None or score > best[0]:
                best = (score, cx + x0, cy + y0, npx, sp)
        if best is None:
            rows.append((i, np.nan, np.nan, 0, 0.0, 0.0))
        else:
            rows.append((i, best[1], best[2], best[3], best[4], best[0]))
    return np.array(rows)
