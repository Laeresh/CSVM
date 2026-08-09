# Puffer — the engine deltas

**ACTIVE PLAN** (written 2026-08-09). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan closes the gap between our CPU puffer simulation (`CSVM/src/Effects/Puffer.cs`) and the
original engine's, as traced through `crimson.exe` in Ghidra on 2026-08-09. It carries **only what
the disassembly settles**: the sprite-size convention, the spawn-scatter convention, and the six
authored mechanisms the engine implements that we do not (`SCALE_SEQUENCE`, `START_AGE_RANGE`,
sub-frame emission along the emitter's motion, wind-coupled friction, the `NEAR_FADE`/`FAR_FADE`
distance fade, and the `PRIORITY` size nudge). Every item names the exact function it came from.

What this plan deliberately excludes: the **crash-fireball hold time** (BL-24x's ~9 sim-s burn that
our burst does not sustain) is a lifetime/duration question, not a puffer-mechanism question — it
stays in `backlog.md`. So does `BL-218`'s `NUMBER` default. And the `puffer.fireRiseScale` /
`fireLifetimeScale` TUNE pair stays exactly as it is until every item below has landed: they are
invented compensation, and the point of this plan is to remove the reasons they were needed before
re-judging them.

## Milestone goal

- A puffer's sprite is the size the original draws it, from the authored `SIZE_RANGE` alone, with
  no invented multiplier on any spawn path.
- The authored per-particle mechanisms the engine runs — age ramp, start age, sub-frame emission,
  wind, distance fade — are ours too, so an effect that looks wrong is a *data* question again.
- `docs/formats/` carries the decoded puffer runtime, so the next session reads it instead of
  re-tracing `crimson.exe`.

