# The AI pilot: what a roster vehicle flies, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-15, settling `BL-364` and the decode half of
`BL-362`. Every claim below names the function or address it came from, and the two data censuses
name the files they counted.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the addresses
are given so any claim can be re-checked at source.

**Where the other halves live.** The authored roster side is
[`formats/ai-rosters.md`](../formats/ai-rosters.md) (`netids`, `primary_target`, the skill vector);
the authored airframe side is [`formats/vehicle.md`](../formats/vehicle.md) (`mode`, `attack`,
`return_range`, `preferred_engagement_altitude`); the patrol graphs themselves are
[`formats/ai-nets.md`](../formats/ai-nets.md). The flight physics an AI shares with the player is
[`flightModel.md`](flightModel.md). CSVM's implementation seam is `src/Flight/AiPilot.cs`, whose
steering law is a placeholder and is **not** what this page describes.

## The headline: there is no netless patrol

The engine has two AI flight behaviours for an aeroplane, and which one it runs is decided at spawn
by one authored key and one authored field:

- a **patrol-net follower**, which requires a net and has no fallback if it has none;
- a **formation escort**, which requires a `primary_target` and never looks at a net.

An aircraft with no patrol net does not fly a degenerate straight line and does not loiter. It
either flies a formation station on its leader, or it is a `jet` with no net, which is a state the
shipped data never produces.

## `mode`, the dynamics class

`vehicle.json` carries a per-def key **`mode`** (the string at `0x628084`), parsed by
`FUN_00479240` at `0x0047afe8`–`0x0047b081` into the def struct at `+0xa4`. `FUN_00475820`
(`0x00475abf`) copies it to the vehicle at `+0x67c` when the vehicle is built.

| `mode` | value | debug readout | flight update |
|---|---|---|---|
| `jet` | 0 | airplane | `FUN_0048e580`, the aeroplane integrator |
| `heli` | 1 | autogyro | `FUN_0048ffe0` |
| `tank` | 2 | ground vehicle | `FUN_0048a880` |
| `ship` | 3 | ship | `FUN_0048b480` |
| **`wingman`** | **4** | airplane | `FUN_0048e580`, the same as `jet` |
| `plane` | 5 | bomber | `FUN_0048b480` |

The physics dispatch is `FUN_00489ea0`; the readout names are the engine's own, from the debug
overlay `FUN_0041c470` (`0x0041c852`). **A `wingman` is a full aeroplane**, flying the same
integrator, aerodynamics and collision sweep as the player. Only its AI differs.

The shipped `vehicle.json` authors `mode` on four defs and inherits the rest through `kind_of`
(census of all 75 defs, 2026-08-15):

- **`jet`**: `basic_airplane` and therefore all 11 player defs, all 11 base AI aircraft, and all 39
  militia variants (`r*`, `bhat*`, `sti*`, `blake*`, `ha*`, `sec*`, `med*`, `brit*`, `rus*`, `bs*`,
  `germanhellhound`, `hkfirebrand`). ⚠ `autogyro` and `pautogyro` are `jet`, not `heli`.
- **`wingman`**: exactly 12 defs, being `wingman`, `bswingman`, and the eleven `w<plane>`
  (`wbloodhawk`, `wfirebrand`, `wbrigand`, `wfury`, `wautogyro`, `wavenger`, `wkestrel`,
  `wpeacemaker`, `wbalmoral`, `wwarhawk`, and `wingman` itself off `devastator`).
- **`ship`**: `patrolboat` and `t_truck`.
- Nothing ships `heli`, `tank` or `plane`.

A sibling key `mode_alt` parses to def `+0xa8` (`0x0047b0a6`) and is `0.0` on `basic_airplane`. No
consumer of it was identified.

## The per-frame AI update

The world tick `FUN_004897c0` walks the vehicle list and, for each vehicle that is present
(`+0x91d == 0`) and awake (`+0x944`), calls the AI update `FUN_0041c270`. That function forks in
this order:

