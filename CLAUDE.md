# CLAUDE.md

## ⚠ Keep this file up to date

**This file is authoritative project context for Claude.** Whenever you make a non-trivial change to this project — new source modules, new CLI flags, changed defaults, new algorithms, new output fields, renamed entry points, structural refactors — **update this file in the same turn as the code change**. A stale CLAUDE.md misleads future sessions and wastes the user's time.

Update rules:
- Code changes that alter *what the tool does from the outside* (flags, outputs, algorithms, ui) → update the relevant section here.
- Bug fixes that correct a documented invariant → update the invariant.
- Pure refactors with no external effect → usually no update needed.
- When in doubt, update.

## Project Description

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive, Microsoft): a modern engine that plays the original game using the player's own legally-owned game files. Nothing like this exists yet for Crimson Skies — this is a first-of-its-kind effort.

Charter decided 2026-07-14 (full detail in Claude's project memory):

- **Milestone 1** — complete asset extraction: fill the Crimson Skies gaps in mech3ax (`planes.zbd`, `gamez.zbd`).
- **Milestone 2** — vertical slice: free flight only. One plane, one map (candidate: C1 instant-action arena), arcade controls, original sounds. No AI, objectives, or damage.
- Long-term direction (not commitment): full campaign remake.

## 🚫 Hard rule: no game assets in version control — ever

Public open-source project under the **XWVM legal model**: the repo ships **code and format documentation only**. Game files, extracted assets, ZBD contents, screenshots of hexdumps containing bulk asset data — none of it gets committed or published. The importer reads the player's own install at runtime. `.gitignore` enforces this for `CrimsonSkiesGame/`, `extracted/`, and `tools/`; keep it that way when adding directories.

## Architecture & key decisions

- **Engine:** Godot 4 .NET (C#). Consumes extraction output via mech3ax / Mech3DotNet (strongly-typed C# wrapper).
- **Format reverse engineering:** happens in a fork of [mech3ax](https://github.com/TerranMechworks/mech3ax) (Rust), PR'd upstream to TerranMechworks. Their byte-identical round-trip test harness (extract→repack) is the correctness standard.
- **Flight model:** data-driven approximation — parameterized by plane stats from extracted zrdr reader files, hand-tuned against the original game. No exe decompilation.
- **Division of labor:** Claude writes the Rust parsers, Godot code, and docs; the user reviews, playtests flight feel, and owns upstream/community communication.

## Repo layout

- `CrimsonSkies/` — the Godot 4 .NET project (the actual remake; committed). See "Godot project" below.
- `CrimsonSkiesGame/` — the user's retail game install (git-ignored). ZBD archives in `CrimsonSkiesGame/ZBD/` organized as campaign chapters `C1`–`C5`, each with instant action (`IA1`), story missions (`M0x`), multiplayer maps (`MP1`–`3`). Cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored). Notably `planes-gamez.zip` (unzbd of planes.zbd) and `c1-texture.zip` (unzbd of C1/texture.zbd), which the viewer reads.
- `tools/` — downloaded binaries (git-ignored): mech3ax v0.6.1, Godot 4.7 .NET editor at `tools/godot/Godot_v4.7-stable_mono_win64/` (`*_console.exe` for CLI use).

## Godot project (`CrimsonSkies/`)

Godot 4.7 .NET, C# / net8.0. Build & run:

```
dotnet build CrimsonSkies/CrimsonSkies.sln
tools/godot/.../Godot_v4.7-stable_mono_win64_console.exe --path CrimsonSkies res://scenes/Main.tscn -- --plane=player_bhawk
```

(First time only: run with `--headless --import` once before running scenes.)

- `src/Mech3/GameZ.cs` — loads a mech3ax GameZ extraction (ZIP or unpacked dir): nodes.json / meshes.json / materials.json into plain C# objects. Parses World partition refs and per-corner vertex colors.
- `src/Mech3/TextureArchive.cs` — texture lookup over an unzbd texture ZIP (PNGs), handles the two name quirks below.
- `src/Mech3/SceneBuilder.cs` — shared GameZ-subtree → Node3D/MeshInstance3D builder: n-gon triangulation, material+mesh caches, keeps only nearest LOD, takes a skip predicate. `fullbright: true` renders unshaded (texture × baked vertex color — the original engine's world look). `generateCollision: true` attaches a static trimesh collider (`StaticBody3D` + `ConcavePolygonShape3D`, cached per mesh, `BackfaceCollision = true` because the source winding is inconsistent) to each mesh; a `collisionSkip` predicate exempts subtrees (renders but not solid).
- `src/Mech3/PlaneBuilder.cs` — thin wrapper: builds one aircraft, skips cockpit/destroyed/damage/shadow/prop-animation subtrees. Shaded (dynamic lighting).
- `src/Mech3/WorldBuilder.cs` — builds a whole chapter world (fullbright): World children + partition-referenced subtrees; skips `horizon` (17 km skydome — TODO render as real sky), `fvol*` (flight-boundary volumes), `dzpaths` (path ribbons). `collision: true` (passed only when flying) generates static colliders for solid geometry but exempts cloud/sky by **texture** (`cloud*`/`sky*` — node names are unreliable; cloud layers appear under generic names like `g27517`).
- `src/Mech3/Zrdr.cs` — zrdr extraction reader (`Zrdr.LoadFile` from zip or dir + `ZrdrDict` view over the alternating key/[values…] reader lists).
- `src/Mech3/WavFile.cs` — pure-C# WAV parser (no Godot deps) with MS ADPCM→PCM16 decoder; the game's WAVs are MS ADPCM (fmt 2, 4-bit, 22 kHz), which Godot can't load natively. Decoder verified byte-identical against an independent Python implementation.
- `src/Mech3/SoundArchive.cs` — WAV lookup over a soundsh/soundsl extraction (zip or dir) → cached Godot `AudioStreamWav` (forward loop when the sound def says LOOPED).
- `src/Mech3/SoundDefs.cs` — sounds.json SETS parser: `snd_*` name → `SoundDef` (wav, LOOPED/3D/FREQUENCY flags, RANGE, VOLUME).
- `src/Flight/PlaneStats.cs` — typed flight stats for one player plane from vehicle.json (`dynamics` block through the `kind_of` chain) + engines.json (stock engine power) + player.json globals, plus sound: the plane's `engine_sound` def name and the player.json volume/pitch `SoundCurve`s (clamped two-point ramps).
- `src/Flight/FlightAudio.cs` — own-plane sound loops (non-positional): per-plane engine WAV with throttle-driven pitch 0.6→1.0, overspeed whine (`prop_sound` curves, silent below fd_speed), airframe rattle (`rattle` block, volume 0→1 over 1.0–1.2× fd_speed). Driven per-frame from FlightController (not while crashed). `OnCrash()` kills the loops and fires one of the game's plane-explosion one-shots (`snd_exp_plane1..4`, random pick); the engine loop auto-restarts on the next `Update` after respawn.
- `src/Flight/FlightModel.cs` — arcade flight dynamics: body rates from control torque × reciprocal inertia vs `ang_momentum_damp`; velocity chases the nose (alignment lag + low-speed gravity sag); thrust vs quadratic drag with equilibrium at `fd_speed`; stall nose-drop below 0.3·fd_speed. Hand-tuned scale constants marked TUNE.
- `src/Flight/FlightController.cs` — flying-aircraft node: keyboard + gamepad → FlightModel → transform, smoothed chase camera, text HUD (mph/ft/throttle), telemetry print 1/s. Ground/object collision: each physics frame casts a ray along the frame's flight path (prev→next, extended one airframe-length past the nose) against the world's static colliders; a hit crashes the plane — explosion one-shot, airframe hidden, frozen at the impact point with a "CRASHED — PRESS R" HUD banner until R (or gamepad Y/A) respawns (`--hold` runs auto-respawn after 1.5 s so scripted flights keep going). `DebugCollision` (from `--debug-collision`) draws the swept collision ray + a cross at the probe tip (green; freezes red at the impact). Backstop respawn when under the map (y < 60) if the swept ray ever misses. Gamepad (first connected joypad): left stick pitch/roll with 0.15 deadzone + squared response, LB/RB rudder, RT/LT throttle up/down, Y respawn.
- `src/PlaneViewer.cs` — Main.tscn root script: orbit camera, lighting. User args (after `--`): `--plane=`, `--world[=world1]` (render a chapter world; default gamez becomes c1-gamez.zip), `--fly` (free flight: C1 world + plane + arcade controls — WASD/arrows pitch+roll, Q/E rudder, Shift/Ctrl throttle, R respawn, Esc quit; or gamepad), `--gamez=`, `--textures=`, `--zrdr=` (default extracted/zrdr.zip), `--sounds=` (default extracted/soundsh.zip), `--mute` (skip flight audio), `--debug-collision` (draw the plane's collision probe), `--hold=p,r,y,thr` (constant flight input for automated runs), `--frames=N` (delay before screenshot), `--yaw=`, `--pitch=`, `--campos=x,y,z` / `--lookat=x,y,z` (manual camera placement), `--screenshot=<path>` (render a few frames, save PNG, quit — used for automated visual verification).

### GameZ format facts (validated on this install, planes.zbd)

- `nodes.json` `children`/`parent` are **flat list positions**, NOT the `node_index` field (node_index has duplicates).
- Euler `transformation.rotation` composes **R = Ry(y)·Rx(x)·Rz(z)** = Godot's `EulerOrder.Yxz` (fit numerically, zero error, against the 221 nodes that also carry a matrix). When `matrix` is present, use it instead; it is stored transposed — real columns are (a,b,c),(d,e,f),(g,h,i).
- Coordinates are right-handed Y-up with the nose at **-Z** — Godot's frame exactly; no mirroring, no UV V-flip.
- `meshes.json` has `null` entries (empty slots) — keep them to preserve `mesh_index` alignment.
- Polygons are n-gons (3..35 verts): triangulate as fan, or as strip when `flags.triangle_strip`; `normal_indices`/`uv_coords` may be null (272 polys have no normals → flat-shade fallback).
- Materials are `Colored` (RGB 0-255 + alpha) or `Textured` (texture referenced **by name**). Two name quirks: fixed-width 20-char truncation (`blo_fusalagebottom.t` → prefix-match) and mech3ax duplicate renames (`bldhwk_cowling.-12.tif` → strip `.-N` suffix).
- Plane skin pixel data is NOT in planes.zbd — it's in each chapter's `texture.zbd` (C1's contains all player-plane skins).
- Aircraft tree shape: `player_*` → `geometry` → `healthy` → LOD nodes (`nearest` = range.min 0 is highest detail) + `markers` (firepoints/pylons/camera), plus `cockpit1` (separate interior model), `destroyed`, `shadow`, `dontmove` (props: `staticprop1` static; `prop1`/`prop1b`/`prop2*`/`nitroprop1` are spin-animation frames).
- Polygons carry per-corner `vertex_colors` (RGB 0-255, ~30% non-white) = **baked lighting**. The original engine renders world geometry fullbright: texture × vertex color, no dynamic lights. Rendering the world shaded instead comes out murky-dark — use unshaded + vertex colors.
- Node kinds beyond Object3d/Lod: World, Display, Window, Camera, Light. Display has no `name`/`children`; Window/Camera/Light have no `children` — parse tolerantly.

### World structure (chapter gamez.zbd, validated on C1)

- World content lives in TWO places: the `world1` World node's `children` (66 in C1: horizon, cloud groups, zeppelins, trains, fvol volumes…) **and** ~350 top-level parentless subtrees referenced only via the World's `partitions` (12×12 spatial grid over `area` x,z ∈ [-12288, 0]; each cell lists node indices; the union of distinct refs = placed terrain tiles + buildings + vehicles).
- The remaining ~160 parentless roots (bulletholes, firetrails, muzzle flashes, projectiles…) are runtime-spawned effect prototypes — not world scenery.
- Terrain = ~1 km tiles (`terpat*`/`water1` textures), no transform (verts already in world space), under `Lod` nodes (range 0–2000 = nearest). Cloud deck sits at y≈1160–1350 (`cloudparent` groups, cloud1.tif); terrain y≈100–160; `litehouse` at (-6932, 128, -3042), town/airbase cluster around (-5000..-6600, 128, -5900..-6700).
- `horizon` subtree = original skydome (sky1/sky2.tif, 17 km) — skip or it swallows the scene.
- Cloud/sky geometry (`cloud1`/`cloud2`/`cloudlayer` at y≈960–1350) renders but is **not** collidable; it's identified by texture, not node name (a `cloudlayer.tif` plane sits under the generic node `g27517`). Terrain tiles are named `a3`/`a5`/…; a downward probe reads terrain height ≈150 m (and 0 over the harbor water).

### zrdr flight stats (validated on this install)

- zrdr reader files are nested lists; by convention they alternate `key, [values…]` (a "dict"). `vehicle.json` root[0] alternates def-name / property-list with `kind_of` inheritance: `pbloodhawk` → `player_airplane` → `basic_airplane`. Player planes are the `p*` defs (`pbloodhawk` has `nodename player_bhawk`); the non-`p` defs (`bloodhawk`) are AI variants.
- Units are meters/seconds: `fd_speed` 135 m/s ≈ 302 mph matches the Bloodhawk's published top speed; `flight_ceiling` 2500; player.json `nom_gravity` = 20 m/s² (arcade 2 g).
- The `dynamics` block per plane: `pitch/roll/rudder_torque`, `return_rate` (extra centering when stick released), `ang_momentum_damp`, `rec_moments_inertia` (reciprocal, x=pitch/y=yaw/z=roll), `fd_speed`, `drag_factor`, `veh_weight`, `ref_area`. Steady rate = torque·recInertia/damp (Bloodhawk roll ≈ 1.65 rad/s).
- `engines.json` = rows `[id, name, power]`; a plane's `engine` prop picks its stock engine (Bloodhawk: 11 = Lvl-2, 0.62). Engine power scales thrust/acceleration in our model; `fd_speed` stays the level-speed cap.
- Sound (validated): sounds.json `SETS` maps `snd_*` → WAV + flags (`LOOPED`, `3D`, `FREQUENCY` = pitch-shiftable, `RANGE [full-vol, audible]` m, `VOLUME`). Each plane def names its own engine loop (`engine_sound snd_bloodhawkengine` → bloodhawk.wav). player.json curve blocks (all clamped two-point ramps): `engine_sound` = pitch 0.6→1.0 over throttle 0.1→1.0 (volume flat 1.0); `prop_sound` = **overspeed dive whine**, volume 0→0.5 over speed 1.0→1.1× fd_speed, pitch 0.65→1.25 over 1.0→1.2× (WAV not named in readers — we use `snd_enginewhine`, the only pitch-shiftable candidate); `rattle` = snd_planeshake, volume 0→1 over 1.0→1.2× fd_speed.
- All game WAVs are MS ADPCM (fmt tag 2, 4-bit, 22050 Hz, mono or stereo) — Godot only loads PCM/IMA-ADPCM/QOA, hence the decoder in WavFile.cs.
- **Explosions/effects (surveyed 2026-07-14, for the future dogfight milestone): fully data-driven and already extractable — no binary anim format needed.** Three layers, all in zrdr + texture.zbd: (1) *sequencing scripts* — `ai_plane_destruct.json` / `player_plane_destruct.json` are `ANIMATION_DEFINITIONS` readers defining per-plane, surface-sensitive crash sequences (`player_crash_default`/`_dirt`/`_water`; AI equivalents per plane def) that toggle model subtrees (healthy→destroyed), play named sounds at nodes, and `CALL_ANIMATION` into effect scripts; (2) *particle emitters* — `flame_ball.json` (16 `PUFFER_STATE` blocks, e.g. `large_fireball`), `fire.json`, `pufftrails.json`, `flak_trails.json`, `zepskinfire.json` etc. define billboard-particle emitters with full parameters (NUMBER, SIZE_RANGE, LIFETIME_RANGE, GROWTH_FACTOR, random velocity, FRICTION, fade ranges) and a `TEXTURE_SEQUENCE` flipbook with per-frame timestamps; (3) *flipbook textures* in chapter texture.zbd: `fire_f01–06` (fireball), `fire101–112` (sustained fire), `exp_yel01`/`exp_red01`/`exp_blu02`/`exp_gre01` (flash sprites), `fireflare1`. The gamez effect-prototype nodes (`ball_of_fire`, `flak_explosion`, `dense_firetrail`, `explode_here1–9`) are mostly empty anchor Object3d's the emitters attach to. Maps naturally onto Godot GPUParticles3D/billboard quads. (`cam_anim.zbd`/`mis_anim.zbd` remain unsupported but appear unneeded for dogfight effects.)

## Format support status (validated against THIS install with mech3ax v0.6.1, 2026-07-14)

mech3ax's README support matrix is outdated — actual v0.6.1 support for CS is far better. All validation ran on this install (`unzbd cs …`, round-trip via `rezbd cs …` + sha256):

| Format | Status |
|---|---|
| `texture.zbd` / `rtexture*.zbd` / `rimage.zbd` | ✅ extracts to PNGs; round-trip **byte-identical** (C1 verified) |
| `soundsh.zbd` / `soundsl.zbd` | ✅ extracts to WAVs |
| `zrdr.zbd` (reader/mission config) | ✅ extracts to JSON (ai, engines, Briefing, …) |
| `interp.zbd` | ✅ extracts to JSON (engine boot scripts) |
| `gamez.zbd` (world geometry) | ✅ extracts (metadata/textures/materials/meshes/nodes JSON); round-trip **byte-identical** (C1 + C5 verified) |
| `planes.zbd` (aircraft models) | ✅ extracts — it's a GameZ-format file (the boot script loads it via `GameZReadZBDFile`). Round-trip differs by only 72 bytes / 6 MB: swapped `\0`/`.` garbage past the null terminator in fixed-width texture-name fields. Semantically lossless; upstream fix candidate. |
| `cam_anim.zbd` / `mis_anim.zbd` | ❌ genuinely unsupported — deferred, not needed for free flight |

Extracted plane data confirmed usable: `nodes.json` has 3,317 nodes including full hierarchies for `player_bhawk`, `player_peacemaker`, `player_kestrel`, `player_autogyro`, `player_avenger`, `player_balmoral`, `player_fury` with control surfaces (ailerons/elevators), props, gear, firepoints, cockpits.

## Current status / next step

**Milestone 1 (extraction) is essentially already delivered by mech3ax v0.6.1** — the planned RE work is reduced to (a) the cosmetic planes.zbd padding nit (upstream PR candidate) and (b) the deferred anim formats.

**Milestone 2 in progress (2026-07-14): plane rendering, C1 world rendering, arcade free flight, and flight sound work.** The Godot project renders textured aircraft (verified: `player_bhawk`, `player_kestrel`, `player_autogyro`) and the full C1 Sea Haven world from `c1-gamez.zip` (verified via `--world --screenshot` renders). `--fly` gives free flight over Sea Haven: zrdr-driven arcade dynamics, chase camera, HUD (verified via `--hold` scripted flights + screenshots — straight flight settles at the drag-curve equilibrium, roll rate matches torque/damp prediction), and original-game sound: the plane's own engine loop with data-driven throttle→pitch, overspeed whine, and airframe rattle (verified via scripted dive past fd_speed — loops start/stop cleanly, no decode warnings). **First user playtest passed (2026-07-14): flight feel "real good for a first throw", sound "like the original"** — the TUNE constants and the audio pipeline are validated as a solid first approximation; fine-tuning deferred until there's more to compare against.

**Ground/object collision added (2026-07-14), user-confirmed working.** The flyable world gets static trimesh colliders (2670 in C1; cloud/sky exempted by texture), and the flight loop swept-raycasts the frame's path each physics tick: flying into terrain, water, buildings, or the airbase zeppelin crashes the plane. Verified via scripted `--hold` runs: a dive crashes at the true terrain surface (≈150 m) instead of sinking through to the old y<60 backstop; a full-throttle climb passes cleanly through the cloud layers (to ≈1200 m) with no false crash. Rendering is unchanged (colliders are invisible; static `--world` viewing skips them).

**Crash sequence + collision debug view added (2026-07-14):** a crash now plays one of the game's own plane-explosion one-shots (`snd_exp_plane1..4`), hides the airframe, and freezes at the impact point until R / gamepad Y/A respawns (scripted `--hold` runs auto-respawn after 1.5 s). `--debug-collision` draws the swept collision ray (green, red at impact). Verified via scripted dive: crash → banner + frozen red probe (screenshot) → auto-respawn resumes flight; all four explosion WAVs load without warnings. **User playtest passed (2026-07-14): "the explosion sounds good."**

Remaining known gaps: original skydome not used (procedural sky instead), clouds are alpha-scissor cutouts (original alpha-blends), prop/vehicle animations static, no propstart/propstop one-shots (mid-air spawn starts with engine running), no crash explosion visual (sound + hide only). With rendering, flight, sound, and collision in place, **Milestone 2's core is essentially complete.** **Next step:** pick among the remaining polish items (candidate: render the original `horizon` skydome).
