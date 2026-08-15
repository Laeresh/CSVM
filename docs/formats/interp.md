# `interp.zbd` — the engine boot scripts (`.gw`)

`interp.zbd` (extracted by `unzbd cs interp` to a single `interp.json`) holds the game's
**engine boot scripts**: 98 named command lists, one per `support\…\*.gw` file of the
original build tree. They are what the engine runs to *assemble* a world — set search paths,
load models, register clutter templates, define the partition grid, and then, per mission,
switch off everything that mission does not show.

This page documents the script format and the two families this project consumes. The
clutter half is described in more detail in [clutter.md](clutter.md).

## At a glance

This page is the current reference for its documented format family.

## At a glance

This page is the current reference for its documented format family.

## Container shape

`interp.json` is a flat JSON array of script objects:

```json
[ { "name": "support\\c1\\ia1.gw",
    "datetime": "2000-08-26T08:08:30.0Z",
    "lines": ["FindNode hk_zep", "NodeSetActive off", …] } ]
```

`name` is the original build-tree path (backslash-separated, always lower case);
`lines` is the command list in execution order. One command per line, space-separated,
no quoting or escaping anywhere in this install.

## Script families

| Name | Count | Role |
|---|---|---|
| `support\*.gw` | 11 | Engine-global setup (display, cockpit, gamez/planes loading, texture effects) |
| `support\<chapter>\init.gw` | 8 | Per-chapter search paths + soils |
| `support\<chapter>\load.gw` | 8 | Builds the chapter world: `LoadGameGen` model loads, `AddChild` into `world1`, partition grid, fog, the sun light |
| `support\<chapter>\adjust.gw` | 8 | `AddClutterTemplates` — the clutter registry ([clutter.md](clutter.md)) |
| `support\<chapter>\tex_fx.gw` | 8 | `CycleTextureSet*` — material flipbook setup |
| `support\<chapter>\<mission>.gw` | **53** | **Per-mission world setup — see below** |
| `support\util\*.gw` | 2 | Offline authoring utilities, not runtime |

Chapters are lower-cased (`c1`, `c1b`, `c2b`, …) and missions match the mission folder
names (`ia1`, `m02`, `mp3`), so the script for a mission is exactly
`support\<chapter>\<mission>.gw`.

### Build vs. run — the `USEZBD` split

