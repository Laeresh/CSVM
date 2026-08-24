# Milestone 4 polish — ten small follow-ups

**ACTIVE PLAN** (written 2026-08-24). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan clears ten of the newest, smallest open items in `backlog.md`: the tooling and cleanup
follow-ups filed when `PLAN-flight-model-parity` closed (`BL-444`, `BL-446`, `BL-452`, `BL-455`,
`BL-458`), four bounded research or fidelity items from the same batch (`BL-449`, `BL-451`,
`BL-457`) plus the at-the-controls audio bug `BL-442`, and the perf-test flake `BL-417`. Selection
criteria: newest IDs first, open and unblocked, no item scheduled by
[`PLAN-M5-campaign.md`](../PLAN-M5-campaign.md), and each landable in one session.

Every item was re-verified still-open in this session against the record: `git log --all --grep`
for each ID returns only the filing commits (`8c4cd4f3`, `471f887c` for the `BL-44x`/`BL-45x`
batch), and each entry is still present in `backlog.md`. Code checks done this session: the
`farFromPlayer` branch is still in `AiControlLaw.cs:316`; `CSVM.Tests/ZzBaselineDump.cs` still
exists; `CheckCommentCaps.ps1:37` still roots on `git rev-parse --show-toplevel`; `FlightAudio.cs:208`
still passes `damageFrac > 0f`; `PerfSampleTests.cs:272` already carries the `BL-379` warm-up loop.

