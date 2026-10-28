# Enhanced vegetation

**ACTIVE PLAN** (written 2026-10-19). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan extends the original's clutter system in Enhanced mode, as PLAN-enhanced-ocean extended
its water: a Vegetation Quality setting whose Rich value breaks up the template's repeated layout,
gives forests mass past the card fade, dresses ground the original leaves bare, and draws 3D trees
and ground cover up close. Each of those four is one wave, prototyped and judged at the controls
before it ships. The scope came from a grilling in this session; its calls are the Decisions table,
which is the authority where the prose disagrees. The plan draws no `backlog` issues. #57 (CM23
co-op host perf) is out of reach: CM23 is C5, which has no vegetation entry; #142 and #205 (Enhanced C5
city look) touch clutter but are city bugs and stay separate.

The plan assumes the decode in [`org/clutter.md`](org/clutter.md) and leaves it untouched: the
faithful stamp, its seed and its streams are the Original setting, and nothing here changes them.

## Milestone goal

- A **Vegetation Quality** setting, Original or Rich, under Enhanced only, switching live.
- A per-chapter **vegetation sidecar** naming the vegetation kinds and their tuned values, edited in
  a **vegetation lab** in `--freecam` with a paint tool (texture picks and a brush mask).
- Under Rich: per-stamp variation and better card shading (Wave A), forest mass past the card
  fade (Wave B), dressing for ground the original leaves bare (Wave C), and 3D look-alike trees and
  ground cover up close (Wave D), shipped for C1, C1B, C2, C3 and C4.

**The Original setting and Original mode are untouched, bit for bit.** Every remake addition reads
the sidecar under Rich and nothing else; an Original golden that moves is a defect in this plan.

## Decisions (2026-10-19)

| # | Question | Decision |
|---|---|---|
| 1 | What does the first wave solve? | **All four goals, one wave each**, every wave prototyped before it ships |
| 2 | Wave order | **Pattern, mass, density, near**. Pattern is cheapest and lays down the per-stamp data the later waves read; near may need new art |
| 3 | Gating | **Enhanced + a Vegetation Quality word setting (Original / Rich)**, live switch like Water Quality; Rich on desktop, Original on the Deck and Linux until measured |
| 4 | Which decorations count as vegetation | **An explicit per-chapter list in a shipped sidecar JSON** (like `ocean_seas.json`). Unlisted kinds keep the faithful stamp, so city blocks, cars and lamp posts are never touched |
| 5 | Undressed green ground (C3's jungle, C1's fields) and close-up grass | **In scope** |
| 6 | Art source | **The chapter's own cards first for new ground; new art for grass and crops; procedural ground cover explored beside it.** 3D trees and bushes are Blender models |
| 7 | Where new ground and grass go | **New ground rides the density wave, grass the near wave** |
| 8 | Tuning | **A vegetation lab in `--freecam`** with live values saved to the sidecar |
| 9 | The lab's paint tool | **Both texture picks and a brush mask.** A pick assigns a ground texture a vegetation set; the mask is world-space and only gates and scales density, so placement stays in texture space |
| 10 | The mask's reach | **Every listed vegetation kind, the original's stamps included**, so a strip can be cleared or an edge thickened. Original ignores the mask |
| 11 | C3's jungle (a painted canopy seen from above) | **A canopy volume over the painted canopy, with palms on its transition textures**, never cards stood on the painted treetops |
| 12 | Mass technique | **Prototype canopy shells against impostor clumps on C1 and pick at the controls** |
| 13 | C1's fields and grassland in the density wave | **Sparse original cards** (bushes, the odd fir; hedgerows by mask). Crops and grass wait for the near wave |
| 14 | Chapters shipped | **Every chapter with ground**: C1, C1B, C2, C3, C4. C1 and C3 are the prototypes. C1C and C2B have no ground; C5 is city only |
| 15 | Collision of 3D trees | **Not solid**, like the cards, so the setting is visual only and co-op peers on different settings play the same game |
| 16 | 3D trees vs cards | **A Blender look-alike per card species within a near radius, dithering over to the original card beyond it**, on the same stamps |
| 17 | Who models | **Agents, with scripted Blender run headless**, exporting glTF; you judge renders beside the card |
| 18 | Budget | **Measured per wave** with `--perf` at a fixed worst-case view per chapter; the ceiling is set from Wave A's measurement; a co-op host adds network and simulation cost, which vegetation does not touch, so the per-chapter view is the co-op measure too; the Deck stays Original until a Deck item measures it |
| 19 | Card shader upgrades (sway, sun-side shading, contact shadow) | **Wave A**, beside jitter and tint |

Defaults set without a question, open to change: the variation comes from a fixed seed (identical on
every launch and machine); the brush mask is one image per chapter beside the sidecar; the Blender
scripts and exported models live in the repo; Enhanced goldens are re-pinned at the end of each wave.

## What the data actually ships

From [`analysis/veg-ground-survey/FINDINGS.md`](../analysis/veg-ground-survey/FINDINGS.md)
(measured areas and inventory; the green verdicts are read from names and images, not judged):

| Chapter | Templated ground | Undressed vegetation-bearing ground |
|---|---|---|
| C1 | `terpat02` 84.9 km² (firs), `river1/2` (bushes) | `terpat03` grassland 10.7, `crop04` 6.8 and `crop05` 4.2 fields, `terpat03b` 2.5, `grass1` 0.7, transitions ~2 |
| C1B | `vegrocktop` 0.13 | grey-green rock only (`rocktop` 0.49, `rocktop128` 0.22) |
| C2 | 17 templates (scrub, suburbs, `parkfront` palms) | two sports fields, 0.05 each |
| C3 | `cliff1_sandtrans` 0.93 (palm1) | **~26 km² of jungle canopy**: `forest1` 10.5, `forest1-128` 8.1, `cliff1` 3.4, `cliff1_watertrans2` 2.9, `cliff1_toptrans` 2.5, smaller transitions |
| C4 | `terpat01` 109.7, `terpat01-128` 8.0, `river5` | dry grass and rock transitions ~8 (`cliff01-trans1` 3.9, `cliff01-trans3` 3.3) |
| C1C, C2B | none | no ground |
| C5 | city blocks, all templated | none |

**No grass, reed or crop card exists anywhere in the install**; the smallest plant cards are C2's
`brush1/2` (~2.4 × 2.1 m). Every sprite card is CylindricalY. C3's `palmtree2`/`palmtree3` and C4's
`firtree2` already draw, as substitution targets of `palmtree1` (4:4:4) and `firtree1` (9:5); C4
also places `spruce.flt`. The original's own variation keys (`translate_uv_range`,
`rotation_range`, `align_normal`, the slope pair) are decoded and authored by no chapter
([`org/clutter.md`](org/clutter.md), "The kind block").

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

### Wave A, pattern

1. ☑ Vegetation Quality setting (Original / Rich) under Enhanced, live switch
2. ☑ The vegetation sidecar: per-chapter kind list and values, read under Rich
3. ☑ Per-stamp jitter, rotation and tint for listed kinds
4. ☑ Card shading: wind sway, sun-side shading, contact shadow
5. ☑ Vegetation lab in `--freecam`: live values saved to the sidecar
6. ☑ The lab's paint tool: texture picks and a brush mask
7. ☑ Wave A budget: the perf baseline and the ceiling; goldens re-pinned

### Wave B, mass

11. ☑ Canopy mask bake: from stamp density (C1) and from texture picks (C3)
12. ❌ Prototype: canopy shells on C1 (not picked)
13. ☑ Prototype: impostor clumps on C1 (picked)
14. ☑ Pick the mass technique at the controls and productionise it
15. ☑ C3's canopy volume over its painted jungle
16. ☑ Clump tiles beside the vegetation models, replaceable by a model pack
17. ☑ Wave B budget and goldens

### Wave C, density

21. ☑ Remake-added stamps on picked textures, in texture space, under the mask
22. ☑ C1's grassland and fields: sparse original cards, hedgerows by mask
23. ☑ C1B, C2 and C4 tuned in the lab
24. ☑ Wave C budget and goldens

### Wave D, near

31. ◐ Scripted Blender pipeline: one generator per species, headless glTF export, renders beside the card
32. ☑ Near-radius 3D draw with a dither handover to the card
33. ☑ Look-alike models for every listed card species
34. ☐ Ground cover prototype: new-art grass and crop cards
35. ☐ Ground cover prototype: procedural shells; pick at the controls
36. ☐ Deck and Linux: measure Rich and set their default
37. ☐ Wave D budget and goldens

## Dependency and parallelism notes

