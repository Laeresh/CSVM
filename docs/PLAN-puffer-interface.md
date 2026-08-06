# Puffer interface — six start/stop verbs collapse into Emit / Stop

**HANDOFF DRAFT — grill before activating** (written 2026-08-06 from that day's architecture
review, candidate 3; decisions NOT yet settled). The first working session on this plan **must
start with a `/grilling` session with the user** (item A1) — the Decisions table below is empty
until then, and the checklist after A1 is provisional. When a session activates this plan, point
PROJECT_CONTEXT.md's "Current status" at it. Sibling handoffs:
[`PLAN-template-stage.md`](PLAN-template-stage.md).
[`PLAN-engine-free-suites.md`](plans/PLAN-engine-free-suites.md) is a completed sibling, archived
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
hand-maintained in `ProjectilePool.ReleaseTrails` (`Flight/Projectile.cs` ~:877) and **absent**
from `DamageVisuals` (~:79–81, :230–253 — only ever `TrailBurnAt`/`Clear`). `DriveAt` already is
the "don't make the caller choose" method — it exists for exactly one caller; `TrailBurnAt` is a
third mode existing only because the damage lab's plane doesn't move, which is a *host* property,
not an emitter property.

The proposal: one continuous pair — `Emit(worldPos, worldBasis, dt)` / `Stop()` (plus the
untouched `Burst`, `Clear`, `LiveCount`) — with the authored distance-vs-time mode, the trail
carry, re-homing on revival and the static-host fallback all inside. The six continuous verbs
leave the interface. **Behaviour-preserving**: 13/13 goldens hash-identical per item, and the
determinism pins below are hard constraints.

## Milestone goal

- One way to run a puffer continuously and one way to stop it; the authored mode is
  implementation, not caller knowledge.
- The both-stops/revive/re-home rule lives once — `DamageVisuals` gets the ghost-trail fix by
  construction, not by a third hand copy.
- The `puffer-modes` suite exercises every caller's path through the same two members.

**The callers' pooling bookkeeping stays theirs (recommended — grill it).** `EmitterDirector`'s
keying, `ProjectilePool._trailEmitters`' `InUse`/`LiveCount` reuse, `DamageVisuals._panelTrailPool`
— unifying those is a separate, larger seam; this plan only collapses the verb surface.

## Decisions (unfilled — settle in A1's grilling session)

| # | Question | Decision |
|---|---|---|
| 1–7 | See the grilling agenda in item A1. | *(to be filled by the grilling session; this table then becomes the authority where prose disagrees)* |

## ⚠ Read this before implementing anything

| # | The claim to keep honest | What guards it |
|---|---|---|
| 1 | "It's just a rename — construction can be tidied too." | No. Per `Puffer`'s architecture entry: `SpawnSustained`'s `Rand` draw order is shared determinism, and `_rng` seeding is pinned to *where* `new Puffer()` sits in `Create` — moving construction or reordering draws re-pins 4 goldens. This is an **interface-only** change; the bodies move under new names with their statement order intact. |
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

1. ☐ Grilling session: settle the Decisions table with the user; rewrite A2/A3 if the calls differ
2. ☐ `Emit`/`Stop` land inside `Puffer` (mode pick, trail carry, static-host fallback, the both-stops rule), old verbs become private; `puffer-modes` suite rewritten to the new interface
3. ☐ The three callers migrate (`PufferEmitter`, `ProjectilePool`, `DamageVisuals`); the compensating adapter lines and the hand-maintained stop rule are deleted

## Dependency and parallelism notes

A1 → A2 → A3, linear. **File ownership:** `CSVM/src/Effects/Puffer.cs`,
`CSVM/src/Mech3/Anim/PufferEmitterFactory.cs`, `CSVM/src/Flight/Projectile.cs` (trail sites only),
`CSVM/src/Flight/DamageVisuals.cs`, `CSVM/src/Testing/Suites.cs` (the `puffer-modes` suite).
Contends with [`PLAN-template-stage.md`](PLAN-template-stage.md) on the effects goldens/`Anim/`
neighbourhood; [`PLAN-engine-free-suites.md`](plans/PLAN-engine-free-suites.md) (completed
2026-08-06) also touched `Suites.cs` — no longer a live contention.

---

# Wave A — decisions, then the collapse

## A1 ☐ Grilling session — settle the decision tree with the user

**Goal.** Every question below has a user-made call in the Decisions table; the checklist is
rewritten to match. No code before this lands.

