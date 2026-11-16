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
- Not every window is lit. The window behaviour is the one the user chose in the `cblock7`
  prototype.
- The city is identical on every launch and on every multiplayer client, with nothing synchronised.
- The added cost stays inside the vegetation budget on the PC, the distant draw-call count on C5
  does not rise, and the Deck follows the existing vegetation fallback.

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
| 4 | Window lights | **Decided by the user's eyes in the prototype**, between three modes behind a debug switch (see B13). The atlas carries a window id and a floor id from the start. |
| 5 | Scope | **Buildings only.** Ground paint, lamps and cars wait for the prototype verdict. |
| 6 | Style | **Look-alikes**: facade families taken from what the originals show, each variant graded to its original's average colour and brightness, so C5's fog and lighting tuning stays valid. |
| 7 | How far may a model leave its collision hull? | **About 3 m carved inward and about 4 m of non-solid roof dressing above** (water tanks, vents, parapets). Tall spires and deep setbacks only where the original hull has them. Both numbers are TUNE. |
| 8 | Budget | **The vegetation budget**: at most 2.0 ms GPU and 0.5 ms CPU added at fixed PC views, with C5 views added to that instrument, plus the C5 rule that the distant draw-call count does not rise. The Deck follows the existing vegetation fallback. |
| 9 | Menu option | **The existing 3D Vegetation option, relabelled to cover scenery.** Off is the original city; near and far set the hand-over distance. The stored key stays. |
| 10 | Rollout | **A `cblock7` prototype first**, then the remaining hulls after the user's verdict. |
| 11 | Authoring | **Python generators through headless Blender**, as the vegetation models are built. Blender MCP is not part of the pipeline. |
| 12 | Faithful look | Untouched. |
| 13 | Mirroring or turning a building | None. A turned model no longer matches its collision. |
| 14 | Where a building's variety comes from | A hash of its world position. Nothing is random at run time and nothing is synchronised. |
| 15 | What goes into the repo | Models built from measured dimensions. No original vertices and no original pixels. |
| 16 | Model packs | A user model pack still overrides a shipped building, as it does for vegetation. |
| 17 | Shadows | Buildings keep casting none. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "C5 has 25 building hulls, and `cblock7` has three." | 25 counts only the base `a` models. C5 authors 77 decoration models, and the `b`, `c` and `d` substitutes reached by the substitute roll are separate meshes with their own collision. Whether their shapes differ from the `a` model is unmeasured; A1 measures it. The full city may be nearer 50 hulls, and `cblock7` up to six. |

Everything in "What the data actually ships" comes from one read-only survey of the docs, the code
and the local extraction. Its file and line references were not re-read when this plan was written.
Confirm each before building on it.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | none | No item here is ready to implement without a confirming read. |
| **Direction sound, magnitude a judgement call** | A2, B14, C24 | The budget and the draw-call limit rest on measurements; the C5 view poses and the near triangle allowance are TUNE. |
| **Leads only, no mechanism yet** | A1, A3, B11, B12, B13, B15, C21, C22, C23 | Budget for investigation. A3 in particular may show that the seam assumed here does not exist in the form described. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

**The city.** C5 is Manhattan at night (`docs/formats/campaign-missions.md:56-59`). The ground is
seven painted city-block textures, `cblock1` to `cblock7`, at 256 m per texture repeat
(`analysis/item9-depth-bias/CBLOCK-LOD.md:51-60,100-112`). Each texture is a clutter template with
one fixed building layout. Where two layers overlap, the `no_clutter` polygon flag picks the
dressed one: flagged ground gets `cblock4/5/6` (low-rise, up to 52 m), clear ground gets
`cblock1/2/3/7` (towers, up to 108 m) (`docs/org/clutter.md:163-175`).

**Decorations per template** (counted over the local extraction, names and counts only):

| Template | Unflagged area | Buildings | Lamps |
|---|---|---|---|
| `cblock1` | 51.5 km² | 28, from 10 models (`cb00a` to `cb05a` plus four `det01`) | 34 poles, 34 glows |
| `cblock2` | 9.6 km² | 24 | 20, 20 |
| `cblock3` | 5.0 km² | 27 | 33, 33 |
| `cblock4` | flagged ground | 17, from 5 models (`cb12a` to `cb16a`) | 12, 12 |
| `cblock5` | flagged ground | 23 | 20, 20 |
| `cblock6` | flagged ground | 25 | 33, 33 |
| `cblock7` | 75.8 km² | 25, from 3 models (`cb12a` ×10, `cb13a` ×4, `cb14a` ×11) | 12, 12 |

