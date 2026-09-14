# Gun fire, decoded from `crimson.exe`: how the original emits rounds, and how it answers taking them

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-18, settling the fire-rate half of backlog
`BL-266`(a). Every claim names the function or address it came from. No decompiler output is
reproduced; addresses are given so any claim can be re-checked at source.

**The question this page answers.** The engine fires the player's guns *one selected slot at a
time* at the authored `FIRE_RATE` (8.0 for `wep_40`), alternating its two muzzles. A clip of the
original firing 40-cal slugs gave a counter-derived "~12–13 rounds/s", which raised the worry
that the original either fires two gun groups at once or double-rate. This page decodes the true
round-emission model from the binary. Answer up front: **the original fires exactly one round per
fire-tick from a single selected gun group at the authored `FIRE_RATE`; the engine's model is
correct and is not under-firing by rate.** The clip's higher number was a redraw-window artifact.

## One selected gun group, per-plane weapon defs

The plane object (`DAT_0071c298` for the player) carries **four gun groups**, each described by a
weapon def pointer stored at `plane + 0x4f4 + group·0xc`, plus a parallel **ammo/cooldown counter**
at `plane + (group·3 + 0x13e)·4` (= byte offset `0x138 + 12·group`). Only **one** group is the
*gold* armament at a time, selected by the single index `plane + 0x604`:

- Arming/switching: `FUN_004b20b0(plane, def)` writes the chosen def to `plane + 0x950` (the
  armed-def pointer) and, for the player, immediately runs the fire routine `FUN_004540c0`.
  `FUN_004b2550` arms a group wholesale: it stores the def at `+0x4f4 + group·0xc` and sets that
  group's counter at `+0x138 + 12·group` to an ammo count (`param_4`, from `FUN_004bad90`).
- Selection helpers read only `plane + 0x604`: `FUN_004b3470` advances through groups to pick the
  one that can fire, and `FUN_004b34f0` drives the "fire when a ready group exists" logic using
  the same single `+0x604` index.

There is no second "active gun" state, four groups, one armed at a time, one `+0x604` selector.
That is the engine's "one gun slot at a time" model, verbatim.

## Nothing re-arms in flight outside the multiplayer rearm base

