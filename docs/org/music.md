# The music channel, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim below names the function it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side is `extracted/zrdr/sounds.zrd.json`, whose
`SETS`/`SOUND_GROUPS` vocabulary is [`formats/sounds.md`](../formats/sounds.md); the cues are the
campaign missions' `objectives.zrd` `WAKEUP_SOUND_GROUP` directives
([`formats/objectives.md`](../formats/objectives.md)) and the out-of-mission screens' shared sound
object ([`formats/campaign-screens.md`](../formats/campaign-screens.md)). Our own player is
`CSVM/src/Mech3/MusicPlayer.cs`, whose entry in [`architecture.md`](../architecture.md) carries the
plumbing. This page is the original's runtime: what reaches the music channel, which of the six
numbered variants plays, and how the score starts, loops and stops.

## Function map

| Address | Role |
|---|---|
| `FUN_00593590` | The general play entry: routes a sound object to the music channel, to the group picker, or to the ordinary SFX path |
| `FUN_0059ab60` | Group play: picks a member, records its ordinal, and routes a music group to the player |
| `FUN_0059a440` | The `DYNAMIC_WEIGHTS` weighted-random picker, including the recency decay |
| `FUN_00595140` | The streaming music player: the same-track no-op, the hard cut, the loop flag |
| `FUN_005954f0` | The resolver: group or track name to WAV, and the variant choice |
| `FUN_00596ea0` | Registration: binds each loaded music WAV's handle to its slot |
| `FUN_005970c0` | The character-sum key the registration indexes variants with |
| `FUN_00595990` | End-of-stream: restarts the track when the loop flag is set |
| `FUN_00594e80` | Music-subsystem init: zeroes the counters, computes the base-name sums |
| `FUN_0046cc70` | The objectives runtime's sound-group wake: the three `player.zrd`-bound groups' stateful treatment |
| `FUN_0046cd00` | The latched battle-music start |
| `FUN_0046caf0` | Creates a tracked fade record for a cue |
| `FUN_0046cdf0` | Per-tick fade service: the three ramp rates and the record sweep |
| `FUN_0046c930` | Applies a fade record's gain, and stops the sound when a fade-out reaches zero |
| `FUN_0046c870` | The battle timer: start, count down, fade out |
| `FUN_0046c850` | The combat ping: refresh the timer to 20 s |
| `FUN_0046c700` | The proximity scan that pings |
| `FUN_004b9bc0` | The damage handler, whose player branch also pings |

## What reaches the music channel

There is one music channel, and it is a streaming buffer separate from the pooled SFX voices. A
sound reaches it in one of two ways, both decided in `FUN_00593590`:

- a plain `SETS` definition whose flag word carries `MUSIC`, or whose WAV file name starts with the
  two characters `mu`;
- a `SOUND_GROUPS` group, which `FUN_0059ab60` picks a member of first, and which reaches the
  channel when the **group name** starts with `mu` or the group carries `MUSIC`.

Everything else goes to the ordinary positional path. So `MUSIC` in `sounds.zrd` is not a comment:
it is the routing flag, which is why no `SOUND` animation event ever names a `music_*_sg` group.

The channel holds one track. `FUN_00595140` compares the requested WAV name against the name of the
track already streaming and **returns immediately when they match**, so re-cueing the playing track
never restarts it. When they differ it stops the current stream and starts the new one. There is no
crossfade anywhere on this path: track changes are hard cuts.

## Variant choice

Four families ship six numbered tracks each (`prebattle`, `battle`, `battlesuccess`,
`missionsuccess`), and each has a six-member `DYNAMIC_WEIGHTS` group. The number that plays is the
group's own weighted-random pick, not a chapter, an act or a sequence:

1. `FUN_0059a440` draws a member in proportion to its weight and writes the chosen member's ordinal
   to a global.
2. `FUN_0059ab60` copies that ordinal across before calling the player.
3. `FUN_005954f0` maps the group name to a WAV with `ordinal % 6`.

Each family draws independently, so a mission's prebattle number and its battle number are
unrelated; the six tracks are not a matched score set.

**The recency decay, exactly.** `FUN_0059a440` multiplies the chosen member's weight by the group's
`DYNAMIC_WEIGHTS` factor (0.5 in all shipped groups), then floors any member that has decayed below
0.001 back up to 0.001, then renormalizes every live member's weight so the group sums to 100. The
decay therefore **persists and compounds** across picks rather than applying to the next pick only.
⚠ `SoundDefs.SoundGroup.Pick` implements the simpler reading (scale the last pick, no persistence,
no renormalize); the difference is a distribution difference, not a wrong-member difference, and
changing the shared picker would move every existing weighted-sound suite.

**The objective stingers are not random.** The three two-member stinger groups
(`music_primaryobj_sg`, `music_secondaryobj_sg`, `music_tertiaryobj_sg`) are picked by the group
like anything else, and then `FUN_005954f0` **overrides** that pick with a per-family counter,
taking member `counter & 1` and incrementing. The two takes therefore alternate strictly from the
first cue of a process, and the group's weights never reach the speaker.

⚠ An authoring slip sits beside those counters: the handle slot the resolver stores alongside the
WAV name is indexed with the *secondary* family's counter for both the primary and the tertiary
branch, and the tertiary branch indexes it unmasked. Nothing reads that slot except a non-zero
test in `FUN_00595140`, so no cue is audibly affected. Do not reproduce it.

## Looping

