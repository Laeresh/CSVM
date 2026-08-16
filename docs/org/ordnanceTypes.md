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
| `0x800` | `INSTANT` |
| `0x4000` | `MINE` |
| `0x8000` | `LOCK_ON` is present |
| `0x10000` | `LOCK_ON_LEAD` is present |
| `0x100000` | `REMOTE_DETONATE` |
| `0x200000` | `TETHER_GUIDED` |
| `0x8000000` | `RANDOM_DEVIATION` |

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

⚠ **Ten of the keys this parser accepts are authored by nothing.** `PITCH_RATE`,
`TURN_SUSPEND_TIME`, `TETHER_GUIDED`, `REMOTE_DETONATE`, `INSTANT`, `MINE`, `RANDOM_DEVIATION`,
`MULTI_TARGET`, `EXPIRES` and `IMPACT_TYPE` appear in no entry of this install's `weapons.zrd.json`,
so they are absent from [`formats/weapons.md`](../formats/weapons.md)'s field census, which
enumerates what the data carries. They still shape the shipped behaviour by their **defaults**: a
`TURN_SUSPEND_TIME` of zero gives every guided round its turn authority from the first frame,
`INSTANT` being unset makes every round a travelling projectile rather than a one-step hitscan,
`MINE` being unset is why every blast has a falloff, and `RANDOM_DEVIATION` being unset is why no
shipped round wanders.

Six of the ten have traced behaviour; four were only ever seen parsed:

| Key | Where | What setting it would do |
|---|---|---|
| `TURN_SUSPEND_TIME` | `+0x5c` | Suspends guidance for N seconds after launch, then ramps turn authority as `(age − N) / LOCK_ON`. |
| `PITCH_RATE` | `+0x34` | Terrain avoidance: a guided round within 10 m of the ground adds `PITCH_RATE × 2/π` of "up" to its desired heading. |
| `TETHER_GUIDED` | `0x200000` | Swaps that pull-up for a ceiling clamp: the round may not climb past a global altitude. |
| `INSTANT` | `0x800` | The round resolves in one step instead of travelling. Hitscan. |
| `MINE` | `0x4000` | No acceleration; accumulates half of each step, so it flies twice its nominal range; its own motion routine; and its blast applies full damage with **no falloff** anywhere in the radius. |
| `RANDOM_DEVIATION` | `0x8000000` | A bounded random walk when the round has no target or its target is far. |
| `REMOTE_DETONATE` | `0x100000` | **Reader not traced.** |
| `MULTI_TARGET` | — | **Reader not traced.** |
| `EXPIRES` | — | **Reader not traced.** |
| `IMPACT_TYPE` | `+0x190` | **Reader not traced.** |

⚠ Four of those defaults are load-bearing, and one is easy to miss: **`PITCH_RATE` defaulting to 0
means no shipped round avoids terrain.** The avoidance code runs on every guided round; it just adds
zero. That is why rockets fly into hillsides rather than nosing over them.

