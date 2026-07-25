# Testing & Verification Infrastructure

**ACTIVE PLAN** (written 2026-07-24; **now the sole active plan — PLAN-M3-weapons completed and archived 2026-07-25**; see "Overlap with M3 Wave F" below for the two M3 debug-tool items this plan supersedes). It sits in `docs/`, which by this repo's convention makes it a live plan; CLAUDE.md's "Current status" names the active plan. Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan replaces the project's screenshot-first verification culture with deterministic, machine-checkable instruments, then builds the interactive inspection tools the user has asked for. It was scoped in a grilling session on 2026-07-24 (decisions table below). Two audiences, in priority order: **(1) agent-facing infrastructure** — a deterministic test mode, an in-engine test harness, unit tests, a perf framework, a logging layer — so every future change verifies cheaper and less flakily than today's noise-floor rituals (`docs/verification.md` exists because the current instruments lie); **(2) user-facing inspect tools** — a shared click-selection with an ancestor ladder, a node lab, mesh/damage labs in freecam, collider wireframes — fixing real debugging friction (the zeppelin-selects-a-motor complaint).

**Cross-plan ID note:** this plan's IDs collide with PLAN-M3-weapons' (both have a D32). References to the other plan are always written "M3 D32"; bare IDs mean this plan.

## Milestone goal

- One command (`RunTests.ps1`) builds, runs the unit tests, the in-engine suites, the golden-image tripwire, and reports a single PASS/FAIL with a nonzero exit code on failure.
- Under `--det`, a run is **byte-identical frame-for-frame** — every RNG seeded, spawn pinned, shader time driven by the sim clock — so a screenshot at frame N is an md5 compare, not a noise-floor measurement.
- The game clock can be halted and single-stepped interactively in **every** mode.
- New features verify primarily through headless probes (log assertions, dumps, suites); screenshots are reserved for genuinely visual questions.
- The user can click any world object, ladder up to the node they mean, and inspect its tree, dependencies, shading, HP, and colliders in freecam.

**Boundary: single-dev, single-machine tooling only — no CI, no cloud runners, no cross-machine baselines. And nothing here may change shipped gameplay behaviour: every test affordance must be provably inert when its flag is absent.** The XWVM asset rule extends to fixtures: a committed test fixture must be hand-authored from the format spec, never a copy (however small) of extracted game data; goldens commit **hashes**, never pixels.

## Decisions (2026-07-24)

| # | Question | Decision |
|---|---|---|
| 1 | Plan spine — infra or user tools first? | **Infra first, tools second** — determinism/harness/perf compound; the tools then build on the shared selection and clock. |
| 2 | Sequencing vs M3 | **Finish M3 Wave D first** (D32/D44/A10 verify the old way); this plan runs clean between milestones. |
| 3 | Unit-test shape | **Both, in-engine as spine** — a `--run-tests` harness formalizes the existing `--dump-*`/`--damage-test` assertions; a small xUnit project covers genuinely pure logic. |
| 4 | Test data | **Local data + synthetic edge cases** — real-`extracted/` golden invariants are the spine (the retail install is a fixed input, so golden counts are stable); committed fixtures only for hand-authored byte-level cases; tests skip-with-notice when data is absent. |
| 5 | Determinism depth | **Full-frame** — beyond seeding/pinning, shader `TIME` is replaced by a sim-clock-driven global, so water/UV-scroll/precipitation are frame-deterministic and shots are byte-identical. |
| 6 | Test stages | **Both** — `--stage=empty` (no gamez; flat collision plane, plane at origin) and `--node=<cs_name>` (single-subtree build in viewer/anim-lab). |
| 7 | Clock control reach | **Halt + step in every mode** — P halts, `.` steps, in fly/freecam/viewer/anim-lab alike; one shared GameClock. |
| 8 | Perf regression detection | **A/B harness + git-ignored local history** — no committed thresholds (machine drift, verification rules 8/41). |
| 9 | Log migration | **Incremental** — harness + new code use `Log` from day one; existing `GD.Print` families convert as items touch them. |
| 10 | Screenshot policy | **Headless-probe-primary policy + a golden-image tripwire** (~10 `--det` shots, committed md5 hashes of raw pixel buffers). |
| 11 | Texture drop-in scope | **Named override + census mode** — `--tex-override=<name>` interactive, `--tex-census` as a headless per-surface visibility instrument. |
| 12 | Selection model | **Click leaf + ancestor ladder** — breadcrumb of the struck mesh's `cs_name` ancestry, PgUp/PgDn walks it; no auto-resolve heuristics. |
| 13 | Tool shape | **Integrated inspect layer** — one shared selection; N node lab, M mesh lab, H damage sliders, C collider wireframes all act on it. |
| 14 | Scripted-run default | **`--screenshot`/dump/test runs imply `--det`**; `--no-det` opts out. Noise becomes opt-in. |
| 15 | Subject placement (user-directed, 2026-07-24) | **`--pos`/`--direction` become the one placement pair in every mode** — camera in freecam/viewer/anim-lab, plane in fly/stunt — replacing the `--campos`/`--lookat` + `--spawn-at`/`--spawn-dir` split, so a scripted water-dive places the plane over the water instead of hoping the mission spawn cooperates. |
| 16 | Flight camera views (user-directed, 2026-07-24) | **The original's held-numpad camera perspectives around the flying plane, plus a scripted `--view=` twin** — a capture instrument now (flank/belly shots in flight) and a fidelity feature the game needs later anyway. |

## Overlap with PLAN-M3-weapons Wave F (checked 2026-07-24)

M3's Wave F ("debug & verification", F39–F43) predates this plan and overlaps it. Disposition, confirmed by the user and applied to M3's plan on 2026-07-24:

| M3 item | Disposition |
|---|---|
| F39 weapon lab (`--viewer`) | **Stays in M3** — no counterpart here; its "all 48 mount and fire" verify additionally becomes a B12 suite so the check is automated, not lab-only. |
| F40 freecam pick + HP control | **⊘ superseded by D31 + D34** (ancestor-ladder selection + HP slider/kill/reset). |
| F41 destructible list + camera jump + coverage columns | **⊘ superseded by D32**, which explicitly carries F41's coverage instrument (root-resolves / event-kinds-implemented) and census-totals verify. |
| F42 `--destroy=` trigger | **Stays in M3** — exit criterion 2 depends on it; later composes with `--det`/`--pos`/`--view`. |
| F43 `--infinite-ammo` / `--loadout=` | **☑ found already delivered** (both flags live in `PlaneViewer.cs:466–467` and documented in `docs/cli.md`) — ticked in M3. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler; never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the data/code before building on it; **a correct disproof that lands no code is a success here**, not a failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-TUNE / lead-only).
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Determinism core

1. ☑ A1 — `GameClock`: one shared sim clock; halt (P) + frame-step (`.`) in every mode; fixed-dt under `--det`
2. ☑ A2 — Clock-driven shader time: `csky_time` global replaces `TIME` in every shader
3. ☑ A3 — One master seed: per-subsystem RNGs derived from `--seed`; `CANNON_SPREAD`, crash-sound pick, spawn, liveries all pinned
4. ☑ A4 — The `--det` bundle; `--screenshot`/dump/test runs imply it; `--no-det` opt-out **(done 2026-07-25 — one resolution block in `PlaneViewer._Ready`, a `det …` announcement line, verification rule 83; `docs/HISTORY.md`)**
5. ☑ A5 — `--pos`/`--direction`: one placement pair in every mode (camera in freecam/viewer, plane in fly) **(done 2026-07-25 — one `ResolvePlacement` block in `PlaneViewer._Ready`; `--lookat` stays a point, the `--viewer` orbit pivot is synthesized from `--direction`; `docs/HISTORY.md`)**

### Wave B — Harness + logging

11. ☑ B11 — `Log`: categories/levels, `--log=` console filter, always-on full-detail file sink **(done 2026-07-25 — `src/Utils/Log.cs`, 9 categories, 5 files converted; `docs/HISTORY.md`)**
12. ☑ B12 — `--run-tests`: in-engine suite registry, pass/fail report, nonzero exit code; existing dump/damage-test assertions become suites **(done 2026-07-25 — `src/Testing/`, 7 suites green in 16 s; `docs/HISTORY.md`)**
13. ☑ B13 — `CSVM.Tests` xUnit project: pure-logic units, local-data golden invariants, hand-authored fixtures **(done 2026-07-25 — 135 tests, no `CSVM/src/` change needed; `docs/HISTORY.md`)**
14. ☑ B14 — `RunTests.ps1`: the single entry point (build → units → suites → goldens → summary) **(done 2026-07-25 — one script, one exit code; the golden and perf stages report TODO until C23/C22; `docs/HISTORY.md`)**

### Wave C — Perf + visual instruments

