# Milestone 2 Polish Plan

Working plan for the not-yet-done items in NOTES.md's "Milestone 2 Polishing" section,
in the agreed implementation order. Each item lists its goal, the data/code evidence it
rests on, the approach, and how it gets verified. Statuses: ☐ open · ◐ in progress · ☑ done.

Ground rules carried over from CLAUDE.md: original-game data drives everything (zrdr
readers + gamez/planes extractions); hand-tuned constants are marked TUNE and validated
by user playtests against the original; CLAUDE.md is updated in the same turn as each
landed item.

## Checklist

1. ☑ Wing-light blink
2. ☑ Chase camera rolls with the plane
3. ☑ Moon size — no change needed (user re-checked in-game 2026-07-16: already matches)
4. ◐ Weather: distance fog ☑, cloud-band whiteout ☑, cloud deck anchoring ☐, ambient puffs ☐
5. ☐ Forest trees missing from forest-textured terrain
6. ☐ Flight model: stall toward ground, knife-edge lift, climb speed retention
7. ☐ Control-surface animation (ailerons/elevators/rudders)
8. ☐ Finer plane collision (real swept shapes instead of one ray)

---

## 1. Wing-light blink — ☑ DONE (2026-07-16)

**Landed as:** `WingLights` (Mech3, classifier + data constants) + `WingLightBlinker`
(Flight, PropAnimator-style toggler). PlaneBuilder now builds the `wing_flare1/2` nodes
hidden (reset state) and re-skins each glow quad (`oil_liteflare.tif`) as an additive,
camera-facing billboard tinted the data's warm amber (LIGHT_STATE COLOR 0.88/0.78/0.36) —
so the flare reads from any angle, not just from behind (the source quads are one-sided).
In `--fly`, FlightController advances the blinker (frozen while paused/crashed, reset on
respawn), flashing the flares for a short window (`FlashDuration` 0.08 s, TUNE — the data
flash is one frame) every `WingLights.BlinkPeriod` 1.5 s. The additive-billboard treatment
is scoped to the flare nodes by name (not the texture — `oil_liteflare` is also used by a
few airframe meshes, which must not be recentered/billboarded). Verified: blink interval
logged at 1.528/3.003/4.505 s (≈1.5 s); force-on screenshot shows both wingtips glowing
amber from a banked chase view; static `--plane` viewer shows no flares; Bloodhawk/autogyro
(no `wing_flare` nodes in the data) get no blinker and fly clean. The 1.25 m point lights
the anim also toggles are skipped (negligible at chase distance). Remaining as-designed:
`FlashDuration` is a first-approximation TUNE pending user playtest against the original.

**Goal:** Planes' wingtip lights flash yellow every 1.5 s like the original (user-verified
on Kestrel and Fury); today the flare sprites render permanently and are only visible
from behind the plane.

**Evidence (all in the data):**
- `extracted/zrdr/wing_light.json` — `wing_lights_blink` / `wing_lights_brigand`
  ANIMATION_DEFINITIONs: RESET_STATE deactivates `wing_flare1`/`wing_flare2`; the
  `blink_lights` sequence activates both flares plus two point lights (COLOR
  0.88, 0.78, 0.36 — warm yellow; RANGE 0.5–1.25 m) and deactivates everything again at
  EVENT_OFFSET 0.0001 s (≈ a single-frame flash), looping forever at SEQUENCE_OFFSET 1.5 s.
  The whole thing only runs when ANIMATION_LOD is HIGH.
- `extracted/zrdr/vehicle.json` wires `wing_lights_blink` into most plane defs (same
  pattern as `spin_props_anim`).
- `extracted/planes/nodes.json` has 38 `wing_flareN` nodes across the fleet, plus
  `winglight1`/`winglight2` and `wingtip_lights` group nodes.

**Why it's broken now:** PlaneBuilder ignores this anim's RESET_STATE, so the one-sided
flare quads render always — exactly the "sprites visible from behind the plane" symptom.

