# AnimRuntime role factories — retire the effects/crash construction ritual

**ACTIVE PLAN** (written 2026-07-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans.md), when every item lands.

Candidate 2 of the 2026-07-30 architecture review ([`.scratch/architecture-review-2026-07-30.html`](../.scratch/architecture-review-2026-07-30.html)),
reshaped by the grilling session that produced this plan. `AnimRuntime` is constructed three ways —
world (`WorldSession.cs:248`), world-effects (`WorldEffectsFactory.cs:125`), per-player crash rig
(`WorldEffectsFactory.cs:229`) — each an order-sensitive object initializer over ~a dozen fields
whose flag combinations turn one module into three different things. This plan extracts **two** role
factories (`ForEffects`, `ForCrashRig`) that bake the invariant flags as implementation and state
the `PufferParent`/`Rng` traps once, deletes the dead `Apply` fossil, and — first — closes the
headless-coverage gap that would otherwise let the refactor's own blast radius go unverified.

It is a **behaviour-preserving refactor**: no anim behaviour, flag value, seed derivation, or log
line changes. A golden hash that moves means the move went wrong.

**Out of scope, deliberately.** The **world** construction stays inline — after analysis it has
almost nothing invariant to hide, so a `ForWorld` factory would be an ~11-parameter relocation, not
a depth gain (see Decision 7). The review's "19 → 3 interface" headline is not adopted; the honest
result is "the effects and crash sites each shed four invariant booleans." The motion `pose(t)`
split and `AnimProgram`/`CompiledAnim` test coverage (candidates 3–4) are separate future decisions.

## Milestone goal

- `AnimRuntime.ForEffects(...)` and `AnimRuntime.ForCrashRig(...)` exist as static factories that
  return a **configured-but-unbound** runtime; each bakes `AutoStart=false`,
  `PlaceCalledTemplates=true`, `NameResolveFallback=true`, `SoundHandledElsewhere=true` as internal
  constants and takes only the role-varying values as parameters.
- `BuildWorldEffectsRuntime` and `BuildFlightCrashRuntime` call the factories, then keep their own
  `Bind(...)` + `AddChild(...)` (the world-effects sound/prewarm sequencing and the crash scene
  scaffolding never enter `AnimRuntime`).
- Dead `AnimRuntime.Apply` (zero external callers) is deleted.
- The `ForEffects` and `ForCrashRig` construction paths are each exercised by a deterministic tripwire
  in `.\RunTests.ps1` (they have none today), so "goldens green" genuinely means "these two roles
  unchanged."
- `.\RunTests.ps1` green with every prior golden hash identical.

**No behaviour change of any kind.** A golden hash that moves, a log line that changes, or a
`--debug-anim` verdict that flips means the extraction went wrong — revert and re-approach.

## Decisions (2026-07-30)

Settled in the grilling session that produced this plan. The table is the authority where prose below drifts.

| # | Question | Decision |
|---|---|---|
| 1 | Factory thickness | **Thin** — the factory owns only the runtime's construction ritual (flag combo, `Seed`→`_rng`, `PufferParent`, config). Scene scaffolding (wreck build, effect-template stage, rest-pose collection) stays in the caller and is passed in as built nodes. Thick rejected: it drags `PlaneBuilder`/`Node` deps into `AnimRuntime` and would be as untestable as today. |
| 2 | Factory home & knowledge | **Static methods on `AnimRuntime`**, taking the already-`Subset`-ed `AnimProgram` and built target `Node3D` as params. No def-specific strings (`"player_crash_dirt"`, `EffectAnimNames`) leak into the module; closure identity stays in the session layer. |
| 3 | Which flags become internal | **Bake the four role-invariant booleans** (`AutoStart=false`, `PlaceCalledTemplates=true`, `NameResolveFallback=true`, `SoundHandledElsewhere=true`) shared by effects+crash. `EffectTtl` stays a **param** (a tuning value; do not migrate the constant into the module). `Seed`/`PufferParent`/`PufferFactory`/`DebugMotions`/`PlayerPosition` stay params — they carry caller-owned values/lifetimes and **cannot** be hidden. |
| 4 | Bind ownership | **Configure-and-return (unbound).** The factory returns the configured runtime; the caller calls `Bind(target, subsetProgram)` + `AddChild`. The one real ordering trap ("set `PufferFactory` before `Bind`") is solved for free because construction wholly precedes the returned handle. Factory-owned `Bind` rejected: the world role's sounds-pool-before-`Bind` sequencing would drag `WorldSounds` into the module, and mixing self-bind for two roles but not the third is an inconsistent interface. |
| 5 | `Seed` param type | **Raw `int`.** The caller derives it (`Rng.IntSeedFor(Rng.Effects)` = fixed; `Rng.NewIntSeed(Rng.Crash)` = advancing, per-player). A `Rng.Stream` enum rejected: it can't preserve the fixed-vs-advancing split without encoding Rng policy inside `AnimRuntime`, which the `_rng` ⚠ constraint forbids. |
| 6 | Acceptance bar | **Byte-identical `.\RunTests.ps1`, no new unit tests — but only after confirming both changed paths are actually exercised** (see Wave A). Characterization tests asserting factory field values rejected: they test the implementation, not behaviour. |
| 7 | `ForWorld`? | **No — two factories only.** World is built at one site with almost nothing invariant (`AutoStart`/`Seed`/every collaborator vary); `ForWorld` would be an 11-param relocation. The real duplicated ritual + shared traps live between **effects and crash**; capture exactly that. World construction stays inline (just delete `Apply`). |
| 8 | How to cover the stranded crash path | **A minimal `--crash[=frame]` flight flag + one `--det` crash golden** (see A2), paralleling `--destroy` for world objects. A heavier fixed-step `crash-rig` suite is the fallback if the flag proves awkward. |

