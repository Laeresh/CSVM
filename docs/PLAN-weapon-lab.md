# Weapon lab — fire it like the world does

**DRAFTED, NOT YET ACTIVE** (written 2026-08-02). The file sits in `docs/`, but
`PROJECT_CONTEXT.md`'s "Current status" **deliberately does not name it yet** — it is a written-up
plan waiting to be scheduled, not the running one. Point "Current status" at it when the work
starts; move it to `docs/plans/` with a `COMPLETE` banner and add its row to
[`plans.md`](plans/plans.md) when every item lands.

## Context

Today's weapon lab (`CSVM/src/UI/WeaponLab.cs`, key **W** in `--viewer --plane=`) is a bench that
only *approximates* firing. It builds its own `ProjectilePool` with **no world at all**
(`WeaponLab.cs:495` — `new ProjectilePool(_textures, null, null)`), so by construction:

- rockets fly as bare streaks (no `FLYOUT` prototype to instance) and carry no smoke trail,
- every impact draws a hard-coded stand-in sprite burst — none of the authored `*_gunhit`,
  fireball, flak/sonic/AP or splash effect defs can play, because there is no world-effects
  runtime to play them,
- `DamageSink` is null, so nothing is ever hit,
- the target is a translucent 60 m box that merely *claims* a surface class by setting
  `SceneBuilder.SurfaceMeta` on itself (`WeaponLab.cs:536-551`),
- pylons carry no ordnance models (`PylonOrdnance` is `--fly` only),
- and the firing loop is a hand-written copy of `FlightController.UpdateGuns`
  (`WeaponLab.cs:559-569`) that already carries a ⚠ in `docs/architecture.md` warning it must be
  kept in step by hand.

On top of that, the mount list is built from the **stock loadout**
(`WeaponLab.BuildMounts`, `Loadout.Bind`), so testing a firepoint the stock fit does not name
means editing `CSVM/data/stock_loadouts.json` — every plane actually carries `firepoint1…8`
(Kestrel 7) and `pylon1…8` (`docs/formats/markers.md:20-21,52,79`), and the stock fit binds only
a subset.

**Outcome wanted:** a lab where a weapon fires *exactly* as it does in flight, against real world
surfaces, with the authored impact and flyout animations, with the hardpoint models visible under
the wings, and with every firepoint and pylon on the airframe selectable without touching the
stock-loadout file.

## Decisions (2026-08-02)

| # | Question | Decision |
|---|---|---|
| 1 | Where does the lab's stage come from? | **A real chapter world.** The lab becomes a *flight-mode* session, so it inherits colliders, the world gamez/scene/anim program, the world-effects runtime and the destructible registry with zero new stage code. |
| 2 | How does the plane get onto the right target? | **Click to place.** A physics raycast from the active camera through the mouse gives the hit point, its collider and therefore its real surface class; the plane is re-parked *on that camera ray* at the panel's stand-off distance, nose on the clicked point. Scriptable twins (`--weapon-target=`, `--weapon-surface=`) for `--screenshot` runs. |
| 3 | How does it fire? | **Through a real `FlightController`.** The lab swaps weapons into the live `Loadout` and pulls the normal trigger; `WeaponLab`'s own firing loop is deleted. |
| 4 | Which animations are missing? | **Impact/hit effects** and **rocket flyout + trails** — both are consequences of having no world, so both fall out of decision 1. |
| 5 | Camera | **Both, switchable:** orbit the held plane by default, key-toggle to a free camera to fly out and watch the impact. |
| 6 | Mount selection | **The stock loadout supplies the starting fit, never the choice.** Mounts come from the plane's full marker rig — 4 gun groups over `firepoint1…8` plus every individual firepoint, and all 8 pylons. |

## Milestone goal

- `--weapon-lab --plane=X [--chapter=C2]` opens a chapter world with the aircraft held in place,
  armed, and firing through the same code path free flight uses.
- Any of the 48 weapons mounts on **any** firepoint/pylon of the airframe — no
  `stock_loadouts.json` edit needed to reach a mount the stock fit omits.
- A hit on real water plays the authored splash, on a real building the buildings-class `IMPACT`
  variant, on terrain the dirt one — because the thing being shot really *is* that surface.
