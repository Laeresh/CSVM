# AI flight — the plant, then the pilot

**ACTIVE PLAN** (written 2026-08-15). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

M4 delivered the AI's decisions (modes, target ranking, the maneuver library, gunnery, voice) but
not the AI's flying. `Flight/AiPilot.cs` converts those decisions into stick with an invented
control law, and the `FlightModel` it drives has no AI branch at all, while the original runs its AI
aircraft on a measurably different force path. This plan closes both halves, in that order: port the
AI force path that is already decoded and sitting unimplemented (wave C), then decode the original's
own AI control law and replace the placeholder with it (waves D and E). The invented constants that
prop the placeholder up (the altitude leash, `BankPull`, `PatrolThrottle`, `AiNetFollower`'s 200 m
arrival radius) are retired as a consequence of E, not tuned as an end in themselves.

Three items from `backlog.md` ride in, all of them decoded-and-unimplemented pieces of the same
force path. `BL-330`'s low-speed control-authority ramp and the reverse-authority factor come from
`FUN_0048bdd0`, which is shared, and the ramp is the likely reason the AI speed floor exists.
`BL-172`'s `bounce_factor` restitution carries a sixth decoded player/AI divergence ("only the
player bounces") and lands on the seam C21 builds. All three affect the player, so this plan changes
player flight feel as well as AI flight feel, and owes a player-side playtest for it. `BL-330` and
`BL-172` were both re-verified still-open in this session against the code (`FlightModel.Step`
applies no speed term to pitch or roll; nothing under `CSVM/src` reads `bounce_factor`) and against
[`org/flightModel.md`](org/flightModel.md)'s own "decoded and UNIMPLEMENTED" headings.

`BL-095` is deliberately **not** an item here, because it is not work. Its own entry declares the
physics block "DECODED END TO END. What is left is implementation, not research", and its remaining
to-do list is `BL-330` plus `BL-172`, both of which this plan lands. It therefore retires as a
consequence of C24 and C25 rather than through an item of its own.

## Milestone goal

- AI aircraft fly the original's force path: its airflow treatment, its weathervane rule, its speed
  floor, its ground blow, and its air-density band, selected by a branch our `FlightModel` does not
  have today.
- The original's AI control law is located in the binary, decoded, documented in `docs/org/`, and
  ported, or its absence is recorded as a named dead end with what was searched.
- No invented constant survives in `AiPilot` or `AiNetFollower` that exists only to stabilise the
  placeholder law.
- The result is judged against footage of the original's AI and at the controls, not only against
  our own re-pinned suite numbers.

**Mission-layer AI is out of scope.** Formation flying, wingman orders, scripted mission retargeting
and the danger-zone modes stay unbuilt; this plan is about how one AI aeroplane flies, not about
what a mission tells it to do.

## Decisions (2026-08-15)

| # | Question | Decision |
|---|---|---|
| 1 | Complete `AiPilot` by decoding, by porting the already-decoded physics, or by tuning the placeholder? | **Decode (waves D and E), sequenced through the already-decoded physics (wave C).** C changes what the law is steering, so doing E first means doing it twice |
| 2 | Trust the static reading that AI aerodynamics run on the thin density band? | **No, verify in Ghidra first (A1).** It is recorded as "static reading, not runtime-verified", and it decides whether the altitude leash is tuned or deleted |
| 3 | Who takes the player force path when 2 to 4 humans fly at once? | **Every human-piloted aircraft (`IsHumanPiloted`).** The original's guard is a single global player pointer, a case our splitscreen makes meaningless; recorded as a named divergence |
| 4 | How is the AI force path selected in code? | **A `readonly` flag set at construction, branching at the five decoded sites inside `Step`.** It mirrors the original's own one-function-with-guards shape, and a constant field cannot flip under a golden |
| 5 | What happens to `architecture.md`'s oversized `FlightModel` entry? | **Mechanism rules move into `FlightModel.cs`; the entry keeps purpose, at most three ⚠, and a pointer.** The paragraph-length decode comments already in that file are an accepted exception to the one-line comment rule |
| 6 | What judges the result? | **A new `CAP-` of the original's AI flying, plus new `PT-` items**, with suite re-pins as records rather than verdicts. The capture asks behavioural and comparative questions only, never absolute distances or speeds |
| 7 | How is the plant change A/B'd at the controls? | **A temporary CLI switch, removed when the playtest closes.** Not a permanent flag |
| 8 | Where does the hunt for the control law start? | **The stun function's zeroed offsets first, then the mode-enum writers, then the update dispatcher.** A closed set of write-xrefs cannot miss the function the way a call-tree walk can |
| 9 | Do `BL-330` and the reverse-authority factor ride in? | **Yes.** Same function, same consumer, both decoded and unported, and the ramp is the likely reason the AI speed floor exists |
| 10 | Do `BL-095` or `BL-172` go first? | **Neither.** `BL-095` is a tracking umbrella with no work of its own and retires as a consequence of C24 and C25. `BL-172` rides in as C25 gated on C21: landing it first would put a player/AI test in the collision path before the seam and its divergence rationale exist |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "`AiPilot.cs` is a placeholder", read as a stub to be written | It is a complete 274-line driver wired to `AiModeMachine`, `AiGunner`, `AiNetFollower` and `ManeuverExecutor`. Only its **control law** is invented. Nothing in it needs writing from scratch; the law needs replacing, and its support constants need retiring |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | C22, C23, C24, C25 | Addresses and line cites are in `docs/org/flightModel.md`. Confirm the trace, then implement |
| **Traced statically, never verified at runtime** | A1, A2 | The reading is recorded with addresses but explicitly marked unverified. These items exist to settle it, and either answer is a result |
| **Leads only, no mechanism yet** | D31, E41 | The AI control law has never been located. Budget for investigation; this may end in a documented dead end |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

