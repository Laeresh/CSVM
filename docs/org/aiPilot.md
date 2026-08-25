# The AI pilot: what a roster vehicle flies, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-15, settling `BL-364` and the decode half of
`BL-362`, and extended 2026-08-16 with the merge rule. Every claim below names the function or
address it came from, and the two data censuses name the files they counted.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the addresses
are given so any claim can be re-checked at source.

**Where the other halves live.** The authored roster side is
[`formats/ai-rosters.md`](../formats/ai-rosters.md) (`netids`, `primary_target`, the skill vector);
the authored airframe side is [`formats/vehicle.md`](../formats/vehicle.md) (`mode`, `attack`,
`return_range`, `preferred_engagement_altitude`); the patrol graphs themselves are
[`formats/ai-nets.md`](../formats/ai-nets.md). The flight physics an AI shares with the player is
[`flightModel.md`](flightModel.md); the firing half (when an AI pulls the trigger, and the
`weapons` 5-tuple that governs it) is [`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md). CSVM's
implementation seam is `src/Flight/AiPilot.cs`, the driver that picks each mode's aim point and
parameter block; the steering law it hands them to is
`src/Flight/AiControlLaw.cs`, decoded in [`aiControlLaw.md`](aiControlLaw.md).

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

Firing is not one of those branches. All three behaviours call the fire decision `FUN_0041f420`
themselves, each with both weapon classes enabled, and it is decoded in
[`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md).

The AI mode enum lives at `+0x358` and is a different thing from the task: 1 evasive maneuver,
2 approaching danger zone, 3 avoid crash, 4 stunned, 5 navigating danger zone, 0 otherwise.
⚠ **`patrol`, `pursue` and `lay off` are not stored states.** The debug readout derives them
(`FUN_0041c470`, the `AI mode:` string block at `0x0041c98b`): no selected target prints `patrol`; with a target,
`+0x2f0 == 1` prints `pursue` and anything else prints `lay off`. So "lay off" is literally *has a
target and is flying its net*, and "patrol" is *has no target*.

## Target acquisition: four candidate classes, not one

`FUN_0041fe10` is step 2 above. It picks the target, and `FUN_0041f9c0` is the sweep it delegates
to. The candidate pool is **four separate global lists**, each with its own `Target` subclass; the
RTTI names survive in the binary.

| Class | List | Constructor | vtable | Admission | Rank offset |
|---|---|---|---|---|---|
| `TargetVehicle` | `DAT_0071dabc` | `FUN_004a6330` | `0x0060886c` | none | candidate `+0x340` |
| `TargetTurret` | `DAT_0071d914` | `FUN_004a6370` | `0x006088cc` | none | scorer `+0x344` |
| `TargetStruct` | `DAT_0071d33c`–`DAT_0071d340` | inline | `0x0060364c` | object `+0x8d` set; `+0x65` needs gasbag ordnance | scorer `+0x344` |
| `TargetProjectile` | `DAT_0064f78c` | `FUN_004a63b0` | `0x00608920` | object `+0x6c` set | none |

`FUN_0041f9c0` carries one running minimum across all four lists, so the pick is the global minimum;
the return chain resolves projectile, struct, turret, vehicle only because a later list records a
winner solely when it already beat the earlier ones. `+0x344` on the scoring vehicle is a per-pilot
handicap applied to turrets and structures but never to aircraft; `+0x340` on a candidate vehicle is
a per-target offset. `TargetProjectile` is the consumer of the `TARGETABLE` weapon flag.

⚠ **World structures are always swept.** The fourth argument to `FUN_0041f9c0`, which decides
whether the struct list is walked at all, is the literal `1` pushed at `0x00420002`. Only the
list's gasbag members are conditional.

### The scorer, and which one runs

`rank = weight × 1200 + distance + objectiveBias`, minimised, as
[`ai-rosters.md`](../formats/ai-rosters.md) has it. Two implementations, selected on the scoring
vehicle's own `+0x67c` at `0x0041fe75`: `FUN_00421ad0` (vtable `0x00603544`) for `jet` and
`wingman`, `FUN_00421950` (vtable `0x0060354c`) for everything else. The debug overlay
`FUN_0041c470` duplicates both instruction for instruction, which is where the readout's formula
came from.

Weight starts at **1.0**, or **0.7** when the candidate is the player (`DAT_0071c298`). Then:

- **+0.4** when the candidate casts to `TargetVehicle` and its `+0x67c` is **4**, that is, when the
  candidate is a **`wingman`**. Minimised, so this is 480 m against it: the engine de-prioritises
  enemy wingmen. The readout's "one dynamics class" is this, and "dynamics" is the overlay's label
  for the `mode` field above.
- **−0.5** when the `Target`'s virtual at vtable `+0x1c` returns true. `TargetVehicle`,
  `TargetTurret` and `TargetProjectile` all bind `0x00422720`, a constant false. Only
  `TargetStruct` binds `0x004227a0`, which returns the object's `+0x65` flag, and the overlay calls
  that same virtual to print `Gasbag targeted: %s`. So the readout's "one structure case" is a
  **zeppelin gasbag**, worth 600 m in its favour, and nothing else in the game takes the term.

`FUN_00421ad0` adds three more terms that `FUN_00421950` does not have, so **a ground or sea AI
scores on base weight and the two class terms alone**. Let `d` be candidate minus self, in metres
and not normalised:

- `d` dotted with the scorer's own forward row (`+0x198`–`+0x1a0`): above **+0.5** adds **+0.2**,
  below **−0.5** adds **−0.2**, between them adds nothing. ⚠ This is an ahead/behind test with a
  half-metre deadband, **not** a cone, and **ahead is the unfavourable arm**. The engine prefers the
  target on its tail.
