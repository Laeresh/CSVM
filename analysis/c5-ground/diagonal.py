"""The diagonal districts' street grid, for PLAN-c5-ground item A2 (issue #357).

Asks whether `cblock2`'s and `cblock3`'s 45-degree streets form a regular grid in texture space,
and so whether a rotated lattice of variant cells exists whose edges lie on street centrelines
and which repeats with the 256 m texture tile.

A street in these paints is a dark band between two one-texel kerb lines, with lamp glows along
it. The kerbs are found as the texel diagonals that are lit almost end to end (a diagonal of a
square texture wraps onto itself, so every per-diagonal number already wraps the tile). Each kerb
is then fitted as a line through its own texels, giving angle, phase and residual. The building
footprints of both sets that stand on the paint (survey.py's `footprint`) are tested against the
centreline and against every diagonal of the crossing direction.

Same data root and rules as survey.py: CSVM_DATA_ROOT names the checkout holding extracted/, the
script embeds no game data, and --plot (texture pixels) refuses a directory inside the repo.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/c5-ground/diagonal.py [--plot <dir outside the repo>]
"""
import argparse
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

# `py -I` leaves the script's own directory off the path.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import survey as S  # noqa: E402

# The visible paint and the two building sets that stand on it (clear, and flagged over its base).
DIAGONAL = {"cblock2": ("cblock2", "cblock5"), "cblock3": ("cblock3", "cblock6")}
# A texel diagonal is a kerb when this share of its texels is lit (luma above KERB_LUMA).
KERB_SHARE = 0.9
KERB_LUMA = 3


def diagonals(n, family):
    """Index of every texel's diagonal. Family '+' holds the lines u + v = const, '-' the lines
    u - v = const; diagonal k of '+' is the texels with (col + row) mod n == k."""
    y, x = np.mgrid[0:n, 0:n]
    return (x + y) % n if family == "+" else (x - y) % n


def per_diagonal(values, idx, n):
    return np.bincount(idx.ravel(), values.ravel().astype(float), n) / np.bincount(idx.ravel(), None, n)


