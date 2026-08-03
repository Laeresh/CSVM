# camparam.json — the chase/third-person camera tuning

One of the shared-scope zrdr readers ([zrdr.md](zrdr.md)); in an extraction it lands as
`extracted/zrdr/camparam.zrd.json`. It carries the original's camera geometry and easing: chase
distance, catch-up rates, third-person eye height/pitch, and the geometry of the look-behind,
death, crash and flyby cameras.

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
| `dist` | 13.0 | Chase distance, metres. The one field decoded with confidence, and the only one the engine applies. |
| `dist_factor` | 0.01 | Undecoded — scales the distance by *something*. |
| `dist_vary` | 0.1 | Undecoded — the amount that scaling may swing. |
| `dist_min` / `dist_max` | 15.7 / 25.0 | The distance's range. ⚠ See below. |
| `dist_catch_up` | 1.0 | Rate at which distance eases to its target. Units undecoded. |
| `pos_catch_up` | 2.0 | Rate at which position eases. Units undecoded. |
| `look_catch_up` | 3.0 | Rate at which the aim eases. Units undecoded. |
| `thirdp_height` | 0.138 | Third-person eye height. Units unknown (not metres at this magnitude). |
| `thirdp_pitch` | 0.29 | Third-person pitch. As radians this is 16.6°. |
| `back_dist_min` / `_max` | 15.5 / 55.0 | The look-behind view's distance range. |
| `death_interval` | 2.0 | Seconds between death-camera re-frames. |
| `death_z` / `death_x` / `death_alt` / `death_min_alt` | 0 / 80 / 5 / 15.1 | Death-camera placement. |
| `crash_horiz` / `crash_y` / `crash_chord_y` / `crash_elev` | 30 / 45 / 1000 / 40 | Crash-camera geometry. |
| `flyby_min_watch_time` / `_max_` | 3.8 / 4.3 | Seconds the flyby camera watches before moving. |
| `flyby_min_radius` / `_max_` | 5.5 / 7.0 | How close the flyby camera sits to the flight path. |
| `flyby_min_interval` / `_max_` | 1.9 / 2.3 | Seconds between flyby re-sites. |
| `flyby_min_switch_dist` / `_max_` | 70 / 85 | Distance at which the flyby camera hands over. |
| `flyby_z` / `flyby_y` / `flyby_min_alt` | 0 / 0.1 / 0.1 | Flyby placement offsets. |

## Open questions

⚠ **`dist_min` (15.7) is LARGER than `dist` (13.0) in the `default` block.** In all seven
per-plane blocks the two are equal instead. So the rule cannot be "clamp `dist` into
`[dist_min, dist_max]`" — under that reading no plane would ever sit at the default's own 13.0.
Either `dist` is a base that something scales up before the clamp applies, or the two serve
different cameras. Undecided; do not implement a dynamic distance on a guess.

⚠ **The catch-up triplet's units are unknown.** `pos_catch_up` 2.0 / `look_catch_up` 3.0 /
`dist_catch_up` 1.0 read plausibly as `1/s` exponential-smoothing rates, which is the shape the
engine's own camera smoothing uses — but they could equally be frame counts or seconds-to-settle.
The readings differ by roughly a factor of four in felt lag, and none of them looks broken on
screen, so this needs a settling-time measurement against the original rather than a code change
that "looks about right".

⚠ **`thirdp_height`'s units are unknown**, which is why the engine takes only the *radius* from
this file and leaves the chase offset's *direction* as a hand-picked value. `thirdp_pitch` 0.29 rad
= 16.6° sits suggestively close to the engine's hand-picked 15.7° elevation, but one near-match is
not a decode.

## What the engine reads

`CSVM/src/Flight/CamParams.cs` parses the whole file and exposes every field; only `Dist` is
applied, as the chase radius in `CSVM/src/Flight/CameraController.cs` (which the numpad fixed views
share, so both cameras move together). Everything else is carried deliberately dormant behind the
warnings above.
