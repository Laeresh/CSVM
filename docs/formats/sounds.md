# Sounds: `sounds.json` SETS, player curves, WAV format

Part of the [format documentation](README.md). Covers the shared zrdr archive's sound
definitions and curve blocks, and the audio container format. Consumed by
`CrimsonSkies/src/Mech3/SoundDefs.cs`, `WavFile.cs`, `src/Flight/FlightAudio.cs`.

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
