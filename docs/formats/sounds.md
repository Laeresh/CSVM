# Sound readers and WAV format

Part of the [format documentation](README.md). This page covers shared sound definitions, player
volume and pitch curves, and the WAV container. The readers are `SoundDefs.cs`, `WavFile.cs`, and
`FlightAudio.cs`.

## Contents

- [Sound sets](#sound-sets)
- [Sound groups](#sound-groups)
- [The music channel](#the-music-channel)
- [Player curves](#player-curves)
- [WAV format](#wav-format)
## Sound sets

`SETS` alternates set-name → list of entries. Entry shape:

```
[snd_name, wavName, FLAG…, KEY, [values], …]
```

- **Bare flags:** `LOOPED` (plays as a forward loop), `3D` (positional), `FREQUENCY`
  (the engine may pitch-shift this sound), `SFX`, `PURGEABLE`, `OPTIONAL`.
- **Valued keys:** `RANGE [fullVolumeDist, audibleDist]` (meters), `VOLUME [gain]`,
  `QUEUE [waitSeconds]`, `QPRIORITY [n]`.

### Which channel a definition plays on

⚠ **`3D` is the positional opt-in, not the default.** Only 124 of the 2,766 shipped definitions
carry it, every one of them also carries `RANGE`, and no definition carries `RANGE` without it. A
definition with neither is not a positional sound with default distances; it has no distance model
at all. The flags sort the whole library into four channels, and the classes do not overlap:

| Class | Marker | Count | Where it plays |
|---|---|---|---|
| Positional | `3D` (+ `RANGE`, `SFX`) | 124 | a point in the world, `WorldSounds` |
| Radio line | `QUEUE [seconds]` | 2,538 | the mission radio queue, no position |
| Music | `MUSIC`, or a `mu`-prefixed WAV or group | 33 | the streaming channel |
| Cockpit and UI | none of the above | 71 | flat: briefings, `*_cp` engine loops, menu clicks |

⚠ **`QUEUE` and `3D` never co-occur**, over all 2,766 definitions. Every mission-VO and
combat-voice definition is a radio line, so a callout is heard the same wherever the player is;
placing one in the world is an invention the data does not support. `QUEUE`'s value is how long
the line may wait for a channel someone else holds before it is dropped as stale: 0.5 s for the
combat barks in `COMMON` and the `id<N>` sets (say it now or not at all), 45 s for a mission's own
VO, 85 s for C5/M04's. The parser (`FUN_00592c90`) reads the key into the definition's wait field
and then **adds 0.3 s**; a definition with no `QUEUE` key keeps that field's initial **5.0 s**.
`QPRIORITY` writes a 3-bit priority (bits 10–12) over the file header's `DEFAULT_QUEUE_PRIORITY 3`,
on three definitions in `c3m05` and nowhere else; ⚠ which direction is more urgent is undecoded, so
nothing reads it. ⚠ The tolerance is a wait rule, not a deadline on the cue's own 1 s start delay:
charging the delay against it drops every 0.5 s bark before it can speak. The cue delay and the
`STOP_QUEUED_SOUNDS` cancellation are [objectives.md](objectives.md).

Beside the effect/UI sets (`COMMON`, the per-mission `c<x>m<nn>` sets, the `brief_*` and `DIALOG`
sets), 35 sets named `id<N>` carry the combat-voice clips: `snd_id<N>_<TYPE>` →
`VO_id<N>_<TYPE>.wav`, 1,414 defs. Their runtime chain and caveats (defs without WAVs, the
announcer id without defs) are [combat-voice.md](combat-voice.md); the reader is
`CSVM/src/Mech3/CombatVoice.cs`.

⚠ **An aircraft's weapon cues are in the positional class, not the cockpit one.** Every caliber's
`LOOPED_SOUND_NAME` (`snd_30cal` through `snd_70cal`, plus `snd_turretgun` and `snd_chaingun`)
carries `3D` + `LOOPED` + `RANGE`, and so does the `NO_AMMO_WARNING` cue `snd_emptyclip`. So a gun
loop is a point in the world, which is what the remake plays it as: the pilot's own guns stay flat
because that is what the pilot hears, and every other aircraft's come from its own position, culled
at the definition's own audible distance. Those distances are short, 150 m for a 30-cal against 800 m
for the dry cue, so traffic firing 300 m off is inaudible by the data's own numbers. ⚠ Nothing here
authorises a pitch term: `RANGE` is a gain model, and `CAP-09` measured no Doppler on the original's
world emitters at all.

Each plane def names its own engine loop via `engine_sound` / `cockpit_engine_sound`
(see [vehicle.md](vehicle.md)) — e.g. `engine_sound snd_bloodhawkengine` → bloodhawk.wav.
`damaged_engine_sound` is an array of swap candidates for that same slot (`snd_damagedengine`
install-wide), not a second loop blended over it; the entry's two floats are a pitch-multiplier
range drawn once per swap. See vehicle.md.

## Sound groups

A sibling of `SETS`: the weighted random sound groups a one-shot `SOUND` animation event resolves
through when it names a group instead of a plain `snd_*` definition. The combat/destruction
one-shots go through these — `air_mixed_exp_sg`, `ground_mixed_exp_sg`, `plane_destroy_sg`,
`bullet_hit_sg`, `bullet_warning_sg`, `window_hit_sg`. Parsed by `SoundDefs.LoadGroups`.

Entry shapes (all start with the group name):

```
[name, "DYNAMIC_WEIGHTS", factor, [member], [member], …]          -- uniform members + recency
[name, "MUSIC", "DYNAMIC_WEIGHTS", factor, [member], …]           -- as above; a music category
[name, [member, "WEIGHT", w], [member, "WEIGHT", w], …]           -- explicit per-member weight
[name, [dialogueRoot, [line], [line], …]]                         -- a VO chain, NOT a weighted group
```

- **`DYNAMIC_WEIGHTS factor`** — the `factor` (0.5 everywhere it appears) is a **recency scalar**:
  the member returned last has its weight multiplied by it on the next pick, so the same clip is
  less likely to repeat back-to-back. Members are single-element lists, each weight 1.
- **Explicit `WEIGHT`** — `snd_plane_die`/`snd_plane_dmg` give each member its own weight and no
  recency decay; their `snd_nothing` at 0.7 is a 70% chance of silence.
- **VO dialogue chains** (`snd_assignments`, `snd_HI1*`; 222 groups) nest a list where a weighted
  member's name would be: `[firstLine, [line], [line], …]`, an ordered sequence of snd names.
  They contribute no weighted member; the parser keeps them as `SoundGroup.Chains`, `MissionRadio`
  speaks them in order as one radio call, and `WorldSounds.Prewarm` decodes their lines. No chain
  group mixes chains with weighted members, and each holds exactly one chain (2–13 lines). Every
  line of every chain an objective cues is a `QUEUE` definition; not one is `3D`. A group with neither
  members nor chains is not registered. Music `*_sg` groups parse but no `SOUND` event names them:
  music is cued by name from the missions' `objectives.zrd` and from the menu scripts, not by an
  animation event (see [The music channel](#the-music-channel)).
- **Combat-voice variant groups**: 466 entries named `snd_<FAMILY>-A_id<N>_random`
  (`DYNAMIC_WEIGHTS 0.5` over one pilot's `-A/-B/-C` takes of one family), the data's own answer
  to how a take is picked; see [combat-voice.md](combat-voice.md).

A `SOUND` event's NAME is resolved against `SETS` first, then `SOUND_GROUPS`: `air_mixed_exp_sg`
picks one of `snd_exp_hit1/2/3/3a/5`, each of which is an ordinary `SETS` entry
(`snd_exp_hit1` → `explosion_1.wav`). See [anim-definitions.md](anim-definitions.md) for the
`SOUND` vs `SOUND_NODE` distinction (only the latter is ambient looping world audio).

## The music channel

`MUSIC` is a routing flag, not a label. A `SETS` definition carrying it, or one whose WAV name
starts with `mu`, plays on a single streaming channel; so does a `SOUND_GROUPS` group whose name
starts with `mu`. ⚠ It selects the streaming channel and nothing else: what a definition without
it plays on is decided by `3D` and `QUEUE`, not by the absence of `MUSIC`
([which channel a definition plays on](#which-channel-a-definition-plays-on)). The 33 `music_*.wav` tracks in
`soundsh`/`soundsl` are reached through seven groups (`music_prebattle_sg`, `music_battle_sg`,
`music_battlesuccess_sg`, `music_missionsuccess_sg` and the three `*obj_sg` stingers) plus the
plain definitions `snd_music_splash`, `snd_instantaction` and `snd_spicyairtales`.

The channel holds one track: a new cue hard-cuts the old one, a cue for the track already playing
is ignored, and nothing crossfades. Which of the six numbered variants plays is the group's own
weighted-random pick, not a chapter or an act; the two-take stingers alternate instead. Only
`battle1-6` carry `LOOPED`, and the resolver forces prebattle to loop as well.

The runtime behind all of that, including the battle timer, the fade rates and which tracks ship
with no trigger at all, is [`org/music.md`](../org/music.md).

## Player curves

All are clamped two-point ramps `(inStart→inEnd maps outStart→outEnd)`:

| Block | Meaning |
|---|---|
| `engine_sound` | pitch 0.6→1.0 over throttle 0.1→1.0; volume flat 1.0 |
| `prop_sound` | the **overspeed dive whine**: volume 0→0.5 over speed 1.0→1.1× `fd_speed`, pitch 0.65→1.25 over 1.0→1.2×. Drives the engine audio's second slot, whose definition is the vehicle def's own `prop_sound` key |
| `rattle` | `snd_planeshake`: volume 0→1 over speed 1.0→1.2× `fd_speed` |

⚠ **The whine never sounds in the retail install, and `snd_enginewhine` is not its WAV.** The
second slot's definition comes from the vehicle def's `prop_sound` string key; no shipped def
authors it, the field has no compiled default, and `snd_enginewhine` appears as a literal nowhere
in `crimson.exe`. The curves above are still read, and would drive the slot if a def ever named
one. A spectral comb in a reference dive recording was previously read as the whine mixed 12–18 dB
under the engine; that reading is refuted. What moves the engine note in a dive is the engine
slot's own **manoeuvre and attitude** terms, decoded in
[vehicle.md](vehicle.md#the-engine-slots-pitch-and-gain-are-not-throttle-alone). ⚠ Not a
speed-driven term, which this page said before the expression was read: the quantity is angular
velocity, and airspeed reaches the engine slot nowhere.

## WAV format

All game WAVs (`soundsh.zbd` high-quality / `soundsl.zbd` low) are **MS ADPCM**
(fmt tag 2, 4-bit, 22050 Hz, mono or stereo). Godot only loads PCM/IMA-ADPCM/QOA, hence
the pure-C# decoder in `WavFile.cs`.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
