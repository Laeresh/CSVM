# Engine-free suites — nine checks leave the windowed harness for `CSVM.Tests`

**ACTIVE** (written 2026-08-06 from that day's architecture review, candidate 4; decisions
settled the same day in A1's grilling session — the Decisions table below is the authority where
prose disagrees). Sibling handoffs:
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

The nine, **re-classified in A1's session (2026-08-06, evening)** — two verdicts changed from
the morning table:

| suite | blocker to clear | A1 verdict |
|---|---|---|
| `stunt-gates` | `StuntMission.cs`: 8 `GD.*` sites → `Log` (hand-edit; see Decision 6's closed list) | moves (A4) |
| `flight-envelope` | none — `FlightModel.cs` has zero `GD.*` | moves (A3) |
| `gauge-colours` | none (pure statics) | moves (A3) |
| `gauge-arrow-tween` | none (pure statics) | moves (A3) |
| `weapons-defs` | none | moves (A3) |
| `weapon-blast` | none | moves (A3) |
| `markers-rig` | none | moves (A3) |
| `loadout-bind` | first half pure `Probes.Loadouts`; **BL-294 (8faa6b1) appended a second half building real planes** (`PlaneBuilder.Build` → `Node3D`, `Free()` at `Suites.cs:833`) | **splits** (A4): probes half moves, fill-order half stays in-engine |
| `tex-dropin` | **engine-bound** — body works on Godot `Image` objects (`GetData`/`GetFormat`/`Flatten`, `Suites.cs:1349–1394`); `Image` is native-backed and crashes the xUnit host | **❌ stays put** — moving it would mean re-implementing the flatten check, the exact "never re-implement" trap |

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

## Decisions (settled 2026-08-06, A1 grilling session — authority where prose disagrees)

| # | Question | Decision |
|---|---|---|
| 1 | Confirm the nine after re-classification | **Seven clean movers** (`flight-envelope`, `gauge-colours`, `gauge-arrow-tween`, `weapons-defs`, `weapon-blast`, `markers-rig`, `stunt-gates`); **`loadout-bind` splits** — `Probes.Loadouts` half moves, BL-294 fill-order half stays in-engine with a narrowed description; **`tex-dropin` closed ❌** (engine-bound on Godot `Image`). *Losing option:* keep `loadout-bind` whole in-engine — would strand the `StockLoadouts` print conversion's beneficiary. ⚠ **Corrected in A4 (2026-08-06):** the `loadout-bind` split does not hold — re-classified at execution, `Probes.Loadouts` calls `StockLoadouts.Load()` (native `Godot.FileAccess`) and `PlaneBuilder.Build` for every plane, both engine-bound (verified empirically: an off-engine call crashes the xUnit host with `AccessViolationException` at the `FileAccess` step, per ⚠ #1's re-run-the-classification rule). `loadout-bind` stays whole in-engine, same as `tex-dropin`; only `stunt-gates` actually moved. See `docs/architecture.md`'s `Suites.cs` entry for the full evidence. |
| 2 | Delete or keep in-engine copies? | **Delete, same commit as each move.** For the split suite, delete only the moved half's lines. *Losing option:* keep-both transition period — double green, zero extra coverage (both tiers call the same probe), rots. |
| 3 | Naming/discoverability | **One xUnit class per suite, name mirrored** (`flight-envelope` → `FlightEnvelopeTests`), old kebab suite name verbatim in each class doc; one file per class in `CSVM.Tests/` root. **No `[Trait]`s** — nothing filters by trait. *Losing options:* one big file (kills suite-per-commit cadence); traits (machinery without a consumer). |
| 4 | Skip semantics + reporting | **No script/mechanism changes** — `RunTests.ps1` already prints per-stage skip + Unchecked lines for both tiers (`:377`, `:478`), and `[ExtractedDataFact]` skips with `NoDataReason`. Prose only: landing commits state the data-less behaviour; one line in the Testing entry noting counts shifted tiers. *Losing option:* a "moved suites" summary note in `RunTests.ps1` — machinery for a one-time event. |
| 5 | Wave B in or out? | **In**, as its own goldens-identical item, **scoped to `GaugeCluster` only** — the `FlightController` feed predicate stays untouched (the `_held` exemption trap lives there). *Losing option:* defer — leaves `stall-warning` as the lone pure-arithmetic suite paying a windowed launch, re-asking "why is this one different" every session. |
| 6 | Enabler scope (closed list) | **Exactly 9 sites:** `StuntMission.cs` `:177` (PushWarning), `:182`, `:184`, `:270`, `:394`, `:547`, `:556`, `:561`; `Loadout.cs:51` (PushWarning in `StockLoadouts.Load`). The `:547/:556/:561` trio was missing from the morning list. `PushWarning` → nearest `Log` level, resolved by A2 against `Log`'s actual API — not a scope widening. *Losing option:* any wider sweep (SHELL-3). |
| 7 | Order of the moves | **A3 → A2 → A4 → B11.** Pattern proven on no-blocker suites before any live-code edit; enablers land while `stunt-gates` still verifies them in-engine; B11 last so its review carries no relocation noise. *Losing option:* the checklist's original A2-first chain — front-loads the riskiest hand edits before the pattern justifying them is demonstrated. |

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