Everything below is already recorded in [`org/flightModel.md`](org/flightModel.md), found while
decoding the **player** path and set aside for M4's AI work. None of it is implemented.

| Fact | Where | Address |
|---|---|---|
| AI throttle is read as a target speed, `fd_speed · throttle` | flightModel.md:1060 | `0x48c593` |
| AI aerodynamics skip the altitude zeroing, so the thin density band is reachable | flightModel.md:189 | `0x48c883` |
| AI always uses nose-aligned wind, i.e. permanently zero incidence | flightModel.md:217 | (in text) |
| AI does not get weathervane centring | flightModel.md:791 | (in text) |
| Forward-velocity floor of 4.4704 m/s (10 mph), player exempt | flightModel.md:137 | (in text) |
| AI ground blow is a fixed push, linear in proximity, not `dt`-scaled, factor 0.15 | flightModel.md:1339 | `0x0048c317` |
| Per-AI random jitter of `fd_speed` and `ThrustFactor` | flightModel.md:1004 | `FUN_00477280` |
| The bank-coupling block is inlined a second time on the AI path behind a byte flag | flightModel.md:585 | `0x48cc61`–`0x48ccf4` |
| Control authority ramp, shared, 0 at `turn_fade_in` = 10 mph | flightModel.md:471, 499 | `FUN_0048bdd0` |
| Reverse-authority factor, decoded, unimplemented and unowned | flightModel.md:488 | `FUN_0048bdd0`, consumed by `FUN_0048c470` |
| AI mode enum, with mode 0 sub-dispatched on three flags | flightModel.md:1354 | `obj+0x358`; flags `obj+0x948`, `obj+0xBA`, `obj+0x2F0` |
| Stun zeroes the AI's control inputs | flightModel.md:1366 | `FUN_004200d0` |
| The three stick channels are summed into the torque accumulator | flightModel.md:1308 | inside `FUN_0048c470` |
| Per-object update dispatch on the vehicle class | flightModel.md:1391 | `FUN_00489ea0` |
| Only the player bounces; AI gets position correction alone | `BL-172`, flightModel.md "Collision response" | `FUN_0048d7f0` |

The 11 pinned shots in `analysis/goldens/manifest.json` contain **no AI aircraft**, so wave C cannot
move a golden hash. The in-engine suites that do fly AI (`ai-modes`, `ai-gunnery`, `air-to-air`,
`ai-net-follow`, `zeppelin-motion` in `src/Testing/Suites.cs`) all will.

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

### Wave A — Verify the two unverified reads

1. ☐ A1 The thin air-density band on the AI force path
2. ☐ A2 Whether AI throttle is a speed setpoint or a lever

### Wave B — Documentation migration

11. ☐ B11 Move `FlightModel`'s mechanism rules out of `architecture.md` into the code

### Wave C — Port the decoded plant

21. ☐ C21 The AI force-path seam, plus the temporary A/B switch
22. ☐ C22 The AI aerodynamic deltas: airflow, weathervane, density band, speed floor
23. ☐ C23 The AI ground blow, a different law
24. ☐ C24 `BL-330`'s authority ramp and the reverse-authority factor
25. ☐ C25 `BL-172`'s `bounce_factor` restitution, player-only as decoded

### Wave D — Decode the control law

