# Milestone 2 Polish Plan — Run 2

Working plan for NOTES.md's "Milestone 2 Polishing Run 2" section plus the three still-open
"Issues" bullets (dive sound, skybox color grading, turn rates), in the agreed implementation
order. Each item lists its goal, the data/code evidence it rests on, the approach, and how it
gets verified. Statuses: ☐ open · ◐ in progress · ☑ done.

Ground rules carried over from CLAUDE.md: original-game data drives everything (zrdr readers +
gamez/planes extractions); hand-tuned constants are marked TUNE and validated by user playtests
against the original; CLAUDE.md is updated in the same turn as each landed item. New this run:
every newly decoded reader format lands together with its `docs/formats/` page (item 13).

Scope decisions from the 2026-07-17 grilling session are recorded in the footer.

## Checklist

1. ☑ Log hygiene — missing-texture + clutter warnings: once, without stack traces **(DONE 2026-07-17)**
2. ☑ C4/C5 shader instance-uniform errors (buffer size; + C5 `rtexture*` check) **(DONE 2026-07-17)**
3. ☑ C4 white fog — FOG_COLOR integer-RGB schema fix **(DONE 2026-07-18)**
4. ☑ Clouds render through fog — fog term for cloud sprites + puffs **(DONE 2026-07-18)**
5. ☐ Precipitation — RAIN (C1C/C2B) + SNOW (C4) from the weather TYPE block
6. ☐ Night brightness calibration — deck + sky vs original
7. ☐ Map edge continuation — border tiles extend outward (terrain→terrain, sea→sea)
8. ☐ Mission states (anim-state engine pt 1) — zepstate/startanims; fixes destroyed-variant flicker
9. ☐ Animated vehicles (pt 2) — train/car path motion + steam puffers
10. ☐ Collision damage model — collider fit → part HP + severity → visible damage → crash breakup
11. ☐ Dive sound — tune down (ours reads louder than the original)
12. ☐ Turn rates — split `rotationTune` per axis, calibrate vs measured original
13. ☐ `docs/formats/` — public reader-format reference, seeded with this run's decodes

---

## 1. Log hygiene — repeated warnings with stack traces

**Goal:** Startup/build logs show each real problem once, in one line. Today a single missing
texture prints a ~24-frame managed stack trace, and C5 repeats one clutter warning dozens of
times — the noise eats Claude-session tokens and buries real errors.

**Evidence (reproduced 2026-07-17, c4-fly.log / c5-fly.log):**
- `pir_spinner.tif` is referenced by **every** chapter's gamez (`materials.json`/`textures.json`
  in all 8 chapters) but the PNG exists **nowhere** in the extraction — the game data genuinely
  lacks it (the original engine evidently tolerates that). C5 additionally references the
  equally-absent `barngrill.tif`. Each miss goes through `TextureArchive.cs:83`
  `GD.PushWarning`, which in Godot .NET prints the full managed stack.
- C5's `cblock1` clutter template logs `decoration 'cbNNa.flt' is not a sprite quad — skipped`
  (`Clutter.cs:158`) once **per stamped polygon**, not once per decoration — dozens of
  identical warnings, each with a stack trace.

**Approach:**
- TextureArchive: keep a per-archive `HashSet` of already-reported names; report each missing
  texture once via `GD.Print` (no PushWarning → no stack trace), plus one end-of-build summary
  line. While in there: find what mesh actually uses `pir_spinner` (likely a pirate-zeppelin
  spinner disc) and check a C1 screenshot for a visible magenta patch — if visible, choose a
  quieter fallback (neutral gray or skip) for known-absent textures.
- Clutter: dedupe the warning per (template, decoration-name) pair.

**Verify:** C1/C4/C5 build logs: each missing texture exactly one line, `cblock1` ≤ one line
per decoration kind, zero stack traces from these paths.

