# The hangar — Build Custom Plane (BL-354, absorbing BL-067)

**ACTIVE PLAN** (written 2026-08-21). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

The full Build Custom Plane flow: airframe, engine, armour, guns, hardpoints, paint, name, a
purchase review, persistence, and the built plane flyable from every human plane picker. The
engine-side decode this plan builds on is complete and lives in
[`docs/org/hangar.md`](org/hangar.md) (both callback dispatchers, the screens' callback map, the
airframe stat table at `0x00619bb0`, the whole economy) and
[`docs/formats/paint.md`](formats/paint.md) "Saved custom planes" (the 204-byte record, field by
field). BL-354 and BL-067 were both re-verified still-open in the session that wrote this plan:
both entries were read and amended the same day, and a code sweep found no hangar surface in
`CSVM/src` (only `LoadoutChoice`'s deliberate "a custom plane's saved fit" seam awaiting one).

Deliberately out of scope: a funds/wallet economy (campaign property, BL-354 trap b), BL-062's
backward weapon-cycle direction (its blocker is the keymap, not this plan's loadouts), writing
anything into the original game install, and any `--det` visibility (the hangar is menu-side;
a scripted run must stay byte-identical throughout).

## Milestone goal

- A hangar flow opens from the Instant Action wizard's Build button and from a top-level
  launchscreen entry, walking the original's screen order: plane selection, airframe, engine,
  armour, guns, hardpoints, paint, name, purchase review.
- Every value on those screens is the decoded original's: slot titles and turret slots from the
  stat table, the 11-row gun model, per-wing hardpoint counts 0-4, armour units 0-12 shown x5,
  the decoded costs and weights with the running totals, and the overweight / no-engine gate.
- A finished plane persists as CSVM's own JSON under `user://`, appears after the 11 stock
  airframes in every human plane picker (splitscreen seats included) with the after-build
  auto-select, and flies with its guns, hardpoints, paint and name real.
- Saved planes from a real original install's `Planes\` directory import read-only through the
  decoded 204-byte record.
- Whatever A1's consumer decode establishes about armour, engine and weight reaching the flight
  model and damage pools is wired, or explicitly split out as its own backlog item.

**No wallet, no writes into the install, no `--det` change.** The funds gate is campaign
property; the install is source data; the hangar must be invisible to a scripted run.

## Decisions (2026-08-21)

From the grilling that preceded this plan. The table is the authority when prose contradicts it.

| # | Question | Decision |
|---|---|---|
| 1 | Scope: full hangar or a BL-067 slice? | **Full hangar** — the decode is complete enough that nothing blocks it; a partial hangar has no seam a player would accept. BL-067 lands as two of its screens. |
| 2 | What does PURCHASE enforce? | **Capacity + engine present; prices computed and displayed** — the decoded cost arithmetic is built as a reusable component for later campaign use, but no invented wallet gates a build. |
| 3 | What do armour and engine do in flight? | **Decode first, in-plan (A1)** — trace the spawn-descriptor consumers in `crimson.exe`; wire what it finds. If it balloons past a wave it splits out as its own BL and the two fields ship chosen-but-inert here. No invented scalings. |
| 4 | UI surface | **Hybrid** — launchscreen idiom extending `LaunchMenu` the way the IA wizard did, pulling in the blueprint/icon TGAs where they carry information, `PlanePainter` driving a live paint preview. |
| 5 | Save format | **Own JSON in `user://`; the 204-byte format is import-only**, read from a real install's `Planes\` directory. |
| 6 | Entry and reach | **IA Build button + top-level entry; customs flyable in every human picker**, after the 11 stock airframes, with the index-11 after-build auto-select contract. |
| 7 | BL-062 rides along? | **No** — keymap problem, stays its own item. |
| 8 | Execution and verification | **Orchestrated per-item subagents on this worktree branch, orchestrator commits; unit tests on the importer and the economy arithmetic; one owed at-the-controls closing pass.** |
| 9 | Airframe availability in Instant Action | **All 11 offered** — the stat table's campaign-progress threshold ships in the data (B13 carries it) but gates nothing here; inventing a progress value for a sandbox mode would be a guess. The campaign gets the gate when it exists. |
| 10 | Running totals and the overweight gate (user, mid-run) | **A persistent second stats row on every hangar screen** (total price, weight / capacity, flagged when over) lands with C26. **Overweight purchase stays blocked**: the commit callback 2263 re-checks nothing, but `PURCHASE.SCRIPT` disables `pur_b_purchase` (mail 10018) whenever the problems callback 2264 reports, so the original hard-blocks at the button; our commit-refusal is the same rule. |
| 11 | The first D33 pass's findings (user, at the controls) | **Wave E**: the airframe-defaults ask lands with string 206's own wording; the engine None row STAYS (the decode disproved its removal: callback 2218 authors seven rows with 1165 "None"; user accepted, our 1171 label corrected); a pattern pick loads that pattern's default colours (the `0x0061daf0` table). The "(2)" prefix was a question, not a defect: it is the original's twin-mount label. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Airframes differ in gun/hardpoint slot counts, held somewhere in the executable" (BL-067's framing) | The prospecting decode: `GUNS.SCRIPT` builds 4 dropdowns in a fixed loop, `HARDPOINTS.SCRIPT` 2, no callback gates either; the stat table's 11 dwords are fully accounted for and none is a count. Every airframe is 4 gun slots + 2 per-wing hardpoint groups; what varies is slot titles, turret bits and weight capacity ([`org/hangar.md`](org/hangar.md)). |
| 2 | "Record +0x34/+0x38 are the two hardpoint ordnance picks" (this plan's own first decode pass) | The same prospecting pass: they are the left/right wing hardpoint COUNTS, 0-4 each (dropdown 2244, langui 1165/1168/1169). Corrected in paint.md the same day. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced** | A2, A3 (inputs), B12, B13, C21-C26, D31 (the data they render/compute is decoded with addresses) | Confirm against `org/hangar.md` / `formats/paint.md`, then build. |
| **Direction sound** | B11, D32 | The record and spawn path are decoded; the CSVM-side shape (JSON schema, builder wiring) is ours to design. |
| **Leads only** | (none since A1 landed) | A1's consumer trace closed the plan's one lead-only item: armour and engine are traced to their combat consumers, weight and the power rating are sourced dead ends. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The survey is not duplicated here; it lives in two decode pages written the same day as this plan:

- [`docs/org/hangar.md`](org/hangar.md): the widget dispatcher at `0x004093a0` and screen-flow
  dispatcher `FUN_00407670`; the callback map for AIRFRAME/GUNS/HARDPOINTS/ARMOR/PURCHASE; the
  airframe stat table at `0x00619bb0` (all 11 rows: cost, weight, capacity, agility, armour,
  availability, turret bitmask, four slot-title langui ids); the gun table at `0x00619e68`
  (wing/turret cost and weight columns, twin doubles both); engine bases at `0x00619d98` with
  per-id offsets; armour units x4; hardpoints $410 / 480 lb; totals `FUN_00405680` /
  `FUN_00405550`; the purchase gate 2264.
- [`docs/formats/paint.md`](formats/paint.md) "Saved custom planes": the 204-byte record layout,
  the writer `FUN_0041a7b0`, the scratch slot, the 24-slot name index, callback 1024.
- Shipped data this plan consumes without re-decoding: `extracted/zrdr/weapons.zrd.json` (the
  calibre gun defs, `30CAL`-`70CAL` families), `extracted/rof/ui_strings.json` (slot titles
  3060-3079, gun names 3310-3314, engine names 3100+af*6+id, purchase strings 1165-1194, 1227).

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — decodes and inventories

1. ☑ Trace the spawn-descriptor consumers: armour units, engine id and total weight into flight and damage
2. ☑ Asset and string inventory: the blueprint/icon TGAs and every langui roster the screens need
3. ☑ The gun mapping: calibre + twin + turret onto `weapons.zrd` defs, and how the ammo layer sits on top

### Wave B — model, persistence, economy

11. ☑ `CustomPlaneDef`: the CSVM model and its JSON persistence under `user://`
12. ☑ The 204-byte importer, tested against real saved-plane files
13. ☑ The economy component: costs, weights, totals, and the capacity/engine gate

### Wave C — the screens

21. ☑ Hangar shell and navigation: screen order, IA Build entry, top-level entry
22. ☑ AIRFRAME screen
23. ☑ ENGINE and ARMOR screens
24. ☑ GUNS and HARDPOINTS screens (BL-067)
25. ☑ PAINT and PLANENAME screens
26. ☑ PURCHASE review screen

### Wave D — into flight

31. ☑ Custom planes in every human plane picker, with the after-build auto-select
32. ☑ Building a custom plane into a flying aircraft
33. ◐ The closing at-the-controls pass, and the BL-354/BL-067 closures (first pass flown, findings below)

### Wave E — the first pass's findings (2026-08-23, user at the controls)

41. ☑ The airframe-defaults ask (string 206): default armour/engine/guns on the airframe pick, or keep
42. ☑ Engine screen: the None row keeps its place, relabelled to the original's 1165
43. ☐ Pattern pick loads the pattern's default colours (the `0x0061daf0` table; swatch decode)
44. ☐ Armour displays as the original's 0-60 in steps of 5 (the x5 display scale), everywhere it shows

## Dependency and parallelism notes

A1-A3 are independent of each other and of Wave B; run them in parallel. B11 blocks B12 (the
importer targets the model) and C21-C26 (the screens edit the model); B13 blocks C26 and the D32
gate wiring. C21 blocks C22-C26 (they mount in its shell); C22-C26 are then mutually independent
but all edit the launchscreen UI surface, so give concurrent agents per-screen file ownership and
never two agents in `LaunchMenu.cs` at once. D31 needs B11 and C21; D32 needs B11, A1 and A3; D33
is last and is hand-flown. A1 may end in a split-out BL, in which case D32 wires guns/hardpoints/
paint/name only.

---

# Wave A — decodes and inventories

## A1 ☑ Trace the spawn-descriptor consumers: armour units, engine id and total weight into flight and damage

**Landed.** The full trace is [`org/hangar.md`](org/hangar.md) "Into the mission: what the
build changes on the spawned vehicle". The shape D32 wires:

- **Armour is live per-zone combat data.** The four values (order nose, tail, left wing,
  right wing) travel as raw x5 floats through globals onto the vehicle's four named damage
  zones, setting the armour pool's max and current; structure pools come from the mission
  file. Vehicle totals are always recomputed sums over zones, never independent state.
  Negative means "do not override". Difficulty scales enemies only (x0.875/1.0/1.125), never
  the player.
- **Engine id is two things.** Ids 0-5 decompose into a power tier 0-2 plus a nitrous boolean
  (ids 3-5); the tier selects a row of a separate engine registry whose power float lands in
  the vehicle's flight-tuning block (`veh+0x66c`); nitrous is an independent flag
  (`veh+0x946`). Id 6 = stock, no override.
- **Two sourced dead ends.** Total weight (+0x3c) is consumed only by the hangar's overweight
  indicator; the stat-table power rating feeds only two display strings. Neither reaches
  mass, thrust or drag; D32 must not invent a flight dependency on either.
- **The type-8 spawn message carries paint only** (pattern, colours, and the three
  +0x5c/+0x60/+0x64 picks as texture/decal registrations); stats ride a separate global
  block.
- Not decoded, split as its own concern: the per-hit damage application order across
  zone/total and armour/structure pools (entry points recorded in the org page's Open list).

**Verified.** Report-only decode; every constant in the write-up carries its address and
condition, per the item's own bar.

**Original approach (kept for reference).** Fresh-context decode agent from the type-8
message consumer outward, bounded to the three fields, with the split-out rule of Decision 3
if the web exceeded a wave. The ballooning case did not arise; the only unexplored branch
(per-hit application order) was deliberately left, being a damage-model topic rather than a
hangar one.

## A2 ☑ Asset and string inventory: the blueprint/icon TGAs and every langui roster the screens need

**Landed.** Every hangar screen can get the hybrid treatment; no launchscreen-only fallback is
needed. The inventory, from the read-only sweep of `Z:\CSVM\extracted`:

- **Blueprints 11/11**: `extracted/rof/ASSETS/GRAPHICS/PX_<n>_BLUEPRINT.TGA`, n = 0..10, all
  358x335 24-bit RLE (TGA type 10). No converted copies exist; the TGAs are the only form.
- **Icons 128 files**, `PX_ICON_<airframe>_<pattern>_<n>.TGA`, all 358x335 32-bit RGBA RLE, the
  same canvas as the blueprints so the two overlay exactly. The `n` axis is always complete
  (0-3) but **patterns are sparse**: only pattern 4 exists for every airframe (2-4 patterns per
  airframe; 13 unused everywhere, 12 only on airframe 9). A UI assuming a dense 0-13 grid hits
  missing files; default to pattern 4 when a pattern id has no icon set.
- Bonus assets in the same directory: `PX_P_BLUEPRINTSMALL.TGA` (an 11-cell 128x128 thumbnail
  strip, one per airframe), `PX_P_DECALS.TGA` (a 50-cell 66x66 strip), `PX_PLANEICONS.PNG`,
  `PX_PLANENAMEBACKGROUND.PNG`, `PX_BACKGROUND.JPG`, and the `PX_B_*` button/scrollbar set.
- **Strings: all 105 requested ids present** in `extracted/rof/ui_strings.json`. That file is a
  flat array of `{id, symbol, font, text, dll}` records, ids are NOT unique across the two
  merged tables (ids 9-35 exist in both `langui` and `language`); filter `dll == "langui"`,
  where every hangar range lives. Formats are Win32 `FormatMessage` style (`%1!d!`), not printf.
- **Correction to the decode's label reading**: string 506 is `"(%1!d!) "`, a parenthesised
  count prefix with a trailing space, not a literal "2x". No `2x` string exists in the file. A
  twin gun row therefore reads "(2) <gun name>". `org/hangar.md` corrected in the same commit.
- Airframe names 3000-3010 confirmed (Ford Hoplite through Curtiss-Wright P2 Warhawk); the
  engine layout `3100 + airframe*6 + engineId` confirmed by the data (three displacements plus
  the same three " nitro" per manufacturer); gun names 3310-3314 carry embedded double quotes
  on 3310/3313/3314.

**Verified.** Report-only item; the inventory above is the deliverable, recorded here and
consumed by C22-C26.

**Original approach (kept for reference).** Filesystem sweep over `extracted/` plus a read of
`ui_strings.json` for each id enumerated in [`org/hangar.md`](org/hangar.md) (templates at
`0x61f370`/`0x61f3a0`); missing TGAs would have demoted the affected screen to pure
launchscreen idiom.

## A3 ☑ The gun mapping: calibre + twin + turret onto `weapons.zrd` defs, and how the ammo layer sits on top

**Landed.** The mapping is arithmetic and neither twin nor turret enters the def id:

- **Def id.** `StockLoadouts.GunWeaponId(caliber, ammo)` = `wep_{caliber + AmmoIndex[ammo]}`
  (`CSVM/src/Flight/Loadout.cs:41`; slug=0, dumdum=1, ap=2, magnesium=3), calibre in tens. The
  player matrix `wep_30..33/40..43/50..53/60..63/70..73` covers all 5 calibres x 4 ammo types.
  Hangar calibre id c maps to `caliber = 30 + 10c`; pick id 5 = empty = slot omitted entirely
  (`LoadoutChoice.None`, no group built).
- **Twin is one gun instance with two firepoints**, never a second def and never two instances:
  the marker list is the multiplicity (`GunSpec.Markers`, slot n owns `firepoint(9-2n)` and
  `firepoint(10-2n)`; single = the low one only). Proof: the Kestrel's slot 1 is the sole
  single-barrel stock mount (`CSVM/data/stock_loadouts.json`, one marker where every other slot
  has two, same def either way); the original corroborates (an `ai.zrd.json` turret is one
  weapon over a two-entry firepoint list). Hangar-side the twin bit is only the x2 price/weight
  multiplier.
- **Turret is the same def flagged** (`"turret": true` -> `GunGroup.IsTurret`), selecting only
  the price/weight column. ⚠ Recorded divergence: the original's AI turrets fire the detuned
  slug-only `wep_130..170` family; CSVM's turret slots bind the full-strength player def. Inert
  today (turrets do not fire; `LoadoutChoice.ApplyTo` forces the ammo pick null at
  `LoadoutChoice.cs:123`), but if a hangar turret ever fires, `wep_1N0` is the original's id,
  and no non-slug turret def exists at all.
- **The ammo layer composes with no change.** The hangar writes `Caliber`/`Markers`/`Turret`
  and leaves `WeaponId` null; Ammo Selection keeps writing its slot-keyed ammo name;
  `Loadout.Bind` resolves `WeaponId ?? GunWeaponId(Caliber, Ammo)`. The `WeaponId` escape hatch
  stays reserved for AI defs.

**⚠ Traps for B11/C24/D32.** (a) Render four gun rows for every airframe: most stock planes
author fewer slots in `stock_loadouts.json`, which per the original are slots holding empty
(id 5), not absent slots. (b) The stat table's turret bitmask is 0-based; `stock_loadouts.json`
slots are 1-based (Balmoral `0x0c` = JSON slots 3 and 4); shift by one when binding.
(c) `wep_00..03` is a separate higher-rate slug family whose `NAME`s collide with the player
matrix's; resolve by id, never by NAME (weapons.md's standing rule). It has no 70-cal member
and is not hangar-reachable. (d) Magazine size is calibre-derived (`CLUSTER_SIZE`
2800/2400/2000/1600/1200, agreeing with the hangar stat at gun-table +0x14), so picking a
bigger calibre legitimately shrinks rounds; a design consequence, not a bug.

**Verified.** Report-only item; every one of the 11 dropdown rows x wing/turret resolves to a
shipped def or an explicit named absence, as required.

**Original approach (kept for reference).** Read `weapons.zrd.json`'s gun families and the
stock planes' gun entries; settle twin = two instances vs a dedicated def; the mapping becomes
B11's gun field semantics.

# Wave B — model, persistence, economy

## B11 ☐ `CustomPlaneDef`: the CSVM model and its JSON persistence under `user://`

**Landed.** `CSVM/src/Flight/CustomPlaneDef.cs` holds the pure model (no Godot types): `Name`,
`Airframe` 0-10, `Engine` 0-6 with `EngineNone = 6`, four armour zone unit counts 0-12, four
`GunChoice` slots (nullable calibre 0-4 plus twin bit; null calibre is the record's empty id 5),
per-wing hardpoint counts 0-4, and paint as pattern 0-13, the two composite `a*5+b` picks, the
third pick dword carried opaquely, and three `PaintColour` (byte RGB) slots. The record's derived
fields are deliberately absent, and `Clamp()` forces every field into its decoded range.
`CSVM/src/Flight/CustomPlaneStore.cs` is the persistence: one JSON file per plane
(`<name>.json`, invalid filename characters replaced by `_`), schema `version: 1` with fields
`name / airframe / engine / armour{nose,tail,leftWing,rightWing} / guns[4]{calibre,twin} /
hardpoints{leftWing,rightWing} / paint{pattern,pick1,pick2,pick3,colour1..3}`; `List` / `Load` /
`Save` run over a plain absolute directory through System.IO so they unit-test engine-free, and
`UserPlanes()` is the single Godot touch resolving `user://Planes/`. A file claiming any other
version, or malformed, or missing, reads as null (List skips it); serialisation is canonical, so
load then save is byte-identical. Duplicate-name policy: the name is the identity, exactly as the
original's `sprintf("Planes\%s")` writer behaves, so saving a plane whose name matches an
existing file overwrites it, and two names sanitising to the same filename are the same plane.
Tests in `CSVM.Tests/CustomPlaneStoreTests.cs`: `RoundTrip_PreservesEveryField`,
`Serialize_IsStableAcrossARoundTrip`, `Load_MissingFile_ReturnsNull`,
`Load_MalformedFile_ReturnsNull`, `Load_WrongVersion_ReturnsNull`,
`List_SortsByName_AndSkipsTheUnreadable`, `List_MissingDirectory_IsEmpty`,
`Save_SameName_Overwrites`, `Save_EmptyName_Throws`, `Deserialize_ClampsOutOfRangeValues`,
`Constructor_RelativeDirectory_Throws`.

**Verified.** Orchestrator battery on the committed tree at C21: build 0/0, units 1734/1734,
engine suites 90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** A value type holding everything the record holds (airframe, engine, armour x4, guns x4
with twin bits, hardpoint counts x2, paint pattern/picks/colours, name), serialised as CSVM's own
JSON under `user://Planes/`, listed, loaded and saved; the base `LoadoutChoice.ApplyTo` composes
over.

**Evidence (confidence: direction-sound).** The field set is the decoded record
([`formats/paint.md`](formats/paint.md)); the JSON shape is ours (Decision 5).
<TODO: JSON schema details — naming, versioning field, duplicate-name policy.>

**Approach.** New `CSVM/src/Flight/CustomPlaneDef.cs` (or `Mech3/`, per where `PaintScheme`
lives); persistence in the pattern the stunt-scores file uses for `user://` IO. No Godot types in
the model. Unit tests: round-trip, missing-file, malformed-file.

**Model recommendation.** medium — well-specified construction.

**Verify.** `dotnet test` round-trip suite green; a hand-written file loads and re-saves
byte-identical (JSON-normalised).

**⚠ Traps.** The record's derived fields (+0x98 empties, +0xa8 display cells, +0x28/+0x3c totals)
are not stored in our JSON; they are recomputed (B13). Keep the model free of them.

## B12 ☐ The 204-byte importer, tested against real saved-plane files

**Landed.** `CSVM/src/Flight/CustomPlaneRecord.cs` is the pure import-only reader:
`Read(ReadOnlySpan<byte>)` maps one 204-byte record to a `CustomPlaneDef` (name at +0x04, airframe
+0x2c, engine +0x30, hardpoints +0x34/+0x38, pattern +0x40, picks +0x5c/+0x60/+0x64 carried
opaquely, colours +0x68..+0x70 with the alpha byte ignored, armour +0x74..+0x80 divided by 5 into
units, per-slot twin bit from +0x84, gun ids +0x88..+0x94 with 5 = empty = null calibre);
`ReadFile` and `ImportDirectory` (absolute paths, read-only, name-sorted) wrap it with the store's
tolerant contract: a short file, an empty name, or a field outside its decoded range reads as
null, never a throw. Derived fields (+0x28, +0x3c, +0x98.., +0xa8..) and the undecoded dwords
(+0x00, +0x44..+0x58, +0xc8) are ignored. Seven genuine saves from the user's installs are the
fixtures (`CSVM.Tests/fixtures/planes204/`, original filenames kept); every field of the decoded
table verified against them, all seven parse with name = filename and the four Fury saves all
carry airframe 7. Tests in `CSVM.Tests/CustomPlaneRecordTests.cs`.

Findings against [`formats/paint.md`](formats/paint.md): the layout holds exactly, and the four
Fury saves (made wearing the four shipped Fury patterns) pin the pattern indices blckswan = 1,
fortune = 4, hughes = 6, studio = 11. Their colour triples match the vehicle.json scheme table
except that every scheme slot the table lists as `(0,0,0)` is saved as `(25,25,25)`: the paint
UI's darkest Shade is 25/25/25, not pure black, which also refines `player_fortune`'s inferred
trim. One record (`B`) carries stray high bits in the +0x84 twin dword (0xc3 with only two
occupied slots), so only bit n may be read for slot n.

**Verified.** Orchestrator battery on the committed tree at C21: build 0/0, units 1734/1734,
engine suites 90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** Reading a real install's `Planes\` directory (read-only, absolute path) yields
`CustomPlaneDef`s: name, airframe, engine, armour, guns, hardpoint counts, pattern, picks,
colours, per the decoded layout.

**Evidence (confidence: traced).** The full field table with addresses is
[`formats/paint.md`](formats/paint.md) "Saved custom planes"; the composite `a*5+b` encoding of
+0x5c/+0x60 and the +0x64 open dword are recorded there.
<TODO: at least one real 204-byte file as a test fixture — locate in the user's install
(`Z:` original install path) or ask the user for one; the decode was static, and an end-to-end
read of a genuine file is the proof.>

**Approach.** A pure byte reader beside B11's model, plus tests over fixture files committed to
`CSVM.Tests` (fixtures are binary, small, and legally the user's own saves). The +0x64 dword is
read, preserved, and marked unknown, never interpreted.

**Model recommendation.** medium, low effort — a decoded layout transcribed to a reader.

**Verify.** Fixture tests assert every decoded field against values independently visible in the
original's own UI (name, colours, airframe at minimum).

**⚠ Traps.** Import and creation must not conflate (BL-354 trap a, now the other way round: our
JSON is authoritative for our planes; imports are a one-way read). Armour units are stored
premultiplied by 5 in the record; do not double-apply the display factor.

## B13 ☐ The economy component: costs, weights, totals, and the capacity/engine gate

**Landed.** `CSVM/src/Flight/HangarEconomy.cs` holds the decoded tables as data (`Airframes`,
the 11 stat rows with cost/weight/capacity/agility/armour/availability/turret mask/slot-title
ids; `GunTable`, five wing/turret cost-weight rows; `EngineBases` plus the six-entry
`EngineCostOffsets`/`EngineWeightOffsets`; hardpoint and armour-unit constants) and
`HangarEconomy.Price(CustomPlaneDef) -> HangarBill`: per-line `CostWeight` for airframe, engine
(id 6 = none = zero), each of the four gun slots (wing or turret column per the airframe's
0-based turret bit, twin doubling both), armour units x4 for cost and weight, hardpoints at
$410 / 480 lb; the summed `Total`; the `PurchaseVerdict` (Overweight when total weight exceeds
the airframe's capacity, else NoEngine on id 6, else Ok, funds never checked per Decision 2);
and the display-only star ratings. The armour star formula was confirmed against the decompile
of `FUN_0040faf0` case 2 to read the record's stored armour dwords, which hold units x5, so
`ArmourStars = min((armourStat + units*5 - 1)/0x49, 4)`; agility is C-truncated `(val-1)/4`
capped at 4, matching case 3's shift arithmetic. Provenance stays in
[`org/hangar.md`](org/hangar.md); the code carries one pointer, no per-line addresses.
Tests in `CSVM.Tests/HangarEconomyTests.cs` (17 cases): `AirframeTable_MatchesTheDecode`
(Hoplite/Balmoral/Warhawk rows), `AirframeTable_SlotTitles_MatchTheDecode`,
`GunTable_MatchesTheDecode` (all five rows), `EngineLine_AppliesBaseAndOffsets`,
`EngineLine_NoEngine_IsZero`, `EmptyBuild_TotalsAirframePlusEngine_AndPassesTheGate`
(Hoplite 7650/2400 Ok), `MaxedBalmoral_GoesOverweight` (14187/19892 vs 15760),
`EnginelessWarhawk_IsRejectedForItsEngine` (3081/5773 NoEngine),
`StarRatings_MatchTheDecodedFormulas`.

**Verified.** Orchestrator battery on the committed tree at C21: build 0/0, units 1734/1734,
engine suites 90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** A pure component that, given a `CustomPlaneDef`, reproduces the original's arithmetic:
per-line costs and weights, the two totals, and the gate verdict (overweight / no engine / ok),
exactly as decoded.

**Evidence (confidence: traced).** Every table and formula with addresses:
[`org/hangar.md`](org/hangar.md) "The economy" (gun table, engine bases and offsets, armour x4,
hardpoints 410/480, totals `FUN_00405680`/`FUN_00405550`, gate 2264 checking capacity from stat
table +0x08 and engine != 6).

**Approach.** `HangarEconomy.cs` beside the model; the decoded tables as data (authored constants
with their addresses in a comment-free table file or const arrays; provenance lives in
`org/hangar.md`, not comments). Tests assert the tables and a set of whole-plane totals computed
by hand from the decode. Built per Decision 2 as reusable: no UI types, no gate on funds but the
cost figures are first-class outputs.

**Model recommendation.** medium, low effort — decoded arithmetic transcribed.

**Verify.** Test suite green, including at least one deliberately overweight build and one
engineless build rejected with the right verdicts.

**⚠ Traps.** Armour is x4 in cost AND weight but displayed as x5 lb; three different factors near
each other. The turret price column is selected by the airframe's turret bit per slot, not by the
gun.

# Wave C — the screens

## C21 ☐ Hangar shell and navigation: screen order, IA Build entry, top-level entry

**Landed.** The shell is `CSVM/src/UI/HangarFlow.cs`, engine-free the way `BoardMenu` is: it owns
the screen order, the scratch plane and the rules, and `LaunchMenu` is only its renderer and input
source. `HangarFlow.Order` is the original's nine screens (plane selection, airframe, engine,
armour, guns, hardpoints, paint, name, purchase), walked over one scratch `CustomPlaneDef` with
`Move` / `Step` / `Accept` / `Back` in the launchscreen's own live-stepper idiom.

- **Two doors, one entry point.** `LaunchMenu.OpenHangar(returnTo)` is reached from a trailing
  `Build Custom Plane` row past the three Mode rows, and from the same row past the eleven
  airframes on the Instant Action plane pick (Decision 6). It records the screen to land back on,
  so cancelling always returns where the pilot pressed.
- **Cancel is residue-free by construction.** Nothing is written until `Commit()`, so `Back()` off
  the first screen sets `Exit = Cancelled` and the scratch plane is simply dropped. There is no
  undo path to get wrong. A flow never survives a trip through flight either: `ShowMenu` clears it.
- **The commit is the whole gate, in one place.** A name (langui 203), then `HangarEconomy`'s
  verdict in the original's own words (1182 + 1227 OVERWEIGHT, 1182 + 1171 No Engine Selected),
  then `CustomPlaneStore.Save`. Funds are never checked (Decision 2). Editing a saved plane starts
  from a copy made through the store's canonical serialisation, so abandoning an edit cannot touch
  what is on disk.
- **The D31 seam** was `LaunchMenu.LastBuiltPlane`, the name a completed build hands back. D31
  has since landed and consumes it: customs list after the eleven stock airframes and
  `CloseHangar` auto-selects the just-built plane by name (the index-11 contract).
- **Strings** resolve through the new `CSVM/src/Mech3/UiStrings.cs`: `extracted/rof/ui_strings.json`
  under the session's own `dataRoot` (the path idiom `HudFont` already uses), langui rows only
  (ids repeat across the file's two tables), `FormatMessage` specifiers (`%1!d!`, `%1!02d!`, `%%`)
  converted to composite format, leading `[FONTID]` tags stripped. Loaded once on first hangar
  entry; a missing extraction is a warning and every label falls back to its own plain text.
- **Nothing scripted sees it.** No `--menu=` opening onto a hangar screen, no `SessionSpec` field,
  no `Launcher` change. The hangar row is a door, not an aircraft: it cannot be locked or
  confirmed, so no launch path reads it, and it is drawn only for a lone pilot under Instant Action
  (`HangarRowOnPlaneScreen`) so a splitscreen pane's roster stays the eleven airframes.

**The mount-point contract for C22-C26.** Each item implements `IHangarPage` for its screen in a
NEW file of its own (`CSVM/src/UI/Hangar<Screen>Page.cs`), deriving from `HangarPage` for the flow,
the scratch plane and the langui heading. The members are:

| Member | What the screen provides |
|---|---|
| `Screen` | its `HangarScreen` value |
| `Title` | inherited; already the screen's own langui id (1017 / 1004-1010 / 1401) |
| `RowCount` / `RowText(row)` | the list the shell draws |
| `Detail(row)` | the line under the list for the focused row, or `""` |
| `Step(row, dir)` | the ←→ live stepper, editing `Scratch` in place; returns whether anything changed |
| `Accept(row)` | `false` (the default) lets the flow advance to the next screen; `true` means the page handled the press |

Everything is plain text and plain indices, so a page is engine-free and testable and the shell
needs no change to draw one. The ONLY edit outside the new file is one line in
`HangarFlow.PageFor`'s switch, replacing that screen's `HangarPlaceholderPage`. `LaunchMenu.cs`
is not touched by C22-C26 at all, which is what lets them run concurrently. Screens needing art
(the blueprint/icon TGAs of Decision 4, C25's `PlanePainter` preview) also need a TGA loader, which
the codebase does not have and this shell deliberately did not invent: that page brings its own,
plus whatever extension to the page contract it needs.

What stands in each screen today: `HangarPlaceholderPage` for airframe, engine, armour, guns,
hardpoints and paint. The right heading, a Continue row, and a real summary of what the scratch
plane carries there (langui names, stat-table slot titles, the economy's own figures). It edits
nothing, so a flow walked straight through produces exactly the plane the screens before it chose.
`HangarPlaneSelectionPage` is real (New Plane, or one of the store's saved planes to edit).
`HangarNamePage`'s stepper over airframe-derived names is a placeholder for C25's text entry, and
`HangarPurchasePage`'s totals line is a placeholder for C26's itemised list, but the gate and the
save under it are already the real ones.

Tests in `CSVM.Tests/HangarFlowTests.cs` (14 cases): `ScreenOrderIsTheOriginals`,
`AFlowOpensOnPlaneSelection`, `EveryScreenTitlesItselfFromLangui`,
`ConfirmWalksTheOrderAndStopsOnPurchase`, `BackWalksTheOrderInReverse`,
`BackOffTheFirstScreenCancels`, `CancellingMidFlowWritesNothing`,
`CompletingTheFlowSavesTheScratchPlaneAndNamesIt`, `AnEnginelessBuildIsRefusedWithTheOriginalsWords`,
`AnOverweightBuildIsRefused`, `ANamelessBuildIsRefused`, `EditingASavedPlaneWorksOnACopy`,
`NewPlaneSeatsAFreshScratch`, `ThePlaceholderScreensEditNothing`; and
`CSVM.Tests/UiStringsTests.cs` for the table's two-namespace rule and the `FormatMessage`
conversion.

**Verified.** Orchestrator battery on the committed tree at C21: build 0/0, units 1734/1734,
engine suites 90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** The hangar opens from the IA wizard's Build button and from a top-level launchscreen
entry, walks the original's screen order over a scratch `CustomPlaneDef`, and returns to the
caller with the built plane selected or the flow cancelled.

**Evidence (confidence: traced for the order).** The original's flow and screens:
[`org/hangar.md`](org/hangar.md); the IA wizard's structure in `CSVM/src/UI/LaunchMenu.cs` is the
idiom to extend (Decision 4).

**Approach.** Extend `LaunchMenu` the way the IA wizard did: a hangar state with a screen index,
scratch def, and back/next navigation. The IA dropdown's Build entry and the top-level entry both
route here.

**Model recommendation.** high — the item that decides the UI seam every other C item mounts on.

**Verify.** Enter from both doors, cancel from every screen without residue, complete a flow and
land back with the new plane selected.

**⚠ Traps.** `--det` and the scripted launch path must not see any of this; the hangar is reached
only through interactive menu input.

## C22 ☐ AIRFRAME screen

**Landed.** `CSVM/src/UI/HangarAirframePage.cs` fills the Airframe slot in `HangarFlow.PageFor`
(the one switch line). All 11 airframes are rows (Decision 9: the availability threshold gates
nothing), named from langui 3000+id, the chosen one ticked. The ←→ stepper makes the focused row
the scratch plane's airframe and writes nothing else: guns and hardpoints are count-valid on
every airframe (wrong-claim 1), so no other pick needs re-clamping, and Confirm advances without
editing, so a flow walked straight through keeps whatever airframe was chosen. Each row's detail
line carries the stat table's cost, weight and weight capacity plus the two star ratings, priced
through `HangarEconomy.Price` for the focused airframe wearing the scratch plane's other picks
(armour stars therefore track the bought armour units, exactly as the decoded formula reads the
record; the scratch plane is restored after pricing, never left edited by a read).

**The art seam C23-C26 inherit (the contract extension this item added, per Decision 4).**

- `CSVM/src/Mech3/TgaImage.cs` is the engine-free TGA decoder: types 2 and 10 (RLE), 24/32-bit,
  both row orders (TGA is bottom-up unless descriptor bit 5 is set), decoding to top-down RGBA8
  bytes plus dimensions. Anything outside that coverage, malformed or truncated decodes as null;
  `TryLoad` reads an absent file the same way, since hangar art is optional by design.
- `IHangarPage` gained one member: `HangarArt? Art { get; }`, a decoded `TgaImage` plus a
  caption, null by default (`HangarPage` supplies the null, so existing pages needed no change).
  A page wanting art overrides it; the shell draws at most one such block per screen.
- `HangarFlow` takes an optional third constructor argument, the folder `extracted/` sits in,
  exposed as `Flow.DataRoot`. Null (tests, a missing extraction) reads as no art anywhere.
- `LaunchMenu.cs` was touched for exactly this and nothing else (the serial lane): `OpenHangar`
  passes `_dataRoot`, and `Rebuild` draws `Page.Art` under the detail line through
  `HangarArtControl`, a fixed-height letterboxed `TextureRect` with the caption under it, its
  texture rebuilt only when the page hands over a different image; `LayoutScale` counts the
  block so the screen still fits 720p. C23-C26 show art by overriding `Art` alone, with no
  further `LaunchMenu` edit; C25's live paint preview hands over its composed RGBA the same way.

This page's own art is the focused airframe's `PX_<af>_BLUEPRINT.TGA` (A2: 358x335, 24-bit RLE),
captioned with the airframe's name and cached per airframe, misses included. It follows the
cursor, not the pick, so browsing the roster previews each airframe.

Tests: `CSVM.Tests/TgaImageTests.cs` (synthetic files pin the BGR order, both orientations, both
RLE packet kinds and the null-on-malformed contract; extracted-data facts decode a real shipped
blueprint and icon at their catalogued shape) and `CSVM.Tests/HangarAirframePageTests.cs`
(`OffersAllElevenAirframes_NamedFromLangui`, `SteppingSelectsTheFocusedAirframe`,
`ChangingAirframe_PreservesTheOtherPicks`, `AcceptAdvancesWithoutEditing`,
`DetailShowsTheDecodedFigures`, `StarRatingsMatchTheEconomy`, `DetailLeavesTheScratchUntouched`,
`ArtIsNullWithoutADataRoot`, `ArtShowsTheFocusedAirframesBlueprint`).

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** Pick one of the 11 airframes: name (langui 3000+af), stat-table figures, agility/armour
star ratings by the decoded formulas, blueprint TGA if A2 confirmed it.

**Evidence (confidence: traced).** Stat table rows and rating formulas
([`org/hangar.md`](org/hangar.md)). Decision 9: all 11 airframes offered; the availability
threshold gates nothing in Instant Action.

**Approach.** A dropdown/list in C21's shell bound to the scratch def's airframe; changing
airframe re-derives dependent screens' bounds (turret slots, capacity) but preserves picks where
valid, matching the original's scratch-record behaviour.

**Model recommendation.** medium.

**Verify.** All 11 rows show the decoded cost/weight/capacity; the two star ratings match the
formulas for hand-checked airframes (Hoplite 4 stars agility, Balmoral bottom).

## C23 ☐ ENGINE and ARMOR screens

**Landed.** `CSVM/src/UI/HangarEnginePage.cs` and `CSVM/src/UI/HangarArmourPage.cs` fill the
Engine and Armour slots in `HangarFlow.PageFor` (the two switch lines; nothing else in the shell
changed, per the C21 contract).

The ENGINE screen is seven rows: engine ids 0-5 named from langui 3100+af*6+id (each
manufacturer's three displacements, then the same three with nitro, per A2) following the scratch
plane's airframe, and id 6 the no-engine row from langui 1171. Selection is the airframe page's
idiom: the pick shown ticked, the ←→ stepper making the focused row the scratch plane's engine
(a fresh build opens with the no-engine row ticked, since `CustomPlaneDef` defaults to engine 6),
Confirm advancing without editing. Each row's detail is the decoded cost and weight through
`HangarEconomy.EngineLine` (per-airframe base plus per-id offsets; the no-engine row prices at
zero). The original's power stat line is shown too: at landing the per-id factor table at
`0x00619e38` was unread and the agent rightly omitted the line rather than invent it; the
orchestrator then read the six doubles (0.9/1.0/1.1, then x1.33 nitrous: 1.197/1.33/1.463,
recorded in [`org/hangar.md`](org/hangar.md)) and folded `HangarEconomy.PowerStat` plus the
detail-line segment in as the landing's integration step.

The ARMOR screen is four rows, the zones in the record's own order (nose, tail, left wing, right
wing), each named through its own langui format 1191-1194 ("Nose: %1!d! units" and kin, which
carry the number themselves; a missing table falls back to the same shape in plain text). The
stepper walks the focused zone's units 0-12 with wraparound, the original's 13-row dropdown as a
cycle, writing the scratch def; Confirm advances without editing. The detail keeps the three
factors distinct: the units bought (through format 1170 when present, the original's dropdown
string), cost at units x4, weight at units x4, and the units x5 lb figure the original's dropdown
displayed, labelled as shown-only so the display factor never reads as a price.

Tests: `CSVM.Tests/HangarEnginePageTests.cs` (`OffersSevenRows_NamedFromLangui`,
`EngineNamesFollowTheAirframe`, `TheDefaultPickIsNoEngine`, `SteppingSelectsTheFocusedEngine`,
`AcceptAdvancesWithoutEditing`, `DetailShowsTheDecodedCostAndWeight`,
`DetailLeavesTheScratchUntouched`, and the extracted-data
`EngineNamesResolveForAllAirframes` sweeping all 11 airframes x 6 engines plus 1171) and
`CSVM.Tests/HangarArmourPageTests.cs` (`OffersTheFourZones_NamedFromLangui`,
`RowTextFallsBackWithoutStrings`, `EachRowEditsItsOwnZone`, `SteppingWrapsAtBothEnds`,
`DetailSeparatesTheThreeFactors`, `DetailUnitsUseFormat1170`, `AcceptAdvancesWithoutEditing`).

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** Engine: the six per-airframe engines (langui 3100+af*6+id) plus "no engine", with the
decoded cost/weight per id; armour: the four zones (langui 1191-1194), 0-12 units shown x5 lb.

**Evidence (confidence: traced).** Engine bases/offsets and armour handler
([`org/hangar.md`](org/hangar.md)).

**Approach.** Two screens in the shell over the scratch def; the armour dropdowns are the 13-row
x5 lb roster the original's callback 2246 builds.

**Model recommendation.** medium, low effort.

**Verify.** Engine names resolve for all 11 airframes x 7 rows; armour rows price at units x4 and
display units x5.

## C24 ☐ GUNS and HARDPOINTS screens (BL-067)

**Landed.** `CSVM/src/UI/HangarGunsPage.cs` and `CSVM/src/UI/HangarHardpointsPage.cs` fill the
Guns and Hardpoints slots in `HangarFlow.PageFor` (the two switch lines; nothing else in the
shell changed, per the C21 contract).

The GUNS screen is always four rows, whatever the airframe (the slot-count disproof: titles
vary, the count does not), each titled from the airframe's stat-table slot-title string
(`AirframeStats.SlotTitle` through langui, following the scratch airframe, so the Balmoral shows
Nose Turret and Rear Turret on rows 2 and 3) and showing the slot's pick. The ←→ stepper walks
the original's 11-entry dropdown (callback 2248) as a cycle: the five calibres single (langui
3310-3314), the same five twinned through the shared `HangarFlow.GunName` (the "(2) " prefix,
format 506, per A2's correction), then No Gun, which is langui 3315: 2248 names its rows
3310+type and the empty gun id is 5, so the empty row has its own string, with "No Gun" as the
plain fallback. Stepping writes the slot's `GunChoice` (calibre + twin; empty = null calibre);
Confirm advances without editing. The detail line is the slot's decoded cost and weight from the
gun table, the turret column when the airframe's turret bit marks the slot (0-based, no shift,
per A3 trap b), doubled for twin, plus the calibre's magazine (A3 trap d: CLUSTER_SIZE
2800/2400/2000/1600/1200 rounds, per gun so twinning does not change it), stated so the
calibre-versus-rounds trade-off is visible where the pick is made. An empty slot prices at zero
with no rounds figure.

The HARDPOINTS screen is two rows, one per wing, named through langui 1176/1177 ("Left Wing:
%1!d!" / "Right Wing: %1!d!", which carry the count themselves). The stepper walks the focused
wing's count 0-4 with wraparound, the original's 5-row dropdown (callback 2244) as a cycle,
writing the scratch def; Confirm advances without editing. The detail speaks that dropdown's own
vocabulary (1165 "None", 1168 "1 Hardpoint", 1169 "%1!d! Hardpoints") and prices it: the decoded
$410 / 480 lb per hardpoint, then the wing's line total.

Gun picks store calibre + twin only; resolution to weapon defs stays at build time (D32) via
A3's mapping. This is BL-067's landing site; its entry is deleted when this item and D32 both
land.

Tests: `CSVM.Tests/HangarGunsPageTests.cs` (`AlwaysFourRows_TitledFromTheStatTable`,
`RowTextFallsBackWithoutStrings`, `SteppingWalksTheElevenRowCycle`, `ElevenStepsReturnToEmpty`,
`EachRowEditsItsOwnSlot`, `TwinRowsCarryThePrefix`, `DetailShowsTheWingColumnAndRounds`,
`DetailUsesTheTurretColumn`, `DetailDoublesForTwin_ButNotTheRounds`,
`BiggerCalibreMeansFewerRounds`, `EmptySlotDetailIsZero`, `PickingAGunMovesTheBill` (the
purchase-screen verify: the bill prices the same scratch def the stepper edits),
`AcceptAdvancesWithoutEditing`, and the extracted-data `GunStringsAndSlotTitlesResolve` sweeping
3310-3315, 506 and every airframe's four slot-title ids) and
`CSVM.Tests/HangarHardpointsPageTests.cs` (`TwoRows_NamedFromLangui`,
`RowTextFallsBackWithoutStrings`, `EachRowEditsItsOwnWing`, `SteppingWrapsAtBothEnds`,
`DetailSpeaksTheDropdownVocabulary`, `DetailUsesLanguiWhenPresent`,
`AcceptAdvancesWithoutEditing`, and the extracted-data `HardpointStringsResolve`).

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** Guns: four slots titled from the stat table, each an 11-row dropdown (five calibres,
five twins prefixed "(2) " per string 506, No Gun), turret slots priced as turrets; hardpoints:
the two per-wing counts 0-4 with the original's row labels.

**Evidence (confidence: traced).** The 11-row model, slot titles, turret bits, hardpoint roster
([`org/hangar.md`](org/hangar.md)); the def mapping from A3.

**Approach.** Two screens over the scratch def; gun picks store calibre+twin, resolution to defs
happens at build (D32) via A3's mapping. This is BL-067's landing site; its entry is deleted when
this item and D32 both land.

**Model recommendation.** medium.

**Verify.** Slot titles match the stat table per airframe (Balmoral shows Nose Turret and Rear
Turret); twin rows carry the "(2) " prefix (string 506, per A2's correction); the PURCHASE
screen's gun lines move when picks change.

**⚠ Traps.** Do not model per-airframe slot counts; wrong-claim 1. A turret slot is a title and a
price column, not a different control.

## C25 ☐ PAINT and PLANENAME screens

**Landed.** `CSVM/src/UI/HangarPaintPage.cs` and `CSVM/src/UI/HangarNamePage.cs` fill the Paint
and Name slots in `HangarFlow.PageFor` (the paint switch line added, C21's in-flow placeholder
name page deleted and moved out to its own file; nothing else in the shell changed).

**The pattern index is a decode, not an ordering guess.** Record +0x40's 0-13 is a row of the
engine's 14-entry pattern-name table at `0x0060301c`, read out of `crimson.exe` and written into
[`formats/paint.md`](formats/paint.md): `blackhat`, `blckswan`, `blake`, `british`, `fortune`,
`hollywd`, `hughes`, `medusas`, `cccp`, `sactrust`, `german`, `studio`, `broadway`, `itstaxi`.
The four indices the fixture saves pin (blckswan 1, fortune 4, hughes 6, studio 11) all land on
their own name, and the same read settles airframe id to skin prefix: each airframe's shipped
`PX_ICON_<af>_<pattern>_*` sets are exactly the pattern list `PatternLibrary.PatternsFor` gives
that prefix, for all eleven. The PAINT screen's pattern row therefore steps the airframe's OWN
patterns (the Fury's four, the Balmoral's two), not a dense 0-13 grid the original never offers.
With no extraction there is no per-aircraft list to read, so the row walks the decoded table
itself rather than guessing a subset.

**The palette decision.** The three colour rows step an ordered palette rather than free RGB:
the twelve shipped schemes' own colour triples, deduplicated in first-appearance order, with the
table's (0,0,0) entries written as the (25,25,25) the paint UI's darkest shade actually saves
(the B12 fixture finding). Twenty-four colours, each labelled with the scheme slot it came from
("hughes 1" is the yellow off a Hughes Aviation plane), so every colour a pilot can reach is one
the original's artists authored. Free RGB stays the livery lab's business: it spans more than the
original's Colour x Shade dropdowns could, which is right for a debug tool and wrong for a screen
that is meant to be the original's. A colour the palette does not carry (an imported original
save, or the model's own (0,0,0) default on a fresh plane) is kept and shown as its own triple
until that slot is stepped, which then lands on an authored colour: no read of this screen
rewrites what was imported, and no screen writes the scratch plane on arrival.

**The preview is the live composite, not the icon art.** `PaintBitmap`'s three per-texel weight
masks blend the three colours, the pattern's shading map modulates that, its overlay composites
over it, and the `.BM`'s bottom-up rows are flipped: exactly `PlanePainter`'s formula
([`formats/paint.md`](formats/paint.md)), reimplemented over the raw masks because `PlanePainter`
itself produces a Godot `ImageTexture` from a `TextureArchive` and this page must stay engine-free.
The skin previewed is the airframe's wing where it has one and its fuselage otherwise (the Hoplite),
probed by name against the pattern's own folder. The composite reaches C22's art seam as a 32-bit
top-down TGA decoded back through `TgaImage`, since the seam speaks `TgaImage` and that file is
not this item's to extend. The preview is cached on (airframe, pattern, the three colours), so the
shell rebuilds its texture on an edit and on nothing else, and every scratch edit refreshes it.
⚠ The sparse-icon fallback is only the last resort: a pattern that ships no `.BM` for this
aircraft falls back to `PX_ICON_<af>_<pattern>_0.TGA`, and to pattern 4's set when that pattern
has no icons (A2's trap).

**The decal picks turned out unconsumable, so they are carried untouched.** Nothing in the remake
reads +0x5c/+0x60/+0x64 today (`PaintScheme`'s nose/tail/wing decals come from `vehicle.json`, and
no code maps a composite pick onto one), the `a*5 + b` encoding's per-pick meaning is still open in
the decode ([`org/hangar.md`](org/hangar.md) "Open"), and +0x64 has no screen handler in the
original at all. The screen edits only what the decode names: the pattern and the three colours.
The three picks survive a flow byte for byte, which a test pins.

**Name entry is per-character, and `LaunchMenu` was not touched.** The codebase's only text input
is the debug labs' `LineEdit`s (`NodeLab`, `AnimLab`, `AiNetsOverlay`), which are mouse-and-keyboard
tools, not a launchscreen idiom to reuse, so the pad-friendly reading of the item applies: one row
per character stepped through `A-Z 0-9 space -`, plus a trailing length row whose stepper adds a
character (seating an `A` under the cursor, which is then on that character's own row) and removes
the last. The cap is 32, the original's 33-byte name-index records. Every control is the same live
←→ stepper every other screen uses, so `Accept` still just advances and the blank-name gate stays
where C21 put it, at the commit (langui 203); the length row's detail line says so where it can
still be fixed. The alphabet carries no character a filename cannot hold, so
`CustomPlaneStore.PathFor`'s sanitisation never rewrites a name this screen produced; a lower-case
character from an imported save survives until its own cell is stepped.

Tests: `CSVM.Tests/HangarPaintPageTests.cs` (`OffersThePatternAndThreeColourRows`,
`WithoutALibraryThePatternRowWalksTheDecodedTable`, `ColoursStepTheShippedSchemesPalette`,
`EachRowEditsItsOwnSlot`, `SteppingWrapsAtBothEnds`, `AnOffPaletteColourIsKeptUntilStepped`,
`DetailNamesTheSchemeAColourCameFrom`, `TheCompositePicksAreCarriedUntouched`,
`AcceptAdvancesWithoutEditing`, `ArtIsNullWithoutADataRoot`,
`ThePreviewComposesTheMasksAndTheColours` (a hand-built 2x2 mask set whose expected pixels are
arithmetic, pinning the blend, the shading modulation and the bottom-up flip),
`EveryEditRefreshesThePreview`, and the extracted-data `ThePatternRowIsTheAirframesOwnList` and
`EveryAirframeComposesItsOwnPatterns`, the sweep that would catch a wrong airframe-to-prefix row)
and `CSVM.Tests/HangarNamePageTests.cs` (`OneRowPerCharacterPlusTheLengthRow`,
`TheLengthRowAddsAndRemovesCharacters`, `LengthStopsAtTheRecordsCap`,
`SteppingACellWalksTheAlphabet`, `TheAlphabetIsFilenameSafe`,
`ImportedCharactersSurviveUntilStepped`, `DetailMarksTheFocusedCharacter`,
`ABlankNameShowsTheOriginalsRefusal`, `TheEnteredNameIsWhatTheStoreSaves`).

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** Pattern, picks and three colours edited with a live `PlanePainter` preview; a name
entered and validated (the JSON's file identity).

**Evidence (confidence: traced for the paint machinery).** `Mech3/PatternLibrary` +
`Mech3/PlanePainter` and the livery lab already step the full paint system
([`formats/paint.md`](formats/paint.md)).
<TODO: the +0x64 third pick dword's meaning is open in the decode; the screen edits only what the
decode names (pattern + two picks + colours) until it is settled.>

**Approach.** Reuse the livery lab's composition path inside the shell; name screen is a text
entry with duplicate handling per B11's policy.

**Model recommendation.** medium.

**Verify.** The preview matches the livery lab's output for the same inputs; a saved plane
reloads with identical paint.

**⚠ Traps.** The shipped `PX_ICON_*` sets are sparse per pattern (A2): only pattern 4 exists
for every airframe. If the screen shows the icon art, fall back to pattern 4's set for a
pattern id with no icons; the live `PlanePainter` preview is the primary rendering and has no
such gap.

## C26 ☐ PURCHASE review screen

**Landed.** `CSVM/src/UI/HangarPurchasePage.cs` fills the Purchase slot in `HangarFlow.PageFor`
(C21's in-flow page deleted and moved out to its own file; the switch line was already there, so
the flow file only shrank).

The review's row set is dynamic the way the original's per-line callbacks are: one row per priced
thing the scratch plane actually carries, absent components getting no row at all. The airframe
row is always present (langui 3000+af); the engine row appears when the engine is not id 6
(3100+af*6+id); each armed gun slot rows exactly as the GUNS screen names it (the stat table's
slot title, then the shared calibre naming with its "(2) " twin prefix); each armour zone with
units rows through its own langui format 1191-1194; each wing with hardpoints through 1176/1177.
Every component row's detail is that line's decoded cost and weight off `HangarEconomy.Price`'s
bill (the gun rows therefore price the turret column where the airframe's turret bit says so,
doubled for twin), in the "$C   W lbs." shape every other screen prices in. Then the totals row
(1198), whose detail is the flow's own totals line, and the Purchase Now row (1199), whose press
runs `flow.Commit()` with the existing semantics: the name gate (203), then the verdict in the
original's words. A press on a review row is a no-op; the purchase screen is the last one, so
nothing advances past it either way.

**The gate is the button, not only the commit** ([`org/hangar.md`](org/hangar.md):
`PURCHASE.SCRIPT` disables `pur_b_purchase` via mail 10018 whenever the problems callback 2264
reports, with `pur_t_problems` carrying the text). The page mirrors that reading: whenever the
verdict is not Ok the Purchase Now row renders flagged ("✕  " prefixed) with the problems text
visible in its detail (1182 + 1227 OVERWEIGHT / 1171 No Engine Selected) before any press, and
`HangarPurchasePage.BuildEnabled` is the state a richer renderer could grey the row with. The
press on a flagged row still runs the commit, whose refusal is the same rule in the same words,
so the flag is a mirror of the gate and never a replacement for it.

**The persistent totals row (Decision 10).** `HangarFlow.TotalsLine` is the second stats line
every hangar screen shows: total price and weight against the airframe's capacity
("$4980   6420 / 7610 lbs."), recomputed from `HangarEconomy.Price` on demand and flagged with
"⚠ " plus the original's OVERWEIGHT word (1227) when over, with `TotalsOverweight` alongside as
the colour flag. `LaunchMenu.cs` was touched for exactly this (the C22-shaped serial lane):
`Rebuild` draws the line under the heading on `Screen.Hangar` in the detail font, error-coloured
when over, and `LayoutScale` counts the pair; nothing else in the layout moves.

Tests: `CSVM.Tests/HangarPurchasePageTests.cs` (`ABareBuildShowsOnlyAirframeTotalsAndBuild`,
`AnEngineRowAppearsWhenChosen`, `ArmedGunSlotsRowAsTheGunsScreenNamesThem`,
`ArmouredZonesRowThroughTheirLanguiFormats`, `WingsWithHardpointsGetTheirRows`,
`RowTextFallsBackWithoutStrings`, `TheTotalsRowJudgesWeightAgainstCapacity`,
`TheBuildRowIsFlaggedAndExplainsWhenBlocked`, `AnOverweightBuildFlagsWithTheOriginalsWord`,
`TheBuildRowCommits`, `AReviewRowPressDoesNotCommit`, `ABlockedBuildPressIsStillRefused`) and,
in `CSVM.Tests/HangarFlowTests.cs`, `TheTotalsLineCarriesPriceWeightAndCapacity` and
`TheTotalsLineFlagsOverweight`.

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** The itemised review: airframe, engine, per-zone armour, per-slot guns, per-wing
hardpoints, each with the decoded cost and weight, the two totals, and the gate verdict blocking
Build on overweight or no engine.

**Evidence (confidence: traced).** All figures from B13; row naming from the decoded purchase
callbacks ([`org/hangar.md`](org/hangar.md)).

**Approach.** A read-only list bound to B13's output over the scratch def, plus the Build button
that commits (save via B11, return via C21).

**Model recommendation.** medium, low effort.

**Verify.** Line items and totals equal B13's tested figures; the two gate failures show the
original's message strings (1227 OVERWEIGHT, 1171 No Engine Selected) and block the commit.

# Wave D — into flight

## D31 ☐ Custom planes in every human plane picker, with the after-build auto-select

**Landed.** One roster stands behind every human plane picker:
`CSVM/src/UI/PlanePickerRoster.cs`, engine-free, building the 11 stock airframes (the
launchscreen's curated order) then the store's saved customs in `CustomPlaneStore.List()`'s own
name-sorted order. A row is a `PickerPlane(Name, Node, CustomName)`: `CustomName` null on a stock
row and the store name on a custom, which is the identity every consumer distinguishes the two
by; `Node` on a custom is its airframe's STOCK `player_*` node (`AirframeNode`, ids 0-10 in the
stat table's row order, the Hoplite being `player_autogyro`).

- **The picker sites, enumerated.** The remake has exactly two places a human picks their own
  plane, both `Screen.Plane` in `LaunchMenu.cs`: the lone-pilot centred list (`Rebuild`/`Row`,
  which IA, Free Flight and Dogfight's chapter flow all share as their final step) and the
  splitscreen panes (`RebuildPanes`/`PaneBody`, one per joined seat). Both now draw
  `LaunchMenu._roster`; the cursor wrap (`HandleInput`), the stats detail, the AMMO SELECTION
  heading, the locked line and `FireLaunch` all index it. The third aircraft list, the Wingmen
  step's stepper, stays on the stock `Planes` table: wingmen are AI-flown, so Decision 6's
  "every human picker" does not reach it.
- **Refresh and auto-select.** `ShowMenu` and `CloseHangar` both call `RefreshRoster`
  (re-list the store, clamp every cursor into the possibly-changed list), so a save appears
  without a menu restart. On `HangarExit.Built`, `CloseHangar` looks the built name up in the
  refreshed roster (`PlanePickerRoster.IndexOf`, case-blind like the store's duplicate-name
  policy) and sets player 1's `PlaneIndex` to it: entered from the IA plane pick that is the
  visible cursor on return, entered from the Mode door it is where the pick opens later. The
  original selects index 11, the first custom slot, after a build (`gui_continue`); ours selects
  the just-built plane by name because our customs sort rather than filling slots.
- **The door still cannot be read as a plane.** The Build row now trails the customs at index
  `_roster.Count` (`PlaneRowCount`), still lone-pilot-IA-only (`HangarRowOnPlaneScreen`,
  C21's gating untouched). C21's clamp reasoning re-checked against the grown roster: the door
  Accept check is `PlaneIndex >= _roster.Count`, the door never locks or confirms so
  `FireLaunch` never indexes it, `RebuildPanes` clamps joining cursors to `_roster.Count - 1`
  (only the door is clamped away; a custom stays a valid pick in every pane, Decision 6), and
  `RefreshRoster` clamps to `PlaneRowCount - 1` so a cancelled flow keeps the cursor on the door.
- **⚠ The D32 seam, exactly.** A custom pick survives the menu layer as
  `LaunchMenu.PlayerChoice.CustomPlane` (the store name; null on a stock pick).
  `Launcher.StartSessionFromMenu` reads only `PlaneNode` and `Fit` today, so a picked custom
  launches as its airframe's stock plane, with the airframe's stock Ammo Selection list
  (`StockFitFor` resolves through the roster row's node). D32 reads `CustomPlane` there, loads
  the def from `CustomPlaneStore` and builds it into the spawned aircraft. The IA wizard's
  nominal `PlayerPlane` label gets the airframe's stock name for a custom
  (`NominalPlaneName`): the def's consumers speak ia.json's stock vocabulary.
- **Scripted paths untouched.** `--plane=`/`--det` name planes by node straight into
  `SessionSpec` (`ParsePlanes`) and never see the roster; no CLI lists customs, and the hangar
  itself remains unreachable outside interactive input (C21).

Tests in `CSVM.Tests/PlanePickerRosterTests.cs` (6 tests, 16 executed cases):
`StockRowsComeFirstInTheirGivenOrder`, `CustomsListAfterTheStockRows`,
`ACustomRowFliesAsItsAirframesStockNodeAndKeepsItsName`, `AirframeNodesMatchTheStatTableOrder`
(all 11 ids), `AirframeNodeClampsOutOfRangeIds`, `IndexOfFindsACustomByNameAndOnlyACustom`.

**Goal (original).** Every human plane picker (IA pilot plane, chapter flow, splitscreen seats)
lists saved customs after the 11 stock airframes; completing a build auto-selects the new plane
in the picker the hangar was entered from (the original's index-11 `gui_continue` contract).

**Evidence (confidence: traced for the contract).** The 11+customs sizing and index-11 select are
decoded ([`org/hangar.md`](org/hangar.md), the 1024/2099 count callbacks).

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

## D32 ☑ Building a custom plane into a flying aircraft

**Landed.** `CSVM/src/Flight/CustomPlaneBuild.cs` is the join, pure and engine-free: a saved
`CustomPlaneDef` plus the airframe's stock `LoadoutDef` in, a built `LoadoutDef`, a `PaintScheme`
and a `PlaneDamage` ledger out. The seam is `Launcher.StartSessionFromMenu`, which reads
`PlayerChoice.CustomPlane` back into a def through `CustomPlaneStore` and carries it on
`SessionSpec.MenuCustomPlanes` (one per pane, D31's `MenuLoadouts` pattern) for
`HumanFlightAdapter.Assemble` to build the aircraft from. A plane whose file went away between the
picker's listing and the launch warns and flies the stock airframe rather than refusing the
session.

- **Guns, per A3.** Calibre row c becomes `Caliber = 30 + 10c` with `WeaponId` left null, so
  `Loadout.Bind` resolves the `wep_30..73` matrix and the Ammo Selection layer composes on top
  unchanged (`LoadoutChoice.ApplyTo` runs over the built def, not over the stock one). A twin is
  ONE gun over `firepoint(9-2n)`/`firepoint(10-2n)`, a single takes the low one; `Turret` and the
  mount name come from the stock slot; an empty pick omits the slot entirely. ⚠ The stock slot's
  own marker list narrows the pair when the rig is short of it: the Kestrel has no `firepoint8`
  and its stock slot 1 says so by naming one marker, and binding an absent marker is a loud throw,
  so without that narrowing a twin pick there would spawn the plane unarmed.
- **The hardpoint join rule (the item's open TODO, now settled).** `Loadout.PylonFillOrder`
  (`{1,5,2,6,3,7,4,8}`) alternates the wings entry by entry, so its two interleaved halves ARE the
  wings: pylons 1-4 one side, 5-8 the other. A wing's count takes that wing's fill-order entries
  in order, hanging the stock fit's ordnance at each entry's fill index, and **caps at the pylons
  the stock fit authors for that wing** — the record stores counts and no weapons, so a pylon the
  stock fit never names has nothing to hang. Worked example: the Bloodhawk's three-pylon fit
  authors fill indices 0/1/2, so four-per-wing caps to pylons 1 and 2 on one wing and pylon 5 on
  the other. Unchosen entries keep their place as `LoadoutChoice.None`, since dropping one would
  slide every later pylon onto the other wing. ⚠ Which half is physically LEFT is still undecoded
  ([`formats/markers.md`](formats/markers.md) omits the pylon positions); a swap would be
  invisible except at the controls, which is D33's pass.
- **Armour, per A1, on the pools' own scale.** The four zones reach
  `PlaneDamage`'s `nose`/`tail`/`leftwing`/`rightwing` parts at five units per point, the record's
  own premultiply, and no other rescaling is needed: CSVM's `destroyable_parts` armour pools carry
  the shipped stock allocations 15/20/25/30/35/40 ([`formats/vehicle.md`](formats/vehicle.md)),
  which is the same scale the original's mission loader writes its raw x5 floats onto. Only the
  ARMOUR pool is set; structure stays the def's, as the original leaves the mission file's alone.
  Parts are copied rather than overwritten (a `PlaneStats` is shared by every plane of that
  airframe). The vehicle totals stay derived: no player def authors an `armor`/`health` pair, so
  the whole pair is the sum over the rebuilt zones, which is the original's own recompute. The
  player is never difficulty-scaled, and gets that for free — nothing in CSVM scales a human rig's
  pools at all.
- **Paint** is the record's pattern and three colours through the existing texture-substitution
  path (`PlaneBuilder`'s `scheme`, the entry point the livery lab drives). The three composite
  `a*5 + b` picks stay out: they register per-plane decal textures in the original, but the
  encoding is undecoded ([`org/hangar.md`](org/hangar.md)'s Open list), so the three decal slots
  keep their "leave the shipped placeholder" sentinel rather than inventing an index.
- **The name** is the plane's own on the stunt scoreboard, the race board and the best-time key
  (two builds on one airframe fly differently, so they rank separately). The targeting HUD still
  prints the airframe's name, which shows only in splitscreen, where one pilot brackets another's
  build; picked up as a follow-up rather than widened here.
- **⚠ Engine: wired as the orchestrator's integration step after the item landed inert.** The
  item agent held off correctly (at its landing the registry values looked unread); they turned
  out to be shipped data — [`org/hangar.md`](org/hangar.md) names `extracted/zrdr/engines.zrd.json`
  as the registry the trace ends in, base row per airframe (the `FUN_00416e10` map, now
  `CustomPlaneBuild.EngineRegistryBase`) plus the tier, scalars 0.23 to 1.28 — and CSVM already
  consumes that very table (`PlaneStats.EnginePower` is the airframe's stock Lvl-2 row,
  `FlightModel` multiplies thrust by it). So the wiring is the original's own override with
  authored values, nothing invented: `CustomPlaneBuild.EnginePowerFor` resolves the pick's tier
  row and `HumanFlightAdapter` swaps it in on a shallow `PlaneStats` copy
  (`WithEnginePower`; the shared per-airframe cache is never mutated). Stock pick (id 6) keeps
  the airframe's row, matching the original's id -1 no-override. **Nitrous stays inert** (the
  `veh+0x946` consumers are untraced) and the verbose launch line says so. The tier's flight
  effect is D33's to judge at the controls. **No dependency on total weight or the stat-table
  power rating exists anywhere**, per A1's two sourced dead ends.

Tests in `CSVM.Tests/CustomPlaneBuildTests.cs` (19 tests, the last two the orchestrator's
engine-wiring additions: `TheEnginePickResolvesToItsAuthoredRegistryRow`,
`EveryAirframesRegistryBaseRowExists`):
`ACalibreRowBecomesItsCaliberAndNeverAResolvedWeaponId`,
`ATwinTakesBothFirepointsAndASingleTheLowOne`,
`ASingleBarrelStockSlotNarrowsATwinPickToTheMarkerTheRigHas`, `AnEmptySlotIsOmittedEntirely`,
`TheTurretFlagComesFromTheStockSlot`, `EachWingsCountFillsThatWingsPylonsInTheFillOrder`,
`TheTwoWingCountsAreIndependent`, `ACountAboveTheStockFitCapsAtWhatItAuthors`,
`NoHardpointsBoughtHangsNothing`, `APylonCarriesTheStockFitsOrdnance`,
`TheAmmoLayerComposesOverTheBuiltDef`, `TheBuiltDefKeepsTheAirframeAndTakesTheCustomName`,
`ArmourUnitsLandOnTheZonesArmourPoolAtFivePerUnit`, `AZoneTheRecordDoesNotNameIsUntouched`,
`TheAirframesOwnPartsAreNeverMutated`, `TheVehicleArmourTotalIsTheSumOverTheBoughtZones`,
`ThePaintCarriesThePatternAndColoursAndNoInventedDecals`.

**Verified.** Closing orchestrator battery at D32: build 0/0, units 1861/1861, engine suites
90/90 with errors clean, 16 goldens hash-identical, exit 0.

**Goal (original).** A picked custom plane spawns and flies: the airframe's model and stock base,
A3's gun defs on the chosen slots, hardpoint ordnance capacity per the wing counts, paint applied,
name shown where planes are named, and armour/engine/weight wired per A1's findings (or explicitly
inert with the split-out BL minted).

**Evidence (confidence: direction-sound, traced for the armour/engine wiring).**
`LoadoutChoice.ApplyTo` was built to take any base; `PlaneBuilder` drives per-aircraft paint
substitution already ([`formats/paint.md`](formats/paint.md) "Implementing this in the
remake"). The armour and engine wiring is A1's traced shape ([`org/hangar.md`](org/hangar.md)
"Into the mission"): per-zone armour pools in nose/tail/leftwing/rightwing order, engine as
power tier + nitrous flag, and explicitly NO flight dependency on total weight or the
stat-table power rating. The join from `CustomPlaneDef` to a `LoadoutDef` base is new.
<TODO: how hardpoint COUNTS map onto the stock pylon set — the original stores counts, our
loadouts store per-pylon weapons; the join rule (first N pylons per wing?) is unread. Lead:
`FUN_00443de0`, the in-mission weapon wiring, is the one flight-side reader of the plane
record (+0x84 and the +0x88..+0xc4 runs) and is where the original makes this join.>

**Approach (original).** A `CustomPlaneDef -> LoadoutDef` builder beside A3's mapping; spawn path
otherwise unchanged. Wire A1's findings exactly as decoded, nothing more.

**Model recommendation.** high — the fidelity-bearing join of the plan.

**Verify.** Build a plane with a distinctive fit (one twin turret, asymmetric wings, loud paint),
fly it, and confirm every element at the controls; the 8-chapter `--freecam` regression stays
clean (no world-side change expected; a baseline first).

**⚠ Traps.** No invented armour/engine scalings (Decision 3); if A1 split out, the fields stay
inert and say so in the UI copy nowhere (silent, not fake).

## D33 ☐ The closing at-the-controls pass, and the BL-354/BL-067 closures

**Goal.** The owed hand-flown verification: build, purchase-gate, save, relaunch, reload, fly,
import a real original save if one exists; then BL-354 and BL-067 closed per the close ritual
(entries deleted, evidence in the closing commit).

**Evidence (confidence: n/a — this is the verification item).** Decision 8.

**Approach.** User at the controls with a short script of checks drawn from every C/D item's
Verify line; findings that are new work get minted as BLs, not fixed inline.

**Model recommendation.** medium — orchestration and record-keeping around a human pass.

**Verify.** This item is the verify. The plan completes only after it.

# Wave E — the first pass's findings

## E41 ☐ The airframe-defaults ask (string 206): default armour/engine/guns on the airframe pick, or keep

**Landed.** The ask is string 206's own question, rendered by the airframe page as an inline
two-row confirm in the launchscreen idiom (the flow has no modal machinery). The state lives on
the flow: `HangarFlow.DefaultsAsk` names the airframe whose defaults are on offer and
`DefaultsAskText` carries the formatted question, %1 the new airframe's name and %2 the plane
being built (its name once it has one, its previous airframe's name before that), captured when
the ask is raised. `StartNewPlane` raises it, so a new plane's first arrival on the AIRFRAME
screen opens on the confirm; `HangarAirframePage.Step` raises it when the pick changes to a
different airframe, with the switch itself standing either way. That is what makes Cancel keep
every current pick, exactly as 206's wording implies, and it is the pre-E41 behaviour the Wave C
walk-through tests now reach by declining. While the ask shows, the page draws OK and Cancel
with the question as the detail line and the new airframe's blueprint as art; Accept answers it
(`AnswerDefaultsAsk`), stepping is inert, and the cursor lands back on the chosen airframe.
`StartFromSaved` never asks: the plane already is what its builder chose.

- **Accepting loads the defaults** (`HangarFlow.LoadAirframeDefaults`): guns from
  `stock_loadouts.json` read back through the A3 mapping (stock caliber 30..70 becomes calibre
  row (caliber-30)/10, a two-marker slot is the twin mount, a slot the stock fit does not author
  is empty, so the Kestrel's single-barrel slot 1 defaults untwinned); hardpoint counts as the
  stock fit's authored pylons per wing (`StockWingCounts`, D32's `PylonFillOrder`
  interleaved-halves rule read backwards: the Balmoral's eight pylons load 4/4, the Hoplite's
  two load 1/1, the Kestrel's five load 3/2); engine id 1, the stock Lvl-2 tier that is
  `PlaneStats`' own stock registry row; armour as the airframe's stock zone allocations in units
  (the `destroyable_parts` armour pools / 5, read through `PlaneStats` off the flow's `ZrdrPath`,
  the same menu-side zrdr scope the plane picker's stats detail already reads). Paint and name
  are not the airframe's to default and stay as they are.
- **The sources are optional in the hangar's own missing-data idiom.** `HangarFlow` gained two
  optional constructor arguments, `StockFits` and `ZrdrPath`; `LaunchMenu.OpenHangar` passes its
  `Fits` table and `_zrdrPath` (the one line it changed). A missing stock table loads gun and
  hardpoint defaults empty; a missing or unreadable zrdr scope loads armour 0.

Tests: `CSVM.Tests/HangarAirframePageTests.cs` grew `ANewPlanesFirstArrivalRaisesTheAsk`,
`EditingASavedPlaneDoesNotAsk`, `SteppingTheChosenAirframeDoesNotAsk`,
`TheAskSpeaksString206WithBothNames`, `AcceptingLoadsTheAirframeDefaults` (the Balmoral worked
example), `DefaultsReadTheStockFitPerAirframe` (Hoplite and Kestrel),
`DecliningKeepsTheEmptyState_AndNoStockTableDegradesQuietly` and the extracted-data
`TheDefaultsCarryTheStockArmourAllocations`; `ChangingAirframe_PreservesTheOtherPicks` and
`SteppingSelectsTheFocusedAirframe` now go through the ask (decline = the old behaviour), and
every walk-through helper in the hangar test files declines it on the way past.

**Verified.** <pending orchestrator run>

## E42 ☐ Engine screen: the None row keeps its place, relabelled to the original's 1165

**Landed.** The engine screen's row 6 reads langui 1165 "None", the decoded dropdown's own
string: callback 2218 at `0x0040bce1` authors seven rows and names the last 1165. 1171
"No Engine Selected" keeps its decoded places, the purchase screen's problems text and the
commit refusal; `HangarFlow.EngineName` still answers it for id 6 and only the engine page's own
row label changed (`HangarEnginePage.RowText`). Tests updated in
`CSVM.Tests/HangarEnginePageTests.cs`: `OffersSevenRows_NamedFromLangui` pins "None" on row 6
and that 1171's wording no longer appears there; the extracted-data
`EngineNamesResolveForAllAirframes` asserts both ids resolve.

**Verified.** <pending orchestrator run>
