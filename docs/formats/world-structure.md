# World structure (chapter gamez.zbd, validated on C1)

Part of the [format documentation](README.md) (see also [gamez.md](gamez.md), [clutter.md](clutter.md), [fogvol.md](fogvol.md) — the `fvol*` volumes named below are the ambient cloud field's authored extent). Moved from CLAUDE.md on.

- World content lives in TWO places: the `world1` World node's `children` (66 in C1: horizon, cloud groups, zeppelins, trains, fvol volumes…) **and** ~350 top-level parentless subtrees referenced only via the World's `partitions` (12×12 spatial grid over `area` x,z ∈ [-12288, 0]; each cell lists node indices; the union of distinct refs = placed terrain tiles + buildings + vehicles). The `area` record's fields map as `left` = xMin, `right` = xMax, `top` = zMin, `bottom` = zMax (right-handed Y-up world coords — "top" is the smaller z). ⚠ **The grid's two axes index in opposite directions**: a cell stores its own low x but its own HIGH z, and the file's row order runs z downward from `bottom`, because the second axis is authored with a negative cell size. Anything that maps a world position to a CELL INDEX (as opposed to binning geometry it collected itself) has to read that off the cells rather than from `area` — see [interp.md](interp.md), `WorldPartitionSetActive`.
- The remaining ~160 parentless roots (bulletholes, firetrails, muzzle flashes, projectiles…) are runtime-spawned effect prototypes — not world scenery — **except the clutter templates** (C1: `terpat02`, `river1`, `river2`), which ARE scenery the engine stamps onto matching-textured terrain at runtime (see `Clutter.cs`; the chapter's `AddClutterTemplates` list in interp.json names them).
- Terrain = ~1 km tiles (`terpat*`/`water1` textures), no transform (verts already in world space), under `Lod` nodes (range 0–2000 = nearest). Cloud deck sits at y≈1160–1350 (`cloudparent` groups, cloud1.tif); terrain y≈100–160; `litehouse` at (-6932, 128, -3042), town/airbase cluster around (-5000..-6600, 128, -5900..-6700).
## Reference

- **The terrain is a complete grid of one-cell ground meshes.** C1 (and C4/C5): a 12×12 grid of 1024 m tiles — one terrain/water mesh per `area` partition cell, **identity transform with vertices already in world space**, exactly tiling `area` x,z ∈ [-12288, 0]. Every cell is filled (C1: land in the north rows, `water1.tif` sea in the south; the SE corner is harbour sea). A second one-cell mesh per cell is the `cloudlayer` deck at y≈960 (distinguish by texture, not size). The only irregular ground is the **map-centre airfield** — a few offset/oversized `aN` tiles, split half-tiles (e.g. 1024×768/512 farm fields), and genuine holes where structures sit — all ≥3 cells from any edge. This regularity is what makes **map-edge continuation** possible. The original continues the world indefinitely past `area` (user video `C1 IA1 Tile Loading.mp4`, 10+ min of flight): it reloads a tile grid around the plane (~one reload per tile crossing — the fog wall visibly creeps in then jumps back out), and what it fills the outside with is the **local border tile repeated forever** — the same one-tile view recurs every crossing, the map interior (the airport) never reappears, and the content stays type-matched to the local edge (sea edge → sea forever, forest → forest). The continued terrain also carries the clutter trees. The remake replicates this as a camera-following window of border-cell repeats — see `MapEdgeExtender.cs`. **The original REPEATS, and the block is per chapter — settled by A/B against the original at the controls**: plain repetition of a **2-cell** border block matches exactly on C1, C2 and C4, and a **1-cell** block on C5, with no seam gaps in any of them. The remaining four chapters (C1B, C1C, C2B, C3) were measured to carry **only water tiles at their borders**, which fixes them at 1 and makes the depth moot there. Landed as `MapEdgeExtender.DefaultBlockCells`. ⚠ **This reversed the `CAP-17` reading that the original mirrors** — that strip analysis got both the fold and the distance wrong, though its correlation numbers were sound; the post-mortem is in the deleted `analysis/video-flight-calibration/FINDINGS.md` under "Traps this measurement walked into" (`git log -p` on that path). ⚠ **Do not size a map-edge block from a video-derived period** — fly it against the original with the quantity as a knob.
- **A border cell's ground is not always ONE tile.** `MapEdgeExtender.AdoptComplements` runs after
  the scan: a cell whose accepted tiles do not span it adopts the full-cell-spanning flat strips the
  classifier refused for being thin. Without it, three C5 border cells — a base tile plus a
  256–384 m water strip completing it — leave a hole their own width in every continuation copy: sky,
  no collision, outward forever. The adoption is keyed on the CELL being short, never on the strip
  alone — flatness by itself also matches hangar floors and city-block rooftops, and the surface
  class cannot separate them either (`cblock*`, the city ground texture, classifies as `buildings`).
  `--dump-tilegrid` writes the per-cell coverage census that settled it: 16 adoptions on C5, 0 on the
  other seven chapters.
- `horizon` is the camera-anchored skydome. `zone1` supplies the day dome and `zone2` the night dome; a node draws when its `zone_id` is `-1` or matches the camera weather state. The day dome has a flat `FOG_COLOR` cap and skirt, and only C1's zone-one dome scrolls (`texture_scroll` 0.07 u/s). C4 names its zone-one node `h_zone2scroll`, but its scroll rate is zero.
- **`zone_id` — every gamez node carries one (decoded; runtime meaning decompiled and
  implemented).** An `i32` on
  the node record (unified extraction shape: a top-level `zone_id` field), matching the `horizon`
  subtree's zone names. Counts per chapter:

  | | −1 (always) | zone1 | zone2 | zone3 |
  |---|---|---|---|---|
  | C1 | 3529 | 2666 | 869 | — |
  | C1B | 3500 | 2101 | 2 | — |
  | C1C | 4181 | 146 | 1317 | — |
  | C2 | 4189 | 766 | 1 | — |
  | C2B | 3338 | 149 | 1414 | — |
  | C3 | 3759 | 1647 | 2 | — |
  | C4 | 5330 | 802 | 2157 | — |
  | C5 | 9734 | 1555 | — | 149 |

  Both zones span the **whole map** spatially, so these are alternative world *variants*, not
  regions: `-1` renders always, `1`/`2`/`3` only when that zone is active. C1B, C2 and C3 have
  1–2 nodes in their second zone and are effectively single-zone; C1C, C2B and C4 are
  zone2-dominant; C1 and C5 zone1-dominant. The zone numbering is the same one
  [weather.md](weather.md#the-zone-names-are-per-chapter-not-a-fixed-zone1zone2-pair)
  documents — hence C5's `3`. **What activates a zone was the open question, and it is answered:**
  `FUN_0056c430` is a per-frame, per-node *camera-state* filter — a node draws iff its `zone_id` is
  `−1`, or is in the set `{0, camera weather state}` armed each frame (1 below the cloud deck /
  2 above it / 3 inside a `fog_zone` volume). It is not a static per-mission partition, which is
  why no data file ever named the active zone. Full decode in
  [gamez.md](gamez.md) (`zone_id`'s runtime meaning, with the meshed-node census);
  engine-side `GameZNode.ZoneId` → `SceneBuilder.BuildSubtree(…, zoneGate: true)` →
  `Mech3.ZoneGate` per camera. Note `zone_id` does *not* explain the C5 coarse/fine ground pair —
  those are all `zone_id=1`.
- **`zone_set` — every polygon carries one (decoded, `BL-057`, parse+census only, no
  rendering change).** A per-**polygon** list field (unified extraction shape `zone_set`; upstream's
  current Polygon struct spelling, absent on a legacy v0.6.1 tree) — finer-grained than `zone_id`,
  which is per-node. Every polygon in the install carries **at most one** value where the array is
  non-empty (verified: zero polygons with 2+ entries in all 8 chapters + planes.zbd), so
  `GameZPolygon.ZoneSet` keeps just that value (`int?`, null when the array is empty or the field is
  absent). Counts per chapter (polygons whose `zone_set` is non-empty, broken out by value; the
  remainder ship an empty array):

  | | total polys | empty `[]` | `-1` | `0` | `1` | `2` | `3` |
  |---|---|---|---|---|---|---|---|
  | C1 | 18,277 | 3,396 | 8,875 | 10 | 5,623 | 373 | — |
  | C1B | 8,323 | 3,107 | 3,336 | 14 | 1,866 | — | — |
  | C1C | 8,040 | 3,262 | 3,698 | — | 586 | 494 | — |
  | C2 | 12,645 | 4,320 | 4,277 | 26 | 4,022 | — | — |
  | C2B | 7,008 | 2,869 | 3,234 | — | 587 | 318 | — |
  | C3 | 16,087 | 5,466 | 4,975 | 10 | 5,636 | — | — |
  | C4 | 19,661 | 5,259 | 6,217 | 2 | 6,122 | 2,061 | — |
  | C5 | 22,493 | 6,616 | 10,558 | 44 | 3,307 | — | 1,968 |
  | planes.zbd | 16,200 | 15,327 | 873 | — | — | — | — |

  The `-1`/`1`/`2`/`3` values and their per-chapter availability line up exactly with `zone_id`'s
  numbering above (C5 has no `zone2` and carries `3` instead, C1C/C2B/C4 have a `zone2` and carry
  `2`, everyone else tops out at `1`) — the same reasonable read is that `-1` means "no zone
  restriction", by analogy with `zone_id`'s "always" value, but that is not established by this
  item and is not acted on. **`0` is unexplained** — it is not one of `zone_id`'s or `weather.md`'s
  zone numbers, appears only in five chapters (C1, C1B, C2, C3, C4 — never C1C/C2B/C5), and is rare
  everywhere (2–44 polygons per chapter that has it at all) — a lead for a future item, not guessed
  here. `planes.zbd` carries the field too, but only ever `-1`, on a small minority (873/16,200) of
  its polygons.

  **Not a ground selector — checked and ruled out** (the original backlog evidence): this is *not*
  what picks between coarse/fine ground variants or any other content swap. Which zone is active is
  now known for the per-**node** field (the camera's weather state, above), but **no decompiled
  gate reads the per-polygon `zone_set`** — nothing in `CSVM/src` reads `ZoneSet` either, and per
  the plan's ground rules a wrong guess here would delete visible content, so no rendering
  consequence is drawn from it in this item.
- **Authoring gizmos: a lone untextured triangle is a marker, never scenery (decoded).**
  Mission and AI anchors are mesh-less empty nodes, but many carry one **single-polygon,
  three-vertex mesh whose only material is `Colored`** — the level editor's visual mark for the
  anchor, which the original never draws. 142 nodes install-wide have one, and *every* name in
  that set reads as a marker: `cone`/`sphere`/`half_cone` (approach and landing anchors, e.g. C3's
  `treasure_approach`→`do_approachN`→`cone`→`land_on` and the pirate zeppelin's
  `tilt_zeppelin`→`pz_auto_land`→`sphere`), `pt_emitter1/2`, `yacht_emitter1/2`, `sail_emit1/2`,
  `eb_emitter1/2`, `exhaust1` (puffer origins), `flak_explosion` / `scat_explosion` (trail
  origins), `lookat_caboose`, `hangar_panic_scream` (a sound anchor), `p2..p4_lightmarker`,
  `boat_tip`. Some sit under a parent literally named `markers`. The mark is a **billboard**
  (`facade_mode: CylindricalY`) and the models are shared across parents, so it faces the camera
  as a large white or black sail — the C3 cones are ~195 m across and visible from kilometres.
  **No node or model flag distinguishes them** — `flags.active` is true, `zone_id` matches their
  neighbours, and they are reached through the normal partition/child tree; the geometry itself is
  the only signal. Untextured `Colored` materials alone are *not* enough (real scenery uses them:
  a matte-black `gun`, a `b_interior`, the `letterbox` bars), and neither is the name — it is the
  1-poly/3-vertex/untextured conjunction that is exact. **Suppress the mesh, not the node:**
  animations attach puffers and sounds to precisely these nodes by name.
- Point-sprite lights also sit on world meshes (validated on C1): red tower beacons (`rc2_h`/`rc3_h`, `g1183` ×6 instances), a string of 8 orange/red lights (`g1245`), a zeppelin beacon (a `healthy` mesh), plus black/white lights on runtime effect prototypes (`bit1–3`, `lens_flash`) that WorldBuilder never places. All render as camera-facing soft glow sprites via the generic mesh-lights path. **The visible lamp sprites are a separate mechanism:** single-quad flare *meshes* the engine camera-billboards — C1: `refinery_flare` ×6 (16 m quad, `oil_liteflare.tif`), `gen_flare_yellow` ×22 (4 m, same texture), `docklight_flare` ×6 (9.6 m, `dock_liteflare.tif`, blue), lighthouse `litehsflare` (19.2 m, `poleflare.tif`), plus per-chapter `flare_red`/`flare_green`/`yellow_flare`/`fireflare1`/`light_flare*` variants (C5's street/bridge lights are dozens of 1-poly `flare_red`/`flare_green` quads). The flare textures are soft alpha ramps on black. The chapter light readers (`st_light.json`/`dock_light.json`) wire them: `reflight*`/`docklight*` ANIMATION_DEFINITIONs activate the flare child at startup and attach a dynamic `LIGHT_STATE` (range 7–22 m, the warm `0.88/0.78/0.36` color) under LOD gating. Caveats for renderers: the same flare textures also skin polys *inside* regular meshes (a placed C4 Brigand's wing carries an `oil_liteflare` poly), and some flare meshes are multi-poly *strings* (6-poly `flare_green` rows spanning 22–72 m) — only single-poly all-flare meshes are safe to billboard as a unit. The per-light record is decoded in full under [Mesh point lights](#mesh-point-lights) below. Colored examples: 64 gray-white `(218.7)³` stars, 4 orange-yellow `(255,170,0)` refinery tarmac lamps, bluish-white `(215,205,255)` pier lights, red beacons, one `(255,69,69)` lighthouse lamp that blinks every five seconds.
- Cloud/sky geometry (`cloud1`/`cloud2`/`cloudlayer` at y≈960–1350) renders but is **not** collidable; it's identified by texture, not node name (a `cloudlayer.tif` plane sits under the generic node `g27517`). Terrain tiles are named `a3`/`a5`/…; a downward probe reads terrain height ≈150 m (and 0 over the harbor water).
- **The overcast cloud deck, per chapter:** C1/C1C/C2B ship 144 `cloudlayer.tif` single-quad 1024 m tiles at y=960; **C4's deck is 144 `Sky1.tif` quads at y=1050** (parentless partition-referenced nodes `g1720..g1863`, bit-for-bit C1's flat-quad signature); C1B and C3 ship no deck. A deck is the only flat-tile altitude bucket whose footprint covers ~100% of the world `area` — every other flat-tile bucket in the install (water, city-block bases) tops out under 10%. The deck is *not* "the highest thing in the world": C4's tallest non-tile root reaches y=1490, above its own deck.
- **`skywal*` is a BUILDING wall texture, not sky**, despite the name: C4's sky-city pods (`pod2_hi`, `pod6_hi`, `g74`) and several C1/C1B/C2/C3 structures mix `skywal*` faces with unmistakable building textures (`jim_floor01`, `jim_rail1`, `bhfbuild03`, `flaghut2`), and three C4 terrain roots carry a `skywal01` polygon alongside cliff/rail/bridge ones. Any `sky*` prefix rule (collision exemption, deck detection) will misclassify solid architecture. C5 ships no `sky*` texture at all.

## Mesh point lights

A model's point-light array is `model+0x20` (count) and `model+0x40` (pointer), 76 bytes per record,
read by `gmod_const.c`'s model reader `FUN_00560dc0` and walked by both model draws: the software
one `FUN_005524d0` over `00552bf5`-`00552d1c` and the hardware one `FUN_00554550` over
`00554619`-`00554730`, which share the per-light plot `FUN_00558b30`. The blink lives at
`00552cb6` and `005546c8`, the fade in `FUN_00558b30` at `00558d53`. mech3ax's `unkNN` names are byte offsets, so the table below reads
against `PointLightC` in `crates/gamez/src/model/common.rs`.

| Offset | mech3ax | What the original does with it |
| --- | --- | --- |
| `0x00` | `unk00` | Draw mode. 0 plots the first vertex; 1 walks the vertex list one step per interval and also plots the step behind it in the stashed colour. **Mode 1 is authored nowhere in this install** (0 on all 715 lights), so the walking form is untestable data. |
| `0x04` | `unk04` | Blink gate for mode 0: 1 makes the light flash, 0 leaves it steady. 56 of the install's 715 lights carry it, all of them in C1 (5) and C5 (51). |
| `0x08` | `unk08` | **Seconds between toggles**, not a size. 1.0 (30 lights), 2.0 (25) and 5.0 (the C1 lighthouse lamp). Read only inside the `unk04` branch, so the 659 steady lights that carry a value here never elapse it. |
| `0x0c` | `vertex_count` | Positions in the record's own vertex list. 1 on every light in this install. |
| `0x10` | `zero16` | **Runtime only**: the seconds accumulator, advanced by the frame delta `DAT_009ad744` and zeroed on each toggle. 0 on disk. |
| `0x14` | `zero20` | **Runtime only**: low half the current vertex index, high half the stashed alternate colour. 0 on disk, which is what makes a blink read as on/off rather than as two colours. |
| `0x18` | `unk24` | Low signed 16 bits are the **draw-priority bias**, applied as `screen_z *= priority * PRIORITY + 1` exactly as a polygon's own `priority` is. |
| `0x1c` | `color` | Three 0..255 floats. The loader packs them into the 16-bit frame-buffer colour at `0x28`. |
| `0x28` | `rgb` | **Runtime only**: that packed colour. 0 on disk. |
| `0x2a` | `flags` | **Read by nothing.** No instruction in the binary loads this halfword, and its value does not behave like a field: the 64-star horizon mesh is bit-identical between chapters in every other field while carrying 1030 in C1, 904 in C1B, 889 in C1C/C5 and 930 in C4. Treat it as uninitialised export memory, stable within one export run. |
| `0x2c` | `vertices_ptr` | The vertex list. |
| `0x30` | `unk48` | The fade band's near edge. Not read directly: it is the distance at which the slope below reaches full alpha. |
| `0x34` | `unk52` | The distance the light is fully gone at. **0 disables the fade outright** — the draw tests it against zero first and plots the light opaque at any range — so an unfaded light is not one with a zero-length fade. |
| `0x38` | `unk56` | The fade slope: `alpha = (unk52 − distance) × unk56`, in 0..255 alpha units, opaque at 255. Authored as `255 / (unk52 − unk48)`, which is where 0.17 (a 1500 m band), 0.102 (2500 m) and 0.255 (1000 m) come from. |
| `0x3c` | `unk60` | 1 marks the light a **lens flare**. `FUN_004d6a10` casts a ray from the camera to it each frame and, when nothing blocks it, `FUN_0057cc00` draws the four-element flare of `FUN_0057ca80`: a glow on the light plus ghosts at 0.5, 0.1 and −1.0 along the vector to screen centre, sized off viewport width. 18 lights in the install carry it. |
| `0x40` | `unk64` | **Read by nothing.** The flare path takes the field block at `0x30` and touches only `0x30`, `0x34`, `0x3c`, `0x44` and `0x48` inside it. |
| `0x44` | `unk68` | Flare-only: the distance the flare is still at full brightness. |
| `0x48` | `unk72` | Flare-only: the distance the flare is gone at, ramping `(unk72 − d) / (unk72 − unk68)` in between. Dead on any light whose `unk60` is 0, which is 697 of 715. |

⚠ **`unk68` is not a point light's visibility range.** It is 4000 on most of the install and reads
like one, but the flare gate at `0x3c` is clear on all but 18 lights, so for everything else it is
never loaded. The point light's own reach is `unk52`, and on most lights that is zero.

**The blink is on/off, not two-coloured.** The toggle swaps the packed colour at `0x28` with the
high half of `0x14`, and `0x14` is zero on disk, so the first toggle stashes the colour and leaves
black behind. Every light's accumulator starts at load and nothing reseeds it, so lights sharing a
period blink in unison.

**The light is one screen pixel.** The software flush `FUN_00584330` writes a single 16-bit texel;
the hardware draw batches one point per light. Neither has a size to copy, so a renderer drawing
soft sprites is choosing its own and needs a horizon fade the original does not, since an unfaded
pixel is swallowed by fog and an unfaded sprite is not.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.