**Why the set looks like this.** Read together, these ten describe mines, wire-guided rounds held
under a ceiling, multi-target seeking and hitscan weapons. That is a **MechWarrior** weapon roster
rather than an aerial-combat one, and this is the MechWarrior 3 engine: the extractor these decodes
lean on is `mech3ax`, and `CSVM/src/Mech3/` is named for it. The economical explanation is that these
keys are the other game's features carried in a shared codebase and never authored here.
⚠ That is an inference from the key set plus the engine lineage, not a decode. It is recorded because
it explains the shape of the data, and it should not be cited as evidence for anything.

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
weapon record through `FUN_005aec90` (target `0x004ba660`, which Ghidra has not resolved as a
function). That handler lands at weapon `+0x20c` and is the per-weapon impact hook the detonation
path calls first; see [the detonation section](#detonation-the-impact-then-the-splash).

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
⚠ **`REAR` appears exactly once in the whole weapon table, on `wep_13`, the smoke screen.** The flare
`wep_15` is *called* `MSG_WEAP_REARARC_FLASH` and does **not** carry the flag. Its rear-arc character
comes from its own numbers instead: `VELOCITY [1.0]` so it barely moves, `LOCK_ON [2.5]` so it leaves
at the launching aircraft's speed and sheds it over 2.5 s, and `DETONATION_TIME [2.0]` so it goes off
two seconds after release. You drop it and fly away; it hangs where you left it and flashes at
whoever is still there. `IMPACT_PROXIMITY [500]` gives it an enormous radius, and `FLASH`'s
facing requirement is what keeps it from blinding you as you leave.

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

## Who aims ordnance, and who does not

The gun aim assist `FUN_004b6530` is reached from the **`CANNON` branch only**. Both ordnance
branches, the player's and the AI's, bypass it, so no ordnance round of any shooter is aim-assisted.
What separates the two is the direction each hands the spawn `FUN_005aef40`:

- **The AI's ordnance leaves along the mount's clamped aim, in world space.** The direction argument
  is the mount record's `+0x3c`, which `FUN_004b7670` writes at the end of every mount update as the
  clamped aim `+0x48`–`+0x50` rotated through the aircraft's orientation matrix. That aim tracks the
  lead solve within the authored `gun_pitch`/`gun_yaw` band
  ([`aiPilot/aiWeapons.md`](aiPilot/aiWeapons.md)), so an AI's rocket really does leave along a
  direction that follows the target.
- **The player's ordnance leaves along the aircraft's own basis axis**, negated, or taken as-is for a
  `REAR` weapon. The mount contributes the spawn **position** only; its aim is not read.

So the original gives the player no aim component on ordnance, and gives the AI one. ⚠ Note the
remaining difference on our side: we launch along the **pylon marker's** transform, where the
original uses the **aircraft's** axis. Those coincide only for a pylon whose marker is aligned with
the airframe.

## Guidance

`FUN_005af960` is the per-round steering step, and `FUN_005af720`, the projectile tick, decides who
gets it. The gate is **`LOCK_ON` present (`+0x74` bit `0x8000`) and the round holding a target at
`+0x70`**. A round failing either condition never enters the steering step at all; it goes straight
to the motion step, `FUN_005afd50` normally or a plain constant-velocity integration when `+0x74` bit
`0x800` is set.

So "guided" has two independent gates in the original, and neither is a `GUIDED` flag: a weapon must
author `LOCK_ON` to be steered at all, and the round must have acquired something. `TURN_RATE` then
decides how hard it may turn, with the 0.001 sentinel meaning a `LOCK_ON` weapon that is steered but
turns almost imperceptibly.

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

⚠ This decay lives **inside** the steering step, so it inherits that step's gate: a `LOCK_ON` round
fired with **no target** is never stepped through `FUN_005af960` and so keeps its inherited launch
velocity indefinitely. In practice the shot routine hands a `LOCK_ON` weapon a target when the player
has none, so the usual case is the decaying one, but the two are not the same rule.

`LOCK_ON` is doing three separate jobs, which is why the name misleads: it is the lead-guidance ramp
denominator, the launch-velocity decay window, and the flag that decides whether launch velocity is
inherited at all. [`formats/weapons.md`](../formats/weapons.md) describes it as lock-acquisition
time; that is the one job this decode did **not** find it doing.

## The motion step, and where a round dies

`FUN_005afd50` moves every round that is not being steered, and it holds most of the remaining
ballistics keys. In order:

- **Acceleration.** While the round's speed (`+0x640`) is below its cap (`+0x644`), speed increases
  by `ACCELERATION * dt` (weapon `+0x38`) and is clamped to the cap (the test at `0x005afd76`, the
  clamp at `0x005afd98`). A round already at or past its cap is left alone rather than clamped down.
  Nothing anywhere reduces speed except the steering step's turn penalty, so **there is no drag term
  in the original**.
- **What the cap is, and where a motor round starts.** Both are seeded in the spawn,
  `FUN_005aef40`. The cap takes the weapon's `VELOCITY` (`+0x2c`) at `0x005af0ea`. A weapon with no
  `ACCELERATION` (and without `INSTANT`) then has its speed seeded at `VELOCITY` too
  (`0x005af147`), **equal to its cap**, which is why nothing accelerates it. A weapon that does
  author `ACCELERATION` has its speed seeded at `1e-4` instead (`0x005af318`) and the **launcher's
  own speed** `|launcherVelocity|` added to both the cap (`0x005af356`) and the speed
  (`0x005af371`). So a motor round leaves at the speed of the aircraft that fired it and climbs to
  `VELOCITY` **above** that, rather than starting at `VELOCITY`: `wep_04` off a standing launcher
  takes its full 3 s to reach 450 m/s, and `wep_27`'s flak leaves an emplacement at nothing and is
  still short of its authored 850 when its 900 m `RANGE` runs out.
  ⚠ For a `LOCK_ON` motor round the launcher's speed therefore arrives twice: once as this scalar
  and once as the inherited vector at `+0x30`.
- **Gravity.** When the weapon authors `GRAVITY` (weapon `+0x54`), the round's vertical velocity
  loses `GRAVITY * dt` each frame. It is an acceleration in m/s², not a scale on world gravity. The
  reader exists and works; every entry in this install authors 0.0, which is why the shipped rounds
  fly flat. ⚠ The one path that would expose it is broken in the original: an accelerating round
  with a non-zero `GRAVITY` rebuilds its velocity as `velocity + heading * speed` each frame
  (`FUN_005389a0` called on `+0x60` rather than on the inherited vector at `+0x30`), which
  compounds. No shipped weapon reaches it.
- **First-frame guard.** On the frame a round is spawned, the integration delta uses `1e-6` instead
  of the real frame time, so a round never jumps a full step on its birth frame.
- **Wander.** Under `RANDOM_DEVIATION`, and only when the round has no target or its target is
  farther than `max(4 * DETONATION_DISTANCE, 400 m)`, three `rand()` draws build an angular
  acceleration of magnitude `5 * dt`, sign-flipped to keep pushing outward, integrated into an offset
  bounded at ±1.0, ±0.75, ±1.0 with a restoring `10 * dt`. An unguided or far-from-target round
  wobbles rather than flying a perfect line.
- **Reveal distance, which is what `RANGE_MINIMUM` actually is.** The key sets `+0x74` bit
  `0x80000000` and stores at `+0x24` (`FUN_005ad630` at `0x005adfbb`), and a `FLYOUT_HEALTH` round
  carrying that bit is made **visible** only once it has travelled `+0x24`. So `wep_14`'s
  `RANGE_MINIMUM [300]` hides the torpedo for its first 300 m rather than disarming it.
  ⚠ [`formats/weapons.md`](../formats/weapons.md) glosses the key as "minimum arming range". That
  reading is contradicted here: nothing on this path gates arming.
- **The ceiling.** `TETHER_GUIDED` clamps the vertical step so the round cannot climb past a global
  limit, confirming the reading in the guidance section.

**Three independent ways a round ends**, all resolved here by calling `FUN_005ac3a0`:

1. **Range.** Distance travelled accumulates in `+0x664`; once it reaches `RANGE` (weapon `+0x1c`)
   the round is done. A `MINE` accumulates only **half** of each step, so it flies twice its nominal
   range before expiring.
2. **Timed fuse.** If the weapon authors `DETONATION_TIME` (weapon `+0x48`) and the round's age
   exceeds it, the round detonates. This is the rear-arc flare's 2.0 s.
3. **Target proximity.** A round with a target that comes within `DETONATION_DISTANCE` of it
   detonates. This is a **second, per-round fuse path** alongside the list sweep in `FUN_004b5fb0`:
   the sweep catches anything passing near any aircraft, this one catches the round's own target.

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

## The beeper and the seeker, which are one weapon in two halves

`FUN_00441830` runs immediately after every spawn and is where a round acquires its per-type
behaviour. Three things happen there, each on its own flag:

- **`BEEPER_SEEKER`** (extension struct `0x8000`) installs `FUN_00441780` as the round's **per-frame
  retarget callback** at round `+0x688`.
- **`TARGETABLE`** (extension struct `0x20`) wraps the round in a 0x70-byte `TargetProjectile`
  (vtable `PTR_LAB_006063c4`), naming it from string id `0x2f6a`, and pushes it onto `DAT_0064f78c`.
- A weapon whose fuse distance (`+0x44`) exceeds 0.01 gets the same wrapper if it does not have one
  already, with the weapon stored at wrapper `+0x68`.

The wrapper's `+0x6c` byte is set to **1 only on the `TARGETABLE` path** and left 0 for a fuse-only
wrapper, which is exactly the admission byte [`aiPilot.md`](aiPilot.md) names for `TargetProjectile`.
So `TARGETABLE` is what makes a round appear in the target list, while `FLYOUT_HEALTH` is what lets
it absorb damage; the two flags do different halves of "shootable" and the torpedo carries both.

⚠ This also corrects the fuse section above: `DAT_0064f78c` is **not** every round in flight. Only
rounds that are `TARGETABLE` or carry a fuse distance are ever wrapped into it, so the fuse loop
walks exactly the set that could fuse, and a plain round is invisible to it.

### The full beeper loop

1. A `BEEPER` round reaches an aircraft. `FUN_004b9bc0` zeroes its damage and calls `FUN_004b88a0`,
   which builds a tag object carrying the weapon's shared `TIME` and pushes it onto the tag list
   `DAT_0071dbac` (count `DAT_0071dbb0`).
2. `FUN_004b8ad0` counts every tag down by the frame delta through `FUN_004b89c0`. If the tagged
   aircraft dies or is removed (`+0x91d`/`+0x91f`), the countdown is slammed to **-1.0**. Crossing
   zero calls `FUN_004b8970`, which is where the tag stops being usable; the entry is only deleted
   once it passes **-5.0**, so there is a five-second tail after expiry.
3. A `BEEPER_SEEKER` round's callback `FUN_00441780` queries the tag list every frame through
   `FUN_004b8b50`, passing the round's **position** (`+0x48`) and **heading** (`+0x3c`). Tags with a
   countdown at or below zero are skipped.
4. The query keeps a running best and replaces it by the rule below.
5. The winner is written into the round's target fields `+0x70` and `+0x74`, which are the same
   fields the guidance step reads. From there the round steers toward it at `TURN_RATE` like any
   other guided round.

### The query's selection rule

Read from the branch structure at `0x004b8c03`–`0x004b8c72`, with all four constants read out of the
image. **The dot's sign is inverted from the intuitive one**: the vector is taken from the tag toward
the round and dotted with the round's heading, so a tag **directly ahead scores near -1** and a lower
dot is a better-aligned candidate. With that convention, and writing `ratio` for
`candidateDistance / bestDistance`:

- **No current best** → take the candidate.
- **Candidate better aligned** (`candDot < bestDot`) → take it if `ratio < 1.2`. A better-aligned tag
  is allowed to be up to 20% farther away.
- **Candidate worse or equally aligned** (`candDot >= bestDot`) → take it only if it is strictly
  nearer (`ratio < 1.0`) **and** its alignment is not much worse, meaning
  `candDot <= 0.7` or `candDot < bestDot + 0.1`.

The four constants are `1.2` (`0x006040ac`), `1.0` (`0x006032dc`), `0.7` (`0x006035b0`) and `0.1`
(`0x006034a8`). Net effect: alignment leads, range breaks near-ties, and a nearer tag can only steal
the pick if it gives up less than 0.1 of alignment.

So the two weapons are one system: `wep_10` paints a target for its `TIME`, and `wep_11`, the sole
`BEEPER_SEEKER` and the sole weapon with a real `TURN_RATE` (1.25), homes on whatever is painted.
Neither half is useful alone, which is also why the Seeker is the only entry that needs guidance at
all.

## The shootable flyout

`FLYOUT_HEALTH`, not `TARGETABLE`, is what makes a round shootable. At the end of the spawn
`FUN_005aef40`:

- **Without** `FLYOUT_HEALTH` (`+0x74` bit `0x08` clear), the round's `+0x19c` and `+0x19d` are both
  set to **-1.0**, the not-shootable sentinel, and its scene node's `0x8000000` flag is cleared.
- **With** it, `+0x19c` takes the weapon's `+0x8c` (the zero the parser wrote, so a damage
  accumulator starting empty) and `+0x19d` takes `FLYOUT_HEALTH` itself. The node gets the
  `0x8000000` flag set, a `1` at node `+0xbc`, and a **back-pointer to the round at node `+0x40`**,
  which is what lets a collision against the node find the round it belongs to.