## ⚠ Read this before implementing anything

Findings from the coverage check (2026-07-30) that guard this plan — the instruments here lie in
exactly the two spots the refactor touches:

| # | The trap | The fact |
|---|---|---|
| 1 | "The 11 goldens will catch a construction regression." | **They cover the world runtime only.** Every shot is a freecam flyover, the viewer, the empty stage, or `c1-flight` with `--hold=0.2,0.1,0,1` (no fire, no crash). Neither the **effects** nor the **crash** construction path moves a single golden hash today. |
| 2 | "`--destroy=`/`--damage-test` exercise the crash rig." | **They drive the *world* runtime's `DamageAt`.** The per-player crash rig is a separate `AnimRuntime` built by `BuildFlightCrashRuntime`→`ForCrashRig`, played only by `FlightController.Crash()` on a live collision — **no headless trigger exists at all.** |
| 3 | "A freecam `--destroy` will render the effects runtime." | **Freecam builds no world-effects runtime** ("a plane-less `--freecam`/`--anim-lab` builds none of its own"). The effects runtime is a **flight-session** thing; A1's golden must be a flight session, and must confirm ≥1 effects puffer actually renders. |
| 4 | "The interface shrinks 19 → 3." | Overstated. `ManualAdvance` and `InheritedWorldVelocity` are **live post-build knobs** (mutated by `GameSession`/`TestHarness`/`FlightController`), never construction config — leave them as public setters. The genuine shrink is four booleans per factory. |
| 5 | "The `PufferParent`=world-root trap gets encapsulated." | It **can't** — `AnimRuntime` has no handle on the world root, so the value must come from the caller. The win for it is *locality* (the ⚠ stated once at the factory param), not encapsulation. |
| 6 | "There's existing duplication to remove." | There isn't — each role is constructed at **one** site. The justification is invariant-flag hiding + trap locality + a self-protecting type for a future second instance, not de-duplication. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value.
- **Evidence is a lead to verify, not a finding to implement.** A correct disproof that lands no code
  is a success here.
- **`CLAUDE.md` + `docs/architecture.md` are updated in the same turn** as each landed item; a landed
  item gets a dated `docs/HISTORY.md` entry. This refactor adds no new decode, so no `docs/formats/`
  page.
- **Read `docs/verification.md` before measuring anything** — and note the six traps above; this
  change's instruments are blind exactly where it edits.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts) — plus, for this plan, the two new role tripwires from Wave A.
- **Read `## src/Mech3/AnimRuntime.cs` in `docs/architecture.md` before modifying it.** The `_rng`
  ⚠ (one die per runtime, `Reseed()` also clears sound-group recency) is load-bearing here.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Pin the two uncovered paths (baseline BEFORE touching construction)

1. ☑ Effects-runtime tripwire — a `--det` flight golden that renders ≥1 `ForEffects` puffer
2. ☑ Crash-runtime tripwire — minimal `--crash[=frame]` flag + a `--det` crash golden

