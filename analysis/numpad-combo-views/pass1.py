"""CAP-08 pass 1: decode the combo sweep into per-frame camera-state features.

BL-150(d) asks what ADDING a second numpad key does while the first is still
held - blend, replace, or ignore.  The rig (`analysis/capture-rigs/
NumpadComboSweep.ahk`) staggers every step so the answer is visible as a
*transition*, and it logs every key edge, so nothing here has to infer event
boundaries from motion.  What this pass must supply is (i) the video->log time
offset and (ii) a per-frame quantity that says where the camera is.

The camera is aircraft-relative, so the world background is noise: between two
steps 60 s apart the sea, clouds and sun have all moved.  The *aircraft* has
not.  It is the only strongly red thing in frame, so a redness mask segments it
cleanly against sky and sea, and the mask's moments (area, centroid, principal
axis, elongation) are a background-free proxy for the camera's position on the
view sphere.  Two frames showing the same silhouette are the same camera view
regardless of where in the world they were shot.

Kept deliberately cheap: one decode at 1/8 scale, features only.  Full-res
stills for the actual reading come from pass 2 at the timestamps this picks.

Outputs (.scratch/cap08/):
  feat.npy   float64 [n, 10]  see FEAT_COLS
  pts.npy    float64 [n]      presentation time, seconds (VFR-safe)
  mask/      uint8            downscaled masks, for silhouette comparison
"""
import os
import re
import subprocess
import sys

import imageio_ffmpeg as iio
import numpy as np

VIDEO = "Z:/CSVM/playtest/CAP-08/CAP-08.mp4"
OUT = os.environ.get("CAP08_OUT", ".scratch/cap08")

# 1/4 of 2560x1440.  The aircraft masks to a few thousand px here, enough that
# the principal axis and elongation are stable frame to frame; every mask still
# fits in memory packed (28.8 kB/frame).
S = 2
W, H = 320 * S, 180 * S

# HUD furniture to exclude before segmenting, quoted in 320x180 units and
# scaled by S.  The gauges are strongly coloured (red needles, red arcs) and
# would otherwise dominate the red mask; the FPS overlay and the letterbox are
# flat but cheap to drop.
HUD_BOXES = [(x0 * S, y0 * S, x1 * S, y1 * S) for x0, y0, x1, y1 in [
    (0, 0, 320, 10),      # top strip: FPS overlay + compass tape
    (28, 76, 56, 135),    # left gauge stack (ROCKETS / ALT / ADI)
    (196, 76, 224, 122),  # right gauge stack (GUNS / MPH)
    (0, 168, 320, 180),   # bottom letterbox
]]

FEAT_COLS = ["area", "cx", "cy", "m20", "m02", "m11", "orient", "elong",
             "motion", "meanred"]


def pts_times(path):
    """Presentation timestamps in decode order.

    The captures are variable-frame-rate (FINDINGS.md), so frame/fps is wrong
    by up to +-50% on individual intervals and must never be used to place an
    event.  showinfo prints the real pts for every frame ffmpeg hands over,
    in the same order read_frames yields them.
    """
    ff = iio.get_ffmpeg_exe()
    p = subprocess.run(
        [ff, "-hide_banner", "-i", path, "-map", "0:v:0",
         "-vf", "showinfo", "-f", "null", "-"],
        capture_output=True, text=True, errors="replace")
    t = [float(m) for m in
         re.findall(r"pts_time:([0-9.]+)", p.stderr)]
    if not t:
        raise SystemExit("no pts_time lines from showinfo")
    return np.asarray(t)


def hud_mask():
    m = np.ones((H, W), bool)
    for x0, y0, x1, y1 in HUD_BOXES:
        m[y0:y1, x0:x1] = False
    return m


