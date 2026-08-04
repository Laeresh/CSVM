# Engine-note measurement (audio side)

Measures the **playback-rate multiplier** of the own-ship engine sample over time, from a capture's
audio track alone. Written for `CAP-10` / `BL-109` ("the original's engine drops ~12% through a dive
and overshoots ~1.05 at pull-out") but nothing here is CAP-10-specific.

## Why this method

The engine is a **looped sample played at a varying rate**, so its whole spectrum translates
*rigidly* in log-frequency. Whitening each frame's log-spectrum and cross-correlating it against a
reference frame recovers that translation directly — which is exactly the pitch multiplier, with
**no f0 estimate anywhere in the chain**.

That matters: a harmonic-product-spectrum tracker was tried first and abandoned because it
octave-hopped between 45.6 and 77 Hz on this material. Anything that has to pick a fundamental will
do the same. Cross-correlation never picks one.

## Scripts

All take an audio *key*, which names `<key>.wav` in the audio folder (see below).

| script | does |
|---|---|
| `scale.py <key>…` | **the measurement.** Writes `<key>_scale.npy` as rows `(t, ratio, ncc)`. |
| `curve.py <key>…` | re-anchors a saved run to level flight and prints the curve as a bar chart. |
| `spec.py <key>…` | plain spectrograms, 0–1200 Hz. |
| `resid.py <key>…` | **median-subtracted** spectrograms — only non-stationary energy survives, which is how a quiet moving harmonic is separated from a loud stationary bed. |

Always check the `ncc` column `scale.py` reports. It is the normalised cross-correlation peak, i.e.
the tracker's own confidence. Level flight runs ~0.62–0.69 on CAP-10 material; a sustained sag
toward ~0.5 means a competing sound has entered (the `prop_sound` overspeed whine does this in a
fast dive) and the ratio there is good to about ±1%, not better.

## Paths

Run **from the repo root**. Scripts read and write `playtest/CAP-10/audio` (`D` at the top of each
file) — extracted `.wav` inputs and the `_scale.npy` curves they produce. That directory is
gitignored: it is derived data, regenerable from the source videos with one `ffmpeg` call per clip
plus `scale.py`. The *scripts* live here, under `analysis/`, so that the method itself is version
controlled — an earlier revision kept them in `playtest/` and they were silently never committed,
while `backlog.md` and `docs/HISTORY.md` cited them as the method.

## The rest of the CAP-10 chain lives elsewhere

Anything that pairs this audio curve to **decoded flight state** needs the video harness, so it sits
next to it in `analysis/video-flight-calibration/` and imports `decode`/`extract` from there:

- `enginepitch.py` — pairs note to altimeter/speedometer on PTS, regresses candidate drivers
- `dutysweep.py` — the scripted-rig takes (`analysis/capture-rigs/ElevatorDutySweep.ahk`)
- `recovery.py`, `recoveryplot.py` — the dive-recovery take and its figure

Findings and traps: `docs/HISTORY.md` 2026-08-04 entries (a)–(e), `playtest/CAP-10/README.md`, and
`analysis/video-flight-calibration/FINDINGS.md` for the chase-HUD decode rules.
