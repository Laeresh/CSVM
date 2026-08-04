"""Measure the engine-sample playback-rate multiplier over time.

The own-ship engine is a looped sample played at a varying rate, so the whole
spectrum translates rigidly in log-frequency. Cross-correlating each frame's
log-spectrum against a reference frame recovers that translation directly, which
is the pitch multiplier BL-109 talks about -- no f0 estimate needed.
"""
import sys, wave, numpy as np

D = 'playtest/CAP-10/audio'


def load(p):
    w = wave.open(p, 'rb')
    d = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768.0
    return d, w.getframerate()


def logspec(x, sr, win=8192, hop=1024, fmin=60.0, fmax=5000.0, nbins=1200):
    """Spectrogram resampled onto a uniform log-frequency axis."""
    w = np.hanning(win)
    lf = np.linspace(np.log(fmin), np.log(fmax), nbins)
    grid = np.exp(lf)
    freqs = np.fft.rfftfreq(win, 1.0 / sr)
    frames, times = [], []
    for i in range(0, len(x) - win, hop):
        S = np.abs(np.fft.rfft(x[i:i + win] * w))
        S = np.interp(grid, freqs, S)
        S = np.log(S + 1e-8)
        S = S - np.convolve(S, np.ones(81) / 81, mode='same')  # whiten: keep peak structure only
        frames.append(S)
        times.append((i + win / 2) / sr)
    return np.array(times), np.array(frames), (lf[1] - lf[0])


def track(F, dlf, ref, maxshift=200):
    """Sub-bin log-shift of each frame vs the reference frame."""
    r = F[ref] - F[ref].mean()
    out = []
    for f in F:
        a = f - f.mean()
        cc = np.correlate(a, r, mode='full')
        lags = np.arange(-len(r) + 1, len(r))
        m = np.abs(lags) <= maxshift
        cc, lags = cc[m], lags[m]
        k = int(np.argmax(cc))
        if 0 < k < len(cc) - 1:
            y0, y1, y2 = cc[k - 1], cc[k], cc[k + 1]
            d = 0.5 * (y0 - y2) / (y0 - 2 * y1 + y2 + 1e-30)
        else:
            d = 0.0
        peak = cc[k] / (np.sqrt(np.sum(a**2) * np.sum(r**2)) + 1e-30)
        out.append(((lags[k] + d) * dlf, peak))
    return np.array(out)


for key in sys.argv[1:]:
    x, sr = load(f'{D}/{key}.wav')
    t, F, dlf = logspec(x, sr)
    res = track(F, dlf, ref=0)
    ratio = np.exp(res[:, 0])
    np.save(f'{D}/{key}_scale.npy', np.vstack([t, ratio, res[:, 1]]))
    print(f'### {key}  dur={len(x)/sr:.2f}s  frames={len(t)}  (ratio is relative to t={t[0]:.2f}s)')
    print(f'    min={np.min(ratio):.4f} @ t={t[np.argmin(ratio)]:.2f}   '
          f'max={np.max(ratio):.4f} @ t={t[np.argmax(ratio)]:.2f}   '
          f'span={np.max(ratio)/np.min(ratio):.4f}')
    for i in range(0, len(t), 2):
        bar = '#' * int(max(0, (ratio[i] - 0.80)) * 200)
        print(f'  t={t[i]:6.2f}  ratio={ratio[i]:6.4f}  ncc={res[i,1]:5.2f}  {bar}')
