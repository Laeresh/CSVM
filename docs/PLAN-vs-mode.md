# VS Mode — "Dogfight": splitscreen deathmatch + air-to-air hittability

**ACTIVE PLAN** (written 2026-08-06). It sits in `docs/`, which by this repo's convention makes it
a live plan; it lives on the `worktree-vs-mode` branch, so `main`'s "Current status" keeps naming
`PLAN-engine-free-suites` until this branch merges. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

A third game mode, invented for this remake (the original's multiplayer was networked — there is no
splitscreen reference to copy, per `playtest.md`): splitscreen free-for-all deathmatch for 2–4
players. Weapon kills score +1; first to the kill threshold, or the leader at the time limit, wins.
The mode front-loads M4 wave-A item A1 (air-to-air hittability, `docs/SCOPING-M4-ai.md:781`) — the
hittability work is M4 work landing early, not scaffolding. Out of scope, deliberately: AI
opponents, networking, teams, the retail MP1–MP3 spawn tables (`net.zrd.json` is undecoded — a
follow-up `[Research]` item, minted at merge), collision-shape fidelity upgrades (follow-up BL),
and menu-side match options (defaults only in v1).

## Milestone goal

- Flying aircraft have physics bodies: projectiles can strike them, part-mapped damage lands, and
  a dead critical part downs the plane — with the shooter attributed.
- "Dogfight" is the third menu entry (and `--vs` on the CLI): 2–4 players, kills/deaths tracked,
  first-to-5-kills or best-at-5-minutes, per-pane score HUD, opponent edge-arrows, ranked
  end-of-match board with R-rematch.
- The hit chain is pinned by tests at both tiers: engine-free match bookkeeping + spec parsing,
  and one in-engine suite proving hit → part → damage → kill → score, including the
  you-can't-shoot-yourself negative case.

**No AI, no networking, no teams, no MP-map data work — splitscreen FFA only.** Everything else is
M4-or-later; this plan's job is the mode and the hittability it stands on.

## Decisions (2026-08-06)

Settled in a grilling session; this table is the authority when prose below drifts.

| # | Question | Decision |
|---|---|---|
| 1 | Test rig or real mode? | **Real third mode** — menu entry, board, rematch; also serves as M4's air-to-air test bed |
| 2 | Hittability route | **Real physics bodies + collision layers (M4-A1 as scoped)** — not a bespoke near-miss-scan hit test that M4 would redo |
| 3 | Collision shapes | **Share the existing `PlaneCollider` box set** between body and terrain sweep; fidelity upgrade (convex hulls) deferred to a BL that then pays off twice |
| 4 | What downs a plane | **The real per-part model** — weapon damage → `PlaneDamage.Apply`, armor first, dead critical part = downed (the terrain-graze rule, `FlightController.cs:1547`) |
| 5 | Scoring | **Weapon kill = +1 shooter; terrain/mid-air deaths are just deaths** — no −1, no last-damager credit, no mutual kills; board shows kills *and* deaths, winner on kills only |
| 6 | Respawn | **3 s crash cam → auto-respawn (R skips), own spawn point, no invulnerability** — camping/protection are post-playtest tuning |
| 7 | Win condition | **First to 5 kills or 5 minutes, leader wins at time-out; tie = draw** (no overtime). `--vs-kills=N` / `--vs-time=minutes`, 0 disables that limit; menu uses defaults |
| 8 | Worlds | **Chapter picker unchanged, IA1 scenario spawns** — retail MP maps deferred behind the `net.zrd.json` decode |
| 9 | Representation | **`SessionSpec.Versus` bool modifier on `Fly`** (like `Stunt`); flag `--vs`, no alias; `ModeName` `"vs"`; `--vs` beats `--stunt` by fixed precedence with a warning; default scenario **`dogfight_ace`** |
| 10 | Menu | **"Dogfight"** entry; `Launch` callback's `bool stunt` becomes `enum MenuMode { Free, Stunt, Versus }`; start locked until ≥2 joined; CLI allows `--vs --players=1` with a warning |
| 11 | HUD/board | **Mirror the stunt-race triple**: engine-free `VersusMatch`, per-pane timer/K/D/leader + kill banners, opponent edge-arrows (v1, load-bearing), `VersusBoard` + `RestartMatch` |
| 12 | Verification | **Both tiers** — xUnit for match/spec/menu; one in-engine suite for the full hit chain with the self-hit negative case non-optional; `PT-` item for human judgment |
| 13 | Process | **Worktree branch `worktree-vs-mode`**, this plan on the branch; waves A (hittability) → B (kill flow) → C (mode+UI), each independently landable; follow-up BLs + SCOPING-M4 update at merge |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in
this worktree session; use a local commit or a file copy.

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

