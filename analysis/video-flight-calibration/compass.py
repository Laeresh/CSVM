"""Heading from the cockpit compass tape.

The tape is a strip that scrolls horizontally with heading, so tracking it is a
1-D registration problem rather than a dial-angle one. Two things make it a
self-calibrating instrument:

  * it is *periodic in 360 degrees*, so resampling the tracked strip into a
    tape-fixed coordinate builds a panorama whose period is one revolution; and
  * its major ticks form a lattice, which is a second, independent ruler.

Both are measured here and cross-checked against each other, so no
degrees-per-pixel constant has to be assumed. On CAP-01 they agree to 0.4% and
put the scale at exactly 1.500 px/deg, majors 15 deg apart, 24 per revolution.

Only the central few pixels of the tape are used. The tape is a 3D model under
perspective, so its scale varies across the strip; near the centre that
variation is negligible, and the panorama is built from the centre alone.

WARNING: this reads the NOSE heading, not the velocity vector. With any angle of
attack it is not the flight-path turn rate.
"""
import numpy as np

from decode import CACHE

# compass tape, in ROI-local coords (ROI origin is game (295, 355))
TAPE = (235, 455)          # x span
TICKS = (40, 60)           # rows holding the tick marks - used for tracking
BAND = (38, 62)            # rows kept in the panorama (labels + ticks)
LABELS = (0, 11)           # panorama rows holding the cardinal glyphs
HW_TRACK = 45              # half-window for frame-to-frame tracking
HW_PANO = 12               # half-window resampled into the panorama
PX_PER_DEG = 1.5000        # measured; see module docstring


def _prep(x):
    x = x - x.mean()
    return x * np.hanning(len(x))


def _shift1(a, b, maxs=25):
    """Sub-pixel 1-D cross-correlation; positive means b moved right of a."""
    fa, fb = np.fft.rfft(_prep(a)), np.fft.rfft(_prep(b))
    cc = np.fft.fftshift(np.fft.irfft(fb * np.conj(fa), n=len(a)))
    m = len(cc) // 2
    lo, hi = m - maxs, m + maxs + 1
    k = int(np.argmax(cc[lo:hi])) + lo
    y0, y1, y2 = cc[k - 1], cc[k], cc[k + 1]
    d = y0 - 2 * y1 + y2
    sub = 0.0 if abs(d) < 1e-12 else float(np.clip(0.5 * (y0 - y2) / d, -1, 1))
    peak = float(cc[k] / (np.linalg.norm(_prep(a)) * np.linalg.norm(_prep(b)) + 1e-9))
    return (k + sub) - m, peak


def track(short, verbose=True):
    """Cumulative tape travel in px, one entry per frame."""
    a = np.load(f"{CACHE}/{short}_lum.npy").astype(np.float32)
    strip = a[:, TICKS[0]:TICKS[1], TAPE[0]:TAPE[1]].mean(axis=1)
    n, w = strip.shape
    c = w // 2
    win = strip[:, c - HW_TRACK:c + HW_TRACK]
    d = np.zeros(n)
    pk = np.zeros(n)
    for i in range(1, n):
        d[i], pk[i] = _shift1(win[i - 1], win[i])
    cum = np.cumsum(d)
    np.save(f"{CACHE}/{short}_compass_cum.npy", cum)
    if verbose:
        print(f"{short:6s} tape travel {cum[-1]:+8.1f} px = {-cum[-1] / PX_PER_DEG:+8.2f} deg   "
              f"track peak min {pk[1:].min():.3f} median {np.median(pk[1:]):.3f}")
    return cum