21. ☑ C21 — Startup-phase stopwatches (always-on structured timing log) **(done 2026-07-25 — `src/Utils/StartupProfile.cs`, one `[perf] startup` line per session build; `docs/HISTORY.md`)**
22. ☑ C22 — Perf suite: fixed `--det` scenarios, A/B mode, git-ignored local history **(done 2026-07-25 — `RunTests.ps1 -Perf` over `analysis/perf/scenarios.json`, 5 scenarios × 300 sim frames; the same-build band was measured on two pairs, not one; `docs/HISTORY.md`)**
23. ☑ C23 — Golden-image tripwire: ~10 `--det` shots, committed md5 hashes of raw pixels **(done 2026-07-25 — 11 shots in `analysis/goldens/manifest.json`, run as `RunTests.ps1`'s own scripted stage rather than a B12 suite; `docs/HISTORY.md`)**
24. ☑ C24 — Texture drop-in: `--tex-override=<name>` + `--tex-census` (+ census assertions for suites) **(done 2026-07-25 — hooked into `TextureArchive.Find`; classification is chromaticity with a measured tolerance; counts are lower bounds; `docs/HISTORY.md`)**
25. ☑ C25 — Test stages: `--stage=empty` and `--node=<cs_name>` **(done 2026-07-25 — `src/Mech3/EmptyStage.cs` + `WorldBuilder.BuildNode`; the anim-bind audit's findings are in the item's landing note, and C22/Wave D depend on them; `docs/HISTORY.md`)**
26. ☑ C26 — Flight camera views: held-numpad perspectives around the plane + scripted `--view=` **(done 2026-07-25 — one view table in `FlightController`, `--view=<1-9>`; the geometry sweep and the C24-composed belly capture are verified, the magnitudes are TUNE and the held-key half is by construction; `docs/HISTORY.md`)**

### Wave D — Inspect layer

31. ☑ D31 — Shared selection: click leaf + ancestor-ladder breadcrumb, PgUp/PgDn, highlight box **(done 2026-07-25 — `src/UI/SelectionService.cs`; the picking audit's answer and what D32–D35 may rely on are in the item's landing note; `docs/HISTORY.md`)**
32. ☑ D32 — Node Lab (N): tree panel synced to selection — search, frame, hide/show, dependencies **(done 2026-07-25 — `src/UI/NodeLab.cs` + `--debug-nodelab`; absorbs M3's F41; `docs/HISTORY.md`)**
33. ☑ D33 — Mesh Lab (M) operates on the selected world subtree in freecam **(done 2026-07-25 — `MeshLab` parameterized on a `SelectionService`; the override materials are now the surface's own shader with two edits, proven 0-px against the shipped render by the new `--debug-mesh=force`; `docs/HISTORY.md`)**
34. ☑ D34 — Damage sliders (H) on the selected destructible — supersedes M3 F40 **(done 2026-07-25 — `src/UI/WorldDamageLab.cs` + `--debug-damage`; the two-pool honesty rule and what the effects runtime does NOT cover are in the item's landing note; `docs/HISTORY.md`)**
35. ☑ D35 — Collider wireframes (C); collision force-buildable in freecam **(done 2026-07-25 — `src/UI/ColliderOverlay.cs` + `--collision[=show]`; on/off counts and the names that flipped are reported separately; `docs/HISTORY.md`)**

## Dependency and parallelism notes

A1 blocks A2 (the uniform is driven by the clock) and A4; A3 blocks A4. A5 is independent of A1–A4 and can land first — A3's and C22/C24's scripted-flight verifications get simpler once it exists. B11 before B12 (the harness speaks `Log`); B12 before B13's in-engine-adjacent invariants and before C23/C24's suite assertions; B14 closes Wave B. C21 is independent (wants B11's `perf` category). C25's empty stage feeds C22's scenario list, so C25 before C22 is convenient but not required. C26 is independent of the rest of Wave C but touches `FlightController.cs` like A1 does — land after A1, never in parallel with it. D31 blocks D32–D35 (C26 is deliberately *not* in Wave D: it hangs off the flight camera, not the shared selection). **D34 additionally depends on M3 D32** (the world-effects runtime, landed 2026-07-24) for its damage-stage effects to render in freecam — the HP/kill/reset mechanics work without it. **File contention: nearly every item touches `PlaneViewer.cs` (arg parsing, session wiring) — do not run two items of this plan in parallel worktrees.** Waves land in order; items within a wave are sequential in the listed order.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a worktree session here; use a local commit or a file copy.

---

# Wave A — Determinism core

## A1 ☑ `GameClock`: shared sim clock, halt + step everywhere, fixed-dt under `--det`

**Landed 2026-07-25** — `src/Utils/GameClock.cs` (Realtime / FixedAccum / FixedStep + `Halted`),
every sim consumer converted, `--det` = the fixed clock only. Evidence in `docs/HISTORY.md`.
Residuals: the halt/step keys are verified by construction (live keypresses are unscriptable here);
one-shot audio plays through a halt; shader `TIME` still runs on wall time until A2, so a `--det`
world shot has a measured 1.60 % pixel floor rather than zero; and the playhead in `AnimLab` stayed
its own counter rather than `GameClock.Frame`, because the clock advances `Frame` by the whole
frame's `Steps` at once and that would stamp every sub-step at the frame's end time.

**Goal.** One clock object owns sim time for a session. Every sim consumer — `FlightModel`, `AnimRuntime.Advance`, `TextureCycler`, `Puffer`, `ProjectilePool`, prop/control-surface/wing-light animators, stunt clocks — receives dt from it, never raw `_Process` delta. Interactively, **P halts and `.` steps one frame in every mode** (fly, freecam, viewer, anim-lab). Under `--det` the clock is fixed-dt: frame N is the same sim state on every run, regardless of render rate.

**Evidence (confidence: traced).** The pattern already exists, mode-locally: `AnimLab.cs:44` (`FixedDt = 1/60`), `AnimLab.cs:165–186` (accumulator; `_fixedFrameStep` ignores wall delta in scripted runs), `AnimRuntime.cs:556` (`ManualAdvance` — the runtime detaches from `_Process` and is driven externally; `PlaneViewer.cs:902–907` explains why double-driving at 2× was the bug this prevents). The lab's P/`.`/speed transport is the interaction model to generalize. `docs/verification.md` rule 51 (a sampler paced by the clock under test cannot see a rate error) is the standing trap for verifying this.

**Approach.** New `src/Utils/GameClock.cs`: `Dt` (fixed 1/60 under det, else wall delta), `Halted`, `StepOnce()`, `Scale` (the lab's 0.1–4× selector keeps working through it). `PlaneViewer` creates one per session and hands it down; consumers swap `(float)delta` for `clock.Dt`. `AnimLab` refactors onto it (its accumulator moves into the clock) — behaviour-preserving for the lab. Fly's existing soft P-pause is replaced by the clock halt; anim-lab keys unchanged. Audio behaviour on halt: engine loops pause via `StreamPaused` where cheap; one-shots may play out — state the residual in the item's landing note rather than chasing it.

**Verify.** (a) Inert-when-absent: an unchanged build vs the refactor, same `--screenshot` pose, pixel-identical (this is a pure plumbing change until `--det` is passed — rule 15: make sure the compare *could* fail by also flipping the clock to 2× once and seeing pixels move). (b) Fixed-dt: two `--det --frames=300` runs of a flight with a scripted `--hold` dive → identical `--debug-anim` pose lines at every logged second. (c) Interactive: halt mid-flight, step 5 frames, confirm the plane advances exactly 5/60 s of sim (log the sim time). 8-chapter freecam regression, zero errors.

**⚠ Traps.** Rule 51: verify rate with wall-paced sampling, not clock-paced. Godot's physics server keeps its own tick — this project's collision is raycasts driven by our dt (`Projectile.cs:380`, `FlightController.cs:704`), so sim state follows the clock, but anything reading `GetTicksMsec` for sim purposes is a divergence bug to hunt (`PlaneViewer.cs`, `AnimRuntime.cs` are the two current users). A1 alone does **not** freeze shader motion — water/scroll/precip keep flowing until A2; don't read that as a failure.

## A2 ☑ Clock-driven shader time: `csky_time` replaces `TIME`

**Landed 2026-07-25** — `src/Utils/ShaderTime.cs` + `CSVM/shaders/csky_time.gdshaderinc`; the
`SceneBuilder` scroll variant and `Precipitation` converted; `--jitter` defaults 0 under `--det`.
Evidence in `docs/HISTORY.md`. **Two sites, not three:** the plan's `PlaneViewer.cs:2317` skydome
`TIME` does not exist — a whole-tree grep finds `TIME` only in those two files, and the skydome's
scrolling sky layer is built through `SceneBuilder`'s scroll path, so it converted with it.
Residuals: the remaining `--det` pixel noise is **unseeded RNG, not the clock** — the waterfall's
mist puffer (0.52 % of the frame) and precipitation's per-instance seeds (4.75 % C2B rain, 6.27 %
C4 snow), both A3's. The interactive halt was proved with a scripted `Halted`-at-session-start
probe; the **P / `.` keypresses stay verified by construction**. Note for A3/C23: only C1, C1B and
C4 carry any UV scroll at all, so a scroll-sensitive golden must be framed on one of them.

**Goal.** Every shader animation runs from a global uniform `csky_time`, set once per frame from the `GameClock`. A halted clock is a true freeze-frame; under `--det`, pixel output is a function of frame count — byte-identical shots.

**Evidence (confidence: traced).** Shader `TIME` sites: `SceneBuilder.cs:1041` (UV scroll; the rollover comment at `SceneBuilder.cs:997` documents a wrap hazard that a session-local uniform removes outright), `Precipitation.cs:91,95,96` (fall/sway), `PlaneViewer.cs:2317` (skydome — reads TIME + camera built-ins). No `.gdshaderinc` uses `TIME` directly (grepped 2026-07-24). `TextureCycler` and `Puffer` are CPU-driven and inherit determinism from A1. The global-uniform precedent is `csky_fog_range` (`--no-fog` writes it globally precisely to avoid the per-instance index-mismatch hazard — see `docs/cli.md` `--no-fog`).

**Approach.** Declare `csky_time` as a Godot global shader uniform (project settings `shader_globals`, like the fog globals); `PlaneViewer` (or the session) writes it each frame from the clock. Replace `TIME` at the three generator sites. Skydome: confirm what its TIME term drives (`PlaneViewer.cs:2317`) and convert it identically.

**Verify.** (a) A/B at a `TIME`-sensitive pose (C3 open water + a scrolling surface + C1C precipitation): unchanged build vs converted build at the *same wall moment* can't be compared (rule 9 — frame budgets); instead verify within the new build: `--det --shots=2` at the same frame count across two runs → **md5-identical raw pixels**, then two different frame counts → different pixels (the instrument can fail). (b) Halt in freecam over C3 water: two consecutive shots identical. (c) Full-stderr shader-error grep (rule 61). 8-chapter regression.

**⚠ Traps.** Rule 36: compare raw pixel buffers, not PNG bytes. Rule 9 becomes moot *only* under `--det` — wall-clock runs still have unequal frame budgets. This item changes pixel output of TIME-driven surfaces at any given wall moment, so any stale screenshot baselines die here — C23's goldens are captured **after** this lands, never before. `--jitter` must default 0 under `--det` (it exists to defeat bit-identical frames — the very property `--det` wants).

## A3 ☑ One master seed: per-subsystem RNGs derived from `--seed`

**Goal.** `--seed=N` (default 1 under `--det`) pins every random draw in the session: gun spread, crash-sound pick, spawn choice, liveries, `RANDOM_WEIGHT` dice. Same seed → same run; different seeds genuinely branch.

**Evidence (confidence: traced).** Unseeded draws today: `Projectile.cs:246–247` (`GD.Randf()` — the `CANNON_SPREAD` non-determinism of verification rule 77), `FlightAudio.cs:198` (`GD.Randi()` crash-sound pick), `PlaneViewer.cs:2332` (`GD.Randi()` spawn pick), `PlaneViewer.cs:1904–1910` (paint RNG, already pinnable via `--paint-seed`), `LiveryLab.cs:67`. Already seedable: `AnimRuntime.cs:143` (the lab's `Seed`). **Two more surfaced by A2's verification — after A2 they are the ONLY thing left keeping a `--det` world shot from byte-identity:** `Precipitation.Init`'s `new System.Random()` particle seeds (measured 4.75 % of pixels on a C2B rain shot, 6.27 % on C4 snow; 0.00 % with the seed pinned) and the puffer particle spread (0.52 % of a C1 waterfall frame, all of it inside the mist at the base).

**Approach.** Prefer **per-subsystem `RandomNumberGenerator` instances** seeded as `master ⊕ hash(subsystemName)` over one shared global sequence — call-order independent across subsystems, so adding a draw in one subsystem can't shift another's sequence. Route the four unseeded sites through named instances (`weapons`, `flightaudio`, `spawn`, `paint`); also `GD.Seed(master)` once for stragglers. `--paint-seed` stays as an override of the derived paint seed. Spawn under `--det` additionally defaults to `--spawn=0` in A4 (a pinned *choice* beats a pinned *dice roll* for scenario stability across data changes).

**Verify.** Two `--det --fire --hold=<dive>` runs over C1B water → identical impact logs (position + count), the check rule 77 says is impossible today (take the failing baseline first on the unseeded build). Crash twice with the same seed → same crash sound named in the log. 8-chapter regression.

**⚠ Traps.** Determinism of a *sequence* still requires deterministic *call order* within a subsystem — under A1's fixed clock that holds; any draw made from a wall-time or focus-dependent path (menu idle, pad rumble) must not share a sim subsystem's RNG. Unseeded (no `--det`, no `--seed`) behaviour must stay time-seeded — the shipped game keeps its variety (the boundary rule).

**Landed 2026-07-25** — `src/Utils/Rng.cs`: one master seed, `Reset(master, pinned)` per session
build, and a subsystem seed of `splitmix64(master ⊕ fnv1a(name))` — a hand-written hash, because
`string.GetHashCode()` is per-process randomized in .NET and would have defeated the item outright.
Evidence in `docs/HISTORY.md`.

**Scope: ten subsystems, not the plan's four.** An audit of every draw in the tree after A1 found
five more, and implementation turned up a sixth. All are on the sim path and all move pixels:
`anim` (the **world** `AnimRuntime` — its `Seed` machinery existed but `PlaneViewer` wired
`RuntimeSeed` only in `--anim-lab`, so `--fly`/`--freecam` ran the world's `RANDOM_WEIGHT` dice,
`SOUND_GROUPS` picks and crash-debris scatter unseeded), `effects` (the world-effects runtime,
seeded only under `--effects-test`), `crash` (the per-player crash rig — a third `AnimRuntime`
family the audit did not list), `puffer`, `clouds`, `precip`. Precipitation and the waterfall mist
were the whole of A2's measured residual. `UI/LiveryLab` deliberately stays on its own non-sim
generator: a button press is not sim state.

**The trap seeding alone does not fix:** `SoundDefs.SoundGroup._last` is recency state living
*outside* the RNG (it halves the last pick's weight), so a re-seeded runtime replays a different
sequence. `ResetRecency()` / `WorldSounds.ResetGroupRecency()` now clear it and `AnimRuntime.Reseed()`
calls it in the same breath — `docs/verification.md` rule 81.

**Deviation:** `--seed=N` was the animation lab's own seed; it is now the session master and the lab
derives from it. `--det`, `--anim-lab` and `--effects-test` pin the master to 1 (which is what the
lab's old default did, and what kept the effects census comparable); everything else draws from the
clock, and the resolved value is logged so an unpinned run can be replayed.

**Residual found here, since closed:** a `--det --fly` *screenshot* was not byte-identical (2.71 %
of pixels) because `FlightController.UpdateChaseCamera` smoothed on the raw wall delta. A1's "UI and
camera code stays off the sim clock" turned out to be the right rule stated one notch too widely —
it only has to hold *through a halt*. The chase camera now takes the clock's dt while running,
which took a bare `--screenshot` C1 flight to 0 of 921,600 px at frames 15/120/300, so **C23's
goldens may frame flight poses after all**.

## A4 ☑ The `--det` bundle; scripted runs imply it

**Goal.** `--det` = fixed-dt clock + master seed 1 + `--spawn=0` (unless explicit) + pinned livery + `--no-pads` + `--jitter=0`. `--screenshot=`, every `--dump-*`, `--damage-test` and `--run-tests` **imply `--det`**; `--no-det` opts out. A `det [...]` log line announces the full resolved bundle, so every capture is self-documenting (the `--no-fog` convention).

**Evidence (confidence: traced).** The trap this kills is composition-by-memory: verification rules 62 (`--no-pads`), 77 (spread), and the "Known non-deterministic surfaces" table are all "you forgot a pinning flag" failure classes. All constituent switches exist after A1–A3; this item is wiring + defaults in `PlaneViewer` arg parsing.

**Verify.** (a) `--screenshot` twice, no other flags → md5-identical raw pixels (the headline capability). (b) `--no-det --screenshot` twice over C3 water → pixels differ (opt-out works, and the identity check is seen able to fail). (c) `--det` absent + no scripted flag → grep the log for `det` line absent, and confirm pad input still reaches the flight (inertness). Update `docs/cli.md`, CLAUDE.md's flag table, and `docs/verification.md` (new rule: scripted runs are deterministic by default; wall-clock behaviour needs `--no-det`; retire/annotate the noise-floor table entries that `--det` obsoletes — they still apply to `--no-det` runs).

**⚠ Traps.** Existing scripted workflows change pixel output once, permanently — land A4 *before* C23 captures goldens. `--frames=N` semantics tighten to "exactly sim frame N"; state it in cli.md. Do not let `--det` leak into interactive defaults: a bare `--fly` must keep random spawn/livery (playtest variety is a feature).

**Landed 2026-07-25** — one resolution block in `PlaneViewer._Ready` (after `Log.Open`, ahead of the
jitter and master-seed resolution it feeds), a `[core] det clock=… seed=… spawn=… livery_seed=…
pads=off jitter=… via=…` announcement, and `sim_frame=`/`sim_time=` on the saved-shot line.
Implying flags: `--screenshot=`, `--dump-markers`, `--dump-weapons`, `--dump-loadout`,
`--dump-config`, `--damage-test` — each verified individually, each cancelled by `--no-det`, which
also beats an explicit `--det`. Evidence in `docs/HISTORY.md`; the standing rule is verification 83.

**The item's own verify (a) does not hold as written, and the reason is A3's residual, not a defect
here.** "`--screenshot` twice, no other flags" is a **flight** run — flight is the default for any
content arg — so it hits `UpdateChaseCamera`'s wall-delta smoothing: measured 29.38 % of pixels at
frame 15, 3.21 % at 120, 32.70 % at 300, while the simulation matches exactly. Byte-identity is a
`--freecam` / `--viewer` / `--anim-lab` property (all three measured md5-identical, 0 px). **C23's
goldens must avoid flight poses until that camera moves onto the clock**; filed in `backlog.md`.

**Deviation:** the implication list includes `--dump-config` (the plan said "every `--dump-*`", and
it is one) but deliberately NOT `--effects-test` / `--weapon-test`, which already pin what they need
and were left alone rather than widened by guess. `--run-tests` joined the list with B12.

## A5 ☑ `--pos`/`--direction`: one placement pair in every mode

**Goal.** `--pos=x,y,z` + `--direction=x,y,z` place the **subject** of whatever mode is running: the camera in `--freecam`/`--viewer`/`--anim-lab`, the plane (spawn position + nose direction, bypassing the mission spawn list) in `--fly`/`--stunt`. One pair to remember, one syntax in every scripted run. The motivating case: a water-dive test today spawns wherever the mission says and can crash into land before reaching water — with A5 the plane starts exactly over the lake, pointed down it.

**Evidence (confidence: traced).** All four mechanics already exist, split by mode: `--campos`/`--lookat` (camera, `docs/cli.md`) and `--spawn-at`/`--spawn-dir` (plane, `docs/cli.md`: "place the plane just short of a target for a deterministic straight-line scripted run") — so this is CLI unification and reach, not new machinery. Verification rule 77's workaround ("pick a chapter whose **spawn sits over** the surface you want — C1B/C2B dive → all water") exists precisely because plane placement wasn't reached for; A5 retires it for placement-controllable tests. F11 currently prints ready-to-paste `--campos=`/`--lookat=` (CLAUDE.md keys) — it must print the new form, per mode.

**Approach.** Parse the new pair in `PlaneViewer` and route by mode onto the existing plumbing (`--campos` path for camera modes, `--spawn-at`/`--spawn-dir` path for flight). **Semantics stay explicit: `--direction` is a direction vector; `--lookat=<point>` survives as a convenience alias converted to a direction at parse time** (aim-at-a-point is genuinely useful when framing a target). `--campos`/`--spawn-at`/`--spawn-dir` become deprecated aliases that log their replacement once. F11's printout switches to `--pos=`/`--direction=`. Update `docs/cli.md`, CLAUDE.md's key line, and the `docs/verification.md` non-determinism table entries that say "pin with `--campos`/`--lookat`".

**Verify.** (a) Alias equivalence: a freecam `--pos`/`--direction` shot md5-equal to the same pose via `--campos`/`--lookat` (then move one coordinate → pixels change; the compare can fail). (b) The motivating case: `--fly --chapter=C1 --pos=<over the lake> --direction=<down the lake> --hold=<dive> --fire` → impact log reports water impacts on the first run, no land crash — the scenario rule 77 says needs chapter-shopping today. (c) Flight spawn parity: `--pos`/`--direction` in flight behaves identically to the same values via `--spawn-at`/`--spawn-dir` (same logged spawn pose). 8-chapter regression untouched with the flags absent.

**⚠ Traps.** `--lookat` is a *point*, `--direction` a *vector* — converting one to the other blindly at the plane-spawn site is the mixup to guard; keep the conversion in one parse-time place. PowerShell splits unquoted comma args into arrays (rule 63 and the `--data-root` note in cli.md) — every example in docs shows the quoted form. Mid-air placement must go through the existing `--spawn-at` code path so `FlightModel`'s initial speed/trim handling applies — do not invent a second spawn initializer.

**Landed 2026-07-25** — one `ResolvePlacement` block in `PlaneViewer._Ready` (after the `--det`
block; it needs `_fly`, which settles far earlier) routing the pair onto the existing per-mode
plumbing, `PrintCameraPose` → `PrintPlacement`, and `ChooseSpawn`/`LogSpawn` converted to `Log` (the
German locale had been printing `dir=(0,97,-0,24,0,00)` in the very line the parity check compares).
Evidence in `docs/HISTORY.md`; the standing rule is verification 84, which retires rule 77's
chapter-shopping workaround.

**The two decisions the item text left open, both raised by the audit:**

- **`--lookat` is NOT converted to a direction at parse time — only flight converts it.** The item
  said "converted at parse time", and that is right for a nose direction and wrong everywhere else:
  `OrbitCamera.Frame`'s `lookAt` is a true **pivot** and sets the orbit **radius** with the eye, so a
  parse-time collapse would leave the `--viewer` wheel and drag spinning about the eye. `--lookat`
  therefore stays a point, `--direction` a vector, and a `--viewer --direction` gets a **synthesized**
  pivot — nearest approach of the aim ray to the plane's AABB centre (min radius 1 m), or the AABB
  centre with the eye swung to the aim when there is no `--pos` — announced on its own log line. Also
  why `--lookat` alone (no `--pos`) still works in `--freecam`: there is no position to convert
  against, and the point is what that mode wanted anyway.
- **In `--freecam`/`--anim-lab`, `--pos` beats `--spawn-at`** (which already reached those modes
  through `ChooseSpawn`, with `--campos` layered on top). The deprecated flags keep their *old
  per-mode meaning* rather than becoming renames: `--campos` still never places the plane, and
  `--spawn-at` still moves the anim lab's parked stage prop as well as the camera — `--pos` places
  only the camera there and deliberately does not copy that.

**Residual.** F11 was proved through a temporary probe call at the screenshot-save site (then
reverted), not a live keypress — the standing limit in `docs/verification.md`. The printed direction
carries a `-0` component when an axis is zero; it round-trips through `ParseVec3` unharmed.

# Wave B — Harness + logging

## B11 ☑ `Log`: categories, levels, `--log=` filter, always-on file sink

**Landed 2026-07-25** — `src/Utils/Log.cs`; evidence in `docs/HISTORY.md`. Four things the item text
got wrong or left open, settled here:

- **The census is bigger than budgeted: 223 sites across 37 files, not 184/29** (M3's landing added
  ~40). **`PlaneViewer.cs` alone holds 98 of them (44 %)** and spans every category, so it must be
  migrated cluster by cluster and last, never while another item is editing it.
- **A ninth category, `ui`, was added** rather than widening `core`. The 8 proposed had no home for
  the ~24 lab/launchscreen sites (`LaunchMenu` 7, `AnimLab` 6, `MeshLab` 5, `LiveryLab` 2,
  `WeaponLab` 2, `NodeLabels` 1, `DamageLab` 1); `core` is the session/CLI/config spine a scripted
  run always wants to see, while lab dumps are read on purpose and must be silenceable alone.
- **Console default is `info`, not "errors and warnings only"** — that is what an unconverted
  `GD.Print` showed, so a converted site is inert on the console; `--log=*:warn` is the quiet shape.
  And **`--debug-anim` implies `--log=anim:debug,sound:debug`**: it already opens those families'
  call-site gates, and would otherwise half-work while their 35 sites are unconverted.
- **`Log` takes a `FormattableString` rendered invariant**, so every interpolated site is fixed
  structurally at migration time. Consequence to know before converting anything:
  `$"a{x}" + $"b{y}"` is a `string` and will not compile (deliberate — it would have formatted in
  the current culture already), and a composite's own `ToString()` escapes the invariant rendering
  outright, so log a record's *fields*, never the record.

**Demonstration set — 5 files, 14 sites, and NO sweep** (decision 9, rule 67): `Utils/Config.cs`
(`core`), `Mech3/Clutter.cs`, `Mech3/TextureArchive.cs`, `Flight/Weather.cs` (`world`),
`Mech3/TextureCycler.cs` (`anim`, debug). Levels are preserved per site except `TextureArchive`'s
two, whose own comment recorded the level as compromised. `PlaneViewer.cs` took three lines.
Residual for later items: the two relative-path `.scratch` writes the scout found
(`--weapon-test`, `--effects-test`) are untouched and still B12's to fix.

**Goal.** `src/Utils/Log.cs`: `Log.Info("anim", "motion target=... pos=...")` etc., categories ~ {`anim`,`world`,`flight`,`weapons`,`sound`,`perf`,`test`,`core`}, levels error/warn/info/debug. Console shows errors/warnings plus whatever `--log=cat[:level],...` enables; a **full-detail file sink always writes everything** to `.scratch/logs/<mode>-<timestamp>.log`, so a post-hoc grep never misses a category that wasn't enabled. Stable `[cat] message key=value` grammar so suites parse lines reliably.

**Evidence (confidence: traced).** 184 `GD.Print` sites across 29 files (grepped 2026-07-24); the failure modes of ad-hoc printing are verification rules 58 (a line structurally unable to show the thing), 59 (print caps hiding the entity under test), 61 (grep full stderr), 65 (an exception in `_Process` silently kills per-frame logging).

**Approach.** Thin static class over `GD.Print`/`GD.PrintErr` + a `StreamWriter` (line-flushed, so a crash still leaves the log — rule 65). Migration is **incremental** (decision 9): B12's harness and all new code use it from day one; `--debug-anim`/`--perf` families convert when later items touch them; no bulk sweep (rule 67 — bulk text rewrites have corrupted files here before).

**Verify.** Unit-testable pure (filter parsing, grammar formatting) in B13. In-engine: a run with `--log=anim` shows anim lines on console and *all* categories in the file; a thrown-then-caught test error appears in both. Confirm the sink's cost is invisible in `--perf` (debug level off the hot path).

**⚠ Traps.** `.scratch/` is swept by `CleanScratch.ps1` — logs are disposable by design; anything worth keeping gets moved to `analysis/` per the standing rule. Never buffer in memory (crash = lost evidence). Windows file locking: one sink per process, filename carries PID if two sessions collide.

## B12 ☑ `--run-tests`: the in-engine suite harness

**Goal.** `--run-tests[=filter]` boots the engine, runs registered assertion suites, prints a per-suite PASS/FAIL table plus a `.scratch/test-report.json`, and **exits nonzero on any failure**. The existing proto-tests become suites: `weapons-defs` (48 defs, no unhandled keys — from `--dump-weapons`), `loadout-bind` (all 11 bind, every marker resolves — from `--dump-loadout`), `damage-stages` / `damage-hd` (the C22–C28 checks from `--damage-test`), `markers-rig`, `weapons-fire` (all 48 weapons mount and fire without error — the automated half of M3 F39's verify), `destructible-census` (per-chapter destructible totals match M3 A4's census — F41's verify, automated), plus new ones as later items add them (C24 census asserts, C23 goldens).

**Evidence (confidence: traced).** The dump tools already assert (loud `!!` lines, "no unhandled keys" pass criteria — see `docs/cli.md`) but exit 0 regardless; nothing aggregates them. `SessionPaths`/`WorldSession` make world-building callable outside the interactive modes (the `--damage-test` precedent).

**Approach.** A small suite registry (name → `Action<TestContext>`); `TestContext` carries asserts, the `Log` `test` category, and data paths. The dump tools keep their flags (they're inspection reports) but their assertion cores move into shared code the suites call — one source of truth. Suites that need pixels run windowed no-focus (rule 71: `--headless` breaks readback); pure-data suites accept `--headless`. Data-dependent suites skip-with-notice when `extracted/` is absent (decision 4).

**Verify.** Rule 14 ritual: plant a deliberately failing assertion, see the nonzero exit and the report line, remove it. Then: full suite green on current data; `--run-tests=weapons` filters correctly; exit code checked from PowerShell (`$LASTEXITCODE`).

**⚠ Traps.** Rule 66 (stray Godot processes poison runs — B14's script handles the kill, scoped to this worktree's binaries); rule 74 (absolute output paths only); rule 75 (suites observing scheduled effects must tick the clock via A1's machinery, in-tree, `ManualAdvance`). A suite must never write outside `.scratch/`.

**Landed 2026-07-25** — `src/Testing/Probes.cs` (assertion cores), `TestHarness.cs` (registry,
`TestContext`, table, JSON report, exit code, error screen) and `Suites.cs` (the seven suites).
`PlaneViewer` keeps the dump flags but its four handlers are now thin wrappers over the probes —
net −430 lines there, and the `--dump-weapons`/`--dump-markers`/`--dump-loadout`/`world_colliders`
outputs are **byte-identical** to the pre-refactor ones (md5). The one deliberate change:
`--damage-test` now renders invariant, so the German machine's `HEALTH 0,01` reads `HEALTH 0.01`.

**Suites and measured results (C1, retail data, 16 s wall, exit 0):** `weapons-defs` 48 defs /
0 unhandled · `markers-rig` 11/11 airframes · `loadout-bind` 11 bound / 0 failed · `weapons-fire`
48/48 fired, 0 errors, 0 skipped · `damage-stages` 16 defs, all resolve and fire a stage ·
`damage-hd` 16 defs, all destroyed, all reset+rekill idempotent · `destructible-census` all 8
chapters against the committed table.

**The pass criterion, decided before writing it.** A suite's verdict is its own structured checks.
Native `ERROR:` lines are C++ `ERR_FAIL_COND` prints that **cannot** be intercepted from C#, so
they are screened out of band: the run reads its own engine log back (`--log-file`, else the
project's default rotating log when this run wrote it) and classifies each error against a
**capped** allowlist — two entries today, `det == 0` (max 8) and `!is_inside_tree()` (max 4), each
naming its open backlog item. Unknown error → fail; over cap → fail; no log → SKIP, never PASS.
**Every allowance's count is printed even on a pass** (`allowed 1/4x …`), which is the thing that
stops an entry swallowing a new error silently; the classifier is pure and has 7 xUnit tests in
`CSVM.Tests` (149 total, up from 142) including "one over the cap fails".

**Two live rule-74 bugs fixed on the way:** `--weapon-test` and `--effects-test` wrote through
relative `./.scratch/` paths, which resolve against the *process* working directory — the evidence
is a stray `CSVM/.scratch/` holding 24 MB of misplaced probe artifacts. Both now go through
`WriteScratch` (absolute, under the repo root).

**Deviation from the item text:** `destructible-census` covers all 8 chapters in one process
(6.7 s) rather than only the run's chapter, building and freeing each non-cached world. It reads
`DestructibleRegistry.Count`/`DistinctAnchors`, never the sweep rows, which `Probes.SweepCap` caps
at 16.

**Finding: the `det == 0` errors are not what the backlog said.** Corrected there in full; the two
transferable halves are `docs/verification.md` rules 83–85. Briefly: they are printed *after* the
sweep completes (so they abort nothing), they survive `--mute` (so not `WorldSounds`), the
continuous-sweep mode never produces them (so not the stage path), no single def group reproduces
them, and `--run-tests=damage-hd --chapter=C2` produces a byte-identical report with **0** errors —
the harness frees its world before the frame that emits them. Leading suspect is now `Puffer`'s
per-frame `GlobalPosition` sets on death-created emitters.

## B13 ☑ `CSVM.Tests`: the xUnit project

**Goal.** `dotnet test` runs a plain xUnit project covering the genuinely pure logic: `WavFile` (ADPCM block decode), `Zrdr`/`ZrdrDict` (alternating-list semantics), parser edge cases (`SoundDefs`, `WeaponDefs`, `AnimDefs` key handling), `Log`'s filter/grammar, format math (the Yxz Euler order, UV mirroring helpers if extractable). Two fixture kinds: **hand-authored synthetic** bytes/JSON committed under `CSVM.Tests/fixtures/` (authored from `docs/formats/`, never copied from extracted data — the asset rule), and **local-data golden invariants** (counts and structural facts against the user's `extracted/`, e.g. "48 weapon defs", "11 loadouts", "zrdr reader count per chapter") that skip-with-notice when the data is absent.

**Evidence (confidence: direction-sound).** `Godot.Vector3`/`Basis` are managed structs usable without the engine; `GD.*`/`FileAccess`/resource loads are native and throw outside Godot. **Unverified: which readers are native-free today** — audit first; where a reader touches Godot IO, either it already takes `System.IO` streams or the fix is a trivial seam. A reader that can't be freed cheaply stays covered by B12 instead — do not force a refactor from this item.

**Approach.** New `CSVM.Tests/CSVM.Tests.csproj` (net8.0, xunit, references `CSVM.csproj`) added to `CSVM.sln`; data root resolved via `CSVM_DATA_ROOT` (the existing worktree convention) falling back to the repo-relative `extracted/`; a `[SkippableFact]`-style helper reports skipped-for-data distinctly from passed.

**Verify.** Rule 14: one deliberately failing test seen failing. `dotnet test` green (a) with data present, (b) with `CSVM_DATA_ROOT` pointed at an empty dir — skips reported, zero failures. Confirm `dotnet build CSVM/CSVM.sln` still builds the game project unchanged.

**⚠ Traps.** The Godot SDK csproj may fight the test SDK if tests are added to the *game* project — keep them in their own csproj. **A "small real example" fixture is still a game asset** — synthetic means authored, byte by byte, from the spec; when in doubt it does not get committed. Golden *numbers* (counts) are fine to commit; golden *content* is not.

**Landed 2026-07-25 — 135 tests, `dotnet test CSVM/CSVM.sln`, and NO `CSVM/src/` edit was needed.** The audit the item's Evidence asked for, settled:

- **Godot-free outright** (no `using Godot`): `Zrdr`/`ZrdrDict`, `WavFile`, `SoundDefs`/`SoundGroup`, `WeaponDefs`, `Messages`, `MissionTargets`, `SessionPaths`.
- **Managed-Godot only** (`Vector3`/`Basis`/`Transform3D`/`Mathf`/`Color`, all pure C# structs — they load and run outside the engine): `GameZ` (incl. `Basis.FromEuler(…, Yxz)`), `MarkerRig`, `AnimDefs`, `PlaneStats`, `SpawnPoints`, `PaintScheme`, `AnimProgram`. `AnimDefs`' `AnimRuntime.HighLod` reference is a `const`, so it is inlined and never loads the `Node`-derived type.
- **Partly free, no seam cut**: `StockLoadouts.Load(path)` is free with an explicit existing path (only `DefaultPath`'s `ProjectSettings.GlobalizePath` and the missing-file `GD.PushWarning` are native); `TextureArchive`'s constructor, `FindByDecalIndex`, `IsKnownAbsent`, `MissingTextures` and `Dispose` are free while `Find`/`FindImage`/alpha classification need `Image`.
- **Stays with B12**: `Loadout.Bind` (`Node3D`), `Weather.Load` (a `GD.Print` per zone on *every* load), `SoundArchive` (`AudioStreamWav`), `Config` (`GD.Print` + `res://`), `HudMetrics` (takes a live `Control`), `DestructibleRegistry` (`Node3D`), `CompiledAnim` in its failure paths only. **Never call a `GD.*` from a test host** — outside Godot the unmanaged callback table is uninitialised, so it does not throw cleanly.
- **Free but not yet covered** — cheap headroom for later items: `PlaneStats.Load`, `SpawnPoints.LoadIa`/`LoadPlayerInit`, `PaintScheme.LoadCatalog`, `AnimProgram.Load`.

Also landed: `CSVM_DATA_ROOT` probes *both* shapes (a checkout holding `extracted/`, and the extraction tree itself), and `[ExtractedDataFact]`/`[ExtractedDataTheory]` set xUnit v2's attribute `Skip` — v2 has no `Assert.Skip`, and a theory must skip at the attribute level or its rows fail individually. B14 should call `dotnet test CSVM/CSVM.sln` (which runs only the test project) and treat exit 0 with a nonzero skip count as "data absent", not "passed".

## B14 ☑ `RunTests.ps1`: the single entry point

**Goal.** One script: build (`dotnet build CSVM/CSVM.sln`) → `dotnet test` → `--run-tests` (windowed, `--det` implied) → golden compare (once C23 exists) → one summary block and a single exit code. Switches: `-Filter <suite>`, `-SkipUnits`, `-SkipEngine`, `-Perf` (append C22's history entry).

**Evidence (confidence: traced).** Launch mechanics are established in `RunGame.ps1`/`RunDev.ps1` and verification rules 63 (space in repo path — call operator + quoted args), 66 (kill stray Godots first, filtered to this tree's command line), 74 (absolute paths), 71 (no `--headless` for pixel suites).

**Approach.** PowerShell 5.1-safe (no `&&`), reuses the launch scripts' Godot-resolution logic (including the `CSVM_DATA_ROOT` fallback so it runs from a worktree). Aggregate exit: nonzero if any stage failed; skipped-for-data is not failure but is printed.

**Verify.** Run it green end-to-end; break one unit test and one suite in turn → script exits nonzero with the failing stage named. Run it from a worktree with `CSVM_DATA_ROOT` set → identical behaviour.

**⚠ Traps.** Rule 66's kill must be scoped (command-line filter on this worktree's path) — never a blanket Godot kill; another agent's run is not yours to kill.

**Landed 2026-07-25** — `RunTests.ps1`: build → units → engine → goldens (+ perf under `-Perf`),
one summary block, one exit code. Evidence in `docs/HISTORY.md`.

**The two seams are honest TODO rows, not stubs.** `goldens` is C23's and prints "not implemented
yet — no golden hashes are captured or compared"; `perf` is C22's and prints "not implemented yet —
no scenario set, no A/B, no history store". Both add a `not checked:` line, so the summary of a
fully green run still says out loud that no pixel regression and no timing regression is being
caught. Neither ever contributes a PASS.

**Rule 66's scoping had to be tightened, and the trap is now in the rule.** A command-line filter on
this tree's project dir alone matches a *live playtest*: the first two runs each killed two
`--plane=player_bhawk --chapter=C1` processes another session had launched seconds earlier. The kill
is now filtered to this tree's dir **and** `--run-tests` — a run that always quits by itself, so a
live one is stuck and ours — and every other Godot on this tree is printed and left alone.

**A second trap, now verification rule 88:** with `$ErrorActionPreference = "Stop"`, piping the
script's own output (`.\RunTests.ps1 | Select-String …`) makes PowerShell 5.1 wrap the child's
stderr in `NativeCommandError` records, and Godot's first allowlisted `ERROR:` line then killed the
script at the launch line while the identical unpiped run passed. Every native call now runs with
errors non-terminating and is judged by its exit code.

**Known gap, inherited from B13, not fixed here:** `CSVM_DATA_ROOT` pointed at a directory holding
no extraction makes the *engine* suites skip (it resolves that path strictly) but not the
data-dependent *unit* tests, which fall back to their own checkout — so from the primary tree they
still find `extracted/` and run. Documented in `docs/tooling.md` rather than changed, since it is
`TestData`'s resolution order, not the script's.

# Wave C — Perf + visual instruments

## C21 ☑ Startup-phase stopwatches

**Goal.** Every session logs a structured, always-on timing breakdown on the `perf` category: data load (gamez/textures/zrdr per phase), world build, clutter, anim bind, sound prewarm, first rendered frame — `[perf] startup total=… gamez=… world=… clutter=… bind=… first_frame=…`.

**Evidence (confidence: traced).** Near-zero instrumentation today (`Stopwatch` only in `PlaneViewer.cs` and `AnimRuntime.cs`, grepped 2026-07-24); the phases are cleanly delimited in `WorldSession` (load → WorldBuilder → clutter → bind → prewarm, per its architecture entry). Rule 42 (cold OS file cache: first-run 8.1 s on 630 fresh JSONs) is the known confounder.

**Approach.** Stopwatches at the `WorldSession` phase boundaries + `PlaneViewer` session end-to-end; emit via B11. Cheap enough to run unconditionally.

**Verify.** Phase sum ≈ measured wall startup (within the residual — name what the residual contains). Numbers visibly move when they should: an unzipped vs zipped extraction changes the load phase (an A/B the docs already describe via `PreferUnzipped`).

**⚠ Traps.** Rules 41/42: cold-vs-warm cache differences dwarf real changes — C22's protocol (warm-up run discarded) is where comparisons live; this item only *reports*.

**Landed 2026-07-25** — `src/Utils/StartupProfile.cs` plus `Mark`/`Record` calls at the
`WorldSession` phase boundaries and around `PlaneViewer`'s data loads. Evidence in
`docs/HISTORY.md`; the standing rules are verification 89 (cold vs warm reshapes the profile) and
90 (read the line, not the process wall).

**The line closes arithmetically rather than approximately.** The item said "phase sum ≈ measured
wall startup"; two extra keys make that an identity instead — `boot` (engine start → build start)
and `rest` (the build minus its phases). `total = boot + Σ(phases) + rest + first_frame`, checked
across 24 runs with 0 mismatches beyond 0.2 ms rounding. Measured C1 `--freecam`:
`total=3028.0 boot=1091.0 gamez=548.2 textures=1.6 sounds=0.3 zrdr=15.6 world=461.7 clutter=26.4
anim=270.7 bind=342.6 prewarm=85.0 edge=8.9 weather=28.3 rest=64.5 first_frame=83.3`.

**What `rest` contains, measured rather than argued.** It is per-mode: freecam 26–65 ms, flight
240–243, viewer 255. A temporary probe mark (added, measured, reverted) put **84.9 ms of the
viewer's 255 on the damage lab's ten baked pufftrail emitters**; the rest is the gauge cluster and
the four lab UIs. Flight's 240 is the fly-minus-freecam delta — per-player rig, projectile pool,
world-effects runtime, loadout bind. Outside the process, `total` 3028 sits in a 5355 ms
`--quit-after 120` wall: 118 vsync frames plus ~330–360 ms of spawn/shutdown C# cannot see.

**Three phases beyond the item's list** (`plane`, `weather`, `edge`) because they are large and
mode-specific, and the runs that quit inside the build (`--damage-test`/`--effects-test`/
`--weapon-test`) emit from `NotificationExitTree` with `first_frame=none` rather than not at all.
`StartupProfile.Current` is deliberately null outside a session build, so `--run-tests`' eight
census worlds — same `WorldSession` code — record nothing.

**The A/B the item asked for, both directions.** Zip-only data root vs unzipped, C1 ×3 each:
`anim` **265–271 → 431–438 ms**, `sounds` 0.2 → 6.9, build 1831–1862 → 1936–1970 — while `world`
(451–456 → 409–417) and `zrdr` (15.7–16.5 → 12.3–12.7) reproducibly went the *other* way, many
loose files costing more than one zip handle. Cold vs warm (a freshly-copied C3 root): `total`
**9777 → 2570**, concentrated — `anim` 5974 → 278 (**21×**), `world` 4.5×, `prewarm` 4.3×, `gamez`
unmoved. Cost of the stopwatches themselves: noise floor first (rule 7) at 1824/1848/1863 ms,
instrumented 1853/1857/1868, re-measured baseline 1774/1828/1847 — the two baselines differ by more
than the change does (rule 41), against an arithmetic bound of 12 QPC pairs ≈ 1 µs.

**Residual.** The launchscreen-driven rebuild is verified by construction (same `StartSession`; a
live menu launch is not scriptable here), so `boot`'s menu-wait caveat is reasoned, not measured.
A true cold OS cache cannot be forced on this machine — the cold reading is a freshly-written copy,
a floor on the penalty rather than its ceiling.

## C22 ☑ Perf suite: fixed scenarios, A/B mode, local history

**Goal.** `RunPerf.ps1` (or `RunTests.ps1 -Perf`) runs a fixed scenario set under `--det --perf` — proposed: `empty-stage` (C25), `C1` fly spawn 0 scripted hold, `C4` (building-heavy), `C2B` (water+precip), `C5` city freecam pinned pose — each a fixed number of **sim frames**, parses the `--perf` lines plus C21's startup block, and appends one JSON record per scenario to a **git-ignored** `perf-history.jsonl` at the repo root (add to `.gitignore`; `.scratch/` is swept, history must survive sweeps). Regression verdicts come from **A/B runs** (flip the one line under test per rule 10, run the suite twice back-to-back), never from committed thresholds.

**Evidence (confidence: traced).** `--perf`'s semantics and lies are documented: rules 37 (`script` reads ~2.2×, ratio-only), 38 (vsync-capped `frame`/`fps` are floors; `physics` is the collision term), 41 (sub-instrument differences are noise), 8 (machine drift), 42 (cold caches).

**Approach.** First scenario iteration runs and is discarded (cache warm-up); the record stores medians over the remaining frames, plus build metadata (git describe, dirty flag). The A/B mode is just "run suite, swap, run suite, print the paired ratios" — the script does the pairing and prints per-metric ratios with the ratio-only caveats attached.

**Verify.** Same-build noise floor first (rule 7): suite twice unchanged → the printed ratios ≈ 1 within a measured band; then a deliberate perturbation (e.g. temporarily double clutter density) → the affected scenario's ratio moves. History file grows one line per scenario per run.

**⚠ Traps.** Never let vsync-pinned `fps` into a verdict (rule 38); durations in sim frames not wall seconds (A1 makes that exact); a history *trend* is awareness, not evidence — the A/B is the only regression instrument this plan trusts.

**Landed 2026-07-25** — `RunTests.ps1`'s `perf` stage over `analysis/perf/scenarios.json`
(the plan's five scenarios: `empty-stage`, `c1-flight`, `c2b-water`, `c4-terrain`, `c5-city`), each
300 sim frames × 3 launches, medians appended to the git-ignored `perf-history.jsonl`. `-PerfLabel` /
`-PerfCompare` are the A/B. Evidence in `docs/HISTORY.md`; the standing rules are verification
100–102, and the stage's contract is in `docs/tooling.md`.

**One same-build pair is not a noise floor, and finding that out changed the design.** The first
unchanged pair put every startup phase inside ±10.5 %; the *second* reached ±14.8 % and flagged five
same-build rows against a band calibrated on the first. The marker therefore needs **both** a
relative band and an absolute floor (rule 41 made mechanical — 0.045 ms of jitter on a 0.26 ms
`gpu_ms` is a 17 % ratio and no difference), and the shipped bands are calibrated so that all three
recorded same-build pairs produce zero marks.

**The counts turned out to be the instrument; the milliseconds are the coarse fallback.**
`draws`/`prims`/`nodes` came back identical to the digit in all 10 same-build scenario pairings,
while `render_cpu_ms` spanned ±6 % and `gpu_ms` ±33 %. The perturbation (clutter tiling period
halved, one line, reverted) confirmed it: C4's sprites went ×2.26 and its `prims` ×2.03, C5's ×2.90
and ×2.92, `startup.clutter` ×1.70 / ×2.26, `startup.edge` ×2.07 — while **`empty-stage`, the
control, did not move on a single metric**. Two predictions failed and are recorded because they are
mechanism, not noise: `nodes` did *not* move on C5 (clutter placements live in MultiMesh buffers,
never as nodes, on the solid path too), and `c1-flight` moved *downwards* (halving the period
re-scatters C1's cell offsets rather than multiplying them — 9,303 sprites → 8,253, and `prims`
tracked that too, ×0.94).

**Deviation: three engine-side changes the item text did not budget for, each forced by a
measurement.** (1) `--perf`'s window is now 60 rendered frames rather than one wall second, and its
line is flat `key=value` — a wall-second window makes the sample count a function of the frame rate,
which is exactly what a paired comparison cannot have. (2) The line gained `prims`, `nodes` and
`mem_mb` alongside `draws`; the counts are the only noise-free terms in the report. (3) A new
`--no-vsync` (vsync off + `Engine.MaxFps 0`) exists because at the refresh cap `script_ms` collapses
onto the frame time — `--stage=empty` read 17.00 ms against C4's 17.20 ms with 11× the draw calls —
and it steadied `gpu_ms` (C4: 0.47–2.26 ms capped → 0.36–0.37 uncapped) and halved the wall time.
It is inert unless passed, and provably changes no simulation: the `empty-stage` golden hash holds
with it on.

**Refused, with the reason printed on every A/B: `fps`, `frame_ms`, `script_ms`, `physics_ms`,
`mem_mb`.** `physics_ms` is the one worth naming — it is empty *by construction* under `--det`
(0.01–0.04 ms in every scenario), because the fixed clock is parent-driven and `_PhysicsProcess`
consumers no-op, so collision cost lands in `script_ms`. Rule 38's advice to "read `physics` for
collision" therefore does not survive contact with `--det`.

**Residuals.** Even uncapped, this machine paces at exactly 120 fps from outside the engine
(driver or compositor — not diagnosed), so `fps`/`frame_ms` stay floors. Nothing in the suite can
see a pure C#-sim regression that stays under that cap, and `c2b-water`'s `clutter` phase (3.6 ms)
sits below the stage's 15 ms absolute floor, so even a 4× there would go unmarked. The scenarios are
all single-player and muted: splitscreen, the launchscreen, the labs and audio are unmeasured.

## C23 ☑ Golden-image tripwire

**Goal.** ~10 curated `--det` shots — proposed: the 8 chapters (`--freecam`, pinned `--pos`/`--direction` at each spawn), one `--viewer` parked plane, one `--stage=empty` — hashed as **md5 of the raw pixel buffer** (`Image.GetData()`, never the PNG file — rule 36) and recorded in a committed `analysis/goldens/manifest.json` (command line, frame number, hash — hashes and commands only, no pixels: asset-rule clean). A `goldens` suite in B12 re-renders and compares; any mismatch fails with the offending shot named and the actual image left in `.scratch/` for eyeballing.

**Evidence (confidence: traced, contingent on Wave A).** Byte-identical `--det` shots are A2/A4's verified deliverable; the manual "8-chapter regression" checklist line in `docs/verification.md` is exactly this, unautomated.

**Approach.** Manifest-driven so adding a shot is a data edit. Regeneration is deliberate: `RunTests.ps1 -RegenGoldens` rewrites hashes and the diff shows up in review. **Policy (goes into `docs/verification.md`):** a landed visual change updates the manifest in the same commit, with the shot(s) it moved named in the commit message; an *unexplained* golden flip is a stop-the-line finding.

**Verify.** Rule 14: perturb one shader constant → exactly the expected shots fail, others hold; revert → green. Two clean runs → green twice (no flaky hashes — this is the real test of Wave A).

**⚠ Traps.** A GPU driver update can legitimately flip every hash on this machine — document "regenerate after driver updates" in the manifest header; that's the accepted cost of decision 10. Goldens are a *tripwire*, not a diagnosis — a failure is investigated with the headless instruments (census, `--debug-anim`, mesh lab), not by staring at diffs.

**Landed 2026-07-25** — `analysis/goldens/manifest.json` + `README.md`, a `goldens` stage in
`RunTests.ps1` (`-RegenGoldens` / `-SkipGoldens`), and `src/Testing/GoldenShot.cs` behind one new
log line at the `--screenshot` save site: `[core] shot pixmd5=… size=… gpu=…`. Evidence in
`docs/HISTORY.md`; the standing rules are verification 95–97.

**Goldens run as their own scripted pass, NOT as a B12 suite — the constraint C24 flagged is real
and structural.** The harness runs every suite to completion inside one `_Ready` call and never
yields a frame, so nothing there can photograph anything; an async harness would have been a rewrite
of B12 to serve one stage. Driving eleven separate Godot launches from `RunTests.ps1` instead has a
second payoff the suite shape could not have: each manifest entry **is** the literal command a human
re-runs, so a failing shot is reproduced by copying one line. Cost: ~53 s for the eleven, hence
`-SkipGoldens`.

**Eleven shots, not ten, and the extra one is a flight pose** — A3's chase-camera fix made flight
byte-identical, so `c1-flight` (C1, `--hold`, chase cam + prop/control-surface animators + the whole
HUD) is in, and it is by far the strongest tripwire in the set: **34.52 % of pixels move between
sim frame 120 and 121**. The rest: the 8 chapters on pinned `--pos`/`--direction`, one `--viewer`
parked plane, one `--stage=empty`.

**Rule 80 was applied per shot as a measurement, not an argument.** Every entry carries its measured
frame-N-vs-N+1 delta, because a pose with no animated surface would pass even with the clock broken.
Six move on a one-frame perturbation (`c1-flight` 34.52 %, `empty-stage` 12.21 %, `c4-snow` 3.74 %,
`c2b-rain` 3.47 %, `c1c-rain` 2.50 %, `c1-waterfall` 1.28 %); `c1b-night-sea` needs 4 s to show its
cloud-puff drift (0.13 %); `c2-city` (51 px), `c5-city-night` (11 px), `c3-island` (9 px) and
`viewer-bhawk` (0 px) are geometry-and-shading shots and say so in the manifest.

**Known non-coverage, stated rather than papered over.** No pose in the set moves more than 9 px
across a full `TextureCycler` cycle — the water flipbooks differ by ~2/255 (rule 32), so goldens
cannot be their instrument and `--debug-anim` stays it. C1B's four UV-scroll models (the wakes) were
not located and are unrepresented; C1's and C4's scroll is covered instead. Sound is muted in every
shot; splitscreen, the launchscreen and the labs are unrepresented.

**Deviation: the hash is computed in-engine, not in PowerShell.** `Image.GetData()` is only reachable
from C#, and hashing there means no PNG decode round-trip can sit between the frame and its
fingerprint. The side effect is the useful part: *every* `--screenshot` in this project now prints
its own pixel hash, so turning any capture into a golden is a copy-paste. The adapter string rides
the same line so a driver change is visible on the log rather than inferred from a mass failure.

## C24 ☑ Texture drop-in: `--tex-override` + `--tex-census`

**Goal.** (a) `--tex-override=<name>[=<color>]` — the named texture resolves to a loud flat color (default magenta): "is this thing drawing at all?", interactively or in a shot. (b) `--tex-census` — *every* texture resolves to a unique flat color; the name→color map is logged and written to `.scratch/tex_census.json`; a suite helper answers "≥N px of texture X visible from pose Y" from one `--det` shot — the machine-readable rendering map (attacks rule 49's "every metric says live, the frame is blank" class).

**Evidence (confidence: traced).** `TextureArchive` is the single resolve point (its architecture entry: name quirks + alpha classification live there); `TextureCycler` swaps `albedo_tex` at runtime and must respect the override or census colors would revert on flipbook surfaces.

**Approach.** Override at `TextureArchive` resolution so every consumer (world, clutter, planes, puffers) inherits it; census assigns colors deterministically (hash of name → distinct RGB, generated with max separation) and keeps them opaque with the texture's original alpha *class* preserved (a hard-alpha cutout keeps its cutout, else silhouettes lie). `TextureCycler` short-circuits under census. Census pixel-counting classifies by nearest census color with a small tolerance (lighting/fog shade the flats — count by hue distance, or run census shots with `--no-fog`; decide during implementation and document).

**Verify.** Override: `--tex-override` on a known zeppelin skin texture, shot shows magenta exactly where the zeppelin is. Census: C1 pinned pose → counts for known-visible textures > 0, a texture from another chapter = 0 (able to fail); the census suite asserts a small curated set. Rule 33 check: nothing occluding/fogging the asserted surface at the chosen pose.

**⚠ Traps.** Rule 56 — the census must not perturb geometry or materials beyond the albedo swap (no shader replacement); shading still tints flats, hence tolerance-based classification. Draw-priority/subface gotchas (`docs/formats/gotchas.md`) mean a surface can be legitimately overdrawn — pick assert poses where the subject is unoccluded.

**Landed 2026-07-25** — `TextureDropIn` in `Mech3/TextureArchive.cs` (the flatten writes RGB bytes
only, leaving size, format, alpha and mip chain alone), `TextureCycler` frozen under either flag,
`SceneBuilder.Resolve` standing the aircraft paint substitution aside, two args in `PlaneViewer`
plus a count call at the screenshot site, a `tex-dropin` engine suite and three xUnit tests on the
colour hash. Evidence in `docs/HISTORY.md`; the standing rules are verification 91–92.

**The tolerance decision the item left open: chromaticity + a measured tolerance AND `--no-fog` —
both, because they answer different halves.** Fog is not residual shading to tolerate, it is the
dominant distortion and it is switchable: the same pose classified **374,491 px** confidently with
`--no-fog` against **129,210** with fog on (unmatched 19.4 % → 58.8 %). The tolerance covers what is
left, which is **not** a scalar dim: the world shader multiplies the flat by a *per-channel* vertex
colour, so half of the zeppelin's hull reads `184,0,196` where a scalar dim gives `204,0,204` — a
chromaticity shift of 0.124, bigger than any palette this size can separate. Tolerance **0.045**
with an **absolute** separation of **0.03** came from a sweep against a ground truth of 113,947 px
(the same surface under `--tex-override`) plus 60 textures that exist only in C4/C5: 75 % recall at
a worst-case 575 px credited to a texture that cannot be on screen. An absolute gap beat the ratio
margin the approach text assumed — a pixel sitting exactly on a flat has a winning distance of ~0,
which passes any ratio test however close the rival sits.

**What the item cannot deliver, and the honest replacement.** A per-texture census is **not** a
reliable single-shot answer at ~400 resolved textures: `px` is a lower bound, `px + contested` an
upper one, and a count under ~1,000 px means "not shown". "Is this drawing at all?" is
`--tex-override` — exact, needing no separation at all (113,947 px against 0) — and the census is
the map that tells you which texture to override. Eight bits a channel also cap the palette at
~200k colours, so 4 of C1's 882 textures collide outright; each is warned by name and counted in the
map rather than nudged apart, which would make a colour depend on load order.

**Deviation: no rendering suite.** The B12 harness runs every suite to completion inside one
`_Ready` call and never yields a frame, so a "≥N px from pose Y" suite would need an async harness —
C23's problem, not this item's. What landed instead is the reusable helper (`TextureDropIn.Count`,
already driving the `--screenshot` count report) plus `tex-dropin`, which asserts the invariant the
whole instrument rests on: the flatten repaints RGB and moves nothing else.

## C25 ☑ Test stages: `--stage=empty` and `--node=<cs_name>`

**Goal.** (a) `--stage=empty`: no gamez at all — a flat `StaticBody3D` ground plane with a generated grid texture, default sky/sun, plane at origin; flight, weapons and colliders fully functional; boots in ~a second. The stage for flight-model and ballistics suites and clean effect shots. (b) `--node=<name>`: `--viewer`/`--anim-lab` builds **only** the matching `cs_name` subtree from the chapter's gamez, camera auto-framed on it — the zeppelin alone, one building, one destructible.

**Evidence (confidence: traced for the builder path; direction-sound for bindings).** `SceneBuilder` is already "shared GameZ-*subtree* → MeshInstance3D" (architecture entry) — single-subtree build is its natural call shape. Name matching must use the `cs_name` meta, not Godot names (rule 60). What `AnimProgram`/`MissionSetup` do with a mostly-absent world is the open half: binding must skip-and-log unresolved targets rather than throw — audit before building.

**Approach.** `--stage=empty` is a new `WorldSession` path that skips load/build/clutter/bind and fabricates the plane+grid (grid texture generated in code — nothing committed, nothing extracted). `--node=` filters the subtree roots `WorldBuilder` hands to `SceneBuilder`; mission setup skipped; anim bind restricted to defs whose targets resolve inside the subtree (the anim-lab picker then works on just those). Multiple matches: build the first, log the full match list.

**Verify.** Empty: a flight suite takes off, fires, impacts the ground plane (impact log), `--perf` startup < ~2 s. Node: `--viewer --chapter=C1 --node=<the zeppelin's cs_name>` shows the zeppelin alone, framed; `--anim-lab --node=…` plays its def; a bogus name lists candidates and exits cleanly. 8-chapter regression untouched (both flags absent = today's paths, byte-identical shot on one chapter as the inertness check).

**⚠ Traps.** Rule 28 (file bboxes are node-frame — world-frame the auto-framing math); flat-position child indexing (`gotchas.md`) when slicing the subtree; a def whose condition reads a *missing* sibling must degrade to logged-skip, not a throw (rule 47's two-failures-look-identical — log which of "handler doesn't fire" vs "node doesn't exist" happened).

**Landed 2026-07-25** — `src/Mech3/EmptyStage.cs` (a third `StartSession` branch beside the world
build and the parked plane) and `WorldBuilder.BuildNode`/`MatchNodes`/`SuggestNodes` +
`WorldSession.Options.NodeSubtree`. Evidence in `docs/HISTORY.md`. Measured: the empty stage boots
in **1939–1993 ms** warm (build alone 821–877 ms) against C1 flight's 4474–4547 (3355), guns and
rockets both impact `ground/col`, and two `--det` runs are 0 of 921,600 px apart. Inertness: five
modes md5-identical to the pre-C25 binary, 8-chapter sound-enabled `--freecam` at 0 engine errors,
`RunTests.ps1` PASS (149 units, 7 suites, exit 0).

**The anim-bind audit, which C22 and Wave D depend on.** `AnimRuntime.Bind` **never throws** on a
mostly-absent world — it degrades in two ways that are indistinguishable from outside, which is rule
47 verbatim: an unanchored def is `continue`d (*no handler ever fires*) and an anchored def whose
event names an unbuilt node bumps `_opsUnresolved` and dispatches into nothing (*the node is not
here*). `ReportResolution` (node stages only) now separates them per definition with a cause —
C1 `--node=hk_zep`: `anchored_by_name=50 unanchored=763 target_missing_ops=134`, all 134
`why=index-not-built`, none `name-no-match`.

**The finding that changed code, and the one Wave D must not undo:** `MaxRootLift` is calibrated on
a WHOLE-WORLD node count and inverts on a slice. C1's 20-node `ap_radiotwr` bound **95 lifted defs
and 91 phantom destructible instances** because it holds 2 `healthy` nodes where the full world holds
217. `AnimRuntime.SuppressRootLift` (node stages only, refusals counted and printed) takes that to 1
def / 2 instances. **D32's node lab and D34's HP slider read the same registry**, so anything that
builds part of a world must set it or show fabricated destructibles.

**Two deviations from the item text, both forced by measurement.** (1) The framing box cannot come
from a merge over the live tree: `MeshLab` parks three EMPTY overlay meshes at the session origin,
which stretched the subtree's box from 419 m to 5.3 km and framed the camera 12 km away —
`FrameCamera` now takes a box measured at build time by `WorldBuilder.DetachedWorldAabb`
(verification rule 92). (2) The anim lab's `autoFrame` is OFF on a node stage; its per-Play re-aim
threw the tower out of frame on its own destruction (reproduced on the unchanged full-world path, so
pre-existing lab behaviour, not a regression).

**Residuals.** No interactive pass — orbit drag on a node stage, lab transport on a one-node world,
and the empty stage at the controls are unflown. On the empty stage rockets fly without their FLYOUT body and impacts draw the spark
fallback, both because the prototypes and the world-effects runtime live in a chapter gamez —
stated in `docs/cli.md` rather than worked around, since C22's scenario list wants the cheap stage,
not a half-loaded chapter.

## C26 ☑ Flight camera views: held-numpad perspectives + scripted `--view=`

**Goal.** In `--fly`/`--stunt`, holding a numpad key snaps the camera to a fixed perspective **around the plane at the chase camera's distance**, looking at the plane; releasing returns to the standard chase view — the original game's in-flight camera control. Layout (the numpad's own geometry, per the user's recall of the original): **2** = straight underside; **1**/**3** = 45° up from underside on the left/right; **4**/**6** = level left/right flank; **7**/**9** = 135° (above-flank) left/right; **8** = camera ahead of the plane, looking back at it. A scripted twin, `--view=<1–9>`, holds that perspective for the whole run — so a `--det --view=2 --screenshot` finally photographs the belly, flanks and nose of a *flying* plane (today's captures are chase-cam-only).

**Evidence (confidence: direction-sound).** The original's behaviour is user-recalled (feature and layout certain; **exact camera distance, elevation angles and whether the snap is instant are fidelity details to A/B against the original** — `OriginalScreenshots/` may hold reference captures; ask the user if one is missing, per the standing rule). Engine-side: `FlightController` owns the chase camera (module index), so the view state is a plane-frame direction override inside it. The capture gap it closes is real: M3-style items (ordnance under wings, muzzle flashes from the side) currently cannot be screenshot in flight from any angle but astern, and verification rule 34 ("one camera angle is not a test — sweep") is expensive precisely because angles aren't scriptable in flight.

**Approach.** A view table in `FlightController`: numpad key → unit direction in the plane's frame; while any mapped key is held, camera position = plane origin + direction × chase distance, look-at the plane (same distance as chase, per the original); release restores the chase pose. `--view=N` pins the same state for scripted runs. Keyboard is per-machine, so interactively this is P1's control in splitscreen; no pad binding for now (the D-pad is taken by weapon select — revisit at playtest if the original had one). Default behaviour with nothing held/passed is byte-identical to today (the inertness boundary rule).

**Verify.** (a) Scripted geometry: `--det --view=4` — log the camera's plane-frame offset; assert direction and |distance| = chase distance; repeat for all eight views in one scripted loop (the rule-34 sweep, now cheap). (b) Capture: `--det --view=2 --screenshot` over C1 shows the belly (pair with C24's census to assert the underside skin texture visible — the two instruments compose). (c) Inertness: no `--view`, no numpad → chase capture md5-identical to the pre-C26 build. (d) Fidelity: user A/B against the original for distance/angles/snap — anything off goes to `backlog.md`'s TUNE list as magnitude-TUNE, not re-derived here.

**⚠ Traps.** The angles/distance are **user-recalled, not data** — treat the layout as settled (it matches the numpad's spatial geometry) but the magnitudes as TUNE pending the original A/B; don't present the first implementation's numbers as fidelity. Audit numpad key collisions before binding (the flight key list in CLAUDE.md uses none today, but Godot distinguishes `Kp*` keycodes from digits — bind the `Kp` codes so the top-row digits stay free). Rule 15 for the capture check: a belly shot that would pass with the chase camera too is not a test — assert on content only visible from below.

**Landed 2026-07-25** — one `Views` table (direction + image up per view, both in the **plane's**
frame) plus `ActiveView`/`ApplyFixedView`/`LogView` in `FlightController`, and `--view=<1-9>` in
`PlaneViewer`. Nothing else in the tree changed. Evidence in `docs/HISTORY.md`.

**Verified.** (a) The rule-34 sweep, all eight views in one loop on the empty stage with the plane
pitched/rolled/yawed off the world frame: every plane-frame offset is exactly `dir × 16.621` — the
chase offset's own length `√(16²+4.5²)` — and every `aim` is exactly `−dir`, so the camera looks at
the plane; 90 logged lines per run, so the pose is held rather than set once, and 0 lines when no
view is active. (b) The C24 composition over C1: `--tex-override=blo_fusalagebottom` counts **19,509
magenta px from `--view=2`, 1,287 from `--view=8`, 127 from the chase camera** — rule 15's demand
that a chase shot could not pass the same assertion. (c) Inertness: three poses md5-identical to the
pre-C26 binary (0 of 921,600 px each), and the compare seen able to fail — the old binary ignores
`--view=2`, whose image differs 97.39 %. 8-chapter sound-enabled `--freecam`: 0 engine errors.
`.\RunTests.ps1` PASS (152 units, 8 suites, exit 0).

**What is NOT verified, and is not this item's to close.** The **held-key half is by construction**
— pinned and held share one code path, differing only in the `KeyDown(Kp*)` predicate, and live
keypresses are unscriptable here. The `Kp*` keycodes need **NumLock on** (Windows sends navigation
keycodes otherwise); documented, not worked around. And the **magnitudes are TUNE**: distance,
the 45° elevations, and instant-vs-eased snap all wait on the user's A/B — `backlog.md`'s TUNE list
and `playtest.md` §3. `OriginalScreenshots/` holds no usable reference (its one candidate is an
uncropped-context 460×374 crop with no HUD or horizon); nothing was inferred from it.

**Deviation from the item text:** a held key beats `--view=` rather than the reverse, so a pinned
scripted pose can still be explored at the controls; and `--view=` outside `--fly`/`--stunt` warns
and falls back rather than being silently accepted, as does `--view=5` (the middle of the pad is
where the chase camera already is, so it stays unbound).

# Wave D — Inspect layer

## D31 ☑ Shared selection: click leaf + ancestor ladder

**Goal.** In freecam and anim-lab: click any object → the struck mesh is selected and an on-screen breadcrumb shows its full `cs_name` ancestry (leaf → world root); **PgUp/PgDn walks the ladder**; a highlight box tracks the current level's subtree AABB. The selection is session state every other inspect tool reads. Zeppelin: click a motor, PgUp twice, you're on the main node.

**Evidence (confidence: direction-sound).** Anim-lab already click-picks and follows objects (its architecture entry: "click-to-follow … terrain is skipped") — **audit how it picks** (freecam/anim-lab worlds build no colliders, rule 72, so it is not a plain physics raycast against world bodies; whatever it does is the mechanism to reuse). The complaint this fixes: picking lands on leaf meshes with no way up.

**Approach.** Extract the lab's picking into a `SelectionService` (own file); breadcrumb as a HUD line (reuse `NodeLabels`' text conventions); highlight via an `ImmediateMesh` wireframe AABB. Ancestor chain from the struck node's `cs_name`-bearing ancestry (skip Godot-only wrapper nodes). Anim-lab's camera-follow rebinds to "follow the selection's current level".

**Verify.** Scripted: `--anim-lab --det` + a synthetic click at a known screen position (the `--debug-*` convention: a `--debug-select=x,y` arg) → log the resulting ladder; assert the zeppelin case lists motor → … → main node in order. Interactive pass by the user (this is their tool — the item is done when the zeppelin frustration is gone, and that's their call).

**⚠ Traps.** Rule 60 (`cs_name` meta, not Godot names — duplicates are auto-renamed). Rule 30's lesson generalized: screen-position picks depend on camera pose — the scripted test pins `--pos`/`--direction` first.

**Landed 2026-07-25** — `src/UI/SelectionService.cs` (own file, a `Node` under the session root),
`--debug-select=x,y[,up]`, and `AnimLab` reduced to a follower. Evidence in `docs/HISTORY.md`.

**The picking audit, and what D32–D35 may rely on.** The lab's click-to-follow was never a physics
raycast — it cannot be: `WorldSession.Options.Collision` is flight-only (rule 72), so these modes
build no bodies. It is a **manual ray-vs-AABB scan over the visible `MeshInstance3D`s** under the
world content root: camera `ProjectRayOrigin`/`ProjectRayNormal`, each mesh's own AABB tested in its
local frame (the affine inverse leaves the ray parameter equal to the world distance, so it orders
hits across nodes), nearest wins, one walk per click. That is now `SelectionService.PickAt`, and it
is what every later inspect tool inherits, with three properties to design around: it is
**AABB-accurate, not triangle-accurate**; a 350 m world-AABB-diagonal cap makes **terrain
unselectable** (the reason a click doesn't grab the map, and a limit D32's tree panel must be able to
reach around — `Select(Node3D)` is the programmatic entry it should use); and a miss is logged
(`select miss … tested= skipped_oversize=`), never silent. The state to bind to is
`Current`/`Ladder`/`Level`/`CurrentBox` plus `Changed(service, freshPick)`; `CurrentBox` is measured
from the selected subtree's own meshes, never through `OrbitCamera.MergedAabb` (rule 92), and the
highlight lives on the service, not inside the subtree it measures. **The ladder is deeper than the
complaint assumed** — C1's zeppelin is nine rungs from a motor's mesh to `hk_zep`, and `healthy` is
one of them, which is the rung D34's HP slider will resolve through `DestructibleRegistry`.

**Deviation from the item text:** Home/End were added beside PgUp/PgDn once the C1 zeppelin measured
nine rungs deep; "PgUp twice" was never going to reach the main node. The anim lab re-frames only on
a fresh pick and merely re-follows on a ladder walk, because re-framing each rung throws the camera
out to the whole subject's radius mid-walk.

**Residuals.** The interactive half is unverified here and is the user's call (`playtest.md`): live
mouse and key input are unscriptable in this project, so `--debug-select` drives the same
`PickAt`/`StepUp` entry points and only the event binding is by construction. The highlight box is
baked into the rung's local frame at selection time, so it tracks rigid motion exactly but does not
grow to follow articulation inside the subtree.

## D32 ☑ Node Lab (N): tree panel synced to selection

**Goal.** N toggles a dockable panel: the world's node tree (by `cs_name`), search box, two-way sync with D31's selection (click in world ⇄ click in tree), per-node actions — frame camera on it, hide/show subtree — and a **dependencies readout** for the selected node: anim defs targeting it (from `AnimProgram`), its destructible pool/HP (from `DestructibleRegistry`), its textures/materials, its colliders if built. Plus a **destructibles view** (a filter of the same tree, absorbed from M3 F41): every destructible in the chapter, camera-jumpable, with F41's coverage columns per entry — does its `ANIMATION_ROOT_NAME` resolve to real nodes, and do its sequences reference only implemented event kinds. Unresolved entries are shown loudly, never hidden.

**Evidence (confidence: traced for the data sources).** All four dependency sources exist with query surfaces: `AnimProgram` (def → resolved targets), `DestructibleRegistry.Resolve`, mesh materials on the instances, `WorldSession.Options.Collision`. The camera framing is `OrbitCamera.Frame`/`FollowNode` (already extracted for the lab).

**Approach.** Godot `Tree` control, **lazy-populated** per expanded branch (a chapter world is thousands of nodes — never build the full tree eagerly, never rebuild per frame). Hide/show flips `Visible` only (no teardown — reversible, rule 29's reversible-action preference). Search filters by `cs_name` substring, results frame-able.

**Verify.** Scripted: open panel via a `--debug-*` arg, select a known node, dump the dependencies readout to the log, assert the water tower lists its `DAMAGE_SEQUENCE` def and pool. Destructibles view: per-chapter totals match M3 A4's census (the F41 verify — also standing as B12's `destructible-census` suite). Perf: panel open in C5 city, `--perf` frame time unchanged within noise. User pass for feel.

**⚠ Traps.** Rule 43/72 family: the readout must *say* when colliders aren't built in this mode rather than showing an empty list (absence-of-instrument ≠ absence). Hide/show interacts with anim visibility ops — a def re-showing a user-hidden node is correct behaviour, not a bug; the panel should show live `Visible` state so this reads as what it is.

**Landed 2026-07-25** — `src/UI/NodeLab.cs`, four read-only accessors on `AnimRuntime`
(`AnchorsOf`/`FindNodes`/`HandledEventKinds`/`PartialEventKinds`), `SelectionService.SubtreeWorldAabb`
made public, and `--debug-nodelab[=deps,dest,open,node=<cs_name>]` in `PlaneViewer`. Evidence in
`docs/HISTORY.md`. **This absorbs M3's F41** — the destructible list, the camera jump and the
root-resolves / event-kinds-implemented coverage columns are the Destructibles view here, exactly as
the Wave-F overlap table above says; nothing is owed in M3's plan beyond the supersession mark
already there.

**Verified.** The C1 water tower reports both its pools and its `DAMAGE_SEQUENCE` (6 events,
2 thresholds) with the compiled def marked authoritative; the destructibles totals equal the
`destructible-census` suite's on a different code path (C1 267/196, C5 568/292); the C25 phantom
case reads **2 instances, not 91**, behind a red `PARTIAL WORLD` banner carrying
`root_lift_suppressed=95`; the collider line prints the not-built-in-this-mode notice, and the
branch behind it was proved to report `bodies=4 shapes_enabled=1 shapes_disabled=3` with collision
temporarily forced on. `.\RunTests.ps1` PASS (152 units, 8 suites, 11 goldens, exit 0) with the same
pose seen able to fail when the panel is open; 8-chapter sound-enabled `--freecam` at 0 errors
beyond the known C3 `!is_inside_tree()`. Perf in C5 with the panel open: every verdict metric inside
C22's bands except `draws` +22 (the panel's own UI calls).

**Three deviations.** The camera is `SpectatorCamera.Frame`/`FollowNode`, not `OrbitCamera`'s
(the orbit camera is `--viewer`-only and this lab is not). A branch caps at 500 rows with the
overflow stated — C5's world root has 557 named direct children. And `--debug-nodelab` grew a
`node=<cs_name>` selector, since a scripted run must reach a *known* node and a screen-position
pick cannot name one; it is also how anything over D31's 350 m pick cap is reached.

**Residual: the interactive half is unverified** — N, the expand arrows, the search field, the
buttons and the two-way click sync run through `--debug-nodelab` and by construction only, and the
layout was checked at 1280×720 alone. `playtest.md`, beside D31's zeppelin case.

## D33 ☑ Mesh Lab on the selection in freecam

**Goal.** M in freecam/anim-lab applies the existing mesh-lab overlays — normals, wireframe/seams, cull/normal-source overrides, steerable light — to **the selected subtree only**, restoring everything on deselect/toggle-off.

**Evidence (confidence: traced).** `MeshLab` exists viewer-only, targeting the parked plane's subtree; its architecture entry + rule 56 (override materials must replicate `SceneBuilder`'s vertex stage verbatim — its old hardcoded light re-aimed the sun) are the binding constraints. The world's shaders differ from the plane's (fog, lights, instance uniforms — `CSVM/shaders/` includes); the override materials must account for the *world* variants too.

**Approach.** Parameterize `MeshLab` on a target subtree (D31's selection) instead of the viewer's plane root; per-mesh original-material bookkeeping for exact restore; the light-steering controls stay lab-scoped and must not touch the world's `WorldLight` (rule 56 redux).

**Verify.** In freecam over C1: select a building, M on → overlays on that building only (shot: rest of world's pixels unchanged vs baseline outside the building's screen rect); M off → byte-identical to pre-toggle shot (`--det`). The A/B that matters: `cull=inverted` on a world mesh visibly flips it (able to fail).

**⚠ Traps.** Rule 56 is the whole item: any lighting/vertex divergence in the override materials silently changes what you're inspecting. Restore-on-exit must survive the selection changing while M is active.


**Landed 2026-07-25** — `MeshLab` takes a `SelectionService` in a second constructor and becomes
the *scoped* lab; M attaches, M again restores. Evidence in `docs/HISTORY.md`.

**How rule 56 was answered, and the control that proves it.** The override material is **the
surface's own shader, edited twice** — the cull token in `render_mode`, and a
`csky_lab_normal_mode` rewrite injected at the *top* of `fragment()` (the top, because the world's
fullbright variant derives its LIGHT_STATE lighting normal inside the body) — with every uniform
copied by name. Deriving replaced re-implementing because a world shader carries `unshaded`, the
sRGB vertex modulate, the light spill, cylindrical fog, UV scroll and its alpha term, none of which
the old hand-written replica had. The new `--debug-mesh=force` token builds the overrides at the
data's own settings, which must reproduce the shipped picture: **0 px** on both a world subtree and
the parked plane, against **1,682 px** (of a ~2,500 px subject) through the replica. That replica
is now the fallback for a material with no readable shader, and it announces itself.

**Deviations from the item text.** Only **M** is bound in the scoped modes — the viewer's G/N/W/B/C/V
cyclers are keys the free camera flies on, so there the panel's buttons are the interface. The light
sliders drive the lab's **own** `DirectionalLight3D`, created dark on first use, and a fullbright
target says "no light reaches it" rather than offering a control that does nothing; ambient, the
zone boxes and the viewport-wide engine wireframe are hidden in scoped mode as not-subtree-scoped.
The panel hides itself in a `--screenshot` run (the anim lab's convention), which is what let the
scoping be measured as pixels.

**Residuals.** M, the light steering and re-targeting by clicking another object while attached are
all keypress halves — unscriptable here, so they are by construction and sit in `playtest.md`. A
selected rung's collection is capped (3,000 surfaces, 60k drawn triangles) and says when it capped.

## D34 ☑ Damage sliders on the selected destructible (supersedes M3 F40)

**Goal.** When D31's selection resolves (via `DestructibleRegistry.Resolve`) to a destructible instance, H shows its pool HP with a slider + kill/reset buttons driving `AnimRuntime.DamageAt`/`ResetDestructible` — the interactive twin of `--damage-test`, on any object, in the live world.

**Evidence (confidence: traced).** The full mechanics exist headlessly (`--damage-hd` exercises damage/kill/swap/colliders/debris/reset — cli.md); **M3's F40** names exactly this UI gap (cli.md's `--damage-test` entry calls itself "the verification tool until F40's interactive HP control lands"), and this item plus D31 supersedes it — richer than F40's crosshair+keys shape (see the Wave-F overlap table above). **Dependency: M3 D32** (the world-effects runtime, landed 2026-07-24 — `AnimRuntime.PlayEffectAt`/`ExternalEffect`) is what lets stage effects *render* in freecam (rule 76: the puffer factory is otherwise torn down post-build outside the labs); confirm at implementation time that the freecam build wires it, since the HP/kill/swap/reset mechanics work either way.

**Approach.** Reuse `DamageLab`'s slider idiom (one slider, this time per selected pool, not per plane part); H context-switches: selection is a destructible → this panel; in `--viewer` H keeps meaning the plane damage lab (unchanged). F40 lives in M3's plan, not `backlog.md` (verified — no F-references there); the supersession mark in M3 is the paper trail, nothing to delete on landing.

**Verify.** Freecam C1: select the water tower, slide HP to 30 → black-smoke stage fires (log + visible once M3 D32 is in); kill → swap + collider flip logged (rule 73: report off/on separately); reset → healthy again; second kill identical (the C28 idempotency check, now interactive). Scripted variant via `--debug-select` + a scripted H for one regression-suite case.

**⚠ Traps.** Rule 72 (collider assertions need collision built — pair with D35's force flag or assert on the logged flips only); rule 75 (debris is scheduled — the interactive world's clock is running, so this is the one place it "just works"; the *scripted* variant must tick past the schedule).

**Landed 2026-07-25** — `src/UI/WorldDamageLab.cs` (own file, a `Node` under the session root),
`--debug-damage[=script]`, `DestructibleRegistry.PoolsOn`, three census helpers lifted out of
`Probes.Damage` into `Probes` so the panel and `--damage-hd` count the same way, and
`PlaneViewer.EnsureWorldEffects` (which `--destroy` now shares). Evidence in `docs/HISTORY.md`.
**This supersedes M3's F40** exactly as the Wave-F overlap table says; the supersession mark in M3's
archived plan is the whole paper trail and `backlog.md` holds no F-reference to delete (re-checked).

**The dependency the item told me to confirm rather than assume, and the answer is "no".** The
freecam build does **not** wire M3's world-effects runtime — `BuildWorldEffectsRuntime` runs in
`--fly`, `--effects-test` and, since F42, under `--destroy`; a plain `--freecam` builds none, and the
world runtime's own puffer factory is torn down after the bootstrap (rule 76). So this item wires it,
on the **first damage action** rather than at session start, and a killed C1 `m_build01` then renders
its `great_balls_of_fire` in freecam (captured). **It is not a blanket fix, which is the finding
worth carrying:** that runtime binds a fixed 28-name closure, and the water tower's progressive
stages (`sputter_black_smoke_obj`, measured in the data as a `PUFFER_STATE` def) and its own
`h2twr_puffer` sequence are not in it — they fire, log, and draw nothing outside flight. Rule 76 now
says so.

**The two-pool case, which decided the UI's shape.** C1's `ap_h2otwr1` resolves to **two** pools with
independent 60 HP — compiled `h2twr_destruction1@ap_h2otwr1` and reader `h2twr_destruction*@ap_h2otwr*`
— and `DamageAt` re-resolves through `Resolve`, so damage spent on the reader twin would drain a pool
nothing can ever hit. **Deviation from "one slider per pool": every pool is listed with live HP, but
only the reachable one carries the slider, Kill and Reset**; the others carry the reason instead, and
a scripted `pool=2,kill` is refused out loud. Verification rule 103.

**Verified — the scripted sequence, and it agrees with the headless twin exactly.**
`--freecam --chapter=C1 --det --debug-damage=node=ap_h2otwr1,hp=30,kill,tick=3.5,reset,kill`:
HP 60→30 escalates `stage=0→1` and starts `sputter_black_smoke_obj`; the kill starts
`sputter_fire_smoke_obj h2twr_destruction1`, swaps `healthy=0/1 destroyed=1/1`, flips colliders
**`off=1 on=3`** (rule 73 — reported as two numbers, never the +2 net), and reads **debris=0 pre-tick**;
`tick=3.5` then reads **debris=+2** (rule 75 — the `OBJECT_MOTION` is scheduled at t≈2.2 s, and the
whole script runs inside one frame); reset returns `hp=60/60 Healthy stage=0 healthy=1/1 destroyed=0/1`;
the second kill is line-for-line identical (the C28 idempotency check, now interactive).
`--damage-test=ap_h2otwr1 --damage-hd=60` reports the same `swap[healthy 0/1, destroyed 1/1]`,
`col[off 1, on 3]`, `debris[2 launched]`, `snd[0 played]`, `reset[healthy=✓, rekill 1h ✓]` — two code
paths, one set of numbers.

**Verified — rule 72's notice is a notice.** `--debug-damage` forces the collision build on (the
`--damage-test` precedent, one `||` in the same expression). With that force temporarily removed
(flipped, built, measured, reverted — rule 10) the same kill prints
`colliders NOT BUILT IN THIS MODE — … a census here would read zero and lie`, never an empty list.

**Verified — inertness.** `.\RunTests.ps1` PASS: 152 units, 8 engine suites, **11 goldens
hash-identical**, exit 0, 78.8 s — and 8 of those 11 are `--det --freecam` poses whose hashes were
committed before this change, which is the byte-identity claim. The compare is seen able to fail:
`c1-waterfall` with `--debug-damage=open` hashes `e790256d…` against the golden's `0bb2532d…`.
8-chapter sound-enabled `--freecam` (`--quit-after 240`, flags absent): **0 errors in seven
chapters**, the known pre-existing C3 `!is_inside_tree()` ×1 in the eighth.

**Residual: the interactive half is unverified** — H, the slider drag, the Kill/Reset buttons and the
panel's feel run through `--debug-damage` and by construction only; live mouse and key input are
unscriptable here. `playtest.md` §9, beside D31's zeppelin case and D32's panel check.

## D35 ☑ Collider wireframes (C), collision force-buildable in freecam

**Goal.** C toggles wireframe rendering of every built `CollisionShape3D` (world + clutter + plane boxes), colour-coded by owner class; `--collision` forces the collision build in freecam/anim-lab (today those modes build none — rule 72), and toggling C without it prompts with the fact instead of drawing nothing silently.

**Evidence (confidence: traced).** `WorldSession.cs:55` (`Collision` option); rule 72 (the census-reads-zero trap this UI must not reproduce); rule 39 (the clutter `ConcavePolygonShape3D` BVH build measured at ~3.4 s — the startup cost of `--collision`, worth logging via C21's phases).

**Approach.** Walk built shapes once on toggle, build `ImmediateMesh` wireframes as children (boxes/concave outlines), `Visible`-flip thereafter; regenerate on world mutations that swap colliders (destructible death) by subscribing to the registry's swap event or regenerating on toggle. `--collision` is a `WorldSession.Options` pass-through.

**Verify.** Freecam C2 with `--collision`: C on → the propane tank and gates show wireframes; kill the gate (D34) → its healthy wireframe gone, wreck's present (the rule-73 direction split, now visible). Without `--collision`: C prints the no-colliders notice. `--perf` with overlay on: draw calls rise, frame time within budget at the C4 pose.

**⚠ Traps.** Rule 72 is the item's reason and its trap — never render an empty overlay as if it were "no colliders exist". Wreck-swap colliders appear at death (rule 73); a stale overlay after a kill is a lie — regenerate on swap.

**Landed 2026-07-25** — `src/UI/ColliderOverlay.cs`, `--collision[=show]` and `--debug-colliders`.
Evidence in `docs/HISTORY.md`.

**What the shapes actually are, for whoever draws them next.** Two mechanisms, not one: world and
aircraft geometry hangs its shapes on `CollisionShape3D` nodes, but the **solid clutter attaches
shared shapes straight to a region body's RID with no node at all**, so those are read back through
`PhysicsServer3D.BodyGetShape*` — and only through those getters, since any `ShapeOwner*` call on
one of those bodies makes Godot rebuild it from the nodes it does not have and silently empty it.
Two engine limits bit: an `ImmediateMesh` caps at **256 surfaces** (one surface per *body*, never
per shape — a C2 clutter region carries thousands of placements), and a surface closed with no
vertices is an error, so emptiness is checked before one is opened.

**The staleness answer.** Rather than regenerating on a swap event, every wireframe's visibility
**follows its shape's live `Disabled` flag**, re-read 4×/s: the wreck's colliders already exist at
build time (disabled), so one walk covers both sides of every future death. When the tally moves it
logs the two directions and the names that flipped — never a net, which for C2's `gate1` is +7
against the true `col[off 1, on 8]`.

**Deviations from the item text.** `--collision` is a plain build-forcer and `=show`/`--debug-colliders`
open the overlay — the split exists because "C without `--collision`" is itself a case that must be
tested. The overlay is bound in `--freecam`/`--anim-lab`/`--fly` but **not `--viewer`**, where C is
the mesh lab's cull cycler; `--collision` there still builds the bodies and says why nothing draws.
Trimeshes over 2,000 triangles (and everything past a 400k-line budget) draw as bounding boxes.

**Residuals.** The C press itself is by construction (`playtest.md`). Counts are pose-dependent —
the map-edge extender adds clutter bodies as the camera moves — and the overlay is built once, so
bodies created after the first toggle are not in it. Cost with it up, C4 at the golden pose
(`--perf --no-vsync`, last 60-frame window): draws 2,181 → 2,532, prims 217k → 257k, nodes
16,607 → 19,195, `render_cpu` 1.05 → 1.42 ms, `gpu` 0.34 → 0.35 ms, memory 225 → 266 MB —
`fps`/`frame_ms` say nothing here, pinned at this machine's 120 fps floor (rule 102).