**Approach:**
- PlaneBuilder: hide `wing_flare*` at build time (reset state), in both the static viewer
  and flight. Record the flare nodes like PropParts records prop discs.
- Flight mode: a small `WingLightBlinker` (PropAnimator-style, advanced from
  FlightController) toggles the flare nodes visible on the 1.5 s cycle. The data says the
  flash is ~instant; at the original's frame rate that reads as one bright blink — start
  with a ~50 ms visible window, TUNE against the original.
- Render the flare quads as additive billboards (they are sprite quads; one-sided static
  quads are the current bug). The 1.25 m point-light radius is negligible at chase-cam
  distance — skip the actual OmniLight3D unless playtest misses it.

**Verify:** night `--fly` run on Kestrel/Fury: blink interval 1.5 s (log), flash visible
from front and behind; static `--plane` viewer shows no flares. Side-by-side with original.

## 2. Chase camera rolls with the plane — ☑ DONE (2026-07-16)

**Landed as:** `FlightController.UpdateChaseCamera` (replacing the inline `_Process` camera
block). `DesiredCamPos` now offsets the camera behind-and-above in the plane's own frame
(`camUp = _model.Attitude.Y`, full bank-follow — was `Vector3.Up.Lerp(up, 0.45)`, which
went degenerate inverted). The orientation is no longer a hard per-frame LookAt from a
near-world up; instead the camera *basis* is slerped toward `Basis.LookingAt(lookTarget −
camPos, planeUp)` at `CamRotSmooth` 7 /s (TUNE — a touch of rotational lag so fast rolls
read dynamic), with the existing position smoothing kept at `CamSmooth` 8 /s. A dot-product
guard falls back to world-up if the view direction ever runs parallel to the plane's up
(practically never — the plane's up is ⟂ to its nose). `SnapCamera` (spawn/respawn) still
sets the orientation instantly via LookAt, so there's no slerp transient on (re)spawn.
Verified: scripted pure-roll `--hold=0,1,0,0.7` flight, screenshots at successive roll
phases — the horizon rotates smoothly 0°→90°→180°→270° with the world fully inverted at
180° (sky at the bottom, ground at the top) and no camera flip/snap/degeneracy through
±90° or inverted. Pending user playtest to fine-tune `CamRotSmooth`.

**Goal:** Flying inverted shows the world upside down, as in the original; the camera
follows the plane's roll fully instead of staying near world-up.

**Evidence:** `FlightController.DesiredCamPos` lerps the camera up-vector only 45 % toward
the plane's up (`Vector3.Up.Lerp(_model.Attitude.Y, 0.45f)`) — near-degenerate when
inverted (the lerp of opposing vectors shrinks toward zero).

**Approach:** use the plane's up-vector fully (bank-follow factor 1.0) and smooth the
camera *basis* (slerp), not just its position; keep the existing position smoothing.
Guard the LookAt against up ∥ view. Optional TUNE: a small lag on roll so fast rolls
read dynamic instead of glued.

**Verify:** scripted `--hold` full roll + loop with screenshots (horizon must rotate
through 360°, no camera flip/snap at ±90°); user playtest.

## 3. Moon size — ☑ DONE (2026-07-16), no code change

**Resolution:** The user re-checked in-game and the remake's moon is already the same
size as the original — the earlier "visible mismatch" did not reproduce. Closed with no
change. (Analysis done before the recheck, kept for reference: the reference shot
`OriginalScreenshots/C1 IA1 Cloud Puffs and Moon.png` has the moon disc ~209 px across ≈
14 % of the 1483 px frame height. Our moon is a `BillboardKeepScale` quad of the mesh's
native 422.8-unit side at horizon-local center (−1624.6, 1426.1, −2154.9), dist 3052 from
the dome origin — elevation 27.9°, matching the designed ~28° — sitting 7630 units from
the camera after the 2.5× dome scale. If a future recheck disagrees, the knob is the
`QuadMesh.Size` in `WorldBuilder.BillboardMoon`.)

**Goal:** The moon's apparent size matches the original (user reports a visible mismatch).

**Evidence / reference:** `OriginalScreenshots/C1 IA1 Cloud Puffs and Moon.png` shows the
original's moon at a measurable angular size. Our moon is a `BillboardKeepScale` quad on
the 2.5×-scaled camera-anchored dome (SceneBuilder/WorldBuilder moon path).

**Approach:** measure the moon's diameter as a fraction of screen height in the original
shot; take an equivalent `--fly` screenshot at the same view and measure ours; correct the
billboard quad scale by the ratio (account for FOV difference between the original —
estimate from its HUD/known geometry — and our camera). One or two iterations with user
eyeball sign-off.

**Verify:** side-by-side screenshots at matching heading/pitch.

## 4. Weather: fog, whiteout, cloud deck, ambient puffs — ◐ IN PROGRESS

**Landed so far (2026-07-16): the `WeatherState` loader + distance fog + cloud-band whiteout.**
- `src/Flight/Weather.cs` (`WeatherState`) parses the flown mission's `weather.json` (same
  archive as the spawn readers) — per-zone `FOG_COLOR`/`FOG_RANGES`/`CLIP_RANGES`, the
  `CLOUD_COVER` band, and `WIND` (parsed now for the future puffs). CLOUD_COVER/WIND pair keys
  with bare scalars, so they're walked as raw pairs rather than through `ZrdrDict`.
