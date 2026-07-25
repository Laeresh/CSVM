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

1. ☐ A1 — `GameClock`: one shared sim clock; halt (P) + frame-step (`.`) in every mode; fixed-dt under `--det`
2. ☐ A2 — Clock-driven shader time: `csky_time` global replaces `TIME` in every shader
3. ☐ A3 — One master seed: per-subsystem RNGs derived from `--seed`; `CANNON_SPREAD`, crash-sound pick, spawn, liveries all pinned
4. ☐ A4 — The `--det` bundle; `--screenshot`/dump/test runs imply it; `--no-det` opt-out
5. ☐ A5 — `--pos`/`--direction`: one placement pair in every mode (camera in freecam/viewer, plane in fly)

### Wave B — Harness + logging

11. ☐ B11 — `Log`: categories/levels, `--log=` console filter, always-on full-detail file sink
12. ☐ B12 — `--run-tests`: in-engine suite registry, pass/fail report, nonzero exit code; existing dump/damage-test assertions become suites
13. ☑ B13 — `CSVM.Tests` xUnit project: pure-logic units, local-data golden invariants, hand-authored fixtures **(done 2026-07-25 — 135 tests, no `CSVM/src/` change needed; `docs/HISTORY.md`)**
14. ☐ B14 — `RunTests.ps1`: the single entry point (build → units → suites → goldens → summary)

### Wave C — Perf + visual instruments

21. ☐ C21 — Startup-phase stopwatches (always-on structured timing log)
22. ☐ C22 — Perf suite: fixed `--det` scenarios, A/B mode, git-ignored local history
23. ☐ C23 — Golden-image tripwire: ~10 `--det` shots, committed md5 hashes of raw pixels
24. ☐ C24 — Texture drop-in: `--tex-override=<name>` + `--tex-census` (+ census assertions for suites)
25. ☐ C25 — Test stages: `--stage=empty` and `--node=<cs_name>`
26. ☐ C26 — Flight camera views: held-numpad perspectives around the plane + scripted `--view=`

### Wave D — Inspect layer

31. ☐ D31 — Shared selection: click leaf + ancestor-ladder breadcrumb, PgUp/PgDn, highlight box
32. ☐ D32 — Node Lab (N): tree panel synced to selection — search, frame, hide/show, dependencies
33. ☐ D33 — Mesh Lab (M) operates on the selected world subtree in freecam
34. ☐ D34 — Damage sliders (H) on the selected destructible — supersedes M3 F40
35. ☐ D35 — Collider wireframes (C); collision force-buildable in freecam

## Dependency and parallelism notes

