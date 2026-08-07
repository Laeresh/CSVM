# Frame-to-frame registration of a fuselage-centred patch: the plane's own
# translation per frame, immune to the red-mask flicker that poisoned the
# centroid instrument (see analyze.py first pass).
#
# Plain windowed phase correlation, frame n vs n-1, Hann window only — per
# docs/verification.md, never a binary stillness mask on both images. The patch
# (game coords) covers fuselage + canopy + wing roots, where plane texture
# dominates; the sky behind is featureless so the plane's shift wins the vote.
# Outputs per-frame (dx, dy, peak) to series_track.npz and prints the same
# fire-vs-idle stats as analyze.py.
import os

import imageio_ffmpeg as iio
import numpy as np

VIDEO = r"Z:\CSVM\OriginalScreenshots\Videos\Gun Wobble and animation.mp4"
OUT = os.path.join(".scratch", "gun-wobble")
X0 = 640
PATCH = (slice(420, 560), slice(500, 780))  # y, x in game coords
FIRE = (113, 247)
IDLE = [(10, 108), (252, 355)]


def _par(m1, z, p1):
    d = m1 - 2 * z + p1
    return 0.0 if abs(d) < 1e-12 else float(np.clip(0.5 * (m1 - p1) / d, -1, 1))


def register(a, b, win, ms=8):
    fa = np.fft.rfft2((a - a.mean()) * win)
    fb = np.fft.rfft2((b - b.mean()) * win)
    cps = fb * np.conj(fa)
    m = np.abs(cps)
    cps = np.where(m > 1e-9, cps / np.maximum(m, 1e-9), 0)
    h, w = a.shape
    c = np.fft.fftshift(np.fft.irfft2(cps, s=(h, w)))
    cy, cx = h // 2, w // 2
    s = c[cy - ms:cy + ms + 1, cx - ms:cx + ms + 1]
    j = np.unravel_index(np.argmax(s), s.shape)
    py, px = int(j[0]), int(j[1])
    dy, dx = py - ms, px - ms
    if 0 < py < s.shape[0] - 1:
        dy += _par(s[py - 1, px], s[py, px], s[py + 1, px])
    if 0 < px < s.shape[1] - 1:
        dx += _par(s[py, px - 1], s[py, px], s[py, px + 1])
    return float(dx), float(dy), float(s.max())


def stats(x, lo, hi):
    seg = x[lo:hi]
    rms = float(np.sqrt(np.mean(seg ** 2)))
    d = np.diff(seg)
    flips = float((np.sign(d[1:]) * np.sign(d[:-1]) < 0).mean())
    return rms, flips, float(np.abs(d).mean())


def main():
    os.makedirs(OUT, exist_ok=True)
    reader = iio.read_frames(VIDEO)
    meta = next(reader)
    w, h = meta["size"]
    fps = meta["fps"]
    ph = PATCH[0].stop - PATCH[0].start
    pw = PATCH[1].stop - PATCH[1].start
    win = np.outer(np.hanning(ph), np.hanning(pw)).astype(np.float32)

    prev = None
    dxs, dys, peaks = [0.0], [0.0], [1.0]
    for raw in reader:
        g = np.frombuffer(raw, dtype=np.uint8).reshape(h, w, 3)[:, X0:X0 + 1280]
        patch = g[PATCH].astype(np.float32).mean(axis=2)
        if prev is not None:
            dx, dy, pk = register(prev, patch, win)
            dxs.append(dx); dys.append(dy); peaks.append(pk)
        prev = patch
    dxs, dys, peaks = map(np.asarray, (dxs, dys, peaks))
    np.savez(os.path.join(OUT, "series_track.npz"), fps=fps, dx=dxs, dy=dys, peak=peaks)

    for name, d in (("dx", dxs), ("dy", dys)):
        # d is already a per-frame velocity; wobble shows as large alternating d
        frms, fflip, fstep = stats(d, *FIRE)
        irms = np.sqrt(np.mean([stats(d, lo, hi)[0] ** 2 for lo, hi in IDLE]))
        iflip = np.mean([stats(d, lo, hi)[1] for lo, hi in IDLE])
        print(f"{name}: fire rms {frms:.3f}px/f flip {fflip:.2f}   idle rms {irms:.3f}px/f "
              f"flip {iflip:.2f}   ratio x{frms / (irms + 1e-9):.2f}")

    for lo, hi, tag in (FIRE + ("fire",), *[(a, b, "idle") for a, b in IDLE]):
        seg = dxs[lo:hi] - dxs[lo:hi].mean()
        spec = np.abs(np.fft.rfft(seg * np.hanning(len(seg))))
        freqs = np.fft.rfftfreq(len(seg), 1 / fps)
        top = np.argsort(spec)[-3:][::-1]
        print(f"dx {tag} f{lo}..{hi} peaks: " + ", ".join(f"{freqs[i]:.1f}Hz:{spec[i]:.1f}" for i in top))
    print("min peak in fire window:", float(peaks[FIRE[0]:FIRE[1]].min()))


if __name__ == "__main__":
    main()
