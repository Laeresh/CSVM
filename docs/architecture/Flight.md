# Flight

The plane as a flying, shooting, damageable thing, plus its HUD and stunt mode. Reads plane stats from the extracted zrdr; owns the arcade physics and everything drawn over the pilot's view.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Flight/WeaponDefs.cs
Typed reader over the shared `weapons.zrd.json` `BALLISTICS` block — 48 `WeaponDef`s (guns /
rockets / ordnance) keyed by `wep_*`, plus the `NO_AMMO_WARNING` empty-clip sound. Ballistics,
damage, allotment, the class flags, the specials, and the `FIRE`/`FLYOUT`/`IMPACT` bindings
(`IMPACT` keyed by `SurfaceRegistry` id); `DESC` resolved through `Messages`. Modelled on PlaneStats.
Schema: docs/formats/weapons.md. Verify/inspect with `--dump-weapons`.

`RANGE`, `DETONATION_DISTANCE` and `IMPACT_PROXIMITY` are exposed twice: the authored metres, and
`RangeSqM` / `DetonationDistanceSqM` / `ImpactProximitySqM`, squared once at parse as the original
squares them (`FUN_005ad630`). Compare a `Sq` field against a squared distance and never square-root
one to reach the authored field. The authored form is still the right one where a real length is
wanted — the blast sphere-query radius, the reticle's `RANGE` path cap, a threshold on the authored
number — and every comparison in the tree today measures through a geometry helper that already
returns a plain distance, so none of them changed. `TANGLER`'s `RADIUS` has no square by design:
the original stores it raw and compares it against a squared distance
(docs/org/ordnanceTypes.md).

`ImpactHook` is the per-weapon impact hook the detonation calls first (weapon `+0x20c`), an enum
rather than a delegate because the binary installs exactly one, in the `TANGLER` parse; the parse
here sets `ImpactHook.Tangler` where `FUN_004ba6f0` calls `FUN_005aec90`, and
`ProjectilePool.RunImpactHook` is the dispatch. `TanglerData` carries the engine's defaults for a
block omitting `TIME` (5.0) or `RADIUS` (10.0).