Areas are from `analysis/bl-305-clutter-uv/FINDINGS-noclutter.md:161-166`.

**Models.** `templates.zrd` authors 77 decoration models for C5 (`docs/formats/templates.md:135-145`,
`CSVM.Tests/ClutterTemplatesTests.cs:24-36`). Templates place `cb00a` to `cb24a` and seven
`cbNNdet01` detail halves directly; the `b`, `c`, `d`, `det02` and `det03` models are reached only
through the substitute roll, mostly 50/50 between `a` and `b`. A building's `a` and `det01` parts
are two halves drawn at the same origin, not a LOD pair (`docs/formats/clutter.md:157-163`).
Twelve blocks carry extra street-wall and roof meshes (`docs/formats/clutter.md:55-62`). Buildings
are real meshes of 2 to 27 polygons with 64 to 128 px night textures that have the lit windows
painted in.

**Placement.** One stamp per integer repeat of the ground texture's UV across each triangle
(`docs/org/clutter.md:196-206`). No position, rotation or orientation variation is authored in any
chapter (`docs/org/clutter.md:121-138`), every C5 block authors `scale_range` 1 to 1, and the seed
is fixed (`CSVM/src/Mech3/Clutter.cs:77`). The only per-stamp variety is the substitute roll, so
each 256 m tile of a template repeats one layout.

**Drawing.** Each kind is one `MultiMesh` with shadows off (`Clutter.cs:1843-1860` solids,
`1865-1891` extra building parts). 67,836 buildings are placed (`docs/formats/clutter.md:125-128`).
Far fade bands: detail halves about 75 to 500 m, small blocks 200 to 400 m, the 21 tower kinds 700
to 900 m, dithered in the cutout pass (`docs/org/clutter.md:279-312`). Under Enhanced the fade is
stretched by fog scale and View Distance (`CSVM/src/Launch/EnhancedLook.cs:262-265`), and each kind
is cut into map cells with a visibility range (`CSVM/src/Mech3/ClutterInstances.cs:20-24,231`);
C5 has 5,425 clutter cells. Collision is one shared concave shape per distinct mesh, attached per
placement (`docs/formats/clutter.md:189-204`).

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
decoration's mesh on every stamp (`CSVM/src/Launch/SceneBuilder.cs:1107-1118,1769-1791`), and
collision keeps the original geometry (`docs/formats/model-packs.md:246-252`). It swaps one look
for another; it does not choose between several.

**Performance.** On the Deck, C5 splitscreen is bound by draw submission, and clutter cells are
about half of a spyglass disc's draws and nearly all of its triangles
(`analysis/issue-124-deck-c5-splitscreen/FINDINGS.md:9-24,93-198`). On the PC over CM23's city the
frame holds 0.6 to 0.8 M primitives and 6 to 12.9 ms GPU at 5120x1440
(`analysis/cm23-host-perf/FINDINGS.md:16-18,100-115`). The vegetation budget instrument
`analysis/veg-budget/measure.ps1:24,47-53` has views for C1 to C4 and none for C5.

**Unresolved in the survey.** The placed sprite count is 110,668 in `docs/formats/clutter.md` and
"139k" in `docs/org/clutter.md:356`. The collision note speaks of about 2,300 distinct shapes,
which does not obviously square with 77 decoration models. The total triangle count of C5's
building meshes is unknown.

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

1. ☐ Hull census and a montage of the original buildings
2. ☐ C5 views in the budget instrument, with a baseline
3. ☐ The per-building selection seam, decided from the code

### Wave B, the `cblock7` prototype

11. ☐ Building generator, facade atlas, near models and far boxes for the `cblock7` hulls
12. ☐ Draw the near model and the far box on the building stamps
13. ☐ Per-building facade, tint and the three window modes in the shader
14. ☐ Measure the prototype against the budget, PC and Deck
15. ☐ The user's verdict on the prototype

### Wave C, the rest of the city

21. ☐ Generate the remaining hulls
22. ☐ Relabel the 3D Vegetation option to cover scenery
23. ☐ Engine checks and golden frames for the new city
24. ☐ Closing measurement, the Deck default, and follow-up issues

## Dependency and parallelism notes