**No new invented constants.** Every number this plan introduces is read out of the disassembly or
the authored data. Where the footage and the decode disagree (see A1), the decode lands and the
disagreement is recorded — not averaged away.

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `Puffer.SizeScaleDefault` needs a judged stand-in for "a missing engine constant" | The constant is not missing. `FUN_0057c5c0` draws the sprite at `screenX ± r`, `screenY ± r` — `SIZE_RANGE` is a **radius**. The stand-in was standing in for a factor of exactly 2. |
| 2 | ×4 (settled at the controls 2026-08-01) is the sprite-size answer | ×4 was chosen because at ×1 the emitters "read as a thin scatter of specks". Two separate factor-2 errors produce exactly that symptom: sprites half the size they should be (A1) and scatter twice as wide as it should be (A2). ×4 over-corrected one of them and never touched the other. |
| 3 | The five puffers whose `growth_factors` don't map to a reader `GROWTH_FACTOR` scalar are name collisions across readers (`docs/formats/anim-definitions.md:1180`) | The census (below) reproduces that survey's own denominator and finds **zero** mismatches — the apparent ones differ by 1.9e-07, float print noise. The real cause is wildcard reader names expanding at compile time. Separately: an entry is `(age, scale)`, not `(min, max)`, proven by 216 events whose "max" is less than its "min". |
| 4 | The CAP-16 footage measurement (≈9.7 m fireball) is a check on the decode | **Video measurements are not trusted evidence in this repo — they have failed repeatedly** (author's standing instruction, 2026-08-09). A1 landed against a *decode*, and the footage number disagreed with the sim at both the old constant (16.6 m) and the new one (20.7 m) — i.e. no value of `SizeScaleDefault` was ever going to reconcile it, so it was never evidence about this constant. Do not re-open a landed decode on a footage estimate. |

**⚠ Standing rule on video evidence.** Frame-measured distances from the original footage
(`OriginalScreenshots/`, the `CAP-nn` clips) are **weak evidence** here and have misled this project
more than once. They are usable for *qualitative* reads — is there a plume, does it rise, roughly
how long does it last — and not for deriving or contesting a constant. Where a decode and a footage
measurement disagree, **the decode wins and the disagreement is a note, not an open question.**

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B3, B4, B5, C7, C8 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B6 | The coupling is traced; the global wind vector's authored source is not, so its magnitude is TUNE. |
| **Leads only — no mechanism yet** | C9 | Budget for investigation; this may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The puffer implementation in `crimson.exe`, by function:

| Piece | Address | Notes |
|---|---|---|
| `PUFFER_STATE` reader/parser | `FUN_004f7120` | `zeff_anim_init.c`; one key-block per authored key, each setting a bit in the def's flag word at `+0x30` |
| Event applier (def → live puffer) | `FUN_004e7e40` | Flag-gated setter calls; this is the def-offset → object-offset map |
| Puffer object ctor (0xEC bytes) | `FUN_00550100` | Defaults: interval reciprocal at `+0x44`, sizes/lifetimes 1.0, far fade `FLT_MAX`, `PRIORITY` 0 |
| Global tick — wind, emit, integrate, reap | `FUN_0054ee10` | Gusting wind, then every emitter, then every particle |
| Emitter tick + particle spawn | `FUN_0054f8b0` | The spawn draw order and the emission-count accumulator |
| Per-particle draw | `FUN_0054e6e0` | Distance fade, colour ramp, scale ramp, final radius |
| Screen-space sprite quad | `FUN_0057c5c0` | `±r` in both axes, 1-pixel cull, UV clip against the 320×200 viewport bounds |

**The live puffer object's layout** (offsets into the 0xEC-byte object, as mapped by `FUN_004e7e40`):

| Offset | Key | Offset | Key |
|---|---|---|---|
| `+0x04` | `NUMBER` | `+0x64` | `DEVIATION_DISTANCE` |
| `+0x3c` | interval is by distance | `+0x68` | `FRICTION` |
| `+0x40`/`+0x44` | interval, 1/interval | `+0x6c` | `WIND_FACTOR` |
| `+0x4c`/`+0x50` | `SIZE_RANGE` min/max | `+0x70` | `PRIORITY` |
| `+0x54`/`+0x58` | `LIFETIME_RANGE` min/max | `+0x88`…`+0xcc` | six vec3s: translation, local vel, world vel, min/max random vel, world accel |
| `+0x5c`/`+0x60` | `START_AGE_RANGE` min/max | `+0xd0`…`+0xe4` | `NEAR_FADE`, `FAR_FADE` (+ reciprocals) |
| `+0x30`/`+0x34` | scale-sequence vector | `+0x10`…`+0x18` | colour-ramp vector |

**The particle** is 0x78 bytes (indices `0..0x1d`): `[0..2]` pos, `[3..5]` vel, `[6..8]` accel,
`[9]` **base size**, `[10]` age, `[11]` lifetime, `[12]` 1/lifetime, `[0x13]` friction,
`[0x14]` wind factor, `[0x15]` priority, `[0x17]` a *byte* flag (not a float), `[0x1d]` the shared,
refcounted ramp container.

The fade bounds and the two ramp cursors were **read wrong in this plan's first draft** and are
corrected here (traced 2026-08-09, second pass):

| Index | What it actually is |
|---|---|
| `[0xd]`/`[0xe]` | `NEAR_FADE[0]` / `NEAR_FADE[1]` |
| `[0xf]`/`[0x10]` | `FAR_FADE[0]` / `FAR_FADE[1]` |
| `[0x11]`/`[0x12]` | near reciprocal / far reciprocal |
| `[0x16]` | **`TEXTURE_SEQUENCE` cursor** — *not* the scale-ramp cursor. Stride 8 `(time, textureHandle)`, **stepped, never interpolated** |
| `[0x18..0x1a]` | the per-particle **texture-sequence vector** (begin/end/capacity, heap-allocated per particle in `FUN_0054f8b0`, freed in `FUN_00550970`) — *not* a copy of the scale ramp |
| `[0x1b]` | colour cursor into `[0x1d]`+4/+8, stride `0x14` = `(r,g,b,a,time)`, time key at `+0x10` |
| `[0x1c]` | **the scale-ramp cursor**, into `[0x1d]`+0x14/+0x18, stride 8 `(time, scale)`, lerped |

⚠ **The fade copy from object to particle is not a contiguous six-float memcpy.** `FUN_0054f8b0`
writes the four bounds first and *then* the two reciprocals, so an implementer who mirrors the
object's `+0xd0`…`+0xe4` order onto the particle lands the wrong value in `[0xf]` and `[0x11]`.

**The keys the parser accepts that we do not read at all:** `SCALE_SEQUENCE`, `START_AGE_RANGE`,
`NEAR_FADE`, `FADE_RANGE`/`FAR_FADE`, `WIND_FACTOR`, `PRIORITY`.

**The render equation**, verbatim from `FUN_0054e6e0`:

```
radius_px = projScaleX * (1 + K * PRIORITY) * baseSize * scaleSeq(ageFrac) / z_view
```

`projScaleX` is the same factor applied to the x-column of the view matrix in `FUN_0054ed10`, so
the world↔screen ratio cancels: the sprite is equivalent to a camera-facing world quad of
**side `2 × baseSize × scaleSeq(ageFrac)`**. `K` is `0.01` on the software path and `0.02` on the
hardware path (`FUN_0054d9c0`). The two script-exposed globals, `PufferSetGlobalAgeFactor` and
`PufferSetGlobalFadeFactor`, both default to `1.0` (`00637a90`, `00637a94`).

## The census (2026-08-09) — what the install actually authors

Read-only sweep of **17,567 JSON files** under `Z:\CSVM\extracted`: the reader surface (all 1,293
`*.zrd.json`, yielding **879 `PUFFER_STATE` blocks**, of which 594 are full definitions and 285 are
name+`ACTIVE_STATE` stubs) and the compiled surface (**4,535 `PufferState` events**, 2,906 fully
authored). Coverage caveat: the plan's header cites 4,387 compiled events from `Puffer.cs:126`;
the file surface has 4,535 (4,427 excluding `#1`-suffixed duplicate-named anim files). The
discrepancy is unreconciled and changes no conclusion below — the histogram has no tail on any
denominator.

| Key | Reader blocks | Compiled events | Distinct puffers | Verdict |
|---|---|---|---|---|
| `SCALE_SEQUENCE` | **0** | — | — | **B3 unreachable — disproof** |
| `growth_factors` length ≠ 2 | — | **0 of 2,906** | — | **B3 unreachable — disproof** |
| `START_AGE_RANGE` | 4 | 80 | 4 | B4 implement (narrow) |
| `NEAR_FADE` | 33 | 432 | 25 | C7 implement |
| `FADE_RANGE`/`FAR_FADE` | 574 / 1 | **2,508** | **238** | **C7 implement — the widest unimplemented key in the plan** |
| `PRIORITY` | 58 | 192 | 47 | C8 implement — *not* "none" |
| `WIND_FACTOR` | 27 | 112 | 27 | B6 half-1 reachable |
| `DEVIATION_DISTANCE` ≠ 0 | — | **2,882 (99.2%)** | — | A2 landed; touched nearly everything |

**`growth_factors[i]` is `(age_i, scale_i)` — now proven from the data alone**, independent of the
disassembly: **216 events author a second entry whose "max" is less than its "min"**, including
`(1.0, −0.2)`. That is a coherent `(age 1, scale −0.2)` stop and an incoherent range. All 2,906
arrays are exactly `[(0,1), (1,G)]`; entry 0 is `(0,1)` in every single one.

**The "five name collisions" of `anim-definitions.md:1180` do not reproduce.** Re-running that
survey's own denominator (177 single-definition reader names, 168 with compiled counterparts) gives
**zero** mismatches — the 31 apparent ones differ by at most 1.9e-07, float32↔float64 print noise.
The likely original cause is an undocumented reader idiom: three **wildcard names**
(`rc_smokn_stacks*`, `stack_puffer*`, `torch_puffer*`) expand at compile time into exactly the 7
compiled names that have no reader definition. Separately there are **17 genuine** name collisions
where one name has several differing reader definitions (`fire_n_smoke` has 35; `smokerpuff` spans
G = 4.0 to 85.0), each resolved per-file. None is a multi-stop ramp.

**Compiled ↔ reader disagreements: none**, on any key, anywhere.

## The second disassembly pass (2026-08-09) — two ambiguities the census could not settle

**Negative start age.** `fire_at_zepskin3` authors `START_AGE_RANGE = (−1.0, 0.1)`, so ~91% of its
particles are born with a negative age. The engine neither delays nor extrapolates: `FUN_0054ee10`
integrates and reaps with **no sign test at all** (reap is `age >= lifetime` only, so the particle
simply lives ~1 s longer than its authored lifetime), and `FUN_0054e6e0` **clamps the ramp
parameter** — `if (0.0 < age) t = age/life; else t = 0.0f`, confirmed in raw x87 at `0054e777`.
There is no `< 0` guard that skips the draw. The particle is drawn on the frame it is born, pinned
to stop 0 of the scale ramp, the colour ramp *and* the life-envelope alpha alike, while still moving
under velocity, friction and wind. **It is a stagger-and-hold, not a spawn delay.**

**Fade field order** (settled at six independent points — parser, applier, setter, ctor defaults,
spawn copy, and the raw x87 comparisons):

- **Far band:** `FAR_FADE[0]` = ramp start (last full-alpha distance), `FAR_FADE[1]` = **hard
  discard cutoff**. `alpha = (FAR_FADE[1] − d′) / (FAR_FADE[1] − FAR_FADE[0])`. Always authored
  ascending.
- **Near band:** `NEAR_FADE[0]` = **hard discard cutoff** (invisible at or below it),
  `NEAR_FADE[1]` = the distance at which alpha reaches 1. The code assumes `[0] < [1]`.
- The reciprocal is `1 / (second − first)`, left as the raw difference (`0.0`) when they are equal.
- The ctor defaults settle which block is which — near = `(0, 0)`, far = `(FLT_MAX, FLT_MAX)` — and
  are coherent only this way round.

⚠ **A genuine cross-wire in the original.** The near ramp's origin is `particle[0xf]` =
**`FAR_FADE[0]`**, not `NEAR_FADE[0]` — verified in raw assembly (`0054e7b5 FSUB [ESI + 0x3c]`
against the *near* reciprocal at `ESI + 0x44`), not a decompiler artefact. It is consistent with
`FADE_RANGE` being the original field (low flag bit `0x1000`) and `NEAR_FADE` bolted on later (high
bit `0x80000`) by copy-pasting the far-band line. **Reproduce it faithfully and document it**; do
not "fix" it. It is dead code for every puffer but one, and fixing it would make `volcanosmoke`
fade in over 1→75 m where the original pops it in at 75 m — a silent divergence.

⚠ **With the shipped data the near band never produces a partial alpha.** All five descending pairs
(`70,30`, `70,20`, `70,50`, `40,5`, `30,10`) make the ramp branch unreachable, degenerating to a
**hard near-cull** at `NEAR_FADE[0]`: invisible within 70 units, full alpha beyond. The one
ascending pair, `volcanosmoke`'s `(1,75)`, reaches the ramp branch but the cross-wire drives alpha
negative, so it culls too. The near band is a **cull**, not a fade, on every puffer in the install.

**`PufferSetGlobalFadeFactor` (`00637a94`, default 1.0) scales the far band only.** `d′ = d ×
factor` feeds the far discard, the near/far band selector and the far ramp numerator; both near
comparisons use plain, unscaled `d`. Below 1.0 it pushes the far fade outward.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the two factor-of-two conventions

1. ☑ `SIZE_RANGE` is a radius — the quad side is `2 × size`
2. ☑ `DEVIATION_DISTANCE` scatters ±0.5·d, not ±d

### Wave B — the missing per-particle mechanisms

3. ☐ `SCALE_SEQUENCE`: the age→scale ramp, of which `GROWTH_FACTOR` is the two-stop case
4. ☐ `START_AGE_RANGE`: random birth age, sub-frame age offset, and the born-dead skip
5. ☐ Sub-frame emission — spread a frame's puffs along the emitter's motion segment
6. ☐ Friction damps toward the wind, not toward zero

### Wave C — the render-side rules

7. ☐ `NEAR_FADE` / `FAR_FADE` camera-distance alpha, and the 1-pixel cull
8. ☐ `PRIORITY` inflates the sprite by `1 + K·PRIORITY`
9. ☐ Emission-accumulator rules: the 200 m teleport guard vs. our per-frame batch cap

### Wave D — record and re-judge

10. ☐ Write `docs/formats/puffer.md` and re-judge the fire TUNE pair against the corrected sim

## Dependency and parallelism notes

**A1 and A2 land together or not at all** — each is a factor of 2 in the opposite visual direction,
and landing one alone will look worse than landing neither (bigger sprites over a scatter that is
still twice too wide, or authored-width scatter of sprites still half size). Treat them as one
commit even though they are two items.

A1+A2 block everything downstream: every later item is judged against the corrected baseline, and
every golden that carries a puffer moves once, at A1+A2, rather than nine times.

B3–B6 are independent of each other and all edit `CSVM/src/Effects/Puffer.cs` — **file contention:
do not run them in parallel worktrees.** B3 additionally edits `PufferState.FromAnimEvent` and
`CSVM/src/Mech3/AnimDefs.cs`; B4 and B5 both touch `SpawnSustained`/`SpawnTrailPuff`.

C7–C9 are independent of B and of each other. C7 is the only item that touches
`CSVM/src/Effects/EmitterRenderer.cs` and the shader; C8 is a one-line rider on C7's plumbing (both
need the camera distance the fade already computes) — land C7 first, then C8 is trivial.

