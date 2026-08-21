# FlightController deepening — six waves, six modules

**ACTIVE PLAN** (written 2026-08-21). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

`CSVM/src/Flight/FlightController.cs` is one Godot node holding human and AI input, the fixed-step
flight loop, weapons, targeting, the collision sweep and its response, crash and respawn, camera
policy and the whole pilot HUD. It carries 47 public declarations at class scope, of which 43 are
mutable fields, plus roughly 70 private mutable fields, and it has no test file in either tier. This
plan takes six concepts out of it behind six interfaces, each proved by an assertion in the tier the
extraction makes reachable. Three of the six follow one shape deliberately: the module owns its
rules, returns a value, and the node performs the effects.

**Two success measures, not one.** Testability and locality come first, and file size is a real
second goal rather than a side effect. Every wave therefore owes both a new assertion and a
reduction in `FlightController`'s public field count. A wave that lands neither has not finished,
whatever the goldens say.

Out of scope, and each for a reason stated in the Decisions table: the camera mode choice, the
`Downed`/`InertChanged`/`DamageApplied` events, `Pilot` itself, and the five fields `GameSession`
assigns after the build (`Turrets`, `Match`, `Race`, `RestartRace`, `RestartMatch`). Those last
belong to the session-side candidate, not here. **No behaviour change is intended anywhere in this
plan.** A wave that needs a golden repinned has changed behaviour, which means the wave is wrong or
an unrelated bug rode in with it. Stop and split.

## Milestone goal

- The world's physics queries live behind one `IWorldQuery` with two methods, and `TurretController`
  no longer holds a whole `FlightController` to cast one ray.
- Which stick an aircraft flies is a value resolved once at bind, not a ternary re-evaluated every
  physics frame.
- The pilot HUD is a module fed a per-frame struct, and no other module writes its text.
- The decoded collision response (slide, restitution, lever-arm kick) is on `FlightModel`, where
  `BounceNormalSpeed` already is, and nothing writes the model's four public fields from outside.
- A contact's outcome (fate, damage pair, doom, struck part) is a value a unit test can read with no
  physics world in the process.
- `_crashed`, `_inert` and the spawn timers live in one module whose transition table is assertable
  off-engine.

**No wave adds a feature, no wave repins a golden, and no wave crosses into `GameSession.cs`.**
The session-side collection of Instant Action state is a separate candidate; a wave here that finds
itself editing `GameSession` has drifted and should stop.

## Decisions (2026-08-20)

Settled in a grilling session against the tree at `1ffbf4c7`, then re-checked against `28bc2033`.
This table is the authority where the prose disagrees with itself.

