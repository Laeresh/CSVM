# The vehicle damage model, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-13, to settle which of two shipped numbers is
the patrol boat's hit points. That backlog item is retired on the strength of this page; its closing
commit is `git log --grep=BL-102`. Every claim below names the function it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Scope.** One class covers everything the player can shoot down that flies or drives, and it is
selected by **how the instance was created, never by what it is**: a roster entry spawns a vehicle,
which runs the code on this page (player plane, AI plane, autogyro, patrol boat, truck). A compiled
anim def anchored to a placed world node makes a destructible, which runs the *other* system,
described in [`formats/destructibles.md`](../formats/destructibles.md). Nothing in the executable
special-cases a vehicle type; the split is entirely in the data. The patrol boat is authored into
both, which is what made its hit points look contradictory, and the last section works that through.

**Where the authored side lives.** [`formats/vehicle.md`](../formats/vehicle.md) has the
`armor`/`health` pair, `destroyable_parts` and `injure_anims`;
[`formats/ai-rosters.md`](../formats/ai-rosters.md) has the `aiv` roster slots this page shows being
consumed (`init_health`, the four zone pairs, `armor`).

## Function map

| Address | Role |
|---|---|
| `FUN_005abcf0` | The impact dispatcher: calls the handler the struck node registered at `+0xbc` |
| `0x004b9750` | The handler a vehicle registers there, a thunk onto `FUN_004b9770` |
| `FUN_004b9770` | Resolves the shooter from the round, files the radio and threat calls, then enters the wrapper |
| `FUN_004b9b30` | **The take-hit entry point, a WRAPPER that LOOPS** (found 2026-08-14 — see the correction section) |
| `FUN_004b3950` | The struck-zone resolver: matches hit geometry against parts **with health remaining only** |
| `FUN_004b3b60` | The resolver's miss fallback: rand() over the first up-to-3 surviving parts |
| `FUN_004b7f30` | Clamped pool subtract: pool -= damage floored at 0, returns the leftover |
| `FUN_00479240` | The `vehicle.zrd` parser. `health` -> def`+0xb4`, `armor` -> def`+0xb8`, each written only if the key is present |
| `FUN_00475820` | Def -> instance: the whole-vehicle pair, the `destroyable_parts` array, the death-anim reference, and the def pointer itself |
| `FUN_00476250` | Spawn/reset: resolves model nodes, fills the per-part pools from the def, and applies the per-spawn jitter |
| `FUN_0047c210` | The roster spawn: applies an `aiv` block's `init_health`, `armor` and four zone pairs, then the difficulty scale |
| `FUN_0047bd90` | Set one named part's pools **and re-derive the whole-vehicle pair as the sum over parts** |
| `FUN_0047bf70` | The same for current values only (no maxima) |
| `FUN_004b9bc0` | Take a hit: routing, the steady-hand test, the damage voice lines, the death test |
| `FUN_004b7f80` | The spend: armour first, 1:1 overflow into health |
| `FUN_004b8070` | Spend against the whole-vehicle pools |
| `FUN_004b3bf0` | Spend against one part, then recompute the whole-vehicle pools from the parts |
| `FUN_004b80a0` / `FUN_004b8180` | Set armour / set health directly (repair, cheats); the health one rescales every part pool |
| `FUN_004b3800` | The def-level `injure_anims` driver, keyed on the **whole-vehicle** health fraction |
| `FUN_004b3d70` | The per-part `injure_anims` driver, keyed on **that part's** fraction |
| `FUN_004b1790` | The low-health test that arms the damage-state call (`FUN_004b1690`) |
| `FUN_004b82d0` | Death: plays the def's destroy anim, and everything that follows from being dead |
| `FUN_0048ad20` | The surface-vehicle terrain update, which carries a health drain of its own |
| `FUN_0041c470` | The debug info panel, which is what names these fields |

## The two ledgers

A vehicle instance carries a whole-vehicle pair and, optionally, a list of per-part pools.

| Instance offset | Field |
|---|---|
| `+0x2c4` | armour max |
| `+0x2c8` | armour current |
| `+0x2cc` | health max |
| `+0x2d0` | health current |
| `+0x64` | the airframe def it was built from |
| `+0x9c` / `+0xa0` | begin/end of the per-part array, records of `0x58` bytes |
| `+0x890` | one anim handle per def-level `injure_anims` entry |
| `+0x6d0` | the destroy anim reference, copied from def`+0x158` |

