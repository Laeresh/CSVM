# C3's balloon-battery kill chain (BL-348)

**ACTIVE PLAN** (written 2026-08-21). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan resolves `BL-348`: C3/M02's six balloon batteries do not die the way their authored data
says, on any of four death paths. Each balloon is three independently `WeaponHit`-able destructibles
(`tbaseN`, the ground tether anchor; `bontN`/`ball_kaboomN`, the balloon body; `b_turretN`, the slung
AI turret) wired by three exact-numbered `CallAnimation`s, and all four faults sit somewhere in the
`CallAnimation` / `NameResolver` / sequence-completion path. `BL-348` is the only backlog item this
plan draws on, and it was re-verified still-open in this session against both the record
(`git log --grep=BL-348` returns the filing commit `022d4467` and two decode amendments, `df9e78f4`
and `4ddac6c8`, none of them closing it) and the code (`Mech3/Anim/NameResolver.cs`,
`Mech3/AnimRuntime.cs:2276`).

Two boundaries. The reverse-engineering is **done** and is not re-run here: both the node-resolution
half and the `CALL_ANIMATION` dispatch half are decoded and written up in
[`org/sequences.md`](org/sequences.md), and this plan consumes those decodes rather than reopening
them. And the four known divergences between our resolver and the original (a per-event list, an
`OrdinalIgnoreCase` compare plus a `.flt` fallback, `*`/`#` read as node-name patterns, and the call
site used as the callee's Start anchor) are treated here as *candidate causes to test*, not as a
faithfulness backlog to clear; correcting a divergence that turns out not to drive any of these four
symptoms is out of scope and gets its own item.

## Milestone goal

- Shooting one `tbaseN` raises exactly one balloon, and that anchor stays on the ground.
- A balloon that rises detonates at the top of its 24-second tether burn, with the full
  `ball_kaboomN` (debris, fireball, sound, `balloon_downaN` fall).
- Shooting a `b_turretN` swaps that balloon's skin and, two seconds later, runs the same full
  `ball_kaboomN`, instead of leaving it hanging.
- Each of the four faults is confirmed fixed, or confirmed already gone, on its own evidence.

**No fix ships against a symptom that has not been re-observed on the current build.** The four
symptoms were recorded at the controls before `BL-415`'s resolver-scope change landed, and at least
two of them have a plausible reason to have already moved.

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Shooting the tether instantly explodes the balloon instead of raising it" is a fifth bug | `tether1` has no separate destructible. It is a plain node inside `bontN`'s own def, so a hit there resolves to `bontN`'s independent `ball_kaboomN` pool (health 30, immediate detonation by design). Confirmed at the F5 `--debug-damage` damage lab: the tether node's pool resolves to the parent, not to a phantom tether entity. Do not re-file it. |
| 2 | A name leak in the resolver drives six balloons off one event | The original binds exactly one node per reference, at load, and the running engine reads only an index (`FUN_004efaf0`, `FUN_004e8d60`). Nothing on that path can multiply one event into six. |
| 3 | The `*`/`#` wildcard divergence is what raises six balloons *here* | M02 authors no wildcard. `extracted/C3/M02/mis_anim/` ships six separate exact-numbered defs per role (`ball_kaboom1..6`, `balloon_up1..6`, `tbase_kaboom1..6`, `balloont_die1..6`), each on its own numbered anchor. |
| 4 | A play-once latch, or the `+0xac ≈ -99.0` refusal, drops the second `ball_kaboomN` | `FUN_004ed8c0` decides restart on the callee's run-state byte `+0xa0` alone under `CALL_ANIMATION`'s always-zero anchor, and teardown `FUN_004ed190` returns a finished animation to state 1, so a completed callee is immediately re-callable. The `+0xac` slot is the authored `RESET_TIME` (loader `0051f503`/`0051f553`, compiled offset 172) and the shipped archives hold only -1.0, 0.0 and one 5.0. Retired in `4ddac6c8`. |
| 5 | Our own `(def, anchor)` call guard is dropping calls | It is strictly more permissive than the original's gate, never stricter. It can refuse only a callee already live on that same anchor. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B3, B4 | The *what* is settled; the *how much* is TUNE. Add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only, no mechanism yet** | B2, B5, C6 | Budget for investigation; this may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. This plan runs on
`worktree-bl348-balloon-kill-chain`.

## What the data actually ships

The authored side of the kill chain, from `extracted/C3/M02/mis_anim/` (six numbered copies of each,
`N` = 1..6; all three defs ship `local_nodes_only: false`, and every `CallAnimation` names its target
with an exact digit, never a wildcard):

| Def | Trigger | What it calls, and when |
|---|---|---|
| `tbase_kaboomN` | `tbaseN` (ground tether anchor) killed | calls `balloon_upN`. Its own death sequence moves only its three debris chunks, never `tbaseN` itself. |
| `balloon_upN` | called | second sequence is `ObjectMotionFromTo(bontN, run_time 24.0)` (the 512 m rise) then `CallAnimation(ball_kaboomN)`. The ramp **is** the event's duration, so the call is due at the top. |
| `balloont_dieN` | `b_turretN` killed | swaps the skin, calls two sequences, runs `StopSequence(flame_light_seq)`, then `CallAnimation(large_fireball)`, `Sound`, and at `Event + 2.0` both `CallAnimation(ball_kaboomN)` and `ObjectActiveState(b_turretN)`. |
| `ball_kaboomN` | `bontN` killed (health 30), or called | debris, fireball, sound, the `balloon_downaN` fall, and `CallAnimation(balloont_dieN)` back. |

The decoded original, from [`org/sequences.md`](org/sequences.md), and where we diverge:

| The original | Ours |
|---|---|
| One node bound per reference, at load; the runtime reads an index (`FUN_004efaf0`, `FUN_004e8d60`) | a per-event list |
| Every tier compares case-sensitively, with no `.flt` stripping | `OrdinalIgnoreCase` plus a `.flt` suffix fallback |
| `*`/`#` on a definition's NAME multiplies INSTANCES (`FUN_0059d610`, `FUN_0051ff40`) | read as node-name patterns |
| `CALL_ANIMATION` does not re-anchor its callee: `PUSH 0x0` at `004eb53d`, the site arriving as `INPUT_NODE` at `callee+0x7c` | the call site becomes the callee's Start anchor (`AnimRuntime.cs:2276`, `var callAnchor = siteNode ?? anchor`); flagged as an undeliberate difference at `org/sequences.md:567-569` |

Two further decoded facts this plan leans on: a `StopSequence` also reaches the **caller's own**
runner and takes effect within the same tick (`org/sequences.md:528-534`), and sequence identity for
"is this running" is the sequence **object**, never its name (`:532-534`).

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

### Wave A — re-observe

1. ☑ Re-fly C3/M02 and record which of the four symptoms survive on the current build

### Wave B — the four faults, one item each

2. ❌ (a) One `tbaseN` kill raises all six balloons
3. ❌ (c) A wrongly-risen balloon never detonates at the top of its 24 s rise
4. ❌ (d) A `b_turretN` kill leaves the balloon hanging, with no `ball_kaboomN`
5. ❌ (b) The killed `tbaseN` drags upward with its balloon

### Wave C — close

6. ☐ Retire `BL-348` once all four paths are confirmed

## Dependency and parallelism notes

A1 blocks all of Wave B: every B item's first question is whether its symptom still reproduces, and
A1 answers all four at once in a single flight. A1 may also close B2 and B5 outright as
already-fixed, which is the cheapest outcome available here.

Within Wave B the four items are independent by construction (the entry's own trap (ii): four
triggers, four candidate causes, no shared proof), so they can run in parallel **except** for file
contention. B2 and B5 both plausibly land in `Mech3/Anim/NameResolver.cs`; B3 and B4 both plausibly
land in `Mech3/SequenceRunner.cs` and the motion-completion path (`Mech3/Anim/MotionSet.cs`,
`Mech3/AnimRuntime.cs`). Run at most one of {B2, B5} and one of {B3, B4} concurrently, and give each
agent its file-ownership boundary in writing: the resolver pair owns `Anim/NameResolver.cs`, the
sequence pair owns `SequenceRunner.cs` + `Anim/MotionSet.cs`. Neither pair edits `AnimRuntime.cs`'s
`CallAnimation` arm without saying so, since both may want it.

C6 needs all four B items resolved (landed or disproven).

---

# Wave A — re-observe

## A1 ☑ Re-fly C3/M02 and record which of the four symptoms survive on the current build

**Goal.** A current, dated record of which of `BL-348`'s four symptoms still reproduce, so no item in
Wave B is chasing a fault that is already gone.

**Evidence (confidence: traced).** The four symptoms were observed at the controls on 2026-08-13 and
have not been re-observed since. `BL-415` has landed in the meantime: `NameResolver.ResolveScoped`
now filters **every** one of its three tiers through `AdmissibleStaging`
(`Mech3/Anim/NameResolver.cs:200-216`, the filter itself at `:399`), so a pooled template copy
answers only the definition that owns or reaches it. That is the same fault shape as symptom (a) (a
tier searching a shared subtree where the original searches the definition's private copy), and the
in-code note at `:396-398` states outright that narrowing one tier alone just hands the same foreign
copy to the next tier down, which is what the pre-`BL-415` code did. Whether that moved the
over-trigger is unmeasured. The entry carries the re-observe instruction itself.

**Approach.** Launch `--chapter=C3 --mission=M02`. Run all three kills in one sitting, on separate
balloons, and record each outcome:

1. Kill one `tbaseN`. How many balloons rise? Does the killed anchor stay grounded?
2. For each balloon that rose, does it detonate at the top or vanish?
3. Kill one `b_turretN` on an untouched balloon. Skin swap only, or the full `ball_kaboomN` two
   seconds later?

Take the `--debug-damage` overlay and, per `CLAUDE.md`'s note on the file sink, the `--debug-anim`
flag as well: absence of a log line is not evidence unless the sink and the gate are both known to
be carrying that line. Do not fix anything in this item.

**Model recommendation.** Not an agent item at all; this is a human at the controls. The write-up of
what was seen is medium tier at most.

**Observation, recorded at the controls.** None of the four symptoms reproduce. All three kills were
flown in one sitting, on separate balloons:

1. Killing one `tbaseN`: exactly one balloon rises, and no other. The killed anchor deactivates and
   stays planted; the tether rope burns and shrinks as the balloon climbs, rather than stretching or
   dragging the anchor upward.
2. The balloon raised by that kill detonates cleanly at the top of its rise, full `ball_kaboomN`
   (debris, fireball, sound, fall); the animation reads correctly.
3. Killing a `b_turretN` on a separate, untouched balloon: the skin swaps, then the full
   `ball_kaboomN` runs two seconds later, matching the authored `Event + 2.0` pair.

The build under test was `worktree-bl348-balloon-kill-chain`; a diff against `main` at the time of
this flight shows the two branches identical in every source file, differing only by this plan
document and the `PROJECT_CONTEXT.md` pointer to it, so the animation code exercised is the same
code either checkout would have run.

**Verify.** The item's deliverable is the observation record, so it verifies itself. The one failure
mode to guard: confirm which build is actually running before flying. A live symptom is evidence
about the build that was running, and this repo has parallel worktrees that can put a different
binary under the same launch command.

**⚠ Traps.** Do not shoot `tether1` and count it as a fifth symptom; see wrong-claim 1 above. Do not
run all three kills on the same balloon, since (a) and (d) contaminate each other. Record what you
saw, not what you concluded.

# Wave B — the four faults, one item each

## B2 ❌ (a) One `tbaseN` kill raises all six balloons

**Outcome.** Disproven by A1's re-fly: killing one `tbaseN` on the current build raises exactly one
balloon. Closed as fixed-by-`BL-415`, per this item's own stated expected outcome.

**Goal.** Killing one balloon's ground tether anchor raises that balloon and no other.

**Evidence (confidence: lead-only).** Observed at the controls 2026-08-13 on a pre-`BL-415` build,
and not since. The cause is unknown and three candidates are already retired (wrong claims 2, 3 and
5 above): it is not a resolver name leak in the original's terms, not the `*`/`#` divergence (M02
authors no wildcard), and not our call guard. What remains as a candidate is our per-event **list**
where the original holds a single bound index, reached through a tier that pre-`BL-415` could return
a foreign pooled copy. `A1` may retire this item outright.

**Approach.** If A1 shows it gone, close the item as fixed-by-`BL-415` and say so in the commit; that
is the expected outcome and a legitimate one. If it still reproduces, instrument the resolution of
`balloon_upN` at the `tbase_kaboomN` call site: log the candidate list each tier returns and which
tier answered, then compare against the exact digit the def authored. The divergence to test first
is the per-event list, since a single-answer resolve cannot raise six. `OrdinalIgnoreCase` and the
`.flt` fallback cannot merge `balloon_up1` with `balloon_up2`, so they are not candidates for this
symptom specifically. <TODO: decide, once the tier is known, whether the fix is to narrow that tier
or to make the resolve single-answer as the original is; the second is the faithful shape but has a
blast radius across every def in the game.>

**Model recommendation.** High. The suspect module is shared by every animation in the project, so a
narrowing that overshoots breaks chapters this item never touches.

**Verify.** Kill one `tbaseN` on C3/M02: exactly one balloon rises. Then the full 8-chapter
`--freecam --chapter=<X>` regression with zero errors and unchanged mesh/node counts, plus the
`bind-census` output compared against a baseline taken **before** the change — an unchanged number
is not evidence unless you have seen it able to fail.

**⚠ Traps.** Do not "fix" this by making the resolver stricter globally without the census baseline;
the resolver has already had one authored stop fail to reach its emitter that way
(`NameResolver.cs:197-199`). Do not assume this item's cause explains (c); the entry is explicit that
they are opposite shapes.

## B3 ❌ (c) A wrongly-risen balloon never detonates at the top of its 24 s rise

**Outcome.** Disproven by A1's re-fly: the balloon raised by a `tbaseN` kill on the current build
detonates cleanly at the top of its rise. Closed as no longer reproducing.

**Goal.** Every balloon that runs `balloon_upN` fires the `CallAnimation(ball_kaboomN)` that follows
its rise, and detonates at the top.

**Evidence (confidence: direction-sound).** The five wrongly-triggered balloons *do* play their own
correctly-numbered `balloon_upN`, and simply vanish at the top instead of exploding. That rules the
symptom out as a name leak and into a dropped completion. `balloon_upN`'s second sequence is
`ObjectMotionFromTo(bontN, run_time 24.0)` then `CallAnimation(ball_kaboomN)`, and the decode
establishes that the ramp is the event's own duration, so an incomplete motion loses the trailing
call. The magnitude side (what fraction of the ramp actually completes, and why it stops) is
unmeasured.

**Approach.** Reproduce with a legitimately-risen balloon, not only a wrongly-triggered one, so the
item stands independently of B2. Instrument the motion's termination: does the
`ObjectMotionFromTo(bontN)` reach its 24-second end and report completion to the sequence runner, or
does it terminate early? `docs/org/objectMotion.md` holds the original's termination model
(including the retired readings); read it before concluding our termination is right. Then follow
the completion report into `SequenceRunner.cs` and check the trailing event actually fires.

**Model recommendation.** Medium. The mechanism is already named; this is a trace along a stated
path rather than an open search.

**Verify.** On C3/M02, a balloon raised by its own `tbaseN` kill detonates at the top of the rise
with debris and fireball, roughly 24 seconds after the kill. Regression surface: the
`object-motion` suites and the full 8-chapter freecam pass, since 379 apex-less `OBJECT_MOTION`
falls ride the same contact model.

**⚠ Traps.** Sequence identity is the sequence **object**, never its name — `he_ground_effect` ships
two unnamed sequences, and a name-keyed test collapses them and loses half the burst. Halting a
runner does not touch what it already launched: motions and puffers have authored lifetimes and
outlive the sequence that started them, so "the balloon still rose" does not prove the runner
survived.

## B4 ❌ (d) A `b_turretN` kill leaves the balloon hanging, with no `ball_kaboomN`

**Outcome.** Disproven by A1's re-fly: killing a `b_turretN` on an untouched balloon on the current
build swaps the skin, then runs the full `ball_kaboomN` two seconds later, matching the authored
`Event + 2.0` pair. Closed as no longer reproducing.

**Goal.** Killing a slung turret swaps the balloon to its destroyed skin and, two seconds later,
runs the full `ball_kaboomN`: debris, fireball, sound and the `balloon_downaN` fall.

**Evidence (confidence: direction-sound).** What is missing is exactly, and in order, everything
`balloont_dieN` schedules **after** its `StopSequence(flame_light_seq)`: `CallAnimation(large_fireball)`,
the `Sound`, and at `Event + 2.0` both `CallAnimation(ball_kaboomN)` and
`ObjectActiveState(b_turretN)`. What survives is exactly what precedes the stop (the skin swap). The
decode gives the mechanism that produces that cut: a stop also reaches the **caller's own** runner
and takes effect within the same tick (`org/sequences.md:528-534`). The entry states this reading
directly. Unmeasured: whether our `StopSequence` is matching `flame_light_seq` by name and therefore
hitting more sequence objects than the original would.

**Approach.** Reproduce on an untouched balloon (a `b_turretN` kill with no prior `tbaseN` kill on
the same balloon). Trace `StopSequence`'s selector in `SequenceRunner.cs`: what set of running
sequence objects does `flame_light_seq` resolve to, and does the caller's own runner end up in it?
Compare against the decoded rule, whose test is object identity. Confirm the `Event + 2.0` pair is
scheduled before checking whether it fires, so a scheduling bug and a stop bug are told apart.

**Model recommendation.** Medium. Same shape as B3, a named mechanism to confirm or refute, on a
module with an existing headless test seam (`ISequenceHost`).

**Verify.** On C3/M02, kill one `b_turretN`: the skin swaps, the fireball and sound play, and two
seconds later the balloon detonates and falls. Regression: the sequence-runner suites, which are
headless, plus the 8-chapter freecam pass. `stop_p1trail` is the known adjacent case: the decode
records that this teardown never runs in the original in any chapter, so do not "fix" it into
running.

**⚠ Traps.** The name-keyed identity trap above applies here most of all, since this item is about a
stop's reach. Do not widen the fix into the `callAnchor` divergence without evidence that it drives
this symptom; that divergence is real but is a separate change.

## B5 ❌ (b) The killed `tbaseN` drags upward with its balloon

**Outcome.** Disproven by A1's re-fly: the killed `tbaseN` stays planted while its balloon rises; the
tether rope burns and shrinks rather than stretching or dragging the anchor upward. Closed as
fixed-by-`BL-415`, per this item's own stated expected outcome for A1 closing it outright.

**Goal.** A killed ground tether anchor stays on the ground. Only its three debris chunks move.

**Evidence (confidence: lead-only).** Observed as visible tether stretching and ripping, with the
anchor body rising alongside the balloon. Nothing in either def's `ObjectMotion*` targets the other's
node: `tbase_kaboomN`'s own death sequence moves only its debris chunks, and `balloon_upN` moves
`bontN`. So the coupling is not authored, which points at the transform/parenting relationship
between `tbaseN` and `bontN` rather than at any event. No mechanism identified. Shares no proven
cause with the other three symptoms.

**Approach.** Confirm it still reproduces (A1). Then inspect the scene parenting under a balloon
instance: is `tbaseN` a descendant of `bontN`, or of a node `balloon_upN`'s motion moves? A
destroyed-state reparent or a pooled-copy staging that puts the anchor under the wrong root is the
kind of thing that produces this with no authored motion at all. <TODO: name the instrument, the
node-tree dump or overlay that shows a live instance's parenting at runtime, which this session did
not establish.>

**Model recommendation.** Medium. Open search, but narrow: one scene subtree, one relationship.

**Verify.** Kill a `tbaseN` on C3/M02 and watch the anchor: it stays put while the balloon rises, and
the tether behaves as it does in the original. <TODO: name the `OriginalScreenshots/` or capture
reference for what the tether should look like during the rise, if one exists.>

**⚠ Traps.** Do not fix this by pinning the anchor's transform in code. If the parenting is wrong,
the pin hides it and the same wrong parent will bite the next thing that moves. Video-derived
distances have failed repeatedly in this project; if the question becomes "how far did it move", that
is not a measurement footage can settle.

# Wave C — close

## C6 ☐ Retire `BL-348` once all four paths are confirmed

**Goal.** `BL-348` is deleted from `backlog.md`, with the outcome of all four investigations recorded
in the closing commit's message.

**Evidence (confidence: lead-only).** The entry's own trap (ii): four independent symptoms, four
candidate causes, each to be verified independently before closing. Some will land code and some may
close as already-fixed or disproven; both are outcomes, and both belong in the record.

**Approach.** Run the `close-backlog-item` skill on `BL-348`. It retires the ID, logs the outcome in
the closing commit, and strikes the caveat everywhere it was restated. Two restatements are known and
must be struck or updated: `docs/plans/PLAN-M4-ai.md:501-503` (which tells C9 to verify against a
chapter without this bug and to keep the fix separate) and `CSVM/src/Testing/AiTargetingAndZeppelinSuites.cs:662`
(which records that this bug and its fix stay out of that item). `docs/org/sequences.md:569` names
`BL-348` as the place the `callAnchor` divergence is a candidate cause; update that line to say what
the investigation actually found.

**Model recommendation.** Medium. Mechanical, but it touches three files that each state the bug's
status, and getting one wrong leaves a stale caveat behind.

**Verify.** `git log --grep=BL-348` shows the closing commit and its four-symptom record; a repo-wide
grep for `BL-348` returns only history, not live prose.

**⚠ Traps.** Do not close on three of four. If one symptom ends unreproducible rather than
understood, say that in the commit message in those words, so a later recurrence is recognised as a
recurrence.