- `d.y > 0`, the candidate above, adds **+0.2**; otherwise **−0.2**.
- `d` dotted with the candidate's own forward (its vtable `+0x04`) negative, meaning it faces the
  scorer, adds **+0.2**; otherwise **−0.2**.

The activation test is a **cylinder**, not a radius: horizontal `d.x² + d.z²` at or under
`+0x328`, and `+0x32c ≤ d.y ≤ +0x330`. Outside it the score is `1e21` and the candidate is never
picked. `FUN_00421ad0` also returns `1e21` when the candidate object's `+0x04` field is 3 or more,
and `FUN_00422890` is a validity check whose failure scores the same.

### `rating_biases` returns rank units directly

`FUN_0041ae40` walks the bias list at scorer `+0x8a4`, entries of 0x14 bytes with the bias float at
`+0x10`, matching through `FUN_0041add0` and, for a turret, walking the parent chain at `+0x54` /
`+0x58`. It returns a value added raw to `weight × 1200 + distance`:

| Authored bias | `TargetVehicle` | `TargetTurret` |
|---|---|---|
| no matching entry | `0` | `+37.5` |
| `≤ −1.0` | `FLT_MAX`, which every caller maps to `1e21` | same |
| `≥ 1.0` | `−100000` | `−99962.5` |
| otherwise | `bias × −750` | `bias × −750 + 37.5` |

⚠ **An authored `−1.0` is a hard exclusion, not a penalty.** The turret column is uniformly the
vehicle column plus 37.5, so a turret carries a flat 37.5 m handicap against an aircraft.

### The gasbag gate is ordnance, checked at admission

`FUN_00420070` walks the scorer's weapon list (`+0x268` to `+0x26c`, stride 0x30) for a weapon
whose def flags at `+0x210` carry bit **`0x1000`**, with ammo above zero and both cooldowns at or
under `DAT_0071c470`. Bit `0x1000` is `DAMAGES_ZEPPELIN`, set by the weapon parser `FUN_004ba6f0`
at `0x004ba960` from the keyword string at `0x0062b2ec`. The result is pushed as the third argument
at `0x00420011`, and it admits the `+0x65` members of the struct list.

So a pilot without gasbag ordnance is never offered a gasbag at all. It still sees the airship's
engines, turrets and cannons, which are ordinary members of the turret and struct lists.

### Three more rules the acquisition carries

1. **A `wingman` ignores its `primary_target`.** `0x0041feb0` skips the assigned-target branch when
   the scorer's own `+0x67c` is 4. This is the escort law's reading confirmed from a second site:
   for a `wingman` that field is a formation leader, not a target.
2. **An assigned `primary_target` wins outright** whenever it scores under `1e20`, without the pool
   being swept.
3. **A standing target is sticky.** It is re-scored only once `+0x94c` exceeds `DAT_0071c470`, and
   while it still scores under `1e20` it is kept and the pool is not swept at all.

⚠ **Deconfliction is a count, not a pool drop.** `FUN_0041fe10` decrements `target+0x8` and
`target->object+0x4` before scoring and restores them after (`0x0041fece`, `0x0041ff0b`), so
`object+0x4` is a live count of how many AI hold that target and the scorer simply excludes its own
contribution. There is no drop-and-reselect pass.

### What CSVM ports of this (D36)

