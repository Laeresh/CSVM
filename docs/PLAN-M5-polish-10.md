# M5 Polish Run 10

**ACTIVE PLAN** (written 2026-09-02). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names the active plans when more than one is
present. Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to
[`plans.md`](plans/plans.md), when every item lands.

Ten player-visible defects in the delivered M1 to M5 game, selected from `backlog.md` on 2026-09-02
by the criteria the author approved that day: open, unblocked defects a player meets in normal play,
spread across themes rather than walked in mission order, with small high-value `[Tuning]` and
`[Feature]` fills. Excluded on the same day: everything `[Blocked]` (`BL-322` and `BL-335` on the
texture-header plumbing that `B12` here is the first half of, `BL-639` on `CAP-47`, `BL-284` on
`CAP-34`, `BL-413` on `BL-412`, `BL-033` on an SDL release, `BL-181` on a shared type scale,
`BL-380` on per-instance fog uniforms, `BL-314` on `PT-45`), everything `[Owed-playtest]` (the code
side is done and what is missing is a sitting, which `PT-97` to `PT-117` already owe), the
`[Research]` questions whose deliverable is an answer rather than a fix, everything hosted by
`LaunchMenu` or the campaign boards, `BL-603` (whose first step is flying the original), `BL-150`
(recorded plan-sized, not a polish item), and anything belonging to Milestone 6.

Two clusters stay in the backlog whole on their own recorded grounds. The AI-mode-machine group
(`BL-523`, `BL-550`, `BL-565`, and the `BL-558` research question beside it) is declined for a
fourth time, because runs 8 and 9 each recorded that splitting it wastes the shared decode; it wants
a dedicated run. `BL-672`'s hit-attribution question is left whole for the same reason: narrowing
the parent-chain climb needs a per-chapter census of which pools stop answering, which is a run of
its own rather than an item in this one.

**Each of the ten was re-verified still-open on 2026-09-02** against the record
(`git log --oneline --all --grep=BL-nnn`, which returned filings, mints and plan listings only, with
no landing for any of them), the current `backlog.md` entry, the live plans, and the worktree and
branch list. Eight were additionally re-read against the cited code in this session, and two of
those cites had drifted; the corrected `file:line` is in each item's Evidence below. `A1` and `C21`
were verified against the record and their entries but not re-read line by line, which their
Evidence lines say. Two entries gained a fact in that pass: `B13`'s calibration already exists for
the enhanced path and only the faithful path lacks it, and `C24`'s comparer is nested in
`AnimRuntime.cs`, not in `NameResolver.cs` where the entry looked for it. The scheduled entries were
moved out of `backlog.md` into this plan in the same change that created it.

