# A/B the ENGINE's wobble against the original's measurement: register wing patches
# across the RunProbe frame sequences (.scratch/wobble fire_NN.png vs .scratch/wobble-idle
# idle_NN.png, 1280x720, dead-astern --stage=empty chase) with the same instrument that
# measured the original clip (track.register). Usage: python engine_check.py [fireDir idleDir]
import os
import sys

import numpy as np
from PIL import Image

from track import register, stats

PATCHES = {
    "lwing": (slice(455, 515), slice(470, 580)),
    "rwing": (slice(455, 515), slice(730, 840)),
}


def series(folder):
    files = sorted(f for f in os.listdir(folder) if f.endswith(".png"))
    frames = [np.asarray(Image.open(os.path.join(folder, f)).convert("L"), dtype=np.float32)
              for f in files]
    out = {}
    for name, p in PATCHES.items():
        win = np.outer(np.hanning(p[0].stop - p[0].start),
                       np.hanning(p[1].stop - p[1].start)).astype(np.float32)
        dxs, dys = [0.0], [0.0]
        for a, b in zip(frames, frames[1:]):
            dx, dy, _ = register(a[p], b[p], win, ms=6)
            dxs.append(dx)
            dys.append(dy)
        out[name] = (np.asarray(dxs), np.asarray(dys))
    return out, len(frames)


def main():
    fire_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(".scratch", "wobble")
    idle_dir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(".scratch", "wobble-idle")
    fire, nf = series(fire_dir)
    idle, ni = series(idle_dir)
    print(f"fire {nf} frames, idle {ni} frames")
    for name in PATCHES:
        for axis, i in (("dx", 0), ("dy", 1)):
            f = fire[name][i]
            q = idle[name][i]
            frms, _, _ = stats(f, 0, len(f))
            irms, _, _ = stats(q, 0, len(q))
            print(f"{name} {axis}: fire rms {frms:.3f}px/f  idle rms {irms:.3f}px/f  "
                  f"x{frms / (irms + 1e-9):.1f}")
    r = float(np.corrcoef(fire["lwing"][1][1:], fire["rwing"][1][1:])[0, 1])
    print(f"lwing-vs-rwing dy correlation while firing: {r:+.2f} "
          f"({'roll-like (anti)' if r < -0.3 else 'not roll-dominated'})")


if __name__ == "__main__":
    main()