### Wave A — Air-to-air hittability (front-loads M4 A1)

1. ☑ Aircraft physics bodies on the shared `PlaneCollider` shapes, with collision layers
2. ☑ Projectiles strike aircraft: per-shot owner exclusion, struck-part mapping, damage applied
3. ☑ In-engine hittability suite: hit → part → damage → kill chain + self-hit negative case

### Wave B — Kill flow and match bookkeeping

11. ☐ Bullet-kill attribution: critical-part death → `Crash` with killer identity reported
12. ☐ `VersusMatch` engine-free bookkeeping + xUnit tests
13. ☐ VS respawn loop: 3 s auto-respawn, R skips early

### Wave C — Mode plumbing and UI

21. ☐ `SessionSpec`: `--vs`, `--vs-kills`, `--vs-time`, precedence, `dogfight_ace` default + parse tests
22. ☐ Menu: "Dogfight" entry, `MenuMode` enum, ≥2-player start lock + menu tests
23. ☐ Per-pane VS HUD: match timer, own K/D, leader, kill banners
24. ☐ Opponent edge-arrows (MarkerHud adaptation)
25. ☐ `VersusBoard` end-of-match overlay + `RestartMatch` rematch
26. ☐ Landing sweep: docs (cli.md, architecture.md, controls.md), follow-up BLs, SCOPING-M4 A1 note, `PT-` item

## Dependency and parallelism notes

A1 → A2 → A3 is a strict chain and blocks B11. B12 is engine-free and independent — it can land any
time. B13 needs B11 (a VS-mode death must exist). C21 and C22 are spec/menu plumbing, independent of
waves A/B, but C22 builds on C21's spec surface. C23–C25 need B11 + B12 (scores must exist to show).
C26 is last. File contention: A2 and B11 both edit `Projectile.cs`/`FlightController.cs`, and C21/C22
both touch `SessionSpec.cs` — run those pairs sequentially, not in parallel agents. Everything runs
in this one worktree; no parallel worktrees planned.

---

# Wave A — Air-to-air hittability

## A1 ☑ Aircraft physics bodies on the shared `PlaneCollider` shapes, with collision layers

**Goal.** Every flying aircraft carries a `CollisionObject3D` body whose shapes are the existing
`PlaneCollider` boxes, on a dedicated collision layer — so a physics ray can strike a plane, and
the shape set stays single-sourced for both "being shot" and the terrain `CastMotion` sweep.

**Evidence (confidence: traced).** `docs/SCOPING-M4-ai.md:650-666`: the flying aircraft has no
physics body; `PlaneBuilder` leaves `generateCollision = false`; `PlaneCollider` boxes are
query-only (`CastMotion`/`GetRestInfo`); repo-wide there are zero `CollisionLayer`/`CollisionMask`
assignments. `PlaneCollider` derives 5–8 plane-frame boxes from mesh triangles (architecture.md
`## src/Flight/PlaneCollider.cs`): boxes deliberately overlap, earliest in Parts order wins;
Bloodhawk canard tips uncovered — known limit, unchanged by this plan.

