# Air race courses

**ACTIVE PLAN** (written 2026-11-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

A remake-only air race in the style of the Red Bull Air Race: a fixed, ordered course of course
pylon pairs on the ground and balloon-held rings in the air, flown solo against the clock. It is a
new course source for the existing Stunt Flying machinery (run clock, objective marker, rerun,
`ScoreStore`), not a separate mode. Courses are authored per chapter in a new freecam lab, saved one
file per course, and picked from a Course option under Instant Action > Stunt Flying.

Out of scope: multiplayer Stunt Race and its bots on authored courses (they keep flying Danger
Zones; a follow-up issue records the work), single turn pylons flown around on a side, tilted rings,
a ghost of the best run, and an in-editor test-fly hand-off. No original-game data is involved
beyond the chapter worlds: the course elements are procedural and the courses are authored content.
No `backlog` issue feeds this plan. The whole design rests on a grilling (2026-11-01) and is
`lead-only` except where an Evidence line cites a path and line.

## Milestone goal

- A course file format with two sources, shipped (`CSVM/data/courses/<chapter>/<name>.json`) and
  the game folder (`<dataRoot>/Courses/<chapter>/<name>.json`), the game folder winning on a name
  clash.
- A strict-order course run with a flying start, directional gates, gates reused in the sequence,
  and a +3 s debounced touch penalty, timed and scored through the Stunt Flying machinery.
- Procedural course pylons, rings and balloons that ripple, bob and sway, with a pylon that slices,
  sags and regrows when cut.
- A course editor lab: place, adjust, gizmo-move and order gates, and save.
- A Course option in Stunt Flying listing "Danger Zones" plus the chapter's authored courses.

**Authored courses are solo only.** Multiplayer needs the course carried host to guest (the file may
exist in one player's game folder only) and bots taught the sequence; both are a follow-up.

## Decisions (2026-11-01)

| # | Question | Decision |
|---|---|---|
| 1 | Who races in the first version | **Solo time trial**, the Red Bull format; best time per course stored locally |
| 2 | New mode or a course source | **A course source for Stunt Flying**; its clock, marker, rerun and score store are reused |
| 3 | Course elements | **Pylon pair and balloon ring**, both polygon fly-throughs; no single turn pylons |
| 4 | Gate order rule | **Only the next sequence entry counts**; skipping one means turning back, the clock is the penalty |
| 5 | Hitting a pylon, rim or balloon | **No collision, +3 s penalty**, the inflatable-pylon rule |
| 6 | Penalty counting | **+3 s per touch, debounced**: one touch until the plane leaves that element's volume; pylons, rims and balloons count alike |
| 7 | Element visuals | **Procedural primitives**, animated |
| 8 | Pylon cut | **Slice at the crossing height, top tips and falls and vanishes on landing, stump sags, regrows over about 3 s**; a dithered fade of the top was rejected as too dithery |
| 9 | Idle animation | **Everything ripples**: pylons like fabric, balloons and rings bob and sway; the ring's scoring polygon moves with the visible ring |
| 10 | Course storage | **Shipped `CSVM/data/courses/`** (written from the dev build) **and `<dataRoot>/Courses/`** (written from an exported build); no `user://` source |
| 11 | File granularity | **One file per course**, `<chapter>/<name>.json`, so a course is shared by copying one file |
| 12 | Courses per chapter | **Several, named** |
| 13 | Editor entry | **Shift+F3 under `--freecam`, plus `--course-edit=<chapter>[/<name>]`**; no menu entry |
| 14 | Placement | **Click the ground to place, sliders to adjust, and a 3D gizmo** |
| 15 | Gate orientation | **Heading only**; pylons and rings hang vertical. The gizmo is three move arrows and one yaw ring |
| 16 | Ordering | **List panel plus a 3D path**; a placed gate can be added to the sequence again with a direction choice, so a course can turn around and fly back |
| 17 | Start and clock | **Flying start** at an authored start point; the clock starts on crossing the first entry and stops on the last |
| 18 | Test loop | **Save, then `--stunt --course=<chapter>/<name>`** or the menu |
| 19 | Menu | **A Course option in Stunt Flying**; chapters with authored courses become eligible without Danger Zones (C1C, C2B) |
| 20 | Bests | **Keyed by chapter, course name, plane and a hash of the layout and sequence**; stored time includes penalties, with the penalty count beside it |
| 21 | Race HUD | **Next gate and penalties**: marker on the next entry, `Gate n/N`, clock, penalty total, a "+3 s" flash, a direction chevron on the next gate, the gate after it dimly highlighted |
| 22 | Multiplayer and bots | **Out of this plan**, filed as a follow-up |
| 23 | Name clash between the two sources | **The game-folder course wins**, as HD packs override (assumed, not asked) |
| 24 | Position storage | **Absolute world coordinates**; a pylon over water stands on the sea surface (assumed, not asked) |
| 25 | Default element sizes | **TUNE at the controls**; these planes are larger than Red Bull racers (assumed, not asked) |
| 26 | Terms | **"Pylon" already means a weapon hardpoint** (`MarkerKind.Pylon`, `CSVM/src/Mech3/MarkerRig.cs:62`); the new elements get distinct terms in `CONTEXT.md` |

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

### Wave A, course model and run

1. ☑ Course file format, loader for both sources, content hash, terms
2. ☑ Gate apertures from course gates, with a directional crossing test
3. ☑ Strict-order course run with a flying start
4. ☑ Touch penalty, +3 s debounced, no collision
5. ☑ Course bests keyed by layout hash, with penalty count
6. ☑ `--course=<chapter>/<name>` launch under `--stunt`

### Wave B, look and HUD

11. ☑ Procedural course pylons, rings and balloons with idle animation
12. ☑ Pylon cut: slice, sag, regrow
13. ☑ Race HUD: next gate, `Gate n/N`, penalties, wrong-side cross

### Wave C, course editor lab

21. ☑ Editor lab shell: Shift+F3, `--course-edit`, load and save targets
22. ◐ Click-to-place, adjustment sliders, start point
23. ☐ 3D gizmo: three move arrows and a yaw ring
24. ☐ Sequence panel, gate reuse with direction, world numbers and path line

### Wave D, menu and follow-up

31. ☐ Course option in Stunt Flying
32. ☐ File the multiplayer and bots follow-up issue

## Dependency and parallelism notes

A1 blocks everything (every item reads the course model). A2 → A3 → A4 is a chain through the run;
A5 and A6 need A3. B11 needs A1 only and can run beside A2..A6; B12 needs B11 and A4 (the touch
event drives the cut); B13 needs A3 and A4. Wave C needs A1, and C22 needs B11's meshes to show
what is placed; C21 → C22 → C23 → C24 is a chain in one lab file. D31 needs A1 and A6. D32 has no
dependency. File contention: A3, A4 and B13 all touch the Stunt run and HUD under
`CSVM/src/Flight/Modes/`, so do not run them in parallel worktrees; Wave C items share the lab file.

---

# Wave A, course model and run

## A1 ☑ Course file format, loader for both sources, content hash, terms

**Landed.** `CSVM/src/Flight/Modes/Course.cs` (the engine-free model, JSON reader and writer, and
layout hash) and `CourseLibrary.cs` beside it (the two sources, list, load, save), with
`CSVM.Tests/CourseTests.cs`. The home is `Flight/Modes/` beside `StuntMission.cs` and
`ScoreStore.cs`, the Stunt Flying machinery a course feeds. The file carries `version`, `start`
{`pos`, `heading`}, `gates` [{`id`, `kind` `pylons`|`ring`, `pos`, `heading`, `width` and `height`
or `diameter`}] and `sequence` [{`gate`, `dir` `forward`|`reverse`}]; positions are `[x, y, z]`,
headings yaw degrees on `SpawnPoint`'s convention, and chapter and name come from the path only. A
pylon gate's `pos` is its base midway between the pylons; a ring's is its centre. The hash covers
the pool in file order without ids, the sequence as pool indices with direction, and the start;
name, chapter and ids stay out of it. The game folder wins a clash, matched without case. **A
`--det` run reads no game folder** (`CourseLibrary.ForLaunch(dataRoot, det)`), as it reads no HD
pack; a suite builds its own `CourseLibrary` over `ctx.ScratchDir` folders. `CONTEXT.md` gains
course, course gate, pylon gate, course pylon, balloon ring and sequence entry. A malformed file is
rejected whole with a reason naming the field; an empty sequence parses, so the editor can save a
course in progress, and A3 must refuse to run one.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A course loads from `CSVM/data/courses/<chapter>/<name>.json` or
`<dataRoot>/Courses/<chapter>/<name>.json`, the game-folder file winning on a clash, and lists per
chapter; every course has a stable hash of its layout and sequence.

**Evidence (confidence: lead-only).** The shape is the grilling's: a pool of gates (kind pylon
pair or ring, absolute position, heading, size fields), a sequence of `(gate, direction)` entries in
which a gate may repeat, and a start point (position and heading). The data-root folders `HD` and
`HDModels` default in `Launcher.cs` (around lines 480 and 485, reported by an exploration agent, not
re-read). `--det` runs read neither HD folder unless a flag names it.

**Approach.** A pure C# course model and JSON reader/writer, a source scan over the two folders, and
a hash over the gate and sequence fields (not the name). Add the terms for the new elements to
`CONTEXT.md` (Decision 26). The JSON field names and the module's home are settled under
**Landed** above.

**Model recommendation.** A mid-tier model: a contained pure-C# module with unit tests and no
engine behaviour.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseTests"`
(round trip, a reused gate, the clash, hash stability, malformed files, `--det`), then the complete
unit tests and the battery. No engine path reads a course yet, so the battery shows only that
nothing else moved.

**⚠ Traps.** Tests never read `<dataRoot>` modding folders; a suite's courses go in `ctx.ScratchDir`
or behind an env var unset by default. A `--det` run reads no `<dataRoot>/Courses`, by analogy with
the HD folders.

## A2 ☑ Gate apertures from course gates, with a directional crossing test

**Landed.** `CSVM/src/Flight/Modes/CourseAperture.cs`, with `CSVM.Tests/CourseApertureTests.cs`;
`StuntGate` is unchanged. `new CourseAperture(gate)` builds the polygon once in the gate's own
frame (plane z = 0, forward -Z) as a `StuntGate`: a pylon gate's `Width` by `Height` rectangle from
its base, or a balloon ring's 32-gon of its `Diameter` about its centre. `CrossedBy(from, to,
direction, motion, out t)` carries the world segment into that frame through `(Rest * motion)`'s
inverse, refuses motion the wrong way along the forward, and runs `StuntGate.CrossedBy` there; `t`
is the fraction along the world segment, which an affine map keeps. `Rest` is the authored
transform (heading about Y at `Position`). `motion` is the gate's idle motion as a `Transform3D` in
its own frame, `Transform3D.Identity` at rest, so B11's motion function should return that offset
and draw the gate at `Rest * motion` too; A3 passes it each physics step at the sim time it draws
with. 32 sides: the inscribed polygon falls short of the circle by `1 - cos(180°/32)`, under 0.5 % of
the radius (5 cm on a 20 m ring), and the vertices are read only on a step that crosses the gate's
plane, so the count costs nothing per step. The aperture uses the pylons' centrelines; a plane
clipping a pylon body is A4's touch, not a miss here. Danger Zone runs keep the side-blind
`StuntGate.CrossedBy`, now pinned by a unit test.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Each course gate yields a scoring polygon (between the two pylons up to their tops; the
inside of a ring), and a crossing counts only from the side its sequence entry names.

**Evidence (confidence: traced for the existing test, lead-only for the rest).** `StuntGate` is a
planar polygon aperture with `Vertices`, `Center` and `Normal`
(`CSVM/src/Flight/Modes/StuntMission.cs:61-66`); `StuntGate.CrossedBy(from, to, out t)` at
`StuntMission.cs:70` is a segment-versus-plane plus point-in-polygon test, called per physics frame
with the last and current plane position (`StuntMission.cs:288-289`). Whether it already rejects a
crossing by side is not checked.

**Approach.** Build `StuntGate`s from course gates; approximate a ring with a polygon. Add the
direction check (sign of the motion along `Normal`) without changing Danger Zone behaviour. The
ring polygon's vertex count is settled under **Landed** above.

**Model recommendation.** A mid-tier model: a small pure-C# geometry module with unit tests and no
engine behaviour.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseApertureTests"`
(forward pass, reverse refused both ways, misses beside a pylon, above the tops, below the base and
outside the ring, a fast step, a turned heading, a bobbed and swayed pose, the Danger Zone gate
counting either side), then the complete unit tests. No engine path builds an aperture yet, so the
battery shows only that nothing else moved.