- Rockets leave the pylon as their `FLYOUT` model with their smoke trail, and the mounted
  ordnance model disappears from the pylon as it launches.
- Left-click any point in the world → the plane re-parks on your line of sight, aimed at it, at
  the stand-off you chose; the panel names what you clicked and its surface class.

**Boundary: no new weapon *systems*.** No armour pool (`BL-085`), no hittable aircraft
(`BL-226`), no sticky-bullet aim assist (`BL-091`), no new effect decodes. This plan changes only
*where and how the existing firing code is driven* — every visual it unlocks already exists and is
already exercised by `--fly`.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a
  handler; never guess a value.
- **Evidence is a lead to verify, not a finding to implement.** A correct disproof that lands no
  code is a success.
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn**
  as each landed item; a landed item gets a dated `docs/HISTORY.md` entry.
- **Read `docs/verification.md` before measuring anything.**
- **Read the module's entry in `docs/architecture.md` before modifying it** — `src/UI/WeaponLab.cs`
  (line 1253), `src/Flight/Projectile.cs` (670), `src/Flight/PylonOrdnance.cs` (1051),
  `src/Session/GameSession.cs` (1420), `src/Session/WorldEffectsFactory.cs` (1689).
- **Verify with `.\RunTests.ps1`** (build → units → in-engine suites → golden hashes → one exit
  code) and drive probes through `RunProbe.ps1`, never the Godot exe directly.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven.

### Wave A — the lab becomes a flight mode

1. ☐ A1 — `--weapon-lab` routes to flight, not the viewer (`SessionSpec`)
2. ☐ A2 — `FlightController.Held`: the airframe holds its pose while the world sim runs
3. ☐ A3 — `GameSession` builds the lab in the flight path, off the shared pool/effects wiring

### Wave B — the full-rig loadout

4. ☐ B4 — `Loadout.ForRig`: 4 gun groups over every firepoint + all 8 pylons, seeded from stock
5. ☐ B5 — panel drives the live loadout (weapon/mount swap, ammo refill, ordnance rebuild)

### Wave C — targeting

6. ☐ C6 — click-to-place: raycast pick, surface readout, re-park on the camera ray
7. ☐ C7 — scripted twins: `--weapon-target=`, `--weapon-surface=`, `--weapon-standoff=`

### Wave D — camera, probes, docs

8. ☐ D8 — orbit ↔ free camera switch in the lab
9. ☐ D9 — `WeaponBench`: the 48-weapon pass check, split off the lab node
10. ☐ D10 — docs: `cli.md`, `controls.md`, `architecture.md`, `HISTORY.md`, `PROJECT_CONTEXT.md`

## Dependency and parallelism notes

A1→A2→A3 is a chain and blocks everything. B4 blocks B5. C6/C7 need A3 (a world with colliders)
and B5 (something to fire). D8 needs A3; D9 is independent of everything after A1 and can run in
parallel; D10 last. **File contention:** A1 and C7 both edit `SessionSpec.cs`; A3, C6 and D8 all
edit `GameSession.cs`; B5, C6 and D8 all rewrite `WeaponLab.cs` — do not run those in parallel
worktrees.

---

# Wave A — the lab becomes a flight mode

## A1 ☐ `--weapon-lab` routes to flight, not the viewer

**Goal.** `--weapon-lab[=wep_id] --plane=player_bhawk` builds a chapter-world *flight* session
(default `--chapter=C1`), the way `--stunt` is a flight modifier. `--weapon-test` alone keeps its
cheap no-world path.

**Evidence (confidence: traced).** `SessionSpec.Resolve` votes the lab into the viewer today
(`SessionSpec.cs:773-774`); `WorldMode` (`:879`) and `BuildsCollision` (`:157`) both come out true
for free once the mode is `Fly`.

**Approach.** Drop `WeaponLab` / `WeaponMount != null` / `WeaponFire` from the `viewer` vote and
add them to the `fly` vote (`SessionSpec.cs:770`); leave `WeaponTest` voting viewer, since D9
keeps that a plane-only probe. Update `SessionSpecTests` accordingly.

**Model recommendation.** medium — mechanical, but the arbitration order in `Resolve` is
load-bearing and documented as such.

**Verify.** `--weapon-lab --plane=player_fury` logs `mode=fly chapter=C1`; `--weapon-test` still
logs the viewer subject. `dotnet test` (`SessionSpecTests`).