`TARGETABLE`'s half of the job, admitting the round to the target list, is `FUN_00441830` above.

### How the flyout's health is spent, and what happens at zero

`FUN_005abcf0`, the projectile-hit resolver, tests the struck node's `+0xbc` for the `1` the spawn
wrote and follows the node's `+0x40` back-pointer to the round. It then spends the pair exactly the
way an aircraft damage zone is spent, **armour first, then health**:

    flyout[+0x670] = max(0, flyout[+0x670] - incomingArmourDamage)
    if flyout[+0x670] == 0:
        flyout[+0x674] = max(0, flyout[+0x674] - incomingHealthDamage)

⚠ The armour pool starts at **zero**. The spawn seeds `+0x670` from weapon `+0x8c`, and `+0x8c` is
only ever written as the literal 0 by the parser, with no key feeding it. So a flyout's armour pool
is always empty and the first hit spends health directly. The two-pool structure is real but inert as
shipped.

The projectile tick `FUN_005af720` then checks `+0x674 == 0.0` **every frame, before anything else**,
and on zero destroys the round: it plays the weapon's `DESTROY_ANIMATION` (weapon `+0x188`) if one is
authored, or calls `FUN_005ac3a0` if not, and clears the alive flag. This is why the not-shootable
sentinel is **-1.0** rather than 0: a round without `FLYOUT_HEALTH` must never satisfy that equality.

