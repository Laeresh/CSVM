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

| 5 | The engine's born-dead skip (`if (age0 >= life)`) is unreachable with the shipped data — B4's own conclusion | Died at **B5**. `age0` is the `START_AGE_RANGE` draw **plus** `(1 − frac)·dt`, the sub-frame term. B4 compared only the authored key (max 0.1 s) against the minimum lifetime (1.0 s) and so was right about the key and wrong about the guard: any long frame reaches it, for **any** puffer, with no `START_AGE_RANGE` authored at all. A 5 s hitch hands the earliest catch-up batch a start age of ~4.8 s. |

**⚠ Standing rule on video evidence.** Frame-measured distances from the original footage
(`OriginalScreenshots/`, the `CAP-nn` clips) are **weak evidence** here and have misled this project
more than once. They are usable for *qualitative* reads — is there a plume, does it rise, roughly
how long does it last — and not for deriving or contesting a constant. Where a decode and a footage
measurement disagree, **the decode wins and the disagreement is a note, not an open question.**

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B3, B4, B5, **B6**, C7, C8 | Confirm the trace, then implement. |
| ~~Direction sound, magnitude a judgement call~~ | ~~B6~~ | **Emptied.** B6 was here because the wind's authored source was untraced, making its magnitude TUNE. It is authored — every `weather.zrd.json` ships a `WIND` block — so B6 moved up to traced and this row now has no members. |
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

3. ❌ `SCALE_SEQUENCE`: the age→scale ramp — **disproven, unreachable**; landed as a doc correction
4. ☑ `START_AGE_RANGE`: random birth age (⚠ its born-dead disproof was **wrong** — corrected by B5)
5. ☑ Sub-frame emission — spread a frame's puffs along the emitter's motion segment
6. ☑ Friction damps toward the wind, not toward zero — **and the wind is authored, not TUNE**

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

## B3 ❌ `SCALE_SEQUENCE`: the age→scale ramp — disproven, closed as a doc correction

**Closed as a disproof — no simulation code was written, and none was needed.** The census gate the
item set for itself came back empty and the item stopped there, as the ground rules require.
Independently re-derived over 17,569 extracted JSON files: `SCALE_SEQUENCE` appears **zero times**
anywhere in this install (0 of 1,293 reader files, 879 `PUFFER_STATE` blocks; 0 in the compiled
surface), and of the 2,906 events authoring `growth_factors`, **every single one has exactly two
stops** with entry 0 = `(0.0, 1.0)`. So `Mathf.Lerp(1f, GrowthFactor, lifeFrac)` is not "right for
two-stop puffers and silently wrong for others" — it is right for **100%** of this install, and it
stays untouched.

**What the item established instead, and what was corrected.**
1. `growth_factors[i]` is `(age_i, scale_i)`, **proven from the data alone**: 216 events author a
   second entry whose "max" is below its "min", down to `(1.0, −0.2)` — coherent as a stop,
   incoherent as a range.
2. The `anim-definitions.md:1180` "matches 172 of 177 puffers / five name collisions" claim
   **does not reproduce** — re-running its own denominator gives *zero* mismatches; the 31 apparent
   ones differ by 1.9e-07, float32↔float64 print noise. Withdrawn.
3. The real cause is an **undocumented reader idiom**: a `PUFFER_STATE`'s own `NAME` (a namespace
   separate from `AT_NODE`) can carry a `*` wildcard and expands at compile time. Exactly three do,
   and they expand into exactly the 7 compiled names with no reader definition — and nothing else.
4. Genuine collisions are a different thing and do exist: 17 reader names carry more than one
   distinct `GROWTH_FACTOR` (`smokerpuff` spans 4.0 to 85.0), each resolved per file. None is a
   multi-stop ramp. Compiled↔reader disagreements, once wildcards expand: none, on any key.