- **Distance fog:** SceneBuilder's generated world/aircraft shader gains a fog term — global
  shader params `csky_fog_color`/`csky_fog_range` (registered + set once per flight in
  PlaneViewer, no-op range otherwise), mixed per-pixel toward the fog color over view distance
  (`length(VERTEX)`, view-space under skip_vertex_transform). The camera-anchored skydome opts
  out via a per-instance `csky_fog_on = 0` (WorldBuilder.DisableFog) — at ~22 km it would
  otherwise fog the whole sky solid gray. The aircraft is a no-op (always within the near range
  at chase distance). Verified A/B: C1/IA1 zone2 fog 1000→4000 m fades distant terrain to 0.69
  gray while near terrain and the plane stay crisp; the dome/sky is unchanged vs a no-fog run.
- **Whiteout:** a full-screen `ColorRect` overlay (PlaneViewer, layer below the HUD) whose
  opacity follows `WeatherState.WhiteoutAmount(cameraY)` — a symmetric trapezoid the user worked
  out from the original: clear sight at the `CLOUD_COVER` band edges (970/1124 m in C1/IA1),
  ramping to a fully-opaque near-white core (plane no longer visible) that is THICKNESS deep and
  centred on the midpoint (total only in 1032–1062). So `THICKNESS` (30) is the opaque-core
  depth, not an edge transition. `WhiteoutColor` near-white (TUNE, not the 0.69 fog gray) matches
  `OriginalScreenshots/C1 IA1 whiteout at height.png`. Verified via `--campos` at 965/1000/1047/
  1100 m: clear below 970, partial on the ramps, uniform total whiteout at the 1047 core.
- **Static verification path:** fog + whiteout now also apply in static `--chapter` mode when
  `--sky-zone` is given (same rule that already shows the dome there), so `--campos` at any
  altitude gives deterministic fog/whiteout shots.

**Remaining sub-items (next turn):** cloud deck follows the player (☐), ambient puffs (☐) —
detailed below. The loader already exposes the cloud band + wind they need.

**Goal:** Replicate the original's weather rendering, all user-observed in C1 IA1:
distant terrain fades into fog; climbing into the cloud band whites out the screen
(`OriginalScreenshots/C1 IA1 whiteout at height.png`); the `cloudlayer` deck follows the
player while the cloud1/cloud2 sprites stay world-fixed; very transparent cloud puffs
drift past the plane (`C1 IA1 Cloud Puffs and Moon.png`).

**Evidence:** `extracted/<chapter>/<mission>/zrdr/weather.json` (per mission!) —
- `CLOUD_COVER` TOP 1124 / BOTTOM 970 / THICKNESS 30 (C1/IA1): the whiteout band. The
  user's whiteout screenshot reads ~3400 ft ≈ 1030 m — inside the band. THICKNESS is the
  edge-transition depth.
