# The aircraft ground shadow, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-13, for `BL-331`. Every claim below names the
function it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side (the top-level `SHADOW_ANGLES` and `SHADE_*`
keys, and the `SUNLIGHT_DIFFUSE`/`SUNLIGHT_AMBIENT` pair the shadow colour is derived from) is
[`formats/weather.md`](../formats/weather.md); the sunlight node those two land on is
[`weather.md`](weather.md)'s "The sun". CSVM draws no ground shadow at all today, so there is no
implementation half yet; `BL-331` is the item, and `BL-332` owns the authored light intensities.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement, the
decode wins and the disagreement is a note. Nothing on this page has been checked against footage;
the last section says what to look for.

## Function map

| Address | Role |
|---|---|
| `FUN_0049d3d0` | The per-frame shadow pass: the sun-occlusion test, then every aircraft, then the Spruce Goose |
| `FUN_0049d0a0` | One projected shadow: direction, both fades, the ground query, and the object's lifetime |
| `FUN_0049cf00` | The shadow colour, from the sunlight node's diffuse and ambient |
| `FUN_00565d80` | The projection, the footprint, the 32×32 raster, the blur and the ramp |
| `FUN_00565ce0` | Publishes the texture under the material name `gModShadow` |
| `FUN_00565ae0` / `FUN_00565bf0` / `FUN_00565b60` | The shadow object: alloc, its 32×32 16-bit buffer, free |
| `FUN_00565aa0` | Decides **which** of the two shadow implementations is live |
| `FUN_004b3050` | The authored `shadow` node driver, the other implementation |
| `FUN_00476250` | Resolves `shadow` (or `f18shadow`) on the plane model at load, into `plane+0x2bc` |
| `FUN_0049cb90` | Resolves the `sprucegoose` node and its `shadow` child |
| `FUN_004c76e0` | The terrain column query at an (x, z): a count byte, then 0x2c-byte records with the hit height at `+0x10` |
| `FUN_004c7630` | Its wrapper that keeps the highest surface **at or below** the query point |
| `FUN_00538920` | Distance squared in **x and z only**; it never touches y |
| `FUN_0053bf40` | Euler → the 3×3 orientation matrix at `plane+0x180`, rows = the aircraft's local axes in world space |
| `FUN_0049ccb0` | The `SHADE_*` sun-occlusion test, adjacent to this and a different feature |

## Two implementations, one switch

`FUN_00565aa0` sets the global at `00a06f90` to 0, then to 1 only when `009be708 == 1` (the
hardware renderer, the same flag `FUN_00472ea0` uses to pick the `SW_ZONE` weather tables) **and**
`009c6820` is non-zero (a device capability). That one flag picks the implementation, and the two
are mutually exclusive:

| | Projected shadow | Authored card |
|---|---|---|
| Driver | `FUN_0049d0a0`, from the pass in `FUN_0049d3d0` | `FUN_004b3050`, from the per-aircraft update |
| Runs when | `00a06f90 != 0` | `00a06f90 == 0` |
| What it draws | the aircraft's silhouette, projected onto the ground, rasterised live into a 32×32 texture, drawn as a modulate quad | the `shadow` node authored on the plane model, moved down to ground level and flattened |

⚠ **The `shadow` node on the plane models is the software fallback, not the real shadow.** It is a
genuine authored card and it is genuinely used, but only on the non-accelerated path, and
`FUN_004b3050` hides it outright whenever the projected path is live. A remake reproduces the
projected path; excluding `shadow` from the built plane (`PlaneBuilder.cs`) stays correct.

## The projected shadow (`FUN_0049d0a0`)

The pass in `FUN_0049d3d0` calls it once per live aircraft, passing the plane's shadow-object slot
(`plane+0x2c0`), the model root to rasterise (`plane+0x0c`), the plane's position, an is-this-the-
player flag, the plane's orientation matrix (`plane+0x180`), and four range constants. It then calls
it once more for the Spruce Goose with its own four. The four differ per caller and nothing else
does:

| | near dist | far dist | full-shadow altitude | cutoff altitude |
|---|---|---|---|---|
| Aircraft | 1 | 200 | 60 | 250 |
| Spruce Goose (`sprucegoose`) | 300 | 600 | 180 | 750 |

⚠ **These six arguments are pushed for the position virtual call and deliberately left on the stack
as the tail arguments of `FUN_0049d0a0`.** Ghidra's decompiler misattributes them to the virtual
call; the disassembly at `0049d3fb`…`0049d42d`, with its single `ADD ESP,0x24` covering nine
dwords, is what settles the signature.

### Direction

The direction is the top-level `SHADOW_ANGLES` when the weather file authors one, and the sunlight
node's own direction otherwise. All 53 shipped weather files author `[-90, 0, 0]`, which through
`FUN_0053c610` is exactly straight down, so **in every shipped mission the projection is vertical**
([`formats/weather.md`](../formats/weather.md)). `FUN_00565d80` refuses to draw at all unless the
direction's y is negative, confirming the vector is the direction light *travels*.

