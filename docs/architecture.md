# Godot project — per-module implementation notes

One `## src/...` entry per module in `CSVM/src`. Entry shape: 1–2 sentences of purpose beyond
CLAUDE.md's one-line index, then every still-binding constraint or deliberate-design marker as a
`⚠` one-liner. Body ≤ ~8 lines (~12 for the heaviest modules). Look a module up with
`Grep "## src/Mech3/SceneBuilder.cs" -A 12` — the shape guarantees that returns the whole entry.

Narratives, diagnoses, and landed-work stories do not live here: they get a short dated entry in
`HISTORY.md`, and git history keeps the rest. Knowledge about the game's data formats belongs in
`docs/formats/`, not here.

## src/Mech3/GameZ.cs
Loads a mech3ax GameZ extraction (zip or unpacked dir): nodes/models/materials/textures JSON into
plain C# objects, reading both the v0.6.1 "legacy" and the fork "unified" shapes (field mapping:
docs/formats/gamez.md); `WorldTransformOf` resolves a node's world transform without building it.
⚠ GameZNode.Index is the flat list position, NEVER the unified JSON `index` (1-based, duplicated);
  child_indices are flat positions too — getting this wrong rebuilds the graph without erroring.
⚠ The unified transform `scale` is deliberately ignored (measured unit on every transformed node).
⚠ ModelType/FacadeMode/TextureScroll are unified-only: null/zero on a legacy tree, SceneBuilder
  falls back to its texture-name heuristic. Reading both shapes keeps a v0.6.1 rollback data-only.

## src/Mech3/TextureArchive.cs
Texture lookup over an unzbd texture zip or unpacked PNG dir; absorbs the stored-name quirks
(20-char truncation prefix match, legacy `.-N` renames, the fork's trailing doubled period — see
docs/formats/gamez.md) and classifies each texture's alpha channel via LastHadAlpha /
LastAlphaIsSoft ("soft" = a 0.5 scissor cutout would erase or shred it; drives blend-vs-scissor).
⚠ Unresolved names are reported ONCE via plain GD.Print (MissingTextures), never GD.PushWarning —
  Godot .NET prints a full managed stack trace per PushWarning call and buries real errors.
⚠ IsKnownAbsent (pir_spinner, barngrill — verified absent from the whole extraction) renders
  neutral gray; debug magenta must keep meaning a genuine name-resolution failure, not a data gap.

## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order
as depth bias (priority × surface rank × node index → polygon offset).
⚠ Instance-uniform block is an ORDERING CONTRACT — every shader on one
  instance must declare the same block (csky_instance_uniforms).
⚠ A shader with NO instance uniform must not take the preamble
  (16-vec4 per-instance buffer cost).
⚠ Hybrid by design: the 128 blend/scroll/clamp variants stay generated in C#;
  .gdshaderinc holds only the shared blocks.
⚠ Do not raise DepthBiasPerLevel/SurfaceRankBias/NodeOrderBias — the measured coplanar-separation
  floor is ~1e-6 of view distance; a uniform raise scrambles the authored layering (C5 got worse).
⚠ Never blanket repeat_disable: UV clamp is per-surface (UvsWithinUnitSquare); 54% of surfaces tile.