| # | Question | Decision |
|---|---|---|
| 1 | Is the success measure testability and locality, or file size? | **Testability and locality first, file size as a real second goal.** That fixes a contact-driven wave order, but keeps the HUD wave early rather than last so the size relief does not wait for wave 6. |
| 2 | Does the presentation block earn a real module, or is it a partial file? | **A real module.** A partial would deliver the whole size win at no risk, but it leaves 43 public mutable fields untouched and hides the coupling rather than removing it. `AnimRuntime` is the cautionary case: it is declared `partial` with no second part anywhere in the tree. No partial files in this plan. |
| 3 | Does the contact resolver return an outcome, or apply one? | **Return, and return both halves.** `_collideArmorDamage`, `_collideHealthDamage` and `_collideDooms` exist only to carry `ResolveContact`'s result forward to `SurviveHit` in the same step; a returned value makes that ordering structural instead of remembered. The struck party's damage cannot be returned, so it comes back as an explicit instruction the caller executes. Same reasoning as `PLAN-deepening` Decision 19. |
| 4 | Does detection sit inside the resolver, or behind its own seam? | **Behind a seam.** `SweepAirframe` is irreducibly Godot (`DirectSpaceState`, `CastMotion`, `GetRestInfo`), so a pure resolver cannot contain it. |
| 5 | Is that seam contact detection, or world queries? | **World queries.** `HitWorld` alone is called by three of this plan's waves, so a contact-only seam would leave the primitive existing twice. Two methods cover all eight query sites. The seam already exists in two weaker forms: `AiModeMachine.ProbeBlocked` (a delegate) and `TurretController._host` (a nullable concrete `FlightController` held largely for one ray). |
| 6 | Where does the collision response go? | **A three-way split.** Slide, restitution and the lever-arm kick move onto `FlightModel`, which already owns `BounceNormalSpeed` and is already pinned by `BounceRestitutionTests`; fate, the damage pair and the un-embed loop go to the resolver, which holds `IWorldQuery`; presentation goes to the HUD wave. Rejected: response inside the resolver, which would have the resolver mutating a `FlightModel` it does not own, relocating the coupling rather than removing it. Also rejected: un-embed on `FlightModel`, which is dependency-free today and should stay so. |
| 7 | Does `AircraftLifecycle` execute a transition's engine effects, or report them? | **Report, the same shape as contact.** Executing them would make lifecycle depend on the HUD wave and give the plan two extraction shapes instead of one. `InPlay` and `Crashed` stay on the node as forwards, so the fifteen internal readers and every external consumer are untouched. |
| 8 | One stored input source, or three extracted arms behind the existing ternary? | **One stored `IFlightInputSource`, resolved in `Bind`.** The arm is provably fixed after bind: `Pilot` is assigned only through the build DTO, and `HoldSegments` is set once in `HumanFlightAdapter`. The ternary re-evaluates an invariant choice every physics frame for every aircraft. |
| 9 | Is presentation one module, or the HUD feed only? | **HUD feed only.** The camera already has `CameraController`; what `_Process` holds is a four-way mode choice with the work already delegated. Moving that too would mean a fourth extraction shape and a fight with `CameraOwned`. The camera mode choice stays on the node as a separate, later question. |
| 10 | What is the wave order? | **World query, input, HUD, response, contact, lifecycle.** The seam is foundational, input is the cheapest real extraction and establishes the evidence discipline, and the HUD at wave 3 buys the size relief three waves earlier than a strict dependency order would. Accepted cost: the damage-flash wiring is touched twice, once in C5 as it stands and once in E10 to come from the outcome. |
| 11 | Is `PLAN-deepening`'s evidence rule inherited? | **Yes, at 16/16 goldens, with no wave exempt, plus one new assertion per wave watched failing first.** With one amendment: goldens are the backstop for wave E, not the proof. A frame hash cannot witness a contact decision that only fires on impact, so E is gated on five in-engine suites instead. |
| 12 | Which new names become domain terms? | **Two `CONTEXT.md` entries.** The contact family, fixing **contact** (the event), **impact** (the point), **graze** (the survivable outcome) and **crash** (the fatal one) against each other, because the code currently spells one family at least five ways. And **world query**, named `IWorldQuery` with `Sweep` and `Ray`. "Probe" is unavailable: it is taken by `Testing/Probes.cs`, `ProbeGroundBlow` and `ProbeBlocked` in three different senses. Everything else gets an `architecture.md` entry and nothing more. |
| 13 | Is the public field reduction a deliverable or a nice-to-have? | **A per-wave deliverable with a counted target.** Roughly 16 of the 43 bind-time fields leave the public surface across the six waves. Without this the plan produces six modules and a node whose interface is exactly as wide as today, which is the critique that already applies to `FlightControllerBuild`. The one field this plan *adds*, the narrow member replacing the cross-instance grace write, is counted against the reduction rather than excused. |

## ⚠ Read this before implementing anything

