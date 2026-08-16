# AI weapon employment: when an AI pulls the trigger, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-16, for `BL-395` (our AI never fires
ordnance) and `BL-396` (our gun gates were inferred, not read). Every claim names the function or
address it came from; the data census names the file it counted.

This is the firing half of [`../aiPilot.md`](../aiPilot.md), which decodes what an AI *flies*:
target acquisition, steering, escort, crash avoidance. Nothing here is about steering. The authored
armament side is [`../../formats/vehicle.md`](../../formats/vehicle.md) (`weapons`), the weapon
definitions themselves are [`../../formats/weapons.md`](../../formats/weapons.md).

⚠ **None of this reaches turrets.** The weapon-slot vector is a vehicle field, and the shot routine
`FUN_004b6820` has exactly three callers, all vehicle paths: the world tick over the vehicle list
(`FUN_004897c0`), the player's trigger (`FUN_004881e0`) and the scripted fire command
(`FUN_004706f0`). Turrets are their own system (`D:\zipper\Crimson\turret.cpp`, classes `Turret`
and `TurretRate : TargetRate`) with their own entry into the projectile spawner `FUN_005aef40` and
their own authored rate model, `WEAPON.FIRE_RATE` redrawn `uniform(min, max)` after every shot
([`../../formats/turrets.md`](../../formats/turrets.md)). Established from call sites and the class
layout, not from reading the turret update end to end.

## Contents