**A wrong data shape found and removed.** `AnimDefs.AddPufferState` synthesised a *single*-entry
`growth_factors = [{min: 0, max: G}]`, which under the corrected reading literally encodes
*"at age 0, scale G"*. It yielded the right number only via a compensating `Count == 1` fallback in
`FromAnimEvent`. It now emits the engine's two stops, `(0,1)` and `(1,G)`, so the normalised reader
shape is identical to the compiled one. Value-preserving.

**Verified.** Full `RunTests.ps1` green — 871 units, 29 engine suites, 13/13 goldens
hash-identical. No behaviour change, so no golden or chapter movement was expected or seen.

### Original approach (kept for reference)

## B3 (original) `SCALE_SEQUENCE`: the age→scale ramp, of which `GROWTH_FACTOR` is the two-stop case

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

## B4 ☑ `START_AGE_RANGE`: random birth age (the born-dead skip is a documented disproof)

**Landed.** `PufferState` gains `StartAgeMin`/`StartAgeMax`, populated in **both** parsers
(`start_age_range` compiled, `START_AGE_RANGE` reader), and all three spawn paths seed
`Particle.Age` from `Rand(StartAgeMin, StartAgeMax)` instead of the literal `0f`, positioned
immediately after the lifetime draw to match the engine's own lifetime-then-start-age order as
closely as our load-bearing pos → vel → size → life order allows.

**The extra draw is gated** on a `HasStartAgeRange` predicate, so the ~2,900 puffers that do not
author the key consume no additional `_rng` draw and stay bit-identical. This was the item's main
risk — an unconditional draw would have re-scattered every emitter in the game.

**Negative ages work as the engine works them.** `lifeFrac` is now
`p.Age > 0f ? p.Age / p.Life : 0f`, mirroring `FUN_0054e6e0`'s `if (0.0 < age)` clamp. A
negative-age particle is **drawn on the frame it is born**, pinned to stop 0 of every ramp and
envelope, while `p.Age` keeps integrating and reaping normally — so it outlives its authored
lifetime by `|age0|`. Stagger-and-hold, not a spawn delay.

**⚠ CORRECTED BY B5 — the born-dead skip was NOT a disproof.** B4 closed the engine's
`if (age0 >= life)` guard as unreachable dead code, reasoning that across the four authoring
puffers the maximum authored start age is 0.1 s and the minimum authored lifetime is 1.0 s. That is
right about the key and **wrong about the guard**. The engine's `age0` is not the `START_AGE_RANGE`
draw — it is that draw **plus `(1 − frac)·dt`**, the sub-frame term of the time-cadence spawn.
B4's own Evidence section states the full formula; the disproof then tested only half of it. The
sub-frame term makes the skip reachable on **any long frame, for any puffer, with no
`START_AGE_RANGE` authored anywhere in the install**: measured on the `puffer-modes` state
(`TIME_INTERVAL` 0.2, `LIFETIME_RANGE` 0.8–1.0), a 5 s hitch's eight capped catch-up batches are
born at 4.80, 4.60, 4.40, 4.20, 4.00, 3.80, 3.60 and 3.41 s — all 144 particles born past their own
lifetime. The skip is implemented in all three spawn paths and lands with B5, which is what made it
reachable. This is not a defect in the engine's data: a batch whose virtual emission moment was
4.8 s ago really *is* 4.8 s old. The skip is the engine's answer to its own sub-frame term, and the
two mechanisms only make sense together — which is why B4, holding one without the other, could not
see it.

**Verified.** Full `RunTests.ps1` green — **871 units** (4 new `START_AGE_RANGE` assertions),
**29/29 engine suites**, **13/13 goldens hash-identical**. 8-chapter `--freecam --det --frames=90`
sweep: all eight completed and produced their screenshots, zero real errors (the single `ERROR`
substring in C1's log is `godot_variant_call_error` inside a stack trace attached to the expected
headless "no audio session" sound warning, not a failure). Unchanged goldens are the *expected*
result here, since none of the four authoring puffers (`fire_at_zepskin2/3`, `depotfirepuff`,
`fire_at_hydrotank`) appears in a golden pose.

