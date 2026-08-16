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
and what they spawn), **guidance**, the **proximity fuse**, the **shootable flyout**, and the
**hit-side dispatch** (what happens when a round reaches an aircraft).

## A second flags word, and four keys nothing authors

`FUN_004ba6f0`'s extension struct is not the only place a weapon's behaviour lives. The `.zrd`
dispatcher `FUN_005ad630` writes a **separate** flags word at weapon record `+0x74`, and the guidance
and spawn paths read that one, not the extension struct. The two are unrelated bit spaces: `0x08` in
the extension struct means `TORPEDO`, while `0x08` at `+0x74` means the weapon authored
`FLYOUT_HEALTH`.

| `+0x74` bit | Set by |
|---|---|
| `0x08` | `FLYOUT_HEALTH` is present |
| `0x800` | (a key parsed just before `LOCK_ON`) |
| `0x8000` | `LOCK_ON` is present |
| `0x10000` | `LOCK_ON_LEAD` is present |
| `0x100000` | `REMOTE_DETONATE` |
| `0x200000` | `TETHER_GUIDED` |

And the scalar slots the guidance path uses:

| Offset | Key |
|---|---|
| `+0x30` | `TURN_RATE` |
| `+0x34` | `PITCH_RATE` |
| `+0x58` | `LOCK_ON` |
| `+0x5c` | `TURN_SUSPEND_TIME` |
| `+0x7c`, `+0x80` | `LOCK_ON_LEAD` element 0 and element 1 |
| `+0x84` | derived from the two above |
| `+0x8c`, `+0x90` | the flyout's damage accumulator (initialised to 0) and `FLYOUT_HEALTH` |

⚠ **Four of those keys are parsed and authored by nothing.** `PITCH_RATE`, `TURN_SUSPEND_TIME`,
`TETHER_GUIDED` and `REMOTE_DETONATE` appear in no entry of this install's `weapons.zrd.json`, so
they are absent from [`formats/weapons.md`](../formats/weapons.md)'s field census, which enumerates
what the data carries. They still shape the shipped behaviour by their **defaults**: a
`TURN_SUSPEND_TIME` of zero is what gives every guided round its turn authority from the first frame.

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