**DONE (2026-07-17):** `TextureArchive` now reports each unresolved name once via `GD.Print`
(a `_reportedMissing` HashSet; no more `GD.PushWarning` managed stack trace), exposes
`MissingTextures` for the one-line end-of-build summary PlaneViewer prints, and a small
`KnownAbsentFromGameData` set (`pir_spinner`, `barngrill` — verified absent from every
extracted chapter) that `IsKnownAbsent` reports as a data gap; SceneBuilder renders those as
neutral gray (0.5) instead of the debug magenta, keeping magenta only for genuine
lookup-resolution failures. `Clutter.ParseTemplate` collapses the per-decoration "not a sprite
quad" spam into one summary line per template (distinct example names + true count) and drops
the stack traces on its other two warnings. Verified via windowed `--fly --screenshot` runs on
C1/C4/C5: C5 went from ~169 stack-trace warnings (7 cblock templates) to 7 one-line summaries;
each of `pir_spinner`/`barngrill` is one line + the summary line; grep for stack-trace frames
across each full log = 0; C1's 9,303-sprite clutter and normal load are unchanged.

## 2. C4/C5 "too many shader instance variables" errors

**Goal:** Rocky Mountains (C4) and New York (C5) load and fly without the error flood — and
without silently broken decal layering.

**Evidence (reproduced 2026-07-17):** flying either chapter floods
`ERROR: Too many instances using shader instance variables. Increase buffer size in Project
Settings.` plus `global_shader_parameters_instance_allocate … Returning: -1`
(material_storage.cpp:2028). Cause: SceneBuilder sets the `node_bias` **instance uniform** on
every world MeshInstance3D (the cross-node draw-order tie-break); Godot's global buffer for
instance uniforms defaults to 65536 and `project.godot` sets no override. C1 fits under the
default; C4/C5 have more mesh instances and exhaust it — and instances that fail allocation
**lose their node_bias**, so their decals can z-fight again (correctness, not just noise).

**Approach:** set `rendering/limits/global_shader_variables/buffer_size` in `project.godot`
comfortably above the biggest chapter's instance count (measure C4/C5 instance counts from the
build log, then size with headroom). If some chapter still exceeded a sane setting, fallback
plan: bake the node bias into per-surface material data instead of an instance uniform (costs
material-cache sharing, so only if needed).

**Also investigate here (observed during the repro):** C5's city ground renders very blurry.
The per-chapter `rtexture2/4/6/8/14.zip` archives (and top-level `rimage.zip`) are extracted
but **never loaded** by the viewer — check whether they hold higher-resolution replacements
the original uses (the `r*` texture system), and if so, wire them into TextureArchive's
lookup chain.

**Verify:** C4 + C5 `--fly` runs log-clean; A/B screenshot of a C5 decal area (unchanged or
improved layering); if rtexture gets wired: C5 ground sharpness A/B vs the original.

**DONE (2026-07-17):** Root cause confirmed exactly — Godot allocates `MAX_INSTANCE_UNIFORM_INDICES`
(16 vec4 slots) per instance that carries any instance uniform, so the default 65536-slot
global-shader-variable buffer caps at ~4096 mesh instances. Measured counts: C1 3481 (fit, why
it was clean), **C4 4234**, **C5 4746** — both overflow, giving 143 (C4) / 600 (C5) "Too many
instances" errors AND dropping `node_bias` on the overflow. Set
`rendering/limits/global_shader_variables/buffer_size=262144` in `project.godot` (~16384
instance slots, a 4 MB GPU buffer — headroom for the largest chapter plus item-7 border tiles).
Re-verified: C4/C5 `--fly` now log **zero** instance-uniform errors (0 "Too many instances",
0 total ERROR lines); C1 regression clean; C4/C5 worlds render with decal layering intact
(NY skyline/bridge/piers, RM terrain — screenshots).

