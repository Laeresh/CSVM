"""Per-frame needle decoding for the altimeter and speedometer.

Scales measured off the original textures' own tick rings (7.2 deg minor tick on
both faces): speedometer 0.72 deg/mph, altimeter 36 deg per numeral, i.e. 360
deg per 1,000 ft on the long needle and per 10,000 ft on the short one.

Each frame is illumination-matched to the pooled median inside the dial before
differencing, because the night-scene panel lighting swings with attitude.
"""
import numpy as np

from polar import polar_grid, bilin, CACHE

NTH = 720
DEG = 360.0 / NTH

# needle radial extents, measured from the residual radial profile
ALT_LONG = (0.355, 0.50)
ALT_SHORT = (0.15, 0.30)
SPD_NEEDLE = (0.18, 0.45)
FACE_BAND = (0.12, 0.58)   # illumination-match band


def circ_centroid(prof, i0, half=24):
    """Circular centroid of a peak at bin i0, over +-half bins."""
    n = len(prof)
    idx = (np.arange(i0 - half, i0 + half + 1)) % n
    w = np.clip(prof[idx], 0, None)
    if w.sum() <= 0:
        return np.nan, 0.0
    off = np.arange(-half, half + 1)
    return (i0 + np.sum(off * w) / w.sum()) % n, float(w.sum())


class DialReader:
    def __init__(self, affine, med):
        self.affine = np.asarray(affine, float)
        gx, gy, th, r = polar_grid(self.affine, r0=0.10, r1=1.00, nth=NTH)
        self.gx, self.gy, self.r = gx, gy, r
        self.med = bilin(med.astype(np.float32), gx, gy)
        self.fb = (r >= FACE_BAND[0]) & (r <= FACE_BAND[1])

    def residual(self, img, dx=0.0, dy=0.0):
        p = bilin(img.astype(np.float32), self.gx + dx, self.gy + dy)
        A = p[:, self.fb].ravel()
        B = self.med[:, self.fb].ravel()
        ok = np.isfinite(A) & np.isfinite(B)
        # robust gain/offset so panel-lighting swings do not fake a needle
        if ok.sum() > 50:
            Bm = B[ok]
            Am = A[ok]
            lo, hi = np.percentile(Bm, [20, 80])
            sel = (Bm >= lo) & (Bm <= hi)
            if sel.sum() > 30 and Bm[sel].std() > 1e-3:
                g, o = np.polyfit(Bm[sel], Am[sel], 1)
            else:
                g, o = 1.0, float(np.median(Am - Bm))
        else:
            g, o = 1.0, 0.0
        return p - (g * self.med + o)

    def band_profile(self, res, lo, hi):
        m = (self.r >= lo) & (self.r <= hi)
        return np.nansum(np.clip(res[:, m], 0, None), axis=1)


def read_speed(rd, img, dx=0.0, dy=0.0):
    res = rd.residual(img, dx, dy)
    prof = rd.band_profile(res, *SPD_NEEDLE)
    i = int(np.argmax(prof))
    c, s = circ_centroid(prof, i)
    return (c * DEG) % 360.0, s


def read_alt(rd, img, dx=0.0, dy=0.0):
    res = rd.residual(img, dx, dy)
    pl = rd.band_profile(res, *ALT_LONG)
    il = int(np.argmax(pl))
    cl, sl = circ_centroid(pl, il)
    ps = rd.band_profile(res, *ALT_SHORT)
    # blank the long needle's angular window before hunting the short one
    n = len(ps)
    blank = (np.arange(int(round(cl)) - 18, int(round(cl)) + 19)) % n
    ps2 = ps.copy()
    ps2[blank] = 0
    isw = int(np.argmax(ps2))
    cs, ss = circ_centroid(ps2, isw, half=18)
    return (cl * DEG) % 360.0, sl, (cs * DEG) % 360.0, ss


def unwrap_deg(a):
    """Unwrap a 0..360 series into a continuous one (shortest step)."""
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


def run(short):
    a = np.load(f"{CACHE}/{short}_lum.npy")
    sft = np.load(f"{CACHE}/{short}_shift.npy")
    med = np.load(f"{CACHE}/pool_med.npy")
    P = np.load(f"{CACHE}/dial_affines.npy")
    ra = DialReader(P[0], med)
    rs = DialReader(P[1], med)
    n = len(a)
    out = np.zeros((n, 6), np.float32)
    for i in range(n):
        dx, dy = float(sft[i, 0]), float(sft[i, 1])
        cl, sl, cs, ss = read_alt(ra, a[i], dx, dy)
        sp, sps = read_speed(rs, a[i], dx, dy)
        out[i] = (cl, sl, cs, ss, sp, sps)
    np.save(f"{CACHE}/{short}_dials.npy", out)
    return out


if __name__ == "__main__":
    for s in ["pitch", "roll", "yaw", "dive", "accel", "decel"]:
        o = run(s)
        alt_long, alt_short, spd = o[:, 0], o[:, 2], o[:, 4]
        alt_k = alt_short / 0.036           # ft from the short needle alone
        print(f"{s:6s} n={len(o):4d}  spd {spd.min():6.1f}..{spd.max():6.1f} deg "
              f"({spd.min()/0.72:5.0f}..{spd.max()/0.72:5.0f} mph)   "
              f"alt(short) {alt_k.min():7.0f}..{alt_k.max():7.0f} ft   "
              f"strength spd={o[:,5].mean():5.0f} altL={o[:,1].mean():5.0f} altS={o[:,3].mean():5.0f}")