The naming is not inferred: `FUN_0041c470` prints these four as
`Armor: %.1f/%.1f  Health: %.1f/%.1f` (`0061fdfc`), and each part as
`DP: %s Armor: %.1f/%.1f  Health: %.1f/%.1f` (`00620400`) from the part fields below. An object with
no vehicle behind it gets a third format, `Armor: N/A  Health: %.1f/%.1f` (`0061fe44`), read through
two virtuals rather than these offsets: that is the static-destructible case.

A part record (`0x58` bytes, `FUN_00476250` fills it, `FUN_0041c470` prints it):

| Part offset | Field |
|---|---|
| `+0x04` | the model node this zone is |
| `+0x0c` / `+0x10` | the got-hit anim and its live handle |
| `+0x18` / `+0x1c` | the destroy anim and its live handle |
| `+0x24` / `+0x28` | armour max / current |
| `+0x2c` / `+0x30` | health max / current |
| `+0x34` | the zone id a hit is matched against |
| `+0x3c` / `+0x40` | the part's own `injure_anims` list, `0x1c` per entry |
| `+0x4c` | one handle per entry of that list |

## Where the numbers come from at spawn

Four things can write the pools, in this order.

**1. The airframe def.** `FUN_00475820` copies def`+0xb4` into health max and def`+0xb8` into armour
max, unconditionally, and copies the whole `destroyable_parts` array across. `FUN_00476250` then
fills each part's max and current from the def's own record, so a fresh vehicle starts at full on
both ledgers. Because `FUN_00479240` only writes def`+0xb4` when the def actually spells `health`,
a def chain that never spells it leaves whatever the def struct was initialised with (see "Open
threads").

**2. The roster block.** `FUN_0047c210` is the spawn used for an `aiv` entry. It applies
`init_health` (block`+0x28`) **only if greater than zero**, and `armor` (block`+0x2c`) if greater
than or equal to zero, which is exactly the "0.0 means use the airframe default" the roster's own
field list implies. It then hands the four zone pairs (block`+0xdc` onward: nose, tail, then the
literal names `leftwing` and `rightwing`) to `FUN_0047bd90` one at a time.

⚠ **`FUN_0047bd90` would change what "total health" means, and in the shipped data it never runs.**
It sets the named part's max and current, then re-derives the whole-vehicle armour and health, max
and current, as the **plain sum over all parts**. That would make the parts authoritative and the
whole-vehicle pair a derived total. Its first line rejects the call when both arguments are `-1`,
and all 414 shipped roster blocks carry `-1` in all eight zone slots, so the sum-derivation is dead
in this install and the def's own `health` stays authoritative.

**3. The difficulty scale.** Still in `FUN_0047c210`, and only when the spawned vehicle's team
differs from the player's: armour max and health max are multiplied by `1 + k * 0.125`, where `k`
comes from the difficulty query `FUN_00440710` as `-1` on one tier and `+2` on another, with the
middle tier skipping the block entirely. Enemy vehicles are therefore on **0.875x, 1.0x or 1.25x**
their authored pools. Current is re-seeded from max afterwards.

**4. The per-spawn jitter, aircraft only.** At the end of `FUN_00476250`, a vehicle whose name is
not `player`, in single player, and whose `mode` is `jet` (0) or `heli` (1), gets each
of armour max and health max multiplied by a uniform random factor in `[0.95, 1.05]`, current
re-seeded from max, and the same treatment applied to nine other def-derived numbers. **Ships (mode
3), ground vehicles (mode 2) and the `w*` wingman family (mode `wingman`, 4) are excluded**, so no
patrol boat, truck or shipped wingman is ever jittered and no player plane is either. The full
eleven-slot list, the gates and the class table are decoded in
[`flightModel.md`](flightModel.md)'s "The per-spawn jitter"; the whole-vehicle pair here is slots 1
and 2 of it, and **the per-part pools are not touched**. Implemented as
`PlaneStats.WithAiSpawnJitter`, applied at `AiAircraftSpawner.Spawn` (PLAN-ai-flight C26).

## Taking a hit

⚠ *Corrected 2026-08-14: `FUN_004b9bc0` is NOT the entry point — `FUN_004b9b30` wraps it and
loops the unabsorbed leftover back through. The section below stands for one pass; the
correction section further down is the full contract.*

`FUN_004b9bc0` is the per-pass body. Ignoring the special cases it opens with (already dead, hit by
its own shooter, weapon classes that detonate or attach instead of damaging), it does this:

1. It takes a **pair** of damage numbers, armour damage and health damage, not one figure.
2. If the hit reports a zone id and the vehicle has parts, it looks for the part whose `+0x34`
   matches and spends against that part (`FUN_004b3bf0`). Otherwise it spends against the
   whole-vehicle pools (`FUN_004b8070`). Both routes go through the same spend helper.
3. The spend (`FUN_004b7f80`) takes armour first and overflows 1:1: the armour pool absorbs the
   fraction of the armour damage it can cover, and only the remaining fraction of the health damage
   reaches health. Armour covering the hit outright leaves health untouched. This is the ordering
   `CAP-19` observed at the controls, now confirmed in code.
4. **Part damage rewrites the whole-vehicle pools.** After spending on a part, `FUN_004b3bf0` sets
   whole-vehicle health current to `(sum of part health current / sum of part health max) * health
   max`, and armour the same way. The whole-vehicle pair is a running summary of the parts whenever
   parts exist, and it is what everything downstream reads.
5. **Death is one test and one test only: whole-vehicle health current at or below zero**, which
   sends it to `FUN_004b82d0`. *(Corrected 2026-08-14: under (4) every part exhausted is
   SUFFICIENT, not necessary — the wrapper loop's zone-less overflow can drain the pool with
   parts still alive.)*
6. Along the way it prints the AI's steady-hand test (`Absorbed %f damage; steady hand test
   failed. Evading.`, `0062b1e8`) and picks a radio line by comparing combined
   `armour + health` current against 0.7, 0.5 and 0.3 of combined max.

The reverse direction exists too: `FUN_004b8180` sets health directly and then scales **every**
part's current health by the new whole-vehicle fraction, so the two ledgers are kept consistent
from either end.

## Teams and friendly fire

**A round from one aircraft damages another whatever the two teams are.** The team ids gate the
target scan and the radio lines; nothing on the damage path gates the spend. Read 2026-08-14 to
settle `PLAN-instant-action` A2, which asked whether the original refuses friendly damage or only
friendly targeting. It refuses only the targeting.

The path from a struck polygon to a drained pool has no team test in it. The node that owns the
struck geometry carries a handler table at `node + 0xbc`, a list of `{context, callback}` pairs;
`FUN_005abcf0` calls `pair.callback(pair.context, weapon, hitNode, damagePair)` at `0x005abe68`. A
vehicle installs the thunk `0x004b9750`, with itself as the context, into slot 0 of its own node's
table when it spawns (`FUN_0047c210` at `0x0047c7d0`, through `FUN_005abbf0` and `FUN_005abb20`).
The thunk enters `FUN_004b9770`, which resolves the shooter and calls the wrapper
`FUN_004b9b30`, which calls the per-pass body `FUN_004b9bc0`. The spend is reached at `0x004ba0da`
(part-scoped) or `0x004ba102` (whole-vehicle), and the only things between the entry and those two
calls are the body's own early returns: the victim is already dead (`+0x91d`) or already in its
scripted destruct in a network game (`+0x91f`), a no-damage byte is set on the victim (`+0x920`) or
globally outside a network game (`DAT_0064f66e`), the shooter is the victim itself, the damage pair
is zero, or the weapon belongs to a class that detonates, attaches or blinds instead of damaging.
None of them reads a team.

**The team sits at instance `+0x08`,** and the engine has one recurring predicate over it: *the two
teams are equal, or either one is 0*. It is the same test the target scan rejects a candidate on
([aim-assist.md](aim-assist.md)) and the same test the spawn skips the enemy difficulty scale on
(`FUN_0047c210`, whose `local_14[2]` is this field). It appears three times on the damage path, and
each time it picks an announcement rather than an outcome:

- **`0x004b9d5e`–`0x004b9d7d`** computes the predicate over shooter and victim into a stack byte.
  Its **only** read is `0x004ba599`, where it is pushed as argument 2 of `FUN_0042e840`, and
  `FUN_0042e840` never reads argument 2 (its body touches `[EBP+8]`, `+0x10`, `+0x14`, `+0x18`,
  `+0x1c`, `+0x20` and `+0x24`, and nothing at `+0xc`). The value is computed and discarded. The
  branch that reads it is in any case the flash/sonic arm, which zeroes the damage pair for every
  victim regardless of team.
- **`0x004b98e0`** in `FUN_004b9770`: when the **local player's** round damages an aircraft the
  predicate calls friendly, that aircraft speaks combat-voice trigger 28 (`0x004b9961` sets `ECX`
  to the victim), if it owns a line for it. See [`formats/combat-voice.md`](../formats/combat-voice.md).
- **`0x004ba125`** at death: the gloat line is picked from the predicate over shooter and victim,
  then over victim and the local player. When shooter and victim come out friendly, **no line is
  chosen at all** and control falls straight through to `FUN_004b82d0`. A friendly kill is a silent
  kill, not a refused one.

⚠ **Do not read the test at `0x004b9d5e` as a friendly-fire rule.** It computes exactly the value
such a rule would need and hands it to a parameter nothing reads, so the one place the damage path
asks about teams decides nothing. `docs/verification.md` SRC-6 is the general form.

## Damage staging

Two independent drivers, both structured identically, both able to retract what they started.

**Def-level `injure_anims` (`FUN_004b3800`)** runs off `health current / health max` on the
**whole vehicle**, walking the def's list at def`+0x1a8` (entries of `0x1c` bytes, the anim
reference at `+0x18`). When the fraction drops to or below an entry's threshold and that entry has
no anim running, it starts one and stores the handle in the instance's `+0x890` array; when the
fraction rises back above the threshold and a handle is live, it stops the anim and clears the
handle. So the staging is reversible, not a latch, and repairing a vehicle visibly un-stages it.

⚠ **Armour is not in the fraction.** The divide is literally `[inst+0x2d0] / [inst+0x2cc]` — the
health pair only. The armour pair (`+0x2c4` / `+0x2c8`) is never read on this path, so a vehicle
with its armour stripped and its health untouched has crossed no def-level stage. This matters
because the combined armour+health progression is the right scale for the gauge (a hit walks one
down and then the other) and the wrong scale for these thresholds.

The entry layout, `0x1c` bytes:

| Offset | Field |
|---|---|
| `+0x00` | the threshold fraction |
| `+0x08` | the root-node reference, read only when `+0x0c` is set |
| `+0x0c` | non-zero selects a node-scoped context: `FUN_004d8cf0` resolves `+0x08` against the vehicle (falling back to `_C_exref` when `+0x08` is null). Zero plays against the vehicle's own node at inst`+0x0c` |
| `+0x18` | the anim reference, passed to `FUN_004edda0` |

That `+0x0c` field is the optional third element in
[`formats/vehicle.md`](../formats/vehicle.md)'s `[[fraction, animName, rootName], …]`. The player
airframes spell two elements, so their stages play against the vehicle root.

**Per-part `injure_anims` (`FUN_004b3d70`)** is the same loop against `part health current / part
health max` (`[part+0x30] / [part+0x2c]` — **also health-only**, and likewise not the combined
progression), over the part's own list at part`+0x3c` with handles at part`+0x4c`. It takes its
context straight from entry`+0x14` rather than resolving a root. It runs on every part-scoped hit.

Both are called from the spend paths, so a single bullet can move both levels at once. A part
reaching zero also plays that part's destroy anim (part`+0x1c`), which is separate from either list.

⚠ **The two levels are keyed on different pools and are not interchangeable.** The player's
def-level list carries `[0.85, player_fuelleak]` and `[0.10, player_smoketrail]`; both are
whole-vehicle-health stages, so `player_smoketrail` means "the hull is at 10%", not "some zone is at
10%". Driving the def-level list off a per-part fraction fires the whole-plane trail while the hull
is still near full. Decoded 2026-08-15 (`BL-246`); our implementation's three deltas against this
are `BL-384`.

### One start per downward crossing

The handle arrays (inst`+0x890`, part`+0x4c`) are the whole lifetime rule. An entry starts only when
its slot reads zero, and the slot is written with the instance `FUN_004edda0` returns. The slot is
cleared on the **upward** crossing alone (`threshold < fraction`, via `FUN_004ed480`), never when the
anim finishes by itself. So a stage fires exactly once per downward crossing and cannot fire again
until a repair lifts the fraction back over its threshold.

`FUN_004b3e20` (per-part) and `FUN_004b3910` (def-level) are the wipes: each stops every live anim in
its array and zeroes the slots. `FUN_004b8180`, the set-health path behind repairs and cheats, calls
the per-part wipe before re-running `FUN_004b3d70`, so a repair un-stages and then restages from the
new fractions.

Two consequences for a per-part list. Each part carries its own copy of a shared entry, with its own
slot, so an entry authored on all four player zones fires up to four times over a flight, once as
each zone first crosses. And because the fraction is health-only while `FUN_004b7f80` blocks health
damage outright until a part's armour is spent, a fully-armoured part crosses nothing at all: even a
0.99 entry waits for the armour pool. Decoded 2026-08-15 (`BL-297`).

### Where a stage's effects land

Node names in a started anim are bound in `FUN_00521180`, which walks the anim's node tables and
resolves each reference through `FUN_004efa40`. An entry naming an explicit parent is searched under
that parent; everything else goes to `FUN_004efaf0`, which tries, in order, the instance's context
subtree (inst`+0x6c`), the context node (inst`+0x48`), the anim's two local tables (`FUN_004ee7e0`,
`FUN_004ee770`), and finally a global by-name lookup (`FUN_004d0280(7, name)`). The subtree search is
`FUN_004efa70`, a recursive name compare down `+0x56`/`+0x5c`.

⚠ **The context does not redirect a name, it only disambiguates one.** A name that is unique on the
airframe resolves to the same node whichever context started the anim, because a miss in the context
subtree falls through to the global lookup. The `pdpN` panel nodes are unique, so a stage naming
`pdp1` sparks at `pdp1` regardless of which part's list started it. There is no part-relative
retarget on this path. Decoded 2026-08-15 (`BL-297`).

## Death

`FUN_004b82d0` starts the anim reference the def supplied at `+0x158` (instance `+0x6d0`), keeps its
handle at `+0x6d8`, drops the AI's target, marks the instance dead, and picks the kill message.
No vehicle def in `vehicle.zrd` carries a key naming that anim, so the reference is resolved by
name at load: the shipped `zrdr/patrol_boat_destroy.zrd` holds an `ANIMATION_DEFINITION` named
exactly `patrolboat`, and it is that def's sequence (parts thrown clear, `sinker` rolling and
sliding under, `ptboat_slick` fading in) that plays. For the player specifically, `FUN_00476250`
resolves an anim named `player` plus a set of `player_crash_*` variants into the same slot.

## The surface-vehicle drain

`FUN_0048ad20`, the terrain-conform update for ground vehicles and ships, carries a health path of
its own: while one global flag is set it kills the vehicle outright, and while a second is set it
subtracts `0.5 * health max` per second and kills at zero. Both flags are cleared each pass by the
update itself and are set from inside the terrain query, so this reads as a terrain or obstacle
collision penalty. **What sets them is not decoded** and this page does not claim more than that
the drain exists and is expressed as a fraction of max per second.

## What this settles for the patrol boat

The question was which of two shipped numbers is the boat's hit points: `vehicle.zrd`'s
`patrolboat` (`armor 0`, `health 40`, stages at 0.60 and 0.30 firing `ptboat_50damage` and
`ptboat_75damage`) or an anim def's `HEALTH 20` with `ANIM_HEALTH` stages at 12 and 6. They agree on
the stage fractions and differ by exactly 2x on the total, which is what made it look like one
object described twice.

**Both are live, on different boats, and nothing in the executable knows a boat from a water
tower.** There are three shipped defs, not two, and which one an instance runs under is decided
entirely by how that instance was created:

| Def | Binds to | Model |
|---|---|---|
| `vehicle.zrd`'s `patrolboat` | an `aiv` roster entry, spawned as a vehicle | this page: 40 health, 0 armour, `injure_anims` at 0.60/0.30 |
| `C1/zrdr/patrol_boat.zrd`, `NAME ptboat*`, `HEALTH 20`, with a `DAMAGE_SEQUENCE` | placed world nodes matching the wildcard | the destructible model in [`formats/destructibles.md`](../formats/destructibles.md) |
| `zrdr/patrol_boat_destroy.zrd`, `NAME patrolboat`, `HEALTH 20`, no `DAMAGE_SEQUENCE` | the unparented prototype node | neither: it is the vehicle's death animation |

**The AI boat is a vehicle and reads 40.** `health 40` reaches health max through `FUN_00475820`;
`injure_anims` `[[0.6, ptboat_50damage], [0.3, ptboat_75damage]]` is the list `FUN_004b3800` walks
against the whole-vehicle fraction; death is the whole-vehicle test in `FUN_004b9bc0`. 19 `aiv`
blocks name a `patrolboat` (12 in C1/M05, 4 in **C1B/M03**, a mission the earlier survey missed, 2
in C5/M01, 1 in C2/M01), and every one carries `init_health 0.0`, `-1` in all eight zone slots and
`-1` for `armor`, so `FUN_0047c210` overrides nothing. Being a ship it takes the difficulty scale
(**35 / 40 / 50**) but not the aircraft jitter.

**The placed boat is a destructible and reads 20.** C1's refinery has three: `ptboat1`, `ptboat2`
and `ptboat3`, children of `refinery.flt` with real translates (nodes 3015 / 3029 / 3043), each
compiled into `C1/cam_anim` as `health 20.0`, `activation WeaponHit`, carrying the wildcard def's
`DAMAGE_SEQUENCE` (`ANIM_HEALTH 12` -> `sputter_black_smoke_obj`, `ANIM_HEALTH 6` ->
`sputter_fire_smoke_obj`, the generic shared effects rather than the boat-specific pair). These are
the boats visible in C1's freecam chapter, and they are shootable exactly like any other
destructible.

**The prototype is neither.** The `patrolboat` node in each chapter's `gamez` has no parent and an
identity transform (C1 2689, C1B 620, C2 4930, C3 1550, C5 8042; C5's `t_truck` at 7565 likewise),
so it is not in the scene graph and cannot be hit. What is compiled onto it, in exactly the four
missions that have roster boats, is exactly the anim list the **vehicle** def names:
`emit_ptsplash1` and `emit_ptsplash2` (its `start_anims`), `ptboat_50damage` and `ptboat_75damage`
(its `injure_anims`), and `patrolboat`/`healthy` (its death sequence). That correspondence is the
cleanest confirmation that the vehicle path is what consumes them; the `health 20.0` on the death
entry is a field inherited from the source def and nothing reads it.

⚠ **So the answer depends on which boat, and the earlier "the vehicle def governs the AI combatant,
the anim def governs placed scenery" reading was right.** For a scenery-only scope it is 20, from
the destructible; for mission play with a roster it is 40, from the vehicle. C1B's dock boat
(`patrolboat.flt` under `boat_at_dock`, node 669) is a third case: placed, but with no compiled def
of any kind, so it is inert geometry.

## The same question for aircraft

`vehicle.md` recorded that 11 AI defs resolve **both** a whole-vehicle `armor`/`health` pair and
their own `destroyable_parts`, and left it open which one an AI combatant spends. The answer is
both, in a fixed relationship: **the parts are the ledger and the whole-vehicle pair is a running
summary of them**, recomputed as a fraction of max on every part-scoped hit (`FUN_004b3bf0`), while
a hit that names no zone spends the summary directly. Death, the def-level staging and the AI's
damage reactions all read the summary. So a Bloodhawk's `armor 64 / health 64` is not an alternative
to its 4x20/20 zones; it is the scale the zones are expressed in.

The two other airframe-only behaviours are the ones above: enemy aircraft take the same difficulty
scale as the boat, and aircraft alone additionally take the +/-5% per-spawn jitter on both pools
(and on nine other def numbers), which is why two Bloodhawks on the same tier are not identical.

## The A4 decision: what the remake builds on this

M4 item A4 (decided 2026-08-13) asked how the remake represents multi-zone damage across its
three consumers: aircraft, zeppelins, and the static world destructibles. The decision, justified
against this decode:

**The two systems stay two systems, and `DestructibleRegistry` gains no zone concept.** The
original runs two disjoint damage models selected by how an instance was created (the scope note
above), and their semantics share nothing: a destructible is one scalar pool with absolute
`ANIM_HEALTH` thresholds and a one-way death; a vehicle is two ledgers with an armour pool, 1:1
overflow, fraction-keyed reversible staging, and spawn-time scaling. Folding zones into
`Mech3/DestructibleRegistry.cs` would graft the vehicle model onto a registry whose `(def, anchor)`
scalar instances and monotonic `DamageStage` are correct for what they cover. The registry keeps
the static destructibles only; vehicles keep their own damage component.

**(a) Aircraft: `Flight/PlaneDamage.cs` is the vehicle ledger and grows the summary pair.** It
already holds the per-part half of this page's model (four zones nose/tail/left/right, each an
(armour, health) pair, armour first with 1:1 overflow, confirmed by CAP-19). What it lacks is the
decoded whole-vehicle summary: after a part-scoped spend, whole-vehicle current health and armour
are recomputed as the parts' fraction of their summed maxima times the whole-vehicle maxima, and a
hit that names no zone spends against the summary directly. An AI airframe's `armor`/`health` pair
is the scale that summary is expressed in, not a competing pool; player defs resolve none, so the
open thread above (the initialiser) stands. Kill threshold: **whole-vehicle health current at or
below zero** — *corrected 2026-08-14: NOT "every zone exhausted". The wrapper loop
(`FUN_004b9b30`, the correction section above) re-enters the unabsorbed leftover zone-less and
drains the whole pair directly, so the kill can arrive with zones still healthy; and a dead zone
is never struck — the resolver redirects the hit to a surviving zone. The whole pair is a real,
independent pool, not only a running summary.* ⚠ The remake today
kills on any `critical` part reaching zero (`FlightController.cs:823`, `:1957`); no code on the
decoded death path reads that flag (the open thread below), so the current rule is a recorded
divergence. D14 retires it when it lands the damage routing, keeping the flag parsed; if a part
kill is ever observed at the controls of the original, that observation reopens the flag question,
not this decision.

**(b) Zeppelins: an aggregator over per-part scalar pools, not a zoned registry instance.** The
decoded kill check walks the `healthy` node list, counts entries still active, and kills when
`survivors < num_healthy_required` ([`formats/mission-entities.md`](../formats/mission-entities.md);
default 1, clamped to the `healthy` list length; the survivor polarity must be asserted in a test,
because the inverse reading is an immortal zeppelin). Gasbags, engines and cannons are each their
own node with their own pool and destruction anim, which is exactly the registry's existing
`(def, anchor)` scalar shape once the sub-part defs anchor at all. So F18 builds: (1) the
deliberate `NameResolver.Anchors` change that lets zeppelin sub-part defs anchor; (2) pool seeding
from the mission record (`gasbags` hp 80-400, `cannon_health` hp 200) where authored; (3) a
per-zeppelin aggregator component, fed by the mission's `zeppelins.json` record, that counts
surviving `healthy` nodes and owns the kill, plus the engine-loss square root over the `engines`
list. No `(def, anchor, zone)` keying is needed anywhere, because each zone is already its own
instance; the zeppelin-level threshold lives in the aggregator, not the registry.

**(c) Static destructibles: unchanged.** Scalar `HEALTH`, `DAMAGE_SEQUENCE`, death at zero, per
[`formats/destructibles.md`](../formats/destructibles.md). This page's scope note is the reason:
the executable itself never unifies the two models, so a unified remake registry would be an
invention.

What each downstream item consumes:

- **F18** takes (b) whole: the anchor change, the seeding, the aggregator and its survivor
  threshold, and the `WeaponDef.DamagesZeppelin` gate (parsed, never consumed today) as the
  routing flag for what may hurt a gasbag.
- **D14** takes the hit contract from "Taking a hit" *as corrected 2026-08-14*: every hit is a
  pair (armour damage, health damage) plus an optional zone; zone hits spend the part then
  recompute the summary, THE LEFTOVER THEN RE-ENTERS ZONE-LESS and drains the whole pair
  directly; a dead zone redirects to a surviving one. The remake's zone supplier is
  `PlaneDamage.MapStruckPart` standing in for `FUN_004b3950`'s geometric half (untraced); the
  resolver's live-only rule and random-survivor fallback are ported exactly. D14 also retires
  the `critical` kill divergence above.
- **A2's spawned aircraft** seed the ledger exactly as "Where the numbers come from at spawn":
  the `r*` def chain's `destroyable_parts` plus `armor`/`health`, then the roster's `init_health`
  (only if > 0) and `armor` (if >= 0). The eight per-zone roster slots are parsed for index
  alignment and ignored (`-1` on all 414 blocks; the sum-derivation path is dead in this install).
  The difficulty scale (0.875/1.0/1.25) and the aircraft-only per-spawn jitter (uniform 5 %) are
  decoded constants to apply when a difficulty setting exists, not TUNEs.
- **G21** keys its crash choreography off the vehicle death event (the whole-vehicle kill raising
  `FlightController.Downed`), never off `DestructibleRegistry`.

One contradiction with the plan and the design document, recorded rather than smoothed over: the
design's unified damage-zone model (every object divides into critical/non-critical zones and dies
when a threshold count of critical zones dies) is design-era. The shipped engine has **two kill
rules and no shared zone vocabulary**: vehicles die by sum-exhaustion of their zones, zeppelins by
the survivor count, and the design's own aircraft example (1 of {tail, nose, wings} critical) is
refuted by the decoded death path. `plans/PLAN-M4-ai.md`'s "Damage zones" section and its
architecture-constraints bullet proposing `(def, anchor, zone)` plus a threshold counter inside
the registry are superseded by this note.

## Correction (2026-08-14): the take-hit wrapper loop

The 2026-08-13 pass read `FUN_004b9bc0` as the take-hit entry point. It is not: `FUN_004b9b30`
wraps it, and the wrapper LOOPS. An at-the-controls report against the remake (an enemy Fury
absorbing nine HE rockets) prompted the re-read; everything below is instruction-level, same
method as the rest of this page. This section partially corrects "Taking a hit" step 5 and
"The A4 decision" below.

- **`FUN_004b9b30` loops the leftover.** First pass: a caller supplying no part id has the
  struck zone resolved by `FUN_004b3950`, and `FUN_004b9bc0` runs with it. The wrapper then sets
  part id := -1 and calls again while BOTH leftover damage values are still positive and
  whole-vehicle health (`+0x2d0`) is above zero. Every later pass is therefore the zone-less
  route — the whole-vehicle spend `FUN_004b8070`, with NO recompute behind it.
- **The spend writes its leftovers back.** `FUN_004b7f80` takes the damage pair in/out. The
  armour pool absorbs what it can of the armour damage (`FUN_004b7f30` clamps the pool at zero)
  and the unabsorbed armour damage is written back. Armour covering the armour damage outright
  ZEROES the health damage and ends the hit; so does armour standing against a hit that carries
  no armour damage at all. Otherwise the uncovered share of the health damage reaches the health
  pool, and the write-back is the full health magnitude minus what the pool absorbed — both the
  pool's overflow AND the armour-shielded share re-enter the loop, where they meet the whole
  pair.
- **A dead zone is never struck.** `FUN_004b3950` matches the hit geometry only against parts
  with health remaining, and its miss fallback `FUN_004b3b60` picks with `rand()` among the
  first up to three surviving parts. A hit aimed at a dead zone is REDIRECTED to a surviving
  one; only with no survivor does a hit run zone-less from the first pass — and by then the
  recompute has already written whole health to zero.
- **Net behaviour.** The struck zone absorbs what it can; the leftover drains the whole pair,
  gated once per pass by whatever whole armour the last recompute left standing. Concentrated
  one-bearing fire kills because the redirect walks the surviving zones down; a large warhead
  kills through the overflow with zones still healthy. Death stays the single test — whole
  health current at or below zero — but the overflow can reach it with parts alive, so
  "every part exhausted" was sufficient, never necessary.
- **The recompute quirk, kept.** A later part-scoped spend recomputes whole current from the
  parts (`FUN_004b3bf0`) and OVERWRITES earlier zone-less dents — a partial heal. The engine's
  own arithmetic does this; the remake reproduces it rather than fixing it
  (`PlaneDamageTests.ALaterPartSpendOverwritesAnEarlierOverflowDent`).
- **Whole-pair seeding in the remake.** Where the def chain authors `armor`/`health` (the AI
  defs) that pair is the whole maxima; player defs author none, so the remake seeds the pair as
  the sum over parts — `FUN_0047bd90`'s re-derivation is the decoded precedent for
  sum-over-parts as the whole pair. The player-initialiser open thread below stands; this is a
  documented stand-in, not a decode. Measured: player_bhawk seeds 80/80 (4×20/20), the Fury
  90/90 (25+25+20+20 — equal to the AI `fury` def's authored 90/90; the AI `bloodhawk` authors
  64/64 against its parts' 80/80, a difference the remake reaches only when AI spawns read the
  `r*` def chain).

What this landed as: `Flight/PlaneDamage.cs` carries the whole pair, the resolver redirect, the
verbatim spend and the wrapper loop; the `air-to-air` suite pins the one-bearing kill (80 rounds
of `wep_00`) and the rocket kill (a Fury falls to 5 head-on `wep_06` rockets against the
reported 9-rocket sponge); `PlaneDamageTests` pins the arithmetic including the overflow kill
with three zones healthy.

## Open threads

- **Where a player plane's health max comes from.** No player def resolves a whole-vehicle pair, and
  `FUN_00479240` leaves the field alone when the key is absent, so the value is whatever the def
  struct is initialised with. `FUN_004b3bf0`'s recompute multiplies by that max, so it cannot be
  zero in practice. The initialiser was not located.
- **The `critical` flag** on a `destroyable_parts` entry is documented as "the plane is destroyed
  when this part reaches 0 HP", from the flag's name. No code on the death path reads a part flag:
  `FUN_004b82d0` has four callers and none of them is a per-part check, and `FUN_004b3bf0` only
  plays the part's destroy anim. Under the recompute, one zone at zero leaves the summary at 75%.
  Either the flag is consumed somewhere not yet found or the reading is wrong; it is not settled
  here.
- **Who supplies the zone id** a hit is matched against (`FUN_004b9bc0`'s parameter, against part
  `+0x34`) is on the hit-detection side. Partially resolved 2026-08-14: a caller passing -1 has
  it resolved by `FUN_004b3950` (live parts only, random-survivor fallback); that function's
  geometric matchers (`FUN_004b39d0`/`FUN_004b3aa0`) remain untraced.
- **What sets the surface-vehicle drain flags**, as above.
