# Deepening — seven modules, seven waves

**ACTIVE PLAN** (written 2026-08-03). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

⚠ **Started 2026-08-03 with `A1`.** When this was written,
[`PLAN-bounce-launch`](plans/PLAN-bounce-launch.md) was live and its uncommitted `A4` gated Waves D
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
| 7 | Does every wave get a `/grilling` session? | **No — only the two whose interface is open: `D8` and `E12`.** (`E12` ran on 2026-08-03; its output is the Decisions 9–18 table below. `D8` is still open.) A, B, C and F are graded traced: the fix shape is dictated by the code, and grilling a settled shape is ceremony. `G18` is already a decision item by construction. |
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
| **Interface settled by a grilling session; implementation traced** | E13–E15b | Upgraded from "direction sound" on 2026-08-03 when `E12` landed. The module shape is no longer a judgement call — Decisions 9–18 fix the seam, the members, the census, the ownership and the item order, with each fork's losing option recorded. Implement against that table; reopening a row is allowed, editing it silently is not. |
| **Direction sound, module shape a judgement call** | D9–D11 | *That* the concept is spread is measured; *which* interface collects it is a design call — which is why the wave opens with a grilling session (`D8`) whose output is the interface. Do not start the extraction before its session has landed rows in the Decisions table. |
| **Leads only — no mechanism yet** | D8, G18–G19 | Budget for investigation. These may end in a disproof, and a recorded "no" is the deliverable if so. |

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

6. ☐ Give `Log` a settable console sink
7. ☐ Convert one family and assert on it off-engine

### Wave D — a motion that owes a bounce

8. ☐ **Grill the `MotionSet` interface** — no code
9. ☐ Extract `MotionSet` from `AnimRuntime`
10. ☐ Fold the pending-bounce rules into it
11. ☐ Port the `bounce-launch` suite onto the interface

### Wave E — emitter lifetime behind a seam

12. ☑ **Grill the `EmitterDirector` interface and the seam** — no code *(landed 2026-08-03; Decisions 9–18)*
13. ☐ `IEmitter`/`IEmitterFactory` + extract `EmitterDirector` with all **four** stop paths
14. ☐ The counting fake, and proof it is reachable
15. ☐ The suite `BL-241` says cannot exist
15b. ☐ `IEmitterRenderer` inside `Puffer` — **last** (numbered `15b`, not `16`, so F and G keep their IDs)

### Wave F — invariants the caller no longer remembers

16. ☐ `SessionArchives.OpenFor(intent)`
17. ☐ Seal the raw effects builder (`BL-232`)

### Wave G — three runtimes, three interfaces

18. ☐ Design it twice, then decide — no code
19. ☐ Execute the split, or record the "no"

## Dependency and parallelism notes

**One hard gate left.** **G18** cannot start before both D and E land; its whole question is what
`AnimRuntime`'s interface looks like once two modules have left it. The plan's other gate —
`PLAN-bounce-launch` `A4`–`A6` before `D9` — **was satisfied on 2026-08-03** when that plan
completed. Nothing else here waits on anything outside this file.

**The grilling items are outside every gate.** `D8` and `E12` land no code, so they contend on
nothing and wait for nothing (Decision 8). `E12` ran on 2026-08-03, while Wave A was in flight, and
that is exactly the intended timing: `E13` now starts against a settled interface. `D8` is still
open and should be run the same way. They are also the two items that **cannot be delegated**:
`/grilling` interrogates the author, so these are sessions you sit in, not work an agent runs in a
worktree.