31. ☐ D31 Locate and decode the original's AI control law

### Wave E — Replace the placeholder

41. ☐ E41 Port the control law into `AiPilot`
42. ☐ E42 Retire the placeholder-support constants

### Wave F — Judge it

51. ☐ F51 Capture the original's AI flying
52. ☐ F52 The at-the-controls verdict, AI side and player side

## Dependency and parallelism notes

A1 and A2 are read-only Ghidra work and can run together. A1 gates C22 (it decides whether the
density branch is part of that item, and whether E42's altitude leash is deleted or re-derived), and
A2 gates C21's throttle semantics. B11 touches only `docs/architecture.md` and `FlightModel.cs`
comments and can run at any time before C21, but **not in parallel with C21 or C22**, which edit the
same file. C21 blocks C22, C23 and C24 (they all branch on its flag), and C25 is gated on it for
consistency rather than necessity (see that item). C24 and C25 are the items that change player
flight, so they can land independently of C22 and C23 if the AI half stalls, and `BL-095` retires
once both are in. C22, C23 and C24 all edit `FlightModel.cs` and must not run in parallel with each
other; C25's fix site is `FlightController.SurviveHit`, so it is the one C-wave item that can run
concurrently with the others. D31 blocks E41; E41 blocks E42. F51 can be shot as soon as the plan
starts and does not depend on any code item; F52 needs C and E landed, and the switch still present.

---

# Wave A — Verify the two unverified reads

## A1 ☐ The thin air-density band on the AI force path

**Goal.** Settle whether the original's AI aircraft really fly with air density 16.7× lower than the
player's, which would mean near-zero aerodynamic forces, or whether the band threshold is written
somewhere and the AI runs dense like the player.

**Evidence (confidence: traced statically, never verified at runtime).**
[`org/flightModel.md`](org/flightModel.md):189 records that the force accumulator's second call site
at `0x48c883`, on the AI-side flight path, does not perform the save / store-0 / restore of
`[obj+0x208]` that the player call site does at `0x491250`–`0x491284`. With the band threshold read
as `0.0` and the comparison `alt ≤ threshold → dense` at `0x41aca4`–`0x41acc4`, any AI above sea
level selects the thin band, ρ = 1.3560e-4 against the dense 2.2688e-3 (flightModel.md:166). The
document marks this a static reading and flags it for exactly this plan. Already ruled out: the
player side, which is proven dense from the bytes and independently corroborated by the stall
arithmetic.

**Approach.** In Ghidra, enumerate every write xref to the band-threshold `DAT` read at
`0x41aca4`, and confirm none of them runs before AI flight (a constructor-time or config-time write
would change the answer). Then re-read the AI call site around `0x48c883` and its caller to confirm
the zeroing really is absent rather than hoisted. Record the result in
[`org/flightModel.md`](org/flightModel.md)'s Atmosphere section, replacing the ⚠ with a settled
statement either way. No engine code changes in this item.

**Model recommendation.** high. A binary read where a missed write xref silently produces the wrong
plant for every AI aircraft in the game.

**Verify.** The answer is the deliverable. State the xref list explicitly, including the empty case,
so the next reader can see the search was exhaustive rather than trust the conclusion.

**⚠ Traps.** An unwritten `DAT` reading `0.0` in a static dump is not proof that nothing writes it at
runtime; the point of this item is the xref sweep, not re-reading the same bytes. Do not implement
anything on this item's finding without it, and do not let a plausible answer here override the
runtime behaviour observed later in F52.

## A2 ☐ Whether AI throttle is a speed setpoint or a lever

**Goal.** Determine whether `fd_speed · throttle` at `0x48c593` is a target speed the AI's engine
control chases, or a term inside a lever-style thrust calculation. `AiPilot.Throttle` is a lever
today, and `SteerLayOff` walks it at an invented rate; if the original holds a speed, both are the
wrong shape.

**Evidence (confidence: traced statically, never verified at runtime).**
[`org/flightModel.md`](org/flightModel.md):1060 lists `fd_speed · throttle` as "an AI target speed"
among three uses of `fd_speed` as a normalising reference, and warns that `fd_speed` is
`FakeDynSpeed`, not a top speed. `[obj+0x124]` and `[obj+0x128]` are recorded at flightModel.md:1065
as the commanded and current throttle, with a slew at `[obj+0x134] -= dt · throttle · 5` and
`FUN_00491820` snapping them together, so a two-value throttle with a rate limit already exists on
the object.

**Approach.** Read `0x48c593` in context: what consumes the product, and whether the result feeds a
force term directly or an error term against `[obj+0x934]` (the true speed). Follow `[obj+0x124]`
and `[obj+0x128]` write xrefs on the AI path to see who commands AI throttle and at what rate.
Record the finding in [`org/flightModel.md`](org/flightModel.md). No engine code in this item.

**Model recommendation.** high. This decides the shape of the AI's throttle interface, which C21
then builds and E41 depends on.

**Verify.** The answer is the deliverable, cited to addresses.

**⚠ Traps.** `fd_speed` is not the top speed and must not be treated as one; flightModel.md:1062 is
explicit about this. A "target speed" reading that quietly assumes the AI reaches it is the same
error in a new place.

# Wave B — Documentation migration

## B11 ☐ Move `FlightModel`'s mechanism rules out of `architecture.md` into the code

**Goal.** `docs/architecture.md`'s `src/Flight/FlightModel.cs` entry runs about 50 lines against the
file's own stated ceiling of roughly 12 for the heaviest module, and its hard limit of three ⚠ per
module. After this item it carries purpose, at most three ⚠ that an outside caller can actually
violate, and a pointer to [`org/flightModel.md`](org/flightModel.md), with no mechanism prose left
in it.

**Evidence (confidence: traced).** `docs/architecture.md`:3294 onward, against
`PROJECT_CONTEXT.md`'s budget rule ("body ≤ ~8 lines, ~12 for the heaviest; max 3 ⚠ per module").
Much of the entry is already duplicated verbatim in `FlightModel.cs`: thrust availability and the
attitude scale at lines 78–121, the lift demand and the density band at 129–182, the weathervane
decode at 309–331.

**Approach.** Apply one rule. If a fact is only needed while editing the line that computes it, it
becomes a comment at that line, or is deleted where the comment already says it. If it is needed
before deciding to touch the module at all, it stays in `architecture.md`. Anything that is a
measurement trap goes to `docs/verification.md` instead of either. `FlightModel.cs`'s existing
paragraph-length decode comments are an accepted exception to the one-line comment convention, and
this item may add to them. Land as its own commit with no code change, so wave C's diff is readable.

**Model recommendation.** medium. Mechanical text movement against a stated rule, with judgement
only at the comment-versus-entry boundary.

**Verify.** `docs/architecture.md`'s entry is under the budget and has at most three ⚠; no fact
present before the move is absent after it (it is in the code, in `verification.md`, or was a
duplicate). `.\RunTests.ps1` is untouched by construction, but run it to prove no comment edit
disturbed a line.

**⚠ Traps.** Do not delete a fact because it "sounds like a decode" and assume
[`org/flightModel.md`](org/flightModel.md) has it; check that it does before dropping it. The entry
is what a future session reads *before* deciding to modify the module, so stripping the constraint
lines defeats the purpose of the file.

# Wave C — Port the decoded plant

## C21 ☐ The AI force-path seam, plus the temporary A/B switch

**Goal.** `FlightModel` can flow either the player force path or the AI one, selected once at
construction, with no behaviour change yet. A temporary CLI switch flips AI aircraft back to the
player path so the change can be A/B'd at the controls.

**Evidence (confidence: traced).** The original branches inside one function on a pointer compare
against the single global player object (`flightModel.md`:617). Our two production construction
sites are `Session/FlightRigAssembler.cs`:388 (human) and `Session/AiAircraftSpawner.cs`:145 (AI);
about 18 test sites use `new FlightModel(stats)`. `FlightController.IsHumanPiloted` already exists
and is read by `AiPilot.Next` for the lay-off decision.

**Approach.** Add a `readonly bool` set from a constructor parameter, defaulted to the player path so
the test sites compile unchanged, and pass it from the two production sites off `IsHumanPiloted`.
Name it for the force path it selects, not for who is flying. Add the temporary switch following the
existing `--no-assist` pattern, documented in [`cli.md`](cli.md) as temporary and owned by F52.
Record in `docs/architecture.md` (post-B11) the divergence from the original's single-player guard,
quoting the original's own compare, so the next reader sees the byte and the reason we did not
follow it.

**Model recommendation.** medium. Small and mechanical, but it touches the CLI parser's accepted-flag
count and `docs/cli.md`'s index, which `PROJECT_CONTEXT.md` requires to stay equal.

**Verify.** `.\RunTests.ps1` fully green with no re-pins, because this item changes no arithmetic;
that is the point of landing it alone. Confirm the flag count and `cli.md`'s index still match.

**⚠ Traps.** The optional default means a future production construction site silently gets the
player path. With two sites today that is acceptable, but a third one added later is a real risk;
say so at the constructor. Do not make the flag mutable, a plant that can change mid-flight makes a
golden or a suite run unreproducible.

## C22 ☐ The AI aerodynamic deltas: airflow, weathervane, density band, speed floor

**Goal.** An AI aircraft flies the original's aerodynamics: nose-aligned wind always (zero
incidence), no weathervane centring, its own air-density band per A1, and a forward-velocity floor of
4.4704 m/s applied after integration.