## Detonation: the impact, then the splash

[`weaponImpact.md`](weaponImpact.md) covers the per-surface `IMPACT` **table**, meaning which effect
and sound a row binds and how rows are filled. It does not cover what a detonation *does*. That is
`FUN_005ac3a0`, and it has two halves.

### Half one, the direct impact (`FUN_005ac7a0`)

- **A per-weapon impact hook.** If the weapon has a callback at `+0x20c`, it runs first and returns a
  suppression mask: bit 1 silences the row's sound, bit 2 suppresses damage and effects, bit 4
  suppresses the impact animation. This is the slot `FUN_005aec90` writes, and the `TANGLER` parse is
  the one caller that installs one (target `0x004ba660`), which closes the loose end noted at the top
  of this page.
- **Direct damage** is the authored pair scaled by a per-round factor at round `+0x678`, applied
  through `FUN_005abcf0` to the struck object alone.
- **The explosion effect** then spawns: `FUN_005ac690` for a `ROCKET`, `FUN_005ac580` for a
  `TANGLER`. Both are effect spawners rather than damage, both gate on the struck node carrying
  `0x10000`, and both randomise a count and two scales from weapon fields `+0x198`/`+0x1c0`,
  `+0x1a4`/`+0x1cc` and `+0x1a8`/`+0x1d0`.
- **The row's own bindings** follow: the sound through `FUN_005ad100`, the `ANIMATION`, and the
  `SURFACE_ANIMATION` **oriented by the struck surface's normal**, which the routine builds from the
  hit record before spawning it.

### Half two, the splash (`FUN_005aca30` then `FUN_005acac0`)

