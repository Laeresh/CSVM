"""Dial reading, second pass.

Three changes over the first pass, each fixing something the dive clip exposed:

* Registration is per dial and gradient-based (Lucas-Kanade with a gain/offset
  term), not a masked phase correlation. Multiplying both images by the same
  binary static-mask put a spurious peak at zero lag, which is why the first
  pass reported no shake on a clip that visibly shakes.
* The angular profile is high-passed before the peak hunt. A lit LOW ALT or
  STALL window is a broad bright blob sitting in the needle band; a needle is a
  ~7 deg ridge. Subtracting a rolling median over +-25 deg keeps only ridges.
* Altitude comes from the *unwrapped* long needle with a single robust anchor
  from the short needle, instead of re-picking the 1,000 ft band every frame.
"""
import numpy as np

from polar import bilin
from decode import CACHE

NTH = 720
DEG = 360.0 / NTH


# ---------------------------------------------------------------- registration
class LKRegistrar:
    """Sub-pixel translation of one dial against the pooled median."""

    def __init__(self, affine, r_out=1.02, pad=6, sfx=""):
        # `sfx` selects the pooled reference: "" is the cockpit pool (the historical
        # default, so every existing caller and published number is untouched),
        # "_chase" the chase one. Same convention as pool.py / fitdial.py.
        cx, cy, a, b, c, d = affine
        xs = [cx + a * sx + b * (-sy) for sx in (-r_out, r_out) for sy in (-r_out, r_out)]
        ys = [cy + c * sx + d * (-sy) for sx in (-r_out, r_out) for sy in (-r_out, r_out)]
        self.x0 = int(np.floor(min(xs))) - pad
        self.y0 = int(np.floor(min(ys))) - pad
        self.x1 = int(np.ceil(max(xs))) + pad
        self.y1 = int(np.ceil(max(ys))) + pad
        med = np.load(f"{CACHE}/pool_med{sfx}.npy").astype(np.float32)
        sd = np.load(f"{CACHE}/pool_std{sfx}.npy").astype(np.float32)
        self.ref = med[self.y0:self.y1, self.x0:self.x1]
        s = sd[self.y0:self.y1, self.x0:self.x1]
        # weight: trust pixels that hold still, and that carry a gradient
        gy, gx = np.gradient(self.ref)
        gmag = np.hypot(gx, gy)
        self.w = (1.0 / (1.0 + (s / 4.0) ** 2)) * np.clip(gmag, 0, 40)
        self.w /= self.w.sum()
        self.gx, self.gy = gx, gy
        self.h, self.wd = self.ref.shape
        yy, xx = np.mgrid[0:self.h, 0:self.wd]
        self.xx = xx.astype(np.float32)
        self.yy = yy.astype(np.float32)

    def shift(self, frame, dx=0.0, dy=0.0, iters=12):
        sub = frame[self.y0 - 8:self.y1 + 8, self.x0 - 8:self.x1 + 8].astype(np.float32)
        for _ in range(iters):
            I = bilin(sub, self.xx + 8 + dx, self.yy + 8 + dy)
            ok = np.isfinite(I)
            if ok.sum() < 100:
                break
            w = np.where(ok, self.w, 0.0)
            Iv = np.where(ok, I, 0.0)
            # gain/offset: I ~= g*ref + o
            sw = w.sum()
            mI = (w * Iv).sum() / sw
            mR = (w * self.ref).sum() / sw
            vR = (w * (self.ref - mR) ** 2).sum() / sw
            g = ((w * (self.ref - mR) * (Iv - mI)).sum() / sw) / max(vR, 1e-6)
            g = float(np.clip(g, 0.2, 5.0))
            o = mI - g * mR
            r = Iv - (g * self.ref + o)
            # J = g * grad(ref)  (moving the frame is equivalent to moving the model)
            Jx = g * self.gx
            Jy = g * self.gy
            A = np.array([[(w * Jx * Jx).sum(), (w * Jx * Jy).sum()],
                          [(w * Jx * Jy).sum(), (w * Jy * Jy).sum()]])
            bb = -np.array([(w * Jx * r).sum(), (w * Jy * r).sum()])
            try:
                step = np.linalg.solve(A + np.eye(2) * 1e-6 * max(A[0, 0], 1e-6), bb)
            except np.linalg.LinAlgError:
                break
            step = np.clip(step, -2.0, 2.0)
            dx += float(step[0])
            dy += float(step[1])
            if abs(step[0]) < 1e-3 and abs(step[1]) < 1e-3:
                break
        return float(dx), float(dy)


# ------------------------------------------------------------------- unwrapping
def polar_xy(affine, r0, r1, nth=NTH, nr=64):
    cx, cy, a, b, c, d = affine
    th = np.arange(nth) * (2 * np.pi / nth)
    r = np.linspace(r0, r1, nr)
    TH, R = np.meshgrid(th, r, indexing="ij")
    u = R * np.sin(TH)
    v = R * np.cos(TH)
    return cx + a * u - b * v, cy + c * u - d * v, r


def hp_angular(prof, half=50):
    """Remove anything broader than +-half bins (25 deg) in theta."""
    n = len(prof)
    k = 2 * half + 1
    pad = np.concatenate([prof[-half:], prof, prof[:half]])
    base = np.array([np.median(pad[i:i + k]) for i in range(n)])
    return prof - base


def circ_centroid(prof, i0, half=14):
    n = len(prof)
    idx = np.arange(i0 - half, i0 + half + 1) % n
    w = np.clip(prof[idx], 0, None)
    if w.sum() <= 0:
        return float(i0), 0.0
    off = np.arange(-half, half + 1)
    return (i0 + np.sum(off * w) / w.sum()) % n, float(w.sum())


class Dial:
    def __init__(self, affine, bands, match=(0.12, 0.58), sfx=""):
        self.affine = np.asarray(affine, float)
        med = np.load(f"{CACHE}/pool_med{sfx}.npy").astype(np.float32)
        self.gx, self.gy, self.r = polar_xy(self.affine, 0.10, 1.00)
        self.med = bilin(med, self.gx, self.gy)
        self.bands = bands
        self.fb = (self.r >= match[0]) & (self.r <= match[1])

    def profiles(self, frame, dx, dy):
        p = bilin(frame.astype(np.float32), self.gx + dx, self.gy + dy)
        A = p[:, self.fb].ravel()
        B = self.med[:, self.fb].ravel()
        ok = np.isfinite(A) & np.isfinite(B)
        Am, Bm = A[ok], B[ok]
        lo, hi = np.percentile(Bm, [15, 85])
        sel = (Bm >= lo) & (Bm <= hi)
        if sel.sum() > 30 and Bm[sel].std() > 1e-3:
            g, o = np.polyfit(Bm[sel], Am[sel], 1)
        else:
            g, o = 1.0, float(np.median(Am - Bm))
        res = p - (g * self.med + o)
        out = {}
        for name, (lo_r, hi_r) in self.bands.items():
            m = (self.r >= lo_r) & (self.r <= hi_r)
            out[name] = hp_angular(np.nansum(np.clip(res[:, m], 0, None), axis=1))
        return out
