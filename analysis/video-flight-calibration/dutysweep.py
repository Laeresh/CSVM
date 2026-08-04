"""CAP-10 scripted duty sweep: engine note against KNOWN elevator input.

`capture-rigs/ElevatorDutySweep.ahk` alternates nose-up/nose-down at a fixed period
and steps the input duty cycle, logging every key edge. So for this take the elevator
is known rather than inferred, which is the one thing the eight hand-flown takes could
not supply (docs/HISTORY.md 2026-08-04 (c)).

Alignment follows capture-rigs rule 1 - one offset for the whole run. It is solved
rather than eyeballed: the drive puts a 1/period oscillation into the altimeter whose
amplitude follows the duty staircase, so cross-correlating that envelope against the
logged duty recovers the offset.
"""
import re
import sys

import numpy as np
from scipy.signal import butter, filtfilt, hilbert

from decode import CACHE

AUD = 'playtest/CAP-10/audio'
RIG = 'analysis/capture-rigs'


def read_log(path):
    """-> (period_s, [(t_begin, duty)], t_stop)"""
    steps, t_stop, period = [], None, None
    for line in open(path, encoding='utf-8'):
        if line.startswith('#'):
            m = re.search(r'period_ms=(\d+)', line)
            if m:
                period = int(m.group(1)) / 1000.0
            continue
        m = re.match(r'([\d.]+),step,begin,duty=([\d.]+)', line)
        if m:
            steps.append((float(m.group(1)), float(m.group(2))))
        m = re.match(r'([\d.]+),run,stop', line)
        if m:
            t_stop = float(m.group(1))
    return period, steps, t_stop


def duty_at(steps, t_stop, tl):
    """Logged duty as a function of log time (0 outside the run)."""
    d = np.zeros_like(tl)
    for i, (t0, duty) in enumerate(steps):
        t1 = steps[i + 1][0] if i + 1 < len(steps) else t_stop
        d[(tl >= t0) & (tl < t1)] = duty
    d[(tl < steps[0][0]) | (tl >= t_stop)] = 0.0
    return d


def envelope(t, alt, f0, bw=0.45):
    """Amplitude of the drive-frequency component of altitude."""
    fs = 1.0 / np.median(np.diff(t))
    b, a = butter(3, [max(f0 - bw, 0.15) / (fs / 2), (f0 + bw) / (fs / 2)], btype='band')
    e = np.abs(hilbert(filtfilt(b, a, alt - np.polyval(np.polyfit(t, alt, 3), t))))
    # smooth over ~1.5 s so the envelope tracks the staircase, not each cycle
    w = max(3, int(1.5 * fs) | 1)
    return np.convolve(e, np.ones(w) / w, mode='same')


def solve_offset(t, env, steps, t_stop, lo=0.0, hi=20.0):
    """Video time of log t=0, by matching the envelope to the duty staircase."""
    best, bestr = None, -np.inf
    for off in np.arange(lo, hi, 0.02):
        d = duty_at(steps, t_stop, t - off)
        if d.std() < 1e-9:
            continue
        r = float(np.corrcoef(d, env)[0, 1])
        if r > bestr:
            bestr, best = r, off
    return best, bestr


def main(key='cap10f13', log=None, tmax=60.0):
    period, steps, t_stop = read_log(f'{RIG}/{log}')
    t = np.load(f'{CACHE}/{key}_pts.npy')
    alt = np.load(f'{CACHE}/{key}_alt2.npy')
    n = min(len(t), len(alt))
    t, alt = t[:n], alt[:n]
    ok = t < tmax                       # the gauges vanish when the run ends
    t, alt = t[ok], alt[ok]

    env = envelope(t, alt, 1.0 / period)
    off, r = solve_offset(t, env, steps, t_stop)
    print(f'{key}: period {period*1000:.0f} ms, {len(steps)} steps, log stop {t_stop:.2f}s')
    print(f'  alignment: log t=0 is video t={off:.2f}s   (envelope-vs-duty r={r:+.3f})')

    ta, ra, _ = np.load(f'{AUD}/{key.replace("cap10", "")}_scale.npy')
    from numpy.lib.stride_tricks import sliding_window_view
    med = np.median(sliding_window_view(np.pad(ra, (3, 3), mode='edge'), 7), axis=1)
    base = np.median(med[(ta > off + steps[0][0]) & (ta < off + steps[1][0])])  # duty 0
    pitch = np.interp(t, ta, med / base)

    # The control check the design promises: if climb rate or airspeed tracks duty,
    # the sweep did NOT isolate elevator and the staircase means nothing.
    mph = np.load(f'{CACHE}/{key}_mph2.npy')[:n][ok]
    cr = np.full(len(t), np.nan)
    for i in range(len(t)):
        j0 = np.searchsorted(t, t[i] - 0.5)
        j1 = min(len(t) - 1, np.searchsorted(t, t[i] + 0.5))
        if j1 > j0:
            cr[i] = (alt[j1] - alt[j0]) / (t[j1] - t[j0]) * 60

    print(f'\n  {"duty":>5s} {"window (video s)":>20s} {"note":>9s} {"sd":>7s}'
          f' {"climb":>9s} {"mph":>7s}')
    rows = []
    for i, (t0, duty) in enumerate(steps):
        t1 = steps[i + 1][0] if i + 1 < len(steps) else t_stop
        m = (t > off + t0 + 1.5) & (t < off + t1 - 0.3)   # drop the settling edge
        if m.sum() < 10:
            continue
        rows.append((duty, pitch[m].mean(), np.nanmean(cr[m]), mph[m].mean()))
        print(f'  {duty:5.2f} {off+t0+1.5:9.2f}..{off+t1-0.3:6.2f} '
              f'{pitch[m].mean():9.4f} {pitch[m].std():7.4f} '
              f'{np.nanmean(cr[m]):+9.0f} {mph[m].mean():7.1f}')

    dd = np.array([r[0] for r in rows])
    nn = np.array([r[1] for r in rows])
    cc = np.array([r[2] for r in rows])
    vv = np.array([r[3] for r in rows])
    print(f'\n  CONTROLS  duty vs climb R2 = {np.corrcoef(dd, cc)[0,1]**2:.3f}'
          f'   duty vs airspeed R2 = {np.corrcoef(dd, vv)[0,1]**2:.3f}')
    print(f'  ACROSS THE 9 STEPS   note vs duty  R2 = {np.corrcoef(dd, nn)[0,1]**2:.3f}'
          f'   note vs climb R2 = {np.corrcoef(cc, nn)[0,1]**2:.3f}'
          f'   note vs airspeed R2 = {np.corrcoef(vv, nn)[0,1]**2:.3f}')

    d = np.array([r[0] for r in rows])
    p = np.array([r[1] for r in rows])
    g, o = np.polyfit(d, p, 1)
    print(f'\n  note ~= {o:.4f} + {g:+.4f} * duty     R2 = {np.corrcoef(d, p)[0,1]**2:.3f}')
    print(f'  span across the sweep: {p.max()-p.min():+.4f} '
          f'({(p.max()-p.min())*100:+.2f}% of level)')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'cap10f13',
         sys.argv[2] if len(sys.argv) > 2 else 'elevator-duty-sweep-20260804-124528-log.txt')
