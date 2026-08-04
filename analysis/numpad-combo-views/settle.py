"""CAP-08 pass 6: is the combined position a POSITION, and how is it reached?

Two failure modes would invalidate the verdicts in match.py, and neither shows
up in a still:

1. **The combination might never settle.**  If the camera is still drifting at
   the end of the 3.5 s hold then "the combined position" is not a position and
   the IoU match is measuring a moment, not a state.  Same test BL-150 already
   applied to CAP-07's nine holds (frame-to-frame motion over the last 1.2 s,
   0.055-0.278 against a 0.090 baseline): compare each window's residual motion
   against a no-key baseline stretch of the same length.

2. **The route might pass through base.**  BL-150(d)'s standing claim is that
   the original goes straight to a new position without snapping to base first.
   That is a claim about the path, not the endpoints, so it needs the samples
   in between: track the silhouette's distance from the base view across the
   transition and see whether it ever returns near zero.
"""
import os

import numpy as np

import sync
from decide import desc, dist, iou, mean_mask, unpack, window

OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")


def resid(feat, pts, t0, t1):
    """Mean per-second silhouette rate inside a window - the settledness test."""
    grid, sig = sync.descriptor_rate(feat, pts)
    m = (grid >= t0) & (grid < t1)
    return float(np.median(sig[m])) if m.any() else np.nan


def main():
    feat = np.load(f"{OUT}/feat.npy")
    pts = np.load(f"{OUT}/pts.npy")
    masks = np.load(f"{OUT}/masks.npy")
    off = float(np.load(f"{OUT}/offset.npy")[0])
    edges, steps = sync.load_log()
    grid, sig = sync.descriptor_rate(feat, pts)

    # Baseline: the lead-in before the first press, no key ever held.
    t_first = min(e[0] for e in edges if e[1] == "DOWN")
    base_rate = np.median(sig[(grid >= pts[0] + 0.5) & (grid < t_first + off - 0.2)])
    print(f"no-key baseline silhouette rate = {base_rate:.2f} px/s "
          f"(over {t_first + off - 0.7:.1f} s of lead-in)\n")

    print("Settledness - median silhouette rate over the last 1.2 s of each "
          "window, px/s.\nA window at or near the baseline is at rest.\n")
    print("step combo     A-alone   combined   ratio_to_baseline")
    rows = []
    for n, keys, t_step in steps:
        ds = [e for e in edges if e[0] > t_step and e[1] == "DOWN"]
        us = [e for e in edges if e[0] > t_step and e[1] == "UP"]
        tA, tB, tU = ds[0][0], ds[1][0], us[0][0]
        ra = resid(feat, pts, tB + off - 1.2, tB + off - 0.05)
        rc = resid(feat, pts, tU + off - 1.2, tU + off - 0.05)
        rows.append((n, "+".join(keys), tA, tB, tU, ra, rc))
        print(f"{n:3d}  {'+'.join(keys):7s} {ra:9.2f} {rc:10.2f} "
              f"{rc/base_rate:12.2f}x")
    ar = np.array([r[5] for r in rows])
    cr = np.array([r[6] for r in rows])
    print(f"\n  A-alone   : {ar.min():.2f} - {ar.max():.2f} px/s")
    print(f"  combined  : {cr.min():.2f} - {cr.max():.2f} px/s")
    print(f"  baseline  : {base_rate:.2f} px/s")

    # --- route: does adding the second key pass through base? ---
    print("\n\nRoute of the add-second-key transition.  Distance is the "
          "silhouette's\ncentroid+axis distance from the settled BASE view of "
          "that step, sampled\nevery frame from the edge to +1.5 s.  If the "
          "camera snapped to base first,\nthe trace would dip to ~0 on the way.\n")
    print("step combo    d(A,base) d(AB,base)  min d during transition  "
          "dips_to_base?")
    for n, lab, tA, tB, tU, _, _ in rows:
        wb = window(pts, tA + off - 0.8, tA + off - 0.05)
        wa = window(pts, tB + off - 0.8, tB + off - 0.05)
        wc = window(pts, tU + off - 1.0, tU + off - 0.05)
        db, da, dc = desc(feat, wb), desc(feat, wa), desc(feat, wc)

        def sep(d):
            c, ar_, ang = dist(db, d)
            return np.hypot(c, np.degrees(0) + abs(ang) * 2)

        idx = window(pts, tB + off, tB + off + 1.5)
        trace = [sep(desc(feat, [i])) for i in idx]
        dmin = min(trace) if trace else np.nan
        # the two endpoints of this transition, for scale
        sa, sc = sep(da), sep(dc)
        floor_ = 0.25 * min(sa, sc)
        print(f"{n:3d}  {lab:7s} {sa:9.1f} {sc:10.1f} {dmin:22.1f}  "
              f"{'YES' if dmin < floor_ else 'no'}")


if __name__ == "__main__":
    main()
