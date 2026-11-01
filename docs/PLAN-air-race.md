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
| 8 | Pylon cut | **Slice at the crossing height, top drops and fades, stump sags, regrows over about 3 s** |
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
2. ☐ Gate apertures from course gates, with a directional crossing test
3. ☐ Strict-order course run with a flying start
4. ☐ Touch penalty, +3 s debounced, no collision
5. ☐ Course bests keyed by layout hash, with penalty count
6. ☐ `--course=<chapter>/<name>` launch under `--stunt`

### Wave B, look and HUD

11. ☐ Procedural course pylons, rings and balloons with idle animation
12. ☐ Pylon cut: slice, sag, regrow
13. ☐ Race HUD: next gate, `Gate n/N`, penalties, direction chevron

### Wave C, course editor lab

21. ☐ Editor lab shell: Shift+F3, `--course-edit`, load and save targets
22. ☐ Click-to-place, adjustment sliders, start point
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

## A2 ☐ Gate apertures from course gates, with a directional crossing test

**Goal.** Each course gate yields a scoring polygon (between the two pylons up to their tops; the
inside of a ring), and a crossing counts only from the side its sequence entry names.

**Evidence (confidence: traced for the existing test, lead-only for the rest).** `StuntGate` is a
planar polygon aperture with `Vertices`, `Center` and `Normal`
(`CSVM/src/Flight/Modes/StuntMission.cs:61-66`); `StuntGate.CrossedBy(from, to, out t)` at
`StuntMission.cs:70` is a segment-versus-plane plus point-in-polygon test, called per physics frame
with the last and current plane position (`StuntMission.cs:288-289`). Whether it already rejects a
crossing by side is not checked.

**Approach.** Build `StuntGate`s from course gates; approximate a ring with a polygon. Add the
direction check (sign of the motion along `Normal`) without changing Danger Zone behaviour.
<TODO: the ring polygon's vertex count.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** The ring polygon moves with the bobbing ring (Decision 9), so the aperture is rebuilt
or transformed per step; it must be driven by the sim clock, not wall time, or goldens and replays
move under load.

## A3 ☐ Strict-order course run with a flying start

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
consumers.> Danger Zone runs keep their order-free rule and spawn-started clock.

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** A gate repeated in the sequence must not register twice from one crossing.

## A4 ☐ Touch penalty, +3 s debounced, no collision

**Goal.** Course pylons, ring rims and balloons have no collision; each touch adds 3 s to the run,
counted once until the plane leaves that element's volume, and raises an event the cut effect and
HUD use.

**Evidence (confidence: lead-only).** Static world colliders are layer 1 and aircraft layer 2
(`CSVM/src/Flight/Airframe/CollisionLayers.cs`, reported by an exploration agent); a layer-1 element
would take the normal crash damage, which Decision 5 rejects.

**Approach.** Create no collider; test the plane against each element's volume per physics step.
<TODO: the volume shapes (capsule per pylon, torus distance for a rim, sphere per balloon) and the
plane's extent used.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** The 3 s value is the Red Bull rule, not original-game data. Debounce per element, not
per run (Decision 6).

## A5 ☐ Course bests keyed by layout hash, with penalty count

**Goal.** A course best is stored per chapter, course name, plane and layout hash, including
penalties, with the penalty count beside it; editing a course starts a fresh board.

**Evidence (confidence: lead-only).** `ScoreStore.cs` keeps bests in `user://stunt_scores.json`,
keyed chapter, mission and plane (reported by an exploration agent, not re-read); `StuntSummary.cs`
shows a finished run against the stored best.

**Approach.** Extend the key for course runs; Danger Zone keys stay as they are. <TODO: the key
shape and whether old score files need a migration.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** Scripted runs and tests override the score store path; keep that true for course keys.

## A6 ☐ `--course=<chapter>/<name>` launch under `--stunt`

**Goal.** `--stunt --course=<chapter>/<name>` flies that course in its chapter's world.

**Evidence (confidence: lead-only).** `--stunt` is `--fly` plus the mission's `ia.json` `dzones`
as objectives (`docs/cli.md:166`); Stunt Flying flies the chapter's `IA1` world.

