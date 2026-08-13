# What is `group` (aiv roster slot 4)?

Instrument: `aiv_group_slot.py` (read-only; run from the extraction root). It censuses slot 4
across every per-mission AI vehicle table and correlates shared group values with net, team,
target, spawn proximity, ace presence, and the activation flags. The binary half was read out of
`crimson.exe` with Ghidra; function addresses below.

This answers plan item B7's re-scoped question ([`docs/PLAN-M4-ai.md`](../../docs/PLAN-M4-ai.md)):
after slot 6 turned out to be `primary_target`, slot 4 was the last data candidate for a formation
mechanism. **It is not one.**

## Verdict: `group` is a mission-logic cohort id, not a formation

Slot 4 tags a roster block with a small integer so that scripts, the Instant Action wave sequencer,
and the fighter-launch generators can address a set of vehicles at once. Nothing in the engine's
steering, targeting or mode logic reads it; no read site positions one aircraft relative to
another. Formation flying does not live here, and B7 closes disproven.

## The data (all 414 blocks, 53 missions)

- Value histogram: `{0: 124, 1: 118, 2: 70, 3: 34, 4: 26, 5: 30, 6: 9, 7: 2, 8: 1}`. Small
  per-mission ordinals, `0` the default.
- Per-mission there are 1 to 8 distinct values; 71 shared groups (2 or more members) and 63
  singletons.
- Correlations over the 71 shared groups: **same team 71/71** (a group never crosses teams),
  same `primary_target` 47/71, same patrol net only 37/71, spawn spread within 500 m only 31/71
  (spreads run 0 m to 12.8 km), and an ace anchoring mooks on only 5/71. A flying formation
  predicts tight spawns, one net and a leader; the data shows none of those reliably.
- Group 0 is the at-mission-start population: 111/124 ship `deactivated 0` (slot 21), and in
  campaign missions it is the team-1 cohort (player, wingmen, the `devastator_*` escorts).
  Higher groups ship dormant: group 1 is 69/118 `deactivated 1`, group 2 is 68/70, group 3 is
  28/34. Later cohorts sit parked until something releases them, which is exactly what the
  binary's read sites do.

## The binary mechanism (crimson.exe, x86 retail)

The chain from file to consumer:

| Address | Role |
|---|---|
| `FUN_00439530` | Loads `<cx>/<mission>/zrdr/aiv.zrd` and constructs one `CCEVeh` record per block |
| `FUN_00437620` | `CCEVeh` read: slot 4's token (offset `0xc + 8*4` in the parsed field list) -> record `+0x38`. Team (slot 3) lands at `+0x34`, `enabled` (5) at `+0x3c`, `primary_target` (6) at `+0x44` |
| `FUN_0047c210` | The roster spawn (the same function that applies `init_health`/`armor`): copies record `+0x38` onto the live vehicle instance at `+0x388` |
| `FUN_00452850` | The `egen` generator parser: the generator's `vehicle.group` key -> generator record `+0x64` |

Every read of instance `+0x388` in the executable, and what it does:

- **`FUN_004658d0` / `FUN_00465850`: script wake-up by group** (reached from the mission-script
  layer, `D:\zipper\Crimson\mission.cpp`). For every living vehicle whose group matches the
  argument, the activation volumes are widened to 9000 m (radius squared `8.1e7`, altitude band
  9000 both ways), i.e. the vehicle is woken regardless of player distance, and the survivors are
  counted. `FUN_00465910` is the script condition built on it: wake group N, true when at most M
  members remain (optionally adding a named generator's remaining capacity to the count).
- **`FUN_0045b9d0`: the Instant Action wave sequencer.** A global current-group counter
  (`DAT_00718cd0`): when no living enemy of the current group remains, it increments the counter,
  picks a stored spawn point at least 500 m from the player (`250000` = 500 squared), teleports
  every member of the new group there fanned 100 m apart at 45-degree offsets, and reactivates
  them (`FUN_004b0f40(0)`, the inverse of the `deactivated` flag the spawn applied). `ia.zrd`'s
  `group1`..`group4` keys (parsed at `FUN_00459390` via the `group%d` string at `0x00625640`)
  author those waves.
- **`FUN_00452450`: the generator launch.** A generator finds a parked vehicle whose group
  matches its own `vehicle.group` (`+0x64`), reactivates it, places it at the generator's origin
  node with launch attitude (pitch -pi/2) and a 22.35 m/s drop velocity. `group` is the join key
  between an `egen` generator and the roster vehicles it launches.
- **`FUN_004659b0` / `FUN_00465a70`: objective conditions.** Count living members of a group
  inside or outside a radius of a point (the group id read from the objective record at
  `+0x598`).

There is no fifth consumer. In particular the AI update, target ranking and maneuver logic never
touch `+0x388`.

## Where formation behaviour actually lives (leads, not chased)

- The wingman/escort look of the original is carried by nets and targets, not by `group`: nets
  whose trailer names `player` follow the player ([`docs/formats/ai-nets.md`](../../docs/formats/ai-nets.md)),
  and 27 roster blocks author `primary_target player`.
- The decoded mode dispatch (`patrol`, `pursue`, `lay off`, ...) has no formation or wingman mode.
- If a station-keeping behaviour exists at all, the net-follower is the remaining place to look
  (B5's territory); nothing in this decode required one.

## Side finding for the F20 `capacity` puzzle

`FUN_0045b9d0`'s mission-type-2 branch **adds the new group's member count to a generator's
remaining capacity** (generator `+0x80`) and stamps the generator's group (`+0x64`) with the
current wave. That is a runtime top-up of the very counter the 2026-08-10 pass reported as having
only three writes (load, decrement, reset). It lives in the Instant Action module, not the
generator module, which is presumably how it was missed. It explains how zeppelins launch fighters
in Instant Action with `capacity 0`; whether campaign missions have an equivalent feed was not
chased here.