Line citations below are against `28bc2033`. `main` moved between the grilling session and this
plan (BL-415, BL-428, BL-429), which shifted every anchor in the file by roughly 44 to 52 lines and
added photo mode. Re-locate before trusting a number.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "`FlightControllerBuild.cs` already extracted responsibilities from the controller." | It moved 12 lines net. It is a 21-field DTO plus a `Bind` that performs 26 assignments; no flight, contact, weapon, camera or HUD logic moved, and the public fields are still public. `architecture.md` frames it as the roster keeping data resolution while the controller keeps its runtime interface. |
| 2 | "The input ternary is a live per-frame choice between three arms." | `Pilot` is assigned only through `FlightControllerBuild.Pilot`, applied in `Bind`; every other site in the repo is constructing that DTO. `HoldSegments` is assigned once, in `HumanFlightAdapter.cs`. The arm cannot change after bind, so the branch is invariant and the per-frame evaluation buys nothing. |
| 3 | "The contact cluster can be extracted whole as a pure module." | `SweepAirframe` is `GetWorld3D().DirectSpaceState`, `PhysicsShapeQueryParameters3D`, `CastMotion` and `GetRestInfo`. It is a Godot query and cannot be in a pure module. This is why D4 and D5 exist and why the plan cuts after detection. |
| 4 | "`SurviveHit` is a decision function with a bool on the end." | It is five concerns: the fate decision, the damage ledger application, the presentation writes (`Visuals`, `Gauges`, `_damageFlashText`), the collision response mutating four of `FlightModel`'s public fields from outside, and an iterative un-embed that needs world queries. Splitting it is the substance of waves D and E, not a detail of one item. |
| 5 | "The 16 goldens will witness a contact regression." | A golden is a hash of one frame. `c1-crash` samples one crash at one frame. A resolver that got the entity cut wrong for AI into AI would very plausibly leave all 16 hashes untouched. The in-engine suites are the net for wave E; see Decision 11. |
| 6 | "The presentation wave can take all of `_Process`." | Its first 25 lines are `PollResultsShortcuts` (input), `PollPauseAndHalt` (halt state), orbit seeding (camera state) and render interpolation (pose maths). The HUD is what comes after. C5 takes roughly 300 lines, not the 415 a whole-`_Process` reading suggests. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B3, B4, C5, C6, D7, D8 | The friction and the fix shape are both traced. Confirm the trace, then implement. |
| **Shape settled by the Decisions table, the slice is a judgement call** | E9, E10, E11, F12, F13 | The interface is decided; where exactly each of `SurviveHit`'s five concerns cuts is not, and the un-embed loop in particular needs care. Budget for a slow read before the first edit. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy.

**⚠ Another session owns the boards.** `VersusBoard`, `StuntRaceBoard`, `StuntScoreboard`,
`PauseBoard` and `IaWrapupBoard` are being worked on concurrently. C5 must not touch them. The
pilot HUD collaborators (`Gauges`, `Reticle`, `WeaponReadout`, `Compass`, `Marker`, `TargetHud`,
`FontTest`) are this plan's; the boards are not.

## What the data actually ships

The eight physics-query sites in `FlightController.cs`, which A1 routes through one seam:

| Site | Query | Wave that owns the caller |
|---|---|---|
| `SweepAirframe` | shape cast along a motion | E |
| `HitWorld`, from the sim step | ray, named hit | E |
| `HitWorld`, into `Gauges.AglMeters` | ray | C |
| `HitWorld`, in `StepWreckFall` | ray | F |
| `ProbeGroundBlow` | ray | stays on the node |
| `HeightAboveWorldGround` | ray, camera AGL | stays on the node |
| `WorldBlocksLine` | ray, line of sight | `TurretController` |
| `AvoidCrashBlocksLine` | ray, line of sight with a name | `AiModeMachine.ProbeBlocked` |

Public declarations at class scope: 47, of which 43 are mutable fields. The 15 this plan targets:
`Compass`, `Gauges`, `FontTest`, `WeaponReadout`, `Reticle`, `Marker` and `TargetHud`
(wave C); `Collider`, `Body`, `TouchdownDefs` (wave E); `CrashRuntime`, `CrashDefs`, `CrashAnchor`
(wave F); `HoldSegments` (wave B). `VersusHud` and `Scoreboard` are board-adjacent readouts, fed
nothing per frame and parented on the HUD canvas alone, and stay until the concurrent board work
settles.