- [The weapon list, and who builds it](#the-weapon-list-and-who-builds-it)
- [The `weapons` 5-tuple, decoded](#the-weapons-5-tuple-decoded)
- [The trigger routine](#the-trigger-routine)
- [The fire routine, and the aim gate](#the-fire-routine-and-the-aim-gate)
- [`gun_pitch`/`gun_yaw` clamp the mount, they do not gate the shot](#gun_pitchgun_yaw-clamp-the-mount-they-do-not-gate-the-shot)
- [The specials](#the-specials)
- [Our gun gates against this path](#our-gun-gates-against-this-path)
- [What the shipped data amounts to](#what-the-shipped-data-amounts-to)
- [Function map](#function-map)
- [Open](#open)

## The weapon list, and who builds it

Every vehicle carries one `std::vector` of 0x30-byte weapon slots at `+0x268`–`+0x270`
(begin/end/capacity), constructed empty (`FUN_004aff80` at `0x004b0072`). Guns and ordnance are the
same list; nothing separates them but a flag on the weapon def.

| Slot offset | Field |
|---|---|
| `+0x00` | weapon def pointer (flags live at def `+0x210`) |
| `+0x04` | numeric weapon id, parsed out of the `wep_NN` name (`0x004b5a1f`) |
| `+0x08` | rounds carried; decremented per shot, and the AI's only ammo bookkeeping |
| `+0x10` | next-ready timestamp, compared against the world clock `DAT_0071c470` |
| `+0x14` | refire interval, seconds; `+0x10` is set to now + this on every shot |
| `+0x18` | **minimum** engagement range, **squared** |
| `+0x1c` | **maximum** engagement range, **squared** |
| `+0x20`, `+0x24` | fire sound + its handle |
| `+0x28` | which gun mount fires it, an index into the per-mount array at `+0x2a0` |
| `+0x2c` | the trigger byte: set by the fire routine, consumed and cleared by the shot routine |

Two builders exist and they do not agree:

- **`FUN_004b59b0`** fills the list from the vehicle def's `weapons` block. It is called only from
  the def parser `FUN_00479240`, so **this is the path every AI aircraft, boat and truck takes**.
  Every field comes from the authored 5-tuple.
- **`FUN_00444300`** builds one slot from a weapon id with **hardcoded** numbers: 1 m minimum,
  900 m maximum, a 0.5 s gun interval (1.0 s in one caller branch) and a **20 s ordnance interval**.
  Its only caller is `FUN_00443de0`, the campaign loadout build for the player and `wingman_1`.

So the player and their wingman fly engine constants; everyone else flies authored data.

## The `weapons` 5-tuple, decoded

`FUN_004b59b0` reads the tuple's elements at reader offsets `+0x14`, `+0x1c`, `+0x24`, `+0x2c` and
writes them to slot `+0x08`, `+0x14`, `+0x18`, `+0x1c`, squaring the last two
(`0x004b5a4b`–`0x004b5a78`). The tuple is therefore:

```
[weapon_id, rounds_carried, refire_interval_seconds, min_range_metres, max_range_metres]
```

This settles the two fields [`../../formats/vehicle.md`](../../formats/vehicle.md) recorded as
unread. The reading is confirmed by the gun rows, which are only sensible one way round: every AI
gun entry is `0.05`–`0.08` s between rounds over `1`–`900` m, which is a machine gun; read the other
way it would be a weapon with a 1-metre minimum range firing every 900 seconds.

⚠ **Five base defs author the interval and the minimum range transposed against every other def.**
`firebrand`, `bloodhawk`, `brigand`, `fury` and `autogyro` author `200, 30` where all 25 militia
variants author `30, 200`, a 200-second refire at a 30 m minimum against a 30-second refire at a
200 m minimum. The reader does not care which looks intentional; it takes element 3 as the interval
in every case. Treat it as shipped data, not as a bug to correct.

## The trigger routine

`FUN_0041f420` is the AI's fire decision. It does not spawn anything: it selects a weapon and sets
trigger bytes. It is called from all three AI behaviours (the per-frame update `FUN_0041c270`
(`0x0041c348`), pursue `FUN_0041d9f0` (`0x0041e5a1`) and the escort law `FUN_0041e760`
(`0x0041eff4`)), and **all three pass `(1, 1)`**, the two class-enable arguments, so neither guns
nor ordnance are ever suppressed at the call site.

Gates, in the order the function applies them:

1. **Quick draw.** Only when the shooter's own `mode` is `jet` or `wingman` *and* the target is a
   vehicle of the same two classes: `|dot(unit separation, target forward)|` must be at or above
   the shooter's `+0x960`. Below it the function returns immediately and nothing fires. That is the
   `Cannot shoot at target, quick draw...` debug string at `0x006204b8`. `+0x960` is
   `cos(quick_draw_angle)` at the pilot's rating, built at spawn (`0x0047cf68`).
2. **Target validity.** The target's virtual at vtable `+0x14` aborts the whole walk.
3. Then, per weapon slot, in list order:
   - rounds carried above zero;
   - **class**: a `CANNON` weapon (def flag bit `0x40`) is considered only if no cannon has been
     considered yet this pass (**one gun per pass, the first in the list**). A non-cannon (ordnance)
     weapon is considered only when its slot cooldown `+0x10` has expired;
   - **zeppelin match**: a `DAMAGES_ZEPPELIN` weapon (bit `0x1000`) is offered **only** against a
     gasbag, and a weapon without that bit **only** against a non-gasbag. The gasbag test is the
     target's vtable `+0x1c`, the same virtual the acquisition's gasbag admission uses
     ([`../aiPilot.md`](../aiPilot.md#the-gasbag-gate-is-ordnance-checked-at-admission));
   - **range window**: squared separation inside `[+0x18, +0x1c]`.
4. A slot that passes becomes the vehicle's **selected weapon** (`FUN_004b20d0` writes `+0x950`,
   the same field the player's HUD weapon readout reads). Ordnance always takes the selection;
   a cannon takes it only if no ordnance has taken it this pass.
5. If the slot's cooldown has also expired, its trigger byte `+0x2c` is set, and the pilot voice
   line `0x0e` plays when the target is the player (`FUN_004afd00`).

⚠ **The gun's cooldown is checked at step 5, the ordnance's at step 3.** A gun off cooldown is still
selected and still drives the aim. An ordnance weapon off cooldown is invisible to the whole
routine: it cannot select, and it cannot suppress the gun's selection.

## The fire routine, and the aim gate

`FUN_004b6820` walks the same list, acts on set trigger bytes, and is where the shot actually leaves.
For anything that is not the player it applies an **aim-quality gate** against the mount's own
aim quality (per-mount field `+0xa4`, written by the mount aim update `FUN_004b7670`, below):

| Weapon class | Required aim | Half-angle |
|---|---|---|
| ordnance (no `CANNON` bit) | `≥ 0.9962` | 5° |
| gun (`CANNON`) | `≥ 0.9848` | 10° |

Miss the gate and the trigger byte is cleared without firing. The zeppelin match is re-checked here
(`0x004b6935`), so it holds at both ends.

Then, still before the shot:

- **Ordnance carries a vehicle-wide lockout.** Slot `+0x14` is written both to the slot's own
  `+0x10` and, for ordnance only, to the vehicle's `+0xec`. While `+0xec` is in the future **no
  ordnance of any kind fires**, so a plane carrying two rocket types cannot alternate them.
- **Ordnance is rolled for.** For an AI (never the player) in `mode` `jet` or `wingman`, a uniform
  draw must come in at or under the vehicle's `+0x964`, which is `quick_draw_chance` at the pilot's
  rating (`0x0047cf8b`, interpolated `(max − min) × rating ÷ 9 + min` like every other skill).
  Fail it and the launch is skipped entirely (`0x004b6b59`). **This is `quick_draw_chance`'s only
  consumer**, the per-launch ordnance dice, not a gun modifier.
- Guns are subject to neither.

The lead solution both classes consume is `FUN_0041afe0`, per mount, using the weapon def's speed
(`+0x2c`) and drop (`+0x38`): a weapon authoring a non-zero drop gets a ballistic solve with the
travel-time drop added, everything else a straight-line intercept. It writes the desired direction
to mount `+0x78` and a has-solution flag to `+0x84`; with no solution the mount is pointed at the
vehicle's own reversed forward axis instead.

## `gun_pitch`/`gun_yaw` clamp the mount, they do not gate the shot

The aim quality the gate reads is produced by `FUN_004b7670`, the per-mount aim update run from the
world tick over the vehicle list (`FUN_004897c0`), and it is where the airframe's authored gun
limits are spent.

`gun_pitch` and `gun_yaw` are parsed as min/max pairs by `FUN_00479240` (`0x004798c0`, `0x004798fa`),
each multiplied by the degrees-to-radians double at `0x006040e8` and stored to vehicle def `+0x20`,
`+0x24` (pitch) and `+0x28`, `+0x2c` (yaw). `FUN_00476250` copies the four values onto the gun
mount's `+0x94`, `+0x98`, `+0x9c`, `+0xa0` at spawn. Nothing else reads them.

`FUN_004b7670` rotates the desired lead direction into the vehicle frame and then, **for each axis
whose min differs from its max**, decomposes to pitch `atan2(y, sqrt(x^2 + z^2))` and yaw
`atan2(-x, -z)`, clamps the angle into its band and rebuilds a direction from the clamped pair. That
becomes the mount's actual aim (`+0x48`–`+0x50`). An axis authoring min equal to max is not clamped
at all. Then, at `0x004b78c9`:

```
mount +0xa4 = dot(actual mount aim, desired lead direction)
```

So a lead outside the limits does not forbid the shot; it lowers `+0xa4` by the angle the clamp had
to give away, and the shot survives while that residual stays inside the class threshold above. With
the `[-11, 11]` every AI aircraft authors, a lead 20 degrees off the nose clamps to 11, leaves a
9-degree residual, and `cos 9 = 0.9877` clears the gun's `0.9848`. The employable cone is therefore
about the authored limit plus 10 degrees per axis, not the authored limit.

An animated mount (a node in `+0x34`/`+0x38`) slews toward the clamped direction through
`FUN_00460840` rather than snapping to it, so its `+0xa4` also carries however far the mount still
has to travel. A fixed forward gun has no node and reaches the clamped direction the same frame.

## The specials

Three shipped ordnance types are not "launch at the target ahead", and the engine handles them in
the same two routines:

- **`REAR` (def flag `0x20000`, `0x004ba7dd`; `wep_13`, the smoke screen).** `FUN_004b6820` negates
  the mount's aim value and requires the vehicle's *being-pursued* byte `+0xba` to be set. A
  rear-firing weapon is used only while something is on the AI's tail, and it is aimed at what is
  behind.
- **`FLASH` (`wep_09`, `wep_15`) and `TANGLER` (`wep_12`).** These take the ordinary ordnance path:
  they carry no `DAMAGES_ZEPPELIN` bit, so they are used against aircraft, under the same 5° aim
  gate, the same lockout and the same `quick_draw_chance` roll.
- **`TORPEDO`/`TARGETABLE` (`wep_14`).** It carries `DAMAGES_ZEPPELIN`, so by the zeppelin match it
  is fired **only** at a gasbag, never at an aircraft, despite the Black Hat Warhawk carrying eight
  of them with the shortest refire in the game.

## Our gun gates against this path

`AiGunner.Solve` applies three gates. Read against `FUN_0041f420` and `FUN_004b6820` they stand as
follows; `BL-396` carries the fixes.

| Our gate | The original | Verdict |
|---|---|---|
| the round must reach the intercept inside the weapon's `RANGE` | squared separation inside the slot's `[+0x18, +0x1c]` window, 1 to 900 m on every AI gun | wrong quantity, and we have no minimum-range floor |
| `[-11, 11]` degrees of yaw/pitch off the airframe's nose, a hard fire gate | the same numbers clamp the mount's aim; the gate is the 10-degree residual that survives the clamp | wrong mechanism, and our cone is about half the employable one |
| the quick-draw cone, applied every tick against any target | the same test and the same `cos(quick_draw_angle)`, but only when the shooter's mode is jet or wingman **and** the target is a vehicle of those same classes | formula right, scope wrong |

Two conditions on the aim gate have no counterpart in `AiGunner` at all: it is skipped for the
player and for any vehicle carrying the `+0xf8` byte, and a `REAR` weapon negates the mount's aim
value and additionally requires the being-pursued byte `+0xba`.

## What the shipped data amounts to

Census of all 39 `weapons` blocks in `extracted/zrdr/vehicle.zrd.json`
(`analysis/ai-ordnance-census/`; the `player_airplane` block is the 39-id buyable catalogue, not a
loadout, and is excluded):

- Every AI aircraft def carries exactly one gun (`wep_00`/`130`/`140`, 8000–9000 rounds, 0.05–0.08 s,
  1–900 m) and, except `devastator`, `bswingman` and `wingman`, one or two ordnance entries.
- Ordnance counts are 2–8 rounds. Refire is 30 s on the militia variants, 200 s on the five base
  defs, and 5 s for the Black Hat Warhawk's torpedo. Minimum range is 200 m (30 m on those base
  defs, 350 m for the torpedo); maximum is 800 m throughout.
- Ten distinct ordnance ids are flown: `wep_04`–`09`, `12`–`15`. Six militias carry a `wep_05` AP
  rocket, four a `wep_06` HE rocket, five the `wep_12` choker; the flash, flare, smoke and torpedo
  are one or two militias each.
- `patrolboat` and `t_truck` carry a single `wep_29` at 0.3 s over 1–500 m, and no ordnance.

Multiply it out and an ordinary militia fighter launches at most one rocket per 30 s, only inside
200–800 m, only with the nose within 5° of the lead point, and only when a `quick_draw_chance` roll
passes (0.05 at rating 1, 0.44 at rating 9 against the shipped table). Rare by construction, and
rarer still for the 89 mook blocks whose only authored skill is `dead_eye 1`.

## Function map

| Address | What |
|---|---|
| `FUN_0041f420` | the AI fire decision: gates, weapon selection, sets trigger bytes |
| `FUN_004b6820` | the shot routine: aim gate, ordnance lockout and dice, spawns the round |
| `FUN_004b59b0` | builds a vehicle's weapon slots from the def's `weapons` 5-tuples |
| `FUN_00444300` | builds one slot with hardcoded ranges/intervals, player and `wingman_1` only |
| `FUN_00443de0` | the campaign loadout build that calls it |
| `FUN_004b20d0` | selects the current weapon (`+0x950`); refreshes the HUD when it is the player |
| `FUN_0041afe0` | the per-mount lead solver: writes the desired direction `+0x78` and the has-solution flag `+0x84` |
| `FUN_004b7670` | the per-mount aim update: clamps to `gun_pitch`/`gun_yaw` and writes the aim quality `+0xa4` |
| `FUN_00476250` | the spawn that copies the def's gun limits onto each mount |
| `FUN_004897c0` | the world tick over the vehicle list that drives the mount update |
| `FUN_004b2080` | weapon-slot lookup by numeric id |
| `FUN_004442a0` | clears a vehicle's weapon list |
| `FUN_004ba6f0` | the weapon-def flag parser: `CANNON` `0x40`, `DAMAGES_ZEPPELIN` `0x1000`, `REAR` `0x20000` |
| `FUN_004735b0` | loads `ai_skill_parameters`, including the `quick_draw_chance` pair at `DAT_0071c4b8` |
| `FUN_0047c210` | the roster spawn that interpolates the skills onto the vehicle |

## Open

- `FUN_00440ad0` (a read of `DAT_0064f750`) appears in the ordnance dice as an override that lets a
  failed roll fire anyway, and again in the shot routine's muzzle-position branch. What the global
  means is unread; a network or demo-playback mode is the likely candidate.
- Slot `+0x0c` is never written by either builder and never read. Padding or a dead field.
- Why the trigger routine considers only the first cannon in the list is unread. No shipped AI def
  carries two guns, so nothing distinguishes "deliberate" from "never exercised".
