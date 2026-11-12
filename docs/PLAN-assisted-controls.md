# Assisted controls and the level chase camera

**ACTIVE PLAN** (written 2026-11-12). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan adds two remake-only options modelled on the Standard mode of the Ace Combat games. The
first is an **Assisted control scheme**: the stick commands a turn and a climb, and an assist works
aileron, rudder and elevator to fly it. The second is a **Level chase camera**, which takes the bank
out of the chase rig while keeping its pitch. Both are off by default, so the port plays as the
original until a player opts in. The assist produces stick deflections into the unchanged decoded
flight model and never sets attitude itself. Each airframe therefore keeps its own turn rate, stall
and speed loss, and multiplayer (owner-authoritative planes) needs no protocol change. Out of scope:
stall and terrain protection, the cockpit and nose views, the VR chase (it keeps its own `XrChase`
setting), and any change to the original controls. No `backlog` issue feeds this plan; its source
is the grilling recorded in the Decisions table.

## Milestone goal

- A player can choose Assisted controls and fly every flyable aircraft, the autogyro included,
  with left/right stick turning the plane and up/down stick climbing or diving relative to the horizon.
- A player can choose the Level chase camera, independently of the control scheme, and the chase
  view then never banks in a turn, follows the plane over the top of a loop, and rolls upright with it.
- Both options persist, sit in the controls settings, work under Original and Enhanced graphics, and
  are per player in every mode including multiplayer.

**The assist only ever writes a `FlightInput`.** It never touches attitude, velocity or the flight
model's constants, so it cannot fly a plane past what a pilot could do by hand, and the net code,
the AI and the original controls stay untouched.

## Decisions (2026-11-12)

| # | Question | Decision |
|---|---|---|
| 1 | What does stick left/right do? | **A coordinated turn, proportional to the input.** Small inputs turn on rudder alone; larger ones blend into a bank with elevator. Centring the stick rolls the wings level. |
| 2 | How does the assist move the plane? | **It drives the control surfaces** through the decoded flight model, as `AiControlLaw` does for the AI. Not a kinematic attitude override. |
| 3 | What does stick up/down do? | **Climb or dive relative to the horizon.** The assist adds the elevator a bank needs, so pulling during a turn climbs rather than tightening it. |
| 4 | Holding the stick back to vertical and beyond? | **A full loop while held; easing off rolls the plane upright** (a half loop ends as an Immelmann turn, a held dive as a split-S). |
| 5 | What does the level camera show at the top of a loop? | **It follows over the top**, world upside down there, and rolls upright together with the plane. Not "sky always up". |
| 6 | One bundled setting or two? | **Two options:** control scheme (Original / Assisted) and chase camera (Original / Level). Choosing Assisted switches the camera to Level once; the player may switch it back. |
| 7 | Which views does Level change? | **The chase view only.** Cockpit and nose bank with the plane; VR keeps its own setting. |
| 8 | The separate yaw input (keys, bumpers, twist, pedals) under Assisted? | **Added straight onto the assist's rudder** for fine aim; the assist does not fight it. |
| 9 | Which modes allow Assisted? | **Every mode, per player**, multiplayer and stunt races included. |
| 10 | The autogyro? | **The same assist**: it targets turn and climb rates and finds the autogyro's own deflections, so stick left turns it left. |
| 11 | Stall and ground protection? | **None.** The plane can stall and can fly into the ground. |
| 12 | How hard does full sideways stick turn? | **The hardest turn the plane can hold without losing height at its current speed**, bleeding speed as a hard turn does. Roughly the first fifth of travel is rudder only (TUNE at the controls). |
| 13 | Does the level camera keep the chase rig's feel? | **Yes**: the existing eased rig, speed-based distance, zoom and look-stick swing, with the bank removed from the frame it follows. |
| 14 | Defaults? | **Both off.** Both options live in the controls settings and work under both graphics modes. |
| 15 | What does a centred stick do after a climb or dive? | **Holds the current climb angle.** Push or pull to change it; centring keeps it, as in Ace Combat. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, options and the level chase camera

1. ☐ The two options: control scheme and chase camera, persisted, in the controls settings
2. ☐ The level chase camera: the chase rig without bank, continuous over the top of a loop

### Wave B, the assist

11. ☐ The assist law: stick and yaw input to a `FlightInput`, pure and unit-tested
12. ☐ Wire the assist into the human input path for every device, the mouse included
13. ☐ The autogyro under the assist
14. ☐ Tune the assist and the level camera at the controls

## Dependency and parallelism notes

A1 blocks A2 and B12 (both read the options). A2 and B11 are independent and can run in parallel
worktrees: A2 owns `CSVM/src/Flight/Camera/`, B11 owns a new file beside `MouseFlight.cs` in
`CSVM/src/Flight/Airframe/` plus its unit tests. B11 → B12 → B13 is a chain; B13 may fold into B11
if the law turns out to need no airframe branch. B14 runs last, with the user at the controls, once
A2 and B13 have landed. `FlightController.cs` is touched by B12 only.

---

# Wave A, options and the level chase camera

## A1 ☐ The two options: control scheme and chase camera

