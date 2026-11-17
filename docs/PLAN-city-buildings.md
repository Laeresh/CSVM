# City buildings, the Enhanced look for C5's Manhattan

**ACTIVE PLAN** (written 2026-11-16). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

C5 is the one chapter the Enhanced look leaves undressed: the Rich vegetation path refuses it by
design, so its city draws the original building clutter. The complaint this plan answers, in the
user's words: "The buildings clutter is very repetitive", "every building has every window lit.
that adds to the repetition too", and all four of too few building shapes, uniform placement, same
colours and textures, and a cheap look ("I see every point", "Perhaps we need a bit of all of
them"). The plan replaces the look of every C5 building under Enhanced with script-built
look-alike models that differ per building in facade, tint, roof dressing and window lights, while
the city stays physically the original's.

Out of scope: the painted ground textures, the street lamps and their glows, the cars, and the
named world models (bridges, docks, the statue). They are judged again once the new buildings
stand on them, and a follow-up issue is filed only if the user's eyes ask for one. The Faithful
look is untouched. No item is drawn from a `backlog` issue, so there is nothing to re-verify as
still open.

## Milestone goal

- Under Enhanced with the scenery option on, no C5 building draws its original mesh: near the
  camera it is a script-built look-alike, and beyond the hand-over it is a script-built box of the
  same hull.
- A building looks the same near and far, and two buildings of the same hull differ in facade,
  tint and brightness, roof dressing and lit windows.
- Not every window is lit. The window behaviour is the one the user chose in the `cblock1`
  prototype.
- The city is identical on every launch and on every multiplayer client, with nothing synchronised.
- The added cost stays inside the vegetation budget on the PC, the distant draw-call count on C5
  does not rise, and an integrated GPU keeps the original city by default.

**The city stays physically the original's.** Every building keeps its lot, footprint, height and
collision, so Faithful and Enhanced players, the AI and the missions fly the same city. A building
that looks different is allowed; a building that is somewhere else, or a different size, is not.

## Decisions (2026-11-16)

Rows 1 to 11 were put to the user and answered. Rows 12 to 17 were stated as assumptions and not
objected to; reopen any of them by asking.

| # | Question | Decision |
|---|---|---|
| 1 | Reskin, reshuffle or rebuild the city? | **Reskin only.** Lot, footprint, height and collision stay original, so the skyline's heights stay as authored. |
| 2 | Do originals stay in the near mix? | **No, look-alikes replace every original building look near the camera.** One quality level, and an authored facade atlas means the window positions are known. |
| 3 | What stands in at distance? | **A script-built plain box per hull, wearing the same facade, tint and windows.** The original mesh is never drawn in the Enhanced city; only its collision remains. A building that changed at the hand-over, and a distant city as repetitive as today, were the reasons against the original mesh. |
| 4 | Window lights | **Slow: single lit windows and whole lit floors, each rolling again every few minutes on its own offset.** The user flew the three modes of the prototype and said "Slow looks good" (B15). The fixed scatter and floors modes and their switch are removed. The atlas carries a window id and a floor id. |
| 5 | Scope | **Buildings only.** Ground paint, lamps and cars wait for the prototype verdict. |
| 6 | Style | **Look-alikes**: facade families taken from what the originals show, each variant graded to its original's average colour and brightness, so C5's fog and lighting tuning stays valid. |
| 7 | How far may a model leave its collision hull? | **About 3 m carved inward and about 4 m of non-solid roof dressing above** (water tanks, vents, parapets). Tall spires and deep setbacks only where the original hull has them. Both numbers are TUNE. |
| 8 | Budget | **The vegetation budget**: at most 2.0 ms GPU and 0.5 ms CPU added at fixed PC views, with C5 views added to that instrument, plus the C5 rule that the distant draw-call count does not rise. The Deck follows the existing vegetation fallback at two or more panes. No Deck is available to measure on, so its one-pane cost is unmeasured and the new city is off by default on an integrated GPU until it is measured. |
| 9 | Menu option | **The existing 3D Vegetation option, relabelled to cover scenery.** Off is the original city; its other four words (Near, Far, Very Far, Extreme) set the hand-over distance. The stored key stays. |
| 10 | Rollout | **A prototype on the `cblock1` tower district first**, then the remaining hulls after the user's verdict. The user first chose `cblock7` on the planner's recommendation and moved the prototype when A2 showed that district lies under the street fog; the tower district shows the repetition and the all-lit windows plainly, and whole lit floors only read on towers. The `det` dressing models on the `cblock1` buildings keep drawing as they are for the prototype. |
| 11 | Authoring | **Python generators through headless Blender**, as the vegetation models are built. Blender MCP is not part of the pipeline. |
| 12 | Faithful look | Untouched. |
| 13 | Mirroring or turning a building | None. A turned model no longer matches its collision. |
| 14 | Where a building's variety comes from | A hash of its world position. Nothing is random at run time and nothing is synchronised. |
| 15 | What goes into the repo | Models built from measured dimensions. No original vertices and no original pixels. |
| 16 | Model packs | A user model pack still overrides a shipped building, as it does for vegetation. |
| 17 | Shadows | Buildings keep casting none. |
| 18 | Who owns the city path | **A new module `CSVM/src/Mech3/CityModels.cs`; `ClutterInstances` draws it as a third layout beside Original and Rich.** `VegetationModels` is unlit, cut out and never solid, and `Clutter.cs` is placement. (A3) |
| 19 | How a stamp picks its geometry | **On the CPU, `VariantOf(seed, V)`, in the near tier only. Near cells are keyed by cell and variant; the far tier is one box mesh per hull in the kind's existing cells.** The distant draw count per kind cell falls from one per mesh of the chain to one. (A3) |
| 20 | How per-building values travel | **A seed hashed on the CPU from the stamped point, in the solid stamps' custom data `w`, which no solid shader reads; the shader expands it.** The near model, the far box and the map edge's copies then agree exactly, and the buffer layout does not change. A hash recomputed in the shader from the instance origin would differ between tiers by float rounding. (A3) |
| 21 | Where window and floor ids live | **A second RGBA8 texture laid out like the facade atlas, sampled nearest with no mipmaps; the atlas stays opaque.** (A3) |
| 22 | Lighting path | **The clutter's Enhanced lit arm (world light, ambient fill, terrain shadow, night darkening through `FOG`), in a standalone shader with no instance uniforms.** C5's night key and the city darkening hold, and thousands of near cells do not exhaust the instance-uniform buffer. The unlit vegetation look is the wrong template. (A3) |
| 23 | Fade and hand-over | **The vegetation band and dither split as they are (`VegetationModels.Band`, `ModelShare`), with the far box in the card's role. The box keeps the kind's own fade and cells; near cells are 2048 m (`CityModels.CellM`, TUNE).** 512 m cells cost process time per node (B14). The city follows the vegetation band at every word, Extreme included (Decision 33). (A3, B14) |
| 24 | Where the original is suppressed | **`ClutterInstances`' layout choice while the city is on. The original MultiMesh and all collision stay built, so a live switch needs no rebuild. A model pack's replacement for a kind keeps that kind off the city.** (A3) |
| 25 | Does the city follow Vegetation Quality as well as the scenery option? | **Yes: it draws only at Rich with the option on.** The integrated-GPU fallback that Decision 8 relies on sits on Vegetation Quality, not on the option. Decided by the orchestrator from Decision 8, not asked. |
| 26 | What the city copied past the map edge draws | **The far box at every distance, no near models.** Decided by the orchestrator as the cheapest reading, not asked. |
| 27 | Small windows under TAA (B15) | **Windows under 4 px average nine taps.** The user reported "some buildings that flimmer at certain distances" and said of the filtered look "works for me". |
| 28 | Glow strength (B15) | **Right as it is** (`WindowGain`, `FarPointGain`). |
| 29 | Tint and brightness spread (B15) | **Right as it is** (10 percent tint, 20 percent brightness). |
| 30 | Wall brightness (B15) | **Right as it is**, with the even ambient on every face: "the rest is tuning in the lighting lab". |
| 31 | Near variants per shape (B15) | **"More for common shapes"**: two for a rare shape, a third or fourth for the shapes placed most often. C21 picks the shapes by A1's placement counts. |
| 32 | The `det` dressing models (B15) | **"Rebuild them too"**: each dressing piece gets a model in the new towers' style, and its original is not drawn in the Enhanced city. Done in C21. |
| 33 | Extreme's reach for the city (B15) | **"Full reach, re-measure later."** Extreme hands the city over across its own band. With 2048 m cells the prototype draws 1,776.5 at `C5-alt` against 1,775.5 with the city off, one draw over Decision 8's rule, which the user accepts until C24 measures the whole city; the reach is shortened then if the excess has grown past a figure the user accepts. |
| 34 | Ground and lamps (B15) | **"Ground looks wrong"** beside the new buildings; the lamps were not named. A follow-up issue filed in C24, outside this plan (Decision 5). |
| 35 | Walk-up colour variety (C21) | Of the dressed `cblock4` the user said: "The cblock4 could use more color variation at least the roofs", and of the other walk-up district: "cblock7 looks good". Each tile roof takes one of six colours and the walls under tile roofs four per family; shown both districts before and after, the user chose **"Land it as shown"**, so the variety reaches `cblock7` too. At the controls afterwards: "works for me". |
| 36 | `cb07b`'s two tops (C21) | **"Keep both"**: the open frame with two slabs on one near variant and the far box, the landing deck with edge rails on the other. |
| 37 | Walk-up cornice (C21) | **"Add a pale cornice"**: `cb12`, `cb13`, `cb15` and `cb16` get a pale band under the roof edge where the originals carry their dentil cornice, on every near variant and the far box. Shown a first build, a 1.2 m mid-grey band, beside the original's thin near-white line, the user chose **"Thinner and brighter"**, and of the 0.6 m near-white line that followed: **"Land it"**. |
| 38 | Walk-up wall brightness (C21) | **"Keep as it is"**: the walk-ups keep their lit brick and stone against the originals' near-black walls. |
| 39 | Tower warmth from altitude (C21) | **"Keep the warm look"**: the window light mix of B15 stays, against the original's cool grey-white skyline. |
| 46 | The `det` dressing's fire escapes and look (C21) | Shown three ways to build a fire escape: **"B: see-through lattice"**, one flat panel per flight with a lattice of our own drawing, cut out so the wall shows through. Shown the build, with roof boxes paler than the original's and one zigzag per lattice band against the original's finer diamonds: **"Land as shown"**. |
| 45 | Original posters on the new boards (C21) | The user: "could we place the original posters on the new buildings boards, mixed with the new ones?" The three C5 posters (`poster02`, `poster03`, `poster04`) are copied from the game's loaded textures into the atlas in memory, never into the repo; `poster03` joins the tall designs, `poster02` and `poster04` the wide ones. **"Equal chance"**: each original is one more design in the seed's draw. Not built; it follows the `det` run. |
| 44 | Pitched tower roofs of `cblock5` and `cblock6` (C21) | The original's hip and pyramid roofs on `cb18`, `cb19b` and `cb20` read dark red at night; the new ones follow each building's tint. **"Keep as shown"**. |
| 43 | `cb17a`'s gable ends (C21) | The original's gables are orange-red brick against pale walls; the first build painted them in the wall's colour. **"Red brick gables"**. |
| 42 | Roof billboards of `cb22` and `cb23` (C21) | The originals show a lit poster; the first build drew blank pale boards. **"Own painted posters"**, then on the wording: "Perhaps you can get some plane,parts manufactures or fictional companies from the game files." Shown ten designs (Blake, Tornado, Achilles Armor, Hughes, Wright, Bristol, Ford, Colt, and the words EAT and INN on the narrow board), five of them names of real firms the game names as makers: **"Land it"**. |
| 41 | Windows on corners (C21) | The user: "some windows are right on corners". A family's four facade rows shared no bay grid but the 8 m tile, so no mesh fit suits all four. Shown the options, the user chose **"Common grid, all buildings"**: the eight walk-up rows with 3 bays take 4 and the three tower rows with 5 take 4, which changes approved facades. Of the result, with a pier of 0.4 to 0.5 m on each side of a corner: **"Land it"**. |
| 40 | The option's label (C22) | **"3D Scenery"** replaces "3D Vegetation" on the VIDEO page. The stored key and values stay. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "C5 has 25 building hulls, and `cblock7` has three." | 25 counts only the base `a` models. The substitutes reached by the substitute roll are separate meshes with their own collision, and 29 of the 39 differ from their source in geometry. A1 measured 45 building shapes in 32 distinct boxes, plus 16 `det` dressing shapes. `cblock7` draws 6 shapes in 4 boxes (`analysis/city-hulls/FINDINGS.md`). |
| 2 | "67,836 buildings are placed." | The engine's C5 load log reads 66,763 3D decorations, and that count includes the `det` halves: 54,194 buildings and 12,569 `det` halves. |
| 3 | "A template's building count is its number of buildings" (28, 24, 27, 17, 23, 25, 25). | Those count decorations. A `det` half is a separate decoration at its building's origin, so the tiles hold 16, 16, 17, 17, 17, 19 and 25 buildings. |
| 4 | "A `det` half is half of the building's volume." | It is exterior dressing (rails, fire escapes, scaffolding, roof boxes) on its own independent roll, with its own collision shape, fading at 75 to 250 m. |
| 5 | "`cblock7` is where the repetition is worst for a player, because it has the most buildings from the fewest shapes." | Its large district lies inside the fog volumes `fvol6` and `fvol31` to `fvol34` (up to y = 183): a low camera there sees about ten buildings in near-black, and from above it is seen through the cloud field. In the clear it stands only in two patches of about 1 km² (A2). |

"What the data actually ships" comes from one read-only survey of the docs, the code and the local
extraction, corrected by A1's census where the two disagreed. The survey's code references outside
the clutter census were not re-read. Confirm each before building on it.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | B12, B13 | A3's wiring contract names the functions. The custom-data, build-loop and layout claims it rests on were re-read; confirm the rest as you go. |
| **Direction sound, magnitude a judgement call** | A2, B11, B14, C24 | The budget and the draw-call limit rest on measurements; the C5 view poses, the near triangle allowance and the 2048 m near cell are TUNE. |
| **Leads only, no mechanism yet** | B15, C21, C22, C23 | Budget for investigation. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

**The city.** C5 is Manhattan at night (`docs/formats/campaign-missions.md:56-59`). The ground is
seven painted city-block textures, `cblock1` to `cblock7`, at 256 m per texture repeat
(`analysis/item9-depth-bias/CBLOCK-LOD.md:51-60,100-112`). Each texture is a clutter template with
one fixed building layout. Where two layers overlap, the `no_clutter` polygon flag picks the
dressed one: flagged ground gets `cblock4/5/6` (low-rise, up to 52 m), clear ground gets
`cblock1/2/3/7` (towers, up to 108 m) (`docs/org/clutter.md:163-175`).

**Decorations per template** (`analysis/city-hulls/FINDINGS.md`):

| Template | Unflagged area | Building decorations | Buildings | Placed buildings (estimate) | Lamps |
|---|---|---|---|---|---|
| `cblock1` | 51.5 km² | 28 | 16 | about 12,686 | 34 poles, 34 glows |
| `cblock2` | 9.6 km² | 24 | 16 | 2,270 | 20, 20 |
| `cblock3` | 5.0 km² | 27 | 17 | 1,265 | 33, 33 |
| `cblock4` | flagged ground | 17 | 17 | 2,802 | 12, 12 |
| `cblock5` | flagged ground | 23 | 17 | 2,115 | 20, 20 |
| `cblock6` | flagged ground | 25 | 19 | 1,750 | 33, 33 |
| `cblock7` | 75.8 km² | 25 | 25 | 31,077 | 12, 12 |

Areas are from `analysis/bl-305-clutter-uv/FINDINGS-noclutter.md:161-166`. `cblock7` holds more
than half of the city's placed buildings, from `cb12a` (10 lots per tile), `cb13a` (4) and `cb14a`
(11) and their substitutes.

**Models.** `templates.zrd` authors 77 decoration models for C5 (`docs/formats/templates.md:135-145`,
`CSVM.Tests/ClutterTemplatesTests.cs:24-36`). 71 building models have geometry, in 61 distinct
shapes: 45 building shapes in 32 distinct boxes, and 16 `det` shapes. Templates place `cb00a` to
`cb24a` and seven `cbNNdet01` models directly; the `b`, `c`, `d`, `det02` and `det03` models are
reached only through the substitute roll. A substitute keeps its source's shape in 10 of 39 pairs
and differs in geometry in the other 29, so the hull a stamp has is the hull of the model it
rolled. A `det` model is exterior dressing drawn at its building's origin (rails, fire escapes,
scaffolding, roof boxes), 0 to 3 m outside the building's box and up to 3.7 m above it, a separate
decoration with its own roll and its own collision shape (`docs/formats/clutter.md:157-163`).
Nine models carry further meshes that are part of the same building: the remaining walls, a
cornice ring, a ground-floor awning that reaches 1.6 m past the walls, or a tower setback
(`docs/formats/clutter.md:55-62`). A building has 10 to 112 triangles. The facade textures are
64x64 tiles holding one window (`smbldg1` to `smbldg5`) and 128x64 tiles holding about eight
windows on two floors (`bldg1` to `bldg4`), with the lit windows painted in; roofs are 32x32 and
trims 16x16.

