"""CAP-10 Dive Recovery: the engine note through a dive AND its pull-out.

The one manoeuvre the other nine takes never contained. BL-109's standing claim is
"drops ~12% through a dive and overshoots ~1.05 at pull-out", and a pull-out is the
only place the two candidate drivers point OPPOSITE ways:

  elevator model  -> push (nose-down) and pull (nose-up) have opposite sign, so the
                     note dips at the pushover and spikes at the recovery
  climb-rate model-> the note tracks descent rate, so it moves monotonically down
                     into the dive and back up out of it, with no spike

so the sign of the note during the recovery separates them without needing a rig.

Baseline is the level segment before the pushover, not scale.py's t=0 frame, so
every number here is "x times the level-flight note" like the rest of CAP-10.
"""
import numpy as np
from numpy.lib.stride_tricks import sliding_window_view

from decode import CACHE

AUD = 'playtest/CAP-10/audio'
KEY, AKEY = 'cap10rec', 'rec'
LEVEL = (0.5, 4.0)          # steady 296 mph, +650 ft/min, before the stick moves
MPH_FPS = 5280.0 / 3600.0


def series():
    t = np.load(f'{CACHE}/{KEY}_pts.npy')
    alt = np.load(f'{CACHE}/{KEY}_alt2.npy')
    mph = np.load(f'{CACHE}/{KEY}_mph2.npy')
    n = min(len(t), len(alt), len(mph))
    t, alt, mph = t[:n], alt[:n], mph[:n]

    cr = np.full(n, np.nan)
    for i in range(n):
        j0, j1 = np.searchsorted(t, t[i] - 0.5), min(n - 1, np.searchsorted(t, t[i] + 0.5))
        if j1 > j0:
            cr[i] = (alt[j1] - alt[j0]) / (t[j1] - t[j0])
    gamma = np.degrees(np.arcsin(np.clip(cr / np.maximum(mph * MPH_FPS, 1e-6), -1, 1)))
    # d(gamma)/dt is the closest observable to elevator input: the stick commands a
    # pitch rate, and in a wings-level dive pitch rate IS the flight-path rotation.
    dg = np.gradient(gamma, t)
    w = 7
    dg = np.convolve(dg, np.ones(w) / w, mode='same')

    ta, r, ncc = np.load(f'{AUD}/{AKEY}_scale.npy')
    med = np.median(sliding_window_view(np.pad(r, (3, 3), mode='edge'), 7), axis=1)
    base = np.median(med[(ta > LEVEL[0]) & (ta < LEVEL[1])])
    return t, alt, mph, cr, gamma, dg, np.interp(t, ta, med / base), np.interp(t, ta, ncc)


def main():
    t, alt, mph, cr, gamma, dg, note, ncc = series()
    print(f'{KEY}: n={len(t)}  baseline = level flight {LEVEL[0]}..{LEVEL[1]}s')
    print('\n     t     alt    mph    climb   gamma  dgamma/dt    note   ncc')
    for i in range(0, len(t), 6):
        print(f'  {t[i]:5.2f} {alt[i]:7.0f} {mph[i]:6.1f} {cr[i]*60:+8.0f} '
              f'{gamma[i]:+7.1f} {dg[i]:+10.1f} {note[i]:8.4f} {ncc[i]:5.2f}')

    print('\n=== phases (note relative to level flight)')
    PH = [('level        ', 0.5, 4.0), ('pushover     ', 4.6, 6.2),
          ('dive         ', 7.0, 11.5), ('deepest dive ', 11.0, 12.0),
          ('PULL-OUT     ', 12.3, 14.8), ('just after   ', 14.8, 16.0),
          ('recovered    ', 16.5, 19.5)]
    for name, a, b in PH:
        m = (t > a) & (t < b)
        print(f'  {name} {a:5.1f}..{b:4.1f}s  note {note[m].mean():.4f} '
              f'(min {note[m].min():.4f} max {note[m].max():.4f})  '
              f'gamma {gamma[m].mean():+6.1f} deg  dg {dg[m].mean():+6.1f} deg/s  '
              f'mph {mph[m].mean():5.1f}')

    print('\n=== the two claims in BL-109')
    dive = (t > 6.5) & (t < 12.0)
    print(f'  deepest note in the dive : {note[dive].min():.4f} @ t={t[dive][np.argmin(note[dive])]:.2f}s'
          f'   (claim: ~0.88, i.e. -12%)')
    post = (t > 12.0) & (t < 17.0)
    k = int(np.argmax(note[post]))
    print(f'  peak note at/after pullout: {note[post].max():.4f} @ t={t[post][k]:.2f}s'
          f'   (claim: ~1.05)')
    lvl = (t > 16.5) & (t < 19.5)
    print(f'  settles back to           : {note[lvl].mean():.4f}'
          f'   (overshoot is only real if the peak exceeds this AND the level baseline)')

    # gamma SATURATES at -90 deg through the dive: descent rate reaches airspeed
    # because the aircraft really is vertical, so arcsin clips and dgamma/dt there is
    # a clipping artifact (it reads 0 while pinned, then +/-60 deg/s as it unpins).
    # Any elevator comparison has to drop those frames or it is measuring the clip.
    unclipped = np.abs(cr / np.maximum(mph * MPH_FPS, 1e-6)) < 0.98
    print(f'\n=== which driver  ({unclipped.sum()}/{len(t)} frames; gamma-clipped dive '
          f'frames dropped)')

    def r2(x, m0):
        m = np.isfinite(x) & np.isfinite(note) & m0
        if m.sum() < 20:
            return np.nan
        return float(np.corrcoef(x[m], note[m])[0, 1] ** 2)
    for nm, x in [('climb rate', cr), ('airspeed', mph), ('gamma', gamma),
                  ('dgamma/dt (signed)', dg), ('|dgamma/dt| (elevator)', np.abs(dg))]:
        print(f'  note vs {nm:23s} R2 = {r2(x, unclipped):.3f}')

    print('\n=== is the dive floor just the overspeed whine (BL-252) leaking in?')
    print('    the whine gates on airspeed, so if it were the cause the note floor')
    print('    would follow speed. Two segments, both at the floor, 60 mph apart:')
    for a, b in [(6.5, 7.3), (9.0, 11.2)]:
        m = (t > a) & (t < b)
        print(f'   {a:5.1f}..{b:4.1f}s  mph {mph[m].mean():5.1f}  note {note[m].mean():.4f} '
              f'  tracker ncc {ncc[m].mean():.2f}')
    m = (t > 0.5) & (t < 4.0)
    print(f'   level ref     mph {mph[m].mean():5.1f}  note {note[m].mean():.4f} '
          f'  tracker ncc {ncc[m].mean():.2f}')


if __name__ == '__main__':
    main()
