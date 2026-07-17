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
4. ☑ Weather: distance fog ☑ (remodeled 2026-07-17: cylinder + FOG_ALTITUDE + sRGB gray), cloud-band whiteout ☑, cloud deck anchoring ☑, ambient puffs ☑
5. ☑ Forest trees — clutter-template system (2026-07-17); tree crashes user-confirmed
6. ☑ Flight model — velocity-vector rework (2026-07-17): real lift (no 0-mph hover), stall toward ground, knife-edge sink, climb retention; TUNE pending playtest
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

## 4. Weather: fog, whiteout, cloud deck, ambient puffs — ☑ DONE (2026-07-17)

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

- **Cloud deck follows the player (2026-07-16):** WorldBuilder splits the `cloudlayer` deck
  (144 tiles at y=960 covering the map) into a `CloudDeck` node; PlaneViewer re-anchors it each
  frame centred on the camera x/z and pinned to a fixed altitude at the **whiteout-band centre**.
  You climb toward it as a fixed overcast ceiling, pass through it exactly where the whiteout is
  fully opaque (the ceiling→floor transition is hidden), and it becomes a floor once above the
  band. cloud1/cloud2 sprites stay world-fixed. Model chosen by user playtest: a fixed-gap-
  follows-you variant and this band-centre anchor were prototyped behind a `--deck-ceiling`
  toggle; the user picked the band-centre anchor, so the fixed-gap model + toggle were dropped.
  Verified: low camera / `--fly` shows an overcast ceiling above the plane (matches
  `OriginalScreenshots/C1 IA1 Cloudcoverage 1.png`); above the band it flips to a floor below;
  static orbit viewing unchanged.

