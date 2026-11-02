# Air race courses in multiplayer Stunt Race

**ACTIVE PLAN** (written 2026-11-02). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan delivers issue #277: an authored air race course (PLAN-air-race) flown in a Stunt Race,
the shared-window time attack, with a second local pilot, with bots, and over the network. Today a
course is solo only. Four gates refuse it: `SessionSpec.CourseRefusal`
(`CSVM/src/Spec/SessionSpec.cs:2910-2916`) refuses `--course=` with `--players>1`, `--vs-bot`/
`--vs-bots`, `--vs` or a network session; `InstantActionFeature.OffersCourse`
(`CSVM/src/UI/Menu/InstantActionFeature.cs:365`) hides the Course box with more than one seat or a
bot row; the network lobby offers no course. #277 was re-verified still open in this session against
both the issue (`gh issue view 277`: OPEN, no comments, no commit cites it) and the code (all four
gates stand as described).

Out of scope: Dogfight on a course (`--vs` stays refused), Danger Zone photographs on a course run,
a penalty count on the race board, and any change to how a solo course run scores. Authored courses
are remake-only, so no item has an original-game value to decode.

## Milestone goal

- A network host's course reaches every guest in the lobby, and every machine flies the same gates
  from the host's copy, checked against `Course.Hash`.
- The network race carries a course run's starts, splits, finishes and reruns for a sequence of up
  to 255 entries, with penalty time inside the run times, and each machine records its best under
  its course key.
- Stunt race bots fly a course's sequence in order, through each gate from the side the entry
  names, including moving rings, and shoot what blocks their line.
- `--course=` accepts a second local pilot, race bots and a network session, and the Instant Action
  menu and the network lobby offer the Course choice there.

**A course run stays a course run on every path.** It never takes a Danger Zone photograph
(`StuntCapture` returns on a course run) and never records under a Danger Zone key
(`ScoreStore.KeyFor`); the network and bot paths keep both true rather than growing their own.

## Decisions (2026-11-02)

| # | Question | Decision |
|---|---|---|
| 1 | A course longer than the 24 splits `RaceStandingMessage` carries, in a network race | **Variable-length splits**: the racer line carries exactly as many splits as the run has zones, up to the 255 the zone byte bounds; a network race refuses a longer course with a reason. Danger Zone races keep their size. |
| 2 | Whether the race board shows a racer's penalty count | **Times only.** Penalty seconds are already inside every reported time; no count goes on the wire or the board. The solo HUD keeps its own penalty line. |
| 3 | When the host's course reaches a guest | **In the lobby**: a new reliable lobby message (chapter, name, hash, JSON), sent when the host picks a course and to each guest that joins. The launch builds from that copy. It has a size limit, and a course over it cannot be picked for a network race. |
| 4 | Whether a guest keeps the host's course | **Save if absent**: write it to `<dataRoot>/Courses/<chapter>/<name>.json` only when no file of that name exists there; a same-named file is never overwritten. |
| 5 | Whose pylon cuts the drawn course shows with several seats | **Local seats, shared**: each machine draws the cuts of the seats it flies (every split-screen pane and its bots, never a remote pilot); a pylon stands whole again when the seat that cut it reruns. |
| 6 | How a race bot flies a course gate | **Synthetic ribbons**: per sequence entry, a short `DangerZoneRibbon.FromPolyline` from before the gate, through its opening along the entry's direction, to past it, ridden by the existing `FlyDangerZone` run. A moving gate's ribbon follows the gate's live pose (`run.GateMotion`) each step. |
| 7 | Whether a bot's skill tier changes anything more on a course | **Same as the Danger Zones**: the tier's cruise throttle and approach spread carry over; further tuning is a `type:tuning` issue after the bots fly. |
| 8 | Whether race bots fire on a course | **Shoot blockers**: run the Danger Zone blocking-structure cast along each synthetic ribbon and shoot what it strikes. |
| 9 | Instant Action layout once Course and Race Bots both show | **Race Bots beside Race Time** on the first free line, the Course box alone on the second. The user judges the look from a screenshot. |
| 10 | The network lobby's course choice | **A Course dropdown in the host's Mission Options**, shown for Stunt Race in a chapter with courses: Danger Zones first, then shipped and game-folder courses by title, any over the size limit left out. Guests see the host's pick read-only, by title. The user judges the look from a screenshot. |
| 11 | How a course bot gets onto an entry's line when gates stand 250 to 400 m apart, closer than the Danger Zone approach reaches (B11) | **Join at once**: a bot within the 105 m lock range of the line, at least 150 m short of the opening and heading within 30 degrees of the line, rides it from where it is (`AiPilot.RejoinDangerZone`) instead of coming round to its start. Without it, two ace bots cleared 2 and 5 of `C2/Hollywood_Track_1`'s 19 entries in a 16-minute window. The 150 m is remake-only. Approved by the user. |

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