*rtexture/rimage investigation (negative — nothing to wire):* the per-chapter
`rtexture2/4/6/8/14.zip` are **downscaled quality tiers** of the same 896 world textures, not
hi-res replacements — measured across all 896 C5 textures: base `texture` == `rtexture14`
(both max-res, e.g. cblock 256²), while `rtexture2` = ¼ (64²) and `rtexture4/6/8` = ½ (128²);
**zero** rtexture file exceeds its base. `rimage.zip` is the UI/HUD image set (crosshairs,
buttons, cursor, menu splash, briefing thumbs), no world textures. So the viewer already loads
the max-res set. The reported C5 "blurry city ground" is a **grazing-angle mipmap** artifact,
not source resolution (proof: the same C1 terrain is sharp viewed straight down, blurry at a
grazing angle). Fix without new assets: the SceneBuilder world shader now samples albedo with
`filter_linear_mipmap_anisotropic` and `project.godot` sets
`textures/default_filters/anisotropic_filtering_level=4` (16×). Before/after A/B on the exact
C5 Manhattan street grid: isotropic smears the receding grid to gray mush mid-distance,
anisotropic keeps the street lines crisp and legible much further out; C1 grass shows a
subtler-but-real gain. (Look change — pending user's fidelity sign-off; trivially reverted.)

## 3. C4 white fog — FOG_COLOR integer-RGB schema

**Goal:** Rocky Mountains fog renders its data color (192 gray), not blown-out white with a
hard horizon cut (`c4-fly.png` repro matches the user report exactly).

**Evidence:** weather.json `FOG_COLOR` has **two encodings**: C1's is a normalized float triple
(`0.69`), C4's is integer RGB (`[192, 192, 192]` — likewise C4's `CLOUD_COVER`
`TOP_COLOR`/`BOTTOM_COLOR`, C5's `[220,220,220]`, C1B/C3's `[32,56,72]`). `Zrdr.cs:44`
converts every JSON number via `GetSingle()`, so 192 arrives as float 192.0; `Weather.cs:116`
builds `Color(192,192,192)` — saturated white — and PlaneViewer's sRGB→linear conversion
(which assumes 0..1) blows it out further.

**Approach:** normalize in `Weather.cs`: any color triple with a component > 1 is /255. Parse
`TOP_COLOR`/`BOTTOM_COLOR` while in there (feeds item 6). Document the dual encoding in
`docs/formats/weather.md`. Then re-check C4: the "hard cut off" is expected to be the blown
color's doing; if a hard edge survives the fix, investigate C4's `FOG_ALTITUDE` 10000→11000
semantics (full fog at every flyable altitude — a pure cylinder with no vertical fade).

**Verify:** C4 A/B screenshot (fog converges to 192-gray, soft distance ramp); C1 regression
(0.69-float path renders byte-identical); user in-game check vs the original's RM haze.

**DONE (2026-07-18):** `Weather.ParseColor` normalizes every weather.json colour triple —
divide by 255 iff any component is strictly > 1 — proven unambiguous by scanning all
weather.json (only `1.0`-bearing colour is a float sky-fog `[0.80,0.84,1.0]`, max exactly 1
→ stays float; no integer colour is all-{0,1}). FOG_COLOR now flows through it; C4's
`[192,192,192]` → 0.753 sRGB → PlaneViewer's existing sRGB→linear, unchanged. Verified by
`--chapter --sky-zone --campos` shots: **C4** fully-fogged region measures exactly
`(192,192,192)` with a soft mountain→fog ramp (was blown white with a hard cut); **C1**
zone1 fog measures `(176,176,176)` = 0.69·255, byte-identical (the float path is untouched
by construction — `≤1` triples aren't scaled; log still prints `fog 0.69 gray`). The C4
"hard horizon cut" was entirely the blown colour — no FOG_ALTITUDE follow-up needed (its
`[10000,11000]` is a correct pure-cylinder full-fog-at-all-altitudes for RM). Also decoded +
parsed `CLOUD_COVER`'s `TOP_COLOR`/`BOTTOM_COLOR` (integer RGB, into
`CloudTopColor`/`CloudBottomColor`, unused this milestone — feeds item 6). New
`docs/formats/weather.md` seeds the public reference (schema + the dual-encoding rule); item
13 grows it. User in-game A/B vs the original RM haze still pending playtest.

## 4. Clouds render through fog

**Goal:** Distant cloud sprites and ambient puffs fade into the fog like the terrain they
float over — today they punch through as crisp white shapes against the fog wall.

**Evidence:** the two cloud renderers are the only world geometry without the cylindrical fog
term: SceneBuilder's billboard path deliberately keeps `StandardMaterial3D` (no generated
shader → no `csky_fog_*`), and `CloudPuffs.cs:76` declares `fog_disabled` with no custom fog
in its shader. The `cloudlayer` deck (regular SceneBuilder surface) fogs correctly.