`FlightController.SelectRankedTarget` sweeps `TargetVehicle`/`TargetTurret`/`TargetStruct` (the
gun aim assist's own three lists) for one global minimum, and `AiTargetRanking.ObjectiveBiasFor`
carries the turret's flat `+37.5`. Routed into `AiGunner.GroundTarget` rather than the
aircraft-only `Target` field, so `AiPilot`'s flight law never chases a turret or structure through
the sky — it keeps flying its assigned course while the gunner alone aims and fires at it.

Unmodelled, named rather than guessed: the `wingman` **+0.4** de-prioritisation and the zeppelin
gasbag **−0.5**/ordnance-gated admission (`TargetStruct`'s `+0x65` members) both need a `mode`
field and a gasbag identity CSVM's session wiring does not carry into `FlightController` yet; the
ahead/behind deadband, altitude sign and facing ±0.2 terms stay the pre-D36 cone/sign reading
rather than the decoded half-metre-deadband geometry above; and `TargetProjectile` is not part of
the acquisition sweep at all.

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
split field, because the original has no splitscreen; the call (2026-08-15) is that split play
matches single player, so rig 0 is used and nothing else is invented.

## What the patrol executor aims at

`FUN_0041d1f0` case 0 is the aeroplane's patrol arm, and the aim point it hands the control law is
**not the node**. Built at `0x0041d30b`–`0x0041d4e0`, with `cur` the node the aircraft is flying
from (`obj+0x2e8`) and `next` the far end of the current edge (`DAT_0064e860`, both positions read
through `FUN_00432140` so the trailer offset is already in them):

```
leg   = normalise(next - cur)                    ; FUN_00422690
along = dot(pos - cur, leg)
off   = ((pos - cur) - along · leg) · 0.9        ; 0.9 at 0x0060355c
if |off|² > 40000:  off = off · (200 / |off|)    ; 40000 at 0x00603558, 200.0 (double) at 0x00603550
aim   = next + off
```

Then `FUN_0041b560(aim, velocity = DAT_0075d1b8, table 0x61fb68, emergency = 0, gunLead = 0)`, where
`DAT_0075d1b8` is three zero floats, so the aim point is stationary.

**The aim point carries 0.9 of the aircraft's own cross-track error, capped at 200 m.** An aeroplane
50 m left of the leg line is sent at a point 45 m left of the node, so the commanded course is very
nearly parallel to the leg and only the residual tenth of the offset converges on it. The
displacement moves with the aircraft's own drift, which is a washout on the position error.

⚠ **This is a tracking rule, not a damping rule, and it does not by itself steady the aeroplane.**
It was measured on the ported law: flying a 8 km leg entered 120 m off the line, the displacement
holds the aeroplane out on a parallel course as intended, and it does not change the roll against
the same flight aimed at the node. What steadies the aeroplane is in the law, and specifically in
the sign of `bz` ([`aiControlLaw.md`](aiControlLaw.md), step 4): the renormalisation is the astern
case, so an aircraft tracking something in front of it keeps its error's true magnitude.

⚠ The vertical component is carried too. `off` is a full 3-vector, so an aircraft above or below its
leg is aimed above or below the node by the same 0.9, and the 200 m cap is on the 3-D magnitude.

Case 3, avoid crash, aims at the aircraft's own position with **1000.0 added to Y only**
(`0x0041d2e2`) on the emergency table `0x61fb48`. There is no lateral component to it.

## Arrival is measured ALONG the leg, not as a distance to the node

After the law call, case 0 advances the walk on this test, with `next` the node being flown at and
`leg` the unit vector from `cur` to it:

```
if dot(pos - next, leg) <= -sqrt(edge+0x1c):  return   ; not arrived
FUN_0041d8f0(obj)                                      ; step to the next edge
```

**It fires as soon as the aeroplane draws abeam the node, however far off to the side it is.** A
capture SPHERE can be missed by an aircraft that cannot turn tightly enough, and then the walk is
stranded on a node it orbits forever; a plane perpendicular to the leg cannot be. The other branches
(pursue, maneuver) use a horizontal squared distance against the same `edge+0x1c` instead, so the
along-leg shape is the patrol arm's alone.

`edge+0x1c` is not authored. It is written once per edge at net load by `FUN_00431a90`, which also
fills the edge's delta and 3-D length (`+0x0c`…`+0x18`) before normalising the delta in place:

```
r = sqrt(dx² + dz²) · 0.1                   ; the leg's HORIZONTAL length, a tenth of it
if r < net+0x20:  r = net+0x20              ; CCENet+0x28, copied in by FUN_004314e0
edge+0x1c = r²
```

**A tenth of the horizontal leg length, floored at 10 m, stored squared.** The floor is `CCENet`'s
constructor default (`FUN_004303d0` writes `10.0f` to `+0x28`) and every shipped net leaves it
alone — element 1 of the net record is `10.0` on all 222 files. Altitude change along a leg does not
widen the capture, which is consistent with the follower's other, horizontal, arrival tests.

⚠ **Porting this does not by itself steady the aeroplane.** Measured on the anchored `M4ReinfAce`
against the invented 200 m horizontal radius it replaced, node advances in 90 s go 3 → 7 with the
player under way, so the stranding is real and this removes it, while the roll was unchanged. The
roll was the `bz` sign in [`aiControlLaw.md`](aiControlLaw.md), which is a separate fault in a
separate function; both were real and this one is the reason a walk could stall on a node.

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
  current edge `+0x2ec` (`FUN_00431e40`, below);
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

## Which edge a vehicle leaves a node on: the nose, never a draw

`FUN_00431e40(net, nodeIndex, excludeEdgeIndex, direction)` picks the edge, and it has exactly two
callers: the net assignment `FUN_00475fc0` at seat, with no edge excluded, and the walk step
`FUN_0041d8f0` after an arrival, excluding the edge just flown. Both pass the same direction, the
vehicle's `+0x198`–`+0x1a0` basis row with each float's sign bit flipped, which is its backward axis
negated and so its **nose**.

The pick walks the node's own edge list, skips the excluded edge, resolves each edge's far end,
normalises `farEnd − node` through `FUN_00422690`, dots it with the direction and keeps the maximum,
seeded at `−FLT_MAX` so the first of equal maxima wins. A node with no edges returns −1.

Three things follow. **The walk is deterministic**: nothing draws, so two vehicles seated on one node
facing one way leave it on one edge, which is how the campaign's grouped aircraft fly a shared net in
formation. **The seat is the node, not a destination**: `+0x2e8` holds the node the vehicle is flying
FROM and the aim point is the far end of that edge, so a vehicle offset to the side of the leg keeps
that offset through the cross-track carry below rather than converging on a node. And the direction
is the nose rather than the velocity, so a slipping or rolled aeroplane picks the same edge as a
coordinated one.

⚠ One arm ahead of all of that is unread: when net `+0x10` is non-zero the function instead returns
the first non-negative entry of the node's `+0x20` array. That is the same field the net assignment
tests to seat task 2 rather than 0, so it belongs with the undecoded danger-zone path tags
([`../formats/ai-nets.md`](../formats/ai-nets.md)) and no shipped net this reaches has been read.

CSVM ports the pick as `AiNetFollower.PickOnward`, taking the nose through `AiNetFollower.Update`;
`AiPilot.FlyPatrol` passes `−model.Attitude.Z`. A caller with no facing keeps the older
nearest-node seat and a seeded draw, which is `ZeppelinMotion` alone, because a zeppelin is not a
vehicle in this engine at all and does not reach `FUN_00431e40`.

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
| 0 | the target station, or the leader's position +200 m of altitude when there is no target or the LEADER is beyond **1800 m** | leader live, within **700 m**, own speed above **20.576 m/s** → 1 |
| 1 | the formation offset above | (set from 0, 2 or 4) |
| 2 | the target station | no target → 1; or `(3 × altitude error)² + horizontal range²` above **1200 m** squared for a player leader, **800 m** squared for an AI leader → 1 |
| 3 | re-join | within **50 m** of the station → 4; target acquired → 2 |
| 4 | the formation offset | within **50 m** → 1; target acquired → 2 |

Both distances in state 2's break-off are measured to the **leader**, not to the target, and the
range term is the horizontal one alone (`FUN_00538920`, x and z); every other test on this page is
a 3-D distance (`FUN_00538880`).

Nothing inside this function enters state 3, and **nothing inside it leaves state 1**: the column
above lists that state's entries, not an exit. A wingman on a player leader therefore joins once
and holds the formation offset for the rest of the mission, target or no target. The engaging
state is reachable only through the every-frame forcing an AI leader applies (above), which is why
a wingman-of-a-wingman is the one that alternates.

Nothing OUTSIDE the function moves the state either. `+0xd8` is written at six sites, all of them
inside `FUN_0041e760` (`0x0041e7b0`, `0x0041e8d1`, `0x0041e9f6`, `0x0041ea21`, `0x0041ea37`,
`0x0041ea62`) plus the constructor's zero at `0x004b05a3`; no other function in the image writes a
vehicle's escort state at all.

### A wingman still selects a target, and still shoots

Holding the formation is not the same as ignoring the war, which is why a `mode wingman` block
authoring `rating_biases` is coherent.

**It acquires.** `FUN_0041c270` calls the acquisition `FUN_0041fe10` at the top of every AI frame
before it forks on `mode`, so a wingman runs it exactly as a jet does: the same ranked pick
(`FUN_0041f9c0`) against the same bias table at `+0x344`. Two arms of that function are keyed on
`mode` being 0 or 4, which is the wingman's own class: the scorer it picks (`PTR_FUN_00603544`
rather than `PTR_FUN_0060354c`), and the arm that would prefer `primary_target` as the target,
which is gated on `mode != 4` so a wingman never shoots at its own leader.

**It fires.** The escort law's tail, after it has handed the station to the steering law, is
`if (+0x948 != 0) { … FUN_0041afe0(); FUN_0041f420(1,1); }`, the same fire routine the pursuit path
in `FUN_0041c270` ends on. The gate ahead of it is `FUN_00460be0`'s firing solution against the
gun group at `+0x950`, refused when the solution's dot against the aircraft's own forward row falls
below −0.9, so a wingman fires whenever the target it selected happens to lie ahead of the station
it is flying. The engagement a player sees from a wingman is that, not a pursuit.

### The release that is not wired up

`FUN_0049c880` walks every vehicle: alive (`+0x91d == 0`), `mode` 4, a non-null `+0x2fc` whose
leader answers its vtable `+0x38` test, and hands each one to `FUN_0049c920`. That function assigns
the chapter's first net (`+0x2e4`, with `+0x2e8`/`+0x2ec` from the node and edge lookups), sets the
AI state at `+0x2f0` from the net record, and clears `mode` to 0. A vehicle it has touched is an
ordinary netted AI: `FUN_0041c270` stops forking to the escort law for it and runs the patrol and
pursue machine instead, which is the only "wingman leaves formation and fights" mechanism in the
image.

⚠ **It is unreachable.** `FUN_0049c880` has no callers and no 4-byte pointer to it anywhere in the
image, so nothing in the shipped build can run it. Whatever it was for (the shape reads as a
release order given to the flight), the shipped game never gives it.

**The target station** (states 0 and 2) ramps with the *target's* speed and is placed along the
NEGATION of the target's backward axis, i.e. that far AHEAD of it along its own facing, a cut-off
point rather than a trail:

```
d = 106.68                                   for v <= 20.576 m/s
d = 106.68 + (v - 20.576) * 1.8516719        for 20.576 < v < 102.880005 m/s
d = 259.08                                   for v >= 102.880005 m/s
```

Those are imperial figures in metric storage: 350 ft at 46 mph ramping to 850 ft at 230 mph.

⚠ Pursue computes the same ramp from the same immediates and applies it with the OPPOSITE sign
(`FUN_0041d9f0`: the aim point is the victim's position plus `d ×` its backward axis, so pursue
stations itself that far BEHIND its victim). The two laws differ in that one sign alone. The aim
velocity handed to the steering law goes with the station: the target's own on states 0 and 2, the
LEADER's on states 1 and 4, and zero on the "fly at the leader" arm of state 0.

**Separation.** Inside **80 m** of the leader (6400 m² compared before the square root), the station
is pushed away from the leader along the leader-to-follower vector scaled by `80 / distance`.
⚠ The player station is 18.97 m from its leader, so this push ALWAYS fires there: the commanded
point alternates between the station itself and a point about 99 m out along the current
leader-to-wingman line, and the hold that results is a weave around the leader rather than a
parade-tight join.

**Two AI modes short-circuit the law.** Avoid crash (`+0x358 == 3`) steers at the aircraft's own
position plus **1000 m** of altitude, and stunned (`+0x358 == 4`) returns immediately with no input
at all. The net follower's avoid-crash case does the same 1000 m climb-out with its own parameter
block, so **avoid crash is "aim 1000 m above yourself" in both laws**. The escort law flies its own
climb-out on `DAT_0061fb28`, the wingman table, where the net follower uses `DAT_0061fb48`.

### What CSVM ports of this (D34)

`src/Flight/AiEscort.cs` is the law: the five-state machine, both station offsets, the ramp, the
break-off test and the separation push, pure over a leader/target snapshot. `AiPilot.Escort` holds
it and, when its leader is in play, dispatches to it INSTEAD of pursue, lay off, patrol, evade and
a running maneuver, keeping only stunned and avoid crash ahead of it, which is the original's own
fork order. The station is flown through `AiControlLaw` on `AiLawParams.Wingman`.

Not ported: the radio call the join plays (`DAT_0071c3b0`) and the re-acquire sweep state 2 runs
when its target is lost (`FUN_0041f9c0` again, with its own 3600 m test and second cue,
`DAT_0071c3b4`); the fire decision the law ends on, which in CSVM is the host's `AiGunner` pass;
and the null-leader dereference, which CSVM answers by falling back to the pilot's standing orders.

The acquisition and the guns come out the same way. `AiPilot.Escort` short-circuits the STEERING
dispatch only; `FlightController`'s gunner pass runs on every AI tick regardless, so a CSVM wingman
ranks targets against its own `rating_biases` and fires from the station exactly as the law's tail
does. The `wingman-engage` suite measures it on a flown leg: the wingman holds one bandit as its
target throughout, opens fire, and its escort state never leaves the formation.

**The spawner.** A campaign session spawns the mission's `aiv` roster through
`Session/CampaignRoster.cs` (the plan) and `CampaignDirector.BuildRoster` (the placement). The fork
above is applied per block from the def's `mode` (`Mech3/VehicleDefs.cs`, resolved through
`kind_of`) and the block's `netids`: a netless `mode wingman` block gets `AiPilot.Escort` on the rig
its `primary_target` names (the literal `player` is the first human), resolved in a second pass once
every rig exists; any authored net becomes `AiPilot.Patrol` on that net and no escort. The net's
volumes (record elements 2–10) are applied first and the block's own slots 8–19 over them, each
field on the non-zero test, then the activation radius is floored at `min_ai_active_dist`, which is
the order decoded under "Net assignment". A `deactivated` block is built inert and `WAKEUP_ENEMIES`
re-activates it. The `campaign-roster` suite pins all of it over C1/M04.

