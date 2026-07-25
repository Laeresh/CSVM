import sys
import numpy as np
from PIL import Image


def srgb_to_linear(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def chroma(rgb_lin):
    m = rgb_lin.max(axis=-1, keepdims=True)
    return np.where(m > 0, rgb_lin / np.maximum(m, 1e-9), 0.0)


a = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(np.float64)
ref = np.array([int(sys.argv[2][i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64)
mask_hex = sys.argv[3] if len(sys.argv) > 3 else None

lin = srgb_to_linear(a)
ch = chroma(lin)
ref_ch = chroma(srgb_to_linear(ref))
d = np.linalg.norm(ch - ref_ch, axis=-1)

r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
# the override mask: magenta-hued, matching the count.py definition
sel = (np.abs(r - b) <= 24) & (r - g >= 40) & (b - g >= 40) & (r >= 40)
print(f"selected px {int(sel.sum())}")
ds = d[sel]
if ds.size:
    qs = [0, 50, 75, 90, 95, 99, 100]
    print("chromaticity distance from the flat colour, percentiles:")
    for q in qs:
        print(f"  p{q:>3}: {np.percentile(ds, q):.4f}")
    brightest = lin[sel].max(axis=-1)
    print(f"linear brightness of those px: min {brightest.min():.4f} median {np.median(brightest):.4f} max {brightest.max():.4f}")
