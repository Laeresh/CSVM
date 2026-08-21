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

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Airframes differ in gun/hardpoint slot counts, held somewhere in the executable" (BL-067's framing) | The prospecting decode: `GUNS.SCRIPT` builds 4 dropdowns in a fixed loop, `HARDPOINTS.SCRIPT` 2, no callback gates either; the stat table's 11 dwords are fully accounted for and none is a count. Every airframe is 4 gun slots + 2 per-wing hardpoint groups; what varies is slot titles, turret bits and weight capacity ([`org/hangar.md`](org/hangar.md)). |
| 2 | "Record +0x34/+0x38 are the two hardpoint ordnance picks" (this plan's own first decode pass) | The same prospecting pass: they are the left/right wing hardpoint COUNTS, 0-4 each (dropdown 2244, langui 1165/1168/1169). Corrected in paint.md the same day. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced** | A2, A3 (inputs), B12, B13, C21-C26, D31 (the data they render/compute is decoded with addresses) | Confirm against `org/hangar.md` / `formats/paint.md`, then build. |
| **Direction sound** | B11, D32 | The record and spawn path are decoded; the CSVM-side shape (JSON schema, builder wiring) is ours to design. |
| **Leads only** | A1 | The consumers of armour/engine/weight past the spawn descriptor are untraced; this may end in a partial disproof or a split-out BL. |

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

1. ◐ Trace the spawn-descriptor consumers: armour units, engine id and total weight into flight and damage
2. ☑ Asset and string inventory: the blueprint/icon TGAs and every langui roster the screens need
3. ☑ The gun mapping: calibre + twin + turret onto `weapons.zrd` defs, and how the ammo layer sits on top

### Wave B — model, persistence, economy

11. ☑ `CustomPlaneDef`: the CSVM model and its JSON persistence under `user://`
12. ☐ The 204-byte importer, tested against real saved-plane files
13. ☐ The economy component: costs, weights, totals, and the capacity/engine gate

### Wave C — the screens

21. ☐ Hangar shell and navigation: screen order, IA Build entry, top-level entry
22. ☐ AIRFRAME screen
23. ☐ ENGINE and ARMOR screens
24. ☐ GUNS and HARDPOINTS screens (BL-067)
25. ☐ PAINT and PLANENAME screens
26. ☐ PURCHASE review screen

### Wave D — into flight

31. ☐ Custom planes in every human plane picker, with the after-build auto-select
32. ☐ Building a custom plane into a flying aircraft
33. ☐ The closing at-the-controls pass, and the BL-354/BL-067 closures

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

## A1 ☐ Trace the spawn-descriptor consumers: armour units, engine id and total weight into flight and damage

**Goal.** A sourced statement of what the original does with the record's armour units (+0x74),
engine id and total weight (+0x3c) once the spawn message leaves `FUN_00414f40`: which flight or
damage quantities they feed and by what arithmetic, so D32 wires facts rather than inventions.

**Evidence (confidence: lead-only).** The spawn path is decoded to the message boundary:
`FUN_00417090` packs the 17-dword descriptor mirroring the record's own offsets and
`FUN_00414f40` repacks it as a type-8 message handed to `FUN_0041a320` ([`org/hangar.md`](org/hangar.md)).
Past that queue nothing is traced. The stat table carries an engine power rating (+0x08 of
`0x00619d98`) and the star-rating formulas use armour units, but no combat consumer of either has
been found. This may legitimately conclude "cosmetic in Instant Action" for some field; a sourced
null result is a valid landing.

**Approach.** Fresh-context decode agent, read-only on the Ghidra project, starting from the
type-8 message consumer out of `FUN_0041a320`'s queue and from xrefs to the per-plane structures
the spawn fills. Bound it to the three named fields. If the consumer web exceeds a wave's worth of
work, stop, mint the split-out BL with the leads recorded, and let D32 fall back to
chosen-but-inert per Decision 3.

**Model recommendation.** high — an open-ended binary trace where a wrong reading poisons D32.

**Verify.** Every reported constant carries its address and applying condition; the write-up lands
on [`org/hangar.md`](org/hangar.md) as a new section before D32 consumes it.

**⚠ Traps.** Do not fall back to footage or feel to fill a gap the trace leaves; an unrun decode
stays an open question (this project's standing rule). Do not confuse the hangar's star-rating
formulas (`FUN_0040faf0`, display only) with combat consumers.

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

**Verified.** <pending orchestrator run>

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

**Goal.** Pick one of the 11 airframes: name (langui 3000+af), stat-table figures, agility/armour
star ratings by the decoded formulas, blueprint TGA if A2 confirmed it.

**Evidence (confidence: traced).** Stat table rows and rating formulas
([`org/hangar.md`](org/hangar.md)).
<TODO: decision — the stat table's availability threshold (+0x14) is campaign progress; recommend
ignoring it in Instant Action (all 11 offered) but this was not grilled.>

**Approach.** A dropdown/list in C21's shell bound to the scratch def's airframe; changing
airframe re-derives dependent screens' bounds (turret slots, capacity) but preserves picks where
valid, matching the original's scratch-record behaviour.

**Model recommendation.** medium.

**Verify.** All 11 rows show the decoded cost/weight/capacity; the two star ratings match the
formulas for hand-checked airframes (Hoplite 4 stars agility, Balmoral bottom).

## C23 ☐ ENGINE and ARMOR screens

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

**Goal.** Every human plane picker (IA pilot plane, chapter flow, splitscreen seats) lists saved
customs after the 11 stock airframes; completing a build auto-selects the new plane in the picker
the hangar was entered from (the original's index-11 `gui_continue` contract).

**Evidence (confidence: traced for the contract).** The 11+customs sizing and index-11 select are
decoded ([`org/hangar.md`](org/hangar.md), the 1024/2099 count callbacks);
<TODO: enumerate the remake's actual picker sites in `LaunchMenu.cs` and splitscreen join UI —
not surveyed this session.>

**Approach.** One roster provider (stock + B11's listing) consumed by every picker; selection
carries an id that distinguishes stock from custom.

**Model recommendation.** medium.

**Verify.** A saved plane appears in all pickers; building from IA returns with it selected; a
splitscreen seat can pick it.

## D32 ☐ Building a custom plane into a flying aircraft

**Goal.** A picked custom plane spawns and flies: the airframe's model and stock base, A3's gun
defs on the chosen slots, hardpoint ordnance capacity per the wing counts, paint applied, name
shown where planes are named, and armour/engine/weight wired per A1's findings (or explicitly
inert with the split-out BL minted).

**Evidence (confidence: direction-sound).** `LoadoutChoice.ApplyTo` was built to take any base;
`PlaneBuilder` drives per-aircraft paint substitution already
([`formats/paint.md`](formats/paint.md) "Implementing this in the remake"). The join from
`CustomPlaneDef` to a `LoadoutDef` base is new.
<TODO: how hardpoint COUNTS map onto the stock pylon set — the original stores counts, our
loadouts store per-pylon weapons; the join rule (first N pylons per wing?) needs A1/A3 input or a
decode note.>

**Approach.** A `CustomPlaneDef -> LoadoutDef` builder beside A3's mapping; spawn path otherwise
unchanged. Wire A1's findings exactly as decoded, nothing more.

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
