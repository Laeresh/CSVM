"""CAP-08 pass 4: pull the settled full-res stills the verdict is read off.

The moments in decide.py say how much changed; they cannot say what it changed
*to*.  A view is identified by which surface of the aircraft is visible and
where the nose points (that is exactly how BL-150(a)'s table was derived), and
that has to be looked at.  So this writes one strip per step - base | A alone |
A+B held - at the settled instants, plus a reference sheet of all eight keys
held alone.

Times come from the fitted offset; -0.4 s before the next edge puts the sample
inside the settled tail of each window (a slew is ~0.3 s, the shortest window
is 2.5 s).  `-ss` goes after `-i` for exact-frame seeking.
"""
import os
import subprocess

import imageio_ffmpeg as iio
import numpy as np

import sync

OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")
VIDEO = "Z:/CSVM/playtest/CAP-08/CAP-08.mp4"
STILLS = f"{OUT}/stills"
LEAD = 0.4          # s before the edge = inside the settled tail
# Crop to the aircraft's neighbourhood: the gauges and the empty sky waste
# most of a 2560x1440 frame when several stills are read side by side.
CROP = "crop=1500:820:530:340,scale=500:-1"


def grab(t, path, crop=CROP):
    ff = iio.get_ffmpeg_exe()
    subprocess.run([ff, "-y", "-v", "error", "-i", VIDEO, "-ss", f"{t:.3f}",
                    "-frames:v", "1", "-vf", crop, "-q:v", "2", path],
                   check=True)


def main():
    os.makedirs(STILLS, exist_ok=True)
    off = float(np.load(f"{OUT}/offset.npy")[0])
    edges, steps = sync.load_log()
    ff = iio.get_ffmpeg_exe()

    plan = []
    for n, keys, t_step in steps:
        ds = [e for e in edges if e[0] > t_step and e[1] == "DOWN"]
        us = [e for e in edges if e[0] > t_step and e[1] == "UP"]
        tA, tB, tU = ds[0][0], ds[1][0], us[0][0]
        lab = "+".join(keys)
        plan += [(n, lab, "base", tA + off - LEAD),
                 (n, lab, "A" + keys[0], tB + off - LEAD),
                 (n, lab, "AB", tU + off - LEAD)]

    print("step  combo    window   video_t")
    for n, lab, w, t in plan:
        p = f"{STILLS}/s{n:02d}_{lab.replace('+','')}_{w}.png"
        grab(t, p)
        print(f"{n:3d}   {lab:7s} {w:6s}  {t:8.3f}  -> {os.path.basename(p)}")

    # one strip per step: base | A | AB
    for n, lab, _, _ in plan[::3]:
        tag = lab.replace("+", "")
        ins = []
        for w in ("base", "A" + lab.split("+")[0], "AB"):
            ins += ["-i", f"{STILLS}/s{n:02d}_{tag}_{w}.png"]
        subprocess.run([ff, "-y", "-v", "error", *ins, "-filter_complex",
                        "hstack=inputs=3", f"{STILLS}/strip{n:02d}_{tag}.png"],
                       check=True)
    print(f"\nwrote {len(plan)} stills and {len(steps)} strips to {STILLS}")


if __name__ == "__main__":
    main()