### Wave A — decisions, then the relocation (execution order: A1 → A3 → A2 → A4, per Decision 7)

1. ☑ A1 Grilling session: Decisions table settled 2026-08-06; checklist rewritten to match
2. ☑ A3 The six no-blocker suites move (per-suite xUnit twins calling the same probes; in-engine copies deleted)
3. ☑ A2 Enablers, by hand: the 9 sites of Decision 6 → `Log` (engine suites still verify them in-engine)
4. ☑ A4 `stunt-gates` moves + `loadout-bind` splits; tripwire failure-drill and data-less-run drill recorded
5. ❌ `tex-dropin` — closed in A1: engine-bound on Godot `Image` (see the classification table)

### Wave B — companion (kept by Decision 5)

11. ☐ B11 `StallLamp`/`ArrowSweep` structs inside `GaugeCluster`; `stall-warning` moves; the 7 testability-escape internals retire (`FlightController` untouched)

## Dependency and parallelism notes

A1 blocks all. Execution order A3 → A2 → A4 → B11 (Decision 7); A2 and A3 are independent of
each other, A4 needs both, B11 runs last by choice not dependency. **File ownership:**
`CSVM/src/Testing/Suites.cs`, new `CSVM.Tests/*` files, `CSVM/src/Flight/StuntMission.cs`,
`CSVM/src/Flight/Loadout.cs` (print site only), Wave B adds `CSVM/src/Flight/GaugeCluster.cs`
only (`FlightController.cs` excluded by Decision 5). Contends with
[`PLAN-puffer-interface.md`](PLAN-puffer-interface.md) on `Suites.cs` — not in parallel worktrees
with it. No contention with [`PLAN-template-stage.md`](PLAN-template-stage.md).

---

# Wave A — decisions, then the relocation

## A1 ☑ Grilling session — settle the decision tree with the user

**Done 2026-08-06.** All seven questions settled in the Decisions table above; checklist and
items A2–A4/B11 rewritten to match. The agenda below is kept for the record.

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

## A2 ☑ Enablers, by hand

**Goal.** `StuntMission` and the `Loadout`/`StockLoadouts` print site log through `Log`, so plain
classes construct in the xUnit host. Runs **after A3** (Decision 7) — the pattern is proven
before these hand edits land.

