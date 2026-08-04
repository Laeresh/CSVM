"""Plot the CAP-10 dive-recovery take: engine note against the decoded flight state.

The figure is the argument in one picture. Two vertical bands mark the only two
moments the stick moves hard: the pushover and the pull-out. The note bumps UP at
both -- including the pushover, where the aircraft is still at its peak altitude and
its level-flight airspeed -- while the slow level of the note goes DOWN through the
sustained dive. A single climb-rate term cannot make both happen.

Run from the repo root with analysis/video-flight-calibration on PYTHONPATH.
"""
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

from recovery import series, LEVEL

OUT = 'playtest/CAP-10/dive-recovery.png'
PUSH = (4.3, 5.2)
PULL = (12.6, 14.3)

t, alt, mph, cr, gamma, dg, note, ncc = series()

fig, ax = plt.subplots(3, 1, figsize=(11, 9), sharex=True,
                       gridspec_kw=dict(height_ratios=[2, 1, 1]))

for a in ax:
    a.axvspan(*PUSH, color='tab:orange', alpha=0.16, lw=0)
    a.axvspan(*PULL, color='tab:green', alpha=0.16, lw=0)
    a.grid(alpha=0.25)

ax[0].plot(t, note, color='k', lw=1.4)
ax[0].axhline(1.0, color='tab:blue', ls='--', lw=1,
              label=f'level-flight baseline ({LEVEL[0]}-{LEVEL[1]} s)')
ax[0].axhline(0.88, color='tab:red', ls=':', lw=1, label='BL-109 claim: -12% in the dive')
ax[0].axhline(1.05, color='tab:red', ls=':', lw=1, label='BL-109 claim: 1.05 at pull-out')
k = int(np.argmax(np.where((t > 12) & (t < 17), note, -np.inf)))
ax[0].annotate(f'pull-out peak {note[k]:.4f}', (t[k], note[k]),
               xytext=(t[k] + 1.2, note[k] + 0.022),
               arrowprops=dict(arrowstyle='->', lw=1))
k2 = int(np.argmax(np.where((t > 4) & (t < 5.5), note, -np.inf)))
ax[0].annotate(f'pushover peak {note[k2]:.4f}\n(nose DOWN, and still at\nlevel-flight speed/altitude)',
               (t[k2], note[k2]), xytext=(0.15, 0.985), textcoords=fig.transFigure,
               arrowprops=dict(arrowstyle='->', lw=1), va='top', fontsize=9)
# a third stick event, mid-dive: climb rate is steepening straight through it, so a
# climb-rate term predicts the note keeps falling. It rises 6% instead.
k4 = int(np.argmax(np.where((t > 7.2) & (t < 8.0), note, -np.inf)))
ax[0].annotate(f'mid-dive input {note[k4]:.4f}\n(climb still steepening)',
               (t[k4], note[k4]), xytext=(t[k4] - 1.1, note[k4] + 0.030),
               arrowprops=dict(arrowstyle='->', lw=1), fontsize=9, ha='center')
k3 = int(np.argmin(np.where((t > 6) & (t < 12), note, np.inf)))
ax[0].annotate(f'dive floor {note[k3]:.4f}', (t[k3], note[k3]),
               xytext=(t[k3] + 1.0, note[k3] - 0.022),
               arrowprops=dict(arrowstyle='->', lw=1))
ax[0].set_ylabel('engine note\n(x level flight)')
ax[0].legend(loc='lower right', fontsize=8)
ax[0].set_title('CAP-10 Dive Recovery - the note bumps UP at every stick event, '
                'and sits DOWN through the sustained dive', pad=42)

ax[1].plot(t, cr * 60, color='tab:purple', lw=1.3)
ax[1].axhline(0, color='k', lw=0.7)
ax[1].set_ylabel('climb rate\n(ft/min)')

ax[2].plot(t, mph, color='tab:brown', lw=1.3)
ax[2].axhline(mph[(t > LEVEL[0]) & (t < LEVEL[1])].mean(), color='tab:blue', ls='--', lw=1)
ax[2].set_ylabel('airspeed\n(mph)')
ax[2].set_xlabel('video time (s, PTS)')

fig.text(0.98, 0.995, 'orange = pushover   green = pull-out', fontsize=9, color='0.35',
         ha='right', va='top')
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig(OUT, dpi=110)
print(f'wrote {OUT}')
