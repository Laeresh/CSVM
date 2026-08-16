# Race spawn fairness — the abreast starting grid

**✅ COMPLETE** (written 2026-08-08, completed 2026-08-08 on branch `worktree-bl-084-race-grid`).
All six items landed, each by a subagent and verified before merge: `.\RunTests.ps1` green at every
step (639 unit tests, 26 engine suites, 13/13 goldens hash-identical), and the headline guarantee —
a `--det` splitscreen stunt launch emitting the same spawn lines it emitted before the grid existed
— checked against a baseline captured on the clean tree *before* any edit, at both 2 and 4 players.
Indexed in [`plans.md`](plans.md).

**What this bought, in one line:** a 4-player C1 race used to put P4 at `(-6957,230,-2684)` heading
`-23°` while P1 sat at `(-4974,179,-3861)` heading `90°` — two kilometres and a different direction.
It now puts all four on one line, 60 m apart, on one heading, at one altitude.

**Owed:** `PT-45` (hand-flown, two controllers) is the only evidence this feature can ever have —
`--det` is given the old spawn walk by design, so no scripted run can photograph a grid. `BL-314`
(the race countdown) is minted and blocked on it.

This plan closes `BL-084`. A splitscreen stunt race currently hands each pilot the *next* entry in
the mission's `stunt_flying` spawn list (`SpawnPicker.ChooseSpawn`, `CSVM/src/Session/SpawnPicker.cs:61`),
so pilots begin at genuinely different distances from the zones and the placing is decided partly
before anyone touches a stick. This plan replaces that, for races only, with an **abreast starting
grid**: one anchor spawn, every pilot fanned symmetrically about it, the whole field lifted as one
to clear terrain. The seam is a new whole-field `IFlightStarts` interface, so `--det`, solo flight
and Dogfight never construct the grid at all and their spawns stay byte-identical.

`BL-084` was re-verified still-open on 2026-08-08 against both the record (`git log --grep=BL-084`
is empty; `docs/HISTORY.md` is frozen and carries nothing on it) and the code (`SpawnPicker.cs:61`
still does per-player list indexing; the `SpawnAbreast` fan at `:54` is reachable only from the
`--spawn-at`/`--pos` debug override).

**Deliberately excluded.** The 3-second race countdown is **not** in this plan — it is minted here
as its own `BL-` item and lands only after this grid has been confirmed at the controls, because it
changes `StuntMission.Elapsed`'s documented "the clock never stops" rule and deserves its own
scrutiny. Dogfight (`--vs`) is untouched: its spawn spacing is `BL-301`'s call, to be judged from
`PT-43`. `BL-126` keeps `SpawnAbreast` and the debug-override fan.

## Milestone goal

- Every pilot in a splitscreen stunt race starts at the same point on the map, on the same heading,
  at the same altitude, so finishing order reflects flying rather than spawn luck.
- Spawn placement has a seam: a whole-field `IFlightStarts` with two implementations, selected once
  at session build.
- The fan geometry and the lift rule are covered by headless tests in `CSVM.Tests`, including a
  proof that a `--det` race produces exactly today's spawns.
- Grid width and terrain clearance are config-backed and appear in `--dump-config`, so the owed
  playtest can settle them in one sitting.

**This plan changes spawn *placement* only — never the flight model, the run clock, or Dogfight.**
The countdown, race best-times and slot rotation are all deliberately deferred to items minted by
A6, each blocked on the playtest this plan owes.

## Decisions (2026-08-07 grilling session)

The table is the authority where the prose below contradicts itself.