**Evidence (confidence: traced, except the density band which A1 settles).** flightModel.md:217
(AI skips the airflow blend and always uses nose-aligned wind), :791 (weathervane is player only),
:137 (the 10 mph floor along the nose axis, applied after integration, player exempt), :189 with A1
for the band. `FUN_00477280` (flightModel.md:1004) additionally jitters `fd_speed` and
`ThrustFactor` per non-player aircraft, which belongs with this item if it is cheap, and is
otherwise a `<TODO: split into its own item if the jitter's distribution is not readable from the
function>`.

**Approach.** Four guarded branches at the existing sites in `FlightModel.Step`, each carrying the
original's address in a one-line comment. The airflow branch bypasses the `liftAOAs` cosine window
rather than reimplementing it; the weathervane branch skips `WeathervaneTorque()`; the floor is a
post-integration clamp on the nose-axis component only, not on speed. Re-pin the AI suites, with
before-and-after numbers in the commit message.

**Model recommendation.** high. Four coupled changes to the force path, each of which changes every
AI number we have, and where a sign or an axis error flies plausibly and is wrong.

**Verify.** The suites re-pin with a stated reason per moved number. Take the baseline first:
`docs/verification.md`'s rule that an unchanged number proves nothing unless it was able to fail
applies directly, since three of these four branches are skips, and a mis-wired flag would show as
"no change". A/B at the controls with C21's switch is F52's job, not this item's.

