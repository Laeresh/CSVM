# Data-driven crash — generic animation handlers + the crash plays its def

**🟢 LIVE — written 2026-07-23.** Handoff plan from the session that landed polish-4 item 8
slices 1–2 (the bespoke crash choreography). Scope decided **with the user**: make the crash
**fully data-driven** — the player crash def (`player_crash_dirt`) runs through a real
`AnimRuntime`, with **generic motion/opacity handlers** doing the work, retiring the hand-written
`CrashChoreography` / hand-rolled debris / `CrashBreakup` scatter. This is also the **Milestone 3
foundation**: every weapon-hit destruction and kill-chain in the game uses these same handlers.

**This supersedes the open half of `docs/PLAN-M2-polish-4.md` item 8.** When this plan's Layer 2
lands, item 8 closes (the dirt crash is complete, data-driven). Until then item 8 stays ◐.

---

## Why this shape (the evidence, so it is not re-measured)

The user's instinct — "are these animation types used elsewhere? I'd like generic handlers called
by the crash handler; as data-driven as possible" — was measured and strongly confirmed.

**Census across ALL chapters' compiled `cam_anim` + `mis_anim` defs** (2026-07-23):

| Event kind | Events | Handler today |
|---|---|---|
| **`ObjectOpacityFromTo`** | **9,917** in 1,693 files | ❌ **none** (no `case` in the dispatch switch) |
| `ObjectMotion` · `translation` block | 1,233 | ❌ counted `ObjectMotion(ballistic)`, not simulated |
| `ObjectMotion` · `scale` block | 4,651 | ❌ counted, not simulated |
| `ObjectMotion` · `translation_range` | 2,616 | ❌ counted (the debris arcs hand-rolled this) |
| `ObjectScaleState` (instant) | 2,209 | ✅ `PoseScale` |
| `ObjectMotionFromTo` | 9,421 | ✅ `FromToMotion` (incl. a `scale` channel) |
| `ObjectMotion` · `xyz_rotation` | 3,635 | ✅ `SpinMotion` |
| `ObjectMotion` · `forward_rotation` | 3,165 | ⚠ partial (spin only) |

**Reachability — activation of the defs that CONTAIN these** (the honest caveat):

- `ObjectOpacityFromTo` defs: **1,611 OnCall, 81 WeaponHit, 1 OnStartup**.
- `translation_range` defs: **216 OnCall, 184 WeaponHit**.

So these pervasive types are **trigger-gated, not ambient** — they fire when a mission script, a
weapon hit, or a **crash** calls them, not on their own in free flight. Two consequences:

1. **The crash is the one big reachable trigger today**, so generic handlers make it data-driven now.
2. **This is exactly what M3 needs** — every `WeaponHit` destruction reuses these handlers with no
   rework. Building them here is not crash-only spend.

Reproduce the census:
```
for pat in '"ObjectOpacityFromTo"' '"ObjectMotion"'; do
  grep -rho "$pat" extracted/*/cam_anim extracted/*/*/mis_anim | wc -l; done
# sub-blocks: grep -rho '"scale": {' extracted/*/cam_anim extracted/*/*/mis_anim | wc -l
# reachability: for f in $(grep -rl '"ObjectOpacityFromTo"' extracted/*/cam_anim extracted/*/*/mis_anim);
#   do grep -m1 '"activation"' "$f"; done | sed -E 's/.*"activation": "?([A-Za-z]+)"?.*/\1/' | sort | uniq -c
```

---

## What already landed this session (keep vs. supersede)

Committed on `main` (`d088a2a` slice 1, `5f14eca` slice 2):

- **KEEP — reusable by this plan:**
  - `PufferState.FromAnimEvent` now reads the `interval_type: "Distance"` case into
    `DistanceInterval` (`Puffer.cs`). ⚠ inverted flag: `has_interval_value` is *false* yet
    `interval_value` holds the distance — key off `interval_type`. **Proven a no-op for world
    puffers**: `AnimRuntime` only *sustains* puffers (`SustainAt` ignores `DistanceInterval`; pool
    sizing checks `sustained` first). Layer 2's `call_crash_trails` puffers ride this path already.
  - The whole `Puffer` trail/sustain/burst machinery (`TrailAdvance`/`SustainAt`/`Burst`) and the
    `blend`/`softParticles` `Puffer.Create` overrides.
  - `FlightAudio.OnGroundExplosion` (`snd_exp_ground_a`) — a Sound event the crash def carries; can
    stay as the audio path, or be driven by the def's `Sound` event through the runtime.