### Wave A, the course on the wire

1. ☑ The host's lobby sends its course to every guest, and each machine builds the run from that copy (#277)
2. ☐ The network race carries a course run: variable-length splits, penalty times, score keys (#277)

### Wave B, course bots

11. ☑ Stunt race bots fly a course's sequence on synthetic ribbons, through moving rings (#277)

### Wave C, open the gates

21. ☐ `--course=` with a second local pilot and with race bots; pylon cuts per local seat (#277)
22. ☐ `--course=` in a network session, and the Course dropdown in the lobby's Mission Options (#277)
23. ☐ Instant Action offers the Course box with more seats and bot rows (#277)

## Dependency and parallelism notes

A1 blocks A2 and C22. B11 blocks the bot half of C21 and C23. C21, C22 and C23 each lift one of
the gates, so each lands only after the work behind it: a gate opened early flies a course the
guests never received, or hands bots a course their steering cannot fly. A1/A2 (`Net/`, the lobby's
message plumbing and `Session/World/NetRaceLink.cs`) and B11 (`Session/World/StuntRaceBots.cs`, the
bot objective and `Flight/Modes/DangerZoneRibbon.cs`) touch disjoint files and can run in parallel
worktrees. C21, C22 and C23 all edit `SessionSpec.CourseRefusal`; run them in sequence, never in
parallel.

---

# Wave A, the course on the wire

## A1 ☑ The host's lobby sends its course to every guest, and each machine builds the run from that copy

**Landed.** A Stunt Race host's course travels as `LobbyCourseMessage` (`0x6C`, reliable, host to
each guest: round, chapter, name, `Course.Hash`, the JSON whole; an empty name is the Danger
Zones), in `CSVM/src/Net/NetDogfightMessages.cs` over a counted UTF-8 field added to
`NetMessageWriter.cs`. `DogfightLobby.SetCourse` is the host's pick: a new round, the word sent
ahead of that round's options to every guest and to each one seated later, refused on a guest and
for a course `DogfightLobby.CourseFits` leaves out. `NetLobby.Course` holds a guest's word (a host
that advertises takes none), and `DogfightLobby.Course` is the guest's copy once
`DogfightLobby.CourseOf` parses it and the hash matches (`CourseRefusal` says why not); the launch
runs the same check. `LauncherContext.NetCourse` (wired by `NetFlight.WithNet` from
`DogfightLobby.CourseWord` on every machine of a lobby flight) reaches `SessionNet.HostCourse`, and
`GameSession.ResolveCourse` builds the run from that word in place of the library; a guest keeps
it through `CourseLibrary.SaveIfAbsent`, which never overwrites a same-named file. The limit is
`LobbyCourseMessage.MaxSize`, the header's 65535 bytes; the WebRTC data channel carries a
262140-byte payload and refuses a 262144-byte one, so the header is the binding cap on both
carriers. The network refusal in `SessionSpec.CourseRefusal` stands (C22 lifts it).

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A guest who lacks the host's course file, or holds a different file under the same name,
flies the host's course: the course reaches the guest in the lobby, the launch builds every
machine's run from the host's JSON, and a copy whose `Course.Hash` does not match is refused with a
reason. A guest with no file of that name keeps a copy in its game folder.

**Evidence (confidence: traced).** A course lives in one file, shipped
(`CSVM/data/courses/<chapter>/<name>.json`) or in the game folder
(`<dataRoot>/Courses/<chapter>/<name>.json`); the shipped `C2/Hollywood_Track_1.json` is 5352 bytes.
`GameSession.ResolveCourse` (`CSVM/src/Launch/GameSession.cs:1403-1440`) loads it from the local
library only (`CourseLibrary.ForLaunch(...).Load`) and builds the run with `StuntMission.ForCourse`,
setting `GateMotion` to `CourseMotion.Offset`. No message carries a course: the Dogfight lobby's
`DogfightOptionsMessage` (`CSVM/src/Net/NetDogfightMessages.cs:29`) is a fixed 12-byte payload of
environment, mission type and match rules, sent to each guest whenever an option moves, and the race
messages in `CSVM/src/Net/NetRaceMessages.cs` carry runs, lines, clock and calls. Games run over
`EnetTransport` or `WebRtcTransport`.

**Approach.** A new reliable lobby message (chapter, name, `Course.Hash`, the course JSON), modelled
on `DogfightOptionsMessage` and its epoch, sent when the host picks a course and to each guest that
joins (Decision 3). The guest parses it with `Course.Parse`, checks it against the hash, and holds it
for the launch; `ResolveCourse` takes that course in place of the library load, and the rest of it
stays. A guest with no file of that name saves it through `CourseLibrary` to its game folder
(Decision 4). The message gets a size limit both transports deliver in one reliable message; a
course over it is not offered for a network race (C22 hides it).

**Model recommendation.** high: a new wire message across two transports and the lobby's join
ordering.

**Verify.** A loopback network suite where the guest has no copy of the host's course, and one where
it holds a different file under the same name: both fly the host's gates, the second's best lands
under the host course's hash, the first leaves a copy in the guest's scratch game folder, and the
second leaves the guest's own file unchanged. A message round-trip test at the size limit and one
byte past it. Tests read only `ctx.ScratchDir` as the game folder.

**⚠ Traps.**
- The file name is the course id everywhere; the optional `title` is display only. Key nothing on
  the title.
- `Course.Hash` covers the gate pool, the sequence and the start point, not the name or title. A
  best is keyed `course/<chapter>/<name>/<hash>/<plane>`, so two machines with different files
  under one name must not merge boards.
- Never overwrite a guest's file of the same name, whatever its content.
- Measure the size limit on `WebRtcTransport`, not only ENet: ENet fragments a large reliable
  packet, a WebRTC data channel may not.

## A2 ☐ The network race carries a course run: variable-length splits, penalty times, score keys

**Goal.** A network Stunt Race on a course shows every racer's splits, penalty-inclusive run times
and best-run board on every machine, for a sequence of up to 255 entries.

**Evidence (confidence: traced).**
- `NetRaceLink.Feed` (`CSVM/src/Session/World/NetRaceLink.cs:150-174`) and `StuntRace.Follow`
  (`CSVM/src/Flight/Modes/StuntRace.cs:348-360`) work off any `StuntMission`. `ForCourse`
  (`CSVM/src/Flight/Modes/StuntMission.cs:302-339`) builds one zone per sequence entry, so a course
  run already reports as a run of N zones.
- `RaceStandingMessage.MaxZones` is 24 and its `Size` is fixed at `HeaderBytes + 20 + 4 * MaxZones`
  (`CSVM/src/Net/NetRaceMessages.cs:214-217`); the racer line carries one split per zone. `Course`
  has no sequence cap (`CSVM/src/Flight/Modes/Course.cs`).
- A run report's zone byte is clamped to `RaceRunMessage.NoZone = 0xFF`
  (`NetRaceLink.cs:203`, `NetRaceMessages.cs:72`), so 255 entries is the bound.
- `AddPenalty` adds its seconds into `Elapsed` (`StuntMission.cs:539`), and the finish report sends
  `run.Elapsed` (`NetRaceLink.cs:173`), so penalty time is already inside every reported time.

**Approach.** Make `RaceStandingMessage` variable-length: a split count, then that many splits
(Decision 1). A network race refuses a course of more than 255 entries with a reason. No penalty
field (Decision 2). Check that each machine's best records under `ScoreStore.KeyFor`'s course key and
that `StuntCapture` stays off on the network path. Update `docs/org/multiplayer-messages.md`
("Stunt race") for the new layout.

**Model recommendation.** high: a wire format change that every race message test pins.

**Verify.** `RaceStandingMessage` round-trip tests at 0, 24, 25 and 255 splits and a malformed count.
A loopback network race suite on a course of more than 24 entries: every split stands on the guest's
board, a finish with a touch penalty reads the same time on both machines, and both record under the
course key. The existing Danger Zone network race suites stay green.

**⚠ Traps.**
- Penalties need no new wire field for the times to be right.
- Course runs never take Danger Zone photographs and never record under a Danger Zone key. Keep both
  true on the network path.

# Wave B, course bots

## B11 ☑ Stunt race bots fly a course's sequence on synthetic ribbons, through moving rings

**Landed.** `StuntMission.ForCourse` gives every sequence entry a `Route`: a straight line through its
gate's opening the way the entry is flown, from `EntryLeadInM` (250 m) short of the opening's centre
to `EntryRunOutM` (60 m) past it, both declared remake-only. `DangerZoneRibbon.ForEntry` builds it
`IsCourseEntry`, with an `Opening` and a `Drift` that reads its own run's `GateMotion` at
`SimSeconds()` on every point, so a ring's line moves with the ring; each run, a `ForAnotherPlayer`
copy included, lays its own lines. `StuntBotObjective` takes only the first open entry when its line
is a course entry's, always from the first vertex (`EndFrom`), and `RunsFrom` gives such a line no
row, so the table never offers a far end. Course gates stand closer than the Danger Zone cone's
reach, so a bot past a line's start also joins it at once (`StuntBotStart.Join`,
`AiPilot.RejoinDangerZone`) when it is within lock range of the line, at least `JoinRailM` (150 m,
remake-only) short of the opening and tracking along it; a course fluff passes over the gate. The way
an entry crosses its gate is `CourseGate.FlownWay`. `FlyDangerZone`, `Blocking`/`Targets`, `Rejoin`
and the order log read the line unchanged; `ReturnPose` finds the gate's centre on it, where a
moving ring stands rather than its rest. No reader of `StuntZone.Route`
outside the bots and `ReturnPose` exists: the HUD, marker, `StuntCapture` and `ScoreStore` key off
`run.Course`. Files: `Flight/Modes/DangerZoneRibbon.cs`, `Flight/Modes/StuntMission.cs`,
`Flight/Modes/Course.cs` (`CourseGate.FlownWay`, which the course lab reads too),
`Flight/Ai/StuntBotObjective.cs`, `Session/World/StuntRaceBots.cs` (the join's dispatch), suites
`stunt-race-bot-course` and `stunt-race-bot-course-blocker` in `Testing/StuntRaceBotSuites.cs`, unit
tests in `CourseRunTests` and `StuntBotObjectiveTests`, docs `org/aiPilot.md` "Stunt race bots" item 7.
For C21: the launch seats bots on a course once `SessionSpec.CourseRefusal` stops refusing
`VsBots`; nothing else is needed, since `StuntRaceDirector.Wire` builds `StuntRaceBots.For` from the
seats' course runs. The two suites seat their bots past the refusal by reflection in `CourseRace`,
which C21 replaces with the plain `bots:` argument.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A race bot on a course flies the next sequence entry through its gate the way the entry
names (a reverse entry from the other side), keeps up with a moving ring, shoots what blocks its
line, and reruns while the window is open, as it does on the Danger Zones.

**Evidence (confidence: traced).** `StuntRaceBots` (`CSVM/src/Session/World/StuntRaceBots.cs`)
builds its run table from each zone's `Route`, a `DangerZoneRibbon` (`StuntBotObjective.RunsFrom`,
line 98), starts a zone with `pilot.FlyDangerZone(order.Ribbon!, ...)` (line 148), finds blocking
structures by casting along the route (`Blocking`, line 207), and rejoins a route after a crash
(`Rejoin`, line 355). `ForCourse` builds course zones with no `Route` and a one-point marker gate
that nothing can cross (`StuntMission.cs:323-335`). `DangerZoneRibbon.FromPolyline`
(`CSVM/src/Flight/Modes/DangerZoneRibbon.cs:73`) builds a ribbon from any two or more points.
`AiPilot.FlyDangerZone` (`CSVM/src/Flight/Ai/AiPilot.cs:362`) starts a `DangerZoneRun` on a ribbon.
A gate's motion is `CourseMotion.Offset(gate, simSeconds)`, which the run's `GateMotion` already
holds.

