# Puffer interface — six start/stop verbs collapse into Emit / Stop

**COMPLETE** (2026-08-06). Drafted 2026-08-06 from that day's architecture review, candidate 3;
the A1 grilling session ran 2026-08-06 and filled the Decisions table below, which is the
authority where prose disagrees. Sibling handoffs:
[`PLAN-template-stage.md`](../PLAN-template-stage.md) (still live).
[`PLAN-engine-free-suites.md`](PLAN-engine-free-suites.md) is a completed sibling, archived
2026-08-06.

`Puffer`'s caller-visible surface exposes the implementation's modes: `Burst`, `TrailAdvance`,
`TrailEnd`, `TrailBurnAt`, `SustainAt`, `SustainEnd`, `DriveAt` (`Effects/Puffer.cs` ~:430–577) —
the caller must know which mode the authored data uses. The real adapter already lies to
compensate: `PufferEmitter.SustainAt` calls `Puffer.DriveAt` (`Anim/PufferEmitterFactory.cs`
~:92–104, comment *"distance states trail; time states sustain"*), and its `SustainEnd` calls
**both** `SustainEnd()` and `TrailEnd()` — that second line IS the ghost-trail fix (commit
`450131a`, 2026-08-06, "Rocket explosions no longer draw ghost puff trails from the previous
blast"), whose own message ends: *"The projectile pool's own flyout trails already managed
`TrailEnd` themselves and are untouched."* One caller was fixed; the identical rule is
hand-maintained in `ProjectilePool.ReleaseTrails` (`Flight/Projectile.cs` ~:877) and in
`ThrottleSlamSmoke` (`Flight/ThrottleSlamSmoke.cs` ~:104–107 — a **fourth** caller the original
draft missed, found in A1's call-site sweep), and **absent** from `DamageVisuals` (~:79–81,
:230–253 — only ever `TrailBurnAt`/`Clear`). `DriveAt` already is
the "don't make the caller choose" method — it exists for exactly one caller; `TrailBurnAt` is a
third mode existing only because the damage lab's plane doesn't move, which is a *host* property,
not an emitter property.

The proposal: one continuous pair — `Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)` /
`Stop()` (plus the untouched `Burst`, `Clear`, `LiveCount`) — with the authored distance-vs-time
mode, the trail carry, re-homing on revival and the static-host fallback all inside. The six
continuous verbs leave the interface. **Behaviour-preserving**: 13/13 goldens hash-identical per item, and the
determinism pins below are hard constraints.

## Milestone goal

- One way to run a puffer continuously and one way to stop it; the authored mode is
  implementation, not caller knowledge.
- The both-stops/revive/re-home rule lives once — `DamageVisuals` gets the ghost-trail fix by
  construction, not by a third hand copy.
- The `puffer-modes` suite exercises every caller's path through the same two members.

**The callers' pooling bookkeeping stays theirs (Decision 5).** `EmitterDirector`'s keying,
`ProjectilePool._trailEmitters`' `InUse`/`LiveCount` reuse, `DamageVisuals._panelTrailPool`,
`ThrottleSlamSmoke._exhausts` — unifying those is a separate, larger seam; this plan only
collapses the verb surface.

## Decisions (settled 2026-08-06; authority where prose disagrees)

| # | Question | Decision |
|---|---|---|
| 1 | `Burst` a separate verb? | **Yes — `Burst(worldPosition)` stays public and untouched.** One-shot, no `dt`, none of the trail-carry/re-home machinery. *Losing option:* folding it into `Emit` via a mode parameter — re-exports the mode knowledge the collapse deletes. |
| 2 | `Emit`'s signature | **`Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)`.** Moving host → the `DriveAt` dispatch unchanged; still host + `0` → time cadence (the static building sputter, golden-pinned); still host + `>0` → virtual-metre burn (the damage lab, which passes its own `StaticBurnSpeed` 15 — a caller TUNE no pose delta can reconstruct, so the plan's suspected optional parameter is confirmed needed). Two distinct static behaviours exist today and both survive. *Losing options:* per-instance burn-speed property (splits behaviour across construction, ⚠#1 territory); always-required speed; `Puffer`-owned burn TUNE (silently changes the building sputter). |
| 3 | Where the mode comes from | **The authored `PufferState`, read per call — `_state.DistanceInterval > 0`, exactly `DriveAt`'s existing dispatch.** No mode enum, no constructor flag; `Create`'s `sustained:` stays a pool-sizing knob only, never a mode selector. *Losing options:* construction-time enum (second source of truth); caller-passed hint. |
| 4 | `Stop` semantics | **Unconditional both-stops (`SustainEnd` + `TrailEnd`), idempotent, re-home on next `Emit`; `Clear` stays the separate hard-kill.** Verified against every stop site: no caller needs the trail kept alive on stop (`ReleaseTrails` and `ThrottleSlamSmoke` stop pure trails, so both-stops is a no-op superset; `DamageVisuals` never stops, only `Clear`s; the adapter already does both). `EmitterDirector`'s four stops stay four — selector×disposition is *which* emitters stop, above this seam. *Losing option:* `Stop(endTrail:)` — no caller for the `false` arm, reopens the ghost-trail bug class. |
| 5 | Caller bookkeeping unification | **Out of scope.** Four idioms, not three (`ThrottleSlamSmoke._exhausts` joins the list); they differ on ownership/reuse policy — a separate axis with its own ⚠-guarded, measured rules. This collapse makes the seams comparable; unifying them is a future candidate. *Losing option:* unify now — scope creep in a 13/13-hash-identical plan. |
| 6 | Migration shape | **Two commits; privatisation moves to A3** (the draft's "private in A2" cannot compile with four external callers). A2: `Emit`/`Stop` land as statement-order-preserving dispatch over the *still-public* old verbs + suite rewritten — goldens trivially identical. A3: all four callers migrate, compensations deleted, six verbs flip private in the same commit — the compiler enforces no-straddle. *Losing options:* one big commit (loses the bisectable proven-contract step); per-caller commits (half-duplicated ghost-trail rule in between; fallback only if a golden moves mid-migration). |
| 7 | Determinism pins + A/B | **Five pins, not four** (the "four" in the `Puffer` entry's `_rng` ⚠ is a stale count; the `SizeScaleDefault` 4→1 measurement is the authority): `c1-waterfall`, `c3-island`, `c5-city-night`, `c1-destroy-effects`, `c1-crash` — the other 8 goldens carry no live emitter. A/B = the full 13-golden `.\RunTests.ps1` run, hash-identical per item, at baseline / post-A2 / post-A3 (per-caller during A3's work). Any pin moving = construction/draw-order slip — stop and diagnose, **never re-pin**. Fix the architecture entry's count when that file is next touched. |

## ⚠ Read this before implementing anything

| # | The claim to keep honest | What guards it |
|---|---|---|
| 1 | "It's just a rename — construction can be tidied too." | No. Per `Puffer`'s architecture entry: `SpawnSustained`'s `Rand` draw order is shared determinism, and `_rng` seeding is pinned to *where* `new Puffer()` sits in `Create` — moving construction or reordering draws re-pins the five pin goldens (Decision 7). This is an **interface-only** change; the bodies move under new names with their statement order intact. |
| 2 | "`Create`'s overrides can be simplified." | The `blend`/`softParticles` overrides must survive verbatim (same entry). |
| 3 | "`TrailBurnAt` is dead weight." | It is a *behaviour* (static-host virtual-metres burn) that must survive as the `Emit` fallback when the pose delta is ~zero — `DriveAt` ~:566–577 already half-implements it. Deleting the semantics instead of the verb breaks the damage lab. |
| 4 | "Line citations are current." | Gathered 2026-08-06 against `51cdb5a` (unaffected by `7410cbe`, which touched `Flight/FlightController`/`Loadout` only — but `Projectile.cs` cites may drift under the concurrent polish plan). Re-grep before editing. |

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

### Wave A — decisions, then the collapse

1. ☑ Grilling session: Decisions table settled 2026-08-06; A2/A3 rewritten (fourth caller found, privatisation moved to A3, `Emit` grew `staticBurnMps`)
2. ☑ `Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)`/`Stop` landed inside `Puffer` (`DriveAt` body moved into `Emit` + the burn branch, `DriveAt` now an alias); `puffer-modes` suite rewritten — sustain, trail, still-sputter, static-burn and stop/revive all through `Emit`/`Stop`; 13/13 goldens hash-identical
3. ☑ The **four** callers migrate (`PufferEmitter`, `ProjectilePool`, `DamageVisuals`, `ThrottleSlamSmoke`); the compensating adapter lines and the hand-maintained stop rules are deleted; the six old verbs are `private` (`DriveAt` deleted outright — dead once `PufferEmitter` calls `Emit` directly). `RunTests.ps1` green (551 units / 24 engine / 13 goldens); `c1-flight` re-pinned — its `--hold` throttle jump crosses `ThrottleSlamSmoke`'s slam threshold, and migrating its raw `TrailAdvance` to `Emit` adds the documented one-puff homing-frame sputter (⚠, confirmed at the controls via a rocket-volley capture and a damage-lab capture, verdict: accept). This closes the plan.

## Dependency and parallelism notes

A1 → A2 → A3, linear. **File ownership:** `CSVM/src/Effects/Puffer.cs`,
`CSVM/src/Mech3/Anim/PufferEmitterFactory.cs`, `CSVM/src/Flight/Projectile.cs` (trail sites only),
`CSVM/src/Flight/DamageVisuals.cs`, `CSVM/src/Flight/ThrottleSlamSmoke.cs`,
`CSVM/src/Testing/Suites.cs` (the `puffer-modes` suite).
Contends with [`PLAN-template-stage.md`](PLAN-template-stage.md) on the effects goldens/`Anim/`
neighbourhood; [`PLAN-engine-free-suites.md`](plans/PLAN-engine-free-suites.md) (completed
2026-08-06) also touched `Suites.cs` — no longer a live contention.

---

# Wave A — decisions, then the collapse

## A1 ☑ Grilling session — settled 2026-08-06

All seven questions put to the user one at a time, recommendation first; every recommendation was
accepted, two with substantive amendments the call-site sweep forced: the caller list grew to four
(`ThrottleSlamSmoke`, Q5/Q6) and the draft's "old verbs go private in A2" was corrected to A3 —
it cannot compile earlier (Q6). The sweep also confirmed Q2's suspicion (the lab's
`StaticBurnSpeed` 15 is a caller TUNE no pose delta can reconstruct → the optional parameter) and
surfaced that "the static-host fallback" is TWO behaviours (time-cadence sputter vs virtual-metre
burn), both of which survive. The Decisions table above holds each call with its losing options.

## A2 ☑ `Emit` / `Stop` inside `Puffer`; the suite drives the new interface

**Goal.** `Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)` and `Stop()` land as new public
members; the six old verbs **stay public and untouched** in this commit (Decision 6 — they flip
private in A3). The `puffer-modes` suite (fake renderer, no GPU) proves burst, distance-trail,
sustain and BOTH static-host paths (time-cadence at `staticBurnMps == 0`, virtual-metre burn
at `> 0`) through the new pair, including the stop-ends-everything and revive/re-home rules.

**Evidence (confidence: traced).** The surface list and the adapter's compensating lines, cited in
the header; `RecordingEmitterRenderer` + `CreateWith` already build atlas-free for the suite.

**Approach.** `Emit`/`Stop` are dispatch over the existing verb bodies — statement-order-preserving
(⚠ #1/#2): `Emit` extends `DriveAt`'s dispatch (still + `staticBurnMps > 0` → the `TrailBurnAt`
path), `Stop` is the adapter's both-stops pair. Mode per Decision 3 (`_state.DistanceInterval`,
read per call; `sustained:` stays pool sizing only). Do not touch `SpawnSustained`, `Create`, or
any draw-order-bearing code. Goldens are trivially identical here — no caller changes.