A1 blocks A2 (the uniform is driven by the clock) and A4; A3 blocks A4. A5 is independent of A1–A4 and can land first — A3's and C22/C24's scripted-flight verifications get simpler once it exists. B11 before B12 (the harness speaks `Log`); B12 before B13's in-engine-adjacent invariants and before C23/C24's suite assertions; B14 closes Wave B. C21 is independent (wants B11's `perf` category). C25's empty stage feeds C22's scenario list, so C25 before C22 is convenient but not required. C26 is independent of the rest of Wave C but touches `FlightController.cs` like A1 does — land after A1, never in parallel with it. D31 blocks D32–D35 (C26 is deliberately *not* in Wave D: it hangs off the flight camera, not the shared selection). **D34 additionally depends on M3 D32** (the world-effects runtime, landed 2026-07-24) for its damage-stage effects to render in freecam — the HP/kill/reset mechanics work without it. **File contention: nearly every item touches `PlaneViewer.cs` (arg parsing, session wiring) — do not run two items of this plan in parallel worktrees.** Waves land in order; items within a wave are sequential in the listed order.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a worktree session here; use a local commit or a file copy.

---

# Wave A — Determinism core

## A1 ☐ `GameClock`: shared sim clock, halt + step everywhere, fixed-dt under `--det`

**Goal.** One clock object owns sim time for a session. Every sim consumer — `FlightModel`, `AnimRuntime.Advance`, `TextureCycler`, `Puffer`, `ProjectilePool`, prop/control-surface/wing-light animators, stunt clocks — receives dt from it, never raw `_Process` delta. Interactively, **P halts and `.` steps one frame in every mode** (fly, freecam, viewer, anim-lab). Under `--det` the clock is fixed-dt: frame N is the same sim state on every run, regardless of render rate.

**Evidence (confidence: traced).** The pattern already exists, mode-locally: `AnimLab.cs:44` (`FixedDt = 1/60`), `AnimLab.cs:165–186` (accumulator; `_fixedFrameStep` ignores wall delta in scripted runs), `AnimRuntime.cs:556` (`ManualAdvance` — the runtime detaches from `_Process` and is driven externally; `PlaneViewer.cs:902–907` explains why double-driving at 2× was the bug this prevents). The lab's P/`.`/speed transport is the interaction model to generalize. `docs/verification.md` rule 51 (a sampler paced by the clock under test cannot see a rate error) is the standing trap for verifying this.

**Approach.** New `src/Utils/GameClock.cs`: `Dt` (fixed 1/60 under det, else wall delta), `Halted`, `StepOnce()`, `Scale` (the lab's 0.1–4× selector keeps working through it). `PlaneViewer` creates one per session and hands it down; consumers swap `(float)delta` for `clock.Dt`. `AnimLab` refactors onto it (its accumulator moves into the clock) — behaviour-preserving for the lab. Fly's existing soft P-pause is replaced by the clock halt; anim-lab keys unchanged. Audio behaviour on halt: engine loops pause via `StreamPaused` where cheap; one-shots may play out — state the residual in the item's landing note rather than chasing it.

**Verify.** (a) Inert-when-absent: an unchanged build vs the refactor, same `--screenshot` pose, pixel-identical (this is a pure plumbing change until `--det` is passed — rule 15: make sure the compare *could* fail by also flipping the clock to 2× once and seeing pixels move). (b) Fixed-dt: two `--det --frames=300` runs of a flight with a scripted `--hold` dive → identical `--debug-anim` pose lines at every logged second. (c) Interactive: halt mid-flight, step 5 frames, confirm the plane advances exactly 5/60 s of sim (log the sim time). 8-chapter freecam regression, zero errors.

**⚠ Traps.** Rule 51: verify rate with wall-paced sampling, not clock-paced. Godot's physics server keeps its own tick — this project's collision is raycasts driven by our dt (`Projectile.cs:380`, `FlightController.cs:704`), so sim state follows the clock, but anything reading `GetTicksMsec` for sim purposes is a divergence bug to hunt (`PlaneViewer.cs`, `AnimRuntime.cs` are the two current users). A1 alone does **not** freeze shader motion — water/scroll/precip keep flowing until A2; don't read that as a failure.

## A2 ☐ Clock-driven shader time: `csky_time` replaces `TIME`

**Goal.** Every shader animation runs from a global uniform `csky_time`, set once per frame from the `GameClock`. A halted clock is a true freeze-frame; under `--det`, pixel output is a function of frame count — byte-identical shots.

**Evidence (confidence: traced).** Shader `TIME` sites: `SceneBuilder.cs:1041` (UV scroll; the rollover comment at `SceneBuilder.cs:997` documents a wrap hazard that a session-local uniform removes outright), `Precipitation.cs:91,95,96` (fall/sway), `PlaneViewer.cs:2317` (skydome — reads TIME + camera built-ins). No `.gdshaderinc` uses `TIME` directly (grepped 2026-07-24). `TextureCycler` and `Puffer` are CPU-driven and inherit determinism from A1. The global-uniform precedent is `csky_fog_range` (`--no-fog` writes it globally precisely to avoid the per-instance index-mismatch hazard — see `docs/cli.md` `--no-fog`).

**Approach.** Declare `csky_time` as a Godot global shader uniform (project settings `shader_globals`, like the fog globals); `PlaneViewer` (or the session) writes it each frame from the clock. Replace `TIME` at the three generator sites. Skydome: confirm what its TIME term drives (`PlaneViewer.cs:2317`) and convert it identically.

**Verify.** (a) A/B at a `TIME`-sensitive pose (C3 open water + a scrolling surface + C1C precipitation): unchanged build vs converted build at the *same wall moment* can't be compared (rule 9 — frame budgets); instead verify within the new build: `--det --shots=2` at the same frame count across two runs → **md5-identical raw pixels**, then two different frame counts → different pixels (the instrument can fail). (b) Halt in freecam over C3 water: two consecutive shots identical. (c) Full-stderr shader-error grep (rule 61). 8-chapter regression.

**⚠ Traps.** Rule 36: compare raw pixel buffers, not PNG bytes. Rule 9 becomes moot *only* under `--det` — wall-clock runs still have unequal frame budgets. This item changes pixel output of TIME-driven surfaces at any given wall moment, so any stale screenshot baselines die here — C23's goldens are captured **after** this lands, never before. `--jitter` must default 0 under `--det` (it exists to defeat bit-identical frames — the very property `--det` wants).

## A3 ☐ One master seed: per-subsystem RNGs derived from `--seed`

**Goal.** `--seed=N` (default 1 under `--det`) pins every random draw in the session: gun spread, crash-sound pick, spawn choice, liveries, `RANDOM_WEIGHT` dice. Same seed → same run; different seeds genuinely branch.

**Evidence (confidence: traced).** Unseeded draws today: `Projectile.cs:246–247` (`GD.Randf()` — the `CANNON_SPREAD` non-determinism of verification rule 77), `FlightAudio.cs:198` (`GD.Randi()` crash-sound pick), `PlaneViewer.cs:2332` (`GD.Randi()` spawn pick), `PlaneViewer.cs:1904–1910` (paint RNG, already pinnable via `--paint-seed`), `LiveryLab.cs:67`. Already seedable: `AnimRuntime.cs:143` (the lab's `Seed`).

**Approach.** Prefer **per-subsystem `RandomNumberGenerator` instances** seeded as `master ⊕ hash(subsystemName)` over one shared global sequence — call-order independent across subsystems, so adding a draw in one subsystem can't shift another's sequence. Route the four unseeded sites through named instances (`weapons`, `flightaudio`, `spawn`, `paint`); also `GD.Seed(master)` once for stragglers. `--paint-seed` stays as an override of the derived paint seed. Spawn under `--det` additionally defaults to `--spawn=0` in A4 (a pinned *choice* beats a pinned *dice roll* for scenario stability across data changes).

**Verify.** Two `--det --fire --hold=<dive>` runs over C1B water → identical impact logs (position + count), the check rule 77 says is impossible today (take the failing baseline first on the unseeded build). Crash twice with the same seed → same crash sound named in the log. 8-chapter regression.

**⚠ Traps.** Determinism of a *sequence* still requires deterministic *call order* within a subsystem — under A1's fixed clock that holds; any draw made from a wall-time or focus-dependent path (menu idle, pad rumble) must not share a sim subsystem's RNG. Unseeded (no `--det`, no `--seed`) behaviour must stay time-seeded — the shipped game keeps its variety (the boundary rule).

## A4 ☐ The `--det` bundle; scripted runs imply it

**Goal.** `--det` = fixed-dt clock + master seed 1 + `--spawn=0` (unless explicit) + pinned livery + `--no-pads` + `--jitter=0`. `--screenshot=`, every `--dump-*`, `--damage-test` and `--run-tests` **imply `--det`**; `--no-det` opts out. A `det [...]` log line announces the full resolved bundle, so every capture is self-documenting (the `--no-fog` convention).

**Evidence (confidence: traced).** The trap this kills is composition-by-memory: verification rules 62 (`--no-pads`), 77 (spread), and the "Known non-deterministic surfaces" table are all "you forgot a pinning flag" failure classes. All constituent switches exist after A1–A3; this item is wiring + defaults in `PlaneViewer` arg parsing.

**Verify.** (a) `--screenshot` twice, no other flags → md5-identical raw pixels (the headline capability). (b) `--no-det --screenshot` twice over C3 water → pixels differ (opt-out works, and the identity check is seen able to fail). (c) `--det` absent + no scripted flag → grep the log for `det` line absent, and confirm pad input still reaches the flight (inertness). Update `docs/cli.md`, CLAUDE.md's flag table, and `docs/verification.md` (new rule: scripted runs are deterministic by default; wall-clock behaviour needs `--no-det`; retire/annotate the noise-floor table entries that `--det` obsoletes — they still apply to `--no-det` runs).

**⚠ Traps.** Existing scripted workflows change pixel output once, permanently — land A4 *before* C23 captures goldens. `--frames=N` semantics tighten to "exactly sim frame N"; state it in cli.md. Do not let `--det` leak into interactive defaults: a bare `--fly` must keep random spawn/livery (playtest variety is a feature).

## A5 ☐ `--pos`/`--direction`: one placement pair in every mode

**Goal.** `--pos=x,y,z` + `--direction=x,y,z` place the **subject** of whatever mode is running: the camera in `--freecam`/`--viewer`/`--anim-lab`, the plane (spawn position + nose direction, bypassing the mission spawn list) in `--fly`/`--stunt`. One pair to remember, one syntax in every scripted run. The motivating case: a water-dive test today spawns wherever the mission says and can crash into land before reaching water — with A5 the plane starts exactly over the lake, pointed down it.

**Evidence (confidence: traced).** All four mechanics already exist, split by mode: `--campos`/`--lookat` (camera, `docs/cli.md`) and `--spawn-at`/`--spawn-dir` (plane, `docs/cli.md`: "place the plane just short of a target for a deterministic straight-line scripted run") — so this is CLI unification and reach, not new machinery. Verification rule 77's workaround ("pick a chapter whose **spawn sits over** the surface you want — C1B/C2B dive → all water") exists precisely because plane placement wasn't reached for; A5 retires it for placement-controllable tests. F11 currently prints ready-to-paste `--campos=`/`--lookat=` (CLAUDE.md keys) — it must print the new form, per mode.

**Approach.** Parse the new pair in `PlaneViewer` and route by mode onto the existing plumbing (`--campos` path for camera modes, `--spawn-at`/`--spawn-dir` path for flight). **Semantics stay explicit: `--direction` is a direction vector; `--lookat=<point>` survives as a convenience alias converted to a direction at parse time** (aim-at-a-point is genuinely useful when framing a target). `--campos`/`--spawn-at`/`--spawn-dir` become deprecated aliases that log their replacement once. F11's printout switches to `--pos=`/`--direction=`. Update `docs/cli.md`, CLAUDE.md's key line, and the `docs/verification.md` non-determinism table entries that say "pin with `--campos`/`--lookat`".

**Verify.** (a) Alias equivalence: a freecam `--pos`/`--direction` shot md5-equal to the same pose via `--campos`/`--lookat` (then move one coordinate → pixels change; the compare can fail). (b) The motivating case: `--fly --chapter=C1 --pos=<over the lake> --direction=<down the lake> --hold=<dive> --fire` → impact log reports water impacts on the first run, no land crash — the scenario rule 77 says needs chapter-shopping today. (c) Flight spawn parity: `--pos`/`--direction` in flight behaves identically to the same values via `--spawn-at`/`--spawn-dir` (same logged spawn pose). 8-chapter regression untouched with the flags absent.

**⚠ Traps.** `--lookat` is a *point*, `--direction` a *vector* — converting one to the other blindly at the plane-spawn site is the mixup to guard; keep the conversion in one parse-time place. PowerShell splits unquoted comma args into arrays (rule 63 and the `--data-root` note in cli.md) — every example in docs shows the quoted form. Mid-air placement must go through the existing `--spawn-at` code path so `FlightModel`'s initial speed/trim handling applies — do not invent a second spawn initializer.

# Wave B — Harness + logging

## B11 ☐ `Log`: categories, levels, `--log=` filter, always-on file sink

**Goal.** `src/Utils/Log.cs`: `Log.Info("anim", "motion target=... pos=...")` etc., categories ~ {`anim`,`world`,`flight`,`weapons`,`sound`,`perf`,`test`,`core`}, levels error/warn/info/debug. Console shows errors/warnings plus whatever `--log=cat[:level],...` enables; a **full-detail file sink always writes everything** to `.scratch/logs/<mode>-<timestamp>.log`, so a post-hoc grep never misses a category that wasn't enabled. Stable `[cat] message key=value` grammar so suites parse lines reliably.

**Evidence (confidence: traced).** 184 `GD.Print` sites across 29 files (grepped 2026-07-24); the failure modes of ad-hoc printing are verification rules 58 (a line structurally unable to show the thing), 59 (print caps hiding the entity under test), 61 (grep full stderr), 65 (an exception in `_Process` silently kills per-frame logging).

**Approach.** Thin static class over `GD.Print`/`GD.PrintErr` + a `StreamWriter` (line-flushed, so a crash still leaves the log — rule 65). Migration is **incremental** (decision 9): B12's harness and all new code use it from day one; `--debug-anim`/`--perf` families convert when later items touch them; no bulk sweep (rule 67 — bulk text rewrites have corrupted files here before).

**Verify.** Unit-testable pure (filter parsing, grammar formatting) in B13. In-engine: a run with `--log=anim` shows anim lines on console and *all* categories in the file; a thrown-then-caught test error appears in both. Confirm the sink's cost is invisible in `--perf` (debug level off the hot path).

**⚠ Traps.** `.scratch/` is swept by `CleanScratch.ps1` — logs are disposable by design; anything worth keeping gets moved to `analysis/` per the standing rule. Never buffer in memory (crash = lost evidence). Windows file locking: one sink per process, filename carries PID if two sessions collide.

## B12 ☐ `--run-tests`: the in-engine suite harness

**Goal.** `--run-tests[=filter]` boots the engine, runs registered assertion suites, prints a per-suite PASS/FAIL table plus a `.scratch/test-report.json`, and **exits nonzero on any failure**. The existing proto-tests become suites: `weapons-defs` (48 defs, no unhandled keys — from `--dump-weapons`), `loadout-bind` (all 11 bind, every marker resolves — from `--dump-loadout`), `damage-stages` / `damage-hd` (the C22–C28 checks from `--damage-test`), `markers-rig`, `weapons-fire` (all 48 weapons mount and fire without error — the automated half of M3 F39's verify), `destructible-census` (per-chapter destructible totals match M3 A4's census — F41's verify, automated), plus new ones as later items add them (C24 census asserts, C23 goldens).

**Evidence (confidence: traced).** The dump tools already assert (loud `!!` lines, "no unhandled keys" pass criteria — see `docs/cli.md`) but exit 0 regardless; nothing aggregates them. `SessionPaths`/`WorldSession` make world-building callable outside the interactive modes (the `--damage-test` precedent).

**Approach.** A small suite registry (name → `Action<TestContext>`); `TestContext` carries asserts, the `Log` `test` category, and data paths. The dump tools keep their flags (they're inspection reports) but their assertion cores move into shared code the suites call — one source of truth. Suites that need pixels run windowed no-focus (rule 71: `--headless` breaks readback); pure-data suites accept `--headless`. Data-dependent suites skip-with-notice when `extracted/` is absent (decision 4).

**Verify.** Rule 14 ritual: plant a deliberately failing assertion, see the nonzero exit and the report line, remove it. Then: full suite green on current data; `--run-tests=weapons` filters correctly; exit code checked from PowerShell (`$LASTEXITCODE`).

**⚠ Traps.** Rule 66 (stray Godot processes poison runs — B14's script handles the kill, scoped to this worktree's binaries); rule 74 (absolute output paths only); rule 75 (suites observing scheduled effects must tick the clock via A1's machinery, in-tree, `ManualAdvance`). A suite must never write outside `.scratch/`.

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

## B14 ☐ `RunTests.ps1`: the single entry point

**Goal.** One script: build (`dotnet build CSVM/CSVM.sln`) → `dotnet test` → `--run-tests` (windowed, `--det` implied) → golden compare (once C23 exists) → one summary block and a single exit code. Switches: `-Filter <suite>`, `-SkipUnits`, `-SkipEngine`, `-Perf` (append C22's history entry).

**Evidence (confidence: traced).** Launch mechanics are established in `RunGame.ps1`/`RunDev.ps1` and verification rules 63 (space in repo path — call operator + quoted args), 66 (kill stray Godots first, filtered to this tree's command line), 74 (absolute paths), 71 (no `--headless` for pixel suites).

**Approach.** PowerShell 5.1-safe (no `&&`), reuses the launch scripts' Godot-resolution logic (including the `CSVM_DATA_ROOT` fallback so it runs from a worktree). Aggregate exit: nonzero if any stage failed; skipped-for-data is not failure but is printed.

**Verify.** Run it green end-to-end; break one unit test and one suite in turn → script exits nonzero with the failing stage named. Run it from a worktree with `CSVM_DATA_ROOT` set → identical behaviour.

**⚠ Traps.** Rule 66's kill must be scoped (command-line filter on this worktree's path) — never a blanket Godot kill; another agent's run is not yours to kill.

# Wave C — Perf + visual instruments

## C21 ☐ Startup-phase stopwatches

**Goal.** Every session logs a structured, always-on timing breakdown on the `perf` category: data load (gamez/textures/zrdr per phase), world build, clutter, anim bind, sound prewarm, first rendered frame — `[perf] startup total=… gamez=… world=… clutter=… bind=… first_frame=…`.

**Evidence (confidence: traced).** Near-zero instrumentation today (`Stopwatch` only in `PlaneViewer.cs` and `AnimRuntime.cs`, grepped 2026-07-24); the phases are cleanly delimited in `WorldSession` (load → WorldBuilder → clutter → bind → prewarm, per its architecture entry). Rule 42 (cold OS file cache: first-run 8.1 s on 630 fresh JSONs) is the known confounder.

**Approach.** Stopwatches at the `WorldSession` phase boundaries + `PlaneViewer` session end-to-end; emit via B11. Cheap enough to run unconditionally.

**Verify.** Phase sum ≈ measured wall startup (within the residual — name what the residual contains). Numbers visibly move when they should: an unzipped vs zipped extraction changes the load phase (an A/B the docs already describe via `PreferUnzipped`).

**⚠ Traps.** Rules 41/42: cold-vs-warm cache differences dwarf real changes — C22's protocol (warm-up run discarded) is where comparisons live; this item only *reports*.

## C22 ☐ Perf suite: fixed scenarios, A/B mode, local history

**Goal.** `RunPerf.ps1` (or `RunTests.ps1 -Perf`) runs a fixed scenario set under `--det --perf` — proposed: `empty-stage` (C25), `C1` fly spawn 0 scripted hold, `C4` (building-heavy), `C2B` (water+precip), `C5` city freecam pinned pose — each a fixed number of **sim frames**, parses the `--perf` lines plus C21's startup block, and appends one JSON record per scenario to a **git-ignored** `perf-history.jsonl` at the repo root (add to `.gitignore`; `.scratch/` is swept, history must survive sweeps). Regression verdicts come from **A/B runs** (flip the one line under test per rule 10, run the suite twice back-to-back), never from committed thresholds.

**Evidence (confidence: traced).** `--perf`'s semantics and lies are documented: rules 37 (`script` reads ~2.2×, ratio-only), 38 (vsync-capped `frame`/`fps` are floors; `physics` is the collision term), 41 (sub-instrument differences are noise), 8 (machine drift), 42 (cold caches).

**Approach.** First scenario iteration runs and is discarded (cache warm-up); the record stores medians over the remaining frames, plus build metadata (git describe, dirty flag). The A/B mode is just "run suite, swap, run suite, print the paired ratios" — the script does the pairing and prints per-metric ratios with the ratio-only caveats attached.

**Verify.** Same-build noise floor first (rule 7): suite twice unchanged → the printed ratios ≈ 1 within a measured band; then a deliberate perturbation (e.g. temporarily double clutter density) → the affected scenario's ratio moves. History file grows one line per scenario per run.

**⚠ Traps.** Never let vsync-pinned `fps` into a verdict (rule 38); durations in sim frames not wall seconds (A1 makes that exact); a history *trend* is awareness, not evidence — the A/B is the only regression instrument this plan trusts.

## C23 ☐ Golden-image tripwire

**Goal.** ~10 curated `--det` shots — proposed: the 8 chapters (`--freecam`, pinned `--campos`/`--lookat` at each spawn), one `--viewer` parked plane, one `--stage=empty` — hashed as **md5 of the raw pixel buffer** (`Image.GetData()`, never the PNG file — rule 36) and recorded in a committed `analysis/goldens/manifest.json` (command line, frame number, hash — hashes and commands only, no pixels: asset-rule clean). A `goldens` suite in B12 re-renders and compares; any mismatch fails with the offending shot named and the actual image left in `.scratch/` for eyeballing.

**Evidence (confidence: traced, contingent on Wave A).** Byte-identical `--det` shots are A2/A4's verified deliverable; the manual "8-chapter regression" checklist line in `docs/verification.md` is exactly this, unautomated.

**Approach.** Manifest-driven so adding a shot is a data edit. Regeneration is deliberate: `RunTests.ps1 -RegenGoldens` rewrites hashes and the diff shows up in review. **Policy (goes into `docs/verification.md`):** a landed visual change updates the manifest in the same commit, with the shot(s) it moved named in the commit message; an *unexplained* golden flip is a stop-the-line finding.

**Verify.** Rule 14: perturb one shader constant → exactly the expected shots fail, others hold; revert → green. Two clean runs → green twice (no flaky hashes — this is the real test of Wave A).

**⚠ Traps.** A GPU driver update can legitimately flip every hash on this machine — document "regenerate after driver updates" in the manifest header; that's the accepted cost of decision 10. Goldens are a *tripwire*, not a diagnosis — a failure is investigated with the headless instruments (census, `--debug-anim`, mesh lab), not by staring at diffs.

## C24 ☐ Texture drop-in: `--tex-override` + `--tex-census`

**Goal.** (a) `--tex-override=<name>[=<color>]` — the named texture resolves to a loud flat color (default magenta): "is this thing drawing at all?", interactively or in a shot. (b) `--tex-census` — *every* texture resolves to a unique flat color; the name→color map is logged and written to `.scratch/tex_census.json`; a suite helper answers "≥N px of texture X visible from pose Y" from one `--det` shot — the machine-readable rendering map (attacks rule 49's "every metric says live, the frame is blank" class).

**Evidence (confidence: traced).** `TextureArchive` is the single resolve point (its architecture entry: name quirks + alpha classification live there); `TextureCycler` swaps `albedo_tex` at runtime and must respect the override or census colors would revert on flipbook surfaces.

**Approach.** Override at `TextureArchive` resolution so every consumer (world, clutter, planes, puffers) inherits it; census assigns colors deterministically (hash of name → distinct RGB, generated with max separation) and keeps them opaque with the texture's original alpha *class* preserved (a hard-alpha cutout keeps its cutout, else silhouettes lie). `TextureCycler` short-circuits under census. Census pixel-counting classifies by nearest census color with a small tolerance (lighting/fog shade the flats — count by hue distance, or run census shots with `--no-fog`; decide during implementation and document).

**Verify.** Override: `--tex-override` on a known zeppelin skin texture, shot shows magenta exactly where the zeppelin is. Census: C1 pinned pose → counts for known-visible textures > 0, a texture from another chapter = 0 (able to fail); the census suite asserts a small curated set. Rule 33 check: nothing occluding/fogging the asserted surface at the chosen pose.

**⚠ Traps.** Rule 56 — the census must not perturb geometry or materials beyond the albedo swap (no shader replacement); shading still tints flats, hence tolerance-based classification. Draw-priority/subface gotchas (`docs/formats/gotchas.md`) mean a surface can be legitimately overdrawn — pick assert poses where the subject is unoccluded.

## C25 ☐ Test stages: `--stage=empty` and `--node=<cs_name>`

**Goal.** (a) `--stage=empty`: no gamez at all — a flat `StaticBody3D` ground plane with a generated grid texture, default sky/sun, plane at origin; flight, weapons and colliders fully functional; boots in ~a second. The stage for flight-model and ballistics suites and clean effect shots. (b) `--node=<name>`: `--viewer`/`--anim-lab` builds **only** the matching `cs_name` subtree from the chapter's gamez, camera auto-framed on it — the zeppelin alone, one building, one destructible.

**Evidence (confidence: traced for the builder path; direction-sound for bindings).** `SceneBuilder` is already "shared GameZ-*subtree* → MeshInstance3D" (architecture entry) — single-subtree build is its natural call shape. Name matching must use the `cs_name` meta, not Godot names (rule 60). What `AnimProgram`/`MissionSetup` do with a mostly-absent world is the open half: binding must skip-and-log unresolved targets rather than throw — audit before building.

**Approach.** `--stage=empty` is a new `WorldSession` path that skips load/build/clutter/bind and fabricates the plane+grid (grid texture generated in code — nothing committed, nothing extracted). `--node=` filters the subtree roots `WorldBuilder` hands to `SceneBuilder`; mission setup skipped; anim bind restricted to defs whose targets resolve inside the subtree (the anim-lab picker then works on just those). Multiple matches: build the first, log the full match list.

**Verify.** Empty: a flight suite takes off, fires, impacts the ground plane (impact log), `--perf` startup < ~2 s. Node: `--viewer --chapter=C1 --node=<the zeppelin's cs_name>` shows the zeppelin alone, framed; `--anim-lab --node=…` plays its def; a bogus name lists candidates and exits cleanly. 8-chapter regression untouched (both flags absent = today's paths, byte-identical shot on one chapter as the inertness check).

**⚠ Traps.** Rule 28 (file bboxes are node-frame — world-frame the auto-framing math); flat-position child indexing (`gotchas.md`) when slicing the subtree; a def whose condition reads a *missing* sibling must degrade to logged-skip, not a throw (rule 47's two-failures-look-identical — log which of "handler doesn't fire" vs "node doesn't exist" happened).

## C26 ☐ Flight camera views: held-numpad perspectives + scripted `--view=`

**Goal.** In `--fly`/`--stunt`, holding a numpad key snaps the camera to a fixed perspective **around the plane at the chase camera's distance**, looking at the plane; releasing returns to the standard chase view — the original game's in-flight camera control. Layout (the numpad's own geometry, per the user's recall of the original): **2** = straight underside; **1**/**3** = 45° up from underside on the left/right; **4**/**6** = level left/right flank; **7**/**9** = 135° (above-flank) left/right; **8** = camera ahead of the plane, looking back at it. A scripted twin, `--view=<1–9>`, holds that perspective for the whole run — so a `--det --view=2 --screenshot` finally photographs the belly, flanks and nose of a *flying* plane (today's captures are chase-cam-only).

**Evidence (confidence: direction-sound).** The original's behaviour is user-recalled (feature and layout certain; **exact camera distance, elevation angles and whether the snap is instant are fidelity details to A/B against the original** — `OriginalScreenshots/` may hold reference captures; ask the user if one is missing, per the standing rule). Engine-side: `FlightController` owns the chase camera (module index), so the view state is a plane-frame direction override inside it. The capture gap it closes is real: M3-style items (ordnance under wings, muzzle flashes from the side) currently cannot be screenshot in flight from any angle but astern, and verification rule 34 ("one camera angle is not a test — sweep") is expensive precisely because angles aren't scriptable in flight.

**Approach.** A view table in `FlightController`: numpad key → unit direction in the plane's frame; while any mapped key is held, camera position = plane origin + direction × chase distance, look-at the plane (same distance as chase, per the original); release restores the chase pose. `--view=N` pins the same state for scripted runs. Keyboard is per-machine, so interactively this is P1's control in splitscreen; no pad binding for now (the D-pad is taken by weapon select — revisit at playtest if the original had one). Default behaviour with nothing held/passed is byte-identical to today (the inertness boundary rule).

**Verify.** (a) Scripted geometry: `--det --view=4` — log the camera's plane-frame offset; assert direction and |distance| = chase distance; repeat for all eight views in one scripted loop (the rule-34 sweep, now cheap). (b) Capture: `--det --view=2 --screenshot` over C1 shows the belly (pair with C24's census to assert the underside skin texture visible — the two instruments compose). (c) Inertness: no `--view`, no numpad → chase capture md5-identical to the pre-C26 build. (d) Fidelity: user A/B against the original for distance/angles/snap — anything off goes to `backlog.md`'s TUNE list as magnitude-TUNE, not re-derived here.

**⚠ Traps.** The angles/distance are **user-recalled, not data** — treat the layout as settled (it matches the numpad's spatial geometry) but the magnitudes as TUNE pending the original A/B; don't present the first implementation's numbers as fidelity. Audit numpad key collisions before binding (the flight key list in CLAUDE.md uses none today, but Godot distinguishes `Kp*` keycodes from digits — bind the `Kp` codes so the top-row digits stay free). Rule 15 for the capture check: a belly shot that would pass with the chase camera too is not a test — assert on content only visible from below.

# Wave D — Inspect layer

## D31 ☐ Shared selection: click leaf + ancestor ladder

**Goal.** In freecam and anim-lab: click any object → the struck mesh is selected and an on-screen breadcrumb shows its full `cs_name` ancestry (leaf → world root); **PgUp/PgDn walks the ladder**; a highlight box tracks the current level's subtree AABB. The selection is session state every other inspect tool reads. Zeppelin: click a motor, PgUp twice, you're on the main node.

**Evidence (confidence: direction-sound).** Anim-lab already click-picks and follows objects (its architecture entry: "click-to-follow … terrain is skipped") — **audit how it picks** (freecam/anim-lab worlds build no colliders, rule 72, so it is not a plain physics raycast against world bodies; whatever it does is the mechanism to reuse). The complaint this fixes: picking lands on leaf meshes with no way up.

**Approach.** Extract the lab's picking into a `SelectionService` (own file); breadcrumb as a HUD line (reuse `NodeLabels`' text conventions); highlight via an `ImmediateMesh` wireframe AABB. Ancestor chain from the struck node's `cs_name`-bearing ancestry (skip Godot-only wrapper nodes). Anim-lab's camera-follow rebinds to "follow the selection's current level".

**Verify.** Scripted: `--anim-lab --det` + a synthetic click at a known screen position (the `--debug-*` convention: a `--debug-select=x,y` arg) → log the resulting ladder; assert the zeppelin case lists motor → … → main node in order. Interactive pass by the user (this is their tool — the item is done when the zeppelin frustration is gone, and that's their call).

**⚠ Traps.** Rule 60 (`cs_name` meta, not Godot names — duplicates are auto-renamed). Rule 30's lesson generalized: screen-position picks depend on camera pose — the scripted test pins `--campos`/`--lookat` first.

## D32 ☐ Node Lab (N): tree panel synced to selection

**Goal.** N toggles a dockable panel: the world's node tree (by `cs_name`), search box, two-way sync with D31's selection (click in world ⇄ click in tree), per-node actions — frame camera on it, hide/show subtree — and a **dependencies readout** for the selected node: anim defs targeting it (from `AnimProgram`), its destructible pool/HP (from `DestructibleRegistry`), its textures/materials, its colliders if built. Plus a **destructibles view** (a filter of the same tree, absorbed from M3 F41): every destructible in the chapter, camera-jumpable, with F41's coverage columns per entry — does its `ANIMATION_ROOT_NAME` resolve to real nodes, and do its sequences reference only implemented event kinds. Unresolved entries are shown loudly, never hidden.

**Evidence (confidence: traced for the data sources).** All four dependency sources exist with query surfaces: `AnimProgram` (def → resolved targets), `DestructibleRegistry.Resolve`, mesh materials on the instances, `WorldSession.Options.Collision`. The camera framing is `OrbitCamera.Frame`/`FollowNode` (already extracted for the lab).

**Approach.** Godot `Tree` control, **lazy-populated** per expanded branch (a chapter world is thousands of nodes — never build the full tree eagerly, never rebuild per frame). Hide/show flips `Visible` only (no teardown — reversible, rule 29's reversible-action preference). Search filters by `cs_name` substring, results frame-able.

**Verify.** Scripted: open panel via a `--debug-*` arg, select a known node, dump the dependencies readout to the log, assert the water tower lists its `DAMAGE_SEQUENCE` def and pool. Destructibles view: per-chapter totals match M3 A4's census (the F41 verify — also standing as B12's `destructible-census` suite). Perf: panel open in C5 city, `--perf` frame time unchanged within noise. User pass for feel.

**⚠ Traps.** Rule 43/72 family: the readout must *say* when colliders aren't built in this mode rather than showing an empty list (absence-of-instrument ≠ absence). Hide/show interacts with anim visibility ops — a def re-showing a user-hidden node is correct behaviour, not a bug; the panel should show live `Visible` state so this reads as what it is.

## D33 ☐ Mesh Lab on the selection in freecam

**Goal.** M in freecam/anim-lab applies the existing mesh-lab overlays — normals, wireframe/seams, cull/normal-source overrides, steerable light — to **the selected subtree only**, restoring everything on deselect/toggle-off.

**Evidence (confidence: traced).** `MeshLab` exists viewer-only, targeting the parked plane's subtree; its architecture entry + rule 56 (override materials must replicate `SceneBuilder`'s vertex stage verbatim — its old hardcoded light re-aimed the sun) are the binding constraints. The world's shaders differ from the plane's (fog, lights, instance uniforms — `CSVM/shaders/` includes); the override materials must account for the *world* variants too.

**Approach.** Parameterize `MeshLab` on a target subtree (D31's selection) instead of the viewer's plane root; per-mesh original-material bookkeeping for exact restore; the light-steering controls stay lab-scoped and must not touch the world's `WorldLight` (rule 56 redux).

**Verify.** In freecam over C1: select a building, M on → overlays on that building only (shot: rest of world's pixels unchanged vs baseline outside the building's screen rect); M off → byte-identical to pre-toggle shot (`--det`). The A/B that matters: `cull=inverted` on a world mesh visibly flips it (able to fail).

**⚠ Traps.** Rule 56 is the whole item: any lighting/vertex divergence in the override materials silently changes what you're inspecting. Restore-on-exit must survive the selection changing while M is active.

## D34 ☐ Damage sliders on the selected destructible (supersedes M3 F40)

**Goal.** When D31's selection resolves (via `DestructibleRegistry.Resolve`) to a destructible instance, H shows its pool HP with a slider + kill/reset buttons driving `AnimRuntime.DamageAt`/`ResetDestructible` — the interactive twin of `--damage-test`, on any object, in the live world.

**Evidence (confidence: traced).** The full mechanics exist headlessly (`--damage-hd` exercises damage/kill/swap/colliders/debris/reset — cli.md); **M3's F40** names exactly this UI gap (cli.md's `--damage-test` entry calls itself "the verification tool until F40's interactive HP control lands"), and this item plus D31 supersedes it — richer than F40's crosshair+keys shape (see the Wave-F overlap table above). **Dependency: M3 D32** (the world-effects runtime, landed 2026-07-24 — `AnimRuntime.PlayEffectAt`/`ExternalEffect`) is what lets stage effects *render* in freecam (rule 76: the puffer factory is otherwise torn down post-build outside the labs); confirm at implementation time that the freecam build wires it, since the HP/kill/swap/reset mechanics work either way.

**Approach.** Reuse `DamageLab`'s slider idiom (one slider, this time per selected pool, not per plane part); H context-switches: selection is a destructible → this panel; in `--viewer` H keeps meaning the plane damage lab (unchanged). F40 lives in M3's plan, not `backlog.md` (verified — no F-references there); the supersession mark in M3 is the paper trail, nothing to delete on landing.

**Verify.** Freecam C1: select the water tower, slide HP to 30 → black-smoke stage fires (log + visible once M3 D32 is in); kill → swap + collider flip logged (rule 73: report off/on separately); reset → healthy again; second kill identical (the C28 idempotency check, now interactive). Scripted variant via `--debug-select` + a scripted H for one regression-suite case.

**⚠ Traps.** Rule 72 (collider assertions need collision built — pair with D35's force flag or assert on the logged flips only); rule 75 (debris is scheduled — the interactive world's clock is running, so this is the one place it "just works"; the *scripted* variant must tick past the schedule).

## D35 ☐ Collider wireframes (C), collision force-buildable in freecam

**Goal.** C toggles wireframe rendering of every built `CollisionShape3D` (world + clutter + plane boxes), colour-coded by owner class; `--collision` forces the collision build in freecam/anim-lab (today those modes build none — rule 72), and toggling C without it prompts with the fact instead of drawing nothing silently.

**Evidence (confidence: traced).** `WorldSession.cs:55` (`Collision` option); rule 72 (the census-reads-zero trap this UI must not reproduce); rule 39 (the clutter `ConcavePolygonShape3D` BVH build measured at ~3.4 s — the startup cost of `--collision`, worth logging via C21's phases).

**Approach.** Walk built shapes once on toggle, build `ImmediateMesh` wireframes as children (boxes/concave outlines), `Visible`-flip thereafter; regenerate on world mutations that swap colliders (destructible death) by subscribing to the registry's swap event or regenerating on toggle. `--collision` is a `WorldSession.Options` pass-through.

**Verify.** Freecam C2 with `--collision`: C on → the propane tank and gates show wireframes; kill the gate (D34) → its healthy wireframe gone, wreck's present (the rule-73 direction split, now visible). Without `--collision`: C prints the no-colliders notice. `--perf` with overlay on: draw calls rise, frame time within budget at the C4 pose.

**⚠ Traps.** Rule 72 is the item's reason and its trap — never render an empty overlay as if it were "no colliders exist". Wreck-swap colliders appear at death (rule 73); a stale overlay after a kill is a lie — regenerate on swap.