A1 and A2 block everything: every later item reads the setting and the sidecar. A3 and A4 both edit
`ClutterBuilder`'s stamp and shader (`CSVM/src/Mech3/Clutter.cs`), so they run in sequence, not in
parallel worktrees. A5 → A6 is a chain, and A6 blocks B11 (C3's mask comes from texture picks) and
C21 (picks and mask drive added stamps). B12 and B13 can run in parallel worktrees once B11 lands if
each owns its own new module and neither edits `Clutter.cs`. B14 needs both. B16's first shipped
tile needs D31's pipeline. C21 blocks C22 and C23.
D31 blocks D33; D32 can start beside D31 against a placeholder mesh. D34 and D35 run in parallel if
each owns its own module. D36 waits for every look item. Each wave's budget item closes that wave.

---

# Wave A, pattern

## A1 ☑ Vegetation Quality setting (Original / Rich) under Enhanced, live switch

**Goal.** A Vegetation Quality word setting in the video options, Original or Rich, that only acts
under Enhanced and switches live without a relaunch. Rich is the desktop default; the Deck, Linux
and an integrated GPU default to Original (Water Quality's rule; the integrated-GPU half is your
ruling, beyond Decision 3's Deck and Linux).

**Evidence (confidence: traced-to-code).** The pattern to copy is Water Quality from PLAN-enhanced-ocean
(`git log --grep=PLAN-enhanced-ocean`); the live clutter path already exists:
`EnhancedLook.FollowClutterFade` (`CSVM/src/Launch/EnhancedLook.cs`) and
`ClutterBuilder.Recut` (`CSVM/src/Mech3/Clutter.cs`). Word settings register once and every
settings site walks the registry (`git log --grep="#162"`). Water Quality is declared in
`CSVM/src/Utils/WaterQualitySetting.cs` and registered in `Utils/WordSettings.cs`'s `All`. Its
fallback is `FallbackFor(OperatingSystem.IsLinux(), ShadowQualitySetting.IntegratedGpu)`: the low
word on Linux (which covers the Deck) or an integrated GPU, the high word otherwise, and the high
word under `--det`. The options store, the flag parser, the Options pages, the launch resolve and
the live apply all walk the registry, so a new setting is its own file, one registry line, one
authored VIDEO row and its build added to `EnhancedLook.FollowWordSettings`.

**Approach.** `Utils/VegetationQualitySetting.cs` copies Water Quality: words `original`/`rich`,
`--vegetation-quality=`, the saved `vegetationQuality` word, the `graphics.vegetationQuality` key,
then the same machine fallback (Original on Linux or an integrated GPU, Rich otherwise and under
`--det`). It is Enhanced-only on the Options rows and sits on the Original VIDEO page as a
"Vegetation" dropdown under View Distance in the scrolled window. The clutter reads
`VegetationQualitySetting.RichOn` (the word is `rich` and the mode is Enhanced), never the word,
so Original mode stays Original whatever is saved. A live change runs
`EnhancedLook.ApplyVegetationQuality`, from `FollowWordSettings`, which calls
`GameSession.FollowVegetationQuality`: `ClutterBuilder.Recut` plus the map edge's
`FollowClutterFade`. A mode switch already recuts both. At this item nothing reads `RichOn` past
that hook, so the setting changes no pixel.

*Recut, not rebuild.* A3's variation can switch live through a recut if A3 keeps the stamps
index-aligned between Original and Rich. Three consumers bind to a kind's placement indices when
the session loads: `ClutterActivation` (stamp to world node), `ClutterCull` (a crater's flattened
stamps, kept in `ClutterInstances._moved`) and `MapEdgeExtender` (the copies past the map edge,
built from `ExportedKinds`). A rebuild of the clutter root would have to rebind all three and
carry over every stamp the world hid or a crater flattened, or lose them. So A3 bakes, per listed
kind, a Rich placement and custom-data list beside the Original one, one entry per Original stamp:
a jitter that would leave the source triangle is clamped back inside, never dropped, and no stamp
is added. `ClutterInstances` keeps both in its prototype, and `Recut` takes the variant `RichOn`
asks for. Today `Recut` returns early when the layout (whole or cells) stands, so the variant must
join that test. The edge copies take the same variant in `FollowClutterFade`. A `_moved` stamp is
zero-scaled, so it stays hidden under either variant. C21's added stamps have no Original index,
so they belong in their own exports, shown or hidden by the same recut rather than mixed into an
original kind's buffer.

**Model recommendation.** A mid-tier model suffices: the item is a copy of a registered pattern,
and its one judgement (recut against rebuild) is recorded above.

**Verify.** Units: `VegetationQualitySettingTests` (fallback per platform and GPU, `--det`, the
precedence ladder, flag and options-file round trip),
`GraphicsModeTests.RichVegetationNeedsEnhancedAndTheRichWord`, and the VIDEO page's row, walk and
scroll tests in `OriginalOptionsTests` and `OptionsChoicesTests`. Engine:
`graphics-vegetation-quality` on a C1 Enhanced flight, where `RichOn` follows a live
original/rich move and a live mode switch both ways, and every live move leaves the clutter's
sprite count and cells as built; `menu-original-tracer-core` walks the new row. Goldens: none may
move. Under `--det` the word resolves to `rich`, so every Enhanced golden runs at Rich, which
draws nothing different yet.

**⚠ Traps.**
- Original must stay bit-identical: an Original golden that moves is a defect.
- Read `RichOn`, never `Word`: the saved word survives a switch to Original.
- `--det` resolves to Rich, so from A3 on the Enhanced goldens move by design and are re-pinned
  at A7; an Original golden moving is still a defect.
- The VIDEO page's scrolled window grew a row (ten dropdowns over seven lines), which moves every
  test that counts the window's scroll positions or walks past View Distance.

**Verified.** <pending orchestrator run>

## A2 ☑ The vegetation sidecar: per-chapter kind list and values, read under Rich

**Goal.** A shipped per-chapter JSON that names the vegetation kinds and carries their tuned values;
under Rich the clutter reads it, under Original nothing reads it.

**Evidence (confidence: traced-to-code for the keys and the kind lists).** Decision 4.
`ocean_seas.json` is the precedent (`git log --grep=E41`). Kinds are keyed by model name
(`Kind.Model`, `Clutter.cs:1741`), which is what the original's own `templates.zrd` keys on
([`org/clutter.md`](org/clutter.md)), with the `.flt` suffix. The lists are read from each chapter's
`extracted/<C>/zrdr/templates.zrd.json`, and every listed model was checked in the chapter's gamez
to be a one-polygon Facade card, CylindricalY:

| Chapter | Listed kinds | Left out |
|---|---|---|
| C1 | `firtree1`, `firtree2`, `dougfirtree1`, `bush1`, `bush2` | none |
| C1B | `bush1`, `bush2`, `dougfirtree1` | none |
| C2 | `palmtree1`, `palmtree2`, `palmtree3`, `spruce`, `brush1`, `brush2`, `brush3`, `brush4` | `filmbuild01`..`09`, `resbuild01`..`27`, `c_studebaker1`..`6` (solid, `Default` model type) |
| C3 | `palmtree1`, `palmtree2`, `palmtree3` | none |
| C4 | `firtree1`, `firtree2`, `dougfirtree1`, `spruce` | none |

C3's `palmtree2` and `palmtree3` are not unplaced: `palmtree1` rolls to each a third of the time
([`formats/templates.md`](formats/templates.md), "The extra blocks are substitution targets"), so
they already draw today. C4's `spruce` has a block and is placed directly, though the survey's card
sizes leave it out. Every substitution target of a listed source is itself listed.

**Approach.** `CSVM/data/vegetation.json`, read by `CSVM/src/Mech3/VegetationSidecar.cs` and
shipped in the pck by the existing `data/*.json` export filter. The shape:

```json
{
  "_about": "prose",
  "C1": {
    "seed": 0,
    "mask": "vegetation/C1.bin",
    "kinds": {
      "firtree1.flt": { "jitter_uv": 0.05, "yaw_deg": 0, "tint": 0.05, "brightness": 0.1 },
      "bush1.flt": {}
    },
    "sets": { "hedge": ["bush1.flt", "bush2.flt"] },
    "picks": { "terpat03": "hedge" }
  }
}
```

- The chapter keys are C1, C1B, C2, C3 and C4; any other is a warning and ignored, so C5's blocks
  cannot be listed.
- `kinds` is the list Decision 4 asks for. A model absent from it is not vegetation and keeps the
  faithful stamp. Each value is a symmetric per-stamp spread, 0 when absent: `jitter_uv` in texture
  repeats per UV axis (0 to 0.5), `yaw_deg` (0 to 180), `tint` as a per-channel colour multiplier
  spread (0 to 0.5), `brightness` as an all-channel one (0 to 0.5). A load clamps into those ranges
  and warns.
- `seed` is an integer salt for the Rich variation's fixed stream (the plan's fixed-seed default:
  identical on every launch and machine); the lab can change it to reroll one chapter.
- `mask` (A6) is the brush mask's path relative to `CSVM/data`, empty or absent for none.
- `sets` and `picks` (A6, C21) are empty for now. A set is a named list of listed kinds, and a member
  that is not listed is dropped with a warning, so a pick can never stamp a city block or a car. A
  pick maps a ground texture name to a set; a pick naming no set is dropped.
- The writer (`WithEntry`, `Save`) replaces one chapter's entry in place, writes only nonzero values,
  keeps every other key and its order, skips comments as the read does, refuses a file it cannot
  parse, and writes through `AtomicFile` (BOM-less UTF-8).

The shipped values are modest starting points (`jitter_uv` 0.05, `tint` 0.05, `brightness` 0.1,
`yaw_deg` 0) for the user to tune in A5. ⚠ A CylindricalY card's shader replaces the instance basis
(`Clutter.cs:869`), so a yaw on a card is invisible: it reaches the eye only through D32's 3D models,
or through a horizontal mirror if A3 maps the yaw's sign onto one.

**Wiring (landed with A3, see A3's Approach).** `WorldSession.Build` loads `VegetationSidecar.Load().For(o.Chapter)`
beside `ClutterTemplateSpec.Load` in every session and passes it to a new optional `ClutterBuilder`
parameter. The Rich placements are baked at build beside the Original ones whatever the word, since
a live switch to Rich recuts rather than rebuilds (A1's Approach); the Original draw never reads
them, and the Rich stream is separate, so Original stays bit-identical. At the end of
`ResolveProperties`, after minting, each kind in `_allKinds` takes `entry.Find(kind.Model)`, null
for an unlisted kind. `PlaceOnTriangle` applies the variation of the kind the stamp lands in
(`target`), drawn from a third `Random` seeded from `PlacementSeed`, a fixed Rich constant and
`entry.Seed`, after the substitute, scale and fade draws, and for every kept listed stamp before any
mask drop, so a mask edit does not reshuffle the other stamps.

**Model recommendation.** Sonnet: a reader and writer on the `OceanSeas` pattern plus a data list
read off the extracted files; no judgement call beyond the schema.

**Verify.** `VegetationSidecarTests`: the shipped file reads clean and has every chapter with
ground; every listed model is in its chapter's `templates.zrd` (an extracted-data fact); no C2
building or car is listed; a mistake is one warning; a clamp is reported; sets keep only listed
kinds and picks need a set; Save round-trips, leaves the shipped file byte-identical when saving
what was loaded, and refuses a file it cannot read. The Original check (every golden unchanged) is
A3's, since nothing reads the file until the wiring lands.

**⚠ Traps.** Key by model name, not by mesh index: C5 ships one building as several meshes, and a
substitution target resolves to one model however many templates mention it (`Clutter.cs:1001`).

## A3 ☑ Per-stamp jitter, rotation and tint for listed kinds

**Goal.** Under Rich, each listed stamp moves by a small offset, turns and takes a slight tint and
brightness, so the template's layout no longer repeats visibly from altitude.

**Evidence (confidence: traced for the placement path, lead-only for the look).** Every texture
repeat stamps the same layout because the original authors no jitter or rotation
([`org/clutter.md`](org/clutter.md), "The kind block"; stamp loop `Clutter.cs:556`). The fade draw
already runs on its own stream so a fade A/B leaves species and scale alone (`Clutter.cs:634`).

**Approach.** Draw the Rich variation from a stream of its own on a fixed seed, after the point is
kept and after the substitute/scale/fade draws, so Original's streams are untouched. Jitter in UV
units, so it follows the painted ground. Carry the tint in per-instance data.

The lattice walk records every kept card stamp (its kind and index, triangle, texture coordinate,
scale and mesh lift) when the chapter has a vegetation entry, and `ClutterBuilder.BakeRich` replays
them in walk order once the walk ends. A stamp whose landing kind (`target`) is listed draws
`VegetationVariation.DrawsPerStamp` values from a `Random` seeded from `PlacementSeed`, a fixed Rich
label and the entry's `seed`, whatever its values: two jitters, the mirror, brightness, three tint
channels, and A6's mask keep draw, reserved and unread. The result is a Rich transform and colour
per Original stamp, index-aligned (`KindExport.Rich`); the Original transforms, fades and streams
are not touched. A jitter that leaves the source triangle is halved until it is back inside, at
worst unmoved, so no stamp is dropped or added. A solid kind is never given a variant.

- *Tint slot:* the MultiMesh instance colour. Custom data holds the fade in all four channels, and
  Godot multiplies an instance colour into the card shader's `COLOR` before the vertex stage, which
  the shader already linearises and multiplies into the albedo. The Rich cells' MultiMeshes take
  `UseColors`; the Original ones do not, so no Original buffer changes.
- *Turn:* every listed kind is a CylindricalY card whose shader replaces the instance basis, so a
  yaw is invisible. `yaw_deg` stays the field and maps to a horizontal mirror with probability
  `yaw_deg`/180, carried as a negative X axis on the Rich instance basis. A Rich twin of the card
  shader (key bit 32) reads that sign and flips the card's X; the Original shader text is
  unchanged.
- *Live switch:* `ClutterInstances` keeps the Rich variant as a layout of its own beside the
  Original cells, and `Recut` picks it from `RichOn`, so a live move swaps nodes. A hidden or
  cratered stamp carries across as the same move off the other variant's authored transform.
  `MapEdgeExtender` rebuilds only the listed kinds' copies in each live cell on a move.
- *Lab hook:* `GameSession.ApplyVegetation(entry)` (through `ClutterBuilder.ApplyVegetation` and
  `MapEdgeExtender.FollowVegetation`) re-bakes from a new entry and recuts, with no lab caller yet.

**Model recommendation.** Opus: the item is a design across the builder, the drawn layouts and the
map edge, with three bit-identity constraints to hold at once.

**Verify.** Units: `VegetationVariationTests` (the jitter stays in its triangle and is clamped,
never dropped; a fixed draw count whatever the values; a zero entry is the Original stamp; the
mirror rate and tint ranges; the seed). Engine: `clutter-rich-variation` (a C1 entry leaves every
Original transform and fade bit-identical, bakes index-aligned variants, mirrors half the cards at
a turn of 90, and retuning one kind moves no other); `graphics-vegetation-quality` (a live move to
original draws other transforms and no instance colour, on the map and past its edge, a move back
draws the first build's exactly, and a re-bake through `ApplyVegetation` from the shipped entry
does too). The 8-chapter `--freecam` regression in Faithful and in Enhanced at both words, zero
errors and equal counts. Original and Faithful goldens hold; the Enhanced goldens on chapters with
listed kinds move, re-pinned at A7. A montage from altitude over C1, Original against Rich, judged
by you.

**⚠ Traps.**
- Never add a world grid or a world-space offset (`Clutter.cs:17`, `:554`); jitter in texture space.
- Never draw from `rng` or `fadeRng`: one extra draw moves every later species and scale, and with
  them the goldens.
- A jitter can carry a stamp off its source triangle; the `InSourceTriangle` check (`Clutter.cs:616`)
  counts that, so clamp or re-test.
- The exported placements feed MapEdgeExtender's copies past the map edge (`Clutter.cs:455`), so
  the variation must be in the placements, not added in the shader, or the copies differ.
- A card's `scale_range` draw is in its instance basis, which the card shader replaces, so no card
  draws its scale today; the Rich mirror reads only the X axis's sign for that reason.

**Ruling (yours, from the montage).** The ground texture paints a dark blob under each original
tree, and a jitter of 0.05 repeats carries the cards off their blobs. The shipped entries carry no
jitter: `yaw_deg` 90 (half the cards mirrored), `tint` 0.1, `brightness` 0.2. The jitter stays a
field for the lab and for remake-added stamps, which have no painted blob. Breaking the layout's
repeat falls to Waves B and C. C2's `terpat04` and `terpat04-128` paint the same blob, dark to a
2.5 m radius and gone by 4 to 5 m at 1 m a texel, under every brush and palm, in the original and
the HD pack alike, so a jitter that breaks C2's rows (0.1 repeats, up to 25 m) leaves a row of bare
blobs; a jitter the blob still holds is under 0.005 repeats and changes nothing seen. C2 breaks its
rows by a kind's `keep` instead: the share of the original's stamps Rich draws (1 when absent), the
rest dropped on the mask's keep draw as a zero basis, so the near models and the map-edge copies drop
with them and no stream moves. C2's brushes keep 0.6, and its `scrub` and `scrub128` sets scatter
brush on `terpat04` and `terpat04-128` at the density that replaces the dropped share; the palms keep
every stamp, since `palmtree1` also stands in the residential yards. A dropped stamp leaves its blob
bare, which reads as a ground mark.

**Verified.** <pending orchestrator run>

## A4 ☑ Card shading: wind sway, sun-side shading, contact shadow

**Goal.** Under Rich, cards sway in a wind, brighten on their sun side, and sit on a soft contact
shadow.

**Evidence (confidence: traced-to-code for the sources, lead-only for the look).** The card shader
is generated in `ShaderCode` (`Clutter.cs`) with its facing in `FaceBasisLines`. Every listed card
is a one-quad CylindricalY card with its pivot at the base centre: C1's `firtree1` spans y 0 to
17.83 and x ±5.01, `bush2` y 0 to 3.35 (`extracted/C1/gamez/models.json`), so a vertex is either at
the base or at the top. The authored wind is the puffer wind (`WorldWind`, `weather.zrd`'s `WIND`
block): C1 and C1B author a 2 m/s updraft and a gust whose magnitude steps 5 m/s a frame with no
`dt`, which would jitter a tree, and no shader reads it. The Enhanced ocean runs its own fixed wind
bearing (`SeaState.WindDeg`) baked into its shader. Animated shaders read `csky_time`
(`csky_time.gdshaderinc`, `ShaderTime`), the session's sim clock, so a `--det` frame is a function
of its frame count. The sun is the `csky_sun_dir` global (toward the sun, `WeatherRig.WriteSunDirection`)
with the zone's `SUNLIGHT_DIFFUSE` in `csky_sun_light.y`, which the Enhanced puffer and fog-volume
cards already read.

**Approach.** Landed as three Rich-only line groups in the card shader, beside A3's mirror
(`ClutterBuilder.RichDecls`, `RichVertexLines`, `RichFragmentLines`). The Original card's text is
unchanged, and an unlisted kind never takes the Rich variant.

- *Wind source:* the remake's own breeze, not `WorldWind`: a fixed bearing 20° from +X toward +Z
  (the ocean's default), in the shader text. Time is `csky_time`, so the sway is deterministic under
  `--det` and freezes with a halted clock. The card leans about its base (`VERTEX.y` scales it, so
  the base stays planted): a 0.012 rad mean lean, plus a gust (12 s period, rolling across the forest
  along the bearing at about 10 m/s) of up to 0.024 rad and a per-tree flutter (3 s) of ±0.020 rad
  times the gust (doubled at your request after the first montage). A 17.8 m fir's top moves at
  most about one metre. The phase is a dot of the
  planted point with a fixed vector, no random draw. Both periods divide `ShaderTime.RolloverSecs`,
  so the clock's hourly wrap shows no jump. The lean is applied in world axes and folded back
  through the face (`transpose(face)`), so the card's existing `* keep` still collapses a hidden or
  cratered stamp.
- *Sun-side shading:* the half of the card facing the sun's bearing (its horizontal direction, so a
  high sun still gives a side) gains up to (1.6, 1.5, 1.2) in linear, warmer in red so it reads as
  lit rather than grey, times `dot(face[0], bearing)` and the
  zone's diffuse clamped to 1. The gain is full from a sixth of the card's width off its axis,
  since the foliage sits near the axis and a ramp to the quad's edge left it at a few percent. The
  shadow side keeps the Original look: nothing is darkened. A zone with no diffuse (a night or cloud
  zone authoring `SUNLIGHT_DIFFUSE` 0) gets no gain.
- *Contact shadow:* none. A card-base darkening (the lowest 18 % of the card to 0.6) was prototyped
  and removed by your ruling: on/off captures at four close poses measured a 2-3.5 sRGB-level mean
  change in a thin band inside the dark blob the ground texture already paints under each original
  tree, invisible without an amplified diff. The same captures measured the first sun gain (0.8 in
  linear) at a 3-6 level mean, reading as greyer foliage rather than a lit side, and you asked for
  it stronger. A ground decal would double the painted blob.

The constants are in the shader text, not the sidecar; the lab gains them only if the look asks for
per-chapter tuning.

**Model recommendation.** Opus: the shading is small, but holding the Original text, the live
switch's twins and the hidden-stamp collapse at once is the work.

**Verify.** Engine: `clutter-rich-variation` checks every listed kind's Rich card shader is its
Original text plus exactly the three Rich line groups (so the Original text is untouched) and that
it reads `csky_time`, `csky_sun_dir` and the side varying; no unlisted card's shader sways. The
targeted suites `clutter-*`, `graphics-vegetation-quality`, `graphics-shader-twins`,
`graphics-retext-compiles`, `graphics-live-switch`, `graphics-view-distance` and `crater-carve`
pass. Goldens: every Faithful and Original shot holds; the Enhanced shots on listed chapters that
A3 moved stay moved, and no other shot moves. The look is a montage judged by you: Original, Rich
before A4 and Rich with A4 at C1 from 600 m and 300 m, a C2 pose, two C1 poses at tree height (one
looking along the sun's bearing, one across it), and a 3 s sway strip under `--det`.

**⚠ Traps.**
- Never declare an instance uniform below `csky_instance_uniforms.gdshaderinc` (`Clutter.cs:829`).
- A blended card takes `depth_prepass_alpha`, never `depth_draw_never` (`Clutter.cs:812`).
- A hidden or cratered stamp has a zero basis that the vertex stage reads (`Clutter.cs:841`); sway
  must not revive it.
- A live mode switch must find the other variant's compiled shader (ShaderTwins, `Clutter.cs:70`).
- The sun-side gain is a multiply on near-black foliage, so a gain that reads as large in the text
  moves a dark crown by a few sRGB levels; judge it at a pose across the sun's bearing, never along it.

**Verified.** <pending orchestrator run>

## A5 ☑ Vegetation lab in `--freecam`: live values saved to the sidecar

**Goal.** A lab panel in `--freecam` with live sliders for the sidecar's values that rebuilds the
clutter and saves the chapter's entry to the sidecar.

**Evidence (confidence: traced-to-code for the precedent).** Decision 8. The ocean lab
(`CSVM/src/UI/Labs/OceanLab.cs`, `git log --grep=E41`) is a `Node` built by
`InspectionLabs.BuildOceanLab` in `--freecam` alone, toggled by Shift+F1, applying an edit through a
session callback on slider release or 0.25 s after a click, and saving through `OceanSeas.Save` to
the path `SourceTreePath()` gives (null in an export, which turns Save off). Its Save keeps the file:
the writer parses with the loader's options and throws `InvalidDataException` on a file it cannot
read, which the lab shows as "save refused" and leaves the file alone (`git log --grep="review 2"`).
`VegetationSidecar.WithEntry`/`Save` already behave the same way (A2).

**Approach.** The mechanism is landed: `CSVM/src/UI/Labs/VegetationLab.cs`, the ocean lab's panel
copied, toggled by Shift+F2 (no flight or freecam control reads `F2`). It holds the chapter's seed
(a 0 to 999 slider, wider when the saved seed is larger) and one slider per `VegetationKind.Fields`
value of each listed kind, plus Revert to saved and Save. An edit is a new `VegetationEntry` from
`VegetationEntry.With(model, field, value)` (clamped; an unlisted model throws) or `WithSeed`, handed
to an `Action<VegetationEntry>` on the same release/0.25 s timing, so a drag rebuilds no clutter per
frame. `InspectionLabs.BuildVegetationLab(saved, apply)` builds it in `--freecam` on C1, C1B, C2, C3
or C4 only. The lab holds no clutter and knows nothing of Vegetation Quality.

**Wiring (lands after A1, with or after A3).** `GameSession`, beside its `BuildOceanLab` call, loads
`VegetationSidecar.Load().For(_spec.Chapter)` into a session field and calls
`_labs!.BuildVegetationLab(saved, ApplyVegetation)`. `ApplyVegetation(entry)` stores the entry as the
session's drawn one and re-bakes the Rich placement lists from it, then recuts (A1's live switch is a
recut over index-aligned Original and Rich lists, never a rebuild of the clutter root); under
Original the recut draws nothing new, so a later switch to Rich draws the edit. The same commit adds the
Shift+F2 row to `docs/controls.md` (the lab is unreachable until then, so the row waits) and a
`vegetation-lab` engine suite on the `ocean-lab` pattern: one lab in an Enhanced Rich `--freecam` C1
session and none in `--fly`; `Set` and `SetSeed` reach the rebuilt clutter; `RevertToSaved` restores
the opening placements.

**Model recommendation.** Sonnet for the mechanism (a copy of the ocean lab on a landed reader and
writer); the wiring wants Opus, since the rebuild touches `Clutter.cs`'s live path and the goldens.

**Verify.** `VegetationSidecarTests.ALabEditChangesOneValueAndSavesAsEdited`: a slider edit moves one
value of one kind, clamped, and leaves the rest of the entry; the seed moves alone; the entry Save
writes reads back as the edit with every other chapter as shipped. The existing Save tests cover the
file kept on a refused Save. The panel itself is checked by the wiring's `vegetation-lab` suite and a
look at the controls.

**⚠ Traps.**
- Save through `AtomicFile` with explicit UTF-8, as every repo writer does; `VegetationSidecar.Save`
  does, and refuses a file it cannot parse rather than replacing it with one chapter.
- Save writes the whole entry, so `sets`, `picks` and `mask` must ride along in the edited entry:
  `With`/`WithSeed` copy them, and A6's paint edits must go through the same entry.
- A yaw on a card is invisible (A2's note): the `yaw_deg` sliders move nothing on screen until D32 or
  a mirror in A3.
- The ocean lab sits on the same right edge; both open at once overlap. Open one at a time.

**Wiring landed.** `GameSession` builds the lab from the clutter's entry with `ApplyVegetation` (a Rich
re-bake and recut, never a rebuild) and the Shift+F2 row is in `docs/controls.md`; the `vegetation-lab`
engine suite verifies it (one lab in `--freecam`, none in `--fly`, a tint edit re-bakes one kind alone,
a seed edit rerolls, a revert draws the first build exactly, Save goes to a temp copy).

**Verified.** <pending orchestrator run>

## A6 ☑ The lab's paint tool: texture picks and a brush mask

**Goal.** In the lab you click a ground texture to give it a vegetation set, and brush a world-space
mask that thins, thickens or clears listed vegetation, the original's stamps included.

**Evidence (confidence: traced-to-code for the bounds, the export and the pick's walk; lead-only for
the look).** Decisions 9 and 10. Each vegetation chapter's gamez has one World node, `world1`, whose
`area` is -12288 to 0 on both axes on C1, C1B, C2 and C4 and -16384 to 0 on C3
(`extracted/<C>/gamez/nodes.json`); `MapEdgeExtender` and `WorldBuilder` read the same rectangle as
Godot X and Z. The export's `data/*.bin` filter already ships `data/cloud_puffs/*.png.bin` from a
subfolder, and Godot imports no `.bin`. No existing tool resolves a ground texture under a ray: the
weapon lab rays the world colliders (`CollisionLayers.World`) for a point only, and the colliders
are separate meshes from the textured ground, so the texture comes from walking the gamez polygons.

**Approach.** The mechanism is landed as two modules and the lab's paint tool.

- `Mech3/VegetationMask.cs`: the mask is raw bytes, a square of 1024 × 1024 for a new mask (about
  12 m a texel on a 12 km chapter, 16 m on C3; any square file loads), at
  `CSVM/data/vegetation/<chapter>.bin` (`PathFor`, the entry's `mask` value). Raw rather than PNG,
  since a unit test can read and write it with no engine and the `data/*.bin` filter packs it
  unimported, so an exported build ships it with no export change. The square covers `world1`'s
  `area`, row 0 along the lowest Z and column 0 along the lowest X, sampled bilinearly between
  texel centres and neutral outside the area. A byte over 128 is the density factor: 128 neutral,
  0 bare, 255 nearly twice. A brush dab (`Stroke`) thickens or thins toward 255 or 0 by a strength
  times a (1 − d²/r²)² falloff, or clears or restores neutral inside its radius. Save goes through
  a new `AtomicFile.WriteAllBytes`.
- **The density rule** (`VegetationMask.Keeps(density, draw, added)`): a stamp is kept when its
  Rich draw is below the density, times one half for a stamp C21 adds. An original stamp is whole
  at neutral or above and only thins, so a thickened mask changes nothing for it; C21 generates its
  candidates at twice its intended density, a neutral mask shows half, and a thickened one shows up
  to all. Thickening therefore never places.
- `Mech3/VegetationGround.cs`: a pick reads the textured world polygons under the hit, walked as
  `PlaceOnWorld` walks them, and names the first unflagged layer among those coplanar with the
  nearest (within half a metre). On a flagged overlay with no base beneath, it names nothing.
- **A pick** is `VegetationEntry.WithPick(texture, models)`: the checked kinds, reduced to listed
  ones, reuse a set with exactly those members (a hand-written `hedge` survives) or add one named by
  joining them with `+`; no listed kind removes the pick. `WithMask` sets the path.
- **The lab**: a PAINT section with a tool choice (Off, Pick, Brush), a checkbox per listed kind for
  a pick, the chapter's picks, and the brush mode with radius (10 to 2000 m) and strength sliders. A
  left click off the panel rays the world's own triangles (`VegetationGround.RayHit`), and a ring of
  the brush radius follows the cursor on the ground while the brush is chosen. A pick applies at
  once through the A5 callback. A brush paints while held and hands the mask to its own callback on
  release, so a stroke rebakes nothing per frame; the first dab names `vegetation/<chapter>.bin` in
  the entry. Save writes the mask, then the entry; Revert restores both.

**Wiring (lands with A3 and A5's wiring; the contract).**
- `WorldSession.Build` loads `VegetationMask.Load(entry.Mask, world1.AreaLeft, AreaTop, AreaRight,
  AreaBottom)` beside the sidecar and hands it to `ClutterBuilder` with the entry.
- In `PlaceOnTriangle`, a stamp whose `target` is listed takes its Rich variation draws, a fixed
  count per stamp, then **always one more draw** from the same Rich stream, the keep draw, whatever
  the mask holds. The mask is sampled at the stamp's Rich world position (after the jitter) and
  `Keeps(density, draw, added: false)` decides. The draw count per stamp never depends on the mask, so a mask
  edit moves no other stamp. A dropped stamp stays in the Rich list at its index with a zero basis,
  as a cratered stamp does, so `ClutterActivation`, `ClutterCull` and the map edge's copies bind
  unchanged; the copies past the edge inherit their source's drop and never sample the mask.
- An unlisted kind takes no Rich draw and no sample (Decision 10). Original draws its own list and
  never reads the mask. C21's added stamps call `Keeps(…, added: true)` with one draw each.
- `GameSession` passes `BuildVegetationLab(saved, ApplyVegetation, mask, ApplyVegetationMask,
  p => VegetationGround.LayersAt(gamez, p), (from, dir) => VegetationGround.RayHit(triangles, from, dir))`,
  the triangles built once on first use; `ApplyVegetationMask(mask)` stores the mask as the
  session's drawn one, re-bakes the Rich lists as `ApplyVegetation` does, and recuts.
- Picks feed nothing until C21 reads `entry.SetFor(texture)` per polygon, behind the `no_clutter`
  gate as the faithful stamp is.

**Model recommendation.** The mechanism (pure modules and a lab panel) suits a mid-tier model. The
wiring wants Opus: it rides A3's Rich bake in `Clutter.cs`, where one extra or missing draw moves
the goldens.

**Verify.** Units: `VegetationMaskTests` (neutral in and out of the area, the corner mapping, Clear
and Neutral, the falloff, the atomic round trip, a non-square file refused, the density rule),
`VegetationGroundTests` (layers nearest first, a flagged overlay gives the pick to its base, a
flagged layer alone gives none) and
`VegetationSidecarTests.APickGivesListedKindsAndSavesWithTheMask`. The wiring extends the
`vegetation-lab` suite: a `Clear` stroke over a C1 forest point zero-scales the listed Rich stamps
inside its radius and no other, leaves the Original list and every unlisted kind as built, and
`RevertToSaved` restores them; a `Pick` on `terpat03` lands in the entry; and a stroke followed by
`Neutral` gives back the opening Rich placements bit for bit. The first committed mask is checked in
an export (`ExportRelease.ps1`, the pck lists `data/vegetation/<chapter>.bin`). The look (a cleared
strip, a thinned edge) is a montage judged by you.

**⚠ Traps.**
- The mask gates and scales; it never places. Placement stays per texture repeat (no world grid).
- The mask acts only on listed kinds: a C5 block or a C2 car must be unreachable by the brush. C5
  has no entry and builds no lab, and an unlisted kind is never sampled.
- `no_clutter` selects which coplanar layer decorates, it does not mean bare ground
  ([`org/clutter.md`](org/clutter.md)); a pick must not be read as overriding it. The pick names the
  decorating layer's texture, and an added stamp still obeys the flag per polygon.
- Draw the keep draw for every listed stamp whatever the mask holds, and drop by a zero basis, never
  by removal: either mistake reshuffles or misaligns every later stamp.
- The ground ray can stop on a solid decoration (a C2 building roof); the brush paints at that XZ
  and a pick reads the ground beneath, which is what is wanted.
- The lab must never depend on the world colliders: `--freecam` builds none, so a physics ray finds
  nothing under the cursor there.

**Wiring landed.** `WorldSession` loads the mask beside the sidecar, `ClutterBuilder.BakeRich` (where
A3 put the Rich draws, not `PlaceOnTriangle`) reads the reserved keep draw against it, a dropped stamp
keeps its index with a zero basis that the activation, the crater cull and the edge copies carry, and
`ApplyVegetationMask` re-bakes and recuts; the `vegetation-lab` suite verifies it (a Clear stroke drops
the listed stamps inside its radius alone under Rich and none under Original, a dropped stamp stays
hidden through a node hide and show and a crater across a live move, a cleared map hides every edge
copy, a Neutral stroke and a revert give back the first build, a pick lands in the entry and moves no
stamp).

**Verified.** <pending orchestrator run>

## A7 ☑ Wave A budget: the perf baseline and the ceiling; goldens re-pinned

**Goal.** Wave A's cost measured, a ceiling for the plan set from it, and the Enhanced goldens Wave
A moved re-pinned.

**Evidence (confidence: lead-only).** Decision 18. No co-op host measurement: the sidecar lists
C1, C1B, C2, C3 and C4, and a chapter with no entry builds no Rich variant and compiles no Rich
shader, so C5's CM23 (#57) draws as before.

**Approach.** The instrument is [`analysis/veg-budget/measure.ps1`](../analysis/veg-budget/measure.ps1),
which every later budget item (B17, C24, D37) reruns unchanged. Each chapter has one `--freecam`
view, 80 m up and 300 m off on both axes from its densest listed vegetation (the cell with the most
card stamps), looking at it:

| Chapter | View | In frame |
|---|---|---|
| C1 | `--pos=-8975,271,-5980 --lookat=-9275,191,-6280` | a hill of `terpat02` firs to the skyline |
| C1B | `--pos=-7288,285,-5486 --lookat=-7588,205,-5786` | the bushes and firs on the rock top, night sea |
| C2 | `--pos=59,88,-2955 --lookat=-241,8,-3255` | palms and brush over desert scrub between two suburbs |
| C3 | `--pos=-5439,80,-3077 --lookat=-5739,0,-3377` | the palm transition along a beach under the painted jungle hills |
| C4 | `--pos=-8957,827,-4918 --lookat=-9257,747,-5218` | firs on both sides of the railway, the snow peak behind |

C3's cards are only the 707 palms on its transitions; the view keeps the painted jungle in frame for
Wave B's canopy. Every launch is `--det` (no saved option reaches it, and the sway's clock is the
sim clock) with `--perf --no-vsync --mute`, 600 sim frames, through `RunProbe.ps1` on the hidden
desktop at `-Resolution 5120x1440` (the author's display; the client area is 5120x1421), where
`--det`'s Enhanced profile is TAA, Ultra shadows, far view distance and render scale 100 %. A case
is Faithful (`--graphics=original`), Enhanced Original or Enhanced Rich, and a repeat runs every
chapter's three cases back to back, so drift lands on all three (PERF-9, PERF-48). The first two
windows (the first draw's shader compiles) are dropped; a launch reports the median of its other
eight windows, a case the median and range of its launches.

The hidden desktop presents at the display's 120 Hz even with `max_fps=0`, so `frame_ms` sits at
8.33 ms in every case and is a floor, not a cost (PERF-2); the cost is read off `gpu_ms` (the GPU's
own timing of the root viewport's render, which the present pacing does not enter),
`render_cpu_ms`, `proc_ms` and the counts. At that pacing the GPU idles for part of each frame and
may run below its boost clock; both words are measured under the same pacing, so the difference
between them holds.

**Baseline (Wave A).** Four launches a case on one PC (RTX 5080 on Vulkan, 120 Hz display), idle,
gaming mode off. Median (range of the launch medians):

| Chapter | Case | `gpu_ms` | `render_cpu_ms` | `proc_ms` | draws | prims |
|---|---|---:|---:|---:|---:|---:|
| C1 | Faithful | 0.71 (0.70-0.72) | 0.30 (0.29-0.30) | 1.24 (1.21-1.27) | 240 | 88,596 |
| C1 | Enhanced Original | 5.87 (5.85-5.87) | 0.45 (0.44-0.45) | 1.23 (1.23-1.25) | 421 | 322,531 |
| C1 | Enhanced Rich | 5.87 (5.85-5.88) | 0.45 (0.43-0.46) | 1.24 (1.22-1.24) | 421 | 322,531 |
| C1B | Faithful | 0.65 (0.64-0.66) | 0.34 (0.34-0.35) | 0.19 (0.18-0.19) | 211 | 2,452 |
| C1B | Enhanced Original | 3.51 (3.50-3.52) | 0.49 (0.49-0.49) | 0.21 (0.20-0.21) | 282 | 272,580 |
| C1B | Enhanced Rich | 3.50 (3.50-3.51) | 0.49 (0.48-0.51) | 0.21 (0.20-0.21) | 282 | 272,580 |
| C2 | Faithful | 0.85 (0.85-0.86) | 1.08 (1.08-1.09) | 0.56 (0.54-0.58) | 1,474 | 308,853 |
| C2 | Enhanced Original | 3.00 (2.99-3.02) | 1.25 (1.24-1.27) | 0.58 (0.58-0.62) | 1,606 | 485,367 |
| C2 | Enhanced Rich | 2.99 (2.99-2.99) | 1.25 (1.24-1.26) | 0.59 (0.59-0.60) | 1,606 | 485,367 |
| C3 | Faithful | 1.34 (1.33-1.35) | 0.82 (0.81-0.85) | 0.73 (0.70-0.75) | 1,061 | 19,173 |
| C3 | Enhanced Original | 4.24 (4.22-4.24) | 1.10 (1.08-1.12) | 0.75 (0.73-0.77) | 1,211 | 287,593 |
| C3 | Enhanced Rich | 4.25 (4.23-4.25) | 1.11 (1.07-1.14) | 0.75 (0.73-0.77) | 1,211 | 287,593 |
| C4 | Faithful | 0.74 (0.73-0.75) | 0.38 (0.37-0.39) | 0.47 (0.46-0.48) | 382 | 205,459 |
| C4 | Enhanced Original | 2.70 (2.69-2.70) | 0.48 (0.47-0.49) | 0.47 (0.46-0.47) | 462 | 59,806 |
| C4 | Enhanced Rich | 2.71 (2.69-2.72) | 0.48 (0.48-0.49) | 0.47 (0.47-0.47) | 462 | 59,806 |

**Wave A's cost is below this rig's noise.** Rich against Original moves `gpu_ms` by -0.01 to
+0.01 ms and `render_cpu_ms` and `proc_ms` by at most 0.01 ms at every view, inside the 0.01 to
0.03 ms range of one case's launches, with the same draws and primitives: the Rich layout swaps
nodes one for one, and the mirror, tint, sway and sun side are a few vertex and fragment
instructions on cards that cover a small share of the frame. The Enhanced stack itself (TAA, Ultra
shadows, SSAO, SSR at 5120x1421) is the frame's cost: 2 to 5 ms of GPU over Faithful at every view,
the most at C1, whose firs fill the frame and every shadow cascade.

**Ceiling.** Waves B to D together may add at most **2.0 ms of `gpu_ms`** and **0.5 ms of CPU
(`render_cpu_ms` plus `proc_ms`)** over the Rich row at any of the five views, measured with this
script: Wave B (mass) 0.75 ms GPU, Wave C (density) 0.25 ms, Wave D (near) 1.0 ms, the CPU half
shared as each wave needs it. The reasoning: C1 is the heaviest view at 5.87 ms, and 5.87 + 2.0 =
7.87 ms keeps the worst vegetation view inside the 8.33 ms period of the author's 120 Hz display
with half a millisecond to spare for the plane, the cockpit and the HUD, which a freecam view does
not draw. 2.0 ms is about a third of what Enhanced already costs at C1, so Rich stays a fraction of
the mode that carries it. The CPU half is small because the frame's CPU side in flight belongs to
the simulation and the AI, and a co-op host carries both (Decision 18). A wave whose difference is
below 0.05 ms (about twice this baseline's launch range) counts as zero; a wave over its share
either buys it back from a later wave's share or is cut before it ships. The Deck and Linux are
outside this ceiling and stay Original until D36 measures them.

**Model recommendation.** A mid-tier model suffices: the measurement is a script run and a table,
and the goldens step follows `docs/verification.md`'s GOLD rules.

**Verify.** The baseline above, from `measure.ps1 -Launches 4`, every launch exit 0 and every
Rich capture's hash different from its Original twin's (the word reached the draw). Goldens: the
goldens stage alone (`RunTests.ps1 -SkipUnits -SkipEngine`) moved five Enhanced shots on listed
chapters, `c1-lake-enhanced`, `c1-rocket-hit-enhanced`, `c1-cockpit-enhanced`,
`c1-cockpit-xr-sim-enhanced` and `c1b-ocean-enhanced`, and no Faithful, Original or C5 shot. Each
rendered one hash four times over two runs at four workers (each run's batch and its re-render),
so none flips under load. Each shot rendered with `--vegetation-quality=original` added to its
manifest args lands on its old pin exactly, so the Rich word is the whole of each move. The five
hashes are re-pinned by hand from those renders (never `-RegenGoldens`, so no other shot could be
re-baselined), `c1-lake-enhanced`'s `exercises` names the Rich conifers (its frame sensitivity reads
23.70 % under Rich against 23.68 % under Original, so the recorded figure stands), and a third run
passes 38 of 38. `c1-cloud-deck-enhanced`, which moved once after A3, holds its pin in all three
runs.

**⚠ Traps.** Read `docs/verification.md` first. A golden that flips under external GPU load is a
determinism defect to name and track, not one to re-pin.

**Verified.** <pending orchestrator run>

# Wave B, mass

## B11 ☑ Canopy mask bake: from stamp density (C1) and from texture picks (C3)

**Goal.** A per-chapter canopy mask that says where forest mass is drawn: baked from the placed
stamps' density where the forest is cards (C1), and from the texture picks where it is painted (C3).

**Evidence (confidence: traced-to-code for the inputs, measured for the coverage, lead-only for the
density scale).** Decisions 11 and 12. `OceanMask` (`CSVM/src/Effects/OceanMask.cs`) is the
baked-mask precedent: a pure compute over world triangles and a bake memoised per built world. The
Rich placements are `KindExport.Rich.Placements`, index-aligned with the Original stamps, jittered
(A3) and with a zero basis where A6's brush mask dropped the stamp; `ApplyVegetation` and
`ApplyVegetationMask` replace them through `BakeRich`. C1's `terpat02` stamps its firs (card 10 m
wide, 17.8 m tall) about one per 32 m cell. C3's picked canopy measures 22.1 km² of the survey's 22.0
for `forest1`, `forest1-128` and `cliff1`.

**Approach.** `CSVM/src/Mech3/CanopyMask.cs`, a pure module, with the bake behind
`ClutterBuilder.Canopy()`.

- *Shape (what B12, B13 and B15 read):* square cells of `CanopyMask.CellM` = 32 m over `world1`'s
  `area` (384 × 384 on C1, C1B, C2 and C4, 512 × 512 on C3), row 0 along the lowest Z as in
  `VegetationMask`. Each cell holds `Density` (0 to 1), `Ground` (the mean ground height under the
  canopy) and `Top` (the mean tree top); `DensityAt` samples bilinearly and `CellAt` indexes a cell.
  B12 sets its shells between `Ground` and `Top` and fades them by `Density`; B13 places clumps per
  cell in proportion to `Density` at `Ground`, sized to `Top - Ground`; B15's volume rises from
  `Ground` over the painted cells by a thickness of its own, since painted canopy has `Top = Ground`.
  A consumer that wants a texture uploads `Density` as R8 itself.
- *Stamps (C1 and every chapter with no canopy pick):* every listed kind's Rich placements; each
  kept stamp's crown (a disc as wide as its card) splits bilinearly over the four cells around it, so
  the stamp lattice reads even whatever its phase to the grid. Density is the crown share over
  `FullCrownShare` (0.075, one fir crown a cell), held at 1; ground and top are crown-weighted, the top
  the stamp's origin plus its card height (the card shader draws no scale). A zero-basis stamp counts
  nothing, so the brush clears mass and cards together. On C1: 78,747 cells covered, median 0.46,
  4,269 full, the full ones a median fir's height over their ground. The density keeps the
  template's per-repeat pattern, which is what the cards show; a smoother look is B12/B13's filter.
- *Picks (C3):* the painted canopy is named through A6's pick mechanism: a pick to the reserved,
  empty set `canopy` (`CanopyMask.CanopySet`) marks a ground texture as painted forest. The set stays
  empty, so C21 stamps no card there (Decision 11), and the reader and writer need no change. C3 ships
  `"sets": {"canopy": []}` with `forest1.tif`, `forest1-128.tif` and `cliff1.tif` picked to it (the
  gamez texture names the lab's pick writes); the transitions (`cliff1_toptrans`, `cliff1_sidetrans`,
  `cliff1_watertrans2`) stay free for B15's palm picks. The bake takes the world triangles carrying a
  canopy texture (`VegetationGround.Painted`, flagged or not, since a painted layer shows either way),
  marks 4 × 4 sample points a cell under the highest such triangle, and gives a cell the covered share
  times the brush mask's factor held at 1 (the brush thins or clears painted canopy and never
  thickens it). A chapter with any canopy pick bakes from picks alone, so C3's 707 palms add no mass.
- *Storage:* baked at need and cached on the builder, never shipped. Both inputs are already in the
  session (the Rich placements and the sidecar), and a shipped file would drift from the lab's edits.
- *Live:* `BakeRich` drops the cached mask, so after a lab edit (`ApplyVegetation`,
  `ApplyVegetationMask`) the next `Canopy()` re-bakes: 8 ms on C1 from stamps, 28 ms on C3 from
  picks. A consumer re-reads it from `GameSession.ApplyVegetation`/`ApplyVegetationMask`, where it
  hooks its own rebuild.
- *Rich only:* `Canopy()` bakes nothing until called, and the consumers call it while
  `VegetationQualitySetting.RichOn` only, so Original bakes and reads nothing. A chapter with no
  vegetation entry (C1C, C2B, C5) answers null. The mask covers the map; the edge copies past it are
  the consumers' to extend.

**Model recommendation.** Opus: the item is a data contract three later items read, with the Rich
placements' bit-identity rules beside it; the code itself is small.

**Verify.** Units: `CanopyMaskTests` (the grid from either corner order; a crown's share, held at 1,
and crown-weighted heights; a dropped or off-area stamp counts nothing; a stamp on a cell line splits
evenly and a one-fir-a-cell lattice is full whatever its phase; a painted cell's covered share, the
higher layer's height and top equal to ground; the brush clears and never thickens painted canopy;
the shipped C3 picks name its painted jungle and not its transitions, with the canopy set empty) and
the existing `VegetationSidecarTests` (the shipped file reads clean and round-trips byte-identical
with the canopy set). Engine: `canopy-mask` (headless, about 2.2 s): no entry gives no mask; a
jittered C1 entry's mask equals one baked from the Rich placements and differs from one baked from
the Original; C1's full cells stand 10 to 25 m over their ground; a Clear stroke empties the densest
cell while its Original stamps stay, and a Neutral stroke gives the first mask back; C3's painted
canopy covers 18 to 25 km² and a re-bake gives it again. The suite writes `canopy-C1.png` and
`canopy-C3.png` (density in green over the solid ground's heights in grey, two pixels a cell, row 0
at the top) beside its report. No golden moves: nothing draws from the mask until B12.

**⚠ Traps.** A mask baked from stamps must bake from the Rich placements (after A3 and A6), or the
mass and the cards disagree.
- Never give the `canopy` set a member: C21 would stamp it on the painted treetops.
- The lab's pick tool removes a pick when no kind is checked, so re-picking a canopy texture there
  deletes its canopy pick; a canopy toggle in the lab belongs to B15 if the look needs it tuned.

**Verified.** <pending orchestrator run>

## B12 ❌ Prototype: canopy shells on C1

**Goal.** Stacked alpha-tested shells over C1's forest that take over as the cards fade, so the
forest reads as mass from altitude.

**Evidence (confidence: traced-to-code for the fade and the mask, lead-only for the look).** The
layered-forest technique for distant views is in the literature (TU Wien 2006 report on forest
rendering); Decision 12. C1's listed firs fade over `far_fade_range` near 500 to 1000 m and far
1000 to 2000 m (`firtree1`; `firtree2` and `dougfirtree1` 300 to 600 and 1000 to 2000), and the
fade's global scale is divided by the Enhanced fog push (2) and the View Distance reach (Far, 2), so
under `--det` the Enhanced cards stand fully drawn to about 2 to 4 km, behind C1's haze. **From
altitude the forest does not vanish by the fade at these views: it thins, because every card is an
upright CylindricalY quad**, so seen from above a fir is a narrow spike over the painted ground and
a full canopy cell (one fir per 32 m) reads as scattered dots. The shells are therefore driven by
the eye's height as well as the fade. C1's fir card is dark foliage (mean opaque texel about
sRGB 21, 30, 16).

**Approach (as prototyped).** Eight alpha-tested shells a 2 km chunk over the canopy mask, each
drawing the most-stamped listed card (C1's `firtree1`) revolved into crowns on a jittered 9 m
lattice, taking over from the cards by the eye's height and the card fade. The prototype, its suite
and its flag are in git history (`git log --grep=B12`); the tree no longer carries them.

**Outcome: not picked (B14).** GPU per frame on C1 at 5120x1440, `--det`, against Rich alone:
17.42 ms at `measure.ps1`'s 80 m view against 6.06 (+11.4 ms), and 15.38 / 14.69 / 13.91 ms at the
300, 600 and 700 m poses against 4.33 / 3.33 / 3.29, fourteen to fifteen times Wave B's 0.75 ms share at each. The
crowns read near black at every pose, since the fir card is.

## B13 ☑ Prototype: impostor clumps on C1

**Goal.** Pre-rendered cards of several trees placed past the card fade on C1, as MSFS does.

**Evidence (confidence: traced-to-code for the fade distances, lead-only for the look).** Decision 12.
Under Enhanced the card fade does not sit where the original put it. C1's firs author
`far_fade_range` 500-1000 m near and 1000-2000 m far; Enhanced divides the fade scale by the fog
push (2) times the View Distance reach (Far, the default and `--det`'s, 2), so the firs fade from
2-4 km to 4-8 km. C1's zone-1 fog, pushed the same 2, ends at 3.5 km (authored 1000-1750 m). The
Rich cards therefore never fade inside the visible range at any of the montage poses; at Normal
View Distance they fade from 1-2 km, still mostly in the fog. A clump that appears only where the
cards fade out is never seen, so the mass this item can add is density in front of the fog, not
continuation past the fade.

**Approach (as prototyped; B14 is the production form).** `CSVM/src/Mech3/CanopyClumps.cs`, behind `--canopy-clumps` (off by default, Enhanced
Rich only); `GameSession.FollowCanopyClumps` builds it at load, drops it on a live move off Rich or
to Original, and builds it again from the re-baked mask after a lab edit.

- *Clump art: the chapter's own cards (Decision 6), composed at load.* Every listed card kind's
  texture (`TextureArchive.FindImage`, so an HD pack's art is used where present), weighted by its
  stamp count, is resized to its real metres and alpha-blended into one atlas of six 24 m tiles
  (10 px a metre), four to six trees a tile in even strata across it, back rows raised 8 m and
  shaded 0.9. Clear texels are bled to the solid colour before the mips, or the clumps darken far
  off. The cards were chosen over D31's Blender models because they are the exact art the clumps
  stand beside (same palette, unlit as the cards draw), they exist for every listed species on every
  chapter today while D31 has one fir, and composing them costs about 100 ms at load with no new
  asset to ship or check for copyright. The models' gain, true depth from above, is lost at the
  distances clumps draw.
- *Placement from `CanopyMask` (B11).* A covered cell holds its density times 1.5 clumps, rounded
  by a draw, at jittered points in the cell; the base is the ground interpolated between covered
  cells' `Ground`, sunk 18 % of the clump's height so a cross slope shows no gap. The clump is the
  tile scaled by the cell's `Top - Ground` over the listed cards' mean height (19.5 m on C1), so its
  trees stand at the cards' size. Every draw is an integer hash of a fixed seed, the cell and the draw,
  so the layout is the same on every launch and a brush stroke moves only its own cells' clumps. C1:
  58,079 clumps in 30 chunks of 2 km (one MultiMesh each, for the frustum).
- *Fade.* A clump fades in by alpha (not dithered: the dither read as a screen door in the band)
  from a near distance drawn per clump in 350-900 m over the next 600 m, and collapses inside it, so
  the near view is the Rich cards alone. Past the card fade it stays, so at a View Distance whose
  fade falls inside the fog the clumps carry the forest on. The shader is the card's: cylindrical
  face, fog, sun-side gain, tint line, per-clump brightness and mirror.
- *Map edge: open.* `MapEdgeExtender` copies card stamps past the edge in its rolling window; it
  carries no clumps, so the mass stops at the map edge while the cards continue. At the default
  View Distance the edge is in fog from the montage poses; B14 extends the copies if this technique
  is picked.

**Model recommendation.** Opus for B14's productionising if clumps win: the look iterates against
captures, and the map-edge copies touch `MapEdgeExtender`'s rolling window.

**Verify.** Units: `CanopyClumpsTests` (placement is a function of the mask and seed; clumps stand
only in covered cells, about 1.5 a full cell; a clump's size, sink and fade band; clearing one cell
moves no other cell's clumps; a tile is laid out back first inside its width, the same every call).
Engine: `canopy-clumps` (about 10 s, windowed through `headless-limits.json`'s shader list): a Rich
flight without the flag builds none; with it, clumps stand; a live move to original drops them from
the tree and back builds the same chunks and counts; a live switch to Original drops them and back
builds them. No golden moves, since the flag is off by default. Montage (yours to judge):
`.scratch/b13/montage-P1.png` to `montage-P4.png` and `montage-overview.png` in the B13 worktree,
`--freecam --chapter=C1 --det --mute --graphics=enhanced` at B12's four poses, columns Original,
Rich, Rich with `--canopy-clumps`. The budget script runs it as `measure.ps1 -Chapters C1 -Modes
rich,clumps`.

**⚠ Traps.**
- A fade-in tied to the card fade is invisible under Enhanced (see Evidence); judge the clumps at
  the default View Distance, not at the authored fade.
- The clumps add trees: about 1.5 clumps of five trees a full cell against the cards' one fir, so
  the forest thickens with distance. The thickening starts 350-900 m out and reads as a darker band
  on the hills from 300 to 600 m up; that is the look being judged, not a defect to tune away
  unseen.
- P4 (1200 m up) is inside C1's cloud band (970-1124 m, its fog to 1047 m), so all three columns
  show cloud; it says nothing about either technique.
- Never let a clump read a card's Original stamps: the mask is the Rich bake's, and Original builds
  no clumps.

## B14 ☑ Pick the mass technique at the controls and productionise it

**Goal.** One technique chosen by you from B12 and B13, made production quality on C1.

**Evidence (confidence: measured cost, judged look).** Decision 12. Your pick is the impostor
clumps (B13); the shells (B12) are not picked. The prototypes' GPU per frame on C1, 5120x1440,
`--det`: at `measure.ps1`'s 80 m view Rich 6.06 ms, with shells 17.42 ms, with clumps 6.09 ms; at
the 300, 600 and 700 m poses (`--pos=-8575,491,-5580`, `-8275,791,-5280`, `-8075,890,-5080`, all
`--lookat=-9275,191,-6280`) Rich 4.33 / 3.33 / 3.29, shells 15.38 / 14.69 / 13.91, clumps 4.81 /
3.47 / 3.43, single launches noisier at those views. The prototype clumps' weakness was that they
added trees (about 1.5 clumps of five a full cell over the cards' one fir), so the forest thickened
and darkened with distance, a darker band on the hillsides from 300 to 700 m up, and their trees
read darker than the Rich cards beside them (back rows shaded 0.9, the sun-side gain on half a clump
rather than half a tree, and front rows sunk 18 % of the clump's height).

**Approach (as built).** `CanopyClumps` draws by default under Enhanced and Rich on C1
(`CanopyClumps.DefaultOn`); `--canopy-clumps=on|off` forces it either way on any chapter with a
canopy mask. The switch stays because it is the A/B every check of this item reads: a golden's
off-state, the budget script's `noclumps` mode and the other-chapter captures. Under Original
nothing builds.

- *A handover, not an addition.* Past `HandoverNearM`-`HandoverFarM` (1000 to 1600 m, plain metres,
  so no View Distance moves it) the listed cards' Rich material fades them out by their own dither
  and the clumps fade in by alpha; past it the clumps alone draw, to the fog. The card side is a
  `canopy_handover` uniform in the Rich card text, zero (and skipped) while no clumps stand, so the
  other chapters' Rich cards draw as before.
- *Trees per area.* `CanopyClumps.Rate` gives each canopy cell its kept listed stamps over a tile's
  mean tree count (3.17 on C1), so the clumps carry the cards' trees cell by cell: 37,508 stamps,
  11,857 clumps. The mask's density is held at one fir a cell, so a rate from it moved trees out of
  the dense stands. Tiles hold two to four trees, so the clumps spread nearly as evenly as the
  one-a-cell lattice.
- *Colour.* No back-row shade; each clump's instance colour takes the Rich cards' brightness and tint
  spreads (mean 1); a two-channel side map gives each tree in a tile its own sun-side half, so the
  shared `RichFragmentLines` gain lands as it does on a card. The C1 cards are unlit with white
  vertex colours, so nothing else enters. A clump stands on the lowest ground under its width, 0.5 m
  down, at full card height.
- *Band placement.* A handover at 450 to 1050 m or 600 to 1000 m read sparser than the cards: a tile's
  trees stand together, and the ground paints a dark blob under every original stamp, which shows bare
  where a clump stands elsewhere. Past 1000 m both are under a few pixels and in C1's haze.
- *Map edge.* `MapEdgeExtender.SetClumps` gives each extension cell its source cell's clumps
  (`CanopyClumps.Field.Copy`), moved as its ground is, so the mass continues past the edge with the
  card copies, which take the same handover.
- *View Distance.* At Normal the cards' own fade starts inside 1 km (`firtree2` and `dougfirtree1`
  from 600 m), and the clumps carry the far hills the cards leave thin; at Far, Very Far and
  Unlimited the frame matches Rich alone in density and colour.
- *Other chapters (left off).* `--canopy-clumps=on` against off at `measure.ps1`'s views raised to
  about 300 m and from 1.5 km: C1B's 339 stamps all stand inside 1 km of its view, so nothing
  differs; C2's clumps mix palms with brush into one tile (card height 6 m) and stand larger plants
  across the far desert, palms over a hill's skyline, where the brush cards are specks; C4 (27,677 clumps) matches its cards from 1.5 km, its 300 m view sits in cloud.
  None clearly reads right, so `DefaultOn` names C1 alone; C3 is B15's.

**Ruling (yours, from the montage).** The handover stays: at Far and beyond the clumps keep the
cards' density and colour rather than adding mass, and their gain is View Distance Normal's far hills
and the forest past the map edge.

**Model recommendation.** Opus: the look iterates against captures and the rate, band and handover
are judgement calls the measurements settle.

**Verify.** Units: `CanopyClumpsTests` (placement a function of the mask, rate and seed; the rate
carries the stamps' tree count to 10 %; a clump's size, base and colour spread; a cleared cell
moves no other cell's clumps; the tile layout; C1 alone by default). Engine: `canopy-clumps` (a C1
Enhanced flight at rich builds them by default, the cards' Rich material carries the band and the
map edge copies them; a live move to original drops clumps, copies and band and a move back builds
the same chunks; a live switch to Original drops them and back builds them; a lab re-bake builds new
ones; `--canopy-clumps=off` or a session at original builds none). Look: `.scratch/b14/montage-C1.png`
(80, 300, 600 and 700 m, Rich before against Rich with clumps), `.scratch/b14/motion-strip.png`
(1800 to 600 m from the target at 300 m up, no band or pop), the other chapters and View Distances
under `.scratch/b14/other/`. In the 600 m view's forest the mean sRGB moves by under 1.5 levels and
the share of tree pixels by 10 % less with clumps.
Budget: `measure.ps1 -Modes rich,noclumps -Launches 4` on an idle machine puts the clumps at
+0.04 ms GPU, +0.07 ms `render_cpu_ms` and +0.08 ms `proc_ms` at C1's 80 m view (5.90 against
5.87 ms, 469 draws against 421), within Wave B's 0.75 ms GPU share and its CPU half; at the 300,
600 and 700 m poses `rich` against `noclumps` reads 3.80 / 3.78, 2.79 / 2.79 and 2.68 / 2.69 ms over
four launches each. C1B, C2, C3 and C4 draw no clumps and match A7's Rich row within 0.02 ms.
Goldens: the goldens stage alone moved the five Enhanced C1 shots A7 re-pinned,
`c1-lake-enhanced`, `c1-cloud-deck-enhanced`, `c1-rocket-hit-enhanced`, `c1-cockpit-enhanced` and
`c1-cockpit-xr-sim-enhanced`, and no Faithful, Original or other chapter's shot. Each rendered one
hash four times over two runs at four workers, with other worktrees' probes running beside them;
each rendered with `--canopy-clumps=off` added to its manifest args lands on its old pin exactly, so
the clumps are the whole of each move. Four move 1.6 to 2.0 % of their pixels, all on the far hills;
`c1-cloud-deck-enhanced` moves four pixels by one level on the screen's centre column under the deck.
The five hashes are re-pinned by hand (never `-RegenGoldens`), `c1-lake-enhanced`'s `exercises`
names the handover, and a third run passes 38 of 38.

**⚠ Traps.**
- A handover inside 1 km shows: a tile's trees stand together, and the ground's dark blob under each
  original stamp shows bare. Keep the band past where both are a few pixels.
- Never rate clumps from the mask's density: it is held at one fir a cell, so the dense stands lose
  trees.
- The cards' handover is a uniform on the shared Rich material; every path that drops the clumps
  zeroes it (`CanopyClumps.Handover`), or the cards fade with nothing behind them.

## B15 ☑ C3's canopy volume over its painted jungle

**Goal.** C3's `forest1`/`cliff1` canopy gains a volume with depth. The palms on its transition
textures are C21's: a pick to a set places no stamp until C21 builds the added stamps.

**Evidence (confidence: measured areas, lead-only look).** The survey table above; Decision 11.
Picks are read today only for the canopy test (`CanopyMask.IsCanopy` in `ClutterBuilder`).

**Approach (as built).** B14's `CanopyClumps` fed by B11's pick-baked mask. `Build` takes a painted
path when the chapter has a `canopy` pick; C3 draws it by default (`DefaultOn` names C1 and C3).

- *Rate.* Painted cells hold no stamps, so `CanopyClumps.Rate(mask, PaintedTreesPerCell, meanTrees)`
  gives each cell its covered share times 6 crowns (about 13 m crowns edge to edge in a 32 m cell)
  over a tile's mean tree count (3.17). B14's trap does not apply here: a pick-baked cell's density
  is the share of it the canopy texture covers, never held at a cap. C3: 40,947 clumps in 19 chunks.
- *Height.* Painted canopy's top is its ground, so `Place` takes `PaintedRiseM` (16 m) as every
  clump's rise; a crown tile stands 25.6 m with its back row. A clump stands on the painted
  surface, which is the treetops, so the volume adds crowns above the canopy the texture shows.
- *Art.* Crowns cut from the picked canopy textures themselves (`forest1`, `forest1-128`,
  `cliff1`): three dome silhouettes a texture, each of overlapping discs darkened to their rims and
  the card darkened toward its base, the texture tiled 12.8 m a repeat. The crowns keep the painted
  colour and grain: on hill regions the mean colour sits within 1 to 5 sRGB levels of the frame
  without them.
- *Near bound.* No card stands on the painted canopy, so nothing hands over and the beach palms
  keep every distance (`Handover` is not called). The clumps take their own band,
  `PaintedNearM`-`PaintedFarM` (700 to 1300 m): inside it nothing stands, so the near view is the
  painted canopy alone. Over the band a clump rises out of the canopy: the shader's `grow` sinks it
  below the painted surface by its unfaded share of its height, so it stays opaque.

Rejected in the montage, one line each:
- C3's palm cards as the clump art: too much colour variation, a grey-brown palm plantation over the
  broadleaf jungle.
- An alpha fade over the band: half-clear ghost crowns on the skyline.
- Squashing the clump's height over the band: flat, light pancakes on the near slopes.
- A 400-1000 m band: crown tips poke through the near slopes as light spots.
- A 1000-1600 m band: the hills at about 1 km stay flat.

**Ruling (yours, from the montage).** Crowns cut from the canopy texture, rising out over 700 to
1300 m, on by default on C3. Once Wave D's look-alike models exist, they fill the 0 to 700 m band
over C3's painted canopy (D32).

**Model recommendation.** Opus: the art, rate and band are look calls iterated against captures.

**Verify.** Units: `CanopyClumpsTests` (a painted cell's rate is its share times the crowns a full
cell carries, and the clumps carry that many trees per covered area to 10 %; none stands off the
painted cells; without a rise painted canopy stands none, with one every clump takes it and the
layout repeats; the painted band starts past 500 m and differs from the cards' band; C1 and C3 on
by default). Engine: `canopy-clumps` adds a rich C3 flight: clumps stand, their material carries
the painted band, the palms' Rich material carries no handover, and every clump stands in a painted
cell. Look: `.scratch/b15/` in the plan worktree (the five poses with clumps off, crowns and palm
cards; the band and fade variants; a 3000 to 600 m approach to a ridge with no pop). Goldens: C3's
two shots (`c3-island`, `c3-flare-xr-sim`) pass no `--graphics=enhanced`, and the C1 shots see the
clump shader's `grow` lines at zero; all 38 pass unchanged.
Budget: `measure.ps1 -Chapters C3,C1 -Modes rich,noclumps -Launches 4` on an idle machine puts C3's
80 m view at 4.33 against 4.27 ms GPU (+0.06; 1,242 draws against 1,211, 361k primitives against
288k) with `render_cpu_ms` and `proc_ms` within 0.01 ms; C1's view is unchanged within 0.05 ms. At
300 m up, 4 km east of the southeast island's hills (`--pos=-2400,300,-4600
--lookat=-6400,100,-4600`), 3.72 against 3.65 ms GPU over four launches each. Within Wave B's
0.75 ms GPU share.

**⚠ Traps.** No cards on the painted treetops: the texture already shows them from above.
- Never call `Handover` for painted canopy: the listed cards there are the beach palms, and they
  would fade out past 1 km with nothing behind them.
- Never fade painted-canopy clumps by alpha: a part-clear clump shows as a ghost against the sky.
  Keep `grow` on for them, so they sink into the canopy instead.
- Keep the band's near edge past where the crown tips read as spots on the near slopes (about
  700 m). Inside it the painted texture carries the canopy until D32's models fill it.

## B16 ☑ Clump tiles beside the vegetation models, replaceable by a model pack

**Goal.** A species' clump tile sits beside its 3D model in `CSVM/data/vegetation_models/<chapter>/`,
rendered from that model, so the far clumps match the near models. A model pack (`HDModels`) replaces
either file under the same key. Without a tile, the clumps compose at load as they do today.

**Evidence (confidence: traced-to-code for the loaders, lead-only for the look).** B14's atlas is
composed at load from the listed cards' images (`CanopyClumps.Compose`); B15's crowns are cut at load
from the canopy textures (`CanopyClumps.Crowns`). D31 writes `<chapter>/<model>.glb.bin`. The model
pack reader is `Extraction/PackIndex.cs` and `HdModels.cs` ([`formats/model-packs.md`](formats/model-packs.md));
it never replaces a billboard, so a vegetation card has no pack key yet. Your ruling: the model and its
clump texture live in one folder, and the shipped ones are generated by the remake.

**Approach (as built).** [`formats/model-packs.md`](formats/model-packs.md), "Clump tiles", is the
reference; `Mech3/ClumpTile.cs` reads a tile and `CanopyClumps.Build` places it.

- *Key.* `<chapter>/<card>.clump.png`, `<card>` the listed card's model without `.flt`
  (`C1/firtree1.clump.png`). C3's crowns have no card, so a painted tile takes the canopy texture's
  name without its extension (`C3/forest1.clump.png` replaces the three crowns cut from `forest1.tif`).
  Shipped as `CSVM/data/vegetation_models/<chapter>/<card>.clump.png.bin` beside the GLB; an
  `HDModels` pack replaces it under the same key, before the shipped file, before composition. A
  chapter key only, since a card name is a different size per chapter. The pack index keys a clump
  file by its whole name with its extension, as it keys `rig.json`, so `HdModels.KeyFor` never
  resolves a node to one and refuses a node named like one; any other `.png` in a pack is ignored.
- *Beside the image.* A sidecar, `<card>.clump.json`: `width_m` (at most 24) and `trees`, one count
  per tile. The image is a strip of equal tiles, so a species carries several and its height follows
  from the image at that width. The side map is a second image, `<card>.clump.side.png` (red the
  sun-side share on a tree's right half, green on its left, as the composed map), optional: without
  it each tile is shaded as one tree. All three come from the one source holding the image and its
  sidecar. A pack with only half of that pair, or with a bad sidecar, logs a warning and keeps the
  shipped tile, so a pack image never meets the shipped tree count.
- *Atlas.* Whole-tile replacement per species. `CanopyClumps.Slots` gives each tiled species its
  stamp share of the six slots by largest remainder; the untiled species together take the rest and
  compose from their cards alone. Composed slots come first, so with no tile the atlas is today's. A
  species under about a twelfth of the stamps gets no slot and stays composed. `MeanTrees` over the
  slots reads each tile's own count, so `Rate` keeps the cards' trees per area whatever a tile holds.
  A tile stands centred on its slot's floor, which grows to the tallest tile. On C1 `firtree1` takes
  two slots of six; the other four compose from `dougfirtree1`, `firtree2`, `bush1` and `bush2`.
- *The render.* D31's pipeline gains `veg_blender.clump_render` ([`tooling.md`](tooling.md),
  "Vegetation models"): tiles of the generator's `clump_trees` (3, 2 and 4 for `firtree1`) laid out
  as `Layout` lays out a composed tile (24 m wide at 10 px a metre, strata across the width, 0.85 to
  1.1 of the card, back trees up to 16 m deeper and 8 m higher), each tree turned at random. EEVEE,
  orthographic from the side at four times the size, averaged down and cut at alpha 0.5, lit by the
  sky and a sun above the camera so neither side is lit; a second pass emits each tree's offset from
  its trunk for the side map. Cycles shaded the crown's bent normals about eight times darker, so it
  is not used. The generator's `clump_grade` (linear, per channel) puts the tiles' mean on the card's (sRGB 21.9,
  30.1, 16.4 against the card's 21.8, 30.1, 16.3): the cards draw to 1 km and hand over to the clumps
  past it, and the unscaled render was 2.5 times lighter. The PNGs come from the script's own encoder;
  two reruns wrote all three files byte for byte the same. The card's pixels never reach a tile.
- *On C1.* The shipped tile's 3 and 2 trees equal the composed tiles they replace, so a clump still
  carries 3.17 trees and C1 stands its 11,857 clumps as before. At the 300 and 600 m poses 2.4 % of
  the frame moves by more than 2 levels, all on the far hills, and the moved pixels' mean sRGB moves
  by 1 to 1.3 levels (lighter).

**Ruling (yours, from the montage).** The `firtree1` tile stays graded to the card, so the far
forest matches the near cards with the model's silhouette. A lighter grade, like D31's model, waits
for D32's near models to replace the cards.

**Model recommendation.** Opus for a new species' tile: its grade and layout are look calls against
captures, and a pack format change touches the pack docs. A mid-tier model can rerun an existing
generator.

**Verify.** `.\MakeVegetationModel.ps1 -Species firtree1` exits 0 headless and writes the three tile
files; a second run writes the same bytes. Units: `ClumpTileTests` (a pack beats the shipped tile,
which beats none, with the side map from the image's own source; half a pair or a bad sidecar keeps
the shipped tile; each sidecar error names its file and reason), `HdModelsTests` (a clump file is read
by its whole key, counts as no replacement and reaches no node, not even one named like it),
`CanopyClumpsTests` (no tile composes every slot; a tiled species takes its share after the composed
slots and a rare one stays composed; the mean reads a tile's own counts and the clumps then carry the
stamps' trees to 10 %). Engine: `canopy-clumps` adds that a C1 flight takes the shipped `firtree1`
tile, that a build with no tile composes another atlas, and that a model pack's tile under the same
key replaces it, its counts raising the trees a clump. Look: `.scratch/b16/` (the composed tiles
against the `firtree1` tile at 300 and 600 m up, View Distance Far and Normal, with 3x crops of the
far hills, and the tile beside its model and the card). Goldens: exactly the five Enhanced C1 shots
B14 re-pinned move (`c1-lake-enhanced`, `c1-cloud-deck-enhanced`, `c1-rocket-hit-enhanced`,
`c1-cockpit-enhanced`, `c1-cockpit-xr-sim-enhanced`), each re-rendering to its new hash, and no
other of the 38; B17 re-pins them. Budget: the atlas keeps its size and the clump count stands at
11,857, so B17's measurement is its check.

**⚠ Traps.**
- Never ship a tile composed from the original's card or canopy textures. A shipped tile is rendered
  from a remake model; the load-time composition stays load-time only.
- A replacement tile changes the trees a tile holds, so the rate reads the tile's own count, or the
  forest's density moves.
- The pack index is shared with the HD models: a clump key must never reach a world node.

## B17 ☑ Wave B budget and goldens

**Goal.** As A7, for Wave B.

**Evidence (confidence: measured).** Decision 18. A7's instrument, views and ceiling.

**Approach.** `measure.ps1 -Launches 4`, unchanged, on an idle machine with gaming mode off, and the
goldens stage re-pinned by hand for the shots Wave B moved.

**Result (Wave B).** Median (range of the launch medians), four launches a case:

| Chapter | Case | `gpu_ms` | `render_cpu_ms` | `proc_ms` | draws | prims |
|---|---|---:|---:|---:|---:|---:|
| C1 | Faithful | 0.71 (0.50-0.71) | 0.30 (0.29-0.31) | 1.32 (1.20-1.35) | 240 | 88,596 |
| C1 | Enhanced Original | 5.88 (5.87-6.25) | 0.48 (0.45-0.51) | 1.31 (1.25-1.37) | 421 | 322,531 |
| C1 | Enhanced Rich | 5.88 (5.88-5.93) | 0.50 (0.47-0.53) | 1.29 (1.24-1.33) | 469 | 335,491 |
| C1B | Faithful | 0.65 (0.64-0.65) | 0.36 (0.34-0.36) | 0.20 (0.18-0.20) | 211 | 2,452 |
| C1B | Enhanced Original | 3.54 (3.52-3.55) | 0.52 (0.48-0.54) | 0.21 (0.20-0.22) | 282 | 272,580 |
| C1B | Enhanced Rich | 3.53 (3.50-3.53) | 0.53 (0.50-0.56) | 0.21 (0.21-0.22) | 282 | 272,580 |
| C2 | Faithful | 0.85 (0.85-0.86) | 1.12 (1.11-1.13) | 0.59 (0.56-0.62) | 1,474 | 308,853 |
| C2 | Enhanced Original | 3.00 (3.00-3.00) | 1.29 (1.23-1.32) | 0.62 (0.60-0.63) | 1,606 | 485,367 |
| C2 | Enhanced Rich | 3.00 (2.99-3.01) | 1.27 (1.24-1.30) | 0.63 (0.61-0.64) | 1,606 | 485,367 |
| C3 | Faithful | 1.34 (1.34-1.37) | 0.89 (0.81-0.90) | 0.77 (0.71-0.81) | 1,061 | 19,173 |
| C3 | Enhanced Original | 4.27 (4.27-4.28) | 1.15 (1.12-1.15) | 0.78 (0.74-0.79) | 1,211 | 287,593 |
| C3 | Enhanced Rich | 4.32 (4.31-4.34) | 1.15 (1.11-1.17) | 0.76 (0.72-0.79) | 1,242 | 360,817 |
| C4 | Faithful | 0.74 (0.73-0.76) | 0.40 (0.38-0.41) | 0.51 (0.49-0.52) | 382 | 205,459 |
| C4 | Enhanced Original | 2.69 (2.69-2.70) | 0.51 (0.49-0.54) | 0.51 (0.48-0.52) | 462 | 59,806 |
| C4 | Enhanced Rich | 2.69 (2.69-2.70) | 0.52 (0.47-0.54) | 0.51 (0.47-0.51) | 462 | 59,806 |

**Wave B costs at most 0.05 ms of GPU at the five views, inside its 0.75 ms share.** The rows are
read against this run's own Enhanced Original, since Wave A's cost is below the noise and the
Faithful rows drifted by up to 0.08 ms of CPU from A7's: C3's crown clumps add 0.05 ms of `gpu_ms`
(1,242 draws against 1,211), C1's clumps none (469 draws against 421), and C1B, C2 and C4 draw no
clumps and move by at most 0.01 ms. CPU moves by -0.02 to +0.02 ms everywhere. Away from these
views the clumps cost at most 0.07 ms of GPU: C1's 300, 600 and 700 m poses (B14) and C3's 300 m view
across the southeast island (B15). Waves C and D keep the remaining 1.95 ms of the 2.0 ms ceiling.

**Model recommendation.** A mid-tier model suffices, as A7.

**Verify.** Every launch exit 0. Goldens: Wave B moved exactly five Enhanced C1 shots, each at
B14 (the clumps) and again at B16 (the `firtree1` tile): `c1-lake-enhanced`,
`c1-cloud-deck-enhanced`, `c1-rocket-hit-enhanced`, `c1-cockpit-enhanced` and
`c1-cockpit-xr-sim-enhanced`. B16's battery rendered each to one new hash in its batch and its
re-render; no other shot moved. The five hashes are re-pinned by hand from those renders (never
`-RegenGoldens`), and two further goldens runs pass 38 of 38.

**⚠ Traps.** As A7.

# Wave C, density

## C21 ☑ Remake-added stamps on picked textures, in texture space, under the mask

**Goal.** Under Rich, a picked texture gets stamps from its vegetation set, once per texture repeat
like the original's, and the mask thins or thickens them. C3's transition textures
(`cliff1_toptrans`, `cliff1_sidetrans`, `cliff1_watertrans2`) are picked to palm1, palm2 and palm3;
palmtree2/palmtree3 draw today as palmtree1's substitution targets.

**Evidence (confidence: traced for the lattice, lead-only for the rest).** The lattice and its rules
are `PlaceOnTriangle` (`Clutter.cs:556`) and [`org/clutter.md`](org/clutter.md), "The lattice rule".

**Approach (as built).** `Mech3/VegetationScatter.cs` (pure) lays a set out; `ClutterBuilder.BuildAdded`
places and draws it. [`org/clutter.md`](org/clutter.md), "Where CSVM deliberately differs", states it
against the lattice rule.

- *The unit cell.* A set's `Scatter` is a fixed order of points on the unit texture repeat, seeded from
  the Rich seed and the set's name (FNV-1a, never a per-process hash). Each point is the farthest of
  eight draws from the points before it, so any prefix is spread and a prefix never depends on how far
  the order grew. A point's member is read off a draw of its own, uniform over the set's kinds, so a
  membership edit moves no point. The placement is a sibling of `PlaceOnTriangle`, not a reuse: it
  shares the world walk (`ForEachTriangle`, with both polygon gates, `no_clutter` included) and the
  per-triangle guards (`Lattice`: both areas, the footprint floor, the lattice tripwire), then
  `VegetationScatter.OnTriangle` walks the triangle's integer repeats as the original walks a template,
  under step 6's strict edge test and a quarter-metre dedup per member mesh of its own. No world grid
  exists anywhere on the path.
- *The count.* `VegetationSet.Density`, added stamps a hectare at a neutral mask. A repeat takes twice
  the stamps the density gives the ground it covers on that triangle (`UvTriangle.RepeatArea`), the last
  one weighted by the fraction left, so one set reads the same on C1's 64 m `grass1` repeat and its
  256 m `terpat03` one. The default, 7.85,
  is the original's own palm stand on C3's `cliff1_sandtrans`: six `palm1` a 7,642 m² repeat. A load
  and the lab hold it at 100. The mask keeps a candidate when its keep draw is below the mask there
  times one half times its weight (A6's rule, `added: true`): 0 clears, neutral keeps the density, 255
  nearly twice. The sidecar writes a set as a bare list at its defaults, so the shipped file round-trips,
  and as `{"kinds": [...], "density": n, "mass": true}` otherwise. Original reads none of it.
- *Wave B.* A set's `Mass` flag decides, default false. A forest set's kept stamps feed the stamp-baked
  canopy mask, B14's clump rate and atlas, and the handover, as an original stamp's do. Any other set's
  do not, and its cards keep every distance, since no clump stands behind them: a bush scatter on C1's
  grassland would otherwise raise fir clumps there and fade out past 1 km. Painted canopy (C3) bakes from
  picks alone, so the palms feed nothing either way. A pick to the reserved `canopy` set stamps nothing,
  even if the set were given a member (`VegetationScatter.Stamped`), so Decision 11 holds.
- *Drawing and live behaviour.* Each listed card of a set draws as an added kind (`Kind.AddedTwin`: the
  member's own fade, since a set places it directly, and its mesh lift; no substitution, no scale), two
  where a forest set and another name it. They are exported after the original's kinds
  (`KindExport.Added`, `KindExport.Mass`) under one `clutter_added` node that `Recut` shows only while
  `RichOn`, so a live move to original and a live switch to Original drop every added stamp and a move
  back draws the same ones. Their Original placements are the unvaried points, drawn by nothing; their
  Rich ones carry the member's variation (jitter, mirror, tint) and the mask's drop, a zero basis kept at
  its index. Every candidate takes `VegetationScatter.DrawsPerStamp` draws on an added stream of its own
  whatever the mask holds, after the Rich bake, so no original stamp, stream or draw moves and a stroke
  reshuffles no other added stamp. A lab pick, a set edit (the lab's SETS rows: a density slider a
  hectare and a forest box) and a mask stroke rebuild the added kinds whole: `ClutterActivation.RebindAdded`
  drops their old handlers and binds the new ones, and `MapEdgeExtender.FollowVegetation` bins the stamps
  again, its copies of an added kind standing only under Rich. A crater or an activation hide carries
  across a live Vegetation Quality move as for the original's stamps; a rebuild starts the added ones
  afresh.
- *Scope.* Only a chapter with a sidecar entry (C1, C1B, C2, C3, C4; `WorldSession` hands C5 and the
  unlisted chapters none) and only a picked texture gets added stamps. C3 ships `palms`
  (`palmtree1`, `palmtree2`, `palmtree3`) at 15.7 a hectare, twice the default, not forest, picked by
  `cliff1_toptrans.tif` and `cliff1_sidetrans.tif`.

**Ruling.** `cliff1_watertrans2` stays unpicked: its palms stood in the reef shallows. The transitions
take twice the beach stand's density so they read as palm groves; the value is yours to tune further
with the lab's SETS slider. The yellow sand patch at the base of the palm cards stays until D32's model
replaces them near the camera.

**Model recommendation.** Opus: the mechanism crosses the builder's export list, the activation, the map
edge and the canopy consumers, with the Original bit-identity held throughout. C22 and C23 are lab work
over it and suit a mid-tier model driving the lab's pick, density and forest controls.

**Verify.** Units: `VegetationScatterTests` (placement a function of the triangle's UVs and the seed, the
same on a moved triangle; every repeat inside the triangle holds the same scatter, the fractional last
weighted; the scatter prefix-stable and spread; a point on a shared diagonal claimed by neither triangle
and a coplanar copy refused by the dedup; mask 0 keeps none, neutral about the density, 255 nearly all;
only a picked set with members and density stamps, the canopy set never; the scatter seed fixed per set)
and `VegetationSidecarTests` (a set's density and forest flag read, clamped, warned, edited by `WithSet`
and written back, a default set as a bare list; C3 picks its cliff-top and side transitions to its palms; the shipped
file round-trips). Engine: `vegetation-added-stamps` (a C3 Enhanced flight at rich adds palms,
every sampled one on a picked triangle, half the candidates kept, none forest; a live move to original
and a live switch to Original draw none and a move back the same ones; a pick of `cliff1_sandtrans`, a
doubled density and a Clear stroke each place them again live, the stroke dropping only those under it,
and the shipped entry and the neutral mask give the first stamps back exactly; a pick to the canopy set
stamps none; C5 builds none); `canopy-mask` (a C1 bush pick leaves the mask as it was, the same pick as
forest covers more cells, and the shipped entry gives the first mask back); `vegetation-lab` (a pick adds
stamps of the kind and moves no other, a revert drops them); `clutter-activation`, `clutter-cells`,
`canopy-clumps`, `graphics-vegetation-quality` and `clutter-rich-variation` pass as before. Look:
`.scratch/c21/montage-c21.png`, C3 Rich before against after at A7's view, close along
`cliff1_watertrans2`/`cliff1_sidetrans` and 300 m up, judged by you. Goldens: none should move. C3's
two shots run Faithful, where the added node is hidden, and no other chapter ships a pick.

**⚠ Traps.**
- No world grid: added stamps walk the same UV lattice.
- Keep the strict edge test and the quarter-metre dedup (`Clutter.cs:604`) on added stamps too.
- C5 already draws about 139k sprites; density must not reach unlisted chapters' cities.
- Keep added stamps off any polygon laid over the picked ground (`VegetationGround.Overlays`). C1's railway
  (`gantry_tracks2`) and roads are polygons of their own over `terpat03` and the crops, and the picked ground's
  lattice runs on under them. Ground transitions are exempt: C1's forest-edge blends lie over the C22 hedges.

## C22 ☑ C1's grassland and fields: sparse original cards, hedgerows by mask

**Goal.** C1's `terpat03`/`terpat03b`/`grass1` carry scattered bush1/bush2 and the odd fir, and its
`crop04`/`crop05` fields stay sparse, with hedgerows painted in by mask where wanted.

**Evidence (confidence: measured areas, lead-only look).** The survey table above; Decision 13.

**Approach (as built).** Two sets and a mask in C1's sidecar entry, no code. The density a set carries is the
one knob a lab slider tunes later, so each ground that wants its own density takes its own set.

- *Grassland.* `terpat03`, `terpat03b` and `grass1` pick `scrub`: bush1 and bush2 named twice each and firtree1 once,
  at 2 a hectare. A member is read off a uniform draw over the set's list, so naming a kind twice weights it without
  a weight field. Since every repeat holds the same scatter, the share is that of the first points: at 2 a hectare 486
  of the 5,000-odd grassland candidates are firs, the odd fir. The original's own bushes along `river1`/`river2` stand about 5.8 a
  hectare and its firs about 4.3 on `terpat02`, so 2 reads as scattered next to both.
- *Fields.* `crop04` and `crop05` pick `hedge` (bush1, bush2) at 64 a hectare, and the mask holds it to the field
  edges: 255 on an edge texel (about 128 a hectare, a bush every 6 to 8 m along a 12 m strip), 2 inside (1 a hectare).
  Hedges are a set of their own, picked on the crop textures and gated by the mask, because one set has one density:
  the grassland's 2 a hectare doubled by a thickened mask is 4, which draws no line. A field takes the hedge set's high
  density, and the mask alone decides where it stands.
- *The mask* (`CSVM/data/vegetation/C1.bin`, written by `analysis/veg-ground-survey/c1_mask.py` from the extracted
  gamez). Each 12 m texel is classed by the ground under its centre. A crop texel is an edge where it borders other
  ground, where the world's 256 m grid crosses it (the crop repeats sit on that grid, their painted border along it) or
  where u = k + 0.5 crosses it (the field line painted down the middle of both crop textures, wherever a repeat is
  turned). That gives fields of 128 by 256 m lined on every side, the lines on the texture's own field borders. The
  railway's polygons (`gantry_tracks2`) are skipped, so a field under or beside the rails is interior and no hedge
  lines them. Every other texel is 128.
- *The landing strips.* No added stamp stands beside a strip or under its approach. Each piece of `tarmac01` (the
  runway texture, centre line and edge marks) of at least 300 texels is a strip: the generator finds four, the
  east-west pair north of the hangar apron and the diagonal pair south-west of it, each boxed along its principal axis.
  A texel inside a box grown by 150 m to each side and 600 m off each end is 0. Six hundred metres is where a 3° glide
  path stands 31 m up, above the tallest card (dougfirtree1, 22 m); 150 m to each side keeps a plane that drifts off
  the strip, or circles low over it, clear of an 18 m fir. The clearance leaves 31,812 of the 32,186 kept added stamps.
- *The original's stamps.* A texel goes below neutral only where neither it nor any of its eight neighbours is ground
  a C1 template stamps (`terpat02`, the river banks), so no bilinear sample there thins an original stamp: the firs
  and river bushes inside a strip's margins stand as in the original (`mask_dropped=0`). Picked ground beside the
  hangar apron, the hangars and the roads, which nothing stamps, clears with the strip.
- *Not forest.* Neither set is `mass`. Marked forest, the grassland's bushes and firs and the hedges would raise fir
  clumps over the grassland and the fields past 1 to 1.6 km (the clump tile is firtree1) and fade their own cards
  there, so the fields would turn to forest at a distance.
- *Count.* `clutter added stamps: sets=scrub,hedge candidates=145922 kept=31865`, beside the original's 37,508
  stamps on C1.

**Ruling (yours, from the montage).** The grassland at 2 a hectare with the odd fir, the hedges at 64 a hectare on all
three kinds of line, and no tree around the landing strips. A sparser (1, 32) and a denser (4, 100) option were shown
beside it.

**Model recommendation.** A mid-tier model for a retune (a density in the sidecar, the generator's two bytes); Opus
for the views, the tests and a look that iterates against captures.

**Verify.** Units: `VegetationSidecarTests.TheShippedFilePicksC1sGrasslandToScrubAndItsFieldsToHedges` and
`TheShippedC1MaskHedgesTheFieldsAndClearsTheStrips` (edges at 255, bare beside and off the east-west strips, neutral
grassland away from them). Engine: `vegetation-lab` (opened on the shipped mask, which drops no original stamp; the stroke stands where that mask is neutral, a pick moves no original stamp and a revert
gives the shipped added stamps back), `canopy-mask` (the shipped entry gives the first canopy back with its own added
candidates), `canopy-clumps` (the scrub and hedge cards keep every distance while the original's hand over), with
`vegetation-added-stamps`, `graphics-vegetation-quality`, `clutter-rich-variation`, `clutter-activation` and
`clutter-cells` as before. Look: `.scratch/c22/montage-c22.png` (before against three densities at four views:
`terpat03` from 45 m, the fields from 45 m, each from 300 m), `.scratch/c22/montage-c22-mass.png` (the forest flag
at 300 m) and `.scratch/c22/montage-c22-strips.png` (before against after the strip clearance, from 300 m and on the
western approach to the east-west strips), judged by you. Goldens: `c1-rocket-hit-enhanced` and `c1-cockpit-xr-sim-enhanced` look over the base's `terpat03` and
are expected to move; `c1-cockpit-enhanced` (the spawn over the base) and `c1-lake-enhanced` (the west grassland past
the lake) likely; `c1-cloud-deck-enhanced` only where the ground shows under the deck. No Faithful, Original or other
chapter's shot reads the C1 entry. They are C24's to re-pin.

**⚠ Traps.** Grassland must stay grassland: the sparseness is the brief.
- The mask reaches the original's stamps (Decision 10): a value below neutral beside forest or river ground thins them.
  Go below neutral only where every neighbour is picked ground; `vegetation-lab` and `mask_dropped=0` hold it.
- The generator is the mask's source: regenerate rather than paint, or the next regeneration loses the paint. It must
  write the shipped file byte for byte.
- A set's density is per set, not per pick: a lower density on one ground means a set of its own.

## C23 ☑ C1B, C2 and C4 tuned in the lab

**Goal.** Each of C1B, C2 and C4 has a sidecar entry tuned by you in the lab.

**Evidence (confidence: measured areas and stamp counts, lead-only look).** Decision 14; the survey
table above. The templates' own stand densities, from the template layouts and the C4 build's stamp
counts: C1B's `rockclut` 31 stamps a 256 m period on `vegrocktop` (25 `dougfirtree1`, 6 bushes); C2's
`terpat01` 3.5 `spruce` a hectare, `terpat04` 4.3 (brush with the odd palm); C4's `terpat01` about 6.5
a hectare (71k stamps over 109.7 km², spruce 23 : dougfir 10 : firtree1 10 a period), `terpat01-128`
and `river5` about 19.

**Approach (as built).** Sidecar values only, no mask and no code. Each chapter's undressed ground was
walked by texture (`analysis/veg-ground-survey/survey.py`'s census, located per texture) and looked at
under Rich at the A7 view and over the ground itself.

- *C1B.* Set `bushes` (`bush1.flt`, `bush2.flt`) at 3 a hectare, not forest, picked by `rocktop.tif`
  and `rocktop128.tif`, the bare grey-green domes around the `vegrocktop` fir stands. It adds 348
  kept stamps (747 candidates). A fir among the members scatters single trees over the domes, which
  read as random rather than as the stands the original paints; a denser bush scatter reads as specks
  on cliff faces. The night lighting keeps either subtle.
- *C2.* No change. Its undressed vegetation-bearing ground is the football and baseball fields, which
  are mown turf and take nothing. `terpatwrd1` (0.23 km² of scrub) carries a road strip inside its
  texture, so an added stamp would stand on the road. `beach1`, `dunes` and `cliff01` are bare by
  nature, and the suburbs, lots and parked cars are solid decorations, never listed or picked. The one
  alternative, brush (`brush1`-`brush4`) at 4 a hectare on the `terpat01` hills under their spruce,
  adds 20,737 kept stamps that read only near the ground.
- *C4.* Set `treeline` (`spruce.flt`, `dougfirtree1.flt`, `firtree1.flt`, the three cards its templates
  place) at 3 a hectare, forest, picked by `cliff01-trans1.tif` and `cliff01-trans3.tif`, the grass to
  rock transitions where `terpat01`'s firs stop at a hard edge. It adds 2,439 kept stamps (4,943
  candidates), thinner than the forest beside it so the trees thin out toward the rock. Forest so the
  stamps join the canopy mask and the clumps if C4's clumps are ever turned on; while `DefaultOn` names
  C1 alone the flag changes no frame.

**Model recommendation.** A mid-tier model: the work is a texture census, sidecar values and captures,
with the look judged by you.

**Verify.** Units: `VegetationSidecarTests.TheShippedFilePicksC1BsRockTopsAndC4sTransitions` (C1B's
rock tops to its bushes at 3, not forest; C4's two transitions to its three firs at 3, forest;
`terpat01` unpicked; C2 picks nothing) and the shipped file's round trip; the `Vegetation` unit filter
passes. Engine: `vegetation-added-stamps`, `vegetation-lab` and `canopy-clumps` pass as before (no
engine suite builds C1B, C2 or C4 vegetation). The `clutter added stamps:` log line on a C1B and a C4
Enhanced Rich `--freecam` build gives the counts above. Look: `.scratch/c23/montage-C1B.png`,
`montage-C2.png` and `montage-C4.png`, Rich before against after with the alternatives beside it at
the A7 view and over the changed ground, judged by you. Goldens: `c1b-ocean-enhanced` frames C1B's
rock tops and should move (902 pixels at its pose); every other C1B, C2 and C4 shot runs Faithful.

**⚠ Traps.** C2's suburbs and parked cars are solid decorations: never list them.

## C24 ☑ Wave C budget and goldens

**Goal.** As A7, for Wave C.

**Evidence (confidence: measured).** Decision 18. A7's instrument, views and ceiling.

**Approach.** `measure.ps1 -Launches 4`, unchanged, on an idle machine with gaming mode off. Since the
A7 views frame little of the ground Wave C dresses, three more views over it, three launches each,
Rich against Original at the same settings: C1's fields from 300 m (`--pos=-3000,430,-3900
--lookat=-1200,128,-5000`), C1's grassland 45 m up (`--pos=-3700,175,-4500 --lookat=-5800,128,-4650`)
and C3's palm transitions close (`--pos=-6090,35,-3290 --lookat=-6208,15,-3460`). The goldens stage
re-pinned by hand for the shots Wave C moved.

**Result (Wave C).** Median (range of the launch medians), four launches a case:

| Chapter | Case | `gpu_ms` | `render_cpu_ms` | `proc_ms` | draws | prims |
|---|---|---:|---:|---:|---:|---:|
| C1 | Faithful | 0.70 (0.70-0.74) | 0.29 (0.28-0.30) | 1.26 (1.24-1.29) | 240 | 88,596 |
| C1 | Enhanced Original | 5.95 (5.94-5.96) | 0.47 (0.45-0.49) | 1.29 (1.27-1.37) | 421 | 322,531 |
| C1 | Enhanced Rich | 6.03 (6.02-6.04) | 0.54 (0.49-0.63) | 1.43 (1.26-1.66) | 497 | 461,289 |
| C1B | Faithful | 0.65 (0.65-0.65) | 0.34 (0.33-0.48) | 0.19 (0.18-0.21) | 211 | 2,452 |
| C1B | Enhanced Original | 3.51 (3.50-3.53) | 0.51 (0.49-0.69) | 0.21 (0.21-0.25) | 282 | 272,580 |
| C1B | Enhanced Rich | 3.51 (3.50-3.52) | 0.51 (0.49-0.73) | 0.21 (0.21-0.24) | 286 | 274,074 |
| C2 | Faithful | 0.86 (0.85-0.87) | 1.13 (1.08-1.22) | 0.57 (0.56-0.61) | 1,474 | 308,853 |
| C2 | Enhanced Original | 2.99 (2.99-3.00) | 1.26 (1.22-1.71) | 0.61 (0.58-0.70) | 1,606 | 485,367 |
| C2 | Enhanced Rich | 3.00 (2.99-3.01) | 1.55 (1.21-2.07) | 0.66 (0.59-0.81) | 1,606 | 485,367 |
| C3 | Faithful | 1.35 (1.35-1.36) | 0.97 (0.80-1.34) | 0.78 (0.72-1.00) | 1,061 | 19,173 |
| C3 | Enhanced Original | 4.28 (4.27-4.29) | 1.18 (1.06-1.95) | 0.77 (0.72-1.11) | 1,211 | 287,593 |
| C3 | Enhanced Rich | 4.30 (4.30-4.31) | 1.14 (1.11-1.15) | 0.75 (0.73-0.83) | 1,255 | 375,769 |
| C4 | Faithful | 0.74 (0.74-0.74) | 0.39 (0.39-0.50) | 0.48 (0.47-0.62) | 382 | 205,459 |
| C4 | Enhanced Original | 2.70 (2.70-2.71) | 0.48 (0.47-0.53) | 0.48 (0.47-0.50) | 462 | 59,806 |
| C4 | Enhanced Rich | 2.70 (2.69-2.73) | 0.48 (0.48-0.50) | 0.47 (0.46-0.48) | 476 | 61,870 |

Off the A7 views, three launches a case, Rich against Original:

| View | `gpu_ms` Original | `gpu_ms` Rich | `render_cpu_ms` Original | `render_cpu_ms` Rich |
|---|---:|---:|---:|---:|
| C1 fields, 300 m | 2.62 (2.61-2.63) | 2.69 (2.68-2.70) | 0.43 (0.43-0.45) | 0.55 (0.46-0.59) |
| C1 grassland, 45 m | 4.71 (4.70-4.72) | 4.73 (4.73-4.74) | 1.39 (1.27-1.42) | 1.33 (1.22-1.42) |
| C3 palm transitions | 5.25 (5.24-5.25) | 5.39 (5.39-5.39) | 1.16 (1.10-1.90) | 1.20 (1.16-1.76) |

**Wave C costs at most 0.14 ms of GPU, inside its 0.25 ms share.** At the A7 views it adds 0.08 ms on
C1 (the grassland and hedges in frame: 497 draws against Wave B's 469, 461k prims against 335k) and at
most 0.02 ms elsewhere; C1B's bushes and C4's treeline add 4 and 14 draws and no measurable time, and
C2 is unchanged. The worst view is C3's palm transitions, 0.14 ms over Original, which also holds B15's
crowns. CPU: C1's fields add up to 0.12 ms of `render_cpu_ms` (the 32k added cards' culling), every
other view moves inside its noise (C2, unchanged, moves 0.30 ms between identical cases). Wave D keeps
the remaining 1.8 ms of the 2.0 ms GPU ceiling.

**Model recommendation.** A mid-tier model suffices, as A7.

**Verify.** Every launch exit 0. Goldens: Wave C moved exactly five Enhanced shots, `c1-lake-enhanced`,
`c1-rocket-hit-enhanced`, `c1-cockpit-enhanced` and `c1-cockpit-xr-sim-enhanced` (C22's grassland and
hedges) and `c1b-ocean-enhanced` (C23's rock-top bushes); each rendered to one hash in its batch and its
re-render. The five hashes are re-pinned by hand (never `-RegenGoldens`), and two further goldens runs
pass 38 of 38.

**⚠ Traps.** As A7.

# Wave D, near

## D31 ◐ Scripted Blender pipeline: one generator per species, headless glTF export, renders beside the card

**Goal.** A Blender Python script per species that an agent runs headless to produce a glTF model
and a render beside the original card, for your judgement.

**Evidence (confidence: measured for the tools).** Blender 4.3, 4.4 and 5.2 are installed under
`C:\Program Files\Blender Foundation`. Decision 17. None of the three bundles Sapling Tree Gen (no
`addons_core` entry; it became an online extension in 4.2). 5.2.1 LTS runs `--background
--factory-startup` cleanly, EEVEE included, and its glTF exporter writes `alphaMode: MASK` from a
Round node on the alpha. C1's `firtree1.flt` card is a 10.026 × 17.834 m CylindricalY quad, pivot at
its base centre, textured `firtree1.tif` (128 × 128); its opaque silhouette is about 7 m wide at the
base and tapers to a point at 17.4 m, mean colour a very dark green. The model flag `lighting` is
false, so the card draws unlit.

**Approach.** Landed as the pipeline and its first species; [`tooling.md`](tooling.md), "Vegetation
models", is the reference.

- **Blender 5.2** (5.2.1 LTS): the newest installed, a long-term-support line, already the version
  `CSVM/data/cloud_puffs/render_cloud_puffs.py` names, and clean headless.
- **Location.** `CSVM/data/vegetation_models/<model>.py`, one generator per card species, plus the
  shared `veg_blender.py`. Output `CSVM/data/vegetation_models/<chapter>/<model>.glb.bin`, committed
  as the cloud puffs' `.png.bin` are: Godot imports no `.bin`, so there is no `.import` file to
  commit, and the export's `data/*.bin` filter packs it. D32 loads it with
  `GltfDocument.AppendFromBuffer`. A rerun is byte-identical. The chapter folder is there because a
  model name carries a different card per chapter (`firtree1` is 10 × 18 m on C1, 10 × 13 m on C4).
- **Generator: hand-built `bmesh` geometry** and a texture drawn with numpy in the script, so the run
  needs no download and the model holds no pixel of the original. One mesh, one material, one 256 ×
  256 atlas (fronds, a core patch, a bark patch), alpha MASK, double-sided, so an instanced draw is
  one surface.
- **Conventions.** Metres; pivot at the trunk base on the ground; Y up in the GLB (Blender's Z up,
  converted by the exporter); sized to the card it replaces. Each generator holds its card sizes and
  stops if the extracted card differs. Normals are bent out from the trunk axis and up, so the crown
  shades as a volume.
- **The render** (`--render`, which `MakeVegetationModel.ps1` passes, into `.scratch/vegetation/`):
  the card's texture on a quad at its real size beside the model, unlit as the game draws it, one
  orthographic camera and one sun, from eye level (`_side`) and from 35° above (`_above`), plus the
  model's atlas (`_atlas`). Scratch only, never committed.
- **firtree1 (C1)**: a six-sided trunk in the lower crown only, a six-sided foliage core that runs
  up to 16 m, 165 drooping clumps on a jittered golden-angle spiral (no whorls), spread by crown
  girth with a floor so the top keeps its share, their tips on the card's silhouette, and two
  crossed fronds as the leader. A clump is lit on its ridge, dark where its sides hang, with
  lighter specks of new growth toward its tip. **1400 triangles**, 2323 vertices,
  8.1 × 7.8 × 17.39 m, a 137 KB GLB.

**Model recommendation.** Opus for a new species: the work is a look iterated against renders, and
D33 adds palms and bushes the fir's builder does not cover. A mid-tier model can rerun or retune an
existing generator.

**Verify.** `.\MakeVegetationModel.ps1 -Species firtree1` exits 0 with Blender headless and no Godot
launch; the GLB's JSON reads one mesh with one primitive, `alphaMode: MASK`, `doubleSided: true`,
POSITION y from 0 to 17.39; a second run writes the same bytes. The look is yours: the `_side` and
`_above` renders of `firtree1` beside its card.

**⚠ Traps.**
- The models are new art made by the remake; nothing traced from the original's textures ships as a
  model texture without a copyright check. The card texture goes into the scratch render only.
- The card draws unlit and the model is lit, so the render's brightness match holds for its sun and
  sky only; D32's handover must be judged in game.
- A card name is not a species across chapters: size and texture differ per chapter, so a
  generator gains a `CARDS` entry (and its own look check) per chapter.

**Ruling (yours, from the first renders).** The pipeline stands; the look iterates. The next pass
makes the crown shaggier and less tiered and lighter, with the card's yellow-green highlights, and
hides the bare trunk in the upper half. On the second pass's renders: good enough to carry D32's
handover, judged again in game.

**Verified.** <pending orchestrator run>

## D32 ☑ Near-radius 3D draw with a dither handover to the card

**Goal.** Within a near radius a listed stamp draws its 3D model, dithering over to the original card
beyond it; from altitude nothing changes. Over C3's painted canopy, where no card stands, the near
models fill the 0 to 700 m band inside B15's crown clumps (B15's ruling).

**Evidence (confidence: lead-only).** Decision 16; X-Plane 12 draws trees the same way (developer
docs, "building custom 3D trees"). The card fade already dithers (`csky_clutter_dither_keep`,
`Clutter.cs:850`).

**Approach (as built).** `Mech3/VegetationModels.cs` reads and shades a species' model;
`ClutterInstances` draws it over the card's own Rich stamps; [`formats/model-packs.md`](formats/model-packs.md),
"Vegetation models", is the pack reference.

- *Gating.* Rich only (Decision 3). A kind's model cells draw exactly while its Rich variant does, so
  Faithful, Enhanced Original and a live move or switch away from Rich draw none, and a move back draws
  the same cells. The 3D Vegetation word at `off` draws none either, the A/B for captures and
  `measure.ps1 -Modes nomodels`.
- *Source.* `<chapter>/<card>.glb` from a model pack (`HdModels.Vegetation`, a chapter key only), else
  D31's shipped `vegetation_models/<chapter>/<card>.glb.bin`, imported at load with
  `GltfDocument.AppendFromBuffer`: the first mesh, its first surface's base-colour texture, the clear
  texels bled to the solid mean before the mips (black clear texels darkened the model with distance).
  A file that fails to import passes to the next source. A species with no model gets no `Near`, its
  card's band stays zero and the card draws as before. The species with a model are D33's table; a
  scrub-set twin of a modelled card (C21's added stamps on the grassland) takes the model too.
- *Band.* A registered word setting, 3D Vegetation (`Utils/VegetationModelsSetting.cs`, the VIDEO
  page's row under Vegetation, `--vegetation-models=off|near|far`, saved as `vegetationModels`): `near`,
  the default on every machine and under `--det` (the Deck and Linux included until D36 measures them),
  is 150 to 250 m; `far` is 250 to 400 m; `off` draws no model. Plain metres, so no View Distance moves
  the cost. Near sits inside C1's card fades (500 m at the nearest, at the highest View Distance) and
  the clump handover (1000 m), and short of a 300 m view's nearest stamp: the 300 m capture is
  pixel-identical with and without models. Far reaches a 300 m view and covers about 2.5 times Near's
  area. A live change recuts with no rebuild: the cards and models take the word's band, the model cells
  its range, and `off` drops the cells and hides the edge copies. The words follow the existing
  settings' plain ladder (Shadow Quality's `off`, View Distance's `far`).
- *Handover.* The card's Rich material fades it by the band's share (`ModelShare` in
  `RichVertexLines`, on `VegetationModels.BandParam`, zero on a card with no model), and the model
  keeps exactly the 4x4 dither cells the card gives up (`csky_clutter_dither_keep` of the card's far
  fade, less the cells the card keeps), so a stamp never draws both or neither; the card's own far fade
  bounds both, so at a low View Distance they fade out together.
- *Per stamp.* The model stands on the card's Rich transform: its origin (jitter included), its mirror
  (the basis' X sign) and its instance colour (tint and brightness). It draws no `scale_range`, as the
  card draws none (A3's trap), so the swap does not change size. A card has no yaw, so the model turns
  by a hash of its planted point. It sways: the card's breeze lines (`ClutterBuilder.LeanLines`) at the
  card's phase, leaning about its base, so the swap does not jump and no still tree stands among
  swaying cards.
- *Lighting.* Unlit like the card (D31's trap: a lit model meets an unlit card only at one sun). A grade
  meets the card in brightness and keeps the model's own hue: the ratio of the card texture's mean
  luminance to the model's, each colour channel allowed at most 10 % off it (`MaxChroma`), held to
  0.25-4. Both means read the top half of the tree (`CrownShare`), the crown on every species, so the
  trunk and C3's sand patch at a palm card's foot drop out; the model's texels are weighted by the
  world area its triangles cover, since an atlas mean counts patches the mesh barely uses. A grade per
  channel was tried first and broke the palms: their card and model means measured different mixes
  (sand patch, trunk, the card's dark fringe), and blue gains up to 3.7 turned `palmtree2` yellow with
  a lilac trunk and `palmtree1` grey-white. On top of the grade the model takes the card's own
  sun-side gain by screen half (`SunLine`, `RichFragmentLines`, against half the card's width), so the
  gain lands as on the card from every view. A gain read off the model's normals (option C in the
  montage) brightens it seen with the sun behind and darkens it against the sun, where the card does
  neither. The grade is computed at load, so a pack's model or a retuned file meets its card's
  brightness with no number to tune; its hue is the file's, so a model's colours are tuned to its card
  in its generator (D33, "Colour in game"). Grades: C1 `firtree1` (0.342, 0.335, 0.337),
  `firtree2` (0.327, 0.329, 0.361), `dougfirtree1` (0.301, 0.309, 0.308), `bush1` (0.739, 0.778,
  0.853), `bush2` (0.518, 0.59, 0.6); C4 `firtree1` (0.299, 0.294, 0.295), `firtree2` (0.302, 0.303,
  0.334), `dougfirtree1` (0.293, 0.3, 0.3), `spruce` (0.383, 0.385, 0.373); C2 `palmtree1` (1.062,
  0.882, 1.062), `palmtree2` (0.6, 0.523, 0.6), `palmtree3` (0.665, 0.744, 0.812), `spruce` (0.758,
  0.767, 0.713), `brush1` (1.171, 1.014, 1.117), `brush2` (0.641, 0.62, 0.661), `brush3` (0.621,
  0.621, 0.615), `brush4` (0.667, 0.678, 0.734); C3 `palmtree1` (0.651, 0.532, 0.651), `palmtree2`
  (0.603, 0.527, 0.603), `palmtree3` (0.71, 0.789, 0.868).
- *Shadows.* None, cast or received, as the card. A shadow that ends at 250 m draws a ring on the
  ground where the cards cast none, and casting re-renders the models into each Ultra cascade.
- *The draw.* `ClutterInstances` cuts the Rich variant again into `CellM` (256 m) cells, one MultiMesh
  of the model mesh each, ranged at the word's far edge plus the cell's radius. Godot's own range and frustum cull
  pick the cells per camera, so splitscreen panes and the XR stereo pair need no per-frame CPU work;
  inside a reached cell a stamp outside the band collapses in the vertex stage. The cells are cut once
  at the first Rich draw and kept, and stand under `vegetation_models` beside the clutter root, so the
  crater walk and the suites' censuses read each stamp once. Not solid: no collider exists. A cell
  exists only where its species stands: C1 cuts 5,635 model cells over its ten modelled kinds
  (`firtree1` 1,416, `dougfirtree1` 1,393, `firtree2` 1,417, the bushes' cards 54 to 60 each, and
  C21's added `bush1` 489, `bush2` 490 and `firtree1` 201), logged as `vegetation models:` at the
  build, and the map edge 348 model copies at its corner.
- *No instance uniform.* The model shader declares none: it reads the card's fog as always on and
  takes no debug tint, which no overlay ever set on a model. Godot gives every node whose shader
  declares one a block of 16 slots in a global buffer of 65,536 blocks, so a cut of the cells took
  5,635 blocks, against 4,470 for the rest of the build. A suite runs in one engine frame, where a
  lab re-bake's queued-free cells keep their blocks until the frame ends, and the battery's first
  overflow came about eight re-bakes into `vegetation-lab`. The cell count now costs no block, so
  the cut holds for every species D33 adds; `vegetation-models` fails a C1 build whose model nodes
  declare one, or whose nodes that do pass a quarter of the buffer. A C1 build at the A7 view
  carries 5,645 more scene nodes with the models than without (15,555 against 9,910, the cells and
  their ten groups) and its `render_cpu_ms` does not move, so the cells stay at 256 m. A 512 m cell
  would cut the nodes about four times, and by its reach (the band plus half its diagonal) draw
  about twice the collapsed instances in the vertex stage.
- *Following the card.* Every stamp write reaches the model cells, so an activation hide, a crater and
  a show move the model with its card, through live switches both ways. A lab re-bake re-cuts them; an
  added kind's models stand under a child rebuilt with the added kinds. Past the map edge each Rich
  card copy gains a model copy, one node per edge cell and kind. A kind first listed by a lab edit takes
  no model until the next load.
- *C3.* Its beach palms take their models by file alone, as above. Over the painted canopy no card
  stands, so the near models there are B15's, built with the clumps (`CanopyClumps`):
  - *Source.* `CanopyClumps.CrownModels` (pure) stands crown models in the clumps' own mask cells, never
    on a world grid of their own: a 3 × 3 grid of slots a 32 m cell, each kept at the cell's share of
    `PaintedTreesPerCell` (6, B15's crown rate) over the cell's painted share, jittered inside its slot.
    A crown stands only where the nearest painted sample point is canopy, so none stands on the beach
    or a transition texture, and at the painted surface there (`CanopyMask.SurfaceAt`, bilinear
    between the bake's 8 m samples, which a painted bake now keeps). Scale 0.85 to 1.15, mirror and
    instance colour (the C3 kinds' brightness and tint spread) are hashes of the cell, as the clumps'
    are. C3 stands 129,531 crowns in 471 cells of 256 m: 3,354 inside 700 m of the A7 view (beach and
    sea fill half of it) and 6,781 at the low view over the jungle (`--pos=-5450,170,-3550`).
  - *Band.* `ModelBand`, 700 to 1000 m: inside 700 m the models alone draw (the clumps there are sunk
    to nothing by `grow`); over 700 to 1000 m the clumps keep only the 4x4 dither cells the models give
    up (`v_model`, `csky_clutter_dither_keep` of the same share on both sides), while `grow` still
    raises them, so at 1000 m they stand at half height (8 m front, 12.8 m back row) against the
    model's 6 m. The map edge's clump copies keep no band, since no model stands past the edge.
  - *The word.* Near and Far draw the same canopy band: the crowns start rising at 700 m on every
    word, so a farther model band would replace crowns that already carry the volume, at the square
    of the reach in instances. Off hides the crown models and zeroes the clumps' band, so B15 draws as
    it did. A live change follows through `Field.FollowModels` with no rebuild; Original, Faithful and
    a live move off Rich drop the clumps and their models together, and `--canopy-clumps=off` draws
    neither.
  - *The draw.* D32's cell scheme: one MultiMesh of the crown model per 256 m cell under the clumps'
    root (never the clutter root), ranged at 1000 m plus the cell's half diagonal, Godot's own range
    and frustum cull per camera, no collider, no shadow and no instance uniform (the model shader's
    `canopy` variant: no card fade, its instance basis scales it). Graded to the B15 crowns' mean
    colour over their top half (`VegetationModels.ForMean`), grade 0.354, 0.329, 0.372.
  - *Cost* at 5120x1440 on an idle machine, three launches each, `off` against `near` (Off also
    drops the palm models), with the 150-triangle crown: at the A7 view `gpu_ms` 4.45 against 4.35,
    1,299 against 1,255 draws, 1,247,983 against 375,769 primitives, 7,933 against 7,385 scene
    nodes, `render_cpu_ms` 1.20 against 1.10; at the low view `gpu_ms` 3.69 against 3.08, 1,362
    against 1,300 draws, 1,879,830 against 386,580 primitives, `render_cpu_ms` 1.19 against 1.12.
    The primitives are the crowns inside 1000 m in the frustum. The first crown, 210 triangles,
    read 4.56 and 3.88 ms at the same views (2,477,130 primitives at the low view).

**Ruling (yours, from the montages).** Brightness A: graded to the card with the card's sun-side gain.
No shadows. The model sways with the card. The band: 150 to 250 m by default, with an option to push it
out, which became the 3D Vegetation setting above (Off, Near, Far). On the palms in game the per-channel grade turned them off-hue, so the grade matches brightness only and keeps each model's hue; the models are recoloured toward their cards in their generators (D33). C3's painted canopy inside 700 m takes D33's `canopytree` crowns on B15's clump cells (*C3* above).

**Ruling (yours, from the montages).** C3's crowns: the greener palette (option C), 6 crowns a 32 m
cell as built (not 9), their top 6 m over the painted canopy as built (not 3 or 10 m), and for the
cost the lighter crown, about 150 triangles with five-sided domes, over the same 700 to 1000 m band.
Montages: `.scratch/c3canopy/` (`montage-off-on`, `-colour`, `-height`, `-density`, and
`montage-final.png`, off against the ruled crown before and after the lighter domes).

**Model recommendation.** Opus: the item crosses the clutter's drawn layouts, the map edge, the shader
text the Rich card shares and a look iterated against captures.

**Verify.** Units: `VegetationModelsTests` (the card mean is linear over solid texels; the model mean
reads only the texels its triangles cover, weighted by area; both read only the crown's top half; the grade meets the card's luminance, keeps the model's hue within the chroma clamp, and falls back to one) and
`HdModelsTests.AVegetationModelIsReadUnderItsChapterKeyAlone`, `VegetationModelsSettingTests` (the
ladder, `near` on every machine, the flag and the file keep a known word, each word's band) and the
options tests (`OptionsChoicesTests`, `OriginalOptionsTests`: the row is dead under Original and walks
between Vegetation and VR Mode). Engine: `vegetation-models` (C1 Enhanced
at rich: every card with a model shipped for C1 takes it and its card the band, every other card none;
no model node declares an instance uniform and the build's nodes that do stay under a quarter of the
buffer; the cells
stand outside the clutter root with no collider, ranged inside the band, drawing exactly the card's
Rich stamps; the shader keeps the cells the card gives up; a crater, a hide and a show reach the model;
a live move to original and a switch to Original draw none and back the same, the map edge copies them
under rich alone; a live move to `off` draws none with every card band zero, one to `far` ranges the same
cells past 400 m with the far band, and back to `near` restores them; a session at original draws none; a pack's
`C1/firtree1.glb` replaces the shipped file), with `clutter-rich-variation`, `graphics-vegetation-quality`,
`canopy-clumps`, `vegetation-added-stamps`, `vegetation-lab`, `clutter-activation`, `clutter-cells`,
`graphics-live-switch`, `crater-carve`, `display-det-guard` (the new saved word is dropped under
`--det`) and the VIDEO page's menu suite passing. C3's crowns: units
`CanopyMaskTests.APaintedBakeReadsItsSurfaceBetweenItsSamples` (the surface between samples, the painted
point and share, a stamp bake's ground) and `CanopyClumpsTests.CrownModelsStandOnThePaintedCanopyAtItsCrownRate`
and `CrownModelsHandOverInsideTheClumpsRise`; engine `canopy-models` (a C3 Enhanced flight at rich: the
crowns stand under the clumps' root outside the clutter root, no collider, no instance uniform, every
one on the painted surface, cells ranged past 1000 m; the clumps keep the dither cells the models give
up over the band; Off hides them and zeroes the band, Far and Near draw the same nodes; original,
Original and back; a session at original draws none) and `canopy-clumps` (C1's card forest stands no
crown model). Look: `.scratch/d32/` (montages of the low
view among firs, the handover band, three band radii and the 300 m view; the brightness and gain
options), judged by you. Budget: a first A/B, three launches a case at 5120x1440 with another
worktree's battery running (so `gpu_ms` reads about four times C24's and its ranges overlap): at
`measure.ps1`'s C1 view `rich` against `nomodels` reads 505 against 497 draws and 511,689 against
461,289 primitives, `gpu_ms` 25.7 against 26.5 and `render_cpu_ms` 0.85 against 0.81; at the low view
among firs (`--pos=-9230,206,-6235 --lookat=-9330,196,-6335`) 405 against 395 draws and 548,823
against 466,223 primitives, with `gpu_ms` inside its noise. With D33's five C1 models the same
views read 527 against 497 draws and 780,853 against 464,801 primitives, and 429 against 395 draws
and 666,111 against 470,049 primitives. D37 measures it on an idle machine with
`measure.ps1 -Modes rich,nomodels`. Goldens: rendered at their manifest args with the models on and
off, `c1-lake-enhanced` moves 344 pixels by at most 2 levels, `c1-cockpit-xr-sim-enhanced` 12 and
`c1-rocket-hit-enhanced` 5 at that bound, and `c1-cloud-deck-enhanced` and `c1-cockpit-enhanced`
none; D37 re-pins.

**⚠ Traps.** Not solid (Decision 15): give the 3D draw no collider.
- Never parent the model cells under the clutter root: the crater walk would collapse a model as a
  card of its own and every census would count each stamp twice.
- Never light the model with the scene's lights or by its normals alone: the card is unlit and gains by
  screen half, and the handover shows wherever the two disagree.
- Bleed the model texture's clear texels before its mips, or the model darkens as it recedes into the
  band.
- Never give the model shader an instance uniform: every model cell would take a block of Godot's
  global buffer, and the cell count grows with each species.

## D33 ☑ Look-alike models for every listed card species

**Goal.** A Blender model per listed card species: C1's firs and bushes, C2's spruce, palms and
brushes, C3's palms, C4's firs.

**Evidence (confidence: measured sizes).** Card sizes are in the survey's FINDINGS.

**Approach.** D31's pipeline per species, judged beside its card.

*The palms (as built).* `palmtree1`, `palmtree2` and `palmtree3` are three species, not one at three
sizes: a date palm with a thick scarred trunk and a dark crown lit in pale green (7.589 × 13.189 m,
`palm1`), a coconut palm with a slender ringed trunk and an open deep green crown over nuts (5.744 ×
9.983 m, `palm2`) and a weeping palm with a red-brown trunk and dead red-brown fronds under a
grey-green crown (5.744 × 9.983 m, `palm3`). Each has its own generator holding its numbers; the
shared builder is `veg_palm.py`, beside `veg_blender.py`.
- *One model for C2 and C3.* Each card has the same size on both chapters, and C3's texture is
  C2's at half the resolution (64 × 128 against 128 × 256), with a base patch added on `palm1` and
  `palm3`. Each generator has a `CARDS` entry per chapter and its own render per chapter, and
  writes the same bytes to `C2/` and `C3/`.
- *Geometry.* A tapered trunk (six or eight sides), a bulb where the frond bases meet, and fronds:
  each one a strip folded down along its rachis, leaving the crown at an elevation from 88° down
  to the horizon or below and bending down progressively along its length. The top fronds stand
  slim and nearly straight in a tuft, and the ones below arch over and weep. `palm3` adds a skirt of
  dead fronds hanging down the trunk and draws its lowest third of fronds dead; `palm2` adds six
  nuts. The trunk base is the pivot; `palm2`'s trunk leans 0.45 m, because the card's trunk stands
  0.5 m off its centre under a centred crown.
- *Texture.* Leaflets drawn either side of a rachis, slanted toward the tip, their tips on an
  envelope so the frond tapers to a point, cut by the alpha with gaps between them; a bark patch
  (leaf-base crescents or rings), a core patch and a nut patch. The trunk and bark colours are read
  off the card's trunk texels; the frond colours are set in game ("Colour in game" below). A frond
  takes one palette with a narrow spread, so the crown keeps one coherent colour (B15's rejection
  of the palm cards for colour variation). `palmtree1`'s second strip (`frond_b`) is its lit
  fronds: every other frond draws nearly black-green with few highlights and the rest mostly in
  the pale green highlight, so the crown carries the card's dark body and whole fronds catching
  the sun.
- *Not modelled:* the yellow-green base patch on the cards. The ground under the stamp supplies it.
- *No clump tiles.* No chapter builds palm forest (the palms are not `mass`, and C3's painted
  canopy clumps are cut from its canopy textures), so the palm generators write no clump tiles.

| Model | Chapters | Model size (w × d × h, m) | Triangles | GLB bytes |
|---|---|---|---|---|
| `palmtree1` | C2, C3 | 8.15 × 8.16 × 12.99 | 992 | 109,996 |
| `palmtree2` | C2, C3 | 5.65 × 6.41 × 9.90 | 546 | 83,672 |
| `palmtree3` | C2, C3 | 6.23 × 6.85 × 9.78 | 1,234 | 121,980 |

*C1's firs and bushes (as built).* `firtree2`, `dougfirtree1`, `bush1` and `bush2`, and `firtree1` on
C4. Two more shared builders: `veg_fir.py`, `firtree1`'s builder with its numbers moved into a spec
(a key a fir leaves out takes `firtree1`'s value; `firtree1`'s C1 files rebuild byte for byte), and
`veg_bush.py`.
- *Per chapter.* C1B's `dougfirtree1`, `bush1` and `bush2` are C1's cards (same texture, same size),
  so each generator writes the same bytes to `C1/` and `C1B/`. C4's `firtree1`, `firtree2` and
  `dougfirtree1` show the same trees as C1's at other proportions: `firtree1` 10.0 × 13.2 m against
  10.0 × 17.8, `firtree2` 15.1 × 13.2 against 10.7 × 20.9, `dougfirtree1` 10.0 × 17.2 against
  10.0 × 22.5. Each takes a C4 `CARDS` entry, which builds the crown to that card's width and
  height from the same silhouette fractions, so the model's outline is the card's. A uniformly
  scaled C1 model would stand narrower than the card (`firtree1` 7.4 m against 10 m). The spruce
  and C2's brushes are below.
- *`firtree2`.* A dense olive cone on a dark trunk bare to 0.11 of the height, its widest skirt to
  0.36, then a straight taper to a rounded top. Its 190 clumps droop at 0.6 of `firtree1`'s, so the
  skirt hangs near the card's level edge, and are lit nearly evenly across, so the crown reads as
  one fuzzy mass rather than in layers; more highlight specks and a yellower palette than
  `firtree1`, as the card's body (sRGB 30, 34, 19) and top tenth (68, 75, 44) are.
- *`dougfirtree1`.* A stout trunk bare to 0.26 of the height, green with moss as the card's is (it
  takes the needle patch, not bark), then 90 clumps gathered into eight whorls (`tiers`), longer
  and flatter than `firtree1`'s, so each tier reads as a broad plate with the trunk between tiers.
  Strong shade across a clump with its top lit; a thin core above 0.45 only. Its palette is set
  from its own card as `firtree1`'s is ("Colour in game" below); the two cards' colours are within
  a few levels. The card's grass tuft at the base is left to the ground.
- *The bushes.* A few stems, and leaf clusters scattered through the card's silhouette, their
  outer edges on it: each cluster two crossed quads, one facing out and one standing across, on
  one of three leaf cells (lit, plain, shaded) chosen by its depth and height. The normals point
  out from the bush's centre, so it shades as one volume. Leaf colours come off each card's darkest
  tenth, body and lightest tenth. `bush1` (vase-shaped, dense, low contrast) takes 150 small
  clusters, those above 0.8 of its height spread over the whole top and facing up (`cap`), so the
  top is wide and flat, and a dark leaf core so it does not read see-through; `bush2` (small, blue-green, open) draws its leaves in rosettes
  with holes between them and has no core. A top cluster sits low enough that its leaves end at
  the card's height.
- *Clump tiles.* The firs write tiles as `firtree1` does, (3, 2) trees, with a `clump_grade` per
  chapter that puts each tile's mean within 0.3 sRGB levels of its card's solid mean (C1 `firtree2`
  30.9, 34.9, 19.9; `dougfirtree1` 20.8, 28.7, 16.0 on every chapter). The bushes are not forest
  and write none. By `analysis/bl-305-clutter-uv/_raw_output.txt`'s C1 census (`firtree1` 4,145,
  `firtree2` 2,580, `dougfirtree1` 2,267, bushes 311 of 9,303 stamps) each new fir carries about a
  quarter of the forest stamps, well over the twelfth B16's `Slots` needs. A C1 Rich build logs
  `firtree1`, `dougfirtree1` and `firtree2` in two slots each of six and no composed slot, so the
  bushes no longer reach the clumps. A clump carries 2.5 trees against 3.17, and C1 stands 15,055
  clumps against 11,857 at the same trees per area. At B14's 300, 600 and 700 m poses 3.2 to 4.0 %
  of the frame moves by more than 2 levels, all on the far hills, the moved pixels' mean sRGB 2.6
  to 3.8 levels darker (`.scratch/d33/clumps-montage.png`). C1B and C4 build no clumps by default (`DefaultOn` names C1 and C3), so
  their tiles are read only under `--canopy-clumps=on`.

| Model | Chapters | Card (w × h, m) | Model size (w × d × h, m) | Triangles | GLB bytes | Clump tile |
|---|---|---|---|---|---|---|
| `firtree1` | C4 | 10.03 × 13.19 | 8.05 × 7.75 × 12.86 | 1,400 | 137,948 | yes |
| `firtree2` | C1 | 10.72 × 20.88 | 10.21 × 10.14 × 20.36 | 1,600 | 148,792 | yes |
| `firtree2` | C4 | 15.14 × 13.19 | 14.39 × 14.25 × 12.86 | 1,600 | 147,864 | yes |
| `dougfirtree1` | C1, C1B | 10.03 × 22.47 | 6.97 × 7.30 × 21.91 | 776 | 99,104 | yes |
| `dougfirtree1` | C4 | 10.03 × 17.15 | 6.97 × 7.30 × 16.73 | 776 | 99,232 | yes |
| `bush1` | C1, C1B | 5.58 × 5.06 | 5.32 × 5.24 × 4.97 | 682 | 107,488 | no |
| `bush2` | C1, C1B | 3.69 × 3.35 | 3.73 × 3.81 × 3.32 | 192 | 59,440 | no |

*C3's crown model (as built).* `canopytree.py` builds `canopytree`, the crown D32 stands over C3's
painted jungle canopy where no card exists. It is self-contained (no shared builder) since it has no
card to match.
- *Geometry.* A crown and nothing below it: a top dome (radius 3.6 m) and a ring of five lower domes
  (2.4 to 3.0 m, 3.5 to 4.3 m out), each five-sided with rings at 50, 90 and 130 degrees from its top
  and every rim vertex pushed up to 16 % in or out, so no dome outlines a regular polygon. No trunk:
  the crown stands sunk in the painted canopy, which hides what lies under it. The pivot is the lowest
  point, as every model's is; **150 triangles**, 11.60 × 12.32 × 6.55 m, a 98 KB GLB. Seven-sided
  domes (210 triangles) read rounder up close and cost 0.19 ms more GPU at the low view.
- *Texture.* Four 128-pixel discs on one 256 atlas, one a dome, mapped by the polar angle from the
  dome's top: lit at the centre and darkening to the rim (the skirt), under soft lighter crownlets and
  a leaf grain a texel or two across, as the painted canopy reads up close. The top dome takes the lit
  disc, the ring the three plainer ones. Drawn unlit, the texture carries all the relief.
- *Height.* Its top stands `ModelRiseM` (6 m) over the painted surface at scale 1. The painted surface
  is the treetops seen from above, so a crown standing on it is an emergent crown over the canopy, as
  B15's crowns are; 6 m sits under where the rising clumps stand at the band's far edge (8 m), so the
  dither hands over to taller crowns rather than shorter ones, and the domes' skirts still sink into
  the canopy on a slope. 3 and 10 m are in `.scratch/c3canopy/montage-height.png`.
- *Colour.* D32 grades it to B15's crowns' mean, cut from the same textures, since the crowns are what
  it hands over to and they sit within a few levels of the lit painted ground on the hills. Forest1's
  own bands by D33's rule (sRGB 35, 51, 26; 50, 70, 38; 92, 113, 66) drew greyer than the lit painted
  ground's yellow-green up close, so the palette is greener and yellower (option C: shade 0.11, 0.20,
  0.06, body 0.17, 0.29, 0.09, highlight 0.40, 0.50, 0.20 in sRGB), set at 0.85 of those levels. At
  full levels the grade's luminance ratio was 0.240, under its 0.25 floor, so the crowns drew about
  4 % brighter than their target; at 0.85 it lands at 0.354, 0.329, 0.372 and they draw at it.
- *No clump tile*: the clumps over painted canopy are cut from the canopy textures (B15).

| Model | Chapters | Model size (w × d × h, m) | Triangles | GLB bytes |
|---|---|---|---|---|
| `canopytree` | C3 | 11.60 × 12.32 × 6.55 | 150 | 98,264 |

*Ruling (yours, from the montages).* `canopytree` takes option C, the greener palette; 6 crowns a
32 m cell; its top 6 m over the painted canopy; and the lighter crown, about 150 triangles with
five-sided domes, for the cost, over the same band. `.scratch/c3canopy/montage-final.png`.

*The spruce and C2's brushes (as built).* `spruce` on C2 and C4, and C2's `brush1` to `brush4`.
C2 lists only vegetation kinds (the palms, `spruce` and the brushes; no suburb, building or car),
so nothing solid takes a model. A C2 Rich build logs 7,009 model nodes over its 21 modelled kinds,
`spruce` 816, `brush2` 485 and the other brushes about 1,200 each in their main group; C4 logs
1,940 for `spruce` in three groups and 259 for its added treeline stamps.
- *`spruce`.* `veg_fir.py` with whorls: a narrow cone of nine drooping branch tiers swept to the
  ground at 0.05 of the height, widest at 0.3, on a short trunk, 110 clumps lit across their tops
  and dark beneath with yellow-green tips, and a thin core so the shade between tiers shows as on
  the card. C2's and C4's cards are one picture at one size (5.57 × 9.00 m), but C4's texels are
  about half as bright and a few degrees bluer, so `colours` takes an entry per chapter and the
  two files differ in their texture alone.
- *`spruce`'s clump tiles.* Both chapters write them, (3, 2, 4) trees, though neither builds
  clumps by default (`DefaultOn` names C1 and C3). Under `--canopy-clumps=on` a C4 build gives
  `spruce` three of the six slots (it carries 49,428 of the census' 88,630 stamps), beside one
  each for `firtree1`, `firtree2` and `dougfirtree1`, so no slot is composed; a C2 build gives it
  one slot and composes the other five from the palm and brush cards. Each `clump_grade` puts
  its tile's mean on its card's solid mean (C2 53.5, 65.5, 29.0; C4 28.2, 34.8, 17.3).
- *`brush1`.* An agave, built by `veg_rosette.py`: 24 stiff, broad, pointed leaves fanning up and
  out from a small base, each a strip folded along its middle on a quadratic curve to a tip on the
  card's silhouette, its texture darker down the fold and pale at the margins.
- *`brush2` and `brush4`.* `veg_bush.py` shrubs with no core and leaves sparse enough to leave
  holes: in game the cards show the sand through their leafage, and a dense ball read darker and
  more solid than the card. `brush2` is a round shrub on three short stems; `brush4` a wide dome to
  the ground.
- *`brush3`.* A grass tussock, built by `veg_tussock.py`. Its texture is a fan of blades: each
  rises nearly straight from the base on its tip's side to a tip on the card's silhouette, past it
  by up to 15 % (`over`) or inside it, so the blade ends trace the wide mound loosely, spikes stand
  above the body as on the card, and the outer blades reach the ground. Three upright quads crossed
  through the centre carry the dense fan (260 blades) at the card's width and height and give the
  outline from every side; 16 smaller tufts round them, each two crossed quads, give it depth, the
  outer ones on a sparse fan (110 blades) so the edge reads loose; a small dark core sits at the
  foot. The spikes past the outline make the model 6.05 m wide against the card's 5.67. The atlas is 512 × 512, as
  a blade a few centimetres wide needs. Two earlier builds read as a different plant: strips of
  blades arching from a base stood as a narrow column, and `veg_bush.py`'s leaf clusters drawn as
  thin blades read as a dark leafy mound.
- *Budget.* C2 stamps about 13,000 brushes against 6,200 palms (`analysis/bl-305-clutter-uv`'s
  census: `brush2` 6,909, `brush3` 2,588, `brush1` 2,136, `brush4` 1,699), so every brush stays
  under 350 triangles; `brush3` takes 100.

| Model | Chapters | Card (w × h, m) | Model size (w × d × h, m) | Triangles | GLB bytes | Clump tile |
|---|---|---|---|---|---|---|
| `spruce` | C2 | 5.57 × 9.00 | 5.21 × 5.03 × 8.82 | 960 | 108,112 | yes |
| `spruce` | C4 | 5.57 × 9.00 | 5.21 × 5.03 × 8.82 | 960 | 106,396 | yes |
| `brush1` | C2 | 2.37 × 2.10 | 2.26 × 2.16 × 2.01 | 288 | 51,748 | no |
| `brush2` | C2 | 2.36 × 2.08 | 2.20 × 2.27 × 2.03 | 204 | 74,232 | no |
| `brush3` | C2 | 5.67 × 5.00 | 6.05 × 6.06 × 5.00 | 100 | 181,736 | no |
| `brush4` | C2 | 5.67 × 5.00 | 4.93 × 4.92 × 4.97 | 332 | 79,144 | no |

*Ruling (yours, from the in-game montages).* `brush1`, `brush2`, `brush4` and the spruce on C2 and
C4 ship as built, with the spruce's clump tiles on both chapters. `brush3` took another pass toward
a grass tussock with visible blades, since the leaf-cluster build read as a dark leafy mound, a
different plant (`.scratch/d33/c2c4-brush3.png`). On that tussock: a raggeder edge, blade tips
running past the outline and the outer tufts thinned, so the edge reads loose like the card rather
than a smooth dome; the tussock above is that pass (`.scratch/d33/c2c4-brush3b.png`).

*Ruling (yours, from the in-game recolour montages).* The firs keep the cards' blue-green; the palms
take the greener, darker option B over the card texels' mean hue; `palmtree1`'s lit fronds are pale
green rather than yellow; the bushes stay as they were.

*Colour in game (as built).* The game draws a model unlit and grades it to its card by brightness
alone (D32), so a model shows its texture's own hue. A Blender render lights the model under a blue
sky, so colours tuned until the render matched the card drew too yellow and too saturated in game:
at the C1 handover `firtree1`'s crown read hue 75 to 84 and saturation 0.61 to 0.67 against its
card's 84 to 95 and 0.39 to 0.46, and the palms more olive than their cards' greens. The frond
and needle colours are therefore set against the card's texels, not the render.
- *The rule.* Each palette level (shade, body, highlight) keeps its own brightness and takes the
  hue and saturation of the card's crown texels at that level: the solid texels of the top half,
  2 px in from the alpha edge, sorted by luminance, the darkest level from the 10 to 30 %, the
  body from the 40 to 60 % and the highlight from the 80 to 100 % band. The model's
  area-weighted crown mean then sits within a few percent of the card's in each channel over its
  luminance (the D32 grade's own measure).
- *The firs* (`firtree1`, `firtree2`, `dougfirtree1`) follow the rule on C1's card and also sit
  darker, so the luminance grade lands at 0.29 to 0.34 on every chapter and inside its 0.25 floor;
  `firtree2` and `dougfirtree1` had clamped at 0.25 and drawn up to 38 % too bright. In game, over
  boxes inside the crowns at the handover and close up, C1's models read hue 74 to 100 and
  saturation 0.30 to 0.45 against the same boxes' cards at 72 to 97 and 0.37 to 0.48, and C4's
  hue 50 to 84 and saturation 0.31 to 0.41 against 51 to 74 and 0.31 to 0.39 (the boxes take in
  some ground). The clump tiles change with the model, and each
  `clump_grade` is refitted so the tile's mean stays within 0.3 sRGB levels of its card's.
- *The palms* go greener and darker than the rule, since a palm card's crown mean is pulled toward
  grey by its pale fringe and grey mid tones, and the rule alone left them olive in game.
  `palmtree1`'s body and lit-frond highlight are green (sRGB 0.08, 0.15, 0.08 and 0.55, 0.70,
  0.36) under the card's dark teal shade; `palmtree2` takes the card's deep green with a darker
  body (`light` 0.2) and a brighter highlight, so its contrast nears the card's; `palmtree3`'s
  live fronds are a deeper green and its dead fronds and skirt keep their red-brown. The D32 grade
  holds `palmtree1` and `palmtree2` at its chroma bound on both chapters. On the C2 palm crowns'
  green texels `palmtree2` reads hue 72 to 78 and saturation 0.62 to 0.65 against its card's 79 to
  81 and 0.53 to 0.74.
- *The bushes* keep their colours: in game `bush1` reads hue 91 against its card's 86 at the same
  saturation, and `bush2` the card's blue-green.
- *The spruce and C2's brushes* take the rule from the start: each palette level is the card's
  crown texels at that level, scaled in linear by one factor per model (1.5 for the brushes, 1 for
  C2's `spruce`, 2 for C4's darker card), which the brightness grade takes back. The grades land
  at 0.38 (C4 `spruce`) to 1.17 (`brush1`). In game, over the foliage pixels (green over red) of
  the plants inside 150 m, card against model: C2 `spruce` hue 73 to 78 and saturation 0.36 to
  0.46 against 71 to 81 and 0.36 to 0.51; C4 `spruce`, every one in the frame together, hue 79 and
  0.43 against 80 and 0.42; `brush1` 94 and 0.31 against 98 and 0.28; `brush3` 90 and 0.37 against
  92 and 0.40; `brush4` 71 and 0.55 against 67 and 0.50. `brush2`'s card reads red-brown (green
  under red), so over all its changed pixels: hue 34 to 40 and saturation 0.34 to 0.39 against 32
  to 40 and 0.38 to 0.41. The spruce, `brush3` and `brush4` cover more of their outline than their
  cards, whose holes show the ground (1.1 to 2.7 times the foliage pixels), and the brushes' cards
  draw the sand through their leafage as pale flecks the models show less of. `brush1` covers 0.6
  of its card's pixels: the card shows every leaf flat in its plane, the model many edge on.

**Ruling (yours, from the renders).** The palms: `palmtree1` takes a darker crown body with
stronger yellow highlights, closer to the card's punch (the lit-frond strip above). `palmtree2` stays
symmetric: stamps turn at random, so a lopsided crown would show its lean from every side.
`palmtree3` keeps its red skirt as rendered. The crown tops of `palmtree1` and `palmtree3`, wider than
the cards' narrow tufts, stand for now and are judged at D32's handover in game.

**Model recommendation.** Opus for a new species: the work is a look iterated against renders. A
mid-tier model can retune an existing generator's numbers against a ruling.

**Verify.** `.\MakeVegetationModel.ps1 -Species <model> -Chapter <C>` exits 0 for each model and
chapter it ships for, with Blender headless and no Godot launch. Each GLB's JSON reads one mesh with
one primitive, one material with `alphaMode: MASK` and `doubleSided: true`, and POSITION y from 0 to
the model's height. A second run writes the same bytes, and `C2/` and `C3/` hold identical files.
Rerunning `firtree1` after a change to `veg_blender.py` changes nothing but the exporter's version
string. The look is yours: each model's `_side` and `_above` renders beside its card per chapter,
and the contact sheet of all six.
For C1's firs and bushes: the same per model and chapter of the table above; a rerun of all eleven
writes every model and tile byte for byte the same; `C1/` and `C1B/` hold identical
`dougfirtree1`, `bush1` and `bush2` files; `firtree1`'s C1 model and tiles rebuild through
`veg_fir.py` unchanged but for the exporter's version string, and the palms unchanged. Look:
`.scratch/d33/c1-sheet.png` (every new model and chapter beside its card, and `firtree1` on C1 for
reference), the clump strips in `.scratch/vegetation/<C>_<model>_clump.png`, and
`.scratch/d33/clumps-montage.png` (C1 Rich with `firtree1`'s tile alone against all three fir
tiles, at B14's 300, 600 and 700 m poses). Engine: `canopy-clumps` (every tile a C1 flight takes is
its own species' shipped file, `firtree1`'s among them; a pack's `firtree1` tile replaces only
that one) and `canopy-mask`; units `ClumpTile` and `CanopyClumps`.
For C3's `canopytree`: `.\MakeVegetationModel.ps1 -Species canopytree` exits 0 headless; the GLB
reads one mesh with one primitive, `alphaMode: MASK`, `doubleSided: true`, POSITION y from 0 to
6.55; a rerun writes the same bytes. Look: `.scratch/vegetation/C3_canopytree_{side,above,atlas}.png`
(beside a forest1 swatch) and the in-game montages in `.scratch/c3canopy/` (D32).
For the spruce and C2's brushes: the same per model and chapter of their table; a rerun of all six
writes every model and tile byte for byte the same; every committed model and tile of `firtree1`,
`firtree2`, `dougfirtree1`, `bush1` and `bush2`, rebuilt after the change to `veg_fir.py` (per
chapter `colours`), writes the same bytes, and every other C2 and C4 file is unchanged. A C2 and a C4 Rich build log each new model's grade and its cells
(`vegetation models:`), and a C2 or C4 build with `--canopy-clumps=on` logs `spruce` in its slots
from its shipped tile. Look: `.scratch/d33/c2c4-sheet.png` (each new model and chapter beside its
card, side and above), the clump strips `.scratch/vegetation/C2_spruce_clump.png` and
`C4_spruce_clump.png`, and `.scratch/d33/c2c4-ingame-<view>.png`, cards only
(`--vegetation-models=off`) against models (`near`) at `--graphics=enhanced
--vegetation-quality=rich --det`: C2's desert scrub (`--pos=-100,20,-3100 --lookat=-200,8,-3200`),
A7 view, desert palms and hills among spruce (`--pos=-560,70,-5560 --lookat=-680,45,-5680`), and
C4 among firs and spruce; those show the leaf-cluster `brush3`. `.scratch/d33/c2c4-brush3.png`
shows the scrub view with cards only, that build and the first tussock, and
`.scratch/d33/c2c4-brush3b.png` cards only, the first tussock and the ragged one. Engine: `vegetation-models` and
`canopy-clumps`, which prove the mechanism on C1 and need nothing for C2 or C4.

**⚠ Traps.** Match the card's silhouette and colour, or the handover in D32 shows.
- The first node named after a card can be a placed copy without a model (C3's `palmtree2`);
  `read_card` takes the first node that resolves one.
- The palm cards carry a yellow patch of ground cover at their base. Leave it out of the model: it
  is the patch C21's ruling wants gone near the camera, and the ground supplies what is under the
  trunk.
- Shipping a tile for another C1 forest species changes B16's slot split and the far hills, so the
  Enhanced C1 goldens that show them move. `canopy-clumps` checks that every tile a C1 flight takes
  is its own species' shipped file with `firtree1`'s among them, and that a pack's `firtree1` tile
  replaces only that species'.
- A tile's grade is per chapter: C4's `firtree1` card is darker than C1's, so one grade would
  lighten C4's far forest.

## D34 ☐ Ground cover prototype: new-art grass and crop cards

**Goal.** Close-up grass and crop cover drawn from new-art cards on picked textures.

**Evidence (confidence: measured).** No grass or crop card exists in the install (survey); Decision 6.

**Approach (as built).** `Mech3/GroundCover.cs`, the Ground Cover word's `cards` (the default);
[`architecture/Mech3.md`](architecture/Mech3.md) has the module.

- *The setting.* A registered word setting, Ground Cover (`Utils/GroundCoverSetting.cs`, the VIDEO page's row
  under 3D Vegetation, `--ground-cover=off|shells|cards`, saved as `groundCover`): `cards`, the default on every
  machine and under `--det` (`GroundCoverSetting.Default`, the one place it is set), draws these cards; `shells`
  draws D35's shells; `off` draws neither. `--det` reads no saved word, so a scripted capture runs `cards` unless
  its flag says otherwise.
- *Gating.* Rich only, as the clumps: `GameSession.FollowGroundCover` builds the word's cover while
  `VegetationQualitySetting.RichOn` and drops it on a live move to original, a switch to Original or a live move to
  another word, through the session's follow with no other rebuild, so one cover draws at a time. A lab mask stroke
  builds it again under the new mask.
- *Picks.* The chapter's sidecar entry names the textures under `cover` (`VegetationEntry.CoverFor`), each `grass`
  or `crop`, or `{"grows": ..., "min_saturation": n, "max_value": m, "covers": "cards"}`. Over the 2 × 2 texels a
  card's colour averages, a texel whose sRGB saturation (max less min over max) is under n, or whose value (its
  brightest channel) is over m, grows nothing; `covers` `cards` keeps the pick off the shells (the default `both`).
  The gate and the crop row map read the authored texels (`FindImage(authored: true)`), so a texture pack never moves
  what grows; the colour reads what is drawn, an HD replacement included, averaged over the same 2 × 2 authored
  texels of ground (8 × 8 of a 4× pack). With the user's HD pack (2,173 replacements, C1's and C4's ground at
  1024 × 1024) every C1 and C4 view counts the same cells, draws and cards as without it; `.scratch/gc/montage-hd.png`
  shows C1's crop field 30 m up and C4's `slope8` with the pack. C1 keeps the prototype's picks with no
  gate: 1,351 picked triangles over 8,342 cells, and at the suite's views 60 cells, 69 draws, 62,025 crop and 37,270
  grass cards over the crop field, 168,239 grass cards (60 draws) over the meadow, 92,805 at the strip edge with
  88,155 lattice points bare. The meadow holds 6,799 more than the prototype's 161,440 because transitions are no
  longer overlays (below).
- *C4.* `cliff01-trans1` and `cliff01-trans3`, the dry grass that runs into rock at `terpat01`'s edges, grow grass
  at `min_saturation` 0.2: the dry grass reads 0.25 to 0.45 and the grey rock 0.04 to 0.18, so the rock half of
  each transition stays bare. 875 picked triangles over 3,108 cells; 30 to 36 cells, 18 to 22 draws and 41,909 to
  46,581 grass cards at the two low views. No runway, apron, dock or helipad polygon lies on or within 50 m of
  them, and C4 has no brush mask, so nothing is cleared.
- *C4's fir ground.* `terpat01` grows grass between the firs at `min_saturation` 0.2 and `max_value` 0.55, cards
  only. Over its 2 × 2 texels that pass the saturation floor the value reads 0.31 / 0.40 / 0.52 at the 25th, 50th and
  90th percentiles: the grass. The pale paths and bare-soil patches are the 6.7 % over 0.55 (3.5 % over 0.6), so
  0.55 leaves them bare and keeps 93 % of the grass; 0.5 drew the same frames at the 8 m views. A gated pick's
  colour averages only the texels of its block that pass the gate, read on the authored texels, so a tuft beside a
  path takes the grass's colour; a card whose block holds no passing texel is not placed. The shells' blades of a
  gated pick sample the drawn image with its gated-out texels filled from their passing neighbours. Cards only, since the shells would
  cut 955,150 pieces in 2,128 chunks, about 8.4 s; the shells draw exactly as without the pick. 4,457 picked
  triangles over 30,436 cells, indexed in about 80 ms; at the four views 64, 62, 62 and 51 cells, 64, 62, 56 and 38
  draws, 188,541, 181,028, 159,256 and 101,879 grass cards. `gpu_ms` at 5120x1440, three launches, `off` against
  `cards`: 7.00 against 5.91 at 8 m, 7.68 against 7.65 at 30 m (the cards hide ground fragments that were shaded
  themselves); 52 and 46 more draws, 465k and 408k more primitives, `render_cpu_ms` +0.01. Look:
  `.scratch/gc/montage-C4-terpat01-b.png` at `--pos=-1707,699,-6997 --lookat=-1707,670,-7157` (8 m along the slope),
  `--pos=-2901,794,-9899 --lookat=-2901,759,-10059` (30 m), `--pos=-3115,710,-9045 --lookat=-3115,690,-9205`
  (among firs) and `--pos=-8960,621,-8580 --lookat=-8960,645,-8380` (against `srock1-trans1`'s rock).
- *C1B, C2, C3.* No pick. C1B's rock tops are rock, and C3's green ground is painted canopy seen from above
  (Decision 11). C2's football and baseball fields carry their white lines and sidewalks inside the texture, and
  `parkfront`'s lawn carries painted palm shadows, both of which grow cover.
- *Strips on every chapter.* Cover grows only on a picked texture's own triangles, so a runway, apron or helipad
  drawn with its own texture takes none, and one laid over picked ground is an overlay the cards keep off (below).
  C1's mask clears 150 m beside and 600 m beyond each strip. A future pick next to a strip on a chapter with no mask
  is checked the same way: the polygons of the strip textures within 50 m of the picked ones.
- *Where.* On the
  unflagged layer the clutter decorates (`VegetationGround.Covered`), on ground flatter than 60°. C22's mask
  gates it by the texel under the card, unsampled: a bare texel (the landing strips' clearance) grows nothing, and
  a crop texel over neutral (the hedge lines along the field edges and the painted field line) grows grass, so the
  hedges stand in a grass margin. Every other textured polygon over the picked ground is an overlay
  (`GroundCover.OverlaysFor`, shared with the shells), and no card stands within half a metre of one: the railway
  across the western meadows and the roads are separate polygons over `terpat03`, and the tufts first grew through
  the rails. Transitions are exempt, as for C21's added stamps: C1's forest-edge blends lie over the fields and
  meadows, and as overlays they kept about 2.1 km² of picked ground bare.
- *Placement.* On a lattice in the triangle's own texture space, as C21 places, so the cover moves with the
  painted ground. Grass: a tuft every 1.1 m, jittered by 0.4 of the spacing, turned and scaled 0.75 to 1.25 by a
  hash. Crops: loose rows 0.75 m apart on average, a card every 1.3 m along a row on average (`Loose`). Each row
  shifts across by up to a fifth of its spacing and takes its own phase along it, each card wobbles across and
  slides a third of a step along, and a card is 1 to 1.5 steps long, so neighbours overlap or leave a gap and no
  card ends line up from row to row. A 16 m patch keeps 0.6 to 1 of its cards (`PatchKeep`) and grows 0.8 to 1.15
  times the height, so a field is thicker in places. The first build (straight rows of abutting 1.6 m cards 0.9 m
  apart) read as a regular grid of stripes from 30 m. *Rows follow the paint.*
  Each texture is cut into 16 × 16 patches (16 m on C1's 256 m repeat), and a patch's rows run along U or V by
  which way its brightness changes more (`RowsAlongV`), read from the texture at load. The crop textures' fields
  are drawn axis-aligned, so two directions cover them. A card stands upright with its foot along the slope.
- *Colour.* Each card's instance colour is the ground's linear albedo under it: the texture averaged over 2 × 2
  texels (2 m on the 256 m repeat) times the triangle's baked corner colour, linearised, the product the world
  shader takes. The shader is lit like the Enhanced ground (D32's cards are unlit, but the Enhanced ground shades
  under the sun by its normal): the card takes the ground's normal from its custom data, so it shades as the ground
  beside it at every sun, and fogs as the lit ground does. It receives no shadow (*Cost* below). The art carries no hue,
  so the cover has the ground's hue exactly and brightness varies only by the art's gain and 8 % a card.
- *The art (new, drawn in code).* `GrassArt` (128 × 128) is 52 blades fanning from the foot; `CropArt` (256 ×
  128, wrapping across X so a row's cards join) is upright stalks every 6 texels with three arching leaves each.
  Both are grey gain maps averaging one over their solid texels, dark at the foot (0.45) and light at the top
  (1.35), a clear texel holding the mean so the mips do not darken an edge. Seeded `System.Random`, so every run
  draws the same bytes; no generator file, no shipped image. Sizes: a tuft 0.9 m wide and 0.5 m tall at scale
  one (three quads crossed at 60°, 6 triangles); a crop card 1.3 to 1.95 m long, either way round, 0.68 to 1.32 m
  tall, spread 0.6 to 1.4 across and leaning up to a tenth of its height (two quads along the row leaning apart,
  4 triangles).
- *Band and draw.* Each cover has its own band, set in one place (`GroundCover.Band`): crops draw fully inside
  80 m and dither out by 120 m (`csky_clutter_dither_keep`); grass draws fully inside 160 m and dithers out by
  240 m, twice the first build's, as ruled. Past its band a card collapses in the
  vertex stage. Three-dimensional distance, so a camera 240 m over a meadow draws none. The picked triangles
  (1,351 on C1, over 8,342 cells) and the overlays (78,012 triangles) are indexed by 64 m cell at load, in about
  80 ms. A cell's cards of one cover are placed when a rig's camera comes within that cover's band, nearest first,
  two a frame once some stand, and freed a cell past it. One MultiMesh a cover a cell, ranged at the band plus a
  cell. A grass build walks the crop triangles too, for their hedge lines. Sway: the cards' breeze lines
  (`ClutterBuilder.LeanLines`), three times the card's lean for grass and one and a half for crops. No collider,
  no shadow cast, no instance uniform.
- *Counts* (the suite's views, the camera 12 m over the ground): over C1's densest crop field 60 cells, 69 draws,
  62,025 crop and 37,270 grass cards standing; over the western meadow 60 cells, 60 draws, 168,239 grass cards;
  at the strip clearance's southern edge 57 cells, 30 draws, 92,805 grass cards, 88,155 lattice points left bare
  by the mask. The 80 to 120 m grass band of the first build stood 16, 19 and 20 cells and 3,238, 52,930 and
  32,025 grass cards at the same views; the crop rework added about 47 % crop cards (42,135 then). The cards in a
  built cell's far corners lie past their band and collapse in the vertex stage.
- *Cost* (5120x1440, idle, three launches a case, `gpu_ms` against the cover off at three low C1 views: a crop field
  30 m and 8 m up and a meadow 25 m up). Lit and receiving the sun's shadow, the cover cost 3.6, 5.9 and 5.5 ms.
  The cost was the shadow lookup on every alpha-tested card fragment: unlit, the cover cost nothing, and lit with
  `shadows_disabled` 0.3, -0.3 and -0.9 ms in a one-launch probe, where distance thinning, half the cards, no
  alpha-to-coverage and opaque quads each recovered only part of it. The shader therefore receives no shadow:
  +0.50, +0.00 and -0.37 ms, three launches each. A view can come in under the cover off because the cards hide
  ground fragments that were shaded themselves. The four look poses (crop field 30 m and 6 m up, meadow 30 m up and
  looking out) render pixel-identical to the shadowed build, since no real-time shadow falls on that ground. Distance
  thinning (full density to 30 m, then a share (30 / d)² of the cards grown to keep their coverage) saved a further
  0.2 to 0.6 ms but visibly thinned the crops past 30 m, so it is not built.
- *Cost of the word* (`measure.ps1`'s method, 5120x1440, idle, three launches interleaved, `gpu_ms` medians, `off` /
  `shells` / `cards`): C1 crop field 30 m up 4.82 / 5.47 / 5.31, C1 meadow 25 m up 7.42 / 7.95 / 7.09, C4 transition
  8 m up 4.16 / 4.34 / 3.82, 30 m up 3.27 / 3.80 / 3.39. `render_cpu_ms` moves at most 0.05 ms. The cards add 2 to 54 draws
  and up to 500k primitives; at two views they come in under `off`, as above. `.scratch/gc/perf-table.txt` has the ranges.

**Model recommendation.** Opus: the look iterates against captures and the draw crosses the session's live
switches; a mid-tier model can retune the constants (spacing, sizes, band) against a ruling.

**Verify.** Units: `GroundCoverSettingTests` (the ladder, `cards` on every machine and under `--det`, the flag and
the file keep a known word, a `--det` launch reads no saved word), `VegetationSidecarTests` (C1's and C4's cover picks,
a bare and a gated pick read, clamped, warned and written back), the options tests (the row walks between 3D
Vegetation and VR Mode) and `GroundCoverTests` (the saturation gate keeps grey texels bare and the value ceiling pale paths; a bare texel grows nothing
and a hedge line grows grass; `TexelAt` reads unsampled; the row map reads stripes down and across; grass fills a
cell at its spacing on the ground, coloured by it, and a second walk places the same tufts; crop cards lie along
the painted rows, loosely, off any lattice; the mask clears and grasses by texel; a card's foot follows the slope;
a wall and a far cell grow nothing; the art is the same on every run and averages a gain of one). Engine:
`ground-cover-cards` (C1 at rich: at `off` nothing builds; at the default `cards`, around a crop field, a meadow and the
strip clearance's edge every card stands on picked ground, read back through `VegetationGround.LayersAt`, and none
on a bare texel; the cover stands outside the clutter root with no collider, no shadow cast and no instance
uniform; a live move to original and a switch to Original drop it, and back builds it; a live move to `off` drops it
and back to `cards` builds it). Look, judged by you beside D35: `.scratch/d34/`, the first build, flag off against on at a crop field 30 m and 6 m up, a meadow 30 m up, the
strip clearance's north edge 30 m up and the first band's far edge 70 m and 150 m up; `.scratch/d34b/`, flag off,
the first build and the rework at the crop field and meadow views, and the meadow looking out to the 160 to 240 m
edge; `.scratch/d34c/`, the cost pass, the shadowed build against the unshadowed one at those four poses, and
`perf-table.txt`, every step's numbers; `.scratch/gc/` the setting and the spread (below). Budget: the *Cost*
bullet; D37 measures Wave D whole. Goldens: rendered at their manifest args at `off` and at the default `cards`, only
`c1-rocket-hit-enhanced` moves, 2 pixels by 1 level, the same in a repeat; every other C1 Enhanced shot and the
Faithful `c1-waterfall` render identical. D37 re-pins.

**⚠ Traps.**
- New art only: the cards are drawn in code and take the ground's colour at runtime. Never sample the original's
  textures into the art.
- Never let the cards receive the sun's shadow: the lookup on their alpha-tested overdraw was 3 to 6 ms of GPU.
- Never give the shader an instance uniform: a cell is a node, and the cells churn as the camera flies.
- Keep the cover off overlays: a road or rail is its own polygon over the picked texture, and the cover under it
  grows through. Both covers read `GroundCover.OverlaysFor`.
- Sum a shell piece's area about its own corner: summed about the origin, coordinates in the thousands round a
  rail's sliver of overlap to nothing and the piece grows on the rail.
- Read the mask by texel, not bilinear: the crop interiors sit at 2 of 255, so a bilinear sample beside the
  strip clearance blurs the cut.
- Never write a float into shader text through C# formatting: the session's culture writes `80,0` and the shader
  fails to compile, which draws every card as Godot's white default.
- Keep the picks in the sidecar's `cover`; a texture with rock, tarmac or painted shadow in it takes a `min_saturation`.

## D35 ☐ Ground cover prototype: procedural shells; pick at the controls

**Goal.** The same ground cover drawn procedurally (shells or generated blades), shown beside D34 so
you pick one.

**Evidence (confidence: lead-only).** Decision 6.

**Approach (as built).** `Mech3/GroundCoverShells.cs`, the Ground Cover word's `shells` (D34's *The setting*),
built while that word stands and Rich is on, on the same sidecar `cover` picks and `min_saturation` gate as the cards
(the fragment shader reads the ground texel's saturation). Mech3.md has the rules.
- *Why shells, not generated blades.* From 20 to 40 m a pixel at 5120x1440 is 2 to 5 cm on the ground,
  so single blades are a pixel or less across most of the band and would cost hundreds of thousands of
  instances inside 120 m. Shells cost a fixed geometry (the ground's own pieces, eight times) and a
  fill that the band bounds; they carry crop rows and grass tufts, the structure that reads at that
  range, and they follow the ground exactly because they are the ground lifted.
- *Where.* The sidecar's `cover` picks: on C1 `crop04` and `crop05` grow crop rows, `terpat03`, `terpat03b` and
  `grass1` grass tufts; on C4 `cliff01-trans1` and `cliff01-trans3` grass (66,036 pieces in 383 chunks with the
  overlay cut, about 1.0 s, cut on a 12 m grid since C4 has no mask). A `covers` `cards` pick (C4's `terpat01`) is
  left out whole: it neither grows shells nor counts as their ground. A gated pick's shells read the cards' gate as an
  R8 texture of the authored image's 2 × 2 blocks, sampled unfiltered. The picked triangles (`VegetationGround.PaintedSurface`) are cut on C1's brush mask's 12 m texel grid and
  every piece on a texel at 0 is dropped, so the 150 m beside and 600 m beyond each landing strip grow
  nothing (C22's mask). The cards' overlays are cut out of each piece (`KeepOff`): each overlay triangle lying in the
  cards' height window over the piece, grown by half a metre and its box grown by as much, so a rail keeps a
  half-metre margin rather than a 12 m cell. 197,327 pieces cover 22.65 km² (`crop04` 6.76, `crop05` 4.15,
  `terpat03` 8.93, `terpat03b` 2.21, `grass1` 0.60), in 512 chunks of 256 m per texture, built in about 2.1 s
  (0.9 s before the overlay cut). No piece corner stands on C1's railway, where 874 uncut pieces did.
- *Draw.* Each chunk is one MultiMesh drawing its mesh `Layers` (8) times, each layer lifted by its
  share of the cover's height (instance custom data): grass 0.5 m, crops 0.9 m. `Band` is the cards' band (`GroundCover.Band`), so both covers reach as far: grass whole inside 160 m of the eye and dithered out by 240 m, crops whole
  inside 80 m and out by 120 m (eye to fragment, so from that height up nothing draws). A piece past its
  band collapses in the vertex stage. Ranged at the band plus the chunk's farthest corner. No collider,
  no shadow cast or received, no instance uniform.
- *Pattern.* Hashed in the texture's own metres, so it moves with the painted ground. Grass: a tuft every
  0.45 m, jittered, widening with height and split into nine blades that thin toward their tips. Crops:
  rows 0.75 m apart along the texture axis across the painted stripes, read from the live texture at load
  (a luminance structure tensor over a 32 × 32 grid, never saved). The painted fields run along the
  texture's axes, so the axis is snapped: following the field's angle smoothly bent the rows into
  fingerprint swirls. The rows follow it loosely: they wander up to 0.8 of a row over 23 m and wobble
  per row over a few metres, each row sits off its slot by up to 0.15, and each plant (one every 0.2 m)
  by up to 0.125 more. A plant varies in height (0.55 to 1) and thickness (0.7 to 1.35) and is cut into
  strands. Patches of about 7 m (value noise) keep 60 to 100 % of the plants and lower the crop by up to
  30 %, so neither the near rows nor the far cover is even. Where a cell is under a few pixels (two for
  grass, four to ten for crops) a fragment dithers at its layer's mean cover, the crop's taken under its
  patch, which does not shimmer or band. A slow sway moves the tips.
- *Colour.* The ground's own texel, a mip and a half softer, times its baked vertex colour, lit as the
  world (Enhanced's lit terrain), so hue and brightness are the ground's; only a shade from 0.53 at the
  foot to 1.27 at the tip (crops 0.68 to 1.26, times its patch's 0.93 to 1.05) and a spread per tuft of
  ±15 % (per plant ±10 %) move it. Over the pixels the shells change, the captures keep the ground's hue
  and read 0.3 sRGB levels darker at 30 m over the fields, 4 up close, 2.5 on the meadow and 1 at its
  far edge.
- *At the probe poses* (ground in the band from the 12 m raster, chunks whose range reaches the eye,
  each one draw of 8 layers before the frustum cull): low over a crop field 4.2 ha, about 4 chunks; 8 m
  over crops 4.4 ha, 5 chunks; low over meadow 17.8 ha, 9 chunks (4.2 ha and 4 chunks at the first
  80-120 m band); 30 m over meadow looking out 13.4 ha, 8 chunks; facing a strip's clearance 9.5 ha,
  9 chunks; 60 m up 3.4 ha, 5 chunks; 150 m up none. The far ground covers fewer pixels a hectare, so the
  meadow's fill grows less than its area.
- *Ruling (yours, from the first montages).* "the crop fields are to regular or need more density. the
  stripes look bad in the top row. can you add more randomness to it? the meadow distance is too short.
  double it or if its to expensive leave it out." The crop randomness and the grass band above are that pass
  (`.scratch/d35b/`).

**Model recommendation.** Opus: a shader look iterated against captures, and the clutter's gating and
mask rules to respect.

**Verify.** Units: `GroundCoverShellsTests` (only C1's fields and meadows grow cover; the layers rise
evenly to the full height; kept pieces tile the triangle inside their cells; a refused cell takes
nothing and the rest lose exactly its share; the row field reads stripes along v, along u, diagonal
and flat). Engine: `ground-cover-shells` (a rich C1 flight at `off` or `cards` builds no shell; at `shells`, every
chunk draws a picked texture, no chunk declares an instance uniform, stands a collider or casts a
shadow, each draws 8 layers ranged inside the band; no piece centre stands on a cleared mask texel, the
clearance holds picked ground the cut drops, and sampled pieces lie on a picked texture at their
height; a live move to original and a switch to Original drop them and back builds the same; a live move to `cards`
drops them for the cards and back builds the same; a session at original builds none; no piece corner stands on C1's
railway, `gantry_tracks2`, where the uncut pieces stand). Unit `AnOverlayCutsItsGrownStripOutOfAPiece`. Look:
`.scratch/gc/tracks/montage-tracks.png`, off, shells and cards along the railway at `--pos=-3170,168,-4600
--lookat=-3165,128,-5000` and 10 m over it at `--pos=-3164,138,-4780 --lookat=-3162,128,-4960`. Look: `.scratch/d35/montage-<pose>.png` (off against on, a full-size crop and
where the shells moved a pixel) and `montage-all.png`, at `--freecam --chapter=C1 --graphics=enhanced
--vegetation-quality=rich --frames=40`, 2560x720: crop `--pos=-2142,158,-4602 --lookat=-2142,128,-4702`,
meadow `--pos=-4566,153,-4794 --lookat=-4566,128,-4894`, strip `--pos=-6490,150,-4074
--lookat=-6380,128,-4074`, mid `--pos=-2142,188,-4560 --lookat=-2142,128,-4760`, high
`--pos=-2142,278,-4560 --lookat=-2142,128,-4760`, close `--pos=-2142,136,-4640 --lookat=-2142,128,-4680`.
The rework's `.scratch/d35b/montage-<pose>.png` show flag off, the first prototype and the rework for
crop, close and meadow, and off against the rework for meadowfar (`--pos=-4566,158,-4600
--lookat=-4566,128,-4954`, the 160-240 m edge), each with where the rework moved a pixel.
Budget: not measured (D37 measures it beside D34 on an idle machine).

**⚠ Traps.** The look call is yours.
- Never give the shell shader an instance uniform: each of the 510 chunks would take 16 slots of
  Godot's global buffer.
- Keep the row direction snapped to the texture's axes: a smoothly turning direction times a phase
  measured from the world origin bends the rows into swirls. Add the randomness as offsets to the row
  coordinate, never as a turn of the direction.
- `patch` is a reserved word in Godot's shading language; the crop's patch noise is `field_patch`.
- The cut follows the mask's 12 m texels, so the clearance's edge is a 12 m stair step; a crater
  (`TerrainCarve`) leaves the shells at the old ground, and the map edge copies none. A prototype
  accepts all three.

## D36 ☐ Deck and Linux: measure Rich and set their default

**Goal.** Rich measured on the Deck and on Linux, and their default set from the measurement.

**Evidence (confidence: lead-only).** Decisions 3 and 18. PLAN-enhanced-ocean measured and budgeted
the Deck the same way.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D37 ☑ Wave D budget and goldens

**Goal.** As A7, for Wave D.

**Evidence (confidence: measured).** Decision 18. A7's instrument, views and ceiling.

**Approach.** `measure.ps1 -Launches 4 -Modes faithful,original,rich,nomodels,nocover` on an idle
machine with gaming mode off: Rich carries D32's near models (and C3's crown models) and D34's ground
cover at its default, Cards; `nomodels` and `nocover` are Rich with `--vegetation-models=off` and
`--ground-cover=off`, each part's A/B. Six more views over the ground Wave D dresses, three launches
each, through `-ExtraViews`: C1's crop field 30 m up (`-2142,158,-4602` to `-2142,128,-4702`), C1's
meadow 25 m up (`-4566,153,-4794` to `-4566,128,-4894`), low among C1's firs (`-9230,206,-6235` to
`-9330,196,-6335`), C2's plain 30 m up (`-100,40,-3100` to `-400,10,-3350`), C3's palm transitions
close (`-6090,35,-3290` to `-6208,15,-3460`) and C4's `terpat01` 8 m up (`-1707,699,-6997` to
`-1707,670,-7157`). The goldens stage re-pinned by hand for the shots Waves C and D moved.

**Result (Wave D).** Median (range of the launch medians), four launches a case; Rich against
Original, and against Rich with each part off:

| Chapter | `gpu_ms` Faithful | Original | Rich | no models | no cover | Rich − Original (Wave C) | draws Rich | prims Rich |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| C1 | 0.71 | 5.87 (5.87-5.89) | 5.96 (5.96-5.96) | 5.96 | 5.96 | 0.09 (0.08) | 527 | 780,853 |
| C1B | 0.65 | 3.49 (3.49-3.49) | 3.50 (3.49-3.50) | 3.49 | 3.50 | 0.01 (0.00) | 302 | 324,170 |
| C2 | 0.87 | 2.99 (2.99-3.00) | 3.06 (3.06-3.07) | 2.99 | 3.06 | 0.07 (0.01) | 1,685 | 1,099,417 |
| C3 | 1.35 | 4.25 (4.25-4.25) | 4.38 (4.38-4.40) | 4.28 | 4.38 | 0.13 (0.02) | 1,299 | 1,247,983 |
| C4 | 0.74 | 2.70 (2.70-2.70) | 2.87 (2.86-2.89) | 2.78 | 2.80 | 0.17 (0.00) | 520 | 420,866 |

Off the A7 views, three launches a case:

| View | `gpu_ms` Original | Rich | no models | no cover | `render_cpu_ms` Original / Rich |
|---|---:|---:|---:|---:|---|
| C1 crop field, 30 m | 4.59 | 5.34 (5.33-5.34) | 5.19 | 4.86 | 0.53 / 0.56 |
| C1 meadow, 25 m | 7.41 | 7.11 (7.10-7.14) | 7.16 | 7.47 | 0.85 / 0.88 |
| C1 among the firs | 6.83 | 6.92 (6.92-6.92) | 6.91 | 6.92 | 0.39 / 0.44 |
| C2 plain, 30 m | 2.77 | 2.80 (2.80-2.80) | 2.77 | 2.80 | 1.22 / 1.33 |
| C3 palm transitions | 5.28 | 5.48 (5.47-5.48) | 5.26 | 5.48 | 1.07 / 1.17 |
| C4 `terpat01`, 8 m | 6.09 | 5.04 (5.04-5.10) | 5.10 | 6.03 | 0.37 / 0.44 |

**Wave D costs at most 0.64 ms of GPU, inside its 1.8 ms share, and all four waves at most 0.75 ms of
the 2.0 ms ceiling.** The worst view is C1's crop field at 30 m: the cards add 0.49 ms and the near
models 0.15 ms. At the A7 views Wave D adds 0.17 ms at most (C4: models 0.09, cover 0.07), C3's
crown and palm models 0.10 to 0.22 ms. Over the meadow and C4's `terpat01` the cover comes in under
Original, by 0.36 and 0.99 ms, because the cards hide ground fragments that cost more to shade. CPU:
`render_cpu_ms` rises 0.03 to 0.11 ms with the models and `proc_ms` up to 0.13 ms (C4), C2's 0.30 ms
noise between identical cases (C24) included. C3's low jungle view of D32's C3 part (+0.61 ms there)
is not one of these views; its pose is not recorded.

**Model recommendation.** A mid-tier model suffices, as A7.

**Verify.** Every launch exit 0. Goldens: Waves C and D moved exactly five Enhanced shots,
`c1-lake-enhanced`, `c1-cloud-deck-enhanced`, `c1-rocket-hit-enhanced`, `c1-cockpit-enhanced` and
`c1-cockpit-xr-sim-enhanced` (D32's and D33's C1 models, the tracks rule, the ground cover); each
rendered to one hash in its batch and its re-render, and to the same hash in three full batteries.
The five hashes are re-pinned by hand (never `-RegenGoldens`), and two further goldens runs pass 38 of
38.

**⚠ Traps.** As A7.
