"""CAP-08 pass 2a: align combo-log.txt to the video's PTS clock.

The rig's tooltip was not visible in the capture (checked: the only overlay in
frame is the recorder's FPS box), so the log's t=0 has to be recovered from the
footage.

Two things make the naive fit fail, and both are worth stating because they are
the kind of trap FINDINGS.md keeps collecting:

1. **Whole-frame motion energy is the wrong signal.**  Its mean is 1.37 but its
   median is 0.20 and its p99 is 21.4 - a passing cloud bank or the sun's glare
   swamps a camera slew, so a mean-in-window score chases weather.  The
   *aircraft* is what the camera is bolted to, and the red mask isolates it from
   the world entirely.  A camera slew moves the silhouette's centroid, area and
   principal axis together and does it in ~0.3 s; the pilot's own gentle
   manoeuvring moves them slowly.  So the signal used here is the rate of change
   of the mask descriptor, not frame difference.

2. **Half the logged edges must not be used as sync anchors.**  Whether adding
   the second key moves the camera at all IS the question (BL-150(d) - blend,
   replace or *ignore*), so scoring an offset on those edges assumes the answer.
   Only the edges whose motion is not in doubt are anchors: the first DOWN of
   each step (base -> A, which certainly moves) and the last UP of each step
   (release -> base, likewise).  28 anchors, all unambiguous.

No sim-clock scaling is applied or needed.  Both clocks are WALL clocks:
AutoHotkey's A_TickCount and the capture's PTS.  The original's fast sim clock
(k = 1.390, FINDINGS.md) matters only when a duration is quoted in sim seconds,
which nothing here does.
"""
import os
import re

import numpy as np

OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")
# playtest/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree that does
# (the same env var the engine reads), defaulting to this checkout.
DATA_ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOG = os.path.join(DATA_ROOT, "playtest", "CAP-08", "combo-log.txt")
HZ = 120.0            # resample grid; well above the ~30 fps source
SLEW = 0.45           # s, a full camera slew at k ~ 7.5/s (BL-150(b))


def load_log(path=LOG):
    """-> (edges, steps).  edges = [(t, 'DOWN'|'UP', key, held_after)],
    steps = [(index, [keys], t_step)]."""
    edges, steps = [], []
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\s*([0-9.]+)\s+(\S+)\s*(.*)", line.rstrip("\n"))
        if not m:
            continue
        t, what, rest = float(m.group(1)), m.group(2), m.group(3)
        if what in ("DOWN", "UP"):
            k = re.search(r"Numpad(\d)", rest).group(1)
            held = re.search(r"held=(\S+)", rest).group(1)
            edges.append((t, what, k, [] if held == "-" else held.split("+")))
        elif what == "STEP":
            edges and None
            steps.append((int(rest.split()[0]), rest.split()[-1].split("+"), t))
    return edges, steps


def anchors(edges, steps):
    """The 28 edges whose camera motion is not itself under test."""
    out = []
    for n, keys, t_step in steps:
        seg = [e for e in edges if e[0] > t_step and
               (n == len(steps) or e[0] < 1e9)]
        # first DOWN after this STEP line
        d = next(e for e in edges if e[0] > t_step and e[1] == "DOWN")
        out.append((d[0], f"step{n} press {d[2]}"))
        # the UP that empties the held set
        u = next(e for e in edges if e[0] > t_step and e[1] == "UP" and not e[3])
        out.append((u[0], f"step{n} release"))
    return out


