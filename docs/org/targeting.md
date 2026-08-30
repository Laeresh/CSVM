# Player target selection and the target marker, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-16. Every claim
below names the function or address it came from. The HUD geometry is additionally measured off
`OriginalScreenshots/Targeting HUD Kestrel.png` and `OriginalScreenshots/C1 M04 Zeppelin.png`, and
the measurements agree with the constants to the pixel.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the addresses
are given so any claim can be re-checked at source.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a screenshot reading or with a
remembered behaviour, the decode wins and the disagreement is a note.

**Where the neighbours live.** The gun aim assist, which walks the *same four entity lists* with a
different scorer, is [`aim-assist.md`](aim-assist.md). The AI's own standing target, which lives in
the same plane field but is chosen by entirely different rules, is [`aiPilot.md`](aiPilot.md). The
HUD elements this page does not cover (compass, gauges, pipper) are
[`../formats/hud.md`](../formats/hud.md).

## The headline

The player's target is a **sticky, explicitly chosen** entry in a **per-frame rebuilt, sorted
candidate list**. Eleven input actions drive it: three classes (Enemy/Objective, Ally,
Non-Aircraft) times next/previous/nearest, plus "nearest crosshairs" and "target nothing". The
class is a global mode, not a per-press filter, and it persists until another class's key is
pressed.

Two findings will not be guessed correctly from the screenshots:

- **"Nearest" does not mean nearest.** It means *first in the cycle order*, and the cycle order is
  a four-sector angular sort (ahead, behind, left, right) with distance as the tie-break inside a
  sector, and with mission objectives promoted ahead of everything.
- **The bracket range gate is not a HUD constant.** The brackets draw exactly when the *selected
  gun group could reach the target's intercept point inside its authored `RANGE`*. It is the aim
  assist's own reachability test, re-run for the HUD.

## Function map

| Address | Role |
|---|---|
| `FUN_004895a0` | Input-action registration. Binds action ids `0x24`–`0x2e` to the eleven targeting handlers |
| `LAB_00488680` … `LAB_00488c20` | The nine class handlers, action ids `0x24`–`0x2c` (see the table below) |
| `FUN_00488db0` | Action `0x2d`, **Select Target Nearest Crosshairs** |
| `LAB_00488ca0` | Action `0x2e`, **Target Nothing** |
| `FUN_00488ce0` | The nearest-crosshairs score: 15° nose cone gate, then slant range |
| `FUN_004b5fb0` | **The per-frame targeting update.** Rebuilds the candidate list, sorts it, re-resolves the current selection, publishes the target pose |
| `FUN_004b5cd0` | Per-candidate class filter and list insert |
| `FUN_004bb9b0` / `FUN_004bc020` | The sort (introsort) over the candidate list |
| `FUN_004bbd60` | **The comparator.** This is the cycle order |
| `FUN_004b6490` | Step the cycle: find the current entry, move `±1` with wrap, return it |
| `FUN_004b6480` | The cycle's acceptance predicate. Returns constant `1`; the list is pre-filtered |
| `FUN_004a64e0` | The death/despawn hook. Drops a dead entity from every target field and both lists |
| `0x004a36e0` / `0x004a36f0` | A mission structure's position and velocity getters, vtable `0x00608848` slots 0 and 1: `LEA EAX, [ECX + 0x74]` / `[ECX + 0x80]` |
| `FUN_004a2570` | The structure constructor. Stores the scene node at `+0xc` and fills `+0x74` from `FUN_004cf2c0` |
| `FUN_004a2730` | The per-frame refresh of `+0x74`/`+0x80`, gated on the `+0x8f` moving flag |
| `FUN_004cf2c0` | Node → point: the active bbox (`FUN_004cd960`, six floats at `node + 0x70`), its midpoint (`FUN_004d8b10`), in the frame `FUN_004cef20` builds |
| `FUN_004b9770` | The take-a-hit path. Appends the shooter to the attacker queue |
| `FUN_004a5f40` | `Target::GetColor`, the marker colour rule |
| `FUN_004574d0` | **Draws the bracket box** (six line sprites) and computes the label anchor |
| `FUN_004579e0` | **Draws the three label lines** (`targetHelp` / `targetTitle` / `targetPos` fonts) |
| `FUN_0049d940` | The off-screen case: the edge position and the `%d o'clock` bearing |
| `FUN_0049d8a0` | The clock hour itself (1–12) |
| `FUN_0049fe70` | The HUD frame that calls `FUN_004574d0` and `FUN_004579e0` |

Globals:

| Global | What it is |
|---|---|
| `DAT_0071c298` | The local player's plane (same global the aim assist uses) |
| plane `+0x948` | **The target.** A `Target*` wrapper. The AI uses the same field on its own plane |
| plane `+0x294` / `+0x298` | The target's published position and velocity, refreshed each frame |
| plane `+0x728` / `+0x72c` | The net-transmitted target kind and net id |
| `DAT_0071c478` … `DAT_0071c47b` | The four class-mode flags: Enemy, Ally, Non-Aircraft, Objective |
| `DAT_0071d378` (`+4` = `0071d37c`, `+8` = `0071d380`) | The candidate list, `vector<Target*>`, rebuilt every frame |
| `DAT_0071d388` (`+4` = `0071d38c`, `+8` = `0071d390`) | The **attacker queue**, appended to when the player is hit |
| `DAT_0071dabc` / `DAT_0071d914` / `DAT_0071d33c` / `DAT_0064f78c` | `VehicleList` / turrets / `MStructList` / live fused ordnance. The same four pools the aim assist scans |
| `DAT_0071dacb` | Set only by the `-too` command-line switch (`0x004a749e`). Zero in normal play |