**Chains.** A1 → A2 (A2 is a decision about the module A1 creates). B3 → B4 → B5. C6 → C7
(disproven claim 1). D8 → D9 → D10 → D11. E12 → E13 → E14 → E15 → E15b (Decision 17 — and note it
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

## A1 ☑ Extract `Ballistics` and route both callers through it

**Goal.** The `VELOCITY`/`ACCELERATION`/`GRAVITY` integration exists once. `ProjectilePool` and the
gun pipper call the same module, so a change to the model cannot move one without the other.

**Evidence (confidence: traced).** `ProjectilePool.SimStep` (`Projectile.cs:590`) integrates
`vel += dir*accel*dt; vel += Down*grav*dt; pos += vel*dt`. `FlightController.BallisticImpactPoint`
(`FlightController.cs:908`) re-implements the same three lines, and its own doc-comment
(`:899–902`) says so: "the SAME `VELOCITY`/`ACCELERATION`/`GRAVITY` integration `ProjectilePool`
steps each round with, so the reticle and the rounds agree". Nothing enforces that agreement.

**Approach.** New `CSVM/src/Flight/Ballistics.cs`, a static class with no Godot `Node` dependency:
`Step(ref Vector3 pos, ref Vector3 vel, float accel, float grav, float dt)` and
`March(WeaponDef, origin, forward, inheritVel, maxDistance)` for the reticle's capped walk. Both
callers keep their own loop shape — `SimStep` still owns the raycast and the fuse test, `March` still
owns the range cap and the 4096-iteration bound. Move **only** the integration. Do not touch
`ProximityFuseTriggered`, the near-miss pass, or the trail spacing.

**Model recommendation.** medium — mechanical extraction with an exact before/after, but it sits in
the flight hot path so the reviewer needs to see the arithmetic is unchanged.

**Verify.** `.\RunTests.ps1` full pass with 13/13 goldens hash-identical — `c1-flight` is the one
that would move if the rounds changed. Then a targeted `--viewer --weapon-lab` capture and a
`--fly --screenshot` at a pinned pose, both byte-identical to a baseline taken **before** the edit.

**⚠ Traps.** The two loops are not identical today (see A2) — extract the arithmetic, keep each
caller's current `dt` at this item so the change is provably inert. Changing the step *and* moving
the code in one commit makes the golden evidence unreadable.

## A2 ☑ Settle the integration step, and unit-test the model

**Goal.** A recorded answer to which step size is correct, and the first unit tests the ballistic
model has ever had.

**Evidence (confidence: traced; the correct step is a judgement call).** `BallisticImpactPoint`
hard-codes `const float dt = 1f/120f` (`FlightController.cs:916`) with the comment "guns are
straight-line so it is moot". `SimStep` uses the caller's sim `dt`. For a player gun — no
`ACCELERATION`, no `GRAVITY` — both produce the same straight line, which is why nobody has noticed.
For any weapon that carries either, they diverge, and 48 `WeaponDef`s exist.

**Approach.** Census `weapons.json` first: how many of the 48 carry a non-zero `ACCELERATION` or
`GRAVITY`, and of those, how many are reachable from a gun group (the reticle only ever draws for a
gun group). If the answer is zero, record that — the divergence is unreachable and the fixed step
stays, as a documented choice with the reason. If it is not zero, make `March` take the sim step.
Then `CSVM.Tests/BallisticsTests.cs`: a straight-line case, a gravity-drop case with a hand-computed
expectation, and a range-cap case that proves the march stops at `RANGE`.

**Model recommendation.** high — it carries the one epistemic choice in the wave, and "moot" is a
claim to verify, not to inherit.

**Verify.** The census output is the evidence, cited in the commit. Break the drop case deliberately
(halve the gravity) and confirm the new test fails; restore. `.\RunTests.ps1` green with goldens
identical — if a golden moves here, the step change was *not* inert and A2 has found a real
behaviour difference, which becomes a `backlog.md` entry rather than a rebaseline.

**⚠ Traps.** "Guns are straight-line so it is moot" is exactly the kind of comment that is true when
written and false later. Verify it against the current 48, not against the comment. Assert
hand-computed values, not values captured from the implementation — a test that records what the
code does cannot catch the code being wrong.

# Wave B — deciding an impact, apart from performing it

## B3 ☑ `ImpactOutcome` + a pure `Resolve`

**Goal.** "What should happen when this weapon hits this surface" is a value that can be computed,
returned and asserted, with no `Node3D` and no physics space.

**Evidence (confidence: traced).** `ProjectilePool.Impact` (`Projectile.cs:1141`) fuses one decision
to seven side effects: `ClassifySurface` (`:377`), the `WeaponDef.Impact` per-surface table,
`SpawnImpactModel` (`:1085`), `EffectSink`, three hand-authored stand-ins (`SpawnExplosion` /
`SpawnDirtDebris` / `SpawnRicochet`, `:1306–1387`), `ApplyDamage` (`:1251`) and `PlaySound`
(`:1678`). The review found **no automated coverage of this path anywhere** — it is verified by the
first eight breadcrumb prints (`:1161–1167`) and by eyeballing `--weapon-test`.

**Approach.** A `readonly record struct ImpactOutcome { string? EffectName; string? Sound; StandIn
StandIn; float Damage; float BlastRadius; }` and a pure
`ImpactOutcome Resolve(WeaponDef weapon, SurfaceClass surface, bool modelResolved)` alongside it —
new file `CSVM/src/Flight/ImpactOutcome.cs`. `Resolve` is a decision only: it reads the weapon's
table and the surface and picks, including which of the three stand-ins applies. It must not touch
`GameZ`, the sinks, or the sound archive. Land this item with `Impact` still doing its own thing —
`Resolve` is written and tested but not yet wired.

**Model recommendation.** high — the record's field set is the interface, and getting it wrong makes
B4 a rewrite rather than a routing change.

**Verify.** `.\RunTests.ps1` green; nothing is wired yet, so goldens are trivially identical and that
is the point — this item is provably inert. The real verification is B5's suite.

**⚠ Traps.** `SurfaceClass.Player` / `Enemy` are unreachable in M3 — `Projectile.cs`'s own ⚠ records
that the flying aircraft carries no physics body, so a round can never hit a plane. Model them in
the enum (they exist) but do not invent behaviour for them; a `Resolve` case for an unreachable
surface is content invented rather than decoded.

## B4 ☑ Route `Impact` through `Resolve` → `Apply`

**Goal.** `Impact` decides nothing. It calls `Resolve`, then obeys the result.

**Evidence (confidence: traced).** Same trace as B3. The ordering inside `Impact` is load-bearing:
`SpawnImpactModel` is tried first and, when the effect name resolves to a real gamez node, nothing
else runs — so `modelResolved` has to be an *input* to `Resolve`, not something `Resolve` discovers.

**Approach.** Split `Impact` into the classify + `Resolve` call, then `Apply(outcome, point,
collider, normal)` carrying the seven side effects unchanged. Keep the breadcrumb prints — they are
still the only thing a human reads during a playtest — but move them to log the *outcome record*,
which makes the probe line and the unit assertion say the same thing.

**Model recommendation.** high — this is the item with real blast radius in the wave; every weapon
in the game goes through it.

**Verify.** A baseline first: `--viewer --weapon-test` and the `c1-destroy-effects` golden captured
before the edit. After: 13/13 goldens hash-identical, `.\RunTests.ps1` green, and the
`--weapon-test` breadcrumb lines identical modulo the reformat. Then the 8-chapter `--freecam`
sweep, zero errors.

**⚠ Traps.** The three stand-ins fire only when neither the gamez model nor the `EffectSink`
produced anything — preserve that precedence exactly or a weapon quietly gains a second effect.
`ApplyDamage` runs its own `IntersectShape` blast query independent of the primary raycast; that
query stays in `Apply`, not in `Resolve`.

## B5 ☑ Unit suite: 48 weapons × the reachable surfaces

**Goal.** The first automated coverage this dispatch has ever had, at the seam B3 created.

**Evidence (confidence: traced).** `CSVM.Tests` already reads real extracted data through
`[ExtractedDataFact]` (`CSVM.Tests/TestData.cs:19-21`), so a suite over the shipped 48 `WeaponDef`s
skips cleanly on a checkout with no `extracted/` rather than failing.

**Approach.** `CSVM.Tests/ImpactOutcomeTests.cs`: for every one of the 48 weapons × the reachable
surfaces, assert `Resolve` returns a coherent outcome — an effect name or a stand-in but never
neither, a sound key that exists in `SoundDefs`, and blast damage only where `HasBlastDamage` says
so. Add the hand-picked cases the review named: a gun on `Buildings` picks the ricochet, a round on
`Default` picks dirt debris, a rocket on `Water` resolves the gamez splash model rather than a
stand-in.

**Model recommendation.** medium — a suite against behaviour B3/B4 have already settled.

**Verify.** Break `Resolve`'s surface lookup deliberately (return `Default` unconditionally) and
confirm the suite fails on more than one case; restore. Then `.\RunTests.ps1` full pass.

**⚠ Traps.** Assert the *rule*, not a snapshot of all 48 outcomes — a golden table of 48 rows breaks
on every future weapon-polish item and teaches the next session to rebaseline it without reading.

# Wave C — a sink seam under the log

## C6 ☐ Give `Log` a settable console sink

**Goal.** A test host can install its own console sink, so a module that logs is no longer forced
onto the engine.

**Evidence (confidence: traced).** `Log.Configure`'s doc-comment already claims "Pure — it touches
no Godot API, so it is callable from a test host" (`Log.cs:71-73`), but the write path is not:
`GD.Print` at `Log.cs:185` and `GD.PrintErr`/`GD.Print` at `:219–227`. `CSVM.Tests.csproj` has no
engine, so any call reaching those lines cannot run there. Nine plain non-`Node` classes call
`GD.Print` today (`Flight/DamageVisuals`, `HudFont`, `PylonOrdnance`, `StuntMission`, `StuntRace`,
`Session/FlightRigAssembler`, `LiveryResolver`, `WeatherRig`, `WorldEffectsFactory`) and have no
other engine dependency.

**Approach.** One `public static Action<string>? ConsoleSink` on `Log`, defaulting to null and
meaning `GD.Print`. Route `:185` and `:219–227` through it. The file sink is untouched — it is
already pure and already takes everything, and the ⚠ constraint that it takes *everything* must
survive: installing a test sink changes the console only.

**Model recommendation.** medium — small and mechanical, but it is a session-wide service and
`docs/verification.md` treats log determinism as load-bearing.

**Verify.** A `--det` run's `.scratch/logs/` file must be byte-identical to a pre-change run of the
same command — that is the sharpest available proof the sink change is inert. Plus `.\RunTests.ps1`
green.

**⚠ Traps.** Disproven claim 1: converting callers without this item buys nothing. Do not reorder
the console/file writes — the file sink must still receive a line the console filter drops.

## C7 ☐ Convert one family and assert on it off-engine

**Goal.** One family of modules moves to the unit-test tier, proving the seam pays.

**Evidence (confidence: traced).** `StuntRace` is a plain `sealed class` (`StuntRace.cs:53`) whose
whole state is a `List<Racer>`; its only engine dependency is `GD.Print` in `OnFinished`
(`:137`). `StuntScoreboard` and `StuntRaceBoard` are its siblings. Placings and standings ordering
— the thing a splitscreen race is *for* — have never been asserted.

**Approach.** Convert the stunt-race family's `GD.Print` calls to `Log.Info("flight", …)`, then
`CSVM.Tests/StuntRaceTests.cs` over finish ordering, the rematch reset, and the standings sort with
a tie. Install a collecting `ConsoleSink` in the test and assert the finish line is emitted once per
racer — which is the assertion that proves the seam, not just the class.

**Model recommendation.** medium — mechanical, one family, no judgement calls left after C6.

**Verify.** Break the standings comparator deliberately and confirm the new tests fail; restore.
`.\RunTests.ps1` green. A `--stunt --players=2` run's log lines are unchanged in text.

**⚠ Traps.** ⚠ `src/Utils/Log.cs` forbids a bulk sweep of the remaining `GD.Print` sites — SHELL-3,
a bulk text rewrite corrupted files here before. **One family, edited by hand.** The other eight
classes convert when a later item touches them, not here.

# Wave D — a motion that owes a bounce

## D8 ☐ Grill the `MotionSet` interface — no code

**Goal.** The interface `D9` extracts to is decided and written down before any code moves, as rows
appended to this plan's Decisions table with their own date.

**Evidence (confidence: lead only — this item exists because the shape is undecided).** The review
traced *that* the bounce concept is spread across five holders; it did not settle *which* interface
collects them. The four-member sketch in `D9`'s approach (`Add` / `Tick` / `OwesWork` /
`LaunchCount`) is a proposal, not a decision — in particular `Tick` returning its landings is one of
at least two workable shapes, the other being a callback the way `ISequenceHost.OnEventDispatched`
already works.

