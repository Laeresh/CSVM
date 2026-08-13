# Animation runtime — match the original interpreter

**COMPLETE** (written 2026-08-10, completed 2026-08-10). Archived under `docs/plans/`; all 15 items
landed except `B16`, closed ❌ disproven (the original's `RANDOM_WEIGHT` table has no run-time form
in the shipped data). `D32` re-baselined the three goldens B12/D31 already named, ran clean across an
883-unit / 32-suite / 13-golden `RunTests.ps1` pass and an 8-chapter `--freecam` sweep, and brought
`SequenceRunner.cs`'s `docs/architecture.md` entry back to the 3-`⚠` budget.

Everything CSVM knows about how an animation definition *executes* was inferred from the shipped
data — the C1 train, the bowl sign, the `LOOP 0` census, the 2,999-call `WAIT_FOR_COMPLETION`
survey. That inference is now checkable: `crimson.exe`'s interpreter has been located and read
(`FUN_004ecbb0` and the 47-slot dispatch table at `DAT_00727de0`, compiled from
`D:\zipper\gamez\zEffect\zeff_ani*.c`). This plan closes the gap between the inferred runtime and
the real one: record the decode in `docs/formats/`, fix the places where our semantics differ, add
the events we never implemented, and prove it with a timeline test over three ordnance bursts
(HE, flash, sonic) that between them exercise every divergence found.

**Out of scope:** the SI-script player, puffer emission physics, and the effect-template pooling
layer. Those sit *under* the interpreter and are unchanged by it — this plan touches how events are
scheduled, branched and dispatched, not what an individual handler renders. Also out of scope:
re-decoding any file format. mech3ax's event numbering was independently confirmed correct by the
exe's dispatch table (slot 29 is null in both), so no extraction change is expected.

## Milestone goal

- The sequence interpreter's control flow — `CALL_SEQUENCE`, `STOP_SEQUENCE`, `IF`/`ELSE`/`ENDIF`,
  `LOOP`, `START_TIME` — behaves as `crimson.exe` behaves, with each divergence either removed or
  recorded as a deliberate, justified one.
- Every event kind that actually appears in the shipped data has a handler or a documented reason
  it does not (today five kinds appearing 592 times fall silently to `default:`).
- `docs/formats/anim-definitions.md` describes the *decoded* runtime, not an inference from data,
  and says which parts were inferences that survived.
- A `--run-tests` suite plays `he_ground_effect`, `flash_effect` and `sonic_ground_effect` end to
  end and asserts their event timelines against the authored data.

**This plan does not change what any handler renders.** If an item's fix makes an effect look
different, that is a scheduling consequence and must be visible in the timeline test — a change to
a light range, a puffer parameter or a mesh is out of scope and belongs in `backlog.md`.

## Decisions (2026-08-10)

| # | Question | Decision |
|---|---|---|
| 1 | Match the original's quirks, or keep our "more correct" behaviour? | **Match, unless the divergence is a documented deliberate improvement.** The goal is stated as "functions like the original". A divergence that survives needs a one-line justification in `docs/architecture.md`, not silence. |
| 2 | The `SequenceRunner` loop-overshoot carry (`BL-237`) contradicts the original, which hard-zeroes the sequence clock on `LOOP`. Revert it? | **Keep the carry, record it as deliberate.** The original runs its own fixed update tick, so hard-zeroing quantises to *its* tick; we must pace against sim time at any step size. `BL-237` measured the cost of not carrying. B14 writes this down rather than reverting it. |
| 3 | Where does the ground truth live? | **`docs/formats/anim-definitions.md`**, with exe addresses cited. It is the existing home of the scheduling section; a second page would split the topic. |
| 4 | Test shape for the three bursts? | **An in-engine `--run-tests` suite**, beside `effects-census` / `wait-for-completion` in `Suites.cs` — not a new probe flag. The existing suites already own effect playback and the harness prints one PASS/FAIL table. |
| 5 | Re-baseline a moved golden per item, or once? | **Once, in D32.** Several items can move a golden; re-pinning per item churns the manifest and each re-pin quietly blesses whatever the previous one missed. The branch runs golden-red from B12 until D32 — that is the intended state, not a failure, and D32 owns explaining every moved hash before it re-pins. |
| 6 | Reproduce the original's stopped-sequence disable? | **No — filed as `BL-334`.** The original leaves a stopped sequence un-callable until the def resets; 123 definitions name one sequence in both a call and a stop, but whether any reaches the stop first is control flow a static census cannot settle. Implementing it would change all 123 to match a rule none is known to observe, and a sequence wrongly disabled fails silently. The instrument comes first. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "`LOOP` has a run-time form we fail to handle." | Census over 3,015 compiled defs: **1,018 of 1,018 `Loop` events carry `Count`; zero carry `RunTime`.** mech3ax also notes `LOOP_RUN_TIME (not in reader)`. The exe implements it (`004ebfd0`, `flags & 2`) but nothing ships it. B15 documents it as unreachable; it does not get built. |
| 2 | "An absent `start` means `Event + 0`." | Half right. The *encoding* is `Animation + 0.0` (mech3ax `common.rs` collapses that pair to `None`). It behaves as "as soon as the previous event completes" only because the exe evaluates a start gate exclusively after the previous event reports completion. The observable behaviour we shipped is correct; the stated mechanism was not. |
| 3 | "`ENDIF` pops a branch-state frame." | `004ec5d0` is `MOV EAX,2 / RET` — a no-op. The original's branch handling is entirely stateless; `IF` resolves its whole chain inside one handler call. Our `_branchTaken` stack is an implementation detail, not a model of the original. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, B12, B13, B15, C21 | Confirm the trace against the cited address, then implement. |
| **Direction sound, magnitude a judgement call** | B14, C22 | The *what* is settled; the *how much* is a judgement call — say so in the commit rather than inventing a number. |
| **Leads only — no mechanism yet** | B16 | Budget for investigation; this may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

**⚠ `extracted/` is absent from a worktree** (git-ignored). Read install data by absolute path
(`Z:\CSVM\extracted\...`); never link it in — see `CLAUDE.md` on junctions.

**⚠ A bare `.\RunTests.ps1` in this worktree is a green that measured nothing** — no `tools/godot`
and no `extracted/` means the in-engine suites and the golden hashes skip silently and the exit code
still says PASS. Set `$env:CSVM_DATA_ROOT="Z:\CSVM"` first and check the printed counts
(`verification.md` LOG-17). Every item in Waves B–D depends on this.

## What the data actually ships

Census over **3,015 compiled defs** across all 8 chapters' `cam_anim` + `mis_anim`
(`analysis/anim-interpreter-decode/anim_census.py`, re-runnable; its `FINDINGS.md` beside it is the
decode this plan works from):

**Event kinds present** (top of the distribution, and the tail that matters):
`ObjectActiveState` 8,928 · `CallSequence` 4,649 · `CallAnimation` 4,589 · `ObjectMotion` 3,051 ·
`PufferState` 2,774 · `ObjectMotionFromTo` 2,552 · `Sound` 2,457 · `If` 1,531 / `Endif` 1,531 ·
`LightState` 1,045 · `ObjectOpacityFromTo` 1,040 · `Loop` 1,018 · `Else` 977 · `StopSequence` 630 ·
`LightAnimation` 415 · `ObjectMotionSiScript` 348 · `Elseif` 344 · **`Callback` 288** ·
**`FbfxColorFromTo` 152** · **`ObjectCycleTexture` 96** · **`ObjectDeleteChild` 48** ·
**`CameraState` 8**.

The five bold kinds are the whole of what appears in the data and has no handler — **592 events**.
Everything else the exe's table can dispatch (`Effect`, `FogState`, `SoundAdjust`, `CameraFromTo`,
`ObjectConnector`, `CallObjectConnector`, `FbfxCsinwaveFromTo`, `DetonateWeapon`,
`ObjectMotionSiScriptAllNames`) has **zero occurrences** in the compiled scope and needs no work.

**The divergence census** — each number is an item's justification:

| Shape | Count | Where |
|---|---|---|
| `If` nested inside an open `If` | **96** | every chapter's `gunhit-*slug_gunhit` and `mag_gunhit-*` — played on every gun impact |
| `CALL_SEQUENCE` target called from >1 site in the same def | **77** | incl. `sonic_effect-sonic_ground_effect → sonic_light_seq ×2`, `ap_effect → ap_light_seq ×2`, `player-player → destroy_craft ×3` |
| `CALL_SEQUENCE` naming a non-`OnCall` sequence | **6** | `reflight1..6 → refinery_light_seq` (state `Initial`) |
| `STOP_SEQUENCE` on an `OnCall` sequence nothing calls | **16** | `flame_ball_01/02 → stop_p1trail`, all 8 chapters — **inside the HE explosion's call chain** |

**The interpreter, in `crimson.exe`** (image base `0x400000`):

| Piece | Address |
|---|---|
| Sequence stepper (one sequence, one tick) | `FUN_004ecbb0` |
| Sequence reset (rewind + re-arm) | `FUN_004ebfa0` |
| Event dispatch table, 47 slots, index 1–0x2f | `DAT_00727de0`, populated by `FUN_004ee1a0` |
| `IF` / `ELSEIF` condition evaluator | `FUN_004ec080` |
| `ELSE` / `ELSEIF` fall-through (scan to `ENDIF`) | `004ec5a0` |
| `ENDIF` | `004ec5d0` (no-op) |
| `LOOP` | `004ebfd0` |
| `CALL_SEQUENCE` | `004eb570` |
| `STOP_SEQUENCE` | `004eb610` |

The live sequence struct is mech3ax's `SeqDefInfoC` (64 bytes): `+0x20` current state, `+0x21`
reset state (`Initial`=0, `OnCall`=3), `+0x24` **sequence timer**, `+0x28` **event timer**, `+0x2c`
accumulated loop time, `+0x30` u16 loop counter, `+0x34` current event pointer, `+0x38` start
pointer, `+0x3c` byte size. mech3ax's field-name guesses at 36/40/44 (`loop_time` / `event_time` /
`seq_time`) are mis-ordered against this — A1 records the correction.