**⚠ Traps.** Do not tune anything in `AiPilot` to keep an old suite number green; the plant moving is
the intended result, and E42 is where the law's constants are addressed. The thin band, if A1
confirms it, means near-zero aerodynamic forces, so an AI aircraft that suddenly behaves like a
thrust-and-torque object is the expected outcome and not evidence of a bug. The floor is on the
nose-axis velocity component, not on `Speed`; conflating them changes behaviour in a dive.

## C23 ☐ The AI ground blow, a different law

**Goal.** AI aircraft get the original's AI ground blow: a fixed push independent of the AI's own
command, linear in proximity rather than quadratic, at factor 0.15.

**Evidence (confidence: traced).** flightModel.md:1339, `0x0048c317`, describing the AI path as "a
different law, not a scaled one", with both the factor and `S` cut to 0.15, and `ai_groundblow`
authored at 0.5 against the 0.9 fallback (flightModel.md:1128). `BL-359` landed the player ground
blow on 2026-08-15 and closed noting the AI law is unbuilt and different.

**Approach.** Branch inside the existing ground-blow implementation on C21's flag. Reuse the probe
and its falloff unchanged; only the response differs.

**Model recommendation.** medium. A single well-bounded branch against a fully traced decode, in
code that landed this month.

**Verify.** An AI aircraft flown at terrain in `--stage=empty` and in a chapter world visibly repels
without the player law's command-proportional term. Re-pin whatever in `air-to-air` moves.

**⚠ Traps.** flightModel.md:1346 records that the AI law is **not** multiplied by `dt` anywhere, so
it is frame-rate dependent in the original. At our fixed 60 Hz sim that is a choice: reproduce it
literally, or normalise it and say so. Decide when the code is in front of you, and record which,
because a silent `dt` insertion is exactly the kind of quiet correction that makes a later
comparison against footage unfalsifiable. A stunned AI has its ground blow suppressed
(flightModel.md:1369) and flies into terrain deliberately; do not "fix" that.

## C24 ☐ `BL-330`'s authority ramp and the reverse-authority factor

**Goal.** Roll and pitch authority fade with airspeed as the original does, for every aircraft, and
`FUN_0048bdd0`'s fifth output softens control above `yaw_max`. `BL-330` closes here.

