# Milestone 5 polish, run 3: the frames the effects runtime drops

**ACTIVE PLAN** (written 2026-08-27). It sits in `docs/`, which by this repo's convention makes it a
live plan. **It is queued behind [`PLAN-M5-polish-2.md`](PLAN-M5-polish-2.md), which is the plan
PROJECT_CONTEXT.md's "Current status" names and keeps naming until it archives.** Move this file to
`docs/plans/` with a `COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every
item lands.

This is the third polish run over Milestone 5, weighted **frame-time and hitches first** by user
decision. The backlog's true hitch cluster is three items (`BL-355`, `BL-418`, `BL-231`), so the run
is filled out with the adjacent per-frame costs the effects and clutter runtimes carry (`BL-337`,
`BL-336`, `BL-218`), one animation-runtime correctness item on the same subsystem (`BL-334`), and
the two flight-model items cheap enough to admit under the boundary below (`BL-450`, `BL-453`).
`BL-300` closes the set as the one collision-shape item with a reported symptom behind it.

**Every item here was re-verified still-open in this session against both the record
(`git log --grep=<ID>`) and the backlog entry's own cited `file:line`.** All ten IDs are filed-by,
not closed-by, their `git log` hits. Two hits looked like closures and were read at the commit
message: `4efe3e99` fixed the pooled copy's re-reset and its body explicitly files `BL-418` as the
separate first-play hitch, and `98948b25` (`B12`) files `BL-334` as the residual divergence it chose
not to implement.

**Deliberately out of scope.** The eleven campaign items are `PLAN-M5-polish-2`'s (`BL-507`,
`BL-497`, `BL-499`, `BL-457`, `BL-491`, `BL-484`, `BL-482`, `BL-503`, `BL-460`, `BL-483`, `BL-458`);
none of them recurs here, and this plan does not touch the campaign path at all. `BL-508` (the
scissor path has no original to reproduce) is out despite being a render-cost item: it is a per-family
faithfulness decision over 388 textures, plan-sized, and moving thousands of coplanar foliage cards
into the transparent pass is a frame-time risk this run would have to measure before it could even
start. `BL-328` (the deck floor's annulus half-span) is out on the same measurement-first ground and
because nothing is broken today. `BL-341`, `BL-419` and `BL-293` are effects-adjacent but are look
questions rather than cost ones.

## Milestone goal

- The crash and damage cascade no longer builds emitters and materials inside the frame that plays
  them, so a `DamageLab` sweep and a crash stop tripping `HitchMonitor`.
- The sonic burst's nine puffers exist before the first burst fires, by the same mechanism.
- `effect_pools.json`'s sizes are re-judged against a build where the first-use cost is gone, so the
  recycle instrument measures concurrency rather than construction.
- Distant clutter fades the way the authored `far_fade_range` says it should, at the detail level
  that scales it, so the draw is not carried to the horizon.
- An unauthored puffer emits at the original's cadence rather than ten times it.
- An AI spawned by the mission spawner gets the roster block's fields, and a dry tank freezes the
  lever the way the original's does.

**This plan does not open the flight model's force path, and does not re-tune any decoded constant.**
`BL-450` and `BL-453` are admitted because both are additive features against undisputed decodes
(a burn term that does not exist yet, and a roster field with no live producer), not because the
theme is open. Anything that would move `FlightScenarios`, the `*Tune` rates or the equilibrium
curve belongs to a flight-model plan and not to a polish run.

## Decisions (2026-08-27)

| # | Question | Decision |
|---|---|---|
| 1 | What weights the ten-item selection? | **Frame-time and hitches first.** Chosen by the user over seen-in-flight fidelity, closing the TUNE list, and unblocking the blocked. The user was told in advance that the backlog holds fewer than ten strong candidates on this weight and accepted the padding. |
| 2 | What fills the gap after the three real hitch items? | **Adjacent per-frame cost on the same runtimes**, in this order: the clutter draw the authored fade would remove (`BL-337`), the emission rate that is ten times the original's (`BL-336`/`BL-218`), then correctness on the same anim runtime (`BL-334`). |
| 3 | How are at-the-controls items handled? | **In, and the plan stops for them.** `A3` and `B6` each halt at an explicit stop rather than letting a suite stand in for the judgement; `C11` is the flown session that carries the stops needing one sortie. |
| 4 | Does the plan reach the flight model? | **The cheap ones only.** `BL-450` and `BL-453` are in; `BL-443`, `BL-447`, `BL-448` and `BL-456` are out. |
| 5 | Can this plan run while `PLAN-M5-polish-2` is live? | **No, it queues behind it.** Every landing commit rewrites PROJECT_CONTEXT.md's "Current status", one block that both plans would fight over, and polish-2's own `A1` exists to settle that block. Polish-2 archives first. |
| 6 | `BL-300` or `BL-328` for the tenth slot? | **`BL-300`.** It has a user-reported symptom (close-stunt false crashes from box overhang) and a stated contract to keep; `BL-328` has an admittedly dead derivation but nothing visibly wrong. |

## ⚠ Read this before implementing anything

These claims were made about items in this plan and are dead. They are recorded here rather than
only inside their item, because each one is plausible enough to be re-derived by a reader who opens
a different item first.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The damage/crash hitch is GC (`BL-355`). | `gc0_delta`/`gc1_delta`/`gc2_delta` are **0** on both hitching frames. No collection of any generation fired. |
| 2 | The damage/crash hitch is allocation volume (`BL-355`). | `allocated_bytes_delta` is 300 to 350 KB per hitching frame, three orders of magnitude under the ~860 MB burst `PLAN-perf-hitches` `B5` needed to move those columns at all. |
| 3 | The damage/crash hitch is GPU or render cost (`BL-355`). | `render_cpu_ms`/`gpu_ms` hold their normal ~0.5/0.2 ms on both frames; the whole cost is inside the CPU/script span `frame_ms` measures. |
| 4 | Raising `effect_pools.json` sizes fixes either first-use hitch (`BL-355`, `BL-418`). | A pool avoids RELOCATING an already-built emitter onto a new call and does nothing for the first build of a distinct `(name, host, def)` key. `PoolRecycles` is 0 through both captures, and `large_firetrail` was sized 6 with 3 pieces in flight. |
| 5 | `BL-418` was fixed by the pooled-copy re-reset. | `4efe3e99` fixed the rings vanishing from the fifth burst on (`AnimRuntime.ResetCheckedOutCopies`) and its own body files `BL-418` as the separate first-play construction hitch. |
| 6 | The puffer `NUMBER` default is too low, leaving unnumbered emitters thin (`BL-218`). | Withdrawn. The puffer ctor `FUN_00550100` writes `1` to `+0x04` before any authored key applies, so our 1 is the original's. The sibling `large_10sec_fire`'s `NUMBER 3` is that puffer's own authored value and was never evidence about the unauthored case. **Do not raise the fallback.** |
| 7 | `BL-336` is one constant swapped from `0.1` to `1.0`. | The same `0.1` does double duty at `Puffer.cs:174` as the synthetic still-host sputter cadence handed to every DISTANCE state, which is our own invention for a mode the engine does not have. Moving that one would make every static building's sputter ten times slower. |
| 8 | A build-time CLI preset can be used to verify a hitch fix. | `docs/verification.md` PERF-14: a preset can never trip `HitchMonitor`. `--crash=300` under `--no-vsync` is the live, scriptable event that does. |
| 9 | `far_fade_range`'s authored metres are literal distances (`BL-337`). | They are a base scaled by the graphics detail level: `FUN_00440750` writes 1.0/4.0/9.0 to `_DAT_0062d170` for levels 0/1/2, so fade distance scales ×1/×2/×3, before any per-mission script override. |
| 10 | Nothing in CSVM renders a `far_fade_range` (`BL-337`'s "read but not applied"). | Half wrong, and this is the shape to copy rather than a gap to invent: `FogVolumeClutter.cs:263-274` declares a `far_fade` uniform with a `smoothstep` alpha and a `step` cull, fed at `:533` from the fog-volume block. It is the **templates** clutter path that stores the pair (`ClutterTemplates.cs:45,51,282-283`) and ignores it. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B4, B5, C9, C10 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | A3, B6 | The mechanism is settled and the instrument exists; the number is TUNE and ends at the controls. |
| **Leads only, no mechanism yet** | C7, C8 | Budget for investigation. C7 in particular is expected to end in a disproof rather than a fix. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen, never append) and is **deleted** from
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

### Wave A — first-use construction, the named mechanism

1. ☐ Pre-warm the crash and damage-stage emitter keys (`BL-355`)
2. ☐ Pre-warm the sonic burst's nine puffer keys at stage build (`BL-418`)
3. ☐ Re-judge `effect_pools.json` against a build with no first-use cost (`BL-231`)

### Wave B — the per-frame cost the data authors and we ignore

4. ☐ Apply `far_fade_range` on the templates clutter path, with its detail scale (`BL-337`)
5. ☐ An unauthored puffer `TIME_INTERVAL` is `1.0` s, not our `0.1` (`BL-336`)
6. ☐ Judge puffer density at the controls now the fire's shape is right (`BL-218`)

### Wave C — the same runtime's correctness, and the two cheap features

7. ☐ A stopped sequence stays callable; instrument before implementing (`BL-334`)
8. ☐ Tighter aircraft collision shapes, convex hulls per clipped region (`BL-300`)
9. ☐ Fuel burn and the empty-tank lever freeze (`BL-450`)
10. ☐ The mission spawner does not read roster blocks (`BL-453`)
11. ☐ The flown session: the hitch sortie and the two density judgements

## Dependency and parallelism notes

**A1 blocks A2 in practice, and both block A3.** A1 and A2 are the same mechanism on two hosts (the
crash rig and the world-effects stage), so A1 settles the pre-warm shape and A2 applies it; running
them in parallel means two sessions inventing the same seam. A3 must come last of the three, because
its whole instrument is `AnimRuntime.PoolRecycles` and the miss counter, and a build that still pays
first-use construction cannot tell an undersized pool from an unbuilt key.

**A1, A2 and A3 all reach the effects runtime**, specifically `EmitterDirector`,
`WorldEffectsFactory` and `AnimRuntime`'s staging path. Do not run them in parallel worktrees.

**B5 blocks B6.** `BL-218`'s density judgement is a time series over sprite count, and `BL-336`
changes the emission interval by a factor of ten on exactly the states that do not author one, so a
density verdict taken before B5 lands measures the wrong build.

B4 is independent of everything else here and can run in its own worktree: it touches the clutter
build and its shader, which nothing else in this plan writes.

C7 reads `AnimRuntime`/`AnimInstance` and so contends with Wave A; sequence it after A2 or give it a
stated ownership split. C8, C9 and C10 are independent of the effects runtime and of each other
(collision shapes, the throttle path, the spawn path) and can run in parallel.

C11 runs last by construction: it is the sortie that judges A1, A2, A3, B5, B6 and C8 at the
controls.

---

# Wave A — first-use construction, the named mechanism

## A1 ☐ Pre-warm the crash and damage-stage emitter keys

**Goal.** A crash, and a sweep through the `DamageLab` panel, play their effects without building
emitters or materials in the frame that needs them, so neither trips `HitchMonitor`.

**Evidence (confidence: traced).** `BL-355`, diagnosed under the frame-hitch instrument
(`PLAN-perf-hitches` G15/G16). `--crash=300 --no-vsync` tripped `HitchMonitor` twice: frame 300 at
`frame_ms=48.43` with `samples=part_detach:1x33.01`, frame 301 at `frame_ms=62.11` with
`samples=effect_pool_miss:7x54.36`. The mechanism was traced live with a temporary, reverted
`GD.Print` in the miss branch: the crash's own dispatch names ten distinct first-time misses in the
same one-two frames, `lgpuffer` on `piece1`/`piece3`/`piece4`, `spurtpuffer1..5` on `fly_trail1..5`,
`fierypuffer` on `flame_ball_01`, and `trailpuffer2` on `yellow_spark_01`. Each is a
`(name, host, def)` key `EmitterDirector.Assert` has never seen, and each pays `_factory.Create`'s
full build synchronously, a `Puffer` plus `EmitterRenderer.Attach`'s `MaterialCreate` nested inside
the same scope. The scope is `PerfSample.Scope(PerfSite.EffectPoolMiss)` at
`CSVM/src/Mech3/Anim/EmitterDirector.cs:115`, and `EmitterRenderer.cs:118` records that the material
build is absorbed into it. Confirmed at the controls in a real interactive session, not just the
proxy: a `DamageLab` panel sweep tripped `HitchMonitor` 31 times in about 7 seconds, and 24 of the 25
records that survived name `effect_pool_miss`. The cost **recurs** across a session, because there
are enough distinct keys (8 parts × armour and health × several `injure_anims` thresholds) that a
real sweep keeps finding new ones. See ⚠ rows 1 to 4 for what this is not.

**Approach.** Construct each `crashRoots` and damage-stage emitter once, off the frame that needs it:
at plane spawn, at session build, or on a loading beat. `StartupProfile`'s existing `prewarm` phase
is the idea to follow, and `CombatVoice.SessionPrewarmNames`/`WorldSounds.Prewarm` are the shape
already in the repo for "decode the roster's subset, not the whole bank", and the same reasoning
applies to picking which emitter keys are worth building up front. The fallback, if a full pre-warm proves too
broad, is to spread one compound event's misses across several frames instead of one dispatch batch.

**Model recommendation.** high. The seam choice (where the pre-warm runs, and which subset it covers)
sets the shape A2 then follows, and a pre-warm that builds every key in the install would trade a
mid-flight hitch for a load-time cost nobody measured.

**Verify.** `--crash=300 --no-vsync` on `--fly --chapter=C1 --plane=player_bhawk` shows no
`HitchMonitor` trip whose samples name `effect_pool_miss`, against the recorded baseline of two trips
at 48.43 and 62.11 ms. Take that baseline again on the current tree first: the numbers above are from
the diagnosing session, and an unchanged count is not evidence unless it has been seen able to fail.
Then `RunTests.ps1` in full, since the staging path is under the golden set. The `DamageLab` half is
judged in C11.

**⚠ Traps.** ⚠ `docs/verification.md` PERF-14: a build-time CLI preset can never trip `HitchMonitor`,
so the verification has to use the live `--crash=` event. ⚠ PERF-13: a hitch count only compares
across runs in the same vsync mode. ⚠ PERF-15: a flat-leaf sampler is only negligible outside
per-particle loops, so do not add a `PerfSample` scope inside the emitter's own step to measure this.
⚠ The 25th trip in the controls capture (`frame_ms=79.91`, `samples=[]`, every counter at baseline) is
unexplained by this mechanism and is not this item's; do not chase it here. ⚠ `BL-356` (the sidecar
dropping records under a hitch storm) is already fixed in `73512b47`; do not re-file it.

*Cross-refs:* `PLAN-perf-hitches` G15/G16, `docs/verification.md` PERF-14.

## A2 ☐ Pre-warm the sonic burst's nine puffer keys at stage build

**Goal.** The first sonic burst finds every emitter it needs already built, so the first plays cost
what the fifth does.

**Evidence (confidence: traced).** `BL-418`, the `BL-355` mechanism on the world-effects runtime. The
weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08 --weapon-fire --infinite-ammo
--weapon-surface=default --weapon-standoff=90`) trips `HitchMonitor` on the first bursts at sim
frames 141, 202 and 263, one per fresh slot copy, with 130 to 290 ms frames whose samples read
`effect_pool_miss:8x`, and once with a 70 ms frame naming `effect_checkout` alone. `PoolRecycles`
stays 0 throughout. `sonic_ground_effect` calls nine puffer defs (`sonic_puff1`, `sonic_puff4` through
`sonic_puff11`, in `extracted/zrdr/sonic_control.zrd.json`), and each first `PUFFER_STATE 1` on a
never-seen key takes the same miss branch as A1. With four pool slots per root, four bursts each pay
it once per slot copy before every key exists.

