"""Resolve the altimeter's 1,000 ft ambiguity once per clip.

The long needle gives altitude mod 1,000 ft to about half a foot, and its
unwrapped track gives every *change* in altitude. The only unknown left is which
1,000 ft band the clip starts in - ten candidates, because the band must keep
the long needle's own reading intact.

Scoring each candidate against the short needle's angular profile across every
frame at once beats reading the short needle frame by frame: the short needle is
fat, sits in a thin annulus and gets crossed by the long needle's shaft, so any
single frame can be wrong while hundreds of frames cannot.
"""
import numpy as np

from decode import CACHE
from reader2 import Dial, LKRegistrar, DEG
from run2 import ALT_BANDS, SPD_BANDS, unwrap

NTH = 720


def resolve(short, verbose=True):
    P = np.load(f"{CACHE}/dial_affines.npy")
    a = np.load(f"{CACHE}/{short}_lum.npy")
    g0 = np.load(f"{CACHE}/{short}_shift.npy")
    rec = np.load(f"{CACHE}/{short}_dials2.npy")
    dA = Dial(P[0], ALT_BANDS)
    rA = LKRegistrar(P[0])
    n = len(a)
    shorts = np.zeros((n, NTH), np.float32)
    for i in range(n):
        dx, dy = rA.shift(a[i], float(g0[i, 0]), float(g0[i, 1]))
        shorts[i] = dA.profiles(a[i], dx, dy)["short"]

    altL = unwrap(rec[:, 0]) / 0.36            # continuous, correct mod 1000
    base = altL - altL[0]
    start_mod = altL[0] % 1000.0
    scores = []
    for k in range(10):
        a0 = start_mod + 1000.0 * k
        pred = ((a0 + base) * 0.036) % 360.0    # short needle: 360 deg / 10,000 ft
        b = np.round(pred / DEG).astype(int) % NTH
        # sum the profile in a small window around the prediction
        sc = 0.0
        for w in range(-3, 4):
            sc += shorts[np.arange(n), (b + w) % NTH].sum()
        scores.append(sc)
    scores = np.array(scores)
    k = int(np.argmax(scores))
    alt = start_mod + 1000.0 * k + base
    if verbose:
        order = np.argsort(scores)[::-1]
        rel = scores / max(scores.max(), 1e-9)
        top = ", ".join(f"k={j}:{rel[j]:.2f}" for j in order[:4])
        print(f"{short:6s} band k={k} -> alt {alt.min():7.0f}..{alt.max():7.0f} ft   "
              f"margin {rel[order[0]]/max(rel[order[1]],1e-9):.2f}x   [{top}]")
    np.save(f"{CACHE}/{short}_alt3.npy", alt)
    np.save(f"{CACHE}/{short}_shortprof.npy", shorts)
    return alt


if __name__ == "__main__":
    for s in ["pitch", "roll", "yaw", "dive", "accel", "decel", "cap01"]:
        resolve(s)