**Approach.** An `AnimatableBody3D` (or `Area3D` if ray-only proves sufficient — decide in-code
against how `ProjectilePool` queries) parented to the aircraft, one `CollisionShape3D` per
`PlaneCollider.Part` reusing its `BoxShape3D` + local transform, moved with the plane each sim
tick. Introduce named collision layers (world / aircraft / projectile-query) as constants — first
layer assignments in the repo, so put them in one place. Keep shape derivation untouched: the body
consumes `PlaneCollider.Parts`, it does not fork it.

**Model recommendation.** high — first collision-layer scheme in the codebase, and the
plane-vs-plane sweep side effect (below) is a blast-radius judgment.

**Verify.** In-engine: a ray fired at a spawned plane's fuselage from a test suite returns the
body and the struck shape maps to a `Parts` entry. Regression: `.\RunTests.ps1` green; stunt and
free flight unchanged (terrain sweep still uses the same shapes); frame cost check with `--perf`
on a 4-player session — four bodies must not move the needle.

**⚠ Traps.** Giving aircraft bodies makes them visible to every other aircraft's `CastMotion`
terrain sweep — plane-vs-plane collision arrives as a side effect (`SCOPING-M4-ai.md:671-677`).
That is *accepted* (mid-airs should be lethal, decision #5 makes them plain deaths), but it must be
deliberate: the sweep must exclude the sweeping plane's own body, and a mid-air must route through
the existing `SurviveHit`/`Crash` path, not a physics solver response (the plane is a `Node3D`
moved by `FlightModel`, not physics-driven — nothing here may start pushing transforms).

## A2 ☑ Projectiles strike aircraft: per-shot owner exclusion, struck-part mapping, damage applied

**Goal.** A fired round that crosses an opponent's plane hits it: the shooter's own plane is never
hittable by their own rounds, the struck box maps to a data part, and the weapon's damage values
land through `PlaneDamage.Apply`.

**Evidence (confidence: traced).** `Projectile.cs:463` `Spawn(...)` already carries
`shooterId` (= `FlightController.PlayerIndex`). `ClassifySurface` (`Projectile.cs:406-418`) can
only return `Default`/`Water`/`Buildings` today; `SurfaceClass.Player`/`Enemy`
(`Flight/WeaponDefs.cs:10`) are shipped but unreachable. `NearMissPass` (`Projectile.cs:1801-1814`)
already measures every round against every registered aircraft with shooter-identity exclusion —
the hit test slots beside it. `PlaneDamage.MapStruckPart` + `Apply` semantics:
architecture.md `## src/Flight/PlaneDamage.cs`.

**Approach.** Extend `ProjectilePool`'s existing raycast to include the aircraft layer, excluding
the shooter's body per shot. On an aircraft hit: `ClassifySurface` → `Player`, resolve the rig from
the struck body, `MapStruckPart(partName, localImpact)`, `Damage.Apply(part, weapon health/armor
damage)`, reuse the impact feedback plumbing (flash text, gauges blink, part visuals) that terrain
grazes drive. Death handling itself is B11 — here a critical-part zero may simply call the
existing `Crash`; attribution wiring waits.

**Model recommendation.** high — touches the two highest-traffic combat modules
(`Projectile.cs`, `FlightController.cs`) and the shared-ray trap lives here.

**Verify.** In-engine (formalized as A3): scripted shot on a known bearing strikes, damages the
right part, and a self-aimed geometry never self-hits. `--weapon-test` and the existing weapon/
damage suites stay green — world-destructible hits must be untouched.

**⚠ Traps.** `ProjectilePool._ray` is a **shared mutable query object** — per-shot `Exclude` must
be set *and reset*, or the exclusion leaks into the next shot (`SCOPING-M4-ai.md:671-677`). Do not
route plane hits through the destructible pipeline (`DestructibleRegistry` stages are
world-object semantics). `PlaneDamage`'s "tail" arm is correct only because `PlaneCollider.Relabel`
hands it no outboard boxes — do not "fix" sidedness in `PlaneDamage` (architecture.md, rejected).

## A3 ☑ In-engine hittability suite: hit chain + self-hit negative case

**Goal.** A `--run-tests` suite pins the whole chain — projectile hit → struck part → damage →
critical-part kill — and pins that a plane's own rounds never strike it. The self-hit case is
non-optional: it is the regression most likely to arrive silently (reads as "guns too strong" in
playtest) and only a suite can catch it.

**Evidence (confidence: traced).** Suite registry: `Testing/Suites.cs` `Register` (25 suites);
harness patterns in `Testing/TestHarness.cs` (`WithWorld`, SKIP-when-data-absent, report +
exit code). `--det` is implied by `--run-tests`. Suites must run windowed — `--headless` compiles
no shaders (architecture.md `## src/Testing`).

**Approach.** One new suite (working name `air-to-air`): build a 2-player VS-ish stage (`--stage=empty`
is the cheap host — collidable ground plane, ~2 s boot), place two planes on a known bearing,
fire a scripted burst, assert: ray struck the target body; `MapStruckPart` named the expected part;
HP/armor moved by the weapon's data values; forced critical-part zero triggers `Crash`; and a
burst fired through the shooter's own geometry (e.g. straight ahead through the propeller disc arc)
registers zero self-hits. Follow the SKIP-not-PASS rule for absent retail data.

**Model recommendation.** medium — pattern-following against 25 existing suites; the hard
thinking happened in A1/A2.

**Verify.** `.\RunTests.ps1` — new suite green on the retail install, SKIP without it, and the
suite *fails* when owner exclusion is deliberately broken (prove it can fail before trusting it;
`docs/verification.md`).

**⚠ Traps.** Read `docs/verification.md` first. `--frames=N` is a sim coordinate, not wall-clock —
time the burst in sim frames. Keep the suite in-engine; anything engine-free it accretes belongs
in `CSVM.Tests` per the two-tier rule.

# Wave B — Kill flow and match bookkeeping

## B11 ☐ Bullet-kill attribution: critical-part death → `Crash` with killer identity reported

**Goal.** When a round's damage kills a critical part, the victim crashes exactly as terrain
crashes do today, and the session learns *who* killed *whom*; terrain and mid-air deaths report a
death with no killer. Post-match deaths still crash but score nothing (match ignores them — B12).

**Evidence (confidence: traced).** The lethality rule at `FlightController.cs:1547-1551` ("a dead
critical part downs the plane"); `Crash(...)` at `:1359`; `shooterId` arrives with the hit (A2).
Rigs are assembled per player by `FlightRigAssembler` with `PlayerIndex` — the session can address
both parties.

**Approach.** The A2 damage-apply site, on critical-part zero, calls `Crash` and raises a
kill/death event carrying (victim `PlayerIndex`, killer `PlayerIndex?` — null for terrain and
mid-air). `GameSession` (or a thin VS runtime it owns) forwards to `VersusMatch` (B12) when in VS
mode; outside VS the event simply has no subscriber. Keep `FlightController` ignorant of match
rules — it reports facts, the session scores them.

**Model recommendation.** high — the seam between combat and session is the design-sensitive cut;
`FlightController.cs` is high-traffic.

**Verify.** In-engine: extend or reuse A3's suite — a scripted kill increments the right shooter's
tally through the real event path; a scripted terrain crash reports a death with null killer.

**⚠ Traps.** Do not put score state in `src/Flight/` — M4 scoping notes flight has no mutable
static state, and the mode must not introduce any. Mid-air collisions arrive through `SurviveHit`'s
existing path; resist inventing a separate mid-air handler.

## B12 ☐ `VersusMatch` engine-free bookkeeping + xUnit tests

**Goal.** An engine-free class (not a Node — `StuntRace`'s shape) holding per-player kills/deaths
and the match clock; fires `MatchCompleted` on kill threshold or time limit; `Standings()` ranked
by kills; `Restart()` rematches; events after completion are ignored.

**Evidence (confidence: traced).** The pattern: `Flight/StuntRace.cs` (freed with the session,
host-fed state, `RaceCompleted` event, off-engine tested in `CSVM.Tests/StuntRaceTests.cs`).
Rules are Decisions #5/#7: kills only score, deaths tracked for display, draw on tie at time-out,
0 disables a limit.

**Approach.** `Flight/VersusMatch.cs`: ctor takes player count + `killTarget` + `timeLimit`;
`RegisterKill(shooter, victim)`, `RegisterDeath(victim)`, `Advance(dt)` host-fed like the race;
`Standings()` returns rows (index, kills, deaths, rank; ties share rank → draw when tied at
completion). `CSVM.Tests/VersusMatchTests.cs`: threshold win, timeout win with leader, draw,
post-completion shots ignored, rematch resets, disabled-limit variants.

**Model recommendation.** medium, low effort — a small pure class against a worked example
(`StuntRace`) with the rules fully decided.

**Verify.** `dotnet test` — the new test file green; no engine reference (`GD.*`, `Node`) anywhere
in the class (the two-tier rule, architecture.md `## CSVM.Tests`).

**⚠ Traps.** `StuntRace`'s lesson (`StuntMission` once crashed the xUnit host via `GD.Print` —
fixed in engine-free-suites A2): keep every engine touch out, including logging.

## B13 ☐ VS respawn loop: 3 s auto-respawn, R skips early

**Goal.** In VS mode a downed player watches the crash cam ~3 s, then auto-respawns at their own
spawn point with full HP/ammo; R still respawns early. No invulnerability window. Other modes'
manual-R behavior is unchanged.

**Evidence (confidence: traced).** `Respawn()` (`FlightController.cs:536`) already resets damage,
weapons, visuals, gauges, prop-start choreography. `AutoRespawnDelay = 1.5 s` exists but is
scripted-runs-only (`:273`). R binding documented in `docs/controls.md:26`.

**Approach.** Arm the existing auto-respawn path in VS mode with a 3 s delay (a spec-driven
condition where the HoldInput-only condition sits today). Respawn position: the player's own
spawn-list entry, which is what `Respawn()` already returns to. Update `docs/controls.md` only if
any binding changes (none planned).

**Model recommendation.** medium, low effort — a guarded constant and a condition.

**Verify.** In-engine: in a `--vs` scripted session a crashed plane is flying again ≈3 s later
(sim frames, not wall-clock); in `--fly` it still waits for R. Death registered exactly once per
crash (no double-count across the auto/manual paths).

**⚠ Traps.** Respawn must not emit a second death event; the death was registered at `Crash`.
Spawn camping / invulnerability are explicitly deferred (Decision #6) — do not "improve" here.

# Wave C — Mode plumbing and UI

## C21 ☐ `SessionSpec`: `--vs`, `--vs-kills`, `--vs-time`, precedence, `dogfight_ace` default + parse tests

**Goal.** `--vs` parses to `Versus = true` on `Mode = Fly` with scenario defaulting to
`dogfight_ace`; `--vs-kills=N`/`--vs-time=minutes` carry the match rules (defaults 5/5, 0 disables);
`--vs` beats `--stunt` with a logged warning; `--vs --players=1` warns and runs; `ModeName` is
`"vs"`. All engine-free, all covered in `SessionSpecTests`.

**Evidence (confidence: traced).** `SessionSpec.cs:15` mode enum + the doc comment ("modifiers on
top of one, not shapes of their own"); `Stunt` bool at `:104`; `Players` at `:199`; `ModeName`
`:130-138`; `FromMenu` ~`:750`; parser precedence style (anim-lab > freecam > viewer > fly).
`dogfight_ace` is a shipped IA1 scenario (`docs/cli.md:194`).

**Approach.** Follow `Stunt`'s plumbing end-to-end: flag → record field → `ModeName` → `FromMenu`.
Scenario default: `Versus ? "dogfight_ace"` in the same place stunt sets `stunt_flying`. Parse
stays pure. Tests beside the existing stunt cases.

**Model recommendation.** medium — mechanical but in a 66 KB load-bearing record; the tests are
the guard.

**Verify.** `dotnet test` (`SessionSpecTests` + new cases). `docs/cli.md`: three new flag bullets
**and** the flag-index count — the parser's accepted-flag count and cli.md's index count are kept
equal on purpose (100 → 103); PROJECT_CONTEXT's day-to-day table gets *no* new row unless
day-to-day (it is — add `--vs` only, the match flags stay cli.md-only).

**⚠ Traps.** cli.md bullets are the description of record; the PROJECT_CONTEXT table is a gloss
(its own ⚠ says so). Don't let `--vs` imply `--players=2` silently — the menu enforces ≥2, the CLI
only warns (Decision #10).

## C22 ☐ Menu: "Dogfight" entry, `MenuMode` enum, ≥2-player start lock + menu tests

**Goal.** The mode screen offers Free Flight / Stunt Flying / Dogfight; the launch callback carries
`enum MenuMode { Free, Stunt, Versus }` instead of `bool stunt`; in Dogfight the plane-screen start
stays locked until ≥2 players have joined, with the existing hint style prompting "P2: press Start".

**Evidence (confidence: traced).** Hardcoded 2-entry `Modes` array `LaunchMenu.cs:72-76`; `Launch`
callback signature `:52`; join flow (P1 picks mode/chapter, pads join with Start on the Plane
screen) `:17-28`; engine-free menu tests exist (`CSVM.Tests/SessionSpecMenuTests.cs`).

**Approach.** Extend the `Modes` array; thread the enum through `Launch` → `SessionSpec.FromMenu`
(sets `Versus`, scenario, default match rules). Start-lock: the plane screen's ready/start logic
gains a Versus-only ≥2 condition + hint line. Update `SessionSpecMenuTests` for the third entry and
the lock rule.

**Model recommendation.** medium — UI threading with an existing test net.

**Verify.** `dotnet test` menu cases; in-engine `--menu=mode --screenshot=` shot showing the third
entry (the layout-verification path cli.md documents for `--menu`).

**⚠ Traps.** The callback signature change touches every `Launch` call site — sweep them all in
one edit. Don't leak match-rule configuration into the menu (Decision #7: defaults only).

## C23 ☐ Per-pane VS HUD: match timer, own K/D, leader, kill banners

**Goal.** Each pane shows one compact `HudFont` line — remaining time, own kills/deaths, current
leader — and a transient "P2 DOWNED P3" banner on each kill, sized through `HudMetrics` so it
stays readable in a quarter pane.

**Evidence (confidence: traced).** `HudMetrics.PaneFactor` (√(pane/window) damping, 1P identity);
`MarkerHud` already carries per-pane status banners + `PlayerIndex`; `HudFont`/`WeaponReadout` are
the bitmap-font precedent; `SplitScreen.PlayerTag`/`PlayerColor` name and tint players.

**Approach.** A small VS HUD element per rig (assembled by `FlightRigAssembler` when `Versus`),
fed from `VersusMatch` standings + the kill event; banners reuse `MarkerHud`'s banner machinery or
its pattern. Player names are the existing P1–P4 tags with their colors.

**Model recommendation.** medium.

**Verify.** `--vs --players=2 --screenshot=` deterministic shot (a `--debug-*` force-visible
switch if needed, per the house `--debug-scoreboard` convention); readable at 4-player pane size.

**⚠ Traps.** HUD sizing decisions belong in `HudMetrics` — don't scale ad hoc per element
(architecture: it is "the one place HUD sizing is decided").

## C24 ☐ Opponent edge-arrows (MarkerHud adaptation)

**Goal.** Each player's pane marks opponents when off-screen: edge arrow + clock bearing, in the
opponent's player color — because two planes losing each other in a chapter-sized world makes the
mode unplayable (the original had radar for exactly this reason; we have no splitscreen reference,
so the stunt-gate marker language is our own precedent to extend).

**Evidence (confidence: traced for the machinery, direction-sound for the presentation).**
`MarkerHud` draws off-screen edge arrows with clock bearings for stunt gates, per-pane, already
race-aware with `PlayerIndex`.

**Approach.** Feed `MarkerHud` (or a sibling using its projection/edge math) the other rigs'
aircraft positions in VS mode; one marker per living opponent, hidden while that opponent is
crashed. Presentation details (arrow-only vs on-screen box) are TUNE — start with the gate
marker's existing visual language.

**Model recommendation.** medium.

**Verify.** Deterministic screenshot with a known opponent bearing; marker flips to the correct
edge as the target crosses out of frame (two-shot A/B).

**⚠ Traps.** Do not use `AnimRuntime.PlayerPosition` (a P1-only singleton, flagged in
`SCOPING-M4-ai.md:702-706`) — read positions from the rigs.

## C25 ☐ `VersusBoard` end-of-match overlay + `RestartMatch` rematch

**Goal.** On `MatchCompleted`, a full-window ranked board (winner or "DRAW" on top; kills + deaths
per row, player colors) over the live world; R rematches — everyone respawns, scores and clock
reset — mirroring the race board's flow. Post-match flying continues beneath; nothing scores.

**Evidence (confidence: traced).** `StuntRaceBoard` (CanvasLayer 10 above SplitScreen's 0, rows
from `Standings()`, wakes on the completion event, R routed through `GameSession.RestartRace`,
`GameSession.cs:1489-1501`). `Racer.FinishTime` snapshots so rematch doesn't corrupt the board —
same discipline for the final standings here.

**Approach.** `Flight/VersusBoard.cs` mirroring `StuntRaceBoard`; `GameSession.RestartMatch`
mirroring `RestartRace` (respawn all rigs, `VersusMatch.Restart()`). Snapshot standings at
completion for display.

**Model recommendation.** medium, low effort — the closest thing this plan has to a
pattern-stamp.

**Verify.** In-engine: scripted match to threshold → board rows match the bookkeeping; R →
scores zeroed, clock restarted, planes respawned. Deterministic screenshot of the board.

**⚠ Traps.** R is also the respawn key (B13) — the board must own R only while visible, exactly
as the race board does; check the race board's input-claim pattern rather than inventing one.

## C26 ☐ Landing sweep: docs, follow-up BLs, SCOPING-M4 note, `PT-` item

**Goal.** The merge-ready state: docs updated, follow-ups minted, nothing left implicit.

**Evidence (confidence: traced).** House rules in PROJECT_CONTEXT (docs in the same turn as the
change; backlog items minted via `New-ItemId.ps1` only — its counter is shared across worktrees).

**Approach.** (1) `docs/architecture.md`: entries for the new/changed modules (`VersusMatch`,
`VersusBoard`, VS HUD, body/layers in `PlaneCollider`/`FlightController`/`Projectile` entries).
(2) `docs/cli.md` already done in C21 — recheck the count. (3) `docs/controls.md` only if bindings
changed. (4) Mint BLs: `[Research]` decode `net.zrd.json` as the MP spawn table → retail MP1–MP3
maps for VS; `[Cleanup/Tuning]` collision-shape fidelity (convex hulls per clipped region — also
fixes close-stunt terrain false positives); one consolidated `[Tuning]` VS item (spawn
camping/protection, suicide penalty, last-damager credit, sudden-death, menu match options,
`dogfight_ace` spacing). (5) `docs/SCOPING-M4-ai.md`: mark wave-A item A1 front-loaded by this
plan. (6) Mint the `PT-` item: damage balance plane-vs-plane, camping viability, arrow
readability, draw frequency. (7) Archive this plan per the lifecycle when merged.

**Model recommendation.** medium, low effort — bookkeeping with exact targets.

**Verify.** `.\RunTests.ps1` fully green in the worktree; `New-ItemId.ps1` used for every ID;
PROJECT_CONTEXT "Current status" edit happens **at merge on main**, not on the branch.

**⚠ Traps.** BL-084 (race spawn fairness) and BL-126 (splitscreen tuning) stay open and separate —
the new VS tuning item must reference, not absorb, them. `docs/HISTORY.md` is frozen — the record
goes in commit messages.