**Approach.** Pre-warm those emitter keys at stage build, in
`WorldEffectsFactory.BuildWorldEffectsRuntime`, once per pool copy, following whatever seam A1
settles. `AnimRuntime.cs:34` documents that every staged template copy lives under that build, and
`AnimProgram.Subset` (`AnimRuntime.cs:437`, memoized per anim name) already computes the set of defs
an anim reaches through `CALL_ANIMATION`, which is the same reachability question a pre-warm has to
answer. This is not a pool-size change; `effect_pools.json` sizes concurrency, not first construction.

**Model recommendation.** medium. The mechanism, the probe and the fix shape are all settled by A1;
the work is applying them at a second host and getting the per-slot-copy loop right.

**Verify.** The same probe run to eight bursts shows no `effect_pool_miss` sample after the build and
no `HitchMonitor` trip naming `effect_checkout`. Take the three-trip baseline first. Then the
`effect-pool-reset` suite, which `4efe3e99` added over exactly this stage and would catch a pre-warm
that leaves a copy in the wrong pose.

**⚠ Traps.** ⚠ `AnimRuntime.ResetCheckedOutCopies` (`AnimRuntime.cs:2000`, called at `:1193`) runs in
the same `effect_checkout` scope, so a hitch attributed to that site is this item's construction cost
until measured otherwise. ⚠ A pre-warm must not leave a copy started or posed: `4efe3e99`'s whole
finding is that a pooled copy starts from whatever END pose its last run left, and the re-reset is
what fixes that. Building an emitter is not the same as playing it, and the `effect-pool-reset` suite
is the guard. ⚠ See ⚠ rows 4 and 5.