1. Byte `+0xcc` set suppresses the AI entirely and the function returns.
2. `FUN_0041fe10` runs target selection and writes the selected target to `+0x948`.
3. If the selected target was lost and the current task is pursue, the task reverts to the vehicle's
   default task `+0x2f4` and a timer at `+0x300` is set to now plus `+0x308` (`0x0041c299`).
4. **`+0x67c == 4` dispatches to `FUN_0041e760`, the escort law, and nothing else runs.**
5. Otherwise the AI task `+0x2f0` selects the steering (`0x0041c2e6`): `1` is pursue
   (`FUN_0041d9f0`), `0` and `2` are both the patrol-net follower (`FUN_0041d1f0`), and any other
   value returns without steering.
6. After the net follower, a live selected target promotes the task to pursue (`FUN_0041f040` sets
   `+0x2f0 = 1`).

The AI mode enum lives at `+0x358` and is a different thing from the task: 1 evasive maneuver,
2 approaching danger zone, 3 avoid crash, 4 stunned, 5 navigating danger zone, 0 otherwise.
⚠ **`patrol`, `pursue` and `lay off` are not stored states.** The debug readout derives them
(`FUN_0041c470`, the `AI mode:` string block at `0x0041c98b`): no selected target prints `patrol`; with a target,
`+0x2f0 == 1` prints `pursue` and anything else prints `lay off`. So "lay off" is literally *has a
target and is flying its net*, and "patrol" is *has no target*.

## The chapter's net table, and what "the first net" means

`DAT_0064f610` points at a 16-byte header built by `FUN_004311c0`: an allocation figure at `+0x00`,
the entry count at `+0x04`, the array of entry pointers at `+0x08`, a flag at `+0x0c`. Each entry is
12 bytes: the net **id**, a strdup'd **name**, and the loaded net object. The count is
`(recordElements − 2) / 2` and the loop walks the parsed `neindex` record forward from element 1,
so **entry order is the file's pair order** (`FUN_00431300` is the matching teardown).