- **SUPERSEDE — Layer 2 replaces these with data-driven playback:**
  - `CrashChoreography.cs` — the bespoke timeline (sparks/fireball-cluster/smokeball cues + the
    hand-rolled debris ballistic). Its **numbers are transcribed**, which is exactly what
    data-driven playback removes. Delete once Layer 2 plays the def.
  - `CrashBreakup.cs` — the generic wreck-piece scatter (random velocity + up-kick). The crash def's
    `piece1seq..piece4seq` carry the **authored** piece velocities + bounces; the ballistic
    `ObjectMotion` handler (Layer 1) drives them instead.
  - `FlightController.Crash`'s direct `CrashEffect?.Burst` / `Choreography?.Begin` / `Breakup?.Begin`
    calls — replaced by "trigger the crash def on this player's crash `AnimRuntime`".

**Do not delete the superseded code until Layer 2 is verified end-to-end** — it is the working
fallback and the visual reference for "did the data-driven version reproduce it".

---

## Architecture

Two layers. Layer 1 is unambiguous and reusable; Layer 2 is the rewire.

### Layer 1 — generic handlers in `AnimRuntime` (the reusable core)

All live in `CSVM/src/Mech3/AnimRuntime.cs`. The motion infrastructure to reuse:
`interface IAnimMotion { Node3D Target; bool Finished; void Tick(float dt); void Seek(float t); }`
(existing impls: `ScriptPlayback` ~:1301, `SpinMotion` ~:1348, `FromToMotion` ~:1438), added via
`AddMotion`, ticked in `TickMotions` (~:1276), rest pose via `RestOf(target)`.

**1a. `ObjectOpacityFromTo`** — the biggest gap (9,917 events, zero handler).
- **No `case` exists** in the `Dispatch` switch — add one next to `ObjectOpacityState` (:403).
- Schema (from flydirt `hide_dust`): `{ name, opacity_from: {opacity, state}, opacity_to: {opacity,
  state}, run_time, opacity_delta }`, with an event `start` offset (e.g. `{Animation, 1.9}`).
- Implement as an `IAnimMotion` (`OpacityFade`) that lerps `opacity_from.opacity → opacity_to.opacity`
  over `run_time`, calling the **existing** `SetSubtreeOpacity(target, alpha)` (:2013) →
  `ApplyOpacity` (:2023) which sets `SceneBuilder.OpacityParam` (`csky_opacity`) per instance.
  `instant`/`run_time<=0` → `Seek(run_time)` (land on the end), same as `FromToMotion`.