**Approach.** Per sequence entry, a synthetic ribbon from a point before the gate, through its
opening along the entry's direction (reversed for a reverse entry), to a point past it (Decision 6).
Give each course zone that ribbon as its `Route`, so `RunsFrom`, `FlyDangerZone`, `Blocking`,
`Rejoin` and the order log work unchanged, and the order of play comes from `run.CompletedCount`
since only the next entry scores. A moving gate's ribbon follows the gate's live pose through
`run.GateMotion` at sim time on each step of the run. Skill tiers are unchanged (Decision 7). Bots
shoot blockers along the ribbon (Decision 8). The lead-in and run-out lengths are not game values:
mark them as such where they are declared, and file a `type:tuning` issue if they need tuning after
the bots fly.

**Model recommendation.** high: new behaviour on the AI pilot's ribbon run, with moving geometry.

**Verify.** A headless bot suite on `C2/Hollywood_Track_1` with race bots: each bot completes runs
in sequence order and reruns, a reverse entry is crossed from its named side, and a moving ring is
crossed. A suite course with a weapon-hit structure on a ribbon: the bot shoots it. The Danger Zone
bot suites stay green.

**⚠ Traps.**
- The course zones' `GreenGate`/`RedGate` are a one-point marker for `ReturnPose`; a gate test built
  on them finds nothing to cross. Scoring stays the aperture's.