- Per zone (`ZONE1`/`ZONE2`; `SW_*` twins are likely the software-renderer variants —
  ignore): `FOG_COLOR` (0.69 gray), `FOG_RANGES` (zone2: 1000→4000 m), `FOG_ALTITUDE`
  (zone1: 970–1047; zone2: 4000–5000 — semantics to pin down during implementation; the
  whiteout itself is CLOUD_COVER-driven), `CLIP_RANGES` (far plane 4500).
- `WIND`: STATIC_VELOCITY (0, 2, 0), RANDOM_MAX_SPEED 10, RANDOM_ACCEL 5 — drift source
  for the puffs.
- `VIEWING_RANGE` CLIP_SCALE/FOG_SCALE per detail level (we use HIGH = 1.0).
- Our renderer currently has **no fog at all**, and the whole cloud field is world-static.

**Approach (four sub-items, one WeatherState loader feeding all of them):**
- **Loader:** `src/Flight/Weather.cs` (or similar) parsing weather.json for the flown
  mission (same pattern as SpawnPoints) — fog color/ranges per zone, cloud band, wind.
- **Distance fog:** add a fog term to SceneBuilder's generated world shader (we control
  it — mix toward FOG_COLOR between FOG_RANGES; cheap, uniform-driven). Exclude the
  camera-anchored skydome (painted backdrop). Aircraft shader: same fog for consistency.
- **Whiteout:** full-screen fade to fog color as camera altitude enters the CLOUD_COVER
  band, ramping over THICKNESS at the edges (ColorRect overlay driven from PlaneViewer,
  or a global shader uniform pushing fog density to max — pick whichever also swallows
  the plane model, as the original does).
- **Cloud deck follows player:** WorldBuilder splits `cloudlayer`-textured meshes into a
  separate node re-centered on the camera in x/z each frame (skydome pattern; y stays at
  the data altitude). cloud1/cloud2 sprites remain world-fixed (matches observation).
- **Ambient puffs:** first search the mission zrdrs for a defining reader (none found in
  the shared zrdr); failing that, a hand-tuned ambient Puffer: soft cloud sprites spawned
  in a shell around the plane above ~900 m, drifting per WIND, very low alpha, culled
  beyond ~300 m. TUNE against the screenshot.

**Verify:** scripted climb 800→1200 m: fog thickens, screen whites out inside 970–1124,
clears above; distant terrain fades at 1000–4000 m (screenshot vs original); deck stays
overhead in level flight across the map; puffs drift past at altitude. User playtest.

## 5. Forest trees

**Goal:** Forest-textured hillsides show standing trees as in the original (user-confirmed
fidelity gap).

**Evidence:** C1 gamez `nodes.json` contains dozens of placed `firtree1.flt` /
`firtree2.flt` / `dougfirtree1.flt` Object3d nodes — *parented* subtrees with local
transforms and tree-sized bounds (~3–22 m tall); the tree textures exist in C1
(`firtree1/2.png`, `dougfirtree1.png`). So the trees are data, not procedural — our
WorldBuilder just never places them.

**Approach:** diagnose which skip drops them: trace one tree's parent chain (e.g. the
node with parent 5886) — candidates: their root subtree is not partition-referenced and
fell into the "runtime effect prototypes" bucket; an LOD ancestor whose nearest range we
prune; or a group with `mesh_index: -1` whose children we drop. Fix placement; the cutout
alpha pipeline already handles tree textures (scissor). Give them colliders like other
scenery (the original has `spruce_destroy1/2.json` — trees are hittable/destructible;
destruction itself is dogfight-milestone work).

**Verify:** build log tree count > 0; screenshot of a forested hillside vs original;
scripted flight into a tree crashes.

## 6. Flight model: stall, knife-edge lift, climb speed

**Goal (three user-specified deviations from the original):**
1. Stall should be more prominent and pull the nose toward the **ground**, regardless of
   attitude — today `FlightModel.Step` applies a body-frame pitch-down
   (`BodyRates.X -= …`), which points the nose "down" relative to the plane, not the world.