**Model recommendation.** high — determinism-pinned code; the cheap-looking edits are the trap.

**Verify.** `.\RunTests.ps1` green; 13/13 goldens hash-identical (the five pin goldens of
Decision 7 are the tripwire — if any moves, stop, do not re-pin). `puffer-modes` covers every
mode through `Emit`, both static fallbacks included.

**⚠ Traps.** ⚠ #1–#3 above. The revive/re-home rule currently lives in three variants — port the
*union* of behaviours and prove each caller's scenario in the suite before A3 deletes their local
copies.

## A3 ☐ The four callers migrate; the compensations die; the verbs go private

**Goal.** `PufferEmitter` stops translating (`SustainAt`→`DriveAt`, the double-stop);
`ProjectilePool.ReleaseTrails` and `ThrottleSlamSmoke` stop hand-managing `TrailEnd`;
`DamageVisuals` calls `Emit(…, StaticBurnSpeed)`/`Stop` and thereby *gains* the ghost-trail rule
it never had. The six old verbs flip private in this same commit (Decision 6) — the compiler,
not discipline, enforces that no external caller straddles. No public `Puffer` mode verb remains.

**Evidence (confidence: traced).** Caller cites in the header paragraph; `ThrottleSlamSmoke.cs`
~:104–107, :123.