Handler return values drive the state byte: **2** = event complete (advance), **1** = still running
(re-dispatch the same event next tick), **4** = sequence rewound by `LOOP` (re-gate from the top,
and yield the rest of this tick). State **3** = parked, awaiting a call.

**The canonical worked example** is `Z:\CSVM\extracted\C1\cam_anim\he_ring-he_ground_effect.json` —
5 sequences, and its `frame_buffer_effects1` sequence (6× `FbfxColorFromTo`, 1.2 s, gated on
`If PlayerRange 10000`) is the single most visible thing we do not render today.

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

### Wave A — Ground truth

1. A1 ☑ Record the decoded interpreter in `docs/formats/anim-definitions.md`
2. A2 ☑ Correct the three claims the decode changes, and open the `PLAYER_RANGE` question

### Wave B — Interpreter semantics

11. B11 ☑ `CALL_SEQUENCE`: one instance per sequence, startable only from the parked state
12. B12 ☑ `STOP_SEQUENCE`: halt only — retire the stopper idiom
13. B13 ☑ `IF` skip: match the original's non-nesting-aware scan
14. B14 ☑ `LOOP`: align the rewind, keep the carry, write down why
15. B15 ☑ `START_TIME` origins: `Animation` reads the animation clock
16. B16 ❌ `RANDOM_WEIGHT`: the original's 200-entry shared table

### Wave C — Missing events

21. C21 ☑ `FBFX_COLOR_FROM_TO` — the full-screen flash
22. C22 ☑ Triage the remaining four unhandled kinds that ship (`Callback`, `ObjectCycleTexture`, `ObjectDeleteChild`, `CameraState`)

### Wave D — Proof

31. D31 ☑ `ordnance-burst-timeline` suite: HE, flash and sonic played end to end
32. D32 ☑ Full regression + docs sweep

## Dependency and parallelism notes

A1 blocks nothing but should land first — every later item cites it. A2 depends on A1.

Wave B items all edit `CSVM/src/Mech3/SequenceRunner.cs` and, for B11/B12, `AnimInstance` in the
same file. **Do not run B11–B15 in parallel worktrees** — they contend on one file. Run them in
listed order in one session; B11 and B12 are a pair (B12's stopper idiom exists to compensate for
B11's concurrent-runner model, so B11 must land first or B12's evidence will read wrong).

B16 is independent (`AnimRuntime.EvaluateCondition`) and may run in parallel with Wave C.

C21 and C22 edit `AnimRuntime.cs` only; they may run in parallel with each other if each owns its
`case` block, but both must land before D31.

D31 depends on all of Wave B and C21. D32 is last.

---

# Wave A — Ground truth

## A1 ☑ Record the decoded interpreter in `docs/formats/anim-definitions.md`

**Landed.** Extended "Consuming the extraction (playback, 2026-07-21)" in
`docs/formats/anim-definitions.md` with: an address table for the stepper, reset, dispatch table,
`IF`/`ELSE`/`ENDIF`, `LOOP`, `CALL_SEQUENCE`/`STOP_SEQUENCE` and `FBFX_COLOR_FROM_TO`; the
`SeqDefInfoC` field map (with the mech3ax offset-36/40/44 naming correction called out, left
unchanged in the fork per the trap below); the handler-return-value state machine (2/1/4, and
parked state 3, including which stepper states re-evaluate the start-time gate); and the opcode
table finding (47 slots match `EventType` exactly, including the null slot 29, plus four real
handlers at slots 38/43/44/45 that mech3ax leaves undecoded and nothing ships). The six existing
inference narratives (three `START_TIME` origins / start gates its own event / concurrent
sequences, `LOOP 0` infinite, the 1/60 tick, and the `WAIT_FOR_COMPLETION` next-event-gate census)
each now carry an inline "Decode status" line marking them confirmed (with the address) or, for the
null-`start` mechanism specifically, superseded — left for A2 to reword, not rewritten here.