Goldens on this plan's path, of the 16: `c1-flight`, `c1-crash`, `c1-debris-rest`,
`c1-destroy-effects`, `c1-targeting-hud`, `c1-ai-wreck`.

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

### Wave A — the world-query seam

1. ☑ `IWorldQuery` with `Sweep` and `Ray`, its Godot adapter, and all eight query sites routed
2. ☑ `TurretController` drops its `FlightController` reference for the seam, asserted off-engine

### Wave B — the input source

3. ☑ `IFlightInputSource` and its three adapters, resolved once in `Bind`
4. ☑ A scripted adapter, and `HoldSegments` stops being a public field

### Wave C — the pilot HUD

5. ☑ `FlightHud` fed a per-frame struct, absorbing `SetPilotHudVisible`
6. ☑ The HUD state-to-readout mapping asserted off-engine, including the damage flash

### Wave D — the collision response

7. ☑ `FlightModel.Collide` takes slide, restitution and the lever-arm kick
8. ☑ `BounceRestitutionTests` extended past restitution to the slide friction and the kick

### Wave E — the contact resolver

9. ☑ `ContactReport` and `ContactOutcome`, and the detection half reduced to filling a report
10. ☑ `AircraftContactResolver` owns fate, the damage pair and the un-embed loop
11. ☐ The outcome table asserted off-engine, and the cross-instance grace write replaced

### Wave F — the aircraft lifecycle

12. ☐ `AircraftLifecycle` owns `_crashed`, `_inert` and the spawn timers, and reports transitions
13. ☐ The transition table asserted off-engine, including inert and the respawn timers

## Dependency and parallelism notes

A1 blocks E9, E10, C5 and F12, because each routes a query through the seam. A2 depends only on A1.
B3 and B4 contend with nothing and could run at any point; they are placed second because they are
the cheapest real extraction and the plan wants the evidence discipline established before wave E
has to spend it. D7 prepares E10 but is independently verifiable, so D can land before E is started.
E9 precedes E10 precedes E11 as a chain. F12 consumes E10's crash outcome, so wave F starts only
after E10 lands.

File contention: every item except A2, D7 and D8 edits `FlightController.cs`, so **no two of them
run in parallel worktrees**. A2 owns `TurretController.cs`; D7 and D8 own `FlightModel.cs` and
`BounceRestitutionTests.cs`. Those three are the only items safe to run concurrently with another,
and only with an item that does not touch their files.

Out of bounds for every item: `GameSession.cs` beyond the minimum a moved call site forces, and the
five board files another session owns.

---

# Wave A — the world-query seam

## A1 ☑ `IWorldQuery` with `Sweep` and `Ray`, its Godot adapter, and all eight query sites routed

**Goal.** Every physics query in `FlightController` goes through one interface with two methods, and
the Godot types behind them appear in exactly one adapter.

**Evidence (confidence: traced).** Eight sites, tabulated in "What the data actually ships". Two of
them are shape casts or rays whose results differ only by mask and by whether the collider's
identity is wanted; the other six are the same ray with a different mask. `AiModeMachine.ProbeBlocked`
is already a `Func<Vector3, Vector3, string?>`, and `TurretController._host` is already a nullable
concrete `FlightController` used largely for one ray, so two weaker forms of this seam exist today.

**Approach.** Define `IWorldQuery` with `Sweep` (shape, base transform, motion, mask, exclusions,
returning a report with stop fraction, contact point, normal, collider and part) and `Ray` (from, to,
mask, returning first hit with collider identity). Write one Godot adapter over `DirectSpaceState`.
Hand the adapter to `FlightController` in `Bind`. Route all eight sites. Do not change a mask, an
exclusion or a fallback while routing: this item is motion only. Add the `CONTEXT.md` **world query**
entry and the `architecture.md` module entry plus index line in the same commit.

