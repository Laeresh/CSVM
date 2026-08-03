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

CACHE = ".scratch/vidcal/cache"

# The pixel-locked 2026-07 set. 2,529 frames total (FINDINGS.md).
POOL = ["pitch", "roll", "yaw", "dive", "accel", "decel"]


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


def build():
    frames, n, s1, s2 = [], 0, None, None
    for s in POOL:
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
    np.save(f"{CACHE}/pool_med.npy", med)
    np.save(f"{CACHE}/pool_std.npy", std)
    print(f"pool_med/pool_std {med.shape} from {n} frames")
    return med


def check(med=None):
    """Every cached clip's panel offset against the median. Must be 0, 0."""
    import glob
    import os

    if med is None:
        med = np.load(f"{CACHE}/pool_med.npy")
    for p in sorted(glob.glob(f"{CACHE}/*_lum.npy")):
        s = os.path.basename(p)[: -len("_lum.npy")]
        a = np.load(p).astype(float)
        dx, dy, peak = _offset(np.median(a, 0), med)
        flag = "" if (dx, dy) == (0, 0) else "   <-- NOT pixel-locked"
        print(f"  {s:12s} dx={dx:+d} dy={dy:+d} peak={peak:.3f}{flag}")


if __name__ == "__main__":
    if "--check" in sys.argv:
        check()
    else:
        check(build())