**⚠ Traps.** The ring polygon moves with the bobbing ring (Decision 9), so the aperture is rebuilt
or transformed per step; it must be driven by the sim clock, not wall time, or goldens and replays
move under load.

## A3 ☑ Strict-order course run with a flying start

**Landed.** A sequence mode inside `StuntMission`, not a separate run class:
`StuntMission.ForCourse(course)` builds a run with one `StuntZone` per sequence entry (`DzName`
`gateN`, `Description` `Gate N`, `PathName` the pool gate id, `Position` the gate's centre), so the
HUD, rerun control, summary, scoreboard, race events and `ScoreStore` callers read it through the
members they already use, and no consumer of the concrete `StuntMission` type (about forty files)
had to move to an interface. Danger Zone code paths are unchanged: every course rule is a branch on
`Course != null`. `Update` tests only entry `CompletedCount` through its pool gate's
`CourseAperture`, so a crossing of any other gate does nothing and one segment advances at most one
entry, which is what keeps a repeated gate from counting twice. The clock (`Tick`) stands until the
first entry is crossed, which also raises `RunStarted`; the last entry completes the run and stops
it as before. `CollectTargets` offers the next entry alone, so the objective marker already points
at it. A tap's `ReturnPose` is the last flown gate's centre heading the way it was flown (each zone
carries a one-point `StuntGate` marker nothing can cross, so the existing fallback path computes
it); with nothing flown it is the start. A rerun (`Reset`) starts again at entry 0. An empty
sequence is refused: `ForCourse` logs the reason and answers null, as `Load` does for a mission with
no zones. **Motion seam:** `GateMotion`, a `Func<int, double, Transform3D>` (pool index into
`Course.Gates`, sim seconds) defaulting to the identity, asked each step at `SimSeconds()`, which
defaults to `GameClock.Current.Time`, the session sim clock. **Penalty hook:** `AddPenalty(seconds)`
adds to a running clock and counts `PenaltyCount`; a run not started, complete or stopped takes
none, and `Reset` clears it. **Spawn:** no new path. A Stunt run spawns from the IA1 `ia.json`
spawn list through `SpawnPicker.ChooseSpawn`, on `SpawnPicker.StartState`'s throttle 1.0 and the
mission's `PLAYER_INIT` speed field (the Instant Action spawn rule, `docs/formats/spawns.md`); a rerun
and a tap with nothing flown go back to `FlightController._spawnPos`. A6 makes the spawn list
`[course.Start]`, which keeps all of that. The run is not reachable in-engine until A6 wires it.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The plane spawns airborne at cruise speed at the course's start point; only the next
sequence entry registers; the clock starts on the first entry and stops on the last; rerun respawns
at the start point.

**Evidence (confidence: traced for the existing run, lead-only for the rest).** `StuntMission` is
order-free: a zone completes once both its apertures are crossed, in any order
(`StuntMission.cs:103-104`, `:288-297`), and records a `CompletionOrder`. It fires per-zone, run
start, reset and finish events (`StuntMission.cs:136-148`) and offers `ForAnotherPlayer()`
(`:252`). Respawn and rerun live in `StuntRunControl.cs`; the Stunt clock today starts at spawn.

**Approach.** <TODO: the seam. Either a sequence mode inside `StuntMission` or a separate run class
raising the same events the HUD, rerun and score code consume; decide after reading those
consumers.> Danger Zone runs keep their order-free rule and spawn-started clock. The seam is
settled under **Landed** above.

**Model recommendation.** A mid-tier model: a contained change to one engine-free class with unit
tests; the session wiring is A6's.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseRunTests"`
(in-order advance, out-of-order and wrong-way crossings ignored, a repeated gate once per crossing,
the clock from the first entry to the last, reset to entry 0, an empty sequence refused, the motion
asked at the sim time, the next entry the only target, the tap's return, the penalty hook, an
independent copy), then the complete unit tests, whose Stunt tests show Danger Zone runs unchanged.
No engine path builds a course run yet, so the battery shows only that nothing else moved.

**⚠ Traps.** A gate repeated in the sequence must not register twice from one crossing.

## A4 ☑ Touch penalty, +3 s debounced, no collision

**Landed.** `CSVM/src/Flight/Modes/CourseElements.cs`, with `CSVM.Tests/CourseTouchTests.cs`; the
test runs from `StuntMission.Update`'s course branch after `CrossNextEntry`. No element is a
collider. The volumes are in each gate's frame at `Rest * GateMotion(poolIndex, SimSeconds())`, the
motion the crossing test asks, and every size is B11's `CourseGateShape`, the one the meshes read: a
course pylon is the solid tapered cone from `PylonBase(gate, side)`, `PylonBaseRadius` at the foot to
`PylonTopRadius` at Height; a ring rim the `TubeRadius` tube about the `TubeCentreline` circle (the
Diameter is the clear opening, the tube outside it); a balloon the sphere of `BalloonRadius`
stretched upright (local Y) by `BalloonStretch` at `BalloonCentre(gate, i)`, one per
`BalloonAnglesDeg` entry. The balloon's nearest point is taken in squashed space, a few percent of
its radius off the exact one. **Plane extent:** a sphere of `AircraftBody.BoundRadius`, the radius
about the body origin holding every `PlaneCollider` hull, which `FlightController` passes as
`Stunt.Update(_model.Position, Body?.BoundRadius ?? 0f)` (0, a point, where no body exists). Each step
does one bounding-sphere reject per gate, then each element's nearest point, with no allocation. An
element touches when the plane's centre lies within the sphere radius of its nearest point.
**Debounce:** one inside flag per element; a touch counts on the step the flag rises and again only
after it has fallen. **Penalty:** `AddPenalty(CourseElements.TouchPenaltySeconds)`, 3 s. **Event:**
`StuntMission.ElementTouched`, an `Action<CourseTouch>`; `CourseTouch(int Gate, CourseElement
Element, int Balloon, Vector3 Point, float PylonHeight, bool TimeAdded)`, with `CourseElement`
`LeftCoursePylon` (-X, left flying forward), `RightCoursePylon`, `RingRim`, `Balloon`. `Point` is
the world point of the element's surface nearest the plane, `PylonHeight` that point's height above
the course pylon's base. It fires before the first entry and on the finishing step too, with
`TimeAdded` false, so the cut still plays and the "+3 s" flash can stay off. A finished run tests
nothing, since `Update` returns first; a Danger Zone run never tests touches.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Course pylons, ring rims and balloons have no collision; each touch adds 3 s to the run,
counted once until the plane leaves that element's volume, and raises an event the cut effect and
HUD use.

**Evidence (confidence: lead-only).** Static world colliders are layer 1 and aircraft layer 2
(`CSVM/src/Flight/Airframe/CollisionLayers.cs`, reported by an exploration agent); a layer-1 element
would take the normal crash damage, which Decision 5 rejects.

**Approach.** Create no collider; test the plane against each element's volume per physics step.
A3 landed the clock side: `StuntMission.AddPenalty(seconds)` adds to a running course clock and
counts `PenaltyCount`, so a touch calls it with 3 s and raises A4's own touch event.
The volume shapes and the plane's extent are settled under **Landed** above.

**Model recommendation.** A mid-tier model: engine-free geometry inside one class's course branch,
with unit tests.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseTouchTests"`
(a centre pass takes nothing, a long brush one 3 s, leaving and re-entering a second, two elements
in one step two, rim and balloon touches, a moving ring's volumes with its motion, the event's
element, point and height on a turned gate, no time before the first entry or on the finish while
the event fires, a C4 Danger Zone run tests nothing), then the complete unit tests and the Stunt
suites. No engine path builds a course run until A6, so the suites show only that nothing else moved.

**⚠ Traps.** The 3 s value is the Red Bull rule, not original-game data. Debounce per element, not
per run (Decision 6). The bounding sphere is pessimistic along the fuselage and above and below the
wings, so a plane passing a course pylon nose-first can touch earlier than its drawn wingtip would;
`AircraftBody.NearestShape` is the hull-exact upgrade if that reads wrong at the controls.

## A5 ☑ Course bests keyed by layout hash, with penalty count

**Landed.** `ScoreStore.KeyFor(run, chapter, mission, plane)` is the one key every recorder asks
(`HumanFlightAdapter`'s solo board and race seat, `InstantActionDirector`'s wrap-up summary): a
Danger Zone run keeps `chapter/mission/plane`, a course run's is
`course/<chapter>/<name>/<Course.Hash>/<plane>`, which keeps it off `<chapter>/IA1/<plane>`, the
Danger Zone key of the world it flies, whose best it would otherwise overwrite. The `course/` prefix is a namespace no Danger Zone key can take (those open with a chapter
id), so old score files load unchanged and need no migration. **Rename:** the key carries the name,
as Decision 20 says, so a renamed course starts a fresh board even though `Course.Hash` (which leaves
the name out, A1) is unchanged; an edit of the layout, sequence or start changes the hash and starts
one too. `Course.Hash`'s comment points at the key as where the name enters.
**Penalties:** `RecordIfBest(key, total, penalties)` writes `penalties` beside `best` and `date` when
given; `ScoreStore.PenaltiesOf(run)` is a course run's `PenaltyCount` and null for a Danger Zone run,
whose entry keeps its old shape; `GetPenalties(key)` reads it back. The best is `Elapsed`, which
already includes the penalty time. **Summary:** `StuntSummary.TotalLine()` ends a course run's total
with `(n penalties)`, drawn by `StuntSplits` and flattened by `Lines`; the comparison line is
unchanged and compares against the course key's best. The stored best's own penalty count is not
shown (B13 or D31 can read `GetPenalties`). **Photograph:** `StuntCapture.Update` returns at once on
a course run (`Course != null`). The Danger Zone camera is the original's feature tied to its `dzN`
markers; on a course it would write one PNG per gate into the player's `screenshots/stunts`. No
`StuntMission` change was needed.

**Verified.** <pending orchestrator run>

**Model recommendation.** A mid-tier model: one key function threaded through three recorders, a
guard in the camera, and a suite extension.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseScoreKeyTests"`
(the key's shape, a file round trip keeps it, a moved gate and a reversed entry change it, a rename
changes it with the hash equal, a course key is never the IA1 Danger Zone key, the penalty count and
the summary's total line, a C4 Danger Zone run's key and no penalties), then the complete unit
tests, then `.\scripts\RunTests.ps1 -Suite 'course-launch,stunt-scores-scripted,stunt-capture,stunt-race-time-attack' -SkipUnits -SkipGoldens`.
`course-launch` now launches with `--scores=` over a scratch file seeded with the plane's old-format
`C1/IA1/<plane>` best, adds one penalty after the first entry, and after the finish reads the file
back: the course key holds `Elapsed` and the penalty count, the Danger Zone entry loads unchanged
with no penalties, and the file holds those two keys only. It also swaps in a camera whose pane
accepts every request, each pass starting inside the 15 m camera radius: no request, no latch, no
`screenshots/stunts`. With the course guard disabled that check fails (5 requests, 5 latched).

**Original approach (kept for reference).**

**Goal.** A course best is stored per chapter, course name, plane and layout hash, including
penalties, with the penalty count beside it; editing a course starts a fresh board.

**Evidence (confidence: lead-only).** `ScoreStore.cs` keeps bests in `user://stunt_scores.json`,
keyed chapter, mission and plane (reported by an exploration agent, not re-read); `StuntSummary.cs`
shows a finished run against the stored best.

**Approach.** Extend the key for course runs; Danger Zone keys stay as they are. A course run is a
`StuntMission` whose `Course` is set (A3): `Course.Hash`, `Course.Chapter` and `Course.Name` give
the key, `Elapsed` already includes penalties, and `PenaltyCount` is the count. <TODO: the key
shape and whether old score files need a migration.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** Scripted runs and tests override the score store path; keep that true for course keys.

## A6 ☑ `--course=<chapter>/<name>` launch under `--stunt`

**Landed.** `--course=<chapter>/<name>` implies `--stunt` and flies the chapter's IA1 world, where
Stunt Flying flies, whatever `--chapter=` or `--mission=` say; the chapter is read case-blind.
`SessionSpec` keeps `Course` and `CourseRefusal`: a value without both parts, `--vs`, a second
pilot (`--players=`, a `--plane=` list), bots, `--net-host`/`--net-join` and `--stage=empty` each
refuse (Decision 22), and a mode that beats flight drops the course with a warning. `GameSession`
resolves it before the world build (`ResolveCourse`) through `CourseLibrary.ForLaunch(dataRoot,
det)`. A refusal, a missing or broken file (the library's error text) or an empty sequence is
logged as an error and quits with exit 1 rather than flying free, since the Danger Zones in its
place would pass for the course. The run is `StuntMission.ForCourse(course)` with
`GateMotion = (g, t) => CourseMotion.Offset(course.Gates[g], t)`, and it replaces
`StuntMission.Load` in the stunt block; the spawn list is `[course.Start]`, so the spawn, rerun and
respawn land on the start point through `SpawnPicker`, `--pos=` still first; and
`CourseVisuals.Build(course)` joins the session's world root. The sample is
`CSVM/data/courses/C1/sample_out_and_back.json`: from the first `stunt_flying` spawn (heading 90,
west), pylon gate `p1` 400 m out, ring `r1` 800 m out and 80 m right, pylon gate `p2` 1200 m out
and 60 m left, ring `r2` 1600 m out, then back through `r1` reversed. Pylons stand on the ground
the suite's ray reads (128 m and 136.5 m), and the rings' centres sit 50 m and 55 m above it.
Both library reads worked in the engine: `res://` listing and loading the shipped file, and a
game-folder file of the same name winning it. A course run's best key and its Danger Zone
photograph are A5's.

**Verified.** <pending orchestrator run>

**Model recommendation.** A mid-tier model: launch wiring along a seam A3 already cut, plus a
scripted suite.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~SessionSpecTests"`
(the course's world, every solo refusal, a malformed value, a mode that drops it), then the complete
unit tests, then `.\scripts\RunTests.ps1 -Suite 'course-launch,course-visuals,stunt-scores-scripted,stunt-respawn-tap-hold,stunt-start-count,stunt-target-cycle' -SkipUnits -SkipGoldens`.
`course-launch` (headless) reads the sample through both sources, launches
`--course=C1/sample_out_and_back --det`, checks the spawn on the start point, the drawn gates and a
rerun to the start, then places the aircraft short of each gate and steps it through: the second
entry's gate and the first gate backwards count nothing with the clock at 0, each entry then counts
in order, the clock starts within two steps of the first, each crossed gate's drawn node stands at
the pose its crossing is scored against (a ring away from rest), and the clock stops on the last.
⚠ A `--det` session's clock advances only in its frame callback, so the suite steps
`GameClock.BeginFrame` and the pilot's `SimStep` itself; `GameSession._PhysicsProcess` is a no-op
there.

**Original approach (kept for reference).**

**Goal.** `--stunt --course=<chapter>/<name>` flies that course in its chapter's world.

**Evidence (confidence: lead-only).** `--stunt` is `--fly` plus the mission's `ia.json` `dzones`
as objectives (`docs/cli.md:166`); Stunt Flying flies the chapter's `IA1` world.

**Approach.** <TODO: which mission world a course flies (IA1 assumed), and the flag's wiring and its
`docs/cli.md` bullet within the bullet cap.> The run side is A3's: in `GameSession`, where
`StuntMission.Load` builds `stuntZones`, a resolved course builds `StuntMission.ForCourse(course)`
instead (null for an empty sequence, already logged: fly free or refuse the launch), and B11's
motion is bound as `GateMotion = (g, t) => <B11's function>(course.Gates[g], t)`. The spawn list
`_spawnPicker.LoadSpawnList` answers is replaced by `[course.Start]` with spawn base 0, so
`ChooseSpawn`, `StartState` (throttle 1.0, `PLAYER_INIT` speed), the rerun count's walk and every
respawn land on the start point through the existing path.

**⚠ Traps.** `--vs` beats `--stunt` by fixed precedence (`docs/cli.md:167`); define what `--course`
does with `--vs` or `--players`.

# Wave B, look and HUD

## B11 ☑ Procedural course pylons, rings and balloons with idle animation

**Landed.** The user accepted the look from the C1 montage, sizes and colours to be tuned in flight. `CSVM/src/Flight/Modes/CourseMotion.cs` is the motion: `Rest(gate)` is
the gate frame A2 scores in (heading yaw, local -Z forward, the flown plane at local z = 0),
`Offset(gate, seconds)` the motion in that frame (a ring bobs 1.0 m over 6 s, drifts 0.6 m in its
own plane and rolls 2 degrees about its axis over 8.5 s; a pylon gate is the identity), and
`Pose = Rest * Offset`. Phases hash from the gate id (FNV-1a plus murmur3's finaliser, so `g1` and
`g2` do not move together). `CourseVisuals.cs` is the Node3D builder: `Build(course, withStart)` makes
one node per pool gate, in pool order, and `Pose(seconds)` stands each at `CourseMotion.Pose`, which
its `_Process` does from `GameClock.Current.Time`. Every dimension is in `CourseGateShape.cs`, in
the gate frame, for the meshes and A4's touch volumes alike. A pylon gate's course pylons stand at
local x = ±width/2, tapered cones (base radius 0.09 and top 0.025 of the height) banded red and
white. A ring's diameter is its clear opening, the striped torus (tube radius 0.03 of the diameter,
at least 0.5 m) lying outside it, under three balloons (radius 0.14 of the diameter) on tethers
(0.45 of the diameter) fanned at -40, 0 and 40 degrees. One shader
text lit by `csky_sun_rgb_at` and fogged as the faithful aircraft, casting no shadow; the ripple is a
vertex wave on `csky_time` (the sim clock's global uniform), in one `ripple()` function, and the
fragment reads the undisplaced local point `v_local`, so B12 adds its sag in `ripple()` and its clip
on `v_local.y`. Every size and colour is TUNE and the look is the user's: the `course-visuals` suite
writes a C1 montage (close pylon gate, close ring, wide with the aircraft, a ring one second apart and
the pair's difference) to its scratch. Tests: `CSVM.Tests/CourseMotionTests.cs` and the suite, which
also pins node counts and the drawn pose against the function.

**Verified.** <pending orchestrator run>

**Model recommendation.** A mid-tier model: primitives, one shader and a pure function; the work
that needs judgement is the user's look call.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseMotionTests"`,
then `.\scripts\RunTests.ps1 -Suite course-visuals -SkipUnits -SkipGoldens` (windowed, writes
`course-visuals/montage.png` under the run's scratch), then the battery. The look closes with the
user's verdict on the montage.

**Original approach (kept for reference).**

**Goal.** Course pylons, rings and the balloons holding each ring render in every chapter from
Godot primitives; pylons ripple like fabric, balloons and rings bob and sway.

**Evidence (confidence: lead-only).** No ring or hoop mesh was found in the repo's decodes; barrage
balloons exist only as C3/C4 scenery (`docs/formats/anim-definitions.md`), so they are not reused.

**Approach.** Primitive meshes and one shader for ripple and bob, driven by the sim clock. The ring
transform used for scoring (A2) and the one drawn come from the same function. A4's touch volumes
(`CourseElements`) read `CourseGateShape`'s sizes, so a change there moves the drawn and touched
elements together. <TODO: the look
(colours, chequered bands, tether lines) is the user's judgement; show a montage before settling.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** No game assets in the repo. A vertex-shader-only bob would put the drawn ring off the
scoring polygon; Decision 9 requires them to agree.

## B12 ☑ Pylon cut: slice, sag, regrow

**Landed.** The user accepted the cut from the C1 montage, timings to be tuned in flight. `CSVM/src/Flight/Modes/CoursePylonCut.cs` is the cut as a pure function,
with `CSVM.Tests/CoursePylonCutTests.cs`; `CourseVisuals.cs` draws it. **Uniforms:** each course
pylon has its own two `ShaderMaterial`s on the pooled shader, set as `MaterialOverride` (per-node
materials, not instance uniforms: those live in the canonical block every CSVM shader shares, and a
course has a handful of pylons). The shader gains a clip window on the undisplaced height above the
base (`clip_lo_m`, `clip_hi_m`), `sag_m` and the falling top's `drop_m`, `tilt_rad` and
`tilt_pivot`, all at rest by default, so rings and balloons draw as before. **Time:** the uniforms are
written from C# by `Pose(seconds)`, on a change only, from the age since the cut on the session's
sim clock as a double, so `csky_time`'s 3600 s wrap never reaches them and the shader holds no time
logic for the cut. **Falling top:** a second, hidden `MeshInstance3D` on the same mesh, child of the
course pylon, clipped below the cut, tipped outward from the aperture (away from the plane in the
common inside brush) about the cut's outer edge and dropped under 9.81 m/s². It is hidden the moment
the drop reaches the cut height, its foot on the ground, at `sqrt(2 * cut / 9.81)` s (1.6 s for a
13 m cut); there is no fade and no ground settle. This replaces Decision 8's fade: the first montage
dithered the top away by screen-space noise, which the user judged "Fade too dithery", and chose a
top that just falls and vanishes on landing. The same-mesh alternative was rejected: the triangles across
the cut would stretch between the stump and the falling top. The stump is the course pylon's own
instance, clipped above its regrowing top and sagged in `ripple()` (down and slightly out, growing toward
its top). The shader now draws both faces, the inside at half light, so the cut reads as a hollow
tube; closed meshes draw as before. **TUNE:** `RegrowSeconds` 3.0 (whole from then on),
`HoldSeconds` 0.5 at the cut before regrowing (smoothstep to full height), `SagInSeconds` 0.3,
`SagShare` 0.15 of the stump, `TipSeconds` 1.2 for the tip (to `MaxTiltDeg` 70), `FallAccel`
9.81, which also sets when the top vanishes. A second cut mid-regrow is cut within what has regrown (`Recut`), and a touch
above the regrown stump drops nothing. **Wiring:** `GameSession` adds
`CourseVisuals.Build(course).Follow(_courseRun)`; `Follow` cuts on `ElementTouched` (course pylons
only, every touch, `TimeAdded` or not) at the run's `SimSeconds()` and stands every pylon whole on
`RunReset`, so a rerun meets every pylon whole at once (the open TODO, settled as reset). The
handlers only record; `Pose` draws on the next frame. The `course-visuals` suite brushes a run's
left course pylon and reads the uniforms back (the cut at the touch's height, the other pylon whole,
the top still falling 0.05 s before its landing and hidden 0.01 s after, regrowing at 2 s, still
short at 2.98 s, whole at 3 s, rims and balloons ignored, a second touch cutting again, a reset
whole mid-regrow), and writes `course-visuals/cut-montage.png` from a C1 flight: whole, then 0.5 s
(tipping), 1.53 s (falling, just before landing), 1.68 s (just after it vanished), 2.3 s and 3.1 s
after a cut at 13 m.

**Verified.** <pending orchestrator run>

**Model recommendation.** A mid-tier model: one pure function, shader uniforms and a suite; the
judgement is the user's look call on the montage.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CoursePylonCutTests"`,
then `.\scripts\RunTests.ps1 -Suite 'course-visuals,course-launch' -SkipUnits -SkipGoldens`
(course-visuals windowed, writing `course-visuals/cut-montage.png` beside B11's montage), then the
battery. The look closes with the user's verdict on the cut montage and in flight.

**Original approach (kept for reference).**

**Goal.** On a pylon touch the pylon is clipped at the crossing height, the top drops and fades,
the stump sags, and the pylon regrows over about 3 s.

**Evidence (confidence: lead-only).** The grilling's description of the Red Bull inflatable pylon.

**Approach.** Shader uniforms set from A4's touch event (cut height, cut time). The event is
`StuntMission.ElementTouched` (`CourseTouch`): a cut is an `Element` of `LeftCoursePylon` or
`RightCoursePylon` on pool gate `Gate`, sliced at `PylonHeight` (metres above that course pylon's
base, the contact point's height); the event fires whether or not `TimeAdded`, so read the sim clock
for the cut time. <TODO: how the
falling top is drawn (second instance or the same mesh clipped the other way).>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** The 3 s regrow time is TUNE. A rerun mid-regrow should meet a whole pylon <TODO:
confirm with the user>.

## B13 ☑ Race HUD: next gate, `Gate n/N`, penalties, wrong-side cross

**Landed.** The user accepted the HUD from the C1 montage after two reworks (no chevron, a thin red wrong-side cross, no intro line), sizes and colours to be tuned in flight. `StuntRunHud.cs` reads a course run (`Course != null`) in course words,
Danger Zone runs unchanged. The status line is `CourseStatusLine`: `0:14.2    GATE 2/5    +6 s`, the
clock, then the sequence entry to fly next counted from 1 (the last, `5/5`, once all are flown; the
next entry reads right where the marker points), then the penalty total when there is any
(`PenaltyCount` times `CourseElements.TouchPenaltySeconds`, already inside the clock). GATE is
upper case to match the line's ZONES. A course run has no intro line. A crossing flashes
`GATE 2  0:14.2` (`ClearedFlash`, the clock at the crossing) where a zone flashed `CLEARED`, and the
finish banner reads COURSE COMPLETE, the time and `PENALTIES +6 s` when there are any. Each
`ElementTouched` with `TimeAdded` shows a `PenaltyFlash`, "+3 s" in a penalty red on the line under
the clock (free, since a course is solo) for the same 1.6 s as the cleared flash, summed while it
shows ("+6 s"); a touch with no time added shows nothing. **Marker:** A3's `CollectTargets` already
offers the next entry alone and it draws, but its line 1 read " [] -" from the course zone's empty
label pair; `TargetPool` now passes an empty label as none, so the marker reads a blank line 1 and
`Gate 2` (and `Gate 2` / `7 o'clock` at the edge). **World cues**, in `CourseVisuals.Follow(run)`:
`Cross`, a red X (two closed bars corner to corner, 0.08 of the opening's lesser side wide and
0.6 m deep) across the next gate's clear opening, the rectangle between the course pylons'
centrelines or the ring's disc at 45 degrees, shown only while the aircraft is on that entry's
wrong side. **Wrong side:** the aircraft's position (`StuntMission.PlanePosition`, the last step's,
a read-only accessor added for this) in the gate's frame at its motion pose, measured from the
aperture's middle along the entry's flown way (local -Z forward, +Z reverse): more than 2 m past
the gate's plane and within 800 m shows it; once shown it holds until 2 m back on the right side or
beyond 1000 m (`WrongSide`), so a pass along the plane or at the range edge cannot strobe it. A new
entry, a respawn (`Relocated`) and a rerun start hidden. The bars are closed, so the cross is seen
from both sides while shown; nothing shows from the right side. `Highlight` fills the gate after
the next across its clear opening with a thin slab dithered to a quarter (`fade` 0.75). `Pose`
stands both at `CourseMotion.Pose * local` from the run's `CompletedCount`, so they ride a bobbing
ring; both hide once the run is complete, the fill when no entry follows. Both are on the course
shader with uniforms set, so no shader compiles mid-flight. All sizes and colours are TUNE.
**Montage seam:** a suite's single frame never runs the deferred redraw, and `CanvasItem.Draw*`
refuses outside `_Draw`, so `MarkerDraw.Text` and `TargetHud`'s box, tag and arrow now draw through
the canvas item's own `RenderingServer` calls, the same calls `Draw*` makes. **The user's verdict on
the first montage** (a yellow chevron on every next gate and a course intro line): "Remove the
arrow, the flow of the course should show the direction. Or only when the player is on the wrong
side of the gate. I don't think we need the explanation in the Hud. loading screen is enough." They
chose a cue only from the wrong side, then a red cross in place of the chevron, and kept the dim
fill. **Loading screen:** a `--course=` launch is a mode of ours, whose blackboard writes only the
mode heading (`LaunchBoards.Subject`, FREE FLIGHT for `--stunt`) and has no description line, so no
course line was added; an Instant Action Stunt Flying launch writes the original's own dialog texts,
the Danger Zone blurb, which D31 must replace or keep off a course launch. Not done here: the Danger
Zone photograph (`StuntCapture`) is A5's. The Evidence below named `UI/Overlays/MarkerOverlay.cs`,
which is the `--viewer` mount-marker overlay; the HUD's marker is `Flight/Hud/TargetHud.cs`.

**Verified.** <pending orchestrator run>

**Model recommendation.** A mid-tier model: string formatting, two world meshes on an existing
shader and a suite; the judgement is the user's look call on the montage.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseHudTests"`
(the status line, the course flashes, the "+3 s" flash's timing and sum, the wrong-side bands, the
marker's lines), then
`.\scripts\RunTests.ps1 -Suite 'course-hud,course-visuals,course-launch,stunt-scores-scripted,stunt-target-cycle' -SkipUnits -SkipGoldens`.
`course-hud` (windowed) reads the HUD text and both cues across every entry of a hand-made course:
the cross hidden from the right side, shown from the wrong side across the opening in the gate's
plane, held inside the band, hidden out of range, riding a bobbing ring, on a reverse entry's
wrong side, and gone at the finish and on a rerun; a touch before the first entry and one on the
clock. It then flies the shipped sample in a C1 `--det` session and writes
`course-hud/montage.png`: the HUD approaching entry 2 the right way (no cross, the fill on entry 3's
gate), the fill close, the "+3 s" flash after a brush outside entry 3's left course pylon (the
aircraft is then past that gate, so its cross shows), the reverse entry approached from its wrong
side with the cross, that cross from the side, and the reverse entry approached the right way. The
look closes with the user's verdict on the montage and in flight.

**Original approach (kept for reference).**

**Goal.** The objective marker and edge arrow point at the next sequence entry; the status line
shows `Gate n/N`, the clock and the penalty total; a "+3 s" flash shows on a touch; the next gate
carries a direction chevron in the world and the gate after it is dimly highlighted.

**Evidence (confidence: lead-only).** The Stunt HUD is `StuntRunHud.cs`; the marker and edge arrow
are `Flight/Hud/EdgeMarker.cs`, `MarkerDraw.cs` and `UI/Overlays/MarkerOverlay.cs` (reported by an
exploration agent, not re-read).

**Approach.** A3's seam: a course run is a `StuntMission` from `ForCourse`, one zone per sequence
entry. The next entry is `Zones[CompletedCount]`, its gate `Course.Gates` by `PathName` (the pool
id) with `Course.Sequence[CompletedCount].Direction`; `TotalCount` is the entry count and `Elapsed`
the clock. `CollectTargets` already offers only the next entry, so the objective marker follows
it. Until B13 the status line reads `ZONES n/N` and the flash `Gate n CLEARED`. The penalty total
is `PenaltyCount` touches of `CourseElements.TouchPenaltySeconds` each, already inside `Elapsed`;
the "+3 s" flash shows on `StuntMission.ElementTouched` when its `CourseTouch.TimeAdded` holds (it is
false before the first entry and on the finishing step). The rest is settled under **Landed** above.

**⚠ Traps.** `CONTEXT.md` lists "waypoint marker" under `_Avoid_` for the HUD off-screen marker; use
its term.

# Wave C, course editor lab

## C21 ☑ Editor lab shell: Shift+F3, `--course-edit`, load and save targets

**Landed.** `CSVM/src/UI/Labs/CourseLab.cs`, built by `InspectionLabs.BuildCourseLab` in `--freecam` over a
chapter world (not `--stage=empty`), with `CSVM/src/UI/Labs/CourseDraft.cs`, the engine-free edit model, and
`CSVM.Tests/CourseDraftTests.cs`. Shift+F3 was free: plain F3 is the flight gun selector, which no freecam reads.
`--course-edit=<chapter>[/<name>]` (`SessionSpec.CourseEdit`) implies `--freecam`, flies the chapter's IA1 world as
`--course` does, and opens the lab at launch on the named course, or a new one without a name or when the file is
missing. A broken file is reported and the lab starts `new_course`, so a save cannot overwrite it. The anim lab beats
it with a warning. The panel (right edge, as the ocean and vegetation labs) lists the chapter's courses with their
source, opens one, starts a new one from the name field with its start at the camera's pose, and saves.
**Save target:** `CourseDraft.SaveTarget(OS.HasFeature("editor"), library.GameFolder != null)`: the editor build
writes the shipped root (`CourseSource.Shipped`, `CSVM/data/courses/` through `GlobalizePath`), an exported build the
game folder (`<dataRoot>/Courses`). An exported `--det` run has no game folder, so Save is disabled with a label
saying why. An editor-build `--det` run still saves to the shipped root, since that root needs no game folder. The
status line shows the path written. `CourseLibrary.cs` is unchanged. **Drawing:** every edit raises
`CourseDraft.Changed`, on which the lab frees its `CourseVisuals.Build(course, withStart: true)` and builds it again,
plus an amber box round the selected gate (`SelectionService.NewBoxInstance`/`DrawBox`). The visuals hide with the
panel, and nothing is built before the first open. The world-triangle ground ray is the vegetation lab's, now built
once per session for both labs (`GameSession`).

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Shift+F3 under `--freecam`, or `--course-edit=<chapter>[/<name>]`, opens the course
editor; it creates, opens and saves courses, to `CSVM/data/courses/` from the dev build and to
`<dataRoot>/Courses/` from an exported build.

**Evidence (confidence: lead-only).** OceanLab and VegetationLab are Shift+F1/Shift+F2 under
`--freecam` and save only when `OS.HasFeature("editor")` (`OceanLab.cs:159`, reported by an
exploration agent); labs are wired in `CSVM/src/Launch/InspectionLabs.cs` and build no UI until used.

**Approach.** Follow the OceanLab pattern, but save in an exported build too, to the game folder.
Shift+F3 is settled under **Landed** above.

**Model recommendation.** A mid-tier model: a lab on an existing pattern, a launch flag and a scripted suite.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseDraftTests|FullyQualifiedName~SessionSpecTests"`
(the save target by build and game folder, `--course-edit` with and without a name, beaten by the anim lab, an empty
chapter refused), then the complete unit tests, then
`.\scripts\RunTests.ps1 -Suite 'course-lab,course-visuals,course-launch' -SkipUnits -SkipGoldens`. `course-lab`
(windowed; headless drops only its photograph) launches `--course-edit=C1/sample_out_and_back --det`, checks the lab
is open on the sample with its four gates drawn, swaps in a `CourseLibrary` over `ctx.ScratchDir` folders, and
continues as C22's Verify says.

**⚠ Traps.** No lab writes objects to a file yet, so there is no precedent to copy for the exported
save path.

## C22 ◐ Click-to-place, adjustment sliders, start point

**Landed (at the controls pending).** In `CourseLab.cs` and `CourseDraft.cs`. **Kind selector:** "Nothing (click
selects)", pylon gate (the default), balloon ring, start point (`CourseLab.Place`). **Click:** a left click the panel
does not take (`_UnhandledInput`, so it reaches the lab before the freecam's `SelectionService`) runs `ClickAt(screen)`.
It first selects the course gate whose drawn parts' boxes the camera ray meets, the nearest box centre winning, whatever
kind is armed. Off a gate it places the armed kind on the ground ray's hit, facing the camera: the heading is
`CourseDraft.HeadingOf(camera basis)`, the view's yaw on `SpawnPoint`'s convention (the screen's up when looking
straight down), so a gate placed from where the flyer would come is flown forward. With "Nothing" armed and no gate hit
the click is left to the freecam's selection, so the two never fight; the course visuals sit outside the world root the
selection walks, so a freecam click never picks them. **Water:** the ground ray is `VegetationGround.RayHit` over
`VegetationGround.Triangles`, which keeps every solid world polygon, the sea-level water sheet among them, so over open
sea a pylon gate's base lands on y = 0, the sea surface (Decision 24), with no separate sea lookup.
**Placing:** `Place` appends the gate with a forward sequence entry, so a course is flyable as placed, and selects it;
`PlaceStart` moves the start point. Ids are `p`/`r` and one past the highest number of that prefix in use (`NextId`).
**Defaults (TUNE, `CourseDraft` constants):** pylon gate `PylonWidthM` 40 by `PylonHeightM` 30, ring `RingDiameterM`
40 with its centre `RingAboveGroundM` 50 above the ground clicked (the sample's rings stand 50 and 55 m up), start
`StartAboveGroundM` 50 up. **Sliders** (selected gate, applied as they move): a pylon gate's width (5 to 200) and
height (5 to 120), a ring's height above ground (5 to 500) and diameter (5 to 200), and heading (-180 to 180) for
both. The ring stores world Y; the height slider writes `WithAboveGround(ring, v, ground)` with the ground under the
ring, sampled by a ray straight down (`GroundBelow`) and sampled again whenever the gate's X/Z moved. **Delete gate**
removes the gate and every sequence entry naming it (`CourseDraft.Delete`). **Drag:** holding H (no freecam tool reads
it; G is the graphics-mode toggle and M the mesh lab) moves the selected gate over the ground under the cursor on each
mouse motion (`DragTo`, `CourseDraft.MovedTo`): a pylon gate stands on the hit, a ring keeps its height above ground.
**Hooks for C23 and C24:** `CourseLab.Draft` is the seam. `Draft.Selected`/`SelectedId` is the selection,
`Draft.Update(gate)` moves or turns a gate, `Draft.Changed` fires after every edit and selection change, and the lab
redraws on it. The gizmo should call `Draft.Update` from its drag and claim its own clicks before `ClickAt`. The
sequence panel should add its reorder and reuse operations to `CourseDraft` beside `Place` and `Delete`, so every edit
still raises `Changed`. `CourseLab.Visuals.GateNodes[i]` is pool gate i's drawn node.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Left-click on the ground places the selected kind facing the camera; sliders set a pylon
pair's width and height, a ring's height above ground and diameter, and heading; the start point is
placed the same way.

**Evidence (confidence: lead-only).** WeaponLab already places by clicking a world surface (reported
by an exploration agent, not re-read).

**Approach.** The ground pick over water is settled under **Landed** above.

**Model recommendation.** A mid-tier model: pure edit functions with unit tests, and lab wiring through the calls a
scripted suite can drive.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~CourseDraftTests"` (ids, heading
from a camera yaw and from straight down, placement with its forward entry and selection, the ring's height above
ground stored as world Y, a move keeping that height and standing a pylon gate on the ground, a delete dropping every
entry naming the gate), then the complete unit tests, then the `course-lab` suite. From rays through known screen
points in the C1 freecam it places two pylon gates, a ring and the start point and checks each stands on the ground
on its click's ray at the camera's heading. It selects p1 and r1 by clicking them with a kind armed, sets the ring's
height-above-ground and diameter sliders through their `HSlider`s, and drags p1 and then the ring. Over open sea
(found by down rays at y within 0.25 m of 0) a click places a pylon gate at y 0. It deletes p2, saves to the scratch
shipped root (and checks nothing appeared in `CSVM/data/courses`), and reloads the file and reopens it through the
lab to the same JSON and hash. It writes `course-lab/lab.png`, the panel with p1 selected over the course. The
at-the-controls part closes with the user placing, dragging and saving a course in a real `--course-edit` session,
and the default sizes with their flight.

**⚠ Traps.** Default sizes are TUNE at the controls (Decision 25).

## C23 ☐ 3D gizmo: three move arrows and a yaw ring

**Goal.** The selected gate shows three move arrows and one yaw ring that drag it in the world.

**Evidence (confidence: lead-only).** Godot has no runtime 3D gizmo, so this is new code.

**Approach.** <TODO.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** Freecam already uses RMB look and left-click select; the gizmo's drag must not fight
them.

## C24 ☐ Sequence panel, gate reuse with direction, world numbers and path line

**Goal.** A side panel lists the sequence in order; entries move up and down, a new gate inserts
after the selected entry, a placed gate can be added again with a direction choice, and clicking a
row flies the camera to it. In the world, entry numbers float over the gates and a line joins them
in order.

**Evidence (confidence: lead-only).** The grilling.

**Approach.** <TODO.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** A gate used twice shows two numbers; deleting a gate removes every entry that uses it.

# Wave D, menu and follow-up

## D31 ☐ Course option in Stunt Flying

**Goal.** Instant Action > Stunt Flying offers a Course option: "Danger Zones" plus the chapter's
authored courses by name; chapters with authored courses but no Danger Zones become eligible.

**Evidence (confidence: lead-only).** `MenuChapters.For(mode)` returns the six chapters that ship
`dzones` for Stunt Flying; C1C and C2B ship none (`CSVM/src/UI/Menu/MenuChapters.cs`, reported by an
exploration agent). The menu contract is `docs/menu-presentations.md`; the launch payload is
`LaunchExit`.

**Approach.** <TODO: after reading `InstantActionFeature` and `menu-presentations.md`.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** The original local lobby also reads `MenuChapters`; authored courses stay out of
multiplayer (Decision 22).

## D32 ☐ File the multiplayer and bots follow-up issue

**Goal.** A `backlog` issue records what Stunt Race and its bots need to fly authored courses.

**Evidence (confidence: lead-only).** Stunt Race replicates through `Session/World/NetRaceLink.cs`
and `Net/NetRaceMessages.cs`; bots are `Session/World/StuntRaceBots.cs` (reported by an exploration
agent). A guest may not have the host's game-folder course file.

**Approach.** File the issue; no code.

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** The issue exists and is linked from the closing commit.

**⚠ Traps.** None known.
