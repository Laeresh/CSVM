"""Z-fight detector: how many pixels change WINNER under a millimetre camera move.

A coplanar pair that the depth buffer cannot separate does not merely look wrong once —
its winner is unstable, so the same pixel flips between the two surfaces when the camera
moves by an amount that changes nothing else. Run the same pose with two loud
`--tex-override` colours a few times with millimetre offsets and count the flips.

A pixel is CLASSIFIED as one override colour when that channel dominates the other by
`MARGIN`; the blend ramp between them (both channels comparable) is excluded, so a
smoothly shifting blend edge is not counted as a flip.

Usage:  python flipcount.py <png> <png> [<png> ...]
"""
import sys

from PIL import Image

MARGIN = 1.35  # channel ratio that counts as "this surface won this pixel"


def classify(path):
    im = Image.open(path).convert("RGB")
    px = im.load()
    w, h = im.size
    out = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r + g < 24:                 # too dark to classify
                continue
            if r > MARGIN * max(g, 1) and r > b:
                out[y * w + x] = 1         # water
            elif g > MARGIN * max(r, 1) and g > b:
                out[y * w + x] = 2         # surf
    return out, w, h


if __name__ == "__main__":
    paths = sys.argv[1:]
    maps = [classify(p) for p in paths]
    w, h = maps[0][1], maps[0][2]
    base = maps[0][0]
    n_red = sum(1 for v in base if v == 1)
    n_grn = sum(1 for v in base if v == 2)
    print(f"{paths[0]}: classified {n_red:,} px A / {n_grn:,} px B "
          f"of {w*h:,} ({(n_red+n_grn)/(w*h):.1%})")
    for p, (m, _w, _h) in zip(paths[1:], maps[1:]):
        flips = sum(1 for a, b in zip(base, m) if a and b and a != b)
        appear = sum(1 for a, b in zip(base, m) if bool(a) != bool(b))
        print(f"  vs {p}: {flips:,} pixels swapped winner "
              f"({flips/(w*h):.2%} of frame), {appear:,} entered/left classification")