**The `cblock7` hulls.** Seven models in six shapes and four boxes: `cb12a` (20 x 16 x 14 m) and
`cb12b` (20 x 16 x 22 m, with a chimney); `cb13a` and `cb13b`, one shape (16 x 20 x 26 m);
`cb14a`, `cb14b` and `cb14c`, one box (16 x 12 x 17 m) with a hipped, a lower hipped and a gabled
roof. None has a `det` model. `cb12a` and `cb13a` carry the ground-floor awning.

**The `cblock1` hulls, the prototype's.** Fourteen models in twelve shapes, 16 buildings per tile
and about 12,700 placed:

| Shape | Models | Box (m) | Triangles | Facade texture | Lots per tile |
|---|---|---|---|---|---|
| `cb00a` | `cb00a`, `cb00b` | 40 x 24 x 16 | 14 | `smbldg1`, `smbldg2` | 4 |
| `cb01a` | `cb01a`, `cb01b` | 32 x 32 x 24 | 16 | `smbldg1`, `smbldg2` | 3 |
| `cb01c` | `cb01c` | 32 x 32 x 24 | 16 | `smbldg3` | roll |
| `cb01d` | `cb01d` | 32 x 32 x 24 | 22 | `smbldg4` | roll |
| `cb02a` | `cb02a` | 31.8 x 31.8 x 65.2 | 50 | `bldg3` | 3 |
| `cb02b` | `cb02b` | 32 x 32 x 64 | 54 | `bldg4` | roll |
| `cb03a` | `cb03a` | 32 x 32 x 64 | 107 | `bldg1` | 2 |
| `cb03b` | `cb03b` | 32 x 32 x 64 | 112 | `bldg2` | roll |
| `cb04a` | `cb04a` | 32 x 32 x 80 | 72 | `bldg2`, the clock and a motif | 2 |
| `cb04b` | `cb04b` | 32 x 32 x 80 | 50 | `bldg3` | roll |
| `cb05a` | `cb05a` | 23.7 x 23.7 x 58.9 | 19 | `bldg4`, a teal crown | 2 |
| `cb05b` | `cb05b` | 24 x 24 x 92 | 31 | `bldg1` | roll |

`cb04a` carries a further mesh, its upper setback with the clock (20 to 69 m). The towers' boxes
hide setbacks; a near model follows its hull's measured tiers, not its box. `cb00` to `cb03` stand
with `det` dressing models (`cb00det01` to `03`, `cb01det01` and `02`, `cb02det01` and `02`,
`cb03det01` and `02`).

**Placement.** One stamp per integer repeat of the ground texture's UV across each triangle
(`docs/org/clutter.md:196-206`). No position, rotation or orientation variation is authored in any
chapter (`docs/org/clutter.md:121-138`), every C5 block authors `scale_range` 1 to 1, and the seed
is fixed (`CSVM/src/Mech3/Clutter.cs:77`). The only per-stamp variety is the substitute roll, so
each 256 m tile of a template repeats one layout.

**Drawing.** Each kind is one `MultiMesh` with shadows off (`Clutter.cs:1843-1860` solids,
`1865-1891` extra building parts). One C5 load places 54,194 buildings and 12,569 `det` models,
2,383,387 triangles in all, and 110,528 lamp sprites. Fade bands: `det` models 75 to 250 m, small
blocks 200 to 400 m, 20 tower models 700 to 900 m and `cb05` 500 to 700 m, dithered in the cutout
pass (`docs/org/clutter.md:279-312`). Under Enhanced the fade is
stretched by fog scale and View Distance (`CSVM/src/Launch/EnhancedLook.cs:262-265`), and each kind
is cut into cells of twice its far fade, clamped to 512 to 4096 m, each with a visibility range
(`CSVM/src/Mech3/ClutterInstances.cs:20-24,230-240`), with one more MultiMesh per cell for each
further mesh of a building. At Effects High and View Distance Far the stretch puts C5's small
blocks at 800 to 1,400 m and its towers at 2,800 to 3,600 m. C5 draws 5,425 such nodes over the
whole map at that setting, lamps and glows included; a spyglass disc view draws about 600 to 700
of them. The same model placed by two templates is two kinds with separate cells. Collision is one shared concave shape per mesh, attached per placement
(`CSVM/src/Mech3/Clutter.cs:1894-1946`): 123 shapes holding 4,224 triangles.

**What the vegetation work already built.** Generators in `CSVM/data/vegetation_models/` run
through `scripts/MakeVegetationModel.ps1` in headless Blender (`docs/tooling.md:746-828`), with the
shared helpers in `veg_blender.py` (`reset_scene`, `make_image`, `atlas_material`, `export_glb`,
`read_card`, `compare_render`, `report`). A model is one mesh, one alpha-masked double-sided
material and one atlas, stored as `<chapter>/<model>.glb.bin`. At run time
`VegetationModels.For` draws it on the card's Rich stamps in 256 m `MultiMesh` cells
(`CSVM/src/Mech3/VegetationModels.cs:38`) and hands over to the card by dither at 400 to 600 m
(Near) or 600 to 900 m (Far). There is no per-instance choice between models and no folder for C5.
`VegetationSidecar.cs:206-208` refuses a C5 vegetation entry, and
`CSVM/src/Testing/GraphicsSwitchSuites.cs:2257-2262` pins that C5 builds no added stamp.

**The existing replacement seam.** A model pack's `C5/cb12a.glb` already replaces a building
decoration's mesh on every stamp (`CSVM/src/Mech3/SceneBuilder.cs:1107-1118,1769-1791`), and
collision keeps the original geometry (`docs/formats/model-packs.md:246-252`). It swaps one look
for another; it does not choose between several.