That order is what indexes the nets themselves: a lookup scans the table for a matching id and uses
its **position** to reach the net record at `*(DAT_0064f614 + 4) + index × 100`, 100 bytes per net.
Nothing sorts, so the table's first entry is simply the first pair in `neindex.zrd.json`, which is
**not** the lowest id: in this install C1B opens on id 29 (`Patrolboat3`) and C1C on id 25
(`M1Defense`), both against a lowest id of 11. Every consumer that takes "the first net" (Instant
Action's three branches below) means this entry.

`FUN_00475f30` is the by-name form (`SET_AI_NET`'s string arm): it walks the same table comparing
each entry's name, then calls `FUN_00475fc0` with the entry's id.

## The trailer: an anchored net rides its target

A net record's trailer is `[anchorNodeIndex, "name"]`. `FUN_004314e0`, which builds a `CCENet`
from the parsed record, stores the anchor index at net `+0x18` and resolves the NAME to an object
once, at build: `FUN_004d0280(7, name)`, the by-name lookup over the kind-7 registry, into net
`+0x1c`. A record with no name leaves `+0x1c` at 0.

Every node position the engine reads goes through `FUN_00432140(net, out, nodeIndex)`, which is a
thin wrapper over **`FUN_00432010`**, and that is where the trailer does its work. With a resolved
target and an anchor index other than −1:

```
target = the trailer object's world position   (FUN_004cf2c0, refreshed once per frame,
                                                cached against the frame stamp at net +0x00)
anchor = node[net +0x18]                        the net's own stored anchor position
out.x  = (node.x - anchor.x) + target.x
out.y  =  node.y                                ⚠ authored altitude, NEVER the target's
out.z  = (node.z - anchor.z) + target.z
```

So **the graph is a pattern carried horizontally by the named object**, keeping each node's
authored height. With no trailer, or an anchor of −1, the node's stored position is returned
verbatim. Because every consumer goes through this one function, the offset applies to the nearest
-node scan (`FUN_00431900`), the follower's current target and the edge geometry alike: nothing
sees the static coordinates.

⚠ **This is what "an AI escorts something" is in the shipped data.** 76 of 222 nets are anchored,
and the census of their targets is in [`../formats/ai-nets.md`](../formats/ai-nets.md): zeppelins,
a train, a tanker, and **`player` on 11 of them**. C1's `M4ReinfAce` (the chapter's FIRST net, so
the one every Instant Action actor is handed) is `[10, "player"]`: a single 10-node cycle, every
node degree 2, whose node 10 is the edgeless anchor. In the original, every Instant Action aircraft
in C1 flies that pattern carried around the player. Six of the eight chapters' first nets are
anchored this way, to the player (C1), a zeppelin (C1C, C2, C3) or a train (C4).

⚠ **The anchor is not the pattern's centre, and the pattern is not a ring.** Measured on both C1
player-anchored nets (2026-08-15, at the controls and confirmed against the node coordinates): the
cycle's geometry crosses itself, tracing a **figure eight** of two lobes, and the anchor node sits
at the centre of ONE lobe rather than at the centroid.

| Net | Nodes | y | Extent | Anchor vs centroid | Anchor vs near lobe's centre |
|---|---|---|---|---|---|
| `M4ReinfAce` #10 | 10 + anchor | 400 | ~550 × 1030 m | 255 m off | 39 m |
| `M2Ace` #23 | 12 + anchor | 350 | ~585 × 1105 m | 246 m off | ~110 m |

So the aircraft orbits the player closely through one lobe and swings ~1 km away through the
other, rather than circling at a constant radius. Both anchors sit off-centre in the same
direction, which is authoring, not coincidence.

⚠ One mission-specific special case sits at the top of `FUN_00432010` and is NOT the general rule:
if the trailer target is one of `britbalmoral_1/2/3` (`DAT_0071c4e4`/`e8`/`ec`, set by name in
`FUN_004735b0` for one mission only) the net re-binds to whichever of the three is still present.
An escort-target failover for that mission, nothing more.

⚠ A node with no edges is skipped by the nearest-node scan (`FUN_00431900` tests the node's own
degree at `+0x18`), which is how an anchor node parked off the ring never becomes a flight target
itself.

Implemented 2026-08-15 (`BL-377`): `AiNetFollower.NodePosition` applies the offset to every node
read and `Session/NetTrailerTargets` resolves the name, `player` to the player rig and anything else
to a world node. The one thing the binary cannot answer is whose position `player` means with a
split field; rig 0 is used and nothing else is invented.

## The patrol-net follower has no netless branch

`FUN_0041d1f0` resolves the net before it does anything else (`0x0041d1f9`–`0x0041d237`): it scans
the net-id table for the vehicle's `netids` value at `+0x2e4` and, **on no match, uses index −1**,
indexing 100 bytes before the first element of the net array and dereferencing the node and edge
pointers it finds there. There is no guard and no fallback path.

This is latent rather than reachable: the shipped data never gives a `jet` a missing net (below),
so the index −1 read is never executed by the retail game. It is recorded here because it is the
positive proof that "netless patrol" is not a behaviour the engine has.

Net assignment is `FUN_00475fc0`. On `netids == -1` it returns immediately, leaving the task
untouched. On a valid net it:

- sets the current node `+0x2e8` to the nearest node to the spawn position (`FUN_00431900`) and the
  current edge `+0x2ec` (`FUN_00431e40`);
- sets the task `+0x2f0` to **2 when the net record's `+0x10` field is non-zero, else 0**;
- **overwrites the vehicle's three volumes from the net's own**, where the net authors a non-zero:
  activation from net `+0x24`² / `+0x28` / `+0x2c` into `+0x318` / `+0x31c` / `+0x320`, attack from
  net `+0x34`² / `+0x38` / `+0x3c` into `+0x328` / `+0x32c` / `+0x330`, and return from net `+0x40`²
  / `+0x44` / `+0x48` into `+0x334` / `+0x338` / `+0x33c`. Radii are stored squared.

⚠ **The roster block outranks the net on all nine.** The spawn runs the net assignment first
(`FUN_0047c210` calls `FUN_00476250` at `0x0047c77b`, and the net assignment is inside it) and only
then copies the block's own volumes from `+0x48`–`+0x6c` over the same nine fields, each guarded by
the same non-zero test, the last of them at `0x0047c91c`. So a net's volumes reach a vehicle only
where its roster block leaves that slot at 0.0, which is the campaign case. An Instant Action
block authors all nine at ±10000 m (`FUN_0045a240`), so there the net contributes none of them.

**No activation volume is smaller than `min_ai_active_dist`**, the `player.zrd.json` key read into
`_DAT_0071c3ec` by `FUN_004735b0` (`0x00474139`), **2000.0** in this install and 0 when the key is
absent. The clamp runs twice over the same three fields, once after the net assignment
(`0x004763f4`) and once after the block copy (`0x0047c922`): radius floored to `min_ai_active_dist`,
the low bound to −it and the high bound to +it. The attack and return volumes have no such floor.

⚠ **A net demotes a wingman.** `FUN_00476250` (`0x00476382`–`0x004763c3`): if `mode == 4` and
`netids >= 0`, the escort buffer at `+0x2f8` is freed and `+0x67c` is forced to **0**, after which
the vehicle takes the net like any other `jet`. The script-side net assignment `FUN_0049c920` ends
with `+0x67c = 0` on every path, including the failure path where the net id did not resolve.

So `mode wingman` is not "this aircraft escorts". It is **"this aircraft escorts when it has no
net"**, and the net wins whenever one is authored.

## The escort law, `FUN_0041e760`

Its leader is `primary_target` at `+0x2fc`, dereferenced through `+4` to the leader's vehicle with
no null check, so the mode requires an assigned target.

**Station offsets**, in the leader's own body frame (basis at `+0x180`, translation the leader's
position). Axis 2 of that basis is the leader's *backward* axis, derived from `FUN_00476250` setting
the forward vector to its negation (`0x00476339`):