The targeting state is reached once per frame through the flight state's update virtual
(vtable `0x00608310` slot 2 → `FUN_004a0a80` → `FUN_004a0220` → `FUN_004b5fb0`).

## The eleven actions

Action ids come from `FUN_004895a0`; the names come from `extracted/messages.json`, whose
`MSG_CMD_TARGET_*` ids run `10015`–`10023` in exactly the same order as ids `0x24`–`0x2c`.

| Id | Action | Class flags it sets | Step |
|---|---|---|---|
| `0x24` | Next Enemy/Objective | Enemy, if not already in that class | attacker queue first, else `+1` |
| `0x25` | Previous Enemy/Objective | Enemy, if not already (clears the target) | `−1` |
| `0x26` | Nearest Enemy/Objective | Enemy, always | head of list |
| `0x27` | Next Ally | Ally, if not already (clears the target) | `+1` |
| `0x28` | Previous Ally | Ally, if not already (clears the target) | `−1` |
| `0x29` | Nearest Ally | Ally, always | head of list |
| `0x2a` | Next Non-Aircraft | Non-Aircraft, if not already (clears the target) | `+1` |
| `0x2b` | Previous Non-Aircraft | Non-Aircraft, if not already (clears the target) | `−1` |
| `0x2c` | Nearest Non-Aircraft | Non-Aircraft, always | head of list |
| `0x2d` | Select Target Nearest Crosshairs | derived from what it picks | its own scan |
| `0x2e` | Target Nothing | all four cleared | clears the target |

The nine class handlers each play the `sg_switchtarget` sound at gain 1.0 (nine separate copies of
the string, `0x00628968`–`0x006289e8`). **`0x2d` and `0x2e` are silent**, and so is `0x24`'s
attacker-queue branch.

Note the asymmetry the code makes explicit: **"Nearest" always re-asserts its class and restarts
the cycle**, while "Next"/"Previous" only reset when the class actually changes. Pressing the same
class's Next repeatedly walks the list; pressing Nearest returns to its head.

⚠ **The candidate list lags the class flags by one frame.** A handler sets the flags and
immediately calls `FUN_004b6490` on the list that last frame's `FUN_004b5fb0` built under the
*previous* class. The next frame rebuilds under the new class, and because the old selection is no
longer in the list the re-resolve drops to the head of the new list. The visible result self-heals
in one frame, but a port that rebuilds the list synchronously inside the handler will not reproduce
that one-frame step exactly.

## The class model

Class is decided per candidate in `FUN_004b5cd0`, in this order:

1. The candidate's `Target` virtual at vtable `+0x14` (the aim assist's "dead / not yet live"
   predicate) rejects it outright.
2. Entity `+0x4d` set → **Objective** class. This overrides everything, including aircraft.
3. Entity `+0x4c` set → **Non-Aircraft** class. Also overrides the aircraft split.
4. Otherwise, the candidate must be a `TargetVehicle` or a `TargetProjectile`; a `TargetTurret` or
   `TargetStruct` with neither flag is **not selectable at all**. The player's own plane is
   excluded. Teams then split it: different, both non-zero → **Enemy**; same team, or either team
   is 0 → **Ally**. An own-team aircraft additionally has to have `+0x67c` in `{0, 4}`.

`+0x4c` and `+0x4d` are almost certainly the `aiv.zrd` `otherTarget` / `objectiveTarget` fields (the
format comment at `0x00622508` lists them adjacently, and the mission-script verbs
`ADD_OTHER_TARGET` / `ADD_OBJECTIVE_TARGET` / their `REMOVE_` partners exist at `0x00626704`). The
field-to-key binding is inference; the offsets and their effect are traced.

`DAT_0071c47b` (the Objective flag) is set as a *companion* to whichever of the other two the
player is in:

- Enemy class sets `Objective = (DAT_0071dacb == 0)`.
- Non-Aircraft class sets `Objective = DAT_0071dacb`.

`DAT_0071dacb` is written only by the `-too` command-line switch (string `0x006298c4`, parsed at
`0x004a749e`) and is zeroed with its siblings at `0x004b3773`. **In normal play it is 0**, so
objectives ride with the Enemy cycle and not with the Non-Aircraft cycle. That is why the action is
called "Next Enemy/**Objective**"; the switch exists to move them onto the ground cycle instead.

**A zeppelin gasbag is an `MStructList` entry**, not a special case: `TargetStruct` is the only
class binding `0x004227a0` for the vtable `+0x1c` virtual, which returns the object's `+0x65` flag
and is what the AI overlay prints `Gasbag targeted: %s` from (`aiPilot.md`). So a gasbag is
selectable when, and only when, its mission structure carries `otherTarget` or `objectiveTarget`.

⚠ **There is no sub-part enumeration anywhere in the targeting path.** Everything selectable is one
entry in one of the four global pools. Engines and cannons appear only if the mission authors them
as their own `MStruct` entries; the decode does not show the engine walking a zeppelin's parts.

## The team space

The engine has **one** team space, one field, and one hostility predicate. Aircraft, turrets and
world objects all store the same integer in the same place, and nothing anywhere remaps it per
entity kind.

**The vocabulary is three one-line constructors**, and they are the only things that mint a team id:

| Address | Meaning | Body |
|---|---|---|
| `FUN_004a3f80` | neutral | `*out = 0` |
| `FUN_004830c0` | ally (the player's side) | `*out = 1` |
| `FUN_0045c260` | **enemy from index** | `*out = index + 2` |
| `FUN_00453740` | raw / identity | `*out = value` |

So the space is `0` neutral, `1` ally, and enemy team *index* `N` becomes id `N + 2`.

**The field is combat-object `+0x8`**, written by the virtual setter at vtable slot 2 (base
implementation `FUN_00441b80`, the write at `0x00441b86`). Every kind reaches it:

| Entity | Where its team is written | Value |
|---|---|---|
| Aircraft and generic combat objects | `FUN_004a2570`, 4th argument, write at `0x004a259f` | from the caller |
| Vehicles | `FUN_004aff80`, write at `0x004affba` | neutral by default (`FUN_004a3f80` at `0x004affa7`) |
| Turrets | `FUN_004a9a60`, write at `0x004a9a99` | `FUN_0045c260(_, 0)` = **2** |
| Instant Action enemy aircraft | `FUN_0045a390`: enemy index 0 pushed at `0x0045b2ba`, `FUN_0045c260` at `0x0045b2bc`, passed to `FUN_004a2570` at `0x0045b2db` | **2** |
| Authored mission aircraft | `aiv.zrd` tuple slot 3, read verbatim by `FUN_00437620` at `0x00437723` | the authored integer, unmodified |
| Zeppelins and their parts | `FUN_004bd8d0` parses the record, `FUN_004bf030` → `FUN_004bee80` fans one value onto `+0x8` of every component and child | see below |
| World / scene objects | `FUN_004a3360` builds them through the same `FUN_004a2570`; the team comes from `FUN_004a32f0` at `0x004a3493` | see below |

⚠ **A turret's own default is exactly the id an Instant Action enemy fighter carries.** Both are
`2`, minted by the same `FUN_0045c260` at enemy index 0. That is the whole reason the original's
zeppelin turrets do not shoot the fighters that zeppelin launched: the two ids compare equal and
the predicate rejects the candidate.

The mission-script verb `SET_AI_TEAM` (`FUN_00469e20`) confirms the same identity handling. It
takes the script's raw integer and hands it straight to the virtual setter on a vehicle, or to a
zeppelin's `+0xE0` (`0x00469efe`), with no conversion on the way.

### The hostility predicate

Virtual slot `+0x34` on the target wrapper hierarchy. The base vtable at `0x006035f4` has
`__purecall` there; four concrete overrides exist and all four share one team core.

> A candidate is hostile if and only if its `+0x8` differs from the shooter's team **and** neither
> team is `0`.

The canonical implementation is `FUN_004a5b90` (the aircraft/vehicle wrapper, vtable `0x0060364c`),
which first requires the candidate's per-class targetable flags at `+0x8c` and `+0x90`, then makes
the three comparisons at `0x004a5bb0` (equality), `0x004a5bb4` (candidate is 0) and `0x004a5bb8`
(shooter is 0). The siblings are `FUN_004a5b20` (zeppelins and large craft, comparisons at
`0x004a5b40`/`44`/`48`), `FUN_004a5bd0` (`0x004a5bea`/`ee`/`f2`) and `FUN_004a5c10` (emplacement
hosts, `0x004a5c1e`/`22`/`26`). `FUN_004a5c10`'s codegen inverts polarity through
`XOR EAX,EAX / SETZ DL` and resolves to the same truth table.

Two properties matter for a port:

- **Neutral is symmetric and total.** A team-0 object is never anyone's target, and a team-0
  shooter never acquires one. It is excluded from both sides of the test.
- **There are no bands and no magic values.** No override contains a `>=` test, a range check, or a
  special case for any id but `0`. The comparison is raw integer against raw integer.

This is the same rule the class model above applies when it splits Enemy from Ally.

### The turret gunner runs the same predicate

The range-gated picker is `FUN_0041f9c0`, a minimise-cost-over-candidates loop (best seeded to
`1e+20`, rejects scored `1e+21`) walking the four global pools. Its query object is built by
`FUN_00422850`, which stores the shooter's position at `query+4`…`+0xc` and **the shooter's team,
copied from `*(shooter+8)`, at `query+0x10`**; the picker passes `query+0x10` to the predicate.
The turret gun update `FUN_004aabb0` calls `FUN_00422850` at `0x004aaf5c` with its own `*(this+8)`,
calls the picker at `0x004aaf9b`, and stores the result to `turret+0x210` at `0x004aafd7`.
Predicate call sites inside the picker are `0x0041fb9e` (devirtualised straight to `FUN_004a5b90`),
`0x0041fc68` and `0x004228a0`.

So a turret compares its own raw `+0x8` against an aircraft's raw `+0x8`, with no remap on either
side, through the same predicate the player HUD's scan uses.

A turret's `SetTeam` override (`FUN_004acb70`) clears its current target pointer `+0x210` whenever
the team actually changes (`0x004acb90`), so a retargeted turret drops a now-friendly lock rather
than keeping it.

### Zeppelins carry a record override, not a second space

A zeppelin's `+0xE0` (with flag byte `+0xDC`) is the mission record's team override, parsed by
`FUN_004bd8d0` from the strings `enemy` (`0x0062b6c4`), `ally` (`0x0062b6cc`) and `neutral`
(`0x0062b6d4`), or from a raw integer (writes at `0x004bd977`, `0x004bda02`, `0x004bda35`,
`0x004bda5e`). Note which constructor each string picks: `enemy` takes `FUN_0045c260(_, 0)` and is
therefore **2**, `ally` is `1`, `neutral` is `0`, and a raw integer is stored raw. `FUN_004bee80`
then fans that one value out onto `+0x8` across the whole airship, its turrets included.

### World objects are in the same space, and are normally neutral

Destructibles are not a separate class and are not excluded by list membership. A named scene node
becomes a combat object through the find-or-create factory `FUN_004a3360`, which tries the vehicle
registry (`0x004a3385`), the turret registry (`0x004a33cc`) and the combat-object vector registry
(`0x004a3415`), then falls back to resolving a type-7 scene node through `FUN_004d0280(7, name)` at
`0x004a3459`, allocating `0x94` bytes at `0x004a346b` and constructing it with the same
`FUN_004a2570` at `0x004a34a3`. The new object is pushed into the shared candidate registry
`DAT_0071d338` by `FUN_0045c590` at `0x004a34d1`, wrapped in the same `0x14`-byte node
(vtable `0x0060364c`) the Instant Action aircraft path uses at `0x0045b30c`.

Its team comes from `FUN_004a32f0` (called at `0x004a3493`): walk the node, then its ancestors via
`**(node+0x58)`, and at each one extract a **two-bit field** from `node+0x28` at bit offset
`(2 * DAT_0071c0a0 - 2) & 0x1f`. The first non-zero value found is the team id directly. If every
ancestor yields zero, it falls through to `FUN_004a3f80` and the object is **neutral**. Two writers
of that packed field are `OR [EAX+0x28],0x40000000` at `0x0048490c` and `OR [EDI+0x28],0x10000000`
at `0x004807ef`, both setting a slot to value 1 (ally).

So hostility toward a world object is decided by team number like everything else, and an
unauthored one is untargetable because it is neutral, not because it sits outside the pools.

⚠ **The ownership field is two bits wide**, so a scene node can only ever author `0`–`3`. That is a
bound on *this* field, not on the space: the stored id at `+0x8` is a full integer, and an
`aiv.zrd` team is a raw integer read verbatim. What it corroborates is that the ids in play are
small and that no banding scheme exists anywhere.

## The candidate list

`FUN_004b5fb0`, once per frame:

1. Release every entry of the previous frame's list. **The list is rebuilt from scratch every
   frame**; nothing about it persists.
2. If any of the four class flags is set, walk all four pools and offer each candidate to
   `FUN_004b5cd0`:
   - `VehicleList` (`DAT_0071dabc`): every entry.
   - Turrets (`DAT_0071d914`): only if `+0x4c` or `+0x4d`.
   - `MStructList` (`DAT_0071d33c`): only if `+0x4c` or `+0x4d`.
   - Live fused ordnance (`DAT_0064f78c`): only if its `+0x6c` tracking byte is set. `FUN_00441830`
     sets that byte on the `TARGETABLE` path alone, so a round wrapped only because it is fused is
     on the list and unselectable. The wrapper's display name is the literal string id `0x2f6a`
     (`MSG_WEAP_AERIAL_TORPEDO`, "Aerial torpedo") for every such round, not the weapon's own
     `DESC`; see [ordnanceTypes.md](ordnanceTypes.md).
3. Sort with `FUN_004bb9b0` under the comparator `FUN_004bbd60`.
4. Re-resolve the current selection: `FUN_004b6490(currentTarget, 0)` finds the entry whose
   underlying entity matches, or returns the **first entry** if it is gone. Clone it, release the
   old, store at `+0x948`.
5. Publish the target's position and velocity to plane `+0x294`/`+0x298`, or zero both if there is
   no target.
6. In a network game, publish the target kind and net id to `+0x728`/`+0x72c`.

Step 4 is the entire lifecycle mechanism. See "Lifecycle" below.

## Where a mission structure is

Step 5 reads the entity's own position through vtable slot 0. For a mission structure that slot is
`0x004a36e0`, three bytes long: `LEA EAX, [ECX + 0x74]`. It returns a cached vector, and slot 1
(`+0x80`) returns the velocity the same cache derives.

**`+0x74` is the world-space CENTRE of the structure node's active bounding box, not the node's
origin.** The constructor `FUN_004a2570` stores the scene node at `+0xc` and fills `+0x74` from
`FUN_004cf2c0(node)`, which reads the node's active bbox (`FUN_004cd960` copies the six floats at
`node + 0x70`) and takes its midpoint (`FUN_004d8b10`: `(min + max) * 0.5` per axis) in the frame
`FUN_004cef20` builds up the parent chain. `FUN_004a2730` recomputes the same value every frame for
a structure whose `+0x8f` moving flag is set, and derives `+0x80` as the frame-to-frame delta over
the tick, which is why a structure on a flying node is marked where it now is.

