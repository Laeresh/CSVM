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
| 6 | One bundled setting or two? | **One option**, the control scheme (Original / Assisted); Assisted brings the level chase camera. Two options landed first (A1, A2) and were merged in B14: at the controls they did not work well apart. |
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

1. ☑ The two options: control scheme and chase camera, persisted, in the controls settings
2. ☑ The level chase camera: the chase rig without bank, continuous over the top of a loop

### Wave B, the assist

11. ☑ The assist law: stick and yaw input to a `FlightInput`, pure and unit-tested
12. ☑ Wire the assist into the human input path for every device, the mouse included
13. ☑ The autogyro under the assist
14. ☐ Tune the assist and the level camera at the controls

## Dependency and parallelism notes

A1 blocks A2 and B12 (both read the options). A2 and B11 are independent and can run in parallel
worktrees: A2 owns `CSVM/src/Flight/Camera/`, B11 owns a new file beside `MouseFlight.cs` in
`CSVM/src/Flight/Airframe/` plus its unit tests. B11 → B12 → B13 is a chain; B13 may fold into B11
if the law turns out to need no airframe branch. B14 runs last, with the user at the controls, once
A2 and B13 have landed. `FlightController.cs` is touched by B12 only.

---

# Wave A, options and the level chase camera

## A1 ☑ The two options: control scheme and chase camera

**Landed.** Both options are per-seat fields of the keymap profile, copying mouse flight's pattern
end to end, because mouse flight is the existing per-player controls option (the options file is
process-wide). `Bindings/ControlScheme.cs` holds the enums `ControlScheme { Original, Assisted }`
and `ChaseCamera { Original, Level }`; `BindingProfile.ControlScheme` and `.ChaseCamera` carry them
(default Original); `BindingStore` saves them as `"controlScheme"`/`"chaseCamera"` words in
`bindings_p<n>.json`, a missing or unknown word reading Original with no version bump.
`ControlsFeature.ControlScheme`/`.ChaseCamera` stage them with the maps, and the `ControlScheme`
setter is the one-time write: a change to Assisted also stages Level, and a camera put back to
Original stays. `MenuControlsSeats` and `StickProfileResolver.WithoutStickRows` copy them where
they copy the mouse fields. The menu is Original's CONTROLS page (`OriginalControlsPage.cs`): a
third panel ("Controls" and "Camera" choosers in the Controller Type column), the plate grown by one
band of `CP_BackGround.png`'s own art, the door and plaques one band lower. Docs:
`docs/controls.md` "Assisted controls and the level chase camera", `docs/architecture/Bindings.md`,
`docs/architecture/UI.md`, `docs/org/menu-inventory.md`'s ControlsPrefs row. Tests:
`BindingStoreTests` (round-trip, Assisted with the Original camera, older and hand-edited files),
`ControlsFeatureTests` (Level once, per seat, Cancel), `OriginalOptionsTests` (the panel's rows,
the grown plate, the clearance facts over the fixture and the install).
⚠ **The flight seat does not take the two fields yet.** `FlightControllerBuild.ApplyProfile`
(`CSVM/src/Flight/Airframe/`, outside A1's files) copies only the keymap and the two mouse fields,
so B12 (and A2 for the camera) adds `ControlScheme = profile.ControlScheme;` and
`ChaseCamera = profile.ChaseCamera;` there, with seat properties over `_controls.Profile` like
`MouseFlying`'s. Both the launch read and a pause-accepted CONTROLS page reach the seat through it.

**Verified.** The complete `RunTests.ps1` on the merged plan tree (A1, A2, B11, B12 and B13 with
main merged in): build, units 8462 passed and 0 failed of 8468 (6 skipped for missing extracted
data), engine 679 passed and 0 failed with engine errors clean, goldens 41 shots hash-identical. The
first pass failed one unit, `SuiteShardsTests`, because `chase-level`, `flight-assisted-law` and
`flight-assisted-seat` carried no weight in `analysis/engine-suite-weights.json`; their measured
shard times were added and the unit stage re-run green. The 8-chapter `--freecam` regression was not
run separately: no item touches world loading.

**Original approach (kept for reference).**

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

## A2 ☑ The level chase camera

**Landed.** A seat whose `ChaseCamera` is Level flies the flat chase on a bankless frame. The seat
takes the field in `FlightControllerBuild.ApplyProfile` (`ChaseCamera` over `_controls.Profile`,
like `MouseFlying`), and `FlightController` passes `ChaseCamera == Level` to
`CameraController.Chase`'s new `level` argument. There, before `StepFrames`, the attitude is replaced
by `Flight/Camera/LevelChase.cs`'s `Step`, so the easing, speed distance, zoom and look-stick swing
are the existing rig's (Decision 13). The hold (`LevelChaseHold`: the frame's right axis and its
target level right) is kept per `Shot`; a new shot (respawn, view change) reseeds it and the eased
frames on the level frame, and a switch from Original in flight eases into it without a cut. The
VR chase, cockpit and nose are untouched.