## src/Flight/Loadout.cs
Two layers over `CSVM/data/stock_loadouts.json`. `StockLoadouts.Load` parses the file (default
`res://data/`) into per-plane `LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves
each gun slot's markers to live muzzle `Node3D`s and its caliber+ammo to a `WeaponDef` (via
`GunWeaponId` = `wep_{N+k}`), and each hardpoint to its `pylon`, yielding `GunGroup`s (independent
ammo counters from `CLUSTER_SIZE`) + `Hardpoint`s. Turret slots bind but `IsTurret` (inert, M4).
Schema: docs/formats/loadouts.md. Verify/inspect with `--dump-loadout` (add `--weapon-lab` to bind
the full-rig loadout below instead of the stock one). `StockLoadouts.Load`'s missing-file warning
logs through `Log` (`Utils`), not `GD.PushWarning` (`engine-free-suites` A2).

A third layer sits above those two: `src/Flight/LoadoutChoice.cs`. `LoadoutOptions` holds the Ammo
Selection screen's two dropdown rosters, parsed from the same file's `selectable` block (the eleven
offered ordnance types plus `none`, and the four ammo types plus `none`, both in the original's own
order). `LoadoutChoice` records one pilot's edits keyed by **slot identity** — gun slots 1–4,
pylons 1–8, the formats' own ceilings — and `ApplyTo(LoadoutDef)` lays them over a base handed in
rather than looked up, dropping a pick for a slot the base lacks. That is what lets a custom plane's
saved fit (`BL-354`) use the same path. `none` on a gun omits the group; on a pylon it keeps the
array entry as a sentinel that `Bind` skips, because entry *i* binds to `PylonFillOrder[i]` and
dropping it would move every later pylon to the other wing.

`Loadout.ForRig(plane, WeaponDefs, LoadoutDef?)` synthesizes a lab loadout covering the
airframe's **whole** rig rather than only what stock names: the 4 gun-group slots the reverse-index
rule seats (`docs/formats/markers.md` "Slot → firepoint binding" — W1→fp(9−2n),(10−2n)), each
populated with whichever of its firepoint pair the rig actually has (the Kestrel's W1 resolves to
the lone centreline `firepoint7`), plus one hardpoint per `pylonN` present — then runs the
synthesized `LoadoutDef` through the same `Bind`, so there is still exactly one bind path. A slot
stock does name keeps its weapon/mount/caliber; one it doesn't defaults to the stock's first gun
weapon (`wep_30` if the plane has no stock guns at all) under a generic mount label ("Gun Group N").

## src/Flight/WeaponBench.cs
The world-less "do all 48 weapons mount and fire without throwing" pass check behind
`--weapon-test` and the `weapons-fire` in-engine suite: one static
`Run(plane, Loadout, WeaponDefs, ProjectilePool)` over a **parked** plane that spawns straight into
the caller's pool and returns the report plus the counts a suite asserts on (`Total`/`Ok`/`Errors`/
`Skipped` + `GunMounts`/`PylonMounts`). Needs no world, no colliders and no frame — `Spawn` does the
muzzle math and the pool insert synchronously. Hand it `Loadout.ForRig`'s loadout (both callers do)
and every weapon fires from **every** mount of its class: measured on the Bloodhawk, 4 gun groups ×
2 muzzles for each of the 31 guns and 8 pylons for each of the 17 hardpoint weapons — 48/48, 0
errors, 0 skipped.

## src/Flight/FireControl.cs
The fire-control state machine (BL-295), a plain engine-free class: trigger edges (first shot on
the press tick), per-group `FIRE_RATE` accumulators + muzzle rotation, ammo draw-down, both weapon
selectors with their on-empty auto-advance, the rocket pull/cooldown gate and the two once-only dry
cues. `Step(dt, FireInputs)` takes raw HELD booleans — every edge is detected inside — and returns
decisions in one reused `FireOutcome` (spawn commands as (group, muzzle)/pylon indices, the
declarative gun-loop state, the cues); `FlightController.ApplyFireOutcome` performs them against
muzzle transforms, `ProjectilePool` and `FlightAudio`. Ammo mutates through the node-free
`IGunSlot`/`IPylonSlot` views (`GunGroup`/`Hardpoint` implement them), so `Loadout` stays the single
store the gauges read and a decision can never diverge from the counters mid-tick. `Refill` re-arms
everything but deliberately keeps the gun pick; the pylon cursor doubles as the firing cursor, so it
resets to pylon 0.

## src/Flight/AimAssist.cs
The gun aim assist (`BL-342`, decoded in `docs/org/aim-assist.md`). `GunAimSlot` is one gun
barrel's plane-local state — `Smoothed` (what the round fires along), `Target` (what `Smoothed`
chases), `LastUpdate` (game-time seconds) — mirroring the original's eight `0x24`-byte slots at
plane `+0x3a4`. `AimAssist.Tick` is the per-frame forget + catch-up pass
(`FUN_004b3e50`): past `forgetInterval` seconds since the slot was last touched the target unwinds
to local forward; otherwise `Smoothed` slerps toward `Target` at `catchupRate` per second, snapping
outright once a single frame covers the whole turn (`catchupRate·dt ≥ 1`). A plain, engine-free
static class — no `GameClock` read inside it, `now`/`dt` are always passed in — so it unit-tests
without a `FlightController`; proven in the in-engine `aim-assist` suite (`Suites.cs`), which B3/B4/
B5 add their own cases to. `FlightController` owns one `GunAimSlot[]` per firable gun group (indexed
exactly as `FireControl`'s own `(group, muzzle)` pairs — no re-derivation of the original's
`weaponGroup·2+barrelToggle` index), built alongside `_firableGuns`, and ticks every group's array
immediately BEFORE performing `_fire.Step`'s outcome — the original restamps a slot's `lastUpdate`
on every round that goes out (B5's job), so the forget pass must see the pre-shot state. Gated on
`FlightController.IsHumanPiloted` (default true) — the original ticks this only for the local
player, and an AI plane's dead-eye path has no slots at all. Every CSVM plane is human-piloted
today, so the gate is a no-op until AI aircraft are active
aircraft; `AssistedGunDirection` falls back to the unassisted muzzle axis for a non-human pilot,
the same fallback a barrel with no slot already takes.

`AimAssist.TryIntercept` (`FUN_00460e30`) is the constant-velocity intercept solver: given a
muzzle position, the round's speed, a target position, and the target's velocity RELATIVE to the
shooter (the caller subtracts before calling), it returns the fire direction and time of flight, or
false for no solution. Frame-agnostic — every input in one space (world or plane-local), the answer
comes out in that space; B5 supplies plane-local inputs. Internally solves for `u = 1/t` rather than
`t` directly: `t`'s own quadratic has `|relVel|² − speed²` as its leading coefficient, which sits
near zero whenever the target's closing speed is close to the round's (the common case), while `u`'s
leading coefficient is `|displacement|²`, essentially never near zero for a real separation — that
substitution, not the quadratic formula's own cancellation avoidance, is the "numerically stable"
part. Fails on near-zero separation, a negative discriminant, or both `u` roots non-positive (no
forward-time solution — the case that catches a target receding faster than the round in a straight
line, which can still leave the discriminant positive). Proven in the `aim-assist` suite's
dead-ahead/crossing/receding cases.

`AimAssist.Scan` (`FUN_004b6530`'s scan half) picks the target the original would pick, or none.
It takes an `AimScan` context (world muzzle, shooter velocity + team, the plane's forward axis, the
weapon's speed/`RANGE²`/cone cosine, `dist_factor`) and an `AimCandidateSet`, and returns the
highest-scoring survivor as an `AimScanResult` (which list it came from, the intercept direction,
time of flight, score, and the candidate's `Source` object). One scorer runs over all four of the
original's lists in its order — the engine ships four byte-identical scorers differing only in the
container accessor. Gates, in the engine's order: the shooter itself (matched by reference against
`AimScan.Self`), a candidate that is not live (the vtable `+0x14` predicate), same team **or either
side unaffiliated**, no intercept, `speed²·t² > RANGE²`, outside the acceptance cone
(`AimAssist.WeaponConeCos` = `cos(CANNON_SPREAD°)`, or the candidate's own `+0x50` half-angle in
radians via `ConeCosFor`). Survivors rank by `alignment − distance × dist_factor`. All world-space:
B5 rotates the winner world→local into the slot's target field. Proven in the `aim-assist` suite,
every gate with its able-to-fail baseline.
`AimCandidateSet` keeps the four lists **separately** and deliberately: `Vehicles` (the live
`FlightController`s, joined by every built `SurfaceVehicle` through
`SurfaceVehicleRuntime.CollectVehicles` — the decoded `VehicleList` holds "aircraft and AI
ground/sea vehicles" alike, docs/org/aim-assist.md "The four lists"), `Turrets` (fed since M4 C9a by `ProjectilePool.CollectTurrets` — every
registered aircraft's carried gunners, on their host's team), `Structures`
(`AddStructures(DestructibleRegistry)` — an **approximation** of the original's `targets.zrd`
`MStructList`, recorded as one; `MissionTargets` is not the analogue, it holds objective display
strings and nothing damageable) and `Ordnance` (`ProjectilePool.CollectFusedOrdnance` — a FILTER
over the rounds in flight, not a structure of its own). The ordnance filter is
`FUN_00441830`'s two independent reasons to wrap a round: a fuse over `AimAssist.MinFuseDistance`,
**or** `TARGETABLE`. Only a `TARGETABLE` round carries a `Source` (its `ProjectilePool.Flyout`),
which is the admission byte the wrapper sets at `+0x6c` and the only thing `TargetPool` admits.

`AimAssist.FireDirection` is the whole fire call in one place (`FUN_004b6530`'s step order):
seed the slot's target with the plane's forward axis, run `Scan`, rotate the winner world→local into
`GunAimSlot.Target`, restamp `LastUpdate`, rotate the slot's `Smoothed` local→world as the direction
actually fired, and scatter it. `AimAssist.Scatter` (`FUN_004608a0`) is that scatter: a uniform roll
about the aim axis, then a polar angle **uniform in `[0, inaccuracy]`**.

## src/Flight/TargetRef.cs
The one abstraction over everything the player can select: an enemy Fury, a zeppelin engine and a
turret emplacement are three unrelated C# types, and every consumer downstream (the classed pool,
the cycles, the label formatter, the marker) reads this and never the underlying type. It WRAPS an
`AimCandidate` rather than restating it, adding what the aim assist has no use for: `Kind`, `Class` +
`Objective`, `Name`/`DisplayName`/`TypeLabel`/`Category`, and optional `Health`/`Armor`. `Classify`
is the decoded class model (`FUN_004b5cd0`); `CategoryLine` composes the marker's line 1;
`SortsFirst` is `FUN_004bbd60`'s pair of `key = −1` overrides, an objective **or** a hostile round in
flight. Pure data, no Godot node. Decode: [org/targeting.md](org/targeting.md). Pinned by the
`target-ref` suite.

## src/Flight/TargetPool.cs
The player's classed candidate pool: three lists of `TargetRef` (`Enemy`, `Ally`, `NonAircraft`,
reachable through `Of(TargetClass)`), rebuilt from scratch on every `Rebuild` call, which is the
original's own contract and why a runtime spawn appears and a death disappears with no extra
plumbing. It walks three of the aim assist's four lists (`Vehicles`, `Turrets`, `Ordnance`);
selectable structures arrive through `Rebuild`'s separate `subParts` argument, filled only by
`ZeppelinRuntime.CollectTargetParts`, and the campaign's objective sites through its `objectives`
argument, filled by `ObjectiveSites.Collect`. The two mission flags stay separate: `objectiveTarget`
comes in with the candidate and puts a site on the Enemy cycle, `otherTarget` is stood in for by
what the candidate is and puts a sub-part on the Non-Aircraft one. An `Ordnance` entry is admitted only when its source is a
`ProjectilePool.Flyout` with the `TARGETABLE` admission byte set and still live, so a round wrapped
only because it is fused stays unselectable. `Describe` is the only place in the targeting path that reads
a concrete source type, beside `NameOf`/`OwnerOf`, the identity pair the AI ranker's
`rating_biases` match reuses so it grows no second switch of its own. `TargetSelection` owns the instance; `HumanFlightAdapter` wires one per human
pane and `FlightController.StepTargeting` feeds it every frame. Decode:
[org/targeting.md](org/targeting.md) "The candidate list". Pinned by the `target-pool` suite, with
the carried-gunner exclusion on `turret-gunner`.

## src/Flight/TargetSelection.cs
One pilot's target selection: the sticky choice, the eleven actions and the lifecycle. One instance
per pane; it OWNS its `TargetPool`. The split between the action handlers (which only mutate the
class and the selection identity, stepping the list that already exists) and `Resolve` (the per-frame
pass that re-sorts and re-finds the selection by entity, falling back to the list head) is the
original's, and that one fallback is the entire lifecycle: auto-acquire, switch-on-death and
drop-on-class-change are all the same failed re-find. `SectorKey` is the cycle comparator
(`TargetRef.SortsFirst` ahead of every sector, then ahead, behind, left, right, nearest-first inside
each); `Select`/`ApplyInitial` are `--target=`'s
seam, the only things here with no counterpart in the original. No Godot node dependency. Decode:
[org/targeting.md](org/targeting.md). Pinned by the `target-selection` and `target-flag` suites.

## src/Flight/TurretDefs.cs
Typed reader over the shared `ai.zrd`'s `TURRET` section — 42 `TurretDef`s (docs/formats/turrets.md):
the carried/standalone split (`CREATE_STANDALONE` present-and-zero = carried, looked up by `TITLE`
from a host; `NODES` patterns = world emplacements), the `PARTS` kinematic chain (3-element
`[yaw, pitch, firepoint(s)]` or 2-element with no traverse ring), the `WEAPON` sub-block
(`NAME` is a BALLISTICS id), arcs, the attack/bored duty-cycle windows and `SOUNDS.CANNON`.
Tolerates the eight engine-accepted never-authored keys. `FindByTitle` mirrors the engine's lookup:
a titleless entry matches unconditionally. `TeamId` carries the decoded loader default — an
absent TEAM is the first ENEMY team (id 2; the space is 0 neutral / 1 ally / 2+ enemy), and the
four authored TEAM 1 standalone entries are the piratezep's own allied rings.

## src/Flight/TurretController.cs
One `ai.zrd` turret gunner, both families: carried (`BuildCarried`, per host from
the vehicle def's `thirdp` `TurretMount`s × `TurretDefs` × the built plane model, ticked from
`FlightController.SimStep`) and world emplacement (`BuildEmplacements`, per matched `NODES`
pattern node via `AnimRuntime.FindNodes` with multi-segment paths scoped to the prior match,
ticked by `Session/TurretEmplacementRuntime`). Per tick: the nearest hostile entry of the whole
`VehicleList` inside `DETECTION_RANGE`, read through `ProjectilePool.CollectVehicleList`, so an
AI ground or sea vehicle is a candidate beside an aircraft the way the decoded picker holds both
(docs/org/targeting.md "What a turret's candidate set holds"; `turret-vessel-targets` suite). A
ship reaches it as the vessel it is, never registered as aircraft. Then the mission structures,
through `ProjectilePool.CollectMissionStructures`, walked after the vehicles against the same
running best with a tie going to them and a gasbag dropped, which is the decoded pass order
(`turret-structure-targets` suite). The picker's other two pools (turrets, tracked ordnance) are
decoded but unscanned. Team gate through `AimAssist.Hostile`; carried = host's
`FlightController.Team`, emplacement = the authored/default `TurretDef.TeamId` with no conversion,
since one integer space covers aircraft and emplacements alike, until `SetTeam` fans a zeppelin
record's own team over the guns standing on that hull),
`AimAssist.TryIntercept` lead (no solution ⇒ track, hold fire),
wrap-aware directed yaw clamp + pitch clamp, bounded slew (3.0/s), pose written onto the PARTS
nodes, then the fire gates: `Activated`, attack window, 15° barrel-on-solution cone, cached
1–2 s world-only line of sight, `FIRE_RATE` redraw. `Alive` reads the `HEALTHY_NODE` visible IN
THE TREE, not its own flag: a mission `.gw` switches a site off at its root (C3/M03's
`b_turret1..6`), and a gunner under it is dead to its tick and to every gunner's scan
(`mission-off-turrets` suite). `PlatformOf`/`PlatformColliderRids` are what
keep an emplacement's own mounting section out of its line-of-sight ray, and the same set rides
each of its rounds as the pool's `ownerBodies`, so the flak neither strikes nor splashes the gun
that fired it while a neighbouring gun's burst still lands (`turret-self-fire`).
A carried gunner's line of sight runs through `WorldBlocksLine`, a static method mirroring
`FlightController.WorldBlocksLine`'s exact call shape against the `IWorldQuery` `BuildCarried`
hands the constructor (a `GodotWorldQuery` over the host); `_host` itself stays for what it alone
gives (`WorldVelocity`, `InPlay`, `PlayerIndex`). An emplacement has no host and no `IWorldQuery`
either, so it keeps its own `WorldRayBlocked` twin, deliberately left alone: a gunner mounted on
world geometry needs its own section excluded from the ray, which a carried gunner never does.
Format and decode: [formats/turrets.md](formats/turrets.md). Proven by the `carried-turrets`,
`world-turrets` and `turret-vessel-targets` suites, `TurretDefsTests` and
`TurretLineOfSightTests`.

## src/Flight/WeaponCursor.cs
`FireControl`'s internal ammo-slot index math (an `internal` class — nothing else may call it):
`NextArmed` is the firing cursor — the selected slot while it has rounds, else the next armed slot
forward-wrapping (`-1` when all empty); `NextSelectable` is where the manual G/H step lands — the
next armed slot strictly after the cursor, skipping empties. Each slot is its own position
regardless of ordnance/weapon type, so H cycles even a uniform loadout. That per-hardpoint reading
is confirmed against the original (user at the controls, 2026-08-14): the player picks a hardpoint,
the game does not merely drain them in pylon order. Stateless; proven through
`FireControl`'s interface (`FireControlTests`), not its own.
The order it walks matches the original's (same observation), so the sequence is settled and not a
knob.

## src/Flight/Ballistics.cs
The VELOCITY/ACCELERATION/GRAVITY integration every round steps with: a static, Godot-`Node`-free
class with `Step` (one round's per-frame advance, mutating pos/vel in place — `ProjectilePool.SimStep`
owns the surrounding raycast/fuse-test loop and its own `dt`) and `March` (the reticle's whole capped
walk — range cap and 4096-iteration bound included — called once per frame by
`FlightController.BallisticImpactPoint` with its own fixed `dt`, for the distance the decoded pipper
rule asks for). Extracted so the two callers cannot
silently diverge; each still owns its own step size. The weapon census behind why a fixed `dt` is
safe for `March`: [formats/weapons.md](formats/weapons.md).

**The motor and its cap** (`FUN_005afd50`, org/ordnanceTypes.md): `ACCELERATION × dt` raises the
round's own speed only while it is **below** `speedCap`, and clamps there. `LaunchSpeed` is the pair
the pool and the march both seed from, and it is where the counter-intuitive half lives: a weapon
with no motor is seeded AT its cap (nothing accelerates it), while a motor round leaves at its
**launcher's** speed and climbs to `VELOCITY` above that. ⚠ **Nothing here may ever reduce a speed.**
The original has no drag term — a round already faster than its cap is left alone rather than clamped
down — and that absence is why its rounds carry so far; `NoStepEverSlowsARound` pins it. `grav` is
the weapon's `GRAVITY` in m/s² directly, not a scale on world gravity, and every shipped entry
authors 0.0. Both vectors are the round's OWN velocity: a launcher's share is the caller's to carry,
so `March` advances position by `vel + inheritVel` while accelerating `vel` alone.

## src/Flight/DisablingIntensity.cs
The shared `SONIC`/`FLASH` intensity (`FUN_0042e840`, decoded in
[org/ordnanceTypes.md](org/ordnanceTypes.md)): a static, Godot-`Node`-free `TryResolve` returning the
wash weight and the stun duration (five times it), plus `FacingDot` for the `FLASH` direction
convention. The curve is a plateau, full strength to a squared ratio of 0.6 (77% of the radius) and
fading over the last quarter, so **both distance inputs are squares** — the engine's distance routine
returns a square and this path never takes a root. `FLASH` adds the facing test (nothing behind the
victim, scaled by twice the dot below 0.5); `SONIC` does not, and that is the only behavioural
difference between the flags. The consumers are the player's screen wash, the AI stun and the smoke
screen; the module itself knows about none of them.

## src/Flight/Difficulty.cs
The difficulty setting, as the engine's own 0/1/2, and the single thing it does: multiply an enemy
vehicle's armour and health maxima at spawn by 0.75 / 1.0 / 1.25 (`FUN_0047c210`, decoded in
[org/vehicleDamage.md](org/vehicleDamage.md)). `Parse` takes both shipped vocabularies, the campaign
selector's Normal/Hard/Hardest and Instant Action's novice/veteran/ace, which name the same three
tiers; `FactorForSpawn` owns the team gate, which is inequality with the player's team and not
hostility, so a neutral or team-less spawn is scaled too. `PlaneStats.WithEnemyDurability` applies
the factor, and the per-spawn jitter runs after it, banding around the scaled hull. The setting
reaches nothing else: in the executable it is readable only through `FUN_00440710`, whose four
callers are that spawn, the options screen, the settings save pass, and Instant Action's
save/set/restore around the same spawn. No AI skill, accuracy or aggression is keyed to it.

## src/Flight/TanglerChoke.cs
The choker's engine-dead duration (`FUN_004b9bc0`'s `TANGLER` branch, decoded in
[org/ordnanceTypes.md](org/ordnanceTypes.md)): a static, Godot-`Node`-free `Duration` of
`ENGINE_DEAD_max × (1 − d²/RADIUS)` floored at `ENGINE_DEAD_min`, plus `EngineDeadBounds`, which
resolves the bounds the way the original does — they are a pair of GLOBALS every `TANGLER` parse
overwrites, so the last entry carrying one wins for every choker in the install (this one authors
exactly one, `wep_12` at `[5, 13]`). **The numerator is squared and the radius is raw**, an authentic
unit mismatch that puts the full-strength zone of a 35 m weapon at about 4.6 m; the floor covers
everything past it, so the curve alone never returns less than the minimum, and whether an aircraft
is caught at all is the cloud's squared-radius test in `ProjectilePool.StepTanglerClouds`. The
seconds go to `FlightController.TryChokeEngine`; this module knows nothing about aircraft.

## src/Flight/SmokeScreens.cs
The `SMOKE_SCREEN` mechanism (`FUN_004b8fd0`, decoded in
[org/ordnanceTypes.md](org/ordnanceTypes.md) "SMOKE_SCREEN is a stun trap"), three types in one
file. `SmokeScreenRule` is static and Godot-`Node`-free: `Catches` (strictly inside the raw range
AND the unit layer-to-victim line's dot with the layer's BACKWARD axis strictly above the stored
half-angle cosine) and `StepWash` (the human wash's cadence over one re-arm timer: 0.97 on a first
hit, 0.9 every 1.5 s while inside, 2 s duration, the grey-green `(0.2, 0.29, 0.145)`).
`SmokeScreenTunables` reads `smokescreen_stun_range` / `_angle` / `_interval` off `player.json`
with the loader's own image defaults (200 m, cos 0.8, 3 s) for an absent key; the angle is stored
as `cos(angle/2)`, so the shipped 170° reaches 85° off axis. `SmokeScreens` is the world registry:
`Lay(layer, timeSeconds)` is the fire path's entry (a `SMOKE_SCREEN` weapon spawns no round; it lays
a screen for its `TIME`), `SimStep(dt)` runs the timer down, ends a screen whose layer is out of
play on the spot, and walks the roster delegate for every running screen, hitting every in-play
aircraft other than the layer inside the cone about the layer's LIVE pose: a human gets the wash
through the `SmokeWashSink` (`ScreenFlash.PlayBlend` in the session) addressed to its own
`PlayerIndex`, an AI gets `FlightController.TryStunPilot(interval)` refreshed every step it stays
inside, so it goes limp for the whole screen and the interval beyond it. The wash re-arm timer is
kept per victim per screen (the original's one slot is single-player), Decision 2. It is NOT an
occluder: no collision, no visibility and no targeting role — the smoke is drawn, and stops nothing.
Each screen carries its own emitter over the `ISmokeEmitter` seam: `Lay` asks the settable
`Emitters` factory for one and homes it with a zero step at the launch pose, `SimStep` drives it at
the layer's live pose every step, and the same teardown both end conditions reach stops it.
`SmokeScreenEmitters` is the engine side of that seam, reading `generate_smokescreen`'s
DISTANCE_INTERVAL `PUFFER_STATE`s out of the world `AnimProgram` (the session wires it once the
chapter's textures exist) and pooling one `Puffer` per authored state, reused only once its previous
screen's puffs have decayed. The compiled archive is where the definition is taken from; the reader
normalizer now carries `DISTANCE_INTERVAL` too, so the reader form of the same definition is a trail
rather than the no-trail sustain it used to read as. The cloud's look is the authored numbers through `Puffer` unchanged (`smokerpuff`:
four puffs per 0.65 m, `SIZE_RANGE` 0.15–0.25 growing 85× over a 2.5–4 s life, `LOCAL_VELOCITY`
10 m/s astern plus ±17 m/s of random, `NEAR_FADE 30,10` so a camera nearer than 30 m of depth
sees none of it, the `53,74,37` ramp at alpha 0.8; `smokerpuff2` is the thin 1–1.3 s ribbon at the
tail); the two `INACTIVE` events at `Animation 2.0` are not applied, since the emitter runs for the
screen's `TIME` and the reference footage shows the cloud still being laid well past 2 s. Pinned
by `SmokeScreenTests` (rules, tunables) and the `smoke-screen` suite (a live five-aircraft roster:
the AI astern stunned throughout and recovering after expiry, the AI beyond 85° untouched, the
human astern washed on its own pane while the layer's and a third human's stay clear, a downed
layer's screen ending at once, the emitter started homed and stopped with the screen, the two
authored trail states read off C1's compiled `cam_anim`, and `smokerpuff` driven at 100 m/s for
four seconds through the particle runtime holding thousands of puffs live past its starting pool,
tens of metres across near the end of life and blown down the host's own backward axis) and by
the `ordnance-launch-axis` suite for the
launch side (a `wep_13` pylon lays one screen, spends its ammo and puts no round in the pool). Not a
`Node`; the session owns and steps it.

## src/Flight/BeeperTags.cs
The `BEEPER` / `BEEPER_SEEKER` pair (`FUN_004b88a0`, `FUN_004b8ad0`, `FUN_004b8ce0`, `FUN_004b8b50`,
decoded in [org/ordnanceTypes.md](org/ordnanceTypes.md) "The beeper and the seeker, which are one
weapon in two halves"), three types in one file. `IBeeperSubject` is the three facts a tag reads off
its aircraft (`WorldPosition`, `InPlay`, `Team`); `FlightController` implements it through a partial
declaration in this file, so the registry and its tests run without an engine. `BeeperTagRule` is
static and Godot-`Node`-free: `Step` (the countdown: an aircraft out of play slams a still-painting
tag to −1 before the frame's decrement, the frame that reaches or crosses zero ends the paint, and
the original never slams again after that), `AlignmentDot` (the unit vector FROM the tag TOWARD the
round, dotted with the round's heading, so a tag dead ahead scores −1 and LOWER is better aligned)
and `Prefers`, the running-best comparison with the four literals: a better-aligned candidate
replaces the best under a SQUARED-distance ratio of 1.2 (about 9.5% farther as a length); a
worse-or-equally-aligned one must be strictly nearer (ratio under 1.0) AND either sit at or under a
dot of 0.7 (anything less than about 134° off the round's nose) or give up under 0.1 of alignment.
Net effect: the nearest painted aircraft, unless it is well behind the round, with a
better-aligned one stealing only within the 20% squared window; and because it is a running best
in tag order, a near-worse and a far-better pair inside that window resolves to whichever was
tagged LATER. `BeeperTags<TAircraft>` is the world registry: `TryTag(shooterTeam, victim, seconds)`
is the hit path's entry and holds every creation gate the original has (`AimAssist.Hostile` on the
teams, the victim in play, no live tag on it already, and one tag per sim step, which is the
original's clock stamp compared on the next creation); a second beeper hit on a live-tagged aircraft
makes no tag and does NOT refresh the first, while an aircraft in its tail takes a fresh one.
`SimStep(dt)` runs every tag through `Step` and deletes it once its countdown sits at or below −5,
five seconds of tail after expiry (four after a slam) so nothing holding a reference sees it vanish.
`PickTarget(roundPos, roundHeading)` is the seeker's per-frame query over tags with a countdown
strictly above zero and returns the aircraft or null; an untagged aircraft is never returned, and
in-play is not tested there because a dead aircraft's tag collapses on the next step. Pinned by
`BeeperTagsTests` (every threshold from both sides, the slam, the tail, the gates, the order
dependence, and `wep_10`'s `TIME 20` read off the extracted file). Not a `Node`; the session owns
and steps it after every aircraft, in both step paths, and hands it to `ProjectilePool.BeeperTags`
for the hit-side tagging and the seeker's retarget, which are the projectile integrator's.

## src/Flight/CamParams.cs
One aircraft's camera tuning out of `camparam.json` ([formats/camparam.md](formats/camparam.md)):
the `default` block, then the plane's own block layered on top. Seven of the eleven airframes carry
one; the other four take the 13.0 default. Mirrors `PlaneStats.Load`'s shape (same
`Load(zrdrPath, planeNodeName)`, same nearest-wins resolution), and like it is loaded once per
distinct plane and cached by `GameSession`. `FromData` is false when the file was absent — the
built-in fallbacks are the shipped `default` block verbatim, so a partial extraction still flies and
the session log distinguishes "the data says 13" from "we guessed 13".

## src/Flight/CameraController.cs
The flown aircraft's camera, split out of `FlightController`: the roll-following chase camera, the
numpad fixed views (`Views`, `ActiveView`, `FixedView`, `LogView`), the look-behind view
(`BackView`, numpad 0 / `--view=back` / a pad click, at the chase radius bounded into the authored
`back_dist_min/max`), the E42 (`BL-372`) analog look-around (`PadLook`, the right stick — a
continuous twin of the fixed views at the SAME dynamic radius, rigid and instant so releasing the
stick reads as a snap back to the ordinary chase pose), the authored crash camera (`CrashView`, a
hard cut to a static elevated vantage `crash_horiz` behind / `crash_y` above the impact, held until
respawn — framing decoded off the original's crash footage; the shared world clearance and the
decoded death/flyby modes remain unimplemented under `BL-260`) and the free orbit used while the
weapon lab holds an airframe. ⚠ That orbit no longer answers to a HALT (`BL-429`): a board's menu
cursor reads the same `WASD`/arrows/left stick `OrbitInput` does, so a halted world that also flew
the camera meant choosing a menu row swung the view. `FlightController` writes nothing to the
camera while a board is up, and the free look moved behind the board's Photo Mode row, which hands
the pane to a `SpectatorCamera` instead. `Held` is the only remaining orbit source here, and no
menu shares its keys. Steers a `Camera3D` it does not own, as `UI/OrbitCamera` does for the
static viewer. Beside the held views it carries the pilot's SELECTED view mode (`ViewMode`,
`FirstPerson`, `CycleCockpitViews`, `SelectChase`): Chase, Cockpit or Nose, seeded from
`--view=cockpit`/`=nose` and changed at the controls by F8 (Cockpit → Nose → Chase) or F6
(directly to Chase). The decisions themselves are `PilotView`'s, not this class's, so they are testable
without an engine; this class holds the state and the camera. ⚠ The modes are deliberately NOT rows
in `Views`: `BL-150` rebuilds that table later and must be able to replace it without touching them.
⚠ Outside first person a held numpad key overrides the mode for as
long as it is down and leaves the selection alone, the same precedence it has over `--view=`'s
pinned digit; INSIDE Cockpit or Nose it holds no view at all, because the numpad is the head-look
snap cluster there, which is what the original binds it to (`OriginalScreenshots/Keybinds Views
2.png`: `Kp1`–`Kp9` = Look Up/Left/Rear … Look Forward … Look Up/Right). `PilotView.HoldsFixedViews`
is that rule and `ActiveView` returns −1 under it, so the per-frame chain and `Snap` obey it
together. Numpad 0's look-behind is outside the cluster and still overrides every mode. Cockpit and
Nose sit at the plane's authored `cockpit_camera` marker (`FirstPersonPose`, a static, engine-free
law: `camera_world = plane_pos + plane_rotation × offset`, plus the fixed −4.70° head-pitch
tilt — `FirstPersonView` is its thin write onto the owned `Camera3D`), rigidly mounted with no
smoothing and no camera-side shake so the camera inherits the plane node's wobble for free
(`docs/org/shakes.md`). The offset comes in through the constructor
(`PlaneBuilder.CockpitCameraOffset`, fallback the origin). The aim is `Head`'s (a `HeadLook`)
current angles, composed azimuth-about-the-plane's-up then elevation-about-the-yawed-right-axis,
with the fixed tilt riding the elevation axis; `Snap` recenters the head, so a respawn never frames
itself over the pilot's shoulder. Each mode carries its own FOV
(`HorizontalToVerticalFovDeg`, a static, engine-free law: `vertical = 2·atan(tan(H/2) ·
(4/3)/liveAspect)`, 80°H Cockpit / 60°H Nose — `ApplyFirstPersonFov` is its thin write, reading
the owned camera's OWN viewport for the live aspect so a splitscreen pane derives its own answer).
Every other pose runs on the external FOV the camera carried at construction
(`RestoreExternalFov`, captured once so this class never reaches into `GameSession`'s 62° global);
`FlightController._Process` calls it by default and only the `FirstPerson` arm overrides it, so a
look-behind while SELECTED Cockpit/Nose gets the external FOV while held and the first-person FOV
back on release. `Snap` and `CrashView` carry the same default/override shape, so
a spawn/respawn/crash-cut never shows a stale FOV. `LogView` already names the modes
(`view n=cockpit`), which is what makes a scripted mode selection verifiable. The chase RADIUS is dynamic per plane (BL-248): `d = Dist + DistFactor·V` (both
authored) plus a first-order acceleration transient relaxing at the MEASURED 0.65 /sim-s
(`UpdateDynamics`, host-called once per sim step); the offset's DIRECTION (behind and above at
~15.7° elevation) is not in the data and stays hand-picked. The numpad `+`/`−` zoom axis
(`BL-433`) trims that shared radius further: `UpdateZoom` reads the two keys directly (no pad,
the same rule `ActiveView`/`BackActive` follow), moving a target at 2/s and easing the shown
value at 1.5/s, both clamped `[0, 1]`; `EffectiveRadius` is where the trim actually lands
(`_radius` minus `shown · Dist`, floored at zero), read by `Chase`, `FixedView`, `BackView` and
`PadLook` instead of `_radius` so the trim reaches every external pose alike. Called only from
the ordinary flight branch, never while the weapon lab's held orbit is running, since `Orbit`
reads the same two keys for its own dolly. The head-look centre key zeroing this value too is
`BL-435`, not yet wired. Collaborators: `FlightController` (the only host) and `CamParams`.

## src/Flight/HeadLook.cs
The pilot's head in the two first-person views, decoded from the original's shared look controller
(the original's shared look controller). It holds two pairs of angles: the
TARGETS the input sets, and the SHOWN angles that chase them exponentially,
`shown = target + (shown − target)·e^(−rate·dt)`, at 3.0/s for elevation and 5.0/s for azimuth.
Elevation is 0 at level and +π/2 straight up, clamped to `ElevationFloor`..π/2; azimuth is 0 dead
ahead, positive to the left, wrapped to ±π. `Step` picks this frame's target and then always
chases it, so snap, free-look, the center key and C22's autohead all reach the eye through one law.
The three input paths are the original's: a **snap** direction maps to a target through
`SnapTargets` (dead ahead looks straight UP, a 45° diagonal 45° up, anything else level, azimuth
being the direction's own angle mirrored so that pointing right looks right), releasing it returns
the targets to straight ahead; **free-look** integrates the targets at a fixed 2 rad/s along the
input direction, which is normalised first because the rate IS the law — the original's input is a
hat switch, so a light stick deflection pans exactly as fast as a hard one; the **center key**
zeroes both targets at once and beats a held snap. ⚠ `ElevationFloor` is a constructor parameter,
not a constant: the original's first-person caller passes 0 and its chase caller −π/2, and the
chase look-around is the same controller. `IdleAim` is the no-input hook — consulted
only on a frame with no look input at all, its answer becomes the targets directly, deliberately
past the floor, because autohead's own floor is below level. `AutoheadTarget` (static, engine-free)
is that seam's law: local-frame sideways/vertical velocity only (forward speed dropped — a port
decision, docs/formats/vehicle/player-globals.md's autohead row), scaled by `autohead_turn_time`,
capped in magnitude at `autohead_turn_max`, its components read DIRECTLY as (elevation, azimuth)
rather than through an arctangent. `FlightController.AutoheadTarget` is `IdleAim`'s live wiring —
gated on `ViewMode == Cockpit` and a `Config` toggle (`headLook.autohead`, default ON).

Engine-free apart from `Mathf`, so every law unit-tests without a camera. Collaborators:
`CameraController` (owns one as `Head` and composes its shown angles into the view basis) and
`FlightController` (reads the devices and steps it on the SIM clock — a wall-clock step would run
the smoothing 39% off, the same trap the chase distance transient records).

## src/Flight/CockpitVisibility.cs
The per-mode node hiding the original applies to the pilot's OWN aircraft while a first-person view
is on the screen (`docs/org/cameraViews.md`, "Mode 6 = Cockpit" / "Mode 7 = Nose"): Cockpit draws
`cockpit1` and hides the `healthy` body; Nose hides the interior, the body, and the `markers` and
`dontmove` groups; every external pose renders the aircraft exactly as it was built. `Rules` is the
whole decision as a pure function over `(PilotViewMode, firstPerson)`, so it unit-tests engine-free;
`Bind` finds the four groups in a built plane model and `Apply` writes one frame's answer onto
them. `FlightController._Process` calls it every frame beside the camera write, keyed to the pose
that frame actually took — the look-behind is an external pose and brings the body back while it is
held, the same shape `CameraController.RestoreExternalFov` has. A held numpad key reaches this only
from an external selection, since in first person it drives the head instead of the camera.

⚠ `Bind`'s group search is interior-blind: each gauge sub-assembly inside `cockpit1` carries its own
`markers` child, so a plain depth-first walk can bind a gauge's instead of the airframe's.

⚠ **Splitscreen is a shared scene tree.** Visibility is a property of the node, not of a viewport,
so a pane whose pilot sits in the cockpit hides that plane's body in EVERY pane. Each rig owns its
own plane model, so the rule is at least per-pilot rather than keyed to player 1; making it
per-pane needs render layers, which Decision 5 defers.

## src/Flight/CockpitOverlay.cs
The cockpit interior's own render pass, behind `--cockpit-pass` and off by default. It takes the
built `cockpit1` node out of the plane model and re-parents it into a `SubViewport` carrying its own
`World3D`, at zero translation with the mount basis `PlaneBuilder` gave it (the head-pitch tilt and
`InteriorScale`); the pass's camera sits at that world's origin, aimed by
`CameraController.FirstPersonPose` with the plane position and the `cockpit_camera` offset both
zero, since those two cancel between the eye and the panel. The projection is therefore the main
world's exactly, computed from small numbers instead of chapter-scale ones. The viewport is
transparent-backed on `HudLayers.CockpitPass`, so the world draws under the panel and the whiteout,
the screen wash and the HUD still draw over it. Lighting is a copy of the world's sun, re-aimed by
the inverse plane attitude each frame, plus a `Duplicate()` of the world Environment. In enhanced
graphics mode the clone also copies every shadow setting off the live sun (mode, splits, blend,
biases), clamping `DirectionalShadowMaxDistance` to the pass's own 100 m camera far plane rather
than the zone's kilometre-scale value; `GameSession.BuildCockpitPasses` then registers the clone
with `WeatherRig.RegisterExtraLighting` so a later zone change reaches it too, alongside the
session sun. Original mode is untouched: the clone's `ShadowEnabled` mirrors the live sun's, which
never turns on there. `GameSession.BuildCockpitPasses` builds one per `PlayerRig`, on that rig's own
`HudParent`; `FlightController._Process` calls `Sync` beside the camera write, and `Sync` follows
the interior's own `Visible` so `CockpitVisibility` keeps deciding which views show a cockpit.
See "Rendering: the enhanced graphics mode" above for the divergence record as a whole.

## src/Flight/ImpactOutcome.cs
"What should happen when this weapon hits this surface id" as a value — `EffectName` (the row's
`ANIMATION`, else its `SURFACE_ANIMATION`) with `SurfaceOriented` naming which slot it came from,
`Sound`, the `ImpactStandIn`, `Damage`/`BlastRadius` (+ `HasBlastDamage`) — plus the pure static
`Resolve` that computes it from a `WeaponDef`, a `SurfaceRegistry` id and the impact hook's
`ImpactSuppression` mask (`FUN_005ac7a0`'s: `Sound` nulls the sound, `Animation` drops the
`ANIMATION` slot and leaves a `SURFACE_ANIMATION` standing, `Effects` covers the crater carve and
the row's `EFFECT`, neither of which CSVM plays; the damage figures are under no bit). No Godot type, no scene, no sink, no sound archive, so the dispatch's one
decision is readable by a unit test; `ImpactOutcomeTests` is that test, including a suite over all 48
shipped weapons × the eight reachable ids asserting the *rule* (an effect or a stand-in but
never neither; a resolved sound names either a `SoundDefs` entry or a `SOUND_GROUPS` name;
`HasBlastDamage` matches the raw damage/radius fields) rather than a table of expected per-weapon
outcomes. `ProjectilePool.Impact` reads the struck id (`SurfaceIdOf`) and calls it, then `Apply`
performs the result; `ProjectilePool.HasBlastDamage` is the weapon-level spelling of the same
`HasBlastDamage` rule, so the rule exists once. The stand-in ladder (model ▸ explosion ▸ ricochet ▸
spark, no arm for ground) is decoded on `StandInFor`; the `buildings`(11) surface-id pitfall is in
[formats/weapons.md](formats/weapons.md).

## src/Flight/Projectile.cs
**The original's projectile-visual runtime is written up in [org/tracers.md](org/tracers.md)** — how
the engine draws a round at all (the `FLYOUT MODEL` is aimed ONCE at spawn and thereafter only
translated; the shared model node is multi-parented, not cloned, so one `slug.flt` serves every live
round), plus the authored tracer geometry the hand-tuned constants here stand in for: two crossed
0.2 × 4.5 m quads whose **tail** is the tracked point, a separate 0.29 m `*tip` quad 4.56 m ahead,
and a 600 m LOD past which the original draws nothing. Its closing table lists every place this file
deliberately differs — read it before retuning `TracerLength`/`TracerWidth`/`TracerBrightness`/
`TracerMinPixels`.

**A gun round leaves along the aim assist's line, not the muzzle axis.** The retail engine runs a
per-muzzle assist at spawn time (target scan → constant-velocity intercept → plane-local smoothing →
1° scatter), decoded in [org/aim-assist.md](org/aim-assist.md) and built in `AimAssist.cs`
(`BL-342`). It is a **launch-direction** assist: nothing steers a round in flight, so it belongs
at the fire call, not in this file's integrator.
`Spawn`'s optional `ownerBodies` is the original's owner node for a round nobody's aircraft fired
(a world emplacement's own mount): the hit ray excludes those bodies and the splash gather skips
them, the same way a pilot's own airframe is excluded through the shooter id
(`org/ordnanceTypes.md` "Half two, the splash"). Nothing else is exempt from splash: a neighbouring
gun's burst, or a rocket into the pit, still kills the gun.
`Spawn`'s optional `aimDir` is how it arrives — a world direction the CALLER computed
(`FlightController.AssistedGunDirection`); omitted, `Spawn` still uses the muzzle axis, which is
what every rig, the bench and the rockets pass. Only the round's velocity uses it — the muzzle flash still rides
`muzzle.Basis`, because the barrel has not moved.
`CollectFusedOrdnance`/`CollectAircraft`/`CollectTurrets` build three of the assist's four
candidate lists off this pool's own state: the live rounds the engine wraps (a FILTER, every def
with a fuse longer than `AimAssist.MinFuseDistance` **or** `TARGETABLE`, `FUN_00441830`'s two
independent reasons), the registered aircraft, and each
registered aircraft's carried turret gunners — the same roster the hit ray and the fuse
already use, so the assist cannot drift onto a second list. `CollectVehicleList` is the whole of
the first of those lists, aircraft plus the `SurfaceVehicles` runtime's hulls, for a scan that
wants the engine's `VehicleList` rather than its aircraft half; `GameSession` wires the runtime in
beside the world emplacements, and a build with no hulls leaves it null.
`CollectMissionStructures` is the third of those lists, the session's `DestructibleRegistry` wired
in the same way, so a gunner reaches the structure pool through the pool it already holds.
`PlayShotSound` is the turret gunners'
launch bark through the pool's own one-shot pool. `DefaultVelocity` (500 m/s, the
launch speed for a def with no `VELOCITY`) is shared with the scan so the lead is solved for the
speed the round actually leaves at.

**A round's velocity is two vectors, not one** (`org/ordnanceTypes.md`, "Launch velocity is
inherited"). `Proj.Vel` is the round's OWN velocity, the original's `heading × speed` and what
`ACCELERATION` raises; `Proj.Inherited` is the launcher's velocity the spawn copied into it. Every
reader asking how fast and which way a round is travelling goes through `WorldVelocity`, which adds
what `InheritedFraction` has left of the second: 1 at launch falling linearly to 0 at `LOCK_ON`
seconds. `CarriesLockOn` is the inherit-at-all flag (`FUN_005aef40` copies the launcher's vector
only for a weapon authoring `LOCK_ON` and zeroes it otherwise, so the choker and the two
non-`LOCK_ON` emplacement rounds inherit nothing) and is the decay's whole gate here.
⚠ **That is a deliberate, recorded divergence from the original.** There the decay lives inside
the steering step `FUN_005af960`, which `FUN_005af720` runs only for a `LOCK_ON` weapon whose round
**holds a target** (`SteeringStepRuns`), and its shot routine always satisfies that half by handing
a `LOCK_ON` weapon a synthetic ring-buffer target when the player has none selected (undecoded and
not reproduced). A CSVM round can hold no target at all, so gating the decay on it would fly a
torpedo fired with nothing selected at launcher speed plus 60 m/s for its whole range; the turn
stays gated on the target, only the decay does not. **Guns are held outside the rule on
purpose** (`InheritedAtLaunch`): no `CANNON` in this install authors `LOCK_ON`, so applying it to
them would strip every bullet of its launcher's velocity, and both the gun aim assist and the impact
reticle (`Ballistics.March`) are built on the inheriting round. `CollectLiveRounds` is the seam a
scripted run samples a round's speed through, and a breadcrumb reports the first four rounds that
finish shedding.

**The steering step turns only what the original turns, and turning costs speed**
(`org/ordnanceTypes.md`, "Guidance"). Per round and per sim step, in `FUN_005af720`'s own order:
`RetargetSeeker` (a `BEEPER_SEEKER` round asks `BeeperTags.PickTarget` with its position and unit
heading and REPLACES `Proj.Target` with the answer, null included, as `FUN_00441780` writes zeros
when nothing is painted; so the target a seeker was launched with is gone on its first frame and a
seeker steers only at a painted aircraft), then `Steer` on a round `SteeringStepRuns` admits whose
target has a position (`TargetPosition`; `Proj.Target` stays `object?` because the slot takes an
aircraft, an emplacement or a zeppelin sub-part alike), then `Ballistics.Step`, so the motor acts
on the penalised speed. Inside `Steer`: the desired direction is the bearing to the target, or
`LeadDesired`'s blend; `MaxTurnRad` is `TURN_RATE × dt × ramp` in radians times the engine's
per-shot turn scalar (`DAT_00a1e1b8`, initialised to 1.0 and reset to 1.0 by every spawn, so 1 on
every steering frame; the ramp is `TURN_SUSPEND_TIME`'s, unauthored, so 1 from the first frame and
kept as a term); an angle over the clamp slerps the heading by exactly `clamp / angle`
(`SlerpDirection`, renormalised, a dead-astern target left alone), otherwise it snaps; and whenever
the angle was above zero the own speed is multiplied by `TurnPenaltyFactor(turned)`,
`0.8 + 0.2·cos` of the angle actually swung THIS frame (the clamp when clamped, the whole angle when
it snapped). That is per steering frame, never per turn, and because it is the cosine of one
frame's swing the loss over a whole turn scales with the frame's authority: at 60 fps the seeker's
1.25 rad/s costs about 0.3% over a 90° swing (`ordnance-guidance` asserts the exact product), and a
coarser step costs more. The gate is on the flag: every dumbfire type authors `TURN_RATE 0.001` and
so, with a target, is steered by 0.001 rad/s, which the same suite measures on the torpedo (0.23°
in 4 s). `LeadDesired` is `LOCK_ON_LEAD` (`FUN_005af960`'s first block): from element 0 of age, on
a target with a velocity (`TargetVelocity`, the original's second target field, round `+0x74`),
the constant-velocity intercept **`AimAssist.TryIntercept`** (the same solver `AiGunner` and
`AiRocketeer` use; the original's `FUN_0053e56d` is the same solve on the round's OWN speed and the
target's velocity) replaces the bearing, slerped in from the bearing at element 0 to the full solve
at element 1 (`FUN_005ad630` at `0x005ade43` stores `1/(el1 − el0)` at weapon `+0x84` and clamps
el0 to at most el1); no solution leaves the bearing. ⚠ No shipped carrier reaches it: `wep_04`,
`wep_25` and `wep_27` are the three, all pair it with the 0.001 sentinel, and all expire at their
900 m `RANGE` before their 4 s / 5 s onset even off a standing launcher (`wep_04` at 3.46 s), so the
suite exercises the blend on a lab def cloned from `wep_11`.

**A `BEEPER` hit paints and deals nothing.** In `Apply`, a weapon with `BeeperTime` reaching an
`AircraftBody`, ray-struck or fused, calls `BeeperTags.TryTag(round team, victim rig, TIME)` and
returns before either damage path (`FUN_004b9bc0`'s `BEEPER` branch: the tag, then both damage
figures zeroed): the pair is discarded structurally rather than passed as zero, so an authored
pair would still spend nothing; `wep_10` authors 0.0/0.0 either way. The effect and sound above it
play as usual. `Impact`/`Apply` carry the round's own `Proj.Team` for that gate, stamped once at
spawn and never re-derived. `CollectHeldTargets` is the seam a scripted run reads a seeker's pick
through. All of B6 to B9 is flown by the `ordnance-guidance` suite on a live pool with a lab
`BeeperTags` beside it.

**The impact hook runs first, and the `SURFACE_ANIMATION` sits on the surface**
(`org/ordnanceTypes.md`, "Half one, the direct impact"). `Impact` calls `RunImpactHook` before it
resolves anything: the dispatch over `WeaponDef.ImpactHook`, an enum because the binary installs
exactly one hook (the `TANGLER` parse's `LAB_004ba660`), whose arm spawns the choker cloud below and
returns `ImpactSuppression.Sound`, so a choker's `IMPACT` row plays no sound, as the original's does
not. The mask goes into `ImpactOutcome.Resolve`, so `Apply` performs a row already stripped of what
the hook silenced and decides nothing new. `EffectOrient` picks the template basis the effect is
placed with: `SurfaceUpBasis(normal)` (the shortest rotation from world up onto the struck normal,
`FUN_0053fd40` from `DAT_006379c0 = (0,1,0)`) for the `SURFACE_ANIMATION` slot and identity for a
plain `ANIMATION`, which is BL-293's parked half: the fixed-axis upper ring is a plain `ANIMATION`
and stays fixed. It reaches the world-effects runtime through `EffectSink`'s new `Basis` argument
(`AnimRuntime.PlayEffectAt(orient)` → `TemplateStage.PlaceOn(orient)`, which sets the root's whole
basis when given and leaves it alone when null, so every other placement path is unchanged) and the
gamez-model spawn through `SpawnImpactModel(orient)`. On flat ground the rule changes nothing;
`impact-orientation` fires into a 30° slope and into flat ground and reads the basis back off the
sink. `SurfaceBasis` (Z along the normal) stays the sprite stand-ins' own frame.

**A `SONIC` or `FLASH` burst disables every aircraft in its radius and hurts none of them**
(`org/ordnanceTypes.md`, "The hit-side dispatch" and "SONIC and FLASH"). `ApplyDisabling` runs
from `Apply` on every burst of such a weapon, struck, fused or timed out, over the same
`GatherAircraftCandidates` + `BlastCovered` + 32-cap walk the damage splash takes, because the
original hands each splash entry to `FUN_004b9bc0` and its `SONIC`/`FLASH` branch runs the intensity
on the entry's squared surface distance. Per victim: `DisablingIntensity.TryResolve` on that square,
`WeaponDef.ImpactProximitySqM`, the `FLASH` flag and `FacingDot(NoseDirection, WorldPosition,
burst)`; then the victim's kind decides. A human's pane gets `WashSink(PlayerIndex, colour,
intensity, stunSeconds, DisablingWashStartDelay)`, red `(1,0,0)` for `SONIC` and white for `FLASH`,
duration five times the intensity, after the 1.0 s start delay the branch passes `FUN_0042e9d0`;
`GameSession` assigns `ScreenFlash.PlayBlend` to that sink, so it is per pane and blends on overlap.
An AI gets `FlightController.TryStunPilot(stunSeconds)`, which holds the guards; nothing on the
human path touches a control. `AircraftDamageDiscarded` is the four no-damage types (`SONIC`,
`FLASH`, `BEEPER`, `TANGLER`) and is read twice: `Apply` returns before `TakeProjectileHit` for a
struck aircraft, and `ApplyDamage` skips its aircraft gather for them, so a scaled zero never
reaches a plane's ledger, flashes "HIT" or wakes the AI (world bodies still take the authored, zero,
pair). `disabling-hits` flies all of it on two human rigs over a real two-pane `ScreenFlash` and two
AI rigs, including a burst fusing 29 m abeam that stuns for the fade's 3.8 s and a direct hit that
overwrites it to 5 s.

**The choker is a cloud** (`org/ordnanceTypes.md`, "The choker, settled"). `SpawnTanglerCloud`,
the hook's arm, leaves a `TanglerCloud` at the burst with the weapon's `RADIUS` both raw (the
duration's denominator) and squared (the catch), living the shared `TIME` (2 s on `wep_12`);
`StepTanglerClouds`, run by `SimStep` after the rounds, counts each down and, while it lives, hands
every in-play aircraft whose ORIGIN sits inside the squared radius `TanglerChoke.Duration(originSq,
radiusRaw, EngineDeadBounds)` through `FlightController.TryChokeEngine`, whose timer is extend-only,
so a victim inside is held at the formula's value until the cloud is gone and only then runs down.
Nothing excludes the shooter, as nothing does in `FUN_004b9590`. `EngineDeadBounds` is the pool's
copy of `TanglerChoke.EngineDeadBounds(weaponDefs)`, assigned by `GameSession`; the static image's
pair until then. The round's `DETONATION_DISTANCE` decides only where the cloud forms.
`CollectTanglerClouds` is the seam a suite reads the clouds through; `disabling-hits` reads a 12.7 s
choke on the struck AI, the 5 s floor on a human 20 m out, the refresh while the cloud lives and the
countdown once it is gone. The at-the-controls piece still owed is the choked aircraft's sound: the
original swaps the engine loop (`FUN_004b15c0`) and this side cuts thrust only.

`Proj.Cap` is the own speed `ACCELERATION` climbs to, seeded beside `Vel` at spawn from
`Ballistics.LaunchSpeed` (**after** the `weapons.rocketSpeedScale` dev factor, so scaling a rocket
scales its cap with it) off the raw `inheritVel`, which the original reads before the `LOCK_ON` gate.
The four motor weapons therefore leave at their launcher's speed rather than at `VELOCITY`: a flak
emplacement's `wep_27` starts at nothing and is still short of its authored 850 m/s when its 900 m
`RANGE` runs out. A motor round's first-frame speed is ~0, so `Spawn` poses the `FLYOUT` body down
the launch direction instead of a velocity too short to normalise. `Proj.Grav` is the weapon's
`GRAVITY` verbatim in m/s²; `WorldGravity` is now the sprite debris' fall rate alone. The
`motor-acceleration` suite flies all of this on a live pool.

**A round ends itself for one of exactly three reasons, and never on the step it collides**
(`org/ordnanceTypes.md`, "The motion step, and where a round dies"). `EndConditionMet` is the
original's own branch: `Proj.Travelled` reaching `Proj.Range` wins outright and neither fuse is
consulted after it, `TargetFuseTriggered` is the per-round fuse on the round's OWN target (squared
throughout, gated on `LOCK_ON` and a held target and a non-zero `DETONATION_DISTANCE`), and the
`DETONATION_TIME` age test is what a round failing that gate falls through to. All three run
**before** the swept step's ray, because the original ends a round inside its motion step and only
moves and collides what survives, and all three leave through `EndRound`, which shares the effect,
sound and splash paths a struck surface gets. `DetonatesAtRange` is the expiry's own question: the
original's rule is `LOCK_ON` and not `EXPIRES`, or `DETONATE_AT_RANGE`, and since this install
authors neither of the last two, carrying `LOCK_ON` is the whole rule — the choker, the cannonball
and the fake weapon vanish at their range where every other ordnance type detonates. `DefaultRange`
is the engine's own 500 m for a def authoring no `RANGE`. ⚠ `ProximityFuseTriggered` is a
DIFFERENT path and stays as it is: it sweeps the aircraft roster for anything passed near, is
aircraft-only by decode (`BL-233`), and holds fire while a candidate is still being closed on. The
two are complementary, and the `ordnance-end-conditions` suite flies a case where the sweep is
holding fire on a nearer aircraft while the target fuse goes off anyway.

**`RANGE_MINIMUM` is a hittability gate riding the same accumulator, and hides nothing.** A weapon
carrying both `FLYOUT_HEALTH` and `RANGE_MINIMUM` (`FlyoutUnhittableAtLaunch`, the torpedo alone)
spawns with `Proj.IntersectOff` set, and `ArmFlyoutIntersect` clears it once `Proj.Travelled`
passes the authored distance; `FlyoutStruck` skips a round while it is set. That is the whole
effect: `FUN_004cd210`, which the spawn and the gate call, writes node flag `0x10`, the
`INTERSECT_SURFACE` bit, and visibility is a different bit nothing on the projectile path touches
(`org/ordnanceTypes.md`, "Hittable distance"). The body and its `MODEL_ANIMATION` run from the
spawn frame like every other ordnance round's; the torpedo's folded launch look, its 3.5 s switch
to wings, prop and white puffs, and its beeps are all the def's own timeline, run by
`ProjectileFlyoutAnim.cs` below. `CollectFlyoutIntersect` is the seam a scripted run reads the gate
through and `CollectFlyoutBodies` the one it reads the launch look through. ⚠ An earlier reading
here had the round hidden for 300 m and held its trail back to match; both are gone, and the
`ordnance-end-conditions` and `shootable-flyout` suites assert the drawn-from-launch rule.

**A `TARGETABLE` or `FLYOUT_HEALTH` round carries a `ProjectilePool.Flyout`** (`SeedFlyout`,
minted for the torpedo alone in this data), which is both halves of "shootable": the admission byte
`TARGETABLE` sets on the target wrapper (`FUN_00441830`, the byte at `+0x6c`) and the armour/health
pair the spawn seeds from weapon `+0x8c`/`+0x90`. It is a CLASS because the player's target registry
re-finds a selection by source object every frame and a pooled round is a slot in a struct array with
no identity of its own; a round carrying neither key gets none of it, which is the same outcome as
the engine's **−1.0** not-shootable sentinel without the per-round allocation. The sentinel itself is
still carried, on a `TARGETABLE` round authoring no `FLYOUT_HEALTH`, and is what keeps the
`Health == 0` frame test safe. `Flyout.Spend` is `FUN_005abcf0`'s arithmetic verbatim: each pool
clamped at zero, health touched only once armour is empty. ⚠ It is NOT `PlaneDamage.Spend`
(`FUN_004b7f80`), which is a different, richer routine with an armour-shielded share; the flyout
branch is the plain clamp-and-subtract pair, and the two must not be merged.

**A flyout is struck through the pool, not through a Godot body.** `FlyoutStruck` walks the live
rounds and slab-tests each swept segment against the struck round's own box (`Proj.HitCentre`/
`HitHalf`, the `FLYOUT MODEL`'s AABB in the round's flyout pose), and the nearer of that answer and
the world ray's wins, which is `FUN_005b03f0`'s rule for its one intersect database. The round
excludes its OWN box and nothing else: the decode has no owner exclusion here, so a pilot can shoot
down a torpedo he launched himself. Splash never reaches a flyout — the pair is spent only on this
direct-hit path — and a flyout is never cover. A struck flyout spends the pair and the impact ends
there: no per-surface effect, no sound, no splash (`FUN_005abcf0`'s first branch returns). The
choice of a pool sweep over a physics body is deliberate: a `--run-tests` pass never yields a frame,
so a body's queued transform never reaches the physics server and a real ray through it misses.

**Destruction at zero is not a detonation.** `FUN_005af720` tests health `== 0.0` every frame after
the retarget callback and before the guidance gate, so a shot-down round neither steers nor moves on
the frame it dies. `DestroyFlyout` plays `DESTROY_ANIMATION` (`torpedo_destroy_effect`) at the
round's position through `EffectSink` and retires it; only a shootable round authoring no
`DESTROY_ANIMATION` falls through to the ordinary detonation, which nothing in this install does, so
a shot-down torpedo never sets off its warhead. `CollectFlyouts` is the seam a scripted run reads the
pair and the admission byte through. Pinned by the `shootable-flyout` suite.

`ProjectilePool` — the shared-world weapon-fire subsystem: a fixed pool of projectiles integrated
with `Ballistics` (VELOCITY/ACCELERATION/GRAVITY, ending at RANGE), plus tracer streaks,
muzzle flashes, and the per-surface IMPACT sound + effect model. Per-class impact looks: a
water hit instances the authored splash model and plays its def's own curves for the 2 s run
(`AdvanceSplash` — base disc 1→2→1.8 xz, column popped to ×100 Y collapsing to 0, the splash1/bsplsh
zrd values verbatim) on the shared world materials, which honour the models' authored
`lighting/fog: false` themselves since BL-214 — the hand-rolled `OverrideUnlit` this needed is
gone, verified pixel-identical on a C1B night water burst. C6 (`BL-265`) adds the def's other two
pieces: the 0.05 s opacity fade-in / 1 s fade-out (`OBJECT_OPACITY_FROM_TO` targets base AND column
together) through a per-instance translucent twin (`EnsureSplashFade`, `SceneBuilder.FadeShaderFor`
— never edits the shared cached material, since concurrent splashes share it) driven via
`SetInstanceShaderParameter`, and the column's `splash01→03` flipbook (`EnsureSplashFlipbook`,
reusing `TextureCycler`'s frame-swap machinery — registered manually because the gun splash's own
polygon binds to a non-cycling sibling material, confirmed against C1B's gamez data, so
`SceneBuilder`'s automatic per-polygon path never reaches it). Column *width* plays at 8×
(`SplashColumnWidthScale`; judged at the controls 2026-08-06 with the fades in — the authored quad
is 5 cm wide, sub-pixel past ~30 m, while the reference ticks measure ~0.35 m, which 8× matches;
the authored 1× stays reachable, `static readonly` not `const`, so the branch stays compiled). A dirt
(unclassified-terrain) hit takes the same single spark as every other unhandled surface: it has no
stand-in of its own.
A gun hit on a
buildings-classed surface spawns a ricochet spark burst + flash (`SpawnRicochet`, additive — a
judged stand-in: the authored `bld_damage.flt`/`rcochet1` are 2 of the 5 install-missing names). Each burst sprite carries its own orientation basis (`Sprite.Orient`): the muzzle flash
rolls in the firing plane's basis, the impact spark/explosion/debris faces the struck surface
normal (then tumbles, for debris) — a fixed world plane for none of them (BL-139); tracers stay
velocity-aligned, and none of the three is billboarded. A gun shot's muzzle flash (`BL-263`) is
the triad — three `_muzzle1` quads 120° apart sharing one continuous per-shot roll — **anchored
to the firing muzzle node** (`Sprite.Anchor`: Pos/Orient stored local, resolved to world per
frame in `RenderSprites`; a freed anchor skips the draw), so the flash rides the plane instead of
being flown through at speed. The per-ammo axis is `MuzzleAmmoIndex` (`{slug,dum,ap,mag}_muzzle1`
from the weapon's `FIRE` binding); the roll draws `Rng.Weapons`, so `--det` stays reproducible.
Each quad is
centred by default (`Sprite.Pos`), except the muzzle flash, which sets `Sprite.AnchorLeft` so
`RenderSprites` derives the quad centre from `Pos + Orient.X * (currentSize/2)` — the texture's left
edge (QuadMesh's default UV, U=0 at local X=-0.5) stays pinned at the muzzle as the quad shrinks over
its life, instead of the texture being buried under a centred blob. `AddMultiMesh`
draws every texture un-mirrored (a `Uv1Scale` mirror without a matching `Uv1Offset` here once
degenerated, under `TextureRepeat=false` clamping, into every sprite this pool draws — tracer,
muzzle, impact, smoke — rendering as a flat single-column colour stripe); a texture needing a
flip gets it from its own geometry instead, never from that shared material — the tracer streak
carries the same per-ammo axis (`tracer_slug`/`_dumdum`/`_armorpierce`/`_magnesium`) and takes its
UVs from its own mesh.

**The tracer is the authored shape, not a tuned sprite** (`org/tracers.md`). `CrossedStreakMesh`
is the original's `rabbit_blur`: two perpendicular quads in ONE `ArrayMesh` — so a round still costs
one instance — 0.2 m wide × 4.5 m long, its local +Y the front, U running 0-at-front to 1-at-tail.
`RenderTracers` scales both width axes by `TracerWidth` and the length axis by `TracerLength`
(`new Basis(xAxis*width, yAxis*len, zAxis*width)`) and places the round's position at the streak's
**tail**, geometry running forward, because that is where the engine attaches the model. A second
pass draws the **tip disc** — the authored bright head is a separate mesh (`TipTextures`:
`slugtip`/`dumdumtip`/`armourpiercetip`/`magnesiumtip`), `TracerTipSize` across, `TracerTipOffset`
ahead of the round and perpendicular to flight, so it self-hides side-on exactly as the data does.
Three consequences worth not re-litigating: **no camera** enters the streak basis (the crossed pair
reads solid from any angle — the eye is consulted only for the pixel floor below), **no growth ramp**
(the engine never scales a gun round; it is full size from the spawn frame), and **ordnance draws no
streak at all** (every rocket `FLYOUT` prototype is a missile body — its trail is the
`MODEL_ANIMATION` puffer smoke, so `RocketStreakScale`/`RocketExhaustScale` and the generic `tracer1`
entry are gone). `weapons.tracerLength`/`tracerWidth`/`tracerBrightness` still route the
constants through `Config`, but the defaults are now **measured off the model**, not tuned by eye —
and `tracerBrightness` sits at a neutral 1.0, since the ×3 overbright was compensating for the tip
disc and second quad this shape supplies itself. `RenderTracers` still floors the drawn width/length
per round against `weapons.tracerMinPixels` via `ScreenSize.MinWorldSizeForPixels`, screen-space
over the one shared world-space mesh, so bound to `ProjectilePool.Viewers` (a `ViewerSet`, this
file's own entry below) and sized for the **nearest** viewer — see docs/org/tracers.md for the
deliberate conflict this floor carries against the decode's 600 m LOD, and for the splitscreen bug
a P1-only floor produced. `TracerScreenSizeTests` pins the rule.
`Spawn(weapon, worldMuzzle, inheritVel, shooterId)` fires one round; one pool per session, fed by
every player's guns — `shooterId` is the firing `PlayerIndex` (`NoShooter` for the lab's), carried on
the round so the near-miss cue can exclude its own. `NearMissTargets` is that cue's registry (BL-087):
each step measures the round's ACTUAL travelled segment — hit/fuse point included — against every
registered aircraft but its shooter's, and reports the pass distance (`WarningShotCue`).
`RegisterAircraft` is the hittability half: the hit ray runs world+aircraft with each round
excluding its own shooter's registered `AircraftBody` by RID.
`ScoredShooters`/`CannonRoundsFired`/`CannonHits` are the wrap-up
board's "Shot %" counters: `Spawn` increments the fired side once per round actually created (the
pool was not full) and `Impact` increments the hit side, both gated on `weapon.IsCannon` and the
shooter's id being in `ScoredShooters` — `GameSession` populates that set with every human seat's
`PlayerIndex` on an Instant Action mission and leaves it empty otherwise, so the counters cost
nothing outside one. `Impact` is CSVM's single choke point for all three of the decode's hit sites:
a cannon round only ever reaches it through `SimStep`'s direct-hit ray, since the fuse/range-expiry
call sites below are both gated to rockets.
A struck plane classifies `Player`
and routes to `FlightController.TakeProjectileHit` (struck shape → part, armor-first damage),
carrying the round's `Shooter` id through so a kill is attributable — never the destructible
pipeline. A missing round may still fuse: the `DETONATION_DISTANCE` proximity fuse arms
against AIRCRAFT ONLY — never world geometry (BL-233: a world fuse detonated every rocket short
of its aimed surface and, with a null collider, locked every hardpoint weapon out of its
per-surface IMPACT entry) — checked AFTER the ray so a dead-on rocket keeps its direct hit, and
detonating at the round's CLOSEST APPROACH to the registered boxes within the swept step
(`AircraftBody.SegmentDistance`), holding while still closing at step end: most rockets author
`DETONATION_DISTANCE` equal to `IMPACT_PROXIMITY`, so a first-entry fuse would always detonate
exactly where the blast reaches zero. The fused-on body rides into `Impact` for
per-surface effect/sound selection, but its damage arrives only through `ApplyDamage`'s gather
(`GatherAircraftCandidates`): every registered plane inside the blast radius — never the shooter's
own, never a wreck — takes the weapon's ARMOR/HEALTH scaled by the splash falloff, measured to the
nearest point of its OWN boxes (`NearestShape`, 0 inside, the blast-neighbor-shape rule) and struck
at that box, so part mapping and kill attribution run the direct-hit path. Planes never enter
`DamageSink`; the destructible blast sphere stays world-masked.
`DamageSink` (→ `AnimRuntime.DamageAt`) turns a world hit into destructible damage —
`WorldDamageGate` (→ `ZeppelinRuntime.GateWeaponDamage`, F18) is asked per struck body first,
so a weapon without `DAMAGES_ZEPPELIN` cannot hurt a gasbag while its impact effect/sound still
play;
`EffectSink` (→ `AnimRuntime.PlayEffectAt`) plays the non-model impact effects, each with the
template basis `EffectOrient` chose — rockets on the runtime's own bound, gun hits under
`GunEffectTtl` 0.3 s (the `*_gunhit` family's longest authored stop, and the only bound the
stop-less slug defs have) and one play per `GunEffectInterval` 0.1 s per effect name = per firing
group (`GunEffectDue`, on the sim clock);
`WashSink` (→ `ScreenFlash.PlayBlend`) is the disabling wash's route to a struck human's pane and
`EngineDeadBounds` the choker's `ENGINE_DEAD` pair, both assigned by the session (null / the image
pair in a lab); `BeeperTags` (→ the session's `BeeperTags<FlightController>`) is the world's tag
list, assigned beside the sinks, read by the `BEEPER` hit's `TryTag` and the `BEEPER_SEEKER` round's
per-frame `PickTarget` (null tags and seeks nothing);
`SurfaceIdOf` is `public static` — the struck body's numeric surface id off `SceneBuilder
.SurfaceIdMeta`, the SAME index space the crash and graze cascades use, with `default`(0) for an
untagged collider (`FUN_005acf60`'s null-material arm) and `player`(6) for a struck `AircraftBody`;
`Impact` reads it and calls `ImpactOutcome.Resolve`, then `Apply` obeys the result and decides
nothing — the first 8
impacts log a breadcrumb of that record (`id/name` + `fx=`/`snd=`/`standin=`), so the probe line and the unit
assertion say the same thing, which is what makes a "these two surfaces look the same" report
answerable without a lucky screenshot (`BL-019`); rockets fly
their FLYOUT model body via `BuildFlyoutBody` (shared with `PylonOrdnance`) and run their FLYOUT
`MODEL_ANIMATION` def from the spawn frame (`ProjectileFlyoutAnim.cs`, resolved from the world
`AnimProgram`, ctor `flyoutAnims`): the trail puffers, the sonic's authored 8.73 rad/s body roll and
the torpedo's launch look all come off that instance. Gun shots add the
`muzzle_burst` secondaries: a pooled per-shot `gunshell` casing instance flying the def's
OBJECT_MOTION verbatim (per-shot nodes on purpose — a shared anchor under `CallAnimation`'s
already-live gate drops gun-rate ejections), the authored `muzzlepuffer` smoke on a gravity-free
sprite pool — 6 puffs the instant-spawn gloss of the def's 0.05 s interval over its 0.3 s window
(`BL-261`; the invented white eject-puff cluster the smoke had been misread as is deleted) —
and a pooled `OmniLight3D` flash from the def's `3rdperson_lts` range/colour variants, its
magnitudes TUNE. `PlaySound`
(FIRE's launch bark, played once per `Spawn`; IMPACT's per-surface hit) resolves a `SOUND_GROUPS`
name (e.g. the incendiary rocket's `ground_mixed_exp_sg` default impact) through `_soundGroups`
first, same as `WorldSounds.PlayOneShot` — `FIRE.SOUND` is null for every cannon in the data
(`LOOPED_SOUND_NAME` covers continuous gunfire instead), so the one-shot never doubles up (BL-211).
**D31 (`BL-370`):** these 8 voices are plain `AudioStreamPlayer`s, never `AudioStreamPlayer3D`, so
A2's per-pane-listener engine rule never touches them — splitscreen has to be applied by hand.
`PlaySound` now multiplies the existing `def.Volume * 0.2f` (the pre-existing tuned balance,
carried verbatim) by `MixGain` (the same equal-power `1/√N` figure `FlightAudio.MixGain` already
gives a plane's own-ship loops) and `DistanceGain` — a linear falloff between the sound's own
`RANGE` (1 at/inside the full-volume distance, 0 at/past the audible one, the same [full-volume,
audible] reading `WorldSounds`' positional emitters give the pair) measured to the NEAREST entry
in `PlayerPositions` (`GameSession.PlayerPositionsSnapshot`, C21's `PLAYER_RANGE` seam — nearest
human, not nearest pane camera, per this plan's seam-choice decision). A shooter's own muzzle bark
and impact read full volume (distance ≈ 0 to themselves); a firefight at the far end of a
splitscreen map fades for everyone else. `PlayerPositions` null (the weapon bench, `Suites.cs`
labs — no human to measure against) skips the term entirely, gain 1. `PlayShotSound` (a turret
gunner's launch bark) takes the same treatment from its firepoint's position.
Positive-`HEALTH_DAMAGE` blasts fall on the original's curve, `1 − d²/IMPACT_PROXIMITY²`
(`BlastFalloff`, fed `WeaponDef.ImpactProximitySqM` and a `DistanceSquaredTo`; `FUN_005acac0`,
docs/org/ordnanceTypes.md "Half two, the splash"), quadratic in distance so 0.75 at half the
radius, zero at it, and applied to both pools; `DAMAGE 0` effect radii never damage. The struck body
always takes full damage (`ApplyDamage`'s direct-hit branch, unscaled by falloff); every OTHER body
the blast sphere overlaps is scored from the nearest point on **its own collision shape**, not its
transform origin (`NearestBlastPoint`, BL-239) — a `GetRestInfo` query against the same sphere with
every other candidate body excluded, so a large neighbour (a zeppelin gasbag, a long building mesh)
is scored by how close the blast actually is to its skin, not by how far the blast is from wherever
its origin happens to sit. Falls back to the struck shape's bounds centre only if that query somehow
finds no contact. The original measures to a per-node bounding sphere instead; the shape is kept
because a Godot body has no such sphere and a chapter mesh's would engulf the map. Planes and world
bodies then share ONE candidate list (`_blastCandidates`), sorted nearest first, and each is
**cover-tested** before it takes its share (`BlastCovered`, C11): a ray from the burst, lifted
`CoverRayLift` 0.1 m along the struck normal so the wall the round hit shields what stands behind it
and not its own side, to the candidate's centre, world layer only (`_coverRay`; aircraft are not
cover, see the field), the candidate itself excluded; any hit drops it. A world candidate's centre
is the **bounds centre of its struck collision shape** (`BlastCentre`, the original's per-node box
centre), read off the physics server (`BodyGetShape` / `BodyGetShapeTransform` / `ShapeGetData`,
bounds cached per shape RID in `_shapeBounds`), never a node origin: a chapter mesh node's origin
sits at ground level, so a ray to it ends on the terrain and every building reads as covered, an
origin-parked root's is the world origin, and a clutter region body (`Clutter.BuildSolidCollision`,
`MapEdgeExtender`) has server-side shapes with no `CollisionShape3D` owner at all, on which the
`ShapeFindOwner` / `ShapeOwnerGetTransform` pair errors and returns identity. A burst deals one
share per world OBJECT, not per collider body: the original's buffer holds one entry per collidable
node, while `SceneBuilder.AttachCollision` splits one node into a body per surface class, so the
nearest-first walk keeps the first body of each `WorldCollision.OwnerOf` group and skips its
siblings (the struck node's group seeded as already spent, since it took the full figure). Bodies
`SceneBuilder` did not build stand for themselves, so genuinely separate parts keep separate shares.
At most `MaxBlastTargets` 32 candidates take damage per burst, the original's hit-buffer size; when more
are inside the radius the pool prints one `blast limit:` line naming the weapon, the burst and how
many were dropped; never silently discard a capped blast, and do not call it a "cap", which this repo
uses for captures). `MaxBlastBodies` 4096 is only the raw sphere
query ceiling. The fuse tests the whole swept segment per candidate plane (no tunnelling at
~20 m/step) and gates through `DETONATION_DOT_PRODUCT` toward the nearest hull point. The
1 N·s/HP impulse is TUNE (BL-227's open half).

## src/Flight/ProjectileFlyoutAnim.cs
The `FLYOUT MODEL_ANIMATION` half of `ProjectilePool`, a partial-class file. Every ordnance round
runs its own `AnimInstance` of its weapon's def (`he_rocket`, `sonic`, `torpedo_trail`, …) on the
real `SequenceRunner`, and the pool is the `ISequenceHost`: `StartFlyoutAnim` poses the def's
`RESET_STATE`, adds the initial sequences and fires their t=0 events inside `Spawn`, which is where
the original starts it (`FUN_005aef40` hands weapon `+0x110` to `FUN_004edda0`), and
`AdvanceFlyoutAnim` runs the instance each sim step after the round has moved, then ticks its
motions and feeds every emitter it has on. `Dispatch` runs the kinds these defs author against
the round's own model instance: `ObjectActiveState` (node visibility, the torpedo's folded wings
and absent prop), `ObjectScaleState`, `ObjectMotionFromTo` (`FlyoutTween`, `FromToMotion`'s rule
without the world runtime: absolute channels, held components), `ObjectMotion` spins (`SpinMotion`
on a child node; on the model root the rate becomes `Proj.RollRate`, since the flight pose rewrites
the root every frame), `PufferState` (`AcquireEmitter`/`StopEmitter` over the pool's reusable
`TrailEmitter`s, `Puffer.Emit` driving both `DISTANCE_INTERVAL` and `TIME_INTERVAL` states),
`Sound` (`PlaySound` at the round), `CallSequence`/`StopSequence`, and `CallAnimation` (the
`EffectSink`, placed where the round is now). Anything else is logged once and skipped, which
today is the rear-arc flare's `ObjectOpacityFromTo`. Targets bind through the def's symbol table
to the built node's gamez index first (`AnimRuntime.IndexMeta`), the original name second. The
host seam carries no round identity, so `_animSlot`/`_animPos`/`_animBasis` are pinned around each
`Advance`. `PufferState`s are cached per authored event so emitter reuse can match on identity, and
a round with no anim program (the suites' bare pools, the empty stage) runs none of this. Decode:
`org/ordnanceTypes.md`, "The launch look is the def's timeline".

## src/Flight/WarningShotCue.cs
The incoming-fire near-miss cue's shipped accumulator (player.json `warning_shot_max` 2.0 /
`_dissipation` 2.0 / `_interval` 1.0), plus the swept-segment-to-point distance a round's step is
measured with — lifted out of `FlightController` so both unit-test without a live node, the same
split `WeaponCursor` uses. One pass accrues 1.0, saturating at max, draining at dissipation/s; the
cue re-triggers no faster than the interval.

## src/Flight/AiNetFollower.cs
Walks an `AiNet` patrol graph as a waypoint stream. Given the vehicle's nose it is the decoded
walk (`FUN_00431e40`, `docs/org/aiPilot.md`): seat on the nearest node and fly the far end of the
edge whose leg best lines up with that nose, then the same pick at every arrival with the edge just
flown excluded. Nothing draws, so vehicles seated on one node facing one way leave it together,
which is what makes a group sharing one net fly in formation (`BL-498`). A caller with no nose
(`ZeppelinMotion`, and a zeppelin is not a vehicle in the original) keeps the older nearest-node
seat with branches drawn from its own seeded `Random` (per
plane off the `Rng.Ai` stream at spawn, never Godot's global rng). Aircraft-agnostic on purpose:
positions in, target node out; its two consumers are `AiPilot.Patrol` (aircraft) and
`ZeppelinMotion` (F17's kinematic node follow). Arrival is the decoded ALONG-LEG test — a tenth of
the leg's horizontal length, floored at 10 m (`docs/org/aiPilot.md`) — so a vehicle that cannot
turn tightly enough flows past its node instead of orbiting a capture sphere it never enters.
A zeppelin raises that floor to clear its own turning circle, which is invented and only a floor.
Also holds a net's live STOP-POINT flags, seeded from the file and rewritten by
`COMPLETED_STOPPOINT` through `SetStopPoint`: an armed node is never advanced past, and once the
walk is inside 30 m of it (`StopPointHoldM`, the zeppelin follower's own hold distance, not the
leg's arrival radius) `Holding` goes true. ⚠ Off by default — `observesStopPoints` is set only by
`ZeppelinRuntime`, because only the zeppelin follower reads the flag; the aircraft one reads a
node's danger-zone fields instead (`docs/formats/ai-nets.md`).
`StopsAt`/`Holding` also cover a STRUCTURAL dead end — the current node's only edge is the one just
flown — for an observing follower: decoded (`FUN_004bf9d0`'s own-node gate calls the level/hold
routine, `FUN_004bf500`, unconditionally, ahead of and regardless of any armed stop point), this is
what stops a zeppelin on an open route whose far node authors no stop point at all from re-picking
its only neighbour and shuttling the route forever (`BL-529`, `docs/formats/mission-entities.md`
"Route ends and stop points"). The aircraft follower keeps its own decoded turn-back at a dead end
(`PickOnward`'s degree-1 short-circuit) unchanged; the unconditional hold is opt-in on
`ObservesStopPoints`.
Pinned by `AiNetFollowerTests` + the `ai-net-follow` suite.
`ArrivedNode` is the danger-zone report: the node the last `Update` reached and stepped past,
which `AiPilot` reads for its tag, never the node being flown toward.
`Reseat` is the original's activation snap (`FUN_004b0f40` into `FUN_00432010`): an Instant Action
wave member ticks while it is inert, presence being no sim gate, so without it the member's first
update latches a node near its parking pose and it flies back there after the teleport (`BL-364`).
It optionally names an edge the next seat pick must refuse, which is how the danger-zone
exit continues the course: the original's exit (`FUN_00490590`) hands `FUN_00431e40` the edge id
the walk was on when the run began, so a racer set down by the ribbon beside its own entry node
cannot fly that leg again and re-lock the zone it just flew (`BL-615`). An undirected neighbour
list says the same thing by naming the edge's far end; the exclusion is spent on the seat it
applies to.

## src/Flight/DangerZoneRibbon.cs
The decoded danger-zone run (`docs/org/aiPilot.md` "The danger-zone run"), engine-free:
`DangerZoneRibbon` is one `dzpathN` route as the original builds it, the polygon's vertices joined
by cubics parameterised in metres plus the lane table (the zero lane and one per child node);
`DangerZoneRun` is a pilot's cursor on it (entered from the nearer end, walking the segments either
way, `Done` past the exit); `DangerZoneRail` is the state-5 integrator that writes the pose off the
ribbon in place of the flight model, closing the aeroplane's residual offset, banking the wings into
the bend and settling on the 155 mph cruise. Every constant is read out of the image and named at
its declaration. Pinned by `DangerZoneRibbonTests`.

## src/Flight/DangerZoneRibbons.cs
A mission's ribbon set read straight off the chapter gamez, independent of `--debug-dzpaths`: every
`dzpathN` node's route polygon by the route-versus-gate-pair material rule (`docs/formats/missions.md`,
never polygon index), its children as lanes, and `dzones.zrd`'s `disable` list as the inactive
flag. `ByIndex` serves a numbered net tag, `NearestEnd` the negative one. One instance per session,
shared through `AiPilot.DangerZones`, because lanes are occupancy-counted across pilots;
`CampaignDirector.Attach` builds it and hands it to every roster pilot.

## src/Flight/ZeppelinBroadside.cs
The pure zeppelin broadside law (M4 F19), engine-free: the decoded 90° arc
(`dot(toTarget, sideNormal) > 0.707` against the MOVING hull's lateral axis, `SideNormal`/
`TargetSide` — side alternation is geometric, the opposite cones never both bear, no cadence
invented), the per-cannon stowed→deploy→ready→fire machine (`Step` emits deploy/retract/
ready lists; deploy/retract durations come from the authored anim defs) with its own re-fire
timer (`cannon_fire_delay`, armed by `Fired` per cannon), `TryAim` (the intercept solve,
`AimAssist.TryIntercept` consumed), `PickGasbag` (the zeppelin-vs-zeppelin rand() pick over
the target's in-arc live gasbags), `FirstLiveTarget` (the decoded candidate walk over the
record's `targets` in authored order, `player` resolving like any zeppelin node, no team or
hostility read) and `CannonsEngaged` (the decoded zeppelin byte `+0xc`: off at construction,
written only by the script's `COMPLETED_ZEPCANNONS`; while clear `Step` deploys nothing and
retracts any ready cannon outright, which is why no shipped broadside ever fires on the player;
`formats/mission-entities.md` "Broadside firing" has the chain).
`Session/ZeppelinRuntime.Cannons.cs` wires it. Pinned by
`ZeppelinBroadsideTests` + the `zeppelin-broadside` suite.

## src/Flight/ZeppelinDamage.cs
The pure zeppelin kill arithmetic (M4 F18), engine-free: `Survivors`/`IsDead` over the record's
`healthy` list (counted literally, entry by entry — C5/M01 ships `gasbag5` twice), the
`AliveEngines` recount, `MayDamageGasbag` (= `WeaponDef.DamagesZeppelin`, the gasbag-only
routing gate) and `CrossedStages` (the `cannon_health` 0.6/0.3 stage list, injure_anims
semantics: every crossed threshold fires, once). The zone pools live in `DestructibleRegistry`;
`Session/ZeppelinRuntime` supplies the aliveness views. Pinned by `ZeppelinDamageTests` + the
`zeppelin-damage` suite.

## src/Flight/ZeppelinMotion.cs
The kinematic zeppelin motion law (M4 F17): flies a `ZeppelinDef` along its net through
`AiNetFollower`, forward-only along the facing, speed by `max_accel` toward `max_speed`, yaw and
pitch through the decoded steer law (`Steer`, `FUN_004bf530`/`FUN_004bf620`, one routine per
axis): the target is the bearing and the raw slope from the hull to the current node, the rate
asked for is the record's `max_rate_*` eased to `max_rate · (error/25°)²` inside 25° of error
(`EaseRad`), the live rate moves toward it at `accel_*`, and the angle advances by that rate
scaled by `speed / max_speed` (the authored one), so a stopped hull cannot turn. No per-step
pitch band: the record's ±30 is a degree-valued pair the original compares against radians, so
it neither clamps the initial pitch nor the drawn pose (`FUN_004bf950`), and the law here has no
clamp either (`docs/formats/mission-entities.md` "Steering"). ⚠ The bang-bang rate this
replaced (ask for `error/dt`, reach it at `accel_*`) rang up under `accel_pitch` 0.5°/s² into a
standing ±30° swing on level legs, which read at the controls as the Pandora diving along its
route; the ease is what damps it. Pure state — no Node, no flight model; `ZeppelinRuntime` writes
the pose onto the world node, and `ResumeAt` re-seats it in place after a scripted motion.
The stop-point half is the decoded approach (`FUN_004bf360`): full speed until the along-facing
range to a halting node falls under 250 m, then linearly down to zero; inside the follower's
30 m (`Dock`) the throttle is cut, the pitch holds, and the hull and heading decay onto the node
and the leg's bearing at e^(−0.2·dt), which settles it ON the node in plan and in altitude. A
follower still on its SEAT (`LegStartIndex` −1) takes the own-node law (`FUN_004bf500`) instead,
a station-keep on the record's own pose: pitch commanded to 0 and heading kept, both frozen by
the speed factor at speed 0. Pinned by `ZeppelinMotionTests` + the `zeppelin-motion` and
`zeppelin-pandora-dead-end` suites.

## src/Flight/AiPilot.cs
The non-player `FlightModel` driver: standing orders in (heading in the mission-data
`SpawnPoint.HeadingDeg` convention, altitude, throttle, optional `Patrol` net follower, optional
`Gunner` whose live target is chased at the decoded lead offset ahead of it, optional `Machine` —
D11's nine-mode state machine, which when set is stepped first and picks this step's AIM POINT and
parameter table: patrol flies the net node, a reached node's danger-zone tag starts a
`DangerZoneRun` (approach on the emergency table, then `RailPose` published for the host to apply
in place of the model step, then the net re-seated), pursue leads the gunner's target on
the engaged table (or aims at it outright for the head-on firing solution), lay off holds its
entry course and then walks the throttle toward `sixth_sense_factor` × the pursuer's speed so the
human catches up, evade flies the machine's orders, avoid crash aims 1000 m up on the emergency
arm, displaced 1000 m right of its own ground track (`ClimbOutBreakM`, invented and measured) for
a netted pilot but purely vertical for an ESCORTING one, which is the decoded aim both of the
original's laws build,
an evasive maneuver plays its `ManeuverExecutor`, stunned returns neutral sticks),
and an optional `Escort` (`AiEscort`) which, whenever its leader is in play, takes the dispatch
away from all of those but stunned and avoid crash, the original's own `mode wingman` fork,
one `FlightInput` per sim step out, read by a `FlightController` whose `Pilot` is set. A leader
that leaves play drops the pilot to the netless arm, which projects the orders it was left with
and so holds them; `CampaignDirector` is what re-seats such a pilot, on the lost leader's net. Pure over
the model state and its own fields, seeded randomness only, so a fixed-dt run is deterministic
(`AiPilotTests`). The original's own steering law is `AiControlLaw`; this class is only its driver
(docs/org/aiPilot.md). `Stun(seconds)` is the AI stun's entry (`FUN_004200d0`, reached by a
`SONIC`/`FLASH` burst and the smoke screen through `FlightController.TryStunPilot`, which holds the
victim guards): the mask is neutral stick and rudder with the throttle lever left where it was, the
three channels the original zeroes and the one it does not, so the aircraft stays on the flight
model and coasts under power. With a `Machine` the stun is its `Stunned` mode; without one the pilot
keeps its own countdown; `IsStunned` reads either and `ClearStun` is the respawn reset.

## src/Flight/AiControlLaw.cs
The original's own AI steering law, documented in `docs/org/aiControlLaw.md` — read
that page before changing anything here. An aim point, that point's velocity and one of four
parameter tables read out of the image in, one `FlightInput` out: a desired speed from the aim
point's own speed plus range-weighted lead terms, an intercept solve (`AimAssist.TryIntercept`) for
the direction, bank-to-turn with the elevator joining once the bank command is inside a deadband, a
wings-level rule, a low-speed unload, and a per-axis scale/limit stage off `PlaneStats`. The
throttle lever has one path, the walk toward the desired speed; the original's distance-gated
open-loop branch is not ported (`docs/org/aiControlLaw.md`'s throttle section says why).
Engine-free and pure over its arguments; pinned against the decode by `AiControlLawTests`.

## src/Flight/AiEscort.cs
The formation-escort law a netless `mode wingman` aircraft flies, which in the shipped data is the
campaign's `wingman_N` / `bswingman_N` roster blocks and nothing else (`docs/org/aiPilot.md`, "The
escort law"). A leader snapshot (position, attitude basis, velocity, whether it is the player) and
an optional target snapshot in, one station point and that point's velocity out, over the engine's
own five-state machine: close on the leader, hold the body-frame station, fly a station on the
target, and the two re-join states nothing in the law enters. Every constant is decoded, the two
stations included ((6, 0, 18) off the player, (8, −2, −8) off an AI); the 80 m separation push is
what makes the hold a weave rather than a tight join. Engine-free and deterministic, holding only
its state and last station; the driver is `AiPilot.Escort`, the table `AiLawParams.Wingman`, and
the live check is the `wingman-station` suite. The campaign hand-off is not a join problem: at the
first stepped frame after C3/M01's intro the wingman reads 117.6 m and 53.6 m/s against the 700 m /
20.576 m/s gate and is in `Station` on the next frame, so a wingman that ends up high and behind
left the escort law rather than never entering it.

## src/Flight/AiModeMachine.cs
The nine-mode AI state machine, owned by `AiPilot.Machine` and stepped from its `Next`:
the mode list and vocabulary are the engine's own debug-readout dispatch (`NameOf` returns them
verbatim; docs/formats/ai-rosters.md "AI modes, engine-side"). Decoded and wired: activation
into pursue inside `min_ai_active_dist`/vehicle `attack` (both 2000 shipped, `AiSkills`/
`PlaneStats`); a hit rolls steady-hand (`NotifyDamage`, called from
`FlightController.TakeProjectileHit` for AI planes) and a FAILED test breaks off; a pursued AI
target entering an evasive state rolls sixth-sense and a FAILED test stuns for
`stun_recovery_interval`; `Stun(seconds)` is the one stun entry (the original's `FUN_004200d0`,
shared by that roll, a `SONIC`/`FLASH` hit and the smoke screen): it enters `Stunned` from ANY
mode, dropping a running maneuver or climb-out, and a stun landing on a stunned pilot OVERWRITES
the remaining time (clock + seconds, no max), which is what lets the smoke screen refresh it every
frame; the mode clears on its own clock and returns to the mode it interrupted, and an external
`Enter` releases it with no stale expiry; an evasive maneuver is an `EligibleFor`-culled, signature-weighted,
seeded library draw played to `ManeuverExecutor.Done`, then back; `lay off` is D15's rubber-band
assist (decoded: the mode and `sixth_sense_factor` 0.994→1.07, "the ease-off while pursued") —
pursue eases into it when a chasing HUMAN target has fallen behind, and it releases when the
pursuer catches up or stops chasing; `AssistEnabled` false (`--no-assist`) never enters it.
Transitions raise `ModeChanged` (the session's `ai mode:` log lines); rolls raise `RollLogged`
in the engine's pass/fail wording. `avoid crash` runs the original's three altitude bands: below
`AltitudeFloorM` (20) the climb-out arms with no ray at all, above `ProbeCeilingM` (8000) nothing
is cast and a running one releases, and between them ONE ray along the aeroplane's own velocity,
cast every 0.5–1.0 s per plane, decides, releasing on the first clear ray (docs/org/aiPilot.md).
Engine-free; pinned by
`AiModeMachineTests` + the `ai-modes` suite. Named inventions (evade's scramble run, the probe's
minimum reach, lay off's entry/exit cones) are marked at
their own declaration. The two danger-zone modes are entered only by `AiPilot`, off a reached net
node's tag; `approaching` still runs the crash check and hands back to itself, `navigating` runs
nothing (the pose is the ribbon's), and a stun or climb-out out of either returns to the approach.

## src/Flight/ManeuverExecutor.cs
Plays one library maneuver's timed step program as `FlightInput` values — `Next(model,
dt)` each sim step until `Done`, consumed the way `AiPilot` is; D11's state machine holds one per
`evasive maneuver` run and switches back to its own law on `Done`. Steps are TARGET ATTITUDES in
degrees (not stick deflections, not rates), composed onto the entry frame — level entry-heading
frame normally, the full entry attitude for a `relative` maneuver; a positive-duration step is
held for its time, a zero-duration step advances when the attitude is captured. Pure and
engine-free; deterministic on a fixed dt (`ManeuverExecutorTests` demonstrates a real Bloodhawk
flying the shipped dive and split_s).

## src/Flight/AiGunner.cs
The AI's forward-gun gunnery: per sim tick the host `FlightController` hands it the
fire geometry (`Solve`), it answers with the trigger (`WantsFire`) and the intercept, and each
round leaves along `ShotDirection(muzzlePos)` — the line from THAT barrel to the intercept point
(wing guns converge; parallel lines straddle a fuselage) perturbed inside the dead-eye cone, one
seeded draw per shot. Gates in the engine's order: the quick-draw cone off the TARGET's nose/tail
axis, the separation inside the slot's authored engagement window (1–900 m on every AI gun), then
the airframe's ±11° `gun_pitch`/`gun_yaw` clamp on the lead (`AimAssist.TryIntercept`, consumed
never re-derived) with the residual the clamp leaves gated at 10°, so the employable cone is the
traverse limit plus the gate (`docs/org/aiPilot/aiWeapons.md`).
Engine-free (`AiGunnerTests`); the live half is the `ai-gunnery` suite.

## src/Flight/AiRocketeer.cs
The AI's ordnance employment, the gun path's twin (`docs/org/aiPilot/aiWeapons.md`): per sim tick
the host `FlightController` (`DriveAiRocketeer`) ages the vehicle-wide lockout and hands over the
fire geometry (`Solve`), which answers with the trigger (`WantsFire`), the hardpoint it chose
(`SelectedPylon`) and the direction the round leaves along (`LaunchDirWorld`, the clamped mount aim
rather than the raw lead). Gates in the engine's order: the quick-draw cone aborting the whole pass,
then per pylon the armed check, the two-way `DAMAGES_ZEPPELIN` match, the squared engagement band
and the traverse clamp's residual against `AimQualityCos` = 0.9962, cos 5°. That constant is the
ordnance half of a decoded pair and is deliberately not shared with `AiGunner.AimQualityCos` = 0.9848,
cos 10°: a lead 18° off the nose clamps to 11° and is taken by the gun and refused by the ordnance.
The lockout is stamped BEFORE the `quick_draw_chance` roll, so a failed roll spends the whole refire
interval instead of retrying next tick.

**The lead is solved in the frame the round flies in, per pylon** (`org/aiPilot/aiWeapons.md`, the
branch table under the lead solver). A pylon whose weapon authors `ACCELERATION` (`RoundAccel`,
`wep_04` among the AI's ordnance) takes `TryMotorIntercept`: the round starts at rest in its
launcher's frame and climbs to `VELOCITY` at `ACCELERATION`, so the intercept is where the
separation from the target's RELATIVE track equals the path flown, `0.5·a·t²` inside the ramp and
`VELOCITY·t − VELOCITY²/(2a)` past it, bisected where the original roots a polynomial
(`FUN_00462ce0`). A pylon without a motor takes `AimAssist.TryIntercept` at `VELOCITY` against the
target's WORLD velocity, since such a round is seeded at its cap rather than off its launcher
(`FUN_00460e30`, the world-frame call). The difference is large at the shipped numbers: `wep_04` at
450 m/s off a 150 m/s² motor needs 3 s to reach its `VELOCITY`, and a 600 m shot at a 100 m/s
crosser leads 26.5° where the constant-speed solve leads 12.8°. `AiGunner` keeps the relative-frame
constant-speed solve, which is the original's third branch, the `CANNON` one.

Three properties of the original hold here by construction rather than by a test. The aim gate is
skipped for the player, and this class only runs for a non-human pilot, so player fire never reaches
it (a human's rocket leaves along the aircraft's own axis through `FireControl`). It holds no target of its
own, mirroring the original's single validated target across the whole weapon walk. And the
`DAMAGES_ZEPPELIN` match's zeppelin side is unexercised in play: the AI acquisition admits aircraft
alone (`BL-363`), so `targetIsGasbag` is always false at the call site, and no shipped stock loadout
carries `wep_14` because the vehicle def's `weapons` tuple is not parsed yet (`BL-394`). Both halves
of the match are pinned in tests against the Black Hat Warhawk's authored fit instead.
Engine-free (`AiRocketeerTests`).

## src/Flight/AiVoiceDispatcher.cs
The E16 trigger dispatch, engine-free (`docs/formats/combat-voice.md`): events in, (speaker,
clip, outcome) decisions out — the talker roll, the hardcoded halving on bearing ids 1–12, the
broadcast speaker election (one line per event, a failed roll passes to the NEXT candidate,
wrapping), the DI tiers at 70/50/30 % most-severe-first, the death cries (20/21) with force, and
`BearingTriggerId` = 1 + 3·bearing + band. Availability comes from the injected resolver
(`CombatVoice.PlayableFor` + `HasStream`), never def presence. Pinned by
`AiVoiceDispatcherTests` + the `ai-voice` suite.

## src/Flight/AiTargetRanking.cs
The decoded target-ranking formula (docs/formats/ai-rosters.md "AI modes, engine-side"):
`rank = weight × 1200 + distance + objectiveBias`, MINIMISED; player base weight 0.7, others
1.0, ±0.2 weight terms for bearing / altitude sign / target facing, `1e21` beyond the
activation radius (never picked). Snapshots in, index + `TargetScore` out, engine-free
(`AiTargetRankingTests`); `SelectBest` prefers the best candidate no ally already holds and
falls back to the overall best when the pool is exhausted (the design's deconfliction, minimum
reading); `ObjectiveBiasFor` matches `rating_biases` patterns, first match wins, and saturates at
both ends — 1.0 or more is always-target, −1.0 or less is the exclusion.

## src/Flight/IncomingFire.cs
`--incoming[=metres[,wep_id]]` — the near-miss test rig: a phantom shooter 120 m on each player's
six, alternating sides, firing the target's own gun (or the named weapon) into the shared pool under
a shooter identity no player holds. Exists for deterministic near-miss testing: an AI gunner
(D14's `--ai-attack`) is a real shooter but aims to hit, and a splitscreen pilot needs a second
human. Aims along the target's own nose with a lateral offset, so the round overtakes on a
parallel track and the pass distance holds without lead maths.

## src/Flight/PhysicsConstants.cs
`PhysicsConstants.NomGravity` — the single 20 m/s² player.json `nom_gravity` value, shared by
`PlaneStats.Gravity`'s default and `ProjectilePool.WorldGravity` so the two can't drift apart.

## src/Flight/PlaneStats.cs
Typed per-plane stats: vehicle.json `dynamics` (resolved through the `kind_of` def chain), the
def's `turrets` block as `TurretMount`s (title + node per viewpoint rig — the host→`ai.zrd`
gunner link) +
engines.json stock engine power + player.json globals (the flight constants, the near-miss cue's
`warning_shot_*` block, the gun aim assist's `sticky_bullet_catchup_rate`/`_forget_interval`
(`AimAssist.cs`'s B2), `_dist_factor` (B4's scoring) and `_inaccuracy` (B5's launch scatter, stored
in RADIANS as the original stores it), the Cockpit head's `autohead_turn_time`/`_turn_max`/
`_turn_min_pitch` (C22, `HeadLook.AutoheadTarget`'s constants — `turn_max`'s authored-vs-default
asymmetry, docs/formats/vehicle/player-globals.md), plus the decoded model's
lift/AoA/G, turn/yaw-curve, pitch-fade and drag-fade-speed globals — docs/org/flightModel.md; converted
exactly as the original does: MPH×0.44704, AoA/liftAOAs cosined, highGs/lowGs raw G; the turn/yaw
curves, the pitch fade, the G limiter and the AOA window are all live in the model, the first two
neutral on the authored values and the window binding on all of them), the `crash` block's
`bounce_factor` (a raw scalar, read one level down inside that block
— the collision restitution's ceiling), the `engine_sound` def name with its
volume/pitch `SoundCurve`s (clamped two-point ramps), `destroyable_parts` → `DestroyablePart`
records (name, max HP, max armor, `critical`/`engine` flags, `got_hit_anim`, per-part
`injure_anims`), the def-level `VehicleInjureAnims`, and the `collision` probe list as
`CollisionProbes` (nearest def in the damage chain: the player def's six points, or
`basic_airplane`'s single origin probe on every AI load, which is the shape `FlightController`'s
AI sweep carries). Schema: docs/formats/vehicle.md.
Two flavours of one airframe: `Load` resolves everything down the player chain, `LoadForAi` takes
  ONLY the damage model (pair, parts, def-level ladder) from the AI def's own chain — the player
  def's name minus its leading `p`, validated — and leaves `DefName`, dynamics, turrets and the
  model on the player chain. An AI aircraft is therefore **zone-less**: an authored `armor`/`health`
  pair and NO `destroyable_parts`, which is what every roster-named def chain resolves in the
  shipped game (`docs/org/vehicleDamage.md`'s 2026-08-16 correction). `DefName` stays the player
  def on both flavours on purpose (`AiDefName`'s own doc); `damaged_engine_sound` and
  `cockpit_engine_sound` both parse fully — `EngineAudioCurves.EngineDefFor` is what selects
  between them and the plain `engine_sound` (see `src/Flight/EngineAudioCurves.cs` below).
  `LoadForAi` also reads the AI chain's `title` message key into `AiTitleKey`, the authored name a
  militia def carries ("MSG_VEH_MEDUSA_KESTREL") and a plain def inherits from its airframe;
  `AiFlightAssembler` resolves it into `AiTitle` for the targeting readout.
  `VehicleMode` carries the def chain's own `mode` key, and `WithAiSpawnJitter` is gated on it: the
  original jitters the `jet` and `heli` classes only, so the `mode wingman` family flies its authored
  dynamics (`docs/org/flightModel.md`, "The per-spawn jitter"). `WithRosterDurability` applies the
  roster block's own `init_health`/`armor` override (aiv slots 7/66) to `VehicleHealth`/
  `VehicleArmor` before `WithEnemyDurability`; both arguments arrive already gated, so null always
  means unset, never an authored zero (`docs/org/vehicleDamage.md`).
`DamagedTimer` (a `DamagedEngineTimer`, one mutable `Elapsed` field) is the damaged-engine
re-arm timer's shared state (C22, `docs/formats/vehicle.md` "What makes an airframe damaged").
⚠ Every `With*` method's `MemberwiseClone` carries the SAME reference forward from the cached
def, on purpose: it is what makes every aircraft flying one airframe share one counter, as the
original's own def field does. Do not reassign it in a new `With*` method.

## src/Flight/SpawnPoints.cs
Reads the flight spawn from a mission's OWN zrdr (`extracted/<chapter>/<mission>/zrdr/` — a
different archive than the shared `--zrdr`), two schemas both yielding
`SpawnPoint(Position, HeadingDeg)`: `LoadIa` (instant-action ia.json `spawn_points` per scenario;
only IA1 folders have one, the original picks one at random per launch) and `LoadPlayerInit`
(story objectives.json `PLAYER_INIT`, position + yaw). Schema: docs/formats/spawns.md.

## src/Flight/MissionTargets.cs
Loads a mission's targets.json: target KEY → objective display keys
(`description`/`category_label`/`help_label`), resolved through `Messages`, plus the valueless
`objective`/`other_target` marker flags a mission starts with. A bare node name keys itself and a
nested `[parent, child]` entry keys `parent/child`, the same key `ObjectiveTarget` gives the
script's target directives, so the two tables meet on one string. `Load(mission, chapter)` is the
original's reader search path (`init.gw`'s `RdrAddPath` chain): the mission's own file, else the
chapter's, one file whole and never a merge. ⚠ Read both scopes for a campaign mission: C1C/M01
ships no targets.zrd and every one of its objective labels sits in `C1C/zrdr/targets.zrd`.
Generic across mission types; no file in either scope yields an empty set. `ByNode` exposes the
whole table for a consumer that needs the starting flags rather than one key's labels. Schema:
docs/formats/missions.md.

## src/Flight/MarkerDraw.cs
The world marker's drawing primitives, shared by `MarkerHud` and `TargetHud`: the
shadowed reticle, the edge arrow with its tail stroke, and the centred text block (`Lines`) with
the pane-clamped variant (`LinesClamped`) an off-screen marker needs. Owns the marker blue and
the drop shadow; colour and scaled sizes stay with the caller, since each HUD scales through its
own `HudMetrics.Scale`. Where a marker GOES is `EdgeMarker`'s; this is only what it looks like.

## src/Flight/StuntMission.cs
Stunt Flying state: `Load` builds the ordered zone list from ia.json `dzones` (HUD positions via
`GameZ.WorldTransformOf`, gate polygons from `dzpathN`; strings via MissionTargets + Messages; null
when a mission has none → free flight); `Update` requires both polygon-plane crossings in either
order, fires events, advances the target; clock/scoring via
`Elapsed`/`CompletedAt`/`CompletionOrder`/`InCompletionOrder`; `ForAnotherPlayer()` clones an
independent run so the archives parse once per session. Logs through `Log` (`Utils`), not
`GD.Print`/`GD.PushWarning` (`engine-free-suites` A2) — the class itself has no other engine
dependency, which is what lets `stunt-gates` move off-engine in A4.

## src/Flight/HudMetrics.cs
The one place the flight HUD decides how big it draws: `Scale(control, reference = 1440)` =
window height / reference, damped by `PaneFactor` = sqrt(paneH/windowH) inside a splitscreen
pane (2P ≈ 71 %, 4P 50 %; the damping exponent is TUNE). CompassTape, GaugeCluster, MarkerHud,
StuntScoreboard and FlightController's text block all route through it.
`hud.statusTextScale` and `hud.markerTextScale` multiply only their matching flight-HUD text,
clamped from 0.5 to 2.0; arrows and layout remain at the base scale.

## src/Flight/HudFont.cs
The game's own HUD bitmap font, rebuilt from `extracted/rimage/5pointhud.png` (+ the brighter
`5pointhudbrite.png` highlight variant): a proportional 5-px font covering printable ASCII
`0x20`–`0x7e` (layout/colours: docs/formats/hud.md). `Load` returns null (one log line) if the
atlas is absent; `Draw(CanvasItem,…)`/`Measure` render onto any caller's canvas, sized via
`HudMetrics`. The E34 foundation E35/E36 draw with.

## src/Flight/WeaponReadout.cs
The selected-weapon text readout: a bottom-centre two-line `Control` drawing the current gun
group + rocket type and their live ammo in `HudFont`, from the game's own `MSG_HUD_GUNGAUGE` /
`MSG_HUD_MISSLES` templates (`Messages.Fill`, never hardcoded). FlightController pushes the state
each frame (`%1` = gun mount name / rocket display name, `%2` = per-group / per-pylon rounds).

## src/Flight/ImpactReticle.cs
The gun aiming reticle: a viewport-filling `Control` drawing `impact_point.png` (the game's
pipper, from `extracted/rimage/`) at a world impact point fed each frame by FlightController,
projected via `Camera3D.UnprojectPosition` at `_Draw` time (mirrors MarkerHud, never cached).
Fixed screen size scaled by `HudMetrics`; one per player pane. What the pipper follows — the nose
axis at 0.5 s of flight, range-smoothed, deliberately never the assist's line — is decoded in
docs/org/aim-assist.md "What the gun pipper follows".

## src/Flight/EdgeMarker.cs
The off-screen edge marker's placement rules, engine-free and pure: `Resolve(projected, behind,
paneSize, margin)` answers on-screen vs edge-clamped (the margin-inset rect test, the
behind-the-camera mirror, the degenerate-direction fallback, the clamp along the direction to the
inset boundary) as a `Placement`, and `ClockHour(ownPos, headingDeg, targetPos)` is the "N o'clock"
bearing. Owns `RefEdgeMargin`. The camera stays with the callers — `MarkerHud`, `VersusHud` and
`TargetHud` project through their own pane's camera and keep their own arrow/tag/label styling.
Off-engine coverage: `CSVM.Tests/EdgeMarkerTests.cs`.

## src/Flight/MarkerHud.cs
The stunt objective marker HUD: a viewport-filling `Control` drawing the on-screen reticle/text
block, the off-screen edge arrow (`EdgeMarker.Resolve` + clock-hour bearing), run status and banners
(`CompleteBanner` branches solo vs race); one per player, sized via `HudMetrics.Scale(this)`.

## src/Flight/StuntScoreboard.cs
End-of-run results overlay: plain Godot UI (dimming backdrop → CenterContainer →
PanelContainer → VBox + 3-column split grid) filled from `StuntMission.InCompletionOrder()`;
wakes on `RunCompleted` (records via `ScoreStore.RecordIfBest`, logs the split table to stdout
for headless review), branching NEW BEST vs BEST on the stored record. Raises `HaltReason.Ended`
and carries a Restart · Exit `BoardMenu`; `_Process` releases both once `AllComplete` clears, so
R and pad Y reach the rerun without going through the menu. Not built while Instant Action is
active — `IaWrapupBoard` carries the splits there instead (`BL-358`).

## src/Flight/ScoreStore.cs
Stunt best-time persistence: one JSON object in `user://stunt_scores.json` keyed
`chapter/mission/plane` → `{best, date}`; `GetBest` / `RecordIfBest` (returns whether it was a
new best — never worsens a record). The public `Load()` always opens the player's own file; an
internal `Load(storePath)` overload exists only so a suite can point at a throwaway path instead
(`instant-action-stunt-summary`) — never the player's own store.

