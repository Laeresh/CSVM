# aiv.zrd, maneuvers.zrd — AI rosters, pilot skills & the maneuver library

Part of the [format documentation](README.md). The per-mission AI roster (`aiv.json`), the shared
maneuver library (`maneuvers.json`), and the `ai_skill_parameters` block in `player.json` that turns
a pilot's 1–9 skill ratings into engine constants. Companion pages: [ai-nets.md](ai-nets.md) (the
patrol graphs a roster entry references by id), [mission-entities.md](mission-entities.md)
(zeppelins, generators), [vehicle.md](vehicle.md) (the per-airframe AI tuning keys).

**Provenance.** Unlike most pages here, the field *names* on this page are not inferred — they are
read from `crimson.exe`, which embeds its editor's own text-format comment for the roster file at
`.rdata:0x00622508` (an unused writer header retained in the retail build). Every name below is that
comment's, verbatim. Field *positions* and *values* are then confirmed against the shipped
extraction; where the two disagree this page says so. See
[the field-list gap](#the-three-unnamed-slots) for the one place they do.

## At a glance

This page is the current reference for its documented format family.

## `<Cx>/<mission>/zrdr/aiv.zrd` — the AI vehicle roster

One file per mission directory (53 in this install), **414 vehicle blocks** total. The root list is:

```
[ header, ["nodename", [ …flat fields… ]], … ]
```

The header (element 0) is a positional `(slotId, designerLabel)` list, **one pair per vehicle block,
in block order**. Labels mix character names with param-set names (`Eairg31_params`) that `egen.json`'s
`vehicle.params` references ([mission-entities.md](mission-entities.md)).

⚠ **Blocks are not fixed-width — trailing fields are simply omitted.** Field-count histogram across
the 414: `{42: 20, 65: 1, 66: 33, 67: 14, 68: 39, 81: 307}`. A reader that assumes 81 throws on a
quarter of the data; read defensively and treat a missing slot as unset.

### Field table

Index, name (the exe's), and what the shipped data shows. `-1` is the near-universal "unset" marker.

| idx | name | notes |
|---|---|---|
| 0 | `netids` | patrol-net id into the chapter `neindex` ([ai-nets.md](ai-nets.md)); `-1` = none. The exe comment calls it a **list** and the reader agrees: `FUN_0047c210` (`0x0047c733`) takes the single id when the count is 1 and **`rand() % count`** when it is higher, drawn once at spawn. This install only ever authors a scalar, so the draw never fires. ⚠ **A `-1` here is not "no orders", it selects a different AI behaviour entirely** ([`org/aiPilot.md`](../org/aiPilot.md)); in the shipped campaign the only netless blocks are `player` and the wingmen (below) |
| 1 | `(x y z)` | spawn position — the only list-typed slot, present on all 414 |
| 2 | `yaw` | spawn heading, degrees |
| 3 | `team` | |
| 4 | `group` | **mission-logic cohort id, not a formation** (see [below](#group-is-a-cohort-id-not-a-formation)). Values 0-8; `0` (the default) is the at-mission-start population |
| 5 | `enabled` | |
| 6 | `primary_target` | an assigned target node name. 6 distinct: `""` (346), `player` (27), `devastator_1/2/3`, `piratezep`. The engine's own debug readout prints it as "Primary target: %s". ⚠ **Its meaning depends on `mode`:** on a `jet` it is a targeting assignment, but on a netless `wingman` it is the **formation leader**, and the escort law flies a fixed offset from it ([`org/aiPilot.md`](../org/aiPilot.md)). Corrects the earlier "not a formation leader" reading, which was right about `jet`s and wrong about wingmen |
| 7 | `init_health` | starting health override; `0.0` = use the airframe default. Real values do occur (e.g. `216.0`) |
| 8–19 | the activation/attack/return volumes | 12 slots for the 9 named `{active,attack,return}_{rad,u,l}` — see [below](#the-three-unnamed-slots). `rad` is a radius, `u`/`l` an upper/lower altitude band |
| 20 | `title` | `MSG_*_NAME` display key, resolving in `messages.json` ([missions.md](missions.md)) |
| 21 | `deactivated` | |
| 22–30 | **the skill vector** | `dare_devil natural_touch sixth_sense dead_eye quick_draw steady_hand stun_recovery talker constitution` — see [below](#the-skill-vector) |
| 31 | `pref_engage_alt` | **preferred engagement altitude in metres**, not a radius; `-1.0` on 384, else 350 / 1100 / 1500 / 1550 / 1600. Spelled `preferred_engagement_altitude` in `vehicle.json`, which is also the fallback when this slot is `-1.0` (`basic_airplane` authors 300.0). ⚠ **It is a maneuver-selection weight, not an altitude order:** its one reader (`FUN_004201a0`, `0x004204da`) adds 1.0 to a candidate evasive maneuver's weight when the aircraft is on the wrong side of it. Nothing steers toward it ([`org/aiPilot.md`](../org/aiPilot.md)) |
| 32 | `signature_maneuvers` | **bitmask over the maneuver library** — see [below](#signature_maneuvers-is-a-bitmask) |
| 33 | `rating_biases` | target-selection weights: a list of `[nodeNamePattern, bias]` pairs, wildcards allowed (`["fuel_truck*", -1.0]`). List on 321 blocks, null on 93; 1–9 entries (697 total); biases run `-1.0`…`1.0`, both signs, `-1.0` on 389. ⚠ The exe's comment admits a third element per entry, but this install authors none (0 across all 414 blocks, measured 2026-08-13) — read defensively, preserve a third raw if one ever appears |
| 34 | `nitro` | |
| 35 | `engine` | engines.json row id ([vehicle.md](vehicle.md)) |
| 36 | `otherTarget` | |
| 37 | `objectiveTarget` | |
| 38 | `categoryLabel` | |
| 39 | `helpLabel` | the objective-kind key — `MSG_OBJ_FOLLOW` / `MSG_OBJ_DESTROY` / `MSG_OBJ_DEFEND` |
| 40 | `taxiPath` | |
| 41 | `stickiness` | |
| 42 | `bait` | |
| 43 | `pilot` | pilot def name (`P_Wingman` and friends; see `pilots.zrd`) |
| 44–55 | `sclp sclr scly limp limr limy` + `esclp esclr escly elimp elimr elimy` | per-axis **scale** and **limit** factors on the AI's control output — pitch/roll/yaw, then the `e`-prefixed *emergency* set. These are `vehicle.json`'s `ai_input_*` / `ai_emerg_input_*` at roster scope. `-1.0` (which is what all 414 blocks author) means "fall through to the def". ⚠ **The def and the runtime hold these in roll/pitch/yaw order, not this file's pitch/roll/yaw** — the spawner transposes, slot by slot. Both orders are real; see [aiControlLaw.md](../org/aiControlLaw.md#where-the-gains-come-from) for the slot-to-offset table and the exact fallback |
| 56 | `attack_time_factor` | |
| 57–64 | `anose hnose atail htail aleft hleft aright hright` | **per-zone armour + health**, in `(armor, health)` pairs over the four damage zones nose / tail / left / right — the same zone set and the same armour-first two-pool model as the player's `destroyable_parts` ([vehicle.md](vehicle.md#the-hp-pair-armor--hit-points)) |
| 65 | `accentID` | **the voice id** → row in `voice.zrd` → `soundsh/VO_id<N>_*` clips |
| 66 | `armor` | |
| 67 | `ace` | |
| 68–71 | `pattern decal1 decal2 decal3` | livery ([paint.md](paint.md)) |
| 72–80 | `r1 g1 b1 r2 g2 b2 r3 g3 b3` | livery colours ([paint.md](paint.md)) |

**31 of the bare-number slots are constant across all 414 blocks** (8–19, 36, 43–56, 58, 60, 62, 64) —
authored defaults, not signal.

### Who is netless: the player and the wingmen, nobody else

Census of all 53 `aiv.zrd.json` files, 414 blocks, 2026-08-15. `netids` is `-1` on exactly 106 of
them and every one is the player or a wingman:

| node name | blocks | `netids` |
|---|---|---|
| `player` | 53 | `-1` |
| `wingman_N` | 50 | `-1` |
| `bswingman_N` | 3 | `-1` |
| everything else (enemies, `patrolboat_N`, `t_truck_N`) | 308 | a real net id, every one |

This is not a curiosity of the data, it is the switch that selects the AI behaviour. `wingman` and
`bswingman` are also the two `vehicle.json` defs (besides the eleven Instant Action `w<plane>` ones)
that author `mode wingman`, and a netless `wingman` flies a formation station on its
`primary_target` instead of a patrol graph. A netted one is demoted to `jet` at spawn and flies the
graph like everything else. [`org/aiPilot.md`](../org/aiPilot.md) has the mechanism and the
constants.

### `group` is a cohort id, not a formation

Decoded 2026-08-13 (plan item B7; instrument and function addresses in
`analysis/m4-b7-group-slot/`). Slot 4 tags a block with a small integer so that mission logic can
address a set of vehicles at once. The executable has exactly four consumers of the value, and
none of them is flight behaviour:

- **Script wake-up.** The mission-script layer wakes every living member of a group by widening
  its activation volumes to 9000 m, and its companion condition tests "at most N members of group
  G remain" (`FUN_004658d0` / `FUN_00465850` / `FUN_00465910`).
- **Instant Action waves.** `ia.zrd`'s `group1`..`group4` keys define waves; when the current
  group is wiped out, the sequencer advances a counter, teleports the next group's members to a
  point at least 500 m from the player (fanned 100 m apart) and reactivates them
  (`FUN_0045b9d0`).
- **Generator launches.** An `egen` generator's `vehicle.group` key selects which parked roster
  vehicles it launches; `group` is the join key between the two files (`FUN_00452450`,
  parser `FUN_00452850`).
- **Objective conditions.** Count living members of a group inside or outside a radius of a point
  (`FUN_004659b0` / `FUN_00465a70`).

The data agrees: a shared group value never crosses teams (71/71 shared groups) but only weakly
shares a net (37/71) or spawn proximity (31/71), and later groups ship `deactivated 1` (group 2:
68 of 70 blocks) waiting to be released. The steering, targeting and maneuver code never reads
the field. Formation-looking behaviour in the original rides nets whose trailer names `player`
([ai-nets.md](ai-nets.md)) and `primary_target`, not this slot.

### The three unnamed slots

The exe's comment lists **78** fields; the shipped format has **81**. The surplus is localised
exactly: `primary_target` is confirmed at 6 and `title` at 20, so the three extra slots fall inside
**8–19**, the volume block — 12 slots where the comment names 9 (`{active,attack,return}` ×
`{rad,u,l}`). The natural reading is a fourth value per volume, and slot 11 is the one
integer-typed slot among eleven floats, which fits a per-volume flag.

⚠ **Unresolvable from this install, and it does not matter.** All twelve slots are `0.0` in every
one of the 414 blocks — the volumes are never authored, so every AI falls back to the airframe's
`activation` / `attack` / `return_range` in `vehicle.json` and to `player.json`'s
`min_ai_active_dist` (2000 m). Do not spend time on it; do not invent values for it.

**Closed 2026-08-10 — treat them as inherited padding.** The likeliest explanation is that they are
a remnant: this engine is a descendant of Zipper's earlier `mech3` lineage (the same lineage the
extraction toolchain targets — [extraction.md](extraction.md)), and a record layout that outlived
the fields it was written for is exactly what a carried-over roster format looks like. That is a
hypothesis and this page does not assert it. What *is* established is enough to act on:

- the exe's own editor comment — the authoritative field list — **names nine, not twelve**;
- all twelve are `0.0` across every block in the install, so nothing reads a meaningful value;
- the fallback path they defer to is fully documented and independently sourced.

**A reader must still parse twelve slots** to keep the following field indices aligned — that part
is load-bearing. Beyond preserving positions, ignore them. This question is closed and should not
be reopened without new evidence (a different build, or an authored non-zero value).

### The skill vector

Slots 22–30 are nine consecutive integers valued `-1` (unset) or **1–9**, in the exe's order:

| slot | stat |
|---|---|
| 22 | `dare_devil` |
| 23 | `natural_touch` |
| 24 | `sixth_sense` |
| 25 | `dead_eye` |
| 26 | `quick_draw` |
| 27 | `steady_hand` |
| 28 | `stun_recovery` |
| 29 | `talker` |
| 30 | `constitution` |

They are populated as a complete nine-value vector on exactly **29 blocks** — precisely the named
aces. `<Cx>/IA1/zrdr/ia.zrd`'s `ace_stats` key independently fixes the count at nine
(`[9,9,9,9,9,9,9,9,9]`, in all 8 chapters), confirming 9 is the ceiling.

Three shipped cases corroborate the ordering, each landing on a different slot:

- **`MSG_CABBIE_NAME`** (a Manhattan cab autogyro) reads `9 6 6 3 3 6 9 7 8` — its two lowest values
  sit on `dead_eye` and `quick_draw`. A cabbie who cannot shoot.
- **`MSG_STUNT_PLANE_NAME`** reads all 1s except a **9 on `steady_hand`** — a show plane that flies
  its routine unbothered.
- **89 generic blocks** carry `-1` in eight slots and a lone **`1` on `dead_eye`** — mooks that can
  barely shoot, which is the observed gameplay.

The vector also scales with fame across a recurring antagonist's appearances (the Black Swan reads
6s at first contact and all nines in the final encounter). ⚠ **It is not monotonic in chapter-directory
order** — the chapter directories are not story order.

⚠ **Three of the design's twelve pilot stats are not in this vector**: preferred engagement altitude
is slot 31, signature maneuvers is slot 32, and there is no signature-*approach* field at all.

### `ai_skill_parameters` — what a 1–9 rating actually means

`extracted/zrdr/player.zrd.json` carries an `ai_skill_parameters` block: **one `[value@1, value@9]`
pair per stat**, the endpoints the rating interpolates between.

| key | @1 | @9 | governs |
|---|---|---|---|
| `daredevil_chance` | 0.35 | 0.99 | probability of taking an available Danger Zone run |
| `sixth_sense_chance` | 0.45 | 0.71 | passing the test to follow a target's maneuver (a failure leaves the AI stunned) |
| `sixth_sense_factor` | 0.994 | 1.07 | ~~the ease-off factor applied while being pursued~~ **decoded 2026-08-15 (`D31`): a flat multiplier on the AI's three stick channels, applied every frame on the non-emergency path** ([aiControlLaw.md](../org/aiControlLaw.md#the-skill-scalar-and-how-a-1-to-9-rating-interpolates)). Not conditional on being pursued |
| `dead_eye_angle` | 4.0° | 1.45° | half-angle of the aiming-error cone around the lead point |
| `quick_draw_angle` | 50° | 89° | half-angle of the cones off the target's nose/tail within which a shot is taken |
| `quick_draw_chance` | 0.05 | 0.44 | probability of taking a marginal shot |
| `steady_hand_chance` | 0.5 | 0.08 | probability of breaking off into Evade after absorbing damage |
| `stun_recovery_interval` | 4.8 s | 0.6 s | how long the pilot flies straight after being stunned |
| `talker_chance` | 0.25 | 0.95 | probability of actually playing a triggered voice line |
| `constitution_chance` | 0.35 | 0.95 | bail-out probability once shot down |

Notes that matter to anyone implementing this:

- ~~**Only the two endpoints are decoded.**~~ **Traced 2026-08-15 (`D31`), and the working assumption
  was the right shape with the wrong origin.** The engine computes
  `value = lo + (hi − lo) · rating · 1/9` (`FUN_0047c210` at `0x47d0c1`–`0x47d101`, the constant at
  `0x608028` being exactly `0.11111112`). The endpoints therefore sit at rating **0 and 9**, not 1
  and 9: a 9 yields `hi` exactly, but a 1 yields `lo + (hi − lo)/9`, not `lo`. Two of the ten pairs
  are confirmed on this path by name (`sixth_sense_chance` → `obj+0x970`, `sixth_sense_factor` →
  `obj+0x974`); the other eight are assumed to share it, since one interpolation site serves the
  block. **Corrected 2026-08-15 (`E42`): `AiSkills.At` now computes `rating/9` directly** rather
  than `(rating-1)/8`, matching the engine at every rating rather than only at 9.
- **The scale is 1–9 and nothing else.** Ratings are an index into this table; there is no 0–100
  scale anywhere in the shipped data. (The original *design document* gives a Danger-Zone poll
  interval of `100 − DareDevil` seconds, which only type-checks on 0–100. That formula is design-era:
  applied literally to shipped 1–9 data it yields a 91–99 s interval regardless of pilot — a stat
  with no effect. The shipped `daredevil_chance` pair replaces it.)
- **`natural_touch` has no entry, by design.** It is compared directly against a maneuver's own
  `natural_touch` difficulty (below), so both sides are already on the same 1–9 scale and no
  interpolation is needed.
- **Two stats improve downward** (`dead_eye_angle`, `steady_hand_chance`) — a tighter cone and a
  lower break-off chance are the *better* pilot. Do not normalise the direction away.
- `sixth_sense` and `quick_draw` each spend two parameters; the other seven spend one.

The engine's own debug strings name each of these tests in pass/fail terms and settle the
vocabulary, which the design document contradicts itself on: *"Absorbed %f damage; steady hand test
**failed**. Evading."* / *"…test **passed**. Not evading."*, and *"AI has been evaded. Sixth sense test
failed; AI now stunned."*

## `zrdr/maneuvers.zrd` — the maneuver library

One shared reader; a flat alternating `name, [properties…]` list of **17 maneuvers**. Each is a
timed control program plus a difficulty gate — the library is *data*, not code.

| `natural_touch` | maneuver | steps | flags |
|---|---|---|---|
| 0 | `nitro_evade` | 1 | `autogyro_allowed`, `nitro` |
| 1 | `rudder_turn` | 1 | `autogyro_allowed`, `relative` |
| 2 | `roll` | 1 | `relative` |
| 2 | `bank_turn` | 4 | |
| 3 | `climb` | 1 | `autogyro_allowed`, `bias -0.5` |
| 3 | `dive` | 1 | `autogyro_allowed`, `bias -0.5` |
| 4 | `jinking` | 4 | `autogyro_allowed`, `relative` |
| 5 | `scissors` | 6 | |
| 5 | `rolling_scissors` | 10 | |
| 6 | `snap_roll` | 2 | |
| 6 | `barrel_roll` | 1 | `relative` |
| 7 | `immelman` | 4 | |
| 7 | `loop` | 5 | |
| 8 | `split_s` | 4 | |
| 8 | `spiral_dive` | 2 | |
| 9 | `lag_pursuit_roll` | 3 | |
| **99** | `high_yo_yo` | **0** | |

- **`steps` is a list of `[duration_s, pitch, yaw, roll]`** — a target attitude in degrees held for a
  duration. `climb` is one step of `[4.0, 60, 0, 0]`; `dive` mirrors it at `-60`; `rudder_turn` is
  `[0.0, 0, 50, 0]`; `roll` is `[3.0, 0, 0, 90]`. A `0.0` duration reads as "advance as soon as the
  attitude is reached" rather than "hold for zero seconds" — every multi-step maneuver mixes zero
  and non-zero durations.
- ⚠ **Two steps carry 7 elements, not 4**: `barrel_roll`'s single step and `spiral_dive`'s second
  are `[duration, pitch, yaw, roll, 0.5, 0.0, 1.0]` — three extra numbers, identical on both, on
  exactly the two corkscrew maneuvers. Undecoded (a rotating-input candidate); a reader must
  accept them and should preserve them raw rather than interpret or drop them.
- **`relative`** — the step attitudes are relative to the current orientation rather than absolute.
- **`autogyro_allowed`** — the maneuver is legal for autogyros (5 of 17 are).
- **`bias`** — a fixed adjustment to the maneuver's selection rating; only `climb`/`dive` carry it,
  both `-0.5` (deprioritised).
- ⚠ **`high_yo_yo` is shipped disabled** — difficulty 99 is unreachable against a stat capped at 9,
  and it has no `steps` list at all. It is a stub. Do not implement it and do not "fix" its
  difficulty; there is no authored behaviour behind it.
- ⚠ **`nitro_evade` is difficulty 0**, i.e. always available, and is the only maneuver flagged
  `nitro`.

The difficulty column matches the original design document's own 1–9 table exactly for the fourteen
maneuvers the two have in common. `nitro_evade` and `high_yo_yo` are shipped-only additions the
design text does not list.

### `signature_maneuvers` is a bitmask

`aiv` slot 32 is a bitmask over the maneuver library, weighting the marked maneuvers up during
selection. ⚠ **The bit order is the exe's internal table order, which is NOT the order the JSON file
lists them in**:

```
0 roll            4 dive              8 snap_roll     12 lag_pursuit_roll  16 spiral_dive
1 rudder_turn     5 jinking           9 immelman      13 high_yo_yo
2 bank_turn       6 scissors         10 loop          14 nitro_evade
3 climb           7 rolling_scissors 11 split_s       15 barrel_roll
```

17 entries = bits 0–16, and the shipped values fit exactly: singles run 1…65536 (65536 = bit 16 =
`spiral_dive`), and the composites decode sensibly — `2064` = bits 4+11 = `dive` + `split_s`,
`32896` = bits 7+15 = `rolling_scissors` + `barrel_roll`, `2048` = `split_s` alone (the Black Swan's
signature). `0` on 174 blocks = no signature maneuver.

## AI modes, engine-side

Not a format, but decoded from the same binary and load-bearing for anyone reading this data. The
engine's debug readout dispatches on a single mode field with these states:

`patrol` · `pursue` · `lay off` · `evade` · `evasive maneuver` (running a library entry, reported as
`"%s" natural touch %d/%d, step %d/%d`) · `stunned` · `avoid crash` · `approaching danger zone` ·
`navigating danger zone`.

⚠ **`lay off` is a first-class mode, not a hidden fudge.** It is the "let the player catch up"
behaviour — the same idea as `sixth_sense_factor` — and being a distinct mode makes it directly
observable and switchable rather than something buried in the steering maths.

⚠ **At most one AI per frame can be laying off.** The combat driver's break-off branch is gated on
a global (`DAT_0064ee4d`) that the world tick clears once a frame and the first AI through the
branch sets ([aiControlLaw.md](../org/aiControlLaw.md#dat_0064ee4c-and-dat_0064ee4d)). The steering
of the mode itself is that page's subject; the mode dispatch is `obj+0x358`, not this file.
⚠ **These nine are not one stored field**, and the readout does not dispatch on one.
[`aiPilot.md`](../org/aiPilot.md) "The per-frame AI update" has the decode: `patrol`, `pursue` and
`lay off` are derived from whether a target is selected and from the AI *task* at `+0x2f0`, while
the five interrupt states live in a separate enum at `+0x358`.

The same readout recomputes the **target ranking** inline, which fixes its shape:
`rank = weight × 1200 + distance + objectiveBias`, minimised. The weight starts at **1.0 for any
target except the player, which starts at 0.7** (the "rank the player last" rule as a hard
constant), then takes ±0.2 adjustments for bearing, for altitude sign, and for whether the target
faces the AI, plus two class terms. Targets outside the activation volume score `1e21` (i.e.
excluded).

⚠ **The two class terms are decoded, and this paragraph used to name them wrongly.** It read "+0.4
for one dynamics class, and −0.5 for one structure case", both read off the readout's labels rather
than off the ranking function. [`aiPilot.md`](../org/aiPilot.md) "Target acquisition" has the
decode:

- **+0.4** applies to a candidate whose **`mode`** is `wingman` (the field at `+0x67c`, which the
  debug overlay labels `Dynamics:`; see [`vehicle.md`](vehicle.md) and `aiPilot.md` "`mode`, the
  dynamics class"). It is not a dynamics or airframe class, and minimisation makes it 480 m
  *against* the candidate, so the engine de-prioritises enemy wingmen.
- **−0.5** applies only to a **zeppelin gasbag**, reached through the `Target` virtual at vtable
  `+0x1c` that just three of the four candidate classes hard-wire to false. It is worth 600 m in
  the gasbag's favour, and nothing else in the game takes the term.

⚠ **`objectiveBias` is not in metres and not scaled by 1200.** `rating_biases` resolves through
`FUN_0041ae40` to rank units directly: `bias × −750`, with `≥ 1.0` collapsing to `−100000` (always
target), `≤ −1.0` returning the `1e21` exclusion (**never** target), and a turret taking a flat
`+37.5` on top of every arm, including the no-match case. So an authored `−1.0` is a hard
exclusion, not a penalty.

⚠ **The ±0.2 terms are aircraft-only.** There are two scorers, chosen on the *scoring* vehicle's
`mode`: `jet` and `wingman` take all three, and every other mode scores on base weight and the two
class terms alone.
