# SequenceRunner seam — extract the sequence interpreter behind `ISequenceHost`

**ACTIVE PLAN** (written 2026-07-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

`AnimRuntime.cs` (3,538 lines) nests the sequence interpreter — `SequenceRunner`, the event-clock /
LOOP / IF-ELSEIF engine — as a private class that reaches its host at exactly three points:
`rt.Dispatch(...)`, `rt.EvaluateCondition(...)`, and the `rt.OnEventDispatched?.Invoke(...)`
debugger hook. The interpreter is pure logic, but being nested and private it is untestable from
`CSVM.Tests`, so its ten hard-won semantics (each one a shipped, measured bug — the bowl sign's
38%-blank frames, the frozen traffic loops, the double-polling waterfall) exist only as prose in
comments and are verifiable only through full-engine launches. This plan extracts the interpreter
into an engine-free module behind a 3-member seam and lands a headless xUnit charter encoding all
ten behaviours. It is a **behaviour-preserving refactor**: no anim behaviour, flag, or log line
changes.

Out of scope, deliberately: the role-shaped construction factories, the motion `pose(t)` split, and
`AnimProgram`/`CompiledAnim` test coverage (candidates 2–4 of the 2026-07-30 architecture review) —
each is its own future decision. Also out of scope: trimming the interpreter's narrative comments;
they move **verbatim** (any slimming is a separate, later judgement).

## Milestone goal

- `CSVM/src/Mech3/SequenceRunner.cs` exists: `ISequenceHost`, `EventDispatch`, `AnimInstance`,
  `SequenceRunner` — public, engine-free, joining the `AnimDefs`/`AnimProgram`/`CompiledAnim` layer.
- `AnimRuntime` implements `ISequenceHost` **explicitly**; its own interface grows by zero members.
- `CSVM.Tests/SequenceRunnerTests.cs` runs all ten documented interpreter behaviours headlessly
  under `dotnet test`, against hand-authored fixtures.
- `.\RunTests.ps1 -Perf` is green with 11/11 golden hashes identical and the hot-path interface-call
  overhead measured, not asserted.

**No behaviour change of any kind.** A golden hash that moves, a log line that changes, or a
`--debug-anim` verdict that flips means the move went wrong — revert and re-approach, don't patch
forward.

## Decisions (2026-07-30)

Settled in the grilling session that produced this plan.

| # | Question | Decision |
|---|---|---|
| 1 | Shape of the seam | **3-member interface `ISequenceHost`**: `bool Dispatch(AnimEvent, AnimDefinition, Node3D?, bool instant, out float duration)`, `bool EvaluateCondition(AnimData?, AnimDefinition, Node3D?)`, get-only `Action<EventDispatch>? OnEventDispatched` — the nullable-delegate property preserves the zero-cost null-conditional on the hot path. Constructor delegates rejected (contract stays anonymous, 3 loose delegates per site); abstract base rejected (`AnimRuntime : Node` forecloses it). |
| 2 | What moves, where | **`SequenceRunner` + `AnimInstance` + `ISequenceHost` + `EventDispatch`, one new file** `CSVM/src/Mech3/SequenceRunner.cs`. `AnimInstance` moves so its concurrent-sequences/removal loop is testable in its real home, not re-implemented in fixtures. Partial-class split rejected (types stay private — seam in name only). |
| 3 | Visibility | **Public types + explicit interface implementation** — matches the already-public engine-free layer; no `InternalsVisibleTo` (repo-first mechanism, and weaker than explicit impl). `Dispatch`/`EvaluateCondition` stay off `AnimRuntime`'s own surface. |
| 4 | Anchor type across the seam | **Keep `Node3D?` as an opaque pass-through; tests pass `null`.** The interpreter never dereferences it. Genericizing rejected: one adapter = hypothetical seam. |
| 5 | Test charter | **All ten documented behaviours**, named tests, hand-authored C# fixtures shaped like the original cases — never extracted game data. Smoke-only rejected: the extraction is the risky move and "later" has no forcing function. |
| 6 | Verification | **Full `.\RunTests.ps1 -Perf`** — goldens prove invisibility, in-engine suites prove the dispatch path, `-Perf` A/Bs the added interface call per event fire against `perf-history.jsonl`. |

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

### Wave A — the seam and its charter

1. ☐ Extract `SequenceRunner.cs`: the four types move, `AnimRuntime` implements `ISequenceHost` explicitly
2. ☐ The headless charter: `RecordingHost` fake + the ten documented behaviours as named xUnit tests

## Dependency and parallelism notes

Linear: A1 blocks A2 (the types are private until A1 makes them public). Both items touch
`AnimRuntime.cs`/`SequenceRunner.cs` — no parallel worktrees.

---

# Wave A — the seam and its charter

## A1 ☐ Extract `SequenceRunner.cs`: the four types move, `AnimRuntime` implements `ISequenceHost` explicitly

**Goal.** `CSVM/src/Mech3/SequenceRunner.cs` holds public `ISequenceHost`, `EventDispatch`,
`AnimInstance`, `SequenceRunner`; `AnimRuntime` satisfies the seam via explicit interface
implementation; the game is pixel-identical and perf-neutral.

**Evidence (confidence: traced).** The interpreter's three host touch points are
`rt.Dispatch(ev, inst.Def, inst.Anchor, instant: false, out duration)` (AnimRuntime.cs:3002),
`rt.EvaluateCondition(ev.Data.Obj("condition"), inst.Def, inst.Anchor)` (:3086), and
`rt.OnEventDispatched?.Invoke(new EventDispatch(...))` (:3008). `AnimInstance` is
AnimRuntime.cs:2928–2951, `SequenceRunner` :2965–3210, `EventDispatch` record :703 (consumed by
`UI/AnimLab.cs`, which reads it as `AnimRuntime.EventDispatch`). Besides ordinary playback
(`Start`, :841), the damage path constructs a throwaway runner too: `ApplyDamageStages`
(:2027–2045) builds an `AnimInstance` over `DAMAGE_SEQUENCE` and calls `host.Advance(this, 0f)` —
both construction sites take the new `ISequenceHost` view. `CSVM.Tests` reaches types via plain
project reference, no `InternalsVisibleTo` anywhere (verified by grep 2026-07-30).

**Approach.** Pure move, no rewrites: cut the two nested classes plus the record out of
`AnimRuntime.cs` into the new file; add `ISequenceHost` with the three members exactly as the
Decisions table spells them; change `SequenceRunner.Advance(AnimRuntime rt, …)` /
`AnimInstance.Advance(AnimRuntime rt, …)` parameters to `ISequenceHost`; add the explicit
implementations on `AnimRuntime` forwarding to the existing private methods. Update `AnimLab`'s
`AnimRuntime.EventDispatch` references to the new top-level `EventDispatch`. Every narrative
comment moves verbatim. Same turn: new `## src/Mech3/SequenceRunner.cs` entry in
`docs/architecture.md` (register the seam vocabulary there), trim the `AnimRuntime.cs` entry's
description accordingly, bump CLAUDE.md's module map count `src/Mech3/ (30)` → `(31)`.

**Model recommendation.** opus — the design is fully specified above, so the work is a careful
mechanical move; the care is in touching nothing else in a 3,500-line hot file.

**Verify.** `.\RunTests.ps1 -Perf`: build green, existing xUnit green, in-engine suites
(`DamageStages`/`DamageHd`/`DestructibleCensus`) unchanged, **11/11 golden hashes identical**, perf
A/B against `perf-history.jsonl` within noise. Goldens satisfy "seen it able to fail" — they are
pinned hashes that any visible drift breaks. Read `docs/verification.md` before reading the perf
numbers.

**⚠ Traps.** (1) `OnEventDispatched` must stay a *nullable delegate property* on the interface —
making it an interface method forces `EventDispatch` construction on every fire; the null-conditional
short-circuit at :3008 is the documented zero-cost contract. (2) Explicit implementation only —
adding public `Dispatch`/`EvaluateCondition` to `AnimRuntime` widens the exact interface this plan
exists to protect. (3) Do not "improve" the interpreter while moving it (the `_loopsLeft == -2`
sentinel, the `goto case "Elseif"`, the 256-fire guard all look refactorable and are all
load-bearing); A2's tests land *after* the move, so the move itself is protected only by the
goldens. (4) The comments' measured evidence (bowl-sign percentages, the Loop-Count-0 survey) moves
untouched — trimming is explicitly out of scope.

## A2 ☐ The headless charter: `RecordingHost` fake + the ten documented behaviours as named xUnit tests

**Goal.** `CSVM.Tests/SequenceRunnerTests.cs` encodes the interpreter's documented semantics as ten
named, headless tests; `dotnet test` becomes the first automated instrument for anim event
semantics.

**Evidence (confidence: traced).** Each behaviour is documented at its mechanism, all in the moved
file (post-A1 line numbers shift; anchors are the comments themselves): START_TIME gates the
carrying event, not its successor (`SetDue` doc — the bowl sign, 38% blank frames before / 0%
after); authored `Loop Count 0` = infinite (the 26-loop install survey in the `Loop` case);
instant-iteration loops yield once per frame while timed bodies restart immediately
(`_iterScheduledTime`); the trailing `Loop {Event 1.2}` inter-cycle offset is honoured (re-gate
after control flow); taken-branch fall-through to ENDIF and depth-aware `Scan`; `Else` with no `If`
degrades to running the branch (`Taken` doc); `"Animation"`/`"Sequence"` origins are absolute,
`"Event"`/null relative to `_base` = previous fire + duration; the 256-fires-per-frame guard;
`AnimInstance` runs sequences concurrently and removes finished runners (the C1 train doc).
`AnimSequence`/`AnimEvent` are public with public fields and parameterless construction
(CompiledAnim.cs:233, :258) — fixtures build directly in C#.

**Approach.** One file, one small `RecordingHost : ISequenceHost` (records every `Dispatch` call,
returns scripted handled/duration answers; scripted `EvaluateCondition` verdicts; optional
`OnEventDispatched` recorder). Fixtures are hand-authored sequences *shaped like* the documented
cases — bowl-sign strict pairs with only the first of each pair stamped, a train-shaped
`[SiScript, Loop{-1}]`, a waterfall-shaped instant poll loop. Drive with fixed `dt` steps and assert
on the recorded dispatch order/timing. Follow the `AnimDefsTests.cs` house style.

**Model recommendation.** opus — the fixtures encode subtle timing semantics; a botched fixture
that passes for the wrong reason is worse than no test.

**Verify.** `dotnet test` green with the ten new tests listed by name; then a full
`.\RunTests.ps1` to confirm nothing in-engine moved. Each test must be seen to fail once (flip an
expected timing/order value, watch it go red) before it counts — an assertion that cannot fail is
not an instrument.

**⚠ Traps.** (1) **No game data in fixtures** — the no-assets hard rule covers extracted event
lists; shapes from the comments, invented values. (2) Test through the real `AnimInstance`, never a
re-implemented advance loop — its reverse-iteration/removal semantics are part of the charter.
(3) Anchors are always `null` in tests; a `Node3D` cannot be instantiated without the engine, and no
interpreter semantic needs one. (4) The `RecordingHost` must let `Dispatch` return `false` for
control-flow kinds — returning `true` for `Loop`/`If` silently bypasses the entire branch logic and
the tests would pass while testing nothing.