⚠ **A leader flying faster than 250 mph cannot be formated on at all.** The steering law caps an
AI's desired speed at `AiControlLaw.SpeedCeiling` (111.76 m/s) whatever the airframe can do, so a
wingman handed a faster leader falls behind for the rest of the mission. That is the original's
ceiling, not a port artifact, and it is why the in-engine check flies its leader at a cruise lever.

## Crash avoidance is a STATE, not an altitude rule

Decoded 2026-08-15 to answer "the nets are authored at 400 m, what stops an AI flying into high
ground?". Nothing in the net does: node altitudes are absolute and authored, and neither
`FUN_00432010` nor the follower `FUN_0041d1f0` samples terrain. The answer is a separate
per-plane check that flips the AI substate at vehicle `+0x358`.

`FUN_0041f810`, reading the vehicle's own Y at `+0x208`:

| Y | What happens |
|---|---|
| below `DAT_0071c3f0` (**20.0**) | state `+0x358` = **3** outright, no ray and no throttle |
| `DAT_0071c3f0` … `DAT_0071c3f4` (**8000.0**) | cast a ray, hit sets state 3, a clear ray clears it |
| above `DAT_0071c3f4` | no check, and a state of 3 is CLEARED to 0 every frame |

Both bounds are hard immediates written at level setup by `FUN_004735b0`: `0x45fa0000` = 8000.0 at
`0x00474151` and `0x41a00000` = 20.0 at `0x0047415b`. They are not per-mission and not authored.
The nose-speed floor 4.4700 (`DAT_0071c3f8`, `0x408f0d84`) is written in the same run.