**Approach.** Run `/grilling` on the sketch. The questions it must not leave open:

- Does `MotionSet` own the `CallSequence` dispatch on landing, or return landings for `AnimRuntime`
  to dispatch? The second keeps `MotionSet` engine-free; the first keeps the ordering rule
  (remove-then-dispatch) inside the module that owns it. Both cannot be true.
- Is `OwesWork` bounce-specific or general? A general predicate reads better and is the exact shape
  that re-opens `BL-236` if it is ever widened to "any live motion".
- What does `MotionSet` need a `Node3D` for, and can that be an identity token instead? This decides
  whether `D11` is an engine suite or a unit test.
- Which of the three invariants stay enforced by the module and which stay caller obligations?

Append the answers as a dated Decisions block. If the session concludes the extraction is not worth
it, land this item as `❌` and close Wave D — that is a success, not a failure.

**Model recommendation.** high — interactive, and it is the item that decides whether `D9`–`D11` are
one session or three.

**Verify.** Not code. The deliverable is the Decisions rows. The test that they are real: `D9`'s
approach paragraph can be rewritten to name exact signatures without any remaining "or".

**⚠ Traps.** Grill against `HEAD`, not against a memory of the pre-`A4` file: `A4` (`840bc98`)
added `Retirable`/`HasPendingBounceFor` — two of the five holders this wave collects — and `A5`
(`9fecbfa`) added the `bounce-launch` engine suite, which `D11` has to port rather than write.