`MSG_AMMO_PICKUP` (message id 128, "1 round of %1") and `MSG_AMMO_PICKUPS` (id 129, "%1!d! rounds
of %2") are dead text: no code path in the retail build consumes either id. Messages reach the
screen through `FUN_0059cd90` (`LoadStringA` then `FormatMessageA`), its varargs wrapper
`FUN_0059cd70` and the id-only wrapper `FUN_0059ce40`, and every instruction in the image carrying
the immediate `0x80` or `0x81` is a buffer size, a stack adjustment, a mask, an assert line number
or a `REP` count, with no id table holding the pair either; the same scan does find live ids
(`PUSH 0x84` at `0049e23c` formats id 132, and the multiplayer flag builder `FUN_00495d40` formats
7056 to 7061), so the absence is the finding rather than the method. The shipped data agrees: no
reader, node tree or `.gw` script names an ammo, supply or crate pickup, and the four `pickups.zrd`
files are campaign person rescues. What does re-arm is a fixed multiplayer base. `FUN_0049b970`
walks the rearm-node list (`DAT_0071c7c4`), compares the player's distance against `_DAT_00628f10`
(624.0 by default, written from the config keys read at `00473f8c` and `0043f33b`), and on entry
calls `FUN_00480480`, which restores health and armour (`FUN_004b80a0` and `FUN_004b8180`, from the
stored maxima at `plane + 0x2c4` and `plane + 0x2cc`) and re-applies the entire loadout through
`FUN_004b24d0`, calling `FUN_004b2550(plane, slot, def, -1, flag)` for all four gun groups and all
eight pylons, where `-1` means the def's own full count from `FUN_004bad90`. A rearm therefore
restores everything at once, never one round and never one weapon; the caller preserves the
selected group (`plane + 0x604`) and pylon (`plane + 0x608`) across the reset, latches on
`DAT_0071d23c` so it fires once per entry into the radius, requires the base to belong to the
player's own team in the team modes, and announces with id 7077 (`MSG_MP_REARMED`, "Rearmed!") at
`0049ba99` and `0049bbb9`. The nodes (`rabase1`, `rabase2`, `racmplx`, `rabaset1`, `ammosign`,
`rearm_node_1`, `rearm_node_2`, and `zep_rearm_node_1` / `zep_rearm_node_2` on the zeppelin map)
exist only in the multiplayer maps, and the `.gw` scripts of every campaign mission and of Instant
Action switch them off.

## The fire tick spawns exactly ONE round

The per-frame fire update `FUN_004897c0` (the main weapon/AI tick) calls, for the player plane:

```
FUN_004881e0(plane)   // fire-tick: spawn a round if the group is ready
FUN_004b34f0()        // auto-fire selection / cooldown gate
FUN_004b3e50()        // muzzle-flash / recoil timers
```

`FUN_004881e0` reads the trigger (input slot `0x13`, fire bits `0x3/0x4`), reconciles the armed
def (`+0x950`) with the selected group's def (re-arming if they differ, `FUN_004b20b0`), and then
spawns a round: a **single** `FUN_00498850` call (disassembly `00488440`) and a **single** pass
through the fire routine `FUN_004b6820` (`0048837e`, and `00489c0e`/`00489c2d` for the arm the
trigger half raises). There is **no loop over barrels and no salvofire**, so one round leaves per
fire-tick. The interval is gated by the group's counter, decremented
elsewhere until it drops below the fire threshold seen in `FUN_004b34f0`
(`plane + (group·3 + 0x13e)·4 < 1`).

Consequently the effective rounds per second equals the authored `FIRE_RATE`, the same data the
engine already reads (8.0 for `wep_40`). The two muzzle slots are visual/aim, not a doubled rate.

## The firing sound plays from the aircraft's own position, not from a muzzle

The fire tick's two sound plays both go through `FUN_0045e470(slot, key, def, ttl, position,
velocity)`, the keyed 3D loop: it starts the definition through the general play entry
`FUN_00593590` if the keyed slot holds no live voice, then sets that voice's world position and
velocity through `FUN_00597af0` (`zSound/zsnd_3d.c`) and stamps an expiry of `now + ttl`, so the
voice dies unless the tick refreshes it again.

**The position argument is the vehicle's own origin.** Both calls push `plane + 0x204`
(`004884a9` for the first, the same LEA for the second), which is the aircraft's world position
`x`/`y`/`z` at `+0x204`/`+0x208`/`+0x20c`. There is no barrel slot, muzzle marker or forward offset
anywhere on the path: the `plane + 0x3a4 + i·0x24` barrel array the aim assist and the muzzle
flashes read is never consulted for the sound. A firing emitter placed at the muzzle in a port
would be an invention, and against the 20 to 40 m full-volume radius the shipped gun cues author
it would be inaudible as a difference anyway.

The two plays are:

| Cue | Definition | `ttl` | Gate |
| --- | --- | --- | --- |
| the caliber's firing loop | the armed weapon record's own slot at `plane + 0x950`, the data's `LOOPED_SOUND_NAME` | 0, so it lives one tick and the next tick renews it | the armed weapon has one |
| `cannon_sound` | the vehicle def's `+0x180`, resolved by name at vehicle load (`0047a7fb`–`0047a81c`) | 0.1 s | the weapon carries `CANNON` (flag bit `0x40`) |

⚠ **No shipped vehicle def authors `cannon_sound`**, so `+0x180` is null install-wide and the
second play never fires. One firing loop is heard, the caliber's own.

⚠ **The dry-trigger cue is not positional at all.** `NO_AMMO_WARNING` is resolved once at
`wep.ini` load into a global (`FUN_005ad630` at `005ad71e`) and played by `FUN_005ac560`, which
calls `FUN_00593590` with no position argument. `FUN_005936e0` takes that null as "2D" and puts
the voice in the head-relative mode, so the cue is flat even though its definition carries `3D` and
a `RANGE`. The play site is the fire routine's out-of-ammo arm and is not owner-gated, so a
non-player aircraft running dry sounds it flat as well.

## The vehicle gun's voice is renewed per tick, and the renewal is ahead of the refire timer

The keyed loop above is started from the fire DECISION, never from the shot routine, and the refire
timer sits between the two. `FUN_004b6820` plays no firing sound at all: its only audio call is the
dry-trigger arm. Every vehicle's loop comes from whichever routine set its trigger byte.

- The player's own gun: `FUN_004881e0`, in both of its arms (the selected group and the held
  trigger), at a `ttl` of 0 each time.
- Every other vehicle, an AI aeroplane and a `mode ship` hull alike: the AI fire decision
  `FUN_0041f420`, which a patrol boat and a turret truck reach from the per-frame update
  `FUN_0041c270` ([`aiPilot.md`](aiPilot.md), "What a `mode ship` vehicle runs").

In `FUN_0041f420` the play sits between the slot's range window and its refire timer:

| Address | Step |
|---|---|
| `0x0041f604` | the slot's rounds `+0x08` must be above zero |
| `0x0041f68a`, `0x0041f69b` | squared separation inside the slot's own `[+0x18, +0x1c]` window |
| `0x0041f6d2` | the slot's looped-sound handle `+0x20`; null skips this play and nothing else |
| `0x0041f6ea`, `0x0041f6ee`, `0x0041f6f5` | the key is the slot's own `+0x24`, the position the VEHICLE origin `+0x204`, and the `ttl` the literal zero pushed here |
| `0x0041f6fd` | the keyed 3D loop `FUN_0045e470` |
| `0x0041f749` | only now the refire test, game time against the slot's `+0x10` |
| `0x0041f75b` | and only inside that arm the trigger byte `+0x2c`, which is what makes a round leave |

So the lease is **zero, renewed every tick that selects the gun**, which is the aircraft slot's
reading exactly, and the renewal is **ahead of the refire timer**. A gun whose authored refire
interval is 0.3 s therefore sounds continuously while it holds a target inside its window rather
than in 0.3 s bursts. The renewal is ahead of the shot routine's aim-quality gate as well, so a
mount still slewing onto the lead is already sounding. What ends it is always the same event, a pass
that does not reach the play: an ammo, class or range gate above it closing, the target being
dropped, or the update not calling the decision at all, which it does only while a mount reports a
firing solution.

⚠ **A zero lease is one tick of life, not none.** `FUN_0045e470` stamps the expiry as the sound
clock plus the `ttl`, and the release `FUN_0045e360` drops the handle only once that expiry is
STRICTLY below the sound clock, so a voice renewed this tick survives this tick's release pass and
dies on the next one. A build that reads the zero as "stop at once" silences every gun in the
install.

⚠ **A hull's voice is its weapon's, never a `SOUNDS.CANNON` lease.** The turret fire routine
`FUN_004aabb0` keys its slot on the turret entry's `SOUNDS.CANNON` at a 0.5 s lease
([`../formats/turrets.md`](../formats/turrets.md)); this path reads no turret entry at all. Both
shipped surface defs carry one `wep_29`, whose `LOOPED_SOUND_NAME` is `snd_turretgun`, and
`t_truck`'s `ai.zrd` emplacement entry is a second, static gun on the same model which authors no
`SOUNDS` block.

The second play in the same block (`0x0041f741`) is the `cannon_sound` one: the vehicle def's
`+0x180` at a 0.1 s lease, keyed on the vehicle's own `+0x274` rather than the slot's, and gated on
the weapon's `CANNON` flag. No shipped vehicle def authors `cannon_sound`, so it never fires here
either.

## The two barrels are muzzle-flash / assist, not a rate doubler

Each gun group still has two barrel slots:
`FUN_004b3e50` iterates **4 groups × 2 barrels** at `plane + 0x3b8 + i·0x24` (the same
`+0x3a4 + i·0x24` slot array the aim-assist half reads for its "two barrel slots",
[`aim-assist.md`](aim-assist.md)). It updates muzzle-flash/recoil timers
(`_DAT_0071c45c + counter < DAT_0071c470` → reset), visuals for a two-muzzle gun, matching the
engine's alternating muzzles at the *same total* rate. They do not emit a second projectile.

## Why the clip read "~12–13/s" and why that is wrong

The clip's counter dropped **46 rounds** (2371→2325). At the true `FIRE_RATE` 8.0/s that is a
~5.75 s burst. The motion-redraw window the earlier analysis used to time the burst only captured
~4.5 s (frames 113–247 at 30 fps), the redraw detector under-bounds the firing, and dividing the
46-counter by shorter windows inflates the rate (46/4.5 ≈ 10.2; the "12–13" figure came from an even
narrower window). This is the same redraw-artifact trap `FINDINGS.md` already flagged. The engine's
8.0/s is the correct rate; there is no rate shortfall to fix.

## Implication for the gun-rattle amplitude (`BL-266`(a))

With rate ruled out, BL-266's remaining amplitude avenue, the "(B) 60 fps pose-interpolation"
loss, is also ruled out and replaced by a **decode, clip-independent** finding: the shake pivot
is written once per 60 Hz physics tick and Godot auto physics interpolation is OFF, so the 15 Hz
sawtooth renders stepped with no smoothing loss. After that, what the engine renders is fully
determined by decoded constants + the oscillator's own math: a kick of envelope `E` renders
**~0.28× E as RMS** at 8/s (sawtooth duty × damp decay), so the law's literal number (2.80e-3
kick) renders as **~8e-4 rad RMS = ~0.20 px/frame** at the ±205 px lever. The engine reads under
the law's literal number **by construction**, not by a pipeline loss;
`magnitude_factor` was decoded as a kick amplitude but the rendered output is always ~0.28× a
kick. Whether the look should be ~3.5× stronger to match the original is the clip/fidelity
judgment, not the decode. Full reconciliation:
`analysis/gun-wobble-shake/FINDINGS.md`.

## The incoming-fire cues

Three `SOUND_GROUPS` entries answer being shot at: `bullet_warning_sg` (`snd_bulletpass1-3`),
`bullet_hit_sg` (`snd_ricochet1-4`) and `window_hit_sg` (`snd_windowhit1-3`). `player.json` names
only the first two, as `warning_shot_sound` and `bullet_hit_sound`; the loader resolves each to a
play handle at `0x00474702` (`DAT_0071c488`) and `0x00474730` (`DAT_0071c48c`) inside the player
globals reader `FUN_004735b0`, leaving the handle null when the key is absent.

**Both are played flat, without a position.** The only read of either handle is in the damage
routine `FUN_004b9bc0`, at `0x004b9ea9` and `0x004b9ec9`, and both go through
`FUN_00593590(handle, 1.0)`, whose group arm `FUN_0059ab40` passes a null position and a null
velocity down to `FUN_0059ab60`, which then clears the positional bit. So `snd_ricochet1-4`'s own
`3D` + `RANGE [20, 200]` never reaches a distance model on this path. The member is a weighted
random draw with the `DYNAMIC_WEIGHTS` recency scalar applied to the pick and the weights
renormalised afterwards (`FUN_0059a440`), which is the draw every group share.

**The dispatch gate is a CANNON round on the player's own vehicle.** `FUN_004b9bc0` tests the
weapon's flag word for `0x40` at `0x004b9e7e` and the struck vehicle against the player global
`DAT_0071c298`, and only inside that arm does either cue sound. Nothing on the vehicle-contact path
plays them.

⚠ **`window_hit_sg` is not view-gated.** It is authored in the five `bullet1`..`bullet5` defs of
`cockpit_bulletholes.zrd` rather than in `player.json`, three or four `SOUND` events apiece across
the def's ~0.91 s timeline, and those events sit in the def's SECOND sequence. The
`IF PLAYER_1ST_PERSON` branch is in the FIRST sequence and chooses only the visual, the cockpit
glass holes against the exterior `two_bulletholes_*` / `three_bulletholes_*` / `four_bulletholes`
call. A build that silences the glass outside the cockpit is inventing a gate the data does not
author.

**Which interval opens a hole.** The tick `FUN_004b1340` is the `warning_shot_*` block's own:
`+0x914` ages against `warning_shot_interval`, `+0x918` counts the CANNON hits taken since the
last close (incremented at `0x004b9e83`), `+0x910` is the intensity against `warning_shot_max`, and
`+0x91c` is the shield flag the next section is about. An interval that closes with a non-zero hit count calls
`FUN_004b1210`, which walks the vehicle def's `bullethole_anims` vector at `def + 0x1b8`, each entry
an 8-byte `{anim handle, used byte}` pair, and:

| Step | Address | Rule |
|---|---|---|
| health gate | `0x004b1219`–`0x004b1292` | proceed only while `health/maxHealth` is **below** `unused/total` |
| chance | `0x004b12ae` | proceed only while `rand()/32768` exceeds **0.7** |
| pick | `0x004b12c5`–`0x004b12d4` | `ftol(rand()/32768 · (unused − 1))` indexes the unused entries |
| play | `0x004b1317` | starts that `ON_CALL` def, then marks its entry used at `0x004b1328` |

So a pristine airframe never opens a hole, each hole raises the bar for the next, and a hole opens
once per sortie until the `reset_bulletholes` spawn anim clears the flags. ⚠ The span of the pick is
the unused count **less one**, so the last unused entry is reachable only on a maximal draw; that is
the shipped arithmetic, not a transcription slip.

## The `warning_shot_*` block is a shield on the player, and `bullet_warning_sg` is its cue

The same tick carries the damage rule the two cues choose between. `+0x91c` is not a rating: it is
a flag meaning "gun damage on this aeroplane is discarded", and the vehicle constructor writes it
SET (`0x004b032b`, beside the three zeroes at `0x004b0319`-`0x004b0325`). In `FUN_004b9bc0`, once
the round is a CANNON round on the player's own vehicle, the arm reads in this order:

| Address | Step |
|---|---|
| `0x004b9e83` | `+0x918` (the hit count the hole cadence runs on) is incremented FIRST, so an absorbed round still counts |
| `0x004b9e89` | the struck vehicle against the player global `DAT_0071c298` |
| `0x004b9e91` | the shield flag `+0x91c` |
| `0x004b9e9b` | `*DAT_0064f750`, which must read zero |
| `0x004b9ea4`-`0x004b9ea7` | both floats of the incoming damage pair are written zero |
| `0x004b9ea9` | `warning_shot_sound` plays and the routine RETURNS, so nothing below it runs |
| `0x004b9ec9` | the other way out of the same test: `bullet_hit_sound`, then the damage is spent |

So the two cues are exclusive per round, and the pass cue means the round did nothing. The return
skips the whole remainder of the routine: the armour/health spend, the per-part loop, the kill test,
and the hit-direction indicator at the end. It does not skip the camera shake, which the player arm
kicks earlier at `0x004b9d26`, so an absorbed round still rocks the aeroplane.

**What fills the shield.** The tick's own arithmetic, on the shipped `max` 2.0, `interval` 1.0 and
`dissipation` 2.0:

| Address | Rule |
|---|---|
| `0x004b1355`-`0x004b137e` | `+0x914` accrues the frame delta; nothing happens until it reaches `warning_shot_interval` |
| `0x004b138c`-`0x004b1396` | an interval that closed with a hit adds its own ELAPSED LENGTH to `+0x910`, not the hit count |
| `0x004b13a2`-`0x004b13b5` | at `warning_shot_max` the intensity is clamped there and the shield flag is CLEARED |
| `0x004b13e5`-`0x004b13f5` | a quiet interval subtracts `elapsed · coefficient` instead |
| `0x004b1406`-`0x004b1422` | below `max` again the flag is SET, and the intensity floors at zero |

The coefficient is not the authored `warning_shot_dissipation`: the loader divides 1.0 by what it
read (`0x0047469f`-`0x004746ad`, default 2.0 at `0x006076b8`), so the shipped 2.0 sheds 0.5 per
second of quiet. In flight that reads as: about two seconds of sustained gunfire land on the player
for free, the rounds sounding `snd_bulletpass1-3` as though they had missed, and only then do they
tell; one quiet interval re-arms the shield, and four give it its whole charge back.

**It is single-player, and the player alone.** The world tick calls `FUN_004b1340` at `0x0048985f`
only for `DAT_0071c298` and only while `*DAT_0064f750` reads zero (`0x00489854`). That global is
the `Network` setting: `FUN_0043fb50` binds it at `0x00440247` from the config key `Network`
(`0x006237f4`), beside `NetListen` and `MPFuel`, and writes 0 through `FUN_00440ab0`. Its only
other writers are the session paths: `FUN_004a827f` sets it as a network session is entered, and
`FUN_00496cf0` and `FUN_00440460` clear it again when one ends. So in a network game the
accumulator never ticks at all, which is why the
damage arm tests the same global a second time: without it, `+0x91c`'s constructor value would
leave a networked player permanently invulnerable.

Nothing else reads the block. `+0x910`, `+0x918` and `+0x91c` have no readers outside the
constructor, this tick and this arm, so the intensity feeds no HUD and no AI decision.

⚠ **There is no near-miss geometry anywhere in the image.** `DAT_0071c488` has exactly one read,
`0x004b9ea9` above, and the projectile module has none of the player global's 640 references (every
one lies between `0x0041xxxx` and `0x004cxxxx`), so the round stepper `FUN_005af720` cannot measure
a pass distance against the player even in principle. A build that sounds `bullet_warning_sg` on a
round flying past is inventing the event.
