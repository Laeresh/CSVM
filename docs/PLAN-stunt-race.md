# Stunt race: a time-attack Danger Zone race, solo, split screen and network

**ACTIVE PLAN** (written 2026-10-03), worked alongside `PLAN-bots.md`. When every item lands, the
closing commit deletes this file, records the completion in its message, and clears the "Current
status" pointer; any live prose linking this file by path is unlinked in the same commit.

This plan turns the stunt run into a Trackmania-style time attack and runs it in three places: a
solo Instant Action stunt run, the split screen stunt race, and a new network Stunt Race. A race is
a shared time window over one chapter's Danger Zone course; every pilot flies as many runs as the
window allows, restarting at will, and the fastest completed run wins. Pilots do not collide, carry
no weapons and draw as ghosts when close. The original game has no race: its stunt flying is a
single-player Instant Action mission type and its multiplayer lobby offers Capture the Flag,
Deathmatch and Zeppelin vs Zeppelin only (`UI/Menu/DogfightLobby.cs:8-20`). The original's single
player has no respawn either, so the respawn and restart rules below are this port's own
(`docs/controls.md`, the `Backspace` row), and changing the solo rules breaks nothing faithful.

The plan is drawn from `BL-314` (backlog.md), whose countdown shape (a rolling start on rails, GO
equal to today's spawn state, `--det` untouched) and its four traps are carried into A2 unchanged.
`BL-314` is still open (no landing under its id, no countdown in `StuntMission.cs` or
`StuntRace.cs`), and its entry is a pointer at this plan.

Out of scope: bots in a race (a follow-up to `PLAN-bots.md` once it lands, Decision 14), joining a
race in progress (Decision 13), race ghosts or replays of a best run, and any race mode in the
Built-in (legacy) menu beyond what its Instant Action screen already reaches.

## Milestone goal

- A solo stunt run follows the time-attack rules: a tap of respawn puts the pilot back at the last
  cleared Danger Zone with the clock running, a hold restarts the run from the start behind a short
  on-rails count, and the run clock starts at GO.
- The split screen stunt race is a time attack: a shared window, an opening count, per-pilot
  restarts, best-run ranking, a live leaderboard, ghosts up close, weapons off, no collisions.
- A network host can pick Stunt Race in the Original Multiplayer Lobby, choose one of the six
  chapters with a course and a time limit, and race guests under the same rules, each run timed by
  its own pilot and the leaderboard and window kept by the host.
- Solo and race bests share one store key per course and plane.

**One race implementation drives split screen and network.** The network item adds transport and
authority to the split screen race, not a second race.

## Decisions (2026-10-03)

| # | Question | Decision |
|---|---|---|
| 1 | Time attack or one run each? | **Time attack.** One shared window; each pilot restarts at will and only their best completed run counts. |
| 2 | When does the on-rails count run? | **Both**: a shared opening count at the window's start, and a short personal count on every restart. The host already holds every machine at the load screen until all are in, so the opening count is the race-start feel, not load sync. |
| 3 | What does a crash mid-run do? | **Tap respawn: back at the last cleared Danger Zone**, progress and clock kept (Trackmania checkpoints). **Hold the same respawn control: restart the run** from the start line. |
| 4 | Where is it picked, and which world? | **A fourth Type, Stunt Race, in the Original lobby.** The Environment box greys Above the Clouds (C1C has no course); the match flies the chosen chapter's Instant Action stunt course (its `IA1` world and `dzones`), not an MP map; Mission Options keep only the time limit. |
| 5 | Weapons? | **Off for the whole match**: fire does nothing, weapon readouts drop off the HUD, ground targets on the course world stay inert. |
| 6 | How are other pilots drawn? | **Ghosts when near, solid when far**, a fade from solid beyond 80 m to ghost inside 40 m (both TUNE). Callsign label from the marker; not targetable; no spyglass. |
| 7 | Who times a run? | **The owner**, on its own sim clock, reporting zone splits and the finish to the host. The host owns the window clock, the leaderboard and the match end. |
| 8 | A run in progress at time up? | **It may finish and still counts**, capped at 2 minutes after time up; no run starts or restarts after time up; the HUD shows FINAL RUN in that stretch. |
| 9 | Ranking and boards? | **Best completed run, fastest first**; pilots with none rank below every finisher by most zones in any run, then lowest time to them. A compact live leaderboard (place, leader's best, your gap), the full table on the held scoreboard key, and `StuntRaceBoard` at the end with each pilot's best-run splits. |
| 10 | Save race times? | **Yes**, each machine its own pilot's runs. |
| 10b | Change solo to the same rules? | **Yes.** Solo takes the tap/hold rules and the short restart count, so solo and race runs both start at GO in the same spawn state and share the one existing `<chapter>/IA1/<plane>` key; no `race/` namespace. Old solo bests stay valid (set under harsher rules). |
| 11 | Split screen race too? | **Yes, the same time-attack race.** The abreast `StartGrid` retires from the race (all pilots start from the one spawn point, no collisions); co-op keeps the grid. |
| 12 | Default window and where it is set? | **5 minutes.** Network: the lobby's existing time box, defaulting to 5 when the Type turns to Stunt Race. Split screen: a time row on the Instant Action screen, shown for Stunt Flying with more than one seat, offering 3, 5, 10, 15 minutes. |
| 13 | Join a race in progress? | **No.** A late joiner lands in the lobby, as every network match already does. |
| 14 | Bots? | **Not in this plan**; a follow-up to `PLAN-bots.md`. |
| 15 | Count lengths and look? | **Opening: about 5 s** (READY for 2 s while the field rolls in on rails, then 3, 2, 1, GO). **Restart: 3, 2, 1, GO.** Large centred numbers on the chrome type scale, a beep per number and a higher tone on GO from the shipped UI sounds; stick and throttle are read but ignored during a count, held inputs take effect on the first frame after GO. |
| 16 | A pilot leaves mid-race? | **Their best stays on the boards marked "left"** and ranks normally; their ghost goes. The host leaving ends the race as a Dogfight does. A race left with one pilot runs on to the window's end. |
| 17 | After the race board? | **Network: the host's board offers Restart** (a new window, same course and rules, fresh opening count) **and Lobby**; a guest's board waits for the host and offers Leave. **Split screen: Restart and Back** (to the Instant Action screen). |
| 18 | How is it carried? | **This plan**, three waves: solo rules, then split screen, then network. Not dispatched before 0.3.0 ships. |

## ⚠ Read this before implementing anything

Nothing here was decoded or disproven, so there is no wrong-claims table. Every design item is
**lead-only**: it rests on the grilling, not on a measurement. The facts about today's code cited
below come from a read of the tree at 198671cd and are path-and-line traced; re-read them before
building on them, since stunt, race and lobby code moves.

Two rules from `BL-314` bind every item:

- **`--det` never reaches a count.** Like the grid today, every count is reached only through the
  race and restart paths, so a scripted run stays byte-identical and every pinned golden holds.
- **The instrument is a hand-flown sitting.** An automated check can prove the rules and the wire;
  whether a count feels like a race start comes from the controls (a split screen sitting, then a
  two-machine sitting).

`PLAN-bots.md` Decision 7 ("a human who joins a running match takes the newest bot's seat")
assumes a late join into a running match; per Decision 13 here, a late joiner lands in the lobby.
That plan needs the correction before its Wave touching joins is worked.

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

### Wave A, the time-attack run (solo)

1. ☑ Tap respawn returns to the last cleared Danger Zone; hold restarts the run
2. ☑ The on-rails restart count, and the run clock starting at GO; one bests key

### Wave B, the split screen race

11. ☑ The split screen race becomes a time attack: window, opening count, best-run ranking, FINAL RUN, boards
12. ☑ Race presence: no collisions, weapons off, ghosts when near
13. ☑ The Instant Action time row for a multi-seat Stunt Flying run

### Wave C, the network race

21. ☐ Stunt Race in the Original lobby, flying the chapter's stunt course over the network
22. ☐ Owner-timed runs on the wire; the host's window, leaderboard and match end
23. ☐ Network race end: Restart and Lobby, guests waiting, pilots leaving

## Dependency and parallelism notes

The waves are a chain: B builds on A's run rules and count, C carries B's race over the wire. A1
and A2 both edit `StuntMission.cs` and the respawn path in `FlightController.cs`: run them in
order, not in parallel. B11 and B12 are independent in files (race bookkeeping and boards against
collision, weapons and rendering) and may run side by side with that boundary. B13 is UI only and
can run beside either. C21 blocks C22 and C23; C22 and C23 both edit the race's network messages and
should run in order.

---

# Wave A, the time-attack run (solo)

## A1 ☑ Tap respawn returns to the last cleared Danger Zone; hold restarts the run

**Goal.** In a solo stunt run, a tap of the respawn control (`Backspace` / pad Y) after a crash or
in flight puts the pilot at the exit of the Danger Zone they most recently completed, heading
through it at spawn speed, with zones and clock kept; holding the same control restarts the run
from the start line with zones and clock cleared. With no zone cleared yet, a tap returns to the
start pose with the clock kept.

**Evidence (confidence: lead-only for the design; traced for today's code).** Today a crash plus
respawn keeps zones and clock and returns to the mission spawn (`Flight/Airframe/FlightController.cs:1281-1283`,
`Flight/Modes/StuntMission.cs:107-108`); `Reset`/`Rerun` clears both
(`StuntMission.cs:274-291`, `FlightController.cs:1274-1279`). Zones are order-free; each stores
`CompletedAt` and `CompletionOrder` (`StuntMission.cs:25-32, 511-517`), so "most recently
completed" is the highest `CompletionOrder`. Gates come from the `dzpathN` mesh's two aperture rings
(`StuntMission.cs:412-448`). The respawn being a port action, not the original's:
`docs/controls.md`, the `Backspace` row.

**Approach.** *The exit ring (traced-to-code for the rule, lead-only for the choice).* The data
authors no direction for a zone: the original's AI enters a `dzpathN` ribbon from whichever end is
nearer (`FUN_00421500`, `docs/org/aiPilot.md` "Two entries"), and the two rings' normals disagree in
sign within a pair (C1B `dzpath1` and `dzpath4`, C5 `dzpath16`), so neither the data nor the ring
winding says which ring is the exit. The exit is therefore the pilot's own: the ring whose crossing
completed the zone, and of two crossed in the same frame the later along the movement segment.
`StuntMission.Update` records it as `StuntZone.ExitGate` with the crossing direction as
`ExitTravel`.
*The heading and position (lead-only, measured over all 54 IA1 zones).* The route ribbon, not the
ring normal. The ring planes follow the structure, not the flight line: the red portal rings of
C1's three train tunnels sit 45° to 51° off the tunnel's level ribbon (normal Y up to 0.76), so a
normal heading would pitch a respawn 50° up out of a tunnel. `StuntMission.ReturnPose` puts the pilot on the zone's own
ribbon (`DangerZoneRibbon.FromPolyline` over the route polygon, the line the original's AI flies
through the zone) at the point nearest the exit ring's centre, heading along the ribbon's tangent
signed to agree with `ExitTravel`; a zone whose route builds no ribbon falls back to the ring centre
and its signed normal. Measured with both flight orders: every zone completes, every heading agrees
with the crossing (dot 0.46 to 1.00), and the return sits 0 to 44 m from the exit ring centre except
C1 Passenger Hangar (43 and 69 m), C4 Zep Dock (74 and 81 m) and C5 Brooklyn Bridge (238 m, a 1.1 km
ring), where the ribbon passes that far from the ring's centre. C1B Bootlegger's Tunnel is
a vertical shaft whose ribbon is vertical at the gates (heading Y ±1.00), so its return points
straight up or down; `ReturnToLastZone` rolls such a heading off `Vector3.Back` instead of world up.
The speed is `_spawnSpeed`, the `spawnSpeed` `Setup` took from `FlightStart.SpeedMps`
(`HumanFlightAdapter.cs:555`), never a constant; the lever is the spawn lever, as every respawn.
*The pose path (traced-to-code).* `Respawn` takes a one-shot `_zoneReturn` and never writes it into
`_spawnPos`, because `RespawnPlacement`, `WarpTo` and `Activate` all overwrite the spawn pose and a
hold must still find the start line. `Respawn` also calls `StuntMission.Relocated`, so the jump to
the new pose is not tested against every gate as a flown segment.
*The split.* One `TapHoldButton` per `FlightController`, so every splitscreen seat splits its own
button. In the crashed branch a tap calls `ReturnToLastZone`, a hold `Rerun`, and the crash camera's
auto-respawn timer takes the tap's return (it ticks only while the button is up, as today's
short-circuit already did). In flight the split runs inside `ReadKeyboard` where the old level read
was, only where `AllowLiveRespawn` is true; where it is false (Instant Action, which pins it) the
button is stepped reading up, so a refused press cannot resolve after a crash. A seat with a
`RespawnRequest` (a granted network return) keeps the old single press. Non-stunt sessions keep the
old code path untouched.
*The hold duration (traced-to-code, TUNE).* `TapHoldButton.PadHoldSeconds`, 0.25 s, the constant
d-pad up's targeting split and the pad weapon selectors already share (`Utils/TapHoldButton.cs:29`),
already marked TUNE there. The tap resolves on release and the hold fires once at the threshold, so
a long press never also taps. The one reason not to reuse it is that the hold is destructive (it
throws the run away) where the targeting hold is not; that is a judgement for the controls sitting,
and raising it means a constant of its own beside this split.
*Trap check (traced-to-code).* The tap and the hold both go through `Respawn`, which repairs,
restocks and refuels as today's stunt respawn already does, and neither is read in flight where
today's live respawn was refused, so neither is more generous than today. The in-flight read now goes
through the reentry latch and `CommandsHeld`, which is stricter than the old raw level read.
`docs/controls.md`'s `Backspace` row is updated.

**Model recommendation.** Opus for the design read (choosing the exit and heading needed the
data measured across every zone and the ribbon decode), Sonnet for the docs edits once the rule is
fixed.

**Verify.** Built: `CSVM.Tests/StuntReturnTests.cs` over C1/IA1's real gates and ribbons: no return
with no zone cleared; one zone flown both ways returns abeam the right exit ring heading the right
way; zones flown 3, 1, 2 return exactly where zone 2 alone would and away from zone 3's and zone 1's
returns; `Reset` leaves no return. Engine suite `stunt-respawn-tap-hold` (in
`Testing/FlightInputHandoffSuites.cs`, weighted in `analysis/engine-suite-weights.json`) on a real
human rig set up at a distinct 71.5 m/s spawn speed: a crashed tap with no zone returns to the start
with the clock running on; a crashed tap after a zone returns abeam its exit, nose along the route,
at 71.5 m/s, zones and clock kept; an in-flight tap after a second zone returns to that one; an
in-flight hold and a crashed hold both restart at the start with zones and clock cleared and their
release taps nothing; pinned as Instant Action pins it, neither press is taken in flight while a
crashed tap still returns (able-to-fail control). Presses straddle the threshold, two frames either
side of 15 at 60 Hz. Mutation-checked: return pose ignored, return written into the spawn, in-flight
hold returning instead of restarting, the pin ignored in flight, the crashed tap restarting, the
threshold raised and lowered 20 %, each turned the suite red; choosing the lowest `CompletionOrder`,
always taking the green ring, and dropping the heading's sign each turned a unit red. No pinned
golden flies a stunt crash (`c1-stunt-marker` is an early-frame shot), so every golden is unchanged
by construction; the orchestrator's battery confirms.

**⚠ Traps.** A respawn while still flying must not become a free repair, restock and refuel beyond
what a stunt run already allows (`docs/controls.md`, the `Backspace` row). The respawn speed is the
mission's own spawn speed, carried on `FlightStart` (`BL-314` trap 1). A respawn arms no
collision grace (`AircraftLifecycle.Respawn` re-arms no spawn window), so a return inside a narrow
aperture rests on the ribbon being the AI's flyable line; if a return crashes on arrival at the
controls, the fix is the return's placement, not a grace window that would let a pilot fly through
structure. The crash prompt still reads "Press %1 to respawn" and names no hold.

**Verified.** <pending orchestrator run>

## A2 ☑ The on-rails restart count, and the run clock starting at GO; one bests key

**Goal.** A hold-restart, and a solo run's first start too (the user's ruling: every solo run starts
the same way, at GO in the spawn state), runs a 3, 2, 1, GO count during which the
aircraft rides a kinematic level walk that ends exactly on today's spawn state at GO; the run clock
starts at GO. Solo bests record under the existing `<chapter>/IA1/<plane>` key.

**Evidence (confidence: lead-only; the count shape is `BL-314`'s, decided before this plan).**
`BL-314`: a rolling start, not a freeze; the walk is driven into `_model.Reset(pos, attitude,
SpawnSpeed, throttle)`, the existing call shape in `FlightController.cs`; nothing is simulated
during the count. `StuntMission.Elapsed` advances in `Tick(dt)` every physics frame and keeps
running through a crash freeze (`StuntMission.cs:105-109, 239-246`, `FlightController.cs:1948-1951`).
`ScoreStore` keys are `chapter/mission/plane` (`Flight/Modes/ScoreStore.cs`,
`Session/Roster/HumanFlightAdapter.cs:481`); race totals are not recorded today
(`StuntRace.cs:50-51`, `HumanFlightAdapter.cs:480`).

**Approach.** *The controller (traced-to-code).* `Flight/Modes/StartCount.cs`, engine-free, one
per seat (`FlightController.StartCount`). `Begin(phases)` takes the figures as data: `Restart` is
3, 2, 1 at a second each, and `Opening(readySeconds)` puts READY first for B11's window opening.
`Advance(dt)` steps it on the sim dt and answers a beat when a figure comes up and GO on the step
that reaches the end. The first beat comes from the first `Advance`, so a count begun while the
session is still building sounds when the sim moves. `Figure` is what to draw, GO lingering for
`GoSeconds` (1 s, TUNE). `WalkPose(spawnPos, spawnAttitude, spawnSpeed)` is the walk.
*The seat (traced-to-code).* `FlightController.BeginStartCount(phases)` begins the count from the
spawn pose a respawn just set, places the aircraft on the walk's first point and snaps the camera;
`Rerun` calls it after its `Respawn` when `RestartCount` is set (`FlightController.cs:1335`). In
`SimStep` the count is decided before anything steps (`FlightController.cs:2088`): while it runs,
`StuntMission.Tick` is not called, `--debug-scoreboard`'s synthetic finish waits, and
`StepStartCount` (`:3662`) writes the walk into `_model.Reset(pos, spawnAttitude, spawnSpeed,
lever)` and returns before the input read, the model step, the sweep, weapons, turrets, the stunt
gate test, the camera latch and the under-map backstop. The GO step is still the count's and leaves
exactly the state `Respawn` leaves; the clock reads 0 at its end, and the step after it reads the
controls and ticks the clock to one step. `Respawn` cancels a running count (`:1390`), so a tap
(`ReturnToLastZone`) never runs one and a respawn is never dragged back onto the walk. A held
respawn read inside `InputSource.Read` that begins a count leaves the step at once (`:2207`), so the
rest of that step does not fly and sweep from the walk's first point.
*Trap 1, read (traced-to-code).* The rule the trap forbids is a setback whose END follows from
arithmetic on a speed, which hard-codes one map's number. Here the end is the spawn pose by
construction: `WalkPose` answers `(spawnPos, spawnAttitude)` itself once nothing remains
(`StartCount.cs:138`), never a sum that lands near it. How far back the walk STARTS is the
mission's own spawn speed, read at run time from `FlightStart`, times the time left, so the walk's
velocity is the one the flight model takes at GO and the hand-over does not jump. A different
mission or a re-read speed changes only the start distance (54 m at C1/IA1's 18 m/s, 174 m at
C1C/M01's 58 m/s). The walk runs along the spawn's own nose, which is level on every Instant Action
spawn (`SpawnPoint` carries a yaw only).
*Trap 2 (traced-to-code).* `StuntMission.Elapsed`'s comment (`StuntMission.cs:121`) and `Tick`'s
(`:294`) each now say first that the clock starts at GO, and then that once started it never stops,
through the crash freeze; `SimStep`'s comment says the same.
*Trap 3 (traced-to-code).* Nothing is simulated during the count (the model is reset each step,
never stepped) and nothing is frozen: the clock, the world, the AI, audio and the camera all run.
*A walk through structure (traced-to-code).* The walk passes kinematically through whatever lies
behind the spawn. The airframe's own
contact path is the sweep and centre ray inside the flying branch, which a count step never
reaches, so a walk through terrain or a building resolves no contact and crashes nothing. Another
aircraft's round or ram still reaches it, as at any spawn; a solo Stunt Flying run fields none
unless the Instant Action wizard adds waves. A hull downed mid-count freezes the count and the
clock starts ticking through the crash freeze; the crash branch runs as usual, and the respawn that
follows cancels the count.
*Who gets a count (traced-to-code).* `GameSession` hands `HumanRosterBindings.SoloStartCount`
`StartCount.Restart` only for a stunt run with no race and not under `--det` (`GameSession.cs:1916`);
`HumanFlightAdapter` sets it as the seat's `RestartCount` and begins the first count after `Setup`
(`HumanFlightAdapter.cs:460, 565`). That covers `--stunt`, the Instant Action Stunt Flying run
(its Restart rebuilds the session, so it counts again) and the solo board's R. A remote seat, an AI
rig, a race seat and every suite-built rig carry no `RestartCount`.
*The look (direction-sound, size TUNE).* A new top rung of the chrome type scale, `ChromeSize.Count`
at 72 frame units (`ChromeType.cs`), drawn centred by `StuntRunHud` in the HUD blue; GO in the
completion green, fading over its last 0.4 s.
*The sounds (traced-to-data, choice TUNE).* The shipped UI sounds are the four menu cues under
`extracted/rof/ASSETS/SOUNDS` (`Launch/MenuCueTable.cs:17-20`). Decoded from their MS ADPCM:
`MOUSEOVER.WAV` and `MOUSECLICK.WAV` are noisy clicks (energy near 3.8 and 4.1 kHz, 50 and 87 ms),
`ENTERTEXT.WAV` is a 4 ms tick, and `ENTERTEXT_ERROR.WAV` is the one plain tone (1.56 kHz, 25 ms, no
second peak above 2 % of the first). The beat is `ENTERTEXT_ERROR.WAV`; GO is `MOUSECLICK.WAV`,
louder, longer and higher (`FlightAudio.cs:32, 36`). They load through a directory `SoundArchive`
over that folder (`GameSession.MenuSounds`) into two players on the seat's `FlightAudio`
(`BindStartCount`, `OnStartCount`), under `MixGain`; a missing file is a logged silent cue.
*One bests key (traced-to-code).* The solo board records `<chapter>/<mission>/<plane>`
(`HumanFlightAdapter.cs:489`, `StuntScoreboard.cs:68`) and the Instant Action wrap-up the same
shape (`InstantActionDirector.cs:845, 137`), the mission being `IA1` for every stunt course. Both
record `StuntMission.Elapsed`, which the count leaves at 0 until GO, so the only change is that the
time starts at GO. The Instant Action mission clock (the wrap-up's "Time to Complete Mission") is
the runtime's own and still includes the count. Race recording is B11's.
*For B11 (traced-to-code for the API, lead-only for its use).* Each seat keeps its own controller.
To open a window: set every seat's `RestartCount = StartCount.Restart` and call
`BeginStartCount(StartCount.Opening(2f))` on every seat in the same step; they stay in lockstep on
the shared sim dt. `GameSession.RestartRace` calls `Respawn()` per seat today; B11 adds
`BeginStartCount(StartCount.Opening(2f))` after each `Respawn()` there. `--det` must keep reaching
no count. C22's start on a host instant may need a `Begin` that takes the seconds already elapsed.

**Model recommendation.** Opus for the seat integration (the step ordering against the crash
branch, the respawn read inside the input read, and the bit-equal hand-over); Sonnet for the docs
once the rules are fixed.

**Verify.** Built: `CSVM.Tests/StartCountTests.cs`: the walk over C1/IA1's (18 m/s) and C1C/M01's
(58 m/s) own `PLAYER_INIT` spawns starts the spawn speed times the count's length back along the
nose, moves the spawn speed each step, and lands on the spawn pose bit for bit at GO; the restart
count beats at steps 1, 60 and 120 and goes at 180 at 60 Hz, GO standing about 60 steps; the
opening count beats READY, 3, 2, 1 at 1, 120, 180 and 240 and goes at 300; the controls are held
for every step through GO's (180) and released after; a cancelled count holds and shows nothing.
Engine suite `stunt-start-count` (`Testing/FlightInputHandoffSuites.cs`, weighted in
`analysis/engine-suite-weights.json`) on a real human rig at a 71.5 m/s spawn over C1/IA1: a
crashed tap after a zone returns with no count and the clock running; an in-flight hold restarts
behind the count, the aircraft on the nose line the spawn speed times the time left back; with
pitch, roll, throttle and respawn held through the count, no count step changes the clock, the
nose, the lever or the command, nothing leaves the walk line; the GO step's position, nose,
velocity and lever equal a plain respawn's bit for bit with the clock at 0; the step after, the
held stick acts and the clock reads one step (able-to-fail control); the respawn held over GO taps
nothing on release; a crashed hold runs the count too. Mutation-checked: the clock ticking through
the count, the input read inside the count, a setback from a fixed 18 m/s, `Rerun` without the
count and a tap that begins one each turned the suite red; the fixed-speed setback and GO one step
early each turned the units red. Not caught by either: dropping the early return after the input
read, since the suite's rig flies an empty world with nothing behind the spawn to strike. Every
pinned golden is unchanged, `c1-stunt-marker` (a `--det --stunt` solo run) among them, which is
the check that `--det` reaches no count. A non-`--det` probe (`--stunt --no-det --volume=0`) drew
2 at frame 75 with the clock at 0:00.0, logged three beats and GO, and drew GO fading with the
clock at 0:00.9. Hand-flown: the count's feel, the two sounds and the figure's size and colour.

**⚠ Traps.** From `BL-314`, verbatim in substance: (1) do not derive a setback from a speed; the
walk ends on the spawn pose whatever the speed is; (2) "the clock never stops" is stated twice in
`StuntMission.cs` on purpose, keep it and add the start-at-GO rule beside it; (3) do not simulate
the count and do not freeze the sim; (4) the instrument is a hand-flown sitting.

**Verified.** <pending orchestrator run>

# Wave B, the split screen race

## B11 ☑ The split screen race becomes a time attack: window, opening count, best-run ranking, FINAL RUN, boards

**Goal.** A multi-seat Stunt Flying run is a time attack: the shared opening count, a window of the
chosen length, per-pilot restarts through A1/A2, best-run ranking, a compact live leaderboard and
the full table on the held scoreboard key, FINAL RUN with its 2-minute cap after time up, and
`StuntRaceBoard` at the end with placings, callsigns, aircraft, best times and best-run splits,
offering Restart and Back.

**Evidence (confidence: lead-only for the design; traced for today's code).** Today `GameSession`
builds a `StuntRace` for a stunt mission with more than one rig (`Launch/GameSession.cs:1872-1873`),
each pilot on an independent copy of the course (`StuntMission.ForAnotherPlayer`,
`StuntMission.cs:190-210`); `StartGrid` spawns them abreast (`Session/Roster/StartGrid.cs:9-27`);
the race ends when every pilot finishes (`Flight/Modes/StuntRace.cs:63-67, 144-158`); `Rank` is
finishing order and `Standings()` already orders unfinished pilots by zones then time
(`StuntRace.cs:118-131, 148-150`); R on the board restarts the race (`StuntRace.cs:101-113`,
`GameSession.cs:2775-2786`).

**Approach.** *The bookkeeping (traced-to-code for the build, lead-only for the rules).*
`Flight/Modes/StuntRace.cs` is engine-free and fed by events, never by a controller:
`BeginOpening(seconds)`, `Advance(dt)`, `RunStarted(index)`, `ZoneCleared(index, zone, runTime)`,
`RunFinished(index, runTime)`, `RunAbandoned(index)` and `Restart()`. Its `Phase` runs Opening,
Open, FinalRun, Ended; `MayStartRun` is true in Open alone. A `Racer` keeps its best completed run
(`BestTime`, splits by course index), its furthest run (`MostZones`, `TimeToMostZones`, splits),
`InRun`, `RunsStarted`, `RunsFinished`, `Callsign` (the player tag in split screen) and `ScoreKey`.
`Standings()` is Decision 9's order: best time, then most zones, then time to them, then player
order. A completion with no counted run, a zone or finish after the end, and a start outside Open
count nothing. The window clock accumulates in a double, so a five-minute window ends on its step.
`Follow(index, run)` is the local feed: `StuntMission` gained `RunStarted` (its clock's first
`Tick`, `StuntMission.cs:308`) and `RunReset` (`:359`) beside `ZoneCompleted` and `RunCompleted`.
*For C22 (lead-only).* The host calls the same entry points from owner reports (run started, zone
split, run finished, run thrown away) and `Advance` on its own clock; a guest's leaderboard and
board read a `StuntRace` the host's messages feed. `Callsign` takes the network seat's name.
*The opening count and the window (traced-to-code).* `HumanRosterBindings.RestartCount`
(`StartCount.Restart`) and `FirstStartCount` (`StartCount.Opening(RaceReadySeconds)` in a race,
`Restart` solo) replace the solo-only field (`GameSession.cs:1931-1933`, `GameSession.cs:59`);
every seat begins its first count in `BuildPlayers` (`HumanFlightAdapter.cs:559`) and
`GameSession` calls `race.BeginOpening` with the same count's length in the same build
(`GameSession.cs:2075`). `StepHumanAircraft` advances the race after every seat
(`GameSession.cs:3102`), so the window opens on the seats' GO step and the step after it reads one
step on the window and on every run clock. A restart's own 3, 2, 1 is A2's `Rerun` path.
`RestartRace` (`GameSession.cs:2819`) resets every run and spawn and opens again on every seat.
*Restarts and FINAL RUN (traced-to-code; a small API addition in `Rerun` only).*
`FlightController.Rerun` returns without acting while `Race is { MayStartRun: false }`
(`FlightController.cs:1330`): no restart during the opening count, after time up or after the end.
A restart whose count straddles time up has its run start refused by the race, so that run never
counts, and the pane reads TIME UP. A run in progress at time up may finish inside
`StuntRace.FinalRunCap` (120 s); the race ends at the cap or as soon as no run is in progress, and
`RaceCompleted` wakes `StuntRaceBoard`, which halts the sim (`SimStep` and `PollResultsShortcuts`
read `Race.Ended`, `FlightController.cs:2112, 3900`).
*The board (traced-to-code, the look is the user's).* `UI/Screens/StuntRaceBoard.cs`: placing,
callsign, aircraft, best, gap to the winner and runs (completed/started); a pilot with no
completed run shows their furthest run's zones and the time to them; under it the best-run splits,
a row per zone in course order and a column per pilot. Its Restart is `GameSession.Rerun`: a new
window in place, or in Instant Action a rebuilt mission that opens on its own count. Its Exit row
is the standard "Exit to Menu", which `Launcher.ExitSession` (`Launcher.cs:3087`) takes back to the
screen the flight launched from, the Instant Action screen for a menu launch; whether it should
read "Back" is a look call.
*Instant Action (traced-to-code, lead-only for the choice).* A multi-seat Stunt Flying run from
the Instant Action screen is this race (B13's row sets its window). `EndConditionInputs.Race`
makes the director wire no zone-set win (`InstantActionDirector.cs:592`, `:827`), so the race's
board ends the run, and it leaves a race seat's live respawn on (`:658`): Decision 1's restart at
will. Lives still count a crash, and a race whose every pilot runs out of lives still loses the
mission onto the wrap-up.
*The start (traced-to-code).* `Session/Roster/SharedSpawnStarts.cs` puts every pilot on player 1's
spawn and start state (`GameSession.cs:1927`); co-op keeps `StartGrid`. It relies on B12's
no-collision rule.
*The live leaderboard's placement (traced-to-code for the geometry, TUNE for the look).* The line
under the run-status line, top centre (`StuntRunHud.cs:97`, `RefRaceLineGap` 8 at `:32`):
`StuntRace.LeaderboardLine` reads `TIME 4:12   2nd/2   LEADER P1 0:45.2   +1.3`, FINAL RUN and
its countdown in place of TIME after time up, `NO TIME` for a pilot with no completed run, the
leader's own gap being its lead over second. The band is clear: the compass tape ends at 75 of
1440 (`CompassTape.cs:15`), the status line stands at 100 (`StuntRunHud.cs:31`) in the Readout
rung, 8 of 600 frame units, 19.2 at 1440 (`ChromeType.cs:24`), so this line's bottom is near 154;
the message slots start a fifth of the pane down (`HudMessages.cs:111, 316`), 288 full screen.
In a 2P stacked or 3P/4P grid pane the HUD scale is damped by the square root of the pane share,
so the line ends near 0.076 of the window height against slot 0's 0.1; side by side matches full
screen. The banners stand at 0.26 and 0.34 of the pane and the count at 0.5 (`StuntRunHud.cs:105,
111, 127`), the gauges at the bottom corners (`GaugeCluster.cs:490`), and in a race pane B12
builds no weapon gauges at all. The zone marker is `TargetHud`'s and can pass anywhere, as it
already does behind the status line. A 50-character line at the damped Readout size spans about
a quarter of a 4P pane's width.
*The held scoreboard key (premise disproven, traced-to-code).* No scoreboard key exists in
Dogfight or anywhere in flight: `InputAction` has no scores member (`Bindings/InputAction.cs`), and
`docs/org/multiplayer-messages.md` says the remake binds none. The original's own is Display
Scores (Multiplayer Only), command `0x23`, default `Tab` (`docs/org/input.md`, its table).
⚠ Not built: the full table on a held key needs an appended `InputAction` (default `Tab` per the
original), both controls screens and a pad binding, a decision for the user that Dogfight would
share. The compact line and the end board carry the race meanwhile.
*Bests, Decision 10b (traced-to-code).* `ScoreStore` is one file per machine keyed
`chapter/mission/plane` with no profile or seat dimension (`ScoreStore.cs:38, 57`). Each racer
carries the solo key for its own aircraft (`HumanFlightAdapter.cs:463`), and `BestImproved` records
it through `GameSession.RecordRaceBest` (`GameSession.cs:1124`) into the session's store, a
throwaway under `--det`, a scripted run or `--debug-scoreboard`. Two seats on one aircraft share
that key's best, as two players on one machine always have; no seat is skipped.
*`--det` (traced-to-code).* Under `--det` no seat carries a count, `BeginOpening(0)` opens the
window at once and the plain `SpawnPicker` places the field. No pinned golden flies a race (the
manifest has no `--stunt` with `--players`), so every hash holds. `--debug-scoreboard` builds a
zero window, so its staggered forced finishes run as the final run and the last wakes the board.

**Model recommendation.** Opus for the race bookkeeping and the session wiring (the step order
between the seats and the window, the Instant Action ending's hand-over, the refusal in `Rerun`);
a mid-tier model for the board and the HUD text once the rules hold. The look is the user's.

**Verify.** Built: `CSVM.Tests/StuntRaceTests.cs` (13): the window opens on step 300 of a 5 s
opening and reads one step after it; finishers rank by best, not finish order; pilots with none
rank by most zones in any run, then time to them, an earlier run's furthest counting; a restart
keeps the best and only a faster run replaces it, splits and all; no run starts in the opening or
after time up; a run in progress at time up finishes inside the cap and counts, and ends the race;
one still going at the cap does not count; time up with nobody running ends at once; `Restart`;
the leaderboard line; `Follow` off a run's clock, zones, finish and reset; the formats. Engine
suite `stunt-race-time-attack` (`Testing/StuntRaceSuites.cs`, weighted): two seats built through
the session's roster over C1/IA1 race a 20 s window; their opening counts hand over together on
step 300 with the window opening on that step and both clocks and the window one step on after it;
P1's held restart runs its own count while P2 flies on, keeps its first best until a faster run
replaces it with that run's splits; P2 restarts late, is in a run at time up and finishes it as the
final run, faster than P1; P1's hold in the final run is refused; the race ends on P2's finish
with the board up, the sim halted, the rows ranking P2 then P1 and each pilot's best-run splits.
Mutation-checked, each red and restored: finishers ranked slowest first (unit and suite),
non-finishers by the slower time (unit), a slower run replacing the best (unit), the cap doubled
(unit), runs allowed in the final run (unit and suite), the window opening a step early (unit and
suite), `Rerun` ignoring the race (suite), a reset not abandoning the run (unit), the run clock
not reporting its start (unit and suite). Captures (temporary manifest entries through the golden
stage, manifest restored byte-identical): the window line, the final run and the end board.
Hand-flown: a two-seat split screen sitting for the opening count, the restarts, the final run
and the board; the leaderboard line's place and wording; whether the Exit row should read Back.

**⚠ Traps.** Do not let the race read a controller or a run in its ranking or window: C22 feeds it
from the wire, and `Follow` is the only local wiring. The window opens on the seats' GO step only
because `BeginOpening` is called in the same build as the counts and `Advance` runs after every
seat in `StepHumanAircraft`; moving either breaks the lockstep, which the unit and the suite catch.
`Rerun`'s refusal is the one place a seat reads the window. The shared spawn needs B12's
no-collision rule; without it the field rams at GO. A multi-seat Instant Action stunt run no longer
ends on the zone sets, so `InstantActionRuntime.ZoneSetsFlown` decides a solo run alone. A race
seat in Instant Action may restart in flight, a free repair the solo run refuses; with weapons off
(B12) it restores nothing a race uses.

**Verified.** <pending orchestrator run>

## B12 ☑ Race presence: no collisions, weapons off, ghosts when near

**Goal.** In any race, aircraft never collide with each other, fire controls do nothing and the
weapon readouts leave the HUD, ground targets on the course world stay inert, and another pilot
fades from solid beyond 80 m to a ghost inside 40 m, keeps its callsign label and is never a target.

**Evidence (confidence: traced-to-code for today's paths, TUNE for the numbers and the look).**
Aircraft collide through the airframe sweep (`FlightController.cs:2214`, `SweepAirframe` at 4406,
mask `CollisionLayers.WorldAndAircraft`), which strikes another aircraft's `AircraftBody` on the
aircraft layer; `PerformContact` (4293) then damages both and arms both grace windows (4300). A
body leaves that layer only through `SetHittable`, written by `ApplyPresence` (3080) and the two
death paths. A plane with no loadout builds no `FireControl` and binds no weapon gauge
(`FlightController._Ready`), and the assembler builds the pipper only over a loadout. The C1, C1B,
C2, C4 and C5 IA1 stunt worlds hold no AI aircraft, vessel or generator in a `--stunt --players=2`
run; their only hostile content is the world AA emplacements, of which C4 has 11 and C5 5 awake
and alive at load (the `turrets:` census line); a dormant emplacement takes no tick
(`TurretController.cs:514`). The pilots are hostile to those guns' default team in a plain
splitscreen race. The callsign label is #116's marker name line.

**Approach.** *The session flag (traced-to-code).* `FlightWorldBindings.Racing`
(`Session/Roster/FlightRosterInputs.cs:169`) is the one session-level race flag: `GameSession` sets
it from `race != null` (`Launch/GameSession.cs:1983`, the `StuntRace` built at 1875), never from the
pane count, so C21's network race takes every rule below the moment its session builds a
`StuntRace`. B11 reads nothing new; C21 needs only to build the race. Both assemblers stamp it on
every aircraft as `FlightController.Racing` (`FlightController.cs:152`; `HumanFlightAdapter.cs:189`,
`AiFlightAssembler.cs:159`), AI wingmen included.
*No ram (traced-to-code).* `ApplyPresence` writes `SetHittable(InPlay && !Racing)`
(`FlightController.cs:3080`): in a race no body sits on the aircraft layer for the whole session, so
no sweep, centre ray, AI probe or round finds another aircraft, and the resolver, the grace window
and the damage pair are never reached. One rule covers both sides of a pair; the contact code is
untouched, so a non-race session is unchanged.
*Weapons off (traced-to-code).* `HumanFlightAdapter` binds no loadout, no pylon ordnance and no
carried turret under the flag (`HumanFlightAdapter.cs:254, 324`). With no loadout there is no fire
control, so a held trigger does nothing, and the gun gauge, the missile gauge and the pipper are
never built; the pylon rockets leave the model too. The ground stays inert: `GameSession` puts every
world emplacement to sleep for the session (`TurretEmplacementRuntime.SleepAll`, 97; called at
`GameSession.cs:2270`, logged with the count that was awake). A pilot's collision with a
`WeaponOrCollideHit` destructible still shatters it, as solo does, because a course may thread one.
*Target cycle and spyglass (traced-to-code).* `TargetPool.Offer` drops a human aircraft carrying the
flag (`TargetPool.cs:300`), so no cycle, `--target=` or nearest pick reaches another race pilot, and
the spyglass, which shows only the selection, never does. An AI aircraft in a race session stays
selectable.
*The label (traced-to-code for the name, lead-only for the drawing).* `TargetHud.RaceMarks`
(`TargetHud.cs:61`, drawn at 595), on for a race pane, labels every other live race pilot with
`TargetPool.AircraftDisplayName`, the #116 name line (mode tag, then callsign, then plane type),
through the same on-screen tag and edge arrow the hostile marker uses, in the friendly green.
*The ghost (traced-to-code for the path, TUNE for the numbers).* Aircraft surfaces are the
`SceneBuilder` bias shader's shaded arms in both presentations (Original's per-vertex sun arm and
Enhanced's lit arm). `SceneBuilder.RaceGhostShader` (260, key bit 262144 at 1883) adds one vertex
line and one fragment discard (2047) under a key bit of its own, set only on a race session's human
airframe builder (`PlaneBuilder` `raceGhost`, the interior builder never), so every other build keeps
its exact shader text. `Mech3/RaceGhost.cs` holds the law: alpha `GhostAlpha` 0.35 within
`GhostWithinM` 40 m, 1 beyond `SolidBeyondM` 80 m, linear between; the shader
(`shaders/csky_race_ghost.gdshaderinc`) evaluates it per mesh instance from `CAMERA_POSITION_WORLD`,
the drawing camera, and dithers through the clutter fade's ordered 4x4 keep in the opaque pass. A
per-pane fade is therefore possible in one shared scene: each pane camera draws with its own
position. The per-instance `csky_ghost` (appended last to the ordered instance-uniform block) is
written once at assembly (`HumanFlightAdapter.cs:164`) and names the owner's first-person layer;
the owner's pane and spyglass drop that layer (`SplitScreen.OwnViewCullMask`, 168, read in the
shader as `CAMERA_VISIBLE_LAYERS`), so the owner always sees its own aircraft solid, and its Danger
Zone photograph, whose mask adds the layer back (`DangerZonePhotograph.cs:183`), is exempted by the
armed `csky_photo_eye`. Batching holds (one material per surface, the value per instance) and no
per-frame write exists. These are the viewer seams' kind of rule: a draw rule that says "the
camera", answered by the camera drawing.

**Model recommendation.** Opus for the design read (the layer and photograph rules and the shader
key took the whole render path traced), Sonnet for a TUNE change of the three constants.

**Verify.** Built: engine suite `race-presence` (`Testing/RacePresenceSuites.cs`, weighted in
`analysis/engine-suite-weights.json`) on two seats built through `FlightRoster` with the flag set
and, as the control, without it: a seat flown nose on through a parked one passes 0.23 m from its
origin with no health lost, no crash and both bodies off the aircraft layer, while the control rams
(both ledgers lose health, stopped short); a race seat holding both triggers is unarmed (no
loadout, no weapon gauge) and puts 0 rounds in the pool while the control fires; the other race
pilot is on no cycle yet labelled, while the control offers it on the Enemy cycle; the race build
stamps 105 instances and compiles the ghost into 75 shaders (the shader's uniform list parses),
the control stamps none and carries the ghost in none. Engine suite `race-ghost-fade`: alpha 0.35
at 30 m, 0.675 at 60 m, 1 at 100 m, and the shader line carries the same constants.
Mutation-checked, each red then restored: bodies left on the layer (race rams, 20+20 health lost),
the race loadout bound (9 rounds), the cycle skip removed (enemy 1), the labels off, the stamp
removed (0 instances), the law's denominator wrong (0.513 at 60 m), the shader's floor literal
typed apart from the law, and a shader compile error (no uniform list, 16 engine errors). The
session wiring was read from a live `--chapter=C1 --stunt --players=2` probe log: `weapons: none, a
race pilot flies unarmed` and `turrets: all 74 emplacement(s) dormant for the stunt race (15 were
awake)`. Captures of the ghost at about 30, 60 and 100 m in both presentations are owed to the
user's eye. No pinned golden flies a race, and the ghost reaches no shader outside a race.
Hand-flown: the 80/40 m band and the ghost's look in both presentations, at a two-seat sitting.

**⚠ Traps.** The 80 m, 40 m and 0.35 floor are TUNE, judged at the controls; change them in
`RaceGhost` alone, never as literals in the shader. Ghosting must not change a non-race session's
rendering, which is why it is a shader key bit and not a uniform branch in every aircraft shader.
The owner test is a first-person layer bit, and that band is four layers wide
(`SplitScreen.FirstPersonLayer` wraps), so a network race past four seats would show a seat that
shares a bit as solid; C21 must widen the band or cap the field before then. Under Enhanced the sun's
shadow pass draws from the light's own camera, so the ghost's shadow follows that camera's
distance, solid in practice; Original's ground-shadow quad stays solid under a ghost, and a crash's
wreck pieces are not stamped. The plain splitscreen race has no callsign, so the label reads the
plane type; whether a local seat reads `P2` instead is #116's separate decision. Do not move the ram
rule into the contact resolver: the body off the layer is what keeps every query, the AI probe and
the AGL ray included, from finding a ghost.

**Verified.** <pending orchestrator run>

## B13 ☑ The Instant Action time row for a multi-seat Stunt Flying run

**Goal.** With Stunt Flying chosen and more than one seat joined, the Original Instant Action
screen shows a time row (3, 5, 10, 15 minutes, default 5) that sets the race window; a solo run
shows no row.

**Evidence (confidence: traced-to-code).** The screen's focus order is its row list order within a
`Column` (`OriginalShell.StepWithinColumn`, `OriginalShell.cs:1288`), a sideways step crosses
columns by ordinal (`OrdinalInColumn`, `:1350`, which counts disabled rows), and a focus on a dead
row falls back to the page's first live row (`EnsureFocus`, `:1585`), the contents window on the
far page. The shipped `[@InstantAction@]` section's setup lines are 210, 235, 280, 305, 350, 375
with 18-pixel boxes (`extracted/rof/ASSETS/LAYOUT.CSV`), so it leaves two clear lines: one between
Wingmen and Mission, which the remake-only Lives box takes, and one between Environment and the
enemy block. The Lives box is the precedent for a remake-only row: a hardcoded label string, the
mission dropdown's left edge and height, its line read off the gaps (INSTR-77). The Instant Action
def already carries the remake's other invented rule, `Lives`, from the wizard to the session
(`InstantAction.BuildFromWizard`, `SessionSpec.FromMenu`).

**Approach.** Placement (traced-to-code): the box takes the setup stack's second clear line,
`ClearLine(screen, height, 1)` (`OriginalInstantActionScreen.cs:893-910`), which on the shipped
layout is Y 327.5 under Environment and over the enemy block, at the mission dropdown's X (525),
110 wide. Focus order (traced-to-code): `AddRaceTime` inserts the row before the first setup row
standing below its line (`:845-862`), so the walk reaches it between Environment and the enemy
count, the order the page reads in. That leaves #25 no worse: no row's order departs from its
line. Hidden (solo, or another mission type), the row stays in the list with `Visible` and
`Enabled` false and `Column` -1 (`:861-862`), so no other row's index or column ordinal moves when
a seat joins or leaves; the precedent for an unseen, unhit row is `OriginalWidgets.PageRows`. A
focus left on the hidden row, or inside its list when a seat leaves under it, lifts to the nearest
live box above (`LiftFocusOffHiddenRaceTime`, `:679, 695`); a hidden box answers no dropdown
(`:1079`). Label (traced-to-code, the look is the user's): "Race Time:" in the title column and
heading ink (`:149, 1321`), items "3 minutes" to "15 minutes", the closed box drawn by the same
`ComposeRow` path and arrow strip (art 4 of the mission dropdown) as Lives. Visibility is
`InstantActionFeature.OffersRaceWindow(seats)`, stunt flying with more than one seat
(`InstantActionFeature.cs:343`), read on every build. The pick is
`InstantActionFeature.RaceWindowMinutes` (default 5, left alone by a preset, reset by `Discard`),
carried on `InstantActionDef.RaceWindowMinutes` (`InstantAction.cs:409`) and resolved into
`SessionSpec.StuntRaceMinutes` (`SessionSpec.cs:396, 1856`), which keeps 5 on every launch that
builds no def. B11 reads `_spec.StuntRaceMinutes` where `GameSession` builds the `StuntRace`
(`Launch/GameSession.cs:1874-1875`). A screenshot aid, `--menu=instant-action:race-time` with
`--debug-join=1 --presentation=original`, poses it (`OriginalInstantActionScreen.PoseRaceTime`).

**Model recommendation.** A mid-tier model suffices: the work is one menu module, a feature field
and a spec field, with the Lives box as the pattern to copy. The look needs the user's eyes.

**Verify.** Units: `OriginalInstantActionTests.TheRaceTimeBoxShowsOnlyForAStuntRunWithASecondSeatAndKeepsItsPlaceInTheWalk`
and `ASeatLeavingUnderTheOpenRaceTimeListClosesItAndTheRaceWindowRidesTheLaunch` (hidden solo, shown
at the same index with two seats, sideways step and list pick, hidden under another type, the focus
lift, the list closing on a leave, the def and `SessionSpec.FromMenu` carrying the pick, 5 without a
def), `InstantActionFeatureTests.TheRaceWindowIsOfferedToAMultiSeatStuntRunAndRidesTheDefUntilDiscarded`.
Engine: `menu-original-instant-action` over the install's layout (Y 327.5, no overlap, walk order
Environment, Race Time, enemy count, sideways step, hidden under Zeppelin Run, the launch's spec at
10 minutes). Mutation-checked: showing it solo, appending it at the column's end, dropping the
spec field, dropping the focus lift and reusing the Lives line each turned a test red. Every pinned
golden unchanged (no pinned shot shows the Instant Action screen).

**⚠ Traps.** Do not drop the hidden row from the list: the shell's focus is an index, so a row that
comes and goes moves the focus of every row under it when a seat joins. Do not leave the hidden
row in column 1: `OrdinalInColumn` counts disabled rows, so a solo screen's column crossings would
shift by one. Do not write the box's Y: the unit fixture has one clear line, the shipped layout
two, and a written Y lands on an authored box in one of them (INSTR-77). The window rides the def
on every Instant Action launch, solo included; only a multi-seat stunt run may read it.

**Verified.** <pending orchestrator run>

# Wave C, the network race

## C21 ☐ Stunt Race in the Original lobby, flying the chapter's stunt course over the network

**Goal.** The Original Multiplayer Lobby's Type box offers Stunt Race. With it chosen the
Environment box greys Above the Clouds, the Mission Options grey everything but the time limit
(defaulting to 5 minutes), and the launched match loads the chosen chapter's Instant Action stunt
world and course on every machine.

**Evidence (confidence: lead-only for the design; traced for today's code).** The lobby's types and
their maps (`UI/Menu/DogfightLobby.cs:10-21`); its seven environments map to C1C, C3, C2, C5, C1,
C1B, C4 (`DogfightLobby.cs:41, 88-96`); `DogfightOptionsMessage` carries the mission options
(`Net/NetDogfightMessages.cs:6-32`); a multiplayer folder ships no `ia.json`, so `StuntMission.Load`
returns null there (`StuntMission.cs:126-133`, `GameSession.cs:1865-1871`); C1C and C2B have no
course (`UI/Menu/MenuChapters.cs:22-30`).

**Approach.** First, make the mission type one field: `SessionSpec` carries it as the two bools
`CaptureTheFlag` and `ZeppelinVsZeppelin`, checked across 14 files, while `DogfightMissionType`
lives in `UI/Menu/DogfightLobby.cs` and the lobby compares raw bytes against it. Move the enum
beside `SessionSpec`, make it the spec's one mission field and remove the bools, so Stunt Race
lands as an enum value rather than a third bool. <TODO: the new `DogfightMissionType` value and
its wire encoding (a wire-version consideration); how a network session loads `<chapter>/IA1` with its `dzones` and what of the IA1
mission's own content (opposition, objectives) it suppresses; the lobby text strings (remake-only,
no langui id)>.

**Model recommendation.** <TODO>

**Verify.** <TODO: a menu suite for the Type, the greyed environment and options; a loopback net
suite launching a two-seat Stunt Race and asserting both machines load the same course>.

**⚠ Traps.** <TODO>

## C22 ☐ Owner-timed runs on the wire; the host's window, leaderboard and match end

**Goal.** Each machine times its own pilot's runs and reports zone splits, finishes and restarts to
the host; the host keeps the window clock, the leaderboard of best runs and the FINAL RUN stretch,
and sends the leaderboard and the match state to every guest, whose live leaderboard and boards read
them.

**Evidence (confidence: lead-only for the design; traced for the authority model).** Aircraft state
and deaths are owner-authoritative (`Net/NetMessages.cs:447, 691`); `MatchStateMessage` is written
only by the host (`NetMessages.cs:1000-1014`) and sent on `MatchStateCadence`'s 1 Hz tick, with the
match end sent at once (`Net/MatchStateCadence.cs:10-27`, `Session/World/VersusDirector.cs:247,
685, 690`); `VersusMatch` takes a guest's clock and ending from the host (`Flight/Modes/VersusMatch.cs:78-81`).

**Approach.** <TODO: the run-report message (reliable, per split or per finish), the leaderboard
message, and whether the race reuses `MatchStateMessage` for the window clock; the opening count's
start on every machine from one host instant>.

**Model recommendation.** <TODO>

**Verify.** <TODO: serialisation units for the new messages; a loopback suite with one lossy cell
where a guest's finish reaches the host's leaderboard and the guest's board; the FINAL RUN cap on
the host>. Hand-flown: a two-machine sitting (the Deck can host, per `analysis/net-real-link/`).

**⚠ Traps.** <TODO>

## C23 ☐ Network race end: Restart and Lobby, guests waiting, pilots leaving

**Goal.** The host's race board offers Restart (a fresh window, opening count, same course and
rules) and Lobby (everyone back to the lobby); a guest's board says it is waiting for the host and
offers Leave. A pilot who leaves keeps their best on the boards marked "left"; the host leaving ends
the race; a race left with one pilot runs on to the window's end.

**Evidence (confidence: lead-only; traced for today's Dogfight end).** A completed match plus R
calls `RestartMatch` (`FlightController.cs:3695-3698`); a guest's Dogfight board offers no Restart
(`docs/controls.md`, the `Backspace` row).

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO: a loopback suite: host Restart reopens the window on both machines; Lobby returns
both; a guest leaving mid-window keeps its row marked "left">.

**⚠ Traps.** Deathmatch's "fewer than two pilots left" ending (`VersusMatch.cs:17-27`) must not
apply to a race (Decision 16).