- **⚠ First step: confirm the schema is consistent across a sample of the 9,917 events** (I was
  about to when this session ended). flydirt is one instance; sample ~10 others across chapters to
  be sure `opacity_from/opacity_to` are always `{opacity, state}` and `opacity_delta` is the
  relative alt form (mirrors `FromToMotion`'s `*_delta` — likely also unreachable/bare; check).
- **⚠ Opacity risk:** the fade only shows if the *target mesh's shader reads `csky_opacity`* —
  `HasOpacityPath` (:2048) tests for `SceneBuilder.OpacityTerm` (`" * csky_opacity"`), which only
  alpha-writing variants carry (item 10 Part B). If the built `dust` mesh is opaque, either force
  its material onto the alpha/opacity path in `SceneBuilder`, or fall back to fading a per-instance
  `MeshInstance3D` modulate/color-alpha. **Decide this while building the dust mesh (Layer 2).**

**1b. `ObjectMotion` ballistic/scale/tumble** — the `translation` / `translation_range` / `scale` /
`forward_rotation` blocks the current handler (:424-475) only *counts*.
- Current handler does `xyz_rotation` → `SpinMotion` and counts everything else as
  `ObjectMotion(ballistic)`. Extend it to build a new `MotionRuntime` `IAnimMotion` when any of
  `translation` / `translation_range` / `scale` / `forward_rotation` is present.
- Full schema (from `player_crash_dirt` piece1seq + `carnage_trails` + flydirt):
  ```
  { node, gravity: {value(-9.8), complex, no_altitude, do_intersections}|null,
    translation: {initial:{x,y,z}, delta:{x,y,z}, rnd_xz:{x,y,z}}|null,     // initial = VELOCITY
    translation_range: {xz:{min,max}, y:{min,max}, initial:{min,max}, delta:{min,max}}|null,
    forward_rotation: {Time: {initial(rate rad/s), delta}}|null,            // tumble
    xyz_rotation: {initial, delta}|null,                                    // steady spin (SpinMotion)
    scale: {initial:{x,y,z}, delta:{x,y,z}}|null,                           // ramp over run_time
    bounce_sequence: {default, water, lava}|null, bounce_sound, run_time }
  ```
- **Semantics, established this session:**
  - `translation.initial` = **initial velocity** (piece1 launches up at y=10 m/s), `rnd_xz` = random
    spread added to it, `gravity.value` accelerates (negative). Integrate ballistically over
    `run_time` (`CrashBreakup.Advance` is the reference integrator: `vel += down*g*dt; pos += vel*dt`
    with a short down-ray ground-rest).
  - `translation_range` = **ranged ballistic launch** (the debris form). ⚠ **undocumented and
    AnimRuntime never simulated it** — the reading `xz`/`y` = horizontal/vertical *distance travelled
    over run_time*, launched in a random azimuth, is a **reasoned interpretation (TUNE), not a
    decode**; `initial`/`delta` left unmapped. `CrashChoreography.StartDebris` (this session) has the
    exact math to port: `vHoriz = xz/run_time`, `vVert = y/run_time − 0.5·g·run_time`.
  - `forward_rotation.Time.initial` = tumble **rate in rad/s** (piece1 = 15.708 = 900°/s) — spin
    about a local axis while it flies. (`delta` unmapped; 0 everywhere reachable.)
  - `scale: {initial, delta}` = start scale + per-`run_time` delta, **linear ramp** (flydirt dust:
    initial (3.5,10,3.5), delta (−1,−5,−1) over 6 s). Reuse `PoseScale` semantics for the write.
  - `bounce_sequence.default` = a sequence name to `CallSequence` **on ground contact** (needs
    `do_intersections` + a down-ray like `CrashBreakup`); the piece defs use it to play
    `ground_mixed_exp_sg` and re-launch (`pNhit`). Ground-contact detection: reuse `CrashBreakup`'s
    `PhysicsDirectSpaceState3D.IntersectRay` idea. **`bounce_sequence` can be a Layer-1.5 follow-up**
    — the pieces still read fine tumbling to rest without the bounce re-launch.
- **⚠ Frame:** `translation`/`scale` are in the node's own/parent frame (same convention as
  `FromToMotion`, which is "absolute in parent frame"). Seed from `RestOf` / the live transform like
  the other motions. Do NOT double-add to the world position (the 7-km-off-map trap FromToMotion's
  docstring records).
- **⚠ Idempotence:** these sit under `Loop`/`CallSequence`; a re-assertion must not restart a live
  motion (see `SpinMotion.Matches`). For one-shot ballistic (run_time-bounded) this matters less, but
  keep the `_motions.Any(...)` guard pattern.

### Layer 2 — the crash plays its def

Make the crash a data-driven `AnimRuntime` playback instead of the bespoke driver.

**2a. Build the crash subtree per player.** The def targets, and must resolve by name:
- **Player crash nodes:** `healthy` (= the plane model), `destroyed`, `dontmove`, `markers`,
  `piece1..4`, `shadow`, `cockpit1`. `PlaneBuilder.BuildDestroyed` already builds the `destroyed`
  subtree (`healthy` is the plane model itself). Confirm the built node NAMES match the def's
  (`cs_name`), and that `piece1..4` are reachable as scene nodes.
