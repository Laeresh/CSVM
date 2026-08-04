"""CAP-08 pass 2b: detect camera slews outright, then lock the offset on them.

Sliding the whole logged edge train against the signal only reached a 1.4x
margin over its runner-up, because the rig's schedule is quasi-periodic and an
offset of about -5.9 s drops every press onto a release.  A correlation peak
that shallow is not evidence, so the offset is instead fixed from the *pattern*
the events make.

The rig's cadence is strongly asymmetric and that is what breaks the tie: the
gap from a step's first press to its release is 6.047 s, and from that release
to the next step's press is 3.516 s.  An alias that swaps presses for releases
would have to produce 3.516 then 6.047, which is a different sequence.  So:
detect slew onsets, take the alternating gap sequence, and check which
assignment it matches.
"""
import os

import numpy as np

import sync

OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")


def detect(grid, z, thresh=8.0, refractory=0.8):
    """Slew onsets: upward crossings of `thresh` MAD, de-bounced.

    A slew is ~0.3 s of very fast silhouette motion (BL-150(b): k ~ 7.5/s wall),
    so the 0.8 s refractory cannot merge two rig events - the closest pair the
    rig ever produces is 2.516 s apart - but does stop one slew registering as
    several as the signal chatters over the threshold.
    """
    over = z > thresh
    onset = np.flatnonzero(over[1:] & ~over[:-1]) + 1
    out, last = [], -1e9
    for i in onset:
        if grid[i] - last >= refractory:
            out.append(grid[i])
            last = grid[i]
    return np.array(out)


def main():
    feat = np.load(f"{OUT}/feat.npy")
    pts = np.load(f"{OUT}/pts.npy")
    edges, steps = sync.load_log()
    grid, sig = sync.descriptor_rate(feat, pts)
    med = np.median(sig)
    mad = np.median(np.abs(sig - med)) + 1e-9
    z = (sig - med) / mad

    ev = detect(grid, z)
    print(f"detected {len(ev)} slew onsets\n")

    anch = sync.anchors(edges, steps)
    press = np.array([t for t, n in anch if "press" in n])
    rel = np.array([t for t, n in anch if "release" in n])

    # Least-squares offset for each hypothesis, using only anchors that find a
    # detected slew within +-0.6 s (a press the game ignored would otherwise
    # drag the fit).
    def fit(logts, label):
        best = None
        for off in np.arange(-8, 25, 0.002):
            d = np.abs(ev[None, :] - (logts[:, None] + off))
            near = d.min(axis=1)
            hit = near < 0.6
            if hit.sum() < 0.6 * len(logts):
                continue
            cost = near[hit].mean() - 0.02 * hit.sum()
            if best is None or cost < best[0]:
                best = (cost, off, hit.sum(), near[hit].mean())
        print(f"{label}: offset={best[1]:+.4f}  matched={best[2]}/{len(logts)}  "
              f"mean|resid|={best[3]*1000:.0f} ms")
        return best

    print("Hypothesis A - the 14 first-presses are presses:")
    a = fit(press, "  press-train")
    print("Hypothesis B - alias, the 14 releases are where presses appear:")
    b = fit(rel, "  release-train")

    off = a[1]
    print(f"\nUsing offset {off:+.4f} s from the press train.")
    print("Residual of every anchor against the nearest detected slew:")
    print("  anchor                    log_t   video_t   nearest_slew  resid_ms")
    res = []
    for t, name in anch:
        d = ev - (t + off)
        j = np.argmin(np.abs(d))
        res.append(d[j])
        flag = "" if abs(d[j]) < 0.35 else "   <-- NO SLEW DETECTED NEARBY"
        print(f"  {name:24s} {t:7.3f} {t+off:9.3f} {ev[j]:12.3f} "
              f"{d[j]*1000:9.0f}{flag}")
    res = np.array(res)
    good = np.abs(res) < 0.35
    print(f"\n  {good.sum()}/{len(res)} anchors within 350 ms; "
          f"their mean resid = {res[good].mean()*1000:+.0f} ms, "
          f"sd = {res[good].std()*1000:.0f} ms")

    # Refine on the matched anchors only.
    off_ref = off + res[good].mean()
    print(f"\nREFINED offset = {off_ref:+.4f} s   (video_t = log_t + this)")
    np.save(f"{OUT}/offset.npy", np.array([off_ref]))
    np.save(f"{OUT}/events.npy", ev)


if __name__ == "__main__":
    main()