## A3 ☐ Re-judge `effect_pools.json` against a build with no first-use cost

**Goal.** The shipped pool sizes are judged where concurrency is actually highest, on a build where a
`PoolRecycles` count means overlapping calls rather than unbuilt keys.

**Evidence (confidence: direction-sound; the instrument is real, the numbers are invented and always
will be).** `BL-231`. The original copies its template per call and has no such number, so any finite
pool is our approximation of "unbounded", which is why it is an editable file rather than a `const`.
Shipped today: default **4 base +1 per extra player**, `partial_damage_obj` **8 +1**, the three gun
roots **1 +0**, ceiling **16**, plus the separate `localCallRoots`/`localCallDefault` map with
`facdsticks` at base 6. The default came from rocket concurrency (`FIRE_RATE` 1/s against ~2.5 s of
authored trail motion, so at most 3 overlapping blasts, plus a spare); the sputter root came from
measurement, five simultaneous `ap_h2otwr` kills wrapping a 4-slot pool exactly once. The instrument
ships in the build: `AnimRuntime.PoolRecycles` counts every call that wrapped onto a still-live slot,
the runtime names the first per effect, and the world-effects build line prints the sizes staged.
**The per-player term and the ceiling are the two knobs a many-player build should re-judge**: at 16
players the default root wants 19 and gets 16.

