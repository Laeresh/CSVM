# PlaneViewer split — Launcher / GameSession refactor

**ACTIVE PLAN** (written 2026-07-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

`CSVM/src/PlaneViewer.cs` is 3597 lines / 216 KB — the Main.tscn root owns the entire app: the
19-phase CLI bootstrap (`_Ready`, 392–682), the launchscreen, session build (`StartSession`,
689–1906, ~1220 lines), per-frame driving (`_Process`, 3375–3509), input, screenshot capture, a
dozen dump/test probes, and a manual ~18-field null-out teardown in `ReturnToMenu`. PLAN-sessionspec
already made the CLI a pure value (`SessionSpec`); this plan does the same for the structure: a
`Launcher` scene root that owns bootstrap + menu + persistent rendering, a per-launch `GameSession`
node freed on return-to-menu, and the low-coupling clusters extracted into their own classes.
**Pure refactor — zero behavior change.** The 11 golden hashes are the equivalence gate and must
stay byte-identical on every commit. Out of scope: any new feature, any flag change, any further
decomposition of `_Process`'s weather/deck loops beyond named methods (Decision 6).

## Milestone goal

- Main.tscn's root is `src/Session/Launcher.cs`: bootstrap, engine globals, early-quit probes,
  launchscreen, persistent camera/orbit/sun/env, menu flow.
- Each launch instantiates a `src/Session/GameSession.cs` node; return-to-menu is a `QueueFree`,
  not a field-by-field null-out.
- The heavy sub-builds live in their own classes (`FlightRigAssembler`, `WorldEffectsFactory`,
  `LiveryResolver`, `SpawnPicker`, `WeatherRig`, `ProbeRunner`, `CaptureDirector`,
  `ScriptedWindow`); `StartSession`'s remainder reads as ordered phase methods.
- `PlaneViewer.cs` no longer exists; `src/` root holds only `SessionSpec.cs` and `Pads.cs`;
  GameSession is under ~800 lines.

**No behavior changes, however small.** A golden-hash move, a changed log line order the tests
read, or a new flag is a bug in this plan — behavior work waits for the next plan.

## Decisions (2026-07-30)

| # | Question | Decision |
|---|---|---|
| 1 | Launcher shape | **New scene root.** `Launcher` becomes the Main.tscn root: bootstrap + engine globals + early-quit probes + launchscreen; instantiates a session node per launch, frees it on return-to-menu (the null-out list dies structurally). |
| 2 | Persistent rendering | **Launcher owns** camera, orbit rig, sun, WorldEnvironment, and the once-per-process shader-global registration — exactly as persistent as today. GameSession receives references and only configures them. |
| 3 | StartSession split | **Hybrid.** Heavy self-contained sub-builds become classes; thin sequential glue becomes named private phase methods on GameSession. |
| 4 | File layout | **New `src/Session/`** for Launcher, GameSession, and the assemblers; probe/dump wrappers → `src/Testing/`; window-hide + focus-mute helpers → `src/Utils/`. |
| 5 | Naming | **PlaneViewer retired**, deleted at the end. Session node named **`GameSession`** (bare `Session` collides with SessionSpec/SessionPaths/WorldSession). |
| 6 | Per-frame machinery | **Split + light extract.** Per-frame/input content moves to the node that owns it; the screenshot pipeline becomes `CaptureDirector`; weather/deck loops become named methods, not classes. |
| 7 | Sequencing | **Leaves first.** Wave A extracts low-coupling clusters from the living PlaneViewer; Wave B does the structural split on the smaller class; Wave C breaks the remaining StartSession. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
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

### Wave A — leaf extractions from the living PlaneViewer

1. ☑ Probe/dump wrappers → `src/Testing/ProbeRunner`
2. ☑ Screenshot pipeline → `src/Testing/CaptureDirector`
3. ☑ Livery, spawn, and pure helpers → `src/Session/LiveryResolver` + `SpawnPicker` + homes
4. ☑ Effect/crash stage factories + name tables → `src/Session/WorldEffectsFactory`
5. ☑ Weather build + per-frame block → `src/Session/WeatherRig`
6. ☑ Window-hide → `src/Utils/ScriptedWindow`

### Wave B — the structural split

7. ☑ `Launcher` becomes the Main.tscn root; PlaneViewer becomes the instantiated session node
8. ☑ PlaneViewer → `src/Session/GameSession.cs`; teardown = QueueFree; docs rename sweep

### Wave C — break the remaining StartSession

9. ☐ StartSession → ordered phase methods on GameSession
10. ☐ Per-player flight loop + crash runtime → `src/Session/FlightRigAssembler`
11. ☐ Final sweep: `_Process` a short dispatcher, GameSession < ~800 lines, complete the plan

## Dependency and parallelism notes

Strictly linear, in listed order — every item edits the same file (PlaneViewer.cs, later
GameSession.cs). **No parallel worktrees anywhere in this plan.** A1–A6 are individually
commit-sized; B7 is the risk peak and lands alone; B8 depends on B7; C9 → C10 → C11 is a chain.

---

# Wave A — leaf extractions

Shared shape for every Wave A item: move the code into the new class, leave PlaneViewer calling it
(a thin delegation, no behavior reorder), `.\RunTests.ps1` fully green with the 11 golden hashes
byte-identical, commit via `/commit-next`. Line numbers below are from the 3597-line file as of
f57a2ac and drift as items land — re-locate by method name, not line.

## A1 ☑ Probe/dump wrappers → `src/Testing/ProbeRunner`

**Goal.** `DumpMarkers`, `DumpWeapons`, `DumpFlight`, `DumpLoadout`, `ApplyRocketOverride`,
`RunTestSuites`, `RunEffectsTest`, `RunDamageTest`, `TriggerDestroy`, `WriteScratch` live in
`src/Testing/ProbeRunner.cs`; PlaneViewer's `_Ready`/`StartSession` call it.

**Evidence (traced).** Lines 2863–3184: thin wrappers over `Testing.Probes` — read `_spec` +
paths, write scratch, quit. Only `RunTestSuites`/`RunEffectsTest` touch `_clock`/`_camera`;
`TriggerDestroy`/`ApplyRocketOverride` are already static.

**Approach.** ProbeRunner takes what the wrappers actually read (spec, paths, and for the two
entangled ones the clock/camera) as constructor or method parameters — no back-reference to the
node. The early-quit call sites in `_Ready` (590–615) and the test/destroy sites in `StartSession`
stay where they are, one line each.

**Model recommendation.** sonnet — a mechanical move of already-thin wrappers; the test suite is
its own verification.

**Verify.** `.\RunTests.ps1` (exercises `--run-tests` + goldens); plus one `--dump-flight` and one
`--damage-test` run by hand — same report bytes in `.scratch/`.

## A2 ☑ Screenshot pipeline → `src/Testing/CaptureDirector`

**Goal.** The screenshot state machine — `_pendingShot`/`_shotDelay`/`_shotIndex`/
`_shotBaseXform`/`_shotPivot`, `ApplyShotJitter`, `IndexedShotPath`, `SaveScreenshot`,
`PrintPlacement`, `Vec3Arg`/`DirArg`, and the capture block at the tail of `_Process` — becomes
`src/Testing/CaptureDirector.cs` with a per-frame `Tick()`.

**Evidence (traced).** Fields 238–244; methods 3517–3596; the `_Process` capture block
(~3470–3509) including `GoldenShot` + `TextureDropIn.CountShot` calls. Reads camera/orbit/rigs —
passed in, not reached for.

**Model recommendation.** sonnet — mechanical extraction, and the sim-frame countdown trap is
exactly what the golden hashes exist to catch.

**Verify.** Goldens are this item's own subject: all 11 hashes byte-identical proves the pipeline
moved without moving. Also one `--shots=3` burst — identical indexed filenames.

**⚠ Traps.** `--frames=N` is a sim coordinate, not a wall-clock delay — the countdown must keep
decrementing in exactly the same place in the frame as today or every golden lands on a different
sim frame.

## A3 ☑ Livery, spawn, and pure helpers to their homes

**Goal.** `PaintCatalog`/`Patterns`/`PatternsForPlane`/`ContainsPattern`/`SchemeFor`/`NewPaintRng`
→ `src/Session/LiveryResolver.cs`; `ChooseSpawnBase`/`ChooseSpawn`/`LogSpawn` →
`src/Session/SpawnPicker.cs`; `PlaneFor`/`PlaneDisplayName`/`Humanize`, `AssignPads`/`LogPads`,
`LoadRimageTexture`/`MakePuffer`, `WarmTuningRegistry` to sensible static homes.

**Evidence (traced).** Lines 2158–2298 (paint — near-pure, caches `_paintCatalog`/
`_patternLibrary`, reads `_rofPath` + spec), 2721–2774 (spawn — pure over spec + `Rng`),
2010–2036 (pads — static), 2575–2595 (loaders — static), 2838–2853 (warmup — static).

**Model recommendation.** sonnet — verbatim moves of near-pure code; the RNG-order trap is written
down and golden-guarded.

**Verify.** `.\RunTests.ps1`; liveries are seed-pinned under `--det`, so the goldens catch any
paint-RNG reorder.

**⚠ Traps.** `NewPaintRng` draw order feeds pinned liveries — do not change when or how many times
the rng is constructed or advanced. `SchemeFor` is per-player-index; keep the index math verbatim.

## A4 ☑ Effect/crash stage factories → `src/Session/WorldEffectsFactory`

**Goal.** `BuildEffectStage` ×2, `BuildWorldEffectsRuntime`, `EnsureWorldEffects`,
`BuildFlightCrashRuntime`, `CollectRestPoses`/`CollectVisibility`, `BuildCrashAnchorSet`, and the
static name tables (`EffectTemplateRoots`, `EffectAnimNames`, `EffectStageRoots`,
`CrashAnchorNodes`, `EffectRuntimeTtl`) live in `src/Session/WorldEffectsFactory.cs`.

**Evidence (traced).** Lines 2303–2566 + call sites in StartSession (`--effects-test`, projectile
sinks, anim-lab stage, `--destroy=`) and `BuildFlightCrashRuntime` from the per-player loop. Only
`_worldEffects`/`_worldRoot` bind them to instance state — the factory holds the lazily-built
runtime, PlaneViewer keeps a reference for teardown.

**Model recommendation.** sonnet — mostly-static code with enumerable call sites; the one design
choice (who holds the lazy runtime) is already decided above.

**Verify.** `.\RunTests.ps1` (the in-engine suites cover destructibles); one
`--destroy=<name> --screenshot` run and one `--effects-test` run by hand.

## A5 ☑ Weather → `src/Session/WeatherRig`

**Goal.** `LoadWeather`/`SetupWeather` (2603–2716) plus the per-rig skydome/whiteout/deck/puff
update block inside `_Process` become `src/Session/WeatherRig.cs` with `Build()` + `Tick()`.

**Evidence (traced).** Touches `_weather`, `_activeZone`, `_precip`, per-rig canvas/puff nodes,
`_deckCenter`, and **Sets** (never Adds) global shader params.

**Model recommendation.** sonnet — a contained move with one sharp, documented trap (Set vs Add)
that the manual menu-cycle check covers.

**Verify.** `.\RunTests.ps1`; goldens include foggy chapters, so a shader-param ordering change
shows up as a hash move.

**⚠ Traps.** `GlobalShaderParameterAdd` runs once per process in `_Ready` — WeatherRig must only
`Set`. The in-process relaunch (menu → session) is where an accidental re-Add crashes; test a
menu cycle by hand.

## A6 ☐ Window-hide → `src/Utils/ScriptedWindow`

**Goal.** The `ShowWindow` P/Invoke + `HideScriptedWindow` + `SwHide` (3186–3212) become
`src/Utils/ScriptedWindow.cs`.

**Evidence (traced).** Fully static, Win32-only, one call site in `_Ready`.

**Model recommendation.** haiku — a trivial static-class move with a single call site.

**Verify.** `.\RunTests.ps1` under `HiddenDesktop.ps1` — if hiding broke, test windows appear on
screen, which the user notices immediately.

# Wave B — the structural split

## B7 ☑ `Launcher` becomes the Main.tscn root

**Goal.** `src/Session/Launcher.cs` is Main.tscn's root: today's `_Ready` bootstrap (SessionSpec
parse + warnings, data-root/paths, Pads/TextureDropIn/Log/Rng/shader-global side effects,
early-quit probes, lighting + persistent camera/orbit/sun/env, gamepad roster), the menu flow
(`ShowLaunchMenu`/`StartSessionFromMenu`/`ReturnToMenu`), focus mute/`_Notification`, and
Esc-to-menu input. PlaneViewer becomes the node Launcher instantiates per launch (keeping its old
name for this one commit), constructed with `(SessionSpec spec, LauncherContext ctx)` — ctx carries
paths, camera/orbit/sun/env references, master seed, menu pads.

**Evidence (traced).** `_Ready` phases at 392–682; menu flow at 2040–2154; focus mute at
3236–3289; the persistent-node fields and the architecture.md ⚠ that shader globals register once.

**Approach.** Move, don't rewrite: the bootstrap body transplants verbatim into Launcher.
`ReturnToMenu` still calls the old teardown this commit — the QueueFree conversion is B8, so each
commit changes one thing. Main.tscn's root script swaps to Launcher.

**Model recommendation.** fable — the risk peak of the plan, and the item with the weakest
automated coverage relative to blast radius: the goldens only cover single-shot `--det` launches,
so the menu-relaunch cycle and the four entry shapes are verified by hand, and the
process-scoped-vs-session-scoped call per bootstrap phase is not fully pre-enumerated.

**Verify.** `.\RunTests.ps1`; by hand: menu → session → menu → session cycle (in-process relaunch
is where persistent-node ownership breaks), one scripted `--screenshot` run, one `--run-tests` run,
one early-quit `--dump-flight` run — all four entry shapes must still work from Launcher.

**⚠ Traps.** Neither Launcher nor the session node may grow arg-holding fields — a new flag is a
SessionSpec change (the PLAN-sessionspec rule). `SessionSpec.FromMenu(_cli, …)` derives from the
pristine `_cli` Launcher holds, never from the outgoing session's spec. `_pendingJoin`/`_menuPads`
are session/join state, not args — they ride the ctx, not the spec.

## B8 ☑ PlaneViewer → `src/Session/GameSession.cs`; teardown = QueueFree

**Goal.** The session node is renamed/moved to `src/Session/GameSession.cs`; `ReturnToMenu` frees
the node and shows the menu — the ~18-field null-out list is deleted; docs renamed in the same
turn (architecture.md entries for Launcher + GameSession replacing `## src/PlaneViewer.cs`,
CLAUDE.md module map + high-traffic list, cli.md mentions).

**Evidence (traced).** The null-out list in `ReturnToMenu` (2113–2154). Disposal duties that are
NOT node children — `_sessionTextures`, archive handles — move to `_ExitTree`/`Dispose` on
GameSession so QueueFree covers them.

**Model recommendation.** opus — deleting the null-out list is only safe if every disposal duty is
correctly re-homed; leak/double-dispose reasoning is judgement-heavy, the rename sweep is not.

**Verify.** `.\RunTests.ps1`; repeated menu ↔ session cycles watching the log for leaked-handle or
double-dispose errors; goldens unchanged.

# Wave C — break the remaining StartSession

## C9 ☐ StartSession → ordered phase methods

**Goal.** `StartSession`'s remainder reads as a short ordered sequence of named private methods on
GameSession — archives, `--node=` resolve, the empty/world/static branches, labs, cloud decks,
freecam, summary, framing — each under ~80 lines.

**Evidence (traced).** The phase table lives in the responsibility map that produced this plan;
phases are already sequential with locals handed forward. The catch block (1821–1836) must keep
wrapping the whole build — keep the try boundary where it is.

**Model recommendation.** sonnet — mechanical carving along phase boundaries that are already
mapped; the StartupProfile trace verifies ordering.

**Verify.** `.\RunTests.ps1`; the `StartupProfile` phase marks must fire in the same order (the
startup log is a free sequencing trace).

## C10 ☐ Per-player flight loop → `src/Session/FlightRigAssembler`

**Goal.** The ~260-line per-player loop (PlaneBuilder + FlightController + loadout/ordnance/
compass/gauges/reticle/audio/stunt/spawn + crash runtime hookup) becomes
`src/Session/FlightRigAssembler.cs`, called once per rig.

**Evidence (traced).** Lines 1481–1742 plus the session flight data setup (1376–1445) that feeds
it. The loop's inputs are enumerable: planes gamez, stats cache, pads, paint rng, spawn list,
weapons/loadouts, HUD assets, projectile pool, rig.

**Model recommendation.** opus — the loop mixes shared and per-player state with ordering-sensitive
RNG draws and controller-enter-tree sequencing; a wrong split compiles and passes casually.

**Verify.** `.\RunTests.ps1`; a `--players=2 --det --screenshot` splitscreen shot by hand (per-rig
assembly is where a shared-vs-per-player mixup shows).

**⚠ Traps.** Per-player draw order from the shared rngs (paint, spawn index wrap) must not change;
the loadout/rocket override is set before the controller enters the tree (its `_Ready` builds the
fire state) — keep that ordering.

## C11 ☐ Final sweep and plan completion

**Goal.** `_Process` on GameSession reads as a short dispatcher (clock/sim, startup frame,
shader time, perf, unplaced poll, WeatherRig.Tick, edge extender, CaptureDirector.Tick);
GameSession is under ~800 lines; `PlaneViewer.cs` is gone; the plan completes (COMPLETE banner,
move to `docs/plans/`, row in `plans.md`, CLAUDE.md status back to "no active plan").

**Model recommendation.** sonnet — cleanup and bookkeeping against a checklist; the perf A/B is the
only judgement call and it has a recorded baseline.

**Verify.** `.\RunTests.ps1` including `-Perf` (an A/B against perf-history.jsonl — the refactor
should be frame-cost-neutral); the full 8-chapter `--freecam --chapter=<X>` regression sweep.