⚠ **A group node's own origin is not its site.** Two shipped cases prove it: C2's `sghangar` carries
the no-transform `"Initial"` and so stands at the world origin 8 km from the building, and C1/M05's
nine `lifesaverNM` attack-balloon groups stand on the water with the balloon hung 16.4 m above them
and the lifeboat at the group's own origin, so a marker on the node reads as a marker on the boat.
The authored bbox is the answer in both: `lifesaver11`'s `child_bbox` spans y −1.411 to 24.745 in
group coordinates, putting its centre 11.667 m up.

## The cycle order

`FUN_004bbd60` compares two candidates against a 15-float snapshot of the player: position in
`[0..2]`, then the plane's basis rows from `+0x180` in `[3..14]`. For each candidate:

```
v      = targetPos − playerPos
a      = dot(v, row0)        // row0 = plane +0x180, the RIGHT axis
b      = dot(v, row2)        // row2 = plane +0x198, the NEGATED forward axis
theta  = atan2(b, a) + pi/4            // [0x00608038] = 0.7853982
theta  = wrap(theta, 0 .. 2pi)         // FUN_00460b10
q      = trunc(theta * 2/pi)           // [0x006035a8] = 0.63661975  ->  q in 0..3
key    = q == 0 ? 3 : q == 3 ? 0 : q   // swap the ahead and right sectors
```