`FUN_00595140` takes the loop flag from bit 0 of the definition's flag word, which is the data's
`LOOPED`. `FUN_005954f0` then **forces it on** for `music_battle_sg` and `music_prebattle_sg`.
`FUN_00595990` runs at end of stream and, when the flag is set, plays the same track again.

In the shipped data only `snd_battle1`–`snd_battle6` carry `LOOPED`. Prebattle loops because of the
resolver's override, not because its definitions say so. Every other music track (both success
families, all six stingers, splash, instantaction, spicyairtales) plays once and leaves the channel
silent until the next cue.

## Fades

Fading is not a property of the music channel. It belongs to the objectives runtime's tracked-cue
list, and only three groups are ever tracked: the ones `player.zrd` binds to `pre_battle_sound`,
`in_battle_sound` and `won_battle_sound`. `FUN_0046cc70` gives those three stateful treatment and
sends every other woken group straight to `FUN_00593590` at full gain.

A tracked cue gets a record from `FUN_0046caf0` starting at gain 0 with target 1.
`FUN_0046cdf0` services the list every tick at these rates, in gain units per second:

| Record state | Rate | Time from 0 to 1 |
|---|---|---|
| fade in | +4.0 | 0.25 s |
| fade out | −0.25 | 4 s |
| fast fade out | −4.0 | 0.25 s |

`FUN_0046c930` writes the gain through and, when a fade-out reaches zero, stops the sound and
drops the record. `FUN_0046cdf0` also sweeps records whose sound has finished on its own.

⚠ The shipped `player.zrd` binds only `in_battle_sound`, to `music_battle_sg`; `pre_battle_sound`
and `won_battle_sound` both hold the placeholder literal `your_sound_here`, which resolves to
nothing. **Battle music is the only track in the game that fades.** Everything else starts and stops
at full gain.

## The prebattle-to-battle transition

The binding is data (`in_battle_sound`), but the trigger is code. `FUN_0046c870`, ticked from the
objectives runtime's per-frame tick `FUN_0046a490`, drives a battle timer:

- timer above zero and no battle music playing: start it through `FUN_0046cd00`, which latches one
  handle at a time so a second start cannot stack;
- timer above zero and battle music playing: subtract the frame delta, so the timer runs down only
  while the music is actually heard;
- timer at zero and battle music playing: flip its fade record to the 4 s fade-out.

`FUN_0046c850` refreshes the timer to **20 seconds**, and only ever upward: a ping while more than
20 s is owed changes nothing. Two things ping it:

- **the proximity scan**, `FUN_0046c700`. It runs at most every 5 seconds, and only while battle
  music is silent. It counts other vehicles whose squared distance from the player is under 1e6,
  that is within **1000 m**, and pings when the count is **greater than 2**.
- **player damage**, `FUN_004b9bc0`. Its player branch calls `FUN_0046c830`, which pings when the
  cached nearby count is greater than 1 and battle music is not already playing.

When battle music fades out the channel is silent, not returned to prebattle. A mission's own
`WAKEUP_SOUND_GROUP music_prebattle_sg` objectives are what cue prebattle again.

## Who cues what

Music is a campaign feature. A census of all 53 shipped `objectives.zrd` files finds music cues in
the 24 campaign missions and in none of the 8 Instant Action or 21 multiplayer missions:

| Group | Missions | Cues |
|---|---|---|
| `music_prebattle_sg` | 24 | 37 |
| `music_missionsuccess_sg` | 22 | 22 |
| `music_primaryobj_sg` | 20 | 29 |
| `music_secondaryobj_sg` | 20 | 24 |
| `music_battlesuccess_sg` | 16 | 19 |
| `music_tertiaryobj_sg` | 1 (`C5/M03`) | 2 |

`music_battle_sg` is cued by no mission at all; its only reference in the shipped data is
`player.zrd`'s `in_battle_sound`, and the battle timer above is what starts it.

Out of mission there is exactly one track. `GLOBALS.SCRIPT` creates one sound object holding
`music_splash.wav` with loop count 5, and every out-of-mission screen drives that same object with
mailbox `11003` (stop) and `11004` (start). **The cabin's background music is the splash track**, which
is what `CAP-44` heard; no script assigns the cabin, the briefing or the flight check a track of
its own.

## Named gaps and disproofs

- ⚠ **`music_instantaction.wav` has no trigger.** Its definition `snd_instantaction` is named by no
  mission, no animation definition and no ROF script, and the executable references the WAV name
  only in the resolver and the registration table. Instant Action ships silent.
- ⚠ **`music_spicyairtales.wav` has no trigger either.** The same census result. "Spicy air tales as
  the cabin radio" is disproven: the cabin drives the splash object, and nothing anywhere names
  `snd_spicyairtales`.
- **`music_airtales.wav` is a cut track.** `FUN_005954f0` and `FUN_00596ea0` carry a branch and a
  handle slot for it, but the file is in neither `soundsh` nor `soundsl`.
- **`AUDIO.SCRIPT` names `music_loop.wav`** for the audio options page's preview. That file ships in
  neither sound archive either.
- **`snd_music`** is a three-member `MUSIC` group over `snd_music1`/`2`/`3`, none of which has a
  `SETS` definition and none of which is cued. Undecoded, and not reproducible.
- **What stops music at mission end is not traced.** The success tracks are one-shots, so the
  channel falls silent on its own, but no explicit stop on the return to the cabin was found.
- **The proximity scan's skip predicate is not identified.** `FUN_0046c700` skips a vehicle when a
  virtual call returns true; whether that excludes wrecks, friendlies or neither is open, so
  "more than 2 vehicles within 1000 m" may be narrower than stated.