**Evidence (confidence: traced 2026-08-06 evening, A1's re-grep).** Decision 6's closed list:
`StuntMission.cs` `:177` (PushWarning), `:182`, `:184`, `:270`, `:394`, `:547`, `:556`, `:561`;
`Loadout.cs:51` (`StockLoadouts.Load`'s PushWarning). Re-grep first anyway — line numbers drift.

**Approach.** Hand edits only (⚠ #3), exactly the 9 sites. `PushWarning` maps to `Log`'s nearest
warning-flavoured level — resolve against `Log`'s actual API, don't add one. `Log.ConsoleSink`
in the tests, per `StuntRaceTests`' existing pattern.

**Model recommendation.** medium, low effort — mechanical, but the SHELL-3 rule makes care the
point.

**Verify.** `.\RunTests.ps1` green (the engine suites still pass with the converted prints —
`stunt-gates` exercises `StuntMission` in-engine before A4 moves it).

## A3 ☐ The no-blocker suites move

**Goal.** The six no-blocker suites — `flight-envelope`, `gauge-colours`, `gauge-arrow-tween`,
`weapons-defs`, `weapon-blast`, `markers-rig` — run as xUnit facts calling the same `Probes.*`;
their `Suites.cs` bodies and registrations are deleted in the same commits (Decision 2). Naming
per Decision 3: mirrored class names, old suite name verbatim in the class doc, one file each.
**Runs first** (Decision 7) — this item proves the whole pattern before any live code is touched.

**Evidence (confidence: traced).** The classification table above; `Probes.Markers` / `Weapons` /
`Loadouts` / `MipChains` / `FlightEnvelope` all take paths and return records.

**Approach.** One suite per commit or small batches — each commit leaves `RunTests.ps1` green
with the check present in exactly one tier. Data-gated ones use `[ExtractedDataFact]`.

**Model recommendation.** medium — mechanical translation against a proven pattern.

**Verify.** `.\RunTests.ps1` green; units count up by the moved facts, engine count down by the
moved suites, goldens untouched (nothing rendering changed).

**⚠ Traps.** ⚠ #1/#2. A suite body that quietly used `ctx` conveniences beyond data paths
(RequireData is fine — it maps to the skip gate) is the drift to catch.

## A4 ☑ `stunt-gates` moves; `loadout-bind` split disproven; the two drills

**Goal.** `stunt-gates` moves whole. `loadout-bind` splits per Decision 1: the `Probes.Loadouts`
half (bind counts, failure list) becomes an xUnit twin; the BL-294 pylon-fill-order half
(`Suites.cs:798–840`, needs built planes) stays in-engine as a slimmed suite whose description
narrows to the fill-order check. (`tex-dropin` left the plan in A1 — engine-bound.) Then the two
drills: **(1) tripwire** — break a `FlightModel` Tune rate locally, watch `RunTests.ps1` fail at
the *units* stage, revert; **(2) data-less** — run `dotnet test` with `TestData` pointed at an
empty root, see skips-with-reason, not passes. Both recorded in the landing commit message.

**Outcome (2026-08-06): the `loadout-bind` split does not hold — disproven at execution, per
⚠ #1's re-run-the-classification rule.** `stunt-gates` moved clean: `CSVM.Tests/StuntGatesTests.cs`
calls the same `StuntMission.Load`/`Update` the in-engine suite did, with a no-op `Log.ConsoleSink`
installed (mirroring `StuntRaceTests`) since `StuntMission.Load` now logs through `Log` (A2) and an
uninstalled sink falls through to a host-crashing `GD.Print`. But `loadout-bind`'s "pure
`Probes.Loadouts` half" turned out not to be pure: `Probes.Loadouts` calls `StockLoadouts.Load()`
(no explicit path — its default reads `res://data/stock_loadouts.json` through `Godot.FileAccess`)
and `PlaneBuilder.Build` for every stock plane, to resolve `Loadout.Bind`'s markers against a real
node tree. Verified empirically (the project's own standing method for this class of question — see
the `StuntRace` C7 precedent in `docs/HISTORY.md`): a scratch off-engine xUnit fact calling
`Probes.Loadouts` crashed the whole test host with an unmanaged `AccessViolationException`, at the
`Godot.FileAccess.FileExists` call inside `StockLoadouts.Load` — before `PlaneBuilder` was even
reached. There is no engine-free remainder to extract without re-implementing `Loadout.Bind`'s own
marker resolution off-engine, which the plan's own "never re-implement" trap forbids. `loadout-bind`
stays whole in `Suites.cs`, unchanged; only its registration position shifted up one slot when
`stunt-gates`'s entry was deleted below it. The Decisions table's Decision 1 is corrected above.

**Evidence (confidence: traced).** `FlightModel`'s ⚠: "the flight-envelope suite fails if the
Tune rates move" — the tripwire must keep failing the build after the move (⚠ #4).

**Approach.** As A3. The drills are not optional — they are the item's Verify.

**Model recommendation.** medium.

**Verify.** Both drills above, plus `RunTests.ps1` green with `stunt-gates` present in exactly one
tier (units, not engine) and `loadout-bind` unchanged in the engine tier (Decision 1 correction).

# Wave B — companion (kept by Decision 5)

## B11 ☐ `StallLamp` / `ArrowSweep` — the gauge cues become plain structs; `stall-warning` moves

**Goal.** The stateful stall-lamp and arrow-sweep halves of `GaugeCluster` live in two plain
structs (`Set/Advance/Lit`, `Advance/Angle/Reset`); the 7 `internal static` testability escape
hatches retire; `stall-warning` and its 87 lines join the xUnit tier. **Scoped to `GaugeCluster`
only (Decision 5):** the 4-term feed predicate in `FlightController`
(`!_crashed && !halted && !_held && IsStallWarned()`) stays untouched — the `_held` exemption
trap lives there, and no test for it exists yet to justify the second live file.

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
