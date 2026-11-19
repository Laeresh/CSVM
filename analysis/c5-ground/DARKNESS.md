# The C5 ground's darkness in a rendered frame (PLAN-c5-ground A3)

**Question.** Decision 3 sets the new block floor's texture luma at about 25 to 35, under the original wall
textures (40 to 70) and the streets (82 to 89). What does a texture level render as on screen under Enhanced
with the new city on, beside the walls, roofs and streets as they render, and which texture level puts the
floor clearly under the walls and the streets but readable?

Luma is Rec. 601 on sRGB bytes, 0 to 255, on both the textures and the captures.

## Instrument

`darkness.ps1` takes 14 `--freecam --det --fov=60` captures at 1920x1080 per view through `RunProbe.ps1`, and
`darkness_measure.py` classifies and measures them. The views are `analysis/veg-budget/measure.ps1`'s
`C5-cb7` (120 m over `cblock7` walk-ups), `C5-cb1` (160 m over the `cblock1` towers), `C5-alt` (450 m, across
the city to the horizon) and `C5-street` (45 m, down a `cblock1` street).

| Class | How a pixel is assigned (all from existing flags) |
|---|---|
| ground | The frames with every `cblock1`..`cblock7` texture flattened to grey 16 and grey 128 (`--tex-override`, which also flattens the authored `_1`/`_2` levels) differ by more than 4 levels (SHOT-38: two colours, not one). |
| street / floor | Each ground pixel's own transfer curve is measured at greys 16, 24, 32, 48, 64 and 128; the plain frame is read back through it to the pixel's effective texture luma. Street is effective luma 55 or more, the survey's street threshold; floor is the rest. |
| building | Not ground, and the grey-128 frame differs from its `--no-clutter` twin by more than 6 levels on any channel. |
| roof / wall | A building pixel that gains more than 10 levels from a strong sun 2 degrees off the zenith (`--debug-lighting=sun_energy:4,sun_pitch:60`) over the sun switched off (`sun_energy:0`) is roof; the rest is wall. The gain is bimodal at every view: wall median 0 to 2.7, roof median 26.6 to 30.5. |
| range | The pixel ray's range to the ground plane y = 5 from the pose. A building pixel takes its base's range, the first non-building pixel below it. Bands: near under 400 m, mid 400 to 1000 m, far beyond. |

Faithful (`--graphics=original`, the original buildings) takes the same ground and building masks without the
roof split, as the reference.

**Controls.** A repeated capture is byte-identical (cb1, grey 32), so every difference above is the switch
(METHOD-2). The read-back recovers the shipped texture: the floor's effective luma at cb7 near averages 8.7
against the survey's 8.6 for `cblock7`'s free floor. Under Faithful the ground renders at exactly its texel
byte (16 to 16.0, 128 to 128.0), the linear Original look (SHOT-46).

## Result

Enhanced, the city on; rendered luma mean / median. Floor and street are the near band (all bands at alt, which
has no near ground); walls and roofs are all bands.

| View | Floor | Street | Wall | Roof | Faithful floor | Faithful street | Faithful building |
|---|---|---|---|---|---|---|---|
| cb7 | 3.5 / 0.0 | 67.9 / 72.3 | 35.3 / 27.2 | 26.2 / 18.6 | 5.8 / 0.0 | 96.3 / 102.0 | none drawn at this range |
| cb1 | 6.2 / 1.2 | 64.2 / 66.5 | 24.4 / 12.7 | 13.0 / 11.6 | 8.9 / 1.3 | 91.5 / 94.6 | 34.6 / 22.3 |
| alt | 5.5 / 1.0 | 52.0 / 52.9 | 13.1 / 10.1 | 10.7 / 10.7 | 5.5 / 0.0 | 76.8 / 75.9 | 18.9 / 11.0 (1,639 px) |
| street | 8.9 / 2.7 | 60.6 / 59.1 | 29.7 / 13.3 | 15.4 / 9.4 | 13.0 / 4.8 | 86.0 / 83.8 | 29.1 / 15.0 |

