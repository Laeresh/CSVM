# Enemy generators � `egen.json`

Part of [Mission entities](../mission-entities.md) in the [format documentation](../README.md).

## `egen.json` � enemy generators

An **enemy generator** spawns AI aircraft into a live mission from a host entity. The host is
either a zeppelin (fighters dropped out of its hangar) or a fixed installation — this install
has ground airfields `eairg31`/`eairg32`, a ship `eshipg31`, and a submarine `barracuda`.

| Key | On | Meaning |
|---|---|---|
| `node` | 23/23 | the host world node |
| `vehicle` | 23/23 | nested: `params` (a designer label in the `aiv.json` HEADER's `(slotId, label)` pairs, e.g. `Eairg31_params`; authored on 15 of 23, see the typo note below), `nets` (one or more AI net names), `choose_nets` (`cyclic` throughout) |
| `capacity` | 23/23 | `0` throughout — a lifetime spawn budget, decremented per launch. ⚠ **`0` does not obviously mean "unbounded"** — see [the capacity puzzle](#the-capacity-puzzle) |
| `max_active` | 23/23 | concurrent live spawns (1/4/5/6/10) |
| `wave_size` | 23/23 | planes per wave (1, once 3) |
| `wave_period` | 23/23 | seconds between waves (1–20) |
| `ind_period` | 23/23 | seconds between individuals inside a wave (0.5–10) |
| `zeppelin` | 17/23 | `[1]` — marks the zeppelin-hangar variant |
| `open_anim` / `close_anim` | 17/23 | the hangar-door animations, run before and after a wave |
| `origin` | 17/23 | the node the fighters appear at — `cargobay` on 16 of 17 |
| `rotation` | 17/23 | `[-90, 0, 0]` throughout — the drop attitude. Read as **three** angles (all converted to radians), not the single value the data suggests |
| `min_altitude` | 17/23 | **100–300 m: the launch gate** |
| `healthy` | 1/23 | the node whose destruction stops the generator (`subhealthy`, the submarine) |
| `moving_path` | 1/23 | bare flag |

**`min_altitude` is confirmed by the design document by name.** The design specifies that a
zeppelin drops fighters through its hangar door and so must be high enough to do it; that the
generator gets an extra altitude parameter for this; that generation is *held* until the
altitude is reached; and it names the file — `egen.zrd`. The `open_anim` → spawn →
`close_anim` sequence is spelled out there too. This is the one place in this page where the
design document and the shipped data agree field-for-field.

33 of the 53 `egen.json` files are an empty `[null]` — most multiplayer maps have no generator.

**One authored `params` label is a shipped typo.** The linkage is by exact label: an egen
`vehicle.params` value names a designer label in the same mission's `aiv.json` header. 14 of the
15 authored labels resolve; C1/M04's egen says `Eairg32_params` while the header spells it
`Earig32_params` (a transposition), so that generator's roster lookup cannot succeed as authored.
Asserted in `CSVM.Tests/EnemyGeneratorsTests.cs`.

## The generator cycle

Decoded from the binary. One generator holds a timer, a next-event threshold, a per-wave counter
and a door state; each tick advances the timer by the frame delta (and stops entirely while the
game is paused).

```
if host is dead                    -> disable this generator permanently
blocked = (wave_size - spawnedThisWave) + active   > max_active
       or (wave_size - spawnedThisWave)            > capacityRemaining
       or (min_altitude set and host altitude < min_altitude)

if blocked:      if door open and timer >= 4 -> close door        # hold, do not cancel
else:
  door open  and timer >= 4 and timer + 8 < nextEvent -> close door
  door closed and timer >= nextEvent - 4               -> open door
  door open  and timer >= nextEvent                    -> SPAWN
```

On a successful spawn: `capacityRemaining--`, `active++`, timer resets to 0, and the wave counter
advances. If the wave is now complete the counter resets and
`nextEvent = ind_period + wave_period`; otherwise `nextEvent = ind_period`.

Three things that reading pins down:

- **`ind_period` and `wave_period` compose, they do not alternate.** `ind_period` is the gap between
  individuals *within* a wave; the gap *between* waves is `ind_period + wave_period`, not
  `wave_period` alone.
- **The door timings are hardcoded, not data.** The door opens **4 s before** a due spawn, stays
  open at least 4 s, and only closes early if the next spawn is more than 8 s away — so a
  fast-cycling generator simply leaves its hangar open.
- **The altitude gate holds, it does not cancel** — exactly as the design document says. The wave
  counter and the timer are untouched while blocked; only the door closes. The gate is skipped
  entirely when `min_altitude` is unset (a `-1.0` sentinel).

One value the decode does not pin: the FIRST `nextEvent` threshold after load. The remake's
implementation (`Session/GeneratorCycle.cs`) assumes the full inter-wave gap
(`ind_period + wave_period`), the conservative reading, and says so where F20 will revisit it.

**The host's death disables the generator.** For a fixed installation that is the `healthy` node
going inactive; for a zeppelin it is the zeppelin's own destroyed flag. Two further load-time
rejections: a generator whose `node` cannot be resolved is **dropped**, and so is one where **none**
of its `vehicle.nets` names resolve — a generator with no valid net does not load inert, it does not
load at all.

Smaller loader findings: `open_anim`/`close_anim` **default from the node name** when unauthored
(`<node>_open_<nn>` / `close_<nn>`), so the 6 non-zeppelin generators still get a door pair;
`choose_nets` parses only its first letter and accepts **`random`** as well as the `cyclic` every
file authors; and the `vehicle` block additionally accepts **`primary_target`** and **`title`**,
neither authored in this install.

What the door names resolve to: every authored `open_anim`/
`close_anim` is a **compiled `mis_anim` definition** — root `hangerdoors` under the host
zeppelin, activation OnCall — whose sequences `OBJECT_MOTION_FROM_TO` the hull's `door_left`/
`door_right` nodes 0 → ±90° about Z over **5 s** (close is the reverse), with the open's first
event activating `cargobay`. No non-zeppelin host ships a def matching the node-name default, so
that fallback resolves nothing in this install. Two placement facts that bite: the chapter can
carry several `hangerdoors` namesakes (C1 has three — the zeppelin's and two ground hangars'),
so a door call must be scoped to the host's subtree; and the `cargobay` origin node sits ON the
bay floor inside the hull — an airframe spawned exactly there collides with the bay geometry on
frame one (the remake drops fighters 12 m below it, an invented clearance; the binary's two
untraced launch timers are not interpreted). C1/IA1's mission setup deactivates its zeppelin at load (`support\c1\ia1.gw`). The mission builder
then activates the zeppelin selected by `zeppelin_type` only for `zeppelin_run`; a `dogfight_squadron`
session leaves it inactive. The same builder path arms the hull's dormant gun rings
([turrets.md](../turrets.md#waking-a-whole-subtree)).

## Capacity rule and limit

`capacity` is `0` on all 23 generators. The decoded blocking rule is:

```
(wave_size - spawnedThisWave) > capacityRemaining     ->  blocked
```

Taken literally, that rule blocks every shipped generator on its first tick: `wave_size` is at
least 1 while `capacityRemaining` begins at 0. The data therefore does **not** establish that
`0` means unlimited. Do not implement that interpretation from this field alone.

For an Instant Action `zeppelin_run`, the wave sequencer credits the objective zeppelin's named
generator with the member count of each newly-current wave. The generator uses the decoded budget
rule in this mode: nothing launches before the first credit, and one wave's worth may launch after
it. This accounts for `capacity 0` plus live capacity credits in this mission type.

Campaign missions may use a different capacity source or path. Reading the raw `capacity` bytes
from `egen.zbd` is still the discriminating evidence for that case.

## CSVM handling

CSVM applies the decoded capacity check when `capacity > 0`. At `capacity <= 0`, its general
runtime leaves the check disabled rather than treating zero as a confirmed unlimited budget. A
positive value initializes `capacityRemaining`, decrements per spawn, and blocks when
`wave_size - spawnedThisWave > capacityRemaining`.

The Instant Action `zeppelin_run` path instead enables the decoded rule from zero and grants the
objective generator one wave's capacity at each wave change. That is the current evidence-backed
case for zero-capacity generators; it does not establish campaign behavior.