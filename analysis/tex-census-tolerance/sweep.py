import json
import sys
import numpy as np
from PIL import Image

M64 = (1 << 64) - 1


def fnv1a(s):
    h = 14695981039346656037
    for ch in s:
        c = ord(ch)
        h = ((h ^ (c & 0xFF)) * 1099511628211) & M64
        h = ((h ^ ((c >> 8) & 0xFF)) * 1099511628211) & M64
    return h


def mix(z):
    z = (z + 0x9E3779B97F4A7C15) & M64
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
    return z ^ (z >> 31)


def lin_to_srgb(x):
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(np.maximum(x, 0), 1 / 2.4) - 0.055)


def srgb_to_linear(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def color_for(name):
    h = mix(fnv1a(name.lower()))
    a = ((h >> 8) & 0xFFFF) / 65535.0 * 0.9
    b = ((h >> 32) & 0xFFFF) / 65535.0 * 0.9
    sector = h % 3
    lin = [1.0, a, b] if sector == 0 else ([b, 1.0, a] if sector == 1 else [a, b, 1.0])
    srgb = lin_to_srgb(np.array(lin))
    return tuple(int(round(v * 255)) for v in srgb)


mapfile, shot, extra = sys.argv[1], sys.argv[2], sys.argv[3:]
m = json.load(open(mapfile))
names = [t["name"] for t in m["textures"]]
rgb = [tuple(t["rgb"]) for t in m["textures"]]
# cross-check the hash against the engine's own map
bad = [n for n, c in zip(names, rgb) if color_for(n) != c]
print(f"hash cross-check: {len(names) - len(bad)}/{len(names)} agree with the engine map")
for n in extra:
    if n not in names:
        names.append(n)
        rgb.append(color_for(n))

cand_lin = srgb_to_linear(np.array(rgb, dtype=np.float64))
cand = cand_lin / np.maximum(cand_lin.max(axis=1, keepdims=True), 1e-9)

img = np.asarray(Image.open(shot).convert("RGB")).astype(np.int32)
h, w, _ = img.shape
flat = img.reshape(-1, 3)
key = (flat[:, 0] << 16) | (flat[:, 1] << 8) | flat[:, 2]
uniq, inv, counts = np.unique(key, return_inverse=True, return_counts=True)
ucols = np.stack([(uniq >> 16) & 255, (uniq >> 8) & 255, uniq & 255], axis=1).astype(np.float64)
ulin = srgb_to_linear(ucols)
umax = ulin.max(axis=1)
uch = ulin / np.maximum(umax[:, None], 1e-9)

d = np.linalg.norm(uch[:, None, :] - cand[None, :, :], axis=2)
order = np.argsort(d, axis=1)
best = order[:, 0]
second = order[:, 1]
bestd = d[np.arange(len(uniq)), best]
nextd = d[np.arange(len(uniq)), second]

truth = {"lkzepskin": 113947}
print(f"{'tol':>6} {'margin':>7} {'confident%':>11} {'contested%':>11} {'unmatched%':>11} "
      + " ".join(f"{k}" for k in truth) + "  falsepos")
for tol in (0.02, 0.03, 0.045, 0.06, 0.08, 0.12):
    for margin in (1.2, 1.6, 2.0, 3.0):
        ok = (bestd <= tol) & (nextd >= bestd * margin) & (umax >= 0.02)
        contested = (bestd <= tol) & (nextd < bestd * margin) & (umax >= 0.02)
        conf = counts[ok].sum()
        cont = counts[contested].sum()
        unm = counts.sum() - conf - cont
        per = {}
        for k in truth:
            i = names.index(k)
            per[k] = counts[ok & (best == i)].sum()
        fp = 0
        for e in extra:
            i = names.index(e)
            fp += counts[ok & (best == i)].sum()
        vals = " ".join(f"{per[k]:>{len(k)}d}" for k in truth)
        print(f"{tol:>6.3f} {margin:>7.2f} {100*conf/counts.sum():>10.2f}% {100*cont/counts.sum():>10.2f}% "
              f"{100*unm/counts.sum():>10.2f}% {vals}  {fp}")
