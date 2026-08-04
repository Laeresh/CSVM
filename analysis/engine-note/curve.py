import sys, numpy as np
D = 'playtest/CAP-10/audio'

for key in sys.argv[1:]:
    t, r, ncc = np.load(f'{D}/{key}_scale.npy')
    # median-smooth to kill single-frame correlation slips
    k = 7
    pad = np.pad(r, (k//2, k//2), mode='edge')
    from numpy.lib.stride_tricks import sliding_window_view
    med = np.median(sliding_window_view(pad, k), axis=1)
    # re-anchor: level flight = the first 1.5 s of the clip
    base = np.median(med[t < 1.5])
    rel = med / base
    print(f'\n### {key}   anchored to median of t<1.5s   '
          f'min={rel.min():.4f}@{t[np.argmin(rel)]:.2f}s  max={rel.max():.4f}@{t[np.argmax(rel)]:.2f}s')
    for i in range(0, len(t), 3):
        v = rel[i]
        pos = int(round((v - 0.85) * 100))
        bar = ' ' * max(0, pos) + '*'
        print(f'  t={t[i]:6.2f}  x{v:6.4f}  ncc={ncc[i]:4.2f} |{bar}')