**⚠ Traps.** `Resolve`'s step ORDER is behaviour (`SessionSpec.cs:759-764`) — add the vote, don't
reorder the block. `--weapon-lab --viewer` must now report the contradiction rather than silently
half-building.

## A2 ☐ `FlightController.Held`: the airframe holds its pose while the world sim runs

**Goal.** A held aircraft does not move, does not stall, does not fall, and does not collide — but
its props spin, its guns fire, its rounds fly and the world keeps running. This is *not* the P
halt, which stops the whole clock (`FlightController.cs:740-761`).

**Evidence (confidence: traced).** `SimStep`'s weapon half (`FlightController.cs:699-704` —
`CycleWeaponSelectors` / `UpdateGuns` / `UpdateRockets` / `Ordnance?.Update()`) is already
independent of the flight-model half above it (`:647-693`); `FlightModel.Reset(position, attitude,
speed, throttle)` (`FlightModel.cs:119`) is the existing placement entry point `Respawn` uses.

**Approach.** Add `public bool Held` to `FlightController`. When set, `SimStep` skips
`ReadKeyboard`/`_model.Step` and the whole collision sweep, re-applies the pinned pose through
`FlightModel.Reset(pos, attitude, speed: 0, throttle: 0)`, and sets `_simPrev = _simCurr =
GlobalTransform`. Everything below that point runs unchanged. Add
`public void PlaceHeld(Vector3 pos, Vector3 lookAt)` for C6/C7 to teleport it, reusing the same
`Reset` + `SnapCamera` pair `Respawn` uses. Also expose `SelectGunGroup(int)` / `SelectPylon(int)`
so B5 can drive `_gunSel` / `_selectedPylon` live (currently only settable via `InitialGunSelect`
at `_Ready`).

**Model recommendation.** high — `SimStep` is the hottest path in the project and its ordering
comments record earlier mistakes.

**Verify.** `--weapon-lab --plane=player_bhawk --fire --frames=300 --screenshot=.scratch/held.png`:
the flight telemetry line reports `spd=0.0` and an unchanged `pos=` for the whole run, while
`weapons` log lines keep spawning rounds. Regression: a plain `--fly` capture must be
byte-identical to its pre-change twin (`Held` defaults false).

**⚠ Traps.** A held plane must not trip the `UnderMapY` respawn backstop (`:717`) or the stall
gauge (`:822`) — both read `_model`, which is why the pose is re-applied through `Reset` rather
than by writing `GlobalTransform` behind the model's back. Do **not** reuse `GameClock.Halted`:
the point is that the *world* keeps running.

## A3 ☐ `GameSession` builds the lab in the flight path

**Goal.** The lab node is built in `BuildFlightRigs`, after the rigs, holding a reference to
player 1's `FlightController` — and therefore sharing the session's fully-wired `ProjectilePool`
(`GameSession.cs:1196-1207`: `flyoutGamez`/`flyoutScene`/`flyoutAnims`/`soundGroups` +
`DamageSink`) and its world-effects `EffectSink` (`:1214-1225`).

**Approach.** Delete the viewer-side lab construction (`GameSession.cs:1030-1082`) and the
`_weaponLabNode.SimStep` fork in `DriveSimSteps` (`:1635-1638`) — the controller now owns the fire
clock. Build the lab at the end of `BuildFlightRigs` when `_spec.WeaponLab`, passing
`(rig.Controller, weaponDefs, stockLoadouts, projectiles, rig.Camera)`. Default `InfiniteAmmo` on
in the lab so a soak run never dries up. Keep the `--weapon-test` branch out of this path entirely
(D9).

**Model recommendation.** high — touches the session build order.

**Verify.** `--weapon-lab=wep_06 --plane=player_bhawk --chapter=C1 --weapon-fire` and watch for
`world-effects runtime: … effect template(s) staged` in the log plus a `flyout` instance in the
pool; a rocket must leave a smoke trail and its impact must play a named effect
(`--log=weapons,anim`). Compare against the same shot from `--fly --fire-rockets` — they should be
indistinguishable.

**⚠ Traps.** `EffectSink` is only wired when `state.WorldScene != null` (`:1215`) — on
`--stage=empty` there is no world program, so the lab must say so in one line rather than silently
drawing stand-ins again. The 8-chapter `--freecam` regression still has to pass: nothing in the
flight build may change for a non-lab session.

