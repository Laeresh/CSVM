# M5 polish run 14: the second pilot's walk, wrecks over water, and the campaign's own screens

**ACTIVE PLAN** (written 2026-09-08). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan takes ten player-visible defects out of `backlog.md` and fixes them: the four that make a
second pilot's walk through the sortie screens behave differently from the first pilot's, the wreck
and turret faults that show whenever a zeppelin is fought over water, the reach a DEDG objective is
decoded to give its watched group, the two campaign screens and props that show the wrong thing on
CM13 and CM15, and the missing positional voice on another aeroplane's guns. It closes with the
sitting that judges the plan's own work and answers two looks the tree already owes.

Every item was drawn from `backlog.md` and re-verified still open in this session against both the
record and the code: `git log --all --grep=<ID>` for all thirteen entries, plus a mechanism read for
each of the six where a stale entry was plausible. `OriginalSeatPlane.cs` has no commit since the
sortie that filed `BL-746` to `BL-749`; `AircraftStage.cs` none since `BL-690` was filed; `git log
-S TryGroundColumn` and `-S DedgMet` since those items' filings are both empty; `DedgMet`
(`ObjectiveGraph.cs:601-616`) still only counts; `AircraftStage.cs:142` still builds a bare
`SceneBuilder`. Two entries were dropped during that pass rather than scheduled, and one whole batch
was deferred; the Decisions table records all three.

## Milestone goal

- A second pilot joins, picks an aircraft and a weapon loadout, backs out of a screen without losing
  their seat, and launches, on Instant Action and on Free Flight alike, driven only by their own
  device.
- A zeppelin killed over water comes to rest on the sea in every one of its pieces, on both hulls
  that break up.
- A zeppelin's rings hold fire on a bearing its own moving hull blocks.
- A watched DEDG group comes to the player from anywhere on the map, as the decoded side effect says
  it does.
- CM13's flight check answers per crew slot and grants the mission's aircraft; CM15's staged
  Balmoral wears the scheme of the aeroplane it stands in for.
- Another aeroplane's guns are heard from where that aeroplane is.

**The plan adds no system the game does not already have.** Ground shadows (`BL-331`), the MPG
cinemas (`BL-446`) and the numpad camera scheme's rebuild (`BL-150`) are each their own milestone,
and a polish run that opened one of them would land none of the ten.

## Decisions (2026-09-08)

| # | Question | Decision |
|---|---|---|
| 1 | What do the ten items optimise for? | **Visible defects that are code-ready.** `[Impact: high]`, `[Next: code]` or `[Next: data]`, size `S` or `M`, no `[Blocked:]` tag. Ten things that are wrong today and can be fixed and verified without asking anyone anything first. |
| 2 | How much of the plan waits on the author at the controls? | **Two entries, batched into one sitting.** `BL-545` and `BL-649` are both already fixed in code and owe only a look, and both are judged from outside the aeroplane on one flight, so they ride the closing sortie instead of stalling an item. |
| 3 | Where is the milestone boundary? | **Ship-quality of what exists, plus a feature that completes something half-built.** `BL-748` (a joined pilot's loadout) is in because the per-seat walk already exists and stops one row short. A feature opening a system the game does not have is not. |
| 4 | What was dropped after re-verification? | **`BL-695` and `BL-322`.** `BL-695`'s question was answered by `PLAN-public-release` A2, which decoded the gasbag section's demolition chain and showed the bay and engine correlation is what the authored data produces; what is left is a re-flight, which `PT-119` already carries. `BL-322`'s own entry refutes its premise: the original modulates those facades exactly as we do, and the residual ratio is not predicted by any decoded lighting term, so it is research, not polish. |
| 5 | Why is run 13's cosmetic sortie not here? | **Deferred, not dropped.** `BL-750` to `BL-769` are the Plane Construction and wallet-free store cosmetics, and all but one are `[Impact: low]`. They are a coherent single-screen run of their own and would have crowded out every high-impact item under Decision 1. |
| 6 | Once Back stops unjoining (A1), what does CANCEL SELECTIONS do on the per-seat screen? | **What its label says: drop the seat's selection and reopen the list, without leaving the walk or the sortie.** Unjoining then exists in exactly two places, Back on the Instant Action screen and the device-lost path, which is what the report asked for. Keeping the button wired to `_setup.Unjoin` would leave an unjoin behind a label that does not announce it. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — The second pilot's walk

1. ☑ `BL-746` Back on the per-seat screen returns instead of unjoining
2. ☑ `BL-747` Only the picking seat's device drives the per-seat screen
3. ☐ `BL-749` A two-pilot Free Flight walk launches on the last confirm
4. ☐ `BL-748` A joined pilot picks a weapon loadout

### Wave B — Wrecks, hulls and reach over the mission map

11. ☑ `BL-698` + `BL-700` Every breakup piece rests on the sea, on both hulls
12. ☑ `BL-714` A moving hull blocks its own rings' fire
13. ☐ `BL-565` An awake DEDG widens its members' engagement volume

### Wave C — The campaign's own screens and props

21. ☑ `BL-689` CM13's flight check answers per crew slot and grants its aircraft
22. ☐ `BL-690` A staged cutscene aeroplane wears its scheme

### Wave D — What another aeroplane sounds like

31. ☐ `BL-079` Another aircraft's weapons have a position

### Wave E — The closing sortie

41. ☐ `BL-545` + `BL-649` The sitting that judges this plan and two owed looks

## Dependency and parallelism notes

Wave A runs strictly in listed order and never in parallel worktrees: all four items edit
`CSVM/src/UI/Menu/Original/OriginalSeatPlane.cs`. A1 comes before A2 for a reason A2 depends on,
namely that removing seat 0's reach into the screen is only safe once the picking seat's own Back is
a reliable way out of the walk. A4 is the only Wave A item that also touches `OriginalLoadout.cs`
and `OriginalSeats.cs`.

B11, B12 and B13 are independent of each other and of Wave A. B11 and B12 both concern a zeppelin in
flight but own different files (`MotionRuntime` against `TurretController`), so they may run in
parallel worktrees. C21 and C22 are independent of everything, as is D31.

E41 is last by definition: it flies the whole plan. It needs every other item landed and merged, and
it is the author's sitting, not an agent's.

---

# Wave A — The second pilot's walk

## A1 ☑ `BL-746` Back on the per-seat screen returns instead of unjoining

**Landed.** No press on the per-seat screen unjoins any more. `BackSeatPlane` keeps one arm for the
picking seat with a selection standing, which takes that selection back and reopens the list, and
every other press of Back, the picking seat's over the open list and seat 0's, goes through
`CancelSeatWalk`. CANCEL SELECTIONS now shares the first arm through a new `ReopenSeatList`, so
Decision 6 lands with the rest of the item: the button drops the seat's selection and reopens the
list without leaving the walk or the sortie. `CancelSeatWalk` unwinds seat 0 to browsing in a loop
rather than one stage, because a sortie screen reopens the walk on every frame seat 0's pick still
stands and a single stage back could put the walk straight up again. The walk is therefore
re-enterable from every state it can be left in: on a sortie screen seat 0 picks an aircraft again
and the walk reopens at the same seat, and on Instant Action FLY MISSION is unconditionally live and
starts it again. Unjoining survives at `StepSeat`'s guest arm (a seat's Back away from its own
screen) and on the device-lost path through `Step`, which is what the report asked for.

