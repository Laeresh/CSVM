# AnimRuntime dispatch-axis families

**ACTIVE PLAN** (written 2026-08-21). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans/plans.md`](plans/plans.md), when every item lands.

This plan carves the dispatch region of `CSVM/src/Mech3/AnimRuntime.cs` (the ~1,300 lines from the
`switch (ev.Kind)` at `:2030` through the per-kind handlers) into three internally owned event-kind
families, then trims the public surface using the per-caller census run on 2026-08-21. The design
was settled in a grilling session the same day (the Decisions table below is the authority) on top
of `.scratch/architecture-review-2026-08-20.html`, candidate A. Out of scope, deliberately: any
change callers can see (the facade holds), the mode-axis split (ambient/effects/crash, examined and
declined by an earlier design session), the destructible/death flow (`DestructibleRegistry` already
owns its state), and folding the config fields into `WorldSession.Options` (a real idea the census
surfaced, but it is `WorldSession` design work and belongs to a different plan). No item here is
drawn from `backlog.md`, so there is nothing to re-verify still-open.

## Milestone goal

- Three internal families in `Anim/` (sound, light, pose/visual) each own their kinds' dispatch
  bodies, state, tick and teardown; `AnimRuntime` reduces toward bind, bootstrap and route.
- The router keeps the explicit `switch (ev.Kind)` with every case label visible; only case bodies
  move. `HandledEventKinds`/`PartialEventKinds` stay verifiably in sync with the labels.
- The public surface stops growing unchecked: dead members deleted or private, single-configurer
  and test-only members `internal`, and the public declaration count recorded per wave.
- `docs/architecture.md`'s AnimRuntime and `Anim/` entries describe the new division.

**No caller sees a change.** Every production and test caller keeps talking to the `AnimRuntime`
facade exactly as today; a wave that needs to edit a golden or rewire a caller has gone wrong.

## Decisions (2026-08-21)

| # | Question | Decision |
|---|---|---|
| 1 | Goal: real seam, size relief, or surface audit first? | **Real ownership seam, with the caller census as mandatory first step** (census ran 2026-08-21, results below). File-splitting via the vestigial `partial` was declined as a starting point. |
| 2 | Does the facade hold, or do families go public? | **Facade holds.** Families are `internal`, driven only by `AnimRuntime`, matching every existing sibling. Callers reach the runtime through delegate sinks and never asked for a family. |
| 3 | Which families? | **Three: pose/visual, sound, light**, carved by owned state. Sequence-control cases stay thin shims over `SequenceRunner`; `PufferState`, `Callback`, `FbfxColorFromTo`, `ObjectAddChild` stay in the router. No manufactured families for the leftovers. |
| 3b | Who owns `_rest`? | **`_rest` stays on the runtime** as shared substrate (pose handlers and the death flow both read it); families reach it through a narrow recorder seam. |
| 4 | Seam mechanism | **Explicit `switch` stays in the router, calling family methods directly** (no kind-to-family registry). Families are plain internal classes constructed by `AnimRuntime` with per-dependency injection; **no host interface**. A family constructor wanting eight things means the boundary is wrong. |
| 5 | Pose/visual vs `MotionSet` boundary | **The family inherits the motion-builder role** (constructs `ScriptPlayback`/`SpinMotion`/`FromToMotion`/`OpacityFade`, hands them to `MotionSet`); `MotionSet` stays a pure live-set container. **The tick spine stays in `Advance`**, calling each family's tick. |
| 6 | Execution | Waves sound → light → pose/visual → surface trim; public member count is a per-wave deliverable; full suite plus goldens unchanged per wave; `architecture.md` updated per wave; a short design check inside the pose/visual item (no full design-it-twice). |
| 7 | Record | **One ADR** (`docs/adr/0001-dispatch-axis-families-for-animruntime.md`), capturing the declined mode split as context; **no CONTEXT.md entry** ("family" is implementation vocabulary, not domain language). |
| 8 | Trim aggressiveness | **Delete unread observability; demote other dead members to private; `WorldSession`-only and test-only tiers go `internal`.** No `Options` migration in this plan. Every demotion verified by the build, since object-initializer writes carry no receiver token for grep. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The 2026-08-21 per-caller census (all of `CSVM/src` outside `Mech3/`, plus `CSVM.Tests` and the
in-engine `src/Testing` suites) over the 101 public/internal member declarations of `AnimRuntime`:

- **Biggest production slices:** `Session/WorldEffectsFactory.cs` 22 members, `UI/AnimLab.cs` 12,
  `Session/GameSession.cs` 9 (plus 6 via `WorldSession.Options`), `UI/NodeLab.cs` 8,
  `UI/WorldDamageLab.cs` 7, `Session/ZeppelinRuntime.cs` 6 (+3 in `.Cannons`),
  `Flight/FlightController.cs` 5+1 doc. Eight further UI/Flight files touch only the `NameMeta` or
  `IndexMeta` constants.
- **Growth since the August census (~65 → ~101) explained:** `WorldEffectsFactory` doubled its
  slice (11 → 22), the Zeppelin combat work added 9 uses, lab/overlay tooling added ~30 references.
  The crash slice is unchanged at 6. Real callers, mostly tooling and effects config, but a large
  tail never needed to be public.
- **Family boundaries validated:** no caller reaches toward sound, light or pose internals.
  `Lights` has zero external references; sound is reached only via the `Sounds` field and the
  `OneShotSoundsPlayed` counter. Combat reaches the runtime through delegate sinks
  (`Projectile.DamageSink`/`EffectSink`, `FlightController.CollideDamageSink`/`GrazeEffectSink`,
  the crash trio `WreckVelocity`/`StopDamageStages`/`StopWreckFlying`) without naming it;
  `WorldSession.Options` is the single largest configure-without-naming seam.
- **Trim tiers** (exact member lists inline in D31/D32): ~14 members referenced nowhere outside the
  file, ~10 referenced only from `Mech3/WorldSession.cs`, ~15 referenced only from `src/Testing/*`.
  `CSVM.Tests` uses no member at all (one doc-comment mention).
- **Census caveat:** object-initializer writes (`new AnimRuntime { AutoStart = … }`) have no
  receiver token; the three initializer sites found are `WorldAndToolSuites.cs:449/837/1560` and
  `DestroyChoreographySuites.cs:70`. The compiler, not the grep, is the authority per demotion.

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

### Wave A — Sound family

1. ☑ Extract the sound family (`SoundNode`/`Sound`) into `Anim/`

### Wave B — Light family

11. ☑ Extract the light family (`LightState`/`LightAnimation`) into `Anim/`

### Wave C — Pose/visual family

21. ☐ Extract the pose/visual family, with the `_rest` seam design check

### Wave D — Surface trim

31. ☐ Delete unread observability; demote dead members to private
32. ☐ Demote the `WorldSession`-only and test-only tiers to `internal`

## Dependency and parallelism notes

Strictly linear: A1 → B11 → C21 → D31 → D32. A1 rehearses the seam shape cheaply so anything wrong
with per-dependency injection surfaces before C21 spends real effort; D31/D32 run last so the trim
is measured against the post-extraction surface. Every item edits `AnimRuntime.cs`, so no two items
ever run in parallel worktrees.

---

# Wave A — Sound family

## A1 ☑ Extract the sound family (`SoundNode`/`Sound`) into `Anim/`

**Landed.** `CSVM/src/Mech3/Anim/SoundChannel.cs` owns `HandleSoundNode`/`HandleSound`/
`OneShotSoundPosition`/`ReportLateSoundFailure`, the `_soundEmitters`/`_soundFailuresReported`
state, the census counters (`_soundsUnknown`/`_soundsAfterBuild`/`_soundCensusPrinted`) and its own
`OneShotSoundsPlayed`. `AnimRuntime` keeps `case "SoundNode":`/`case "Sound":` delegating to a
lazily-built `Sound` property (same `??=` pattern as `Emitters`), forwards `OneShotSoundsPlayed` to
the channel's counter, and keeps the two reach-ins the router still needs directly: the
`OBJECT_ACTIVE_STATE` sound-emitter test now calls `Sound.TrySetActive`, and the sound-emitter
quarter of `OBJECT_ADD_CHILD` now calls `Sound.TryGetChild`/`Sound.Attach`. `ResetToBaseState` and
`TearDownResourcesOf` hand their sound work to `Sound.Reset()`/`Sound.DiscardFor(anchor)`.
`Sounds`/`SoundHandledElsewhere` stayed public fields on `AnimRuntime`; the channel takes them as
`Func<WorldSounds?>`/`Func<bool>` closures rather than a constructor snapshot, since `Sounds` is
null until the world build finishes and can be reassigned afterwards (`WorldEffectsFactory`'s crash
runtime). The channel's other two dependencies are `Resolve` (an existing method-group delegate)
and `Func<Random>` over `_rng`, plus an `Action` callback for `_opsApplied` — five dependencies
total, matching Decision 4's "a handful". `WorldTransform` went `private static` → `internal
static` (the same visibility `NameOf`/`VisualOriginOf` already carry) so the channel's one-shot
positioning can call it without AnimRuntime having to forward it as a sixth delegate.
`docs/architecture.md`'s `AnimRuntime` and `Anim/` entries, and the top-level `src/Mech3/Anim/`
bullet list, describe the new module and the router's remaining reach-ins.

**Verified.** `.\RunTests.ps1` full pass: build, 1646/1646 units, 89/89 engine suites with zero
engine error lines, 16/16 goldens hash-identical, hitch detector healthy. `AnimRuntime`'s public
declaration count is unchanged at 96 (`'public '` matches): this wave moved bodies and state, it
did not touch the public surface, which is Wave D's job.

**Decided along the way.** `HandleAddChild`'s two sound reach-ins (the emitter lookup and the
`Sounds.Attach` call) are not case bodies the plan named for this item — `OBJECT_ADD_CHILD` stays a
router case per Decision 3 — but they read `_soundEmitters` directly, so they got two narrow methods
on the channel (`TryGetChild`, `Attach`) rather than staying reach-ins into a field that no longer
lives on `AnimRuntime`. Same reasoning for the `OBJECT_ACTIVE_STATE` sound-emitter test: it became
`Sound.TrySetActive`, which also folds in the `_opsApplied` bump so the router's case body is just
`if (Sound.TrySetActive(ev, anchor)) return true;`.

### Original approach (kept for reference)

**Goal.** The `SoundNode` and `Sound` case bodies, their state and their teardown live in one
internal family class in `Anim/`; the router case labels remain and call it directly. Sound becomes
assertable per-family with a stage and a program.

**Evidence (confidence: traced).** Handler cluster at `AnimRuntime.cs:2589–2725`
(`ReportLateSoundFailure`, `SoundEmitter`, `HandleSoundNode`, `HandleSound`,
`OneShotSoundPosition`); state `_soundEmitters` (`:424`), `_soundFailuresReported` (`:426`);
counter `OneShotSoundsPlayed` (`:660`); config `Sounds` (`:295`), `SoundHandledElsewhere` (`:262`).
Census: sound is reached from outside only via the `Sounds` field (wired by `WorldEffectsFactory`)
and the `OneShotSoundsPlayed` counter (`WorldDamageLab`, `Probes`); no caller touches internals.
Teardown reach-ins from `TearDownResourcesOf` (`:1994`) hand back to the family.

**Approach.** New internal class in `CSVM/src/Mech3/Anim/` (naming per the sibling pattern,
e.g. `SoundChannel`), constructed by `AnimRuntime` with exactly what it needs (the `WorldSounds`
handle, the resolver, the `SoundHandledElsewhere` flag, the logger); no host interface. The router
keeps `case "SoundNode":`/`case "Sound":` and delegates. `OneShotSoundsPlayed` stays on the facade,
forwarding to the family. Update `architecture.md`'s AnimRuntime and `Anim/` entries in the same
turn. The exact constructor dependency set is settled during extraction; more than a handful of
dependencies means the boundary is being drawn wrong (Decision 4).

**Model recommendation.** medium — mechanical extraction behind a settled design; the risk is
style/discipline (comment caps, decode comments moving intact), not judgement.

**Verify.** Full suite plus the golden shots, unchanged (no golden or caller edit is acceptable);
public declaration count of `AnimRuntime` recorded in the landing commit message alongside the
pre-wave count.

**⚠ Traps.** `SoundHandledElsewhere` exists because a second runtime (effects) shares the world:
the flag's semantics must move with the family intact, not be re-derived. Do not touch the
`ExternalEffect`/`ExternalEffectStop` bridge; it is effects routing, not sound.

# Wave B — Light family

## B11 ☑ Extract the light family (`LightState`/`LightAnimation`) into `Anim/`

**Landed.** `CSVM/src/Mech3/Anim/LightChannel.cs` owns `HandleLightState`/`HandleLightAnimation`/
`Tick`/`Reset`/`DiscardFor`, the `_lights` table and the bootstrap-census accessors (`Count`/
`ActiveCount`/`Names`). `AnimRuntime` keeps `case "LightState":`/`case "LightAnimation":` delegating
to a lazily-built `Light` property (same `??=` pattern as `Sound`), and `ResetToBaseState`/
`TearDownResourcesOf` hand their light work to `Light.Reset()`/`Light.DiscardFor(anchor)`. The
`Advance` tick spine calls `Light.Tick(dt)` where it called `TickLights` (Decision 5). `Lights`/
`LightViewerPositions` stayed public fields on `AnimRuntime`; the channel takes them as
`Func<WorldLights?>` and a single `Func<IReadOnlyList<Vector3>>` closure that folds
`LightViewerPositions`' null/empty fallback to `PlayerPos()` in at construction, plus `Resolve` (an
existing method-group delegate) and `Func<bool>` over `DebugMotions` — four dependencies total,
tighter than A1's five. `HandleLightState`/`HandleLightAnimation` report whether they applied
through their return value instead of taking `_opsApplied`/`Count` callbacks the way `SoundChannel`
took `recordApplied`; the router applies `_opsApplied++`/`Count("LightAnimation(no light)")` after
the call, exactly where the handlers always did it inline, which cut two more dependencies without
changing when either fires. `docs/architecture.md`'s `AnimRuntime` and `Anim/` entries, and a new
`LightChannel.cs` entry mirroring `SoundChannel.cs`'s, describe the module. `AnimRuntime`'s public
declaration count is unchanged at 96 (`'public '` matches): this wave moved bodies and state, same
as A1.

**Verified.** `.\RunTests.ps1` full pass: build, 1646/1646 units, 89/89 engine suites with zero
engine error lines, 16/16 goldens hash-identical, hitch detector healthy. `AnimRuntime`'s public
declaration count unchanged at 96.

**Decided along the way.** Rebuilding with `-t:Rebuild` (the repo's pre-commit hook forces this)
surfaced two `SA1202`/one `SA1204` StyleCop warnings already present on `main` before this item
touched anything (`AnimRuntime.Motions`/`WorldTransform` out of accessibility order, and
`SoundChannel`'s private helpers interleaved with its internal methods) — confirmed via
`git stash` against the pre-change tree. Since the hook blocks `dotnet test`/`git commit` on any
remaining warning regardless of who introduced it, this item also reorders those members (no
behaviour change, StyleCop-only) so the verification step it owes can actually run.

### Original approach (kept for reference)

**Goal.** The `LightState` and `LightAnimation` case bodies, the `_lights` table and the per-frame
light tick live in one internal family class; `Advance` calls the family's tick.

**Evidence (confidence: traced).** Handlers `HandleLightState` (`:2744`), `HandleLightAnimation`
(`:2789`), `TickLights` (`:2822`); state `_lights` (`:432`); value type `AnimLight` already in
`Anim/`. `LightAnimation` reports its `run_time` as the event's duration (decode in
`docs/formats/anim-definitions.md`); the duration-out contract of `Dispatch` must survive the move.
Census: `Lights` (`:299`) has zero external references; `LightViewerPositions` (`:119`) is set only
through `WorldSession.Options`. No caller touches light internals.

**Approach.** Same pattern as A1: internal class, per-dependency construction (the viewer-positions
func, the resolver, the logger). The tick spine stays in `Advance` (Decision 5); it calls the
family's tick where it called `TickLights`. `architecture.md` updated in the same turn.

**Model recommendation.** medium — same shape as A1, rehearsed once already.

**Verify.** As A1: full suite plus goldens unchanged; public declaration count recorded.

**⚠ Traps.** The `FbfxColorFromTo` case shares the duration-reporting behaviour but is a router
singleton, not a light: leave it and the `Rgba` helper (`:2534`, used only by it) in place.

# Wave C — Pose/visual family

## C21 ☐ Extract the pose/visual family, with the `_rest` seam design check

**Goal.** The object-pose and visual kinds (`ObjectActiveState`, `ObjectTranslateState`,
`ObjectRotateState`, `ObjectScaleState`, `ObjectMotionFromTo`, `ObjectOpacityState`,
`ObjectOpacityFromTo`, `ObjectMotion`, `ObjectMotionSiScript`) have one family owner that parses
events, constructs motions for `MotionSet`, and owns the opacity/fade machinery. This is most of
the dispatch mass and the item the first two waves rehearse for.

**Evidence (confidence: traced).** Case bodies from `:2032–2245`; opacity/fade state `_opacity`
(`:461`), `_fadeTwinCache` (`:467`), `_fadeTwins` (`:469`), `_fadeShaderCache` (`:478`),
`_resumeFromLanding` (`:476`); helpers `EnsureOpacityPath` (`:1596`), `FadeTwinOf` (`:1628`),
`ApplyOpacity` (`:1644`), `SetSubtreeOpacity` (`:1412`), `PoseTranslate`/`PoseRotate`/`PoseScale`
(`:3468–3483`). The builder/container split with `MotionSet` is documented in `architecture.md`
("never constructs a motion"); the family inherits the builder role (Decision 5), so
`architecture.md`'s line gets its subject renamed, not its meaning changed. `_rest` (`:363`) is
read by both pose handlers and the death flow (`RestoreRestPoses` `:3043`, `ApplyDeathSwap`
`:3147`) and stays on the runtime (Decision 3b).

**Approach.** Begin with the short design check the grilling reserved for this item: shape the
narrow `_rest` recorder seam (record/restore, nothing more) against both its consumers before
moving any code, and write the outcome into this section when the item lands. Then extract as in
A1/B11. Motion construction reads `org/objectMotion.md`'s decodes; the family hands finished
motions to `MotionSet` and never ticks them. `MotionRuntime`, `MotionSet` and the death flow are
not to be edited beyond the seam. `architecture.md` updated in the same turn.

**Model recommendation.** high — the design check and the `MotionSet`/death-flow boundary make
this the judgement-heavy item, the highest tier of the three extraction waves.

**Verify.** As A1, and specifically the `effect-pool-reset` suite and the motion-heavy goldens
(`c1-debris-rest` among them) unchanged; public declaration count recorded.

**⚠ Traps.** `ObjectMotion`'s launch decode has a non-normalisation rule shared with
`ProjectilePool`'s gun-casing path (INSTR-3, doc comments in `Anim/MotionRuntime.cs`): the family
must keep calling the ONE expression (`RangeLaunchDirection`/`TumbleAxis`), never re-spell it.
`MotionRuntime` seeds launches from authored rest poses; the `_rest` seam must expose what it needs
without widening into a general runtime handle.

# Wave D — Surface trim

## D31 ☐ Delete unread observability; demote dead members to private

**Goal.** Members with no reference anywhere outside `AnimRuntime.cs` and its siblings stop being
public: unread observability is deleted, real behaviour with no external caller goes private.

**Evidence (confidence: traced, census 2026-08-21).** Delete (pure unread observability):
`ActiveInstances` (`:595`), `WaitsRouted` (`:651`), `WaitsInert` (`:654`). Demote to private (real
behaviour, no external caller): `Invalidate` (`:1121`), `ResetAnimation` (`:1133`), `FirstPerson`
(`:123`), `EffectTtl` (`:270`), `DefScopedPufferKeys` (`:199`), `Lights` (`:299`); the internal
helpers `NameOf`, `VisualOriginOf`, `ConsumeLandingResume`, `SetSubtreeOpacity` follow the same
rule where the extractions have not already moved them.

**Approach.** Line numbers above are pre-extraction and will have shifted by Wave D; re-locate by
name. Where a deleted counter feeds a `Report*` method, the reporting keeps working from family
state; a counter nothing reads and nothing reports is deleted with its increments.

**Model recommendation.** medium, low effort — mechanical and compiler-verified.

**Verify.** The build is the authority (initializer writes evade grep); full suite plus goldens
unchanged; final public declaration count recorded against the plan-start count (~101 census'd
members, 96 `public` matches in the file).

**⚠ Traps.** `Invalidate`/`ResetAnimation` are public wrappers over behaviour the dispatch cases
also reach (`InvalidateAnimation`/`ResetAnimation` kinds): demote the wrappers, do not touch the
event-driven paths.

## D32 ☐ Demote the `WorldSession`-only and test-only tiers to `internal`

**Goal.** Members reachable only by their single configurer or by the in-engine suites stop
claiming `public`.

**Evidence (confidence: traced, census 2026-08-21).** `WorldSession`-only tier: `ReportResolution`,
`DebugMotions`, `QualityLod`, `Setup`, `Seed`, `ResolveLibraryRoot`, `IndexPooledCopy`,
`NameResolveFallback`, `LightViewerPositions`, `PlayerPosition`. Test-only tier (suites live in
`src/Testing` in the same assembly, so `internal` breaks nothing): `Start`, `StopAll`,
`ApplyDamageStages`, `InheritedWorldVelocity`, `InheritedVelocityArmed`, `ArmInheritedVelocity`,
`UnresolvedStageAnchors`, `UnhandledEventCounts`, `WorldRoot`, `WaitsInstalled`, `WaitsAbandoned`,
`Emitters`, `PoolRecycles`, `RestOf`, `MarkLandingResume`, `AutoStart`, `EmitterFactory` and the
two constructors. `CSVM.Tests` references no member (doc mention in `NameResolverTests.cs:95`
only).

**Approach.** `public` → `internal`, member by member, keeping the test-only counters (they earn
their keep as regression probes). No `Options` migration (Decision 8). Check `CSVM.Tests` really
compiles untouched; it is a separate project, and the census says it needs nothing here.

**Model recommendation.** medium, low effort — same shape as D31.

**Verify.** As D31; the landing commit message records the final public count as the plan's
surface-deliverable.

**⚠ Traps.** `PoolRecycles` and `PlayerPosition` have doc-comment mentions in production files
(`WorldEffectsFactory.cs:30`, `VersusHud.cs:15`); update the comments if the member name or
reachability changes, or the docs point at nothing.