# Wave B — the full-rig loadout

## B4 ☐ `Loadout.ForRig`: every firepoint and pylon, seeded from stock

**Goal.** In the lab, the mount list is the airframe's **whole** marker rig — 4 gun groups
(W1→`firepoint7,8`, W2→`fp5,6`, W3→`fp3,4`, W4→`fp1,2`, the authored rule from
`docs/formats/markers.md:150-155`), each individual firepoint as its own single-muzzle mount, and
`pylon1…8` — regardless of what `stock_loadouts.json` binds. The stock fit only decides which
weapon each group *starts* with and supplies the readable mount names ("Inner Wing Guns").

**Evidence (confidence: traced).** `MarkerRig.Classify` (`MarkerRig.cs:73+`) already recognises
`firepointN`/`pylonN`; `WeaponLab.CollectRawMarkers` (`WeaponLab.cs:267-296`) already walks them as
the no-loadout fallback; `GunGroup.Weapon` / `Hardpoint.Weapon` are plain mutable fields
(`Loadout.cs:266-290`), which is how `ProbeRunner.ApplyRocketOverride` (`ProbeRunner.cs:47-66`)
already re-arms every pylon at launch. Marker counts are uniform 8/8 per airframe, Kestrel 7
firepoints (`markers.md:20-21,52,79`).

**Approach.** Add `Loadout.ForRig(Node3D plane, WeaponDefs, LoadoutDef? stock)`: discover the
markers, synthesize a `LoadoutDef` naming all of them by the slot rule, and run it through the
existing `Loadout.Bind` so there stays exactly **one** bind path. Groups the stock fit defines keep
its weapon, mount name and caliber; the rest default to the stock's first gun weapon (or `wep_30`
when the plane has none). Every synthesized group is `IsTurret = false` so all four are firable — a
deliberate lab-only difference from stock, where the turret slot is inert until M4. A Kestrel
(7 firepoints) simply yields one single-muzzle group.

**Model recommendation.** medium.

**Verify.** New in-engine assertion in `Suites.cs`: for all 11 airframes, `ForRig` yields 4 gun
groups covering every `firepointN` present and one hardpoint per `pylonN`, with no marker bound
twice and none missing. `--dump-loadout` for a lab session lists mounts the stock file never names.

**⚠ Traps.** `Loadout.Bind` throws loudly on a missing marker by design (`Loadout.cs:230-238`) —
`ForRig` must only name markers it actually discovered, never assume 8. Co-located firepoints are
real (`markers.md:221-235`); two groups sharing a point is correct, not a bug. Do not "fix"
`stock_loadouts.json` — the stock fit stays the description of the original.

## B5 ☐ The panel drives the live loadout

**Goal.** The panel's bank / weapon / mount steppers change what is actually mounted: picking a gun
assigns it to the selected group and refills ammo; picking a hardpoint weapon re-arms every pylon
and **rebuilds the mounted ordnance models** so the wings show the new type; the mount stepper
drives which gun group / pylon the trigger uses. A "reset to stock" button restores the authored
fit.

**Approach.** Strip `WeaponLab` down to a panel + state: delete `FireVolley`, `SimStep`, `_pool`,
the target wall, `BuildScene`, `PlaceTarget`, `ApplySurfaceTag` and the `Mount.NextNode` cursor.
Weapon swap = write `GunGroup.Weapon`/`Capacity`/`Ammo` (or reuse `ProbeRunner.ApplyRocketOverride`
for the pylon bank), then `controller.Ordnance = PylonOrdnance.Build(loadout, pool)` after freeing
the old one (`PylonOrdnance.cs:33`). Mount stepper calls A2's `SelectGunGroup`/`SelectPylon`. The
auto-fire toggle sets `controller.AutoFire` / `AutoFireRockets` by bank. Panel toggle moves from
**W** (pitch in flight!) to **B**; keep the CLI-args copy button, extended with the new flags.

**Model recommendation.** high — this is the item that deletes the duplicated firing loop, and
getting the ordnance rebuild wrong leaks nodes per swap.

