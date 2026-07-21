# `interp.zbd` — the engine boot scripts (`.gw`)

`interp.zbd` (extracted by `unzbd cs interp` to a single `interp.json`) holds the game's
**engine boot scripts**: 98 named command lists, one per `support\…\*.gw` file of the
original build tree. They are what the engine runs to *assemble* a world — set search paths,
load models, register clutter templates, define the partition grid, and then, per mission,
switch off everything that mission does not show.

This page documents the script format and the two families this project consumes. The
clutter half is described in more detail in [clutter.md](clutter.md).

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
| `Object3DSetScroll on\|off <u> <v>` | 68 | **Set a texture scroll rate** on the selection — see below |
| `WorldPartitionSetActive on\|off <x1> <z1> <x2> <z2>` | 25 | Toggle a rectangular region of the partition grid (C3 only) |
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
- **`Object3DRotate`'s angle unit is ambiguous** and unresolved. Nine of the eleven uses are
  small integers that only make sense as degrees (`0 45 0`, `0 172 0`); the other two are
  high-precision values paired with a high-precision translate (`-0.000010 -3.144009
  -0.000000`, i.e. π on Y) that only make sense as radians. No mission this project defaults
  to uses either verb, so the question has not had to be settled.

## `Object3DSetScroll` — texture scrolling is set here, not in the gamez

The gamez material carries a `texture_scroll` field, but the boot scripts *also* set scroll
rates at load time, and the two do not agree. C1's own `ia1.gw` ends with:

```
FindNode wf01_water
Object3DSetScroll on 0.0 -0.4
FindNode wf01_edge
Object3DSetScroll on 0.0 -0.4
```

— so the C1 waterfall scrolls its texture at −0.4 v/second even though its gamez
`texture_scroll` is `{0,0}`. Any work on scrolling surfaces has to read both sources: 68
uses live in mission scripts and 7 more in the chapter `tex_fx.gw` scripts.

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
