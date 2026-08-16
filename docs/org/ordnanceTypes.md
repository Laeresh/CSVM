# What each ordnance type does, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function it came from, so any of
them can be re-checked at source. No decompiler output is reproduced.

**Where the other halves live.** The authored side, meaning which keys a weapon entry carries and
what values this install ships, is [`formats/weapons.md`](../formats/weapons.md). The per-surface hit
effect table is [`weaponImpact.md`](weaponImpact.md). The AI's side of firing them is
[`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md). Our implementation is `WeaponDefs.cs` (which parses
every key below and acts on none of the special ones) and `Projectile.cs`.

**Scope.** This page covers the **flag map**, the **launch-side dispatch** (which types fire at all,
and what they spawn), the **proximity fuse**, and the **hit-side dispatch** (what happens when a
round reaches an aircraft). Guidance and the torpedo's shootable flyout are not decoded.

## The weapon-extension struct

`FUN_004ba6f0` is the weapon-def flag parser. It `calloc`s one 0x38-byte block per weapon and hangs
it off the ZWEP record at `+0x210`; every consumer below reaches the weapon's behaviour through that
pointer. The block's layout, all of it written by that one function:

| Offset | Field |
|---|---|
| `+0x00` | the flags dword (below) |
| `+0x04` | `PRIORITY` |
| `+0x08` | `-cos(CANNON_SPREAD deg)`, the aim assist's acceptance cone ([`aim-assist.md`](aim-assist.md)) |
| `+0x0c` | `CLUSTER_SIZE` |
| `+0x10` | `CALIBER`, defaulting to 0 when the key is absent |
| `+0x14` | `FIRING_HEAT`, defaulting to 0; no consumer reads it |
| `+0x18` | a **shared `TIME` slot**, default 5.0 |
| `+0x1c` | `TANGLER`'s `RADIUS`, default 10.0 |
| `+0x20` | `DETONATION_DOT_PRODUCT` |
| `+0x24` | `LOOPED_SOUND_NAME`, `strncpy`'d to 19 bytes |

**`+0x18` is one slot, written by three different keys.** `BEEPER`'s `TIME`, `SMOKE_SCREEN`'s `TIME`
and `TANGLER`'s `TIME` all land there, each defaulting to 5.0 when its block omits the key. No
shipped weapon carries two of the three, so nothing collides, but they are the same field and a
reader cannot tell which key wrote it except by the flags.

## The flags dword

Every bit `FUN_004ba6f0` sets, in the order the function tests for them:

| Bit | Key |
|---|---|
| `0x08` | `TORPEDO` |
| `0x10` | `ROCKET` |
| `0x20` | `TARGETABLE` |
| `0x40` | `CANNON` |
| `0x200` | `SONIC` |
| `0x400` | `HIGH_EXPLOSIVE` |
| `0x800` | `FLASH` |
| `0x1000` | `DAMAGES_ZEPPELIN` |
| `0x2000` | `SHAKES_CAMERA` |
| `0x4000` | `BEEPER` |
| `0x8000` | `BEEPER_SEEKER` |
| `0x10000` | `SMOKE_SCREEN` |
| `0x20000` | `REAR` |
| `0x40000` | `TANGLER` |
| `0x80000` | `DETONATION_DOT_PRODUCT` is present |

`0x01`, `0x02`, `0x04`, `0x80` and `0x100` are never set by the parser. Note that the last row is a
**presence** bit, not a behaviour flag: the value itself goes to `+0x20`, and the bit only records
that the weapon authored the key.

There is no bit for `HIGH_EXPLOSIVE`'s counterpart, no bit for guidance, and no bit for `CRATER`.
Armour-piercing versus high-explosive is the `ARMOR_DAMAGE`/`HEALTH_DAMAGE` pair plus this one bit;
guidance is `TURN_RATE` with no flag at all.

Parsing `TANGLER` has a side effect the other flags do not: `FUN_004ba6f0` registers a handler on the
weapon record (`FUN_005aec90`, target `0x004ba660`, which Ghidra has not resolved as a function).

## The launch-side dispatch

`FUN_004b6820` is the shot routine. It walks the shooter's weapon-slot list and acts on each slot
whose trigger byte is set. Everywhere in it, gun versus ordnance is decided by shifting the flags
dword right by 6 and taking bit 0, which is `CANNON`.

**The aim gate, and who it applies to.** Before anything is spawned, the mount's aim quality
(the `+0xa4` written by `FUN_004b7670`, see [`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md)) must
reach a threshold that differs by weapon class: **0.9962 for ordnance, 0.9848 for guns**, that is
cos 5 degrees against cos 10 degrees. Ordnance must be aimed more than twice as precisely as a gun
before it will leave the rail. The whole gate is **skipped when the shooter is the player**, and also
under a shooter flag at `+0x3e`.