**Model recommendation.** medium. The shape is dictated by the eight call sites; the work is careful
mechanical routing with one interface design decision already made.

**Verify.** 16/16 goldens hash-identical and `.\RunTests.ps1` green. A moved golden here means a mask
or a fallback changed while routing, which is the one way this item can go wrong.

**⚠ Traps.** `SweepAirframe`'s nudge of 0.05 past first overlap exists because `GetRestInfo` can come
back empty at exactly the unsafe fraction, leaving a head-on fallback normal on what was a shallow
graze. Carry it verbatim; it is not tidyable. `HitWorld`'s three callers want different things from
the same ray, so resist collapsing them into one helper with a flags argument. The ⚠ forbidding a
swept sphere lives on the sweep and moves with it.

## A2 ☑ `TurretController` drops its `FlightController` reference for the seam, asserted off-engine

**Goal.** A turret's line-of-sight check runs against a world it was handed, not against a 3,000-line
node it holds a reference to, and a test can put a blocker in that world.

**Evidence (confidence: traced).** `TurretController._host` is a `FlightController?` field;
`WorldBlocksLine` is called through it, and the file already carries a comment describing a twin
implementation for a gunner with no host rig. Two callers wanting the same query with no shared
interface is the duplication this seam removes.

**Approach.** Give `TurretController` an `IWorldQuery` and use it for the LOS check. Keep `_host` for
everything else it legitimately needs (`WorldVelocity`, `InPlay`, `PlayerIndex`); this item narrows
one dependency, it does not remove the field. Write a synthetic `IWorldQuery` in `CSVM.Tests` and
assert the blocked and clear cases.

**Model recommendation.** medium. One dependency narrowed and one test written, both shapes settled.

**Verify.** The new assertion must be watched failing first: write it against the unblocked case,
see it pass, then add the blocker and see the pre-change code unable to report it. 16/16 goldens and
`.\RunTests.ps1` green.

**⚠ Traps.** Do not fold the gunner-with-no-host twin into this item. It is a second behaviour with
its own comment, and merging the two is a behaviour change wearing a refactor's clothes.

# Wave B — the input source

## B3 ☑ `IFlightInputSource` and its three adapters, resolved once in `Bind`

**Goal.** Which stick an aircraft flies is a value chosen at bind, and the sim step reads one source.

**Evidence (confidence: traced).** The ternary selects between `NextHoldInput`, `NextPilotInput` and
`ReadKeyboard`, all three of which already return `FlightInput`. `Pilot` is assigned only through the
build DTO and `HoldSegments` only in `HumanFlightAdapter`, so the choice cannot change after bind.
See disproven claim 2.

**Approach.** `IFlightInputSource.Read(dt)` returning `FlightInput`. Three adapters carrying the
current bodies unchanged. Resolve in `Bind` and store. Replace the ternary with one call. The seam
reads pilot intent only: `ProbeGroundBlow` and the `AiGroundBlowScale` write stay on the node, which
`FlightInput`'s own doc already blesses by saying the ground-blow fields are filled by the caller
because only it has the world. `Pilot` stays a public member, since `DriveAiGunner` and
`DriveAiRocketeer` reach through `Pilot.Machine` for more than input.

**Model recommendation.** medium. Mechanical, with one ordering rule to get right.

**Verify.** 16/16 goldens hash-identical and `.\RunTests.ps1` green. The AI goldens (`c1-ai-wreck`)
are the ones that would move if the resolution order slipped.

**⚠ Traps.** Resolution must happen before the first `SimStep`, which adds an ordering rule to a
class that already has several. Put it next to the existing `Bind` assignments rather than in
`_Ready`, so the rule sits where the other construction rules are. A null source is a bug, not a
fallback to keyboard: fail loudly.

## B4 ☑ A scripted adapter, and `HoldSegments` stops being a public field

**Goal.** A suite can fly a deterministic input profile without a public field on the controller.

