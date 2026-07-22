# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. **No plan is active**;
completed plans are in `docs/plans/`. Per-item history/diagnosis
detail is in `docs/HISTORY.md` (dated entries) and `docs/architecture.md` (module bullets); how to
verify a change without fooling yourself is `docs/verification.md`. **The live list of hand-tuned
constants awaiting playtest lives here** (see "TUNE constants pending playtest" below) — it moved
out of CLAUDE.md on 2026-07-22, since CLAUDE.md's status section is current-state-and-next-step
only. When an item gets scheduled into a plan, move it there; when it lands, delete it here.

## Blocked / deferred

- ~~**Animated world vehicles**~~ — **LANDED 2026-07-21** (`docs/plans/PLAN-anim-playback.md`, revival-plan
  item 7): the C1 train drives its SI-script track loop, the road vehicles run their
  `OBJECT_MOTION_FROM_TO` chains and the hangar doors swing, via the generic `AnimRuntime`.
  `PufferState` landed the same day (the train's steam plume, waterfall mist — user-confirmed
  in-game), including two follow-up bugs found and fixed the same day: a reader-def dedupe gap
  that let a duplicate `waterfall01` instance re-kill the splash puffers every frame, and the
  `AT_NODE` spread offset being parsed nowhere (silently dropped on 862 of 4387 PUFFER_STATE
  events install-wide) — the mist sat on one point instead of spreading across the falls until
  fixed. **Everything else this entry originally listed (the remaining event kinds, `If`/
  `Elseif` evaluation, mission-spawned entity rosters, `texture_scroll`) is now scheduled in
  `docs/plans/PLAN-anim-rendering-followups.md`** (2026-07-21, 4 independent session-sized items with
  goal/evidence/approach/verify each) — see that plan rather than this entry for current detail.
- **Burning-object fires (`fire1`/`fire2` templates + `EFFECTS` flipbooks)** — **POSTPONED
  2026-07-21 by user decision: minor detail, and the trigger is not findable.** Fully decoded,
  so nothing needs re-deriving; what is missing is *when* to start a fire, not how. Blocked on
  a decision, not on data. Decode in `docs/formats/anim-definitions.md` ("Fire: templates,
  flipbooks, and a trigger that lives in the exe"):
  - **Templates:** `fire1`/`fire2` are real single-poly `Facade`/`CylindricalY` meshes under the
    **parentless roots** `fire1.flt`/`fire2.flt` (C1 nodes 493–496), which `WorldBuilder` never
    builds (it builds only World children + partition-referenced subtrees). Same for the other
    effect roots (`large_firetrail`, `short_firetrail`, `lg_fireball`, … ~gamez idx 74–150).
  - **Flipbook:** `effects.zrd.json` gives `fire1` 12 maps @ 10 fps, `fire2` 6 @ 5 fps, resolved
    **by filename from the texture archive** — `textures.json` registers only `fire101`/`fire102`
    while `extracted/<ch>/texture/` ships all twelve `fire1NN.png`. `TextureCycler` already plays
    frame lists, so this is small *once the templates are built*.
  - **EFFECTS is node-keyed, not texture-keyed** (user-confirmed: a *sustained* muzzle flash never
    changes texture, always `fire101`). So `flame01` — the refinery gas flare, sharing material 88
    with the `fire1` template — is a **static base flame**, and the animated fire the user sees
    there is a **placed `fire2` instance** (6 frames @ 5 fps, matching their independent read).
  - **Why it is blocked:** the four `fire.zrd.json` behaviours (`timed_big_fire`,
    `persistent_big_fire`, `persistent_small_fire`, `timed_small_fire`, all anchored on
    `fire2.flt`) are called by **nothing** — their names appear in exactly one file, their own,
    and `CALL_ANIMATION` references animations by name string only (no index form exists anywhere
    in this data). **User searched the disassembly 2026-07-21 and found no trigger either.** So
    the original starts them engine-side by a condition we cannot recover; reproducing them means
    inventing our own trigger, which is a fidelity guess rather than a data-driven port.
  - **If resumed:** the placement half already works — `CALL_ANIMATION`'s target parameter landed
    2026-07-21 and is the mechanism that puts a template at a site. Build the template pool first.
- **mech3ax upstream PR** (cosmetic): planes.zbd round-trip differs by 72 bytes — swapped
  `\0`/`.` garbage past the null terminator in fixed-width texture-name fields. Semantically
  lossless; folded into `docs/plans/PLAN-mech3ax-cs-revival.md` item 12 (stretch goal, same code
  the gamez revival is already touching) / item 14 (its own small upstream PR if not already
  folded into the gamez PR).
- **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

- ~~**Degenerate zeppelin node transforms**~~ — **FIXED 2026-07-22** (polish-3 item 9). The cause
  was `SiScript.SplineInterp` being parsed and then read by nothing, so the 15 scripts that set
  `spline_interp: false` had their *uninitialised* spline coefficient blocks evaluated as cubics.
  C1/M04's `piratezep.zan` decodes a scale constant term of `(0.0, 4.259e27, 4.611e27)` — a
  singular basis that propagates down the whole zeppelin chain. Neither of this entry's two
  guessed candidates was right, and neither was the plan's (`ScriptPlayback` compounding scale).
  See `docs/HISTORY.md` 2026-07-22 and the `CompiledAnim.cs` bullet in `docs/architecture.md`.

- **`SpinMotion` re-seeds its rest pose from an already-spun pose (found 2026-07-22, deliberately
  not fixed).** `SpinMotion` captures `_rest = target.Transform.Basis` from the CURRENT pose at
  construction, and the idempotence guard in `Dispatch` matches only on identical
  `(rate, runTime)`. `zeppelin_rocksleft` fires five events with five different rate/runtime pairs
  at the same `rock_zeppelin`, so each replacement motion anchors to wherever the previous one
  left the node, and a looping call drifts. It is **bounded** — rotation is orthonormal, so this
  can never produce the 1e27 blowup it was originally suspected of — but the drift is real.
  **Not fixed because both candidate fixes risk a visible regression to cure an invisible one,
  and the data does not adjudicate:** (a) seeding from `RestOf` would discard a deliberately-posed
  starting orientation on all 590 spins in the install — C1/M05's `random_prop` poses `propstill`
  to a random angle *before* spinning it, and that pattern would break; (b) inheriting the
  previous motion's `_rest` assumes the five rock events oscillate about a fixed pose, but a
  chained eased rock (accelerate, decelerate, reverse) is at least as plausible a reading, and
  under (b) each event would snap back to rest. **Needs the original game**: watch a zeppelin rock
  through several loops and see whether it returns to the same attitude or walks. Same class of
  call as `MissionSetup`'s unguessed `Object3DRotate` angle unit.

- **Animation event kinds that need weapons or cutscenes — `CALLBACK`, `OBJECT_CYCLE_TEXTURE`,
  one-shot `SOUND`** (triaged 2026-07-22, the last of `docs/plans/PLAN-anim-rendering-followups.md`
  item 2 after `OBJECT_MOTION` landed). All three still dispatch at bootstrap, so the counts in
  the "not yet acted on" report look like open work — **they are not**. Each was probed at the
  dispatch site across C1/C3/C4/C5 (def, anchor, resolved target count, payload), and each fails
  for a concrete reason rather than a suspicion. Implementing any of them today is a provable
  no-op, the same verdict `OBJECT_ADD_CHILD` got:

  | Kind | Count | Why it cannot do anything |
  |---|---|---|
  | `Callback` | ×8 every chapter | Every dispatch is `def=camera1`, **unanchored**, values 1/2/10/11/14/20/913/914 — engine notifications for the intro **cutscene** camera. This project has no cutscenes, and a callback's whole purpose is to notify mission logic that does not exist here. |
  | `ObjectCycleTexture` | ×1–2 per chapter | Every dispatch is `node=taildamage` with **`targets=0`** — the node never resolves, so there is nothing to cycle. The one real use of this mechanism (the cockpit damage-indicator hilite) is already a build-time material swap in `GaugeCluster.cs`. |
  | one-shot `Sound` | ×1, C3 only | `def=spew node=snd_waterfall`, **`targets=0`** — it names a sound *definition*, not a node. That waterfall already sounds through `SOUND_NODE`. The family at large is 4,378 `OnCall` + 1,650 `WeaponHit` combat audio (21 names are `DYNAMIC_WEIGHTS` groups needing a further decode), which needs weapons this project does not have. |

  **Pick these up when the thing they depend on exists** — weapons for `SOUND`, a cutscene player
  for `Callback` — not before. `ObjectCycleTexture` needs neither; it needs a mission that
  actually builds a `taildamage` node, which none of the ones this project defaults to do.
  The one kind from that list that *was* reachable, `OBJECT_OPACITY_STATE`, is scheduled work and
  stays in the plan, not here.

- ~~**World renders into only the upper-left quadrant at the world origin**~~ — **NOT A BUG,
  closed 2026-07-22.** It is exact projective geometry. C1's World area is
  `left=-12288, top=-12288, right=0, bottom=0`: the world origin *is* the map's corner, and all
  terrain lies at x ≤ 0, z ≤ 0. At `--campos=0,30,420 --lookat=0,0,0`, `FrameCamera` derives yaw 0
  (`PlaneViewer.cs:1751`), so camera-right is exactly world +X. The plane x=0 contains the camera
  and therefore projects to the exact vertical centre line; the line (t,0,0) passes through the
  lookat target parallel to camera-right and projects to the exact horizontal centre line. Terrain
  fills the upper-left quadrant with two hard half-viewport edges — no clipping involved. The
  splitscreen theory is dead too: `PlaneViewer.cs:1123-1128` early-returns for 1P and never builds
  a rig, `SplitScreen` clamps to 2–4 (`SplitScreen.cs:104-110`), and there is no other
  `SubViewport` in `CSVM/src`. The surrounding void is unfilled because `MapEdgeExtender` is
  deliberately off in plain `--viewer` (`PlaneViewer.cs:629`, "honest data view").

- **We ignore `zone_id` entirely** (found 2026-07-22). Every gamez node carries a `zone_id`:
  `-1` = always rendered, `1`/`2`/`3` = only when that zone is active. Both zones span the **whole
  map** spatially, so they are alternative world variants, not regions. Per-chapter node counts
  (`-1` / zone1 / zone2 / zone3): C1 3529/2666/869/—, C1B 3500/2101/2/—, C1C 4181/146/1317/—,
  C2 4189/766/1/—, C2B 3338/149/1414/—, C3 3759/1647/2/—, C4 5330/802/2157/—, C5 9734/1555/—/149.
  So C1B, C2 and C3 are effectively single-zone (1–2 nodes in the second); C1C, C2B and C4 are
  zone2-dominant; C1 and C5 zone1-dominant. **Nothing in `CSVM/src` reads the field** — we render
  every zone's geometry at once. Plausible source of artifacts; not yet shown to cause a specific
  one (checked and ruled out for the C5 ground z-fight, where both surfaces are `zone_id=1`).
  **Documented 2026-07-22** (polish-3 item 2) in `docs/formats/world-structure.md`, counts
  re-verified against `nodes.json`. **Blocked on the same unknown as the fog zone:** which zone a
  mission activates is in no file in the install (exhaustive negative result now written up in
  `docs/formats/weather.md`), so implementing this means *guessing what to hide* — and a wrong
  guess deletes visible world content, which is strictly worse than drawing both. Wants the
  user's zone A/B (plan's closing section) first.