**⚠ Unverified.** The gate's no-extra-draw property is argued from the code and corroborated by 13
unchanged goldens, but no deliberate able-to-fail control was run against it (`docs/verification.md`
would want one before treating "nothing moved" as proof). Visual confirmation of the four affected
puffers — the zeppelin-skin fires, the C1 fuel-truck and the C3 hydrogen tank — is owed at the
controls and has not been done.

### Original approach (kept for reference)

## B4 (original) `START_AGE_RANGE`: random birth age, sub-frame age offset, and the born-dead skip

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

## B5 ☑ Sub-frame emission — spread a frame's puffs along the emitter's motion segment

**Landed.** `SustainAt` keeps the previous world origin and spawns batch `b` at
`prevOrigin.Lerp(origin, frac)` with `frac = (b+1)·interval / accumulator`, carrying the engine's
matching `(1 − frac)·dt` onto the drawn start age (`FUN_0054f8b0`). The first frame after a
`Stop()`/revive re-homes the previous origin to the current pose rather than trailing from a stale
one — `TrailAdvance`'s existing ghost-trail rule (commit `450131a`), and the rocket-explosion bug if
it is missed. The age offset is computed, not drawn, so it adds no `_rng` call.

**⚠ An invented clamp was found and removed.** The first attempt at this item shipped
`ageDt = Mathf.Min(dt, interval)`, with a comment stating plainly that it existed because the
unclamped term made `puffer-modes`' 5 s hitch case regress from 108 to 18. The engine has no such
clamp. It was invented compensation keeping physically-dead particles alive — the exact failure
mode this plan's ground rules name — and it is precisely what the engine's born-dead skip exists to
make unnecessary. Removed; the offset rides the raw `dt`.

