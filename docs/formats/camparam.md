# camparam.json — the chase/third-person camera tuning

One of the shared-scope zrdr readers ([zrdr.md](zrdr.md)); in an extraction it lands as
`extracted/zrdr/camparam.zrd.json`. It carries the original's camera geometry and easing: chase
distance, catch-up rates, third-person eye height/pitch, and the geometry of the look-behind,
death, crash and flyby cameras.

## At a glance

This page is the current reference for its documented format family.

## At a glance

This page is the current reference for its documented format family.

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
in [markers.md](markers.md); in the engine it is `MarkerRig.PlayerAirframes`.

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

## The `default` block

| Key | Value | Reading |
|---|---|---|
| `dist` | 13.0 | Base chase distance, metres. |
| `dist_factor` | 0.01 | Metres of extra chase distance per m/s of airspeed: `d = dist + dist_factor·V`, V in metres per **sim** second. CAP-21's Bloodhawk staircase clips show: the measured slope 5.65e-4·d(0) per m/s implies 0.0105 against the shipped 0.01 — 5% agreement. |
| `dist_vary` | 0.1 | Undecoded — the amount the distance may swing by *something*; CAP-21's takes never move it. |
| `dist_min` / `dist_max` | 15.7 / 25.0 | The distance's range. ⚠ See below. |
| `dist_catch_up` | 1.0 | Rate at which distance eases to its target. Units undecoded. |
| `pos_catch_up` | 2.0 | Rate at which position eases. Units undecoded. |
| `look_catch_up` | 3.0 | Rate at which the aim eases. Units undecoded. |
| `thirdp_height` | 0.138 | Third-person eye height. Units unknown (not metres at this magnitude). |
| `thirdp_pitch` | 0.29 | Third-person pitch. As radians this is 16.6°. |
| `back_dist_min` / `_max` | 15.5 / 55.0 | The look-behind view's distance bounds. Ships with no base-distance sibling, so the engine reads it as bounds on the shared chase radius: the min bites for the smallest airframes (a Kestrel's 15.0 m dynamic radius is lifted to 15.5), the max never in practice. A reading from the data's shape, not a capture-verified decode — no look-behind footage exists. |
| `death_interval` | 2.0 | Seconds between death-camera re-frames. |
| `death_z` / `death_x` / `death_alt` / `death_min_alt` | 0 / 80 / 5 / 15.1 | Death-camera placement. |
| `crash_horiz` / `crash_y` | 30 / 45 | Crash-camera offset, metres: on a fatal crash the camera hard-cuts to `crash_horiz` m behind the impact (along the flight path's horizontal component) and `crash_y` m up, looking at the impact, then holds still. `C1 IA1 Crash.mp4` / `C1 IA1 Crash 2.mp4` show: instant cut, static elevated look-down (the implied 56° matches both clips), HUD hidden, and the near-vertical dive clip's overhead view is what the horizontal-component rule degenerates to in a dive. |
| `crash_chord_y` / `crash_elev` | 1000 / 40 | ⚠ Undecoded, deliberately unwired. `crash_elev` duplicates the vertical role `crash_y` fills and the footage cannot separate 45 from 40 (56° vs 53° of look-down); `chord_y`'s meaning is unknown. Capture-gated on `BL-260`. |
| `flyby_min_watch_time` / `_max_` | 3.8 / 4.3 | Seconds the flyby camera watches before moving. |
| `flyby_min_radius` / `_max_` | 5.5 / 7.0 | How close the flyby camera sits to the flight path. |
| `flyby_min_interval` / `_max_` | 1.9 / 2.3 | Seconds between flyby re-sites. |
| `flyby_min_switch_dist` / `_max_` | 70 / 85 | Distance at which the flyby camera hands over. |
| `flyby_z` / `flyby_y` / `flyby_min_alt` | 0 / 0.1 / 0.1 | Flyby placement offsets. |

## Open questions

⚠ **`dist_min` (15.7) is LARGER than `dist` (13.0) in the `default` block.** In all seven
per-plane blocks the two are equal instead. So the rule cannot be "clamp `dist` into
`[dist_min, dist_max]`" — under that reading no plane would ever sit at the default's own 13.0.
CAP-21 does not settle it either: the realised distances (18.3–22.5 on the Bloodhawk) never reach
`dist_max` 25.0, and the one dip under `dist_min` is inside the measurement's ~1% systematic. The
engine's dynamic distance (`d = dist + dist_factor·V` + a measured throttle transient, `BL-248`)
is therefore deliberately **unclamped**.

⚠ **The catch-up triplet's units are unknown.** `pos_catch_up` 2.0 / `look_catch_up` 3.0 /
`dist_catch_up` 1.0 read plausibly as `1/s` exponential-smoothing rates, which is the shape the
engine's own camera smoothing uses — but they could equally be frame counts or seconds-to-settle.
CAP-21's one measured rate — the throttle transient's relaxation, 0.65 /sim-s — matches **none**
of the three (`dist_catch_up` 1.0 is the nearest at 1.54×), so the engine carries that rate as a
measured constant rather than reading it from this triplet. Whether `dist_catch_up` is *meant* to
be that rate (with something else costing the missing 35%) is untested.

⚠ **`thirdp_height`'s units are unknown**, which is why the engine takes only the *radius* from
this file and leaves the chase offset's *direction* as a hand-picked value. `thirdp_pitch` 0.29 rad
= 16.6° sits suggestively close to the engine's hand-picked 15.7° elevation, but one near-match is
not a decode.

## The throttle transient — measured off CAP-21, authored nowhere

Beyond the authored `d = dist + dist_factor·V`, the original's chase distance carries a **transient
in the along-path acceleration**: slam the throttle open and the camera falls back, cut it and the
camera closes in, both relaxing back onto the speed law. Every number below is **measured off the
Bloodhawk staircase clips (`CAP-21`, `BL-248`)** — none of it is decoded from `crimson.exe`, and
none of it matches an authored constant in this file.

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

⚠ **This rate is a measured constant, not a reading of `dist_catch_up`.** See the catch-up warning
above: 0.65 /sim-s matches none of the three authored rates, and `dist_catch_up` 1.0 — the nearest —
is 1.54× it.

⚠ **Do not wire `dist_min`/`dist_max` as a clamp on the dynamic radius.** The realised distance is
`dist + dist_factor·V` plus this transient, deliberately **unclamped**; the default block's own
`dist` 13.0 sits *below* its `dist_min` 15.7, and the footage's realised distances never reach
`dist_max`. `dist_vary` has no identified input either — the capture footage never moves it.

## What the engine reads

`CSVM/src/Flight/CamParams.cs` parses the whole file and exposes every field;
`CSVM/src/Flight/CameraController.cs` applies:

- `dist` + `dist_factor` — the dynamic chase radius (shared by the numpad fixed views, so both
  cameras move together — dynamics included). The throttle transient's 0.65 /sim-s relaxation
  there is a CAP-21 **measurement**, not a field of this file.
- `crash_horiz` / `crash_y` — the crash camera's hard-cut pose (`CrashView`).
- `back_dist_min` / `back_dist_max` — the look-behind view's distance bounds (`BackView`,
  numpad 0 / `--view=back`).

The death and flyby cameras stay **capture-gated dormant**: their triggers exist to decode
(`death_z`/`death_x`/`death_alt`/`death_min_alt` are placement magnitudes with unknown axes,
and the flyby's 12 fields describe a re-siting roadside pass) but no death or flyby footage is
on disk — `BL-260` owes those captures. Everything else is carried deliberately dormant behind
the warnings above.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