**Approach.** Run the concurrency cases with the instrument reading: a rocket burst into a cluster of
destructibles, and a 4-player splitscreen session. Raise the specific root that logs a recycle, never
the default. Then stop for the user's judgement on whether the recycles that remain are visible, and
whether the ceiling should move for many-player builds.

**Model recommendation.** medium. Mechanical measurement against a shipped instrument; the judgement
is the user's and the ordering constraint is already stated.

**Verify.** A scripted run that logs no recycle had enough pool. The build's own world-effects line
gives the sizes staged, and `RunTests.ps1` covers the staging. ⚠ **This item ends at a stop:** the
question of whether a wrap is visible in play is not one a suite answers, and it rides C11.

**⚠ Traps.** ⚠ **This is explicitly not the fix for A1 or A2** (⚠ row 4); if this item is reached
before those land, its measurements are meaningless. ⚠ Raising a size is not free: each slot is one
more copy of that root's subtree (147 templates at 1 player, 252 at 4), so raising the default
multiplies world-build cost and memory for effects that are mostly not concurrent. ⚠ The three
gun-impact roots stay at **1** deliberately, because C8 throttles the gun family to one play per 0.1 s
per name; raising them belongs with removing that throttle, as its own step with its own emitter-count
check. ⚠ Sizing a root **0** does not disable pooling, it clamps to 1.