Reading the globals this function tests also pins the parsed weather struct's base at `0071dcd0`:
`+0x28`/`+0x2c` are the `SHADOW_ANGLES` present-flag and vector, `+0x38`/`+0x3c` the `SHADE_ANGLES`
pair, and `+0x54` the `ZONE1` array that `FUN_00472ea0` indexes at `0071dd24` with stride `0x58`.

### The distance fade: horizontal, and linear in squared distance

Skipped entirely for the player's own aircraft. For everything else, with `d²` the **horizontal**
distance squared from the player (`FUN_00538920` differs x and z and ignores y):

- `d² ≤ near²` → factor 1;
- `d² ≥ far²` → the shadow object is destroyed and the function returns;
- between → `(far² − d²) / (far² − near²)`, i.e. linear in *squared* distance, so the fade is
  strongly weighted toward the far end.

### The ground height

`FUN_004c76e0` queries the terrain grid cell under the aircraft's (x, z) and returns a list of
surface records; `FUN_0049d0a0` takes the **plain maximum** height over all of them.

⚠ **That maximum is not "the surface below the aircraft".** `FUN_004c7630`, the wrapper the
authored-card path uses, does the correct thing and keeps the highest surface at or below the query
point. The projected path does not, so under a bridge or inside a canyon its shadow snaps to the
surface *above* the aircraft. This is a bug in the original, not a rule to reproduce.

### The altitude ramp

With `alt` the aircraft's height above that ground height, and the two altitude constants from the
table above: at or below the full-shadow altitude the factor is 1, at or above the cutoff it is 0
and the shadow object is destroyed, and between them it is
`(cutoff − alt) / (cutoff − full)`. For aircraft that is `(250 − alt) / 190`.

The two factors multiply into a single strength `A`. When `A` reaches 0 the shadow object is freed
(`FUN_00565d50` + `FUN_00565b60`) rather than drawn transparent, so a distant or high aircraft costs
nothing per frame.

### The colour (`FUN_0049cf00`)

The texture is a **modulate** map, so one end of its ramp is white (no darkening) and the other is
the colour computed here. Per channel, from the sunlight node's diffuse RGB (`+0xbc…+0xc4` of its
class data) and ambient RGB (`+0xc8…+0xd0`), which are the two triples `FUN_004dbdb0` and
`FUN_004dbce0` write, i.e. the mission's `SUNLIGHT_DIFFUSE` and `SUNLIGHT_AMBIENT` scaled by their
colours:

```
k       = ambient / (ambient − diffuse · dir.y)      // dir.y < 0, so the denominator is
                                                     // ambient + diffuse·|dir.y|
channel = clamp(round(255 · ((1 − A) + 0.8 · A · k)), 0, 255)
```

`k` is the physical ratio "ambient only" over "ambient + diffuse·(N·L)" for flat ground with an up
normal. **The original darkens the ground by exactly the light the aircraft blocks**, times a fixed
0.8, faded toward white by `1 − A`. The literals are `1.0` at `6032dc`, the `0.5` rounding term at
`6032e0`, and `255.0` at `60414c`.

⚠ **Shadow darkness is per-mission, not a constant grey.** It follows the same
`SUNLIGHT_DIFFUSE`/`SUNLIGHT_AMBIENT` pair `BL-332` is about, and gets *lighter* as a mission's
ambient rises.

### The footprint and the size law (`FUN_00565d80`)

Each of the 8 corners of the model's bounding box is projected onto the ground plane along the
direction (`x + (ground − y)·dir.x/dir.y`, and the same for z), and the min/max of the projected
corners is the quad. The quad's top is then nudged `+2.0` to keep it off the terrain.

The footprint is then scaled about the projected origin by a factor that is **1 for every aircraft
except the player's own**, where it is `3 − 2·(altitude factor)`: 1× at or below 60 units, growing
linearly to **3× at 250 units**. Combined with the strength fade that reads as a shadow spreading
and softening as the player climbs.

### The texture

A 32×32, 16-bit buffer (`FUN_00565bf0` allocates `32·32·2` bytes), cleared and rebuilt every frame:

1. The model node is rasterised into it with the flattening projection above. For AI aircraft the
   whole model root is used; for the player's own aircraft the node named `geometry` is used
   instead when it exists (resolved once at `0071c31c`). The node's cull flag `0x8` is cleared
   first (`FUN_004ccf00`) so frame culling cannot skip the render.
2. A fixed 3×3 blur: every covered texel adds 4 to itself and 2 to each of its 8 neighbours, with
   the border ring skipped, so accumulated values run 0…20.
3. Each texel is replaced by entry `value/2` of an 11-entry ramp built per channel as
   `255 − i·(25.5 − channel/10)` for `i = 0…10`, so entry 0 is white and entry 10 is exactly the
   colour from `FUN_0049cf00`.

The result is published under the material name `gModShadow` (`FUN_00565ce0`).

## The player's own aircraft is a special case, three times over

Worth collecting, because two of the three are the opposite of what a physical shadow would do:

1. **No distance fade** (it is always at distance 0 from itself, so this is only bookkeeping).
2. **The footprint grows to 3× by 250 units**, while every other aircraft's stays tight.
3. **The direction is skewed backwards along the flight path.** Before use, `1.5 ×` the horizontal
   part of the aircraft's nose direction is subtracted from the shadow direction and the result
   renormalised. The nose direction is row 2 of the matrix `FUN_0053bf40` builds at `plane+0x180`;
   the same pair, negated, is cached at `plane+0x1e0` as the reverse heading. Since the vertical
   component is unchanged, the projection ratio becomes `1.5 × nose`, and the shadow lands
   **1.5 × altitude behind the aircraft along its flight direction**.

⚠ **Point 3 is decoded but unverified against footage.** The magnitude is large (45 units behind, at
30 units of altitude), and it only makes sense as a readability aid for the default third-person
chase view, where a shadow displaced toward the camera is easier to read as an altitude cue than one
hidden under the aircraft. The falsification test is in the last section.

## The authored card (`FUN_004b3050`)

The software path, for completeness. `FUN_00476250` searches the plane's model root at load for
`f18shadow` when `plane+0x67c` is non-zero and `shadow` otherwise, and caches the node at
`plane+0x2bc`; a plane with no such node gets no shadow on this path at all. Per frame, when the
projected path is off:

- The player's own card additionally requires the node at `plane+0x3a0` to carry flag `0x4`.
- The ground comes from `FUN_004c7630`, correctly the highest surface at or below the aircraft.
- Above 100 units the card is hidden; at or below 20 units it is opaque; between them its node alpha
  is `(100 − alt) / 80`. Note these are much shorter ranges than the projected path's 60/250.
- The card is a child of the plane, so it is offset within the plane's local frame by
  `alt ×` (world down expressed in local coordinates), which is column 1 of the same orientation
  matrix. Its rotation is recomputed every frame so it stays flat to the world whatever the plane's
  attitude.
- Its X and Z scales are `clamp(|cos(angle)| / sqrt(1 − s²), 0.2, 1.0)` for roll and for pitch, Y
  stays 1: the card foreshortens as the plane banks or pitches, with a hard floor at 0.2.

There is no silhouette, no distance fade and no derived colour on this path. It is visibly the cheap
version.

The Spruce Goose sits between the two: it is drawn by the projected path, but its own child node
named `shadow` is used purely as an enable switch. If that node carries flag `0x4`, the shadow
object is destroyed instead of drawn.

## Sun occlusion: the `SHADE_*` keys (`FUN_0049ccb0`)

The other half of the shadow pass, and a different feature: whether the **player's aircraft** is in
shade. It traces from the player's position along `SHADE_ANGLES` (falling back to the sunlight
direction when the key is absent, which is every shipped mission) over `SHADE_DIST`, targets 1.0 for
lit and 0.0 for occluded, and moves the current value toward that target at `SHADE_CHANGE_RATE` per
second, re-testing every `SHADE_INTERVAL` seconds. The parser's defaults, in `FUN_004bc680`:
`SHADE_DIST 200.0`, `SHADE_INTERVAL 0.5`, `SHADE_CHANGE_RATE 4.0`. This is what darkens an aircraft
as it passes under a mountain or a zeppelin. CSVM does not implement it and no backlog item covers
it.

## What CSVM does today

No ground shadow is drawn at all (`BL-331`). The decode confirms the shape of the gap rather than
changing it:

- **Godot shadow mapping cannot be the mechanism.** The original never casts a shadow map; it draws
  a projected silhouette onto the terrain. The world is built `fullbright: true` and an unshaded
  material receives nothing, so `BL-324`'s removal of `_sun.ShadowEnabled` stays correct and is not
  a regression to rediscover.
- The faithful build is a small top-down render of each aircraft (the shipped `SHADOW_ANGLES` is
  always straight down, so the projection collapses to an orthographic view from above), blurred,
  ramped white → the derived colour, drawn as a ground-conforming multiply quad sized to the
  projected bounding box and lifted 2 units.
- The constants above are directly reusable: 60/250 altitude, 1/200 horizontal distance, the colour
  formula, the player's 3× growth, the `+2.0` lift.
- The original's own terrain-query bug (the plain maximum) should not be reproduced.

## Open questions, and how to falsify this page

None of this has been checked against footage. `BL-331` still owes an original-game A/B, and it now
has specific predictions to shoot at:

1. The **player's own** shadow trails the aircraft by roughly 1.5 × its altitude along the flight
   direction and grows to 3× its footprint by 250 units, while an AI aircraft's shadow stays tight
   and directly beneath it. A capture showing the player's shadow directly beneath the aircraft
   falsifies the row-2-is-the-nose reading and the whole skew term needs re-deriving.
2. Aircraft shadows vanish above 250 units, and other aircraft's shadows fade out past 200 units of
   horizontal distance from the player.
3. The Spruce Goose keeps its shadow to 750 units of altitude and 600 of distance.
4. Shadow darkness differs between missions, tracking `SUNLIGHT_AMBIENT`/`SUNLIGHT_DIFFUSE`.