**Approach:** give both the same fog treatment as SceneBuilder's world shader (horizontal
distance + altitude fade from the `csky_fog_*` globals): (a) replace the cloud-sprite
StandardMaterial3D with a billboard ShaderMaterial variant (spatial shader with the hand-rolled
keep-scale billboard, like CloudPuffs'), fed through the same material cache; (b) add the term
to CloudPuffs' shader (mix ALBEDO toward fog color; keep its alpha shaping). The moon/skydome
billboards are untouched (dome fog behavior is by design since the 2026-07-17 remodel).

**Verify:** `--campos` shots near the fog wall: cloud sprites fade in step with adjacent
terrain; puffs at shell edge fog correctly while near puffs are unchanged; C1 zone2 + C4.

**DONE (2026-07-18):** Both cloud renderers now carry SceneBuilder's cylindrical fog term
(identical formula + `csky_fog_*` globals). (a) The cloud-sprite billboard path was
`StandardMaterial3D` (no generated shader ⇒ fog-immune); replaced with a billboard
**`ShaderMaterial`** — same hand-rolled keep-scale billboard as CloudPuffs, `COLOR × albedo`,
`blend_mix`/`depth_draw_never` for the soft-alpha clouds, and the fog `mix` — cached per
blend/scissor in a new `_billboardShaderCache`. It carries **no** `csky_fog_on` instance
uniform (clouds always fog, and omitting it keeps the sprites off the item-2 instance-uniform
buffer). (b) `CloudPuffs`' shader got the three fog globals + the same `mix` (its `fog_disabled`
render mode stays — that only turns off Godot's *built-in* fog; ours is custom). **Verified**
via deterministic stash-based A/B at a camera above the puff layer (so the random-seeded near
puffs don't confound), C1/IA1 zone2 + C4/IA1 zone2, pixel-measured by distance band:

- **C1** far sprites (≳2 km) `196.6→176.0` gray = *exactly* FOG_COLOR (0.69·255), crisp-white
  px `35%→0%` — fully absorbed into the fog wall; mid `204→178`; **near** sprites stay bright
  `208→195` (24 % still white). Smooth near-bright→far-fog gradient matching the terrain.
- **C4** far sprites `→192.0` = *exactly* its FOG_COLOR (192), `0%` white; near `→200` (20 %
  white). Same mechanism, C4's colour.
- Both shaders compile + render with **zero** errors (`--fly` C1 smoke clean: spawn/world/2670
  colliders intact). No-weather `--chapter` (fog globals at their no-op default) renders clouds
  **crisp** (max 239, 27 % white) — the billboard shader doesn't fog when weather is absent.

*Honest scope note:* CloudPuffs' **visible** fog is negligible in the flyable zones — the 620 m
puff shell sits almost entirely inside the fog's clear near-range (`FOG_RANGES.x`/2 = 500 m in
both C1/C4 zone2), so shell-edge puffs fog only ~2 % and near puffs are unchanged (measured
`203.4→203.1` same-camera). It is a correct, minimal *consistency* fix (removes the fog
immunity; future-proof if fog ranges tighten). The dominant "crisp white shapes against the fog
wall" were always the world **sprites**, now fixed. Moon/skydome untouched (dome fog is by
design). User in-flight A/B still nice-to-have.

## 5. Precipitation — rain and snow

**Goal:** Missions whose weather defines precipitation show it: Rocky Mountains IA1's falling
weather (user-observed; the data says SNOW), Sea Haven night-rain variant C1C and Hollywood
C2B (RAIN).

**Evidence:** weather.json ends in a precipitation block (raw key/value pairs after
`SHADOW_ANGLES`, same bare-scalar style as CLOUD_COVER): C4/IA1 + C4/M01 —
`TYPE SNOW, COLOR [128,128,128], WIND_DIR 0.0, WIND_VEL 0.8, GRAVITY 1.0,
ALPHA_GRADIENT [0.5, 0.0]`; C1C/IA1 + C2B/IA1 — `TYPE RAIN, PARTICLES 100, …` (remaining RAIN
params to decode during implementation). C1/C5 IA1 have no block. Note the user remembers
"rain" in RM while the data says SNOW — at speed, gray streaking flakes may well read as rain;
the in-game look decides nothing here, the data drives it and the A/B confirms.

**Approach:** extend `WeatherState` with the TYPE block (both schemas; document in
`docs/formats/weather.md`). Render as one camera-following MultiMesh field (CloudPuffs
pattern: fixed pool recycling through a box around the camera, world-anchored fall so the
plane flies through it): SNOW = small COLOR-tinted flakes falling under GRAVITY, drifting per
WIND_DIR/WIND_VEL, alpha per ALPHA_GRADIENT; RAIN = velocity-stretched streak quads, density
scaled by PARTICLES. Feel constants TUNE.