A1 blocks B11 (the generator needs the measured hulls) and C21. A2 blocks B14 and C24. A3 blocks
B12 and B13. A1, A2 and A3 touch disjoint files and can run in parallel. B11 (Python generators
and model files under `CSVM/data/`) and B12 (C# under `CSVM/src/`) can run in parallel once A1 and
A3 are done, with B12 drawing a placeholder box until B11's files exist. B13 follows B12 and edits
the same material, so the two do not run in parallel. B14 then B15 close the wave, and B15 gates
all of Wave C. In Wave C, C21 and C22 are independent; C23 follows both; C24 is last.

---

# Wave A, survey and instruments

## A1 ☐ Hull census and a montage of the original buildings

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

## A2 ☐ C5 views in the budget instrument, with a baseline

**Goal.** The vegetation budget instrument measures C5, and a baseline of today's Enhanced city is
on record: GPU time, CPU time, draw calls and primitives per view, on the PC and on the Deck.

**Evidence (confidence: direction-sound).** `analysis/veg-budget/measure.ps1:24,47-53` has views
for C1 to C4 only. The limit on the Deck is draw submission, not triangles
(`analysis/issue-124-deck-c5-splitscreen/FINDINGS.md`), which is why the baseline must record draw
calls. The PC figures over CM23's city are in `analysis/cm23-host-perf/FINDINGS.md`.

**Approach.** Add fixed C5 views to the instrument: one low over a `cblock7` district, one low over
a `cblock1` tower district, one from altitude with the city to the horizon, and the CM23 city
approach. <TODO: the exact poses.> Run the baseline at View Distance Far, the default. <TODO: the
Deck run recipe and whether the instrument runs there as is.>

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The instrument is seen able to fail: turning the clutter far fade off, or changing View
Distance, moves the C5 numbers. An unchanged number is not evidence otherwise.

**⚠ Traps.** Read `docs/verification.md` first. Do not start a complete `RunTests.ps1` while a
measurement runs.

## A3 ☐ The per-building selection seam, decided from the code

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

# Wave B, the `cblock7` prototype

## B11 ☐ Building generator, facade atlas, near models and far boxes for the `cblock7` hulls

**Goal.** For each hull that `cblock7` places (`cb12a`, `cb13a`, `cb14a` and whatever substitutes
A1 finds): about two near models and one far box, built by script, sharing one C5 facade atlas.

**Evidence (confidence: lead-only).** The pipeline and its rules are in `docs/tooling.md:746-828`
and `CSVM/data/vegetation_models/veg_blender.py`. `read_card` there reads an original card's size
from `extracted/` at authoring time, which is the precedent for reading a hull's dimensions.
The vegetation models' triangle counts run from 100 to 1,600; no budget exists for a building.

**Approach.** A shared building builder beside the `veg_*.py` builders, driven by a per-hull spec
of measured dimensions from A1. <TODO: the builder's name and the folder for C5's model files;
no C5 folder exists.> Each near model stays inside Decision 7's margin: at most about 3 m carved
inward, at most about 4 m of non-solid roof dressing. The far box has about the original's
triangle count. The atlas holds several facade rows per family, generated by script, graded to the
original's average colour and brightness from A1, and carries a window id and a floor id per
window. <TODO: how the ids are stored (a second texture, a channel, or vertex data); A3 informs
it.> <TODO: the near model's triangle allowance, set from A2's baseline and checked in B14.>
Files are stored with the `.bin` extension like the vegetation models.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** Each model's bounding box against its hull from A1, with the deviation inside the
margin. `compare_render` or its building equivalent puts each look-alike beside its original for
the user. <TODO: whether `compare_render` works for a building or needs a sibling.>

**⚠ Traps.** No original vertices and no original pixels go into a model or the atlas; dimensions
and average colours are measurements. Generators run through `MakeVegetationModel.ps1` or a
sibling in headless Blender, never through Blender MCP. Facade families come from A1's montage, so
B11 does not start before the user has seen it.

## B12 ☐ Draw the near model and the far box on the building stamps

**Goal.** Under Enhanced with the option on, a `cblock7` building draws its near model inside the
hand-over distance and its far box beyond it, its original mesh is not drawn, and its collision is
unchanged. With the option off, or under Faithful, the city is exactly today's.

**Evidence (confidence: lead-only).** The vegetation hand-over distances are 400 to 600 m (Near)
and 600 to 900 m (Far), by dither. Building far fades reach 700 to 900 m for towers before the
Enhanced stretch. The seam is whatever A3 decides.

**Approach.** Implement A3's decision for the hulls B11 produced. Hulls without a model keep their
original mesh, so the prototype runs with only `cblock7` dressed. A user model pack's file for a
building still wins (Decision 16). Shadows stay off (Decision 17).

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** A capture low over a `cblock7` district with the option off, near and far. The
collision overlay shows the same shapes in all three. The 8-chapter `--freecam` regression is clean
and the other chapters' mesh and node counts are unchanged. <TODO: the exact capture commands.>

**⚠ Traps.** A collider claim is settled in a live `--fly` session, not in a single-frame suite.
The existing engine check that C5 builds no added vegetation stamp must stay true; the city is not
a vegetation entry.

## B13 ☐ Per-building facade, tint and the three window modes in the shader

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
TUNE and are set in B15. <TODO: the debug switch's key or flag.>

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** A montage of one `cblock7` district in each mode, near and from altitude, sent to the
user. Two launches produce identical frames in modes 1 and 3. In mode 2 a floor and a single
window are each seen to switch during one flight.

**⚠ Traps.** Mode 2 needs a clock in the shader. A pinned test frame must freeze it, or the golden
hash moves between runs. Whichever mode loses is removed with its switch, not left behind an
option.

## B14 ☐ Measure the prototype against the budget, PC and Deck

**Goal.** A recorded verdict on whether the prototype holds Decision 8, with the numbers.

**Evidence (confidence: direction-sound).** The budget is the vegetation plan's: at most 2.0 ms GPU
and 0.5 ms CPU added at fixed views, where the vegetation work measured at most 0.75 ms. On the
Deck the 3D vegetation models cost 1.9 to 2.1 ms on C3.

**Approach.** Run A2's instrument on the C5 views with the option off, near and far, on the PC and
on the Deck at one pane. Compare draw calls at the altitude view against the baseline. Scale the
`cblock7` result to the full city by hull count from A1 and state the estimate as an estimate.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** Added GPU at most 2.0 ms and added CPU at most 0.5 ms at every PC view; distant draw
calls not above the baseline. The Deck numbers are recorded and feed C24's default.

**⚠ Traps.** Only `cblock7` is dressed in the prototype, so a view over another district measures
nothing. Measure a cause both ways before asserting it.

## B15 ☐ The user's verdict on the prototype

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

## C21 ☐ Generate the remaining hulls

**Goal.** Every C5 building hull from A1's census has its near models and its far box, and no
building in the Enhanced city draws an original mesh.

**Evidence (confidence: lead-only).** The hull count is A1's. The facade families, variants per
hull and tint spread are B15's.

**Approach.** Extend B11's per-hull specs to the remaining hulls, district by district, starting
with `cblock1` (the second largest area). The tower hulls up to 108 m are where the original's own
setbacks and tops must be measured and kept. <TODO: the per-district order after `cblock1`.>

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** Per hull, the bounding-box check from B11. A log line or check that counts building
stamps still drawing an original mesh under Enhanced reads zero on C5. A montage per district for
the user.

**⚠ Traps.** The `hotelsign0/1/2` blocks are placed by no template; they need no model.

## C22 ☐ Relabel the 3D Vegetation option to cover scenery

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
goldens that move, and add one frame low over `cblock7`.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The complete `.\scripts\RunTests.ps1`. The new check is seen to fail with the option
off.

**⚠ Traps.** A golden that flips under external GPU load is a determinism defect, not a tolerance
to widen. If the changing window mode was chosen, its clock is frozen in every pinned frame.

## C24 ☐ Closing measurement, the Deck default, and follow-up issues

**Goal.** The full city is measured against Decision 8, the option's default per GPU class is set
from the Deck numbers, and anything the user's eyes asked for outside this plan's scope is filed.

**Evidence (confidence: direction-sound).** As B14, now with every hull dressed.

**Approach.** Rerun A2's instrument on all C5 views, PC and Deck. Set the Deck's one-pane default
from the result; at two or more panes the existing fallback already shows the original city.
File follow-up issues for the ground paint, the lamps or the layout pattern only where B15 or a
later look asked for them.

**Model recommendation.** <TODO: not settled in the planning session.>

**Verify.** The budget numbers hold at every view, and distant draw calls are not above A2's
baseline. The 8-chapter `--freecam` regression is clean.

**⚠ Traps.** The Deck's splitscreen measurement is bound by draw submission; a triangle count that
looks fine says nothing about it.