This plan runs beside [`PLAN-menu-presentations.md`](PLAN-menu-presentations.md) and touches nothing
it owns: no `LaunchMenu`, no menu screens, no campaign board chrome. It also runs beside the open
closing sorties of [`PLAN-M5-polish-8.md`](PLAN-M5-polish-8.md) and
[`PLAN-M5-polish-9.md`](plans/PLAN-M5-polish-9.md), and that is a scheduling constraint rather than a file
one: see Decision 4. `A2` died at dispatch and its named first alternate `BL-405` was promoted into
its place as `C25`. Remaining alternates if another item dies early:
`BL-391` (an audio level the author has already reported), `BL-614` (the general form of a walk
`C21`'s neighbourhood measured).

## Milestone goal

- A downed zeppelin comes to rest on the sea instead of under it, its cannons see the hulls the
  original's turret picker sees, and a zeppelin on a scripted route flies its legs instead of
  cutting them.
- A surface that the original refuses to collide with stops stopping the player, and a surface the
  original refuses to light stops being modulated, both from the same decoded per-polygon and
  per-texture flags rather than from a fitted constant.
- An aircraft is lit by the brightness its mission authors, so a night mission and a day mission no
  longer light the same plane identically.
- The sonic ground burst reads as the flat ring the original draws, a damaged engine waits before it
  comes back, and the chase camera has the zoom axis the original's keybind page ships.
- A suite staging several airframes in one process stops throwing on a stale node.
- A closing sortie judges at the controls, theme by theme, every landed item whose acceptance needs
  eyes.

**No menu work, no AI-mode-machine work, and no Milestone 6 work.** Everything hosted by
`LaunchMenu` belongs to `PLAN-menu-presentations`; the patrol, pursue and lay-off cycle stays whole
for a dedicated run; networking does not exist yet and nothing here prepares for it.

## Decisions (2026-09-02)

| # | Question | Decision |
|---|---|---|
| 1 | Theme-spread defects, or the deferred AI cluster, or a render-fidelity run? | **Theme-spread player-visible defects**, run 8's shape, with small `[Tuning]` and `[Feature]` fills. The AI cluster stays whole for its own run, as three prior runs recorded. |
| 2 | May `[Research]` and `[Owed-playtest]` items enter the ten? | **No.** A `[Research]` item's deliverable is an answer, and an `[Owed-playtest]` item's is a sitting; both are already owed elsewhere. `A3`, `C23` and `C22` are the sanctioned `[Tuning]`/`[Feature]` fills. |
| 3 | Two global fidelity changes (`B11`, `B12`) in one run, or one? | **Both, in separate items and separate worktrees.** They are the run's only golden-movers and they answer the same shape of question (a decoded per-primitive flag the remake ignores), but they share no file: `B11` is collision, `B12` is materials. Every re-pin is user-reviewed. |
| 4 | Land before or after the two open closing sorties? | **Waves A, C and D land freely; `B11` and `B12` land after `PT-97` to `PT-117` are flown.** Those sorties judge landed campaign items at the controls, and a global collision or lighting change underneath them would be judged as part of what it is meant to confirm. |
| 5 | Does `B12` fix `BL-322`'s C5 facade ratio as well? | **No, and it must not be sold as doing so.** `BL-322`'s own premise is refuted (see the table below): the measured facades are on the lit side of both gates. `B12` lands the rule and the plumbing; whether the exempt overlays drawn on top of those facades move the measured ratio is the next question, not this one's success criterion. |
| 6 | Does `B13` reuse enhanced mode's SUNLIGHT mapping? | **Read it, do not copy it.** `WeatherRig.EnhancedEnergies` is a real precedent for turning authored `SUNLIGHT_DIFFUSE`/`AMBIENT` into Godot energies, but it was calibrated for a shaded world. The faithful path lights only the aircraft, so its own numbers are a fresh calibration against the author's eyes. |

## ⚠ Read this before implementing anything

Three of these items carry a claim that has already died once, and one carries a premise that dies
in its own entry. None of them is blame; each is a place a session would otherwise spend an hour.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | CM12's ace `hkfirebrand_9` dies because the AI mode machine flies it into terrain (`BL-566`). | `PLAN-M5-polish-9` `C23` disproved it three times over. The ram is our own collider: both tiles the ace meets are wholly single-sided (`g35052` 37/37, `tagged` 23/23) and the original backface-culls collision per polygon. That disproof is what filed `B11` here. |
| 2 | CM10's attack-balloon marker is placed wrongly and needs an offset or an altitude floor (`BL-656`). | `PLAN-M5-polish-9` `B14` disproved it: the marker tracks its geometry correctly and the geometry itself dips to the water. The anchor rule is the original's (`FUN_004cf2c0`, the midpoint of the node's active bounding box). The remainder is `BL-674`, an alternate here, and any fix that adds a constant to the marker is wrong by construction. |
| 3 | C5's lit facades are dim because the original exempts them from the world light (`BL-322`). | `PLAN-M5-polish-6` `A2` decoded the exemption and refuted the premise. `cblock1` to `7`, `bldg1` to `4` and `bldgtrim1` all ship storage flags `0xa5`, no alpha bit, so the original modulates them exactly as we do. What survives is the narrower rule `B12` implements, applied to the overlays drawn on top rather than to the facades themselves. |
| 4 | `BL-535`'s repeat-burst cost sits two bursts before the pool wrap, and `EnsureOpacityPath`'s shader scan is the bottleneck. | `PLAN-M5-polish-6` `B13` measured both away. The cost lands exactly at the wrap, and the scan is under 0.1 ms. `C21` inherits the corrected reading, and the general form of the walk is `BL-614`, an alternate here. |
| 5 | The zeppelin cannons' candidate set is too narrow and should be widened to the turret picker's whole `VehicleList` (`BL-667`, this plan's own `A2` as written). | Four read-only lenses killed it at dispatch. The broadside is not a turret and runs no candidate scan: it walks the record's authored `targets` node-name list, and the one `CollectAircraft` call only resolves the literal name `player` behind an `IsHumanPiloted` filter. Its mirror image, "a broadside should only target other zeppelins", is equally false and has now been refuted three times (`BL-517`, `BL-567`, and this pass): what keeps shipped broadsides off the player is the `COMPLETED_ZEPCANNONS` engage flag. See `A2`. |
| 6 | Our puffer blend verdict can be corrected by deleting the darkness rule (`BL-335`). | Its own entry records that deleting it alone makes the reported case worse, because `fire_n_smoke` dies on an unflagged sprite and reaches the right answer by the wrong route. `BL-335` is not in this run; `B12` must not be extended toward it opportunistically, because the blend rule needs a different header field and the depth sort is a separate delta again. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | `A3`, `B11`, `B12`, `B13`, `C22`, `C24`, `C25` | Confirm the trace, then implement. Each names a decoded function or a measured census. `C25` is the exception in one respect: its mechanism is decoded but the data question it turns on is not, so its first step is a census and its likely outcome is a disproof. |
| **Direction sound, magnitude a judgement call** | `A1`, `C23` | The behaviour is settled and the number is not. `A3`'s replacement radius and `B13`'s energy mapping are TUNE and belong in the author's hands, not in a fitted constant. |
| **Leads only, no mechanism yet** | `C21` | Budget for investigation. The reference footage is exact and our behaviour is measured, but which part of the anim runtime produces the difference is unidentified, and this may end in a decode rather than a fix. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy.

**⚠ Golden hazard.** `B11` and `B12` each reach every chapter, so both will move goldens across the
whole set. Take a baseline before either starts, keep them in separate worktrees, and put every
re-pin in front of the author. Two further items can move a narrower band and must not be assumed
safe: `B13` changes the light every aircraft is lit by, so the plane-bearing shots are in scope, and
`C21` reaches the particle path, so the particle shots are. An unchanged hash after any of these is
not evidence until you have seen the shot able to move. `A1` moved one as well, `c1-debris-rest`,
which its own manifest entry names as the column-tier shot; that pin is open in front of the author
and every later item's baseline has to be taken against the tree that carries it.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Airships

1. ☑ `BL-668` A downed zeppelin's wreck rests on the sea, and its gasbags stop falling through it
2. ❌ `BL-667` The zeppelin cannons' own scan reads the whole VehicleList, hulls included (disproven: the broadside runs no candidate scan, it walks the record's authored `targets` names, and the proposed swap is a no-op behind an `IsHumanPiloted` filter; `BL-681` filed for the real gap)
3. ☑ `BL-670` A zeppelin on a scripted route flies its short legs instead of cutting them

### Wave B — What a surface is: collision and light

11. ☑ `BL-678` Collision honours the polygon's own backface flag, as the original's test does
12. ☑ `BL-613` An alpha-textured surface takes no sun term, as the original's light evaluation does
13. ☑ `BL-332` The aircraft light takes the brightness the mission authors, not one hardcoded pair

### Wave C — Feedback at the controls

21. ☑ `BL-419` The sonic ground burst reads as one flat ring on the terrain (the ring is authored on
    the ground; a callee's translate on the caller's `sonic_emit1` carried it 30 m into the air)
22. ☑ `BL-459` A damaged engine's loop waits out the original's re-arm delay before it restarts
23. ☑ `BL-433` Numpad `+`/`−` drive the chase camera's zoom
24. ☑ `BL-679` A staged airframe leaves no stale node behind in the name resolver
25. ❌ `BL-405` Mounted ordnance tracks the aim before it launches, or the census closes it (disproven: no shipped airframe authors an animated mount node, so the pylon staying fixed already matches the original)

### Wave D — Closing sortie

31. ☐ At-the-controls pass, theme by theme, over every landed item that owes a judgement

## Dependency and parallelism notes

Waves group by theme, not by dependency. Every code item is independently landable and `D31` runs
last. Contention rules for parallel worktrees:

- **`A1`, `A2` and `A3` all reach the zeppelin family**, but not the same files: `A2` is
  `Session/ZeppelinRuntime.Cannons.cs`, `A3` is `Session/ZeppelinRuntime.cs`, and `A1` is the motion
  and contact machinery in `Mech3/Anim/MotionRuntime.cs`. They can run in parallel, but all three
  are judged by the same `zeppelin-breakup` suite and the same CM08 and CM14 flights, so land them
  in listed order and re-run that suite after each.
- **`B11` and `B12` are the run's only PLANNED golden-movers**, and `A1` moved `c1-debris-rest`
  before either of them ran. Never run `B11` and `B12` in one worktree, and never
  re-pin from a tree carrying both: a moved shot must be attributable to one of them. `B11` reaches
  `Mech3/SceneBuilder.cs` and `Mech3/Clutter.cs`; `B12` reaches the material path in
  `Mech3/SceneBuilder.cs` as well, so the two contend on that one file and must be sequenced rather
  than run side by side. `B11` first, because its census is narrower.
- **`B13` is disjoint from both** (`Session/Launcher.cs`, `Session/WeatherRig.cs`) and can run beside
  either, but it moves the plane-bearing goldens, so it does not share a re-pin with them.
- **`C21` and `C24` both reach the animation runtime** (`Mech3/AnimRuntime.cs`,
  `Mech3/Anim/PoseChannel.cs`, `Mech3/Anim/NameResolver.cs`). `C24` is the resolver and its identity
  comparer; `C21` is the pose and opacity path. Different members of one file, so merge with care or
  run them sequentially.
- **`C22` (`Flight/AiEngineAudio.cs`) and `C23` (`Flight/CameraController.cs`, the flight keymap and
  `docs/controls.md`) are disjoint** from each other and from everything above.
- **Scheduling, not files:** `B11` and `B12` land only after `PLAN-M5-polish-8`'s and
  `PLAN-M5-polish-9`'s `D31` sorties have been flown (Decision 4). Waves A and C are unaffected.
  **That gate is discharged:** all of `PT-86` and `PT-97` to `PT-117` were flown in one sitting, so
  `B11` and `B12` are free to land. `PLAN-M5-polish-9`'s `D31` is complete (`PT-107` re-flown after
  `B11`); `PLAN-M5-polish-8`'s stays open for one re-fly (`PT-102` corrected), which is not a hold on
  this wave.
- **`B11` carries a rider.** The CM12 ace was seen teleporting back to its authored pose repeatedly.
  The writer is `FlightController.cs:1752`'s under-map backstop, `Position.Y < UnderMapY` calling
  `Respawn()`, and it emits no log line at all, which is why a whole sortie's worth of teleports went
  undiagnosed. `B11` removes the trigger (the ace stops being stuck in geometry it should fly out
  of), so this is not its own item — but give the backstop a log line in the same change, or the next
  aircraft to fall through the world will be just as silent.

---

# Wave A — Airships

## A1 ☑ `BL-668` A downed zeppelin's wreck rests on the sea, and its gasbags stop falling through it

**Landed.** `MotionRuntime.TryGroundColumn` now reads the body's whole column instead of only what
is under it: the downward ray runs as before and, only when it answers nothing, a second ray runs
upward over the same `ColumnDepth`. The original's column is a cell query at `(x, z)` whose answer
does not depend on the body's height, and whose landing test `y + stepY < height` lifts a body that
is already beneath a surface back onto it. A single downward ray cannot express that: a body that
overshoots a surface within one step goes blind and never contacts it again. Nothing is clamped and
no constant is tuned; both tiers, the landing response and the watchdog are untouched.

**Verified.** The wreck comes to rest at y = 0.0 against the sea at y = 0, where it used to stop at
y = −411.2. All six gasbags stop between y = 0.0 and y = −5.6 instead of two of them, and the run
now dispatches 6 `huge_splash` and 6 `huge_ripple` where it dispatched 2 of each. The column
watchdog fires for none of the seven bodies, where it previously ended five of them. Full
`.\RunTests.ps1` on the landed tree: 3,073 units, 230 engine suites and 17 of the 18 goldens green
with engine errors clean, and one golden moved and left UNPINNED, which is the run's exit 1.

**⚠ One golden moved and is UNPINNED.** `c1-debris-rest`,
`ac23a4f7aba9003441ab0338de9011e4` → `e003acfd62ee90b1fbc2914937ed703f`. It is the manifest's own
column-tier shot, so it is the shot most able to move. The change is 13,846 pixels, 1.50 % of the
frame, inside one box at x 970..1144, y 191..299 with a maximum channel delta of 183: the spark
puffers on one resting debris piece sit a few pixels differently and its smoke plume is a slightly
different shape. Nothing structural moved, and the shot's own log is otherwise identical on both
sides (7,064 gamez nodes, 3,433 mesh instances, 1,995 colliders, 14 live motions). With the second
ray disabled the same tree reproduces the pinned hash exactly, so the move is attributable to this
change alone. The before and after captures and their crops are in the branch's `.scratch/`
(`a1-debris-before.png`, `a1-debris-after.png`, `a1-crop-*.png`).

**The same defect reached CM10's lifeboat.** `lifefallNM` throws `lifeboat` straight down at 1 m/s
from a hull already floating at y = 0.00, so it owes `bounce_sequence.water`. It used to sink 6.18 m
in the first second and never reach the branch at all; it now stops on the sea and calls
`med_splash` plus its own `lboat_destructionNM`. `PLAN-M5-polish-9` `B14`'s reading that the CM10
balloon geometry itself dips to the water is untouched by this: what changed is only that a body at
the water stays there.

**The diagnosis, which was the deliverable.** The defs cannot explain the split: `floatdown` on
`piratezep` and `break1` to `break6` on `gasbag1` to `gasbag6` all author `do_intersections: false`,
`no_altitude: false` and no `run_time`, so all seven bodies take the DEFAULT column tier with the
15 s column watchdog behind it, and the six gasbag events are identical to each other in every
field but their node and their `bounce_sequence.water` branch. What differed was arrival time.

- **The hull** does contact the sea, three times, at 105.8, 21.5 and 4.65 m/s with the decoded 0.2
  restitution between them. The third contact is survivable on the energy test (21.6 ≥ 3.5², the
  gravity it is falling under), so it parks the hull 7.7 cm above the surface with 0.93 m/s still
  on it. Two frames later the hull is 3 cm under the water, the downward ray answers nothing from
  there, and the watchdog freezes it 411 m down.
- **Gasbags 1 to 4** never get a crossing frame at all. The step the column tests is built from the
  gasbag's own ballistic origin through the CURRENT parent transform at both ends, so
  `to.Y − from.Y` carries only their 0.27 m local fall and not the 3.8 m the hull descends in the
  same frame. Their world height therefore steps +1.03 → −2.76, +2.99 → −0.80, +0.37 → −0.63 and
  +3.20 → −0.57 straight past the test, and the ray is blind from the next frame on.
- **Gasbags 5 and 6** are not different bags. They reach the water after the hull's own contact has
  cut the parent frame's descent from about 113 m/s to about 1 m/s, so their world step is 1.0 m
  against the same 0.3 m local step and `from.Y` lands inside the window where the surface is still
  below `from` and above `to`: 0.05 → −0.26 and 0.34 → −0.01. They are the two that arrived late.

The original has the same one-frame blind spot in its own step and does not care, because its query
answers with the surface height whatever the body's `y`. That is the single divergence, and it is
the one the fix closes.

**Tests.** `ground-contact` gained case `1b2`, a column body started 25 m UNDER the surface with the
column below it asserted empty first: it lifts back onto the surface instead of running out at
−2,085 m. `zeppelin-breakup` gained the wreck's and the six gasbags' resting heights, and its
`huge_splash` check is now `Same(6, …)`. Its gasbag drop check moved off displacement onto the
dispatched `ObjectMotion` event, because the old "moved more than 1 m" proxy passed on the defect
and failed on the fix (see `docs/verification.md` `INSTR-39`). `campaign-balloon-death` now wires
`SurfaceIsWater` as a real session does and asserts the lifeboat's water branch and its resting
height, replacing a "still dropping" check that scored sinking as the healthy answer. All the new
checks were shown red on the unfixed tree and green after.

**Still owed.** The CM14 (C2B/M04, `./RunGame.ps1 --campaign=<profile>:13`) kill over water belongs
to `D31`: the wreck should be seen floating rather than read off an artifact.

**Docs.** `docs/org/objectMotion.md`'s divergence table (the column is now two rays; the per-step
admission test is no longer inert; the roughly 4 cm band around a surface where neither ray answers
is recorded as measured), `docs/architecture.md`'s `src/Mech3/Anim/` entry, and
`docs/verification.md` `INSTR-39`.

**⚠ For whoever runs `B11`.** The upward half of this column relies on a collider answering a ray
that reaches it from behind, which today it does because `SceneBuilder` sets `BackfaceCollision`
unconditionally. `B11` makes that flag per polygon. If a water or terrain polygon clears
`SHOW_BACKFACE`, `zeppelin-breakup`'s resting checks are what will catch it, and the answer is a
column that casts downward from above rather than upward from below, not a re-pin.

