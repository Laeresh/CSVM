# Wobble amplitude, firing vs idle, from series.npz.
#
# High-pass each pose series (subtract a 9-frame moving median: kills manoeuvre
# drift, keeps >~3 Hz), then compare RMS + frame-to-frame alternation inside the
# firing window against the idle stretches. A 15 Hz sawtooth sampled at 30.24 fps
# sits at Nyquist: it shows up as sign-alternating frame-to-frame differences.
import os

import numpy as np

OUT = os.path.join(".scratch", "gun-wobble")
FIRE = (113, 247)          # sustained ammo-counter redraw window (measure.py)
IDLE = [(10, 108), (252, 355)]


def hp(x, k=9):
    pad = k // 2
    xp = np.pad(x, pad, mode="edge")
    med = np.array([np.nanmedian(xp[i:i + k]) for i in range(len(x))])
    return x - med


def stats(x, lo, hi):
    seg = x[lo:hi]
    seg = seg[~np.isnan(seg)]
    rms = float(np.sqrt((seg ** 2).mean()))
    d = np.diff(seg)
    # alternation: fraction of consecutive diffs that flip sign (0.5 = noise, ->1 = Nyquist tone)
    flips = float((np.sign(d[1:]) * np.sign(d[:-1]) < 0).mean())
    return rms, flips, float(np.abs(d).mean())


def main():
    z = np.load(os.path.join(OUT, "series.npz"))
    fps = float(z["fps"])
    for name in ("cx", "cy", "ang"):
        x = hp(np.asarray(z[name], dtype=float))
        frms, fflip, fstep = stats(x, *FIRE)
        irms = np.sqrt(np.mean([stats(x, lo, hi)[0] ** 2 for lo, hi in IDLE]))
        iflip = np.mean([stats(x, lo, hi)[1] for lo, hi in IDLE])
        istep = np.mean([stats(x, lo, hi)[2] for lo, hi in IDLE])
        unit = "deg" if name == "ang" else "px"
        print(f"{name:>3}: fire rms {frms:.3f}{unit} step {fstep:.3f} flip {fflip:.2f}   "
              f"idle rms {irms:.3f}{unit} step {istep:.3f} flip {iflip:.2f}   "
              f"ratio rms x{frms / (irms + 1e-9):.2f} step x{fstep / (istep + 1e-9):.2f}")

    # spectrum of the firing window for each series (rfft, Hann)
    for name in ("cx", "cy", "ang"):
        x = hp(np.asarray(z[name], dtype=float))[FIRE[0]:FIRE[1]]
        x = np.nan_to_num(x - np.nanmean(x))
        spec = np.abs(np.fft.rfft(x * np.hanning(len(x))))
        freqs = np.fft.rfftfreq(len(x), 1 / fps)
        top = np.argsort(spec)[-4:][::-1]
        peaks = ", ".join(f"{freqs[i]:.1f}Hz:{spec[i]:.1f}" for i in top)
        print(f"{name:>3} fire spectrum peaks: {peaks}")


if __name__ == "__main__":
    main()
