# `OBJECT_MOTION` `translation_range` is a spherical launch, not a distance

`census.py` walks every compiled `ObjectMotion` event in all 8 chapters' `cam_anim` and prints each
distinct `translation_range` shape, plus the value distributions. Run it from the repo root:

```
python analysis/object-motion-range/census.py
```

## The finding (2026-08-01)

**`xz` is an AZIMUTH and `y` an ELEVATION, both in degrees; `initial` is the launch SPEED in m/s**
(`delta` a speed ramp over the run time). The engine had read `xz`/`y` as *distances travelled over
`run_time`* with a random azimuth, which threw debris and smoke trails hundreds of metres.

Measured over **1,217 events / 613 distinct shapes**:

| | measured |
|---|---|
| `xz` | every value in **[−170, 359]** — never outside ±360 |
| `y` | every value but one in **[−90, 90]**; the outlier is a single 110 |
| `initial` | median 18, range −45…95 — plausible speeds |

Four independent confirmations that a distance reading cannot produce:

1. **The five trails of one explosion carry evenly spaced `xz` bands** — `fly_trail1` 35–55,
   `fly_trail4` 85–105, `fly_trail2` 135–165, `fly_trail5` 185–205, `fly_trail3` 235–255. That is a
   starburst around the circle, which is exactly what the original's HE impact looks like
   (`OriginalScreenshots/HE Rocket 1-3.png`). As distances it was a fan of 35 m to 255 m throws.
2. **`y` goes negative exactly where the thing falls.** A balloon turret's parts: −10…−30, −50…−70,
   −70…−90. A dock platform collapsing: +70…+80. A helium tank blowing sideways: +1…+2. As a
   "vertical distance" a −90 would be meaningless; as an elevation it is straight down.
3. **The gun casing.** `gunshell` carries `xz` ±10°, `y` **−75…−85°**, `initial` 1.5–1.8 m/s — brass
   dropping out of the gun port at walking pace. Read as distance it was a 40 m/s downward throw.
4. **`translation_range_min_only`** (112 events) marks rows whose `max` fields are all **0.0** —
   e.g. azimuths 0/20/30/50/100/120 at elevation 80 and speed 5, a fixed-bearing fountain. Under a
   range read those interpolate toward 0 and aim nowhere the data asked for; the flag means the min
   IS the value.

## `gravity.value` is an absolute m/s², not an offset

Worth stating because the aircraft's own arcade `nom_gravity` is **20** (`player.json`), which
invites reading `-3.0` as an offset to it. It is not: the census carries a literal **−9.8 on 173
events** and −10 on 400 — Earth gravity spelled out. The weak values (−1/−2/−3) sit on smoke trails
and casings, where floating is the authored look.

| gravity | events |
|---|---|
| −10.0 | 400 |
| −2.0 | 250 |
| (absent) | 208 |
| −9.8 | 173 |
| −3.0 | 88 |
| −1.0 | 80 |
| −9.0 | 16 |
| −6.0 | 2 |

## What is still a choice, not a decode

**Which world bearing azimuth 0 points along.** The engine uses +X. The data fixes the trails'
spacing *relative to each other* (that is what makes the starburst), never their absolute compass,
and no capture can settle it — a rotated starburst is the same starburst.

⚠ **623 of 3,651 ranges have `min > max`.** Interpolation must handle an inverted pair rather than
assuming ordering (`a + rand·(b−a)` does; a `clamp(min,max)` would not).

## `SCALE` is an offset from unit scale too (2026-08-01)

Same event, same shape of mistake. **`scale.initial` and `scale.delta` are offsets from 1**, not
absolute sizes: `scale = 1 + initial + delta·u`. (`OBJECT_SCALE_STATE` and `OBJECT_SCALE_FROM_TO`
*are* absolute — this is `OBJECT_MOTION`'s channel only.)

The install decides it. Of **45 distinct SCALE events**, **30 carry a bare `(-0.1, -0.1, -0.1)` with
zero delta** — every `h2twr`/`radiotwr`/`transmitter` collapse and every `gullfly`. Read as an
absolute that is a **negative scale**: the piece inside-out at a tenth of its size, effectively
invisible. Read as an offset it is a clean 10 % shrink.

Confirmed visually on C1's `ap_h2otwr1` at frame 250 of a `--freecam --destroy --det` capture:

| reading | what the kill looks like |
|---|---|
| absolute (`initial`) | only the legs remain standing; the tank and roof sections are gone (a dark speck) |
| offset (`1 + initial`) | the tank body tumbles away at 90 % size — a collapsing water tower |

⚠ **That shot only discriminates late.** At frame 90 the tower is still intact and the image is
byte-identical under *both* readings **and** under a forced `Vector3.One * 5f` control — an
able-to-fail check is what caught it (METHOD-9). Use frame 250.

⚠ **The base is 1, and whether it should be the node's own authored scale is undecided.** Every node
carrying this channel is authored at exactly unit scale in this install, so the two coincide and no
capture can separate them.