**Approach.** Run `/grilling` with this agenda, one question at a time, recommendation first (the
FireControl session, 2026-08-06, is the model):

1. **Does `Burst` stay a separate verb?** *Recommended:* yes — a one-shot burst is semantically
   distinct from continuous emission; the collapse targets the six continuous verbs only.
2. **`Emit`'s signature.** `Emit(worldPos, worldBasis, dt)` with internal distance tracking, or
   does it also need the host speed (`TrailBurnAt` takes `speedMps` today)? *Recommended:* pose +
   dt, with the static-host fallback spending virtual metres at the rate `DriveAt` already
   derives — but trace `TrailBurnAt`'s callers first; if the lab feeds a speed no pose delta can
   reconstruct, the signature grows one optional parameter.
3. **Where does the mode come from?** *Recommended:* the authored `PufferState` at construction
   (distance states trail; time states sustain — the rule `PufferEmitterFactory`'s comment
   states); `Emit` consults it, callers never do.
4. **`Stop` semantics.** Ends trail AND sustain (the ghost-trail rule), idempotent, re-home on
   next `Emit`. Any caller that must NOT end the trail on stop? *Recommended:* none exists —
   verify by reading all stop sites before deciding.
5. **Caller bookkeeping unification** (the three pools/free-lists). *Recommended:* out of scope —
   revisit as its own candidate once the verb surface is small.
6. **Migration shape.** One commit for the interface + all three callers, or interface-first with
   the old verbs delegating? *Recommended:* two commits (A2 interface + suite, A3 callers) — but
   the old verbs go private in A2, so no external caller can straddle.
7. **Which four goldens are the determinism pins**, and what run A/Bs them? Name them from the
   `Puffer` entry during the session and record them in the Decisions row (⚠ #1).

**Model recommendation.** high — user-interactive; it rewrites this plan.

**Verify.** Decisions table filled, each row naming its losing option.

## A2 ☐ `Emit` / `Stop` inside `Puffer`; the suite drives the new interface

**Goal.** `Puffer`'s public continuous surface is `Emit`/`Stop` (+`Clear`/`LiveCount`); the six
verbs are private implementation; the `puffer-modes` suite (fake renderer, no GPU) proves burst,
distance-trail, sustain and static-host paths through the new pair, including the
stop-ends-everything and revive/re-home rules.

**Evidence (confidence: traced).** The surface list and the adapter's compensating lines, cited in
the header; `RecordingEmitterRenderer` + `CreateWith` already build atlas-free for the suite.

**Approach.** Statement-order-preserving moves only (⚠ #1/#2). The mode dispatch reads the
authored state per A1 Q3; the fallback per Q2. Do not touch `SpawnSustained`, `Create`, or any
draw-order-bearing code.

**Model recommendation.** high — determinism-pinned code; the cheap-looking edits are the trap.

**Verify.** `.\RunTests.ps1` green; 13/13 goldens hash-identical (the four pin goldens from A1 Q7
are the tripwire — if any moves, stop, do not re-pin). `puffer-modes` covers every mode through
`Emit`.

**⚠ Traps.** ⚠ #1–#3 above. The revive/re-home rule currently lives in three variants — port the
*union* of behaviours and prove each caller's scenario in the suite before A3 deletes their local
copies.

## A3 ☐ The three callers migrate; the compensations die

**Goal.** `PufferEmitter` stops translating (`SustainAt`→`DriveAt`, the double-stop);
`ProjectilePool.ReleaseTrails` stops hand-managing `TrailEnd`; `DamageVisuals` calls `Emit`/`Stop`
and thereby *gains* the ghost-trail rule it never had. No public `Puffer` mode verb remains.

**Evidence (confidence: traced).** Caller cites in the header paragraph.

**Approach.** One caller at a time, goldens after each. The `DamageVisuals` change is the one
place behaviour could legitimately *improve* (a bug class it was exposed to closes) — if any
visual changes there, capture before/after in `.scratch/` and put the verdict to the user rather
than assuming either way.

**Model recommendation.** medium — mechanical once A2's contract is proven, but each caller has
its own pooling idiom to respect.

**Verify.** As A2, plus a damage-lab pass (`--fly` + F5, panel trails through revive) and a
rocket-volley check against the ghost-trail commit's scenario.

**⚠ Traps.** `ProjectilePool._trailEmitters`' reuse predicate (`InUse && LiveCount == 0`) has its
own ⚠ in the architecture entry — the migration must not change when an emitter is considered
free.
