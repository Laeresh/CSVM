# Sounds: `sounds.json` SETS, player curves, WAV format

Part of the [format documentation](README.md). Covers the shared zrdr archive's sound
definitions and curve blocks, and the audio container format. Consumed by
`CSVM/src/Mech3/SoundDefs.cs`, `WavFile.cs`, `src/Flight/FlightAudio.cs`.

## `sounds.json` — the SETS block

`SETS` alternates set-name → list of entries. Entry shape:

```
[snd_name, wavName, FLAG…, KEY, [values], …]
```

- **Bare flags:** `LOOPED` (plays as a forward loop), `3D` (positional), `FREQUENCY`
  (the engine may pitch-shift this sound), `SFX`, `PURGEABLE`, `OPTIONAL`.
- **Valued keys:** `RANGE [fullVolumeDist, audibleDist]` (meters), `VOLUME [gain]`,
  `QUEUE [...]`.

Each plane def names its own engine loop via `engine_sound` / `cockpit_engine_sound`
(see [vehicle.md](vehicle.md)) — e.g. `engine_sound snd_bloodhawkengine` → bloodhawk.wav.
`damaged_engine_sound` is a second, vehicle.json-only loop (`snd_damagedengine`) blended in as
damage accumulates — its own two-float shape, not a `player.json` curve block; see vehicle.md.

## `sounds.json` — the SOUND_GROUPS block

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
- **VO dialogue chains** (`snd_assignments`, `snd_HI1*`) nest a list where a weighted member's name
  would be. They contribute no weighted member and are skipped; a group left with none is not
  registered. Music `*_sg` groups parse but no `SOUND` event names them (music is triggered
  elsewhere).

A `SOUND` event's NAME is resolved against `SETS` first, then `SOUND_GROUPS`: `air_mixed_exp_sg`
picks one of `snd_exp_hit1/2/3/3a/5`, each of which is an ordinary `SETS` entry
(`snd_exp_hit1` → `explosion_1.wav`). See [anim-definitions.md](anim-definitions.md) for the
`SOUND` vs `SOUND_NODE` distinction (only the latter is ambient looping world audio).

## `player.json` — volume/pitch curve blocks

All are clamped two-point ramps `(inStart→inEnd maps outStart→outEnd)`:

| Block | Meaning |
|---|---|
| `engine_sound` | pitch 0.6→1.0 over throttle 0.1→1.0; volume flat 1.0 |
| `prop_sound` | the **overspeed dive whine**: volume 0→0.5 over speed 1.0→1.1× `fd_speed`, pitch 0.65→1.25 over 1.0→1.2×. The WAV is not named anywhere in the readers (the remake uses `snd_enginewhine`, the only pitch-shiftable candidate — re-confirmed by an all-archive comb sweep against a reference recording, 2026-07-19) |
| `rattle` | `snd_planeshake`: volume 0→1 over speed 1.0→1.2× `fd_speed` |

**Caveat — the curve volume is not a linear mix amplitude.** Spectral analysis of a
reference dive recording (2026-07-19) shows the original plays the whine 12–18 dB below
what `prop_sound`'s 0.5 volume cap would give as a linear gain against the engine loop —
the engine applies scaling of its own between the curve value and the mixer. Treat these
volume numbers as relative shapes, not absolute amplitudes.

## WAV format

All game WAVs (`soundsh.zbd` high-quality / `soundsl.zbd` low) are **MS ADPCM**
(fmt tag 2, 4-bit, 22050 Hz, mono or stereo). Godot only loads PCM/IMA-ADPCM/QOA, hence
the pure-C# decoder in `WavFile.cs`.