## D9 ☐ Extract `MotionSet` from `AnimRuntime`

**Goal.** The live motion collection and its rules live in one module; `AnimRuntime` keeps the
dispatch that feeds it.

**Evidence (confidence: direction sound; the trace is exact, the interface is a design call).**
`_motions` (`AnimRuntime.cs:447`), `AddMotion` (`:3334`) and `TickMotions` (`:3345`) carry three
invariants a caller must currently know by reading them: `Owner` is stamped on add, one motion per
`(Target, Channel)` evicts the previous, and the finished body is removed *before* its bounce
dispatches because that dispatch may add motions of its own (`:3358-3360`).

**Approach.** New `CSVM/src/Mech3/Anim/MotionSet.cs` owning the list and those three rules:
`Add(IAnimMotion, AnimDefinition, Node3D?)`, `Tick(float dt)` returning the landings it swept,
`OwesWork(def, anchor)`, `LaunchCount`. `AnimRuntime` holds one and forwards. **Do not move**
`RestOf`, `_rng`, `SetSubtreeOpacity` or `NonSingularScale` — `docs/architecture.md`'s
`src/Mech3/Anim/` entry records them as `internal` on `AnimRuntime` precisely so the motion types can
reach them, and that stays true.

**Model recommendation.** high — highest-churn file in the repo, and the sweep is per-frame.

**Verify.** Baseline first: `--freecam --chapter=C1 --destroy=refuel --debug-anim` with its
`N live motion(s), M ballistic launch(es)` line. After: the same counts, the same landing lines, the
same order. 13/13 goldens hash-identical, `.\RunTests.ps1` green, 8-chapter sweep clean.

**⚠ Traps.** The removal-before-dispatch order in `TickMotions` is not incidental — reversing it
re-ticks a landed body and can double-fire its `BOUNCE_SEQUENCE`. `LogMotions` prints at most 12
motions (`LOG-5`); the cumulative counter, not the list, is the headless answer to whether a launch
happened.

## D10 ☐ Fold the pending-bounce rules into it

**Goal.** "A motion owes a bounce" is asked of one module, and `AnimInstance.Finished` no longer
needs a doc-comment warning that it is not the retirement test.

**Evidence (confidence: direction sound).** After `PLAN-bounce-launch` `A4` the concept has five
holders: `MotionRuntime.PendingBounce` (`Anim/MotionRuntime.cs:104`, set `:216`), the `bounceArmed`
flag in `Dispatch` (`AnimRuntime.cs:2036`), the landing dispatch in `TickMotions` (`:3383`),
`Retirable` / `HasPendingBounceFor` (`:3049`, `:3052`), and the warning on `AnimInstance.Finished`
in the engine-free `SequenceRunner.cs:62`.

**Approach.** `Retirable` becomes `inst.Finished && !_motions.OwesWork(inst.Def, inst.Anchor)` —
one call, no `_motions` scan spelled out at the call site. Keep the arming in `MotionRuntime.Create`
where the drawn `v0` lives; `MotionSet` reads it, it does not re-derive it. Rewrite
`AnimInstance.Finished`'s doc-comment to point at `MotionSet.OwesWork` as the single answer.

**Model recommendation.** high — the predicate's width is the difference between fixing 150 events
and re-opening `BL-236` across 1,037 defs; `PLAN-bounce-launch`'s Decision 4 is the evidence.

**Verify.** The negative first, because it is the one that bites: the emitter census for the
`BL-236` regression set (`c3-island`, `c4-snow`, `c5-city-night`) must be unchanged, proving no
spin-bearing instance was pinned. Then `A5`'s recorded `--destroy=m_build` probe — **not**
`RunTests` — as the able-to-fail control for the hold itself. Then all 13 goldens.

**⚠ Traps.** **Do not widen `OwesWork` to "any live motion."** `SpinMotion.cs:36` never reports
`Finished` for the 2,181 unbounded spins, so a wider predicate makes them immortal — this is
`PLAN-bounce-launch`'s disproven claim 2 and it must not be re-derived here.

⚠ **A green `RunTests` does not prove the hold survived this item.** `A5`'s own commit records that
breaking `A4`'s retirement hold leaves the `bounce-launch` suite green: every C1 def with a solvable
launch also runs an unbounded `fire_n_smoke` loop that keeps its instance alive regardless, and the
yard still gave 28/28 with the hold removed. The only instrument that has been *seen* to catch it is
the `--destroy=m_build` probe, at 1 miss in 7. Use it, and do not read an unchanged 18/18 as
coverage.

## D11 ☐ Port the `bounce-launch` suite onto the interface

**Goal.** The family's existing guard reads `MotionSet`'s interface instead of `AnimRuntime`'s
internals, and survives the extraction unchanged in what it asserts.