Deliberately excluded from the same batch: `BL-443` (reorders `FlightModel.Step` and moves every
envelope row), `BL-447` (nine independent edges, its own plan), `BL-450` (a fuel model no shipped
mission needs), `BL-453` and `BL-456` (open decode work), `BL-445` and `BL-448` (whole-envelope
coverage and a ceiling question, neither small), `BL-454` (owed playtest, needs the user's screenshots).

## Milestone goal

- The parity lane's loose ends are tidy: the eleven-airframe dump has a host-independent hash, its
  class is named as the permanent instrument it is, and the dead AI throttle branch is gone.
- `CheckCommentCaps.ps1` scans the worktree it is run from.
- `BL-266`'s text quotes only decoded shake numbers.
- Three ledger rows move from "unsupported" to decoded or ported: the negative `C_L` ceiling, the
  dead AI's frozen throttle, the per-contact camera shake.
- A graze no longer drops the engine to a sputter unless the original would.
- One unit flake cannot take the engine, golden and hitch stages down with it.

**No item in this plan touches the force path of `FlightModel.Step`.** `BL-443` is the one item
that would, and it is out of scope because it moves every asserted envelope row.

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

## Battery on the merged tree

Every item's own verification is in its section; this is the one run over all of them together, on
the branch with the nine landed items merged:

```
  PASS  build       2.6s  dotnet build CSVM.sln
  PASS  units      19.1s  2153 passed, 0 failed, 0 skipped of 2153
  PASS  engine    109.3s  94 passed, 0 failed, 0 skipped; engine errors clean
  PASS  goldens    96.1s  16 shot(s) hash-identical; gpu NVIDIA GeForce RTX 5080 / 1.4.341
  TODO  hitch      17.9s  clean: 0 hitch line(s); inject: 1 hitch line(s); awareness only
  result: PASS -- 244.9s total, exit 0
```

A4's cross-host claim re-checked here rather than taken from its own worktree: the eleven-airframe
dump from `--dump-flight=all` and from `FlightEnvelopeDump` both hash
`C3050CEB6485DB29A38C5FF55044066F44EE8F2492597149D7B4337341C45C85`, zero differing lines.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — tooling and cleanup

1. ☑ `BL-458` `CheckCommentCaps.ps1` resolves relative paths against the current worktree
2. ☑ `BL-446` Rename `ZzBaselineDump` out of the throwaway prefix
3. ☑ `BL-455` Delete `AiControlLaw.Throttle`'s dead far-from-player branch
4. ☑ `BL-444` A flight-dump hash that is the same on both hosts
5. ☑ `BL-452` Rewrite `BL-266` (b) and (d) against the decoded shake numbers
6. ☑ `BL-417` Re-verify the perf flake, and stop one unit flake from skipping the later stages

### Wave B — decode and port

11. ☑ `BL-449` Confirm the one-sided negative `C_L` ceiling, then port or record it
12. ☑ `BL-451` A dead AI's throttle and surfaces freeze at their last commanded values
13. ☑ `BL-457` Port the per-contact camera shake (block 5)
14. ☐ `BL-442` Decode what keys the damaged-engine swap and pitch, then port it

## Dependency and parallelism notes

Wave A items are independent and can run in parallel worktrees, with one exception: A2 and A4 both
edit `CSVM.Tests/ZzBaselineDump.cs` (A2 renames it, A4 changes what it prints), so run A2 first and
A4 after it on the renamed file. A5 edits only `backlog.md`; it contends with every item's closing
edit of the same file, so land it alone, not in a parallel worktree. A6 edits `RunTests.ps1` and
`PerfSampleTests.cs`, nothing else touches either.

Wave B items are independent of Wave A and of each other. B11 touches the lift law in
`FlightModel` and B13 touches `AircraftContactResolver`/`PlaneShake`; B12 touches
`AircraftLifecycle` and `AiControlLaw` (A3 also edits `AiControlLaw.cs`, so land A3 before B12).
B14 touches `FlightAudio`/`EngineAudioCurves`/`AiEngineAudio` and nothing else in the plan does.

---

# Wave A — tooling and cleanup

## A1 ☑ `BL-458` `CheckCommentCaps.ps1` resolves relative paths against the current worktree

**Goal.** Run from any worktree with a relative path (or no path), the script scans that worktree.

**Evidence (confidence: traced).** `CheckCommentCaps.ps1:37-38` sets `$root` from
`git rev-parse --show-toplevel`, which in a linked worktree returns that worktree's root only when
`$PWD` is inside it; the reported symptom (scanning the `flight-parity` worktree) says the
resolution lands elsewhere. `:101` strips `$root + '\'` from each resolved path, so a wrong `$root`
also breaks the relative-path display. Hook (7) in `.claude/settings.json` calls the script; check
what working directory the hook runs with.

**Approach.** Root on `(Get-Location).Path` first and fall back to `git rev-parse` only when `$PWD`
is not inside a repo; or resolve the explicit `$Path` arguments with `Resolve-Path` before joining.
Keep the script pure ASCII. Add a note to CLAUDE.md hook (7) only if the fix changes how the hook is
invoked.

**Model recommendation.** medium, low effort: a scoped script fix.

**Verify.** From `.claude/worktrees/<x>`, run `.\CheckCommentCaps.ps1 -Summary` and a relative-path
call; the file list names that worktree's paths. Run from `Z:\CSVM` unchanged. A `git commit` in a
worktree still runs hook (7) and reports against the worktree's files.

**⚠ Traps.** `git rev-parse --show-toplevel` in a worktree is correct when invoked from inside it;
find where the wrong directory actually comes from (the hook's cwd, or a relative `$Path` joined to
`$root`) before rewriting the root logic.

**Verified.** Two cwd bugs, both reproduced with a planted over-cap probe
rather than diagnosed by inspection: `$root` answered for the caller's cwd rather than the script's
tree (now `$PSScriptRoot`), and an explicit relative `$Path` reached `[IO.File]::ReadAllText`
unresolved, which follows the process directory `Set-Location` does not move (now `Resolve-Path`).
Hook (7) is unchanged. `-Summary` clean from both the worktree and the main checkout.

## A2 ☑ `BL-446` Rename `ZzBaselineDump` out of the throwaway prefix

**Goal.** The eleven-airframe dump class carries a name that says it is the ledger's permanent
instrument.

**Evidence (confidence: traced).** `CSVM.Tests/ZzBaselineDump.cs:13` defines the class; `:23` calls
`Probes.FlightEnvelopeAll`, the same probe `--dump-flight=all` reaches (`ProbeRunner.cs:263`).
References to update: `docs/verification.md:463` (INSTR-19), `ControlLimiterTests.cs:169`,
`FlightConstantInventoryTests.cs:288` (both cite it as the env-var output pattern), and
`docs/architecture.md`'s test-project entry if it lists the class. `docs/plans/*.md` are frozen
history and keep the old name.

**Approach.** Rename file and class (`FlightEnvelopeDump` or the name `architecture.md` uses for the
instrument), update the env-var name only if it carries the prefix, fix the live references above.

**Model recommendation.** medium, low effort.

**Verify.** `dotnet test` runs the renamed test; `Grep` for `ZzBaselineDump` hits only
`docs/plans/` and commit history.

**⚠ Traps.** A4 edits the same file; land this first.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

## A3 ☑ `BL-455` Delete `AiControlLaw.Throttle`'s dead far-from-player branch

**Goal.** `AiControlLaw.Throttle` has one path, the slewed one; `OpenLoopPlayerRange` and the
`playerPosition` parameter are gone.

**Evidence (confidence: traced).** `AiControlLaw.cs:312-323`: `farFromPlayer` fires beyond
`OpenLoopPlayerRange` (2000 m, `:98`) and sets `lever = want / stats.FdSpeed` open-loop. The
backlog entry says `FlightModel.FarFieldPlant` (beyond 1000 m horizontal) now owns the far-field
velocity-match, so an AI more than 2000 m from the player is already on the far-field plant and
never reaches this throttle. That "never reaches" is the claim to confirm, not assume.

**Approach.** Instrument first: assert at the branch (or a temporary `GD.Print`) through the
`--run-tests` AI suites and an Instant Action squadron run; if it never fires, delete the branch,
`OpenLoopPlayerRange`, the `playerPosition` parameter and its callers' argument. Update the
`AiControlLaw` entry in `docs/architecture.md` and `docs/org/aiControlLaw.md`.

**Model recommendation.** medium.

**Verify.** `.\RunTests.ps1` green; the AI-related unit suites unchanged; one Instant Action
squadron flight shows AI at range holding speed as before.

**⚠ Traps.** If the branch does fire (an AI far from the player but within the far-field plant's
1000 m horizontal band by altitude geometry), the item is a disproof and the entry is rewritten, not
deleted.

`AiPilot.PlayerPosition` (the only seam that could ever set `Steer`'s `playerPosition`) was never
assigned anywhere outside `AiControlLawTests`' direct calls, so the branch was unreachable through
any real AI path before this change, not merely shadowed by `FlightModel.FarFieldPlant`. A stderr
probe at the branch confirmed it: `dotnet test` fired it only from the two unit tests that passed
`playerPosition` explicitly, and it never fired across the in-engine `--run-tests=Ai` battery
(16 suites, including `ai-far-field-plant` holding an AI at 1200 m) or a 900-frame Instant Action
`dogfight_squadron` flight. The probe was removed after confirming the disproof. The one deleted
test that observed `SpeedCeiling` numerically was rewritten against the closed loop rather than
dropped, so the unit count holds at 2150 passed, 0 failed. The `--run-tests=Ai` suites re-ran clean
after the deletion (same 16 suites, all PASS).

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

## A4 ☑ `BL-444` A flight-dump hash that is the same on both hosts

**Goal.** One `--dump-flight=all` hash can be quoted across the Godot runtime and `dotnet test`.

**Evidence (confidence: traced).** `docs/verification.md:461-465` (INSTR-19): 9 of 1123 lines differ
by one unit in the last printed place on knife-edge samples, identical code. Two runtimes, so it is
float-mode or formatting, not the plant.

**Approach.** Find the 9 lines first (diff the two dumps). Prefer rounding the printed values one
digit short of where they diverge over pinning a float mode, since the dump is a comparison
instrument, not the plant. Then rewrite INSTR-19 to say the hash is host-independent and by what
means.

**Model recommendation.** medium.

**Verify.** Dump from both hosts, hash equal. The unit and engine golden checks that hash the dump
are re-pinned in the same commit with the diff attributed to the rounding alone.

**⚠ Traps.** Rounding too coarsely hides a real plant change; keep the resolution where a one-unit
change in an asserted row still shows. If the difference is in a computed intermediate rather than
the print, rounding the print may not remove it; say so and record the finding.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

## A5 ☑ `BL-452` Rewrite `BL-266` (b) and (d) against the decoded shake numbers

**Goal.** `backlog.md`'s `BL-266` quotes only the decoded impact and high-speed shake values.

**Evidence (confidence: traced).** `backlog.md:2010-2040`: (b) declares the impact sources' per-event
quantities TUNE stand-ins; (d) carries a dated narrative of the `high_speed` decode. The decode
that replaced them is `docs/org/shakes.md` (block table `:91`, reader `:119-133`) and
`docs/formats/shakes.md`.

**Approach.** Rewrite (b) and (d) to state what is decoded and what remains open, in the body
template's labelled form, with no dates or event narration (CLAUDE.md writing style). Anything in
(b) still a stand-in stays TUNE and says which capture would pin it.

**Model recommendation.** medium, low effort: prose against two decode pages.

**Verify.** The entry reads as current state; `git commit` passes hooks (4) and (5). No code change.

**⚠ Traps.** Do not delete the still-open (c) or the `[Owed-playtest]` tag. Land alone, since every
other item's closing commit also edits `backlog.md`.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

## A6 ☑ `BL-417` Re-verify the perf flake, and stop one unit flake from skipping the later stages

**Goal.** (1) A verdict on whether `AScopeAllocatesNothing` still flakes after the `BL-379` warm-up
loop. (2) `RunTests.ps1` runs the engine, golden and hitch stages even when the units stage fails,
still returning a red exit code.

**Evidence (confidence: traced).** `PerfSampleTests.cs:270-275` carries the warm-up loop landed for
`BL-379` (the 4872-byte flake); the 3984-byte reading predates it. `RunTests.ps1:355-360` and
`:409-412` skip later stages on a build failure only, so check whether a units FAIL actually
short-circuits or whether the backlog entry describes an older script; the entry says it does.

**Approach.** Run the unit suite twenty times (`dotnet test --filter AScopeAllocatesNothing`) and
the full battery three times; if zero flakes, close (a) and (b) as covered by `BL-379`. For (c),
let a units FAIL fall through to the engine stage while keeping the summary and exit code red;
document the ordering in the script header.

**Model recommendation.** medium.

**Verify.** Force a unit failure locally (temporary assert), run `.\RunTests.ps1`, see all stages
reported and exit 1; revert.

**⚠ Traps.** Do not loosen the zero-allocation assertion (backlog trap (a)). A fall-through must
not let a red units stage read as green in the summary block.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

`AScopeAllocatesNothing` did not flake once: 20 filtered `dotnet test --filter AScopeAllocatesNothing`
runs plus 3 full `.\RunTests.ps1` batteries (23 executions total) all passed, each battery's units
stage reporting `2150 passed, 0 failed, 0 skipped of 2150`, engine `94 passed, 0 failed, 0 skipped`,
goldens `16 shot(s) hash-identical`. (a) and (b) are covered by `BL-379`'s warm-up loop; the
zero-allocation assertion (`PerfSampleTests.cs:294`) is unchanged.

(c)'s premise did not hold against the current script: `git log -p -- RunTests.ps1` shows the
engine/goldens/hitch stages have only ever gated on `$buildOk` (a failed `dotnet build`), never on
the units stage's own result — the backlog entry described an older or misread script. Confirmed
behaviorally: a temporary forced-failure `[Fact]` in `PerfSampleTests.cs`, run through
`.\RunTests.ps1`, produced `FAIL  units  ... 2150 passed, 1 failed, 0 skipped of 2151` while `engine`,
`goldens` and `hitch` all still ran and reported their own non-zero counts, and the summary read
`result: FAIL in units -- 264.6s total, exit 1`. The temporary test was reverted; `git diff` on
`CSVM.Tests/PerfSampleTests.cs` is empty. No code change to the stage-gating logic was needed; the
ordering is now stated explicitly in `RunTests.ps1`'s header and in `docs/tooling.md`'s stage table.

# Wave B — decode and port

## B11 ☑ `BL-449` Confirm the one-sided negative `C_L` ceiling, then port or record it

**Goal.** The ledger row "the one-sided negative `C_L` ceiling"
(`docs/org/flightModel.md:3410`) reads decoded, with CSVM either matching the asymmetry or the
row recording why it is a decompiler artefact.

**Evidence (confidence: traced to the decompile, unconfirmed against disassembly).** `FUN_0041abd0`
clamps `C_L` to ±1.8 and applies `min(C_L, 0.5·FUN_0041ad80(M))` (`0.75 − 0.15M`) to positive lift
only, per the entry. CSVM's lift law is in `FlightModel` (see `docs/org/flightModel.md` for the
`C_L` section).

**Approach.** Read the disassembly of `FUN_0041abd0` via the Ghidra MCP (`disassemble_function`),
not only the decompile, and check the sign handling around the `min`. If asymmetric: port it to the
lift law behind the existing envelope tests, run `--dump-flight=all` before and after and attribute
every moved row (expected: inverted or pushed-over samples only). If symmetric: rewrite the ledger
row as decoded-matching.

**Model recommendation.** high: reading x86 float compare/branch sequences for sign handling.

**Verify.** `FlightEnvelopeTests` and `ParityLedgerTests` green; the A/B diff of the dump touches
only negative-lift samples. INSTR-19 applies (same-host comparison until A4 lands).

**⚠ Traps.** The shipped envelope scenarios fly positive lift, so an unchanged dump is not
evidence the port is right; add one inverted sample to the dump only if none exists.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

**Outcome: a disproof, no code change.** The disassembly confirms the `min` at `0x41ac5f`–`0x41ac7b`
is one-sided and carries no sign handling, but the asymmetry cannot be reached. Both call sites of
`FUN_0041abd0` build its `n` argument as the length of the demand vector (`0x48c821`–`0x48c852` and
`0x49122e`–`0x491236`: a sum of squares through the integer sqrt approximation, then `/ 9.82`) and
apply the resulting force along that vector's own normalised direction, so `n ≥ 0`, `C_L ≥ 0`, and
the `−5` G clamp and the `−1.8` floor are both dead. A pushover reaches the function as a positive
coefficient and meets the same `0.75 − 0.15·M` ceiling a pull does. `FlightModel` already has the
same structure (`LoadFactorDemand` is a `liftDir.Length()`, `:688`), so `LiftCapAt` capping both
signs at the positive ceiling is the decode, not a departure from it. The ledger row moves to
decoded, and the "one clamp asymmetry remains unported" paragraph in `docs/org/flightModel.md` is
rewritten as settled.

## B12 ☑ `BL-451` A dead AI's throttle and surfaces freeze at their last commanded values

**Goal.** During the three-second dead-hull flight, CSVM's lever and control surfaces stay where
the AI last wrote them, as `FUN_004b82d0` leaves them.

**Evidence (confidence: traced on the original, lead-only on CSVM).** The death function zeroes
neither the throttle command nor the surfaces (entry). On the CSVM side, `AircraftLifecycle.cs`
carries `WreckFalling`/`WreckLanding` state (`:18`, `:56`, `:119`, `:217`) but no throttle field,
so where the lever goes at death is undetermined; check whether the recovery arm or `AiControlLaw`
keeps writing after the handover.

**Approach.** Trace the death handover in `AircraftLifecycle` and the AI update loop; log the
lever and surface commands for the three seconds after a kill in an Instant Action ace mission.
If they already freeze, record it in the ledger and close. If something writes them (the recovery
arm, a zeroing on death), stop it.

**Model recommendation.** medium.

**Verify.** A `--run-tests` AI death suite (or a new unit on `AircraftLifecycle`) asserts the
lever is unchanged across the handover; one ace-mission kill watched at the controls for the
dead hull's motion.

**⚠ Traps.** The `[obj+0x384]` crashed-flag writers are `BL-456`, out of scope; do not fold that
decode in. A3 edits `AiControlLaw.cs` too, so land A3 first.

**Outcome: a disproof, no production code change.** The freeze falls out of the update order rather
than from any explicit hold: `FlightController` branches on `Crashed` before it reads `InputSource`
again, so nothing writes `_lastInput` until `Respawn` clears it, and `StepWreckFall` steps the model
with a copy of that frozen struct every tick of the fall (refreshing only `NearestHumanDistSqM`,
since the original re-tests the far-field boundary regardless of who is flying). `EndFlightSystems`
does not touch it either, and `AircraftLifecycle` needs no lever field because the handover is
`FlightController`'s alone. Measured on a real Instant Action kill: `throttle=1.000 pitch=0.000
roll=0.000 yaw=0.000` at the kill and the same at the handover 3.02 s later, the throttle holding at
exactly 1.000 rather than decaying being what rules out a hidden neutraliser. The `ai-wreck-fall`
suite now samples the command every frame and asserts bit-identical equality on all four channels,
against an `internal LastCommand` seam. Out of scope but worth knowing:
`ControlSurfaceAnimator`'s `animate` guard means an AI's control surfaces are never posed at all,
alive or dead, so a dead AI's held deflection is not visible.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

## B13 ☑ `BL-457` Port the per-contact camera shake (block 5)

**Goal.** Every resolved contact, grazes included, kicks the camera the way `FUN_0048d2c0` does at
`0x48d409`.

**Evidence (confidence: traced).** `docs/org/shakes.md:91` lists block 5 (`+0xf4`) with its only
kicker `FUN_0048d2c0` at `0x48d409`, one kick per collision contact, magnitude computed per
collision by that function (`:123-124`). `PlaneShake.cs` has the block machinery (`Kick` at
`:180`, per-source kick methods `:64-108`); `AircraftContactResolver.Resolve` (`:100`) is the
contact site, and `FlightModel`'s collision damage path (`Flight/CollisionDamage.cs`, the port of
`FUN_0048d2c0` per `docs/org/flightModel.md:2797-2800`) is where the magnitude already exists.

**Approach.** Decode the magnitude expression at `0x48d409` (the entry does not give it; read the
decompile around the call), add a `PlaneShake.ContactHit(...)` block-5 source wired from
`CollisionDamage`, and update the shakes ledger row to ported. Add the row to
`docs/org/shakes.md`'s block table.

**Model recommendation.** high: reading the magnitude out of the collision function.

**Verify.** A unit on `PlaneShake` asserting a contact kick of the decoded magnitude; a corner
graze at the controls (the `BL-120`/`PT-53` corner) now moves the camera. Full 8-chapter
`--freecam` regression unchanged.

**⚠ Traps.** Block 5 keeps the constructor's law constants (`shakes.md:133`); do not author a
`turbulence` def. `CollisionDamage.cs` gates on positive severity (`flightModel.md:2650`); confirm
whether a graze reaches `FUN_0048d2c0` at all before claiming a graze shakes.

**Verified.** Full battery on the merged tree (see "Battery on the merged tree" above).

The magnitude read off `0x0048d3cc`–`0x0048d409` is `min(speed · s · 0.03, 0.15)` radians: the true
airspeed at `obj+0x934`, the RAW severity cosine rather than the pair's cube, the literal `0.03` at
`0x006080c4` and the ceiling `0x3e19999a` / `0x006036a8`. Two guards stand over it, the object being
the player (`0x0048d3c4`) and its crashed flag `obj+0x384` being clear (`0x0048d3aa`), and nothing
else. The graze half holds: the only gate on the call itself is a positive severity cosine
(`0x48ed79` guarding `0x48ed8b`), so a graze kicks like any other contact, and at flight speed a
cosine around 0.05 already saturates the ceiling. No residue item was minted.

Ported as `CollisionDamage.ContactShake` feeding `PlaneShake.ContactHit`, carried across as
`ContactOutcome.ShakeMagnitude` (zero on an AI) and spent in `FlightController.PerformContact` under
the same not-crashed gate the impulse takes. Block 5 runs on the constructor's law (2 Hz, damp 4.5,
no sawtooth) held in `PlaneShake`; no `turbulence` def was authored. `PlaneShakeTests` pins the
saturated kick and the linear graze kick, `AircraftContactResolverTests` pins the human-pilot gate,
and both constants have inventory rows. The ledger row moves from unsupported to decoded.

⚠ **The kick's magnitude is a velocity in the original, not a displacement**: `FUN_0042be10` adds it
to the block's velocity accumulators and `FUN_0042bec0` integrates them, where `PlaneShake` models a
block as an envelope in radians directly. That is the same modelling gap the fire source carries and
it belongs to `BL-266(a)`, so how loud the kick reads is the one thing that still needs the user's
eyes at the controls (the `BL-120`/`PT-53` corner graze).

**Verified (this worktree).** `.\RunTests.ps1` PASS, exit 0: units 2153 passed / 0 failed of 2153,
engine 94 passed / 0 failed of 94 suites with errors clean, goldens 16 shots hash-identical, hitch
awareness-only. The 8-chapter `--freecam --det` regression (C1, C1B, C1C, C2, C2B, C3, C4, C5) exits
0 on every chapter with zero error lines.

## B14 ☐ `BL-442` Decode what keys the damaged-engine swap and pitch, then port it

**Goal.** The engine note drops to `snd_damagedengine` when the original would, and the pitch
multiplier is derived the way the original derives it.

**Evidence (confidence: traced on CSVM, lead-only on the original).** `FlightAudio.cs:208` calls
`UpdateEngineSlot(damageFrac > 0f, cockpitView)`, so any damage at all swaps the stream (`:383-409`);
`EngineAudioCurves.cs:68-69` draws the pitch uniformly in `[DamagedEnginePitchLo, Hi]`
(`PlaneStats.cs:280-287`). `AiEngineAudio.cs:180` mirrors the same gate for AI rigs. The original's
gate is undecoded; `docs/org/vehicleDamage.md:392` places the damage stages in the per-plane
runtime, the likely home.

**Approach.** Find the reader of the `snd_damagedengine` def in the executable (string xref to the
def name, then the caller that selects it) and decode the condition and the pitch expression. Port
the gate into `FlightAudio` and `AiEngineAudio` together, and the pitch law into
`EngineAudioCurves`. Nitro's engine variants share slot 0 (`git log --grep=BL-089`); check the
nitro swap survives.

**Model recommendation.** high: a fresh decode with a user-facing outcome.

**Verify.** `engine sound: slot 0 -> ...` prints (`FlightAudio.cs:408`) in a headless run with
scripted damage show the swap at the decoded threshold; a graze at the controls no longer
sputters. Unit on `EngineAudioCurves` for the pitch law.

**⚠ Traps.** No threshold by feel (entry). If the original also swaps on any damage and the
sputter is the pitch draw alone, the fix is the pitch law only; report that as the finding.