with two overrides that set `key = −1`:

- the entity has `+0x4d` set (an objective), or
- the candidate is a `TargetProjectile` whose owner is on a different, non-zero team (incoming
  ordnance).

Candidate A sorts before B when `key(A) < key(B)`, or when the keys tie and `|vA|² < |vB|²`.

Because `row2` is the *negated* forward axis (the same convention `aim-assist.md` records for
`FUN_004b6530`'s seed), the resulting sector order is:

| key | Sector | Meaning |
|---|---|---|
| −1 | (none) | mission objectives and incoming hostile ordnance |
| 0 | `theta` in [5pi/4, 7pi/4) | **ahead**, the 90° quadrant centred on the nose |
| 1 | `theta` in [pi/4, 3pi/4) | **behind** |
| 2 | `theta` in [3pi/4, 5pi/4) | **left** |
| 3 | `theta` in [−pi/4, pi/4) | **right** |

Nearest first inside each sector. The sectors are 90° wide and centred on the axes, so a target
45° off the nose sits on a boundary.

**So "Nearest Enemy/Objective" returns: the nearest objective if any exists, otherwise the nearest
enemy in the forward quadrant, otherwise the nearest one behind, otherwise left, otherwise right.**
It is not a global nearest, and a target 200 m off the left wing loses to one 900 m ahead.

## Stepping the cycle

`FUN_004b6490(current, dir)`:

- Scan the list for the entry whose *underlying entity* matches `current`. Match by entity, not by
  wrapper pointer, because the wrappers are new objects every frame.
- Not found (or `current` is null): return the first entry, or 0 if the list is empty.
- Found: `dir > 0` steps forward with wrap to the head, `dir < 0` steps backward with wrap to the
  tail, `dir == 0` stays put.
- After each step, `FUN_004b6480` is consulted and returns constant `1`, so every entry is
  acceptable; the loop's "came back to where we started, give up" guard is the only exit.

## Next Enemy/Objective prefers whoever shot you

Action `0x24` alone consults a second list first, and this is the least guessable behaviour on the
page.

`FUN_004b9770` is the take-a-hit path. When the local player is hit by a shooter on a different,
non-zero team, it wraps that shooter as a `TargetVehicle` and appends it to the queue at
`DAT_0071d388` via `FUN_0045c590(DAT_0071d390, 1, …)`, the end insert. The queue is
cleared only at mission load (`0x00474b64`) and pruned by the death hook `FUN_004a64e0`.

`0x24` then walks that queue **backwards from the end**, i.e. most recent attacker first:

- Queue empty → fall through to the ordinary cycle step `+1`, and play `sg_switchtarget`.
- Current target not in the queue → select the **last** entry, the most recent attacker.
- Current target found in the queue → select the entry **before** it, an older attacker.
- Current target is the queue's first entry → fall through to the ordinary cycle step `+1`.

None of the queue branches play the switch sound; only the fall-through does.

⚠ `FUN_004bc1e0` is called on the wrapper immediately before the insert and is probably a
remove-if-present, which would move a repeat attacker to the end rather than duplicating it. Not
traced.

## Select Target Nearest Crosshairs

Action `0x2d`, `FUN_00488db0`. It ignores the class flags and the candidate list entirely and runs
its own scan over all four pools, scoring each with `FUN_00488ce0`:

```
v = targetPos − playerPos
d = |v|
if dot(v, row2) > d * −0.9659258   ->  reject (FLT_MAX)
else                               ->  score = d
```

`0.9659258` is `cos(15°)`, and `row2` is the negated forward axis, so the test accepts a target
**within a 15° half-angle cone of the nose** and scores it by plain slant range. The running best
starts at **2000.0**, which is therefore a hard 2 km maximum range for this action.

It scores against the **nose axis**, not against the gun pipper. The pipper is a separate,
velocity-derived point (`../formats/hud.md`, `aim-assist.md`), and nothing in this path reads it.

Candidate gates in the scan are looser than the cycle's:

- Aircraft: everything except the player's own plane, and except own-team aircraft whose `+0x67c`
  is neither 0 nor 4. **Friendlies are included**, which is why the action reaches an ally.
- Turrets, structures: only with `+0x4c` or `+0x4d`.
- Ordnance: only with the `+0x6c` tracking byte.

Having chosen, it *writes the class flags back* from what it found, so a subsequent Next/Previous
continues in that target's own class: ally → Ally, enemy → Enemy, turret/structure/`+0x4c` →
Non-Aircraft, and `+0x4d` → the Objective companion pair.

## Lifecycle

- **Mission start.** `0x00474a6b` sets the class to Enemy/Objective (`478 = 1`, `47b = 1` with
  `-too` absent) and clears both lists. The target itself is null.
- **Auto-acquire.** Because the class flags are non-zero from mission start, `FUN_004b5fb0` builds
  a list on the first frame and its re-resolve step returns the list head. **The player therefore
  starts a mission already targeting the head of the Enemy/Objective cycle**, with no input.
- **Target death or despawn.** `FUN_004a64e0` is the hook: it nulls the player's `+0x948` if it
  points at the dying entity, removes it from the candidate list and the attacker queue, nulls
  every AI's `+0x948`, and clears any turret's `+0x210`. The next frame's re-resolve then picks the
  **head of the current cycle**. So the original does switch on death, and it switches to the head,
  not to the neighbour of the dead entry.
- **Target merely leaving the list** (dying, going out of the class, or the class changing) has the
  same effect: re-resolve drops to the head.
- **Explicit clear.** `0x2e` nulls the target *and* all four class flags. With every flag zero,
  `FUN_004b5fb0` skips the collection pass, the list stays empty, and the re-resolve returns 0. So
  **`Target Nothing` stays cleared**, because the auto-acquire cannot fire again until a class key is
  pressed. This is the mechanism behind the sticky-clear, and it is worth pinning with a test.
- **Range, line of sight and field of view drop nothing.** No such gate exists anywhere in the
  path.
- **The player's own death** was not traced. `FUN_00421500` and `FUN_00469e20` both zero a plane's
  `+0x948`, and whether either runs on the player's respawn is unresolved.

⚠ **The player and the AI share the field, not the rules.** `+0x948` is one field on the shared
plane class; `FUN_0041fe10` writes it for an AI actor using rating biases and a standing-target
hysteresis (`aiPilot.md`), and `FUN_004b5fb0` writes it for the local player using this page's
rules. Reading `aiPilot.md`'s selection logic as the player's is the mistake this note exists to
prevent.

## The HUD: the bracket box

`FUN_004574d0` draws **six line sprites** forming a `[ ]` pair around the target's projected point.
The geometry is three absolute pixel constants:

| Constant | Value | Role |
|---|---|---|
| `0x00607a0c` | **10.0** | half-width |
| `0x00607a14` | **8.0** | half-height |
| `0x00607a10` | **4.0** | arm length |

so the box is a fixed **20 × 16 pixels** with 4-pixel arms, centred on the projected point. Nothing
scales it: not range, not resolution, not field of view. The only depth test is the near-plane
epsilon `0.1` at `0x006034a8`; behind the camera, all six sprites hide.

**Measured, and it matches exactly.** On `Targeting HUD Kestrel.png` the two vertical strokes sit at
x = 1943 and x = 1963 (20 px apart) and span rows 202–218 (16 px), with 4-pixel arms at both ends.
On `C1 M04 Zeppelin.png`, a different shot at a different resolution, the strokes sit at x = 1839
and x = 1859 and span rows 834–850. Same 20 × 16 box.

### The range gate is the selected gun's `RANGE`

This is the bracket range threshold, and it is not a HUD constant.

If a gun group is selected (plane `+0x604` index is non-negative and its weapon def resolves),
`FUN_004574d0`:

1. Solves the lead intercept with `FUN_00460e30`, the aim assist's own solver, on the target's
   position and relative velocity.
2. Forms the muzzle velocity vector `dir * VELOCITY + planeVelocity` (`VELOCITY` is def `+0x2c`).
3. Draws the box only when the solver succeeded **and** `|muzzleVel|² · t² <= RANGE²`
   (`RANGE²` is def `+0x20`, precomputed at parse, see `aim-assist.md`).

Otherwise all six sprites hide. If **no** weapon def resolves, the fallback gate is a plain
`distance <= 1e6`, which never fires in practice.

So the observed "brackets only under a range threshold" is exactly "the selected gun could reach
the target's intercept point inside its authored range". For the player guns that authored range is
`RANGE 1000` (`../formats/weapons.md`), so the box appears at roughly 1 km and closer, less
whatever the target's own motion costs in intercept time. A target outrunning the round gets no box
at any range, because the solver returns no intercept.

⚠ **This makes the marker weapon-dependent.** Switching gun groups changes when the box appears,
and a rocket-only loadout has no gun group to gate on. That is behaviour the screenshots cannot
show and a fixed-metres port would lose.

### Colour

`FUN_004a5f40` (`Target::GetColor`) returns one of three colours:

| Condition | Colour |
|---|---|
| No category label (`+0x40` clear), teams differ and both non-zero | **(200, 0, 0)** red |
| No category label, same team or either team 0 | **(0, 255, 0)** green |
| Category label is `Destroy`, `Disable`, `Disable Engines` or `Damage` | **(200, 0, 0)** red |
| Any other category label | **(0, 0, 255)** blue |

The four red-listed category ids are `0x1f42`, `0x1f46`, `0x1f49` and `0x1f8d`, which are
`extracted/messages.json` ids 8002 / 8006 / 8009 / 8077, i.e. `MSG_OBJ_DESTROY`, `MSG_OBJ_DISABLE`,
`MSG_OBJ_DISABLEENG`, `MSG_OBJ_DAMAGE`.

⚠ **A friendly target is green, not blue.** Blue is reserved for a non-destructive objective
(protect, escort, and the rest of the `MSG_OBJ_*` set). The `hud_v2.zrd` `HUD_COMMON` block's
`COLOR_RED (200,20,20)` / `COLOR_BLUE (40,40,240)` are a different pair and are not these.

## The HUD: the label

`FUN_004579e0` draws **three stacked text lines**, 15 pixels apart, each in its own font: line 1 in
`targetHelp`, line 2 in `targetTitle`, line 3 in `targetPos` (the font names come from
`FONT_TARGETHELP` / `FONT_TARGETTITLE` / `FONT_TARGETPOS`, `extracted/messages.json` ids
3880/3885/3890, and are looked up through `FUN_00530700`). All three hide when there is no target.

**Line 1** composes the entity's own label pair, `+0x28`/`+0x2c` (the name) and `+0x3c`/`+0x40`
(the category), through one of four format strings:

| Case | Format | Address |
|---|---|---|
| Both present | `"%s [%s] -"` | `0x006253ac` |
| Name only | `"%s -"` | `0x006253b8` |
| Category only | `"[%s] -"` | `0x006253c0` |
| Neither | blank | `DAT_006f1ee8` |

**Line 2** is the entity's own name string at `+0x14`/`+0x18`, and its source is settled: the
**roster block's own `title`** (`aiv.zrd` slot 20, the `MSG_*_NAME` pilot key). The block reader
`FUN_00437620` `_strdup`s element 20 into the block struct's `+0xc` (from `src+0xac`, the reader's
`list + 0xc + 8k`), and the spawn path `FUN_0047c210` reads it at `0x0047c9a2`, resolves it through
`FUN_0059cd20` (key to message id, stored at entity `+0x20`) and `FUN_0059ce40` (id to text), then
assigns the text into the `std::string` at entity `+0x10` at `0x0047c9c8`. A key the string table
does not know is copied verbatim instead (`0x0047ca98`–`0x0047cafd`).

