import sys
import numpy as np
from PIL import Image


def load(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.int32)


a = load(sys.argv[1])
b = load(sys.argv[2]) if len(sys.argv) > 2 else None
h, w, _ = a.shape
total = h * w
print(f"size {w}x{h} = {total} px")

r, g, bl = a[:, :, 0], a[:, :, 1], a[:, :, 2]
# magenta-hued: red and blue close to each other and both clearly above green
mag = (np.abs(r - bl) <= 24) & (r - g >= 40) & (bl - g >= 40) & (r >= 40)
print(f"magenta-hued px in A: {int(mag.sum())}")
exact = ((r == 255) & (g == 0) & (bl == 255))
print(f"exact ff00ff px in A: {int(exact.sum())}")

if b is not None:
    diff = (np.abs(a - b).sum(axis=2) > 0)
    print(f"pixels differing A vs B: {int(diff.sum())}")
    print(f"  of those magenta-hued: {int((diff & mag).sum())}")
    print(f"  magenta-hued but equal in B: {int((mag & ~diff).sum())}")
    print(f"  differing but NOT magenta-hued: {int((diff & ~mag).sum())}")
    ys, xs = np.nonzero(diff)
    if len(xs):
        print(f"  diff bbox x[{xs.min()},{xs.max()}] y[{ys.min()},{ys.max()}]")
    ys, xs = np.nonzero(mag)
    if len(xs):
        print(f"  magenta bbox x[{xs.min()},{xs.max()}] y[{ys.min()},{ys.max()}]")
