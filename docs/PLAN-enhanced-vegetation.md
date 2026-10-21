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
co-op host perf) is a constraint the budget holds, not an item here; #142 and #205 (Enhanced C5
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
| 18 | Budget | **Measured per wave** with `--perf` at a fixed worst-case view per chapter; the ceiling is set from Wave A's measurement; CM23's co-op host (#57) must not get slower; the Deck stays Original until a Deck item measures it |
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
3. ◐ Per-stamp jitter, rotation and tint for listed kinds
4. ☐ Card shading: wind sway, sun-side shading, contact shadow
5. ◐ Vegetation lab in `--freecam`: live values saved to the sidecar
6. ◐ The lab's paint tool: texture picks and a brush mask
7. ☐ Wave A budget: the perf baseline, the ceiling, CM23 unchanged; goldens re-pinned

### Wave B, mass

11. ☐ Canopy mask bake: from stamp density (C1) and from texture picks (C3)
12. ☐ Prototype: canopy shells on C1
13. ☐ Prototype: impostor clumps on C1
14. ☐ Pick the mass technique at the controls and productionise it
15. ☐ C3's canopy volume over its painted jungle, palms on the transitions
16. ☐ Wave B budget and goldens

### Wave C, density

21. ☐ Remake-added stamps on picked textures, in texture space, under the mask
22. ☐ C1's grassland and fields: sparse original cards, hedgerows by mask
23. ☐ C1B, C2 and C4 tuned in the lab
24. ☐ Wave C budget and goldens

### Wave D, near

31. ◐ Scripted Blender pipeline: one generator per species, headless glTF export, renders beside the card
32. ☐ Near-radius 3D draw with a dither handover to the card
33. ☐ Look-alike models for every listed card species
34. ☐ Ground cover prototype: new-art grass and crop cards
35. ☐ Ground cover prototype: procedural shells; pick at the controls
36. ☐ Deck and Linux: measure Rich and set their default
37. ☐ Wave D budget and goldens

## Dependency and parallelism notes

A1 and A2 block everything: every later item reads the setting and the sidecar. A3 and A4 both edit
`ClutterBuilder`'s stamp and shader (`CSVM/src/Mech3/Clutter.cs`), so they run in sequence, not in
parallel worktrees. A5 → A6 is a chain, and A6 blocks B11 (C3's mask comes from texture picks) and
C21 (picks and mask drive added stamps). B12 and B13 can run in parallel worktrees once B11 lands if
each owns its own new module and neither edits `Clutter.cs`. B14 needs both. C21 blocks C22 and C23.
D31 blocks D33; D32 can start beside D31 against a placeholder mesh. D34 and D35 run in parallel if
each owns its own module. D36 waits for every look item. Each wave's budget item closes that wave.

---

# Wave A, pattern

## A1 ☑ Vegetation Quality setting (Original / Rich) under Enhanced, live switch

**Goal.** A Vegetation Quality word setting in the video options, Original or Rich, that only acts
under Enhanced and switches live without a relaunch. Rich is the desktop default; the Deck and
Linux default to Original.

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

## A3 ◐ Per-stamp jitter, rotation and tint for listed kinds

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
  unchanged. The shipped `yaw_deg` stays 0.
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

**Verified.** <pending orchestrator run>

## A4 ☐ Card shading: wind sway, sun-side shading, contact shadow

**Goal.** Under Rich, cards sway in a wind, brighten on their sun side, and sit on a soft contact
shadow.

**Evidence (confidence: lead-only).** The card shader is generated in `ShaderCode`
(`Clutter.cs:814`) with its facing in `FaceBasisLines` (`:865`).

**Approach.** Emit the three as a Rich-only variant of the card shader. <TODO: the wind source
(a global, or WeatherRig), and whether the contact shadow is a ground decal or a card-base darkening.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.**
- Never declare an instance uniform below `csky_instance_uniforms.gdshaderinc` (`Clutter.cs:829`).
- A blended card takes `depth_prepass_alpha`, never `depth_draw_never` (`Clutter.cs:812`).
- A hidden or cratered stamp has a zero basis that the vertex stage reads (`Clutter.cs:841`); sway
  must not revive it.
- A live mode switch must find the other variant's compiled shader (ShaderTwins, `Clutter.cs:70`).

## A5 ◐ Vegetation lab in `--freecam`: live values saved to the sidecar

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

## A6 ◐ The lab's paint tool: texture picks and a brush mask

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
  left click off the panel rays the world colliders on the next physics frame. A pick applies at
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
  p => VegetationGround.LayersAt(gamez, p))`; `ApplyVegetationMask(mask)` stores the mask as the
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
- The physics ray can stop on a solid decoration's collider (a C2 building); the brush paints at
  that XZ and a pick reads the ground beneath, which is what is wanted.

## A7 ☐ Wave A budget: the perf baseline, the ceiling, CM23 unchanged; goldens re-pinned

**Goal.** Wave A's cost measured, a ceiling for the plan set from it, CM23's co-op host no slower,
and the Enhanced goldens Wave A moved re-pinned.

**Evidence (confidence: lead-only).** Decision 18. The CM23 measurement method is
`analysis/cm23-host-perf/FINDINGS.md`.

**Approach.** <TODO: the fixed worst-case view per chapter (low over C1's forest, C3's jungle).>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** Read `docs/verification.md` first. A golden that flips under external GPU load is a
determinism defect to name and track, not one to re-pin.

# Wave B, mass

## B11 ☐ Canopy mask bake: from stamp density (C1) and from texture picks (C3)

**Goal.** A per-chapter canopy mask that says where forest mass is drawn: baked from the placed
stamps' density where the forest is cards (C1), and from the texture picks where it is painted (C3).

**Evidence (confidence: lead-only).** Decisions 11 and 12. `OceanMask` (`CSVM/src/Effects/OceanMask.cs`)
is the baked-mask precedent, with a cached bake (PLAN-enhanced-ocean Wave B).

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** A mask baked from stamps must bake from the Rich placements (after A3 and A6), or the
mass and the cards disagree.

## B12 ☐ Prototype: canopy shells on C1

**Goal.** Stacked alpha-tested shells over C1's forest that take over as the cards fade, so the
forest reads as mass from altitude.

**Evidence (confidence: lead-only).** The layered-forest technique for distant views is in the
literature (TU Wien 2006 report on forest rendering); Decision 12.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO: a montage from several altitudes against the Original fade, for B14.>

**⚠ Traps.** <TODO>

## B13 ☐ Prototype: impostor clumps on C1

**Goal.** Pre-rendered cards of several trees placed past the card fade on C1, as MSFS does.

**Evidence (confidence: lead-only).** Decision 12.

**Approach.** <TODO: how the clump textures are made (rendered from the original cards, or from
D31's models).>

**Model recommendation.** <TODO>

**Verify.** <TODO: the same montage poses as B12.>

**⚠ Traps.** <TODO>

## B14 ☐ Pick the mass technique at the controls and productionise it

**Goal.** One technique chosen by you from B12 and B13, made production quality on C1.

**Evidence (confidence: lead-only).** Decision 12.

**Approach.** Show both side by side (montage, then the controls) and ask with AskUserQuestion.

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The look call is yours. A luminance distance never settles it.

## B15 ☐ C3's canopy volume over its painted jungle, palms on the transitions

**Goal.** C3's `forest1`/`cliff1` canopy gains a volume with depth, and its transition textures
(`cliff1_toptrans`, `cliff1_sidetrans`, `cliff1_watertrans2`) carry palm1, palm2 and palm3.

**Evidence (confidence: measured areas, lead-only look).** The survey table above; palmtree2/palmtree3
draw today as palmtree1's substitution targets. Decision 11.

**Approach.** The B14 technique fed by the C3 mask from texture picks; palms through A6's texture
picks. <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** No cards on the painted treetops: the texture already shows them from above.

## B16 ☐ Wave B budget and goldens

**Goal.** As A7, for Wave B.

**Evidence (confidence: lead-only).** Decision 18.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** As A7.

# Wave C, density

## C21 ☐ Remake-added stamps on picked textures, in texture space, under the mask

**Goal.** Under Rich, a picked texture gets stamps from its vegetation set, once per texture repeat
like the original's, and the mask thins or thickens them.

**Evidence (confidence: traced for the lattice, lead-only for the rest).** The lattice and its rules
are `PlaceOnTriangle` (`Clutter.cs:556`) and [`org/clutter.md`](org/clutter.md), "The lattice rule".

**Approach.** <TODO: how a vegetation set is laid out in the unit cell (a generated scatter on a
fixed seed), and whether it reuses `PlaceOnTriangle` or a sibling.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.**
- No world grid: added stamps walk the same UV lattice.
- Keep the strict edge test and the quarter-metre dedup (`Clutter.cs:604`) on added stamps too.
- C5 already draws about 139k sprites; density must not reach unlisted chapters' cities.

## C22 ☐ C1's grassland and fields: sparse original cards, hedgerows by mask

**Goal.** C1's `terpat03`/`terpat03b`/`grass1` carry scattered bush1/bush2 and the odd fir, and its
`crop04`/`crop05` fields stay sparse, with hedgerows painted in by mask where wanted.

**Evidence (confidence: measured areas, lead-only look).** The survey table above; Decision 13.

**Approach.** Lab work over C21. <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** Grassland must stay grassland: the sparseness is the brief.

## C23 ☐ C1B, C2 and C4 tuned in the lab

**Goal.** Each of C1B, C2 and C4 has a sidecar entry tuned by you in the lab.

**Evidence (confidence: lead-only).** Decision 14; the survey table above.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** C2's suburbs and parked cars are solid decorations: never list them.

## C24 ☐ Wave C budget and goldens

**Goal.** As A7, for Wave C.

**Evidence (confidence: lead-only).** Decision 18.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

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
- **firtree1 (C1)**: a six-sided trunk, a six-sided dark core, and 15 whorls of drooping fronds (5 to
  7 a whorl) whose tips follow the card's silhouette. **758 triangles**, 1281 vertices,
  8.5 × 8.3 × 17.39 m, a 102 KB GLB.

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

**Verified.** <pending orchestrator run>

## D32 ☐ Near-radius 3D draw with a dither handover to the card

**Goal.** Within a near radius a listed stamp draws its 3D model, dithering over to the original card
beyond it; from altitude nothing changes.

**Evidence (confidence: lead-only).** Decision 16; X-Plane 12 draws trees the same way (developer
docs, "building custom 3D trees"). The card fade already dithers (`csky_clutter_dither_keep`,
`Clutter.cs:850`).

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** Not solid (Decision 15): give the 3D draw no collider.

## D33 ☐ Look-alike models for every listed card species

**Goal.** A Blender model per listed card species: C1's firs and bushes, C2's spruce, palms and
brushes, C3's palms, C4's firs.

**Evidence (confidence: measured sizes).** Card sizes are in the survey's FINDINGS.

**Approach.** D31's pipeline per species, judged beside its card.

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** Match the card's silhouette and colour, or the handover in D32 shows.

## D34 ☐ Ground cover prototype: new-art grass and crop cards

**Goal.** Close-up grass and crop cover drawn from new-art cards on picked textures.

**Evidence (confidence: measured).** No grass or crop card exists in the install (survey); Decision 6.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D35 ☐ Ground cover prototype: procedural shells; pick at the controls

**Goal.** The same ground cover drawn procedurally (shells or generated blades), shown beside D34 so
you pick one.

**Evidence (confidence: lead-only).** Decision 6.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The look call is yours.

## D36 ☐ Deck and Linux: measure Rich and set their default

**Goal.** Rich measured on the Deck and on Linux, and their default set from the measurement.

**Evidence (confidence: lead-only).** Decisions 3 and 18. PLAN-enhanced-ocean measured and budgeted
the Deck the same way.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D37 ☐ Wave D budget and goldens

**Goal.** As A7, for Wave D.

**Evidence (confidence: lead-only).** Decision 18.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** As A7.