**One thing Decision 6 costs, for A2 to weigh.** The per-seat screen's three rows are the list,
ACCEPT SELECTIONS and CANCEL SELECTIONS, and with CANCEL SELECTIONS no longer leaving, no row of
that screen leaves the walk: the only way out is Esc or B. That is fine for a pad or a keyboard and
it removes the mouse's one exit, which matters because A2's goal keeps the mouse driving this
screen. `OriginalCoverageTests`' `seat-plane` journey now walks out by completing the walk (the
field, then ACCEPT SELECTIONS) rather than by cancelling it, with the reason on the journey. The two
fixes are a second CANCEL SELECTIONS press leaving when there is no selection left to drop, or a
BACK plaque on the screen; neither was taken here, because Decision 6 says this button stays in the
walk and A2 owns who drives the screen.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying A1, B11, B12 and C21:
build clean, 268 of 268 engine suites pass with engine errors clean, 18 goldens hash-identical,
units 3,518 of 3,519. `menu-player-setup-seats` covers this item's whole sequence, and the
`OriginalSeats` and `OriginalCoverage` unit cases pass. The one unit failure is the `MenuLayout`
census pin (`artMissing` expects 2 where this install's extraction reports 0), which predates the
wave and is recorded on main; it touches nothing any item here changed. Still owed at the controls,
on E41's sortie: two pads on Instant Action, Back on the per-seat screen returning to the sortie
screen with both seats listed, and Back on the Instant Action screen still unjoining.

**Original approach (kept for reference).**

**Goal.** On the per-seat plane-selection screen, Back leaves the walk and reopens the screen the
walk came from with every joined seat kept. A pilot leaves the sortie only by pressing Back on the
Instant Action screen, or by losing their device.

**Evidence (confidence: traced).** `BackSeatPlane`
(`CSVM/src/UI/Menu/Original/OriginalSeatPlane.cs:248-271`) undoes a selection while the seat is
locked and calls `_setup.Unjoin` at `:269` while it is browsing, so a seat that has not yet picked
loses its place on its first Back. `ActivateSeatPlane`'s CANCEL SELECTIONS arm does the same at
`:222`. Seat 0's own Back already takes the right path, through `CancelSeatWalk` (`:171-179`)
reached at `:255-257`. Reported at the controls over `PLAN-M5-polish-12`'s closing sortie:
"pressing B should not remove the player but return to the previous screen; only a B on the main
Instant Action screen or a controller disconnect should remove the player". Every line number above
was confirmed against the tree in this session.

**Approach.** Give every seat the `CancelSeatWalk` path Back takes for seat 0 today, so Back leaves
the walk and reopens `_seatReturn` with `_setup`'s joins untouched. Unjoining moves to Back on the
Instant Action screen and to the device-lost path. CANCEL SELECTIONS keeps a job on this screen
under Decision 6: its arm at `:221-223` drops the seat's selection and reopens the list instead of
calling `_setup.Unjoin`, so the walk and the sortie both survive it.

**Model recommendation.** high. The edit is small, but it rewrites a screen's exit rule that three
other items build on, and it carries an open design call.

**Verify.** Two pads on Instant Action: the second pilot joins, the walk opens, Back on the per-seat
screen returns to the sortie screen with both seats still listed, and re-entering the walk reaches
the same seat. Then Back on the Instant Action screen, which must still unjoin. Complete
`.\RunTests.ps1`.

**⚠ Traps.** A seat that is kept but unconfirmed still gates FLY, so the return must not leave the
sortie screen unlaunchable with no way back into the walk: check that re-entering the walk from the
sortie screen is reachable after a Back at every point in it. Decision 6 settles CANCEL SELECTIONS,
so do not leave that arm calling `_setup.Unjoin` on the ground that Back no longer does; both
changes belong to this item.

## A2 ☑ `BL-747` Only the picking seat's device drives the per-seat screen

**Landed.** The mechanism was one frame-routing decision, not a device check. `StepSeat` handed seat
0's whole frame to the shell on every screen, so the per-seat screen took seat 0's cursor, Accept and
Back as if they were the picking seat's. The screen now answers to the seat that is picking: seat 0's
frame is reduced to its pointer alone while another seat picks (`SeatZeroFrame`), and the public
`Step` became the seat-0 entry over a private `ApplyFrame`, so no caller can drive a screen it does
not own by skipping `StepSeat`. The pointer is kept deliberately, since the mouse rides seat 0's
source and is the one device a pilot without a pad of their own can pick with; the identity that
decides is the seat, never the device kind. `_steppingSeat` is gone with the last reader it had:
`BackSeatPlane` now branches on the picking seat's own state, which is also what makes the walk
behave the same when seat 0 is itself the picking seat.

**Decided: the mouse keeps an exit, through CANCEL SELECTIONS.** A1 handed A2 the question, and the
answer is that a mouse-only pilot could not leave the walk once seat 0's reach was removed. On
Instant Action the only remaining way off the screen would have been to complete the walk, which
launches the mission, so the desk had no way back at all whenever the picking seat's pad was not to
hand (a sleeping wireless pad, a `--debug-join` seat with no device). Of A1's two candidate fixes the
smaller landed: CANCEL SELECTIONS drops the selection standing, exactly as Decision 6 says, and with
none left to drop the same press leaves the walk. That makes it the pointer's Back, in the two stages
the picking seat's Back already takes, and it adds no row to a screen authored with two buttons. The
BACK plaque was not taken, because A4 adds a Weapon Loadout row to this screen and a fourth plaque is
better judged with that row's layout in hand.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The picking seat's device and the mouse drive the per-seat plane-selection screen. Seat
0's commands do nothing there unless seat 0 is the picking seat.

**Evidence (confidence: traced).** This is deliberate today: `OriginalSeatPlane.cs:14-17` records
seat 0's controller driving the screen "so one pad at the desk can walk it", and `_steppingSeat`
(`:31`, read at `:255`) exists only to tell seat 0's Back from the picking seat's. Both confirmed in
the tree this session. Reported at the controls over `PLAN-M5-polish-12`'s closing sortie: "P2 Plane
Selection in Instant Action should only be controlled by P2 controller or mouse".

**Approach.** Resolve the screen's commands against the picking seat's own `Source`. The way out of
a walk whose second pad has gone quiet comes from A1's Back, not from seat 0's reach, which is why
A1 lands first.

**Model recommendation.** medium. One file, one identity check, with A1 having already removed the
hazard.

**Verify.** Two pads: seat 0's stick and buttons move nothing on the per-seat screen while seat 1 is
picking, and seat 1's do. A mouse still drives the screen for a pad-less second pilot. Then the same
walk with seat 0 as the picking seat, where its own device must drive it. Complete `.\RunTests.ps1`.

**⚠ Traps.** Do not fix this by gating on device kind; the seat's own `Source` is the identity that
matters.

## A3 ☐ `BL-749` A two-pilot Free Flight walk launches on the last confirm

**Goal.** The last seat's confirm launches the sortie, on Free Flight as it already does on Instant
Action, so seat 0's pick is the ready and the walk is the launch.

**Evidence (confidence: traced).** `FinishSeatWalk` (`OriginalSeatPlane.cs:156-167`, confirmed at
`:151` and `:156` in the tree) launches only when `_seatReturn` is `InstantAction`; every other
return reopens the sortie screen and waits for seat 0's FLY, which is that screen's documented rule
(`OriginalSeats.cs:11-14`). Reported at the controls over `PLAN-M5-polish-12`'s closing sortie:
"Free Flight 2P should follow the same rules ... on P2 ready go to fly, not back to the Free Flight
screen".

**Approach.** Make the launch follow the walk's completion whatever the return screen is.

**Model recommendation.** medium. The change is one branch, and the two gates it must not break are
both named below.

**Verify.** Two pads on Free Flight: seat 0 picks map and aircraft, seat 1 joins with Start, seat 0
readies, seat 1 confirms, and the flight starts. Then the single-player case, where seat 0 alone
still reaches FLY by pressing it, and Dogfight, which must still withhold the launch until a second
seat has joined. Complete `.\RunTests.ps1`.

**⚠ Traps.** Dogfight shares the sortie path and additionally withholds the launch until a second
seat has joined; keep that gate.

## A4 ☐ `BL-748` A joined pilot picks a weapon loadout

**Goal.** Every joined pilot picks a weapon loadout as well as an aircraft, on Instant Action and on
Free Flight, and that choice reaches the launch.

**Evidence (confidence: traced).** The per-seat screen draws three rows only, the list, ACCEPT
SELECTIONS and CANCEL SELECTIONS (`OriginalSeatPlane.cs:473-491`). Weapon Loadout is the Instant
Action screen's own strip over seat 0's `LoadoutChoice` or the feature's wingman fit
(`CSVM/src/UI/Menu/Original/OriginalLoadout.cs:9-11`, `:66-100`), reached from
`OriginalInstantAction.cs:437`. Free Flight's sortie screen has no loadout row at all
(`OriginalSeats.cs:192-226`). Reported at the controls over `PLAN-M5-polish-12`'s closing sortie,
twice, once for each mode.

**Approach.** Give the per-seat screen a Weapon Loadout row that opens the existing loadout screen
against the picking seat's own choice, and carry that choice into the launch through
`_setup.Choices`. The per-seat choice gets its own storage rather than becoming a fourth reader of
`LoadoutChoice`.

**Model recommendation.** high. It crosses three screens and the launch path, and its storage
decision is the part a later item would have to undo.

**Verify.** Two pads on Instant Action and again on Free Flight: each pilot picks a different
loadout, and each aeroplane carries the weapons its own pilot chose once in the air. Seat 0 alone is
unchanged. Complete `.\RunTests.ps1`.

**⚠ Traps.** Nothing here is decoded, because the original has no second pilot; this is a remake
decision and must not be written up as fidelity. The loadout screen is written against seat 0 and
the wingman radio pair. `<TODO: confirm the launch path can read a per-seat loadout, or name what
has to change in it, before the row is drawn.>`

---

# Wave B — Wrecks, hulls and reach over the mission map

## B11 ☑ `BL-698` + `BL-700` Every breakup piece rests on the sea, on both hulls

**Landed.** The cause is neither of the three candidates below. Every piece takes the column tier,
every piece reaches the sea, and every origin is inside the column the hull queries. What decides
where a piece stops is WHEN it lands: the wreck is still descending at about 104 m/s when the first
sections reach the water, the solve is in the wreck's own frame, and the energy test that ends a
body reads only the body's velocity IN that frame, which is a metre or two a second. So a section
that touches the sea early is rested at the surface, frozen in the wreck's frame, and then carried
down by every metre the wreck still has to fall. Measured on C2B/M04 before the fix, a section's
final depth is exactly minus the wreck's height at the frame it landed: `gasbag4` landed with the
hull at 26.96 m and rested at −26.94, `gasbag1` landed with it at 13.14 m and rested at −13.12,
while `gasbag2`, `gasbag3` and `gasbag5` landed at or after the hull's own rest and stopped on the
water. The same shape was already visible on `piratezep`, where `zeppelin-breakup` recorded rest
heights of −0.2 to −5.6 m under a 10 m band that passed them all.

The fix reads the ARRIVING speed rather than the local one: the body's own velocity taken into
world, plus the velocity of the frame it is solved in, taken from that frame's own step. A section
carried through the surface therefore fails the energy test's "the contact is survivable" arm no
longer, is lifted back onto the water each frame it is carried under it, and comes to rest there
once the wreck stops. A contact the body did not approach under its own power charges the 15 s
watchdog instead of the contact cap, because how many frames a wreck takes to settle is a
frame-rate figure and spending the cap on it would make the resting height depend on the frame
rate. Nothing is clamped, no constant is tuned, and a body under a parent that stands still is
untouched: the two readings agree exactly there.

Measured after, on the new `gemini-breakup-rest` suite: all five Gemini sections rest at −0.2 to
+0.1 m against a sea at 0, each on `col_water`, and the wreck at 0.0. On `piratezep` all six move
to −0.2 to 0.0 from −0.2 to −5.6. Both suites, `ground-contact` and `campaign-balloon-death` pass.

`gemini-breakup-rest` is the resting check the Gemini hull owed: C2B/M04 with collision wired, the
hull killed through its gasbag zones, and the wreck and all five sections asserted against the sea.
It was red on the unfixed tree at 3 of 5 sections afloat and is green at 5 of 5.

**Verified.** The complete `.\RunTests.ps1` on the merged tree: build clean, 268 of 268 engine
suites pass with engine errors clean, 18 goldens hash-identical, units 3,518 of 3,519, the one
failure being the pre-existing `MenuLayout` census pin recorded under A1. `gemini-breakup-rest`
reports the C2B/M04 wreck resting at y = 0.0 with 5 of 5 sections on the sea, and `ground-contact`,
`gemini-gasbag-bays`, `zeppelin-breakup` and `campaign-balloon-death` pass beside it. Still owed at
the controls, on E41's sortie: a CM14 Gemini and a pirate zeppelin each killed over water and
watched from outside until every piece settles.

**Follow-up found and not fixed here.** Two of the five sections dispatch no `hit_waterN` splash,
the two that land last, and this predates the fix. `MotionSet.OwesBounce` can only hold a def
instance open once a bounce is already recorded, and a body records one at its landing, so a body
still in the air holds nothing and `killgmzep` has already gone INVALID by the time it lands. The
pirate hull hides it because all six of its sections land inside the wreck's own descent. Needs its
own id.

**Original approach (kept for reference).**

**Goal.** A zeppelin killed over water comes to rest on the sea in every one of its pieces: the
pirate zeppelin's front and back hull sections, and the Gemini's `gasbag1` and `gasbag5`, settle
where the middle pieces already settle.

**Evidence (confidence: traced, from two reports at the controls).** `BL-668` added a second,
upward ground-column read in `MotionRuntime.TryGroundColumn`, which holds the main hull and the
middle bags at the surface. The two end pieces still sink through it on both hulls: reported on
`piratezep` over water on the build that landed `BL-668`, and on `geminizep` on the breakup the
retired `PT-103` flew, where the author counts "the middle three stayed above water, the front and
back sank through it" and the sortie log has the authored `all_gmzep_gasbags` playing. `git log -S
TryGroundColumn` since either report is empty, confirmed this session. `BL-668`'s own diagnosis
explains the shape: the crossing step is built from the piece's ballistic origin through the current
parent transform, and a piece that arrives outside the ray's reach is never lifted back. The
Gemini's end bags sit farthest from the pitch pivot and so carry the most vertical speed at the
break.

**Approach.** Read where `gasbag1` and `gasbag5` are on the frame `break1`/`break5` starts, against
`ColumnDepth`, before changing anything. The candidates in order are a per-piece origin that sits
outside the column the hull queries, a piece that never takes the rest path because it is spawned by
a different route than the hull, and a piece whose collision shape is authored around a centre the
surface test does not use.

**Model recommendation.** high. The fix is small once the cause is known, and every wrong cause here
has a plausible fitted answer that would pass a shallow look.

**Verify.** A CM14 Gemini and a pirate zeppelin each killed over water and watched from outside
until every piece settles, plus a resting check that covers the Gemini hull. Complete
`.\RunTests.ps1`.

**⚠ Traps.** Do not clamp pieces to sea level or to y = 0. `BL-668` replaced exactly that fitted
answer with the column read and tuned no constant. `zeppelin-breakup` pins `piratezep` alone and
would pass green while the Gemini sinks; `gemini-gasbag-bays` does start `killgmzep` on the Gemini
but over a collision-less world where nothing comes to rest, so neither suite covers a resting
check. Gasbags ride the hull and were confirmed resting in the same sitting, so on the pirate ship
this is not the gasbag path.

## B12 ☑ `BL-714` A moving hull blocks its own rings' fire

**Landed.** The hull's motion is not the mechanism. A ring's line-of-sight ray excluded every
collider of its own *mounting section*, and on `piratezep` a section is a large piece of the hull:
the belly rings `ctur1` to `ctur3` hang off `underneath`, whose 33 bodies include `g375`, the very
skin standing between them and a plane on the far flank. Across the seventeen rings, **422 of 517**
in-arc bearings that cross 120 m or more of the hull's own body read clear under that rule, and the
belly rings fired 7 rounds in 6 s docked and 27 flying straight through 162 to 255 m of their own
hull. The rule now reads the world layer with no exclusion at all, blind only to the first
`TurretController.MountSkirtM` = 1.5 m off the muzzle, which is where a ring's own bodies stop (`g21`
and `gun` answer at 0 to 1 m) and below where a hull skin stands over one (2 m). That leaves 172 of
the 517 clear, mostly hull carrying no collider, and every ring keeps 54 % to 79 % of its in-arc
field of fire. The mounting section is still what a round the gun fires owns, for the hit ray and
the splash; only the sight line stopped reading it.

The new `turret-moving-hull-blocks-own-fire` holds each ring's target at a station fixed in the
HULL's frame, so the hull's motion is the only variable between a docked leg and a flying one, and
probes successive rings at successive points of the net. Both legs are green, and both were red on
the unchanged rule. Two instrument faults found beside it, both now rules in `docs/verification.md`:
a first-obstruction sweep tests the geometry nearest the instrument rather than the shot in the
report (INSTR-48), and a suite with no clock of its own freezes every time-expiring cache on its
first verdict, which is why `turret-hull-blocks-own-fire`'s "8 s over several 1-2 s windows" was one
cast per ring and why `turret-self-fire` read every bearing off bearing 0 (INSTR-49). Both suites now
step a clock. The collider's own pose lag was measured directly off the physics server at **0.25 m**
on the net (INSTR-50): real, but it only flips a verdict grazing a body 6 m off, and it is left
unfixed.

**Verified.** The complete `.\RunTests.ps1` on the merged tree: build clean, 268 of 268 engine
suites pass with engine errors clean, 18 goldens hash-identical, units 3,518 of 3,519, the one
failure being the pre-existing `MenuLayout` census pin recorded under A1. The new
`turret-moving-hull-blocks-own-fire` passes on both legs, `turret-hull-blocks-own-fire` now confirms
16 of 17 rings against the 14 it read before, and `turret-self-fire` and `surface-vehicle-guns`
pass on the widened rule. Still owed at the controls, on E41's sortie: an Instant Action Dogfight
against a pirate zeppelin, flown along the far hull while it moves.

### Original approach (kept for reference)

**Goal.** A zeppelin's rings hold fire on a bearing its own hull blocks while that hull is moving
along its net, as they already do while it is parked.

**Evidence (confidence: traced to a failed reproduction, so the mechanism is still open).**
Reported at the controls in an Instant Action Dogfight, flying the far flank of a pirate zeppelin: a
ring on the far side fires through the hull it is mounted on, sustained, not the single shot the
cached verdict allows at the hull's edge. The static probe does not reproduce it
(`turret-hull-blocks-own-fire`, `git log --grep=BL-714`): on a parked C1/M04 `piratezep`, fourteen
of seventeen emplacements confirm a hull-blocked bearing inside their own arc and hold fire reading
`Blocked` over 8 s, and the two candidates it cleared stay cleared, the hull's colliders carrying
`CollisionLayers.World` and existing by the time a ring steps. What a parked hull cannot show: a
collider that trails the drawn hull by a physics step, and `TurretController.WorldRayBlocked`'s 1 to
2 s cache carrying a clear verdict taken while the hull stood elsewhere.

**Approach.** The moving-hull probe comes first. Drive `piratezep` along its net with the player
parked on the far flank inside a ring's arc, and log the ray's hit list per ring per step across
several cache periods against the collider's pose and the drawn hull's. Then fix on the collision
side, either pose sync or the cache's sampling.

**Model recommendation.** high. The item is a diagnosis whose obvious instrument has already
returned a false negative once.

**Verify.** The probe red against the current tree and green after the fix, plus an Instant Action
Dogfight against a pirate zeppelin flown along the far hull in motion. Complete `.\RunTests.ps1`.

**⚠ Traps.** Do not make aircraft cover; the decode says world geometry only. Do not widen the
exclusion to the vehicle, because `PlatformOf`'s comment records that this is precisely how rings
shoot through their own hull. A probe that samples one point of the authored leg found no in-arc
self-obstruction there and so tested nothing; sample across the leg. `BL-735`, the multiplayer
hulls' belly rings over no collider at all, is a separate change and stays separate.

## B13 ☐ `BL-565` An awake DEDG widens its members' engagement volume

**Goal.** Each tick an awake DEDG objective raises every live member of the watched group to a
9,000 m activation radius, so a watched group never disengages by distance and comes to the player
from anywhere on the map.

**Evidence (confidence: traced, decoded).** `docs/formats/objectives.md`'s `DEDG` row and
`FUN_00465850` carry the side effect. CSVM's `DedgMet` only counts, confirmed in the tree this
session at `CSVM/src/Session/ObjectiveGraph.cs:601-616`, where the whole body resolves
`GroupLiveCount` and compares it to `dedg.Max`. Members keep `AiModeMachine.ActivationRange` at the
2,000 m `min_ai_active_dist` floor (`CSVM/src/Flight/AiModeMachine.cs:136`, read at `:474`, applied
at `CSVM/src/Session/CampaignRoster.cs:341`) and drop back to patrol at "target lost" or "beyond
return range", which is how a wave survivor sits on its net 8 km away while the objective waits on
it. `PLAN-public-release` A1 re-read this entry with `BL-523` and confirmed it open.

**Approach.** Have `GroupLiveCount`, or a sibling the graph calls per awake DEDG, apply the widening
to each counted member's machine as `ActivationRange = max(ActivationRange, 9000)`. The altitude
bands wait until they have a consumer.

**Model recommendation.** medium. The change is one expression on a path that is already walked, and
the single rule it must respect is stated below.

**Verify.** A campaign mission whose DEDG objective waits on a wave survivor: the survivor closes
instead of sitting on its net, and the objective completes. Take a baseline of the failing case
first, since an objective that completes anyway proves nothing. Complete `.\RunTests.ps1`.

**⚠ Traps.** The widening is per awake objective per tick, so a napped or killed DEDG stops widening
but the original never shrinks the volume back. Match that: set, never reset.

---

# Wave C — The campaign's own screens and props

## C21 ☑ `BL-689` CM13's flight check answers per crew slot and grants its aircraft

**Landed.** The flight check now answers CHANGE PLANE per crew slot, reads the owned count the way
`uiData` 2018 reports it, and grants the mission's story aircraft on entry.

- `CampaignFeature.ChangePlaneAllowed` is a method over the crew slot, not a slot-less property.
  The pilot's answer alone carries `FLIGHTCHECK.SCRIPT`'s outright bar on the two grant missions;
  both answers carry the floor of three.
- `ChangePlaneCount` is `uiData` 2018's own answer, the owned count less one on missions 13 and 17,
  which is the term the tree had no expression for. The floor itself was already there.
- `CampaignFeature.GrantMissionAircraft` is `uiData` 2021: it takes the mission reward table's
  ungated entry, adds the aircraft the first time with the table's own name and the class-2 marker,
  and makes it the pilot's plane on every entry. `CampaignFlow.Entered` runs it as the flight check
  opens, before the page composes, so both presentations get it through the one flow.
- `CampaignFlightCheckPage` holds the two answers separately in `FlightCheckState` and hands each
  slot its own. A guest's own check is unchanged: neither script rule is about a guest's aircraft.

**Verified.** The complete `.\RunTests.ps1` on the merged tree: build clean, 268 of 268 engine
suites pass with engine errors clean, 18 goldens hash-identical, units 3,518 of 3,519, the one
failure being the pre-existing `MenuLayout` census pin recorded under A1. The new
`campaign-flight-check-plane-change` runs a two-plane profile (granted the aircraft, neither button
offered), a three-plane one (the wingman's button alone, the pilot's barred by the mission), a
profile already carrying the award (granted nothing a second time, the plane re-selected) and the
neighbouring mission (both buttons, the selection kept); `campaign-layout-parity` and
`menu-campaign-journey` pass beside it. Still owed at the controls, on E41's sortie: CM13 replayed
on a profile on each side of the floor of three.

**Original approach (kept for reference).**

**Goal.** CM13's flight check answers CHANGE PLANE per crew slot rather than once for both, applies
the original's second rule, and grants the mission's own aircraft.

**Evidence (confidence: traced).** `CampaignFeature`'s change-plane answer is slot-less
(`CSVM/src/Session/CampaignFeature.cs:165-173`) and `CampaignFlightCheckPage.cs:385,389` hands the
same answer to the pilot and the wingman. The original's second rule, owned count minus one against
a floor of three, has no term in our code at all, and the mission's grant never happens. Reported at
the controls as "in CM13 i cant select a plane on replay". No commit has touched either file since
the entry was filed, confirmed this session.

**Approach.** Close the three gaps named above and nothing else. Read
`docs/formats/campaign-screens.md:283-287` before writing anything, because it holds both of the
original's own bars.

**Model recommendation.** high. Half the reported symptom is faithful behaviour, so the judgement of
what to leave alone is the item.

**Verify.** CM13 replayed on a profile whose owned count puts it on each side of the floor of three:
the pilot's button is barred as the original bars it, the wingman's follows the owned-minus-one
rule, and the mission's aircraft is granted. Complete `.\RunTests.ps1`.

**⚠ Traps.** **Half of what was seen is faithful, so do not fix it away.** The original bars the
*pilot's* button outright on mission 13 (`docs/formats/campaign-screens.md:283`), and bars *both*
buttons when owned-minus-one falls below three (`:285-287`). Whether the wingman's button should
have been offered at all depends on how many aeroplanes that profile owns. The gaps are the three
named above, not "CM13 locks plane selection".

**Corrections to the item as written.** Two of the three gaps were stated slightly wrong and one
path was wrong. The files are `CSVM/src/UI/Menu/CampaignFeature.cs` and
`CSVM/src/UI/CampaignFlightCheckPage.cs`, not the `Session/` and `Menu/Original/` paths cited; the
per-slot lines are `:441` and `:445`, not `:385,389`. Gap 2's floor of three was already in the
tree (`(Profile?.Planes.Count ?? 0) >= 3`); what had no term was the minus-one on the two grant
missions. Gap 1 and gap 3 were exactly as stated.

## C22 ☐ `BL-690` A staged cutscene aeroplane wears its scheme

**Goal.** CM15's staged Balmoral wears the scheme of the aeroplane it stands in for, rather than the
shipped skins, and the same route serves every staged prop that has a scheme.

**Evidence (confidence: traced).** CM15 puts two different Balmoral models on screen for one
aeroplane and only the flyable one is painted. `AircraftStage` builds every staged subtree on one
bare `SceneBuilder` with no `textureSubstitute` hook and holds no `PaintScheme`
(`CSVM/src/Mech3/AircraftStage.cs:142`, confirmed in the tree this session), where `PlaneBuilder`
passes `textureSubstitute: (name, tex) => _painter?.Substitute(name, tex) ?? tex`
(`CSVM/src/Mech3/PlaneBuilder.cs:69-70`). Reported at the controls as "the balmoral in the cutscene
should have fortune hunters livery not the default". The backlog entry cites this file under
`src/Session/`; it lives under `src/Mech3/`, and the entry's path is corrected when the item lands.

**Approach.** Give the staged aeroplane its own `SceneBuilder` carrying the scheme the mission's own
rig resolved, taking that scheme from the `balmoral_1` rig so the prop tracks the aeroplane it films.

**Model recommendation.** medium. The fix shape is stated and the hook already exists on the
neighbouring builder; the care needed is in what the shared builder reaches.

**Verify.** CM15's intro watched with the drop Balmoral in frame, against the flyable one in the
same mission. Check `piratefighter`, `chuteman` and the wing-walk figures in the same pass, since
one builder serves them all. Complete `.\RunTests.ps1`.

**⚠ Traps.** **Do not hardcode `player_fortune` into the stage.** **One `SceneBuilder` serves every
staged subtree**, so a substitution hook installed on it reaches `piratefighter`, `chuteman` and the
wing-walk figures as well. ⚠ **The data authors no livery for this block at all**, so "Fortune
Hunters" rests on the remake's default-pattern rule and on the report, not on a value in a file, and
no CM15 footage exists to settle what the original's drop Balmoral wears. The intro prop
`piratefighter`, whose `devastator`/`wingman` defs do author `paint_pattern player_fortune`, is the
better-founded half of the same item.

---

# Wave D — What another aeroplane sounds like

## D31 ☐ `BL-079` Another aircraft's weapons have a position

**Goal.** Another aircraft's gun loop and weapon one-shots play from that aircraft's position with
their own distance cull, as its engine already does.

**Evidence (confidence: traced for the gap, footage for the goal).** `PLAN-public-release` A1
re-verified and narrowed this entry: the claim that all sound is own-plane and non-positional is
false, because every AI aircraft carries a positional engine and damaged-engine loop through
`AiEngineAudio` and `WorldSounds` positions world emitters the same way. What has no positional
voice is another aircraft's weapons. `StartGunLoop` and the one-shots sit on `FlightAudio`, the
own-ship path (`CSVM/src/Flight/FlightAudio.cs:143-166`, `StartGunLoop` confirmed at `:143` in the
tree this session), and `AiEngineAudio`'s own contract says it carries the two engine slots and
deliberately nothing else. The goal rests on footage: the original's Instant Action traffic is
clearly audible in the reference video. The backlog entry cites `AiFlightAssembler.cs` under
`src/Flight/`; it lives under `src/Session/`, and the entry's path is corrected when the item lands.

**Approach.** A positional weapon voice per AI aircraft, following the pattern `AiEngineAudio`
already sets, reusing `FlightAudio`'s sound selection rather than growing a second copy of it.

**Model recommendation.** high. It puts a seam between own-ship and world audio that later items
will read, and the tempting extra feature is one the evidence forbids.

**Verify.** An Instant Action sortie flown past firing traffic with `--volume=0`, reading the
emitter positions and counts out of `.scratch/logs/`, then the same sortie at the controls for the
distance cull. Take a baseline of the current non-positional case first. Complete `.\RunTests.ps1`.

**⚠ Traps.** **Do not build Doppler.** The entry originally read "clearly audible with Doppler",
which was an impression off a listen and never a measurement; `CAP-09` measured the original's world
emitters and found no Doppler at all, the police siren holding its source asset's pitch to within
0.008 % straight through a 250 mph overflight. `CAP-09` contains no other aircraft, so treat Doppler
on Instant Action traffic as unverified and measure it by the same method, a tonal component tracked
against the source WAV, before any pitch term is written. `--mute` is a load-time switch, so a muted
baseline is blind here and would count nothing; use `--volume=0`, which still loads, plays, counts
and logs.

---

# Wave E — The closing sortie

## E41 ☐ `BL-545` + `BL-649` The sitting that judges this plan and two owed looks

**Goal.** The plan's ten landed items are flown, and the two looks the tree already owes are
answered in the same sitting.

**Evidence (confidence: traced; both fixes have landed and only the look is outstanding).**
`BL-545`: the hookup definition now reaches the flown airframe's own subtree, so its per-airframe
hook extend and wing fold run instead of every `IF NODE_ACTIVE` arm reading false, and the presence
flag moved off the airframe node whose visibility is the ACTIVE bit those arms test
(`git log --grep=BL-545`, `d5caa7cc`). What is owed is CM02's auto-land watched again from outside,
which `PT-130` carries with its three checks. `BL-649`: every vantage that takes the camera now
gives the airframe back (`1feec441`), and what is owed is the flight, which `PT-122` carries.

**Approach.** One CM02 sitting covers both, because both are judged from outside the aeroplane on
the same flight. For `BL-545`, watch the auto-land from outside against `PT-130`'s three checks: the
hook deployed, the aeroplane's height on the trapeze, and a Balmoral's wings folded. For `BL-649`,
fly `--view=cockpit`, pause, enter photo mode and check the aeroplane is in the shot rather than
hidden behind its own panel, then leave photo mode and confirm the cockpit comes back over a world
that is still halted. Then fly Wave A's two-pilot walks, a zeppelin killed over water, an Instant
Action Dogfight against a pirate zeppelin, CM13's replay, CM15's intro, and firing traffic within
earshot, and file what does not hold.

**Model recommendation.** Not applicable. The author flies this item; an agent writes up the
verdicts, closes what holds, and files what does not.

**Verify.** Each of the ten items re-judged at the controls, `BL-545` and `BL-649` closed or
refiled, `PT-130` and `PT-122` retired with them, and every new finding minted through
`New-ItemId.ps1`.

**⚠ Traps.** Photo mode's hand-back cannot be left to the per-frame arm, because it returns to the
halted world the board froze and `halted` is its own no-write branch in `_Process`. The three debug
callers of `CameraOwned` are private methods behind a live session, so the suites reach the seam but
not its callers, and only this flight covers them.