| leader | offset | reading |
|---|---|---|
| the player | `DAT_0061fb88` = (6.0, 0.0, 18.0) | 6 m out, level, 18 m astern |
| another AI | `DAT_0061fb98` = (8.0, −2.0, −8.0) | 8 m out, 2 m low, 8 m ahead |

⚠ Escorting a non-player leader also forces the state to 2 every frame and sets byte `+0xdd`
(`0x0041e7b6`), which suppresses the radio call that the player-escort path plays. The effect is
that a wingman-of-a-wingman has no persistent state machine: it trails its selected target when it
has one and flies the station when it does not.

**The state machine** at `+0xd8`, five states, evaluated twice per frame (once to transition, once
to compute the station):

| state | station | leaves when |
|---|---|---|
| 0 | trail the selected target, or the leader's position +200 m of altitude when the target is beyond **1800 m** | leader live, within **700 m**, own speed above **20.576 m/s** → 1 |
| 1 | the formation offset above | (set from 0, 2 or 4) |
| 2 | trail the selected target | no target → 1; or `(3 × altitude error)² + range²` above **1200 m** squared for a player leader, **800 m** squared for an AI leader → 1 |
| 3 | re-join | within **50 m** of the station → 4; target acquired → 2 |
| 4 | the formation offset | within **50 m** → 1; target acquired → 2 |

Nothing inside this function enters state 3.

**The trail station** behind the selected target (states 0 and 2) ramps with the *target's* speed,
placed along the target's backward axis:

```
d = 106.68                                   for v <= 20.576 m/s
d = 106.68 + (v - 20.576) * 1.8516719        for 20.576 < v < 102.880005 m/s
d = 259.08                                   for v >= 102.880005 m/s
```

Those are imperial figures in metric storage: 350 ft at 46 mph ramping to 850 ft at 230 mph.

**Separation.** Inside **80 m** of the leader (6400 m² compared before the square root), the station
is pushed away from the leader along the leader-to-follower vector scaled by `80 / distance`.

**Two AI modes short-circuit the law.** Avoid crash (`+0x358 == 3`) steers at the aircraft's own
position plus **1000 m** of altitude, and stunned (`+0x358 == 4`) returns immediately with no input
at all. The net follower's avoid-crash case does the same 1000 m climb-out with its own parameter
block, so **avoid crash is "aim 1000 m above yourself" in both laws**.