**Performance.** On the Deck, C5 splitscreen is bound by draw submission, and clutter cells are
about half of a spyglass disc's draws and nearly all of its triangles
(`analysis/issue-124-deck-c5-splitscreen/FINDINGS.md:9-24,93-198`). On the PC over CM23's city the
frame holds 0.6 to 0.8 M primitives and 6 to 12.9 ms GPU at 5120x1440
(`analysis/cm23-host-perf/FINDINGS.md:16-18,100-115`). The vegetation budget instrument
`analysis/veg-budget/measure.ps1:24,47-53` has views for C1 to C4 and none for C5.

**Stale figures in the docs.** `docs/formats/clutter.md:125-128` gives 67,836 buildings and 110,668
sprites, and `docs/org/clutter.md:356` gives "139k" sprites; the engine log reads 66,763 3D
decorations and 110,528 sprites. Whether the doc figures describe an older build is unverified.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, survey and instruments

1. ☑ Hull census and a montage of the original buildings
2. ☑ C5 views in the budget instrument, with a baseline
3. ☑ The per-building selection seam, decided from the code

### Wave B, the `cblock1` prototype

11. ☑ Building generator, facade atlas, near models and far boxes for the `cblock1` hulls
12. ☑ Draw the near model and the far box on the building stamps
13. ☑ Per-building facade, tint and the three window modes in the shader
14. ☑ Measure the prototype against the budget
15. ☑ The user's verdict on the prototype

`c5-city-night-enhanced` is re-pinned to the slow look with the window filter (a918f6f3).

### Wave C, the rest of the city

21. ◐ Generate the remaining hulls
22. ☑ Relabel the 3D Vegetation option to cover scenery
23. ☐ Engine checks and golden frames for the new city
24. ☐ Closing measurement, the integrated-GPU default, and follow-up issues

## Dependency and parallelism notes

