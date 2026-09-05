# Flight

The plane as a flying, shooting, damageable thing, plus its HUD and stunt mode. Reads plane stats from the extracted zrdr; owns the arcade physics and everything drawn over the pilot's view.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Flight/WeaponDefs.cs
Typed reader over the shared `weapons.zrd.json` `BALLISTICS` block: 48 `WeaponDef`s (guns, rockets,
ordnance) keyed by `wep_*`, plus the `NO_AMMO_WARNING` empty-clip sound. It owns ballistics, damage,
allotment, the class flags, the specials and the `FIRE`/`FLYOUT`/`IMPACT` bindings, `IMPACT` keyed by
`SurfaceRegistry` id and `DESC` resolved through `Messages`. Modelled on `PlaneStats`. `RANGE`,
`DETONATION_DISTANCE` and `IMPACT_PROXIMITY` are each exposed twice, authored and squared, and the
rule for comparing them sits at the fields themselves; `ImpactHook` is the per-weapon detonation hook
`ProjectilePool.RunImpactHook` dispatches. Schema: [../formats/weapons.md](../formats/weapons.md);
behaviour: [../org/ordnanceTypes.md](../org/ordnanceTypes.md). Inspect with `--dump-weapons`.

## src/Flight/Loadout.cs
Two layers over `CSVM/data/stock_loadouts.json`. `StockLoadouts.Load` parses the file into per-plane
`LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves each gun slot's markers to live
muzzle `Node3D`s and its caliber plus ammo to a `WeaponDef`, and each hardpoint to its `pylon`,
yielding `GunGroup`s with their own ammo counters and `Hardpoint`s. Turret slots bind but stay inert.
`Loadout.ForRig` synthesizes a lab loadout covering the airframe's whole rig rather than only what
stock names, and runs it through the same `Bind`, so there is exactly one bind path. Inspect with
`--dump-loadout`. Slot-to-firepoint binding: [../formats/markers.md](../formats/markers.md); schema:
[../formats/loadouts.md](../formats/loadouts.md). Read `LoadoutChoice.cs` next.

## src/Flight/LoadoutChoice.cs
One pilot's edits to a fit, and the rosters the Ammo Selection screen offers. `LoadoutOptions` holds
the two dropdowns parsed from the same file's `selectable` block, authored in the original's own
order and neither derived nor sorted. `LoadoutChoice` keys its picks by slot identity, gun slots 1 to
4 and pylons 1 to 8, rather than by position in a def's arrays, and `ApplyTo` lays them over a base
handed in rather than looked up, so a custom plane's saved fit takes the same path and a pick for a
slot the base lacks is simply dropped. `None` is an explicit empty mount while a null entry is no
choice at all, which is what makes reset-to-stock a clear rather than a rebuild.
`Session/CampaignLoadout.cs` fills one from a profile. Read `Loadout.cs` for the bind it feeds.

## src/Flight/WeaponBench.cs
The world-less "do all 48 weapons mount and fire without throwing" pass check behind `--weapon-test`
and the `weapons-fire` in-engine suite: one static `Run(plane, Loadout, WeaponDefs, ProjectilePool)`
over a parked plane that spawns straight into the caller's pool and returns the report plus the
counts a suite asserts on. It needs no world, no colliders and no frame, since `Spawn` does the
muzzle math and the pool insert synchronously. Both callers hand it `Loadout.ForRig`'s loadout, so
every weapon fires from every mount of its class rather than only from what stock names. Read
`Loadout.cs` for the rig it binds and `Projectile.cs` for the pool it fills.

## src/Flight/FireControl.cs
The fire-control state machine, a plain engine-free class: trigger edges, per-group `FIRE_RATE`
accumulators and muzzle rotation, ammo draw-down, both weapon selectors with their on-empty
auto-advance, the rocket pull and cooldown gate, and the two once-only dry cues.
`Step(dt, FireInputs)` takes raw held booleans, detects every edge inside, and returns decisions in
one reused `FireOutcome`; `FlightController.ApplyFireOutcome` performs them against muzzle
transforms, `ProjectilePool` and `FlightAudio`. Ammo mutates through the node-free
`IGunSlot`/`IPylonSlot` views, so `Loadout` stays the single store the gauges read and a decision
cannot diverge from the counters mid-tick. The slot index math is `WeaponCursor.cs`; read it next.

## src/Flight/AimAssist.cs
The gun aim assist, decoded in [../org/aim-assist.md](../org/aim-assist.md). `GunAimSlot` is one gun
barrel's plane-local state and `Tick` the per-frame forget and catch-up pass over it; `TryIntercept`
is the constant-velocity lead solver every other module in the namespace consumes rather than
re-deriving; `Scan` picks the target the original would pick out of an `AimCandidateSet`, whose four
lists are kept separately and are fed by `ProjectilePool` and `DestructibleRegistry`; `FireDirection`
is the whole fire call in one place and `Scatter` its launch cone. A static, engine-free class that
reads no clock of its own, so it unit-tests without a `FlightController`, which owns one
`GunAimSlot[]` per firable gun group. Read `FireControl.cs` for what fires along the answer.

## src/Flight/TargetRef.cs
The one abstraction over everything the player can select: an enemy Fury, a zeppelin engine and a
turret emplacement are three unrelated C# types, and every consumer downstream reads this and never
the underlying type. It wraps an `AimCandidate` rather than restating it, adding what the aim assist
has no use for: `Kind`, `Class` and `Objective`, the display names and labels, and optional health
and armor. `Classify` is the decoded class model, `CategoryLine` composes the marker's first line,
and `SortsFirst` is the pair of overrides that put an objective or a hostile round in flight ahead of
every sector. Pure data, no Godot node; pinned by the `target-ref` suite. Decode:
[../org/targeting.md](../org/targeting.md). Read `TargetPool.cs` next.

## src/Flight/TargetPool.cs
The player's classed candidate pool: three lists of `TargetRef` (`Enemy`, `Ally`, `NonAircraft`,
reachable through `Of(TargetClass)`), rebuilt from scratch on every `Rebuild`, the original's own
contract and why a runtime spawn appears and a death disappears with no extra plumbing. It walks
three of the aim assist's four lists; structures arrive through `subParts` and the mission's
`targets.zrd` sites through `objectives`, and the two mission flags stay separate so a site lands on
the Enemy cycle and a sub-part on the Non-Aircraft one; an aeroplane whose roster block flags itself
carries the marker on its own vehicle candidate, never twice. `TargetSelection` owns the instance.
Decode: [../org/targeting.md](../org/targeting.md) "The candidate list". Read `TargetSelection.cs`.

## src/Flight/TargetSelection.cs
One pilot's target selection: the sticky choice, the eleven actions and the lifecycle. One instance
per pane, and it owns its `TargetPool`. The split between the action handlers, which only mutate the
class and the selection identity, and `Resolve`, the per-frame pass that re-sorts and re-finds the
selection by entity before falling back to the list head, is the original's, and that one fallback is
the entire lifecycle: auto-acquire, switch-on-death and drop-on-class-change are all the same failed
re-find. `SectorKey` is the cycle comparator, and `Select`/`ApplyInitial` are `--target=`'s seam. No
Godot node dependency; pinned by the `target-selection` and `target-flag` suites. Decode:
[../org/targeting.md](../org/targeting.md). Read `TargetHud.cs` for what draws the result.

## src/Flight/TurretDefs.cs
Typed reader over the shared `ai.zrd`'s `TURRET` section, 42 `TurretDef`s: the carried/standalone
split (`CREATE_STANDALONE` present and zero means carried, looked up by `TITLE` from a host, while
`NODES` patterns mean world emplacements), the `PARTS` kinematic chain, the `WEAPON` sub-block whose
`NAME` is a BALLISTICS id, the arcs, the attack and bored duty-cycle windows, and `SOUNDS.CANNON`. It
tolerates the eight engine-accepted keys nothing authors, and `FindByTitle` mirrors the engine's
lookup, where a titleless entry matches unconditionally. `TeamId` carries the decoded loader default.
Schema: [../formats/turrets.md](../formats/turrets.md); the team space:
[../org/targeting.md](../org/targeting.md). Read `TurretController.cs` next.

## src/Flight/TurretController.cs
One `ai.zrd` turret gunner, both families: carried (`BuildCarried`, per host off the vehicle def's
`TurretMount`s, ticked from `FlightController.SimStep`) and world emplacement (`BuildEmplacements`,
per matched `NODES` pattern node, ticked by `Session/TurretEmplacementRuntime`). Per tick it takes
the nearest hostile out of the vehicle list and then the mission structures in the decoded pass
order, solves the lead through `AimAssist.TryIntercept`, slews the PARTS nodes inside the authored
arcs, and runs the fire gates: activation, the attack window, the barrel-on-solution cone, a cached
line of sight and the `FIRE_RATE` redraw. Aliveness, the team space and a platform's exclusion
from its own fire state their rules at their members: [../org/targeting.md](../org/targeting.md).

## src/Flight/WeaponCursor.cs
`FireControl`'s internal ammo-slot index math, an `internal` class nothing else may call: `NextArmed`
is the firing cursor, the selected slot while it has rounds and otherwise the next armed slot
forward-wrapping, and `NextSelectable` is where the manual G or H step lands, the next armed slot
strictly after the cursor with empties skipped. Each slot is its own selectable position whatever it
carries, so the selector steps across slots rather than ordnance types and H cycles even a uniform
loadout, which is the per-hardpoint reading the original gives at the controls. Stateless, and proven
through `FireControl`'s own interface rather than its own. Read `FireControl.cs` next.

## src/Flight/RocketTriggerLatch.cs
The rocket trigger's consumed-press latch. A cutscene skip or a pause-menu Resume can hand flight
input back on the same frame the button that confirmed it is still physically down, and
`FlightController`'s edge-free button read would take that still-down press as a fresh pull.
`ArmIfHeld` is called at the moment input comes back and `Read` is the trigger's own read every frame
after, reporting released until the button lets go. Pure state with no `Node` and no clock, public
rather than `internal` so its own unit tests can drive it directly, since this project carries no
`InternalsVisibleTo`. `FlightController` is the only caller. Read `FireControl.cs` for the machine
the reading feeds.

## src/Flight/Ballistics.cs
The VELOCITY/ACCELERATION/GRAVITY integration every round steps with: a static, Godot-`Node`-free
class holding `Step`, one round's per-frame advance mutating position and velocity in place, and
`March`, the reticle's whole capped walk with its range cap and iteration bound. Extracted so the
live pool and the pipper cannot silently diverge, while each caller keeps its own step size and its
own surrounding loop. `LaunchSpeed` is the seed pair both take, and the motor rule and the prohibition
on ever reducing a speed are stated at the members that hold them. Behaviour:
[../org/ordnanceTypes.md](../org/ordnanceTypes.md); the census behind a fixed `dt` for `March`:
[../formats/weapons.md](../formats/weapons.md). Read `Projectile.cs` for the pool that steps it.

## src/Flight/DisablingIntensity.cs
The shared `SONIC`/`FLASH` intensity, decoded in
[../org/ordnanceTypes.md](../org/ordnanceTypes.md): a static, Godot-`Node`-free `TryResolve`
returning the wash weight and the stun duration, plus `FacingDot` for the `FLASH` direction
convention. The curve is a plateau that fades over the last quarter of the radius, so both distance
inputs are squares, the engine's own distance routine returning a square and this path never taking a
root. `FLASH` adds the facing test and `SONIC` does not, which is the only behavioural difference
between the two flags. The consumers are the player's screen wash, the AI stun and the smoke screen,
and this module knows about none of them. Read `SmokeScreens.cs` next.

## src/Flight/Difficulty.cs
The difficulty setting, as the engine's own 0/1/2, and the single thing it does: multiply an enemy
vehicle's armour and health maxima at spawn, decoded in
[../org/vehicleDamage.md](../org/vehicleDamage.md). `Parse` takes both shipped vocabularies, the
campaign selector's and Instant Action's, which name the same three tiers. `FactorForSpawn` owns the
team gate, which is inequality with the player's team rather than hostility, so a neutral or
team-less spawn is scaled too; `PlaneStats.WithEnemyDurability` applies the factor and the per-spawn
jitter bands around the scaled hull afterwards. The setting reaches nothing else, and no AI skill,
accuracy or aggression is keyed to it. Read `PlaneStats.cs` next.

## src/Flight/TanglerChoke.cs
The choker's engine-dead duration, decoded in
[../org/ordnanceTypes.md](../org/ordnanceTypes.md) "The choker, settled": a static,
Godot-`Node`-free `Duration`, plus `EngineDeadBounds`, which resolves the bounds the way the original
does, as a pair of globals every `TANGLER` parse overwrites so the last entry carrying one wins for
every choker in the install. The curve's numerator is squared while its radius is raw, an authentic
unit mismatch the floor covers, and whether an aircraft is caught at all is the cloud's own
squared-radius test in `ProjectilePool.StepTanglerClouds`. The seconds go to
`FlightController.TryChokeEngine`; this module knows nothing about aircraft. Read `Projectile.cs`.

## src/Flight/SmokeScreens.cs
The `SMOKE_SCREEN` mechanism, decoded in [../org/ordnanceTypes.md](../org/ordnanceTypes.md)
"SMOKE_SCREEN is a stun trap", as three types in one file. `SmokeScreenRule` is static and
Godot-`Node`-free, holding the catch test and the human wash's cadence; `SmokeScreenTunables` reads
the three `player.json` keys with the loader's own image defaults; `SmokeScreens` is the world
registry, where `Lay` is the fire path's entry (such a weapon spawns no round) and `SimStep` runs the
timer down and stuns or washes every other in-play aircraft inside the cone about the layer's live
pose. It is not an occluder: no collision, no visibility and no targeting role. Each screen drives
its own emitter over the `ISmokeEmitter` seam, whose engine side is `SmokeScreenEmitters`.

## src/Flight/BeeperTags.cs
The `BEEPER` and `BEEPER_SEEKER` pair, decoded in
[../org/ordnanceTypes.md](../org/ordnanceTypes.md) "The beeper and the seeker, which are one weapon
in two halves", as three types in one file. `IBeeperSubject` is the three facts a tag reads off its
aircraft, implemented by `FlightController` through a partial declaration here so the registry and
its tests run without an engine. `BeeperTagRule` is the static arithmetic: the countdown with its
dead-aircraft slam, the inverted alignment dot, and the running-best comparison with its four
literals. `BeeperTags<TAircraft>` is the world registry holding every creation gate, the per-step
list with its tail, and the seeker's per-frame `PickTarget`. Read `Projectile.cs` next.

## src/Flight/CamParams.cs
One aircraft's camera tuning out of `camparam.json`
([../formats/camparam.md](../formats/camparam.md)): the `default` block with the plane's own block
layered on top. Seven of the eleven airframes carry one; the other four take the 13.0 default.
Mirrors `PlaneStats.Load`'s shape (the same `Load(zrdrPath, planeNodeName)`, the same nearest-wins
resolution) and like it is loaded once per distinct plane and cached by `GameSession`. `FromData`
says whether the values came out of the file, so a partial extraction still flies and the log
distinguishes a value the data states from the built-in fallback. Read `CameraController.cs` next.

## src/Flight/PilotViewMode.cs
The three views a pilot can SELECT, valued as the engine's own camera modes (Chase 0, Cockpit 6,
Nose 7), and `PilotView`, the pure rules over them: the cycle key's three-stop order, the
first-person test the anim data's `PLAYER_1ST_PERSON` condition is answered with, whether a held
numpad key overrides the camera in this mode at all (`HoldsFixedViews`), the view in force this
frame, and the `--view=` spelling. Engine-free, so the decisions unit-test without a camera, while
`CameraController` holds the state and the `Camera3D` they act on. Decode:
[../org/cameraViews.md](../org/cameraViews.md).

## src/Flight/CameraController.cs
The flown aircraft's camera: the roll-following chase pose, the numpad fixed views, the look-behind,
the right-stick look-around, the authored crash cut and the weapon lab's held-airframe orbit, plus
the pilot's selected view mode (`PilotViewMode` decides, this class holds the state and the camera).
The chase radius is per plane and dynamic, `Dist + DistFactor·V` with an acceleration transient, and
`EffectiveRadius` applies the numpad zoom trim to every external pose. Cockpit and Nose mount
rigidly at the authored `cockpit_camera` marker with `Head`'s angles composed in and their own FOV;
every other pose restores the FOV the camera carried at construction. Steers a `Camera3D` it does
not own, `FlightController` its only host. Decode: [../org/cameraViews.md](../org/cameraViews.md).

## src/Flight/HeadLook.cs
The pilot's head in the two first-person views, decoded from the original's shared look controller.
It holds the TARGET angles the input sets and the SHOWN angles chasing them exponentially, 3.0/s in
elevation and 5.0/s in azimuth, and `Step` picks this frame's target before always chasing it, so
the snap, free-look, the centre key and autohead all reach the eye through one law. The three input
paths are the original's: a snap direction mapped through `SnapTargets`, free-look integrating the
targets at a fixed 2 rad/s along the normalised input direction, and the centre key zeroing both.
`IdleAim` is the no-input hook `AutoheadTarget` fills. Engine-free apart from `Mathf`; owned by
`CameraController` as `Head`, and stepped by `FlightController` on the sim clock.

## src/Flight/CockpitVisibility.cs
The per-mode node hiding the original applies to the pilot's OWN aircraft while a first-person view
is on the screen: Cockpit draws `cockpit1` and hides the `healthy` body, Nose hides the interior,
the body and the `markers` and `dontmove` groups, and every external pose renders the plane as it was
built. `Rules` is the whole decision as a pure function over `(PilotViewMode, firstPerson)`, so it
unit-tests engine-free; `Bind` finds the four groups in a built plane model and `Apply` writes one
frame's answer onto them. `FlightController._Process` calls it beside the camera write, keyed to the
pose that frame actually took, so a look-behind brings the body back while it is held. Decode:
[../org/cameraViews.md](../org/cameraViews.md).

## src/Flight/CockpitOverlay.cs
The cockpit interior's own render pass, behind `--cockpit-pass` and off by default. It re-parents
the built `cockpit1` node into a `SubViewport` with a `World3D` of its own, on the mount basis
`PlaneBuilder` gave it, and puts the pass camera at that world's origin aimed by
`CameraController.FirstPersonPose` with the plane position and `cockpit_camera` offset both zero,
since those cancel between eye and panel, so the projection is the main world's exactly. The
transparent-backed viewport sits on `HudLayers.CockpitPass`; its lighting is a re-aimed copy of the
world's sun plus a duplicated Environment that enhanced graphics mode extends with shadow settings.
`GameSession.BuildCockpitPasses` builds one per `PlayerRig`; `Sync` follows the interior `Visible`.

## src/Flight/CockpitGauges.cs
The authored instrument panel inside the pilot's own `cockpit1` interior: five needle nodes take an
absolute angle each frame, the artificial-horizon ball and the compass drum take the engine's own
basis, and the belts, damage-zone skins, readouts and two warning lamps take the state their
condition says. All of it reads `GaugeCluster`'s already-computed values rather than re-deriving
them, so the 3D panel and the screen-space dials cannot disagree. `Bind` finds the nodes in one
built interior (null on any plane without one) and `Apply` is the per-frame write, the same shape
`CockpitVisibility` uses, called only while the interior is on the screen. Node names, the
authored-rotation rule and the per-airframe name variants: [../formats/hud.md](../formats/hud.md).

## src/Flight/ImpactOutcome.cs
"What should happen when this weapon hits this surface id" as a value: the effect name and which
`IMPACT` slot it came from, the sound, the stand-in burst and the damage/blast-radius pair.
`Resolve` is the whole decision, with no Godot type, no scene and no sound archive behind it, so a
unit test reaches the dispatch directly; `ImpactSuppression` is the mask a weapon's impact hook
returns. `ProjectilePool.Impact` reads the struck surface id and calls it, `Apply` performs the
answer and decides nothing, and `ProjectilePool.HasBlastDamage` is the weapon-level spelling of the
same rule. The stand-in ladder and the `default`-row backfill are decoded at their own members.
Decode: [../org/weaponImpact.md](../org/weaponImpact.md), [../formats/weapons.md](../formats/weapons.md).

## src/Flight/Projectile.cs
`ProjectilePool`, the shared-world weapon-fire subsystem: a fixed pool of rounds integrated off the
weapon data (launch and inherited velocity with its decay, acceleration, gravity, the steering step,
the two fuses and the three end conditions), the swept hit ray over world and aircraft, and the
impact that follows. It owns the visuals too, crossed-quad tracers and their tip discs, the muzzle
flash triad with its casing, smoke and light secondaries, the per-surface `IMPACT` effect, sound and
stand-in burst, and the water splash's authored playback. Damage and presentation leave through the
sinks a session assigns (`DamageSink` behind `WorldDamageGate`, `EffectSink`, `WashSink`,
`BeeperTags`); a burst gathers world bodies and aircraft into one nearest-first, cover-tested
candidate list. The `Collect*` methods are the seams the aim assist and a scripted run read the
pool's own state through. Two rules are the remake's own and are recorded where they bind: the
inherited-velocity decay is not gated on a held target (`InheritedFraction`), and the tracer's
pixel floor draws rounds the data's LOD would cut ([../org/tracers.md](../org/tracers.md)).

## src/Flight/ProjectileFlyoutAnim.cs
The `FLYOUT` `MODEL_ANIMATION` half of `ProjectilePool`, a partial-class file. Every ordnance round
runs its own `AnimInstance` of the weapon's def (`he_rocket`, `sonic`, `torpedo_trail`) on the real
sequence interpreter with the pool standing in as the `ISequenceHost`: `StartFlyoutAnim` poses
`RESET_STATE` and fires the t=0 events inside `Spawn`, and `AdvanceFlyoutAnim` runs the instance
each sim step once the round has moved. `Dispatch` covers the kinds these defs author (node
visibility and scale, from-to tweens, spins, puffer trails, sounds, sequence and animation calls)
and logs anything else once. The trail puffers, the sonic's body roll and the torpedo's launch look
all come off this instance. Decode: [../org/ordnanceTypes.md](../org/ordnanceTypes.md).

## src/Flight/WarningShotCue.cs
The incoming-fire near-miss cue's shipped accumulator (player.json `warning_shot_max` /
`_dissipation` / `_interval`), plus the swept-segment-to-point distance a round's step is measured
with, lifted out of `FlightController` so both unit-test without a live node, the same split
`WeaponCursor` uses. One pass accrues 1.0, saturating at the max, drains at the dissipation rate,
and the cue re-triggers no faster than the interval; the pass radius is a tune explained at its own
declaration. `ProjectilePool.NearMissTargets` is the registry whose aircraft each round's actual
travelled segment is measured against, its own shooter excluded. Read `Projectile.cs` next.

## src/Flight/AiNetFollower.cs
Walks an `AiNet` patrol graph as a waypoint stream, aircraft-agnostic on purpose: positions in and a
target node out, with `AiPilot.Patrol` and `ZeppelinMotion` its two consumers. Given the vehicle's
nose it is the decoded walk, seating on the nearest node and flying the far end of the edge whose leg
best lines up with that nose, then repeating that pick at every arrival with the edge just flown
excluded, which is what makes a group sharing one net fly in formation. Arrival is measured along the
leg rather than as a capture sphere. It also holds a net's live stop-point flags and the structural
dead-end hold, both gated on `ObservesStopPoints`, and `Reseat` is the original's activation snap.
Decode: [../org/aiPilot.md](../org/aiPilot.md). Read `AiPilot.cs` next.

## src/Flight/DangerZoneRibbon.cs
The decoded danger-zone run ([../org/aiPilot.md](../org/aiPilot.md) "The danger-zone run"),
engine-free: `DangerZoneRibbon` is one `dzpathN` route as the original builds it, the polygon's
vertices joined by cubics parameterised in metres plus the lane table; `DangerZoneRun` is a pilot's
cursor on it, entered from the nearer end, walking the segments either way and `Done` past the exit;
`DangerZoneRail` is the state-5 integrator that writes the pose off the ribbon in place of the flight
model, closing the aeroplane's residual offset, banking the wings into the bend and settling on the
cruise speed. Every constant is read out of the image and named at its declaration. Pinned by
`DangerZoneRibbonTests`. Read `DangerZoneRibbons.cs` for the set a mission carries.

## src/Flight/DangerZoneRibbons.cs
A mission's ribbon set read straight off the chapter gamez, independent of `--debug-dzpaths`: every
`dzpathN` node's route polygon by the route-versus-gate-pair material rule
([../formats/missions.md](../formats/missions.md)), never by polygon index, its children as lanes,
and `dzones.zrd`'s `disable` list as the inactive flag. `ByIndex` serves a numbered net tag and
`NearestEnd` the negative one. One instance per session, shared through `AiPilot.DangerZones`,
because lanes are occupancy-counted across pilots; `CampaignDirector.Attach` builds it and hands it
to every roster pilot. Read `DangerZoneRibbon.cs` for one route's geometry.

## src/Flight/ZeppelinBroadside.cs
The pure zeppelin broadside law, engine-free: the decoded 90 degree arc against the moving hull's
lateral axis, where side alternation is geometric and the opposite cones never both bear; the
per-cannon stowed, deploy, ready and fire machine, whose `Step` emits the deploy, retract and ready
lists on durations taken from the authored anim defs, with its own re-fire timer; `TryAim`, which
consumes `AimAssist.TryIntercept`; `PickGasbag` and `FirstLiveTarget`, the decoded candidate walk in
authored order with no team or hostility read; and `CannonsEngaged`, the decoded byte the mission
script writes, while clear of which nothing deploys. `Session/ZeppelinRuntime.Cannons.cs` wires it.
Chain: [../formats/mission-entities.md](../formats/mission-entities.md).

## src/Flight/ZeppelinDamage.cs
The pure zeppelin kill arithmetic, engine-free: `Survivors` and `IsDead` over the record's `healthy`
list, counted literally entry by entry because a shipped chapter names one gasbag twice; the
`AliveEngines` recount; `MayDamageGasbag`, the gasbag-only routing gate; and `CrossedStages` over the
record's stage list, where every crossed threshold fires once. The zone pools live in
`DestructibleRegistry` and `Session/ZeppelinRuntime` supplies the aliveness views. Pinned by
`ZeppelinDamageTests` and the `zeppelin-damage` suite. Read `ZeppelinMotion.cs` next.

## src/Flight/ZeppelinMotion.cs
The kinematic zeppelin motion law: flies a `ZeppelinDef` along its net through `AiNetFollower`,
forward-only along the facing, speed by `max_accel` toward `max_speed`, and yaw and pitch through the
decoded per-axis steer law, whose commanded rate eases inside 25 degrees of error and whose angle
advances scaled by the fraction of authored speed the hull is making, so a stopped hull cannot turn.
There is no per-step pitch band, the record's pair being degree-valued and compared against radians.
The stop-point half is the decoded approach, cutting the throttle inside the follower's hold distance
and decaying the pose onto the node, while a follower still on its seat station-keeps. Steering:
[../formats/mission-entities.md](../formats/mission-entities.md). Pure state, no `Node`.

## src/Flight/AiPilot.cs
The non-player `FlightModel` driver: standing orders in (heading, altitude, throttle, an optional
`Patrol` net follower, an optional `Gunner` whose live target is chased at the decoded lead offset,
an optional `Machine` and an optional `Escort`), one `FlightInput` per sim step out, read by a
`FlightController` whose `Pilot` is set. A `Machine` is stepped first and picks this step's aim point
and parameter table; an `Escort` whose leader is in play takes the dispatch away from every mode but
stunned and avoid crash, which is the original's own wingman fork. `Stun` is the AI stun's entry,
leaving the throttle lever where it was so the aircraft coasts under power. Pure and seeded, so a
fixed-dt run is deterministic. Decode: [../org/aiPilot.md](../org/aiPilot.md).

## src/Flight/AiControlLaw.cs
The original's own AI steering law, documented in
[../org/aiControlLaw.md](../org/aiControlLaw.md); read that page before changing anything here. An
aim point, that point's velocity and one of four parameter tables read out of the image in, one
`FlightInput` out: a desired speed from the aim point's own speed plus range-weighted lead terms, an
intercept solve for the direction, bank-to-turn with the elevator joining once the bank command is
inside a deadband, a wings-level rule, a low-speed unload, and a per-axis scale and limit stage off
`PlaneStats`. The throttle lever has one path, the walk toward the desired speed; the original's
distance-gated open-loop branch is not ported. Engine-free and pure over its arguments.

## src/Flight/AiEscort.cs
The formation-escort law a netless `mode wingman` aircraft flies, which in the shipped data is the
campaign's `wingman_N` and `bswingman_N` roster blocks and nothing else. A leader snapshot (position,
attitude basis, velocity, whether it is the player) and an optional target snapshot in, one station
point and that point's velocity out, over the engine's own five-state machine: close on the leader,
hold the body-frame station, fly a station on the target, and the two re-join states nothing in the
law enters. Every constant is decoded, the two stations included, and the separation push is what
makes the hold a weave rather than a tight join. Engine-free and deterministic; the driver is
`AiPilot.Escort`. Decode: [../org/aiPilot.md](../org/aiPilot.md) "The escort law".

## src/Flight/AiModeMachine.cs
The nine-mode AI state machine, owned by `AiPilot.Machine` and stepped from its `Next`: the mode list
and vocabulary are the engine's own debug-readout dispatch. Decoded and wired are the activation into
pursue inside the shipped distances, the steady-hand roll a hit provokes and the break-off a failed
test causes, the sixth-sense roll and its stun, the `Stun` entry every stun source shares, which
enters `Stunned` from any mode and overwrites a remaining stun rather than capping it, the seeded
library draw an evasive maneuver plays to `ManeuverExecutor.Done`, the rubber-band `lay off` mode
`--no-assist` disables, and `avoid crash`'s three altitude bands. Engine-free, with named inventions
marked at their declarations. Decode: [../org/aiPilot.md](../org/aiPilot.md).

## src/Flight/ManeuverExecutor.cs
Plays one library maneuver's timed step program as `FlightInput` values: `Next(model, dt)` each sim
step until `Done`, consumed the way `AiPilot` is, with the state machine holding one per
`evasive maneuver` run and switching back to its own law on `Done`. Steps are target attitudes in
degrees rather than stick deflections or rates, composed onto the entry frame, which is the level
entry-heading frame normally and the full entry attitude for a `relative` maneuver; a
positive-duration step is held for its time and a zero-duration step advances when the attitude is
captured. Pure and engine-free, deterministic on a fixed dt (`ManeuverExecutorTests`).

## src/Flight/AiGunner.cs
The AI's forward-gun gunnery: per sim tick the host `FlightController` hands it the fire geometry
(`Solve`), it answers with the trigger (`WantsFire`) and the intercept, and each round leaves along
`ShotDirection(muzzlePos)`, the line from that barrel to the intercept point so wing guns converge,
perturbed inside the dead-eye cone by one seeded draw per shot. Gates in the engine's order: the
quick-draw cone off the target's nose-tail axis, the separation inside the slot's authored engagement
window, then the airframe's traverse clamp on the lead with the residual the clamp leaves gated in
turn, so the employable cone is the traverse limit plus that gate. Engine-free; the live half is the
`ai-gunnery` suite. Decode: [../org/aiPilot/aiWeapons.md](../org/aiPilot/aiWeapons.md).

## src/Flight/AiRocketeer.cs
The AI's ordnance employment, the gun path's twin: per sim tick the host `FlightController` ages the
vehicle-wide lockout and hands over the fire geometry (`Solve`), which answers with the trigger, the
hardpoint it chose and the direction the round leaves along, the clamped mount aim rather than the
raw lead. Gates in the engine's order: the quick-draw cone aborting the whole pass, then per pylon
the armed check, the two-way `DAMAGES_ZEPPELIN` match, the squared engagement band and the traverse
clamp's residual against an aim-quality cosine tighter than the gun's. The lead is solved per pylon
in the frame that round flies in, and each unlocked pass leaves a verdict behind, keyed without its
numbers so a host logs a gate change. Engine-free. Decode: [aiWeapons.md](../org/aiPilot/aiWeapons.md).

## src/Flight/AiVoiceDispatcher.cs
The combat-voice trigger dispatch, engine-free
([../formats/combat-voice.md](../formats/combat-voice.md)): events in, speaker, clip and outcome
decisions out, over the talker roll, the hardcoded halving on the bearing ids, the broadcast speaker
election where a failed roll passes to the next candidate, the damage tiers taken most-severe-first,
the death cries with force, and the computed bearing trigger id. Availability comes from the injected
resolver rather than from def presence. Pinned by `AiVoiceDispatcherTests` and the `ai-voice` suite.

## src/Flight/AiTargetRanking.cs
The decoded target-ranking formula ([../org/aiPilot.md](../org/aiPilot.md) "Target acquisition"): a
rank built from a weight, the distance and an objective bias, and MINIMISED, with the player carrying
a lower base weight than everyone else, a wingman a higher one, a gasbag a lower one, ±0.2 terms for
ahead/behind on a half-metre deadband, altitude sign and closing, and an effectively infinite rank
beyond the activation radius. Snapshots in, index and score out, engine-free. `SelectBest` prefers
the best candidate no ally already holds and falls back to the overall best when the pool is
exhausted, and `ObjectiveBiasFor` matches `rating_biases` patterns, first match wins, saturating at
always-target and at exclusion.

## src/Flight/PursuitQuarry.cs
The flight law's snapshot of `AiGunner.Target` for one step, whatever its class: position, velocity,
the nose axis of an aircraft or zero for a turret or structure, and the aircraft-only facts the merge
rule, the lay-off assist and the sixth-sense trigger read. `Of` is the one place a standing target
becomes this shape, so a pilot pursues a zeppelin engine through the same arm it pursues a fighter,
which is the original's `Target` vtable read ([../org/aiPilot.md](../org/aiPilot.md) "What pursue
does with a non-vehicle target").

## src/Flight/IncomingFire.cs
`--incoming[=metres[,wep_id]]`, the near-miss test rig: a phantom shooter on each player's six,
alternating sides, firing the target's own gun or a named weapon into the shared pool under a shooter
identity no player holds. It exists for deterministic near-miss testing, since an AI gunner is a real
shooter but aims to hit and a splitscreen pilot needs a second human. It aims along the target's own
nose with a lateral offset, so the round overtakes on a parallel track and the pass distance holds
with no lead maths. Read `WarningShotCue.cs` for what the near miss feeds.

## src/Flight/PhysicsConstants.cs
`PhysicsConstants.NomGravity`, the single player.json `nom_gravity` value shared by
`PlaneStats.Gravity`'s default and `ProjectilePool.WorldGravity` so the two cannot drift apart.

## src/Flight/PlaneStats.cs
Typed per-plane stats ([../formats/vehicle.md](../formats/vehicle.md)), the one reader every flight
consumer takes its numbers from: vehicle.json `dynamics` resolved through the `kind_of` def chain,
the def's `turrets` block as `TurretMount`s, engines.json stock engine power, and player.json's
globals, which are the flight constants plus the tuning the cue, aim-assist, head-look and damage
paths read. It also carries the `crash` block's restitution ceiling, the engine sound defs and their
curves, `destroyable_parts` as `DestroyablePart` records with the def-level injure anims, and the
`collision` probe list. `Load` resolves down the player chain, `LoadForAi` takes only the damage
model off the AI chain, and the `With*` family layers roster, difficulty and hangar overrides on.

## src/Flight/SpawnPoints.cs
Reads the flight spawn from a mission's OWN zrdr, a different archive than the shared `--zrdr`, in
two schemas both yielding `SpawnPoint(Position, HeadingDeg)`: `LoadIa` for the instant-action
`spawn_points` per scenario, which only some folders carry and the original picks one of at random
per launch, and `LoadPlayerInit` for the story objectives' `PLAYER_INIT` position and yaw. Schema:
[../formats/spawns.md](../formats/spawns.md).

## src/Flight/MissionTargets.cs
A mission's `targets.json` as one table: target key to its objective display keys
(`description`, `category_label`, `help_label`, resolved through `Messages`) plus the valueless
`objective`/`other_target` marker flags the mission starts with. A bare node name keys itself and
a nested `[parent, child]` entry keys `parent/child`, the same spelling `ObjectiveTarget` gives
the script's directives, so the two tables meet on one string. `Load(mission, chapter)` walks the
original's reader search path, and `ByNode` exposes the whole table for a consumer that wants the
starting flags rather than one key's labels. Schema: [../formats/missions.md](../formats/missions.md).

## src/Flight/MarkerDraw.cs
The world marker's drawing primitives, shared by `MarkerHud` and `TargetHud`: the shadowed
reticle, the edge arrow with its tail stroke, and the centred text block with the pane-clamped
variant an off-screen marker needs. Owns the marker blue and the drop shadow, while colour and
scaled sizes stay with the caller, since each HUD scales through its own `HudMetrics.Scale`.
Where a marker goes is `EdgeMarker`'s, which is the module to read next; this is only what it
looks like.

## src/Flight/StuntMission.cs
Stunt Flying's per-pilot run state: `Load` builds the ordered danger-zone list from a mission's
ia.json `dzones` (HUD positions, gate polygons, strings through `MissionTargets` and `Messages`,
null where a mission authors none), `Update` requires both polygon-plane crossings in either order
and advances the target, and `Elapsed`, `CompletedAt`, `CompletionOrder` and `InCompletionOrder`
carry the clock and the splits. `ForAnotherPlayer()` clones an independent run so the archives
parse once per session. Engine-free apart from its logging, which is what lets its coverage run
off the engine. Read `MarkerHud` for what a run draws and `StuntScoreboard` for what it scores.

## src/Flight/HudMetrics.cs
The one place the flight HUD decides how big it draws: `Scale(control, reference = 1440)` is
window height over the reference, damped by `PaneFactor`, the square root of pane height over
window height, inside a splitscreen pane. `hud.statusTextScale` and `hud.markerTextScale` multiply
only their matching flight-HUD text within a clamp, leaving arrows and layout at the base scale.
Every flight-HUD control routes through it, so a sizing change lands in one place.

## src/Flight/HudFont.cs
The game's own HUD bitmap font, rebuilt from `extracted/rimage/5pointhud.png` and the brighter
`5pointhudbrite.png` highlight variant: a proportional five-pixel font covering printable ASCII.
`Load` returns null with one log line where the atlas is absent, and `Draw`/`Measure` render onto
any caller's canvas at a scale `HudMetrics` supplies. Layout and colours:
[../formats/hud.md](../formats/hud.md); `HudFontTest` is the overlay that proves the renderer.

## src/Flight/HudFontTest.cs
The `--hud-font-test` verification overlay for `HudFont`: a known string drawn in the normal and
highlight variants near the top left of each player's pane, sized through `HudMetrics` so a single
view and a splitscreen pane can be screenshot and compared. A thin rule under each line marks
`HudFont.Measure`'s reported width, which is what confirms the metric agrees with the glyphs
actually drawn. Not part of the flight HUD; it is added only when the flag is set.

## src/Flight/ImpactReticle.cs
The gun aiming reticle: a viewport-filling `Control` drawing the game's own `impact_point.png`
pipper at a world impact point `FlightController` feeds it each frame, projected through
`Camera3D.UnprojectPosition` at draw time rather than cached. Fixed screen size scaled by
`HudMetrics`, one per player pane. What the pipper follows, and why it is deliberately not the
aim assist's line, is decoded in [../org/aim-assist.md](../org/aim-assist.md).

## src/Flight/EdgeMarker.cs
The off-screen edge marker's placement rules, engine-free and pure: `Resolve(projected, behind,
paneSize, margin)` answers on-screen against edge-clamped as a `Placement` (the margin-inset rect
test, the behind-the-camera mirror, the degenerate-direction fallback and the clamp to the inset
boundary), and `ClockHour` is the bearing in hours. Owns `RefEdgeMargin`. The camera stays with
the callers: `MarkerHud`, `VersusHud` and `TargetHud` project through their own pane and keep
their own arrow, tag and label styling. `MarkerDraw` draws what this places; off-engine coverage
is `CSVM.Tests/EdgeMarkerTests.cs`.

## src/Flight/MarkerHud.cs
The stunt objective marker HUD: a viewport-filling `Control` drawing the on-screen reticle and
text block, the off-screen edge arrow with its bearing, the run status line and the completion
banners, one per player and sized through `HudMetrics.Scale`. Placement is `EdgeMarker`'s and the
primitives are `MarkerDraw`'s; what it reports is `StuntMission`'s.

## src/Flight/ResultsBoard.cs
The shared shell every results board is built on (`StuntScoreboard`, `StuntRaceBoard`,
`VersusBoard`, `IaWrapupBoard`): the dimmed backdrop and centred panel, the board palette and
label factories, the halt-and-retire contract on the sim clock, and the standard Photo Mode,
Restart and Exit menu. A subclass keeps only its build signature, its completion event, its
populate content and its still-ended test. `PauseBoard` shares the chrome statics but not the
shell, since a held clock is not an ended run.

## src/Flight/StuntScoreboard.cs
Stunt Flying's end-of-run results overlay on `ResultsBoard`'s shell: the plane and chapter heading
over a `StuntSplits` section of per-zone splits, total and best-time comparison. Wakes on
`StuntMission.RunCompleted`, records through `ScoreStore.RecordIfBest` and logs the split table so
a headless run is reviewable. The one per-pane board among the results boards, which is why it
overrides the shell's whole-window placement. Read `ResultsBoard` for the shared shell and its
halt contract, and `IaWrapupBoard` for the board Instant Action carries the splits on instead.

## src/Flight/StuntSplits.cs
The stunt run's split section, shared by `StuntScoreboard` and `IaWrapupBoard`: the per-zone rows
in the order flown with split and cumulative times, placeholder rows for zones never reached, the
total, and the new-best or stored-best comparison line. `StuntSummary` is the value a board hands
it, one run with its total and the stored best. A single flag keeps the two boards' shipped
layouts apart, since the scoreboard rules off its total and the wrap-up board runs the table
straight into it.

## src/Flight/ScoreStore.cs
Stunt best-time persistence: one JSON object in `user://stunt_scores.json` keyed
`chapter/mission/plane`, with `GetBest` and `RecordIfBest`, which never worsens a record and
answers whether the run was a new best. The public `Load()` always opens the player's own file;
the internal path overload exists only so a suite can point at a throwaway directory instead.
`CustomPlaneStore` is the same file-backed shape for a heavier record.