**Evidence (confidence: traced).** The suite already exists — `PLAN-bounce-launch` `A5` landed it as
`bounce-launch`, the 18th registered suite (`9fecbfa`, +186 lines in `Suites.cs`). It kills one
`refuel*` tank through `DamageAt` and asserts on the `OnEventDispatched` timeline: 4 ballistic
launches, `sparkout3`/`sparkout4` each dispatching both their events, each flight inside the band
its authored `translation_range` allows, and a yard sweep over seven `m_build` buildings giving
28/28 dispatches. It needs no `PufferFactory`, so `BL-241` never blocked it.

**Approach.** Repoint its reads: `BallisticMotionsLaunched` becomes `MotionSet.LaunchCount`, and
the landing assertions read `Tick`'s result rather than the dispatch timeline where that is now the
more direct question. **Assert the same facts** — a port that changes what is asserted is a new
suite wearing an old name, and the old one's able-to-fail evidence no longer covers it.

**Model recommendation.** medium — mechanical once D9/D10 are in.

**Verify.** Break `D9`'s launch counting deliberately (drop one launch) and confirm the suite fails;
restore. `.\RunTests.ps1` full pass at 18/18 suites. **Do not use `OwesWork` as the thing you break**
— per D10's second trap the suite stays green when the hold goes, so that control proves nothing
here.

**⚠ Traps.** Assert a **band**, not an exact time — the launch is a random draw within
`translation_range`, and `A5` measured the same piece at 4.083 s alone and 3.883 s in the full sweep
because the shared `anim` stream had been drawn from a different number of times first. A pinned
seed does not pin the draw.

# Wave E — emitter lifetime behind a seam

## E12 ☑ Grill the `EmitterDirector` interface and the seam — no code

**Landed 2026-08-03.** The deliverable is the "Decisions — Wave E's interface" table above
(rows 9–18) and the restated `E13`–`E15b` below. All four questions this item said it must not
leave open are answered, including the effect-template-pool constraint its trap demanded be
reached explicitly (Decision 16). Three answers changed the wave's shape rather than merely
detailing it: the seam cuts in **two** places (9), there are **four** stop paths rather than three
(12), and the wave's file footprint is wider than written (11). One item was added (`E15b`) and the
`E13`/`E14` order in `E14`'s Verify was found self-contradictory and settled (17).

**Goal.** The interface and the seam's exact shape are decided before `E13` moves a line — this is
the plan's top recommendation and its largest extraction, so a wrong interface here is the most
expensive mistake available.

**Evidence (confidence: lead only — this item exists because the shape is undecided).** The spread
is measured: start in `HandlePufferState` (`AnimRuntime.cs:2243`), three stops at `:2243`, `:2399`
and `:1815`, follow at `:2421`, keying explained in a field comment at `:381`. What is *not* settled
is which of those the module owns versus forwards, and where exactly `IEmitterFactory` cuts.

**Approach.** Run `/grilling` on `E13`/`E14`'s sketch. The questions it must not leave open:

- **Where does the seam cut?** At `Puffer.Create` (the factory makes an emitter object), or lower,
  at the `MultiMesh` (the factory makes a renderer)? The first is a smaller change; the second is
  the one that makes `Puffer`'s own logic — burst, trail, sustain modes — unit-testable, and
  `Puffer.cs` is 847 lines that no test reaches today.
- **Do the three stops reconcile into one, or stay three?** They exist for different reasons
  (explicit stop, `OBJECT_ACTIVE_STATE`, instance end) and each was the site of a shipped bug.
  Collapsing them is the deepening; keeping them three may be what the data actually requires.
- **What is `Census` a census *of*?** `BL-241`'s trap says assert names, not counts, because several
  runtimes contribute — so does `Census` span runtimes, or is it per-`EmitterDirector` with the
  suite composing them?
- **Does `EmitterDirector` outlive its `AnimRuntime`?** Three runtimes exist per session (world,
  world-effects, per-player crash rig). One director each, or one shared?

Append the answers as a dated Decisions block.

**Model recommendation.** high — interactive, and it sets the interface for the plan's top pick.

**Verify.** Not code. The deliverable is the Decisions rows, and the test is that `E13` and `E14`
can each be restated with exact signatures and no remaining "or". If the seam question is still
open after the session, the wave is not ready.
**Met** — the signatures are in `E13` below, and every "or" in this item's question list resolved
to a recorded fork with its losing option stated.

**⚠ Traps.** ⚠ `docs/architecture.md` warns the effect-template pool is **not** a third keying
scheme — the session must reach that constraint explicitly and decide the pool stays out, rather
than discovering it mid-extraction. Read `src/Effects/Puffer.cs`'s entry too, not just
`AnimRuntime`'s: the seam's lower option lands inside it.

## E13 ☐ `IEmitter`/`IEmitterFactory` + extract `EmitterDirector` with all four stop paths

**Goal.** An emitter's whole life — start and all four stops — is one module with a readable
census, instead of five methods over two private collections.

**Evidence (confidence: traced; the spread is measured and the interface is settled by `E12`).**
Start is `HandlePufferState` (`AnimRuntime.cs:2243`, ~136 lines). **There are four stops, not
three** — the fourth, `TearDownResourcesOf` (`:1836`), is the only one that also *removes* the
`_puffers` entry rather than just `SustainEnd`-ing it, and it shares its selector with
`FinishEffectInstance` (`:1815`, `BL-236`); the other two are `HandlePufferState`'s own stop branch
(`:2264`, including `BL-242`'s owner fallback) and `EndSustainedOn` (`:2399`, the
`OBJECT_ACTIVE_STATE false` path, `BL-224`). `Clear()` (`:838`) is a fifth disposition again
(`SustainEnd` + `Clear` + `QueueFree`). Per-frame follow is `TickPuffers` (`:2421`), whose
`HostOffsetOf`/`_hostOffsets` (`:2442`, `:390`) has no other caller and moves with it — but
`VisualOriginOf` (`:1319`) is shared with the `ExternalEffect` path at `:2172` and stays. The keying
rule — `(Name, Node, Def?)` gated by `DefScopedPufferKeys` — is explained in a field comment at
`:368`, nowhere near the method that implements it. Four bugs in this family (`BL-233`, `BL-235`,
`BL-236`, `BL-242`) were each diagnosed by hand from a `--debug-anim` log.