The frame rule for the crossing (TUNE values, judged in B14):
- The target is one of the two level rights ±(forward × up), the one nearer last frame's target,
  so a loop carries it over the top and the view is upside down there (Decision 5).
- It flips to the other level only when the wing is more than 90° + `FlipPastRad` (20°) from it.
  This hysteresis keeps a held knife-edge on one level (Original controls with Level) and keeps a
  steep assisted turn wobbling about 85° level, where a projection or any fixed function of bank
  would be ill-conditioned.
- The frame rolls to the target at `RollRateRad` (240°/s), above the 180°/s aileron roll, measured
  through the wing so the half turn goes the aircraft's way. The recovery roll from inverted thus
  reads as the view rolling with the aircraft, about 0.15 s behind it at 180°/s.
- Within 20° of vertical (`XrMount.VerticalConeCos`, Unrolled's cone) the right and target are
  carried by projection and nothing flips.
Why and the rejected rules: `docs/org/cameraViews.md` "The level chase camera (remake-only)".

Tests: `CSVM.Tests/LevelChaseTests.cs` (8 facts at 60 steps/s; every step asserts the forward is
the aircraft's and the frame turns no more than the nose plus `RollRateRad·dt` + 0.001 rad).
Measured: the right axis's height stays below 1e-4 (0 to float precision) through a 60° turn, an
85° turn with a ±3° wobble, a full loop at 90°/s (up equal to the aircraft's, upside down at the
top), a half loop, after an Immelmann's 180°/s roll upright (from 0.5 s after the roll), and a held
knife-edge with a ±5° wobble (frame upright throughout); 105° bank holds upright, 115° rolls to the
inverted level. Largest step: 0.34° (60° turn), 0.58° (85° turn), 1.50° (loop), 4.0° (the roll cap,
during the Immelmann's roll), with no step against the aircraft's roll. A roll on the vertical
leaves the right axis fixed. The one tilt: a loop whose nose passes 3° beside vertical tilts the
right axis up to 5.79° inside the cone, back to level in 2 steps on leaving (largest step 4.27°).
Engine suites: `chase-level` (`ChaseRigSuites.cs`, the eased rig on a real `Camera3D`): after a
snap at 60° bank and through 3 s of the turn the view's right axis height is 0, over the loop's top
the view's up is y −0.928 (the ease trails the 90°/s pitch), the largest frame-to-frame turn is
1.5°, and the Original chase banks with the same turn. A C1 `--fly` `--hold` capture suite
photographed level flight, a 76° and an 85° bank (view right axis y −0.004 and 0), and a loop (view
up y −0.94 at the top) for the user's look, which accepted it; the suite was then dropped to keep 8 s
out of every battery. Existing `chase-rig` and `chase-trail` pass unchanged,
as do the 245 camera, chase and XR view unit tests. A respawn (`Snap`) and the spectator `Watch`
take the seat's last choice, so a respawn settles on the level frame.

**Verified.** The complete `RunTests.ps1` on the merged plan tree (A1, A2, B11, B12 and B13 with
main merged in): build, units 8462 passed and 0 failed of 8468 (6 skipped for missing extracted
data), engine 679 passed and 0 failed with engine errors clean, goldens 41 shots hash-identical. The
first pass failed one unit, `SuiteShardsTests`, because `chase-level`, `flight-assisted-law` and
`flight-assisted-seat` carried no weight in `analysis/engine-suite-weights.json`; their measured
shard times were added and the unit stage re-run green. The 8-chapter `--freecam` regression was not
run separately: no item touches world loading.

**Original approach (kept for reference).**

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

## B11 ☑ The assist law

**Landed.** `AssistedFlight.Steer` in `CSVM/src/Flight/Airframe/AssistedFlight.cs`, pure and not yet
wired to any seat (B12). The design, the reasons it does not feed `AiControlLaw` an aim point, the
plant readings behind each loop, the contract B12 wires to and the TUNE constants are in
[`assisted-flight.md`](assisted-flight.md). `is_autogyro` reaches no plant term, so the law flies
the autogyro unchanged; B13 reduces to feeding the raw stick from before `MouseFlight`'s exchange.

Measured in `flight-assisted-law` (Bloodhawk / Hoplite): 2.1 / 1.9 m lost in 12 s of full left
stick against 188.9 / 184.8 m for the raw stick, a steady 32.5 / 64.0 deg/s at mean elevator 1.000,
wings level 0.72 / 1.37 s after centring, a held pull closing the loop in 11.8 / 6.1 s on the entry
heading, and a release over the top upright in 1.30 / 2.80 s on the reversed heading.
`CSVM.Tests/AssistedFlightTests.cs` pins the fixed-state deflections.

**Verified.** The complete `RunTests.ps1` on the merged plan tree (A1, A2, B11, B12 and B13 with
main merged in): build, units 8462 passed and 0 failed of 8468 (6 skipped for missing extracted
data), engine 679 passed and 0 failed with engine errors clean, goldens 41 shots hash-identical. The
first pass failed one unit, `SuiteShardsTests`, because `chase-level`, `flight-assisted-law` and
`flight-assisted-seat` carried no weight in `analysis/engine-suite-weights.json`; their measured
shard times were added and the unit stage re-run green. The 8-chapter `--freecam` regression was not
run separately: no item touches world loading.

**Original approach (kept for reference).**

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

## B12 ☑ Wire the assist into the human input path

**Landed.** One seam, `FlightController.Steered`, at the return of `ReadKeyboard`, where the keys,
pads, flight sticks and mouse are already summed and clamped in the plant's signs. A seat with
`IsHumanPiloted` set and its own profile's `ControlScheme` on Assisted hands that stick to
`AssistedFlight.Steer` with the seat's `_model` and the sim step's dt; every other seat passes it as
read, and AI and remote seats never reach the keyboard arm at all. `FlightControllerBuild` carries
`ControlScheme` over `_controls.Profile` like `MouseFlying`, and `ApplyProfile` copies it, so the
launch read and a CONTROLS page accepted over the pause both reach the seat. The seat's
`AssistedState` is reset by the `ControlScheme` setter when the value changes, by `Respawn` (which
also runs at first spawn), and on every held step, since a held aircraft is placed rather than
flown. A wholly held seat (`FlightControlHold.All`, a pause sheet over network flight) feeds its
centred stick through the same seam, so under Assisted it levels and holds the climb. Centring, per
device: the keyboard ramp assigns exactly 0; a pad's pitch passes `PadCurve` (0 inside 0.15); a
flight stick's full axis reads `ControlValue.None` inside its stamped 0.02; the mouse's `Gate` is 0
inside 0.1. Only a flight stick whose centre has drifted past its own deadzone rests off zero, and
the seam adds no band for it: a band of even 0.02 would swallow a pad's first 0.12 of raw travel past
its deadzone, since `PadCurve` squares. Docs: `docs/assisted-flight.md` "The wiring",
`docs/controls.md`, `docs/architecture/Flight.md`. Suite `flight-assisted-seat` (in
`AssistedFlightSuites.cs` beside the law's suite, which owns the flown helpers it reuses): a held
roll-left key raw under Original against through the law under Assisted, the state reset on a
switch, an accepted profile and a respawn but not on an accept that keeps the scheme, and an AI seat
on Assisted reading raw without priming the law.

**Verified.** The complete `RunTests.ps1` on the merged plan tree (A1, A2, B11, B12 and B13 with
main merged in): build, units 8462 passed and 0 failed of 8468 (6 skipped for missing extracted
data), engine 679 passed and 0 failed with engine errors clean, goldens 41 shots hash-identical. The
first pass failed one unit, `SuiteShardsTests`, because `chase-level`, `flight-assisted-law` and
`flight-assisted-seat` carried no weight in `analysis/engine-suite-weights.json`; their measured
shard times were added and the unit stage re-run green. The 8-chapter `--freecam` regression was not
run separately: no item touches world loading.

**Original approach (kept for reference).**

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

## B13 ☑ The autogyro under the assist

**Landed.** No law branch (B11 found `is_autogyro` reaches no plant term). Keys, pad and flight
stick never pass the autogyro exchange, which lives in `MouseFlight.Read` alone, so the one change
is that `FlightController.MouseFlightRead` asks `Read` for an aeroplane's mapping while the seat is
Assisted: the cursor reaches the law as roll and nothing exchanges the law's output afterwards.
`flight-assisted-seat` flies the Hoplite through the seat for 3 s on the left key and on a cursor
left of centre and checks a left turn of over 20 degrees banked over 10 degrees left; the bank is
what a cursor exchanged before the law would not produce (it would reach the law as yaw, which the
law passes to the rudder with the wings held level). The suite also reads the cursor under both
schemes: yaw under Original, roll under Assisted. Measured: the left key turns the Hoplite 103.5
degrees in 3 s at up to 80.1 degrees of bank, the cursor at -0.8 turns it 71.8 degrees at up to 64.0.
A mutant that kept the exchange under Assisted failed the suite: 73.5 degrees of turn at 0.0 bank.

**Verified.** The complete `RunTests.ps1` on the merged plan tree (A1, A2, B11, B12 and B13 with
main merged in): build, units 8462 passed and 0 failed of 8468 (6 skipped for missing extracted
data), engine 679 passed and 0 failed with engine errors clean, goldens 41 shots hash-identical. The
first pass failed one unit, `SuiteShardsTests`, because `chase-level`, `flight-assisted-law` and
`flight-assisted-seat` carried no weight in `analysis/engine-suite-weights.json`; their measured
shard times were added and the unit stage re-run green. The 8-chapter `--freecam` regression was not
run separately: no item touches world loading.

**Original approach (kept for reference).**

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

**Found at the controls.** "Moving up or down and releasing the stick moves the plane strongly in
the other direction keeping only a small part of the climb afterwards." The hold captured the
path's climb angle at release, and the path lags a pull by 6 to 25 degrees, so the hold swung the
nose back through that whole lag and settled on the path's angle. Measured in
`flight-assisted-law` on the Bloodhawk and Hoplite (full stick 0.6 s, half and 0.2 for 1.5 s,
half with 0.6 sideways, each up and down, then 8 s centred): the nose fell back 9 to 37 degrees
at 17 to 64 deg/s, and the path kept only the angle it had at release. The fix holds the nose's
angle at release (`AssistedFlight`'s else branch), and the path lean counts the path's own
climb rate over `PathLead`/`lift_accel_rate` seconds as already closing the error, bounded
between zero and the plain error, so a converging path no longer pulls the nose past the held
angle. `ClimbTrim` is kept across a pull: the banked releases, which carry it, met the same
bounds. After the fix the nose falls back at most 3.1 degrees at up to 12.4 deg/s and the path
settles within 2.2 degrees of the release nose (checked at 4 degrees, 15 deg/s and 3 degrees; the
path's lag at release over 4 degrees is the able-to-fail control). The turn, wings-level, loop and
Immelmann checks read as before, and a unit test,
`AReleasedPull_HoldsTheNoseWhereItWasLeft`, pins the capture on the default airframe.

**Found at the controls.** "Can you merge both into one setting? They don't work good separate."
The `ChaseCamera` option is gone: the control scheme alone picks the chase, Assisted flying the
level frame (`FlightController` passes `ControlScheme == Assisted` to `Chase`). The CONTROLS page
keeps its 69 px band (a shorter crop would cut the panel's rim) with the one `Controls` row centred
in it. A saved `chaseCamera` field is never read and drops on the next save
(`BindingStoreTests.AnOldChaseCameraFieldIsIgnored`). `flight-assisted-seat`'s
`ChaseFollowsTheScheme` holds roll for 2 s: the Original seat's camera banks with the plane, the
Assisted seat's right axis stays level within 0.01.
