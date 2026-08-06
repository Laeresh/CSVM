# Engine-free suites — nine checks leave the windowed harness for `CSVM.Tests`

**HANDOFF DRAFT — grill before activating** (written 2026-08-06 from that day's architecture
review, candidate 4; decisions NOT yet settled). The first working session on this plan **must
start with a `/grilling` session with the user** (item A1) — the Decisions table below is empty
until then, and the checklist after A1 is provisional. When a session activates this plan, point
PROJECT_CONTEXT.md's "Current status" at it. Sibling handoffs:
[`PLAN-template-stage.md`](PLAN-template-stage.md),
[`PLAN-puffer-interface.md`](PLAN-puffer-interface.md).

This is a **seam relocation, not a redesign**: nine of `Suites.cs`'s ~31 in-engine suites touch no
live Node — they call `Probes.*` functions that take paths and return records (`Probes.cs` has
zero `GD.*` calls, measured 2026-08-06) — yet each run pays a *windowed* Godot launch
(`--headless` compiles no shaders, per the Testing entry) for arithmetic like
`TweenArrow(170°, −170°)`. The standing ⚠ in `architecture.md`'s Testing entry already endorses
the move: *"In-engine is the smaller half. Only checks that need a live Godot belong here;
anything that runs without the engine goes in `CSVM.Tests`."* The infrastructure exists:
`TestData.DataRoot` / `[ExtractedDataFact]` (`CSVM.Tests/TestData.cs`) for data-gated facts, and
the `Log.ConsoleSink` shim precedent (`CSVM.Tests/StuntRaceTests.cs` — "the real `GD.Print` …
crashes the whole test host outside the engine").

The nine, as classified on 2026-08-06 (body sizes then; **re-verify each at execution — this
table is a lead, not a fact**):

| suite | ~lines | blocker to clear |
|---|---|---|
| `stunt-gates` | 52 | `StuntMission.cs`: 8 `GD.Print` → `Log` (hand-edit) |
| `flight-envelope` | 31 | none — `FlightModel.cs` has zero `GD.*` |
| `gauge-colours` | 74 | none (pure statics) |
| `gauge-arrow-tween` | 53 | none (pure statics) |
| `weapons-defs` | 15 | none |
| `weapon-blast` | 21 | none |
| `markers-rig` | 14 | none |
| `loadout-bind` | 25 | `Loadout`/`StockLoadouts`: 1 print site (hand-edit) |
| `tex-dropin` | 64 | none claimed — re-verify `TextureArchive` use |

`stall-warning` (87 lines) is **excluded**: it is engine-bound only because `GaugeCluster :
Control` forces `new`/`Free()` — freeing it is the optional Wave B (the review's candidate 6:
plain `StallLamp`/`ArrowSweep` structs).

**The moved checks call the same `Probes.*` — nothing is re-implemented in either tier.** The
`weapons-fire` suite and everything needing a built plane/world stays in-engine untouched.

## Milestone goal

- The nine checks run under `dotnet test` in seconds with no GPU and no window; `Suites.cs` keeps
  only what needs a live tree.
- One source of truth per check survives: xUnit twins call the identical probe, and the in-engine
  copies are **deleted**, not kept.
- The flight-envelope Tune-rate tripwire still fails the build when the rates move
  (`RunTests.ps1` runs the units gate before the engine gate, so the tripwire fires *earlier*).

**No bulk `GD.Print` sweep.** The enabler conversions are hand edits at the ~9 named sites only —
verification SHELL-3 records a bulk text rewrite corrupting files here before.

## Decisions (unfilled — settle in A1's grilling session)

| # | Question | Decision |
|---|---|---|
| 1–7 | See the grilling agenda in item A1. | *(to be filled by the grilling session; this table then becomes the authority where prose disagrees)* |

## ⚠ Read this before implementing anything

| # | The claim to keep honest | What guards it |
|---|---|---|
| 1 | "The nine are engine-free — the table says so." | The table is a 2026-08-06 classification (grep for `WithWorld`/`ctx.Host`/`ctx.Camera`/`GD.`/`new Node`/`AddChild`/`.Free()`). `Suites.cs` is the highest-churn file in its cluster — re-run the classification per suite at execution; one that grew an engine touch stays put. |
| 2 | "Skip semantics are equivalent." | xUnit `[ExtractedDataFact]` *skips* without data; the harness throws `SuiteSkippedException`. Equivalent in spirit, but the "green because it checked nothing" analysis differs — the landing commit must state how a data-less machine reports, and `RunTests.ps1`'s summary lines change (units count grows, engine count shrinks). |
| 3 | "Converting prints is safe anywhere." | Only by hand, only at the sites the moved suites actually execute (SHELL-3, above). The `Log` migration is incremental **by decision** — do not widen it. |
| 4 | "The tripwire moved, so it still works." | "An unchanged number is not evidence unless you've seen it able to fail" — after moving `flight-envelope`, deliberately break a Tune rate locally and watch `RunTests.ps1` exit nonzero at the units stage, then revert. |

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

### Wave A — decisions, then the relocation

1. ☐ Grilling session: settle the Decisions table with the user; rewrite A2–A4 if the calls differ
2. ☐ Enablers, by hand: `StuntMission` 8 prints + the `Loadout`/`StockLoadouts` site → `Log`; re-classify all nine suites
3. ☐ The no-blocker suites move (per-suite xUnit twins calling the same probes; in-engine copies deleted)
4. ☐ `stunt-gates` + `loadout-bind` + `tex-dropin` move; tripwire failure-drill run and recorded

### Wave B — optional companion (if the grilling keeps it)

11. ☐ `StallLamp`/`ArrowSweep` structs inside `GaugeCluster`; `stall-warning` moves; the 7 testability-escape internals retire

## Dependency and parallelism notes

A1 blocks all. A2 → A3 → A4 is a chain; B11 independent after A1. **File ownership:**
`CSVM/src/Testing/Suites.cs`, new `CSVM.Tests/*` files, `CSVM/src/Flight/StuntMission.cs`,
`CSVM/src/Flight/Loadout.cs` (print site only), Wave B adds `CSVM/src/Flight/GaugeCluster.cs` +
`CSVM/src/Flight/FlightController.cs` (the gauge feed site). Contends with
[`PLAN-puffer-interface.md`](PLAN-puffer-interface.md) on `Suites.cs` — not in parallel worktrees
with it. No contention with [`PLAN-template-stage.md`](PLAN-template-stage.md).

---

# Wave A — decisions, then the relocation

## A1 ☐ Grilling session — settle the decision tree with the user

**Goal.** Every question below has a user-made call in the Decisions table; the checklist is
rewritten to match. No code before this lands.

**Approach.** Run `/grilling` with this agenda, one question at a time, recommendation first (the
FireControl session, 2026-08-06, is the model):

1. **Confirm the nine** after a fresh classification pass (⚠ #1) — any suite that grew an engine
   touch stays; any newly-free suite may join.
2. **Delete or keep the in-engine copies?** *Recommended:* delete — the one-source-of-truth ⚠
   ("never re-implement a check in a suite") decides this; a keep-both option rots.
3. **Naming/discoverability.** Do the xUnit twins keep the suite names (`stunt-gates` →
   `StuntGatesTests` or a `[Trait]`) so grep and the docs survive? *Recommended:* mirror the
   names in class names and reference the old suite name in each class doc.
4. **Skip semantics + reporting** (⚠ #2): what does a data-less machine print, and do
   `RunTests.ps1`'s stage summaries need a note that the counts moved? *Recommended:* a line in
   the landing commit message + the Testing entry, nothing more.
5. **Wave B in or out?** The `GaugeCluster` structs move `stall-warning` too and retire 7
   `internal static` escape hatches — but touch the live gauge path. *Recommended:* in, as its
   own goldens-identical item; the LOW ALT fixed blink stays un-generalised (its entry's ⚠
   forbids folding the ramp onto it).
6. **Enabler scope** (⚠ #3): exactly which print sites convert — list them in the Decisions row
   so A2 has a closed list.
7. **Order of the moves.** *Recommended:* no-blocker suites first (A3) so the pattern is proven
   before the enabler-dependent three (A4).

**Model recommendation.** high — user-interactive; it rewrites this plan.

**Verify.** Decisions table filled, each row naming its losing option.

## A2 ☐ Enablers, by hand

**Goal.** `StuntMission` and the `Loadout`/`StockLoadouts` print site log through `Log`, so plain
classes construct in the xUnit host; the nine-suite classification is re-run and recorded.

**Evidence (confidence: traced, but stale-able).** Site list as of 2026-08-06:
`StuntMission.cs` ~:177, :182, :184, :270, :394 (8 prints); `StockLoadouts.Load`'s
`GD.PushWarning`. Re-grep first.

**Approach.** Hand edits only (⚠ #3). `Log.ConsoleSink` in the tests, per `StuntRaceTests`'
existing pattern.

**Model recommendation.** medium, low effort — mechanical, but the SHELL-3 rule makes care the
point.

**Verify.** `.\RunTests.ps1` green (the engine suites still pass with the converted prints —
`stunt-gates` exercises `StuntMission` in-engine before A4 moves it).

## A3 ☐ The no-blocker suites move

**Goal.** `flight-envelope`, `gauge-colours`, `gauge-arrow-tween`, `weapons-defs`,
`weapon-blast`, `markers-rig` (and `tex-dropin` if A1's re-classification cleared it early) run as
xUnit facts calling the same `Probes.*`; their `Suites.cs` bodies and registrations are deleted.

**Evidence (confidence: traced).** The classification table above; `Probes.Markers` / `Weapons` /
`Loadouts` / `MipChains` / `FlightEnvelope` all take paths and return records.

**Approach.** One suite per commit or small batches — each commit leaves `RunTests.ps1` green
with the check present in exactly one tier. Data-gated ones use `[ExtractedDataFact]`.

**Model recommendation.** medium — mechanical translation against a proven pattern.

**Verify.** `.\RunTests.ps1` green; units count up by the moved facts, engine count down by the
moved suites, goldens untouched (nothing rendering changed).

**⚠ Traps.** ⚠ #1/#2. A suite body that quietly used `ctx` conveniences beyond data paths
(RequireData is fine — it maps to the skip gate) is the drift to catch.

## A4 ☐ `stunt-gates`, `loadout-bind`, `tex-dropin` move; the tripwire drill

**Goal.** The enabler-dependent suites move; then the failure drill: break a `FlightModel` Tune
rate locally, watch `RunTests.ps1` fail at the *units* stage, revert — recorded in the landing
commit message.

**Evidence (confidence: traced).** `FlightModel`'s ⚠: "the flight-envelope suite fails if the
Tune rates move" — the tripwire must keep failing the build after the move (⚠ #4).

**Approach.** As A3. The drill is not optional — it is the item's Verify.

**Model recommendation.** medium.

**Verify.** The drill, plus a data-less run of `dotnet test` (temporarily point `TestData` at an
empty root) showing skips, not passes.

# Wave B — optional companion

## B11 ☐ `StallLamp` / `ArrowSweep` — the gauge cues become plain structs; `stall-warning` moves

**Goal.** The stateful stall-lamp and arrow-sweep halves of `GaugeCluster` live in two plain
structs (`Set/Advance/Lit`, `Advance/Angle/Reset`); the 7 `internal static` testability escape
hatches retire; `stall-warning` and its 87 lines join the xUnit tier — and the 4-term feed
predicate in `FlightController` (`!_crashed && !halted && !_held && IsStallWarned()`) becomes
assertable if the grilling chose to move it with them.

**Evidence (confidence: traced).** `GaugeCluster.cs` ~:340–391 (the statics each carrying an
"Internal so the run-tests suite can assert" comment), the `Suites.cs` `StallWarning` body's
`new GaugeCluster`/`Free()` dance, and the entry's ⚠ block (sim-dt integration; `Reset` clears to
NaN; the LOW ALT blink stays a plain fixed blink — **do not generalise the ramp onto it**).

**Approach.** Structs first with the suite moved onto them, then the `GaugeCluster` internals
retire in the same commit. The "integrated, not read off a clock" rule becomes the struct's own
invariant — port statement order, don't rederive the ramp.

**Model recommendation.** medium.

**Verify.** `.\RunTests.ps1` green; goldens untouched; the moved test reproduces the CAP-06
constants (0.30 fd lamp, 643→296 ms ramp) exactly as the in-engine suite pinned them.

**⚠ Traps.** The entry's three ⚠ lines, verbatim. The `_held` exemption is documented behaviour
(the weapon lab), not an accident — a "simplified" predicate that drops it breaks the lab.