**Evidence (confidence: traced, corroborated at the controls).** flightModel.md:465–504: the base
ramp is 0 below `turn_fade_in` (10 mph), rising linearly to 1 at `turn_fade_out` (fallback 40,
authored 50), and applies to roll and pitch; roll has no high-speed fade. The reverse-authority
factor is decoded at flightModel.md:488, returns `max(yawAuthority, 0.2)` above `yaw_max` (authored
50 mph) and 1.0 at or below it, and is consumed by `FUN_0048c470`. `FUN_0048bdd0` is called from
`FUN_0048c470` (flightModel.md:78), which is the **shared** force and torque function, so both
outputs reach AI aircraft as well as the player. `FlightModel.Step` today applies the authored yaw
curve and no speed term at all to pitch or roll.

**Approach.** Implement the base ramp as a scalar on roll and pitch torque, reading `turn_fade_in`
and `turn_fade_out` from the authored globals, not the fallbacks, where they are available. Add the
reverse-authority factor at its consumption site in the torque path. Both are unbranched: they apply
to player and AI alike. Delete `BL-330` from `backlog.md` in the landing commit, with its record in
the message.

**Model recommendation.** high. This changes player flight feel, which is the highest-blast-radius
surface in the project, and `BL-330` carries an explicit warning against filling flight-model holes
with invented rate limiters.

**Verify.** The golden shots are player-side and *can* move here; take the baseline first, and treat
any moved hash as requiring an explicit justification rather than a re-pin by reflex.
`.\RunTests.ps1` in full. The at-the-controls half is F52's player-side arm, judged against
`BL-330`'s existing corroboration.

**⚠ Traps.** The 10 mph `turn_fade_in` and the AI's 10 mph speed floor are the same number, which is
this plan's working hypothesis for why the floor exists. It is a hypothesis and not a decode: do not
write it into `docs/` as a finding unless D31 confirms it. `BL-330`'s own trap applies with full
force. Do not let this become a speed-versus-bank coupling that quietly costs pitch authority, which
is the wrong-mechanism fix `BL-124` warns about. `maxAOA` and `liftAOAs` are not part of this item.

## C25 ☐ `BL-172`'s `bounce_factor` restitution, player-only as decoded

**Goal.** A player aircraft grazing a surface rebounds along the contact normal as the original does.
AI aircraft keep getting position correction and nothing else, which is what the original gives
them. `BL-172` closes here, and with C24 it retires `BL-095`.

**Evidence (confidence: traced, and independently measured).** `bounce_factor` is a raw scalar
(global `0x0071c35c`, parser store `0x00473c38`, fallback 0.8, authored 0.6), applied in the
collision resolver `FUN_0048d7f0` as a normal-only impulse with no tangential or friction term
([`org/flightModel.md`](org/flightModel.md), "Collision response and `bounce_factor`"). Three
decoded properties shape the implementation: effective restitution is `f_lin × bounce_factor` where
`f_lin = L/(L+A)`, `L = 2.25·|J|`, `A = |Δω|`, so a short lever arm rebounds at up to 0.6 while a
wingtip throws most of the impact into rotation; there is **no** surface dependence anywhere in the
code, no verticality test and no material lookup; and **only the player bounces**, the impulse branch
being entered for the local player alone and only while not already crashed. `CAP-14` measured the
signature independently on 2026-08-04 (seven contacts, two airframes, three surface orientations,
139–302 mph): vertical surfaces e = 0.10 ± 0.05, flat ground e = 0.62 ± 0.19 against a shipped 0.60.
Nothing under `CSVM/src` reads `bounce_factor` today; `FlightController.SurviveHit`
(`FlightController.cs`:1435-1535) does a friction-scaled tangential slide, a lever-arm attitude kick
and a fixed 0.15 m push-out, with no normal-direction term at all.

**Approach.** Add the normal-direction restitution impulse in `FlightController.SurviveHit`
alongside the existing tangential slide, scaled by `f_lin × bounce_factor` from the shipped
constant, gated on the original's own condition (local player, not already crashed). This is
binding a shipped constant, not inventing a pushback mechanic. The gate is C21's rationale applied a
second time, but it is **not** C21's flag: the site is `FlightController`, which already has
`IsHumanPiloted`, and the original's guard here additionally excludes an already-crashed aircraft.
Point the gate's comment at C21's recorded divergence so the two decisions read as one.

**Model recommendation.** high. It changes collision feel, which is a player-facing surface with its
own history of wrong-mechanism fixes, and the impulse's second term is easy to get wrong.

**Verify.** The `CAP-14` split is the acceptance test and it is already measured: a normal-direction
restitution reproduces flat-ground e ≈ 0.6 while leaving a vertical-wall contact's altimeter nearly
untouched, because on a wall the sink is tangential. Reproduce both orientations in a scripted run
before judging feel. `.\RunTests.ps1` in full; the player-side goldens can move here, so baseline
first.