- **Effect templates** (world-gamez effect ROOTS that `WorldBuilder` deliberately skips): build them
  with `SceneBuilder.BuildSubtree` from the world `gamez` (both `gamez` and `textures` are in scope
  through PlaneViewer's whole `try`, incl. the `_fly` build loop ~:1074). Needed roots:
  - `carnage_trails` → `fly_trail1..5` (meshless anchor nodes the `spurtpuffer` trails ride).
  - `flydirt` → `flydirt` (meshless, `model_index -1`, sinks) + `dust` (`model_index 131`,
    scales+fades). World gamez nodes: C1 `nodes.json` lines 46813 / 46899.
  - Puffer effect roots are attached via `at_node` to `healthy` (the fireball/spark/smokeball calls
    pass `AtNode: {node:"healthy"}`), so their *host* is the player node — no separate root build for
    those, but `HandlePufferState` + `PufferFactory` must be wired on the crash runtime.
- Build **once**, or per-player (splitscreen: 2–4 can crash at once — each needs its own instances).
  Mirror the current per-player emitter parenting (`CrashChoreography.Emitters` → `AddChild`).

**2b. Bind a crash `AnimRuntime`.**
- Create a per-player (or per-crash-subtree) `AnimRuntime`, set `PufferFactory` (the session
  `TextureArchive` factory — see `MakePuffer`/the world wiring at PlaneViewer :644-679), then
  `Bind(crashSubtreeRoot, crashProgram)`. `crashProgram` = an `AnimProgram` holding the crash def +
  the effect defs (`EffectSet.Load` already loads these; the program is `animProgram` at
  PlaneViewer :634, which contains them). **Bootstrap is light** for a crash program — the crash
  defs are `OnCall`, so no ON_STARTUP/start-anim work fires at bind; only reset-state pass matters
  (it sets dust scale/opacity, piece rest, `destroyed` inactive — which is correct).
- **⚠ Reset states:** the crash defs' `reset_state` (dust scale (1,0.1,1)+opacity 0.3, `destroyed`
  off, `fly_trailN` on) must run at bind so the templates start hidden/posed, and re-run on respawn.

**2c. Trigger on crash.** `FlightController.Crash` calls the crash runtime to `Start` /
`CallAnimation("player_crash_dirt")` on the player anchor (surface-selected: `player_crash_dirt` for
Ground; `_default`/`_water` stay unreachable — see item 8 traps). Respawn re-applies the reset
states and stops live motions/puffers. Remove the direct `CrashEffect`/`Choreography`/`Breakup`
calls once verified.

**2d. Retire the bespoke code** (`CrashChoreography.cs`, `CrashBreakup.cs`, the debris/piece
hand-code) after the data-driven path reproduces slices 1–2 at least as well. Keep
`FlightController.ClassifySurface` and the surface enum.

---

## Build order

- **Wave 1 — Layer 1 handlers (no crash wiring yet).** 1a `ObjectOpacityFromTo`, then 1b
  `ObjectMotion` ballistic/scale/tumble. Both are additive to `AnimRuntime` and cannot regress the
  world (all usages are trigger-gated; nothing ambient fires them — the 8-chapter world regression
  should be **byte-identical**, which is itself the safety proof). `bounce_sequence` can defer.
- **Wave 2 — Layer 2 scaffolding.** Build the crash subtree + effect templates; bind a crash
  `AnimRuntime`; run reset states. Verify the templates build in all 8 chapters (node/mesh counts,
  zero errors), still no trigger.
- **Wave 3 — trigger + verify end-to-end.** `Crash` triggers the def. Verify each effect appears
  (a forward-dive crash screenshot burst), diffing against slices 1–2's known-good output. Resolve
  the opacity risk here (dust fade visible or fall back).
- **Wave 4 — retire the bespoke code**, update docs, close item 8.

---

## Verification

- **Layer 1 alone:** an 8-chapter `--freecam`/`--fly` world regression must be **structurally
  identical** (same node/mesh/motion counts, zero errors) — because nothing reachable triggers these
  handlers, adding them changes nothing ambient. That IS the proof they are safe. (`docs/verification.md`
  rule 5: an unchanged number is only evidence if you have seen it able to change — so also add a
  targeted trigger, i.e. the crash, to see them fire.)
