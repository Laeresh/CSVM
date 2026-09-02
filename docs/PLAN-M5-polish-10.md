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
[`PLAN-M5-polish-9.md`](PLAN-M5-polish-9.md), and that is a scheduling constraint rather than a file
one: see Decision 4. `A2` died at dispatch and its named first alternate `BL-405` was promoted into
its place as `C25`. Remaining alternates if another item dies early: `BL-673` (latent, small),
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
not evidence until you have seen the shot able to move.

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

1. ☐ `BL-668` A downed zeppelin's wreck rests on the sea, and its gasbags stop falling through it
2. ❌ `BL-667` The zeppelin cannons' own scan reads the whole VehicleList, hulls included (disproven: the broadside runs no candidate scan, it walks the record's authored `targets` names, and the proposed swap is a no-op behind an `IsHumanPiloted` filter; `BL-681` filed for the real gap)
3. ☐ `BL-670` A zeppelin on a scripted route flies its short legs instead of cutting them

### Wave B — What a surface is: collision and light

11. ☐ `BL-678` Collision honours the polygon's own backface flag, as the original's test does
12. ☐ `BL-613` An alpha-textured surface takes no sun term, as the original's light evaluation does
13. ☐ `BL-332` The aircraft light takes the brightness the mission authors, not one hardcoded pair

### Wave C — Feedback at the controls

21. ☐ `BL-419` The sonic ground burst reads as one flat ring on the terrain
22. ☐ `BL-459` A damaged engine's loop waits out the original's re-arm delay before it restarts
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
- **`B11` and `B12` are the run's only golden-movers.** Never run them in one worktree, and never
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

---

# Wave A — Airships

## A1 ☐ `BL-668` A downed zeppelin's wreck rests on the sea, and its gasbags stop falling through it

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

## A3 ☐ `BL-670` A zeppelin on a scripted route flies its short legs instead of cutting them

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

## B11 ☐ `BL-678` Collision honours the polygon's own backface flag, as the original's test does

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

## B12 ☐ `BL-613` An alpha-textured surface takes no sun term, as the original's light evaluation does

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

## B13 ☐ `BL-332` The aircraft light takes the brightness the mission authors, not one hardcoded pair

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

## C21 ☐ `BL-419` The sonic ground burst reads as one flat ring on the terrain

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

## C22 ☐ `BL-459` A damaged engine's loop waits out the original's re-arm delay before it restarts

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
slews unless the rate is read too, so a half-port is worse than no port. `BL-404` (whether the
player's rocket gets a direction at all) is a separate open question and is not in scope.

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
- `B12`: the C5 night poses, plus a pass over the alpha-textured populations in at least three
  chapters, against `playtest/CAP-11/`.
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