**The born-dead skip landed here**, in all three spawn paths, because this item is what made it
reachable (see B4's corrected section). Two properties were engineered deliberately: it **consumes
no pool slot** (`_liveCount` does not advance; the `k`-loop goes on to try the rest of `NUMBER`),
and it **does not perturb the RNG stream** — every draw (pos, vel, size, life, start age, frame) is
made into locals *before* the skip decides, so a skipped particle burns the same draws a created one
would and cannot re-scatter its neighbours.

**The `puffer-modes` hitch expectation was rewritten**, not relaxed. It had encoded a model the
engine contradicts — that every catch-up batch is born at age 0. It now asserts two things across
two frames: (1) after the 5 s hitch, `LiveCount` is still 18, read **before** `_Process` so it is
the spawn path under test and not the reaper tidying up; and (2) the pool-overrun guard, kept and
made *more* discriminating — the 8-batch cap leaves 3.4167 s of accumulator carried, so the next
frame emits its 8 batches at `dt = 1/60` whose offsets are milliseconds and which therefore all
live: 144 spawns against 90 free slots, asserted to clamp at exactly 108 and reach `MaxIndex` 107.
That second frame landing 90 spawns is itself the proof the hitch's batches were genuinely
*attempted and discarded*, since a bare `dt = 1/60` with no carry emits no batch at all.

**Verified.** Able-to-fail control on a real build: with the skip neutered, the hitch's `LiveCount`
reads **108** (the born-dead batches fill the pool) instead of 18 — both directions observed. Full
`RunTests.ps1` green: **871 units, 29/29 engine suites, 13/13 goldens**, confirmed on an independent
re-run by the orchestrator. 8-chapter `--freecam --det --frames=90` sweep: all eight exit 0, **zero**
`ERROR` lines, every screenshot produced.

**⚠ The goldens manifest had been silently re-baselined** by an earlier attempt's orphaned
background process, so three consecutive full runs reported a false "13/13 hash-identical" for a
change that had moved six shots; `git diff` on the manifest is what caught it. Now recorded as
`GOLD-9` in `docs/verification.md`. Before-images were recovered by neutralising *only* the age
offset, a build which reproduced HEAD's six committed hashes digit for digit — which also proves the
entire golden delta is the sub-frame age term and nothing else. A second control forcing `frac = 0`
changed nothing at all, so **every sustained emitter in every golden pose is stationary** and the
position interpolation is an exact no-op there. The six moves (`c1-waterfall`, `c3-island`,
`c4-snow`, `c5-city-night`, `c1-destroy-effects`, `c1-crash`) are birth-age shifts only: max channel
delta 1 (3 on `c1-crash`), **zero** pixels differing by more than 8/255 in any shot, every change
bounding box landing on an emitter and nowhere else. Eye-checked before/after: indistinguishable in
all six.

**⚠ Unverified.** **No golden and no capture carries a *moving* sustained emitter** — the `frac = 0`
control proves it — so B5's headline case, the C1 train's smokestack laying a line instead of a
clump, is proven only by headless assertions and **is owed at the controls**. The trail and burst
paths' skips are unreachable with today's data (neither has a sub-frame term, and no puffer authors
a `START_AGE_RANGE` reaching a lifetime); they are reproduced for fidelity, not because anything
reaches them — as dead today as B4 claimed the sustained one was. No node/mesh instance counts were
read across the sweep, only the error census.

### Original approach (kept for reference)

## B5 (original) Sub-frame emission — spread a frame's puffs along the emitter's motion segment

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

## B6 ☑ Friction damps toward the wind, not toward zero

**Landed — including half 2, which the plan expected might dead-end.** The wind's authored source is
found: **every one of the 53 `weather.zrd.json` in the install ships a `WIND` block**, and all 53
author it *identically* — `STATIC_VELOCITY (0,2,0)`, `RANDOM_MAX_SPEED 10`, `RANDOM_ACCEL 5`,
`RANDOM_ANG_VEL 5`. A steady 2 m/s updraft under a gust wandering a 10 m/s horizontal disc, in every
mission. **The magnitude is therefore data, not TUNE**, and B6 moved into the traced row.

**The key→global mapping, confirmed at two independent call sites**: the weather reader
`FUN_004bc680` (assert string `D:\zipper\Crimson\weather.cpp`) and the debug console `FUN_005b80a0`,
which names the same four setters `GlobalWindStaticVelocity` / `GlobalWindRandomMaxSpeed` /
`GlobalWindRandomAccel` / `GlobalWindRandomAngVel` — the original's own name for the mechanism.

| `WIND` key | Global | Setter |
|---|---|---|
| `STATIC_VELOCITY` | `00763db4`/`b8`/`bc` | `FUN_00550690` |
| `RANDOM_MAX_SPEED` | `00763dc8` | `FUN_005506b0` |
| `RANDOM_ACCEL` | **`00763dd0`** (the plan said `00763dc4`, which is the magnitude *state*) | `FUN_005506c0` |
| `RANDOM_ANG_VEL` | **`00763dcc`** (corrected) | `FUN_005506d0` |

**Three findings the earlier trace did not have**, all in raw x87: `RANDOM_ANG_VEL` is in
**degrees/s** (`FUN_004bc680` multiplies by `0.017453292` on the way in); the magnitude step carries
**no `dt`** while the heading step does — one `±RANDOM_ACCEL` jump per *frame*, i.e. frame-rate
dependent, **reproduced as traced rather than smoothed**, because a `dt` nobody wrote would be an
invented breeze; and the gust is **horizontal** — `wind.y` is `STATIC_VELOCITY.y` copied verbatim
(`0054ef5f`), only x/z carry `mag·cos/sin(heading)`.

**⚠ `WIND_FACTOR`'s default is 1, not 0 — and this plan had it backwards.** `FUN_00550100` writes
`0x3f800000` to `+0x6c` and `FUN_004e7e40` only overwrites it under flag `0x100000`, so **a puffer
that says nothing about wind is fully carried by it**. Reading an absent key as 0 would have becalmed
2,802 of the install's 2,863 friction-bearing events. Absent and explicit-zero must stay
distinguishable, and they are (`wind_factor: null` vs `0.0`). Effective factor across the
friction-bearing events: **1.0 (default) on 2,802 / 225 names**, 0.3 on 29, 0.1 on 19, 0.0 on 12
(the six `subdoors_puffer*` opting out), 0.5 on 1. Note also that only **5 reader blocks** author the
key at all — the plan's "27" was the distinct *compiled* name count.

**The integration order landed in full**, beyond the plan's accel-before-damp: `FUN_0054ee10`
advances position on the velocity from the **top of the frame** (`0054efd0`–`0054eff0` read
`ESI+0xc` before `0054eff8` writes it), *then* adds `a·dt`, and only then damps — and the damp sits
inside the engine's own `if (friction != 0)` gate (`0054f016`). Ours did all three the other way
round. That gate used to be an `Exp(0) == 1` identity in our code; with a wind inside the block it is
load-bearing.

**The seam is injected, not a singleton.** `Effects/WorldWind.cs` carries the gust model and
`EffectAmbience`, the per-frame world state a `Puffer` reads but does not own. `GameSession` owns
the single instance (it must reach the emitter factories at `StartSession`, long before `WeatherRig`
exists); `WeatherRig.Tick` writes it **once per frame before the rig loop** — one wind for the world,
not one per camera. `EffectAmbience.Still` is a null object that *throws* if written, so a missed
wire fails loudly instead of silently becalming an emitter. **C7's camera position belongs on this
same seam** as a second property. The gust draws off its own `Rng.Wind` stream so its frame count can
never perturb an emitter's spawn scatter.

**Verified.** 882 units (11 new), 30/30 engine suites (new `puffer-wind`), 13/13 goldens — baseline
before the change was 871/29/13, and the whole run was confirmed independently by the orchestrator.
8-chapter `--freecam --det --frames=90`: 8/8 exit 0, 8/8 screenshots, 8/8 logs, zero real errors.
Manifest diff reviewed before trusting the golden result (`GOLD-9`).

**Six goldens moved, and a zero-wind able-to-fail control splits them cleanly.** `c1-waterfall`
(5,915 px, max Δ 6), `c3-island` (39 px, max 8) and `c4-snow` (5 px, max 2) reproduce the zero-wind
hashes *digit for digit* — they moved on the reorder alone, and their emitters carry `FRICTION 0`, so
the gate keeps them out of the wind entirely. `c1-flight` (10,354 px, max 38), `c1-destroy-effects`
(3,496 px, max 66) and `c1-crash` (67,750 px, max 140) hash *differently* with and without wind,
which is the proof it reaches real puffers and not only the suite's synthetic ones. Eye-checked:
`c1-crash`'s fireball keeps its size, shape and colour and shifts bodily a few pixels with a slightly
tilted plume; `c1-flight`'s two wing damage-trails are subtly narrower and displaced;
`c1-destroy-effects` is indistinguishable side by side, its amplified diff tracing the airframe
outline (a sprite *behind* the plane). `c5-city-night` is the one puffer-bearing golden left
byte-identical — its emitter has neither friction nor world acceleration, making the reorder an exact
algebraic identity there.

**⚠ Unverified.** Nothing was checked **at the controls** — whether a 10 m/s wandering gust *looks*
right on the C1 train plume, the waterfall mist or a rocket trail is owed to the user, and this is
the item most likely to read wrong despite being traced correctly. The gust's frame-rate dependence
is deliberate and unaddressed. `WorldWind`'s `rand()` quantisation matches the engine
(`rand()/16384 − 1`) but the underlying generator is .NET's, not MSVC's — sequences differ,
distributions do not. The 0.3/0.1/0.5 `WIND_FACTOR` puffers (chimneys, train, torches, markers)
appear in no golden pose, so their coupling is proven only by the headless linearity assertion. Node
and mesh instance counts were not read across the sweep.

### Original approach (kept for reference)

## B6 (original) Friction damps toward the wind, not toward zero

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
