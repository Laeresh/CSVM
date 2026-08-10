# Animation runtime — match the original interpreter

**ACTIVE PLAN** (written 2026-08-10). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

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

11. B11 ☐ `CALL_SEQUENCE`: one instance per sequence, startable only from the parked state
12. B12 ☐ `STOP_SEQUENCE`: halt only — retire the stopper idiom
13. B13 ☐ `IF` skip: match the original's non-nesting-aware scan
14. B14 ☐ `LOOP`: align the rewind, keep the carry, write down why
15. B15 ☐ `START_TIME` origins: `Animation` reads the animation clock
16. B16 ☐ `RANDOM_WEIGHT`: the original's 200-entry shared table

### Wave C — Missing events

21. C21 ☐ `FBFX_COLOR_FROM_TO` — the full-screen flash
22. C22 ☐ Triage the remaining four unhandled kinds that ship (`Callback`, `ObjectCycleTexture`, `ObjectDeleteChild`, `CameraState`)

### Wave D — Proof

31. D31 ☐ `ordnance-burst-timeline` suite: HE, flash and sonic played end to end
32. D32 ☐ Full regression + docs sweep

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
`PLAYER_RANGE` `dist² * 4.0` comparison, and the non-nesting-aware scan to the first `Else`/`Elseif`
byte. **Not independently re-checked this pass:** `LOOP` (`004ebfd0`) has no Ghidra-recognised
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

## B11 ☐ `CALL_SEQUENCE`: one instance per sequence, startable only from the parked state

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

## B12 ☐ `STOP_SEQUENCE`: halt only — retire the stopper idiom

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

## B13 ☐ `IF` skip: match the original's non-nesting-aware scan

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

## B14 ☐ `LOOP`: align the rewind, keep the carry, write down why

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

## B15 ☐ `START_TIME` origins: `Animation` reads the animation clock

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

## B16 ☐ `RANDOM_WEIGHT`: the original's 200-entry shared table

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

---

# Wave C — Missing events

## C21 ☐ `FBFX_COLOR_FROM_TO` — the full-screen flash

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

**⚠ Traps.** The `PlayerRange` radius may be half what we compute (A2) — do not tune the gate to
make the flash appear at a pleasing distance; that hides the open question. And `FbfxCsinwaveFromTo`
ships **zero** occurrences; do not build it for symmetry.

## C22 ☐ Triage the remaining four unhandled kinds that ship

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

---

# Wave D — Proof

## D31 ☐ `ordnance-burst-timeline` suite: HE, flash and sonic played end to end

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

## D32 ☐ Full regression + docs sweep

**Goal.** The plan lands with the install-wide surfaces green and the docs matching the code.

**Approach.** `.\RunTests.ps1` (build → units → in-engine suites → goldens → one exit code), then a
`--freecam` pass over all 8 chapters checking for zero errors and unchanged node/mesh counts, then a
read of `docs/architecture.md`'s `SequenceRunner` / `AnimRuntime` entries against what actually
landed. Refresh `PROJECT_CONTEXT.md`'s "Current status" pointer and archive the plan.

**Model recommendation.** medium.

**Verify.** One exit code from `RunTests.ps1`; the 8-chapter sweep's logs in `.scratch/logs/`.

**⚠ Traps.** The `⚠` budget in `docs/architecture.md` is **max 3 per module**. Wave B will want to
add more than three to `SequenceRunner`; merging or retiring an existing one is part of this item,
not an excuse to exceed it.
