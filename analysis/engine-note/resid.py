"""Median-subtracted spectrogram: shows only what MOVES.

Any component that holds a fixed frequency for the whole clip (room tone, a
constant loop, encoder artefacts) sits at the per-bin median and cancels. A
modulating engine harmonic sweeps across bins, so it survives as a bright trace
however quiet it is relative to the stationary bed.
"""
import sys, wave, numpy as np, matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

D = 'playtest/CAP-10/audio'

def load(p):
    w = wave.open(p, 'rb')
    d = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64)/32768.0
    return d, w.getframerate()

keys = sys.argv[1:]
fig, axes = plt.subplots(len(keys), 1, figsize=(15, 3.6*len(keys)), squeeze=False)
for ax, k in zip(axes[:, 0], keys):
    x, sr = load(f'{D}/{k}.wav')
    win, hop = 8192, 256
    w = np.hanning(win)
    S = np.array([np.abs(np.fft.rfft(x[i:i+win]*w))
                  for i in range(0, len(x)-win, hop)]).T
    f = np.fft.rfftfreq(win, 1/sr)
    hi = np.searchsorted(f, 900)
    dB = 20*np.log10(S[:hi]+1e-8)
    dB = dB - np.median(dB, axis=1, keepdims=True)   # kill anything stationary
    ax.imshow(dB, origin='lower', aspect='auto', cmap='inferno',
              extent=[0, len(x)/sr, 0, f[hi]],
              vmin=0, vmax=np.percentile(dB, 99.5))
    ax.set_title(f'{k}  -  median-subtracted (only non-stationary energy)', fontsize=10)
    ax.set_ylabel('Hz')
ax.set_xlabel('seconds')
plt.tight_layout()
out = f'{D}/resid_{"_".join(keys)}.png'
plt.savefig(out, dpi=95)
print(out)