D10 needs every item above.

---

# Wave A — the two factor-of-two conventions

## A1 ☑ `SIZE_RANGE` is a radius — the quad side is `2 × size`

**Landed.** `Puffer.SizeScaleDefault` (`CSVM/src/Effects/Puffer.cs:281`) is now `2f`, with its doc
comment rewritten to cite `FUN_0057c5c0`/`FUN_0054e6e0` as the decoded radius→diameter conversion
rather than a judged stand-in. The three `puffer.*SizeScale` config keys are untouched and now
default to the decode. The cull margin in `Init` (`Puffer.cs:751-755`) derives from
`Mathf.Max(_burstSizeScale, _trailSizeScale, _sustainSizeScale)`, which itself defaults from
`SizeScaleDefault` — confirmed to follow the constant with no separate edit needed.
`fireRiseScale`/`fireLifetimeScale` are untouched, left for D10.

**Verified.** Landed together with A2 (see A2's Verified paragraph for the shared regression). The
CAP-16 re-measurement (`RecordingEmitterRenderer` over `fierypuffer` verbatim, `Puffer.Burst`/
`_Process` at dt = 1/60 to t = 0.14) read, on the unchanged build (`SizeScaleDefault` 1,
pre-A2 `±d` scatter): mean sprite **4.10 m**, particle-cloud span (centres) **12.52 m**, whole
burst span (cloud + mean sprite) **16.62 m**. After A1+A2: mean sprite **8.20 m** (exactly ×2,
confirming the constant took effect — METHOD-15), cloud span **12.53 m** (A2 does not move this
particular puffer — `fierypuffer` authors no meaningful `DEVIATION_DISTANCE`; its spread is almost
entirely the ±65 m/s random velocity), whole burst span **20.73 m**. ⚠ **This is the trap the plan
warned about, realised**: the corrected sim reads *larger* against the footage's 9.7 m than the old
×1 baseline already did (16.6 m), not smaller. The decode lands anyway, per the ground rule — this
is recorded as a finding in `backlog.md`'s `BL-122`/CAP-16 entry (2026-08-09 update), not resolved
by picking a different constant. Whoever next tunes `puffer.burstSizeScale` or re-judges the fire
TUNE pair (D10) inherits an open question: either the fire sprite's visible alpha core is
substantially smaller than its quad, or the fire-keyed pixel measurement in the original CAP-16
analysis undercounts the additive glow's true extent. The full 8-chapter `--freecam --chapter=<X>`
regression and the targeted goldens are reported under A2.

### Original approach (kept for reference)

**Goal.** A puffer sprite covers the same world footprint as the original's, from the authored
`SIZE_RANGE` alone. `Puffer.SizeScaleDefault` stops being "a judged stand-in for a missing engine
constant" and becomes the decoded factor.

**Evidence (confidence: traced).** `FUN_0054e6e0` computes
`radius_px = projScaleX * (1 + K*PRIORITY) * particle[9] * scaleSeq(ageFrac) / z_view` and hands it
to `FUN_0057c5c0`, which lays the quad at `*param_1 - param_2` … `param_2 + *param_1` in x and the
same in y — i.e. the value is a **half-extent**, and the sprite spans `2r`. The `0.5 / param_2`
used for the viewport UV clip in the same function confirms the full extent is `2r`. `projScaleX`
(`_DAT_009fd6f0`) is the same scalar applied to the x-column of the view matrix in `FUN_0054ed10`,
so it cancels: the sprite is a world quad of side `2 × size`. Ours is a `QuadMesh{Size = One}`
scaled by `size` — side `1 × size` (`EmitterRenderer.cs:133,152`). **The constant is 2.**

**⚠ This contradicts the CAP-16 estimate** in `backlog.md:263-291`, which measured the crash
fireball at ≈9.7 m across at t=0.14 and concluded "the footage puts the missing constant near 1,
not 4". At ×2, `fierypuffer` (SIZE_RANGE 2–4, GROWTH 1→3) gives a mean sprite of ~7.9 m over an
8.9 m particle cloud — a ~17 m span against a 9.7 m measured one. Both can be true only if the
fire-keyed visible core is ~55% of the quad, which is plausible for a soft radial fire sprite but
is not established. The decode lands; the disagreement is recorded, not averaged.

**Approach.** `Puffer.SizeScaleDefault` → `2f`, and rewrite its doc comment: it is no longer a
judged stand-in but the decoded radius→diameter conversion, cited to `FUN_0057c5c0`. Leave the
three `puffer.*SizeScale` config keys in place as per-path dev knobs (they stay in
`Config.WarmTuningRegistry`), but they now default to the decode rather than to a guess. Do **not**
touch `fireRiseScale`/`fireLifetimeScale` here — that is D10's job, after the whole plan lands.

**Model recommendation.** medium, low effort — the code change is one constant and a comment; the
judgement was done in the disassembly.

**Verify.** Land with A2 and measure once. Re-run the CAP-16 comparison from `backlog.md:274-286`
(simulate `flame_ball_01-large_fireball`'s `fierypuffer` through `SpawnBatch`/`_Process` at
dt = 1/60, measure the sprite span at t = 0.14) and record the new number against the footage's
9.7 m, whatever it says. Then the 8-chapter `--freecam` regression, plus the c1-waterfall and
c5-fog goldens, which will move — take the baseline first.

**⚠ Traps.**
- **Every puffer-bearing golden moves.** Take baselines before touching the constant, and land
  A1+A2 in one commit so the goldens move exactly once.
- The cull margin in `Init` (`Puffer.cs:753-755`) is computed from the size scales — it follows the
  constant automatically, but confirm it does, or large sprites will pop at screen edges.
- **Do not "split the difference" with the CAP-16 estimate.** A decode and an estimate are not two
  measurements of the same quality. If the corrected sim still reads too big at the controls, that
  is a *finding* about sprite alpha or particle count, and it gets its own backlog entry.

## A2 ☑ `DEVIATION_DISTANCE` scatters ±0.5·d, not ±d

**Landed.** All three spawn paths (`SpawnSustained` `Puffer.cs:776`, `SpawnTrailPuff` `:806`,
`SpawnBatch` `:842`) now draw `Rand(-0.5f*d, 0.5f*d)` in place of `Rand(-d, d)`, one `Rand()` call
per axis, same draw order (pos → vel → size → life) as before.

**Verified.** Full 8-chapter `--freecam --chapter=<X> --det --frames=90 --screenshot=…` regression
(C1, C1B, C1C, C2, C2B, C3, C4, C5): **0 `ERROR` lines in any chapter's engine log**; every run
produced its screenshot, proving each reached its frame count rather than hanging or crashing. No
node/mesh-count instrumentation was read this pass (A1/A2 touch only `Puffer`'s spawn-time
particle transforms, never node or mesh counts, so none was expected to move) — noted as
unverified below.

Golden regression (`RunTests.ps1 -SkipUnits -SkipEngine`, then `-RegenGoldens`): **7 of 13 shots
moved**, all and only the puffer-bearing ones — `c1-waterfall`, `c3-island`, `c4-snow`,
`c5-city-night`, `c1-flight`, `c1-destroy-effects`, `c1-crash`. The other 6 (`c1b-night-sea`,
`c1c-rain`, `c2-city`, `c2b-rain`, `viewer-bhawk`, `empty-stage`) are byte-identical, confirming the
change is localised to `Puffer` (GOLD-5). `c4-snow` moving was not predicted by
`docs/architecture.md`'s five-shot list — by-eye inspection of `.scratch/goldens/c4-snow.png` shows
a distinct dark puff cloud on the mountainside not visible before, i.e. it does carry a live
puffer at that pose and the list in `architecture.md` was incomplete; corrected there. Eye-verified
by-image, not just by hash, for the five shots a "before" capture was taken for:
`c1-waterfall`'s mist puffer goes from faint/thin specks to a solid rounded cloud (the exact "thin
scatter of specks" symptom the plan's header table names, now fixed for the right reason);
`c1-crash`'s fireball grows from a cross-shaped cluster that still shows the aircraft silhouette
through it to a solid blob that fully occludes the plane — the direction A1's evidence section
warned about; `c3-island` and `c5-city-night` show small, localised brightening on a single distant
sprite, consistent with a small/far emitter; `c1-destroy-effects`'s moved pixels are not in frame
at this pose (the def's puffer sits off-camera), so nothing to confirm by eye there beyond the hash
diff. `c1-flight` and `c4-snow` were not captured before A1/A2 (discovered as movers only after the
fact), so their moves are confirmed by hash + `architecture.md`'s/the image's own account of what
they carry, not by a locally-held pixel diff — recorded as unverified-by-eye below.

**Unverified in this pass:** node/mesh instance counts across the 8-chapter sweep (only the error
census was read); `c1-flight` and `c4-snow`'s moved goldens by eye against a locally-saved "before"
image (both accepted on the hash move plus route-of-cause reasoning, not a side-by-side visual
diff); the CAP-16 finding's implication for `puffer.burstSizeScale`/the fire TUNE pair is explicitly
left open for D10, not resolved here.

### Original approach (kept for reference)

**Goal.** A puffer's spawn scatter covers the authored volume, not eight times it.

**Evidence (confidence: traced).** `FUN_0054f8b0` spawns at
`pos = prev + delta*frac + (rand01 - 0.5) * DEVIATION_DISTANCE` on each axis, where `rand01` is
`rand() * 3.051851e-05` (i.e. `rand()/32768`, 0…1) — so the offset is **±0.5·d**. Ours is
`Rand(-d, d)` — **±d** — in all three spawn paths (`Puffer.cs:776, 804, 840`). We scatter twice as
wide per axis, eight times the volume. Note the engine's own idiom for a symmetric ±1 draw is
`(x*3.05e-5 + x*3.05e-5) - 1` (seen in the wind gust code in `FUN_0054ee10`); the deviation code
deliberately does not use it.

**Approach.** `Rand(-d, d)` → `Rand(-0.5f * d, 0.5f * d)` in `SpawnSustained`, `SpawnTrailPuff` and
`SpawnBatch`. Keep the draw *order* (pos → vel → size → life) exactly as it is — the comment at
`Puffer.cs:773-775` is right that reordering re-scatters every sustained emitter and moves the
goldens for an unrelated reason.

**Model recommendation.** medium, low effort — mechanical, but it must be all three paths and the
draw order must survive.

**Verify.** Folded into A1's measurement. Additionally: the C1 waterfall, whose three splash
puffers sit ±11 m apart by `AT_NODE` offset with their own deviation on top, is the clearest visual
read on scatter width — capture it before and after.

**⚠ Traps.** Changing the number of `_rng` draws would re-scatter everything; changing their
*arguments* does not. Keep one draw per axis.

---

# Wave B — the missing per-particle mechanisms

## B3 ☐ `SCALE_SEQUENCE`: the age→scale ramp, of which `GROWTH_FACTOR` is the two-stop case

**Goal.** A puffer whose authored size ramp has more than two stops animates through all of them,
instead of being flattened to a straight line to the wrong endpoint.

**Evidence (confidence: traced).** The parser (`FUN_004f7120`) accepts **`SCALE_SEQUENCE`: up to
six `(age, scale)` stops** (`if (5 < i) break`), stored as a count at def word `0x88` followed by
float pairs. When `SCALE_SEQUENCE` is absent it falls through to `GROWTH_FACTOR` and **synthesises
the two-stop ramp** `count=2, (0.0, 1.0), (1.0, G)`. `FUN_004e7e40` copies the stops into the
object's vector at `+0x30`; `FUN_0054e6e0` walks them with a per-particle cursor
(`particle[0x16]`), advancing while `ageFrac >= stop.age` and lerping between the bracketing pair,
clamping to the last stop's value past the end.

So our `Mathf.Lerp(1f, GrowthFactor, lifeFrac)` (`Puffer.cs:533`) is **exactly right for two-stop
puffers and silently wrong for any other**: `FromAnimEvent` takes `growth[1].max` (`Puffer.cs:167-169`),
which is stop #1's *scale*, discarding stops 2–5. This also re-reads the compiled shape:
`growth_factors[i]` is `(min = age_i, max = scale_i)`, **not** a min/max pair — which is why
"the second entry's max is the reader's scalar" held for 172 of 177 puffers
(`docs/formats/anim-definitions.md:1180`). The five outliers are the hypothesis this item tests.

**Approach.** Two halves, in order:
1. **Census first.** Count `growth_factors.Length` across every `PUFFER_STATE` event in the install
   (4,387 of them per `Puffer.cs:126`), and separately grep the zrdr readers for `SCALE_SEQUENCE`.
   If the answer is "always 2", this item closes as a **disproof** — record it and stop. Do not
   write the ramp code before the census says it is reachable.
2. If multi-stop ramps exist: add `IReadOnlyList<(float Age, float Scale)> ScaleSequence` to
   `PufferState`, populated from `growth_factors` in `FromAnimEvent` and from `SCALE_SEQUENCE` (with
   the `GROWTH_FACTOR` fallback synthesising `[(0,1),(1,G)]`) in `Parse`; replace the `GrowthFactor`
   lerp in `_Process` with a ramp walk. Model it on `RampColor` (`Puffer.cs:815-828`), which already
   does the identical age-keyed lerp for `COLORS` — reuse its shape rather than inventing a second
   idiom. Keep `GrowthFactor` as a derived convenience if anything else reads it.

**Model recommendation.** high — the census is a judgement call about what the data means, and the
answer decides whether any code gets written at all.

**Verify.** The census output itself is the primary artefact — it goes in the landing commit
message either way. If code lands: a headless sim of one multi-stop puffer, asserting the size at
each stop's age matches the authored value; plus the `--effects-test` suite and the 8-chapter
regression.

**⚠ Traps.**
- **This may end in a disproof, and that is a success.** If every puffer in the install ships two
  stops, the finding is that `GROWTH_FACTOR` *is* the whole mechanism here — and the five outliers
  need their own explanation before `anim-definitions.md:1180` is rewritten.
- The engine's cursor is **per particle and monotonic** — it never rewinds. A particle whose age
  jumps past several stops in one frame lands on the right pair regardless; a naïve `for` scan from
  0 gives the same answer, so prefer the simple scan and skip the cursor.
- `docs/formats/anim-definitions.md:1180-1182` states the old `(min,max)` reading as fact. It must
  be corrected in the same turn, not left to D10.

## B4 ☐ `START_AGE_RANGE`: random birth age, sub-frame age offset, and the born-dead skip

**Goal.** A puffer's particles are born spread across their life phase, the way the original's are,
instead of all starting at age zero.

**Evidence (confidence: traced).** In `FUN_0054f8b0` the spawn draws
`life = LIFETIME_RANGE.min + rand01*(max-min)` and
`age0 = START_AGE_RANGE.min + rand01*(max-min) + (1 - frac)*dt`, then **`if (age0 >= life)` the
particle is not created at all** — so a `START_AGE_RANGE` overlapping the lifetime range also acts
as a probabilistic thinner. The `(1 - frac)*dt` term is the sub-frame correction that pairs with
B5's position interpolation. The parser reads the key at `FUN_004f7120`'s `START_AGE_RANGE` block
(def words `0x27`/`0x28` → object `+0x5c`/`+0x60`), flag `0x8000`. `PufferState` has no field for
it and `FromAnimEvent` never looks for it.

**Approach.** Add `StartAgeMin`/`StartAgeMax` to `PufferState` (both parsers), and in the three
spawn paths draw the start age, skip the particle when it is ≥ its lifetime, and seed `Particle.Age`
with it. The skip must not consume a pool slot. Note the interaction with the sustained pool sizing
in `Init` (`Puffer.cs:740-742`): a non-zero start age *lowers* the steady-state population, so the
existing formula stays a safe over-estimate — leave it.

**Model recommendation.** medium — mechanical once B4's draw order question is settled.

**Verify.** A headless sim asserting that a puffer with `START_AGE_RANGE` produces particles whose
initial ages span the authored range, and that one with a range exceeding its lifetime emits
proportionally fewer. Then the census line in `--debug-anim` (live counts will drop for affected
puffers — that is the expected direction, confirm it is not zero).

**⚠ Traps.**
- **Draw order again.** Adding a draw re-scatters every emitter downstream of it. The engine draws
  lifetime *then* start age, both before the position draws — match that, and expect the goldens to
  move for every puffer that has the key. Bundle with B5 if both land.
- Do not confuse this with `MAX_START_AGE`/`MIN_START_AGE`, which appear as separate error strings
  in the binary — the parser reads one `START_AGE_RANGE` list; the two other strings are its
  per-element diagnostics.

## B5 ☐ Sub-frame emission — spread a frame's puffs along the emitter's motion segment

**Goal.** A fast-moving emitter (the train's smokestack, a trailing plane) lays a continuous line of
puffs instead of a clump per frame.

**Evidence (confidence: traced).** `FUN_0054f8b0` emits `count = floor(accumulator / interval)`
batches per frame, and for batch `k` computes `frac = (k+1)*interval / accumulator` and spawns at
`prevPos + (curPos - prevPos) * frac`, with the matching `(1 - frac)*dt` added to the start age
(B4). Our `SpawnSustained` puts every batch at the *current* pose (`Puffer.cs:769`), so at 8
batches/frame we stack eight puffs on one point. `SpawnTrailPuff` already does the distance-mode
equivalent correctly (`Puffer.cs:643-648`, "walk back from the current position") — this item gives
the time-cadence path the same treatment.

**Approach.** Track the previous world pose per emitter in `SustainAt` and pass the interpolated
origin (and the age offset, if B4 has landed) into `SpawnSustained`. The trail path is the working
model; do not disturb it.

**Model recommendation.** medium.

**Verify.** A headless sim of a sustained emitter moved 100 m in one frame at an interval that
yields 8 batches: assert the eight spawn positions are evenly spaced along the segment. Visually,
the C1 train's steam plume at speed is the read.

**⚠ Traps.** The first frame after a `Stop()`/restart has no valid previous pose — re-home to the
current position rather than interpolating from a stale one, exactly as `TrailAdvance` does for the
ghost-trail rule (`Puffer.cs:488-497`). Getting this wrong draws a line of puffs from wherever the
emitter last was, which is the rocket-explosion bug that commit 450131a already fixed once.

## B6 ☐ Friction damps toward the wind, not toward zero

**Goal.** Puffer particles drift with the world's wind the way the original's do, and the damping
integrates in the same order.

**Evidence (confidence: direction sound, magnitude TUNE).** `FUN_0054ee10` integrates each particle
as `pos += v*dt; v += a*dt;` then, when `friction != 0`,
`v = (v - wind*WIND_FACTOR) * exp(-friction*dt) + wind*WIND_FACTOR` — friction pulls the particle
toward the **wind velocity**, not toward rest. Ours does `v = v*damp + a*dt` (`Puffer.cs:529`):
damped toward zero, and the acceleration applied *after* the damping rather than before.
The wind vector itself is a global (`00763da8`…) re-derived every tick with a random-walk gust:
a heading at `00763dc0` stepped by `±gustRate*dt`, a magnitude at `00763dc4` clamped to a ceiling
(`00763dc8`), reflected through +π when it goes negative. `WIND_FACTOR` (object `+0x6c`, parser
flag `0x100000`) is the per-puffer coupling; we do not read it.

**What is not traced:** where the wind's base heading/magnitude/gust-rate ceiling come from — they
are set through `FUN_00550690`/`FUN_005506b0`-`d0` by a caller this pass did not follow. Until that
caller is found, the wind *magnitude* is TUNE and belongs on `backlog.md`'s TUNE list; the
*coupling* is fact.

**Approach.** Two separable halves — land the first, and only take the second if the caller is
found:
1. **Integration order and `WIND_FACTOR` plumbing** (fact): apply `v += a*dt` before the damp, add
   `WindFactor` to `PufferState`, and damp toward `wind * WindFactor` with `wind` supplied by the
   session. With `wind = 0` this is a pure no-op *except* for the accel-before-damp reordering, so
   it can land safely on its own.
2. **The wind source** (investigation): find the caller of `FUN_00550690` and follow it back to the
   authored data — `weather.json` is the obvious suspect and `CSVM/src/Session/WeatherRig.cs` is
   where it would land. If it is not found, stop and record the dead end in
   `docs/architecture.md`.

**Model recommendation.** high — half of this item is a fresh trace through the binary, and the
stopping condition (record a dead end rather than invent a wind) is a judgement call.

**Verify.** Half 1: a headless sim asserting a particle with friction and zero wind decays to rest
on the same curve as before, and that a non-zero accel now shows the one-frame difference the
reorder predicts. Half 2: whatever the trace finds, cited to an address.

**⚠ Traps.** **Do not invent a wind vector.** A plausible breeze that no data authorises is exactly
the failure mode the ground rules name. Zero wind with the coupling wired is a complete, honest
landing for this item.

---

# Wave C — the render-side rules

## C7 ☐ `NEAR_FADE` / `FAR_FADE` camera-distance alpha, and the 1-pixel cull

**Goal.** Distant puffers fade out and vanish the way the original's do, instead of staying at full
alpha to the horizon.

**Evidence (confidence: traced).** `FUN_0054e6e0` opens with a distance gate: with
`d = projZ * viewZ` and `d' = d * globalFadeFactor`, a particle is **discarded** when
`d' >= FAR_FADE.end` or `d <= NEAR_FADE.start`, and alpha-ramped linearly across either band
(`(d - nearEnd) * nearRecip`, `(farEnd - d') * farRecip`) — the reciprocals are precomputed at set
time by `FUN_00550500`/`FUN_00550550`. Defaults are near = 0 and far = `FLT_MAX` (`FUN_00550100`),
i.e. no fade unless authored. The parser reads `NEAR_FADE` and `FADE_RANGE`/`FAR_FADE` (the latter
two are aliases for the same block). Separately `FUN_0057c5c0` returns without drawing when the
half-extent is `< 1.0` pixel, and clips the quad against the viewport bounds with a UV correction.
We read none of these keys.

**Approach.** Add `NearFadeStart/End` and `FarFadeStart/End` to `PufferState` (both parsers) and
multiply the per-particle alpha in `_Process` by the distance factor, discarding past the bounds.
Use the field order settled in "The second disassembly pass" above — **do not infer it from the
authored values**, which are descending in five of six near cases. The camera distance has to reach
`Puffer._Process`; that is new plumbing — prefer passing the active camera's global position in
rather than reaching for a singleton. The 1-pixel cull is a resolution-dependent optimisation, not a
look: **implement the fade, skip the cull**, and say so in the commit.

**⚠ Addition (author's instruction, 2026-08-09): this must be switchable off.** The original's
distance handling carries a 1998 particle budget we no longer need. Implement it as config keys in
`Config.WarmTuningRegistry` alongside the existing `puffer.*SizeScale` block, **granular rather than
one boolean**, because the key conflates three mechanisms with different natures:

| Mechanism | Nature | Key | Default |
|---|---|---|---|
| Far alpha ramp across the band | **Authored look** — 238 puffers, bands hand-picked from 400→600 up to 3600 m | `puffer.distanceFade` | `true` |
| Hard discard past `FAR_FADE[1]` | **Performance** — this is the one to be able to disable | `puffer.farCull` | `true` |
| Hard cull inside `NEAR_FADE[0]` | **Artifact guard** — stops a screen-filling billboard when the camera flies through it | `puffer.nearCull` | `true` |

**Every key defaults to the original's behaviour.** A flag that ships off would be a silent
divergence wearing a config key, which is exactly what this plan exists to remove. Disabling the far
cull while keeping the ramp is the interesting setting — distant puffers stay drawn at their
authored alpha instead of vanishing at the budget line. Note that honouring
`PufferSetGlobalFadeFactor` (far band only, below 1.0 pushes the fade outward) gives a fourth,
*authored* knob for free; prefer wiring that to inventing a distance multiplier of our own.

⚠ Do not let the near cull default off on the argument that it is "also just performance" — it is
not, and the census shows it fires on `flame_ball`, `zepskinfire`, `partial_damage` and
`bhf_hangarboom`, all of which the camera can plausibly fly through.

**Model recommendation.** high — it introduces a new dependency (camera position) into a node that
currently has none, and where that seam goes is a design call.

**Verify.** A capture at a fixed pose of a chapter with authored fades, at two camera distances;
plus the `--effects-test` suite. Take a baseline — an unchanged screenshot is not evidence unless
some puffer in frame actually carries the key, so pick the pose from the census, not by eye.

**⚠ Traps.** The `COLORS` ramp already owns the alpha when present (`Puffer.cs:534-539`) and the
`FadeFor` life envelope owns it otherwise. The distance fade multiplies *both*; it does not replace
either. Getting the precedence wrong makes ramped puffers invisible.

## C8 ☐ `PRIORITY` inflates the sprite by `1 + K·PRIORITY`

**Goal.** The authored `PRIORITY` value has the effect on sprite size the original gives it.

**Evidence (confidence: traced).** `FUN_0054e6e0` scales the per-particle screen radius by
`(1 + K * particle[0x15])`, where `particle[0x15]` is the object's `PRIORITY` (`+0x70`, parser flag
`0x400000`) and `K` is `_DAT_00a06fb0` — `0x3c23d70a` = **0.01** on the software path,
`0x3ca3d70a` = **0.02** on the hardware path (`FUN_0054d9c0`). Default `PRIORITY` is 0, so the
factor is 1 unless authored. The effect is small — a `PRIORITY` of 10 is +10%/+20% — and it is
almost certainly a depth-priority constant reused as a size nudge, but it is what the code does.

**Approach.** Add `Priority` to `PufferState` (both parsers) and fold `1 + 0.02f * Priority` into
`BaseSize` at spawn. Use the hardware constant — we have no software path. One line each side of
C7's plumbing; land it right after C7.

**Model recommendation.** medium, low effort.

**Verify.** A headless assertion that a puffer with `PRIORITY = 10` spawns particles 20% larger
than the same state with `PRIORITY = 0`. Then confirm from the census how many puffers in the
install author a non-zero `PRIORITY` at all — if the answer is "none", close this as a disproof
instead and record the constant in `docs/formats/puffer.md`.

**⚠ Traps.** Do not use `PRIORITY` for draw ordering on the strength of its name — this pass found
only the size use. If a depth use exists it is elsewhere, and it needs its own trace.

## C9 ☐ Emission-accumulator rules: the 200 m teleport guard vs. our per-frame batch cap

**Goal.** Understand — and then match or deliberately diverge from — how the original stops a
teleporting emitter from vomiting a frame's worth of particles.

**Evidence (confidence: lead only).** `FUN_0054f8b0` accumulates into the emitter's interval
counter at `+0x48` under a guard: in **distance mode** (`+0x3c` set) it computes the frame's motion
length and **only accumulates when it is under 200.0** — a teleport/respawn guard — while in time
mode it always accumulates `dt`. Emission is then `floor(accumulator * (1/interval))` with the
remainder carried, and **there is no per-frame batch cap**. Ours has the inverse shape: a
`MaxSustainBatchesPerFrame = 8` cap on the time path (`Puffer.cs:319, 704`) and no distance guard
at all.

What is not established: whether 200 m is a world constant or a coincidence of this build, and
whether our cap has ever actually fired in a real session (it was added defensively against a long
hitch, per the comment at `Puffer.cs:317-319`).

**Approach.** Instrument first: log when the sustain cap clamps and when a distance emitter moves
>200 m in a frame, and run the 8-chapter regression plus a mission with the crash-debris
`spurtpuffer`s. If the cap never fires, replace it with the engine's guard (drop the cap, add the
200 m distance gate) and note the trade in the commit. If it does fire, keep both and say why.

**Model recommendation.** high — the outcome is a judgement about whether to match the original or
keep a defensive divergence, and it needs the instrumentation read correctly.

**Verify.** The instrumentation output over a full regression is the artefact. "The cap never
fires" is only evidence if the log line is proven able to fire — force it once with a deliberate
hitch before trusting a zero count (`docs/verification.md`).

**⚠ Traps.** This is the one item that may correctly land **no code**. A defensive cap that the
original does not have is a legitimate divergence if it is documented; the failure would be
removing it blind and eating a pool blowout on the next long frame.

---

# Wave D — record and re-judge

## D10 ☐ Write `docs/formats/puffer.md` and re-judge the fire TUNE pair against the corrected sim

**Goal.** The puffer runtime is documented from the disassembly, and the invented fire scales are
re-judged against a simulation that no longer has two factor-of-two errors in it.

**Evidence (confidence: traced, for the document; the re-judgement is by definition a fresh
measurement).** Everything in this plan's "What the data actually ships" section is the document's
raw material: the function map, the object and particle layouts, the render equation, the full key
list including the six keys we did not previously read. `puffer.fireRiseScale = 2.5` /
`fireLifetimeScale = 1.5` (`Puffer.cs:283-292`) are explicitly INVENTED against footage, chosen
because the authored numbers "integrate to a ~10–12 m column" against the original's much taller
plume — a judgement made while sprites were half-size and scatter was double-width.

**Approach.** Write `docs/formats/puffer.md` covering: the authored key list and which of them we
implement, the object/particle layouts, the render equation with the radius convention called out,
and the three emission modes. Correct `docs/formats/anim-definitions.md:1180-1182`'s
`growth_factors` reading (B3's finding). Then re-run the `large_30sec_fire` column measurement from
`backlog.md:187-191` against the corrected sim and decide, with a number, whether the fire scales
are still needed — if they are, restate *why* citing the new measurement; if they are not, delete
them and their config keys.

**Model recommendation.** high — the re-judgement decides whether two invented constants survive,
and the document is the artefact the next session will trust without re-checking.

**Verify.** The document is reviewable against the addresses it cites — every claim in it names a
function. The fire re-judgement needs a capture at the controls against
`OriginalScreenshots/C1 IA1 Burning Fuel Tanks.png` and the `C1 IA1 Destruction.mp4` t≈176 s
columns, which is the same evidence the original judgement used, so the comparison is like for like.

**⚠ Traps.** Do not delete the fire scales just because the plan's other items landed — that is
the same reasoning error in the opposite direction. They come out only if a measurement says the
column now reaches without them.