**The ground's transfer.** Median rendered luma of a flat ground texture, Enhanced, near and mid bands, the same
at all four views to 0.1 level:

| Texture luma | 16 | 24 | 32 | 48 | 64 | 128 |
|---|---|---|---|---|---|---|
| Rendered | 8.9 | 14.9 | 21.0 | 33.2 | 45.0 | 89.3 |
| Ratio | 0.56 | 0.62 | 0.66 | 0.69 | 0.70 | 0.70 |

The ratio falls below 32 (the tonemap's toe), so a dark floor loses more than a bright one. Interpolated,
Decision 3's 25 to 35 renders at 15.7 to 23.3. The ground takes no Godot light: the zenith sun raises it by a
median 1 level where it raises a roof by 27, as the `cblock` ground's all-255 vertex colours under the world
light lead one to expect (`docs/org/vertexLighting.md`).

**The walls' ratio.** The new city's albedo is a hull's grade times the facade atlas. Over the 71 hulls the log
names, the wall albedo's luma has median 38.2 (p10 30.9, p90 84.4) and the roof's 25.2. Rendered wall median
over that: 0.71 at cb7, 0.33 at cb1, 0.26 at alt, 0.35 at street; roofs 0.74, 0.46, 0.42, 0.37. The walls take
the moon by their facing, so their ratio moves by a factor of 2.7 between views where the ground's does not
move. Wall means sit far above their medians because of the lit windows' glow.

**Fog.** The ground has no distance dependence up to 1000 m. Past it the zone's black fog (1500 to 2250 m) takes
the far band's grey-128 mean from 89 to 66 to 78, and the ground past 2250 m is gone. Walls darken with range
too (alt: median 12.4 mid, 9.2 far, the clutter's night darkening and the fog), so the floor-to-wall relation
holds roughly to 1500 m. Near is the case to design for.

## Verdict

On screen the walls are not at the 40 to 70 their texture-space figure suggests. Their unlit facade renders at a
median of 10 to 13 at three of the four views and 27 only at cb7, and the roofs at 9 to 19, while the ground
renders at two thirds of its texture luma whatever the view. A floor at texture 25 to 35 renders at 16 to 23:
under the streets (53 to 72), but above the wall median at cb1, alt and street, and above the roof median at
those three views (at cb7 it straddles the roofs' 18.6).

To keep Decision 3's ordering on screen, the floor's mean texture luma wants to be about **20 (18 to 22)**, which
renders at 10.4 to 13.4: at or under the near wall medians at the tower and street views, under half the cb7
walk-ups' walls, about a fifth of the streets, and several levels over today's floor (median 0 to 2.7). Detail
inside the floor (joints, kerbs, lawns) at plus or minus 6 texture levels renders at about plus or minus 4.5,
since the curve's slope between 16 and 24 is 0.75.

This is a number for B12 to generate at, not a look decision: B15 judges it at the controls, best against a
second variant at texture 30 so the author sees both sides.

## Caveats

- A dark wall over a dark sky in the `--no-clutter` twin differs by less than 6 levels and is left out (visible
  on the street view's nearest tower top), so the wall medians are slightly high.
- The wall albedo reference weights each hull once, not by its pixels, and averages the atlas over the families.
- Grey overrides are neutral; a tinted floor's luma will render a little differently through AgX.
- Fog volume cards over the ground count as ground with their tint; no view here stands under the western
  district's `fvol` banks.
- Under Faithful the original buildings are not drawn at cb7's range, so that view has no Faithful wall figure.
- The captures hold game pixels and stay outside the repo.

## Reproduce

    $env:CSVM_DATA_ROOT = "Z:\CSVM"
    & .\analysis\c5-ground\darkness.ps1 -Out <scratch folder outside the repo>
    & .\analysis\c5-ground\darkness.ps1 -Out <same folder> -SummaryOnly

It skips any capture already in the folder, prints the table per view, look and band, and writes
`montage-<view>.png`: the Enhanced capture, its classes (blue floor, yellow street, red wall, green roof), and
the same for Faithful (red building).
