"""Track the weapon-gauge selector arrow, on either HUD.

BL-184 asks one thing: when the player switches gun group or hardpoint, does the
pointer snap to the new slot or sweep to it, and if it sweeps, over how long.

Two things make this harder than reading a needle, and both are handled here
rather than assumed away:

  * The gauge face carries two BRIGHT READOUT BOXES ("40 SLUG", "2400") that sit
    in the same radial band as the arrow and are just as bright, so a plain
    angular-profile peak hunt finds text about as often as it finds the arrow.
    They are static, though, so differencing each frame's profile against the
    clip's own median profile cancels them. What survives is the arrow.
  * Differencing also cancels the arrow wherever it happens to sit at the
    reference angle - which is most of the clip, because the arrow rests on one
    slot. That is detected (weak positive peak) and reported as "at the
    reference angle" rather than silently returning noise.

The pivot is not taken on trust from `fitdial`: the gun gauge fits its texture
at only NCC ~0.74, because those same readout boxes are painted over the face
and are not in the texture. Instead `solve_pivot` intersects the arrow's own
line across the frames where it is mid-sweep - the point every sweep line passes
through IS the rotation centre, which is the only thing the angle needs.
"""
import numpy as np

import hud as hudlib
from polar import bilin

CACHE = ".scratch/vidcal/cache"
NTH = 720
DEG = 360.0 / NTH


def _dial_geom(hud, dial="gungauge"):
    (cx, cy), (au, av), box, _tex = hudlib.dials(hud)[dial]
    try:                       # prefer the fitted affine's scale when present
        p = hudlib.load_affine(hud, dial, CACHE)
        return float(p[0]), float(p[1]), float(np.hypot(p[2], p[4]))
    except SystemExit:
        return cx, cy, float(au)


def profiles(frames, cx, cy, radius, r0=0.25, r1=0.62, nth=NTH, nr=32):
    """Angular brightness profile per frame over an annulus of the dial.

    r0/r1 bracket the arrow's shaft: outside the hub, inside the tick ring."""
    th = np.arange(nth) * (2 * np.pi / nth)
    r = np.linspace(r0, r1, nr) * radius
    TH, R = np.meshgrid(th, r, indexing="ij")
    # theta measured clockwise from 12 o'clock, matching a dial face
    X = cx + R * np.sin(TH)
    Y = cy - R * np.cos(TH)
    out = np.empty((len(frames), nth), np.float32)
    for i, f in enumerate(frames):
        out[i] = np.nanmean(bilin(f.astype(np.float32), X, Y), axis=1)
    return out


def _hp(prof, half=40):
    """Drop anything broader than +-half bins in theta (bezel gradients, glow)."""
    n = prof.shape[-1]
    k = 2 * half + 1
    pad = np.concatenate([prof[..., -half:], prof, prof[..., :half]], axis=-1)
    base = np.stack([np.median(pad[..., i:i + k], axis=-1) for i in range(n)], -1)
    return prof - base


def track(clip, hud="chase", dial="gungauge", min_peak=2.0, centre=None):
    """Arrow angle per frame, in degrees clockwise from 12 o'clock.

    Returns (angle, strength, reference_angle). Frames whose positive residual
    never clears `min_peak` are reported at the reference angle, because that is
    what a cancelled arrow means - not a failed read.

    `centre` overrides the fitted dial centre, for a gauge whose texture fit did
    not lock. ⚠ A wrong centre does not fail loudly here - it skews the angle
    smoothly, and the tell is that an out-and-back pair stops being equal and
    opposite. Check that before quoting any angle off an unfitted dial."""
    a = np.load(f"{CACHE}/{clip}_lum.npy")
    cx, cy, radius = _dial_geom(hud, dial)
    if centre is not None:
        cx, cy = centre
    P = _hp(profiles(a, cx, cy, radius))
    ref = np.median(P, axis=0)
    D = np.clip(P - ref, 0, None)

    # the reference frame's own arrow: strongest ridge in the median profile
    ref_i = int(np.argmax(_hp(ref[None, :])[0]))
    ang = np.full(len(a), ref_i * DEG, float)
    strength = D.max(axis=1)
    for i in range(len(a)):
        if strength[i] < min_peak:
            continue
        j = int(np.argmax(D[i]))
        idx = (np.arange(j - 12, j + 13)) % NTH
        w = D[i][idx]
        off = np.arange(-12, 13)
        ang[i] = ((j + (off * w).sum() / max(w.sum(), 1e-9)) % NTH) * DEG
    return ang, strength, ref_i * DEG


