"""Decode both dials on every clip, registering each dial locally per frame."""
import numpy as np

from decode import CACHE, DialReader, read_alt, read_speed, unwrap_deg
from localreg import LocalRegistrar

CLIPS = ["pitch", "roll", "yaw", "dive", "accel", "decel"]


def box_of(p, r=0.95):
    cx, cy, a, b, c, d = p
    xs = [cx + a * r * sx + b * (-r * sy) for sx in (-1, 1) for sy in (-1, 1)]
    ys = [cy + c * r * sx + d * (-r * sy) for sx in (-1, 1) for sy in (-1, 1)]
    return (int(np.floor(min(xs))), int(np.floor(min(ys))),
            int(np.ceil(max(xs))), int(np.ceil(max(ys))))


def main():
    med = np.load(f"{CACHE}/pool_med.npy")
    P = np.load(f"{CACHE}/dial_affines.npy")
    regA = LocalRegistrar(box_of(P[0]))
    regS = LocalRegistrar(box_of(P[1]))
    rdA = DialReader(P[0], med)
    rdS = DialReader(P[1], med)
    for s in CLIPS:
        a = np.load(f"{CACHE}/{s}_lum.npy")
        n = len(a)
        out = np.zeros((n, 12), np.float32)
        for i in range(n):
            dxa, dya, pa = regA.shift(a[i])
            dxs, dys, ps = regS.shift(a[i])
            cl, sl, cs, ss = read_alt(rdA, a[i], dxa, dya)
            sp, sps = read_speed(rdS, a[i], dxs, dys)
            out[i] = (cl, sl, cs, ss, sp, sps, dxa, dya, dxs, dys, pa, ps)
        np.save(f"{CACHE}/{s}_dials.npy", out)

        thL, thS, spd = out[:, 0], out[:, 2], out[:, 4]
        altS = thS / 0.036
        altL = thL / 0.36
        k = np.round((altS - altL) / 1000.0)
        alt = altL + 1000 * k
        np.save(f"{CACHE}/{s}_alt.npy", alt)
        mph = unwrap_deg(spd) / 0.72
        np.save(f"{CACHE}/{s}_mph.npy", mph)
        print(f"{s:6s} n={n:4d} | shift alt dx {out[:,6].min():+5.2f}..{out[:,6].max():+5.2f} "
              f"dy {out[:,7].min():+5.2f}..{out[:,7].max():+5.2f} | "
              f"alt {alt.min():6.0f}..{alt.max():6.0f} ft (d2 sd {np.diff(alt,2).std():5.2f}) | "
              f"mph {mph.min():5.1f}..{mph.max():5.1f} (d2 sd {np.diff(mph,2).std():4.2f}) | "
              f"L-S resid sd {(alt-altS).std():5.1f} ft")


if __name__ == "__main__":
    main()
