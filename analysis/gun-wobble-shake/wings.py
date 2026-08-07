# Track three patches — left wing, right wing, tail/fuselage — to decompose the
# firing wobble into roll (wing dy anti-correlated) vs heave (correlated) and to
# size the amplitude away from the rotation centre. Same registration as track.py.
import os

import imageio_ffmpeg as iio
import numpy as np

from track import FIRE, IDLE, register, stats

VIDEO = r"Z:\CSVM\OriginalScreenshots\Videos\Gun Wobble and animation.mp4"
OUT = os.path.join(".scratch", "gun-wobble")
X0 = 640
PATCHES = {
    "lwing": (slice(455, 520), slice(370, 500)),
    "rwing": (slice(455, 520), slice(780, 910)),
    "tail": (slice(500, 600), slice(590, 690)),
}


def main():
    reader = iio.read_frames(VIDEO)
    meta = next(reader)
    w, h = meta["size"]
    fps = meta["fps"]
    wins = {k: np.outer(np.hanning(p[0].stop - p[0].start),
                        np.hanning(p[1].stop - p[1].start)).astype(np.float32)
            for k, p in PATCHES.items()}
    prev = {}
    series = {k: {"dx": [0.0], "dy": [0.0], "pk": [1.0]} for k in PATCHES}
    for raw in reader:
        g = np.frombuffer(raw, dtype=np.uint8).reshape(h, w, 3)[:, X0:X0 + 1280]
        for k, p in PATCHES.items():
            patch = g[p].astype(np.float32).mean(axis=2)
            if k in prev:
                dx, dy, pk = register(prev[k], patch, wins[k], ms=6)
                series[k]["dx"].append(dx); series[k]["dy"].append(dy); series[k]["pk"].append(pk)
            prev[k] = patch

    out = {}
    for k, s in series.items():
        for axis in ("dx", "dy"):
            d = np.asarray(s[axis])
            out[f"{k}_{axis}"] = d
            frms, fflip, _ = stats(d, *FIRE)
            irms = np.sqrt(np.mean([stats(d, lo, hi)[0] ** 2 for lo, hi in IDLE]))
            print(f"{k} {axis}: fire rms {frms:.3f}px/f (flip {fflip:.2f})  idle {irms:.3f}  "
                  f"x{frms / (irms + 1e-9):.1f}")
    np.savez(os.path.join(OUT, "series_wings.npz"), fps=fps, **out)

    lo, hi = FIRE
    for axis in ("dy", "dx"):
        a = out[f"lwing_{axis}"][lo:hi]
        b = out[f"rwing_{axis}"][lo:hi]
        r = float(np.corrcoef(a, b)[0, 1])
        print(f"lwing-vs-rwing {axis} correlation in fire window: {r:+.2f}"
              f"  ({'roll-like (anti)' if r < -0.3 else 'heave-like (co)' if r > 0.3 else 'mixed'})")


if __name__ == "__main__":
    main()