**Evidence (confidence: traced).** Suites already construct bare controllers and set fields directly,
so the profile mechanism has consumers. `HoldSegments` exists as a public field only because the
selection was a ternary over it.

**Approach.** A scripted adapter taking a segment list. Convert the held-input suites to construct
it. Make `HoldSegments` private or delete it. This is the wave's public field reduction.

**Model recommendation.** medium.

**Verify.** The assertion is the `FlightInput` sequence a fixed profile produces, watched failing
first by asserting the wrong expected value once. 16/16 goldens and `.\RunTests.ps1` green.

**⚠ Traps.** The held-input path also zeroes the throttle and neutralises the surfaces on the held
branch. That behaviour belongs to the node's held branch, not to the adapter; moving it would change
what a held airframe looks like.

# Wave C — the pilot HUD

## C5 ☑ `FlightHud` fed a per-frame struct, absorbing `SetPilotHudVisible`

**Goal.** The pilot HUD is a module told what to draw, and the controller stops pushing text and
numbers into seven collaborators through public fields.

**Evidence (confidence: traced).** Roughly 300 lines across the tail of `_Process`,
`UpdateWeaponGauges`, `UpdateReticle`, the AGL read and the damage flash. `SetPilotHudVisible`
touches six of the same collaborators and is called from `GameSession`'s photo-mode path, so it is
the same module's job. See disproven claim 6 for what is not in this item.

**Approach.** A `FlightHud` module holding the seven pilot-HUD collaborators, with one entry point
taking a per-frame state struct and one `SetVisible`. Move `SetPilotHudVisible`'s body into it and
leave a forward on the node, since `GameSession` calls it. Take the damage flash as it stands today;
E10 rewires its source. The struct is passed by value or `in`, never a class: this runs every
rendered frame per aircraft and `docs/verification.md`'s PERF rules apply.

What stays on the node: `PollResultsShortcuts`, `PollPauseAndHalt`, orbit seeding, render
interpolation, and the camera mode choice.

**Model recommendation.** high. The largest single move in the plan, into a file another session is
adjacent to, with a live perf constraint.

**Verify.** `c1-targeting-hud` hash-identical is the primary witness, plus the other 15 and
`.\RunTests.ps1` green. Take the baseline before the first edit.

**⚠ Traps.** Do not touch the five board files; another session owns them. The photo-mode gate on the
pause key reads `InPhotoMode`, which stays on the node, so do not let the flag follow the HUD into
the module. `VersusHud` is board-adjacent and stays on the node this wave.

## C6 ☑ The HUD state-to-readout mapping asserted off-engine, including the damage flash

**Goal.** The mapping from aircraft state to what the pilot reads is pinned by a test rather than by
one golden frame.

**Evidence (confidence: traced).** The whole board and HUD family is currently reachable from neither
test tier; `c1-targeting-hud` is a single frame hash and is the only witness today.

**Approach.** Assert the state struct to readout mapping in `CSVM.Tests`, with the damage flash, the
stall warning, the AGL line and the weapon gauge cases. No Godot `Control` in the test: assert the
values the module would write, which is what the struct-in, values-out shape makes possible.

**Model recommendation.** medium.

**Verify.** Watched failing first. 16/16 goldens and `.\RunTests.ps1` green.

**⚠ Traps.** Resist asserting formatted strings where a number will do; a formatting change is not a
regression and a test that says otherwise will be deleted rather than fixed. Where a string must be
asserted, it goes through `CultureInfo.InvariantCulture`, per the repo-wide rule.

# Wave D — the collision response

## D7 ☑ `FlightModel.Collide` takes slide, restitution and the lever-arm kick

**Goal.** The decoded collision response lives on the model whose fields it changes.

**Evidence (confidence: traced).** `SurviveHit` writes `_model.Position`, `_model.Speed`,
`_model.VelocityDir` and `_model.BodyRates` directly, and calls `_model.BounceNormalSpeed`, which is
already on the model. `FlightModel` uses Godot only for `Vector3` and `Basis` structs, which this
project has established work in the unit tier.