**Approach.** Per Decisions 9–18. New files under `CSVM/src/Mech3/Anim/` — and the interfaces land
here, wired to the **real** adapter from the first commit, so the emitter construction path is
edited once rather than twice (Decision 17):

```csharp
public interface IEmitter
{
    void SustainAt(Vector3 worldPos, Basis worldBasis, float dt);
    void SustainEnd();
    void Clear();
    void Destroy();
    int  LiveCount { get; }
    bool IsValid   { get; }   // the IsInstanceValid guard EndSustainedOn/TickPuffers both make
}

public interface IEmitterFactory { IEmitter? Create(PufferState state); }
// real:  PufferEmitterFactory(TextureArchive textures, Node parent) — Create + AddChild (Decision 11)
// spent: SpentEmitterFactory(Action<string> count) — returns null, warns once (Decision 15)

public readonly record struct EmitterCensusRow(
    string Name, string Host, string Def, bool Emitting, int LiveParticles);

public sealed class EmitterDirector
{
    public EmitterDirector(IEmitterFactory factory, bool defScopedKeys, bool debug,
                           Action<string> count);

    public void Assert (string name, Node3D host, AnimDefinition def, Node3D? anchor, PufferState state);
    public void End    (string name, Node3D host, AnimDefinition def, Node3D? anchor); // + BL-242 owner fallback
    public void EndOn  (Node3D root);                        // OBJECT_ACTIVE_STATE false
    public void EndFor (AnimDefinition def, Node3D? anchor);  // pause, entry kept  (FinishEffectInstance)
    public void Discard(AnimDefinition def, Node3D? anchor);  // pause + forget     (TearDownResourcesOf)
    public void Reset  ();                                    // + Destroy          (Clear)
    public void Tick   (float dt);

    public IReadOnlyList<EmitterCensusRow> Census { get; }
    public int  Built { get; }        // PuffersBuilt forwards here, keeping verification.md WORLD-12 true
    public void RetireFactory();      // bootstrap end: swaps in SpentEmitterFactory
}
```

One director per runtime, one shared real factory (Decision 14). Move the keying rule's explanation
onto the module. `AnimRuntime` keeps the dispatch case, the `at_node` sentinel resolution and the
`active_state` read, and forwards; the **stub check** (`Textures.Count == 0 &&
TextureSequence.Count == 0`) moves to the director, being emitter knowledge. The `Action<string>
count` argument exists so every `Count("PufferState(...)")` string stays byte-identical in the
report lines.

**What leaves `AnimRuntime`:** `_puffers`, `_activePuffers`, `_hostOffsets`, `HostOffsetOf`,
`PufferFactory`, `PufferParent`, `_reportedPufferFactoryGone`, `DefScopedPufferKeys`, the four stop
methods, `TickPuffers`, `HandlePufferState`'s body below the `at_node` resolve, the `:368` keying
comment, and the `:3308` debug block (which becomes a projection of `Census`). `PuffersBuilt` stays
as a forwarding property.

**Model recommendation.** high — the biggest extraction in the plan, out of the highest-churn file.
If it is too large to verify as one unit, split **by file, not by seam**: `E13a` lands the
interfaces and adapters as new files with `AnimRuntime` untouched (trivially golden-identical, since
nothing calls them), `E13b` moves the director onto them.

**Verify.** Baseline the emitter census from a `--freecam --chapter=C1 --destroy=refuel --debug-anim`
run at 1 s / 5 s / 10 s **before** the edit; after, the same names at the same times. The `BL-236`
regression set (`c3-island`, `c4-snow`, `c5-city-night`) census unchanged. 13/13 goldens — four of
them frame live emitters (`c1-waterfall`, `c3-island`, `c1-destroy-effects`, `c1-crash`), which
makes them the sharpest instrument in this wave.

**⚠ Traps.** ⚠ `docs/architecture.md` warns the effect-template pool is **not** a third keying
scheme, and Decision 16 settles that the pool **stays out**: `EmitterDirector` absorbs the existing
two keys and leaves `SlotOf` / `NextPooledAnchors` / `PlaceTemplateAt` where they are, being handed
already-resolved host and anchor nodes. Assert **names**, not counts, in every census comparison —
`BL-241`'s own trap: several runtimes contribute to the count. ⚠ Do not "simplify" the four
selectors into one parameterised `End` — the two axes are provably independent (`FinishEffectInstance`
and `TearDownResourcesOf` share a selector, differ in disposition), and every shipped bug in this
family was a selector error, which a collapsed call site would re-enable (Decision 12).

## E14 ☐ The counting fake, and proof it is reachable

**Goal.** A suite can observe emitter lifetime with no GPU, no `TextureArchive` and no `Puffer` —
the third implementation that makes the `E13` seam real rather than hypothetical.

**Evidence (confidence: traced).** `Puffer.Create` builds a texture atlas and a `MultiMesh` — a
genuinely essential engine dependency, per `docs/architecture.md`'s `src/Effects/Puffer.cs` entry.
That is exactly why the dependency belongs behind a seam rather than being fought:
`WorldSession.cs:251` nulls `PufferFactory` when `TexturesOutliveBuild` is false, and the harness
takes that branch (`TestHarness.cs:582`). `Puffer` is `sealed` (`Puffer.cs:274`), so the fake cannot
*be* a `Puffer` — which is what forced `IEmitter` in Decision 9.

