# vehicle.json — aircraft definitions

Part of the [format documentation](README.md). Validated against this install's zrdr
extraction (mech3ax v0.6.1), decoded across Milestone-2 flight work and Run-2 item 10
(2026-07-19). One reader file, shared by every mission scope; the root list alternates
`defName, [properties…]`.

## At a glance

This page is the current reference for its documented format family.


## Contents

- [Def structure & inheritance](#def-structure-inheritance)
- [Units, dynamics & engines](#units-dynamics-engines)
- [player.json � player-global blocks](#playerjson-player-global-blocks)
- [destroyable_parts (Run-2 item 10)](#destroyableparts-run-2-item-10)
- [Def-level injure_anims](#def-level-injureanims)
- [collision — 6 probe points](#collision-�-6-probe-points)
- [Effect emitters](#effect-emitters)
- [Weapons, damage & AI keys](#weapons-damage-ai-keys)
## At a glance

This page is the current reference for its documented format family.



## Contents

- [Def structure & inheritance](#def-structure-inheritance)
- [Units, dynamics & engines](#units-dynamics-engines)
- [player.json � player-global blocks](#playerjson-player-global-blocks)
- [destroyable_parts (Run-2 item 10)](#destroyableparts-run-2-item-10)
- [Def-level injure_anims](#def-level-injureanims)
- [collision — 6 probe points](#collision-�-6-probe-points)
- [Effect emitters](#effect-emitters)
- [Weapons, damage & AI keys](#weapons-damage-ai-keys)
## Contents

- [Def structure & inheritance](#def-structure-inheritance)
- [Units, dynamics & engines](#units-dynamics-engines)
- [Player global blocks](vehicle/player-globals.md)
- [destroyable_parts (Run-2 item 10)](#destroyableparts-run-2-item-10)
- [Def-level injure_anims](#def-level-injureanims)
- [collision — 6 probe points](#collision-�-6-probe-points)
- [Effect emitters](#effect-emitters)
- [Weapons, damage & AI keys](#weapons-damage-ai-keys)
## Def structure & inheritance

Each def's property list is an alternating key/value-list dict. `kind_of` names the parent
def; properties resolve **nearest-first** through the chain (e.g. `pbloodhawk` →
`player_airplane` → `basic_airplane`). Player planes are the defs whose chain contains
`player_airplane`; `nodename` names the GameZ model root (`player_bhawk`). AI variants
(wingmen, pirates) are separate defs with the same shape but different numbers.

Keys the remake consumes (see `src/Flight/PlaneStats.cs`):

| Key | Meaning |
|---|---|
| `kind_of` | parent def (inheritance chain) |
| `nodename` | planes.zbd model root node |
| `engine` | engines.json row id → power factor |
| `engine_sound` / `cockpit_engine_sound` | sound-def names (SETS in sounds.json) — only `cockpit_engine_sound` unconsumed (needs a cockpit view) |
| `damaged_engine_sound` | `[[soundName, f0, f1]]` — one shared `basic_airplane` entry (`snd_damagedengine`, 0.0, 1.0) covers every plane; `f0`/`f1` are undecoded and read as a fade window over accumulated damage fraction (below) |
| `dynamics` | nested dict: `pitch_torque`, `roll_torque`, `rudder_torque`, `return_rate`, `ang_momentum_damp`, `rec_moments_inertia` (xyz), `fd_speed` (m/s), `drag_factor`, `veh_weight`, `ref_area`. The parser also accepts `level_off_rate`, which **no shipped def authors** — see below |
| `spin_props_anim` / `stop_props_anim` | prop-disc anim names (plane_props.json) |
| `start_anims` | anims run at spawn (`wing_lights_blink`, `reset_bulletholes`) |
| `injure_anims` | def-level damage thresholds (below) |
| `destroyable_parts` | the damage model (below) |
| `collision` | 6 collision probe points (below) |
| `bullethole_anims`, `weapons`, `turrets`, `cannon_jam`, `armor`/`health`, AI tuning | only `turrets` consumed (M4 C9a) — [Weapons, damage & AI keys](#weapons-damage--ai-keys) below |

## Units, dynamics & engines

Units are meters/seconds: `fd_speed` 135 m/s ≈ 302 mph matches the Bloodhawk's published
top speed; `flight_ceiling` 2500 m. `player.json` holds player-global values —
`nom_gravity` = 20 m/s² (an arcade 2 g) — plus the sound curve blocks
([sounds.md](sounds.md)).

The `dynamics` block: `rec_moments_inertia` is the *reciprocal* inertia per axis
(x = pitch, y = yaw, z = roll); steady-state rotation rate = torque · recInertia /
`ang_momentum_damp` (Bloodhawk roll ≈ 1.65 rad/s). `return_rate` is extra centering
applied when the stick is released.

⚠ `fd_speed` was documented here as "the full-throttle level-speed equilibrium". **Treat that as
unconfirmed.** The original's own name for the field is `FakeDynSpeed`, its tuner *measures*
`TopSpeed` separately from it, and at runtime it is read as a normalising reference speed
(`speed/fd_speed` fractions, an AI target speed, a clamp) rather than solved for. Working the
decoded drag polar backwards from each airframe's `fd_speed` also fails to close — the Balmoral
misses by 60 %. See [org/flightModel.md](../org/flightModel.md#thrustfactor-is-the-engines-power-factor--resolved);
the flight-model plan's B12/B13 own the question.

**The 13th `dynamics` field: `ThrustFactor` is not authored — it is engine power.** The shipped
Dynamics tuner names thirteen per-plane values where only twelve are authored keys. The extra one,
`ThrustFactor`, has **no parser token at all** (the literal appears exactly once in `crimson.exe`,
inside the tuner's CSV header) and **no shipped def authors a thrust-like key**: all 24 `dynamics`
blocks in this install author the same ten keys. The slot is filled at load from `engines.json`
via the def's `engine` property, and the hangar's engine-swap writes it directly. Confirmed both
in the binary and by data: ranking the eleven player airframes by
`power / (drag_factor · C_D)` reproduces their `fd_speed` order exactly (Spearman +1.000), which a
uniform thrust factor does not. `level_off_rate` is the mirror case — a token the parser accepts
that no def uses.

`engines.json` is a flat list of rows `[id, name, power]`; a plane def's `engine`
property picks its stock engine by id. ⚠ **Every player airframe's stock engine is its Lvl-2 row**
(ids 11, 14, 17, 20, 23, 26, 29, 32, 35, 38, 41 — Bloodhawk: 11 = Lvl-2, power 0.62), never Lvl-1;
solving a constant from a Lvl-1 row inflates it by ~30 %. `power` is the plane's `ThrustFactor`,
and the original applies it as `Thrust = power · ref_area · thrustAvailable(Mach) · throttle` —
scaled by **reference area**, not divided by weight.

## player.json � player-global blocks

See [player global blocks](vehicle/player-globals.md) for the player-global reader reference.

## destroyable_parts (Run-2 item 10)

A list of part entries:

```
[name, hp, hp, flags…,
 "got_hit_anim", [animName, rootName],
 "injure_anims", [[fraction, animName, rootName], …]]
```

- `name`: `nose` / `tail` / `leftwing` / `rightwing` for every player plane.
- **The pair is (hit points, armor)** — `[1]` is the zone's hit points, `[2]` its **armor pool**,
  spent first. Settled against the original's armory; see [below](#the-hp-pair-armor--hit-points).
- **The two values are equal in every entry** — all 88 parts across the 22 defs that carry
  `destroyable_parts` (11 player `p*` + 11 AI `r*`), measured; values 15/20/25/30/35/40. Equal
  because armor is **purchasable** and these are the *stock* allocations, not because the number is
  duplicated. `PlaneStats` reads both values (`DestroyablePart.MaxHp`/`MaxArmor`); the two-pool
  `PlaneDamage.Apply(part, healthDamage, armorDamage)` — armour first, 1:1 overflow — landed
  2026-08-04 (`PLAN-armour-layer`).
  *(An earlier version of this page said AI variants differ 25/20 — that is wrong; nothing in
  this install has an unequal pair.)*
- Flags: `critical` — the plane is destroyed when this part reaches 0 HP (all four player
  parts carry it); `engine` — engine damage/power loss on that part.
  ⚠ **The `critical` reading is from the flag's name and the 2026-08-13 executable decode does not
  support it** ([`org/vehicleDamage.md`](../org/vehicleDamage.md)): the death path tests only
  whole-vehicle health, no code on it reads a part flag, and one zone at zero leaves the
  whole-vehicle summary at 75 %. Either the flag is consumed somewhere not yet found, or the
  reading is wrong. Do not build a kill rule on it without settling that first. Not tail-only: it sits on
  the tail for `pbloodhawk`/`pdevastator` but on the nose for `pautogyro`/`pbrigand`/`pfury`/
  `ppeacemaker`/`pbalmoral`/`pwarhawk` and on **both wings** for
  `pfirebrand`/`pavenger`/`pkestrel` — it
  marks the part the engine(s) physically live in. Handling penalties are unmodelled **by
  design**, not deferred: the original design states damage does not degrade an aircraft's
  performance, a plane on its last legs keeping full performance and lethality. The shipped
  flag may still drive something (sound, effects) the design text does not cover.
- `injure_anims`: **descending HP fractions**; when the part's HP fraction crosses one,
  the named anim runs. Two families interleave:
  - `<part>_damage_green` (0.72) / `_yellow` (0.46) / `_red` (0.20) — the cockpit
    damage-indicator texture cycle (unwired until a cockpit exists). The retail manual describes
    this indicator (the "Crispen Mark V") as colouring each of nose/tail/left/right wing over the
    **combined** progression of both pools: yellow = up to half the zone's armor gone, orange =
    the rest of the armor plus the first quarter of the airframe, red = beyond that. With armor
    equal to hp at stock those bands break at 0.75 and 0.375 of the combined pool, and
    `_damage_green` firing at **0.72** is the expected one-state name lag (see the standing rule in
    `backlog.md`). ⚠ **A reading, not a decode** — `_damage_yellow` at 0.46 sits mid-band rather
    than on 0.375, so the correspondence is suggestive and does not pin the mapping down.
    Source: Crimson Skies PC manual, damage-indicator section —
    <https://manualmachine.com/gamespc/crimsonskies/1119420-user-manual/>.
  - `<part>_damage_effects` (0.99) — **not part of that cycle, despite the neighbouring
    thresholds: this is the per-impact spark burst.** All four (`nose`/`tail`/`leftwing`/
    `rightwing`) are one-event shims calling `random_gun_impact` (anim root `player`) with no
    parameters, so the part identity is discarded by the data itself. `random_gun_impact` is an
    IF/ELSEIF `RANDOM_WEIGHT 0.4` / `0.4` pair choosing `yellow_sparks_follow WITH_NODE pdp1` or
    `pdp2`, **followed by an unconditional third call at `pdp4`** — so a hit sparks one panel or
    two, never none. `yellow_sparks_follow` (root `yellow_spark_02`) emits `trailpuffer2` +
    `chippuffer1` at its INPUT_NODE (the chosen panel) under a 50/50 pick between two
    `snd_ricochet1–4` sequences. ⚠ **Only `player_pfighter` ships the 0.99 entries** — measured
    install-wide, exactly 4 occurrences, all in the def whose `nodename` is `player_pfighter`
    (`title MSG_VEH_DEVASTATOR`), while each of the 11 planes spells its own `got_hit_anim`.
    Inheritance does not spread it: all 22 defs carrying `destroyable_parts` (11 `player_*`
    flyables + 11 lowercase AI variants) are `kind_of` a base that carries no parts list of its
    own. The other 10 aircraft have no per-impact spark at all.
    At 0.99 it fires on the *first scratch*, which is authored, not a threshold to retune.

    ⚠ **`random_gun_impact`'s real home is `weapons.json`, not here — read this entry as a probable
    authoring leftover (hypothesis, 2026-08-01).** It is the `player` **IMPACT surface animation**
    for `wep_03` (60slug) — "what a bullet does when it hits the player's aircraft"
    ([weapons.md](weapons.md)), the counterpart of the `enemy` and `default`/`buildings` classes.
    That is a general mechanism gated on being shot at, which nothing can do in M3. One plane of
    eleven ALSO firing it off a damage threshold fits a leftover better than a per-aircraft
    feature — but no capture of the original settles it, so it is a reading, not a finding, and the
    entry is shipped data either way. Do not "fix" the other ten planes by adding the entry to them;
    that would be inventing content.
  - `pdpanelN` — flips the exterior torn-skin panel `pdpN` (planes.zbd nodes; the anims
    live in the plane's own reader, e.g. player-1.json). Left wing: pdpanel5 @0.5,
    pdpanel4 @0.3, pdpanel3 @0.15; right wing: pdpanel6 @0.4, pdpanel1 @0.3,
    pdpanel2 @0.15; nose pdpanel7 / tail pdpanel8 @0.15.

The `pdpanelN` anim (`ANIMATION_DEFINITION`, ON_CALL, one shared def per panel in
player-1.json, re-rooted per plane via the injure entry's rootName) sets
`OBJECT_ACTIVE_STATE pdpN ACTIVE` **without** deactivating the healthy `pdpN_h` twin,
and calls effect anims at the panel: `gimmeflakes` debris, `yellow_sparks_follow`,
`small_fireball_follow`, `short_firetrail` / `loop_short_firetrail` — the discrete-puff
fire trail streaming from every damaged panel (clearly visible in
`OriginalScreenshots/Videos/C1 IA1 Crash.mp4`). In fact **no zrdr data ever deactivates an
`_h` node** — yet `player_destruct_reset.json` (`plane_reset`) re-ACTIVEs
`pdp2_h`/`pdp3_h` alongside setting every `pdpN`/`pcdpN` INACTIVE, so the original
engine must hide the healthy skins at damage time by an engine-side rule. Beware: the
`pdpN`↔`pdpN_h` numbering is crossed on three plane models — pair torn↔healthy by mesh
position, not by name (measurements in `gamez.md`, "Player-plane damage states").

### The hp pair: armor + hit points

**The two numbers on a `destroyable_parts` entry are that zone's hit points `[1]` and its armor
pool `[2]`, armor spent first.** Settled 2026-08-03.

**How it was settled.** Every pair in this install is *equal*, so no measurement over the shipped
data can separate (armor, hp) from (hp, hp) or (max, current) — the reading stood as a hypothesis
for that reason. The original's **armory breaks the tie, because it varies armor independently of
health**: its per-zone allocation is in units that are armor points 1:1, and a **stock** airframe
reads the same per-zone numbers the zrdr def carries (a stock Bloodhawk shows ~20 units on each of
its four zones; `pbloodhawk`'s parts are 20/20/20/20). Observed at the controls, 2026-08-03.

**Confirmed end-to-end by `CAP-19`** (observed at the controls, 2026-08-03). Three results:

1. **Armor depletes before health.** The ordering retail string 3372 states and
   [`PLAN-M3-weapons.md`](../plans/PLAN-M3-weapons.md) C23 derives from a dominance argument is now
   *directly observed*, not inferred.
2. **The armory's per-zone cap is 60 units**, uniform across a plane's four zones. (Whether the cap
   varies by airframe is untested — one airframe was read.)
3. **A stripped zone falls far faster** than an armored one — green→red in visibly less time, more
   damage per hit. **Direction only, and deliberately not timed:** a live sortie moves ammo type,
   hit distribution, graze damage and pilot skill at once, so it cannot isolate a time-to-kill
   figure — and does not need to. With armor equal to hp at stock and armor spent first with 1:1
   overflow, the **2× effective pool is entailed by the model**, not a separate quantity to measure.

`CAP-19` is discharged and retired from [`playtest.md`](../../playtest.md).

⚠ **The zrdr number is the *stock* allocation, not a fixed property of the airframe.** A player
buys more. Every pair being equal is a fact about stock loadouts, **not** a licence to fold armor
into hp — see `BL-085`.

Corroborating evidence, all data-confirmed:

1. **The HUD showed two pools.** `messages.json` `MSG_HUD_HEALTH` = `Armor: %1%% Health: %2%%` —
   the shipped in-flight readout has an armor bar *and* a health bar.
2. **Weapons carry both damage figures, and they differ.** Every one of the 46
   `weapons.json` `BALLISTICS` entries with damage carries `ARMOR_DAMAGE` **and**
   `HEALTH_DAMAGE`, and 18 of them differ — the ammo matrix is built out of the split:
   `wep_N1` (dum-dum) is armor-light/health-heavy (`wep_31` 1.5 / 4.5), `wep_N2` (AP) is the
   mirror (`wep_32` 4.5 / 1.5), `wep_N3` (magnesium) is between. A two-pool target is the only
   thing that makes those numbers mean different things.
3. **Crash damage is two-pool too.** `player.json`'s `crash` block is
   `armor_damage_range [50,300]` + `health_damage_range [50,300]` + `bounce_factor`.
4. **Retail shipped a per-zone armor purchase.** `rof/ui_strings.json` id 1039 `IDS_AR_TITLE`
   = "3) ADD ARMOR", ids 1044–1047 = Nose / Tail / Left Wing / Right Wing — exactly the
   `destroyable_parts` names. Id 1155 `IDS_PX_ARMORINFO` prices and weighs armor per unit and
   warns "Left and right wings must be balanced!" (and indeed `leftwing == rightwing` in all 22
   defs); id 1170 `IDS_PX_ARMORUNITS` = "%1!d! units"; id 206 `IDS_PX_SWITCHAIRFRAMES` speaks of
   "the **default** armor, engine, and guns for this new airframe" — a stock allocation exists.
   *(This replaces an earlier appeal to the pre-release design spec, which
   [`playtest.md`](../../playtest.md) flags as unreliable as a class for HUD/damage material.)*
5. **Retail states armour-first depletion outright** — `ui_strings.json` id 3372 (AP: "hardened
   tip designed for shredding and destroying armor. WARNING: AP rounds tend to punch clean through
   unarmored surfaces, inflicting very little damage"), id 3371 (dum-dum: "very useful for
   finishing off aircraft that have already been damaged"), id 3410 (AP rocket: "remove most, if
   not all, of the armor from an aircraft but has no noticeable effect on unarmored surfaces").
   A gate, not a damage reducer. [`PLAN-M3-weapons.md`](../plans/PLAN-M3-weapons.md) C23 derives
   the same ordering from a dominance argument; this is the direct statement.

**Still open: what `ARMOR: Standard (N/T/W)` is — and it is now known *not* to be the cap.** Five of
the eleven airframe blurbs carry a per-zone armor triple (`ui_strings.json` ids 40115 Balmoral
400/400/350, 40116 Bloodhawk 400/300/200, 40118 Fury 400/400/350, 40120 Warhawk 700/500/700, 40122
Autogyro 300/300/200). It is **not** the stock allocation — stock is ~20 — and `CAP-19` has now ruled
out the per-zone-cap reading that stood in its place: the observed cap is **60 units, uniform across
a plane's four zones**, while every blurb triple is both far larger and *unequal* across zones.
Three measured constraints on what it can be: retail-triple ÷ zrdr-part-sum is
13.75 / 16.7 / 12.0 / 21.7 / 16.7 across the five, so **no linear map** relates them; triple ÷ 60 is
ragged for the same reason; and at the armory's observed 4 lbs/unit a **fully** armored airframe is
4 × 60 × 4 = **960 lbs** against a `veh_weight` of 1900 — a real trade-off, where the
blurb-as-cap reading implied 1100 units and 4,400 lbs on a 1900 lb plane. That weight arithmetic was
already one of the two arguments against blurb-as-cap; the measured 60 replaces it with a figure the
weight model can carry. The armory's own constants — per-unit cost and weight, per-zone caps — are
**executable-resident**; `ui_strings.json` ships only the printf templates.

## Def-level injure_anims

```
"injure_anims", [[0.10, "player_smoketrail"], [0.85, "player_fuelleak"]]
```

Whole-plane effects. **The fractions are the whole-vehicle health fraction**, decoded from the
executable 2026-08-13 ([`org/vehicleDamage.md`](../org/vehicleDamage.md)); an earlier reading here
had them as any single part's fraction, on the argument that a total-HP reading could never reach
0.10 if a critical part killed the plane at 75 % total. The decode removes that argument: the
whole-vehicle fraction is itself the parts-weighted total, and nothing on the death path reads the
`critical` flag, so a plane reaches 0.10 by having all four zones nearly gone. Staging is also
**reversible** rather than a latch: the driver stops an anim again if the fraction climbs back above
its threshold. `player_smoketrail` starts the `dense_firetrail` pair (pufftrails.json)
following `prop1`: a black-smoke trail (COLORS ramp: born orange 255,164,90 → near-black
5,5,5) plus a fire trail, both `DISTANCE_INTERVAL` emitters (one puff per N meters of the
node's motion — 1.0 m smoke / 0.25 m fire). `player_fuelleak` runs a `fuel_trail` at a
random pdp panel (unwired).

## collision — 6 probe points

Each player def carries a `collision` list of six xyz points in the plane's local frame
(nose −Z, right +X, meters) — the original's own collision representation, apparently
probe points: nose, right wing, left wing, tail, top, belly. pbloodhawk:

```
(0, 0, −5.68)  nose        (0, −0.20, 4.55)  tail
(+5.71, −0.82, +1.87) right wing   (0, +0.80, −1.46) top (canopy)
(−5.71, −0.82, −1.87) left wing    (0, −1.80, +3.28) belly
```

Note the left/right pair is point-symmetric (both z signs flipped), not mirrored —
probably hand-authored. The remake does **not** use these (its swept boxes are derived
from the actual mesh, item 10a); documented for completeness.

## Effect emitters

The effect emitters these anims call (`short_firetrail`, `dense_firetrail`,
`large_fireball`, …) are `PUFFER_STATE` definitions — full schema in
[effects.md](effects.md).

## Weapons, damage & AI keys

The airframe half of the combat data — of which only `turrets` is consumed by the remake so
far (M4 C9a). Player defs carry `weapons` (as a catalogue), `cannon_jam`, `turrets` and
`bullethole_anims`; the `armor`/`health` pair and the AI-tuning keys live only on the AI
variant defs.

**`weapons`** — a list of 5-tuples `[weapon_id, count, ?, ?, range]`, ids into
[weapons.md](weapons.md). On `player_airplane` it is a **capability catalogue, not a
loadout**: all 39 buyable ids at once (`wep_00`–`15`, `25`/`27`/`28`, and the full
`wep_30`–`73` player matrix), each with position-5 range `10000` — a UI sentinel, since the
real per-plane loadout is executable-resident. On an AI def it is the actual armament:
`bloodhawk` = `[["wep_04",4,…,800],["wep_07",2,…,800],["wep_00",9000,…,900]]` — a carried
count, two undecoded factors, then an **engagement range** in metres (800–900 for AI
fighters, 500 for the boat/truck). Positions 3–4 are inferred, not confirmed.

**`cannon_jam`** (`player_airplane`) — `heat_safe_limit 1000`, `heat_dissipation_rate 50`,
`jam_chance 0.1`; reads as a gun-overheating model paired with `FIRING_HEAT` in
[weapons.md](weapons.md). ⚠ **Dead data: the original executable has no reader for it**
(decoded 2026-08-14). None of `cannon_jam`, `heat_safe_limit`, `heat_dissipation_rate` or
`jam_chance` exists as a string in `crimson.exe`, and the zrdr readers look keys up by string
(`FUN_0057a090(dict, "KEY")`), so no lookup is possible. Sibling keys `bullethole_anims`
(`0x00627ec4`) and `destroyable_parts` (`0x00627d7c`) are present, which is the calibration
that makes the absence meaningful. Not implemented here either, and reproducing it would be
invention rather than restoration.

**`armor` / `health`** — the AI two-pool damage model (fighters `64/64`…`100/100`, always
equal; `patrolboat`/`t_truck` `0/40`, unarmoured soft targets). Carried by the 12 base aircraft
defs plus the boat and truck — 15 in all. `PlaneStats` does not read either. Armour is spent
before health, the same ordering as the per-part pools.

⚠ **The whole-vehicle pair and the per-part pools are not alternatives — 11 defs resolve both.**
An `r*` AI variant chains to its base def (`rbloodhawk → bloodhawk → basic_airplane`), so it
inherits `armor 64` *and* carries its own 4×20/20 `destroyable_parts`. No **player** def resolves
a whole-vehicle pair at all (`pbloodhawk → player_airplane → basic_airplane` carries none in the
chain), so for player planes the per-part pools are the whole model.

**A vehicle spends both, in a fixed relationship: the per-part pools are the ledger and the
whole-vehicle pair is a running summary of them.** Decoded from the executable 2026-08-13, full
write-up in [`org/vehicleDamage.md`](../org/vehicleDamage.md). A weapon hit carries two damage
numbers (armour and health, not one figure) and, sometimes, a zone id. When it names a zone, the
damage is spent against that zone's pools, armour first with 1:1 overflow into health, and the
whole-vehicle current values are then recomputed as the parts' fraction of their own maxima times
the whole-vehicle maxima. When it names no zone, it is spent against the whole-vehicle pools
directly, through the same armour-first helper. So the `armor 64 / health 64` on an AI Bloodhawk is
not a second, competing pool; it is the scale its four 20/20 zones are expressed in.

Everything downstream reads the summary rather than the parts. **Death is one test: whole-vehicle
health at or below zero.** (Corrected 2026-08-14: the take-hit wrapper loops the unabsorbed
leftover back into the whole pair zone-less, and a dead zone redirects to a surviving one, so the
kill can arrive with zones still healthy — every zone exhausted is sufficient, not necessary;
[`org/vehicleDamage.md`](../org/vehicleDamage.md)'s correction section has the full contract.) The
def-level `injure_anims` stage off the same fraction (see below), as do the AI's damage reactions
and the pilot radio lines. The one shipped datum that would invert this relationship, an `aiv`
block's four per-zone `(armor, health)` pairs, would set the zones directly and then re-derive the
whole-vehicle pair as their plain **sum** rather than a fraction, making the zones authoritative;
all 414 shipped blocks leave those eight slots at `-1`, so that path never runs.

Two spawn-time modifiers scale the authored numbers. An enemy vehicle (one whose team differs from
the player's) has both maxima multiplied by a difficulty factor of **0.875, 1.0 or 1.25**. On top of
that, an **aircraft or autogyro that is not the player** draws a fresh uniform **±5 %** on both
maxima (and on nine other def-derived numbers) every time it spawns, which is why two AI planes of
the same type are never quite identical. Ships and ground vehicles are excluded from the jitter, so
a patrol boat is exactly its authored 40 times the difficulty factor: 35, 40 or 50.

**The patrol boat has two sets of hit points because it is authored as two things, and both are
live** (settled 2026-08-13; the decode is [`org/vehicleDamage.md`](../org/vehicleDamage.md)). A boat
spawned from an `aiv` roster is a **vehicle** and reads the 40 on this page, with the 0.60/0.30
`injure_anims` above it; a boat *placed* in the world is a **destructible** and reads the `HEALTH 20`
of the anim def whose wildcard `NAME` catches it, with that def's own `ANIM_HEALTH` stages. C1 ships
both: three placed `ptboat1`–`3` at the refinery, and 12 roster boats in M05. Which model applies is
decided by how the instance was created and by nothing else; the executable does not know a boat
from a water tower. A third def, the `patrolboat` in `zrdr/patrol_boat_destroy.zrd`, is compiled
onto an unparented prototype node and is neither: it is the vehicle's **death animation**, and its
own `HEALTH` is never read. Same shape for `t_truck`.

**`turrets`** — on 16 defs: the five player turret airframes (`pavenger`, `pbalmoral`,
`pbrigand`, `pfirebrand`, `pkestrel`, both viewpoints), their six AI variants and five `r*`
remote-player variants (`thirdp` only). A viewpoint-keyed list (`firstp`/`thirdp`) of
`[title <MSG_TUR_*>, node <turretNode>]` entries; the Balmoral is the only two-turret plane
(`balmoral_turret0`–`3`). A turret entry carries a title and a node and nothing else — the
gunner's whole behaviour, arcs included, lives in the `ai.zrd` row the title names
([turrets.md](turrets.md)). **Consumed since M4 C9a**: `PlaneStats` parses the block and
`TurretController` drives the `thirdp` rig as a live gunner.

**`gun_pitch` / `gun_yaw` are the AI's forward-gun aiming cone, not a turret arc.** Both keys
appear exactly 12 times, always together, always `[-11, 11]` (degrees), and always on an AI
airframe def — a census settles which:

- **7 of the 12 carriers have no turret at all** (`bswingman`, `bloodhawk`, `fury`, `autogyro`,
  `devastator`, `peacemaker`, `warhawk`), so presence cannot be tracking turrets.
- **60 of the 63 non-player defs resolve the cone** through `kind_of`; the 3 that do not are
  `basic_airplane` (the abstract root) and the two surface vehicles `patrolboat` / `t_truck`.
  So: every AI *aircraft*, turret or not.
- **0 of the 12 player defs carry or inherit it — including all five turret airframes**
  (`pavenger`, `pbalmoral`, `pbrigand`, `pfirebrand`, `pkestrel`). A turret arc would have to be
  on the plane that mounts the turret; this is on the plane that has an AI pilot.

±11° is the AI's fixed-gun firing tolerance. The design's gunnery model backs the reading — an
NPC's Dead Eye statistic sets the radius of a lead sphere it will shoot into.

**`bullethole_anims`** — per player plane, the ON_CALL cockpit-glass hit-decal anims
`bullet1`…`bullet5` (see [anim-definitions.md](anim-definitions.md)).

**`mode`** — the dynamics class, and the one key that decides which AI behaviour an aircraft flies.
Parsed from a string (`FUN_00479240`, `0x0047afe8`): `jet` 0, `heli` 1, `tank` 2, `ship` 3,
`wingman` 4, `plane` 5. Only four defs author it and the rest inherit through `kind_of`:
`basic_airplane` is `jet` (so are all 11 player defs, all 11 base AI aircraft and all 39 militia
variants, including `autogyro`), `patrolboat` and `t_truck` are `ship`, and 12 defs are `wingman`
(`wingman`, `bswingman`, and the eleven Instant Action `w<plane>` defs). Nothing ships `heli`,
`tank` or `plane`. A `wingman` is a full aeroplane on the same integrator as a `jet`; what differs
is that **a `jet` flies a patrol net and a netless `wingman` flies a formation station on its
`primary_target`**. A `wingman` that is given a net is demoted to `jet` at spawn.
[`org/aiPilot.md`](../org/aiPilot.md) has the mechanism, the station offsets and the constants.
`mode_alt` parses to the def struct alongside it (`0.0` on `basic_airplane`) and no consumer of it
was found.

**`preferred_engagement_altitude`** (300.0 on `basic_airplane`, inherited by every aircraft) is the
fallback for the roster's `pref_engage_alt` slot. ⚠ It is a **weight on the evasive-maneuver draw**,
not an altitude order: nothing steers toward it ([`org/aiPilot.md`](../org/aiPilot.md)).

**AI-combatant tuning** (AI variant defs, M4): pilot skill/personality (`dare_devil`,
`dead_eye`, `quick_draw`, `steady_hand`, `sixth_sense`, `natural_touch`, `stun_recovery`,
`talker`, `constitution`, `accentID`) — the same nine-stat vector the mission rosters author
per pilot, decoded in [ai-rosters.md](ai-rosters.md#the-skill-vector), so a def value here is the
airframe-level default a roster entry overrides; flight/behaviour (`mode`/`mode_alt`, `target_bias`,
`struct_bias`, `pursuit_range`, `attack`/`attack_dwell`/`not_pursuit_dwell`, `rates`,
`turns`, `*_damping`, `mass`, `friction`, `chas_*`, `ai_input_*` / `ai_emerg_input_*` limits
and scales, `preferred_engagement_altitude`/`return_range`, `activation` = spawn/aggro
range). The boat and truck add surface-vehicle motion keys (`platform`, `collision_d`,
`a_damping`). Paint keys (`paint_pattern`, `paint_colorN`, `paint_decalN`) set the AI
liveries — see [paint.md](paint.md). A few airframe oddballs round out the set: `fuel`,
`is_autogyro`, `rudder_tol`, `pilot`, `flight_ceiling`, `title`.

**`ai_input_*` / `ai_emerg_input_*`** are the AI control law's per-axis output stage, decoded in
[../org/aiControlLaw.md](../org/aiControlLaw.md): the three `scale` keys multiply the law's roll,
pitch and yaw commands and the three `limit` keys clamp them, with the `emerg` set substituted
during crash recovery. ⚠ **The def struct holds them in roll/pitch/yaw order** (`+0x264`…`+0x278`)
while the roster's twelve slots are in pitch/roll/yaw order; a roster value of `-1.0` falls through
to the def, which is what every shipped roster block does. The shipped defs author only
`ai_input_limit_pitch` (11 defs, 0.79–0.91) and one `ai_input_limit_yaw` (0.79); the rest inherit
down the `kind_of` chain as one six-slot block.

**`rudder_tol`** selects between the law's two steering branches, and **a higher value means MORE
rudder**: clearing the threshold picks the bank branch, so raising it makes banking harder to
reach. **Default 0.2**, at which any target ahead is banked toward and the rudder is reserved for
targets nearly dead astern. Exactly two defs author it, `autogyro` and `balmoral`, both at `1.0`,
which is the ceiling the compared quantity can never exceed: on those two a lateral-dominant aim
error goes on the rudder even when the target is straight ahead. The `autogyro` is class 1 and
never reaches this law, so `balmoral` is the one aeroplane that turns onto a target with rudder
rather than bank.

**Def defaults for the block above** (from the def initialiser, not from any key): scales `3.5`,
limits `1.0`, the emergency set identical, `rudder_tol` `0.2`. The AI speed clamp that sits beside
them is fixed at `0`…`111.76 m/s` (250 mph) for every airframe and has no token at all.

**`mode` is the vehicle class**, and the parser maps it to a small enum the whole object update
dispatches on: **`jet` = 0, `heli` = 1, `tank` = 2, `ship` = 3, `wingman` = 4, `plane` = 5**
(`0x47afc0`–`0x47b081`; classes 0 and 4 fly the aeroplane path, 1 the autogyro path, 2 the ground
path, 3/5 the ship path). Only `basic_airplane` (`jet`), `patrolboat`/`t_truck` (`ship`) and the
eleven `w*`/`wingman`/`bswingman` defs (`wingman`) author it; everything else inherits `jet`.
The class is what gates the per-spawn ±5 % jitter of eleven runtime slots — `fd_speed`,
`ThrustFactor`, `drag_factor`, `pitch_torque`, `roll_torque`, the two `rates` and two `turns`
values, and the whole-vehicle armour/health maxima — which runs on classes 0 and 1 only, for
vehicles not named `player`, outside a network game (docs/org/flightModel.md, "The per-spawn
jitter"; implemented C26). `rates` and `turns` are the surface-driving integrator's acceleration
and steering rates with their clamps: `basic_airplane` authors them (10/42 and 4.6/6.5) and every
aircraft therefore carries them, but the aeroplane arm never reads them.
## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