A1 blocks B11 (the generator needs the measured hulls) and C21. A2 blocks B14 and C24. A3 blocks
B12 and B13. A1, A2 and A3 touch disjoint files and can run in parallel. B11 (Python generators
and model files under `CSVM/data/`) and B12 (C# under `CSVM/src/`) can run in parallel once A1 and
A3 are done, with B12 drawing a placeholder box until B11's files exist. B13 follows B12 and edits
the same material, so the two do not run in parallel. B14 then B15 close the wave, and B15 gates
all of Wave C. In Wave C, C21 and C22 are independent; C23 follows both; C24 is last.

---

# Wave A, survey and instruments

## A1 ☑ Hull census and a montage of the original buildings

**Landed.** `analysis/city-hulls/census.py` measures every C5 building model from `extracted/C5`:
its box over its whole mesh chain, triangles, textures, area-weighted mean colour and luma, the
templates that place or roll it, the fade a stamp uses, and whether each substitute matches its
source in shape. `render.py` draws contact sheets offline. C5 has 71 building models in 61 shapes:
45 building shapes in 32 boxes, plus 16 `det` dressing shapes. Ten of the 39 substitutes are their
source's shape with other textures. A `det` model is a separate, independently rolled decoration
of rails, scaffolding and roof boxes at its building's origin, fading at 75 to 250 m. The further
meshes are the rest of their own building: walls, a cornice, a ground-floor awning, or a tower
setback. `cblock7` draws 6 shapes in 4 boxes. The "about 2,300" in the collision note is distinct
triangles; the code builds 123 shared shapes holding 4,224 triangles. The table is in
`analysis/city-hulls/FINDINGS.md`.

**Verified.** The building decorations per tile are 28, 24, 27, 17, 23, 25 and 25, matching the
survey. The placed total does not match 67,836: the engine's C5 load log reads 66,763 3D
decorations, 54,194 buildings and 12,569 `det` models. The census reproduces the log's 123
collision shapes and 4,224 triangles exactly, and the orchestrator read those three figures in the
log. The montage went to the user: one sheet per template, the seven `cblock7` models side by
side, and every building texture flat. It is drawn with a flat key light and shows no night
lighting, fog, fade or placement density.

**Facade families proposed from the montage**, for the user to judge: warm office tower (`bldg1`,
`bldg4`); dark or steel office tower (`bldg2`, `bldg3`); grey stone walk-up (`smbldg1`, `smbldg2`);
brick tenement (`smbldg3`, `smbldg4`, `smbldg5`, with plain brick side walls and chimneys); deco
landmark accents (the clock and motif on `cb04a`, the teal crown on `cb05a`); street level and trim
(cornices, awning bands, billboards on `cb22` and `cb23`). Roofs are dark tar (58 models) or brown
tile (17 models).

**Original approach (kept for reference).**

**Goal.** A table of every C5 building hull the plan must author, with its measured size, and a
montage of the original buildings for the user to look at before any facade is designed.

**Evidence (confidence: lead-only).** The counts in "What the data actually ships" are names and
counts from the local extraction (`extracted/C5/zrdr/templates.zrd.json` and the template subtrees
in `nodes.json`). Not established: whether a `b`, `c` or `d` substitute differs from its `a` model
in shape or only in texture; how an `a` model and its `det01` half combine into one hull; which
hulls the twelve extra street-wall and roof meshes belong to; the triangle total of the building
meshes; and what the "about 2,300 distinct" collision shapes are.

**Approach.** A read-only script under `analysis/` that walks C5's templates and decoration models
and writes, per model: bounding box, triangle count, texture names, average texture colour and
brightness, the templates that place it, and whether each substitute matches its `a` model in
geometry. Group the models into hulls (an `a` model with its `det` half and extra meshes is one
hull). Record the table in the analysis folder's `FINDINGS.md`, and correct the hull counts in this
plan. Render the originals into a montage. <TODO: which existing tool renders a single original
model to an image; the planning session did not establish one.>

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The building total per template matches the table above (28, 24, 27, 17, 23, 25, 25),
and the placed total matches 67,836. The montage is sent to the user.

**⚠ Traps.** The montage holds original pixels, so it goes to scratch and to the user, never into a
commit. The table holds measured numbers only.

## A2 ☑ C5 views in the budget instrument, with a baseline

**Landed.** `analysis/veg-budget/measure.ps1` measures C5 at four fixed `--freecam` views with
three city modes (`enhanced`, `vdnormal`, `vdunlimited`): `C5-cb7`, 120 m over the unflagged
`cblock7` cells round (-3328, -3072); `C5-cb1`, 160 m over the largest unflagged `cblock1` cells;
`C5-alt`, 450 m up looking north-west across the city to the horizon, where the distant draw-call
rule is read; and `C5-cm23`, CM23's `PLAYER_INIT` pose. The instrument already reported draws and
primitives, so no engine change was needed. `analysis/veg-budget/FINDINGS.md` holds the poses, the
baseline and the reproduction command. At View Distance Far on the PC (RTX 5080, 5120x1421):

| View | Draws | Primitives | GPU (two batches) |
|---|---:|---:|---|
| `C5-cb7` | 945 | 0.61 M | 6.0 to 6.6 ms |
| `C5-cb1` | 893 | 0.77 M | 4.8 to 5.3 ms |
| `C5-alt` | 1,776 | 0.77 M | 3.5 to 3.8 ms |
| `C5-cm23` | 1,160 | 0.58 M | 3.8 to 4.2 ms |

On every view `render_cpu_ms` is 1.2 to 1.7 ms and `proc_ms` 1.1 to 1.4 ms. The Deck baseline is
unmeasured.

**Verified.** The draw and primitive counts repeat exactly in every launch. GPU time agrees to
0.05 ms within a batch (0.3 ms at `C5-cb7`), and two batches differ by 0.3 to 0.6 ms, so B14 and
C24 re-measure the baseline in the same interleaved batch as the change (METHOD-3, PERF-48) and
never subtract this table from a later batch. The instrument can fail on C5 (METHOD-9): View
Distance Unlimited, the never-fading clutter scale, moves `C5-alt` from 1,776 to 4,366 draws and
3.45 to 5.13 ms GPU and every other view likewise, and Normal lowers every count. Launches that
shared the GPU with another session's windowed run were dropped. The orchestrator looked at the
`C5-cb7`, `C5-cb1` and `C5-alt` frames and did not re-run the instrument.

**What it found beyond the baseline.**

- **The large `cblock7` district is under the fog volumes.** The western district lies inside
  `fvol6` and `fvol31` to `fvol34`, whose boxes reach y = 183. A camera below that sees about ten
  buildings in near-black, and from above the district is seen through the cloud field. Outside
  the volumes unflagged `cblock7` stands only in patches of about 1 km², round (-3328, -3072) and
  round (-8960, -1792). A pose over the city is valid only when its log carries no
  `camera state 3` line.
- **`graphics.clutterFarFade` cannot drive the instrument.** It is a config key and `--det` drops
  config; `--view-distance=unlimited` reaches the same scale.
- **The `C5-cb7` frame takes one of two pixel hashes from launch to launch** while its counts
  never move. The surface that flips is not established. A golden at that pose would carry it.

**Original approach (kept for reference).**

**Goal.** The vegetation budget instrument measures C5, and a baseline of today's Enhanced city is
on record: GPU time, CPU time, draw calls and primitives per view, on the PC. The Deck baseline is
unmeasured (Decision 8).

**Evidence (confidence: direction-sound).** `analysis/veg-budget/measure.ps1:24,47-53` has views
for C1 to C4 only. The limit on the Deck is draw submission, not triangles
(`analysis/issue-124-deck-c5-splitscreen/FINDINGS.md`), which is why the baseline must record draw
calls. The PC figures over CM23's city are in `analysis/cm23-host-perf/FINDINGS.md`.

**Approach.** Add fixed C5 views to the instrument: one low over a `cblock7` district, one low over
a `cblock1` tower district, one from altitude with the city to the horizon, and the CM23 city
approach. <TODO: the exact poses.> Run the baseline at View Distance Far, the default.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The instrument is seen able to fail: turning the clutter far fade off, or changing View
Distance, moves the C5 numbers. An unchanged number is not evidence otherwise.

**⚠ Traps.** Read `docs/verification.md` first. Do not start a complete `RunTests.ps1` while a
measurement runs.

## A3 ☑ The per-building selection seam, decided from the code

**Landed.** Decisions 18 to 24 hold the outcome, and B11, B12 and B13 hold the contract it
produced. The city path is a module of its own, `Mech3/CityModels.cs`, beside `VegetationModels`.
It reads a building's hull file, builds its box and near materials, and owns the building seed and
the variant hash. `ClutterBuilder.Build` gives each solid kind with a hull a
`ClutterInstances.City`, and `ClutterInstances` draws it as a third layout beside the Original and
Rich ones. The box stands over the kind's own cells in place of the original mesh, the further
meshes of the building are hidden, and the near variants stand in `CellM` cells keyed by variant.
The original MultiMesh, its collision, the activation, the crater cull and the map edge's
colliders stay as built. Each solid stamp carries a seed hashed on the CPU from its stamped point
in the custom data's `w`. The far tier draws one MultiMesh per dressed kind per kind cell where
the original draws one per mesh of its chain, so the distant draw-call count falls. The near tier
adds at most one draw per variant per dressed kind per near cell, on the layer no spyglass disc
draws.

**Verified.** The item edits no code and runs nothing. The orchestrator re-read the claims the
design rests on: `Clutter.cs:923` writes the fog-volume row into the custom data's `w` for every
stamp; the solid shader passes the custom data only to `csky_clutter_fade_alpha` and
`csky_clutter_dark_lit` (`SceneBuilder.cs:2850-2852`), and `csky_clutter_fade.gdshaderinc` reads
no `w`; the build loop skips a kind whose MultiMesh is null and builds collision only for kinds
that have one (`Clutter.cs:572-574, 600-603`), so the original is suppressed by layout and never
by a null MultiMesh; `ClutterInstances.Draw` already carries an Original layout, a Rich variant
and a near model (`ClutterInstances.cs:86-109`). The draw counts per cell are formulas over A1's
census and are checked by B14. Unverified: whether Godot keeps `TEXCOORD_1` and `baseColorFactor`
on import, and whether a lit window's emission feeds SDFGI from MultiMesh clutter.

**Original approach (kept for reference).**

**Goal.** A written decision, added to this plan's Decisions table, for how a building stamp picks
its geometry variant, how the per-building values (facade row, tint, window seed) reach the shader,
and which module owns the city path.

**Evidence (confidence: lead-only).** Solid kinds are one `MultiMesh` per kind
(`CSVM/src/Mech3/Clutter.cs:1843-1860`), cut into cells under Enhanced
(`ClutterInstances.cs:20-24,231`). `SceneBuilder.SharedMesh` and `Replacement`
(`SceneBuilder.cs:1107-1118,1769-1791`) replace a kind's mesh from a model pack. `VegetationModels`
draws a model on a card's stamps with a dithered hand-over, and never on a solid kind
(`docs/architecture/Mech3.md:320`). No code chooses between several models for one solid building.
The vegetation sidecar refuses C5, and an engine check pins that.

**Approach.** Read `Clutter.cs`, `ClutterInstances.cs`, `VegetationModels.cs` and the model-pack
replacement path, then decide: (1) whether the city path extends `VegetationModels` or is its own
module; (2) how a stamp's world-position hash selects one of a hull's geometry variants, given that
each variant is its own `MultiMesh` per cell and so its own draw call; (3) whether the facade row,
tint and window seed travel as `MultiMesh` instance colour and custom data; (4) how C5's
night-zone lighting key and the fog darkening of city clutter (#142, #311) apply to the new
material; (5) how the dithered fade and View Distance apply to the near model and the far box.
The vegetation sidecar keeps refusing C5; the city does not go through `vegetation.json`.
<TODO: the planning session did not choose between these; A3's output is the choice.>

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The decision names the functions it changes and states the expected draw-call count per
cell for the near tier and the far tier, so B14 has a number to check.

**⚠ Traps.** Draw calls grow with the number of geometry variants present in a cell, and they are
the Deck's limit on C5. Facade variety through the shader costs none, which is why Decision 8
keeps geometry variants to about two per hull and limits them to the near tier.

---

# Wave B, the `cblock1` prototype

## B11 ☑ Building generator, facade atlas, near models and far boxes for the `cblock1` hulls

**Landed.** `CSVM/data/city_models/` builds C5's city models headless in Blender through
`MakeVegetationModel.ps1 -Folder city_models -Species c5`. `c5.py` holds each `cblock1` kind's
hull, measured by `analysis/city-hulls/tiers.py` as horizontal sections from the base up, with its
family and its original's mean colour. `city_blender.py` turns a hull into a far box that is
exactly that profile, and into two near variants with parapets, a set-back top floor or a reshaped
crown, and roof dressing. `city_atlas.py` paints the atlas: four families (grey stone walk-up,
brick tenement, warm office tower, dark steel office tower), each with four facade rows and a
roof, no lit window painted in, and the ids texture holding window id, floor and glass weight.
Each of the fourteen kinds has a `<kind>.glb.bin` with nodes `near0`, `near1` and `far`, beside
`facade.png.bin` and `facade_ids.png.bin`, 860 KB in all. The near models hold 22 to 340
triangles. The far boxes hold 10 to 74; `cb02a`, `cb02b`, `cb03a`, `cb03b`, `cb04a` and `cb04b`
are above 30 because their notches and stepped setbacks need the faces, and each stays at or below
its original's count. A rerun writes every file byte for byte the same, and the export's
`data/*.bin` filter packs them.

**What the shader reads**, as built:

- **Atlas.** 1024 x 1024, cells of 160 px (a 128 px tile and a 16 px wrapped gutter on every
  side). Atlas row f is family f (0 grey stone, 1 brick, 2 warm tower, 3 dark steel tower);
  columns 0 to 3 are the family's facade rows and column 4 its roof. A tile is 8 x 8 m, two floors
  of 4 m. The lookup is `(col*160 + 16 + fract(u)*128, f*160 + 16 + (1 - fract(v))*128) / 1024`
  with `textureGrad` on the unwrapped coordinates.
- **`TEXCOORD_1`** is (family, class: 0 wall, 1 roof). **`TEXCOORD_0`** u carries a whole-number
  base per wall plane, the same on near and far, so every window column of a building has its own
  `floor(u)`. A window's identity is (`floor(u)`, R, `floor(v)*2 + G`). A wall end that would cut a
  window is mapped onto the solid band round a floor line.
- **Ids texture.** R the window id within the tile, 1 to at most 24, 0 for wall; G the floor
  within the tile, 0 lower and 1 upper; B the glass weight, 0 for frames, 140 behind a drawn
  blind, 255 for clear glass, meant to multiply the emission; A 255.
- **Grade.** Each kind's `baseColorFactor` puts its far box's mean atlas colour on its original's
  mean colour. That mean includes the original's painted lit windows, so the unlit walls sit
  brighter than the original's walls; the game's lighting shows whether that needs a correction.

**Verified.** `analysis/city-hulls/margin.py` compares each mesh's height field with the
original's, all meshes of the chain included. Every near and far mesh is inside the margin: at
most 2.0 m inward (a set-back top floor), at most 3.69 m above the hull (a water tank), and no
sideways overhang; each far box reads 0, 0, 0. Parsing the files back shows `TEXCOORD_1` as
(family, class) and a single-sided material carrying the grade. The orchestrator looked at the
three images and sent them to the user: each original beside its near models and far box, the
atlas, and an offline night mock that lights windows from the ids texture alone. The user's
answer to "is this facade and model direction right to build the in-game shader on?" was "Yes,
build on it". The scripts were run by the item's agent and not re-run. Not in the models: the
clock top on `cb04a`, the teal crown on `cb05a` and the tile slopes on `cb04b` keep their shape
and lose their decoration, and no ground-floor shopfront row exists.

**Original approach (kept for reference).**

**Goal.** For each of the twelve shapes `cblock1` places (the table in "What the data actually
ships"): about two near models and one far box, built by script, sharing one C5 facade atlas with
the walk-up and office-tower families.

**Evidence (confidence: lead-only).** The pipeline and its rules are in `docs/tooling.md:746-828`
and `CSVM/data/vegetation_models/veg_blender.py`. `read_card` there reads an original card's size
from `extracted/` at authoring time, which is the precedent for reading a hull's dimensions.
The vegetation models' triangle counts run from 100 to 1,600; no budget exists for a building.

**Approach.** Generators live in `CSVM/data/city_models/`: a shared building builder that reuses
the `vegetation_models/veg_blender.py` helpers, driven by a per-hull spec of measured dimensions
from `analysis/city-hulls/FINDINGS.md`. Outputs go to `CSVM/data/city_models/C5/`, outside
`vegetation_models/` so `VegetationModels.For` never resolves a building. The runner is
`MakeVegetationModel.ps1` with a root parameter, or a sibling script. Each near model stays inside
Decision 7's margin: at most about 3 m carved inward, at most about 4 m of non-solid roof
dressing. What the files hold, from A3:

- **One `<model>.glb.bin` per kind name**, substitutes included (`cb00a`, `cb00b`, `cb01a` to
  `cb01d`, `cb02a` to `cb05b`), named as the decoration without `.flt`. Mesh nodes `near0`,
  `near1` and `far`. Metres, Y up, in the drawn gamez mesh's own frame, covering the main mesh and
  every further mesh of the building (the clock setback on `cb04a`). Opaque, outward winding
  and normals, no bottom face on `far`, and `far` at about the original's triangle count (at most
  30).
- **`TEXCOORD_0`** is facade tile coordinates: u along each wall from the footprint corner in tile
  widths, v up from the base in tile heights, unbounded, identical on near and far so floors line
  up. **`TEXCOORD_1`** is (family row index, surface class: 0 wall, 1 roof). The material's
  `baseColorFactor` is the hull's grade to the original's mean colour.
- **`facade.png.bin`**, the atlas: power-of-two, sRGB, families by rows, each row one seamless tile
  of bays by floors, a roof region per family, and gutters between rows against mipmap bleed.
- **`facade_ids.png.bin`**, the same size and layout, linear RGBA8: R the window id within the
  tile (0 is wall), G the floor index within the tile, B the glass mask, A 255.

<TODO: the near model's triangle allowance, set from A2's baseline and checked in B14.> Confirm
that the export filter covers `data/city_models/**/*.bin`; only `data/*.bin` is documented.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** Each model's bounding box against its hull from A1, with the deviation inside the
margin. `compare_render` or its building equivalent puts each look-alike beside its original for
the user. <TODO: whether `compare_render` works for a building or needs a sibling.>

**⚠ Traps.** No original vertices and no original pixels go into a model or the atlas; dimensions
and average colours are measurements. Generators run through `MakeVegetationModel.ps1` or a
sibling in headless Blender, never through Blender MCP. Facade families come from A1's montage, so
B11 does not start before the user has seen it.

## B12 ☑ Draw the near model and the far box on the building stamps

**Landed.** `Mech3/CityModels.cs` reads a building model's hull from
`data/city_models/<CHAPTER>/<model>.glb.bin`: the `far` node as the box, `near0`, `near1` and so on
as the near variants, and the far material's `baseColorFactor` as a linear grade. A file without
`far`, or with a moved node, is warned and skipped. Each solid stamp's custom data `w` carries
`CityModels.Seed` of its stamped point, a 24-bit integer; cards keep their fog-volume row, and no
solid shader reads `w`. `ClutterBuilder.Build` gives each solid kind with a hull a
`ClutterInstances.City`. A kind whose mesh a model pack replaced gets none. A further mesh of a
dressed building gets a `City` with no box, which hides its node while the city is drawn.
`ClutterInstances` draws the box as a third layout in the kind's own cells. The near variants
stand in `CellM` cells keyed by cell and variant under `ModelRoot`, on the models' layer, with
shadows off. The original layouts and every collider stay built, so a live move swaps nodes. The
city draws only while Rich and a 3D Vegetation word other than Off stand. The city shader is the
clutter's Enhanced lit arm on the flat grade, with no instance uniform. The box fades out across
the band by `ModelShare` and the near variant keeps the dither cells the box gives up. The map
edge's copies draw the box whole, under an unbanded copy of its material. The engine suite
`city-buildings` (in `GraphicsSwitchSuites.cs`) and the unit tests `CityModelsTests` pin it.

**Where it differs from the contract below.** `Hull.Box` is not nullable: a file with no `far`
node is a bad file, since a hull with no box would draw nothing at distance. `CityModels.Root` is
an internal seam so a suite reads fixture hulls from its scratch folder. Every chapter passes its
name to the builder, and a chapter with no vegetation entry gains its model root only when a
building takes a hull. The map edge swaps a copy's mesh and material in place on a live move,
because rebuilding the copies would attach their collision twice.

**Verified.** By the item's agent, on the targeted runs and `-Quick`; the complete battery is the
merge gate and is not run yet. `city-buildings` passes on fixture hulls for 13 dressed kinds: a
pack's `cb00b` stays original; collision shapes, attachments, region bodies and slots equal a
build with no hull; visible clutter nodes go from 3,189 to 3,178 (the `cb04a` setback hidden). It
was seen to fail with the city forced off. Against B11's files every check but the fixture's own
grade passes, and the log reads `city models: 13 kind(s) with a hull (drawn), 199 far node(s),
4991 near node(s), 1 further mesh(es) it hides`. `CityModelsTests` 2 of 2; `clutter-cells`,
`clutter-activation`, `clutter-determinism`, `clutter-rich-variation`, `vegetation-models`,
`vegetation-added-stamps` and `hd-model-swap` green; `-Quick` passes with 1,583 unit tests and 13
engine suites. Of 41 goldens only `c5-city-night-enhanced` moved (52d84488 to abebc4a1, stable on
re-render); it is not re-pinned until B13 has given the buildings their facades. The orchestrator
read the source diff and `CityModels.cs`, and looked at the capture at A2's `C5-cb1` view: the
towers stand as flat-graded look-alikes with setbacks and roof dressing, the `det` dressing still
draws its originals, and the flat grade reads far lighter than the original's dark walls.

**Open after this item.** The near tier adds about 5,400 nodes over the whole map, as many as C5
draws today; B14 measures what culling them costs. The suites that open C5 under Enhanced outside
the targeted list were not run. Map-edge box copies keep a `node_bias` instance parameter their
shader does not declare; whether Godot still gives them an instance-uniform block is unverified.
`city-buildings` is listed as headless-only-failing without a headless run that proves it.

**Original approach (kept for reference).**

**Goal.** Under Enhanced with the option on, a `cblock1` building draws its near model inside the
hand-over distance and its far box beyond it, its original mesh is not drawn, and its collision is
unchanged. With the option off, or under Faithful, the city is exactly today's.

**Evidence (confidence: lead-only).** The vegetation hand-over distances are 400 to 600 m (Near)
and 600 to 900 m (Far), by dither. Building far fades reach 700 to 900 m for towers before the
Enhanced stretch. The seam is whatever A3 decides.

**Approach.** Implement Decisions 18 to 26 for the hulls B11 produces. Hulls without a file keep
their original mesh, so the prototype runs with only `cblock1` dressed, and the `det` dressing
kinds keep drawing their original meshes (Decision 10). The contract from A3, in
order:

1. **`CSVM/src/Mech3/CityModels.cs`** (new, static): `ShippedRoot` (`res://data/city_models`),
   `CellM` (512, TUNE), `On` (`VegetationQualitySetting.RichOn && VegetationModels.On`),
   `Seed(Vector3)` (a 24-bit hash of the point's X and Z bits, returned as an exact float),
   `VariantOf(float seed, int count)`, and `For(chapter, model)` returning a cached
   `Hull(ArrayMesh? Box, IReadOnlyList<ArrayMesh> Near, ShaderMaterial BoxMaterial,
   ShaderMaterial NearMaterial)` read from `<ShippedRoot>/<CHAPTER>/<model>.glb.bin`. No file gives
   null; a bad file logs a warning and gives null. The shader is the clutter's Enhanced lit arm
   assembled from `SceneBuilder`'s shared includes, with no instance uniforms. Until B11's atlas
   exists it draws a flat graded colour.
2. **`ClutterInstances.cs`**: a `City(Mesh? Box, Material? BoxMaterial, Near? Near)` record, a
   third layout chosen in `CellsAt` while the city is wanted (the box over the same placements and
   the same cell edge), `Near` gaining init-only `Variants` and `VariantOf` so the vegetation
   callers do not change, `Layout.Cut` keyed by cell and variant, and `FollowNear` drawing the
   city's near cells at `CityModels.CellM` with shadows off. A further mesh of a dressed building
   keeps its layout and hides its node.
3. **`Clutter.cs`**: at line 923 a solid stamp's custom data `w` is `CityModels.Seed(p)` and a
   sprite's stays the fog-volume row. In the build loop, record `ReplacedMeshCount` around
   `BuildSolidInstance`; a kind whose mesh a model pack replaced takes no city. Otherwise look the
   hull up by `HdModels.NodeKey(kind.Model)`, pass the `City` to `Draw`, and hide the further
   meshes. `Recut` sets the band on the city materials. Log a `city models:` line beside the
   vegetation line.
4. **`MapEdgeExtender.cs`** (around lines 406-418 and 732-750): a dressed solid kind's copies past
   the map edge draw the far box whole, and redraw when the city turns on or off.
5. **`WorldSession.cs:198-199`** passes the chapter.
6. **Docs**: a `CityModels` entry in `docs/architecture/Mech3.md`, its index bullet, the Clutter
   entry's `w` channel, the `ClutterInstances` city layout, and one line in
   `docs/formats/model-packs.md`.

A user model pack's file for a building still wins (Decision 16). Shadows stay off (Decision 17).
The new suite `city-buildings` (C5, Enhanced, Rich, Near) checks: each dressed kind draws its box
in the kind's cells; further meshes are hidden; every stamp is in exactly one near cell and one
box cell; the near variant equals `VariantOf` of the stamp's seed; collision shapes, instances and
per-placement colliders equal an Off build's; visible distant MultiMesh nodes are at most the Off
build's count; live moves to Off, back to Near and to Faithful swap the layouts; a pack's
`C5/<hull>.glb` turns the city off for that kind. Unit tests: `Seed` is deterministic,
`VariantOf` stays in range and both variants occur.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** A capture at A2's `C5-cb1` view with the option off, near and far. The
collision overlay shows the same shapes in all three. The 8-chapter `--freecam` regression is clean
and the other chapters' mesh and node counts are unchanged. <TODO: the exact capture commands.>

**⚠ Traps.** A collider claim is settled in a live `--fly` session, not in a single-frame suite.
The existing engine check that C5 builds no added vegetation stamp must stay true; the city is not
a vegetation entry. Never suppress the original by returning a null MultiMesh from the build: the
loop skips that kind and its building loses its collision. Do not include
`csky_instance_uniforms` in the city shader: every instance that carries an instance uniform takes
a 16-vec4 block of a fixed buffer, and the near tier is thousands of nodes. Do not apply
`VegetationModels.ReachSq` to buildings. The existing suites `clutter-cells`,
`clutter-activation`, `clutter-determinism` and `vegetation-models` pin code this touches and stay
green; the golden `c5-city-night-enhanced` moves once a dressed hull is in its view, and
`c5-city-night` must not.

## B13 ☑ Per-building facade, tint and the three window modes in the shader

**Landed.** `CityModels.LookCode` expands each building's seed in the shader, one text shared word
for word by the far box and the near model. It gives a facade row of the building's family, a warm
or cool lean, a brightness, a lit share between 15 and 70 percent, and a window salt. The facade
atlas is sampled sRGB and mipmapped with `textureGrad` on the unwrapped tile coordinates. The
window ids are read nearest without mipmaps. Both load once per chapter folder, with each facade
row's mean glass weight. A window is (`floor(u)`, its id, its floor) hashed with the salt. A lit
one emits through its glass weight in one of four light colours, mostly warm yellow, and the night
fog darkens it with its building. Under 4 px a window (`FilterPx`) the single windows average a 3
by 3 grid of taps across the pixel along both screen derivatives, so the frame's jitter moves a
lit window's share of a pixel a little and a grazing face keeps its floors. Between 1.75 and
0.75 px a window (`HandOverPx`) the wall hands over to far points: square cells of 1 m doubled per level in the building's tile coordinates, sized
from the pixel's footprint to stay about 1.5 px wide, with two levels blended so a change fades.
Each cell lights by a roll against the building's lit share times `FarPointShare`. The far points
show no whole lit floors and do not follow the clock. The item landed three window modes behind
`--city-windows`: single windows, single windows with whole lit floors, and that mix rolling again
every 300 s (windows) and 360 s (floors) of `csky_time`, each window and floor on its own offset.
B15 kept the last as the only look and removed the other two with the flag. `csky_time` is the
session clock, which steps a fixed amount per frame under `--det`, so a pinned frame holds. A hull folder without the facade files warns and draws the flat
grade. The grade targets the original's unlit wall: `census.py` measures each texture's mean at
or below luma 90, `c5.py` takes those values, and the models are regenerated (the atlas files are
byte for byte the same). The tunables are named constants in `CityModels.cs`.

**Verified.** By the item's agent, on the targeted runs and `-Quick`; the complete battery is the
merge gate and is not run yet. `CityModelsTests` and `SessionSpecParserTests` pass 64 of 64. 19
targeted engine suites, among them every suite that opens C5 under Enhanced, are green with engine
errors clean. `-Quick` passes with 1,586 unit tests and 13 engine suites. The two new
`city-buildings` checks and `OnlyTheSlowModeReadsTheClock` were each seen to fail. Two `--det`
launches are pixel-identical in scatter and in floors. Slow frames 300 s apart differ in 6.9 to
8.4 percent of pixels, and whole floors and single windows are seen to switch. The near model and
the far box differ in 0.5 percent of pixels at a forced hand-over, all of it geometry edges. Of 41
goldens only `c5-city-night-enhanced` moved (to 22d28f5c, stable on re-render); `c5-city-night`
held. Mean linear luminance over the buildings against the original, at 2560x1421: towers 0.017 to
0.020 against 0.027, street 0.026 to 0.031 against 0.022, the `cblock1` patch from altitude 0.012
to 0.013 against 0.015. The orchestrator read the whole source diff and looked at the three
montages, the slow-mode crop and the golden-pose pair, and sent them to the user; it ran no test.

The far points replaced an even mean glow, which read as a beige wash over the towers 500 to
1400 m away at 1280x720. Measured there at the golden pose, the glow was about 60 percent of the
wash and the wall the rest; a mip cap on the atlas moved the far wall's median only from 0.0028 to
0.0030, so the albedo was left alone. With the far points the far band reads mean 0.0057 against
the original's 0.0064, with 3.4 against 3.6 percent of pixels lit, where the even glow read 0.0084
and 1.6 percent. At 2560x1421 the three poses change in 0.01 to 0.18 percent of pixels, all in the
far band. Over 4 m camera steps 75 to 85 percent of lit far pixels stay within one pixel. The same
19 suites, the 64 unit tests and `-Quick` pass again, two `--det` launches are identical in
scatter and in floors, and `c5-city-night-enhanced` now renders 9d63838a, stable on re-render.

The user's flight found the flicker this item left open: "Slow looks good but i got some buildings
that flimmer at certain distances." With the camera still, single windows of 0.5 to 4 px changed
from frame to frame under TAA's jitter, because a nearest window id with a hashed roll is a step.
Measured in the user's view over 8 frames, the share of band pixels whose luma span passes 16 of
255 reads 4.27 percent, 0.000 with TAA off, 0.06 with the single windows off and 6.63 with only
them. The tap filter brings it to 0.10 percent in scatter, 0.14 in slow and 0.12 in floors. The far
band at the golden pose reads 0.00516 against 0.00536 before, so the wash does not return; with
the old hand-over at 0.5 to 1.5 px the filter read 0.00702, which is why the hand-over moved up.
Windows of 4 px and more take one tap and are unchanged. The near city now adds 0.51 ms GPU at
`C5-cb1` (0.25 before) and 0.165 ms at `C5-street` (0.075 before). Two `--det` launches stay
identical in scatter and floors, and the targeted run and `-Quick` pass.

**Open after this item.** The golden `c5-city-night-enhanced` is not re-pinned until the look is
settled in B15, so the goldens stage is red on the branch for that one frame. The filter is
measured at static poses only; motion, the step from nine taps to one at 4 px, the far points
under TAA and the slow mode's pace are judged in B15 at the controls. The original lights each face differently, many of them black,
where the new walls take an even ambient on every face, so the new far walls stay a little lighter
(median 0.0031 against 0.0018). At 1280x720 the converted towers are darker overall than the
original, which paints every window lit (towers view 0.0133 against 0.0228). The floors and slow
modes are about 15 percent brighter than scatter. The map-edge `node_bias` loose end from B12
stands.

**Original approach (kept for reference).**

**Goal.** Two buildings of one hull differ in facade row, tint and brightness and lit windows, each
building looks the same near and far, and a debug switch selects between the three window modes.

**Evidence (confidence: lead-only).** The Rich vegetation path already gives listed cards a
per-stamp tint and brightness (`CSVM/data/vegetation.json`, `docs/architecture/Mech3.md:344-399`).
Nothing equivalent exists for a solid kind.

**Approach.** Hash the building's world position into: a facade row, a tint and brightness inside
a spread, a lit share, and a window seed. The near model and the far box read the same values. The
window modes:

1. **Fixed scatter.** Each building rolls a lit share (about 15 to 70 percent) and each window
   rolls against it. Nothing changes during a flight.
2. **Slow changes.** Single windows and whole floors go on or off now and then during a flight, so
   this mode shows lit floors as well as single lit windows.
3. **Singles and floors.** Single lit windows mixed with whole lit floors, fixed.

Mode 2 is the user's own definition ("mode 2 should toggle single windows or whole floors"); a
whole floor switching is meant to be noticeable. Mode 3 is the planner's reading of the user's
note ("With 3. randomized with single windows and whole floors?") and was not objected to. The lit
share range, the tint spread, the share of lit floors and the light colours are
TUNE and are set in B15.

From A3: `CityModels`' shader expands the stamp's seed (the custom data's `w`) into facade row,
tint, brightness, lit share and window salt, samples the ids texture nearest, and fades each
window's on or off decision toward the building's lit share once a window covers less than about
a pixel. The debug flag is `--city-windows=scatter|slow|floors`, removed with the losing modes
after B15. Mode 2 reads the shader clock. Lit windows start as emission, darkened with distance by
the night fog like the rest of the building; whether they should glow or stay albedo is a look
question for B15.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** A montage of the `cblock1` district in each mode, at A2's `C5-cb1` and `C5-alt` views,
sent to the
user. Two launches produce identical frames in modes 1 and 3. In mode 2 a floor and a single
window are each seen to switch during one flight.

**⚠ Traps.** Mode 2 needs a clock in the shader. A pinned test frame must freeze it, or the golden
hash moves between runs. Whichever mode loses is removed with its switch, not left behind an
option.

## B14 ☑ Measure the prototype against the budget

**Landed.** `analysis/veg-budget/measure.ps1` measures the city by its 3D Vegetation word on C5,
where the word reaches only the city: `nomodels` (off), `near`, `far`, `veryfar`, `extreme`,
`original` (Vegetation Quality Original, a second off) and, until B15 removed the window modes,
`winslow`/`winfloors`, at a fifth view, `C5-street`, 45 m over the `cblock1` street grid.
It also reports scene nodes, the startup line's build times and each mode's `city models:` line.
A `--det` launch with plain `--graphics=enhanced` draws the city at near. On the PC the prototype
adds at most 0.27 ms GPU (`C5-cb1`, near) and 0.29 ms CPU (`C5-street`, extreme), and lowers the
distant draw count at `C5-alt` from 1,776 to 1,709 at near, 1,711 at far and 1,741 at veryfar.
**Extreme raises it to 1,830, which fails Decision 8's draw-call rule.** The three window modes
cost the same within 0.02 ms. The city adds about 290 ms to the world build and 5,452 nodes, which
cost 0.04 to 0.10 ms of `proc_ms` at every view, also out of range. Scaled to the whole city by
building count (an estimate), the near nodes reach about 23,500 and the CPU cost at near
approaches 0.5 ms. Those figures are the prototype with 512 m near cells and the vegetation band
at every word.

The follow-up measured both levers and set them: **`CityModels.CellM` is 2048 m and the city's
band stops at Very Far's under Extreme** (on both hull materials, the live switch in `Recut` and
the near cells' range). The near nodes fall from 5,438 to 768, the mean
`proc_ms` added from 0.059 to 0.013 ms, the worst added CPU from 0.25 to 0.05 ms and the world
build's cost from 240 to 48 ms. Every word holds the draw rule at `C5-alt`: 1,709.5 at near and
1,738.5 at Very Far and Extreme, against 1,775.5 with the city off. No cell size alone held it at
Extreme (1,777.7 at 1024 m, 1,776.5 at 2048 m), so the cap is needed. The larger cell submits more
off-band stamps (primitives +27 to 52 % at Very Far), which costs up to 0.06 ms GPU at `C5-street`.
The whole-city estimate falls to about 3,300 near nodes and 0.1 ms CPU.
`analysis/veg-budget/FINDINGS.md` holds the tables. The Deck is unmeasured. B15 then lifted the
cap (Decision 33): the build keeps the 2048 m cells and hands the city over across Extreme's own
band, at 1,776.5 draws at `C5-alt`.

**Verified.** By the item's agent. Every comparison is interleaved inside one batch, with 3
launches per case, 5 at `C5-cb1` and `C5-street` for the window modes, where the street view's
near delta resolved. The draw and primitive counts repeat exactly in every launch; the extreme
count at `C5-alt` repeats in two batches, and the near cost at `C5-cb1` reads 0.265 and 0.26 ms in
two batches. `nomodels` and `original` reproduce A2's counts exactly, and the `city models:` line
reads `drawn` with 5,438 near nodes in every on mode and `not drawn` with 0 in both off modes. No
other Godot process ran during the batches and no log carries `camera state 3`. The orchestrator
read the script diff and the tables; it ran no launch.

The follow-up is verified by its agent in one interleaved batch of 150 launches (three cell sizes,
three words and off, five views, n=3) and two smaller ones. `proc_ms` follows the node count,
measured both ways: with the nodes varied and the frames pixel-identical it reads +0.059, +0.026
and +0.013 ms at 5,438, 1,877 and 768 nodes, and with the nodes fixed a band that draws no near
model and one that draws every near model add the same. Extreme's lower GPU at `C5-cb1` is the
dither band's double draw, about 0.32 ms at near, which covers less of the frame at Extreme. The
cell size moves no hand-over: frames at `C5-cb1` and `C5-street` are pixel-identical across the
three sizes, and elsewhere at most 596 pixels shift, on the harbour crane. The final build reads
Extreme and Very Far alike at `C5-alt` (1,738.5 draws, 847,162 primitives). The orchestrator read
the diff.

**Open after this item.** `c5-city-night-enhanced` renders 10251f0f with the 2048 m cells (2 px of
delta 1 against B13's frame) and stays unpinned until B15. Which part of Godot's process step
walks the static nodes is not identified. Whether the 37-draw margin at `C5-alt` holds for the
whole city is unmeasured, as is the Deck's vertex cost for the stamps a large cell submits. No
suite switches to Extreme live; the unit test and the pinned launches cover the cap. Only static
poses are measured, with no aircraft, AI or mission.

**Original approach (kept for reference).**

**Goal.** A recorded verdict on whether the prototype holds Decision 8, with the numbers.

**Evidence (confidence: direction-sound).** The budget is the vegetation plan's: at most 2.0 ms GPU
and 0.5 ms CPU added at fixed views, where the vegetation work measured at most 0.75 ms. On the
Deck the 3D vegetation models cost 1.9 to 2.1 ms on C3.

**Approach.** Run A2's instrument on the C5 views with the option off, near and far, on the PC.
Compare draw calls at the altitude view against the baseline. Scale the
`cblock1` result to the full city by hull count from A1 and state the estimate as an estimate.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** Added GPU at most 2.0 ms and added CPU at most 0.5 ms at every PC view; distant draw
calls not above the baseline. The Deck cost is recorded as unmeasured.

**⚠ Traps.** Only the prototype district is dressed, so a view over another district measures
nothing. Measure a cause both ways before asserting it. Two batches of the same view differ by 0.3
to 0.6 ms of GPU, a quarter of the budget, so the off and on cases are interleaved in one batch
and A2's table is never subtracted from a later one.

## B15 ☑ The user's verdict on the prototype

**Landed.** The user flew the prototype in the three window modes and answered the look questions
one at a time; Decisions 4 and 27 to 34 hold the answers. The prototype stays and Wave C goes
ahead. Slow is the window mode, and the scatter and floors modes are removed with
`--city-windows`. Glow, tint spread and wall brightness stay as they are, with further tuning left
to the lighting lab. Common shapes get more than two near variants, the `det` dressing is rebuilt
in the towers' style, and Extreme gets its own reach back, which removes B14's cap and is measured
again in C24. The ground now looks wrong beside the buildings and becomes a follow-up issue in
C24. The flight also found the small-window flicker that B13 records as fixed.

**Verified.** The answers are the user's, given through the question dialog after their flight of
the worktree build. No instrument closed this item.

**Open after this item.** The verdict is on the `cblock1` towers only. The user has not seen the
walk-up districts dressed, the rebuilt dressing, or a third variant.

**Original approach (kept for reference).**

**Goal.** The user has judged the prototype from montages and at the controls, and has chosen the
window mode, the tint spread and the number of variants per hull, or has stopped the plan.

**Evidence (confidence: lead-only).** Three risks were named when the plan was agreed and are
judged here: the 256 m layout and height pattern remains under a reskin, and per-building looks
may not hide it; sharp buildings may make the 2 to 4 m per texel ground look worse; and the
original screenshots `OriginalScreenshots/C5 IA1 Terrain.png` and `C5 IA1 Terrain3.png` are the
reference for the mood.

**Approach.** Send montages (original beside prototype, low and from altitude, each window mode),
then ask the look questions one at a time once the user has flown it. Record the chosen values in
the Decisions table. If the ground or the lamps now look wrong, file a follow-up issue; it is not
added to this plan.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The verdict and the chosen values are in the Decisions table, in the user's words where
they gave a reason.

**⚠ Traps.** A look judgement is the user's. No luminance or similarity number closes this item.

---

# Wave C, the rest of the city

## C21 ◐ Generate the remaining hulls

**Landed so far.** The first tranche dresses `cblock2` and `cblock3`: 14 kinds in 8 shapes, `cb06`
to `cb11`, which also dresses `cb06` and `cb11` where `cblock5` and `cblock6` place them. Each of
these buildings stands turned 45 degrees in its mesh frame. `analysis/city-hulls/tiers.py
--turn=45` measures it turned back, and the generator builds it in its own frame and turns it on
export. The facade families and the atlas are unchanged. A shape placed 1,000 times or more gets a
third near variant and one placed 2,000 times or more a fourth (Decision 31; the counts and the cut
are in `analysis/city-hulls/FINDINGS.md`, "Variants"). That gives `cb00` four, and `cb01a/b`,
`cb02a`, `cb02b`, `cb06` and `cb11` three. The added variants differ in massing: a set-back top
floor, light wells cut 2.5 m into the walls, or a crown stepped in twice. Every other shape keeps
two. New near shapes are `cb07b`'s open frame crown along either axis, a mast or a three-step crown
on `cb09`'s pyramid, and four floors stepping up under `cb10a`'s shed roof. The model files hold
10,582 near and 840 far triangles (4,092 and 410 before). The `city models:` log line counts the
solid stamps that still draw their original mesh. `measure.ps1` gains `C5-cb23`, 65 m over the
`cblock2` cells.

The second tranche dresses `cblock4` and `cblock7`: 11 kinds in 10 shapes, `cb12` to `cb16`, none
turned. `cb12a`, `cb12b`, `cb13`, `cb14a`, `cb14b` and `cb14c` are each placed over 5,000 times
and take four near variants; `cb15` and `cb16` take two. `cb14a` and `cb14b` are one geometry and
draw two sets that share two variants. The new massings are street awnings, light wells above the
awning floor, a low end or a central attic, chimneys and dormers through hipped roofs, a hip or
gable cut to a flat walk, and gable ends that wear the wall (`gables`). The atlas gains two
families, the stone and brick walk-ups under a brown tile roof, painted after the first four so
their cells do not change. It also gains a light metal cell per family, surface class 2, which the
shader takes ungraded. `cb07b`'s top is rebuilt as the original's open frame, after the user said
of the first tranche's closed block: "The top of the original cb07b is either a landing site or
still under construction, the newly generated nearly black tops dont match that." It is slabs at
68.6 and 73.39 m on four light metal corner posts on the far box and one near variant, and a
landing deck with edge rails on the other. `measure.ps1` gains `C5-cb4`, over the `cblock4` cells
round (-4600, -5600).

The third tranche dresses `cblock5` and `cblock6`: 16 kinds in 13 shapes, `cb17` to `cb24`, each
turned 45 degrees in its mesh frame and each placed under 1,000 times, so two near variants each.
`cb21` is `cb13`'s block turned and `cb24a` and `cb24b` are `cb15a` and `cb15b` turned a quarter;
they take those shapes' massings, `cb21a` and `cb21b` a different pair each. A band that a stack
of rectangles cannot hold is built by a hull's `bands` entry: `city_blender.hip_band` builds
`cb17a`'s ridged wings with gable ends, mitred at the corners (a near variant cuts the ridges to a
flat walk), and `cb19b`'s hipped skirt round its notched plan; `c5.py`'s `pyramids` builds
`cb20b`'s twin pyramids, whose apexes stand off centre as measured. `cb17b` keeps its corner stair
tower and pyramid, `cb18` its low pyramid, `cb19a` its notches and set-in top floor, `cb20a` its
stepped podium. `cb19a`'s roof stands at 24.8 m (measured 25.22) so the far box's walls are not
split, and its notch backs stand 4 cm inside its top floor's wall so they keep their windows. The
walk-ups under tile roofs (`cb21` to `cb24`) wear the cornice line; the towers do not, and their
pitched roofs follow each building's tint (Decision 44). `measure.ps1` gains `C5-cb5` and
`C5-cb6`.

The `det` dressing is rebuilt (Decision 46): each of the 16 dressing kinds has a model file like a
building's, one near node and a far node, its pieces at the places measured on the original and
its far node at the original's triangle count. Street awnings are light metal; roof boxes, vents
and a ledge ring's top are the roof surface, its edge light metal. A fire escape is dark plates
for its flights and landings with a lattice band along each flight and a rail over each landing,
and `cb02det02`'s mast is four lattice panels. The lattice is an atlas cell of our own drawing
(board format 4), a truss of light metal bars on black. A hull whose material is named `*.cutout`
draws through a second pair of shaders, the buildings' text with `CityModels.CutCode()` appended,
which discards a lattice texel darker than `CutLuma`; the buildings keep their shader text. The
`city models:` line reads 0 solid stamps drawing an original on C5. The model files hold 21,556
near and 2,378 far triangles. Remaining: the original posters among ours (Decision 45) and the two
roof clashes named under Open.

The billboards of `cb22` and `cb23` show posters of our own (Decision 42) at their measured places
on the far box and both near variants, on the face the original's poster faces (both faces on
`cb23`'s single board), the other face a plain plank back. The boards are surface class 4, which
samples the atlas's free strips ungraded: six landscape and four portrait designs, one picked per
building from its seed so the near model and the far box agree, each a name the game's own text
gives (Blake Aviation, the Tornado engines, the Achilles Armor Company, and the makers Hughes,
Wright, Bristol, Ford and Colt) or a plain word, in block lettering over plain shapes of our own.
`CityModels.PosterGain` (1.25, an albedo gain) lifts a poster toward the original's lit poster on
a night frame. `cb17a`'s gable ends are red brick (Decision 43), a board cell at the original
gable texture's unlit mean colour; the brick family's band under the kind's grade came out about
2.5 times too dark. The atlas's existing cells and window ids are byte-identical.

The walk-up districts under tile roofs vary in colour (Decision 35). The shader picks each tile
roof's colour from the building's seed out of `CityModels.Roofs`: weathered brown 30 %, terracotta
20 %, dark slate 15 %, blue-grey slate 15 %, green-grey 10 %, near-black tar 10 %. A colour is
applied as its ratio to the atlas's brown, so the tiles, the grade and the agreement of near model
and far box hold. The two tile-roof atlas families (rows 4 and 5) paint their four wall rows in
colours of their own (`city_atlas.py`'s `TILE_WALLS`): grey stone, buff limestone, brownstone and
grey-green render; red, brown and buff brick and a painted wall. Each family's mean colour is held
to its former value, so every hull file and grade is byte-identical and only `C5/facade.png.bin`
changed. The tar roofs (the towers, and families 0 and 1 with the `cb14` houses) are unchanged.
The change reaches `cb12`, `cb13`, `cb15` and `cb16`: 12 of 17 buildings per `cblock4` tile and 14
of 25 per `cblock7` tile.

The walk-ups under a tile roof (`cb12`, `cb13`, `cb15`, `cb16`) wear a thin near-white cornice
line (Decision 37). The top 0.6 m of the wall under every roof edge open to the sky and under
every eave is surface class 3, which samples the light metal cell ungraded and multiplies it by
`CityModels.CorniceGain` (2.5), an albedo gain that emits nothing. It is on every near variant and
the far box, and follows set-back floors, light wells, a low end, a central attic and `cb15b`'s
raised middle; roofs at street-floor height take none, and parapets stay brick above it. Height
and gain are matched to the original's line on a night frame: about 0.6 m, and a raw luma peak of
about 120 against the original's 120. Class 2 is unchanged, so `cb07b`'s posts stay as they were.
Each far box gains 8 triangles and stays under its original (at most 64 of `cb15b`'s 96).

No window is cut at a corner (Decision 41). Each facade family's four rows share a pier every 4 m
(`city_atlas.grid_m`): the eight walk-up rows with 3 bays and the three tower rows with 5 bays are
re-spaced to 4, and nothing else in their spec changes. A wall plane holds a whole number of 4 m
bays between its ends, stretched by at most 10 % (8.6 % on `cb02b`, 6.7 % on `cb05a`, under 5 %
elsewhere), the same on the far box and the near variants. The variants' set-backs, light wells,
low ends and attics stand on that grid (`c5.py`). Solid strips remain only on the 2.5 m sides of
light wells and notches and at the ends of the long-side set-backs of `cb12b` and `cb14c`. A
dormer front is 2 m wide and shows one whole window of every walk-up row, off centre in the
paired row. `cb02a`'s L-step moves 1.33 m inward onto its grid and its variants use shallower
set-backs to stay in the margin. `analysis/city-hulls/corners.py` checks every windowed wall edge
of the shipped models against every row's bays. A corner shows 0.4 to 0.5 m of wall on each side,
the gap between two neighbouring windows. The walk-ups carry 16 windows per tile floor where they
had 14, the warm towers 22 for 23 and the dark towers 22 for 24.

**Verified so far.** By the tranche's agent. Every near and far mesh is inside the margin
(`analysis/city-hulls/margin.py`): at most 2.5 m in, 3.77 m above, nothing sideways, and every far
box reads 0, 0, 0. The `cblock1` files without a new variant and the atlas are byte-identical. The
log reads 38 kinds with a hull, 2,066 near nodes, and 49,281 solid stamps in 58 kinds still drawing
their original (12,569 `det` halves and 36,712 buildings of `cblock4` to `cblock7`). In one
interleaved batch, n=3, the city adds at most 0.595 ms GPU (`C5-cb1`, near) and 0.10 ms
`render_cpu_ms` (`C5-cb23`, extreme). `C5-alt` draws 1,679.9 at near and 1,808.9 at Extreme,
against 1,775.5 with the city off. The targeted suites, `CityModelsTests` and `-Quick` (1,584
units, 13 engine suites) pass. Of 41 goldens only `c5-city-night-enhanced` moved; it is re-pinned
to 144dee34, pixel-identical on re-render. The orchestrator read the C# and script diff, looked at
the district and variant montages and sent them to the user.

The second tranche is verified by its agent. Every new near and far mesh is inside the margin: at
most 3.0 m in (the light wells), 3.70 m above, nothing sideways. The existing atlas cells and
every model file but `cb07b`'s are byte-identical. The log reads 52 kinds with a hull, 3,339 near
nodes, and 15,065 solid stamps in 44 kinds still drawing their original (12,569 `det` halves and
2,496 `cblock5`/`cblock6` buildings). In one interleaved batch, n=3, over seven views the city
adds at most 0.585 ms GPU (`C5-cb1`, near) and 0.16 ms CPU (`C5-cb1`, extreme). `C5-alt` draws
1,649.9 at near and 1,778.9 at Extreme against 1,775.5 with the city off: a dressed kind's distant
cells draw one box where the original drew its mesh chain, so the excess fell. The targeted
suites, `CityModelsTests` and `-Quick` (1,584 units, 13 engine suites) pass. Of 41 goldens only
`c5-city-night-enhanced` moved; it is re-pinned to 2a653847, pixel-identical on re-render, and the
golden stage reads 41 of 41 after the pin. The orchestrator read the C# diff, looked at the
`cblock7` district montage and the `cb07b` night crop, and sent the montages to the user.

The colour variety is verified by its agent. A cell-by-cell comparison of the regenerated atlas
shows families 0 to 3, every roof and metal cell and the window ids byte-identical, and the 39
hull files are byte-identical. A tower view (`C5-cb1`) differs in 0.02 % of its pixels by one
level. Near reach and Extreme show the same roof colour building for building across the
hand-over, and two `--det` launches give byte-identical frames. `CityModelsTests` (3), five
targeted engine suites and `-Quick` (1,584 units, 13 engine suites) pass. Of 41 goldens only
`c5-city-night-enhanced` moved; it is re-pinned to 55b3fc37, pixel-identical on re-render, and the
golden stage reads 41 of 41 after the pin. The orchestrator read the diff, looked at the `cblock4`,
`cblock7` and tower montages and sent them to the user, who chose to land it as shown. Its cost
was not measured (one roll and one multiply on roof pixels).

The cornice is verified by its agent. Every changed mesh is inside the margin with nothing
sideways (at most 3.00 m in, 3.70 m above). The atlas and the 33 other model files, `cb07`'s
included, are byte-identical, and the `C5-cb1` and `C5-cb23` tower views are pixel-identical
before and after. On 8-frame `--det` bursts with the camera pinned, at a pose where the line is 1
to 2 pixels and one where it is a pixel or less, the line raises the mean frame-to-frame luma
change from 0.14 to 0.58 and from 0.20 to 0.63 levels, and at most 0.03 % of its pixel pairs change
by more than 16 levels, so it does not crawl. `CityModelsTests`, the five targeted engine suites
and `-Quick` (1,584 units, 13 engine suites) pass. Of 41 goldens only `c5-city-night-enhanced`
moved; it is re-pinned to 62905965, pixel-identical on re-render, and the golden stage reads 41 of
41 after the pin. The orchestrator read the diff and the montages and sent them to the user. Not
measured: the line under a moving camera, and its cost.

The corner fix is verified by its agent. `corners.py` reads 0 cut windows on every near variant
and far box, against 597 on the files before it, with no window column shared by two planes and
14 dormer edges clear of any window by 0.1 m or more. Every mesh is inside the margin (at most
3.00 m in, 3.77 m above, nothing sideways). Every far box keeps its triangle count, each at or
under its original (`cb02a` 46 of 50); the near models go from 14,836 to 15,444 triangles. Eleven
atlas cells changed and four more by one level; every other cell is byte-identical. The grades
moved by at most 2.8 % per channel. Near reach and Extreme show the window columns of a building
in the same places. `CityModelsTests`, the five targeted engine suites and `-Quick` (1,584 units,
13 engine suites) pass. Of 41 goldens only `c5-city-night-enhanced` moved; it is re-pinned to
47a9c340, pixel-identical on re-render, and the golden stage reads 41 of 41 after the pin. The
orchestrator read the atlas and doc diff and the montages and sent them to the user. Not checked:
the dormers in game at close range, the cost.

The third tranche is verified by its agent. Every near and far mesh of the 55 models is inside the
margin: at most 2.83 m in, 3.69 m above, nothing sideways on the new ones; each new far box is
under its original's triangle count (`cb19b` 84 of 96). `corners.py` reads 0 cut windows over all
55 models. The 39 model files that existed before are byte-identical, as are the window ids and
the atlas's 36 existing cells. The log reads 73 kinds with a hull, 3,852 near nodes, and 12,569
solid stamps in 23 kinds still drawing their original, the `det` halves. In one interleaved batch,
n=3, over nine views, taken before the posters and gables, the city adds at most 0.665 ms GPU
(`C5-cb1`, near) and 0.116 ms CPU (`C5-cb23`, extreme). `C5-alt` draws 1,626.1 at near and 1,777.1
at Extreme against 1,775.5 with the city off. A poster on a night frame reads a mean raw luma of
80 against the original's 97 at the same pose, peaking at 169 against the new lit windows' 158;
the plank back reads 46.8 against the original's 46.7. On pinned 8-frame bursts at about 300 m a
board changes by 0.47 levels frame to frame (the original's board 0.56) and no pixel pair by more
than 16. `CityModelsTests`, the six targeted engine suites and `-Quick` (1,584 units, 13 engine
suites) pass. Of 41 goldens only `c5-city-night-enhanced` moved; it is re-pinned to 193f972e,
pixel-identical on re-render, and the golden stage reads 41 of 41 after the pin. The orchestrator
read the shader, generator and doc diff and the montages and sent them to the user; `c5.py`'s
specs were not read line by line, the margin and corner scripts and the sheets stand for them. Not
measured: the cost of the boards and gables (the display had changed to 1920x1080 and another
session's run overlapped the batch), which C24's rerun covers.

The dressing is verified by its agent. Every dressing node stays within its original's box grown
by 0.05 m (at most 0.006 m past it), each far node holds its original's triangles (566 in all),
and `corners.py` reads 0 faults over all 71 models. The 55 earlier model files and the window ids
are byte-identical, and the atlas changed in the new cell alone. With the dressing files set aside
and the earlier atlas, the code renders the earlier build's `cblock5` frame pixel for pixel; the
new atlas moves 10 horizon pixels by one level through its deepest mips. On pinned 8-frame bursts
a fire escape at about 80 m changes by 0.08 levels frame to frame (the original's 0.14), no pixel
pair by more than 16; the whole frame at about 230 m changes by 0.14 (the original's 0.12). The
log reads 96 kinds with a hull, 4,433 near nodes and 0 solid stamps drawing an original.
`CityModelsTests` (4), the six targeted engine suites and `-Quick` (1,585 units, 13 engine suites)
pass. The orchestrator read the shader and test diff and the montages and sent them to the user;
`c5.py`'s piece builders were not read. Not run: the golden stage, so `c5-city-night-enhanced`'s
pin may be stale, and the cost batch, since other sessions held the GPU.

**Open so far.** The 2.5 m sides of light wells and notches are plain wall, and the paired row's
dormer window sits off centre. Lit windows in the pinned frames rose by 4 to 7 % over the walk-ups
and fell by 6 to 7 % over the towers with the re-spaced rows; `LitShareMin/Max` are not retuned.
Extreme's distant draws at `C5-alt` are 1.6 over the city off (3.4 after the second tranche, 33
after the first, one with the prototype); Decision 33 puts the excess to the user in C24. On
`cb00`'s fourth near variant its own tank reaches into the dressing's roof box, and on `cb02b`'s
first a vent stands inside it. The lattice has one zigzag per band where the original's truss
shows finer diamonds, and the dressing's roof boxes read paler than the original's; the user keeps
both (Decision 46). The golden stage and the cost of the dressing are not run. The solid strips include `cb19a`'s 3.22 m notch sides and the sides of the
light wells and notches of `cb21a`, `cb22`, `cb23` and `cb24b`. `cb18b`'s arched windows are not
drawn. Five of the ten posters carry names of real firms the game names as makers (Hughes, Wright,
Bristol, Ford, Colt), each one cell of `paint_posters`. `cb17a`'s gable reads about 1.75 times
brighter than the original's on a night frame, as the walls do, and a poster's cream peaks about
7 % above a lit window. The cost of the boards and gables is unmeasured. `cb12b`'s chimney wears the roof cell where the original's is
orange-red brick, and `cb03a` and `cb08a` show a reddish band on the top roof edge that is not
traced to a texture. The new walk-ups wear brighter, more tinted walls than the originals' dark
ones, which the user keeps (Decision 38). A1's "hipped, lower hipped and gabled" for `cb14a`, `cb14b` and `cb14c` is wrong: `cb14a`
and `cb14b` are one hipped geometry. From altitude the new towers read warmer than the original's
blue-white ones, which the user keeps (Decision 39). `city-buildings` checks
fixture hulls, not the shipped geometry; the margin script is the geometry check. The Deck is
unmeasured.

**Original approach (kept for reference).**

**Goal.** Every C5 building hull from A1's census has its near models and its far box, and no
building in the Enhanced city draws an original mesh.

**Evidence (confidence: lead-only).** The hull count is A1's. The facade families, variants per
hull and tint spread are B15's.

**Approach.** Extend B11's per-hull specs to the remaining 33 shapes, district by district:
`cblock2` and `cblock3` (the rest of the towers, up to 108 m), then `cblock4` and `cblock7` (the
walk-ups, more than half of the placed buildings), then `cblock5` and `cblock6`. A shape placed
often gets a third or fourth near variant and a rare one keeps two (Decision 31), by A1's
placement counts, the `cblock1` shapes included. Each of the 16 `det` dressing shapes gets a model
in the towers' style, and its original is not drawn in the Enhanced city (Decision 32).

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** Per hull, the bounding-box check from B11. A log line or check that counts building
stamps still drawing an original mesh under Enhanced reads zero on C5. A montage per district for
the user.

**⚠ Traps.** The `hotelsign0/1/2` blocks are placed by no template; they need no model. The 16
`det` shapes are not hulls: they are separate decorations with their own stamps, so how a rebuilt
dressing piece is drawn and its original suppressed is traced in the code before it is built. The
cut that puts a shape among the common ones is a judgement; state the counts and the cut. Every
added variant moves which variant a building draws (`VariantOf` takes the count), so the look of
the prototype's buildings changes with it.

## C22 ☑ Relabel the 3D Vegetation option to cover scenery

**Landed.** The VIDEO option behind `VegetationModelsSetting` reads "3D Scenery" (Decision 40,
`OriginalVideoPage.cs`), and its description is "Rich vegetation only: trees, crowns and city
buildings in 3D." It is one sentence because the page draws only a description's first. Off's
Original-world line says the original world keeps its tree cards and buildings. The stored key
`graphics.vegetationModels`, the saved field `vegetationModels`, the words, the
`--vegetation-models` flag and every identifier are unchanged. Every live doc and comment that
named the option by its label says 3D Scenery, and the setting's architecture entry and the menu
inventory's Video row say that the option gates C5's script-built city. Release notes keep the
old label.

**Verified.** By the item's agent. `VegetationModelsSettingTests` reads a literal options file,
`{"version":1,"vegetationModels":"veryfar"}`, back as `veryfar`. The targeted run passes 121 of
121 units and the `menu-original-tracer-core`, `menu-original-tracer`, `vegetation-models` and
`city-buildings` suites, and `-Quick` passes 1,584 units and 13 engine suites. A 1920x1080
capture of the VIDEO page with the row on the window's bottom line shows the label inside its
title box and the two-line description clear of the row below. No golden shows the VIDEO page.
The orchestrator read the diff and the capture and sent the capture to the user.

**Open.** `docs/cli.md`'s `--vegetation-models` entry does not mention the city. The headset's
VIDEO page, the Deck's 1280x800 and the Original-world description are not captured.

**Original approach (kept for reference).**

**Goal.** The VIDEO option that drives the vegetation models and the city buildings has a label
that says so, and its stored key is unchanged.

**Evidence (confidence: lead-only).** The option is `VegetationModelsSetting` (off, near, far).
The integrated-GPU fallback is in `CSVM/src/Utils/VegetationQualitySetting.cs:58,64-65`.

**Approach.** Change the label text and every doc line that names the option. <TODO: the label's
final wording ("3D Scenery" was the working name) and the list of places that name the option.>

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** <TODO: the menu suite or capture that shows the label.> A settings file written before
the change still loads with its value.

**⚠ Traps.** The key and the stored values do not change; only the label does.

## C23 ☐ Engine checks and golden frames for the new city

**Goal.** The new city is pinned: a check that C5's buildings draw script-built models under
Enhanced and originals under Faithful, and golden frames for the C5 Enhanced views.

**Evidence (confidence: lead-only).** `CSVM/src/Testing/GraphicsSwitchSuites.cs:2257-2262` is the
existing C5 check and stays true. <TODO: which golden frames show C5 under Enhanced today and will
move.>

**Approach.** Add the check beside the existing graphics-switch checks. Re-pin the C5 Enhanced
goldens that move, and add one frame at A2's `C5-cb1` view. A frame over `cblock7` is valid only
outside the fog volumes, and A2's `C5-cb7` pose takes one of two pixel hashes from launch to
launch, so it is not a golden pose until that flip is found.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The complete `.\scripts\RunTests.ps1`. The new check is seen to fail with the option
off.

**⚠ Traps.** A golden that flips under external GPU load is a determinism defect, not a tolerance
to widen. The windows roll on `csky_time`, which steps a fixed amount per frame under `--det`, so
a pinned frame holds; a frame pinned past 300 s of session time would show rolled windows and is
not covered by `c5-city-night-enhanced`, which captures at frame 120.

## C24 ☐ Closing measurement, the integrated-GPU default, and follow-up issues

**Goal.** The full city is measured against Decision 8, the option's default per GPU class is set,
and anything the user's eyes asked for outside this plan's scope is filed.

**Evidence (confidence: direction-sound).** As B14, now with every hull dressed.

**Approach.** Rerun A2's instrument on all C5 views on the PC. An integrated GPU defaults to the
original city at one pane (Decision 8), and at two or more panes the existing fallback already
shows it. File an issue for the Deck one-pane measurement that would lift that default.
File the follow-up issue for the ground paint, which B15 asked for ("Ground looks wrong",
Decision 34), with the user's own description of what is wrong; file one for the lamps or the
layout pattern only if a later look asks for it. Measure Extreme's distant draws at `C5-alt` with
every hull dressed and put the excess over the original city to the user (Decision 33).

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The budget numbers hold at every view, and distant draw calls are not above A2's
baseline. The 8-chapter `--freecam` regression is clean.

**⚠ Traps.** The Deck's splitscreen measurement is bound by draw submission; a triangle count that
looks fine says nothing about it.
