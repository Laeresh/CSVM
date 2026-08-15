# Sound readers and WAV format

Part of the [format documentation](README.md). This page covers shared sound definitions, player
volume and pitch curves, and the WAV container. The readers are `SoundDefs.cs`, `WavFile.cs`, and
`FlightAudio.cs`.

## Contents

- [Sound sets](#sound-sets)
- [Sound groups](#sound-groups)
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
  `QUEUE [...]`.

Beside the effect/UI sets (`COMMON`, the per-mission `c<x>m<nn>` sets, the `brief_*` and `DIALOG`
sets), 35 sets named `id<N>` carry the combat-voice clips: `snd_id<N>_<TYPE>` →
`VO_id<N>_<TYPE>.wav`, 1,414 defs. Their runtime chain and caveats (defs without WAVs, the
announcer id without defs) are [combat-voice.md](combat-voice.md); the reader is
`CSVM/src/Mech3/CombatVoice.cs`.

Each plane def names its own engine loop via `engine_sound` / `cockpit_engine_sound`
(see [vehicle.md](vehicle.md)) — e.g. `engine_sound snd_bloodhawkengine` → bloodhawk.wav.
`damaged_engine_sound` is a second, vehicle.json-only loop (`snd_damagedengine`) blended in as
damage accumulates — its own two-float shape, not a `player.json` curve block; see vehicle.md.

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
  They contribute no weighted member; the parser keeps them as `SoundGroup.Chains` for the
  comms/mission layer, and `WorldSounds.Prewarm` decodes their lines. No chain group mixes chains
  with weighted members, and each holds exactly one chain (2–13 lines). A group with neither
  members nor chains is not registered. Music `*_sg` groups parse but no `SOUND` event names them
  (music is triggered elsewhere).
- **Combat-voice variant groups**: 466 entries named `snd_<FAMILY>-A_id<N>_random`
  (`DYNAMIC_WEIGHTS 0.5` over one pilot's `-A/-B/-C` takes of one family), the data's own answer
  to how a take is picked; see [combat-voice.md](combat-voice.md).

A `SOUND` event's NAME is resolved against `SETS` first, then `SOUND_GROUPS`: `air_mixed_exp_sg`
picks one of `snd_exp_hit1/2/3/3a/5`, each of which is an ordinary `SETS` entry
(`snd_exp_hit1` → `explosion_1.wav`). See [anim-definitions.md](anim-definitions.md) for the
`SOUND` vs `SOUND_NODE` distinction (only the latter is ambient looping world audio).

## Player curves

All are clamped two-point ramps `(inStart→inEnd maps outStart→outEnd)`:

| Block | Meaning |
|---|---|
| `engine_sound` | pitch 0.6→1.0 over throttle 0.1→1.0; volume flat 1.0 |
| `prop_sound` | the **overspeed dive whine**: volume 0→0.5 over speed 1.0→1.1× `fd_speed`, pitch 0.65→1.25 over 1.0→1.2×. The WAV is not named anywhere in the readers (the remake uses `snd_enginewhine`, the only pitch-shiftable candidate — identified by an all-archive comb sweep against a reference recording) |
| `rattle` | `snd_planeshake`: volume 0→1 over speed 1.0→1.2× `fd_speed` |

**Caveat — the curve volume is not a linear mix amplitude.** Spectral analysis of a
reference dive recording shows the original plays the whine 12–18 dB below
what `prop_sound`'s 0.5 volume cap would give as a linear gain against the engine loop —
the engine applies scaling of its own between the curve value and the mixer. Treat these
volume numbers as relative shapes, not absolute amplitudes.

## WAV format

All game WAVs (`soundsh.zbd` high-quality / `soundsl.zbd` low) are **MS ADPCM**
(fmt tag 2, 4-bit, 22050 Hz, mono or stereo). Godot only loads PCM/IMA-ADPCM/QOA, hence
the pure-C# decoder in `WavFile.cs`.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