**`REAR` inverts the gate and the spawn.** For a `REAR` weapon (`0x20000`) the routine negates the
mount's aim quality before testing it, so the weapon fires when the target is **behind** rather than
ahead. It additionally requires the shooter's `+0xba` flag, which the hit handler sets when the
aircraft absorbs damage and refreshes with an expiry 8 seconds out. So the rear-arc flare is not a
freely-fired weapon: an AI throws it only while it is being shot at. `REAR` then flips the spawn
direction (the launch axis is negated for an ordinary weapon and taken as-is for a rear one) and
flips the sign of a 10 m offset along that axis.

**`DAMAGES_ZEPPELIN` is a two-way gate, not a permission.** The routine asks the current target
whether it is a zeppelin (vtable slot `+0x1c`) and drops the trigger both when a non-`DAMAGES_ZEPPELIN`
weapon is pointed at a zeppelin **and** when a `DAMAGES_ZEPPELIN` weapon is pointed at anything else.
The torpedo is therefore restricted to zeppelins on this path, not merely permitted against them.

**`SMOKE_SCREEN` spawns no projectile.** Both the player and the AI branch test `0x10000` around the
spawn call. Without it, the round goes to the ordinary projectile spawn `FUN_005aef40` and is
registered by `FUN_00441830`. With it, neither runs: the routine allocates a 0x18-byte object through
`FUN_004b8d50`, handing it the shared `TIME` at ext `+0x18`, and pushes it onto a world list
(`DAT_0071dbbc`, counted by `DAT_0071dbc0`). The smoker is a world effect placed at the mount, not a
round in flight.

**A decoy registration sits on the `REAR` path**, gated on a bit `0x8000` of a word at weapon record
`+0x74` (not the extension struct's flags dword) and taken only when the player currently holds no
target. It writes an entry into a 20-slot ring buffer at `DAT_0071dbc8` and points the player's target
block (`+0xa4`..`+0xa6`, `+0x1ca`) at it. Reading that as the flare's decoy behaviour is an inference
from the writes, not from a decoded consumer.

**The firing shake is sized by `CALIBER`.** When the player fires, the routine calls `FUN_0042c070`
with kind **0** and `CALIBER` times a global at `DAT_0064ef78 + 0x3c`. That is the same feedback call
the hit handler uses with kinds 1, 2 and 3, which confirms from the other direction that
`SHAKES_CAMERA` is not the fire-path mechanism.

## The proximity fuse

`FUN_004b5fb0` runs the fuse as part of the world tick. For each round in flight (`DAT_0064f78c`) it
walks **the aircraft list** (`DAT_0071dabc`), skips anything on the shooter's own side, and tests
range against the round's weapon def `+0x44`. Nothing else is a candidate: the loop never touches
world geometry, which decodes what was previously a recollection, that the original fuses on
aircraft and never on terrain.

When the weapon carries `DETONATION_DOT_PRODUCT` (`0x80000`), passing the range test is not enough.
The routine normalises the vector between the two and dots it against **the candidate's** orientation
axis (`+0x198`..`+0x1a0`), and requires that dot to reach the authored threshold at ext `+0x20`. Below
it, the candidate is skipped and the round flies on. The cone is measured against the target's
facing, not the round's. `FUN_004b9770`, the terminal-impact path, repeats the same test against the
victim's basis before it will resolve a hit.

## The hit-side dispatch

`FUN_004b9bc0` is the routine that applies one weapon hit to one aircraft. It receives the victim,
the weapon record, the **distance** from the detonation, the struck zone id, the shooter, and a
two-float damage pair (armour, health). It reads the flags dword from `weapon+0x210` and branches,
and the branch order is what decides which types can ever deal damage:

1. **Feedback magnitude.** A kind and a magnitude are computed for the player's per-hit feedback call
   `FUN_0042c070`, which runs only when the victim is the player. Kind 1 is a `CANNON` hit, sized by
   `CALIBER` times a global; kind 2 is anything else, sized by the larger of the two damage figures
   times a different global. `HIGH_EXPLOSIVE` applies a third global on top. Kind 3 is
   `SHAKES_CAMERA`, and it is the only one of the three that attenuates with distance, falling
   linearly to zero at the weapon record's `+0x40` (an effect-radius field; which authored key writes
   it is unread here).
   ⚠ The `HIGH_EXPLOSIVE` branch tests `distance <= 400.0` and then applies the **same** multiplier in
   both arms. The comparison is dead as shipped.
