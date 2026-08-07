# Fog volumes: `fogvol.zrd` + the gamez `fvol*` boxes

Part of the [format documentation](README.md). Covers the chapter-scope reader that fills the
world's invisible fog volumes with cloud sprites — the original's ambient cloud field.
Consumed by `CSVM/src/Mech3/FogVolumes.cs` (reader + volume census) and
`CSVM/src/Effects/FogVolumeClutter.cs` (the scatter and the render).

**The format is two halves that only mean something together.** The reader says *what* to
scatter and *how densely*; the gamez says *where*. Neither alone tells you a chapter has clouds:
three chapters ship a reader file and no volumes, and their reader file names a template their
gamez does not carry.

## Half 1 — `extracted/<chapter>/zrdr/fogvol.zrd.json`

One chapter-scope reader file, root = an [alternating key/list dict](README.md#shared-conventions-zrdr-readers).

| Key | Value | Meaning |
|---|---|---|
| `fog_zone` | `[int]` | Which fog zone the volumes belong to. **Read, not consumed** — see the open question below |
| `distance` | `[float]` | The scatter's world-space grid period, metres |
| `fog_fade_dist` | `[float]` | *(C5 only)* the volume's own fog fade distance |
| `interior_fog_fade_dist` | `[float]` | *(C5 only)* the same, from inside |
| `fog_color` | `[r,g,b]` | *(C5 only)* the volume's interior fog colour, integer 0–255 |
| `clutter` | `[block, …]` | The scatter table — one or more blocks, each an alternating dict |

A `clutter` block:

| Key | Value | Meaning |
|---|---|---|
| `weight` | `[float]` | This block's weight among the table's alternatives |
| `nodes` | `[[weight, name], …]` | The gamez clutter-template roots this block may place, with their own weights |
| `far_fade_range` | `[[a,b],[c,d]]` | **Two** fade bands (metres, start → gone) — the same pairing `templates.zrd` uses for ground clutter, i.e. per detail level |
| `perp_dist_range` | `[min,max]` | Offset perpendicular to the volume's horizontal plane (vertical metres) |
| `perturb_dist_range` | `[min,max]` | Displacement from the grid point within that plane |
| `scale_range` | `[min,max]` | Multiplier on the template sprite's own authored size |

⚠ **C1B, C2 and C3 ship a degenerate copy**: no `fog_zone`, no `clutter` key — the block sits in
the root list with nothing in front of it, which `ZrdrDict.FromAlternating` drops as a stray
value. `FogVolumeSpec.Parse` reads it anyway, on purpose: those three name a template
(`cloudsprite`, no digit) that **exists in no chapter's gamez**, so reading the block is what
lets "this chapter renders no clouds" be a proven lookup failure rather than a parser choice.
Their ranges are degenerate too (`perp_dist_range [151.25, 151.25]`), which is the second sign
the file is vestigial.

## Half 2 — the gamez `fvol*` nodes

Every chapter that scatters anything carries `fvol1`…`fvol34`: parentless-in-effect boxes
directly under the `World` node, each with its own model of 8–56 vertices, invisible (the world
walk has always skipped them — `WorldBuilder.SkipWorldNode`, now via `IsFogVolumeNode`, which the
volume census shares so the two sets cannot drift). Their vertex bounds are the volume.

The correlation with half 1 is exact and is the whole decode:

| Chapter | `fvol*` | volume altitude band (m) | `distance` | `fog_zone` | templates named | sprites placed |
|---|---|---|---|---|---|---|
| C1 | 9 | 970 – 1090.5 | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,025 |
| C1B | **0** | — | 206.25 | *(absent)* | `cloudsprite` — **absent from every gamez** | 0 |
| C1C | 21 | 970.7 – 1091.3, **+ 12 boxes reaching 1390–1688** | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 11,368 |
| C2 | **0** | — | 206.25 | *(absent)* | `cloudsprite` — absent | 0 |
| C2B | 9 | 970 – 1090.5 | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,025 |
| C3 | **0** | — | 206.25 | *(absent)* | `cloudsprite` — absent | 0 |
| C4 | 9 | 1060 – 1180.5 | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,025 |
| C5 | 17 | −463 – 183 | 80 | 1 | `cloudsprite1`, `cloudsprite2` | 19,356 |

C1/C1C/C2B/C4's `fvol1`–`fvol9` tile the whole 12,288 m map as a single flat slab ~120 m thick.
**C1C additionally stacks twelve smaller boxes on top of that footprint**, reaching 1,688 m —
authored build-ups over particular places, and the reason the scatter fills *each* volume rather
than taking the first one that contains a grid cell. C5's are not a slab at all: seventeen
low strips at −463…183 m following the streets, which is why its clouds read as ground-level
night haze between the skyscrapers rather than as an overcast.

⚠ **A fog volume is not the `CLOUD_COVER` band.** C1's floor (970) happens to equal its
`weather.json` `BOTTOM`, but C1C (band 1055–1110 vs volume 971–1091), C4 (1000–1100 vs
1060–1181) and C5 (9950–10150 vs −463–183) all disagree. The volumes are their own authored
geometry; do not re-derive them from the weather file.

## The sprite templates

`cloudsprite1`/`cloudsprite2` are ordinary [clutter template roots](clutter.md) — parentless
`Object3d` nodes resolved by name (`ClutterBuilder.FindTemplateRoot`, shared with the trees) with
one child carrying the card. The card is a single 4-vertex, 1-polygon tri-strip quad,
`model_type: Facade` + `facade_mode: SphericalY` (full camera-facing, not the trees' upright
`CylindricalY`), skinned `cloud1.tif` / `cloud2.tif`, vertex colours 240/240/240, centred on its
own quad centre to within 3 mm.

| Chapter | card size | `lighting` | `fog` |
|---|---|---|---|
| C1, C4 | 132.3 × 132.3 m | `false` | `false` |
| C1C, C2B | 132.3 × 132.3 m | `true` | `false` |
| C5 | 70.0 × 70.0 m | `true` | `false` |

⚠ Every card is authored `fog: false` — the sprites are exempt from the mission distance fog and
carry `far_fade_range` instead. That is a deliberate reversal of what `CloudPuffs` did (it fogged
its puffs); the fade band replaces the fog wall.

## What is decoded and what is inferred

**Decoded, traced to data:** which templates and their weights; the volumes and their extents;
the per-sprite scale range; the draw distances; the card geometry, texture, billboard mode and
render flags; and the three-chapter split, which four independent absences agree on (no `fvol*`,
no `cloudsprite*` template, no `clutter` key, degenerate ranges).

**Inferred, and marked as such:**

- **`distance` is the scatter's grid period.** The reader shares its grammar with
  `templates.zrd`, the ground-clutter file, whose system tiles on a fixed world-space period —
  and `perturb_dist_range` exists to jitter exactly such a grid. The density this produces is the
  corroboration: C1's 130 m period over a 132.3 m card gives ~1.0 sprite-areas of cover per unit
  of layer, i.e. an overcast **exactly one sprite deep**. A period much larger or smaller than
  the card would not land there.
- **`perp_dist_range` is vertical.** "Perpendicular" to the volume's horizontal plane. The
  asymmetry supports it: `cloudsprite1` gets `[-5, 5]` and `cloudsprite2` `[-5, 10]`, so one kind
  floats slightly higher — which is a reading a horizontal offset makes no sense of.
- **`far_fade_range[1]` is what to draw at.** The pair is per detail level (`templates.zrd`
  authors it the same way, e.g. `firtree1` `[[500,1000],[1000,2000]]`); the remake has no
  reduced-detail mode, so it takes the farther band. Both are read and kept.
- **The vertical spread inside a volume is uniform.** The box is a volume, so the field fills it;
  `perp_dist_range` is applied on top.
  ⚠ **Under challenge from footage (2026-08-07, `CAP-12` C4 take — see `BL-118`):** C4's clear
  air shows the plane in *clear sky at 1135 m*, inside the 1060–1180.5 volume, with puff bases
  well above the 1050 m deck sheet — a gap uniform fill cannot produce (132.3 m cards would hang
  to ~956 m). A top-anchored scatter (centres near the volume top + `perp_dist_range`) fits both
  the C4 gap and C1's measured whiteout onset at 1003 m. Not yet implemented; the correction
  belongs here, not in a tuning constant.

**Undecoded / not implemented:**

- **`fog_zone`, and C5's `fog_color` / `fog_fade_dist` / `interior_fog_fade_dist`** are read and
  reported and nothing consumes them. Rendering a volume's *interior fog* is a separate feature
  from its clutter.
- ⚠ **`fog_zone` is not the sky/fog zone selector.** `docs/HISTORY.md` (2026-08-05) records the
  negative "no chapter's copy has a `fog_zone` key" — **that is wrong**: five chapters do. But
  the values do not select a weather zone either: C1's is 0 while its `fvol` nodes carry
  `zone_id: 2` and its weather file names `ZONE1`/`ZONE2`, and C5's is 1 against `zone_id` on its
  own volumes. `BL-277`'s geometry rule stands as landed; treat `fog_zone` as an index into
  something still unidentified, and do not re-open the zone decode on it.
- **The engine's exact grid phase** — the remake anchors the grid on the world origin, the same
  choice and the same unknown as [clutter.md](clutter.md)'s template alignment.

## Visible consequence to know about

At a grazing angle the 130 m lattice is visible as a faint comb in the cloud sheet: a 10–20 m
perturbation on a 130 m grid is only ±15 % jitter. That is what the authored numbers produce
under the grid reading, and it is deliberately **not** tuned away here — a density judgement
against the original is `BL-118`'s, to be made now that the field is authored rather than
invented.