**`REAR` inverts the gate and the spawn.** Already decoded in
[`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md), and repeated here only for what it adds: for a `REAR`
weapon (`0x20000`) the routine negates the mount's aim quality before testing it, so the weapon fires
when the target is **behind**, and it requires the shooter's `+0xba` byte, which that page reads as
being-pursued and which the hit handler sets when the aircraft absorbs damage, refreshing an expiry
8 seconds out. New here: `REAR` also flips the **spawn** direction (the launch axis is negated for an
ordinary weapon and taken as-is for a rear one) and flips the sign of a 10 m offset along that axis.
⚠ The `REAR` carrier in this install is `wep_13`, the **smoke screen**, not the flare: it is the one
entry carrying both `REAR` and `SMOKE_SCREEN`, which makes it a screen laid behind a pursued aircraft.

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

**Firing a `LOCK_ON` weapon with no target acquires one.** Gated on `+0x74` bit `0x8000`, which is
`LOCK_ON` being present, and taken only when the player currently holds no target, the routine writes
an entry into a 20-slot ring buffer at `DAT_0071dbc8` and points the player's target block
(`+0xa4`..`+0xa6`, `+0x1ca`) at it.

**The firing shake is sized by `CALIBER`.** When the player fires, the routine calls `FUN_0042c070`
with kind **0** and `CALIBER` times a global at `DAT_0064ef78 + 0x3c`. That is the same feedback call
the hit handler uses with kinds 1, 2 and 3, which confirms from the other direction that
`SHAKES_CAMERA` is not the fire-path mechanism.

## Guidance

`FUN_005af960`, reached from `FUN_005af720`, is the per-round steering step. It runs for **every**
round, not only guided ones; what makes a rocket dumbfire is that its `TURN_RATE` sentinel of 0.001
buys it almost no turn per frame.

**The desired direction** starts as the bearing from the round's position to its target. If the
weapon authored `LOCK_ON_LEAD` (`+0x74` bit `0x10000`) and the round is older than `+0x7c`, the
routine solves an intercept instead of a bearing, and between `+0x7c` and `+0x80` it **slerps** from
the plain bearing to the full lead solution. So the lead comes in gradually rather than at once.

**The turn authority per frame** is

    maxTurn = TURN_RATE * dt * ramp,  where
    ramp    = 0                                       while age < TURN_SUSPEND_TIME
            = (age - TURN_SUSPEND_TIME) / LOCK_ON     when TURN_SUSPEND_TIME > 0
            = 1                                       when TURN_SUSPEND_TIME is 0 (every shipped weapon)

in radians. The routine takes the angle between the current heading and the desired one, and if it
exceeds `maxTurn` it slerps by exactly `maxTurn / angle` and renormalises; otherwise it snaps to the
desired direction outright.

**Turning costs speed.** After the turn, the round's speed is multiplied by `0.8 + 0.2 * cos(angle)`
every frame it steers, so a hard-turning round bleeds up to 20% of its speed per frame while the turn
lasts. Nothing in the authored data hints at this.

**Terrain avoidance is a weapon field.** When the round is within 10 m of the ground and a collision
probe comes back clear, the routine adds `PITCH_RATE * 2/pi` of "up" to the desired direction and
renormalises, so a guided round noses up to clear terrain. A `TETHER_GUIDED` weapon takes a different
branch: instead of the pull-up it **zeroes any climb component** once the round is near a global
altitude limit, which reads as a ceiling clamp rather than a floor.

### Launch velocity is inherited, and decays over `LOCK_ON`

This is the piece with the most consequence, and it is not visible in the data at all.

At spawn (`FUN_005aef40`), a weapon carrying `LOCK_ON` (`+0x74` bit `0x8000`) has the **launcher's
velocity vector** copied into the round at `+0x30`..`+0x38`. A weapon without `LOCK_ON` gets a zero
vector there. Then every frame, while the round is younger than `LOCK_ON` seconds, the guidance step
sets

    velocity = heading * speed  +  ((LOCK_ON - age) / LOCK_ON) * inheritedLaunchVelocity

so the launcher's contribution is blended out **linearly to zero over `LOCK_ON` seconds**, leaving
the round travelling at its own authored `VELOCITY`. A round launched from a fast aircraft therefore
starts fast and visibly slows to its own cruise, and one launched from a slow aircraft does not.

`LOCK_ON` is doing three separate jobs, which is why the name misleads: it is the lead-guidance ramp
denominator, the launch-velocity decay window, and the flag that decides whether launch velocity is
inherited at all. [`formats/weapons.md`](../formats/weapons.md) describes it as lock-acquisition
time; that is the one job this decode did **not** find it doing.

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

## The shootable flyout

`FLYOUT_HEALTH`, not `TARGETABLE`, is what makes a round shootable. At the end of the spawn
`FUN_005aef40`:

- **Without** `FLYOUT_HEALTH` (`+0x74` bit `0x08` clear), the round's `+0x19c` and `+0x19d` are both
  set to **-1.0**, the not-shootable sentinel, and its scene node's `0x8000000` flag is cleared.
- **With** it, `+0x19c` takes the weapon's `+0x8c` (the zero the parser wrote, so a damage
  accumulator starting empty) and `+0x19d` takes `FLYOUT_HEALTH` itself. The node gets the
  `0x8000000` flag set, a `1` at node `+0xbc`, and a **back-pointer to the round at node `+0x40`**,
  which is what lets a collision against the node find the round it belongs to.

The AI's side of this is decoded separately: [`aiPilot.md`](aiPilot.md) has `TargetProjectile`
(list `DAT_0064f78c`, constructor `FUN_004a63b0`) as the consumer of `TARGETABLE`, admitted on the
round's `+0x6c` byte. What writes `+0x6c` from the flag was not traced, so the join between the
`TARGETABLE` admission and the `FLYOUT_HEALTH` health pair is the one link still missing.

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

- **`BEEPER_SEEKER`** (extension struct `0x8000`). No reader located. The `BEEPER` half is decoded on
  both the launch and hit sides, but what consumes the tag it leaves is not. The Seeker steers
  through the ordinary guidance step like every other round, so whatever `BEEPER_SEEKER` adds is
  target **selection**, not steering.
- **`TORPEDO`** (extension struct `0x08`). No reader located. The torpedo's shootability comes from
  `FLYOUT_HEALTH` and its zeppelin restriction from `DAMAGES_ZEPPELIN`, so this flag may carry
  nothing.
- **What writes the round's `+0x6c`**, the `TARGETABLE` admission byte that `aiPilot.md` names.
- Absence of a located reader is not proof of absence. The bit searches behind the two flags above
  were truncated by the tool's result cap and are not exhaustive.
- `FUN_00480f50` tests `REAR` on the player's fire-feedback path and was not opened.
- `FUN_004881e0` is the player's fire-input tick. It routes by `CANNON` (`0x40`) and `ROCKET`
  (`0x10`) only, feeding `CALIBER` to `FUN_004810d0` for guns and the whole weapon to `FUN_00480f50`
  for ordnance.

## Evidence & limits

- `FUN_004ba6f0` was read in full, so the flag map and the struct layout are complete for this build.
- `FUN_004b9bc0` was read in full. It handles hits **on an aircraft**; whether ground objects and
  zeppelins route through the same function is unread, so the "four types deal no damage" finding is
  stated for aircraft targets.
- `FUN_004b6820`, `FUN_004b5fb0`, `FUN_004b9770`, `FUN_005aef40`, `FUN_005af960` and `FUN_004b1690`
  were read in full.
- The `+0x74` bit assignments and the guidance scalar offsets were read from `FUN_005ad630`'s parse
  sites one key at a time, each confirmed against the key string it is stored beside. `+0x84` is
  computed from `LOCK_ON_LEAD`'s two elements by an expression that was not read.
- `FUN_00538ca0` (bearing), `FUN_0053e56d` (the intercept solve), `FUN_00538d70` (slerp) and
  `FUN_004c7630` (the terrain probe) were not opened; their roles are inferred from arguments and
  from the arithmetic around the call.
- `FUN_0042c070`, `FUN_0042e840`, `FUN_0042e9d0`, `FUN_0048f5e0`, `FUN_004b8ce0`, `FUN_004b8d50`,
  `FUN_005aef40`, `FUN_004b15c0` and `FUN_004b1630` were not opened; their roles above are inferred
  from their arguments and call sites, and are labelled as such.
- Weapon-record offsets `+0x40` (an effect radius), `+0x44` (the fuse trigger distance) and `+0x74`
  (a second flags word) are named by use. Which authored key writes each one was not traced back
  through the `.zrd` parse, so the mapping to `IMPACT_PROXIMITY` and `DETONATION_DISTANCE` is
  inferred from the arithmetic they appear in.
- The globals indexed off `DAT_0064ef78` (the feedback multipliers at `+0x3c`, `+0x68`, `+0x94`,
  `+0x98`, `+0xc0`) were not resolved to values.
