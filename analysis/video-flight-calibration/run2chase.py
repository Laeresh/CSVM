"""run2.py's decode, for the chase HUD.

Same reader, same bands, same scales. Two differences from the cockpit path, both
because the chase HUD is a screen-space sprite rather than a dial on a 3-D panel:
it needs `dial_affines_chase.npy`, and there is no shake to subtract (the HUD is
pinned by construction — `checkclip`'s gate has nothing to test and `pool.py`
registers every chase clip at dx=dy=0), so the per-frame seed is (0, 0).

PTS, not frame/fps: these captures are VFR (FINDINGS.md's VFR trap).
"""
import subprocess
import sys

import imageio_ffmpeg as iio
import numpy as np

from decode import CACHE
from extract import VID, clip_hud
from reader2 import Dial, circ_centroid, DEG
from run2 import ALT_BANDS, SPD_BANDS, unwrap

DIAL_ORDER = ["altimeter", "speedometer", "horizonindicator", "gungauge", "rockets"]


def pts(short):
    """Presentation timestamps, in wall-clock seconds."""
    path, _ = clip_hud(short)
    out = subprocess.run(
        [iio.get_ffmpeg_exe(), "-i", f"{VID}/{path}", "-vf", "showinfo", "-f", "null", "-"],
        stderr=subprocess.PIPE, stdout=subprocess.DEVNULL, text=True).stderr
    return np.array([float(l.split("pts_time:")[1].split()[0])
                     for l in out.splitlines() if "pts_time:" in l])


def decode(short):
    P = np.load(f"{CACHE}/dial_affines_chase.npy")
    dA = Dial(P[DIAL_ORDER.index("altimeter")], ALT_BANDS, sfx="_chase")
    dS = Dial(P[DIAL_ORDER.index("speedometer")], SPD_BANDS, sfx="_chase")
    a = np.load(f"{CACHE}/{short}_lum.npy")
    n = len(a)
    rec = np.zeros((n, 8), np.float32)
    # No LK registration on the chase HUD: it is a screen-space sprite, pinned by
    # construction (pool.py registers every chase clip at dx=dy=0), and its dials sit
    # hard against the screen edges where LKRegistrar's padded ROI would run negative.
    for i in range(n):
        dxa = dya = dxs = dys = 0.0
        pa = dA.profiles(a[i], dxa, dya)
        ps = dS.profiles(a[i], dxs, dys)
        pl = pa["long"]
        cl, sl = circ_centroid(pl, int(np.argmax(pl)))
        psh = pa["short"].copy()
        psh[np.arange(int(round(cl)) - 16, int(round(cl)) + 17) % len(psh)] = -1e9
        cs, ss = circ_centroid(psh, int(np.argmax(psh)), half=12)
        pn = ps["needle"]
        cn, sn = circ_centroid(pn, int(np.argmax(pn)))
        rec[i] = (cl * DEG, cs * DEG, cn * DEG, sl, ss, sn, dxa, dya)

    thL = unwrap(rec[:, 0])
    altL = thL / 0.36                      # 360 deg per 1,000 ft
    altS = rec[:, 1] / 0.036               # 360 deg per 10,000 ft
    alt = altL + round(np.median(altS - altL) / 1000.0) * 1000.0
    mph = unwrap(rec[:, 2]) / 0.72         # 0.72 deg/mph
    t = pts(short)[:n]
    np.save(f"{CACHE}/{short}_alt2.npy", alt)
    np.save(f"{CACHE}/{short}_mph2.npy", mph)
    np.save(f"{CACHE}/{short}_pts.npy", t)
    print(f"{short} n={n} | alt {alt.min():.0f}..{alt.max():.0f} ft "
          f"(d2 sd {np.diff(alt,2).std():.2f}) | mph {mph.min():.1f}..{mph.max():.1f} "
          f"(d2 sd {np.diff(mph,2).std():.2f}) | L-S sd {(alt-altS).std():.0f} ft")
    return t, alt, mph


if __name__ == "__main__":
    for s in sys.argv[1:] or ["cap10chase"]:
        t, alt, mph = decode(s)
        # climb rate over a centred 1 s window, on PTS
        for i in range(0, len(t), 15):
            j0 = np.searchsorted(t, t[i] - 0.5)
            j1 = min(len(t) - 1, np.searchsorted(t, t[i] + 0.5))
            cr = (alt[j1] - alt[j0]) / max(t[j1] - t[j0], 1e-6) * 60
            print(f"  t={t[i]:6.2f}s  alt={alt[i]:7.0f} ft  mph={mph[i]:6.1f}  climb={cr:+8.0f} ft/min")