## src/Flight/StuntRace.cs
Splitscreen stunt-race bookkeeping: one `Racer` per player over their own `StuntMission`, with
finishing stamping the next placing, `RaceCompleted` firing once the last pilot is in, and
`Standings()` ordering finishers by placing then in-flight players by progress. `Restart()` resets
every mission and clears the placings, leaving the respawn to `GameSession`, which owns the
planes. Membership is append-only apart from the internal `Remove`, which compensates an
uncommitted roster build. Off-engine coverage: `CSVM.Tests/StuntRaceTests.cs`. Read
`StuntRaceBoard` for what a finished race draws.

## src/Flight/StuntRaceBoard.cs
The race's shared ranked results overlay on `ResultsBoard`'s shell: one row per player from
`StuntRace.Standings()` with placing, tag, plane, zones, total and gap to the winner, and a DNF
row for an unfinished run. Whole-window rather than per-pane, since a race ends for everybody at
once. Wakes on `RaceCompleted` and retires once `AllFinished` clears, so the rematch is reachable
without going through the menu. `StuntScoreboard` is the single-pilot form of the same table.

## src/Flight/VersusMatch.cs
Dogfight deathmatch bookkeeping, engine-free: `RegisterKill` scores the shooter and tallies the
victim's death, `RegisterDeath` tallies a death with no killer and no score change, `Advance(dt)`
is the host-fed match clock, `MatchCompleted` fires once on the kill threshold or the time-out
(leader wins, equal top kills draw), `Standings()` ranks by kills with ties sharing a rank, and
`Restart()` zeroes every score and re-arms completion. Off-engine coverage:
`CSVM.Tests/VersusMatchTests.cs`. Read `VersusHud` and `VersusBoard` for what it feeds.