**Approach.** `CountingEmitterFactory` in `CSVM/src/Testing/`, returning a **plain record**
implementing `IEmitter` that records `(key, started, stopped)` and holds no Godot type at all
(Decision 10). Reached by suites through `WorldSession.Options.EmitterFactory`, which defaults to
the real adapter (Decision 18); `TestHarness` passes it, and that is Wave E's entire edit to that
file. ⚠ The fake must stay honest about `SustainEnd`-then-revive — the damage-stage sputter loop
cycles `ACTIVE_STATE` 0/1 and depends on the entry surviving — or `E15` asserts against a lie.

**Model recommendation.** medium — a small type against an interface `E13` has already settled.

**Verify.** 13/13 goldens hash-identical (nothing in a session's path changed). Then confirm the
fake is actually reachable: a scratch suite that installs it and reads one emitter start, before
`E15` depends on it.

**⚠ Traps.** Do not let the fake become the default anywhere outside a suite — a session that
silently draws no emitters is `BL-234` again, and `BL-234` was found by a human noticing missing
fire, not by a test. Enforce it structurally: the fake lives in `CSVM/src/Testing/` and is
**never referenced from `src/Session/` or `src/Mech3/`**. ⚠ And `SpentEmitterFactory`'s warn stays a
warn (Decision 15) — a null object that silently swallows *is* `BL-234`, however polite its type
name.

## E15 ☐ The suite `BL-241` says cannot exist

**Goal.** Killing a destructible in the harness asserts that its emitters start and then stop —
the guard four bugs in this family never had.

**Evidence (confidence: traced).** `BL-241`: "No engine suite can observe emitter lifetime — a test
world builds no puffers at all." Its fix note asks for a public read-only census on `AnimRuntime`
because "`_activePuffers` is private, and the debug line is a `GD.Print`" — E13 provides that census
and E14 removes the GPU requirement.

**Approach.** In `Suites.cs`, kill a `refuel*` tank through `DamageAt` with the counting fake
installed via `WorldSession.Options.EmitterFactory`, then assert the emitter census returns to its
pre-kill set. Close `BL-241` with `/close-backlog-item`.

⚠ **This item does *not* set `TexturesOutliveBuild = true`** — `BL-241`'s own fix shape, and now
unnecessary: the fake needs neither a `TextureArchive` nor a `MultiMesh`, so the harness's archive
lifetime stays entirely `F16`'s problem. The fake must be installed **through `Options`, not after
`Build` returns**: the bootstrap is where most `PUFFER_STATE`s fire (`AnimRuntime.cs:258` — C1's
waterfall mist, the train's steam, two truck dust plumes), so a late install leaves this suite with
no pre-kill baseline, and the pre-kill set is the whole assertion (Decision 18).

**Model recommendation.** medium — a suite against behaviour E13/E14 have settled.

**Verify.** Revert `BL-236`'s fix locally and confirm this suite fails — that is the strongest
possible proof, since `BL-236` is a bug this family actually shipped. Restore, then
`.\RunTests.ps1` full pass.

**⚠ Traps.** **Assert the names, not the count** (`BL-241`'s own trap) — though note the per-runtime
census (Decision 14) narrows that hazard: rows now say which director they came from, so a
cross-runtime miscount is no longer the default failure. Assert on `Emitting` **and** on the row's
presence: they are different facts (`EndFor` pauses and keeps, `Discard` forgets), and a suite that
reads only one cannot tell a broken disposition from a working one. And `BL-241` warns that baking
an atlas per authored state costs real time on world build — with the fake that cost is gone, so if
the suite is slow, something is still constructing real emitters.

## E15b ☐ `IEmitterRenderer` inside `Puffer` — last

**Goal.** `Puffer`'s own logic — burst, distance-trail and sustain modes — becomes reachable by a
test, which today it is not at any point in its 847 lines.

**Evidence (confidence: direction sound; the seam's exact cut is deliberately left to this item).**
`Puffer.Create` (`Puffer.cs:403`) does three separable things: `BuildAtlas` (returns null → `Create`
returns null, the "no texture" data-coverage outcome the director counts), `new Puffer()`, and
`Init` (`:676`), which sizes the particle pool, compiles the shader and builds the `MultiMesh`
(`:716`). `docs/architecture.md` records the atlas + `MultiMesh` as a genuinely essential engine
dependency — which is the argument for a seam, not against one.

**Approach.** Settle at the top of this item, not now: `IEmitterRenderer`'s shape depends on whether
the atlas is built **above** or **below** it. Above makes `Puffer`'s modes constructible without a
`TextureArchive` — the entire point of Decision 9's lower seam; below does not, and would leave this
item delivering nothing testable. Nothing in `E13`–`E15` depends on the answer, and `Puffer`'s
internals will read differently once they do.

**Model recommendation.** high — the one item in this wave that goes near the particle spawn path.

**Verify.** 13/13 goldens hash-identical, and this is the item where that is *hard*: `_rng` is a
field initializer (`Puffer.cs:354`), so the RNG stream is pinned to the order of `Puffer` **object**
constructions, and `docs/architecture.md` records that each emitter's seed depends on how many were
built before it. Leave `new Puffer()` exactly where it sits in `Create` and the stream is untouched;
move it, and all four emitter-bearing goldens (`c1-waterfall`, `c3-island`, `c1-destroy-effects`,
`c1-crash`) move with it. Then a new suite exercising at least one mode through the fake renderer,
seen failing first.

**⚠ Traps.** ⚠ **This item runs after `E15`, deliberately** (Decision 17): it is the only Wave E
change near the spawn path, and landing it once the census suite exists turns a regression into a
named failing assertion instead of a hash diff to bisect. ⚠ The honest risk of that ordering is that
`E15b` arrives when the wave reads as finished and a 847-line refactor is least appetising — if it
is going to be dropped, drop it *explicitly* into `backlog.md` with this section as its evidence,
rather than by attrition.

# Wave F — invariants the caller no longer remembers

## F16 ☐ `SessionArchives.OpenFor(intent)`

**Goal.** Opening the archives names an intent, and the lifetime flags come with it — no caller
sets them by hand.

**Evidence (confidence: traced).** `GameSession.LoadArchives` (`GameSession.cs:473-513`) and
`TestContext.BuildWorld` (`TestHarness.cs:570-586`) open the same five archives with near-identical
lines; the only difference is that `GameSession` sets `TexturesOutliveBuild = true` (`:628`) and the
harness does not (`BL-241`). Two adapters at one seam, one of which forgot the rule.

**Approach.** `SessionArchives.OpenFor(intent)` where `intent` is `Session | Lab | Suite`, returning
the archives plus the matching `WorldSession.Options` lifetime flags. Both callers use it. Keep
**both** flags: ⚠ `docs/architecture.md`'s `src/Mech3/WorldSession.cs` entry records "one flag per
archive because the two lifetimes differ" as a decision — `OpenFor` *chooses* them, it does not
collapse them.

**Model recommendation.** medium — mechanical, but it touches the session build's phase boundaries.

**Verify.** The `StartupProfile` `[perf] startup …` line must still split by the same phases — ⚠
that entry records the phase boundaries as a reported contract, and a dropped phase reads as a
growing `rest`, not as missing. Then `.\RunTests.ps1` and the 8-chapter sweep.

**⚠ Traps.** The sound archive is scoped to the build and the texture archive is not; that asymmetry
is deliberate and `OpenFor` must reproduce it per intent, not normalise it.

## F17 ☐ Seal the raw effects builder (`BL-232`)

**Goal.** There is one world-effects runtime because there is only one way to ask for one.

**Evidence (confidence: traced).** `WorldEffectsFactory.BuildWorldEffectsRuntime` is public
(`:209`) and `GameSession` calls it directly at `:685` and `:1222`, bypassing the cache in
`EnsureWorldEffects` (`:284`, which calls it at `:291`). `BL-232`: a `--fly --destroy=` session
therefore builds two runtimes, each carrying `EffectPoolSlots` × ~38 template subtrees.

**Approach.** Move the `EffectSink` wiring that `:1222` does out of the call site, route both raw
call sites through `EnsureWorldEffects`, then make `BuildWorldEffectsRuntime` private. Close
`BL-232` with `/close-backlog-item`.

**Model recommendation.** medium — small, but disproven claim 3 says it is not the one-liner it
looks like.

**Verify.** A `--plane=… --destroy=…` run must print `world-effects runtime: …` **once**, not twice
— that is `BL-232`'s own instrument. `c1-destroy-effects` hash-identical (it already is either way,
so it is a control, not the finding). Full `.\RunTests.ps1`.

**⚠ Traps.** Disproven claim 3, restated because it is the whole item: `:1222` wires the projectile
pool's `EffectSink` **and** the world runtime's `ExternalEffect`; `EnsureWorldEffects` wires only the
latter, and only if unset. Check the ordering against a live `--fly --destroy` run before assuming
the two are interchangeable.

# Wave G — three runtimes, three interfaces

## G18 ☐ Design it twice, then decide

**Goal.** A recorded decision on whether `AnimRuntime`'s three modes get three interfaces — with the
reasoning kept whichever way it goes.

**Evidence (confidence: lead only).** `AnimRuntime` is one class with 66 public declarations, ~30 of
them wiring knobs a caller sets before `Bind`, serving three modes selected by toggling bools and
delegates: the ambient world runtime, the world-effects closure (`ForEffects`, `:574`) and the
per-player crash rig (`ForCrashRig`, `:601`). The shape is documented as deliberate, so this is a
question, not a finding.

**Approach.** Run `/codebase-design`'s design-it-twice pattern: two independent interface proposals
for the post-D/post-E `AnimRuntime`, compared on depth, locality and seam placement. Count what is
actually left on the class once `MotionSet` and `EmitterDirector` have gone — the answer may be that
the union is no longer wide enough to be worth splitting, which closes this as `❌`.

**Model recommendation.** high — a judgement call with the largest blast radius on the page, and its
best outcome may be a well-argued "no".

**Verify.** Not code. The deliverable is the decision plus its record: a `⚠` line in
`docs/architecture.md`'s `src/Mech3/AnimRuntime.cs` entry if the answer is no, so no future review
re-suggests it; a written interface if the answer is yes.

**⚠ Traps.** Do not run this before D and E land — the whole question is what is *left*, and
measuring the current 66 answers a question nobody is asking by then.

## G19 ☐ Execute the split, or record the "no"

**Goal.** Either three narrow interfaces over one shared implementation, or a recorded decision that
closes the question.

**Evidence (confidence: lead only).** Depends entirely on G18.

**Approach.** If G18 says go: three interfaces, one implementation, each mode exposing only the
knobs its caller may set. `ForEffects` and `ForCrashRig` become the constructors of two of them. If
G18 says no: this item lands as `❌` and the `⚠` line from G18 is the whole deliverable.

**Model recommendation.** high if it executes; the `❌` path needs no separate session.

**Verify.** 13/13 goldens, `.\RunTests.ps1`, the 8-chapter sweep, and the `BL-236` emitter-census
regression set — a change of this width touches every mode at once.

**⚠ Traps.** A `❌` here is a success, not an abandoned item. Record it as one, with the reasoning,
and delete nothing from `docs/architecture.md` that explains why the toggles were deliberate.