- Read the moving gate at sim time through `run.GateMotion`, never wall time; the drawn gate and the
  scoring aperture use the same function at the same time.
- A Danger Zone bot may start either end of a zone; a course entry has one direction. The run table
  must not offer the far end.
- `Route` on a course zone is new; check every reader of `StuntZone.Route` (the HUD, the marker, the
  capture) still treats a course run as a course run.

# Wave C, open the gates

## C21 ☐ `--course=` with a second local pilot and with race bots; pylon cuts per local seat

**Goal.** `--stunt --course=<chapter>/<name>` launches with `--players>1` and with `--vs-bot`/
`--vs-bots`, every seat flying its own run of the course in one race, and the drawn pylons show the
cuts of every local seat.

**Evidence (confidence: traced).** The refusals are `SessionSpec.cs:2912-2913`. Per-seat runs exist:
`StuntMission.ForAnotherPlayer` (`StuntMission.cs:356-375`) copies a course run with its `Course`,
apertures, `GateMotion` and `SimSeconds`, and `GameSession` builds the session's run from `_courseRun`
(`GameSession.cs:2056`). The drawn course follows one run only:
`CourseVisuals.Build(course).Follow(_courseRun)` (`GameSession.cs:2061`).

**Approach.** Drop the two refusals; the bot half lands after B11. `CourseVisuals` follows every
local seat's run: a touch by any of them cuts the pylon, and a seat's rerun restores only the pylons
that seat cut (Decision 5).