## The steering law both behaviours call

`FUN_0041b560(this, stationPoint, desiredVelocity, params, emergencyFlag, leadFlag)` turns a target
point into stick and throttle. It is the original's AI control law and CSVM has no implementation of
it; a full decode is separate work, not attempted here. What is settled is its parameter table:
three 8-float blocks that differ only in their first two entries.

| block | first two | used by |
|---|---|---|
| `DAT_0061fb28` | 0.4, 1.5 | the escort law, and its avoid-crash climb-out |
| `DAT_0061fb48` | 0.6, 1.3 | the net follower's avoid-crash climb-out |
| `DAT_0061fb68` | 0.8, 1.1 | the net follower's normal patrol |

Shared tail on all three: `0.06, 0.06, 0.15, 0.025, 0.35, 0.0`. Entries 0 and 1 are the throttle
floor and ceiling, which the law walks toward at **0.35 per second** and then clamps; entries 2 and
3 are the stick thresholds below which the second control axis is
added; entries 4 and 5 weight the along-track and across-track components of the intercept; entry 6
is the bank-authority threshold. The law also clamps its own speed demand to a ±**26.8224 m/s**
(60 mph) band around the current speed, and floors it at **22.352 m/s** (50 mph).

## `netids` is a list, and the engine picks one at random

The roster spawn `FUN_0047c210` reads the count at block `+0x10` and the array at block `+0x14`
(`0x0047c733`–`0x0047c76d`):

- count 1 takes that id;
- count above 1 takes **`rand() % count`**, drawn once at spawn;
- count 0 or less writes `-1`.

This install authors a scalar per block, so the draw never fires in the campaign, but the reader is
the list reader and the editor comment quoted in [`ai-rosters.md`](../formats/ai-rosters.md) is
right to call it a list.

Activation is separate from all of this. `FUN_004b0f40(vehicle, deactivated)` is the
activate/deactivate primitive, clearing or setting `+0x945`, `+0x91d`, `+0x91e` and `+0x91f`, and is
called from the spawn with the roster's `deactivated` field. On activation, a vehicle that has a net
is snapped to that net's nearest node (`FUN_00432010`).

## `preferred_engagement_altitude` is a maneuver-selection weight

The roster's `pref_engage_alt` (the editor comment's name; `vehicle.json` spells it
`preferred_engagement_altitude`) is block `+0x98`. `FUN_0047c210` (`0x0047d44b`) copies it to the
vehicle at `+0x98c`, falling back to the airframe def's own value when the block leaves it at
`-1.0`. `basic_airplane` authors **300.0** and every aircraft def inherits it; the vehicle
constructor's own default is also 300.0 (`0x004b0434`).

Its **one reader in the image** is `FUN_004201a0` at `0x004204da`, the evasive-maneuver selector: a
candidate maneuver's weight gains **+1.0** when the aircraft's altitude (`+0x208`) sits on the wrong
side of the preferred altitude. It is a bias on the maneuver library draw and **not** an altitude
order. Nothing steers toward it.

## What the shipped rosters actually do

Census of all 53 `aiv.zrd.json` files, 414 blocks, 2026-08-15:

| | blocks | `netids` |
|---|---|---|
| `player` | 53 | −1 |
| `wingman_N` | 50 | −1 |
| `bswingman_N` | 3 | −1 |
| everything else | 308 | a real net id, every one |

**Every enemy, boat and truck in the campaign is netted, and the only netless AI in the campaign is
the player's wingmen.** Their node names are `wingman_N` and `bswingman_N`, which are exactly the
two non-`w<plane>` defs that author `mode wingman`. The two halves agree: netless is the wingman
case, and the wingman case is the escort law.

## Instant Action gives every actor a net

⚠ This corrects [`instant-action.md`](../formats/instant-action.md), which recorded that an Instant
Action actor leaves `netids` at its `-1` default.

`FUN_0045a390` builds each actor's roster block on the stack and, in **all three** of its branches
(the wingmen loop, the ace, and the wave loop), writes:

```
block +0x10 = 1                                   the netids count
block +0x14 = malloc(4), holding the first entry of the chapter net-id table DAT_0064f610
```