- **End-to-end (Wave 3):** forward-dive crash into C1 terrain with `--spawn-at`/`--spawn-dir`,
  **no `--hold`** so the crash freezes (auto-respawn only fires for HoldSegments runs) — capture a
  `--shots` burst. Reference: `.scratch/obl_06.png` from this session (sparks + fireball cluster +
  wreck pieces + debris streaks) is the bar to match. Confirm `--debug-anim` shows the crash def
  driving and each effect def resolving its nodes.
- **Opacity:** log `HasOpacityPath(dust)` when built; if false, the fade needs the color-alpha
  fallback — the smoke-vs-fireball confusion from slice 1 (`docs/verification.md` rule 24) applies:
  isolate the dust to confirm the fade, do not eyeball it under the fireball.

## Traps

- **`translation_range` is a reading, not a decode.** The anchor (`fly_trailN`) is invisible; only
  its fire trail shows, so only the arc's rough scale reads — it is TUNE, not fidelity. Do not
  present the trajectory as authoritative. `initial`/`delta` are unmapped.
- **The opacity/motion handlers must not double-apply to world position.** Seed from the live/rest
  transform in the parent frame (FromToMotion's 7-km-off-map lesson).
- **Effect roots are unbuilt by design.** `WorldBuilder` skips them (like `fire1.flt` in backlog).
  Building them for the crash must not make them appear in normal world rendering — build them into
  the *crash* subtree, hidden until triggered (reset states handle this).
- **Splitscreen:** per-player crash subtrees + runtimes, or the effects collide. The current
  per-player emitter model is the pattern.
- **Keep the bespoke code until the data-driven path is verified** — it is the visual reference.
- **`plane_destroy_sg` audio is already done** (`FlightAudio` snd_exp_plane1..4); the def's `Sound
  snd_exp_ground_a` is `OnGroundExplosion`. Do not double-play if the def's `Sound` events start
  driving audio through the runtime — pick one path (`docs/PLAN-M2-polish-4.md` item 8 trap).
- **Air/water variants stay unreachable** (air = mid-air destruct = M3; water = a sea-surface signal
  the collision system does not expose). This plan completes the **dirt** crash only.

## Key locations (as of 2026-07-23)

- `AnimRuntime.cs`: `Dispatch` switch (`ObjectMotion` :424, `ObjectScaleState` :380,
  `ObjectOpacityState` :403, `ObjectMotionFromTo` :385, `CallAnimation`/`CallSequence` :507-515);
  motions `ScriptPlayback` :1301 / `SpinMotion` :1348 / `FromToMotion` :1438; `AddMotion`/`TickMotions`
  :1276; `SetSubtreeOpacity` :2013 / `ApplyOpacity` :2023 / `HasOpacityPath` :2048; `PoseScale` :1985;
  `Bind`/`Bootstrap` :77/:121; `Start` :277; `ResolveOne` :963; `HandlePufferState` :601.
- `PlaneViewer.cs`: world gamez `gamez` :539 + `textures` :540 (in scope through the whole `try`);
  world `AnimRuntime` wiring :633-695; per-player crash-effect build ~:1057-1084; `MakePuffer`
  helper (call shape) ~:1042-1060.
- `SceneBuilder.cs`: `public SceneBuilder(GameZ, TextureArchive, bool fullbright=…)` :122;
  `BuildSubtree(GameZNode, …)` :145; `OpacityParam`/`OpacityTerm` :272-274.
- `PlaneBuilder.cs`: `BuildDestroyed(rootName)` :133 (builds the wreck subtree).
- Data: `extracted/<ch>/cam_anim/player-player_crash_dirt.json` (the def);
  `carnage_trails-call_crash_trails.json`, `flydirt-flydirt_plane.json`,
  `flame_ball_01-large_fireball.json`, `black_smoke_ball_01-large_black_smokeball.json`,
  `yellow_spark_01-small_yellow_sparks.json`, `fire_here-large_10sec_fire.json`. World gamez
  `flydirt`/`dust` nodes: `extracted/C1/gamez/nodes.json` :46813 / :46899.