The ray is the vector the vehicle's virtual `+0x04` accessor returns, scaled by **4.5**, passed to
`FUN_004c8f70`. ⚠ That accessor is velocity by shape and by use, which makes the ray about 4.5
seconds of travel, but the identification is inferred rather than read off a name; the 4.5 itself
is exact. The check is throttled per plane by `+0xf0` against the game clock `DAT_0071c470` to
**clock + 0.5…1.0 s**, the fraction drawn as `rand() × 3.051851e-05` (that is `rand()/32767`), so
it is not a per-frame cast. The throttle gate returns before the cast and before the clear, so
state 3 persists between due checks; the caller resets `+0xf0` to the current clock when a vehicle
first becomes active, so a plane that pops in checks on its first tick rather than waiting out a
fresh interval.

⚠ **A clear ray releases the state in the same call.** There is no consecutive-clear counter and no
hold: the ray misses, control falls through to `if (state == 3) state = 0`, and the plane is back on
its route on the next tick. The climb-out is exactly as long as the obstacle keeps arming it.

State 3 is consumed by the follower's own switch on `+0x358` (`FUN_0041d1f0` case 3, and the same
case in the `+0x67c == 1` arm): the steering target becomes **the plane's own position with
Y + 1000**, flown through parameter block `DAT_0061fb48` rather than patrol's `DAT_0061fb68`, i.e.
a throttle band of 0.6–1.3 instead of 0.8–1.1. So the climb-out is both a different target and a
hotter lever, and it ends when the check stops setting the state.

Two neighbouring facts about the same floor. `FUN_0041b560`, the shared steering law, reads
`DAT_0071c3f0` directly (`0x0041b5c5`, `0x0041b5d5`), so the floor is enforced inside the control
law as well as by this check. And `FUN_004216e0` (the maneuver, reached from the follower
`FUN_0041d1f0` at `0x0041d4f2` and the merge rule `FUN_0041d9f0` at `0x0041da75`) bypasses both
bounds: it stashes `DAT_0071c3f0` and writes −FLT_MAX (`0x004216f5`), with a paired per-vehicle
ceiling at `+0x314` written +FLT_MAX, and restores both at `0x00421775`.