⚠ **A block that authors an empty `title` gets no name line at all** — the branch at `0x0047c9a7`
leaves the string empty. That is 239 of the install's 414 roster blocks, so most enemies in the
original show a box and no name.

**Three authors write that string, and the vehicle definition is none of them.**

- **The campaign roster**, as above: `aiv` slot 20 through the block struct's `+0xc`.
- **Instant Action**, in `FUN_0045a390`, which builds the same block struct three times and writes
  `+0xc` in each: the player's flight from five hardcoded string ids (`0x32c9`, `0x32cb`, `0x32cc`,
  `0x32cd`, `0x32e4`) at `0x0045a7ae`, the ace from `ia.zrd`'s `ace_name` at `0x0045ab0a`, and each
  enemy group from its own `enemy_name` at `0x0045ae73`. `ia.zrd`'s parser resolves both keys at
  parse time (`0x0045946b`, `0x00458e36`), so they arrive as display text. This is why an Instant
  Action Kestrel carries a name line while an unnamed campaign block does not.
- **A template-less generator spawn**, in `FUN_00451bf0`, which assigns entity `+0x10` directly from
  the `egen.zrd` record's `vehicle`/`title` value at `0x004520ee`. That path takes the value RAW,
  with no string-table lookup, so whatever the file authors is displayed literally. The two are
  complementary: an `egen` record that resolved a roster block runs the roster path above instead.

