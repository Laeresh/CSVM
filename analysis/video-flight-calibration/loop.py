"""The full loop in a cockpit clip: slowest point, apex, and 360 deg duration.

Produces the `loop from 300 mph level, held full pull` row in FINDINGS.md, and with it the
`zoom-climb` probe's targets. Run after extract/pool/fitdial/shake/run2/anchor, e.g.

    python analysis/video-flight-calibration/loop.py pitch

Two things this exists to keep straight, both of which were got wrong first:

* The SLOWEST POINT AND THE APEX ARE DIFFERENT MOMENTS, 2.2 s and 63 mph apart on the
  `pitch` clip. Speed bottoms where thrust - drag = g*sin(gamma), which is still in the
  climb; altitude peaks later, at gamma = 0. Quoting one as the other is a 60 mph error,
  and "what speed does it come out of the loop at" is a third number again (239.7 mph).
* TIMES ARE MEASURED FROM THE PULL, not from frame 0 - the clip opens with several
  seconds of level cruise, and our own zoom-climb probe starts pulling at t = 0.

Time is from PTS, never frame/fps: these captures are variable-frame-rate and individual
intervals are wrong by up to 50%. Durations are reported in SIM seconds as well as wall,
via the clock factor k (FINDINGS.md; verification DET-11) - the original's sim clock runs
fast and a wall duration is not a sim duration.
"""
import os
import subprocess
import sys

import imageio_ffmpeg as iio
import numpy as np

CACHE = ".scratch/vidcal/cache"
K = 1.390          # sim seconds per wall second, +/- 0.021 (FINDINGS.md)


def pts_seconds(path):
    """Presentation timestamps, one per decoded frame."""
    out = subprocess.run(
        [iio.get_ffmpeg_exe(), "-i", path, "-vf", "showinfo", "-f", "null", "-"],
        stderr=subprocess.PIPE, stdout=subprocess.DEVNULL, text=True, errors="replace",
    ).stderr
    return np.array([float(tok.split(":", 1)[1])
                     for line in out.splitlines() for tok in line.split()
                     if tok.startswith("pts_time:")])


def report(short, path):
    alt = np.load(f"{CACHE}/{short}_alt2.npy")
    mph = np.load(f"{CACHE}/{short}_mph2.npy")
    t = pts_seconds(path)
    n = min(len(alt), len(mph), len(t))
    alt, mph, t = alt[:n], mph[:n], t[:n] - t[0]

    # Cruise reference from the pre-pull hold; its sd is what sets the pull threshold, so
    # a clip that starts already manoeuvring will show it as an implausibly wide band.
    cruise = mph[:100]
    print(f"{short}: {n} frames, {t[-1]:.2f} s wall / {t[-1] * K:.2f} s sim")
    print(f"  cruise {cruise.mean():.2f} +/- {cruise.std():.2f} mph at {alt[:100].mean():.0f} ft")

    i_pull = int(np.argmax(mph < cruise.mean() - 3 * cruise.std()))
    i_slow = int(np.argmin(mph))
    i_apex = int(np.argmax(alt))
    tail = np.arange(i_apex, n)
    i_bot = int(tail[np.argmin(alt[i_apex:])])

    print(f"  pull   frame {i_pull:3d}  t=+{t[i_pull]:.2f} s wall   {mph[i_pull]:.1f} mph")
    for label, i in (("slowest", i_slow), ("apex", i_apex), ("bottom", i_bot)):
        print(f"  {label:8s} frame {i:3d}  +{t[i] - t[i_pull]:5.2f} wall / "
              f"{(t[i] - t[i_pull]) * K:5.2f} SIM after the pull   "
              f"{mph[i]:6.1f} mph   {alt[i] - alt[i_pull]:+6.0f} ft")
    dt = (t[i_bot] - t[i_pull]) * K
    print(f"  loop 360deg {dt:.2f} sim s => mean {360 / dt:.2f} deg/sim-s "
          f"(compare the BANKED sustained turn's 18.95 - see FINDINGS.md)")


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import extract  # noqa: E402  (same-directory module, path set above)

    for short in sys.argv[1:] or ["pitch"]:
        fname, _ = extract.clip_hud(short)
        p = os.path.join(extract.VID, fname)
        report(short, p if os.path.exists(p) else fname)
