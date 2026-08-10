# The puffer particle system, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-09/10, over the course of
[`PLAN-puffer-engine-deltas.md`](../plans/PLAN-puffer-engine-deltas.md). Every claim below names
the function it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side — the `PUFFER_STATE` key list, the reader files,
the flipbook textures, the compiled-event schema — is [`formats/effects.md`](../formats/effects.md)
and [`formats/anim-definitions.md`](../formats/anim-definitions.md). Our implementation is
`CSVM/src/Effects/Puffer.cs`, whose entry in [`architecture.md`](../architecture.md) carries the
plumbing and the traps. This page is the original's runtime: what the engine does with those keys.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement, the
decode wins and the disagreement is a note (`PLAN-puffer-engine-deltas`'s standing rule on video
evidence). Where CSVM deliberately differs, that is listed at the bottom rather than hidden.

## Function map

| Address | Role |
|---|---|
| `FUN_004f7120` | `PUFFER_STATE` reader/parser (`zeff_anim_init.c`) — one key-block per authored key, each setting a bit in the def's flag word at `+0x30` |
| `FUN_004e7e40` | Event applier, def → live puffer: flag-gated setter calls, and therefore the def-offset → object-offset map |
| `FUN_00550100` | Puffer object constructor (0xEC bytes) — the unauthored defaults |
| `FUN_00550500` / `FUN_00550550` | The two fade-band setters; each stores the band, then its reciprocal |
| `FUN_0054ee10` | The global tick: gusting wind, then every emitter, then every particle |
| `FUN_0054f8b0` | Emitter tick — the emission accumulator, and the particle spawn with its draw order |
| `FUN_00550970` | Particle teardown (frees the per-particle texture-sequence vector) |
| `FUN_0054e6e0` | Per-particle draw: distance fade, colour ramp, scale ramp, final radius |
| `FUN_0057c5c0` | Screen-space sprite quad: `±r` in both axes, the 1-pixel cull, the UV clip |
| `FUN_0054ed10` | View-matrix setup — pre-scales the third column, which is why the draw's distance is a view-space DEPTH |
| `FUN_0054d9c0` | Writes the `PRIORITY` size constant `K`: `0.01` software, `0.02` hardware |
| `FUN_0053c110` | Hardware-path projection setup — makes the view/draw scale factors exact reciprocals |

Two script-exposed globals, `PufferSetGlobalAgeFactor` (`00637a90`) and
`PufferSetGlobalFadeFactor` (`00637a94`), both default to `1.0`. Nothing in this install writes
either.

## The emitter object (0xEC bytes)

Offsets as mapped by the applier `FUN_004e7e40`:

| Offset | Key | Offset | Key |
|---|---|---|---|
| `+0x04` | `NUMBER` | `+0x64` | `DEVIATION_DISTANCE` |
| `+0x10`…`+0x18` | colour-ramp vector | `+0x68` | `FRICTION` |
| `+0x30`/`+0x34` | scale-sequence vector | `+0x6c` | `WIND_FACTOR` |
| `+0x3c` | interval is by distance (flag) | `+0x70` | `PRIORITY` |
| `+0x40`/`+0x44` | interval, `1/interval` | `+0x78` | previous emitter position |
| `+0x48` | the emission accumulator | `+0x84` | "has a previous position" flag |
| `+0x4c`/`+0x50` | `SIZE_RANGE` min/max | `+0x88`…`+0xcc` | six vec3s: translation, local vel, world vel, min/max random vel, world accel |
| `+0x54`/`+0x58` | `LIFETIME_RANGE` min/max | `+0xd0`…`+0xe4` | `NEAR_FADE`, `FAR_FADE`, and their reciprocals |
| `+0x5c`/`+0x60` | `START_AGE_RANGE` min/max | | |

**The constructor's defaults** (`FUN_00550100`) are the answer to every "what does an unauthored
key do?" question, and two of them contradict guesses this project had shipped:

| Field | Default | Note |
|---|---|---|
| `TIME_INTERVAL` (`+0x40`, `+0x44`) | **1.0** s | `0x3f800000` written to both the interval and its reciprocal. ⚠ Ours invents **0.1** — `BL-336` |
| `NUMBER` (`+0x04`) | **1** | Settles `BL-218`: our fallback of 1 is the engine's own, and raising it would be a divergence |
| `SIZE_RANGE`, `LIFETIME_RANGE` | 1.0 | |
| `WIND_FACTOR` (`+0x6c`) | **1.0, not 0** | So an unauthored puffer is FULLY wind-carried; only an explicit `0.0` opts out |
| `NEAR_FADE` | `(0, 0)` | i.e. no near cull |
| `FAR_FADE` | `(FLT_MAX, FLT_MAX)` | The equal ends are why the reciprocal is stored as the raw difference (`0`) rather than an infinity |
| `PRIORITY` (`+0x70`) | 0 | So the size factor `1 + K·PRIORITY` is exactly 1 unless authored |