**Approach.** <TODO: which mission world a course flies (IA1 assumed), and the flag's wiring and its
`docs/cli.md` bullet within the bullet cap.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** `--vs` beats `--stunt` by fixed precedence (`docs/cli.md:167`); define what `--course`
does with `--vs` or `--players`.

# Wave B, look and HUD

## B11 ☐ Procedural course pylons, rings and balloons with idle animation

**Goal.** Course pylons, rings and the balloons holding each ring render in every chapter from
Godot primitives; pylons ripple like fabric, balloons and rings bob and sway.

**Evidence (confidence: lead-only).** No ring or hoop mesh was found in the repo's decodes; barrage
balloons exist only as C3/C4 scenery (`docs/formats/anim-definitions.md`), so they are not reused.

**Approach.** Primitive meshes and one shader for ripple and bob, driven by the sim clock. The ring
transform used for scoring (A2) and the one drawn come from the same function. <TODO: the look
(colours, chequered bands, tether lines) is the user's judgement; show a montage before settling.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** No game assets in the repo. A vertex-shader-only bob would put the drawn ring off the
scoring polygon; Decision 9 requires them to agree.

## B12 ☐ Pylon cut: slice, sag, regrow

**Goal.** On a pylon touch the pylon is clipped at the crossing height, the top drops and fades,
the stump sags, and the pylon regrows over about 3 s.

**Evidence (confidence: lead-only).** The grilling's description of the Red Bull inflatable pylon.

**Approach.** Shader uniforms set from A4's touch event (cut height, cut time). <TODO: how the
falling top is drawn (second instance or the same mesh clipped the other way).>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** The 3 s regrow time is TUNE. A rerun mid-regrow should meet a whole pylon <TODO:
confirm with the user>.

## B13 ☐ Race HUD: next gate, `Gate n/N`, penalties, direction chevron

**Goal.** The objective marker and edge arrow point at the next sequence entry; the status line
shows `Gate n/N`, the clock and the penalty total; a "+3 s" flash shows on a touch; the next gate
carries a direction chevron in the world and the gate after it is dimly highlighted.

**Evidence (confidence: lead-only).** The Stunt HUD is `StuntRunHud.cs`; the marker and edge arrow
are `Flight/Hud/EdgeMarker.cs`, `MarkerDraw.cs` and `UI/Overlays/MarkerOverlay.cs` (reported by an
exploration agent, not re-read).

**Approach.** <TODO: after A3's seam is chosen.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** `CONTEXT.md` lists "waypoint marker" under `_Avoid_` for the HUD off-screen marker; use
its term.

# Wave C, course editor lab

## C21 ☐ Editor lab shell: Shift+F3, `--course-edit`, load and save targets

**Goal.** Shift+F3 under `--freecam`, or `--course-edit=<chapter>[/<name>]`, opens the course
editor; it creates, opens and saves courses, to `CSVM/data/courses/` from the dev build and to
`<dataRoot>/Courses/` from an exported build.

**Evidence (confidence: lead-only).** OceanLab and VegetationLab are Shift+F1/Shift+F2 under
`--freecam` and save only when `OS.HasFeature("editor")` (`OceanLab.cs:159`, reported by an
exploration agent); labs are wired in `CSVM/src/Launch/InspectionLabs.cs` and build no UI until used.

**Approach.** Follow the OceanLab pattern, but save in an exported build too, to the game folder.
<TODO: confirm Shift+F3 is free.>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

**⚠ Traps.** No lab writes objects to a file yet, so there is no precedent to copy for the exported
save path.

## C22 ☐ Click-to-place, adjustment sliders, start point

**Goal.** Left-click on the ground places the selected kind facing the camera; sliders set a pylon
pair's width and height, a ring's height above ground and diameter, and heading; the start point is
placed the same way.

**Evidence (confidence: lead-only).** WeaponLab already places by clicking a world surface (reported
by an exploration agent, not re-read).

**Approach.** <TODO: the ground pick over water (Decision 24 puts pylons on the sea surface).>

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** <TODO: not settled in the grilling.>

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
