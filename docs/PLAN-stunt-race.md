# Stunt race: a time-attack Danger Zone race, solo, split screen and network

**QUEUED PLAN** (written 2026-10-03). Work starts in the release after 0.3.0, alongside
`PLAN-bots.md`; until then PROJECT_CONTEXT.md's "Current status" does not name it, and the session
that starts Wave A flips this banner to ACTIVE PLAN and points "Current status" at it. When every
item lands, the closing commit deletes this file, records the completion in its message, and clears
the "Current status" pointer; any live prose linking this file by path is unlinked in the same
commit.

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
`BL-314` was read in this session but not re-verified against `git log --grep=BL-314` and the code;
<TODO: re-verify still-open against git log --grep=BL-314 + the code before Wave A starts>. When
this plan goes ACTIVE, `BL-314`'s entry shrinks to a pointer at it.

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

1. ☐ Tap respawn returns to the last cleared Danger Zone; hold restarts the run
2. ☐ The on-rails restart count, and the run clock starting at GO; one bests key

### Wave B, the split screen race

11. ☐ The split screen race becomes a time attack: window, opening count, best-run ranking, FINAL RUN, boards
12. ☐ Race presence: no collisions, weapons off, ghosts when near
13. ☐ The Instant Action time row for a multi-seat Stunt Flying run

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

## A1 ☐ Tap respawn returns to the last cleared Danger Zone; hold restarts the run

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

**Approach.** <TODO: the respawn pose at a zone's exit: which aperture ring is the exit when zones
are order-free and each has a green and a red gate; derive the heading from the ring's normal or the
route ribbon, and the speed from `FlightStart` (never a constant)>. Split respawn into tap and hold
inside the respawn consumer (as D-pad up already splits tap and hold for targeting,
`docs/controls.md`). <TODO: the hold duration>. Update `docs/controls.md`'s `Backspace` row.

**Model recommendation.** <TODO>

**Verify.** <TODO: a unit over StuntMission for the respawn target after 0, 1 and 3 zones; an engine
suite that crashes after a zone and asserts the respawn pose and the unchanged clock; the hold path
clearing zones and clock; mutation-checked>. Every pinned golden unchanged.

**⚠ Traps.** A respawn while still flying must not become a free repair, restock and refuel beyond
what a stunt run already allows (`docs/controls.md`, the `Backspace` row). The respawn speed is the
mission's own spawn speed, carried on `FlightStart` (`BL-314` trap 1).

## A2 ☐ The on-rails restart count, and the run clock starting at GO; one bests key

**Goal.** A hold-restart (and a solo run's first start, <TODO: decide whether a solo run's very
first start gets the count or starts at once as today>) runs a 3, 2, 1, GO count during which the
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

**Approach.** A count controller shared by solo, split screen and network (B11 and C22 reuse it):
it owns the count's phase, drives the walk into `_model.Reset` each frame, ignores stick and
throttle, and hands control over on GO. Rewrite the `StuntMission.Elapsed` comments to separate
"the clock starts at GO" from "the clock never stops once started". Count numbers on the chrome
type scale with a beep per number and a higher GO tone (Decision 15). <TODO: which shipped UI
sounds>.

**Model recommendation.** <TODO>

**Verify.** <TODO: a unit for the walk ending bit-equal on the spawn pose at GO for two missions
with different spawn speeds; an engine suite asserting the clock reads 0 at GO and inputs held
through the count change nothing before GO; `--det` runs byte-identical (every golden unchanged)>.

**⚠ Traps.** From `BL-314`, verbatim in substance: (1) do not derive a setback from a speed; the
walk ends on the spawn pose whatever the speed is; (2) "the clock never stops" is stated twice in
`StuntMission.cs` on purpose, keep it and add the start-at-GO rule beside it; (3) do not simulate
the count and do not freeze the sim; (4) the instrument is a hand-flown sitting.

# Wave B, the split screen race

## B11 ☐ The split screen race becomes a time attack: window, opening count, best-run ranking, FINAL RUN, boards

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

**Approach.** Rework `StuntRace` into the window's bookkeeping: per pilot a best run (time and
splits), the current run, a run count, and the window clock with its FINAL RUN stretch. Retire
`StartGrid` from the race path (co-op keeps it). <TODO: the live leaderboard's HUD placement and
the held scoreboard key's binding in a stunt race>. Bests record per Decision 10b.

**Model recommendation.** <TODO>

**Verify.** <TODO: units for the ranking (finishers by best, non-finishers by zones then time), the
FINAL RUN cap and the no-start-after-time-up rule; an engine suite flying two scripted seats through
a short window with one restart each>. Hand-flown: a two-seat split screen sitting for the count
and the restarts.

**⚠ Traps.** <TODO>

## B12 ☐ Race presence: no collisions, weapons off, ghosts when near

**Goal.** In any race, aircraft never collide with each other, fire controls do nothing and the
weapon readouts leave the HUD, ground targets on the course world stay inert, and another pilot
fades from solid beyond 80 m to a ghost inside 40 m, keeps its callsign label and is never a target.

**Evidence (confidence: lead-only; traced for the collision path).** Aircraft collide today through
`FlightController`'s plane-versus-plane ram with a collision-grace window
(`FlightController.cs:1892-1926, 4148-4149, 4250`). The callsign label is #116's marker.

**Approach.** <TODO: how the race suppresses the ram (a mode flag on the session, not a per-rig
special case); how weapons are disabled (the loadout, the input or the fire path) without changing
a non-race session; the ghost's material path under both presentations and its distance fade; how
the target cycle and the spyglass skip a race pilot>.

**Model recommendation.** <TODO>

**Verify.** <TODO: an engine suite flying two seats through each other (no damage, no ram event);
fire held in a race (no projectile spawned); the fade's alpha at 30, 60 and 100 m; the target cycle
skipping the other pilot>. Every pinned golden unchanged (no golden flies a race).

**⚠ Traps.** The 80 m and 40 m are TUNE, judged at the controls. Ghosting must not change a
non-race session's rendering.

## B13 ☐ The Instant Action time row for a multi-seat Stunt Flying run

**Goal.** With Stunt Flying chosen and more than one seat joined, the Original Instant Action
screen shows a time row (3, 5, 10, 15 minutes, default 5) that sets the race window; a solo run
shows no row.

**Evidence (confidence: lead-only).** The Instant Action screen's rows are listed in the focus-order
census of #25 (`IA_D_LIVES`, `IA_D_NWING` and the other dropdowns).

**Approach.** <TODO: where the row sits in the column and its focus order (#25 is open on this
screen's order); its label (no original string exists; a remake string)>.

**Model recommendation.** <TODO>

**Verify.** <TODO: a menu suite: the row hidden solo, shown with two seats, its choice reaching the
race window>.

**⚠ Traps.** <TODO>

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