**Model recommendation.** medium: two refusals and one visual's bookkeeping.

**Verify.** A split-screen course race suite and a bot course race suite, each recording bests under
the course key. A `CourseVisuals` test where seat 1 cuts a pylon, seat 0 reruns and the pylon stays
cut, then seat 1 reruns and it stands.

**⚠ Traps.** `--vs` stays refused: it is a Dogfight.

## C22 ☐ `--course=` in a network session, and the Course dropdown in the lobby's Mission Options

**Goal.** A host launches a network Stunt Race on a course, from the command line or by picking it
in the lobby's Mission Options, and its guests see the pick and fly it.

**Evidence (confidence: traced for the gate, lead-only for the lobby layout).** The network refusal
is `SessionSpec.cs:2914`. The lobby's Mission Options travel as `DogfightOptionsMessage`
(`NetDogfightMessages.cs:29`). <TODO: the Mission Options screen and its free space, read before
placing the dropdown.>

**Approach.** Drop the network refusal after A1 and A2. Add a Course dropdown to the host's Mission
Options for Stunt Race in a chapter with courses: Danger Zones first, then shipped and game-folder
courses by title, any over A1's size limit left out (Decision 10). A pick sends A1's message. A
guest's lobby shows the host's pick read-only, by title. Place it as Instant Action's remake-only
boxes are placed.

**Model recommendation.** high: lobby UI over the network with the host/guest split.

**Verify.** A loopback network launch suite from the lobby on a shipped course, and the guest's
lobby showing the title. A screenshot of host and guest Mission Options for the user to judge.

**⚠ Traps.** The lobby shows the course title, but the id it sends is the file name.

## C23 ☐ Instant Action offers the Course box with more seats and bot rows

**Goal.** Instant Action's Stunt Flying shows the Course box with more than one seat or with bot
rows, and the launch flies the picked course with all of them.

**Evidence (confidence: traced).** `OffersCourse` requires `seats == 1 && bots == 0`
(`InstantActionFeature.cs:365-366`); `LaunchCourse` and `BuildExit` (line 626) follow it, and
`OriginalInstantActionScreen.CourseShown`/`CourseFlown` (lines 596, 599) read it. Race Bots and Course
share the second free line today (`RaceBotsBox`/`CourseBox`, both `RemakeBox(screen, 1, …)`, lines
968-970); the shipped layout has two free lines (`ClearLine`, line 988). `MenuInstantActionSuites`
pins the solo rule (`CourseWithdrawn`, line 313: a bot row hides the Course box).

**Approach.** After C21, relax `OffersCourse` to Stunt Flying in a chapter with courses. Move Race
Bots beside Race Time on the first free line, and the Course box alone on the second (Decision 9).
Rewrite `CourseWithdrawn` to assert the opposite and update `OffersCourse`'s summary.

**Model recommendation.** medium: a gate, a layout move and the suite that pins it.

**Verify.** `MenuInstantActionSuites` with two seats and a bot row: the Course box shows, and the exit
builds the session spec `--stunt --course= --players=2 --vs-bot` builds. A screenshot of the page
with Race Time, Race Bots and Course all shown, for the user to judge.

**⚠ Traps.** A course launch takes no waves, wingmen or lives (`CourseFlown`); that holds with more
seats too.