**Goal.** The controls settings offer "Control scheme: Original / Assisted" and "Chase camera:
Original / Level", both Original on a new install, persisted, and readable by the flight code per
player. Choosing Assisted sets the camera to Level once; switching the camera back afterwards sticks.

**Evidence (confidence: lead-only).** Settings live somewhere among `CSVM/src/Utils/OptionsStore.cs`,
`CSVM/src/UI/Menu/OptionsChoices.cs`, `CSVM/src/Launch/LiveSettings.cs` and
`CSVM/src/UI/Screens/PausePreferences.cs` (all matched a search for flight assists; not read).
`<TODO: name the existing controls option this should copy, e.g. mouse flight's on/off, and its
store key and menu row.>` `<TODO: which menu carries it: the Original-style options page
(OriginalGameOptionsPage.cs), the remake options, or both.>`

**Approach.** Copy the pattern of the nearest existing per-player controls option end to end: store
key, menu row, live-settings read, and the per-player plumbing multiplayer uses for a seat's
settings. Two enum-valued options, not booleans, so a later scheme has a place to go. The one-time
"Assisted sets Level" is a write at the moment the scheme changes, not a derived rule.

**Model recommendation.** medium. The plumbing follows an existing option's pattern.

**Verify.** A unit or engine suite that the defaults read Original/Original, that the values
persist round-trip, and that switching to Assisted flips the camera once and only once.
`<TODO: name the existing options suite to extend.>`

**⚠ Traps.** Per player, not per machine: in split-seat or co-op each seat has its own choice.
`<TODO: confirm how a seat's settings reach FlightController in multiplayer.>`

## A2 ☐ The level chase camera

**Goal.** With Level selected, the chase camera follows the plane's heading and pitch but never its
bank in a turn. Through a loop it pitches over the top with the plane (world upside down at the top)
and rolls upright as the plane rolls upright, with no cut and no one-frame flip anywhere.

**Evidence (confidence: traced for the existing rig, lead-only for the new frame).** The chase is
`CameraController.Chase` (`CSVM/src/Flight/Camera/CameraController.cs:744`): `AuthoredRig` gives the
offset and aim, `StepFrames` eases two aircraft frames (`_posFrame`, `_lookFrame`) at
`pos_catch_up`/`look_catch_up`, and the look stick's `PadSwing` turns the finished pose. The VR
level chase (`:750`) uses `XrMount.Unrolled` (`CSVM/src/Flight/Camera/XrMount.cs:104`), built on
`XrMount.NoRoll` (`:125`), which keeps the up axis **on the sky's side**. Past vertical that flips
the view by a half turn in one frame, and the VR chase covers it with a comfort-fade cut. That is the
"sky always up" behaviour Decision 5 rejected, so the flat chase cannot reuse `NoRoll` as it stands.

