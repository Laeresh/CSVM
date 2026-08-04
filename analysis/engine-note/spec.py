import sys, wave, numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

D = 'playtest/CAP-10/audio'

def load(p):
    w = wave.open(p, 'rb')
    d = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64)/32768.0
    return d, w.getframerate()

keys = sys.argv[1:]
fig, axes = plt.subplots(len(keys), 1, figsize=(16, 3.4*len(keys)), squeeze=False)
for ax, k in zip(axes[:, 0], keys):
    x, sr = load(f'{D}/{k}.wav')
    win, hop = 8192, 512
    w = np.hanning(win)
    cols = []
    for i in range(0, len(x)-win, hop):
        cols.append(np.abs(np.fft.rfft(x[i:i+win]*w)))
    S = np.array(cols).T
    f = np.fft.rfftfreq(win, 1/sr)
    hi = np.searchsorted(f, 1200)
    dB = 20*np.log10(S[:hi]+1e-8)
    ax.imshow(dB, origin='lower', aspect='auto', cmap='magma',
              extent=[0, len(x)/sr, 0, f[hi]],
              vmin=np.percentile(dB, 55), vmax=np.percentile(dB, 99.9))
    ax.set_title(f'{k}   (0-1200 Hz)', fontsize=10)
    ax.set_ylabel('Hz')
ax.set_xlabel('seconds')
plt.tight_layout()
out = f'{D}/spec_{"_".join(keys)}.png'
plt.savefig(out, dpi=90)
print(out)
