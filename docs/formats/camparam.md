# Camera tuning - `camparam.json`

Part of the [format documentation](README.md). This shared zrdr reader defines chase distance,
catch-up rates, third-person eye height and pitch, and look-behind, death, crash, and flyby camera
geometry.

## Contents

- [Shape](#shape)
- [Per-plane overrides](#per-plane-overrides)
- [Default block](#default-block)
- [Static death, crash, and flyby cameras](#static-death-crash-and-flyby-cameras)
- [The distance law](#the-distance-law)
- [Known limits](#known-limits)
- [Throttle transient](#throttle-transient)
- [Engine-read fields](#engine-read-fields)
## Shape

The root is a plain alternating `name, properties` list — the same layout `vehicle.json` uses for
its defs, with no wrapping list. Every value is a one-element list of a float:

```
[ "default", [ "dist", [13.0], "dist_factor", [0.01], … ],
  "Bloodhawk", [ "dist", [18.5], "dist_min", [18.5], "dist_max", [25.0] ],
  … ]
```

`default` comes first and carries every key. Each later block is one aircraft and carries **only
the keys it overrides** — three keys for most, five for the Balmoral. Resolution is therefore
`default` first, then the plane's own block layered on top.

⚠ **The blocks are keyed by the aircraft's DISPLAY name** — `"Bloodhawk"`, `"Firebrand"` — not by
the model node (`player_bhawk`) and not by the vehicle def (`pbloodhawk`). The node→display map is
in [markers.md](markers.md); in the engine it is `MarkerRig.PlayerAirframes`. Do not substitute
`PlaneRoster.PlaneDisplayName`: it strips a leading `p` and title-cases, which yields `"Fbrand"` for
`player_fbrand` and silently drops the Firebrand's override.

## Per-plane overrides

Seven of the eleven player airframes carry a block. The other four — Devastator, Hoplite,
Hellhound and Brigand — have none and take `default`'s 13.0.

| Aircraft | `dist` | `dist_min` | `dist_max` | also |
|---|---|---|---|---|
| Bloodhawk | 18.5 | 18.5 | 25.0 | |
| Fury | 17.0 | 17.0 | 25.0 | |
| Peacemaker | 18.0 | 18.0 | 30.0 | |
| Kestrel | 14.5 | 14.5 | 30.0 | |
| Firebrand | 20.5 | 20.5 | 30.0 | |
| Warhawk | 20.0 | 20.0 | 35.0 | |
| Balmoral | 25.0 | 25.0 | 30.0 | `thirdp_height` 0.2, `thirdp_pitch` 0.2 |

The distance tracks airframe size: the Kestrel is the smallest number and the Balmoral — a heavy
two-turret aircraft — the largest.

## Default block

| Key | Value | Reading |
|---|---|---|
| `dist` | 13.0 | Base chase distance, metres. |
| `dist_factor` | 0.01 | Metres of extra chase distance per m/s of airspeed: `d = dist + dist_factor·V`, V in metres per **sim** second. CAP-21's Bloodhawk staircase clips show: the measured slope 5.65e-4·d(0) per m/s implies 0.0105 against the shipped 0.01 — 5% agreement. |
| `dist_vary` | 0.1 | The throttle transient's gain: metres of extra distance per unit of the gap between speed and its own lagged copy. See "The distance law". |
| `dist_min` / `dist_max` | 15.7 / 25.0 | The bounds the speed-driven distance is held inside for a forward-facing camera, so `dist_min` is also the pose the view rests at. See "The distance law". |
| `dist_catch_up` | 1.0 | The rate, per frame-second, at which the lagged speed copy `dist_vary` works against eases toward the real one, so also the throttle transient's relaxation rate. |
| `pos_catch_up` | 2.0 | Rate at which position eases, in the same exponential and the same clock. |
| `look_catch_up` | 3.0 | Rate at which the aim eases, likewise. |
| `thirdp_height` | 0.138 | Third-person eye height. Units unknown (not metres at this magnitude). |
| `thirdp_pitch` | 0.29 | Third-person pitch, in **degrees**: the reader multiplies it by π/180 on the way into the block, and the placement adds the result to the camera's smoothed elevation. 0.29° is a hair of tilt, not the 16.6° that reading the file's number as radians would suggest. |
| `back_dist_min` / `_max` | 15.5 / 55.0 | The look-behind view's distance bounds. Ships with no base-distance sibling, so the engine reads it as bounds on the shared chase radius: the min bites for the smallest airframes (a Kestrel's 15.0 m dynamic radius is lifted to 15.5), the max never in practice. A reading from the data's shape, not a capture-verified decode — no look-behind footage exists. |
| `death_interval` | 2.0 | Seconds of velocity projection in the death-camera placement: `speed · death_interval` becomes the third local offset component. It is not a re-frame timer. |
| `death_z` / `death_x` | 0 / 80 | Longitudinal addition / radius of the random local-plane offset used for the death camera. |
| `death_alt` / `death_min_alt` | 5 / 15.1 | World-Y addition / absolute world-Y floor applied after the local death-camera offset is transformed through the aircraft basis. |
| `crash_horiz` / `crash_y` | 30 / 45 | Crash-camera offset, metres: on a fatal crash the camera hard-cuts to `crash_horiz` m behind the impact (along the flight path's horizontal component) and `crash_y` m up, looking at the impact, then holds still. `C1 IA1 Crash.mp4` / `C1 IA1 Crash 2.mp4` show: instant cut, static elevated look-down (the implied 56° matches both clips), HUD hidden, and the near-vertical dive clip's overhead view is what the horizontal-component rule degenerates to in a dive. |
| `crash_chord_y` / `crash_elev` | 1000 / 40 | Shared static-camera world-collision probe: ray-start height above the candidate / clearance above the highest hit. The retail executable names the second key `crash_min_elev`; the extracted reader exposes it as `crash_elev`. Applies to crash, death, and flyby placement. |
| `flyby_min_watch_time` / `_max_` | 3.8 / 4.3 | Random seconds the flyby camera must watch before distance may request a re-site. |
| `flyby_min_radius` / `_max_` | 5.5 / 7.0 | How close the flyby camera sits to the flight path. |
| `flyby_min_interval` / `_max_` | 1.9 / 2.3 | Random seconds of velocity projection in the flyby placement: `speed · interval` becomes longitudinal distance. It is not time between re-sites. |
| `flyby_min_switch_dist` / `_max_` | 70 / 85 | Random distance threshold tested after the watch deadline; exceeding it requests a re-site on the next frame. |
| `flyby_z` / `flyby_y` / `flyby_min_alt` | 0 / 0.1 / 0.1 | Flyby placement offsets. |

## Static death, crash, and flyby cameras

All three cameras first calculate a candidate world position and then pass its Y coordinate through
one shared world-collision clearance rule. `crash_chord_y` raises the start of a vertical ray above the
candidate; the ray ends 1000 m below the candidate. When it hits terrain, the candidate rises to at
least `highest_hit_y + crash_elev`. The executable calls the latter field `crash_min_elev`, which
describes its role; `crash_elev` is the extracted reader's spelling. The rule is `FUN_0042c390`,
called through `FUN_0042c580` by crash (`0042e041`–`0042e04e`), death
(`0042e1d7`–`0042e1e4`), and flyby (`0042e387`–`0042e394`).

The death camera is mode 8. On entry it chooses one fixed world point:

1. Draw an angle uniformly around the aircraft and form the local offset
   `(death_x·cos θ, death_x·sin θ, −(speed·death_interval + death_z))`.
2. Transform that offset through the aircraft basis and add the aircraft world position.
3. Add `death_alt` to world Y, clamp Y to `death_min_alt`, then apply the shared clearance.

`FUN_0042e0b0` performs the placement (`0042e0c3`–`0042e1e4`). The camera holds the resulting
world point and re-aims at the aircraft every frame; it has no timer-driven re-frame. Mode 8 is
entered for the destroyed player when callback event `0x0f` consumes the armed death-camera flag
(`00470912`–`0047093c` and `0048072a`–`00480794`). Reset/respawn leaves it for mode 6.

The flyby camera is mode 9. Each re-site (`FUN_0042e1f0`, `0042e1f0`–`0042e3f6`):

1. Draws a local angle outside the ±15° forward/back exclusion wedges and a radius uniformly from
   `flyby_min_radius..flyby_max_radius`.
2. Draws `interval` uniformly from `flyby_min_interval..flyby_max_interval` and uses
   `−(speed·interval + flyby_z)` as the longitudinal offset.
3. Transforms the offset through the aircraft basis, adds `flyby_y` to world Y, clamps to
   `flyby_min_alt`, and applies the shared clearance.
4. Draws a watch duration and switch distance independently from their authored min/max pairs.

`FUN_0042db40` holds that world point and re-aims at the moving aircraft each frame. After the watch
deadline, distance beyond the chosen switch threshold requests a re-site; because the re-site check
precedes the distance check, the move occurs on the next frame. Every re-site redraws all four
random choices. Mode 9 has no internal exit; another camera-mode transition ends it.

`FUN_0042f700` maps the raw floats without conversion: death fields occupy table offsets
`+0x30..+0x40`, crash placement/clearance `+0x44..+0x50`, and flyby `+0x54..+0x7c`. Position fields
are metres. The two `*_interval` values are seconds because they multiply speed to produce metres;
only `flyby_*_watch_time` is a dwell.

The vector-component formulas and basis transform are exact. Calling the third local component
fore/aft is an interpretation of the resulting camera behaviour; the executable does not expose a
friendly name for that basis axis.

## The distance law

The engine builds an external camera's distance in three steps, decoded in
[`../org/cameraViews.md`](../org/cameraViews.md), "The external camera's distance, and the zoom
axis that only goes outward": a speed law with an acceleration term, a clamp into an authored
bounds pair, then the pilot's zoom added outward on top. What that settles about this file:

- **`dist_min`/`dist_max` clamp the speed-driven distance**, and the pair is chosen by where the
  camera points: forward-facing takes these two, the look-behind takes `back_dist_min`/`_max`, and
  a partly swung view takes the linear blend. So the `default` block's `dist` 13.0 sitting *below*
  its own `dist_min` 15.7 is not a contradiction: the four airframes with no block of their own
  ride the near bound at 15.7 rather than 13.0, and the seven with a block author `dist` equal to
  `dist_min` so that the bound and the rest pose are the same number.
- **`dist_vary` is the throttle transient's gain and `dist_catch_up` its relaxation rate.** The
  term is `dist_vary·(V − V̄)`, with `V̄` a lagged copy of speed eased at `dist_catch_up` on the
  engine's per-frame dt. Under steady acceleration that settles at `dist_vary/dist_catch_up`
  metres of excess per m/s², i.e. the shipped 0.1, against CAP-21's measured **0.105** — 5%
  agreement. The clip's raw relaxation figure, **0.90 per wall-second**, is within 10% of the
  shipped `dist_catch_up` 1.0 in the same clock.
- **`pos_catch_up` 2.0 and `look_catch_up` 3.0** are the position and aim easing rates in the same
  exponential, scaled up by how far the view is swung off the flight path.

⚠ **`dist_max` is not the far end of the pilot's zoom.** The zoom is a flat 10 m added after the
clamp, so the travel available runs from `dist_min` out to `dist_min + 10`, and a fast enough
aircraft sits at `dist_max + 10` with the axis fully out.

## Known limits

⚠ **`thirdp_height`'s units are unknown**, which is why the engine takes only the *radius* from
this file and leaves the chase offset's *direction* as a hand-picked value. `thirdp_pitch` is no
help there either: at 0.29° it is far too small to be the offset's own elevation.

## Throttle transient

Beyond the authored `d = dist + dist_factor·V`, the original's chase distance carries a **transient
in the along-path acceleration**: slam the throttle open and the camera falls back, cut it and the
camera closes in, both relaxing back onto the speed law. Every number below is **measured off the
Bloodhawk staircase clips (`CAP-21`)**, and it is what the engine carries today. The mechanism is
now decoded as `dist_vary`/`dist_catch_up` (see "The distance law"), which the measurements
corroborate to 5% and 10%; swapping the engine over to the two authored fields is `BL-816`.

| Quantity | Measured | Note |
|---|---|---|
| Relaxation rate | **0.65 /sim-s** (τ = 1.55 sim-s) | first-order decay of the excess distance |
| — the same, in wall time | τ = **1.11 wall-s** (0.90 /wall-s) | the raw clip figure, before conversion |
| Wall→sim conversion | **k = 1.390** | the project-wide constant, same one the STALL lamp's dwells use |
| Steady-state excess | **+0.28 % of `d` per mph/sim-s** = **0.105 m per m/s²** | residual-vs-`dV/dt` correlation **−0.79 to −0.85** in all four takes |
| Peak excursion | **≈ +15 % of the radius** on a full-throttle slam, **≈ −7 %** on a full cut | this is the part the eye actually sees |

⚠ **Apply it on the SIM clock.** Using the wall figure (0.90 /wall-s) runs the relaxation **39 %
fast** — the same 1.390 trap that governs every measured dwell in this project (verification
`DET-11`). The engine advances the radius once per sim step, never per render frame, so the
acceleration derivative is clean and a halted or crashed sim freezes the radius with everything
else.

⚠ **The engine's own rate is the measured one, on the sim clock.** The decode puts the original's
relaxation at `dist_catch_up` on its per-frame dt, and the raw clip figure (0.90 /wall-s) is the
one that matches it; the sim-converted 0.65 does not. Which clock CSVM should run it on is part of
`BL-816`, so until that lands the two figures must not be mixed.

## Engine-read fields

`CSVM/src/Flight/CamParams.cs` parses the whole file and exposes every field;
`CSVM/src/Flight/CameraController.cs` applies:

- `dist` + `dist_factor` — the dynamic chase radius (shared by the numpad fixed views, so both
  cameras move together — dynamics included). The throttle transient's 0.65 /sim-s relaxation
  there is a CAP-21 **measurement**, not yet a reading of `dist_vary`/`dist_catch_up` (`BL-816`).
- `dist_min` / `dist_max` — the bounds that radius is held inside for every forward-facing pose,
  and so the pose the view rests at (`ExternalRadius`).
- `crash_horiz` / `crash_y` — the crash camera's hard-cut pose (`CrashView`).
- `back_dist_min` / `back_dist_max` — the look-behind view's distance bounds (`BackView`,
  numpad 0 / `--view=back`), which take no zoom.

The death and flyby cameras are decoded but remain dormant in CSVM. The original's F7 "Access Chase
View" enters flyby mode 9; destroyed-player callback event `0x0f` enters death mode 8. Captures are
useful after implementation to judge their presentation, but they do not supply any field meaning
or placement constant. See [`docs/org/cameraViews.md`](../org/cameraViews.md) for the camera-state
dispatch and lifecycle.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