Gated on `weapon +0x3c > 0`. `FUN_005aca30` runs a sphere query (`FUN_004cb420`) of radius
`round[+0x678] * IMPACT_PROXIMITY` and fills a hit buffer. `FUN_005acac0` then walks it and, per
entry:

    t      = 1 - dSurface² / IMPACT_PROXIMITY²
    armour = t * ARMOR_DAMAGE  * round[+0x678]
    health = t * HEALTH_DAMAGE * round[+0x678]

**The falloff is quadratic in distance, not linear**, because both terms of that ratio are squared
quantities (see [the squared-radius convention](#the-engine-stores-radii-squared)). It reaches zero
at the radius and scales both pools by the same factor. At half the radius a linear curve would give
0.5 and this gives **0.75**, so the original is markedly more generous close in and falls away faster
near the edge.

Three further details:

- **The distance is to the object's bounding-sphere surface, not its centre**, and is clamped to zero
  when the object overlaps the burst. Anything the burst engulfs takes full damage.
- **The blast is occlusion-tested.** `FUN_005aca30` passes both the distance flag and the occlusion
  flag, so for each candidate `FUN_004cb420` casts from the burst centre to the object and drops it
  if something blocks the way. Cover works against splash in the original.
- **At most 32 objects** are collected; past that the query logs "Database intersections array is
  full" and silently stops adding.

A weapon carrying `MINE` (`+0x74` bit `0x4000`) skips the falloff entirely and applies full damage to
everything inside the radius. `round[+0x678]` is a per-round yield multiplier scaling the radius and
both damage figures together, so it is one knob over the whole burst.

### The engine stores radii squared

`FUN_005ad630` keeps two forms of `IMPACT_PROXIMITY` and a squared `DETONATION_DISTANCE`, and every
distance it compares them against comes from `FUN_00538880`, which returns a **squared** distance
with no square root:

| Offset | Holds |
|---|---|
| `+0x3c` | `IMPACT_PROXIMITY`, raw, used as the sphere-query radius |
| `+0x40` | `IMPACT_PROXIMITY²`, the falloff denominator |
| `+0x44` | `DETONATION_DISTANCE²`, the fuse trigger |
| `+0x48` | `DETONATION_TIME`, raw seconds |

So any reading of this engine that treats a `FUN_00538880` result as a plain distance, or `+0x40` and
`+0x44` as plain radii, gets the curve shape and the trigger range wrong. Both squarings are the same
`FLD v; FLD ST0; FMUL ST1; FSTP` idiom at `0x005adbb5` and `0x005add73`.

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
2. **`SONIC` or `FLASH`** (`0x200 | 0x800` tested together). `FUN_0042e840` computes an intensity;
   if it is non-zero the victim is affected, and **the victim's kind decides how**. The player gets a
   screen flash through `FUN_0042e9d0`, white for `FLASH` and **red for `SONIC`**. An AI gets
   `FUN_004200d0`, which is a **stun**. Either way both damage figures are then zeroed.
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
never spent. For `FLASH` (`wep_09`) and the flare (`wep_15`) that matches the authored `DAMAGE 0`;
for `SONIC` (`wep_08`), the `BEEPER` (`wep_10`) and the choker (`wep_12`), which all carry a real
damage pair, **the data is misleading and the engine discards it**. Dealing no damage is not the same
as doing nothing, though: three of the four disable the victim instead, which is the section below.

## What the no-damage types do, to the player and to an AI

The player and an AI take **different** effects from the same weapon, decided at the point of impact.
The short version: an AI loses its controls, the player loses their view.

### `SONIC` and `FLASH`: an intensity, then a stun

`FUN_0042e840` produces one intensity in `[0, 1]` from the squared distance ratio
`d² / IMPACT_PROXIMITY²`:

    ratio     = min(1, d² / IMPACT_PROXIMITY²)
    intensity = 1                          while ratio < 0.6
              = 1 - (ratio - 0.6) * 2.5    from there to the radius

So the effect is at **full strength out to √0.6 ≈ 77% of the radius** and only fades over the last
quarter. It is a plateau, not a falloff.

**`FLASH` additionally requires the victim to be facing it**, and `SONIC` does not. For a `FLASH`,
the routine dots the unit vector toward the burst against the victim's forward axis and returns zero
if that dot is negative. Below 0.5 it scales the intensity by **twice the dot**, which meets the
unscaled value exactly at 0.5 and ramps to nothing at 0, so a burst 60° off the nose is untouched and
one at 66° keeps 80% of its strength. A flash going off behind you does nothing; a sonic
burst behind you works at full strength. That is the one place the two flags differ beyond the
screen colour.

The routine returns the intensity and **five times** the intensity. The player's screen flash takes
the first; `FUN_004200d0` takes the second as a **stun duration in seconds**, so a dead-centre hit
stuns for the full 5 s.

`FUN_004200d0` refuses a dead victim (`+0x91d`), the player, a victim with `+0xf8` set, and any
vehicle whose **dispatch class** `+0x67c` is not 0 or 4, that is, anything but a `jet` or a
`wingman` ([`aiPilot.md`](aiPilot.md); the AI *mode* at `+0x358` is not consulted, so a stunned
pilot is accepted). Otherwise it sets AI mode **4**, **zeroes the three stick channels raw and
copied** (`+0x100`/`+0x108`/`+0x10c` and `+0x114`/`+0x11c`/`+0x120`,
[`aiControlLaw.md`](aiControlLaw.md) "The channels"; the throttle lever at `+0x124` is left alone),
and writes the expiry to `+0xc0` as **clock + seconds, overwriting** whatever was there. There is
no max against the running expiry, so a stun landing on a stunned pilot replaces the clock in both
directions.

**`+0x978` is `stun_recovery_interval` and it is display-only here.** The field is the per-pilot
interpolation of the `stun_recovery_interval` pair (`DAT_0071c4c8`/`DAT_0071c4cc`, written by the
`ai_skill_parameters` loader `FUN_004735b0`, defaults 6.0/0.6, authored 4.8/0.6), stored at spawn by
`FUN_0047c210` (`0x0047d16a`) and defaulted to a flat 3.0 in `FUN_004aff80` (`0x004b0404`). Its
only other reader is `FUN_0041d9f0`'s failed sixth-sense test, which is `FUN_004200d0`'s third
caller and passes `+0x978` **as the seconds argument** (`0x0041df89`, "AI has been evaded. Sixth
sense test failed"). The debug `sprintf` inside `FUN_004200d0` prints `+0x978` regardless of what
was passed, so it is truthful for that caller and misleading for the other two: a sonic or flash
hit is stunned for five times the intensity and the smoke screen for `smokescreen_stun_interval`,
and the pilot's recovery interval multiplies neither.

So a sonic or flash round against an AI takes its hands off the controls for up to five seconds. It
is the strongest non-damaging effect in the weapon table. Our side: `AiPilot.Stun` through
`FlightController.TryStunPilot`, which holds the victim guards.

**The player's half is `FUN_0042e9d0`, and it is purely visual.** It is a full-screen colour wash:
`SONIC` red `(1, 0, 0)`, `FLASH` white `(1, 1, 1)`, at a **weight** equal to the intensity and for a
**duration equal to five times the intensity**, the same number the AI stun uses. Two derived timings
at 0.35 and 0.15 of the duration split it into phases; the tick (`FUN_0042eb80`) reads them as an
**attack** over the first 0.15 of the duration (the displayed weight climbs `dt / attack` of the peak
per frame, capped at the peak), a sustain at the peak, and a **release** over the last 0.35 (the
peak sheds `dt / release` of itself per frame, decaying toward 1/e of the sustain), then a hard cut
at the duration. Overlapping washes **blend** rather than replace: the running colour is mixed toward
the new one by the incoming weight (`(old·p + new·w) / (p' + w)`, normalised by the NEW weight) and
the weights combine as `p' = w + p − w·p`, so two flashes are worse than one but never saturate; a
re-hit resets the elapsed clock but not the displayed weight. The first argument is a **start
delay** during which nothing paints, read on the first hit only: the sonic/flash caller passes 1.0 s,
the smoke caller 0. An optional sound handle starts with it.

⚠ **Nothing on the player's path touches the controls.** The player branch calls only the screen
wash and returns; there is no input lockout, no state change, no stun. That asymmetry is the design:
the same round blinds a human and disables an AI, because blinding an AI would mean nothing and
locking a human's controls for five seconds would be intolerable.

### `TANGLER`: mechanical only

The choker sets the engine-dead bit and its timer, and nothing in the AI decision layer reads that
mask. Its readers are in the flight model (`FUN_0048fc40` and the `FUN_004b18a0` group), so a choked
AI is not told it has been choked; it simply flies an aircraft with no thrust. There is no stun, no
state change and no evasive reaction.

### `BEEPER`: nothing at all to the victim

The tag is a world-list entry that seeker rounds query. The victim gets no effect, no state change
and no notification.

### `SMOKE_SCREEN` is a stun trap, not concealment

The smoke object built at launch is **not an occluder** and has nothing to do with line of sight or
targeting. Nothing queries it when picking or tracking a target. What it does, every frame while its
`TIME` runs, is walk the **aircraft list** and test each aircraft that is alive and is not the layer:

    within  smokescreen_stun_range          of the layer, and
    dot( unit(other - layer), layerAxis )  >  cos( smokescreen_stun_angle / 2 )

Anything passing both gets hit, and again the victim's kind decides how. **The player** gets a
`FUN_0042e9d0` wash in a grey-green `(0.2, 0.29, 0.145)` at weight 0.9, or 0.97 on the first hit,
lasting 2 s and re-arming on a 2 s per-victim cooldown, so it keeps re-applying while you stay in it.
**An AI** gets the same `FUN_004200d0` stun as a sonic round, for `smokescreen_stun_interval`
seconds; since the stun leaves the AI in state 4 and `FUN_004200d0` accepts state 4, it is refreshed
every frame the AI remains in the cloud.

The three tunables are **not per-weapon**. `FUN_004735b0`, the `ai_skill_parameters` loader, writes
them from `player.zrd.json`, where their authored names state the mechanism outright:

| Key | Authored | Default if absent | Stored as |
|---|---|---|---|
| `smokescreen_stun_range` | 600 m | 200 m | raw |
| `smokescreen_stun_angle` | 170° | ≈73.7° | `cos(angle × π/180 × 0.5)`, a **half**-angle cosine |
| `smokescreen_stun_interval` | 5 s | — | raw, the AI stun duration |

600 m across a 170° cone is close to "everything behind the layer", which makes the smoker one of the
most powerful weapons in the table rather than a defensive screen. The layer axis is the third row of
the aircraft's world matrix (`+0x198`–`+0x1a0`); its sign was not traced, though the weapon is
`REAR`-firing.

## The choker, settled

The engine-dead duration is not the authored pair read straight off. `FUN_004b9bc0` computes

    duration = ENGINE_DEAD_max * (1 - dSquared / TANGLER_RADIUS),  floored at ENGINE_DEAD_min

and passes it to `FUN_004b1690`, which sets bit `2` of the victim's disabled-systems mask at `+0x2dc`
and raises the timer at `+0x2e0` to that duration if it is longer than what is already running. The
mask's rising edge calls `FUN_004b15c0` and its falling edge `FUN_004b1630`, the engine stop and
restart.

⚠ **The units here do not match, and that is what the code does.** The numerator is a squared
distance from `FUN_00538880`, while `TANGLER`'s `RADIUS` is stored raw by `FUN_004ba6f0` (unlike
`IMPACT_PROXIMITY`, which the other parser squares). So with `wep_12`'s `RADIUS [35]` and
`ENGINE_DEAD [5,13]`, a dead-centre hit kills the engine for 13 s and the term reaches the 5 s floor
at **√21.5 ≈ 4.6 m**, not at 21.5 m. Whether that is a bug in the original or intended, the effective
full-strength zone is a few metres wide.

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

## `TORPEDO` selects a force-feedback effect

Its one reader is `FUN_00480f50`, which drives the Immersion TouchSense force-feedback API
(`CImmCompoundEffect`). Gated on `ROCKET` (`0x10`), so guns are handled elsewhere, it picks one of
three launch effects and gives each its own direction and magnitude:

| Weapon | Effect slot | Direction | Magnitude |
|---|---|---|---|
| `TORPEDO` (`0x08`) | `+0x158` | 0 | 1.0 |
| `REAR` (`0x20000`) | `+0x154` | 180 | 0.58 |
| any other ordnance | `+0x150` | 0 | 0.79 |

So the torpedo is the heaviest thing you can launch and the stick says so, and a rear-firing weapon
kicks from behind. This is the only behaviour `TORPEDO` selects; everything else about the aerial
torpedo comes from its other keys.

⚠ **This corrects an earlier claim on this page that `TORPEDO` had no reader at all.** That claim
rested on three instruction sweeps, and the gap between them was exactly this instruction form:
`TEST <memory>, 0x8` covered memory operands, `AND <memory>, 0x8` and `AND <register>, 0x8` covered
the `AND` forms, and **`TEST <register>, 0x8` was never swept**. The compiler loads the flags dword
into a register here and tests it there. A negative result about a flag is only as good as the
instruction forms behind it, and three sweeps that share a blind spot are not three independent
checks.

### What the torpedo actually does, then

Every part of `wep_14`'s flight is now accounted for by keys that **do** have readers:

| Key | Value | Effect, from the decode above |
|---|---|---|
| `TURN_RATE` | 0.001 | the dumbfire sentinel: guidance runs but turns it essentially not at all |
| `VELOCITY` | 60 m/s | its own cruise speed, and a slow one |
| `ACCELERATION` | 0 | no motor ramp |
| `LOCK_ON` | 2.5 s | inherits the launcher's velocity, decaying linearly to zero over 2.5 s |
| `RANGE_MINIMUM` | 300 m | the one entry carrying it |
| `DETONATION_DISTANCE` | 1 m | fuse trigger distance, and its ticket into the fuse list |
| `IMPACT_PROXIMITY` | 30 m | blast radius |
| `TARGETABLE` + `FLYOUT_HEALTH 10` | | wrapped into the target list, with 10 HP to absorb |
| `DAMAGES_ZEPPELIN` | | restricted to zeppelins at launch, and the gasbag routing gate on impact |

So it leaves the rail at launcher speed plus 60 m/s, flies straight with no gravity drop, and sheds
the inherited component over 2.5 s down to a 60 m/s cruise. An aircraft at 120 m/s launches one that
**halves its speed** across those 2.5 s, which is the slowdown a player at the controls of the
original reports seeing after a torpedo drop.

## What a round collides with

`FUN_005b03f0` advances a round to its next hit and has two modes. If the spawn built a **precomputed
hit list** along the flight path (`+0x608`, the sorted list `FUN_005aef40` fills for a round whose
weapon takes that path), the step just consumes entries in order, comparing each entry's stored
squared distance against the distance travelled so far. Otherwise it runs a live swept query
(`FUN_004c8ec0`) from the round's current position. Either way the result goes to `FUN_005ad330`,
which is the hit resolve already documented in [`weaponImpact.md`](weaponImpact.md), including its
water special case. When both a list entry and a live hit are available, the **nearer** of the two
wins, compared by squared distance.

## Still open

Nothing bearing on ordnance behaviour. `FUN_004c8ec0` and `FUN_004c8f70` (the swept and segment
queries themselves) are geometry-layer routines shared with the collision system rather than weapon
code, and were not opened.
- `FUN_004881e0` is the player's fire-input tick. It routes by `CANNON` (`0x40`) and `ROCKET`
  (`0x10`) only, feeding `CALIBER` to `FUN_004810d0` for guns and the whole weapon to `FUN_00480f50`
  for ordnance.

## Evidence & limits

- `FUN_004ba6f0` was read in full, so the flag map and the struct layout are complete for this build.
- `FUN_004b9bc0` was read in full. It handles hits **on an aircraft**; whether ground objects and
  zeppelins route through the same function is unread, so the "four types deal no damage" finding is
  stated for aircraft targets.
- `FUN_004b6820`, `FUN_004b5fb0`, `FUN_004b9770`, `FUN_005aef40`, `FUN_005af960`, `FUN_005af720`,
  `FUN_005abcf0`, `FUN_00441830`, `FUN_00441780`, `FUN_004b8b50`, `FUN_004b8ad0`, `FUN_004b89c0`,
  `FUN_004b7670`, `FUN_004b1690`, `FUN_005afd50`, `FUN_00480f50`, `FUN_005ac3a0`, `FUN_005ac7a0`,
  `FUN_005ac690`, `FUN_005ac580`, `FUN_005aca30`, `FUN_005acac0`, `FUN_0042e840`, `FUN_004200d0`,
  `FUN_0042e9d0`, `FUN_004b8d50` and `FUN_004b8fd0` were read in full.
- The three `smokescreen_stun_*` values were traced from their authored names in `player.zrd.json`
  through `FUN_004735b0`'s stores to their reads in `FUN_004b8fd0`, including the `× π/180 × 0.5`
  conversion, so the half-angle reading is decoded rather than inferred.
- The claim that no AI code reads the disabled-systems mask rests on an enumeration of every
  instruction referencing `+0x2dc`: twelve sites, all in the flight-model and damage ranges, none in
  the AI decision range. `FUN_0048fc40`, the flight model's reader, was not opened.
  `FUN_004b8b50`'s selection rule was taken from its disassembly rather than its decompilation,
  because the decompiler's rendering of the branch order there is misleading.
- `+0x3c`, `+0x40`, `+0x44` and `+0x48` were traced to their parse sites in `FUN_005ad630` and are
  confirmed, squarings included. `+0x1c` `RANGE`, `+0x38` `ACCELERATION` and `+0x54` `GRAVITY` are
  still named by their use in `FUN_005afd50` rather than traced, though all three agree with the
  value ranges [`formats/weapons.md`](../formats/weapons.md) reports.
- `FUN_00538880` returns a **squared** distance. Every comparison in this page that reads as a
  distance test is a squared-distance test, which is what makes the splash falloff quadratic and the
  `TANGLER` radius mismatch visible.
- The `+0x74` bit assignments and the guidance scalar offsets were read from `FUN_005ad630`'s parse
  sites one key at a time, each confirmed against the key string it is stored beside. `+0x84` is
  computed from `LOCK_ON_LEAD`'s two elements by an expression that was not read.
- `FUN_00538ca0` (bearing), `FUN_0053e56d` (the intercept solve), `FUN_00538d70` (slerp) and
  `FUN_004c7630` (the terrain probe) were not opened; their roles are inferred from arguments and
  from the arithmetic around the call.
- `FUN_0042c070`, `FUN_0048f5e0`, `FUN_004b8ce0`, `FUN_004b15c0` and `FUN_004b1630` were not opened;
  their roles above are inferred from their arguments and call sites, and are labelled as such.
  `FUN_005aef40` **was** opened for the speed-cap seeding, and `FUN_005389a0` with it: it is
  `out = a + b * scale`, which is what makes the motion step's velocity rebuild
  `inherited + heading * speed`.
- Weapon-record offsets `+0x40` (an effect radius), `+0x44` (the fuse trigger distance) and `+0x74`
  (a second flags word) are named by use. Which authored key writes each one was not traced back
  through the `.zrd` parse, so the mapping to `IMPACT_PROXIMITY` and `DETONATION_DISTANCE` is
  inferred from the arithmetic they appear in.
- The globals indexed off `DAT_0064ef78` (the feedback multipliers at `+0x3c`, `+0x68`, `+0x94`,
  `+0x98`, `+0xc0`) were not resolved to values.
