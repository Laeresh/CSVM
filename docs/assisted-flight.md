# Assisted flight: the control law and why it is shaped this way

How the Assisted control scheme turns a stick into control-surface deflections, what in the decoded
plant forces each part of that design, and which numbers are tuning rather than decode. The scheme
is remake-only and the original game has nothing like it. The law is
`CSVM/src/Flight/Airframe/AssistedFlight.cs`, routed from [`architecture.md`](architecture.md) into
[`architecture/Flight.md`](architecture/Flight.md); the plant it flies is decoded in
[`org/flightModel.md`](org/flightModel.md).

## Contents

- [What the law does](#what-the-law-does)
- [The contract](#the-contract)
- [The wiring](#the-wiring)
- [The mouse and the autogyro under the assist](#the-mouse-and-the-autogyro-under-the-assist)
- [Why not the AI's steering law](#why-not-the-ais-steering-law)
- [What the plant does, and how the law follows from it](#what-the-plant-does-and-how-the-law-follows-from-it)
- [The autogyro](#the-autogyro)
- [The tuning constants](#the-tuning-constants)
- [The verification layers](#the-verification-layers)

## What the law does

- **Sideways stick is a coordinated turn in proportion to the input.** Inside `RudderBand`, the
  first fifth of travel, the wings stay level and the rudder alone turns the plane flat. Past it the
  plane banks, and the elevator adds the pull the turn needs. A centred stick rolls the wings level.
- **Full sideways stick is the hardest turn the plane can hold without losing height** at its
  current speed. It flies at full pull and bleeds speed as a hard turn does.
- **Stick back and forth is a climb or dive against the horizon**, not a plane-axis elevator. A pull
  while banked climbs rather than tightening the turn.
- **A centred stick holds the climb angle it was left at.**
- **A held pull flies through vertical into a full loop.** Released while inverted, the plane rolls
  upright on its new heading, so half a loop ends as an Immelmann turn.
- **The separate yaw input lands on the rudder after the loops**, for fine aim, and nothing in the
  law fights it.
- **There is no stall or terrain protection.** The plane can stall and can fly into the ground.

The law only ever writes deflections into the unchanged plant. It never sets attitude or velocity,
so each airframe keeps its own turn rate, stall and speed loss, and multiplayer needs no protocol
change.

## The contract

`AssistedFlight.Steer(FlightInput stick, FlightModel model, ref AssistedState state, float dt)`
takes the pilot's summed stick in the plant's own signs: Roll + turns left, Pitch + climbs, Yaw +
noses left. It returns the same struct with Roll, Pitch and Yaw replaced. Throttle, boost and every
other field pass through untouched.

The caller owns one `AssistedState` per seat and hands it a fresh `default` on spawn, respawn and a
scheme switch; the first step primes it from the plane. The climb hold engages only on a Pitch of
exactly zero, which is what every device produces at centre.

## The wiring

`FlightController` is the one caller. Its keyboard arm (`ReadKeyboard`) sums the keys, the pads, the
flight sticks and the mouse into one clamped `FlightInput`, and its return goes through `Steered`:
a seat with `IsHumanPiloted` set and its own profile's `ControlScheme` on Assisted hands that stick
to `Steer`, and every other seat passes it as read. The sum is in the plant's signs already, the
separate yaw input sits in its Yaw, and the throttle is the live lever. The `dt` is the sim step's.

- **Only a person's seat.** An AI seat reads `PilotInputSource` and a remote seat a neutral
  scripted hold, so neither reaches the keyboard arm. A seat that does read it with
  `IsHumanPiloted` clear stays on the raw stick whatever its profile says.
- **The scheme is the seat's own.** It is `BindingProfile.ControlScheme` on the seat's keymap
  profile, taken through `ApplyProfile` at build and when a Controls page is accepted over the
  pause, so each player's seat in splitscreen and multiplayer flies its own choice.
- **The state resets** when `ControlScheme`'s setter sees a different value, on every `Respawn`
  (which also runs at first spawn), and on every held step (`Held`), since a held aircraft is
  placed rather than flown. An accept that keeps the scheme keeps the state.
- **A wholly held seat** (`FlightControlHold.All`, and a pause sheet over a multiplayer flight)
  feeds a centred stick through the same seam, so under Assisted the plane levels its wings and
  holds its climb rather than flying on with its bank.
- **Each device centres to exactly zero.** The keyboard ramp assigns 0 when its key is up; a pad's
  pitch and roll pass `AnalogAxes.PadCurve`, zero inside 0.15; a flight stick's full-axis binding
  reads `ControlValue.None` inside its stamped deadzone (0.02); the mouse's `MouseFlight.Gate` is zero
  inside 0.1. A flight stick whose centre has drifted past its own deadzone reads a small pitch,
  and the law takes that as a slow climb or dive rather than the hold. The seam adds no band of its
  own, because a band wide enough to matter would also swallow the pad's finest pitch, which
  `PadCurve` squares.

## The mouse and the autogyro under the assist

`MouseFlight.Read`'s autogyro exchange puts the cursor's sideways travel on the yaw. Under Assisted
the seat asks `Read` for an aeroplane's mapping instead, so the cursor reaches the law as roll like
every other device, and nothing exchanges the law's output afterwards.

## Why not the AI's steering law

`AiControlLaw.Steer` takes an aim point and returns a `FlightInput`, so feeding it a point displaced
by the stick looks like a shortcut. Five readings of it rule that out:

- Its output stage is near-bang-bang: `ai_input_scale` 3.5 against a limit of 1 saturates at 0.29 of
  body-frame error, so no proportional stick maps through it.
- It chooses bank or rudder by the airframe's authored `rudder_tol` (1.0 on the Balmoral, which is
  all rudder), not by a share of stick travel, so the rudder-only band cannot be expressed.
- Its elevator joins only once the roll command is under `CrossDeadband` (0.06 to 0.08), so there is
  no pull while the bank is still coming in.
- It clamps the aim point between 20 m and `flight_ceiling` and drives the throttle lever. Both
  contradict the scheme, which has no protection and passes the throttle through.
- It is tuned for the AI force path, which has neither the weathervane nor the stall nose-drop; the
  human plant has both.

## What the plant does, and how the law follows from it

These are read from `FlightModel.Step` and checked by flying the law into the plant on the
Bloodhawk, Hoplite, Balmoral, Fury and Peacemaker.

**Lift follows the nose, not the wing.** The plant's lift is the demand (the relative-wind swing
plus weight) projected perpendicular to the nose. Bank alone neither costs lift nor sets a turn
rate; the path turns by following the nose at `lift_accel_rate` once the nose leads it past
`liftAOAs[0]` (5 degrees on all five airframes). So the law commands a world turn rate ψ and a world
climb rate γ, and projects them through the bank φ onto the body rates:

- pitch = γ·cos φ + ψ·sin φ
- yaw = ψ·cos φ − γ·sin φ

A pull in a bank therefore climbs against the horizon, and part of it lands on the rudder.

**One set of gains serves every airframe.** Each body rate becomes a deflection through the plant's
steady rate per full deflection, `2·torque·rec_inertia·authority(speed)/ang_momentum_damp`: the
plant integrates torque against an exponential damping, so a held deflection settles there. Roll and
pitch add a correction on the measured body rate. Yaw does not, so nothing reads the pilot's yaw
input back and fights it.

**The bank is chosen to put the turn on the elevator.** The bank target is the tilt a real wing
needs for the banked share of the turn, atan(V·ψ/g). Past `yaw_fade_out` the rudder keeps only
`yaw_high_speed`'s share of its authority, so with the wings held shallower the rudder saturates at
half stick.

**The climb hold closes on the nose, not the path.** The path ignores a nose that sits inside
`liftAOAs[0]` of it, so a loop closed on the path angle alone limit-cycles across that 5 degree dead
band (about ±7 degrees at a 5 s period). The nose target is the held climb angle leaned on the
path's error (`PathGain`), and an integrator (`TrimGain`) carries the nose-down bias the plant's
bank-to-yaw coupling adds in a turn.

**The held angle is the nose's at release.** While stick Y is off centre the held angle follows the
nose's climb, and centring keeps the last one. The path lags a pull: after 0.6 s of full stick the nose
leads it by 25 degrees on the Bloodhawk and 18 on the Hoplite. A hold on the path's angle at release would
swing the nose back through all of that lag, so the player would see the nose drop and keep only
part of the climb. The lean takes only the error the path is not already closing: the path's
climb rate over `PathLead`/`lift_accel_rate` seconds counts as closed, and the lean is bounded
between zero and the plain error. While the path still converges on the held angle the nose
stays where it was left. Once the path slows inside the dead band the lean is the plain error,
as in steady flight. Past `NearVerticalNoseY` the hold stands down, so a pull released with the
nose within 26 degrees of vertical flies on until the nose comes back below that.

**The hardest level turn is a loop, not a formula.** Lift constants are decoded only in part, so the
full-stick turn rate `AssistedState.TurnCap` is found in flight: at full stick it rises at
`CapRecover` while no surface is saturated, and the sink below the held climb, led by its own rate
(`CapLead`), cuts it through `CapGain`. It settles at full pull. Part stick scales from an estimate,
`lift_accel_rate·2·sin(maxAOA/2)` (how fast the path can chase a nose the opposing-command limiter
still lets lead it), until full stick has measured the real value. The estimate is a scale and never
the full-stick answer.

**A loop is measured from inverted.** Over the top of a held pull the bank is measured from
inverted, so the loop flies on instead of rolling out halfway round. Released inverted, the bank
target is upright and the plane rolls out on its new heading. With the nose within 26 degrees of
vertical the bank has no meaning and the bank loop stands down.

## The autogyro

`is_autogyro` reaches no flight-plant term ([`org/flightModel.md`](org/flightModel.md)), so the
law flies the Hoplite unchanged. The one rule is at the caller: the assist takes the stick from
before `MouseFlight`'s autogyro exchange ([the wiring](#the-mouse-and-the-autogyro-under-the-assist)),
so stick left banks the Hoplite left and turns it left on every device.

## The tuning constants

Every constant below is a feel choice to settle at the controls, not a decoded value.

| Constant | Value | What it sets |
|---|---|---|
| `RudderBand` | 0.2 | share of sideways travel that turns on rudder alone |
| `FlatRudder` | 0.5 | rudder deflection the flat turn reaches at the band's edge |
| `BankGain` | 4 /s | roll rate asked per radian of bank error |
| `MaxRollRate` | 3 rad/s | the most roll rate the bank loop asks for |
| `MaxClimbRate` | 1.5 rad/s | climb rate full stick asks for, past any elevator, so full stick is full pull |
| `HoldGain` | 2 /s | nose climb rate per radian the nose sits off its target |
| `PathGain` | 1 | how far the nose target leans per radian the path sits off the held climb |
| `PathLead` | 2 | how many 1/`lift_accel_rate` seconds of the path's climb rate count as already closing its error |
| `TrimGain` | 2 /s² | the hold's integrator |
| `RateGain` | 1 | the roll and pitch correction on measured body rate |
| `CapGain` | 0.05 (rad/s)/(m/s)/s | turn rate cut per m/s of sink at full stick |
| `CapLead` | 1.5 s | how much of the sink's own rate the full-stick loop adds |
| `CapRecover` | 0.2 rad/s² | how fast the turn cap rises at full stick, and returns to its estimate off it |

`NearVerticalNoseY` (0.9) is geometry rather than feel: past it bank is undefined.

## The verification layers

- `CSVM.Tests/AssistedFlightTests.cs` pins the deflections at fixed states on the default airframe:
  rudder alone inside the band, wings level on a centred stick from either side, full pull at full
  stick in a steep bank against half stick, the held climb angle, a pull at 60 degrees of bank putting
  climb on the rudder against a wings-level pull, the yaw input passing straight through, a held
  pull over the top against a release, and a flown pull or push released with the path lagging,
  which holds the nose's angle and settles the path on it.
- The engine suite `flight-assisted-law` flies the law through a real seat's sim step
  (`FlightControllerBuild.InputSource`) on the Bloodhawk and the Hoplite, off the identity heading and
  high over an empty world (DIAG-19, INSTR-22). It checks that 12 s of full sideways stick loses
  under 10 m at a steady rate and full pull, against the same stick flown raw losing over 40 m
  (METHOD-9); that centring levels the wings inside 2 s; that a held pull closes a loop on the entry
  heading; that a pull released over the top is upright on the reversed heading inside 4 s; and
  that a climb or dive released from full, half or gentle stick, wings level or banked, keeps its
  nose within 4 degrees of where it was left, never pitching back faster than 15 deg/s, and settles
  its path within 3 degrees of that angle in 8 s.
- The engine suite `flight-assisted-seat` flies the scheme through a seat's own device reader. A
  held roll-left key reads raw under Original and through the law under Assisted; a scheme switch
  and a respawn reset the state while an accept that keeps the scheme does not; an AI seat on
  Assisted reads the raw stick and never primes the law; and the Hoplite turns left on the left key
  and on a cursor left of centre, banking into the turn, which a cursor exchanged before the law
  would not do.
- None of these layers judges the feel. That is the player's at the controls.