## The particle (0x78 bytes)

Float indices `0..0x1d`:

| Index | Meaning | Index | Meaning |
|---|---|---|---|
| `[0..2]` | position | `[0x11]`/`[0x12]` | near reciprocal / far reciprocal |
| `[3..5]` | velocity | `[0x13]` | friction |
| `[6..8]` | acceleration | `[0x14]` | wind factor |
| `[9]` | **base size** | `[0x15]` | priority |
| `[10]` | age | `[0x16]` | **`TEXTURE_SEQUENCE` cursor** — stride 8 `(time, textureHandle)`, **stepped, never interpolated** |
| `[11]` | lifetime | `[0x17]` | a **byte** flag, not a float |
| `[12]` | `1/lifetime` | `[0x18..0x1a]` | the per-particle texture-sequence vector (begin/end/capacity), heap-allocated per particle in `FUN_0054f8b0` and freed in `FUN_00550970` |
| `[0xd]`/`[0xe]` | `NEAR_FADE[0]` / `NEAR_FADE[1]` | `[0x1b]` | colour cursor into `[0x1d]`+4/+8, stride `0x14` = `(r,g,b,a,time)`, time key at `+0x10` |
| `[0xf]`/`[0x10]` | `FAR_FADE[0]` / `FAR_FADE[1]` | `[0x1c]` | **the scale-ramp cursor**, into `[0x1d]`+0x14/+0x18, stride 8 `(time, scale)`, lerped |
| | | `[0x1d]` | the shared, refcounted ramp container |

⚠ **The fade copy from object to particle is not a contiguous six-float memcpy.** `FUN_0054f8b0`
writes the four bounds first and *then* the two reciprocals, so an implementer who mirrors the
object's `+0xd0`…`+0xe4` order onto the particle lands the wrong value in `[0xf]` and `[0x11]`.

⚠ `[0x16]` and `[0x18..0x1a]` are the **texture** sequence, not the scale ramp — this plan's first
draft read them as a per-particle copy of the scale ramp and was wrong.

## The tick (`FUN_0054ee10`)

Once per frame, in this order: derive the one global gusting wind vector; tick every emitter
(spawning); then integrate and reap every particle. The per-particle body is three steps, and the
**order is load-bearing**:

1. `pos += v · dt` — on the velocity the particle had at the top of the frame;
2. `v += a · dt`;
3. only when `FRICTION != 0` (the gate at `0054f016` is an explicit compare against 0.0, not an
   `exp(0) == 1` identity): `v = (v − wind·WIND_FACTOR) · exp(−FRICTION·dt) + wind·WIND_FACTOR`.

**Friction damps toward the WIND, not toward rest.** With a non-zero wind that gives a particle a
terminal velocity of `wind·WIND_FACTOR + a/FRICTION` rather than `a/FRICTION`, and a frictionless
puffer feels no wind at all — the gate is what keeps the two coherent.