**Approach.** Feed `StepFrames` a de-banked attitude instead of the raw one, so the easing, distance,
zoom and swing all stay as they are (Decision 13). The de-banked frame keeps the plane's forward and
takes its right axis horizontal, with the up's **sign carried from last frame** rather than chosen
toward the sky. Through a pitch loop the plane's right axis stays horizontal, so the frame follows
over the top continuously. The open part is the recovery roll from inverted. That roll carries the
plane's right axis through vertical, where a horizontal projection has no length, and the
projection's sign flips. `<TODO: choose the frame rule for that crossing. The candidate: the camera
eases its roll toward the nearest level target (0 or 180 degrees) at a capped rate, so a roll
through 90 degrees of bank reads as the camera rolling with the plane. It needs a check against the
original controls with Level, where a pilot can hold 90 degrees of bank.>` Reuse `XrMount.Unrolled`'s
held-right carry for the vertical cone if it applies; do not change the VR path.

**Model recommendation.** high. A frame rule with singularities, judged by eye.

**Verify.** A unit test over scripted attitudes (a held 60-degree banked turn, a full pitch loop, a
loop followed by a 180-degree roll, a pass straight through vertical): the camera's right axis
stays horizontal within `<TODO: ε>` outside the recovery roll, and the frame never changes by more
than `<TODO: max degrees per step>` between steps. Then a `--fly` session and a montage for the user
(look judgements are theirs).

**⚠ Traps.** `NoRoll`'s sky-side up is correct for VR and wrong here; keep the two rules separate.
Don't fold the de-bank in after `StepFrames`, because the eased frames would then trail a banked
attitude and the camera would visibly roll during turn entry. A hard assisted turn banks steeply,
so the projection is ill-conditioned near 90 degrees in ordinary flight, not only in the recovery.

# Wave B, the assist

## B11 ☐ The assist law

**Goal.** A pure function from (stick X, stick Y, yaw input, the plane's attitude, velocity and
angular rates, the airframe) to a `FlightInput`, with no device and no scene in it, covering
Decisions 1, 3, 4, 8 and 12.

**Evidence (confidence: lead-only).** The precedent for the shape is `MouseFlight`
(`CSVM/src/Flight/Airframe/MouseFlight.cs`): pure arithmetic producing a `FlightInput`, unit-tested
without a window. The AI's bank-to-turn law is `AiControlLaw` (`CSVM/src/Flight/Ai/AiControlLaw.cs:66`),
which takes an aim point and returns a `FlightInput`. `<TODO: decide whether the assist can be an aim
point fed to AiControlLaw (a point ahead, displaced by stick X and Y) or needs its own loops. The AI
law's deadbands and its wings-level rule are tuned for AI and may fight Decision 1's rudder-only
band.>` A centred stick Y holds the current climb angle (Decision 15).

**Approach.** Closed loops, each mapping a target to one deflection:
- Lateral: stick X maps to a turn-rate target. Inside the rudder-only band (about the first fifth,
  TUNE) the bank target is zero and rudder turns the plane. Beyond it, a bank target rises with the
  input. At full stick, elevator is held at full pull and the bank is the steepest that holds
  vertical speed at zero (Decision 12). Aileron tracks the bank target; rudder cancels sideslip.
- Vertical: stick Y maps to a flight-path-angle rate in world terms. Elevator is that command plus
  the pull the current bank needs to hold the turn level (Decision 3).
- Loop: held stick back keeps pitching through vertical and over. On release while inverted, the
  bank target becomes upright and the plane rolls on its new heading (Decision 4).
- Yaw input is added straight onto the rudder output after the loops (Decision 8).
- No stall or terrain logic (Decision 11).

**Model recommendation.** high. Control-law design against an unfamiliar flight model.

**Verify.** Unit tests on the law's outputs at fixed states (rudder-only inside the band, wings
level on a centred stick, full pull at full lateral stick, yaw pass-through). Then an engine suite
flying a real airframe: a held full-left stick reaches a steady turn and loses less than `<TODO: m>`
of height in `<TODO: s>`; releasing it levels the wings within `<TODO: s>`; a held pull completes a
loop, and release while inverted ends upright. `<TODO: the docs/verification.md rule that bites,
likely the single-frame physics desync (memory: settle a collider or physics claim in a live --fly
session).>`

**⚠ Traps.** "Hardest level turn" is a feedback target, not a formula. Do not derive it from lift
constants, which this project has decoded only in part; close the loop on vertical speed.
Horizon-relative pitch and plane-axis elevator differ once banked; a test at zero bank cannot tell
them apart.

## B12 ☐ Wire the assist into the human input path

**Goal.** With Assisted selected, every human device (keyboard, pad, flight stick, mouse) flies
through the assist; with Original selected, nothing changes.

**Evidence (confidence: lead-only).** The mouse's stick enters at
`CSVM/src/Flight/Airframe/FlightController.cs:3962` (`MouseFlight.Read(stick.X, stick.Y, 0f, …)`).
`<TODO: find where keyboard, pad and stick deflections are summed into the human FlightInput
(HumanFlightAdapter.cs or FlightRosterInputs.cs are the leads) and where yaw is read separately.>`

**Approach.** One seam: after the devices sum into stick X, stick Y and yaw, and before the flight
model reads the `FlightInput`, replace the roll/pitch/yaw with the assist's output when the player's
scheme is Assisted. Throttle passes through. The mouse needs no special case because its cursor
offset is already stick deflection.

**Model recommendation.** medium.

**Verify.** An engine suite (extend `FlightInputHandoffSuites` or `MouseFlightSuites`, `<TODO:
choose>`) showing the same input yields different deflections under the two schemes, and an
Original-scheme run of the complete `RunTests.ps1` unchanged against main.

**⚠ Traps.** AI pilots and remote planes must never pass through the assist; gate on the human
seat's own option, not a global.

## B13 ☐ The autogyro under the assist

**Goal.** With Assisted selected, stick left turns the autogyro left, as for any plane (Decision 10).

**Evidence (confidence: traced for the original's exchange).** `MouseFlight.Read` swaps the roll
and yaw sources for an autogyro (`MouseFlight.cs:48`), because the original yaws it where a plane
banks. `<TODO: how the autogyro's flight model responds to roll and yaw, and whether B11's loops
work unchanged on it.>`

**Approach.** Because the assist targets turn and climb rates rather than deflections, the loops
may need only different gains for the autogyro. Feed the assist the raw stick, before any autogyro
exchange, and let its output reach the flight model directly.

**Model recommendation.** medium.

**Verify.** B11's engine suite run on the autogyro.

**⚠ Traps.** Do not apply `MouseFlight`'s autogyro exchange on top of the assist's output; it would
swap the assist's roll and yaw a second time.

## B14 ☐ Tune at the controls

**Goal.** The user flies Assisted with Level and judges the turn feel, the rudder-only band, the
recovery roll and the camera through loops; the tuned values land.

**Evidence (confidence: lead-only).** All magnitudes in B11 and A2 are TUNE.

**Approach.** One batched at-the-controls session once A2 and B13 have landed, with the questions
asked after the run. Record each tuned value with its reason in the landing commit.

**Model recommendation.** medium.

**Verify.** The user's verdict per question.

**⚠ Traps.** A live symptom is evidence about the build that was running; confirm the build before
filing anything from this session.
