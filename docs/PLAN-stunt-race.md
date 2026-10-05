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
14. ☑ A held Display Scores key on Tab, drawn in the original's look
15. ☑ The race board's Back row, and an Original-looking race board
16. ☑ No lives in a race

### Wave C, the network race

21. ☑ Stunt Race in the Original lobby, flying the chapter's stunt course over the network
22. ☑ Owner-timed runs on the wire; the host's window, leaderboard and match end
23. ☑ Network race end: Restart and Lobby, guests waiting, pilots leaving

## Dependency and parallelism notes

The waves are a chain: B builds on A's run rules and count, C carries B's race over the wire. A1
and A2 both edit `StuntMission.cs` and the respawn path in `FlightController.cs`: run them in
order, not in parallel. B11 and B12 are independent in files (race bookkeeping and boards against
collision, weapons and rendering) and may run side by side with that boundary. B13 is UI only and
can run beside either. B14 to B16 are the user's rulings on B11's report: B15 runs before B14 so the
scores display can reuse its Original race table, and B16 is independent in files. C21 blocks C22 and C23; C22 and C23 both edit the race's network messages and
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

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

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

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

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
will. A race spends no lives (B16).
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

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

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
its exact shader text. `Mech3/RaceGhost.cs` holds the law: alpha `GhostAlpha` 0.35 (`EnhancedGhostAlpha` 0.55 under Enhanced, the user's ruling: a 0.35 ghost all but vanishes there) within
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
at 30 m, 0.675 at 60 m, 1 at 100 m (0.55 at 30 m under Enhanced), and each presentation's shader
line carries the same constants.
Mutation-checked, each red then restored: bodies left on the layer (race rams, 20+20 health lost),
the race loadout bound (9 rounds), the cycle skip removed (enemy 1), the labels off, the stamp
removed (0 instances), the law's denominator wrong (0.513 at 60 m), the shader's floor literal
typed apart from the law, and a shader compile error (no uniform list, 16 engine errors). The
session wiring was read from a live `--chapter=C1 --stunt --players=2` probe log: `weapons: none, a
race pilot flies unarmed` and `turrets: all 74 emplacement(s) dormant for the stunt race (15 were
awake)`. Captures of the ghost at about 30, 60 and 100 m in both presentations are owed to the
user's eye. No pinned golden flies a race, and the ghost reaches no shader outside a race.
Hand-flown: the 80/40 m band and the ghost's look in both presentations, at a two-seat sitting.

**⚠ Traps.** The 80 m, 40 m and the 0.35 / 0.55 floors are TUNE, judged at the controls; change them in
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

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

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

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

## B14 ☑ A held Display Scores key on Tab, drawn in the original's look

**Goal.** A held Display Scores action, default `Tab` plus a pad binding, on both controls screens,
shows the full standings while held: the race table (place, pilot, aircraft, best, gap, runs) in a
stunt race and the match scores in a Dogfight, local split screen and network alike. Under the
Original presentation it is drawn in the original game's own scores look.

**Evidence (confidence: traced for the original's command; lead-only for its look).** B11 found no
held scoreboard key in the remake: `InputAction` has no scores member, and
`docs/org/multiplayer-messages.md` (the Display Scores paragraph) says the remake binds none in
flight. The original binds "Display Scores (Multiplayer Only)", command `0x23`, default `Tab`
(`docs/org/input.md`, `MSG_CMD_DISPLAY_SCORES` in `docs/formats/strings.md`); its handler is
`FUN_00489320`, which also hides the chat lines. The user's ruling: add it, shared with Dogfight, and
make the display look like the original's.

**Approach.** *The decode (traced-to-code, `docs/org/multiplayer-scoring.md` "The in-flight
scores", `docs/org/input.md` "Dispatch").* `FUN_00489320` does two things: it hides the chat panel
(`FUN_004a8510` on `0x0071d8a8`) and hands the score list `0x0071c13c` to `FUN_00456400`, which
runs `FUN_004565d0` on the HUD object `0x00654234`. That draws no art and does not reuse the lobby's
Game Scores page: it is 18 HUD text lines in the `hudNetPlay` font (Courier New, height -12, width
8, 255/250/66, shadowed, weight 600), at x 50 and y 30 to 200 every 10 in the 640 by 480 HUD frame,
with an "F" column at x 40 for a Capture the Flag carrier (team 1 170/170/0, team 2 170/0/0, the
colour word order inferred from the `scorecolors` console command). The lines come from
`FUN_0046e310(21, 7)`: a header `Left(MSG_MPHUD_PLAYER, 21) + " " + Left(MSG_MPHUD_SCORE, 7)`, then
per pilot `Left(name + 24 spaces, 21) + " " + Left(score + 13 spaces, 7)` by score; a team match
puts `"%s (Team Score: %d)"` over each team's pilots, indented by one. Each line is shown with
`FUN_005c55f0(4.0)`, a four-second timer, and the dispatch calls a handler on the press only, so the
original's Display Scores is a tap that shows the table for four seconds (expiry hiding the lines is
inferred from the chat panel's identical timer field). Every score message `0x13` raises it too.
*The action (traced-to-code).* `InputAction.DisplayScores` is appended after `LookUpRightRear`
(92, the old members keep 0 to 91); saved keymaps name actions, so no saved row moves, and a file
that already put `Tab` on another action keeps it (the saved row's claim). Defaults: `Tab`, the
original's key, and pad Back: every other pad button is a flight action's (B, A, X, Y, the d-pad,
both shoulders, both stick clicks, Start, Misc1) or the throttle triggers, while Back holds no flight
action (the chase view, the one action ever given it, ships unbound); it is also the conventional
held-scoreboard button. Label: the
original's own "Display Scores (Multiplayer Only)", kept because the remake's split screen is
multiplayer too; it fits the Original page's action column (capture). The Original KEYS AND BUTTONS
page lists it on the Other tab between Pause/Quit/Objectives and Chat to Everyone, as the original's
page does; the Built-in controls screen lists every flight action already.
*Held, per pane (lead-only, the user's ruling).* The original's four-second tap was put to the user, kept the hold. `FlightController.ScoresShown`
is the held action in a race or a Dogfight with no board, pause sheet or photo mode over the flight
(`--debug-scores` holds it, documented in `docs/cli.md`). `GameSession.AttachScores` gives every local
pane a `UI/Overlays/ScoresOverlay.cs` on a layer over its HUD wherever a race or a Dogfight exists,
so solo, campaign and co-op get none. Split screen: the table stands in the holding seat's pane, not
the whole window, because the original's table is one screen's HUD and a held control is one seat's;
a whole-window table would cover the other pilots' flight in the middle of a race. Network: the local
pane, seats named by network callsign. `ChatPanel.ForPane` (now every pane's chat) steps aside while
its pane's seat holds the action; the original instead hides it at once on the press until the next
line, which is not reproduced. By the user's ruling the pane's top-centre status lines step aside
the same way while the seat holds (the race's run status and leaderboard lines, the Dogfight's match
line, `StatusHiddenWhile` on `StuntRunHud` and `VersusHud`), in both looks, because the Original
text keeps the decoded position and overprints them in a 16:9 pane; the compass tape, banners,
count and crash prompt stay.
*The looks.* Original (traced-to-code for a Dogfight): `OriginalScoresText` builds the decoded
lines, the team branch and the flag included, and the overlay draws them three times over in the
HUD's 1440-line reference, every character on the 8-pixel cell so the columns hold under any stand-in
face, in Courier New weight 600 where installed. By the user's ruling each Dogfight pilot line also
carries kills (6 characters) and deaths after the original's two columns, headed "kills" and
"deaths": remake columns on the original's grid, borrowed like the race's; a team's line stays the
original's. A race (borrowed, remake text): the same grid with
place and callsign in the name column, then aircraft (12), best (10), gap (9) and runs, headed
"aircraft", "best", "gap", "runs" in the score header's lowercase. B15's `OriginalRaceTable` is not
used, since the in-flight display is not the lobby page. Built-in: `ScoresTable` in the results
boards' columns (race: place, pilot, aircraft, best, gap, runs; Dogfight: place, pilot, score, kills,
deaths, team lines first) as a chrome table centred in the pane, the Text and Note rungs.
*Not carried (decoded):* the four-second tap, the raise on every score update, `scorecolors` and the
per-entry colour, the join-order tie break.

**Model recommendation.** Opus for a decode of this kind (the dispatch's press rule and the
CString formatting were read off the disassembly where the decompiler lost them); a mid-tier model
for a change of the race columns, the Built-in table's placement or the label.

**Verify.** Units: `CSVM.Tests/DisplayScoresTests.cs` (12): the action is 92 after `LookUpRightRear`
91 and the last member; it ships on `Tab` and pad Back as a flight action, each control owned by it
alone, captioned with the original's string; a keymap saved before it reads every other flight row
back as saved (a rebind included) and the new action at its default; a saved row on `Tab` keeps the
key and the action keeps Back; the Other tab's order; a free-for-all's lines (header, a cut long
name, a negative score, no flag, kills and deaths after the score); a team match's lines (team
totals, one-character indent, the carrier's flag, the members' kills and deaths); the 18-line cap; a race's rows and lines in both looks; the Dogfight table's team
rows first; a source with no mode shows nothing; the header words from the table and the fallback.
Also the shipped-table rows in `DefaultBindingsTests` and `FlightBindingMappingTests` (`Tab`, Back).
Engine suites (weighted): `stunt-race-display-scores` and `dogfight-display-scores`
(`Testing/DisplayScoresSuites.cs`), two seats through the roster, each in its own pane with
`ChatPanel.ForPane` up and both looks attached: nothing before a hold; the holder's pane alone shows
the standings in both looks while the other pane does not; the holder's chat and top status lines
step aside and the other's stay; the release takes them back; a race seat with no race shows nothing
and keeps its status lines. The status lines' step-aside was mutation-checked by unwiring
`StatusHiddenWhile` in each mode (each suite red on that check alone).
Mutation-checked, each red and restored: name column 20, no member indent, 17 lines, gap column 8,
team lines unmarked, no pad Back, the tab order moved, a member filed before the action, kills 5, deaths dropped, the score in the kills column, the header without kills and deaths
(units); deaths dropped (Dogfight suite);
the held action ignored (both suites), the chat never hidden (both suites), no mode gate (race
suite). Captures through the golden stage with temporary manifest entries (`--debug-scores`, two
seats, with and without `--force-builtin`, a Dogfight with `--debug-scoreboard`'s kill, and
`--menu=keys:other`); the manifest was restored byte-identical and every pinned golden held.
Hand-flown: the held table at a two-seat split screen sitting in both presentations, the Original
text's size and line pitch at the pane's scale, the Built-in table's centred place (it covers the
crash prompt when a crashed seat holds it), and Back on a pad.

**⚠ Traps.** The original's in-flight scores are HUD text, not the lobby's Game Scores page; do not
dress them in B15's page art. Its Display Scores is a four-second tap; held is the user's ruling,
so a report of "the table vanishes after four seconds" is the original's behaviour, not a defect.
Do not draw the Original lines as whole strings in a proportional face: the columns are character
cells. The table is per pane on purpose; a whole-window version must decide whose hold raises it.
A team-less seat in a team match has no counterpart in the original and follows the teams as a
plain line. Kills and deaths under Original are the remake's columns, not a decode: the original's
lines carry name and score alone. C21's network race reaches the overlay through the `StuntRace` it builds; its seats'
callsigns come from `NetSeat.Callsign`.

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

## B15 ☑ The race board's Back row, and an Original-looking race board

**Goal.** The race board's exit row reads "Back" (Decision 17), and under the Original presentation
the end-of-race board is drawn in the original game's look rather than the remake's chrome board.

**Evidence (confidence: traced-to-data for the borrowed screen, lead-only for the borrowing).**
B11's `StuntRaceBoard` is a chrome `ResultsBoard` whose exit row was the standard "Exit to Menu"
(`Launcher.ExitSession` returns to the Instant Action screen). Two original results screens exist.
The Instant Action wrap-up page (`[@IA_WrapUp@]`, `UI/Menu/Original/OriginalWrapupScreen.cs`,
`docs/formats/instant-action/wrap-up.md`) is one pilot's four title/value pairs on a magazine
spread, a menu page shown after the flight; it has no table. The multiplayer lobby's Game Scores tab
(`MULTIPLAYERLOBBY_STATS.SCRIPT`, page art `MP_LOBBY_STATSCREEN.PNG` at (314, 26)) is the original's
multiplayer results table: five headed columns (10542 to 10546, TREB13B), ten rows at a 20-pixel
pitch from (+24, +69), the name column 154 wide and left-justified, four figure columns 62, 61, 60
and 57 wide and centred (TREB10B, faces 10573 and 10574), a grey row for a flagged pilot and a
scroll bar past ten. It is where a finished Dogfight lands (`OriginalLobbyScreen.Land`), so it is
the original's end-of-match screen. `FUN_00489320` (Display Scores) was not decompiled here: the
Ghidra session had no program loaded, and that decode is B14's.

**Approach.** *The borrowed screen (lead-only, the look is the user's).* The race is a multi-pilot
ranking, which is the Game Scores page's shape and not the wrap-up's, so the board is the lobby on
Game Scores with the race in it: `MP_LOBBY_BACKGROUND.JPG`, the scores page at its tab corner with
"Game Scores" (10507) on its picked tab and the other three tabs unlabelled, "STUNT RACE RESULTS" in
the lobby's title box (10046's face), and the lobby's three plaque slots.
*The table, reusable (traced-to-data).* `UI/Menu/Original/OriginalRaceTable.cs`:
`Rows(standings, zoneCount)` and `Compose(rows, pageX, pageY, strings, layers)` draw the page and
its rows at the script's positions and faces. Six race columns into five page columns: place and
callsign at the name column's left and the aircraft right-aligned in it, then best, gap and runs;
the fifth column stays empty. A 62-pixel figure column clips "Bloodhawk" (measured in the first
capture), which is why the aircraft shares the wide column. The columns' words are the Built-in
board's (`StuntRace.BestText`, `GapText`), and the Built-in board heads its gap column "GAP" as this
page heads it "Gap".
*The screen (lead-only).* `UI/Menu/Original/OriginalRaceResults.cs`: the zone key ("1  Passenger
Hangar") down the player list's lines from (34, 83), two columns past eleven zones (the longest
shipped course's 17 take nine and eight); the best-run splits in the chat pane, zone numbers on its first line and a pilot
per 20-pixel line, columns no wider than the scores page's 62; the context line in the chat box;
Photo Mode, Restart and the exit on the Create Team, Send and Leave Game plaques, in the lobby's
strip frames and label tints. "Photo Mode" overflows the 74-pixel Send plaque, so it takes the
131-pixel Create Team one. The sheet is frozen at the race's end (`RaceResultsSheet`).
*The board (traced-to-code).* `UI/Menu/Original/OriginalRaceBoard.cs`, a whole-window Control on the
race board layer: wakes on `RaceCompleted`, raises `HaltReason.Ended`, retires once `Ended` clears
(Restart, R and pad Y), a fresh menu each end resting on Photo Mode, any arrow stepping the three
plaques, player 1's pointer on `BoardMenuPointer`'s rule. `SessionBoards.BuildRaceBoard` builds it
when the presentation is Original and the install has the scores page art and `ui_strings.json`,
else the chrome board; it now returns `Control`.
*Back (traced-to-code).* `StuntRaceBoard.ExitLabel`: "Back" for a menu launch, which returns to the
Instant Action screen as before; a command-line `--stunt` race keeps "Quit Game", since its exit
quits and "Back" would misname it. `ResultsBoard.InitShell` takes the label. Both boards use it.
*Borrowed, not original.* Every header ("Pilot", "Aircraft", "Best", "Gap", "Runs"), the title, the
zone heading and key, the splits, the context and the three plaque labels are remake text drawn in
the face of the lobby string standing at that place; the placement of the zone key, the splits,
the context and the plaques in the player list, chat pane, chat line and Create Team, Send and Leave
Game slots is the remake's reuse of those areas; the empty fifth column and unlabelled tabs follow
from it. The art, the fonts, the scores page geometry and Game Scores are the original's.
*For B14 (traced-to-code for the API).* `OriginalRaceTable.Rows(race.Standings(), race.ZoneCount)`
then `OriginalRaceTable.Compose(rows, pageX, pageY, UiStrings, layers)` into a `BoardLayers`, built
into a `ComposedBoard` and shown through `ComposedBoardView`; `VisibleRows` (10), `RowPitch` (20),
`PageArt`. The script's grey row and scroll bar are decoded and not built.

**Model recommendation.** Opus for the choice of screen and the layout read (the script, the
lobby's geometry and the column fit), a mid-tier model for a change of columns or plaque slots.

**Verify.** Units: `CSVM.Tests/OriginalRaceBoardTests.cs` (8): the table's rows, order and words;
its page, headers and cells at the script's positions, justifications and widths; the ten-row cap;
the frozen sheet's splits in race order and the "Zone n" fallback; the screen's background, page,
title, tab, context, zone key and splits; a 17-zone course's two key columns and narrower split
columns; the plaques' slots, frames and label tints; the pointer hit test. Engine suite
`stunt-race-boards` (`Testing/StuntRaceSuites.cs`, weighted): through `SessionBoards` with the
install's art and strings, Original builds `OriginalRaceBoard`, which wakes on the race's end with
the sim halted, ranks 1st P2, 2nd P1, 3rd P3 (one zone, no finish) with the install's faces, draws
P1's splits in its chat line, rests on Photo Mode and offers Restart and Back; Restart opens a new
window and retires it with the clock released; on the next end the pointer fires Back and Photo
Mode; Built-in keeps `StuntRaceBoard`, every column but the placing headed (GAP included), with Back
from the menu and Quit Game from the command line, and the Original board's command-line exit reads
Quit Game. Mutation-checked, each red and
restored: row pitch 21, aircraft left-justified, Best and Gap headers swapped, eleven rows, splits
in player order, Photo Mode and Restart plaques swapped, a hit test ignoring x, one key column to
20 zones, an uncapped split column (units); presentation ignored, no retire on a new window, no
halt, "Exit to Menu" from the menu, the menu resting on Restart, a pointer hitting nothing, the gap
header blank (suite).
Captures (temporary manifest entries through the golden stage, `--chapter=C1 --stunt --players=3
--debug-scoreboard` with and without `--force-builtin`, and `--menu=lobby:host:scores
--presentation=original`; manifest restored byte-identical): the Original board, the chrome board
and the remake's Game Scores page side by side. Every pinned golden unchanged (no pinned shot
flies a race). Hand-flown: the board's look, the column fit and plaque slots, and Back at the
controls from the Instant Action screen.

**⚠ Traps.** The original has no race board: do not describe this one as decoded beyond the scores
page; the reuse of the player list, chat pane and plaques is the remake's. Do not move the aircraft
back into a figure column: the 62-pixel cells clip most aircraft names. A field past ten rows (the
page) or seven pilots (the chat pane's splits) is cut off; C21's larger network field needs the
decoded scroll bar or a cap. The remake's own Game Scores tab left-aligns its figures where the
script centres them; the race table follows the script.

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

## B16 ☑ No lives in a race

**Goal.** A multi-seat stunt race spends no lives: a crash costs only time, the mission cannot be
lost by running out, and the Instant Action Lives row is hidden while the Race Time row shows.

**Evidence (confidence: traced-to-code, from B11's report).** B11 left Instant Action's lives
counting crashes in a race, and a race where every pilot runs out is lost onto the wrap-up board
(`InstantActionDirector`). The user's ruling: no lives in a race.

**Approach.** *The ledger (traced-to-code).* The only way an Instant Action mission is lost is
`InstantActionRuntime.NotifyPilotDown` returning false for the last registered pilot
(`Session/InstantAction/InstantActionRuntime.cs:418-447`), which also sends that pilot to
spectate through `InstantActionDirector.BeginSpectate`. `InstantActionRuntime.WaiveLives` sets
`LivesWaived`, and `UnlimitedLives` (`Def.Lives == 0 || LivesWaived`) is what `NotifyPilotDown`
now reads, so a waived mission answers every death with "fly again" and never ends on lives.
`WireEndConditions` waives exactly when `inputs.Race != null` (`InstantActionDirector.cs`, ahead
of the ledger registration), the same test that already drops the zone-set win for a race, so no
`MissionEnded`, `WrapupDue` or wrap-up hand-off can fire in a race and the race's own board is its
only ending. The crash then follows A1's rules unchanged: the director's `AutoRespawnAfter` and the
seat's tap/hold split. Every seat still registers, so the setup log keeps the seat count; its loss
reads "none, a race spends no lives".
*The hidden value (decision, traced-to-code).* A race ignores the stored lives value whatever it
is: the def keeps `Lives` as picked (`InstantActionDef.Lives`, still on the launch's def), the
runtime simply does not spend it, and the menu shows the same count again when the row returns.
The alternative, writing 0 into the def for a race, would lose the player's pick on the way back
to the menu, and a forced value in the menu would move under a seat joining or leaving.
*The Original screen (traced-to-code, B13's pattern).* Exactly one of the two remake-only boxes is
hidden at a time (`OriginalInstantActionScreen.HiddenRemakeKey`): Race Time solo or off stunt
flying, Lives in a race. Hidden, the Lives row keeps its index between the wingman plane and the
mission dropdown with `Visible` and `Enabled` false, `Column` -1 and no label, its "Lives:" title is
not drawn, `DropdownFor` answers null for it, an open Lives list closes when a seat joining makes
the run a race, and a focus left on it lifts to the nearest live box above
(`LiftFocusOffHiddenBox`, generalised from B13's race-time lift). Column -1 keeps the race
screen's column-1 count equal to the solo screen's, since Race Time enters column 1 as Lives
leaves it. The Race Time box stays on its own second clear line; it does not move up into the
Lives line.
*The Built-in screen (traced-to-code).* Built-in has no Race Time row, but its Mission screen
carries a Lives stepper in the description slot (`UI/Screens/LaunchMenu.cs`, `LivesDetail`). With
the cursor on Stunt Flying and a second seat joined (`InstantActionFeature.OffersRaceWindow`, the
Original's test), the detail is blank, the footer drops "←→  Lives" and a sideways press steps
nothing; another type or a solo run shows the stepper and its count again.
*The wrap-up (traced-to-code).* A solo stunt run and every other type keep the ledger, the
spectate hand-off, the loss and the wrap-up exactly as before: nothing changes unless a
`StuntRace` is handed in, which `GameSession` builds only for a stunt mission with more than one
rig.

**Model recommendation.** A mid-tier model suffices: one runtime flag, one line in the director
and B13's hidden-row pattern applied to the Lives box. The suite through the director needs care
with the Downed subscription order when two directors share a seat.

**Verify.** Units: `InstantActionEndTests.AWaiverMakesEveryDeathFreeWhateverTheDefHolds` (two pilots
on one life crash five times each with no life spent, nobody spectating, the def's count kept; the
unwaived control loses on the second death);
`OriginalInstantActionTests.TheLivesBoxGivesWayToTheRaceTimeBoxAndComesBackWithItsCount` (solo stunt
shows Lives; a joining seat hides it at the same index with no column, label or title, the focus
lifting to the live box above, no dropdown; another type and the guest leaving bring it back with
the count) and `ASeatJoiningUnderTheOpenLivesListOfAStuntRunClosesIt`. Engine: `stunt-race-no-lives`
(`Testing/StuntRaceSuites.cs`, weighted): two seats built through the roster over C1/IA1 with the
director's own `WireEndConditions` over a two-life def; P1 crashes three times and both crash
together, every crash returns its pilot after the crash camera, no life spent, nobody spectating,
the mission running and no wrap-up after the hold; the control, the same director wiring with no
race over P1 alone on one life, loses on its first crash into spectate and hands the wrap-up to
the menu once after the hold. `menu-original-instant-action` (Lives shown solo, hidden with its
title on the join, the focus left on it lifted to the box above, back with its count under
Zeppelin Run, gone again on Stunt Flying) and `menu-instant-action-journey` (the Built-in stepper
hidden for two seats on Stunt Flying, a sideways press leaving the count alone, back on Zeppelin
Run and solo). Mutation-checked, each red then restored (METHOD-9, METHOD-17): the director never
waiving (suite), the runtime ignoring the waiver (unit and suite), the director waiving solo too
(suite control), the Lives row always shown (unit and both menu suites' Original half), the hidden
Lives row left in column 1 (unit), the focus lift skipped for Lives (two units and the menu suite,
which fell back to the contents window), Built-in never hiding the stepper (journey).
Hand-flown: a two-seat Instant Action stunt race crashed into the ground on one life, the pilot
coming back each time and no wrap-up appearing; the Lives row's absence on both screens.

**⚠ Traps.** A solo stunt run and every other mission type keep their lives exactly as today; the
waiver keys on the race object, never on the pane count or the mission type alone. Do not write a
lives value into the def for a race: the menu would show the forced value when the row returns.
Do not drop the hidden Lives row from the list or leave it in column 1, for B13's reasons. The
waiver lives in the Instant Action director alone: a network race (C21) built outside it must keep
the lobby's Limited Lives (`VersusDirector`) off for Stunt Race, as Decision 4's time-limit-only
Mission Options imply, and nothing here enforces that.

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

# Wave C, the network race

## C21 ☑ Stunt Race in the Original lobby, flying the chapter's stunt course over the network

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
lands as an enum value rather than a third bool.
*The refactor (traced-to-code).* `DogfightMissionType` now stands beside `MenuMode` in
`Spec/SessionSpec.cs:56`, and `SessionSpec.MissionType` (`:234`, default Deathmatch) replaces the
two bools. `--ctf` and `--zvz` became parse votes that `Resolve` turns into the one field (`:2417`),
keeping both old drops (no `--vs`, `--zvz` beside `--ctf`). `FromMenu` takes a `missionType` in
place of the two bools (`MenuType`, `:2293`: the Dogfight types need `MenuMode.Versus`, Stunt Race a
stunt launch with no Instant Action def). `VersusRules` carries `MissionType` in place of its two
bools (`UI/Menu/MenuExit.cs`), `DogfightLobby.ModeOf` and `LoadScreens.MultiplayerKey` take the
enum, and the lobby reads the wire byte through `DogfightLobby.TypeOf`. Footprint: 13 source files
(`SessionSpec`, `DogfightLobby`, `MenuExit`, `OriginalLobbyScreen`, `LoadScreens`, `Launcher`,
`GameSession`, `SessionBoards`, `SessionVoices`, `OppositionStage`, `VersusDirector`, plus
`HumanFlightAdapter` and `SplitScreen` for the race), 4 suite files and 3 unit files.
`RearmRuntime`'s own `CaptureTheFlag` input stays a bool: it is a rearm rule input that
`VersusDirector` fills from the type, not a second home for the type.
*The value and the wire (traced-to-code for the layout, lead-only for the release rule).* Stunt Race
is value 3, appended after the string table's three, in `DogfightOptionsMessage`'s existing type
byte (`Net/NetDogfightMessages.cs:78`): same id `0x53`, same 16 bytes, no new field. There is no
protocol version to bump: two builds share a lobby only on the same MAJOR.MINOR
(`NetBuildVersion.PlaysWith`, `Net/NetBuildVersion.cs:114`, checked by `BuildVersionMessage`), and a
patch never changes the protocol. A build without type 3 that heard it would draw an empty Type
label, the Zeppelin vs Zeppelin line (the description index clamps), and fly a Deathmatch on `MP1`
while its host races, a desync. **Decision:** no wire change; Stunt Race must ship in a minor
release, never a 0.3.x patch (the tree reads 0.3.0, tagged), which the release must bump. Recorded
in `docs/org/multiplayer-messages.md`, the Dogfight lobby section.
*The course on every machine (traced-to-code).* The lobby launch leaves in `MenuMode.Stunt`
(`DogfightLobby.LaunchMode`, `UI/Menu/DogfightLobby.cs:423`; `OriginalLobbyScreen.cs:1173`), so
`FromMenu` builds `Stunt` on, `Versus` off, `Mission` `StuntRaceMission` = `IA1` whatever
`--mission` says (`SessionSpec.cs:143, 1849`), Scenario `stunt_flying`, and every machine loads the
same `<chapter>/IA1` and its `ia.json` `dzones` through the unchanged `StuntMission.Load` path
(`GameSession.cs:1884`). The guest builds its own spec from the host's options, the same path, so
both machines agree by construction.
*What the IA1 mission's content does (traced-to-code).* Nothing needs suppressing beyond what the
build already leaves out: the lobby launch carries no `InstantActionDef`, so
`InstantActionDirector.TryCreate` (`Session/InstantAction/InstantActionDirector.cs:107`) builds no
director (no waves, ace, wingmen, objective, lives, zeppelin switch or wrap-up), and `FromMenu`
clears a command line's `--ia=` for a race (`SessionSpec.cs:1865`). `Versus` is off, so
`VersusDirector.TryCreate` returns null (`Session/World/VersusDirector.cs:126`): no `VersusMatch`,
no lives limit, no kill or time end, no "fewer than two pilots left" ending, no match-state wire.
The opposition stage spawns only from the command line's own `--ai=`/`--zeppelins`/`--generators`
(`OppositionStage`), and B12 sleeps every world emplacement (`GameSession.cs:2301`). The suite reads
it: no Instant Action runtime, no AI aircraft, no Dogfight director, no generators on either machine.
*The race and its window (traced-to-code).* `GameSession.IsRace` (`GameSession.cs:2856`) builds the
`StuntRace` (`:1896`) when the stunt run has a second seat, a network guest's included
(`_seatRigs`, where it read `_rigs`, the local panes), or when the spec's type is Stunt Race, so a
host left alone in a lobby race still races. Its window is `StuntRaceMinutes`, which `FromMenu`
takes off the lobby's Time box (`VersusRules.TimeLimitMinutes`, `SessionSpec.cs:1891`). `Racing` is
set from `race != null` (`:2010`), so B12's no-contact, weapons-off, sleeping ground, race marks and
ghosts apply on every machine unchanged. Lives: the lobby greys Limited Lives in a race,
`RulesOf` carries 0 lives for a Stunt Race, and nothing on a non-Versus, non-Instant-Action session
reads a lives limit.
*Each machine's local race, and C22's hook (traced-to-code).* Each machine's `StuntRace` times its
own seats alone: `HumanFlightAdapter.Assemble` runs a course and calls `race.Add`/`race.Follow` only
for a local seat (`Session/Roster/HumanFlightAdapter.cs:443`, unchanged guard, now commented), and
the racer takes the network seat's callsign (`:484`). A remote seat flies its pose with no
`StuntMission`, no `Race` and no board row. So each machine's leaderboard and board show its own
pilot, each window opens on its own opening count (both counts begin in the build and run on the
sim, which the start gate holds until every machine has loaded; the loopback suite opens both on
one step), and each machine ends its own race. Restart (`RestartRace`, `GameSession.cs:2832`)
resets this machine's panes alone. **C22's hook:** keep `Follow` as each owner's local feed, report
`RunStarted`, `ZoneCleared(index, zone, runTime)`, `RunFinished(index, runTime)` and
`RunAbandoned` off that feed to the host, and on the host call the same `StuntRace` entry points
for each remote seat after `race.Add(seat, plane, "")` for it (the `Add` call this item skips at
`HumanFlightAdapter.cs:443`); the host's `BeginOpening`/`Advance` own the window and a guest's race
reads the host's. `StuntRace.Add` takes any seat index (`Flight/Modes/StuntRace.cs:237`), so the
seat numbering needs no change.
*The start (traced-to-code).* Outside `--det` every seat on every machine takes
`SharedSpawnStarts` (`GameSession.cs:1934`): the one spawn player 1 takes, the same pick on both
machines (shared seed). Each machine's own seat begins its opening count on the rails behind it, so
at build the remote seat stands on the spawn and the local one on the rails' start; at GO the local
seat is on the spawn (the suite measures it). No contact between them is B12's rule.
*The lobby (traced-to-code for the greying, the look is the user's).* The Type list offers four rows,
the fourth "Stunt Race" (`DogfightLobby.StuntRaceName`, remake-only, no langui id;
`OriginalLobbyScreen.cs:1364`), and the line under the Type box is the remake's own
`OriginalLobbyScreen.StuntRaceDescription` ("Race the chapter's Danger Zone course against the
clock. Fly as many runs as the time allows. The fastest complete run wins.") in langui 10123's face
(`:64, 1890`). Picking Stunt Race moves off a greyed environment as Capture the Flag's pick does,
sets Victory to Time and the Time box to 5 (`DogfightLobby.StuntRaceDefaultMinutes`; a repeat pick
keeps a typed window; `DogfightLobby.cs:508`). The Environment list greys every row whose chapter
ships no Danger Zones (`MenuChapters.DangerZonesFor`, `DogfightLobby.cs:403`): of the seven, Above
the Clouds (`C1C`) alone; `C2B` is not a lobby row. The greying follows the existing pattern, an
`Enabled` flag per row: `MissionRows` computes `rules = live && !race` (`OriginalLobbyScreen.cs:1275`)
and greys both radios, Score, Restrict Teams and its boxes, Limited Lives, Lives, Auto Respawn,
Allow Custom Planes, Outlaw Components and Select...; only the Time box, Type and Environment stay
live. The setters refuse the same options in a race (`SetVictory` `:531` and the rest), and
`LaunchRefusal` passes a race whatever the greyed team boxes stood on (`:273`). The plane rules stand
as the type change found them.
*The first-person band past four seats (traced-to-code).* Neither widened nor capped: a remote seat's
ghost stamp names `SplitScreen.EveryCameraLayer` (`UI/Boards/SplitScreen.cs:36`, the world's layer
1, which every pane, spyglass and photograph camera draws) in place of its seat's first-person
layer (`HumanFlightAdapter.cs:165`). No camera on a machine looks out of a remote aeroplane, so the
owner exemption is never wanted for one. The band then carries only a machine's own seats: a host's
panes are seats 0 to k-1 and a guest's seats are one contiguous run (`NetSeats`' run rule), at most
four either way, so `FirstPersonLayer`'s wrap (`:167`) never lands two of one machine's seats on one
bit, at any field size up to `NetSeats.MaxPlayers` (16).

**Model recommendation.** Opus: the refactor touches 13 files and the session wiring (the race's
seat rule, the remote seat's stamp and race membership) needs the whole net seat model read. The
lobby greying and its strings alone are mid-tier work.

**Verify.** Built: units in `CSVM.Tests/DogfightLobbyTests.cs`:
`AStuntRaceGreysAboveTheCloudsAndEveryOptionButTheTime` (the type moves off Above the Clouds, Time 5
and Victory Time, `Offers` equal to `MenuChapters.DangerZonesFor` per row, every other setter
refused, a repeat pick keeps 7, `RulesOf` = `(0, 7, 0, true, StuntRace)`, `LaunchMode` Stunt, back
on Deathmatch everything live), `AStuntRaceLaunchesWhateverTeamsTheGreyedBoxesStoodOn`,
`AStuntRaceCrossesTheWireAsTypeThree` (byte at offset 6 is 3, round trip, 4 flies nothing) and
`AStuntRaceLaunchFliesTheChaptersIa1WithTheLobbysWindow` (IA1 over `--mission=MP2`, window 7, each
Dogfight type on its own map, no other launch races); the refactor's units adjusted in place
(`ZeppelinVersusTests`, `LoadScreensTests`, `DogfightLobbyTests`, same assertions on the enum).
Engine suite `menu-original-lobby-stunt-race` (`Testing/MenuOriginalConnectionSuites.cs`): over the
loopback the Type list's fourth row is Stunt Race, picking it on Above the Clouds lands on Hawai'ian
Islands with Time 5 on both ends, the Environment list greys exactly the rows without Danger Zones,
every option but Time greys on the host (control: all live on Deathmatch), both ends draw the race
line, a typed 7 reaches the guest, and LAUNCH! and the guest's launch both leave as
`MenuMode.Stunt` on C1 with the race and 7 minutes, which `FromMenu` flies as C1/IA1. Engine suite
`net-lobby-stunt-race` (`Testing/NetLobbyEnvironmentSuites.cs`): two sessions over the loopback from
the lobby's options with Limited Lives ticked: both specs C1/IA1, Stunt, not Versus, 7 minutes, 0
lives; both build with no Instant Action runtime, AI aircraft, Dogfight director or generators; the
same 5-zone course on both; the remote seat on the spawn and each local seat on the same rails'
start on both machines; each machine races its own seat alone, 420 s, under the seat's callsign;
every aircraft unarmed and racing; ghost stamps local = its first-person layer, remote = the
every-camera layer; both windows open on one step with each local seat on the spawn. Both weighted.
Mutation-checked, each red then restored: `IsRace` back on the local panes (net suite: no race),
the remote stamp on the seat's band (net suite: 0/111 right/wrong), the Time box dropped from the
window (unit and net suite: 300 s), the race on `cli.Mission` (unit and menu suite: MP1), Stunt Race
offering Above the Clouds (unit and menu suite), the lobby rows greyed on `live` alone (menu suite:
seven rows still live), `LaunchMode` always Versus (unit and menu suite), `LaunchRefusal`'s race pass
removed (unit), `SetVictory`'s race refusal removed (unit), `RulesOf`'s race branch removed (unit and
net suite), the callsign dropped (net suite: P1/P2), the default 5 not set (unit and menu suite).
Battery: every unit (6227 passed, 3 skipped), the 105 engine suites matching net-, lobby,
menu-original, versus, stunt, race, pause-sheet, load-sheet and instant-action, `-Quick` and the
goldens (24 hash-identical) all pass. Hand-flown, owed: a two-machine sitting (the Deck can host,
per `analysis/net-real-link/`) picking Stunt Race in the lobby, both machines on the same course,
each pilot's own race and board, the ghosts and no contact; the Type list's fourth row and the
race line are the user's look.

**⚠ Traps.** Do not put the race's type back on two bools or a third bool: every check reads
`SessionSpec.MissionType`. Do not ship type 3 in a patch of a minor that lacks it: the wire has no
version of its own, only MAJOR.MINOR keeps an older build out. Do not set `Versus` for a Stunt Race:
it would build a `VersusMatch` with its lives, its time end and Deathmatch's "fewer than two pilots
left" end, the match-state wire and the `MP1` spawn table. Do not read the race condition off
`_rigs`: a network guest has one pane, so the race would vanish on two machines. Do not stamp a
remote seat with its seat's first-person layer: past four seats it shares a bit with a local pane
and draws solid there. A remote seat must stay out of the local race until C22 feeds it from the
host, or a machine would rank a pilot it cannot time. A Built-in guest of an Original host flies what
its own screen chose, as it already does for Capture the Flag and Zeppelin vs Zeppelin; the race
does not change that. The race board's Exit and the lobby return are C23's: `Launcher.LobbyLanding`
lands only a completed `VersusMatch`, so a race exits to the launch's own destination today. The
command line's opposition flags (`--ai=`, `--zeppelins`, `--generators`) still reach a lobby race
launched from a development command line.

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

## C22 ☑ Owner-timed runs on the wire; the host's window, leaderboard and match end

**Goal.** Each machine times its own pilot's runs and reports zone splits, finishes and restarts to
the host; the host keeps the window clock, the leaderboard of best runs and the FINAL RUN stretch,
and sends the leaderboard and the match state to every guest, whose live leaderboard and boards read
them.

**Evidence (confidence: lead-only for the design; traced for the authority model).** Aircraft state
and deaths are owner-authoritative (`Net/NetMessages.cs:447, 691`); `MatchStateMessage` is written
only by the host (`NetMessages.cs:1000-1014`) and sent on `MatchStateCadence`'s 1 Hz tick, with the
match end sent at once (`Net/MatchStateCadence.cs:10-27`, `Session/World/VersusDirector.cs:247,
685, 690`); `VersusMatch` takes a guest's clock and ending from the host (`Flight/Modes/VersusMatch.cs:78-81`).

**Approach.** *The shape (traced-to-code).* `Session/World/NetRaceLink.cs` is the one link, opened
by `SessionNet.WireRace` (`Launch/SessionNet.cs:415`) before the roster builds
(`GameSession.cs:1943`), since `HumanRosterBindings.RaceFeed` (`GameSession.cs:2041`) replaces
`race.Follow` for each local seat (`HumanFlightAdapter.cs:485`). Every machine's race now holds every
seat: a remote seat is `Add`ed with no score key and its callsign and runs no course
(`HumanFlightAdapter.cs:514`). On the host `Feed` is `race.Follow`; on a guest it reports the run's
four events. The layouts and the rules are `docs/org/multiplayer-messages.md`, "Stunt race".
*The run report (traced-to-code for the build, lead-only for the choice).* `RaceRunMessage`, `0x67`,
reliable on the events channel, guest to host, one per event: the run clock's start, each zone's
first clearing with its run time, the finish, a restart (`NetRaceMessages.cs:48`). Per split rather
than per finish, because Decision 9 ranks a pilot with no finish by zones and time, and the host's
board must show that while the run is in progress. It carries seat, kind, zone, round, the owner's
run number and the run time, 16 bytes. Reliable delivery is ordered and never duplicated
(`Net/INetTransport.cs:10`), so the run number and round guard what the transport cannot: a report
from a window the host has moved past, a repeated start, an event of a superseded run, a seat the
sender does not fly, a non-finite time (`NetRaceLink.cs:207-234`). Each is dropped and counted. The
race then applies its own rules to what passes (B11): a start counts only while the window is open,
judged by its arrival at the host; a finish counts in the final run; nothing counts after the end.
*The leaderboard (traced-to-code).* `RaceStandingMessage`, `0x69`,
one racer's line: counts, best or furthest run, and the ranking run's splits, 120 bytes for up to 24
zones (the longest shipped course has 17). One message per racer, because a whole table of 16
racers' splits would pass the session's 512-byte send buffer (`Net/NetSession.cs:19`).
Event-driven: `Racer.Revision` (`StuntRace.cs:99`) counts every change, and the host's `Flush`
(`NetRaceLink.cs:166`) sends each racer whose revision moved, from its step after `race.Advance`
(`GameSession.cs:3138`) and at once from the report handler, since a finish that ends the race
halts the host's simulation. No cadence for the lines: reliable delivery already guarantees them.
*The window clock (traced-to-code, lead-only for the choice).* Its own message, `RaceStateMessage`,
`0x68`: phase, round, seconds into the phase's clock, the window, the host's session clock. Not
`MatchStateMessage`: its fields are a Dogfight's score target and `NetMatchEnd` reasons, and a race
has an opening and a final run where a match has neither; `VersusDirector` owns its handler. Sent on
`MatchStateCadence`'s 1 Hz tick and at once on every change of phase, the ending among them, always
after the lines on the same ordered channel, so a guest's board waking on the ending reads final
lines.
*A guest's race (traced-to-code).* `StuntRace.Replicate` (`StuntRace.cs:376`): `TakeLine` (`:380`)
loads a racer whole and raises `BestImproved` on a better best, which records the guest's own
pilot's best off the host's accepted run (Decision 10). `TakeHostClock` (`:394`) moves the phase only
forward and ends the race on the host's ending. The run entry points count nothing on a replica, the
cap never ends it (`:366`) and `EndIfNoRunLeft` is the host's (`:552`). Its opening and window still
run between readings, so its own time up already refuses a guest's `Rerun` through the unchanged
`Race.MayStartRun` gate (`FlightController.cs:1335`), and the host's FinalRun reading closes it
for a guest whose clock lags.
*The opening from one host instant (traced-to-code; the one-step bias lead-only).* The start
barrier releases a guest a link after the host, so its count would open that much late. Each host
reading carries the host's session clock. The guest reads it forward by its lateness, its own clock
plus the slew's newest reading less the stamp (`NetRaceLink.cs:314`), and catches its race's
opening up in whole steps (`StuntRace.CatchUpOpening`, `:560`). `CatchUp` then skips every local
seat's count the same seconds (`StartCount.CatchUp`, `StartCount.cs:104`, through
`FlightController.CatchUpStartCount`), so the seats' GO and the window stay on one step. It rounds
down, counts the step this machine is about to take and stops one step short of GO, so a guest errs
a step behind. A guest that opened first would send its first run's start into the host's
still-closed window. A round trip measured across the held start reads short, since neither
session clock moves while held, so a guest asks the host's clock again on its first flown step
(`NetClockPing.AskSoon`, `Net/NetClockPing.cs:105`). A plain `StartCount.Begin` overload was not
enough: the counts begin in the build, before the reading arrives.
*How a race ends on a guest (traced-to-code).* The host decides time up, the final run and its
120 s cap; its ending goes out at once like the Dogfight's, and the guest's replica raises
`RaceCompleted` on it, which wakes its board (`Flush` and `TakeHostClock`).
*Wire compatibility (traced-to-code).* Ids `0x67` to `0x69` follow `0x66`, minted above the
original's ceiling (`NetMessages.cs:180`). A build without them drops them as unknown, and
MAJOR.MINOR keeps such builds apart; they ship with type 3 in the minor release C21 named.
*Seams for C23 (traced-to-code).* `StuntRace.Round` (`:238`) counts windows and `Restart` advances it
(`:467`); every message carries it, and a guest drops a line or a clock under another round
(`NetRaceLink.TakeLine`, `TakeState`), where C23's restart word starts the guest's new window. A
guest's `GameSession.Rerun` is refused for a replicated race (`GameSession.cs:2827`), where C23 puts
Leave and the host's Restart and Lobby. A pilot leaving: `SessionNet.TakeSeatLeft` is where C23
marks the racer "left"; the race keeps a racer whose reports stop. The display B14 needs is
`StuntRace.Standings()` on every machine, which now holds the whole field.

**Model recommendation.** Opus for the clock and authority design (the opening's catch-up rests on
the slew, the start barrier's step order and the held-clock round trip, each found by measurement);
a mid-tier model for a message field or a doc change once the rules hold.

**Verify.** Built: `CSVM.Tests/NetStuntRaceTests.cs` (8), two sessions over a perfect loopback:
the three messages' ids, sizes, reliability, byte offsets and round trips, a line cut at 24 zones; a
guest's run reaching the host's race and coming back to the guest's board with its splits, the
guest's own best raised once, the replica's run entry points counting nothing; a repeated start and
a superseded run's zone and finish dropped while the run in progress's zone counts; another window
and a spoofed seat dropped; a finish after time up counted, the ending reaching the guest after the
final line, and a start reaching the host in the final run refused; a finish past the cap refused,
the guest's replica outliving its own cap and ending on the host's word; the catch-up in whole steps,
never back and never past GO; the lateness read off the slew and the count skipping the same
seconds. Engine suite `net-stunt-race-wire` (`Testing/NetStuntRaceSuites.cs`, weighted): two
sessions from the lobby's options over a 100 ms loopback, then over 100 ms with 20 ms jitter and
25 % loss: the guest released 5 and 6 steps late opens its window on the host's step (304/304,
305/305, caught up 5 and 6 steps); both boards hold both pilots, the guest's a replica; the guest's
and the host's first runs reach the other's board with their splits; both boards agree on order,
bests, splits and run counts after a run each, after a final run and at the end; a guest run
finished after time up counts as its best while the host's pilot flies on; the guest's restart in
the final run is refused; the guest's race passes its own cap and ends on the host's, which cut the
host pilot's run at the cap. `net-lobby-stunt-race` now reads both seats on both machines' races,
own seat with a score key, the guest's a replica, and still opens both windows on one step.
Mutation-checked, each red then restored byte for byte (METHOD-9, METHOD-17): no catch-up (units and
suite: 304/309), the guest never asking again (suite: 304/309), the replica ending at its own cap
(unit and suite), the host ignoring remote starts (units and suite), the repeated start counted, a
superseded run's event counted, the round check dropped, the owner check dropped (units), the clock
sent before the lines (unit), the lines never sent (units and suite), the guest's best never raised
(unit), the remote racer not added (both suites), the catch-up rounding to nearest with no one-step
bias (units and both suites: the guest opening a step first, 299/298 and 304/302). Hand-flown, owed:
a two-machine sitting (the Deck can host, per `analysis/net-real-link/`) for the opening count's
start on both machines, both boards during and after the window, and a run finished after time up.

**⚠ Traps.** Do not feed a guest's race from its own runs: its record is the host's, and a local feed
would flicker the leaderboard with each line and save a finish the host refused. Do not let a guest's
race end of its own accord: the cap and "no run in progress" are the host's to decide. Do not
compute the lateness from the slew's walked offset: it lags the newest reading, and the suites never
walk it. Do not round the catch-up to nearest or drop the step it counts: a guest that opens first
has its first run's start refused at the host. A start within one link's latency of time up is
refused by its arrival, the host's decision by Decision 7; stamping it with the guest's clock would
move that decision to the guest. Do not move the lines after the clock in `Flush`. A session clock
stands still through a held start, so a round trip measured there reads short; that holds for every
network mode, and only the race asks again. The suite rig drives `_PhysicsProcess` alone, so its
`Step` frames each session's clock first; without it every clock reads 0 and no lateness is seen.

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.

## C23 ☑ Network race end: Restart and Lobby, guests waiting, pilots leaving

**Goal.** The host's race board offers Restart (a fresh window, opening count, same course and
rules) and Lobby (everyone back to the lobby); a guest's board says it is waiting for the host and
offers Leave. A pilot who leaves keeps their best on the boards marked "left"; the host leaving ends
the race; a race left with one pilot runs on to the window's end.

**Evidence (confidence: lead-only; traced for today's Dogfight end).** A completed match plus R
calls `RestartMatch` (`FlightController.cs:3695-3698`); a guest's Dogfight board offers no Restart
(`docs/controls.md`, the `Backspace` row).

**Approach.** *The wire (traced-to-code for the layout, lead-only for the choice).* One new
message, `RaceCallMessage`, `0x6A` (`Net/NetRaceMessages.cs:170`, `Net/NetMessages.cs:190`),
reliable on the events channel, 8 bytes: call at 4 (restart 1, lobby 2, leave 3), round at 5. The
host sends restart and lobby to every guest; a guest sends leave to the host. One message rather
than three, because each is one word naming a window. Not a newer-round `RaceStateMessage` as the
restart word: C22's `Flush` sends the lines before the clock, so a guest would drop the new
window's lines under its old round before the clock arrived. The call goes out at the restart
itself (`GameSession.RestartRace`, `:2879`), ahead of that window's lines and clock on the same
ordered channel, so the guest is in the new round when they land. The racer line `0x69` gains
flag bit 2, the pilot left (`NetRaceMessages.cs:224`), so a guest's replica takes the mark from the
host's record as it takes the rest. Same minor release as type 3 and `0x67` to `0x69`.
*Restart (traced-to-code).* The host's board keeps Restart; its R and the row are `Rerun`, now
`RestartRace` plus `NetRaceLink.CallRestart` (`Session/World/NetRaceLink.cs:115`). A guest's
`TakeCall` (`:263`) takes a restart only from the host and only for the round after its own, and
runs `Restarted`, which `GameSession` binds to the same `RestartRace` (`GameSession.cs:2105`):
every local seat respawned on the shared spawn behind `StartCount.Opening`, the race restarted and
`BeginOpening` called in one step. The new window's first clock reading then catches the guest's
opening up to the host's instant through C22's unchanged `TakeState`/`CatchUpOpening`/`CatchUp`
path, since the race is back in its opening when it arrives. A guest's own `Rerun` stays refused
(`:2855`). A guest's restart resets its seat's run, whose abandon would name run 0 of the new window;
`Report` sends nothing for a run the window never numbered (`NetRaceLink.cs:194`).
*Lobby, and where the results land (traced-to-code for the path, lead-only for the place).* The
host's board names its exit Lobby and a guest's Leave (`StuntRaceBoard.NetworkExitLabel`,
`UI/Screens/StuntRaceBoard.cs:65`; Quit Game from a command line). Every board's and the pause
sheet's exit is now `GameSession.LeaveFlight` (`:1117`): `NetRaceLink.Leave` first, then the
launcher's exit. A host leaving an ended race from a menu launch sends the lobby call; a guest sends
leave. On a guest the call sets `LobbyCalled`, and the launcher's lobby-guest upkeep
(`Launcher.TickVersusGuestFlight`, `Launch/Launcher.cs:3214`, the check at `:3227`) runs its own
`ExitSession`, which lands it as a finished Dogfight lands. **Decision:** the race's results land
on the lobby's Game Scores tab, the page the Original race board already borrows.
`Launcher.LobbyLanding` (`:1471`) takes the race, `RaceLanding` (`:1489`) builds
`OriginalRaceTable.Rows` of an ended race, `LobbyReturn` carries them (`UI/Menu/MenuReturnDestination.cs:77`),
`DogfightLobby.Land` holds them as `RaceScores` (`UI/Menu/DogfightLobby.cs:322`; `RaceTableRow`
moved beside `DogfightScore`, `:19`), and `OriginalLobbyScreen.ComposeScores` draws them with
`OriginalRaceTable.ComposeRows` (`UI/Menu/Original/OriginalLobbyScreen.cs:1950`) in the race board's
columns and grey rows; the next Dogfight's landing replaces them. A Built-in lobby has no page and
lands nothing, as for a Dogfight.
*The guest's board (traced-to-code, the look is the user's).* Built-in: `ResultsBoard.RestartWithheld`
reads `StuntRaceBoard.WaitingForHost`, "Waiting for the host", over Photo Mode and Leave, the
Dogfight guest's pattern. Original: `OriginalRaceBoard.RestartWithheld` (`OriginalRaceBoard.cs:53`)
builds a two-row menu, `OriginalRaceResults.Slots` (`OriginalRaceResults.cs:144`) leaves the Send
plaque empty so Leave keeps the Leave Game slot, `MenuRowAt` maps the pointer, and the chat line
reads "C1 · Stunt Flying · Waiting for the host". `SessionBoards.BuildRaceBoard` takes both
(`Launch/SessionBoards.cs:148`).
*A pilot leaving (traced-to-code).* A guest walking back to the lobby keeps its link, so its leave
call is the sign: `SessionNet.WireRace` hooks `GuestLeft` to `TakeGuestLeft` (`Launch/SessionNet.cs:418`);
a dropped link reaches the same `OnPeerLeft`. `TakeSeatLeft` sets the aeroplane inert (`:1046`),
which hides its pivot and leaves `InPlay` (`FlightController.cs:3133`), so the ghost, the race label
and every query go with it, and now calls `NetRaceLink.SeatLeft` (`SessionNet.cs:1053`), which runs
`StuntRace.MarkLeft` (`Flight/Modes/StuntRace.cs:459`) and on the host flushes the marked line at
once. Every guest reaches `TakeSeatLeft` through the world link's seat-left event. `MarkLeft` keeps
the record and `Racer.Left` (`:89`); `Compare` is untouched, so the pilot ranks as their record
stands; a run in progress stops, which can end a final run; the run entry points count nothing more
for a left racer; `Restart` drops left racers (`:497`), since nobody joins a race in progress.
*The marks (traced-to-code, the look is the user's).* Original board and lobby: the scores page's
decoded grey (`0xffbbbbbb`, `OriginalRaceTable.cs:66`) on the whole row and on the splits row.
Built-in board: `StuntRace.NameText` (`:308`) adds " (left)" and the row dims. Built-in held table:
`ScoresTable.Race` takes `NameText` (`UI/Overlays/ScoresTable.cs:50`), as does the live leaderboard's
leader name. ⚠ Not built: the Original held table (`UI/Overlays/OriginalScoresText.cs:108`, B14's
borrowed race grid) still writes `r.Callsign` unmarked, because a B14 follow-up owns that file on
another branch; the fix is `StuntRace.NameText(r)` in its name cell once that branch lands.
*One pilot left, the host leaving (traced-to-code).* A network race builds no `VersusMatch`
(`VersusDirector.TryCreate` returns at `Session/World/VersusDirector.cs:126` for a non-Versus spec),
so no "fewer than two pilots left" ending exists to apply; the race runs to its window and final run.
The host leaving mid-race lands nowhere in the lobby (the race has not ended), so `EndNetWire` closes
its door with the notice, and each guest's door fails and its upkeep returns it to the Connection
page: the Dogfight's path, unchanged.
*No join in progress, Decision 13 (traced-to-code).* `NetLobby.Peers` is the peers present when the
session bound (`Net/NetLobby.cs:209`), so a peer arriving mid-race is handed the advert and waits in
the lobby; no race line reaches it and its payloads never reach the session. Verified, nothing
changed.
*A capture aid.* `--debug-race-end=host|guest` (`Spec/SessionSpec.cs:871`, `docs/cli.md`) poses a
split screen `--debug-scoreboard` race's board as the host's or a guest's with player 2 marked left
once its run counts, since a golden shot is one process and a network board needs two.

**Model recommendation.** Opus for the window's end on the wire (the call's place ahead of the
lines, the guest's restart riding C22's catch-up, the lobby hand-off through the launcher's guest
upkeep and the left mark through the seat-left path); a mid-tier model for a board label, the grey
or the suffix.

**Verify.** Built: units `NetStuntRaceTests` (13): the race call's id, reliability, 8-byte layout
and round trip, a left line's flag byte; the host's restart opening the guest's next window ahead of
its lines, an old window's line, the same restart again and an old lobby call each dropped, the new
window's report counted; the lobby call only from an ended race and only on `toLobby`, a guest's
leave reaching the host as `GuestLeft`; a seat marked left keeping its best on the guest's board,
first, and its later start counting nothing; a late joiner on a `NetLobby` getting no race line and
its report and leave reaching nothing. `StuntRaceTests` (15): a left pilot's best ranked as it stood,
`NameText` and the leaderboard's leader marked, nothing more counted, a rival's faster run still
outranking it, `Restart` dropping it; a left pilot's run ending a final run, a replica only marking.
`OriginalRaceBoardTests` (10): the grey row and grey splits, the other rows black; the guest sheet's
two plaques, Leave on the Leave Game slot, the waiting line, `MenuRowAt`. `DogfightLobbyTests`: a
race landing its table and the next Dogfight replacing it. Engine suite `net-stunt-race-end`
(`Testing/NetStuntRaceSuites.cs`, weighted), two sessions from the lobby's options over a 100 ms
loopback: at the window's end the host's board offers Photo Mode, Restart, Lobby and the guest's
Photo Mode, Leave under "Waiting for the host"; the guest's restart opens nothing; the host's Restart
puts both machines in round 1 on the guest's one call, both openings ending on step 299 with each
local seat on the shared spawn, both boards retiring, an old window's line sent into the new one
dropped; in the new window the guest's Leave keeps its best first and marked left on the host's
board, its aeroplane inert and undrawn there, the host's race still open 120 steps later and ending
on its window's step, the host's board row reading "1st  guest1 (left)"; on a second pair the host's
Lobby calls the guest and both machines land the same table (control: a running race lands none).
Engine suite `menu-original-lobby-race-end` (`Testing/MenuOriginalConnectionSuites.cs`, weighted)
over two lobby doors: an ended race lands both ends on a live Game Scores with the race's table, the
left pilot's row grey and the other black and no Dogfight header; a second race launches from that
lobby; the host leaving it fails the guest's door ("Host left the game") onto the Connection page.
Mutation-checked, each red then restored byte for byte (METHOD-9, METHOD-17): left racers ranked
last, `Restart` keeping them, a left racer's start counted, `MarkLeft` not stopping the run, the
grey ink black, the guest's slots on the Restart plaque, the line's left flag unwritten, the
restart call's round check dropped, the lobby call from a running race, the late joiner reaching the
session (`NetLobby.Peers` = every peer), `Land` keeping an old table, the line round check dropped
(units); the restart call never sent, the guest's restart catch-up lost (299/304), `TakeSeatLeft`
not marking the racer, the guest's leave never sent, the seat not set inert, the race ending at
fewer than two pilots, the guest's board not withheld, the lobby call never sent, the line round
check dropped, the guest's `Restarted` doing nothing (net suite); the lobby page ignoring the race,
`LobbyLanding` ignoring it, `VersusGuestFlightOver` never true, `RaceLanding` of a running race
(menu suite). Captures through the golden stage (temporary manifest entries, `--chapter=C1 --stunt
--players=3 --debug-scoreboard --debug-race-end=host|guest --det --mute` with `--presentation=original`
and `--force-builtin`; manifest restored byte-identical; the 24 pinned shots unchanged): the host's
and the guest's boards in both looks with P2's row left. Hand-flown, owed: a two-machine sitting for
the host's Restart reopening both machines' windows on one count, Lobby taking the guest to Game
Scores with the table, the guest's Leave and its grey row on the host, and the host leaving
mid-race; the guest board's waiting line, the Lobby and Leave words, the grey row and the "(left)"
suffix are the user's look.

**⚠ Traps.** Deathmatch's "fewer than two pilots left" ending must not reach a race (Decision 16):
it lives in `VersusMatch`, which a race never builds, so do not set `Versus` for a race or add a
field-size end to `StuntRace`. Do not make the restart word a newer-round clock reading or move the
call after `Flush`: a guest drops every line under a round it is not in yet. Do not let a guest's
board restart in place: its window is the host's. Do not remove a left racer from the field
mid-window: Decision 16 keeps the record ranked; only a new window drops it. A guest's walk back to
the lobby keeps its link, so the host learns of it only from the leave call; a dropped link is the
other path, and both meet in `TakeSeatLeft`. The host's lobby call is sent only from an ended race:
a host leaving mid-race closes its door instead, which is what ends the guest's flight.

**Verified.** The complete `.\RunTests.ps1` on the finished plan tree (00295edf6, every item and the follow-ups merged, main merged in): build PASS; units 6272 passed, 3 skipped of 6275; engine 527 passed, 2 skipped, engine errors clean; goldens 24/24 hash-identical. This item's own suites and units are among them; the at-the-controls checks are PT-177 and PT-178 in `playtest.md` and the two-machine sitting.
