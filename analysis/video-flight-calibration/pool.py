"""Rebuild the pooled median panel frame every other stage registers against.

The six 2026-07 clips are pixel-locked (phase correlation of their reference
frames gives dx = dy = 0), so their 2,529 frames pool into one median in which
the needles average away.  That median is the reference `fitdial`, `checkclip`,
`decode` and `reader2` all difference against.

It lives in the swept `.scratch/` cache, so this rebuilds it from the clips:

    python analysis/video-flight-calibration/extract.py
    python analysis/video-flight-calibration/pool.py
    python analysis/video-flight-calibration/fitdial.py

Only add a clip here after confirming it registers at dx = dy = 0 against the
existing median - `--check` reports that for every cached clip.  A clip that is
merely *decodable* (a later session normalised to game coords by `extract.py`)
does not need to pool; pooling more frames buys nothing once the needles are
already gone.
"""
import sys

import numpy as np

import hud as hudlib

CACHE = ".scratch/vidcal/cache"

# Which clips build each HUD's reference frame.
#
# COCKPIT: the pixel-locked 2026-07 set, 2,529 frames (FINDINGS.md). The needles
# average away because the panel art is identical in every frame of every clip.
#
# CHASE: the opposite reasoning gets to the same place. The HUD is a screen-space
# overlay, so it is the only thing that holds still while the sea, the horizon
# and the aircraft slide past - a median over a chase clip renders the
# instruments crisp and smears the world into a flat wash. Fewer frames are
# needed than the cockpit case because there is no shake to average out.
POOLS = {
    "cockpit": ["pitch", "roll", "yaw", "dive", "accel", "decel"],
    "chase": ["cap18", "cap10chase"],
}
POOL = POOLS["cockpit"]      # kept: callers that predate the HUD split


def _offset(a, b):
    """Phase-correlate two panel frames; returns (dx, dy, peak)."""
    A = (a - a.mean()) / (a.std() + 1e-9)
    B = (b - b.mean()) / (b.std() + 1e-9)
    R = np.fft.rfft2(A) * np.conj(np.fft.rfft2(B))
    R /= np.abs(R) + 1e-9
    c = np.fft.irfft2(R, A.shape)
    i = np.unravel_index(np.argmax(c), c.shape)
    dy = i[0] - A.shape[0] if i[0] > A.shape[0] // 2 else i[0]
    dx = i[1] - A.shape[1] if i[1] > A.shape[1] // 2 else i[1]
    return int(dx), int(dy), float(c.max())


def build(hud=hudlib.DEFAULT_HUD):
    frames, n, s1, s2 = [], 0, None, None
    for s in POOLS[hud]:
        a = np.load(f"{CACHE}/{s}_lum.npy")
        frames.append(a)
        f = a.astype(np.float64)
        # streamed sum / sum-of-squares: the stacked float array would be ~5 GB
        s1 = f.sum(0) if s1 is None else s1 + f.sum(0)
        s2 = (f * f).sum(0) if s2 is None else s2 + (f * f).sum(0)
        n += len(a)
        print(f"  {s:12s} {len(a):5d} frames")
    med = np.median(np.concatenate(frames), axis=0)
    # per-pixel spread: high where a needle sweeps, ~0 on the painted panel
    std = np.sqrt(np.maximum(s2 / n - (s1 / n) ** 2, 0.0))
    np.save(hudlib.med_path(hud, CACHE), med)
    np.save(hudlib.std_path(hud, CACHE), std)
    print(f"{hudlib.med_path(hud, CACHE)} {med.shape} from {n} frames")
    return med


def check(med=None, hud=hudlib.DEFAULT_HUD):
    """Every cached clip's panel offset against the median. Must be 0, 0.

    Only clips of the SAME HUD are comparable - a chase clip's ROI is a
    different size and shape, so it is skipped rather than reported as
    misregistered."""
    import glob
    import os

    from extract import clip_hud

    if med is None:
        med = np.load(hudlib.med_path(hud, CACHE))
    for p in sorted(glob.glob(f"{CACHE}/*_lum.npy")):
        s = os.path.basename(p)[: -len("_lum.npy")]
        try:
            if clip_hud(s)[1] != hud:
                continue
        except KeyError:
            continue        # a region cache or a clip no longer in CLIPS
        a = np.load(p).astype(float)
        if a.shape[1:] != med.shape:
            print(f"  {s:12s} SKIPPED - ROI {a.shape[1:]} != median {med.shape}")
            continue
        dx, dy, peak = _offset(np.median(a, 0), med)
        flag = "" if (dx, dy) == (0, 0) else "   <-- NOT pixel-locked"
        print(f"  {s:12s} dx={dx:+d} dy={dy:+d} peak={peak:.3f}{flag}")


if __name__ == "__main__":
    which = next((a.split("=", 1)[1] for a in sys.argv[1:]
                  if a.startswith("--hud=")), hudlib.DEFAULT_HUD)
    if "--check" in sys.argv:
        check(hud=which)
    else:
        check(build(which), which)