The same script tree is both the engine's asset **compiler** and its **loader**, switched
by preprocessor defines in `support\main.gw`. Without `USEZBD`, the `load.gw` scripts
`LoadGameGen` the raw `..\data\**\*.flt` art tree into a scene DB and `GameZWriteZBDFile`
writes the chapter's `gamez.zbd` — that path owns `mkdir`, `PrintUsedTextures` and the
`COMPILE` define, and it is dead in a retail install, which ships no `..\data\` tree.
`support\planes.gw` builds `planes.zbd` the same way, running `util\planesurgery.gw` once
per aircraft to split `cockpit1` out of the model into a `player_*` root with `geometry` +
`cockpit1` children — the shape the readers see. With `USEZBD` (retail runtime), the engine
reads `gamez.zbd` + `planes.zbd` instead. `adjust.gw` (the clutter registry) and the
per-mission `<mission>.gw` run **in both paths** — they are the only genuinely runtime
scripts, and exactly the subset this project consumes.

## The per-mission scripts — which entities a mission shows

**This is the mechanism that decides world entity presence, and it settles a question the
readers cannot answer.** A chapter's `gamez.zbd` contains *every* one of its missions'
content — all the zeppelins, the CTF props, the boats, the AA guns. The engine loads all of
it, then runs the mission's `.gw`, which switches off what this mission does not want.

C1/IA1's script deactivates 29 nodes, among them `hk_zep` (the Hollywood Knights zeppelin
moored at the tether tower), `multiplayer1zep`/`multiplayer2zep`, `piratezep`,
`workersvoyagezep`, `redcross`, the nine `lifesaver*` props, five AA guns, the rearm bay
and all four CTF props. C1/M04's script does **not** name `hk_zep` — which is exactly why
that zeppelin is on the field in M04 and nowhere else.

Polygons removed from each chapter's Instant Action by its own script:

| Chapter | Nodes off | Polygons | Largest items |
|---|---:|---:|---|
| C1 | 29 | 11,822 | `piratezep` 2792, `multiplayer1zep`/`2zep` 1779 each, `hk_zep` 1272 |
| C1B | 11 | 8,646 | `piratezep`, `vostokzep` 1820, both MP zeppelins, `freighter` |
| C1C | 2 | 3,558 | both MP zeppelins |
| C2 | 19 | 9,549 | `piratezep`, `cargozep2` 1861, MP zeppelins, `sprucegoose` 283 |
| C2B | 5 | 8,162 | `piratezep`, `geminizep` 1796, MP zeppelins |
| C3 | 45 | 10,897 | MP zeppelins, `britbalmoral_1..3` 726 each, 6 boats + turrets, 9 `studebaker*` |
| C4 | 28 | 13,122 | `blackhatzep` 1850, three `cargozep*`, MP zeppelins |
| C5 | 15 | 15,185 | six named zeppelins + both MP zeppelins |

Every chapter parks `multiplayer1zep` **and** `multiplayer2zep` in its world and switches
both off outside multiplayer — so ignoring these scripts leaves two phantom zeppelins in
every single Instant Action map.

### Vehicles load unplaced — the origin is the map corner

Every chapter's `support\<chapter>\load.gw` loads its vehicles with a bare `LoadGameGen` +
`AddChild %worldName%` and **no placement**, so every zeppelin, car, boat, train car and
aeroplane in the install ships with gamez `transform: "Initial"` and sits at the world
origin. Every chapter's world `area` is x,z ∈ [−N, 0], so the origin is the map's
**corner**. Each mission then either switches the vehicle off in its own `.gw` setup script
or places it from the animation layer — an `ON_STARTUP` `OBJECT_TRANSLATE_STATE` (C3/IA1's
`cgzepstate` puts `cargozep1` at (−12412.9, 134.0, −10424.8)) or an `OBJECT_MOTION_FROM_TO`.

Retail data misses some: **C5/IA1 leaves `piratezep` and `sprucegoose` on**, and
**C1C/IA1** — whose `ia1.gw` is four lines naming only the two MP zeppelins — leaves
`piratezep`, `blackswanzep` and `workersvoyagezep` parked at the corner. (Measured across
all 8 chapters, the transformless walk roots whose built world AABB straddles the origin in
x and z are 122 nodes — all vehicles, zero terrain.)

### Capture the Flag

`ctf_1`/`ctf_2` (gate posts) and `cs_flag_1`/`cs_flag_2` (the flags) exist in the five
chapters that ship an MP2 mission (C1, C2, C3, C4, C5) and are switched **off by every
mission script except `mp2.gw`**. That is the whole CTF gate — no roster file is involved.
`targets.zrd.json` in `MP2/zrdr/` references the same names, but it is the objective list,
not the spawn signal.

### Command vocabulary (mission scripts)

Across all 53 mission scripts, only ten verbs appear. The selector is stateful: `FindNode`
picks a node by name, `FindSubNode` narrows to a descendant of it, and the next verb acts on
that selection.

| Verb | Uses | Meaning |
|---|---:|---|
| `FindNode <name>` | 1215 | Select a node by gamez name (the `.flt` suffix is used interchangeably) |
| `NodeSetActive on\|off` | 1125 | Activate / deactivate the selection's subtree |
| `Object3DSetScroll on\|off <u> <v>` | 68 | **Set a texture scroll rate** on the selection's model — see below |
| `WorldPartitionSetActive on\|off <x1> <z1> <x2> <z2>` | 25 | **`NodeSetActive` applied by area**, not to the selection — see below (C3 only) |
| `Object3DTranslate <x> <y> <z>` | 14 | Reposition the selection |
| `Quit` | 13 | End of script — always the last line (C4/C5 scripts only) |
| `DeleteTree <name>` | 12 | Remove a subtree outright (C3 only) |
| `Object3DRotate <x> <y> <z>` | 11 | Re-orient the selection |
| `FindSubNode <name>` | 8 | Narrow the selection to a descendant |
| `FindSubNodeNode <name>` | 2 | Same selector, different spelling |

Semantics worth knowing:

- **Order matters, last write wins.** C3/M02's script sets `cargozep1` on and then off; C4's
  scripts alternate `bhf`/`bhfplug`. A set-and-forget map of name → state is wrong.
- **A `FindNode` that matches nothing is tolerated**, and the shipped scripts rely on it:
  C3's script names `blackhatzep` and `blackswanzep`, neither of which is in C3's gamez.
  Never treat an unresolved name as an error.
- **`DeleteTree` names its own target** rather than acting on the current selection, and no
  script re-activates a name it deleted (checked over all 53).
- **`WorldPartitionSetActive` ignores the selection too** — it takes a rectangle in world XZ
  and toggles every node the partition grid indexes inside it. It is the *same* toggle
  `NodeSetActive` performs, reached by area instead of by name — see below.
- **`Object3DTranslate`/`Object3DRotate` place the selection** (`MissionSetup`, BL-249, consumed
  translate is a plain absolute position, applied through the same
  parent-frame convention `OBJECT_TRANSLATE_STATE` uses. **`Object3DRotate`'s angle unit is
  ambiguous per script, not globally**, and is decided once per script by magnitude
  (`MissionSetup.RotateAsRadians`): any component whose absolute value exceeds 2π marks that
  whole script's `Object3DRotate` statements as degrees, otherwise they are radians. C1/M05's
  nine uses are small integers that only make sense as degrees (`0 45 0`, `0 172 0`); C3/MP1
  and MP2's two uses are high-precision values paired with a high-precision translate
  (`-0.000010 -3.144009 -0.000000`, i.e. π on Y) that only make sense as radians — the two
  families are cleanly separable by the 2π threshold, so a per-script decision costs nothing a
  global guess would have gotten right and fixes the one case (mixed units across scripts) a
  global guess cannot. No mission this project defaults to (an IA1) uses either verb, so the
  goldens cannot catch a wrong guess here — verified instead by targeted `--freecam` captures at
  C1/M05 (boats/`redcross`/`workersvoyagezep` at their authored positions and headings) and
  C3/MP1 (`cargozep1` at ≈π). Cross-ref `BL-034`: the same question over animation-layer
  `OBJECT_3D_ROTATE`/`OBJECT_ROTATE_STATE` data must not be resolved differently there.

## `Object3DSetScroll` — the second source of texture scrolling

The gamez *model* carries a `texture_scroll` field ([gamez.md](gamez.md)), and the boot
scripts also set scroll rates at load time. C1's own `ia1.gw` ends with:

```
FindNode wf01_water
Object3DSetScroll on 0.0 -0.4
FindNode wf01_edge
Object3DSetScroll on 0.0 -0.4
```

— so the C1 waterfall scrolls its texture at −0.4 v/second even though its gamez
`texture_scroll` is `{0,0}`. All 75 uses (68 in mission scripts, 7 in the chapter
`tex_fx.gw` scripts) are the `on` form; no script ever turns a scroll off.

**The verb writes the selected node's MODEL scroll field.** That is the reading the data
forces, and it explains why the two sources disagree only where they must:

| Where the statement lives | In the shipped gamez? |
|---|---|
| chapter `tex_fx.gw` (7 uses) | **Yes — identical values.** C1's `h_zone1scroll` is 0.07 in both; C1B's `con_scroll` −1.0, `eb_wakefront` 1.0, `wakefront_left`/`_right` 0.7. |
| per-mission `<mission>.gw` (68 uses) | **No** — the six C1/C4 waterfall leaves are `{0,0}` in the gamez. |

A chapter-level script runs once per chapter, so its writes could be (and were) baked into
the chapter's saved gamez; a per-mission script cannot be, because one gamez serves every
mission. Only the mission-level statements therefore have to be applied at load, and the
chapter-level ones are pure redundancy in this install — the single statement that is *not*
redundant, C4/`tex_fx.gw`'s `waterfall01 0.0 -0.5`, names a group node with no model of its
own, and every C4 mission script then sets that waterfall's two leaves to −0.4 directly. So
it is unobservable whether the verb also recurses into a subtree.

`MissionSetup.ScrollByModel` resolves each statement to a gamez
model index and hands the table to the world build, because the rate has to be known while
the material is created (a scrolling model can share its material with static geometry — see
`SceneBuilder`'s cache key in `docs/architecture.md`). Every scroll target in this install is
a model used by exactly one node, so per-model and per-node granularity cannot disagree here.

## `WorldPartitionSetActive` — `NodeSetActive`, selected by area

The `crimson.exe` control flow establishes this behavior. Reproducible at the addresses
named: verb dispatch `FUN_005b80a0` (the interpreter — all ten verbs are matched there by
`strncmp`), rectangle walk `FUN_004db790`, and the shared toggle `FUN_004cca30`.

**The verb is a bulk `NodeSetActive`.** `FUN_004db790(world, on_off, x1, z1, x2, z2)` converts
the rectangle to partition-grid cell indices, then calls `FUN_004cca30(node, on_off)` for every
node every covered cell indexes. `NodeSetActive` calls **the same function**, with the same two
arguments — Ghidra recovers its authored name from an assertion inside it, `gwNodeSetActive`,
at `D:\zipper\gamez\zclass\Class.c:1315`. There is no second visibility system here: both verbs
write bit 2 (`0x4`) of the node's flag word at `+0x24`, set on `on` and cleared on `off`. Node
kinds 1/2/5/6 get only that; kind 9 additionally tests `*(node+0x38)+0xe0 & 0x200` and calls
`FUN_004cf8b0`; kind 10 delegates to `FUN_004e0b90`; anything else logs "Unrecognized" and
returns error 3.

⚠ **This is not the ground-LOD mechanism**, a claim [`docs/HISTORY.md`](../HISTORY.md)'s M2
polish-4 entry still makes ("we draw both because the original selects between them via
partition visibility"). The verb appears in no C5
script at all; C5's coarse/fine ground selection is the **subface flag**
(`analysis/item9-depth-bias/CBLOCK-LOD.md`, and `BL-250` for the clutter side).

The grid it walks hangs off `world + 0x38`: origin floats at `+0x34` (x) / `+0x38` (z),
reciprocal cell size at `+0x84` / `+0x88`, cell counts at `+0x98` (x) / `+0x9c` (z), and a
row-pointer table at `+0xa0` with a cell stride of `0x58`. Each cell holds a `short` count at
`+0x3a` and a pointer at `+0x3c` to 12-byte entries whose first dword is the node.

Two traps for anyone reimplementing it:

- **Corner order does not matter.** Each coordinate becomes
  `floor((coord - origin) * reciprocalCellSize)`, is clamped to `[0, count-1]`, and the code
  then explicitly swaps min/max on both axes. C3 authors the second corner below-left of the
  first (`WorldPartitionSetActive on -10240 -2048 -2048 -6144`) and the engine normalises it.
- **The rectangle is half-open in cell space.** Both loops are strict `<` against the max cell
  index, so the last row and column are excluded — and **a rectangle that lands inside a single
  cell toggles nothing at all**. An inclusive `<=` over-selects by one row and one column.

All 25 uses are `support\c3\*.gw`, and they resolve to just three distinct rectangles:
`(-10240,-2048)→(-2048,-6144)`, `(-8192,-6144)→(-2048,-8192)` and
`(-15360,-7168)→(-8192,-14336)`. Every `off` sits in a story mission, so IA1 is unaffected in
all eight chapters — which is why the remake has been able to skip the verb so far.

Three sibling verbs exist in the same dispatch and appear in no shipped script: `WorldPartition`
(`FUN_004daa20`, sets the grid up), `WorldPartitionInclusionTolerance` and
`WorldPartitionMaxDECFeatureCount`.

**Not consumed.** `GameZ.cs` already parses the World node's `partitions` array, but it
dedups every cell into one flat `PartitionNodes` list behind a `HashSet<int>`, discarding the
per-cell membership a rectangle query needs; the source JSON still carries it.
`MapEdgeExtender` already has the world-position → cell-index helper.

## Relationship to the animation definitions

The `.gw` scripts and the `zepstate` animation definitions ([anim-definitions.md](anim-definitions.md))
do the same *kind* of thing — switch world objects off per mission — through two different
systems, and both are needed:

- `.gw` runs at world load, names nodes directly, and covers the entity families
  (zeppelins, CTF props, vehicles, guns).
- `zepstate` runs as an `ON_STARTUP` animation definition and covers scenery
  (`dliner1`, `cargotrain`), and is gated by the mission's compiled `mis_anim` manifest.

They overlap deliberately: C1/M02 hides `hk_zep` in *both* its `.gw` and its `zepstate`.
Load order is world → `.gw` → animation bootstrap, so an animation state can override a
script state.

**Corrects an earlier reading.** This project previously hypothesised that entities were
absent-unless-a-roster-spawned-them, with `aiv.zrd.json` and `zeppelins.zrd.json` as the
rosters. Both are wrong: `aiv.zrd.json` is the AI vehicle table (its only mention of
`hk_zep` anywhere is inside a wingman's target-priority list in C1/M02), and
`zeppelins.zrd.json` is the flyable-zeppelin gameplay config, which never names `hk_zep` in
the one mission that shows it. Entities are present by default and switched off by the boot
script — the same polarity as `zepstate`, not the mirror image of it.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