---

# Wave B — the per-frame cost the data authors and we ignore

## B4 ☐ Apply `far_fade_range` on the templates clutter path, with its detail scale

**Goal.** Distant templates clutter fades and stops drawing at the distance its own block authors,
scaled by the graphics detail level, instead of being drawn all the way out.

**Evidence (confidence: traced).** `BL-337`, deferred since Decision 3 of
`PLAN-clutter-uv-placement` because a fade that removes distant clutter would have confounded that
plan's density A/Bs. `far_fade_range` decodes to `[[nearMin, farMin], [nearMax, farMax]]`, both
distances drawn from **one** `rand()` per instance (`FUN_004dd6e0` step 11). It is authored on all
143 `templates.zrd` blocks: C5's city blocks fade 200 to 350 m, C1's firs 500 to 2000 m, and the
install spans 50 to 2000 m over 27 distinct pairs. `ClutterTemplates.cs:45,51` store the pair as
`FarFadeMin`/`FarFadeMax` (parsed at `:262`, assigned at `:282-283`) and `ClutterBuilder`
(`CSVM/src/Mech3/Clutter.cs:21`) does not use them. The runtime scale is a single global
`_DAT_0062d170`, written by the script command `CameraSetClutterFadeScaleSq` (dispatch `FUN_005b80a0`
case `'C'`) through `FUN_004d2120`, and defaulted by the graphics detail setter `FUN_00440750` to
1.0/4.0/9.0 for levels 0/1/2. It is not clutter-specific: consumers `FUN_004d5de0`/`FUN_004d6010`
apply `fadeScale * distanceSq` against every type-5 scene node's near²/far² thresholds.

**Approach.** A rendering-side change, not a placement one, and **the implementation to copy is
already in the repo**: `FogVolumeClutter.cs:263-274` declares `uniform vec2 far_fade`, computes
`v_alpha = 1.0 - smoothstep(far_fade.x, far_fade.y, d)` and culls with `step(d, far_fade.y)`, fed at
`:533` from its own block's `FarFade`. Bring the same pair of lines to the templates clutter shader,
fed by the per-instance near/far `ClutterTemplates` already stores, and add the detail-level global
that scales it (nothing in the remake reads the graphics detail setting for this today).

**Model recommendation.** high. Two halves that have to land together to be correct at more than one
detail level, a shared global whose scope reaches beyond clutter, and an interaction with
`MapEdgeExtender` that is easy to miss.

**Verify.** The frame-time case first, because that is why this item is in this plan: a fixed C5 pose
looking down a city street, draw and primitive counts before and after, under `--no-vsync` per PERF-11.
Then the look: the full 8-chapter `--freecam --chapter=<X>` regression with the node-count baseline
taken first, since this is meant to change what draws. `FogVolumeClutter.cs:418-419` already bounds its
ring at the largest authored `far_fade`, and the equivalent bound has to be checked against
`MapEdgeExtender` so fringe clutter does not pop at the same distance the authored fade removes it.

**⚠ Traps.** ⚠ Implementing this without the detail-scale half matches the game at exactly one detail
level (⚠ row 9). ⚠ The two fade bounds are one draw, not independent: `translate_uv_range`,
`far_fade_range` and `rotation_range` are grouped by BOUND (`docs/formats/templates.md`). ⚠ PERF-5:
ignore differences below measured noise and an absolute floor; PERF-9: two unchanged pairs for noise
before the A/B. ⚠ Do not re-derive the fade from `FogVolumes.cs`'s `FarFadeNear`/`FarFade` fields;
those are the fog-volume blocks' own pair on a different reader.