**Verified.** Read `FUN_004ecbb0` (stepper) and `FUN_004ebfa0` (reset) in full — both match the
state machine and struct offsets exactly, including the three `START_TIME` origin codes and the
reset's `state ← +0x21` / `event ptr ← +0x38` / timers-to-0. Read `FUN_004ee1a0` in full — confirms
the dispatch table's 47 entries slot-for-slot, the null slot 29 (`_DAT_00727e54 = 0`), and real
handlers at 38/43/44/45. Confirmed `004ec5d0` is a bare `MOV EAX,2 / RET`. Read `FUN_004ec080` (the
`IF`/`ELSEIF` evaluator) in full — confirms the `RANDOM_WEIGHT` 200-entry table read/increment, the
`dist² * 4.0` comparison, and the non-nesting-aware scan to the first `Else`/`Elseif`
byte. (⚠ That `* 4.0` comparison was read here as `PLAYER_RANGE`'s and it is not — the flag word
was traced token-by-token on 2026-08-13 and it belongs to `PLAYER_LINED_UP`, an unauthored angle
condition. `PLAYER_RANGE` compares `dist² <= m²`. See `docs/formats/anim-definitions.md`'s
flag-word table and `git log --grep=BL-333`.) **Not independently re-checked this pass:** `LOOP` (`004ebfd0`) has no Ghidra-recognised
function boundary (it's reached only through the dispatch table) and was not disassembled here — its
use of struct offsets `+0x2c`/`+0x30` rests on the existing decode in
`analysis/anim-interpreter-decode/FINDINGS.md`, not on a re-check in this session. The
`SeqDefInfoC` field layout itself is taken from mech3ax as given, not independently re-derived
field-by-field.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** A reader of `anim-definitions.md` can tell which scheduling rules are *decoded from the
exe* and which remain inferences from the data, and can re-find any of it from a cited address.

**Evidence (confidence: traced).** The addresses and the struct/state-machine layout in "What the
data actually ships" above. The dispatch table's ordering matches mech3ax's `EventType` enum
including the null slot 29 — an independent confirmation the fork's numbering is right, worth
stating because it retires a standing doubt.

**Approach.** Extend the existing "Consuming the extraction (playback)" section rather than adding a
new one; it is already the scheduling home. Add: the handler-return-value state machine (2/1/4, and
parked 3), the sequence-struct field map with the mech3ax naming correction at offsets 36/40/44,
the opcode table, and a short note that the exe implements four slots (38, 43, 44, 45) mech3ax
leaves as gaps but which no shipped def uses. Keep the existing inference narratives — mark each
one **confirmed** or **superseded** rather than deleting it; the reasoning is why we trust the rest.

**Model recommendation.** medium — documentation of settled facts, no judgement calls.

**Verify.** `docs/formats/anim-definitions.md` renders; every address cited resolves in Ghidra; the
`⚠` budget in `docs/architecture.md` is untouched (this is a formats page, not a module entry).

**⚠ Traps.** Do not "fix" mech3ax's `SeqDefInfoC` field names in the fork as part of this item —
renaming a struct field there is a round-trip-test surface and belongs in its own change. Record the
correction in prose here.

</details>

## A2 ☑ Correct the three claims the decode changes, and open the `PLAYER_RANGE` question

**Landed.** Reworded the null-`start` and `LOOP 0` passages in `docs/formats/anim-definitions.md`'s
"Event scheduling" and `LOOP` bullets so the lead sentence states the mechanism the decode found
(`Animation + 0.0` encoding gated post-completion; the u16 counter that counts up to the authored
value) instead of the census that used to stand in for it — the censuses stay, now framed as
corroborating measurement rather than the sole justification. Folded A1's two "Decode status"
addenda for those passages into the prose directly rather than leaving them as bolted-on
corrections; the intro sentence to "The decoded interpreter" section was reworded to match. Left
the `WAIT_FOR_COMPLETION` and animation-frame-tick "Decode status" notes as separate markers — both
confirm without superseding, and each adds something (a weaker confirmation route for the former, an
unruled-out hypothesis for the latter) the surrounding prose doesn't already say, so folding them in
would have been churn. Fixed a second, unmarked restatement of the null-`start` claim
(`anim-definitions.md`'s `hg_splasher` paragraph, "Since an absent `START_TIME` is `EVENT_OFFSET 0`
…") found by sweeping the rest of the file. Swept `docs/architecture.md`, `docs/verification.md`,
`docs/formats/*.md` and this plan for the other two superseded claims (`ENDIF` popping branch
state; `LOOP 0` infinite "because a census says so") and found no further restatements — the one
`ENDIF`/`_branchTaken` mention in `architecture.md`'s `SequenceRunner` entry describes CSVM's own
implementation, not a claim about the original, so it needed no change. Opened `backlog.md`
`BL-333` `[Research]` under "Effects & animation runtime" for the `PLAYER_RANGE` `* 4.0` factor,
naming `FUN_004ec080`, the untraced `FUN_0053f610`/`FUN_0053f9b0`/`DAT_009fd190`/`FUN_0053fca0`
chain, the 1,052 shipped conditions, and a ⚠ Traps line against halving radii on the multiply alone.
(`BL-333` was settled and closed on 2026-08-13: the chain is angular, the `* 4.0` belongs to the
unauthored `PLAYER_LINED_UP` condition, and no radius moves. The trap held.)

**Verified.** `grep` for `Event \+ 0` and `EVENT_OFFSET 0` across `docs/formats/anim-definitions.md`
returns only the two corrected sentences (the encoding they now name is `Animation + 0.0`, stated as
the correction, not the claim). No other `docs/*.md` file restates any of the three superseded
claims. `BL-333` has an ID minted by `New-ItemId.ps1 -Kind BL`, placed in its theme's ID order as
observed in the surrounding entries (append-at-end-of-theme, matching neighbours).

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** No live doc states a mechanism the exe contradicts.

**Evidence (confidence: traced, except the last).** The disproven-claims table above. Additionally,
`FUN_004ec080`'s `PLAYER_RANGE` branch compares `dist² * 4.0 <= value` — if the vector reaching that
comparison is not already halved by the `FUN_0053f9b0`/`DAT_009fd190` transform chain, then a
compiled `PLAYER_RANGE 72900` (reader 270) fires at **135 m, not 270 m**, and every gated effect in
the game has half the radius we give it. **This is a lead, not a finding** — the transform chain is
untraced.

**Approach.** Edit the `LOOP 0` and null-`start` passages in `anim-definitions.md` to state the
mechanism rather than the census that stood in for it (keep the censuses — they are still the proof
that nothing else uses the shape). Add a `backlog.md` `[Research]` item for the `PLAYER_RANGE`
factor, naming `FUN_004ec080` and the untraced chain, so it does not ride this plan.

**Model recommendation.** medium.

**Verify.** `grep` for the old phrasings across `docs/` returns nothing stale; the new backlog item
has an ID from `New-ItemId.ps1`.

**⚠ Traps.** Do not act on the `PLAYER_RANGE` factor in this plan. Halving every gate radius on an
untraced multiply is exactly the kind of change that looks like a fidelity win and is a regression;
it needs the trace or an at-the-controls capture first.

</details>

---

# Wave B — Interpreter semantics

## B11 ☑ `CALL_SEQUENCE`: one instance per sequence, startable only from the parked state

**Landed.** `AnimInstance.CallSequence` (`CSVM/src/Mech3/SequenceRunner.cs`) now starts a sequence
only when nothing is already running it **and** its authored activation is `ON_CALL`; both other
cases are silent no-ops that still return *found*. Runner identity is the `AnimSequence` **object**,
matched by reference in a new `AnimInstance.IsRunning(AnimSequence)` over the existing
`List<SequenceRunner>`, with `SequenceRunner.Sequence` exposed to make the match possible.

**The `List<SequenceRunner>` was kept rather than replaced by a map.** A dictionary keyed on the
sequence buys nothing here: instances hold a handful of runners, so the linear scan is free, and the
list is the only thing that fixes *advance order*, which a `Dictionary` does not guarantee — swapping
it would have put every golden at risk for no semantic gain. It also keeps `AnimInstance.Finished`,
`AnimRuntime.Retirable` and `MotionSet.OwesBounce` reading exactly what they read before, which is
why none of the three needed a change: they ask "is any runner still live", and that question is
unaffected by how many runners a call may add.

**No new `seq_state` field was needed** — the plan expected one, but `AnimSequence.OnCallOnly` is
*already* the single field both spellings feed (`CompiledAnim.cs` from `seq_state == "OnCall"`,
`AnimDefs.cs` from `ACTIVATION ON_CALL`), so the gate reads it directly. Gating on `OnCallOnly` alone
covers every `Initial` case the original's state byte does: an `Initial` sequence is never parked at
state 3, whether it is mid-run (state 0/1), finished (state 2) or not yet started.

**`StopSequence`'s tail no longer routes through `CallSequence`.** The stopper idiom's fall-through
now adds its runner directly, so the idiom's behaviour is byte-identical to before this item and its
retirement (B12) stays a separate, attributable change. Nothing else in `StopSequence` — the idiom,
`_everStarted`, `LaunchesContactTestedBody` — was touched.

**Two new units and one fixture correction** in `CSVM.Tests/SequenceRunnerTests.cs`: a second call
into a running sequence starts no second copy, and a call naming a non-`ON_CALL` sequence starts
nothing yet returns *found*. The correction is in the `Instance(defined, run)` builder, whose own doc
comment already said the sequences it does not start "sit ON_CALL" while never setting the flag —
`StopSequenceWithNoRunningTargetCallsItLikeCallSequence` was the one test that noticed, and it
noticed correctly.

**Verified.** `.\RunTests.ps1` (with `CSVM_DATA_ROOT=Z:\CSVM`, since a worktree has no `tools/godot`
or `extracted/`) before the change: PASS — 879 units, 30 in-engine suites, **13 goldens
hash-identical**. After the change: PASS — 881 units, 30 suites, **all 13 golden hashes unmoved**.
The suites
the item put at risk (`stop-sequence`, `wait-for-completion`, `effects-census`, `bounce-launch`,
`ground-contact`, `self-ref-launch`, `nulled-launch`) all pass. That the goldens did not move is the
expected result and not a weak one: `c1-destroy-effects` and `c1-crash` are single frames, and the
77-def doubled-call divergence is a *timeline* difference that D31's suite is what will actually
assert.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** Calling a sequence that is already running does nothing; calling a sequence whose
`seq_state` is `Initial` does nothing. A def never runs two copies of one sequence.

**Evidence (confidence: traced).** `004eb570`: resolve the name to an index in the def's own
sequence array (stride 0x40), cache it into the event, then `if (seq->state == 3) seq->state = 0;`
and nothing else. State lives in the def, so there is exactly one instance per sequence.
`AnimInstance.CallSequence` (`SequenceRunner.cs`) instead appends a second concurrent
`SequenceRunner` and documents duplicates as legitimate. **77 defs** call some sequence from more
than one site, including `sonic_ground_effect → sonic_light_seq ×2` — the sonic burst in D31 will
show the doubled light pulse directly. **6 defs** call an `Initial` sequence
(`reflight1..6 → refinery_light_seq`), which in the original is a silent no-op.

**Approach.** Give `AnimInstance` a per-sequence runner map keyed by sequence name instead of a
flat `List<SequenceRunner>`, and have `CallSequence` start a sequence only when it has no live
runner *and* its `seq_state` is `OnCall`. That needs `AnimSequence` to carry `seq_state` — today
`AnimDefs.cs` collapses it to `OnCallOnly`, which is the same bit for the compiled path but is
parsed separately for readers; keep both spellings feeding one field. The bootstrap that starts
non-`ON_CALL` sequences is unchanged.

**Model recommendation.** high — it changes the identity model the rest of the runner assumes, and
`AnimInstance.Finished` / `MotionSet.OwesBounce` retirement reads off the runner list.

**Verify.** `--run-tests` full pass, with attention to `stop-sequence`, `wait-for-completion`,
`effects-census`, `bounce-launch`, `ground-contact`, `self-ref-launch`. Then the C1 golden set —
`analysis/goldens/manifest.json`. A moved golden here is a real behaviour change and must be
explained, not re-baselined silently.

**⚠ Traps.** The empty sequence name is not unique — `he_ground_effect` has **two** unnamed
`Initial` sequences. Key the map by identity or index, not by name, or one of them disappears.
`AnimInstance.CallSequence` returning "the def has that sequence" is read by callers to decide
whether a name resolved at all; a no-op call must still report *found*, or `CALL_ANIMATION`
fallbacks will misfire.

</details>

## B12 ☑ `STOP_SEQUENCE`: halt only — retire the stopper idiom

**Landed.** `AnimInstance.StopSequence` (`CSVM/src/Mech3/SequenceRunner.cs`) now halts every runner
matching the name and returns whether the name resolved — nothing else. The stopper idiom is gone,
and with it `_everStarted` (the instance-wide "has this name ever run" set) and
`LaunchesContactTestedBody` (the `do_intersections` exception `PLAN-ground-contact` B7 added to
bound the idiom's damage); `AddRunner`'s doc comment, which existed to explain the bookkeeping
`_everStarted` needed, was rewritten to say what it now does. `AnimRuntime`'s `StopSequence`
dispatch comment was corrected to match. The return contract is unchanged: false only when the name
matched neither a runner nor a sequence.

**The disable is not persisted, and that is a stated residual divergence.** The original writes the
sequence's state byte to *done*, which — because `CALL_SEQUENCE` starts only from *parked* — makes a
stopped sequence un-callable until the definition resets. Reproducing that needs a per-sequence
stopped set, i.e. re-adding exactly the kind of instance-wide state this item deleted, and a census
run for this item found **123 definitions** that name one sequence in both a call and a stop (mostly
`flame_light_seq`, plus `chuteman_drop`/`chuteman_sway`, the `sail_splash*`/`yacht_splash*` sets and
`warhawk`'s `smokepuff1..3`). Whether any of them reaches the stop *before* the call at run time is
a control-flow question the static census cannot answer, so persisting the flag would change all 123
on a divergence none is known to observe. Recorded in the `StopSequence` doc comment, in
`docs/architecture.md`'s `SequenceRunner` entry and in `anim-definitions.md`, and filed as
**`BL-334`** with the instrument that would settle it (Decision 6).

**The `p1hit`/`piece1seq` loop did not reproduce.** `ground-contact` passes with the exception
deleted, which is the expected result — B11 removed the concurrent-runner model the loop depended
on, and with no start path at all the stop cannot relaunch a landed piece in the first place.

**Two tests changed, both at the assertion, not around it.** The unit
`StopSequenceWithNoRunningTargetCallsItLikeCallSequence` became
`StopSequenceWithNoRunningTargetStartsNothingAndLeavesItUncallable`: the stopper's `off1`/`off2`
must never fire, and the stop must still report *found*. The in-engine `stop-sequence` suite's
fireball half asserted `stop_p1trail` dispatching its `PUFFER_STATE` at the authored 0.3 s; it now
asserts `stop_p1trail` dispatches **nothing**, with the fireball's own puffer events as the
still-alive control so the check cannot pass on an effect that never started. The 30 s fire's halt
half is untouched — that is the idiom that *is* the original's.

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM` before the change: PASS — **881 units,
30 in-engine suites, 13 goldens hash-identical**. After: **881 units pass, 30 suites pass, engine
errors clean, 12 of 13 goldens hash-identical and `c1-destroy-effects` moved**
(`fa27f0cd… → 00ab194f…`). `analysis/goldens/manifest.json` was deliberately **not** re-baselined.
Before/after renders of the moved shot are in `.scratch/b12-goldens/`. The move is **4 pixels of
921,600** (0.0004 %), in a 3×2 patch at (909,289), each channel changed by 1–3/255: the fogged
far-distance haze above the airfield where the destroyed `ap_radiotwr`'s `great_balls_of_fire`
smoke sits, ~1.5 km out. Nothing in the frame is visibly different.

**⚠ What that pose cannot show.** `c1-destroy-effects` is one camera at frame 120 of a kill whose
effect is at the horizon — it can only ever report that the burst still renders, not *what* the
change did. The behaviour this item removed is a teardown (`PUFFER_STATE INACTIVE` +
`OBJECT_ACTIVE_STATE INACTIVE`) that used to fire 0.3 s into `flame_ball_01`; deleting it changes an
effect's **lifetime**, and a single pinned frame is exactly the instrument blind to that. A 4-pixel
move is therefore evidence that the pose is insensitive here, not evidence that the change is small.
The assertion that actually carries this item is the `stop-sequence` suite's timeline, and D31's
`ordnance-burst-timeline` is what will measure the burst end to end.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** `STOP_SEQUENCE` halts matching runners and does nothing else. A stop naming a parked
`ON_CALL` sequence leaves it un-callable until the def resets, as the original does.

**Evidence (confidence: traced).** `004eb610` resolves the name exactly as `CALL_SEQUENCE` does and
then unconditionally writes `seq->state = 2` (done). There is no start-if-not-running path. Because
`CALL_SEQUENCE` only starts from state 3, a stopped parked sequence is permanently disabled until a
def reset restores `seq_state`. Our `AnimInstance.StopSequence` does the opposite: with nothing
running it *calls* the sequence. **16 defs** hit exactly this case —
`flame_ball_01/02 → stop_p1trail`, in all 8 chapters, and `large_fireball` is called by the HE
explosion, so D31 covers it.

**Approach.** Reduce `StopSequence` to halt-matching-runners, and delete `LaunchesContactTestedBody`
and `_everStarted` along with the idiom they guard. Both exist only to bound the idiom's damage.

**Model recommendation.** high — the current code carries an explicit warning that removing the
idiom moves the `c1-destroy-effects` golden.

**Verify.** Run the golden set *before* the change to take a baseline, then after. If
`c1-destroy-effects` moves, capture both and decide from the images whether the new behaviour is the
original's; a moved golden is the expected outcome here, not a failure, but it must be looked at.
Then `--run-tests`, especially `ground-contact` (the `player_crash_dirt` `p1hit` loop the idiom's
exception was added for — B11 should have removed the loop's cause).

**⚠ Traps.** The `p1hit`/`piece1seq` re-launch loop documented in `StopSequence` is a symptom of the
concurrent-runner model, not of the idiom. Land B11 first; if the loop still reproduces after B11,
stop and re-diagnose rather than re-adding a special case.

</details>

## B13 ☑ `IF` skip: match the original's non-nesting-aware scan

**Landed.** The depth counter is gone from `SequenceRunner.Scan`
(`CSVM/src/Mech3/SequenceRunner.cs`). `NextBranch` now returns the first `Else`/`Elseif`/`Endif`
after the failed condition and `SkipToEnd` the first `Endif` after a fall-through — the two stop
sets stay separate, matching `FUN_004ec080` and `004ec5a0` respectively. `_branchTaken` was kept.

**The two readings disagree, on 48 shipped sequences, and the hand trace is the evidence.** A census
over all 3,015 compiled defs found nesting in exactly **48 sequences** — every chapter's
`gunhit-*slug_gunhit` and `mag_gunhit-*`, played on every gun impact — and all 48 have the *same*
shape (`C1/cam_anim/gunhit-3040slug_gunhit.json`, sequence 5):

```
 0 If AnimationLod 2      1 If PlayerRange 1000000      2 If RandomWeight 0.2
 3 LIGHT_STATE gunhit_lt (0.5..21.25)   4 OBJECT_ACTIVE_STATE gunhit_lt off (Event + 0.0001)
 5 Elseif RandomWeight 0.2
 6 LIGHT_STATE gunhit_lt (0.9..12.25)   7 OBJECT_ACTIVE_STATE gunhit_lt off (Event + 0.0001)
 8 Else   9 Endif   10 Else   11 Endif   12 Endif
```

Both `ELSE` bodies are empty, and that is what exposes the depth counter. With the LOD gate false:

| | old (depth-aware) | new / `crimson.exe` |
|---|---|---|
| `IF` @0 false → | scan skips the nested chain → **12** (the outer `Endif`) | scan breaks on the first marker → **5** (the inner `Elseif`) |
| then | `Endif` @12, `_pc`=13, sequence over | re-tests `RandomWeight 0.2`: on a hit fires **6, 7**; on a miss lands @8 |
| fires | **nothing, ever** | **the dim impact light, on a 20 % roll** |

The `PlayerRange 1000000` (1 km) gate behaves identically: false, the scan lands on the same inner
`Elseif` and re-tests it. So the original lights a `gunhit_lt` point source at any range and at any
LOD setting, one impact in five, and CSVM lit none. All four remaining combinations (both gates
true, inner weight true/false) were traced and **agree** between the two readings.

Per Decision 1 this is recorded, not fixed: it reads like a compiler bug in the original's
`zeff_ani` emitter and it is what the original does.

**One residual, unreachable in the install.** `_branchTaken` is a stack; the original has no state
at all and distinguishes a fall-through from a landing by *arrival* — a marker the stepper walked
into is dispatched (skip to `ENDIF`), a marker a failed scan landed on is stepped past unread.
Those coincide on the shipped shape (traced above, all six combinations), but not on a chain whose
inner `IF` closes **before** the outer chain's next marker (`If … If … Endif … Elseif …`): there
the landing pops the only open frame, and a following `ELSE` body runs that the original would
skip. No shipped def has that shape — the census found one shape, 48 times. Recorded in `Scan`'s
doc comment and asserted, deliberately, in
`BranchesFallThroughToEndifAndFailedConditionsAdvanceWithoutCountingDepth` so it is visible rather
than buried.

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM` before the change: **881 units, 30
in-engine suites, 12 of 13 goldens hash-identical**, `c1-destroy-effects` moved — B12's known
golden-red, Decision 5. After: **882 units, 30 suites, engine errors clean, and the same 12 of 13
goldens hash-identical with `c1-destroy-effects` at the same
`00ab194f…`**. **No golden moved for this item.** New unit
`GunhitNestedChainFiresInTheOriginalsOrder` is built from the real `3040slug_gunhit` event list and
asserts the exact firing order over all six condition combinations; it and the reworked
depth-counter unit were **both shown able to fail** by temporarily restoring the depth counter,
which turns both red.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** A false `IF` and a fall-through `ELSE` land where `crimson.exe` lands them, including
inside nested chains.

**Evidence (confidence: traced).** `FUN_004ec080`, on a false condition, advances event-by-event and
breaks at the **first** byte equal to 0x20 (`Else`), 0x21 (`Elseif`) or 0x22 (`Endif`) — no depth
counter. `004ec5a0` (the `ELSE`/`ELSEIF` fall-through) likewise scans to the first 0x22. Our
`SequenceRunner.Scan` counts nesting. **96 nested `If`s ship**, all in the `*_gunhit` / `mag_gunhit`
defs — played on every gun impact, so this is not a corner case.

**Approach.** Drop the depth counter from `Scan`. Keep `_branchTaken` — it is our stand-in for the
original resolving a whole chain inside one handler call, and A1's note says so — but confirm the
two agree on a nested chain by tracing one `gunhit` def by hand before and after.

**Model recommendation.** high — the change is three lines, the reasoning is not, and it can silently
alter which branch of 96 shipped chains runs.

**Verify.** A unit in `CSVM.Tests/SequenceRunnerTests.cs` built from the real `3040slug_gunhit`
event list, asserting the exact firing order. Then `--run-tests` and the golden set.

**⚠ Traps.** "More correct" is the wrong axis here (Decision 1). If matching the original makes a
`gunhit` chain run a branch that looks wrong, that is the original's behaviour and it stays —
record it, do not fix it.

</details>

## B14 ☑ `LOOP`: align the rewind, keep the carry, write down why

**Landed.** Replaced `SequenceRunner`'s down-counting `_loopsLeft` (initialised once via
`authored == 0 ? -1 : authored`, then decremented) with an up-counting `_loopPasses` that mirrors
`004ebfd0` directly: read `authored` fresh off the event every visit (the exe re-reads its own event
struct too, nothing is cached), increment `_loopPasses`, and terminate when `authored != -1 &&
_loopPasses == authored`. `0` is infinite as a **consequence** of that shape — the counter starts at
0 and only grows once a pass has run, so it can't re-equal 0 within a session — with no
normalisation and no census-carrying comment needed to justify it; the comment at the call site now
cites the mechanism (and the still-unimplemented 65,536 wrap) instead. `LOOP_RUN_TIME` (`flags & 2`)
is now also named explicitly in `anim-definitions.md` as implemented-but-unreachable (1,018 of 1,018
shipped `Loop` events carry `Count`, none carries `RunTime`), closing disproven-claim 1 for good.

**The literal mechanism fixed a real, previously undocumented overshoot — in both pass count AND
duration, confirmed against `FUN_004ecbb0` directly, not just against `004ebfd0`'s description.**
Tracing the old down-counter by hand (and confirming empirically, by instrumenting
`CountedInstantLoopTakesItsAuthoredFramesInSeconds` before and after) showed the old code ran
`authored + 1` total passes, not `authored` — its zero-check landed one visit later than the
decrement that should have stopped it. A first pass at the fix widened that test's tolerance
(`3 * dt` to `4 * dt`) without re-deriving *why* the measured total moved, which invited (correctly)
the question of whether `want = 200 * AnimFrame` was still the right centre or whether the fix had
introduced a one-frame-short fencepost (dropping a "free" first pass along with the spurious one).
Settled by decompiling the stepper (`FUN_004ecbb0`) directly rather than reasoning from its
description: a `LOOP` rewind always returns (state 4), so the next pass can only start on the
*following* stepper call — there is no pass that shares a tick with the one before it. Hand-tracing
calls against passes at `dt == AnimFrame` confirms pass *k* completes on call *k* for every *k*,
including pass 1 (CSVM's own `_clock += dt` runs before any dispatch on every `Advance()` call, so
pass 1 is not free there either) — `N` passes cost `N` ticks, not `N - 1`. The instrumented numbers
confirm it lands exactly there: at `dt = 1/60` (the calibration rate `CAP-19` measured against),
the new code measures `3.3333309 s` against `want = 3.333333 s` — float32 noise only, zero
shortfall — while the *old* code measured `3.3499975 s`, a full `AnimFrame` **over** `want`, at the
very same step. So the old code was wrong in duration too (long by one frame, ~0.5% for this
fixture), not accidentally correct via a wrong count; the up-counting rewrite fixed both together.
The residual noise at finer steps (`1/144`, `1/240`) is ordinary last-pass step-quantisation, present
before this item too, just landing with a different sign now that the pass count shifted by one —
the `4 * dt` headroom absorbs it without moving `want`. `CAP-19`'s "~3 s" cannot discriminate 199
from 200 frames and was not leaned on either way. None of this is the carry, and none of it is
something `BL-237` or any golden could have exposed, since none measures total loop-pass duration
that precisely; the comment in `CountedInstantLoopTakesItsAuthoredFramesInSeconds` carries the
evidence now.

**(b) The yield difference is not a second divergence — it is the carry's own mechanism, already
covered by Decision 2.** The original always returns after one `LOOP` pass, no matter what; our
runner returns early only for an instantaneous pass (`_frameGatePending`) and otherwise keeps
advancing the `while` loop for a timed pass. Two things rule this unobservable as anything beyond
what Decision 2 already accepts: **(1)** `GameClock`'s shipped `Realtime` mode (every mode except
`--det`/the lab) feeds `SequenceRunner.Advance` the real per-frame wall delta, not a fixed `1/60` —
so a coarse step *can* let a short-period timed loop (`ftank_boom*` at 0.02 s, `ww_balmoral*` at
0.01 s) roll over more than once within a single `Advance` call. **(2)** That is exactly what the
overshoot carry is *for* — running the correct number of iterations against elapsed sim time rather
than the original's own tick-quantised count — and the architecture.md comment already says so
("pre-fix code reached for an early `return`, and that return *was* the frame lock" / "dropping the
return is the whole change"). Forcing a yield after every pass would not restore a second, distinct
piece of original behaviour; it would undo the carry and reopen the exact `BL-237` regressions
(the 0.02 s-period loops running at 60% speed, `ww_balmoral1/2/3` taking 16.7 s for an authored
10 s). No code change.

**(c) The carry is written down as a deliberate divergence** in `docs/architecture.md`'s
`SequenceRunner` entry, folded into the existing `AnimFrame` ⚠ bullet rather than added as a sixth
(the entry was already at the 3-per-module budget's overage of 5, owned by D32): the original
hard-zeroes both timers and returns immediately, one pass per its own engine tick always, which
quantises correctly only because the original paces itself; CSVM must pace an authored duration
against sim time at whatever step size the session runs, and `BL-237` measured what dropping the
overshoot costs. The neighbouring ⚠ bullet's stale `_loopsLeft == -2` sentinel mention was updated to
name the new up-counting mechanism instead — both edits are text within existing bullets, not new
ones.

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM`: **882 units, 30 in-engine suites,
engine errors clean, 12 of 13 goldens hash-identical, `c1-destroy-effects` unmoved from B12/B13's
`00ab194f…`** — no golden moved beyond the branch's known golden-red state. The timed-loop cases
(`ww_balmoral1/2/3` at `LOOP 1000 @ Sequence 0.01 s`, the `ftank_boom*` family at 0.02 s) are
covered by the existing `AuthoredPeriodIsHonouredAtStepsCoarserThanItself` and
`AuthoredPeriodShorterThanAnAnimFrameIsNotStretchedToOne` theories, both run at 60 Hz and steps
finer than it (down to 1/600 s); no new unit was needed.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** Our `LOOP` differs from the original in exactly one respect — the overshoot carry — and
that is stated, deliberate and justified in `docs/architecture.md`.

**Evidence (confidence: direction sound).** `004ebfd0`: increment a **u16** counter, terminate when
`counter == authored` (with `-1` special-cased infinite, which is why `0` is effectively infinite —
it can only match after 65,536 passes). Then hard-zero both timers via `FUN_004ebfa0` and return 4,
which makes the stepper re-gate the first event and **return** — one pass per tick, always, even
when the body took time. Our runner carries the overshoot (`BL-237`) and continues in-frame when the
body scheduled time. Decision 2 keeps the carry.

**Approach.** Two concrete alignments and one doc line. (a) Make the `LOOP` count a counter that
counts up to the authored value, so the `0`-is-infinite behaviour follows from the mechanism rather
than from the `authored == 0 ? -1` normalisation — the comment there becomes a citation instead of a
census. (b) Confirm our "yield after a loop pass" behaviour matches "always yield"; if our timed-body
path continues in-frame, decide from the data whether any def can observe it. (c) Add the carry to
`docs/architecture.md`'s `SequenceRunner` entry as a deliberate divergence with its `BL-237` measurement.

**Model recommendation.** medium — mechanical once the reasoning above is accepted.

**Verify.** `--run-tests`; the timed-loop cases named in `anim-definitions.md` (`ww_balmoral1/2/3`,
`ftank_boom*`) still run at their authored durations at 60 Hz and at 600 Hz — the sim-rate
independence `BL-237` bought must survive.

**⚠ Traps.** `AnimFrame` is deliberately its own constant, not `GameClock.FixedDt` — do not merge
them while in here. The u16 wrap is a mechanism, not a feature: do not implement an actual 65,536
cap, since `-1` already covers the infinite case and a real wrap is unreachable in a session.

</details>

## B15 ☑ `START_TIME` origins: `Animation` reads the animation clock

**Landed.** The census the item's Verify demanded ran first, and it came back **non-zero**, so the
change was built rather than documented away. `AnimInstance` now carries a `Clock` advanced at the
top of its `Advance` (before the runners, so both clocks move together within a tick), seeded into
each `SequenceRunner` at construction via `AddRunner` and refreshed from `inst.Clock` on every
runner advance. `SetDue` resolves the three origins separately for the first time: `"Animation"`
against the instance clock, `"Sequence"` against the runner's own, `"Event"` and **null** against
`_base`. The `"Animation" or "Sequence"` collapse — and the comment claiming the two coincide "for
every case in the shipped data" — are gone.

**The census, over all 3,015 compiled defs**
(`analysis/anim-interpreter-decode/start_origin_census.py`, kept beside `anim_census.py`): 3,934
events carry an explicit `start` — `Event` 2,573, `Animation` **1,091**, `Sequence` 270 — and every
one of the 3,934 has a **non-zero** time, which is mech3ax collapsing the `Animation + 0.0` pair to
`None` showing up in the data exactly as disproven-claim 2 predicts. Of the 1,091 `Animation`
events, **900 sit in `Initial` sequences** (where the bootstrap starts the sequence with the
instance and the two clocks agree) and **191 sit in `OnCall` sequences** — the reachable blast
radius, 17 % of the origin's uses. The 191 are: every rocket/torpedo/sonic/flak/cannonball trail
puffer shutting off at `Animation 10.0` (and `torpuffer_trail1/2` at 3.5 s / 29.0 s),
`ap_light_seq`'s and `torp_light_seq`'s `LightAnimation` chains at `Animation 0.25` — the same
`ap_light_seq` B11's evidence names as double-called — `chuteman_drop`'s `ObjectMotion` at 0.5 s,
`flydirt_plane`'s `hide_dust` at 1.9 s, `car_go_home`'s `car_dust`, `generate_smokescreen`'s two
emitters at 2.0 s, and the 22-strong `gen_flare_yellow` family's `light_loop` `Loop` at 2.0 s.
Under the old reading every one of those fired late by exactly however long the animation had been
running when the `CALL_SEQUENCE` landed.

**The trap was avoided by mechanism, not by care.** A null `start` leaves `AnimEvent.StartOffset`
**null** (only `CompiledAnim.Parse` ever sets it, and only from a present `start` object), so the
install's unstamped events never reach the `"Animation"` arm at all — they fall to the `_base`
default exactly as before. `SetDue`'s comment states this so the next reader does not "tidy" the
null case onto the new clock.

**The gate is expressed in the runner's own clock** (`_clock + (StartTime - _animClock)`) rather
than by adding a second comparison to the advance loop. Both clocks tick by the same `dt`, so the
gap between them is fixed between the `SetDue` and the fire; a `LOOP` that rewinds `_clock` re-gates
through `SetDue` anyway. That keeps `_due` a single quantity, which the `LOOP` carry
(`_clock - _due`) and the `_frameGatePending` floor both read.

**The 86,400 s clamp is a comment, not code**, as the item asked — it lives on `AnimInstance.Clock`,
which is never rewound (a `LOOP` rewinds the sequence's timers only).

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM`: **883 units** (882 + the new one),
**30 in-engine suites**, engine errors clean, **12 of 13 goldens hash-identical with
`c1-destroy-effects` at B12's `00ab194f…`** — no golden moved beyond the branch's known
golden-red state. The new unit
`AnimationOffsetInACalledSequenceGatesOnTheInstanceClock` builds the 191's shape (an `OnCall`
sequence a `CALL_SEQUENCE` starts at 1.0 s, holding an `Animation 1.5` event) and asserts the fire
lands at 1.5 s and not at 2.5 s; it was **shown able to fail** by restoring the old
`"Animation" => ev.StartTime` arm, which turns it red at exactly that assertion. The two test
fixture builders that bypassed `AddRunner` to `new SequenceRunner(...)` directly now go through
`AddRunner`, which is what the method's own doc comment already claimed was the only path in.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** An event with `START_TIME ANIMATION t` fires when the *definition's* clock reaches `t`,
not when its own sequence's clock does.

**Evidence (confidence: traced).** `FUN_004ecbb0` gates origin 1 against `anim+0xb0` — a clock owned
by the animation instance and shared by all its sequences — origin 2 against the sequence timer
(`seq+0x24`) and origin 3 against the event timer (`seq+0x28`). `SequenceRunner.SetDue` collapses
`"Animation"` and `"Sequence"` onto the runner's own clock, with a comment noting the two coincide
"for every case in the shipped data". They do not coincide for a sequence started by a later
`CALL_SEQUENCE`, which is most `ON_CALL` sequences.

**Approach.** Give `AnimInstance` a clock, advance it in `Advance`, and pass it to the runner so
`SetDue` can resolve `"Animation"` against it. Note the exe clamps that clock at 86,400 s
(`0x47A8C000`) inside the `LOOP` handler — worth a comment, not worth implementing.

**Model recommendation.** high — it adds a second clock to the scheduler; getting the reset points
wrong shifts timings install-wide.

**Verify.** First **census the reachable blast radius**: how many events carry `Animation` with a
non-zero time inside a sequence whose `seq_state` is `OnCall`? If that count is zero the change is a
no-op and the item lands as a documented confirmation instead. Then `--run-tests` and the goldens.

**⚠ Traps.** A null `start` encodes as `Animation + 0.0` (disproven-claim 2). If the fix routes
nulls through the new animation clock, every unstamped event in the install starts gating on a clock
that is not zero — and the null case is 174,938 events. Keep null on the "immediately after the
previous event" path.

</details>

## B16 ❌ `RANDOM_WEIGHT`: the original's 200-entry shared table

**Disproven.** The table is not a constant. Fixed the comparison sense instead
(`AnimRuntime.cs`'s `RandomWeight` case: `<` → `<=`).

`read_memory` on `DAT_009fce20` (800 bytes) in the static image returned all zero. `get_xrefs_to`
on the address showed why: `FUN_004ee380` writes it, once per process start, with 200 calls to
`rand() * (1.0/32767.0)` (`_DAT_00728358 = 3.051851e-05`, matching MSVC's `RAND_MAX`). It is
runtime-generated, not compiled data — there is nothing at that address to dump-and-embed. Worse
for reproduction: the CRT `rand()` stream it draws from is not run-stable in the original either —
`FUN_004df1d0` and `FUN_0044e010` (ordinary startup/level-load paths) both end with
`srand((uint)time(NULL))`, reseeding the same global stream from wall-clock time. Two runs of the
original fill `DAT_009fce20` with two different tables. The "shared 200-slot cursor" everyone reads
`RANDOM_WEIGHT` through is cycling over whichever draw happened to land that session — there is no
fixed sequence for CSVM to match, session to session, even in the original.

CSVM's session-seeded `_rng` (`GameSession.cs` → `AnimRuntime._rng`, per the class comment "One
field rather than scattered `GD.Randf()` calls so the session's master seed can pin the whole
sequence") is already the correct-shape answer: a PRNG stream pinned by `--seed=`/`--det`, same as
the original's own `srand()` discipline. It does not share the original's specific generator or its
cross-condition global cursor, and nothing in the evidence says either is worth reproducing — the
original's own value at a given `RANDOM_WEIGHT` site is not reproducible from the exe alone, so
there is no "original" behavior to match beyond "some PRNG, uniform on [0,1)".

The one real bug was free to fix regardless: the exe's branch is `draw <= threshold`
(`FUN_004ec080`, `004ec117`), CSVM had `_rng.NextDouble() < num`. Now `<=`.

Full derivation, the two `srand(time(...))` call sites and the disproof are recorded in
`analysis/anim-interpreter-decode/FINDINGS.md`.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** Decide, on evidence, whether to reproduce the original's random source.

**Evidence (confidence: lead only).** `FUN_004ec080`'s bit-0 branch reads
`(&DAT_009fce20)[DAT_0072836c]` and advances `DAT_0072836c = (DAT_0072836c + 1) % 200` — a fixed
200-entry table with a **global** cursor shared by every animation, not a per-evaluation `rand()`.
Whether the table is uniform, and whether the sharing is observable, is untraced.

**Approach.** Dump the 200 floats at `DAT_009fce20` and look at the distribution. If it is uniform,
the only difference from ours is the global coupling and the item may land as a disproof. If it is
not uniform, reproducing the table is cheap and exact — it is 800 bytes of constant data, and
constants are not game assets.

**Model recommendation.** medium — investigation first, and it may end with no code.

**Verify.** If implemented: `--det` runs stay byte-identical (the table is deterministic, so this
should *improve* determinism, not threaten it) and the golden set is unmoved.

**⚠ Traps.** Do not commit the table as extracted game data if it turns out to be anything other
than a plain numeric constant — check `PROJECT_CONTEXT.md`'s hard rule before adding a data file.

</details>

---

# Wave C — Missing events

## C21 ☑ `FBFX_COLOR_FROM_TO` — the full-screen flash

**Landed.** `FUN_004ec6a0` was decompiled *and* disassembled before anything was chosen, and every
question the item posed is answered from it rather than from taste:

- **Blend: alpha.** The compiled event interleaves `from`/`to`/**`delta`** per channel from `+0x0c`
  with `run_time` at `+0x3c`. The handler clamps each channel to `0…1`, scales RGB by 255 and packs
  it into ONE frame-buffer pixel through the same mask/shift globals
  (`DAT_009c67fc`/`6800`/`6804`/`680c`) the weather particles' `COLOR` uses — and passes the alpha
  **separately, as a scalar**, not premultiplied into that pixel. A colour plus a scalar weight is
  `dst·(1-a) + colour·a` and nothing else; the data corroborates, since a multiply would render
  `he_ground_effect`'s opening white-at-α-0.3 step invisible. *(The blend state itself sits behind a
  virtual on the renderable at `this+0x28` and was not traced — the colour+weight pair and the
  white-step argument are the evidence, and that limit is stated rather than papered over.)*
- **Interpolation: linear in RGBA.** `from + t * delta`, with `t` the sequence's **event timer**
  (`seq+0x28`) clamped to `run_time` and `delta` the compiled `(to - from) / run_time`. Past the run
  time the value snaps to `to` outright.
- **Concurrency: last writer wins, and it is simpler than blending.** The pair goes to a single
  process-wide object — `FUN_005ca1f0` → `FUN_005ca0e0`, `this` hard-coded to `0x9c8a98`, colour at
  `+0x60`, alpha at `+0x68` — and nothing else in the exe writes those fields. A second burst
  overwrites the first. The vtable call that follows (`FUN_005c54a0`) arms the effect **for the
  current frame only**, which settles a second question the item did not ask: a completed chain does
  not hold its `to` colour, it vanishes on the next frame because nothing re-arms it. CSVM's
  `ScreenFlash.Play` replaces the running ramp and the ramp ends rather than holding.
- **`alpha_delta` is a round-trip artefact, not a parameter — checked before being ignored.**
  mech3ax recomputes every delta as `(to - from) / run_time` and emits `alpha_delta` only when the
  file's stored value disagrees bit-for-bit (`e36_fbfx_color_from_to.rs`). A census over all 3,015
  compiled defs found **8 non-null values, all of them `flak_effect`'s `-0.99999994` against a
  computed `-1.0`** — one ulp, ~2e-8 of alpha across the whole 0.3 s ramp, orders below one 8-bit
  level. Not read.
- **Return contract.** The exe returns 1 (still running) until the event timer reaches `run_time`,
  then 2. CSVM's equivalent is `Dispatch` reporting `run_time` as the event's **duration**, which
  `SequenceRunner` turns into the next event's `_base`. That is the whole reason the six steps
  space out; with `duration = 0` all six fire in one instant (demonstrated — see below).

**Where it lives.** `CSVM/src/UI/ScreenFlash.cs`, a session-owned node holding **one ramp state and
one hidden `ColorRect` per rendered view**. `AnimRuntime` gets a `ScreenFlash` sink field beside its
existing effect sinks (`ExternalEffect`/`ExternalEffectStop`/`ResolveLibraryRoot`) — no second
mechanism was invented — and `GameSession` builds the overlay right after `BuildRigs`, wiring it to
the world runtime directly and to the world-effects runtime through a new
`WorldEffectsFactory.ScreenFlash`. The factory is the right carrier because a census of all 152
shipped events found them in **exactly four definitions**: `he_ground_effect`, `ap_ground_effect`
and `flak_effect` (6 × 8 chapters each), all three in `EffectCatalogue.EffectAnimNames` and so all
three played by the effects runtime, plus the intro cutscene's `gi_scene1` (1 × 8), which CSVM never
plays. `FbfxCsinwaveFromTo` was not built.

**Splitscreen and HUD, decided rather than defaulted.** One state, N surfaces: each rig's
`HudParent` gets its own `ColorRect`, so with `--players=2..4` all panes wash together (the original's
single global state) but the 2 px gutters and the empty 3P quadrant — which are not part of any
rendered picture — stay black. A single window-wide overlay would have painted them, and the
per-rig shape is the one the cloud whiteout already uses. The layer is `HudLayers.WorldOverlay`,
**under** the HUD: this is a world-picture effect, and unlike the lens flare's sun wash (which
`CAP-13` measured whitening the compass at the same α as world pixels) there is no footage saying
this one reaches the instruments, so the defensible reading wins. The launchscreen and both
scoreboards sit at `HudLayers.Board` and are unreachable by it. The `PlayerRange` gate was **not
touched**, and the question of whether its radius was 2× the original's has since been answered no
(`git log --grep=BL-333`).

**The overlay is `Visible = false` whenever no ramp runs**, which is why the golden set is
untouched by its mere existence rather than that being a claim about a transparent rect.

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM`: **883 units, 31 in-engine suites**
(30 + the new one), **engine errors clean, 12 of 13 goldens hash-identical and `c1-destroy-effects`
at B12's `00ab194fba6b99d169ee27fcc213f4fa`** — **no golden moved beyond the branch's known
golden-red state**, including the four full-screen-sensitive ones (`c1-flight`, `c1-crash`,
`c1-waterfall`, `viewer-bhawk`), which is the check a new always-present canvas layer most needed.

New `fbfx-flash` `--run-tests` suite (`Suites.cs`), on `effect-template-mesh`'s `WithEffectStage`
host so it plays the real `he_ground_effect` off the real C1 gamez at the camera point: it asserts
six ramps arrive, each with its authored `run_time` and authored `from`/`to` colours, each starting
one previous run time after the one before it, and the chain spanning its authored 1.1 s first fire
to last. Measured: `0.0167s (0.2), 0.2167s (0.4), 0.6167s (0.2), 0.8333s (0.1), 0.95s (0.2),
1.1667s (0.1)`. **Shown able to fail** by setting `duration = 0f`, which turns the note into
`0.0167s ×6` and the suite red. The span gets one step of headroom per gap, with the reason stated
at the assertion: the authored run times are exact multiples of 1/60 but not of binary float, so
three of the five gaps land one step late and the misses do not cancel.

**Visual evidence** (`.scratch/c21-fbfx/`), all `--det`, `--play-anim=he_ground_effect
--direction=0,-0.15,-1` on C1 (an explicit direction suppresses the lab's auto-framing, which
otherwise pulls the camera past the def's own 100 m `PLAYER_RANGE` gate):

| File | Reading |
|---|---|
| `fbfx-on-f12-t0.20-white-to-violet.png` | End of step 1: the whole frame — sky, terrain, fireball — lifted toward white/lilac. |
| `fbfx-on-f30-t0.50-violet-hold.png` | Step 2's hold: the strongest violet, uniform edge to edge, with the fireball still reading orange through it. |
| `fbfx-on-f66-t1.10-violet-to-white.png` | Step 6's ramp back to white. |
| `fbfx-off-f30-t0.50-control.png` | The identical pose and frame with the sink nulled — the A/B control. |

**⚠ What these frames cannot show.** A single frame cannot show the *ramp*: that the wash moves
white→violet→white on the authored schedule rather than sitting at one colour is exactly what a
still is blind to, and it is the `fbfx-flash` suite's timings — not these images — that carry it.
Nor can they show the splitscreen decision (single-player poses, one surface) or the HUD ordering
(the anim lab draws no flight HUD). They show one thing: the overlay exists, covers the whole
rendered view, and composites as an alpha blend rather than replacing the picture.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** A close HE hit washes the screen white→violet→white over 1.2 s, as the data authors it.

**Evidence (confidence: traced).** `FUN_004ec6a0` is the exe's handler (dispatch slot 36). **152
events ship.** `he_ground_effect`'s `frame_buffer_effects1` is the worked example: six steps,
`rgba(1,1,1,0.3) ↔ rgba(0.2,0,1,0.2)`, run times 0.2/0.4/0.2/0.1/0.2/0.1, reached through
`If PlayerRange 10000` — the gate exists *because* the effect is only worth paying for near the
camera, which is itself evidence it is a screen-space effect and not a world one.

**Approach.** A screen-space overlay owned by the session (not by `AnimRuntime`, which is
world-scoped and can be instanced per-effect-pool), driven by a handler that returns the event's
`run_time` as its duration so the sequence gates correctly. Read `FUN_004ec6a0` before choosing the
blend — whether it multiplies, screens or alpha-blends over the frame buffer is decidable from the
exe and should not be guessed. Concurrent flashes need a composition rule; take the original's.

**Model recommendation.** high — a new rendering surface plus an exe read.

**Verify.** D31's timeline suite asserts the six steps fire at the right times. Visually:
`--screenshot` at a fixed frame during a close HE hit, with and without, at `--det`.

**⚠ Traps.** Do not tune the `PlayerRange` gate to make the flash appear at a pleasing distance.
(A2 suspected the radius was half what we compute; that was traced out on 2026-08-13 and the radius
is correct, so a gate that looks wrong is evidence about something else.) And `FbfxCsinwaveFromTo`
ships **zero** occurrences; do not build it for symmetry.

</details>

## C22 ☑ Triage the remaining four unhandled kinds that ship

**Landed: none of the four gets a handler — all disproven reachable, not just judgement calls.**
All four dispatch targets (slots 35/17/16/20) were missing Ghidra function boundaries — reached only
through the table, like `LOOP` — so each was forced into a function and decompiled in full before
judging it. The plan's own lead ("`ObjectCycleTexture` is the most likely real gap") did not survive
that read: it turned out to be the SAME `<part>_damage_{green,yellow,red}` cockpit indicator
`docs/architecture.md`'s `Flight/DamageVisuals.cs` entry already records as deliberately unwired (no
cockpit), not a second, unrelated mechanism — a disproof, not new code. Full per-kind decode, exe
addresses and def census in `docs/formats/anim-definitions.md`'s "The last four unhandled kinds";
summary:

- **`Callback`** (288, 120 defs) — calls a native callback the anim instance never has registered
  here (`has_callbacks` mission plumbing, no consumer).
- **`ObjectCycleTexture`** (96, 96 defs) — the already-unwired cockpit indicator; confirmed same
  mechanism, no new gap.
- **`ObjectDeleteChild`** (48, 40 defs) — a scene-graph reparent. `camera1-generic_intro`'s rig and
  `apassengers-rem_pas` (whose parent `pass_st` is not a gamez node anywhere, so it can never
  resolve) are unreached by anything CSVM plays; the `cpeject1/2/cpejectstop` defs ARE reached (the
  player's own `destroy_it` crash sequence calls them) but the very next event in all three hides the
  same node regardless, so the delete changes nothing observable either way.
- **`CameraState`** (8, 8 defs) — `gi_1stperson`'s only caller anywhere in the install is the same
  unreached `camera1-generic_intro` chain as above, and CSVM has no scripted first-person camera to
  configure regardless (`PlayerFirstPerson` reads `false`).

**Verified.** No code changed (`AnimRuntime.cs`'s `default:` case already counts all four by name),
so `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM` was run as a docs-change sanity check rather than a
behaviour check: build clean, units and in-engine suites unchanged, goldens unchanged from B12's
known golden-red state (`c1-destroy-effects` still at `00ab194fba6b99d169ee27fcc213f4fa`, nothing
else moved).

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** `Callback` (288), `ObjectCycleTexture` (96), `ObjectDeleteChild` (48) and `CameraState`
(8) each have a handler or a one-line recorded reason they do not.

**Evidence (confidence: traced for the counts, direction-sound for the calls).** All four reach
`AnimRuntime`'s `default:` today and are counted only. `ObjectCycleTexture` is the most likely real
gap — `SceneBuilder`/`TextureCycler` already run material cycles globally and continuously, so an
event that *starts* one per-object is currently a no-op against a cycle that is always running.
`Callback` is `has_callbacks`-gated mission plumbing with no free-flight consumer. `CameraState` at 8
occurrences is cutscene camera work outside every mode we ship.

**Approach.** Read each exe handler (slots 35, 17, 16, 20), then implement only what changes
something observable in a mode we have. A recorded "not implemented, because" in
`docs/architecture.md`'s `AnimRuntime` entry is a valid outcome for the other three — but it must
name the exe slot so the next session does not re-derive it.

**Model recommendation.** medium — mostly reading and deciding.

**Verify.** The `default:` counter's report shrinks to only the kinds that shipped a reason. Where a
handler lands, D31 or an existing suite must cover it.

**⚠ Traps.** Do not implement all four for completeness. Three of them have zero reachable effect in
the modes CSVM has, and an unexercised handler is worse than a documented gap.

</details>

---

# Wave D — Proof

## D31 ☑ `ordnance-burst-timeline` suite: HE, flash and sonic played end to end

**Landed.** `ordnance-burst-timeline` in `CSVM/src/Testing/Suites.cs`, beside `fbfx-flash` and
`effects-census`, on the existing world-effects `AnimRuntime` host. It plays each of the three defs
once on its own miniature stage and matches the **whole** recorded `OnEventDispatched` log against a
lane table read off the def JSON by hand — every sequence, every event, in its sequence's order, at
its authored instant. Order is asserted by **consumption**: a recorded row is claimed by the first
lane whose next *unconsumed* step it matches on (sequence, index, kind, name), so a row that arrives
early, twice, or unauthored matches no pending step and is reported stray. The four-part key is
needed because sequence names are not unique — both HE and sonic ship two unnamed `Initial`
sequences — and it is verified unique against the JSON for all three. Driven at **1/240 s**, four
times finer than `SequenceRunner.AnimFrame`, because the authored gaps go down to 0.01 s and none of
the three defs carries a `LOOP` for the AnimFrame floor to matter to; the slack is six steps
(0.025 s), sized to the mechanism (one step for the record stamp, one per CALL level for `BL-135`,
one for float). The recorded log of all three is written to `.scratch/ordnance-burst-timeline.txt`.

**It found a real divergence on its first run, and that is the item's main result.**
`LIGHT_ANIMATION` reported duration **0**, so every authored light-pulse chain in the install fired
in one instant and each step re-armed the tween the previous one had started. The exe settles it:
the handler is dispatch slot **5, `004e82b0`**, read in full — it seeds the working deltas on the
first dispatch (`seq+0x20 == 0`), adds one tick's worth per dispatch with the last tick shortened to
the remainder, and ends `return (seq->event_timer < run_time) ? 1 : 2`, i.e. **still running until
the run time is up**, the same gate `FBFX_COLOR_FROM_TO` (`004ec6a0`) uses and the same
handler-return contract A1 recorded. The fix is one line in `AnimRuntime`'s `LightAnimation` case
(`duration = instant ? 0 : run_time`; a `RESET_STATE` lands the delta whole, so it still takes no
time). This is scheduling, not rendering — the same class of change as C21 — and it is exactly the
case the milestone's "a fix that makes an effect look different is a scheduling consequence and must
be visible in the timeline test" sentence describes.

**What that fix restored, visibly.** C1's `red_police` (`police_lights`) authors
`LIGHT_STATE` red `{0…10}` / `LIGHT_ANIMATION {max +40}` over 0.25 s / `LIGHT_ANIMATION {max −40}`
over 0.1 s / `LOOP −1` — a 0.35 s flashing beacon. Collapsed, the two ramps cancelled each other
every animation frame and **the police light did not flash at all**; held, it flashes at its
authored rate and the `LOOP −1` paces off the ramps instead of running one instantaneous pass per
`AnimFrame`. `he_ground_effect`'s `he_light_seq` is the same shape with seven ramps over 0.41 s.
Recorded in `docs/formats/anim-definitions.md`'s `LIGHT_STATE`/`LIGHT_ANIMATION` section and in
`docs/architecture.md`'s `AnimRuntime` entry.

**Shown able to fail, on B12's own case.** `git checkout 8ee31a0 -- CSVM/src/Mech3/SequenceRunner.cs`
(the file as of B11, i.e. B12's parent — which also takes back B13/B14/B15's edits to it) and the
suite goes **red**, with exactly two failures and both of them B12's:
`large_fireball's parked stop_p1trail dispatched nothing … (2 event(s))` and the stray-row check
naming `0.308s [stop_p1trail] #0 PufferState fierypuffer` and `#1 ObjectActiveState flame_ball_01`
— the stopper idiom starting a sequence the original leaves parked. Restored with
`git checkout HEAD -- …` and green again. The `p1trail` lane is asserted alongside as the live
control, so "the stopper fired nothing" cannot pass on a fireball that never started.

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM`: **883 units pass, 32 of 32 in-engine
suites pass, engine errors clean**. Goldens: **3 of 13 moved**. `c1-destroy-effects` is B12's known
move and its hash is unchanged by this item (`00ab194f…` with and without the `LIGHT_ANIMATION`
fix — measured by re-rendering the whole set against `HEAD`). The two new ones are this fix, and
both were rendered before and after into `.scratch/d31-goldens/`:

| Shot | Moved | Changed pixels | What |
|---|---|---|---|
| `c1-flight` | `38adfec0…` → `e2eb44c8…` | 10,960 of 921,600 (1.19 %), max channel delta 144, one 126×124 patch at (0,575) | the police car's beacon, now lit mid-flash on the road below — the light that never flashed |
| `c1-crash` | `2e44dc12…` → `3c7b4154…` | 9 of 921,600 (0.001 %), max channel delta 2, a 10×1 strip at y=0 | sky, at the noise floor |

`analysis/goldens/manifest.json` was deliberately **not** re-baselined — D32 owns the single re-pin
(Decision 5), and now owns three named moves rather than one.

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** One `--run-tests` suite plays `he_ground_effect`, `flash_effect` and
`sonic_ground_effect` on a fixed-dt clock and asserts each one's full event timeline against the
authored JSON — every sequence entered, every event fired, in order, at the authored time.

**Evidence (confidence: traced).** These three between them exercise every Wave B divergence:
`he_ground_effect` reaches `large_fireball`, whose `STOP_SEQUENCE stop_p1trail` is the B12 case;
`sonic_ground_effect` calls `sonic_light_seq` **twice**, the B11 case; both carry `If` chains
(B13) and `LightAnimation` run-time chains that exercise the event-timer origin (B15);
`he_ground_effect`'s `frame_buffer_effects1` is C21. `flash_effect`
(`flash_control-flash_effect.json`) is the third because it is the pure light/no-particle case —
`--effects-test` already reports it as building no puffer, so it isolates the scheduler from the
emitter.

**Approach.** Beside `effects-census` and `wait-for-completion` in `CSVM/src/Testing/Suites.cs`,
using the existing world-effects `AnimRuntime` host. Drive `--det` with the fixed-dt clock, hook
`ISequenceHost.OnEventDispatched` (which already reports def, anchor, sequence, event index, kind
and name — it exists for the anim debugger and is exactly this record), and compare the captured
dispatch log against an expectation derived from the def JSON. Assert **order and time**, not just
membership; a membership-only assertion cannot see any Wave B bug.

**Model recommendation.** high — the suite is the plan's whole proof, and a weak assertion makes
every earlier item unverified.

**Verify.** The suite must be shown able to fail: run it against `HEAD~` for one Wave B item and
confirm it goes red. An unchanged green on a suite never seen red proves nothing
(`docs/verification.md`).

**⚠ Traps.** The three defs are chapter-scoped and their template roots must all be staged, or a def
plays nothing *silently* — this is the D31/`analysis/effect-anchor-roots/` failure mode recorded in
`weapon-effects.md`, where 19 of 28 roots gave `PufferState(no host node)×20` and looked like a
pass. Assert the anchor roots resolved before asserting the timeline. Also: `--effects-test`'s
0.3 s instance TTL and 0.1 s per-name throttle exist for the gun path — this suite must not inherit
them, or a 1.2 s flash is truncated at 0.3 s and the timeline "passes" short.

</details>

## D32 ☑ Full regression + docs sweep

**Landed.** `.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM`: **883 units, 32 in-engine suites, engine
errors clean**. Goldens moved exactly the three shots the plan already tracked and named — no
fourth move, so nothing new needed reading. Re-baselined `analysis/goldens/manifest.json` to the new
hashes and folded each move's explanation (already written and rendered in B12/D31) directly into
that shot's `exercises` field: `c1-destroy-effects` (`fa27f0cd…` → `00ab194f…`, B12's stopper-idiom
retirement, 4 of 921,600 px in the fogged far-distance haze), `c1-flight` (`38adfec0…` → `e2eb44c8…`,
D31's `LIGHT_ANIMATION` duration fix, 10,960 px, the police car's beacon now lit mid-flash) and
`c1-crash` (`2e44dc12…` → `3c7b4154…`, same fix, 9 px of sky at the noise floor). A second full
`RunTests.ps1` pass after the re-pin came back **13/13 goldens hash-identical**.

An 8-chapter `--freecam --chapter=<X> --det --mute --frames=120 --screenshot=…` sweep
(`.scratch/d32-freecam-sweep.ps1`, logs in `.scratch/logs/`) came back **exit 0 and a saved
screenshot in every chapter**, zero unallowlisted engine errors (C1 carries the one known
allowlisted `no audio session` warning from `--mute`, the same line `docs/architecture.md`'s
`SoundArchive.Find` entry already documents) and sane per-chapter node/mesh counts: C1 7,064/3,425,
C1B 5,603/3,099, C1C 5,644/2,965, C2 4,956/1,616, C2B 4,901/2,708, C3 5,408/2,331, C4 8,289/4,204, C5
11,438/4,722 (gamez nodes/mesh instances) — no chapter is empty or truncated.

`docs/architecture.md`'s `SequenceRunner.cs` entry was at 5 `⚠` against the plan's 3-`⚠` budget
(flagged in this item's own Traps below). Brought it back to budget by folding two of the five into
plain prose rather than deleting them: the `_loopPasses`/`goto case "Elseif"`/256-guard
"looks-refactorable" note and the `OnEventDispatched` zero-cost-contract note were both already
duplicated almost verbatim as code comments in `SequenceRunner.cs` (the `Loop`/`Advance` bodies and
`ISequenceHost.OnEventDispatched`'s own doc comment respectively), so the architecture.md copy was
redundant, not load-bearing — the constraint survives at its enforcement point, just not doubled
here. The three that stayed are the ones with no code-comment counterpart: the LOOP `AnimFrame`
carry's deliberate divergence from `crimson.exe`, `BL-135`'s one-tick call lag (a `SequenceRunner.cs`
constraint that actually lives on `AnimInstance.CallSequence`/`Advance`, which carry no such note),
and the `WAIT_FOR_COMPLETION` next-event-not-lifetime hold with its census. `AnimRuntime.cs`'s entry
was re-read against what the plan actually landed (C21, C22, the resolver-fallback and pool
paragraphs) and is unchanged — it sits at 3 `⚠` already and nothing this plan touched needed a new
one.

Swept `PROJECT_CONTEXT.md`'s "Current status" to point past this plan (no active plan; playtests
unchanged) and archived this file to `docs/plans/` with a `COMPLETE` banner, row added to
`docs/plans/plans.md`.

**Verified.** `.\RunTests.ps1` before the re-pin: **883 units, 32 suites, 3 of 13 goldens moved**
(`c1-flight`, `c1-destroy-effects`, `c1-crash` — exactly the three the plan named, hashes matching
what B12/D31 recorded). After re-pinning the manifest: **883 units, 32 suites, 13/13 goldens
hash-identical, exit 0**. The freecam sweep: 8/8 chapters exit 0 with a screenshot, only the one
known allowlisted warning, node/mesh counts all in the range prior full-install sweeps have measured
(`docs/HISTORY.md`'s 2026-07-30 entry: C5 largest, C2B smallest — reproduced here). `⚠` count in
`SequenceRunner.cs`'s architecture.md entry: 3 (was 5); `AnimRuntime.cs`'s: 3 (unchanged).

<details>
<summary>Original approach (kept for reference)</summary>

**Goal.** The plan lands with the install-wide surfaces green and the docs matching the code.

**Approach.** `.\RunTests.ps1` (build → units → in-engine suites → goldens → one exit code), then a
`--freecam` pass over all 8 chapters checking for zero errors and unchanged node/mesh counts, then a
read of `docs/architecture.md`'s `SequenceRunner` / `AnimRuntime` entries against what actually
landed. Refresh `PROJECT_CONTEXT.md`'s "Current status" pointer and archive the plan.

**This item owns the single golden re-baseline** (Decision 5). No earlier item touches
`analysis/goldens/manifest.json`, so the branch runs golden-red from B12 onward by design. Here,
every moved hash gets **named, rendered before/after, and explained** before it is re-pinned — a
re-pin with no reading attached is the failure mode this decision exists to prevent. **Three are
already known to have moved, each named, rendered and explained in the item that moved it:**
`c1-destroy-effects` (B12, 4 px of 921,600 in fogged far-distance haze; the pose is blind to the
lifetime change that caused it — see B12's ⚠), and `c1-flight` + `c1-crash` (D31's
`LIGHT_ANIMATION` duration fix — 10,960 px of the police car's restored flashing beacon, and 9 px
of sky at the noise floor; before/after renders in `.scratch/d31-goldens/`). Nothing further is
expected; a fourth move here is a new fact and needs its own reading.

**Model recommendation.** medium.

**Verify.** One exit code from `RunTests.ps1`; the 8-chapter sweep's logs in `.scratch/logs/`.

**⚠ Traps.** The `⚠` budget in `docs/architecture.md` is **max 3 per module**, and the
`SequenceRunner.cs` entry was **already at 5 before this plan started** — B12 looked for one to
retire and found none of the five is about anything this plan touched (they cover the load-bearing
sentinels, `AnimFrame` LOOP pacing, `BL-135`'s one-tick call lag, the `OnEventDispatched` delegate
shape, and the `WAIT_FOR_COMPLETION` hold). Bringing that entry back to budget is real work here,
by merging or moving one to a code comment — not by deleting a still-binding constraint to make the
count fit.

</details>