⚠ **That bypass spans one steering solve, not the maneuver's duration.** The two writes bracket a
single `FUN_0041b560` call and the restore is the next thing the function does, so the clamp is off
for one aim-point evaluation. It therefore never reaches `FUN_0041f810`, which the frame loop calls
from its own site and which always sees the restored 20.0. Nothing in the original suspends crash
avoidance while a maneuver runs.

### Which state wins

The precedence is in the caller, `FUN_004897c0`, not in either function, and it runs in this order
per vehicle per frame:

```
if (mode is 0, 1 or 4) {                             // the dynamics class gate, 0x00489a90
    if (state < 4)                    FUN_0041f810(v);   // the crash check
    if (state == 4 && clock >= +0xc0) state = 0;         // stun expiry
    if (+0x9bc != 0 && state < 2)     state = 2;         // a queued maneuver starts
}
FUN_0041c270(v);                                     // the AI brain, and so the net follower
```

Three rules fall out of the inner block. The crash check runs in states 0 to 3 only, so **stunned
(4) and state 5 suppress it entirely**. It writes 3 without consulting the prior state, so it
**pre-empts a running maneuver**. And a maneuver only starts from state 0 or 1, so once crash
avoidance holds state 3 the maneuver stays queued until the climb-out releases.

### Only three dynamics classes get crash avoidance at all

The whole block above sits behind a test of `+0x67c`, the def's `mode` key (`jet` 0, `heli` 1,
`tank` 2, `ship` 3, `wingman` 4, `plane` 5; see [`flightModel.md`](flightModel.md)). At
`0x00489a90` the class is read and compared against 0, 4 and 1; anything else jumps to
`0x00489b12`, which is the `FUN_0041c270` call itself. So a vehicle of any other class **still
steers its net every frame and simply never runs the crash check**, never expires a stun and never
starts a maneuver. Since `FUN_0041f810` is the only writer of state 3, the follower's own
avoid-crash case is dead code for those classes.

**Zeppelins therefore have no crash avoidance, and could not have it in this install.** They are
not registry vehicles at all: `extracted/zrdr/vehicle.zrd.json` names no zeppelin, blimp or airship
type, so a zeppelin is a gamez scene node driven by `mis_anim` rather than an object in the vehicle
list this tick walks ([`flightModel.md`](flightModel.md), "Zeppelins"). It has no `+0x67c` to pass
the gate with and no `+0x358` to hold a state in. The one direction that does work is the other
one: the zeppelin node carries `active` and `intersect_surface`, which is exactly what
`FUN_004c8f70` tests, so **an aeroplane's crash-avoidance ray sees a zeppelin** and climbs out for
it the way it does for a hillside.

⚠ **The floor is a flat world-Y value, not a terrain follow.** 20 m saves a plane over water and
flat ground and does nothing over a 600 m ridge; the raycast is the only terrain-aware part.