2. At 90° bank the plane should lose lift and drop the nose (today lift only depends on
   speed, not attitude).
3. The original bleeds less speed in a climb than we do (full `gravity · VelocityDir.Y`
   along-path term today).

**Approach (all in `FlightModel.Step`, all TUNE, playtest-calibrated):**
- **Stall:** below stall speed, rotate Attitude toward world-down (slerp of the nose
  toward −Y scaled by stall depth) instead of the body-frame rate bias; raise the
  magnitude until it reads as decisively as the original. Keep the VelocityDir sag.
- **Knife-edge:** scale the lift fraction by wing verticality — multiply liftFrac by
  |Attitude.Y · Up| (so knife-edge ≈ ballistic, inverted still carries |lift|); the
  gravity sag then produces the nose-drop naturally.
- **Climb retention:** asymmetric gravity-along-path factor: full effect diving,
  reduced (~0.5, TUNE) climbing — calibrate against a measured sustained climb in the
  original (user captures speed decay at fixed throttle/pitch).

**Verify:** scripted telemetry runs — slow-flight inverted must nose toward the ground;
knife-edge flight sinks; climb speed decay within ~10 % of the original's measured curve.
User playtest for feel.

## 7. Control-surface animation

**Goal:** Ailerons, elevators, and rudders visibly deflect with stick input (props already
spin).

**Evidence:** every player plane has named surface nodes in `extracted/planes/nodes.json`
(`l_aileron1..3`, `r_elevator1/2`, `l_rudder`/`lrudder1`, `l_rudder_rotate` hinge helpers,
`flap02–04` on some). **No zrdr anim or reader defines deflection** — the original engine
drives these procedurally, so angles/rates are TUNE, not data.

**Approach:** extend the PropParts pattern: a name classifier maps surface nodes to
(axis, sign, input channel) — ailerons opposite-sign per side about the local hinge axis,
elevators together, rudders with yaw; where a `*_rotate` helper node exists, rotate that
(it is the hinge pivot). Hinge axes read from each node's local frame, corroborated
against the mesh geometry like PropParts did. FlightController drives deflection =
input × max angle (start ±20°, TUNE) with a slew rate (~3 full deflections/s, TUNE).
Flight mode only.

**Verify:** paused orbit camera (P) at full stick: surfaces deflect the right way on all
verified planes (Bloodhawk, Kestrel, autogyro — rotor planes may have no ailerons; the
classifier must tolerate absences); screenshots vs original chase-cam footage.

## 8. Finer plane collision

**Goal:** Wingtips (and tail) collide with obstacles as in the original — today a single
swept ray along the flight path means a wing can pass through a building corner.

**Evidence:** `FlightController._PhysicsProcess` casts one prev→next ray (+6 m nose
margin). User: "collision in the original is a lot finer; tip of wing collides."

**Approach:** replace the ray with a swept **shape** test: build 3–5 convex shapes from
the plane's top LOD (fuselage capsule/hull, one slab per wing, tail) at PlaneBuilder time;
each physics frame, sweep them along the frame's motion via
`PhysicsDirectSpaceState3D` shape casts (`CastMotion` + rest info for the impact point,
feeding the existing crash path/fireball). Keep the current ray as a fast-path backstop
against tunnelling. Extend `--debug-collision` to draw the swept shapes. Scheduled last:
it replaces a working system and benefits from everything else being stable.

**Verify:** scripted runs — wingtip clipping a hangar corner crashes; the same path a
half-wingspan away passes; dive-into-terrain still crashes at the surface (no
regression); frame-time unchanged (a handful of shape casts is cheap).

---

*Sources for the unclear items were pinned down in the 2026-07-16 grilling session:
wing lights = data-driven blink anim; moon size = confirmed visible mismatch; trees =
original-fidelity gap with the trees present in gamez; clouds = deck follows player,
sprites fixed, CLOUD_COVER whiteout, ambient puffs (all user-observed, screenshots in
`OriginalScreenshots/`).*