**Verify.** Step the hardpoint bank through every rocket with `--log=weapons` and confirm the
mounted model changes with the selection (the D44 pylon bodies) and that the model count stays
constant across 50 swaps (no leak). Fire each gun group and confirm the muzzle flash comes from the
selected group's firepoints.

**⚠ Traps.** `PylonOrdnance.Build` returns null when the pool has no flyout gamez
(`PylonOrdnance.cs:1061-1062`) — a rocket with no `FLYOUT` model must leave the pylons empty, not
throw. Ammo: with `InfiniteAmmo` the counters never drain, so the "hide the model at zero"
behaviour needs the toggle turned off to be seen at all — say so in the panel.

# Wave C — targeting

## C6 ☐ Click to place: pick a real surface, re-park on the camera ray

**Goal.** Left-click anywhere in the world: the panel names what is under the cursor (`cs_name` +
surface class + distance) and the aircraft teleports onto that camera ray at the stand-off
distance, nose on the clicked point. Shift-click aims without moving. A small marker draws at the
aim point.

**Evidence (confidence: traced).** `SelectionService`'s pick is an AABB scan that deliberately
skips map-scale meshes (`SelectionService.cs:39-42`, `MaxPickDiag` 350 m), so it can never select
the sea or a terrain tile — the two things this lab most needs to shoot at. The flight session
builds real colliders (`SessionSpec.BuildsCollision`), and those bodies carry the surface tag
(`SceneBuilder.cs:649-655`).

**Approach.** The lab does its **own physics raycast** —
`camera.ProjectRayOrigin/ProjectRayNormal` into `PhysicsRayQueryParameters3D`. Classify the struck
body with `ProjectilePool.ClassifySurface` (`Projectile.cs:364-376`) — the one classifier the
impact path itself uses, so the panel cannot disagree with what the round does — and read its name
off the `AnimRuntime.NameMeta` ancestor (`SelectionService.NameOf` is reusable as-is). Placement:
`pos = hit - rayDir * standoff`, attitude `Basis.LookingAt(hit - pos)`, applied through A2's
`PlaceHeld`.

**Model recommendation.** high.

**Verify.** In C2, click the bay → panel reads `water`, fire → the authored splash plays
(`--log=anim` shows the water `IMPACT` effect names, not a stand-in). Click a warehouse →
`buildings`, and a rocket both plays the buildings `IMPACT` variant and *damages the destructible*
(`DamageSink` is live in this mode). Click a hillside → `dirt`/default. Take the three as
`--screenshot` captures for the item's evidence.

**⚠ Traps.** The surface tag lives on the **collider body**, not the mesh
(`SceneBuilder.cs:649-655`), and one mesh can yield several bodies of different classes
(`SceneBuilder.cs:668-679`, `BL-204`) — so classify the body the ray returned and nothing else. A
click that hits nothing must say so and leave the plane put. Re-parking must not put the plane
inside geometry: clamp the stand-off if the ray back from the hit point re-enters a collider.

## C7 ☐ Scripted twins for the click

**Goal.** Deterministic captures without a mouse: `--weapon-target=x,y,z` parks the plane facing
that world point; `--weapon-surface=water|buildings|dirt` finds the nearest collider of that class
to the spawn and parks facing it; `--weapon-standoff=<m>` sets the distance (default 90).

**Approach.** Parse in `SessionSpec` beside the existing weapon flags; the surface search walks the
built world once for `StaticBody3D`s carrying (or lacking) `SceneBuilder.SurfaceMeta` and takes the
nearest by centre. Log the resolved point and class in one line so a capture's evidence is in its
own log.

**Model recommendation.** medium.

**Verify.** `--weapon-lab=wep_06 --weapon-surface=water --weapon-fire --det
--screenshot=.scratch/lab_water.png` reproduces byte-identically across two runs.

**⚠ Traps.** `--det` is implied by `--screenshot=` — the surface search must be deterministic
(nearest-by-distance with a stable tie-break on node index), or every lab capture becomes flaky. A
chapter with no water must warn and leave the plane at spawn, not fail the launch.

# Wave D — camera, probes, docs

## D8 ☐ Orbit ↔ free camera

**Goal.** The lab starts orbiting the held plane; one key hands the camera to a free camera so you
can fly out to the impact point and watch it from a metre away.

