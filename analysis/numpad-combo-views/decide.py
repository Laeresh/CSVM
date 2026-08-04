"""CAP-08 pass 3: does adding a second held key blend, replace, or do nothing?

This is the measurement BL-150(d) is open on.  The rig staggers each step -
first key alone until the camera settles, then the rest added on top, then
everything released - so each step contains three answerable transitions and
the middle one is the question.

Offset check first.  `events.py` shows both the press train and the release
train can each be matched on their own (they are the two halves of the same
detected event set), so the discriminator is whether ONE offset aligns BOTH at
once.  It is: only the true offset does, because the press->release gap (6.047 s)
and release->next-press gap (3.516 s) differ.

Then, per step:

  base    the 0.8 s before the first press
  A       the 0.8 s before the second key goes down (first key alone, settled)
  AB      the last 1.0 s of the hold (combination settled)
  after   the 0.8 s before the next step's press (released, back to base)

and the silhouette is compared between those windows.  Comparison is by mask
IoU plus the scalar descriptor, both computed in frame coordinates.  Two
cautions that shape how the numbers are read:

  * Cross-step comparison is weaker than within-step.  The pilot is flying
    manually, so between step 1 and step 11 the aircraft's own attitude has
    drifted; A-vs-AB inside one step is 3 s apart and clean, whereas AB-vs-
    (B alone, from a different step) can be 60 s apart.  Every key appears as a
    first key somewhere, and five of them appear twice, so the repeat pairs
    give the drift-and-noise floor that cross-step numbers must be judged
    against.
  * "No change" is a claim about a threshold.  The floor above is what makes it
    one - a difference smaller than two same-key holds differ from each other
    is not a difference.
"""
import os

import numpy as np

import sync

OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")
W, H = 640, 360
SETTLE_TAIL = 0.8      # s of settled window sampled before an edge
HOLD_TAIL = 1.0        # s sampled at the end of the combination hold


def unpack(masks, i):
    return np.unpackbits(masks[i])[:W * H].reshape(H, W).astype(bool)


def window(pts, t0, t1):
    return np.flatnonzero((pts >= t0) & (pts < t1))


def mean_mask(masks, idx):
    """Occupancy map over a window: mean of the binary masks."""
    return np.mean([unpack(masks, i) for i in idx], axis=0)


def iou(a, b, t=0.5):
    A, B = a >= t, b >= t
    u = (A | B).sum()
    return float((A & B).sum() / u) if u else np.nan


def desc(feat, idx):
    """Median descriptor over a window: centroid, sqrt-area, axis angle."""
    f = feat[idx]
    ang = np.unwrap(2 * f[:, 6]) / 2
    return np.array([np.median(f[:, 1]), np.median(f[:, 2]),
                     np.median(np.sqrt(f[:, 0])), np.median(ang)])


def dist(d0, d1):
    """Centroid shift in px, area ratio, axis rotation in degrees."""
    return (float(np.hypot(d1[0] - d0[0], d1[1] - d0[1])),
            float(d1[2] / max(d0[2], 1e-9)),
            float(np.degrees((d1[3] - d0[3] + np.pi / 2) % np.pi - np.pi / 2)))


def check_offset(anch, ev, offs):
    print("Offset test - one offset must align BOTH trains at once:")
    for off in offs:
        r = []
        for t, _ in anch:
            d = ev - (t + off)
            r.append(d[np.argmin(np.abs(d))])
        r = np.array(r)
        g = np.abs(r) < 0.35
        print(f"  offset {off:+8.4f}: {g.sum():2d}/{len(r)} anchors matched, "
              f"sd={r[g].std()*1000:5.0f} ms")
    print()


