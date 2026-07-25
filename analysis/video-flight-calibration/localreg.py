"""Per-dial local registration.

A global panel translation is not enough on the shaking dive clip: the panel is
a 3D object, so shake adds a little rotation and the correction that suits the
left dial is wrong for the right one. Registering each dial's own neighbourhood
absorbs that to first order.

The template is the pooled median restricted to pixels that barely vary over the
whole corpus (pooled std below a cut), so needles, the ADI ball and the compass
tape never vote.
"""
import numpy as np

CACHE = ".scratch/vidcal/cache"


def _par(m1, z, p1):
    d = m1 - 2 * z + p1
    return 0.0 if abs(d) < 1e-12 else float(np.clip(0.5 * (m1 - p1) / d, -1, 1))


class LocalRegistrar:
    def __init__(self, box, std_cut=6.0, max_shift=10, pad=14):
        x0, y0, x1, y1 = box
        self.box = (x0 - pad, y0 - pad, x1 + pad, y1 + pad)
        med = np.load(f"{CACHE}/pool_med.npy")
        sd = np.load(f"{CACHE}/pool_std.npy")
        bx0, by0, bx1, by1 = self.box
        self.ref = med[by0:by1, bx0:bx1].astype(np.float32)
        stat = (sd[by0:by1, bx0:bx1] < std_cut).astype(np.float32)
        h, w = self.ref.shape
        self.win = np.outer(np.hanning(h), np.hanning(w)).astype(np.float32) * stat
        self.ms = max_shift
        self.F_ref = self._f(self.ref)
        self.shape = (h, w)

    def _f(self, img):
        x = (img - img.mean()) * self.win
        return np.fft.rfft2(x)

    def shift(self, frame):
        bx0, by0, bx1, by1 = self.box
        sub = frame[by0:by1, bx0:bx1].astype(np.float32)
        F = self._f(sub)
        cps = F * np.conj(self.F_ref)
        m = np.abs(cps)
        cps = np.where(m > 1e-9, cps / np.maximum(m, 1e-9), 0)
        h, w = self.shape
        c = np.fft.fftshift(np.fft.irfft2(cps, s=(h, w)))
        cy, cx = h // 2, w // 2
        ms = self.ms
        s = c[cy - ms:cy + ms + 1, cx - ms:cx + ms + 1]
        j = np.unravel_index(np.argmax(s), s.shape)
        py, px = int(j[0]), int(j[1])
        dy = py - ms
        dx = px - ms
        if 0 < py < s.shape[0] - 1:
            dy += _par(s[py - 1, px], s[py, px], s[py + 1, px])
        if 0 < px < s.shape[1] - 1:
            dx += _par(s[py, px - 1], s[py, px], s[py, px + 1])
        return float(dx), float(dy), float(s.max())