### Original approach (kept for reference)

**Goal.** A zeppelin killed over water settles on the surface and stays visible there, with every
gasbag coming to rest on the water rather than sinking through it.

**Evidence (confidence: direction-sound; the entry and the record were re-verified in this session,
the contact tier was not re-read line by line).** The `zeppelin-breakup` suite's artifact records the
wreck at rest at y = −411 with the water surface at y = 0, and gasbags 1 to 4 falling about 1228 m
through the water while 5 and 6 land on it after 4 to 5 m. The suite is
`CSVM/src/Testing/ZeppelinBreakupSuites.cs` (registered at `:55`, artifact written at `:97`), and its
own comments describe `floatdown`'s −3.5 descent as the only thing bringing the wreck down. That two
of six gasbags stop and four do not is the shape of the finding: this is `MotionRuntime`'s contact
and bounce handling of a large untimed gravity body, not the `NodeUndercover` gate that
`PLAN-M5-polish-8` `B13` landed to let the breakup play at all.

**Approach.** Work in `CSVM/src/Mech3/Anim/MotionRuntime.cs`. Establish first why the contact tier
answers for gasbags 5 and 6 and not for 1 to 4, since that difference is the whole diagnosis and a
fix that stops the hull without explaining it is a guess. `PLAN-object-motion-decode` is the standing
authority on the query tiers: the ground-contact test is the DEFAULT column-tier query, `NO_ALTITUDE`
is its only opt-out, and `DO_INTERSECTIONS` upgrades it to a full sweep. Read which of those the
wreck and each gasbag are running before changing any of them.

**Model recommendation.** high. The work is a diagnosis in shared motion machinery whose contact
rules every falling body in the game reads, and the wrong fix is a special case for zeppelins.

**Verify.** `.\RunTests.ps1 -Suite zeppelin-breakup -SkipUnits -SkipGoldens` while editing, reading
`test-zeppelin-breakup.txt` for the per-step trace rather than the pass line alone. Then a CM14
(C2B/M04, `./RunGame.ps1 --campaign=<profile>:13`) kill over water, watching the wreck come to rest.
Full `.\RunTests.ps1` before landing.

**⚠ Traps.** `floatdown`'s descent is not ended by the `StopSequence` (`docs/org/sequences.md`); the
contact tier is what ends it, so a fix belongs there and not in the sequence runner. Do not clamp a
wreck's Y at the water plane to make the artifact read right: that invents a floor the original does
not have, and it would hide whichever tier is answering wrongly. `BL-639` (the Gemini's gasbags not
burning out) is blocked on `CAP-47` and is a different question about the same ship; do not fold it
in.

## A2 ❌ `BL-667` The zeppelin cannons' own scan reads the whole VehicleList, hulls included

**Closed ❌ disproven, no code.** The entry's premise is a misreading of one call site, and the fix
it proposed is a strict no-op whose own acceptance test could not have gone green. The item was
challenged at the plan's own dispatch, and four independent read-only lenses (the remake code, the
decode, the shipped data, and this project's record) each landed on the same answer.

**What is actually true.** `ZeppelinRuntime.Cannons.cs` is the **broadside battery**, not a turret
module. It calls itself the broadside half of `ZeppelinRuntime` (`:11`), binds the record's
`left_cannons`/`right_cannons` node lists (`:157`), plays the authored hatch deploy and retract
anims (`:241`, `:246`), and fires the hardcoded `wep_28` cannonball (`:21`). Its geometry is a
broadside and not a traverse: `ZeppelinBroadside.SideNormal` builds a lateral vector by side flag
and `TargetSide` gates on `dot(toTarget, sideNormal) > 0.707`
(`CSVM/src/Flight/ZeppelinBroadside.cs:110-143`), with the lateral sign re-derived from the built
cannon X positions so a mirrored import cannot fire the wrong side (`Cannons.cs:187-198`). Cadence
is the record's `cannon_fire_delay` per cannon, nothing like a turret `FIRE_RATE`, and there is no
yaw or pitch node, no traverse and no `DETECTION_RANGE`.
[`docs/formats/turrets.md`](formats/turrets.md) states the separation outright: the broadside is a
separate system with its own arc and fire logic, and `WAKEUP_ZEP_TURRETS` and `COMPLETED_ZEPCANNONS`
are different script ops for a reason.

**Why the fix shape was wrong.** The broadside runs **no candidate scan at all**, so there is no
candidate set to widen. `ResolveTarget` walks the record's authored `targets` node-**name** list and
takes the first live one (`Cannons.cs:311-313` into `ZeppelinBroadside.FirstLiveTarget`,
`ZeppelinBroadside.cs:163-174`). The decode agrees: `FUN_004bd8d0` resolves each `targets` name
through the general node lookup at load and `FUN_004bfe00` walks those stored pairs in authored
order ([`docs/formats/mission-entities.md`](formats/mission-entities.md)). The single
`CollectAircraft` call at `Cannons.cs:341` sits inside `NearestHumanAircraft`, reached only when the
authored name is literally `player` (`:318-322`), and the very next loop rejects anything whose
`Source` is not `FlightController { IsHumanPiloted: true }` (`:346`). Hulls carry
`Source = vessel`, never a `FlightController`, so every hull the widened collect added would be
discarded one line later. The proposed suite, a hostile hull under a cannon with no aircraft in the
scene, would have stayed red under the entry's own fix.

**Why the cited precedent does not carry.** Zeppelin AA turrets are a wholly separate path that
zeppelins already own: `ZeppelinRuntime.FanTeamsOntoTurrets` fans the record team onto
`TurretEmplacementRuntime` (`ZeppelinRuntime.cs:120-141`), whose emplacements are `TurretController`
instances, and `TurretController.AcquireTarget` is the widened one
(`CSVM/src/Flight/TurretController.cs:631-633`). The widening was never withheld from the broadside;
it landed on a different mechanism. `FUN_0041f9c0` is the turret picker and the broadside is not
among its callers.

**The zeppelin-only reading is also not the original's rule.** No team, side or ally field is read
anywhere in the broadside chain, and no `DAMAGES_ZEPPELIN` gate sits on this path: that gate lives
in the AI pilot's and the player's weapon-**slot selection**, and the broadside selects no weapon at
all, it hardcodes `wep_28` by name. Between 15 and 18 of the roughly 47 cannon-bearing records
author `targets` containing `player` (the two counts come from different lenses and the exact figure
is not settled; the conclusion does not turn on it), C3/M03's Pandora among them. What is true is the
observable outcome: only three shipped missions author `COMPLETED_ZEPCANNONS` (C2B/M04, C4/M05,
C5/M04) and every record they engage names another zeppelin, so **no shipped broadside ever fires on
the player**. That is the engage flag doing the work, not a target-class filter, and
`Cannons.cs:309-310` already says so in a comment. `CAP-46` confirmed the behaviour at the controls
of the original.

**Follow-up filed, not fixed here.** One real divergence surfaced. `ResolveOne` handles exactly two
cases, the literal name `player` and a name matched against the zeppelin roster, and warns
`unresolved` for anything else (`Cannons.cs:315-336`); the original resolves **any** world node name
through the general table and aims at a pair whose zeppelin half is 0 by world position. A record
naming a surface hull's node would work in the original and not here. No shipped record does, so it
is currently unobservable, and it is a name-resolution gap rather than a candidate-set gap. Filed as
`BL-681`.

**⚠ Do not re-open this as "broadsides should only shoot zeppelins".** The claim has now been
tested three times: `BL-517` was closed disproven, `BL-567`'s controls report was refuted by the
decode, and this pass refuted it again from four directions. That is evidence with a date on it, not
a prohibition: if a capture of the original ever shows a broadside firing on the player under a
script that authors `COMPLETED_ZEPCANNONS` on a `targets [player]` record, the decode is what would
have to give. Nothing shipped puts that case on the screen.

## A3 ☑ `BL-670` A zeppelin on a scripted route flies its short legs instead of cutting them