## src/Mech3/PlaneBuilder.cs
Builds one aircraft from its GameZ subtree (shaded, cullBackfaces: true — interior lattice must be
backface-culled or it paints over the skin), skipping cockpit/destroyed/shadow/*_hook subtrees.
Repaint(scheme) re-liveries the built plane in place; BuildDestroyed builds the wreck subtree with
the plane-root→destroyed transform chain baked in; WingFlares/DamagePanels expose collected nodes.
⚠ Skip pdpN/pcdpN torn panels (or build hidden under spinningProps/damagePanels) but always render
  the pdpN_h healthy twins — skipping all of player_damage_on amputates real airframe sections.
⚠ nitropropN stays hidden in flight: a NON-spinning blur disc overlaid on spinning ones shimmers.
⚠ *blur* disc textures must alpha-blend, never scissor — alpha peaks ~26%, scissor erases them.

## src/Mech3/PaintScheme.cs
One aircraft livery: pattern name + three colours + three decal indices — the paint_* record a
vehicle.json def carries (see docs/formats/paint.md). LoadCatalog keeps one scheme per pattern
name (the 12 shipped patterns); Random() draws a plausible livery when none is given.
⚠ Colour triples are always integer 0-255 — never weather.json's dual float/int encoding.
⚠ player_fortune ships a pattern name with NO colours (they live engine-side); its catalog entry
  is filled with the Fortune Hunters red 223,0,41 documented in paint.md.
⚠ Random() is deliberately NOT three independent RGBs (clown planes): identity colour + two trims.
⚠ Index catalogs with RandiRange, never (int)Randi() % n — the uint cast goes negative half the time.

## src/Mech3/PatternLibrary.cs
Decodes the original's .BM paint patterns from extracted/rof/ASSETS/GRAPHICS/<PATTERN>/ (produced
by ExtractRof.ps1; .BM layout in docs/formats/rof.md). PatternsFor(prefix) lists the patterns
shipping skins for one aircraft — a pattern is per plane; Skin() caches per (pattern, skin) so
several aircraft in one session share a decode.
⚠ A missing rof extraction is NOT an error: Load returns an empty library plus one line naming
  ExtractRof.ps1, and everything downstream builds unpainted.

## src/Mech3/PlanePainter.cs
Applies a PaintScheme to one aircraft: composites its skins from the pattern's region masks and
swaps the three decal placeholders. Read docs/formats/paint.md and rof.md first — the composite
formula, the shading-plane choice, and the bottom-up .BM rows are documented there.
⚠ The ZBD skin's alpha is copied onto the composite: SceneBuilder already chose blend-vs-scissor
  from that texture's alpha class, so a substitute must preserve it.
⚠ PrefixFor reads the skin prefix off ANY of the three *_noselogo/_taillogo/_winglogo materials —
  the Firebrand ships no fir_noselogo.
⚠ Built per plane instance and never mutates the shared TextureArchive cache.

## src/Mech3/PropParts.cs
Classifies a plane's propeller/rotor subnodes by name (staticpropN/staticrotorN, nitropropN,
propN/propNb, rotorN/rotorNb) and supplies each spinning kind's local axis + rate — the
XYZ_ROTATION values (deg/s, docs/formats/anim-definitions.md) from plane_props.json (spinprops)
and autogyro.json (agyro_rotors): props spin about local Z, rotors about local Y.
⚠ nitropropN is classified but never spun — no nitro system yet; PlaneBuilder keeps it hidden.
⚠ The perceived rate is a visual TUNE: a blur disc reads as spinning at any smooth rate.

## src/Mech3/ControlSurfaces.cs
Classifies a plane's control-surface mesh nodes + hinge axes: the deflecting node (l/r_aileronN,
l/r_elevatorN, l/r_rudderN, the Fury's l/r_rudder_rotate) hangs under a hinge parent group whose
transform places/orients the hinge line; ailerons/elevators hinge about local X, rudders local Y.
⚠ Parent hinge-group names deliberately do NOT classify — rotating parent and child would double
  the deflection (digits are required on the bare l_rudder form for exactly this reason).
⚠ No zrdr anim defines deflection — the original drives these procedurally, so angles/rates are
  TUNE in ControlSurfaceAnimator, not data.

## src/Mech3/WingLights.cs
Single source of truth for wingtip nav lights: the flare-node predicate (wing_flare1/2), the glow
texture (oil_liteflare), the warm-amber flash colour (0.88, 0.78, 0.36 = wing_light.json's
LIGHT_STATE COLOR) and the blink period (1.5 s = its LOOP SEQUENCE_OFFSET). PlaneBuilder hides and
re-skins the flares; WingLightBlinker flashes them.
⚠ oil_liteflare also skins a few real airframe meshes — scope the additive-billboard treatment to
  the flare NODES by name, never through the texture, or those meshes get recentered/billboarded.

## src/Mech3/WorldBuilder.cs
Builds a chapter world (fullbright): World children + partition-referenced subtrees; skips `horizon`
(`BuildHorizon` makes the camera-anchored skydome), `fvol*`, `dzpaths`. Splits the overcast deck into
`CloudDeck` (PlaneViewer moves it with the player); hides origin-parked unplaced vehicles.
⚠ Cloud/sky is a TEXTURE test (`IsCloudOrSkyTexture`), never node names. Collision exempts via
  `IsNonSolidSkyTexture` (= that AND NOT `skywal*`, a BUILDING texture) — narrow there only; the
  shared predicate also drives the cloud alpha-blend rule and MapEdgeExtender's tile filter.
⚠ The deck is found STRUCTURALLY (`FindCloudDeck`: flat one-quad tiles bucketed by altitude,
  coverage ≥ `DeckCoverageFraction` of the map area) — a `sky*` texture rule drags terrain into it.
⚠ `ResolveHorizonZone` falls back to the horizon's FIRST zone child — C5 ships no `zone2` (see
  docs/formats/weather.md); the skydome fogs on purpose (FOG_ALTITUDE fade), never shadows/collides.
⚠ `HideUnplacedEntities` needs the BUILT subtree's world AABB (gamez `child_bbox` is LOCAL and
  matches all terrain); one-shot sweeps break motion targets still at origin → `RestorePlacedEntities`.

## src/Mech3/MapEdgeExtender.cs
Rolling window (`Rings`=5 of 1024 m cells, diffed only on cell crossings) of repeated border tiles +
clutter (grown from `ClutterBuilder.ExportedKinds`) continuing the world past the map edge.
⚠ Repeats the LOCAL BORDER CELL, never the map interior (whole-map tiling brought the airport
  back); `MirrorAxis` clamps to the border cell and alternately reflects copies — OUR seam-free
  construction; the original may plainly repeat (open fidelity question; swapping is one line there).
⚠ Extension sprites carry no collider (matching the map); buildings DO — one lazy `clutter_bld_ext`
  body per cell attaches the shared `KindExport.CollisionShape`; 3D kinds mirror as whole transforms.
⚠ The window is the UNION of all player cameras' neighbourhoods — one focus strands the other pane.

## src/Mech3/Clutter.cs
Stamps the boot-script clutter templates across placed polygons carrying the template's ground
texture, on a fixed world-space X/Z grid of the template period; sprites → one fullbright Y-billboard
MultiMesh per kind, solids → `SceneBuilder.SharedMesh`; the split is `SceneBuilder.ClassifyBillboard`.
⚠ Sprites are NOT collidable — no tree-destruction anim exists in the install (`spruce_destroy*`
  is the Spruce Goose; docs/formats/clutter.md). Solid decorations ARE collidable.
⚠ Collision shapes are SHARED, never expanded per placement (that costs seconds of BVH build): one
  `ConcavePolygonShape3D` per distinct mesh, `BodyAddShape`d at each placement onto per-region
  `clutter_bld_<cx>_<cz>` bodies. They live on the body RID — a `ShapeOwner*` call would run
  `_update_shapes()` and clear them; `SharedShapeMeta` on the clutter root anchors them against the GC.
⚠ Convex/box stand-ins were rejected on the data: the decoration shells fill ≤5% of their bbox — phantom solids.
⚠ Sprites drop basis + local Y at placement; an upright render is NOT proof the basis is consumed
  (authored bases ≈ identity) — the evidence is docs/formats/clutter.md.

## src/Mech3/Zrdr.cs
Zrdr extraction reader (zip or unpacked dir): `LoadFile`, content-sniffing `LoadMatchingFiles`,
and `ZrdrDict`, the key/[values…] view over a reader's alternating list.
⚠ `LoadFile` accepts both entry namings — v0.6.1 writes `X.json`, the fork writes `X.zrd.json` —
  in both the zip and directory branches.
⚠ `ZrdrDict` COLLAPSES duplicate keys; anim definitions repeat keys meaningfully, so AnimDefs
  walks the raw lists instead.

## src/Mech3/Messages.cs
The game's localized string table: plain `System.Text.Json` over the extracted `messages.json`
(NOT a zrdr reader), a case-insensitive key→value map resolving the `MSG_*` keys missions reference.
⚠ Degrades, never throws: a missing file yields an empty table, and `Get` returns the raw key for
  an unknown entry (visible, not blank) — display strings are cosmetic.

## src/Mech3/MarkerRig.cs
A player airframe's weapon marker rig read from planes.zbd GameZ: `Extract` walks a `player_*`
root, accumulating locals down to each `firepoint*`/`pylon*`/`target`, and reports plane-frame
positions + co-located groups (two gun groups on one mount). `Format` prints one dump block per
plane; `PlayerAirframes` is the model→display list. The committed instrument `docs/formats/markers.md`
regenerates from, and the source of truth `--dump-markers` and `UI.MarkerOverlay` share.
⚠ `Classify` requires a numeric suffix, so the AI airframes' bare `firepoint`/`pylon` are excluded;
  only the 11 player roots are walked.
⚠ Co-location is exact-position (1 cm tol) and crosses mirror-pair boundaries — Brigand fp1≡fp4,
  not fp1≡fp2 — so group by position, never by consecutive index.

## src/Mech3/CompiledAnim.cs
Reader for the fork's compiled `cam_anim`/`mis_anim` extraction (zip or dir): typed defs, events,
and the SI-script pool — `Script(index)` parses lazily, ordered by `metadata.json`. Decode facts
(ptr = flat node index, shifted quat labels, half-angle cubics): docs/formats/anim-definitions.md.
⚠ Payloads stay a generic `AnimData` bag, not per-kind DTOs — a new event kind costs this file nothing.
⚠ Degrade, never fail the build: missing archive → null (normal); corrupt archive → log + skip.
⚠ Keep `Parse`'s spline handling: `spline_interp: false` coefficients are garbage that can be
  FINITE — `SiCubic.Eval`'s non-finite guard cannot catch it; degenerate cubics are synthesised
  at parse (so `At(dt)` stays branch-free), and non-finite/zero quaternions are rejected there.

## src/Mech3/AnimDefs.cs
The zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into CompiledAnim's
`AnimDefinition` model (op key SNAKE_CASE→PascalCase IS the compiled tag; unclaimed bodies stay
under `raw`). Exists because compiled archives are incomplete: `zepstate`/`startanims` are reader-only.
⚠ `ParseDef` defaults `AnimName ??= Name` — without it a reader def misses its compiled twin in
  AnimProgram's dedupe key and runs as a second, independently-anchored copy.
⚠ `AddCallTarget` must NOT touch `data["node"]`/`data["name"]` — for CALL_ANIMATION those hold the CALLED animation's name.
⚠ `ReaderCondition` is the ONE place reader↔compiled unit conversions live (PLAYER_RANGE m→m²,
  ANIMATION_LOD HIGH→2); `AddPufferState` bridges the PUFFER_STATE shape differences.
⚠ `DAMAGE_SEQUENCE` parses into a sequence literally named `DAMAGE_SEQUENCE` (the compiled twin's
  name) — the magic name `AnimRuntime.ApplyDamageStages` invokes; it is otherwise an ordinary
  IF/ELSEIF `ANIM_HEALTH` event list (docs/formats/destructibles.md).

## src/Mech3/AnimProgram.cs
Merges the compiled + reader front-ends for one mission — load both, prefer compiled on collision,
keep the remainder — plus `StartAnims`; `ScriptFor` resolves an event slot to its archive SI script.
⚠ Definition identity is (anchor name, animation name) — the dedupe key reader twins must collide on.
⚠ `StartAnims` is a LIST — order matters, last write wins (C1/IA1 layers two anims on the same doors).
⚠ The mission zrdr scope is a LIBRARY, not a manifest: a mission-scope reader def applies only if
  the mission's compiled archive contains it (docs/formats/anim-definitions.md); the gate engages
  only when the manifest actually loaded, and skips are recorded in `MissionLibrarySkipped`.
⚠ Not what decides which world ENTITIES a mission shows — that is MissionSetup + interp.

## src/Mech3/TextureCycler.cs
Runs the gamez material `cycle` flipbooks (water, surf, wakes, crowds) by swapping `albedo_tex`;
frames resolve at build time while the TextureArchive is open — an incomplete flipbook stays static.
⚠ `SceneBuilder.RegisterCycle` registers each BUILT material, not the source: the (material,
  priority, rank, sidedness) cache key yields several ShaderMaterials per cycling source.
⚠ C#-side swapping is deliberate (1–7 cycling materials per chapter): no shader variant, atlas, or same-size assumption.
⚠ Screenshots cannot verify open water (frames differ ~2/255); use `--debug-anim`'s flipbook log.
⚠ The `EFFECTS` reader (`fire1`/`fire2`) is this mechanism bound to a NODE and is NOT wired up —
  it needs OBJECT_ADD_CHILD (docs/formats/effects.md).

## src/Mech3/WorldSounds.cs
`SOUND_NODE` ambient looping 3D emitters: one pooled AudioStreamPlayer3D per live emitter,
following its host's pose per frame; one-shot `SOUND` is a separate mechanism, NOT landed.
⚠ Pooled, never parented into world subtrees — AnimRuntime's FindAll memoization forbids runtime reparenting.
⚠ An emitter is silenced while its host is not visible in tree (the point-light rule) — what makes
  raw emitter counts harmless; a blown-up host pose is silenced + logged (an anim-runtime defect).
⚠ `Prewarm(AnimProgram.SoundNodeNames())` must run BEFORE the sound archive closes: the Loader
  dies with the world build and most SOUND_NODE events first fire at runtime; late failures warn
  via `AnimRuntime.ReportLateSoundFailure` (the bootstrap census is a snapshot and cannot see them).

## src/Mech3/WorldLights.cs
Packs the animated world's `LIGHT_STATE` point lights into the 2×N RGBAF texture the fullbright
world shader reads as spill (global `csky_light_data`, loop bounded by `csky_light_count`).
⚠ Not OmniLight3D — the unshaded world ignores dynamic lights, and the flare at each light is
  already gamez Facade geometry (a glow sprite would double-draw); spill is the missing behaviour.
⚠ Packed via BitConverter, never Image.SetPixel/Color — world-metre positions would hit the 0..1 clamp.
⚠ Count 0 must leave the spill term exactly vec3(0.0): an unlit world stays bit-identical to pre-lights.
⚠ Beyond `MaxActive` (16), rank by significance (range/distance × intensity), not distance; the
  900→1500 m fade (TUNE) dims a light before the budget can drop it, so nothing pops.

## src/Pads.cs
Single source of truth for gamepads — every reader goes through it, never `Input.GetConnectedJoypads()`.
Owns the phantom policy (span every pad, never `pads[0]`), `Disabled` (`--no-pads`), the focus gate.
⚠ The focus gate is on `For` (the read), NOT `Connected` (the roster) — `For(bound)` never
  consults `Connected`, and an empty roster would un-join menu players (`LaunchMenu.SyncDevices`)
  and break `PlaneViewer.AssignPads` at session build.
⚠ `Focused` defaults true and FAILS OPEN (headless runs unchanged); only pads need the gate —
  Godot releases held keys on focus loss, SDL pads are polled regardless.
⚠ Every joy read sits in a `Pads.For` loop except `MenuInput.JoinPressed` (checks `InputBlocked` inline).

## src/Mech3/MissionSetup.cs
Parses + applies the per-mission `.gw` interp script that decides which world entities a mission
shows; acts on `NodeSetActive`/`DeleteTree`/`Object3DSetScroll`, counts + reports every other verb.
⚠ Read docs/formats/interp.md before extending: order matters and last write wins, a `FindNode`
  matching nothing is NORMAL (never warn), and `DeleteTree` names its own target.
⚠ `Object3DRotate` is unimplemented on purpose — the data's angle unit is ambiguous; guessing would silently mis-pose props.
⚠ `ScrollByModel(gamez)` runs BEFORE the world build — a scroll rate is part of SceneBuilder's
  material cache key; per MODEL, not node, because the verb writes the model's `texture_scroll`.
⚠ The entity half applies as AnimRuntime bootstrap pass 0, before animation state (engine load order).

## src/Mech3/AnimRuntime.cs
The animation engine: bootstrap passes (mission setup, anchored RESET_STATEs, ON_STARTUP, startanims,
a safety net), then dispatch-table event playback; unhandled event kinds are counted, never fatal.
⚠ `FindAll` is memoized (`_findCache`); adding or reparenting world nodes at runtime must invalidate it.
⚠ A no-time loop iteration yields a frame — the test is `_iterScheduledTime`, not the clock; authored
  `Loop` count 0 = INFINITE, normalised to -1 where first read, never at the `_loopsLeft == 0` test.
⚠ Absent tween channels HOLD the live value; `FromToMotion`/`ScriptPlayback` keep rot/scale/origin separate.
⚠ `SpinMotion` re-seeds rest from the current pose — a bounded, deliberately-unfixed drift (backlog).
⚠ One motion per node, last registration wins (`AddMotion`); re-assertion is idempotent — only explicit
  `Stop` tears resources down (`TearDownResourcesOf`), `Start`'s own restart must NOT.
⚠ The safety net matches ONLY `destroyed` — a `*_dest` suffix names healthy destructible groups.
⚠ Crash runtime: `NameResolveFallback` keeps `_byIndex` EMPTY (non-portable ptrs, colliding index
  spaces); `Targets()` never falls back to names; crash puffers must parent at world level.
⚠ `ANIM_HEALTH`/`ANIM_HEALTH_RANGE` read LIVE HP via `HealthOf` → `DestructibleRegistry`, not
  `def.Health`; the registry is built in bootstrap pass 1 beside RESET_STATE. Nothing damages HP
  in normal play yet, so every instance is at full health and the read is a no-op today.
⚠ `ApplyDamageStages(instance)` runs a destructible's `DAMAGE_SEQUENCE` against its live HP (C22),
  firing the ONE stage effect for the crossed threshold. It escalates via `DamageStage` (only
  when a deeper threshold is crossed) — do NOT lean on `CALL_ANIMATION`'s live guard for "once":
  a one-shot damage effect (`damage3_mp1zreng11`) finishes and would re-fire without the gate.
⚠ `DamageAt(struck, healthDamage)` is the weapon-hit entry (C23): resolves the struck collider to
  its destructible (`Registry.Resolve`), spends `HEALTH_DAMAGE` (world objects have HEALTH only —
  no armour pool), escalates, and marks it `Destroyed` at zero. Fed by `ProjectilePool.DamageSink`.
⚠ At zero, `RunDeathSequence` plays the def's death via `Start(def)` — its Initial sequences ARE the
  destruction (the swap sequence's name varies: `destroyit`/`destroy_h2twr`/unnamed, always Initial,
  so don't pick by name). `ApplyDeathSwap` is the fallback for the ~10% (AA guns) that declare the
  healthy/destroyed pair but author no swap: flip the roles the def's own RESET_STATE named, only
  when RESET declares a `destroyed` node — the def's explicit targets, NOT a world scan (C24).
  Debris ballistic ObjectMotion (C26) + one-shot Sound (D31) still stubbed.
⚠ Collider removal on death is FREE (C25), not separate code: `SetSubtreeActive` toggles
  `SetCollidersEnabled` with `Visible`, so the swap that hides `healthy`/shows `destroyed` also
  un-solids the door/building and solids the wreck. Measured off/on per kill (C2 gates: off 1, on 8).
  The healthy collider only exists in the FLIGHT build — `Collision` is `_fly` — so a `--freecam`
  census reads zero; the C25 harness forces it with `|| _damageTest`.
⚠ Debris tumble (C26) is FREE too: the ballistic `ObjectMotion` half (`MotionRuntime` —
  translation_range/gravity/forward_rotation/scale over a run_time) was built by the M2 crash work and
  is REACHED on death because the death's `OBJECT_MOTION` events are `Initial`, so `Start` runs them.
  The launch is SCHEDULED mid-sequence (water tower at t=2.2 s), so it only fires as the death plays
  out — a kill-and-check must `Advance` the clock to see it (`BallisticMotionsLaunched` counts them).
  `do_intersections`/`bounce_sequence` ground-rest stays deferred.

## src/Mech3/DestructibleRegistry.cs
Live, mutable per-instance HP for the world's destructibles — any `AnimDefinition` with
`HEALTH > 0`. One `Instance` per `(def, anchor)` pair, seeded from the authored `HEALTH`, plus a
coarse healthy/damaged/destroyed `State` and a monotonic `DamageStage`; built during AnimRuntime's
bootstrap, read by `ANIM_HEALTH` eval, escalated by `ApplyDamageStages`, damaged via `DamageAt`.
`Resolve(struck)` maps a raycast-hit node back to its instance. Schema: docs/formats/destructibles.md.
⚠ Keyed per `(def, anchor)`, NOT per def — a wildcard NAME binds many node groups, each an
  independent pool (one tower's damage must not touch its siblings).
⚠ Instances can exceed node groups (`Count` vs `DistinctAnchors`): the reader's wildcard def and
  the compiler's per-instance defs both bind the same nodes, so one object carries several pools
  (same HEALTH). `_authoritative` keeps ONE per anchor node, compiled-preferred.
⚠ `Resolve` walks the WHOLE parent chain and takes the nearest COMPILED anchor, not the first hit:
  a reader wildcard can grab an inner node the compiled def doesn't (tower `ap_h2otwr*` matches
  `ap_h2otwr.flt`, between the collider and the compiled `ap_h2otwr1` root), and the compiled def
  owns the real DAMAGE_SEQUENCE + death sequence.

## src/Mech3/WavFile.cs
Pure-C# WAV parser with an MS ADPCM→PCM16 decoder (`DecodeMsAdpcm`), no Godot dependencies —
Godot cannot load the game's WAV format (see `docs/formats/sounds.md`).

## src/Mech3/SoundArchive.cs
WAV lookup over a soundsh/soundsl extraction (zip or dir), decoded through `WavFile` into cached
`AudioStreamWav`s; `Find(name, looped)` marks the stream as a forward loop when asked.

## src/Mech3/SoundDefs.cs
sounds.json SETS parser: `snd_*` name → `SoundDef` (wav name, flags, range, volume); the entry
grammar and flag/key meanings are in `docs/formats/sounds.md`.

## src/Flight/WeaponDefs.cs
Typed reader over the shared `weapons.zrd.json` `BALLISTICS` block — 48 `WeaponDef`s (guns /
rockets / ordnance) keyed by `wep_*`, plus the `NO_AMMO_WARNING` empty-clip sound. Ballistics,
damage, allotment, the class flags, the specials, and the `FIRE`/`FLYOUT`/`IMPACT` bindings
(`IMPACT` keyed by `SurfaceClass`); `DESC` resolved through `Messages`. Modelled on PlaneStats.
Schema: docs/formats/weapons.md. Verify/inspect with `--dump-weapons`.
⚠ Flags (`CANNON`/`ROCKET`/`HIGH_EXPLOSIVE`/…) are `KEY,null` in the data — `ZrdrDict` bare-flag
  handling makes them present-but-empty, so `Has` is the test; a valued struct (`BEEPER`/`TANGLER`)
  is `Has`+`Dict`.
⚠ `IMPACT` is walked as raw class/value pairs, not via `ZrdrDict` — a null class value (`enemy`,
  "no effect on that surface") must be skipped, not read back as an empty binding.
⚠ `UnhandledKeys` is a tripwire: empty for this install (asserted by `--dump-weapons`); non-empty
  means the data grew a key `KnownKeys` hasn't learned — update the reader, don't ignore it.

## src/Flight/Loadout.cs
Two layers over `CSVM/data/stock_loadouts.json`. `StockLoadouts.Load` parses the file (default
`res://data/`) into per-plane `LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves
each gun slot's markers to live muzzle `Node3D`s and its caliber+ammo to a `WeaponDef` (via
`GunWeaponId` = `wep_{N+k}`), and each hardpoint to its `pylon`, yielding `GunGroup`s (independent
ammo counters from `CLUSTER_SIZE`) + `Hardpoint`s. Turret slots bind but `IsTurret` (inert, M4).
Schema: docs/formats/loadouts.md. Verify/inspect with `--dump-loadout`.
⚠ A missing marker is a LOUD throw naming plane/slot/marker — never a silent skip (a silent one
  fires a gun from nowhere). Markers resolve by `cs_name` meta from the built tree, like MarkerOverlay.
⚠ Gun ammo is per group (Balmoral's two .50s carry 2000 each); rocket ammo is per pylon
  (`CLUSTER_SIZE` each, total = pylons × that) — A9. Config lives at `res://`, NOT under `--data-root`.

## src/Flight/Projectile.cs
`ProjectilePool` — the shared-world weapon-fire subsystem (B13/B14/B15/D29/D30/D33): a fixed pool of
projectiles integrated with the data's ballistics (VELOCITY/ACCELERATION/GRAVITY, expiring at
RANGE), plus tracer streaks, muzzle flashes, per-surface impact sprites and the IMPACT sound.
`Spawn(weapon, worldMuzzle, inheritVel)` fires one round (with a CANNON_SPREAD cone) and flashes
the muzzle; it runs itself each physics frame. One pool per session, fed by every player's guns.
⚠ Hit detection is a per-step world raycast; the flying plane has no physics body, so a round never
  hits its own launcher and `player`/`enemy` IMPACT classes are unreachable in M3.
⚠ `DamageSink` (wired to `AnimRuntime.DamageAt` in flight, C23) turns a hit into destructible damage:
  every `Impact` invokes it with the struck collider + `HEALTH_DAMAGE`; a no-op for terrain/water.
  Null in views with no anim runtime, where impacts stay cosmetic.
⚠ Surface class comes from the struck collider's `SceneBuilder.SurfaceMeta` (water/buildings),
  stamped at build time from the mesh's dominant material texture; absent ⇒ `default`.
⚠ Tracers are velocity-aligned, NOT billboarded (billboard would collapse the streak to a
  screen-vertical bar); muzzle/impact bursts ARE round billboards. Per-instance colour via MultiMesh.
⚠ Rockets fly the FLYOUT MODEL body (B14): `Spawn` instances the weapon's `.flt` prototype root
  (`he_rocket` …) from the chapter gamez via the world `SceneBuilder` (collision-exempt, so rounds
  don't obstruct one another), posed nose-along-(-Z) down the velocity via `Basis.LookingAt`. Only
  rockets get a mesh (≤1 alive at 1/s); guns stay on the MultiMesh tracer quad (≈10/s, dozens alive).
  A rocket with a body trails a slim exhaust streak; `RocketStreakScale` is now only the fallback
  when a chapter lacks the prototype. The FLYOUT `MODEL_ANIMATION` smoke trail is still pending (D-wave).

## src/Flight/PlaneStats.cs
Typed per-plane stats: vehicle.json `dynamics` (resolved through the `kind_of` def chain) +
engines.json stock engine power + player.json globals, the `engine_sound` def name with its
volume/pitch `SoundCurve`s (clamped two-point ramps), `destroyable_parts` → `DestroyablePart`
records (name, max HP, `critical`/`engine` flags, `got_hit_anim`, per-part `injure_anims`), and
the def-level `VehicleInjureAnims`. Schema: docs/formats/vehicle.md.
⚠ Def-level injure_anims are consumed as ANY-part HP fractions, not per-part — see DamageVisuals.

## src/Flight/SpawnPoints.cs
Reads the flight spawn from a mission's OWN zrdr (`extracted/<chapter>/<mission>/zrdr/` — a
different archive than the shared `--zrdr`), two schemas both yielding
`SpawnPoint(Position, HeadingDeg)`: `LoadIa` (instant-action ia.json `spawn_points` per scenario;
only IA1 folders have one, the original picks one at random per launch) and `LoadPlayerInit`
(story objectives.json `PLAYER_INIT`, position + yaw). Schema: docs/formats/spawns.md.
⚠ Throttle/speed from the data are deliberately ignored — PLAYER_INIT[3]/[4] are not spawn
  throttle/speed (see spawns.md); the remake uses FlightController's fixed start.

## src/Flight/MissionTargets.cs
Loads a mission's targets.json: world-node NAME → objective display keys
(`description`/`category_label`/`help_label`), resolved through `Messages`. Generic across
mission types; a missing file yields an empty set. Schema: docs/formats/missions.md.
⚠ The file is a list of [key, value]-pair lists, NOT the flat-alternating reader shape — walk
  it as pairs, never through `ZrdrDict`.

## src/Flight/StuntMission.cs
Stunt Flying state: `Load` builds the ordered zone list from ia.json `dzones` (positions via
`GameZ.WorldTransformOf`, strings via MissionTargets + Messages; null when a mission has none →
free flight); `Update` completes zones within `DzRadius` (15 m, TUNE), fires events, advances
the target; clock/scoring via `Elapsed`/`CompletedAt`/`CompletionOrder`/`InCompletionOrder`;
`ForAnotherPlayer()` clones an independent run so the archives parse once per session.
⚠ Drive off the dzones LIST, never the gamez `dzN` nodes — numbering is non-contiguous (missions.md).
⚠ `GeometryAnchor` covers zones naming world GEOMETRY (C2's `sghangar`: transform `"Initial"` →
  `WorldTransformOf` = origin, ~8 km off): anchor = union centre of the subtree's `door`-named
  leaf pair (the flown aperture), else the whole-geometry centre; null for every childless
  `model_index -1` marker. `child_bbox`/`node_bbox`/`active_bbox` stay deliberately unparsed.
⚠ Deliberately NOT reset on respawn (a mid-run crash keeps zones + clock); `Reset()` is the opposite.
⚠ `FormatTime` is InvariantCulture (German locale renders `2:13,6` otherwise); MarkerHud delegates to it.

## src/Flight/HudMetrics.cs
The one place the flight HUD decides how big it draws: `Scale(control, reference = 1440)` =
window height / reference, damped by `PaneFactor` = sqrt(paneH/windowH) inside a splitscreen
pane (2P ≈ 71 %, 4P 50 %; the damping exponent is TUNE). CompassTape, GaugeCluster, MarkerHud,
StuntScoreboard and FlightController's text block all route through it.
⚠ The single-player identity is load-bearing: a full-screen view has `PaneFactor` exactly 1, so
  `Scale` returns the plain height ratio unchanged.
⚠ Damped sizes only stay on screen if positions anchor to a pane EDGE — see GaugeCluster's
  bottom-anchored dials and FlightController's text block.

## src/Flight/MarkerHud.cs
The stunt objective marker HUD: a viewport-filling `Control` drawing the on-screen reticle/text
block, the off-screen edge arrow (`EdgePoint`, `ClockHour` bearing), run status and banners
(`CompleteBanner` branches solo vs race); one per player, sized via `HudMetrics.Scale(this)`.
⚠ Projects in `_Draw` via `Camera3D.UnprojectPosition`/`IsPositionBehind` each frame —
  deliberately not cached in `_Process`, so the marker never lags a fast roll.
⚠ Edge-arrow direction is NEGATED when `IsPositionBehind` — behind-camera points unproject mirrored.
⚠ `_Draw` early-returns at zero height and clamps font sizes to `Max(1, …)` — it can run before
  the Control/pane is sized (Godot font-cache `p_size.x <= 0` errors otherwise).

## src/Flight/StuntScoreboard.cs
End-of-run results overlay: plain Godot UI (dimming backdrop → CenterContainer →
PanelContainer → VBox + 3-column split grid) filled from `StuntMission.InCompletionOrder()`;
wakes on `RunCompleted` (records via `ScoreStore.RecordIfBest`, logs the split table to stdout
for headless review), branching NEW BEST vs BEST on the stored record.
⚠ Hides itself in `_Process` the moment `AllComplete` clears (a restart) — no explicit teardown
  wiring; `Populate` rebuilds the panel each time so a second run's board is clean.
⚠ Scale is `max(0.5, HudMetrics.Scale(this, 720))` — a plain viewport ratio floored at 1
  overflowed a splitscreen pane.

## src/Flight/ScoreStore.cs
Stunt best-time persistence: one JSON object in `user://stunt_scores.json` keyed
`chapter/mission/plane` → `{best, date}`; `GetBest` / `RecordIfBest` (returns whether it was a
new best — never worsens a record).
⚠ Read/written via Godot's `FileAccess` + `Json` — only that API resolves `user://`, and
  `Json.Stringify` is locale-neutral. A missing/malformed file loads as an empty store, never throws.
⚠ Deliberately not consulted by the splitscreen race — race totals aren't comparable across
  player counts or spawn indices.

## src/Flight/StuntRace.cs
Splitscreen race bookkeeping: one `Racer` per player (own `StuntMission`, `Rank`, `FinishTime`);
finishing stamps the next placing, `RaceCompleted` fires when the last pilot is in; `Standings()`
orders finishers by placing then in-flight players by progress; `Restart()` (rematch) resets
every mission and clears placings — the planes are respawned by PlaneViewer, which owns them.
⚠ `FinishTime` is snapshotted separately from `Mission.Elapsed` so the board still reads
  correctly after a rematch has reset the missions.
⚠ Deliberately not a Node — it is freed with the session, so the `RunCompleted` subscriptions
  need no teardown.

## src/Flight/StuntRaceBoard.cs
The race's shared ranked results overlay: same clean-Godot-UI construction as StuntScoreboard,
but covering the WHOLE window — on its own CanvasLayer (Layer 10, above SplitScreen's 0) under
the session root, one row per player from `StuntRace.Standings()` (placing, tag, plane, zones,
total + gap to the winner; DNF when unfinished). Wakes on `RaceCompleted`, hides in `_Process`
once `AllFinished` clears; the footer's exit hint follows how the session was launched.
⚠ Scales on raw window height / 720, NOT HudMetrics — pane damping would shrink a full-window
  overlay for no reason.

## src/Flight/Weather.cs
`WeatherState`: per-mission atmosphere from the flown mission's own weather.json — per-zone fog
(`FOG_COLOR`/`FOG_RANGES`/`FOG_ALTITUDE`), `SUNLIGHT_*` → `ZoneFog.WorldLight` (`SunIncidence`
0.46 / `MinWorldLight` 0.15, TUNE), the `CLOUD_COVER` whiteout band (`WhiteoutAmount`
trapezoid), `WIND`, and precipitation → `PrecipData`. Schema + colours + zone names: weather.md.
⚠ `CLOUD_COVER`/`WIND`/precip keys pair with BARE scalars — `ZrdrDict.FromAlternating` cannot
  read them; walked raw (`StringAfter`/`ScalarAfter`/…). Per-zone blocks are list-valued (dict).
⚠ `ZoneKeys` collects `ZONE<digits>` keys from the raw list in FILE ORDER (a Dictionary loses
  order; `ResolveZone`'s fallback is the file's FIRST zone); `SW_ZONE*` twins are excluded.
⚠ The default stays `zone2` (user decision) — which zone a mission flies is in no file
  (negative result in weather.md), so a fallback is the only correct behaviour.
⚠ `ParseColor` normalizes dual-encoded triples (÷255 iff any component > 1); PlaneViewer treats
  every result as DX7 sRGB and linearises.

## src/Flight/FlightAudio.cs
Own-plane non-positional loops (engine with throttle-driven pitch, overspeed whine, rattle) +
one-shots: `StartEngine`/`EngineStartRamp` prop-start fade (re-fired via the loop-restart hook
in `Update`), `OnCrash` → `snd_exp_plane1..4`, `OnGroundExplosion` layering `snd_exp_ground_a`.
⚠ `WhineMixGain` 0.12 (TUNE): don't raise it back — reader "volume" is not a linear mix gain
  (the original's whine sits 12–18 dB below the raw curve cap); re-derive from a new reference.
⚠ `OnEngineStop` is deliberately NOT called on crash; a future shutdown flow must also stop
  driving `Update`, or the restart hook re-fires propstart.
⚠ `MixGain` (1/√N in splitscreen, TUNE) covers only the three loops, never the one-shots.

## src/Effects/Puffer.cs
The original engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks
(schema: docs/formats/effects.md). `PufferState.Load` finds the fully-defined state in an
effects reader; `Puffer.Create` builds a texture atlas + ONE MultiMesh whose shader billboards
each quad, with quad-rim fade + soft-particle depth fade. Modes: `Burst`, `TrailAdvance` /
`TrailBurnAt` (distance trails), `SustainAt` (continuous at a moving node — pool sized to steady
state, catch-up capped); `PufferState.FromAnimEvent` parses the compiled anim payloads.
⚠ COLORS ramp ⇒ blend_mix, else blend_add (effects.md); `Create`'s `blend`/`softParticles`
  overrides exist because the data can lie — the crash `large_black_smokeball` has
  `colors: null` yet needs MIX, and the depth fade zeroes fresh ground-level smoke. Defaults
  leave every existing caller byte-identical.
⚠ A fading additive fireball READS AS SMOKE — isolate the emitter before believing smoke works.
⚠ Compiled-payload quirks (`interval_garbage`, Distance-trail meters, `growth_factors`): anim-definitions.md.

## src/Effects/CloudPuffs.cs
Synthetic ambient cloud field: ONE alpha-blended MultiMesh of cloud1/cloud2 billboards in a
cylindrical shell around the camera — Y anchored to the CLOUD_COVER band, X/Z following the
plane, passed puffs recycling to the leading edge, WIND-driven drift, alpha fading at the shell
edge and by vertical distance. Feel constants all TUNE (`BaseAlpha` kept low — overlaps saturate).
⚠ Synthetic by design: the world's own ~600 cloud sprites cluster near the airfield and no zrdr
  defines an ambient emitter — don't try to source this from world data.
⚠ The shader keeps `fog_disabled` yet carries the custom `csky_fog_*` cylindrical fog term —
  that render mode only disables Godot's BUILT-IN fog; ours is custom.

## src/Effects/Precipitation.cs
Rain/snow from weather.json's precip block (`WeatherState.PrecipData`): ONE MultiMesh whose
shader derives each quad's position from a per-instance seed + `TIME` + `CAMERA_POSITION_WORLD`,
wrapped into a camera-centred box — zero per-frame CPU. SNOW = fluttering flakes; RAIN =
streaks along the data's WORLD fall velocity (not plane-relative — TUNE pending A/B); sprites
are procedural `MakeFlakeTexture`/`MakeStreakTexture` (the original drew untextured primitives).
⚠ World-sized `CustomAabb` (±40 km) stops frustum culling — the instances sit at the node origin.
⚠ Cloud-band gate: precip renders only BELOW the CLOUD_COVER band; a huge sentinel band
  disables the gate when a mission has precip but no cloud band.

## src/Flight/SpectatorCamera.cs
The `--freecam`/`--anim-lab` observation camera: WASD move, RMB-held mouse look (captured only
while held), wheel speed, pads via `Pads.For(null)`; lab additions `Frame(Aabb)`, the
`FollowNode` orbit-lock (released by any translation input; `ExitFollow` keeps orientation) and
a public `Camera` accessor — all inert in plain `--freecam`. Rates TUNE.
⚠ Deliberate: NO collision; pitch clamped (`PitchLimit` ~89°, `OrbitPitchLimit` ~80°); roll can
  never enter — `ApplyOrientation` is world-up yaw then local-X pitch; vertical move is world up.
⚠ Default start is the mission spawn — RANDOM per launch; pass `--campos`/`--lookat` for comparisons.
⚠ `KeyboardCaptured` zeroes keyboard axes while a text field owns focus — raw key polls bypass GUI focus.

## src/Flight/FlightModel.cs
Velocity-vector arcade flight model: body rates = control torque × reciprocal inertia vs
ang_momentum_damp, scaled per axis (PitchTune/YawTune/RollTune, TUNE, calibrated to stopwatch
timings of the original). Thrust/drag/gravity integrate on the velocity vector (speed passes
through zero); lift cancels gravity's cross-path share; drag is normalized so drag(fd_speed) = max thrust.
⚠ Cruise is Slerp's degenerate case — pathDot > 0.999f branches to a normalized lerp (the
  near-parallel cross-product axis is float noise; Rotated throws). Never revert to a bare Slerp.
⚠ While stalled the nose cannot rise over the horizon: world elevation is capped at max(horizon,
  frame-start elevation), so a slow full-pull loop breaks at the top — original behavior.
⚠ The knife-edge nose sag (KnifeNoseSag/KnifeNoseRate) is a bound approached at a rate cap, both
  measured: a free-falling target never settles, an exponential rewrites stall recovery. It also
  acts at zero bank (knife grows with pure pitch) and shifts the settled path ~1:1 by construction.
⚠ Accepted artifacts, not bugs: loop energy pump, steep-climb equilibrium, stall hang, no flight_ceiling.

## src/Flight/PropAnimator.cs
Spins the flying aircraft's prop/rotor blur discs: Build collects every node PropParts classifies
(local axis + rate), Advance rotates each via RotateObjectLocal so the disc spins in-plane
regardless of parent orientation. FlightController drives it throttle-scaled with a PropIdleSpin
0.4 floor (0 while crashed). --fly only — the static viewer keeps the still disc.

## src/Flight/ControlSurfaceAnimator.cs
Deflects ailerons/elevators/rudders to an absolute pose: each surface stores its build-time local
basis and gets Basis = base · Rot(hingeAxis, angle); three channels slew toward the stick at
SlewPerSec (TUNE), ±20° per kind. --fly only; frozen while paused/crashed, reset on respawn.
⚠ The per-surface sign bakes three flips: the stick convention (ailerons opposite per side, TE
  against the commanded rotation), a canard flip (hinge z < CanardMaxZ ⇒ nose-mounted ⇒ pull
  deflects TE-down), and a frame flip from the accumulated hinge axis vs its canonical plane-space
  direction (canard groups mounted yaw-π). Account for all three before touching any sign.

## src/Flight/WingLightBlinker.cs
Flashes the wingtip flares for FlashDuration 0.08 s (TUNE — the source flash is a single frame,
widened so the blink reads) each WingLights.BlinkPeriod; Reset (respawn) restarts the cycle with
the flares off. Advanced each _Process, frozen while paused or crashed. --fly only.

## src/Flight/PlaneCollider.cs
Derives 5–8 plane-frame collision boxes from the built model's mesh triangles alone (no per-plane
data): region-clipped geometry (tail/wing/fuselage), then greedy volume-guided refinement cutting
one OR two parallel planes per axis (the double cut separates bilateral pairs like twin fins).
⚠ Relabel renames aft outboard boxes `wing` (box wholly one side of the centerline + centre
  outboard of WingBandFrac) so PlaneDamage's localImpact-blind "tail" arm never sees a wingtip
  strike. The half-span is known here — do not side-split in PlaneDamage instead.
⚠ Boxes deliberately overlap; the earliest in Parts order is what gets reported.
⚠ Known limit: the Bloodhawk's canard tips stay uncovered.

## src/Flight/FlightController.cs
The flying-aircraft node: input → FlightModel → transform, roll-following chase camera, text HUD +
telemetry, crash and respawn; drives every HUD widget and animator, and sweeps the PlaneCollider
boxes via CastMotion each physics frame (the old center ray stays as an anti-tunnelling backstop).
⚠ The chase camera slerps its BASIS, never a re-derived hard LookAt — inverted flight renders upside down.
⚠ SurviveHit reads the contact normal at a pose 5 cm past the cast hit — at just-touching the rest
  query finds nothing and the head-on fallback turns shallow grazes into crashes; don't shallow it.
⚠ A dead `critical` part crashes regardless of impact speed; billboard trees are intangible (solid clutter only).
⚠ The crash is data-driven: CrashRuntime plays player_crash_dirt (InheritedWorldVelocity = impact
  velocity × WreckMomentum); Respawn resets it and re-homes CrashRestPoses; null runtime = hide only.
⚠ Firing (needs Loadout + Projectiles): UpdateGuns holds Space/pad-B → the SELECTED group fires at
  its FIRE_RATE from its own ammo (ONE group at a time, no ALL — user-confirmed); UpdateRockets F/pad-A
  → ONE rocket per pull from the next armed pylon (round-robin), FIRE_RATE-gated (1 s). `--fire`/
  `--fire-rockets` auto-hold; `--infinite-ammo`.
⚠ Two selectors (CycleWeaponSelectors, edge-detected, --no-pads-safe): guns G/dpad-L cycles the
  firable groups; hardpoints H/dpad-R cycles ordnance types (stock = one, so a no-op). `--gun-select=N`
  (0-based) seeds the gun group for headless tests; selections survive respawn.
⚠ Rocket pad button A also respawns, but only from the crashed / run-complete screens (early-return
  states this live-flight path never reaches), so the two never collide. RefillWeapons re-arms all on respawn.
⚠ PadDevices null = every connected pad, never pads[0] (phantom devices read idle); UseKeyboard
  gates keys to P1; AllowPause is false in splitscreen — the freeze halts the shared world.
⚠ The stunt/race AllComplete freeze runs BEFORE the crash branch; Respawn never resets a mid-run stunt.

## src/Flight/PlaneDamage.cs
Per-part hit points from vehicle.json destroyable_parts (via PlaneStats). MapStruckPart maps a
struck collider box + plane-local impact to the data part: wing/canard by impact X sign (left =
−X), fuselage fore/aft of z 0 → nose/tail. Apply subtracts, Reset refills on respawn, Summary
feeds the HUD DMG line.
⚠ The "tail" arm ignores localImpact and is correct only because PlaneCollider.Relabel hands it
  no outboard boxes — do not fix tail sidedness here; widening the signature was rejected.
⚠ The tail's `engine` flag (power loss) is deliberately unwired — handling penalties out of scope.

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's HP fraction crosses an injure_anims
entry it shows the torn pdpN panel, hides the healthy skin, and assigns a discrete-puff fire trail
from the emitter pool; def-level player_smoketrail starts the nose smoke/fire pair. UpdateStatic
burns the trails in place at StaticBurnSpeed for the parked damage lab.
⚠ PairHealthySkins pairs torn↔healthy by merged mesh-AABB position, never by name (the _h
  numbering is crossed on three models — docs/formats/gamez.md) and never by node origin (the
  placement is baked into mesh space); unpaired _h skins are never hidden.
⚠ player_fuelleak and the *_damage_green/yellow/red cockpit cycle stay unwired (no cockpit).

## src/Flight/DamageLab.cs
The --viewer damage lab (H toggles): one HP slider per destroyable part with threshold readouts,
plus a mirrored GaugeCluster damage dial; presets (--damage=part:frac) land through the same
ValueChanged path as a hand drag. It drives the SAME DamageVisuals instance flight uses — it never
reimplements visuals, only decides when to rebuild them.
⚠ Reapply's crossed-anim set-diff (TargetAnims) is load-bearing twice: it implements repair
  (re-derives from pristine) and keeps a slider drag from restarting the fires at every pixel.
⚠ Built in EVERY --viewer session (StartHidden without --damage) so H has a receiver; two H
  presses must return a byte-identical frame (SetLabVisible toggles panel + gauge layer together).

## src/Flight/CompassTape.cs
The original's top-centre heading tape rebuilt from the game's own compassticks2/compasstxt
textures: a cylindrical drum seen edge-on — DrumX = center − R·sin(Δ), headings increase LEFT,
cos(Δ) fade (rendering model: docs/formats/hud.md). Metrics are probe-fitted Ref* constants ×
HudMetrics.Scale; Build returns null if a texture is missing; _Process re-anchors on resize.
⚠ The filtering split is deliberate: ticks point-sampled without mips on the tape Control (mips
  crush the tile vertically), labels bilinear on a child LabelLayer — do not unify them.
⚠ TileOverscan/RimGain and the nearest-tick look are TUNE pending user A/B; north = −Z is
  FlightController's one-line assumption (open question in hud.md).

## src/Flight/GaugeCluster.cs
The original's cockpit dials as a screen-space HUD: altimeter, speedometer, damage display, all
geometry extracted from the plane's own gauges subtree (structure/scales/quirks:
docs/formats/hud.md); polys draw by data priority, rest rotations ignored; PartFraction binds
flight or the lab; dial centres are bottom-anchored (FromBottom) so panes keep them on screen.
⚠ Never color-key needle.tif — a black key erases the hub's two black discs; the engine-side slim
  taper is replicated as a load-time alpha mask (Needle*Frac constants, TUNE).
⚠ The face textures hold dark UNLIT copies of the STALL / LOW ALT windows — compare pixel values
  (~58,0,0 unlit vs 180+,0,0 lit) before concluding a warning state is wrong; bitten twice.

## src/UI/LaunchMenu.cs
The in-game launchscreen CanvasLayer: Mode → Chapter → Plane, input polled every frame through
one MenuInput per player (no input-map/focus wiring); joining is gated to the Plane screen, and
with >1 player that screen becomes real SplitScreen.PaneRect panes — pick in the pane you fly in.
⚠ Player 1 is the keyboard + the SET of all unclaimed pads until ClaimP1Pad pins its real pad —
  never pads[0], which re-breaks the phantom-device fix; the leftover set makes hand-off free.
⚠ Re-entrant: ShowMenu resets to Mode, clears locks (joined players survive), and primes input +
  join edges from the CURRENT raw state — a still-held Esc/Start must not read as a fresh press.
⚠ Size from GetViewport().GetVisibleRect() (a CanvasLayer is not a CanvasItem); LayoutScale caps fonts so 4P fits 720p.

## src/UI/MenuInput.cs
One launchscreen player's input source — keyboard flag (player 1 only), `Pads` device array, edge/
auto-repeat state; `Poll(dt)` fills Move/Accept/Back/Start (polled: actions can't read a named device).
⚠ `Pads` is an array, not an int: player 1 holds every unclaimed device. Reads OR the buttons and
  take the max-magnitude axis, so idle phantom devices contribute nothing.
⚠ Raw reads go through `CSVM.Pads.For(Pads)`, never the field: the field is the player's binding
  (join bookkeeping needs it while unfocused), `For` is the focus gate; `JoinPressed` inlines the gate.
⚠ `Prime()` seeds edge flags from raw state — a button held through a screen transition is no press.
⚠ `LastActivePad` excludes Start: the join gesture is not proof somebody owns the pad.

## src/UI/SplitScreen.cs
The splitscreen rig for 2–4 players (1P never constructs it, keeping that path untouched): black
gutter backdrop, one `SubViewport` pane per player sharing the main `World3D`, plus the
`PlayerColor`/`PlayerTag` identity table.
⚠ `PaneRect(index, players, size)` is the ONE pane-layout definition — the launchscreen aircraft
  select uses the identical call, so the menu pane you pick in is exactly the flight pane you get.
⚠ `SubViewport.Msaa3D` does not inherit the project msaa_3d setting (root viewport only) — copy it
  across; `RenderTargetUpdateMode` must be `Always`.
⚠ `PlayerVisualLayer` reserves layers 17–20 (`PlayerLayerBit0` = 16); the world stays on layer 1;
  `PlayerCullMask(i)` adds only that player's bit — a pane sees only its own sky/deck/puffs.

## src/Flight/PlayerRig.cs
One rendered view's state bag: index, camera, optional `SubViewport`, `HudParent`, `VisualLayer`,
the player's FlightController, and private camera-anchored copies (`Horizon`/`Deck`/`Puffs`/
`Whiteout`) — those re-anchor to the view's camera every frame, so N players need N of each.
⚠ Single player holds exactly one rig wrapping the main-viewport camera with `VisualLayer` 0, so
  every loop over the rigs degenerates to the old single-camera code.
⚠ In splitscreen the camera's parent is a `SubViewport`, not a Node3D — local `Position` IS the
  world transform, so per-frame anchoring reads `Camera.Position` directly (correct in both modes).

## src/UI/LiveryLab.cs
The `--viewer` livery editor (key L): squadron stepper (loads the squadron's whole livery via
`LoadSquadronLivery`), per-slot RGB sliders, decal steppers, random livery, copy-CLI-args.
⚠ Single write path: every edit funnels through `Apply()` → `PlaneBuilder.Repaint`, and `--viewer`
  builds the plane BARE — `--viewer --paint=X --screenshot` end-to-end tests the repaint itself.
⚠ Steppers walk `PatternLibrary.PatternsFor` (this aircraft's list), never the vehicle catalog;
  `CliArgs()` resolves the canonical entry by NAME and emits bare `--paint=` only on a verbatim match.
⚠ Starts hidden — an unadorned `--viewer` stays byte-identical; `--debug-livery[=N]` scripts the L key.
⚠ `_suppressCallbacks` guards `SyncWidgets` against slider `ValueChanged` re-entry.

## src/UI/NodeLabels.cs
Floating node-name labels (key T) in both the static viewer and flight, cycling Off → Meshes → All;
`--debug-names[=meshes|all]` presets the mode at launch.
⚠ Names come from the `cs_name` meta SceneBuilder stamps, never `Node.Name` — Godot sanitises and
  renames, so its name is often not greppable in the extraction; nodes without the meta are skipped.
⚠ Labels anchor at the mesh-AABB centre in node-local space, not the node origin — origins sit far
  from the geometry and are shared, which collapsed all labels into a single screen cell.
⚠ The nearest-first grid de-clutter (3×3 neighbourhood) is the readability limiter, not `Radius` (1500 m).
⚠ Own plane deprioritised, not excluded; rescans on a 0.35 s timer; builds nothing until enabled.

## src/UI/MarkerOverlay.cs
The `--viewer` marker overlay (key K): draws every firepoint / pylon / target on the parked
aircraft as a coloured gizmo + billboarded label (firepoints orange, shared-mount firepoints
magenta, pylons cyan, target green); `--markers` opens it at launch. Reuses `MarkerRig.Classify`
+ `GroupCoLocated`, so its gizmos agree with `--dump-markers` by construction.
⚠ Markers come from the built plane tree via the `cs_name` meta, not GameZ — the same source
  `NodeLabels` reads; a co-located pair's labels are stacked up the airframe so both survive.
⚠ Gizmo dots always show (no mount position is ever lost); only the LABELS de-clutter, nearest-
  first with firepoints prioritised over pylons — the full named table stays in `--dump-markers`.
⚠ Builds nothing until first shown, so an unadorned `--viewer` screenshot is byte-identical.

## src/UI/MeshLab.cs
The `--viewer` geometry/shading lab (key M): normal lines, smoothing-seam wireframe, collider
boxes, light sliders + headlight, and independent cull × normal-source override cyclers
(`--debug-mesh=cycle=N` scripts them; lighting is only written when `--debug-mesh` asks).
⚠ Built after the plane joins the tree — `GlobalTransform` on a detached node is identity + error spam.
⚠ Override materials replicate SceneBuilder's vertex stage verbatim (`skip_vertex_transform`,
  `depth_bias`/`node_bias`) — otherwise coplanar decals z-fight and every A/B is worthless.
⚠ Bounds-check override slots against the INSTANCE (`GetSurfaceOverrideMaterialCount()`), not the
  mesh — `SmoothMesh` refuses 0-surface meshes; `SetOverride` recovers by re-assigning the mesh.

## src/UI/OrbitCamera.cs
The static inspection view's orbit-camera controller (LMB-drag orbit, wheel zoom, AABB framing):
owns the orbit state and drives a `Camera3D` it does not own; `Frame` honours `--campos`/`--lookat`,
and `MergedAabb(Node3D)` merges a subtree's world-space mesh AABBs (shared with the anim lab).
⚠ The FOV read in `Frame` is 50 — the orbit view never runs in `--fly`/`--freecam`, where FOV is 62.
⚠ `MergedAabb` on a meshless subtree returns a zero-size box at the origin — callers special-case
  it — and the nodes must be IN the tree (`GlobalTransform` on a detached node = identity + errors).

## src/UI/AnimLab.cs
The `--anim-lab` debugger: a quiet `WorldSession` stage (`AutoStart=false`, seed pinned), fixed-dt
clock, transport button panel, def picker, `AnimTimeline`, `SpectatorCamera` freecam with
click-to-follow, and a staged effect/crash anchor set so placeless on-call defs play at the camera.
⚠ The clock is a fixed-dt accumulator (`FixedDt` 1/60, clamped 0.25 s); a scripted run adds exactly
  `FixedDt` × speed per frame, ignoring wall time — captures land on exact step counts.
⚠ The clock hand-off is `AnimRuntime.ManualAdvance`, NOT `SetProcess(false)` (READY auto-enable trap).
⚠ Ordering: `Play` sets the timeline focus BEFORE `AnimRuntime.Play` (t=0 events dispatch
  synchronously); `Step` bumps `_steps` before `Advance` so the playhead equals the runner's clock.
⚠ `ShowUi` gates all UI — hidden in `--screenshot` runs; `--debug-anim-ui` forces it back on.
⚠ The stage parks `StageAnchorDist` (55 m) ahead on each fresh Play — Restart reuses the snapshot —
  and passes as `fallbackAnchor`; framed/followed only when the def actually resolved onto it.
⚠ Determinism boundary: puffer spread is unseeded RNG — same-step shots differ in particle noise.

## src/UI/AnimTimeline.cs
The anim lab's authored-vs-fired timeline (custom-drawn `Control`): authored blocks above, fired
ticks below, one lane per Initial sequence; a slanted first-firing connector = scheduler divergence.
⚠ `BuildLane` re-derives the documented scheduling rule independently — deliberately NOT via the
  runtime's `SequenceRunner`. That independence is the whole instrument; don't "simplify" it away.
⚠ `StaticDuration` mirrors `Dispatch`'s duration out-param so authored block widths stay honest.
⚠ Scope: one instance of the played def (first anchor) plus its CALL_ANIMATION children; same-name
  siblings and post-ambient children are untracked; ticks cap at 600/lane, Restart clears.
⚠ `MouseFilter = Ignore`, so orbit-dragging over the strip still reaches the camera.

## src/SessionPaths.cs
Static resolver for the extracted-data paths (`ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr`) under a data root, plus `PreferUnzipped` (an unpacked sibling dir beats its `.zip`).
⚠ Pure path arithmetic — the only I/O is `PreferUnzipped`'s directory-exists probe.
⚠ The `--gamez=`/`--textures=` override policy deliberately stays in PlaneViewer; this class only
  builds the default extraction-tree paths.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram` — the world+anim half of a session build;
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights.
⚠ Stops before the per-view steps (unplaced-entity watch, edge extender, horizon, weather — those
  stay in PlaneViewer) and does NOT add `Root` to the tree; effects go under `Options.EffectsParent`.
⚠ Disposal contract: `textures`/`sounds` stay the caller's `using` locals — nulls `PufferFactory` +
  the sound loader after bootstrap (prewarming first) unless `Options.KeepArchivesOpen` (the lab).
⚠ Returning `Program` keeps crash-effect-param loading in the caller — no Mech3→Flight dep here.
⚠ `PlayerPosition` is a single per-call delegate — no camera exists at build time.

## src/PlaneViewer.cs
Main.tscn root: parses args, registers shader globals + lighting + the persistent camera once in
`_Ready`, then launchscreen or `StartSession()` — menu and CLI share one session-build path.
⚠ `GlobalShaderParameterAdd` runs in `_Ready` ONCE; `SetupWeather` only `Set`s — the in-process
  world rebuild must never double-Add (that errors).
⚠ `ReturnToMenu` QueueFrees `_worldRoot` and nulls every cached session ref, so `_Process`
  null-guards cover the frame before the deferred free lands.
⚠ `LoadWeather` precedes the per-rig horizon loop; everything reads `_activeZone` (assigned
  unconditionally per load) — dome + fog always share a zone, rebuilds never inherit a stale one.
⚠ Per-rig: the skydome is REBUILT per rig (star mesh `csky_light_fade` instance uniform); the cloud
  deck is duplicated + `CopyInstanceShaderParams` — `Node.Duplicate()` drops instance shader params.
⚠ Focus mute is the master-bus mute on purpose; `MixGain = 0` is the wrong mechanism — WorldSounds
  has no gain plumbing and one-shots bypass `MixGain`, so most audio would stay audible.
⚠ `RunDamageTest` (`--damage-test[=name]`, freecam) is the headless destructible harness: continuous
  HP sweep (C22 stages) or, with `--damage-hd=N`, discrete N-`HEALTH_DAMAGE` hits via `DamageAt`
  (C23/C24/C25/C26) — resolve✓ walk-up, healthy/destroyed swap, `col[off,on]` (colliders switched by
  the kill), and `debris[N]` (ballistic pieces the death launched). It forces `Collision = _fly ||
  _damageTest` so colliders EXIST; `--freecam` alone builds none. Discrete mode covers EVERY
  destructible (doors instant-die, no `DAMAGE_SEQUENCE`), continuous mode only the staged ones.
⚠ Discrete mode adds the world subtree to the tree (`ManualAdvance` so `_Process` doesn't
  double-drive) and `Advance`s the death ~3.5 s AFTER the swap/col census: the debris `OBJECT_MOTION`
  is scheduled (t≈2.2 s), so it needs the clock ticked — and ticking an out-of-tree world spams
  `!is_inside_tree` (global-transform reads). Swap/col are measured pre-tick (immediate post-death),
  debris post-tick; being in-tree also makes positions real (no more C23 (0,0,0) trap).