def fit_kerb(lum, k, family, half=4, glow=60):
    """Least-squares line through one kerb's texels: per texel row, the brightest texel within
    `half` texels of the diagonal, skipping rows where that texel sits in a lamp glow. Returns the
    slope dcol/drow, the diagonal it crosses row 0 at, and the RMS residual, all in texels."""
    n = lum.shape[0]
    rows, cols = [], []
    for r in range(n):
        c0 = (k - r) if family == "+" else (k + r)
        win = [(c0 + d) for d in range(-half, half + 1)]
        vals = [lum[r, c % n] for c in win]
        j = int(np.argmax(vals))
        if vals[j] > KERB_LUMA and vals[j] < glow:
            rows.append(r)
            cols.append(win[j])
    rows, cols = np.array(rows, float), np.array(cols, float)
    a = np.vstack([rows, np.ones_like(rows)]).T
    (slope, icpt), *_ = np.linalg.lstsq(a, cols, rcond=None)
    res = cols - (slope * rows + icpt)
    return slope, icpt, float(np.sqrt((res ** 2).mean())), len(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plot", help="write the stripe cells over each paint (original pixels) here")
    args = ap.parse_args()
    if args.plot and os.path.abspath(args.plot).lower().startswith(S.REPO.lower()):
        raise SystemExit("--plot must name a directory outside the repo: plots hold original pixels")

    ch = S.C.Chapter()
    T = {t: S.template(ch, t) for t in S.C.TEMPLATES}
    for paint, sets in DIAGONAL.items():
        lum = S.tex_luma(T[paint]["tex"])
        n = lum.shape[0]
        mpt = 256.0 / n
        print("== %s (%d texels, %.0f m per texel)" % (paint, n, mpt))
        kerbs = {}
        for fam in "+-":
            idx = diagonals(n, fam)
            lit = per_diagonal(lum > KERB_LUMA, idx, n)
            kerbs[fam] = [int(k) for k in np.where(lit >= KERB_SHARE)[0]]
            print("  family u%sv: diagonals lit end to end (share >= %.2f): %s; most lit other diagonal %.2f"
                  % (fam, KERB_SHARE, kerbs[fam] or "none",
                     max(v for k, v in enumerate(lit) if k not in kerbs[fam])))
        fam = max(kerbs, key=lambda f: len(kerbs[f]))
        cross = "-" if fam == "+" else "+"
        if len(kerbs[fam]) != 2 or kerbs[cross]:
            print("  not one street per tile in one family; no stripe lattice read")
            continue
        fits = [fit_kerb(lum, k, fam) for k in kerbs[fam]]
        rows = np.mgrid[0:n, 0:n][0]
        idx = diagonals(n, fam)
        for k, (slope, icpt, rms, used) in zip(kerbs[fam], fits):
            ang = math.degrees(math.atan(abs(slope)))
            # A kerb tilted off 45 degrees by one texel over the tile would leave its diagonal in
            # some quarter of the rows; every quarter staying lit pins it to within atan(1/n).
            quarters = [float((lum[(idx == k) & (rows // (n // 4) == q)] > KERB_LUMA).mean()) for q in range(4)]
            print("  kerb on diagonal %d: lit share per quarter of the rows %s; free fit slope %+.4f "
                  "(%.2f deg), crosses row 0 at col %.2f, RMS %.2f texels over %d rows" % (
                      k, " ".join("%.2f" % q for q in quarters), slope, ang, icpt % n, rms, used))
        k1, k2 = kerbs[fam]
        width = (k2 - k1) % n
        if width > n / 2:
            k1, k2, width = k2, k1, n - width
        mid = (k1 + width / 2.0) % n
        # Texel (col, row) has its centre at UV ((col + 0.5) / n, (row + 0.5) / n).
        c_uv = ((mid + 1.0) / n) % 1.0 if fam == "+" else (mid / n) % 1.0
        print("  street: kerbs %d texels apart on the diagonal index, %.1f m across; centreline u %s v = %.4f + k"
              % (width, width * mpt / math.sqrt(2), fam, c_uv))
        print("  streets per tile in this family: 1, so centrelines lie %.1f m apart; crossing family: none"
              % (256 / math.sqrt(2)))

        xidx = diagonals(n, cross)
        for s in sets:
            m = S.footprint(T[s]["blds"], n, n)
            on_mid = m[idx == int(round(mid)) % n].mean()
            cov = per_diagonal(m, xidx, n)
            free = [k for k in range(n) if cov[k] == 0]
            print("  %s set: share of the centreline diagonal under a building %.3f; diagonals of the "
                  "crossing family no building touches: %d of %d%s" % (
                      s, on_mid, len(free), n, (" (%s)" % runs(free, n)) if free else ""))
        both = S.footprint(T[sets[0]]["blds"], n, n) | S.footprint(T[sets[1]]["blds"], n, n)
        cov = per_diagonal(both, xidx, n)
        print("  both sets: least-covered crossing diagonal %d at %.3f of its length" % (int(np.argmin(cov)), cov.min()))
        if args.plot:
            plot(lum, fam, c_uv, both, os.path.join(args.plot, "%s_stripes.png" % paint))


def runs(ks, n):
    """Consecutive diagonal indices as 'a-b' ranges, wrapping at n."""
    out, start, prev = [], ks[0], ks[0]
    for k in ks[1:] + [None]:
        if k is not None and k == prev + 1:
            prev = k
            continue
        out.append("%d-%d" % (start, prev) if prev != start else "%d" % start)
        if k is not None:
            start = prev = k
    return ", ".join(out)


def plot(lum, fam, c_uv, built, path):
    """Three by three tiles of the paint, brightened, with the footprints in magenta, the stripe
    cell edges (street centrelines) in green and the tile edges in red."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    n, k = lum.shape[0], 3
    v = np.clip((lum / 255) ** 0.45 * 2.2, 0, 1)
    rgb = np.stack([v, v, v], -1)
    rgb[built] = rgb[built] * 0.5 + np.array([0.5, 0, 0.5])
    im = Image.fromarray((np.tile(rgb, (3, 3, 1)) * 255).astype(np.uint8)).resize((3 * n * k, 3 * n * k), Image.NEAREST)
    d = ImageDraw.Draw(im)
    s = n * k
    for i in range(4):
        d.line([(i * s, 0), (i * s, 3 * s)], fill=(255, 0, 0))
        d.line([(0, i * s), (3 * s, i * s)], fill=(255, 0, 0))
    for j in range(-6, 7):
        c = (c_uv + j) * s
        if fam == "+":
            d.line([(c, 0), (c - 3 * s, 3 * s)], fill=(0, 255, 0), width=2)
        else:
            d.line([(c, 0), (c + 3 * s, 3 * s)], fill=(0, 255, 0), width=2)
    im.save(path)
    print("  plot written to %s" % path)


if __name__ == "__main__":
    main()