**Approach.** The orbit already exists — `FlightController.SeedOrbit`/`UpdateOrbitCamera`
(`FlightController.cs:1924-1947`) run today whenever the sim is halted. Gate that on
`Held || halted` instead of `halted` alone. For the free view, add
`FlightController.CameraOwned`: when set, skip every camera write (`:781-793`, `SnapCamera`), and
have the lab attach a `SpectatorCamera` (`src/Flight/SpectatorCamera.cs`, the `--freecam` one) to
the same `Camera3D`. Toggle on **V**.

**Model recommendation.** medium.

**Verify.** Toggle both ways during a burst — no camera jump on hand-back, and the plane's HUD
stays live. `--fly` unchanged (`CameraOwned` defaults false).

**⚠ Traps.** Splitscreen: the lab binds player 1's rig only; with `--players>1` either refuse the
combination or bind rig 0 and say so.

## D9 ☐ `WeaponBench`: the 48-weapon pass check, split off the lab

**Goal.** `--weapon-test` and the `weapons-fire` in-engine suite keep their cheap, world-less "do
all 48 mount and fire without throwing" check after the lab moves into flight.

**Approach.** Move `WeaponLab.SelfTest`/`RunSelfTest`/`SelfTestResult`
(`WeaponLab.cs:189-241,840-847`) into a new static `Flight/WeaponBench.cs` taking
`(Node3D plane, Loadout, WeaponDefs, ProjectilePool)` — B4's `ForRig` loadout, so the bench now
covers every mount, not just the stock ones. `Suites.WeaponsFire` (`Suites.cs:264-308`) and the
`--weapon-test` branch call it directly; neither constructs a `WeaponLab` any more.

**Model recommendation.** medium — a move, but it is what keeps `RunTests.ps1` cheap.

**Verify.** `.\RunTests.ps1` — `weapons-fire` still asserts 48/48 with 0 errors and 0 skips, and
the skip count should now be 0 *because every weapon has a mount*, not because the fallback found
one.

**⚠ Traps.** The suite's counts are pinned constants (`WeaponDefCount`) — if `ForRig` changes the
mount count, update the assertion deliberately, and keep `Skipped` asserted at 0: it is the
success-looking outcome the existing comment warns about.

## D10 ☐ Docs

**Approach.** Rewrite `docs/cli.md`'s `--weapon-lab` / `--weapon-mount` / `--weapon-fire` bullets
(they currently describe the viewer lab and its stand-ins at length, `cli.md:170-172`) and add
`--weapon-target` / `--weapon-surface` / `--weapon-standoff`, keeping the flag-index count in step
(99 → 102). Move the **W** row out of `controls.md`'s `--viewer` table into a new weapon-lab table
(**B** panel, **V** camera, click to place, Shift-click to aim). Rewrite `docs/architecture.md`'s
`src/UI/WeaponLab.cs` entry — all four of its current ⚠ lines die with this plan — and add the
`Held`/`CameraOwned` constraints to `src/Flight/FlightController.cs`'s. Append a dated
`docs/HISTORY.md` entry; point `PROJECT_CONTEXT.md`'s "Current status" at this plan when the work
starts and swap it again when it lands.

**Model recommendation.** medium.

**Verify.** The parser's accepted-flag count matches `cli.md`'s flag-index count; no doc still
claims the lab has no world.

---

## Verification (end to end)

1. `.\RunTests.ps1` — build, xUnit, in-engine suites, golden hashes, one exit code. The 11 pinned
   goldens are all `--freecam` shots and must be **unchanged**: this plan adds a mode, it does not
   alter world rendering.
2. Interactive: `.\RunGame.ps1 --weapon-lab --plane=player_bhawk --chapter=C2` — click the bay,
   fire guns, watch the splash; click a warehouse, fire a rocket, watch it break; step the
   hardpoint bank and watch the wing models change; V out to the impact point.
3. Scripted evidence, three captures via `RunProbe.ps1`:
   `--weapon-lab=wep_06 --weapon-surface=water|buildings|dirt --weapon-fire --frames=90
   --screenshot=.scratch/lab_<surface>.png`.
4. Regression that the mode is additive: an 8-chapter `--freecam --chapter=<X>` sweep with zero
   errors and unchanged mesh/node counts, plus one `--fly --fire --fire-rockets` capture compared
   against its pre-change twin.