**⚠ Traps.** The measured 0.75–0.86 on flat ground is **above** what `bounce_factor` can produce, and
the decode explains why: the impulse is computed from the contact point's velocity with the
rotational term doubled, then applied in full to the centre of mass with no reaction term
(`n·v_after = −k·(n·v) − (1+k)·2·n·(ω × r)`, `k = f_lin·bounce_factor`). That second term is
unbounded and is not restitution. **Reproducing the original's feel needs that term, not a larger
`bounce_factor`** — raising the constant to chase the measurement is the wrong fix. Already ruled
out and traced as sources: multiple contacts per frame, successive-frame stacking, a separate
ground-support path, and gravity ordering. Do not read the vertical-versus-flat split as a
per-surface coefficient; it is a lever-arm partition, and the code has no surface dependence at all.

# Wave D — Decode the control law

## D31 ☐ Locate and decode the original's AI control law

**Goal.** Identify the function that turns an AI's standing order (a net node, a target, a mode) into
the three stick channels, decode it, and land it as a page under `docs/org/`. If it cannot be found,
the deliverable is a recorded dead end naming what was searched and what would settle it next.

**Evidence (confidence: lead-only).** Nothing in `docs/` names this function. Three anchors converge
on it. `FUN_004200d0`, the stun handler, zeroes the AI's control inputs and therefore names the
offsets the stick channels live at (flightModel.md:1366). `FUN_0048c470` sums "the three stick
channels" into the torque accumulator (flightModel.md:1308) and is the consumer. The mode enum
`obj+0x358`, with mode 0 sub-dispatched on `obj+0x948`, `obj+0xBA` and `obj+0x2F0`
(flightModel.md:1359), sits inside the AI brain, and `FUN_00489ea0` is the per-object update
dispatcher (flightModel.md:1391).

**Approach.** In the decided order. First, read `FUN_004200d0` to recover the control-input offsets,
then take write xrefs on those offsets, subtract the human input path, and the remainder is the AI
control law. Second, take write xrefs on `obj+0x358` and on the three mode-0 flags to confirm the
candidate sits in the AI brain and not on a shared path. Third, walk down from `FUN_00489ea0` to
establish where in the frame the law runs and what it is called with, which matters because our
`AiPilot.Next` runs once per sim step and the original may not. Land the decode as a `docs/org/`
page in the same change, per the standing rule that new decodes land with their docs.

**Model recommendation.** high. Open-ended binary archaeology where the shape of the answer is
unknown.

**Verify.** The recovered law's inputs and outputs account for the flags at `obj+0x948`, `obj+0xBA`
and `obj+0x2F0`, and for every write to the control-input offsets on the AI path. A decode that
leaves a writer unexplained is incomplete, and saying so is the honest outcome.

**⚠ Traps.** The mode machine is already ported from data, so finding the state machine is not
finding the steering; `obj+0x358`'s writers are a corroborator, not the target. Expect the law to be
frame-rate dependent in the same way C23's ground blow is, and record it rather than silently
normalising it. If the search fails, say so plainly. A documented dead end is a result here, and E41
degrades accordingly.

# Wave E — Replace the placeholder

## E41 ☐ Port the control law into `AiPilot`

**Goal.** `AiPilot.Next` produces stick by the original's rule rather than by the invented
bank-to-turn law, through the same class seam the module doc already names.

**Evidence (confidence: lead-only, gated on D31).** `AiPilot.cs`:16–20 and
`docs/architecture.md`:2424 both state that the current law is a placeholder, and that this class is
the seam its replacement lands through. What replaces it is D31's output.

**Approach.** Replace the body of `Next`'s steering, keeping the class's existing contract: mutable
orders (the mission-script rule at `AiPilot.cs`:11), purity over model state and instance fields, and
the mode dispatch that hands off to `ManeuverExecutor` and the machine's own orders. If D31 came back
empty, this item degrades to re-fitting the placeholder against F51's capture on the corrected plant,
and is renamed to say so rather than quietly doing something different from its title.

**Model recommendation.** high. The plan's central item, and the one whose failure mode is a law
that flies plausibly and is not the original's.

**Verify.** `AiPilotTests` and the `ai-modes`, `ai-net-follow` and `air-to-air` suites re-pinned with
stated reasons. The real verdict is F52.

**⚠ Traps.** Do not preserve the invented altitude leash "just in case" while porting. If the ported
law needs it, that is evidence the port is wrong or that C22 is incomplete, and it should be
diagnosed rather than papered over. Orders must stay mutable fields.

## E42 ☐ Retire the placeholder-support constants

