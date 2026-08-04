"""CAP-08 pass 5: identify each combination against the single-key views.

decide.py measures how much the silhouette changed; this asks what it changed
into.  Four hypotheses for what holding A+B does, and each makes a different
prediction about which reference the combination's silhouette matches:

  ignore   AB == A alone            (the second key does nothing while A is down)
  replace  AB == B alone, or A      (a single-view selector picks one winner;
                                     our own ActiveView() takes the first array
                                     match, so it predicts one of the two)
  cancel   AB == base               (the two contributions annul)
  blend    AB matches none of them  (a genuine third position)

Every one of the eight keys is held alone somewhere in this clip, so the
references are drawn from the same take, the same aircraft and the same
lighting as the combinations - no cross-session calibration is involved.

⚠ The floor matters more than the match here.  Cross-step comparison carries
the pilot's own attitude drift, so "matches" has to mean "as close as two holds
of the SAME key in different steps are", which decide.py measures at IoU
0.833-0.978.  A match below that band is not a match, and this script prints
the floor alongside every verdict rather than asserting a threshold.
"""
import os

import numpy as np

import sync
from decide import (HOLD_TAIL, SETTLE_TAIL, desc, dist, iou, mean_mask, window)

OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")


def main():
    feat = np.load(f"{OUT}/feat.npy")
    pts = np.load(f"{OUT}/pts.npy")
    masks = np.load(f"{OUT}/masks.npy")
    off = float(np.load(f"{OUT}/offset.npy")[0])
    edges, steps = sync.load_log()

    single, base, combo = {}, {}, {}
    for n, keys, t_step in steps:
        ds = [e for e in edges if e[0] > t_step and e[1] == "DOWN"]
        us = [e for e in edges if e[0] > t_step and e[1] == "UP"]
        tA, tB, tU = ds[0][0], ds[1][0], us[0][0]
        wb = window(pts, tA + off - SETTLE_TAIL, tA + off - 0.05)
        wa = window(pts, tB + off - SETTLE_TAIL, tB + off - 0.05)
        wc = window(pts, tU + off - HOLD_TAIL, tU + off - 0.05)
        single.setdefault(keys[0], []).append((n, mean_mask(masks, wa),
                                               desc(feat, wa)))
        base[n] = (mean_mask(masks, wb), desc(feat, wb))
        combo[n] = ("+".join(keys), mean_mask(masks, wc), desc(feat, wc))

    # nearest-in-time reference for each key, to minimise drift
    def ref(k, n):
        v = single[k]
        return min(v, key=lambda x: abs(x[0] - n))

    floor = []
    for k, v in single.items():
        for i in range(len(v)):
            for j in range(i + 1, len(v)):
                floor.append(iou(v[i][1], v[j][1]))
    lo, hi = min(floor), max(floor)
    print(f"same-key repeatability floor (n={len(floor)} pairs): "
          f"IoU {lo:.3f} - {hi:.3f}\n")

    keys8 = sorted(single)
    print("IoU of each held combination against every single-key view "
          "(and against base).")
    print("A match at or above the floor means the combination IS that view.\n")
    print("step combo    base " + " ".join(f"  Kp{k}" for k in keys8)
          + "    best        verdict")
    verdicts = []
    for n in sorted(combo):
        lab, occ, d = combo[n]
        ib = iou(base[n][0], occ)
        row = {}
        for k in keys8:
            row[k] = iou(ref(k, n)[1], occ)
        cand = [("base", ib)] + [(f"Kp{k}", row[k]) for k in keys8]
        bk, bv = max(cand, key=lambda x: x[1])
        held = lab.split("+")
        if bv < lo:
            verd = "BLEND (no single view matches)"
        elif bk == "base":
            verd = "CANCEL (back to the default chase view)"
        elif bk[2:] == held[0]:
            verd = f"IGNORE (stayed on {bk})"
        elif bk[2:] in held[1:]:
            verd = f"REPLACE (took {bk})"
        else:
            verd = f"matches {bk}, which is not in the combination"
        verdicts.append((n, lab, bk, bv, verd))
        print(f"{n:3d}  {lab:7s} {ib:5.3f} "
              + " ".join(f"{row[k]:5.3f}" for k in keys8)
              + f"   {bk:5s}{bv:6.3f}  {verd}")

    print("\nSummary by pair type (the rig's own grouping):")
    groups = [("adjacent", range(1, 9)), ("opposite", range(9, 13)),
              ("triple", range(13, 15))]
    for name, rng in groups:
        v = [x for x in verdicts if x[0] in rng]
        print(f"  {name:9s}: " + "; ".join(
            f"{lab}={verd.split(' ')[0]}" for _, lab, _, _, verd in v))


if __name__ == "__main__":
    main()
