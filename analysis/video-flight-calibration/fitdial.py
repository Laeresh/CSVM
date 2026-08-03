"""Fit the affine that maps a dial's original texture onto the cockpit panel.

The panel is a 3D model under a fixed camera, so each flat dial quad reaches the
screen through a homography; over ~90 px an affine is indistinguishable from it.
Fitting the texture rather than guessing a centre+radius gives us the dial's own
frame, where the face art defines the value scale exactly (altimeter 0 at the
top, 36 deg per unit; speedometer labels at 0/100/200/300).

Objective: weighted normalised cross-correlation against the pooled median
frame, with weight 1/(1+std) so the needle-swept pixels stop voting.
"""
import numpy as np
from PIL import Image
from scipy.optimize import minimize

CACHE = ".scratch/vidcal/cache"
TEXDIR = "extracted/C1/texture"


def load_tex(name):
    im = Image.open(f"{TEXDIR}/{name}.png").convert("RGBA")
    a = np.asarray(im).astype(np.float32)
    lum = a[:, :, 0] * 0.299 + a[:, :, 1] * 0.587 + a[:, :, 2] * 0.114
    alpha = a[:, :, 3] / 255.0
    return lum, alpha


def sample(tex, alpha, U, V):
    """Bilinear sample. U,V in [-1,1], v up. Returns (value, valid)."""
    h, w = tex.shape
    px = (U + 1) * 0.5 * (w - 1)
    py = (1 - V) * 0.5 * (h - 1)
    ok = (px >= 0) & (px <= w - 1) & (py >= 0) & (py <= h - 1)
    px = np.clip(px, 0, w - 1)
    py = np.clip(py, 0, h - 1)
    x0 = np.floor(px).astype(int)
    y0 = np.floor(py).astype(int)
    x1 = np.minimum(x0 + 1, w - 1)
    y1 = np.minimum(y0 + 1, h - 1)
    fx = px - x0
    fy = py - y0

    def bl(t):
        return (t[y0, x0] * (1 - fx) * (1 - fy) + t[y0, x1] * fx * (1 - fy)
                + t[y1, x0] * (1 - fx) * fy + t[y1, x1] * fx * fy)

    return bl(tex), bl(alpha) * ok


def fit(texname, c0, m0, box, rmax=1.02, verbose=True):
    tex, alpha = load_tex(texname)
    med = np.load(f"{CACHE}/pool_med.npy")
    sd = np.load(f"{CACHE}/pool_std.npy")
    x0, y0, x1, y1 = box
    img = med[y0:y1, x0:x1]
    wgt = 1.0 / (1.0 + sd[y0:y1, x0:x1])
    ys, xs = np.mgrid[y0:y1, x0:x1]
    xs = xs.astype(np.float32)
    ys = ys.astype(np.float32)

    def ncc(p):
        cx, cy, a, b, c, d = p
        det = a * d - b * c
        if abs(det) < 1e-6:
            return 1.0
        dx = xs - cx
        dy = ys - cy
        # invert [[a,b],[c,d]] applied to (u, -v): screen = c + M @ (u,-v)
        u = (d * dx - b * dy) / det
        w_ = (-c * dx + a * dy) / det
        v = -w_
        t, val = sample(tex, alpha, u, v)
        m = (val > 0.5) & (u * u + v * v < rmax * rmax)
        if m.sum() < 200:
            return 1.0
        W = wgt[m]
        A = t[m]
        B = img[m]
        A = A - np.average(A, weights=W)
        B = B - np.average(B, weights=W)
        num = np.sum(W * A * B)
        den = np.sqrt(np.sum(W * A * A) * np.sum(W * B * B))
        return -num / max(den, 1e-9)

    p0 = np.array([c0[0], c0[1], m0[0][0], m0[0][1], m0[1][0], m0[1][1]], float)
    best = None
    for scale in (1.0,):
        r = minimize(ncc, p0, method="Nelder-Mead",
                     options=dict(maxiter=20000, xatol=1e-4, fatol=1e-8))
        r = minimize(ncc, r.x, method="Powell",
                     options=dict(maxiter=20000, xtol=1e-5, ftol=1e-9))
        if best is None or r.fun < best.fun:
            best = r
    p = best.x
    if verbose:
        cx, cy, a, b, c, d = p
        M = np.array([[a, b], [c, d]])
        sx = np.hypot(a, c)
        sy = np.hypot(b, d)
        rot = np.degrees(np.arctan2(c, a))
        shear = np.degrees(np.arccos(np.clip((a * b + c * d) / (sx * sy), -1, 1)))
        print(f"{texname}: NCC={-best.fun:.4f}")
        print(f"  centre (ROI) = ({cx:.2f}, {cy:.2f})")
        print(f"  M = [[{a:+.3f} {b:+.3f}] [{c:+.3f} {d:+.3f}]]")
        print(f"  |u| axis = {sx:.2f} px, |v| axis = {sy:.2f} px, "
              f"rot(u-axis) = {rot:+.2f} deg, u^v angle = {shear:.2f} deg")
    return p, -best.fun


if __name__ == "__main__":
    # Start points and search boxes are the dial centres measured off pool_std's
    # needle-sweep fans (`pool.py` writes it), not guesses - the optimiser has no
    # basin to climb if the box clips the dial, and it fails at NCC ~0 rather than
    # loudly.  The two flat dials must reach NCC ~0.98 with mirror-symmetric shear
    # (-6.54 / +6.67 px); anything less means the fit did not lock.
    res = {}
    res["altimeter"] = fit("altimeter", (78.0, 214.0), [[46.0, 0.0], [0.0, 46.0]],
                           (20, 155, 140, 275))
    res["speedometer"] = fit("speedometer", (612.0, 216.0), [[46.0, 0.0], [0.0, 46.0]],
                             (550, 155, 675, 275))
    # The ADI is a gyro *ball*, so a flat-quad affine tops out near NCC 0.65 - this
    # fit supplies the aperture only; attitude comes from adi.py's area fraction.
    res["horizonindicator"] = fit("horizonindicator", (348.0, 270.0), [[46.0, 0.0], [0.0, 50.0]],
                                  (285, 210, 410, 335))
    np.save(f"{CACHE}/dial_affines.npy",
            np.array([res[k][0] for k in ["altimeter", "speedometer", "horizonindicator"]]))