def solve_pivot(clip, hud="chase", dial="gungauge", frac=0.6, lag=3):
    """Least-squares intersection of the arrow's line across mid-sweep frames.

    Each frame contributes the line through its arrow pixels; every such line
    passes through the rotation centre, so the intersection is the pivot with no
    reliance on the texture fit.

    ⚠ Difference against a NEIGHBOURING frame, not against the clip median. The
    readout boxes change text on a switch, so for as long as the new value is
    displayed a median-difference leaves a bright HORIZONTAL residual across the
    face - and horizontal residual fits horizontal lines, which dragged an
    earlier version of this pivot 10 px sideways. Two frames `lag` apart carry
    the same text, so it cancels and only the arrow moves."""
    a = np.load(f"{CACHE}/{clip}_lum.npy").astype(np.float32)
    cx, cy, radius = _dial_geom(hud, dial)
    x0, y0 = int(cx - 1.1 * radius), int(cy - 1.1 * radius)
    x1, y1 = int(cx + 1.1 * radius), int(cy + 1.1 * radius)
    ys, xs = np.mgrid[y0:y1, x0:x1]
    rr = np.hypot(xs - cx, ys - cy)
    band = (rr > 0.22 * radius) & (rr < 0.68 * radius)
    A, b = [], []
    used = 0
    for i in range(lag, len(a)):
        d = a[i][y0:y1, x0:x1] - a[i - lag][y0:y1, x0:x1]
        m = band & (d > frac * max(d[band].max(), 1e-6)) & (d > 12)
        if m.sum() < 25:
            continue
        px, py = xs[m].astype(float), ys[m].astype(float)
        px -= px.mean()
        py -= py.mean()
        # principal axis of the arrow pixels -> its direction
        cov = np.array([[(px * px).sum(), (px * py).sum()],
                        [(px * py).sum(), (py * py).sum()]])
        w, V = np.linalg.eigh(cov)
        if w[1] < 6 * max(w[0], 1e-9):        # not elongated: not a clean arrow
            continue
        dirv = V[:, 1]
        nx, ny = -dirv[1], dirv[0]            # normal to the line
        px0, py0 = xs[m].mean(), ys[m].mean()
        A.append([nx, ny])
        b.append(nx * px0 + ny * py0)
        used += 1
    if used < 8:
        return None, used
    A, b = np.array(A), np.array(b)
    sol, *_ = np.linalg.lstsq(A, b, rcond=None)
    return (float(sol[0]), float(sol[1])), used


if __name__ == "__main__":
    import sys

    clip = sys.argv[1] if len(sys.argv) > 1 else "cap18"
    which = sys.argv[2] if len(sys.argv) > 2 else "chase"
    piv, n = solve_pivot(clip, which)
    cx, cy, radius = _dial_geom(which, "gungauge")
    print(f"fitdial centre      ({cx:7.2f},{cy:7.2f})  radius {radius:.2f}")
    if piv:
        print(f"arrow-sweep pivot   ({piv[0]:7.2f},{piv[1]:7.2f})  from {n} frames"
              f"   offset {np.hypot(piv[0]-cx, piv[1]-cy):.2f} px")
    ang, s, ref = track(clip, which)
    print(f"reference angle {ref:.1f} deg; {(s>=2.0).sum()}/{len(s)} frames "
          f"read off-reference")
