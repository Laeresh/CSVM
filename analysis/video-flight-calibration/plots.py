"""Overview plots of the decoded state vectors."""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from scipy.signal import savgol_filter

from decode import CACHE

CLIPS = ["dive", "pitch", "roll", "yaw", "accel", "decel"]
FPS = {}


def load(s):
    m = np.load(f"{CACHE}/{s}_meta.npy")
    fps = float(m[0])
    alt = np.load(f"{CACHE}/{s}_alt3.npy")
    mph = np.load(f"{CACHE}/{s}_mph2.npy")
    t = np.arange(len(alt)) / fps
    return t, alt, mph, fps


def smooth(y, win=15, po=3):
    win = min(win, len(y) - (1 - len(y) % 2))
    if win < po + 2:
        return y
    if win % 2 == 0:
        win -= 1
    return savgol_filter(y, win, po)


def main():
    fig, axes = plt.subplots(len(CLIPS), 2, figsize=(15, 3.0 * len(CLIPS)))
    for r, s in enumerate(CLIPS):
        t, alt, mph, fps = load(s)
        ax = axes[r, 0]
        ax.plot(t, alt, lw=0.8, color="tab:blue")
        ax.plot(t, smooth(alt), lw=1.4, color="k", alpha=0.6)
        ax.set_ylabel("alt (ft)", color="tab:blue")
        ax.set_title(f"{s}   ({len(alt)} frames @ {fps:.2f} fps)")
        ax2 = ax.twinx()
        ax2.plot(t, mph, lw=0.8, color="tab:red")
        ax2.set_ylabel("mph", color="tab:red")
        ax.grid(alpha=0.3)

        ax = axes[r, 1]
        dh = np.gradient(smooth(alt), t)           # ft per WALL second
        dv = np.gradient(smooth(mph), t)           # mph per WALL second
        ax.plot(t, dh, lw=1.0, color="tab:blue", label="dh/dt (ft/wall-s)")
        ax.axhline(0, color="k", lw=0.5)
        ax.set_ylabel("dh/dt (ft/wall-s)", color="tab:blue")
        ax2 = ax.twinx()
        ax2.plot(t, dv, lw=1.0, color="tab:red", label="dV/dt")
        ax2.set_ylabel("dV/dt (mph/wall-s)", color="tab:red")
        ax.grid(alpha=0.3)
        ax.set_title(f"{s}: rates in WALL time")
    for ax in axes[-1]:
        ax.set_xlabel("wall time (s)")
    fig.tight_layout()
    fig.savefig(".scratch/vidcal/overview.png", dpi=100)
    print("wrote .scratch/vidcal/overview.png")


if __name__ == "__main__":
    main()