### Wave B — Extract the factories (behaviour-preserving; Wave A is its tripwire)

11. ☑ `AnimRuntime.ForEffects(...)` + rewire `BuildWorldEffectsRuntime`
12. ☐ `AnimRuntime.ForCrashRig(...)` + rewire `BuildFlightCrashRuntime`
13. ☐ Delete dead `AnimRuntime.Apply`; update `docs/architecture.md` + `docs/HISTORY.md`

## Dependency and parallelism notes

A1 and A2 are independent of each other and **both gate all of Wave B** — they are the only tripwire
that can see B's changes, so they land (green, hashes recorded) first. Within Wave B: **B11 and B12
both edit `WorldEffectsFactory.cs`** — never run them in parallel worktrees; do B11, then B12 on top.
B13 is last (it also touches `AnimRuntime.cs`, which B11/B12 add the factories to). No worktree is
needed; this is a linear single-context plan.

---

# Wave A — Pin the two uncovered paths

## A1 ☑ Effects-runtime tripwire

**Goal.** `.\RunTests.ps1` contains one deterministic check whose value depends on the effects
runtime (`ForEffects`) actually constructing and rendering a puffer — so a later flag/seed
transcription error in B11 flips it.

**Evidence (confidence: traced).** The effects runtime renders puffers via its own `PufferFactory`
and its own `Rng.Effects` die (`WorldEffectsFactory.cs:125–149`); several gun effects gate the
puffer behind `RANDOM_WEIGHT`, so the seed genuinely decides what renders. A world death routes
`CALL_ANIMATION` → `worldRuntime.ExternalEffect` → `effects.PlayEffectAt` (`EnsureWorldEffects`,
`WorldEffectsFactory.cs:174–177`), so a kill in a **flight** session draws effects-runtime puffers.
**Trap #3:** freecam builds no effects runtime — this must be a flight session, and you must confirm
`ExternalEffect` is wired at the moment of the kill (call `EnsureWorldEffects` if the flight build
doesn't already, or drive a real weapon impact via `ProjectilePool.EffectSink`, which builds/uses it
unconditionally).