| # | Question | Decision |
|---|---|---|
| 1 | Abreast grid, or rank on per-player-normalised time? | **Abreast grid** — the stunt objective is order-free (`StuntMission.cs:59`), so there is no route to normalise against; splitscreen stunt is our invention with no original reference, so a grid costs no fidelity |
| 2 | What does the (deferred) countdown hold? | **Rolling start** — physics-alive presentation, same fairness as a full freeze, no dead-still aircraft |
| 3 | How does the countdown flight work? | **On rails** — kinematic level walk handing off at `_model.Reset(...)`, so GO *is* today's spawn; no sink, no divergence between planes, decoupled from `BL-074` |
| 4 | Keeping the fan out of terrain | **Centre the fan, lift the whole field uniformly** — per-plane lift rejected: different altitudes are the same unfairness in a new coordinate |
| 5 | Scope | **Races only** (`Race != null`) — Dogfight wants its scattered spawns; that is `BL-301`'s call from `PT-43` |
| 6 | Scripted runs | **`--det` bypasses the whole path** via implementation selection, not a flag — accepted cost: the grid is hand-flown verification only |
| 7 | Seam shape | **Whole-field `IFlightStarts`**, grid delegates to `SpawnPicker` for the anchor — per-player was rejected: centring needs the count, the lift needs every slot probed first |
| 8 | One item or two? | **Two** — the grid is additive and contained; the countdown changes a deliberate clock contract |
| 9 | Do grid slots rotate? | **Fixed by player index** — residual lateral bias is unmeasured and likely negligible; measure at the playtest, and if real, randomise per race rather than rotate per rematch |
| 10 | Grid spacing | **Own `Config.GetFloat` value**, fallback 60 m — `SpawnAbreast` stays `BL-126`'s and untouched |
| 11 | Race best-time persistence | **Stays off; correct the now-false rationale** in `StuntRace.cs:50-52`; note "race best-times now feasible" as an unlocked follow-up |
| 12 | Terrain probe | **Injected `Func<Vector3, float?>` ground sampler** — keeps the fan and lift headless-testable; the physics-tick trap lives in the production closure |
| 13 | Playtest | **Mint a `PT-` item** — three decisions here explicitly defer to it and `--det` can never exercise this path |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Normalised time is a viable alternative to a grid." | The stunt objective is **order-free** (`StuntMission.cs:59`; `StuntZone.CompletionOrder`, `:29-31`, records the *flown* order, explicitly not the ia.json list order). There is no route, so a baseline would have to assume a tour — and would penalise a pilot who flies a better one than the solver assumed. |
| 2 | "The spawns have a slight downward angle, so a plane will fly into the ground." | `SpawnPoint` is `(Vector3 Position, float HeadingDeg)` — **yaw only** (`SpawnPoints.cs:9`). `LogSpawn` builds the attitude as `Basis(Vector3.Up, heading) * Vector3.Forward`, which is horizontal, and even the story-mission path discards authored pitch and roll (`SpawnPoints.cs:19`). Every spawn we emit is dead level. The observed sink is the flight model: the spawn speed (then a fixed placeholder, now the mission's own `PLAYER_INIT` value via `SpawnPicker.StartState`) sits below the speed that sustains level flight at the spawn throttle, so the plane descends while it accelerates toward cruise. |
| 3 | "Reuse `SpawnAbreast`; the fan already exists." | The 60 m fan at `SpawnPicker.cs:54` is inside the `_spec.SpawnAt` branch — reachable **only** from the `--spawn-at`/`--pos` debug override, never from a normal `--stunt` launch. The code shape is proven; the behaviour is not wired. It is also `BL-126`'s value, and `PLAN-vs-mode.md:500` records that BL-084 and BL-126 stay separate. |

Consequence of #2 worth carrying: any setback or clearance figure derived from today's fixed
53.6 m/s goes stale when **`BL-074`** lands, because the decoded spawn speed is the mission's own
`PLAYER_INIT[4] × 0.1` (18 m/s in nearly every mission) rather than 53.6. A speed-derived offset
would therefore move by a factor of three under the plane, and would move again on any mission
authoring one of the other values. This plan avoids the exposure entirely by not simulating
anything at spawn; the deferred countdown item must not reintroduce it.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
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

### Wave A — the starting grid

1. ☑ `IFlightStarts` — the whole-field seam, with `SpawnPicker` as its plain implementation
2. ☑ `RaceGrid` — centred fan, injected ground sampler, uniform lift, headless tests
3. ☑ Select the implementation once at session build; races get the grid, everything else does not
4. ☑ Config-backed grid width and clearance, reported in the spawn log
5. ☑ Correct the best-time persistence rationale in `StuntRace`
6. ☑ Docs, close `BL-084`, mint the countdown `BL-` and the race `PT-`

## Dependency and parallelism notes

Strictly linear: A1 → A2 → A3 → A4 → A6, with A5 independent of all of them (it touches only a
comment in `StuntRace.cs` and may land at any point). A1 blocks A2 and A3 because both are written
against the interface it introduces. A2 and A3 both touch `CSVM/src/Session/` and A3 edits the file
A1 created — **do not run these in parallel worktrees.** A6 runs last by construction: it closes the
item the others implement, and its `PT-` entry describes behaviour A4 must already have made
dialable.

---

# Wave A — the starting grid

## A1 ☑ `IFlightStarts` — the whole-field seam, with `SpawnPicker` as its plain implementation

**Goal.** Spawn placement is decided by one call that answers for the whole field at once, and
nothing about where any plane starts has changed.

**Evidence (confidence: traced).** `SpawnPicker` has a blast radius of two live sites: constructed
at `GameSession.cs:258`, injected into `FlightRigAssembler` (`:25`, `:30`). Its per-player
`ChooseSpawn(spawns, missionZrdrPath, spawnBase, playerIndex, tag)` returns `(Vector3 pos, Vector3
lookAt)` (`SpawnPicker.cs:41-71`). Interfaces are rare in this tree and reserved for genuine
polymorphism — eight in total (`IEmitter`/`IEmitterFactory`, `IGunSlot`/`IPylonSlot`,
`ISequenceHost`, `IDamageLabTarget`, `IAnimMotion`, `IEmitterRenderer`) — so one here earns its
place only because A2 supplies a second implementation.

**Approach.** Add `IFlightStarts` in `CSVM/src/Session/` with a single whole-field method returning
one start per player (a `readonly record struct` carrying the same `pos` / `lookAt` pair the tuple
carries today, so no call site has to reinterpret anything). Implement it on `SpawnPicker` itself by
looping its existing `ChooseSpawn` — **do not** create a second type for today's behaviour, and do
not change `ChooseSpawn`'s body: A2 calls it directly for the anchor. Change
`FlightRigAssembler` to take `IFlightStarts` and to ask once rather than per rig.

Per-player was considered and rejected (Decision 7): centring needs the player count and the uniform
lift needs every slot probed before *any* answer is correct, so a per-player signature forces the
grid to accumulate state across four calls and makes player 1's answer wrong until player 4 has
asked.

**Model recommendation.** medium — a small mechanical refactor, but it sets the shape every later
item is written against.

**Verify.** `.\RunTests.ps1` fully green. Then the real check: a `--det` splitscreen stunt launch
before and after, with the `spawn [...]` lines (`SpawnPicker.cs:78`) diffed — they must be
identical, character for character. Take the baseline *first*; an unchanged number is not evidence
unless you have seen it able to fail.

**⚠ Traps.** `--det` defaults `--spawn=0` (`docs/cli.md:210`), so a scripted baseline pins the
anchor — good for the diff, but it means a bug in the random per-launch pick will not show up here.
Check an unpinned launch varies too. Do not move the `_spec.SpawnAt` override branch
(`SpawnPicker.cs:47`): it is tested before the list branch, which is what makes `--pos` beat the
spawn list, and A2 depends on that ordering surviving untouched.

## A2 ☑ `RaceGrid` — centred fan, injected ground sampler, uniform lift, headless tests

**Goal.** Given an anchor and a player count, every pilot gets a slot on one level line through that
anchor, symmetric about it, with the whole field raised as one by whatever the worst slot needs to
clear the ground.

**Evidence (confidence: traced for the mechanism, direction-sound for the values).** The fan
expression exists and works — `at += dir.Cross(Vector3.Up).Normalized() * (playerIndex *
SpawnAbreast)` (`SpawnPicker.cs:54`) — but is one-sided, leaving P1 on the authored point and P4
180 m out. Terrain probing is available and already used this way:
`space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.WorldAndAircraft))`
(`Suites.cs:1026`, `FlightController.cs:1506`). `CSVM.Tests` is headless xUnit that already uses
Godot value types (`SessionSpecTests.cs`), so `Vector3` maths is testable there but a live physics
space is not.

**Approach.** `RaceGrid : IFlightStarts` in `CSVM/src/Session/`. Constructor takes the
`SpawnPicker` (for the anchor) and a `Func<Vector3, float?>` ground sampler. Resolve the anchor by
**delegating** to `SpawnPicker.ChooseSpawn` with `playerIndex: 0` — the grid must learn none of the
ia.json / `PLAYER_INIT` / C1-fallback / `--pos` knowledge that call holds. Then: centred offsets
(±0.5, ±1.5 × spacing for four players; the general form is `i - (n-1)/2`), sample every slot, take
the single worst, raise the entire field by it. Every plane shares the anchor's heading.

Name it `RaceGrid`, **not** `SplitScreenSpawner` — Dogfight is splitscreen too and deliberately does
not use this (Decision 5); a name implying otherwise reads as a bug six months from now.

Tests go in `CSVM.Tests` against a synthetic heightfield: slots centred and evenly spaced; the field
lifted by the *worst* slot and never per-plane; a null sample handled deliberately; and the anchor
still honouring `--pos`.

**Model recommendation.** medium — geometry plus a test suite; the judgement is in what the tests
assert, not in the maths.

**Verify.** The headless tests are the primary instrument here, deliberately: the lift rule is the
one place a plausible-looking bug is invisible. Lift each plane by its own ground instead of the
field by the worst and the race still starts, still looks correct in every screenshot, and is
quietly unfair. That assertion must exist and must be seen to fail before it passes.

**⚠ Traps.** The physics space needs a tick before `IntersectRay` returns anything — a probe at
session build can come back empty and read as "no ground." That decision belongs in the production
closure built in A3, in one place with one log line, not threaded through this class. Do not tighten
clearance to the point where `BL-300`'s known box-overhang false positives trip it; convex hulls are
that item's job, not this one's.

## A3 ☑ Select the implementation once at session build; races get the grid, everything else does not

**Goal.** A race constructs `RaceGrid`; solo flight, `--vs`, the zone-less chapters and any `--det`
run never construct it at all.

**Evidence (confidence: traced).** The gate already exists and needs no new condition:
`race = new StuntRace()` fires only when `--stunt` is set, the mission has `dzones`, and
`_rigs.Count > 1` (`GameSession.cs:1376-1382`). `_spawnPicker` is built at `GameSession.cs:258`.
The zone-less chapters (C1C, C2B) take the "flying free" branch at `:1380` and get no race, so they
are excluded for free.

**Approach.** At `GameSession.cs:258`, choose the implementation: `RaceGrid` when the session will
be a race and `--det` is off, otherwise the plain `SpawnPicker`. Build the ground-sampler closure
here — a downward ray through `CollisionLayers.WorldAndAircraft`, with the empty-result case decided
and logged once. Pass `IFlightStarts` into `FlightRigAssembler`.

**Model recommendation.** medium — small, but it is the item that decides what `--det` means for
this feature, and getting the gate wrong is silent.

**Verify.** Four launches: a solo `--stunt`, a 2-player race, a 4-player race, and a `--vs`. Only
the race launches may show grid slots in the spawn log. Then re-run A1's `--det` diff — it must
still be byte-identical, which is the guarantee bought by routing the bypass through implementation
selection rather than a runtime flag.

**⚠ Traps.** The bypass must be **selection**, not an early return inside `RaceGrid` — the point of
Decision 6 is that the grid type is never constructed on a scripted path. Do not reach into
`--vs`: spawning four dogfighters 60 m apart on one heading is an instant head-on merge every round,
which is a balance change to Dogfight and belongs to `BL-301`, to be judged from `PT-43` evidence
rather than assumed here.

## A4 ☑ Config-backed grid width and clearance, reported in the spawn log

**Goal.** The two judge-by-eye values can be dialled at the playtest without a rebuild, and the
values actually in force are visible from the console.

**Evidence (confidence: traced).** `Config.GetFloat(key, fallback)` (`Config.cs:131`) reads a JSON
override, self-registers for `--dump-config`, and returns the fallback verbatim when no file exists —
so an absent config is byte-identical to a hardcoded const. `SpawnPicker.cs:78` already emits a
per-player `spawn [...]` line carrying position and heading.

**Approach.** Grid width and lift clearance each via `Config.GetFloat`, width falling back to 60 m —
not because 60 m is right, but because it is the number already in the tree and there is no evidence
for another. Extend the spawn log so a race line names the slot index and the applied lift, making a
bad grid obvious without a screenshot.

**Model recommendation.** medium, low effort — mechanical, with exact targets.

**Verify.** `--dump-config` lists both new keys with their fallbacks. A config file overriding the
width visibly changes the grid; removing it restores the 60 m spacing exactly.

**⚠ Traps.** These are **TUNE, not fact** — record them as such and do not let a fallback harden
into a decision. Leave `SpawnAbreast` alone: it is `BL-126`'s value, it does a different job
(keeping debug planes out of each other on the `--pos` path), and `PLAN-vs-mode.md:500` records that
the two items stay separate. Sharing one constant would mean retuning the debug fan every time the
grid width moves.

## A5 ☑ Correct the best-time persistence rationale in `StuntRace`

**Goal.** The comment explaining why race best-times are not persisted states a reason that is still
true.

**Evidence (confidence: traced).** `StuntRace.cs:50-52` currently reads: *"race totals aren't
comparable across player counts or spawn positions, since each player starts at a different point in
the mission's spawn list."* The grid falsifies that second clause outright. `ScoreStore` is a simple
keyed store (`GetBest(key)` / `RecordIfBest(key, total)`), so enabling races would be a matter of
choosing a key, not building machinery — which is exactly why the rationale needs to be right.

**Approach.** Comment only; **behaviour unchanged**. Restate the reason that survives: a centred
grid slot is a synthetic position by construction, so no pilot sits on an authored spawn and a race
total measures a run from a different place than a solo total does. Note in the closing commit that
race best-times are now *feasible* — an unlocked follow-up, not a promise, and one that would want
its own key namespace since race and solo totals diverge again once the countdown lands.

**Model recommendation.** medium, low effort — a comment, but one whose wrongness would cost a
future session an hour.

**Verify.** Reading it cold, the comment explains a decision rather than describing code that no
longer exists. `.\RunTests.ps1` green (nothing should move).

**⚠ Traps.** Do not enable persistence as a drive-by. And do not simply delete the comment — a
silent absence leaves the next reader unable to tell whether persistence is off by decision or by
oversight, which is the cold-reading failure this repo's commenting style exists to prevent.

## A6 ☑ Docs, close `BL-084`, mint the countdown `BL-` and the race `PT-`

**Goal.** The plan's record is complete: the modules are documented, `BL-084` is gone from
`backlog.md`, and the two follow-ups exist as real items with real IDs.

**Evidence (confidence: traced).** House rules: docs land in the same turn as the change;
`docs/HISTORY.md` is frozen, so the record goes in the commit message; a landed backlog item is
**deleted**, not marked FIXED. New IDs come from `New-ItemId.ps1` only — its counter is shared
across worktrees (`PLAN-vs-mode.md:481-482`). `playtest.md` §1 groups `PT-nn` under a launch command
with *Look for* / *Blocks* / *Variations*; `PT-43` (`playtest.md:144-162`) is the closest sibling —
invented splitscreen, no original reference, a judgement on our own remake.

**Approach.** (1) `docs/architecture.md`: entries for `IFlightStarts` and `RaceGrid`, and an update
to `SpawnPicker`'s. (2) Delete `BL-084` from `backlog.md`. (3) Mint the countdown `BL-`, carrying
Decisions 2, 3 and 6 and the `BL-074` exposure from the ⚠ table, blocked on the new `PT-`. (4) Mint
the `PT-`: two-pad C1 stunt race, `./RunGame.ps1 --stunt --players=2 --chapter=C1`. *Look for:* does
the grid read as a starting line at 2 and 4 panes; spacing at the wingtips; whether the uniform lift
ever looks absurd (a field hovering over a valley) or too tight (a wingtip in a hillside); a **felt**
end-of-grid advantage, which is Decision 9's rotation trigger; that the per-launch random anchor
still varies between launches; and that `--pos` still overrides the grid. *Blocks:* A4's two config
values, the rotation call, and the countdown item. Record that it needs two controllers, as `PT-43`
does. (5) Refresh PROJECT_CONTEXT.md "Current status" **at merge on main**, not on this branch.

**Model recommendation.** medium, low effort — bookkeeping with exact targets.

**Verify.** `New-ItemId.ps1` used for both new IDs (the duplicate-ID commit hook is the backstop,
not the plan). `.\RunTests.ps1` green. The commit-message record names what landed and how it was
verified.

**⚠ Traps.** `BL-126` and `BL-301` must be **referenced, not absorbed** — the same instruction
`PLAN-vs-mode.md:500` records for BL-084 itself. Write the new `PT-` so it can be judged without
this conversation in context: the whole point of the item is that `--det` can never exercise this
path, so a hand-flown sitting is the only evidence that will ever exist.
