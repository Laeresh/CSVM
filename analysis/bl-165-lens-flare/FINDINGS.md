# BL-165 — the sun's lens flare: what the data settled, and how the rings were calibrated

Worked 2026-08-08/09 on `worktree-bl-165-lens-flare`. The measured spec is `playtest/CAP-13/`
(git-ignored — read it at `Z:\CSVM\playtest\CAP-13\`, never linked into a worktree). This file
holds what that spec does **not**: the decode that anchors the flare, and the calibration method,
so neither is re-derived.

## The sun is a world object, in two chapters only

`nodes.json` carries a node literally named `sun` in **C2 and C3 and nowhere else** — C3 at index
3238, `zone_id: 1`, `model_index: 491`, parented into the `horizon` node's zone subtree, i.e. part
of the camera-anchored skydome. C2 has one at `model_index: 389`. A texture named `sun` ships in
those same two chapters only, and `support\<ch>\init.gw` registers `LensFlareTexture` slots in
those same two chapters only — **three independent agreements**.

So the backlog entry's "open oddity" (only C2/C3 register flare textures; a visible sun was found
only in C3) is not an oddity. It is the rule, and C1/C4 being overcast and C5 night explains why
only C3 showed one on the chapter sweep.

**Model 491 is a single 170.24 × 170.24 quad** (4 verts, 1 polygon, material 268). The ±103.41
figure in `nodes.json` is the symmetric *bounding* box, not the quad — reading it as the quad
overestimates the disc by ~1.2× and was an early wrong turn here.

### ⚠ Do not anchor the flare to `SUNLIGHT_ORIENTATION`

`weather.zrd.json` authors a per-zone `SUNLIGHT_ORIENTATION [pitch, yaw, roll]°` in **every**
chapter (census: C1 `[-25,90]`, C1B/C1C/C2/C2B `[-65,90]`, C3 `[-25,135]`, C4 `[-45,135]`,
C5 `[-25,-135]`; effectively constant across a chapter's zones). It is the *shading* direction and
it is **not** where the sun object is:

| | C3 sun bearing |
|---|---|
| `sun` node translate (1462.7, 1420.7, −1471.4), read Y-up | **yaw 45°**, elevation 34° |
| `SUNLIGHT_ORIENTATION` | **yaw 135°** |

90° apart. Anchoring the flare to the weather orientation draws it in an empty quarter of the sky
with the disc plainly elsewhere in frame. Anchor to the node.

Verified by prediction, not search: a freecam aimed at yaw 45 / elev 34 puts the disc within a few
pixels of frame centre (`--freecam --chapter=C3 --no-fog "--pos=0,1200,0"
"--direction=0.586,0.559,-0.586"`). Measured disc: **34 px visible, 22 px saturated core** at
1280×720 / FOV 62, against ~45 px of quad — `sun.png`'s disc fills ~75 % of its quad.

Separately: our `DirectionalLight3D` is still hardcoded to `(-45, 150, 0)` in
`Launcher.SetupLighting` and reads no authored orientation at all. That is a real gap, but it is
*not* this item — the flare no longer depends on it.

## Slot → element

`init.gw` binds `lflare1`..`lflare4` to slots 0..3, and slot order ascends along the
sun→screen-centre vector. Corroborated independently by what the textures actually look like
(composite them on black — they are near-invisible on white):

| Slot | Texture | Appearance | CAP-13 element |
|---|---|---|---|
| 0 | `lflare1` | filled core glow | core, at the sun |
| 1 | `lflare2` | soft wide dim ring | Ring A, frac 0.50, Δlum 10–12 |
| 2 | `lflare3` | crisp bright ring (only 32² texture) | Ring B, frac 0.90, Δlum 14–21, "crispest/brightest" |
| 3 | `lflare4` | thin large faint ring | Ring C, frac 2.0, Δlum 3–9, faintest |

`bigflare01` is an **8-spoke starburst** and `bigflare02` a **horizontal streak bar** — both
excluded on appearance, not merely unproven, since CAP-13's headline finding is that the flare has
**no streaks**. Treat slot-order-is-element-order as a fact about this four-element rig, not a
decoded rule about the format.

## Calibrating the ring intensities

`measure_flare.py` here takes one of our screenshots and reports each ring's Δlum, in the same
terms `playtest/CAP-13/measure.py` reports the original's. Two things must match or the comparison
is meaningless — both were got wrong first time here:

1. **The pose.** CAP-13's ring Δlum were read off frames that already carried the wash, and the
   wash composites toward white, so a true difference `d` reads as `d·(1−α)`. Solving back from
   `measure.py`'s own probe coordinates, its two ring poses sit at **311 px** and **340 px** from
   centre. Measure ours inside that band rather than applying a correction factor.
2. **The statistic.** `measure.py`'s `sample()` reports the **brightest pixel** in a window on the
   ring. A median around the annulus is the more robust *locator* but reads systematically lower;
   calibrating a median up to a brightest-pixel target overshoots. `measure_flare.py` prints both
   and calibrates on the peak.

Two traps in the measurement itself, both of which produced a confident `Δ = +0.0`:

- Locating the sun by `argmax` lands wherever the saturated plateau breaks ties — 16 px off here —
  and that error enters ring 2.0's predicted centre **doubled**. Use the centroid of the saturated
  core.
- With the sun directly above/below centre, ring 2.0 clips the frame edge and the sampling circle
  reads clamped pixels. Place the sun **diagonally**.

### Result

Pose: `--freecam --chapter=C3 --no-fog "--pos=0,1200,0" "--direction=0.1635,0.8290,-0.5348"`,
1280×720 — sun 328 px from centre, inside the 311–340 band.

| Ring | Target | Before | After | Intensity |
|---|---|---|---|---|
| 0.50 | 10–12 | 8.7 | **10.7** | 0.30 → **0.38** |
| 0.90 | 14–21 | 17.3 | 17.3 | 0.45 (untouched) |
| 2.00 | 3–9 | 5.0 | 5.0 | 0.18 (untouched) |

Only Ring A moved. The other two landed mid-band and were deliberately **not** nudged toward the
middle: the bands are the spread across two poses, not error bars, so centring them would be
fitting to the estimator rather than to the original.

## Still open

- **C2's flare is predicted, never verified** — the data says C2 has one; there is no footage.
- **The additive-blend call overrules one measurement.** The ring annulus reads (194,226,254)
  against a 200 sky — red *below* background, which additive cannot produce. Treated as capture
  noise (6/255, on a compressed frame beside a saturated highlight). Weighed against it: the core
  is measured as saturating and blooming 63→86 px, and Ring C vanishes over bright deck murk. If
  the intensities ever cannot hit target without blowing out the core, revisit this first.
- **Centre-ray occlusion is the simpler of two readings** the footage cannot distinguish (a
  multi-sample disc test fits equally). Chosen because it needs no invented coverage threshold.
  A sphere/shape cast is *not* the fix: it triggers on any overlap, which would kill the flare on
  partial cover and contradict t=18.5.
- **Cockpit camera untested** (the entry's own trap (c)).