## B5 ☐ An unauthored puffer `TIME_INTERVAL` is `1.0` s, not our `0.1`

**Goal.** A puffer state that does not author `TIME_INTERVAL` emits once a second, as the original's
constructor sets it, instead of ten times a second.

**Evidence (confidence: traced).** `BL-336`, decoded while closing `PLAN-puffer-engine-deltas` C9.
The puffer object's constructor `FUN_00550100` writes `0x3f800000` = **1.0** to both `+0x40` (the
interval) and `+0x44` (its reciprocal), and the applier overwrites them only when the parser set the
flag, the same absent-versus-zero shape `WIND_FACTOR` has. Ours defaults to `0.1` in three places:
the field initialiser at `CSVM/src/Effects/Puffer.cs:38`, and both parsers, `FromAnimEvent` at `:174`
and the reader path at `:252`. That is a factor of ten fast on every state that does not author its
own value.

**Approach.** Separate the two uses of `0.1` before touching either. The `:252` reader default and
the `:38` field initialiser are the ctor default and become `1.0`; the `:174` value is
`byDistance ? 0.1f : intervalValue`, and that branch is **our own invention** for a fallback the
engine does not have (a distance emitter on a motionless host), signed off separately. It stays, and
should be named as a distinct constant so the next reader cannot conflate them again.

**Model recommendation.** medium. The decode is settled and the change is small, but the two-uses
split is the whole item and a careless swap makes every static building's sputter ten times slower.

**Verify.** The golden set, which is where a change in emission cadence shows: take the baseline
first and expect movement on the effects shots rather than asserting none. Then a time series rather
than a single frame, per SHOT-19, on a `large_30sec_fire` and on a static building's sputter, to
confirm the sputter's cadence did not move while the unauthored states' did. Also worth a frame-time
read on the same C5 pose B4 uses, since this removes nine tenths of the emissions from every
unauthored state.

**⚠ Traps.** ⚠ ⚠ row 7 is the whole item; do not swap the constant globally. ⚠ ⚠ row 6: the same ctor
settles `NUMBER`'s default at `+0x04` = 1, which is B6's question, and it is already answered. Do not
re-derive it. ⚠ The cost of being wrong here is small in one direction and not the other: every fully
defined reader puffer that reaches the sustained path authors its own `TIME_INTERVAL`, so the default
is only reached by states that do not, but those states include the DISTANCE fallback if the split is
botched.

## B6 ☐ Judge puffer density at the controls now the fire's shape is right

**Goal.** The destruction fires, the damage-stage sputters and the wreck smoke read at the right
density, judged as a moving effect rather than a still.

**Evidence (confidence: direction-sound; the default is settled, the look is not).** `BL-218`. The
default half is closed and our 1 is correct (⚠ row 6): `NUMBER` is absent from 680 of C1's 721
`PufferState` events, `PufferState.FromAnimEvent` falls back to 1, and the ctor writes 1. What
remains is a look question about our sprites, judged now that the fire's shape is right (`PT-22`). It
is a whole-effect multiplier, so a wrong value is visible on all three families at once.

**Approach.** Fly the destruction cases after B5 lands and report the density. **This item ends at a
stop and produces a verdict, not a change**, unless the verdict names a specific sprite problem, in
which case that becomes its own entry with a new ID from `New-ItemId.ps1`.

**Model recommendation.** medium. The agent prepares the build and the watch-list; the instrument is
the user.

**Verify.** The user's own report, from C11's sortie. Their eyes outrank the instruments here by
standing rule.

**⚠ Traps.** ⚠ **Do not raise the `NUMBER` fallback** (⚠ row 6). ⚠ This is not the `puffer.*SizeScale`
knobs. Trading count for size is exactly the substitution that makes a too-sparse plume read as "too
small"; that substitution shipped once as a global 4× `SizeScaleDefault` and `BL-282` reverted it to
the authored 1×, so a density verdict now measures `NUMBER` alone. ⚠ SHOT-19: sprite count only reads
over a time series, never from a single `--screenshot`. ⚠ Do not infer the default from the effects
readers: the `NUMBER`-carrying states are a biased sample, because `PufferState.FindInReader` treats
the presence of `NUMBER` as what makes a state fully defined.

---

# Wave C — the same runtime's correctness, and the two cheap features

## C7 ☐ A stopped sequence stays callable; instrument before implementing

**Goal.** Either the runtime is shown to reach a call on an already-stopped sequence, and the
per-instance disable lands, or it is shown not to, and the divergence stays documented as a disproof.