def descriptor_rate(feat, pts):
    """Rate of change of the aircraft silhouette, on a uniform PTS grid.

    Components are put on comparable scales: centroid in px, sqrt-area in px,
    and the principal axis converted to px by multiplying the angle by the
    silhouette's semi-major length.  Orientation is unwrapped mod pi (a
    principal axis has no head/tail).
    """
    cx, cy = feat[:, 1], feat[:, 2]
    area = np.maximum(feat[:, 0], 1.0)
    ang = feat[:, 6]
    # semi-major axis length, for turning radians into pixels
    tr = feat[:, 3] + feat[:, 4]
    det = feat[:, 3] * feat[:, 4] - feat[:, 5] ** 2
    disc = np.sqrt(np.maximum(tr * tr / 4 - det, 0))
    lmaj = np.sqrt(np.maximum(tr / 2 + disc, 1e-9))

    grid = np.arange(pts[0], pts[-1], 1 / HZ)
    gx = np.interp(grid, pts, cx)
    gy = np.interp(grid, pts, cy)
    ga = np.interp(grid, pts, np.unwrap(2 * ang) / 2)
    gs = np.interp(grid, pts, np.sqrt(area))
    gl = np.interp(grid, pts, lmaj)

    d = np.zeros_like(grid)
    d[1:] = (np.abs(np.diff(gx)) + np.abs(np.diff(gy))
             + np.abs(np.diff(ga)) * gl[1:] + np.abs(np.diff(gs)))
    return grid, d * HZ          # px per second


def fit_offset(anch, grid, sig, lo=-5.0, hi=25.0):
    """Score an offset by the median CAUSAL anchor response, in robust units.

    An after-only score aliases badly: the schedule is quasi-periodic (press and
    release are 6.05 s apart, every step 9.56 s), so sliding by ~-5.9 s drops
    every press onto a release and scores nearly as well.  The fix is to require
    causality - the camera must be STILL just before the key edge and moving
    just after it.  Score = (peak in [t, t+SLEW]) - (peak in [t-SLEW, t-0.05]),
    which a press/release alias cannot satisfy because there the "before"
    window sits inside the previous slew.

    Median over anchors, not mean, so one spectacular slew cannot carry a wrong
    offset.  Units are MADs above the clip's own median: scale-free.
    """
    med = np.median(sig)
    mad = np.median(np.abs(sig - med)) + 1e-9
    z = (sig - med) / mad
    ts = np.array([t for t, _ in anch])
    dt = grid[1] - grid[0]
    win = int(round(SLEW / dt))
    pre = int(round(0.05 / dt))
    curve = []
    for off in np.arange(lo, hi, dt):
        idx = np.searchsorted(grid, ts + off)
        if idx.min() - win < 0 or idx.max() + win >= len(grid):
            curve.append((off, -np.inf))
            continue
        curve.append((off, float(np.median(
            [z[i:i + win].max() - z[i - win:i - pre].max() for i in idx]))))
    curve = np.array(curve)
    best = curve[np.nanargmax(np.where(np.isfinite(curve[:, 1]),
                                       curve[:, 1], -np.inf))]
    return best, curve, z


def main():
    feat = np.load(f"{OUT}/feat.npy")
    pts = np.load(f"{OUT}/pts.npy")
    edges, steps = load_log()
    anch = anchors(edges, steps)
    grid, sig = descriptor_rate(feat, pts)
    (off, score), curve, z = fit_offset(anch, grid, sig)

    fin = curve[np.isfinite(curve[:, 1])]
    order = fin[np.argsort(-fin[:, 1])]
    far = order[np.abs(order[:, 0] - off) > 1.0]

    print(f"anchors={len(anch)}  signal: median={np.median(sig):.2f} "
          f"p99={np.percentile(sig, 99):.1f} px/s")
    print(f"BEST offset = {off:+.4f} s   median anchor response = {score:.2f} MAD")
    print(f"runner-up (>1 s away) = {far[0,0]:+.3f} s  {far[0,1]:.2f} MAD  "
          f"ratio = {score/max(far[0,1],1e-9):.2f}x")
    print(f"  => video_t = log_t {off:+.4f}\n")

    dt = grid[1] - grid[0]
    win = int(round(SLEW / dt))
    print("  anchor                    log_t   video_t   peak(MAD)")
    resp = []
    for t, name in anch:
        i = np.searchsorted(grid, t + off)
        r = z[i:i + win].max()
        resp.append(r)
        print(f"  {name:24s} {t:7.3f} {t+off:9.3f}   {r:8.1f}")
    resp = np.array(resp)
    print(f"\n  anchor responses: min={resp.min():.1f} median={np.median(resp):.1f} "
          f"max={resp.max():.1f} MAD;  {(resp>5).sum()}/{len(resp)} above 5 MAD")
    np.save(f"{OUT}/offset.npy", np.array([off]))


if __name__ == "__main__":
    main()