⚠ **The `vehicle.zrd` def's own `title` (`MSG_VEH_*`) is read by none of them.** `FUN_00479240`,
the vehicle-definition parser, stores it at that definition's `+0xc` (`0x004792ca`–`0x00479305`),
a different object. ⚠ "No fourth author exists" is NOT established: the sweep covered the entity
constructor's site and all five of its callers, not the whole image.

**Line 3** is the `%d o'clock` bearing. `FUN_0049d940` computes the hour and formats
`MSG_N_OCLOCK` (`extracted/messages.json` id 132, `"%1!d! o'clock"`) into a 256-byte buffer, and
`FUN_0049d8a0` produces the hour itself: the difference between the plane's world heading and the
bearing to the target, wrapped to `[0, 2pi)`, converted to twelfths and truncated, with 0 mapped to
12.

⚠ **Nothing wraps.** The zeppelin's two lines are two *different elements* with different fonts, not
one string broken at a wrap width. There is no wrap width to port.

So `C1 M04 Zeppelin.png`'s two visible lines are line 1 (`Zeppelin [Destroy] -`) and line 2
(`Promised Land`), and `Targeting HUD Kestrel.png`'s single visible line is **line 2**. An
ordinary aircraft carries no category label, so line 1 renders blank and only the name shows.

### Where the label sits

`FUN_004574d0` computes the anchor from the bracket box:

- `anchor = boxBottom + 3` when `boxBottom + 33` still fits inside the viewport,
- otherwise `anchor = boxTop − 30`.

So **the label is below the box in the normal case and flips above it** when the box is within 33
pixels of the viewport's bottom edge. The three lines then sit at `anchor`, `anchor + 15`,
`anchor + 30`, and the block is horizontally centred on the box.

**Measured.** Kestrel: box bottom row 218, anchor ink at rows 221–222, the `Kestrel` glyphs at rows
239–247. That is `218 + 3 = 221` for the anchor and `221 + 15 = 236` for line 2, with ~3 px of
font leading before the ink. Zeppelin: box bottom row 850, line 1 ink at rows 856–866, line 2 ink at
rows 871–879, a 15-pixel line pitch and the same ~3 px leading. Both label blocks are centred on
their box to within a pixel (Kestrel ink centre 1953 against box centre 1953; zeppelin 1848.5
against 1849).

### Off screen

`FUN_0049d940` also computes a clamped **edge position** and stores it at `DAT_0071d284 + 0x200` /
`+ 0x204` behind a flag at `+ 0x208`. `FUN_004579e0` reads exactly those three and, when the flag is
set, places the label block at the edge position instead of at the projected point; it additionally
clamps each line into the viewport with a 3-pixel margin. The bracket box hides in this case,
because `FUN_004574d0`'s near-plane test fails. That is the `HUD.png` reading: an edge arrow with
the tag and the clock bearing stacked beside it.

⚠ **The edge arrow sprite itself was not traced.** Its position is `FUN_0049d940`'s output; which
element draws the triangle, and how it is rotated, is unresolved.

## What this means for CSVM