**Verify:** C4 IA1 fly shows falling snow matching the original (user A/B); C1C IA1 shows
rain; C1 IA1 unchanged (no block → no field); one MultiMesh, no measurable frame cost.

## 6. Night brightness calibration — deck + sky

**Goal:** The night scene reads as dark as the original. User-confirmed (grill 2026-07-17):
one complaint, not two — deck, sky, the whole night mood is too bright/washed-out vs the
original ("clouddeck to bright" + the older "color grading for the skybox" note).

**Evidence / leads (in likelihood order):**
- Measured already (CLAUDE.md, 2026-07-17 fog entry): our deck renders ~206 gray where the
  original's overcast reads ~165–175.
- `CLOUD_COVER` carries `TOP_COLOR`/`BOTTOM_COLOR` in many missions (C1B/C3 night-blue
  `[32,56,72]`, C4 `[192]³`, C5 `[220]³`) — plausibly the deck's face tints. C1/IA1 has
  **no** such pair, so C1's deck needs either an engine-default discovery or a measured TUNE.
- Each weather zone defines `SUNLIGHT_DIFFUSE`/`SUNLIGHT_AMBIENT` + colors that we ignore for
  the fullbright world — a candidate global darkening the original applies.
- Pipeline audit: DX7 wrote texel values straight to the sRGB framebuffer; check our
  viewport/WorldEnvironment tonemap is truly pass-through (Linear, no exposure/adjustment) —
  any non-identity tonemap brightens everything uniformly.

**Approach:** (1) user captures matched original screenshots (deck underside, sky gradient,
lit terrain — same spots as our `--campos` shots); (2) test the leads in order against the
measurements: TOP/BOTTOM_COLOR as deck tint on missions that have it, zone sunlight as a
world/deck multiplier, tonemap audit; (3) land whichever mechanism matches, and TUNE C1's
data-less deck to the ~165–175 target. Prefer data-driven over a hand global grade.

**Verify:** pixel-measured deck tone within ~10 units of the original's; side-by-side night
sky screenshots; a day-zone chapter (C1B zone1 or C4) unharmed.

## 7. Map edge continuation

**Goal:** Flying past the world's edge shows the map continuing — terrain keeps going over
terrain edges, sea over sea — instead of our current void. User-verified in the original
(grill 2026-07-17): they crossed the boundary and content continued seamlessly, type-matched
to the local edge.

**Evidence:** the original's own view envelope (far clip ~4.5–4.8 km, fog saturating before
it) means whatever it renders out there only ever shows fogged — consistent with a cheap
border extension, not real extra world (no such geometry exists in the gamez; the tile grid
ends at the `area` bounds, C1 x,z ∈ [-12288, 0]). Our 40 km far plane and altitude-faded fog
can expose the raw edge where the original never could.

**Approach:** replicate border extension in WorldBuilder: clone each outermost border tile
outward N rings (N sized to cover the fog-saturation distance at the flight ceiling),
**mirrored** across the boundary so edge heights match exactly (straight repetition would
step; mirroring is seam-free by construction — visual repetition is invisible under fog).
Corner tiles mirror both axes. Clones share meshes/materials (cheap), grow no clutter, and
get colliders in flight (consistent crash behavior — the visible ground should never be
fly-through). Skipped in plain static orbit viewing to keep the data view honest.

**Verify:** fly past a terrain edge and a sea edge in C1 at low + high altitude: continuous
ground to the fog in every direction, no void, no cliff seams; crash on extended terrain
works; landlocked C4 edges continue as mountains.

## 8. Mission states — anim-state engine part 1 (+ destroyed-variant flicker)

**Goal:** Each mission/scenario shows the world state the original shows: the right zeppelins
present, hangars open or closed, trains only where active — and the destructible buildings'
`destroyed` variants no longer render on top of the healthy ones (the oil-tank/hangar
flickering at the C1 airfield; same mechanism).

**Evidence (all decoded 2026-07-17):**
- Per-mission `zepstate.json` = ANIMATION_DEFINITIONs (`ACTIVATION ON_STARTUP`) setting base
  `OBJECT_ACTIVE_STATE`s — C1/IA1 deactivates `dliner1`, `cargotrain`, ….