- **Partition visibility is a real runtime system we do not implement** (found 2026-07-22 while
  diagnosing the C5 ground z-fight). The interp language has **`WorldPartitionSetActive`**
  (25 uses). The original selects between a coarse `world1`-child ground sheet and the fine
  partition-referenced tiles at runtime; we draw both unconditionally
  (`WorldBuilder.cs:155`, `:157-159`). The polish-run-3 fix is a draw-priority workaround, not
  this. Implementing it properly = cell-resident tracking with pop risk and an 8-chapter
  regression — Milestone 3 work, but now motivated by evidence rather than a hunch.

- **`FogState` is a decoded animation event we do not act on** (found 2026-07-22). Fog **can** be
  changed mid-mission by animation, but the data uses it exactly once install-wide:
  `extracted/C1/M04/mis_anim/camera1-mission_intro_animation.json`, `reset_state/events[4]` —
  `FogState { name: "drop_fog", color 0.69/0.69/0.69, altitude 10000–11000, range 1000–1500 }`.
  It carries fog parameters **inline** and matches neither C1 zone, so it is an ad-hoc third fog
  state on the intro cutscene camera, not a zone selector. Relevant because it is the only
  evidence that weather is scriptable at all; zone *selection* still appears to happen engine-side
  in the binary (same shape as the `fire2` trigger the user searched the disassembly for).
  **Documented 2026-07-22** (polish-3 item 2) in `docs/formats/anim-definitions.md` as
  decoded-but-unacted-on, with the reason: implementing it means a second write path onto the
  `csky_fog_*` globals `PlaneViewer.SetupWeather` owns, for one cutscene the remake does not run.
  Revisit if the user ever sees fog visibly change *during* a mission elsewhere.

## Open bugs (moved from NOTES.md 2026-07-22)

The user's running issue list. **Each was checked against the code on 2026-07-22 — none of them
were already fixed**, so the whole list is live. Where that check pinned the cause it is recorded
here so it is not re-derived; where it did not, the item says so rather than guessing. Paths are
relative to the Godot project's `src/`.

### Flight & damage

- **Knife-edge only sinks; the nose should also drop slightly.** `FlightModel.cs` scales the
  velocity-chase rate by `KnifeAlignFloor` and zeroes the lift fraction at 90° bank — both act on
  `VelocityDir` (the flight *path*), not on `Attitude`, so the path sags below the nose and the
  plane descends wings-level-nosed. The only code that rotates the nose toward world-down is the
  stall block, gated on `Speed < StallSpeedFrac × FdSpeed`. Wants a small attitude torque in the
  knife-edge branch, **not** a tweak to the existing path terms.
- **Tail collision boxes swallow the outboard wings (Bloodhawk).** `PlaneCollider.cs` classifies
  everything aft of `TailStartFrac` (0.7 × length) **at full span** as `tail`; the refinement pass
  that splits that slab keeps the name, so the outboard strips stay `tail`, and `PlaneDamage.cs`
  maps `tail => tail` with no |x| test. Wing hits score as tail damage. The fix is an |x| test in
  the refinement's naming, not a `TailStartFrac` change. (Distinct from the accepted canard-tip
  limit already noted in `docs/HISTORY.md`.)

### Environment