**Approach.** Prefer reusing the existing golden pipeline: add a shot to
`analysis/goldens/manifest.json` of the form `--chapter=C1 --plane=player_bhawk --destroy=<stable
single-object def> --screenshot --det` (auto-frames on the kill via `TriggerDestroy`'s bounds).
Pick `<def>` from a `--damage-test` census — a single, always-present, puffer-building destructible
(not a wildcard). **Verify the shot renders ≥1 effects puffer** (`--effects-test` on the same chapter
lists which names build puffers; a static-geometry kill that builds none is worthless as a tripwire).
If the flight `--destroy` death does not route through `ForEffects` at capture time, fall back to a
scripted weapon hit or wire `EnsureWorldEffects` into the `--destroy` path. Record the new hash and
the GPU field.

**Model recommendation.** sonnet — mechanical once the def is chosen, but the routing confirmation is
a judgement call; escalate to opus only if the wiring proves tangled.

**Verify.** The new shot is green and stable across `--frames=N` vs `N+1` at the chosen frame (pick a
frame where the puffer is mid-life, not pre-emit). Confirm it is **able to fail**: temporarily force
`Rng.Effects` to a different seed and watch the hash move, then revert — an unmovable hash is not a
tripwire (`docs/verification.md`: an unchanged number is not evidence unless you've seen it fail).

**⚠ Traps.** #3 above (freecam builds no effects runtime). Also: `--destroy` caps at 64 and dedupes
by anchor — name a specific def so exactly one object dies and the frame is stable. Do not pick a
`RANDOM_WEIGHT`-heavy effect whose puffer is a coin-flip even under `--det` unless you've confirmed
seed 1 lands it on-screen.

## A2 ☑ Crash-runtime tripwire

**Goal.** `.\RunTests.ps1` contains one deterministic check whose value depends on the crash rig
(`ForCrashRig`) constructing and playing `player_crash_dirt` — the only path with *zero* headless
coverage today (Trap #2).

**Evidence (confidence: traced).** `FlightController.Crash()` is the sole caller of
`Play("player_crash_dirt", CrashAnchor, ...)` (`FlightController.cs:1006`); it is reached only by live
collision/death, and no flag forces it. The crash rig scatters wreck debris off `Rng.NewIntSeed(
Rng.Crash)` (an **advancing** stream, so seed-sensitive per player) and inherits world velocity
(`FlightController.cs:1005`). There is a clean model for a headless kill-and-screenshot: `--destroy`
for world objects (`ProbeRunner.TriggerDestroy`) — but nothing analogous for the player crash.

**Approach (Decision 8).** Add a minimal `--crash[=frame]` flight flag: in a `--det` flight session
it calls `FlightController.Crash()` at the given fixed sim frame (default a small constant), so a
`--det --screenshot --frames=N` run captures the wreck + debris. Add its `docs/cli.md` bullet (the
description of record) and a one-row gloss only if the CLAUDE.md table earns it. Then add one crash
golden to the manifest. This parallels `--destroy` and fills a genuine instrument gap (there is no
way to screenshot a player crash headlessly today). **Fallback:** if forcing `Crash()` mid-session is
awkward, add a fixed-step `crash-rig` suite in `Suites.cs`/`TestHarness` that builds the rig, plays
the def N ticks on the fixed clock, and asserts a deterministic scalar (debris count + rounded
centroid of flung pieces) — heavier infra, no reusable instrument, hence the fallback.

**Model recommendation.** sonnet for the flag + golden; opus if the `Crash()`-forcing touches
FlightController state machine ordering in a way that needs care.

**Verify.** Crash golden green and frame-stable at the chosen frame. **Able-to-fail check:** perturb
the `Rng.Crash` derivation and watch the debris hash move, then revert. Full 8-chapter freecam
regression unaffected (the flag is inert without `--crash`).

**⚠ Traps.** `InheritedWorldVelocity` is written every frame *after* construction — it is a live knob,
not a construction param (Trap #4); the golden exercises it but B12 must not fold it into the factory.
The crash seed **advances** the stream: capture the golden with a fixed `--seed` (implied by `--det`)
and a fixed player count so the draw order is pinned.

---

# Wave B — Extract the factories

## B11 ☑ `AnimRuntime.ForEffects(...)` + rewire `BuildWorldEffectsRuntime`

**Goal.** `BuildWorldEffectsRuntime` constructs its runtime through a named factory that hides the
four invariant booleans; its call site shrinks to the role-varying values, and behaviour is identical.

**Evidence (confidence: traced).** `WorldEffectsFactory.cs:125–141` sets, today: `AutoStart=false`,
`DebugMotions`, `PufferParent=_worldRoot`, `PufferFactory`, `PlaceCalledTemplates=true`,
`NameResolveFallback=true`, `EffectTtl=EffectRuntimeTtl`, `SoundHandledElsewhere=true`,
`Seed=Rng.IntSeedFor(Rng.Effects)`, `PlayerPosition=_playerPosition`. Then `Bind(stage,
worldProgram.Subset(EffectAnimNames))` (`:145`) and `_worldRoot.AddChild(effects)` (`:146`).

**Approach.** Add `public static AnimRuntime ForEffects(int seed, Node3D pufferParent,
Func<PufferState, Puffer> pufferFactory, bool debugMotions, float effectTtl, Vector3 playerPosition)`
to `AnimRuntime.cs` (near `Apply`/`Bind`, so the ritual sits by the fields it sets). It does
`new AnimRuntime { AutoStart=false, PlaceCalledTemplates=true, NameResolveFallback=true,
SoundHandledElsewhere=true, DebugMotions=debugMotions, PufferParent=pufferParent,
PufferFactory=pufferFactory, EffectTtl=effectTtl, Seed=seed, PlayerPosition=playerPosition }` and
**returns it unbound**. Rewrite `BuildWorldEffectsRuntime` to `var effects = AnimRuntime.ForEffects(
Rng.IntSeedFor(Rng.Effects), _worldRoot, st => Puffer.Create(st, textures, sustained: true),
_spec.DebugAnim, EffectRuntimeTtl, _playerPosition);` then keep the existing `effects.Bind(stage,
worldProgram.Subset(EffectAnimNames)); _worldRoot.AddChild(effects);`. Move the load-bearing caller
comments (the `SoundHandledElsewhere`/`Seed` rationale) onto the factory params, stated once. Match
the exact `PufferFactory` delegate type actually used (confirm the signature in `AnimRuntime`).

**Model recommendation.** sonnet — well-specified mechanical extraction, low judgement.

**Verify.** A1 green with its recorded hash; full `.\RunTests.ps1` all prior hashes identical;
`--effects-test --chapter=C1` report byte-identical to a pre-change baseline (capture it first).

**⚠ Traps.** `PufferParent` **must** be the world root even here (the crash lesson) — the param
default must not invite anything else; document it at the param (Trap #5). Do not migrate
`EffectRuntimeTtl` into `AnimRuntime` (Decision 3). Returning unbound is deliberate — do **not** call
`Bind` inside the factory (Decision 4).

## B12 ☐ `AnimRuntime.ForCrashRig(...)` + rewire `BuildFlightCrashRuntime`

**Goal.** The crash rig constructs through a named factory sharing B11's invariant block; its call
site shrinks to `seed, pufferParent, pufferFactory, debugMotions`, behaviour identical.

**Evidence (confidence: traced).** `WorldEffectsFactory.cs:229–244` sets `AutoStart=false`,
`DebugMotions`, `PufferParent=_worldRoot`, `PufferFactory`, `PlaceCalledTemplates=true`,
`NameResolveFallback=true`, `SoundHandledElsewhere=true`, `Seed=Rng.NewIntSeed(Rng.Crash)`. Note it
sets **no** `EffectTtl` and **no** `PlayerPosition` — hence a separate factory, not a shared one with
optional params (Decision 7 rationale: forcing one signature sprouts the mode flags we're killing).
Then `Bind(controller, crashProgram.Subset("player_crash_dirt"))` (`:248`) + `AddChild`.

**Approach.** Add `public static AnimRuntime ForCrashRig(int seed, Node3D pufferParent,
Func<PufferState, Puffer> pufferFactory, bool debugMotions)` — same four baked booleans, params for
the rest, **returns unbound**. Rewrite the `new AnimRuntime {…}` at `:229` to
`AnimRuntime.ForCrashRig(Rng.NewIntSeed(Rng.Crash), _worldRoot, st => Puffer.Create(st, textures,
sustained: true), _spec.DebugAnim);`, keeping the surrounding scaffolding (crashRoot, wreck build,
rest-pose collection, the `Bind(controller, crashProgram.Subset("player_crash_dirt"))`,
`controller.AddChild`, `CrashRuntime`/`CrashAnchor`/`CrashRestPoses` assignment) exactly as-is. The
big `PufferParent`=world-root ⚠ block moves onto the factory param doc, stated once for both roles.

**Model recommendation.** sonnet.

**Verify.** A2 green with its recorded hash; full `.\RunTests.ps1` prior hashes identical. Because
B11 and B12 share `WorldEffectsFactory.cs`, run after B11 lands, not beside it.

**⚠ Traps.** `Rng.NewIntSeed` (advancing) vs B11's `Rng.IntSeedFor` (fixed) — the factory takes a
raw `int`, so this distinction stays at the call site (Decision 5); do not "unify" them.
`InheritedWorldVelocity` is not a construction param (Trap #4).

## B13 ☐ Delete dead `AnimRuntime.Apply`; update docs

**Goal.** The fossil factory is gone and the module docs reflect the two role factories.

**Evidence (confidence: traced).** `AnimRuntime.Apply` (`AnimRuntime.cs:146`) has **zero external
callers** (grep: only its own body references `Bind`). It is the fossil of a narrow path that lost.

**Approach.** Delete `Apply`. If a one-line pointer helps discovery, add a short comment on `Bind`
naming the two factories; no provenance/history per coding conventions. Update `##
src/Mech3/AnimRuntime.cs` in `docs/architecture.md` — one line noting the effects/crash runtimes are
built via `ForEffects`/`ForCrashRig` (respect the max-3-⚠ budget; this is not a new ⚠, fold into the
existing prose). Append a dated `docs/HISTORY.md` entry (what landed, verified by A1/A2 + goldens,
outcome). Swap CLAUDE.md "Current status" to point past this plan (or to the next item), no longer
than before.

**Model recommendation.** sonnet.

**Verify.** Build clean (no reference to `Apply`), `.\RunTests.ps1` green, `(Get-Item
CLAUDE.md).Length` not grown.

**⚠ Traps.** Deleting `Apply` also removes its `Name="AnimRuntime"` set, but `Bind` already sets
`Name` (`AnimRuntime.cs:159`) — no behaviour lost. Do not "tidy" the world construction into a
`ForWorld` while here (Decision 7).