def main():
    feat = np.load(f"{OUT}/feat.npy")
    pts = np.load(f"{OUT}/pts.npy")
    masks = np.load(f"{OUT}/masks.npy")
    ev = np.load(f"{OUT}/events.npy")
    off = float(np.load(f"{OUT}/offset.npy")[0])
    edges, steps = sync.load_log()
    check_offset(sync.anchors(edges, steps), ev, [off, -2.0720])

    grid, sig = sync.descriptor_rate(feat, pts)
    med = np.median(sig)
    mad = np.median(np.abs(sig - med)) + 1e-9
    z = (sig - med) / mad

    def peak(t, w=0.55):
        i = np.searchsorted(grid, t + off)
        return float(z[i:i + int(w * sync.HZ)].max())

    singles = {}          # key -> list of (step, occupancy, descriptor)
    rows = []
    for n, keys, t_step in steps:
        ds = [e for e in edges if e[0] > t_step and e[1] == "DOWN"]
        us = [e for e in edges if e[0] > t_step and e[1] == "UP"]
        tA = ds[0][0]
        tB = ds[1][0]                       # first of the added keys
        tU = us[0][0]
        nxt = next((s[2] for s in steps if s[0] == n + 1), tU + 3.4)

        wins = {
            "base": window(pts, tA + off - SETTLE_TAIL, tA + off - 0.05),
            "A":    window(pts, tB + off - SETTLE_TAIL, tB + off - 0.05),
            "AB":   window(pts, tU + off - HOLD_TAIL, tU + off - 0.05),
            "post": window(pts, nxt + off - SETTLE_TAIL, nxt + off - 0.05),
        }
        occ = {k: mean_mask(masks, v) for k, v in wins.items()}
        dsc = {k: desc(feat, v) for k, v in wins.items()}

        singles.setdefault(keys[0], []).append((n, occ["A"], dsc["A"]))

        rows.append(dict(n=n, keys=keys, tA=tA, tB=tB, tU=tU,
                         occ=occ, dsc=dsc,
                         pA=peak(tA), pB=peak(tB), pU=peak(tU)))

    lbl = lambda k: "+".join(k)
    print("Per-step transitions.  'slew' is the peak silhouette rate in the "
          "0.55 s after the\nedge, in MADs above the clip median; the 28 "
          "unambiguous edges (press-alone,\nrelease-all) run 6-116 MAD, so "
          "that is the scale for 'the camera moved'.\n")
    print("step  combo    slew@pressA  slew@addB  slew@release   "
          "IoU(base,A) IoU(A,AB) IoU(base,AB)")
    for r in rows:
        print(f"{r['n']:3d}   {lbl(r['keys']):7s} "
              f"{r['pA']:11.1f} {r['pB']:10.1f} {r['pU']:13.1f}   "
              f"{iou(r['occ']['base'], r['occ']['A']):11.3f} "
              f"{iou(r['occ']['A'], r['occ']['AB']):9.3f} "
              f"{iou(r['occ']['base'], r['occ']['AB']):12.3f}")

    print("\nSame, as descriptor shifts (centroid px / area ratio / axis deg):")
    print("step  combo     base->A                 A->AB                   "
          "base->AB")
    for r in rows:
        a = dist(r['dsc']['base'], r['dsc']['A'])
        b = dist(r['dsc']['A'], r['dsc']['AB'])
        c = dist(r['dsc']['base'], r['dsc']['AB'])
        f = lambda t: f"{t[0]:6.1f}px {t[1]:5.2f}x {t[2]:+6.1f}d"
        print(f"{r['n']:3d}   {lbl(r['keys']):7s}  {f(a)}  {f(b)}  {f(c)}")

    # --- noise floor from keys that were held alone in more than one step ---
    print("\nRepeatability floor - the same key held alone in two different "
          "steps.\nThese pairs are separated by up to 90 s of hand-flown "
          "drift, so they bound\nhow much of a cross-step difference means "
          "nothing:")
    print("  key   steps      IoU    centroid   area   axis")
    floor = []
    for k, v in sorted(singles.items()):
        for i in range(len(v)):
            for j in range(i + 1, len(v)):
                d = dist(v[i][2], v[j][2])
                I = iou(v[i][1], v[j][1])
                floor.append((I, *d))
                print(f"  {k}     {v[i][0]:2d},{v[j][0]:2d}    {I:5.3f}  "
                      f"{d[0]:6.1f}px {d[1]:5.2f}x {d[2]:+6.1f}d")
    if floor:
        fl = np.array(floor)
        print(f"  -> IoU floor: min={fl[:,0].min():.3f} median="
              f"{np.median(fl[:,0]):.3f};  centroid up to {fl[:,1].max():.1f} px, "
              f"axis up to {np.abs(fl[:,3]).max():.1f} deg")

    np.save(f"{OUT}/rows_occ.npy",
            np.stack([np.stack([r['occ'][k] for k in ("base", "A", "AB", "post")])
                      for r in rows]))
    import json
    json.dump([{k: (v if k != 'occ' and k != 'dsc' else
                    {kk: list(map(float, vv)) for kk, vv in v.items()})
                for k, v in r.items() if k != 'occ'} for r in rows],
              open(f"{OUT}/rows.json", "w"), indent=1)


if __name__ == "__main__":
    main()
