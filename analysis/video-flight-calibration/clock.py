"""Recover the sim-clock rate from the dials alone.

The identity is  dh/dt_sim = -V . sin(gamma).  Since |sin gamma| <= 1, the
measured ratio

    K(t) = |dh/dt_wall| / V          (both in ft/s)

is a *hard lower bound* on k = sim seconds per wall second, needing no attitude
measurement whatsoever - and it becomes an equality wherever the dive is
vertical. A vertical segment therefore reads k off directly.

Units: fd_speed 135 in the dynamics block is 302 mph, which fixes the game's
speed unit as m/s, so the speedometer and altimeter are honest conversions of
one metric unit system and no extra scale factor enters the identity.
"""
import numpy as np
from scipy.signal import savgol_filter

from decode import CACHE

CLIPS = ["dive", "pitch", "roll", "yaw", "accel", "decel"]
FT_PER_S_PER_MPH = 5280.0 / 3600.0     # 1.466667


def load(s):
    alt = np.load(f"{CACHE}/{s}_alt3.npy")
    mph = np.load(f"{CACHE}/{s}_mph2.npy")
    t = np.loadtxt(f"{CACHE}/{s}_pts.txt")
    n = min(len(alt), len(mph), len(t))
    return t[:n] - t[0], alt[:n], mph[:n]


def smooth(y, win, po=3):
    if win % 2 == 0:
        win += 1
    win = min(win, len(y) - (1 - len(y) % 2))
    if win < po + 2:
        return np.asarray(y, float)
    return savgol_filter(np.asarray(y, float), win, po)


def ratio(s, win=21):
    t, alt, mph = load(s)
    a = smooth(alt, win)
    v = smooth(mph, win)
    dh = np.gradient(a, t)                       # ft per wall second
    vf = np.maximum(v * FT_PER_S_PER_MPH, 1e-6)  # ft/s
    return t, a, v, dh, np.abs(dh) / vf


if __name__ == "__main__":
    print("clip   |  max K = |dh/dt_wall|/V   (a HARD lower bound on the clock factor k)")
    for s in CLIPS:
        t, a, v, dh, K = ratio(s)
        i = int(np.argmax(K))
        p99 = np.percentile(K, 99)
        print(f"{s:6s} | max {K.max():5.3f} at t={t[i]:5.2f}s (alt {a[i]:6.0f} ft, "
              f"V {v[i]:5.1f} mph, dh/dt {dh[i]:+7.1f} ft/s)   p99={p99:5.3f}")

    print()
    print("dive: profile through the steep part (K plateaus at k where the dive is vertical)")
    t, a, v, dh, K = ratio("dive")
    for j in range(0, len(t), 10):
        bar = "#" * int(round(K[j] * 30))
        print(f"  t={t[j]:5.2f}  alt={a[j]:6.0f}  V={v[j]:6.1f}mph  dh/dt={dh[j]:+7.1f}ft/s  "
              f"K={K[j]:5.3f} {bar}")