**Evidence (confidence: lead-only for reachability; traced for the rule).** `BL-334`, filed by
`98948b25` rather than implemented. `STOP_SEQUENCE` (`004eb610`) writes the sequence *done*, and
`CALL_SEQUENCE` (`004eb570`) starts a sequence only from *parked*, so once stopped a sequence cannot
be called again for the life of the instance. CSVM halts the runner but does not persist that
disable, so a later call restarts it. **123 definitions name one sequence in both a call and a stop**,
mostly `flame_light_seq`, plus `chuteman_drop`/`chuteman_sway`, `sail_splash*`/`yacht_splash*`, and
`warhawk`'s `smokepuff1..3`. Whether any of the 123 reaches its stop *before* its call is control
flow, and a static census cannot answer it.

**Approach.** Instrument first. Log a call arriving at a sequence this instance has already stopped,
then run the 8-chapter `--freecam` sweep plus the effect closure. Zero hits is a disproof and the
item closes with the divergence documented. Any hit names the def to reproduce, and only then does the
fix land as a per-instance stopped-set consulted by `AnimInstance.CallSequence`.

**Model recommendation.** high. The prohibition is the item: the easy path is to implement the decode
and it is the wrong one, and the failure mode of getting it wrong is silent.

**Verify.** The instrumented sweep's hit count, reported either way. If a fix lands, the
`stop-sequence` suite plus the golden set, with the baseline taken first.

**⚠ Traps.** ⚠ **Do not implement the disable on the strength of the decode alone.** It would change
behaviour in up to 123 definitions to match a rule none is yet known to observe, and a sequence
wrongly left disabled fails *silently*, which is the hardest class of bug to attribute later. ⚠ The
`PLAYER_RANGE` `* 4.0` divergence the same decode opened is closed as a disproof (`BL-333`, the `* 4.0`
is on `PLAYER_LINED_UP`); do not reopen it. ⚠ `98948b25` already removed the stopper idiom itself, so
"a stop starts a parked sequence" is fixed and is not this.

## C8 ☐ Tighter aircraft collision shapes, convex hulls per clipped region

**Goal.** An aircraft's collision shape follows its silhouette closely enough that a close stunt pass
does not read as a terrain crash, and that being shot is fair.

**Evidence (confidence: lead-only for the magnitude; traced for the shape source).** `BL-300`. The
symptom is user-reported: close-stunt false crashes from box overhang. `PlaneCollider`
(`CSVM/src/Flight/PlaneCollider.cs:20`) builds boxes per clipped region, and its own header comment at
`:16` records that the boxes **deliberately overlap**, with the earliest in `Parts` order (`:39`)
winning for a caller. Since `PLAN-vs-mode` A1 single-sourced the shape set, the same `Parts` feed both
the terrain sweep (where the overhang causes the false crash) and the aircraft body (being-shot
fairness, and blast nearest-point falloff), so the change pays off twice. The Bloodhawk's uncovered
canard tips are the known gap in the other direction.

**Approach.** Convex hulls per clipped region instead of boxes, keeping the `Relabel`/part-name
contract intact: `Relabel` at `:94` renames tail pieces to wing, `PlaneDamage`'s "tail" arm depends on
the result (see its `docs/architecture.md` ⚠), and `MapStruckPart` consumes the names unchanged.

**Model recommendation.** high. It changes both the terrain sweep and the damage mapping at once, and
the overlap rule at `:16` means the ordering semantics have to survive the shape change.

**Verify.** <TODO: name the suite arm. The sweep did not settle whether an existing collision or
damage suite can assert per-part hull coverage headless, or whether this needs a new arm measuring
overhang against the mesh.> The close-stunt half is judged in C11.

**⚠ Traps.** ⚠ The overlap is deliberate and order-dependent (`:16`); a hull set that removes the
overlap changes which part a hit maps to even where the geometry is unchanged. ⚠ Tighter shapes cut
both ways: the false crash is overhang, but the Bloodhawk's canard tips are currently uncovered, so
"smaller everywhere" is not the fix. ⚠ Convex hulls are more expensive per contact than boxes, so this
item can cost frame time rather than save it; measure it rather than assuming, per PERF-9.

## C9 ☐ Fuel burn and the empty-tank lever freeze

**Goal.** The player's tank burns with throttle, and a dry tank freezes the throttle lever where it
stands rather than closing it.

**Evidence (confidence: traced).** `BL-450`. `FUN_0048e580` burns
`[obj+0x134] −= dt · throttle · 5`, player-only at `0x48e603`, and a zero tank jumps past the throttle
slew (`0x48e5f7` to `0x48e6c9`), freezing the lever rather than closing it. Nitro burns no fuel: the
site at `0x48e603` reads the lever, not the boost flag. No fuel model exists in CSVM today; a grep for
`Fuel` over `CSVM/src` returns only the `refuel*` tank props and the `player_fuelleak` damage anim,
nothing on the aircraft. The shipped missions never run a tank dry, so this matters only for a
long-flight mode.