2. **`SONIC` or `FLASH`** (`0x200 | 0x800` tested together). Calls `FUN_0042e840` with a boolean taken
   from bit `0xb`, which is what separates the two. On a hit against the player it then builds a
   colour and calls `FUN_0042e9d0`: white for `FLASH`, **red for `SONIC`**. Then it zeroes both damage
   figures and returns.
3. **`BEEPER`** (`0x4000`). Tests the shooter against the victim (`FUN_004b8ce0`), then tags the
   victim by handing `FUN_004b88a0` the shared `TIME` at `+0x18`. Zeroes both damage figures and
   returns.
4. **`TANGLER`** (`0x40000`). Zeroes both damage figures, then computes an engine-dead duration and
   calls `FUN_004b1690`. See below.
5. **Everything else** is the ordinary damage path: a self-hit guard (a round whose shooter is its
   victim deals nothing), then the struck zone is found by walking the victim's zone list at stride
   0x58 and matching the zone id, and the pair is spent against that zone (`FUN_004b3bf0`) or against
   the whole vehicle when no zone matches (`FUN_004b8070`).

**Four of the twelve types cannot damage anything.** `SONIC`, `FLASH`, `BEEPER` and `TANGLER` each
zero the damage pair before returning, so their authored `ARMOR_DAMAGE`/`HEALTH_DAMAGE` figures are
never spent. For `FLASH` (`wep_09`) and the rear-arc flare (`wep_15`) that matches the authored
`DAMAGE 0`; for `SONIC` (`wep_08`), the `BEEPER` (`wep_10`) and the choker (`wep_12`), which all
carry a real damage pair, **the data is misleading and the engine discards it**.

## The choker, settled

The engine-dead duration is not the authored pair read straight off. `FUN_004b9bc0` computes

    duration = ENGINE_DEAD_max * (1 - distance / TANGLER_RADIUS),  floored at ENGINE_DEAD_min

and passes it to `FUN_004b1690`, which sets bit `2` of the victim's disabled-systems mask at `+0x2dc`
and raises the timer at `+0x2e0` to that duration if it is longer than what is already running. The
mask's rising edge calls `FUN_004b15c0` and its falling edge `FUN_004b1630`, the engine stop and
restart. With this install's authored `TANGLER` (`wep_12`: `RADIUS [35]`, `ENGINE_DEAD [5,13]`) a
dead-centre hit kills the engine for 13 s, decaying to the 5 s floor at 21.5 m and holding there out
to the 35 m catch radius.

**`ENGINE_DEAD` is a pair of globals, not a per-weapon field.** `FUN_004ba6f0` writes the two values
into `DAT_0062b120` and `DAT_0062b124` rather than into the weapon's own struct, so the last-parsed
`TANGLER` weapon sets them for every choker in the install. The static image ships them at 2.0 and
10.0; loading `wep_12` overwrites them with 5 and 13.