**Approach.** One caller at a time, goldens after each (per-caller *commits* only as a fallback if
a golden moves mid-migration). `StaticBurnSpeed` stays a `DamageVisuals` TUNE, passed as
`staticBurnMps`. The `DamageVisuals` change is the one place behaviour could legitimately
*improve* (a bug class it was exposed to closes) — if any visual changes there, capture
before/after in `.scratch/` and put the verdict to the user rather than assuming either way.

**Model recommendation.** medium — mechanical once A2's contract is proven, but each caller has
its own pooling idiom to respect.

**Verify.** As A2, plus a damage-lab pass (`--fly` + F5, panel trails through revive), a
rocket-volley check against the ghost-trail commit's scenario, and a throttle-slam plume
(idle→full jump shows the exhaust smoke, ends cleanly).

**⚠ Traps.** `ProjectilePool._trailEmitters`' reuse predicate (`InUse && LiveCount == 0`) has its
own ⚠ in the architecture entry — the migration must not change when an emitter is considered
free.
⚠ **The homing-frame sputter (found in A2).** `Emit`'s still-host rule fires on the trail's FIRST
call too — the homing frame has no motion yet, so a pool/slam trail migrating from raw
`TrailAdvance` gains ONE sustained batch at the muzzle/exhaust per launch (`smokepuffer` authors
no NUMBER ⇒ one puff). The director's callers always had this (it is the building sputter's
immediacy, suite-asserted); the pool/slam callers did not. Judged negligible-to-desirable in
A2 (one 1-puff batch at the launch point), but it IS the one behavioural delta of the migration
besides `DamageVisuals` — put it to the user with the rocket-volley check if it reads as a
change at the controls.
