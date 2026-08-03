# Deepening — seven modules, seven waves

> **✅ COMPLETE — 2026-08-03.** Seven modules extracted behind seams (Ballistics, ImpactOutcome, MotionSet, EmitterDirector, SessionArchives, effects-builder sealing, G18's design-it-twice query); invariant enforcement moved from caller memory to module ownership; all waves landed (A–G, including `G18`/`G19` as `❌` decisions). 20 engine suites, 13 goldens hash-identical.

⚠ **Started 2026-08-03 with `A1`.** When this was written,
[`PLAN-bounce-launch`](PLAN-bounce-launch.md) was live and its uncommitted `A4` gated Waves D
and E. That plan **completed and archived the same day** (`840bc98`, `9fecbfa`, `41b5140`), so the
gate had already lapsed and every wave here was startable from the first item.

This plan turns seven shallow places into deep modules, one wave each, from the architecture review
of 2026-08-03. Every candidate was traced to file and line before it was written down; the review's
own report is `.scratch/architecture-review-artifact.html` (git-ignored, so this file is the
durable record). The unit of work is a **seam**: each wave moves a concept that is currently spread
across several modules behind one interface, and then proves the interface is the test surface by
asserting through it. **No behaviour change is intended anywhere in this plan** — the 13 goldens
staying hash-identical is the primary evidence for every wave, which is why a wave that cannot keep
them identical stops rather than repins them.

**Out of scope: anything that changes what the engine draws or simulates.** Bugs found while
refactoring get filed in `backlog.md`, not fixed in place — a refactor commit whose goldens moved
cannot tell you which half did it. Two known bugs (`BL-232`, `BL-241`) are exceptions, and only
because each is *caused* by the shallowness the wave removes; both are named in their item.

## Milestone goal

- The animation runtime's two spread concepts — a motion that owes a `BOUNCE_SEQUENCE`, and an
  emitter's lifetime — each live in one module with one interface.
- Emitter lifetime is assertable by an engine suite without a GPU, closing `BL-241`.
- The weapon-fire cluster's central decision (which effect, which sound, which damage) is a value a
  unit test can read, and the ballistic model exists once instead of twice.
- The two invariants a caller currently has to remember — archive lifetime and the effects-runtime
  cache — are enforced by the modules that own them, closing `BL-232`.
- `AnimRuntime`'s 3,647 lines and 66 public declarations are smaller by two extracted modules, and
  a recorded decision says whether the remaining three-modes-one-interface shape gets split.

**No wave adds a feature, and no wave repins a golden.** A refactor that needs a golden rebaselined
has changed behaviour, which means the wave is wrong or an unrelated bug rode in with it. Stop and
split.

## Decisions (2026-08-03)

Settled while reading the review against the live plans. This table is the authority where the
prose disagrees with itself.

| # | Question | Decision |
|---|---|---|
| 1 | Wave order — by recommendation strength, or by what is unblocked? | **Was: by what is unblocked. Now: a preference, not a dependency.** The constraint was `PLAN-bounce-launch`'s uncommitted `A4` in `AnimRuntime.cs`; that plan completed on 2026-08-03, so D and E are free. The order below survives on a weaker argument — A–C are graded traced and small, so they establish the goldens-identical discipline before the two judgement-call waves have to spend it. **Reorder freely; only the chains and the file contention are binding.** |
| 2 | Wave D collides with `PLAN-bounce-launch` `A4`. Fold it in? | **No — and `A4` landed as designed on 2026-08-03 (`840bc98`).** Folding would have destroyed `A4`'s verify story, whose evidence is a `--debug-anim` A/B against a known baseline. The cost is now paid rather than predicted: `A4` landed a fifth holder of the bounce concept, and `D9` removes it. |
| 3 | `BL-241` says its fix is `TexturesOutliveBuild = true` in the harness. Is that Wave E or Wave F? | **Wave E, and not by that route.** The flag makes the harness build *real* emitters, which still needs a live `TextureArchive` and a `MultiMesh`. The `IEmitterFactory` seam lets a suite assert on lifetime with neither. `F16` still sets the flag, for the world-build path that genuinely wants real ones. |
| 4 | Is Wave G in scope at all, or just noted? | **In scope as a gate, not a commitment.** `G18` is a design-it-twice decision that may land as `❌` with an entry in `docs/architecture.md` saying why — which is a success. `G19` executes only if `G18` says go. |
| 5 | Wave C converts the remaining `GD.Print` sites? | **No.** `docs/architecture.md`'s `src/Utils/Log.cs` entry forbids a bulk sweep (SHELL-3). `C6` adds the seam; `C7` converts exactly one family to prove the seam works. The rest converts incrementally, as items touch it. |
| 6 | What counts as "verified" for a pure refactor? | **13/13 goldens hash-identical plus `.\RunTests.ps1` green — and at least one new assertion that has been seen able to fail.** An unchanged green proves nothing on its own; every wave lands a test that was watched failing first. |
| 7 | Does every wave get a `/grilling` session? | **No — only the two whose interface is open: `D8` and `E12`.** (Both ran on 2026-08-03; their output is the Decisions 9–18 and 19–27 tables below.) A, B, C and F are graded traced: the fix shape is dictated by the code, and grilling a settled shape is ceremony. `G18` is already a decision item by construction. |
| 8 | When do those sessions run? | **Whenever — they contend on nothing.** A grilling session lands no code, so it never blocks or is blocked by a wave in flight. Running `D8` and `E12` early is free, and means D and E start against a settled interface instead of designing from scratch. |
| 9 (2026-08-03, `A2`) | Should `March` take the sim step, or keep its fixed `1/120 s`? | **Keep the fixed step.** Census of the 48 `BALLISTICS` entries: four carry a non-zero `ACCELERATION` (`wep_04`, `wep_25`, `wep_26`, `wep_27` — all 150 m/s²), **zero** carry a non-zero `GRAVITY`, and none of the four is a gun. A gun group resolves caliber + ammo → `wep_30..73` only, so the divergence disproven claim 4 predicted is **unreachable**: every marched round is a straight line, and the step size cannot move a straight line's endpoint. A fixed step also keeps the reticle from twitching with the frame rate. Guarded by two `[ExtractedDataFact]` tripwires in `CSVM.Tests/BallisticsTests.cs`, so the answer re-checks itself if the data or the loadouts change. |
| 10 (2026-08-03, `B3`) | `Resolve`'s sketched signature takes three arguments. Is that enough to pick a stand-in? | **No — it takes a fourth, `hasEffectsRuntime`.** The explosion stand-in's live condition is `!showedModel && !weapon.IsGun && EffectSink == null` (`Projectile.cs:1173`): a hardpoint weapon in a scene-less pool (the weapon lab) has nowhere to build its real fireball, so the burst stands in for it. That is a fact about the *caller*, exactly like `modelResolved`, and it cannot be derived from the weapon and the surface — with three arguments one of the three stand-ins the item is defined by is unreachable. `B4` passes `EffectSink != null`. |

## Decisions — Wave E's interface (2026-08-03, the `E12` `/grilling` session)

`E12`'s deliverable. Every row was put as a fork with a recommendation and answered by the author;
the code facts each rests on were traced during the session and are cited inline. This table is the
authority for `E13`–`E15b`, and it closes every question `E12` listed as one it "must not leave open".

| # | Question | Decision |
|---|---|---|
| 9 | Where does the seam cut — at `Puffer.Create`, or lower at the `MultiMesh`? | **Both, as two seams with different jobs.** `IEmitter` is what `EmitterDirector` holds (lifetime); `IEmitterRenderer` lives inside `Puffer` (particles → GPU) and is what makes `Puffer.cs`'s 847 untested lines reachable. Forced by `Puffer` being `sealed` — a fake cannot *be* a `Puffer`, so the director's collaborator has to be an interface regardless. |
| 10 | Two seams, so two fakes — or does one cover both? | **Two, with non-overlapping jobs.** `E15`'s fake is a plain record implementing `IEmitter`, with **no Godot type anywhere**; the renderer fake exists for a later `Puffer`-modes suite, not for `E15`. This is what makes `E15`'s own trap self-enforcing: if the suite is slow, something constructed a real emitter, and under this split nothing in its path *can*. |
| 11 | `IEmitter` is not a `Node3D`. Who parents the real emitter? | **The factory.** The real adapter is constructed with the `TextureArchive` **and** the parent node, and does `Create` + `AddChild` itself. `PufferParent` leaves `AnimRuntime`, and with it the "must be the world root, never the per-player crash root" rule currently kept alive by prose in `ForEffects` and `ForCrashRig`. |
| 12 | Do the three stops reconcile into one, or stay three? | **There are four, and they stay four (plus `Reset`).** They vary on two *independent* axes — selector (key / host-subtree / owner / all) × disposition (pause-revivable / forget / destroy) — proven independent by `FinishEffectInstance` and `TearDownResourcesOf` sharing a selector and differing in disposition. One named method per **selector**; disposition implied by the name. Rationale: every shipped bug in this family (`BL-224`, `BL-233`, `BL-235`, `BL-236`, `BL-242`) was a *selector* error, never a disposition error, so the selectors are the distinctions worth naming. |
| 13 | What is `Census` a census *of*? | **Structured rows over the *known* entries**, not the active ones: `(Name, Host, Def, Emitting, LiveParticles)`. Active-only cannot distinguish paused-but-revivable from forgotten — which is exactly the `EndFor`/`Discard` distinction row 12 just named, so an active-only census could not guard its own module. `--debug-anim`'s block at `:3308` becomes a projection of `Census` rather than a parallel re-derivation. |
| 14 | Does `EmitterDirector` outlive its `AnimRuntime` — one shared, or one each? | **One per runtime; one shared real factory.** All three factory closures are already byte-identical (`st => Puffer.Create(st, textures, sustained: true)` — `WorldSession.cs:209`, `WorldEffectsFactory.cs:244`, `:363`), so the *factory* consolidates while the *directors* do not. A shared director is disqualified by `Clear()`: a crash respawn must destroy exactly the crash rig's emitters, which in a shared map becomes a filtered delete over a discriminator — the "did I select the right set?" error this family has already shipped five times, re-created at session scope. `DefScopedPufferKeys` becomes a director constructor argument. |
| 15 | How is "the factory is gone after the build" expressed once it is an interface? | **A null-object `SpentEmitterFactory`, installed via `RetireFactory()`.** Behaviour-identical (returns null, counts, warns once), but it removes a null-branch from the hottest dispatch method and gives `BL-234`'s failure mode a *name* you can ask for. ⚠ The warn stays a warn: a null object that silently swallows **is** `BL-234`. |
| 16 | Is the key's variability a bool, or a supplied key-scope selector? | **A bool, and the effect-template pool stays out of `EmitterDirector`** — the constraint `E12` was required to reach explicitly. A supplied selector is the "third keying scheme" `docs/architecture.md` forbids, wearing a nicer suit: it would make the key unstatable in code. The rule has exactly two cases and a measurement behind each (def-scoped **required** on the effects runtime — the two `black_smoke` sputters masked each other; **forbidden** on the world runtime — C5's six `m_crane_go(#N)` twins stacked six `man_spark` emitters and moved the `c5-city-night` golden). `SlotOf` / `NextPooledAnchors` / `PlaceTemplateAt` stay in `AnimRuntime`; the director is handed already-resolved host and anchor nodes, which is also what lets a suite call it with two arbitrary nodes and no pool at all. |
| 17 | `E13` → `E14`, or `E14` → `E13`? (The plan contradicted itself — `E14`'s Verify said "before `E13` makes it permanent".) | **Seam-first, renderer last: `E13` → `E14` → `E15` → `E15b`.** `E13` defines the interfaces and lands the director wired to the *real* adapter from its first commit, so the emitter construction path is edited once rather than twice — worth more than a smaller first diff on a plan whose only evidence is 13/13 hash-identical. `E15b` (the renderer seam) goes **after** `E15` because it is the sole item near the particle spawn path, where `_rng` ordering and all four emitter-bearing goldens live; landing it after the census suite exists turns a hash diff into a named failing assertion. |
| 18 | How does a suite install the counting fake into a session `WorldSession.Build` constructs? | **`WorldSession.Options.EmitterFactory`**, defaulting to the real adapter. A post-build swap is disqualified: the bootstrap is where most `PUFFER_STATE`s fire (`:258` — C1's waterfall mist, the train's steam, two truck dust plumes), so a late install leaves `E15` with no pre-kill baseline, and `E15`'s assertion *is* "the census returns to its pre-kill set". ⚠ Accepted smell: production code carries a seam whose only non-default caller is a harness. Judged acceptable because the session already picks between two factory behaviours by flag today; this makes that choice a value instead of a branch. |

**Consequence for Decision 3 and for Wave F.** `E15` **no longer needs `TexturesOutliveBuild = true` at all** — with the fake being a plain record it needs neither a `TextureArchive` nor a `MultiMesh`. Decision 3's objection to `BL-241`'s original fix shape now applies to `E15` just as fully. Wave E's edit to `TestHarness.cs` is therefore **one argument in an options initialiser**, no archive-lifetime change and no `using` restructure, so `F16` has nothing to revisit. `F16` still sets the flag for the world-build path that genuinely wants real emitters.

**What Wave E now touches beyond `AnimRuntime.cs`** (wider than the plan assumed, from Decision 11): `WorldSession.cs` (`:209`, `:251`, `Options`), `WorldEffectsFactory.cs` (`:244`, `:363`), `TestHarness.cs` (one option), `Puffer.cs` (`E15b` only). It deliberately does **not** touch `GameSession.cs:1222`'s `EffectSink`/`ExternalEffect` wiring — that is `F17`/`BL-232`'s line, and E must not cross it.

## Decisions — Wave D's interface (2026-08-03, the `D8` `/grilling` session)

`D8`'s deliverable. Numbered **19–27** because the general table above already restarted at 9 for
`A2`/`B3` and the Wave E table runs 9–18; continuing past 18 avoids a third collision. Every row was
put as a fork with a recommendation and answered by the author, with the losing option recorded.
This table is the authority for `D9` and `D11`, and it closes every question `D8` listed as one it
"must not leave open". ⚠ Line citations below are against `AnimRuntime.cs` at `530bf36`, which has
drifted ~21 lines from the numbers the original items quote (`AddMotion` is `:3355`, not `:3334`).

| # | Question | Decision |
|---|---|---|
| 19 | Does `MotionSet.Tick` dispatch the `BOUNCE_SEQUENCE`, or return the landings? | **Return them.** The landing branch needs four `AnimRuntime` members (`InstanceOf`, `CallSequence`, `Count`, `DebugMotions`), so owning the dispatch means a back-reference to the runtime — precisely the coupling `ISequenceHost` exists to avoid. The counter-argument (keep remove-then-dispatch inside the owning module) **fails on inspection**: `AnimInstance.CallSequence` only does `Runners.Add(new SequenceRunner(seq))` (`SequenceRunner.cs:78–85`) and that constructor only calls `SetDue()` (`:159–165`), so no event dispatches synchronously and **no motion can be added or evicted inside `TickMotions` at HEAD**. The `:3374–3376` hazard is unreachable and `D9`'s trap overstates it. Returning makes the rule structural: `Tick` cannot return a landing it has not already removed. Losing option: an `OnLanded` callback mirroring `OnEventDispatched` — no back-reference either, but the ordering stays a choice the module makes rather than one its signature forces. |
| 20 | The sketch names four members (`Add`/`Tick`/`OwesWork`/`LaunchCount`). Is that the surface? | **Nine, and `MotionSet` owns all of them.** The five the sketch omits: `DiscardFor` (`TearDownResourcesOf:1838`), `Reset` (`ResetToBaseState:837`), `HasSpinOn` (`Dispatch:2080`), `Count` (`:777`, `:818`, `:1538`, `:3325`) and `Live` (`LogMotions:3331`). `HasSpinOn` is the one that matters: it is the **other half of the registration rule**, and both bugs this family has shipped are registration bugs — the hangar-door "later registration wins" (`:3350–3354`) and the `Loop{-1}` spin rebuilt every frame, which "would sit almost still while looking, in the logs, perfectly driven" (`:2075–2079`). They live 1,275 lines apart today. It cannot fold into `Add`: the guard must run *before* `new SpinMotion(…)`, whose constructor writes `Target.Transform` (`SpinMotion.cs:29`). **No census row type** — unlike `E13`'s `EmitterCensusRow`, `IAnimMotion` already carries `Target`/`Owner`/`Channel`/`Finished`, which is exactly what a row would copy. |
| 21 | Is `OwesWork` bounce-specific or general? | **Bounce-specific, and renamed `OwesBounce`**, keeping `is MotionRuntime { PendingBounce: not null }` verbatim and moving `Retirable`'s 14-line ⚠ block (`:3035–3048`) onto it. Widening then costs deleting a type test in the file that carries the warning. Losing option: `bool OwesWork => false` as a default interface member on `IAnimMotion`, narrow by opt-in — it reads better but invites a future `SpinMotion` override of `!Finished`, which is `PLAN-bounce-launch`'s disproven claim 2 re-created in a file the ⚠ does not live in. No general case is coming: `BL-245` extends this to the 379 falls and the 204 authored-`RUN_TIME` bounces, all still bounces. `Retirable` **stays on `AnimRuntime`** — it is an instance-retirement question that consults motions, not a motion question. |
| 22 | Does `MotionSet` key on `Node3D`, or on an identity token? | **`Node3D` stays, and `D11` remains an engine suite.** `MotionSet` dereferences no node in any of the nine operations — identity comparison only — so it is **already engine-free in behaviour**, and that is the deepening. Godot *structs* work in the unit tier (`BallisticsTests` builds `Vector3` freely), so `Node3D` identity is the sole blocker — and it blocks exactly the three identity-keyed rules worth testing: an off-engine fake can only return `null!`, so every `Add` evicts every other. The tier also needs a visibility widening (`IAnimMotion`/`MotionRuntime` are `internal`, there is no `InternalsVisibleTo` anywhere in the repo) against a standing ⚠ in `docs/architecture.md`. Not worth it for a suite that already exists. |
| 23 | Which of `D9`'s three invariants stay module-enforced? | **All three, one of them for free — and a new caller obligation takes its place.** Owner-stamping and `(Target, Channel)` eviction live in `Add`; remove-before-dispatch stops being an invariant at all under Decision 19. ⚠ **New, and sharp:** the landings must dispatch *between* `Tick` and the instance walk. Split them and the instance is `Finished` with `OwesBounce` false → retired → `FinishEffectInstance` (`:943`) `SustainEnd`s the piece's trail emitter, which is `BL-236`'s machinery and moves `c1-destroy-effects`. Enforced by keeping a private `AnimRuntime.TickMotions(dt)` that does both, so `Advance` still has one statement at `:926`. |
| 24 | Who increments the launch counter? | **`Add` derives it** — `if (motion is MotionRuntime) LaunchCount++`. Provably equivalent today: `:2033` is the only site that registers one and always increments immediately after. ⚠ `Reset()` clears the list but must **not** zero `LaunchCount` — every reader takes a delta. `BallisticMotionsLaunched` stays a **public forwarding property**, the same call `E13` makes for `PuffersBuilt`: it has four external readers (`Probes.cs:593`/`:660`, `Suites.cs:784`/`:804`, and `WorldDamageLab.cs` in six places including the F5 lab's on-screen status). |
| 25 | What does `D11` port? | **Nothing — it re-aims.** `D11`'s premise is false at HEAD: `BounceLaunch` reads only public API (`BallisticMotionsLaunched`, `OnEventDispatched`, `UnhandledEventCounts`, `Destructibles`, `DamageAt`, `Advance`, `ResetDestructible`), no internals. With the forwarding property it compiles unchanged, and "landing assertions read `Tick`'s result" is **unimplementable** — `Tick` is called from inside `Advance`, which a suite drives from outside. `D11` instead asserts `OwesBounce` across the flight: the retirement hold's real mechanism, which `A5` recorded the existing zero-miss checks as unable to catch. |
| 26 | Where does the module's documentation live, and what is it called? | **Its own `## src/Mech3/Anim/MotionSet.cs` entry**, plus an index line. The `src/Mech3/Anim/` entry is at PROJECT_CONTEXT's 3-⚠ cap and describes itself as "not an independently-owned subsystem", which `MotionSet` would be. The new entry takes the two ⚠ from rows 22 and 23 plus the re-pointed `MotionChannel` one, dropping `Anim/` back to 2. **The name stays `MotionSet`**: `EmitterDirector` builds its emitters through a factory and owns their whole life, while `MotionSet` never constructs a motion — `AnimRuntime` builds them and hands them over. |
| 27 | Three items, or fewer? | **Two.** `D10`'s five holders resolve to two that legitimately stay (`MotionRuntime.PendingBounce`; `Dispatch`'s `bounceArmed`, a per-*event* fact feeding `Count("…deferred)")`, not a per-instance one), two that move in `D9` (the landing sweep, `Retirable`/`HasPendingBounceFor`), and one doc-comment. `D9` absorbs it and inherits its Verify battery — the `BL-236` census, `A5`'s `--destroy=m_build` probe, 13 goldens — running it **once** instead of twice for a rename. `D11` stays its own commit: it adds an assertion that must be watched failing first (Decision 6), and an assertion cannot move a golden. |

**Two things this session found and deliberately did not file.** `TickMotions`' reverse walk calls
out to `CallSequence` mid-iteration; if that ever gained a synchronous dispatch it could evict an
element below `i` and skip a motion for a frame. Unreachable at HEAD (row 19) and structurally
impossible after `D9`, so a `backlog.md` entry would describe a bug that cannot fire and is about to
be deleted. And the ~21-line citation drift is noted at the head of this table rather than corrected
across the original items, which are being rewritten anyway.

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Converting the nine plain classes' `GD.Print` calls to `Log.Info` makes them unit-testable." | `Log`'s own console sink calls `GD.Print` directly (`Log.cs:185`, `:219–227`), so the conversion moves the native call one frame down the stack and buys nothing. The seam has to go **inside** `Log`. This is why `C6` precedes `C7` and not the reverse. |
| 2 | "`BL-241` is fixed by setting `TexturesOutliveBuild` in `TestHarness`." | That makes the harness build real emitters; it does not give a suite anything to read. `BL-241`'s own fix note concedes the rest: `_activePuffers` is private and the census is a `GD.Print`. Without `E13`'s census on the interface, the flag alone leaves the assertion unwritable. |
| 3 | "`BL-232` is a one-line swap of `BuildWorldEffectsRuntime` for `EnsureWorldEffects`." | `BL-232`'s own trap: `GameSession.cs:1222` wires the projectile pool's `EffectSink` **and** the world runtime's `ExternalEffect` in one place, and `EnsureWorldEffects` wires only the latter, only if unset. The two are not interchangeable without moving the sink wiring. |
| 4 | "The reticle and the rounds already agree, the duplication is cosmetic." | They integrate at different step sizes: `ProjectilePool.SimStep` uses the caller's sim `dt`, `FlightController.BallisticImpactPoint` hard-codes `1f/120f` (`:916`). For a straight-line player gun this is moot; for any weapon carrying `ACCELERATION`/`GRAVITY` it is not. `A2` settled it (Decision 9): no gun-reachable weapon carries either, so the fixed step stays. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1–A2, B3–B5, C6–C7, F16–F17 | The friction *and* the fix shape are both traced. Confirm the trace, then implement. |
| **Interface settled by a grilling session; implementation traced** | D9, D11, E13–E15b | Both upgraded from "direction sound" on 2026-08-03, when `E12` and `D8` landed. Neither module's shape is a judgement call now — Decisions 9–18 fix Wave E's seam, members, census, ownership and item order; Decisions 19–27 do the same for Wave D, each fork's losing option recorded. Implement against those tables; reopening a row is allowed, editing it silently is not. |
| **Leads only — no mechanism yet** | G18–G19 | Budget for investigation. These may end in a disproof, and a recorded "no" is the deliverable if so. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. Wave C is the only wave that may run in a
parallel worktree alongside another (see dependency notes).

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
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

### Wave A — one ballistic model

1. ☑ Extract `Ballistics` and route both callers through it
2. ☑ Settle the integration step, and unit-test the model

### Wave B — deciding an impact, apart from performing it

3. ☑ `ImpactOutcome` + a pure `Resolve`
4. ☑ Route `Impact` through `Resolve` → `Apply`
5. ☑ Unit suite: 48 weapons × the reachable surfaces

### Wave C — a sink seam under the log

6. ☑ Give `Log` a settable console sink
7. ☑ Convert one family and assert on it off-engine

### Wave D — a motion that owes a bounce

8. ☑ **Grill the `MotionSet` interface** — no code *(landed 2026-08-03; Decisions 19–27)*
9. ☑ Extract `MotionSet`, folding the pending-bounce rules in
10. ❌ Folded into `D9` (Decision 27) — `OwesBounce` is one of `D9`'s nine operations
11. ☑ Add `OwesBounce` assertions to the `bounce-launch` suite

### Wave E — emitter lifetime behind a seam

12. ☑ **Grill the `EmitterDirector` interface and the seam** — no code *(landed 2026-08-03; Decisions 9–18)*
13. ☑ `IEmitter`/`IEmitterFactory` + extract `EmitterDirector` with all **four** stop paths
14. ☑ The counting fake, and proof it is reachable
15. ☑ The suite `BL-241` says cannot exist
15b. ☑ `IEmitterRenderer` inside `Puffer` — **last** (numbered `15b`, not `16`, so F and G keep their IDs)

### Wave F — invariants the caller no longer remembers

16. ☑ `SessionArchives.OpenFor(intent)`
17. ☑ Seal the raw effects builder (`BL-232`)

### Wave G — three runtimes, three interfaces

18. ❌ Design it twice, then decide — no code *(landed 2026-08-03: the answer is **no**; both
    independent designs declined the split — see the `⚠` block in `docs/architecture.md`'s
    `src/Mech3/AnimRuntime.cs` entry and the dated `docs/HISTORY.md` entry)*
19. ❌ Closed with `G18` — the `❌` path needs no separate session; the `⚠` line is the deliverable

## Dependency and parallelism notes

**One hard gate left.** **G18** cannot start before both D and E land; its whole question is what
`AnimRuntime`'s interface looks like once two modules have left it. The plan's other gate —
`PLAN-bounce-launch` `A4`–`A6` before `D9` — **was satisfied on 2026-08-03** when that plan
completed. Nothing else here waits on anything outside this file.

**The grilling items are outside every gate.** `D8` and `E12` land no code, so they contend on
nothing and wait for nothing (Decision 8). Both ran on 2026-08-03 while other waves were in flight,
which is exactly the intended timing: `D9` and `E13` each start against a settled interface. They
were also the two items that **cannot be delegated**: `/grilling` interrogates the author, so these
are sessions you sit in, not work an agent runs in a worktree.

**Chains.** A1 → A2 (A2 is a decision about the module A1 creates). B3 → B4 → B5. C6 → C7
(disproven claim 1). D8 → D9 → D11 (`D10` folded into `D9`, Decision 27). E12 → E13 → E14 → E15 →
E15b (Decision 17 — and note it
overrides `E14`'s original Verify, which had the E13/E14 order backwards). F16 and F17 are
independent of each other. **They no longer wait on E**: Decision 18 means `E15` needs no
`TextureArchive` at all, so E stops changing what the harness asks of the archive and the
E-before-F preference weakens to "F17 must not be pre-empted by E touching
`GameSession.cs:1222`" — which Wave E is now explicitly forbidden to do. G18 → G19, and G19 may
never open.

**File contention — never run these in parallel worktrees.** A and B both own `Projectile.cs`
(A also `FlightController.cs`). D and E both own `AnimRuntime.cs`. **Wave E's footprint is wider
than first written** (Decision 11): it also owns `WorldSession.cs`, `WorldEffectsFactory.cs`, one
line of `TestHarness.cs`, and — at `E15b` only — `Puffer.cs`. F owns `GameSession.cs` +
`TestHarness.cs`; the `TestHarness.cs` overlap is now a single options argument rather than an
archive-lifetime change, so E and F contend far less than the plan assumed.

**The one safe parallel pair:** Wave C touches only `Utils/Log.cs` and one caller family, so it may
run in a worktree alongside Wave A or B. Give the C agent `CSVM/src/Utils/Log.cs` plus its chosen
family as its stated file ownership, and nothing else.

---

# Wave A — one ballistic model

(Plan content abbreviated for archival — refer to version control history for the full per-item detail.)