- **C4's cloud deck does not follow the plane.** The follow mechanism works (`PlaneViewer`
  re-anchors `rig.Deck` to the camera X/Z each frame, fed by `WorldBuilder.CloudDeck`), but the
  deck is selected by texture prefix **`cloudlayer`** only (`WorldBuilder.cs:77-78`), so
  `CloudDeck` is null in C4, the deck stays world-fixed, and `IsCloudSpriteTexture` additionally
  billboards it as a sprite. C4's weather *does* define the band (`CLOUD_COVER TOP 1100 /
  BOTTOM 1000`), so the `HasCloudBand` guard is not the blocker.
  **Correction 2026-07-22: the deck is NOT `cloudtrans`.** `srock-cloudtrans` skins 35 models of
  96–422 vertices with dy 162–533 m — cloud-shrouded **rock terrain**, which must stay solid.
  C4's real deck is **`Sky1.tif`**: 144 parentless partition-referenced nodes `g1720..g1863`, each
  a single 4-vertex flat 1024×1024 quad at **y = 1050** — the same signature as C1's `cloudlayer`
  deck (144 nodes, 1024², y = 960). Decks exist only in C1/C1C/C2B (`cloudlayer`) and C4 (`Sky1`).
  **The trap:** `Sky1.tif` is the *skydome* in C1/C1B/C1C/C2/C2B/C3 (2 nodes under `horizon/zone2`)
  and the *deck* in C4, so widening the predicate to `sky*` is only safe because `Build` skips the
  `horizon` subtree — an implicit dependency. Wants a structural test, not a name test. Scheduled
  as polish-run-3 item 4.
- **C3 massive z-fighting at the beach** — `--campos=-6151.614,136.079,-3198.714
  --lookat=-6150.76,135.796,-3199.151`. **User-confirmed 2026-07-22 as real z-fighting, and the
  correct resolution is known: the beach should draw over the water.** That makes this the one
  z-fight case where the target surface is *not* in doubt — unlike the C5 ground case below, where
  "which surface should win" is still the blocking question. Undiagnosed as to why the two are
  coplanar. A frame rendered at that pose (`.scratch/c3_whatisthis.png`, 2026-07-22) shows the
  sand/surf boundary with hard polygon-stepped edges and one clean triangular wedge; it is a single
  frame, so it does not by itself separate depth flips from a static alpha-cutoff artifact — use
  `--shots=5 --jitter=0.006` (sub-pixel dither; the 0.15° default moves the camera far too much to
  isolate depth flips) before assuming the mottling and the hard edges are the same fault.
  **Do not fix by raising the bias constants globally** — see the C5 entry below for why.
  **Measured 2026-07-22 (polish-3 item 11): this pose barely flickers at all.** `--shots=5
  --jitter=0.006` there gives **0.41%** of pixels flipping (vs C5's 35.96% and C1B's 30.95% on
  the same instrument), the beach is continuously visible, and the palms stand on sand
  (`.scratch/zf_c3_base_00.png`). Whatever the user is seeing at this shoreline is therefore
  **not** a per-frame depth flip at this camera — it is either a *static* wrong-winner (the
  water consistently over the sand, which a jitter-flip metric cannot see at all) or it needs
  a different camera. **Get a fresh capture or a pose from the user before diagnosing further**;
  the pose recorded here does not reproduce a flicker to measure against.

  **✅ RESOLVED 2026-07-22 — this was fixed by `6c592c2` and the entry was stale.** The user
  reported the flicker gone and guessed the cause was the per-mission entity setup. Bisected and
  confirmed at their own fresh pose (`--campos=-5931.403,149.271,-3292.775
  --lookat=-5933.68,66.417,-3348.722`), measured with `--shots=5 --jitter=0.006`:

  | commit | C3 coast flicker |
  |---|---|
  | `10f48f8` (parent — before per-mission setup) | **4.87%** |
  | `6c592c2` "World: per-mission entity setup — the interp boot script, not a roster" | **0.09%** |
  | `b83252c` (current main) | **0.09%** |

  A **54× reduction at the commit that applied the interp boot script**, and nothing since has
  moved it. Mechanism: `MissionSetup` runs the chapter/mission `.gw` script's
  `NodeSetActive`/`DeleteTree`, so duplicate overlapping entities that had all been drawn
  coplanar stop being drawn. Remove the duplicates, remove the depth fight.

  **The lesson is about the backlog, not the bug.** This entry carried "user-confirmed
  2026-07-22 as real z-fighting" while the fix landed *the same day*, so two separate agent
  sessions (polish-3 items 3 and 11) spent effort chasing an already-fixed symptom, and item 11's
  0.41% reading — correctly measured, correctly reported as "barely flickers" — was the fix
  showing through, not a mis-aimed camera. **When a report and a fix share a date, check the
  commit order before scheduling work.**
- **C3 trees standing in the water** (same camera pose) — **diagnosed 2026-07-22 as the SAME BUG
  as the beach z-fight above, not a clutter placement fault. Merged; scheduled as polish-run-3
  item 7.** The palms are visible in the original and are *supposed* to be there (user-confirmed
  2026-07-22). What fails is that the water wins the depth fight against the beach, so the sand
  they stand on vanishes and they read as growing out of the sea. **Do not "fix" this by dropping
  submerged palms** — an earlier draft of the plan proposed exactly that (`y > waterLevel` guard in
  `Clutter.PlaceOnTriangle`, which has no Y or water test today), and it would have deleted correct
  content while leaving the actual z-fight untouched. Measurement behind the merge:
  `cliff1_sandtrans.tif` (C3's only clutter template ground, node idx 3232 → ground 3240, 6
  `palmtree1.flt` decorations) covers 102 world polygons, **86 of them perfectly flat at exactly
  Y = 0.0** — and Y = 0.0 *is* the C3 sea plane, shared coplanar with `wtr00000` ×1565 (material
  220, `soil: "Water"`), `shore2` ×1027, `shore1` ×487, `cliff1_watertrans2` ×177, `sand128` ×140.
  So ~84% of the palm-bearing sand band is coplanar with the sea, which is why the symptom is
  map-wide rather than a few stray trees. A fix must leave the clutter instance count unchanged.
- **C5 shader warning: `More than one material in instance export the same instance shader uniform
  'csky_fog_on', but they do it with different indices.`** (`instance_uniforms.cpp:62`.) Cause
  found: `csky_fog_on` is an instance uniform declared in two independent shaders at different
  slots — `SceneBuilder.cs` declares it at index **1** (after `instance uniform float node_bias`),
  `Clutter.cs` at index **0** (its shader declares no other instance uniform). Only the first wins,
  so clutter fog can silently read the wrong slot. Fix = force matching indices across every shader
  that declares it. Related but already handled: the `csky_opacity`-must-follow-`csky_fog_on`
  ordering regression (`docs/HISTORY.md`), and the billboard/cloud shader deliberately omitting the
  uniform. Side note found while checking: `WorldBuilder.DisableFog` is now dead code — its only
  call site is commented out.
- **C5 IA1 has a zeppelin sunk in the ground** — `--campos=235.618,1471.759,94.103
  --lookat=237.62,1371.833,97.39`. Undiagnosed. **Not** the degenerate-transform fault that entry
  above used to speculate about: that was the unread `spline_interp` flag (fixed 2026-07-22), and
  all 15 affected scripts are C1 and C4 only — **C5 ships none**, so this is a separate bug.
- **C1B z-fighting** — `--campos=-7698.844,48.763,-5797.924 --lookat=-7749.957,-20.093,-5849.367`.
  **30.95% of pixels flip** at `--shots=5 --jitter=0.006` (measured 2026-07-22, polish-3 item 11 —
  the most severe of the three recorded z-fight poses after C5's 35.96%). **It is a CROSS-NODE
  fight**: raising `NodeOrderBias` from 5e-8 to 2e-6 — a control that changes nothing but the
  cross-node draw-order term — collapses it to **2.35%**. A within-surface (per-polygon)
  tie-break leaves it at 31.00%, i.e. does nothing. Same caution about the bias constants: that
  control is a diagnosis, **not a landable fix** — the same constant takes C5 from 35.96% to
  41.69%, because the node-index span is thousands and a step big enough to beat the
  depth-resolution floor spans tens of priority levels. The real fix has to make the bias
  *dense over the nodes that actually conflict* rather than uniform over all of them; see the C5
  entry for the resolution-floor measurement that makes this the shape of every remaining z-fight
  here.
- **Some oil tanks are already destroyed at spawn in C1 IA1, next to the Bloodhawk hangar.**
  Undiagnosed — likely a mission-setup or animation-bootstrap pass applying a destroyed state
  variant that the original does not.
- **The bowl sign flashes in the original**, but ours disables and re-enables it instead.

### Animation

- **C1 police / mafia / traffic cars drive their route once and stop; the original loops them.**
  Cause found: `AnimRuntime.cs` reads the loop count and treats **0 as "stop immediately"**
  (`_loopsLeft = … ; if (_loopsLeft == 0) { _done = true; break; }`). Every C1 traffic def ships
  `Loop { "Count": 0 }` — verified across `police_car-police_chase`, `mafia-mafia_move1`,
  `black_car1-black_move1`, `car_go_home-car_go_home_start`, `car_loop1-car_loop1_start` — while
  genuinely-endless defs (docklights, firetrucks, `red_police-police_lights`) use `-1`. So
  **`Count: 0` almost certainly means "infinite" in the original** (note the name `car_loop1`).
  Changing that mapping is a one-line fix but touches every def install-wide — verify no
  currently-terminating animation ships `Count: 0` and relies on stopping.
- **Some C1 animated-object rotations are wrong (cars)** — evidence:
  `crimsonskies_2026-07-21_23-14-29-724.png`. Undiagnosed.

### HUD & audio

- **Crash damage display blinks fully red.** `GaugeCluster.cs` blinks a zone for `DamageBlinkTime`
  on `OnPartDamage` and picks the red variant at `frac <= RedAt`, but the only caller is a graze
  hit — `FlightController.Crash()` touches audio, fireball, breakup and visibility and never calls
  into `Gauges`. So the all-red state is not a crash behaviour being mis-fired; it is the ordinary
  damage path left latched. Check what the original shows on a crash before wiring anything.
- **Gauge needles are the wrong shape** — they come from the game's own HUD textures. Could be
  drawn procedurally instead in a future Hi-Def mode.
- **No mute when the window loses focus.** Nothing in `src` handles
  `NOTIFICATION_APPLICATION_FOCUS_OUT` / window focus, and there is no `AudioServer` or bus gate —
  `FlightAudio.cs` only sets per-player `VolumeDb`. Related open question already recorded in
  `docs/HISTORY.md`: whether pad reads should be gated on focus project-wide. Decide both together.

### Mission logic

- **C2 stunt mode: the Seaplane Hangar objective sits at the wrong position.**

## Milestone 2 polish run 3 — candidate scope (from NOTES.md)

Grouped by the user as a prospective third polish run. Not a plan — write one when it is scheduled.

- **A generic way to find billboard sprites** — **mostly already done; rescoped 2026-07-22.** The
  data-driven rule landed 2026-07-21: `GameZ.cs:479-482` exposes `ModelType`/`FacadeMode`,
  consumed at `SceneBuilder.cs:446-459` (spherical) and `:468-470` (cylindrical). The example this
  entry originally named — "some face the camera only about X/Y (the harbour refinery flames)" —
  **is the case that already works**. What actually remains is consolidation: the classifier is
  split across two `private` methods, `Clutter.SpriteInfo` uses an independent 1-poly/4-vert/flat-Z
  shape heuristic, and `WorldBuilder.IsFlareSpriteNode` still gates collision on poly-count +
  texture name. Scheduled as polish-run-3 item 5.
- **Billboards should generally have no collision.** Tree collision is a nice touch but the
  original does not have it. **Confirmed 2026-07-22, and the counter-evidence was a misreading:**
  `Clutter.cs:33-35` and `docs/formats/clutter.md:54` both justify collidable trees with
  "`spruce_destroy` anims exist". The actual data strings are
  `..\data\common\zrdr\**planes**\spruce_destroy1.zrd` (C2/M01) and `spruce_destroy2.zrd`
  (C5/M03) — the **Spruce Goose**, Howard Hughes' flying boat and the C2/M01 mission object, whose
  folder siblings are `sprucegoose-fly_the_goose`, `free_the_goose`, `goose_cooked`,
  `goose_down_lwing`, `spruce_enginedest`. **There is no spruce-*tree* animation in the install.**
  Both doc claims need correcting, not just the code. User decision 2026-07-22: remove tree
  collision outright (not behind a flag); `cblock` city-block **buildings keep** collision, being
  real 3D meshes rather than cards. Scheduled as polish-run-3 item 5.
- **Determine which weather/sky zone each chapter and mission actually uses.**
- **Fine-tune fog and environment** — method: record video from spawn points flying straight for a
  fixed number of seconds, in both engines, and compare.
- **Better mission states.** There is still a lot of difference between our maps and the original's.
  May need a pipeline to diff them, or to crack the mission loading states properly.

## Feature backlog

- **M3-deferred gun mechanics — firing heat and cannon jam** (scoped out of
  `docs/PLAN-M3-weapons.md` 2026-07-22, decision 4: friction with no combat pressure to justify
  it while nothing shoots back). **The constants are exact, so nobody needs to re-derive them:**
  `weapons.json` `FIRING_HEAT` on 4 entries (30-cal = 5.0); `vehicle.json` `cannon_jam` on
  `player_airplane` = `heat_safe_limit 1000`, `heat_dissipation_rate 50`, `jam_chance 0.1`.
  Heat accumulates per shot, dissipates at 50/s, and past the safe limit each shot has a 10 %
  jam chance. Pick this up when there is combat pressure — i.e. alongside or after M4 AI.

- **M3-deferred — ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- **The gun/hardpoint configurator UI** (deferred from M3, decision 9 — M3 flies stock loadouts
  only, but its loadout model is data-driven so this drops in without rework). The original's
  screens are `GUNS.SCRIPT` (4 gun slots, `gn_d_gun0..3`, engine callbacks 2249/2250) and
  `HARDPOINTS.SCRIPT` (2 hardpoint slots, `hp_d_point0..1`, callback 2245), plus
  `PLANECONSTRUCTION.SCRIPT` / `PURCHASE.SCRIPT`. Both are **pure UI layout** — per-plane slot
  counts, weapon costs and the economy are all executable-resident, so the *buying* half would
  have to be invented. The mount names are data (`IDS_AIRFRAMEGUNGROUPNAMES`, ui_strings
  3060–3079) and the per-plane stock table is authored, so the *placing* half is real.

- **M4 dependencies discovered while planning M3** (2026-07-22) — recorded so they are not
  re-derived:
  - **Turrets are AI gunners**, not player-aimed: they acquire and engage other aircraft
    automatically (user-confirmed). This is why `extracted/zrdr/ai.zrd.json` contains nothing
    but `TURRET` defs. Five player planes carry one — `pavenger`, `pbalmoral` (two: front +
    rear), `pbrigand`, `pfirebrand`, `pkestrel` — and **W4 in the stock loadout table is filled
    on exactly those five and no others**. `vehicle.json` `turrets` gives `firstp`/`thirdp` node
    pairs (which mesh renders in which view, *not* a player camera mode); `gun_pitch`/`gun_yaw`
    (±11°) are the gunner's cone.
  - **`target`** — a mesh-less marker, one per plane root (11 player + 11 AI). The aim point
    for AI gunnery and air-to-air lock-on.
  - **Air-to-air lock-on.** M3 implements the full guided-missile flight model but restricts
    acquisition to ground destructibles, so air-to-air is a targeting change, not new flight
    code.
  - **Shootable ordnance.** `wep_14` (TORPDO) has `FLYOUT_HEALTH 10` and `TARGETABLE` — the
    torpedo itself can be shot down. Inert in M3 because nothing else shoots.
  - **AI vehicle armour/health.** `vehicle.json` carries an `armor` + `health` pair on AI defs
    only (aircraft always `armor == health`, 60–100; `patrolboat` and `t_truck` `armor 0 /
    health 40`). `PlaneStats` does not read either. The model is **armour-first, then health**
    (see `docs/PLAN-M3-weapons.md` C23 for the dominance argument that settles it).

- **Residuals from polish-3 item 5 (2026-07-22) — all small, all deliberate.**
  - **`csky_fog_on` instance-uniform index disagrees between the two shaders.** `Clutter`'s
    shader declares it at index 0; `SceneBuilder.GetBiasShader` declares `node_bias` first so
    it lands at index 1. Godot merges instance-uniform mappings across the materials on one
    `GeometryInstance3D`, and a disagreement silently drops fog on the losing surfaces — the
    2026-07-17 unfogged-hilltops bug. **Verified latent, not live:** clutter renders through
    `MultiMeshInstance3D` + `MaterialOverride` and never shares an instance with a
    `SceneBuilder` material, and an 8-chapter run logs **no** `instance_uniforms.cpp` warning
    at all. Padding Clutter's shader with an unused `node_bias` was tried and reverted — it
    enforces nothing, since the next shared uniform still has to be added to both shaders by
    hand. **The real fix is a shared ordered-preamble constant emitted by both**, which means
    editing `GetBiasShader` and re-running the full regression (it changes every world
    material's shader text). Until then the invariant is: declare any new instance uniform
    LAST, in both shaders — documented at both declaration sites.
  - **`PlaneViewer.cs`'s `InstanceShaderParams` omits `csky_opacity`**, so a duplicated
    splitscreen cloud deck loses any animated `OBJECT_OPACITY_STATE` opacity. Noted by the
    plan, untouched here (different file/owner). Arguably the more real of the two uniform
    defects.
  - **`FlightController`'s soft-tree collision branch is now dead code.**
    `hitName.EndsWith("clutter_col")` at `:565` and `:649` can never match. Still true after
    polish-3 item 6 gave the 3D city-block decorations collision: those bodies are named
    `clutter_bld_<cx>_<cz>`, chosen precisely so they do NOT hit this branch — a skyscraper
    must not be soft. Inert, left in place because that file belongs to another module's
    owner. Delete with the `TreeDamage` constant when someone is next in there.
  - **C5's `poleflare` clutter renders with the wrong billboard axis.** The `cblock*` templates
    ship `lightpole` (`CylindricalY`) posts *and* `poleflare` (`SphericalY`) glows — 33,682 of
    each in `cblock1` alone. `ClutterBuilder.Kind` carries no per-kind billboard mode, so every
    kind goes through the one Y-axis shader: the glows spin upright instead of facing the camera,
    and they get the SUNLIGHT night dim a light source should be exempt from. Now *detectable*
    (the shared `SceneBuilder.ClassifyBillboard` distinguishes the two), but fixing it means
    giving `Kind` a billboard mode and a second material path, and it changes how 139,388 C5
    sprites look with no reference shot to check against — so it needs an original-game A/B.
  - **Static collider probe is off by 6 (C4) and 11 (C5).** `.scratch/probe_exempt.py`
    replicates the world walk and reproduces the runtime collider counts *exactly* in 6 of 8
    chapters, but predicts slightly more billboard exemptions than the game applies in those
    two. The safety conclusion is unaffected (the probe's candidate list is a superset and
    contains nothing solid), but the gap is unexplained rather than benign-by-proof.

- **Skybox colour grading.** No tint, grade or tonemap is applied to the skydome anywhere —
  `WorldBuilder.BuildHorizon` only disables shadows, billboards the moon and disables light
  range-fade, and the `WorldEnvironment` sets background/ambient only. The dome does get the shared
  per-mission scalar dim `csky_world_light`, which is brightness, not grading. The decoded
  per-mission cloud tints are parsed and deliberately parked (`Weather.cs`, "unused this
  milestone") — they are the obvious input if this is picked up.
- **Paint scheme follow-ups** (the core landed 2026-07-20 — see `docs/formats/paint.md`
  "Known divergences"; these are the leftovers):
  - *(Resolved 2026-07-20 by the rework onto the original's own region masks: achromatic
    regions now paint, and slot order is read from the data instead of ranked by area. See
    "What the rework fixed" in `docs/formats/paint.md`.)*
  - **The paint UI's "Shade" column** is unmodelled — three Colour *and* three Shade
    dropdowns exist in the UI, only three colours in the data. We ramp black → colour.
  - **A livery picker in the launchscreen.** Selection is CLI-only (`--paint=`); flight
    randomizes per player. Decide from playtest whether the menu should offer it.
  - **AI/ace liveries.** `ia.json` `ace_*` and the AI defs' own `paint_*` are parsed into the
    catalog but nothing flies them — there are no AI aircraft yet.
- **Full `player_plane_destruct` crash choreography**: surface variants
  (`player_crash_default/_dirt/_water`), sparks, black smokeball, `plane_destroy_sg` sound,
  crash trails. Current state = fireball + breakup pieces + 10 s wreck fire (item 10d).
  Folds in the crash-video follow-ups (2026-07-19, `C1 IA1 Crash.mp4`): brown dust burst on
  hard grazes, burning debris arcs, explosion scale/persistence.
- **`flight_ceiling`** (data: 2500 m) unenforced — at full throttle a steep climb is a stable
  equilibrium and sails past it (accepted arcade artifact of the Run-1 item-6 flight model).
- **PLAYER_INIT fields [3]/[4] semantics + per-plane spawn speed** — story-mission spawns
  currently assume the IA convention (0.5 throttle / 53.6 m/s).
- **Sky UV scroll** (`h_zone*scroll`) — scroll rate unknown, not implemented.
- **Star twinkle + undecoded light fields** (flags 523/…, the 0.17 float) — stars/beacons
  render as fixed-size soft sprites, no twinkle.
- **Visual prop spin-up/down** (`startprops`/`stopprops` disc crossfade) — spawning mid-air
  already turning is by design; becomes relevant with a landing/shutdown flow
  (`FlightAudio.OnEngineStop` is already wired for the audio half).
- **Engine dual-stack chorus**: the original plays the engine loop as a ~5%-detuned pair
  (measured in the dive-video analysis, HISTORY 2026-07-19); ours is a single loop.
- **Positional 3D audio for other aircraft** — all sound is own-plane non-positional today;
  the original's IA traffic is clearly audible with Doppler in the reference video.
- **Future cockpit view** would consume already-parsed data: `pcdpN` cockpit damage panels,
  the `*_damage_green/yellow/red` indicator anims (PlaneStats parses them, DamageVisuals
  skips them), `cockpit_engine_sound` (`*_cp` WAVs), `player_fuelleak` (0.85 threshold).
- **Runway `lite*`/`ltout*` lights-on/off state-variant quads** still z-tie (needs an
  engine-side light-state toggle to pick one variant).
- **Rail-over-transition z-nit**: one 6-poly rail patch NE of the C1 bridges sits below the
  draw-order tie-break's resolution.
- **Finished-pilot behaviour in a splitscreen stunt race** (M2.5 item 7): a pilot who clears
  every zone freezes at the finish showing their placing while the field flies on. It matches
  the solo run's freeze and makes the placing unmissable, but it parks a player with nothing
  to do for as long as the slowest pilot takes. The alternative — keep flying freely with the
  timer stopped — is a small change (drop the AllComplete early-return when `Race != null` and
  gate only the objective/marker updates). Decide from the two-controller playtest.
- **Race spawn fairness**: each player takes the next entry in the mission's `stunt_flying`
  spawn list, so pilots start at genuinely different distances from the first zone. Fine for
  a prototype, unfair as a race. Options: spawn everyone abreast from one point (the
  `--spawn-at` `SpawnAbreast` fan already does this), or rank on a per-player-normalised time.

## Open fidelity questions (answerable by testing the original)

- **Patrol boat: which HP governs?** (from `docs/PLAN-M3-weapons.md` C23, 2026-07-22.) The boat
  is described by two systems that **agree on the damage-stage fractions and disagree on total
  HP by exactly 2×**: the `patrolboat` vehicle def says `health 40` with stages at 0.60/0.30
  firing `ptboat_50damage`/`ptboat_75damage`, while the `C1/patrol_boat` anim def says
  `HEALTH 20` with stages at `ANIM_HEALTH` 12/6 (also 0.60/0.30) firing the generic
  `sputter_black_smoke_obj`/`sputter_fire_smoke_obj`. Likely reading: the vehicle def governs
  the boat as an **AI combatant**, the anim def as **placed scenery** — so M3 (scenery only)
  wants 20. **Settle by shooting one in the original with a known weapon and counting hits.**
  Also check whether `t_truck` (`armor 0 / health 40`, no injure_anims), `fueltruck` and
  `armytruck_destruct` show the same duplication.

- **Rocket firing cooldown.** Every rocket entry has `FIRE_RATE 1.0` (vs 8.0–10.5 for guns),
  i.e. one launch per second. The user confirmed one trigger pull = one rocket from one
  hardpoint but was **not sure whether a cooldown exists**, so 1.0 is the data's answer rather
  than an observed fact. A/B against the original.

- ⚠ **Never infer a damage threshold from an animation's name.** `ptboat_50damage` fires at
  **60 %** health remaining and `ptboat_75damage` at **30 %** — the names lag their trigger,
  the same way `docs/formats/hud.md` records for the cockpit damage dial ("the anim names lag
  their effect by one state"). Measured 2026-07-22 while planning M3.

- **Map-edge continuation**: ours alternately *reflects* the border tiles (seam-free by
  construction); the original likely plain-repeats them, possibly sharing the edge vertex row.
  A/B an asymmetric border feature in the original; one-line swap in `MapEdgeExtender.MirrorAxis`.
  **User observation (2026-07-22): the tile borders do match, so it may not be mirrored — and the
  original may extend by more than one tile.** Both are testable in the same A/B: an asymmetric
  border feature settles mirroring, and counting tiles out to the fade settles the extent.
- **Compass north convention**: north = −Z is assumed (one-line flip in FlightController's
  heading feed); drum projection constants + nearest-tick sampling pending A/B.
- **Crossed `pdpN_h` numbering** (bloodhawk/firebrand/brigand data quirk): does the *original*
  amputate the wrong wingtip on wing damage too? Its engine hides healthy skins by an
  engine-side rule we can't see; we pair by mesh position since 2026-07-19.
- **Dive terminal speed**: the dive-video gauge frames pin the original's near-vertical dive
  terminal at ≈ 1.27×fd_speed (~385 mph); our drag curve + `MaxDiveSpeedFrac` cap runs to
  1.7×. Matching it means reshaping the overspeed drag (or the cap) — interacts with the
  whine/rattle curves that key off speed/fd_speed (HISTORY 2026-07-19).
- **Pitch rate vs speed**: the user's 11 s sustained full-pitch 360° visibly bled speed in the
  original — its pitch rate may slow with speed; ours is constant (item-12 calibration matches
  the 11 s average). Likewise the yaw `eff` speed shape (`1.4 − clamp(v/fd)`) is an unvalidated
  interim model away from cruise.
- **Engine pitch behavior in dives**: the original's engine drops ~12% through a dive and
  overshoots ~1.05 at pull-out — not reproducible by the throttle-only pitch curve (cap 1.0).
  Throttle cut? Camera Doppler? A speed/RPM term? Needs a controlled full-throttle-dive
  recording (HISTORY 2026-07-19).
- **`SunIncidence` 0.46** (item-6 world brightness) rests on a single overcast reference —
  a C1B-night and a bright-day original screenshot would confirm/refine the self-scaling.
- **`fogRangeFactor` 2** halving predates the sRGB fog-color fix — re-A/B in game.

## TUNE constants pending playtest

The live list (moved here from CLAUDE.md 2026-07-22). Each is a hand-tuned constant that is
plausible but unvalidated against the original — they need the user in the cockpit, not another
scripted screenshot.

- **Compass tape** — north = −Z convention (unverified vs the original; one-line flip in
  `FlightController`'s heading line), plus `TileOverscan` / `RimGain` / the nearest-tick look.
- **`fogRangeFactor` 2** — the halving predates the sRGB fog-colour fix, so re-A/B it in game.
- **Flight model** — `StallNoseRate`, `KnifeAlignFloor`, `ClimbGravityScale`,
  `LowSpeedDragBlend`, and the Run-2 item-12 per-axis `PitchTune` 0.75 / `YawTune` 1.32 /
  `RollTune` 2.12. Those three are measurement-calibrated (2026-07-19) but the *feel* A/B is
  pending — **especially the ~2.7× cut in pitch authority**, the largest single change to how the
  aircraft handles.
- **Control surfaces** — deflection angles and slew rate.
- **Cloud puffs** — opacity and density. **Cloud deck** — brightness reads ~40 units lighter
  than the original.
- **Wing lights** — `FlashDuration`.
- **Collision feel** — behaviour against building corners.
- **Damage (Run-2 item 10)** — `CrashSpeed` 25, graze friction + attitude kick, tree softness,
  `GrazeStopSpeed`, breakup scatter, and whether the 10c panel-flip and smoke-trail look right in
  real flight (the thresholds need states normal play actually reaches).
- **Audio (Run-2 item 11)** — `WhineMixGain` 0.12; A/B a dive against the original.
- **Stunt mode** — `DzRadius` 30 m (tightened from 60 on user feedback), marker-HUD placement,
  font and distance units; scoreboard fonts and placement.
- **Splitscreen** — the `HudMetrics` sqrt pane damping, `MixGain`, `SpawnAbreast`, join/lock
  feel, tag-gutter widths.
- **Aircraft brightness** — every aircraft got substantially brighter when the inverted-normal
  bug was fixed (2026-07-20); whether flight lighting now wants re-tuning against the reference
  videos is unassessed.

## Owed playtests (need hardware or a human at the controls)

- **The M2.5 playtest pass.** Items 6 and 7 of `docs/plans/PLAN-M2.5-prototype.md` — the
  launchscreen join flow and the splitscreen stunt race — rest on construction plus scripted
  verification for everything needing **two controllers**, which this machine does not have.
  Unverified at runtime: join / un-join, the lock race, per-pad flight binding, Esc-from-
  splitscreen-to-menu, board fonts and placement, rematch feel. Also open as a *design*
  question: should a finished pilot keep flying rather than freeze at the finish?
- **`--freecam` interactive feel** — look sensitivity and the speed curve have never been
  assessed by hand; the module was built entirely through scripted screenshots and
  `--debug-anim`.
- **The labs are mouse-driven** (`--viewer`: damage on H, livery on L, mesh on M) and have had no
  interactive playtest beyond scripted verification.
- **The ambient world sounds have never been listened to** (`SOUND_NODE`, landed 2026-07-22). Mix
  levels and the `RANGE` → `UnitSize`/`MaxDistance` curve are TUNE. Where to listen:
  1. **C1 free flight** — the waterfall and the train are the only two emitters that sound there.
     The waterfall is at roughly (-7868, 0, -3449) with a 1500 m range; the train moves, so it
     should pan and fade as it runs its loop.
  2. **C4** — three waterfalls.
  3. **C1/M04** (`--freecam --chapter=C1 --mission=M04`) is where a zeppelin engine actually plays.
     A `sound: … silenced — its host node's world pose is degenerate` line there is **expected and
     logged**, not a new fault — it is the pre-existing 1e27 transform bug (see "Degenerate zeppelin
     node transforms" above).

  `--debug-anim` prints every emitter's host, distance, range and playing state once a second, which
  separates a placement problem from an activation one.
- **Splitscreen ambient audio may be mixed once per pane — the one structural unknown.** With no
  `AudioListener3D`, Godot makes each pane's camera a listener, so a 2P/4P session may mix every
  3D emitter 2–4×. **If ambient audio sounds loud or doubled in splitscreen but fine solo, that is
  the cause, not the mix levels** — and the fix is an explicit listener, not a gain tweak. Needs a
  splitscreen A/B (and therefore the two controllers this machine does not have).

## C3 ships gamez references to textures its texture.zbd does not contain

Surfaced 2026-07-21 during the extraction cutover and deliberately left alone. C3's gamez
references `cloud1`/`cloud2`, which its own `texture.zbd` does not ship — a **retail-data gap**,
true in both the v0.6.1 and fork extraction trees, so not something the cutover caused.

The fix is a one-line addition to `TextureArchive.KnownAbsentFromGameData`, which would render
them neutral gray instead of magenta. It is left to the user because it is a **visible** change
and it deliberately gives up the magenta signal that means "our bug" for those two names — the
project's convention is that magenta is diagnostic, so suppressing it is a judgement call, not a
cleanup.

## C5 ground z-fighting — coarse quad coplanar with the detailed city ground

Found 2026-07-21 while investigating the animation-runtime performance regression; the user
asked whether follow-up plan item 3 would clear it. **It would not** — full diagnosis in
`docs/HISTORY.md` (2026-07-21 entry), summarised here so it isn't re-chased.

Repro: `--viewer --chapter=C5 --sky-zone=zone2 --campos=-9533.178,76.319,-3367.413
--lookat=-9451.281,28.148,-3398.597`, measured with `--shots=5 --jitter=0.006` (a *sub-pixel*
dither — the 0.15° default moves the camera far too much to isolate depth flips). 8.42% of
pixels flip.

Ruled out: the map-edge extender (identical flicker without `--sky-zone`), and entity rosters /
item 3 (only 2 mesh nodes within 1500 m, generic ground names — item 3 hides discrete roster
objects). Confirmed cause: the depth bias is proportional to view distance
(`VERTEX *= 1.0 - (depth_bias + node_bias)`), so coplanar surfaces sharing a draw priority get
`rank × 2e-6` ≈ 0.16 mm at ~80 m. A 100× bias drops the flicker to 0.01%.

> **⚠⚠ SUPERSEDED AGAIN 2026-07-22 (third measurement pass, polish-3 item 11) — the
> "`g4683` fights ITSELF" answer in the box below is ALSO wrong. Read this box first.**
>
> **`g4683` has no self-overlapping polygons at all.** The "8 pairs exactly coplanar at y = 5,
> overlapping by up to 768 × 512" figure came from an **AABB** overlap test, and an AABB
> overlap is not an area overlap. Clipping the actual outlines (Sutherland-Hodgman, true
> polygon∩polygon area) gives **zero** overlap for every pair in the mesh. Worked example —
> its polygons 3 and 4, the pair whose bounding boxes overlap most:
>
> ```
> poly3  (-9600,-3712) (-9472,-3712) (-9216,-4096) (-10240,-4096)
> poly4  (-9600,-3712) (-10240,-4096) (-10240,-3584) (-9600,-3584)
> ```
>
> They **share the edge (-9600,-3712)→(-10240,-4096) exactly** and lie on opposite sides of
> it. They abut; they are a tiled ground surface, which is what this data mostly is.
>
> **The real mechanism at this pose is cross-node, and it is a resolution problem, not an
> ordering problem.** Nine *different* World-child nodes stack coplanar `cblock*` ground at
> y = 5 inside the repro footprint — nodes 1777 (`g4683`), 1799, 1800, 1801, 1813, 1814, 1822,
> 1823, 1837, priorities 0 and −10. The priority −10 ones separate cleanly (10 × 2e-4). The
> priority-0 ones are separated only by `node_bias`, and the measured depth-resolution floor at
> this view is **≈1e-6 of view distance** (bracketed directly: a per-polygon depth ramp of 2e-7
> moves the flip rate 35.96% → 33.20%, one of 2e-6 moves it to 0.37%). `NodeOrderBias` is
> **5e-8 — twenty times below that floor**, so sibling nodes 36–60 indices apart get a
> separation of 1.8–3.0e-6 that straddles the floor, and nodes 1822 vs 1823 (delta 1 → 5e-8)
> can never separate at all. That is why the flicker is partial rather than total.
>
> **Do not "fix" this by raising `NodeOrderBias`.** Measured: raising it to 2e-6 takes C5 from
> 35.96% to **41.69% (worse)** — the node-index span is thousands, so a step large enough to
> beat the floor spans tens of priority levels and scrambles the authored layering. (It does
> take **C1B from 30.95% to 2.35%**, which is good evidence C1B's fight is cross-node too, but
> it is not a landable fix.)
>
> **Also corrected: "the 35.77% baseline is largely grazing-angle mipmap/aniso resampling" is
> false.** A pure depth-ordering change — the 2e-6 per-polygon ramp, which alters nothing but
> depth and leaves the projected position identical by construction — took the same pose to
> 0.37%. So ≤0.4% of it is resampling and ~35.6% really was depth flipping. What that ramp
> does *visually*, however, is float the coarse sheet in front of the detailed city
> (`.scratch/cmp_c5_after.png`) — the flicker goes away because the wrong surface wins, which
> is `docs/verification.md` rule 4 in its purest form.
>
> **The per-polygon within-surface tie-break was implemented properly and lands nothing.** See
> `docs/HISTORY.md` 2026-07-22 (item 11) for the implementation and why it was reverted.
>
> **This is `g4683` z-fighting ITSELF, not the coarse sheet against the partition ground.**
> Hiding `g4683` alone drops the repro pose's flicker to 0.19%; hiding all 7 coarse sheets
> gives the *identical* 0.19%; and applying the world-child-vs-partition rank changes it not
> at all (35.77% → 35.79%). `g4683` carries **8 pairs of its own polygons exactly coplanar at
> y = 5, same priority 0, overlapping by up to 768 × 512** — five of them the same material
> (`cblock1.tif`), so `BuildMesh` puts them in one surface with one shared depth bias and no
> tie-break can reach them. Giving every polygon its own rank moves the flicker to 21.92%,
> which is the only intervention that shifted it. **The real fix is a per-polygon
> within-surface draw-order tie-break in `SceneBuilder.BuildMesh`** (`SurfaceRankBias` today
> only separates *(material, priority)* groups; the original ordered equal-priority polygons
> by their position in the polygon list).
>
> Two further corrections. **The rule does not generalise** — across all 8 chapters only C1,
> C4 and C5 have any World-child mesh (1 / 4 / 9), **C1C has none**, `a6` is the detailed
> airfield tile and C4's `g1612` is a 477 m cliff, so demoting them would regress. **The
> coverage figures below did not reproduce**: an independent 64-unit rasterisation gives
> 18.2–81.4%, not 0.0–97.8%. The conclusion *no sheet is 100% covered, so culling leaves
> holes* survives; the per-sheet numbers should not be quoted. Also note the 35.77% baseline
> is largely grazing-angle mipmap/aniso resampling, not depth flips — see
> `docs/verification.md` rule 6.
>
> Still confirmed: World children and partition roots are exactly disjoint (intersection 0,
> all 8 chapters), and the `terrain` node flag is set on every partition root and no World
> child, so "partition-referenced" is readable straight from the data.

**Open question — answered 2026-07-22, scheduled as polish-run-3 item 3; see the box above,
which supersedes the answer.**

*Is it a stray LOD tile?* **No.** The coarse sheet is node 1777 `g4683` (34 polys, 2048 × 11264,
`brick1`/`cblock1`), a **direct child of `world1`**; the fine ground is partition-referenced.
`world1`'s 105 children and the 471 partition roots are **exactly disjoint** (intersection = 0),
and neither side sits under an `Lod` node — so `SceneBuilder.cs:132-134`'s nearest-LOD rule could
never have dropped either. We draw both because the original selects between them at runtime via
partition visibility (`WorldPartitionSetActive`, 25 uses in interp), which we do not implement.

*Which should win?* **The fine ground — but the coarse sheets must not be culled.** The
"must not be culled" half stands. ~~Rasterising each C5 coarse sheet on a 64-unit grid against
fine-tile coverage: `g4632` 97.8%, `g4683` 78.4%, `g4425` 33.3%, `g4631` 20.0%, `g4616` 12.5%,
`g4428` 8.8%, `g14550` **0.0%** — **72% total**~~ — **RETRACTED 2026-07-22: these per-sheet
figures do not reproduce** (independent rasterisation gives 18.2–81.4%; `g14550`, claimed 0.0%,
measures 18.2%). The original method tested fine-tile *bounding-box* containment, an axis-aligned
proxy far too crude for swept terrain. **Do not quote these numbers.** What survives is only the
weaker claim that no sheet is fully covered, so culling would leave holes somewhere.
~~The fix is therefore a *draw-priority* change (world-children ground ranks below partition
ground)~~ — **also retracted: that fix was implemented and measured to change nothing** (35.77% →
35.79%). See the superseding box above for the real mechanism.

*Caution for anyone re-measuring:* a naive "large flat quad" filter also catches the `fvol*`
**fog volumes** (10 in C1, 14 in C5, at altitude) — exclude them by name. And `zone_id` does not
explain the pair: both surfaces are `zone_id=1`.

**Reference material for that open question (added 2026-07-22, user-confirmed as being about this
issue):** `OriginalScreenshots/C5 IA1 Terrain.png`, `…Terrain2.png`, `…Terrain3.png` — original-game
captures (dgVoodoo) of C5 IA1 at night: one low pass looking down between buildings, one horizon
view across the city, one high overhead of the whole city ground. In all three the ground reads as
the **fine-detail city-block surface** — lit windows, street strips, per-block variation — with no
large flat low-resolution quad visible over it. That is evidence for "the detailed ground wins",
i.e. the bias-bump direction is indeed backwards.

**Do not treat that as settled yet.** Two limits: a still cannot show z-fighting (which is
temporal), and none of the three poses is matched to our repro camera, so we are comparing
different views of the same map rather than the same view in two engines. To make it conclusive,
re-shoot the original at the repro pose (`--campos=-9533.178,76.319,-3367.413
--lookat=-9451.281,28.148,-3398.597`) — or, cheaper, identify the coarse quad's node in our scene
and check whether it is a LOD sibling that should have been culled, which would settle the "why is
it coplanar at all" half without needing the original at all.

## C1/M04: the pirate zeppelin flies above the overcast and is never visible

**User-reported 2026-07-22** ("there should be a pirate zeppelin flying on the map but it isn't
visible; the other zeppelin on the field is correct"). **Not a rendering or transform bug** — it
builds, renders complete (gun, crane, panels, doors, `counterspin`) and flies. Verified by putting
the freecam on it: `--freecam --chapter=C1 --mission=M04 --campos=-5358,1505,-2200
--lookat=-5358,1505,-1810 --no-fog`.

**It is simply above the clouds.** `--debug-anim` logs successive positions
(-5358, **1505**, -1810) → (-5472, **1523**, -1956) → (-5620, **1546**, -2146): moving *and
climbing*. C1's cloud band is `TOP 1124 / BOTTOM 970` with the opaque `cloudlayer` deck geometry
at **y = 960**, and the player spawns at y ≈ 110. So the deck occludes it from below at every
point on its path, and C1's zone2 fog (1000–4000 m) finishes the job.

**Leading explanation — we bootstrap a cutscene animation as gameplay.** M04's start-anim list is
`[pzep_engines_start, train_on_track, mission_intro_animation]`, and polish-3 item 9 traced the
zeppelin's motion to `scene1 → ObjectMotionSiScript{name: "piratezep", index: 1}` — i.e. the
**intro cutscene's** choreography, on the chain
`piratezep → move_zeppelin → tilt_zeppelin → rock_zeppelin → gasbag3`. That item's report even
described the fix as "the pirate zeppelin now flies its authored **intro** path". This project has
no cutscene player, so the cinematic trajectory runs as if it were the mission's gameplay state.
In the original the intro plays as a cutscene and gameplay presumably starts with the zeppelin at
a visible altitude.

**Before fixing, establish which of these it is** — they need different answers and a screenshot
cannot tell them apart:
1. The cutscene path is correct and we should simply *not* run `mission_intro_animation` at
   startup (it is a `camera1`-anchored def full of `Callback` events, already triaged as
   cutscene-only — see the `CALLBACK` entry above).
2. The path is right but our *starting* pose is wrong, so it climbs from the wrong place.
3. The original really does fly it above the overcast and the user is remembering a different
   mission or a later phase.

Ask the user what altitude the zeppelin appears at in the original before touching anything.
**Do not "fix" it by lowering the zeppelin** — that is content invention, the same trap as the C3
palms.

## C1: the police siren never sounds — a named sibling sequence is never dispatched

**User-reported 2026-07-22** (heard the waterfall and the train in C1/IA1, but not the police car).
**Confirmed and narrowed.**

The car itself is fine: `anim/debug: police_car at (-6868.4, 128.0, -5930.1) … visible`, driving
its route (and looping since polish-3 item 8).

The sound is not: `--debug-anim` reports
**`anim: 38 ambient sound emitter(s): snd_waterfall, snd_zepengine, snd_train`** — 38 emitters
built across only **three distinct names**, and `snd_police` is not one of them. So the emitter is
never created, which is why nothing plays.

**Why, most likely.** `extracted/C1/cam_anim/police_car-police_chase.json` is `activation:
OnStartup` and its `SoundNode { name: "snd_police", active_state: true }` sits inside a sequence
named **`siren_police`**. Its sequence list is `['start_walkin', 'siren_police', 'start_walkin']`
— **every sequence is named**. Compare a working emitter,
`passenger_trengine-train_on_track.json`, whose sequences are `['', '', 'steamplume', '', '', '']`
— **mostly unnamed**. That points at `AnimRuntime` running the unnamed/default sequence on
activation while **named sibling sequences wait for an explicit call that never comes**.

**Verify that before implementing** — it is a hypothesis about the runtime, not a measurement.
Check how `SequenceRunner` selects which sequence to run on activation, and count install-wide how
many `SoundNode`/other events sit in named-but-uncalled sequences; if the count is large this is a
systemic gap rather than one missing siren. Note `snd_police` is **not** a gamez node — but
neither are `snd_train` or `snd_waterfall`, which work, so that is not the discriminator.

**Instrument note:** `--mute` suppresses the audio session entirely and the emitter report then
reads `0 ambient sound emitter(s) [38 requested with no audio session]`, which looks exactly like
"the emitters are broken". Drop `--mute` when investigating audio.

**Confirmed 2026-07-22 from the script data — the "jumps" are cutscene cuts, not waypoints.**
The user watched it move smoothly for ~20 s, jump twice about 10 s apart, then vanish, and asked
whether the jumps were AI waypoints. They are not:

- `data-c1-m04-zrdr-introanm-pzep1-piratezep.zan.json` is **48 frames at exactly 1/3 s apart, a
  uniform straight line**: each frame steps a constant (−13.4, +2.1, −17.3), from
  (−5352, 1504, −1802) to (−5981, 1602, −2611) over **15.67 s** (≈66 units/s). Frame 0 and frame
  47 carry translate+rotate+scale; all 46 between are translate-only. There is no dwell, no
  branch and no waypoint structure anywhere in it — so the smooth phase is this script, and it
  simply **ends**.
- The jumps are therefore what happens *after* it runs out, and the dispatch graph says what
  that is: `camera1-scene1` and `piratezep-scene2` **both call `letterbox`** — the cinematic
  black-bars overlay — and `scene2` also calls `pfighter11` and `open_pzeplaunchdoors`, while
  `piratezep-pzep_launch_player` calls `pz_open_hanger_doors` / `pz_deploy_hook` /
  `pz_retract_hook`. That is the M04 **intro movie**: zeppelin flies in, cut, launch doors open,
  a fighter launches.

So each jump is a **hard cut between cutscene beats** — correct for a movie, nonsense as
gameplay — and "then it's gone" is the last beat deactivating it. This raises the confidence on
reading (1) above (do not bootstrap `mission_intro_animation` at startup) from "leading
explanation" to "very likely", since the same def demonstrably drives `letterbox` too. **Check
whether we are currently drawing letterbox bars in M04** — if so, that is the same bug with a
much more visible symptom, and a good confirmation test.

Still needs the user's answer on what the original shows during *gameplay* before anything is
changed.

**✅ User-confirmed 2026-07-22: "Yeah those are the cut scenes."** The open question above is
settled — the intro choreography is not gameplay state, and bootstrapping it is the bug.

**Scope, surveyed across all 53 missions' `startanims.zrd.json`: 13 bootstrap a cutscene def.**

| def | missions |
|---|---|
| `generic_intro` | 12 — C1/M05, C1B/M03, C1C/M01, C2B/M04, C3/M01, C3/M02, C3/M05, C4/M01, C4/M02, C4/M04, C5/M01, C5/M04 |
| `mission_intro_animation` | 1 — C1/M04 (the bespoke one) |

**No `IA1` and no `MP` mission names one** — all 13 are `M0x` story missions, so this is invisible
in instant action and multiplayer, which is what the project defaults to. That bounds the blast
radius neatly and explains why it went unnoticed until someone flew `--mission=M04`.

**Proposed fix:** skip those two def names when the animation bootstrap walks `startanims`, with
the reason recorded in code (we have no cutscene player, so their choreography would otherwise run
as world state). Small and data-driven — it is a name check against the start-anim list, not a new
subsystem.

**Verify by what disappears, not by what looks right:** in C1/M04 the pirate zeppelin should stop
executing scene1's 15.7 s flight and the subsequent hard cuts, and any `letterbox` bars should
stop being raised. Check the other 12 missions for *removed* motion too — `generic_intro` is
shared, so it may currently be driving things nobody has looked at. An 8-chapter regression will
not catch this: the default mission is IA1, which has no intro at all, so the regression is
inert here by construction (`docs/verification.md` §3, "the change is inert by construction in
that chapter").