**What the ray can hit: other aircraft included** (decoded 2026-08-15, asked as "does crash
avoidance see other planes?"). `FUN_004c8f70` tests terrain plus every scene node whose flag word
at `+0x24` carries `ACTIVE` (0x4) and `INTERSECT_SURFACE` (0x10) (see
[`flightModel.md`](flightModel.md), "Emitters"); it has no vehicle filter. Plane models qualify:
3151 of 3317 nodes in `extracted/planes/nodes.json` carry both flags, including the
`player_pfighter` and `piratefighter` roots. The check itself proves the point: before casting,
`FUN_0041f810` reads its own node's flag word, deactivates the node through `FUN_004cca30`
(`gwNodeSetActive`, named by its own error string at `0x0062cd28`), casts, and restores the
previous active state (`0x41f91e`/`0x41f982`), so the one aircraft excluded from the ray is the
caster. The collision sweep `FUN_0048d7f0` wraps the same query in the same self-exclusion
(`0x48d9c7`/`0x48da3e`), corroborating that aircraft are expected hits. An AI plane whose
4.5-second velocity ray passes through another aircraft therefore enters state 3 and flies the
same 1000 m climb-out as for terrain.

Three bounds on how effective that is in a head-on: the ray is a zero-width line against the other
plane's node volumes, so a small or crossing target can slip between checks; the cadence is one
cast per 0.5…1.0 s per plane, and head-on closure at fighter speeds spends the whole 4.5 s
lookahead in roughly two seconds; and both parties answer with the same straight-ahead climb, so a
mutual detection can still merge. Head-ons in the original are rare, not impossible.

CSVM's probe is `FlightController.AvoidCrashBlocksLine`, masking `CollisionLayers.WorldAndAircraft`
with the caster's own body excluded, which is this rule. `AiModeMachine` carries the rest of the
band structure: `ProbeLookaheadS` 4.5 s, `ProbeIntervalMinS`/`ProbeIntervalMaxS` 0.5…1.0 s drawn
per plane from its seeded rng, `AltitudeFloorM` 20, `ProbeCeilingM` 8000, `ClimbOutM` 1000, and
release on the first clear ray. What remains invented there is the probe geometry inside the middle
band (a second, deck-slanted ray, `ProbeDeckM`) and `ProbeMinLookaheadM`, both marked as such, plus
`AiPilot.ClimbOutBreakM` below.

### Measured: the third bound is the binding one

A spectated 5-versus-5 Bloodhawk dogfight in C1 (a hand-authored `--ia` file, `--debug-spectate
--ai-attack=5 --det --frames=9000`, about 150 s of sim per run) was run to settle which of the
three bounds above actually produces the mid-air collisions CSVM sees. ⚠ `--det` does not pin this
scenario across processes, so each run is a sample; the figures below are per-run averages.

| probe | runs | mid-airs / run | of which head-on | arms / run | arms on an aircraft / run |
|---|---|---|---|---|---|
| the decoded zero-width ray | 8 | 3.63 | ~5 in 6 | 31.0 | 13.8 |
| a swept 10 m sphere | 6 | 3.50 | ~3 in 4 | 79.7 | 66.7 |

Two results. **The ray does see aeroplanes in flight**: 13.8 arms per run on another Bloodhawk's
airframe, against 17 on terrain. And **widening it changes nothing that matters**: the sweep
detects an aeroplane 4.8× as often and the collision rate does not move (3.50 against 3.63, inside
a run-to-run spread of 3 to 4). Detection is not the bottleneck, so the sweep was reverted.

What remains is the third bound, which the decode already names: **both parties answer a detection
with the same straight-ahead 1000 m climb**, so two aeroplanes that both see each other both pull
up along converging tracks and merge anyway. The collisions are overwhelmingly head-on — measured
at impact as the angle between the two velocity vectors, 175°–178° apart on most of them.

### Measured: breaking the climb-out's symmetry is what helps

Same rig, and this time the two arms differ in ONE constant, `AiPilot.ClimbOutBreakM`, so
everything else about the build is identical:

| climb-out | runs | mid-airs / run | of which head-on / run |
|---|---|---|---|
| straight up, the original's | 8 | 3.75 | 3.00 |
| 1000 m up **and 1000 m right of own track** | 14 | **2.21** | 2.14 |

A 41 % cut, Welch t = 2.8 on 13 degrees of freedom, p ≈ 0.014. Right rather than a coin flip is
the point: two aeroplanes meeting head-on that each break right diverge every time, where a random
side still puts them on the same one half the time. It is taken off the ground track and not the
airframe's own right axis, so a rolled or inverted pilot breaks the same way as a level one.

⚠ This is INVENTED and marked so in the code. The original displaces nothing; its climb-out aim
point is the aeroplane's own position with Y + 1000 and no lateral term at all.

⚠ And it is not a cure: 2.2 mid-airs per 150 s of a ten-plane furball is still a lot. It is the
symmetry that was costing the most, not the detection.

⚠ The whole scenario is one the original never produces. Ten netted aircraft all pursuing each
other in one volume is Instant Action as CSVM builds it; the shipped game's actors fly the
chapter's first net (above) and converge rarely.

### Measured: the decoded cadence costs no collisions

Adopting the original's band structure (the 20 m and 8000 m arms, the 0.5…1.0 s per-plane cadence
in place of a flat invented 0.25 s, and release on the first clear ray in place of a four-round
clear streak) probes far less often, so it was A/B'd on the same rig, the two arms differing only
in that change:

| probe rule | runs | mid-airs / run | arms / run | arms on an aircraft / run |
|---|---|---|---|---|
| the invented 0.25 s cadence, 4-round release | 8 | 2.13 | 28.3 | 11.0 |
| the decoded bands, cadence and release | 8 | 1.75 | 24.0 | 4.4 |

Mid-airs do not move: Welch t = 0.97, p ≈ 0.36, so nothing here separates the arms. Detections on
another aeroplane fall by 60 % and the collision rate does not follow, which is the same result the
swept-sphere arm gave from the other direction. Detection frequency is not what produces these
collisions.

## The merge rule: what pursue does when two aircraft close nose to nose

Decoded 2026-08-16 to answer "is there a proximity check against other planes?". There is no
separate one. Beyond the ray above, the ONLY aircraft-aware term in the whole AI is a block inside
pursue, `FUN_0041d9f0` at `0x0041e130`–`0x0041e297`. The net follower `FUN_0041d1f0` has none at
all (its only correction is a cross-track nudge, the perpendicular error × 0.9 clamped to 200 m),
and `FUN_0041afe0`, which pursue calls every frame, is the gunnery lead solve.

The block arms on four conditions, all of which must hold. Let `u` be the unit vector from the
pursuer to its victim:

- the victim casts to `TargetVehicle` and its `+0x67c` is 0 or 4, i.e. it is a `jet` or a
  `wingman`. A ground or sea target never arms this;
- range under **400 m**;
- `dot(ownVelocity, u) > 0.8 × ownSpeed` — the pursuer is flying at the victim, within about 37°;
- `dot(victimVelocity, u) < −0.8 × victimSpeed` — the victim is flying back at the pursuer, within
  the same cone.

What it then does is **not** a break-off:

- the `desiredVelocity` argument handed to the steering law, which is otherwise the victim's own
  velocity, becomes `u × 31.292799` (70 mph). Since the law clamps its speed demand to ±26.8224 m/s
  around the current speed and floors it at 22.352 m/s, this collapses the demand into the pass;
- the aim point's Y gains `sqrt(u.x² + u.z²) × f`, where `f` is `−0.3` at or below 22.352 m/s
  (50 mph), `+0.3` at or above 40.2336 m/s (90 mph), and `(ownSpeed − 31.292799) × 0.033554047`
  between them. ⚠ `u` is already normalised here, so the whole term is worth **0.3 m at most**. It
  is a real branch and it does effectively nothing.

So the engine's answer to a head-on is to throttle back and hold the line. The aim point itself is
unchanged, and it is the victim's own position whenever the aspect test at `0x0041dd49` reads
head-on or tail-chase (`|dot(u, victimBackwardAxis)|` over the pilot's `quick_draw_angle` cosine at
vehicle `+0x960`, default `0.70697`, cos 45°).

