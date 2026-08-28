# Enemy generators — `egen.json`

Part of [Mission entities](../mission-entities.md) in the [format documentation](../README.md).

## `egen.json` — enemy generators

An **enemy generator** spawns AI aircraft into a live mission from a host entity. The host is
either a zeppelin (fighters dropped out of its hangar) or a fixed installation — this install
has ground airfields `eairg31`/`eairg32`, a ship `eshipg31`, and a submarine `barracuda`.

| Key | On | Meaning |
|---|---|---|
| `node` | 23/23 | the host world node |
| `vehicle` | 23/23 | nested: `params` (a designer label in the `aiv.json` HEADER's `(slotId, label)` pairs, e.g. `Eairg31_params`; authored on 15 of 23, see the typo note below), `nets` (one or more AI net names), `choose_nets` (`cyclic` throughout) |
| `capacity` | 23/23 | `0` throughout — a lifetime spawn budget, decremented per launch. ⚠ **`0` does not obviously mean "unbounded"** — see [the capacity puzzle](#capacity-rule-and-limit) |
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
| `moving_path` | 1/23 | bare flag — it makes the take-off path **host-relative**, see [Launching from a surface host](#launching-from-a-surface-host) |

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

What the engine does with it is decoded. The record parser (`FUN_00452850`) compares the `params`
string against every label in the roster table and stores the matching vehicle record at `+0x3c`;
no match leaves it null. The launch (`FUN_00451bf0`, called from the cycle at `FUN_00452640`) then
takes the template-less branch: it copies the generator's `vehicle.type` string (`+0x44`, empty
because no shipped file authors the key) and asks `FUN_0047b650` for a vehicle def of that name,
which walks the def table with `_stricmp` and returns null for the empty name. The launch still
returns 1, so the cycle books it: `capacityRemaining--`, `active++`, the timer resets, the wave
counter advances and the global `%s_eg%d` ordinal increments. Nothing is built, and the `active`
slot is never freed because no vehicle exists to die, so `eairg32` (`max_active 4`) blocks itself
after four empty launches. The other branch, the first block of the def or the sibling generator's
block, does not exist in the code. CSVM matches this: the launch builds nothing, the ordinal
advances and the slot stays taken (`Session/AiGeneratorRuntime.cs`); the `player_bhawk` stand-in
is only for a generator that authors no `params` key at all.

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

**A non-zeppelin generator also needs a take-off path, and is dropped without one.** `FUN_004518d0`
names it from the host node with `sprintf("%.2s%.3s")`, the first two characters and the last three,
so `eairg31` asks for the host-relative subtree `eag31_aipath` and `barracuda` for `bauda_aipath`;
fewer than two waypoints under it, or a host name shorter than five characters, and the generator
does not load. The launch then places the aircraft on waypoint 0 (+0.2 m Y), faces it down the leg
into waypoint 1, gives it zero velocity and a full throttle lever, and drives it along the path.
This is the same movement law the roster's `taxiPath` uses, entered from a second site
([org/flightModel.md](../../org/flightModel.md), "The scripted-path follower").

Smaller loader findings: `open_anim`/`close_anim` **default from the node name** when unauthored.
The loader (`FUN_00452850`) formats `sprintf("%.5s_open%.2s", node, node + len - 2)`, the first
five characters, `_open`, the last two, so `eairg31` asks for `eairg_open31`; it then formats
`close%.2s` into a second buffer but looks the FIRST buffer up again, so an unauthored close
resolves the open definition, and the close call replays the open. Three unauthored hosts ship
the def that default names (C1's `eairg31`/`eairg32`, C2/M01's `eshipg31`, each a `cam_anim`
def rooted on the host sliding its `ldoor`/`rdoor` 8 m over 4 s, the door lead); C3/M03's
`barracuda` asks for `barra_openda` and gets nothing;
`choose_nets` parses only its first letter and accepts **`random`** as well as the `cyclic` every
file authors; and the `vehicle` block additionally accepts **`primary_target`** and **`title`**,
neither authored in this install.

What the door names resolve to: every authored `open_anim`/
`close_anim` is a **compiled `mis_anim` definition** — root `hangerdoors` under the host
zeppelin, activation OnCall — whose sequences `OBJECT_MOTION_FROM_TO` the hull's `door_left`/
`door_right` nodes 0 → ±90° about Z over **5 s** (close is the reverse), with the open's first
event activating `cargobay`. The ground hangars' defaulted defs above are the same shape at a
smaller scale. Two placement facts that bite: the chapter can
carry several `hangerdoors` namesakes (C1 has three — the zeppelin's and two ground hangars'),
so a door call must be scoped to the host's subtree; and the `cargobay` origin node sits ON the
bay floor inside the hull. The remake releases fighters exactly there: the decoded 1.5 s carrier
grace suppresses collision while the drop clears the hull. C1/IA1's mission setup deactivates its zeppelin at load (`support\c1\ia1.gw`). The mission builder
then activates the zeppelin selected by `zeppelin_type` only for `zeppelin_run`; a `dogfight_squadron`
session leaves it inactive. The same builder path arms the hull's dormant gun rings
([turrets.md](../turrets.md#waking-a-whole-subtree)).

## Launching from a surface host

A generator without the `zeppelin` key does not drop fighters out of a hangar; it runs them off a
**take-off path** authored in the host's own subtree. `FUN_00451bf0` is the launch, `FUN_004518d0`
the host bind, and `FUN_00451440` the path build.

**The path name comes from the host node name**, `sprintf("%.2s%.3s", node, node + len - 3)`: the
first two characters plus the last three. So `barracuda` asks for `bauda`, `eairg31` for `eag31`,
`eshipg31` for `esg31`. A host name shorter than five characters rejects the generator outright.
The points are the nodes `<base>_aip0`, `<base>_aip1`, … under a `<base>_aipath` group, searched
inside the host's subtree and read in order until one is missing. **A path shorter than two points
is a load rejection**, alongside the unresolved-host and no-net drops above.

All three surface hosts in this install ship one, and each reads as a runway:

| Host | Path | Points, in the host's frame |
|---|---|---|
| `barracuda` (C3) | `bauda_aip0..3` | `(0, 2.5, 8.5)` → `(0.5, 3.757, -24.674)` → `(0.5, 6.375, -97.127)` → `(0.5, 10.775, -121.984)` |
| `eairg31` (C1) | `eag31_aip0..4` | `(0, 0, -2)` → `(0, 0, -11)` → `(48, 0, -122)` → `(40, 0, -210)` → `(40, 0, -258)` |
| `eairg32` (C1) | `eag32_aip0..4` | `(0, 0, -2)` → `(0, 0, -11)` → `(-16, 0, -114)` → `(8, 0, -178)` → `(40, 0, -210)` |

The submarine's climbs from 2.5 m to 10.8 m over 130 m of deck and open water; the two airfields'
stay flat and curve, which is a ground roll. Note that the sub's run points along its local −Z
while its `sub_movement` drive travels local +Z: the Barracuda arrives in the bay and launches
back out over the water it came from.

**`moving_path` decides whose frame the points are kept in.** The flag clears a byte that the path
builder tests: set (the default, no flag) bakes each point to world coordinates once at load; the
flag, authored only on `barracuda`, keeps the host node on the path record so the points are
stored host-relative and re-transformed by the host's live matrix at every launch. That is what
lets a generator ride a hull that is still driving. Because the `_aip` nodes sit inside the host's
subtree, reading their live global position gives the same answer without repeating the matrix
work.

**The launch state**, from the multi-point branch of `FUN_00451bf0`:

- position: path point 0 through the host's live matrix, **plus 0.2 m in Y**;
- attitude: the angles from point 0 to point 1, rotated by the same matrix, so the path's own
  climb supplies the pitch;
- velocity: **zero**, against the zeppelin drop's inherited carrier velocity;
- throttle: the field pair at `+0x124`/`+0x128` set to **1.0**, where the drop sets them to 0.1;
- the path is kept on the aircraft at `+0xc8` with the flag at `+0xcc` and the leg index zeroed
  at `+0xd0`, which is the take-off run it then flies under the scripted-path follower
  (`FUN_0048a110`, docs/org/flightModel.md "The scripted-path follower": 40 mph along the run,
  accelerating and climbing out on the final leg, the flag cleared at the last point), and which
  also suppresses the net-nearest-node snap an ordinary activation makes (`FUN_004b0f40`), so the
  run places the aircraft and the net takes over where the run ends.

There is no altitude gate and no spawn-height offset on this path. `min_altitude` is a zeppelin
key and does not appear on a surface record.

## Launch names

Every launch is renamed, `sprintf("%s_eg%d", base, counter)`. The base is the roster block's own
name truncated at its **last** underscore, so C3/M03's `britpeace_5` template launches as
`britpeace_eg0`; a generator with no roster block uses its authored vehicle name whole. The
counter is a single mission-global value: zeroed when the egen file loads and advanced by every
successful launch of any of the mission's generators, so C3/M03's four Barracuda launches are
`britpeace_eg0` through `britpeace_eg3`. The spawner swaps the template block's name string for
the launch, spawns from it, and restores the block's own name afterwards, so the block stays
reusable while each launched aircraft carries a distinct identity.

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

Campaign objective scripts supply the same kind of live top-up through `WAKEUP_GENERATOR node n`.
C3/M03 is the direct case: the patrol phase completes before the script credits `barracuda` by 4;
its `vehicle.params BarracudaPlanes` then selects the authored-disabled `britpeace_5` AIV block,
so launches are Peacemakers configured from that template and appear at the submarine's live pose.

## CSVM handling

CSVM resolves a surface host's take-off path at load, drops the generator when it is shorter than
two points, and launches on the decoded pose: point 0 plus 0.2 m, nose on point 1, at rest with
the throttle open. The take-off **run** is not built: the aircraft is handed straight to its
patrol net from that pose rather than flying the remaining path points, so the path's later points
are read but unused.

CSVM applies the decoded capacity check when `capacity > 0`. Campaign generators named by a
mission's `WAKEUP_GENERATOR` additionally start on the zero-credit budget and receive its top-ups;
their `vehicle.params` label resolves the disabled AIV template used for each fresh spawn. A positive
standalone value initializes `capacityRemaining`, decrements per spawn, and blocks when
`wave_size - spawnedThisWave > capacityRemaining`.

The Instant Action `zeppelin_run` path also enables the decoded rule from zero and grants the
objective generator one wave's capacity at each wave change. That is the current evidence-backed
release path for its already-built wave members.