def features(rgb, prev, keep):
    """Segment the aircraft and reduce it to shape moments.

    'Redness' is r - max(g, b): positive only where the surface is genuinely
    red, which at this dusk lighting is the airframe and nothing else.  The sea
    and sky are blue-dominant (negative), clouds are neutral (~0), and the sun
    glare is neutral-to-warm but saturates all three channels equally.
    """
    r = rgb[:, :, 0].astype(np.int16)
    g = rgb[:, :, 1].astype(np.int16)
    b = rgb[:, :, 2].astype(np.int16)
    red = r - np.maximum(g, b)
    m = (red > RED_T) & keep

    lum = (0.299 * r + 0.587 * g + 0.114 * b)
    motion = float(np.abs(lum - prev)[keep].mean()) if prev is not None else 0.0

    n = int(m.sum())
    if n < 12:                      # aircraft lost (eclipsed by cloud, or gone)
        return np.array([n, np.nan, np.nan, np.nan, np.nan, np.nan,
                         np.nan, np.nan, motion, 0.0]), m, lum

    ys, xs = np.nonzero(m)
    cx, cy = xs.mean(), ys.mean()
    dx, dy = xs - cx, ys - cy
    m20, m02, m11 = (dx * dx).mean(), (dy * dy).mean(), (dx * dy).mean()
    orient = 0.5 * np.arctan2(2 * m11, m20 - m02)
    tr, det = m20 + m02, m20 * m02 - m11 * m11
    disc = max(tr * tr / 4 - det, 0.0) ** 0.5
    l1, l2 = tr / 2 + disc, max(tr / 2 - disc, 1e-9)
    elong = (l1 / l2) ** 0.5
    return (np.array([n, cx, cy, m20, m02, m11, orient, elong,
                      motion, float(red[m].mean())]), m, lum)


RED_T = 18      # counts of r over max(g,b); see redness_probe() below


def redness_probe(path, at=(20.0, 60.0, 100.0)):
    """Sanity-check the threshold before trusting 4000 frames of masks."""
    ff = iio.get_ffmpeg_exe()
    for t in at:
        p = subprocess.run(
            [ff, "-v", "error", "-ss", str(t), "-i", path, "-frames:v", "1",
             "-vf", f"scale={W}:{H}", "-f", "rawvideo", "-pix_fmt", "rgb24",
             "-"], capture_output=True)
        fr = np.frombuffer(p.stdout, np.uint8).reshape(H, W, 3)
        r = fr[:, :, 0].astype(np.int16)
        red = r - np.maximum(fr[:, :, 1], fr[:, :, 2]).astype(np.int16)
        keep = hud_mask()
        for th in (8, 12, 18, 25, 35):
            print(f"  t={t:6.1f} thresh={th:3d} px={int(((red>th)&keep).sum()):6d}")
        print(f"  t={t:6.1f} red max={red[keep].max()} "
              f"p99.9={np.percentile(red[keep],99.9):.1f}")


def main():
    os.makedirs(OUT, exist_ok=True)
    if "--probe" in sys.argv:
        redness_probe(VIDEO)
        return

    t = pts_times(VIDEO)
    keep = hud_mask()

    # imageio_ffmpeg 0.6 has no `size=`; scale in the filter chain instead and
    # reshape to (H, W) ourselves rather than trusting meta["size"], which
    # still reports the source geometry.
    g = iio.read_frames(VIDEO, pix_fmt="rgb24",
                        output_params=["-vf", f"scale={W}:{H}"])
    meta = next(g)
    feats, masks, prev = [], [], None
    for buf in g:
        fr = np.frombuffer(buf, np.uint8).reshape(H, W, 3)
        f, m, lum = features(fr, prev, keep)
        feats.append(f)
        masks.append(np.packbits(m))
        prev = lum
    g.close()

    F = np.stack(feats)
    n = min(len(F), len(t))
    np.save(f"{OUT}/feat.npy", F[:n])
    np.save(f"{OUT}/pts.npy", t[:n])
    np.save(f"{OUT}/masks.npy", np.stack(masks)[:n])
    print(f"frames decoded={len(F)} pts={len(t)} kept={n}")
    print(f"pts span {t[0]:.3f}..{t[n-1]:.3f} s  "
          f"median dt={np.median(np.diff(t[:n]))*1000:.2f} ms  "
          f"min={np.diff(t[:n]).min()*1000:.2f} max={np.diff(t[:n]).max()*1000:.2f}")
    ok = np.isfinite(F[:n, 1])
    print(f"aircraft segmented in {ok.sum()}/{n} frames "
          f"({100*ok.mean():.1f}%), median area={np.nanmedian(F[:n,0]):.0f} px")


if __name__ == "__main__":
    main()