- Per-mission `startanims.json` `NEW_GAME_START` lists anims to run at mission start — C1/IA1:
  `player_setup`, `train_on_track`, `pure_panic`, `hangar3_doors`, `mp1_rearm_off`,
  `mp_hangar3_open`. Definitions live across the mission's `mis_anim.json` (9 defs in C1/IA1,
  including an `ANIMATION_PATH`) and shared readers (`anim.json`, `instantaction.json`,
  `small_building.json`/`medium_building.json`, `generator_doors.json`, …).
- C1 gamez holds 66 bare `destroyed` subtrees plus named ones (`ref_tank_dest`,
  `rtwr_destroyed`, `litehsdestroyed`, 8× `destroyed_balloon`) — we build healthy + destroyed
  together, hence the coplanar flicker. Their proper initial states are exactly what the
  building/vehicle anims' RESET_STATEs encode (the planes' damage panels already work this way).
- Scenario dimension: `ia.json` carries per-mission scenario config (`disallow_missions`);
  how scenario selects state (e.g. the zeppelin in `zeppelin_run`) is part of this item's
  analysis — candidate carriers: `zeppelins.json`, `egen.json`, `instantaction.json`.

**Approach:** a generic ANIMATION_DEFINITION loader (NAME / ANIMATION_NAME / ACTIVATION /
RESET_TIME / SEQUENCE_DEFINITION with OBJECT_ACTIVE_STATE & friends — schema documented as
`docs/formats/anim-definitions.md`) + a `MissionState` applier that, at world build:
(1) applies RESET_STATE / ON_STARTUP base states from the shared + mission readers (hides
`destroyed` variants and phantom zeps/trains); (2) applies the end-state of each
NEW_GAME_START anim (doors whose anim is a timed motion get their final pose in part 1 —
motion itself is item 9). Safety net: any `destroyed`-named subtree that no anim covers gets a
name-based hide + log line. Analyze + document the scenario→state path as part of this item.

**Verify:** C1 IA1: oil tanks/hangars stop flickering (A/B screenshots); zeppelin/train
presence matches the original per scenario (user in-game check); nothing legitimate vanishes
on a full fly-over; all chapters still build clean.

## 9. Animated vehicles — anim-state engine part 2

**Goal:** The trains (and any animated road vehicles) actually move along their tracks with
steam puffing, as in the original.

**Evidence:** C1 gamez has `cargotrain`, `caboose`, `steamcloud`, `train_lights`,
`stude_truck`, `firetruck1–6`, `fuel_truck01/02` nodes; C1/IA1's `mis_anim.json` contains an
`ANIMATION_PATH` definition and `startanims.json` runs `train_on_track` at mission start;
`zepstate.json` keeps `cargotrain` INACTIVE by default (so the train exists exactly when a
mission activates it — meshing with item 8). Steam: the `steamcloud` node + the existing
Puffer machinery (`pufftrails.json`-style PUFFER_STATE readers).

**Approach:** extend the item-8 engine with timeline playback for the motion primitives these
anims actually use: `ANIMATION_PATH` (waypoint-following translation + heading),
`OBJECT_MOTION_FROM_TO` (timed translate/rotate — also upgrades item 8's hangar doors from
posed to opening), looping per RESET_TIME/sequence data. Attach a steam Puffer at the
`steamcloud` node while its train runs (locate the emitter def wired to it; the anim's
sequence events are the first place to look). Survey which missions animate road vehicles at
all (search ANIMATION_PATH/OBJECT_MOTION defs referencing truck nodes) — C1/IA1 may only
drive the train; implement what the data actually defines, no invented traffic.

**Verify:** C1 IA1 side-by-side with the original: train circulates its track with steam;
debug pause (P) freezes it with everything else; frame cost negligible; missions without
vehicle anims are unchanged.

## 10. Collision damage model

**Goal (grill-agreed scope):** the full collision-driven damage system — weapons stay out,
flight-handling penalties stay out (user researches the original's behavior first — see
footer). Four staged sub-items, each independently verifiable:

**Evidence (all confirmed in data/code):**
- `vehicle.json` `pbloodhawk` `destroyable_parts` (root[852-block]): parts
  `nose`/`tail`/`leftwing`/`rightwing`, HP 20/20 each, all `critical`, tail additionally
  `engine`; per part a `got_hit_anim` (`nose_got_hit` …) and `injure_anims` — health-fraction
  → anim: `*_damage_green/yellow/red` at 0.72/0.46/0.20 (the cockpit indicator's texture
  cycle) and `pdpanelN` panel flips (leftwing: `pdpanel5`@0.5, `pdpanel4`@0.3, `pdpanel3`@0.15;
  rightwing: `pdpanel6`@0.4, `pdpanel1`@0.3, `pdpanel2`@0.15; nose `pdpanel7`@0.15, tail
  `pdpanel8`@0.15). AI variants carry the same shape (25/20 HP). The `pdp1–8` panels and their
  `_h` healthy twins are already classified in PlaneBuilder; `player-1.json` holds the
  `pdpanelN` anims.
- `PlaneCollider` already reports which airframe part hit (fuselage/wing/canard/tail).
- Every plane has a `destroyed` subtree (currently skipped at build); `fire.json` defines the
  sustained-fire PUFFER_STATE; `player_plane_destruct.json` holds the full crash choreography
  (deferred — see footer).
- Collider fit complaint (user): the Bloodhawk "tail" box is 11.6 m wide × mostly empty — the
  geometric classifier AABBs the full aft span, bridging the outboard fins with air.

**10a — Collider fit.** Split bilateral clusters: inside the tail (and wing, where relevant)
regions, detect left/right vertex clusters separated by a spanwise gap (same technique as the
existing fore/aft chord-gap split) and emit per-cluster boxes (twin fins → two slim boxes,
Bloodhawk tail stops being a barn door). Cap total boxes ~6–8 per plane; thresholds TUNE.
*Verify:* logged box dimensions shrink accordingly on Bloodhawk/Kestrel; `--debug-collision`
wireframes hug the fins; flying between the Bloodhawk's fins over an obstacle no longer
false-crashes; the item-8 wingtip-catch regression still crashes.

**10b — Part HP + collision severity.** A `PlaneDamage` state (per-part HP from
`destroyable_parts` through the `kind_of` chain). FlightController's collision response
becomes severity-based: impact speed along the contact normal (+ struck part) decides —
below a crash threshold: subtract part HP (scaled by severity), kill some speed, small
attitude kick, fly on; above: crash as today; a `critical` part at 0 HP: crash regardless
(that's the data's meaning — matches the user's "sometimes only damages the plane").
Thresholds TUNE against the original's forgiveness (user playtests tree-clips and building
grazes). *Verify:* scripted low-speed tree graze survives with logged part damage + speed
loss; the same geometry at speed crashes; part HP reaching 0 crashes; respawn resets HP.

**10c — Visible damage.** Wire the data's thresholds: as a part's HP fraction crosses each
`injure_anims` entry, flip the named `pdpanelN` (activate `pdpN`, hide the `_h` twin — the
mapping lives in `player-1.json`'s anims) and at 0.4-ish attach the smoke-trail puffer the
data names (`pfsmoketrail`). The green/yellow/red indicator anims are cockpit-side and stay
unwired until a cockpit exists (note in docs). *Verify:* A/B screenshots — a grazed wing
shows its torn-skin panel at the right HP fraction; smoke trails at low HP; respawn pristine.

**10d — Crash breakup + fire/smoke.** On crash: hide the healthy model, spawn the `destroyed`
subtree's parts as free bodies with impact-derived velocities (hand-simulated ballistic +
ground-rest, like the puffers — deterministic and cheap), let them rest on the terrain;
sustained fire + black smoke Puffers at the wreck (~10 s, `fire.json` data) on top of today's
fireball + explosion sound. Parts persist until respawn. *Verify:* scripted crash — plane
visibly breaks apart, parts tumble and lie on the ground, wreck burns; auto-respawn cleans
everything; water crash acceptable interim (surface variants deferred).

## 11. Dive sound — tune down

**Goal:** The overspeed dive is audibly subtler, matching the original (grill-confirmed
direction: **ours is too loud/prominent**).

**Evidence:** our whine follows the data curves (`prop_sound`: volume 0→0.5 over 1.0–1.1×
fd_speed, pitch 0.65→1.25) but the WAV is our guess (`snd_enginewhine` — the readers never
name it) and the rattle (`snd_planeshake`, 0→1 over 1.0–1.2×) stacks on top.