**Approach.** Add the burn term and the empty-tank branch to the player's throttle path, both
decoded. Player-only, matching the decode; do not extend it to AI.

**Model recommendation.** medium. A small addition against a complete decode, in a subsystem where the
constraint is not to touch anything else.

**Verify.** A unit arm on the burn rate and on the lever-freeze branch, both against the decoded
literals. `FlightEnvelopeTests` and `FlightConstantInventoryTests` must be unchanged: this adds a term
that is inert at a full tank, so any movement in the five asserted scenarios means the term is
reaching the plant when it should not.

**⚠ Traps.** ⚠ The flight constants are coupled and `--run-tests` guards them; a fuel term that
touches thrust rather than the lever would move speed and, through it, the yaw `eff`. ⚠ Nitro burns no
fuel, and the site reads the lever rather than the boost flag; do not "fix" that. ⚠ Nothing shipped
runs a tank dry, so this cannot be verified by flying a mission, and the absence of a visible change
in play is the expected result.

## C10 ☐ The mission spawner does not read roster blocks

**Goal.** An AI spawned by the mission spawner carries the roster block's fields, so an authored nitro
injector has a live producer.

**Evidence (confidence: traced).** `BL-453`. `AiSpawn.Nitro` (`CSVM/src/Session/FlightRoster.cs:25`)
reaches the aircraft at `AiFlightAssembler.cs:120` (`controller.Nitro.Installed = spawn.Nitro`), and
the decode has the AI reading roster slot 34 at `0x475c9a`, which three shipped rosters author. The
campaign path fills it: `CampaignRoster.cs:261` passes `Nitro: plan.Nitro`. The mission spawner's own
`AiSpawn` construction sites (`GameSession.cs:2156`, `:2242`, `:2250`, `:2355`) do not, so the field
has no producer outside the campaign and the ledger row is unsupported.

**Approach.** Read the roster block at spawn on the mission path, following `CampaignRoster.cs:261`,
which is the same field over the same record. **Then check which other roster slots the spawner drops
on the same path**, because `Nitro` is the one that surfaced and is unlikely to be the only one; the
census is part of the item, not a follow-up.

**Model recommendation.** medium. Mechanical threading along a path that already exists for the
campaign; the judgement is the census of what else is dropped.

**Verify.** <TODO: name the arm. The sweep did not settle whether an existing AI suite can assert a
mission-spawned AI's `Nitro.Installed` off a roster that authors slot 34; `CampaignRosterSuites`
covers the campaign producer, not this one.> The census result belongs in the landing commit's
message whether or not it finds anything.

**⚠ Traps.** ⚠ Do not fold `BL-469` (an escort cannot hold station on a leader using nitro) into this.
That is a different item, it is not in this plan, and `PLAN-M5-polish-2`'s A5 explicitly forbids
folding it into the wingman work; giving the mission path a live nitro producer makes the two easier
to conflate, not harder. ⚠ Three shipped rosters author the slot, so this changes AI behaviour in
those missions; take the behaviour baseline before landing it.

## C11 ☐ The flown session: the hitch sortie and the two density judgements

**Goal.** The plan's visible items are judged together at the controls, and the two items that end in
a human verdict get one.

**Evidence (confidence: traced for the code, open for the sighting).** A1's own controls capture is
what proved the hitch recurs across a session (31 trips in about 7 seconds through a `DamageLab`
sweep), so a controls session is the instrument this plan's core was diagnosed with, not an optional
extra. A3 and B6 both end in a judgement no suite can make.

**Approach.** ⚠ **This is a stop, not a task an agent completes.** Hand the build to the user with the
watch-list: a `DamageLab` panel sweep and a crash with `HitchMonitor` reading, for A1; repeated sonic
bursts for A2; a rocket burst into a cluster of destructibles and a 4-player splitscreen session for
A3's recycle judgement; the destruction fires, damage-stage sputters and wreck smoke for B6's density
verdict; and a close stunt pass over terrain for C8. Record what the sortie reports. Findings that are
not these items become new `backlog.md` entries with their own IDs from `New-ItemId.ps1`.

**Model recommendation.** medium. The agent's work is preparing the build, the watch-list and the
write-up; the instrument is the user.

**Verify.** The user's own report. Their eyes outrank the instruments here by standing rule.

**⚠ Traps.** ⚠ PERF-13: the sortie runs with vsync on, and a hitch count from it does not compare
against the `--no-vsync` proxy runs A1 and A2 verified with. It answers "does it still hitch in play",
not "by how much". ⚠ The unexplained 79.91 ms trip in A1's original capture may recur; it is not this
plan's and should be filed rather than chased. ⚠ Do not let a green suite stand in for A3's or B6's
verdict; both are open precisely because the suites already pass.