**Goal.** No constant survives whose only justification is stabilising the placeholder law.

**Evidence (confidence: traced, as inventions).** `AiPilot.cs` declares `PatrolThrottle` 0.5,
`LayOffThrottleRatePerS`, `LayOffMinThrottle`, `MaxBankDeg`, `BankPerHeadingDeg`, `RollGain`,
`RollRateLead`, `MaxPathDeg`, `PathPerMeter`, `PitchGain`, `PitchRateLead`, `BankPull` 1.2 and the
150/50 m altitude leash, all documented in-file as invented. `docs/architecture.md`:2361 records
`AiNetFollower.DefaultArrivalRadius` (200 m) as invented and "sized to the placeholder law's
tracking error", explicitly to be shrunk when the real law lands.

**Approach.** Delete each constant whose reason for existing is gone. For any that survives, rewrite
its comment to say what it now is: an original value, a TUNE with a measurement behind it, or an
invention that is still an invention and why. Shrink `AiNetFollower`'s arrival radius to what the
ported law actually needs, leaving the zeppelins' wider per-record radius alone. Update the three ⚠
lines in `architecture.md`'s `AiPilot` entry, which currently assert the law is not original.

**Model recommendation.** medium. Mechanical once E41 lands, with judgement on what survives.

**Verify.** No constant in `AiPilot` or `AiNetFollower` is described as invented without a sentence
saying why it still has to be. Suites green after re-pins.

**⚠ Traps.** `LayOffThrottleRatePerS` and `LayOffMinThrottle` support D15's lay-off, whose
`sixth_sense_factor` is decoded but whose speed-match application is invented. That invention is a
separate question from the steering law and may legitimately survive this item. Zeppelin arrival
radii are a different consumer of the same follower, do not change them here.

# Wave F — Judge it

## F51 ☐ Capture the original's AI flying

**Goal.** Footage of the original's AI aircraft flying, sufficient to judge waves C and E
behaviourally.

**Evidence (confidence: traced).** `playtest.md` owes no capture of the original's AI at all; every
existing `CAP-` is player-side or effects-side. There is therefore no instrument today that can
distinguish a faithful AI port from a merely pleasant one.

**Approach.** Mint a `CAP-` with `New-ItemId.ps1 -Kind CAP` (never assign an ID any other way) and
add it to `playtest.md`'s owed-captures table citing this plan. The capture asks behavioural and
comparative questions only: does an AI aircraft gain altitude through a sustained turn or hold it;
is its turn tighter or wider than the player's in the same airframe; does it hold a speed through
manoeuvres or bleed and recover like a lever-driven aircraft; does it wallow at low speed or stay
crisp; what does it do at the end of a patrol leg. Frame so the AI aircraft and, where possible, the
player's own aircraft are both readable.

**Model recommendation.** medium. Writing a capture spec, where the judgement is in what the capture
is *not* allowed to be used for.

**Verify.** The spec is judged by whether the answers it yields could change a decision in C or E.

**⚠ Traps.** No absolute distances or speeds are to be read off this footage. Footage-derived
distances have failed repeatedly on this project, and a decode must never be contested with one. If
the capture appears to contradict a traced address, the capture is the thing in doubt.

## F52 ☐ The at-the-controls verdict, AI side and player side

**Goal.** The plan's changes are judged by the user at the controls, on both arms: how AI aircraft
now fly, and how the player's own aircraft now flies after C24.

**Evidence (confidence: traced).** C24 changes shared control authority and C25 changes collision
response, so the player arm is not optional. `BL-330`'s existing at-the-controls corroboration is
the reference for the authority half; `CAP-14`'s measured restitution split is the reference for the
collision half.

**Approach.** Mint the `PT-` items with `New-ItemId.ps1 -Kind PT` and add them to `playtest.md`. Fly
the AI arm with C21's switch to A/B the plant directly, in free flight against `--ai-attack`, and in
a chapter mission with patrol nets running. Fly the player arm across the airframes `BL-330` was
corroborated on, low-speed handling especially, plus grazing contacts on flat ground and on a
vertical face for C25. Remove the temporary switch and its `cli.md` entry in the closing commit.

**Model recommendation.** medium. The work is running sessions and recording the user's verdict; the
verdict itself is not the agent's to give.

**Verify.** The user's judgement is the verdict, and it outranks the suites. Record it in the closing
commit message.

**⚠ Traps.** Do not let a green suite table stand in for this item. Do not remove the A/B switch
before the playtest is answered; it is the only way to compare the two plants without a rebuild.