**Approach:** lower the whine's mix gain (TUNE scalar on top of the data curve), A/B the
rattle balance in the same pass; if the character still reads wrong at matched loudness,
revisit the WAV choice against the archive. **Verify:** user A/B — same full-throttle dive in
both games.

## 12. Turn rates — per-axis calibration

**Goal:** Roll (and pitch/yaw) rates match the original, with documented per-axis constants
instead of the current hardcoded ×2-on-everything.

**Evidence:** `FlightModel.cs:85-90` — `rotationTune = 2.0f` multiplies pitch, yaw, AND roll
torque (user commit `ddcc57f`, from the "rolling is a lot faster in original" observation).
CLAUDE.md still documents the unscaled rates (Bloodhawk roll ≈ 1.65 rad/s — now ×2 in
practice), and the doubled pitch authority silently interacts with the stall/elevator balance
tuned in Run 1.

**Approach:** split into `PitchTune`/`YawTune`/`RollTune` TUNE constants. **User task:**
measure the original at matched conditions (Bloodhawk, cruise speed): time a full 360° roll,
a sustained full-pitch loop or turn, and eyeball rudder yaw; we time ours via scripted
`--hold` runs and set each constant to match. Re-check the stall behaviors (item-6 Run 1)
after the pitch factor settles. Update the CLAUDE.md dynamics bullet.

**Verify:** scripted full-roll time within ~10 % of the measured original; stall-cap and
knife-edge regressions unchanged; CLAUDE.md consistent with the code.

## 13. `docs/formats/` — public reader-format reference

**Goal:** The XWVM-model deliverable — format documentation — becomes a real, public artifact:
one page per reader family under `docs/formats/`, seeded now and grown with every future
decode (per the charter: code + format docs ship, game data never does — pages carry field
tables and small excerpt values, no bulk data).

**Approach:** create `docs/formats/README.md` (index + shared conventions: alternating
key/list dicts, bare-scalar blocks like CLOUD_COVER/WIND/precipitation, the float-vs-int
color encodings, `kind_of` inheritance) and seed pages alongside this run's items:
`weather.md` (zones/fog/cloud band/wind/precipitation — items 3/5/6), `anim-definitions.md`
(ANIMATION_DEFINITION schema: activation, reset/sequence, active-state, path/motion — items
8/9), `vehicle.md` (dynamics + `destroyable_parts`/`injure_anims` — item 10), plus migration
pages for what CLAUDE.md already knows: `spawns.md` (ia/objectives), `sounds.md` (SETS +
player curves), `clutter.md` (interp templates). Rule going forward: new decodes land with
their docs page in the same change.

*Progress note (2026-07-18): the CLAUDE.md token-diet split seeded `docs/formats/` early with
three coarse migration pages — `gamez.md`, `world-structure.md`, `zrdr.md` (moved verbatim
from CLAUDE.md's format sections). Item 13 still owes the README.md index/conventions page,
the new-decode pages (weather/anim-definitions/vehicle), and optionally splitting the coarse
zrdr page into the per-family pages listed above.*

**Verify:** pages exist and cross-link; a fresh reader can implement a weather parser from
`weather.md` alone; no game-asset bulk data anywhere in `docs/`.

---

*Scope decisions from the 2026-07-17 grilling session:*
- *DMG model: full collision-damage system this run (weapons out). Flight-handling penalties
  deferred — **user research task:** determine in the original whether part damage degrades
  handling/power before a critical part dies, or stays cosmetic until the explosion.*
- *Crash: breakup + fire/smoke now; the complete `player_crash_default/_dirt/_water`
  choreography (surface variants, sparks, `plane_destroy_sg`) stays on the backlog explicitly.*
- *Mission anims: both halves — states (pt 1) and vehicle motion (pt 2).*
- *Map edge: user crossed the original's boundary — terrain continues over terrain, sea over
  sea → border-extension approach.*
- *"Clouddeck too bright" and "skybox color grading" are one complaint: the night scene is
  too bright overall → single calibration item (6).*
- *Dive sound: ours is too loud vs the original → tune down.*
- *Turn rates: calibrate per-axis against user measurements (360° roll etc.), replacing the
  global ×2.*
- *Docs: `docs/formats/` seeded now, one page per family, new decodes documented as they land.*
- *Order: as the checklist above; quick unblockers first, calibration items interleaved with
  user playtest availability.*