⚠ **A second arm exists and is a player-only special case**, the **reversed arm**: pursue's aim
direction is negated, so the pilot steers away from its victim rather than at it, and the block it
passes the steering law is `DAT_0061fb68` instead of `DAT_0061fb08` (see the parameter table below).
It is gated by the global `DAT_0064ee4d`, so at most one aircraft per frame may take it, plus the
roster byte at vehicle `+0x989` (written by `FUN_0047c210` at `0x0047ca5a`, defaulted to 1 by the
vehicle constructor `FUN_004aff80`) and the per-vehicle flag `+0xba`. Inside 400 m the merge block
gives this arm its own response instead of the two above: aim 500 m along the victim's own forward
axis, past it, and demand `−31.292799 ×` that axis. A jousting pass at the player, not avoidance.
CSVM does not port it.

⚠ The aspect test takes **both** of the victim's cones, since it compares a magnitude. A pursuer
sitting on its victim's tail reads the same as one merging with it, and both aim the law at the
victim itself; only a beam aspect flies to the lead point. Nothing downstream tells the two apart
except the reversed arm's own gate.

Ported 2026-08-16: `AiPilot.IsOnGunAxis` (the aspect test, both cones), `AiPilot.IsMerging` and
`AiPilot.MergeVerticalBias`, all applied in `FlyPursuit`.

## The steering law both behaviours call

`FUN_0041b560(this, stationPoint, desiredVelocity, params, emergencyFlag, leadFlag)` turns a target
point into stick and throttle. It is the original's AI control law; the law itself is decoded in
[`aiControlLaw.md`](aiControlLaw.md) and ported as `AiControlLaw`. What this page settles is which
of its **four** 8-float parameter blocks each behaviour passes.

| block | first two | used by |
|---|---|---|
| `DAT_0061fb08` | 0.3, 1.3 | pursue, `FUN_0041d9f0`'s normal arm |
| `DAT_0061fb28` | 0.4, 1.5 | the escort law, and its avoid-crash climb-out |
| `DAT_0061fb48` | 0.6, 1.3 | the net follower's avoid-crash climb-out |
| `DAT_0061fb68` | 0.8, 1.1 | the net follower's normal patrol, and pursue's reversed arm (above) |

⚠ `DAT_0061fb08` is the one block that does not share the others' tail: it carries
`0.08, 0.01, 0.2, 0.025, 0.9, 0.0`. Pursue also scales its own desired-velocity vector by **0.9**
whenever it passes this block.

Shared tail on the other three: `0.06, 0.06, 0.15, 0.025, 0.35, 0.0`. Entries 0 and 1 are the throttle
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
| `FUN_0041d9f0` | pursue, including the 400 m merge rule at `0x0041e130` |
| `FUN_0041e760` | the formation escort law |
| `FUN_0041b560` | the shared steering law: point in, stick and throttle out |
| `FUN_004311c0` | builds the chapter net table from `neindex`, in file order (`FUN_00431300` frees it) |
| `FUN_004314e0` | builds one `CCENet` from its record: nodes, edges, volumes, and the trailer's name→object resolve |
| `FUN_00431a90` | per edge at net load: the delta, its 3-D length, and the arrival radius squared into `edge+0x1c` |
| `FUN_004303d0` | the `CCENet` constructor, whose `+0x28` default is the 10 m arrival-radius floor |
| `FUN_0041d8f0` | steps the walk to the next edge once the arrival test fires, excluding the edge just flown |
| `FUN_00431e40` | which edge leaves a node: the one whose leg best lines up with the vehicle's nose |
| `FUN_00432010` | node position with the trailer offset applied: the "this net rides that object" rule |
| `FUN_00432140` | node position by index, the wrapper every consumer calls |
| `FUN_00431900` | nearest node to a point, skipping edgeless nodes |
| `FUN_00475fc0` | net assignment (`SET_AI_NET`), including the volume overwrite |
| `FUN_00475f30` | the by-name net assignment: table scan on the entry name, then `FUN_00475fc0` |
| `FUN_004735b0` | the `player.zrd.json` loader, including `min_ai_active_dist` |
| `FUN_0049c920` | script-side net assignment, always forces `mode` to `jet` |
| `FUN_0049c880` | releases every wingman onto a net through the above; UNREFERENCED in the image |
| `FUN_00475820` | def to vehicle copy, including `mode` |
| `FUN_00476250` | post-spawn vehicle init, including the wingman demotion |
| `FUN_0047c210` | the roster spawn: `netids` draw, `preferred_engagement_altitude`, activation |
| `FUN_004b0f40` | activate / deactivate |
| `FUN_00479240` | the `vehicle.json` def parser, including the `mode` string table |
| `FUN_004201a0` | evasive-maneuver selection, the one reader of `preferred_engagement_altitude` |
| `FUN_0041c470` | the debug overlay that names the modes and recomputes the target ranking |
| `FUN_0041fe10` | target acquisition: primary target, sticky standing target, else the sweep |
| `FUN_0041f9c0` | the sweep over the four candidate lists, and the pick |
| `FUN_00421ad0` | the scorer for `jet` and `wingman`, with the three ±0.2 terms |
| `FUN_00421950` | the scorer for every other `mode`, base weight and the two class terms only |
| `FUN_0041ae40` | the `rating_biases` lookup, returning rank units |
| `FUN_00420070` | the `DAMAGES_ZEPPELIN` ordnance check that admits gasbag candidates |
| `FUN_0041f420` | the fire decision every behaviour calls, [`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md) |

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