at `0x0045a8b4` (the wingmen), `0x0045ab18` (the ace) and `0x0045ae85` (the waves). Each reads the
table's **entry 0** and takes that entry's id, so every Instant Action actor is handed the id of
the first pair in the chapter's `neindex` (see "The chapter's net table" above): net 10
(`M4ReinfAce`) in C1, 29 (`Patrolboat3`) in C1B, 25 (`M1Defense`) in C1C, and net 1 in each of the
remaining five.

Their volumes are `FUN_0045a240`'s ±10000 m, not the net's: the block wins the overwrite (above).

Two consequences follow from the demotion rule above, and both are read off the code path rather
than observed at the controls of the original:

- Instant Action **wingmen are demoted from `wingman` to `jet` at spawn** and walk that net like
  everything else. The `w<plane>` defs contribute their pilot and airframe values, not their mode.
- The Instant Action escort chain that `instant-action.md` decodes from `primary_target`
  (0/1/3 on the player, 2/4 on 1/3) is therefore **not** flown as a formation in that mode. It
  survives as a target assignment.

The escort law is a campaign behaviour. If a future decode shows Instant Action wingmen holding
station on the player in the original, the fault is in one of the three facts above and this section
is where to start.

## Function map

| Address | What |
|---|---|
| `FUN_004897c0` | the world tick: iterates vehicles, gates on `+0x91d` / `+0x944`, calls the AI update and the physics |
| `FUN_00489ea0` | physics dispatch on `mode` |
| `FUN_0041c270` | the per-frame AI update and its behaviour fork |
| `FUN_0041fe10` | target selection, writes `+0x948` |
| `FUN_0041d1f0` | the patrol-net follower |
| `FUN_0041d9f0` | pursue |
| `FUN_0041e760` | the formation escort law |
| `FUN_0041b560` | the shared steering law: point in, stick and throttle out |
| `FUN_004311c0` | builds the chapter net table from `neindex`, in file order (`FUN_00431300` frees it) |
| `FUN_004314e0` | builds one `CCENet` from its record: nodes, edges, volumes, and the trailer's name→object resolve |
| `FUN_00432010` | node position with the trailer offset applied: the "this net rides that object" rule |
| `FUN_00432140` | node position by index, the wrapper every consumer calls |
| `FUN_00431900` | nearest node to a point, skipping edgeless nodes |
| `FUN_00475fc0` | net assignment (`SET_AI_NET`), including the volume overwrite |
| `FUN_00475f30` | the by-name net assignment: table scan on the entry name, then `FUN_00475fc0` |
| `FUN_004735b0` | the `player.zrd.json` loader, including `min_ai_active_dist` |
| `FUN_0049c920` | script-side net assignment, always forces `mode` to `jet` |
| `FUN_00475820` | def to vehicle copy, including `mode` |
| `FUN_00476250` | post-spawn vehicle init, including the wingman demotion |
| `FUN_0047c210` | the roster spawn: `netids` draw, `preferred_engagement_altitude`, activation |
| `FUN_004b0f40` | activate / deactivate |
| `FUN_00479240` | the `vehicle.json` def parser, including the `mode` string table |
| `FUN_004201a0` | evasive-maneuver selection, the one reader of `preferred_engagement_altitude` |
| `FUN_0041c470` | the debug overlay that names the modes and recomputes the target ranking |

## Open

- The per-node fields beyond position and degree (`+0xc`, `+0x10`, `+0x11`, `+0x14` on the 0x28-byte
  node record) are still unread; the raw tags `ai-nets.md` exposes are these. The follower's own
  reads of `+0x11` / `+0x14` (a flag and a danger-zone path id, `FUN_0041d1f0`'s `dzpath_%d`
  branch) are the lead.
- `FUN_0041b560` is described by its parameter table only. The law itself (how it converts a station
  point into bank, pitch and rudder) is a separate decode, and it is what would replace
  `AiPilot`'s placeholder.
- `mode_alt` has no identified consumer.
- Whether the original's Instant Action wingmen visibly hold station is untested. The code path says
  they do not.