**Approach.** A `Collide` on `FlightModel` taking the contact facts (previous position, step, stop
fraction, impact, normal, and whether the aircraft is human-piloted) and performing the slide, the
restitution and the lever-arm kick. Move the ⚠ recording that restitution is player-only onto that
member. Do not move the un-embed loop: `FlightModel` is dependency-free and handing it `IWorldQuery`
is a bigger concession than keeping one loop in the resolver.

**Model recommendation.** high. Decoded arithmetic where an ordering slip is invisible until a
golden moves.

**Verify.** `c1-crash` and `c1-debris-rest` hash-identical are the sharp witnesses, plus the other 14
and `.\RunTests.ps1` green.

**⚠ Traps.** The restitution is computed before the attitude kick, deliberately, because the kick adds
to the body rates the restitution reads. Preserve that order. The friction term divides by
`CrashSpeed`, so it is coupled to a constant that lives on the controller; pass it rather than
duplicating it.

## D8 ☑ `BounceRestitutionTests` extended past restitution to the slide friction and the kick

**Goal.** The whole response is pinned off-engine, not just the restitution term.

**Evidence (confidence: traced).** `BounceRestitutionTests.cs` exists and covers restitution alone.

**Approach.** Extend it with the slide direction and speed loss, the ground-stop threshold and the
lever-arm kick's sign. Table-driven, with a head-on case and a shallow-graze case.

**Model recommendation.** medium.

**Verify.** Watched failing first. 16/16 goldens and `.\RunTests.ps1` green.

**⚠ Traps.** The ground-stop threshold exists because a plane ground to near standstill was sitting
collecting zero-damage contacts forever. Assert the threshold, not a speed that happens to be below
it, so a retune does not silently void the test.

# Wave E — the contact resolver

## E9 ☑ `ContactReport` and `ContactOutcome`, and the detection half reduced to filling a report

**Goal.** Detection produces a value, and nothing downstream of it holds a Godot type.

**Evidence (confidence: shape settled, slice a judgement call).** `SweepAirframe` currently yields
seven `out` parameters into local variables that the sim step then threads through `ResolveContact`
and `SurviveHit`. See disproven claims 3 and 4.

**Approach.** Define `ContactReport` (impact, normal, struck part name, collider name, stop fraction,
and a `StruckIsAircraft` flag) and `ContactOutcome` (fate, the decoded damage pair, doom, struck part,
the damage-flash text, and the instruction to damage the struck aircraft and arm both grace windows).
Reduce the sweep and the centre-ray fallback to filling a report. The resolver never holds a `Node`:
the only question `ResolveContact` asks about the struck object is whether it is an aeroplane, which
a bool carries, and the caller already holds the collider for the applying.

**Model recommendation.** high.

**Verify.** 16/16 goldens hash-identical and `.\RunTests.ps1` green, plus the five suites named in
E11. This item introduces types and changes no decision, so a moved golden means a slip.

**⚠ Traps.** The grace window suppresses the sweep, not just the damage, so `_collisionGrace` is a
precondition of detection and not a filter on its result. Keep it on the detection side.

## E10 ☑ `AircraftContactResolver` owns fate, the damage pair and the un-embed loop

**Goal.** A contact's outcome is computed by a module that holds no Godot type and can be run in a
unit test with no physics world.

**Evidence (confidence: shape settled, slice a judgement call).** `SurviveHit`'s five concerns, of
which this item takes two: the fate decision (doom rule, health exhaustion, ground stop) and the
damage pair. The un-embed loop comes too, because it needs `IWorldQuery` and the resolver holds it.

**Approach.** The resolver takes `IWorldQuery` as a constructor dependency and a `ContactReport` per
call, and returns a `ContactOutcome`. The node applies: the damage ledger, the struck party's damage,
both grace windows, `FlightModel.Collide`, the HUD flash, and `Crash` on a fatal fate. Rewire the
damage flash to come from the outcome, closing C5's placeholder. Carry the ⚠ requiring the player to
stay asymmetric onto the resolver's interface as an `IsHumanPiloted` argument.