## src/Flight/StuntRace.cs
The internal `Remove` operation is compensation for an uncommitted roster build, including removal
of that racer's completion subscription; normal race membership remains append-only.

Splitscreen race bookkeeping: one `Racer` per player (own `StuntMission`, `Rank`, `FinishTime`);
finishing stamps the next placing, `RaceCompleted` fires when the last pilot is in; `Standings()`
orders finishers by placing then in-flight players by progress; `Restart()` (rematch) resets
every mission and clears placings — the planes are respawned by GameSession, which owns them.
Its console lines, and `StuntScoreboard`/`StuntRaceBoard`'s, route through `Log.Info("flight", …)`
instead of a bare `GD.Print`. Off-engine coverage:
`CSVM.Tests/StuntRaceTests.cs` (finish ordering, rematch reset, standings ties).

## src/Flight/StuntRaceBoard.cs
The race's shared ranked results overlay: same clean-Godot-UI construction as StuntScoreboard,
but covering the WHOLE window — on its own CanvasLayer (Layer 10, above SplitScreen's 0) under
the session root, one row per player from `StuntRace.Standings()` (placing, tag, plane, zones,
total + gap to the winner; DNF when unfinished). Wakes on `RaceCompleted` and raises
`HaltReason.Ended`; `_Process` hides it and releases the clock once `AllFinished` clears, so R and
pad Y reach the rematch without going through the menu. Carries a Restart · Exit `BoardMenu`
driven by player 1, Exit's label following how the session was launched.

## src/Flight/VersusMatch.cs
Dogfight deathmatch bookkeeping: `RegisterKill(shooter,
victim)` scores the shooter and tallies the victim's death, `RegisterDeath(victim)` tallies a death
alone (terrain/mid-air — no killer, no score change); `Advance(dt)` is the host-fed match clock;
`MatchCompleted` fires once on kill threshold or time-out (leader wins, equal top kills draw);
`Standings()` ranks by kills descending with ties sharing a rank; `Restart()` (rematch) zeroes every
score and re-arms completion. Off-engine coverage: `CSVM.Tests/VersusMatchTests.cs` (threshold win,
time-out win, draw, post-completion no-op, rematch re-arm, each limit disabled on its own).

## src/Flight/VersusHud.cs
Per-pane Dogfight HUD, `--vs` only: a compact status line — remaining
time (omitted once `VersusMatch.TimeLimit` is disabled), this pane's own K/D, and the current
leader's tag — drawn in MarkerHud's run-status slot (`RefStatusY` — Stunt and Versus are mutually
exclusive, so the two never compete for it); a transient "P2 DOWNED P3" kill banner ("P3 DOWN"
with no killer); and one opponent marker per living rig (`Rigs`, excluding `PlayerIndex` and any
`Controller.Crashed` seat) — an on-screen tag at the projected point, or the edge-arrow +
clock-hour bearing (`EdgeMarker`'s placement) when off screen/behind, in that
opponent's own `SplitScreen.PlayerColor`. `Build(match, playerIndex, camera)` binds the match +
this pane's own camera (opponent markers project through it, exactly like MarkerHud's zone);
`Rigs` is attached once by `HumanFlightAdapter` (the SAME live list `GameSession` keeps, not a
snapshot — every seat exists before this pane assembles, only `.Controller` fills in as siblings
do); `PlanePos`/`HeadingDeg` are fed every frame by `FlightController`, same site as `Marker`'s.
The status line pulls the match live every `_Draw` (no pose to project for it) and `OnKill` is
pushed once per `Downed` report by GameSession's own broadcast — a second subscription, never
piggybacked on the scoring one, so every pane hears every kill/death, not just the two it
happened to.
The single-target marker whose shape this reuses is `TargetHud`'s; the original has no per-opponent
marker at all, so drawing one per human opponent is CSVM's own splitscreen answer to its radar.
The shape's provenance is decoded in [`org/targeting.md`](org/targeting.md).

## src/Flight/TargetHud.cs
The per-pane targeting HUD, built on EVERY human pane in every flight session (`--vs` panes get one
alongside `VersusHud`): the pilot's own selected target from `TargetSelection` (a campaign
mission's objective sites included, since they ride that same selection), a nearest-AI-hostile
fallback where no selection exists, and `--debug-markers`' every-live-aircraft overlay. Draws the
original's bracket box, label block and off-screen edge arrow + clock bearing, and owns the colour
table (`MarkerColor`), the label layout (`LabelLines`), the selected gun's reach gate (`GunReaches`,
fed by `FlightController.GunReachesTarget`) and the debug identity string (`DebugTag`). The marker's
decode is [`org/targeting.md`](org/targeting.md); the edge placement and clock bearing are
`EdgeMarker`'s, only the styling is this HUD's own. Pinned by the `hostile-marker-hud` suite,
`HostileTagTests` and the `c1-targeting-hud` golden.


