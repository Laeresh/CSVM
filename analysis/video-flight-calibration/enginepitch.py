"""Regress the measured engine playback-rate against the decoded flight state.

Pairs the audio pitch curve (analysis/engine-note/scale.py) with this harness's decoded
altimeter/speedometer for the same clip, on the same wall-clock PTS base, and asks
which candidate drives the note: climb rate, airspeed, or flight-path angle.
"""
import numpy as np
from numpy.lib.stride_tricks import sliding_window_view

from decode import CACHE

AUD = 'playtest/CAP-10/audio'
# clip key -> (audio-curve key, display name). The audio key names a <key>_scale.npy
# written by analysis/engine-note/scale.py for the same clip.
PAIRS = [('cap10chase', 'c', 'CAP-10 3 3rd Person.mp4'),
         ('cap10dive', 'd', 'Bloodhawk Dive Sound.mp4'),
         ('cap10var', 'nvar', 'CAP-10 Dive 100% Thrust variable climb rate.mp4'),
         ('cap10bank', 'nbank', 'CAP-10 90 deg Banked Pitch Up Down.mp4'),
         ('cap10climb', 'nclimb', 'CAP-10 Variable Climb Pitch up.mp4')]
MPH_FPS = 5280.0 / 3600.0


def series(short, akey):
    t = np.load(f'{CACHE}/{short}_pts.npy')
    alt = np.load(f'{CACHE}/{short}_alt2.npy')
    mph = np.load(f'{CACHE}/{short}_mph2.npy')
    n = min(len(t), len(alt), len(mph))
    t, alt, mph = t[:n], alt[:n], mph[:n]
    # climb rate: centred 1 s window on PTS, ft/s
    cr = np.full(n, np.nan)
    for i in range(n):
        j0, j1 = np.searchsorted(t, t[i] - 0.5), min(n - 1, np.searchsorted(t, t[i] + 0.5))
        if j1 > j0:
            cr[i] = (alt[j1] - alt[j0]) / (t[j1] - t[j0])
    gamma = np.degrees(np.arcsin(np.clip(cr / np.maximum(mph * MPH_FPS, 1e-6), -1, 1)))

    # Energy-height rate. With throttle pinned at 100%, He = alt + v^2/2g strips out the
    # trade between height and speed, so dHe/dt is thrust-minus-drag -- i.e. how much the
    # airframe is being worked. Unlike climb rate it does not go to zero merely because
    # the manoeuvre is horizontal, which is what the 90 deg-banked take needs.
    G = 32.174
    v = mph * MPH_FPS
    He = alt + v * v / (2 * G)
    dHe = np.full(n, np.nan)
    dv = np.full(n, np.nan)
    for i in range(n):
        j0, j1 = np.searchsorted(t, t[i] - 0.5), min(n - 1, np.searchsorted(t, t[i] + 0.5))
        if j1 > j0:
            dHe[i] = (He[j1] - He[j0]) / (t[j1] - t[j0])
            dv[i] = (v[j1] - v[j0]) / (t[j1] - t[j0])

    ta, r, _ = np.load(f'{AUD}/{akey}_scale.npy')
    med = np.median(sliding_window_view(np.pad(r, (3, 3), mode='edge'), 7), axis=1)
    pitch = np.interp(t, ta, med / np.median(med[ta < 1.5]))
    return t, alt, mph, cr, gamma, pitch, dHe, dv


def r2(x, y):
    m = np.isfinite(x) & np.isfinite(y)
    if m.sum() < 10:
        return np.nan
    return float(np.corrcoef(x[m], y[m])[0, 1] ** 2)


def main():
    rows = []
    for short, akey, name in PAIRS:
        t, alt, mph, cr, gamma, pitch, dHe, dv = series(short, akey)
        m = np.isfinite(gamma) & np.isfinite(dHe)
        print(f'\n### {name}  ({short})  n={len(t)}')
        print(f'  alt {alt.min():.0f}..{alt.max():.0f} ft | mph {mph.min():.1f}..{mph.max():.1f} | '
              f'climb {cr[m].min()*60:.0f}..{cr[m].max()*60:.0f} ft/min | '
              f'gamma {gamma[m].min():.1f}..{gamma[m].max():.1f} deg | '
              f'dHe {dHe[m].min():.0f}..{dHe[m].max():.0f} ft/s')
        r = dict(climb=r2(cr, pitch), speed=r2(mph, pitch), gamma=r2(gamma, pitch),
                 dHe=r2(dHe, pitch), dv=r2(dv, pitch))
        for k, v in r.items():
            print(f'  pitch vs {k:12s} R2 = {v:.3f}')
        rows.append((name, r))
        print('    t     alt    mph   climb    gamma     dHe   pitch')
        for i in range(0, len(t), max(1, len(t) // 16)):
            print(f'  {t[i]:5.2f} {alt[i]:7.0f} {mph[i]:6.1f} {cr[i]*60:+8.0f} '
                  f'{gamma[i]:+7.1f} {dHe[i]:+7.0f} {pitch[i]:7.4f}')

    print('\n=== R2 summary (higher is better)')
    ks = ['climb', 'speed', 'gamma', 'dHe', 'dv']
    print(f'{"take":52s}' + ''.join(f'{k:>8s}' for k in ks))
    for name, r in rows:
        print(f'{name:52s}' + ''.join(f'{r[k]:8.3f}' for k in ks))


if __name__ == '__main__':
    main()