**There is no airspeed clamp.** The choker branch does exactly two things: zero the damage, and set
the engine-dead bit with a timer. It does not touch velocity, drag, lift or any control authority.
The recollection recorded in [`formats/weapons.md`](../formats/weapons.md), that the choker drops the
airframe to stall speed essentially instantly, is **not** what this routine does, and there is no
second effect for it to be hiding in on the hit path. Whatever the felt instantaneity was, it is the
engine cutout plus the flight model, not an authored clamp.

## Two answers this routine gives to other items

**Blast knockback is authored, not invented.** Late in `FUN_004b9bc0`, when the victim is in vehicle
state `+0x67c == 2` and the larger damage figure exceeds **5.0**, it calls `FUN_0048f5e0` with a
direction and two magnitudes, both derived from `damage * vehicle_def[+0xa0]`: one scaled by
**0.005** and one by **0.0333**. The direction is the hit point to source vector. So the original
does apply a per-hit impulse, it scales with damage and with a per-airframe constant, it has a
damage threshold below which nothing moves, and it carries two magnitudes rather than one.

**Being hit has a direction cue with two variants.** Under a global gate (`DAT_0071c4e0`), the
routine computes the bearing to the hit source as an `atan2` converted to degrees and calls
`FUN_00481330` for a `CANNON` hit or `FUN_004813c0` for anything else, passing the damage magnitude
alongside.

## Still open

- **Guidance.** `TURN_RATE` is the only thing separating the Seeker from a dumbfire rocket, and its
  reader has not been located. Nothing in the shot routine, the fuse or the hit handler steers a
  round.
- **The torpedo's shootable flyout.** No reader was located for `TORPEDO` (`0x08`) or `TARGETABLE`
  (`0x20`), so how `FLYOUT_HEALTH` is spent, and how a round becomes a target, is unread.
- **`BEEPER_SEEKER`** (`0x8000`). No reader located. The `BEEPER` half is decoded on both the launch
  and hit sides, but what consumes the tag it leaves is not.
- Absence of a located reader is not proof of absence. The bit searches behind these three were
  truncated by the tool's result cap and are not exhaustive.
- `FUN_00480f50` tests `REAR` on the player's fire-feedback path and was not opened.
- `FUN_004881e0` is the player's fire-input tick. It routes by `CANNON` (`0x40`) and `ROCKET`
  (`0x10`) only, feeding `CALIBER` to `FUN_004810d0` for guns and the whole weapon to `FUN_00480f50`
  for ordnance.

## Evidence & limits

- `FUN_004ba6f0` was read in full, so the flag map and the struct layout are complete for this build.
- `FUN_004b9bc0` was read in full. It handles hits **on an aircraft**; whether ground objects and
  zeppelins route through the same function is unread, so the "four types deal no damage" finding is
  stated for aircraft targets.
- `FUN_004b6820`, `FUN_004b5fb0`, `FUN_004b9770` and `FUN_004b1690` were read in full.
- `FUN_0042c070`, `FUN_0042e840`, `FUN_0042e9d0`, `FUN_0048f5e0`, `FUN_004b8ce0`, `FUN_004b8d50`,
  `FUN_005aef40`, `FUN_004b15c0` and `FUN_004b1630` were not opened; their roles above are inferred
  from their arguments and call sites, and are labelled as such.
- Weapon-record offsets `+0x40` (an effect radius), `+0x44` (the fuse trigger distance) and `+0x74`
  (a second flags word) are named by use. Which authored key writes each one was not traced back
  through the `.zrd` parse, so the mapping to `IMPACT_PROXIMITY` and `DETONATION_DISTANCE` is
  inferred from the arithmetic they appear in.
- The globals indexed off `DAT_0064ef78` (the feedback multipliers at `+0x3c`, `+0x68`, `+0x94`,
  `+0x98`, `+0xc0`) were not resolved to values.