## src/Flight/VersusBoard.cs
The dogfight's shared results overlay — `StuntRaceBoard`'s construction
almost verbatim: winner (their own `SplitScreen.PlayerColor`, or "DRAW" on a tie) on top, then one
ranked row per player (tag, kills, deaths) from `VersusMatch.Standings()`, covering the WHOLE
window on its own CanvasLayer (Layer 10, above SplitScreen's 0) — the match ends for everybody at
once, unlike a per-pane HUD element. Wakes on `MatchCompleted`, hides in `_Process` once
`Completed` clears (a rematch); `Populate` runs ONLY from `OnMatchCompleted`, so the drawn rows
stay the ones the match actually ended with even after `Restart()` zeroes the live state —
`StuntRaceBoard`'s `FinishTime`-snapshot discipline, achieved here for free since `VersusStanding`
is a value-type snapshot already. Raises `HaltReason.Ended`, so the world stops rather than leaving
the losers to fly under a board that has already counted them. Carries a Restart · Exit
`BoardMenu` driven by player 1; Restart routes through `GameSession.RestartMatch` (mirrors
`RestartRace`: `VersusMatch.Restart()` then every rig respawns), which clears `Completed` and lets
`_Process` do the hide and the clock release. R and pad Y reach the same call directly, owned only
while the board is up via `FlightController.Match is { Completed: true }` — polled in
`PollResultsShortcuts` off the rendered frame, since the halt means no sim step runs to read it.

## src/Flight/HaltReason.cs
Why the sim clock is stopped, as a flags set: `Paused` (a player asked, and carries an owner) and
`Ended` (a results board is up). The clock advances only when the set is empty, which is what lets
two systems halt it at once without either resuming it out from under the other. `PauseState`
arbitrates; nothing else writes a reason.

## src/Flight/PauseState.cs
Who is holding the sim clock and why (`BL-373`) — `VersusMatch`'s engine-free shape: no `GD.*`, no
`Godot.` type, no `Node`. One instance per session, built by `GameSession.BuildFlightRigs` ahead of
the rig loop (the assembler hands it to the per-pane stunt board) and assigned to every rig's
`FlightController.PauseState`. `TryToggle(playerIndex)` owns the pause half: not paused → pauses
and claims `OwnerPlayerIndex`; paused → resumes only if `playerIndex` matches the owner, otherwise
a silent no-op (`Changed` does not fire on a rejected attempt, so `PauseBoard` never flickers on
another player's futile press). Refused outright while `Ended` is set — the results board's own
menu already offers the only two things left to do, so a pause menu would stack on nothing.
`Raise`/`Clear` own the results-board half and REJECT `Paused`, which carries ownership and must
go through `TryToggle`; `ForceResume` drops a pause whoever owns it, for a rerun or an exit chosen
from the menu. Off-engine coverage: `CSVM.Tests/PauseStateTests.cs`.

## src/Flight/PauseBoard.cs
The shared pause overlay (`BL-373`) — `VersusBoard`'s WHOLE-window construction (pausing
stops the game for everybody at once, not one pane), built once by `GameSession` on its own
CanvasLayer (`UI.HudLayers.Board`, same layer the race/dogfight/wrap-up boards share) and wired to
`PauseState.Changed` instead of a match/race completion event. Shows "PAUSED", the pausing
player's tag in their own `SplitScreen.PlayerColor`, and a `BoardMenu` of Resume · Restart · Exit
driven by that same player alone — `PauseState` lets only the owner resume, so binding the cursor
to the owner keeps one rule rather than two, and stops a second pad steering a menu whose Restart
and Exit decide the whole session. Exit's label follows how the session was launched. A fresh menu
each pause, so the cursor starts on Resume and a stray confirm cannot destroy a run.
`Populate()` runs only on a fresh pause (mirrors `VersusBoard`'s snapshot discipline); `Visible`
tracks `PauseState.Paused` on every `Changed` event, and `_Process` polls the host only to move
the cursor. A campaign session's objectives readout (`UI/ObjectivesHud`) rides the same pause on a
layer of its own rather than inside this board, since it belongs to the flown mission and this
board is shared by every mode.

## src/Flight/IaWrapupBoard.cs
Instant Action's wrap-up board — `VersusBoard`'s WHOLE-window
construction, since the mission ends for every human at once (decisions 10/14), not
`StuntScoreboard`'s per-pane shape. Four label/value rows (Time to Complete Mission, Enemies Shot
Down, Danger Zones Completed, Shot %) — the langui titles at ids 1134-1137, kept as literal
strings rather than read off `ui_strings.json` at runtime, since that table is a build-time
extraction artifact of the `.rof` archive and not one of the five archives `SessionArchives.OpenFor`
loads. Unlike `VersusBoard`/`StuntRaceBoard` it takes no live match object at all: `Present`'s
arguments are the caller's own snapshot, handed in once from `InstantActionRuntime.MissionEnded` —
`InstantActionDirector` owns every source (the mission clock, the kill tally, `ProjectilePool`'s
shot counters, the summed `StuntMission.CompletedCount`) and this class only draws what it is given.
On a `stunt_flying` mission `Present` also takes a `StuntSummary`, and the board grows the run's
zone splits, total and best-time row in `StuntScoreboard`'s layout; the scoreboard is then not
built at all, which is how `BL-358`'s two stacked boards became one. Safe because the scoreboard
only ever existed single-pane: several pilots take the race branch, which builds none.
Raises `HaltReason.Ended` on `Present` and carries a Restart · Exit `BoardMenu` driven by player 1.
Nothing else retires this board — a mission that has ended stays ended — so unlike the race and
dogfight boards the hide and the clock release happen on the menu's own Restart. That Restart is a
restart and not a rerun: it reaches the Launcher's `RestartSession`, which rebuilds the world,
because the mission's waves, ace and zeppelin cannot be put back in place.

## src/Flight/Weather.cs
`WeatherState`: per-mission atmosphere from the flown mission's own weather.json — per-zone
`ZoneWeather` records holding fog (`FOG_COLOR`/`FOG_RANGES`/`FOG_ALTITUDE`), `SUNLIGHT_*` →
`WorldLight` (`SunIncidence` 0.46 / `MinWorldLight` 0.15, TUNE) and `SUNLIGHT_ORIENTATION` →
`SunOrientation`, the same block's uncollapsed `SunDiffuse`/`SunAmbient` and their two colours
(what both lighting arms drive the aircraft light from), plus the `CLOUD_COVER` whiteout band (`WhiteoutAmount` trapezoid), `WIND`, and
precipitation → `PrecipData`. Schema + colours + zone names: weather.md.
`DefaultDiffuse`/`DefaultAmbient` (1.5 / 0.5) are the install's modal day pair, public because both
lighting mappings anchor a zone against them and the `NoFog` zone a weather-less mission gets is
authored from them. They are deliberately not `WorldLightFactor`'s own 1/0 fallbacks, which belong
to the faithful collapse and must not move; every shipped weather.json authors both keys, so
neither fallback fires.
**The original's weather/sky/fog/light runtime is written up in [org/weather.md](org/weather.md)** —
the camera weather state machine, the `zone_id` visibility gate, the zone apply's edge trigger, the
band flicker's two curves, and the sun/dome/deck rules. Read it before changing a weather mechanism.

`PreferPopulatedHorizonZone` is the second, geometry-driven correction (`BL-277`): a request whose
`horizon/zone*` subtree carries NO meshes yields to the one zone that does, which is what makes
C1B/C2/C3 render a sky at all. The `ResolveZone(requested, horizonZones)` overload runs both in
order and takes the geometry pick ONLY if this mission's weather.json also defines fog for it, so
sky and fog are always the same zone. Census + per-chapter table: weather.md; the census itself is
`WorldBuilder.HorizonZonesOf`; coverage `CSVM.Tests/SkyZoneTests.cs`.

`CameraWeatherState(cameraPosition, fogZoneArmed, volumes)` (`FUN_0042ee40`) is the binary's
per-frame camera zone 1/2/3, published by `WeatherRig.Tick` onto each
`PlayerRig.CameraWeatherState`: 1 default; 2 when `HasCloudBand` and the camera's altitude is
at/above `CloudCoreBottom` — a THIRD spelling alongside `CloudBandCentre` (the deck-regime flip) and
`CloudBottom` (the visual floor), all within ~80 m of each other in C1 but never unified (Decision
1); 3 when `fogZoneArmed` (`FogVolumeSpec.FogZoneArmed`) and the camera is inside any `FogVolumeBox`
— the exact half-space `Contains` test, not the AABB — and state 3 wins over state 2 on overlap
(never actually exercised in shipped data: only C5 arms `fogZoneArmed`, and its band sits far above
every C5 volume).
`ZoneForState(state)` is B11's consumer-side half: state *n* asks for `zone<n>` and goes through
`ResolveZone`'s file fallback, so a mission with no `ZONE<n>` keeps its first zone rather than
falling to `NoFog` (no fog, fullbright). Deliberately NOT routed through the horizon-aware
`ResolveZone` overload — that one owns the DOME's single per-flight zone; the fog zone changes
underneath it every time the camera crosses the cloud core.

## src/Flight/FlightAudio.cs
Own-plane non-positional loops (engine, overspeed whine, rattle) + one-shots: `StartEngine`/`EngineStartRamp` prop-start fade (re-fired via the loop-restart hook
in `Update`; `EngineStartRamp` 2.0 s is sourced from startprops' authored prop cross-fade duration,
not a bare literal), `OnCrash` → `snd_exp_plane1..4` (the `plane_destroy_sg` group every crash def
that names it wants), `OnGroundExplosion`/`OnWaterExplosion` layering the boom the *chosen crash
def* authors (`snd_exp_ground_a` off `player_crash_dirt` itself, `snd_exp_water_a` from the
`plane_big_splash` inside the sea dive — the crash runtime renders effects, never sound; the
fallback `player_crash_default` Sounds only `plane_destroy_sg`, so it layers neither),
`OnEngineStop` → `snd_propstop`, called right after the explosion one-shots on every crash/destruction
(`FlightController.Crash`) so the loops end on the authored wind-down cue instead of a cut,
`OnGraze(water)` → the survivable scrape's authored `snd_exp_water_b`/`snd_exp_ground_b`
(touchdown.zrd; rate-limited by `FlightController`, not here, and its argument now comes from the
CHOSEN `touchdown_*` def, since the sound is authored inside that def). `OnWarningShot` draws one
`bullet_warning_sg` variant per near miss (player.json `warning_shot_sound` is a SOUND_GROUPS name, so
`Setup` takes the group table too; rate-limited by `FlightController`'s `WarningShotCue`, same split).
The engine is ONE voice on one slot; its pitch, gain and definition all come from
`EngineAudioCurves`, shared with `AiEngineAudio` (see that entry). `UpdateEngineSlot` swaps the
slot's stream for `damaged_engine_sound` while `EngineAudioCurves.EngineDamaged` holds (worst zone
below a quarter health, or the engine choked) and for `cockpit_engine_sound`
while the pilot's selected first-person view (`FlightController.FirstPersonView`) is
Cockpit or Nose, both resolved at `Setup`; damaged takes precedence when both apply
(`EngineAudioCurves.EngineDefFor` carries the rule — no def authors a damaged cockpit variant, and
the plan's evidence does not decode which of the two wins, so damage feedback keeps priority as a
port decision). The view swap keys to the SELECTED mode, not the per-frame camera pose, so a held
numpad key or look-behind does not retrigger it (D31, closes `BL-161`). `MixGain` is the only
own-ship scale left and stays here — splitscreen, not a fidelity knob.

## src/Flight/EngineAudioCurves.cs
The engine-audio slot maths both audio paths read: `EngineDamaged`, `EngineDefFor` and
`DamagedPitchMul` (whether the airframe counts as damaged, which definition the slot then holds, and
the swap's one-off pitch draw), `Engine` and `Whine` (each slot's pitch and gain off the
`PlaneStats` curves), `DriveFrom`, and `CullDistanceSq`. It exists because the original runs one
per-frame routine for the player and every AI vehicle; the decode is in
[formats/vehicle.md](formats/vehicle.md), "The engine audio's slots" and "What makes an airframe
damaged". The damaged swap is a health-fraction gate, not a took-a-hit one.
`AdvanceDamagedRearm` is C22's re-arm timer, pure and testable off a `DamagedEngineTimer` and a
caller-drawn `u` rather than a live RNG (`DamagedPitchMul`'s own reason): only `AiEngineAudio`
reaches it today, since the own-ship path is never culled and so never silences a playing loop.
The engine slot's parameter is **not the throttle lever alone**: `DriveFrom` reads a turn rate off
the two body axes perpendicular to the nose and a climb attitude off the orientation, and `Engine`
adds them to each curve's NORMALISED parameter under a [0, 1.5] clamp before the curve maps it out.
That ordering is why `SoundCurve` exposes `Frac`/`Remap` separately from `Eval`, and the headroom
above 1.0 is what lets a hard pull overshoot the curve's own top. The turn-rate-into-volume term is
inert against the shipped flat volume curve and is kept because the data, not the mechanism, is what
makes it so. `BL-109`'s `CAP-10` measurements are the acceptance test, pinned by `engine-note`.

## src/Flight/AiEngineAudio.cs
The positional twin of `FlightAudio` that an AI-flown aircraft carries instead of it: the same two
engine slots on `AudioStreamPlayer3D`s, plus the cull that stops them past `CullDistanceSq` from
the nearest listener and starts them again inside it. `Attach` is the whole spawner-side surface.
Deliberately carries no own-ship concept — no `MixGain`, no start ramp, no prop-start cue, no crash
one-shots: an AI kill is audible from its crash animation's own authored `Sound` events. Audio
cannot be screenshot-verified, so the `sound` log carries the whole observable: one line per
aircraft at build naming what each slot resolved to (or that there was no archive at all), then one
per cull transition and one per damaged-engine swap. The pair is what separates "silent past the
cull" from "silent because the definition never resolved".
On the healthy->damaged edge, `SetEngineDamaged` stops the slot-0 handle but does not swap it;
`Update`'s `ArmDamagedLoop` is what waits out the shared re-arm timer
(`PlaneStats.DamagedTimer`, `EngineAudioCurves.AdvanceDamagedRearm`) and performs the swap once it
fires (docs/formats/vehicle.md, "What makes an airframe damaged"). ⚠ The timer sits on the
airframe DEFINITION: `PlaneStats`'s per-spawn `With*` clones carry the SAME `DamagedTimer`
reference forward from the cached def, so every aircraft flying one airframe shares one counter
and one draw, and a damaged loop that is still playing never re-enters it. The damaged->healthy
direction stays immediate. Proven by `ai-engine-rearm` and `EngineAudioModelTests`.

## src/Flight/SpectatorCamera.cs
The `--freecam`/`--anim-lab` observation camera: WASD move, RMB-held mouse look (captured only
while held), wheel speed, pads via `Pads.For(_padDevices)`. Every key and pad read resolves
through `InputContext.Camera` (`src/Bindings/`) and the mouse stays on `InputEvent`s. Lab
additions `Frame(Aabb)`, the
`FollowNode` orbit-lock (released by any translation input; `ExitFollow` keeps orientation) and
a public `Camera` accessor — all inert in plain `--freecam`. Rates TUNE.
While locked, the orbit answers the mouse **and the pad**: right stick swings it, the triggers
dolly it (`OrbitPad`, RT out / LT in, the sense `FlightController.OrbitInput` already uses) on
`CameraDollyOut`/`CameraDollyIn`, which read the whole of a trigger's travel rather than borrowing
the boost gate's half-travel threshold. The
target key `F` / pad `X` (`CycleLock`) re-locks onto what `LockCandidates` offers, nearest first
then outward (`OrbitLock.Next`) — the only route back into a lock, since a release is otherwise
one-way. `GameSession.LockCandidateAircraft` supplies the roster (every `InPlay` aircraft, AI and
human) to all four spectator cameras; a null provider leaves the key inert.
⚠ The target key is handled in `_UnhandledInput`, NOT polled like the axes. This camera reads raw
key state, which bypasses `SetInputAsHandled`, so a polled edge would fire behind a host that has
already consumed the key — `BL-279`'s mechanism. The same rule is why the anim lab's def picker
moved from `F` to `F18` (`BL-428`) rather than the two sharing a key. Pad button events are gated
through `ReadsPad`, the event-side twin of `Pads.For`, so `--no-pads`, an unfocused window and a
per-seat binding all still hold.
It is also the pane a pilot out of the mission watches from, an Instant Action pilot out of lives
and a co-op campaign human whose aircraft is lost while the others fly on, both through
`Session/SpectateHandoff.cs`: the lab's own `FollowNode` orbit is what "follow a live aircraft"
needed, so nothing was added for it. Constructor params `padDevices`/`useKeyboard` (
`BL-375`) default to null/true — every connected pad plus the keyboard, unchanged for `--freecam`,
the anim lab and the weapon lab — but the director passes its rig's own
`FlightController.PadDevices`/`UseKeyboard`, the same filter the flying panes use, so two
splitscreen pilots watching at once move independently instead of lockstep. Mouse look has no such
split (one physical mouse) and stays shared. ⚠ Default start is the mission spawn — RANDOM per
launch; pass `--pos`/`--direction` for comparisons. ⚠ `KeyboardCaptured` zeroes keyboard axes while
a text field owns focus — raw key polls bypass GUI focus. ⚠ Vertical is Q/E plus the **Z/U**
  alternate — not C/Space: C toggles the collider overlay and Space fires guns, and because this
  camera POLLS raw key state, sharing either key moved the camera as a side effect of the other
  action (`BL-279` moved Space off; Z stays clear of C for the same reason).

## src/Flight/OrbitLock.cs
`SpectatorCamera`'s re-lock rule, taken as positions and an index rather than nodes so it runs
off-engine (`OrbitLockTests`) — the same split `Pads.AssignPads`'s pure overload uses. `Next`
answers the nearest target to the eye from no lock, the next one outward from a held one, and
wraps past the farthest back to the nearest so no press is ever a dead one. It RANKS rather than
sorts: the answer is an index into the caller's list, so a sorted copy would only have to be
undone. Ties break on the index, so two aircraft at equal range still get distinct turns and
neither is unreachable. A `current` that is out of range reads as no lock, which is what a stale
index (its plane gone since the last press) resolves to.

## src/Flight/FlightModel.cs
The decoded, data-driven aircraft plant. Rotation sums stick torque, bank coupling, `return_rate`
weathervane and ground blow before exponential `ang_momentum_damp` decay. `BodyRates` stores the
original's quaternion half-angle rate; `PhysicalBodyRates` and the attitude update double it. Authored speed curves
scale the stick command only, roll on the base ramp and pitch on that ramp times the authored
high-speed fade. `OpposingCommandLimitAt` softens the pitch and yaw commands that swing the nose
further off the flight path, on the decoded G ramp and the decoded AOA window; `AoaLimiterFactor`
is the window's A/B seam (1, the default, is the decode); the pitch rate it produces is the answer
and the filmed 33 °/s is discarded beside it.
Translation composes the original's own chain: the lag vector as a clamped lift demand plus
decoded Mach drag, thrust and gravity; the velocity direction rotates only through that lift and
the ground-blow steer (`NoseChaseFactor` 0 pins the retired kinematic chase out, config-selectable
for A/B). The
footage altitude clamp, the STALL lamp's fraction and the dive-speed cap are ours, and are the
whole of what is not decoded in the file; every constant's class is in
[`org/flightModel.md`](org/flightModel.md)'s inventory, censused by `FlightConstantInventoryTests`.
`UsesAiForcePath` holds near-field differences, and `FarFieldPlant` is the original's level-of-detail
branch, re-decided every step: an AI aircraft more than 1 km horizontally from the NEAREST human
pilot holds `throttle · fd_speed + 5` along its nose at a 1/s lag and skips lift, drag, thrust,
gravity, the authority curves, the command limiter and the bank coupling, keeping its stick torques
and the ground blow. The range arrives as `FlightInput.NearestHumanDistSqM`, which is 0 for a plant
nobody tells; player-only guards widen to all humans. `Collide` is the decoded contact response
whole: the placement (0.03 m off the surface for a human, the sweep's stop exactly for an AI) and
the human-only normal impulse on velocity and body rates, with NO tangential, friction or
vertical-speed term, so a scrape bleeds speed only through repeated impulses; contact lifecycle
and the remaining invented laws (`C22`) stay in `AircraftContactResolver`/`FlightController`.
The choker's extend-only timer zeroes thrust alone. There is no engine torque: every write to the
original's angular accumulator is a product of state-derived vectors and none reads the throttle,
so the rotational plant is mirror-symmetric and `EngineTorqueAbsenceTests` pins it that way; do not
re-chase the GDD's one-sided turn assist. `FlightInput.Boost` is the nitro flag: it REPLACES the
lever with `BoostLever` 1.8 at the thrust read (the throttle and its slew are untouched, so an idle
boost is a full-throttle boost) and scales the drag coefficient by `BoostDragFactor` 0.8; the
lifecycle that sets it is `NitroSystem`. Full decode, and every mechanism's class (decoded, named
product exception, or unsupported): [`org/flightModel.md`](org/flightModel.md), "Parity ledger";
measurement rules: `verification.md`.

## src/Flight/NitroSystem.cs
The original's nitro boost lifecycle, engine-free (docs/org/flightModel.md, "Nitro"): a 30-unit
tank burned at 4/s while boosting and refilled at 1/s always, so a burn nets 3/s and runs 9.5 s
from full to the 5 % cutoff; the human arm (`HumanCommand`) engages on a held command only from a
99 % tank and re-asserts the flag until the cutoff, so nothing stops a burn and the refill to
re-arm takes 28.2 s; the AI arm (`AiSet`) has no engage line and fires once per nitro-flagged
maneuver. Both refuse an engine-out aircraft and a re-engage while the boost or decay animation
is alive; the boost animation lives at least 1 s after an engage. `Installed` is the injector
(the hangar's nitrous engine ids 3-5, or the roster block's `nitro` slot); `EngagedThisTick` and
`ReleasedThisTick` are the edges `FlightController.AdvanceNitro` turns into the shake kick, the
`nitro_boost`/`nitro_decay` defs and the `snd_nitro` loop.
⚠ **The edges are cleared by `BeginStep` at the top of the step and nowhere later.** The original
carries no edges at all: the play, the player shake and the force-feedback effect run inside
`SetNitro` itself, so these two flags stand in for that single call and have to survive from the
arm that raises one to the reader at the end of the same step. The tank update (`Advance`) runs
after the command arm, as the original's per-vehicle update runs after its input handler, and
clearing them there deleted every engage: the boost still accelerated the aircraft, while its
animation, its shake and its loop sound were all cancelled before anything read them. The release
edge survived that, being raised by the tank update's own cutoff, which is why the decay half
looked healthy. `AdvanceNitro` plays the defs with
`AnimRuntime.Play(name, PlaneModel, applyReset: false)`, the same fallback-anchor shape
`startprops`/`stopprops` already use: the defs' own anchor NAME (`warhawk`, `plane_props.zrd`)
never resolves inside a per-plane crash rig's index, so `PlayWithin` (no fallback) silently played
neither. What an engage shows is the `nitropuffN` exhaust puffers at `exhaust1..4` and the
`prop1..3` fade, both authored to last one second; the disc swap (`nitropropN`) shows on no flyable
aircraft, and the decode says it never did, because the original resolves a `LOCAL_NODES_ONLY`
definition's names strictly inside the calling vehicle's own node subtree with no global tier
(`org/flightModel.md`, "Nitro"), and the discs ship only on the separate bare-named library root.
Regressions: `nitro-boost-anchors` (the call shape on a replica rig) and `nitro-boost-flown-rig`
(the engage edge and the emitting puffers on the rig `WorldEffectsFactory` builds for a session,
reached through `FlightController`'s own step). Every constant is censused by
`FlightConstantInventoryTests`; `NitroSystemTests` pins the lifecycle and the force couplings.

## src/Flight/PathFollower.cs
The engine's SECOND movement law, and the exclusive alternative to `FlightModel`: the dispatcher
picks between them before any flight law runs, so nothing here is a steering input. Pure state and
maths, driven by `Session/ScriptedPathVehicles.cs`. Holds two independent flags, `Following` (the
path owns this vehicle) and `Frozen` (it is placed and waiting), because folding them together
cannot express the state most authored path vehicles spend a mission in. Every constant is decoded,
not tuned. The final leg steers at, and ENDS at, the decoded point 300 m along the leg from the
waypoint behind it, raised with speed, so a run whose final leg is shorter than that flies past its
last waypoint climbing (`FinalLegOvershoot`; the motion's `Pitch` is the bearing to that target's
height, exposed for whoever poses the vehicle). What is NOT pinned by the decode: the ride height,
which for the aircraft movement classes reads a vehicle-type field this project has not identified.
Law, constants and the gap: [`org/flightModel.md`](org/flightModel.md), "The scripted-path
follower".

## src/Flight/PropAnimator.cs
Spins the flying aircraft's prop/rotor blur discs: Build collects every node PropParts classifies
(rest pose + rate, radians/second local axes). Advance recomputes each disc's absolute pose from
its stored rest pose through `SpinMotion.ComposeSpin` — the same accumulate-from-rest decode
`AnimRuntime` plays the ambient world's own `XYZ_ROTATION` spins through — rather than stepping
`RotateObjectLocal`, so a long flight session cannot drift. FlightController drives it
throttle-scaled with a PropIdleSpin 0.4 floor (0 while crashed). --fly only — the static viewer
keeps the still disc.

## src/Flight/ThrottleSlamSmoke.cs
The throttle-slam exhaust smoke (`CAP-21` re-read): a large sudden throttle increase streams the
`nitro_boost` def's own `nitropuffN` puffers (AT_NODE `exhaust1..4`) from the plane's exhaust marker
nodes for a few sim-seconds, reused WITHOUT the rest of that def (no `nitropropN` discs, no
`snd_nitrostart`) — the capture shows the same trail-smoke shape on a plain throttle jump with no
boost. `Update(dt, throttle)` runs an edge-triggered gate: it tracks the throttle at the start of the
current unbroken climb and fires once per climb the instant the cumulative rise crosses
`SlamThreshold`, never again for that climb and never on a flat or falling throttle — so tapping one
notch at a time (each tap separated by a flat/falling frame) evaluates fresh every time. Drives the
puffers via `Puffer.Emit`/`Stop` (DISTANCE_INTERVAL, the same mechanism
`DamageVisuals` uses for the nose smoke trail) — the authored def is a distance-triggered trail,
not a time-interval burst. `Reset(throttle)` (crash/respawn) hard-stops any plume and re-anchors the
climb tracker so the throttle jump those moments make is never itself read as a slam.

## src/Flight/FuelTank.cs
The flown aircraft's tank, engine-free so the arithmetic is testable without a scene. `Step(dt,
lever)` takes `dt · lever · 5` off `Remaining`, clamps at zero, and returns whether the throttle
lever may move this tick; it returns false on a dry tank, which is what freezes the lever where it
stands rather than closing it. The lever handed in is the value entering the tick, before that
tick's slew, matching the original's ordering. `Capacity` comes from `PlaneStats.FuelCapacity` (the
def's `fuel` key, 54926 on every player airframe) and `Fill` is the spawn-time top-up; a
non-positive capacity frees the lever instead of freezing it, so a fixture without the authored key
still flies. Only `FlightController.ReadKeyboard` burns, which is the original's player-only gate:
an AI-flown or scripted aircraft leaves its tank full, and a crashed airframe burns nothing while
still moving its lever. Nitro costs no fuel, because the burn reads the lever and not the boost
flag. Decode: `docs/org/flightModel.md`, "Part-throttle equilibrium"; the key:
`docs/formats/vehicle.md`.

## src/Flight/SpeedCue.cs
Its internal `Dispose` removes every puffer node when roster assembly is rolled back.

Loads `cuepuffer1..3` directly from the chapter's `speed_cue.zrd` and drives exactly one through
`Puffer.Emit` at the aircraft pose. Camera altitude selects the authored 30/15/8/15 m density
bands; within 50 m AGL none emits, while the script's empty branch above 1500 m preserves the
current selection. One instance is built per player and its puffer renderers are stamped onto that
rig's visual layer, so splitscreen panes never see another pilot's private speed cue. It shares the
session `EffectAmbience`, so the authored 500–600 m camera-distance fade and wind apply. `Reset`
hard-clears all three on crash/respawn so a teleported aircraft cannot bridge its old position.

## src/Flight/ControlSurfaceMix.cs
The decoded angle solver behind the surfaces, engine-free so the arithmetic is testable without a
scene: roll drives the two aileron slots at ∓0.5 rad, pitch the two elevator slots at −0.6 rad with
a ±0.18 rad differential roll term added, yaw both rudder slots at −0.61086524 rad times the
reverse-authority factor. Aileron and elevator targets are clamped (±0.5, ±0.6); the rudder is not.
Each slot then decays exponentially toward its target at 2/s, exp(−rate·dt), so the shape holds at
any frame rate. `Advance`'s `animate` flag is the original's player-only guard: false writes no
slot at all, which is how an AI aircraft's surfaces stay frozen. Addresses and the list population
that fixes which node takes which slot: docs/org/flightModel.md, "The original's control-surface
animation".

## src/Flight/ControlSurfaceAnimator.cs
The node side of that: collects every classified surface, poses it as an absolute pose (build-time
local basis, Basis = base · Rot(hingeAxis, slotAngle · scale)) rather than accumulating, and owns
the two frame flips the original does not need: a mount rotated by yaw π, and a nose-mounted
canard, which raises the nose the other way. --fly only; frozen while paused/crashed, reset on
respawn. `FlightController` passes its `IsHumanPiloted` as the guard, which is the plan's Decision
3: every human pilot animates for splitscreen, AI stays frozen as the original has it.

## src/Flight/WingLightBlinker.cs
Flashes the wingtip flares for FlashDuration ~0.033 s (TUNE — matches the original's own footage at
~1 frame @ 30 fps; the authored 0.0001 s literal is unusable) each WingLights.BlinkPeriod, and
toggles a matching OmniLight3D per flare (range/colour from WingLights) on the same window. Gated on
the session's ANIMATION_LOD quality flag (SessionSpec.AnimLod vs AnimRuntime.HighLod) — below HIGH
the flares/lights stay off for the instance's life, the def's authored low-detail branch. Reset
(respawn) restarts the cycle with everything off, handing every suspended lamp back. Advanced each
_Process, frozen while paused or crashed. --fly only.
`Suspend(flareName)` (BL-287) hands a named lamp to whatever just deactivated it — the fuel-leak stage
(`player_fuelleak`'s `OBJECT_ACTIVE_STATE … wing_flare2 false`, never reversed by the def) used to
fight the blink cycle, which unconditionally re-asserted the flare/light `Visible` every tick and
undid the leak's deactivation within 1.5 s. `HumanFlightAdapter`'s `DamageEffectSink` calls
`Suspend("wing_flare2")` right after playing `player_fuelleak` through the rig runtime — a suspended
lamp's index is skipped outright in `Advance`, deterministically, not by detecting a stray write after
the fact (which would miss the common case where the blinker's own commanded state already happened
to read "off" the instant the leak fires).

## src/Flight/PylonOrdnance.cs
The rockets mounted under a plane's wings: `Build` instances ONE FLYOUT MODEL body per loaded
pylon via `ProjectilePool.BuildFlyoutBody` (the SAME gamez prototype the round flies), parents it to
that pylon marker at identity local transform (nose -Z forward, tail at the mount = the launch pose),
and `Update` shows/hides each per its live `Hardpoint.Ammo`. FlightController drives `Update` after
UpdateRockets; the mounted body rides the plane and is freed with it. --fly only. `Unmount` takes the
set back off — detaching each body from its pylon IMMEDIATELY, not merely queueing it — so the weapon
lab's rebuild-on-swap cannot leave the old ordnance hanging beside the new.

## src/Flight/PlaneCollider.cs
Derives up to 8 plane-frame convex hulls from the built model's mesh triangles alone (no per-plane
data): region-clipped geometry (tail, wing, fuselage out to the wing band), greedy volume-guided
refinement cutting one OR two parallel planes per axis (the double cut separates bilateral pairs
like twin fins), then one `ConvexHull` per refined piece. The refinement and the part order are
judged on the pieces' boxes, so the hull is only the emitted shape and never moves a cut or a name.
Single-sourced: the terrain sweep casts these hulls AND `AircraftBody` mounts the same
`ConvexPolygonShape3D` resources as the plane's hittable body — never a second derivation.
`Layout` is the engine-free half (`Triangle`s in, named `Region`s out) the `airframe-hull-coverage`
suite and the unit tests measure; `Build` wraps it in shapes.

## src/Flight/ConvexHull.cs
A convex hull over a point cloud with no engine dependency: vertices, outward faces, edges, bounds
and volume, plus `Contains` and the point-to-surface `Distance` the fuse and blast passes ask.
Incremental construction on millimetre integer coordinates with exact 64-bit volume signs, so a
near-coplanar mesh cannot fold it (a float-epsilon hull did, on two airframes). A cloud thinner
than the thickness floor along an axis is padded to it first, the per-dimension floor the box
shapes applied; a cloud too degenerate to hull falls back to its padded bounding box.

## src/Flight/ShakeDefs.cs
Typed reader over the shared `shakes.zrd.json` — the six shake-oscillator sources
(`docs/formats/shakes.md`), modelled on `WeaponDefs`: load once (`GameSession` →
`AircraftAssemblyResources.Shakes`), named accessors per source, unhandled-key tripwire.
Each source is one law (frequency/damp/sawtooth) plus exactly one magnitude-term variant
(`magnitude_factor` [+ `he_factor`], `min_speed`+`magnitude_quotient`, or absolute `magnitude`);
absent sources read as null and `PlaneShake` no-ops them.

## src/Flight/PlaneShake.cs
The plane-wobble oscillators: gunfire buzz (`fire_bullet`), overspeed rattle (`high_speed`),
being-hit rocks (`bullet_impact`/`missile_impact`/`explosion`) and the nitro engage (`nitro`, one
kick of its absolute authored `magnitude`, human pilots only), summed each sim tick into
`Roll` — radians the controller writes to `ShakePivot`, the node the assembler hung the plane
model under. Engine-free on purpose (unit-tested); the pivot write is the controller's one line.
Amplitude = `magnitude_factor × caliber` in radians of roll, **measured** off original footage
(`analysis/gun-wobble-shake/`); a rocket hit stands in its armor damage (declared TUNE).
`high_speed`'s input is speed over the plane's `fd_speed`, so the authored `min_speed` 1.0 gate
means "beyond rated max" — the dive rattle whose magnitude is the EXCESS over the gate,
`(speedRatio − min_speed)/quotient` (zero at rated max, gentle overspeed ramp); cruise stays
silent like the footage's idle floor. `ContactHit` is block 5, the one oscillator no def authors:
it runs on the constructor's law (2 Hz, damp 4.5) and takes its magnitude from
`CollisionDamage.ContactShake`, which every resolved contact spends for a human pilot.

## src/Flight/FlightControllerBuild.cs
The internal construction handoff from `FlightRoster` to `FlightController`. It contains one
resolved controller's pre-tree state from either flight adapter, and `Bind` consumes it exactly once, before tree
attachment; a repeated or late bind is a construction error. The roster keeps the data-resolution
and lifecycle ordering, while the controller keeps its runtime interface. `HoldSegments` (the
scripted hold profile, when a caller wants one) rides this DTO the same way `Pilot` does, so
`FlightController` never exposes a public field for either arm; `Bind` copies it to the private
`_holdSegments` field before resolving `IFlightInputSource` (below) and storing it, alongside the
`IWorldQuery` seam.

## src/Flight/IFlightInputSource.cs
The seam a sim step reads this frame's pilot intent through: `Read(dt)` returns one `FlightInput`.
`PilotInputSource`/`KeyboardInputSource` are thin wrappers calling the matching `FlightController`
method (kept `internal` rather than `private` so the adapters can reach them); `ScriptedInputSource`
is the third arm and carries its own state instead — the segment list and its own elapsed-time
clock — so a suite can construct one directly with no `FlightController` in the process. Its
`Reset()` is what a respawn calls to restart the sequence from the first segment.
`FlightController.Bind` resolves which one flies a given aircraft off whichever of the private
`_holdSegments` field (assigned from `FlightControllerBuild.HoldSegments`, the caller's scripted
profile) or `Pilot` is set, and stores it, because that choice cannot change afterward (both arrive
only through the build DTO, before the first sim step). A private `InputSource` property lazily
resolves and caches the same way for a bare test rig that never binds — no suite mutates either
field once a rig is stepping, so this is never stale. Ground-blow probing and the AI ground-blow
write stay on `FlightController` after `Read` returns: they need the live world, which a source
does not have.

## src/Flight/FlightHud.cs
Everything one pane draws for its pilot, in one module the flight node holds privately: the heading
tape (`CompassTape`), the cockpit dials and their two weapon gauges (`GaugeCluster`), the gun pipper
(`ImpactReticle`), the selected-weapon text readout (`WeaponReadout`), the stunt objective marker
(`MarkerHud`), the targeting HUD (`TargetHud`), the `--hud-font-test` overlay (`HudFontTest`) and
the flight text block. Nothing outside this class writes one of them. The text block's auto-land
line is a placeholder pending the `langui` string extraction, marked as such where it is declared.
The per-frame entry is `Draw(in FlightHudState)`, a struct passed by `in` and never a class: this
runs once per rendered frame per aircraft, so it allocates nothing. The struct carries aircraft
STATE, not readout values: `Crashed`, `Halted`, `Held`, `StallWarned` and the rest arrive raw, and
the composing into text, dial positions and gates happens here, which is what makes the mapping
assertable with no Godot `Control` in the process. Three query properties exist so the caller can
skip work it would only throw away: `DrawsReticle` (a muzzle midpoint costs one world transform per
barrel), `DrawsTextBlock` (the damage ledger's `Summary` walks and joins every hurt zone) and
`NeedsStuntStatusLine` (the marker HUD normally carries that line instead). Each guards a real
per-frame cost on aircraft that draw nothing, AI rigs above all.
`Attach(canvas, versusHud, scoreboard)` builds the text block and parents every readout in the
shipped draw order; `VersusHud` and `StuntScoreboard` are still the flight node's, and are threaded
through because their z-order slots sit INSIDE this order rather than after it. `SetVisible` is
the hide-everything a cutscene's chrome-off and photo mode both take (`BL-429`, forwarded from
`FlightController.SetPilotHudVisible` because `GameSession` drives it per rig; the compass tape goes
with the rest); `SetInstrumentsVisible` is the narrower one `--debug-spectate`
wants, which keeps the marker HUD deliberately. `StepAgl` is the altimeter's LOW ALT feed, one ray
per physics frame through the same `IWorldQuery` seam every other aircraft query uses, and
`AglMeters` reads it back for the flight telemetry line. `Flash` raises the impact line and `Reset`
is what a respawn calls. The damage flash counts down on WALL time, so a halted session does not
burn it off while nothing is drawn.
The state-to-readout mapping itself is decoded into static, Control-free pieces `CSVM.Tests`
(`FlightHudMappingTests`) drives directly: `ComputeStallWarning`, `MphFromSpeedMps`,
`FeetFromWorldY` and `ComputeAgl` are pure functions of the struct (or a synthetic `IWorldQuery`);
`DrawnText` (internal, test-only) reads the built `Label` back, so an in-engine suite can assert
what a realtime frame actually put on screen rather than only what `ComposeTextLines` returned.
`ComputeGunGauge`/`ComputeMissileGauge` take bare `GunGroup`/`Hardpoint` lists (no bound `Loadout`
needed) and return a `GunGaugeReadout`/`MissileGaugeReadout`, filling the reused belt-fraction list
by pylon NUMBER, not list position; `AdvanceDamageFlash` is the wall-time countdown, gated off while
halted or crashed, independent of whether a text block exists to show it; and `ComposeTextLines`
returns the text block's lines as a list rather than one concatenated string. `Draw` and the three
`Update*` helpers stay the thin writers pushing those return values onto the seven Controls.

## src/Flight/FlightController.cs
The flying-aircraft node: input → FlightModel → transform (or, for an AI pilot publishing a
`RailPose`, the danger-zone ribbon's pose in place of the model step, the sweep still run), weapon fire as
`FireControl`'s engine adapter. `Group` is the roster cohort (the `aiv` block's `group`, the
original's `+0x388`), null outside a campaign roster spawn until a 967 capture swap stamps the
captured aircraft's on the human rig; `CampaignDirector`'s `DEDG` walk reads it. Weapon fire as
`FireControl`'s engine adapter (polls the held triggers, `Step`s the machine each sim tick,
performs the `FireOutcome`: muzzle-transform spawns, gun-loop start/stop, dry cues, breadcrumb
logs), crash and respawn. The pilot HUD is `FlightHud`'s (see that entry): this node holds the
module privately, feeds it one `FlightHudState` per rendered frame, and forwards photo mode's
`SetPilotHudVisible` because `GameSession` calls it; the seven readouts are no longer fields here.
`VersusHud` and `Scoreboard` stay board-adjacent fields on this node.
The camera is `CameraController`'s — this node only feeds it
the pose, the dt and the mixed orbit axes (`OrbitInput`), plus the two view-selection keys
(`PollViewModeKeys`: F8 cycles Cockpit → Nose → Chase, F6 selects Chase, both edge-detected on their own
slots like the targeting keys). `PinnedViewMode` seeds the selection from `--view=`; `ViewMode` and
`FirstPersonView` read it back live, and the session polls the latter for the anim data's
`PLAYER_1ST_PERSON` condition. `Cockpit` (a `CockpitVisibility`, null on any rig built without an
interior) is applied in the same block, keyed to whether the pose THIS frame was a first-person
one rather than to the selection, so a look-behind restores the aircraft while it is down.
`SetViewedFromOutside` is that block's stand-in for a caller that owns the camera and therefore
silences it, the cutscene presentation being the one (`src/Session/CutsceneController.cs`).
Head-look input is read here too and nowhere else (`HeadLookRead`, `SnapLookInput`, `FreeLookRead`,
`MouseLookDelta`), for the same reason `OrbitInput` is: `CameraController` never learns about pads,
mice or key layouts. `SnapLookInput` reads `Kp1`–`Kp9` and `Kp5` recenters, the original's own
bindings; they are only reachable in a first-person mode, where `ActiveView` holds no fixed view.
Head-look is gathered and stepped only inside the first-person arm, on the SIM dt, so a look-behind
freezes the head where it was and releasing resumes it. The mouse is polled like
every other control (a screen-position delta while the right button is held, `UseKeyboard`-gated as
player 1's) rather than event-driven, which the fixed pan rate makes safe: only the DIRECTION of
the motion is read, so a stale delta on the frame first person is entered is worth one frame of
2 rad/s and nothing more. `Head.IdleAim` is wired here too, once, in `Setup` (C22): the private
`AutoheadTarget` reads `_model.Attitude`/`VelocityDir`/`Speed`/`Stats` and gates on `ViewMode ==
Cockpit` plus the `headLook.autohead` `Config` toggle, so `HeadLook` itself never learns about the
flight model or the mode.
On a crash it cuts to `CrashView` once,
writes nothing to the camera until respawn, and hides the HUD layer (the original's crash camera
shows no HUD — footage), restoring it on respawn. Every physics query — the PlaneCollider boxes'
sweep on every other sim step (`SweepCadence`, the original's parity, the skipped step's motion
carried into the next sweep) plus every ray (ground AGL, the ground-blow probe, the camera's height
check, both turret/AI lines of sight) — goes through the one `IWorldQuery` bound in `Bind`
(`GodotWorldQuery`, the sole adapter over `DirectSpaceState`); mask world+aircraft with its own
`Body` (`AircraftBody`, built in `_Ready` from the same boxes) excluded by RID, so another plane
is solid and a mid-air resolves through the same contact rules as terrain; `Crash`/`Respawn`
toggle the body's hittability. Contact detection is three fillers of one `ContactReport` (see that
entry): `SweepAirframe` from the hull sweep on a human rig, `SweepProbes` on an AI rig (the def's
`PlaneStats.CollisionProbes` carried along the motion as rays, the earliest strike winning, which
is the original's contact test and resolves to ONE origin point on every AI def, so an AI's wings
clip through a slot a hull cannot pass, the CM13 racers' `dzpath2` arch among them), and
`CenterRayContact` from the anti-tunnelling centre ray when neither reached the obstacle. Deciding what that contact does is
`AircraftContactResolver`'s (see that entry); this node builds the `ContactConditions`, hands over
a `ContactEffects` for the applying, and performs the `ContactOutcome` (the struck rig's share and
both grace windows, the HUD flash, the un-embed push, `Crash` on a fatal fate). Arming the struck
rig's own grace window goes through its `ArmCollisionGrace()`, a narrow public method, rather than
a direct write to the other instance's private field.
Which state the aircraft is in, and what moves it between states, is `AircraftLifecycle`'s (see that
entry): this node holds one privately, forwards `Crashed`/`Destroyed`/`WreckFalling`/`Inert`/`InPlay`
onto it, and performs what a transition reports rather than deciding it. `StageAt` is the one
exception to `Inert` meaning undrawn: an intro cutscene writes the pose the animation runtime put
its `player` marker in and draws the model there, then hands back the pose the aircraft held when
the staging began, or the one `ResumeAt` re-placed it at (`Session/CutsceneController.cs`).
`BindCrashRig` takes the
crash runtime, the def table, the anchor and the two respawn snapshots in one call, so the rig
cannot be half-bound and only `CrashRuntime`/`CrashAnchor` stay readable as properties.
Those two and `CrashDefs` also FORCE a rig that is armed but not built: `ArmPendingCrashRig` hands
this aeroplane the rest of a stepped build (`Session/CrashRigQueue.cs`) and `EnsureCrashRig` runs it,
which is what `TakeProjectileHit`, `TakeCollisionHit` and `Crash` call at their head so nothing is
shot at, flown into the ground or destroyed while its rig is out of reach. ⚠ Respawn reads the
backing field instead: a still-armed rig has played nothing, so there is nothing there to undo, and
asking would build the whole rig on the frame an aeroplane is placed. The DEATH family (`CRASH into`, `midair aspect`, every
`vehicle health exhausted`, `graze`, `embedded in terrain`, `AI ram`, `impact`) routes through
`Log.Info("flight", …)`, so a play session's file sink carries how each aircraft died; the
per-round weapon breadcrumbs around them are a different family and still `GD.Print`. The
once-a-sim-second `telemetry` line is `Log.Debug("flight", …)` behind a `Log.ConsoleShows` ask
taken BEFORE its values are formatted, since every live aircraft crosses that boundary on the same
sim step and an unasked line is one write per aircraft inside one physics tick; `--log=flight`
turns it on (verification.md PERF-23).
The sim half is `SimStep(dt)`, called by
`_PhysicsProcess` (realtime clock) or by `GameSession` (fixed/halted clock). `SimStep` also ticks
`Turrets` (the carried gunners) after the fire outcome, so the crash branch's early
return silences them; `WorldBlocksLine` is their world-only line-of-sight ray. Which stick flies a
given aircraft is one `IFlightInputSource` (see `src/Flight/IFlightInputSource.cs`), resolved once
in `Bind` and read through `InputSource.Read(dt)` in place of the old per-frame ternary. An AI
aircraft is this SAME node with `Pilot` (an `AiPilot`) driving that source, `IsHumanPiloted`
false, `Setup(null)` for the camera (every camera write skipped, `_cam` null) and no HUD canvas
built — flight, collision, weapons and damage are byte-for-byte the player's path. The one thing an
AI rig is told that a human rig is not is `HumanPositions`, the session's nearest-human snapshot,
which this node turns into a horizontal squared range every step (the wreck fall included, since the
original re-tests it whatever is flying the aircraft) and hands the plant as
`FlightInput.NearestHumanDistSqM` for its far-field branch.
`TakeProjectileHit` and the contact's ledger spend run the decoded damage flow (PlaneDamage: dead-zone redirect +
whole-pool overflow, 2026-08-14) — the struck zone is Apply's ANSWER, not the geometric guess,
and `IsDestroyed` is tested on every hit, zone-less included; the HUD DMG line leads with the
hull pair. The two disabling entries a hit path calls on a struck aircraft sit beside them:
`TryStunPilot(seconds)` (never a human) and `TryChokeEngine(seconds)`, the choker's, which does apply
to a human because the original's `TANGLER` branch has no player guard — the engine is a mechanical
system and nobody, AI included, is told about it. Both refuse an out-of-play airframe, and `Respawn`
clears both. `EngineDeadRemainingS` reads the timer back out of the model. Collaborators:
FlightModel, CameraController + CamParams, SpeedCue, Loadout + ProjectilePool (guns/rockets),
`CollideDamageSink` →
`AnimRuntime.CollideDamageAt` (fly-through facades), CrashRuntime, every HUD widget and animator.
Pause (`BL-373`): `AllowPause` gates whether THIS rig's P / Esc / gamepad-Start reads at all (true
for every human rig, false for AI rigs and the suites' bare test rigs); the edge-detected press
then goes through `PauseState` — every human rig in a session shares the SAME instance
(`GameSession` assigns it, the way `Match` is), so any player can pause but `PauseState.TryToggle`
only lets `OwnerPlayerIndex` resume it. Esc joins the toggle rather than leaving the flight: a
board menu's Exit item is what leaves, so a pad can reach it. `_Process` mirrors `PauseState.Halted`
into the session's `GameClock.Halted` every frame (a no-op write once every rig agrees), which is
the one field every other halt-aware consumer (animation, puffers, the projectile pool, `.`'s
single-step) already reads. Reading the COMBINED value is what lets a results board halt the world
without this mirror fighting it back to running on the next frame — `PauseState` decides who may
flip it, not how a halt behaves once flipped. A rig built with no `PauseState` (the suites) falls
back to the unconditional toggle, unreachable there since `AllowPause` is false on every
such rig. `PauseBoard` is the shared pause board.

`PollResultsShortcuts` reads R / pad Y while a results board is up, from `_Process` on wall time
rather than from the sim step. The board halts the clock, so the sim step no longer runs to read
them, and the hold harness's automatic rematch would have stopped with them. The sim step keeps
only the structural halves of those branches: the early returns, and the `_simPrev = _simCurr`
hold that leaves no stale pair to interpolate at the finish pose.
`Crash` reads the struck body's numeric surface id (`SceneBuilder.SurfaceIdMeta`) and hands it to
the lifecycle, which indexes `CrashDefs` (`SurfaceDefTable`) with it, the original's own cascade: `dirt`(13) plays
`player_crash_dirt` + `snd_exp_ground_a`, `water`(1) `player_crash_water` + `snd_exp_water_a`, and
everything else — id 0 plus the ids whose def this install does not ship — falls back to slot 0,
`player_crash_default`, which authors no surface boom of its own. `--crash` has no struck body, so
it takes the null-material arm to that same slot 0. On an AI plane `CrashDefs` is the `ai_crash_*`
vector instead (M4 G21 — `BuildFlightCrashRuntime` keys the family on `IsHumanPiloted`; same
cascade, same trio per chapter, and its defs hide the wreck rather than flinging pieces);
`LastCrashDef` records the selection and the `CRASH … def=` line prints it, which is how the
`ai-crash-defs` suite and a headless run read which family fired.
**Death is two-stage, and the two stages are two methods.** `Destroy` is stage one: whole-vehicle
health at zero starts `DestroyDef`, the airframe's own self-named def (`fury-fury`, `player-player`),
leaves the wreck VISIBLE, cuts the camera and raises `Downed`. `Crash` is stage two, the ground
contact — a live aircraft flown into terrain, or that wreck landing. Which system carries the wreck
down is asked of the data (`EffectCatalogue.FliesOwnHull`): the eleven airframe defs author an
`ObjectMotion` on `MAIN_ROOT_NODE` with their own bounce landing and own the fall outright, so no
`*_crash_*` follows; `player` authors none, so the lifecycle's `WreckFalling` keeps the hull in the flight model
(no input, no weapons) until `StepWreckFall`'s sweep reaches the world and `Crash` plays
`player_crash_*`. That second call is the one re-entry `Crash` allows while `_crashed`, and it
re-fires neither `Downed` nor the camera cut. The `ai-wreck-fall` suite drives both stages on one
spawned aircraft end to end and is where a regression in either shows up. `player_crash_default` is the cascade's **fallback
arm**, reached by a null material, an out-of-range id or an empty slot — it is NOT an air/no-impact
variant, a reading `BL-059` disproved. `this+0x19f ∈ {0,4}` inside `FUN_004b82d0` is undecoded and
stays unmodelled.
It also fires `Audio.OnEngineStop` (the wind-down cue, layered over the explosion) and plays
`stopprops` on `CrashRuntime` — the one call site every engine-death path shares, whether the
collision resolver called it for a full-speed impact, for whole-vehicle health exhausting on a
survivable-speed graze (a `ContactFate.Crash` outcome), or for a projectile kill
(`TakeProjectileHit`: the pool-resolved hit — part-mapped armor-first damage plus the graze's
feedback triple, no cooldown since rounds are discrete; its `damageScale` is the blast falloff
share for a splash hit, 1 for a direct round). The weapon/graze kill test is
`PlaneDamage.IsDestroyed` — whole-vehicle health at zero, the decoded rule (D14 retired the
old any-critical-part kill; the `critical` flag stays parsed, nothing consults it). An AI
pilot's trigger and lead are its `AiPilot.Gunner`: `SimStep` drives the gunner before
the fire step (`DriveAiGunner` — standing target kept while live, else re-acquired through
`SelectRankedTarget`, D12/D36's decoded ranking over aircraft, turrets and structures (`BL-363`)
with the same team gate: primary_target outranks — by NAME the first match, by the `player` token
the human NEAREST this attacker so a wave spreads over the panes instead of converging on P1
(BL-367), both aircraft-only — ranking otherwise, activation-cutoff candidates never picked, a
non-aircraft winner routed to `AiGunner.GroundTarget` rather than `Target` so `AiPilot` never sees
it, first acquisition logged with its rank inputs; with a
mode machine a standing target is kept in every mode but only pursue/lay off solve and shoot),
the fire inputs read `WantsFire` instead of the raw controls, and `AssistedGunDirection`'s
non-human arm fires the gunner's per-shot dead-eye scatter — an AI plane NEVER ticks or reads
the aim-assist slots (pinned by the `ai-gunnery` suite's A/B). `TakeProjectileHit` also rolls
the D11 damage reaction for AI planes (`AiModeMachine.NotifyDamage` — the steady-hand test),
and `NextPilotInput` lazily wires `WorldBlocksLine` as the machine's terrain probe. Every `Crash` raises `Downed` exactly
once — (victim `PlayerIndex`, killer: the killing round's shooter id; null for terrain, mid-air,
an unowned `NoShooter` round and every other cause) — a fact report the session scores in `--vs`;
flight holds no match state, and `Respawn` emits nothing. `AutoRespawnAfter` (session-armed —
Versus sets 3 s on every rig) auto-respawns a crash on the sim clock with R still skipping early;
null, the default, keeps every other mode manual-R (scripted hold runs keep their 1.5 s).
In a splitscreen stunt race, a finished pilot continues normal flight, collision, weapons and
crash/respawn while `StuntMission` holds their timer/objectives and `MarkerHud` holds their placing;
this prevents their finish pose from obstructing another pilot's gate.
`Respawn` plays `startprops` back and resets
`ThrottleSmoke`, which `Update` otherwise drives every frame off the live throttle.
A survivable graze also REBOUNDS along the contact normal (retiring `BL-172`): the decoded
`bounce_factor` impulse (`FlightModel.BounceNormalSpeed`) replaces the normal component the
tangential slide strips out, computed before the attitude kick so it reads the rates the contact was
entered with. Player-only, as the original is; an AI aircraft gets the position correction and nothing else. The `graze-bounce`
suite flies both into the same floor (e = 0.56 against 0.00) and a player rig along a vertical face,
where the same impulse fires horizontally and leaves the altimeter alone — there is no surface test
anywhere in it, and `CAP-14`'s flat-versus-vertical split must not be implemented as one.
A survivable graze plays touchdown.zrd's per-surface reaction (`GrazeReaction`) through the SAME
cascade: the struck body's surface id indexes `TouchdownDefs` (the session's one `SurfaceDefTable`,
built by `WorldEffectsFactory` against the world program because the original's touchdown vector is
a level-init global). `dirt`(13) raises dust, `water`(1) splashes, and everything else falls back to
slot 0, so **ordinary terrain and buildings alike spark off `touchdown_default`** — the same
correction B11 made to the crash, and the reverse of what this build did before. The def is staged
at the contact point via `GrazeEffectSink` (the world-effects runtime) with `FlightAudio.OnGraze`
under it, gated on the CHOSEN DEF rather than on a water flag, one per `GrazeReactionInterval`.
Where it stages is an open A/B — `graze.siteAtContact`, default the contact point (judged at the
controls); false stages on the aircraft, which is what the def's `MAIN_ROOT_NODE` offsets assume.
`AttachWarningShotCue` registers the aircraft on the pool as a near-miss target and `OnNearMiss`
rates the passes through `WarningShotCue` into `FlightAudio.OnWarningShot`; `PlayerIndex` is both
the pane seat and the identity every round this pilot fires carries, so it must be set before the
registration (the assembler sets it at construction, not in the stunt block). `Team` is this
aircraft's side for every hostility test — the aim-assist candidate scan, `SelectRankedTarget`,
carried `TurretController`s and `AiVoiceDispatcher` registration all read it, none re-derives one
from `PlayerIndex` — and defaults to `AimAssist.TeamOfPilot(PlayerIndex)` until a mission sets it
explicitly, so free flight and `--vs` are unchanged; plain flight's `--coop` is the one other
explicit setter, `HumanFlightAdapter` giving it `AimAssist.PlayerTeam` the same way Instant Action
does. `Inert` is this aircraft's other lifecycle state: BUILT but held
completely out of the session — not stepped (`SimStep` and `_Process` return at once), not drawn,
not on the aircraft collision layer, not hittable, not a targeting candidate, and not counted as
living. `InPlay` (`!Crashed && !Inert`) is the one "is it there" question every roster asks; a
consumer that still tests `Crashed` alone silently sees inert aircraft. Only two things are pushed
as state, by the private `ApplyPresence()` — `ShakePivot.Visible` and `Body.SetHittable` — and it
runs from `Respawn` AND `_Ready`, because `Setup` calls `Respawn` before `_Ready` has built the
body. **⚠ The pivot, never the model root**: that root is the airframe's own aircraft-archive node
and its `Visible` is the ACTIVE bit a hookup definition reads to decide which aeroplane it is posing
(`docs/formats/anim-definitions/cutscenes.md`), while a cutscene holds the aircraft `Inert`
throughout. The two crash paths still hide the model itself, which is a death state rather than
presence, and coming back into play undoes it. Everything else consults the flag: `TakeProjectileHit`/`DebugForceCrash` refuse,
`DriveAiGunner` drops a standing target that leaves play, and outside this class
`ProjectilePool.CollectAircraft` (which carries the aim assist, `SelectRankedTarget` and the
hostile tracker with it), the pool's fuse/blast passes, `TurretController.Alive`, `AiPilot.Next`'s quarry
test and `TargetHud`'s hostile draw all read `InPlay`. `InertChanged` is the event a session-level
roster mirrors it into (`AiVoiceRuntime` → `Speaker.Alive`). `Activate(pos, lookAt)` is the
inverse — re-home, clear the flag, `Respawn` — the original's teleport-then-reactivate in one call.
`Spectating` is the third lifecycle flag and the narrowest: a pilot out
of lives stays crashed for the rest of the mission. Checked at the TOP of the crash branch, ahead
of both respawn triggers, so neither R nor the armed `AutoRespawnAfter` timer can fly it again —
clearing the timer instead would leave R working. The session sets it from its own lives ledger and
hands the pane to a `SpectatorCamera` through `CameraOwned`; this node holds no mission state and
decides no rule, exactly as it holds none for `--vs`.
`Held` (the weapon lab) pins this ONE airframe while the session runs on: the sim step skips input,
`FlightModel.Step` and the whole collision sweep and re-applies the pinned pose through
`FlightModel.Reset(pos, attitude, 0, 0)` instead — everything from the pose commit down (weapon
selectors, guns, rockets, ordnance, gauges, telemetry) runs exactly as in free flight, which is what
makes the lab fire through the real path. `PlaceHeld(pos, lookAt)` moves the pin (C6/C7's re-park)
through the same `Reset` + `SnapCamera` pair `Respawn` uses; `ReleaseHeld(velocity, throttle)` is
the scripted-path follower's handoff, un-holding in place at that velocity and lever with the net
reseated and NO respawn or spawn grace (the original clears the path flag and nothing else, so the
sweep and ground blow run from the first flown step); `SelectGunGroup`/`SelectPylon` are the
programmatic twins of G/H for the lab panel. A held airframe also takes the ORBIT camera rather than
the chase — `halted || Held`, since both mean "the plane is standing still and the view should swing
around it" — and `CameraOwned` makes this node write nothing to the camera at all while the lab
hands the same `Camera3D` to a `SpectatorCamera`; clearing it re-seeds the orbit from wherever the
free camera left the eye.
`SelectedGun`/`MuzzleMidpoint` are the shared "which gun is selected, and where does its fire leave
from" answer, read by the pipper and by `GunReachesTarget`, the targeting marker's bracket gate
(see `TargetHud.cs`). `Stats` exposes the flight model's own `PlaneStats`, needed for the airframe's
display name; it is null on a rig `Setup` has not run on.
`StepTargeting` is the player-targeting frame, run for every human pane whose `Targeting` is set:
rebuild the pool and re-resolve, prune the attacker queue, then dispatch input. `TargetSubParts` is
the delegate that adds the zeppelin sub-parts, bound by `GameSession` rather than the assembler.
D-pad Up runs through `TapHoldButton`; `T`/`Y`/`U`/`I`/`O` are the curated keyboard set.
`ApplyInitialTarget` spends `--target=`'s one application (`cli.md`), and `TakeProjectileHit` feeds
the attacker queue `Next Enemy/Objective` walks backwards. Decode:
[`org/targeting.md`](org/targeting.md).

`ApplyFireOutcome` is where the gun aim assist meets the world (`BL-342`/B5): it rebuilds
`AimAssist`'s candidate set ONCE per tick (aircraft + fused ordnance off the pool, the world's
destructibles off `Destructibles` when a world runtime wired one), then `AssistedGunDirection`
runs each firing barrel's slot through `AimAssist.FireDirection` and hands the result to
`ProjectilePool.Spawn`. The rocket call deliberately gets no assist (the original reaches it from
the gun branch alone) but it does get a launch direction, from `OrdnanceLaunchDir`, and the two
shooters differ there: a human's round leaves along the AIRCRAFT's own basis axis, negated (as-is
for a `REAR` weapon), with the pylon marker giving the spawn position alone, while an AI's leaves
along the clamped mount aim `AiRocketeer` wrote. That asymmetry is the original's
([`org/ordnanceTypes.md`](org/ordnanceTypes.md), "Who aims ordnance, and who does not"); no shipped
airframe cants a pylon marker, so the human rule is a guard on this data rather than a visible
change. A pylon whose weapon carries `SmokeScreenTime` spawns nothing at all: it calls
`SmokeScreens.Lay` instead, so no round, no `FIRE` sound and no `FIRE` animation, while ammo, the
fire clock and the rate limit run as for any other pylon weapon. What the rocket call also passes is
the shooter's current target, a human's own `Targeting.Current` selection or an AI's `Gunner.Target`
quarry: it is the second half of `ProjectilePool.SteeringStepRuns`'s gate, so it decides whether a
`LOCK_ON` round is steered. The original's own shot routine hands a `LOCK_ON` weapon a synthetic
target when the player holds none; CSVM does not, so a round fired with nothing selected is not
turned, though it still sheds its inherited launch velocity (the divergence recorded on
`Projectile.cs`). A `BEEPER_SEEKER` round drops whatever it was handed here on its first frame and
steers only at the tag list's pick. The `ordnance-launch-axis` suite pins the launch halves.
Two one-shot breadcrumbs on the first gun round make the wiring visible in any flight log: the
candidate counts per list **with the nearest structure's range** (a registry whose anchors carried
no world transform would report its full count from the world origin — untargetable, and the count
alone would not show it), and the first snap with its kind, score and range. Measured in C1 flying
at the airfield: `vehicles=1 turrets=0 (M4) structures=210 ordnance=0, nearest structure 306 m`,
then `P1 snapped onto Structure (score 1.000, 288 m out)`.

## src/Flight/PlaneDamage.cs
The decoded vehicle damage ledger (org/vehicleDamage.md, corrected 2026-08-14): per-part pools
from destroyable_parts PLUS a real whole-vehicle (armor, health) pair — authored where the def
chain carries one (AI defs), else the sum over parts (player defs; player_bhawk 80/80, Fury
90/90). Apply is the decoded take-hit flow ported instruction-for-instruction: the named zone
spends armor-first (a dead/unknown zone REDIRECTS to a random surviving zone — the resolver
rule), the whole pair recomputes as parts' fraction × whole maxima after every part spend, then
the unabsorbed leftover re-enters zone-less and drains the whole pair directly. IsDestroyed =
whole health ≤ 0 (reachable with zones still healthy — the overflow kill). Summary leads with
the hull pair for the HUD DMG line; SummaryHealthFraction reads the whole pool (DI voice);
WorstFraction stays the worst PART on the combined pool (the injure_anims scale);
WorstHealthFraction is the decoded damage-state reading (worst zone on health alone, the hull
pair where an airframe resolves no zones) and is what the engine-audio swap gates on. `ScalePools`
and `SetWholePools` are the airframe swap's two writers, the only ones outside the take-hit flow:
one scales every zone by a fraction and recomputes the whole pair, the other writes that pair
directly. The stock
armor/HP doubling and the kill rule are decoded in
docs/org/vehicleDamage.md and docs/formats/vehicle.md.

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's HEALTH-ONLY fraction (armour is in
neither pool — docs/org/vehicleDamage.md "Damage staging") crosses an
injure_anims entry it shows the torn pdpN panel, hides the healthy skin, and plays the entry's
AUTHORED anim through `DamageEffectSink` — the player's own rig runtime (`BL-259`): `pdpanelN`
(gimmeflakes debris + the staged short_firetrail/loop_short_firetrail burn-down at the panel),
`player_fuelleak` (0.85 — gunhit flash + fuel vapor at a random pdp1–3, its stream authored-gated
on that panel being ACTIVE, so it renders only once torn), the `<part>_damage_effects` spark shims
(0.99, `player_pfighter` data alone — B4; their general home is the weapons.json `player` IMPACT
surface, live since M4 A2+D14 fielded AI shooters), and, for the data's 0.10 `player_smoketrail`, `player_damage_trail`
(short_firetrail at prop1 + the fire_lt light) — `RigAnimFor` owns that one mapping.
`DamageEffectStop` (Reset, first) stops the whole stage CLOSURE, derived from the program — a
stopped pdpanelN cannot reach the trail it CALLed, and prop1's trail has no authored exit;
`DamageEffectStopOne` is the single-stage form a retraction uses. Its lines route through `Log`
(`flight` for the panel and smoke-trail state, `anim` for the stage routing), not a bare `GD.Print`,
so a play session's own file sink records which stages fired. Staging is keyed per LADDER
ENTRY and cleared on the upward crossing alone, so a repair un-stages and the entry can fire again
(`StagedEntryCount`). Panel pairing (def-derived candidate sets, positional assignment, the three
crossed-naming outliers) is decoded on `PairHealthySkins` and runs only for an airframe whose own
data names a `pdpanel*` stage (`PairsPanels`); the null-sink stand-in fallback is on
`UpdateStatic`/`PlayStage`. An AI ladder's own two anims (`pfsmoketrail`,
`random_remote_damage`) play through the same sink: `RigAnimFor` is a membership test over
`EffectCatalogue.DamageStageAnims` + `PlaneDamageEffectAnims`, curated rather than
program-existence, so the cockpit gauge defs (`*_damage_green/yellow/red`, `*_got_hit`) can never
play on an airframe.

**The cockpit-interior twins pcdp4/pcdp6.** `PlaneBuilder.CockpitDamagePanels`
joins `DamagePanels` in the same `_panels` table (an optional constructor param, empty outside a
cockpit-interior build), so `ApplyPartStage`/`Retract` flip `pcdp4`/`pcdp6` alongside `pdp4`/`pdp6`
off the identical `pdpanel4`/`pdpanel6` entries — no separate cockpit rule, and `Reset()` clears
both together for free (neither carries the `_h` suffix that keeps a healthy skin visible). The
pair has no healthy twin of its own to pair (no `pcdp4_h`/`pcdp6_h` ships anywhere in `planes.zbd`),
so the crossed-numbering trap that pairs `pdpN`↔`pdpN_h` by mesh position does not extend to them —
there is nothing to pair. `CockpitVisibility.Apply` (B11) only ever toggles the four top-level
groups it binds, never a panel's own `Visible`, so a torn cockpit panel stays torn across a
Cockpit↔Nose↔external switch with no extra code.

## src/Flight/DamageLab.cs
The damage lab (F5 toggles): one armor slider (parts the data gives an armor pool) plus one health
slider per destroyable part — `PartFrac` (Health, Armor, Combined) is what an `IDamageLabTarget`
reads/writes. `DamageVisuals` itself is driven off the HEALTH fraction alone; `Combined` is
`DamageLab`'s own derived one-number reading, the scale the mirrored GaugeCluster damage dial is
on (see the class's own doc for both). `ReadSliders` floors a part's armor at 0 whenever its
health reads below 1, mirroring `PlaneDamage.Apply`'s armor-first real path (armor absorbs a round
in full before any of it reaches health, so no reachable state has health short of max with armor
still standing) — armor alone can still be driven to 0 with health untouched, just not the
reverse. Presets (--damage=part:frac) set both sliders to the same raw fraction through the same
ValueChanged path as a hand drag, so a fraction below 1 floors armor there too. One panel, two
hosts, chosen by the injected IDamageLabTarget (same file): ViewerDamageTarget drives
DamageVisuals on a parked plane (it holds no model), FlightDamageTarget writes P1's real
PlaneDamage — each pool through its own single-pool `PlaneDamage.Apply(part, healthDamage,
armorDamage)` call after `Reset`, using the fractions `ReadSliders` already floored. Neither
target reimplements visuals, only decides when to rebuild them. The `--damage=` flag's own
open-at-launch and splitscreen (F51, `BL-376`, P1-only) behaviour is the description of record in
[`cli.md`](cli.md).

## src/Flight/CompassTape.cs
The original's top-centre heading tape rebuilt from the game's own compassticks2/compasstxt
textures: a cylindrical drum seen edge-on — DrumX = center − R·sin(Δ), headings increase LEFT,
cos(Δ) fade (rendering model: docs/formats/hud.md). Metrics are probe-fitted Ref* constants ×
HudMetrics.Scale; Build returns null if a texture is missing; _Process re-anchors on resize.

## src/Flight/GaugeCluster.cs
The original's cockpit dials as a screen-space HUD: altimeter, speedometer, damage display, plus
the gun + missile weapon gauges and the nitro dial (`nitrogauge`, drawn only with the injector
installed; its two needles chase the decoded targets through `NitroNeedle`'s exponential at 3/s
and 1.5/s over a 216° sweep, negated at the draw site because the decoded angles are
counter-clockwise-positive; it sits at the bottom of the right column, below the speedometer, and
is the one dial whose bezel centre and radius are read from the tree rather than assumed), all
geometry extracted from the plane's own gauges subtree
(structure/scales/quirks: docs/formats/hud.md); polys draw by data priority, rest rotations
ignored; PartFraction binds flight or the lab; dial centres are bottom-anchored (FromBottom) so
panes keep them on screen. `HeadingDeg` carries the nose heading `CompassTape` also reads (`BL-663`);
the cluster draws no screen-space compass itself, but `CockpitGauges` reads the same field to turn
the authored 3D panel's compass drum, so the tape and the drum never compute the heading twice. `DamageZoneColor(frac, yellowAt, orangeAt, redAt)` (`BL-085`/`BL-173`) is
the damage-dial band function — `frac` is `PartFraction`'s COMBINED armor+health value (both bound
sources, flight and the lab, feed that scale; nothing here computes it),
`yellowAt`/`orangeAt`/`redAt` are mined per-part from the data's own `*_damage_green/yellow/red`
injure_anims. Both `Border` and `Fill` always take the same colour index — `BL-173`'s refuted fix
shape was a synthetic per-pool ring split; there is only ever one colour per zone.
`GunIndicatorColor`/`HardpointIndicatorColor`/`SlotIndicatorColor`/
`DamageZoneColor`/`TargetArrowAngle`/`TweenArrow`/`IndicatorLowFrac`/`ArrowSweepDegPerSimS`/
`StallBlinkHalfPeriodS` are `public` (not `internal`) so `CSVM.Tests` (`GaugeColoursTests`,
`GaugeArrowTweenTests`, `StallWarningTests`) can call them from outside the assembly — moved from
the in-engine `gauge-colours`/`gauge-arrow-tween`/`stall-warning` suites. The two animated
cues are plain nested structs, `GaugeCluster.ArrowSweep` (`Angle`/`Advance`/`Reset`) and
`GaugeCluster.StallLamp` (`Lit`/`Advance`/`Set`) — B11 retired the `internal` testability-escape
hatches (`StallLampLit`, `AdvanceStallLamp`, the private
`_gunArrowAngle`/`_missileArrowAngle`/`_stallBlinkPhase`/`_stallDwellS`/`_stallLampOn`/
`_stallWarnPrev` fields) that existed only so the in-engine suite could reach a live `GaugeCluster`;
the structs need no `Control` to construct, so `CSVM.Tests` drives them directly. Scoped to
`GaugeCluster` only (Decision 5) — `FlightController`'s stall/arrow feed predicates are untouched.

## src/Flight/CustomPlaneBuild.cs
The fidelity-bearing join: a saved `CustomPlaneDef` onto the three things a spawn consumes. Pure
and engine-free — every input is handed in, so nothing here looks up an airframe, a store or a
session.

`LoadoutFor(def, stockBase)` builds over the airframe's stock fit, which stays unmutated.
**Guns**: slot n's calibre row c becomes `Caliber = 30 + 10c` with `WeaponId` left null, so
`Loadout.Bind` resolves `wep_{caliber + ammoIndex}` and the Ammo Selection layer still composes on
top (`LoadoutChoice.ApplyTo` over the result). A twin mount is ONE gun over `firepoint(9-2n)` and
`firepoint(10-2n)`, a single takes the low one; the stock slot's own marker list narrows that pair
when the rig is short of it, which is what keeps a twin pick on the Kestrel's slot 1 off the
`firepoint8` it does not have (binding an absent marker is a loud throw). `Turret` and `Mount` come
from the stock slot; an empty pick (the dropdown's id 5) omits the slot entirely rather than
building it with no rounds. **Hardpoints**: the record counts pylons per wing and the stock fit says
what hangs on each. `Loadout.PylonFillOrder` alternates the wings entry by entry, so its two
interleaved halves are the wings (1-4 and 5-8; which is physically left is undecoded, `markers.md`
omits the pylon positions). A wing's count takes that wing's fill-order entries in order and **caps
at the pylons the stock fit authors** — the record names no ordnance of its own, so a fourth pylon
on a Bloodhawk wing has nothing to hang. Unchosen entries keep their place as
`LoadoutChoice.None`, since dropping one would slide every later pylon onto the other wing.

`PaintFor(def, patternName)` is the record's three resolved colours and its three decals under a
pattern name the caller resolves from the 0-13 index (`HangarPaintPage.PatternName`). The decals
are the same 0-49 index space `vehicle.json`'s `paint_decalN` uses, so they carry straight over; a
slot the build never chose keeps the `-1` "leave the shipped placeholder" sentinel, which a fresh
plane has on all three (the pattern defaults carry no decals).

`ArmouredParts` / `DamageFor` put the bought armour on the damage zones. Armour units reach the
pool at `ArmourUnitScale` = 5, the record's own premultiply, and **no other rescaling**: CSVM's
`destroyable_parts` armour pools are the shipped stock allocations (15/20/25/30/35/40,
`docs/formats/vehicle.md`), the same scale the original's hangar writes its raw x5 floats onto. Only
the ARMOUR pool is set; structure (`MaxHp`) stays the def's, exactly as the original leaves the
mission file's structure alone. Parts are **copied**, never overwritten: a `PlaneStats` is cached
and shared by every plane of that airframe. A zone the record does not name is carried across
untouched. The vehicle totals stay derived — no player def authors an `armor`/`health` pair, so
`PlaneDamage` sums the rebuilt zones, which is the original's own recompute-on-every-zone-write.

⚠ **Engine and weight reach nothing.** The engine pick decomposes into a power tier and a nitrous
flag; the tier indexes the same shipped `engines.json` table `PlaneStats.EnginePower` already reads
an airframe's stock row from, so wiring it means moving `FlightModel`'s thrust term rather than
adding anything here, and it is deliberately not part of this join. Total weight and the stat-table
power rating are hangar-only in the original and must never gain a flight dependency
(`docs/org/hangar.md`, "Into the mission").

## src/Flight/PlayerRig.cs
One rendered view's state bag: index, camera, optional `SubViewport`, `HudParent`, `VisualLayer`,
the player's FlightController, and private camera-anchored copies (`Horizon`/`Deck`/`Whiteout`) —
those re-anchor to the view's camera every frame, so N players need N of each. The ambient cloud
field is deliberately not one of them: the authored fogvol clutter is world-anchored static
geometry every pane shares, gated per-view through `Camera.CullMask` instead of a duplicated
subtree.
`CameraWeatherState` (1/2/3) is the same shape as the deck
regime: a per-rig field, not shared, because splitscreen panes can sit in different states at the
same instant. Written each frame by `Session/WeatherRig.Tick`; rig 0's value drives the per-state
fog switch there — the fog globals are session-wide, so only rig 0's is read for them.

## src/Flight/ViewerSet.cs
`ViewerSet` — the "what do the cameras see" seam promoted out of
`ProjectilePool.Viewers`/`ScreenSize.NearestFloor`, the pattern the tracer floor proved 2026-08-10
([`org/tracers.md`](org/tracers.md)). `GameSession` owns one instance (`_viewers`) and `Bind`s it
once, right after `BuildRigs` returns (`StartSession`) — the same rig-camera list every rig loop
reads, single player included (one entry wrapping the main camera). `Cameras` hands back the raw
bound list unfiltered, for a consumer (`ProjectilePool.TracerFloor`) that needs each viewer's own
FOV and pane height alongside its position and already skips a freed instance itself; `Positions`/
`Poses` are the two derived shapes B13 and B11 consume, respectively — position only, or position
plus forward for a view-space depth comparison. `Poses(into)` fills a caller-owned buffer for B11's
consumer, `EffectAmbience`, which republishes the set every frame and would otherwise allocate a
list per frame. `ScreenSize`'s arithmetic did not move: this class carries cameras, not the
screen-size/view-depth math itself.
Consumers today: `ProjectilePool.Viewers` (tracer floor), `WeatherRig.Tick` → `EffectAmbience`
(the puffer distance fade), both handed the session's one instance at construction, and
`UI.ScreenFlash` (B12's wash routing), handed it at `Build`. That last one reads `Cameras` rather
than `Positions` because it needs the index to stay aligned with its own per-pane rects, and
`Positions` skips a freed camera. `Positions()` also feeds `AnimRuntime.LightViewerPositions` (B13's world-light budget,
`WorldLights.Commit`) via `GameSession`'s `() => _viewers.Positions()` closure — the same
resolved-per-call shape `PlayerPosition`/`PlayerPositions`/`ListenerPositions` already use, since a
pane's own camera moves every frame and the runtime is built before any rig's transform is final.

## src/Flight/CollisionLayers.cs
The named physics collision layers — world (layer 1, the engine default every pre-existing
collider sits on implicitly) and aircraft (layer 2, `AircraftBody`) — plus the combined mask.
The first and only place a layer bit is assigned a meaning; new layers go here, never inline.

## src/Flight/AircraftBody.cs
The flying aircraft's physics body: one `AnimatableBody3D` child of `FlightController`, one
`CollisionShape3D` per `PlaneCollider.Part` reusing the SAME `ConvexPolygonShape3D` + local
transform the terrain sweep casts, on the aircraft layer. Rides the controller's transform;
`PartName(shapeIdx)` maps a query's struck shape back to the part (shapes added in `Parts` order);
`ExcludeSelf` is the cached one-entry RID list the owner's own queries pass; `SetHittable` drops it
to layer 0 while the plane is out of play — crashed, or INERT — and back when it is in play again,
both driven from `FlightController.ApplyPresence`. Also the fuse/blast geometry oracle, answering
from the same `ConvexHull` set without a physics query: `NearestShape(point)` (nearest hull, its
skin distance + surface point — blast falloff), `SegmentDistance(from,to)` (closest approach of a
swept round, ternary search per hull — distance to a convex set is convex along the segment),
`BoundRadius` for the cheap per-step reject, and `TakeProjectileHit(..., damageScale)` scaling
both damage magnitudes by the blast falloff share (1 = direct round).

## src/Flight/IWorldQuery.cs
The one seam onto the live physics world: `Sweep` (a shape moved along a motion, earliest stop
across a named part list), `Ray` (one ray), and `Overlaps` (does any named part touch anything at
a standing pose, which is what the un-embed loop asks). `FlightController` reads the world only
through this; nothing else may reach `DirectSpaceState`. `SweepReport`/`RayReport` carry the answer,
including the struck collider as a plain `Node?` so a caller builds its own name. A carried
`TurretController` reads the same seam for its line-of-sight check
(`TurretController.WorldBlocksLine`), built with the `GodotWorldQuery` its `BuildCarried` makes
from the host it is riding; a synthetic `IWorldQuery` proves the mask and the blocked/clear cases
off-engine (`TurretLineOfSightTests`), with no live node in the process.

## src/Flight/ContactReport.cs
One detected contact, as the value both halves of detection fill: the impact, the struck surface's
normal, which airframe box reached it first, the collider's name, how far along the frame's motion
the airframe stopped, and `StruckIsAircraft`. `FlightController.SweepAirframe` fills it from the
`IWorldQuery` sweep; `CenterRayContact` fills the same shape from the anti-tunnelling centre ray,
where there is no struck box and no surface normal, so the part reads `center`, the normal is the
reversed motion (making the contact head-on) and the stop fraction stays 1. The report holds no
`Node` on purpose: the only question the decision side asks about the struck object is whether it
is an aeroplane, and the caller keeps the collider for the applying (the struck rig's damage, the
crash def's surface id, the graze reaction). The grace window is a precondition of detection rather
than a filter on a report: while `_collisionGrace` is live no sweep runs at all.

## src/Flight/ContactOutcome.cs
What one contact costs the striking aircraft, as a value with no `Node` and no physics space behind
it: the fate (`ContactFate.Graze` survivable, `Crash` fatal), the decoded damage pair both parties
spend, the doom rule's answer, the zone the ledger charged (`Apply`'s answer, not the geometric
guess), the pilot HUD's flash line, `PushOut` (how far along the normal the caller must move the
striker to un-embed it, applied on a crash too), `ShakeMagnitude` (the block-5 camera kick, zero on
an AI), and `DamageStruckAircraft`, the instruction a
caller owes because only it holds the struck rig: hand that aeroplane the pair and arm the
collision grace on both parties. One value with no optional parts, so forgetting to perform it is
forgetting one statement rather than four. `AircraftContactResolver` fills it.

## src/Flight/AircraftContactResolver.cs
The decoded contact rules for one aircraft, holding an `IWorldQuery` and no `Node`: the damage pair
both parties spend (`FUN_0048d2c0`), the fate (the doom rule, no damage data, health
exhausted, an airframe that cannot un-embed), and the un-embed loop over the
seam's `Overlaps`. One call answers one contact with one `ContactOutcome` the caller performs.
The engine effects it interleaves with, because each one's result is the next rule's premise, go
through `IContactEffects`: the fly-through offer, the graze reaction, the ledger spend and its
readouts, and the contact response. `FlightController` implements that as a per-contact
`ContactEffects`, keeping the struck `Node`, the sweep cadence and the flight model on the node.
`ContactConditions` is the striker's state per call, `IsHumanPiloted` included. Every contact the
resolver is handed spends the pair; the original's every-other-frame cadence is the sweep's
(`SweepCadence`), never a gate on the spend.
`AircraftContactResolverTests` pins the rule table off-engine against a synthetic `IWorldQuery` and
a scriptable `IContactEffects`: the doom rule for an AI ramming a non-aeroplane, the entity cut for
AI into AI, the player's exemption from both (asserted on the damage magnitude, not just
`DamageStruckAircraft`, since the entity cut applies only on the non-player branch and only against
another aeroplane), a sustained slide exhausting its ledger against a control that spends nothing,
the camera kick every human-piloted contact spends and an AI's spends none, and the un-embed loop's
three-try give-up.

## src/Flight/SweepCadence.cs
The original's alternate-step collision sweep (`FUN_0048d7f0`'s parity gate and the `obj+0x6B0`
accumulator, docs/org/flightModel.md "Collision response") as a pure value with no `Node`:
`Advance` answers whether this sim step sweeps and, after a skipped step, the origin the sweep runs
from, so the carried motion is swept whole; `Respawn` resets the phase. The parity gates the SWEEP,
never the spend: a contact the sweep resolves always spends the pair. `SweepCadenceTests` drives it
with the real resolver and ledger against a kinematic wall, pinning the shallow-then-steeper sequence
from the controls, a sustained scrape dying in three 50-floor spends, and the spend-gated control
that locks onto the free steps and never spends.

## src/Flight/AircraftLifecycle.cs
The states one aircraft moves between and the rules that move it: in play, crashed, destroyed with
its wreck still flying, inert, and back to spawned. It owns those flags plus the collision-grace,
carrier-drop ground-blow and auto-respawn timers, holds the crash-def table and the selection off it
(`LastCrashDef`), and holds no `Node`, so the whole table runs in a unit test. Every transition
REPORTS what happened instead of performing it: `Crash(surfaceId, killer)` answers one `CrashOutcome`
(did it happen, was it the wreck landing, which crash def, whether the shutdown, the camera cut and
the `Downed` report are owed, and the killer to name) and `Destroy(destroyDef, killer)` one
`DestroyOutcome` on the same terms, with `WreckFalling` deciding whether the hull flies itself down.
Each is one value with no optional parts, so a caller that forgets half a crash is forgetting one
statement rather than four. `FlightController` keeps `Crashed`, `Destroyed`, `WreckFalling`, `Inert`
and `InPlay` as forwards onto it, so its fifteen internal readers and every session-side consumer
read the same spellings they always did, and it keeps the `Downed`/`InertChanged`/`DamageApplied`
events, which the session subscribes to. The guard that a crashed aircraft cannot crash again is a
transition rule here: `Crash` refuses while `Crashed`, and the single exception is the falling
wreck's own landing, which reports `WreckLanding` and owes neither the cut nor the report because
the death was reported at the kill. `ArmSpawnTimers(carrierDrop)` opens the spawn's collision-free
window (`CollisionDamage.SpawnGrace`), and the carrier-drop arm adds
`CarrierDropGroundBlow` seconds of the 0.15 ground-blow multiplier on top; `ArmCollisionGrace` is
the shorter window a resolved ram writes to both parties. `SetInert` answers whether the flag moved,
which is what makes the node's presence write and its `InertChanged` raise conditional.

## src/Flight/GodotWorldQuery.cs
The only adapter over Godot's `DirectSpaceState`, implementing `IWorldQuery`. Resolves the wrapped
node's `World3D` at each call rather than caching it, since the node may be bound before it joins
the tree. `Sweep` holds the airframe's whole per-part cast/rest-info dance, including the 0.05 m
nudge past the first overlap (`GetRestInfo` can come back empty exactly at the unsafe fraction).