**Reaping is `age >= lifetime`, with no sign test anywhere.** A particle born with a negative age
(`START_AGE_RANGE` authoring a negative minimum, e.g. `fire_at_zepskin3`'s `(−1.0, 0.1)`) therefore
simply lives `|age₀|` longer than its authored lifetime. It is *drawn* on the frame it is born,
because the draw clamps the ramp parameter (`if (0.0 < age) t = age/life; else t = 0.0f`, confirmed
in raw x87 at `0054e777`) rather than skipping. **A negative start age is a stagger-and-hold, not a
spawn delay**: the particle sits pinned to stop 0 of the scale ramp, the colour ramp and the life
envelope alike while still moving under velocity, friction and wind.

## The emission accumulator (`FUN_0054f8b0`)

Both continuous modes run one accumulator (`+0x48`), and the branch that feeds it is where they
differ:

```
if (byDistance)  { len = |pos - prevPos|;  if (len < 200.0) accum += len; }
else             {                                          accum += dt;  }
count = floor(accum * (1/interval));            // +0x44 holds the reciprocal
for (b = 0; b < count; b++) {                   // NO per-frame cap
    frac      = (b + 1) * interval / accum;     // position along prevPos -> pos
    ageOffset = (1 - frac) * dt;                // the sub-frame birth age
}
accum -= count * interval;                      // remainder carried
```

Four things this settles:

- **The 200 m test is a teleport guard, and it exists only on the distance arm.** A respawned or
  pooled emitter that jumps across the world lays no line of puffs along the jump. Time mode
  accumulates `dt` with no test of any kind. `200.0` is an inline float immediate, not a `_DAT_`
  global (the same decompile renders the `PRIORITY` constant as `_DAT_00a06fb0`), so there is no
  world-scale parameter behind it.
- **The guard suppresses the frame's emission entirely**, not just the jump's share: the carried
  remainder is by construction below one interval, so `count` is 0 on a guarded frame.
- **Nothing bounds `count`.** A long frame emits the whole catch-up in that frame. What stops that
  from being visible is not a cap but the spawn's own born-dead skip, below.
- **Emission is spread along the emitter's motion**, not stacked on the current pose: batch `b`
  spawns at `prevPos + (pos − prevPos)·frac` and is born `(1 − frac)·dt` old.

⚠ The emitter's previous position (`+0x78`) is written **unconditionally** at the end of the tick,
guarded frame or not, and the `+0x84` "have I a previous position" flag suppresses the whole emit
block on the emitter's first ever tick. A teleport costs exactly one frame of emission, and the
emitter resumes from its new pose with its remainder intact.

### The spawn, and its draw order

Per batch, `NUMBER` particles. The engine draws **lifetime, then start age**, before its position
draws; each position axis is `prev + delta·frac + (rand01 − 0.5)·DEVIATION_DISTANCE`.

- **`DEVIATION_DISTANCE` is a HALF-width**: the offset is `±0.5·d` per axis, not `±d`. (Reading it
  as `±d` scatters eight times the authored volume.)
- **The born-dead skip**: `if (age₀ >= lifetime)` the particle is **not created at all** — no slot,
  no draw. `age₀` is the `START_AGE_RANGE` draw *plus* the sub-frame `(1 − frac)·dt`, so the skip is
  reachable for **any** puffer on any long frame, with no `START_AGE_RANGE` authored anywhere. A 5 s
  hitch hands the earliest catch-up batch a start age of ~4.8 s, past every lifetime in the install:
  the catch-up burst mostly evaporates, which is the engine's own answer to the uncapped `count`
  above.

## The draw (`FUN_0054e6e0`), and the sprite quad (`FUN_0057c5c0`)

```
radius_px = projScaleX * (1 + K * PRIORITY) * baseSize * scaleSeq(ageFrac) / z_view
```

`projScaleX` is the same factor applied to the x-column of the view matrix in `FUN_0054ed10`, so the
world↔screen ratio cancels: **the sprite is equivalent to a camera-facing world quad of side
`2 × baseSize × scaleSeq(ageFrac)`.** `SIZE_RANGE` is a **radius** — `FUN_0057c5c0` draws at
`screenX ± r`, `screenY ± r` — which is the factor of 2 CSVM had been standing in for with a judged
constant.

`K` is `0.01` on the software path and `0.02` on the hardware path (`FUN_0054d9c0`). This project
has no software path, so `0.02` is ours.

`FUN_0057c5c0` also carries a **1-pixel cull** (a sub-pixel sprite is dropped) and clips UVs against
the 320×200 viewport bounds. Neither is reproduced here: the cull is a fill-rate defence for a
software rasteriser, and at modern resolutions it would discard sprites the original kept.

**The camera-distance fade** — the two bands, their field order, the deliberate cross-wire in the
near ramp's origin, and the fact that the near band is a *cull* rather than a fade with the shipped
data — is written up where the authored keys are, in
[`formats/effects.md`](../formats/effects.md#the-camera-distance-fade-fade_range--near_fade). The
distance is a view-space **depth**, not a euclidean range.

### The three ramps

| Ramp | Cursor | Stride | Interpolation |
|---|---|---|---|
| `SCALE_SEQUENCE` (age → scale) | `[0x1c]` | 8 `(time, scale)` | **lerped** |
| `COLORS` | `[0x1b]` | `0x14` `(r,g,b,a,time)` | lerped |
| `TEXTURE_SEQUENCE` | `[0x16]` | 8 `(time, handle)` | **stepped, never interpolated** |

The parser accepts up to six `SCALE_SEQUENCE` stops and **synthesises exactly the two-stop ramp
`(0, 1), (1, GROWTH_FACTOR)`** when the key is absent — which it is, in every reader in this
install. That is why the compiled `growth_factors` array is `(age, scale)` pairs and never a
min/max range, and why a two-point lerp is correct for all 2,906 authored events. Ramp times are
**fractions of the particle's own lifetime**, not seconds.

## Where CSVM deliberately differs

Everything here is a known, deliberate divergence — not a gap waiting to be closed.

| Divergence | Why |
|---|---|
| **The still-host synthetic `0.1 s` cadence** on `DISTANCE_INTERVAL` states | The engine has no such fallback: a distance emitter whose host never moves emits nothing. Ours sputters, so a damaged building smokes. Entangled with `BL-336` — the same 0.1 doubles as our unauthored `TIME_INTERVAL`, where the engine's default is 1.0 |
| **`TrailBurnAt`** — spending *virtual* metres at a held pose | For hosts that cannot move (the damage lab's parked plane). The engine has no equivalent, which is also why the 200 m teleport guard is not applied to it: there is no motion length to test |
| **The 1-pixel cull** (`FUN_0057c5c0`), skipped | A software-rasteriser fill defence; at modern resolutions it would discard sprites the original drew |
| **`K = 0.02`** rather than the software `0.01` | This project has no software path |
| **The life-fade envelope** (ease the additive glow in/out) | A render nicety with no authored key behind it. It is bypassed entirely whenever a `COLORS` ramp is present, since the ramp owns the alpha |
| **The blend mode, derived from the sprite a particle dies on** | The authored data never states one. Measured rule in [`formats/effects.md`](../formats/effects.md) |
| ~~`puffer.fireRiseScale` / `fireLifetimeScale`~~ | **DELETED 2026-08-10.** The one invented multiplier this system carried, and it is gone — see below |

### The fire pair, measured and then DELETED (D10, 2026-08-10)

`large_30sec_fire`'s `fire_n_smoke` measured through the real emitter for its authored 30 s, still
host, heights above the emitter (suite `puffer-fire-column`). The tuned rows are the build as it
stood that morning; the pair was removed the same day:

| Arm | Centre apex | Drawn top | Drawn top at the pre-A1 sprite | Peak live |
|---|---|---|---|---|
| Authored, still air | 17.9 m | 25.9 m | 21.6 m | 50 |
| Authored, C1 IA1's wind `(0, 2, 0)` | 24.3 m | **32.1 m** | — | 50 |
| ~~Tuned (2.5× rise, 1.5× life), still air~~ | 50.7 m | 57.2 m | 53.7 m | 72 |
| ~~Tuned, C1 IA1's wind~~ | 60.1 m | **67.8 m** | — | 73 |

The decode moved the authored column from 21.6 m to 32.1 m — the doubled sprite (+4.3 m) and,
larger, the wind coupling: C1 IA1's `STATIC_VELOCITY` is `(0, 2, 0)`, straight **up**, and against
`FRICTION 0.6` with `WORLD_ACCELERATION −1` the terminal velocity is `2 − 1/0.6 = +0.33 m/s`, so the
column never turns over and climbs until the lifetime ends. **That +49 % is what made the invented
pair obsolete**: with it the plume drew to 67.8 m, i.e. the tune was contributing **2.11×**, and at
the controls that day the refuel-tank flames read as *"~twice the height of the originals"*. A
measured ratio and an eye agreeing to two decimals is as settled as this project gets, so both
scales and their config keys were deleted and the fire family now runs its authored numbers like
every other puffer.

⚠ **Do not re-add a rise or lifetime multiplier for the fire family.** If a fire reads wrong, the
candidates are the authored density (`BL-218`), the blend verdict (below), or the wind — not a
compensating constant keyed on one emitter's name.

⚠ **These are one seed's extremes, good to about ±1.5 m.** The height reported is the tallest
particle of the run, so it is a maximum over ~300 draws rather than a mean, and each emitter's RNG
stream is seeded by how many puffers were built before it. Read the ratios and the deltas, not the
last decimal.

### ⚠ Our blend verdict disagrees with the engine's (open)

Traced 2026-08-10, after the flames were reported drawing dark-smoke-over-fire. **The engine picks
a particle's draw routine on exactly one test: does it have a `COLORS` ramp?** `FUN_0054e6e0`'s
final call is `FUN_0057c5c0(pos, radius, texture, alpha, hasColourRamp)`, and on the hardware path
that flag selects between two entries of the rasteriser dispatch table — `DAT_009be790`
(`FUN_005a4b70`) with a ramp, `DAT_009be78c` (`LAB_005a4580`) without, both installed by
`FUN_005a8c00`. In the ramp-less branch the engine also forces vertex colour to white and alpha to
`(1 − ageFrac)`, its own life envelope.

**Nothing about the sprite's darkness enters into it.** CSVM adds a second condition — the
alpha-weighted luminance of the frame a particle dies on (`Puffer.SmokeLuminance`) — which flips
`fire_n_smoke` (`colors: null`, so ramp-less, so additive in the original) onto `blend_mix`. Because
each emitter is one `MultiMesh` with `depth_draw_never` and no per-particle sort, mixed sprites
paint in instance-index order, so an old near-black puff can cover a young bright flame; drawn
additively a black sprite adds nothing and cannot occlude. That is the reported symptom.

Not yet pinned: which of the two dispatch entries is additive and which is alpha-blend — that needs
`LAB_005a4580` and `FUN_005a4b70` walked for their blend state. The darkness rule was introduced
deliberately (a ramp-less near-black smoke drawn additively became *more glow*), so reverting it is
a real change with a known prior symptom, not a one-line fix.