- **Fog remodeled (2026-07-17, user-diagnosed):** the fog volume is a vertical **cylinder**,
  not a sphere — horizontal (x/z) distance only, scaled by a `FOG_ALTITUDE` fade (full fog
  below low, none above high; zone1's 970→1047 = cloud-band bottom → whiteout-band centre,
  zone2's 4000→5000 > flight ceiling). This un-grays the overcast deck overhead. Plus a
  measured colorspace fix: `FOG_COLOR` 0.69 is an sRGB framebuffer value (the original's
  saturated fog = exactly 176 gray) — now converted sRGB→linear at the set-site; raw 0.69 in
  the linear pipeline had rendered a washed-out 216. Verified against `C1 IA1 Cloudcoverage
  1.png` (deck textured overhead, converging byte-exact to 176 at its horizon) and `C1 IA1
  Fog Range.png` (terrain wall + ghosted zeppelin). TUNE left open: the `fogRangeFactor` 2
  halving predates the color fix — fresh in-game A/B recommended (factor 1 keeps deck texture
  visible further down); the original's overcast tone (~165–175) is also darker than our
  rendered deck texture (~206) — a deck-tint question separate from fog.

- **Ambient cloud puffs (2026-07-17):** `src/Effects/CloudPuffs.cs` — a hand-tuned ambient
  field of soft `cloud1`/`cloud2` billboard sprites (one `MultiMeshInstance3D`, a spatial
  shader billboarding + rolling + alpha-blending each quad) that the plane flies through at
  altitude. **Why a synthetic field and not the world's own sprites:** C1 *does* place ~600
  `cloud1`/`cloud2` sprite quads, but they are **clustered near the airfield** (verified: two
  far map corners at band altitude render bare) and world-fixed, so most of the map has no
  clouds at altitude. No zrdr defines an ambient emitter (only the crash-style PUFFER_STATE
  readers exist), so — per this item's own "failing that" fallback — the field is hand-built
  from the data we do have: the `CLOUD_COVER` band anchors the layer's altitude and the
  weather `WIND` drives a slow drift. Model: a pool of world-anchored sprites in a cylindrical
  shell around the camera, **anchored in Y to the cloud band** (you climb up into it, then out
  over it, like a real layer) while following the plane in X/Z; a puff the plane flies past
  horizontally recycles to the leading edge (endless field), and alpha fades at the shell edge
  (recycled puffs fade in, never pop) and by vertical distance from the camera (clear well
  below/above the layer — no clouds at low altitude). PlaneViewer builds it in `SetupWeather`
  (when the mission has a `CLOUD_COVER` band) and advances it each `_Process` from the camera
  pose. Constants are TUNE (`Count` 12, `Radius` 620 m, `BaseAlpha` 0.06–0.13 — the cloud
  textures are fairly opaque, mean α≈0.43, so overlaps saturate fast; kept faint so they read
  as translucent veils). Verified via `--campos` at 850/1120/1250/1500 m: absent below the
  band, a soft layer around/through it, cloud-tops below once above it (moon + stars showing
  through), and present at both airfield-adjacent and far-corner locations (the field follows
  the plane); `--fly` smoke test clean (no regression; puffs correctly absent at the ~325 m
  spawn). Pending user playtest to fine-tune the opacity/density against the real in-flight sky.

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

## 5. Forest trees — ☑ DONE (2026-07-17)

**Landed as:** `src/Mech3/Clutter.cs` (`ClutterBuilder`). The diagnosis overturned the
"placed subtrees" assumption: all 28 tree nodes hang under `terpat02`, a parentless,
UNREFERENCED root — one of the original's **clutter templates**. The chapter boot script
(interp.json → `support\c1\adjust.gw`) registers them (`AddClutterTemplates terpat02` /
`river1` / `river2`); a template is a ground quad whose texture names the terrain texture
it decorates (terpat02.tif = the forest texture) and whose size (512 m) is the tiling
period, with decoration sprites (single one-sided quads: firs 17.8–22.5 m, bushes) at
local positions on the patch. `ClutterBuilder.TemplateNames` reads the chapter's list;
`Build` stamps each template across every placed world polygon textured with its ground
texture on a fixed world-space grid of the period (the original's exact alignment is
undecoded; world UV tiling is too non-uniform — 256–1280 m/repeat — to follow), planting
each sprite at the polygon's barycentric surface height, skipping >75° slopes, deduping
decal-layered coplanar polys. C1: 9,303 sprites (≈9k firs + 311 river bushes). Rendered
as one Y-axis-billboard MultiMesh per kind — upright, fullbright, scissor cutout, same
cylindrical fog as the world shader. Collision (flight only): one crossed-quad trimesh
(`clutter_col`, 37k tris), and the FlightController crash log now names the hit collider.
Only flat sprite cards billboard — C2's filmblock 3D buildings are detected and skipped
(future). Verified: firs render exactly on forest-textured slopes (bare valleys bare),
close-ups upright and planted; level scripted flight through the forest valley clean;
crash-name logging proven (`CRASH into g314/col`); **user-confirmed in-game
(2026-07-17): crashing into trees works.** Remaining TUNE: density/size eyeball vs the
original during normal playtests.

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

## 6. Flight model: stall, knife-edge lift, climb speed — ☑ DONE (2026-07-17)

**Landed as:** a rework of `FlightModel.Step` into the standard simple velocity-vector
decomposition (researched: brihernandez's ArcadeJetFlightExample, Vazgriz's Unity flight
sim — gravity always acts on the velocity vector; lift ⊥ velocity ∝ v², dies at stall and
with bank; arcade handling = the velocity chasing the nose), plus the user's added report:
**the plane had no lift at all — it could slow to 0 mph and hang in the air** (the old
code snapped the path to the nose and skipped all gravity effects below 1 m/s, freezing a
0-speed plane in place). The pieces: lift fraction = `min(1,(v/v_lift)²) × |up·Y|`
(wingVert: 1 level or inverted — arcade carry — 0 knife-edge); the lift **deficit** bends
the flight path toward the ground every frame (`g·(1−lift)/max(v,10)`), so slow flight
sinks and ~0 airspeed falls; the nose-chase alignment scales with airspeed AND wingVert
(`KnifeAlignFloor` 0.35 TUNE — knife-edge path settles ~10° below the nose); stall (< 0.3
fd_speed) pulls the nose toward **world-down** via a great-circle rotation (attitude-
independent, works inverted, out-muscles full elevator at depth — `StallNoseRate` 1.0 ×
`stall_mag` × depth, TUNE); thrust along the path × `max(0, nose·dir)` (a nose-high
falling plane must not rocket downward); gravity's along-path speed bleed × 0.6 climbing
(`ClimbGravityScale` TUNE), full diving. **Follow-up (same day, user-observed original
rule): while stalled the nose cannot be raised over the horizon at any bank angle** —
after the frame's rotations its world elevation is capped at `max(horizon, frame-start
elevation)`: pulled up from below it parks at the horizon, caught nose-high it only
descends. Verified: stall-into-dive-then-full-pull run shows no stalled sample raised
above 0°; a full-pull zero-throttle hold no longer loops endlessly — it zooms, breaks
at the top, dives, recovers (porpoise), like the original's stall.

**Follow-ups (same day, both user-reported on first playtest):** (1) *no air
resistance* — idle level flight barely slowed, because pure-v² drag dies off below
cruise → drag is now a quadratic + linear blend (`LowSpeedDragBlend` 0.35 TUNE),
normalized so drag(fd_speed) = max thrust (the full-throttle equilibrium stays exactly
fd_speed for any blend value); idle 120 mph now bleeds to the ~89 mph stall in ~9 s and
settles into a natural nose-dropped glide. (2) *plane stops mid-air with mph creeping
back up* — the scalar `Speed` was clamped at 0, so a zoom climb that ran out froze in
place and then re-integrated speed along a stale upward path; the translation block now
integrates thrust/drag/gravity on the velocity **vector** (`v = dir·speed`), so speed
passes through zero and a spent zoom tail-slides out downward immediately. This also
deleted two hacks the vector sum makes redundant (the `max(0, nose·dir)` thrust scale
and the sag-divisor speed floor). Re-verified after both: zero-throttle zoom descends
through the apex with no hang; knife-edge sink and cruise equilibrium unchanged. New
known artifact: at full throttle a steep (~53°) climb is a stable equilibrium (thrust ≈
0.6-scaled gravity) and the data's 2500 m flight_ceiling is not modeled — backlog. Verification needed input *sequences*, so
`--hold` gained `;`-separated segments with `@seconds` durations (FlightController
`HoldSegments`; respawn restarts the sequence) and the telemetry line grew `path`/`nose`
climb angles + `wv` wing verticality. Verified: zoom-climb run bleeds 48→12 m/s at nose
+88°, noses over to −67° and recovers in the dive — no hover, deterministic across
respawns; knife-edge (wv 0.06) sinks at path −10°, −174 m in 15 s; 11° full-throttle
climb holds 122.5 m/s; level cruise regression exactly unchanged (wingVert 1 → every new
factor is 1). Known accepted artifact: the climb/dive asymmetry pumps energy in sustained
loops (zero-throttle full-pull loops slowly gain speed) — revisit only if playtest minds.
Pending user playtest to tune `StallNoseRate` / `KnifeAlignFloor` / `ClimbGravityScale`
(the latter against a measured original climb-speed-decay curve).

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