| | Original | CSVM today |
|---|---|---|
| Who picks the target | the player, from eleven bound actions | nothing; `VersusHud.NearestHostile` re-picks the nearest live AI hostile every frame |
| Selection state | sticky in plane `+0x948`, survives everything except death and an explicit clear | none; there is no selection |
| Candidate pool | four typed pools, rebuilt and re-sorted every frame | `AimCandidateSet`'s same four lists exist for the gun assist, but the marker walks `ProjectilePool.CollectAircraft` alone |
| Classes | Enemy / Ally / Non-Aircraft, plus an Objective companion flag | none; a single team gate |
| Team space | one space for everything: `0` neutral, `1` ally, enemy index `N` = `N + 2`, stored at `+0x8` on every combat object | the same space; an authored id is the runtime id |
| Hostility test | one predicate over raw ids: differ, and neither is `0` | `AimAssist.Hostile`, asked by both the gun assist and the turret gunner rather than restated at each gate |
| Splitscreen pilots | no per-pilot ladder exists | a remake-only rule: pilot 0 is the player's side, further pilots land in `AimAssist.VersusTeamBand` so a `--vs` player cannot inherit the id the no-`TEAM` emplacements default to |
| World objects | neutral until a scene node authors two-bit ownership, and untargetable while neutral | the same: `AimCandidateSet.AddStructures` falls a pool with no authored team through to `AimAssist.NeutralTeam`. Only a zeppelin record authors one, and the two-bit ownership field has no authored writer at all (both `crimson.exe` writers are runtime `OR`s at `0x0048490c` and `0x004807ef`) |
| Turrets and structures | selectable **only** when the mission flags them `otherTarget` / `objectiveTarget` | not selectable |
| Cycle order | objectives first, then ahead / behind / left / right, nearest inside each sector | not applicable |
| "Nearest" | head of that order, not a global nearest | not applicable |
| Nearest-crosshairs | 15° nose cone, nearest inside it, 2000 m cap, friend or foe | not applicable |
| Marker box | fixed 20 × 16 px with 4 px arms, gated on the selected gun's `RANGE` through a lead solve | no box; a text tag only |
| Label | three lines, 15 px pitch, below the box (above near the bottom edge), centred | one line **above** the projected point (`RefOnScreenLift`) |
| Label content | `<name> [<category>] -` / proper name / `%d o'clock` | `HostileTag` cuts the node name at the first `_` to get `AI1` |
| Name line's source | the roster block's `title` alone; an unnamed block shows no name | a campaign spawn takes the block's `title` (`AiSpawn.PilotName`), and where it has none the remake keeps an airframe title the original does not print there |
| Colour | red hostile, green friendly, blue non-destructive objective | HUD red for hostiles, HUD blue for own team under `--debug-markers` |
| Off screen | edge position plus the same three lines, clamped with a 3 px margin | edge arrow plus a one-line tag (`DrawOpponent`) |

⚠ **The "no reference to copy" claim once made in `VersusHud`'s module doc was false.** The
original draws an edge arrow with a stacked tag and clock bearing, which is what CSVM's
`DrawOpponent` edge branch already does — corrected in the code itself.

⚠ **Fixed 20 × 16 pixels does not port literally.** The original never scales its box, so on a
modern display it would be nearly invisible. Scaling through `HudMetrics` like every other CSVM HUD
element is the right port and is a deliberate divergence, not a fidelity loss.

## Not resolved

- What clears the player's target on the player's **own** death or respawn. `FUN_00421500` and
  `FUN_00469e20` both zero a plane's `+0x948`; neither was traced to the player's respawn path.
- The edge arrow sprite: which element draws it and how it is oriented.
- Whether `FUN_004bc1e0` de-duplicates the attacker queue.
- The `aiv.zrd` key names behind entity `+0x28` (label), `+0x3c` (category), `+0x4c` and `+0x4d`.
  The name at `+0x14` is settled (above, slot 20 `title`), and the other two are read from the same
  block struct: `+0x28` from block `+0x114` (block element 38, `0x0047d53f`) and `+0x3c` from block
  `+0x118` (element 39, `0x0047d69e`). ⚠ Their SCHEMA names are still inference: the exe's format
  comment at `0x00622508` names three fewer fields between `primary_target` and `title` than
  `FUN_00437620` consumes, so aligning the comment's tail on `title = 20` (which would make 38
  `categoryLabel` and 39 `helpLabel`) is a `+3` shift that has not been proved.
- The text elements' font metrics. The ~3 px of leading between a line's anchor and its ink is
  measured on two shots, not decoded.
- Whether the box's absolute pixel constants interact with the resolution-doubling branch in
  `FUN_00457400` (`FUN_00440bb0`). Both measured screenshots render at about 2560 px wide, so they
  do not test a second resolution.
- Whether an Instant Action **wingman** reaches the ally constructor. That `1` is the ally id is
  certain, but `FUN_0045a390` never calls `FUN_004830c0`; its other three `FUN_004a2570` sites go
  through `FUN_004a3360` (`0x0045a9ba`, `0x0045b522`, `0x0045b962`) and derive the team from the
  scene node instead of a literal.
- Where an authored mission aircraft's loaded record field `+0x34` is transferred onto the runtime
  object's `+0x8`. The instantiation loop `FUN_004735b0` walks the record list
  (`0x00474cba`…`0x0047535d`), `__RTDynamicCast`s to the vehicle record at `0x00474cfb` and ends at
  `FUN_004a3360`, but reads no `+0x34` and calls no setter. That the authored integer is the engine
  id is confirmed at the file-format level (`0x00437723`, and the writer `FUN_00438010` printing
  `+0x34` straight through `%d`) and at the script level (`SET_AI_TEAM`), not end to end. No `+2`
  remap exists on that path.
- Whether the AI's own target selection uses a range-gated scan at all. `FUN_0041f9c0` has three
  callers: the turret update, and two reached from a draw callback registered at `0x004739e8`, that
  is the player HUD. The AI's standing target appears to come from mission data
  (`primary_target` / `otherTarget` / `objectiveTarget`) rather than a proximity picker, but that
  was not traced.