**Model recommendation.** high. The highest blast radius item in the plan.

**Verify.** The five in-engine suites `graze-bounce`, `ground-contact`, `ai-crash-defs`,
`ai-wreck-fall` and `player-destroy-choreography` are the gate. 16/16 goldens are the backstop, not
the proof; see disproven claim 5.

**⚠ Traps.** Each aircraft sweeps itself, so the resolver stays per-aircraft and never becomes a
session-level broker; suppressing the struck plane hitting back is the failure this shape invites.
"Who executes the returned instruction" is a remembered invariant, which is the accepted cost of
Decision 3: keep the outcome one value with no optional parts, so forgetting it is forgetting one
statement rather than four.

## E11 ☑ The outcome table asserted off-engine, and the cross-instance grace write replaced

**Goal.** The decoded contact rules are readable as a table in a unit test, and no aircraft writes
another aircraft's private field.

**Evidence (confidence: traced).** `ResolveContact` writes `struckRig._collisionGrace` directly,
legal today only because both parties are the same class.

**Approach.** Replace the cross-instance write with a narrow member on `FlightController` that the
caller invokes, and count it against wave E's field reduction rather than excusing it. Assert the
outcome table: the doom rule for an AI ramming a non-aeroplane, the entity cut for AI into AI, the
player's exemption from both, the ground stop, and the shatter-and-fly-through case.

**Model recommendation.** medium.

**Verify.** Watched failing first, one row at a time. The five suites from E10 plus 16/16 goldens.

**⚠ Traps.** The entity cut applies only on the non-player branch and only against another aeroplane.
A table that conflates "struck an aircraft" with "took the cut" will pass against the current code
and mask the exact bug this item exists to catch.

# Wave F — the aircraft lifecycle

## F12 ☐ `AircraftLifecycle` owns `_crashed`, `_inert` and the spawn timers, and reports transitions

**Goal.** The states an aircraft moves between live in one module, and the node performs the effects
of a transition rather than deciding it.

**Evidence (confidence: shape settled, slice a judgement call).** `_crashed` is read in fifteen
places spanning weapons, the HUD and the sim loop, and `InPlay` is already the single public spelling
external consumers read.

**Approach.** The module owns the state and the transition rules and returns what happened. `Crash`
returns downed, which crash def to play, that the camera should cut, and the killer to notify; the
node does those four things. `InPlay` and `Crashed` stay on the node as forwards. The `Downed`,
`InertChanged` and `DamageApplied` events stay on the node, since `GameSession` subscribes to them
and moving them would widen this plan into the session.

**Model recommendation.** high.

**Verify.** `c1-crash` and `c1-ai-wreck` hash-identical, the `ai-wreck-fall`, `ai-crash-defs`,
`inert-aircraft` and `player-destroy-choreography` suites, and `.\RunTests.ps1` green.

**⚠ Traps.** A caller that forgets half a crash produces a `_crashed` plane with no boom and no camera
cut, which is the accepted cost of Decision 7. The mitigation is the same as E10's: one value, no
optional parts. `StepWreckFall` routes through `IWorldQuery` from A1, so do not reintroduce a direct
query here.

## F13 ☐ The transition table asserted off-engine, including inert and the respawn timers

**Goal.** The state machine is pinned by a table rather than by two golden frames.

**Evidence (confidence: traced).** No test in either tier reaches the transitions today.

**Approach.** Assert every transition: in play to crashed, crashed to respawned, the spawn-timer arm
including the carrier-drop variant, inert on and off, and the guard that a crashed aircraft cannot
crash again.

**Model recommendation.** medium.

**Verify.** Watched failing first. 16/16 goldens and `.\RunTests.ps1` green.

**⚠ Traps.** `Rerun` and `Respawn` are different mechanisms and `CONTEXT.md` fixes the words; assert
them separately and do not let the test's own names blur them.