def panorama(short):
    """Stitch the tape into a tape-fixed strip, for calibration and for reading."""
    a = np.load(f"{CACHE}/{short}_lum.npy").astype(np.float32)
    cum = np.load(f"{CACHE}/{short}_compass_cum.npy")
    band = a[:, BAND[0]:BAND[1], TAPE[0]:TAPE[1]]
    n, h, w = band.shape
    c = w // 2
    lo = int(np.floor((-cum).min())) - HW_PANO
    hi = int(np.ceil((-cum).max())) + HW_PANO
    L = hi - lo + 1
    acc = np.zeros((h, L))
    wgt = np.zeros(L)
    for i in range(n):
        for x in range(c - HW_PANO, c + HW_PANO):
            g = (x - c) - cum[i] - lo
            g0 = int(np.floor(g))
            f = g - g0
            if 0 <= g0 < L - 1:
                acc[:, g0] += band[i, :, x] * (1 - f)
                wgt[g0] += 1 - f
                acc[:, g0 + 1] += band[i, :, x] * f
                wgt[g0 + 1] += f
    pan = acc / np.maximum(wgt, 1e-6)
    pan[:, wgt < 1] = 0
    np.save(f"{CACHE}/{short}_compass_pano.npy", pan)
    return pan


def calibrate(short, verbose=True):
    """Measure px/deg two independent ways. Needs >~1 full revolution of tape."""
    pan = np.load(f"{CACHE}/{short}_compass_pano.npy")
    L = pan.shape[1]

    def nrm(x):
        x = x - x.mean()
        return x / (np.linalg.norm(x) + 1e-9)

    # ruler 1: the panorama repeats every 360 deg
    lab = pan[LABELS[0]:LABELS[1]].mean(axis=0)
    s = lab - np.convolve(lab, np.ones(41) / 41, mode="same")
    lags = np.arange(300, min(660, L - 80))
    cc = np.array([np.dot(nrm(s[:L - k]), nrm(s[k:])) for k in lags])
    k = int(lags[np.argmax(cc)])
    xs = np.arange(k - 6, k + 7)
    ys = np.array([np.dot(nrm(s[:L - j]), nrm(s[j:])) for j in xs])
    p = np.polyfit(xs, ys, 2)
    period = -p[1] / (2 * p[0])

    # ruler 2: the major ticks are a lattice
    col = pan[11:22].mean(axis=0)
    v = col - np.convolve(col, np.ones(41) / 41, mode="same")
    pk = []
    for i in range(2, L - 2):
        if v[i] > v[i - 1] and v[i] >= v[i + 1] and v[i] > 0:
            y0, y1, y2 = v[i - 1], v[i], v[i + 1]
            d = y0 - 2 * y1 + y2
            sub = 0.0 if abs(d) < 1e-12 else float(np.clip(0.5 * (y0 - y2) / d, -1, 1))
            pk.append((i + sub, v[i]))
    pk = np.array(pk)
    maj = np.sort(pk[pk[:, 1] > np.percentile(pk[:, 1], 62), 0])

    def fit(p, step):
        off = 0.0
        for _ in range(8):
            idx = np.round((p - (off if off else p[0])) / step).astype(int)
            A = np.vstack([idx, np.ones(len(idx))]).T
            step, off = np.linalg.lstsq(A, p, rcond=None)[0]
        return step, off, p - (np.round((p - off) / step) * step + off)

    # a cardinal glyph sits over some majors and drags the peak off the lattice;
    # one rejection pass drops those before the scale is read off the rest
    step, off, res = fit(maj, 22.5)
    step, off, res = fit(maj[np.abs(res) < 0.45 * step], step)
    if verbose:
        print(f"{short:6s} period {period:7.2f} px (corr {cc.max():.3f})  "
              f"major step {step:7.4f} px -> {period / step:6.3f} majors/rev  "
              f"=> {period / 360:.4f} px/deg vs 24-major {24 * step / 360:.4f}")
    return period, step


def heading(short):
    """Degrees turned, one entry per frame, monotone in the direction of turn."""
    return -np.load(f"{CACHE}/{short}_compass_cum.npy") / PX_PER_DEG


if __name__ == "__main__":
    import sys
    for s in sys.argv[1:] or ["cap01"]:
        track(s)
        panorama(s)
        calibrate(s)
