# Milestone 2 Polish Plan

Working plan for the not-yet-done items in NOTES.md's "Milestone 2 Polishing" section,
in the agreed implementation order. Each item lists its goal, the data/code evidence it
rests on, the approach, and how it gets verified. Statuses: ☐ open · ◐ in progress · ☑ done.

Ground rules carried over from CLAUDE.md: original-game data drives everything (zrdr
readers + gamez/planes extractions); hand-tuned constants are marked TUNE and validated
by user playtests against the original; CLAUDE.md is updated in the same turn as each
landed item.

## Checklist

1. ☐ Wing-light blink
2. ☐ Chase camera rolls with the plane
3. ☐ Moon size
4. ☐ Weather: distance fog, cloud-band whiteout, cloud deck anchoring, ambient puffs
5. ☐ Forest trees missing from forest-textured terrain
6. ☐ Flight model: stall toward ground, knife-edge lift, climb speed retention
7. ☐ Control-surface animation (ailerons/elevators/rudders)
8. ☐ Finer plane collision (real swept shapes instead of one ray)

---

## 1. Wing-light blink

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

## 2. Chase camera rolls with the plane

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

## 3. Moon size

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

## 4. Weather: fog, whiteout, cloud deck, ambient puffs

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