**Landed.** `ZeppelinRuntime.cs:69-75`'s per-instance floor (`1.5 * turnCircle`, up to 515.7 m for
CM08's Pandora) is replaced with `ZeppelinRuntime.ArrivalFloorM`, a flat 50 m TUNE constant with
no decoded mechanism behind it. A campaign census
(`CSVM.Tests/ZeppelinsTests.cs`'s new `TheArrivalFloorClearsEveryShippedZeppelinLeg`) walks
every mission that ships a `ZeppelinDef` (58 records, the reader's own install-wide census),
across all 8 chapters plus Instant Action, and measures the horizontal length of every edge on
every net a zeppelin flies: 678 edges. The shortest is 143.9 m (C4/M04's `M4Piratezep`, edge 0-5),
comfortably above the new 50 m floor, so no shipped leg advances the walk before the hull is
meaningfully underway. 50 m was chosen with margin under that measured minimum, not fitted to it:
it also clears C1/IA1's `IAZep` edge 4-5 (413.7 m), the leg the OLD 515.7 m floor would have
skipped outright, which is what the test catches when the floor is set back to the old worst-case
value.

An engine-free simulation of the real Klondike1 net, the same production `AiNetFollower`/
`ZeppelinMotion`, measured the node 3 to node 4 leg precisely: 1443.2 m long, radius
`max(10% of leg, 50 m floor)` = 144.3 m, since the DECODED 10%-per-leg rule dominates on a leg
this long and the floor never engages there. That predicts 90.0% flown; the measured capture landed
at 90.1%, matching within the turn-in cost of node 3's own 75° corner. An earlier reading of this
same leg reported 86%, read off a `RunProbe.ps1` flight's `zep:` log lines by interpolating between
two heartbeat prints roughly 5 s apart; that interpolation, not a second mechanism, produced the
lower number. The arrival radius alone accounts for the walk's advance on this leg.

A campaign-wide census of every node where a shipped net turns, ranked by how much arc a hull's own
turn circle (`speed / max_rate_yaw`) demands against the shorter of its two adjacent legs, finds its
worst case on C1B/MP3's `ZVZ1a`: a 67.7° turn at node 6 on a 205 m leg, against a 343.8 m turn
circle (speed 30, rate_yaw 5, the Pandora's own class) demanding 406 m of arc, a 201 m shortfall,
the largest anywhere in the campaign. Driven headless with the real production code, the hull enters
that leg in 7.7 s and leaves it in 16.5 s, both close to straight-line cruise time; it never orbits.
`CSVM.Tests/ZeppelinsTests.cs`'s new `TheWorstShippedTurnBreaksOutRatherThanOrbits` pins this: the
hull reaches node 6 and advances past it inside a 90 s budget, about 3x the measured transit. The
`ArrivalFloorM` doc comment now cites this test rather than only asserting the overshoot is
accepted.

The suite `zeppelin-pandora-dead-end` (real Klondike1 data, stops released the way the mission's
objectives would) still shows every one of the route's 12 edges walked end to end, the cargo point
settled to within 0.1 m at its own altitude, and the pitch never exceeding the steepest leg's slope.
The settling glide (`BL-597`, `PLAN-M5-polish-9` `B11`) is unaffected, since an armed stop point
bypasses the arrival-radius test entirely, and this suite is that item's own cargo-point check,
re-run clean.

**Verified.** The complete `.\RunTests.ps1`: build clean, 3075 of 3075 units passed, 230 of 230
engine suites passed with engine errors clean across 4 shards, and all 18 golden shots
hash-identical, in 194.8 s (over the 180 s budget, awareness only). No golden moved.

**Original approach (kept for reference).**

**Goal.** A zeppelin walking an authored node chain flies each leg to its node, including the legs
shorter than the arrival radius, instead of advancing the walk almost immediately at each one.

**Evidence (confidence: traced, with the replacement magnitude TUNE).**
`CSVM/src/Session/ZeppelinRuntime.cs:73` reads `float arrival = 1.5f * turnCircle;` over
`turnCircle = def.MaxSpeed / Mathf.Max(Mathf.DegToRad(def.MaxRateYawDeg), 1e-3f)` at `:72`, confirmed
unchanged in this session. For CM08's Pandora (`max_speed` 30, `max_rate_yaw` 5) that is 515.7 m, not
the 125 m an older paraphrase claimed. Measured: the node walk advanced past node 4 while still 515 m
short of it, so the hull flew 64 % of the 1443 m leg from 3 to 4 and cut the corner; past the cargo
point the legs are shorter than the floor (7 to 8 is 628 m, 8 to 9 is 545 m, 9 to 10 is 640 m), so
the walk advances at once at each node and the route is barely flown. The floor is explicitly
invented, so this is tunable without touching a decode.

**Approach.** Measure the shortest shipped leg across the campaign's zeppelin routes, then pick a
radius that lets it still be flown. The number is a judgement, so record what it was measured
against in the landing commit and mark it TUNE in the same breath. Do not reach for a decoded
constant that does not exist.

**Model recommendation.** medium. The change is one constant, but the measurement that justifies it
spans every shipped route and the answer has to survive the author's eyes at `D31`.

**Verify.** CM08 (C1B/M03, `./RunGame.ps1 --campaign=<profile>:7`) with the Pandora's node walk
logged, showing each leg flown to its node. Re-run `PLAN-M5-polish-9` `B11`'s cargo-point checks,
since the settling glide shares this hull. Full `.\RunTests.ps1` before landing.

**⚠ Traps.** The floor does not affect where an armed stop parks the hull, which is the settling
glide `BL-597` landed, so a route that looks wrong at a stop point is a different question. Do not
confuse this floor with the aeroplane executor's decoded along-leg test, which zeppelins do not use;
[`docs/formats/mission-entities.md`](formats/mission-entities.md), "Steering", is the authority on
which is which. A smaller radius makes the hull overshoot and orbit its node, so the check is that
short legs are flown, not that the number went down.

---

# Wave B — What a surface is: collision and light

## B11 ☑ `BL-678` Collision honours the polygon's own backface flag, as the original's test does

**Goal.** An aircraft, a round or a probe approaching a single-sided face from behind passes through
it, as it does in the original, instead of being stopped by a contact the original never has.

**Evidence (confidence: traced, with a census).** `FUN_0055c9c0`, the node mesh test, calls the
ray-polygon routine as `FUN_0055d6c0(param_2, &start, &end, verts, uVar17 & 0x3ff, uVar17 >> 10 & 1)`.
Inside it, after building the face normal from two edge cross products, the segment's END distance
decides: `if ((0.0 <= fVar14) && (param_6 == 0)) return 0;` culls the face outright, and only then
does the sign test `(((uint)fVar14 ^ (uint)fVar2) & 0x80000000) == 0` look for a crossing. `param_6`
is bit 10 of the packed word whose low ten bits are the vertex count, which is polygon flag bit 0,
documented in [`docs/formats/gamez.md`](formats/gamez.md) as `unk2`, upstream `SHOW_BACKFACE`. One
flag governs both rendering and collision. CSVM sets `BackfaceCollision = true` unconditionally, and
this session re-read both sites: `CSVM/src/Mech3/SceneBuilder.cs:896` and
`CSVM/src/Mech3/Clutter.cs:1087`. No query anywhere sets `HitBackFaces` or `HitFromInside`, so every
ray runs Godot's defaults, where back faces hit. Census (C2): 7656 of 12645 polygons (60.5 %) clear
the flag, matching the install-wide figure in [`docs/formats/gotchas.md`](formats/gotchas.md); of
terrain polygons specifically 2251 of 2325 (96.8 %) clear it.

**Approach.** Honour `poly.ShowBackface` on the collision shape, which the loader already decodes at
`GameZ.cs:481`, behind a census and an A/B rather than as a blanket flip. Take the census first: per
chapter, how many collider faces change sidedness, and which bodies lose a face they were previously
collidable from. `docs/org/weaponRay.md` is in scope because weapon rays run the same test, so the
item is not done until the ray path has been read against the same flag.

**Model recommendation.** max. The blast radius is every chapter, every aircraft and every clutter
body; the change moves goldens and suites; and the failure mode is an aircraft falling through the
world rather than a visible artefact.

**Verify.** Baseline the 18 goldens before touching anything. Then the census above, the full
8-chapter `--freecam --chapter=<X>` regression with mesh and node counts compared, `--collision=show`
at the CM12 site both before and after, and the `BL-669` worked example: CM12's ace `hkfirebrand_9`
survives its authored pose over `g35052` and `tagged` instead of dying on a ram. Full
`.\RunTests.ps1`, and every moved golden re-pinned only with the author's review.

**⚠ Traps.** The render side shares the flag and is a separate known simplification, so a collision
fix must not silently change what draws. The 266 game-wide back-to-back pairs (one quad authored as
two opposite-wound polygons) make a naive per-face change collidable from both sides anyway, so the
census has to count pairs, not faces. `SceneBuilder.cs:896`'s comment states the original grounds for
the blanket flag (inconsistent source winding), so a change there must answer that argument rather
than delete it. This item does not land before the two open sorties are flown (Decision 4).

**Landed.** `CollidersForMesh` emits two shapes per surface class on one `StaticBody3D`, a
`BackfaceCollision` shape for the `ShowBackface` polygons and a one-sided shape for the rest, the
latter with reversed winding since the source's visible side is Godot's back face. `MotionRuntime`'s
ground column had to move with it: water is 100 % one-sided in every chapter but C1B, so an upward
second cast answers nothing and it now comes down from above. Clutter is deliberately left
two-sided, because `AppendTriangles` does not alternate a strip's winding and so its triangles have
no agreed front, and because whether the original's intersection database holds a stamped decoration
is undecoded; `SolidCollisionOneSidedTriangles` sizes that divergence (1439 in C2, 3156 in C5).

**The winding argument is answered by measurement rather than by argument.** The blanket flag rested
on the claim that the source winding is inconsistent, evidenced by rendering culling nothing; but the
world renders double-sided because `_cullBackfaces` is false for the world build, a separate render
simplification that says nothing about winding. The new `world-ground-solid` suite rays down into the
centre of every world-partition cell in all eight chapters and then up from beneath what it found:
1376 of 1376 cells answer from above before and after, while the count answering from below falls
from 1376 to 24. Nothing lost its topside anywhere. Census: 52541 of 82121 collidable faces are
one-sided (64.0 %), with 241 back-to-back pairs staying solid both ways by construction.

**The weapon-ray path needed no change, which is itself the finding.** The original honours the flag
at the query; CSVM honours it on the collider, which every ray shares, so `Projectile`,
`TurretController`, `FlightController.HitWorld`, `WeaponLab`, `LensFlareRig` and `MotionRuntime`
inherit it with no per-call-site edit. That also settles the `HitBackFaces` worry empirically: every
query runs Godot's defaults and the ground census still reads 24 from below rather than 1376.

**Verified.** Full `.\RunTests.ps1`: 3366 units, 235 engine suites, engine errors clean. The
8-chapter `--freecam --collision` regression is identical in gamez nodes, mesh instances and collider
counts across all eight. `--collision=show` at the CM12 site is visually identical, the only moving
pixels being the overlay's own counters and 28 shapes crossing its per-shape box budget, with no
collision triangle added or removed. `BL-669`'s worked example is answered: a ray from
`hkfirebrand_9`'s pose up to the surface returns nothing, its level track meets no collider over
8 km where it used to meet the sheet at 260 m, and flown 40 s it ends with min y 150, grazes 0 and
`crashed False`. The under-map backstop gained the log line `BL-669` asked for, rate-limited with a
running reset count. One golden moved, `c1-debris-rest`, and a 2×2 attribution shows the collider
change moves no golden at all: the mover is the `MotionRuntime` column, and the new frame rests a
debris piece on the ground instead of sinking it. Re-pinned on the author's review of the A/B.

**Flown.** `PT-107` re-flown on the merged tree: the ace is seen in the air together with the
firebrand wave, and the mission finishes correctly. That closed `BL-669` and completed
`PLAN-M5-polish-9`'s `D31`.

## B12 ☑ `BL-613` An alpha-textured surface takes no sun term, as the original's light evaluation does

**Landed.** The exemption is the texture's own answer, carried from the extractor rather than
derived. `TextureArchive` reads the archive `manifest.json`'s `alpha` field at construction and
publishes it as `LastAlphaClass` (`None`/`Simple`/`Full`), beside the existing pixel classification
the blend/scissor choice reads and not in place of it. `SceneBuilder` builds a textured world
surface without the `csky_world_light` term when that class is not `None`, whatever the model's own
`lighting` flag says, and `Clutter` applies the same rule to a sprite card's own shader. Nothing is
fitted and no name is listed: the two gates are ANDed exactly as `FUN_00551d90` and `FUN_005524d0`
compose them. Where no manifest ships the class falls back to the decoded pixels, which is a
degradation and is commented as one, since it cannot see the `Simple` textures.

**Verified.** Full battery on the merged tree: build clean with 0 warnings, 3,369 units passed and 0
failed, 236 engine suites passed and 0 failed with engine errors clean across 4 shards, and all 18
goldens hash-identical against the re-pin below (364 s total, engine and goldens over budget on a
busy workstation, exit 0). The 8-chapter `--freecam` regression is clean in every chapter (exit 0, zero error lines) and each
one reports a non-zero exemption: C1 120 of 375 built materials, C1B 48/147, C1C 38/118, C2 71/309,
C2B 34/108, C3 66/240, C4 111/388, C5 98/330. The whole-table join behind those built counts is in
`docs/org/vertexLighting.md`; against its C5 figure our join reads 237 of 583 off `texture.zbd` and
239 off the `rtexture14` tier the engine loads, where the page previously said 234, so that number
is corrected in the same change. The shipped-data expectation holds on the loaded archive:
`poleflare` and `lightpole` are `Full` and exempt, `cblock1`, `bldg1` and `wtr00000` are `None` and
lit. `texture-alpha-class` is the regression guard for the reader half, weighted in
`analysis/engine-suite-weights.json`.

**Twelve goldens moved, re-pinned on the author's review of the before-and-after montages.**
`c1-waterfall`, `c1b-night-sea`, `c2-city`,
`c3-island`, `c1-flight`, `c1-destroy-effects`, `c1-crash`, `c1-debris-rest`, `c1-targeting-hud`,
`c1-ai-wreck`, `campaign-4p-grid` and `campaign-intro-fill`. Six are hash-identical: `c1c-rain`,
`c2b-rain`, `c4-snow`, `c5-city-night`, `viewer-bhawk` and `empty-stage`. Every changed pixel in
every moved shot got BRIGHTER and none darker, which is the exemption cancelling a multiply by a
sub-1 `world_light` and nothing else. Magnitudes: `campaign-4p-grid` 7.31 % of the frame at a
maximum channel delta of 22, `c1-waterfall` 4.95 % at 33, `c1-targeting-hud` 3.76 % at 36,
`c1-ai-wreck` 2.94 % at 32, `c1-flight` 1.95 % at 32, `c1-crash` 1.92 % at 37, `c1-debris-rest`
1.42 % at 30, `c3-island` 1.17 % at 3, `c1-destroy-effects` 0.39 % at 43, `c1b-night-sea` 0.30 % at
93, `campaign-intro-fill` 0.13 % at 3, `c2-city` 0.09 % at 1. The per-shot magnitude tracks its
chapter's `world_light` deficit exactly: C1B (0.426) carries the largest per-pixel delta, C3 (0.99)
and C2 (1 in the shot's active zone) barely move, and C4 and C5 (1) cannot move at all. The
before-and-after captures and the per-shot change masks are scratch output, reproducible by
regenerating the 18 shots against the previous pin.

**⚠ This does not touch the CAP-11 C5 residual, and cannot.** C5 authors `world_light=1` in both its
zones, so an exemption from a multiply by one changes no C5 pixel; `c5-city-night` is
hash-identical. The README's C5 reading (tower faces ×0.66, lit low-rise ×0.58 of the original)
stands untouched, which is consistent with Decision 5 rather than a new finding.

**Goal.** Every alpha-textured surface in every chapter renders unlit, the way the original's light
evaluation leaves it, instead of being modulated by the world light like an opaque one.

**Evidence (confidence: traced).** [`docs/org/vertexLighting.md`](org/vertexLighting.md) pins the
original's per-surface lighting exemption as two gates, and the second is engine-wide: in
`FUN_005524d0`'s textured branch, bit `0x02` of the texture object's storage flags byte at `+0x09`,
set for exactly the textures carrying an alpha channel, skips the per-vertex light evaluation
outright and sends the polygon through the unlit submission path. CSVM has no counterpart. Confirmed
against the shipped data: `poleflare` and `lightpole` are `alpha: Full` and exempt, while `cblock1`,
`bldg1` and `wtr00000` are `alpha: None` and lit in both. Confirmed in code this session:
`CSVM/src/Mech3/TextureArchive.cs:896` classifies alpha with `ImageHasAlpha(img)` off the decoded
pixels (`:990`, `:993`), so the header word is never seen, exactly as the entry claims.

**Approach.** Two halves, and the first is the plumbing. The alpha class lives in the extractor's
`alpha` field in `texture.zip`'s `manifest.json`; carry it through `TextureArchive` so a material can
ask for it, then apply the exemption on the material the builder creates, keyed on the texture's own
alpha class, alongside the existing per-model `lighting` gate. Land the plumbing and the rule in one
item, because the rule cannot be tested without the field.

**Model recommendation.** high. The mechanism is decoded and unambiguous, but the plumbing crosses a
reader boundary and the result changes a large population of surfaces across all eight chapters.

**Verify.** Baseline the 18 goldens first. Then a per-chapter count of how many materials take the
exemption, checked against the census in `docs/org/vertexLighting.md`; the C5 night poses in
`playtest/CAP-11/README.md`; and the full 8-chapter `--freecam` regression. Full `.\RunTests.ps1`.
Every moved golden goes to the author before it is re-pinned, and the population change wants the
author's eyes at `D31`, not a suite alone.

**⚠ Traps.** **The deployed texture tree cannot answer the question**: it ships PNGs only. **A PNG
alpha-channel test is not a substitute**, because it loses the one to ten `Simple` textures per
chapter that carry the bit as well, which is precisely why `TextureArchive`'s existing pixel
classification cannot be reused for this. Do not extend this item toward `BL-335`'s blend rule: that
reads a different field (the render-flags word at header offset `0x0E`), and its own entry records
that fixing blend alone leaves the reported symptom in place because the depth sort is a separate
delta. Do not claim `BL-322` as closed by this (Decision 5). This item does not land before the two
open sorties are flown (Decision 4). [`docs/org/textures.md`](org/textures.md) carries the
storage-flag bits and the extractor field name; `BL-070` stays in `backlog.md` and its "should be
exempt from the dim" half is settled by this rule, so its remainder after this lands is the
billboard-axis question alone.

## B13 ☑ `BL-332` The aircraft light takes the brightness the mission authors, not one hardcoded pair

**Landed.** The faithful path has its own arm of the zone apply. `WeatherRig.FaithfulEnergies` turns
a zone's authored `SUNLIGHT_DIFFUSE`/`SUNLIGHT_AMBIENT` into the sun's `LightEnergy` and the
Environment's `AmbientLightEnergy`, and `ApplyFaithfulLighting` writes them onto the session sun,
the session Environment and every registered cockpit-pass clone, on every zone change. The
launcher's two hardcoded values become `WeatherRig.DefaultEnergies`, the pair a viewer, a menu or a
weather-less mission still flies under, and the day anchor the mapping scales a zone against.
`GameSession.BuildCockpitPasses` now registers the interior pass in both graphics modes, since the
faithful light moves per zone too.

**The two constants are TUNE and owe the author's eyes**: sun 1.6 and ambient 0.9, each reached by
a zone authoring the install's modal day pair (1.5 / 0.5) and **capped** there. Calibrated against
today's accepted day level rather than against a screenshot: every shipped day zone keeps the exact
energies the build has been flown at, and only a dimmer zone moves, so the change can darken a
plane and never brighten one. The cap is the faithful path's own constraint and not a copy of
enhanced mode's factors (Decision 6): this pass has no tonemap, so an energy past the day level
clips a plane to flat white, where enhanced mode lights and tonemaps a whole world. There is
deliberately no night gate either, because the faithful world light ignores `FOG_COLOR` as well, so
capping C5 would sink its plane below its own fullbright terrain.

**Verified.** `.\RunTests.ps1` exit 1, in the goldens stage alone and only on the four unpinned
movers below: build PASS, units 3078/3078, engine 231/231 suites with engine errors clean, goldens
"4 moved, 0 broken of 18". Nothing else regressed, and `analysis/goldens/manifest.json` is
unmodified in the tree. A new `sun-energy` suite drives a rig per mission and reads the light back:
C1B/IA1 `zone1` resolves sun 0.640 / ambient 0.270 against C1C/M01 `zone1`'s 1.600 / 0.900, and a
C1C/MP1 zone crossing carries the new energy mid-flight. It is red on the unfixed tree by
construction, where both missions leave the light at its Godot default. Five unit facts pin the
mapping, the cap, the absent night gate and the shipped night/day pair. `--det` still isolates the
graphics mode: a `config.json` asking for `enhanced` produced byte-identical hashes for all 18
shots, movers included.

**Goldens moved, UNPINNED and awaiting review.** Four, all C1/IA1 aircraft shots, all from the same
cause: C1 authors diffuse 1.2, so the plane's sun energy drops 1.6 → 1.28. Measured against
before-images reproduced from a neutralised build that returned HEAD's own hashes digit for digit.

| shot | pixels changed | max channel delta | rows |
|---|---|---|---|
| `c1-destroy-effects` | 11172 (1.212 %) | 22 | 430–555 |
| `c1-targeting-hud` | 10802 (1.172 %) | 21 | 247–547 |
| `c1-ai-wreck` | 10393 (1.128 %) | 21 | 209–535 |
| `c1-flight` | 2314 (0.251 %) | 14 | 418–533 |

The pattern is the evidence (GOLD-5): a control run driving the launcher's sun to 0.5 moved seven
shots, and the four missing from this list are exactly the ones the mapping cannot reach.
`viewer-bhawk` and `empty-stage` fly no mission and keep the default pair; `campaign-4p-grid` flies
C1/M02, whose `zone1` authors diffuse 1.5, which is the anchor itself. No shot without an aircraft
moved.

**⚠ The ambient half of the mapping is inert, and this is measured.** With
`AmbientLightSource.Sky` at the default full sky contribution, Godot takes the ambient off the sky
cubemap scaled by the background energy multiplier, so `AmbientLightEnergy` never reaches the
shader: taking the launcher's 0.9 to 0.0 left all 18 goldens byte-identical, while the same
experiment on the sun moved seven. The write is landed and correct, and only the sun half is
visible today. Recorded as `WORLD-32` in `docs/verification.md`. Whether the faithful path should
stop taking its ambient from the sky stays the out-of-scope rendering-design question this item
named, and it now has a measured consequence: an aircraft's ambient fill is the same procedural
daytime sky at night as by day.

**The night/day reference exists, but not the pair this section asked for.** `CAP-11` shipped
original stills at C1B IA1 that show the airframe: `playtest/CAP-11/t0.5-c1b-spawn-island.png` and
`t5-c1b-moon-clouds.png`, chase view, red Bloodhawk, reading deep maroon against the night island;
`OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` and its `2` are the same airframe
at night from a different pose. The day half of that airframe is
`playtest/CAP-11/t0.5-c2-spawn-city.png`, bright red over C2's suburb. So the original plainly does
light a plane by the mission, and our own matched-pose `playtest/CAP-11/csvm-c1b-spawn.png` shows
the defect: our plane reads brighter than the terrain it sits on where the original's does not.
**C1C has no capture at all** (`CAP-11`'s README: unreachable in Instant Action), and CAP-11's own
measurement boxes deliberately avoid the plane, so no shipped capture answers the A/B
quantitatively. What is owed is a matched-pose pair in the original at **one airframe and one
livery**, C1B IA1 at night against a C1C campaign mission by day, chase view, with our build shot
at the same poses and the same paint pinned; the existing stills cannot be measured against ours
because the liveries differ.

**Original approach (kept for reference).**

**Goal.** A plane flying C1B's night mission is visibly darker than the same plane in C1C's daylight,
because the light driving it reads the mission's own `SUNLIGHT_DIFFUSE` and `SUNLIGHT_AMBIENT`.

**Evidence (confidence: traced, with the mapping TUNE).** `CSVM/src/Session/Launcher.cs:1061` sets
`LightEnergy = 1.6f` and `:1077` sets `AmbientLightEnergy = 0.9f` with `AmbientLightSource.Sky`, once
at launcher level for every mission; the comment at `:1057` says the bearing there is a default that
`WeatherRig.ApplyZone` overwrites per zone, and it overwrites the bearing only. The original sets
both on the same `sunlight` node in the same zone-apply call as the orientation: `FUN_00472ea0` calls
`FUN_004dbdb0` (diffuse) and `FUN_004dbce0` (ambient) directly beside `FUN_004dc610` (the rotation
setter). They swing hard, `DIFFUSE` 0.4 to 2.0 and `AMBIENT` 0.15 to 0.6, with C1B night at
0.6 / 0.15 against C1C day at 2.0 / 0.6. `CSVM/src/Flight/Weather.cs:152-153` and `:288-289` already
parse both. **New in this session:** the mapping is not missing everywhere. `WeatherRig.cs:198`'s
`EnhancedEnergies` turns the authored pair into Godot energies (`diffuse * 1.07`,
`ambient * 1.8`, with night caps at `:42-43`), but `WeatherRig.cs:777`'s `ApplyEnhancedLighting` is
enhanced-mode only, so the faithful path still runs the two launcher constants.

**Approach.** Give the faithful path its own zone-apply write of the sun energy and the environment
ambient, reading `Weather.cs`'s already-parsed pair. Read `EnhancedEnergies` as the precedent for the
shape, and calibrate the faithful numbers separately (Decision 6): enhanced mode lights a whole
shaded world, while the faithful path lights only the aircraft, so the same factors are not expected
to be right. Keep the change to those two constants. Whether Godot's ambient should stop being
`Sky`-sourced in the faithful path is a separate rendering-design question and not this item.

**Model recommendation.** high. The mechanism is settled and the calibration is a judgement against
the author's eyes, which is the part that goes wrong when it is fitted to a screenshot instead.

**Verify.** A matched pair of poses, C1B night and C1C day, showing the same airframe under each,
against the original. `CAP-11` pinned the world half of this pair and is the model for the method.
`<TODO: check whether any shipped OriginalScreenshots capture shows aircraft brightness at night and
by day; if none does, mint a CAP with ./New-ItemId.ps1 -Kind CAP and note it in playtest.md rather
than fitting the numbers to our own render.>` Full `.\RunTests.ps1`, and the pair judged at `D31`.

**⚠ Traps.** `DIFFUSE` is a DX7-era intensity, not Godot's `LightEnergy` units, so mapping 0.4 to 2.0
onto the light is a new calibration and not a substitution. Do not reach for enhanced mode's factors
as decoded values; they are TUNE for a different scene. Do not touch `csky_world_light`, which is
`CAP-11`-calibrated on terrain and is the fullbright world's scalar, not the aircraft's.

---

# Wave C — Feedback at the controls

## C21 ☑ `BL-419` The sonic ground burst reads as one flat ring on the terrain

**Landed.** The lead was a decode question and it had a decode answer, in name resolution rather
than in any authored number. Nothing in `sonic_rings.zrd`/`sonic_control.zrd` was touched.

**What was wrong.** All five sonic rings are called `AT_NODE sonic_emit1, 0, -5, 0`, and
`ring_down1` (the lasting ground ring, called at `SEQUENCE_OFFSET 1.2`) translates itself from +30
to +6 in that frame over 0.3 s, so it belongs about a metre over the struck surface and grows 1→5
there over 2.6 s. That is the reference's single flat annulus. The same `sonic_emit1` is also the
site `sonic_puff1` is called on, and `sonic_puff1`'s own second sequence translates `sonic_emit1`
from 0 to 30 m over 1.2 s: that rise is how the thin vapour column is drawn. In the original the two
never meet, because a definition resolves node names inside the private subtree copy it is handed
at load (`def+0x6c`/`def+0x48`) and reaches the call site only through the `INPUT_NODE` sentinel.
Ours anchors a `CALL_ANIMATION`'s callee on the CALLER's node, so `sonic_puff1`'s translate drove
the caller's `sonic_emit1` and carried the burst's whole second half up with it. Measured: the
ground ring settled 31 m over the impact instead of 1 m, and the caller's anchor moved 30 m.

**Verified.** `AnimRuntime.StagingAdmits`'s call-site allowance now stops at a definition that has a
staged copy of its own in that pool slot (`HasOwnCopyBeside`), which drops the name to the own-root
tier the original reads. The ground ring settles at +1.00 m and grows to 5× there, the vapour column
still climbs its authored 30 m on `sonic_puff1`'s own copy, and the caller's `sonic_emit1` does not
move. Filmed in the weapon lab against flat C1 dirt: the airborne cyan torus is gone and one flat
pale ring lies on the terrain and expands. New regression `sonic-ground-ring`, red on all four of
its claims before the change. Full `.\RunTests.ps1` green, no golden moved. Docs:
`docs/org/sequences.md` (the tier chain), `docs/architecture.md` (`AdmissibleStaging`),
`docs/formats/weapon-effects.md` (the burst's shape, and the `OPACITY_STATE OFF` correction below),
`docs/verification.md` INSTR-41.

**Two entry claims turned out wrong, and are corrected rather than acted on.** The "thick grey
puffer column the original does not have" is the authored vapour column (`sonic_emit_puff1`,
`watersquirt`, blue-white through to near-black), not smoke that should be removed; it read wrong
only because it was drawn beside a ring that should have been under it. And
`docs/formats/weapon-effects.md` claimed `ACTIVE` with `OPACITY_STATE OFF` was a way of "starting
invisible"; the argument says whether translucency is ENABLED, so the sonic rings draw at full
opacity from the first frame and each is taken away by its own ramp.

**Still open, and not this item's.** The air detonation was not compared frame by frame: a level
`--weapon-lab --weapon-fire` run detonates at `RANGE` in mid-air, which is what INSTR-41 now
records, but the air burst's own shape against `CAP-23 Rocket SONIC Air.mp4` still owes a sitting at
`D31`. `CAP-26`'s "look for" list should gain the flat-ring-against-airborne-hoops question.

### Original approach (kept for reference)

**Goal.** A sonic rocket striking flat ground reads as one flat pale-green ring lying on the terrain
and growing for about three seconds, with no airborne torus and no smoke column, and the same
definition detonating in the air reads as a brief sparkle.

**Evidence (confidence: lead-only; the reference and our own behaviour are both measured, the
mechanism between them is not identified. The entry and the record were re-verified in this session,
the anim runtime was not re-read for this item).**
`OriginalScreenshots/Videos/CAP-23 Rocket Sonic Ground.mp4` (frames 200 to 330 at 30 fps, impact at
about 204): a bright white star flare with two or three thin pale rings for about 0.3 s, then one
flat crisp pale yellow-green annulus lying on the terrain with radial striations and a dark centre,
expanding smoothly for about 3 s and fading by about 4.2 s, plus a thin blue-white vapour column and
no smoke. `CAP-23 Rocket SONIC Air.mp4`: the air detonation is a small brief white sparkle. Ours
(`--weapon-lab=wep_08 --weapon-fire`, sim frames 12 to 126): three or four fat soft cyan hoops read
as rings rising in mid-air, all finished inside about 1.1 s, then `ring_down1` grows into a very
large fuzzy cyan torus above the ground from 1.3 to 2.1 s, under a thick grey puffer column the
original does not have. Total life is comparable (about 3.8 s authored against about 4.2 s measured),
but ours is front-loaded and airborne where the original is one continuous ground ring. The defs are
`sonic_ground_effect` calling `ring_up1..4` at t=0 and `ring_down1` at +1.2 s on `sonic_ring1..5`
(`extracted/zrdr/sonic_rings.zrd.json`, `sonic_control.zrd.json`), placed as a `SURFACE_ANIMATION`
with world up rotated onto the struck normal.

**Approach.** This is a decode question against `crimson.exe`'s anim interpreter before it is a
tuning one. Read how the runtime interprets the ring defs' motion, specifically whether the authored
channel is a translation up or a scale in the ground plane, then their opacity ramps and colour, and
then whether the puffer smoke belongs to this def at all. Film the burst frame by frame in the weapon
lab against the reference frames. No number here should be adjusted to the footage.

**Model recommendation.** high. The deliverable may be a decode rather than a fix, and the failing
mode is fitting our own constants to video frames.

**Verify.** A weapon-lab burst on flat C1 ground reads as one flat pale-green ring on the terrain
growing for about three seconds, with no airborne torus, and the same def in the air reads as a brief
sparkle. Compare against the CAP-23 frames rather than against a remembered impression. Full
`.\RunTests.ps1`, and the burst judged at `D31`.

**⚠ Traps.** Footage-derived magnitudes have failed repeatedly in this project, so the reference
clips settle **what shape** the effect is, never **what number** produces it. `BL-293` records that
the upper ring's fixed axis is faithful and correct, decoded at `FUN_005ac7a0`, so do not reorient it
here in pursuit of a better-looking burst. `BL-535`'s repeat-burst cost is measured in the same
neighbourhood (`PoseChannel.SetSubtreeOpacity`, `ApplyOpacity`) and is a separate item; if this work
touches that path, do not silently absorb it. `CAP-26` is the rocket-impact rings capture, and its
"look for" list should gain the flat-ring-against-airborne-hoops question as this item settles it.

## C22 ☑ `BL-459` A damaged engine's loop waits out the original's re-arm delay before it restarts

**Landed.** `CSVM/src/Flight/AiEngineAudio.cs`'s `SetEngineDamaged` now only STOPS the slot-0 handle
on the healthy→damaged edge; it no longer swaps the stream on that frame. `Update`'s new
`ArmDamagedLoop` ticks a shared re-arm timer every frame the handle is silent and the airframe is
damaged, via the new pure `EngineAudioCurves.AdvanceDamagedRearm(DamagedEngineTimer, dt, u)`
(`u` the frame's own draw off `Rng.Stream(Rng.FlightAudio)`), and only swaps the stream, draws the
pitch multiplier and logs `slot 0 -> snd_damagedengine` once the accumulated time crosses a
threshold redrawn each call as `3.0 + 2·u` seconds. The timer's state
(`PlaneStats.DamagedTimer`, a new `DamagedEngineTimer` with one mutable `Elapsed` field) lives on
the airframe DEFINITION, not the instance: it is set once in `PlaneStats`'s object initialiser and
every `With*` clone's `MemberwiseClone` carries the same reference forward, so every aircraft built
off one cached `PlaneStats` (`GameSession.BuildFlightRigs`'s `aiStatsCache`) shares one counter and
one draw, exactly as the original's def field does. A looped `snd_damagedengine` that is still
playing skips `ArmDamagedLoop` entirely, matching "never reaches the timer". The damaged→healthy
direction is untouched and stays immediate. `docs/architecture.md`'s `AiEngineAudio.cs`,
`PlaneStats.cs` and `EngineAudioCurves.cs` entries carry the mechanism and the sharing trap.

**Verified.** New suite `ai-engine-rearm` (`CSVM/src/Testing/AiSuites.cs`) spawns two AI aircraft off
one cached `PlaneStats`. The first ticks alone to 2.9 s (under the 3 s floor, so this is
deterministic whatever the seed draws, since no possible drawn threshold sits below it) with no
swap. The second, only silenced afterward, inherits that head start and swaps within its own 174
frames (short of the 3 s floor a fresh timer would need), while the untouched first aircraft still
has not; the damaged→healthy restore is confirmed immediate throughout. Run against the pre-fix
code (a deliberate revert-and-restore, not landed), the same suite fails on its first check, "the
damaged loop does not swap on the frame the damage is decided", a real fail-before/pass-after
regression. A flown `RunProbe.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury
--ai-damage=0.1 --volume=0 --log=sound:debug --seed=1 --det` (a temporary physics-frame counter
added and removed for the measurement) put `audible` at physics frame 8 and
`slot 0 -> snd_damagedengine` at frame 199, 191 frames, 3.18 s at the fixed 16.667 ms step, inside
the decoded 3 to 5 s window; the same probe against the pre-fix code logged the swap at frame 8
too, ahead of `audible` in the log. `EngineAudioModelTests` (`CSVM.Tests`) gained direct unit
coverage of `AdvanceDamagedRearm`'s threshold/reset arithmetic and of the `DamagedTimer` reference
surviving every `PlaneStats.With*` clone. Full `.\RunTests.ps1` after this item's own change: build
clean (0 warnings), 3077/3077 units, 231/231 engine suites, 18/18 goldens hash-identical, no
golden moved. Re-verified after merging in `A1`, `A3`, `B13`, `C23` and `C24`: build clean,
3095/3095 units, 232/232 engine suites (232 now that `ai-engine-rearm` joins the other four
items' own new suites), and the golden stage moves exactly the five shots those items' own
sections already record as pending (`B13`'s four C1/IA1 aircraft shots plus `A1`'s
`c1-debris-rest`), all thirteen others hash-identical. None of the five is a shot this item's own
Evidence, Approach or Verify sections named, and this item touches no rendered pixel path, so
they are not re-pinned here; that stays each moved shot's own item to close.

**Original approach (kept for reference).**

**Goal.** A damaged AI aircraft that comes back inside the engine-audio cull starts its damaged loop
three to five seconds after it becomes audible, as the original does, rather than on the same frame.

**Evidence (confidence: traced).** When the engine slot's handle is not playing and the airframe is
damaged, `FUN_004b18a0` does not restart the damaged loop at once: it accumulates the frame time into
the airframe DEFINITION's field at `def+0x88` and starts a loop only once that total passes a
threshold redrawn each frame as `3.0 + 2·rand()/32767` seconds, resetting the field to zero as it
does ([`docs/formats/vehicle.md`](formats/vehicle.md), "What makes an airframe damaged"). CSVM
restarts on the frame the swap is decided: `CSVM/src/Flight/AiEngineAudio.cs:183` `SetEngineDamaged`
assigns the stream at `:194` with no accumulator anywhere in the file, confirmed in this session. So
a damaged aircraft coming back inside `AiEngineAudio`'s 2000 m cull is audible three to five seconds
earlier than the original's would be.

**Approach.** Port the accumulator and its per-frame threshold draw onto the airframe definition, not
the instance. The reachable path is the cull (or a stream that ends), which is what keeps this small.

**Model recommendation.** medium. The mechanism is fully decoded and the surface is one file; the
judgement is confined to where the shared counter lives.

**Verify.** An AI plane damaged below a quarter health, flown out past 2000 m and back, logs its
`slot 0 -> snd_damagedengine` line three to five seconds after the `audible` line rather than beside
it. Read it from `.scratch/logs/`, and run with `--volume=0` rather than `--mute`, since a muted
baseline is blind to sound errors. Full `.\RunTests.ps1` before landing.

**⚠ Traps.** The timer sits on the **definition**, not the instance, so every aircraft sharing an
airframe def shares one counter and one draw. Port that sharing or record why not, but do not quietly
give each aircraft its own. A looped `snd_damagedengine` that is still playing never reaches the
timer. Rejected already: treating the delay as a crossfade, because the transition is a hard cut.

## C23 ☑ `BL-433` Numpad `+`/`−` drive the chase camera's zoom

**Landed.** Numpad `+` and `−` now drive the chase camera's zoom. `CameraController.UpdateZoom`
reads `Key.KpAdd`/`Key.KpSubtract` directly, the same convention `ActiveView`/`BackActive` already
use, moving a target at the decoded 2/s and clamping it to `[0, 1]`; the shown value chases that
target at the decoded 1.5/s through `HeadLook.Approach`, the same smoothing law the head-look
angles already use. A private `EffectiveRadius` property subtracts `shown · Dist` (the plane's own
authored base distance, not the dynamic radius) from the existing dynamic chase radius, floored at
zero, and `Chase`, `FixedView`, `BackView` and `PadLook` all read it in place of the raw radius, so
the trim reaches the ordinary chase pose, every numbered fixed view and the look-behind alike,
matching the architecture's existing rule that those poses share one number.
`FlightController` calls `UpdateZoom` only from its ordinary per-frame camera branch, never while
the weapon lab's held-airframe orbit is active: `OrbitInput` reads the same two keys for that
orbit's own dolly, and the two branches are already mutually exclusive on `Held`, so no new gating
was needed beyond placing the call correctly. The centre key's zero-the-zoom behaviour stays out of
scope, filed at `BL-435`.

No CLI mechanism can simulate a held keyboard key here (`--hold=` scripts the flight stick, not the
keyboard), so the rate/clamp/smoothing law is asserted engine-free in
`CSVM.Tests/CameraControllerZoomTests.cs` instead: the 2/s target rate in both directions, both
keys cancelling, the `[0, 1]` clamp at both ends, and the shown value's exponential catch-up at
1.5/s against a closed-form value. Two probes confirm the wiring without a physical key: an
unpressed run's chase breadcrumb reads `zoom=0.000` throughout with the dynamic radius unchanged
from before this item, and a pinned `--view=6` run's `dist=` sequence matches the unpinned chase's
`d=` sequence frame for frame, so `--view=`'s precedence is untouched. The interactive feel at both
ends of the clamp is `D31`'s own line for this item.

**Verified.** The full `.\RunTests.ps1` battery on this worktree: build clean, units 3081 passed 0
failed, engine 230 suites passed 0 failed with errors clean across 4 shards, goldens 18 of 18
hash-identical (none moved), 202.8 s total, exit 0.

**Original approach (kept for reference).**

**Goal.** Holding numpad `+` or `−` moves the chase camera in and out at the original's rate, and the
head-look centre key zeroes that same zoom.

**Evidence (confidence: traced, with the binding a judgement).**
`OriginalScreenshots/Keybinds Views 2.png` labels the pair External Camera Zoom In/Out, decoded as
keys `0x43`/`0x44` moving a stored zoom value at `2·dt`, clamped `[0, 1]`, smoothed at `1.5`/s
(`PLAN-cockpit-view.md`, "What the data actually ships"). `BL-150`'s item (f) records the same pair,
independently measured, as a camera distance trim; read together, that trim is this zoom axis and not
a separate control. The head-look controller's own centre key (`0x3e`) also zeroes this zoom value in
free-look, so the two features share one piece of state. Confirmed in this session: no `KpAdd` or
`KpSubtract` binding exists anywhere under `CSVM/src`, so the keys are genuinely unbound, and the
chase distance lives in `CSVM/src/Flight/CameraController.cs` (the authored base distance and speed
factor at `:121`).

**Approach.** One zoom axis bound to numpad `+`/`−`, driving `CameraController`'s chase distance with
the decoded rate, clamp and smoothing. The centre key's zero-the-zoom behaviour rides along once
`HeadLook`'s centre path reaches the chase camera, which is `BL-435` and stays out of scope here.
Update `docs/controls.md` in the same turn, since the player input changes.

**Model recommendation.** medium. The constants are decoded and the surface is small; the judgement
is confined to how the axis reads at the controls.

**Verify.** `--fly` with the chase camera, holding each key to the clamp and back, then a
`--view=` run to confirm a held numpad view still wins. Full `.\RunTests.ps1` before landing, and the
feel judged at `D31`.

**⚠ Traps.** `BL-150` records that the whole numpad camera scheme needs a rebuild and is plan-sized;
this item adds one axis and must not start that rebuild. Do not invent a zoom range: the clamp is
`[0, 1]` over the authored base distance, not an absolute metre figure. The dynamic chase radius is
shared with `--view=`, so check that a pinned view is unaffected.

## C24 ☑ `BL-679` A staged airframe leaves no stale node behind in the name resolver

**Landed.** `NameResolver<TNode>.DropFreed` retires every row naming a node the liveness delegate
rejects, together with the ancestry entries and cached answers keyed on one, and `FreedRows` counts
what a stage would otherwise leave behind. `AnimRuntime.IndexRebasedStage` calls it before it grows
the table (through the private `DropFreedNodes`), and `AnimRuntime.FreedNodeRows` exposes the count
to a suite. That entry is the one staging call that puts a subtree in over one its caller may have
freed, an airframe swapped for another on the same rig, and `AircraftStage.StageFlown` is its only
caller. `landings-hookup-airframe` now asserts the count is zero after each airframe is staged.

**The Evidence below cites the wrong site, and the real one is a level down.** The throw is not in
`Add`. The captured stack is `NameResolver.IsWithin`, the `_parentOf` lookup inside `FindAll`'s
scope filter, reached from `AnimRuntime.Targets` under `PoseChannel.HandleActiveState` while the
hookup definition dispatches an `ACTIVE_STATE`. A freed node is not merely a row `FindAll` skips:
it stays a dictionary KEY in the ancestry map and in the find cache, both keyed by `Node3DIdentity`,
whose `Equals` reads `GetInstanceId()`. The walk therefore throws for whatever later query happens
to hash into the dead key's bucket, which is why a minority of runs fail rather than all of them.
`Remove` cannot be the fix either, since removing hashes the dead key it is handed; `DropFreed`
rebuilds the keyed collections instead.

The Evidence's second claim, that the rate does not change with the number of planes driven, is
also wrong. The stale-row count grows strictly, 0, 134, 237, 334, 446 and 562 across the six
airframes the suite drove, so every further airframe raises the collision odds. That is what made a
seventh airframe throw often enough for `PLAN-M5-polish-9` to drop `player_autogyro` from the list.
It is back, seven airframes now, and passes.

**Verified.** The fault is non-deterministic, so both rates were measured over 14 runs of
`.\RunTests.ps1 -Filter landings -SkipUnits -SkipGoldens`. Unfixed tree: 1 run in 14 threw.
Fixed tree, with the seventh airframe restored: 0 in 14. The suite's own new check does not rest on
that rate and is the instrument to read instead: with the sweep commented out it fails on five of
the six airframes driven at the time, at the counts above, and passes with the sweep in. Off engine,
three `NameResolverTests` cases pin the rule without Godot, against a comparer that throws for a
freed node exactly as `Node3DIdentity` does. Both loops ran on one shard, so shard order is not a
confounder between them. Full battery: build clean, units 3076 passed 0 failed, engine 230 suites
passed 0 failed over four shards with errors clean, goldens 18 of 18 hash-identical, exit 0.

**Original approach (kept for reference).**

**Goal.** A suite staging several airframes in one process runs clean every time, because a freed
airframe's nodes are removed from the resolver when it goes rather than surviving to be compared.

**Evidence (confidence: traced).** An `ObjectDisposedException` inside `NameResolver.Add` reaching
the identity comparer's `Equals`, seen on roughly one run in four of the `landings` filter while
`landings-hookup-airframe` drives six airframes in sequence. The rate does not change with the number
of planes driven, so it is a stale entry surviving a switch rather than a capacity effect.
**Cite corrected in this session:** the comparer is not in `NameResolver.cs`. `Node3DIdentity` is a
private nested class at `CSVM/src/Mech3/AnimRuntime.cs:4095`, handed to the resolver at
`AnimRuntime.cs:618` together with `IsInstanceValid` as the liveness delegate and a second
construction at `:770`; `AnimRuntime.cs:3954` carries the note on why reference equality is
unreliable across proxies of one native node. The resolver itself is
`CSVM/src/Mech3/Anim/NameResolver.cs`, whose only liveness reference is the injected delegate at
`:67`.

**Approach.** Find who owns removal from the resolver's dictionary when a staged airframe is freed,
and clear the entry there. A `Node3DIdentity` that compares a freed node is the symptom, not the
cause.

**Model recommendation.** high. The bug is non-deterministic and the obvious fix is the wrong one, so
the work is ownership analysis rather than a patch.

**Verify.** Drive `.\RunTests.ps1 -Suite landings -SkipUnits -SkipGoldens` repeatedly, ten runs or
more, before and after. A green run proves nothing on its own here, so record the failure rate on the
unfixed tree first and show it able to fail. Full `.\RunTests.ps1` before landing.

**⚠ Traps.** Do not guard `Equals` with an `IsInstanceValid` check and call it fixed: that hides a
stale entry which will also answer a later lookup with the wrong node, turning an exception into a
silent wrong answer. The liveness delegate at `NameResolver.cs:67` exists for the resolver's own
sweep and is not the place to paper over this.

## C25 ❌ `BL-405` Mounted ordnance tracks the aim before it launches, or the census closes it

**Closed ❌ disproven, no code.** The census this item's own Approach called for came back no: no
shipped airframe authors an animated node on the mount its ordnance hangs from, so `PylonOrdnance`
parenting the round body to the pylon marker at identity and never touching it again is not a
simplification. It is what the original does too, because the original never has anywhere to slew.

**The census.** Full write-up in
[`docs/org/aiPilot/aiWeapons.md`](org/aiPilot/aiWeapons.md), "Census: no shipped airframe carries
that node". Per shipped airframe, over the ten `gun_pitch`/`gun_yaw` carriers that author ordnance
(`devastator` and `bswingman` carry none and are excluded, per `aiWeapons.md`'s own ordnance
census; `patrolboat`/`t_truck` carry a single gun and no pylons at all):

| Airframe | Def | Pylon rig | Animated mount node authored? |
|---|---|---|---|
| Bloodhawk | `bloodhawk` | `pylon1`…`pylon8` | No |
| Fury | `fury` | `pylon1`…`pylon8` | No |
| Warhawk | `warhawk` | `pylon1`…`pylon8` | No |
| Hoplite | `autogyro` | `pylon1`…`pylon8` | No |
| Hellhound | `avenger` | `pylon1`…`pylon8` | No |
| Balmoral | `balmoral` | `pylon1`…`pylon8` | No |
| Brigand | `brigand` | `pylon1`…`pylon8` | No |
| Firebrand | `firebrand` | `pylon1`…`pylon8` | No |
| Kestrel | `kestrel` | `pylon1`…`pylon8` | No |
| Peacemaker | `peacemaker` | `pylon1`…`pylon8` | No |

Every one of the ten hangs its pylons off the identical rig the player planes use: mesh-less
`Object3d` markers, `model_index -1`, no children, no distinguishing flag
(`extracted/planes/nodes.json`), and `vehicle.zrd.json` authors no mount or node-reference field for
any of them. The only nodes any shipped plane model ever moves are the five turret airframes'
barrels, and turrets run an entirely separate system (`Turret`/`TurretRate` in `turret.cpp`) that
never reaches this mount. The one shipped `lpylon*`/`rpylon*` node set belongs to `anim_bloodhawk`,
a scripted asset outside the AI pilot's model roster, not to a second AI pylon rig; the
`docs/formats/markers.md` pylon section carried that misreading and is corrected in the same commit
as this closure.

**Original approach (kept for reference).**

**Goal.** Either a mounted rocket visibly follows the aim its launcher has already computed, the way
the original's animated mount slews, or the shipped data is shown not to author such a mount
anywhere and the item closes with that census as its result.

**Evidence (confidence: traced mechanism, undecided data).** The mount model is decoded
([`docs/org/aiPilot/aiWeapons.md`](org/aiPilot/aiWeapons.md), "`gun_pitch`/`gun_yaw` clamp the
mount"): `FUN_004b7670` rotates the desired lead into the vehicle frame, clamps each axis into its
authored band, and writes the result as the mount's actual aim (`+0x48` to `+0x50`); a mount
carrying an animated node (`+0x34`/`+0x38`) slews toward that direction through `FUN_00460840`
instead of snapping to it. Our pylons do not move at all: `PylonOrdnance` parents the body to the
pylon marker at identity and never touches it again (`PylonOrdnance.cs:46-49`). An AI round leaves
along a launch direction up to the traverse limit off the pylon axis
(`AiRocketeer.LaunchDirWorld`), so the mounted body and the round it becomes point different ways at
the launch instant.

**Approach.** **Settle the data question first, before writing anything.** The slewing mechanism is
decoded; whether any shipped aircraft authors an animated node on the mount its ordnance hangs from
is not. Census the shipped vehicle defs for that node. If none authors one, the original's rocket
body does not visibly track either, and this item closes on the census with no code, which is a
success. Only if the census says yes: the pylon marker takes the clamped direction the fire decision
already computes, with the mounted body riding it as it does today, and `FUN_00460840`'s rate is
read so ours slews where the original slews rather than snapping.

**Model recommendation.** medium. The census is mechanical and the decode is already written down;
the judgement is confined to reading the census honestly rather than building the feature anyway.

**Verify.** If it closes on the census: the census itself, per shipped airframe, recorded in the
landing commit. If it lands as code: an AI rocket launch filmed from outside at a traverse limit,
showing the body pointing where the round leaves. Full `.\RunTests.ps1` either way.

**⚠ Traps.** A fixed forward gun has no animated node and reaches the clamped direction the same
frame; if the ordnance mounts are the same, there is nothing to build. Do not add a slew because it
looks better: that invents motion the original does not have. Ours would snap where the original
slews unless the rate is read too, so a half-port is worse than no port. The player's rocket is not
in scope: it takes the aircraft's own axis with no aim at all (`FlightController.OrdnanceLaunchDir`),
so only the AI's mount can disagree with its round.

---

# Wave D — Closing sortie

## D31 ☐ At-the-controls pass over every landed item that owes a judgement

**Goal.** Every item in this plan whose acceptance needs eyes has been seen, and each one is either
confirmed, retuned with the author's number, or reopened with what was actually observed.

**Evidence (confidence: not applicable; this item produces evidence rather than resting on it).**

**Approach.** One sitting, theme by theme rather than in mission order, since this run is
theme-spread. The checks each item owes:

- `A1`: a CM14 zeppelin killed over water comes to rest on the surface with every gasbag on it.
- `A3`: CM08's Pandora flies its short legs to their nodes, and the author confirms the arrival
  radius reads right rather than merely measuring smaller.
- `B11`: the CM12 ace survives its authored pose, and no aircraft anywhere falls through geometry it
  used to stand on.
- `B12`: C1B's night coastline flown low, since the exemption turns those sheets from near-black to
  a lit rim and a still cannot say how the rim reads in motion; plus a pass over the alpha-textured
  populations in at least three chapters. Not the C5 poses in `playtest/CAP-11/`: C5 authors
  `world_light=1`, so no C5 pixel can move and `c5-city-night` is hash-identical.
- `B13`: the matched C1B night and C1C day aircraft poses, judged against the original.
- `C21`: a weapon-lab sonic burst on flat ground, and the same def in the air.
- `C23`: the chase zoom's feel at both ends of the clamp.
- `C25`: only if the census landed code rather than closing the item; an AI rocket launch seen from
  outside at a traverse limit.
- `C22`, `C24`: instrument checks rather than judgements, confirmed from logs and suites, and listed
  here only so the sitting can confirm nothing regressed. `A2` owes nothing at the controls; it
  closed as a disproof.

`<TODO: mint the PT ids for these rows with ./New-ItemId.ps1 -Kind PT -Count <n> when the wave opens,
and write them into playtest.md; do not hand-number them.>`

**Model recommendation.** The author flies it; an agent prepares the launch commands, the reference
frames and the per-row question, and writes up what the author reports. high for the write-up, since
a reported symptom has to be turned into either a confirmation or a correctly-scoped new item.

**Verify.** Every row above answered in writing, with the author's own words kept for any complaint,
and each landed item's checklist status left correct. A row that fails becomes a new `backlog.md`
entry with its `⚠ Traps` section, not a silent reopen.

**⚠ Traps.** The author's eyes outrank the instruments: a luminance distance or an unchanged golden
never overrides a reported look. Quote a complaint verbatim rather than paraphrasing it into a
number. `PLAN-M5-polish-8`'s and `PLAN-M5-polish-9`'s own `D31` rows (`PT-97` to `PT-117`) are flown
first, since `B11` and `B12` do not land until they are (Decision 4).
