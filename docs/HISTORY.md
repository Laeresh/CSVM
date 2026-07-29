# Development history — Milestone 2 log

Chronological record of landed work with verification details, moved from CLAUDE.md's "Current status / next step" section on 2026-07-18. **Append new dated entries at the bottom when work lands**; CLAUDE.md keeps only the compact current state + next step. Entries deliberately preserve diagnosis narratives and dead ends — they exist so future sessions don't re-chase them.

**Milestone 1 (extraction) is essentially already delivered by mech3ax v0.6.1** — the planned RE work is reduced to (a) the cosmetic planes.zbd padding nit (upstream PR candidate) and (b) the deferred anim formats.

**Milestone 2 in progress (2026-07-14): plane rendering, C1 world rendering, arcade free flight, and flight sound work.** The Godot project renders textured aircraft (verified: `player_bhawk`, `player_kestrel`, `player_autogyro`) and the full C1 Sea Haven world from `C1/gamez.zip` (verified via `--chapter --screenshot` renders). `--fly` gives free flight over Sea Haven: zrdr-driven arcade dynamics, chase camera, HUD (verified via `--hold` scripted flights + screenshots — straight flight settles at the drag-curve equilibrium, roll rate matches torque/damp prediction), and original-game sound: the plane's own engine loop with data-driven throttle→pitch, overspeed whine, and airframe rattle (verified via scripted dive past fd_speed — loops start/stop cleanly, no decode warnings). **First user playtest passed (2026-07-14): flight feel "real good for a first throw", sound "like the original"** — the TUNE constants and the audio pipeline are validated as a solid first approximation; fine-tuning deferred until there's more to compare against.

**Ground/object collision added (2026-07-14), user-confirmed working.** The flyable world gets static trimesh colliders (2670 in C1; cloud/sky exempted by texture), and the flight loop swept-raycasts the frame's path each physics tick: flying into terrain, water, buildings, or the airbase zeppelin crashes the plane. Verified via scripted `--hold` runs: a dive crashes at the true terrain surface (≈150 m) instead of sinking through to the old y<60 backstop; a full-throttle climb passes cleanly through the cloud layers (to ≈1200 m) with no false crash. Rendering is unchanged (colliders are invisible; static `--chapter` viewing skips them).

**Crash sequence + collision debug view added (2026-07-14):** a crash now plays one of the game's own plane-explosion one-shots (`snd_exp_plane1..4`), hides the airframe, and freezes at the impact point until R / gamepad Y/A respawns (scripted `--hold` runs auto-respawn after 1.5 s). `--debug-collision` draws the swept collision ray (green, red at impact). Verified via scripted dive: crash → banner + frozen red probe (screenshot) → auto-respawn resumes flight; all four explosion WAVs load without warnings. **User playtest passed (2026-07-14): "the explosion sounds good."**

**Original skydome rendered (2026-07-14):** `--fly` shows the world's real `horizon` backdrop instead of bare procedural sky — camera-anchored, 2.5×-scaled so depth-testing keeps it behind all terrain (see PlaneViewer bullet). Verified via scripted-flight screenshots: dome closed at zenith, no dome shadowing (before/after plane lighting identical), distant terrain not occluded. The procedural sky remains only as ambient-light source behind the dome.

**Zone2 night sky with moon, stars, and beacons (2026-07-15):** `--fly` now defaults to the authentic night sky (`--sky-zone=zone1` restores the day haze). The "empty" `stars` mesh was solved: its 64 stars are point lights in the GameZ mesh `lights` array, now parsed (GameZ.cs) and rendered generically as additive point sprites (SceneBuilder) — which also brought up the world's nav beacons (red tower lights, airbase lights) for free. The moon is a camera-facing billboard with its painted background color-keyed to alpha; both choices are evidenced by the user's original-game screenshot (round moon at any heading, crater detail → not additive, sky up to the halo with no quad edge → not opaque). The skydome anchor follows the camera in all axes (zero-parallax backdrop) so the moon keeps its designed ~28° elevation against the matching dome-cap color. Verified vs the original screenshot: sky tone, moon look, and star sprinkle match; fly-mode smoke test clean.

**Crash fireball added (2026-07-15):** a crash now spawns the game's own `large_fireball` at the ray's impact point, built from the original data (see `src/Effects/Puffer.cs`): `flame_ball.json`'s `fierypuffer` PUFFER_STATE drives a CPU-simulated billboard-particle burst (18 sprites × 2 bursts over 0.3 s, ±65 m/s random velocity + friction 9, size 2–4 m growing ×3, ~0.9 s life, `fire_f01..06` flipbook), drawn additively as one MultiMesh. Fired in FlightController's crash path (`CrashEffect?.Burst(impact)`), positioned in world space (`TopLevel`, not parented to the hidden airframe), cleared at respawn. Verified via scripted dive + screenshots: compact bright burst on the impact frame → billowed fire ~0.3 s later (grows/spreads on the data timeline, additive glow, no quad edges; the ground plane occludes the below-surface half, as it should) → clean flight after auto-respawn with zero lingering particles. The reusable `Puffer` mapper is the first piece of the surveyed dogfight-effects pipeline.

**Spinning propellers added (2026-07-15):** `--fly` now animates each plane's propeller/rotor instead of a frozen disc (see `PropParts`/`PropAnimator` bullets and the playtest issue "Animation of PlaneModels"). The build hides the static disc and spins the `propN`/`propNb` blur layers about local Z (autogyro overhead `rotorN`/`rotorNb` about local Y), rates from the original's `spinprops`/`agyro_rotors` data, throttle-scaled. The blur discs were **invisible** at first: `rotorblur.tif` is a soft sprite (≤26% alpha, near-black), so the default AlphaScissor cutout erased it — the same bug the clouds had; alpha-blending the `*blur*` textures fixed it (clearly visible against sky, subtly over terrain — a faithful faint see-through prop). This also fixed a latent bug in the static `--plane` viewer (the autogyro used to render its spinning rotor discs *and* the static rotor at once; the uniform classifier now shows only the static disc). Verified: rotation-log runs confirm each disc accumulates about the expected local axis only (props Z, rotor Y) at the data rates × throttle; fly screenshots show the disc against sky; the static viewer shows the still disc; a scripted dive still crashes (no flight/collision regression). `startprops`/`stopprops` spin-up/down choreography is deliberately *not* wired — the plane spawns already spinning (matches the mid-air spawn). One playtest gotcha, now fixed: the flight build initially left `nitropropN` rendering (the skip only hid `staticpropN`), and that non-spinning blur disc overlaid the spinning ones with a 1px seam that shimmered with the camera — misread at first as a texture wrap/filter bug (chased mipmaps, alpha-border bleed, point sampling, all dead ends); the real fix was simply hiding the nitro disc in flight too.

**Z-fighting fixed via polygon draw priority + draw-order tie-breaks + soft-alpha blending (2026-07-15/16):** the map-wide decal flicker (forest/crop patches, roads, rail tracks, building shadows — and plane decals: gyro canopy frames, wing logos) had two data-side causes, both now honored (see SceneBuilder bullet). (1) The per-polygon draw-priority field (`unk04`, since renamed `priority` upstream) layers coplanar decals over base surfaces; each (material, priority) pair becomes its own surface, depth-biased toward the eye by priority × 2e-4 of view distance in a generated shader. (2) Equal priorities resolve by the original's draw order — polygon list order within a mesh (tile-mesh roads/shoreline blends over tile grass) and node order across meshes (airfield apron over base tile) — mapped to a per-surface rank bias + a per-instance `node_bias` instance uniform. Verified by A/B screenshots: the hillside forest patch (`terpat02_trans3` over `terpat03`, node g28030) renders shredded without the bias and coherent with it; airfield terminal apron clean (was mottled); runway/taxiway centerlines and rail ribbons clean; plane painted decals stable; fly-mode smoke tests clean. (The gyro hump "lattice over skin" case originally credited to the within-mesh tie-break was later re-diagnosed as backface culling — see the 2026-07-16 plane-fidelity entry; the tile/node-order validations stand.) Bonus fix from the same investigation: baked shadow decals (`bldgshadow`/`abld_shadow`/`lkshad*`/`thin_shadow`) have soft alpha that AlphaScissor made invisible or solid black — a pixel-based soft-alpha classifier (TextureArchive.LastAlphaIsSoft) now routes them (and clouds/blur/waterfalls/smoke generically) to alpha-blend. Also identified, NOT a bug: the solid-black wedges flanking the rural road bridge are baked-black vertex colors in the data (painted shadow), black in the original engine too. Known remaining nits: runway `lite*`/`ltout*` lights-on/off state-variant quads still tie (engine-side toggle, needs game state); one 6-poly rail-over-transition patch NE of the C1 bridges sits below the tie-break's resolution.

**Data-driven spawns + chapter selection (2026-07-15):** the flight spawn is no longer hardcoded. `--chapter=C1|C1B|C1C|C2|C2B|C3|C4|C5` selects which chapter's world to render/fly (its single `world1`), driving the default gamez + texture archives (replaces the old `--world` flag). `--fly` then spawns from the mission's own data: `--mission=IA1` + `--scenario=zeppelin_run` read `ia.json` `spawn_points` and pick one `[x,y,z,heading]` at random per launch (or `--spawn=N` to force one; the pick is logged); **story missions** (`--mission=M0x`, no ia.json) fall back to `objectives.json` PLAYER_INIT. Spawn throttle/speed match the original's observed start (0.5 throttle, ~120 mph, accelerating). Verified in-game (user, 2026-07-15): C1/IA1 `zeppelin_run` spawns + headings match the original. Verified via logs/screenshots: `--spawn=0/3` positions **byte-identical** to `ia.json` (`(-7066,326,-5519)@-48°`, `(-2431,200,-4001)@52°`); C3/M01 falls back to PLAYER_INIT `(-1426,150,-1813)@40°` (matches the data) and flies airborne over the C3 island world; `--chapter=C2` renders its own map.

**Plane model fidelity fixed: damage panels + backface culling (2026-07-16, user-reported):** three original-vs-remake model discrepancies traced to two causes, both data-side (see PlaneBuilder/SceneBuilder bullets + the new format facts). (1) *Missing airframe parts* (Bloodhawk wingtips, Kestrel outer wing thirds): PlaneBuilder skipped the whole `player_damage_on` group, which contains not just the hidden-until-hit damage panels `pdp1`–`pdp8` but also the HEALTHY panels `pdp2_h`/`pdp3_h` that the game's own reset anim (`player_destruct_reset.json` `plane_reset`) activates at spawn. Now only the digits-suffixed `pdpN`/`pcdpN` are skipped (plus the redundant `player_damage_off` intact-duplicate group). (2) *Autogyro aft fuselage covered in a dark diamond lattice* (original shows yellow skin with openings): the `agyro_cock1` lattice polys in the `tail` mesh are inward-facing, single-sided interior structure — the original backface-culls them; our blanket cull-disable rendered them over the skin. Aircraft now honor the per-polygon SHOW_BACKFACE flag (`unk2`) with `cull_front` (source visible side = CCW = Godot's back face; no mirrored plane transforms exist, verified). Verified via screenshots vs the user's original-game Hoplite shot: gyro rear shows skin + openings, canopy frame intact from the front, Bloodhawk wingtips and Kestrel outer wings (roundels) present, fly-mode smoke test clean. The runtime damage system itself (pdpanelN anims flipping panels on hits, texture-cycle damage skins, part destruction) remains future dogfight-milestone work.

**Wing-light blink — M2-polish item 1 (2026-07-16):** planes' wingtip flares now flash instead of rendering permanently (see `WingLights`/`WingLightBlinker` + PlaneBuilder bullets). PlaneBuilder builds the `wing_flare1`/`wing_flare2` glow quads **hidden** (the original `wing_light.json` `wing_lights_blink` RESET_STATE starts them off) and re-skins each `oil_liteflare.tif` quad as an additive, **camera-facing billboard** tinted the data's warm amber (LIGHT_STATE COLOR 0.88/0.78/0.36) — fixing the old "one-sided quad only visible from behind" bug. In `--fly`, `WingLightBlinker` flashes them for a short window (`FlashDuration` 0.08 s, TUNE) every 1.5 s (`wing_light.json` LOOP SEQUENCE_OFFSET). Additive-billboard treatment is scoped to the flare **nodes** by name (the `oil_liteflare` texture is shared with a few airframe meshes that must not be recentered). Verified: blink interval logged at 1.528/3.003/4.505 s (≈1.5 s); force-on screenshot shows both wingtips glowing amber from a banked chase view; static `--plane` viewer shows no flares; Bloodhawk + autogyro (no `wing_flare` nodes in the data — only Kestrel/Fury/Avenger/Balmoral/Peacemaker have them) get no blinker and fly clean; build clean. The 1.25 m point lights the anim also toggles are skipped (negligible at chase distance). Pending user playtest to fine-tune `FlashDuration`.

**Chase camera rolls with the plane — M2-polish item 2 (2026-07-16):** the chase camera now banks fully with the plane instead of hugging near world-up (see the FlightController bullet). `DesiredCamPos` offsets the camera behind-and-above in the plane's own frame (`camUp = Attitude.Y`, was the near-degenerate `Vector3.Up.Lerp(up, 0.45)` — inverted, the lerp of opposing vectors collapsed toward zero, so the world never flipped); `UpdateChaseCamera` slerps the camera *basis* (`CamRotSmooth` 7 /s, TUNE) toward a `Basis.LookingAt` with the plane's up while keeping the existing position lerp (`CamSmooth` 8 /s), so the horizon rolls smoothly through 360° and **inverted flight shows the world upside down**. A dot-product guard falls back to world-up on the (never-observed) up ∥ view case; `SnapCamera` still sets orientation instantly on (re)spawn. Verified: scripted pure-roll `--hold=0,1,0,0.7` flight, screenshots at successive roll phases show a clean 0°→90°→180°→270° horizon sweep — fully inverted at 180° (sky bottom, ground top), no flip/snap/degeneracy through ±90° or inverted; build clean. Pending user playtest to fine-tune `CamRotSmooth`.

**Weather: distance fog + cloud-band whiteout — M2-polish item 4, part 1 (2026-07-16):** the flown mission's `weather.json` now drives atmosphere (see the `Weather.cs`/SceneBuilder/WorldBuilder/PlaneViewer bullets). A `WeatherState` loader parses per-zone fog + the `CLOUD_COVER` band + wind (the CLOUD_COVER/WIND blocks pair keys with bare scalars, so they're walked as raw pairs, not through `ZrdrDict`). **Distance fog:** SceneBuilder's generated world/aircraft shader mixes ALBEDO toward the zone's `FOG_COLOR` over `FOG_RANGES` (per-pixel, by view-space `length(VERTEX)`), via global shader params PlaneViewer registers + sets once per flight; the camera-anchored skydome opts out per-instance (`csky_fog_on = 0`, else it fogs the whole sky gray at ~22 km), and the aircraft is a no-op at chase distance. **Whiteout:** a full-screen `ColorRect` overlay whose opacity follows `WeatherState.WhiteoutAmount(cameraY)` — a symmetric trapezoid the user worked out from the original: clear at the `CLOUD_COVER` band edges (C1/IA1 970/1124 m), ramping to a fully-opaque near-white core (plane invisible) that is THICKNESS deep and centred on the midpoint (total only in 1032–1062). `THICKNESS` is the opaque-core depth, not an edge transition. `WhiteoutColor` near-white (TUNE, not the 0.69 fog gray) matches `OriginalScreenshots/C1 IA1 whiteout at height.png`. Both also apply in static `--chapter` mode when `--sky-zone` is given, so `--campos` gives deterministic weather shots. Verified via A/B `--campos` screenshots: zone2 fog 1000→4000 m fades distant terrain to gray while near terrain + the plane stay crisp and the dome/sky is unchanged vs a no-fog run; whiteout is absent below 970 m, full inside the band, clears above; `--fly` smoke test clean (spawn/collision/crash-effect intact). Pending user playtest to fine-tune the fog feel + `WhiteoutColor`.

**Weather: cloud deck follows the player — M2-polish item 4, part 2 (2026-07-16):** the opaque `cloudlayer` overcast now tracks the plane instead of sitting static (see the WorldBuilder `CloudDeck`/`IsCloudLayerDeckNode` + PlaneViewer cloud-deck bullets). WorldBuilder splits the 144 cloudlayer tiles (y=960, whole-map) into a `CloudDeck` node; PlaneViewer re-anchors it each frame centred on the camera x/z and pinned to a fixed altitude at the **whiteout-band centre** — you climb toward it as a fixed overcast ceiling, pass through it exactly where the whiteout is fully opaque (transition hidden), and it becomes a floor once you're above the band. cloud1/cloud2 sprites stay world-fixed. This was chosen by user playtest: a fixed-gap-follows-you model and a band-centre-anchor model were prototyped behind a `--deck-ceiling` toggle; the user picked the band-centre anchor, so the fixed-gap model + toggle were dropped. Verified via screenshots: `--fly`/low static camera shows the deck as an overcast ceiling above the plane (matches `OriginalScreenshots/C1 IA1 Cloudcoverage 1.png`); above the band it flips to a floor below with the night dome + fixed sprites above; plain `--chapter` orbit viewing unchanged (deck stays at its data altitude). Only ambient drifting puffs remain of item 4.

**Fog remodeled: cylinder + altitude band + true sRGB gray (2026-07-17, user-diagnosed):** the user reported the cloud deck rendering nearly completely gray and proposed the original fogs by horizontal distance only (a fog cylinder) with `FOG_ALTITUDE` capping it vertically — both confirmed and implemented (see the SceneBuilder/Weather/PlaneViewer bullets). The data corroborates the model exactly (zone1 fog altitude 970→1047 = cloud-band bottom → whiteout-band centre, i.e. fog hands over to the whiteout inside the overcast; zone2's 4000→5000 > ceiling). A second, measured cause of the gray-wash: `FOG_COLOR` 0.69 is a DX7 sRGB framebuffer value — the original's saturated fog is exactly 176 gray (flat regions of `C1 IA1 Cloudcoverage 1.png`, std 0) while feeding 0.69 raw into the linear pipeline rendered 216 near-white; now converted sRGB→linear at the set-site, our saturated fog is byte-exact 176. Verified vs originals: zone1 deck fully texture-crisp via the altitude fade; zone2 deck textured overhead fading to 176 toward its horizon (the original's pattern in `C1 IA1 Cloudcoverage 1.png`); terrain fog wall + ghosted airbase zeppelin match `C1 IA1 Fog Range.png`; `--fly` smoke test clean (plane/near terrain unfogged). Open TUNE: the `fogRangeFactor` 2 halving is kept (user's calibration) but predates the color fix — re-A/B in game; the original's overcast also reads ~40 units darker than our deck texture renders (tone ~165–175 vs our ~206) — a separate deck-tint fidelity question, not fog.

**Forest trees (clutter system) — M2-polish item 5 (2026-07-17):** forest-textured hillsides now grow standing trees, and the rivers their bushes (see the `src/Mech3/Clutter.cs` bullet). Diagnosis: the 28 tree nodes in C1's gamez all hang under `terpat02` — a parentless, unreferenced **clutter template** subtree the boot script (`support\c1\adjust.gw` in interp.json) registers via `AddClutterTemplates`; the original engine stamps each template onto every terrain polygon textured with the template's own ground texture (terpat02.tif = the forest texture; its 512 m quad = the tiling period). The remake tiles each template on a fixed world-space grid of that period across every matching placed polygon (the original's exact alignment is undecoded; the world's UV tiling is too non-uniform to follow) — 9,303 sprites in C1 (4,145 firtree1 + 2,267 dougfirtree1 + 2,580 firtree2 + 311 river bushes), drawn as one Y-billboard MultiMesh per kind (fullbright, scissor cutout, same cylindrical fog as the world) with a crossed-quad trimesh collider (`clutter_col`) in flight. FlightController's crash log now names the hit collider (verifies tree strikes). Verified: hillside screenshots show firs exactly on the forest-textured slopes (bare valleys stay bare), close-up shows upright planted cutouts; scripted level flight across the forest valley flies clean and the crash-name logging works (`CRASH into g314/col` on the far obstacle); static + fly builds run clean (~856 ms world load incl. clutter). **User-confirmed in-game (2026-07-17): crashing into trees works.** Remaining TUNE: density/size eyeball vs the original. C2/C5's 3D block templates (filmblock buildings) are detected and skipped — only flat sprite cards billboard (C2's palm trees will work).

**Flight model reworked: real lift + stall — M2-polish item 6 (2026-07-17):** the user's "no lift — I can slow to 0 mph and the plane just stands in the air" report plus the three item-6 goals, landed as the velocity-vector rework in `src/Flight/FlightModel.cs` (see that bullet for the model). The four behavior changes: (1) **no more hovering** — the lift-deficit path sag runs every frame (the old code disabled all gravity effects below 1 m/s), so a plane bled to ~0 airspeed noses over and falls; (2) **stall pulls the nose toward world-down** (great-circle, attitude-independent, overpowers full elevator at depth) instead of a body-frame pitch bias, and **while stalled the nose cannot be raised over the horizon** at any bank (user-observed original rule, added same day — world-elevation cap at `max(horizon, frame start)`, verified via a stall-into-dive-then-full-pull run: no stalled sample ever raised above 0°, and full-pull zero-throttle no longer loops endlessly but breaks at the top); (3) **knife-edge loses lift** (lift × |up·Y|, and the nose-chase weakens with it — 90° bank sinks at path ≈ −10°); (4) **climb retention** — gravity bleeds speed at ×0.6 climbing, full diving (TUNE, awaiting a measured original climb curve to calibrate). Verification needed sequenced inputs, so `--hold` gained time-segmented syntax (`p,r,y,thr@dur;…`, FlightController `HoldSegments`) and the telemetry line grew `path`/`nose`/`wv` readouts. Verified via scripted runs: vertical zoom climb bleeds 48→12 m/s, noses over +88°→−67°, recovers in the dive (deterministic across respawns); knife-edge (wv 0.06) loses 174 m in 15 s; 11° full-throttle climb settles 122.5 m/s; level cruise regression provably unchanged (wings level makes every new factor exactly 1). **Two same-day user-reported fixes after first playtest:** (1) *no air resistance* — idle level flight never slowed (pure-v² drag dies off below cruise) → the quadratic+linear drag blend (see the FlightModel bullet); (2) *plane stops mid-air with mph creeping up* — the scalar `Speed` clamp at 0 froze a zoomed-out plane in place, then re-integrated speed along a stale upward path → translation reworked to integrate forces on the velocity **vector** so speed passes through zero (tail-slide out of the apex; also deleted the `max(0, nose·dir)` thrust hack and the sag-divisor floor as no longer needed). Re-verified after both: zero-throttle zoom bleeds to ~10 m/s and descends *immediately* through the apex (no hang), noses over, recovers; knife-edge sink and cruise equilibrium unchanged. Pending user playtest to tune `StallNoseRate`/`KnifeAlignFloor`/`ClimbGravityScale`/`LowSpeedDragBlend` against the original. Known accepted arcade artifacts (revisit only if playtest minds): the climb/dive gravity asymmetry pumps energy in a sustained loop, and at full throttle a steep (~53°) climb is a stable equilibrium (thrust ≈ 0.6-scaled gravity) — the plane can climb forever, sailing past the data's 2500 m `flight_ceiling`, which the remake does not model yet (backlog candidate).

**Control-surface animation — M2-polish item 7 (2026-07-17):** ailerons, elevators, and rudders now visibly deflect with stick input in `--fly` (see the `ControlSurfaces.cs`/`ControlSurfaceAnimator.cs` bullets). The data has no deflection anim — the original drives these procedurally — so the classifier maps each plane's surface mesh nodes (which hang under hinge-parent groups that place the hinge line) to a hinge axis (local X ailerons/elevators, local Y rudders, both corroborated by mesh geometry) and the animator poses them absolutely from slewed stick channels (±20°, 3 units/s slew, both TUNE). Signs handle the aileron left/right split, the elevator/rudder against-the-stick convention, the Bloodhawk's nose-mounted **canard** elevators (pull = TE down, detected by hinge z < −1 m) and their yaw-π mounting (frame flip from the accumulated hinge-axis direction). Verified via near-identical-viewpoint A/B screenshots (neutral 1 s → input → shot 0.25 s later): roll-left shows right aileron TE down + left TE up, pull shows the Kestrel's twin-boom tail elevators TE up and the Bloodhawk canards TE down; surface counts Bloodhawk 5 / Kestrel 6 / autogyro 2 (ailerons only, absent kinds tolerated); dive-crash → auto-respawn cycles clean with the animator active; static `--plane` viewer keeps surfaces neutral. Pending user playtest to tune max angles + slew vs original footage.

**Weather: ambient cloud puffs — M2-polish item 4, final sub-item (2026-07-17):** the soft wisps that drift past the plane at altitude are now rendered everywhere on the map (see the `src/Effects/CloudPuffs.cs` bullet + PlaneViewer `SetupWeather`/`_Process`). Diagnosis first: C1 *does* place ~600 `cloud1`/`cloud2` sprite quads, but two far-corner `--campos` checks at band altitude render bare — they are **clustered near the airfield** and world-fixed, so most of the map has no clouds at altitude, and no zrdr defines an ambient emitter. So (per item 4's own "failing that" fallback) `CloudPuffs` is a hand-tuned field: a pool of soft `cloud1`/`cloud2` billboards in a shell around the plane, **anchored in Y to the `CLOUD_COVER` band** (a real cloud layer you climb into then over) but following the plane in X/Z, recycling past-the-plane puffs to the leading edge (endless), drifting with the weather `WIND`, alpha-fading at the shell edge and by vertical distance (clear at low altitude). Drawn as one alpha-blended, camera-billboarded `MultiMeshInstance3D`; `BaseAlpha` kept low (the cloud textures are fairly opaque, mean α≈0.43) so overlaps read as translucent veils not a solid ring. Verified via `--campos` at 850/1120/1250/1500 m over C1/IA1 zone2: absent below the band, a soft layer around/through it, cloud-tops below once above (moon + stars showing through), and present at both airfield-adjacent and empty far-corner locations (the field follows the plane); `--fly` smoke test clean (no regression; puffs correctly absent at the ~325 m spawn). This completes item 4 (weather). Pending user playtest to fine-tune opacity/density against the real in-flight sky.

**Finer plane collision: swept airframe boxes — M2-polish item 8, the last item (2026-07-17):** the crash test now sweeps real airframe shapes instead of a single center ray, so wingtips and tail collide with obstacles like the original (see the `PlaneCollider.cs` + FlightController bullets). `PlaneCollider.Build` derives 3–4 plane-frame boxes (fuselage / wing slab(s) / tail) from the built model's mesh triangles by geometric classification (no per-plane data; nose prop blur discs excluded, the autogyro's rotor disc included as its wing); FlightController sweeps them along each frame's motion via `CastMotion`, crashes at the `GetRestInfo` contact point, and names the struck part in the crash log; the center ray remains as an anti-tunnelling backstop, and `--debug-collision` draws the boxes (green, red frozen at impact). Verified via scripted runs: near-vertical dive still crashes at the terrain surface (part=fuselage, impact 5 m ahead of center; auto-respawn cycles deterministically); a held knife-edge sink (wv 0.22, ~18 s) crashes part=**wing** with the impact 5 m *below* the still-airborne center line — the dropped wingtip catches terrain the old ray flew past; 5 s and 20 s cruise runs show no false crash; debug wireframes track the plane through roll and freeze red at the impact pose. Known accepted limit: the Bloodhawk's short-span canard tips (~0.8 m sliver per side) are uncovered (see the PlaneCollider bullet's measurement). Pending user playtest against building corners.

Remaining known gaps: sky UV scroll animation not implemented (`h_zone*scroll` — rate unknown). Star/beacon dots use a fixed 3 px point size (source params 0.17/30/4000/6000 undecoded) and don't twinkle (the original's stars do). The soft cloud sprites now alpha-blend AND billboard toward the camera (both 2026-07-15; AlphaScissor was binarizing them into hard gray facets, and the flat cards read as angled smears — see the SceneBuilder/WorldBuilder bullets). Weather is mostly in (see the 2026-07-16 status entries below): **distance fog** (weather.json `FOG_COLOR`/`FOG_RANGES`), the **cloud-band whiteout** (`CLOUD_COVER`), and the **cloud deck following the player** (the `cloudlayer` overcast tracks the plane and flips above/below at the band; cloud1/cloud2 sprites stay world-fixed) are done. The last cloud sub-item — **very transparent cloud puffs drift past the plane** (`C1 IA1 Cloud Puffs and Moon.png`) — is now done too (the `CloudPuffs` ambient field, 2026-07-17), so **item 4 (weather) is complete**. Player-plane propellers now spin, and the **prop-start sound is wired** (2026-07-15): (re)spawn plays `snd_propstart` with the engine loop fading in over ~1.8 s instead of snapping to full volume; `snd_propstop` is wired as `OnEngineStop()` but not fired yet (reserved for a future landing/shutdown — the crash explosion covers the crash moment). The *visual* `startprops`/`stopprops` disc crossfade (static disc fading out / blur discs spinning up) is still deliberately deferred — the plane spawns mid-air already moving, so a prop spinning up from zero would look wrong; it stays already-turning. World vehicle animations (trains, zeppelin props) are also still static. The crash fireball is the single `large_fireball` burst only — the full `player_plane_destruct` sequence (sparks, black smokeball, 10 s sustained fire, crash trails, surface-specific dirt/water variants, `plane_destroy_sg` sound) is not yet wired. With rendering, flight, sound, collision, the night skydome, and the crash fireball in place, **Milestone 2's core is essentially complete.** **Next step: continue `docs/plans/PLAN-M2-polish.md`** (agreed with the user 2026-07-16 after a grilling session that pinned down the unclear NOTES.md bullets), in order: 1 wing-light blink ✅ **DONE (2026-07-16)** — flares flash amber every 1.5 s from the data's blink anim; 2 chase camera rolling fully with the plane ✅ **DONE (2026-07-16)** — full bank-follow + slerped basis, inverted flight shows the world upside down; 3 moon size ✅ **DONE (2026-07-16)** — no code change: user re-checked in-game and the moon already matches the original (the earlier mismatch didn't reproduce; the knob if it ever returns is `QuadMesh.Size` in `WorldBuilder.BillboardMoon`); 4 weather (distance fog + CLOUD_COVER whiteout + cloudlayer deck following the player + ambient puffs, all from mission weather.json) ✅ **DONE (fog/whiteout/deck 2026-07-16, fog remodel + ambient puffs 2026-07-17)**; 5 forest trees ✅ **DONE (2026-07-17)** — the interp.json clutter-template system, stamped per matching-textured terrain polygon; tree crashes user-confirmed; 6 flight model ✅ **DONE (2026-07-17)** — velocity-vector rework: real lift (no more 0-mph hover), stall toward world-down, knife-edge sink, climb retention; TUNE constants pending playtest; 7 control-surface animation ✅ **DONE (2026-07-17)** — name-classified hinge nodes deflect with the stick (canard + frame flips handled); angles/slew TUNE pending playtest; 8 finer plane collision ✅ **DONE (2026-07-17)** — swept airframe boxes (fuselage/wing/tail) replace the single ray; wingtip-first ground contact verified, ray kept as backstop. **All eight M2-polish items are done** — what remains on them is playtest-driven TUNE calibration (fog range factor, flight-model constants, control-surface angles, cloud-puff opacity, collision feel vs building corners). Older deferred candidates (PLAYER_INIT speed/throttle semantics, rest of the crash sequence, sky UV scroll, flight_ceiling) remain in the backlog.

**Polishing Run 2 planned (2026-07-17, grilled + agreed with the user): `docs/plans/PLAN-M2-polish-2.md`** — 13 ordered items. Key evidence already pinned during planning: (1) the C4/C5 "errors thrown" are `node_bias` instance uniforms exhausting Godot's default 65536 global buffer (`rendering/limits/global_shader_variables/buffer_size` unset in project.godot; failed allocations also silently drop the z-fight tie-break); (2) C4's blown-white fog is a **FOG_COLOR encoding split** — C1 uses normalized floats (0.69) but C4/C5/C1B/C3 use 0–255 int triples (`[192,192,192]`), which `Weather.cs` feeds raw into `Color` (Zrdr converts all JSON numbers to float via `GetSingle()`); components >1 must be /255, ditto the unparsed `CLOUD_COVER` `TOP_COLOR`/`BOTTOM_COLOR` (C1B/C3 `[32,56,72]` — a lead for the too-bright night deck, along with zone `SUNLIGHT_*` and a tonemap audit; C1/IA1 has no deck-color data); (3) weather.json ends in a **precipitation block**: `TYPE RAIN` (C1C/C2B, `PARTICLES 100`) / `TYPE SNOW` (C4, COLOR/WIND/GRAVITY/ALPHA_GRADIENT); (4) per-mission map state lives in `zepstate.json` (ON_STARTUP OBJECT_ACTIVE_STATEs — C1/IA1 hides `dliner1`, `cargotrain`) + `startanims.json` `NEW_GAME_START` (runs `train_on_track`, `hangar3_doors`, …) with definitions across `mis_anim.json` (has `ANIMATION_PATH` — the train motion) and shared readers; the same reset-state mechanism fixes the healthy+`destroyed` world-object flicker (66 bare `destroyed` subtrees in C1 gamez, plus `ref_tank_dest` etc.); (5) `vehicle.json` `pbloodhawk` `destroyable_parts` = nose/tail/leftwing/rightwing, HP 20 each, all `critical` (tail also `engine`), with `injure_anims` health-fraction → `*_damage_green/yellow/red` (0.72/0.46/0.20) and `pdpanelN` flips (0.5/0.4/0.3/0.15) — the collision-damage data; (6) `pir_spinner.tif`/`barngrill.tif` are referenced by gamez but absent from ALL archives (the warning fix must summarize, not stack-trace); (7) the original **extends map-edge content outward** (user crossed the boundary: terrain continues over terrain, sea over sea) → mirrored border-tile skirt; (8) the user's `rotationTune = 2.0f` (FlightModel.cs) multiplies pitch+yaw+roll and is to be split per-axis and calibrated against their original-game measurements. Grill decisions (full list in the plan footer): full collision-damage system now but **no** flight-handling penalties yet (user researches the original's behavior), crash = breakup + fire/smoke with the full destruct choreography explicitly backlogged, dive sound is **too loud** vs original, deck+sky brightness is ONE calibration item, `docs/formats/` gets seeded as the public format reference.

**Run 2 progress:** item 1 **Log hygiene ✅ DONE (2026-07-17)** — missing-texture warnings report once as plain lines (no `GD.PushWarning` stack traces) + a one-line end-of-build summary, known-absent textures (`pir_spinner`/`barngrill`) render neutral gray not debug-magenta, and the C5 `cblock*` "not a sprite quad" spam collapses to one summary line per template (see the TextureArchive.cs bullet). Verified on C1/C4/C5 `--fly`: zero stack-trace frames per log, C5's ~169 warnings → 7 lines. Item 2 **C4/C5 shader instance-uniform buffer + ground sharpness ✅ DONE (2026-07-17)** — the C4/C5 "Too many instances using shader instance variables" flood was the `node_bias` instance uniform exhausting Godot's default 65536-slot global-shader-variable buffer (16 slots/instance → ~4096-instance cap; measured C1 3481 fit, C4 4234 / C5 4746 overflowed, 143/600 errors + silently-dropped `node_bias`). `project.godot` now sets `buffer_size=262144` (~16384 instance slots); C4/C5 `--fly` log-clean, C1 regression clean, worlds render with decals intact (see the SceneBuilder.cs bullet). The `rtexture*` "hi-res replacement" hypothesis was **disproven** (they are downscaled quality tiers; base `texture` == `rtexture14` = max-res; `rimage` = UI graphics — see the `extracted/` bullet); the real cause of C5's "blurry city ground" is grazing-angle mipmap blur (proof: same terrain sharp top-down, blurry at grazing), fixed with `filter_linear_mipmap_anisotropic` + 16× anisotropic level — A/B on the C5 Manhattan street grid: isotropic smears the receding grid to mush, anisotropic keeps street lines crisp far out (**pending user's fidelity sign-off on the look change; trivially reverted**). Item 3 **C4 white fog — FOG_COLOR integer-RGB schema ✅ DONE (2026-07-18)** — weather.json colour triples are dual-encoded (normalized float `[0.69,0.69,0.69]` in C1/C1B/C3 vs integer 0–255 `[192,192,192]` in C4/C5, plus every `TOP_COLOR`/`BOTTOM_COLOR`); `Zrdr` turns both into floats, so C4's 192 reached `Color(192,192,192)` — saturated, then blown to pure white by PlaneViewer's sRGB→linear (which assumes 0..1) — the blown-white RM fog wall with a hard horizon cut. New `Weather.ParseColor` divides a triple by 255 iff any component is strictly >1, proven unambiguous by scanning **every** weather.json in the install (the only `1.0`-bearing colour is a float sky-fog `[0.80,0.84,1.0]`, max exactly 1 → stays float; no integer colour is all-{0,1}, and `[0,0,0]` is black either way). Verified via deterministic `--chapter --sky-zone --campos` shots + pixel measurement: **C4** fully-fogged region = exactly `(192,192,192)` with a soft mountain→fog ramp (log now prints `fog 0.69`→`0.75 gray`); **C1** zone1 fog = `(176,176,176)` = 0.69·255, byte-identical (the float path isn't scaled). The hard cut was entirely the blown colour — no `FOG_ALTITUDE` follow-up needed (C4's `[10000,11000]` is a correct pure cylinder, full fog at every flyable altitude for RM's valley haze). Also decoded `CLOUD_COVER`'s `TOP_COLOR`/`BOTTOM_COLOR` into `CloudTopColor`/`CloudBottomColor` (integer RGB; unused this milestone — feeds item 6's deck-tint calibration) and seeded the public `docs/formats/weather.md` (schema + the dual-encoding rule). User in-game A/B vs the original RM haze still pending. Item 4 **Clouds render through fog ✅ DONE (2026-07-18)** — the cloud sprites and ambient puffs were the only world geometry lacking the cylindrical fog term (the sprite billboard path used a fog-immune `StandardMaterial3D`; `CloudPuffs` declared `fog_disabled` with no custom fog). Both now carry SceneBuilder's exact fog formula + `csky_fog_*` globals: (a) the cloud-sprite billboard became a `ShaderMaterial` (`GetBillboardShader`, cached per blend/scissor — same hand-rolled keep-scale billboard as CloudPuffs, `COLOR × albedo`, `blend_mix`/`depth_draw_never`, the fog `mix`, **no** `csky_fog_on` instance uniform so it stays off the item-2 buffer); (b) `CloudPuffs`' fragment shader got the fog globals + `mix` (its `fog_disabled` render mode stays — that only kills Godot's built-in fog, not ours). Verified by deterministic stash-based A/B at a camera above the puff layer (so the random-seeded near puffs don't confound the sprites) with pixel measurement by distance band: **C1 zone2** far sprites `196.6→176.0` gray = exactly FOG_COLOR (0.69·255), crisp-white px `35%→0%` (fully absorbed into the fog wall), mid `204→178`, near stays bright `208→195` (24% white) — a smooth near-bright→far-fog gradient matching the terrain; **C4 zone2** far sprites `→192.0` = exactly its FOG_COLOR (192), `0%` white, near `→200`. Zero shader errors; `--fly` C1 smoke clean (spawn/world/2670 colliders intact); no-weather `--chapter` renders clouds crisp (fog globals at no-op default, max 239 / 27% white) — the billboard shader doesn't fog when weather is absent. *Honest scope:* CloudPuffs' visible fog is negligible in the flyable zones — the 620 m puff shell sits inside the fog's 500 m clear near-range (`FOG_RANGES.x`/2), so shell-edge puffs fog ~2% and near puffs are unchanged (`203.4→203.1` same-camera); it is a correctness/consistency fix, the dominant "crisp white against the fog wall" was always the sprites. Moon/skydome untouched. **Next step: item 5 — precipitation (RAIN/SNOW from the weather.json TYPE block).**

Item 5 **Precipitation — rain + snow ✅ DONE (2026-07-18)** — the missions whose weather.json defines falling weather now show it: **C4 Rocky Mountains SNOW, C1C/C2B RAIN** (C1/C5 IA1 have none). *Decode:* the precipitation block is the last thing in weather.json's root dict, a bare-scalar set of top-level siblings after `SHADOW_ANGLES` — `TYPE SNOW|RAIN`, `COLOR [128,128,128]`, `WIND_DIR 0`, `WIND_VEL 0.8`, `GRAVITY` (SNOW 1 / RAIN 3 — rain falls ~3× faster), `ALPHA_GRADIENT [0.5,0]` (peak opacity 0.5), and RAIN-only `PARTICLES 100`. Like CLOUD_COVER/WIND it can't go through `ZrdrDict` (which drops a key's value when it's a bare scalar rather than a list), so `WeatherState.Precip` (`PrecipData`) walks the raw `inner` list with new `StringAfter`/`Vec2After` helpers + the existing scalar/list walkers; the keys are unique at inner's top level (the zone sub-lists' FOG_COLOR/SUNLIGHT_* are nested a level down, which the flat walkers never descend into) and all numbers arrive as `float` via mech3ax's `GetSingle()` (so `PARTICLES 100` = `100.0f`). Full field table in `docs/formats/weather.md`. *Render (`src/Effects/Precipitation.cs`):* one camera-following `MultiMesh` (one draw call) the plane flies through, **fully GPU-driven** — each instance carries a fixed random seed in `INSTANCE_CUSTOM`, and a spatial shader computes its world position from `TIME` + `CAMERA_POSITION_WORLD` (fall + wind drift over TIME, wrapped into a box centred on the camera: `mod(rel + box_half, cell) − box_half`), so the field is world-anchored (correct fly-through parallax), infinite, seamless (dense uniform particles make the per-cell wrap invisible), and costs **zero per-frame CPU** (no `_Process`/`Update` — it plays itself from the built-ins; a world-sized `CustomAabb` keeps it off Godot's frustum-culler since the shader positions everything far from the node origin). SNOW = small camera-billboard flakes with a per-instance flutter (so they don't fall in lockstep); RAIN = thin streak quads billboarded **along the fall direction** (the data's GRAVITY+WIND world velocity, length ∝ fall speed — plane-relative "rain coming at you" streaking is a nicer look but not what the data specifies, so it's a documented TUNE follow-up). Alpha = ALPHA_GRADIENT.x × a **near-fade** (a flake sitting on the chase-cam "lens" fades instead of blowing up into a blob — natural in a cockpit, distracting third-person) × a radial box-edge fade × a **cloud-band gate**. The gate (user-requested mid-implementation — *"should the precipitation not be only under the cloud cover?"*): `1 − smoothstep(CloudBottom, CloudTop, particleY)` so precip only shows **below** the CLOUD_COVER band (it falls from the cloud base — none above the overcast; the first above-deck rain shot had streaks against the clear night sky, physically wrong). COLOR is a DX7 sRGB value → converted sRGB→linear for the unshaded ALBEDO (like FOG_COLOR). Sprites are **procedural** (a soft radial dot / vertical streak generated in-code — the original rendered untextured coloured line primitives, which no texture archive carries; asset-free, no `TextureArchive` dependency). No distance-fog term: the ~30 m box sits entirely inside the fog's 250–500 m clear near-range, so a fog mix would provably never fire. Every data→look scale (fall m/s = GRAVITY×5 TUNE, ~4000 particles from PARTICLES×40, 30 m box, streak length, opacity, all fades) is a marked TUNE constant. Two tuning passes during verification: the first C4 render was too sparse to read as snow + had one giant on-lens blob → raised density (1400→4000) and tightened the box (52→30 m) and added the near-fade. **Verified** via windowed `--fly` and static `--chapter/--sky-zone` screenshots: **C4 snow** — dense visible flakes falling around the plane; **C1C rain** — vertical streaks below the deck over the sea, and **absent above the overcast** after the cloud-band gate (the above-deck spawn now shows clear starry sky, was streaking rain before); **C1 IA1** — clean, no field (no TYPE block); a static-camera jitter-off `--shots=4` burst whose 4 frames all have different md5s (proves the field animates purely from `TIME`); one draw call each, zero shader/script errors, no flight regression. **User in-game A/B still pending** (the TUNE feel; and whether the RM snow reads as the "rain" the user remembers). **Next step: item 6 — night-brightness calibration** (deck + sky too bright vs the original — needs matched original screenshots from the user; test the CLOUD_COVER `TOP_COLOR`/`BOTTOM_COLOR` deck tints, zone `SUNLIGHT_*`, and a tonemap audit).

Item 6 **Night/overcast brightness ✅ DONE (2026-07-18)** — the world reads too bright/washed vs the original; landed **two coupled data-driven fixes**, validated pixel-for-pixel against the user's matched reference `OriginalScreenshots/C1 IA1 Zone1 environment Spawn3.png` (they captured it mid-task via dgVoodoo, plus our render at the same C1/IA1 zone1 spawn 3). *Audit first, ruling out the plan's leads:* the pipeline is clean — world/deck/dome are `Unshaded`, albedo `source_color`, the `Environment` uses the default `Linear` tonemap with no exposure/adjustment, so a white-vertex texel round-trips exactly (the deck's ~206 is faithful reproduction of the 210-gray `cloudlayer.tif`, measured — not a bug); `TOP_COLOR`/`BOTTOM_COLOR` is **absent** in C1/IA1 and `SUNLIGHT_DIFFUSE 1.2` (>1) can't be a naive darkener — both leads dead for C1. *Fix 1 — DX7 gamma-space vertex modulate:* the original is a fixed-function engine whose `D3DTOP_MODULATE` multiplied texture × baked vertex colour in **gamma (sRGB)** space, but we multiply in **linear** space, over-brightening the baked-dark corners (measured: 30% of the C1 world's corners are < 255, min 0). The three fullbright world shaders (SceneBuilder bias `!shaded` + cloud-billboard, Clutter — shared/inlined `csky_srgb_to_linear`) now linearise the vertex COLOR before the multiply, reproducing the gamma-space product; terrain greenness G−R **+3.6 → +16.9** (orig +18…+23), fixing washed-yellow → saturated-green. Planes (shaded) keep raw COLOR. *Fix 2 — data-driven SUNLIGHT world-brightness:* the residual (deck/sky/terrain all ~1.3× too bright) is the mission's **`SUNLIGHT`** (weather.json), which the fullbright pass ignores; `SUNLIGHT_DIFFUSE`/`AMBIENT` vary per mission and track the scene (surveyed across the install: C1B night 0.6/0.15, C1 overcast 1.2/0.25, C1C day 2.0/0.6; the gamez `metadata.json` carries no global light, confirming SUNLIGHT is the source). Averaged over the up-facing world it collapses to `WorldLight = clamp(AMBIENT + DIFFUSE·0.46, 0.15, 1)` — one TUNE (`SunIncidence 0.46`) calibrated to the C1 reference. `Weather.WorldLightFactor` → `ZoneFog.WorldLight`; `PlaneViewer` sets the global `csky_world_light`, **linearised** so the shader's linear-space `ALBEDO ×` lands the dim in gamma space (a raw linear ×0.80 reaches only 210→190; gamma-space lands the deck 210→169), applied before the fog mix so `FOG_COLOR` is exempt. *Match (ours vs orig):* deck/sky **168 vs 169**, terrain field mean **59 vs ~57**, fog band **170 vs 176**. **Self-scales** from the data (verified by log + render): C1/IA1 → 0.80, C1B night → 0.43 (rendered clean), C1C day → clamp 1.0; no shader errors on either chapter/zone. Documented: `docs/formats/weather.md` `SUNLIGHT_*` section + the SceneBuilder/Clutter/Weather/PlaneViewer architecture bullets + the `vertex_colors` gotcha. *Open:* `SunIncidence 0.46` rests on the single overcast reference — a C1B-night and a bright-day original would confirm/refine the constant (the night/day self-scaling is a principled prediction); near-field brightest patch runs a touch hot (86 vs 75), and the dark-plane silhouette is a pre-existing lighting issue unrelated to this item. **Next step: item 7 — map edge continuation** (mirrored border-tile skirt so flying past the world edge shows terrain/sea continuing under fog, not the void).

Item 7 **Map edge continuation ✅ DONE (2026-07-18)** — flying past the world edge showed our void (the original's far clip sat just past the fog wall so it never rendered out there; the remake's 40 km far plane can). *Data confirmed first (Python over the C1 gamez):* the terrain is a **complete 12×12 grid of one-cell (1024 m) ground meshes in world coordinates with identity transforms**, exactly tiling the World node's `area` bounds (x,z ∈ [-12288, 0]); all 144 cells filled (land north, `water1.tif` sea south), every cell also carries a same-size `cloudlayer` deck tile (distinguish by texture), and **every border cell + all 125 border-band tiles are identity/world-coord** — the only irregular ground (the map-centre airfield's offset/split tiles and genuine holes under structures) sits ≥3 cells from any edge. C4/C5 share the grid; C4/C5 outer-edge cells are 100 % covered. *Implementation:* `GameZ` now parses the World `area` + partition-grid dims (new `HasArea`/`Area*`/`PartitionCols/Rows`). `WorldBuilder` gains an `extendBorders` ctor flag; `BuildBorderExtension` classifies the ground tiles (`IsGroundTile` — an Object3d whose own mesh is a single ~0.4–1.35-cell terrain/water sheet, not the equally-one-cell `cloudlayer`/sky, binned to its centroid cell) and mirrors the outermost **`BorderRings`=4** outward: **4 edges** reflect the border band across the one boundary it hugs (`Basis.FromScale` negating that axis, `Transform3D` origin = 2·boundary), **4 corners** reflect across both (a 180° Y turn, det +1). Reflection is **seam-free by construction** — a rigid reflection of the contiguous edge-aligned grid keeps the shared edge heights matching exactly, and bands/corners meet the map and each other **edge-to-edge** (E band z∈[zMin,zMax], SE corner z>zMax, …) so there is no area overlap → no z-fighting and no stepping; the repetition is invisible under the distance fog. Each clone is a **bare ground leaf** (`BuildSubtree(node, skip: n ⇒ !ReferenceEquals(n, node))` builds only the tile's own mesh + collider, never its child subtrees) so no edge buildings/props get mirrored; clones reuse the cached meshes/shapes (cheap), grow **no clutter** (ClutterBuilder walks gamez nodes, not the new Godot nodes), and are collidable exactly when the real world is (the visible ground is never fly-through). `BorderRings`=4 covers C1 zone2's **raw** fog-far (4000 m ≈ 4 tiles) so the void stays hidden regardless of the `fogRangeFactor` TUNE, with ~2 km fly-past margin; the 4 airfield-centre cells that reach the outermost ring on C1's E/S edges simply have no tile (fogged out, ≥3 km — not an outer-edge gap). PlaneViewer builds it with `extendBorders: _fly || _skyZoneExplicit` (on in flight and in static weathered views for edge-verification shots; off for plain `--chapter` orbit viewing — an honest data view with no invented geometry). *Verified* via static `--chapter --sky-zone --campos` screenshots: **C1 sea south edge** (water continues seamlessly to the 176-gray fog wall, boundary invisible), **C1 north + east land edges** (forest terrain continues to the fog, **no trees on the extension**, no cliff), **before/after** (plain `--chapter` shows the terrain cliffing off into the brown void — the extension replaces it with continuous fogged ground), **high-altitude** (uniform fog, no void), and a camera **1.8 km outside the map** standing on extension ground; `--fly` C1 smoke: **+251 colliders**, spawn/world intact, **zero** "Too many instances" errors. **C4** (landlocked): mountains continue into the fog, outer-edge cells 100 % covered (+256 tiles); **C5** (biggest, +357 tiles) fits the item-2 instance-uniform buffer (5080 ≪ 16384). Instance counts: C1 static 3454 → 3709 (+255). Docs: `docs/formats/world-structure.md` + the WorldBuilder/GameZ/PlaneViewer architecture bullets. User in-flight A/B of the crossing feel still nice-to-have.

Item 7 **rework, same day — rolling window of repeated border tiles (✅ FINAL 2026-07-18):** the static 4-ring skirt above was **superseded within hours by user evidence from the original.** (1) The user flew the original past the edge for **10+ minutes** and filmed it (`OriginalScreenshots/Videos/C1 IA1 Tile Loading.mp4`): the original **reloads a tile grid around the plane indefinitely** — the fog wall creeps closer for ~10 s, then the loaded grid re-centres and the visible terrain radius jumps back out (frame-extracted the 2:26–2:36 loop via a pip-installed `imageio-ffmpeg`; the pop sits between 2:32.2 and 2:32.6) — and the continued terrain **carries clutter trees at in-map density**. (2) A user in-game test of pass 1 was decisive about the *content*: our whole-map mirror tiling brought **the airport back** flying east (~every 12 km), which the original never does — *"always the same tiles as in the 10-second video interval"*, i.e. one visual loop = one ~1 km tile crossing at ~135 m/s. Conclusion: the original repeats the **LOCAL BORDER TILE**, not the map. *Implementation (`src/Mech3/MapEdgeExtender.cs`, replacing `WorldBuilder.BuildBorderExtension`):* per axis outside the map, `MirrorAxis` **clamps to the border cell** and repeats it forever, **alternately reflected** by ring parity so every seam is a shared mirror plane — heights match exactly (straight repetition would step), the interior is never referenced (no phantom airport), and the continuation stays type-matched to the local edge (sea → sea forever, forest → forest — the user's original grill observation). The window instantiates all cells within **`Rings`=5** (TUNE — raw fog-far 4000 m ≈ 4 tiles + one margin ring, so the original's visible creep-then-pop artifact never shows, per the user's "we could load one row further") of the camera's cell, minus in-map cells; `Update(cameraPos)` (PlaneViewer `_Process`) no-ops until a 1024 m cell crossing, then diffs ~a window row (freed cells `QueueFree`; new cells build in ~µs off the shared SceneBuilder mesh/shape caches). Each cell = the source cell's ground tiles as **bare leaves** under the mirror transform (no child subtrees ⇒ no buildings) **plus its clutter sprites at mirrored planted points** (new `ClutterBuilder.ExportedKinds`: per-kind sprite mesh + material + world positions; the Y-billboard shader re-faces from the instance origin, so mirroring a sprite = mirroring its position) with a crossed-quad trimesh collider when flying. Prerequisite data verified by recursive scan: **all 144 C1 cells** have ground tiles (147 — the airfield-centre tiles hang one level down under group nodes, caught by recursion; split half-tile strips and the coastal falls overlay bin alongside their cell's base tile), zero rotated/translated ancestors. *Verified:* **120 s and 110 s full-throttle flights across the NE corner diagonal** (double-flip cells): continuous terrain + trees to the fog 3–5 km outside, zero errors, zero unwanted crashes; a scripted dive at +3 km **crashes into extension terrain** — `CRASH into g822/col at (1138,433,−12905)`, outside both boundaries, proving the mirrored-transform trimesh colliders work (incl. det −1 edge bands); statics at the old airport-reappearance spot (row-matched border tiles, **no airport**) and 8 km NE of the corner (rolling forest + trees everywhere); C1 in-map regression + C4/C5 smoke clean, no instance-uniform-buffer errors. Float precision needs no plane recentre (sub-cm past 100 km). *Known nit (pre-existing, backlogged):* in-map beacon point-lights (`rc2_h`/`g1245`) punch through the fog as bright additive dots when seen from far outside — the point-sprite path carries no fog term. *Open fidelity question (user, post-landing):* the alternating reflection is our seam-free construction, not verified original behavior — the user believes the original does NOT mirror (NOTES.md: plain repetition, possibly sharing the map-edge vertex row); an in-game A/B of a recognizable asymmetric border feature settles it, and the swap is one line in `MirrorAxis`. **Next step: item 8 — mission states (anim-state engine pt 1)** (per-mission `zepstate`/`startanims` base object active-states + start-anim end poses; also fixes the destroyed-variant flicker at the C1 airfield).

**Compass heading tape ✅ DONE (2026-07-18, user request outside the Run-2 plan)** — the original's top-center compass strip, live in `--fly` (`src/Flight/CompassTape.cs`, built by PlaneViewer while the chapter texture archive is open, fed the nose heading by FlightController each frame). *Assets:* no compass art exists in `rimage` (that's briefing/menu images) — the **user found the two HUD textures in the chapter texture archives** (`compassticks2` 64×16: one 15° tick segment, tall tick straddling the tile seam (255-core cols 62–63|0–3) + four 3° minors (255 cores, rows 11–15); `compasstxt` 128×32 alpha: cream letter atlas as pre-kerned pairs "NE SE SW NW" at x 1–25/27–51/52–83/84–115, singles cut from the pairs, 5 px white→black gradient block at x 123–127 unused — not visible in the reference HUD). *Model, probe-measured from `OriginalScreenshots/HUD.png` (2556×1440):* the tape is a **cylindrical drum seen edge-on, 180° visible** — a mark Δ° off the heading sits at `center − R·sin(Δ)` (R=127.6 px, every tall tick fits at 15° spacing, minors 3°, labels every 45°), brightness falls off as **cos(Δ)** (ticks and labels; measured tall-at-45° 194 ≈ 255·0.766, labels 207/152/105 at Δ 4.5°/40.5°/49.5°), **headings increase to the LEFT** (W left of SW at heading ~229 — a real whiskey-compass card), bar 263×40 px at y 35 (screen-height-relative scaling), labels billboarded upright (NOT drum-compressed, verified W width = center width), 0.625× atlas scale at bar_top+3, bright tall rim tick capping both bar ends (~192). *Dead ends recorded:* (1) initial "MODULATE2X" gain hypothesis (texture probe hit the tall tick's ~140 side-gradient; the real cores are 255, so the original's 245 peaks are plain cos-fade + its own filtering — the landed tick pass has **no gain**, mid-range matches only without it); (2) default mipmapped filtering **crushes the tile vertically** (isotropic lod follows the ~0.5× horizontal minification while the tile upscales 2.4× vertically → talls 45% of bar height vs the original's 77%) — ticks render **point-sampled, no mips** (the original's hard 1–2 px comb IS the minification aliasing), labels on a bilinear child layer (`LabelLayer`) so the letters stay smooth; (3) full-bar-height tile mapping leaves ticks stubby — the original draws the tile **~25% taller than the bar, bottom-aligned, top-clipped** (`TileOverscan` 1.25 → talls 30 px/77%, minors 17 px/42%, matching the probes). *Verified:* probe tables ours-vs-original (bar rect, tall-top y 45 vs 44, label cream 197,194,140 at fade 1, E 145 vs orig-W 152, rim 168–183 vs 192); heading cross-checked against telemetry motion twice — spawn 0 straight run ΔX+130/ΔZ−117 → 48° = NE centered ✓, and a 6 s turn ending ΔX+16/ΔZ−80 → 11° with N exactly +12 px right of center ✓ (whiskey scroll direction confirmed); 720p + 1440p, window-resize proportional; zero log errors. *Open:* **world-north convention assumed = −Z** (heading = `atan2(nose.X, −nose.Z)`; consistent with the map/motion but the original's own north is unverified — if an in-game check disagrees the flip is one line in FlightController), and the drum constants `TileOverscan` 1.25 / `RimGain` 1.5 / nearest-filter tick look are TUNE pending the user's in-game A/B.

Item 8 **Mission states — anim-state engine part 1 ✅ DONE (2026-07-18)** — every world build now applies the mission's animation base/start states, so each mission shows the world state the original shows: the right zeppelins/trains present, hangars posed, and the destroyed-over-healthy coplanar flicker gone. *Decode (schema in `docs/formats/anim-definitions.md`):* ANIMATION_DEFINITIONs live in three zrdr scopes (shared + chapter + mission — the original compiles the same sources into the mech3ax-unsupported `mis_anim.zbd`, so scanning the JSON readers is a full substitute); a def = NAME (anchor node, wildcards `*`/`**` any run + `#` digits, `.flt` suffix optional — `ap_radiotwr` ↔ node `ap_radiotwr.flt`) / ANIMATION_NAME (what startanims/CALL_ANIMATION reference) / ANIMATION_ROOT_NAME + LOCAL_NODES_ONLY (per-instance templates: `s_build**`/`ftank0*` anchor one anim per matching building, op names resolve inside each instance) / ACTIVATION (ON_STARTUP runs at load) / RESET_STATE (the base state: healthy ACTIVE, destroyed INACTIVE, doors at rest) / SEQUENCE_DEFINITIONs. Key duplicate-keys gotcha: defs repeat SEQUENCE_DEFINITION and op keys meaningfully, so `ZrdrDict` (which collapses duplicates) cannot parse them — `AnimDefs` walks the raw lists. *Implementation:* `src/Mech3/AnimDefs.cs` (loader, four state-op kinds parsed — active/translate/rotate/motion-end-pose — playback ops recorded + skipped) + `src/Mech3/MissionState.cs` (applier): (1) anchored defs' RESET_STATEs, (2) ON_STARTUP sequences (zepstate.json — C1/IA1 deactivates `dliner1` + `cargotrain`), (3) startanims.json NEW_GAME_START end-states in list order (C1: `hangar3_doors` slides the four `h3_dr*` doors ±50 then `mp_hangar3_open` ±25 — last wins; offsets from the authored rest pose), (4) a safety net hiding still-visible `*destroyed*` subtrees (C1: 53 — per-object vehicle/balloon defs that part 1 correctly doesn't anchor). *Same-day fix (user-reported):* the net initially also matched a `*_dest` suffix, whose ONLY match — C1's `ref_tank_dest` — turned out to be the parent GROUP of the five healthy harbor `refuel*` tanks ("destructible", not "destroyed"; the `ref_fueltanks.json` def had already set the per-tank states correctly inside it), so the zeppelin-run harbor's visible oil tanks vanished; rule removed, tanks verified back against `OriginalScreenshots/C1 IA1 Harbour zeppelin-run.png`.

**Point-light glow sprites ✅ DONE (2026-07-18, user feedback during the item-8 A/B)** — the world's point lights (yellow refinery tarmac lamps, blue pier lights, the lighthouse, tower beacons, the night sky's stars) rendered as fixed 3 px additive dots; the user reports the original draws them as **camera-oriented sprites with a soft star-like alpha falloff, clearly brighter**. *Decode first (C1 value survey over every mesh-light entry — fields previously "0.17/30/4000/6000 undecoded"):* light `unk08` = **size scale** (0 default / 1 / 2 red beacons / 5 the lighthouse), `unk64` = **max sprite size in px** (30 wherever set), `unk68` (else `unk52`) = **visibility range in metres** (4000 stars+lamps, 2500/1500 others); colored population: 64× gray-white `(218.7)³` stars, 4× orange-yellow `(255,170,0)` tarmac lamps, bluish-white `(215,205,255)` pier lights, one `(255,69,69)` size-5 lighthouse (`flags`/`unk48`/the horizon 0.17 float stay undecoded — backlog). *Implementation:* `GameZLight` gains `SizeScale`/`MaxSizePx`/`Range`; SceneBuilder's `GetLightPoints` grouped per (size, range) with a dedicated point-sprite spatial shader (`blend_add`, `depth_draw_never`): POINT_SIZE = a ~6 m world diameter (TUNE) projected to pixels, clamped [3, `unk64`]; fragment = gaussian hot-core glow with no hard cutoff, ×1.6 brightness gain (TUNE); the data range applied as a smoothstep **distance fade — which also closes the item-7 nit** (beacons no longer punch through the fog from km outside; the 4000 m reach ≈ the raw fog-far). The camera-anchored skydome's stars sit ~22 km out — `WorldBuilder.DisableLightRangeFade` exempts them via a `csky_light_fade` instance uniform (the cost is a handful of instance-uniform slots, far under the item-2 buffer). *Verified by screenshots:* harbor close-up — lamps are bright warm 4-point glows capped at the data's 30 px, blue pier lights clearly visible on the piers (both matching the user's description vs `C1 IA1 Harbour zeppelin-run.png`); the lighthouse lamp reads as a big glow from 300 m (size-5 × data cap); night sky above the deck shows stars + moon intact (range-fade exemption works); `--fly` C1 smoke clean, zero shader errors. *TUNE pending user A/B:* the 6 m base diameter, ×1.6 gain, gaussian falloff shape vs the original's sprite texture, and the 3 px star floor.

**Flare-quad billboards — the lamps' real mechanism ✅ DONE (2026-07-18, follow-up on the user's second report)** — the point-sprite rework above missed the actual complaint: side-by-side crops showed our lamp as a **skewed static quad** (the flare texture's ray tips at its corners) vs the original's big camera-facing star sprite. The visible lamps are not mesh-lights at all but **single-quad flare MESHES** the original camera-billboards: `refinery_flare` ×6 (16 m, `oil_liteflare.tif`), `gen_flare_yellow` ×22 (4 m, same tex — the tarmac lamp rows), `docklight_flare` ×6 (9.6 m, `dock_liteflare.tif` — the blue pier lights), lighthouse `litehsflare` (19.2 m, `poleflare.tif`), `bflare` (`beflare5.tif`); the chapter light readers (`st_light`/`dock_light` anims: `reflight*`/`docklight*` defs) activate the flare child + attach the dynamic LIGHT_STATE. All flare textures carry full soft-alpha ramps on black (measured). *Implementation:* SceneBuilder gains a `glowTexture` predicate (WorldBuilder passes `IsFlareTexture` = contains "flare"); qualifying meshes take the existing keep-scale billboard shader with a new `glow` variant bit — always alpha-blend (scissor would cut the ramp to a hard star), cylindrical fog kept, **no `csky_world_light` night dim** (lamps are light sources). *Scoped tightly after a C1–C5 mesh scan:* only **single-polygon all-flare meshes** billboard (`IsGlowSpriteMesh`), and the collision exemption mirrors it — the same textures appear on polys *inside* regular geometry (C4 world Brigand wings carry an `oil_liteflare` poly; the shared 76-poly effects atlas) where whole-mesh recentering would distort, and 6-poly `flare_green` strings (22–72 m) would swing around a shared centroid if billboarded whole; both keep static rendering (**per-poly billboard pivots = the faithful upgrade, backlog**). *Verified:* harbor close-up + wide shots — refinery lamps and blue pier lights render as camera-facing 4-ray star sprites matching the user's original crop; C1 colliders 2670→2566 (104 flare quads no longer solid); C1/C4/C5 `--fly` smokes clean, zero errors. *TUNE/open:* brightness/size ride the texture as-authored now; the multi-poly flare strings and the `lens_flash`-style prototypes stay static; user in-game A/B pending. INACTIVE = hidden **and** colliders disabled (no crashing into invisible zeppelins). Node lookup by ORIGINAL gamez name via a new `cs_name` meta SceneBuilder stamps (Godot sanitizes and auto-renames duplicate siblings — 9 `lifeballoon`s). *Blowup fixed during verification:* the first anchor pass applied **1.58 M ops** (21.6 s load) — ~50 zeppelin-part defs use the multi-target `NAME1` form (parse to empty NAME) but declare `ANIMATION_ROOT_NAME healthy`/`gunback`, and the root→parent anchor lift matched all **217** bare `healthy` nodes each. Fix: empty-NAME defs never anchor (part-2 per-object scope) + the lift is capped at `MaxRootLift` 16 root matches (legit templates lift ≤ 9; generic roots mark per-object anims) → 3,937 ops, 122 unresolved, world load 1.4 s. *Verified:* stash-based before/after screenshots — airfield building's dark crumpled destroyed-mesh overlay gone (clean checkered hangar), the white `dliner1` zeppelin no longer pokes out of its shed (empty interior girders, as the original's IA1), hangar-3 front doors closed→open (a right-side protruding slab proved pre-existing gamez geometry, identical in the before shot); `--fly` C1 smoke clean (2670 colliders, spawn OK, zero errors); C4/C5/C1C/C2 fly-smokes clean with sensible per-chapter states (C4: 391 ops + 1 net-hide; C5: 752 + 1; C2: 482 + 18) and zero errors. `train_on_track` analysis: it animates the *passenger* train (`passenger_trengine` — sound node + steam PUFFER_STATE + SI-script spline motion, all part-2 playback), so `cargotrain` correctly stays INACTIVE and no train renders parked in part 1. *Scenario→state analysis (negative, closes the item's open question):* scenario names (`zeppelin_run`, …) appear only in `ia.json` spawn lists — world state is per-mission, identical across scenarios; `zeppelins.json` is gameplay config for the always-present IA zeppelin. *Open:* user in-game A/B (zeppelin/train roster per mission + a full fly-over for anything legitimately missing); part 2 upgrades door poses to motion, runs the trains, and owns the per-object (NAME1/generic-root) defs.

**Item 9 (animated vehicles, anim-state pt 2) surveyed and DEFERRED (2026-07-18):** the implementation survey found the plan's premise partly wrong: no waypoint-path op exists in the readers (`mis_anim.json`'s `ANIMATION_PATH` key is a source *directory*), and the train's `train_on_track` motion is `OBJECT_MOTION_SI_SCRIPT` whose `.zan` spline scripts exist **only compiled inside the chapter's `cam_anim.zbd`** — no loose `.zan` anywhere in the install, and mech3ax has no CS support for that archive. Direct binary analysis (scratchpad, read-only) decoded the container far enough to prove feasibility and record a resume point: `cam_anim.zbd`/`mis_anim.zbd` are the MW3 `anim.zbd` family (same 0x08170616 signature, version 53 vs MW3's 39); a mission's archive compiles only its `mis_anim.json`-listed defs (C1/IA1: zeps only) while the chapter archive holds chapter+shared anims including all 48 C1 SI scripts; the SI-script pool records (`path\0 objname\0` + flag-driven frames: `{flags 1|2|4, start, end}` + 19-float blocks) parse byte-exactly for 24/48 scripts **including all four train cars and both fueltrucks**, with the translate block's per-axis cubics (`value + c1·t + c2·t² + c3·t³`) verified exact frame-to-frame (position match + derivative continuity); the train = 4 × 90 frames × ~3.64 s ≈ 327 s per lap over a ~3.9 × 2.2 km loop starting at the parked consist. Undecoded: rotate-block semantics (unit quaternion + 15 floats), AnimDef record internals, the camera/`cpilot_eject` script variant. **User decision: defer the item until the data is extractable via a mech3ax extension** (upstream HEAD has meanwhile dropped CS gamez support — the future fork must handle that); everything else playback needs is already in the readers (C1 car/truck `OBJECT_MOTION_FROM_TO` chains, hangar-door motions, the train's inline steam `PUFFER_STATE`). Findings documented in `docs/formats/anim-definitions.md` § "Compiled anim archives"; plan checklist marked ⏸; next item: 10 (collision damage model).

**Collision damage model — Run-2 item 10, all four stages (2026-07-19):** the full collision-driven damage system (weapons and flight-handling penalties stay out per the grill scope). *10a collider refit:* the planned spanwise vertex-gap split was a dead end (the Bloodhawk's aft region has no vertex gap — its 11.6×2.4×3.1 m tail "barn door" was the thin swept-wing trailing edge AABB'd together with the tall center fins), replaced by clip-based regions (triangles Sutherland–Hodgman-cut at the region planes; "any corner qualifies" classification bloated the Balmoral to a 26 m box, and partitioning raw vertices lost the surface between wing ribs) + greedy volume-guided refinement evaluating one or two parallel cut planes per axis — the double cut is what separates bilateral pairs (slicing one Kestrel twin fin off alone gains nothing while the remainder holds the other fin's height). Result: Bloodhawk tail 86→~12 m³ as slim fin boxes + flat outboard strips, wings split left/right; Kestrel between-fins pocket free air; Peacemaker biplane stack → thin per-wing slabs; fleet-wide 5–8 hugging boxes, debug wireframes verified, item-8 knife-edge wing-catch regression intact. *10b part HP + severity:* `destroyable_parts` → `PlaneDamage`; impact speed along the contact normal (rest query deepened 5 cm past the just-touching pose — at exactly cast[1] GetRestInfo often returned empty and the head-on fallback normal crashed shallow grazes) decides crash (≥25 m/s TUNE) vs graze (quadratic damage, reposition + 0.15 m push-out, velocity deflected along the surface with tangential loss, lever-arm attitude kick, 0.3 s cooldown); trees soft (2.5 HP, ×0.92 speed, never a direct crash); dead critical part = destroyed. Two same-day user-reported rules: ground-stop (slid below 12 m/s = wreck, not a parked plane collecting zero-damage kisses) and un-embed (push out along the normal ≤3×0.3 m or explode — the observed glitch-through can't persist). Verified scripted (deterministic graze slides into a named bridge crash, steep-vs-shallow severity, respawn HP reset) + user-flown (both part-death crash paths, 7-tree forest plows). *10c visible damage:* flight builds construct pdpN panels hidden; `DamageVisuals` flips them at the data's injure_anims thresholds (+ hides the _h twins), streams a firepuffer trail from every flipped panel (the original's per-panel short_firetrail — decisive evidence in the user's mid-implementation reference video `OriginalScreenshots/Videos/C1 IA1 Crash.mp4`: a graze survivor flying on with four discrete-puff fire trails), and starts the nose dense_firetrail smoke/fire pair when any part ≤0.10 (documented interpretation — total-HP could never fire before a critical part died at 75% total). Puffer grew DISTANCE_INTERVAL trail emission, static TEXTURES pools, COLORS age-ramps (ramp ⇒ mix-blend — black smoke is invisible additively), a quad-rim fade (fire_f01 leaks border pixels up to 247 → faint additive rectangles), and soft particles (depth-texture fade — billboards tilted by the high chase camera dipped into terrain/walls and the depth test cut them with hard straight lines, diagnosed from crash screenshots). Panel-flip/smoke look pending the user's next flight (normal play reaches the states easily; scripted holds could not hit the narrow HP windows deterministically). *10d crash breakup:* `PlaneBuilder.BuildDestroyed` + `CrashBreakup` — the destroyed-subtree pieces (Bloodhawk: 4) scatter at the crash pose with impact-derived velocities, hand-simulated ballistic + tumble + down-ray ground rest, persisting until respawn; the wreck burns ~10 s (player_plane_destruct's fire_n_smoke + a rising black_smoke column) on top of the fireball + explosion. Verified: crash screenshots show the burning wreck with scattered pieces feathering softly into terrain and a barn wall; cycles deterministic, zero errors, static viewer untouched. Video-informed backlog: graze dust burst, burning debris arcs, full destruct choreography scale. New format page `docs/formats/vehicle.md` (destroyable_parts / injure_anims / the 6-point collision block).

**Damage lab in the static plane viewer (2026-07-19, user request — item-10 tuning aid):** `--plane` + `--damage[=part:frac,…]` builds the item-10b/c damage pipeline into the orbit viewer: one HP slider per vehicle.json destroyable part (nose/tail/leftwing/rightwing) drives the same `DamageVisuals` flight uses — dragging below an `injure_anims` threshold flips the `pdpanelN` torn-skin panel and lights its panel fire, ≤ 10 % on any part starts the nose dense_firetrail smoke/fire pair; raising the slider back **repairs** (the new `DamageLab` re-derives every visual whenever the set of crossed thresholds changes — flight's one-way `_applied` model stays untouched, and rebuilding only on set changes keeps a drag from restarting the fires at every pixel of travel). The parked plane never moves, so trails would emit nothing — `Puffer.TrailBurnAt` spends a virtual `StaticBurnSpeed` 15 m/s (TUNE, `DamageVisuals.UpdateStatic`) of motion in place per emit point, and the DISTANCE_INTERVAL trails read as flickering panel fires (the puffs' own ±0.8 m/s random velocity + growth do the rest). `PlaneBuilder` grew a `damagePanels` ctor flag (builds the hidden `pdpN` panels with static props); puffer construction is a shared `PlaneViewer.MakePuffer` (the fly path refactored onto it — behavior unchanged; the lab's panel-trail pool is 8 = every pdp panel, vs flight's 4); threshold readouts under each slider print the part's data (`72 green · 50 p5 · 46 yellow · …`; pN = the wired pdpanel flip, green/yellow/red = the unwired cockpit cycle), "repair all" resets, **H hides the UI** for clean F12 shots, and `--damage=leftwing:0.25,nose:40` presets the sliders so `--screenshot` captures damage states deterministically. Verified by screenshots: `--damage=leftwing:0.45` flips pdpanel5 with its fire burning at the panel; `rightwing:0.05,nose:0.14` flips p6/p1/p2 + p7 with fires plus the dark nose smoke pair; plain `--plane` build unchanged (20 mesh instances, no panels); `--fly` smoke test clean after the MakePuffer refactor (damage visuals/crash effect/breakup all built as before). Same-day follow-up: `RunDev.ps1` grew a **damage flow** — `.\RunDev.ps1 --damage` prompts for the plane (the same roster menu as the fly flow, factored into `Select-Plane`) and launches the static lab without adding `--fly`/`--chapter`; an explicit `--fly`/`--chapter` alongside `--damage` passes through verbatim instead (the viewer's own ignore-note explains). Verified via piped-stdin runs: `--damage` + choice 8 → Kestrel lab, no chapter prompt; bare `--plane=` stays promptless.

**Wrong-wing damage fix: healthy-skin pairing by position (2026-07-19, user bug report):** the user's damage-lab screenshots showed bloodhawk/firebrand/brigand tearing panels on the damaged wing but amputating the OTHER wing's tip. Diagnosis: the torn `pdpN` panels sit on the correct side on every plane (vehicle.json leftwing={pdp3,4,5}/rightwing={pdp1,2,6} matches the model geometry fleet-wide), but the healthy `pdpN_h` twin meshes are **cross-named on exactly those three models** (firebrand `pdp3_h` = the right-tip skin = torn `pdp1`'s exact footprint while `pdp5`'s left-tip twin is named `pdp2_h`; bloodhawk `pdp3_h`↔`pdp2`, `pdp2_h`↔`pdp5`; brigand's two `_h` tip strips have no torn counterpart at all, and latently the balmoral's `pdp2_h` is a tail-fin panel its name rule holed on rightwing damage while the autogyro's `pdp2_h` mirrors `pdp2` across the centerline) — measured from planes nodes.json+meshes.json mesh-AABB centers across all 11 player planes (offline script; the `_h` nodes bake placement into mesh space with identity transforms, so node origins locate nothing). The original's own data never hides `_h` nodes (the pdpanelN anims only ACTIVE pdpN; grep-verified no `_h` reference in any zrdr anim), yet `player_destruct_reset.json` re-ACTIVEs `pdp2_h`/`pdp3_h` — so the original engine hides healthy skins at damage time by an engine-side rule; if that rule is the name convention, **the original game glitches these three planes the same way** (open fidelity question the user can test in the original). Fix in `DamageVisuals`: `PairHealthySkins` pairs each `_h` node to the nearest torn panel by merged mesh-AABB center in the plane-root frame (real twins ≤ 0.72 m, crossed/unrelated ≥ 1.36 m → `MaxPairDistance` 1.0; opposite-X candidates beyond ±0.15 m rejected as mirrored), unpaired skins never hide, anomalies logged at build; ctor takes the plane root (both PlaneViewer call sites). Verified: top-down damage-lab screenshots (`--yaw=0 --pitch=1.45`) of all three culprits show torn panels + fires + amputated tip on the slider's own wing with the other tip intact, kestrel control unchanged; balmoral/autogyro pairing logs match the offline predictions (fin + mirrored sliver now never hidden); build clean, zero errors in all runs.

**Dive sound tuned down — Run-2 item 11 (2026-07-19):** the overspeed whine now mixes ~18 dB quieter, from a spectral analysis of the user's reference video `OriginalScreenshots/Videos/Bloodhawk Dive Sound.mp4` (18.8 s: climb through the C1 overcast, near-vertical dive, pull-out; audio + HUD frames analyzed with ffmpeg/numpy/scipy template matching against the extracted WAVs — scripts/figures stay in the session scratchpad, not the repo, per the no-assets rule). **Flight state from the HUD, not the audio:** the MPH dial reads ~300 mph level (exactly fd_speed 135 m/s) and sweeps deep into its red overspeed arc to ~375–385 mph through the dive → speedFrac ≈ 1.25–1.28, squarely in our curves' saturation zone (whine vol cap 0.5, pitch 1.25). At that state the recording contains **no trace of `engine_whine.wav`**: its second-loudest harmonic (H5 — would sit isolated near 1637 Hz at pitch 1.25) is absent (amplitude bound ≤ ~0.06 of the engine's), and independently the WAV's strong broadband hiss never lifts the flat 2.5–6 kHz band (bound ≤ ~0.14). We played the whine at the curve cap 0.5 = −6 dB vs the engine — 12–18 dB hot; the reader's `prop_sound` volume scale evidently is not a linear mix amplitude in the original engine. Fix: `FlightAudio.WhineMixGain` 0.12 (TUNE) — saturated whine now ~−24 dB under the engine, at the measured bound. Rattle left on its data curve: `plane_shake.wav` is inherently quiet (file RMS 0.09 vs engine 0.35; even full volume adds only ~+0.3 dB in the low band, below the recording's measurement floor — the video can neither confirm nor bound it). Diagnosis notes recorded for later: (a) the extra tonal stacks that confused matching are **the original playing the engine as a ~5%-detuned dual stack** (chorus in their playback; ours is a single loop — fidelity nit, backlog) plus **Doppler-shifted engines of nearby IA aircraft** (fury/hellhound-family combs at pitch 1.16–1.37 as the player dives toward the furball); (b) the original's **engine pitch drops ~12% through the dive** and overshoots ~1.05 at pull-out — impossible under the throttle-only pitch curve (cap 1.0), mechanism open (mid-dive throttle cut + camera Doppler vs a speed/RPM term) — settle with a controlled full-throttle-dive video when it matters; (c) an early comb match at p≈0.715 (suggesting speedFrac ≈ 1.02 and an overspeed-drag misdiagnosis) was overturned by the HUD-gauge evidence — the gauge carries the flight state, the combs belonged to (a); (d) **item-12 datum:** the original's near-vertical Bloodhawk dive tops out ≈ 1.27 × fd_speed; our scripted short dive from spawn altitude reached 1.04 before terrain — measure a full-altitude dive when calibrating. Verified: build clean; scripted `--fly --hold` climb–push–dive run exercises the audio path with zero errors (crash + respawn cycle intact). The in-game feel is **pending the user's A/B dive** (same full-throttle dive in both games), per the plan.

**HUD gauges: altimeter, speedometer, damage display (2026-07-19, user request):** the original's three main cockpit dials as a screen-space HUD in `--fly` and the damage lab (`src/Flight/GaugeCluster.cs`; format write-up in `docs/formats/hud.md`). **Resource discovery answering the user's questions:** the dials are not composed screen-side — each player plane carries a `gauges` subtree under its (skipped) cockpit in planes.zbd whose flat meshes ARE the 2D dials in dial-local coords (bezel radius 1): the face is a 12-gon mapping `altimeter`/`speedometer`/`<plane>_damage`.tif (textures ship in every chapter's texture.zbd), **each needle is a single slim textured quad — the taper and hub are painted in needle.tif (32×128), not meshed** (pivot at origin, tip +y; altimeter has long `hundreds` + shorter `thousands`, speedometer `speed`); the `lowalt_on`/`stallwarning_on` warning meshes hold the lit window quad plus two red bezel slashes; and the damage dial's four `*damage` child meshes each carry a bezel border bar (`greenhilite`) plus a part-shaped tiled hatch fill (`grn_hatchptrn`) traced to that plane's silhouette — **part positions are per-plane mesh data**, and the interp `cockpit.gw` script recolors by 4-map texture cycles (green/yellow/orange/red `*hilite`/`*_hatchptrn` — the remake uses 3, per the reference shots). The remake extracts all of it per plane at build time (nothing hand-modeled) and draws in the data's priority order (STALL under the needle, needles over LOW ALT). Wiring: altimeter long/short needles 360°/1k/10k ft; speedometer 0.72°/mph (measured off the face labels); LOW ALT blinks below **300 m over ground** (user spec; FlightController down-ray per physics frame); STALL blinks from `isStalled()`; damage zones color at the part injure_anims thresholds (yellow ≤0.46, red ≤0.20) with fractions from PlaneDamage, and a hit zone (fill + border) **blinks 5 s even in green range** (user-observed original behavior) via `OnPartDamage` beside the DamageVisuals call. **Damage lab integration (user follow-up):** the lab builds the cluster too — sliders drive the dial live, decreases trigger the blink, presets end in a blink `Reset()` (deterministic `--screenshot` shots), a "HUD gauges" CheckButton toggles it. Screen metrics: dark-span scans of HUD.png's bezel rings put all three dials at R 85 @1440p — alt (425.5,1108.5), dmg (426.5,1299), spd mirrored 420 from the right (the two reference screenshots differ slightly in placement; HUD.png canonical). One diagnosed dead end: color-keying needle.tif's black erased the hub's two black discs — the original draws the quad opaque (it is slim; the dark tail region IS the hub art). Verified: 1440p side-by-side crops vs HUD.png (size, position, needles + hub, LOW ALT + red slashes match); lab preset `leftwing:0.25,nose:0.1,tail:0.55` renders yellow/red/green fills + matching border arcs; the pre-Reset preset shot caught zones mid-blink (blink path proven); C1 fly smoke test clean. Pending user playtest: blink rates (`WarnBlinkPeriod` 0.4 s / `DamageBlinkPeriod` 0.32 s TUNE), whether the original ever shows the cycle's orange state, exact gauge placement.

**HUD gauges follow-up: pointed needles + LOW ALT 50 m (2026-07-19, user feedback):** two same-day tweaks from the user's zoomed-original comparison. (1) **LOW ALT threshold 300 → 50 m AGL.** (2) **Needle shaping:** the first pass drew the needle quads with the raw texture, giving square tips — full row sampling proved needle.tif's shaft is a flat full-width slab with no taper at all, so the original's slim pointed lance is an engine-side shape (in neither the texture colors nor the mesh/UVs). `GaugeCluster.FindGaugeTexture` now shapes the texture at load: shaft rows get an alpha mask tapering linearly to a point at the tip (`NeedleMaxHalfFrac` 0.65 / `NeedleTaperEndFrac` 0.9 of the shaft, TUNE, eyeballed against the user's zoom), the darker center notch is filled with slab color (it survived the taper as a split "tweezer" tip), hub rows untouched. Telemetry grew an `agl=` readout (the down-ray's height over ground). Verified: 1440p altimeter crop shows both needles as solid pointed lances with intact hubs; a false "LOW ALT stuck on" scare was disproven by pixel values — the face textures contain dark UNLIT window copies that read deceptively lit in upscaled crops (~58,0,0 vs the lit overlay's 180+,0,0; second time bitten, gotcha recorded in the architecture bullet), with `agl=145` > 50 confirming the wiring.

**Damage display: orange state confirmed + wired (2026-07-19, user in-game check):** the user confirmed the original damage display DOES use the cycle's 4th color — the remake now steps through all four: green > 0.72, yellow <= 0.72, orange <= 0.46, red <= 0.20. This resolves the threshold-mapping ambiguity: the data has three *_damage_* anim thresholds and four cycle maps, so each anim steps to the NEXT color (the anim names lag their effect by one state — *_damage_green at 0.72 turns the zone yellow); red on a still-flying plane matches the "HUD with dmg" reference, and the lab's old yellow-at-25% is exactly where the original shows orange. GaugeCluster: 4-entry hilite/hatchptrn variant arrays (orangehilite/orng_hatchptrn resolve from the chapter archive), per-zone YellowAt/OrangeAt/RedAt parsed from the part anims. Verified: lab preset leftwing:0.25,nose:0.1,tail:0.55 renders orange/red/yellow fills + matching border arcs, rightwing green.

**`docs/formats/` public reference complete — Run-2 item 13 (2026-07-19, pulled ahead of item 12 by user decision):** the XWVM-model deliverable is now a real, self-contained reference: 11 pages + `README.md` index. New `README.md` carries the page index and the shared zrdr reader conventions in one place (everything-arrives-as-float, alternating key/list dicts + the meaningful-duplicate-keys caveat, bare-scalar blocks, the dual colour-triple ÷255 rule, `kind_of` inheritance, name wildcards, meter/second/degree units, and the extraction map incl. the rtexture/rimage tier findings). The coarse `zrdr.md` migration page was split into per-family pages: `spawns.md` (ia.json `spawn_points` + objectives.json `PLAYER_INIT` with the fields-[3]/[4]-are-not-spawn-state caveat, plus the campaign mission↔folder map), `sounds.md` (SETS entry schema with flags/RANGE/VOLUME, the player.json curve blocks including the item-11 finding that the curve volume is not a linear mix amplitude, and the MS-ADPCM WAV note), `effects.md` (the full `PUFFER_STATE` field table as parsed by Puffer.cs — burst vs trail, TEXTURE_SEQUENCE vs TEXTURES, COLORS ramps — plus the three-layer effects survey, flipbook texture families and gamez anchor nodes), and `clutter.md` (interp.json boot scripts, `AddClutterTemplates`, the template subtree shape with ground-quad texture/period semantics, the undecoded original-alignment question, and the C2/C5 non-sprite city-block caveat). `zrdr.md` itself was repurposed as the archive overview (the three scopes shared/chapter/mission + a reader-family → page index) and `vehicle.md` absorbed the remaining flight-stats detail (units, the dynamics steady-rate formula torque·recInertia/damp, engines.json rows) with its puffer section now pointing at `effects.md`. All pre-existing pages (gamez, world-structure, weather, anim-definitions, hud) got README back-links in their headers; grep confirms no stale references to the old zrdr.md sections anywhere outside historical logs, and no game-asset bulk data (field tables + tiny excerpt values only, per the hard rule). CLAUDE.md's docs/formats bullet + format-docs paragraph updated; the going-forward rule stands: **new decodes land with their docs page in the same change.** Remaining Run-2 open item: 12 (turn rates, user-deferred).

**Run-2 item 12 — turn-rate calibration ✅ DONE (2026-07-19)** — the user's interim global `rotationTune = 2.0` (commit ddcc57f, "rolling is a lot faster in original") split into per-axis TUNE constants in `FlightModel.cs`, calibrated in closed form against the user's stopwatch measurements of the original (Bloodhawk, full throttle): **360° roll in 2 s, sustained full-pitch 360° at 90° bank ("horizontal loop") in 11 s (with visible speed bleed), full-rudder 360° in 30 s.** The rotation subsystem is a linear first-order lag — steady rate = torque·recInertia·Tune/`ang_momentum_damp` (× the yaw-only speed factor `eff`, 0.4 at cruise), spin-up τ = 1/damp = 0.2 s, time-to-360° ≈ 0.2 + 2π/rate — so with the Bloodhawk dynamics (pitch_torque 3.3 · recI 1.18, rudder 2.0 · 1.0, roll 7.5 · 1.1, damp 5.0) the constants solve exactly: **`RollTune` 2.12** (3.49 rad/s; the old ×2 gave 189°/s — the user's hand factor was nearly right for roll, which was the axis that prompted it), **`PitchTune` 0.75** (0.58 rad/s; the old ×2 gave 89°/s = a 4.2 s full circle, ~2.7× too much authority), **`YawTune` 1.32** (0.21 rad/s at cruise; old gave a 19.8 s circle vs the measured 30 s). *Verified* by scripted `--hold` telemetry runs: steady `rates=` read 3.50 (roll, from the first 1 s sample — τ 0.2 s), 0.58 (pitch), and 0.21 (yaw, measured after a 25 s full-throttle run-up to 134 m/s; its position trace closes a full circle every ~30 samples = 30 s). Stall regression (throttle-0 full-pull): the horizon cap still holds — nose only descends from its stall-entry elevation — and releasing the stick drops into a normal glide; one behavior shift from the pitch cut: a *held* full-stick stall now settles at the stall-drop/elevator equilibrium (~22 m/s, nose ~28° — depth where 1.25·depth = 0.58) instead of pinning at the cap, a milder cousin of the accepted arcade-hang artifacts. *Open fidelity notes:* the original may couple pitch rate to speed (the user saw the sustained turn bleed speed; ours is a constant rate matching the 11 s average), and the yaw constant is cruise-specific — the inverted-`eff` speed shape (`1.4 − clamp(v/fd, 0.25, 1.15)`, user-authored) is itself unvalidated off-cruise. **Pending user feel A/B**, especially pitch. This closes the last open item of `docs/plans/PLAN-M2-polish-2.md` — all 13 items landed (item 9 deferred by decision).

---

**Milestone 2.5 "First Prototype" begins.** Plan: `docs/plans/PLAN-M2.5-prototype.md` (7 items — stunt mode, launchscreen, splitscreen).

Item 1 **Stunt mission core ✅ DONE (2026-07-19)** — `--stunt` starts a Stunt Flying run: the mission's Danger Zones load from data, flying within a sphere of a zone completes it (logged, HUD), completing all ends the run. Playable-but-ugly — the projected marker HUD is item 2, timed scoring item 3. *Decode (schema in `docs/formats/missions.md`, new page):* three sources combine — the mission `ia.json`'s `dzones` list (`[dzpathN, dzN]` pairs, objective order), the mission `targets.json` (node NAME → `description`/`category_label`/`help_label` MSG keys; a **list of `[key,value]` pairs**, not the flat-alternating reader shape — walked as pairs), and the top-level `messages.json` string table (a plain JSON object `{entries:[{key,id,value}]}`, **not** a zrdr list). `dzN` is a gamez point marker (`mesh_index -1`, under the identity `world1` root so its translation is world-space); `dzpathN` is the untextured vertex-coloured route ribbon under the world's skipped `dzpaths` group (AI/guide data, never rendered). Key gotcha: **drive off the `dzones` list, not the `dzN` nodes** — numbering is non-contiguous (C1B = dz1/3/4/6/7) and C1's gamez `dz6` is not an objective. Zone counts: C1 5, C1B 5, C2 9, C3 4, C4 14, C5 17; C1C/C2B have none (different IA type). *Implementation:* `src/Mech3/Messages.cs` (key→string, missing file → empty, unknown key → itself), `src/Flight/MissionTargets.cs` (targets.json node→keys, generic across mission types), `src/Flight/StuntMission.cs` (`Load` builds the zone list + resolves positions via new `GameZ.WorldTransformOf` (parent-chain accumulation) + strings via the two loaders; `Update(planePos)` each physics frame does the `DzRadius` 60 m TUNE sphere test, order-free, auto-advancing the active target, firing `ZoneCompleted`/`RunCompleted`; `MarkerText` = "Danger Zone [Fly Through] - Passenger Hangar"). Wiring: `--stunt` = `--fly` + forced `stunt_flying` spawns + `controller.Stunt`; the FlightController feeds it `_model.Position` and appends `StatusLine()` to the HUD; **not reset on respawn** (a crash keeps completed zones — deliberate divergence from the original, which exits to the menu). Debug helpers added (user request, to avoid blind flying): `--spawn-at=x,y,z`/`--spawn-dir=x,y,z` place the plane exactly (bypass the mission spawn list), `--debug-dzpaths` builds the route ribbons (`WorldBuilder.BuildDzPaths`). *Verified:* a scripted steep dive onto C1 `dz1` (spawn-at directly above) logs `completed dz1 — Danger Zone [Fly Through] - Passenger Hangar (1/5)` + the target switch to `dz2`, the HUD shows `STUNT 1/5 — … Train Tunnel East`, and the plane re-enters dz1's sphere on every auto-respawn yet logs completion only **once** (proving the no-reset divergence); an all-8-chapter loader smoke resolves 54 zones' positions + names cleanly, with C1C/C2B correctly hitting the free-flight fallback and C1B's non-contiguous numbering handled; `--fly` without `--stunt` is unchanged (no stunt state); `--debug-dzpaths` renders the ribbons. A full 5/5 `AllComplete` (all zones in one hand-flown run → `stunt: ALL DANGER ZONES COMPLETE`) was **confirmed by the user the same day** — the scripted verification couldn't reach it because every C1 zone is embedded in a tunnel/hangar requiring skilled threading (not blind-scriptable), so the user closed it by flying it. Open TUNE: `DzRadius` 60 m.

Item 2 **Marker HUD ✅ DONE (2026-07-19)** — the original's objective marker guidance is live: `src/Flight/MarkerHud.cs`, a viewport-filling `Control` on the flight HUD canvas (CompassTape/GaugeCluster pattern) built by PlaneViewer under `--stunt` and fed the plane pose each frame by FlightController. *Behaviour (matched to `OriginalScreenshots/C1 IA1 Cloudcoverage 1.png` — the assembled marker "Danger Zone [Fly Through] - Train Tunnel Mid / 7 o'clock"):* the active Danger Zone projects through the **live camera at `_Draw` time** (`Camera3D.UnprojectPosition`/`IsPositionBehind`, deliberately not cached in `_Process` so the marker never lags a fast chase-camera roll); on screen (not behind AND inside the margin rect) → a reticle + a centred text block just below it (`Category [Help] -` / description / live distance); off screen or behind → the block clamps to the screen edge with an arrow pointing the shortest way + the `N o'clock` relative bearing. The off-screen direction is the projected offset from screen centre **negated when `IsPositionBehind`** (a point behind the camera unprojects mirrored through centre); the edge point is a ray-vs-inset-rect solve, and `DrawLinesClamped` keeps the whole block on-screen so a corner marker never spills off. `ClockHour` = the zone's world bearing (`atan2(dx,-dz)`, N = −Z) minus the plane heading, rounded to 12 sectors (0 → 12). Also draws the run-status line (`FormatTime` `m:ss.t` + zones done, top-centre under the compass tape), the one-shot intro banner ("Fly through all the Danger Zones to win!", messages `MSG_BRF_IASF_OBJ2`, fading over its last second of a 5 s window), an all-complete banner, and a 1.6 s green zone-cleared flash driven by the `StuntMission.ZoneCompleted` event (unsubscribed in `_ExitTree`). *StuntMission additions:* `Elapsed` (advanced by a new `Tick(dt)` called every physics frame — placed in FlightController after the pause-return but **before** the crash branch, so the run clock keeps ticking through the crash freeze yet freezes while paused, and stops at `AllComplete`), `IntroLine` (resolved at load from `MSG_BRF_IASF_OBJ2`), and `CycleTarget()` (manual next-incomplete cycling, wrapping — `Complete` now re-picks the active zone only when the *displayed* one is the one that completed, so a manual selection sticks). *Input:* Tab / gamepad-X (edge-detected in FlightController) cycle the displayed target — the plan suggested gamepad Y but Y is the respawn button, so X (a free face button); the item-1 text-HUD `StatusLine()` append is retired (kept only as a `Marker == null` fallback). *Distance* is shown in the HUD's imperial units (feet under a mile, else miles, to match the altimeter/speedometer — TUNE, the original's marker unit is unverified). *Verified* by scripted `--spawn-at`/`--spawn-dir` screenshots from one C1 spawn (all with `--hold` level flight): (a) facing dz1 → the reticle + "Danger Zone [Fly Through] - / Passenger Hangar / 3023 FT" land exactly on the Passenger Hangar arch (projection accurate); (b) facing east → the arrow sits on the **left** edge with "9 o'clock", (c) facing south → the arrow at the **bottom** with "6 o'clock" (both matching hand-computed bearings, confirming the mirror-when-behind + shortest-side logic); (d) a level fly-through at the hangar altitude completes dz1 and a burst screenshot catches the timer at `0:04.8`, `ZONES 1/5`, the green "Passenger Hangar — CLEARED" flash, and the marker auto-advanced to dz2 "Train Tunnel East / 6 o'clock"; (e) `--fly` without `--stunt` builds clean with no marker (exit 0, no errors). The circular live-camera objective inset beside the original's marker is **out of scope** (backlog). *Open / TUNE, pending user A/B:* run-status placement, marker font size, the distance unit, the intro/flash durations, and target-cycling feel; `DzRadius` 60 m carries over. **Next: item 3 (scoring — timer + per-zone splits, end scoreboard, best-time persistence, restart flow).**

Item 3 **Scoring ✅ DONE (2026-07-19)** — completing all Danger Zones now produces a results scoreboard with per-zone splits, a total, and a persisted best time. *Two new files:* `src/Flight/StuntScoreboard.cs` — the end-of-run overlay, a `Control` added last to the FlightController HUD canvas (over the marker/dials), hidden until `StuntMission.RunCompleted`. Deliberately **plain Godot UI** (a dimming full-rect `ColorRect` + `CenterContainer` → styled `PanelContainer` → `VBoxContainer`: title, `chapter · scenario · plane`, a 3-column ZONE/SPLIT/TIME `GridContainer` from `InCompletionOrder()`, TOTAL, a best-time line, `R — New Run / Esc — Quit`) rather than the hand-drawn marker/dials — the plan called for "clean Godot UI", and containers get variable-row column alignment right for free. It wakes on `RunCompleted` (reads the prior best, calls `ScoreStore.RecordIfBest`, **logs the split table to stdout** so a headless run's scoring is reviewable, then rebuilds + shows the panel) and **hides itself in `_Process` the instant `AllComplete` clears** — so a restart needs no teardown wiring. `src/Flight/ScoreStore.cs` — best-time persistence in `user://stunt_scores.json` (the engine's writable user dir — never the repo; per-player anyway), keyed `chapter/mission/plane` (`C1/IA1/player_bhawk`) → `{best: seconds, date}`, via Godot `FileAccess`+`Json` (the only API that resolves `user://`, unlike Messages' System.Text.Json); missing/corrupt = empty store (never throws — a persistence hiccup must not break the board), `RecordIfBest` saves iff faster and **never worsens** a record. *StuntMission additions:* `Complete` stamps each zone's `CompletedAt` (cumulative `Elapsed`) + `CompletionOrder` (flown order); `InCompletionOrder()` yields the split order; `Reset()` starts a fresh run (clock + zones cleared — the deliberate opposite of a respawn); `FormatTime` promoted to a shared public static and forced to **InvariantCulture** (the first user run rendered `2:13,6` — the German system locale's comma; a game clock must be locale-neutral, and MarkerHud's duplicate `FormatTime` was removed in favour of it); `DebugCompleteAll()` (test-only) force-completes with synthetic splits. *FlightController:* on `Stunt.AllComplete` `_PhysicsProcess` early-returns **before** the crash branch (the flight sim freezes in place; the `_Process` chase camera still holds on the plane, the results overlay draws over everything), and `RespawnPressed()` there calls `RestartStuntRun` (`Stunt.Reset` + `Respawn` = fresh clock/zones, distinct from a mid-run R which keeps progress); the check sits ahead of the crash branch so completing the final zone always restarts cleanly. `DebugCompleteStunt` (from the new `--debug-scoreboard`) force-completes on the first frame for a deterministic board screenshot. *PlaneViewer:* builds the scoreboard under `--stunt`, deriving the plane display name from the vehicle def (`PlaneDisplayName` — the player defs are `p<name>`: `pbloodhawk` → "Bloodhawk"; placeholder until the item-4 roster) and the `chapter · scenario` context via a small `Humanize`. *Verified* by **three user hand-flown C1 runs** (a full 5-zone stunt run is not blind-scriptable — every zone is a tight ground-level tunnel/hangar gap): run 1 wrote best `133.56` (2:13.6); run 2 (faster, 2:02.8) flipped to **NEW BEST** and overwrote to `122.76` — proving it loaded run 1's value to compare (so the JSON **survives relaunch**) — its splits monotonic in cumulative time and their deltas summing to the total, spanning **two mid-run crashes** whose respawn time the clock absorbed (split 3 = +39.2 s); run 3 (slower, 5:32.0) logged `(best 2:02.8)` — **not** NEW BEST — and left the JSON **unchanged** (never worsens). The board's **visual layout** was confirmed via a `--debug-scoreboard --screenshot` (aligned columns, "Bloodhawk", the BEST-branch line, footer, HUD gauges visible behind the dimmed backdrop; the debug run is score-file-safe — its synthetic 2:15 didn't beat 2:02.8, and the run was backed up/restored regardless). *Same-turn tweaks:* `DzRadius` tightened 60 → 30 m (user TUNE — the 60 m sphere over-completed). *Open / TUNE, pending user A/B:* scoreboard fonts/placement, `DzRadius` 30 m; R-restart is verified by construction (`Reset`+`Respawn`, board hides on `!AllComplete`) — a live keypress isn't headless-testable, but the user will exercise it. **Next: item 4 (launchscreen — Mode → Chapter → Plane, keyboard/controller, no-args default).**

Item 4 **Launchscreen ✅ DONE (2026-07-19)** — a bare launch (no content-selecting arg) now shows an in-game menu instead of orbit-viewing the Bloodhawk: pick Free Flight or Stunt Flying, then the chapter (map), then the aircraft, then fly through the existing pipeline. *New:* `src/UI/LaunchMenu.cs` — a `CanvasLayer` menu, three screens (`Mode → Chapter → Plane`) walked with keyboard **or** controller, and `RunGame.ps1` — the "play" entry point (build, then Godot with no args → the launchscreen; any args forwarded verbatim bypass it, unlike RunDev.ps1's console prompts). *Menu design:* clean opaque-panel Godot UI (title / breadcrumb / heading / centred item list with a ▶ gold-highlight focus / detail line / footer), fonts scaled by viewport height. Chapters mirror RunDev's 8-world roster (display name + folder code); planes mirror RunDev's 11-plane curated order/names (incl. the non-obvious Devastator=`player_pfighter`, Hellhound=`player_avenger`), with a couple of `PlaneStats` stats (Top Speed mph from `fd_speed`, Weight) **loaded lazily per focused plane** and cached (a failed load shows "(stats unavailable)", never blocks). **Input is polled every frame** rather than wired through Godot's input map / focus, so it needs zero project-settings and behaves identically for keyboard + pad: a merged vertical intent from ↑↓ / W,S / d-pad / left-stick with edge-detect + auto-repeat (`RepeatInitial` 0.42 s / `RepeatInterval` 0.12 s TUNE); Enter/Space/A accept, Esc/B back (Back on Mode → quit). `ShowMenu` re-primes the input edges from the current raw state so a key still held from the transition here (the Esc that left a flight) is not read as a fresh press. (Gotcha: a `CanvasLayer` is a `Node`, not a `CanvasItem` — no `GetViewportRect()`; it reads size via `GetViewport().GetVisibleRect()`.) *PlaneViewer refactor (the substantive part):* the giant build formerly inline in `_Ready` was extracted verbatim into a re-runnable `StartSession()` that hangs **everything a session builds** under a fresh `_worldRoot` Node3D (world/plane/horizon/whiteout/puffs/precip/controller); `_Ready` now parses args, registers the global shader params + lighting + the persistent camera **once**, then either shows the launchscreen (`ShowLaunchMenu`, when no content arg or `--menu`) or calls `StartSession`. The menu's selection → `StartSessionFromMenu` fills the same fields the CLI parser sets (`_chapter`/`_planeName`/`_stunt`, scenario derived from the mode) and calls `StartSession`, so **menu and CLI share one build path**. `ReturnToMenu` (Esc from a menu-launched flight, or a failed menu build) `QueueFree`s `_worldRoot`, **nulls every cached session ref** (`_plane`/`_horizon`/`_deck`/`_whiteout`/`_puffs`/`_precip`/`_edgeExtender`/`_weather` — so `_Process`'s existing null guards skip them the one frame before the deferred free lands) and reshows the menu; a CLI-launched flight's Esc still quits (`_menuDriven` gates it). Chapter-dependent paths (gamez/texture/mission) moved into `StartSession` so a new chapter selection re-resolves them; base paths + `PreferUnzipped` (hoisted from a `_Ready` local to a class static) resolve once. Bypass rule: any of `--plane`/`--chapter`/`--fly`/`--stunt`/`--damage`/`--screenshot` sets `hasContentArg` and skips the menu; `--menu[=mode|chapter|plane]` forces it (and the `_Process` screenshot guard was relaxed so `--menu --screenshot` can capture the menu for layout verification). This is **the project's first in-process world rebuild** — the risk the plan flagged. *Verified:* build clean (0 warnings); a bare no-args launch (`--quit-after`) builds **no** plane (the menu shows — the old behaviour printed `loaded 'player_bhawk'`); the Mode and Plane screens render correctly to `--menu=…` screenshots (roster, focus highlight, breadcrumb, and Devastator's "Top Speed 253 mph / Weight 2850" — the lazy stat load works); the CLI regressions build byte-for-byte as before — `--fly` C1 (3489 mesh instances, clutter/edge/weather/spawn logs identical), `--stunt` C1 (marker + scoreboard, the 31 pre-existing early-frame font-cache warnings confirmed **identical on the committed baseline** via a stash A/B, so not a regression), static orbit plane, and static `--chapter` C2; and a scripted in-process rebuild (a throwaway self-test that called the real `StartSessionFromMenu`/`ReturnToMenu` on a frame schedule, then removed) drove **C1 stunt Bloodhawk → teardown → C4 free-flight Fury** (different chapter/plane/mode, 8289 vs 7064 nodes, world-light 1.0 vs 0.80, snow) and rebuilt clean — no `GlobalShaderParameterAdd`-twice error (those stay in `_Ready`; `SetupWeather` only `Set`s), no exception, session 2 rendered correctly. *Open / pending user playtest:* the interactive keyboard-only + controller-only feel (repeat timing, the 11-item list on small viewports), font/placement TUNE, and Stunt on a no-dzone chapter (C1C/C2B) falling back to free flight (logged, not blocked in the menu). **Next: item 5 (splitscreen foundation — shared world, per-player viewports + input).**

**Gamepad hotplug / any-pad input ✅ FIXED (2026-07-19)** — user report: a controller had to be on *before* launch or it didn't work; runtime detection wanted ahead of splitscreen. *Diagnosis:* not a hotplug gap — Godot's `Input.GetConnectedJoypads()` is live and `JoyConnectionChanged` fires on connect/disconnect (the matching-looking godotengine/godot#112802 "controller connected after start not detected on Windows" was closed by its reporter as a false alarm — an addon's fault, not the engine), and all our input reads already re-polled the roster every frame. The real bug was the **`pads[0]` policy** on a machine with **phantom joypad devices**: the launch-roster log (added this change) shows 4 devices here — an "XInput Controller" slot, the 8BitDo Ultimate 2 wireless dongle **twice** (it enumerates even with the pad asleep), and a Razer HID (`0x1532/0x022b`) exposing a joypad interface — so the live pad's index depends on connect order: awake before launch it grabs an early slot and works; woken after, it lands in a later slot that `pads[0]` never reads. *Fix (also the splitscreen-ready policy):* every pad read in FlightController (`AnyPadPressed`/`AnyPadAxis`: fly stick/rudder/throttle, respawn Y/A, pause Start, marker-cycle X, paused orbit cam) and LaunchMenu (navigate/accept/back) spans **all connected pads** — buttons OR'd, axes take the max-magnitude value — so idle phantom devices (sticks under the deadzone, triggers 0) contribute nothing and any real pad works the moment the engine lists it. Plus visibility: PlaneViewer `_Ready` logs the roster at launch + one line per connect/disconnect (note: the engine re-fires the signal for already-present pads shortly after startup), and the launchscreen footer shows the live roster (`GamepadStatus` — none / pad name / "N connected — all active"), redrawn from `_Process` when it changes. *Verified:* menu screenshot shows "Gamepads: 4 connected — all active"; a C1 fly smoke with the 4 idle devices present flies straight (`rates=(0,00…)`, throttle holds 0.50 — no phantom input injected); build clean. *User-confirmed same day:* hot-plugging the pad mid-game works (footer/log react, pad flies).

**Controller-disconnect freeze ✅ WORKED AROUND (2026-07-19, same day)** — user report: turning the controller off in the menu froze the game solid. *Diagnosis (an engine bug, fully pinned):* Godot ≥ 4.5-beta2 replaced its Windows joypad code with a bundled SDL3, and that SDL (3.2.28 even in Godot 4.7.1, released 2026-07-14) has a fatal pair: the **DirectInput backend counts HID button objects uncapped** (`SDL_dinputjoystick.c` `joystick->nbuttons++`, no limit), and the **disconnect path** (`SDL_joystick.c` `SDL_PrivateJoystickForceRecentering`) loops `for (Uint8 i = 0; i < joystick->nbuttons; i++)` — with `nbuttons ≥ 256` the `Uint8` wraps and the main thread spins forever. The 8BitDo Ultimate 2 dongle exposes exactly such a >255-button HID interface, so the moment the pad sleeps or is switched off the engine hangs (kill via task manager). Not our code — no C# runs during the hang. Community trail: godot#112658 (same controller, closed on a premature "4.6rc1 fixed it"), godot#115667 (open), SDL#14961 (root-caused 2026-06-22 by a Godot maintainer via a user memory dump: the debugger shows the stuck `Uint8` loop; fixed upstream by SDL#15304 typing the counter `int`, shipped in **SDL 3.4.4** — no Godot release bundles it yet; Godot 4.7.1's own "update SDL joystick device blocklist" doesn't cover the 8BitDo). *Workaround (landed):* both **RunGame.ps1 + RunDev.ps1** set `SDL_JOYSTICK_DIRECTINPUT=0` before launching Godot (respecting a pre-set value) — SDL hints read the environment, the dinput backend never initializes, so every >255-button phantom view disappears while real pads keep working via the XInput backend (≤ 15 buttons, freeze-proof) or HIDAPI drivers (PS4/PS5/Switch/Xbox pads all covered in the bundled tree). *Verified live:* with the hint set the machine's dinput-backed Razer phantom (`0x1532/0x022b`) vanishes from the roster ("none at launch" vs the phantom entry without the hint) — proving the env-hint path works through Godot's bundled SDL; the actual sleep-disconnect was **user-confirmed the same day** (pad off mid-game via RunGame.ps1 → no freeze, roster + footer react, pad rejoins on wake). *Also:* the roster log now includes each device's GUID + `GetJoyInfo` (VID/PID diagnostics); backlog records the removal condition (Godot bundling SDL ≥ 3.4.4) and the side effect (DirectInput-only controllers invisible while active). *Known limits:* direct editor/exe launches bypass the Run scripts and stay freeze-prone; the alternative user-side workaround is the pad's Bluetooth mode (community-confirmed) — but that pairs the pad as a DInput device, which the disabled backend won't see, so **keep the pad in dongle/2.4G (XInput) mode** with our scripts.

Item 5 **Splitscreen foundation ✅ DONE (2026-07-19)** — the technical core of local multiplayer, de-risked ahead of any UI: **N planes in one shared world, each rendered in its own pane with its own camera, sky, HUD and input device.** CLI-driven via the new `--players=N` (1–4, needs `--fly`/`--stunt`; item 6 adds the join flow). *Two new files:* `src/UI/SplitScreen.cs` — a `CanvasLayer` holding one `SubViewportContainer`+`SubViewport` per player over a black backdrop, laid out by hand on every resize (2P stacked top/bottom, 3–4P a 2×2 grid with 3P's fourth quadrant left black), all panes sharing the main viewport's `World3D`; and `src/Flight/PlayerRig.cs` — the per-view state bag (camera, viewport, HUD parent, visual layer, controller, and the player's private skydome/deck/puffs/whiteout). *The core problem and its solution:* four things are anchored to "**the**" camera every frame — skydome, cloud deck, cloud puffs, whiteout overlay — so N views need N copies, and each camera must see only its own. Solved with a **reserved band of visual layers** at the top of Godot's 20 (`PlayerLayerBit0` = 16 → layers 17–20): a player's private copies are moved onto its layer and its camera's cull mask is *everything outside the band plus only its own bit*, so all shared geometry (terrain, clutter, map-edge extension, precipitation, **and every other player's aircraft**) stays on layer 1 and renders in every pane. The domes are **rebuilt** per rig rather than duplicated (the star mesh carries a `csky_light_fade` instance uniform); the deck, which falls out of the world build, is duplicated with a `CopyInstanceShaderParams` lockstep walk — **`Node.Duplicate()` copies properties but not per-instance shader parameters**, and `node_bias`/`csky_fog_on`/`csky_light_fade` are exactly what SceneBuilder's coplanar draw order needs. Precipitation needed nothing: its shader already positions from `CAMERA_POSITION_WORLD`, so one node is correct in all panes. *Other subsystems:* `MapEdgeExtender.Update` now takes a **list** of focus points and builds the union of the `Rings` neighbourhoods around each (one window serves every pane — with a single focus a player at the far edge would fly over void), with the no-op early-out comparing the whole focus-cell list. `FlightController`'s pad reads became per-instance: `PadDevices` (null = the single-player any-pad policy) binds one device per player, `UseKeyboard` makes P2–P4 pad-only, `HudParent` routes the HUD canvas into the player's SubViewport (so dials/compass scale off *its* height), `AllowPause` disables the P/Start debug freeze in splitscreen since it halts the shared sim. `FlightAudio.MixGain` = 1/√N (equal-power; crash and prop one-shots stay global). Spawns: one list per session, P1 at the random/`--spawn=N` base and each further player the next entry wrapping. `--hold` gained `|`-separated **per-player** sequences, without which none of this is scriptable. **Single player builds no SplitScreen node, narrows no cull mask and sets no visual layer** — that is the whole regression strategy. *Verified (C1 unless noted, all with zero errors):* 2P and 4P build and render independent panes with per-pane HUDs; the per-view diagnostic confirms `layer 17–20 / cull 0x1FFFF–0x8FFFF, sky=own deck=own puffs=own whiteout=own` for each player; a scripted 2P run flew the two planes **9.1 km apart** in opposite directions, both still flying; the decisive shot put P1 **6.2 km past the map's −X edge over continued extension terrain** (agl 512 m) while P2, 5.4 km away and off a different edge, showed **its own night skydome with the moon and stars** above the cloud deck — sharing either the dome or a single-focus edge window would have been immediately visible in that frame; P2 crashed 3× (auto-respawning) while P1's telemetry continued uninterrupted; 4P `--stunt` shows the danger-zone marker + intro banner in **P1's pane only** (per-player racing is item 7, logged); and the CLI battery (`--fly`, `--stunt`, static orbit, static `--chapter`, `--menu`, `--players` without `--fly` → falls back to 1 with a note) is unchanged. *Frame cost:* 600 frames render in the same wall time at 1P/2P/4P on both C1 and C5, up to 4K — all vsync-limited on this RTX 5080, so splitscreen costs nothing measurable here (each pane renders at 1/N the pixels, keeping total pixel work constant; the extra per-view draw-call submission didn't move the number). **A weaker-GPU measurement is still owed.** *Two bugs fixed along the way, both pre-existing and both confirmed on the committed baseline by stash A/Bs:* (1) **the plane stopped flying when held straight and level** — `FlightModel`'s velocity-chases-nose term ended in `VelocityDir.Slerp(nose, …)`, and cruise is exactly the degenerate case: Slerp derives its axis from a cross product whose float error swamps a sub-degree angle, Godot's `IsNormalized` check throws, and the exception propagated out of `_PhysicsProcess`, aborting the frame before the position commit (~1 error per physics frame, forever). Reproduced deterministically with `--hold=0,0,0,1` — the plane froze at a fixed Z with telemetry stopped — and fixed by branching to a normalized lerp under `pathDot > 0.999` (≈ 2.5°, where lerp and slerp differ by under a thousandth of a degree); the anti-parallel guard stays. (2) **MarkerHud's 54-errors-per-run font spam** (present since item 2): `_Draw` can run before `_Process` has sized the Control, making every metric 0 and tripping Godot's font cache — now an early return at zero height plus `Max(1, …)` on each scaled size, which also matters for real in a quarter-height 4P pane. *Open / pending:* per-player HUD metrics are visibly untuned at half/quarter pane height (the compass tape overlaps the text line at 4P) — that is item 6's explicit job; `MixGain` 1/√N and `SpawnAbreast` 60 m are TUNE; interactive multi-pad playtest is owed (this machine has one controller, so P2–P4 were exercised by scripted `--hold`, not by hand). **Next: item 6 (splitscreen join + plane select + per-player HUD).**

Item 6 **Splitscreen join + plane select + per-player HUD ✅ DONE (2026-07-19)** — the launchscreen grew a join flow and every player now picks their own aircraft; the flight HUD learned to draw itself at pane size. *Join flow (`src/UI/LaunchMenu.cs`, rewritten around a player list):* player 1 is the keyboard plus every pad nobody else has claimed, and any free pad joins by pressing **Start**, up to the rig's 4; the join strip under the breadcrumb names each player and their device on every screen, in that player's colour. Player 1 alone drives Mode and Chapter (footer says so); on the **Plane** screen every joined player moves their own cursor through the same list — duplicates allowed — and locks with A, and the flight starts the moment everyone is locked. B unlocks; B while unlocked leaves the session (player 1 instead goes back to the chapter screen, unlocking everyone); a pad that disconnects drops its player, and player 1 just loses that pad. *New `src/UI/MenuInput.cs`* holds one player's device binding + edge/auto-repeat state; splitting it out of LaunchMenu is what made per-player cursors possible at all, since every menu read used to be an any-pad OR across the whole roster. **The one subtle decision:** player 1 holds a *set* of pads (everything unclaimed), not `pads[0]` — binding it to the first device would have quietly re-broken the 2026-07-19 phantom-device fix (a wireless dongle enumerating with the pad asleep sits in slot 0), and it falls out for free that a pad leaves player 1's set when it joins and rejoins it on un-join. `SplitScreen` gained the shared identity colours/tags (P1 gold — the launchscreen's existing focus colour, so a single-player menu is pixel-identical — P2 blue, P3 green, P4 pink) that item 7's race scoreboard will reuse. *Per-player planes:* the menu's `Launch` now carries one `PlayerChoice` (plane + pads) per player; `PlaneViewer` keeps a `_planeNames` list (`PlaneFor(i)`), loads `PlaneStats` per **distinct plane** through a small cache, and honours the join flow's device binding instead of re-deriving it from the roster. `--plane=` accordingly accepts a comma-separated list (`--plane=player_bhawk,player_fury`) which implies that player count — the CLI equivalent of the menu pick, and the only way to verify it without four controllers. *Per-player HUD (`src/Flight/HudMetrics.cs`, new):* every HUD element was calibrated at a 1440p reference and scaled by `viewportHeight/1440`, which in a quarter-height 4P pane rendered the dials as unreadable dots. The new rule splits the two factors — the **window** height still sets the base scale, and the pane's share of it is damped through a square root (a 2P pane draws at ~71 %, a 4P pane at 50 %, instead of 50 %/25 %) — and is exactly the identity map when there is one full-screen view, which is what makes the single-player regression provable rather than hoped for. CompassTape, GaugeCluster, MarkerHud, StuntScoreboard and the text block all route through it. Damped sizes only stay on screen if positions are anchored to a pane **edge**, so GaugeCluster's dials (measured off HUD.png as absolute 1440p y-coordinates, but really sitting 331/141 px up from the bottom) are now placed from the bottom — algebraically identical at the old scale. Two same-turn fixes fell out of looking at real panes: the scoreboard's old `Max(1, h/720)` would have overflowed a half-height pane, and the text block's one-liner ran into the top-centre compass tape at 4P (a pane is proportionally *wider* than tall), so the throttle moves to its own line below full screen. *Verified:* the 1P launchscreen is **pixel-identical** (0 of 921,600 pixels differ over a 200-frame run) and the static orbit shot is **byte-identical** (md5); the 1P `--fly` and `--stunt` shots differ from the committed baseline in **4 and 16 sampled pixels, none of them in the HUD bands**, against a measured baseline-vs-baseline noise floor of 13,346 pixels / 2,902 in the HUD bands (these world shots are not frame-deterministic run to run, so md5 comparison is meaningless — the noise floor had to be measured first). 2P and 4P panes render distinct aircraft with their own readable dials, compass, marker and scoreboard; 3P over C5 keeps its black fourth quadrant; the 4P plane-select screen shows four coloured cursors with per-player pick lines, and a new `--debug-join=N` adds device-less players so that screen is screenshottable on a one-controller machine. The launchscreen also gained a content-aware layout scale (the tallest screen — a 4-player plane select — used to push its footer off a 720p window). *Open:* the **menu → multi-player launch itself is unverified at runtime** — it needs two controllers, which this machine does not have, so join/un-join, the simultaneous lock race, per-pad flight binding, and Esc-from-splitscreen back to the menu all rest on construction plus the CLI-equivalent path and are owed a user playtest; pane HUD sizes (the sqrt damping), the join/lock feel, and the plane-row tag gutter are TUNE. **Next: item 7 (splitscreen stunt race).**

Item 6 **follow-up, same day (user request)** — two revisions to the join flow, both of which made it simpler rather than more elaborate. (1) **The mode/chapter pad is claimed for player 1, and joining moved to the aircraft screen.** The first cut let any pad join on any screen, which left one genuinely ambiguous gesture: Start on the very pad player 1 was steering split it off as player 2 and dumped player 1 back on the keyboard. Now player 1 picks the mode and the chapter first and `ClaimP1Pad` pins whichever pad it actually used (`MenuInput.LastActivePad` — Start excluded, since that is the join gesture and not proof of ownership; phantom devices never register because they read idle), and `ScanJoins` only runs on the Plane screen, with `PrimeJoins` on the transition in. Start therefore has exactly one meaning by the time it is available. Driving mode/chapter with the *keyboard* claims nothing, so all pads stay free — the keyboard-versus-controllers setup is still reachable, now by intent rather than by accident. (2) **The aircraft screen is a real split screen.** One list with four coloured cursors on it was replaced by one panel per player, positioned by the new static **`SplitScreen.PaneRect(index, players, size)`** — the same call `Relayout` now uses for the flight panes, so the pane you choose in is the pane you fly in, and the two layouts cannot drift apart. Each panel shows the player's tag + device, the whole roster with their own cursor in their own colour, their focused plane's stats, and their lock state; the panel's border and background light up in that colour when locked, which is the at-a-glance "who are we waiting for". The shared breadcrumb, join strip and controls line moved to a bottom strip (`StripHeightFrac` 0.12, TUNE). `PaneBody` derives its font scale from the **pane's** height rather than the window's, which is why the full 11-plane roster fits — a 4P quarter pane and a 2P half pane are the same height, so both land on the same size — and it deleted the fixed tag-gutter/name-column machinery the one-list layout had needed. Confirmed while here: **plane select is not exclusive** and never was — nothing checks for duplicates, so any number of players can lock the same aircraft (each panel is independent, so the split version keeps that for free). *Verified:* 2P/3P/4P split select screenshots (3P leaves the same fourth quadrant black as the flight grid; `--debug-join` now locks its last synthetic player so both panel states show in one shot), the 4P flight layout is unchanged after the `PaneRect` extraction, and the single-player launchscreen is **still pixel-identical** (0 of 921,600 px) since none of the split path runs with one player.

**Damage-dial backing disc missing on 10 of 11 aircraft ✅ FIXED (2026-07-19)** — user report, from the 4-player layout screenshots: "the damage display is missing the background for player 2–4". *Diagnosis:* not a splitscreen bug at all — it reproduced in plain single player with `--fly --plane=player_fury`, and only *looked* per-pane because player 1 happened to be the Bloodhawk in every multi-player shot taken so far. Dumping the `damageindicator` subtree of the whole roster out of planes.zbd showed why: the dial's face is the same untextured 12-gon (the dark backing disc) on every aircraft, but `player_bhawk` carries it as the `damageindicator` node's **own** mesh (156, 1 poly) while every other player plane leaves that node `mesh_index` −1 and hangs the disc off an extra generically-named child — `g951` Fury, `g927` Kestrel, `g1156` Balmoral, `g992` Warhawk, `g843` Devastator. `GaugeCluster.ExtractDamageDial` read only the dial node's own mesh for the face and then only `*damage` children for the zones, so those discs were dropped and ten of the eleven planes drew bare floating zone shapes over the world. *Fix:* any child that is not a `*damage` zone now contributes to the face — precisely the rule `ExtractInstrument` already used for the altimeter and speedometer ("anything that isn't a needle or an `_on` overlay is face") — with the face sort moved after the loop. Verified: all four dials in the 4P shot now have their disc with the correct per-plane silhouette, and the Bloodhawk is untouched (its `damageindicator` has no non-zone children, so nothing new is added). Documented in `docs/formats/hud.md` as a per-plane parenting gotcha, since any future reader of this data hits it.

**M2.5 item 7 — splitscreen stunt race ✅ DONE (2026-07-19)** — the prototype's multiplayer payoff and the last item of the Milestone 2.5 plan: 2–4 players fly the same Stunt Flying mission concurrently in one shared world, each with their own zone progress, marker HUD and run clock, ranked on a shared end-of-race board. *Implementation:* two new files. `src/Flight/StuntRace.cs` holds one `Racer` per player (their `StuntMission`, plane name, placing, finish time) and does only the race bookkeeping — subscribing each run's `RunCompleted` to stamp the next placing, `AllFinished`/`RaceCompleted`, `Standings()` (finishers by placing, then anyone still out by progress), `Ordinal()`, and `Restart()` for the rematch. All the actual detection and scoring is item 1's, unchanged: `StuntMission` was already self-contained per run, so racing needed nothing new there beyond `ForAnotherPlayer()` — a deep copy of the zone list giving each player fresh completion flags, clock and events, so the ia.json/targets.json/messages parse and the gamez position lookups happen **once per session** instead of once per pilot. `src/Flight/StuntRaceBoard.cs` is the shared results overlay: same clean-Godot-UI construction as the solo `StuntScoreboard` so the two read as one screen, but it covers the **whole window** — its own `CanvasLayer` (`Layer = 10`) above `SplitScreen`'s panes rather than a SubViewport, and scaled on raw window height rather than `HudMetrics` (whose pane damping is for HUD elements *inside* a pane) — with one row per player: placing, colour-coded tag, aircraft, zones, total and the gap to the winner, winner's row in their own identity colour. *Wiring:* PlaneViewer hoisted the stunt load out of the rig loop and now builds a marker HUD **per player** bound to their own camera (before, only P1 had objectives at all and the rest flew free); `StuntMission.LogTag` prefixes each run's log lines with the player tag so four concurrent runs stay readable in one log. FlightController gained `Race`/`PlayerIndex`/`RestartRace`: its existing AllComplete freeze already parks each pilot independently, so a finisher simply holds at the finish while the field flies on, and R is gated until `AllFinished`, where it invokes the session's rematch (`race.Restart()` + `Respawn()` on every rig) rather than restarting one plane out of a race. MarkerHud's all-complete banner branches on the race: solo it is unchanged, in a race it becomes that pilot's placing + finish time plus `waiting for N pilots…`. Best times are deliberately **not** recorded for a race — totals aren't comparable across player counts or spawn indices — so `ScoreStore` stays single-player-only. *Two testing affordances, both consistent with existing house rules rather than test-only cruft:* `--debug-scoreboard` in a race **staggers** the forced finishes by player index (1.5 s apart, totals padded) so a screenshot run exercises the real one-finishes-while-others-fly sequence instead of N identical totals on frame one; and an unattended `--hold` run auto-rematches on a timer once the race completes, the same rule the crash branch already uses for auto-respawn, which is what makes a scripted race soak-test possible at all. *Verified:* the decisive mid-race shot — P1's pane showing `FINISHED — 1st / 2:15.0 / waiting for 1 pilot…` frozen at the finish while P2's pane still flies with its **own** 0:00.9 clock, 0/5 zones, its own intro banner and its own edge marker; 2P and 4P results boards ranked correctly (4P with four different aircraft, four identity colours, gaps `+0:11.5`/`+0:23.0`/`+0:34.5`); a scripted 2P run flying the identical straight line 60 m apart completed dz2 for P1 only (panes read `ZONES 1/5` vs `0/5`, log shows one completion) — progress is genuinely per-player, not a shared object; an auto-rematch soak looped five consecutive races with **byte-identical totals every time** (2:15.0 / 2:26.5), which is the real proof `Restart()` leaves no state behind, with zero errors. Regressions clean: solo stunt still shows the item-3 splits board with its `BEST 2:02.8` preserved and un-overwritten, 1P and 2P free flight, static orbit, and `--stunt` on a no-dzone chapter (C1C) at 2P all build error-free. *Same-turn fix:* that no-dzone case logged via `GD.PushWarning`, which emits a C# backtrace — a violation of the run-2 log-hygiene rule for what is an expected data fact (C1C/C2B have no dzones by design), now a plain line. **Open:** the two-controller playtest — board fonts/placement, rematch feel, and the open design question of whether a finished pilot should keep flying instead of freezing at the finish.


## 2026-07-20 — Aircraft paint schemes, colours and decals

User request, pulled out of the backlog: aircraft fly in real liveries instead of the shipped unpainted skins (the long-standing "our Bloodhawk is blue, the original's is red" report).

**New modules.** `src/Mech3/PaintScheme.cs` — the seven-field `paint_*` record (pattern + three colours + three decal indices), `LoadCatalog(zrdr)` parsing the **12 shipped patterns** straight out of vehicle.json (deduped by pattern name, since the `_2`/`_3`/`_5` per-chapter roster duplicates repeat their base def verbatim; `player_fortune` ships colourless — its colours live engine-side — so the catalog entry is filled with the paint UI's Fortune Hunters red `223,0,41`), plus `Random()`. `src/Mech3/PlanePainter.cs` — the recolour and decal swap, built per aircraft instance so two players in the same plane wear different liveries, and never mutating the shared `TextureArchive` cache.

**Plumbing.** `SceneBuilder` gained an optional `textureSubstitute` hook: every material's resolved texture passes through it. `Find()` still runs even when a substitute exists — it is what sets `LastHadAlpha`/`LastAlphaIsSoft`, which the blend-vs-scissor decision reads (placeholder and decal are both `alpha=Full`, so the swap is alpha-neutral by construction). `PlaneBuilder` takes an optional `scheme:` and builds the painter lazily in a new `EnsurePainter`, because the aircraft's skin prefix can only be read once its root node is known; `BuildDestroyed` calls it too, so wreck pieces wear the same livery. `TextureArchive` gained `FindImage` (a fresh un-mipmapped `Image`, deliberately not the shared `ImageTexture` cache) and `FindByDecalIndex`.

**The RE had to correct this project's own prior decode.** `docs/formats/paint.md` claimed the palette holds "contiguous index ramps, one per paint region", generalised from `blo_fin`'s tidy 0–31 blue-gray run. That is wrong: on `blo_wing` — the same aircraft — the blue body is scattered across the palette (0, 6–7, 9, 13–14, 16–17, 20, 24–25, 28–31, …), and every plane palette inspected is simply **luminance-sorted**, ordinary median-cut quantizer output; `blo_fin`'s clean run is a coincidence of that sort (its blue body is the darkest thing in the texture). There are also **no `global_palettes`** in any chapter's `texture.zbd` or in `rimage.zbd` (all eight checked). So no index-range palette swap is possible, and the engine's real region table is in none of the extracted data. The **Fury settles it**: its skins are an all-neutral near-black shading map (median value 0.00 on `fur_wing`/`fur_fusalage1`) with zero saturated texels, yet the original flies a studio-blue `secfury` — so the region key cannot be derived from the texture's colours either.

**What the remake substitutes.** A hand-authored per-aircraft table of **hue windows** (`PlanePainter.Regions`), measured over every skin of each aircraft by saturated-texel hue histogram (C1; the skins are byte-identical across all eight chapters, verified by md5, so the table is chapter-independent): Bloodhawk 217° blue + 58° olive swoosh, Kestrel 199°, Peacemaker 219°, Brigand 200°, Warhawk 31° orange, Balmoral 35°, Hellhound 250° purple + 48° amber, Firebrand 252° (wide, 230–270°), Devastator 1° red, Autogyro 58° olive, Fury none. Window order **is** paint-slot order, slot 1 = body. Texels inside a window keep their **value** as position along the paint colour's ramp, which preserves the baked shading exactly; membership fades with hue distance *and* with desaturation, because a hard threshold speckles every antialiased region border (seen and fixed during development); desaturated texels are unpainted structure and are left alone.

**Decals.** `<prefix>_noselogo`/`_taillogo`/`_winglogo` swap for the scheme's numbered decal. Two data corrections landed with it: the Firebrand ships **no `fir_noselogo`** (paint.md said all eleven aircraft had all three — it is ten), so prefix detection keys on any of the three, and the index→texture lookup was proven unambiguous — exactly 50 textures in both C1 and C5 match "two digits + non-digit, not an `_1` LOD twin", one per index 00–49, no collisions anywhere else in the archive.

**Randomization.** `--fly`/`--stunt` draw a fresh livery per player on every map load (the user's ask); static `--plane`/`--damage` views stay unpainted. `Random()` deliberately does **not** roll three independent RGBs — that produces clown planes — but follows the shape all twelve shipped schemes share: slot 1 an identity colour (half the time an actual squadron colour from the catalog), slots 2/3 a dark and a light trim, mostly neutrals, with the catalog's own trims and the occasional muted tint mixed in so `cccp`'s red-and-yellow stays reachable. `--paint-seed=N` makes a scripted run repaint identically.

**Verified.** A `player_fortune` Bloodhawk renders **red with white swooshes**, matching `OriginalScreenshots/CustomPlane Paint1 Bloodhawk.png`; a Fortune Hunters Kestrel matches `OriginalScreenshots/Kestrel.png` (red body, black-and-white striped wing tips). Six aircraft (Bloodhawk/Kestrel/Warhawk/Firebrand/Devastator/Balmoral) paint correctly with decals; the twelve shipped patterns render as distinct, correctly-badged liveries (hughes yellow + Hughes "H", blackhat tan, cccp with the hammer-and-sickle, blckswan black with the swan, …). 2P and 4P give each player their own livery. **Regressions:** static `--plane` and `--damage=leftwing:0.25` screenshots are **byte-identical by md5** to a *verified* stashed-baseline build (the first attempt at this comparison was invalid — `git stash` without `-u` left the new files in place and the baseline build failed, so Godot ran the new DLL; redone with `-u` and an explicit build-success check); `--fly`, `--stunt`, `--chapter=C4` and 4P-over-C5 smokes are error-free. Cost ~38 ms per painted aircraft at load (4P: 5.68 s vs 5.52 s unpainted, 3-run averages).

**Two bugs found and fixed in development:** `catalog[(int)rng.Randi() % n]` threw `ArgumentOutOfRangeException` on the first 2P flight — `Randi()` returns a full uint and casting to int overflows negative for half the range (now `RandiRange`); and a descending-edge `smoothstep` silently returned 0 everywhere in the offline prototype, which had made the Warhawk look like it had no paint regions at all.

**Open:** achromatic paint regions are not recoloured (the Bloodhawk's outer wing panels stay gray where the original blacks them — a hue window cannot see a region with no hue) and the Fury gets decals only; slot order is by region area and is unvalidated beyond the Bloodhawk; the paint UI's "Shade" column is unmodelled (we ramp black → colour). All recorded in `docs/formats/paint.md` "Known divergences" and `backlog.md`. **Pending user A/B against the original**, plus a decision on whether the launchscreen should offer a livery picker (today: CLI-only + random in flight).


## 2026-07-20 — CLI inversion: flight is the default, `--viewer` is the inspection mode

User request. The viewer grew up around static inspection, so flying needed an explicit `--fly` while a bare `--plane=` orbited a parked model — backwards for a project whose point is flight.

**Inverted.** Any content arg now builds a flight unless `--viewer` is present: `--plane=player_fury` flies the Fury, `--chapter=C4` flies over C4. `--fly` is still accepted and still means exactly this, so every existing scripted command keeps working; it is simply redundant. `--viewer` asks for the static inspection view — the parked-plane orbit, or with `--chapter=` the static world the weather/fog/edge-continuation screenshots use. `--damage` implies `--viewer` (it was already meaningless in flight). Passing `--viewer` and `--fly`/`--stunt` together logs a note and honours `--viewer`, since a bare `--fly` is now just the default spelled out. A bare launch still shows the launchscreen, so `RunGame.ps1` is untouched in behaviour.

Internally `_worldMode` stopped being set piecemeal during arg parsing and is now derived once: `_fly || (_viewerMode && _chapterGiven)`. `RunDev.ps1`'s prompt flows key on `--viewer` instead of `--fly`, and no longer append `--fly`.

**Livery lab** (`src/UI/LiveryLab.cs`, new). `--viewer` gained an interactive `PaintScheme` editor on **L**: pattern stepper over the twelve shipped patterns, three RGB colour sliders with live swatches, the three decal slots stepping the 00–49 set by name, a random-livery button, and "copy CLI args" which puts the equivalent `--paint…` arguments on the clipboard — the point being that a livery found by eye becomes a scripted screenshot without retyping. It emits a bare `--paint=<pattern>` only while the colours and decals still match that pattern verbatim, otherwise the explicit overrides carry the whole scheme, so what you copy always reproduces what you see. It exists because the paint region table is hand-authored rather than extracted (`docs/formats/paint.md`): checking a colour against the original means seeing it on the model, and `--paint-color=` restarts the viewer for every guess.

**Live repaint.** `PlaneBuilder.Repaint(scheme)` re-liveries a built aircraft without rebuilding it: a fresh `PlanePainter`, then `SceneBuilder.Repaint()` re-resolves every material in a new `TexturedMaterials` registry (material → source texture name, recorded at build time) and writes the result into the live `ShaderMaterial`'s `albedo_tex`. Repainting an aircraft's ~8 small skins is a few ms against ~38 ms plus mesh work for a rebuild — the difference between an interactive slider drag and a slideshow.

**A verification decision worth recording.** The lab first applied its scheme *on top of* a plane the builder had already painted, which looked right in a screenshot — and would have looked exactly as right if `Repaint` were a no-op, because the constructor's paint was doing the work. So `--viewer` now builds the model **bare** and the lab is the single owner of the livery, applying even the initial `--paint=` through the same `Repaint` every slider uses. `--viewer --paint=cccp --screenshot` then proved the path for real: a CCCP-liveried Bloodhawk rendered from a builder that painted nothing. `--debug-livery[=N]` opens the panel and optionally steps the pattern N times, the headless equivalent of clicking (house convention, cf. `--debug-scoreboard`); `--viewer --plane=player_kestrel --paint=hughes --debug-livery=3` stepped hughes → german, applied the Luftwaffe decals and updated the CLI line, exercising stepper → scheme → repaint → widget sync in one shot.

**Regressions.** Both labs start hidden/clean, so `--viewer --plane=player_bhawk` and `--viewer --plane=player_kestrel --damage=leftwing:0.25` are **byte-identical by md5** to the pre-inversion `--plane=…` equivalents. Battery clean: bare `--plane`/`--chapter` fly, `--viewer --plane`/`--viewer --chapter` are static, `--damage` opens the lab, `--fly`/`--stunt`/4P race/2P mixed planes/`--menu=plane`/bare launchscreen all build error-free.

**Open:** the labs are mouse-driven (Godot UI) while flight is keyboard/pad — fine for a dev tool, but the panels have had no interactive playtest, only scripted verification. Whether the *launchscreen* should offer a livery picker for actual play is still open (see `backlog.md`).


## 2026-07-20 — Fix: H did nothing in a plain `--viewer` (damage lab was never built)

User report after the CLI inversion: "H for damage does not work. The livery lab with L works."

**Cause, and it was not the key handler.** `DamageLab` and `LiveryLab` have structurally identical `_UnhandledKeyInput` overrides, so the obvious suspicion — input being swallowed by the visible panel's focusable widgets, or by `PlaneViewer._UnhandledInput` — was wrong on both counts. Diagnosed by injecting synthetic key events through `Input.ParseInputEvent` from `_Process` and logging every key each lab's handler actually saw: with `--damage`, both labs received H perfectly and the panel *did* toggle (the HUD gauges staying up made it look like it hadn't). Without `--damage`, only the livery lab logged anything — because `PlaneViewer` only ever constructed a `DamageLab` when `--damage` was passed. There was no node to receive the key. The livery lab is built for every `--viewer`, which is exactly why L worked and H didn't.

That also made the livery panel's own hint ("H the damage lab") a promise the build didn't keep.

**Fix, matching the original request that `--viewer` integrate the other viewer options.** Every `--viewer` session now builds a damage lab; `--damage` only decides whether it opens at launch (and presets the sliders). New `DamageLab.StartHidden` keeps it out of sight otherwise, and `PlaneBuilder` gets `damagePanels: _viewerMode` so the `pdpN` torn-skin panels exist to flip. H now toggles the lab **as a whole** — slider panel and gauge layer together — since H means "is the damage lab here", not "is one of its two CanvasLayers here"; `_gaugesWanted` remembers the panel's HUD-gauges checkbox across a hide/show.

**Verified** by the same injection harness (removed again afterwards): in a plain `--viewer`, one injected H raises sliders + gauges, and two H presses return the viewport to a frame **byte-identical by md5** to the baseline — the real test that the toggle leaves nothing behind. `--viewer --plane=player_bhawk` and `--viewer --plane=player_kestrel --damage=leftwing:0.25` remain byte-identical to their pre-inversion `--plane=` equivalents even though the model now carries the hidden torn-skin panels. Battery clean: viewer, damage, a plane with no destroyable_parts, `--viewer --chapter`, bare `--plane` flight, 4P stunt race, `--debug-livery=2`. Load cost of the always-built lab: ~730 ms vs ~525 ms for `--viewer` (PlaneStats + 10 puffers + the gauge cluster), unchanged for `--damage`.

**Worth remembering:** "the key handler doesn't fire" and "the node doesn't exist" look identical from the outside. Injecting input and logging what each candidate handler receives separated them in one run; reading the two handlers side by side would never have, because they were never different.


## 2026-07-20 — Decoded `crimson.rof` and the `langui.dll` UI string table

User request: the plane-customisation screens and their description text are missing from every extraction, and `GOSDATA\ASSETS\crimson.rof` (~57 MB) looked like where they live. Hint supplied from decompiled code: zlib 1.3.1.

**Container.** `.rof` is a tree of directory nodes with zlib-deflated members — `u32 entry_count`, `u32 pool_len`, 24-byte entries `[offset, size_uncompressed, size_compressed, kind, name_len, name_offset]`, then a NUL-separated name pool; `kind` 0 stored / 1 directory / 2 deflate. The zlib hint was correct: `kind=2` payloads are raw zlib streams (`78 01`), so `DeflateStream` works after skipping the 2-byte header. Full schema in `docs/formats/rof.md`.

The decode came from **`crimptch.rof`, not the big file** — the 797-byte patch archive holds exactly one member (`ASSETS/SCRIPTS/AIRFRAME.SCRIPT`), which makes every field in the format visible at once at known values. Trying to infer the same structure from a 57 MB archive first would have been guesswork; the small sibling was the Rosetta Stone. Worth remembering as a general move: when a format ships in two sizes, decode the small one.

Validation: **all 846 members inflate to exactly their declared uncompressed size**, and the last member ends at byte 60,236,221 = the archive's exact length, so the layout is fully accounted for with no unexplained slack.

**The find that matters: the paint region masks.** The 184 custom `.BM` textures are `u16 height, u16 width` then exactly `w*h*10` bytes in four planes — a 24bpp greyscale shading map, **three 8-bit per-pixel weight masks summing to 255**, and a 32bpp overlay layer. Those three masks are the region table `docs/formats/paint.md` had recorded as "not in the extracted data", and which `PlanePainter` works around today with a hand-authored per-aircraft hue-window table. They are per *pattern* and per *skin*, which also answers that page's other open question — what a `paint_pattern` actually varies is the masks themselves.

They are **weights, not indices** (the user's hypothesis was that they might be colorization indices, which is the right instinct one step off): values cluster at 0/255 with intermediates at antialiased region borders. An index encoding cannot express a half-and-half boundary texel — and reintroducing soft membership is exactly what the hue-window implementation had to do to stop borders speckling.

Verified by rendering slot1/2/3 as R/G/B: `HUGHES` resolves into clean rectangular blocks, `FORTUNE` into a curved swoosh matching the white swoosh on the red Bloodhawk in `OriginalScreenshots/CustomPlane Paint1 Bloodhawk.png`. Across all 184 skins the three planes sum to 250–260 for >97% of pixels in 100 files and 89–96% in most of the rest.

**A measurement error worth recording.** An early pass reported some `.BM` base images as strongly saturated (mean chroma ~45), which would have meant the shading maps ship pre-painted. They do not — that pass sampled a fixed 90,000-byte window, which on a 64×64 texture runs far past the 12,288-byte base plane into the mask and overlay planes. Re-measured over the base plane only, mean chroma is **2.5–5.0/255** across every pattern's most-saturated skin: the base is effectively greyscale, as the colouring model requires. A sampling window that ignores the structure it samples will confidently describe the wrong bytes.

**Text.** Not in the archive at all — a Win32 `RT_STRING` table in `BINARIES\langui.dll` (1,247 strings, 16 per resource block, UTF-16LE), which the archive's own `SCRIPTS\RESOURCE.H` maps to `IDS_*` symbols for 327 of them. Aircraft names are IDs 3000–3010, short names 3020–3030, descriptions 3040–3050, in parallel order. Strings carry a `[FONTID]` prefix naming their font, and the font table is self-documenting inside the same string table (ID 9 is a comment explaining the convention). `language.dll` turned out to be GameOS engine error strings plus the locale/LCID, not UI text. Schema in `docs/formats/strings.md`.

**Extractor** — `ExtractRof.ps1` (repo root), matching `ExtractAssets.ps1`'s conventions (`-Source`/`-Dest`/`-Force`, mirrored output paths, coloured progress, up-to-date skipping). It unpacks both archives to `extracted\rof\` (patch overlay to `_crimptch\` so it can be diffed rather than silently overriding), decodes each `.BM` to `<name>.png` + `<name>_mask.png`, and emits `ui_strings.json`. The container/PE/pixel work is an inline C# type via `Add-Type` — unpacking 96 MB and interleaving ~3.1M mask pixels is precisely what PowerShell is slowest at, and it keeps a full run at **~1.5 s**.

**Verification:** a reference decoder was written in Python first, and the PowerShell output was then compared against it — **all 846 members byte-identical**, mask PNG channel order confirmed plane-by-plane (R = slot 1, G = slot 2, B = slot 3), base PNG identical to the shading plane. `-Raw` produces 0 mask PNGs and no JSON; a second run correctly reports both archives up to date; `-Force` redoes them. One genuine confusion during testing was self-inflicted: counting `*.png` under the output to check `-Raw` returned 313, which is the count of PNGs *shipped inside the archive*, not decoder output — counting `*_mask.png` is the honest check.

**Not done, deliberately:** `PlanePainter` still runs on hue windows. Reworking it onto the real masks is a change to the paint system with its own verification burden (the shading map to multiply is the `.BM`'s base plane, **not** the ZBD skin — a different image at the same dimensions), and the user scheduled it as a separate session. `docs/formats/paint.md` gained a "Superseded" section stating that every divergence it documents — achromatic regions unpainted, the Fury decals-only, slot order guessed — is a consequence of inferring regions from hue and disappears under the masks.

**Open:** overlay channel order (its content is greyscale, so RGBA vs BGRA is undetermined), the `BROADWAY`/`ITSTAXI` pattern folders that match no `paint_pattern`, and 3 of 173 `.BM`↔`texture.zbd` dimension mismatches.


## 2026-07-20 — Paint patterns: reworked onto the original's own region masks

User find, following the earlier paint work: the pattern artwork is in `GOSDATA\ASSETS\crimson.rof`, the UI resource archive nothing had opened. Container and `.BM` texture format decoded in `docs/formats/rof.md`; this entry is the remake side.

**What the masks are.** Each `ASSETS/GRAPHICS/<PATTERN>/<SKIN>.BM` carries a near-greyscale shading map, three 8-bit per-pixel weight masks (one per paint colour slot, summing to 255) and a 32bpp overlay. That is exactly the region table `paint.md` had recorded as absent from the game files — it was in the UI archive rather than the ZBD set. `src/Mech3/PatternLibrary.cs` (new) reads it; `src/Mech3/PlanePainter.cs` was rewritten to composite `shading * (w1*c1 + w2*c2 + w3*c3) / 255` with the overlay over the top, on the `.BM`'s own shading plane rather than the ZBD skin of the same name (different images).

**Patterns are per aircraft, and the whole stack now knows it.** `FORTUNE` covers all eleven planes; every other pattern covers one to three. `PatternLibrary.PatternsFor(prefix)` is that list and it drives everything — random liveries draw from the plane's own set, the livery lab's stepper walks it (`studio [4/4]` on a Fury), and `--paint=` validates against it, logging the aircraft's actual set when you name a pattern it lacks. The decisive confirmation: the Fury's four patterns (FORTUNE, BLCKSWAN, HUGHES, STUDIO) are exactly the four the user screenshotted from the original's paint UI.

**Two open questions closed by rendering against the references.**

*Slot order* — file order is `paint_color1..3`. Rendering the Fortune Hunters Bloodhawk with all three plausible assignments and comparing to `CustomPlane Paint1 Bloodhawk.png` singled one out: only *(red, black, white)* puts black on the outer wing panels with the white swoosh between them. White in both trims loses the black wing; black in slot 3 paints the swoosh instead of the panel.

*`player_fortune`'s missing colours* — the same test pins them at `223,0,41 / 0,0,0 / 255,255,255`, the shape every shipped scheme has (`hughes` is yellow/black/white). The red was already known from the paint UI swatch and a saved `.pln` at 0x68; the trims are read off the reference. Still an inference from artwork, not a value found in a file, and recorded as such.

**Verified.** All four Fury patterns render correctly — including the Studio Security blue with its white checkerboard, which the previous implementation could not produce at all because the Fury's ZBD skin is featureless near-black. The Fortune Hunters Bloodhawk matches the blueprint reference down to the black outer wing panels, white swoosh and canard edging. All ten aircraft paint in coherent Fortune Hunters livery with correct decals. `--paint=cccp` on a Bloodhawk logs `ships no skins for this aircraft — it has: BLAKE, FORTUNE, HUGHES`. Unpainted static screenshots remain **byte-identical by md5** to the pre-paint baseline; `--fly`, 4P stunt race, 2P mixed planes, static `--chapter`, the damage lab and the menu all build clean. Load cost **fell into measurement noise** (4P: 5537 ms painted vs 5606 ms unpainted, 3-run averages) — a flat multiply-add per texel replaced per-texel HSV conversion plus a percentile pass.

**What the rework fixed**, all of it consequences of the old approach inferring regions from hue: colourless regions now paint (the Bloodhawk's black outer wings), the Fury paints, slot order is data instead of a guess at region area, and region borders arrive antialiased in the masks rather than needing a soft hue falloff invented to stop them speckling.

**Superseded.** The hand-authored hue-window table is gone from the code. Its measurements survive in `paint.md` because they remain a true description of the ZBD skins — just not of where paint goes.

**Open:** the paint UI's "Shade" column is still unexplained (three Colour *and* three Shade dropdowns, but the masks give each slot exactly one colour); the overlay's channel order is consistent with alpha-over-RGB but the available content is greyscale and cannot prove it; `BROADWAY` and `ITSTAXI` ship masks but no `paint_pattern` names them, so they have no canonical colours.


### 2026-07-20 (same day) — Fix: `.BM` rows are bottom-up

User report on the pattern work: "the skin seems to be flipped in the front-tail axis — the Black Swan pattern on the Fury should look similar to the unpainted one, but the stripes are on the wrong sides of the wings and tail."

Correct, and it is a format-level fact rather than a plumbing slip: **`.BM` rows are stored last-to-first**, where the ZBD textures and PNG are top-down. `PlanePainter` now reads source row `h-1-y` for each destination row `y` (shading, all three masks and the overlay alike).

Diagnosed rather than guessed, because the first two attempts at a cheap test failed. Plain luminance correlation between the `.BM` shading map and the ZBD skin of the same name is near-useless — they are genuinely different images (one neutral, one carrying the paint keys), so the numbers came back weak and self-contradictory (`flipV` for some skins, `flipH` for others). Matching the mask's slot-1 region against the ZBD skin's own body-hue region did better (flipV 5, ident 1, flipVH 3) but still left doubt. What settled it was **edge-map cross-correlation** — panel lines and rivets are shared between the two images even though the colouring is not — run over all four orientations for all 55 same-sized `FORTUNE`/ZBD pairs, then restricted to pairs that can actually discriminate (peak > 0.25, margin > 0.08 over the runner-up): **flipV 24, ident 3**, holding every large margin (`bal_fuslage` 0.68 vs 0.02, `bri_wingbottom` 0.70 vs −0.02, `pea_spinner` 0.78 vs 0.09, `war_engine` 0.91 vs 0.59).

Verified visually the way the user framed it: the Black Swan Fury's rib stripes now sit exactly where the unpainted skin's do, and all four Fury patterns still match the reference screenshots (Studio Security's checkerboard, Hughes' black wingtips, Fortune's red-and-black). A texture-level three-way — ZBD skin, `.BM` as-is, `.BM` flipped — shows the flipped composite matching the ZBD orientation and the unflipped one mirrored, which is the same conclusion by eye.

**Open:** three skins (`fir_engine`, `dev_spinner`, `bri_reartop`) score *better* unflipped. All three are engine/spinner parts whose textures are near V-symmetric, so this is plausibly noise on an almost-tie rather than a per-file difference; the remake flips globally and they render correctly. Recorded in `docs/formats/rof.md`.


### 2026-07-20 (same day) — Livery lab: stepping a squadron loads its colours

User request: "switching the squadron in livery lab should change to the respective squadron colors." It previously kept the current colours and only swapped the mask layout, with a separate button to load the shipped ones — the wrong default, since a squadron *is* its colours.

`SelectPattern` now delegates to a shared `LoadSquadronLivery`, so stepping the Bloodhawk's three gives Fortune Hunters red with the fhunter logo, Blake blue-gray with the Blake logo and Hughes yellow with the Hughes "H", each complete. Decals move with the colours — a squadron's markings are part of its identity. Patterns vehicle.json names no colours for (`BROADWAY`, `ITSTAXI`) keep the current ones, since there is nothing canonical to load. The former "squadron colours" button became "reset to squadron colours", which is now its real job: undo slider edits without changing pattern.

*Bug found while verifying:* `CliArgs()` indexed `_catalog[_patternIndex]`, but `_patternIndex` indexes the **aircraft's** pattern list — different list, different length (12 vs 2–4). It never threw, it just compared against an unrelated scheme, so a freshly-loaded squadron livery printed the long `--paint-color=…/--paint-decal=…` form instead of the compact `--paint=BLAKE`. Now resolved by name.

Verified by stepping both a 3-pattern aircraft and a 4-pattern one: `--paint=BLAKE` → `FORTUNE` → `HUGHES` on the Bloodhawk and `BLCKSWAN` → `FORTUNE` → `HUGHES` → `STUDIO` on the Fury, each logging its own colour triple and decal set, with the panel swatches tracking and the CLI line back to its compact form.


### 2026-07-20 (same day) — "Shade" answered: it is brightness

User: *"The shade column is just brightness of the color. We have rgb sliders instead of combo boxes so we can match the color."*

That closes the last standing question about the scheme record. The original's paint screen offers a Colour dropdown (the hue family) and a Shade dropdown (how light or dark it is); their product is the single RGB stored in `paint_colorN`. There is no fourth field, and the region masks giving each slot exactly one colour is consistent with that rather than in tension with it. No code change — the remake exposes RGB sliders where the original had two dropdowns, which spans the same space and more, so any original livery is reachable by matching the colour directly.

It also **independently confirms `player_fortune`'s colours**, which until now rested on a single line of reasoning. The Bloodhawk paint-UI reference reads Colour/Shade of red/red, white/**black**, white/white — i.e. slot 2 is *white at black brightness* = **black** — resolving to (red, black, white). That is exactly the triple the three-way render test had singled out by matching black outer wing panels and a white swoosh against the same screenshot. Two unrelated routes to the same answer, so the inference is now well supported rather than merely consistent.

Recorded in `docs/formats/paint.md`; the remaining open items there are the overlay's channel order and the three near-V-symmetric skins that prefer unflipped rows.

## 2026-07-20 — Aircraft normals rendered inverted; mesh lab landed

User report: *"I have a hunch that the normals of the planes are not in the correct direction. Fury has some lighter patches for example seen from the top. From below the wing edge and tail are somehow lighted. I don't know where the lighting is coming from in the viewer."* The hunch was right; the location was not.

**The file data is clean.** Three mechanisms were eliminated by measurement before any code was written. Stored normals agree with their polygon winding on **0 of 1827** Fury triangles inverted (Bloodhawk 0/1505, Kestrel 3/2207) — but only once the probe triangulates the way `SceneBuilder.EmitPolygon` actually does, fan vs *alternating strip*. A first pass took a Newell normal over each polygon's raw index list, which is meaningless for a `triangle_strip` (that list is a strip, not a closed loop) and reported a false 7–14% inversion rate. Worth remembering: the wrong triangulation manufactured exactly the evidence the hypothesis predicted. Also ruled out: mirrored nodes (0 negative-determinant transforms across five aircraft) and importer damage (no `GenerateNormals`, no `Index` weld). Separately, `unk164` is **not** a usable bounding box — 0 of 89 Fury mesh nodes match their own vertex AABB and it is all-zero for most, so it is no shortcut for `PlaneCollider`.

**The renderer inverts them.** Our normals point to the polygon's visible side; that side is the CCW loop, which is Godot's *back* face — the reason single-sided aircraft surfaces use `cull_front`. So **every** visible aircraft fragment is back-facing, and Godot negates `NORMAL` on back faces. The normal reached the light calculation pointing into the airframe and every upward surface shaded as though lit from underneath, which is also precisely why the *underside* looked lit.

Diagnosed with the new mesh lab, in the order that mattered: culling was eliminated first — the wing stayed dark in **all four** cull modes (6.4 / 7.8 / 6.4 / 6.6), so sidedness was not the cause — and then a controlled light settled it. Viewed from above with ambient off:

| sun | panel | wing top |
|---|---|---|
| straight **down** (lighting the top) | 67.8 | **6.4** |
| straight **up** (lighting from underneath) | 146.6 | **104.4** |

A wing's upper surface, seen from above and lit from above, was black, and lit up when the sun moved beneath it.

**Fix:** `SceneBuilder.GetBiasShader`'s **shaded** path emits `NORMAL = -normalize(...)`, cancelling the engine flip. Correct for both sidedness cases — single-sided shows only the CCW side; a double-sided (`cull_disabled`) polygon gets the engine's flip on exactly the side that needs it. Fullbright substitutes an empty sign and its shader text is byte-identical to before, so **the world is provably unaffected** (it never reads NORMAL anyway). Under sun-straight-down from above, Fury wing top **6.4 → 73.8**.

**The reported "lighter patches" were never a separate bug.** They are the `pdpanelN_h` healthy damage panels — proved by `--damage=leftwing:0.1`, which replaced the bright wingtip patch with the torn-skin panel at exactly the same footprint. They stood out only because their light-grey texture survived the inversion while the near-black wing around them went to 6.4. After the fix their brightness ratio against the wing falls from **19.4× to 1.55×** and they are no longer visible. Matches `OriginalScreenshots/Fury from above.png` (brightly lit from above, no grey patches).

**Verification:** regression battery 0 errors across viewer × 3 aircraft, painted, static C1, `--fly`, `--stunt`, damage lab; unadorned `--viewer` still byte-identical by md5 with the lab present.

**New:** `src/UI/MeshLab.cs` (`--viewer`, **M**) — normal lines (per-face/per-corner; provenance / direction / winding colouring), wireframe with hard smoothing seams highlighted plus the engine's viewport wireframe, `PlaneCollider` boxes coloured by `PlaneDamage` part and split at x=0 where a slab could map to either wing, steerable lighting with a headlight mode, and `cull` × `normal source` override cyclers (16 combinations) whose materials replicate SceneBuilder's vertex stage verbatim so an override differs in the thing under test and nothing else. `--debug-mesh[=spec]` presets it for scripted screenshots. Two bugs caught during its own build, both worth keeping in mind: it must be constructed **after** the plane joins the tree (`GlobalTransform` on a detached node returns identity and logs per call), and it must **seed its light sliders from the scene** — a hardcoded default re-aimed the sun and broke the byte-identical viewer screenshot.

**Open:** aircraft carry evenly-spaced bright "comb" stripes along the wing and tail trailing edges. Texture-level (no wireframe edges bound them) and they survive painting, so a pattern ships no `.BM` for those skins. Pre-existing and unrelated to the normals; not chased. Also unverified: whether flight lighting wants re-tuning against the reference videos now that every aircraft is substantially brighter.

### 2026-07-20 (same day) — Fix: mesh lab crashed cycling the normal-source override

User report of `Index p_surface = 0 is out of bounds (surface_override_materials.size() = 0)` from `MeshLab.ApplyOverrides`, which they could not reproduce. The backtrace's line numbers placed `CycleRow` ~9 lines earlier than the current source, identifying it as a **pre-fix binary** — but chasing it anyway found a genuine defect and a genuine hole in the first fix.

**Root cause:** `SurfaceTool.Commit()` returns a **0-surface** mesh when the surface it was given carries no triangles. `AllSmooth` swapped such a mesh into the `MeshInstance3D`, which resized its `surface_override_materials` array to 0, and the next `SetSurfaceOverrideMaterial(0, …)` failed. Frame `<BuildUi>b__84_6` in the report is the *normal src [V]* cycler, which matches: only that cycler reaches `AllSmooth`.

**Two guards, because the first one checked the wrong quantity.** The initial fix tested `Mesh.GetSurfaceCount()`, but Godot bounds-checks the **instance's** override array, and those are different numbers — MeshInstance3D sizes the array at mesh-assignment time, so a mesh whose surfaces are committed afterwards (exactly what SceneBuilder does, committing into an already-assigned ArrayMesh) leaves it short. Now: `SmoothMesh` refuses to swap in a 0-surface mesh, and `SetOverride` checks `GetSurfaceOverrideMaterialCount()` and **recovers** — re-assigning the mesh to force the resize — rather than skipping. Skipping would leave a surface un-overridden without saying so, which in a diagnostic lab means drawing a conclusion from half an A/B; if recovery still fails it logs once, as a plain line.

**Testing note worth keeping.** The crash only ever appeared on the *button* path, which no one-shot `--debug-mesh` spec reaches — it takes a second `ApplyOverrides` after a mesh swap. Added `--debug-mesh=cycle=N` (steps the cycler N times at launch, the same convention as `--debug-livery=N`). The first attempt to validate it was worthless: reverting only `SetOverride` still passed, because the *other* guard was silently doing the work. Reverting **both** reproduced the user's error exactly (4 occurrences), and the fixed build passes across Fury/Bloodhawk/autogyro at `cycle=9` (2¼ full loops, so every transition). A regression test that has never been seen to fail proves nothing.

Plain `--viewer` re-checked against **full** stderr this time (the earlier byte-identical run had grepped only for the screenshot line, which would have hidden exactly this class of error): 0 ERROR lines, screenshot still byte-identical.

## 2026-07-20 — Node-name labels (T), in the viewer and in flight

User request: a toggle to show node names in the viewer, *"could be useful for the flight mode too to find nodes on wrong positions faster"* — which is the requirement that shaped it. Identifying a misplaced object only works if you can read its name off the object while flying past, so `src/UI/NodeLabels.cs` is built in both the static viewer and flight and covers the whole session subtree (world and aircraft alike). T cycles Off → Meshes → All.

Names come from the `cs_name` meta SceneBuilder stamps on every node, never `Node.Name`: Godot sanitises `.`→`_` and auto-renames duplicate siblings, so the Godot name is frequently not the name that appears in the game files — and a name you cannot grep for in the extraction is no use for this job.

**Three fixes, each answering a measured failure of the previous cut.** The first version anchored labels at node origins, capped at 300 m, ranked purely by distance. In flight that produced *three* labels, all of them on the player's own aircraft:

- **Anchor at the mesh AABB centre, not the node origin.** Gamez origins are frequently nowhere near the geometry they draw, and a great many world nodes share one, so origin anchoring both floated names off into space and collapsed them into a single screen cell. C1 in flight: **6 labels shown of 183 candidates → 14**, and now sitting on the objects (`rrbrdg3` on the rail bridge, `healthyboy` on the hangar, `gen_flare_yellow` on the town lamps).
- **Screen-space de-cluttering**, nearest-first on a 108×26 px grid with a 3×3 neighbourhood test (checking only the own cell would happily place two labels a pixel apart). This, not the radius, is what keeps the view legible — which is why `Radius` could be raised to a generous 1500 m. At the first cut's 300 m the ground was out of range *entirely*, because the flight camera sits several hundred metres up. Off-screen and behind-camera nodes are rejected before claiming a cell, or they burn label slots on text nobody can see.
- **The flown aircraft is deprioritised**, ranked after every in-range world node. It sits metres from the camera while the world is hundreds of metres away, so nearest-first spent every label on the plane you are sitting in. Deprioritised rather than excluded: a misplaced node can be on the aircraft too, and its labels still appear wherever the world leaves room.

Selection runs on a 0.35 s timer rather than per frame (positions are world-static and Label3D billboards itself, so the cadence is invisible), and the candidate walk is redone on the same timer because the flight tree genuinely changes — MapEdgeExtender adds and drops border tiles on cell crossings, and a one-shot walk would label ghosts and miss new ground.

Builds no nodes until first switched on, so an untouched session renders identically — the static viewer screenshot is still byte-identical. `--debug-names[=meshes|all]` presets it for scripted shots.

## 2026-07-20 — mech3ax fork: CS anim archives structurally decoded + byte-identical round-trip (plan items 1–3)

Work in `tools/mech3ax` (the user's fork), per `docs/plans/PLAN-mech3ax-cs-revival.md` (plan
re-verified same day after being drafted by a weaker model — four factual errors corrected:
deleted-code inventory undercounted ~2× and missed the 625-line `world/data.rs`, the anim-info
cross-check pointed at MW's 68-byte layout where 0x6c = 108 = PM's, the "trailing script pool
discrepancy" dissolved once PM's reader was read (PM *is* a trailing pool), and the
post-removal commit count was ~50, not ~24).

- **Item 1 (fork housekeeping):** `upstream` remote added (fork was already at rc3 head);
  `cargo build`/`cargo test` clean (141 tests); `test.py` run against this install via a
  `tools/test-versions/crimson-cs/zbd` junction + `strings.dll` copy — `--- ALL OK ---` on
  every already-supported CS format (sounds/interp/messages/reader/53 texture ZBDs), gamez +
  anim print the expected `SKIPPING`. So current upstream `main` still fully supports CS's
  non-gamez formats; only gamez + anim are the gap.
- **Item 2 (template study):** PM confirmed as near-verbatim donor by byte-probing the real
  C1 `cam_anim.zbd`: CS's anim-info block is PM's 108-byte `AnimInfoC` field-for-field
  (def_count 476 / script_count 48 / gravity −9.8 decoded), and the SI-script pool is PM's
  exact format — 28-byte `SiScriptC` headers then per-script names+frames, whose declared
  sizes run byte-exactly to EOF. That discovery retro-explains the 2026-07-18 survey's
  24-of-48 "parse failures": the survey's walker simply lacked the header block's declared
  counts. Upstream's `RotateDataC` also confirms the survey's undecoded rotate block
  (quaternion + delta + 3×16-byte spline blocks).
- **Item 3 (structural round-trip):** a Python walk model was iterated against the install
  until it delimited every def/seq/script in **all 61 archives** byte-exactly to EOF —
  discovering the full CS `AnimDefC` (272 B: PM's 268 + a u32-list ptr @264 with count in
  PM's `zero227`), 40-byte static-sound refs (PM 36), the live `unknowns` array (36-B
  records; PM asserts it empty), and the extra unnamed sequence behind `unknown_seq_ptr`
  (PM asserts NULL). Then `crates/anim/src/cs/mod.rs` (structural stage: typed structs for
  fixed records, event/frame regions preserved as raw bytes) landed with a
  `roundtrip_real_archives` test: **61/61 byte-identical**, clippy-clean. The zero-def is
  preserved verbatim (its flags vary per archive — C1/M05 has bit 21 set).

Format knowledge recorded in `docs/formats/anim-definitions.md` the same day. Next: plan
item 4 — semantic `AnimDef` field decode + event dispatch, cross-validated against the
already-decoded zrdr JSON for the same anims.

## 2026-07-21 — mech3ax fork: CS anim archives fully decoded semantically (plan item 4)

`docs/plans/PLAN-mech3ax-cs-revival.md` item 4: every `AnimDef` field, support array and sequence
event in the CS `cam_anim.zbd`/`mis_anim.zbd` archives is now decoded into mech3ax's shared
API types (fork commit `60a0603`), replacing item 3's raw-blob regions. Round-trip stays
**byte-identical on all 61 archives**; the SI-script frame data is the only remaining raw
region (item 5).

- **Architecture:** a new `EventCs` trait in `anim-events` with impls that delegate to
  `EventPm` — justified empirically: a data census over all 61 archives showed every event
  type shared with PM has PM's exact payload size. CS-specific pieces: e12
  OBJECT_MOTION_SI_SCRIPT (64 B `{0, node_index, script_slot, zeros}`), the two events
  beyond the known vocabulary — **e46 SOUND_ADJUST** and **e47 OBJECT_MOTION_SI_SCRIPT
  ALL_NAMES** (both named via the zrdr sources, payloads preserved raw) — and a CS
  `IF`/`ELSEIF` condition codec adding NODE_BELOW_ALT/ANIM_HEALTH/ANIM_HEALTH-range/
  NODE_ACTIVE plus the MAIN_ROOT_NODE (−100) sentinel.
- **Semantics recovered:** the item-3 "unknowns array" is the compiled **NAME1 node path**;
  the u32 list is the def's **SI-script pool indices** (e12 events index into it — item 5's
  record-attribution question answered as a side effect); flag bit 5 = "RESET_TIME key
  present" and bits 12/13 = SAVE_LOG, both pinned by the reader oracle; activation
  prerequisites ARE used (correcting item 3's note).
- **Verified:** 61/61 byte-identical (`cargo test` with `CS_ANIM_DIR`); the decoded
  `hangar3_doors` matches `C1/zrdr/hangar3.json` **field-for-field** (activation, SAVE_LOG,
  all 5 RESET_STATE ops, all 4 sequences incl. the duplicate `open_door2` name and the
  ±50 m / 9–10 s door motions) — the ground-truth oracle no other mech3ax game has; full
  workspace test suite green (MW/PM/RC unaffected); clippy adds no warnings vs baseline.
- Data quirks handled along the way: duplicate node names inside one def (C1/M02 references
  the *second* `pzep_interior` by index — disambiguated with a reversible `~N` suffix),
  garbage-padded name fields (preserved as pads), stale pointers and `wait_for` values,
  rotations beyond ±180°, negative light ranges, a handful of relaxed PM-corpus data bounds.

Format page updated the same turn (`docs/formats/anim-definitions.md` — compiled-archives
section rewritten as decoded fact). Next: item 5 (SI-script frame decode — now a bounded
question) then item 6 (CLI wiring + test.py).

## 2026-07-21 — mech3ax fork: CS SI-script frame decode complete (plan item 5)

`tools/mech3ax` stage 3 (fork commit `916c3d2`): the SI-script pool's frame data — the
last raw region of the CS anim decode — is now semantically decoded through the shared
machinery (`SiScript` API type + PM's `read_si_script_frames`/`write_si_script_frames`).
**All 1090 scripts across all 61 archives frame-decode byte-exactly, and the 61/61
byte-identical round-trip holds.** Both of the item's "known gaps" dissolved on
measurement (probes `.scratch/cs_anim_siprobe*.py`):

- **The 24/48 camera/`cpilot_eject` "parse-failure variant" never existed.** With the
  header-declared `frame_count`/`script_data_len`, the plain flags-driven walk consumes
  every script exactly; the 2026-07-18 failures were the survey's sentinel-guessing
  walker tripping over **flags=0 frames** (8,596 of 76,845 frames are a bare 12-byte
  header with no translate/rotate/scale blocks).
- **Rotate-block semantics decoded, beyond upstream's own bar** (mech3ax never
  interpreted the cubics): the per-axis `{value, c1, c2, c3}` blocks are cubics in
  **half-angle radians relative to the frame's base quaternion** (constant term 0;
  translate's cubics are absolute with constant = base component), `delta` = the cubic's
  average rate over the frame, and composition is left-multiplied quaternion exponential
  in the parent frame: `q(t) = exp((fx,fy,fz)(t)) ⊗ base`. A hypothesis race over all
  60,411 consecutive rotate-frame pairs of the install: L-exp closes 53,515 to <1e-5
  (57,675 to <1e-3); right-multiplication and all six Euler orders decisively lose.
  Residuals: cubic fit error on fast rotations (~1° per 60 ms ladder-climb frame) and
  `pfighter11.zan` (C1/M04) carrying **uninitialized spline memory** (coefficients to
  1.7e+27) under smooth base quaternions — bases are authoritative, splines interpolate
  within a frame only, and this is why spline blocks stay raw bytes (upstream's MW/PM
  choice too). Playback math for item 7 lives in the docs + module comment.
- `spline_interp` turned out to be a real per-script bool in CS (15 of 1090 false; PM:
  always false); name fields verified install-wide as exactly `strlen+1`, so the
  PM-style write reconstructs them byte-identically.

Verified: `cargo test` with `CS_ANIM_DIR` 61/61 byte-identical; full workspace suite
green; clippy clean on the crate. Docs updated same turn (`docs/formats/
anim-definitions.md` SI-pool + validation sections, plan item 5 DONE, CLAUDE.md format
table). Next: item 6 (CLI wiring + test.py CS anim skip flip).

## 2026-07-21 — mech3ax fork: CS anim CLI wired + full-install test.py pass (plan item 6)

`unzbd cs anim` / `rezbd cs anim` work end-to-end (fork commit `946b79d`, branch
`cs-anim`); test.py's CS anim skip is flipped (a `*_anim.zbd` glob for CS) and reports
`--- ALL OK ---` across the whole install — **all 61 cam_anim/mis_anim archives
byte-identical through the real zip pipeline**, all other suites unchanged. README
support matrix + CHANGELOG updated in the fork.

- The CLI arms required reshaping `cs::read_anim`/`write_anim` from item 3's
  whole-archive in-memory struct onto the `SaveItem`/`LoadItem` callback API all other
  games share (per-def/per-script items, `AnimMetadata` in/out); the common
  `ANIMATION_LIST` entry read/write was factored out for CS's header-counted list.
- CS container data with no `AnimMetadata` slot rides in new optional fields (item-4
  precedent): `base_files` + raw `ptrs` (`defs_ptr`/`scripts_ptr`/`world_ptr`/`unk40`/
  `zero_def_flags`). A 61-archive info-block survey (`.scratch/cs_anim_info_probe.py`)
  ruled out a PM-style `Mission` pointer table (56 distinct `defs_ptr`), pinned every
  other field constant for asserts, and found `unk40` = 0 exactly on the 13 multiplayer
  missions and `script_count` legitimately 0 (PM asserts it > 0).
- One bug only the JSON layer could show (the in-memory round-trip test never
  serializes): `carneypkup_cam.zan;camera1` (C5/M02) has a degenerate frame whose
  translate+rotate deltas are six `0xFFC00000` NaNs — serde_json writes NaN as `null`,
  which fails to parse back. A field-by-field survey (`.scratch/cs_anim_nanprobe.py`)
  confirmed these are the install's only non-finite decoded floats, so
  `TranslateData`/`RotateData`/`ScaleData` gained an optional bit-preserving
  `delta_raw` (same idiom as the adjacent `garbage` f32-as-u32 field); MW/PM/RC JSON
  output is unchanged. Also registered the item-4 event types + `AnimPtrs` in
  metadata-gen (item 4 had skipped registration).

Verified: test.py `--- ALL OK ---` (61 anim archives + sounds/interp/messages/reader/
textures unchanged); full workspace `cargo test` green incl. the in-memory 61/61
round-trip; clippy clean on every touched crate. Docs same turn: plan item 6 DONE,
`docs/formats/anim-definitions.md` (CLI + NaN-delta quirk), CLAUDE.md format table.
Next: item 7 — consume in this project (ExtractAssets.ps1 anim mode, SI-script playback
for the train/trucks, backlog cleanup).

## 2026-07-21 — mech3ax fork: Track A started — CS gamez module recovered + port scoped (plan items 8–9)

Track B's item 7 (consume anim in this project) skipped for now by user decision; Track A
(`gamez.zbd`/`planes.zbd` revival) started instead.

- **Item 8 — recovery:** the deleted CS gamez/nodes code is on disk again as a detached
  git worktree at `tools/mech3ax-cs-ref` (commit `0e8707b` = `7f592ec~1`, the last commit
  CS compiled against; read-only reference, `git worktree remove` disposes of it).
  Inventory matches the plan's framing exactly (gamez `cs/` 5,089 lines incl. the eight
  per-chapter texture tables + planes.rs; nodes `cs/` 2,509 lines, seven node kinds;
  api-types `cs.rs` 105). The removal commit's full diffstat (63 files, −7,968) also
  catalogues 13 wiring files outside `cs/` the port must restore (api-types mods, common
  consts, node flags/math, lib FFI arms, metadata-gen, CLI bail arms, test.py).
- **Item 9 — port scoping:** ~29 infra commits between `7f592ec~1` and `main` read and
  classified into an 8-point list (in the plan, section 9). Headlines: upstream unified
  everything into a **single `GameZ` struct + single `Node` API type** for all games
  (`04da5ce`) — the port maps CS onto those instead of reviving `GameZDataCs`/`NodeCs`;
  node read/write moved to `crates/gamez/src/nodes/<kind>/{mw,pm,rc}` organized by kind,
  with camera/display/window/object3d now game-shared; materials reference textures **by
  index** (`4df8963`), making CS's `TextureName` dedupe/rename machinery obsolete (flagged
  for item 13: the extraction JSON will intentionally differ from v0.6.1's renamed
  `.-N` names our Godot TextureArchive compensates for); plus the Count/Index newtypes +
  `chk!` idiom, the `api!` macro type system + metadata-gen registration, PM header slot
  32 = `node_last_free` vs CS's `light_index`, and `model/ng`→`model/pm` mapping CS's old
  imports 1:1. Confirmed current `pm/mod.rs` still uses the `data::Campaign` shape old
  CS mirrored — PM stays the donor.

No code written yet (both items are reference/analysis by design). Next: item 10 — the
actual port, starting with the field-set comparison of CS's old 435-line `node.rs` /
625-line `world/data.rs` against the unified `Node`/`World` API types.

## 2026-07-21 — mech3ax fork Track A items 10–12: Crimson Skies gamez/planes revival

**The CS `gamez.zbd`/`planes.zbd` support upstream deleted in `7f592ec` is back in the
fork, and every archive of this install round-trips byte-identically — including
`planes.zbd`, which closes the 72-byte diff this project has carried as a known nit since
2026-07-14.** Fork commit `df16d8e`. Items 10 (port), 11 (CLI) and 12 (verification)
landed together, because the CLI and `test.py` were what made the port verifiable at all.

**The port targets the unified API, as item 9 scoped it.** No `GameZDataCs`/`NodeCs`
revival: CS maps onto the shared `GameZ`/`Node`/`NodeData` types, with CS-only fields added
as optional (absent for MW/PM/RC). New code is `crates/gamez/src/gamez/cs/` (`mod.rs`
header + top-level read/write, `models.rs`, `fixup.rs`, `nodes/{read,write}.rs`, `data/`
plus the nine per-chapter texture-pointer tables ported verbatim) and `cs` submodules under
the by-kind node dirs (`nodes/node/cs/`, `nodes/world/cs/`, `nodes/light/cs/`,
`nodes/camera/cs.rs`). **CS's lod, window, display and object3d node data reuse the shared
readers unchanged** — a real saving over the deleted module, which carried bespoke copies
of all four. The old `TextureName` dedupe/redupe machinery is gone entirely, as item 9
predicted (materials reference textures by index now).

CS divergences that needed new API surface:

- **`GameZMetadata.model_slots`** — the CS model array interleaves live models with free
  slots. Rather than force `Vec<Option<Model>>` on all four games (what the old CS code
  did), `models` stays a dense `Vec<Model>` and this records each model's original array
  slot; node model indices are remapped dense↔slot on read and write.
- **`ModelFlags::HARDWARE_RENDER`** — MW/PM/RC *synthesise* this on write from the model
  type, but CS sets it independently of the model type, so it has to be stored.
  `write_model_info_cs` skips the synthesis. This was the second data-driven failure the
  round-trip test caught (`0x107` read back as `0x187`).
- **`ModelFlags::UNK8`** (bit 8, CS-only; the first failure the test caught — PM's flag
  set stops at bit 7), **`Node.field040`**, **`World.flags`**,
  **`World.virt_partition_min_x`/`_min_z`** (PM hardcodes 1, CS varies),
  **`World.child_value`**, **`WorldPtrs.children_ptr`**, **`NodeFlags::UNK12`**.
- **`Partition.x`/`z` widened `u8` → `i16`** — CS carries partial partition values (x ==
  −1 with z a real index) that `u8` cannot represent. This is the one change visible in
  the other games' JSON, though their values are unaffected.
- `GameZMetadata.node_last_free` carries CS's header slot 32, which is the **light node
  index** (hardcoded 2338 for `planes.zbd`), per item 9 point 6.

**The 72-byte `planes.zbd` diff was never CS-specific — it was a general `Ascii`
asymmetry.** `to_str_suffix` decodes `prefix\0suffix\0` by restoring a period at the
**first** zero, but `from_str_suffix` re-encoded by converting the **last** period. Those
agree for ordinary names (`foo.tif`), and disagree whenever the stored suffix itself
contains a period: `planes.zbd` has 18 such names, e.g. `bldhwk_cowling\0.tif\0`, which
decodes to `bldhwk_cowling..tif` and was re-encoded as `bldhwk_cowling.\0tif\0` — the
"swapped `\0`/`.`" the old note described. Added `Ascii::from_str_suffix_first` as the true
inverse of the decode and used it from the CS texture writer only, so MW/PM/RC output is
untouched. 18 names × 4 bytes = the 72 bytes exactly.

**Also fixed here, pre-existing and unrelated to this port:** `metadata-gen` panicked
during type resolution — Track B item 4 added `NodeBelowAlt`, `AnimHealthRange`, `NamePtr`
and `NamePadPtr` but never registered them (item 6 registered only some of its new types).
Confirmed pre-existing by stashing this port's changes and reproducing on a clean tree. The
generator now completes for both the C# and Python backends. (Its output dirs must exist
but not the `AutoGen`/`autogen` leaves — it uses `create_dir`, not `create_dir_all`.)

**Verified:** in-memory round-trip **9/9 byte-identical** (`cargo test` with
`CS_GAMEZ_DIR` pointed at the install's `ZBD` — new `roundtrip_real_archives` test,
skipped when unset so a data-less CI stays green); `test.py` **`--- ALL OK ---`** on the
full install through the real `unzbd cs gamez` → `rezbd cs gamez` zip pipeline, which also
exercises the JSON serialization layer (`test_gamez`'s CS skip replaced with a
`gamez.zbd`/`planes.zbd` glob pair); every other suite unchanged (sounds/interp/messages/
reader/textures, and the anim suite still 61/61, i.e. Track B is undisturbed); full
workspace `cargo test` green; manual CLI round-trip on C1 md5-identical; `cargo clippy`
**below baseline** (49 vs 52 warnings on the touched crates, none in the new CS files —
the `Partition` widening made 8 existing `.into()` conversions no-ops, now removed).

**Downstream note for plan item 13 (the extraction cutover):** the fork's JSON is
deliberately **not** shape-compatible with the pinned v0.6.1 output the Godot project reads
today. `nodes.json` was a tagged union per node (`{"World": {…}}`) and is now a flat `Node`
with the variant under a `data` key and several fields renamed (`node_index`→`index`,
`parent`/`children`→`parent_indices`/`child_indices`, `unk040`→`field040`);
`textures.json` was `{original, renamed}` and is now `{name}`, which makes
`TextureArchive`'s `.-N` duplicate-rename workaround unnecessary; `metadata.json` lost
`texture_ptrs` and gained `model_slots`. So item 13 is a port of `src/Mech3/GameZ.cs` +
`TextureArchive.cs` with viewer smoke tests, not a binary swap — which is exactly the
conservatism that item already called for. Nothing in the remake changes until then: it
still runs on the pinned v0.6.1 binary.

## 2026-07-21 — extraction pipeline cut over to the mech3ax fork (plan item 13)

`extracted/` is now produced by the fork build (`tools/mech3ax/target/release/unzbd.exe`)
instead of the pinned v0.6.1 binary. The fork is the only build with CS `gamez`/`planes`
support that round-trips byte-identically (v0.6.1 leaves a 72-byte `planes.zbd` diff), so
this closes the last dependency on an unmaintained binary for the formats the remake
actually reads.

**This was a loader port, not a binary swap.** The fork targets upstream's unified API, so
its JSON is deliberately shape-incompatible: `nodes.json` went from a tagged union per node
(`{"Object3d": {…}}`) to a flat node with the variant under `data` (`mesh_index`→
`model_index`, `children`→`child_indices`, `transformation`→`transform`, whose "no
transform" case is the string `"Initial"` and whose stored matrix moved from `matrix.a…i`
to `original.r00…r22`); `meshes.json`→`models.json` (polygon `unk04`→`priority`,
`triangle_strip`→`tri_strip`; mesh light `extra`→`vertices`); materials reference textures
by `texture_index` into `textures.json` rather than inlining the name, and texture entries
went `{original, renamed}`→`{name}`; partition cells `nodes[].index`→`values[].node_index`;
reader archive entries `ia.json`→`ia.zrd.json`; `interp.json` `last_modified`→`datetime`.

**`GameZ.cs`, `TextureArchive.cs` and `Zrdr.cs` now read *both* shapes.** That was a
deliberate choice over cutting only to the new one: it costs a handful of `TryGetProperty`
fallbacks in the idiom the files already used for `unk04`/`priority`, it makes the rollback
this step called for a pure data operation (`ExtractAssets.ps1 -Unzbd <v0.6.1 unzbd>` with
no code revert), and it let the port be *proven* by rendering the same scene from both
trees.

**Two real breakages found by measurement, not assumed:**
- The fork also exposes a per-node `index` field — 1-based, with duplicates, i.e. v0.6.1's
  `node_index` renamed. `child_indices` are **not** in that space; they are flat list
  positions like before. Confirmed by consistency count (6553 parent/child agreements as
  positions vs 59 as index values) before writing the parser. Getting it backwards would
  have rebuilt the scene graph wrong, silently.
- `bldhwk_cowling..tif`: v0.6.1 hid the 35 duplicate `bldhwk_cowling` texture-table entries
  behind `.-N` renames, so `TextureArchive` never met the doubled period that the
  `prefix\0suffix\0` decode produces when the suffix is empty. The fork stores the true
  name and it resolved to nothing — fixed with a trailing-dot strip in `Resolve`.

`ExtractAssets.ps1` gained `-Unzbd <path>` (defaults to the fork build, errors with build
instructions if absent) and no longer dies on the extractor's stderr: PowerShell 5.1 wraps
a native exe's stderr in ErrorRecords, which `$ErrorActionPreference = "Stop"` turns
terminating, so unzbd's output is now captured and judged by exit code. Upstream mech3ax
emits one `object3d transform fail` warning per node whose euler angles don't recompose to
the stored matrix bit-for-bit (155 across a full run) — informational, since the original
matrix is preserved verbatim and the Godot loader prefers it, so those are counted and
summarized rather than printed. Anything else on stderr is surfaced.

**Verified:** the ported loaders render the C1 Bloodhawk **md5-identical** to a pre-change
stashed baseline *and* md5-identical from the fork tree — a three-way match that pins both
"no regression on the old tree" and "the two shapes are equivalent". Before the swap:
node count/order, every `model_index`, every node name (except the Display node, unnamed in
v0.6.1 and `"display"` in the fork), the `"Initial"`↔null correspondence (3675/3675) and
partition refs (346/346) all match; scale is unit on all 4181 transformed nodes, so ignoring
it is safe; all 881 C1 texture PNGs are **pixel**-identical (all 881 differ byte-wise —
encoder only); all 222 readers are semantically identical; `soundsh.zip` is **byte**-identical;
`interp.json`/`messages.json` match apart from the key rename. After the swap: a full
re-extraction of all 184 archives with zero failures, then a 12-case battery (viewer × 2
aircraft, damage lab, `--fly` on C1/C1B/C2B/C3/C4/C5, `--stunt`, a 4P race, static C2,
launchscreen) clean on the zipped tree and re-run on the **unpacked** tree to exercise the
directory code paths, and the four frame-deterministic shots md5-identical to v0.6.1-tree
renders. The damage-lab shot differs but is not frame-deterministic at all: its fire trails
vary 413–479 px between runs on a single tree, and the cross-tree difference (433–473 px)
sits inside that floor — measured rather than waved away.

**Pre-existing gap surfaced (not caused here):** C3's gamez references `cloud1.tif`/
`cloud2.tif` but C3's `texture.zbd` ships neither, identically in both trees (every other
chapter ships them), so flying C3 logs two "not found in archive" lines. It belongs on
`TextureArchive.KnownAbsentFromGameData` next to `pir_spinner`/`barngrill`, but that is a
visible behaviour change (neutral gray instead of debug magenta) and was left for the user
to decide.

The pinned v0.6.1 binary stays in `tools/` as the documented fallback, and `cam_anim`/
`mis_anim` remain skipped by `ExtractAssets.ps1` — the fork extracts them byte-identically,
but nothing consumes them until plan item 7.

## 2026-07-21 — mech3ax CS revival plan item 14: upstream PRs prepared (not opened)

Split the fork's single working branch into the two independently-mergeable upstream PRs the
plan called for, both off `cbb838f` (0.7.0-rc3): `pr-cs-anim` (Track B `cam_anim`/`mis_anim`,
4 commits) and `pr-cs-gamez` (Track A CS `gamez`/`planes` revival, 1 commit). `cs-anim` was
rebuilt as the integration branch of both and the local release binary rebuilt from it, so
`ExtractAssets.ps1` and `extracted/` are unaffected.

Making them independent — rather than stacking gamez on anim — surfaced a genuine defect. The
anim work did not stand alone: `metadata-gen` panicked at type resolution (`type
mech3ax_api_types::anim::events::NodeBelowAlt required by Condition.NodeBelowAlt not found`),
because the codegen registrations for four types the *anim* work introduced had been written
later, during the *gamez* work, and lived in the gamez commit. The 2026-07-21 gamez entry
diagnosed this correctly but fixed it in the wrong commit; anyone taking the anim work alone —
i.e. an upstream reviewer — got a broken generator. The registration and its changelog line
moved into the anim branch (amended into stage 4, beside its sibling CS event registrations);
the gamez branch no longer touches `metadata-gen`.

Also decided **against** the plan's tentative third PR for the `Ascii::from_str_suffix_first`
fix (the 72-byte `planes.zbd` diff): as implemented it is purely additive and its only caller
is the CS texture writer, so a standalone PR would be dead code upstream. It stays in the
gamez PR as its own changelog entry, with the alternative (fixing `from_str_suffix` itself)
offered in the PR body but not taken — it would change MW/PM/RC write behaviour and there are
no MW/PM/RC installs here to verify against.

Per-branch doc conflicts (`README.md` support matrix, `CHANGELOG.md`) were resolved so each PR
claims only its own support; the integration branch claims both.

**Verified independently per branch**, not just in combination: `cargo build --workspace` and
`cargo test --workspace` clean on both; `cargo run -p mech3ax-metadata-gen` clean on both (389
files on anim, 375 on gamez); `test.py … --release` → `--- ALL OK ---` on both, each correctly
reporting `SKIPPING crimson-cs` for the other track's suite. On the re-integrated `cs-anim`:
`--- ALL OK ---` with nothing skipped — all 61 anim archives and all 9 gamez archives
byte-identical. The proof that the split changed no behaviour: `git diff df16d8e cs-anim`
(pre-split tip vs. re-integration) is **one reordered CHANGELOG line**; the code trees are
identical. Test outputs in `.scratch/mech3ax-test-{anim,gamez,both}/` (git-ignored).

Deliverables in `docs/plans/upstream-pr/`: `README.md` (branch table, split rationale, verification,
push commands), `pr-0-discussion.md` (a short pre-PR question to upstream — was dropping CS
`gamez` a bandwidth call or an architectural one? — which determines how much polish the gamez
PR is worth), and `pr-1-anim.md` / `pr-2-gamez.md` as ready-to-paste PR bodies.

**Nothing is pushed and no PR is open.** Pushing the branches and all upstream communication
are the user's, per the project's division of labor.

**Same-day, outward-facing steps (user-owned):** both PR branches pushed to the fork; issue
[#3](https://github.com/TerranMechworks/mech3ax/issues/3) posted asking whether dropping CS
`gamez` was a bandwidth call or an architectural one; the anim PR opened; the gamez PR prepared
and deliberately held pending the answer to #3. A standing rule was decided in the same turn —
**all outward-facing communication about this work discloses that it was done with the help of
Claude Code** (PR bodies, issues, comments, writeups, not just the initial submission). Both PR
bodies carry the disclosure above their technical content; commits already carried
`Co-Authored-By: Claude`; issue #3's opening post predated the rule, so a follow-up disclosure
comment was posted to that thread before either PR reached a reviewer.

## 2026-07-21 — Animation playback + spectator camera (revival-plan item 7)

The payoff for the whole mech3ax fork effort: the compiled `cam_anim`/`mis_anim` archives are
now consumed, and the world animates. Planned as its own run in `docs/plans/PLAN-anim-playback.md`
after a format-analysis pass over all 61 extracted archives.

**Analysis findings that shaped the design** (all in `docs/formats/anim-definitions.md`):

- Extraction was already wired (the working-tree `ExtractAssets.ps1` change); all 61 archives
  were already extracted and unpacked under `extracted/`.
- **The two sources are complementary, not redundant.** A mission's `mis_anim.zbd` compiles
  only the defs its `mis_anim.json` lists — C1/IA1 is 160 defs, all eight zeppelin files —
  and `zepstate`/`startanims` are **never compiled into any archive**. So the reader path
  could not be replaced, only merged with. Compiled wins on collision.
- **Reader op keys and compiled event tags are one vocabulary**: SNAKE_CASE → PascalCase maps
  exactly across the whole event set, so both front-ends normalize into one model mechanically.
- **A support ref's `ptr` IS the flat gamez node index** — verified exactly on **136,048
  references across all 8 chapters**, the only apparent exceptions being the fork's own
  reversible `~N` duplicate-name suffixes. This is the correct binding and it matters: C1 has
  a `caboose` (the real consist under the world root) and an unrelated `caboose.flt` in the
  rail yard, and name matching drove **both**, putting one somewhere wrong. Each def's
  `objects`/`nodes` arrays are its symbol table. `SceneBuilder` now stamps `cs_index` alongside
  `cs_name` for it.
- **Event scheduling** is `start.offset` ∈ Animation/Sequence/Event + time, with an absent
  `start` (174,938 of the install's events) meaning "when the previous event completes". The
  discriminating case is the train: `[ObjectMotionSiScript, Loop{-1}]` with no offsets is a
  327 s loop only under that reading. A def's sequences run **concurrently** — the train drives
  its four cars from four sibling sequences.
- **JSON trap: the `.zan` rotate quaternion's field labels are shifted**, because mech3ax reads
  the file's `(w,x,y,z)` into a `#[repr(C)] {x,y,z,w}` struct. Invisible to the byte round-trip,
  fatal to playback. Verified by remapping and checking the C1 engine's quaternions are
  unit-norm and its yaw tracks the frame-to-frame chord heading to ~1°.

**Landed:** `src/Mech3/CompiledAnim.cs` (compiled front-end + SI scripts, lazily loaded; event
payloads a generic property bag rather than 35 DTOs), `src/Mech3/AnimDefs.cs` (rewritten as the
zrdr front-end onto the same model), `src/Mech3/AnimProgram.cs` (merge + script resolution),
`src/Mech3/AnimRuntime.cs` (the engine — bootstrap passes, instance clocks, event scheduling,
control flow, dispatch table). `src/Mech3/MissionState.cs` absorbed and deleted, its node
resolution and INACTIVE semantics carried over verbatim. Plus `src/Flight/SpectatorCamera.cs`
and `--freecam` (built first, as the testing vehicle) and `--debug-anim`.

**Verified:** the C1 train drives its four-car consist from the parked position in correct
order and spacing; the hangar-3 doors swing over their authored 9 s; the C1 road vehicles run
their `OBJECT_MOTION_FROM_TO` chains — all via the headless `--debug-anim` pose log. **The end
state provably matches the old code**: a late-frame hangar view differs from the pre-change
baseline by **8 px of 230,400 (0.00%)** while an early frame differs by 1.16% — the transition
is real and the destination identical. All 8 chapters build with zero errors; the mode battery
(fly/stunt/viewer/damage/2P/4P-race/menu) is clean; static `--viewer` is byte-identical (md5).
Load time is unchanged (1785 ms vs a 1685 ms baseline — an alarming first-run 8.1 s turned out
to be a cold OS file cache on 630 freshly written JSON files).

Two bugs caught during the work, both from reading the output rather than the code: motions
were being ticked once per running sequence (the train would have run at 4× speed), and two
start anims legitimately drive the same hangar doors, so motions now follow one-per-node
last-wins. `CallAnimation` chains are depth-capped because the data can cycle.

**Deliberately not done, with reasons recorded:** `ObjectMotion` (7,442 uses) is the
debris-scatter primitive and every use sits in a destruction sequence reachable only by
`WeaponHit`/`ON_CALL` — substantial work, no observable effect at mission start. `If`/`Elseif`
branches are **skipped, not guessed**: their conditions are gameplay state a world build has no
value for, and a wrong guess silently poses objects incorrectly. Nine event kinds (puffers,
sounds, lights, opacity, texture cycling, FBFX, camera) dispatch and are counted but not yet
acted on — each is one `case`, listed in `backlog.md`. One unexplained delta is written down
rather than rounded off: the safety net hides 54 uncovered `destroyed` subtrees where the old
code hid 53 (no visible difference — the net catches it either way).

**Same-day fix (user report): F11 camera pose was wrong in `--freecam`.** `PrintCameraPose`
branched on `_fly` alone, so the spectator camera fell into the orbit branch and printed
`_orbitCenter` — which `--freecam` never sets, because it skips `FrameCamera`. Every pose
therefore aimed at the world origin. Both free-look modes now project the look-at along the
view direction, 100 m out rather than 1 m: the printed args round to 3 decimals, and at world
coordinates in the thousands a 1 m offset quantises the reconstructed direction to ~0.06°.
Verified by round-trip: a requested look-at direction and the printed one normalise to the
identical unit vector (delta length exactly 100.0 m), and pasting the printed pose back
reproduces the view to 10 px of 230,400 (0.004% — the residual is the animated world moving
between the two runs, not the camera).

**Same-day (user request): `PUFFER_STATE` implemented — the train has steam.** `Puffer` gained a
third emission mode, `SustainAt`: continuous `TIME_INTERVAL` emission at a **moving** node, which
is what an animation's `ACTIVE_STATE 1` asks for and which neither the one-shot `Burst` (fixed
point, finite pool) nor the distance-driven `TrailAdvance` could express. `PufferState.FromAnimEvent`
parses the compiled payload; `AnimRuntime` keys one emitter per (puffer name, host node), builds it
through a caller-supplied `PufferFactory` (a puffer bakes its atlas at construction and the session
`TextureArchive` dies with the build scope, so the factory is cleared afterwards), and drives each
from its host's world pose every frame. **User-confirmed in-game**: the C1 train trails a steam
plume along the track. C1 raises 4 emitters (the waterfall's three splash puffers + the train's
steam), C3 2, C4 9 (three waterfalls × three), C5 3; all 8 chapters error-free.

Three things surfaced on the way, none of them the puffers:

- **A latent binding bug that would have broken exactly this work.** `AnimDefinition.NodeRefs`
  admitted `lights`/`puffers`/`dynamic_sounds` alongside `nodes`/`objects`. Measured over
  C1/C2/C4/C5: **every one** of those three arrays' 2,616 `ptr` values is outside the node range
  (they are runtime pointers), while `nodes`/`objects` resolve 100%. Left in, a PUFFER_STATE's own
  name would have bound to a bogus index and resolved to nothing. Restricted to the two verified
  arrays.
- **A zero-duration `Loop` busy-spin.** C1's waterfall is `[PufferState ×3, Loop{-1}]` — all
  instantaneous, so the sequence re-ran as fast as the per-frame guard allowed. A loop iteration
  that consumed no time now yields to the next frame. This alone cut the wasted dispatch counts by
  an order of magnitude (`ObjectOpacityState` 3,614→58, `If` 3,258→71, `LightState` 2,042→518).
- **A wrong diagnosis, corrected.** The spectator camera appeared to drift on its own (99.8% of
  pixels changed over 120 idle frames) and was attributed to a phantom device reporting
  `LeftY = -1.000` at rest; a rest-baseline calibration was written to neutralise it. The real
  cause was the **user holding a controller stick in another game** — Godot polls gamepads
  regardless of window focus, unlike keyboard/mouse. The calibration was reverted (baking "rest"
  at launch would go deaf to a direction held at startup) and re-measured at **0/57,600 px** drift
  with the stick centred. Open question recorded for the user: whether pad reads should be gated on
  window focus project-wide, which would also stop a flight taking stick input while alt-tabbed.

Also fixed here: `--debug-anim` now reports whether each animated node is **visible in tree**, since
a correctly-animated node inside a deactivated subtree moves perfectly and renders nothing.

## 2026-07-21 — Animation transform semantics: absolute poses, radians

User-reported mission-state divergences in C1/IA1 (starting with "missing animated cars")
traced to a single root cause in `AnimRuntime`, not to per-object gaps.

**Every transform channel in the anim data — `translate`, `rotate`, `scale`, in both the
`*_STATE` events and `OBJECT_MOTION_FROM_TO` — is an absolute pose in the node's own parent
frame, and rotations are in radians.** The runtime treated all of them as offsets from the
authored rest pose and ran rotations through `DegToRad`. Full evidence in
`docs/formats/anim-definitions.md`; the decisive measurements:

- C1's `mafia` car is authored to drive `(-6796, 128, -5958) → (-6521, 128, -5958)`, and its
  gamez node rests at `(-6795.828, 128.0, -5956.72)` under the `world1` root. Adding the two
  put it at `(-13574, 256, -11914)` — exactly double, y included, ~7 km off the map. Every
  `OBJECT_MOTION_FROM_TO`-driven world object was displaced the same way, which is why the
  C1 traffic was "missing": it was rendering, in the wrong hemisphere of the map.
- 627 of the resolvable `OBJECT_TRANSLATE_STATE` uses have `STATE` exactly equal to the node's
  rest translate — the same doubling. The 367 zero-valued ones were no-ops by luck.
- Rotation magnitudes peak at `15.708 = 5π` with 99.93% ≤ 2π and 228 on exact π/2 multiples,
  so the values are radians; `DegToRad` made every rotation ~57× too small and nothing visibly
  turned.

Landed: `FromToMotion` rebuilt around absolute channels (with the previously-unhandled
`*_delta` channels applied on top of the rest pose), and `PoseTranslate`/`PoseRotate`/
`PoseScale` likewise. `PoseTranslate` still calls `RestOf` purely to record the authored pose
before disturbing it, so a later motion on the same node doesn't cache an already-moved pose
as its rest.

**Verified:** the C1 traffic (`mafia`, `police_car`, `black_car1`, `car_go_home`, `car_loop1`)
now runs the airfield road at y=128 inside the user's reported viewpoint instead of at y=256
seven kilometres away; all 8 chapters build with **zero errors and identical state-op counts**
to the pre-change run (C1 3533/91, C5 4150/23), and the hangar-3 door and train poses are
unchanged frame-for-frame, so the previously pixel-verified behaviours did not regress.

**Also surveyed, not yet acted on:** all ten `IF`/`ELSEIF` condition kinds are evaluable
(`RandomWeight` is a dice roll, `AnimHealth` is full health in a fresh world, `AnimationLod`/
`HwRender`/`PlayerFirstPerson` are our own settings, `PlayerRange`/`NodeActive`/`NodeBelowAlt`
are live scene state). The runtime still skips every branch, which is what suppresses C1's
refinery flame lighting and lighthouse sparking — both gate their entire light sequence behind
`If { AnimationLod: 2 }` / `If { RandomWeight: 0.7 }`. Table in the format doc.

## 2026-07-21 — Mission object rosters: the mission zrdr scope is a library, not a manifest

Follow-up to the transform fix, from three user observations of the original C1/IA1 (stunt
flying): a Hollywood Knights zeppelin sits on the field that should not be there, the
Passenger Hangar should contain a zeppelin, and a cargo train should be parked in the cut
below the terminal. All three were confirmed against the game data and reference screenshots.

**Root cause for two of the three:** `AnimProgram` applied every reader definition in the
mission zrdr scope unconditionally. But a mission folder ships reader files it never uses —
C1/IA1 carries a `zepstate.zrd.json` hiding `dliner1` and `cargotrain`, and its compiled
`mis_anim` contains neither. The missions that genuinely hide them (C1/M04) compile both.
The rule, verified over every zepstate in the install:

| Mission | zepstate defs | compiled? |
|---|---|---|
| C1/IA1, C1B/IA1, C1C/IA1, C2/IA1, C2B/IA1, C4/IA1 | `dliner1`, `cargotrain` | **unused** |
| C1/M02 | `hk_zep`, `lkshadow`, `tethershadow`, `tethertower` | COMPILED |
| C1/M04 | `dliner1`, `cargotrain` | COMPILED |
| C3/IA1, C3/M02, C3/M03, C4/M03 | `cargozep1` | COMPILED |

C3/IA1 compiling `cargozep1` is why the rule is "consult the mission's compiled manifest",
not "Instant Action ignores zepstate". Two independent user observations corroborate it:
C1/M04 shows the field zeppelin with an empty hangar and no parked train (exactly what
compiling both state defs produces), and C1/M02 is the one mission where the tether tower
disappears — the only mission that compiles `tethertower`.

Landed: mission-scope reader defs are now gated by the mission's compiled manifest, and only
when that manifest actually loaded (an extraction without `mis_anim` degrades to the previous
behaviour rather than to an empty program). Shared and chapter scopes stay unconditional —
they are the world's furniture, not a per-mission roster.

**Verified:** C1/IA1 skips exactly `dliner1/dlinerstate` and `cargotrain/cargotrainstate`
(3533 → 3531 state ops, i.e. only those two hides), the Passenger Hangar zeppelin and the
parked train both render in their reference positions, and the per-chapter skip counts match
the data survey exactly — 2 for C1/C1B/C1C/C2/C2B/C4, 0 for C3 (compiles its def) and C5 (no
zepstate). All 8 chapters build with zero errors.

**Corrects a recorded finding:** the docs previously stated that `zepstate` "is never
compiled into any archive", generalised from C1/IA1. It is compiled into the missions that
use it; being *uncompiled* is precisely the signal that a mission does not instantiate it.

**Still open — `hk_zep`:** the field zeppelin is an AI-vehicle entity, not scenery.
`aiv.zrd.json` is the roster (C1/IA1 lists only the player; C1/M02 lists `hk_zep`; C1/M04
lists `hk_zep` and `piratezep`), and `zeppelins.zrd.json` the flyable-zeppelin roster
(C1/IA1: `multiplayer1zep`). Nothing in IA1 scope hides `hk_zep`, so it needs the second half
of the rule: a mission-spawned entity's gamez geometry is inactive unless that mission's
roster names it. Not yet implemented — `dliner1` must stay visible under it, so the entity
set has to come from the rosters themselves rather than from a name pattern.

## 2026-07-21 — Puffer bugs: reader-duplicate instances, dropped ACTIVE_STATE, dropped AT_NODE offset

User report: the C1 waterfall's splash mist was invisible entirely, then (after the first
fix) visible but stuck at one point instead of spread across the base of the falls. Traced
to three compounding bugs, none of them rendering — all in the animation data path.

**1. Reader/compiled duplicate instances.** `AnimDefs.ParseDef` left `AnimName` as whatever
`ANIMATION_NAME` provided, or null if the reader def didn't carry one. The compiled archives
always set it (confirmed: never null in this install). C1's `zrdr/waterfalls.zrd.json` (a
shared-scope def duplicating the compiled `waterfall01` def, itself otherwise harmless) has
no `ANIMATION_NAME`, so its `AnimName` parsed as null — which meant `AnimProgram`'s
`(Name, AnimName)` dedupe key **did not match** the compiled def's key, and both got
anchored and `Start()`-ed as two independent, concurrently-running `AnimInstance`s over the
same three splash puffers. Fixed: `ParseDef` now defaults `AnimName ??= Name`, mirroring the
compiled side exactly, so a reader def collides with its compiled counterpart instead of
running a phantom second copy. (Measurable effect: C1's def count dropped from 853 to 814 —
39 other reader defs across the install had the same silent duplication.)

**2. `PufferState` had no reader-normalizer case.** `AnimDefs.ToEvent`'s switch handles
`ObjectActiveState`/`ObjectTranslateState`/etc. but had **no `PufferState` case at all** — a
reader `PUFFER_STATE` op's fields (`active_state`, `at_node`, velocities, size/lifetime
ranges, textures, colors) never reached the normalized `AnimData`, only the untouched `raw`
body. `HandlePufferState` reads `ev.Data.Num("active_state") ?? 0f`, so a reader-sourced
event silently read as **OFF**. Combined with bug 1, the duplicate reader instance for
`waterfall01` spent every frame re-asserting its (always-parsed-as-off) PUFFER_STATE events,
tearing down the puffers the correctly-parsed compiled instance had just rebuilt — diagnosed
by tracing `HandlePufferState`'s call sequence directly (temporary instrumentation, removed):
`on=True` from the compiled def, immediately followed by three `on=False` calls with `raw=`
(null) from a second def whose `defAnim` was empty. Fixed: `AddPufferState` normalizes the
reader body into the exact shape `Effects.PufferState.FromAnimEvent` expects — `ACTIVE_STATE`
token → numeric 1/0 (matching `ObjectActiveState`'s existing ACTIVE/INACTIVE handling
elsewhere), `AT_NODE`'s `[name, dx?, dy?, dz?]` split into `at_node` + an optional
`translate`, `TIME_INTERVAL` → `interval_garbage.interval_value`, and the velocity/range/
texture/color fields into the same object shapes the compiled JSON uses.

**3. `AT_NODE`'s offset was parsed nowhere, in EITHER front-end.** Once 1 and 2 were fixed,
the mist appeared but sat at a single point — C1's three splash puffers (`splashpuffer1` at
the bare anchor, `splashpuffer2`/`3` offset ±11 m sideways +8 m up) all spawned at the exact
same position. `Effects.PufferState` had no field for the offset at all; `SpawnSustained`
only ever used the host node's raw `GlobalTransform.Origin`. Surveyed: 862 of 4387
PUFFER_STATE events in this install carry a non-zero `AT_NODE` offset — a widespread
correctness gap, not a waterfall-only one. Fixed: `PufferState.AtNodeOffset` (parsed in both
`FromAnimEvent` and the reader-form `Parse`), applied in `SpawnSustained` as
`worldBasis * AtNodeOffset` — the same host-local-frame convention `LOCAL_VELOCITY` already
uses, so a banking/rotated emitter offsets correctly too.

**Verified:** the waterfall now shows three distinct splash points spread across the base of
the falls, matching the reference screenshot's spray pattern; all 8 chapters build with zero
errors and unchanged state-op counts (the dedupe fix removes redundant *instances*, never
redundant *state applications* — C1's op count stayed 3531 throughout); the C1 train/cars/
hangar doors are pixel-identical to the pre-fix run (spot-checked via `--debug-anim` pose
log); `--fly`/`--stunt` smoke clean. Schema documented in `docs/formats/effects.md`.

## 2026-07-21 — Billboard axis is data-driven: `model_type`/`facade_mode`

User request: differentiate billboards that fully rotate to face the camera (lights) from
ones that should only spin about a fixed vertical axis (trees, the refinery's gas flame),
which the renderer had no way to express — `IsGlowSpriteMesh`/`billboardTexture` only ever
produced a single full camera-facing rotation, driven by matching texture names against a
hand-maintained list (`*flare*`).

**The format itself already carries this classification.** Every model in gamez.zbd/
planes.zbd (`GameZMesh`, one per `meshes.json`/`models.json` entry) has its own
`model_type` (`Default`/`Facade`) and, when `Facade`, a `facade_mode` axis
(`SphericalY`/`CylindricalY`/`CylindricalX`) — fields the renderer never read at all.
Full survey and the `model_type`-vs-`facade_mode` gating trap in `docs/formats/gamez.md`.

Landed: `GameZ.ParseMeshes` reads the three fields (null/zero on a legacy v0.6.1 extraction,
which doesn't carry them — no crash, `SceneBuilder` falls back to its old texture-name
heuristic). `SceneBuilder.IsGlowSpriteMesh` is now gated on `ModelType=="Facade"` first
(superseding its old single-polygon restriction, which the data explains more precisely: the
11 six-poly `flare_green` "flare string" models are `ModelType=="Default"` despite carrying a
stale `CylindricalY` `facade_mode`, and must stay static). A new `GetCylindricalAxis`/
`GetCylindricalMaterial`/`GetCylindricalShader` path handles `CylindricalY`/`CylindricalX`
Facade meshes — a single-axis billboard using the same vertex technique Clutter's tree
shader already proved (`skip_vertex_transform` + a hand-built spin matrix), generalized to
either axis. Axis-billboarded materials read the texture's own alpha classification (scissor
for hard-cutout trees, blend for soft fire) and dim with the world's SUNLIGHT unless the
texture is a light source — reusing the same `glowTexture` delegate the legacy fallback
already used. `WorldBuilder.IsFlareTexture` (that delegate) widened from `*flare*`-only to
also match `*fire*`/`*flame*` — surveyed safe across all 8 chapters (only `fire101`/
`fire102`/`fire_barrel01` newly match) — which is what makes the refinery's `fire101.tif`
flame billboard and stay lit at all; before this it wasn't classified as a sprite by any
existing predicate and rendered as static, non-billboarded geometry.

**Verified:** all 8 chapters build with zero errors (regression: same mesh-instance and
gamez-node counts as before, confirming the new classification adds coverage without
reclassifying anything that used to render a different way). The refinery flame now
billboards (checked from two angles — an upright flame silhouette from both, not a thin
edge-on sliver) and reads at full brightness regardless of the mission's SUNLIGHT dimming.

**Also fixed in `docs/formats/gamez.md`/CLAUDE.md:** documented `texture_scroll` (parsed,
not yet wired into the renderer — the 5 models that use it all share materials with
non-scrolling meshes, so wiring it needs the material cache keyed by more than
`materialIndex`; left as a follow-up since nothing currently reported needs it).

## 2026-07-21 — `IF`/`ELSEIF` condition evaluation + the `AnimationLod` quality setting

Item 1 of `docs/plans/PLAN-anim-rendering-followups.md`. `AnimRuntime`'s sequence runner used to
treat every `If`/`Elseif` as "skip the branch body", on the reading that conditions were
gameplay state a world build could not answer. A survey of all 16,195 conditions across the
install (all 8 chapters, `cam_anim` + every `mis_anim`) showed that is false — there are ten
kinds and every one of them is answerable:

| Count | Condition | Rule used |
|---:|---|---|
| 4537 | `RandomWeight` | `rand() < w`, re-rolled per evaluation |
| 4007 | `AnimHealth` | `health <= n`; uniformly false at an undamaged object's full health |
| 1052 | `PlayerRange` | `dist²(anchor, player) <= value` |
| 717 | `NodeActive` | the referenced node is visible |
| 473 | `NodeUndercover` | **stubbed false** — needs a ground/occlusion probe; all 473 are `ON_CALL` |
| 124 | `AnimHealthRange` | `min <= health <= max` |
| 120 | `AnimationLod` | `QualityLod >= n` — our setting, not the data's |
| 120 | `PlayerFirstPerson` | false (no cockpit view yet) |
| 28 | `NodeBelowAlt` | node world Y < altitude |
| 17 | `HwRender` | true |

Landed: `AnimRuntime.EvaluateCondition` + `ConditionNode`, a per-open-IF `_branchTaken` stack
in `SequenceRunner` (a pure skip-to-`Endif` cannot express "the ELSE runs when the IF didn't",
so the old `SkipBranch` split into `NextBranch`/`SkipToEnd`), `AnimDefinition.NodeList` (the
`nodes` support array in order — conditions index it), `AnimDefs.ReaderCondition` (the zrdr
front-end's IF/ELSEIF normalizer), and `--anim-lod=N` in `PlaneViewer`.

**Three decode findings**, all recorded in `docs/formats/anim-definitions.md`:

- Compiled `PlayerRange` is metres **squared** where the reader's is metres (reader 270 ↔
  compiled 72900, exact across the install), and compiled `ANIMATION_LOD` is `2` where the
  reader's is the token `HIGH`. Both conversions happen once, in `ReaderCondition`, so the
  runtime has one convention.
- Condition node references are **1-based indices into the definition's own `nodes` support
  array** — not gamez node indices and not names (mech3ax resolves index→name for every other
  event kind but leaves conditions raw). Two negative sentinels share the field, -100
  `MAIN_ROOT_NODE` and -200 `INPUT_NODE`, both meaning "the node this def was invoked on";
  they arrive as u32 that float32 JSON parsing rounds together, which is harmless since they
  resolve identically, so the resolver tests magnitude rather than equality.
- `HW_RENDER`/`PLAYER_1ST_PERSON` take no reader argument and store `false` in the shared
  4-byte value slot, so that slot is unused for them and the condition is the runtime flag
  itself. (C1B's `four_bulletholes` reads backwards under that rule — but it is an `ON_CALL`
  player-cockpit def that never runs in a world build, so nothing turns on it.)

**One consequence had to be fixed with it.** Evaluating conditions made the data's *poll*
idiom live for the first time: `If <cond> → CallAnimation; Endif; Loop{-1}` re-issues the call
on every frame the condition holds. C1/MP1's `rearm_node_1/call_door` uses it to fire
`rearm_door_close` while the player is within 25 m, and `CallAnimation`'s restart-on-call
behaviour pinned that 2 s door at frame 0 for as long as you hovered. `CallAnimation` now
skips a definition already live on the same anchor; `Start` keeps its restart semantics for
the bootstrap passes, and the hangar-door pair that relies on "later registration wins"
resolves through `AddMotion`, not through this. Verified by parking the free camera on the
rearm pad: the condition logs `false → TRUE → false` and `rabdr` cycles 163 → 177 → 167 →
154 m instead of standing still.

**Verified.** `If(skipped branch)` is gone from the unhandled report in every chapter. C1
reports `AnimationLod 33✓/0✗` — the refinery, six docklights, six reflights and the police
light are all taken — and `--anim-lod=0` flips that to `0✓/33✗` with the `LightState` count
dropping 520→518 and `LightAnimation` disappearing, so the knob demonstrably gates real
content in both directions. All 8 chapters build with **zero errors**; `state ops applied` is
unchanged in every one of them (only `unresolved` rises, by exactly the newly-taken
`PlayerRange` branches whose `snd_zepengine` target is not built in these worlds). Screenshot
A/B against a stashed pre-change build differs only within a **measured** run-to-run noise
floor: C1/C1B/C3 byte-identical both ways; C1C/C2B/C4 ~5–6% of pixels, matched to within half
a point by a same-build-vs-same-build run (self-animating precipitation — rain, rain, snow);
C2 and C5 a handful of pixels in the identical bounding box. Mode battery (fly, stunt,
viewer, damage lab, 2P, 4P race, `--menu=plane`, static C4 weather view) error-free.

The condition log was kept rather than reverted, in a shape that cannot spam: per-kind
true/false tallies printed once after the bootstrap, and under `--debug-anim` one line per
condition the first time it is seen and thereafter only when its verdict **flips**.

Also fixed while landing this: `GlobalPosition` on a node outside the tree returns identity
**and logs a Godot error per call**, and the world subtree is still detached while the
bootstrap passes run (PlaneViewer parents it afterwards) — thousands of error lines, which
`AnimRuntime.WorldPos` avoids by accumulating local transforms instead.

**Open:** item 1 alone produces no *visible* change, exactly as the plan predicted — the
branches now execute, but their payload is very often a `LightState` the runtime still does
not act on. That is item 2, and it is where the refinery/dock/lighthouse lights actually
light up.

## 2026-07-21 — Animation point lights (`LIGHT_STATE`/`LIGHT_ANIMATION`), a pad kill switch, and a runtime profiler

Follow-up plan (`docs/plans/PLAN-anim-rendering-followups.md`) item 2, partially: the highest-count
unacted-on event kind. C1's harbour now casts light — blue pools under the six dock lamps,
warm pools under the refinery flares — where the flare *sprites* previously hung over pitch-dark
piers.

**The design call, made from data before writing code** (the plan explicitly asked for it, and
suggested a glow-sprite route that turned out to be wrong): all 1,468 `LIGHT_STATE` events in
the install are `type_: "PointSource"`, and the visible flare at a light's position is *already*
gamez geometry — `docklight_flare` is a `Facade`/`SphericalY` mesh on `dock_liteflare.tif`,
`flame01` a `Facade`/`CylindricalY` mesh on `fire101.tif`. A glow sprite would double-draw a
flare that already renders, offset by the payload's 1–3 m. Meanwhile the world renders
`unshaded`, so an `OmniLight3D` contributes literally nothing to it. What a `PointSource`
supplies is the **spill onto surrounding geometry** — precisely what the original's DX7 point
lights did to the same baked vertex lighting our shader reads. So: `src/Mech3/WorldLights.cs`
packs the active set into a 2×N `Rgbaf` texture (global uniforms can't be arrays) and
`SceneBuilder`'s fullbright shader adds `base_col.rgb * spill` before the fog mix.

Decode notes now in `docs/formats/anim-definitions.md`: a `LIGHT_STATE` is a **partial update**
(a fire's flicker is `{name, range}` every 0.03–0.07 s and must not reset position/colour/active
state — the compiled nulls confirm it, `range` being null on exactly the 321 events that switch
a light off); `LIGHT_ANIMATION` ramps **signed deltas**, not targets; 66 reader files also carry
`LIGHT_STATE`, so `AnimDefs` needed the same normalizer that `PUFFER_STATE` did.

**Verified.** A/B through the existing `--anim-lod` knob (which gates exactly these sequences):
dock 33,185 px changed, peak delta `[70,130,196]` blue; refinery 117 px, `[34,16,0]` orange.
Static plane viewer **byte-identical** (md5) to the stashed pre-change baseline. Static world
views byte-identical in 6 of 8 chapters; C2/C3 differ by 2 px against a *measured* same-build
floor of 1–2 px (on C3 with a smaller max delta than the floor). All 8 chapters zero errors.
Mode battery (fly / stunt / viewer / damage lab / 4P race / menu / C5 freecam) clean.

**Three findings worth keeping.**
1. The world's normals need the **same cancelling negation** the aircraft's do — `cull_front`
   makes every visible fragment back-facing and Godot flips `NORMAL`. Established by A/B rather
   than assumed: negated lights the pier deck and the boat decks (a lamp above the pier),
   non-negated lights the pilings and hull sides. This is the first time the world's normals
   have been validated for lighting.
2. A brace-less `if` emitted the light line into the **shaded** shader as well, where the spill
   function does not exist — so every aircraft silently fell back to Godot's untextured default
   material. Nothing errored visibly; only the byte-identical viewer check caught it.
3. **C1 is the only chapter with `OnStartup` light definitions.** Everywhere else `LIGHT_STATE`
   lives in `ON_CALL` combat/destruction effects a bootstrap never reaches, so the change is
   inert in 7 of 8 chapters by construction, not by luck.

**Performance, and two wrong guesses before the right answer.** Naively the feature cost
~8.7 ms/frame. I first assumed the per-fragment shader loop and added a bounding-sphere
early-out; measurement later showed viewport GPU time is **0.27 ms** with 16 lights live, so
that was solving nothing and was removed. The real cause is CPU: each fire's flicker re-issues
its *full* event including `AT_NODE`, ~2,700 `LIGHT_STATE`s/second on C1, of which ~1,740 fell
through `ResolveOne` to the full-world fallback scan — 7,064 nodes against a regex matcher,
~12M comparisons/second. Caching each light's host resolution (redone only when the name
changes) removed it, with bit-identical output. The residual is ~2 ms/frame measured against a
**re-measured same-session** baseline; an earlier 8.6 ms residual turned out to be partly
machine drift over a long session, which is a standing lesson: re-measure the baseline before
believing a regression.

**Two tools landed alongside, both user-requested.** `src/Pads.cs` becomes the single owner of
pad enumeration (concentrating the existing phantom-device policy) and adds `--no-pads`: a
connected pad with stick drift steers the free camera and nudges the flight model, silently
breaking scripted determinism, and SDL's `SDL_JOYSTICK_XINPUT`/`RAWINPUT`/`WGI` hints do not
stop Godot 4.7 enumerating it. `--perf` logs fps / frame / script / render-CPU / **measured
viewport GPU** ms once a second — Godot's visual profiler needs the editor GUI, but these
runtime numbers are headless, suit scripted A/B better, and are what finally attributed the
cost correctly.

**Still open on item 2** (and re-scoped by the user mid-session): the animated *light sprite*
behaviour — the flame cycling through `fire101–112` — is a different mechanism, the
`EFFECTS` reader (`extracted/zrdr/effects.zrd.json`: `fire1` = 12 maps @ 10 fps, `fire2` = 6 @
5 fps, bound to nodes `fire1.flt`/`fire2.flt`), together with the gamez materials' own `cycle`
field (`texture_indices`/`speed`/`looping`, used by splash/water/wake/walking-man) that
`ObjectCycleTexture{name,reset}` triggers. Neither is implemented or documented yet; that is
the next piece. The lighthouse "circling light" is **not** an animation — the user confirmed it
is billboard behaviour: a flare offset from the tower's centre so it stays visible from every
direction, which makes our cylindrical-billboard pivot handling the thing to check.

## 2026-07-21 — Lighthouse beam: cylindrical facades must not be recentered

User report, and the description was the diagnosis: *"the lighthouse light circles the
lighthouse — it is a billboard that rotates around its axis but has an offset to the center of
the lighthouse so that it is visible from every direction."* There is no rotation animation
anywhere in the data for it (searched all chapters: only `hsliteson`/`hslitesoff` lit/unlit
texture variants, and `litehouse_light_on` re-asserting light+flare every 3 s). It is pure
billboard behaviour, and we were destroying it.

`SceneBuilder` recentered every billboard mesh on its quad centroid — necessary for the cloud
sprites, whose quads sit up to ~650 m off their node origin and would otherwise swing wildly as
the camera turns. But that rule had been extended to single-axis (cylindrical) facades, where it
is exactly wrong: `GetCylindricalShader` spins the quad about `MODEL_MATRIX[3]`, the model
origin, so a quad offset *perpendicular* to the spin axis **orbits** that origin. C1's
`litehsflare` is a 19.24 m quad centred at local `(0, 0, 6)` — orbiting the tower at 6 m radius
keeps the glow on the camera-facing side, which is what makes the lamp read as lit from every
direction. Recentering pinned it to one authored world spot: viewed from the east the glow hung
detached in mid-air beside a visibly dark lamp room.

Fix is one condition — recenter only camera-*facing* sprites (`UsesBillboardTexture ||
glowSprite`), never cylindrical ones.

**Surveyed before changing the rule**, across all 8 chapters: 83 of 436 cylindrical facades have
a quad centre more than 0.5 m off their spin axis, and they are exactly three mesh families,
each of which *wants* the orbit — `litehsflare` (6.0 m, the lighthouse), the `fireflare1`
hangar/street lamps (2.58–3.6 m, hanging off their poles) and the `nosegun1` muzzle flashes
(2.4 m, which belong at the barrel tip rather than the gun's pivot). The other 353 have a zero
or purely on-axis offset, where recentering was a no-op, so nothing else moves. Spherical
sprites are untouched, which is what keeps the clouds and the moon correct.

**Verified:** lighthouse shot from two angles before/after — the glow moves onto the lamp room
from the east (brightest pixel +51 px) and is unchanged from the south, where the authored spot
already faced the camera; 8 chapters zero errors with unchanged mesh-instance counts; plane
viewer byte-identical (md5); fly and stunt clean.


## 2026-07-21 - Material texture flipbooks (animated water, surf, wakes, crowd)

The original animates textures three different ways, all sharing the same frame sets, which is
why they are easy to confuse. `docs/formats/effects.md` now separates them. This change lands
the second: a gamez **material's own `cycle` block** (`texture_indices` + `speed` + `looping`),
played on the surface.

`GameZ.ParseMaterials` reads it; `SceneBuilder.RegisterCycle` resolves the frames while the
TextureArchive is still open and registers each **built** material (not each source material -
the cache is keyed by (material, priority, rank, sidedness), so one cycling source legitimately
yields several ShaderMaterials and each needs its own swaps); `src/Mech3/TextureCycler.cs`
advances them by swapping `albedo_tex`. Swapping from C# rather than indexing a
`sampler2DArray` is deliberate - a chapter has 1-7 cycling materials, so it costs a few
`SetShaderParameter` calls and needs no new shader variant, no atlas, and no assumption that
frames share a size. Per chapter: C1 2, C1B 7, C2 7, C2B 1, C3 4, C4 3, C5 2, C1C 0.

The payoff is C1B's sea - 695 polygons of `wtr00000` (16 frames @10 fps) and 375 of `srf0001`
(16 @9), plus boat wakes and turbulence - which was rendering frozen.

**A verification trap worth recording.** The water flipbook cannot be confirmed from a
screenshot: the frames are 64x64 and differ from frame 0 by a mean of ~2/255 (max 3.8) - an
intentionally gentle shimmer, and on C1B's dark sea it is invisible. A same-camera burst with
and without the cycler came out identical to 0.01%, which reads as "not working" and is not.
`srf` (mean abs diff up to 28.9), `wakefront` (23.9) and `splash` (32.5) do have real contrast.
The reliable check is the new `--debug-anim` line logging each flipbook's current frame once a
second; observed wrapping correctly at the authored rates (`wakefront x5 @12` f1->f3->f0,
`wtr x16 @10` f9->f3->f13, `srf x16 @9` f8->f1->f10, `turb x6 @12`).

**Verified:** all 8 chapters zero errors with their cycle counts logged; plane viewer
byte-identical (md5); fly / stunt / 4P race / menu / damage lab clean.

**User-confirmed in game 2026-07-21: the boat wakes and surf animate correctly.** That closes
the one gap the headless checks could not: the high-contrast cycles are visibly right, so the
mechanism is confirmed end-to-end and not merely by the frame-index log. The open-water shimmer
remains subtle by design and is not a useful test target.

**Two related findings.** C1's refinery gas flare is *not* a flipbook - `flame01` is a static
`fire101.tif` billboard whose only animation is the `LIGHT_STATE` range flicker landed earlier
today, so that flicker already is the flame's flicker. And the `EFFECTS` reader
(`effects.zrd.json`, the node-bound flipbooks `fire1`/`fire2`) is deliberately not wired up:
those are parentless template objects the original clones to burn sites via `OBJECT_ADD_CHILD`,
so they belong with that unimplemented event kind rather than ahead of it.

## 2026-07-21 — `CALL_ANIMATION` targets: the real template-instancing mechanism (and `OBJECT_ADD_CHILD` withdrawn)

Started as `docs/plans/PLAN-anim-rendering-followups.md`'s scoped "next up" item,
`OBJECT_ADD_CHILD`. **Surveying the data before writing code disproved both of that item's
premises, so it was withdrawn rather than implemented**, and the survey surfaced the actual
mechanism plus a real bug, which is what landed instead.

**`OBJECT_ADD_CHILD` would have been a provable no-op.** Across all 1,152 `ObjectAddChild` +
192 `ObjectDeleteChild` events install-wide: `fire1.flt`/`fire2.flt` are **never** an
AddChild child (0 of 1,152), so the plan's "it unblocks the `EFFECTS` fire flipbooks" linkage
does not exist; **zero** defs containing it are `OnStartup` (against the plan's claimed 293) —
it is 838 `ByRange{0,90000}`, 240 `OnCall`, 9 `ByRange{0,40000}`; and its own cited build-time
example cannot resolve, because `pass_st` is not a gamez node in any chapter. What the events
actually are: **865 (75%) attach sound *definitions*, not nodes** (`snd_zepengine`→`spin` is
849 alone; those `snd_*` names live in `sounds.zrd.json`), inert until `Sound`/`SoundNode`
lands; ~148 are cutscene machinery this project has no cutscenes for; the rest are mission
entities and 20 CTF flag lights belonging to follow-up item 3. The dependency runs opposite to
the plan's reading — AddChild is mostly the *positioning layer for sound emitters*. Its two
"decode questions to settle first" fell out for free: **clone, not move** (15 of 40 distinct
children have multiple concurrent parents — `snd_waterfall` to `waterfall01/02/03`,
`snd_police` to four cars), and the template-pool question is moot since nothing targets them.

**What landed: `CallAnimation` was silently dropping its target node.** A call may name
another node to run the callee on — `WITH_NODE` (29,633 sites), `AT_NODE` (7,640),
`OPERAND_NODE` (179) — and the runtime ran every callee on the *caller's* anchor instead.
That is the data's template-instancing mechanism: one authored definition serving many sites,
and how effect templates get placed (`CALL_ANIMATION [NAME [huge_30sec_fire], WITH_NODE
[rc*_dbase1]]` burns one ship section). `AnimRuntime.CallTargetAnchor` resolves it in the
caller's namespace; `AnimDefs.AddCallTarget` normalizes the reader's spellings into the
compiled `parameters` union so there is one runtime path. Two subtleties: `FindAll` matches
`node == scope`, which is what makes anchoring ON the target resolve the callee's own
reference to it; and the `IsLive` guard had to move to the **resolved** anchor, since instance
identity is (def, anchor) and the caller's anchor swallows every site after the first.

**Verified.** The proving case is C1/M05, where eight zeppelin engines each call the generic
`gen_zep`/`random_prop` — whose entire body is "rotate the node named `propstill` to a random
angle" — targeting their own `propstill`: `anim: 8 call(s) retargeted onto a named node`,
each resolving to its own engine's node, where before all eight resolved globally to the first
and shared one prop angle. All 8 chapters build with **zero errors**, unchanged mesh-instance
and live-instance counts (C1 3,458 / 616 both before and after); static plane viewer
**byte-identical** (md5); full mode battery clean (fly, stunt, viewer, damage, 2P, 4P race,
menu, M05 story). One measurement trap worth recording: the bootstrap's `unresolved` op count
**varies run to run on an unchanged build** (C1/M05 measured 100, 106, 107) because
`RandomWeight` conditions take different branches — an apparent +2 regression was noise, and
this area needs a re-measured floor rather than a single baseline run.

**Fire, decoded properly (three layers, none of them `OBJECT_ADD_CHILD`).** Templates:
`fire1`/`fire2` are real single-poly `Facade`/`CylindricalY` meshes under the **parentless
roots** `fire1.flt`/`fire2.flt`, which `WorldBuilder` never builds. Flipbook:
`effects.zrd.json` gives `fire1` 12 maps @ 10 fps and `fire2` 6 @ 5 fps, resolved **by
filename from the texture archive** — every chapter's `textures.json` registers only
`fire101`/`fire102` while `extracted/<ch>/texture/` ships all twelve `fire1NN.png`, which is
the tell that EFFECTS is its own lookup path. Behaviours: `fire.zrd.json` holds four `ON_CALL`
defs, all anchored on `fire2.flt`.

**`EFFECTS` is node-keyed, not texture-keyed — settled by user observation**, and it decides
the design. `flame01` (the refinery gas flare) and `mb_spinflame` (the muzzle burst) both
render material 88 = `fire101.tif`, i.e. frame 1 of the `fire1` flipbook; if the effect bound
to the texture, both would animate for free and the refinery would cost nothing. The user
confirmed a **sustained** muzzle flash never changes texture — always `fire101`, rotating and
flashing but no frame advance. (A *brief* flash would have proved nothing: 0.1 s at 10 fps is
one frame. The held case is what makes it evidence.) So `flame01` is a static base flame and
the animated fire at the refinery is a **placed `fire2` instance** — 6 frames @ 5 fps, matching
the user's independent "looks like only 6 states" read. This corrects the previous entry's
closing note, which recorded the templates as being cloned to burn sites via
`OBJECT_ADD_CHILD`.

**Open:** the templates still are not built, so no fire renders yet; and **nothing in the data
triggers the four `fire.zrd.json` animations** — their names appear in exactly one file, their
own, and `CALL_ANIMATION` references animations by name string only (no index form exists
anywhere in this data). The original invokes them engine-side, so reproducing a persistent fire
means choosing our own trigger. User is checking the disassembly for xrefs to those strings.

## 2026-07-21 — Animation runtime performance: C5 was 7.5 fps, script-bound (fixed, 8×)

**Report:** "performance tanked in the last few commits; C5 has over 130 ms script times."

**Reproduced** with `--perf` on C5: 7.5 fps, 133 ms/frame, **GPU 0.27–0.47 ms and render-CPU
2 ms** — i.e. essentially the whole frame was C# — so this was never a rendering-cost question
(the same trap the `LIGHT_STATE` cost fell into; `--perf` exists because of it).

**Diagnosed** by temporary probe scaffolding (named stopwatch accumulators dumped once a
second, plus a per-event-kind dispatch counter). Two compounding causes, both in `AnimRuntime`:

1. **`FindAll` was an uncached full scan of the node index**, and the instance walk spent
   **296 ms of its 299 ms** inside it across **~700 calls/frame**. Each call walks every world
   node running a matcher predicate plus `IsAncestorOf`. The `LIGHT_STATE` work had already
   cached *its* host resolution for exactly this reason; nothing else was.
2. **Every instantaneous loop body ran twice per frame, not once.** The yield guard read
   `bool instantIteration = _clock <= 0f`, but the clock is reset to 0 *by the loop itself*, so
   on the next frame it reads `dt` at that point — the guard fails, the body runs a second
   time, and only then does the clock read 0 and yield. Measured exactly: `Loop` 814
   dispatches/frame → 407 after the fix, `PufferState` 558 → 279, `ObjectActiveState` 417 →
   205, `If` 220 → 110. C5 runs ~400 live poll loops (`If … CallAnimation; Endif; Loop{-1}`),
   so this doubled the call volume feeding cause 1.

**Fixed:** memoize `FindAll` on `(pattern, scope instance id)`, and test "did this iteration
schedule any time?" (a flag set when an event reports a duration or a start offset) instead of
"is the clock zero". The memo is sound because `_index` is built once in the bootstrap and
never added to, and the only runtime mutation of the world tree is `SetSubtreeActive`, which
toggles visibility and colliders without reparenting or freeing — so the answer cannot change.
Callers must treat the returned list as read-only (none mutate it today).

**Verified** by an 8-chapter A/B (stash the fix, rebuild, re-run, diff):

| | C1 | C1B | C1C | C2 | C2B | C3 | C4 | C5 |
|---|---|---|---|---|---|---|---|---|
| before | 34.3 | 31.9 | 33.1 | 31.4 | 28.2 | 45.8 | 122.5 | 133.2 ms |
| after | 16.7 | 16.7 | 16.7 | 16.7 | 16.7 | 16.8 | 16.7 | 16.7 ms |
| | 2.1× | 1.9× | 2.0× | 1.9× | 1.7× | 2.7× | 7.3× | **8.0×** |

Every chapter now sits on the 60 fps vsync cap, so these are floors, not ceilings. **Behaviour
is unchanged:** every bootstrap figure is identical in all 8 chapters — defs, anchored count,
state ops applied, unresolved, ON_STARTUP + startanims, live instances, live motions, puffer
emitters, condition tallies, unacted event kinds. Only the bracketed load timings moved, all
downward (C5 reset states 665→402 ms, start 274→130 ms; C1 600→366 ms), since the bootstrap
resolves through the same cache. Static plane viewer and static C1 screenshots **byte-identical**
(md5); the full mode battery (fly/stunt/viewer/damage/2P/4P race/freecam/menu) is error-free.

One residual, sub-perceptual and understood: the static C5 world view differs by ~19k dark
pixels at **Δ≈1/255 in the red channel only**. Poll loops now advance light flicker once a
frame instead of twice, so warm `LIGHT_STATE` spill settles at a marginally different value.
Not structural — the C5 static view is otherwise deterministic run-to-run (1 px).

**Note on `--perf`'s script figure:** it reports Godot's `TIME_PROCESS` monitor, which reads
~2.2× the measured frame time here. Trust `frame`/`fps` for absolutes and use `script` only as
an A/B ratio.

## 2026-07-21 — C5 ground z-fighting: diagnosed, NOT a mission-state/roster problem

User reported ground z-fighting in C5 and asked whether follow-up plan **item 3**
(mission-spawned entity rosters) would clear it. It would not — recorded here so it isn't
re-chased. Repro camera (user-supplied):
`--campos=-9533.178,76.319,-3367.413 --lookat=-9451.281,28.148,-3398.597`.

Measured with the purpose-built burst tooling — `--shots=5 --jitter=0.006`, a *sub-pixel*
dither so camera motion can't masquerade as a depth flip (the default 0.15° moves far too much
here: it changed 92% of pixels and told us nothing). **8.42% of pixels flip hard.**

Three findings:
- **Not the map-edge extender.** Rendering the same pose without `--sky-zone` (which is what
  enables extension in static views) gives 77,629 flipping px vs 77,566 with — identical.
- **Not entities, so not item 3.** The node-label overlay shows only **2 mesh nodes within
  1500 m**, generically named (`g4684`) — terrain/city ground, not roster entities. Item 3
  spawns/hides discrete objects (zeppelins, CTF props); it cannot touch a ground surface. The
  mechanism that fixed the *C1 airfield* coplanar flicker was a different one (anchored
  RESET_STATEs hiding `destroyed` variants), and C5's safety net reports just 1 uncovered
  destroyed subtree.
- **It is our depth-bias replication.** Raising `SurfaceRankBias` 2e-6→2e-4 and `NodeOrderBias`
  5e-8→5e-6 collapses the flicker from 8.42% to **0.01%**. The shader applies
  `VERTEX *= 1.0 - (depth_bias + node_bias)`, so separation is proportional to view distance:
  two coplanar surfaces sharing a draw priority get only `rank × 2e-6`, which at the ~80 m
  eye-to-ground distance here is 0.16 mm — far below depth-buffer precision.

**Do not just raise the constants.** With the fight resolved, what wins is a large **flat,
washed-out, low-resolution quad** lying over the detailed night-city ground (its hard straight
edges cut across the frame) — i.e. biasing picks the surface that is probably the *wrong* one.
The real question is why that coarse quad is drawn coplanar with the fine city at all (a
coarse LOD tile built alongside the fine one is the obvious suspect, given SceneBuilder's
nearest-LOD selection) and which the original draws on top. Unscheduled; belongs in the
backlog, not in item 3.

## 2026-07-22 — Per-mission world setup: the interp boot script (follow-ups plan item 3)

User-reported bug the item was scheduled on: `hk_zep`, the Blake Aviation zeppelin, sits
moored at C1's tether tower in Instant Action when the original does not show it there (it is
correctly present in M04, per the user's reference screenshot).

**The plan's premise was false, and checking it before writing code is what found the real
answer.** This is the third time that rule has paid — `OBJECT_ADD_CHILD` was withdrawn the
same way. The plan proposed that entities are *absent unless a roster spawns them*, with
`aiv.zrd.json` and `zeppelins.zrd.json` as the rosters. All three parts are wrong:

| Claim | What the data says |
|---|---|
| `aiv.zrd.json` is the spawn roster; C1/M02 and C1/M04 list `hk_zep` | It is the AI **vehicle** table. Its only mention of `hk_zep` anywhere in the install is inside a *wingman's target-priority list* in C1/M02. C1/M04 — the one mission that shows the zeppelin — does not mention it at all. |
| `zeppelins.zrd.json` is the flyable roster and gates presence | It is that zeppelin's gameplay config (position/yaw/engines/cannons/gasbags). It names `multiplayer1zep` for C1/IA1 — a node that mission actually *hides* — and never names `hk_zep`. |
| Entities are absent-by-default | C1/M02's `zepstate` explicitly *deactivates* `hk_zep`. A default-absent entity would never need hiding. |

**The real mechanism: `interp.zbd`'s per-mission boot scripts.** `interp.json` holds 98 named
command lists, one per `support\…\*.gw` of the original build tree, of which **53 are
per-mission world setup** (`support\<chapter>\<mission>.gw`). The chapter gamez contains every
one of its missions' content; the engine loads all of it and then this script switches off what
this mission does not want. C1/IA1's script contains `FindNode hk_zep` / `NodeSetActive off`
outright; C1/M04's does not. The project already read `interp.json` — for
`AddClutterTemplates` — so the file was in hand the whole time; only the mission scripts were
unexamined.

The CTF half falls out of the same place, exactly as the user described it: `ctf_1`/`ctf_2`
and `cs_flag_1`/`cs_flag_2` are switched off by **every mission script except `mp2.gw`**, the
Capture the Flag map. No roster, no mission-type check, no `targets.zrd.json` inference.

**Landed:** `src/Mech3/MissionSetup.cs` parses the script into typed ops and applies it;
`AnimRuntime` runs it as bootstrap **pass 0** through a resolver/setter pair, so it reuses the
one proven name index (including the `.flt`-suffix equivalence) rather than growing a second.
Pass 0 is before the animation passes because that is the engine's load order (world → `.gw` →
anims) and it lets an animation state override a script state — C1/M02 deliberately hides
`hk_zep` through both systems. Decode in the new `docs/formats/interp.md`.

Three semantics that had to be read out of the data rather than assumed: **order matters and
last write wins** (C3/M02 sets `cargozep1` on and then off; C4's scripts alternate
`bhf`/`bhfplug`), so a name→state map is wrong; **a `FindNode` that matches nothing is normal**
and the shipped scripts rely on it (C3's names `blackhatzep`/`blackswanzep`, absent from C3's
gamez; `limo`, `britbalmoral_1..3` and `cpilot_shadow` are in the gamez but are unreachable
roots — no parent, no partition reference — that WorldBuilder never builds, the same pool the
effect templates live in), so it is logged and never warned; and **`DeleteTree` names its own
target** instead of acting on the selection.

**Verified.** The reported camera (`--campos=-5466.595,284.452,-5136.92
--lookat=-5376.862,244.637,-5155.967`) shows the zeppelin gone from C1/IA1, leaving the bare
tether tower and its mooring circle; the *same camera* on `--mission=M04` still shows it, so
the two are separated by data alone. The CTF flag (yellow skull-and-crossbones) renders in
C1/MP2 and the entire gate structure is absent in C1/IA1. All 8 chapters build with **zero
errors** and deactivate exactly the counts an independent survey of the scripts predicted
(29 / 11 / 2 / 19 / 4 / 41+1 / 28+1 / 15). The static plane viewer is **byte-identical** (md5)
to the pre-change build, and the full mode battery (fly, stunt, static viewer, damage lab,
2P fly, 4P race, menu) is error-free.

**Scale of the change:** 3,558–15,185 polygons leave each chapter's Instant Action —
principally phantom zeppelins. *Every* chapter parks both `multiplayer1zep` and
`multiplayer2zep` in its world and switches them off outside multiplayer, so before this every
IA map had two extra zeppelins in it. C1 also loses `piratezep` (2792 polys), `hk_zep` (1272),
`workersvoyagezep`, `redcross` and nine `lifesaver*` props; C3 loses 43 objects including three
`britbalmoral` aircraft, six boats with turrets and nine `studebaker` cars.

**Deliberately not implemented — counted and reported by name**, the same incremental channel
`AnimRuntime` uses for event kinds: `Object3DSetScroll`×68, `WorldPartitionSetActive`×25,
`Object3DTranslate`×14, `Object3DRotate`×11. Two reasons for stopping there. No mission this
project defaults to uses translate or rotate at all; and **`Object3DRotate`'s angle unit is
genuinely ambiguous** — nine hand-authored integer uses (`0 45 0`, `0 172 0`) only make sense
as degrees, while two high-precision ones paired with high-precision translates
(`-0.000010 -3.144009 -0.000000`, i.e. π on Y) only make sense as radians. Guessing would
silently mis-pose a prop. `WorldPartitionSetActive` is C3-only and every `off` sits in a story
mission, so IA1 is unaffected in all 8 chapters either way.

**A finding that re-scopes plan item 4.** C1's own `ia1.gw` ends with `FindNode wf01_water` /
`Object3DSetScroll on 0.0 -0.4` and the same for `wf01_edge` — so the C1 waterfall **does**
scroll its texture, at −0.4 v/s, even though its gamez `texture_scroll` is `{0,0}`. The plan's
item 4 records the opposite as settled fact. The boot scripts are a second and apparently
authoritative scroll source (68 uses in mission scripts, 7 more in the chapter `tex_fx.gw`
scripts) that any scrolling work has to read alongside the gamez field; both plan text and
`docs/formats/interp.md` now say so.

**Fidelity A/B — user-confirmed same day (2026-07-22):** nothing reads as missing from the
emptier Instant Action maps. Notably the user did not know C2 *had* a Spruce Goose, which is
mild positive evidence rather than mere absence of complaint: it is a large, recognisable
Hollywood landmark, and never having noticed it is exactly what `support\c2\ia1.gw` hiding it
predicts. Confirmation is "nothing looks wrong in our build", not a side-by-side against the
original, so the door stays open on the smaller props (C3's boats/trucks/parked cars) if
anything ever looks thin there.

## 2026-07-22 — `texture_scroll` rendering: the waterfalls flow (follow-ups plan item 4)

The last item of `docs/plans/PLAN-anim-rendering-followups.md`. `GameZMesh.TextureScroll` had been
parsed since 2026-07-21 and consumed nowhere, so every UV-animated surface in the game
rendered as a frozen still: the C1 and C4 waterfalls, the boat wake fronts, the oil-dock
conveyor, the daytime skydome's moving sky layer.

**The survey came first, as the plan required, and it answered both of its open questions
plus one it had not asked.**

- *"Confirm the material-sharing collision actually occurs before designing around it."* It
  does, in both possible forms. C1B's `con_scroll` (−1.0 u/s) shares `oildock1.tif` with five
  **static** dock models, so an unkeyed material would slide the whole dock; and C1B's three
  wake fronts are one `wakefront1.tif` at **two different rates** (`eb_wakefront` 1.0,
  `wakefront_left`/`_right` 0.7), so a boolean "does it scroll" key would not have been
  enough either. The material cache key therefore carries the rate itself.
- *"The gamez field and the boot script are two sources that must share one renderer path."*
  They are not merely two sources — they are **the same field written at two different
  times**, and the data says so plainly. The rates in the chapter-level `tex_fx.gw` scripts
  are already baked into the shipped gamez models, value for value (C1's `h_zone1scroll`
  0.07; C1B's `con_scroll` −1.0, `eb_wakefront` 1.0, both `wakefront_*` 0.7), while the
  per-mission ones are not (the six C1/C4 waterfall leaves are `{0,0}` in the gamez). That is
  exactly what a verb writing the *model's* scroll field produces: a chapter script runs once
  per chapter and can be baked, a mission script cannot, because one gamez serves every
  mission. So the override table is keyed by **model index**, which is the engine's own
  granularity — and every scroll target in this install is a model used by exactly one node,
  so per-model and per-node cannot disagree here anyway.
- **Not asked, and wrong in this repo's own docs:** `h_zone1scroll` is not "a hangar
  glass-roof sky reflection". It is a child of `horizon/zone1` — the **daytime skydome's
  scrolling sky layer** (`sky2.tif`, 17.5 km across at y≈1070), only built under
  `--sky-zone=zone1`. `docs/formats/gamez.md` and CLAUDE.md are corrected.

**Landed:** `SceneBuilder.EffectiveScroll` (override else the model's field) feeding a
`scroll_rate` uniform and `texture(albedo_tex, UV + scroll_rate * TIME)`; the material cache
key gains the rate; a `scroll` bit in the shader-variant key so **non-scrolling materials
emit byte-identical shader text**. `MissionSetup.ScrollByModel` resolves the script's
statements to model indices *before* the world build (the rate must be known while the
material is created, so this cannot ride the existing bootstrap pass), and
`Object3DSetScroll` moves out of the "not acted on" report into `N texture scroll(s) set at
build`. `WorldBuilder` passes the table through; `PlaneViewer` loads the setup script one
step earlier and logs `texture scroll: N model(s) animating UVs`.

**Verified.** The proof had to separate the scroll from the splash puffers already animating
at the same place, so every check is a burst at one camera against the *pre-change build*:

- **C1 waterfall** (boot script, −0.4 v/s), 0.5 s apart: baseline changes 12–17k px confined
  to y[540..697] — the mist at the base and nothing else; with the fix, 63–91k px spanning
  y[117..709], the whole falls sheet from the lip down.
- **C4 waterfall** (same source, three of them): baseline 13,935 px in y[601..719]; with the
  fix 109,257 px in y[83..719]. Direction and rate confirmed by cross-correlation — best
  vertical alignment **+24 px downward** over 0.5 s, against ≈29 px predicted from the
  model's own UVs (v spans 0..4 over the quad). Baseline aligns at 0 px with error exactly
  0.00, i.e. provably static.
- **C1 daytime sky layer** (gamez field, 0.07 u/s), 5 s apart: baseline **0 px changed**;
  with the fix 51,156 px in a horizontal band y[347..435] across the full width.
- **The material split works and does not break flipbooks**: `wakefront1.tif` both cycles and
  scrolls at two rates, and C1B's cycler summary goes 7 → 8 animated materials with
  `wakefront1.tif` listed **twice** — one ShaderMaterial per rate, each registered with the
  `TextureCycler`, which is also the mechanism that keeps `con_scroll`'s rate off the five
  static dock models.
- **Scope**: the per-build count matches the data survey exactly — C1 2 (3 with
  `--sky-zone=zone1`), C1B 4, C4 6, and **no line at all** for C1C/C2/C2B/C3/C5, which carry
  no scroll from either source. Nothing else can scroll by construction: a zero rate produces
  the same shader text as before.
- **Regressions**: all 8 chapters `--freecam` with zero errors; static views byte-identical
  (md5) for C1/C1B/C1C/C2/C2B/C4 **and the plane viewer** (planes.zbd has zero non-zero
  `texture_scroll` models, so aircraft are unaffected by data); C5 differs by 1 px at the
  same-build noise floor. **C3's 35,250 px / max-delta-3 difference is pre-existing and not
  ours** — the baseline build flips between the identical two states run to run (measured:
  run1==run2, run2 vs run3 = the same 35,250 px signature), which is its water flipbook
  landing a frame apart. Full mode battery (fly/stunt/viewer/damage/2P/4P race/menu/C4 fly)
  error-free.

One detail worth keeping: `TIME` wraps at Godot's `time_rollover_secs` (3600), and every rate
in this install (0.07 / 0.4 / 0.5 / 0.7 / 1.0) times 3600 is a whole number of texture
repeats, so the wrap lands on the identical frame and no seam is visible.

## 2026-07-22 — Ambient world audio: `SOUND_NODE` (+ the sound half of `OBJECT_ADD_CHILD`)

`docs/plans/PLAN-anim-rendering-followups.md` item 2, continued. The world now has ambient sound: the
C1 and C4 waterfalls roar, the C1 train sounds as it drives its track loop, and the sirens /
zeppelin nacelle engines / fire crackle / warning beeper are all live wherever their host is on
the field. `src/Mech3/WorldSounds.cs` (new) owns the emitters; `AnimRuntime` gained the
`SoundNode` handler plus the sound cases of `ObjectActiveState` and `ObjectAddChild`, and
`AnimDefs` gained the reader normalizers for `PARENT_CHILD` and `SOUND_NODE`.

**The survey decided the scope, before any code.** `SOUND` and `SOUND_NODE` read as one feature
in the plan; measured across the install they are not. `SOUND_NODE` is **10 distinct names**,
every one present in `sounds.json`, every one `3D`, 9 of 10 `LOOPED` with a RANGE, and 293 of its
uses are `OnStartup` — looping positional ambience. `SOUND` is 87 names of one-shot, 4,378
`OnCall` + 1,650 `WeaponHit` (combat this project has no weapons to trigger) against just 8
`OnStartup`, and 21 of its names are not plain sounds.json entries at all but `DYNAMIC_WEIGHTS`
groups needing a further decode. So the ambient half is small, fully resolvable and audible
today, and the one-shot half is deferred rather than half-built.

**`SOUND_NODE` and `OBJECT_ADD_CHILD` turned out to be one mechanism**, which is why they landed
together. The reader writes a **three-event triple** — `SOUND_NODE` declares the emitter,
`OBJECT_ACTIVE_STATE` switches it on, `OBJECT_ADD_CHILD` attaches it to the world node that gives
it a position — where the middle event's NAME is a sounds.json definition, not a gamez node. The
compiled form carries the same facts inline, but its `translate` is an `AtNode` on **379** events
and null on exactly **865** — and 865 is exactly the number of `OBJECT_ADD_CHILD` events that
attach a sound definition. That the two counts match to the event is the proof. It is also the
concrete form of the dependency recorded when `OBJECT_ADD_CHILD` was withdrawn in 2026-07-21
("mostly the positioning layer for sound emitters, so it should follow `SOUND`, not precede it").

**One real bug, caught by the headless log rather than by listening.** The compiled `active_state`
is a JSON **boolean**, where `PUFFER_STATE`'s identically-named field is numeric. Read with a
number accessor it returns null for `true`, and the (correct) absent-means-leave-alone default
then left every emitter in the world switched off — 38 correctly-placed, correctly-hosted, silent
emitters. Both forms are now read.

**Verified:** C1 builds **38** emitters, exactly matching the 38 `SoundNode` dispatches, and both
`SoundNode×38` and `ObjectAddChild×38` leave the "not yet acted on" list (8 kinds → 6) — every
C1 `ObjectAddChild` was a sound attachment. State ops go 4054 → 4130, i.e. +76 = 38 emitters + 38
attachments, with `unresolved` unchanged at 126. `snd_waterfall` plays at its correct world
position and `snd_train`'s emitter **moves between log lines** (-6934,128,-5479 → -6914,128,-5521),
riding the SI-script loop. All 8 chapters build with **zero errors**; the static plane viewer is
**byte-identical** (md5); the full mode battery (fly / stunt / viewer+damage / 4P race / 2P mixed /
menu / mute / static C4) is clean; `--perf` holds the 60 fps vsync cap on both C1 and C5.

**The visibility gate is what makes the counts harmless.** An emitter is silenced while its host
is not visible in tree — the rule the point lights already use. C1/IA1 deactivates both
multiplayer zeppelins, so 36 of its 38 emitters are built and stopped and only the waterfall and
the train sound; C4 plays 3 of 87; **C5 plays 0 of 108**. The positive case checks out too:
C1/M04, the mission that shows its zeppelins, is where a `snd_zepengine` actually plays.

**A pre-existing defect surfaced and was NOT fixed here.** That same M04 emitter first reported a
position of ~(13, 3.8e28, 4.6e29). Probing the parent chain put the blowup between
`rock_zeppelin` and `gasbag3`, and probing the **pre-sound build directly** reproduced it with no
sound code present: `gasbag3` reads a global basis of ~1e27 while sibling instances of the same
node names are perfectly sane. The sound path only *reads* transforms, so it could not have
caused this — it is simply the first consumer to look at one. A degenerate host is now silenced
and logged as such rather than mispositioned; the underlying transform bug is in `backlog.md`.

**Reader-only coverage is dormant and honestly so:** of 252 reader `SOUND_NODE` definitions, 231
have a compiled twin (compiled wins), and all 21 that do not are `ON_CALL`, which the bootstrap
never reaches. The reader triple path is correct by construction but unexercised in a default
session — the same status `NODE_UNDERCOVER` carries.

**Open:** nobody has *listened* to this yet. Mix levels, the RANGE→`UnitSize`/`MaxDistance` curve,
and whether splitscreen's per-pane cameras each act as an audio listener (Godot's default when no
`AudioListener3D` exists) all need a user A/B — the last one is the only structural unknown.

## 2026-07-22 — `OBJECT_MOTION`: the world's propellers turn (plan item 2, continued)

`docs/plans/PLAN-anim-rendering-followups.md` item 2's largest remaining kind. The zeppelin nacelle
propellers now spin — counter-rotating pairs at the authored ∓40°/+30°/s — as does the rotating
signage. New `AnimRuntime.SpinMotion`; decode in `docs/formats/anim-definitions.md`.

**The survey set the scope before any code was written, and it cut the kind in half.**
`OBJECT_MOTION` is the original's rigid-body descriptor and its 7,442 uses do two unrelated jobs:
3,521 are rotation-only (a steady spin), 3,807 pair rotation with `GRAVITY`/`TRANSLATION`/
`BOUNCE_SEQUENCE` (ballistic debris from a kill), 114 are a scale ramp. **That split is exactly
the reachability boundary** — all 590 `ON_STARTUP` uses are rotation-only, and every ballistic use
is `ON_CALL`/`WEAPON_HIT`, which needs weapons this project does not have. So the spin landed and
the other two stay counted rather than half-simulated. The runtime confirms the survey was right:
across all 8 chapters **not one `ObjectMotion(ballistic)` dispatch occurs**, and the entire
residual is a single `ballflare` explosion-flare scale ramp per chapter.

**The decode question that mattered was whether `initial` is a rate or a pose**, since `delta` is
zero in 589 of the 590 reachable events — under a pose reading every one of them would be static.
The reader form settled it: the autogyro's destruction tumble writes `XYZ_ROTATION [55,20,-175,
0,0,0]` on a wreck falling under `GRAVITY [COMPLEX, DO_INTERSECTIONS]`, and a falling wreck with a
fixed pose is not a thing. Units diverge between front-ends as usual — reader degrees/s against
compiled radians/s (`[0,0,-40,…]` ↔ `-0.6981317`), converted once in `AnimDefs.Spin`, the same
way `PLAYER_RANGE` (m vs m²) and `ANIMATION_LOD` (`HIGH` vs `2`) are.

**`delta` is deliberately NOT decoded.** It reads as acceleration on a blown chassis, as a
*decelerating* ramp on `chuteman_sway` (initial `(-10,0,10)`, delta `(+10,0,-10)`, `RUN_TIME` 2),
and could equally be a random spread. Nothing reachable needs it, so it is counted and reported —
the same call `Object3DRotate`'s ambiguous angle unit got.

**Verified.** `ObjectMotion` drops from 73/97/96/97/96/97/170/220 per chapter to ×1, and live
motions rise by exactly the number of state ops gained: C1 17→41 (+24 ops), C4 **0→25** (+25),
C5 15→43 (+28) — C4 had no live motion at all before this. Rates are exact in flight: the rendered
prop reads −40.5°/−81.0° at 1 s/2 s and its counter-rotating partner +30.4°/+60.8°, i.e. the
authored ∓40°/+30°/s. **The decisive shot is a two-frame A/B 0.5 s apart on a C1/MP3 nacelle:
66,279 px change (7.19%, bbox tight on the prop) where the pre-change build at the identical
camera changes exactly 0** — so the motion is the spin and nothing else in frame. Static plane
viewer **byte-identical** (md5); 8 chapters zero errors; full mode battery (fly/stunt/viewer/
damage/4P race/menu/static-C4) clean; `--perf` holds the 60 fps vsync cap on both C1 and C5.

**Host visibility gates this the same way it gates the ambient sounds**, and it is why the win is
smaller than the counts suggest: C4's 25 spins are all on deactivated zeppelins (`HIDDEN` in the
pose log). C1/MP3 — the two-zeppelin multiplayer map — is where 15 of them are actually on screen.

**`--debug-anim`'s motion line now prints rotation as well as position.** A spin turns in place,
so the position-only line was identical every second whether or not it was running; this feature
was not headlessly verifiable without it.

**The `AnimDefs` reader normalizer is measurably inert and kept anyway** — removing it leaves every
chapter's live-motion count unchanged, because every reachable `OBJECT_MOTION` is compiled. It is
in because a reader-only def silently carrying none of its own fields is precisely how
`PUFFER_STATE` killed the C1 waterfall and how `SOUND_NODE` would have muted every emitter: a
missing case there fails silently, never loudly. Recorded as inert in the code comment so nobody
reads it as load-bearing.

**Remaining for item 2:** `ObjectOpacityState` (58 on C1), `Callback` (×8 every chapter),
`ObjectCycleTexture` (×1–2), and the one-shot `Sound`.

## 2026-07-22 — `OBJECT_OPACITY_STATE`: the clouds go translucent (plan item 2 — item 2 is now COMPLETE)

The last reachable event kind of `docs/plans/PLAN-anim-rendering-followups.md` item 2. C1's cloud
sprites now render at the authored **0.6** opacity instead of fully opaque, C5's `wl_glw`/`cfglow`
at 0.4, and C3/C4's barrage balloons and tethers at their (normal) `state=false`. New handler +
`SetSubtreeOpacity` in `AnimRuntime`, an `ObjectOpacityState` normalizer in `AnimDefs`, and a
`csky_opacity` instance uniform in `SceneBuilder`. Decode in `docs/formats/anim-definitions.md`.

**Decode.** `state` is whether translucency is ENABLED, `opacity` the alpha while it is — settled
by the data, where `state=false` pairs with `opacity=1.0` 136 times and with 0.0 never, so
`false` means "render normally" rather than "disappear" (hiding is `OBJECT_ACTIVE_STATE`'s job,
and the data uses both side by side). The reader writes the token and value **in either order**
with the value optional — `["ON",0.6]`, `[0.4,"ON"]`, `["OFF",1]`, `[1,"OFF"]`, `["OFF"]` — so
the normalizer scans by type, not position. Unlike `OBJECT_MOTION`'s normalizer this one is
**load-bearing**: C1's `cloudparent#` is reader-only with no compiled twin, so without a case the
single largest use in the game arrived carrying neither field.

**A real regression shipped into the working tree and the USER caught it, not the test battery.**
Godot assigns instance-uniform indices by declaration order per shader and merges the mapping
across an instance's materials. Declaring `csky_opacity` before `csky_fog_on` gave the opaque
variant `{node_bias:0, csky_fog_on:1}` and the blend variant `{node_bias:0, csky_opacity:1,
csky_fog_on:2}`; a mesh carrying both then disagreed, Godot kept only the first mapping and
**silently stopped fogging the losing surfaces** — reported as hilltops and tree lines standing
outside the fog, and visible in a diff mask as disturbed road and rail decals. Fixed by declaring
it after every other instance uniform. Pinning it with an explicit `instance_index(3)` hint is
**not** a fix: it compiles clean and silently no-ops the uniform. The lesson generalises to any
shared instance uniform added to a conditional shader variant, and the Godot warning
(`…same instance shader uniform … different indices…`) is now something to grep for.

**Two verification failures worth recording, both of which produced a confidently wrong answer.**
(1) **`--freecam`'s default camera is not deterministic** — the spawn is a random pick per launch,
so an A/B without `--spawn=N` or explicit `--campos`/`--lookat` compares two different views. A
54%-of-frame "difference" was entirely this. (2) **A first pass concluded the event had no visible
effect at all**, on the strength of forcing opacity to 0.0 (8 px changed) and even hiding the
nodes outright (5 px) — both true, and both meaningless, because the opaque `cloudlayer`
**`CloudDeck` occludes the sprites from below** and at any normal distance **fog washes them to
exactly `FOG_COLOR`**. The user supplied both facts. With the deck hidden and fog off the effect
is unmistakable.

**Verified.** `ObjectOpacityState` leaves the unhandled list in every chapter (C1 is now
`Callback`×8, `ObjectCycleTexture`×1, two puffer stubs, `ObjectMotion`×1); state ops rise by
exactly the dispatch counts allowing for multi-target events (C1 4154→4212 for 58 events, C3
+50 for 26 events hitting `tether*`×3 / `balloon_t*`×2, C5 +14 for 14); no `(no state)` or
`(no alpha path)` diagnostic fires anywhere. **The decisive shot is C1's clouds with the deck
hidden and fog off: 74,129 px change (8.04%), bbox exactly the cloud region, mean 213.7 → 193.4
as hard opaque white becomes translucent against the sky.** The reported unfogged-terrain camera
is back to 6 px against a measured 3–25 px noise floor; 8 chapters report zero shader errors and
zero instance-uniform warnings; static plane viewer **byte-identical** (md5); full mode battery
(fly/stunt/viewer/damage/4P race/menu/static-C4-zone1) clean; C5 holds the 60 fps vsync cap.

**Not implemented:** `ObjectOpacityFromTo` (the tweened form). Exactly 1 of its 9,917 uses is ever
reached at bootstrap, and its endpoints encode a disable-translucency-when-done convention that
would need its own decode to tween honestly.

**Noted but not chased:** parking the camera at the world origin renders the world into only the
upper-left quadrant of the viewport, like a 4P pane with one player. Reproduced on the pre-change
build, so it is **pre-existing** and unrelated; logged in `backlog.md`.

## 2026-07-22 — `docs/verification.md`: the measurement traps get their own page

User question, and a fair one: the verification gotchas were being written into dated `HISTORY.md`
entries, which is the wrong place for them. HISTORY is chronological — you read it to learn what
happened. These are things you need *before* you start measuring, and nobody reads a 1,700-line
log first.

A sweep of `HISTORY.md`, `CLAUDE.md` and `architecture.md` found **~90 distinct verification
traps**. Only ~14 were phrased as standing rules; `architecture.md` had correctly promoted 5 into
permanent per-module warnings (`AnimRuntime`, `TextureCycler`, `GaugeCluster`, `MeshLab`, `Pads`).
**Everything else existed in exactly one dated entry and was findable only by someone who already
knew it was there** — which is no use to the session that needs it.

They cluster into recurring failure modes rather than one-offs, which is the argument for a page:
screenshot diffs that prove nothing (the subject occluded, fogged, sub-threshold, or shot from the
one angle where the bug is invisible); numbers that are not what they look like (`--perf`'s
`script` figure reads ~2.2× real frame time; vsync-capped results are floors, not ceilings;
`RandomWeight` varies op counts run to run); "nothing changed" having four innocent explanations;
instruments that manufacture the predicted answer (a wrong triangulation "proving" inverted
normals, a sampling window ignoring the structure it sampled); and baselines that were never valid
(`git stash` without `-u` leaving a failed build, a regression test never seen to fail).

Written as `docs/verification.md`: the five rules that have each paid more than once, five sections
by what you are about to trust, what this project *cannot* verify itself (audio, feel, two
controllers, fidelity vs the original), a table of known non-deterministic surfaces, and a standing
pre-flight checklist. It distils rather than duplicates — HISTORY keeps the narratives.

`CLAUDE.md`'s doc-maintenance rules gained the routing line that prevents the regression: a way a
measurement can mislead goes to `docs/verification.md` as a transferable rule, not only into a
dated entry.

No code change. Nothing was deleted from `HISTORY.md`.

## 2026-07-22 — CLAUDE.md back to an index: the module index (docs-cleanup plan items 1-3)

`docs/plans/PLAN-docs-cleanup.md` items 1-3. The file loaded into every session had reached **162 KB**;
two sections were 77% of it and both had become narrative logs rather than index entries.

**Item 1 — backfill (no HISTORY entry was written at the time).** A set-diff of module paths
between `CLAUDE.md` and `docs/architecture.md` returned three modules documented *only* in the
index: `CompiledAnim.cs`, `AnimProgram.cs`, `SpectatorCamera.cs` — all from the 2026-07-21
animation work, i.e. written after the 2026-07-18 split that created `architecture.md`. Bullets
written for each; the set-diff is now empty in both directions (63/63), which is what unblocked
item 3.

**Item 2 — the status section (no HISTORY entry either).** 65,166 -> 2,993 bytes. 27 landed-work
bullets deleted after per-bullet HISTORY verification; three genuinely-open items migrated to
`backlog.md`. That section now holds current state + one next step, as its own preamble always
claimed.

**Item 3 — the module index.** 63 bullets averaging 1,030 chars (66.2 KB) -> 63 one-line entries
averaging 136 chars (8.4 KB). `CLAUDE.md` **110,222 -> 52,677 bytes**.

This is the only item in the plan that could lose information, because `CLAUDE.md` was *newer*
than `architecture.md` in places, so it was done as a per-module diff rather than a bulk cut: each
index bullet compared fact-by-fact against its architecture bullet, and anything the index knew
that `architecture.md` did not was moved there **first**. 30 such facts, `architecture.md`
193,744 -> 210,982 bytes. The substantial ones:

- **SceneBuilder's data-driven billboard classification** (2026-07-21) — `IsGlowSpriteMesh` gating
  on `ModelType=="Facade"` first, `SphericalY` vs the cylindrical single-axis billboard, and the
  rule that cylindrical facades are *not* recentred on their quad centroid (the lighthouse-beam
  orbit). `architecture.md` still described the superseded "single-polygon all-flare meshes ONLY"
  heuristic as current; it is now marked as the legacy-tree fallback it became.
- **PlanePainter's bottom-up `.BM` row order** (`h-1-y`) — the rule that keeps every livery from
  mirroring along V, with its 55-pair edge-correlation evidence. Absent from `architecture.md`.
- **WorldBuilder's `IsFlareTexture` widening** to `*fire*`/`*flame*`, and **GameZ's**
  `ModelType`/`FacadeMode`/`TextureScroll` unified-shape fields.
- **Puffer's `SustainAt`, `FromAnimEvent` and `AtNodeOffset`** (the whole 2026-07-21 block).
- **AnimRuntime's** scheduling/dispatch, `SpinMotion`, the ten `If`/`Elseif` conditions, the poll
  idiom, `LightState` and `PufferState` — this bullet was the one place `CLAUDE.md` was *longer*
  than `architecture.md` (5,440 vs 3,793 chars), and most of that delta was real.
- Smaller: TextureCycler's design rationale + the unwired `EFFECTS` reader, AnimDefs' `AddPufferState`
  history, WorldSounds' three-event triple and the 379/865 split, MissionSetup's pass-0 ordering,
  FlightModel's accepted arcade artifacts, PlaneCollider's Bloodhawk canard-tip limit.

**Verified by the plan's two-stage check**, because the weak version is genuinely misleading:

1. *Token check.* 697 distinct tokens (backticked identifiers, paths, multi-digit numbers) pulled
   out of the 65,600-char deleted span and searched across `docs/**/*.md` + `backlog.md` +
   `NOTES.md` + the new `CLAUDE.md`. 9 flagged, all confirmed as spelling variants of a fact that
   *is* present (`Scale(control, reference = 1440)` spaced differently; the SI-script chain spelled
   `event slot -> si_script_ids -> pool index -> SiScript`; "fuel leak" written `player_fuelleak`).
   **0 genuinely absent.**
2. *Hand-read for open claims.* The check that matters, since token presence proves a fact is
   written down but not that the same *claim* is made. Every "still open / not yet / unwired /
   stubbed / known limit / TUNE" statement in each deleted bullet was located and confirmed to
   survive, by hand.

**One contradiction found en route and fixed:** `architecture.md` said F11's free-look look-at is
projected "one unit ahead along the view ray". `PlaneViewer.cs:1992` is
`PoseLookAtDistance = 100f`, and `CLAUDE.md` said 100 m. The code is authoritative; the
`architecture.md` claim was wrong and is corrected, along with the missing `--freecam` case.

No code change.

## 2026-07-22 — CLAUDE.md back to an index: extraction, CLI, stale claims (docs-cleanup items 4-6)

Continuing the same session as the module-index entry above. **CLAUDE.md 52,677 -> 36,735 bytes**;
the file is now 33% of the 110 KB it started this session at, and 21% of the 162 KB that motivated
the plan.

**Item 4 — format support status -> `docs/formats/extraction.md`.** The support matrix, the
round-trip evidence, the legacy<->unified extraction-shape table and the extracted-aircraft
inventory moved to a new page, listed in the formats README. CLAUDE.md keeps four lines:
extraction is complete, `extracted/` is fork-produced, both shapes are readable, pointer.

**Item 5 — user args -> `docs/cli.md`.** The 43 per-flag prose entries moved **verbatim** as
bullets rather than reworded, so the move could not lose a detail. CLAUDE.md keeps the
CLI-inversion rule (a behavioural fact, not a reference entry), a 14-row table of the day-to-day
set, and the in-flight keys. Verified: all 47 distinct flag names present in the new page,
149/149 tokens present.

**Item 6 — the stale claims.** All six rows of the plan's table, plus five more found while doing
items 3-5 and two contradictions the plan had not spotted. The pattern worth noticing is that
**every one of them was a claim that some work was still pending, written by the session that
deferred it and never revisited by the session that landed it.** Four separate files said
cam_anim/mis_anim were unextracted or unread, in four different wordings, while
`ExtractAssets.ps1` had been extracting them and three C# modules had been reading them:

- the CLAUDE.md matrix row's "Remaining: item 7 ... skipped for now by user decision"
- `docs/formats/README.md`'s "unsupported by mech3ax"
- `ExtractAssets.ps1`'s own docstring, "no part of the Godot project reads the output yet"
- `PLAN-M2-polish-2.md` item 9's "waits for a mech3ax extension"

The same shape produced the paint ones: CLAUDE.md's gotcha said the region table was "superseded
but not yet reworked" and that "rework is deliberately a separate session"; `paint.md`'s own
lead-in said the hue-window sections "document what the remake does today" while two sections
lower in the *same file* documented the rework that replaced them; and `backlog.md` still listed
"achromatic paint regions" and "slot order" as open, both of which `paint.md` records as fixed.

Two were plain factual errors rather than staleness, and both were in `architecture.md`, i.e. the
file the module index was about to defer to: F11's free-look look-at documented as "one unit ahead
along the view ray" when `PlaneViewer.cs:1992` is `PoseLookAtDistance = 100f`, and
`IsGlowSpriteMesh` still described as "single-polygon all-flare meshes ONLY" as though current,
which the 2026-07-21 data-driven facade classification had superseded. Both were caught only
because item 3 forced a per-module diff against the code and the index; a bulk cut would have
deleted the correct claim and kept the wrong one.

**Verification rule this reinforces** (already in `docs/verification.md` in general form): a
grep for a *fact* does not prove the *claim* about that fact is still made anywhere. Item 3's
token check passed 697/697 while three items of genuinely-open work would have vanished; these
eleven were found by reading for "still / not yet / pending / waits for", not by token search.

No code change across items 4-6; the `ExtractAssets.ps1` edit is a comment.

## 2026-07-22 — CLAUDE.md back to an index: plans archived, fork policy, guardrail (docs-cleanup items 7-10)

The last four items, same session. **CLAUDE.md 36,735 -> 35,105 bytes**; the plan is complete.

**Item 8 — the plans are archived.** All six completed plans `git mv`'d into `docs/plans/`, each
with a `COMPLETE — <date>` banner that says in as many words to read the file as history, not as
current state — the specific failure this cleanup kept hitting. 52 path references rewritten
across 9 files and verified to resolve. One was already broken before the move: `backlog.md`
cited `docs/plans/PLAN-M2.5-prototype.md` while the directory did not yet exist.

`docs/` now holds exactly one plan file. That is the point: **which plan is live is signalled by
what is *not* in `docs/plans/`**, so it cannot drift the way a status line can.

**Item 9 — the upstream PR package is retired.** It was written on the premise that the CS work
would land upstream. The user's conversation with the mech3ax developer settled otherwise:
upstream dropped Crimson Skies because they could not maintain it — bandwidth, not architecture —
so the work stays in the fork, with upstream commits merged *into* it.

The package is archived rather than deleted: `pr-1-anim.md` and `pr-2-gamez.md` are still the best
description of what each branch contains, which is what a future upstream revival or any fork
reader would want.

**The replacement policy was written from the fork, not from the plan's notes** — worth recording
as method, because the plan's own summary of the branch state was a session old. Verified
directly: `origin` = `Laeresh/mech3ax`, `upstream` = `TerranMechworks/mech3ax`, all three CS
branches (`cs-anim`, `pr-cs-anim`, `pr-cs-gamez`) present on `origin`, and
`upstream/main...main` = 0/0 with upstream's tip still `cbb838f` (rc3, 2025-11-17). Everything
matched, but checking cost one command and would have caught it if it had not.

Two things deliberately did **not** vanish with the retired package:

- The **AI-assistance disclosure rule** lived inside the bullet being retired, despite being
  project-wide. It moved to CLAUDE.md's top block.
- **What became of the already-opened anim PR is not recorded anywhere**, so the archive says so
  explicitly instead of implying the decision closed it. The user owns upstream communication;
  that disposition is theirs to state.

**Item 10 — the guardrail.** The top block gained the three rules that make the other nine stick:
a stated **~35 KB budget** (with the reminder to check the size *before* adding a paragraph), the
**one-line module-entry shape rule** generalised to "don't restate a list another file already
indexes", and the **status-section rule as a hard one** — landed work is *removed* from "Current
status", not also summarised there.

Applied immediately to the obvious remaining violation, at the user's request: CLAUDE.md carried
its own 17-row copy of the `docs/formats/README.md` index (~1.6 KB). It is now a pointer. That
list is also what proved the rule worth having — during item 4 it was found claiming "13 pages"
while listing 16, and missing `interp.md` entirely. A duplicated index rots exactly like a
duplicated status section.

**Verified:** CLAUDE.md is 35,105 bytes, under its own budget; all 33 files under `docs/` are
reachable from it by path; all 52 plan references resolve.

**Net for the whole plan: 162 KB -> 35 KB, with no unique fact deleted** — the content moved into
`docs/architecture.md` (+17 KB of index-only facts), `docs/cli.md`, `docs/formats/extraction.md`,
`docs/plans/`, and `backlog.md`. 13 stale claims were corrected along the way, most of them work
that one session deferred and another landed without ever revisiting the deferral note.

No code change.

## 2026-07-22 — `docs/tooling.md`: the pipeline, the launch scripts and the fork leave CLAUDE.md

Follow-up to the docs-cleanup plan, on the user's read of the result. Two observations, both
correct: the "The mech3ax fork" section added during item 9 is **reference material for a tool that
already works** — remotes and a sync procedure nobody needs loaded every session — and **Repo
layout never got the one-line treatment** the module index did, still carrying four essay-length
entries.

**CLAUDE.md 35,204 -> 28,104 bytes**, now well under its own 35 KB budget.

New `docs/tooling.md` holds everything *around* the project rather than in it: the `extracted/`
workdir layout (including the unpacked-sibling preference and why `rtexture*`/`rimage` are
deliberately not loaded), `ExtractAssets.ps1`'s per-type mode table and its two output-handling
details, `ExtractRof.ps1`, the two launch scripts plus the `SDL_JOYSTICK_DIRECTINPUT=0`
controller-freeze workaround, `tools/`, and the fork's remotes / branch roles / sync procedure.

Repo layout is now one line per entry and doubles as the routing table to each docs page — which
is what that section should have been all along, since a repo layout *is* an index.

**Worth recording as a verification note.** The token check caught two genuine losses that the
one-line rewrite had dropped: `CrimsonSkiesGame/ZBD/` and
`CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`. The second is the **only** record anywhere of
where the cutscenes live — nothing consumes them yet, so no code would have failed and nothing
would have surfaced it. Compressing prose to one line is exactly where paths get paraphrased away
("cutscenes are plain MPGs"), and a fact with no consumer has no other alarm. Both restored;
127/127 tokens then present.

Also added a transitive reachability check, which is the right shape for this file now that it
routes rather than restates: start at CLAUDE.md, follow every doc reference, confirm all 34 files
under `docs/` are reachable. Four (`plans/upstream-pr/*`) are no longer named by CLAUDE.md
directly and are reached through `tooling.md` — correct, but only a transitive check can tell that
apart from an orphan.

No code change.

## 2026-07-22 — The upstream anim PR was closed (the one thing the archive left open)

The `docs/plans/upstream-pr/` archive was written stating explicitly that it *could not* settle
what became of the anim PR: it had been opened before the fork decision, nothing recorded its
fate, and the user owns upstream communication. **The user closed it after the decision.**
Recorded now, which closes the last loose end of revival-plan item 14.

Nothing of that package is outstanding upstream: the gamez PR was never opened, the anim PR is
closed, and issue [#3](https://github.com/TerranMechworks/mech3ax/issues/3) stands as the
discussion that settled it. Neither branch is withdrawn from the *fork* — `pr-cs-anim` and
`pr-cs-gamez` both remain on `origin`, split by concern, and are what a future upstream attempt
would reopen from.

Writing it down surfaced three further contradictions in the archived revival plan, which is worth
noting as a pattern: **the plan's completion banner and its own checklist disagreed.** The banner
said "all 14 items", while item 14 was still marked `◐` and read "opening them is the user's step
and has not happened — nothing is pushed" — false twice over by then. `docs/plans/PLAN-docs-cleanup.md`
item 8 had called for marking it `☑ resolved by decision`, and that step was simply missed when the
plan was archived. Section 14 also still asserted "**The anim PR is open**" in bold present tense,
and closed on a "**Remaining:** upstream's reply … plus whatever review iteration the anim PR
draws" that no longer described anything pending.

Fixed by marking item 14 `☑` with the outcome, dating the present-tense assertion inline rather
than rewriting the 2026-07-21 snapshot around it, and appending a "Resolved 2026-07-22" paragraph
recording that the second branch of that Remaining sentence is what happened. Also repointed the
archive's fork-maintenance reference from CLAUDE.md's removed "The mech3ax fork" section to
`docs/tooling.md`, and flagged the intro's "nothing has been pushed or opened" as as-written
2026-07-21, since the status table two paragraphs below it says the opposite.

**The transferable lesson:** a `COMPLETE` banner is a claim about a checklist, and adding one does
not verify it. When archiving a plan, check the checklist for surviving `☐`/`◐` markers — an
archived plan whose banner and checklist disagree is worse than an un-archived one, because the
banner discourages reading far enough to find the disagreement. A repo-wide sweep for unchecked
items across `docs/plans/` now returns zero.

## 2026-07-22 — Open-source licensing: GPL-3.0-or-later, CC BY 4.0 for the format docs

The repo had no `LICENSE` and no root `README.md` — for a project whose whole legal posture is
"public open-source under the XWVM model", both were load-bearing gaps.

**The binding constraint, checked first:** upstream mech3ax is **EUPL-1.2**, a copyleft licence.
The fork must stay EUPL — not a choice. But the engine is legally independent of it:
`CrimsonSkies.csproj` has zero `PackageReference`s, no Mech3DotNet, no `using Mech3`; it reads
extraction *output* (JSON/zip) at runtime, and a tool's output is not a derivative of the tool.
So the repo licence was a free choice, not an inherited one.

**Chosen: GPL-3.0-or-later** for code. Three reasons, in weight order: (1) it is the
reimplementation-genre convention — OpenMW, OpenRA, ScummVM, OpenRCT2, OpenTTD — so contributor
expectations match and code can flow between those projects; (2) the EUPL-1.2 Appendix explicitly
lists GPL-2.0/3.0 and AGPL-3.0 as compatible, so if fork code is ever vendored into the engine the
combination is distributable under GPL-3.0 — **MIT would have foreclosed that permanently**, since
EUPL code cannot be relicensed permissively; (3) the durable value here is reverse-engineering
knowledge, and copyleft keeps derivatives open rather than letting someone ship a closed build.

**`docs/formats/` split out under CC BY 4.0.** The 17 format pages are the most reusable output of
this project, and formats are facts. Permissive docs can be picked up by projects that cannot touch
GPL code — including upstream mech3ax itself, or a future CS tool by someone else. The split is
stated in both `README.md` and `docs/formats/README.md` so it is unambiguous where it applies.

Licence texts were **downloaded verbatim** from gnu.org and creativecommons.org rather than typed —
a hand-transcribed licence is a mangled licence. Both were verified head and tail after download
(35,149 B / 18,657 B, genuine text, not error pages).

Also created the root `README.md` (there was none): status, getting started, format-docs pointer,
the **not-affiliated-with-Microsoft / no-assets-distributed** disclaimer, the licence section, and
the AI-assistance disclosure the standing rule requires of outward-facing text.

**Public home set the same day:** <https://github.com/Laeresh/CSVM> (`origin`, already tracking
`main`). That closed the one gap the licence work left open — CC BY requires an attribution target,
and until the repo had a public URL there was nothing to name. Both `README.md` and
`docs/formats/README.md` now attribute to **CSVM** with that link, and the root README is titled
for the repo name rather than a generic description.

**The transferable point:** the licence is not what protects this project legally. The IP exposure
is Microsoft's copyright and trademarks, and that is handled by the XWVM model — no assets in the
repo, runtime read of the player's own install, explicit disclaimer. The licence governs what
*downstream users* may do with the code, which is a separate question that copyleft answers well.


## 2026-07-22 — The Godot project renames to CSVM

`CrimsonSkies/` → `CSVM/`, matching the public repo name set earlier today, and removing the
long-standing ambiguity with `CrimsonSkiesGame/` (the retail install). Moved with `git mv` so
history follows the files.

**Changed:** the project directory; `CrimsonSkies.sln`/`.csproj` → `CSVM.sln`/`.csproj` plus the
solution's internal project reference and `RootNamespace`; `project.godot`'s `assembly_name` and
`config/name` (the window title, previously "Crimson Skies"); the C# namespace `CrimsonSkies.*` →
`CSVM.*` across all 63 sources; `.gitignore`'s three build-artifact paths; the launch scripts; and
the live docs.

**Deliberately not renamed.** `docs/HISTORY.md` and `docs/plans/` keep their old paths — they are
historical records, and house style here dates an assertion inline rather than rewriting a past
snapshot around it. The F12 screenshot prefix stays lowercase `crimsonskies_` (`PlaneViewer.cs`);
renaming it would orphan the existing captures that `NOTES.md` and `backlog.md` cite by filename.
`CrimsonSkiesGame/` is untouched — every rewrite used a `CrimsonSkies(?!Game)` negative lookahead
so the install path could not be caught by accident.

**The rewrite corrupted 3 source files and `dotnet build` reported 0 errors.** The bulk replace
read each file as Latin-1 on the assumption that a byte→char→byte round trip is lossless for an
ASCII-only substitution. It is not: `[IO.File]::ReadAllText($path, $encoding)` still auto-detects
a BOM and decodes as UTF-8 regardless of the encoding argument, so the three BOM-prefixed files
came back as real Unicode and were then written out as Latin-1 — collapsing every multi-byte
sequence to a single byte (`°` C2 B0 → B0, `×` C3 97 → D7) and dropping the BOM. The C# compiler
compiled the resulting mojibake without complaint because it all sat in comments; **Godot refused
the same files outright**, validating UTF-8 strictly, and the only symptom was
`Main.tscn: Parse Error: [ext_resource] referenced non-existent resource`. Recovered by restoring
from the index (`git checkout`, verified byte-identical against `git show HEAD:...`) and redoing
the pass with explicit BOM detection and a throwing UTF-8 decoder. Both halves are now rules in
`docs/verification.md` §4.

**Verified:** `dotnet build` clean (0/0), all 63 sources validate as UTF-8, and a C1 Bloodhawk
flight screenshot is visually indistinguishable from a pre-rename capture taken the same morning —
same terrain, fog, HUD, compass tape, gauge cluster and livery.

**The transferable point is the one already in `docs/verification.md` rule 5, in a new costume:** a
clean compile is only evidence if the compiler is *able* to fail on the thing you broke. It cannot
fail on comment encoding, so it said nothing. Running the game took one command and said
everything.

## 2026-07-22 — `CleanScratch.ps1`: sweeping `.scratch/` without eating the backups

`.scratch/` had reached **322 files / 206 MB** with no sweep tool, so it only ever grew. Added a
root script alongside the other `*.ps1` helpers: `-WhatIf`/`-Confirm` via `SupportsShouldProcess`,
`-OlderThanDays N` for an age-bounded pass, `-Keep <patterns>` to spare specific captures, `-Force`
to skip the prompt. It prunes directories the sweep empties (deepest-first so parents collapse) and
is scoped to `$PSScriptRoot\.scratch`, so it cannot wander outside.

**The part that needed a judgement call: not everything in `.scratch/` is probe output.** The
folder also held `pre-rewrite-backup.bundle` (71 MB of pre-history-rewrite repo state) and
`backlog.md.worktree-backup` — safety nets deliberately parked somewhere git-ignored. A naive
"delete everything in scratch" script destroys those. So `*.bundle` and `*.worktree-backup` are
protected by default, listed under "Keeping" with their reason so their survival is visible rather
than assumed, and `-IncludeBackups` is the explicit opt-out.

That default is pattern-based, and a pattern cannot tell a live backup from a stale one: asked
afterwards, `backlog.md.worktree-backup` turned out to be **byte-identical to the tracked
`backlog.md`** (same SHA256, `git diff --no-index` silent) and therefore disposable — the content
is in git anyway via `c575f7b`. `-Keep '*.bundle' -IncludeBackups` is the combination that drops
the stale copy while still guarding the bundle. Whether the bundle's history is itself redundant
against a remote is unverified.

Verified with `-WhatIf` in three modes against the real directory, deleting nothing: default spares
2 and targets 320 files / 134 MB; `-OlderThanDays 2` spares all 322 (every artifact was <2 days
old); `-Keep '*.py','*.md'` spares 22. No sweep has actually been run yet.

---

## 2026-07-22 — Milestone 2 polish run 3 planned; item 1 (`--data-root=`) landed

**The plan.** `docs/plans/PLAN-M2-polish-3.md` (active at the time; archived 2026-07-22): ten items chosen from `backlog.md` against
the user's criteria — feasibility, little or no user input, a preference for long-running work.
Items needing a playtest, two controllers, or a fidelity judgement the data cannot settle were
deliberately excluded.

**Selection turned up four stale backlog entries**, all corrected in `backlog.md` in the same
turn so they are not re-chased:

1. **"World renders into only the upper-left quadrant at the world origin" is not a bug.** C1's
   World area is `left/top = -12288, right/bottom = 0` — the world origin *is* the map's corner,
   so all terrain lies at x ≤ 0, z ≤ 0. From `--campos=0,30,420 --lookat=0,0,0`, `FrameCamera`
   derives yaw 0, camera-right is exactly world +X, the x=0 plane projects to the vertical centre
   line and the line (t,0,0) to the horizontal one. Two hard half-viewport edges, no clipping.
   The splitscreen theory is dead: 1P never builds a rig (`PlaneViewer.cs:1123-1128`) and there is
   no other `SubViewport` in `CSVM/src`.
2. **"A generic way to find billboard sprites" is largely already done** — the data-driven
   `ModelType`/`FacadeMode` classifier landed 2026-07-21, *including* the cylindrical case the
   entry named as missing (the harbour refinery flames). Rescoped to consolidation.
3. **C4's cloud deck is `Sky1.tif`, not `cloudtrans`.** `srock-cloudtrans` skins 35 models of
   96–422 vertices with dy 162–533 m — cloud-shrouded rock *terrain*, which must stay solid. The
   real deck is 144 parentless 1024² quads at y = 1050. Trap for the obvious fix: `Sky1.tif` is
   the *skydome* in six other chapters.
4. **The C5 coarse quad is not a stray LOD tile.** `world1`'s 105 children and the 471
   partition-referenced roots are exactly disjoint, and neither side sits under an `Lod` node, so
   the nearest-LOD rule could never have dropped either. We draw both because the original selects
   between them via partition visibility (`WorldPartitionSetActive`, 25 interp uses).

**And it turned up one live bug and three undocumented mechanisms**, all logged:

- **C5 has been flying with no fog and no sunlight model at all.** `Weather.cs:166` iterates a
  hardcoded `{ "ZONE1", "ZONE2" }`, but C5 ships `ZONE1` + **`ZONE3`** in all 8 missions, so its
  dict is never populated and `Fog("zone2")` falls through to `NoFog` (near/far 1e8/1e9,
  fullbright). `--sky-zone=zone3` cannot rescue it either. Scheduled as item 2.
- **`zone_id` on every gamez node** — `-1` = always, `1`/`2`/`3` = that zone only; both zones span
  the whole map, so they are alternative world variants, not regions. Never read anywhere in
  `CSVM/src`; per-chapter counts recorded in `backlog.md`.
- **`FogState` exists as an animation event** — one occurrence install-wide (C1/M04 intro
  cutscene camera, `drop_fog`), carrying fog parameters inline rather than naming a zone. It is
  the only evidence that weather is scriptable at all.
- **The documented justification for collidable clutter trees was a misreading.** `Clutter.cs` and
  `docs/formats/clutter.md` both cite "`spruce_destroy` anims exist"; the actual strings are
  `..\data\common\zrdr\**planes**\spruce_destroy{1,2}.zrd` — the **Spruce Goose**, the C2/M01
  flying-boat mission object, alongside `sprucegoose-fly_the_goose`, `goose_cooked`,
  `spruce_enginedest`. There is no spruce-*tree* animation in the install. User decision: remove
  tree collision outright; `cblock` buildings keep it.

**Coarse/fine ground measurement (item 3's evidence).** ⚠ **RETRACTED the same day — see the
item 3 entry later in this file. Do not quote the figures in this paragraph.** Kept as written
because the retraction is the point: this is what a confidently-stated wrong measurement looked
like, and it went out in commit `a16cec0` as fact.

~~Rasterising each C5 coarse `world1`-child ground sheet on a 64-unit grid against fine
partition-tile coverage: `g4632` 97.8%, `g4683` 78.4%, `g4425` 33.3%, `g4631` 20.0%, `g4616`
12.5%, `g4428` 8.8%, `g14550` **0.0%** — **72% overall, so culling them would leave 28% of their
footprint with no ground at all.** That rules out the obvious "hide the coarse quad" fix and makes
it a draw-priority problem instead.~~

What went wrong: the method tested fine-tile *bounding-box* containment — axis-aligned boxes over
swept terrain, far too crude a proxy. Independent rasterisation gives 18.2–81.4%, and `g14550`
(claimed 0.0%) measures 18.2%. Only the weak conclusion survives — no sheet is fully covered, so
culling leaves holes somewhere. The draw-priority fix this paragraph argued for was implemented
and measured to change nothing (35.77% → 35.79%).

Still valid from that measurement session, and worth keeping: a naive "large flat quad" filter
also catches the `fvol*` **fog volumes** (10 in C1, 14 in C5, at altitude), and `zone_id` does not
separate the coarse/fine pair — both are `zone_id=1`.

### Item 1 — `--data-root=` / `CSVM_DATA_ROOT` (landed)

**Why first:** `/extracted/`, `/CrimsonSkiesGame/` and `/tools/` are git-ignored, so a
`claude --worktree` checkout has none of them. A worktree session could edit code and docs but
never build, run, `--screenshot=` or verify — removing this project's main verification
instrument, and with it any hope of working the other nine items in parallel.

**What landed.** One `_dataRoot` field in `PlaneViewer.cs`, resolved *before* the main arg loop
(every base path derives from it), with precedence `--data-root=` > `CSVM_DATA_ROOT` > repo root,
and a one-line log when it differs from the repo root. All 9 asset paths repointed; `_repoRoot`
stays for anything genuinely about the repo. `RunGame.ps1`/`RunDev.ps1` additionally fall back to
`CSVM_DATA_ROOT` to locate `tools/godot`, so a single `$env:CSVM_DATA_ROOT` makes a worktree fully
runnable with no flag — Godot inherits the environment, so nothing needs forwarding.

**Verified end-to-end against a real worktree**, not just the flag. `git worktree add --detach`
produced a checkout confirmed to have no `extracted/` and no `tools/`; with `CSVM_DATA_ROOT` set
it built and rendered, logging `data root: Z:\Crimson Skies (repo root …\wt-test)`. Baseline,
`--data-root=`, and the worktree run all produced **byte-identical PNGs** (sha256
`64a150bbfcf1d420…`). Worktree removed afterwards. Full 8-chapter regression: 0 errors, all 8
screenshots saved.

**Two gotchas worth keeping.** Screenshot paths are relative to the *Godot project dir*
(`CSVM/`), not the repo root — use absolute paths in scripted runs, or the PNG lands somewhere
surprising and the save can fail outright. And `RunGame.ps1`/`RunDev.ps1` forward `$args`
verbatim, so PowerShell splits any comma-bearing argument into an array:
`--campos=-5466,120,-5136` arrives as `System.Object[]` and throws deep inside the arg parse.
Quote them. Both are pre-existing, neither is caused by this change, and both cost time here.

**Plan correction the same day (user, 2026-07-22): item 7 was scoped wrong.** It was written as
"C3 clutter planted in the sea", to be fixed with a `y > waterLevel` guard dropping submerged
palms. That was backwards — **the palms are visible in the original and are supposed to be
there.** What fails is that the water wins the depth fight against the beach, so the sand
vanishes and the palms read as growing out of the sea; the palms are a symptom, not the fault.
The guard would have deleted correct content while leaving the real bug untouched. Item 7 is now
"the beach must draw over the water", **merged with the pre-existing C3 beach z-fighting backlog
entry** — same geometry, same camera pose, one bug — and its verification now requires the
clutter instance count to be *unchanged*. Worth recording as a general lesson: a measurement
("86 of 102 sand polygons are at exactly Y=0, coplanar with the sea") correctly located the
geometry but said nothing about which surface was at fault, and the plausible-looking fix
pointed at the wrong one.

---

## 2026-07-22 — Run-3 item 10: tail collision boxes no longer swallow the outboard wings

**The bug.** `PlaneCollider` clips its tail region on **z alone, at full span** (`TailStartFrac`
0.7), where the wing and fuselage paths also clip on axis 0. The volume-guided refinement pass
then splits that full-span slab but propagates the region name verbatim, so the Bloodhawk's two
flat 4.9 x 0.4 m outboard strips — geometrically the swept wing trailing edge, and in the model
literally the nodes named `leftwing` / `rightwing` — arrived at `PlaneDamage.MapStruckPart`
called `tail`. That is the one arm of the map which ignores `localImpact` entirely, so clipping
a hangar corner with a wingtip 4 m off-centre subtracted HP from the tail. Distinct from the
accepted canard-tip limit recorded earlier.

**The fix** (`PlaneCollider.Relabel`, one file). After refinement, each final box is re-judged:
a `tail` piece becomes `wing` when its box lies **wholly on one side of the centerline** (so
every impact inside it maps to the correct side in `MapStruckPart`) **and** its centre is
outboard of the same `WingBandFrac` threshold that defines wing geometry in the first place.
Renaming at the collider rather than side-splitting in `PlaneDamage` is the structurally right
seam — the half-span is known here, and `MapStruckPart` would otherwise have needed a widened
signature. Applied *after* refinement, not during, so a piece cannot be renamed and then cut
again into an inboard remainder.

**The risk case was measured, not argued.** Twin-boom and twin-fin aircraft genuinely *are*
tail at outboard |x|, so a blanket rule could have reclassified real empennage. Instrument: a
temporary probe dumping every collider box (name, size, centre) alongside every visible mesh
node's AABB in the plane-root frame, for all 11 player aircraft, matched by volume overlap.
Result — the flipped boxes contain **only** wing geometry (`l/r_aileron2` on the Devastator,
`leftwing`/`rightwing` plus `l/r_aileron1` on the Bloodhawk, `l/r_aileron1` on the Firebrand,
the Fury's wingtip `pdp*_h` damage panels, and the autogyro's overhead rotor — which
`PlaneCollider`'s own class doc already calls that plane's wing). **Every `*_rudder*` node in
the fleet sits in a box the rule leaves alone:** the Devastator's and Firebrand's fins at
|x| 3.03 fall inside their planes' centre tail box, and the Kestrel's twin fins sit at |x| 1.98
against a 2.58 m band. The autogyro's single flip is the thinnest margin in the fleet (1.68 vs
1.62 m) and is worth knowing if `WingBandFrac` is ever retuned.

**Per-aircraft collider-name comparison** (9 boxes moved of 87; sizes and centres bit-identical
before and after, which is what proves the change is label-only):

| aircraft | halfSpan | wingBand | tail -> wing | note |
|---|---|---|---|---|
| Devastator `player_pfighter` | 5.46 | 1.91 | 2 (cx +-4.24) | `l/r_aileron2`; twin rudders at +-3.03 stay in the centre tail box |
| Bloodhawk `player_bhawk` | 5.80 | 2.03 | 2 (cx +-3.34) | the reported case — `leftwing`/`rightwing` nodes |
| Firebrand `player_fbrand` | 9.46 | 3.31 | 2 (cx -6.31 / +6.49) | `l/r_aileron1`; twin rudders stay centre |
| Fury `player_fury` | 5.49 | 1.92 | 2 (cx +-3.36) | wingtip `pdp*_h` damage panels |
| Autogyro `player_autogyro` | 4.62 | 1.62 | 1 (cx +1.68) | the overhead rotor; thinnest margin |
| Brigand, Hellhound, Kestrel, Peacemaker, Balmoral, Warhawk | | | **0** | unchanged |

**Verification.**
- Mesh-lab box overlay (`--viewer --plane=player_bhawk --debug-mesh=boxes`, top-down): the wide
  magenta *tail*-coloured band spanning both wings is gone; the strips draw cyan (leftwing) and
  orange (rightwing), and only the centre box stays magenta. The overlay colours boxes by
  `MapStruckPart`, so it shows the mapped damage part, not the raw label.
- **Scripted flight A/B, the decisive one.** C1, Bloodhawk, `--spawn-at` low over the map with
  `--hold=0,1,0,0.5` (continuous roll -> knife-edge wingtip drag), swept over five spawn
  altitudes. At y=188 the same graze reports `graze (tail->tail)` before and
  `graze (wing->rightwing)` after, at **identical** vn 2.2 m/s, dmg 0.1 and hp 19.9/20 — the
  physics did not move, only the attribution. Every other logged line is identical between the
  two builds, including the `tail->tail` grazes and the `CRASH ... (tail)` lines at other
  altitudes: real tail hits still score tail. Only one of the five altitudes discriminated at
  all, which is now recorded as a rule in `docs/verification.md`.
- All 11 aircraft rendered in plain `--viewer` are **byte-identical** (md5) before and after —
  correct, since the overlay is off by default and only labels changed. The overlay-on shots
  *did* differ, so the instrument was shown able to report a difference.
- Mode battery, zero errors in each: `--fly`, `--stunt`, `--viewer`, `--viewer --damage`,
  4-player `--stunt --players=4`, `--freecam --chapter=C5`. The Kestrel's three tail boxes
  (0.5x1.9x3.8 / 3.9x0.3x1.7 / 0.5x1.9x3.8) survive intact in the stunt run's collider log.

**The plan's evidence was correct in full** — line numbers, the 4.9 x 0.4 measurement, the
single-axis clip, the verbatim name propagation and the `MapStruckPart` asymmetry all held up.
One thing it implied that is not true: the strips are not *entirely* outboard (the Bloodhawk's
run from |x| 0.88 out to 5.80, crossing the 2.03 m wing band), so an "entirely outboard" test
would have matched nothing. The box **centre** against the wing band is the test that works.
## 2026-07-22 — `LOOP_COUNT 0` means infinite: C1/C2/C3 traffic drives its route forever (plan item 8)

**Symptom.** C1's police car, mafia car, black car, truck and the two loop cars drove their route
exactly once and froze wherever the last leg ended. Same for C2's ten studebakers and C3/M02's
nine. The original loops all of them.

**Cause — one condition.** `SequenceRunner` read a `Loop` event's count and treated `0` as "stop
now" (`_loopsLeft == 0 → _done = true`). Every one of these defs ships `Count: 0`.

**The survey says `0` is the data's second spelling of "infinite".** Re-run here from the
extraction rather than taken on trust, and it confirms the plan:

| `Loop.Count` | events (compiled `cam_anim`/`mis_anim`) |
|---|---|
| `0` | **26**, in **25** defs |
| `-1` | 2,919 |
| positive N | 530 |

All 26 are ground-vehicle route animations — C1's six traffic defs, C2's ten studebakers,
C3/M02's nine — every one `activation: OnStartup`, and every one with its `Loop` as the **last
event of its sequence** (verified positionally on all 26). No door, gate, one-shot, hangar, bomb
or explosion def carries `Count: 0`, so the risk the backlog flagged — a `Count: 0` def that must
terminate — does not exist. Blast radius: C1, C2, C3. C1B, C1C, C2B, C4 and C5 have none.

**Fix.** Normalise the authored count at read time (`authored == 0 ? -1 : authored`) rather than
special-casing the test below, so `_loopsLeft == 0` keeps meaning "a *finite* loop has run out" —
the only way a positive count can ever terminate. `AnimRuntime.cs`, one hunk.

**One plan claim was wrong, immaterially.** The plan states the chapter/mission `zrdr` scopes
"contain zero `Loop` events". They contain **703**. What is true — and is the claim that matters
— is that **none of them has `LOOP_COUNT 0`**: the reader scope runs `-1` (575) and positive
counts only. So the reader path is untouched either way, but the stated reason was not the real
one. Corrected in `docs/formats/anim-definitions.md`.

**Verification — the runaway check is the point.** A loop the runtime never exits is only safe if
each iteration consumes time. Measured, not argued:

- **A/B on C1, 100 s each, `--freecam --chapter=C1 --spawn=0 --debug-anim`.** The per-second
  motion log truncates at 12 entries, which hides exactly these cars once a route restart
  re-registers their motion at the end of the list — so both runs were taken with the cap
  temporarily raised (reverted before commit; **the 12-cap is a real trap for this measurement**).
  Before: `mafia` last seen at t=14, `black_car1` t=14, `car_go_home` t=20, `car_loop1` t=23,
  `police_car` t=51, `truck1` t=73 — each exactly its authored route length, then frozen. After:
  all six still moving at t=96, restarting every 16 / 16 / 21 / 52 s respectively, which is their
  authored `run_time` to the second. Live motions hold at ~36 instead of decaying 41 → 29.
- **C2 and C3/M02, 110 s each.** C2 base decays 41 → 25, after holds 35 (**+10** = its ten
  studebakers). C3/M02 base decays 33 → 24, after holds 32–33 (**+9**). Restart periods measured
  per car: C2 46/40/40/40/40/35/35/35 s, C3/M02 32/26/40/44/35 s — every one equal to that car's
  authored route time. **No iteration completes in zero time**, so the degenerate two-event cases
  (`C2/studebaker1`, `C1/mafia`) cannot hang; their 46 s and 16 s legs schedule time on their own.
- **No runaway by the indirect measures either**: identical debug-tick counts in the same wall
  clock (107 vs 107 on C2, 108 vs 108 on C3/M02, 32 vs 32 on the short runs) and no log growth
  (1637 vs 1637 lines on C2), so nothing is spinning inside a frame.
- **8-chapter regression, zero errors.** Bootstrap figures **identical on all eight** — ON_STARTUP
  count, start anims, live instances and live motions all match; only the `[index/reset/start ms]`
  timings move, which is noise. Correct by construction: a `Loop` only runs after bootstrap.
- **C4 was checked twice on purpose.** A first pass showed C4 decaying to 3 motions on the
  baseline and holding at 24 after — impossible, since C4 ships no `Count: 0`. It was a dirty
  baseline (see below), not a real effect. Re-run as a clean one-line A/B, C4 is **24 → 24 on both
  sides**, and C1B/C1C/C2B/C5 were unchanged in both passes.

**Process note worth more than the fix: this worktree was not isolated.** Another agent was
editing `AnimRuntime.cs` and `CompiledAnim.cs` in the same directory concurrently. Two things
followed, both of which cost a re-run:

- **`git stash` is repo-global and its stack is shared across worktrees.** Using
  `git stash push <path>` to produce a baseline build silently lost this change when the
  concurrent agent rewrote the same file, and the stash list showed another worktree's entry.
  **Do not use `git stash` to make a baseline here.** Flip the one line under test, build, run,
  flip back — that is what the clean A/B above did.
- **The first "baseline" build therefore contained someone else's in-flight change**, which is
  what manufactured the phantom C4 result. This is `docs/verification.md` §5 ("before you trust
  the baseline") arriving through a new door: the baseline was not merely stale, it was *someone
  else's working tree*. Confirm what the baseline build actually contains before comparing.

## 2026-07-22 — Polish-3 item 9: the 1e29 zeppelin transforms were an unread `spline_interp` flag

**Symptom.** C1/M04 logged `sound: 'snd_zepengine' silenced — its host node's world pose is
degenerate ((-962.483, 3.81e28, 4.58e29))`, and `--debug-anim` showed `counterspin` sitting at
`(4610951000000000000000000000, -5.87e28, -3.57e29)`. Known since the ambient-sound work and
carried in `backlog.md` as a pre-existing defect.

**What the plan predicted, and why it was wrong.** Item 9 diagnosed `ScriptPlayback.Seek` as the
only non-rest-based transform writer: it read the *live* basis, so a frame carrying `scale` but no
`rotate` multiplied its factor into an already-scaled basis every frame. The supporting numbers
were excellent — 207 such frames across 12 scripts, 143 of them the C1 gasbags, `lkgasbag02`
carrying `scale.base.x = 1.52`, and 1.52^150 ≈ 1e27 landing squarely on the reported magnitude.
The plan also flagged, honestly, that it could not show this firing at bootstrap on M04. **That
gap was the whole story.**

**What it actually was.** `SiScript.SplineInterp` was parsed and then **read by nothing** — dead
since the SI-script reader landed. On the **15 of 1,090** scripts that set `spline_interp: false`,
the per-axis coefficient blocks are *uninitialised memory*, and we evaluated them as cubics
anyway. C1/M04's `piratezep.zan` decodes a scale channel whose constant terms are
`(0.0, 4.259e27, 4.611e27)`: a **singular** basis — one zero axis, two astronomical — inherited by
every descendant along `piratezep → move_zeppelin → tilt_zeppelin → rock_zeppelin → gasbagN →
engineN → spin`. That is the whole blowup, present in the very first `Seek(0)`, with no
accumulation involved.

**How it was pinned.** Not by magnitude but by an **exact value**: the script's scale constant is
`4.6109513952913965e27` and the logged `counterspin` world X was `4610951000000000000000000000` —
bit-identical. Before that, the first instrumentation attempt threw `ArithmeticException` out of
`Basis.get_Scale()`, and the stack trace was worth more than the log line it failed to print: it
placed the corruption inside `ScriptPlayback`'s **constructor during bootstrap**, which rules out
per-frame compounding outright. The plan's 12 flagged scripts turn out to all be among the 15 —
the file-level evidence agreed while the mechanism did not.

**The decode.** Both non-spline rules were verified against the next frame's `base` across the
whole non-spline set: translate/scale is `base + delta·dt` (worst relative error **1.8e-4**,
float32 rounding), rotate is `exp(delta·dt) ⊗ base` (worst angular error **0.022°** over all 576
rotate frame pairs, versus **21.6°** if `delta` is ignored and the base merely held). So `delta` is
a per-second rate in both, and a non-spline frame is *exactly the degenerate cubic*
`(base, delta, 0, 0)` — which is how it is implemented, resolved at parse time so `At(dt)` stays
branch-free and the 1,075 spline scripts take a bit-identical path. Documented in
`docs/formats/anim-definitions.md`, which previously recorded `pfighter11.zan`'s "uninitialized
spline memory" as a one-off; it is one of the 15.

**The plan's own fix still landed, with one correction.** `ScriptPlayback` really could compound,
and that is fixed independently. But the prescribed shape — "store a rest like every sibling
runner does" — would have regressed `piratezep`, which sets its orientation once in frame 0 and
then ships 47 translate-only frames: **an absent channel means "hold the last value this script
wrote", not "return to rest"**. The landed form keeps `_rot` (orthonormal), `_scale` and `_origin`
as three separate running components seeded from `RestOf(target)`, making `Seek(t)` a pure
function of `t` while preserving hold semantics.

**`SpinMotion` logged, not fixed.** Its `_rest` is captured from the live pose and the idempotence
guard matches only identical `(rate, runTime)`, so `zeppelin_rocksleft`'s five differing events
drift. Bounded (orthonormal), so it was never a blowup candidate. Both candidate fixes risk a
visible regression to cure an invisible one — seeding from `RestOf` would discard the deliberate
pre-spin pose that C1/M05's `random_prop` sets on all 590 spins, and inheriting the previous
motion's rest assumes an oscillation where a chained eased rock is equally plausible. Needs the
original game; entry in `backlog.md`.

**Verification.** Symptom reproduced on the unchanged build first, then cleared: C1/M04 goes from
1 degenerate-pose line to **0**, `snd_zepengine`'s host `spin` reports a finite
`(-5374, 1521, -1765)` and **plays**, and the pirate zeppelin now actually flies its authored
intro path at ~1500 m instead of departing the float range. Full 8-chapter `--debug-anim`
regression, base build vs fixed build: **every count identical** (gamez nodes, mesh instances,
defs, SI scripts, anchored, state ops, unresolved, ON_STARTUP, live instances, live motions), zero
real errors, zero degenerate lines. Renders byte-identical on 5 of 8 chapters; C1C/C2B/C4 differ by
5.3%/7.5%/6.6% of pixels, which is **inside their own same-build noise floor** (measured
5.0%/6.8%/7.6% — C4's base-vs-fixed delta is *lower* than its floor), i.e. the documented
precipitation non-determinism in `docs/verification.md` §7 and not this change. Files:
`CSVM/src/Mech3/CompiledAnim.cs`, `CSVM/src/Mech3/AnimRuntime.cs`.
## 2026-07-22 — Polish run 3 item 2: weather zones are per chapter, and C5 had no fog at all

**A live bug, not a polish item.** `Weather.Load` iterated a hardcoded
`foreach (var zone in new[] { "ZONE1", "ZONE2" })`. But the zone *names* are per chapter: C1–C4
ship `ZONE1`+`ZONE2`, while **all 8 C5 missions ship `ZONE1`+`ZONE3`** (verified across all 53
`weather.json` in the install, and corroborated by the `horizon` subtree's own child names —
C5's are `zone3`/`zone1`, everyone else's `zone1`/`zone2`). So on C5 the `zone2` default matched
nothing, `Fog("zone2")` fell through to `NoFog` (near/far 1e8/1e9, `WorldLight` 1 = fullbright),
and `BuildHorizon("zone2")` skipped *both* zone children — **every C5 flight since the fog work
landed had no fog, no sunlight model, and an empty skydome.**

**What changed.** `ZoneKeys(inner)` walks the raw alternating list for `ZONE<digits>` keys in
**file order** (raw rather than via `ZrdrDict`, because a `Dictionary` does not preserve order and
file order is what the fallback walks; the `SW_ZONE*` software-renderer twins stay excluded).
`WeatherState.ResolveZone(requested)` returns the request when the mission defines it, else the
file's first zone; `ZoneNames` exposes the list for the log. `PlaneViewer.SetupWeather` split in
two: `LoadWeather` now runs **before** the per-rig `BuildHorizon` loop and resolves `_activeZone`
once, and the dome, the fog and the log line all read `_activeZone` — so the sky and the fog can
never come from different zones, and the fallback logs once per session rather than once per rig.
`WorldBuilder.ResolveHorizonZone` carries the same fallback against the horizon's own zone
children, guarded by `_loggedHorizonZoneFallback`; in the normal path it is a no-op, and it exists
only so a mission with no `weather.json` still gets a dome.

**The default deliberately stays `zone2`** (user decision). Which zone a mission actually flies is
in **no file in the install** — searched the mission `zrdr`, all 53 `.gw` interp scripts (1,215
statements, zero zone mentions) and the ROF/DLL string tables; the only zone references are
chapter-level and mutually contradictory (`support\c1\load.gw` names `zone2_cloud_floor`,
`support\c1\tex_fx.gw` names `h_zone1scroll`). Selection happens engine-side in the binary. That
negative result is now written up in `docs/formats/weather.md`, and settling it is the user's A/B
against the original (the plan's closing section) — a fallback is the only correct move without
that answer.

**Verified.**

- **C5 before/after, same pinned pose:** fog appears, and the previously-empty dome now shows
  zone1's stars. `weather [zone1]: fog 0.00 gray 1500–2250 m` replaces
  `weather [zone2]: fog 0.69 gray 100000000–1000000000 m`.
- **The strongest check:** the new build's *default* C5 render is **byte-identical
  (0/921600 px, max delta 0)** to the **old** build's explicit `--sky-zone=zone1` render. The
  fallback produces exactly what asking for zone1 produced, and nothing else moved.
- **8-chapter regression** (`--freecam --spawn=0`, pinned): 0 errors. Mesh instances unchanged
  everywhere except C5, **4723 → 4726** — the +3 is precisely the zone1 horizon subtree that used
  to build empty. Every C1–C4 `weather [zone2]` line is character-for-character identical.
- **Screenshot diffs against a same-build noise floor** (verification.md §7): C1B/C3
  byte-identical; C1 46 px floor vs 78 px changed, C2 13 vs 19 — noise. C1C/C2B/C4 are the
  precipitation chapters and sit in their known self-animating band; **`old2`-vs-`new` came out
  *below* their own `old`-vs-`old2` floor** (C1C 18.0% vs 21.2%, C4 13.8% vs 17.2%, C2B 5.2% vs
  5.0%), which is what confirms the spread is precipitation noise rather than a change. C5 58.2%
  at max delta 222 — the intended fix.
- **Both fallback logs shown able to fire, and able to *not* fire.** The weather line prints
  exactly once, for C5 only, and never for C1–C4. The horizon line is unreachable in the normal
  path by construction (weather pre-resolves it), so it was forced with
  `--chapter=C5 --mission=M05` (a mission that does not exist → no weather.json): it printed
  `horizon: no 'zone2' subtree (has zone3/zone1) — building 'zone3'`. Re-run under
  `--players=4`: still **one** line, confirming the per-rig log-once guard.
- Untouched by construction: the static plane viewer. `LoadWeather`/`BuildHorizon` are both inside
  the world branch; `--plane=`-only sessions take the `else`.

**Plan evidence correction.** Item 2 stated "C5's dict is never populated". Not quite — the
hardcoded loop *does* find `ZONE1`, so `--sky-zone=zone1` already worked on C5 before this change;
what was unreachable was `zone3` and the whole default path. The symptom and the fix are
unaffected, and the correction is what made the byte-identical verification above possible.
Recorded in the plan file.

**Also documented in the same turn** (both plan-mandated write-ups of findings, not new work):
the **`zone_id`** node field in `docs/formats/world-structure.md` — every gamez node carries one
(−1 = always, else the zone number), per-chapter counts re-verified against `nodes.json`, both
zones spanning the whole map so they are alternative world *variants* rather than regions; nothing
in `CSVM/src` reads it, and it stays unimplemented for exactly the reason above — we do not know
which zone is active, so hiding geometry would be a guess. And **`FogState`** in
`docs/formats/anim-definitions.md` as decoded-but-unacted-on: one occurrence install-wide
(C1/M04's intro cutscene camera), carrying fog parameters inline and matching neither C1 zone, so
it is an ad-hoc third fog state and *not* the zone selector.
## 2026-07-22 — Polish run 3 item 3 investigated and NOT landed: the C5 ground z-fight is one mesh fighting itself

Item 3 was scheduled as "coarse `world1`-child ground sheet coplanar with the fine
partition-referenced city ground; fix by draw priority". That rule was implemented exactly as
specified — a `SceneBuilder.BackgroundRank` predicate giving World-child coarse sheets one
fixed negative `node_bias` (`-DepthBiasPerLevel`), below every index-derived value, plus a
`WorldBuilder` classifier (flat to within 2% of span, and coarser than the fine ground's own
16-polygons-per-partition-cell granularity) that matched exactly 7 nodes, all in C5. It built
clean, 7 of 8 chapters stayed **byte-identical** (md5), and it changed **nothing** about the
bug. It has been reverted; no code shipped. The corrected diagnosis is recorded in
`backlog.md`, `docs/plans/PLAN-M2-polish-3.md` item 3 (banner box) and `docs/verification.md` rule 6.

**What it actually is.** At the plan's own repro pose, hiding `g4683` (node 1777) alone drops
the flicker to 0.19% — the *identical* figure to hiding all seven sheets. `g4683` carries **8
pairs of its own polygons exactly coplanar at y = 5, priority 0, overlapping by up to
768 × 512 units**, five of them on the same material (`cblock1.tif`). `BuildMesh` groups
polygons by *(material, priority, sidedness)*, so those five pairs land in **one surface with
one shared depth bias** and no tie-break can separate them; the original ordered
equal-priority polygons by their position in the polygon list. Polygons 0–7 occupy
x ∈ [−10240, −9216], z ∈ [−4096, −3072] — exactly where the repro camera looks.

| repro pose, `--shots=5 --jitter=0.006`, flip threshold 8/255 | flip rate |
|---|---|
| unmodified build | 35.77% |
| the prescribed coarse/fine node rank | 35.79% (no effect) |
| hide `g4616`+`g4425` (both nested inside `g4683`) | 35.79% (no effect) |
| hide `g4683` alone | 0.19% |
| hide all 7 coarse sheets | 0.19% (identical) |
| one draw-order rank per POLYGON | 21.92% |

**Three further corrections to the plan's evidence.** (1) *"Generalises to C1 (`a6`), C1C and
C4 (`g1612`)"* — no. Only C1, C4 and C5 have any World-child mesh at all (1 / 4 / 9); **C1C
has none**; `a6` is the detailed airfield tile (27 polys, 5.15% relative height, internal
priorities 0/1/3) and `g1612` is a 477 m cliff, so demoting either would regress. (2) The
coverage table did not reproduce — an independent 64-unit rasterisation gives 18.2–81.4%, not
0.0–97.8%; the *conclusion* (no sheet fully covered, culling would leave holes) survives, the
per-sheet numbers should not be quoted. (3) The seven "coarse-only" reference captures were
not produced, because they presume the disproven model.

**Confirmed and reusable:** World children and partition roots are exactly disjoint
(intersection 0 in all 8 chapters), and the `terrain` node flag is set on **every** partition
root and **no** World child — so "partition-referenced" is readable straight from the data
without walking the partition grid.

**Measurement lesson (now `docs/verification.md` rule 6).** "Hide one side and the artifact
goes away" proved neither which two surfaces were fighting nor that there were two. It was
also a confounded control: removing the sheet removed the only textured surface in the near
field, deleting the grazing-angle mipmap/aniso resampling noise along with the depth flips —
most of that 35.77% was never a depth fight, which is why the only intervention that changed
anything (per-polygon ordering, which alters nothing but depth) reached just 21.92%.

**Process note — `git stash` is shared across worktrees.** The stash stack lives in the common
`.git` dir, not per-worktree, so concurrent agent sessions push and pop each other's entries:
this session's `git stash pop` returned the item-9 session's `AnimRuntime.cs`/`CompiledAnim.cs`
WIP. It was detected immediately, their commit was put back with `git stash store`, their files
were reverted here, and no measurement was taken from the mixed tree. **Do not use `git stash`
in a worktree session** — keep work in the working tree or in a local commit on the branch.

## 2026-07-22 — Polish-3 item 4: the cloud deck is picked structurally, and C4 finally has one

**The bug.** C4's overcast deck never followed the plane. `WorldBuilder` selected the deck by
texture prefix `cloudlayer`, so `CloudDeck` came back **null** in C4, PlaneViewer had nothing to
re-anchor, and the sheet stayed world-fixed while the plane flew out from under it. Baseline
`--players=2` run, which is the only place the old build reported the verdict at all
(`deck=own`/`deck=none`): C1 own, C1B none, C1C own, C2 none, C2B own, C3 none, **C4 none**,
C5 none.

**The plan's core evidence held exactly.** C4's deck is `Sky1.tif`: 144 parentless
partition-referenced nodes `g1720..g1863`, each a single 4-vertex flat 1024×1024 quad at
y=1050 — bit-for-bit C1's `cloudlayer` signature at y=960 (both `model_type=Default`,
`facade_mode=CylindricalY`, `transform=Initial`, 1 polygon). Confirmed against the extraction
for all 8 chapters.

**Two of its claims did not.**

1. **"Widening the predicate to `sky*` is only safe because `Build` skips the `horizon`
   subtree."** It would not have been safe at all, and `horizon` is beside the point.
   `skywal*` is a **building** texture *inside* the world walk: 33 C4 nodes (the sky-city
   `pod2_*`/`pod6_*` structures), 4 in C1/C2/C3, 2 in C1B (`racmplx`, `rabdr`). Worse, at the
   level the classifier actually runs — the root of each walked subtree — three C4 **terrain**
   roots carry a `skywal01` polygon alongside their cliff / `rr_tracks` / `trestlegirder` ones.
   A `sky*` rule would have moved those three solid terrain chunks into the cloud deck and made
   them follow the player.
2. **"`IsCloudSpriteTexture` additionally billboards it as a sprite."** It does not and never
   did. That predicate requires the prefix `cloud`; `Sky1.tif` does not start with `cloud`, and
   the tiles are `model_type=Default`, not `Facade`. C4's deck was rendered flat and correctly
   the whole time — the only symptom was that it did not follow. No change was needed there and
   none was made.

**The fix — coverage, not names.** `FindCloudDeck` runs a pre-pass over the walk roots before
`Add`. `FlatTile` accepts a root whose model is one flat horizontal quad (1 polygon, 4 vertices,
all four transformed corners at one Y); those are bucketed by altitude in 1 m buckets, each
tile clipped to the World node's own `area` rect, and the bucket covering ≥ 50 %
(`DeckCoverageFraction`) of the map becomes the deck. The margin is not close:

| chapter | bucket | tiles | altitude | coverage |
|---|---|---|---|---|
| C1 / C1C / C2B | `cloudlayer.tif` | 144 | 960 | **1.000** |
| C4 | `Sky1.tif` | 144 | 1050 | **1.000** |
| C5 | `wtr00000` / `cblock1` / `cblock2` / `cblock3` | 25 / 46 / 12 / 17 | 0 / 5 | ≤ 0.098 |
| C2 | `wtr00000` / `resblock2` / `resblock_trans2` | 1 / 2 / 2 | 0 / 8 | ≤ 0.003 |
| C4 | `wtr00000` | 2 | 517 | 0.002 |
| C1B, C3 | — no flat-tile bucket at all — | | | |

A 10× margin either side of the threshold, which is why summing clipped tile areas instead of
unioning them cannot flip a verdict. Deliberately **not** used as the discriminator: altitude
(it would be a magic number, and C4's tallest non-tile root reaches y=1490 — *above* its own
deck at 1050, so "the thing above the world" is not what a deck is), and texture name.

**Corroboration that the deck-follow is right for C4.** The follow code parks the deck at the
`CLOUD_COVER` band centre. C4's band is `TOP 1100 / BOTTOM 1000` → centre **1050**, which is
*exactly* the altitude the data already put the deck at, so the vertical placement is a no-op
there and only the X/Z tracking is new. (C1 960 vs centre 1047, C1C 960 vs 1082.5, C2B 960 vs
1024 all do get lifted.) An exact value, not a magnitude — `docs/verification.md` rule 6.

**Verification.**

- **8-chapter regression, baseline vs fix** (`--fly --players=2`, the deck verdict + counts):
  only C4 moved, `deck=none` → `deck=own`. Mesh-instance and collider counts **identical** in
  all 8 (C1 3524/2565, C1B 3164/1451, C1C 3033/1752, C2 2074/1871, C2B 2773/1354, C3 2442/2361,
  C4 4277/2529, C5 4791/4372). Zero errors either side. C1B/C2/C3/C5 still resolve **no** deck —
  the specific failure mode being watched for (a newly-matched skydome) did not occur.
- **The follow itself**, `--freecam --chapter=C4`, camera above the deck looking down, two
  poses: **A** = map centre; **B** = x=+8000, i.e. 8000 m beyond the map's east edge
  (`area` x ∈ [-12288, 0]) and so outside the deck's *authored* footprint. Baseline at B shows
  the map-edge extender's mirrored terrain; the fix shows the deck occluding it —
  **100.00 % of pixels changed, max delta 146**, against a same-build noise floor of 4.00 %
  (fix) / 62.45 % (baseline, precipitation over textured terrain). At pose A the two builds
  differ by 0.10 % / max delta 3, inside the fix's own 0.10 % floor — correct, because that
  view down was already full-fog gray with or without the deck. A world-fixed deck cannot be
  overhead at x=+8000; it was.
- **Not billboarded:** an edge-on shot 150 m under the deck at the same outside-the-map position
  reads as a flat ceiling receding into fog, and the predicate provably cannot match `Sky1.tif`.
- New `cloud deck: N tiles at y=… (…% of the map)` log line — the only single-player-visible
  signal that a deck resolved (`deck=own/none` is splitscreen-only). Shown able to report
  failure: it prints in C1/C1C/C2B/C4 and is silent in C1B/C2/C3/C5.

**Measurement trap, now `docs/verification.md` §5.** Restoring the fix over the baseline with
`Copy-Item` made `dotnet build` a **silent no-op**: `Copy-Item` preserves the *source's*
`LastWriteTime`, the restored `.cs` was older than the already-built DLL (13:52:58 vs 13:55:12),
MSBuild judged the project up to date, and Godot loaded the **baseline** assembly. The run
produced a frame pixel-identical to baseline and read exactly as "the fix does nothing". What
caught it was the new log line being *absent* — a build-freshness assertion that a screenshot
alone would never have provided. `git checkout HEAD -- <path>` was used for the reverse
direction and is safe (it writes a fresh mtime); `git stash` was not used anywhere.
## 2026-07-22 — Polish-3 item 5: one billboard classifier; billboards and clutter lose collision

Two halves plus a live bug found mid-item. **Net rendering change: none** — every screenshot
sits at or below its same-build noise floor, and C5 (the one fully deterministic pose, 0 px
floor across repeated runs) is exactly byte-identical.

**(a) One classifier.** The billboard rule was already data-driven (2026-07-21) but split
across two private `SceneBuilder` methods, with `ClutterBuilder` running an independent
1-poly/4-vert/flat-Z shape heuristic and `WorldBuilder` gating collision on poly-count plus
texture name. It is now `public static SceneBuilder.BillboardKind? ClassifyBillboard(GameZMesh)`
(`None`/`Spherical`/`CylindricalY`/`CylindricalX`), consumed by all four call sites. It returns
**null** when the extraction carries no `ModelType`, which is how the per-caller legacy
fallbacks are preserved — they encode genuinely different intents and folding them together
would silently change what a v0.6.1 rollback tree renders. `BuildMesh` was deliberately left
byte-identical: `GetCylindricalAxis` still returns the private `CylAxis` that keys the shader
caches, now derived from `BillboardKind`.

Cross-check that made the clutter switch safe: every clutter template decoration in the install
is either a 1-polygon `Facade` (tree/bush/palm/lightpole card) or a 2–27-polygon `Default` (3D
building), so the data rule and the old shape rule agree exactly — clutter counts are unchanged
in all 8 chapters (C1 9,303 · C3 371 · C4 88,630 · C5 139,388). Note the trap the survey
confirms: the 3D building decorations carry a **stale** `facade_mode: CylindricalY` while being
`model_type: Default`, so the *type* must be the discriminator.

**(b) Clutter and billboards lose collision.** The plan's premise held and is now recorded in
`docs/formats/clutter.md`: `Clutter.cs` and that page both justified tree collision with
"`spruce_destroy` anims exist", but the two strings are
`..\data\common\zrdr\planes\spruce_destroy{1,2}.zrd` — the **`planes\`** folder — and the files
define `g_engine*` anims with `prop_part`, `spin`/`counterspin`, `snd_propstart` and engine
puffers. That is the **Spruce Goose**, the C2/M01 mission object. There is no spruce-*tree*
animation, and no tree-destruction animation of any kind, in the install. Both doc claims
corrected. Clutter collision removed outright (user decision): −37,212 collision triangles in
C1, −354,520 in C4, −557,552 in C5, plus the `MapEdgeExtender` mirror.

**(c) Live bug found and fixed: `skywal*` is a building texture, not sky.** Handed over from the
item-4 session and verified here against the data. `NoCollisionNode` exempted anything matching
`IsCloudOrSkyTexture` (`cloud*`/`sky*`), and `MeshUsesTexture` matches if *any* polygon carries
the texture — so one `skywal01` face made whole structures phantom: C4's `pod2_hi` (73 polys,
107×88×125 m), `pod6_hi` (143 polys), `g74` (64 polys, **395×135×275 m**), and C1/C1B/C2/C3's
`g456` (181 polys, 113×49×102 m), `racmplx`, `rabdr` — each mixing `skywal*` with `jim_floor01`,
`jim_rail1`, `jim_roof01`, `bhfbuild03`, `flaghut2`, `flagstand`. Fixed by narrowing the
**collision** predicate only (`IsNonSolidSkyTexture` = `IsCloudOrSkyTexture && !skywal*`), not
the shared `IsCloudOrSkyTexture`, which also drives the cloud alpha-blend and `MapEdgeExtender`'s
ground-tile filter — so rendering is provably untouched and C4's 144 `Sky1.tif` deck quads stay
exempt.

**Collider counts, the two deltas reported separately** (they move in opposite directions and
would otherwise mask each other):

| chapter | baseline | Δ billboards | Δ `skywal` | final |
|---|---|---|---|---|
| C1 | 2565 | −157 | +4 | 2412 |
| C1B | 1451 | −21 | +2 | 1432 |
| C1C | 1752 | −19 | 0 | 1733 |
| C2 | 1871 | −27 | +4 | 1848 |
| C2B | 1354 | −19 | 0 | 1335 |
| C3 | 2361 | −47 | +4 | 2318 |
| C4 | 2529 | −28 | **+44** | 2545 |
| C5 | 4372 | −119 | 0 | 4253 |

A static probe replicating the world walk reproduces the runtime counts **exactly in 6 of 8
chapters** (off by 6 in C4 and 11 in C5 — unexplained residual, stated rather than papered
over), and lists every node that loses collision: all 1–2-polygon cards ≤29.8 m — streetlight
poles, stands, zeppelin cables, rail signal lamps, water-tower details, muzzle flashes,
distant-plane sprites, seagulls, workmen. No terrain, no building, no ship.

**Also landed:** `WorldBuilder.DisableFog` deleted as dead code (its only call site had been
commented out since the 2026-07-17 fog remodel), and `docs/architecture.md` corrected — it
described the method as "disabled by an early `return`", which it never had.

**Deliberately NOT landed: the `csky_fog_on` instance-uniform alignment.** The index
disagreement is real (`Clutter` 0, `SceneBuilder` 1) but **verified latent, not live**: the two
families never share a `GeometryInstance3D`, and an 8-chapter run logs no `instance_uniforms.cpp`
warning at all — so the plan's "confirm the C5 warning is gone" could not be satisfied, because
there is no such warning. Padding Clutter's shader with an unused `node_bias` to align the
indices was implemented, measured and reverted: it enforces nothing (the next shared uniform
still has to be added to both shaders by hand) while adding a uniform the shader cannot use. The
enforcing fix is a preamble constant shared with `GetBiasShader`, which belongs with that shader;
logged in `backlog.md`, and the invariant is documented at both declarations.

**Verification.** 8-chapter `--fly` regression: mesh-instance counts identical in all 8, clutter
counts identical, zero engine errors, no shader warnings. All 11 aircraft build clean in
`--viewer`. Mode battery clean: fly / stunt / viewer / freecam / 2P split / 4P stunt race /
damage lab. Screenshot A/B against a same-build noise floor measured over three runs per pose —
C5 0 px floor and 0 px signal; C1 14 px floor, 14 px signal; C3 and C4 at their known
water-flipbook and precipitation floors.

**Measurement lessons (both now in `docs/verification.md`).** `--fly` is useless as a
screenshot-diff instrument — its same-build floor measured **30–84% of pixels** because the
plane, camera and animations all advance; the first A/B run with it produced numbers that looked
like a signal and meant nothing. And a *concurrently running* Godot from another agent's worktree
silently corrupted this session's captures: one screenshot never wrote and another came out at
1/7 the file size, which read as a code fault until the stray processes were spotted.
## 2026-07-22 — polish-3 item 11: per-polygon within-surface tie-break — DISPROVEN, nothing landed

**Outcome: no engine change.** The mechanism this item was written around does not exist in the
data, the fix built on it changes nothing at any of the three reported z-fight poses, and it was
reverted. What the session did produce is the first measurement of this renderer's
depth-resolution floor, which reframes all three reports as one problem and explains why two
previous diagnoses of the same bug were both wrong.

**The premise, and why it is false.** Item 11 rested on "C5's `g4683` (node 1777) carries 8 pairs
of its own polygons exactly coplanar at y=5, overlapping by up to 768×512 units". That is an
**AABB** overlap. Clipping the real outlines (Sutherland-Hodgman, true polygon ∩ polygon area)
gives **zero** overlap for every pair in that mesh. The worst-overlapping pair of bounding boxes:

```
poly3  (-9600,-3712) (-9472,-3712) (-9216,-4096) (-10240,-4096)
poly4  (-9600,-3712) (-10240,-4096) (-10240,-3584) (-9600,-3584)
```

They **share the edge (-9600,-3712)→(-10240,-4096) exactly** and lie on opposite sides of it —
abutting ground tiles, which is what most of this terrain is. Install-wide the same substitution
inflates the count of genuinely conflicting polygons from **0.6–1.6% to 9–26%**, a 15× error.
Recorded as `docs/verification.md` rule 9.

**The fix was built properly anyway, and moves nothing.** Per-polygon rank delivered to the
shader in `UV2.x`, restricted by the area test to polygons with a genuine coplanar overlapping
sibling in the same surface, applied as a **pushback on the loser** (`- poly_bias * UV2.x`) so no
polygon is ever pulled toward the camera and nothing can newly occlude anything. Instrumented and
shown able to fire: 270 polygons ranked in C5. Measured with `--shots=5 --jitter=0.006`, flip
threshold 8/255, run-to-run noise floor **0 px** (same build twice gave bit-identical 331,380 px):

| pose | before | after |
|---|---|---|
| C5 `g4683` repro | 35.96% | **35.96%** |
| C3 beach | 0.41% | **0.41%** |
| C1B | 30.95% | **31.00%** |

8-chapter regression: identical gamez node and mesh-instance counts, zero errors, no visible
change. Largest genuine pixel delta was C3's 15,864 px — invisible stipple along the shoreline.
C2B's apparent 24,148 px was **precipitation noise**: same-build-vs-same-build gives 24,117 px at
max delta 1. Load time unaffected (the test runs once per unique mesh behind the mesh cache, and
only 2.6k–10.6k polygon pairs per chapter survive the plane+AABB reject to reach the area clip).

**The "35.77% → 21.92%" result that motivated the item was a step-size artifact.** A *blanket*
per-polygon ramp is a global bias bump, not a tie-break. At 2e-7 it reaches 33.20%; at 2e-6 it
reaches **0.37%** — by floating the coarse sheet in front of the detailed night city, i.e. the
known-wrong surface wins (`.scratch/cmp_c5_after.png`; `docs/verification.md` rule 4). The 21.92%
was a ramp too small to win the depth test, read as a noise floor. This also corrects rule 7's
"most of the 35.77% is grazing-angle mipmap/aniso resampling": a control that changes only depth
took it to 0.37%, so ≤0.4% was resampling and ~35.6% really was depth flipping. New rule 10.

**What the session actually found — the depth-resolution floor, measured for the first time.**
The ramp brackets it: **≈1e-6 of view distance** is the smallest bias that separates two coplanar
surfaces at this view. Against the constants in `SceneBuilder`: `DepthBiasPerLevel` 2e-4 is far
above it, `SurfaceRankBias` 2e-6 sits at it, and **`NodeOrderBias` 5e-8 is twenty times below
it** — so the cross-node draw-order tie-break the architecture doc describes has been inoperative
for any node pair closer than ~40 indices since it was written. New rule 11.

**The real mechanism for all three poses is cross-node.** Nine *different* World-child nodes stack
coplanar `cblock*` ground at y=5 in the C5 repro footprint (1777 `g4683`, 1799, 1800, 1801, 1813,
1814, 1822, 1823, 1837). The priority −10 members separate cleanly (10 × 2e-4); the priority-0
members sit 36–60 indices apart (1.8–3.0e-6, straddling the floor) and 1822/1823 sit 1 index apart
(5e-8, hopeless) — which is why the flicker is partial rather than total. Confirming control:
`NodeOrderBias` 5e-8 → 2e-6, which changes nothing but the cross-node term, takes **C1B from
30.95% to 2.35%**. It is a diagnosis and **not a landable fix** — the same constant takes C5 to
**41.69%, worse**, because the node-index span is thousands and any step that beats the floor
covers tens of priority levels and scrambles the authored layering.

**C3's recorded pose does not reproduce a flicker at all** — 0.41%, beach continuously visible,
palms standing on sand. Either the shoreline fault is a *static* wrong-winner (which a
jitter-flip metric cannot see) or the recorded pose is not where the user saw it. Flagged in
`backlog.md` as needing a fresh capture rather than another diagnosis session.

**Next step if this is picked up again:** make the cross-node bias **dense over the nodes that
actually conflict** rather than uniform over all of them — rank coplanar-overlapping node groups
against each other and spend the available range on them, with the floor above as the budget.
That is a scene-graph analysis, not a constant. Do not re-try a within-surface tie-break, and do
not raise a global constant.

## 2026-07-22 — Polish-3 item 6: the 3D city-block clutter path — C2 and C5 finally have buildings

**The plan's premise held in full**, which after this plan's track record is worth stating
plainly: C2 and C5 really were rendering painted city-block ground with nothing standing on it,
and the cause really was the clutter pipeline being billboard-only. C5 gains **79,306** 3D
decorations and C2 **10,261**, all from templates whose non-sprite decorations were previously
counted, logged and dropped ("skipped N non-sprite decoration(s)", now gone).

### What landed

- `SceneBuilder.SharedMesh(meshIndex)` publishes the cached built `ArrayMesh` with this
  builder's materials. It returns the *mesh*, not a node, because everything `BuildSubtree`
  wraps around a mesh (transform, `node_bias`, collider) is per-placement.
- `WorldBuilder.Scene` exposes the world's `SceneBuilder`, handed to `ClutterBuilder`.
- `Clutter.Kind` gained `Solid`, `NodeIndex` and a `Transform3D` `CellPlacements` list
  (was an XZ `Vector2`). Solid kinds render as one `MultiMeshInstance3D` over the world's own
  mesh + materials — fullbright, fogged, depth-biased identically to the placed world, and
  structurally incapable of billboarding, since the world shader has no camera-facing term.
- Collision (user decision: buildings keep it, sprites do not): one merged static trimesh per
  1024 m region, named `clutter_bld_<cx>_<cz>` — deliberately NOT ending in `clutter_col`,
  which is `FlightController`'s *soft*-tree branch. 79,306 bodies would have been the naive
  alternative.
- `KindExport` switched from `Positions` to `Placements` (`Transform3D`), which
  `MapEdgeExtender` consumes: a sprite still mirrors as its position alone, a building
  mirrors as its whole transform.

### Two plan claims corrected

- **The item's scope is not `cblock*`.** C5's seven `cblock*` templates are only half of it;
  C2's gain comes entirely from `filmblock1-5`, `resblock1-6` and `parklot1-2` (studio sound
  stages, houses, parked Studebakers), which the plan never names. Anything keyed on the
  `cblock` prefix would have fixed C5 and left C2 exactly as reported-broken.
- **"A building needs its real orientation" is true in principle and inert in this data.**
  Surveyed every 3D decoration of every C2/C5 template: the deco→mesh chain is exactly two
  nodes deep in 249 of 249 cases, no mesh node carries a transform at all, and **every
  authored basis is identity to within 0.108°** (the single matrix-stored node is a 0.03°
  rotation). A city block varies because its template names 17–28 *different* models, not
  because it rotates them. The full basis is kept anyway — it is the right thing to consume —
  but **the upright, correct-looking C5 render is not evidence that it is**: dropping the
  basis entirely would look identical. This is exactly the trap the coordinator flagged, and
  the data settled it where a screenshot could not.

### Also found

**`cbNNa` and `cbNNdet01` are co-located halves of one building, not a LOD pair.** Every `det`
decoration in `cblock1` sits at the *exact* same local origin as its `a` sibling, with a
different footprint and a different texture family (`roof01`/`bldg*` vs `genbldg3`). Both draw.
They are 40% of the collision triangles, so this will look like an obvious saving to someone
chasing the load cost — recorded in `backlog.md` with why it was not taken.

### Verification

- **8-chapter regression, zero errors, counts explained.** Sprite counts byte-identical in all
  eight (C1 9303, C1B 60, C2 37167, C3 371, C4 88630, C5 139388); solids appear in exactly two
  (C2 10261, C5 79306); node and mesh-instance counts unchanged everywhere. **C2B gains
  nothing** — it registers `resblock2`/`filmblock1`/`filmblock2` in `adjust.gw` but its gamez
  ships none of those template roots, which is retail data and was already true.
- **Same-build noise floor measured first** (rule 2). Per chapter: C1 0.00%, C1B 0.00%,
  C1C **35.04%**, C2 0.00%, C2B 1.37%, C3 0.00%, C4 **5.86%**, C5 0.00%. Against those floors
  the before/after diffs are C5 **65.59%** and C2 **11.76%** (real), and C1C 43.25% / C2B 1.47%
  / C4 4.61% — all at or *below* their own floors, i.e. precipitation noise, in three chapters
  that gain no clutter. Without the floor, C1C's 43% would have read as a serious regression.
- **C1's residual 54–60 px was chased down rather than waved off.** It is reproducible between
  builds (not run-to-run: three same-build runs agree within 6 px), which is the signature of a
  real change — so the differing pixels were localised. They are two clusters, both on **moving
  road vehicles** (the orange car bottom-left and a blue car on the road), i.e. animation phase.
  A clutter change cannot move a Studebaker, and C1's clutter is 9303 sprites before and after.
- **Non-spinning: verified by construction and by picture.** Two C5 views 90° apart on the same
  block show entirely different faces, silhouettes and occlusion.
- **Collision verified live, both directions.** A scripted C5 dive logs
  `CRASH into clutter/clutter_bld_-10_-4 (wing)` — a hard crash, and the body name is itself
  proof the collider is the new one. The same scripted dive in C1 hits `someroads/col` and the
  forest stays pass-through: C1 builds no clutter collider at all, so item 5's behaviour
  survives untouched.
- **Grounding and phase.** Street-level C5 shows buildings meeting the pavement with no float or
  sink; the far shot shows lamp-post *sprites* lining the streets while *buildings* fill the
  blocks — the already-validated sprite path and the new solid path agreeing on one grid, which
  is a stronger phase check than either alone.
- **Mode battery clean:** freecam, fly, 2P splitscreen stunt race, viewer + damage lab, and the
  map-edge extension (which now continues the city past the map edge). Full-stderr sweep on C5
  shows no shader or `instance_uniforms` warning.

### Cost — reported, not hidden

| | before | after |
|---|---|---|
| C5 `--freecam` load | 2094 ms | 2188 ms |
| **C5 `--fly` load** | **3895 ms** | **7393 ms** |
| C5 draw calls | ~1190 | ~1348 |
| C5 GPU time | 0.35–1.15 ms | 2.3–3.5 ms |
| C5 collision triangles | 0 | 2,554,455 |

Frame rate stays pinned at the 60 fps vsync cap throughout, so those frame numbers are floors,
not ceilings (`docs/verification.md` §2). The one real price is **C5's flight load, +3.5 s**,
entirely the region-trimesh build; `backlog.md` records the two ways to bring it down.

## 2026-07-22 — Clutter city-block collision: shared shapes instead of expanded trimeshes (C5 flight load −3.5 s), and the map-edge city becomes solid

Backlog follow-up to polish-3 item 6, raised by the user the moment that item landed. The
feature was correct; the price was its load cost. **C5 `--fly` goes 7,104 ms → 3,552 ms** (4-run
means) — the entire +3.5 s item 6 charged is gone, landing *below* the 3,895 ms pre-item-6
number. C2 goes 2,637 → 2,381 ms. Nothing about the rendering, the geometry or the collision
surface changes.

### What it was, and why the old comment's reasoning did not survive

`ClutterBuilder.BuildSolidCollision` transformed every vertex of every placement into world
space and merged the result into one `ConcavePolygonShape3D` per 1024 m region: **2,554,455
collision triangles in C5 built from ~2,300 distinct ones**, ~1,100× redundancy. Its comment
justified this by rejecting one `StaticBody3D` per building ("80k bodies would swamp the
broadphase and the scene tree"), which is true, and one whole-map trimesh, also true. But body
count and shape count are different things: a `Shape3D` is shareable, and
`PhysicsServer3D.BodyAddShape(bodyRid, shapeRid, transform)` attaches one to a body with its own
transform and **no scene-tree node per instance**. Neither count the comment worried about moves.

Now: one shape per distinct decoration `MeshIndex` (C5 **57**, C2 **66** — keyed by mesh, which
over-counts, since C5 ships the same model as up to four gamez meshes, one per template that uses
it; 32 distinct models), attached once per placement. The per-region body split is kept for
broadphase locality and because `clutter_bld_<cx>_<cz>` is what locates a crash in the log.

### ⚠ The cost was in the shape build, not the loop everyone looks at

This is the transferable finding, and it is now `docs/verification.md` §2. Timing the two halves
separately, C5 2026-07-22:

| | ms |
|---|---|
| transform 2.55M vertices into world space | **271** |
| `ConcavePolygonShape3D` BVH build, 207 regions | **3,403** |
| total `BuildSolidCollision` | 3,796 |

**90% of it was inside an engine property setter, not in our loop.** A fix aimed at the vertex
arithmetic — SIMD, parallelism, fewer allocations, all the obvious moves — would have bought ~7%.
Sharing the shapes removes essentially all of it, because the BVHs now being built are ~40
triangles each.

### Convex/box shapes: considered, rejected on the data

The backlog's lever 2 (a `BoxShape3D` or convex hull per model, "a city block is box-like at
aircraft scale") was measured rather than assumed, and **the data says no**. The `cbNNdet01`
decorations enclose 0.003–0.05 of their bounding box — they are open detail shells, not solids —
so a hull or box of one would be a phantom solid up to 62 m tall where the data intends a facade.
And with the BVH build gone there is no measurable narrowphase cost left to pay for it (see
below). The collision surface therefore stays exactly what it was: same triangles, still concave,
still `BackfaceCollision`.

### Map-edge extension buildings are now collidable

Item 6 deliberately left them out: a continuation cell is built on the frame the camera crosses a
1024 m boundary, and rebuilding a merged region trimesh there would hitch. **That blocker died
with the rework.** `MapEdgeExtender.AddCellClutter` now creates one lazy `clutter_bld_ext` body
per cell and attaches the kind's shared shape at each mirrored placement — measured **0.5–0.6 ms**
for the ~3,900 buildings of an 11-cell crossing rebuild, inside a rebuild that already cost
~4.1 ms; the initial 66-cell window adds 3.5 ms to 32 ms. Verified live:
`CRASH into ext_16_7/clutter_bld_ext (fuselage) impact=(88,15,-8369)` — outside the map's `x <= 0`
boundary, which also proves the det-−1 mirrored shape transforms work. Extension *sprites* stay
pass-through, as everywhere.

### `--perf` gained a `physics` term

`frame`/`fps` sit pinned at the 60 fps vsync cap in nearly every run here, so they are floors and
**structurally cannot** show a collision change getting cheaper or dearer. `TIME_PHYSICS_PROCESS`
can. Added to the `--perf` line for that reason, and the rule recorded in `docs/verification.md`
§2: when a change lands in a subsystem the frame time cannot see, find the monitor that watches
that subsystem before concluding anything.

### Verification

- **Load, 4 runs each, both directions.** C5 `--fly` before **7,276 / 7,092 / 7,090 / 6,957 ms**
  (mean 7,104), after **3,636 / 3,573 / 3,596 / 3,402 ms** (mean 3,552). C2 before
  **2,711 / 2,608 / 2,620 / 2,610** (mean 2,637), after **2,425 / 2,368 / 2,367 / 2,365**
  (mean 2,381). Spread is ±2–5% in both builds and the gap is ~2× — far outside it.
- **Physics tick: no resolvable change, stated plainly.** A rooftop pass across the C5 city with
  `--perf` gives steady-state **2.32–2.46 ms before, 2.37–2.52 ms after** — indistinguishable.
  The narrowphase neither gained nor lost anything measurable, which is the honest answer: the
  win is entirely load-time. Frame time is 16.67 ms (the cap) in both, i.e. uninformative.
- **The scripted C5 dive still crashes into a named building body, identically.**
  `CRASH into clutter/clutter_bld_-10_-4 (wing) impact=(-9276,78,-3173) spd=55 m/s` — the same
  body, the same impact point to the metre, the same speed as the pre-rework build. Since the
  shape kind did not change, buildings cannot have become more permeable, and this exact match
  is the direct evidence.
- **⚠ A missing CRASH line nearly read as a broken collider.** Four runs after the change logged
  no crash at all, which looks exactly like "collision stopped working" — it was the default
  `--frames` budget, and the *baseline* dropped it in 1 of 5 runs too. Raising `--frames` gives
  3/3 crashes. **The scripted C5 dive's crash is not frame-deterministic near the default budget;
  do not read its absence as a regression without re-running longer.**
- **Rendering provably untouched.** Deterministic C5 `--viewer` shot, same-build noise floor
  **4 px**, before-vs-after **2 px and 5 px** — i.e. at the floor. (md5 is useless here: the two
  same-build captures already differ.)
- **8-chapter regression, zero errors.** Sprite counts identical everywhere (C1 9303, C1B 60,
  C2 37167, C3 371, C4 88630, C5 139388); solids in exactly two, C2 10261 and C5 79306; node,
  mesh-instance and world-collider counts unchanged (C1 2412, C1B 1432, C1C 1733, C2 1848,
  C2B 1335, C3 2318, C4 2545, C5 4253). Clutter collision now reports **shapes / distinct
  triangles / attachments** instead of an expanded triangle total: C5 57 / 2,240 / 79,306 (was
  2,554,455 tris), C2 66 / 1,439 / 10,261 (was 202,303).
- **Sprites stay pass-through; C1 stays non-solid.** C1 builds no clutter collider at all (no
  solid kinds => `BuildSolidCollision` is never called), and scripted C1 dives crash into world
  geometry — `CRASH into g28031/col (fuselage)` at three different impact points — having flown
  through the forest to get there. *Honest gap:* the item-6 note cites a C1 dive hitting
  `someroads/col`; three dive profiles here all landed on terrain instead, so the world-collider
  half is reproduced but not on that exact surface.
- **Mode battery clean, zero errors, no shader or `instance_uniforms` warning:** C5
  viewer / freecam / stunt / 2P race / 4P splitscreen, C2 fly + viewer, the launch menu, and the
  bare plane viewer. `--viewer` correctly builds no clutter collision at all.

### What this leaves

The `cbNNdet01` question in `backlog.md` — "40% of the collision triangles are det meshes, drop
them?" — is now moot and was deleted: at 2,240 distinct triangles total there is nothing to save,
and the name heuristic never had data behind it.

---

## 2026-07-22 — `--no-fog`, an inspection aid

User asked whether a fog-disable flag existed. It did not: `--sky-zone=` changes *which* fog you
get, never whether you get it, and the only writer of the `csky_fog_on` uniform
(`WorldBuilder.DisableFog`) had been deleted as dead code earlier the same day.

**Implemented through the global `csky_fog_range`, not the instance uniform.** `csky_fog_on` is
still live and honoured in both shaders, so writing it would have worked — but it is declared at
**index 1 in `SceneBuilder`'s bias shader and index 0 in `Clutter`'s**, and Godot merges the
instance-uniform mapping per `GeometryInstance3D`. That mismatch is currently latent precisely
*because nothing writes it*; adding the first writer would have made it live, which is the exact
mechanism behind the 2026-07-17 unfogged-hilltops bug. Polish-3 item 5 had already tried padding
the indices, measured that it enforced nothing, and reverted — so the hazard was known and
documented, and this change routes around it rather than re-opening it.

`csky_fog_range` is a **global** uniform every fogged shader reads, so a single write covers the
world, the clutter sprites, the solid city blocks and the skydome with no ordering hazard at all.
`--no-fog` sets it to the same no-op 1e8/1e9 range `Weather.NoFog` already used, and separately
zeroes the cloud-band whiteout overlay (otherwise flying into the cloud band still whites the pane
out, which reads as "fog is not actually off").

**Deliberately does NOT touch `WorldLight` or `FOG_COLOR`.** `Weather.NoFog` also carries
`WorldLight = 1` (fullbright), and reusing it wholesale would have been the obvious shortcut — but
the flag exists to help answer a *shading* question (the C3 coast's dark polygon-edge outlines and
uneven brightness, reported the same day), and changing world brightness while removing fog would
confound exactly the measurement it is meant to enable.

**Verified.** 8-chapter regression, zero errors both with and without the flag. Effect measured at
each chapter's default spawn: C2B 81.3%, C1 61.8%, C1C 45.3%, C3 42.6%, C1B 36.0%, C4 34.6%,
C2 12.5%, C5 0.7% of sampled pixels — so it fires everywhere, not just where it was developed.
C5 is low because its `zone1` fogs at 1500–2250 m and the spawn sits inside the city; the flag's
clearest demonstration is `--chapter=C5 --sky-zone=zone3`, where the default render is black
beyond ~250 m and the whole city returns with the flag on. Inert when absent by construction: the
ternary's false branch is the original expression verbatim.

**Side finding for the pending user A/B:** that C5 `zone3` capture is itself evidence about which
zone C5 actually flies. `zone3`'s 50–250 m fog and 300 m clip hide the entire city from a
rooftop-level camera, which is hard to reconcile with the user's own
`OriginalScreenshots/C5 IA1 Terrain2.png` horizon view across the city. Not conclusive — a still
cannot settle it and the poses are not matched — but it points the same way as the node counts.

**Note:** the `CLAUDE.md` day-to-day flag table was NOT updated in this turn, because that file
holds uncommitted work from a parallel session that owns it. The one-line entry is owed once that
session lands.

## 2026-07-22 — Hairline seams on C3's coast and C4's river: a texture-wrap bug, fixed per surface

**Symptom (user-reported, both chapters).** One-pixel bright *or* dark hairlines on the terrain,
laid out on a regular grid, visible with `--no-fog` at
`--chapter=C3 --campos=-3614.727,98.809,-4639.714 --lookat=-3658.603,8.952,-4640.51` and
`--chapter=C4 --campos=-6439.027,807.788,-7376.811 --lookat=-6399.279,723.584,-7413.278`. The
decisive detail was a **tan** hairline crossing **blue** water: a shading seam on flat water can
only shift the water's own shade.

**Two mechanisms had already been proposed and disproven** (`MapEdgeExtender` mirroring — killed
by the C4 pose being mid-map; the depth bias being a vertex scale — killed by the user zeroing the
bias and commenting out `VERTEX *= 1.0 - (depth_bias + node_bias)` with the seams still present).
The five leads left in the backlog were all wrong in detail, including the leading one: it is a
texture-sampling artifact, but at a **mirrored UV fold**, not at a tile crossing.

**Mechanism.** The terrain's UVs are a **mirrored triangle wave** — U rises to exactly 1.0 and
folds back instead of wrapping to 0 (measured on one C4 surface of constant ID: `fract(U)`
0.8745 → 0.9961 → **1.0000** → 0.9961 → 0.9020, smooth, no jump). That is how the artists tiled
non-seamless textures seamlessly, and it is the mirror symmetry visible across C4's river. Under
`repeat_enable` the bilinear filter's second tap *at the fold* wraps to texel 0 — the opposite
edge of the texture — over a band one texel wide. `river3.tif` is 64×128 with column 0 tan
(107,101,66) and column 63 blue-green (66,81,82); that is the tan hairline. Whether a given seam
reads bright or dark is just which way that texture's two edges differ (C4 +24 R, C3 −30 R).

**Fix — per surface, never global (`SceneBuilder.UvsWithinUnitSquare`).** Each (material,
priority, sidedness) group is asked whether all its UVs lie in [0,1]; if so, and it does not
scroll, it takes a `repeat_disable` shader variant (`clampUv`, shader-key bit 64, added to the
`_materialCache` key because two surfaces on one material can disagree). Safe **by construction**:
with no UV outside [0,1] the wrap is unreachable, so CLAMP and REPEAT can differ only within half
a texel of the edge. Clamped surface counts are logged on the world-load line.

**Verification.**

- Noise floor for the C3 pose measured at **0 px** (same build twice).
- Flat-colour probe (geometry, depth, alpha, vertex colours untouched): C4 water box **4,443 → 0**
  seam px; the same detector still fired **1,818 px** on a real surface boundary, so it was not a
  dead instrument. A per-surface unique-ID render with the material cache bypassed showed that box
  is **100.000%** one surface — no crack, no sliver, nothing behind it.
- Hairlines lie on integer-UV lines: **81.7%** of UV-boundary px carry one in C4 and **100.0%** in
  C3, against 2.4% / 2.3% chance; only **6.5%** lie near a polygon boundary (1.8% chance).
- The seam peaks at exactly the 50/50 blend of the two edge columns: predicted **(86.5, 91.0, 74.0)**,
  measured **(89.5, 92.5, 77.2)**, background (66.0, 86.0, 80.3) ≈ column 63.
- After the fix: C3 sand box **1,322 → 0** seam px; the C4 profile across the tan hairline goes
  from a peak of 89.5 to **flat 66.0**, neighbours unchanged. Changed pixels are **96.3%** (C3) and
  **99.5%** (C4) on UV boundaries — the fix moves the artifact and essentially nothing else.
- **C5 city pose changes only 447 px (0.05%)**, which is the whole point of the per-surface guard:
  a blanket `repeat_disable` changes **738,062 px (80.08%)** there, mean delta 47.
- 8-chapter regression: zero errors; clamped counts 1481/600/782/1132/734/1011/1745/1371 (C1…C5),
  below the static in-range upper bound as expected since only built, non-LOD-culled, non-billboard,
  non-scrolling surfaces reach the test.
- Full mode battery (fly / stunt / viewer / damage / 4P race / menu / freecam): zero errors.
- Aircraft are **not** byte-identical and should not be — the same bug affects skins mapped 0..1.
  The static Bloodhawk viewer changes **802 px (0.087%)**, mean delta 12.7, confined to panel and
  gun-barrel edges; 23 surfaces clamped.

**Fidelity — closed against us.** The user supplied `OriginalScreenshots/C3 IA1 Beach, no seams.png`
(dgVoodoo, same beach). Same detector, original downscaled 2× to our pixel scale, clean sand box in
both: **ours 1,322 high-pass px with a 240 px longest connected run; the original 16 px with a 2 px
run.** The minor seams that *are* visible in the original are the other mechanism — a content step
where a non-repeatable texture genuinely tiles — and the black stripes are trees seen from above.
The two are distinguishable by profile shape, and ours was measured to be the fold kind: identical
background on **both** sides with a spike between (C3 235.5 → 205.8 → 235.3; C4 66.0 → 89.5 → 65.3),
which a texture discontinuity cannot produce because its two sides must differ.

**Two instrument traps recorded** (`verification.md` rules 12 and 13): deriving texel scale from a
`fract(UV)` capture was 11× out because the finite difference spanned ~1.4 quantisation steps of an
8-bit channel — a per-texel *ramp*, where the answer is a period rather than a level, is the
instrument that works; and a flat-colour ID map cannot see a crack between two surfaces that share
a texture, which needed a second probe keyed per surface.

---

## 2026-07-22 — C5's weather zone is settled: `zone1` (user A/B); polish-3 archived

**The last user-gated item of `docs/PLAN-M2-polish-3.md` is answered**, so the plan moved to
`docs/plans/PLAN-M2-polish-3.md` with a `COMPLETE` banner.

**The answer: C5 = `zone1`.** The user flew C5/IA1 in the original and can see across the city.
`ZONE3` fogs at **50–250 m** with a **300 m** clip, which would collapse the view into a 300 m
bubble — so `zone3` is ruled out by direct observation, not by inference.

**No behaviour change was needed, and that is the point.** The remake already renders `zone1`
there: the `zone2` default matches nothing in C5 and `WeatherState.ResolveZone` falls back to the
file's first zone. What changed is the *record* — until now the code and docs said the C5 choice
was an unresolved guess awaiting an A/B, which invited someone to "fix" a fallback that was
already right.

**Verified the fallback is stable, not lucky.** Surveyed all 53 `weather.zrd.json` in the install:
every one of C5's 8 missions lists `ZONE1` before `ZONE3`, so all 8 resolve to `zone1` — there is
no C5 mission where the fallback picks differently. Confirmed at runtime:
`weather: C5/IA1 has no 'zone2' (zones: zone1/zone3) — rendering 'zone1'`, fog 1500–2250,
world light 1.00.

**A latent hazard found and written down while checking this.** C5's weather.json lists `ZONE1`
first but its **gamez `horizon` subtree lists `zone3` first**. Two independent "first zone"
fallbacks over two different orderings could render the sky of one zone with the fog of another.
They do not today, because `PlaneViewer.LoadWeather` (`:1638`) resolves against weather.json and
passes the result into `BuildHorizon` (`:709`) — `BuildHorizon`'s own fallback is a no-op on that
path and exists only for a mission with no weather.json. Recorded as a ⚠ in
`docs/formats/weather.md` so the two are not "simplified" into disagreeing.

**Also changed:** the fallback log line said `defines no 'zone2' … using 'zone1'`, which reads as
a missing-data warning on every single C5 flight. It now reads `has no 'zone2' … rendering
'zone1'` with a comment stating this is expected and confirmed-correct for C5.

**Still open — C1–C4.** All four define `zone2` and resolve to themselves, so they render a
plausible answer either way and this is a fidelity question rather than a bug. **C1 is worth
doing first:** it is the only chapter whose own scripts disagree (`load.gw` names
`zone2_cloud_floor`, `tex_fx.gw` names `h_zone1scroll`) and its two zones are genuinely different
skies — zone2 moon/stars night vs zone1 day haze.

**Not licensed by this answer:** hiding C5's 149 `zone3` *nodes*. The `zone_id` backlog entry now
says so explicitly — a fog answer does not settle a geometry question, and a wrong guess there
deletes visible world content, which is strictly worse than drawing both.

Touched `CSVM/src/Flight/Weather.cs`, `CSVM/src/PlaneViewer.cs`, `docs/formats/weather.md`,
`backlog.md`, `CLAUDE.md`, and moved the plan.

---

## 2026-07-22 — C1's police siren sounds: the sound loader dies before most SOUND_NODEs are reached

**Fixed, but not where the backlog said.** `snd_police` now plays and rides the police car along
its route. The recorded diagnosis — "named sibling sequences are never dispatched" — was refuted.

**The refutation.** The gate on a sequence is `seq_state`, not whether it has a name
(`CompiledAnim.cs:239` sets `OnCallOnly` from `seq_state == "OnCall"`; `AnimRuntime.cs:288` runs
`Where(s => !s.OnCallOnly)`). A *named* `Initial` sequence therefore runs at activation exactly
like an unnamed one — which is how the car drives its route from a sequence called `start_walkin`.
And the call does arrive: `police_car-police_chase.json` seq[2] event[1] is
`CallSequence{siren_police}`, one of **1,129** CallSequence dispatches in a single C1 run. The
count the hypothesis predicted came out **0**: across 16,053 archive JSONs there are 1,030
named-but-never-dispatched sequences (2,503 events), and **not one of them is a `SoundNode`**.

**The real cause is lifetime, not dispatch.** `WorldSounds.Loader` is valid only during the world
build; `PlaneViewer.cs:674` nulls it when the `SoundArchive` zip scope closes, after which
`Create` can serve only names already in `_streams`. The siren's `SoundNode` fires **one frame
late** — `CallSequence` appends to `AnimInstance.Runners` (`:998`) while `Advance` walks that list
descending (`:1485`), so the appended runner sits at an index the loop has already passed. One
frame later the loader is gone. Probed directly on the unmodified build:
`Create name=snd_police inDefs=True cached=False loaderNull=True` — the def is in sounds.json
(`siren_police1.wav`, LOOPED/3D/RANGE 200–1200) and the WAV is in `extracted/soundsh`; the stream
was simply unobtainable.

**The reported bug was the small half.** Measured install-wide: **947 of 1,244** `SOUND_NODE`
events sit in `Initial` sequences of `OnCall`-activation defs, so they are first reached at
runtime — always after the loader dies. **386** name a sound never in a cacheable position at all:
**`snd_fire1` ×363**, `snd_beeper` ×16, `snd_firetruck`, `snd_police`, `snd_train2`,
`snd_freighter`, `snd_enginelooped`. Every one would have failed silently. `snd_fire1` is the
destruction fire crackle, so **the audio half of Milestone 3 was set to fail this exact way**
before a single weapon existed.

**Fix.** `WorldSounds.Prewarm(AnimProgram.SoundNodeNames())` decodes every name the loaded program
can reach — walking `OnCall` sequences and `ResetState` too, since the question is reachability,
not what runs at bootstrap — before the archive closes. 2–4 streams per chapter; the whole install
uses 10 distinct `SOUND_NODE` names, so the bound is trivial.

**Verification.** 8-chapter regression (`--frames=200 --debug-anim --no-pads`): zero errors, zero
late-failure warnings, live emitter names unchanged everywhere except C1, which gains
`snd_police` (`sound: snd_police @ police_car pos (-6864, 128, -5935) … PLAYING`, tracking the car
between log lines). Prewarm counts C1 4, C2 3, C4 3, others 2.

**The C1 census legitimately still reads 38** while 39 emitters exist. That is correct, not a
leak: the siren's emitter is created one frame after `Bootstrap` prints.

**Which is the instrument trap worth keeping** (`docs/verification.md` §4). `anim: N ambient sound
emitter(s): …` is printed inside `Bootstrap`, so it is a *snapshot* and cannot distinguish "never
requested" from "requested later and failed". Read as a complete census it produced a clean,
specific, entirely wrong diagnosis that survived a full investigation because every check
performed agreed with it. A post-census failure now announces itself once per name at the point of
use (`AnimRuntime.ReportLateSoundFailure`) rather than inflating a counter nobody prints again —
`WorldSounds.cs:32-34` had already anticipated exactly this ("only a genuinely new name after the
build is *reported* rather than faulting on a closed zip"); the safety valve existed but was never
wired to a print site outside `Bootstrap`.

**Deliberately not fixed: the one-frame lag.** Draining same-pass-appended runners was implemented
and measured behaviour-neutral, but it is the wrong fix — it repairs the siren only because the
loader happens to still be alive at that instant, and leaves the other 947 broken. The descending
walk is deliberate (`AnimRuntime.cs:250`: instances can be added mid-walk), and any same-pass
drain would need a bound against a self-calling sequence. Recorded in `backlog.md`.

Touched `CSVM/src/Mech3/WorldSounds.cs`, `CSVM/src/Mech3/AnimProgram.cs`,
`CSVM/src/Mech3/AnimRuntime.cs`, `CSVM/src/PlaneViewer.cs`, `docs/architecture.md`,
`docs/verification.md`, `backlog.md`.

---

## 2026-07-22 — Backlog hygiene: closed entries leave, follow-ups get promoted

**Convention set by the user**, now recorded in `CLAUDE.md`: a `FIXED`/closed entry does not stay
in `backlog.md`. Its record belongs here in `HISTORY.md`, its traps in `verification.md` /
`architecture.md`. **If closing it leaves follow-up work, that follow-up becomes its own new entry
with a `⚠ Traps` section.** The failure mode being designed out: an open thread buried inside a
section headed `FIXED` is invisible to anyone scanning headings for work.

Applied to the two sections this affected:

- **`## C1: the police siren` (FIXED) — removed.** Its record is the entry above; its instrument
  trap is `verification.md` §4; its implementation detail is the `WorldSounds` bullet in
  `architecture.md`. Replaced by **`## The one-frame CallSequence dispatch lag`**, the genuine
  follow-up that was buried inside it — `CallSequence` appends to `AnimInstance.Runners` while
  `Advance` walks descending, so *every* called sequence's first event fires a frame late, not
  just the siren's. Traps recorded: the descending walk is deliberate (`AnimRuntime.cs:250`), a
  same-pass drain needs a bound against self-calling sequences, and the bootstrap emitter census
  cannot measure any of it.
- **`## C1/M04: the pirate zeppelin`** — the decision was closed (fix rejected, artifact accepted)
  but the work was not. Promoted to **`## Cutscene player — the missing consumer`**, carrying the
  evidence forward as input rather than as a record: the `piratezep.zan.json` decode (48 frames,
  uniform straight line, 15.67 s), the 13-mission `startanims` survey, and the `letterbox` /
  `CALLBACK` linkage. Four traps stated up front, including the rejected skip-at-bootstrap fix
  (kept explicitly so nobody re-derives it and thinks it is new) and "do not lower the zeppelin".

**A real mistake, caught and corrected in the same session.** Rewriting the siren entry earlier
today used a script that replaced from its heading to the *next* `##` heading — and found none,
because the entire cutscene investigation had been appended underneath the siren section without a
heading of its own. It replaced through EOF and **deleted ~76 lines of decoded evidence**, which
was then committed and pushed (`b862111`). Recovered verbatim from `bbc5f59` and restructured
above. Two lessons worth more than the incident: **a section-replacing edit must assert what it is
about to remove**, not assume a heading terminates it; and content appended without a heading is
load-bearing but structurally invisible — the promotion above gives all of it real headings.

---

## 2026-07-22 — `CleanScratch.ps1` also sweeps agent worktrees, and stops asking twice

**Two problems, one script.**

**1. The double confirmation.** `[CmdletBinding(ConfirmImpact = 'High')]` plus the script's own
`PromptForChoice` meant a sweep asked twice — once for the whole plan, then again **per item**,
because at `High` impact `$PSCmdlet.ShouldProcess` prompts on its own against the default
`$ConfirmPreference = 'High'`. A 132-file sweep therefore ended in `[J] Ja [A] Ja, alle …`.
Changed to `ConfirmImpact = 'Medium'`, which leaves `-WhatIf` and an explicit `-Confirm` working
while `ShouldProcess` stops prompting unasked. Verified: a `-Force` run now calls `ShouldProcess`
11 times with no prompt.

**2. Worktrees accumulated forever.** Subagents run in git worktrees under `.claude/worktrees/`,
which is git-ignored — so nothing ever swept them. Ten survived one plan, each a full checkout,
and they added ~30 false hits to every repo-wide grep (they did exactly that during this
session's own doc work, which is what surfaced it).

**Parsed from `git worktree list --porcelain`, not from the folder listing**, because the two
disagree in precisely the case that matters: a worktree whose directory was deleted by hand still
has git metadata and reports as `prunable`. A folder listing misses those entirely — and that was
the live state here, all 10 having been removed manually.

**Safety rules, in order:** only paths under `.claude/worktrees/` are ever considered (never the
main checkout, never the one the script runs from); a worktree with uncommitted changes is spared
and reported unless `-IncludeDirtyWorktrees`; and removal never touches branches, so committed
work always survives. Leftover branches are listed, and `-PruneBranches` deletes only the merged
ones via `git branch -d`, which refuses unmerged branches by design.

**3. Non-interactive hosts now fail closed with a usable message.** Without `-Force`,
`PromptForChoice` throws in a scheduled task / CI / agent shell. It previously surfaced as a raw
.NET exception; it now prints "Cannot prompt in a non-interactive host — nothing deleted" and
points at `-Force` / `-WhatIf`. Deleting because nobody could be asked is the wrong default for a
script whose whole job is deletion.

**Verified** by `-WhatIf` (found all 10, deleted nothing), then a real run: 10 worktree entries
cleared, `git worktree list` back to the main checkout, all 10 branches deliberately untouched.

**Follow-up the same day — `-PruneBranches` was broken on arrival.** It collected candidates only
from worktrees removed *in the same run*, so once the directories had already been cleaned up (the
common case, and exactly the user's) the list was empty and the switch **silently did nothing**.
Fixed to enumerate `git branch --merged main` independently, excluding `main`, the current branch,
and any branch held by a worktree being kept. This also surfaced that the leftovers were **20
branches, not 10** — a `worktree-agent-*` set exists alongside the `polish3-item*` ones, and the
original implementation could never have seen it because those were not the branches its worktrees
reported. All 20 deleted; `git branch --list` is back to `main` alone, and a re-run is a clean
`Nothing to delete`.

The general lesson: **a cleanup switch scoped to "things this run just touched" no-ops precisely
when the mess is oldest.** `-Force` made it worse by suppressing the prompt that would have shown
an empty plan.

---

## 2026-07-22 — Backlog hygiene: the polish-3 landings and the disproven entries leave

**A sweep, not a change** — no source file was touched. Polish run 3 completed earlier the same
day and `backlog.md` still carried the entries it had closed, which is exactly the failure mode
`CLAUDE.md`'s "delete when landed" rule exists to prevent, and which had already cost two agent
sessions once (see the C3 record below). Ten entries removed, four corrected, three re-homed.

**Removed as landed** — each has a dated entry above; the backlog is not a second copy of it:

| Entry | Closed by |
|---|---|
| Animated world vehicles | `PLAN-anim-playback.md`, 2026-07-21 (struck through since, never deleted) |
| mech3ax upstream PR — the 72-byte `planes.zbd` diff | revival-plan item 12 fixed it 2026-07-21; item 14 resolved 2026-07-22 (CS stays in the fork, PR closed) |
| Degenerate zeppelin node transforms | polish-3 item 9 — the unread `spline_interp` flag |
| C4's cloud deck does not follow the plane | polish-3 item 4 — structural deck detection |
| Tail collision boxes swallow the outboard wings | polish-3 item 10 |
| C1 police / mafia / traffic cars stop after one route | polish-3 item 8 — `LOOP_COUNT 0` means infinite |
| A generic way to find billboard sprites | polish-3 item 5 — `SceneBuilder.ClassifyBillboard` |
| Billboards should generally have no collision | polish-3 item 5 |

**Removed as disproven.** The **C5 `csky_fog_on` shader warning** entry described a warning that
**does not exist** — polish-3 item 5 looked for it across an 8-chapter run and logged none, because
the two shader families never share a `GeometryInstance3D`. The real, *latent* index disagreement
survives as a residual entry under "Feature backlog", which is where it belongs; the duplicate
claiming a live C5 warning is gone. Its side note ("`WorldBuilder.DisableFog` is dead code") was
stale too — that method was deleted the same day.

**Removed as not-a-bug.** *"World renders into only the upper-left quadrant at the world origin"* —
exact projective geometry; the full reasoning was already recorded in the polish-3 planning entry
above (2026-07-22, "Selection turned up four stale backlog entries", item 1) and is not repeated
here. Only its transferable half was missing, and is now `verification.md` rule 15: an artifact
landing on exact half-viewport boundaries is usually the camera pose, not the renderer.

**The C3 coast z-fight record, preserved here because the backlog was its only home.** Reported
and fixed the same day by `6c592c2` (per-mission entity setup), bisected at the user's own pose
(`--campos=-5931.403,149.271,-3292.775 --lookat=-5933.68,66.417,-3348.722`, `--shots=5
--jitter=0.006`):

| commit | C3 coast flicker |
|---|---|
| `10f48f8` (parent — before per-mission setup) | **4.87%** |
| `6c592c2` "World: per-mission entity setup — the interp boot script, not a roster" | **0.09%** |
| `b83252c` (then-current main) | **0.09%** |

A **54× reduction**, and nothing since moved it. Mechanism: `MissionSetup` runs the mission `.gw`
script's `NodeSetActive`/`DeleteTree`, so duplicate overlapping entities stop being drawn — remove
the duplicates, remove the depth fight. **The lesson outlives the bug and is now
`verification.md` rule 14:** the entry carried "user-confirmed as real z-fighting" while the fix
landed the same day, so polish-3 items 3 and 11 both chased an already-fixed symptom, and item 11's
honest "0.41%, barely flickers" reading was the fix showing through rather than a mis-aimed camera.
*When a report and a fix share a date, `git log` the interval before writing code* — and when you
fix something incidentally, go back and close the entry it fixed.

**Closed by measurement, not by inference: "C3 trees standing in the water."** Its recorded cause
was the beach z-fight above, and a polish-3 note even said "the palms stand on sand" — but that was
a *flicker* reading, and this symptom is a **static wrong-winner a jitter metric cannot see**
(the entry itself said so). So it was checked directly rather than inferred. Rendered at both
recorded poses plus 8 more across five separate beaches, including a waterline eye-height view
looking inland from 22 m out over the water — the exact angle at which a losing sand band reads as
trees in the sea. **Every view shows the same intact stack: water → surf → wide sand → palms →
jungle**, with the palms inland on fully-drawn sand casting shadows onto it
(`.scratch/c3_waterline_eye_E.png`).

The flicker numbers there looked alarming at first (6.7% at a low oblique, 15.3% at the waterline)
and are the useful part of the exercise: **a `--jitter=0` control kept 6.4% and 14.4% of it**, at
an amplitude of ≤12/255. That is the C3 water flipbook, not depth — a genuine sand↔water flip is a
delta of ~100+ (sand ≈ 215,190,150 vs water ≈ 45,95,105). `verification.md` §7's C3 row now carries
that measurement, because "~35,250 px at max delta 3" badly understates what open water does to a
jitter burst. Note the coplanarity itself is **unchanged and intrinsic** — 86 of the 102
`cliff1_sandtrans` polygons still sit at exactly Y = 0.0, on the sea plane. What `6c592c2` removed
was the *duplicate* entities that made the tie ambiguous; if this ever recurs, that is the place to
look, not the clutter placement. The trap that entry existed to prevent — "do not fix this by
dropping submerged palms", which would delete correct content — already lives on as
`verification.md` rule 8.

**Corrected rather than removed.** Four entries had stale cross-references into completed plans:
`OBJECT_OPACITY_STATE` described as "scheduled work" (it landed 2026-07-22); partition visibility
citing "the polish-run-3 fix" as a workaround (that fix was measured to change nothing and no cheap
substitute is known); the C5 z-fight open question described as "scheduled as item 3" (item 3 closed
as disproven); and the C1/M04 ambient-audio playtest telling the listener that a
`world pose is degenerate` line was **expected** — it is now a regression, since item 9's fix has
`snd_zepengine` playing at a finite pose. That last one is the sharpest argument for this sweep:
a stale backlog does not merely waste time, it tells a tester to ignore a real fault.

**Re-homed, not deleted.** The `## Milestone 2 polish run 3 — candidate scope` section is gone —
a heading named after a completed plan is stale scaffolding — and its three surviving items moved
to sections that describe them: the C1–C4 weather-zone question and "fine-tune fog and environment"
to "Open fidelity questions", "better mission states" to "Feature backlog". Also refreshed: the
file header still said **"No plan is active"** (`PLAN-M3-weapons.md` is), the "Open bugs" preamble
still asserted that every entry under it had been checked and none were fixed, and the C1 ambient-
audio listening notes predated the police-siren fix, so they named two emitters where three now
sound.

`backlog.md` 65 KB → 53 KB. No code changed, so there is nothing to regression-test; the
verification that matters is that every deleted entry's record exists above and every surviving
cross-reference resolves.

## 2026-07-22 — Milestone 2/2.5 polish run 4 planned; the backlog's open bugs are nearly all scheduled

Ten items selected from `backlog.md` into **`docs/PLAN-M2-polish-4.md`** against three criteria the
user set — **feasibility, little or no user input, and a preference for long-running work** — scoped
to **M2/M2.5 only**, with `docs/PLAN-M3-weapons.md` queued behind it. Everything needing a playtest,
two controllers, or an original-game A/B was deliberately excluded and stays in `backlog.md`.

**Every candidate was verified still open against the code, not just against the backlog** — four
parallel read-only agents checked each one against `docs/HISTORY.md` *and* the source. That pass is
the reason this entry exists, because three of its findings changed the plan:

1. **The bowl sign is not a bowl-sign bug — it is an engine-wide scheduler off-by-one.**
   `AnimRuntime.SequenceRunner.Advance` (`:1549-1568`) computes `_due = NextDue(ev, duration)` from
   the event it just fired, and that `_due` gates the *next* event; per `CompiledAnim.cs:255-259`,
   `"Event"` means "since the previous event fired", so the offset gates the event that **carries**
   it. Every timestamped event therefore fires one slot early and its unstamped partner one slot
   late. The compiled sign (`bowl-desert_onoff.json`) is **nine strict on/off pairs plus an infinite
   Loop**, only the first of each pair timestamped — so the shift splits every pair, and the traced
   result is both variants lit at t=0 and **fully dark for 0.5 s at t=1.0**, which is exactly the
   reported "disables and re-enables it instead". It also shifts the police chase, the C1/C2/C3
   traffic, the hangar doors and the train. Promoted from a cosmetic one-liner to plan item 1.
2. **The C1 cars are a separate root cause**, so the two were *not* merged as intended.
   `FromToMotion` (`:1468-1474`) rebuilds the basis from `_rest` whenever an event has no rotate
   channel, so every translate-only event snaps the node back to its authored rest orientation.
   `police_chase`'s `start_walkin` poses 45° and then runs a **17-second translate-only straight** —
   the longest, most visible leg — at rest orientation. The correct rule is already documented in
   the same file, in `ScriptPlayback`'s docstring (`:1296-1300`): *"an ABSENT channel means 'hold the
   last value this script wrote', not 'return to rest'"*. `ScriptPlayback` keeps persistent
   components; `FromToMotion` does not. Units and Euler order were both checked and ruled out.
3. **Two backlog claims did not survive.** `plane_destroy_sg` is listed as missing from the crash
   choreography but is **already implemented in effect** — the `SOUND_GROUPS` entry is
   `DYNAMIC_WEIGHTS 0.5` over `snd_exp_plane1..4`, and `FlightAudio.cs:53-58`/`:142` already load
   exactly those four and pick one at random. And **`.scratch/probe_exempt.py` no longer exists** —
   `CleanScratch.ps1` swept it, `.scratch/` is gitignored, so there is no copy in git either; that
   item now means rewriting the probe, which is why it was dropped as the eleventh candidate.

**Two further diagnoses fell out of the same pass**, both traced to one mechanism. C5's sunk
zeppelin is `piratezep`: **C5's `ia1.gw` is the only IA1 setup script in the install that does not
deactivate it**, its root carries `"transform": "Initial"` so `GameZ.ParseTransform` leaves `Local`
null, and its `child_bbox` hangs to y −160.6 — a hull 160 m below a root pinned at the origin. C2's
Seaplane Hangar objective has the *same* shape: `sghangar` is the **only dzone in the install that
is a geometry node rather than a `dzN` point marker** (all other chapters' 45 zones are markers), it
also carries `"transform": "Initial"`, and `StuntMission.cs:143` reads
`WorldTransformOf(node).Origin` — resolving the objective to exactly (0,0,0), ~8 km from the
building, which is why it shows a correct label on a wrong point.

**`backlog.md` 53 KB → 42 KB.** The "Open bugs" section is nearly emptied — only the crash-damage
display and the gauge-needle shape remain unscheduled. The C5/C1B z-fight section (129 lines) was
**not simply deleted**: its surviving structural evidence moved into the plan's item 9 first (the
`world1`-children/partition-roots disjointness, the nine coplanar nodes at y=5, the retracted
coverage figures, the `fvol*` and `zone_id` re-measurement cautions, the reference captures), and
what remains here is a pointer carrying the two facts worth knowing without opening the plan.

**`DzRadius` corrected: it is 15 m, user-tuned by hand, and the source was right** — the backlog's
"30 m (tightened from 60)" was stale. Recorded with it is the reason a constant is unsatisfying at
all: 15 m is too tight at some zones while 30 m was loose enough to fly *around* the danger and
still score it. The open lead is a **per-zone extent from the data** — the `dzones` record is two
strings with no size, so it would have to come off the marker node's `RotateTranslateScale` scale or
its `node_bbox`/`child_bbox`, neither of which is parsed into `GameZNode` today.

No code changed. The verification that matters is that every moved entry's evidence exists in the
plan and every cross-reference still resolves — checked, including `CLAUDE.md`'s "Known issues"
z-fight bullet, which pointed into the deleted backlog section and now points at the plan.

## 2026-07-22 — Polish-4 item 1: an event's START_TIME gates that event, not the next one

**The bowl sign was the symptom; the defect is the sequence scheduler, and it shifted every
sequence in the install by one slot.** `SequenceRunner.Advance` fired event *i*, then computed
`_due = NextDue(events[i], duration)` — and that `_due` gated event *i+1*. But
`AnimEvent.StartOffset` is documented at `CompiledAnim.cs:256-259` as `"Event"` = *since the
previous event fired*, null = *immediately after the previous event*: **the offset belongs to the
event that carries it.** So every timestamped event fired one slot early and its unstamped partner
one slot late.

**The fix.** `SetDue()` re-gates on `_seq.Events[_pc]`'s **own** offset after every `_pc` change,
measured from a new `_base` = the previous event's fire time plus its run time. Control flow does
not advance `_base` (LOOP/IF consume no time) but **is** gated, and the constructor calls
`SetDue()` so the first event's own offset applies — `_due` previously started unconditionally at 0.
That last part fixes a second, separate discard: the `Loop` branch hard-reset `_due = 0f`, throwing
away e.g. the bowl sign's trailing `Loop {Event 1.2}`, which is its entire inter-cycle pause.

**Why the sign proves it.** `bowl` (gamez 4868) carries `des_on`/`des_off` (models 631/632), both
shipped `active: true`, both with real textures (`bowlsignon03.tif` vs `bowlsign03.tif`). Its
compiled `on_off` sequence is **nine strict SWAP pairs plus an infinite Loop**, and only the first
of each pair is timestamped — so a one-slot shift splits every pair, leaving windows with neither
variant on. Traced and then measured.

**Measurement.** Face-on at `--freecam --chapter=C1 --campos=-6605,135.15,-5975.2
--lookat=-6618.7,135.15,-5975.2`, `--shots=150 --jitter=0`, crop (505,280)-(650,325), classified
three-way by luminance **standard deviation** (empty sky sd≈18, panel present sd≈48–53) then mean:

| | no panel at all | unlit | lit |
|---|---|---|---|
| before | **38.0%** | **0.0%** | 62.0% |
| after | **0.0%** | 72.0% | 28.0% |

Before the fix **`des_off` never rendered in a single frame** — the sign was lit or gone, which is
precisely the report ("the bowl sign flashes in the original, but ours disables and re-enables it
instead"). After, the blank windows map one-to-one onto the now-unlit ones and the lit windows
shift by exactly one slot.

**⚠ The instrument trap is the more transferable finding, and is now `docs/verification.md` rule 16.**
The first classifier asked "is the crop reddish?" — which cannot distinguish an **unlit panel** from
**empty sky**, because both are grey. It reported this correct fix as a regression, 38% → 72%
"blank", and the number was precise, reproducible and meaningless. Only *looking at the frames*
caught it. A metric built from the broken state does not necessarily survive the state being fixed.

**8-chapter regression.** Every structural count identical across C1/C1B/C1C/C2/C2B/C3/C4/C5 — gamez
nodes, mesh instances, colliders, uv-clamped surfaces, def counts, anchored, unresolved. What moves
is bootstrap-window activity: state ops C1 4136→4132, C4 2282→2278, C5 4192→4146, and live motions
C1 41→39, C5 43→39 — events that used to fire early now wait, so fewer have fired by the time the
snapshot prints. **Those lines are bootstrap snapshots** (`AnimRuntime.cs:223` says so), so C5's
`ObjectMotion(rotation delta)×1` dropping off the "not yet acted on" tally means that event now
fires at its authored time rather than during boot — not that it stopped firing. Zero errors in all
eight. C1 traffic re-verified as still driving its routes continuously (`mafia` −17.2 units/s,
`black_car1` +17.2, smooth across a 10 s run), since the route loops were the documented risk area.

**Found on the way, NOT fixed, now in `backlog.md`: `wait_for_completion` is decoded and read by
nothing.** It appears on **56,750 `CallAnimation` events** install-wide (53,019 null, 3,639 zero, and
92 scattered across 1–6), so it is an index rather than a boolean — the same family as the
`wait_for_raw` connector slots. Nothing in `CSVM/src` references it. This is the `spline_interp`
shape exactly: a field the extraction decodes faithfully and the runtime silently ignores. It is a
separate mechanism from this item's off-by-one and was deliberately not folded in.

## 2026-07-22 — Polish-4 item 5: the C2 stunt "Seaplane Hangar" objective sat at the world origin

**The bug.** C2's first danger zone is not a `dzN` point marker. `extracted/C2/IA1/zrdr/ia.zrd.json`
lists `dzones` as `[["dzpath1","sghangar"], ["dzpath2","dz2"], … ["dzpath9","dz9"]]` — the first
entry names **`sghangar`, the Seaplane Hangar building itself** (C2 ships no `dz1`). `StuntMission`
resolves a zone's position by looking the named node up in the chapter gamez and taking
`WorldTransformOf(node).Origin`, and `sghangar`'s `transform` is the JSON *string* `"Initial"`, which
`GameZ.ParseTransform` leaves as a null `GameZNode.Local`. Its parent chain is empty (it is a
partition-placed subtree root, `parent = -1` — the plan predicted `world1`, which was wrong and does
not change the outcome), so the accumulated transform is identity and the objective resolved to
**(0, 0, 0)**, about 8 km from the hangar. Display text is a wholly independent `targets.json`
lookup, which is why the symptom was a *correct label on a wrong point*:
`sghangar: Danger Zone [Fly Through] - Seaplane Hangar @ (0,0,0)`.

**The fix** (`StuntMission.GeometryAnchor`, one file). `Load` now takes
`GeometryAnchor(worldGamez, node) ?? worldGamez.WorldTransformOf(node).Origin`. `GeometryAnchor`
walks the named node's subtree collecting one world-space AABB per drawing node straight from
`GameZ.Meshes[...].Vertices`, and **returns null when the subtree draws nothing** — which is every
ordinary marker. That guard is what makes the change safe rather than clever: all **53** `dzN`
nodes across C1/C1B/C2/C3/C4/C5 were measured to be childless `model_index -1` nodes, so the
fallback provably cannot fire for any of them.

**No parser change was needed.** The plan predicted this item would have to add `child_bbox` /
`node_bbox` / `active_bbox` to `GameZNode`; it did not. The mesh vertices already give the same
answer, following `NodeLabels.cs:217`'s precedent ("gamez origins are frequently nowhere near the
geometry they draw"). Those three bbox fields remain **unparsed**.

**Which point on the structure — the aperture, not the middle.** Every one of these zones carries
`help_label = MSG_OBJ_FLYTHROUGH`, so the zone means the structure's opening. Where the subtree
carries ≥2 door leaves (name contains `door`, the original's node naming used as the semantic layer
exactly as `PropParts` / `ControlSurfaces` / `WingLights` / `DamageVisuals` already do) the anchor is
the union centre of *those alone*. Measured from the models: `sgh_door1` spans x[−5760.4, −5706.6]
and `sgh_door2` x[−5834.2, −5780.4], both at z ≈ −5623.9, y 8.0→38.9 — the two leaves are retracted
either side of the front wall and leave a **20 m slit** centred on **(−5770.4, 23.5, −5623.9)**.
`DzRadius` (15 m) about that point covers the whole slit in x (±10 needed) and essentially all of it
in y, so you cannot thread the doors without scoring.

**Corroborated by an independent source: `dzpath1`.** The `dzpathN` half of each `dzones` pair is
read by nothing, and was decoded on the way (see the backlog entry). `dzpath1`'s second polygon is
the hangar's **front aperture outline**, centred at (−5770.4, 23.5, −5622.0) — **1.9 m** from the
door-leaf anchor, from a completely different piece of geometry. Two independent sources agreeing to
under 2 m is what settled the choice.

**The rejected alternative** is the whole-structure centre, (−5770.4, 23.5, −5496.6) — which is also
what `child_bbox` would have given. The hangar is a **255 m through-tunnel** (both end walls are
three triangles with an aperture between them), so that point sits 129 m deep inside it: reachable,
but missable if entered off-centre from the open rear. Subtrees with **no** door pair still fall back
to that whole-geometry centre.

**Verification.**
- **The one-line diff.** All 6 stunt chapters were run before and after and their per-zone position
  log lines diffed: **exactly one line changed** — `sghangar … @ (0,0,0)` → `@ (-5770,23,-5624)` —
  plus the new anchor log line. C1 (5 zones), C1B (5), C2's other 8, C3 (4), C4 (14) and C5 (17) are
  byte-identical. The baseline was captured twice, before and after fast-forwarding the worktree onto
  item 1's `1530bd5`, and was identical across that merge.
- **Flown, not just placed.** A scripted straight-line run
  (`--stunt --chapter=C2 --spawn-at=-5770.4,23.5,-5780 --spawn-dir=0,0,1 --hold=0,0,0,0.5`) logs
  `stunt: completed sghangar — Danger Zone [Fly Through] - Seaplane Hangar (1/9)` as it passes the
  doorway, and the plane passes the 20 m gap without colliding, which independently confirms the slit
  is real and flyable. (It then flies the full 255 m interior and crashes into `o11/col` just past the
  rear wall at z = −5362 — a separate world object, present on the baseline too.)
- **The control that makes it a test.** The identical scripted run with the fallback flipped back off
  logs **no completion at all** (`docs/verification.md` §5: a test never seen to fail proves nothing).
- **Visual.** `.scratch/polish4-item5-marker-on-hangar.png` — the reticle and the
  `Danger Zone [Fly Through] - Seaplane Hangar` label sit on the hangar's door opening at 775 ft.
- **8-chapter surface.** C1C and C2B (no `ia.json`) still log
  `--stunt: no danger zones for <ch>/IA1 — flying free` and build their worlds with zero errors.

**Found on the way and NOT implemented** (backlog entry "Per-zone danger-zone extents"): `dzpathN` is
not the "AI route ribbon" it was documented as. Each is a 3-polygon model — **polygon 0 is the
approach/exit polyline, polygons 1 and 2 are the two gate outlines**, the aperture rings the zone is
flown through. `dzpath1`'s polyline reads (−5668.1, 242.7, −5841.1) → … → (−5770.4, 28.5, −5622.0) →
(−5770.5, 30.9, −5366.7) → …: the dive in, the thread, and the climb out.

**Process note — no shared guard with item 4, deliberately.** Items 4 and 5 share the
`"transform": "Initial"` → null `Local` → parent-origin mechanism, and the plan asked whoever went
second whether one guard should serve both. It should not: item 5 wants a **position** ("anchor on
the geometry you can see"), item 4 wants a **presence decision** (treat the object as absent). A
common helper would only be the AABB-of-subtree walk, which is 20 lines and already exists in a third
form in `NodeLabels.cs`. `GeometryAnchor` was kept private to `StuntMission.cs` precisely so the two
items could not collide while being worked in parallel worktrees.

## 2026-07-22 — Focus mute, and the pad-read-on-focus question closed (polish-4 item 7)

**The mute.** Alt-tabbing away now silences the game and alt-tabbing back restores it, via the
project's first `_Notification` override (`PlaneViewer`). `NOTIFICATION_APPLICATION_FOCUS_OUT` /
`_IN` toggle `AudioServer.SetBusMute(0, …)` and log `focus: lost — audio muted, pad reads gated` /
`focus: regained — audio restored, pad reads live`, so a manual alt-tab test is one glance at the
console.

**Why the master bus and not `MixGain`.** There are two independent audio paths and only one has
any gain plumbing. `FlightAudio` has `MixGain`, but its crash and prop one-shots deliberately
bypass it ("one-shots stay global", HISTORY 2026-07-19); `WorldSounds`' ambient `SOUND_NODE`
emitters take `VolumeDb` straight from the sound def with **no** `MixGain` and no shared gate at
all. A `MixGain` mute would have left the whole animated world audible. Nothing in `CSVM/src` ever
sets `AudioStreamPlayer.Bus`, so Master (index 0) carries everything. `--mute` was not reusable: it
is a *load-time* switch that simply never constructs `FlightAudio`/`WorldSounds`.

**The regression the item named did not materialise, and was measured rather than argued.** The
worry was that the engine loop would resume by re-ramping from the `-60f` a fresh
`AudioStreamPlayer` is constructed with (`FlightAudio.cs:79`). The bus mute never touches
`FlightAudio` at all — the loop stays `Playing`, `_engineRamp` stays 1, and `Update` keeps writing
the throttle curve into `VolumeDb` while muted. A temporary probe read the engine player on every
focus transition: `playing=True volDb=-13.98 pitch=0.778 ramp=1.000`, identical before, during and
after, across two cycles. The same probe printed `volDb=-60.00 ramp=0.000` at startup, so it was
demonstrably capable of showing the failure (verification rule 5).

**How it was verified without a human at the keyboard.** Focus in/out is an interactive event, so
the test drove it: launch a live C1 flight windowed, then steal the foreground with a scripted
`AttachThreadInput` + `SetForegroundWindow` (a plain `SetForegroundWindow` from a background script
is silently no-opped by the Windows foreground lock — it reported success and the foreground never
moved, which read as "the notification never fires"). Two clean cycles, `2017` → mute, `2016` →
unmute, `AudioServer.IsBusMute(0)` confirmed each way, zero errors.

**Two facts worth keeping** (Windows 11 / Godot 4.7): the notifications that actually arrive are
the **APPLICATION_** pair (2016/2017), delivered alongside the WM_WINDOW_ pair (1004/1005) when
another application takes the foreground; and **minimising the window from another process
delivers neither of them** — only `WM_MOUSE_ENTER`/`EXIT`. So a manual test must alt-tab, not
click minimise, and a scripted one must steal the foreground rather than minimise.

**The pad half — landed as its own commit, and vetoable.** `docs/HISTORY.md:795-803` recorded the
open question of whether pad reads should be gated on window focus project-wide. They now are:
`PlaneViewer`'s focus notification sets `Pads.Focused`, and `Pads.For()` returns nothing while
`InputBlocked`. This is a behaviour change some players will not want (running windowed with a
pad), so it is its own commit and reverting it leaves the mute fully intact.

**The plan's proposed choke point was wrong, twice.** It named `Pads.Connected()` as "the natural
choke point". (a) It **misses the flight path**: `Pads.For(bound)` returns an explicitly bound
player's pads without ever consulting `Connected()`, and every splitscreen or menu-launched player
has a binding — the exact case the item exists to fix would have gone straight through. (b) It
**breaks the roster**: `Connected()` answers "which pads exist", not "which may be read".
`LaunchMenu.SyncDevices` reads it to drop a player *whose pad disconnected*, so an empty roster
while unfocused would un-join every joined player, and `PlaneViewer.AssignPads` reads it once at
session build, so alt-tabbing during a chapter load would have left the session pad-less until
relaunch. **A pad that is merely unfocused has not gone away.** The gate therefore went on `For()`
and `Connected()` stayed ungated; `SpectatorCamera`'s three read loops moved from `Connected()` to
`For(null)`, and `MenuInput`'s raw reads now go through `CSVM.Pads.For(Pads)` while its `Pads`
field stays the player's *binding*. `JoinPressed` carries the gate inline (it is static and has no
binding to pass). Coverage is complete by construction: every `Input.GetJoyAxis` /
`IsJoyButtonPressed` call in `CSVM/src` sits inside a `Pads.For(…)` loop, `JoinPressed` excepted.

`Pads.Focused` **defaults to true and fails open**, so headless runs and any window that never
gains focus behave exactly as before — no scripted-verification path changes. Only pads need the
gate: Godot releases held keys on focus loss, while joypads are polled from SDL regardless of it.

**Pad-half evidence.** With a real pad connected, across two driven focus cycles the roster held at
1 while both `For(null)` and `For(new[]{0})` went 1 → 0 → 1 — i.e. the *bound* path is gated too,
which is precisely what the plan's suggestion would have missed. Launchscreen smoke test still
shows P1 as "keyboard + pad 0" (the roster half staying ungated). 8-chapter `--freecam` regression:
exit 0 and zero errors in all eight (C1's single hit is the documented `--mute` "no audio session"
warning for `snd_police`).

**Left to the user.** That the muted output is actually *inaudible*, and that a physically held
stick produces no motion while alt-tabbed. The second follows by construction from an empty read
set, but neither is machine-checkable here.

**Also hit, and pre-existing:** `--headless` + `--screenshot` is broken — the dummy renderer's
`Texture2D.GetImage()` returns null, so the capture block NREs every frame and the process never
quits (a 206 MB stderr log in ~10 minutes). Screenshot runs must be windowed. Unrelated to this
change; recorded in `backlog.md`.

## 2026-07-22 — `FromToMotion`: an absent channel HOLDS, it does not reset to the rest pose (polish-4 item 2)

`AnimRuntime.FromToMotion.Create` seeded its pose from `RestOf(target)`, so **every
translate-only `OBJECT_MOTION_FROM_TO` event rewrote the node's orientation back to the shipped
gamez transform.** It now seeds three separate components — orthonormal rotation, scale, origin —
from the node's **live** transform at the instant the event fires. That is the rule
`ScriptPlayback` already documented ("an ABSENT channel means hold the last value this script
wrote, not return to rest", proven on C1/M04's `piratezep`) and the rule `AnimDefs.AddFromTo`'s own
in-code comment has always claimed ("a missing FROM means from wherever the object currently is —
left absent so the handler reads the live pose"). The handler simply never did it.

Components rather than one transform, for `ScriptPlayback`'s reason: it keeps `Seek(t)` a pure
function of `t`, and a rotate channel can no longer silently discard the node's scale (the old code
did `basis = Euler(...)`, dropping it). Seeded **once per event** rather than re-read per frame, so
a tween cannot compound into itself. A non-finite or singular live basis falls back to the rest
pose rather than poisoning every later event on that node.

**Survey that justified it (run before any code).** Across all 5 chapters' `cam_anim`/`mis_anim`:
1,802 `OBJECT_MOTION_FROM_TO` events, of which **883 carry no rotate channel**. Simulating each
sequence's rotation state and comparing against the gamez rest transform, **89 of those (26 nodes)
hold a value that differs from rest** — C1's traffic and firetrucks, C2's ten studebakers, its
sailboats and yachts. Longest divergent leg: `sailboat1`'s **300 s** held 180° out.

**Measurement.** C1/IA1, `--freecam --debug-anim`, 900 frames ≈ 118 s of sim, `LogMotions`' `Take(12)`
cap temporarily raised (the trap the `AnimRuntime` bullet records — the looping cars fall off the
printed list otherwise) and a temporary elapsed-time probe line; both removed before commit.
Baseline built by `git checkout HEAD -- <path>` + re-applying only the probe, verified by
`git diff --stat` showing **5 insertions / 1 deletion** immediately before the baseline build.
Reported as **median heading error against direction of travel** (the vehicle models' local forward
is +X — deduced from the chase's 45° yaw on a leg travelling (+33, 0, −34), confirmed by
`police_car`'s child bbox being long in X and narrow in Z), excluding samples where the car moved
< 1 unit (heading undefined) or > 120 units (the `Loop` restart teleport) between 1 Hz samples:

| node | before | after |
|---|---|---|
| `mafia` — **unaffected control** | **0.0°** | **0.0°** |
| `black_car1` | **90.0°** | **0.0°** |
| `truck1` | 90.0° | 1.9° |
| `car_loop1` | 62.1° | 0.0° |
| `car_go_home` | 49.4° | 0.0° |
| `police_car` | 0.0° (mean 4.1) | 0.0° (mean 2.1) |
| `suspect` | 0.0° (mean 5.0) | 0.0° (mean 1.4) |

`black_car1` is the cleanest case in the install: its whole def is one `ROTSTATE y=180°` plus a
single 16 s translate-only leg in an infinite `Loop`, so it drove **exactly sideways for its entire
life**, 90.0° off on every sample. `suspect`'s 27 s park went from the rest 0° to the authored
−110°, and both chase cars now hold 45° through the opening 2 s diagonal.

`firetruck1-6`, `hauler1` and `flatbed1` are in the survey's divergent set but are **not live in
C1/IA1** (they sit in `OnCall` `deploy_firetrucks` sequences), so they are covered by the data
survey and by construction, not by measurement — stated rather than implied.

**8-chapter `--freecam` regression: all 96 structural count cells identical** (nodes, mesh
instances, colliders, uv-clamped surfaces, defs, anchored, state ops, unresolved, live instances,
live motions, puffers, point lights × 8 chapters), zero Godot errors, warnings unchanged. That is
the *correct* outcome and not an inert change: this alters the pose a tween writes, not which
events fire or how many ops dispatch — and the same counters were shown able to move by item 1
earlier the same day (C1 4136→4132). Mode battery clean: `--fly`, `--stunt`, `--viewer`, 4-player
`--stunt --players=4`.

**Disproven before implementing — the plan's headline symptom was wrong.** The plan asserted the
police car's **17 s straight** "runs entirely at the authored rest orientation" and named it the
longest, most visible damage. It is true but harmless: that leg's held value is **0°, which equals
`police_car`'s and `suspect`'s authored rest** (both ship `rotate: {0,0,0}`), so it renders
identically before and after — and the measurement confirms it, 0.0° in both runs. The real damage
is the opening **2 s diagonal** (must hold 45°) and `suspect`'s **27 s park** (must hold −110°).
The mechanism the plan traced was exactly right; the symptom it picked to demonstrate it was not.

**Found and NOT fixed.** Every `*_delta` channel is dead code — see `backlog.md`.

## 2026-07-22 — Polish-4 item 4: entities the mission never places no longer render on the map corner

**Reported symptom.** C5 Instant Action shows a zeppelin buried in the ground, seen from
`--campos=235.618,1471.759,94.103 --lookat=237.62,1371.833,97.39`.

**Traced mechanism (the plan's account was right, and much too narrow).** A chapter gamez holds
every mission's content, and the chapter's *build* script — `support\<ch>\load.gw` in
`extracted/interp.json` — loads each vehicle with a bare `LoadGameGen` + `AddChild %worldName%`
and **no placement at all**. So every zeppelin, car, boat, train car and aeroplane in the install
starts life parked at the world origin with gamez `transform: "Initial"`; `GameZ.ParseTransform`
correctly leaves `GameZNode.Local` null, `SceneBuilder` correctly assigns no transform, and the
subtree builds at its parent's origin. Every chapter's world `area` is x,z ∈ [−N, 0], so that
origin is the **map's corner** — hence "buried in the ground" (the ground there is
`MapEdgeExtender`'s mirrored continuation).

A mission then either switches the entity off (its own `.gw` setup script) or places it (an
ON_STARTUP `OBJECT_TRANSLATE_STATE` — C3/IA1's `cgzepstate` puts `cargozep1` at
(−12412.9, 134.0, −10424.8) — or an `OBJECT_MOTION_FROM_TO`). Retail data misses some.

**Scope is wider than the plan stated, in two directions.**

1. **C5 is not the only chapter.** The plan says "C5's `ia1.gw` is the only IA1 setup script in the
   install that does not switch off `piratezep`" — **C1C's does not either** (its `ia1.gw` is four
   lines and names only `multiplayer1zep`/`multiplayer2zep`), leaving `piratezep`, `blackswanzep`
   and `workersvoyagezep` on the corner. The plan's list of chapters that *do* deactivate it
   (C1, C1B, C2, C2B, C3, C4) simply omits C1C.
2. **It is not a zeppelin bug.** Surveying all 8 chapters for world-build roots that are both
   transformless and geometrically wrapped around their own origin gives **122 candidates, every
   one a vehicle and not one terrain node**. In C5/IA1 the unhandled pair is `piratezep` +
   `sprucegoose`; in **C2/IA1 it is 16 police/security cars, `kidnapcar` and `fuel_truck01`** and
   no zeppelin at all.

**Fix** (`WorldBuilder.HideUnplacedEntities` / `RestorePlacedEntities`, called from `PlaneViewer`
straight after `AnimRuntime.Bind` and then polled once a second). After the animation bootstrap —
i.e. after the interp setup script (pass 0) and the ON_STARTUP definitions have run — any world
walk root that is *still* visible, *still* sitting exactly on the world origin, and whose built
geometry AABB strictly straddles that origin in x and z, is switched off (invisible **and**
non-collidable, mirroring `AnimRuntime`'s own INACTIVE handling). Anything that later moves off
the origin is restored.

**Verified.** Recorded pose before/after: zeppelin present → gone, terrain unchanged. Full
8-chapter enumeration of every parked candidate's visibility and position at 5 s, baseline vs fix:

| Chapter | Visible **before** | Visible **after** | Switched off |
|---|---|---|---|
| C1 | `passenger_trengine`, `tanker_car`, `box_car`, `caboose` (all placed, ≈(−6870, 128, −5580)) | identical | — |
| C1B | none | none | — |
| C1C | **`piratezep`, `blackswanzep`, `workersvoyagezep` — all at origin** | none | **3** |
| C2 | yacht1–4, sailboat1–3, `rocket`, studebaker1–10 (all placed) **+ `fuel_truck01`, `security9`, `kidnapcar`, `security1–6`, `police1–9` at origin** | yacht1–4, sailboat1–3, `rocket`, studebaker1–10 — same positions | **18** |
| C2B | none | none | — |
| C3 | `cargozep1` @ (−12412.9, 134.0, −10424.8) | identical | — |
| C4 | none | none | — |
| C5 | `agyrobus` @ ≈(−7420, 281, −11711) **+ `piratezep`, `sprucegoose` at origin** | `agyrobus` only | **2** |

**Nothing that was correctly placed disappeared, in any chapter.** C3's `zepbridge1`/`zepbridge2`
and C4's/C5's `zepdock` never enter the candidate set at all: they carry identity transforms but
their geometry is authored in world coordinates far from the origin, so the AABB test excludes
them. That is exactly the "parked zeppelins that are supposed to be at their gamez position" risk
the plan flagged, and it is handled by construction rather than by a name list. C2 shows both
halves working: **35 switched off at bootstrap, 17 restored** within the first second as their
FromTo motions started, leaving the 18 genuinely unplaced. Zero new errors in the 8-chapter
regression; C1's one warning (`AnimRuntime.ReportLateSoundFailure`) is pre-existing and reproduces
with the sweep disabled. Modes exercised clean: `--fly --chapter=C1`, `--stunt --chapter=C2`,
`--viewer --plane=player_bhawk`, `--fly --chapter=C5 --players=2`.

**Two traps hit and recorded** as `docs/verification.md` rules 16 and 17: the `child_bbox` frame
error (464 false hits vs 122 real ones), and "after bootstrap" not being "after everything that
places things".

**Found and NOT fixed on the way:** the engine ignores the gamez node `active` flag entirely
(`GameZ` never parses `flags.active`) — see `backlog.md`, including why it must not be fixed while
item 3 is open.

## 2026-07-23 — Polish-4 cross-item integration regression (items 1, 2, 4, 5, 7 together)

Each polish-4 item was verified by a separate agent against **its own base**, so until now no run
had exercised them together. Items 2 and 4 were the real risk: both touch the animation /
placement path, item 2 changing the pose a tween writes and item 4 deciding presence from where a
node ends up. Item 5 was a second, subtler risk — its hangar anchor keys on a transformless node,
which is exactly the shape item 4's sweep looks for.

**8-chapter `--freecam` (`--no-pads --mute --frames=90`): all 8 rendered, ZERO Godot-level errors**
(`^(ERROR|SCRIPT ERROR|USER ERROR|WARNING|USER WARNING):`). Item 4's sweep reproduces its
independently measured numbers exactly in the combined build:

| Chapter | switched off | restored | net |
|---|---|---|---|
| C1, C1B, C2B, C3, C4 | — | — | — |
| C1C | 3 (`piratezep`, `blackswanzep`, `workersvoyagezep`) | 0 | 3 |
| C2 | 35 | **17** (yachts 1–4, sailboats 1–3, studebakers 1–10) | 18 |
| C5 | 2 (`piratezep`, `sprucegoose`) | 0 | 2 |

The C2 restore firing is what proves the hide-then-restore half works end-to-end under item 2's
changed tween poses — the restored set is precisely the `OBJECT_MOTION_FROM_TO`-driven vehicles.

**Item 5 survives item 4's sweep** (`--stunt --chapter=C2`, 0 errors): `sghangar` still anchors at
(−5770.4, 23.5, −5623.9) on its 2 door leaves, 9 zones load, and the other 8 sit at their own
positions (`dz2` Ramses Tomb (−6033,26,−3844), `dz3` Seastack, `dz4` Bridge-North Spar). Safe by
construction rather than by luck: `IsParkedAtOrigin` requires the built AABB to *straddle* the
world origin, and the hangar's geometry is 8 km away — but it is the exact cross-item interaction
nobody had tested, so it was worth measuring rather than reasoning about.

**⚠ Instrument note — the first pass of this regression manufactured its own errors.** Grepping
the logs for `ERROR` reported 2 hits in C1 and 1 in most other chapters, which read as a real
regression. They were PowerShell `NativeCommandError` wrappers: in PS 5.1, redirecting a native
executable's stderr wraps each line in an ErrorRecord, so the *redirection* creates the word
ERROR. Godot's own diagnostics are line-anchored (`ERROR:`, `SCRIPT ERROR:`), and anchoring the
pattern took all 8 chapters to zero. Same family as the existing rule about grepping the full
stderr — **match the tool's own output format, not a substring that its transport also emits.**

## 2026-07-23 — Polish-4 item 6: the nose sags in a knife-edge, and the dead soft-tree branch goes

Two unrelated changes, landed separately. **Both were written by one agent, whose verification the
user judged untrustworthy mid-run; the work was backed out of `main` and re-verified independently
before landing.** That second pass is most of the value here — it confirmed the mechanics, quantified
a scope deviation the first pass had buried, found a side effect it under-described, and **disproved
a claim it had made about the user**.

### The knife-edge term

Both knife-edge terms in `FlightModel` acted on `VelocityDir`, and nothing ever wrote `Attitude`, so
a sustained knife-edge descended wings-level-**nosed**. A great-circle rotation now walks the nose
toward a bounded target elevation `−KnifeNoseSag × knife`, rate-capped at `KnifeNoseRate × knife`,
reusing the stall drop's own pattern (attitude-independent, no twist about the nose), gated entirely
off while stalled and able only to lower the nose.

**Measured** (C1, `--hold=0,1,0,0.5@0.55;0,0,0,0.5`, 35 s, `--no-pads --mute`; determinism proven
first by two identical baseline runs diffing to zero):

| | baseline | item 6 |
|---|---|---|
| nose | −0° flat | **−4°**, flat, from the first sample |
| path settled | −6° | **−10°** |
| sink rate | 11.8 m/s | **19.4 m/s (+64%)** |
| altitude lost, 35 s | 398 m | 634 m |
| level cruise | — | **identical, 25/25 lines** |

**Two properties the implementation must keep, both measured rather than reasoned.** The target is a
**bound, not a direction**: a nose falling freely toward world-down has no equilibrium, because the
path chases the nose and the coupled pair descends together forever. And the approach is a **rate
cap, not an exponential**: an exponential's rate scales with displacement and reached ~32°/s at a
+62° stalled-zoom nose, rewriting stall recovery.

**⚠ Scope deviation, accepted by the user.** The sag carries into the path **1:1** (settled −6° − 4°
= −10°). The plan required the path to be unchanged; that is unachievable by construction, not by
sloppiness. The constant was instead sized so the settled path lands on the −10° `HISTORY.md:39`
records as the designed knife-edge sink — noting that in that record −10° was the *transient* and −6°
the settled value, so this is a defensible reading, not a proven one.

**⚠ It is not only a knife-edge term.** `knife = 1 − |up·Y|` grows with pure pitch at **zero bank**
(0.5 at 60° pitch). A wings-level full-pull zoom loses ~11° of apex (+81° → +70°); the hands-off
climb-forever artifact erodes at ~4.6°/s; loops still complete, 1.4% slower. The original commit
described this as affecting the *banked* zoom — zero bank suffices. If unwanted, the fix is gating on
actual bank rather than `1−wingVert`, a code change and not a retune.

**Not carried forward:** the original commit's claim that a stall-into-knife-edge "converges to
within 1° after recovery" did **not** reproduce — the recovery dives settle ~9° apart and stay there,
because the pre-stall zoom is itself reshaped and a hands-off dive has no attractor. Not a bug, but
not a fact either. The stall gate rests on code structure (the whole block sits inside `if
(!stalled)`) plus absence of anomaly, not on an isolated measurement: this model only stalls out of a
zoom, so a scripted stall at wv≈0 could not be constructed.

Both constants are on the TUNE list. The user accepted the trade-offs and will tune from flight.

### The soft-tree branch, and a false claim about the user

`FlightController` carried a branch for colliders named `clutter_col` — 2.5 HP, ×0.92 speed, plow
through, never a direct crash, plus an exemption in the un-embed scan. Nothing builds that name any
more, so it is dead and is now removed along with `TreeDamage` and `TreeSpeedFactor`.

**The original commit's history was false, and it is worth recording why.** It asserted that "no
collider in the engine has **ever** carried that name", and concluded that the user's confirmed
7-tree forest plow (2026-07-19, Run-2 item 10) had been "flying through sprites that were never
solid". `git grep` at `a795548~1` shows `Clutter.cs:541` and `MapEdgeExtender.cs:270` both built
`StaticBody3D { Name = "clutter_col" }`, live from the forest-trees landing (2026-07-17) until
`a795548` (2026-07-22, *"billboards and clutter lose collision"* — itself a recorded user decision).
**The branch was live when the user flew it; the plow was the branch working exactly as designed.**
Trees are intangible *now*; they were soft *then*. The deletion is correct — the branch died at
`a795548` — but an agent came within one commit of overwriting a correct user observation with a
plausible-sounding reconstruction, in the commit message and in `docs/architecture.md` both.

**Verified:** deletion-only build vs baseline, two scripted collision runs (rolling wingtip → graze
vn 24.1 → damage → un-embed → destroyed; dive → crash vn 93.4), exercising every path the branch
touched — all collision and telemetry lines identical, diff 0. The stale-build loophole was closed
with a **binary marker**: the string literal `"clutter_col"` compiles into the DLL only while the
branch exists, and was confirmed present in the baseline DLL and absent in the deletion DLL.

## 2026-07-23 — Polish-4 item 9: the C5 ground z-fight was the unparsed OpenFlight SUBFACE flag

**The sixth mechanism, and the first right one.** This bug had five wrong diagnoses across four
sessions (coarse-sheet-vs-partition-ground; `g4683` fighting its own polygons on an AABB overlap;
the per-polygon within-surface tie-break; "nine coplanar World-child nodes stack at y=5", also an
AABB reading; and a day/night-or-LOD variant set). All five shared an assumption the item's own
title carried — that this was a **depth-precision** problem, fixable by a bias constant. It was
not. **No bias constant was changed, and none should be.**

Polygon flag `unk3` (raw `0x0800`) is the OpenFlight **SUBFACE** mark: "this face is coplanar with,
and contained in, the face beneath it — draw it on top". The world is loaded from `.flt` via
`LoadGameGen`, and **`support\init.gw` line 22 applies `GameGenSetSubfacePriorityOffset 1` to it
globally, for every mission in the game**, beside `SetCoplanarTolerance`/`SetBFETolerance`/
`SetInverseZTolerance`. `grep unk3 CSVM/src` returned **zero hits**. So C5's `cblock4/5/6` are not a
daylit LOD set — they are the **base** ground, and `cblock1/2/3` are subfaces laid on them; we were
rendering 66.5% of 25.85M m² of that ground inverted, plus 5.6% exactly tied.

**Fix — 3 edits, as the analysis predicted, no new parser and no constant changes.** Parse
`flags.unk3` in `GameZ.cs` (absent-when-false, so `TryGetProperty` with a false default);
`GameZPolygon.Subface` joins the surface-group key and the material-cache/`GetMaterial`/
`BuildMaterial`/`BiasMaterial` chain in `SceneBuilder.cs`; `bias += SubfaceBias` where the priority
bias is computed. **`SubfaceBias` is half a priority level (1e-4), deliberately not the original's
literal one level** — priority 1 is genuinely authored (955 C5 polygons, 2207 in C1) and a full
level would make a subface tie with a real priority-1 overlay; measured over every C5 subface/base
overlap, 0.5 and 1.0 resolve identically (25,732,146 m² front / 120,999 behind either way).

**Verified.** At the recorded C5 repro pose (`--viewer --chapter=C5 --sky-zone=zone2
--campos=-9533.178,76.319,-3367.413 --lookat=-9451.281,28.148,-3398.597 --shots=5 --jitter=0.006`):

| region | baseline | fixed |
|---|---|---|
| ground crop (0,330)–(850,720) | **28.87%** | **0.41%** |
| buildings crop (0,0)–(1280,300) | 7.13% (27,367 px) | 7.13% (27,373 px) |
| whole frame | 16.46% | 6.21% |
| `--jitter=0` control, whole frame | **0.00%** | **0.00%** |

The buildings crop is the honest reading of the 6.21% residual: it is a **pre-existing, separate**
facade phenomenon, identical to within 6 px across the two builds, and a ground-only fix must not
move it (`verification.md` rule 10 — a residual you have not driven to zero is not a floor; here it
was driven to a *different mechanism*, not asserted as noise). The `--jitter=0` control at exactly
0.00% on both builds proves the residual is not temporal noise either.

**Rule 4 satisfied the right way round — the metric fell AND the picture went to the authored
layer.** The baseline's ground is a washed-out daylit grey mush (the `cblock4` base winning); the
fixed build's is near-black night blocks with sparse street lights and a light-grey plaza. That is
`OriginalScreenshots/C5 IA1 Terrain.png` exactly, and it explains the user's original report ("I
could not find `cblock[4-6].png` in C5 IA1 in the original") — they are not absent, they are
**100.000%** buried under subfaces. **User confirmed at the controls: "Can confirm no more
z-fighting on the C5 ground."**

**8-chapter regression:** node, mesh-instance and collider counts **identical in all 8 chapters**;
zero errors (C1's single flagged line is a `--mute`-induced `snd_police` *warning*, present
identically in both builds — the grep false-matched `godot_variant_call_error` in its stack trace).
uv-clamped surfaces moved only where textured subfaces exist: C1 +0, C1B +0, C1C +0, C2 +3, C2B +0,
C3 **+1**, C4 +15, C5 +18. **C3 gaining exactly +1 is the sharpest confirmation in the run** — C3
ships exactly **one** `unk3` polygon in the entire chapter, so the instrument resolves
single-polygon granularity and lands precisely where the data says. C1B, C1C and C2B are unchanged
because their `unk3` polygons are all untextured `COLORED`.

**Aircraft are provably untouched:** `extracted/planes/models.json` carries **0** `unk3` polygons of
16,200, and the static plane viewer came out **byte-identical** (md5 `F1290254F2DDA1E3B8A9BCEE867D6C3D`
on both builds) — meaningful here because the same instrument had just been seen to move the C5
ground dramatically.

**C1B is structurally safe.** It ships **zero** `unk3` polygons chapter-wide, so this change cannot
perturb it — an independent, data-side corroboration of the user's 2026-07-22 ruling that C1B's
z-fighting is authentic to the original and must not be "fixed".

**Method notes.** Build freshness was proven from inside the running program rather than from the
build log, per the standing trap: the load line's uv-clamped-surface count (1373 baseline vs 1391
fixed) differs, so the correct binary demonstrably ran in each capture. `git stash` was avoided
(banned here); baselines were produced by `git checkout HEAD -- <files>` and restored with
`git apply`, both of which write current mtimes.

**Found and NOT fixed:** the `SurfaceRankCap` backlog entry's four cited C5 zero-separation pairs
(774,152 m²) were **all subface/base pairs** and are now resolved by this change, so that entry has
lost its measured example while its structural hole remains — flagged in `backlog.md` rather than
silently left to be re-quoted.

## 2026-07-23 — Polish-4 item 10 Part B: `csky_opacity` joins the splitscreen instance-uniform copy (and its stated symptom is disproven)

`PlaneViewer.InstanceShaderParams` listed three of the four instance uniforms `SceneBuilder` can
set, so `CopyInstanceShaderParams` — which re-applies them after `Duplicate()` in the splitscreen
cloud-deck loop — silently dropped `csky_opacity`. Added, referenced through
`SceneBuilder.OpacityParam` rather than a fourth string literal, since `AnimRuntime` already writes
it through that constant.

**The fix is correct; the plan's reason for it is not.** The plan called this a *live* defect: "a
deck posed to 0.6 opacity by `AnimRuntime` comes out fully opaque in players 2–4". Measured with a
temporary probe in a 2-player C1 session:

- The opacity-animated node is `/Session/world1/g27816/l2586/cloudparent/…` at **0.6**, exactly as
  C1's `clouds.zrd.json` authors it (`ON_STARTUP`, `OBJECT_OPACITY_STATE … ["ON", 0.6]`).
- It sits **inside `world1`**, which all panes share (one `World3D`, per-player visual layers), so
  it is never duplicated and cannot diverge between panes.
- The subtree the duplication loop *does* copy — WorldBuilder's flat `cloudlayer` deck — carries
  **no `csky_opacity` on any node**: the probe read `deck0 <none>`, `deck2 <none>`. Of 1840 world
  nodes carrying the parameter, every one outside `cloudparent` reads 1.0.

So this is **latent hygiene, not a bug fix**, and the plan's symptom must not be repeated. The list
is worth completing anyway — its contract is "every instance uniform `SceneBuilder` can set", and
Part A (the shared ordered preamble) depends on that contract being honest.

**Near-miss, and the reason `docs/verification.md` gained rule 22.** The first premise check swept
every compiled def in every chapter for opacity events targeting a cloud node and found **zero,
install-wide**. That read as "the plan is wrong about the data" and was one edit from being written
up as a disproof. It was the instrument: the sweep covered `cam_anim`/`mis_anim` only, and C1's
`cloudparent#` is **reader-only with no compiled twin** — a fact `docs/HISTORY.md:1696` already
records about this exact node. The animation layer has two sources and `AnimProgram` merges them
because neither is complete; any census over it must cover both and say so.

**Sequencing checked and fine** (the plan did not raise it): `animRuntime.Bind` runs at
`PlaneViewer.cs:672`, well before `AssignCloudDecks` at `:853`, so any opacity the bootstrap writes
is already on the source when the copy is taken. Had the order been reversed, copying at
duplication time would have been the wrong fix entirely.

**Part A (the shared ordered preamble) remains open** and is deliberately not bundled here: it
rewrites every world material's shader text, needs the full 8-chapter regression plus a
deliberately mis-ordered control to show the regression can fail, and carries a real
instance-uniform-buffer question (`SceneBuilder`'s `OpacityUniform` docstring says emitting it into
opaque variants would put them "on the instance-uniform buffer that Run-2 item 2 had to enlarge for
C4/C5"). That claim needs measuring before any preamble emits four uniforms everywhere.

## 2026-07-23 — Polish-4 item 10 Part A: the shared shader preamble, as `.gdshaderinc` files

The enforcing fix the `Clutter.cs` comment had been asking for since 2026-07-17, done as **real
shader files** rather than a C# const string (user's call, and the better one: a const string
single-sources the *text* but still lets each shader decide whether and where to emit it — and for
the instance-uniform block, that decision is exactly the bug).

**Load-bearing question, answered first:** Godot resolves `#include` in a `Shader` whose `Code` is
assigned **at runtime** from C#, not only in a `.gdshader` loaded from disk. Probed by declaring a
uniform inside an include and reading it back through `GetShaderUniformList()` — it appeared,
identically to an inline control. Worth recording that the *obvious* probe was useless: a
deliberately unresolvable include produced **no Godot error at all**, so "no shader errors in
stderr" cannot distinguish a working include from a broken one. The uniform list can.

**Four files in `CSVM/shaders/`:** `csky_instance_uniforms` (the ordered instance-uniform block),
`csky_srgb` (the DX7 gamma-space vertex modulate), `csky_atmosphere` (fog globals, `csky_world_light`
and the `csky_fog_amount` helper) and `csky_lights` (the `LIGHT_STATE` spill). Before this, the
sRGB function was copy-pasted into four shaders and the fog globals + fog expression into four each.
All include-guarded. **The combinatorial parts stay generated in C#** — `render_mode` and the
blend/scissor/scroll/clamp/shaded variants are 128 shader variants, not one file. This is a hybrid
by design, not a half-finished migration.

**The ordering rule, and its exception.** Every shader that declares *any* instance uniform now
takes the whole preamble in canonical order (`node_bias`, `csky_fog_on`, `csky_light_fade`,
`csky_opacity`), so the indices agree structurally. **The opaque sprite variants deliberately do
not**: Godot allocates a fixed 16-vec4 block per instance carrying any instance uniform, so a
shader with none keeps its instances off that buffer entirely — and C5 alone stamps ~139k billboard
sprites. Adding uniforms to a shader that already carries one is free; giving one to a shader that
carries none is not. That also retires the `OpacityUniform` docstring's claim that omitting
`csky_opacity` kept opaque *bias* materials off the buffer: those always declared `node_bias` and
`csky_fog_on`, so they were on it regardless. Measured: 8 chapters, **zero** `instance_uniforms`
warnings, including C4 and C5.

**Caught before it landed:** `AnimRuntime.HasOpacityPath` decided "does this shader read opacity?"
by string-scanning `sh.Code` for `csky_opacity`. Moving the declaration into an include would have
made that scan find nothing — silently inverting the diagnostic to "no alpha path" everywhere —
and declaring it via the preamble would have made a name-scan report *true* everywhere instead.
It now tests `SceneBuilder.OpacityTerm` (`" * csky_opacity"`), i.e. the **use**, which is what its
own comment always claimed it meant. Behaviour is unchanged: the term is still emitted exactly in
the blend/scissor variants.

**Verified — six pinned `--viewer` poses, A/B against HEAD:**

| pose | result |
|---|---|
| C5 city (world bias, clutter, lights, billboards) | **byte-identical** |
| C1 spawn | **byte-identical** |
| plane viewer (the shaded variant) | **byte-identical** (md5 `F1290254…`, unchanged since before item 9) |
| mesh lab (its own shader) | **byte-identical** |
| C2 spawn | 99.720% identical; 0.280% of pixels differ by exactly **1/255** |
| C1B water | 18.32% — **and this was the instrument, not the change** |

C2's residual is float reassociation from the fog expression becoming a function call: no pixel
moves more than one LSB, well under the project's `>3` flicker threshold.

**C1B is the finding worth keeping, and it is now `docs/verification.md` rule 23.** 18.32% of
pixels changed on the water pose, which read as a real water-shader regression. It was not: the
same build captured at `--frames=120` vs `--frames=121` gives **18.32%, max delta 9/255 — the
identical figures**. Moving blocks into include files changed Godot's shader-compile cost, so frame
120 landed one frame of UV-scroll phase later. Localising the diff is what named it: only the
scrolling water moved, the land in the same frame was untouched. Note the same-build determinism
check *passed* on both builds (bit-stable at a fixed frame count) and proved nothing about frame
*alignment* — a different control was needed.

**Not verifiable, and saying so is the answer:** the plan asked for a deliberately mis-ordered
preamble to prove the regression can catch mis-ordering. **That control cannot fire today.** The
index-collision hazard only manifests when two disagreeing shaders share one `GeometryInstance3D`,
and the plan itself established that never happens in this build (Clutter renders through
`MultiMeshInstance3D` + `MaterialOverride`) — which is why the bug is latent. Mis-ordering the
preamble would therefore render identically, and a passing regression would mean nothing. What
*is* verified is that the enforcement is structural rather than behavioural: there is now exactly
one declaration site, so there is no second order to drift from. The mechanism itself is not
re-derived here — it is inherited from the real 2026-07-17 unfogged-hilltops incident.

## 2026-07-23 — Crash choreography, first slice (polish-4 item 8): the dirt-crash effects

The original plays an authored effect sequence on a crash — three compiled cam_anim defs rooted
at the `player` node (`player_crash_default`/`_dirt`/`_water`), each a list of `CallAnimation`
events firing shared effect defs (`small_yellow_sparks`, `large_fireball`, `large_black_smokeball`,
`call_crash_trails`, `flydirt_plane`, …) at offsets from the crash pose. Item 8 was the last open
item in `docs/PLAN-M2-polish-4.md`. This landed a **first slice**: the ground/dirt variant's
pure-puffer effects, on top of the primary fireball (`CrashEffect`) and 10 s wreck fire
(`CrashBreakup`) earlier passes already shipped.

**What landed.** New `src/Flight/CrashChoreography.cs`: an `EffectSet` read ONCE per session from
the compiled defs via `PufferState.FromAnimEvent` (never transcribed — `trailpuffer2` is reused
with different numbers/sizes by sparks vs smokeball vs steam, so only the per-call payload is
authoritative), and per-player pooled `Puffer` emitters (3 sparks / 3 fireballs / 1 smokeball)
fired round-robin off a per-crash clock. `FlightController.Crash` picks the variant via
`ClassifySurface` and calls `Begin`; the crashed branch `Advance`s it; `Respawn` `Reset`s it.
The dirt schedule: 2 `small_yellow_sparks` bursts + the `large_black_smokeball` at t=0, then the
3-fireball cluster at `Event+0.25/0.50/0.75` s at the data's offsets. Anchored at **`pose.Origin`
(the data's `healthy` node = plane centre), not the impact contact point**. Two `Puffer.Create`
overrides added, both defaulting to a **byte-identical shader** for every existing puffer (the
`BLEND_MODE`/`SOFT_EXPR` substitutions reproduce the old code exactly at their defaults): `blend`
(force MIX/additive) and `softParticles` (disable the depth-fade).

**Three bugs, all the "instrument lies" class (verification.md §4/§7).** (1) The `blend` param I
added was **dead** — `Init` still selected the mode from `state.Colors.Count` alone, so the
smokeball (`colors: null`) stayed additive, and additive black smoke (`thickblksmoke0N` is rgb
(0,4,0) with a smoke-shaped alpha) adds ~0 = invisible. (2) I anchored the effects at the impact
point; the data attaches every crash effect to `healthy` (the plane centre, a few metres up), and
emitting at the ground contact both mis-places them and buries the smoke in the terrain. (3) The
shader's soft-particle depth-fade zeroed the alpha of fresh black smoke sitting near the terrain
behind it (a bright additive fire leaks through the same fade; MIX black does not), so the smoke
only appeared once grown — invisible within a scripted run's 1.5 s auto-respawn. **The first
"smokeball works" verification was wrong**: it mistook a fading additive fireball for smoke. It was
corrected by *isolating* the emitter — suppressing the fireball + wreck breakup and freezing the
crash (no `--hold`, so no auto-respawn) — which proved the smoke absent, then present after the
blend + anchor + soft-fade fixes.

**Verified.** Build clean, 0 warnings. Scripted dive-crashes into C1 terrain (`--spawn-at`/
`--spawn-dir`): the ground/dirt variant selected (`CRASH into g28031/col`), sparks arc up, the
fireball cluster builds over ~0.75 s, and the black smokeball develops into a proper column
(`.scratch/full_05.png`, `full_30.png`). 8-chapter `--fly` build: choreography builds every chapter
(`3 spark + 3 fireball emitters, smoke=on`), zero errors, no shader errors. The shader change is a
no-op for every other puffer (world steam/water, fire, damage trails) by construction, so freecam /
viewer / damage-lab renders are unaffected. User-confirmed the earlier "no black smokeball".

**Deferred to later slices** (each independently verifiable, all in `backlog.md`): the debris arcs
(`call_crash_trails` — needs `FromAnimEvent` to read DISTANCE intervals), the per-piece
`large_firetrail`+bounce sub-sequences, `flydirt_plane`/the water splash+steam (need the
mesh/`ObjectOpacityFromTo` path), and the water/air *variants* (water needs a sea-surface signal
the collision system does not expose; the air/no-impact variant has no trigger until weapons, M3 —
a building crash is `_dirt`, not air).

## 2026-07-23 — Crash choreography, second slice (polish-4 item 8): the debris arcs + the ground boom

The rest of the dirt/ground crash variant, on top of slice 1's sparks + fireball cluster + black
smokeball: the **five burning debris arcs** (`call_crash_trails`) and the **earth-impact boom**
(`snd_exp_ground_a`). The dirt `destroy_it` sequence's remaining unimplemented events were `flydirt_plane`
(event 9), `call_crash_trails` (event 12) and `Sound snd_exp_ground_a` (event 13); this landed 12 and 13.
`flydirt_plane` stays deferred — it is `puffers: null`, a mesh/scale/`ObjectOpacityFromTo` animation on
unbuilt `flydirt`/`dust` effect-root meshes, a different subsystem the plan groups in a later slice.

**The debris arcs.** `call_crash_trails` is five `spurtpufferN` DISTANCE_INTERVAL fire trails
(6-frame `fire_f01..06` flipbook, spacing 1.5–2.5 m), each attached to a `fly_trailN` node flung by
an `ObjectMotion` ballistic (`gravity` −1..−3, `translation_range xz` 35–255 / `y` 20–70, `run_time`
2.0–3.5 s). Two pieces were needed:

- **DISTANCE-interval support in `PufferState.FromAnimEvent`** (`Puffer.cs`). It previously always read
  `interval_garbage.interval_value` into `TimeInterval`; now `interval_type: "Distance"` routes it to
  `DistanceInterval` instead. ⚠ The flag shape is **inverted** for Distance — `has_interval_type` true
  but `has_interval_value` **false**, yet `interval_value` still holds the real distance — so it keys off
  `interval_type`, never `has_interval_value`. **Proven a no-op for every world puffer by construction:**
  `AnimRuntime` only ever *sustains* puffers (`SustainAt`, which ignores `DistanceInterval`, and whose
  pool sizing checks `sustained` first), so only the non-sustained crash trails read the new field; sparks
  and the smokeball are `interval_type: "Time"` (checked), unchanged.
- **A reconstructed ballistic anchor** (`CrashChoreography.StartDebris`/`Advance`). The `fly_trailN` node
  is never built (WorldBuilder builds only World-children + partition subtrees; the `carnage_trails.flt`
  effect roots are neither) — it is an invisible carrier, and only the fire it trails is seen. Crucially,
  `translation_range` is **undocumented and `AnimRuntime` does not implement it** (ballistic OBJECT_MOTION
  is counted, never simulated — no weapons trigger it), so there is no reference decode. The reading here
  is a *reasoned interpretation*, flagged TUNE: `xz`/`y` are the horizontal/vertical distance the debris
  travels over `run_time`, launched in a fanned random azimuth under the anim's own `gravity`; the
  `initial`/`delta` range fields are left unmapped (as `AnimRuntime` leaves the rotation `delta` it cannot
  place). Each frame while crashed, the virtual anchor's ballistic position drives `Puffer.TrailAdvance`,
  and `TrailEnd` fires at `run_time`.

**The ground boom.** `FlightAudio.OnGroundExplosion` plays `snd_exp_ground_a`, called by
`FlightController.Crash` **only for a Ground surface**, layered over the plane explosion `OnCrash`
already fired (`plane_destroy_sg`). The plan directs layering rather than replacing (`plane_destroy_sg`
is treated as done and correct); strictly the dirt def's only Sound event is `snd_exp_ground_a`, so
"keep both" is a judgement call on the TUNE list. A real `SOUND_GROUPS` resolver is still owed for the
piece-bounce `air_mixed_exp_sg`/`ground_mixed_exp_sg` (later slice).

**Verified.** Build clean, 0 warnings. Forward-dive crashes into C1 (`--spawn-at`/`--spawn-dir`, no
`--hold` so the crash freezes): the ground/dirt variant selected (`CRASH into g28031/col`), and the
frame burst shows the fireball cluster, scattered sparks, tumbling wreck pieces, and **fire-trail debris
streaking outward** from the blast (`.scratch/obl_06.png`), with the black smokeball still developing
(slice 1). 8-chapter `--fly` build: every chapter reports `3 spark + 3 fireball + 5 debris-arc emitters,
smoke=on`, **zero hard errors** — the per-chapter `carnage_trails` load works everywhere. The lone log
"error" under `--mute` is the benign `snd_police` late-SOUND_NODE warning (no audio session), unrelated.
The one interpretive risk is honest and recorded: the arc *shape* is a reading of `translation_range`,
not a decode — but the anchor is invisible, so only its scale reads, and that is TUNE for the user.

**Deferred to later slices** (each independently verifiable, all in `backlog.md`): `flydirt_plane` dust
+ the water splash+steam (mesh/scale/`ObjectOpacityFromTo` path, not puffers), the per-piece
`large_firetrail`+bounce sub-sequences (need a `SOUND_GROUPS` resolver), and the water/air *variants*
(water needs a sea-surface signal the collision system does not expose; the air/no-impact variant has no
trigger until weapons, M3 — a building crash is `_dirt`, not air).

**Animation debugger Wave 1 A1 — orbit camera extracted to `src/UI/OrbitCamera.cs` (2026-07-23):**
first slice of `PLAN-anim-debugger.md`, and the first slice of the eventual `PlaneViewer` split. The
static inspection view's orbit-camera controller — LMB-drag orbit, wheel zoom, and AABB framing —
moved **verbatim** out of `PlaneViewer` into a standalone `UI.OrbitCamera` so `--anim-lab` can drive
the same orbit camera without duplication. `OrbitCamera` owns the orbit state
(`_orbitCenter`/`_orbitDistance`/`_yaw`/`_pitch`/`_dragging`) and steers a `Camera3D` it does not own;
`Frame(aabb, camPos, lookAt)` is the old `FrameCamera` body, `Update()` the old `UpdateCamera`,
`HandleInput` the LMB/wheel/drag switch, and `Yaw`/`Pitch` seed the initial angles from
`--yaw=`/`--pitch=`. `PlaneViewer` constructs it once in `_Ready` beside the persistent `_camera`,
delegates `FrameCamera`/`_UnhandledInput`, and reads `OrbitCenter` back out for `PrintCameraPose`
(F11) and `ApplyShotJitter` — so `--campos`/`--lookat`/F11/F12/`--screenshot` behaviour is unchanged.
A pure refactor with no external effect. **Verified byte-identical** (the Wave 1 requirement): static
plane-viewer `--screenshot --jitter=0` md5s were captured from the HEAD build and the refactored build
for `player_bhawk`, `player_fury`, and a `--yaw=0` variant — **all three pairs byte-identical**
(`f1290254…`, `7fe61a97…`, `8348bdd4…`); the restored refactored build reproduced the baseline exactly.
Determinism and the able-to-fail control both hold (verification.md rule 5): two launches of the same
build produced identical bytes, and the `--yaw=0` image differs from the default-angle image, so the
instrument *can* register a change. Build clean, 0 warnings. `dotnet build` mtime-trap ruled out (the
rebuilt dll was confirmed newer than the restored source). Evidence: `analysis/anim-debugger-verification/` (the durable instrument + numbers; the `.scratch/orbit-verify/` outputs were ephemeral). Wave 1
A2 (`SessionPaths`) and A3 (`WorldSession`) are the next slices before the runtime work.

**Animation debugger Wave 1 A2 — extracted-data path resolution to `src/SessionPaths.cs` (2026-07-23):**
second slice of the `PlaneViewer` split. `PreferUnzipped` (prefer the unpacked sibling dir over its
`.zip`) plus the per-chapter/per-mission extraction-path construction moved **verbatim** out of
`PlaneViewer` into a static `SessionPaths` helper — `ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr(dataRoot, chapter[, mission])` — so `--anim-lab` (and A3's `WorldSession`) resolve the
same paths a normal session does. Pure path arithmetic; the `--gamez=`/`--textures=` override policy
(`x ? _xPath : SessionPaths.Chapter…(…)`) deliberately **stays** in `PlaneViewer` as CLI concern, and
`_Ready`'s base-path `PreferUnzipped` calls + `StartSession`'s per-chapter construction + the world
build's `chapterZrdrPath` all now route through the helper. No external effect. **Verified inert twice
over** (the resolved strings are byte-identical by construction, but confirmed with able-to-fail
instruments per rule 5): (1) the static plane-viewer `--screenshot --jitter=0` md5 is unchanged
(`f1290254…`, HEAD-vs-after — covers `PreferUnzipped(planes)` + `ChapterTextures`); (2) a C1/IA1
`--fly` world boot census — gamez-node/mesh/collider counts *and* the full anim def/instance/motion/
puffer/light/condition census (814 defs, 616 ON_STARTUP, 615 live instances, 7064 gamez nodes, …) —
is **byte-identical** HEAD-vs-after after normalising out run-to-run timing (covers `ChapterGamez` +
`MissionZrdr` + `ChapterZrdr`); the 14-line census is non-empty, so "identical" is a real match, not two
empty files. Build clean, 0 warnings. Evidence: `analysis/anim-debugger-verification/`. **A3 (`WorldSession`) is the
last Wave 1 slice before the runtime work (Wave 2).**

**Animation debugger Wave 1 A3 — world+anim build to `src/Mech3/WorldSession.cs` (2026-07-23):**
the third and last slice of the `PlaneViewer` split, and the biggest. The whole world-mode build —
`WorldBuilder` → texture cycler → clutter → mission setup → `AnimProgram.Load` → the `AnimRuntime`
collaborator wiring (`PufferFactory`/`Lights`/`Sounds`/`PlayerPosition`) → `Bind` → sound prewarm —
moved **verbatim** out of `PlaneViewer.StartSession`'s `if (_worldMode)` branch into a `WorldSession`
class in `Mech3`, so `--anim-lab` will build the same world+runtime a flight/viewer session does.
`WorldSession.Build(Options, gamez, textures, sounds, soundDefs)` returns `Root` (the viewer's
`_plane`), `Runtime`, `Program`, `Builder`, `Clutter`, `CloudDeck`, and `Lights`. **Three seams were
kept faithful:** (1) it stops *before* the per-view steps (unplaced-entity watch, edge extender,
per-rig horizon + weather), which stay in `PlaneViewer` and read the returned `Builder`, and it does
**not** add `Root` to the tree — the caller's `_worldRoot.AddChild(_plane)` still owns that, with the
effect siblings (world sounds + puffers) parented to `Options.EffectsParent` = `_worldRoot`, siblings
of `Root`, exactly as before; (2) the disposal-lifetime contract is preserved — `textures`/`sounds`
are the caller's `using` locals, so after the bootstrap it nulls `PufferFactory`/sound `Loader` and
prewarms first, *unless* `Options.KeepArchivesOpen` (the lab's opt-out, wired now, first used in Wave
3); (3) crash-effect loading (item 8) was **left in `PlaneViewer`**, reading `session.Program`, so no
`Mech3→Flight` dependency enters the class. No external effect. **Verified inert with able-to-fail
instruments (rule 5), on two very different chapters:** the static plane-viewer md5 is unchanged
(`f1290254…`, proving the else-branch is untouched), and the full `--fly` world boot census —
gamez-node/mesh/collider counts plus the entire anim def/instance/motion/puffer/light/condition
census — is **byte-identical** HEAD-vs-after for both **C1** (the 814-def, 7064-node airfield with
clutter/sounds/puffers, 17-line census) and **C5** (the 11 438-node, 4253-collider city, 15-line
census) after normalising run-to-run timing; both censuses are many non-empty lines, so "identical"
is a real match. Build clean, 0 warnings. Evidence: `analysis/anim-debugger-verification/`. **Wave 1 (the
`PlaneViewer` split A1-A3) is complete; Wave 2 (the additive `AnimRuntime` capabilities — manual
`Advance`, `AutoStart`, seedable RNG, dispatch hooks, `Stop` cleanup) is next.**

**Docs: CLAUDE.md slimming — plans table + format gotchas moved out (2026-07-23):** two moves per
the budget/shape rules. The completed-plans table violated "Current status is current state and
next step ONLY — it is not a log" (a table of finished plans is a log); it moved to a new index at
`docs/plans/plans.md`, which gets a row appended whenever a plan completes, and "Current status"
now points there. The "Format gotchas" bullet list moved verbatim to `docs/formats/gotchas.md`
(added to the formats README index; only cross-references were re-pointed and the two "(item 6)"
plan-relative tags dropped), with CLAUDE.md keeping a one-line pointer naming the gotchas so they
still surface in every session's context. Pure docs move, no content changed; CLAUDE.md 34.2 KB →
under budget. **A second batch the same day, user-reviewed (all eight edits approved):** removed the
budget-section precedent line; added Milestone 3 to the charter list; trimmed the history clauses
from the OriginalScreenshots bullet, the CLI-default paragraph (HISTORY §2026-07-20 has the
inversion), and the two growth war-stories; collapsed the polish-4 landed-items log in "Current
status" down to its two live facts (the vetoable pad-read-on-focus gate, the owed-playtests
blocker) — every deleted item has a dated entry here, and the opaque-sprite-preamble trap lives in
architecture.md's SceneBuilder bullet; trimmed the M3 research detail (recorded in the plan doc);
merged the empty "Known issues" stub into the backlog pointer. CLAUDE.md → 25.9 KB.

**Animation debugger Wave 2 — additive `AnimRuntime` capabilities (2026-07-23):** the five runtime
hooks the `--anim-lab` mode drives, all additive with defaults that preserve live behavior, landed
in `src/Mech3/AnimRuntime.cs` **only** (`+241/-53`; the WorldSession throwaway probe reverted to a
zero-line diff). **B1 manual advance:** the `_Process` body moved to `public void Advance(float dt)`
and `_Process` delegates, so the lab can `SetProcess(false)` and feed a fixed-dt clock through the
**same code path** the game uses. **B2 quiet stage:** an `AutoStart` flag (default true) gates
bootstrap passes 2 (ON_STARTUP) and 3 (startanims), which were extracted verbatim into
`RunAmbientPasses()`; when false, passes 0/1/4 still run (mission setup, reset states, the safety
net) so the stage is fully *set up* but *still*, and an idempotent `StartAmbient()` runs the deferred
passes on demand. **B3 seedable RNG:** `_rng` gained a `Seed` init-property (default unseeded = game
unchanged) — the constraint the crash plan's Layer-1 handlers must route their randomness through.
**B4 dispatch observability:** three null-by-default hooks — `OnEventDispatched` (a public
`readonly record struct EventDispatch{Def,Anchor,Sequence,EventIndex,EventKind,EventName}`, raised in
`SequenceRunner.Advance` right after a timed dispatch) plus `OnInstanceStarted`/`OnInstanceFinished`
— so Wave 4's timeline reads fired marks straight from the runtime instead of parsing `--debug-anim`
text. The `?.Invoke(new EventDispatch(…))` short-circuits argument construction when the hook is null,
so it is zero cost on the hot dispatch path. **B5 `Stop` cleanup:** `Stop` now tears down the stopped
def's live motions/puffers/lights/sounds via `TearDownResourcesOf` — motions and puffers attributed
to the exact `(def, anchor)` that registered them (a new `Owner` on `IAnimMotion`, set in
`AddMotion`; a `(Puffer, Def, Anchor)` value in `_puffers`), lights and sounds cleared by anchor
(their `(name, anchor)` key already collapses cross-def name collisions). **The B5 trap, found by the
regression, not by reading:** `Start`'s *own* restart (`Stop` at its top) must **not** tear down —
the whole codebase relies on resources *surviving* an instance swap so re-assertion is a seamless
no-op (motions replaced by target in `AddMotion`; puffers/lights/sounds short-circuit on
re-assertion). Tearing down there broke that and rebuilt every resource. It surfaced as a
deterministic **C5 `+2 state ops`** (4146→4148, and C5 rolls no `RandomWeight`, so not dice noise);
a guarded teardown probe traced it to C5 restarting `m_crane_go` and `please_go_spark` at boot. The
fix routes `Start`'s restart through a private `RemoveInstances(tearDown:false)`, leaving only
explicit `Stop` (STOP_ANIMATION / the debugger / the crash respawn) to tear down. A second, subtler
bug the throwaway probe's accounting caught: the Start-internal finish notified `OnInstanceFinished`
even when `_instances.Remove(inst)` no-oped (a t=0 STOP_ANIMATION had already removed it), so
`started − finished ≠ live` by one; gating the notify on the actual removal (`inst.Finished &&
_instances.Remove(inst)`) made the hook accounting exact. **Verification (rule 5 both directions):**
*inert with defaults* — C1 and C5 boot census **byte-identical** HEAD-vs-after (C1's `hangar3_doors`
StopAnimation *does* now tear down 3 door motions, but `mp_hangar3_open` re-adds them, so the count
nets out — the documented "later-registration-wins" door handoff, still correct), the static
plane-viewer md5 unchanged (`5832b6f9…`), and all 8 chapters show **no real boot teardown** except
that C1 handoff; *able to change* — a throwaway `AutoStart=false` gives "0 ON_STARTUP + 0 start anims
running, 0 live instance(s)" with reset states (3511 ops) and the safety net (31 hidden) still run,
`StartAmbient()` then reaches the **exact** normal live state (616 ON_STARTUP + 5 startanims, 615
instances, 39 motions), and the hooks fire 1330 dispatches / 623 starts / 8 finishes with
`623 − 8 = 615` balancing the live count. The lone stderr "error" is the pre-existing `snd_police`
"requested after the world build" `GD.PushWarning` (present in the HEAD baseline too, under `--mute`);
the only backtrace difference is the new `Advance` frame from B1. Build clean, 0 warnings. Evidence:
`analysis/anim-debugger-verification/`. **Wave 2 complete; Wave 3 (the lab MVP — quiet stage, transport, fixed dt + seed,
`--play-anim`, auto-frame) is next.**

**Animation debugger Wave 3 — the `--anim-lab` MVP (2026-07-23):** the lab mode itself: quiet
stage, transport controls, deterministic fixed-dt clock + pinned seed, `--play-anim` auto-play
with camera auto-frame. **New `src/UI/AnimLab.cs`** (clock accumulator at `FixedDt` = 1/60,
clamped 0.25 s; Space pause · `.` step (pauses first) · R restart · S stop · A ambient · 1/2/3 =
0.1×/0.25×/1×; status readout hidden in screenshot runs; in scripted runs wall time is ignored —
exactly `timeScale×1` step per frame, so captures land on exact step counts). **`AnimRuntime`**
gained `Play(animName)` (bootstrap pass 3 now routes through it, so the lab starts defs exactly
as the bootstrap does), `Reseed()` (re-pins `_rng` on every Play/Restart — without it a seeded
replay continues the stream and diverges on the first RANDOM_WEIGHT), `FrameTarget()`, and
`ManualAdvance`. **`WorldSession.Options`** gained `AutoStart`/`RuntimeSeed` (default-preserving);
**`OrbitCamera`** gained `MergedAabb` (PlaneViewer's `ComputeAabb` moved verbatim, both call
sites delegate); **`PlaneViewer`** parses `--anim-lab`/`--play-anim=`/`--seed=` (lab wins over
every other mode), hands the session archives to the lab (`using var scope = _animLab ? null :
textures` keeps the disposal contract elsewhere; the catch disposes on a failed lab build), and
optionally parks a `--plane=` stage prop at the mission spawn. **The bug the verification caught
— the clock hand-off is a flag, not `SetProcess(false)`:** Godot re-enables processing at READY
for nodes overriding `_Process`, and the runtime enters the tree after the lab mode is
assembled, so the intended `SetProcess(false)` was silently undone and the lab world ran at
exactly **2×** (fixed steps + wall dt). The pose-per-sim-second lab-vs-live comparison could not
see it (both sides sampled per sim-second — rate errors cancel; now a rule in
`docs/verification.md` §4); what caught it was 20 logged sim-seconds in a 610-frame (~10 s) run
against the freecam control's 10, and its visible symptom was same-seed screenshot bursts
differing by a quarter-step of door motion (wall-dt variance). Fixed with
`AnimRuntime.ManualAdvance` gating `_Process`. **Verified:** *game inert* — static plane-viewer
md5 `f1290254…` and the full C1 + C5 `--fly` boot censuses byte-identical HEAD-vs-after (re-run
after the final build); *quiet stage* — C1 gives 0 ON_STARTUP + 0 startanims, 0 instances, with
reset states (3511 ops), mission setup (29 nodes) and the safety net (31 hidden) still applied,
0 colliders by design; *lab matches live* — `--play-anim=train_on_track` pose-per-sim-second
equals the `--freecam` world's (caboose within 0.3 m at second 1, rotations within 0.4°, same
~35 units/s track speed; the raised-`Take(12)` log-cap probe was reverted); *determinism* —
same-seed `--frames=90 --shots=3 --jitter=0` bursts on `mp_hangar3_open` byte-identical (3/3
frames, twice), `--frames=30` differs (rule-5 control); *seed is live* — C1/M05
`--play-anim=random_prop`: seeds 1/2/4 roll `false TRUE false`, seeds 3/5/6 roll `false` (a
different branch), same-seed reruns identical. Build clean, 0 warnings. Evidence:
`analysis/anim-debugger-verification/` (`verify.ps1` re-runs every scripted check; the `.scratch/wave3/` outputs were ephemeral). **Wave 3 complete; Wave 4 (picker + authored-vs-fired timeline, fed by the B4
hooks) is next.**

**Evidence out of `.scratch/`, into `analysis/anim-debugger-verification/` (2026-07-23):** user
rule — `.scratch/` is a tmp folder; anything worth keeping moves to a tracked location (now also
stated in CLAUDE.md's scratch section). The anim-debugger waves' evidence dirs
(`.scratch/orbit-verify/`, `wave2/`, `wave3/`, 66 MB) were exactly the class the `analysis/`
README warns about, so the durable parts moved there: **`verify.ps1`** (re-runs every scripted
Wave 1-3 check against the current build — plane-viewer md5, C1/C5 censuses vs the committed
`baseline_*.census`, the quiet stage, the 610-frame clock-rate regression test for the
ManualAdvance 2× bug, the same-seed/control determinism bursts, the seed probe; all 8 checks
PASS on `main`, outputs land in `.scratch/anim-lab-verify/` and stay ephemeral) and
**`FINDINGS.md`** (the accepted numbers + the instrument bugs, incl. the `--screenshot=`
relative-path-resolves-against-`CSVM/` gotcha). The PNGs were deliberately NOT preserved —
rendered frames are game-derived and never enter version control; the md5s in FINDINGS carry
the comparisons. The five HISTORY "Evidence:" citations above now point at the analysis dir,
and the swept `.scratch` dirs were deleted.

**Screenshot mode no longer steals foreground focus — `--no-focus` (2026-07-23):** the automated
`--screenshot=` verification runs (fired constantly to check rendering) opened a real Godot window
that grabbed the user's foreground focus off their editor/terminal for the ~15 frames it rendered
before quitting, interrupting them every capture. Fixed in `PlaneViewer._Ready` (see the
PlaneViewer bullet's no-focus note): right after the arg loop, `DisplayServer.WindowSetFlag(WindowFlags.NoFocus, true)`
is set whenever `--screenshot=` is present (or the new manual `--no-focus` flag, for `--freecam`
observation), so every existing screenshot command is now non-focus-stealing with **zero change to
how the capture is launched**. On Windows this sets `WS_EX_NOACTIVATE`; rendering is unaffected (a
non-minimized background window still composites, so the PNG stays valid). Verified: `dotnet build`
clean; a `--chapter=C1 --screenshot=` capture with a text editor focused leaves the editor focused
and writes a correct non-black render; a normal `RunGame.ps1` launch is unchanged (the flag is only
set for `--screenshot`/`--no-focus`). **Research findings, recorded so they aren't re-chased:** Godot
4.7 has **no** CLI arg or project setting to launch a window unfocused (`--fullscreen`/`--maximized`/
`--always-on-top`/`--position` and the `display/window/size/always_on_top`/`…/borderless` settings
exist; a no-focus equivalent does not); the `FLAG_NO_FOCUS` window flag set from C# is the only
in-engine lever, and because the engine creates the window before any C# runs it can't be guaranteed
to suppress a sub-second first-frame flash (escalation if it ever matters: an OS-level launch wrapper
that saves `GetForegroundWindow` and hands focus back — not built). `--headless` is a dead end for
screenshots — it forces the dummy display+rendering driver, so `GetViewport().GetTexture().GetImage()`
returns a blank PNG. `FLAG_NO_FOCUS` also makes the window ignore keyboard input (docs: "will ignore
all input, except mouse clicks"), which is why it's scoped to captures/observation, not keyboard flying.

**Anim-debugger Wave 4 — the def picker + authored-vs-fired timeline (2026-07-23).** PLAN-anim-debugger
Wave 4 (D), the last piece of the `--anim-lab` UI before the crash stage. Two additions, both fed
straight from the Wave-2 B4 runtime hooks (`OnEventDispatched`/`OnInstanceStarted`/`OnInstanceFinished`),
no `--debug-anim` log-parsing:

- **Def picker** (in `src/UI/AnimLab.cs`): a `LineEdit` filter + `ItemList` over `program.Defs`, one row
  per def as `anim-name · activation · @anchor`. Type to substring-filter across all three columns; Enter
  plays the first filtered playable row, double-click/activate plays that one; defs with no ANIMATION_NAME
  are disabled (Play keys off the name). **P** toggles the panel.
- **Timeline** (`src/UI/AnimTimeline.cs`, a custom-drawn `Control`): one lane per Initial (non-on-call)
  sequence of the played def, the events drawn as **authored** blocks in an upper band, the runtime's
  **actual** dispatch ticks in a lower band, a moving playhead, and any CALL_ANIMATION child def as an
  appended indented lane group offset at the playhead time it started. The authored schedule is computed
  by `BuildLane`, a single linear pass that re-derives the *documented* rule (an event's own `start`
  gates it; "Event"/absent measured from the previous event's completion = its fire time plus its run
  time; control-flow events placed but taking no time) — deliberately **not** by calling the runtime's
  `SequenceRunner`, so a runner bug diverges from this rather than matching it. `StaticDuration` mirrors
  `Dispatch`'s `duration` out-param (FromTo/spin `run_time`, SI-script length) so a timed block is a bar
  of the right width. A near-vertical connector on each event's first firing = "fired on schedule"; a
  slant is the divergence — this is the instrument that would have caught the polish-4 `NextDue`
  off-by-one. The axis auto-scales to `max(2 s, last authored, last fired) × 1.05`, so a loop's period
  reads off the tick spacing (ticks accumulate across passes, capped 600/lane; Restart clears).

**Wiring.** Three ordering details are load-bearing and were verified by measurement: (1) `AnimLab.Play`
sets the timeline focus + clears marks **before** `AnimRuntime.Play`, because that call fires the def's
t=0 events synchronously and the dispatch hook needs `_timelineDef` in place; the anchor is captured from
the first `OnInstanceStarted` of the focus def, which the runtime raises before the t=0 `inst.Advance`.
(2) `Step()` bumps `_steps` **before** `Advance`, so during a dispatch the playhead (`_steps × dt`) equals
the runner's clock (incremented at the top of `Advance`) and a fired tick lands at the time it actually
fired; the t=0 events (dt=0, `_steps=0`) stamp at t=0. (3) A sibling def sharing the played def's
ANIMATION_NAME is another template instance, not a CALL_ANIMATION child (`SameAnimName` excludes it), and
pressing **A** (ambient) sets `_trackChildren=false` so the cascade it launches doesn't pollute the strip.
The hooks are attached in `_Ready` **only when the UI is shown**, so a plain scripted run leaves them null
and the runtime's null-conditional dispatch stays zero-cost. New flag `--debug-anim-ui` (implies
`--anim-lab`) forces the whole lab UI visible in a `--screenshot` — the timeline-verification path;
otherwise the UI is hidden there so shots stay byte-identical (`AnimLab.ShowStatus` → `ShowUi`, now gating
status + picker + timeline together).

**Verified** (`.scratch/anim-lab-verify/`, git-ignored — the shots frame game-asset geometry so they can't
be committed): `--anim-lab --chapter=C1 --play-anim=desert_onoff --debug-anim-ui` — the **bowl sign**, the
very def the polish-4 `NextDue` fix was proven on — draws its single `on_off` lane with each des_on/des_off
pair's fired tick landing on its authored block at 0/1/1.5/1.65/1.8/2.45/2.85/3.2/3.4 s and the second loop
iteration re-firing at ~4.6 s (all times exact 1/60 multiples, so the connectors are vertical = no
divergence, exactly as the fix intends). `--play-anim=train_on_track` renders four SI-script car lanes +
`steamplume` with ~327 s authored duration bars and t=0 fired ticks (authored blocks visibly render as
wide blue bars across multiple lanes). A plain `--anim-lab --play-anim=desert_onoff --screenshot` with
**no** `--debug-anim-ui` renders a clean overlay-free 3D frame (the scripted-screenshot path stays
byte-identical). C1 `--fly` boot unchanged (616 ON_STARTUP + 5 start anims, 615 live instances, 39 motions
— the full ambient world, since the non-lab path never attaches the hooks). Build clean (0 warnings).
**Next: Wave 5, the crash stage** — build the plane's `destroyed` subtree + effect templates into the lab
and wire the puffer factory (the crash plan's Wave 2a/2b scaffolding).

**Anim-lab interaction overhaul — freecam camera, transport panel, click-to-follow, ambient off (2026-07-23).**
Five changes from the first `--anim-lab` playtest, agreed with the user before building:

- **Camera is now the `SpectatorCamera` freecam** (RMB look, WASD/QE move, Shift boost, wheel speed) in
  place of the orbit view — "the camera should work just like freecam". PlaneViewer builds it for the lab
  (started at the mission spawn, `--campos`/`--lookat` override) and the four `_fly || _freecam`
  camera-mode conditionals (FOV 62, FrameCamera-skip, `_orbit.HandleInput`-skip, F11 pose) now include
  `_animLab`.
- **Transport moved to an on-screen button panel** (Pause/Step/Restart/Stop, an Ambient toggle, a
  0.1×/0.25×/1×/2×/4× speed `ButtonGroup`) because the freecam took over WASD/QE. Four non-clashing key
  shortcuts kept: **P** pause · **`.`** step · **R** restart · **F** picker.
- **Fixed "pause resets the animation, then does nothing"**: the old **Space** pause reached a
  focused picker `ItemList` as `ui_accept`, which *activated the selected row* (replaying from step 0)
  instead of toggling pause. Fix: Space is no longer a transport key, every transport button is
  `FocusMode = None` so it never holds keyboard focus, and `PlayRow` releases GUI focus after a pick.
- **Ambient toggles both ways**: new `AnimRuntime.StopAmbient(keepAnim)` tears down every live instance
  and its resources except the played def (kept by name at its playhead), re-applies the pass-1
  RESET_STATE base poses for the torn-down defs + the pass-4 safety net, and clears `_ambientStarted` —
  symmetric with `StartAmbient`.
- **Click any object to focus + orbit it**: an LMB click in the 3-D area ray-casts through the camera
  (`PickObject`/`RayAabb` — a manual ray-vs-AABB scan over the built `MeshInstance3D`s, since the lab
  builds no physics colliders; the local-ray parameter equals the world distance, so hits compare across
  nodes, and terrain-scale meshes are skipped by `MaxPickDiag`) and locks the camera onto the nearest
  mesh, which the `SpectatorCamera` then **orbits** (RMB rotates, wheel zooms, target stays centred as it
  moves). Auto-frame on Play locks onto the played def's anchor the same way. WASD/QE releases the lock.
- **Speeds** extended to `{0.1, 0.25, 1, 2, 4}` ("add a speed up to" → up to 4×).

**Playtest-round fixes (same day)**, from the user flying it: (1) LMB picking almost always hit the
terrain and zoomed out → `PickObject` skips meshes whose world-AABB diagonal exceeds `MaxPickDiag` (350 m),
so a click follows the object, not the map tile. (2) The follow became a proper **orbit-around-target**
(RMB rotates around the object like the static viewer's orbit camera, wheel zooms `_orbitDist`, WASD/QE
releases) instead of position-follow + free-look — the user asked for orbit behaviour on a lock; orbit
pitch is clamped to ~80° so `Basis.LookingAt` never gimbals. (3) **Typing a filter in the picker flew the
camera** — the camera polls raw key state, bypassing GUI focus; `SpectatorCamera.KeyboardCaptured`
(`GuiGetFocusOwner() is LineEdit or TextEdit`) now zeroes the keyboard axes while a text field has focus.
**Follow-up (same round):** the field then wouldn't give the keyboard *back* — Godot does not defocus a
`LineEdit` on a click into empty/3-D space — so a world click and every transport-panel button now call
`GuiReleaseFocus`; clicking the def list also hands control back (an `ItemList` isn't a text field, so it
doesn't gate). The keyboard stays captured only while you're actually focused in the filter.
(4) The transport panel **moved from top-left to just above the timeline**, where it no longer clashes with
the status line + key-hint. (5) Confirmed working by the user: pause toggle, follow-cancel, speed-up (minor
particle quirks at off-speeds, expected), ambient on/off (it stops without fully re-posing SI-driven nodes
— accepted, a data limitation of the reset states).

**Verified** (`.scratch/anim-lab-verify/`, git-ignored — game geometry): the overhauled UI renders (transport
panel with 1× active, freecam framed on the bowl, "following bowl" status, picker top-right); playing
`train_on_track` and capturing at t≈1 s and t≈15 s shows the camera **tracked the moving engine** across the
map (it drove from the open airfield to the trestle and stayed framed); a plain `--anim-lab --screenshot`
(no `--debug-anim-ui`) still renders an overlay-free frame; `--freecam` unaffected (the `SpectatorCamera`
follow additions are inert when `Follow` is null). Build clean. The dynamic controls only an interactive
run can exercise — the P/button pause toggle, the Ambient on→off click, follow-cancel on WASD — are
structurally in place for the user's playtest.

## 2026-07-23 — Anim-debugger Wave 5: stage placeless on-call defs in front of the camera (crash puffers visible)

**PLAN-anim-debugger Wave 5** landed, folding in a user request from the same session — "we can't
play most on-call animations because they have no anchor; would it be good to place them just in
front of the camera with an offset?" The answer turned out to be the crash plan's Wave 2a/2b
scaffolding, so both were delivered together (design confirmed with the user: *snapshot ahead of
camera*, *fold into Wave 5*).

**The discovery that shaped it.** An effect template hosts its puffers on its OWN root, not on the
`AtNode` the caller passes: `small_yellow_sparks`' `PufferState.at_node` is `yellow_spark_01`,
`call_crash_trails`' are `fly_trail1..5`. Those roots are world-gamez nodes `WorldBuilder`
deliberately skips, so playing `player_crash_dirt` fired every `CallAnimation` but every puffer hit
"no host node" and nothing rendered. Re-scoping the call (the existing `CallTargetAnchor`) is not
enough — the template root has to be MOVED to the call site. And a second trap: the crash def's
`healthy`/`destroyed` targets are generic (C1 ships 217 `healthy` nodes), so on the shared world
runtime they resolve GLOBALLY to arbitrary buildings — the effects staged onto a distant refinery.

**What landed (all additive, ambient world byte-identical — `PlaceCalledTemplates` default off, no
stage built outside `--anim-lab`):**
- `AnimRuntime`: `CallTargetAnchor`→**`CallTargetSite`** (now returns the `AtNode` offset too);
  **`PlaceCalledTemplates`** + **`PlaceTemplateAt`** (relocate a called template's own root onto the
  call site, gated + non-instant); **`IndexStage`** (index a post-bootstrap subtree + run its defs'
  reset states, no re-`Bind`); `Play(name, fallbackAnchor)` (a placeless def anchors to the fallback).
- `PlaneViewer` (`--anim-lab` only): `BuildEffectStage` (six effect-template roots from world gamez)
  + `BuildCrashAnchorSet` (a meshless `player`/`healthy`/`destroyed`/`piece*` set so the crash def's
  names resolve LOCALLY, in front of the camera) → one `labStage`, `IndexStage`d, `PlaceCalledTemplates`
  on, handed to the lab.
- `AnimLab`: repositions `labStage` 55 m in front of the camera on each fresh Play (snapshot reused on
  Restart), passes it as the fallback anchor, and frames/follows it when the played def resolves onto it.

**Verified (`.scratch/`, git-ignored — game geometry):** `--anim-lab --play-anim=player_crash_dirt`
renders the fireball + spark cluster centred in front of the camera (retargets resolve to the local
`healthy`/`destroyed`; was a bare terrain vista before); `--play-anim=large_10sec_fire` shows fire
that used to render nothing; the quiet stage (no play) is clean (templates hidden by their reset
states); `train_on_track` still frames + tracks its own moving engine (anchored def, ignores the
stage); C1 `--fly` boot census byte-identical to known-good (616 ON_STARTUP + 5 start anims, 615
instances, 4 puffers, 35 lights, 31 hidden). **Determinism boundary confirmed:** two same-seed crash
screenshots differ (puffer particle spread is `GD.Randf`, outside the runtime's seeded RNG — the
documented v1 boundary); the stage *placement* is deterministic. The piece ballistics + dust
opacity/scale dispatch-but-inert, awaiting `PLAN-data-driven-crash.md` Layer 1. Build clean.

## 2026-07-23 — Data-driven crash Layer 1 (Wave 1): generic ObjectMotion + ObjectOpacityFromTo handlers

**PLAN-data-driven-crash Wave 1** landed — the two reusable, generic animation handlers that make
the crash (and every M3 weapon-hit destruction) data-driven, added to `AnimRuntime` with **no crash
wiring yet** (Layer 2). Both are additive and trigger-gated, so the ambient world is unaffected.

**1a. `OBJECT_OPACITY_FROM_TO` → `OpacityFade`** — the single biggest un-handled event kind (9,917
events install-wide, zero prior `case`). A linear lerp of the two `opacity` numbers over `run_time`
through the existing `SetSubtreeOpacity` (`csky_opacity` per-instance param). Schema censused across
**all 9,917**: always `{name, opacity_from{opacity,state}, opacity_to{opacity,state}, run_time,
opacity_delta(null)}`. ⚠ **The endpoint `state` flag does NOT invert the value** — `(false,0)` fades
to invisible and `(false,1)` to opaque (the two dominant combos, 5073 + 4609) — so unlike
`OBJECT_OPACITY_STATE`'s "false→1.0" rule this is a literal opacity lerp; `opacity_delta` is null in
100% of them (the dead relative form).

**1b. `OBJECT_MOTION` ballistic/scale/tumble → `MotionRuntime`** — the rigid-body half the old
handler only counted. A full body seeded from the node's live parent-frame pose: `translation.initial`
= launch velocity (+`rnd_xz` spread through the runtime's **seedable** `_rng`, +`delta` velocity-ramp),
`translation_range` = ranged launch in a random azimuth (`vHoriz=xz/rt`, `vVert=y/rt−½·g·rt` — TUNE,
`initial`/`delta` unmapped), `gravity.value` accelerates, `forward_rotation.Time.initial` a tumble rate
about local X, `scale.initial/delta` a linear ramp. `do_intersections` ground-rest + `bounce_sequence`
re-launch deferred to Layer-1.5 (need a physics ray); the body integrates over `run_time` then finishes.
Integrator math ported from `CrashChoreography.StartDebris`/`CrashBreakup.Advance`.

**The safety invariant, censused:** the new `MotionRuntime` path activates only when
`translation`/`translation_range`/`scale`/`forward_rotation` is present, and **all 590 `ON_STARTUP`
`OBJECT_MOTION` events install-wide are pure `xyz_rotation` spins** — which stay on the untouched
lightweight `SpinMotion` path. So no boot event can hit the new path.

**A channel discriminator** was added to `IAnimMotion` (`MotionChannel {Transform, Opacity}`, a C#8
default-interface member so the 3 existing motions needed no change): `AddMotion` now evicts only a
prior motion on the **same channel**, because the crash `dust` carries a `MotionRuntime` (scale) AND
an `OpacityFade` at once and neither should displace the other.

**Verified.** A **true before/after** (git-stash baseline, rebuild, re-run) 8-chapter
`--freecam`/`--screenshot` census is byte-identical **except C3** — whose one `ON_STARTUP`
`OBJECT_OPACITY_FROM_TO` (`spiderweb_gone`, fading `spiderweb` 1→0 over 0.7 s so the web is gone by
default) is now handled: live motions 24→25 and it drops off the "not yet acted on" list. That is the
single ambient trigger the plan's reachability census predicted, and a free live sighting (verification
rule 5) — not a regression. In `--anim-lab --play-anim=player_crash_dirt` (`--seed=1`, fixed-dt,
`--debug-anim`): the five `fly_trail*` debris anchors integrate outward and the two carrying
`forward_rotation` tumble (fly_trail1 40.7°→81.3°, fly_trail3 142.3°→−75.3°) while the others hold rot 0;
`dust` appears twice in the motion dump (scale motion + opacity fade, both live on separate channels);
`flydirt` sinks on its `translation`; the rendered frame fans the debris fireballs across the C1 valley
around the central fireball/smokeball/spark cluster. Build clean, 0 warnings.

**⚠ Handed to Layer 2:** `Targets()` (state ops / `OBJECT_MOTION`) prefers the compiled `NodeRefs`
**index** and — unlike `ResolveOne` (`CALL_ANIMATION` retargets) — does **not** name-fall-back, so the
lab's meshless `piece1..4` anchors (no gamez index) leave the piece `OBJECT_MOTION`s unresolved even
though `healthy`/`destroyed` retarget fine by name. The piece launch is the same `translation` path
`flydirt` already proves; Layer 2's real `PlaneBuilder.BuildDestroyed` geometry must carry the matching
indices (or `Targets` needs the fallback). `CrashChoreography.cs`/`CrashBreakup.cs` stay as the working
fallback until Layer 2 is verified end-to-end.

## 2026-07-23 — Data-driven crash Layer 2 (Waves 2–3): the flight crash plays its def (`--data-crash`)

**PLAN-data-driven-crash Waves 2–3** landed, behind **`--data-crash`** (default off — the bespoke
`CrashChoreography`/`CrashBreakup`/`CrashEffect` trio stays the verified fallback and the A/B reference).
With the flag, `FlightController.Crash` PLAYS the compiled `player_crash_dirt` def on a **per-player
scoped crash `AnimRuntime`** (`PlaneViewer.BuildFlightCrashRuntime`): the def hides the airframe, shows
the `destroyed` wreck, launches the `pieceN` ballistics, and fires every authored effect (sparks, the
fireball cluster, the black smokeball, the dirt burst, the five burning-debris arcs) — all from the
extracted data through the Layer 1 `MotionRuntime`/`OpacityFade` handlers + the puffer machinery, the
same runtime M3 weapon-kills will reuse.

**Five new AnimRuntime capabilities, all additive + default-off (ambient census byte-identical):**
- **`NameResolveFallback`** — resolve every node ref by NAME and do NOT populate `_byIndex` at all. Two
  reasons, both measured: the crash def's ptrs are non-portable (`piece1..4` at planes.zbd indices
  7703–7706, OUT OF RANGE for the planes gamez — the def is generic across all 11 planes), and the
  scoped subtree MIXES two gamez index spaces that COLLIDE (fly_trail1 = world-index 400, the plane has
  a node at plane-index 400), so a shared index would misresolve an effect onto a plane part.
- **`AnimProgram.Subset(rootAnimName)`** — bind only the transitive CALL_ANIMATION closure (11 defs),
  not the 800+ world program whose ~150 generic-named defs (`healthy`/`destroyed`/lights) would anchor
  onto the plane's parts and run their reset states on the aircraft.
- **`Play(..., applyReset:false)`** — the crash TRIGGER must NOT re-apply RESET_STATE: the crash def's
  reset calls `player_destruction_reset` → `plane_reset`, which RESTORES the healthy panels (the
  opposite of a crash). Reset runs at bind and on respawn only.
- **`ResetToBaseState`** — the respawn: tear down every live instance + re-apply every anchored reset;
  the caller re-homes the flung wreck pieces (`CrashRestPoses`), which no reset event re-poses.
- **`InheritedWorldVelocity`** — a world-space momentum added to every ballistic launch (transformed
  into the launched node's parent frame), so the wreck pieces carry `WreckMomentum` (0.4) of the impact
  velocity and scatter along travel (the authored launch is a 5–10 m/s pop; the plane hit at 60–90 m/s).

**Two traps the build cost time on, both now documented.** (1) Bind AFTER the controller is in the tree
or the reset states read invalid global transforms (`is_inside_tree` spam). (2) **⚠ The crash puffers
must parent at WORLD level (`_worldRoot`), not under the per-player controller subtree** — a PUFFER_STATE
emitter goes `TopLevel` (world-space) when it emits, and under the controller it was drawn-but-unrendered:
every particle correctly positioned, `IsVisibleInTree` true, the multimesh drawing 462 instances, and
nothing on screen. World-level parenting (like the ambient runtime's own `PufferFactory`) renders it.
A/B against the nearest known-good path (the ambient waterfall splash, same `SustainAt`) is what proved
the mechanism sound and pointed at the parent — recorded in `docs/verification.md` §4.

**Two playtest TUNE calls (user, mid-session).** `forward_rotation.Time.initial` is a TOTAL angle over
run_time (crash pieces carry 5π/4.44π — clean π multiples), NOT a rate: read as a rate the pieces spin
~15 rad/s ("spins like crazy"); ÷ run_time gives 2.5 tumbles. And the momentum inheritance above, which
fixed "the pieces break apart but only drift slightly."

**Verified:** a forward-dive crash into C1 renders the fireball + sparks + smokeball + the tumbling
wreck + momentum-scattered pieces + burning-debris arcs; `--debug-anim` confirms the pieces launch, the
debris arcs fling outward, and the 9 emitters spawn 460+ live particles. Default (bespoke) flight and
`--anim-lab --play-anim=player_crash_dirt` unregressed; 8-chapter ambient census byte-identical to the
Wave 1 baseline (C1/C3/C5 spot-checked). Build clean.

**Two respawn regressions fixed same session (user-reported):** (a) pressing R left "just a
propeller" — the crash def hides `healthy`/`markers` per-node, and `PlaneModel.Visible=true` only
re-shows the root while the RESET_STATE restores `dontmove` alone (the original respawns a FRESH
plane); fixed by capturing the plane model's built visibility (`CrashPlaneVisibility`) and restoring
it on respawn. (b) The fire kept burning after respawn — `ResetToBaseState` tore down only *live*
instances, but `large_10sec_fire`'s instance ends while its 10 s puffer keeps emitting; rewritten to
hard-`Clear`+`QueueFree` the whole puffer/motion/light/sound pool (safe because the runtime is scoped
to one crash). Verified: a crash→auto-respawn cycle restores the whole airframe with no lingering fire,
and the next crash still spawns its 9 emitters.

**Wave 4 owed:** flip `--data-crash` to the default, delete the bespoke trio, scope the texture archive
to the session lifetime (the flag keeps it open past the build scope today), and close
`PLAN-M2-polish-4` item 8 — after the user's A/B playtest across planes/chapters.

## 2026-07-23 — Data-driven crash Wave 4: default flip + bespoke crash preserved on a branch

**PLAN-data-driven-crash Wave 4** landed, closing the plan (all four waves done) and closing
`PLAN-M2-polish-4` item 8. The data-driven crash is now the **default and only** crash path on `main`.

**The user's amendment.** The plan's Wave 4 said *delete* the bespoke `CrashChoreography`/`CrashBreakup`
trio. The user instead directed: *"don't delete the old crash animation — the breaking apart looked
better and this could be a point where we can improve on the original if we have finished recreating it
faithfully. Move those files into an extra branch."* So before any removal, branch
**`bespoke-crash-animation`** was created at the pre-Wave-4 `main` tip (`0d00b79`), where the bespoke
crash is fully wired as the default and `--data-crash` is the A/B toggle. The files were then removed
from `main` — *moved* to the branch, not deleted — as the visual A/B reference and the raw material for a
future "improve on the original" pass. Recorded as a feature-backlog item + a TUNE-list A/B pointer.

**What landed on `main`.**
- **Default flip.** Removed the `_dataCrash` field, the `--data-crash` arg (silently ignored now), and
  every `if (_dataCrash)` / `if (!_dataCrash)` branch. `BuildFlightCrashRuntime` now runs for every
  flown plane (gated only on the crash program/scene being available). `FlightController.Crash` plays
  `player_crash_dirt` unconditionally when `CrashRuntime` is set; the bespoke `else` arm is gone.
- **Removed the bespoke wiring.** Deleted `CrashChoreography.cs` + `CrashBreakup.cs`, the
  `CrashEffect`/`Breakup`/`Choreography` fields, their `Respawn`/`Crash`/`_PhysicsProcess` calls, the
  `crashEffects` `EffectSet.Load`, and the two per-player bespoke build blocks in `PlaneViewer`.
- **Relocated the `Surface` enum.** It lived in the deleted `CrashChoreography.cs`; moved to the top of
  `FlightController.cs` as a standalone `CrashSurface` enum (`ClassifySurface` still returns
  `CrashSurface.Ground`, the seam kept real for M3's air/water variants).
- **Scoped the session texture archive.** The `--data-crash` path had leaked it (a `using` skipped past
  the build scope with nothing owning disposal). Now non-lab builds hand it to `_sessionTextures`, which
  `ReturnToMenu` disposes on teardown (and the failed-build `catch` disposes) — a map reload drops the
  previous archive instead of leaking it. The anim-lab path (lab-owned `labTextures`) is unchanged.

**Verified.** A C1 nose-dive crash (`--hold=-1,0,0,1`, no flag) with `--debug-anim`: the log shows
`data-crash: 6 effect template(s) + 9 wreck node(s) — crash runtime bound` built **by default**, the
`CRASH into g27884/col` at 127 m/s triggers `player_crash_dirt` with all its effect defs retargeting
onto `healthy`/`destroyed`, and the puffer count jumps 4→**9 active / 72 live particles** at crash time
— proving the session-scoped archive stays open for lazy puffer baking. No disposal/leak/null-texture
errors, clean exit; the rendered `--shots` burst shows the broken-apart wreck + fireball + "CRASHED" HUD.
Build clean (0 warnings, 0 errors), which itself proves no dangling refs to the removed types.

**Still owed (user-gated):** the A/B playtest of the data-driven crash across planes/chapters (the
`bespoke-crash-animation` branch is the reference), and the `WreckMomentum` / `forward_rotation`-÷-run_time
TUNE calls.

## 2026-07-23 — Doc prune: architecture.md, verification.md, comment sweep, contract

All four waves of PLAN-doc-prune landed: architecture.md 306 KB → 51 KB (67 per-module `##`
entries, every kept constraint symbol-verified, format spillover merged into 7 docs/formats/
pages incl. the FORWARD_ROTATION and DzRadius corrections); verification.md 53 KB → 16.6 KB
(70 flat rules, each incident told once); the 226 provenance markers swept from 46 CSVM/src
files (comment-only diff, build clean); CLAUDE.md's routing rules now encode the shapes.
Verified by the plan's acceptance greps; one commit per wave.

## 2026-07-24 — M3 Wave A: weapon/destructible format docs (A1, A2, A4, A5, A6)

The documentation half of Milestone 3's Wave A landed: five `docs/formats/` deliverables plus
their index wiring, all verified against the extraction and correcting the plan's own survey.
- **`weapons.md`** — the 48-entry `weapons.zrd.json` `BALLISTICS` table: every key with measured
  range, the caliber×ammo damage matrix (dum-dum ½ armour/1.5× health, AP the mirror — exact on
  all 5 calibers), the AI detune (`wep_130`–`170`, velocity 600 / rate 6.0 / ½ damage), the
  `CLUSTER_SIZE` (per-slot) vs `AMMO_LIMIT` (purchase cap) split, and the `FIRE`/`FLYOUT`/`IMPACT`
  surface-class bindings. Found a **sixth `IMPACT` class, `quicksand`** (plan said five), refined
  the "no AMMO_LIMIT on rockets" rule (the six `CRATER` munitions do carry it), and documented 8
  keys the survey missed (`DAMAGE`, `HIGH_EXPLOSIVE`, `SONIC`, `BEEPER`, `BEEPER_SEEKER`,
  `TANGLER`, `REAR`, `SMOKE_SCREEN`).
- **`markers.md`** — the `planes.zbd` `markers` rig (8 firepoints + 8 pylons; Kestrel 7 with a
  centreline `fp7`), the `IDS_AIRFRAMEGUNGROUPNAMES` enum (3060–3079, contiguous; 3065/3078 =
  Center Guns / Center Guns 2), the per-airframe W1–W4 mount table, and the reverse-index binding
  **slot n → firepoint(9−2n),(10−2n)** — verified 4/4 on the Devastator (two-axis) and Peacemaker
  (asymmetric), Balmoral/Brigand duplicate coords reproduced. Corrected: the gun nodes
  `fgun`/`rgun`/`bgun0..3` are mesh-less markers, and `pdevastator` reuses `player_pfighter`.
- **`destructibles.md`** — `HEALTH` / `DAMAGE_SEQUENCE` / `ACTIVATION` model, `ANIM_HEALTH n` =
  `health <= n`, the water-tower worked example, and the 44 `WeaponOrCollideHit` set (39 C2
  facades + 4 C5 windows + `agyrobus`). Corrected: `unknown_seq` is **not** reliably the death
  sequence; `proximity_damage` is false everywhere; the plan's `AnimRuntime.cs` line numbers have
  drifted (re-locate by symbol).
- **`weapon-effects.md`** — the muzzle (`muzzle_burst`), impact (`gunhit`) and `*_control`
  ordnance effect readers, and the gamez projectile prototype roots. Of 57 `FIRE`/`FLYOUT`/`IMPACT`
  asset targets, 52 resolve + all 23 sounds; **5 are referenced-but-undefined** (`bld_damage.flt`,
  `rcochet1`, `call_small_flash`, `f18sparks2`, `flak_effectplayer`) — flagged for Wave D.
- **`vehicle.md`** — retired the "undecoded here" deferral: documented the `weapons` 5-tuple
  (player catalogue vs AI armament, position-5 engagement range 10000/800–900/500), `cannon_jam`,
  the AI `armor`/`health` pool, `turrets` (M4), `bullethole_anims`, and the AI-tuning key families;
  every key across all 75 vehicle defs now accounted for.

`README.md` Pages table + `zrdr.md` family index updated; the plan's checklist ticks A1/A2/A4/A5/A6
and carries a "Wave A landed — corrections" block. Docs only — no engine code changed. A1/A2/A4
were parallel sub-agents; A5's agent stalled and its page was written directly. **Next: A3 (the
`--dump-markers` tool) and A7 (the stock-loadout file, commit user-gated).**

**M3 Wave A item A3 — the marker reference tool (2026-07-24).** Landed the instrument the
`markers.md` mount tables regenerate from, in two front-ends over one extractor. `src/Mech3/MarkerRig.cs`
walks a `player_*` root in planes.zbd, accumulates locals down to each `firepoint*`/`pylon*`/`target`,
and reports plane-frame positions plus co-located groups (two gun groups on one physical mount).
(a) `--dump-markers[=plane]` prints a per-plane table — name, position, firepoint mirror pair,
`≡` shared mounts — to stdout and `./.scratch/markers_dump.txt`, then quits; needs no world or
camera, so `--headless` runs it windowless. (b) `src/UI/MarkerOverlay.cs` (`--viewer`, key K,
opened at launch by `--markers`) draws each marker on the parked plane as a coloured gizmo +
billboarded label — firepoints orange, **shared-mount firepoints magenta**, pylons cyan, target
green; every gizmo dot always shows so no position is lost, while the labels de-clutter
nearest-first (firepoints before pylons, co-located names stacked) exactly as `NodeLabels` does.
Verified: the dump reproduces `markers.md` **exactly** — Bloodhawk x-values, Devastator's
two-axis (height × spread) triples, Peacemaker's left/right-asymmetric layout, Kestrel's lone
centreline `fp7`, and both Balmoral (`fp1≡fp5, fp2≡fp6, fp3≡fp7, fp4≡fp8`) and Brigand
(`fp1≡fp4, fp2≡fp3, fp5≡fp8, fp6≡fp7` — co-location crosses the mirror-pair boundaries) shared
sets. The overlay's Bloodhawk labels agree with the dump; Balmoral/Brigand duplicates render as
visibly magenta co-located dots; screenshots of all 11 planes captured to `./.scratch/`.
One data correction folded back into `markers.md`: the `target` marker is **not** uniformly the
plane origin — seven airframes sit at (0,0,0) but the Bloodhawk (0,0,−1), Warhawk (0,+0.59,+0.67),
Firebrand (0,+1.08,−0.98) and Hoplite (+0.06,+0.04,+0.33) offset it (M4-scope; the "identity
transform" claim was overstated). `PlaneViewer` gained `--markers`/`--dump-markers`; docs updated
in `architecture.md`, `cli.md`, `markers.md`, CLAUDE.md's module index + viewer-keys line.
**Next: A7 (the stock-loadout file, commit user-gated), then Wave B.** A10 (in-engine
firing-placement check) waits on D29.

**M3 Wave A item A7 — the stock-loadout data file (2026-07-24).** Landed `CSVM/data/stock_loadouts.json`,
the 11 player aircraft's default weapon fit, plus `docs/formats/loadouts.md`. Established a new
committed-config home, `CSVM/data/` (hand-authored engine config, not extracted assets, loaded via
`res://` — the first such file; everything else is read from git-ignored `extracted/`). Per plane:
`guns[]` (slot, mount name verbatim from `IDS_AIRFRAMEGUNGROUPNAMES`, caliber, ammo, firepoint
markers), turret slots flagged `"turret": true` (inert in M3, decision 10), and `hardpoints`
(pylon count + stock `wep_06` HE). Seeded from the user's stock table (delivered 2026-07-22); the
`markers` arrays are the binding rule's (`slot n → firepoint(9−2n),(10−2n)`) explicit output,
checked in rather than runtime-computed so a wrong one is a visible data fix. Gun weapon ids
resolve `caliber N + ammo k → wep_{N+k}` (stock slug → `wep_N`). **Verified independently against
the committed file** (not the generator's memory): all 11 parse; every `markers` entry exists on
that plane's model; every marker matches the binding rule; every derived gun `wep_*` and each
`hardpoints.stock` resolves in `weapons.zrd.json`; the turret set is exactly the 5 airframes
(`pavenger`/`pbalmoral`/`pbrigand`/`pfirebrand`/`pkestrel`), the Balmoral the only two-turret one;
the Kestrel's W1 correctly resolves to the lone centreline `firepoint7`. The verify caught a real
authoring bug — an early draft resolved stock guns as `wep_N0` (`wep_300`) instead of `wep_{N+k}`
(`wep_30`) — exactly the visible-fix property the checked-in approach is for. No engine code
changed (B12 `Loadout.cs` is the wave-B reader). Docs: new `loadouts.md` + README index entry;
plan A7 ticked; CLAUDE.md repo-layout + status updated. **Wave A is now complete bar A10** (waits
on D29). **Next: Wave B — B11 (`WeaponDefs.cs`) blocks the rest, so it goes first.**

**M3 Wave B item B11 — the typed weapons.json reader (2026-07-24).** Landed `src/Flight/WeaponDefs.cs`,
the reader every other wave-B weapon handler reads a def from. `WeaponDefs.Load` types all 48
`weapons.zrd.json` `BALLISTICS` entries into `WeaponDef`s keyed by `wep_*` (plus the shared
`NO_AMMO_WARNING` empty-clip sound): ballistics, damage, allotment (`CLUSTER_SIZE`/`AMMO_LIMIT`),
the class flags (`CANNON`/`ROCKET`/`HIGH_EXPLOSIVE`/`TORPEDO`/`TARGETABLE`/…), the specials
(`BEEPER`/`TANGLER`/`SMOKE_SCREEN`/…), and the `FIRE`/`FLYOUT`/`IMPACT` bindings with `IMPACT` keyed
by a `SurfaceClass` enum over the six classes A1 established (`default`/`water`/`buildings`/`player`/
`enemy`/`quicksand`). `DESC` resolves through `Messages`. Modelled on `PlaneStats`. Two data
subtleties handled: flags are `KEY,null` pairs, so `ZrdrDict`'s bare-flag path (present-but-empty)
makes `Has(key)` the correct test; and `IMPACT` is walked as raw class/value pairs rather than
through `ZrdrDict`, so a class whose value is null (`enemy`, "no effect on that surface") is skipped
rather than read back as an empty binding. Added `ZrdrDict.Keys` so each def can report
`UnhandledKeys` — a tripwire that stays empty unless the data grows a key the reader hasn't learned.
Verified with a new headless tool, `--dump-weapons[=id|name]` (mirrors `--dump-markers`,
locale-independent output → `./.scratch/weapons_dump.txt`): **all 48 parse with NO unhandled keys**,
and the dump cross-checks A1's `weapons.md` — the `wep_50`–`53` damage matrix (slug 6.25/6.25,
dum-dum 3.125/9.375, AP 9.375/3.125, mag 6.75/5.75), the turret gun's `AMMO_LIMIT 9999` + no
`CLUSTER_SIZE`, `wep_06`'s HE fields, the torpedo's flags + `FLYOUT_HEALTH`, and resolved display
names. No session/flight/freecam path touched (the dump quits before any world builds). Docs:
architecture.md + CLAUDE.md module index (WeaponDefs), cli.md (`--dump-weapons`), plan B11 ticked.
**B12–B20 unblocked. Next: B12 (`Loadout.cs`) — read `stock_loadouts.json`, resolve markers against
a built plane, expose gun groups + hardpoints.**

**M3 Wave B item B12 — the loadout reader + marker resolution (2026-07-24).** Landed
`src/Flight/Loadout.cs` — placed in Flight, not Mech3 (it depends on `WeaponDefs`, so Mech3→Flight
would invert the layering; A7's stale `src/Mech3/Loadout.cs` pointers in `stock_loadouts.json` and
`loadouts.md` were corrected). Two layers: `StockLoadouts.Load` parses `CSVM/data/stock_loadouts.json`
(default `res://data/`, deliberately not under `--data-root` — it is committed engine config, not
extracted data) into per-plane `LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves
each gun slot's authored markers to live muzzle `Node3D`s (by `cs_name` meta, as MarkerOverlay reads)
and its caliber+ammo to a `WeaponDef` via `GunWeaponId` (`wep_{N+k}`), and each hardpoint to its
`pylonN`, yielding `GunGroup`s (independent ammo counters seeded from `CLUSTER_SIZE`) and
`Hardpoint`s (per-pylon `CLUSTER_SIZE`). Turret slots bind but carry `IsTurret` and are excluded from
`FirableGuns` (inert in M3, decision 10). A missing marker throws a loud error naming plane/slot/
marker — never a silent skip, since a silent one would fire a gun from nowhere. Verified with a new
headless tool `--dump-loadout[=plane]` (builds each plane, binds, reports gun groups + hardpoints):
**all 11 bind with every marker resolved.** The Balmoral reports **two separate .50 counters** (slot1
+ slot2 `wep_50`, 2000 each) plus its two inert turrets; the Bloodhawk's counters differ (`wep_40`
2400 / `wep_30` 2800) and its **9 total HE rockets (3 pylons × 3) reproduce the A9 playtest**; the
Kestrel's W1 resolves to the lone centreline `firepoint7`. The loud-error path was demonstrated with
`--dump-loadout=Kestrel --loadout=pbloodhawk` (the Bloodhawk loadout wants `firepoint8`, absent on
the 7-firepoint Kestrel) → `!! marker 'firepoint8' not found on the built plane`. `--loadout=<def>`
overrides which def binds (its flight effect lands with B16). No session/flight/freecam path touched
(the dump quits before any world builds). Docs: architecture.md + CLAUDE.md module index (Loadout),
cli.md (`--dump-loadout`/`--loadout=`), loadouts.md pointer fixed, plan B12 ticked. **B16/B17/B18
unblocked. Next: B13 (`Projectile.cs`) — a pooled projectile integrating VELOCITY/ACCELERATION/
GRAVITY, expiring at RANGE.**

**M3 Wave B/D — guns fire (the B13/B15/B16/D29/D30/D33 batch, 2026-07-24).** Landed the first playable
weapons: hold Space (gamepad B) and the plane's guns fire. Done as one batch because no piece is
testable alone. New `src/Flight/Projectile.cs` (`ProjectilePool`): a shared-world pool integrating the
data's ballistics (VELOCITY/ACCELERATION/GRAVITY, expiring at RANGE), inheriting launch velocity, with
a fixed array (no per-round allocation; the raycast query object is reused too). `Spawn` applies the
`CANNON_SPREAD` cone and flashes the muzzle. It renders three MultiMeshes — velocity-aligned tracer
streaks (D33, NOT billboarded, or they collapse to a screen-vertical bar), billboarded muzzle-flash
bursts (D29) and impact sprites — and plays the per-surface `IMPACT` sound (D30, the exact named effect
animations are the remaining depth). **Hit detection (B15)** is a per-step world raycast; the flying
plane has no physics body, so a round never hits its launcher and `player`/`enemy` are unreachable in
M3. Surface class (`default`/`water`/`buildings`) comes from the struck collider's new
`SceneBuilder.SurfaceMeta`, **stamped at build time in `AttachCollision`** from the mesh's dominant
material texture (water: `water*`/`wtr*`/`srf*`/`wakefront`; buildings: `hangar*`/`*build*`/`cblock`/…)
— data-driven, not a runtime name guess. **Gun firing (B16)** wires `Loadout` into `FlightController`:
each firable group runs its own `FIRE_RATE` clock, alternating muzzles so the group's total rate equals
FIRE_RATE, drawing from its own `CLUSTER_SIZE` ammo counter; a dry group sounds `snd_emptyclip` once;
respawn refills; turrets excluded (inert). `FlightAudio` gained the firing loop (LOOPED_SOUND_NAME) +
empty-clip. New flags: `--fire` (hold trigger, scripted runs), `--infinite-ammo`, and `--loadout=`'s
flight effect. An interim HUD ammo line stands in until E36. Verified (C1, `./.scratch/`): guns fire;
ammo depletes per group; the **Fury's 70-cal and 30-cal deplete 102:136 = a 6:8 ratio = their
FIRE_RATEs** (the per-weapon rate is honoured); `--infinite-ammo` shows `∞`; tracers + muzzle flash
render (streaks read against sky, wash a little over bright terrain — a visibility TUNE); rounds hit
**terrain → `Default`** and **sea → `Water`** with correct collider tags and impact sound. Remaining:
the named `IMPACT`/muzzle animations, per-ammo tracer textures, `buildings` (same code path as water,
not yet screenshot), the empty-clip drain (2000+ rounds — a playtest check), and tracer/impact visual
polish. **Owed playtest: firing feel.** Docs: architecture.md (Projectile) + CLAUDE.md module index +
in-flight keys, cli.md (`--fire`/`--infinite-ammo`), plan items 13/15/16/29/33 ticked (14/30 partial).
**Next: B17 (rocket/hardpoint firing — where B14's FLYOUT MODEL instancing lands), then B18 selectors.**

**M3 Wave B — rockets fire (B17, 2026-07-24).** Hardpoints now launch. `FlightController.UpdateRockets`
+ `NextArmedHardpoint`: the rocket trigger (**F** / gamepad **A**, `--fire-rockets` for scripted runs)
fires **one HE rocket per discrete pull** — a human pull fires once, only `--fire-rockets` auto-repeats —
drawn from the next pylon that still holds ordnance, **round-robin across the pylons**, gated by the
weapon's `FIRE_RATE` (1.0/s for every rocket = one launch per second). Each launch depletes that pylon's
own `CLUSTER_SIZE` counter; an all-empty pull sounds the empty-clip cue once; respawn refills. The pad-A
binding does not collide with pad-A respawn — respawn only fires from the crashed / run-complete screens,
early-return states this live-flight path never reaches. Rockets reuse the B13 `ProjectilePool` via the
same `Spawn` (VELOCITY 1200 / RANGE 1000 / no accel-or-gravity need no special path); `IsRocket` tints
them orange and `RocketStreakScale` fattens the streak as a stand-in until the `FLYOUT` `he_rocket` MODEL
mesh lands (B14, still ◐). New flag `--fire-rockets`. Verified: a headless stock-Bloodhawk soak launched
**exactly 9 `wep_06` (HE) rockets** — 3 pylons × `CLUSTER_SIZE 3`, matching the A9 playtest — cycling
`pylon1→pylon2→pylon3→pylon1…`, each depleting 3→2→1→0 independently, then stopping (dry); the infinite-ammo
run confirmed the 1 s cadence. (Headless framebuffer capture is unavailable in this build, so an in-flight
rocket screenshot is deferred to the owed playtest; per-pylon origin is proven by the launch log naming each
`pylonN`.) TUNE/playtest: the 1.0 s cooldown is the data's `FIRE_RATE` not a measured feel, and the F/pad-A
binding is a design choice — both flagged in `backlog.md`. Docs: architecture.md (FlightController +
Projectile), cli.md (`--fire-rockets`), CLAUDE.md (in-flight keys + status), plan item 17 ticked (14 still ◐).
**Owed playtest: does firing feel right — guns *and* rockets. Next: B18 (weapon selectors).**

**M3 Wave B — weapon selectors (B18, 2026-07-24).** Two independent selectors, `FlightController.CycleWeaponSelectors`
+ `_gunSel`/`_rocketSel`, both edge-detected and `--no-pads`-safe. **Gun selector** (G / gamepad
D-pad Left) cycles the firable groups; **hardpoint selector** (H / D-pad Right) cycles the distinct
loaded ordnance types. `--gun-select=N` (0-based) seeds the gun group for headless tests; selections
survive a respawn. **⚠ Design corrected mid-implementation (user, 2026-07-24): the original fires
only ONE gun group at a time — there is no ALL.** Decision 7's "(plus ALL)" is struck, and the
guns-batch behaviour of all groups firing at once (never playtested) is fixed: `UpdateGuns` now fires
only the selected group. The hardpoint selector is a no-op with stock loadouts (all pylons carry HE),
present and data-driven for when mixed loadouts land. Verified with a per-group first-shot breadcrumb:
headless Balmoral soaks (two `wep_50` groups) fire **only "gun group 1 (Inner Wing Guns)" by default**
and **only "gun group 2 (Outer Wing Guns)" under `--gun-select=1`** — exactly one group at a time,
the right one. Docs: architecture.md (FlightController), cli.md (`--gun-select`), CLAUDE.md (keys +
status), plan decision 7 corrected + item 18 ticked. **Next: B19/B20 (guided flight, ground lock-on).**

**M3 — guided flight (B19/B20) deferred to M4 (2026-07-24, user decision).** Plan decision 3 is
revised: guided ordnance is **out of M3 scope**, and **every rocket — the Seeker `wep_11` included —
fires as dumbfire**. The reasoning is a corrected model of the original's mechanics, supplied by the
user: the original has **no manual ground-target selection**; its auto-aim is game-handled and can
only be pointed at **enemy planes** (the same target-cycle as stunt-race objective selection). M3 has
no enemy planes, so nothing a guided missile could authentically lock exists — a ground-lock selector
would be an interaction the original never had. No firing-path change was needed: `UpdateRockets`
already spawns `hp.Weapon` into the ballistic `ProjectilePool` with no homing, so a Seeker on a pylon
already flew straight. Two data facts, verified from `weapons.zrd.json` and now documented
(`weapons.md`, `WeaponDef.IsGuided`): (1) **guidance is `TURN_RATE`, not a flag** — 13 of 14 rockets
carry the 0.001 sentinel (fly-straight), only the Seeker's 1.25 homes; (2) **`LOCK_ON` is universal**
(even dumbfire HE carries 1.3) because it is the auto-aim / lead-solution convergence time for every
weapon, not a steering promise — so it is *not* the guided discriminator. Groundwork kept for M4: the
`WeaponDef.IsGuided` convenience (`TURN_RATE > 0.01`) and the `weapons.md` "guided vs unguided" note.
Docs updated: PLAN-M3-weapons.md (decision 3, checklist 19/20, B19/B20 detail → deferral notes that
preserve the M4 design), CLAUDE.md status, weapons.md. Build clean. **Wave B's remaining in-scope work
is B14 (the `he_rocket` `FLYOUT` MODEL mesh) + D30/D32 polish; the next major thrust is Wave C
(destruction), starting with C21 (per-instance HP + destructible registry).**

**M3 Wave B B14 — rockets fly the `FLYOUT` MODEL body (2026-07-24):** rockets no longer render as an
orange stand-in streak; each launched rocket now instances the original's own projectile mesh. The
`FLYOUT` field of a `weapons.zrd.json` entry names a gamez prototype root (`MODEL`), and for the 15
`ROCKET` entries those are: `he_rocket` (BOOM/stock HE `wep_06`/`wep_24`), `ap_rocket` (ARMOR),
`incendiary` (9M/SEEKER/FW), `flak`, `sonic`, `flash`, `beeper`, `scatter` (CHOKER), `smoker`,
`a_torpedo` (TORPDO), `reararc` (FLARE), `aaflak` (AA FLAK) — 12 distinct roots, all present as named
nodes in every chapter's gamez (`he_rocket` confirmed 1 per chapter, C1–C5). `ProjectilePool` gained
the chapter world gamez + its `SceneBuilder` (threaded through the pool's ctor at the `PlaneViewer`
creation site) and, on a rocket `Spawn`, resolves `weapon.Flyout.Model` via `GameZ.FindByName` and
`SceneBuilder.BuildSubtree` **collision-exempt** (`collisionSkip: _ => true` — a rocket must not
obstruct another round or the world hit-test), caching the resolved node per model name; the built body
is freed on impact/expiry/`Clear`. **Orientation:** every rocket mesh is authored nose-along-(-Z)
(measured: `he_rocket`'s rendered LOD is mesh 66, 0.3 m dia × 1.5 m long, `z ∈ [-1.5, 0]`; all 12
distinct models share the −Z-nose convention), so `Basis.LookingAt(velocityDir)` — which aims local −Z
down its argument — points the nose along flight. **Measure-before-choosing (the plan's explicit
caveat): guns keep the tracer quad, rockets get a mesh** — a rocket lives ~0.83 s at `FIRE_RATE` 1/s
(`VELOCITY` 1200 / `RANGE` 1000), so ≤1 is ever alive per player, whereas a gun fires ~10/s living ~1 s
(dozens alive) → a mesh-per-round for guns would be wasteful against the existing MultiMesh tracer.
A rocket with a body now trails a slim exhaust streak (`RocketExhaustScale`); the old chunky
`RocketStreakScale` survives only as the fallback for a chapter missing the prototype. The `FLYOUT`
`MODEL_ANIMATION` smoke trail (`he_rocket` anim → the `he_effects`/`pufftrails` readers) stays deferred
to the D-wave. **Verified:** build clean (0/0); an 8-chapter (`C1`/`C1B`/`C1C`/`C2`/`C2B`/`C3`/`C4`/`C5`)
headless `--fire-rockets` flight regression — `he_rocket` instances (breadcrumb: 1 mesh) in **every**
chapter with **zero** real errors and unchanged gamez-node load counts; a runtime breadcrumb reading the
model's **applied** world basis back through its full parent chain reports **`nose·velocity = 1.000`**
(nose-forward, non-circular). Windowed captures over C1 (level, climbing, diving; in `./.scratch/`) show
rockets leaving the pylons and flying forward, impacting terrain ahead (`impact … -> Default`). A
pixel-crisp in-flight close-up remains an owed at-the-controls playtest (already listed for B17): a 1.5 m
projectile at ~1260 m/s is not chase-cam-photographable without its (deferred) smoke trail. Docs:
architecture.md (Projectile.cs entry), PLAN-M3-weapons.md (checklist 14 ☐→☑, `### B14` landed note),
CLAUDE.md status. **Wave B's remaining in-scope work is the D30/D32 named-effect polish; the next major
thrust is Wave C (destruction), starting with C21.**

**Per-instance mutable HP + destructible registry — M3 Wave C item C21 (2026-07-24):** the first
Wave-C landing gives the world's destructibles live, mutable hit points, replacing the static
`def.Health` read that had made every `ANIM_HEALTH` branch uniformly false (see the new
`DestructibleRegistry` + updated `AnimRuntime` entries in `docs/architecture.md` and the rewritten
`destructibles.md` "Engine status"). A new `src/Mech3/DestructibleRegistry.cs` records one
`Instance` — current HP, max HP, healthy/damaged/destroyed state — per `(def, anchor)` pair: every
`AnimDefinition` with `HEALTH > 0`, resolved to each world node its (wildcard) `NAME` binds, built
during AnimRuntime's bootstrap pass 1 beside the RESET_STATE application. `EvaluateCondition`'s
`AnimHealth`/`AnimHealthRange` now read the live instance value via `HealthOf(def, anchor)`, falling
back to the static `def.Health` for any unregistered pair (the exact pre-C21 read). **It applies no
damage (C23) and runs no death sequence (C24)** — nothing decrements HP, so every instance sits at
full health and the change is a *provable no-op*: `HealthOf` returns exactly `def.Health` everywhere,
so every `ANIM_HEALTH` verdict is bit-identical to before. **Keyed per `(def, anchor)`, not per def**
(the plan's ⚠): a wildcard `NAME` binds many node groups and each is an independent pool, so one
tower's future damage will not break its siblings. Verified: the full 8-chapter `--freecam`
regression runs clean (all exit 0, no exceptions, screenshots saved) with the registry count reported
per chapter — C1 267 instances / 196 node groups, C1B 108/108, C1C 107/107, C2 574/201, C2B 104/104,
C3 501/202, C4 225/194, C5 568/292 — sane against A4's census (2,603 health-defs across 61 archives).
Instances exceed node groups where the reader's wildcard def and the compiler's per-instance defs
both bind the same nodes (confirmed in C2: `fcpan**`/`grasshut#`/`sign*`/`police*` reader wildcards +
their compiled twins — object-specific, not over-matching); each carries its own pool, which **C23
must collapse to one authoritative instance per struck node (prefer the compiled def)** — recorded as
a ⚠ on the registry's architecture entry. Node/mesh counts are unchanged (C21 is purely additive,
post-world-build). Docs: new `DestructibleRegistry` architecture entry + `AnimRuntime` ⚠,
`destructibles.md` "Engine status" rewrite, PLAN-M3-weapons.md (checklist 21 ☐→☑, `### C21` landed
note), CLAUDE.md status. **Next: C22 (`DAMAGE_SEQUENCE` reader-front-end parsing + live threshold
evaluation) and C23 (`WeaponHit` damage application), both hanging off this registry.**

**DAMAGE_SEQUENCE parsing + live threshold evaluation — M3 Wave C item C22 (2026-07-24):** the
destructibles' progressive damage stages now run — a hit-worn tower smokes, then catches fire, at
the authored health thresholds (see the `AnimRuntime`/`AnimDefs`/`DestructibleRegistry` entries in
`docs/architecture.md` and the rewritten `destructibles.md` "Engine status"). Two pieces landed.
(1) `AnimDefs.cs`'s reader front-end now parses the `DAMAGE_SEQUENCE` block into a sequence
literally named `DAMAGE_SEQUENCE` — the same shape the compiled archives already deliver, and the
magic name the runtime invokes — closing the silent-drop gap (its `ParseDef` switch had no case for
it). (2) `AnimRuntime.ApplyDamageStages(instance)` runs that cascade against the instance's live HP
(C21's `HealthOf`): an IF/ELSEIF `ANIM_HEALTH` chain that fires the one effect for the stage the HP
now sits in. **It escalates via a per-instance `DamageStage` — running the cascade only when a new,
deeper threshold is crossed** — which is what makes "each stage once" hold for *every* effect kind:
the sustained smoke loop would be spared re-firing by `CALL_ANIMATION`'s own live guard, but a
one-shot damage effect that finishes (C5's `damage3_mp1zreng11`) is not, and re-fired on every step
before the gate was added (the bug that surfaced the need). **Nothing calls `ApplyDamageStages` in
normal play yet** — C23's `WeaponHit` path will, after decrementing HP; today the new `--damage-test`
flag drives it. Verified with `--damage-test[=name]` (a new headless C22 verifier standing in until
F40's interactive HP control): sweeping a destructible's HP full→zero and logging which stage effect
fires at which health. The water tower (HEALTH 60, both compiled *and* the reader-parsed twin) fires
black smoke at HP≤36 and fire smoke at HP≤18 (0.60/0.30 × 60); C1's HEALTH-30 AA guns fire at 18/9
and HEALTH-60 buildings at 36/18 — proving the thresholds are absolute values derived per-def from
each object's own `HEALTH`, evaluated against live per-instance HP; C5's `reng11` (HEALTH 40, the
three-stage {0.85,0.50,0.25} progression) fires damage3→damage2→damage1 (each chaining its own
smoke/fire) once each at HP≤34/20/10, with the re-fire gone. The full 8-chapter `--freecam`
regression is **byte-identical to the C21 baseline** — same destructible counts (C1 267/196 … C5
568/292), same node/mesh counts, all exit 0, no exceptions — confirming C22 is a no-op at world
build: `ApplyDamageStages` runs only under `--damage-test`, and the reader-parsed `DAMAGE_SEQUENCE`
sequences are inert because `WeaponHit` defs never bootstrap. Also recorded a verification.md rule
(71): `--screenshot` needs a real GPU context — `--headless` selects the dummy renderer whose null
texture readback throws, writing no file. Docs: `AnimDefs`/`AnimRuntime` ⚠ + expanded
`DestructibleRegistry` architecture entries, `destructibles.md` "Engine status" rewrite, `cli.md`
`--damage-test`, PLAN-M3-weapons.md (checklist 22 ☐→☑, `### C22` landed note), CLAUDE.md status.
**Next: C23 (`WeaponHit` activation + damage application) — decrement HP on a projectile hit and
call `ApplyDamageStages`, then C24's death sequence at zero.**

**WeaponHit activation + damage application — M3 Wave C item C23 (2026-07-24):** the player can now
shoot the world down — a projectile hit spends the weapon's `HEALTH_DAMAGE` on whatever destructible
it struck, runs the damage stages, and kills the object at zero HP (the visible death swap is C24).
Wiring: `ProjectilePool.Impact` (B15's raycast already reports the struck collider) invokes a new
`DamageSink` delegate, wired in flight to `AnimRuntime.DamageAt(struck, healthDamage)`.
`DamageAt` resolves the collider to its destructible via `DestructibleRegistry.Resolve`, subtracts
`HEALTH_DAMAGE` (world destructibles carry HEALTH only — there is no armour pool, so `ARMOR_DAMAGE`
is inert against them, the damage model the plan settled 2026-07-22), calls `ApplyDamageStages`, and
sets `State.Destroyed` at zero. **The resolution was the crux.** A struck collider is a `StaticBody3D`
deep under the anchor's subtree, so `Resolve` walks up the parent chain — but it must walk the WHOLE
chain and prefer the **compiled** def, because a reader wildcard `NAME` grabs an inner node the
compiled def does not: the tower's reader `ap_h2otwr*` matches `ap_h2otwr.flt`, which sits between the
collider and the compiled `ap_h2otwr1` root, so "first anchor up the chain" wrongly picked the reader
twin (an independent HP pool with no death sequence). Taking the nearest compiled anchor fixes it —
`_authoritative` keeps one compiled-preferred instance per node. **Patrol-boat ⚠ resolved:** its anim
def is HEALTH 20 `WeaponHit` (mission archives only, not the freecam IA1 chapters), so M3 damages it
as scenery through that path; the AI-vehicle armour+health model (vehicle-def HP 40) stays M4.
Verified with the new `--damage-hd=<n>` mode of `--damage-test` (discrete weapon hits via `DamageAt`,
counting hits to destruction) — decisive and deterministic: a HEALTH-60 water tower dies in **1** hit
at HD 60 (HE rocket), **2** at 40 (AP rocket — measurably worse against a building, exactly inverting
AP's anti-armour advantage), and **14** at 4.5 (40-cal gun), crossing black smoke at hit 6 (HP 33) and
fire at hit 10 (HP 15); a HEALTH-30 AA gun dies in **10** hits at 3.0 (30-cal), stages at 18/9. A
`resolve✓` check (resolving from a deep descendant, the same node path a collider sits in) passes on
**all 16** C1 `DAMAGE_SEQUENCE` destructibles. The full 8-chapter `--freecam` regression is
byte-identical to the C21 baseline (same counts, no errors) — C23 is a no-op at world build
(`DamageAt` only fires on real hits). An in-flight `--fly --fire` run over C1 confirmed the wiring
end to end: `DamageSink` is invoked on every real impact and correctly no-ops terrain/water (no
spurious damage). **Owed playtest:** watching a specific destructible take fire and die in interactive
flight — the airport structures are placed by baked node transforms this session did not decode into
aim points, and the *visible* death is C24's death swap, so this naturally pairs with C24 (added to
`playtest.md`/`backlog.md`). Docs: `AnimRuntime`/`DestructibleRegistry`/`Projectile` architecture
entries, `destructibles.md` "Engine status" rewrite, `cli.md` `--damage-hd`, PLAN-M3-weapons.md
(checklist 23 ☐→☑, `### C23` landed note), CLAUDE.md status. **Next: C24 (death sequence execution +
healthy→destroyed swap) — run `destroyit`/the death sequence when `DamageAt` reaches zero.**

**Death sequence execution + healthy→destroyed swap — M3 Wave C item C24 (2026-07-24):** shot
destructibles now die — at zero HP the object swaps to its wreck, its smoke/fire keeps burning, and
its puffers fire. `AnimRuntime.DamageAt` (C23), on the transition to `Destroyed`, calls a new
`RunDeathSequence` that plays the def's death via **`Start(def)`** — the def's own Initial sequences
ARE the destruction (its `anim_name` is `h2twr_destruction1`/`destroy_mp1zreng11`): the
healthy→destroyed `OBJECT_ACTIVE_STATE` swap, the debris sequences, and the puffer calls. **The key
finding was that the death swap has no fixed name** — a census of ~100 C1/C5 destructibles found the
swap sequence named `destroyit` (25), `destroy_h2twr` (4), `destroy_twr`, `litehouse_des`, or
*unnamed* (56), and A4's ⚠ already ruled out `unknown_seq` — so rather than pick "the death
sequence" out, `Start` plays them ALL (they are always `seq_state=Initial`, 90/90), which reaches
every naming. The `DAMAGE_SEQUENCE` among them just re-fires the final smoke stage idempotently, as a
one-shot kill wants. **A second finding drove the fallback:** ~10 of the 100 defs (the C1 AA guns
`aagun32`–`36`) declare the healthy/destroyed node pair but author NO swap sequence, so `Start` alone
left them standing. `ApplyDeathSwap` derives the swap from the def's **own RESET_STATE** — flipping
the `healthy`/`destroyed`/`dbase` roles that base state explicitly named — applied only when RESET
declares a `destroyed` node, so an object with no destroyed variant (`noseballgun`, which RESET shows
has none; the fuel trucks, whose RESET is empty) is left intact, not blanked. This reads the def's
explicit OBJECT_ACTIVE_STATE targets (A4's prescribed method), never a world-wide name scan (the
`ref_tank_dest` trap), matches the exact role words (not a `_dest` suffix), and runs per-instance on
real death — so it cannot fight the bootstrap safety net, which only runs at load. Verified via
`--damage-test --damage-hd=` (extended with a `swap[healthy…, destroyed…]` check on the killed
instance): the water tower (explicit `destroy_h2twr`), the AA gun (RESET fallback), and every one of
the **16 C1 DAMAGE_SEQUENCE destructibles** end `healthy 0/1 shown, destroyed 1/1 shown` — healthy
hidden, wreck shown; `reng11` hides its healthy subtree and stages its separately-`CALL_ANIMATION`'d
`mp1reng_destroyed.flt` wreck + `large_fireball`; `noseballgun` dies without being blanked; the broad
C1 sweep kills all 16 with zero errors. The 8-chapter `--freecam` regression is byte-identical to the
C21 baseline (same counts, no errors) — C24 is a no-op at world build (death runs only on real
hits). **Still stubbed:** the ballistic `OBJECT_MOTION` that flings wreck pieces (C26) and the
one-shot death `Sound` (D31), so the wreck appears and smokes but the debris does not yet tumble and
the explosion is silent; the in-flight *visual* of a specific object dying remains the C23 owed
playtest. Docs: `AnimRuntime` architecture entry, `destructibles.md` "Engine status" rewrite, `cli.md`
`--damage-hd` swap check, PLAN-M3-weapons.md (checklist 24 ☐→☑, `### C24` landed note), CLAUDE.md
status. **Next: C25 (collider removal on destruction — the doors) or C26 (ballistic ObjectMotion
debris); C26 is independent and wakes a path skipped since M2.**

**Collider removal on destruction — M3 Wave C item C25 (2026-07-24):** destroyed doors, gates and
buildings stop blocking flight — and it turns out this needed **no new runtime code**, because C24's
death swap already does it. The `OBJECT_ACTIVE_STATE` swap runs through `SetSubtreeActive`, which
toggles the subtree's `CollisionShape3D.Disabled` (`SetCollidersEnabled`) *alongside* its
`Visible` — so the same swap that hides the healthy geometry also un-solids it, and the wreck it
reveals becomes solid. The item was written (before C24 landed) anticipating separate collider code;
the swap made it free. **What C25 actually delivers is the proof and the traps.** The `--damage-hd`
harness gained a `col[off N, on M]` census — the world colliders a kill switches off (the healthy
door/building) vs on (the wreck + any chained animation). Measured: C2 (Hollywood) `gate1`/`gate2`
(the studio doors) kill with **off 1, on 8**; `kkgate` off 4, on 12; C1 `m_build01` off 1, on 10; the
C1 AA gun off 2, on 1 — every destructible removes its healthy collision on death. **Two measurement
traps cost the most and are now `verification.md` rules 72/73:** (1) colliders exist ONLY in the
flight build (`WorldSession.Options.Collision = _fly`), so the first census — run under
`--damage-test`, which is freecam — found "0 world colliders" and nearly concluded destructibles were
non-collidable; forcing `Collision = _fly || _damageTest` revealed 1848 in C2, doors and the propane
tank among them. (2) A *net* collider delta (`+8` for `kkgate`) hides the healthy removal behind the
larger wreck it adds — the off/on split is mandatory. **The propane→door chain is confirmed handled:**
Hollywood's `kkgate` is a WeaponHit destructible whose root is the collidable (shootable) `propane`
tank, HEALTH 10; shooting *it* runs the gate's death — the healthy→destroyed swap (collider off) plus
`CallAnimation` to `genx12`, `tbridg1_fire`/`tbridg2_fire` (the bridges catch fire) and
`free_the_goose`; the door itself is not directly damageable, exactly as the original plays it.
(`sghangar-opensgdoors` is a *different* HEALTH-0 OnStartup open, not the propane target.) The
discrete-hit harness was also broadened to cover EVERY destructible, not only `DAMAGE_SEQUENCE`-carrying
ones — the doors instant-die with no stages, so the old gate would have hidden C25's own cases. The
8-chapter `--freecam` regression is byte-identical to the C21/C24 baseline (the `Collision` change is
gated on `_damageTest`, so freecam builds no collision as before). **Owed playtest:** the destroyed
variant re-adds its own colliders, so whether a blown-open door leaves a clear passage is the original
data's call — fly through a killed door in flight to confirm (the same in-flight aim the C23 playtest
owes). Docs: `destructibles.md` "Engine status", `architecture.md` (`AnimRuntime` + `PlaneViewer`
entries), `verification.md` rules 72/73/74, `cli.md` `--damage-hd`, PLAN-M3-weapons.md (checklist 25
☐→☑, `### C25` landed note), CLAUDE.md status. **Next: C26 (ballistic ObjectMotion debris tumble —
independent, wakes a path skipped since M2) or C27 (the WeaponOrCollideHit collision path).**

**Ballistic ObjectMotion — debris tumble — M3 Wave C item C26 (2026-07-24):** the wreck pieces fly.
Like C25, this needed **no new runtime code** — and the plan's premise ("`AnimRuntime.cs:429`
implements only the spin half and skips the ballistic half") was already outdated: the M2 crash Layer 1
work (`ec8a731`) generalized that into `MotionRuntime`, a full ballistic rigid body handling gravity,
a `translation_range` ballistic arc, a `forward_rotation` tumble, a `scale` ramp and `run_time` — the
exact list C26's Approach asks for. It is REACHED on a weapon-hit death because the death's
`OBJECT_MOTION` events sit in `Initial` sequences, so C24's `Start(def)` runs them. **The C24 HISTORY's
"debris ballistic OBJECT_MOTION (C26) still stubbed" was a measurement artifact, not a real gap:** the
launch is SCHEDULED mid-sequence (the water tower's `h2twr_middle` at t=2.2 s), and the C24/C25
kill-and-check never advanced the animation clock — so it saw `debris[0]` and read as stubbed. In real
gameplay `_Process` ticks the clock to 2.2 s and the pieces launch. Proven by advancing the death in
the harness: the water tower launches **2** visible pieces (`h2twr_middle` arcs from y≈5.3 to y≈19.2 in
0.8 s, `vis=True`, `run_time` 5 s; `h2twr_upper` likewise), C1 buildings **7** each, passenger planes
**2**; deaths that author no `OBJECT_MOTION` (`air_gen`, the AA guns) correctly launch **0**. C26's
deliverable is the durable instrument + the correction: a `BallisticMotionsLaunched` counter on
`AnimRuntime`, and the `--damage-hd` harness now reports `debris[N launched]`. To measure it the harness
adds the world subtree to the tree (with `ManualAdvance`, so `_Process` doesn't double-drive) and
`Advance`s the death ~3.5 s AFTER the swap/col census — ticking an out-of-tree world spammed
`!is_inside_tree` (global-transform reads), and the swap/col numbers must stay the immediate
post-death state (C25's `col[off,on]` are unchanged: `m_build01` off 1/on 10, `aagun32` off 2/on 1).
New `verification.md` rule 75 (a synchronous kill-and-check reads only t=0 effects; scheduled effects
need the clock advanced, in-tree). **Still deferred:** the debris `do_intersections`/`bounce_sequence`
ground-rest (a Layer-1.5 physics-ray follow-up — the pieces arc, tumble, and are then hidden by the
sequence's own `OBJECT_ACTIVE_STATE`, so they read fine without it) and the death `Sound` (D31). The
8-chapter `--freecam` regression is byte-identical to the C21/C24/C25 baseline (the harness changes are
gated on `_damageTest`; the ballistic counter is zero at bootstrap). Docs: `destructibles.md` "Engine
status", `architecture.md` (`AnimRuntime` + `PlaneViewer`), `verification.md` rule 75, `cli.md`
`--damage-hd`, PLAN-M3-weapons.md (checklist 26 ☐→☑, `### C26` landed note), CLAUDE.md status. **Next:
C27 (the 44 `WeaponOrCollideHit` collision path — facades/windows break on contact) or C28
(destructible reset/restore, for the debug tools).**

**The 44 WeaponOrCollideHit collision path — M3 Wave C item C27 (2026-07-24):** flying into the
Hollywood facades, the warehouse windows and `agyrobus` now breaks them and the plane passes through;
ramming anything else (water towers, gates, signs) still kills the plane and leaves the object intact.
`ACTIVATION` is the switch. `SweepAirframe`/`HitWorld` (`FlightController`'s swept-box + center-ray
collision probes) now also out the struck `Node`; before the crash/graze decision, the controller
offers the hit to a new `CollideDamageSink` → `AnimRuntime.CollideDamageAt`, which resolves the node to
its destructible (`Registry.Resolve`) and gates on `def.Activation`. A `WeaponOrCollideHit` instance —
the **44** collide-destructibles (C2 `fcpan01`–`39`, C5 `w_win01`–`04` at health 0.01, C5 `agyrobus`
at 70; verified as exactly 44 across cam_anim) — takes `vn × 8` HEALTH_DAMAGE through the SAME
`DamageAt` a weapon spends (so the object's death — the healthy→destroyed swap, the debris tumble, the
collider removal from C24/C25/C26 — is identical whether shot or rammed), and the hit is CLEARED so the
plane keeps its full-motion pose and flies through. A `WeaponHit` object returns false and stays solid,
so the crash/graze path runs unchanged — ⚠ decision 6 upheld: collision damage is **not** extended to
`WeaponHit` set dressing (their 0.01-vs-real health is the tell). The `vn × 8` scale means a real
flight-speed hit (vn ≥ ~9 m/s) breaks even `agyrobus` (70), while the 43 windows/facades (0.01) shatter
at any motion; a stationary kiss (vn ≈ 0) breaks nothing. Verified headlessly with a new `--damage-hd`
`collide[✓/✗, ACTIVATION]` probe (reset the instance, apply a collision, report accept + destroy): the
C2 facades, C5 windows and `agyrobus` all `collide[✓ broke, WeaponOrCollideHit]`; the C2 signs and
`kkgate` `collide[✗ ignored, WeaponHit]`. The 8-chapter `--freecam` regression is byte-identical to the
C21–C26 baseline (the collide path is `--fly`-only; `CollideDamageAt` is never called at world build).
**Owed playtest:** the in-flight feel — flying through a facade cleanly (it breaks, plane survives) vs.
flying into a water tower (plane dies, tower stands) — the same aim the C23 playtest owes. Docs:
`destructibles.md` "Engine status", `architecture.md` (`AnimRuntime` + `FlightController`), `cli.md`
`--damage-hd`, PLAN-M3-weapons.md (checklist 27 ☐→☑, `### C27` landed note), CLAUDE.md status. **Next:
C28 (destructible reset/restore — re-apply RESET_STATE, restore HP + colliders, for the debug tools),
the last Wave C item.**

**Destructible reset/restore — M3 Wave C item C28 (2026-07-24) — Wave C complete:** a destroyed object
can be returned to healthy, for the debug tools (F40/F41) and respawn. `AnimRuntime.ResetDestructible`
is the death's inverse, in four steps: (1) `Stop` the def's live death, tearing down its
motions/puffers/fires; (2) `RestoreRestPoses` — restore the authored pose of any node the death
physically MOVED, i.e. the ballistic debris pieces (C26): `Stop` removes the motion but leaves the
piece wherever it flew (y≈19 for the water tower's middle section), so a re-destroy would launch from
the wrong place — `_rest` already holds each moved node's rest transform (MotionRuntime records it on
launch), and the membership check confines the restore to nodes that actually moved, leaving the
visibility-only healthy/destroyed nodes to RESET_STATE; (3) re-apply the def's `RESET_STATE`, whose
`OBJECT_ACTIVE_STATE` base states make the healthy subtree visible+collidable and hide the destroyed
one (`SetSubtreeActive` restores colliders with visibility, C25), undoing both the C24 swap and the
`ApplyDeathSwap` RESET-derived fallback; and (4) restore the instance's HP/Status/DamageStage (the C21
fields). Idempotent by construction — destroy→reset→destroy produces identical results. Verified with a
new `--damage-hd` `reset[…]` check (kill, reset, re-kill, compare): across **C1/C2/C5** every
destructible type returns `healthy=✓` (healthy shown, destroyed hidden) and re-kills in the SAME hit
count as the first kill — buildings/towers with debris (`m_build01` 2h, `ap_h2otwr1` 1h), passenger
planes (1h), `air_gen` (2h), the AA gun `aagun32` whose death used the RESET-derived swap fallback
(1h), the doors `gate1`/`gate2` with their rotated-open leaves restored (1h), the propane `kkgate` with
its CallAnimation chain (1h), the C2 facades `fcpan*` (WeaponOrCollideHit, 1h), and C5's substantial
`agyrobus` (health 70, 2h). Objects with no NAMED healthy/destroyed node (the signs, the facades) still
reset cleanly — HP+status restore and the re-kill is idempotent. The 8-chapter `--freecam` regression
is byte-identical to the C21–C27 baseline (`ResetDestructible` is only called from the harness/debug
path, never at world build). **This completes Wave C (destruction):** objects now damage (C21–C23),
die (C24), lose+gain collision (C25), throw debris (C26), break on contact (C27), and reset (C28).
**Still deferred out of the wave:** the death `Sound` (D31) and the debris `do_intersections`/
`bounce_sequence` ground-rest (a Layer-1.5 physics-ray follow-up). Docs: `destructibles.md` "Engine
status" (Wave C complete), `architecture.md` (`AnimRuntime`), `cli.md` `--damage-hd`, PLAN-M3-weapons.md
(checklist 28 ☐→☑, `### C28` landed note), CLAUDE.md status. **Next: Wave D (weapon effects) — D29
muzzle flash, D30 tracers/impacts polish, D31 death audio — and the owed in-flight playtests.**

## 2026-07-24 — M3 Wave D D30: impact effect models (water splash) + puffer-effect finding

The per-surface `IMPACT` **effect animation** is now wired at the hit point, not just the sound.
`ProjectilePool.Impact` resolves the struck surface's `ANIMATION`/`SURFACE_ANIMATION` and, when the
name IS a chapter-gamez model root, `SpawnImpactModel` instances that prototype's subtree at the
point (reusing the flyout `GameZ`/`SceneBuilder`, collision-exempt, upright, freed after 0.4 s) and
suppresses the stand-in spark — the authored model is the effect. In practice this is the **water
splash**: gun `splash1.flt` and HE `bsplsh.flt` each instance 2 meshes, verified reproducibly on
C1B/C2B (both dive-reliably over water). Names that resolve to a reader/control def (`3040slug_gunhit`,
`he_ground_effect`, `large_fireball`) or are undefined (`bld_damage.flt`, `rcochet1`, `call_small_flash`,
`f18sparks2`, `flak_effectplayer`) name no root, so nothing instances and the spark stands in — the 5
undefined names thereby **confirmed inert** (no crash) on C4/C5 building hits.

**Finding (the plan's "Puffer.cs already implements puffer emission" premise, corrected):** the
**puffer/particle** half of the named impact effects (the `gunhit` `blacksmokepuffer` smoke, the
fireball puffs) does **not** render at runtime in flight, because the puffer factory + `TextureArchive`
are torn down after the world build (`KeepArchivesOpen` is `--anim-lab`-only). Rendering them needs a
dedicated world-effects runtime that keeps textures open and relocates the effect templates onto the
hit point — exactly the pattern the per-player crash runtime already proves (`BuildFlightCrashRuntime`:
live `PufferFactory` + `PlaceCalledTemplates` + `BuildEffectStage`). That machinery is shared with
**destruction effects (D32)** (the same `CallAnimation`/puffer templates), so the impact-puffer wiring
folds into D32; D30 lands the model-based effects + sound + spark.

Verified: build clean; guns+rockets dived-and-fired over all 8 chapters with no error tracing to
`Projectile.cs`; water → `splash1.flt`/`bsplsh.flt` instance (2 meshes each); terrain/buildings →
spark, no crash; the 1-instance ObjectDB exit leak reproduced on the no-fire baseline (pre-existing).
8-chapter `--freecam` regression err=0 with baseline mesh counts (the pool is flight-only, so freecam
is untouched by construction). **Owed playtest:** the on-screen look of the water splash at speed.
Docs: `weapon-effects.md` (Engine wiring D30 + 5-name inert confirmation), `architecture.md`
(`Projectile.cs`), `verification.md` rules 76 (runtime puffers dead in the flight build) + 77
(`CANNON_SPREAD` non-determinism in impact tests), PLAN-M3-weapons.md (checklist 30 ◐→☑, `### D30`
reconciled, `### D32` expanded), `playtest.md`, CLAUDE.md status.

## 2026-07-24 — M3 Wave D D31: the one-shot `Sound` anim event + `SOUND_GROUPS` decode

**What landed.** The one-shot `SOUND` animation event — the fire-and-forget destruction/damage/impact
audio a sequence emits (`air_mixed_exp_sg` when a building is struck, `snd_gasbagexp1` on a zeppelin
kill) — now plays. Previously `Sound` fell through `AnimRuntime`'s `default` case and was only counted.
Distinct from `SOUND_NODE` (the pooled looping ambient emitters, landed earlier): a one-shot plays
once at a world point and disposes itself.

- **`SoundGroup` + `SoundDefs.LoadGroups`** decode the `SOUND_GROUPS` block (docs: `sounds.md`).
  A group is a weighted member set; `Pick(rng)` is weighted-random with a recency scalar —
  `DYNAMIC_WEIGHTS factor` (0.5 everywhere) halves the last pick's weight so a variant does not
  repeat back-to-back (that is what the bare `0.5` after the token decodes to). Explicit-`WEIGHT`
  groups (`snd_plane_die`/`snd_plane_dmg`, `snd_nothing` at 0.7) get per-member weights and no
  recency; VO dialogue chains (`snd_assignments`, `snd_HI1*`) contribute no weighted member and are
  skipped; music `*_sg` groups parse but no `SOUND` event names them.
- **`WorldSounds.PlayOneShot(name, worldPos, rng)`** resolves a group→member first, then plays a
  throwaway `AudioStreamPlayer3D` at the point. Registered in `_oneShots`, swept in `Tick` once it
  stops (no reliance on the `Finished` signal); `FlushOneShots` frees them for the synchronous
  damage-test harness, which pumps no frames.
- **`AnimRuntime.HandleSound`** dispatches the event: NAME is a sound *definition* or a group, never
  a gamez node (the recorded C3 gotcha — the lone reader-scope one-shot names `snd_waterfall`, a
  definition, `targets=0`); position is the AT_NODE (`{name,pos}` compiled / flat `at_node`+`translate`
  reader), or the anchor. `OneShotSoundsPlayed` counts successful plays for headless verification.
- **Prewarm** now covers one-shot names too (`AnimProgram.OneShotSoundNames`, group names expanded to
  members in `WorldSounds.Prewarm`), decoded quietly (`SoundArchive.Find(…, warn:false)`) so a
  per-chapter archive legitimately lacking a referenced WAV (`hanger_door.wav` in C4) does not warn —
  the authoritative "silent for the session" report stays at the point of use.

**Verified.** Build clean, 0 warnings. `--damage-test --damage-hd=60` across **all 8 chapters**:
`Sound` dropped off every "not yet acted on" list (was counted, now handled); death sounds play —
switchhouse (the `air_mixed_exp_sg` worked example) `snd[2 played]`, C1 52, C2B 73, C3 4, C5 136 across
the sweep. `--debug-anim` shows the emitters: `sound one-shot: air_mixed_exp_sg → snd_exp_hit1/2/3 @
(-9096,774,-5637)` — the group resolves to *different* members (recency diversifying the picks) at the
`dbase` node. One-shot cleanup is leak-free (C4, 26+ plays on the helium tanks → 0 ObjectDB leak with
`FlushOneShots`). Frame-pumped `--freecam` C1 clean (38 ambient emitters unchanged, no per-frame
errors, `Sound` handled). All residual log noise is pre-existing and non-sound: C2/C3 `det == 0`
degenerate transforms (present in the `--mute` baseline too), C2B `clutter template not found`,
`!is_inside_tree()` harness reads, the non-deterministic 0–6 ObjectDB exit leak, and the
`timeout`-SIGKILL CLR error on a killed freecam.

**Docs.** `sounds.md` (new `SOUND_GROUPS` section), `anim-definitions.md` (one-shot half landed),
`architecture.md` (`SoundDefs`, `SoundArchive`, `WorldSounds`, `AnimRuntime`), PLAN-M3-weapons.md
(checklist 31 ☐→☑, `### D31` reconciled), `backlog.md` (the dead-`Sound`-path note closed), CLAUDE.md
status.

**Config tuning-override layer (`src/Utils/Config.cs`) + FlightModel wired (2026-07-24, on the
`worktree-config-module` side branch).** A static `Config` reads an optional sparse
`res://config.json` (`CSVM/config.json`, git-ignored) whose values override the hand-tuned `const`s
for fine-tuning without a recompile. Design decided by a grilling pass: **override, not replace** — the
`const`s stay in-code as the DEFAULT with their rationale comments, and `Config.GetFloat/GetInt/GetBool/
GetString(key, const)` returns the file's value if the key is present, else the `const` returned
**verbatim**. Keys are `moduleCamelCase.fieldCamelCase`, grouped one nesting level in the JSON; access
is **read-through** at the point of use (per-frame dict lookup, negligible; keeps future live-reload a
cheap mtime-reparse instead of an event system). Every getter self-registers `(key, default)`, so
`--dump-config` writes a fully-populated nested template to `./.scratch/config.dump.json`; a file key
that matches no tunable is warned loudly (typo detector, `ReportOrphans`), and a queried key missing
from a loaded file warns once. `WarmTuningRegistry` (PlaneViewer) steps a throwaway `FlightModel` with
a default `PlaneStats` once at startup so orphan-check + dump are complete with **no world / no game
data**. Comment + trailing-comma tolerant on input; malformed JSON → one error line, never throws.
Scope this pass: **`FlightModel`'s 15 `TUNE` consts only** (read into locals at the top of `Step` so
every key registers even on a frame that skips the stall/knife branches; `MaxDiveSpeedFrac`, marked
"hard cap" not TUNE, left alone). **Read-only** — nothing writes the file; `res://` chosen so a writable
`user://` layer can later stack under the getters (the "save player configs" direction) without touching
a call site. Deferred: write/save + `user://` layer, live-reload, the other 13 TUNE-bearing files,
`Vector3`/`Color` getters.

Verified: build clean (0 warnings). `--dump-config` writes the 15-key `flightModel` template with exact
defaults. With a deliberately-broken `config.json` (one valid override, one string where a number
belongs, one typo'd key, plus a `//` comment + trailing comma): 3 overrides parsed, the valid override
silent, the string caught as a type mismatch, the 13 absent keys reported once each, and the typo'd key
flagged as an orphan — all as designed. Regression: a static-viewer md5 A/B (`--viewer
--plane=player_bhawk --paint-seed=1 --no-pads`, worktree-new vs the same shot with the two edits stashed
back to `f5b3a26` baseline) is **byte-identical** (`f1290254…`), proving `Config.Load()` + the startup
warmup/orphan pass are side-effect-free on the build/render path. The flight math itself is inert **by
construction** — with no config.json every getter returns its `const` fallback verbatim and no operation
was reordered — which is the accepted verification here since `--fly` screenshots are not
frame-deterministic (`verification.md` rules 44, and the non-determinism table). Docs: `architecture.md`
(`src/Utils/Config.cs`), `cli.md` + CLAUDE.md module index (`--dump-config`).

## 2026-07-24 — M3 Wave D D32: the world-effects runtime (destruction + impact puffers)

The named destruction/impact effects now render their smoke and fire. D30 established that a runtime
`PUFFER_STATE` builds nothing in the flight build — the puffer factory + `TextureArchive` are torn down
after the world build (`KeepArchivesOpen` is lab-only, `verification.md` rule 76) — and deferred the
puffer half of both impacts and deaths to a shared **world-effects runtime**. That runtime is the
world-scoped generalization of the per-player crash runtime (`BuildFlightCrashRuntime`), which already
proves the pattern: a live `PufferFactory` over kept-open textures, `PlaceCalledTemplates`,
`NameResolveFallback`, and effect-template roots staged for a `CALL_ANIMATION` to relocate.

**What landed.**
- **`PlaneViewer.BuildWorldEffectsRuntime`** — one per session (built in the `--fly` path, needing the
  session textures the crash runtime already keeps open): a **hidden** `world_effects` stage of the
  `EffectStageRoots` gamez templates (`gunhit`/`dum_gunhit`/`mag_gunhit`/`flame_ball_01`/`flame_ball_02`/
  `he_ring`/`ap_effect`/`flak_control`/… — 18, all present in every chapter's gamez), plus an
  `AnimRuntime` (`AutoStart`=false, `PufferFactory` sustained, `PufferParent`=world root,
  `PlaceCalledTemplates`+`NameResolveFallback` on) bound to the closure of the 28 `EffectAnimNames`.
- **`AnimRuntime.PlayEffectAt(name, worldPoint)`** — relocates the effect def's template root onto the
  point (`PlaceTemplateAt` gained an absolute-point overload) and `Start`s it. Its puffers ride that
  relocated root and parent at world level, so they render even though the stage is hidden — which is
  the point of hiding it: the template **meshes** (the `gunhit` debris bits, the `he_ring`/`huge_splash`
  models) never flash at the stage origin (a documented mesh follow-up). `Handles(name)` gates the
  routing so only curated effects are handed off.
- **Two callers.** `ProjectilePool.EffectSink` on a **rocket/ordnance** impact (the non-model IMPACT
  effect — `large_fireball`, `he_ground_effect`, …); the world runtime's new `ExternalEffect` delegate
  routes a **death** sequence's `CALL_ANIMATION` of a curated effect (`large_30sec_fire`, …) here
  instead of starting it locally where its factory is gone.
- **Bounds + hygiene.** `EffectTtl` (32 s) tears down a stop-less sustained emitter so
  `large_30sec_fire`'s `fire_n_smoke` (a `Loop` with no `ACTIVE_STATE 0` until t=30) does not emit
  forever; `SoundHandledElsewhere` makes the runtime's SOUND/SOUND_NODE no-ops (the impact/death audio
  is already played by D30's pool / D31's world runtime — playing it again would double it and, with no
  audio session, only spam "silent for the session").

**Two decodes fixed on the way.**
- A `PUFFER_STATE` whose `AT_NODE` is `INPUT_NODE`/`MAIN_ROOT_NODE` now resolves to the **anchor**
  (`IsSelfNodeRef`, the same −200/−100 sentinel rule `ConditionNode` already applied). Before, the host
  went through `ResolveOne`→null→"no host node", so `large_30sec_fire` (host `INPUT_NODE`) emitted
  nowhere. With the fix its fire emits on the effect's own relocated root — the whole point of a
  called destruction fire landing at the call site.
- `AnimProgram.Subset` gained a multi-root overload for the effect closure.

**Guns deferred — stronger than the plan's singleton note.** The plan expected the `gunhit` smoke to
*collapse onto one puff* under rapid fire. In fact `gunhit`'s `blacksmokepuffer` has **no `ACTIVE_STATE
0` stop**, so a per-round shared emitter would emit **forever** at the last hit — worse than a jumping
puff, a leak. So gun impacts are **not** routed (`ProjectilePool.EffectSink` is gated `!weapon.IsGun`);
the gun `*_gunhit` names stay bound and testable. A guns pass needs per-hit copied/expiring emitters.
Rockets/ordnance (≤1/s, self-terminating fireballs) route now.

**Verified.** New `--effects-test` (builds the world-effects runtime, plays each bound name at the
camera point, reports resolve✓ + puffer-built per rule 76, to `./.scratch/effects_test.txt`, then
quits). Made deterministic — the runtime RNG is seeded (several gun variants gate their puffer behind
`RANDOM_WEIGHT`) and every effect is `StopAll`'d before the next (they share the `trailpuffer2` puffer
name, so a lingering one reads as the next's "no puffer"). Reproducible and identical across C1/C2/C4/C5:
**28/28 resolve, 16 build a puffer** — `large_fireball`/`small_fireball`/`large_30sec_fire`/
`great_balls_of_fire`/`large_black_smokeball`/`big_splash`, the gun `*_gunhit` smoke, and the
`ap`/`sonic`/`flak`/`scatter`/`torpedo` ground bursts. The 12 that build none are honest: point-light/
model effects (`he_ground_effect`, `flash_effect`, `rear_flash_effect`), the `RANDOM_WEIGHT`-gated gun
variants, and `biggun_flying_parts` (a zeppelin container whose puffers ride unstaged `fly_trail*`
sub-trails). A C1 `--fly --fire-rockets` run routes HE impacts through `PlayEffectAt` (`impact: wep_06
(BOOM) → …`) with no crash and no effects-runtime warning noise. Regression: 8-chapter `--damage-test
--damage-hd=100` clean (0 hard errors, 16 defs swept each — unchanged); `--freecam` C1 ambient puffer
census unchanged (`4 puffer emitter(s): splashpuffer1..3, steampuffer`, same 6 unhandled kinds), so the
`INPUT_NODE`-host fix did not disturb the ambient world. The **on-screen** fireball look is an owed
playtest (`playtest.md`), like D30's splash — headless `--screenshot` still NREs in `GetImage` (a
pre-existing, unrelated harness limitation).

**Docs.** `weapon-effects.md` (D32 engine-wiring section + the impact→effect render map),
`anim-definitions.md` (`INPUT_NODE`/`MAIN_ROOT_NODE` AT_NODE → anchor), `architecture.md` (`AnimRuntime`,
`Projectile`, `PlaneViewer`), `cli.md` + CLAUDE.md module index (`--effects-test`), PLAN-M3-weapons.md
(checklist 32 ☐→☑, `### D32` reconciled), `backlog.md` (gunhit guns follow-up + the mesh-effects
follow-up), `playtest.md` (the owed fireball/impact-effect listen-and-look).

## 2026-07-24 — M3 Wave D D44: pylon ordnance visuals (mounted rockets that deplete)

Rockets now hang under the wings and vanish as they are fired. The new `src/Flight/PylonOrdnance.cs`
mounts one FLYOUT `MODEL` body per loaded pylon — the **same** chapter-gamez prototype the round
itself flies (`he_rocket`, `sonic`, …) — so the thing on the wing and the thing that leaves it are one
asset. `Build(loadout, pool)` calls the newly-public `ProjectilePool.BuildFlyoutBody(weapon)` (the
resolve-cache-and-`BuildSubtree` path that `BuildFlyoutModel` was refactored to share), parents each
body to its `pylonN` marker at **identity local transform** — the marker's −Z is the forward firing
direction, so the body sits nose-forward at the exact pose the round launches in, tail at the mount —
and `Update` (driven by `FlightController` after `UpdateRockets`) shows/hides each body per its live
`Hardpoint.Ammo`, a respawn refill re-showing it. Mounted bodies ride the plane and are freed with it;
they inherit the default visual layer, so every splitscreen camera sees them exactly as it sees the
plane.

**The two flagged traps, checked against the data, not assumed.** *(1) One model per pylon, not one
per round:* a pylon carries `CLUSTER_SIZE` (3, stock HE) but the original shows a single rocket, so
visibility keys on `Ammo > 0`, never a per-round stack. *(2) No double-up with airframe geometry:* a
name search of the plane `nodes.json` for rocket/missile/bomb/torpedo/ordnance/munition mesh turned up
**nothing** — the only pylon-named nodes are `pylon1..8` (the mesh-less firing markers) and one plane's
structural `lpylon*`/`rpylon*`, all `model_index -1`. So the FLYOUT body adds ordnance where the model
had none; it cannot z-fight a static rocket, because no plane ships one.

**`--rocket=<wep_id>` added** (a testing hook, `PlaneViewer.ApplyRocketOverride`). The plan's verify
says "swap rocket type via `--loadout=`", but all 11 stock loadouts carry HE (`wep_06`) and `--loadout=`
only swaps whole plane defs — so no def-swap can change the mounted model. `--rocket=` replaces every
hardpoint's ordnance (resetting each pylon to that weapon's `CLUSTER_SIZE`) and is the honest way to
prove the model is data-derived; the general per-slot `--loadout=` override remains F43.

**A `--screenshot` red herring, run to ground.** The first headless soaks flooded with `ERROR:
Parameter "t" is null` / `NullReferenceException`. It was **not** the new code: those appear only with
`--screenshot` in a headless build (the framebuffer-capture path, the same limitation D30/D32 hit in
`GetImage`). A clean `--quit-after` soak with no `--screenshot` reports **zero** errors — that is the
authoritative regression check here. Recorded as a `verification.md` rule so the next session doesn't
re-chase it.

**Verified** (headless, `--quit-after`, no `--screenshot`, zero errors): stock Bloodhawk / C1 logs
`pylon ordnance: 3 mounted rocket model(s)` and `flyout model 'he_rocket' (wep_06) instanced: 1
mesh(es)`; a `--fire-rockets` soak fires 9 HE round-robin across pylon1/2/3 (each 2→1→0) and hides
`pylon1` on the 7th pull, `pylon2` on the 8th, **`pylon3` on the 9th** — the exact depletion the verify
names. `--rocket=wep_08` instances `sonic` bodies instead (`flyout model 'sonic' (wep_08) instanced`),
proving the model varies by type. The 8-pylon Warhawk mounts 8; Bloodhawk over C5 resolves the
prototype too (the roots exist in every chapter — B14). The pixel-level z-fighting look is the owed
at-the-controls playtest, shared with B14/B17 (a 1.5 m mounted round is not headless-screenshottable in
this build).

**Docs.** `architecture.md` (new `PylonOrdnance` entry + `Projectile`/`FlightController` ⚠ lines),
CLAUDE.md (module index line, Current-status wave position + Next pointer), `cli.md` (`--rocket=`),
`verification.md` (the `--screenshot`-floods-headless rule), PLAN-M3-weapons.md (checklist 44 ☐→☑,
`### D44` landed note; Wave D now complete).

## 2026-07-24 — M3 Wave A A10: the slot→firepoint binding rule confirmed in-engine (Wave A complete)

The last open Wave A item, and — as the plan's preamble anticipates ("landing no code with a correct
disproof is a success here") — a **verification, not code**. It closes the one thread the plan left
open on the muzzle-mount binding: the reverse-index rule (slot _n_ → `firepoint(9−2n),(10−2n)`) was
"strongly supported but pending in-engine confirmation" after two earlier binding hypotheses had been
disproven. A10 confirms it.

**Method — cross-reference two committed instruments, no new tooling.** `--dump-loadout` reports each
gun slot's mount name → bound firepoints; `--dump-markers` reports each firepoint's plane-frame
position (nose −Z, right +X, up +Y). For all 11 aircraft, every **firing** gun group's bound firepoint
matches the geometry its mount name asserts:
- **Devastator (decisive — the only two-axis airframe): 3/3 on both axes.** `Low Inner`→fp7,8 (y−0.6,
  |x|1.2), `Low Outer`→fp5,6 (y−0.83, |x|2.12), `Upper Inner`→fp3,4 (y+0.44, |x|1.0). Low<Upper on y,
  Inner<Outer on |x|.
- **Peacemaker (decisive — the only asymmetric one): 2/2, sides correct.** `Center`→fp7,8 (x≈0),
  `Right Fuselage`→fp5,6 (x+1.96/+1.66).
- **Bloodhawk** reproduces the user's playtest (40-cal `Inner` |x|3.22 < 30-cal `Outer` |x|3.66);
  **Brigand** reproduces "W1 and W2 share the outer mount" (both at |x|2.34, fp7≡fp6/fp8≡fp5).
- **The Firebrand/Kestrel soft spot the plan flagged is resolved, not a discrepancy:** their firing
  guns are correctly ordered (inner<middle<outer / on-centreline); the reverse-index rule only seats
  the **inert `Rear Turret`** on the outermost firepoint (Firebrand |x|5.67, Kestrel |x|3.26), which
  fires no flash in M3 — the turret firepoint binding is M4 scope.

**Windowed corroboration (this machine has a real GPU — RTX 5080/Vulkan, so `--screenshot` WITHOUT
`--headless` works; the absolute-path rule 74 applies).** A firing Bloodhawk shows a warm muzzle flash
on the wing with the HUD depleting **only** the selected group (also re-confirming B18's one-group-at-
a-time). The Peacemaker marker overlay shows `firepoint7` on the centreline and `firepoint5` on the
right fuselage — the mount names made visible. **Appearance:** the game's own `slug_muzzle1`/`2`
flipbook is a warm orange→yellow radial burst that matches `OriginalScreenshots/MuzzleFlash1.png`; D29
draws `slug_muzzle1` and stock is all-slug, so the texture is correct (the 2-frame flip animation +
per-ammo dum/ap/mag textures remain the documented D29 refinement).

**No code changed** — the binding was already correct (the `markers` arrays in `stock_loadouts.json`
are the rule's explicit output, checked A7) and D29 already rendered the flash at the resolved
firepoint. Screenshots are rendered plane frames, so they stay in `./.scratch/` (swept) and are **not
committed** — the no-assets rule covers rendered asset data; the durable evidence is the two dump
instruments + `markers.md`, which cite only format-level firepoint positions.

**Docs.** `markers.md` (binding section "pending A10" → "confirmed in-engine", with the method + the
turret-soft-spot note), PLAN-M3-weapons.md (checklist A10 ☐→☑ + `### A10` landed note; the binding-rule
subsection header/⚠ flipped to confirmed; Wave A now complete), CLAUDE.md Current-status (A10 done →
Next is Wave E).

**Bitmap-font HUD text renderer — M3 Wave E, item E34 (2026-07-24):** the game's own HUD font is now a
reusable renderer (`src/Flight/HudFont.cs`), the foundation the rest of Wave E draws with. The two
atlases in `extracted/rimage/` — `5pointhud.png` (normal) and `5pointhudbrite.png` (highlight) — were
pixel-probed and are **463×6, a proportional 1-bit font, five px tall (rows 0–4), covering printable
ASCII `0x20`–`0x7e`**: space is a blank leading cell, so the 94 ink glyphs map one-per-code
`0x21`–`0x7e` in code order (`glyph(code) = run[code−0x21]`), letters uppercase-only (`a`–`z` reuse
`A`–`Z`). Colours are two green levels on black (normal core (0,150,0), highlight core (0,255,0), a
dim (0,32,0) outline). **The PLAN's shorthand "`0123456789:;<=>?@A…z`" understated the range** — the
probe found it begins at `!`; corrected in `docs/formats/hud.md`, which now carries the full decode.
The reader auto-segments source rects at load (maximal inked-column runs, exact because no glyph has a
blank interior column — 94 runs = 94 codes, verified; it warns if the count drifts), keys the black
background transparent (green kept, so a white modulate reproduces the original), and draws each glyph
with `DrawTextureRectRegion` under a Nearest filter, 1 px tracking, 3 px space; sizing routes through
`HudMetrics` like every other HUD element. **Verified** (`--hud-font-test`, a flag-gated per-pane proof
overlay `src/Flight/HudFontTest.cs`): the sample `GUNS 30: 2000  ROCKETS 06: 9` renders correctly in
both variants at 1P and in a 4-way splitscreen pane, the glyphs identical and the size differing only by
the HudMetrics factor (1P Scale 0.50 vs 4P pane 0.354 — the sqrt-damped 0.707 ratio, not a naive 0.50),
with the `Measure()` underline ending exactly at the last glyph in both. `--screenshot` was run
**windowed** (verification.md rule 71: `--headless` floods `Parameter "t" is null` and never captures).
Purely additive and gated — with the flag off, `HudFont` is not loaded and the flight HUD is unchanged.

**Docs.** `docs/formats/hud.md` (new "HUD bitmap font (`5pointhud`)" section), `docs/architecture.md`
(new `src/Flight/HudFont.cs` entry), `docs/cli.md` (`--hud-font-test`), PLAN-M3-weapons.md (E34 ☐→☑),
CLAUDE.md (module index + Current-status Next E34→E35).

## 2026-07-24 — M3 Wave E E35: gun + missile cockpit gauges (`gungauge`/`missilegauge`)

The two weapon dials the `gauges` subtree carried but nothing wired. `GaugeCluster.ExtractWeaponGauge`
reads both (extending the same extractor as the altimeter/speedometer/damage dials), and
`FlightController.UpdateWeaponGauges` pushes a `WeaponGauge` (count / type / selected slot / per-slot
fractions) each frame from the live loadout. Placed off `OriginalScreenshots/HUD.png` (the Warhawk):
**ROCKETS** one dial-pitch above the altimeter, **GUNS** above the speedometer.

**How the original drives them** (decoded from planes.zbd + `support\cockpit.gw`, full page in
`docs/formats/hud.md`): the dial node is mesh-less on **every** plane and the labelled face
(`gungauge.tif`/`missilegauge.tif`) hangs off a generic child (`g815`/`g819`) — **the per-plane
parenting quirk the plan warned about is the `damageindicator`'s, NOT these two** (verified across the
whole roster); the extractor still uses the safe "any unrecognised child = face" rule. `4char_ammo` is a
`zero.tif`…`nine.tif`,`SPACE.tif` digit cycle (drawn right-aligned); `6char_type` an `A`…`Z`,`zero`…
`nine`,`SPACE` cycle showing the weapon `NAME` upper-cased; `ggindicator0..3`/`mgindicator0..7` are belt
lights (one per gun group / pylon, top = 0, CCW) each carrying a 3-frame green/yellow/red cycle; the
`gg`/`mgarrow` needle rotates to the selected slot.

**Semantics (user-corrected mid-implementation):** the gun count is **per gun group** (the selected
group's own counter — already independent from B12), the rocket count is **per pylon** (the next-to-fire
pylon's rounds — a full HE pylon reads `3`), **not** a fleet total. This matches the original's Warhawk
reading `BOOM 3`. Belt lights step green → yellow (≤ 0.34 fraction) → red (empty); turret gun slots are
excluded (a 2-gun plane lights 2). The green/yellow/red thresholds are inferred (the data only says the
indicators *can* be those three colours, not when) → a `IndicatorLowFrac` TUNE, with a `playtest.md`
item for the user to confirm the yellow state exists in the original and at what fraction.

**Verified** (windowed captures, verification.md rule 71): all 11 flyable planes render both gauges —
including the Devastator's inherited `player_pfighter` and the five turret airframes (only firable groups
lit). Counters track B16/B17: each plane's W1 caliber → its capacity on the gun gauge (70→1200 …
30→2800), `BOOM 3` per full HE pylon on the missile gauge. Belt stepping proven by depleting the
Bloodhawk's rockets — full = green, `1` remaining = **yellow**, `0` = **red**, the arrow tracking the
yellow next-to-fire pylon. Purely additive: the gauges render only when the FlightController feeds them,
so the `--viewer`/lab builds (no loadout) are unchanged.

**Docs.** `docs/formats/hud.md` (new "weapon gauges" section + the `cockpit.gw` drive),
`docs/architecture.md` (`GaugeCluster` entry — the two gauges, per-group/per-pylon rule, generic-face
rule), `playtest.md` (E35 gauge item + the yellow-state A/B), PLAN-M3-weapons.md (E35 ☐→☑),
CLAUDE.md (Current-status Next E35→E36).

## 2026-07-24 — M3 Wave E E36: selected-weapon text readout (`MSG_HUD_GUNGAUGE`)

The game's own textual weapon readout, drawn in the `5pointhud` bitmap font (E34). New
`src/Flight/WeaponReadout.cs` — a bottom-centre two-line `Control` that renders the currently-selected
gun group and rocket type with their live ammo, from the message table's own templates
`MSG_HUD_GUNGAUGE` (id 188, `"GUNS: %1: %2!d!"`) and `MSG_HUD_MISSLES` (id 189,
`"MISSILES: %1: %2!d!"` — the table's misspelling), resolved through `Messages` and **never
hardcoded**. `%1` names the gun group / rocket type (that is why the strings exist — the counters are
per group / per pylon, so the readout has to say which); `%2!d!` is the count.

Added `Messages.Fill`/`Format`: substitutes `%1`…`%9` positional placeholders, consumes a trailing
bang-spec (the `!d!` in `%2!d!` — the arg is already a formatted string), and turns `%%` into a
literal `%` (used by `MSG_HUD_HEALTH` etc.). FlightController feeds the readout in the same
`UpdateWeaponGauges` pass that drives the gauges: `%1` = the gun group's **mount name**
(`Inner Wing Guns`, from `IDS_AIRFRAMEGUNGROUPNAMES`) or the rocket's **display name**
(`High-explosive rocket`, its `MSG_WEAP_*` `DESC` resolved through `Messages`, falling back to the
short handle if unresolved); `%2` = the selected group's per-group rounds / the next-to-fire pylon's
per-pylon rounds (matching the E35 gauge). This **replaced the interim `AmmoLine`** debug text (which
summed rockets to a fleet total — the very total the E35 user-correction moved away from).

The HUD font now loads unconditionally in flight (E36 needs it); `--hud-font-test` gates only the
`HudFontTest` overlay, not the font load.

**Verified** (windowed, verification.md rule 71): the readout renders both lines in the game font at
bottom centre. Cycling gun groups updates both name and count — Bloodhawk `GUNS: INNER WING GUNS: 2400`
→ `GUNS: OUTER WING GUNS: 2800` (the 40- and 30-cal groups' distinct capacities), and the Balmoral (the
plan's named plane, twin independent .50s) `GUNS: INNER WING GUNS: 2000` → `GUNS: OUTER WING GUNS: 2000`
(same count, name changes). The missile line reads `MISSILES: HIGH-EXPLOSIVE ROCKET: 3` (per pylon). The
text comes from `messages.json` — a missing table would render the raw `MSG_HUD_GUNGAUGE` key.

**Docs.** `docs/formats/hud.md` (new "text readout" section), `docs/architecture.md`
(`WeaponReadout.cs` entry + `Messages.cs` `Fill`/`Format`, `HudFont.cs` load note), `playtest.md`
(weapon-selector item updated: the readout replaced the bracket line), PLAN-M3-weapons.md (E36 ☐→☑),
CLAUDE.md (module index + Current-status Next E36→E37).

## 2026-07-24 — M3 Wave E E37: impact-point reticle (ballistic projection) — Wave E complete

The gun aiming reticle, with the original's behaviour: it is **not pinned to screen centre** — it
marks the **projected ballistic impact point of the selected gun group's rounds at a convergence
distance**, so it trails the nose in a hard manoeuvre and sits on the rounds in steady flight (user
spec, 2026-07-22). New `src/Flight/ImpactReticle.cs` — a per-pane, viewport-filling `Control` that
draws the game's own pipper `extracted/rimage/impact_point.png` (a **32×32 RGBA** warm-white disc
with a cross-notch centre; the alpha is authored, so no colour-keying — pixel-verified against the
file) at a world point, projected via `Camera3D.UnprojectPosition` at `_Draw` time (mirrors
`MarkerHud`, never cached, so it can't lag the chase camera), fixed screen size scaled by
`HudMetrics`. Loaded once in `PlaneViewer` and built per player pane whenever the plane carries a
loadout.

`FlightController.UpdateReticle` (in `_Process`) computes the point: it averages the SELECTED
firable gun group's muzzle poses and marches a round through `BallisticImpactPoint` — the **same**
`VELOCITY`/`ACCELERATION`/`GRAVITY` path integration `ProjectilePool` fires each round with (plus
the plane's inherited velocity; only the random `CANNON_SPREAD` is dropped, since the pipper marks
the cone centre) — to `GunConvergenceDist`. `ProjectilePool.WorldGravity` became `internal` so the
reticle and the rounds share one gravity constant. Hidden while crashed or when the plane has no
firable gun.

**The convergence distance is a TUNE (`GunConvergenceDist = 250 m`)** — `weapons.json` carries no
convergence field (player guns are `RANGE 1000`, `VELOCITY 750–1000`, no `ACCELERATION`/`GRAVITY`,
so a straight line). Open question 4 in the plan is now *chosen*, pending an original-game A/B.

**Verified** (windowed, verification.md rule 71). Steady level Bloodhawk (`--hold=0,0,0,0.6`): the
tracer stream passes through the reticle, and a temporary numeric breadcrumb read `nose→reticle =
0.00°` — the pipper sits exactly on the gun axis, so the rounds land where it sits. Hard pull
(`--hold=1,0,0,0.6`): as angle-of-attack grew (`nose→vel` 7.8°→15.0°) the reticle deflected off the
nose (`nose→reticle` 0.46°→0.77°) with a **negative** trail-pitch — i.e. **below** the nose, toward
the velocity vector — reproducing "trails the nose during a pull." (The angle is small because
bullets fly ~900 m/s against a ~55 m/s plane; the on-screen trail is set by that ratio, not by the
convergence distance — a physics fact, now in `hud.md`.) Clean cross-chapter (C4) and 2-player
splitscreen (each pane draws its own reticle through its own camera, scaled by the pane factor); the
breadcrumb was removed before landing; build clean (0 warnings).

**E38 (lock-on indicator) is ⊘ deferred to M4** (with B19/B20 — no enemy planes to lock in M3), so
**Wave E (HUD) is complete**; only Wave F (debug tools) remains.

**Docs.** `docs/formats/hud.md` (new "gun aiming reticle" section — the texture + the
ballistic-projection behaviour + the convergence TUNE), `docs/architecture.md` (`ImpactReticle.cs`
entry + FlightController `UpdateReticle` note), `playtest.md` (new E37 item + the convergence TUNE),
PLAN-M3-weapons.md (E37 ☐→☑, open question 4, Wave-E-complete position), CLAUDE.md (module index +
Current-status → Wave F).

## 2026-07-24 — PLAN-testing authored (testing & verification infrastructure) + M3 Wave F reconciled

**What landed.** `docs/PLAN-testing.md` — a 20-item, 4-wave plan scoped in a grilling session:
determinism core (shared `GameClock` with halt/step in every mode, sim-clock-driven shader time
for byte-identical frames, per-subsystem seeded RNGs, a `--det` bundle implied by scripted runs,
`--pos`/`--direction` unified placement), harness + logging (`Log`, in-engine `--run-tests`
suites with exit codes, a `CSVM.Tests` xUnit project on local-data golden invariants +
hand-authored fixtures, `RunTests.ps1`), perf + visual instruments (startup-phase stopwatches,
an A/B perf suite with git-ignored history, a golden-image md5 tripwire, `--tex-override`/
`--tex-census`, `--stage=empty` + `--node=` stages, the original's numpad flight-camera views +
`--view=`), and a freecam inspect layer (click + ancestor-ladder selection, Node Lab, mesh/damage
labs on the selection, collider wireframes). Execution queued behind M3's remaining F39/F42.

**M3 Wave F reconciliation (user-confirmed).** F40 ⊘ superseded by PLAN-testing D31/D34; F41 ⊘
superseded by D32 (its coverage instrument + census verify absorbed there, the census also a B12
suite); F43 ☑ found already delivered (`--infinite-ammo`/`--loadout=` live in `PlaneViewer.cs`
and documented in cli.md since B12/B16 — the item predated their landing and was never ticked).
F39 and F42 stay in M3 (F42 is exit-criterion 2's instrument).

**Verified.** Plan evidence lines cite code greps (TIME sites, unseeded RNG sites, `GD.Print`
census, `ManualAdvance`/`FixedDt` mechanics); F43's tick verified against `PlaneViewer.cs:466–467`
and `FlightController.InfiniteAmmo`, not the docs alone. No engine code changed.

**M3 F39 — weapon lab in `--viewer` (2026-07-24).** `src/UI/WeaponLab.cs`, the fourth `--viewer`
lab (key **W**, `--weapon-lab[=wep_id]`), beside the damage (H) / livery (L) / mesh (M) labs. It
mounts any of the 48 `weapons.json` entries on any of the parked plane's firepoints/pylons and
fires it, driving its OWN `ProjectilePool` so a round runs the identical ballistics flight fires.
The bottom-right panel steppers pick weapon (with live ballistics: caliber, rate, velocity,
damage, `CLUSTER_SIZE`, range, class flags), mount (`all firepoints` / each `firepointN` / each
`pylonN`), and target surface (`default`/`water`/`buildings`); a slider parks a stand-in target
wall 15–400 m ahead; auto-fire + fire-once + **Space**; and copy-CLI-args emits
`--weapon-lab=<id> [--weapon-mount=<name>] [--weapon-fire]`, as `LiveryLab` does.

*The design question the task flagged — what firing looks like with no world:* the viewer has no
chapter gamez, so the pool is built scene-less (`ProjectilePool(textures, null, null)`) — rockets
fly streak-only (no `FLYOUT` prototype to instance), impacts show the stand-in spark (no `splash`
geometry, no puffer runtime), and `DamageSink` is null. The pool's hits come from a per-step world
raycast, so with no terrain there is nothing to hit; the lab therefore parks a `StaticBody3D`
target wall ahead of the nose, **tagged with `SceneBuilder.SurfaceMeta`** so B15's classifier still
picks the matching `IMPACT` variant. Target + tracers show only while the lab is engaged, so an
unadorned `--viewer` screenshot stays byte-identical (the lab is built hidden in every parked
`--viewer` session so W always toggles it). The pool + target + mounts build in the constructor
(not `_Ready`) so `RunSelfTest` works synchronously right after `AddChild` — `Spawn` needs no frame.

**Verified.** `--weapon-test` mounts and fires every one of the 48 entries once from the
`all firepoints` mount and reports **48/48 fired OK, 0 errors** on the Bloodhawk, the Kestrel
(7-firepoint centreline rig), the Warhawk and the Peacemaker (`./.scratch/weapon_test.txt`).
Windowed captures (`wl_guns.png`) show `wep_30` tracers streaking from the wings into a cluster of
impact sparks on the target, and (`wl_rocket.png`) the `wep_06` rocket path with the panel reading
`1/s · 1200 m/s · dmg h60/a40 · ×3 · HE`; the pool's impact log confirms the raycast hits
`weapon_lab/weapon_target` and classifies the surface (`Default`). A plain `--viewer` renders clean
(`wl_plain.png` — no target/tracers/panel). Build clean (0 warnings / 0 errors). Wave F now closes
with F42 (`--destroy=`); docs updated in the same turn (`cli.md`, `architecture.md`, CLAUDE.md
module index + viewer-keys + Current status, the plan's F39 checklist + detail).

**M3 F39 refinement — gun groups, banks, explosions (2026-07-24, user feedback).** Five changes
after the first playtest. (1) The lab's mounts are now the game's grouping: weapons split into two
banks — GUNS fire from the plane's named **gun groups** (bound from the stock `Loadout` via
`Loadout.Bind` — "Inner Wing Guns" …, each resolving to its firepoint pair), HARDPOINTS fire from
its **pylons**. (2/3) The bank filters both the weapon list and the mount list, so a gun can only
fire from a gun group and a rocket only from a pylon (a bank stepper switches; `--weapon-lab=<id>`
sets the bank from the weapon's class). (4) The target-distance slider now reaches **1100 m** (was
400) — far enough to watch a rocket fly its full course; guns fall short past their ~1000 m range.
(5) **Hardpoint impacts now show an explosion:** `ProjectilePool.SpawnExplosion` renders a cluster
of large additive orange sprites for a `!IsGun` impact when `EffectSink` is null (a scene-less pool
— the viewer had shown only a small spark because there is no world-effects runtime to build the
real fireball); flight is unchanged (its `EffectSink` is set, so the real puffer still plays). Also
fixed a de-DE locale comma in the ballistics readout (`h4,5` → `h4.5`, formatted `InvariantCulture`).
Mount CLI tokens: `g<slot>` for a gun group, `pylon<n>` for a pylon, `all` for the whole bank.

**Verified.** `--weapon-test` now fires each of the 48 from a mount of its class (guns → gun groups,
hardpoints → pylons) and reports **48/48 fired OK, 0 errors, 0 skipped** on the Bloodhawk, the
Kestrel (centreline + turret group), Warhawk and Peacemaker. Windowed captures: `wl_group.png` shows
`wep_40` fired from the named "Inner Wing Guns" group (panel `bank: GUNS`, mount `Inner Wing Guns`,
CLI `--weapon-mount=g1`); `wl_boom.png` shows the `wep_06` HE-rocket explosion fireball on the
target (panel `bank: HARDPOINTS`, `all pylons`). Build clean.

**`--destroy=` CLI trigger — M3 wave F, item F42 (2026-07-24):** the last open Milestone-3 item.
`--destroy=<name>` kills a named world destructible at session build so a `--screenshot` captures its
destruction **with nobody at the controls** — the way the rest of wave C (`--damage-hd`) is verified,
but as a rendered shot instead of a text census. It is exit-criterion 2's instrument. Code is one
file (`PlaneViewer.cs`, +135): a `--destroy=` flag, a `TriggerDestroy` helper, and a build-time trigger
block. `<name>` matches, case-insensitively by substring, any live destructible's def name / animation
name / anchor `cs_name`; every distinct object that matches is killed (resolved to its authoritative
`DestructibleRegistry` instance and de-duped by anchor, capped 64 with a loud note). It **reuses the
weapon-hit path exactly** — `AnimRuntime.DamageAt(anchor, MaxHealth+1)` — so the healthy→destroyed swap
fires synchronously (the death's Initial sequence dispatches its t=0 events inside `Start`) and the
debris/effects/audio are identical to a rocket kill; the world subtree is already in the tree so the
death's global-transform reads and effect stage are valid, and the runtime self-ticks the death out
during the `--screenshot` warm-up. It is a **pure modifier** (forces no mode): pair with `--freecam`
(the "no controls" case) or flight. In `--freecam` it **auto-frames** the killed object (its merged
mesh AABB via `SpectatorCamera.Frame`, unless `--campos`/`--lookat` set) and **builds the world-effects
runtime** itself so the fire/smoke render — both gated on `--destroy`, so a plain `--freecam` build is
byte-identical (confirmed: the 8-chapter node counts are unchanged from a plain freecam).

**Verified.** A destruction screenshot in each of the 8 chapters (`--freecam --chapter=CX --destroy=…
--screenshot`, windowed), names taken from `--damage-test` and the compiled-anim data (never guessed):
`m_build01` C1 (Hollywood-lot building, `great_balls_of_fire`), `bhf_heliumtank1` C4 (helium-tank
facility fireball), `g_tower1` C3 (guard tower), `agyrobus` C5 (fireball over the night city), `s_build`
C2 (building swapped to its destroyed shell), `--mission=MP1 --destroy=patrolboat` C1B (fireball on a
placed harbour dock), `--mission=M01 --destroy=leng11` C1C (fireball on the placed airship's engine).
**C1B/C1C/C2B's IA1 worlds carry no placed ground destructible — their only destructibles are the
mission airship, which a controller-less freecam parks at origin (hidden as unplaced), so the trigger
fires and the effect renders but there is no framed geometry**; C1B/C1C recover a placed target via a
multiplayer / story mission (patrolboat / the M01 airship), while **C2B's every destructible (47/47) is
airship-mounted** — a map-content fact, not a defect. The 8-chapter plain `--freecam` regression is
clean (one pre-existing C1 `ReportLateSoundFailure` PushWarning, unrelated — the `--destroy` path is
inert without the flag). Screenshots stay in `.scratch/` (git-ignored — no game imagery is committed);
reproduce with the commands above. With F42 landed, **M3 wave F is complete** and every M3 checklist
item is done or deferred to M4 (B19/B20/E38 guided flight, F40/F41 → PLAN-testing).

## 2026-07-25 — M3 weapons playtest pass 2 + Milestone 3 plan archived

**Playtest pass 2 (at the controls, user).** A second pass over M3 weapons/destruction; all 14 findings
were traced to code. Actionable ones are recorded in `backlog.md`'s "Milestone 3 Polishing → Playtest
pass 2" (items 1–19); `playtest.md` §1 was reconciled into a true owed-list (passes retired, findings
repointed to their backlog number and re-scoped to "re-test after fix", failures marked). **Passed:**
destruction sound, pad bindings, gun rate/cadence + in-flight muzzle alternation, rocket
one-per-pull/cooldown feel, weapon-selector feel, E35 gauges, E37 reticle, C27 (crash-vs-facade).
**Key findings:** water impacts show *nothing* — the sea has no collider, so rounds raycast through
(grilled confirmation; supersedes the earlier headless "splash works" reading); the fat orange→grey
rocket smoke trail is missing (the deferred `MODEL_ANIMATION` trail); the muzzle flash is too large and
never rotates; damage-stage smoke/fire never render in flight; and the `det==0` throw on the `kkgate`
door coincides with the door not moving / keeping its collider — likely one bug, the exception aborting
the death sequence. The gun "sync" complaint is weapon-lab-only (`WeaponLab.FireVolley` fires every
muzzle at once; flight alternates correctly). Doc-only change; no engine code touched.

**Milestone 3 plan archived.** Every checklist item is landed (Waves A–F) or deferred to M4
(B19/B20/E38) / superseded by PLAN-testing (F40/F41), so `PLAN-M3-weapons.md` moved to `docs/plans/`
with a `COMPLETE` banner and a `plans.md` row. `PLAN-testing.md` is now the sole active plan (its
"queued behind M3" banner cleared); CLAUDE.md's "Current status" + charter and the reference links
across `backlog.md` and the archived plans were repointed to the new path. M3's remaining sign-off is
the at-the-controls re-tests owed in `playtest.md` §1 (user-owned).

## 2026-07-25 — PLAN-testing B13: the `CSVM.Tests` xUnit project

**What landed.** A new `CSVM.Tests/` (net8.0, xunit 2.9.0, `ProjectReference` to `CSVM.csproj`, added
to `CSVM.sln` with the two Export configurations mapped but not built), so `dotnet test CSVM/CSVM.sln`
runs 135 tests. **No `CSVM/src/` file was touched** — the audit that opens the item found enough
already-free surface that no seam had to be cut. Godot-free at runtime: `Zrdr`/`ZrdrDict`, `WavFile`,
`SoundDefs`, `WeaponDefs`, `Messages`, `MissionTargets`, `SessionPaths` (no `using Godot` at all), plus
`GameZ`, `MarkerRig`, `AnimDefs`, `PlaneStats`, `SpawnPoints`, `PaintScheme`, `AnimProgram`, which use
only managed structs (`Vector3`/`Basis`/`Transform3D`/`Mathf`/`Color`). Partly free: `StockLoadouts.Load`
given an explicit path, and `TextureArchive`'s constructor / `FindByDecalIndex` / `IsKnownAbsent`.
Engine-only, left to B12: `Loadout.Bind`, `Weather.Load` (a `GD.Print` per zone on every load),
`SoundArchive`, `Config`, `HudMetrics`, `DestructibleRegistry`, `TextureArchive.Find`.

**Two input kinds.** Committed `fixtures/` are hand-authored from `docs/formats/` — invented `probe_*`
node names, `wep_probe_*` ids, `MSG_PROBE_*` keys — and WAV/ADPCM inputs are assembled byte by byte in
`WavFileTests.cs` with each expected sample derived in a comment. Nothing is copied from an extraction.
Golden invariants read the player's install through `CSVM_DATA_ROOT` (a checkout holding `extracted/`,
the engine's convention, *or* the extraction tree itself — both probed) and record numbers only: 220
shared readers, the per-chapter reader counts, 48 weapon defs with zero unhandled keys, 1023 messages,
2715 sound defs / 488 sound groups, the 11 airframes' 8-firepoint / 8-pylon / 1-target rigs with two
`markers.md` coordinates spot-checked, and every stock loadout's `wep_*` ids resolving.

**Verified.** Rule 14 ritual: perturbing one expected ADPCM sample (116 → 117) failed exactly that test
and returned `$LASTEXITCODE` 1; reverting returned 0. `dotnet test` green both ways — with
`CSVM_DATA_ROOT` on the real tree **135 passed / 0 skipped / 0 failed, exit 0**, and against an empty
directory **119 passed / 9 skipped / 0 failed, exit 0** (skips named individually by the runner).
`dotnet build CSVM/CSVM.sln` still builds the game project with 0 warnings / 0 errors.

**One finding the goldens produced.** A directory listing counts 222 shared readers but the loader
yields 220: `dlgMessage` and `mp_dialog` extract to a literal JSON `null`, so they carry no reader
list — likewise C1C's and C2B's `templates` (29→28, 26→25). Not a parser gap; recorded in the test's
own comment so the discrepancy is stated rather than silently absorbed.

## 2026-07-25 — PLAN-testing A1: `GameClock`, the session simulation clock

**Landed.** New `CSVM/src/Utils/GameClock.cs`: one object per session owning how much sim time a
rendered frame is worth. Three run modes plus an orthogonal halt — **Realtime** (one step at the
wall delta, arithmetically what every consumer used before, so the shipped modes are unchanged),
**FixedAccum** (whole 1/60 s steps from a clamped wall accumulator — the interactive anim lab's old
accumulator, moved), **FixedStep** (exactly one fixed step per rendered frame — a scripted lab run,
and the new `--det`). Every sim consumer now takes dt from it: `AnimRuntime` (one `Advance` per
sub-step, never a summed step), `TextureCycler`, `Puffer`, `CloudPuffs`, `ProjectilePool`,
`FlightController` (sim + the prop/wing-light/control-surface animators + `FlightAudio`),
`WeaponLab`, `DamageLab`. UI and camera code deliberately stays on the raw delta, so a halted world
can still be looked at and the HUD still draws. `_PhysicsProcess` bodies became `public SimStep(dt)`;
`GameClock.PhysicsDt` returns 0 in any non-realtime mode and `PlaneViewer.DriveSimSteps` calls them
itself in the old tree order (pool → controllers; weapon lab → its own pool). `FlightController`'s
`_paused` is gone — P / gamepad Start (still `AllowPause`-gated, so splitscreen cannot freeze the
shared world) toggles `GameClock.Halted` from `_Process`; `PlaneViewer._UnhandledInput` binds P and
`.` for freecam/viewer; the anim lab's transport routes P/`.`/speed through the clock. New `--det`
flag = fixed-dt clock and nothing else yet (A4 makes it the full bundle).

**Verified.** *Inertness (headline):* `--viewer --plane=player_bhawk` at `--frames=30`, raw
32-bpp pixel buffers md5'd (rule 36) — baseline build `7c2b7274…`, twice; refactored build the same
hash. *The compare can fail (rule 15):* the same clock forced to `Scale = 2` moves **88.27 %** of the
pixels of a `--det --fly` C1 shot at `--frames=180`, against a **1.60 %** same-build floor; reverting
returns to 1.75 %, inside the floor. *Fixed-dt:* two `--det` scripted `--hold` flights over C1 log
**15 of 15 identical** telemetry lines. That check alone cannot fail (Godot's physics tick is already
60 Hz), so the discriminating control is render-rate independence: at `--max-fps 30`, 900 rendered
frames give **15 sim seconds and the byte-identical final pose** under `--det`, versus **30 sim
seconds and a different pose** without it. *Regression:* 8-chapter `--freecam` (`--quit-after 200`,
full-stderr grep per rule 61/71) — 0 errors, every gamez-node / mesh-instance / collider /
uv-clamped-surface count identical to the pre-change build; the only log diff is warning-backtrace
line numbers. Mode battery clean (0 errors): stunt, 4P race, weapon lab firing, damage lab, menu,
firing flight, `--dump-weapons`/`--dump-loadout`/`--dump-markers`, `--effects-test`,
`--weapon-test`, `--damage-test` (its 4 `det == 0` errors are the open backlog item, pre-existing).
`--det` drives both projectile pools from `_Process` and still logs impacts (8, the log cap).

**Residuals.** The interactive halt/step keys are verified **by construction only** — live keypresses
are not scriptable here (`docs/verification.md`, "what this project cannot verify itself"). Audio: the
own-plane engine/whine/rattle loops pause via `StreamPaused`; one-shots and world ambience play out.
Shader-driven motion (UV scroll, precipitation, skydome) still runs on wall `TIME` until A2, so a
`--det` world screenshot is not yet byte-identical — measured 1.60 % floor. In `--anim-lab` only,
CPU-driven texture cycles and puffer particles now follow the lab clock instead of wall time (they
freeze on pause and scale with the speed selector) — the item's intent, not inertness drift.

## 2026-07-25 — PLAN-testing A2: `csky_time`, the clock-driven shader clock

**Landed.** Every animated shader this project generates now reads a Godot global uniform
`csky_time` (seconds) instead of the `TIME` built-in. New `src/Utils/ShaderTime.cs` owns it:
`RegisterGlobal()` joins the fog / world-light / `WorldLights` adds in `PlaneViewer._Ready`, and
`Advance(clock, delta)` publishes `GameClock.Time` once per rendered frame from `_Process`, right
after `BeginFrame` — falling back to a wall accumulator when there is no session clock (the
launchscreen, the frame after `ReturnToMenu`) so menu-side animation never stalls. The uniform is
declared once in a new `CSVM/shaders/csky_time.gdshaderinc` and wraps at 3600 s, the same period as
Godot's `TIME`, because every UV scroll rate in this install (0.07 / 0.4 / 0.5 / 0.7 / 1.0 u/s)
times 3600 is a whole number of texture repeats. Two conversion sites, not three: `SceneBuilder`'s
UV-scroll variant (which also builds the skydome's scrolling sky layer — the plan's separate
"skydome `TIME`" site does not exist; a whole-tree grep found `TIME` only in `SceneBuilder` and
`Precipitation`) and `Precipitation`'s self-animating field, which has no C# per-frame hook at all.
`--jitter` now defaults to 0 under `--det`.

**Verified.** *Headline, within the new build:* `--freecam --chapter=C1 --det --no-pads` framed on
the C1 waterfall (`--campos=-7720,60,-3380 --lookat=-7868,40,-3449`), two runs at `--frames=120`,
raw 32-bpp pixel buffers (rule 36) — the falls region is **0 of 28,000 px different**; the
**pre-A2 build at the identical pose and frame count moved 30.3 % and 31.6 %** of that region
between runs (rule 15's able-to-fail control, taken on the unchanged binary). Frame-count
sensitivity: 120 vs 121 moves **22.7 %** of the region, 120 vs 150 **43.6 %**. *Halt is a true
freeze:* with the clock halted from session start, frames 120 and 300 of the same waterfall pose
are **md5-identical** (`e48772dd…`, 0 of 921,600 px) where a running clock moves 2.26 %. *Rollover:*
publishing `t + 3600` and `t + 7200` renders the falls **pixel-identically** to `t`; `t + 1234.5`
moves 43.8 % and `t + 3599.99` moves 16.8 % — the seam is exact, not a coincidence of a still pose.
*Precipitation:* C2B rain is byte-identical at `--frames=120` across runs once its particle seeds
are pinned (probe: `md5 1e6707d4…` twice; 32.9 % different at `--frames=150`). *Inertness:*
`--viewer --plane=player_bhawk` is md5-identical (`7c2b7274…`) on the pre-A2 and A2 builds — no
scroll variant, so no include and no shader-text change. *Jitter:* `--det --shots=3` writes three
md5-identical frames; without `--det` the same command still dithers (30.6 % between `_00` and
`_01`). *Regression:* 8-chapter `--freecam` (`--quit-after 180`, full-stderr grep per rules 61/71) —
**0 errors**, every gamez-node / mesh-instance / collider / uv-clamped-surface count identical to
the A1 baseline; no shader, uniform or compile diagnostic anywhere. Mode battery clean (0 errors):
menu, anim lab, flight, 2P stunt race, damage lab.

**Residuals.** `--det` shots are byte-identical only where nothing unseeded draws: the C1 waterfall's
**mist puffer** still moves 0.52 % of the frame (all of it inside a 150×120 px box at the falls' base)
and precipitation's per-instance seeds move 4.75 % (C2B rain) / 6.27 % (C4 snow) — both are A3's
master seed, not the shader clock. The halt was proved with a scripted probe that sets `Halted` at
session start; the interactive **P / `.` keys remain verified by construction only**. Separately, a
**pre-existing** `!is_inside_tree()` error fires once during C3's animation bind
(`AnimRuntime.OneShotSoundPosition`, world subtree not yet in the tree) whenever sound is enabled —
reproduced on the unchanged pre-A2 binary; A1's baselines missed it because they ran `--mute`.

## 2026-07-25 — PLAN-testing B11: `Log`, categories/levels + the always-on file sink

**Landed.** `src/Utils/Log.cs` — `Log.Info("world", $"…")` / `Warn` / `Error` / `Debug` over nine
categories (`anim world flight weapons sound perf test ui core`) and four levels, with two sinks:
the console (filtered by `--log=cat[:level],…`) and a **file sink that always writes everything**
to `.scratch/logs/<mode>-<stamp>.log`, PID-suffixed on a same-second collision. Line grammar
`[cat] message key=value`, the file prefixing a 5-char level token and **no timestamp column**, so
a `--det` run's log is byte-identical run to run. Messages are `FormattableString`s rendered with
`InvariantCulture`; the sink is a line-flushed `StreamWriter` (UTF-8 with BOM).

**Census correction.** The plan budgeted "184 `GD.Print` sites across 29 files" (grepped 2026-07-24);
the real figure on the day is **223 across 37 files** — M3's landing added ~40, and `PlaneViewer.cs`
alone holds 98 of them. The plan's 8 categories had no home for the ~24 UI/lab sites, so a **ninth
category `ui`** was added rather than widening `core`: `core` is the session/CLI/config spine a
scripted run always wants, while the lab and launchscreen dumps are read on purpose and must be
silenceable on their own.

**Two deviations from the item text, both to protect inertness.** (1) The console default is
`info`, not "errors and warnings only" — that is exactly what an unconverted `GD.Print` showed, so
converting a site changes nothing you see; `--log=*:warn` spells the quieter shape when wanted.
(2) `--debug-anim` implies `--log=anim:debug,sound:debug`, because it already opens those families'
call-site gates and would otherwise half-work while their 35 sites are unconverted.

**Migration converted 5 files / 14 sites and no more, by decision** (plan decision 9, verification
rule 67 — bulk text rewrites have corrupted files here): `Utils/Config.cs` (`core`),
`Mech3/Clutter.cs`, `Mech3/TextureArchive.cs`, `Flight/Weather.cs` (`world`),
`Mech3/TextureCycler.cs` (`anim`, debug level). Each site keeps its original level except
`TextureArchive`'s two, whose own comment recorded that the level had been compromised to dodge
`GD.PushWarning`'s stack trace. `WorldSounds.cs` was converted and then reverted when it landed on
another agent's active file list. `PlaneViewer.cs` took three lines only: the `--log=` arg, the
deferred-spec application, and `Log.Open`.

**Verified.** *The headline property, both directions.* `--debug-anim --log=anim,world:warn`:
`DEBUG [anim] texture cycles` = **34 in the file, 34 on the console**; `INFO [world] weather zone`
= **2 in the file, 0 on the console**. `--debug-anim --log=anim:info` (call-site gate open, console
filter closed): the same anim lines are **5 in the file, 0 on the console** — the instrument is seen
able to fail either way. *Caught error in both sinks:* a deliberately malformed `res://config.json`
put `ERROR [core] config is not valid JSON …` on stderr and the same line **plus the full
`JsonReaderException` stack trace** in the file — and it appears *before* the `log file=` line,
proving the pre-open prelude flushes in order. *Crash safety (rule 65):* a live `--debug-anim`
freecam run `Stop-Process -Force`d (TerminateProcess — no unwind, no `Dispose`) left a **1562-byte,
18-line** log holding all 13 per-second `[anim]` ticks up to ~1 s before the kill. *Cost:* `--perf`
medians over 30 samples, C1 `--det` freecam — same-build noise floor first (rule 7) is **1.14 ms of
`script`** (baseline 19.11 vs 17.97); baseline→converted is **17.97 → 17.54 ms**, i.e. −0.4 ms,
*inside* the floor and in the impossible direction, so no measurable cost (rules 37/41; `frame` sat
pinned at 16.67 ms, the vsync floor, rule 38). Even the run with the per-frame debug site firing
reads 18.33 ms, still inside the floor. *Locale:* the same process renders unconverted
`ZoneFog … WorldLight = 0,80200005` and converted `[world] … world_light=0.802` — and `LogTests`
asserts `Log.Format($"dt={1000f/60f:0.000} ms")` == `dt=16.667 ms` under a forced `de-DE`, with the
able-to-fail control that plain interpolation there yields `16,667`. *Inertness:* baseline vs
converted `--freecam --chapter=C1 --det --spawn=0 --no-focus` with no `--log=` differ by exactly the
four rewritten lines (same count, same positions, same levels bar the documented `WARN` promotion)
plus **one new line**, the `[core] log file=…` announcement; world counts identical
(7064/3458/0/1483). *Regression:* 8-chapter `--freecam` — **0 Godot-format engine errors** in every
chapter, warning counts identical to baseline on C1.

**Tests.** `dotnet test CSVM/CSVM.sln` **142 passed / 0 failed** (135 + 7 new `LogTests` covering
the filter grammar, the line grammar and the invariant rendering — all Godot-free, per B13's audit).

**Residual.** A composite object's own `ToString()` escapes the invariant rendering
(`FormattableString` only reaches `IFormattable` holes), so a record logged whole still emits
current-culture floats — `Weather` now logs the `ZoneFog` fields individually for exactly this
reason, and the constraint is recorded as a `⚠` in the module's `docs/architecture.md` entry.

## 2026-07-25 — PLAN-testing A3: one master seed, ten subsystem RNGs

**Landed.** New `src/Utils/Rng.cs` owns every random draw in a session. One master seed; each
subsystem gets its own generator seeded `splitmix64(master ^ fnv1a(name))` — **independent across
subsystems**, so adding a draw in the weapons code cannot shift what the liveries roll, and only
call order *within* a subsystem matters (which A1's fixed clock pins). The hash is hand-written on
purpose: `string.GetHashCode()` is randomized per process in .NET and would have defeated the whole
item. `Rng.Reset(master, pinned)` runs at the top of `StartSession`, before anything draws, so an
in-process rebuild (Esc to the launchscreen and back) *repeats* the run instead of continuing it;
it also calls `GD.Seed(master)` as the net for any draw not yet routed through a named stream.

**Ten subsystems, where the plan named four.** `weapons` (`ProjectilePool` holds the stream —
`CANNON_SPREAD` is two draws per round; also the stand-in fireball), `flightaudio` (the
`snd_exp_plane1..4` crash pick, which now also prints `crash sound: <name>` — the pick's only trace
outside the speakers), `spawn`, `paint`, `anim`, `crash`, `effects`, `puffer`, `clouds`, `precip`.
The six beyond the plan's list came from an audit of every draw in the tree: the **world**
`AnimRuntime` had `Seed` machinery but `PlaneViewer` wired `RuntimeSeed` only in `--anim-lab`, so
`--fly`/`--freecam` ran the world's `RANDOM_WEIGHT` dice, `SOUND_GROUPS` picks and crash-debris
scatter unseeded; the world-effects runtime was seeded only under `--effects-test`; the per-player
crash rig is a third `AnimRuntime` nobody had listed; and `Puffer`/`CloudPuffs`/`Precipitation` each
held a bare `new System.Random()`. `UI/LiveryLab` deliberately keeps its own non-sim generator — it
is driven by a button press, and the plan is explicit that an input-dependent path must not share a
sim stream.

**The trap seeding alone does not fix.** `SoundDefs.SoundGroup._last` is recency state living
*outside* the RNG — `DYNAMIC_WEIGHTS` halves the last pick's weight — so re-seeding a runtime
replays a different sequence. New `SoundGroup.ResetRecency()` / `WorldSounds.ResetGroupRecency()`,
called by `AnimRuntime.Reseed()` in the same breath as the re-seed. A fresh session was already safe
because `LoadGroups` parses new objects per session; the lab's Play/Restart was not.

**CLI.** `--seed=N` was the animation lab's own seed and is now the session master (the lab's
display and `AnimLab.DefaultSeed` widened to `ulong` and derive from it). Pinned to 1 by `--det`,
`--anim-lab` and `--effects-test` — the first two because determinism is their point, the third
because its census gates effects behind `RANDOM_WEIGHT` and has to be comparable run-to-run.
Everything else draws from the clock and logs `rng: master seed N`, so an interesting unpinned run
can be replayed by passing the number back. `--paint-seed=N` still overrides the derived paint seed
alone.

**Verified.** All pixel numbers are raw 32-bpp buffers (rule 36), two runs at the same `--frames`,
baselines taken on the unchanged pre-A3 binary. *The three residuals A2 handed over:* C1 waterfall
mist **0.47 % → 0.00 %** (md5 `b456fdf5…` three times, whole frame, `--freecam --chapter=C1 --det
--campos=-7720,60,-3380 --lookat=-7868,40,-3449 --frames=120`); C2B rain **5.44 % → 0.00 %**
(`cbaabff3…`); C4 snow **25.84 % → 0.00 %** (`e2f9edad…`). *Rule 77 retired:* two `--det` C1B dives
with `--fire` log **8 of 8 identical impact positions**, where the pre-A3 build's pair of runs shared
**none**; the two flights' full logs are identical apart from one wall-clock `focus: lost` line.
*Crash sound:* seed 1 twice → `snd_exp_plane4` twice; `--seed=2` → `snd_exp_plane2`, `--seed=5` →
`snd_exp_plane3` (both halves of the check — a seed that changes nothing would be as broken as one
that pins nothing). *Seeds branch:* `--seed=2` moves **25.09 %** of the C4 snow frame and 0.44 % of
the waterfall against seed 1. *Inertness (the boundary rule):* three unpinned `--fly --chapter=C1B`
runs drew three different masters, spawn indices 2/0/3, and three different impact patterns; a
random-livery `--viewer` shot moves **3.39 %** of pixels unpinned and **0.00 %** under `--det`; a
plain `--viewer --plane=player_bhawk` shot is md5 `7c2b7274…`, byte-identical to the A1 and A2
baselines. *Regression:* 8-chapter `--freecam --quit-after 180`, **sound enabled** (rule 61 full-
stderr grep) — one real `ERROR:` in the set, C3's known pre-existing `!is_inside_tree()` during the
sound bind; gamez-node / mesh-instance / collider / uv-clamped-surface counts identical to the A2
baselines on all four chapters with prior logs. Mode battery 0 errors: menu, anim lab, 2P stunt
race, weapon lab firing, `--weapon-test`, damage lab, `--damage-test`, `--effects-test` (whose
census is identical across two runs on the derived seed). `dotnet build` 0/0; `dotnet test` 135/135.

**Two pre-existing findings confirmed, not caused here** (rule 13, A/B'd against a pre-A3 binary
rebuilt from file copies — never `git stash`, and the absent `rng:` line proved which binary ran):
the `1 ObjectDB instance was leaked at exit` warning under `--quit-after` appears in A2's own logs,
and every `--dump-*` run logs one `!global_shader_uniforms.variables.has(p_name)` error because the
dump branches quit before `_Ready` registers `csky_time` — an A2 residual, reproduced on the pre-A3
build.

**Residual.** A `--det --fly` *screenshot* is still not byte-identical: **2.71 %** of pixels, mean
delta 1.08. The simulation matches exactly; `FlightController.UpdateChaseCamera` smooths on the raw
wall delta, which is A1's deliberate "UI and camera code stays off the sim clock". Pin flight
captures with `--freecam` or a fixed camera; C23's goldens must avoid chase-cam poses until that
camera moves onto the clock. Filed in `backlog.md`.

## 2026-07-25 — Global shader parameters registered before the dump branches

**What landed.** The `csky_fog_*` / `csky_world_light` / `WorldLights` / `csky_time` registrations
moved above the `--dump-markers` / `--dump-weapons` / `--dump-loadout` early exits in
`PlaneViewer._Ready`. Those branches build materials of their own and then quit, so registering
after them left every dump run emitting one
`!global_shader_uniforms.variables.has(p_name)` error — noise that would have failed B12's suites
on their first green run.

**Verified.** A/B on the one moved block, windowed: **1 → 0** occurrences on `--dump-loadout`, with
all three dump tools at 0 after. The same A/B under `--headless` reads **0 → 0** — the dummy
renderer compiles no shaders and cannot see the error at all, which is why it survived A2's
verification and A3's mode battery. Landed as verification rule 82. Build 0 warnings / 0 errors.

**Trap met on the way.** The first A/B "passed" on both sides because the file swap's `dotnet build`
no-opped (rule 11) — the reverted file kept a stale mtime and Godot ran the old DLL. Forcing the
rebuild is what made the difference appear.

## 2026-07-25 — PLAN-testing A4: the `--det` bundle; scripted runs imply it

**What landed.** `--det` became one named bundle resolved in a single block of
`PlaneViewer._Ready` (after `Log.Open`, ahead of the jitter and master-seed resolution it feeds):
fixed-dt clock + master seed 1 + `--spawn=0` + pinned livery seed + `--no-pads` + `--jitter=0`,
each constituent still overridable by passing its own flag. **`--screenshot=`, `--dump-markers`,
`--dump-weapons`, `--dump-loadout`, `--dump-config` and `--damage-test` turn it on themselves**, so
a reproducible capture needs no other flag; **`--no-det`** beats both the implication and an
explicit `--det`. The whole resolved set is announced on one line —
`[core] det clock=fixed dt_ms=16.667 seed=1 spawn=0 livery_seed=… pads=off jitter=0 via=--screenshot`
— and the saved-shot line now carries `sim_frame=` / `sim_time=`, so a capture documents the moment
it shows. The per-session `det: fixed-dt sim clock` print was folded into the bundle line.

**Verified.** *Headline:* a bare `--screenshot` over the C1 waterfall pose, twice, no other flags →
md5 `bbb18fec…` **=** `bbb18fec…`, **0 of 921600 px differ**; at `--frames=120` md5 `b456fdf5…`
twice, 0 px. The compare can fail: frame 15 vs frame 120 differs **2.24 %** (max delta 170), so the
pose is genuinely time-sensitive (rule 80), and the same pair under **`--no-det` differs 0.47 %**
(max delta 77). *Each implication individually* (rule 12): all five non-screenshot flags log the
`det` line with the right `via=`, and `--no-det` cancels each one — `--det --no-det` and
`--no-det --det` both run on the wall clock, `--no-det` alone logs nothing. *Overrides:*
`--dump-config --seed=7 --spawn=2 --paint-seed=99 --jitter=0.5` announces
`seed=7 spawn=2 livery_seed=99 jitter=0.5`. *Inertness:* an interactive `--fly --players=2` logs no
`det` line, an unpinned master (`18165887919824763418`), spawn `#1 of 4`, and
`player 1 input: keyboard + pad 0` — the same run with `--screenshot` logs `pads=off` and
`player 1 input: keyboard`, so the pad check is able to fail. *`--frames=N` tightened:*
`sim_frame=15 sim_time=0.25` and `sim_frame=120 sim_time=2` under the bundle versus
`sim_frame=15 sim_time=0.435` under `--no-det` — N is now a sim coordinate. *Modes:* `--viewer`
(`7c2b7274…`), `--anim-lab` (`2b20bb13…`) and the damage lab with fires burning (`3e3d0faf…`) are
each md5-identical across two runs, 0 errors; `--stunt` and `--menu` shot cleanly. *Regression:*
8-chapter `--freecam --quit-after 240`, **sound enabled**, windowed (rule 82) — **0 errors in seven
chapters, 1 in C3**, the known pre-existing `!is_inside_tree()` sound bind, reproduced on a
HEAD-restored binary in the same session (rule 13; the `1 ObjectDB instance was leaked at exit`
shutdown warning appears identically on both builds in C1B/C1C/C2/C3/C5). `dotnet build` 0/0;
`dotnet test` **142/142**.

**Residual — the headline's literal form fails, and honestly so.** A bare `--screenshot` with *no*
other flags is a **flight** run (flight is the default for any content arg), and A3's chase-camera
residual stands: measured **29.38 % at frame 15, 3.21 % at frame 120, 32.70 % at frame 300**, mean
delta 1.1–2.4, while the sim itself matches (two runs' logs identical bar the filename).
`FlightController.UpdateChaseCamera` smooths on the raw wall delta by design. Byte-identity is
therefore a `--freecam` / `--viewer` / `--anim-lab` property; C23's goldens must still avoid flight
poses. Filed in `backlog.md`. Landed as verification rule 83.

## 2026-07-25 — The chase camera moves onto the sim clock

**What landed.** `FlightController._Process` feeds `UpdateChaseCamera` the `GameClock`'s `FrameDt`
instead of the raw frame delta. The halted orbit camera keeps wall time, deliberately — the point of
a freeze is to fly the camera around a stopped world.

**Why the earlier rule was too wide.** A1 put "UI and camera code" on the raw delta so a halt still
lets you look around and the HUD still draws. That is right, but it only has to hold *through a
halt*: the chase camera smooths with `1 - exp(-k·dt)`, so its pose is a function of the dt it is fed,
and on wall time a scripted flight capture stayed frame-rate dependent even with the simulation
underneath it pinned. Being simultaneously a view and a function of sim state, only its second half
wanted the sim clock — and that half is the whole of what a capture sees.

**Verified.** A bare `--screenshot` C1 flight, two runs at each of frames 15 / 120 / 300:
**0 of 921,600 px** differ at all three, against the **29.38 % / 3.21 % / 32.70 %** A4 recorded the
same command producing. Controls both ways: frames 15 vs 120 differ by 50.26 % (the pose is
genuinely time-sensitive, rule 80), and `--no-det` still moves 54.67 %, so the opt-out survives.
Inertness: the parked `--viewer` shot is md5 `7c2b7274…`, identical to the A1, A2 and A3 baselines —
in Realtime mode `FrameDt` *is* the wall delta, so nothing outside a fixed clock changed. Build 0
warnings / 0 errors, 142 tests green.

**Consequence.** A4's headline verify — a bare `--screenshot` twice is md5-identical — now holds
literally, and C23's goldens are no longer restricted to `--freecam`/`--viewer` poses. The
`backlog.md` entry is deleted rather than marked fixed, per the standing rule.

## 2026-07-25 — PLAN-testing B12: `--run-tests`, the in-engine test harness

**What landed.** `CSVM/src/Testing/` — `Probes.cs` (the assertion cores that used to live inside the
`--dump-*` / `--damage-test` handlers, now returning report text *and* a structured verdict),
`TestHarness.cs` (`--run-tests[=filter]`: suite registry, `TestContext` with the assert verbs, the
resolved data paths and a `WorldSession`-backed chapter-world builder, a PASS/FAIL/SKIP table,
`.scratch/test-report.json`, and the exit code) and `Suites.cs` (seven suites). The dump flags keep
working and are now thin wrappers over the probes — one source of truth instead of a copy;
`PlaneViewer.cs` shrank by ~430 lines. `WeaponLab.RunSelfTest` gained a counted twin `SelfTest()`.

**Suites, measured green on the retail install (C1, 16.0 s wall, exit 0):** `weapons-defs` 48 defs /
0 unhandled keys · `markers-rig` 11/11 airframes · `loadout-bind` 11 bound / 0 failed ·
`weapons-fire` 48/48 fired, 0 errors, 0 skipped · `damage-stages` 16 defs, every one resolving from
a deep descendant and firing a stage effect · `damage-hd` 16 defs, all destroyed, all
reset-and-rekill idempotent · `destructible-census` all 8 chapters against the committed table
(C1 267/196 … C5 568/292), read from the registry rather than the 16-def-capped sweep rows.

**Verified.** Rule 14 both ways. *Planted:* C1's census expectation flipped to 268 →
`FAIL destructible-census … !! C1 destructible instances expected=268 actual=267`, `$LASTEXITCODE`
**1**; reverted → PASS, exit **0**. *Real bad input:* `--run-tests=loadout-bind
--loadout=pbloodhawk` → FAIL naming the genuine binding error (`marker 'firepoint8' not found on
the built plane`), exit **1**. `--run-tests=weapons` selects 2 of 7 suites. `--data-root=<empty>` →
**0 passed / 0 failed / 7 skipped**, each naming its missing path, exit **0** — a skip is reported
distinctly and never as a pass. A filesystem sweep after a full run found **nothing** written
outside `.scratch/`. The refactor is output-preserving: `weapons_dump.txt`, `markers_dump.txt`,
`world_colliders.txt` and a filtered `loadout_dump.txt` are **md5-identical** to the pre-refactor
files; `damage_test.txt` differs only in `HEALTH 0,01` → `HEALTH 0.01` (the probes now force
`InvariantCulture`, which `--damage-test` never did). Smoke: `--weapon-test` 48/48,
`--effects-test` 28/28 resolved / 16 puffers, `--freecam --chapter=C1 --det --screenshot` 0 errors.
`dotnet build` 0 warnings / 0 errors; `dotnet test` **149 passed / 0 failed** (142 + 7 new).

**The error-screen decision, taken before writing the suites.** Native `ERROR:` lines are C++
`ERR_FAIL_COND` prints and cannot be intercepted from C# at all, so a strict full-stderr criterion
was never available in-process. The harness instead reads the run's own engine log back
(Godot's `--log-file`, else the project's default rotating log when this run is the one writing it;
with neither it reports SKIP, never PASS) and classifies every error line against a **capped**
allowlist: `det == 0` (max 8) and `!is_inside_tree()` (max 4), each naming its open backlog item.
Unknown error fails, over-cap fails, and **every allowance's actual count is printed even on a
pass** — the measured full run reads `allowed 0/8x` and `allowed 1/4x`. The classifier is pure and
carries 7 xUnit tests including "one over the cap fails". Rules 84 and 85.

**Two live rule-74 bugs fixed on the way.** `--weapon-test` and `--effects-test` wrote their reports
through relative `./.scratch/` paths, which resolve against the *process* working directory; the
evidence was a stray `CSVM/.scratch/` holding 24 MB of misplaced probe artifacts. Both now use the
absolute `WriteScratch` helper, and the file sweep above confirms it.

**Finding — the `det == 0` errors are not what `backlog.md` said.** Measured: they are printed
**after** the sweep has finished and both reports are written (lines 55–61 of a 62-line C2 log), so
they cannot be aborting a death sequence that already reported its swap, colliders and seven stage
effects; they survive `--mute`, where `AnimRuntime.Sounds` is null (4 → 4 on C2), ruling out
`WorldSounds`; the continuous-sweep mode never produces them (0 on C1 and C2) while the
clock-ticking discrete mode does; no single def group reproduces them (`kkgate`, `sign*`, `fcpan*`
each 0, the unfiltered 16-def sweep 4); and `--run-tests=damage-hd --chapter=C2` produces a
**byte-identical report with 0 errors**, because the harness frees its world before the frame that
emits them. The named suspect `AnimRuntime.cs:~2666` is ruled out on the earlier grounds that a
managed `Basis.Inverse()` cannot print a `core/math/basis.cpp` location. Leading suspect is now
`Effects/Puffer.cs`'s per-frame `GlobalPosition` sets on emitters the deaths created.
`backlog.md`'s entry and playtest finding 13 are corrected; the transferable half is rules 84–86.

## 2026-07-25 — `--pos`/`--direction`: one placement pair in every mode (PLAN-testing A5)

**What landed.** `--pos=x,y,z` and `--direction=x,y,z` place the *subject of whatever mode is
running* — the camera in `--freecam`/`--viewer`/`--anim-lab`, the plane in `--fly`/`--stunt`. One
`ResolvePlacement` block in `PlaneViewer._Ready` routes them onto the plumbing that already carried
placement (`_spawnAt`/`_spawnDir` in flight, `_camPos` plus a new `_camDir` elsewhere), so no
consumer downstream decides anything. `--campos`/`--spawn-at`/`--spawn-dir` survive as deprecated
aliases that log `WARN [core] deprecated flag=… use=…` once per run. F11 became `PrintPlacement` and
now prints the subject per mode. `ChooseSpawn`/`LogSpawn` moved onto `Log` in passing — the German
locale had been rendering the spawn direction as `dir=(0,97,-0,24,0,00)`, which is unreadable in
exactly the log line this item's parity check compares.

**Two decisions the plan's approach did not cover, both forced by the audit:**

- **`--lookat` stays a POINT; only flight converts it.** `SpectatorCamera` keeps just the direction
  to its look-at, so either form is lossless there — but `OrbitCamera.Frame` treats it as a true
  pivot *and derives the orbit radius from it*, so converting at parse time would have broken the
  `--viewer` wheel and drag. A `--viewer --direction` therefore gets a **synthesized** pivot: the
  point on the aim ray nearest the plane's AABB centre (min radius 1 m), or the AABB centre with the
  eye swung to the aim when no `--pos` was given. It is announced on its own log line, because a
  pivot nobody typed is what a later capture cannot explain.
- **In `--freecam`/`--anim-lab`, `--pos` beats `--spawn-at`.** The two already overlapped there
  (`--spawn-at` reaches the camera through `ChooseSpawn`, `--campos` layered on top). The deprecated
  flags keep their *old per-mode meaning* rather than becoming pure renames: `--campos` still never
  places the plane, and `--spawn-at` still moves the anim lab's parked stage prop as well as the
  camera — which `--pos`, placing only the camera there, deliberately does not copy.

**Verified.** *Alias equivalence:* a C1 `--freecam` shot from `"--pos=-6200,500,-3300"
"--direction=0,0,-1"` is md5-identical (`7facfce6…`, decoded pixels — rule 36) to the same pose as
`--campos`/`--lookat`; moving one coordinate 10 m moves **28.23 %** of pixels, so the compare can
fail (rule 15). Same for `--pos` vs `--campos` alone in `--viewer` (`00695e91…`).
*The motivating case:* `--chapter=C1 "--pos=-6500,300,-1500" "--direction=1,-0.25,0"
"--hold=0,0,0,1" --fire --frames=300` logs **8 of 8 `-> Water` impacts on the first run, no land
crash** — the C1 open water was located from `models.json` by classifying each mesh's dominant
material texture the way `SceneBuilder.SurfaceForMesh` does, not by flying around. *Flight parity:*
the same values via `--spawn-at`/`--spawn-dir` give the identical logged spawn pose
(`pos=(-6500,300,-1500) dir=(0.970,-0.243,0.000)`), the identical 8 impacts and an md5-identical
frame (`e980ff10…`). *The orbit still orbits:* `--viewer --pos=18,6,26 --direction=-0.5,-0.2,-0.75`
reports `pivot --lookat=0.339,-1.064,-0.491 radius=32.613`, and a `--shots=2 --jitter=25` burst
keeps the plane centred — where the degenerate pivot-at-the-eye the synthesis avoids
(`--lookat=<the eye>`) swings it clean out of frame into empty sky. *Deprecation:* `--campos` passed
twice over a 200-frame run logs the notice exactly once, in console and file sink alike.
*F11:* verified through a temporary probe call at the screenshot-save site (live keypresses are
unscriptable here), then reverted — flight printed the **plane's** pose 0.9 m along its track from
the spawn with `--direction=0.97015,-0.24251,-0`, freecam and anim-lab their eye + unit direction,
the viewer `--pos`/`--lookat=<the pivot>`. Also exercised: `--stunt`, `--players=2` (60 m abreast
fan-out intact), `--direction` with no `--pos` in flight (logged no-op), `--lookat` alone in freecam
(unchanged: aims from the mission spawn), `--direction` alone in the viewer.
*Regression:* 8-chapter `--freecam --quit-after 240`, **sound enabled**, windowed (rule 82), flags
absent — **0 errors in seven chapters, 1 in C3**, the known pre-existing `!is_inside_tree()`; node
and mesh counts unchanged. `dotnet build` 0/0; `dotnet test` **142/142**.

**Consequence.** Verification rule 77's chapter-shopping workaround ("pick a chapter whose spawn
sits over the surface you want") is retired for placement-controllable tests; the new standing rule
is 84.

## 2026-07-25 — `RunTests.ps1`: one command, one exit code (PLAN-testing B14)

**What landed.** `RunTests.ps1` at the repo root: `build` (`dotnet build CSVM/CSVM.sln`) → `units`
(`dotnet test`, `--no-build`) → `engine` (Godot `--run-tests`, windowed, `--log-file`) → `goldens`,
plus `perf` under `-Perf`; one summary block, one exit code, nonzero if any stage FAILED. Switches
`-Filter <substring>` (engine suite names only), `-SkipUnits`, `-SkipEngine`, `-Perf`. Counts are
read from machine-readable outputs rather than console prose: a TRX log under
`.scratch/testresults/` for the units, `.scratch/test-report.json` for the engine — the report is
deleted before the run so a dead run cannot be scored from the previous one's numbers. Godot is
resolved this tree first, then `CSVM_DATA_ROOT`, the same fallback `RunGame.ps1` uses.

**Skips are printed, never silent.** No data, no Godot, `-SkipUnits`/`-SkipEngine` all report `SKIP`
and keep the exit at 0, but each adds a `not checked:` line to the summary. The two unwritten stages
report `TODO` with a plain "not implemented yet" — the golden-image compare and the perf A/B — so
even a fully green run says out loud that no pixel and no timing regression is being caught.

**Verified.** *Green end to end:* `build PASS 0.8s · units PASS 2.4s (149 passed, 0 failed, 0
skipped of 149) · engine PASS 15.7s (7 passed, 0 failed, 0 skipped; engine errors clean) · goldens
TODO`, `result: PASS -- 18.8s total, exit 0`. *Rule 14, each stage independently (rule 12):* a
flipped assertion in `ZrdrTests.EveryNumberArrivesAsFloat` gave `FAIL units 148 passed, 1 failed`
and `result: FAIL in units -- exit 1`; reverted, a `WeaponDefCount + 1` in the `weapons-defs` suite
gave `FAIL engine 1 passed, 1 failed … [weapons-defs]` and `exit 1` — each with the other stage
unaffected. *From a worktree* (`git worktree add --detach`, `CSVM_DATA_ROOT` at the primary tree):
identical — 149/149, 7/7, exit 0, `data root: Z:\Crimson Skies (CSVM_DATA_ROOT)`; no
`--headless --import` pass was needed first. *Switches:* `-SkipUnits` / `-SkipEngine` each print
their SKIP row and a `not checked:` line; `-Filter weapons` reached the harness as
`suites=2/7 filter='weapons'`, `-Filter markers` as `suites=1/7`. *Skip path:* `CSVM_DATA_ROOT`
pointed at an empty directory → all 7 suites SKIP, each naming the path it wanted, `0 passed, 0
failed, 7 skipped`, exit 0, with the summary carrying `not checked: 7 in-engine suite(s) SKIPPED`.
*Seams:* `-Perf` printed `TODO perf … not implemented yet -- no scenario set, no A/B, no history
store` and contributed no PASS.

**Two traps found while building it, both now standing rules.**

- **Rule 66's kill has to be scoped twice.** Filtering strays only on this tree's project dir also
  matches a live session: the first two runs each killed two `--plane=player_bhawk --chapter=C1`
  Godots another session had launched seconds earlier (creation timestamps confirmed they were
  fresh, not leftovers). The kill now requires this tree's dir **and** `--run-tests` — a run that
  always quits by itself — and every other Godot on this tree is printed and left alone.
- **Rule 88, new.** With `$ErrorActionPreference = "Stop"`, piping the script's own output
  (`.\RunTests.ps1 | Select-String …`) makes PowerShell 5.1 wrap the child's stderr in
  `NativeCommandError` records: Godot's first allowlisted `ERROR:` line killed the script at the
  launch line, while the identical unpiped run passed. All three native calls now run with errors
  non-terminating and are judged by exit code; cmdlets keep `Stop`, because a silently failed
  `Remove-Item` would score a stage from a stale file.

**Known gap, inherited, not fixed here.** `CSVM_DATA_ROOT` pointed at a directory holding no
extraction skips the *engine* suites (`PlaneViewer` resolves it strictly) but not the
data-dependent *units*: `TestData` falls back to its own checkout, so from the primary tree they
still find `extracted/` and all 149 run. Documented in `docs/tooling.md` — it is the test project's
resolution order, not the script's.

## 2026-07-25 — PLAN-testing C21: startup-phase stopwatches, always on

`src/Utils/StartupProfile.cs` + `Mark`/`Record` calls at the `WorldSession` phase boundaries and
around `PlaneViewer`'s data loads. Every session build now ends in one `perf`-category line:

```
[perf] startup mode=freecam chapter=C1 total=3028.0 boot=1091.0 gamez=548.2 textures=1.6
sounds=0.3 zrdr=15.6 world=461.7 clutter=26.4 anim=270.7 bind=342.6 prewarm=85.0 edge=8.9
weather=28.3 rest=64.5 first_frame=83.3
```

**Shape.** `total = boot + Σ(phases) + rest + first_frame`, an identity a parser can check —
verified 0 mismatches beyond 0.2 ms rounding across 24 collected runs. `boot` is engine start →
build start; `rest` is the build minus its phases (real uninstrumented work, not an error term);
`first_frame` is measured at the top of the *second* `_Process` so the first draw is inside it.
Phases are leaves, never nested, so the sum cannot double-count. Ambient statics rather than an
`Options` field, because `WorldSession` is also driven by the test harness — where `Current` is
null and the eight census worlds record nothing.

**Deviations from the item text.** Three phases beyond the plan's list (`plane`, `weather`, `edge`)
because they are large and mode-specific; `boot` and `rest` added so the line closes arithmetically
instead of asserting an approximation. The runs that quit inside the build (`--damage-test`,
`--effects-test`, `--weapon-test`) never render, so their line is emitted from
`NotificationExitTree` with `first_frame=none`.

**Verified.**

*The residual, named rather than waved at.* `rest` is per-mode and its content was measured, not
argued: freecam 26–65 ms, flight 240–243 ms, viewer 255 ms. A temporary `probe_puffers` mark
(added, measured, reverted) attributed **84.9 ms of the viewer's 255 ms to the damage lab's ten
baked pufftrail emitters**; the balance is the gauge cluster and the livery/mesh/marker/weapon lab
nodes. Flight's 240 ms is the fly-minus-freecam delta: the per-player rig, the shared projectile
pool, the world-effects runtime and the loadout bind. Externally, `total` 3028 ms sits inside a
5355 ms `--quit-after 120` process wall; the remainder is 118 vsync-capped frames (1967 ms) plus
**~330–360 ms of process spawn and shutdown no in-process clock can see** — cross-checked at
`--quit-after 3` (wall 3361, total 2992) and `5` (wall 3389, total 3012).

*The numbers move when they should (rule 15).* A zip-only data root (hardlink mirror, so
`SessionPaths.PreferUnzipped` finds no unpacked siblings) against the normal unzipped tree, C1
freecam ×3 each: `anim` **265–271 → 431–438 ms**, `sounds` 0.2 → 6.9, build total 1831–1862 →
1936–1970. Two phases moved the *other* way and reproducibly — `world` 451–456 → 409–417 and
`zrdr` 15.7–16.5 → 12.3–12.7 — many small loose files costing more than one zip handle.

*Cold vs warm (rule 42, now rule 89).* A freshly-copied C3 data root, first run vs the two after
it: `total` **9777 → 2570/2561 ms**, and the cost is not spread — `anim` **5974 → 278–284 (21×)**,
`world` 1451 → 319–321, `prewarm` 340 → 78–80, while `gamez` did not move at all. A cold run
reshapes the profile rather than scaling it. Comparisons belong in C22's warm-up protocol; this
item only reports.

*Cost is invisible (rules 7/8/41).* Same-build noise floor taken **first**, on the unchanged
binary: C1 `--freecam` ×5 → build 1824/1848/1863 ms (min/avg/max). Instrumented: 1853/1857/1868.
Then the baseline was re-measured by checking the two files back out, rebuilding and re-running ×5
(rule 8; the absence of the `[perf] startup` line is the rule-11 proof the old binary ran):
1774/1828/1847 — the two baselines differ from each other by 20 ms while the instrumented average
sits 9 ms above the first. The arithmetic bound is 12 `Stopwatch.GetTimestamp()` pairs per session,
~1 µs. C4 agrees: baseline 1882/1895/1911, instrumented 1901/1903/1904.

*Coverage.* 8-chapter `--freecam` sweep, **0 engine `ERROR:` lines** anywhere, a startup line on
every chapter; `--viewer`, `--anim-lab`, `--fly` (C5) and `--damage-test` each emit correctly; a
bare launchscreen run emits none (no session built) and is error-free. C5 flight against C5 freecam
shows the split doing its job — `world` 439–449 → 1243–1252 ms (the collision build), `gamez`
523–539 → 781–794 (the second `GameZ.Load` accumulating into the same bucket), `bind` 452 → 652–733.
`.\RunTests.ps1` green: `build PASS · units PASS 149/149 · engine PASS 7/7, engine errors clean ·
goldens TODO`, `result: PASS -- 19.4s total, exit 0`.

**Not verified.** The launchscreen-driven rebuild path (menu → `StartSession`) is verified by
construction only — it is the same `StartSession`, but a live menu launch is not scriptable here, so
the `boot` caveat (it contains the menu wait) is reasoned, not measured. A true cold OS file cache
cannot be forced on this machine without admin cache-flush tooling: the cold reading above is a
freshly-written copy, which is rule 42's own scenario but is a *floor* on the cold penalty, not
necessarily its ceiling.

## 2026-07-25 — Texture drop-in: `--tex-override` + `--tex-census` (PLAN-testing C24)

Two instruments hooked into `TextureArchive.Find`, the one point every consumer resolves a name
through, so world, clutter, aircraft, puffers, clouds and gauges inherit them without knowing.
`--tex-override=<name>[=<color>]` paints one texture flat (default magenta); `--tex-census` gives
every texture its own hashed colour, writes the name→colour map to `.scratch/tex_census.json`, and
counts a `--screenshot` frame into `.scratch/tex_census_<shot>.json`. `TextureCycler` freezes under
either flag, and on an aircraft the drop-in beats the paint substitution. New: `TextureDropIn` in
`TextureArchive.cs`, a `tex-dropin` engine suite, three xUnit tests on the colour hash.

**Verified.** Override on C1/M04's moored zeppelin skin: **113,947 magenta px vs 0** in the same
shot without the flag, all 114,820 changed pixels inside the hull. Census at the same pose:
`lkzepskin` 88,301 px, `cloudlayer` 230,365, `zep_cab02` 478, while four textures that exist only in
C4/C5 read 0/1/2/30 px. Rule 56: an overridden texture's visible extent is 114,820 px with the
census off and 114,819 with it on (the one pixel is a sub-quantum blend fringe the diff method
cannot see, not a geometry move), and a hard-alpha clutter cutout is **pixel-identical**, 803 px in
both. Map stable: two runs md5-equal, and 167/167 names shared between a C1 and a C4 map carry the
same colour. Inert with the flags absent: `--freecam` C1 and C4 shots md5-equal to the pre-change
build, 0 of 921,600 px. 8-chapter `--freecam` regression, sound on, flags absent: 8/8 exit 0, one
`ERROR:` line total, the known C3 `!is_inside_tree()`. `RunTests.ps1` green — 152 units, 8 suites.
Both new checks seen able to fail: the unit tests failed twice on real defects while being written,
and skipping one texel in `Flatten` failed `tex-dropin` on all 7 samples.

**The tolerance decision, and why it needed measuring.** The world shader multiplies the flat by a
**per-channel** vertex colour, so nothing lands on its exact colour (`exact` = 0 on every world shot)
and half of the zeppelin's hull reads `184,0,196` where a pure scalar dim would give `204,0,204` —
a chromaticity shift of 0.124, larger than any palette this size can separate. Classification is
therefore chromaticity (linear colour over its brightest channel, invariant to a scalar dim) with
tolerance **0.045** and an **absolute** separation of **0.03**, both taken from a sweep against the
override's 113,947 px ground truth plus 60 textures that cannot be on screen: 75 % recall at a
worst-case 575 px false credit. An absolute gap beat a ratio margin outright — a pixel sitting
exactly on a flat has a winning distance of ~0, which passes any ratio test however close the rival.
The first palette drew its two free channels uniformly in **sRGB bytes**, which bunches them in the
corners once gamma is undone; redrawing them uniformly in **linear** ratio took the same texture
from 55,950 to 88,301 px. Fog was measured rather than tolerated: 374,491 px classified confidently
with `--no-fog` against 129,210 with fog on, so census shots take `--no-fog`. Standing rules 91–92.

**Two things the item cannot do, stated rather than implied.** A per-texture census cannot be read
back reliably from one shot at this texture count — counts are lower bounds and `px + contested` the
upper one, so a count under ~1,000 px means "not shown" and an exact figure means `--tex-override`.
And 8 bits a channel leave ~200k reachable colours, so 4 of C1's 882 textures hash to the same one;
each collision is warned by name and counted in the map, never silently resolved by nudging a
colour, which would make it depend on load order.

## 2026-07-25 — PLAN-testing C25: test stages `--stage=empty` and `--node=<cs_name>`

Two stages that replace the chapter world when the chapter world is not the thing under test.

**`--stage=empty`** — a third branch in `StartSession` beside the world build and the parked plane:
`src/Mech3/EmptyStage.cs` builds a flat 20 km ground plane under a **grid drawn pixel-by-pixel in
code** (256² texture, 100 m squares, tiled 200×; nothing committed, nothing extracted) plus one
sunk `BoxShape3D` whose top face is y=0, and `_worldMode` goes false so `gamezPath` resolves to
planes.zbd instead of a chapter. No mission setup, no clutter, no animation program, no weather, no
skydome, no edge extender. The plane starts over the grid origin at 300 m; `--pos`/`--direction`
still win; `--freecam --stage=empty` gives the plane-less grid.

*Measured.* Warm, three runs each: `[perf] startup mode=fly stage=empty total=1951.0/1939.4/1992.5
boot=1019/1075/1122 gamez≈490 world≈20 plane≈150 first_frame≈48` against `--chapter=C1` flight's
`total=4535.6/4474.2/4547.0` — the build alone (`total − boot − first_frame`) is 877/821/824 ms vs
3355. A scripted dive with `--fire` logs guns hitting the plane: `impact: wep_40 (40slug) -> Default
at (7,0,-780) on ground/col` (8 of 8 on `ground/col`), then `CRASH into ground/col (fuselage)
impact=(1,0,-310) spd=138 m/s`. Rockets work too — `--fire-rockets` logs `wep_06 (BOOM) -> Default
at (-4,0,-557) on ground/col`, and one that reached RANGE first detonates in mid-air (`on /`) as it
does anywhere. Two `--det` runs of the same scripted flight are **0 of 921,600 px** apart.

*Two limits, both because there is no chapter gamez:* rockets fly without their FLYOUT body model
and pylons carry no mounted ordnance (the prototypes live in the chapter world; `ProjectilePool`
already null-returns them in any world-less mode), and there is no world-effects runtime, so an
impact draws the spark fallback rather than a named puffer effect. Documented in `docs/cli.md`.

**`--node=<cs_name>`** — `WorldBuilder.BuildNode` slices one named subtree instead of walking the
world, placed at its **world** transform (`GameZ.WorldTransformOf`, not its own `Local`). Matching
is on the source name with the `.flt` suffix optional (rule 60); duplicates are normal, so the whole
match list is logged (`hk_zep#3145`), the first is built, and ambiguity warns. A miss lists the names
containing the request and quits cleanly — `--node=zeppelin` → `rock_zeppelin, tilt_zeppelin,
move_zeppelin`, exit 0; `--node=qqzzxx` says nothing contains it either. Three steps are switched
off: mission setup (it would switch the subject off — C1/IA1 hides `hk_zep`), clutter, and the
origin-parked registration (`HideUnplacedEntities` would hide exactly the transformless vehicle a
node run asks for). `--viewer --chapter=C1 --node=hk_zep` shows the zeppelin alone and framed;
`--anim-lab --node=ap_radiotwr --play-anim=radiotwr_destruction` plays its destruction on the tower.

**The anim-bind audit — the open half the plan flagged as direction-sound — found no throw and two
silent degradations that look identical from outside.** `AnimRuntime.Bind` never throws on a mostly
absent world: a def whose NAME resolves nothing gets `Anchors() == []` and is `continue`d, so *no
handler ever fires*; a def that IS anchored but names a node the build skipped bumps `_opsUnresolved`
and dispatches into nothing, so *the node is not here*. Both leave a still object, which is rule 47
exactly. The bind now runs an opt-in per-definition census (`ReportResolution`, set only for a node
stage) that separates them and names the cause: measured on C1 `--node=hk_zep`, `bind census
defs=813 anchored_by_name=50 anchored_by_root_lift=0 root_lift_suppressed=0 unanchored=763
target_missing_ops=134`, with every one of the 134 `why=index-not-built` (the compiled symbol table's
gamez index was never built) rather than `name-no-match`.

**The finding that changed code:** `MaxRootLift`'s premise is a *whole-world* node count, and a
partial world inverts it. The 16-match cap exists so a generic `ANIMATION_ROOT_NAME` (`healthy`,
217× in C1) cannot anchor a def onto every building — but a single subtree drops *under* the cap.
C1's 20-node `ap_radiotwr` first bound **95 lifted defs and 91 phantom destructible instances**
(`pass_plane01`, `air_gen`, `destroy_aagun32`, twelve crates …). `SuppressRootLift`, set only for a
node stage, refuses the lift and prints the refusals: the same stage now binds 1 def and 2
destructible instances, and the lab's picker lists what actually belongs to the subject. Standing
rules: `docs/verification.md` 91.

**A second trap, caught by the picture not the log:** the first framed `--node=` capture put the
zeppelin at 50 px near the horizon. `OrbitCamera.MergedAabb` merges over the LIVE tree, and
`MeshLab` parks three **empty** overlay meshes at the session origin — invisible for a parked plane
or a whole world (both already contain the origin), but it stretched the subtree's box from 419 m to
5.3 km and framed the camera 12 km out. `FrameCamera` now takes the box measured at build time from
`WorldBuilder.DetachedWorldAabb` (built meshes + node transforms, valid before the subtree joins the
tree — rule 28's own point). `docs/verification.md` 92. The anim lab's own auto-frame is also turned
off on a node stage: re-aiming on every Play swung the camera off the only object present (measured
— the tower left the frame on its own destruction; reproduced on the unchanged full-world path, so
it is the lab's pre-existing behaviour, not a regression).

**Inertness.** Both flags absent, five modes captured on the pre-C25 binary and on this one at the
same `--det --frames=60`: `--freecam --chapter=C1`, `--viewer --plane=player_bhawk`, `--viewer
--chapter=C1`, `--chapter=C1` (flight) and `--anim-lab --chapter=C1` are **md5-identical decoded
pixels, 0 of 921,600 px** each. The compare is seen able to fail (rule 15): the node stage against
the same pose differs by 18.30 %. Rule 11's proof that the baseline binary really was the old one:
it ignored `--node=hk_zep` and produced a shot pixel-identical to plain `--viewer --chapter=C1`.
8-chapter `--freecam` regression **sound-enabled** (no `--mute`), 150 frames each: **0 engine
`ERROR:` lines on every chapter**. `.\RunTests.ps1`: `build PASS · units PASS 149/149 · engine PASS
7/7, engine errors clean (1 allowlisted) · goldens TODO`, `result: PASS -- 21.8s total, exit 0`.

*Flag guards, each exercised:* `--stage=empty --viewer` reports "has no gamez to inspect" and falls
back to the parked-plane viewer; `--node= --fly` reports "is a single-subtree inspection stage" and
takes the viewer; `--stage=lagoon` is reported, not guessed. `--stage=empty --players=2` builds the
splitscreen rig and fans the pair abreast (`spawn [P1 override] pos=(0,300,0)` / `[P2 override]
pos=(60,300,0)`).

**Not verified.** No interactive pass — the node stage's orbit drag, the lab transport on a one-node
world and the empty stage's feel at the controls are all unflown. The suppressed root-lift is right
for an inspection stage by construction; whether some future consumer wants the lifted anchors back
is a question this leaves open.

## 2026-07-25 — PLAN-testing C26: flight camera views, held numpad + scripted `--view=`

Holding a numpad key in `--fly`/`--stunt` snaps the camera to a fixed perspective around the plane
and releasing returns to the chase view; `--view=<1-9>` pins the same perspective for a whole run.
The layout follows the numpad's own geometry: 2 belly, 1/3 45° up from the belly to each side, 4/6
level flanks, 7/9 45° above those flanks, 8 ahead looking back, 5 unbound. One table in
`FlightController` holds each view's offset direction and image up **in the plane's frame**; the
camera sits at the chase camera's own offset length (16.62 m) along that direction and takes its
whole basis from `Attitude * Basis.LookingAt(-dir, up)` — no world-up LookAt anywhere, so a view of
a banked plane shows a level aircraft against a tilted horizon exactly as the chase camera's basis
slerp does. The snap is instant: a scripted capture must not depend on how many frames of catch-up
it waited for. `--view=` is flight-only and warns otherwise; 5 and out-of-range warn and fall back.

**The capture gap it closes.** Flight captures were chase-cam-only, so nothing under the wings or on
a flank could be photographed in the air. Composed with C24 over C1 (`--no-fog
--tex-override=blo_fusalagebottom`, the Bloodhawk's fuselage-bottom skin, on the same flying pose):
**19,509 magenta px from `--view=2`, 1,287 from `--view=8`, 127 from the chase camera** — a 154×
ratio that a chase-cam shot could not have passed (rule 15). The texture name itself came out of a
`--view=2 --tex-census --no-fog` run, which is the census doing its documented job as the map.

**Geometry, all eight views in one sweep** (`--stage=empty --hold=0.5,0.7,0.2,0.7 --frames=90`, so
the plane is pitched, rolled and yawed away from the world frame). Logged plane-frame camera offset,
read back off the camera's own transform, and the camera's forward axis in the same frame:

| view | offset | dist | aim |
|---|---|---|---|
| 1 | (−11.753, −11.753, 0) | 16.621 | (0.707, 0.707, 0) |
| 2 | (0, −16.621, 0) | 16.621 | (0, 1.000, 0) |
| 3 | (11.753, −11.753, 0) | 16.621 | (−0.707, 0.707, 0) |
| 4 | (−16.621, 0, 0) | 16.621 | (1.000, 0, 0) |
| 6 | (16.621, 0, 0) | 16.621 | (−1.000, 0, 0) |
| 7 | (−11.753, 11.753, 0) | 16.621 | (0.707, −0.707, 0) |
| 8 | (0, 0, −16.621) | 16.621 | (0, 0, 1.000) |
| 9 | (11.753, 11.753, 0) | 16.621 | (−0.707, −0.707, 0) |

Every distance is exactly `√(16² + 4.5²)`, the chase offset's length, and every `aim` is exactly
`−dir`, i.e. the camera looks at the plane. 90 lines per run, not one, so the pose is *held*. The
line only exists while a view is active, so an ordinary chase flight logs nothing (measured: 0 lines
without `--view=`, 0 with the rejected `--view=5`).

**Inertness.** Flags absent, three poses captured on the pre-C26 binary and on this one at the same
`--det --frames=`: `--chapter=C1` flight, the same with `--hold=0.5,0.7,0.2,0.7 --frames=120` (which
exercises the chase smoothing), and `--stage=empty --plane=player_fury` — **md5-identical decoded
pixels, 0 of 921,600 px** each. The compare is seen able to fail: the old binary ignores `--view=2`
and returns the chase image, which differs from the new binary's `--view=2` by **97.39 %** of pixels
at the same args. 8-chapter `--freecam` regression **sound-enabled** (no `--mute`), C26 flags absent:
**0 engine `ERROR:` lines on every chapter**. `.\RunTests.ps1`: `build PASS · units PASS 152/152 ·
engine PASS 8/8, engine errors clean · goldens TODO`, exit 0.

**Key-collision audit.** No numpad digit was bound anywhere: the whole tree's key literals are
WASD/QE/arrows/Shift/Ctrl/Space/F/G/H/R/P/T/Tab/Esc/F11/F12/`.` plus the labs' L/M/N/B/C/V/K/W (all
`--viewer`/`--anim-lab`, none reachable in flight), and one `Key.KpEnter` in `MenuInput` — numpad
Enter, a different key. `project.godot` declares no `[input]` map at all, so nothing is bound through
actions either. The `Kp*` keycodes are used, not the top-row digits, which stay free.

**Not verified, and not verifiable here.** The held-key half is correct **by construction** — the
pinned and held paths share `ActiveView()`/`ApplyFixedView()` and differ only in the predicate — but
live keypresses are not scriptable in this project. It also means `Input.IsKeyPressed(Key.Kp*)`
needs **NumLock on** on Windows, which is documented rather than worked around. And the **fidelity is
the user's to judge**: the distance, the 45° elevations and the instant snap are recalled from the
original, not measured out of it. `OriginalScreenshots/` was checked and holds **no usable
reference**: its one candidate, `Fury from above.png`, is a 460×374 *crop* of a plane seen from
above-behind with no HUD and no horizon, so neither a distance nor an elevation can be read out of
it, and it does not even establish which view (or the chase camera) it came from. The four videos
are crash, dive-sound and tile-loading captures. Nothing was inferred from any of them. The
magnitudes are filed in `backlog.md`'s TUNE list and `playtest.md` §3 pending the A/B.

## 2026-07-25 — PLAN-testing C23: the golden-image tripwire

**Landed.** `analysis/goldens/manifest.json` (11 pinned `--det` shots: command line, sim frame,
raw-pixel md5, and what each one actually exercises) with its `README.md`; a `goldens` stage in
`RunTests.ps1` with `-RegenGoldens` / `-SkipGoldens`; and `CSVM/src/Testing/GoldenShot.cs` behind
one new line at the `--screenshot` save site — `[core] shot pixmd5=… size=… gpu=…`. The hash is md5
over `Image.GetData()`, never the saved PNG (rule 36). The manifest holds hashes and commands only:
no pixels, so it is asset-rule clean.

**Goldens run as a scripted pass, not a B12 suite.** C24 flagged the constraint and it holds: the
`--run-tests` harness completes every suite inside one `_Ready` call and never yields a frame, so
nothing there can photograph anything. Eleven separate Godot launches from the script instead, each
`<manifest args> --frames=N --screenshot=<abs> --log-file=<abs>` — which makes every manifest entry
the literal command a human re-runs to reproduce one shot. Frame number and render size are checked
**separately** from the hash, so a clock or window-size regression reads as itself instead of as
"pixels moved". A mismatch names the shot and leaves the actual PNG plus that shot's own engine log
in `.scratch/goldens/`.

**The set.** The 8 chapters on pinned `--pos`/`--direction` (`c1-waterfall`, `c1b-night-sea`,
`c1c-rain`, `c2-city`, `c2b-rain`, `c3-island`, `c4-snow`, `c5-city-night`), one `--viewer` parked
plane (`viewer-bhawk`), one `--stage=empty`, and — new since A3 put the chase camera on the sim
clock — one **flight** pose, `c1-flight`.

**Rule 80 applied per shot, measured.** Frame N against N+1 on the same build, because a pose with
no animated surface would pass even with the clock broken: `c1-flight` **34.52 %**, `empty-stage`
**12.21 %**, `c4-snow` **3.74 %**, `c2b-rain` **3.47 %**, `c1c-rain` **2.50 %**, `c1-waterfall`
**1.28 %**. The other five are geometry-and-shading shots and the manifest says so: `c1b-night-sea`
0 px at N+1 but 1,156 px over 4 s (cloud-puff drift), `c2-city` 51 px, `c5-city-night` 11 px,
`c3-island` 9 px over 4 s, `viewer-bhawk` 0 px at 30 vs 360.

**Verified.** *Rule 14, twice, with the blast radius predicted before the run.* Perturbing the snow
flutter constant (`sway_freq * 0.9` → `* 1.4`, snow-only by construction) moved **exactly
`c4-snow`** and held the other ten; widening the precipitation near-fade (`smoothstep(0.0,
near_fade, md)` → `near_fade * 2.0`, rain *and* snow) moved **exactly `c1c-rain`, `c2b-rain`,
`c4-snow`** and held the other eight. Both reverted to green. *Stability:* two clean end-to-end
`.\RunTests.ps1` runs, **11 of 11 hashes identical both times**, exit 0 — `build PASS · units PASS
152/152 · engine PASS 8/8, engine errors clean · goldens PASS 11 shot(s) hash-identical`, 74.0 s
total, the golden stage 53.4–54.2 s of it. No shot was unstable. *Regeneration:* `-RegenGoldens` on
an unperturbed tree rewrites the file **byte-identically** (4,879 → 4,879 bytes, `Compare-Object`
empty), so a real regeneration's diff is exactly the hash lines — with the snow perturbation active
the diff is **one line**. It reports `REGEN`, never `PASS`, plus a `not checked:` line.
*Cross-check:* the eleven committed hashes were measured by a standalone probe script and
reproduced by the stage's independent code path on the first run.

**The encoding bug the first regeneration found, now rule 97.** `Get-Content -Raw` decodes a
BOM-less UTF-8 file as the system ANSI codepage in PowerShell 5.1, so the round-trip turned every
em-dash in the manifest's prose into `â€”` (4,879 → 4,894 bytes). Reading through
`[System.IO.File]::ReadAllText` fixes it; rule 68's other half.

**Stray-Godot scoping (rule 66) is now per stage.** `Stop-StrayGodots` takes the marker to match:
`--run-tests` for the engine stage, the `.scratch\goldens\` output path for the goldens. Both are
arguments only this script's own launches carry, so a live playtest or a hand-run capture to any
other path is reported and left alone.

**Known non-coverage, stated rather than papered over.** No pose moves more than 9 px across a full
`TextureCycler` cycle — the water flipbooks differ by ~2/255 (rule 32) — so goldens cannot be their
instrument and `--debug-anim` stays it. C1B's four UV-scroll models (the wakes) were not located and
are unrepresented; C1's and C4's scroll covers that surface instead. Sound is muted in every shot,
and splitscreen, the launchscreen and the labs have no shot at all. Every hash is a property of this
machine's GPU (`NVIDIA GeForce RTX 5080 / 1.4.341`, recorded in the manifest): a driver change
legitimately moves all eleven, and the stage prints `GPU CHANGED` when the running adapter differs —
the one case where regenerating is the right answer. Standing rules: `docs/verification.md` 95–97.

## 2026-07-25 — `--det` stops reading the git-ignored dev tuning file

**What this was.** The golden tripwire fired on its first real merge: `c1-flight` moved while the
other ten shots held. The cause was not the change under test. `CSVM/config.json` is git-ignored
(`.gitignore:49`) and carries 15 tuning overrides, **all of them `flightModel`** — so a capture that
honoured it was a function of one machine's uncommitted state. The shot reproduced as
`ec35b99d…` in the tree that captured it and `f0493fb0…` in any worktree, and only the flight shot
could notice, which is exactly the pattern observed.

**Diagnosis.** Reverting the merged change's own source in the worktree still gave `f0493fb0…`,
which ruled it out; running the identical command in both trees with the same data root and GPU gave
the two different hashes; `config loaded overrides=15` in one log and no such line in the other named
the cause.

**Fix.** `Config.ClearOverrides()`, called when the `--det` bundle resolves, so a deterministic run
takes in-code defaults. The bundle line now carries `config=defaults dropped_overrides=N`. Capture
with local tuning applied by passing `--no-det`.

**Verified.** The same command now yields `f0493fb0…` in **both** trees — main dropping 15 overrides,
the worktree dropping 0. The manifest was regenerated with exactly one hash changed (`c1-flight`),
which is the shot the cause predicts; the other ten regenerated byte-identically. Landed as
verification rule 98.

**Worth keeping.** The tripwire earned its place on its first outing — it caught a reproducibility
hole in itself that no other instrument here would have surfaced.

## 2026-07-25 — Shared click-selection with a `cs_name` ancestor ladder (PLAN-testing D31)

**What landed.** `CSVM/src/UI/SelectionService.cs`: in `--freecam` and `--anim-lab`, left-click
picks the mesh under the cursor and PgUp/PgDn (Home/End) walk its `cs_name` ancestry from that leaf
up to the placed world object. A breadcrumb HUD line names every rung with the current one bracketed
and its world-frame box; an `ImmediateMesh` wireframe outlines the current rung's subtree.
`Current`/`Ladder`/`Level`/`CurrentBox` + a `Changed` event are the session state D32–D35 bind to.
`--debug-select=x,y[,up]` is the scripted twin. `AnimLab` no longer picks — its `PickObject`/
`RayAabb` moved into the service and its camera-follow now tracks the selection's current rung.

**The picking audit, which the rest of Wave D inherits.** The lab's click-to-follow was never a
physics raycast — it could not be, since `WorldSession.Options.Collision` is flight-only (rule 72)
and these modes build no bodies at all. It is a manual **ray-vs-AABB scan over the visible
`MeshInstance3D`s** under the world root: `ProjectRayOrigin`/`ProjectRayNormal` for the ray, each
mesh's own AABB tested in its local frame (the affine inverse keeps the ray parameter equal to the
world distance, so it compares across nodes), nearest hit wins, one walk per click. Two properties
carried forward verbatim: it is **AABB-accurate, not triangle-accurate**, and a 350 m
world-AABB-diagonal cap skips map-scale meshes — which is what stops every click landing on terrain,
and equally means **terrain is unselectable**. A click that finds nothing now says so
(`select miss … tested= skipped_oversize=`) instead of going quiet.

**Verified.** The zeppelin case, `--freecam --chapter=C1 --mission=M04 --pos=-4848,200,-5165
--direction=-1,0,0 --debug-select=852,360`, clicking an engine nacelle: the ladder is
`g15 < l5 < healthy < lk_rightengine01 < lkgasbag01 < zfronthalf < rock_zeppelin < noserotate <
hk_zep` — motor to main node, **nine** rungs, which is why Home/End exist. The outermost rung's box
reads `centre=(-5248.0,199.8,-5164.6) size=(94.3,72.7,418.6)`, matching C25's independently measured
`DetachedWorldAabb` for the same node exactly, and it wraps the hull in the capture. Rule 60: the
second `box_car.flt` of C1's cargotrain logs `cs_name=box_car.flt godot=@Node3D@5` while the first
logs `godot=box_car_flt`, so the ladder is unreadable from Godot names and correct from `cs_name`.
Inertness: four `--det` poses (C1/M04 zeppelin, C4 default freecam, the C1 cargotrain anim-lab node
stage, C1 default freecam) are raw-pixel md5-identical to the pre-change build, 0 of 921,600 px on
the decoded compare; the same pose **with** `--debug-select` differs 1.32 %, so the compare was seen
able to fail. 8-chapter sound-enabled `--freecam`: 0 errors beyond the known pre-existing C3
`!is_inside_tree()`. `.\RunTests.ps1` PASS — 152 units, 8 suites, 11 goldens hash-identical, exit 0;
no golden moved, which is the expected result for an overlay that draws nothing unasked.

**Not verified here.** The interactive half — the actual click, the PgUp/PgDn feel, whether the
breadcrumb reads at a glance while flying the freecam — is unscriptable in this project
(`docs/verification.md`, "what this project cannot verify itself") and is the user's call;
`playtest.md` carries it. `--debug-select` exercises the same `PickAt`/`StepUp` entry points the
mouse and the keys call, so only the event binding itself is by construction.


## 2026-07-25 — C22: the perf suite, its A/B, and a measured noise floor

**What landed.** `RunTests.ps1 -Perf`: five scenarios from the committed
`analysis/perf/scenarios.json` — `empty-stage`, `c1-flight`, `c2b-water`, `c4-terrain`, `c5-city` —
each run `--det --perf --no-vsync --mute` for **300 sim frames** × 3 launches, medians appended as
one JSON line per scenario to the git-ignored `perf-history.jsonl` at the repo root. `-PerfLabel` /
`-PerfCompare` are the A/B: label a baseline, flip the one line under test, rebuild, run again
paired. The run length is a *frame count*, ended by `--frames=N --screenshot=`, whose saved-shot line
prints `sim_frame=N` — the count is proved, not assumed. The first launch of each scenario and the
first window of each kept launch are discarded (cold cache, first-draw shader compilation).

**Engine side.** `--perf`'s window became 60 rendered frames instead of one wall second (a
wall-second window makes the sample count a function of the frame rate, which a paired comparison
cannot have), its line became flat `key=value` on the `perf` category, and it gained `prims`,
`nodes` and `mem_mb` next to `draws`. New `--no-vsync` (vsync off + `Engine.MaxFps 0`), inert unless
passed: at the refresh cap `script_ms` collapses onto the frame time — `--stage=empty` read 17.00 ms
against C4's 17.20 ms with **11× the draw calls** — and uncapping steadied `gpu_ms` (C4 0.47–2.26 ms
→ 0.36–0.37) and halved the wall time. It changes no simulation: the `empty-stage` golden hash holds
with it on, because the fixed clock steps once per *rendered* frame.

**The noise floor, measured before anything was believed (rule 7) and then re-measured (rule 8).**
Suite run twice unchanged: counts identical to the digit, `render_cpu` within 3.3 %, `gpu` within
1.4 % on a fixed camera and 17.3 % on the moving one, startup phases within 10.5 %. The **next**
unchanged pair was two to three times noisier (startup to ±14.8 %) and flagged five same-build rows
against a band calibrated on the first pair. Hence the shipped marker needs **both** a relative band
and an absolute floor — rule 41 made mechanical, since 0.045 ms of jitter on a 0.26 ms `gpu_ms` is a
17 % ratio and no difference. All three recorded same-build pairs now produce zero marks.

**Perturbation (rule 14/15: the instrument seen able to fire).** One line in `Clutter.cs` — the
tiling period halved — built, run, reverted. Predicted before running: `empty-stage` must not move;
`startup.clutter` must rise on the world scenarios. Held: `startup.clutter` ×1.70 (C4) and ×2.26
(C5), `startup.edge` ×2.07 (C5), `prims` ×2.03 and ×2.92, `gpu_ms` ×2.95 on C5 — and **`empty-stage`
did not move on a single metric**. `prims` tracked the real instance count to a few percent (C4
sprites ×2.26, C5 ×2.90). Two predictions failed, both mechanism: `nodes` did not move on C5
(clutter placements live in MultiMesh buffers, never as nodes, on the solid path too), and
`c1-flight` moved *downwards* — halving the period re-scatters C1's cell offsets rather than
multiplying them, 9,303 sprites → 8,253, which `prims` also tracked (×0.94). After the revert, a
fourth pair against the pre-perturbation baseline was clean.

**Refused, with the reason printed on every A/B.** `fps`/`frame_ms` (paced — floors, rule 38),
`script_ms` (`TIME_PROCESS`, ~2.2× real per rule 37, and pinned when the loop is paced),
`physics_ms`, `mem_mb`. `physics_ms` is the one worth naming: it is empty **by construction** under
`--det` — 0.01–0.04 ms in every scenario — because the fixed clock is parent-driven, so
`_PhysicsProcess` consumers no-op and collision cost lands in `script_ms`. Rule 38's "read `physics`
for collision" does not survive contact with `--det`.

**Verified.** `.\RunTests.ps1 -Perf` green end to end: build, **152 units**, **8 engine suites**
(errors clean), **11 goldens hash-identical** — the goldens are also the proof the engine changes
are inert when their flags are absent — and perf 5/5, exit 0, 161 s. `perf-history.jsonl` grew five
lines per run and is git-ignored (`git check-ignore` confirms).

**Residuals.** Even uncapped this machine paces at exactly 120 fps from outside the engine (driver
or compositor — not diagnosed), so `fps`/`frame_ms` remain floors; nothing in the suite sees a pure
C#-sim regression that stays under that cap. `c2b-water`'s `clutter` phase (3.6 ms) is below the
stage's 15 ms absolute floor, so even a 4× there would go unmarked. Splitscreen, the launchscreen,
the labs and audio are unmeasured. Standing rules: `docs/verification.md` 100–102.

## 2026-07-25 — The node lab: tree, search, dependencies, destructibles (PLAN-testing D32)

**N opens a dockable panel in `--freecam`/`--anim-lab`** — `src/UI/NodeLab.cs`, plus four read-only
accessors on `AnimRuntime` (`AnchorsOf`, `FindNodes`, `HandledEventKinds`, `PartialEventKinds`),
`SelectionService.SubtreeWorldAabb` made public, and the wiring + `--debug-nodelab[=spec]` in
`PlaneViewer`. It absorbs **M3's F41** (destructible list + camera jump + coverage columns), which
the plan's Wave-F overlap table already marked superseded. Nothing else in the tree changed.

**What it holds.** The world's node tree by `cs_name`, populated **one branch at a time** on expand;
a search box over a once-built flat name index; two-way sync with D31's selection (world click →
tree row, tree row → selection, double-click frames); Frame (`SpectatorCamera.Frame`/`FollowNode`)
and Hide/Show (`Visible` flip only); a dependency readout for the current rung; and a destructibles
view with F41's two coverage columns.

**Verified — the water tower, quoted from the run**
(`--freecam --chapter=C1 --det --mute --debug-nodelab=node=ap_h2otwr1,deps`):

```
nodelab select name='ap_h2otwr1' → 'ap_h2otwr1' matches=1 exact=True candidates=[ap_h2otwr1]
nodelab deps anim defs anchored_here=2 naming_this_node=0
nodelab deps anim def=h2twr_destruction1@ap_h2otwr1 rel=anchor activation=WeaponHit health=60
   source=compiled seqs=[DAMAGE_SEQUENCE destroy_h2twr (unnamed) ×4 h2twr_puffer]
nodelab deps destructible pool def=h2twr_destruction1@ap_h2otwr1 hp=60/60 state=Healthy stage=0
   authoritative=True source=compiled
nodelab deps destructible DAMAGE_SEQUENCE def=h2twr_destruction1@ap_h2otwr1 events=6 thresholds=2
nodelab deps geometry meshes=5 surfaces=12 materials=6 textures=h2otwr02.tif, h2otwr01.tif, …
nodelab deps colliders NOT BUILT IN THIS MODE — --freecam/--anim-lab build the world with no
   collision at all, so an empty list here would be the missing instrument, not missing colliders
```

Both pools show — the compiled `ap_h2otwr1` def (authoritative) and the reader's `ap_h2otwr*`
wildcard — which is the registry's per-`(def,anchor)` keying made visible.

**Verified — totals equal the `destructible-census` suite.** Full C1 reports
`defs=132 instances=267 node_groups=196 unresolved_defs=12`, C5 `instances=568 node_groups=292` —
the suite's committed numbers, reached through a different code path (the panel joins the program's
`HEALTH>0` defs to the registry; the suite reads `Count`/`DistinctAnchors`).

**Verified — the C25 phantom case does not appear as real.** On `--anim-lab --node=ap_radiotwr`
the view reports **`instances=2 node_groups=2`**, not the 91 phantom instances the root-lift
heuristic would have produced, and prints a red `PARTIAL WORLD` banner carrying the bind census
(`root_lift_suppressed=95`) above the list. 131 of C1's 132 destructible defs then read `UNRESOLVED`
— correct for a 20-node slice, and stated rather than hidden.

**Verified — the collider notice is a notice, not an empty list, and the branch behind it works.**
With `collisionBuilt` and `WorldSession.Options.Collision` temporarily forced on in freecam (flipped,
built, measured, reverted — rule 10), the same node reads `colliders bodies=4 shapes_enabled=1
shapes_disabled=3`, the rule-73 direction split. `collisionBuilt` is wired to the real option and is
false in every mode this lab runs in today; D35's `--collision` is what flips it.

**Verified — inertness, and the compare seen able to fail.** `.\RunTests.ps1` PASS: 152 units,
8 engine suites, **11 goldens hash-identical**, exit 0, 72.7 s. The same C1 waterfall pose with the
panel open hashes `84af3759…` against the golden's `0bb2532d…`, so a golden could have caught it.
8-chapter sound-enabled `--freecam` (`--quit-after 240`, flags absent, full-log grep): **0 errors in
seven chapters**, the known pre-existing C3 `!is_inside_tree()` ×1 in the eighth.

**Verified — perf, panel open in the C5 city** (`RunTests.ps1 -Perf -PerfFilter c5-city`, paired
A/B against the same build with the flag absent, read against C22's committed bands):
`render_cpu_ms` 1.035 → 1.045 (×1.010), `gpu_ms` ×0.988, `prims` ×1.000, `nodes` +31, `draws`
1192.1 → 1214.1 (×1.018 — **the panel's own 22 UI draw calls**, the only marked verdict metric),
`startup.rest` +33 ms for the panel build. `frame_ms`/`fps` are pinned at this machine's 120 fps
pace and are floors (rule 38), so a sub-5 ms CPU cost hiding under the cap is not ruled out.
A first A/B with the *dump* also running read `script_ms` ×2.877; isolating the panel with the new
`--debug-nodelab=open` token took it to ×1.162, which is what identified the spike as the one-off
dump rather than a per-frame cost.

**Deviations from the item text.** (1) The camera is `SpectatorCamera.Frame`/`FollowNode`, not
`OrbitCamera`'s — the orbit camera is `--viewer`-only and this lab is freecam/anim-lab-only; the
API is the same shape. (2) A branch is capped at 500 rows: C5's world root has **557** named direct
children, and an uncapped root branch would defeat the laziness the tree exists for. The overflow is
a row that names the count and points at the search box. (3) `--debug-nodelab` grew a
`node=<cs_name>` token, because a scripted run has to reach a *known* node and a screen-position
pick cannot name one; it goes through the tree's own `Select`, which is also how anything over the
350 m pick cap is reached (demonstrated on C5's `z3terrain`, a 512×0×512 box).

**Residuals.** The interactive half is unverified here — live mouse and key input are unscriptable
in this project, so N, the expand arrows, the search field, the buttons and the two-way click sync
are exercised only through `--debug-nodelab` and by construction; the panel's feel and the tree's
usability are the user's call (`playtest.md`, beside D31's zeppelin case). The panel's layout was
checked at 1280×720 only, where the anim lab's variant is cramped between the breadcrumb and the
timeline. The name index is a snapshot taken on first search; freed nodes are skipped at query time
rather than triggering a rebuild.
## 2026-07-25 â€” Mesh lab on the selection (M) + collider wireframes (C), `--collision` (PLAN-testing D33 + D35)

**What landed.** `MeshLab` is no longer viewer-only: given a `SelectionService` it becomes the
*scoped* lab, and **M** in `--freecam`/`--anim-lab` attaches every overlay and override to the
selected rung's subtree, restoring it exactly on M again, on a selection change, or on a
deselection. New `src/UI/ColliderOverlay.cs` draws every built collider as a colour-coded
wireframe on **C** (world / water / buildings / clutter / plane / other), and `--collision[=show]`
forces the collision build in the modes that build none. `SelectionService.OverlayMeta` is the new
"this is a drawing, not content" marker both tools use, skipped by the pick and by the box
measurement. `SpectatorCamera`'s undocumented C-descends alternate moved to Z (the camera polls raw
key state, so sharing C descended on every overlay toggle).

**The rule-56 answer: the override materials are now the surface's own shader, edited.** The old
hand-written replica could only stand in for the *aircraft's* shaded variant; a world surface's
shader carries `unshaded`, the sRGB vertex modulate, LIGHT_STATE spill, cylindrical fog, UV scroll
and its alpha term, and rendering it through the replica would have re-lit the very thing under
inspection. `DerivedShader` rewrites two things in the original text â€” the cull token in
`render_mode`, and a `csky_lab_normal_mode` block injected at the top of `fragment()` â€” and copies
every uniform by name. Measured with the new `--debug-mesh=force` (build the overrides at the
data's own settings, the able-to-fail control): **0 px** change against the shipped render on both
the C1 water tower and the parked Bloodhawk (the viewer's 960-px delta is the panel's own status
line), where the replica path moved **1,682 px** of the tower's ~2,500 px. `verification.md` 104.

**Verified.** C1 freecam, `--pos=-6140,185,-4340 --direction=0.71,-0.17,0.68`, pick at (640,360)
walked 4 rungs to `ap_h2otwr1` (12 surfaces, 264 tris, 12 fullbright): overlays on moved **1,278 of
921,600 px, every one inside the tower's own 31Ã—81 px rect** â€” the identical second tower 250 px
away untouched; `--debug-mesh=â€¦,restore` (attach then detach) returned the exact pre-toggle md5
`33e2beâ€¦`; `cull=inverted` moved **626 px, max delta 89**, all inside the same rect (able to fail),
and 3.88 % of the frame on the parked plane. C2 with `--collision=show`: 1,848 node-backed shapes
(the same count rule 72 measured) + 10kâ€“14k clutter placements, `switched on 521 Â· switched off
1349`; with `--destroy=gate1`, `on 528 Â· off 1342` â€” the two directions reported separately, never
the +7 net, and `--damage-test=gate1 --damage-hd=25` independently reads `col[off 1, on 8]`. The
gate's lintel wireframe is one healthy box before and three wreck-piece boxes after. A live
in-run flip logged itself on the propane chain: `switched OFF 4: tbridg2a, tbridg2b, tbridg1a,
tbridg1b`. Without `--collision`, `--debug-colliders` prints the notice once and the only pixels
that move are its 306Ã—51 px text. `--collision` startup, warm, 3 runs each (C2 freecam): total
2,462 â†’ 3,106 ms, `world` 352 â†’ 865, `clutter` 47 â†’ 56 â€” the clutter BVH is no longer the dominant
term rule 39 measured (C5: 3,188 â†’ 4,304, `world` 448 â†’ 1,240). 8-chapter `--freecam` regression
with sound on: zero errors bar C3's known `!is_inside_tree()`. Overlay cost, C4 at the golden pose
(`--perf --no-vsync`): draws 2,181 → 2,532, prims 217k → 257k, `render_cpu` 1.05 → 1.42 ms. `.\RunTests.ps1` green â€” 152 units,
8 suites, **11 goldens hash-identical**, which is the inertness proof for both flags absent.

**The bug the first cut had, and its rule.** Scoped normal lines drew 163 m spikes across the whole
chapter: `BoundingRadius` was `max |v|`, and a world subtree's vertices are absolute under an
identity node transform, so the tower "measured" 7,420 m â€” its distance from the map corner. Now
the geometry's own box half-diagonal (`verification.md` 103). Also: one `ImmediateMesh` surface per
*body*, not per shape â€” a C2 clutter region carries thousands of placements and the cap is 256.

**Residuals.** Every keypress half is by construction (live input is unscriptable here): M, C, the
light steering, and re-targeting the lab by clicking something else while it is attached â€” all in
`playtest.md`. The overlay's counts are pose-dependent (the map-edge extender adds clutter bodies),
big trimeshes draw as bounding boxes over 2,000 tris, and `--viewer` binds no C overlay because C
is the mesh lab's cull cycler there (it says so when `--collision` is passed).


## 2026-07-25 — The world damage lab: HP slider, kill and reset on any destructible (PLAN-testing D34)

**H opens a panel in `--freecam`/`--anim-lab` that damages whatever the shared selection is on** —
`src/UI/WorldDamageLab.cs`, plus `DestructibleRegistry.PoolsOn`, three census helpers lifted out of
`Probes.Damage` into `Probes` (`EnabledColliders`, `WorldRootOf`, `CountVariants`), a shared
`PlaneViewer.EnsureWorldEffects` that `--destroy` now goes through too, and
`--debug-damage[=script]`. In `--viewer` H still means the parked aircraft's `DamageLab`; the two
never exist in the same session. **This supersedes M3's F40**, which the Wave-F overlap table already
marked; `backlog.md` holds no F-reference to delete.

**What it holds.** Every destructible pool on the selected node (or the enclosing one a hit would
reach), each with live HP, state and damage stage; on the reachable one a slider, Kill and Reset
driving `AnimRuntime.DamageAt` / `ResetDestructible`. The slider is **absolute HP** — down spends the
difference through the weapon-hit path, up runs `ResetDestructible` and re-damages, because the model
has no healing. A kill reports the swap and the collider census **pre-tick** (synchronous) and the
debris **post-tick** (scheduled).

**Only one pool per object is drivable, and that decided the UI.** C1's `ap_h2otwr1` carries two
pools with independent 60 HP — the compiled `h2twr_destruction1@ap_h2otwr1` and the reader wildcard's
`h2twr_destruction*@ap_h2otwr*` — and `DamageAt` re-resolves through `DestructibleRegistry.Resolve`,
so health spent on the twin drains a pool nothing can ever hit. Every pool is listed; only the
reachable one carries controls, the rest carry the reason, and a scripted `pool=2,kill` is refused
out loud. New verification rule 103.

**Verified — the scripted sequence, quoted from the run**
(`--freecam --chapter=C1 --det --debug-damage=node=ap_h2otwr1,hp=30,kill,tick=3.5,reset,kill`):

```
damagelab pool=1/2 def=h2twr_destruction1@ap_h2otwr1 source=compiled anchor=ap_h2otwr1
   hp=60/60 state=Healthy stage=0 drivable=True
damagelab pool=2/2 def=h2twr_destruction*@ap_h2otwr* source=reader anchor=ap_h2otwr1
   hp=60/60 state=Healthy stage=0 drivable=False
damagelab hp   … spent=30 hp=60→30 stage=0→1 state=Damaged started=[sputter_black_smoke_obj]
damagelab kill … hp=30→0 state=Destroyed started=[sputter_fire_smoke_obj h2twr_destruction1]
damagelab kill swap healthy=0/1 shown destroyed=1/1 shown (immediate, pre-tick)
damagelab kill colliders off=1 on=3 (counted separately — the net hides a real removal)
damagelab kill debris=0 sounds=0 (immediate, pre-tick — the death's debris motion is SCHEDULED)
damagelab tick advance=3.5s debris=+2 sounds=+0 (scheduled state, post-tick)
damagelab reset … hp=60/60 state=Healthy stage=0 swap healthy=1/1 shown destroyed=0/1 shown
damagelab kill … hp=60→0 state=Destroyed started=[sputter_fire_smoke_obj h2twr_destruction1]
damagelab kill swap healthy=0/1 shown destroyed=1/1 shown (immediate, pre-tick)
damagelab kill colliders off=1 on=3 (counted separately — the net hides a real removal)
```

The second kill is line-for-line the first — the C28 idempotency check, now interactive. Colliders
are reported as `off=` and `on=` and **never as the +2 net** (rule 73); the debris is read before and
after the tick (rule 75), and reads 0 then +2 because the `OBJECT_MOTION` is scheduled at t≈2.2 s
while the whole script runs inside one frame.

**Verified — the panel agrees with the headless twin, number for number.**
`--damage-test=ap_h2otwr1 --damage-hd=60` reports `swap[healthy 0/1 shown, destroyed 1/1 shown]`,
`col[off 1, on 3]`, `debris[2 launched]`, `snd[0 played]`, `reset[healthy=✓ (h1/1,d0/1), rekill 1h ✓]`
and `hit 1 → sputter_fire_smoke_obj, hit 1 → h2twr_destruction1`. Two code paths, one set of numbers —
which is what the shared `Probes` census helpers are for. (`snd 0` is the tower's own data: its death
authors no `Sound` event. C1's `m_build01` reads `sounds=2`, `debris=7`, `col[off 1, on 10]`.)

**Verified — rule 72's notice is a notice, not an empty list.** `--debug-damage` forces the collision
build on, the `--damage-test` precedent, one `||` in the same expression. With that force temporarily
removed (flipped, built, measured, reverted — rule 10) the same kill prints
`colliders NOT BUILT IN THIS MODE — this world was built with no collision at all, so a census here
would read zero and lie about what the death removed`.

**Confirmed, not assumed: the freecam build does NOT wire M3's world-effects runtime.**
`BuildWorldEffectsRuntime` runs in `--fly`, `--effects-test` and under `--destroy`; a plain
`--freecam` builds none, and the world runtime's puffer factory is torn down after the bootstrap
(rule 76). The lab now asks for it on its **first damage action** (nothing built until then), and a
`m_build01` kill renders its `great_balls_of_fire` in freecam — captured. **It is not a blanket
fix:** that runtime binds a fixed 28-name closure, and the tower's progressive stages
(`sputter_black_smoke_obj`, confirmed in `extracted/C1/cam_anim` as a `PUFFER_STATE` def) and its own
`h2twr_puffer` sequence are not in it, so they fire, log, and draw nothing outside flight. Rule 76
now carries that.

**Verified — inertness.** `.\RunTests.ps1` PASS: 152 units, 8 engine suites, **11 goldens
hash-identical**, exit 0, 78.8 s. Eight of those eleven are `--det --freecam` poses whose hashes were
committed before this change, which is the byte-identity claim; the compare is seen able to fail —
`c1-waterfall` with `--debug-damage=open` hashes `e790256d…` against the golden's `0bb2532d…`.
8-chapter sound-enabled `--freecam` (`--quit-after 240`, flags absent, full engine-log grep):
**0 errors in seven chapters**, the known pre-existing C3 `!is_inside_tree()` ×1 in the eighth.

**Residual: the interactive half is unverified.** H, the slider drag and the Kill/Reset buttons run
through `--debug-damage` and by construction only — live mouse and key input are unscriptable here —
and the layout was checked at 1280×720 alone. `playtest.md` §9, beside D31's and D32's checks.

## 2026-07-25 — PLAN-testing complete and archived

All 18 items landed (Waves A–D) and the plan moved to `docs/plans/` with a `COMPLETE` banner and a
`plans.md` row. `--det` runs are byte-identical frame-for-frame in every mode including flight;
`.\RunTests.ps1` is the single entry point (build → 152 units → 8 in-engine suites → 11 golden
hashes → one exit code); the inspect layer — click-selection with a `cs_name` ancestor ladder, the
node lab, the mesh lab scoped to a selection, collider wireframes and world damage sliders — is in
freecam and the anim lab. M3's F40 and F41 are absorbed.

**What the plan changed about how this project verifies itself.** Twenty-five new
`docs/verification.md` rules (78–105), most of them written because an instrument was caught
lying mid-item rather than after the fact. The tripwire earned its keep on its first real merge by
failing on a golden that moved for a reason nothing else could have surfaced: `--det` was reading a
git-ignored dev tuning file, so every capture was a function of one machine's uncommitted state.

**What is owed and cannot be done here.** Every interactive half — the clicks, the ladder keys, the
panels' feel, the numpad camera magnitudes, the flight-model tuning — is unverifiable in this
project and sits in `playtest.md` §9, with D31's zeppelin case as the acceptance test the plan
itself named as the user's call.

## 2026-07-25 — Scripted runs stop stealing the desktop

**The complaint.** A test run repeatedly took the foreground while the user was working. A full
`RunTests.ps1` launches the engine ~19 times, so the milestone's own tooling had made this much
worse than it used to be.

**What it was not.** Godot exposes no CLI flag for this, and project-setting overrides on the
command line are silently ignored — measured by overriding `viewport_width` and getting the same
1280x720 capture and the same pixel hash. The existing `--no-focus` set `WindowFlags.NoFocus` from
`_Ready`, which is after the window exists and has already activated; clearing or setting the flag
then hands nothing back.

**Diagnosis, and a wrong turn worth recording.** A first probe attributed windows by title and
reported that the non-console Godot build never steals focus (0 of 52 samples). That was an
instrument failure: the baseline window it compared against was another `CSVM (DEBUG)` session, so
the thief was indistinguishable from the starting state. Re-attributing by **process tree** — the
console build spawns three processes — gave the real figures: console 5 of 7, non-console 13 of 16.
Landed as rule 106.

**Fix.** `display/window/size/no_focus=true` in `project.godot` creates the window unfocused, and an
interactive session asks for focus explicitly. The engine cannot take it back itself (Windows'
foreground lock no-ops that, measured 0 of 120), so `RunGame.ps1` and `RunDev.ps1` hand the
foreground over from the launching console, which is permitted. Rule 107. `RunTests.ps1` additionally
moved to the non-console binary, which required an `Invoke-Godot` helper that waits: PowerShell does
not block on a GUI-subsystem process, and gives it no stdout.

**A second bug the change exposed.** With every launch returning instantly, the engine stage reported
**PASS** while producing no report at all — a run that never started read as green. A missing report
is now a FAIL, on the same principle as the SKIP rows: absence of a result is not a result.

**Verified.** Full `RunTests.ps1` under a process-tree focus probe: **0 of 158 samples**, with the
suite green throughout (152 units, 8 engine suites, 11 goldens hash-identical, exit 0). `RunGame.ps1`
still takes focus where the unpatched engine-side grab did not.

## 2026-07-25 — Design-document cross-check: five doc corrections + five readers decoded

**Landed.** Read the original pre-release design document against the shipped extraction and
corrected what `docs/` and the code stated wrongly, then documented the mission readers nobody had
decoded. Every claim was re-measured against `extracted/**` first; one did not survive and was
dropped. Instruments are committed as `analysis/gdd-cross-check/` (six probes + a shared zrdr
reader; verdicts in that directory's README).

*Corrections.* `missions.md` said a Danger Zone has no gate geometry — it does: **all 80 `dzpathN`
meshes carry exactly three polygons**, a route ribbon plus a matched outline pair (the design's
entry and exit volumes, both of which must be crossed). `spawns.md` and `LaunchMenu.cs` called
C1/C1B/C1C day/night variants of one Sea Haven map — they are **separate terrain databases** in one
campaign region. `vehicle.md` implied `gun_pitch`/`gun_yaw` were a turret arc — they are the **AI's
forward-gun cone**, and turret rotation limits are in no reader at all. `architecture.md` recorded
the `engine` flag as deferred — it is unwired **by design** (damage never degrades performance),
caveat kept that the shipped data still sets the flag. `loadouts.md`'s uniform-HE stock is now
attributed as a retail-UI observation, with the `{count, stock}` narrowness recorded as a schema
limit (the runtime is already mixed-capable).

*Decoded.* `ia.json`'s full Instant Action configuration (mission type, enemy waves, the named ace
with a `PaintScheme`-compatible livery) → `spawns.md`; per-mission `dzones.json`
(`objective_numbers`/`disable`/`nosnapshot`) → `missions.md`; **new page**
`docs/formats/mission-entities.md` for `zeppelins.json` and `egen.json`; `player.json`'s aim-assist,
warning-shot and smokescreen blocks → `vehicle.md`; and the **74-command bindable inventory** →
`strings.md`, which corrects an earlier conclusion that the spyglass and padlock views were cut —
both shipped, along with the full targeting suite and the objectives display.

**Verified.** Six probes, each printing its own verdict and re-run clean from the worktree.
`gun_cone.py`: 12 cone-owning defs, all `[-11, 11]`, **7 with no turret**, **0 of 12 player defs**
(including all five turret airframes), 60 of 63 AI defs resolving it. `dzpath_gates.py`: polygon-count
histogram `{3: 80}`, 29/80 area-bit-equal pairs, 64/80 within 10 %, median pair separation 11.7 m.
`chapter_distinct.py`: the six Sea Haven airfield nodes present in C1 and absent from C1B and C1C;
C2 vs C2B **zero** identical terrain meshes; disjoint danger-zone names. `damage_pools.py`: 46 weapons
carry both damage fields, 18 differ. `commands.py`: 74 commands under 7 headings.
`reader_census.py`: 58 zeppelin instances over 50 files, 23 generators over 53.

**Failed verification, and dropped.** The brief asserted AI defs ship an unequal `destroyable_parts`
pair (25/20) — false. **All 88 entries across all 22 defs have hp1 == hp2**, no exceptions; the `r*`
AI defs mirror their player counterparts exactly. (A sibling branch measured this independently and
agrees.) The (armor, hit points) reading of that pair is therefore recorded as a **hypothesis with a
falsification test** — rounds-to-kill with `wep_31` (dum-dum) vs `wep_32` (AP) on one zone — not as
decoded fact: every shipped pair being equal means no measurement over this data can discriminate.
`ace_stats`'s 9-value order is likewise inferred, not decoded (all 8 chapters store `[9]x9`).

**Traps recorded** (`docs/verification.md` rules 79–81). An absent asset filename is not evidence a
feature was cut — the spyglass ships with no asset bearing its name. Trust the design document for
system shape, never for numbers. And **prove a per-chapter file or node flag discriminates before
using it**: `map.json` is the same string in 7 of 8 chapters, `location.json` gives two Hollywood maps
Sea Haven camera presets, and the gamez `terrain` flag means different things per chapter — a
terrain-height grid built on it compared clouds to sea and called two different worlds identical.

## 2026-07-25 — Backlog: combat-fidelity gaps from a design cross-check

**Landed (documentation only).** Seven new `backlog.md` entries under Feature backlog, from reading
the original pre-release design spec against the code and re-verifying every claim against
`extracted/` and `CSVM/src` first: the unimplemented armour layer (`ARMOR_DAMAGE` has only display
consumers, and the split *is* the DD/AP/magnesium ammo tier — 18 of 48 entries, with AP currently
inverted into the worst round); no explosive radius (single-raycast `Impact`, `IMPACT_PROXIMITY` /
`DETONATION_DISTANCE` parsed and printed only); the incoming-fire cue set (`bullet_warning_sg` +
the complete `warning_shot_*` accumulator, no caller); Danger Zone scoring as an ordered pair of
`dzpathN` gate crossings rather than one 15 m sphere; nitro booster (data complete, the numbers
executable-resident — low priority); five small per-impact feedback gaps (`damaged_engine_sound`,
the `injure_anims` 0.99 spark burst, silent glancing collisions vs `touchdown.zrd`'s three surface
variants, `gunshell.zrd`, `snd_dangerzone_camera`); and `sticky_bullet_*`.

**Three claims failed verification and were recorded as traps rather than work.** The crash
"fireball leads the explosion by 0.5 s" is contradicted by `player_crash_dirt`, which authors the
ground boom *before* the fireball cascade; `window_hit_sg`/`bullet_hit_sg` are not world-data
orphans — they ride 80 shipped `bullethole_anims` defs (only `snd_warningshot1-3` are); and the
spec's shell-ejection description fails twice against `OriginalScreenshots/C1B IA1 Bloodhawk tracer
and ejection.png` / `…ejection2.png` — the Bloodhawk ejects on a 40-cal/30-cal wing fit, so
ejection is neither gated to the large calibres nor mounted on the underbelly. That bullet was
rewritten from the captures (brass sprite + a persistent, aft-drifting white puff cluster at the
wing mounts), which also corroborate the open muzzle-flash and tracer look items. The section
carries a standing weighting note: the document's structural claims have held, its per-item art and
balance numbers have repeatedly failed — take mechanisms from it, never magnitudes.
Two existing entries were kept in step (pass-2 casing-ejection finding unblocked; the `DzRadius`
TUNE's open mechanism question settled), and `docs/formats/vehicle.md`'s claim that AI variants
ship unequal `destroyable_parts` hp pairs was corrected — re-measured, all 22 defs ship them equal,
which is the evidence the armour-pool hypothesis rests on.
## 2026-07-25 — Milestone 4 (AI) scoping study

**Landed.** `docs/SCOPING-M4-ai.md` — a scoping document, **not** a live plan and deliberately not
named `PLAN-*` (a `PLAN-*.md` in `docs/` reads as active here, and `PLAN-testing.md` is). It carries
the plan shape so scheduling it is a rename. Contents: the shipped-AI-data inventory measured against
the retail extraction; the behavioural specification re-expressed in our own words from the original
Game Design Document v1.03 (no source prose reproduced — the doc is not redistributable); a build
assessment of what the engine can reuse unchanged versus what blocks the milestone; seven open
questions the source cannot settle; and a 20-item wave plan whose ordering differs from the
commissioning proposal in four places, with the reasoning for each.

**The headline.** **The flying aircraft has no physics body** — `FlightController` is a bare
`Node3D`, `PlaneBuilder` never generates collision, and `PlaneCollider`'s `BoxShape3D`s are used
query-only as the source of `CastMotion`/`IntersectShape` calls. Nothing can shoot a plane; three of
the six `IMPACT` surface classes (`Player`, `Enemy`, `Quicksand`) are unreachable; there are **zero**
`CollisionLayer`/`CollisionMask` assignments repo-wide. That gates the entire milestone. Against it,
`FlightModel.Step(FlightInput, dt)` takes a four-float struct with no player coupling and
`ProjectilePool.Spawn` already serves three unrelated callers, so the flight and weapon halves of an
AI pilot are free.

**Verified against real data**, all measured this session and reproducible: 222 chapter-scoped patrol
**graphs** (`ne0NNNNN.zrd.json` + `neindex.zrd.json`, 2,268 nodes / 2,149 explicit edges) — *not* the
per-mission `net.zrd.json` the brief assumed, which stays undecoded; 414 AI vehicle blocks over 53
`aiv.zrd.json` with slots 0/1/2/6/20/31/32/33/39/65 decoded; 42 self-describing turret specs in two
structural families with all 8 `WEAPON.NAME` ids resolving in `weapons.zrd.json`; 23 generators in
three shapes; 58 zeppelin records whose `cannon_fire_delay` (20 s) and 0.60/0.30 cannon damage stages
match both the design and M3's own destructible census; and 1,309 combat voice clips over 31 pilot
ids in 125 tokens across 11 families. **Twelve claims from the commissioning brief were wrong or
overstated and are tabulated in the document** so they are not re-derived.

**New instrument.** `analysis/m4-ai-data/` (`aiv_skill_slots.py` + `FINDINGS.md`): the per-pilot skill
vector is `aiv` slots **22–30** — **nine** stats on a 1–9 scale, `-1` on ~380 blocks and complete on
exactly 29, every one a named pilot. `ia.zrd.json`'s `ace_stats` is independently **nine** values in
all 8 chapters, which fixes the count from a named key rather than an inference and turns "decode the
AI parameter block" from open-ended RE into a bounded nine-way mapping question. The slot→stat
**order stays unresolved on purpose**: the stunt plane and the cabbie point at different orderings,
and the document records the discriminating test rather than picking. Two design-doc ambiguities (the
inverted composure-check wording, two off-by-one loop-back step references) and one design-vs-data
scale conflict (0–100 formula vs shipped 1–9) are recorded as decisions to make, not resolved.

**Docs-only change.** No engine code touched; `CLAUDE.md`'s "Current status" deliberately untouched —
M4 is not scheduled and `PLAN-testing.md` remains the sole active plan.

## 2026-07-25 — `playtest.md` reconciled with the design-documentation cross-check

Folded the design cross-check (the sibling pass that produced `backlog.md`'s combat-fidelity
entries) into the at-the-controls checklist, so nobody repeats it.

**Retired.** §7's danger-zone item stops being a design investigation: the mechanism is settled —
each zone has an entry volume and an exit volume and **both** must be crossed, which is exactly the
"fly around the danger and still score" failure the one 15 m `DzRadius` sphere has, and it accounts
for the two gate polygons each `dzpathN` mesh carries. It is now a post-fix verification. §1's E37
item lost its convergence framing: the fixed reticle is airframe-locked with all weapons on that
centre and the floating one exists only because inertia makes shots lag in a turn (velocity
inheritance, already modelled), so `GunConvergenceDist` 250 m is a drawing distance, not a value to
validate.

**Given a written target so they are judgeable rather than A/B-able.** Gun visuals, from the retail
captures (`OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` + `…ejection2.png`):
short yellow dashes, a yellow-core/orange-flame flash elongated forward at the **wing** mount one
wing at a time, and ejection as a brass casing plus a white puff cluster that persists and drifts
aft — a **trailing emitter**, since shot 2 has a cluster well aft while a fresh casing is still
leaving the wing. The spec's calibre gate and underbelly mount are rejected against
`CSVM/data/stock_loadouts.json` (the Bloodhawk's fit is 40-cal + 30-cal wing guns). Rocket trails
are **per type** (HE white puffs, flak black, incendiary red-hued, sonic sine-wave), not one streak.
Collision feel gets the spec's acceptance criteria — survivable canyon-wall bounce, billboard
destroyed with the plane barely scratched, damage scaling with weight × speed × angle of attack and
spreading to adjacent zones (ours is single-zone with neither term). The ammo gauge's dropped yellow
tier is noted as having been a *heat*/jam axis, so the removal decision stands unchanged.

**Three checks added** that were never on the list: the low-altitude warning should beep as well as
flash; the stall warning should be graded, rising as the stall approaches, where ours is binary; and
the damage gauge ramps Blue → Green → Yellow → Red above 20 % — that 20 % **matches our shipped
`*_damage_red` exactly**, measured 0.20 on all 44 zone entries in `extracted/zrdr/vehicle.zrd.json`.

**What the cross-check could not answer is now recorded in the file's header**, which is the point of
the edit: §2 entirely (the original's multiplayer was networked — no splitscreen reference exists),
every tuning question in §3–§5 (qualitative rules, no numbers), and in §8 the C3 spiderweb (its
Hawaii mission-visuals list names fog, the bridge collapse, waterfalls, torches and seagulls, no
web), patrol-boat hit points, map-edge continuation, north's world axis, and the crossed `pdpN_h`s.
The crash "fireball leads the explosion by 0.5 s" claim was **not** added — `player_crash_dirt`
authors the ground boom before the fireball cascade and contradicts it.

## 2026-07-25 — Wave D playtest passed; the collider overlay found a gameplay bug

**Verdict.** All five inspect-tool playtests passed at the controls. D31's zeppelin case — the
acceptance test the plan named as the user's call — came back "works really good, exactly what i
imagined". Recorded in `playtest.md` §9; five follow-ups went to `backlog.md`.

**The finding worth the whole item.** D35's wireframes showed some C2 buildings in the water colour
and some water in the building colour. That is not an overlay defect: `ColliderOverlay` colours by
`SceneBuilder.SurfaceMeta` and `Projectile` picks the impact sound and effect from the same
metadata, so those buildings answer a hit with a water splash. `SurfaceForMesh` votes over a mesh's
polygons while **skipping the unclassified ones**, so the winner is a majority of whatever matched a
name pattern rather than of the mesh — measured, **96 of C2's 190 tagged meshes are tagged on a
minority of their own polygons**, the thinnest `water` on 1 polygon of 76 (C1 42/109, C4 70/106).
Numbers and the replication script: `analysis/surface-classification/`.

**Two fixes checked and rejected before filing.** Every material carries a `soil` field that looks
like the original's own classification, but it is almost entirely `Default` — C2 has one `Water`
material out of 484, and its other values are MechWarrior 3 soil types inherited from mech3ax. And
the design document describes the system's shape (impact effect and sound follow the surface hit,
water gets a column and a splash) but not the mechanism; searched and recorded as a dead end so
nobody repeats it.

**Also landed.** The design-document cross-check branches (`gdd-combined`), per their handoff: doc
corrections, the shipped command table, key mapping, seven combat-fidelity backlog entries, M4 AI
scoping and a reworked `playtest.md`. Its three verification rules renumbered to 108–110 behind
main's, and three stale rule citations were corrected on the way — two of them pre-existing in main,
left over from B12's own renumber.

## 2026-07-25 — playtest.md trimmed to the owed list

`playtest.md` had accumulated the record of what already passed, which made it a log rather than a
checklist. Retired here so the file stays actionable:

**Wave D inspect tools — all five passed at the controls 2026-07-25.** D31's zeppelin case, the
acceptance test the plan named as the user's call, came back "works really good, exactly what i
imagined". D32: the tree, search, framing, hide and the destructibles filter all work, and the
500-row branch cap was judged well beyond what a human needs. D33: the panel, its readouts and the
restore-on-close all work, and light steering works on world geometry. D35: the wireframes work and
the no-collision warning path is confirmed. D34: it selects the right pool, the slider and
kill/reset work, and the readout reads comprehensibly. §9 now carries only the six re-tests the
follow-ups will need.

**Milestone 3 pass 1 — retired from the checklist.** Passed 2026-07-25: destruction sound including
secondary oil-tank explosions; pad bindings; gun rate, cadence and sound with in-flight muzzle
alternation; rocket one-per-pull and its cooldown feel; the weapon selector's feel and default
group; the gauge readouts; the reticle trailing the nose; and the collision behaviours — crashing
into a building destroys the plane and leaves the building standing, while a filmset facade is
flown through unharmed.

The §1 failure bullets kept their re-test but lost their diagnosis, which duplicated `backlog.md`.

## 2026-07-25 — thrust calibrated against video of the original; a flight-envelope suite to hold it

The flight model's thrust scale was **4.6× too small**, and this is the fix. `ThrustConst` 40 → 184,
which is a max thrust acceleration of 60 m/s² on the Bloodhawk. The source is the video-decoded
measurement in `analysis/video-flight-calibration/` (committed here, with its own `FINDINGS.md`
carrying the numbers and the traps): the original goes from 150 to 290 mph at full throttle in 3.76
sim seconds where we took 17.2. One constant fixes that *and* the terminal dive — the same 60 m/s²
predicts a 70.7° dive terminal of 1.178 × fd_speed against the video's measured 1.182 — so the drag
*shape* (`0.65x² + 0.35x`) was right all along and only the scale was wrong. Level top speed is
untouched by construction (drag is normalized so drag(fd_speed) = max thrust) and measures 302.0 vs
the original's 300.4 mph.

`MaxDiveSpeedFrac` 1.7 → 1.75, and re-labelled: terminal dive is now emergent and correct, so the
constant's job is a numerical backstop above every airframe's own emergent terminal (worst is the
Balmoral's 1.708) rather than a stand-in for one. At the old thrust it *was* binding — the drag
curve's emergent terminal there was 1.72 — which is why its old comment called 1.7 "≈ terminal dive
from the drag curve" and why our dives all pinned the cap.

**The handoff's own prescription was wrong and the data caught it.** It specified `ThrustConst` 243,
back-derived from an engine power of 0.47 — the Bloodhawk *Lvl-1* row. Its `engine` is 11, Lvl-2,
0.62, which is what `PlaneStats` has always read, so 243 would have over-thrust by 32%: 2.83 s on
the acceleration against the measured 3.76, and a terminal dive of 1.137 against 1.182. The video
measurement (A ≈ 60 m/s²) survived; only the arithmetic converting it into a constant did not. Now
`verification.md` rule 113.

**The rate constants were re-checked, not assumed** — raising thrust raises speed, and speed scales
the yaw `eff`. `PitchTune` 0.75 / `YawTune` 1.32 / `RollTune` 2.12 all still land: roll 360° 1.98 s
(video 2.05), sustained pitch 33.5 °/s (33), rudder 360° 29.75 s (28.6, ours holding 301 mph where
the original held 290). Two long-open questions close with them: **pitch authority is not sluggish**
(`playtest.md` §3's "the big one"), and the original's **pitch rate does not fall off with speed**
(37.9 / 33.7 / 30.7 / 36.5 °/s binned over 120–280 mph round a loop), so our speed-independent
pitch is the right shape.

**New instrument: `--dump-flight` + the `flight-envelope` suite** (`Probes.FlightEnvelope`). It
steps a throwaway `FlightModel` — no world, no scene, zrdr readers only — through the manoeuvres the
original was recorded flying and prints both numbers side by side; the suite asserts the six that
have measured targets. This exists because the constants are coupled and a screenshot cannot see any
of it. Three rows are printed but deliberately **not asserted**, and each is now a backlog entry: the
1/8-throttle equilibrium (93 mph vs 138) and the 1/8-throttle deceleration (2.47 s vs 7.04), which
jointly indict the undecoded throttle→thrust curve or the low-speed drag blend without saying which;
and the zoom climb, which arrives at its apex still doing 266 mph where the original bottomed at 104
— **we model no induced drag at all**, the sharpest form yet of the old "the original bleeds speed
in a hard pull" observation.

Verified: `RunTests.ps1` green — 152 units, 9 suites, engine errors clean. The able-to-fail control
is the prescribed one (flip the line, build, run, flip back): at `ThrustConst` 40 the suite fails on
`accel-150-290` (+357%) and `terminal-dive` (+43.8%) and on nothing else, so it is pinned to the
thrust and not to the scenery. Two goldens moved, both flight shots and both expected, regenerated
here: **`empty-stage`** and **`c1-flight`** — the plane is simply further along its `--hold` path.
The other nine, all `--freecam`/`--viewer`, are unchanged.

**The git-ignored `CSVM/config.json` was deleted as part of this** (user's call), because it would
have made the landing invisible in the cockpit: it pinned `thrustConst` to 40 and `pitchTune` to 1.5
(double the calibrated 0.75), and `--det` drops that file while interactive runs honour it — so the
suites would have seen the new defaults and the pilot the old ones. Its other twelve keys were
byte-identical to their in-code defaults, so nothing else changed. Now `verification.md` rule 112,
because the next such file will do the same thing.

## 2026-07-25 — the two doc indexes de-duplicated, and the drift they were hiding

Both `CLAUDE.md` mirrors of a `docs/` list were collapsed to one copy each, after the file hit
35,847 bytes — past its own ~35 KB budget.

**The module index.** 92 one-line entries, 12,986 bytes, 36% of the file, mirroring
`docs/architecture.md`'s 94 `##` entries 1:1. Moved verbatim (harvested by script, not retyped) to a
new `## Module index` at the top of `architecture.md`, grouped by namespace with an orienting
paragraph each; `CLAUDE.md` keeps an 8-line namespace map with entry counts plus the eight
highest-traffic modules, so the common cases never open the file. The move surfaced the drift the
duplication had already caused: **`src/Testing/GoldenShot.cs` had an architecture entry and no index
line**, and `CSVM.Tests/` had none either — 92 → 94, now verified 1:1 in both directions.
`architecture.md` 125.6 → 139.1 KB, self-navigating: read its index, then `Grep "## src/<path>" -A 12`.

**The flag table.** Not the same case, and it was kept. It is 3,642 bytes (not 13 KB), a curated
*subset* (28 of 89, not a mirror), and `cli.md`'s bullets are one line each so grep already returns a
whole entry. A both-directions name diff found **zero** coverage drift. What `cli.md` actually lacked
was any index at all — 89 flags in a flat, ungrouped 86 KB list — so it got a `## Flag index` of all
89 in 13 categories, **names only**: a gloss there would be a second description of the same flag,
which is the failure being fixed. Coverage machine-checked, 89/89, none unassigned, none invented.

**The drift the list check could not see.** The name-level diff passing at 28/28 was itself the
misleading instrument. A reading pass over the same 27 rows found four contradictions, each verified
against `cli.md` before the fix: `--frames` glossed as a wall-clock *delay* when under `--det` (which
`--screenshot` implies) it is the sim frame and changes what is captured; `--debug-anim`'s
edge-triggered condition logging described as once-a-second, inverting its signal (silence means
unchanged, not broken); `--collision`'s C wireframe overlay claimed for `--viewer`, where C is the
mesh lab's cull cycler and the flag binds no overlay; and `--view`'s **settled** layout lumped in
with its TUNE magnitudes, reopening a decided question. Two borderline also fixed — `--stage=empty`
said "no gamez at all" while `planes.zbd` still loads (~490 ms of the ~2 s boot), and `--no-det` did
not say it beats an *explicit* `--det`. Now `verification.md` rule 114.

The root cause was that nothing ranked the copies, so no side was correct to fix toward. Three rules
now do: a new/renamed/deleted module is an `architecture.md`-only edit (index + entry together); a
new flag adds its `cli.md` index entry and bullet together, and `CLAUDE.md`'s rows are **glosses,
never the description of record** — a behaviour change edits the bullet. The old "a module index
entry is ONE line" shape rule, which had licensed the duplication outright, was replaced by "never
restate a list another file indexes — point at that file".

Also landed: `docs/agents/` (`issue-tracker.md`, `triage-labels.md`, `domain.md`) and a
`## Agent skills` section, configuring the installed engineering skills — issues are this repo's own
markdown (`backlog.md`, a live `docs/PLAN-*.md`, `playtest.md`), **not** the stock template's
`.scratch/`, which `CleanScratch.ps1` sweeps.

Verified: docs-only, no build. Both indexes machine-checked against their targets in both directions
(94/94 modules, 89/89 flags); every one of the six CLI corrections read back against the `cli.md`
bullet it now agrees with; the documented lookups (`Grep "## src/Flight/FlightModel.cs" -A 12`,
`Grep "^- .--collision" docs/cli.md`) each return exactly one whole entry. No script, test or `.cs`
file parses either document. `CLAUDE.md` 35,847 → 25,246 bytes.

## 2026-07-25 — three defects an architecture review surfaced, and the last of the suite-count drift

An architecture review of the hot spots (`PlaneViewer.cs`, 37 of the last 60 source touches) turned
up three live defects, all of the same shape: a fact restated per consumer instead of derived once.

**A failed `--dump-*` exited 0.** `DumpMarkers`/`DumpWeapons`/`DumpLoadout`/`DumpFlight` printed the
probe's error and `return`ed, while the caller ran a bare `GetTree().Quit()` — so a missing archive
read to any script exactly like a clean dump. Each now returns its verdict and the caller quits with
it, the way `RunTestSuites` already did.

**Three spellings of "does this session build colliders", each missing a different term.** The
authoritative one fed `WorldSession.Options.Collision`; the copy at the node lab and world damage
lab had dropped `--collision`, and the copy at the C overlay had dropped `--debug-damage`. So
`--collision --freecam` told both labs nothing was built, and `--debug-damage` made the C overlay
print "this mode built NO collision" over colliders that existed. All three now read one
`PlaneViewer.BuildsCollision`.

**`--effects-test` and `--weapon-test` did not imply `--det`,** though both drive and end a session
with nobody at the controls — the rule the other seven scripted flags follow. `--effects-test` had
been half-patched around it with an `|| _effectsTest` term on `_seedPinned`, which is now redundant
and gone. Both are in the bundle; the membership rule ("a flag that drives and ends the session by
itself") is now written down in the `cli.md` `--det` bullet, whose list had also been missing
`--dump-flight` and `--run-tests`.

Docs: `cli.md`'s suite list still said **seven** and omitted `flight-envelope` and `tex-dropin`
(`architecture.md` already said nine). `HISTORY.md` and `PLAN-testing.md` also say seven and were
left alone — they record what was true when written.

Verified: `.\RunTests.ps1` PASS — 152 units, 9/9 engine suites, 11/11 goldens hash-identical, exit 0,
72.9 s. Each defect then checked directly rather than inferred from the green run: a dump with
`--data-root=` at a nonexistent path exits **1** where a real one exits **0**; `--debug-damage
--debug-colliders` no longer prints the NO-collision warning while a plain `--freecam
--debug-colliders` still does (the able-to-fail control); `--collision --debug-nodelab=destructibles`
reports the committed C1 census, 267 instances / 196 node groups. Pinning did not move either
probe's documented result — `--effects-test` still 28/28 resolved and **16** building a puffer,
`--weapon-test` still 48/48 fired, 0 errors, 0 skipped — and both now log `via=--effects-test` /
`via=--weapon-test`; `--no-det` still opts back out. New rules 115 (an instrument must quit with its
verdict) and 116 (a derived predicate copied per consumer reports the absence it created).

## 2026-07-25 — PLAN-sessionspec A1: `--dump-session` and the launch-resolution baseline

The first item of the SessionSpec refactor, and the safety net the rest of it depends on. Landed:
`--dump-session` prints every setting a command line resolved to — mode arbitration, the `--det`
bundle, placement, paths, every probe/debug/modifier flag — as 124 sorted `key = value` rows to
stdout and `.scratch/session_dump.txt`, then quits with `Probes.Session`'s verdict. It is written
against `PlaneViewer`'s *current* fields on purpose: its whole job is to record what the code does
today, so a ~1,000-line hand edit can be shown not to have changed it.

`analysis/session-baseline/` holds the instrument's durable half: `capture.ps1` (50 command lines),
the committed `baseline.txt`, and `FINDINGS.md`. The plan had drafted this into `.scratch/`, which is
wrong for something that must survive to A4 — `.scratch/` is swept by `CleanScratch.ps1`, and
`analysis/README.md` records what that already cost once with `probe_exempt.py`. The matrix is
weighted toward what the pixel goldens cannot see: `--anim-lab`, `--stunt`, splitscreen and the menu
path have **no golden coverage at all**, so a green `RunTests.ps1` is not evidence that a launch
still resolves the way it did.

**The instrument had to be made trustworthy before the baseline meant anything**, and three defects
in the first draft were found by reading its output rather than by reasoning about it. The resolved
master seed is drawn from the clock whenever nothing pins it, so every non-deterministic row differed
from itself on the next capture — it now prints `<clock>` unless pinned, because the reportable fact
is that it came from the clock, not which number came out. Absolute paths made the baseline one
machine's; they render against `{data}`/`{repo}` tokens. And the report is ASCII + LF only: the
PowerShell 5.1 harness turns a single em dash into mojibake that then reads as a diff on every row.
A fourth was in the harness — `$out` (each run's console output) silently overwrote the `$Out`
parameter holding the destination path, because PowerShell variable names are case-insensitive.

**The design decision worth keeping**: `--dump-session` satisfies the `--det` membership rule (a flag
that drives and ends a session by itself) and the `_mode` naming rule, and obeying either would have
destroyed it — implying `--det` prints `det.on = true` on every row of a matrix whose entire subject
is which command lines turn the bundle on, and joining the `_mode` chain reports the observer's mode
instead of the session's. It is the documented exception to both; the one concession, window focus,
is applied to the *decision* rather than folded into the predicate, so the rule's own expression
stays clean. That generalised to rule 117.

Two latent defects the baseline exposed, recorded and deliberately **not** fixed — A1 records current
behaviour including its warts, or it is not a baseline. (1) `_dumpFlight` is missing from `_mode`'s
"dump" chain, so a `--dump-flight` run writes its log as `menu-*.log`; that is a fifth copy of the
rule-116 one-term-at-a-time drift. (2) `--run-tests` resolves `mode.showsMenu = true` and is saved
only by returning before the menu branch — the test harness's correctness currently rests on
statement order in `_Ready`, which is exactly what A3 replaces with a total function. Both are
carried in the plan's A1 section as ⚠ notes for A3.

Verified: two captures of the same build are md5-identical (`944310579BA214A0E99B801FC344098B`).
Able-to-fail exercised on a deliberately perturbed build — renaming `--stunt`'s forced scenario moved
exactly the `stunt-*` rows, and a duplicated field key printed `!! duplicate key: mode.fly`,
`125 settings, 1 DUPLICATE KEY(S)` and **exited 1**; after reverting both, `capture.ps1` reproduced
`baseline.txt` byte for byte. `.\RunTests.ps1` PASS — 152 units, 9/9 engine suites, 11/11 goldens
hash-identical, exit 0, 72.5 s. New rules 117 (an instrument must not be a term of the rule it
reports) and 118 (a baseline holding a clock-derived value or an absolute path is not a baseline).

## 2026-07-25 — the test run's console output went to the terminal, not to the caller

`RunTests.ps1` had been printing Godot's entire world-build chatter straight onto whatever terminal
it was started from, out of band, since the focus change three commits back. The script could not see
it: a Windows GUI-subsystem binary started without std handles calls
`AttachConsole(ATTACH_PARENT_PROCESS)` and reopens stdout on `CONOUT$`, so the text bypasses the
caller's pipes entirely and lands on the console screen buffer. That commit's own measurement —
"hands the call operator no output at all (measured: 0 lines)" — was true and pointed the wrong way;
it read the silence of the pipe as silence of the process. Reproduced minimally: launched with no
redirection, `--version` printed nothing the caller could capture; launched with
`RedirectStandardOutput`, the same 36 bytes arrived in the file and nothing reached the console.

`Invoke-Godot` now starts the process through `ProcessStartInfo` with both streams redirected, parked
next to that launch's own `--log-file` as `<log>.out` / `<log>.err` so a crash in shot 3 of 11 leaves
its evidence beside shot 3. `Start-Process -RedirectStandard*` cannot do this job: it hands back a
disposed object whose `ExitCode` reads as **empty**, which is not an error in PowerShell, so the
first version of the fix scored a run of 9/9 passing suites as FAIL — the stage's verdict rests on
`$engineCode -eq 0` and `$null -eq 0` is false. Both pipes are drained asynchronously before the
wait, or a chatty launch fills the ~4 KB buffer and deadlocks. Three comments that had recorded the
wrong conclusion ("writes nothing to our stdout") were corrected rather than left as a trap.

Verified: `.\RunTests.ps1` PASS — 152 units, 9/9 engine suites, 11/11 goldens hash-identical, exit 0,
73.3 s, with the engine's own text now appearing only where the script echoes it. The perf stage,
which shares the helper, PASS 5/5. The focus fix that started this is intact: a process-name
foreground probe sampling every 250 ms across a full `-Perf` run put Godot in the foreground for
0 of 400 samples. New rules 119 (captured nothing ≠ printed nothing) and 120 (a PowerShell property
that throws yields `$null`, so a verdict reads the failure as a value).

## 2026-07-25 — a scripted run's window no longer opens in front of you

`no_focus` stopped scripted runs stealing the keyboard, but an unfocused window still *opens on top*
of whatever you are reading, about twenty times per `RunTests.ps1`. A scripted session now hides its
window outright — `PlaneViewer.HideScriptedWindow` calls `ShowWindow(SW_HIDE)` on the native handle,
off the same predicate that decides an interactive session should ask for focus.

Godot has no lever for this, which is why it took interop: there is no always-on-bottom window flag
(the `WindowFlags` enum was read out of `GodotSharp.dll` to be sure), `WindowMoveToForeground` has no
opposite, and `--position` is clamped so about a third of the window stays on the desktop — 5184 and
10000 both land at 4686 on a 5120-wide desktop. The obvious lever, creating the window minimized via
`display/window/size/mode=1`, is the one thing that must not be done: a minimized window does not
render, and it put 6 of the 11 goldens on one identical blank hash while leaving the other 5 passing,
so it read as a partial regression rather than a broken instrument. Hiding costs nothing — all 11
stay hash-identical.

A wrong turn worth keeping: the first attempt used the clamped `--position` alone, and a full green
goldens run was taken as proof that an off-screen window still renders. It proved nothing — a rect
probe showed the window sitting at 4686,-31, still on the desktop the whole time. The verdict was
right and the reasoning was worthless, which is now rule 122.

What remains is a **~1 s flash**: the window exists from ~180 ms and `_Ready` cannot run before
~1180 ms. `RunTests.ps1` passes the clamped `--position` anyway so that second happens at the far
edge of the desktop rather than mid-screen. Closing it entirely needs a separate Windows desktop or
`--wid` embedding, neither attempted.

Verified: `.\RunTests.ps1` PASS — 152 units, 9/9 engine suites, 11/11 goldens hash-identical, exit 0,
74.5 s. A window probe attributing by PID across that run saw a Godot window visible in 82 samples
and hidden in 562, every visible one at 4686,-31. Both directions of the predicate checked
separately, since getting it wrong either covers the desktop or launches somebody's game invisible:
`--stage=empty` visible 55 of 59 samples, the same run with `--no-focus` hidden 52 of 58. New rules
121 (minimized does not render, hidden does) and 122 (confirm the intervention took effect before
crediting the result to it).

## 2026-07-25 — the test run moved to a Windows desktop nobody is looking at

Hiding a scripted run's window from `_Ready` left a ~1 s flash per launch — the window exists from
~180 ms and `_Ready` cannot run before ~1180 ms — which is about nineteen flashes per `RunTests.ps1`.
That second is unreachable from inside the engine, so the launcher stopped trying: `HiddenDesktop.ps1`
creates a second Windows desktop with `CreateDesktop` and starts every Godot process on it via
`CreateProcess` with `STARTUPINFO.lpDesktop`. A window belongs to the desktop its process was started
on and only one desktop is ever displayed, so the question is settled before the process runs, which
is the only kind of placement that works (rule 107). The engine-side `SW_HIDE` stays for ad-hoc runs
that go through neither script.

Going through `CreateProcess` by hand means building the std handles by hand too — inheritable
`CreateFile` handles for stdout/stderr and `NUL` for stdin — because without them the GUI binary
reattaches to the launching console and prints past every redirection (rule 119, two commits back).
The summary now names the desktop it used: a silent fallback to the visible one is indistinguishable
from success until windows start appearing, and by then nobody connects the two. A refused desktop
degrades to a visible run rather than failing the tests, verified by asking for an invalid name.

Verified: full `.\RunTests.ps1` PASS — 152 units, 9/9 engine suites, 11/11 goldens hash-identical,
exit 0, 79.8 s — while a probe sampling our own desktop every 50 ms saw a Godot window in 0 of 700
samples with Godot alive in 697 of them, and never saw one hold the foreground. Perf on the hidden
desktop is unchanged: draw counts identical (198.2 / 900.5 / 907 / 2181 / 1192.1), frame costs within
noise. The single-shot proof that the GPU still works there — golden `pixmd5` exact, 0 of 52 samples
on our desktop, 50 of 52 on the new one — is kept as `analysis/hidden-desktop/`, together with the
two rejected alternatives (create-minimized, which does not render; `--position`, which Godot
clamps). New rule 123: `EnumWindows` only sees the calling desktop, so "no window found" is what
success and a dead process look like alike.

## 2026-07-29 — PLAN-sessionspec A2: `SessionSpec` + `Parse`, raw values only

`CSVM/src/SessionSpec.cs` is a `sealed record` carrying everything the command line settles —
grouped by the A1 dump's prefixes — plus `Parse(args)` and the seven arg parsers that were private
to `PlaneViewer`: `ParseVec3`, `ParsePlanes`, `ParseView`, `ParseHold`, `ParsePaintColors`,
`ParsePaintDecals`, `ParseDamagePreset`. Nothing calls it, which is the point: the change is
behaviour-neutral by construction, and A3 gets a settled surface to resolve from.

Two lines were drawn deliberately. **Raw means raw** — a flag records only itself, so the parse-time
implications in today's loop did not come along (`--markers`/`--damage` no longer imply the viewer,
`--damage-test`/`--effects-test` no longer imply the freecam, `--play-anim=` no longer implies the
anim lab, `--stunt` neither forces flight nor moves the scenario). Those are resolution, and A3's
own trap note says the three probe flags wearing a mode as a disguise must be modelled as probes or
the enum inherits the lie. **`Parse` is pure** — no `Pads.Disabled`, no `TextureDropIn`, no
`Log.Configure`, no logging at all: the three side-effecting branches are recorded as data and
complaints accumulate in `Warnings` as `(category, message)`. `Log` ends in `GD.Print`, and B7's
truth table runs in `CSVM.Tests`, which has no Godot runtime to print into. The two lab spec
grammars (`UI.NodeLab`/`UI.WorldDamageLab.ParseDebugSpec`) stayed in their labs for the same
reason — they log as they filter — so the spec carries those two values verbatim.

Verified: `.\RunTests.ps1` PASS — 152 units, 9/9 suites, 11/11 goldens hash-identical, exit 0,
65.9 s. The load-bearing check is A1's instrument rather than the goldens, which do not reach the
modes this plan touches: the 50-row matrix re-captured through `analysis/session-baseline/
capture.ps1` is md5-identical to the committed baseline (`944310579BA214A0E99B801FC344098B`).

## 2026-07-29 — PLAN-sessionspec A3: the launch args resolve themselves

`SessionSpec.Parse` now returns a resolved spec. `SessionMode` is closed — Menu/Fly/Viewer/Freecam/
AnimLab — and every flag that names a mode is a *vote* gathered before anything is arbitrated:
`--viewer`/`--damage`/`--markers`/`--weapon-*` vote viewer, `--freecam`/`--damage-test`/
`--effects-test` vote freecam, `--anim-lab`/`--play-anim=`/`--debug-anim-ui` vote anim lab. That is
where the parse-time implications A2 refused to carry now live, so the five mutating `if` blocks
work on locals and end in one `Mode` assignment. `Fly`/`Viewer`/`Freecam`/`AnimLab` are computed
from it, and eight more predicates are computed rather than stored — `ShowsMenu`, `ModeName`,
`IsScripted`, `ScriptedBy`, `Det`, `DetVia`, `SeedPinned`, `PadsDisabled`, `BuildsCollision` — which
is what makes "exactly one definition each" structural instead of a promise. `SessionProbe` names
the three probes that wear a mode as a disguise, so the enum does not inherit the coercion.

Purity survived resolution, which took two decisions. `PinnedSeed` is null when the seed is
unpinned instead of drawing `Rng.TimeSeed()`: a spec that read the clock would not be a function of
its args, the same defect A1 had to fix in the probe (rule 118). And the two lab spec grammars
gained an optional `rejected` list — supplied, `UI.NodeLab`/`UI.WorldDamageLab.ParseDebugSpec` hand
tokens back as data instead of `Log.Warn`ing them — so `--debug-nodelab=`/`--debug-damage=` are
normalised in the spec with no Godot runtime under it. Two known defects are reproduced on purpose,
because the A4 gate compares against today: `ModeName` omits `--dump-flight` from its "dump" arm,
and `ShowsMenu` is true under `--run-tests`.

Verified: `.\RunTests.ps1` PASS — 152 units, 9/9 suites, 11/11 goldens hash-identical, exit 0,
64.0 s — and A1's matrix re-captured md5-identical (`944310579BA214A0E99B801FC344098B`). Since
nothing calls the spec, neither could have moved; the evidence that the RULES are right is a
throwaway xUnit check that replayed all 50 baseline command lines through `SessionSpec.Parse` and
compared every row the spec resolves — 2,400 values across 50 rows, 0 mismatches, engine-free, which
also proves `Parse` stays GD-free. Shown able to fail by inverting `ShowsMenu`. It was deleted
rather than kept, because A4 owns the field-vs-spec gate and B7 the truth table; what it buys A4 is
a start from 2,400 already-agreeing values against the frozen field side.