## src/Flight/VersusHud.cs
The per-pane Dogfight HUD: a compact status line (remaining time, this pane's kills and deaths,
the leader's tag) in `MarkerHud`'s run-status slot, a transient kill banner, and one marker per
living opponent rig, either an on-screen tag or `EdgeMarker`'s arrow and bearing in that
opponent's own `SplitScreen.PlayerColor`. `Build` binds the match and this pane's own camera;
`HumanFlightAdapter` attaches the live rig list and `FlightController` feeds the pose each frame.
Kills arrive on a subscription of their own to the session's broadcast, so every pane hears every
one. The per-opponent marker is CSVM's splitscreen answer to the original's radar; the shape's
provenance is in [../org/targeting.md](../org/targeting.md).

## src/Flight/TargetHud.cs
The per-pane targeting HUD, built on every human pane in every flight session: the pilot's own
selection from `TargetSelection` (a campaign mission's objective sites included, since they ride
that same selection), a nearest AI-hostile fallback where no selection exists, and
`--debug-markers`' every-aircraft overlay. Draws the original's bracket box and label block and
owns the colour table, the label layout, the selected gun's reach gate and the debug identity
string. Placement and the bearing are `EdgeMarker`'s and the primitives `MarkerDraw`'s; only the
styling is this HUD's own. The marker's decode is [../org/targeting.md](../org/targeting.md).

## src/Flight/VersusBoard.cs
The Dogfight results overlay on `ResultsBoard`'s shell: the winner in their own
`SplitScreen.PlayerColor`, or a draw on a tie, over one ranked row per player with tag, kills and
deaths from `VersusMatch.Standings()`. Whole-window, because the match ends for everybody at once.
Wakes on `MatchCompleted` and retires on the rematch; the rows are populated only from that
completion, so they stay the ones the match ended with even after `Restart()` zeroes the live
state. Restart routes through `GameSession.RestartMatch`, which the keyboard and pad shortcuts
reach directly while the board is up. `StuntRaceBoard` is the same construction over a race.

## src/Flight/HaltReason.cs
Why the sim clock is stopped, as a flags set: `Paused`, which a player asked for and which carries
an owner, and `Ended`, which a results board raises. The clock advances only when the set is empty,
which is what lets two systems halt it at once without either resuming it out from under the
other. `PauseState` arbitrates and nothing else writes a reason.

## src/Flight/PauseState.cs
Who is holding the sim clock and why, engine-free in `VersusMatch`'s shape: one instance per
session, built by `GameSession` ahead of the rig loop and assigned to every rig's
`FlightController`. `TryToggle(playerIndex)` owns the pause half, claiming ownership on the way in
and resuming only for the owner, and it is refused outright while `Ended` is set. `Raise` and
`Clear` own the results-board half and reject `Paused`, which carries ownership and must go
through `TryToggle`; `ForceResume` drops a pause whoever owns it, for a rerun or an exit chosen
from a menu. Off-engine coverage: `CSVM.Tests/PauseStateTests.cs`. Read `PauseBoard` next.

## src/Flight/PauseBoard.cs
The shared pause overlay, whole-window because pausing stops the game for everybody at once. Built
once by `GameSession` on the shared board layer and wired to `PauseState.Changed` rather than a
completion event, it shows the pausing player's tag in their own colour and a Resume, Restart and
Exit menu driven by that player alone, since `PauseState` lets only the owner resume. A fresh menu
each pause, so the cursor starts on Resume and a stray confirm cannot destroy a run. It shares
`ResultsBoard`'s chrome but not its shell. A campaign session's objectives readout rides the same
pause on a layer of its own, since it belongs to the flown mission rather than to every mode.

## src/Flight/IaWrapupBoard.cs
Instant Action's wrap-up board on `ResultsBoard`'s shell, whole-window since the mission ends for
every human at once: four label and value rows for time to complete, enemies shot down, danger
zones completed and shot percentage. It takes no live match object at all, only the caller's own
snapshot handed in once by `InstantActionRuntime`, so `InstantActionDirector` owns every source
and this class draws what it is given. On a stunt mission it also grows a `StuntSplits` section,
and the per-pane scoreboard is then not built. Its Restart reaches the Launcher's session restart
and rebuilds the world, because a mission's waves, ace and zeppelin cannot be put back in place.

## src/Flight/Weather.cs
`WeatherState`, the flown mission's own weather.json as per-zone `ZoneWeather` records: fog colour,
ranges and altitude, the sunlight block resolved into a world light, a sun orientation and its two
uncollapsed colours, the cloud-cover whiteout band, wind, and precipitation. `DefaultDiffuse` and
`DefaultAmbient` are the install's modal day pair, public because both lighting mappings anchor a
zone against them. `ResolveZone` picks the flown zone by name, falling back to the one zone whose
horizon subtree carries meshes where the requested one is empty and this mission also fogs it;
`CameraWeatherState` and `ZoneForState` are the per-frame camera zone `WeatherRig.Tick` publishes.
Schema: [../formats/weather.md](../formats/weather.md); runtime: [../org/weather.md](../org/weather.md).

## src/Flight/FlightAudio.cs
The own plane's non-positional audio: the engine, overspeed whine and rattle loops, plus the
one-shots a crash, a ground or water explosion, a survivable graze and an engine stop fire, each
drawing the sound the chosen crash or touchdown definition itself authors rather than a fixed
name. `OnWarningShot` draws a near-miss variant from the group player.json names; the rate limits
live on `FlightController`, not here. The engine is one voice on one slot whose pitch, gain and
definition all come from `EngineAudioCurves`, and `MixGain` is the only own-ship scale left, for
splitscreen. `AiEngineAudio` is the positional twin an AI aircraft carries instead of this.

## src/Flight/EngineAudioCurves.cs
The engine-audio slot maths both audio paths read: whether an airframe counts as damaged, which
definition the slot then holds and the swap's one-off pitch draw, each slot's pitch and gain off
the `PlaneStats` curves, the drive parameter and the cull distance. It exists because the original
runs one per-frame routine for the player and every AI vehicle. The slot's parameter is not the
throttle lever alone: the drive adds a turn rate and a climb attitude to each curve's normalised
parameter under a clamp with headroom above 1, which is why `SoundCurve` exposes its steps
separately from a plain evaluation. `AdvanceDamagedRearm` is the pure re-arm timer only
`AiEngineAudio` reaches. Decode: [../formats/vehicle.md](../formats/vehicle.md).

## src/Flight/AiEngineAudio.cs
The positional twin of `FlightAudio` that an AI-flown aircraft carries instead of it: the same two
engine slots on `AudioStreamPlayer3D`s, plus the cull that stops them past `EngineAudioCurves`'
cull distance and starts them again inside it. `Attach` is the whole spawner-side surface. It
deliberately carries no own-ship concept, since an AI kill is audible from its crash animation's
own authored sound events. The `sound` log carries the whole observable: what each slot resolved
to at build, then one line per cull transition and one per damaged-engine swap. The healthy to
damaged edge waits out the shared re-arm timer on `PlaneStats.DamagedTimer`; the other direction
is immediate.

## src/Flight/SpectatorCamera.cs
The `--freecam`/`--anim-lab` observation camera: WASD move, RMB-held mouse look, wheel speed and
pads through `Pads.For(_padDevices)`, every key and pad read resolving through
`InputContext.Camera` (`src/Bindings/`). The lab additions are `Frame(Aabb)`, the `FollowNode`
orbit lock (released by any translation input, re-locked by the target key through `OrbitLock.Next`
over `GameSession.LockCandidateAircraft`'s roster) and a public `Camera` accessor; while locked the
orbit answers the right stick and the triggers as well as the mouse. It is also the pane a pilot
out of the mission watches from (`Session/SpectateHandoff.cs`), with `padDevices`/`useKeyboard`
filtering each watcher to its own seat so two watchers move independently. Read `OrbitLock.cs` next.

## src/Flight/OrbitLock.cs
`SpectatorCamera`'s re-lock rule, taken as positions and an index rather than nodes so it runs
off-engine (`OrbitLockTests`), the same split `Pads.AssignPads`'s pure overload uses. `Next` answers
the nearest target to the eye from no lock, the next one outward from a held one, and wraps past the
farthest back to the nearest, so no press is a dead one. It RANKS rather than sorts: the answer is
an index into the caller's own list, which a sorted copy would only have to undo. Ties break on the
index, so two aircraft at equal range still get distinct turns and neither is unreachable. A
`current` out of range reads as no lock, which is what a stale index resolves to.

## src/Flight/FlightModel.cs
The decoded, data-driven aircraft plant. Rotation sums stick torque, bank coupling, the
`return_rate` weathervane and the ground blow before exponential `ang_momentum_damp` decay;
`BodyRates` stores the original's quaternion half-angle rate, and `PhysicalBodyRates` and the
attitude update double it. Authored speed curves scale the stick command alone, and
`OpposingCommandLimitAt` softens the pitch and yaw commands that swing the nose off the flight path,
on the decoded G ramp and AOA window. Translation composes the original's own chain: a clamped lift
demand plus decoded Mach drag, thrust and gravity, with the velocity direction rotating only through
that lift and the ground-blow steer. `FarFieldPlant` is the original's level-of-detail branch,
re-decided every step off `FlightInput.NearestHumanDistSqM`; `Collide` is the decoded contact
response, the placement and the human-only normal impulse, with the lifecycle left to
`AircraftContactResolver`. `FlightInput.Boost` is the nitro flag, replacing the thrust lever and
scaling drag. Full decode and the parity ledger: [../org/flightModel.md](../org/flightModel.md).

## src/Flight/StickRamp.cs
The original's keyboard stick as an accumulator rather than an on/off flag: a held key ramps the
axis toward full deflection at `Rate` 2.5 per second, so full travel takes 0.4 s, and releasing or
reversing drops the axis to centre in one frame. That asymmetry is why a fast stick cadence reaches
far less deflection than a slow one, and the analogue axes bypass it entirely. Pure and
engine-free; `FlightController` steps one per keyboard axis. Decode:
[../org/flightModel.md](../org/flightModel.md).

## src/Flight/NitroSystem.cs
The original's nitro boost lifecycle, engine-free: a 30-unit tank burned at 4/s while boosting and
refilled at 1/s always, so a burn nets 3/s and runs 9.5 s from full to the 5 % cutoff. The human arm
(`HumanCommand`) engages on a held command only from a 99 % tank and re-asserts the flag until the
cutoff; the AI arm (`AiSet`) has no engage line and fires once per nitro-flagged maneuver. Both
refuse an engine-out aircraft and a re-engage while the boost or decay animation is alive.
`Installed` is the injector, and `EngagedThisTick`/`ReleasedThisTick` are the edges
`FlightController.AdvanceNitro` turns into the shake kick, the `nitro_boost`/`nitro_decay` defs and
the `snd_nitro` loop. Decode: [../org/flightModel.md](../org/flightModel.md), "Nitro".

## src/Flight/PathFollower.cs
The engine's SECOND movement law and the exclusive alternative to `FlightModel`: the dispatcher
picks between the two before any flight law runs, so nothing here is a steering input. Pure state
and maths, driven by `Session/ScriptedPathVehicles.cs`. `Following` (the path owns this vehicle) and
`Frozen` (placed and waiting) are held apart, because folding them together cannot express the state
most authored path vehicles spend a mission in. Every constant is decoded: the final leg steers at,
and ends at, the point 300 m along it from the waypoint behind, raised with speed, so a shorter
final leg flies past its last waypoint climbing (`FinalLegOvershoot`). Law, constants and the
unidentified ride-height field: [../org/flightModel.md](../org/flightModel.md).

## src/Flight/PropAnimator.cs
Spins the flying aircraft's prop and rotor blur discs. `Build` collects every node `PropParts`
classifies (rest pose plus rate, radians per second about local axes) and `Advance` recomputes each
disc's absolute pose from its stored rest pose through `SpinMotion.ComposeSpin`, the same
accumulate-from-rest decode `AnimRuntime` plays the ambient world's `XYZ_ROTATION` spins through, so
a long flight session cannot drift. `FlightController` drives it throttle-scaled with a
`PropIdleSpin` floor and zero while crashed. `--fly` only; the static viewer keeps the still disc.

## src/Flight/ThrottleSlamSmoke.cs
The throttle-slam exhaust smoke: a large sudden throttle increase streams the `nitro_boost` def's
own `nitropuffN` puffers (AT_NODE `exhaust1..4`) from the plane's exhaust markers for a few
sim-seconds, reused without the rest of that def, since the same trail-smoke shape shows on a plain
throttle jump with no boost. `Update(dt, throttle)` is an edge-triggered gate: it tracks the
throttle at the start of the current unbroken climb and fires once per climb as the cumulative rise
crosses `SlamThreshold`, never on a flat or falling throttle, so a notch at a time evaluates fresh.
It drives the puffers through `Puffer.Emit`/`Stop` in DISTANCE_INTERVAL mode; `Reset(throttle)`
hard-stops the plume and re-anchors the tracker, so a crash or respawn jump never reads as a slam.

## src/Flight/FuelTank.cs
The flown aircraft's tank, engine-free so the arithmetic is testable without a scene. `Step(dt,
lever)` takes `dt · lever · 5` off `Remaining`, clamps at zero and returns whether the throttle
lever may move this tick; false on a dry tank is what freezes the lever where it stands rather than
closing it. The lever handed in is the value entering the tick, before that tick's slew, matching
the original's ordering. `Capacity` comes from `PlaneStats.FuelCapacity` and `Fill` is the
spawn-time top-up; a non-positive capacity frees the lever, so a fixture without the authored key
still flies. Only `FlightController.ReadKeyboard` burns, the original's player-only gate, and nitro
costs no fuel, the burn reading the lever. Decode: [../org/flightModel.md](../org/flightModel.md).

## src/Flight/SpeedCue.cs
Loads `cuepuffer1..3` from the chapter's `speed_cue.zrd` and drives exactly one of them through
`Puffer.Emit` at the aircraft pose. Camera altitude selects the authored 30/15/8/15 m density bands;
within 50 m AGL none emits, while the script's empty branch above 1500 m keeps the current
selection. One instance is built per player and its puffer renderers are stamped onto that rig's
visual layer, so a splitscreen pane never sees another pilot's cue. It shares the session
`EffectAmbience`, so the authored camera-distance fade and the wind apply. `Reset` hard-clears all
three on crash or respawn, and `Dispose` removes every puffer node when roster assembly is rolled
back.

## src/Flight/ControlSurfaceMix.cs
The decoded angle solver behind the control surfaces, engine-free so the arithmetic is testable
without a scene: roll drives the two aileron slots at ∓0.5 rad, pitch the two elevator slots at
−0.6 rad with a ±0.18 rad differential roll term added, and yaw both rudder slots at −0.61086524 rad
times the reverse-authority factor. Aileron and elevator targets are clamped, the rudder is not, and
each slot then decays exponentially toward its target at 2/s, so the shape holds at any frame rate.
`Advance`'s `animate` flag is the original's player-only guard: false writes no slot at all, which
is how an AI aircraft's surfaces stay frozen. Addresses and the list population that fixes which
node takes which slot: [../org/flightModel.md](../org/flightModel.md).

## src/Flight/ControlSurfaceAnimator.cs
The node side of `ControlSurfaceMix`: collects every classified surface and poses it absolutely
(`Basis = base · Rot(hingeAxis, slotAngle · scale)`) from its build-time local basis rather than
accumulating, owning the two frame flips the original has no need of, a mount rotated by yaw π and a
nose-mounted canard that raises the nose the other way. `--fly` only, frozen while paused or
crashed, reset on respawn. `FlightController` passes its `IsHumanPiloted` as the guard, so every
human pilot animates for splitscreen while AI stays frozen as the original has it.

## src/Flight/WingLightBlinker.cs
Flashes the wingtip flares for `FlashDuration` (about one frame of the original's own footage) each
`WingLights.BlinkPeriod`, and toggles a matching `OmniLight3D` per flare, its range and colour from
`WingLights`, on the same window. Gated on the session's ANIMATION_LOD quality flag: below HIGH the
flares and lights stay off for the instance's life, which is the def's own low-detail branch.
`Reset` restarts the cycle with everything off and hands every suspended lamp back.
`Suspend(flareName)` hands one named lamp to whatever just deactivated it, which is how the fuel
leak's one-way `OBJECT_ACTIVE_STATE` deactivation survives the blink cycle; a suspended lamp is
skipped in `Advance`. Advanced each `_Process`, frozen while paused or crashed; `--fly` only.

## src/Flight/PylonOrdnance.cs
The rockets mounted under a plane's wings. `Build` instances ONE flyout model body per loaded pylon
through `ProjectilePool.BuildFlyoutBody` (the same gamez prototype the round flies) and parents it
to that pylon marker at identity local transform, which is the launch pose; `Update` shows or hides
each per its live `Hardpoint.Ammo`. `FlightController` drives `Update` after the rockets, and the
mounted body rides the plane and is freed with it. `Unmount` takes the set back off, detaching each
body from its pylon immediately rather than queueing it, so the weapon lab's rebuild-on-swap cannot
leave the old ordnance hanging beside the new. `--fly` only.

## src/Flight/PlaneCollider.cs
Derives up to eight plane-frame convex hulls from the built model's mesh triangles alone, with no
per-plane data: region-clipped geometry (tail, wing, fuselage out to the wing band), greedy
volume-guided refinement cutting one or two parallel planes per axis (the double cut separates
bilateral pairs such as twin fins), then one `ConvexHull` per refined piece. The refinement and the
part order are judged on the pieces' boxes, so a hull is only the emitted shape and never moves a
cut or a name. Single-sourced: the terrain sweep casts these hulls and `AircraftBody` mounts the
same `ConvexPolygonShape3D` resources as the plane's hittable body. `Layout` is the engine-free half
the `airframe-hull-coverage` suite measures; `Build` wraps it in shapes.

## src/Flight/ConvexHull.cs
A convex hull over a point cloud with no engine dependency: vertices, outward faces, edges, bounds
and volume, plus `Contains` and the point-to-surface `Distance` the fuse and blast passes ask for.
Construction is incremental on millimetre integer coordinates with exact 64-bit volume signs, so a
near-coplanar mesh cannot fold it. A cloud thinner than the thickness floor along an axis is padded
to it first, the per-dimension floor the box shapes applied; a cloud too degenerate to hull falls
back to its padded bounding box.

## src/Flight/ScreenSize.cs
Screen-space sizing for world-space sprites, split out of `ProjectilePool` so the splitscreen rule
it feeds is testable without a live camera. `MinWorldSizeForPixels` inverts Godot's default vertical
projection to the smallest world size that still covers a pixel target at a distance, reading the
camera's OWN viewport height, which a splitscreen pane makes shorter than the window;
`NearestFloor` takes the SMALLEST of those over several `ViewerSample`s, which is what keeps one
shared mesh from inflating in every nearer pane. Degenerate inputs read as no floor. Decode:
[../org/tracers.md](../org/tracers.md).

## src/Flight/ShakeDefs.cs
Typed reader over the shared `shakes.zrd.json`, the six shake-oscillator sources, modelled on
`WeaponDefs`: loaded once into `AircraftAssemblyResources.Shakes`, with named accessors per source
and an unhandled-key tripwire. Each source is one law (frequency, damp, sawtooth) plus exactly one
magnitude-term variant (`magnitude_factor` with an optional `he_factor`, `min_speed` with
`magnitude_quotient`, or an absolute `magnitude`); an absent source reads as null and `PlaneShake`
no-ops it. Schema: [../formats/shakes.md](../formats/shakes.md); decode:
[../org/shakes.md](../org/shakes.md).

## src/Flight/PlaneShake.cs
The plane-wobble oscillators: the gunfire buzz (`fire_bullet`), the overspeed rattle (`high_speed`),
the being-hit rocks (`bullet_impact`/`missile_impact`/`explosion`) and the nitro engage (`nitro`,
one kick of its absolute authored magnitude, human pilots only), summed each sim tick into `Roll`,
the radians the controller writes to `ShakePivot`. Engine-free on purpose, so the pivot write is the
controller's one line. `high_speed`'s input is speed over the plane's `fd_speed`, so its authored
`min_speed` gate means "beyond rated max" and the magnitude is the excess over that gate, silent at
cruise. `ContactHit` is the one oscillator no def authors: it runs on the constructor's own law and
takes its magnitude from `CollisionDamage.ContactShake`. See [../org/shakes.md](../org/shakes.md).

## src/Flight/FlightControllerBuild.cs
The internal construction handoff from `FlightRoster` to `FlightController`: one resolved
controller's pre-tree state from either flight adapter, which `Bind` consumes exactly once, before
tree attachment. The roster keeps the data resolution and the lifecycle ordering while the
controller keeps its runtime interface. `HoldSegments`, the scripted hold profile a caller may want,
rides this DTO the same way `Pilot` does, so `FlightController` exposes a public field for neither;
`Bind` copies it before resolving and storing the `IFlightInputSource`, alongside the `IWorldQuery`
seam.

## src/Flight/IFlightInputSource.cs
The seam a sim step reads this frame's pilot intent through: `Read(dt)` returns one `FlightInput`.
`PilotInputSource` and `KeyboardInputSource` are thin wrappers over the matching `FlightController`
method, while `ScriptedInputSource` carries its own state instead, the segment list and its own
elapsed clock, so a suite can construct one with no `FlightController` in the process; its `Reset()`
is what a respawn calls to restart from the first segment. `FlightController.Bind` resolves which
one flies a given aircraft from whichever of the hold segments or `Pilot` is set, and stores it,
since both arrive through the build DTO before the first sim step. Ground-blow probing and the AI
ground-blow write stay on `FlightController`, which has the live world a source does not.

## src/Flight/FlightHud.cs
Everything one pane draws for its pilot, in one module the flight node holds privately: the heading
tape, the cockpit dials and their two weapon gauges, the gun pipper, the stunt objective marker,
the targeting HUD, the `--hud-font-test` overlay and the flight text block. Nothing outside this
class writes one of them. With the cockpit interior on screen the dials, tape and text block come
off (`SetCockpitView`), its panel carrying them; the pipper and marker HUDs stay. The per-frame
entry is `Draw(in FlightHudState)`, a struct of aircraft STATE rather than readout values, so the
text, dial positions and gates are composed here and assertable with no Godot `Control`, as statics
(`ComputeStallWarning`, `MphFromSpeedMps`, `FeetFromWorldY`, `ComputeAgl`, `ComposeTextLines`).

## src/Flight/FlightController.cs
The flying-aircraft node: input through `FlightModel` to a transform (or, for an AI pilot publishing
a `RailPose`, the danger-zone ribbon's pose in place of the model step, the sweep still run), plus
weapon fire as `FireControl`'s engine adapter and the crash and respawn paths. It keeps no rule it
can delegate: the camera is `CameraController`'s, the pilot HUD `FlightHud`'s, this frame's stick
one `IFlightInputSource`, the states an aircraft moves between `AircraftLifecycle`'s, and what a
contact costs `AircraftContactResolver`'s. This node reads the devices, performs what each of those
reports, and holds the state the engine can only hold as state. Every physics query runs through the
one `IWorldQuery` bound in `Bind`, and contact detection fills one `ContactReport` from the hull
sweep, the AI probe rays or the anti-tunnelling centre ray. An AI aircraft is this SAME node with
`Pilot` driving the input source, no camera and no HUD canvas, so flight, collision, weapons and
damage are the player's path exactly. `Held`, `Inert`, `Spectating` and `CameraOwned` are the flags
a lab, a cutscene or a session pins it with. Read `FlightHud.cs` and `AircraftLifecycle.cs` next.

## src/Flight/PlaneDamage.cs
The decoded vehicle damage ledger: per-part pools from `destroyable_parts` plus a whole-vehicle
armour and health pair, authored where the def chain carries one and summed over the parts where
it does not. `Apply` is the decoded take-hit flow, spending armour first on the named zone,
redirecting a dead or unknown zone to a random survivor, recomputing the whole pair after every
part spend and draining it directly with whatever is left over, so a kill stays reachable with
every zone still healthy. The four fraction readings differ on purpose, each saying at its own
member which scale it is on. Decode: [../org/vehicleDamage.md](../org/vehicleDamage.md).

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's health-only fraction crosses an
`injure_anims` entry it shows the torn `pdpN` panel, hides the healthy skin and plays that entry's
authored anim through `DamageEffectSink`, the player rig's own runtime. Staging is keyed per
ladder entry and cleared on the upward crossing, so a repair un-stages and the entry can fire
again. `RigAnimFor` is a curated membership test over the effect catalogue, which is what keeps a
cockpit gauge def from ever playing on an airframe, and the panel-pairing traps sit on
`PairHealthySkins`. The cockpit-interior twins ride the same panel table as their exterior
partners, so no separate cockpit rule exists. Decode: [../org/vehicleDamage.md](../org/vehicleDamage.md).

## src/Flight/DamageLab.cs
The damage lab that F5 toggles: one armour slider for the parts the data gives an armour pool, one
health slider per destroyable part, and a `PartFrac` reading of health, armour or the combined
scale the mirrored gauge dial is on. `ReadSliders` floors a part's armour once its health reads
short of full, mirroring the real armour-first path, and a `--damage=` preset takes the same route.
One panel with two hosts chosen by the injected `IDamageLabTarget`: one drives `DamageVisuals` on
a parked plane, the other writes the flown plane's real `PlaneDamage` through single-pool `Apply`
calls. Neither host reimplements the visuals. The flag's own behaviour is in [../cli.md](../cli.md).

## src/Flight/CompassTape.cs
The original's top-centre heading tape, rebuilt from the game's own compass tick and text textures
as a cylindrical drum seen edge-on, headings increasing to the left under a cosine fade toward the
rim. Metrics are probe-fitted reference constants times `HudMetrics.Scale`, `Build` returns null
where a texture is missing, and the control re-anchors on resize. The heading itself comes from
`GaugeCluster`. Rendering model: [../formats/hud.md](../formats/hud.md).

## src/Flight/GaugeCluster.cs
The original's cockpit dials as a screen-space HUD: altimeter, speedometer, damage display, the
gun and missile weapon gauges and the nitro dial, all geometry extracted from the plane's own
`gauges` subtree, drawn by data priority and bottom-anchored so splitscreen panes keep them on
screen. `HeadingDeg` carries the nose heading `CompassTape` and `CockpitGauges` both read, so the
heading is computed once. `DamageZoneColor` bands the damage dial off the combined armour and
health fraction against thresholds mined from the data's own green, yellow and red `injure_anims`.
The animated arrow sweep and stall lamp are plain nested structs needing no `Control`, so
`CSVM.Tests` drives them directly. Structure and scales: [../formats/hud.md](../formats/hud.md).

## src/Flight/CustomPlaneDef.cs
A custom-built plane as a pure model: exactly the decoded 204-byte record's chosen fields
(airframe, engine, per-zone armour units, four gun slots with their twin bits, per-wing hardpoint
counts, the paint pattern with its colour, shade and decal indices, and the name), and none of the
fields the original derives at commit, which are recomputed rather than stored. Engine-free, so
screens edit it, `HangarEconomy` prices it and `CustomPlaneStore` persists it without a session.
`Ammo` and `Ordnance` carry the campaign loadout export in `OwnedPlane`'s own encoding, written
only by `SetLoadout` and left alone by `Clamp`, since their vocabulary belongs to
`CampaignLoadout`. Record layout: [../formats/paint.md](../formats/paint.md).

## src/Flight/CustomPlaneRecord.cs
Import-only reader for the original's 204-byte saved-plane files: one record, or a whole install's
`Planes` directory, into `CustomPlaneDef`s. Every paint field is read as the index it is, the
cached RGBA the engine writes alongside is left out and exposed only for a cross-check, and the
derived fields are ignored. A file it cannot make sense of reads as null rather than throwing, so
an import never breaks on one corrupt save. The read is one way: CSVM's own planes persist through
`CustomPlaneStore` and never in this format. Format: [../formats/paint.md](../formats/paint.md).

## src/Flight/CustomPlaneStore.cs
JSON persistence for `CustomPlaneDef`, one file per plane under `user://Planes/`. The store works
over a plain absolute directory through `System.IO` so it unit-tests without an engine, and
`UserPlanes()` is the single Godot touch resolving the scheme. The name is the identity, as in the
original, so saving over an existing name replaces its file. A missing or malformed file reads as
nothing rather than throwing, since a corrupt save must never break a plane picker. The current
schema stores paint as the original's index pairs, the first schema still loads and upgrades on
its next save, and the optional exported-loadout block deliberately did not raise the version.

## src/Flight/CustomPlaneBuild.cs
The join from a saved `CustomPlaneDef` onto the three things a spawn consumes, pure and engine-free
because every input is handed in. `LoadoutFor` builds over the airframe's unmutated stock fit,
turning a calibre row into a weapon the loadout bind resolves and a twin pick into one gun over a
marker pair, and hanging each wing's own pylons outboard-first from that wing's bought count alone,
every one carrying high explosive for the Ammo Selection layer to overwrite. `PaintFor` resolves the
record's three colours and decals under a caller-named pattern; `ArmouredParts` and `DamageFor` put
the bought armour on the damage zones by copy, leaving structure and unnamed zones alone. The decode
is [../org/hangar.md](../org/hangar.md), "Into the mission".

## src/Flight/HangarEconomy.cs
The hangar's decoded economy over a `CustomPlaneDef`: the airframe, gun and engine tables as data,
the per-line cost and weight of everything a build carries, the two totals, the purchase verdict
and the display-only star ratings. Pure, so a build's whole price resolves without a session. The
verdict covers capacity and engine presence only, because pricing a build never checks funds.
Provenance: [../org/hangar.md](../org/hangar.md), "The economy".

## src/Flight/HangarPaintTables.cs
The paint screen's two decoded tables and its decal names, carried as CSVM's own JSON data: the
swatch table of base colours with their shade ramps and reset variants, and the pattern table of
airframe availability masks with the colour and shade defaults a pattern selection copies over.
`Resolve` is the original's own colour resolver, `Available` the availability mask, and `Nearest`
maps a first-schema store file's free triple onto an authored swatch. Engine-free and pure, so a
paint pick stays an index pair rather than free RGB. Decode:
[../formats/paint.md](../formats/paint.md), "The swatch table and the pattern defaults".

## src/Flight/PlayerRig.cs
One rendered view's state bag: the player index, the camera and its optional `SubViewport`, the
HUD parent, the visual layer, that player's `FlightController`, and the camera-anchored horizon,
deck and whiteout copies, which re-anchor every frame and so need one set per pane. The ambient
cloud field is deliberately not one of them, being world-anchored geometry every pane shares
behind a cull mask. `CameraWeatherState` is a per-rig field rather than a shared one, since
splitscreen panes can sit in different states at the same instant; `Session/WeatherRig.Tick`
writes it each frame.

## src/Flight/ViewerSet.cs
The "what do the cameras see" registry, session-owned and bound once after the rigs are built, so
every draw rule needing it shares one registration, single player included. `Cameras` hands back
the raw bound list for a consumer that needs each viewer's own field of view and pane height and
already skips a freed instance; `Positions` and `Poses` are the two derived shapes, the latter
filling a caller-owned buffer for a consumer that republishes the set every frame. It carries
cameras, not the screen-size or view-depth arithmetic, which stays in `ScreenSize`. Its consumers
are the tracer floor, the puffer distance fade, the screen wash and the world-light budget.

## src/Flight/CollisionLayers.cs
The named physics collision layers, world and aircraft, plus the combined mask. The first and only
place a layer bit is given a meaning; a new layer goes here rather than inline at a collider.

## src/Flight/CollisionDamage.cs
The original's collision damage arithmetic as pure statics: the impact severity cosine, the two
terms of the damage pair a contact costs, the camera kick, the entity-versus-entity cut, and the
grace and spawn windows. Being pure, the suites pin the whole table without a world. The severity
carries no airspeed term and the camera kick does; the two laws sit in the same original function
and the prohibition against merging them is on the members. `AircraftContactResolver` is the
caller that turns these numbers into one contact's outcome. Decode:
[../org/flightModel.md](../org/flightModel.md), "Collision damage".

## src/Flight/AircraftBody.cs
The flying aircraft's physics body: one `AnimatableBody3D` under `FlightController` carrying one
collision shape per `PlaneCollider` part, reusing the same convex hulls and local transforms the
terrain sweep casts. It rides the controller's transform, maps a query's struck shape back to a
part name, caches the one-entry exclusion list the owner's own queries pass, and drops to no layer
while the plane is out of play. It is also the fuse and blast geometry oracle, answering nearest
hull, a swept segment's closest approach and a bound radius from the same hull set with no physics
query, and scaling a projectile hit's damage by the blast falloff share.

## src/Flight/IWorldQuery.cs
The one seam onto the live physics world: a shape swept along a motion for the earliest stop
across a named part list, a single ray, and the standing overlap test the un-embed loop asks.
`FlightController` reads the world only through this and nothing else may reach
`DirectSpaceState`. The reports carry the struck collider as a plain node so a caller builds its
own name. A carried turret reads the same seam for its line of sight, and a synthetic
implementation proves the mask and the blocked and clear cases off-engine with no live node.
`GodotWorldQuery` is the only real adapter.

## src/Flight/ContactReport.cs
One detected contact as the value both halves of detection fill: the impact point, the struck
surface normal, which airframe box reached it first, the collider's name, how far along the
frame's motion the airframe stopped, and whether an aeroplane was struck. The airframe sweep fills
it from the world query, and the anti-tunnelling centre ray fills the same shape where there is no
struck box and no surface normal. It holds no node on purpose: the only question the decision side
asks about the struck object is whether it is an aeroplane. Read `ContactOutcome` next.

## src/Flight/ContactOutcome.cs
What one contact costs the striking aircraft, as a value with no node and no physics space behind
it: the fate, the decoded damage pair both parties spend, the doom rule's answer, the zone the
ledger actually charged, the pilot HUD's flash line, how far along the normal the caller must move
the striker to un-embed it, the camera kick, and the instruction to damage the struck aircraft
that only the caller can perform because only it holds that rig. One value with no optional parts,
so forgetting half a contact is forgetting one statement. `AircraftContactResolver` fills it.

## src/Flight/AircraftContactResolver.cs
The decoded contact rules for one aircraft, holding a world query and no node: the damage pair
both parties spend, the fate (the doom rule, no damage data, health exhausted, an airframe that
cannot un-embed), and the un-embed loop over the seam's overlap test. One call answers one contact
with one `ContactOutcome` the caller performs. The engine effects it interleaves with, because
each result is the next rule's premise, go through `IContactEffects`, which `FlightController`
implements per contact. Every contact it is handed spends the pair; the alternate-step cadence is
the sweep's, never a gate on the spend. `AircraftContactResolverTests` pins the rule table
off-engine against a synthetic world query and a scriptable effects sink.

## src/Flight/SweepCadence.cs
The original's alternate-step collision sweep as a pure value with no node: `Advance` answers
whether this sim step sweeps and, after a skipped step, the origin the sweep runs from, so the
carried motion is swept whole; `Respawn` resets the phase. The parity gates the sweep, never the
spend, so a contact the sweep resolves always spends the damage pair. `SweepCadenceTests` drives
it with the real resolver and ledger against a kinematic wall. Decode:
[../org/flightModel.md](../org/flightModel.md), "Collision response".

## src/Flight/AircraftLifecycle.cs
The states one aircraft moves between and the rules that move it: in play, crashed, destroyed with
its wreck still flying, inert, and back to spawned. It owns those flags, the collision-grace,
carrier-drop and auto-respawn timers, and the crash-def table with the selection off it, and holds
no node, so the whole table runs in a unit test. Every transition reports what happened instead of
performing it, answering one outcome value carrying everything the caller owes.
`FlightController` forwards the flags and keeps the events the session subscribes to. That a
crashed aircraft cannot crash again is a transition rule here, with the falling wreck's own
landing as the single exception.

## src/Flight/GodotWorldQuery.cs
The only adapter over Godot's `DirectSpaceState`, implementing `IWorldQuery`. It resolves the
wrapped node's world at each call rather than caching it, since the node may be bound before it
joins the tree, and `Sweep` holds the airframe's whole per-part cast and rest-info dance,
including the small nudge past the first overlap that a rest query coming back empty exactly at
the unsafe fraction requires.
