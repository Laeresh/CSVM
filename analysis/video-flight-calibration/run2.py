"""Second-pass decode of both dials over every clip."""
import numpy as np

from decode import CACHE
from reader2 import Dial, LKRegistrar, circ_centroid, DEG

CLIPS = ["pitch", "roll", "yaw", "dive", "accel", "decel"]
ALT_BANDS = {"long": (0.355, 0.50), "short": (0.15, 0.30)}
SPD_BANDS = {"needle": (0.18, 0.45)}


def unwrap(a):
    a = np.asarray(a, float)
    out = a.copy()
    acc = 0.0
    for i in range(1, len(a)):
        d = a[i] - a[i - 1]
        if d > 180:
            acc -= 360
        elif d < -180:
            acc += 360
        out[i] = a[i] + acc
    return out


def main():
    P = np.load(f"{CACHE}/dial_affines.npy")
    dA = Dial(P[0], ALT_BANDS)
    dS = Dial(P[1], SPD_BANDS)
    rA = LKRegistrar(P[0])
    rS = LKRegistrar(P[1])
    for s in CLIPS:
        a = np.load(f"{CACHE}/{s}_lum.npy")
        g0 = np.load(f"{CACHE}/{s}_shift.npy")
        n = len(a)
        rec = np.zeros((n, 8), np.float32)
        for i in range(n):
            dxa, dya = rA.shift(a[i], float(g0[i, 0]), float(g0[i, 1]))
            dxs, dys = rS.shift(a[i], float(g0[i, 0]), float(g0[i, 1]))
            pa = dA.profiles(a[i], dxa, dya)
            ps = dS.profiles(a[i], dxs, dys)
            pl = pa["long"]
            il = int(np.argmax(pl))
            cl, sl = circ_centroid(pl, il)
            psh = pa["short"].copy()
            psh[np.arange(int(round(cl)) - 16, int(round(cl)) + 17) % len(psh)] = -1e9
            ish = int(np.argmax(psh))
            cs, ss = circ_centroid(psh, ish, half=12)
            pn = ps["needle"]
            iN = int(np.argmax(pn))
            cn, sn = circ_centroid(pn, iN)
            rec[i] = (cl * DEG, cs * DEG, cn * DEG, sl, ss, sn, dxa, dya)
        np.save(f"{CACHE}/{s}_dials2.npy", rec)

        thL = unwrap(rec[:, 0])
        altL = thL / 0.36
        altS = rec[:, 1] / 0.036
        anchor = np.median(altS - altL)
        alt = altL + round(anchor / 1000.0) * 1000.0
        mph = unwrap(rec[:, 2]) / 0.72
        np.save(f"{CACHE}/{s}_alt2.npy", alt)
        np.save(f"{CACHE}/{s}_mph2.npy", mph)
        print(f"{s:6s} n={n:4d} | alt {alt.min():7.0f}..{alt.max():7.0f} ft "
              f"(d2 sd {np.diff(alt,2).std():6.2f}) | mph {mph.min():6.1f}..{mph.max():6.1f} "
              f"(d2 sd {np.diff(mph,2).std():5.2f}) | L-S sd {(alt-altS).std():5.0f} ft | "
              f"shake dx {rec[:,6].min():+5.2f}..{rec[:,6].max():+5.2f}")


if __name__ == "__main__":
    main()
