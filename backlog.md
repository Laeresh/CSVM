# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. **No plan is active** — the testing &
verification infrastructure, M3 (weapons), the M2/2.5 polish runs and the anim-debugger and
data-driven-crash plans are all complete and archived in `docs/plans/`. Per-item history/diagnosis
detail is in `docs/HISTORY.md` (dated entries) and `docs/architecture.md` (module bullets); how to
verify a change without fooling yourself is `docs/verification.md`. **The live list of hand-tuned
constants awaiting playtest lives here** (see "TUNE constants pending playtest" below) — it moved
out of CLAUDE.md on 2026-07-22, since CLAUDE.md's status section is current-state-and-next-step
only. When an item gets scheduled into a plan, move it there; when it lands, delete it here.

**Item IDs.** Every entry carries a flat `BL-NNN` tag, assigned once in file order and never
renumbered or reused, even when the item it names is deleted — so a stale cross-reference elsewhere
fails loudly instead of silently pointing at the wrong item. **Next ID to assign: `BL-250`.**
When adding a new item, take the next number and bump this line.

## Milestone 3 Polishing (playtest findings, 2026-07-24)

A pass of at-the-controls findings from the user (M3 weapons/destruction). **Six localized fixes are
now merged into `m3-polishing`** (built clean together), each still owing a cockpit playtest; the
**larger items** follow. The
fixes were code-only, so this section is their documentation trail. **When `m3-polishing` lands on
`main`, give each merged fix a `docs/HISTORY.md` entry and delete its row here** — do not let this
table outlive the landing.

### Merged into `m3-polishing` — pending playtest

| Finding | Branch | Change | Confirm in the cockpit |
|---|---|---|---|
| **`BL-001`** Rocket advances to the next pylon after one shot (round-robin) instead of draining the selected pylon | `fix/rocket-drain-pylon` | `FlightController.NextArmedHardpoint`: keep the cursor on the just-fired pylon (`_selectedPylon = idx`) instead of `+1`; the scan skips it once dry | wing empties one pylon fully, in order, before the next starts (see "Rocket firing order" under Feature backlog) |
| **`BL-002`** A rocket that reaches max range vanishes silently | `fix/rocket-detonate-at-range` | `Projectile._PhysicsProcess`: at range-expiry a **rocket** calls `Impact(weapon, pos, null)` (default-surface effect+sound); guns still expire silently | the `default` IMPACT effect reads acceptably as a mid-air self-destruct (not a ground/water splash floating in the sky) |
| **`BL-003`** Gun tracers appear behind the plane | `fix/tracer-grow-from-muzzle` | `RenderTracers`: cap the drawn streak to distance travelled (`min(TracerLength, Range−DistLeft)`) so it grows out of the muzzle | tracers start at the muzzle; steady-state tracers (round >14 m out) unchanged |
| **`BL-004`** Rockets feel too fast | `fix/rocket-speed-tune-hook` | adds a `Config` knob `weapons.rocketSpeedScale` (default **1.0 = data speed, no change**); when set, scales rocket velocity **and** accel so it still despawns at the same range | **value is a TUNE** — set e.g. `0.7` in `config.json` and A/B vs the original (see TUNE list) |
| **`BL-005`** `WARNING: … SOUND 'snd_exp_ground_a' … no audio session` on every ground crash | `fix/crash-sound-warning` | `PlaneViewer.BuildFlightCrashRuntime`: crash runtime gets `SoundHandledElsewhere = true` (matches the world-effects runtime) | warning gone; ground boom still plays (via `FlightAudio.OnGroundExplosion`) |

### Larger items — documented, not fixed

- `BL-008` **Break-apart debris barely moves — "parts only move a short way."** The piece launch is
  `MotionRuntime` (`CSVM/src/Mech3/Anim/MotionRuntime.cs`), and two things combine — **neither of
  them the FROM_TO dropped-delta bug** (that is a different event kind; the debris
  `translation.delta` *is* mapped):
  1. **World destructibles inherit no momentum.** `InheritedWorldVelocity` is set **only** by the
     plane crash (`FlightController.cs:1433`); the shared world `AnimRuntime` never assigns it, so a
     world piece gets only the small authored launch — a 5–10 m/s straight-up pop, which is exactly
     "the pieces barely drift."
  2. **Ground-rest / bounce is deferred.** `do_intersections` + `bounce_sequence` are not simulated
     (counted deferred at `AnimRuntime.cs:2034`), so a piece integrates freely over `run_time` then
     **holds its final pose** — translate a little, stop.
  ⚠ A third cause once listed here — "the magnitude decode is unsettled TUNE", `translation_range`
  xz/y read as distance ÷ run_time with `initial`/`delta` unmapped — is **settled and no longer a
  cause**: xz/y are an azimuth/elevation in degrees and `initial` the launch speed, `delta` a speed
  ramp (`docs/HISTORY.md` 2026-08-01, census of all 1,217 events). Do not re-open it. How much
  limpness is left after that fix is itself worth a look before this item is scheduled.
  **LARGER:** there is no single correct number — livelier world debris means either a world-object
  launch multiplier (a TUNE mirroring the crash's `WreckMomentum`) or implementing `bounce_sequence`
  ground-rest (the deferred Layer-1.5 physics-ray work). Both need an original-game A/B. Cross-ref the
  "Data-driven crash" TUNEs already in this file (`WreckMomentum`, tumble-rate, debris-arc).
  ⚠ Do **not** "fix" it by reviving the FROM_TO deltas — wrong mechanism.

- `BL-009` **C2 SeaHangar doors don't despawn and stay collidable after shooting the propane tank.** The
  SeaHangar doors are `sgh_door1`/`sgh_door2`, driven by `sghangar-opensgdoors` — a **HEALTH-0
  `OnStartup` "open the doors" animation, not a weapon-destructible.** A destructible is any def with
  `HEALTH > 0` (`AnimRuntime.cs:214-216`), so the doors are **never registered in
  `DestructibleRegistry`**; `Resolve` never maps their collider, no death sequence runs, so nothing
  hides them or removes their colliders — they open, then stay as solid set-dressing. The propane tank
  the user shot is almost certainly **Hollywood's `kkgate`** (its `ANIMATION_ROOT_NAME` is `propane`,
  HEALTH 10) — a *different* building, whose death chain (`genx12`/`tbridg*_fire`/`free_the_goose`)
  does not touch `sghangar`. **LARGER — a data/design gap, not a collider bug** (the collider-removal
  machinery is proven on `gate1`/`gate2` and `kkgate`). To settle: (a) confirm from the C2 gamez/zrdr
  whether any propane→`sghangar` chain is authored at all (docs show only `kkgate`'s); (b) A/B the
  original — does shooting a propane tank there destroy the SeaHangar doors?; (c) if it should, decide
  how — give the doors their own destructible def, or a chain-reaction `CALL_ANIMATION` firing an
  `OBJECT_ACTIVE_STATE` swap on them (the fuel-depot pattern). ⚠ The C25 plan text named
  `sghangar_doors` as an intended case, but the shipped C2 data does not make them destructible — an
  aspiration/data mismatch. ⚠ Even a working destructible door may leave wreck colliders — "clear
  passage" is its own playtest.

### Playtest pass 2 (2026-07-25) — new findings + verdicts

A second at-the-controls pass (user, answers "by feeling"; the four ambiguous points were grilled and
settled). Reference shots are in `OriginalScreenshots/` (gitignored — cited by filename). Root causes
were traced to code before writing. Verdicts on pass-1 items are noted on their rows above; the new
work is below.

**Impacts & surfaces (findings 3, 4).**
7. `BL-213` **Open fidelity question (was part of `BL-186`, whose splash look landed with `BL-203`,
   2026-08-01):** does the original splash when gun rounds range-expire over water? Needs a CAP of the
   original (fire out to sea from altitude, watch the 1000 m expiry point). Until answered, our rounds
   expire silently, which METHOD-18 documents as correct-per-data.
**Destruction & doors (findings 12, 13).**
12. `BL-022` **Debris trajectory is wrong, not merely slow — RE-PLAYTEST, the magnitude half is no longer
    a TUNE.** In-flight kills threw pieces "but not in the correct trajectory". **The largest cause landed
    2026-08-01**: `translation_range` was read as a distance travelled when it is an azimuth/elevation launch
    with `initial` the speed (`analysis/object-motion-range/`), which threw debris hundreds of metres along
    one bearing — visible as the `c1-destroy-effects` golden's line of fireballs marching up the runway.
    What remains of this item is what that fix does NOT cover: world objects inherit no launch momentum, and
    `bounce_sequence` ground-rest is unsimulated (both need a physics ray).
    ⚠ **Traps.** (a) Do not re-open the magnitude as a TUNE — the speeds are decoded and censused now; a
    piece that still looks wrong is the momentum or the ground-rest, not the launch. (b) `gravity.value` is
    absolute m/s² (a literal −9.8 on 173 events), NOT an offset to the aircraft's arcade `nom_gravity` of 20
    — that reading was considered and disproven by the same census.
    *Playtest after fix:* look for wreck pieces arcing along a correct trajectory, not just moving
    further. `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`.
**Test / debug affordances (findings 6, 14).**
20. `BL-142` **Re-tune `IndicatorLowFrac` for guns on its own merits, not the pylon coincidence.**
    The 0.34 threshold (`GaugeCluster.cs:76`) was picked so a 3-round rocket pylon steps
    green(3/2)→yellow(1)→red(0) — exactly the case that must now show NO yellow (see `BL-024`). Once
    hardpoints stop consulting this constant, its only remaining justification is "a gun group only
    warns near empty" (`GaugeCluster.cs:75`), which was never independently verified against a real gun
    belt's ammo curve (guns hold hundreds of rounds, not 3). *Fix shape:* after `BL-024` lands, re-tune
    `IndicatorLowFrac` by eye against a gun group draining from full to empty in flight — the
    pylon-derived value may or may not still be right.
    ⚠ **Traps.** Don't skip this because `BL-024` "already tunes it" — `BL-024` only splits the code
    path; it does not re-examine whether 0.34 reads well for a gun belt, since nobody has watched one
    drain past that fraction with intent to judge the colour step.

## Blocked / deferred

- `BL-245` **The other 379 bounce-terminated `OBJECT_MOTION`s are FALLS, not launches — no apex to
  solve, and a live `water`/`lava` surface table to choose between (split out of `BL-240` when the
  census separated them, 2026-08-02).** Same authored idiom as `BL-240` — no `RUN_TIME`, a
  `BOUNCE_SEQUENCE` naming the landing — but these start at rest or head downward, so
  `BL-240`'s return-to-launch-height solve yields `t = 0` for every one of them and leaves the bug
  exactly as it is today. Three shapes, censused over all 17,568 extracted defs:
  **335** `translation initial=(0,0,0)` with gravity −9.8 — a shot-down `gasbag1` or `cargozep1`'s
  `crashnode1` sinking to the ground, bouncing into `hit_ground1` or `hit_water1`; **~17** thrown
  downward at elevation −70…−90° (`lifesaver11`'s `lifeboat` → `boat_explode`/`boat_explode_water`,
  `b_turret1`'s parts); and **8** `chuteman` at `translation (0,−3,0)` with **gravity 0** — a
  constant 3 m/s descent, no parabola at all, ending in `deactivate_chuteman`.
  **Blocked on a ground ray**, and blocked on it twice: the fall distance is unknowable without one,
  and unlike `BL-240`'s 150 these carry populated `water`/`lava` branches, which need the struck
  collider to select. The engine already has both halves of the second problem —
  `ProjectilePool.ClassifySurface` (`Projectile.cs:377`) maps a collider's group to a
  `SurfaceClass`, and `Projectile.cs:296/625` shows the reusable ray query — so this is wiring, not
  decode, once something casts the ray.
  ⚠ Traps:
  - **Colliders are conditional.** `SessionSpec.cs:157` is
    `BuildsCollision => Fly || DamageTest || ForceCollision || DebugDamage != null` — a `--freecam`
    run and every golden-capture mode build **no world colliders at all**. A ray-based fix silently
    does nothing there, so it needs a stated fallback, not an assumption of ground.
  - **Do not give these a constant fall time.** Same trap `BL-240` carries: it would invent a
    landing altitude for 335 zeppelins.
  - `chuteman` has **zero gravity**. Any solve phrased as a parabola divides by zero on it; it is a
    constant-velocity descent and needs the distance, nothing else.

- `BL-222` **The `player` IMPACT surface class — the general got-shot feedback on your own airframe,
  authored on 44 of 48 weapons and untriggerable until something shoots back (found 2026-08-01
  while landing `BL-090` item 2).** `weapons.json`'s `IMPACT` block is keyed by surface class, and
  `player` ("the struck surface is the player's aircraft") is populated on 44 entries: most name the
  caliber's own `*_gunhit`, several name `f18sparks2`, and `wep_03` (60slug) names
  `SURFACE_ANIMATION: random_gun_impact` — the spark burst at a `pdpN` panel that B4 wired.
  `SurfaceClass.Player` already parses (`WeaponDefs.cs`) and `ProjectilePool` already classifies
  surfaces; what is missing is a shooter. **Blocked on M4's enemy aircraft**, not on data or decode.
  This is the *general* mechanism B4's goal described — B4 reaches it only through the Devastator's
  one-off 0.99 `injure_anims` entry, which is plausibly an authoring leftover
  (`docs/formats/vehicle.md`).
  ⚠ **Traps.** (a) `ProjectilePool`'s hit detection is a world raycast against a body-less plane —
  a round never hits an aircraft at all today, so this needs the aircraft to become a hittable body
  first; it is not a matter of adding a switch case. (b) Do not reach it early by firing the
  `player` effect off our own collision path — that is what B4's Devastator entry already does, and
  conflating "I was shot" with "I scraped a wall" would make both wrong. (c) `f18sparks2` is
  undecoded — check it resolves in the effect readers before assuming the class is fully wireable.

- `BL-030` **`docs/SCOPING-M4-ai.md` still names `PlaneViewer.cs:<line>`.** The C11 final sweep
  (PLAN-planeviewer-split) re-pointed the three `docs/formats/` hits to their real post-split
  owners (`WeatherRig.Build`, `WorldEffectsFactory.BuildWorldEffectsRuntime`) but deliberately left
  this one — it's a future-milestone planning doc whose line numbers were already invalidated by
  B7's extraction and the B8 `git mv`, so re-numbering it now is pure churn. Fix when M4 is picked
  up and the doc gets rewritten anyway.

- `BL-032` **Burning-object fires (`fire1`/`fire2` templates + `EFFECTS` flipbooks)** — **POSTPONED
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
- `BL-033` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

- `BL-034` **`SpinMotion` re-seeds its rest pose from an already-spun pose (found 2026-07-22, deliberately
  not fixed).** `SpinMotion` captures `_rest = target.Transform.Basis` from the CURRENT pose at
  construction, and the idempotence guard in `Dispatch` matches only on identical
  `(rate, runTime)`. `zeppelin_rocksleft` fires five events with five different rate/runtime pairs
  at the same `rock_zeppelin`, so each replacement motion anchors to wherever the previous one
  left the node, and a looping call drifts. It is **bounded** — rotation is orthonormal, so this
  can never produce the 1e27 blowup it was originally suspected of (that was the unread
  `spline_interp` flag, fixed 2026-07-22 — see `docs/HISTORY.md`) — but the drift is real.
  **Not fixed because both candidate fixes risk a visible regression to cure an invisible one,
  and the data does not adjudicate:** (a) seeding from `RestOf` would discard a deliberately-posed
  starting orientation on all 590 spins in the install — C1/M05's `random_prop` poses `propstill`
  to a random angle *before* spinning it, and that pattern would break; (b) inheriting the
  previous motion's `_rest` assumes the five rock events oscillate about a fixed pose, but a
  chained eased rock (accelerate, decelerate, reverse) is at least as plausible a reading, and
  under (b) each event would snap back to rest. **Needs the original game**: watch a zeppelin rock
  through several loops and see whether it returns to the same attitude or walks. Same class of
  call as `MissionSetup`'s unguessed `Object3DRotate` angle unit (`BL-249`).

- `BL-035` **Animation event kinds that need weapons or cutscenes — `CALLBACK`, `OBJECT_CYCLE_TEXTURE`,
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
  | one-shot `Sound` | — | **LANDED M3 D31 (2026-07-24).** `AnimRuntime.HandleSound` fires it as a fire-and-forget `WorldSounds.PlayOneShot`; the 4,378 `OnCall` + 1,650 `WeaponHit` combat audio now sound on deaths/hits, and the 21 `DYNAMIC_WEIGHTS` groups are decoded (`SoundDefs.LoadGroups`). No longer in the report. |

  **Pick these up when the thing they depend on exists** — a cutscene player for `Callback` — not
  before. `ObjectCycleTexture` needs neither; it needs a mission that
  actually builds a `taildamage` node, which none of the ones this project defaults to do.
  The one kind from that list that *was* reachable, `OBJECT_OPACITY_STATE`, landed 2026-07-22
  (`docs/HISTORY.md`) — which is why it is not in this table.

- `BL-036` **We ignore `zone_id` entirely** (found 2026-07-22). Every gamez node carries a `zone_id`:
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
  guess deletes visible world content, which is strictly worse than drawing both. The user's
  zone A/B has since answered **C5 = zone1** (2026-07-22), which would mean hiding C5's 149
  `zone3` nodes — but that is exactly the guess-what-to-hide risk, and the *fog* answer does
  not license a *geometry* change. C1–C4 are still unanswered. Do not act on this until the
  remaining chapters are settled and there is a visible artifact it demonstrably fixes.

- `BL-037` **`WorldPartitionSetActive` — ⛔ CORRECTED 2026-07-23. This entry used to claim it was "the real
  runtime system the original uses to pick between coarse and fine ground", and that claim is
  FALSE.** Measured: **all 25 uses are `support\c3\*.gw` — C3 only** — and it takes rectangle
  coordinates, not node names. It never appears in any C5 script, so it cannot be the mechanism in
  the chapter that actually has the bug. It was briefly promoted to "prime suspect" for the C5
  ground z-fight on 2026-07-22 and that promotion was wrong; the real mechanism is the **subface
  flag** (next entry). Kept as a correction rather than deleted because the wrong claim was
  repeated across three documents. Whatever `WorldPartitionSetActive` does for C3 is still
  unimplemented and still undescribed — but it is a C3 question, not a ground-LOD one.

- `BL-249` **`MissionSetup` does not apply the interp placement verbs — `Object3DTranslate` /
  `Object3DRotate`** (measured 2026-08-04; previously visible only as an aside in `BL-034`).
  Mission-script uses (build-side `load.gw` uses are baked into the shipped gamez and need
  nothing): `c1\m05.gw` ×20 — repositions the nine `lifesaver*` boats, `redcross` and
  `workersvoyagezep`, rotations unambiguously **degrees** (`0 45 0`, `0 172 0`);
  `c3\mp1.gw`+`mp2.gw` ×4 — places `cargozep1`, rotation unambiguously **radians**
  (`-3.144009` ≈ π on Y); `c5\mp1.gw` ×1 — moves `rearm_node_2`. Effect while open: flying
  C1/M05, C3/MP1–2 or C5/MP1 leaves those entities at the world corner / unrotated (the
  vehicles-load-unplaced mechanism in `docs/formats/interp.md`).
  ⚠ Traps: **the angle unit is per-script inconsistent in shipped data** — degrees in C1/M05,
  radians in C3/MP — so a single global unit guess mis-poses one set or the other; any fix needs
  a per-script (or magnitude-based) unit decision plus an original-game check. Last-write-wins
  ordering applies as everywhere in these scripts. No IA1/default mission uses either verb, so
  the goldens cannot catch a wrong guess — verify in the named missions directly.

- `BL-038` **`FogState` is a decoded animation event we do not act on** (found 2026-07-22). Fog **can** be
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

- `BL-181` **Marker HUD + scoreboard layout is a provisional pass, not a fidelity sign-off — pending
  the menu hub.** Playtested 2026-07-30
  (`./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`): `MarkerHud`/`StuntScoreboard` placement,
  fonts and distance units "work for now." The verdict is explicitly contingent on the menu hub not
  existing yet — once it lands, distance units, font choice and scoreboard layout may need to match
  its chrome rather than today's placeholder styling. Blocked on the menu-hub milestone, not on data.
  *Fix shape:* re-review `MarkerHud.cs`/`StuntScoreboard.cs` placement once the menu hub UI exists,
  against the hub's own type scale/units rather than in isolation.

## Open bugs (moved from NOTES.md 2026-07-22)

The user's running issue list, **swept again 2026-07-22 after polish run 3** — the six entries that
had landed, been disproven, or been measured gone were deleted (their records are in
`docs/HISTORY.md`), so what is below is live. Where the check pinned a cause it is recorded here so
it is not re-derived; where it did not, the item says so rather than guessing. Paths are relative
to the Godot project's `src/`.

**Most of this section moved into `docs/plans/PLAN-M2-polish-4.md` on 2026-07-22** — the knife-edge nose
drop, the C5 sunk zeppelin, the C1B z-fighting, the C1 IA1 oil tanks, the bowl sign, the C1 car
rotations, the focus-loss mute and the C2 Seaplane Hangar objective are all scheduled there, each
with the diagnosis that verification pass produced. **Do not re-add them here**; if one is closed
without landing, its record goes to `docs/HISTORY.md`. What remains below is what is still
unscheduled.

### Test infrastructure

- `BL-039` **One `c1-flight` golden run exited 1 silently, unreproduced (2026-07-30, during B7).** The shot
  built its world, rendered its first frame ([perf] startup line emitted), then the process ended
  with exit 1 before frame 120 — no PNG, no exception, nothing in `--log-file`, `.out` or `.err`
  beyond the known pre-existing `snd_police` warning. 3 of 4 full gates that day passed with the
  identical pinned hash; the only in-code `Quit(1)` (build-failure-with-pending-capture) cannot
  fire after a successful build. If it recurs, capture the run under a debugger or with
  `--verbose` before touching code.
  ⚠ Traps: don't attribute it to the concurrent-run collision — that failure mode is instant
  (0.9 s, LOG-13); this one died seconds in, with nothing else running.

### Surfaces, colliders and inspect tools (from the Wave D playtest, 2026-07-25)

### HUD & audio

- `BL-047` **Crash damage display blinks fully red.** `GaugeCluster.cs` blinks a zone for `DamageBlinkTime`
  on `OnPartDamage` and picks the red variant at `frac <= RedAt`, but the only caller is a graze
  hit — `FlightController.Crash()` touches audio, fireball, breakup and visibility and never calls
  into `Gauges`. So the all-red state is not a crash behaviour being mis-fired; it is the ordinary
  damage path left latched. Check what the original shows on a crash before wiring anything.
- `BL-048` **Gauge needles are the wrong shape** — they come from the game's own HUD textures. Could be
  drawn procedurally instead in a future Hi-Def mode.
- `BL-050` **`OBJECT_MOTION_FROM_TO`'s `*_delta` channels are silently dropped — all 26 of them.**
  `FromToMotion.Channel` reads a channel as `data.Obj(name)` and then looks for `from`/`to` keys.
  The absolute channels ship that shape (919/919 `rotate`, 401/401 `translate`, 663/663 `scale` all
  carry both ends). **The delta channels do not**: the compiled form ships them as a bare
  `{x, y, z}` vector — 15 `translate_delta`, 6 `rotate_delta`, 5 `scale_delta` install-wide — so
  `Channel` returns `(null, null)` for every one and the delta is dropped. The reader front-end
  (`AnimDefs.AddFromTo`) emits no delta channel at all. So the handler's delta arithmetic has
  **never executed**. Found 2026-07-22 while landing polish-4 item 2; deliberately not folded in,
  because it changes behaviour on 26 events and needs its own regression.
  ⚠ **Traps.** Do **not** "fix" it by making `Channel` fall back to `Vec3` without first deciding
  what a bare vector *means* — a `{from,to}` pair is a tween; a bare vector is most plausibly the
  `to` with an implied zero `from`, but that is a guess, and inventing semantics is this project's
  most-repeated trap. Read `docs/formats/anim-definitions.md` and the 26 actual payloads first.
  The `FromToMotion` docstring now says deltas compose on the HELD pose, which is coherent with the
  hold rule but **has never been observed**, precisely because they are dead — whoever revives them
  owns confirming that. And a live one is the only way `Seek`'s `rot *= Euler(...)` / `scale *= ...`
  lines get exercised at all, so a regression that never reaches one proves nothing about them.
- `BL-053` **`node_bias` already spans 1.22–2.86 priority levels per chapter** (C1 1.77, C1B 1.40,
  C1C 1.41, C2 1.24, C2B 1.22, C3 1.35, C4 2.07, **C5 2.86**). `docs/architecture.md` recorded
  this as an accepted corner case ("a prio-0 node >~4000 indices later can out-bias a prio-1
  overlay; no such pair observed in C1") — measured, the real span is **up to nearly three levels
  and is the normal state of the chapter**, not a corner case. Consequence: today **2–16% of
  cross-node conflicting pairs already resolve the wrong way round**, because within-mesh surface
  rank can out-bid the cross-node term. A dense conflict rank would fix this as a side effect
  (28 × 5e-6 = 1.4e-4 = 0.7 levels). Measured 2026-07-22, `analysis/item9-depth-bias/`.
- `BL-057` **`zone_set` is parsed by nothing** (`grep zone_set CSVM/src` → 0 hits). It is a **per-polygon**
  weather-zone membership list (C5 uses 1 and 3). Not a ground selector — that was checked and
  ruled out — but a real unparsed field, and the per-polygon granularity is interesting given
  weather zones are otherwise handled per chapter.
- `BL-058` **Open question, do NOT act on it from the subface report:** `cblock4/5/6` carry their own
  **disjoint** clutter building templates (`cb12a`–`cb24a`) versus `cblock1/2/3`'s
  (`cb00a`–`cb11a`). That is most likely *why* a fully-buried base ground layer exists at all. Once
  the subface fix lands, the base layer will be correctly hidden while **its clutter presumably
  still spawns** — check whether C5 draws doubled buildings, but treat this as a question, not a
  finding (the report flags it as an open question, not a measurement).

- `BL-160` **No Doppler on any 3D emitter today — open whether the original has one.** Godot's
  `AudioStreamPlayer3D` exposes `DopplerTracking`, but nothing in `CSVM/src` sets it: `WorldSounds.cs`
  constructs every ambient emitter (`:162-169`) and one-shot (`:211-218`) with only
  `Stream`/`UnitSize`/`MaxDistance`/`VolumeDb`/`AttenuationModel`, so `DopplerTracking` stays at
  Godot's default `DISABLED`. Own-ship engine/whine/rattle (`FlightAudio.cs`) are plain non-positional
  `AudioStreamPlayer`, which has no Doppler concept at all regardless. Distinct from `BL-079`
  (other-aircraft audio doesn't exist yet) — this is about the world's own moving `SOUND_NODE`
  emitters that **already play today**, e.g. the C1 train and `snd_police` on the police car.
  *Fix shape:* once a recording settles whether the original pitch-bends a fast pass, enable
  `DopplerTracking` (idle-tracking is enough for anim-driven hosts) on mover-hosted emitters only — a
  stationary host doing so would be a silent no-op, so gating by host motion is cheap.
  *Playtest after fix:* re-listen to a fast C1 pass (the waterfall for a pure-listener-motion test, the
  police car / train for an emitter-motion test) with `--debug-anim` running to correlate host speed
  against any audible pitch shift. *Blocked on `CAP-09`* (`playtest.md` §0).

- `BL-161` **`cockpit_engine_sound` / `damaged_engine_sound` ship per plane, unparsed.**
  `extracted/zrdr/vehicle.zrd.json` carries both alongside `engine_sound` for every plane def
  (Devastator: `snd_devastator_cp` / `snd_damagedengine` + `[0.0, 1.0]`, lines 21-36; repeated at
  lines 553-560, 870-877, 1166-1173 for the other planes). `PlaneStats.Load` reads only `engine_sound`
  (`PlaneStats.cs:190`) — grep for the other two names across `CSVM/src` returns zero hits. Clarifies
  `BL-080`'s wording.
  *Fix shape:* out of scope until a cockpit-audio or damage-audio feature is scheduled; recorded here
  so nobody has to re-derive from scratch that the *data* isn't the blocker.

## Feature backlog

- `BL-059` **Data-driven crash — the remaining variants/follow-ups (PLAN-data-driven-crash COMPLETE for the
  dirt crash, default since Wave 4 2026-07-23).** The dirt/ground crash plays `player_crash_dirt`
  end-to-end through the per-player scoped `AnimRuntime` — sparks, fireball cluster, black smokeball,
  the `flydirt`/`dust` burst (via the Layer-1 `OpacityFade` + `MotionRuntime` scale handlers), the five
  burning debris arcs, and the earth-impact boom all fire from the data. What is still open:
  1. **`bounce_sequence` re-launch (Layer-1.5).** The piece/debris `ObjectMotion`s carry
     `do_intersections` + a `bounce_sequence` (`pNhit` → `ground_mixed_exp_sg` + a second ranged
     launch) that `MotionRuntime` does not yet act on — it needs a ground-contact physics ray
     (`CrashBreakup.Advance`, on branch `bespoke-crash-animation`, is the reference integrator). The
     pieces tumble to rest fine without it; the bounce is an embellishment. Also needs a real
     `SOUND_GROUPS` resolver for `air_mixed_exp_sg`/`ground_mixed_exp_sg` (`snd_exp_ground_a` already
     plays, hardcoded like `plane_destroy_sg`).
  2. **The air variant.** `player_crash_default` — no-impact destruct, `destroyed=false`, pieces arc
     away, per-piece `large_firetrail` — has **no trigger**: it fires when the plane is destroyed with
     no impact at all, and nothing shoots the player down yet. A building crash is `_dirt`, not air, so
     `CrashSurface.Air` is deliberately unreachable from `ClassifySurface`, which reads a *struck
     body*. ⚠ Do not wire it off a low-HP test on the collision path — that is the ground crash with a
     different def. Data: `extracted/C1/cam_anim/player-player_crash_default.json`; the water half
     landed 2026-08-02 (`docs/HISTORY.md`), full decode there under 2026-07-23.
- `BL-228` **Schedule the `WAIT_FOR_COMPLETION` dependency — there is finally a reachable case
  (2026-08-02, from `BL-059` item 2).** The field is decoded (`docs/formats/anim-definitions.md`:
  flag `0x10` + an index into the caller's own `anim_refs`, always naming the call's own `name`;
  3,731 flagged events install-wide) and the runtime ignores it — every `CallAnimation` returns to
  the caller immediately. The sea dive is now the clean case: `player_crash_water`'s `destroy_crash`
  flags its `plane_big_splash` call and then calls `large_steam_spray`, and a capture shows both
  retargeting on the **same tick**, where the splash's own choreography runs 3.0 s. Implementing it
  means the sequence scheduler holds the caller until the callee's instance completes.
  ⚠ **Traps.** (a) 3,731 flagged events install-wide, all `OnCall`/`WeaponHit` — turning the wait on
  globally changes timing far beyond the crash and will move goldens; scope and measure before
  believing a screenshot. (b) `0` and `null` are **different authored states** (3,639 vs 53,019) —
  `0` waits on ref zero, `null` has no flag. (c) `wait_for_raw` is the fork's separate exposure of
  **unflagged stale values** (525 events); it is not a wait and must not be read as one.
  (d) "Completes" needs a definition for a def with no terminating event — decide it from the data,
  not from what makes the crash look right.
- `BL-229` **A splash emitter is killed on the tick it starts, by its own caller (2026-08-02, from
  `BL-059` item 2).** `plane_big_splash`'s `plane_puff_splash1` is three offset-less events:
  `ObjectActiveState sp_1 true` → `CallAnimation hg_splasher WithNode sp_1` → `ObjectActiveState
  sp_1 false`. Our runtime runs all three in one tick and logs `host 'sp_1' deactivated — emitter
  stopped`, so the `splasher` puffer emits nothing — yet `hg_splasher` authors a 0.5 s run
  (`StopSequence` at `Animation+0.5`, plus a `PufferState active_state 0` at `Event+0.1`), which is
  only reachable if the emitter survives its host's deactivation or the events do not share a tick.
  So either the host-deactivation→stop rule is wrong for an already-emitting puffer, or same-tick
  ordering is. The rest of the sea dive renders (splash mesh, ripples, steam, trails), so this is one
  missing emitter, not a broken variant.
  ⚠ **Traps.** (a) Do not "fix" it by dropping the host-deactivation stop wholesale — that rule is
  what stops emitters when a destructible's subtree is swapped out. (b) The same shape may exist
  elsewhere; census the offset-less activate/call/deactivate triple before choosing a rule, or the
  fix is tuned to one def. (c) Related to but not the same as `BL-228` — these events carry **no**
  `WAIT_FOR_COMPLETION` flag, so a wait would not explain them.
- `BL-060` **Improve on the original crash — the bespoke "breaking apart" (branch `bespoke-crash-animation`).**
  User's call (2026-07-23): the retired bespoke `CrashBreakup` wreck-scatter looked *better* than the
  faithful data-driven crash, so it was preserved on that branch rather than deleted. **The A/B playtest
  passed (2026-07-23) — the faithful data-driven crash is confirmed as the default**, so this is now the
  standing follow-up: once the faithful recreation is fully settled, revisit blending the branch's nicer
  breaking-apart (free-body scatter + down-ray ground-rest) into (or over) the data-driven path — an
  explicit "improve on the original" opportunity, not a faithfulness regression.
  ⚠ **Traps (from slices 1–2, 2026-07-23).** The `blend`/`softParticles` `Puffer.Create` overrides
  exist and default to a byte-identical shader — reuse them; a MIX-blend dark puffer near the
  ground also needs `softParticles: false` or the depth-fade zeroes it. A fading additive fireball
  reads as smoke in a screenshot — isolate the emitter (suppress the others, freeze the crash with
  no `--hold`) before believing an effect is present. Anchor at the plane centre (`pose.Origin` =
  `healthy`), not the impact point. For the debris arcs: `translation_range` is undocumented and
  `AnimRuntime` never simulates it, so the `fly_trailN` trajectory is a *reasoned reading* (`xz`/`y`
  = travel distance over `run_time`), not a settled decode — the anchor is invisible, only the
  trail shows, so the arc shape is TUNE, not fidelity; and the DISTANCE interval hides behind an
  inverted flag (`has_interval_value` false, key off `interval_type`).

- `BL-233` **The `DETONATION_DISTANCE` proximity fuse is DISABLED — decode it and re-target it in
  M4 (turned off 2026-08-02).** `Projectile.cs`'s `ProximityFuseEnabled` is `false`; the branch and
  `ProximityFuseTriggered` are intact and unreached. **Why it had to go now:** the fuse sphere-cast
  ran against *any* body, and in M3 the only bodies are terrain and buildings (the flying aircraft
  carries no physics body — `Projectile.cs` class remark), so it fired on **every** hardpoint shot,
  15-50 m short of the surface for rockets and 1 m short for the torpedo. Worse, the fuse branch
  calls `Impact(weapon, fusePoint, null, Vector3.Zero)` — a **null collider** — so
  `ClassifySurface` returned `Default` for every rocket/torpedo hit and **no hardpoint weapon could
  ever reach its `water`/`buildings`/`player` IMPACT entry.** Reported as "a torpedo in C1B water
  plays the ground explosion"; `wep_14` does bind `water → torpedo_water_effect` + `snd_bsplash`,
  and C1B's sea *is* tagged (`wtr*`/`srf*`/`wakefront*` all hit `SceneBuilder.ClassifySurface`) —
  the classification was simply never asked. Measured, `.scratch/logs/probe-20260802-160502-14692.out`:
  `impact: wep_14 (TORPDO) -> Default at (…) on / fx=torpedo_ground_effect snd=-` — the empty `on `
  field IS the null collider.
  **The two open questions for M4:**
  1. ~~**What is `DETONATION_DISTANCE`?**~~ **Settled from the data 2026-08-02: it is the FUSE
     TRIGGER distance, not the explosion radius** — so the current reading is right and only the
     targeting is wrong. Three independent reasons, in order of strength:
     (a) **`wep_12` CHOKER carries `DETONATION_DISTANCE` 35 and NO `IMPACT_PROXIMITY` at all**, with
     `ARMOR_DAMAGE`/`HEALTH_DAMAGE` both 0 and a `TANGLER RADIUS` of **35.0** — the same number,
     authored separately. The choker has no explosion, so a field meaning "explosion radius" on it
     is incoherent; a trigger distance that fires a tangle whose radius is its own field is exactly
     right. (b) **The torpedo runs backwards under the radius reading**: highest damage in the game
     by 2× (200 vs BOOM's 100) paired with the file's *smallest* `DETONATION_DISTANCE` (1.0).
     Biggest warhead + contact fuse holds; biggest warhead + smallest blast does not. (c) The whole
     `DETONATION_*` family is triggers — `DETONATION_TIME` (flare, 2 s) and `DETONATION_DOT_PRODUCT`
     (flare/flash/choker) are unambiguously "when does it go off", and the flare ships
     `DETONATION_TIME` with **no** `DETONATION_DISTANCE`: same slot, timed condition instead of a
     distance one. `IMPACT_PROXIMITY` is correspondingly the effect radius throughout, as
     `ApplyDamage` already treats it — `wep_09` FLASH is `DAMAGE 0` with IP 450 (blind radius),
     `wep_15` FLARE is `DAMAGE 0` with IP 500 (decoy-attraction radius).
     ⚠ **Why this hid for so long:** on the six plain rockets the two are *equal* (ARMOR/BOOM/BEEPER/
     SEEKER 15=15, SONIC 35=35) — "it goes off as soon as it is close enough to hurt you" — so the
     wrong reading produced right-sized results there. Full census, IP vs DD: 9M 25/30 (**the only**
     weapon where IP < DD), FLAK 22/20, FLAK 100/50, gb 155/15, FLASH 450/30, TORPDO 30/1,
     CHOKER –/35, FLARE 500/–, FW 100/–.
  2. **Which bodies may fuse a round?** The user's recollection is enemies only — aircraft and
     zeppelins, never terrain. That needs a targetable-body layer the world colliders are not on,
     which is M4 work (nothing else flies in M3).
  **When it comes back**, the fuse branch must carry its struck body into `Impact` instead of
  `null`, or it re-breaks per-surface effect selection the moment it is switched on.

- `BL-061` **World-effects runtime follow-ups (from M3 D32, 2026-07-24).** The world-effects runtime
  (`PlaneViewer.BuildWorldEffectsRuntime`) renders the impact/destruction **puffers**; one thread
  still open (numbering kept — other entries cite `item 2`; **item 3 disproven, M3 polish-5 B5,
  2026-08-02** — see `analysis/death-effect-closure/biggun-flying-parts.md`: `biggun_flying_parts`
  resolves cleanly onto the already-staged `zep_ng_dstry1.flt` and its callee is pure
  `OBJECT_MOTION` debris with zero `PUFFER_STATE` events; `fly_trail1..5`/`spurtpuffer1..5` belong to
  the unrelated rocket-trail/player-crash-trail effect family and were never reachable from this
  call. "started, built no puffer" is the correct result, the same bucket `flash_effect`/
  `rear_flash_effect` sit in):
  2. **The template MESH half.** The effects stage is hidden, so only the puffers render; the
     `gunhit` debris bits (`bit1`/`bit2`/`chunk` + their `OBJECT_MOTION`), the `he_ring` ground
     shockwave, and the `huge_splash_model`/`zep_ng_dstry1.flt` models do **not** show. Rendering them
     needs the mesh visible-at-the-site without flashing at the stage origin (per-def visibility, or a
     copied instance per call rather than a hidden shared template).
  ⚠ **Traps.** The `--effects-test` census is only reproducible **seeded** — several gun `*_gunhit`
  variants gate their puffer behind `RANDOM_WEIGHT`, so an unseeded run reports a different set each
  time (a manufactured answer). These effects **share puffer names** (`trailpuffer2` across
  `small_fireball`/`great_balls_of_fire`/`large_black_smokeball`) — since A1 (2026-07-31) the effects
  runtime keys emitters per-def (`DefScopedPufferKeys`), which unmasked 7 gun-variant builds
  (census 18 → 25 of 30); the WORLD runtime deliberately keeps the collapsed `(name, host)` key
  (see the `_puffers` field comment — def-scoping it stacked C5's six `m_crane_go` spark defs on
  one node and moved the c5 golden). The related "one live instance per effect def" limitation —
  a second damaged object's sputter restarting the shared def — **is closed for up to
  `EffectPoolSlots` (4) concurrent objects** by the template pool (`BL-225`, landed 2026-08-02,
  measured: 5 simultaneous `ap_h2otwr` kills serve 4 and recycle the 5th, which the runtime names).
  Beyond the pool the old collapse returns; the size is `BL-231` in the TUNE list.

- `BL-062` **Rocket firing order — drain the selected hardpoint, not round-robin (M3 polish, user
  2026-07-24) — the round-robin fix merged as `bea7947`.** The original fires **only the
  selected/current hardpoint, draining it fully before advancing** to the next; `NextArmedHardpoint`
  now keeps the cursor on the just-fired pylon until it is dry before the scan advances, confirmed by
  the D44 hide-breadcrumb (`pylon ordnance: pylonN dry`).

  **Still open — the H-selector design question (the "hardpoints won't cycle" finding).** H / D-pad
  Right *is* wired (`RocketSelectPressed` → `CycleWeaponSelectors`) but gated `_ordnanceTypes.Length >
  1`, and every stock loadout carries one hardpoint type (`wep_06`), so there is nothing to cycle —
  working as designed, not a bug. Settle from the original whether the player selects an individual
  **hardpoint** or the game just drains them in pylon order; meaningful mixed-ordnance cycling arrives
  with the M4 configurator (mixed loadouts). See "Milestone 3 Polishing".

- `BL-064` **Better mission states.** There is still a lot of difference between our maps and the
  original's. May need a pipeline to diff them, or to crack the mission loading states properly.
  (`MissionSetup`'s interp boot script, landed 2026-07-22, closed the largest single gap and
  incidentally fixed the C3 coast z-fight — but it is a boot script, not the full state model.)

- `BL-065` **M3-deferred gun mechanics — firing heat and cannon jam** (scoped out of
  `docs/plans/PLAN-M3-weapons.md` 2026-07-22, decision 4: friction with no combat pressure to justify
  it while nothing shoots back). **The constants are exact, so nobody needs to re-derive them:**
  `weapons.json` `FIRING_HEAT` on 4 entries (30-cal = 5.0); `vehicle.json` `cannon_jam` on
  `player_airplane` = `heat_safe_limit 1000`, `heat_dissipation_rate 50`, `jam_chance 0.1`.
  Heat accumulates per shot, dissipates at 50/s, and past the safe limit each shot has a 10 %
  jam chance. Pick this up when there is combat pressure — i.e. alongside or after M4 AI.

- `BL-066` **M3-deferred — ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- `BL-067` **The gun/hardpoint configurator UI** (deferred from M3, decision 9 — M3 flies stock loadouts
  only, but its loadout model is data-driven so this drops in without rework). The original's
  screens are `GUNS.SCRIPT` (4 gun slots, `gn_d_gun0..3`, engine callbacks 2249/2250) and
  `HARDPOINTS.SCRIPT` (2 hardpoint slots, `hp_d_point0..1`, callback 2245), plus
  `PLANECONSTRUCTION.SCRIPT` / `PURCHASE.SCRIPT`. Both are **pure UI layout** — per-plane slot
  counts, weapon costs and the economy are all executable-resident, so the *buying* half would
  have to be invented. The mount names are data (`IDS_AIRFRAMEGUNGROUPNAMES`, ui_strings
  3060–3079) and the per-plane stock table is authored, so the *placing* half is real.

- `BL-068` **M4 (AI) is scoped but NOT scheduled** — [`docs/SCOPING-M4-ai.md`](docs/SCOPING-M4-ai.md)
  (2026-07-25). The shipped AI data (patrol graphs, turret specs, AI vehicle rosters, generators,
  zeppelin combat parameters, 1,309 combat voice clips) is already in the extraction and **nothing in
  the engine reads any of it**; the document inventories it against the retail files, re-expresses the
  original design's AI specification, and grades what the engine can reuse unchanged. **Its headline
  blocker — the flying aircraft has no physics body, so nothing can shoot a plane — gates the whole
  milestone.** It is a scoping study, not a plan: scheduling it means renaming it to
  `docs/PLAN-M4-ai.md`. The per-pilot skill vector is located and bounded in
  `analysis/m4-ai-data/FINDINGS.md`. The items below are the M3-era leads it absorbs.

- `BL-069` **M4 dependencies discovered while planning M3** (2026-07-22) — recorded so they are not
  re-derived:
  - **Turrets are AI gunners**, not player-aimed: they acquire and engage other aircraft
    automatically (user-confirmed). This is why `extracted/zrdr/ai.zrd.json` contains nothing
    but `TURRET` defs. Five player planes carry one — `pavenger`, `pbalmoral` (two: front +
    rear), `pbrigand`, `pfirebrand`, `pkestrel` — and **W4 in the stock loadout table is filled
    on exactly those five and no others**. `vehicle.json` `turrets` gives `firstp`/`thirdp` node
    pairs (which mesh renders in which view, *not* a player camera mode) and **nothing else — turret
    rotation limits are not in any reader and stay undecoded**. `gun_pitch`/`gun_yaw` (±11°) are
    **not** the turret arc: they sit on AI aircraft defs including seven turret-less ones, on no
    player def including all five turret airframes, so they are the AI's forward-gun aiming cone
    (census in `docs/formats/vehicle.md`).
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
    (see `docs/plans/PLAN-M3-weapons.md` C23 for the dominance argument that settles it).

- `BL-070` **Residuals from polish-3 item 5 (2026-07-22) — all small, all deliberate.** *(Three of the
  original five — the `csky_fog_on` uniform ordering, the missing `csky_opacity`, and
  `FlightController`'s dead soft-tree branch — were scheduled into `docs/plans/PLAN-M2-polish-4.md`
  items 10 and 6 on 2026-07-22. These two remain.)*
  - **C5's `poleflare` clutter renders with the wrong billboard axis.** The `cblock*` templates
    ship `lightpole` (`CylindricalY`) posts *and* `poleflare` (`SphericalY`) glows — 33,682 of
    each in `cblock1` alone. `ClutterBuilder.Kind` carries no per-kind billboard mode, so every
    kind goes through the one Y-axis shader: the glows spin upright instead of facing the camera,
    and they get the SUNLIGHT night dim a light source should be exempt from. Now *detectable*
    (the shared `SceneBuilder.ClassifyBillboard` distinguishes the two), but fixing it means
    giving `Kind` a billboard mode and a second material path, and it changes how 139,388 C5
    sprites look with no reference shot to check against — so it needs an original-game A/B.
  - **Static collider probe is off by 6 (C4) and 11 (C5).** The probe replicated the world walk
    and reproduced the runtime collider counts *exactly* in 6 of 8 chapters, but predicted
    slightly more billboard exemptions than the game applies in those two. The safety conclusion
    is unaffected (the probe's candidate list is a superset and contains nothing solid), but the
    gap is unexplained rather than benign-by-proof. **⚠ Corrected 2026-07-22:
    `.scratch/probe_exempt.py` NO LONGER EXISTS** — `CleanScratch.ps1` swept it and `.scratch/` is
    gitignored, so there is no copy in git either. **Picking this up means rewriting the probe
    first**, which is why it was considered and dropped from polish run 4. When rewritten, the
    surface it must match is `WorldBuilder.NoCollisionNode` (`WorldBuilder.cs:69-70`) =
    `MeshUsesTexture(n, IsNonSolidSkyTexture) || IsBillboardNode(n)`; the likeliest source of the
    divergence is `IsBillboardNode` (`:99-109`), which does a node→`MeshIndex` hop and falls back
    to a 1-polygon flare-texture test, **plus the fact that the exemption is inherited by the whole
    subtree** (`SceneBuilder.cs:337` region, via `BuildSubtree` at `WorldBuilder.cs:353`). Two
    later changes a rewritten probe must also model: clutter collision was removed outright, and
    city-block clutter uses merged/shared shapes.

- `BL-071` **Skybox colour grading.** No tint, grade or tonemap is applied to the skydome anywhere —
  `WorldBuilder.BuildHorizon` only disables shadows, billboards the moon and disables light
  range-fade, and the `WorldEnvironment` sets background/ambient only. The dome does get the shared
  per-mission scalar dim `csky_world_light`, which is brightness, not grading. The decoded
  per-mission cloud tints are parsed and deliberately parked (`Weather.cs`, "unused this
  milestone") — they are the obvious input if this is picked up.
- `BL-072` **Paint scheme follow-ups** (the core landed 2026-07-20 — see `docs/formats/paint.md`
  "Known divergences"; these are the leftovers):
  - **The paint UI's "Shade" column** is unmodelled — three Colour *and* three Shade
    dropdowns exist in the UI, only three colours in the data. We ramp black → colour.
  - **A livery picker in the launchscreen.** Selection is CLI-only (`--paint=`); flight
    randomizes per player. Decide from playtest whether the menu should offer it.
  - **AI/ace liveries.** `ia.json` `ace_*` and the AI defs' own `paint_*` are parsed into the
    catalog but nothing flies them — there are no AI aircraft yet.
- `BL-073` **An altitude limit** — none is modelled, and the original's is a hard *clamp* on altitude
  at ~2003 m (measured, not the data's `flight_ceiling` 2500); see `BL-094` below for the
  measurement and for why a thrust fade is the wrong shape.
- `BL-074` **PLAYER_INIT fields [3]/[4] semantics + per-plane spawn speed** — story-mission spawns
  currently assume the IA convention (0.5 throttle / 53.6 m/s).
- `BL-075` **Sky UV scroll** (`h_zone*scroll`) — scroll rate unknown, not implemented.
- `BL-076` **Star twinkle + undecoded light fields** (flags 523/…, the 0.17 float) — stars/beacons
  render as fixed-size soft sprites, no twinkle.
- `BL-077` **Visual prop spin-up/down** (`startprops`/`stopprops` disc crossfade) — spawning mid-air
  already turning is by design; becomes relevant with a landing/shutdown flow
  (`FlightAudio.OnEngineStop` is already wired for the audio half).
- `BL-078` **Engine dual-stack chorus**: the original plays the engine loop as a ~5%-detuned pair
  (measured in the dive-video analysis, HISTORY 2026-07-19); ours is a single loop.
- `BL-079` **Positional 3D audio for other aircraft** — all sound is own-plane non-positional today;
  the original's IA traffic is clearly audible with Doppler in the reference video.
- `BL-080` **Future cockpit view** would consume a mix of already-parsed and still-raw data: `pcdpN`
  cockpit damage panels and the `*_damage_green/yellow/red` indicator anims are already parsed
  (PlaneStats parses them, DamageVisuals skips them). `cockpit_engine_sound` (`*_cp` WAVs, e.g.
  `snd_devastator_cp`) is **not** parsed anywhere — `PlaneStats.Load` reads only `engine_sound`
  (`PlaneStats.cs:190`) — see `BL-161`. `player_fuelleak` (0.85 threshold) IS already parsed, as a
  `VehicleInjureAnims` entry.
- `BL-081` **Runway `lite*`/`ltout*` lights-on/off state-variant quads** still z-tie (needs an
  engine-side light-state toggle to pick one variant).
- `BL-082` **Rail-over-transition z-nit**: one 6-poly rail patch NE of the C1 bridges sits below the
  draw-order tie-break's resolution.
- `BL-083` **Finished-pilot behaviour in a splitscreen stunt race** (M2.5 item 7): a pilot who clears
  every zone freezes at the finish showing their placing while the field flies on. It matches
  the solo run's freeze and makes the placing unmissable, but it parks a player with nothing
  to do for as long as the slowest pilot takes. The alternative — keep flying freely with the
  timer stopped — is a small change (drop the AllComplete early-return when `Race != null` and
  gate only the objective/marker updates). Decide from the two-controller playtest.
- `BL-084` **Race spawn fairness**: each player takes the next entry in the mission's `stunt_flying`
  spawn list, so pilots start at genuinely different distances from the first zone. Fine for
  a prototype, unfair as a race. Options: spawn everyone abreast from one point (the
  `--pos` `SpawnAbreast` fan already does this), or rank on a per-player-normalised time.

- `BL-150` **plan-sized — not a TUNE. Numpad camera views — the whole scheme needs a rebuild, not a
  retune.** Current implementation: `FlightController.cs:286-303` (`Views[]` table, keys
  Kp1/2/3/4/6/7/8/9 only — **no Kp0**), `:1653-1676` (`ActiveView()` — held key beats the scripted
  `PinnedView`, first array match wins on multiple keys down), `:1685-1690` (`ApplyFixedView` —
  instant snap, no smoothing, shares the chase camera's `ViewDist`); `--view=N` is the scripted,
  machine-verifiable twin. Cockpit testing (2026-07-30) overturned the "layout is settled" claim this
  whole scheme was built on and found five more open questions:
  (a) **Layout is wrong — MEASURED 2026-08-04 from the `CAP-07` scripted re-take, all nine keys.**
  The original's layout, read off nine settled stills (method and confidence below):

  | key | camera sits | ours today (`CameraController.cs:53-60`) |
  |---|---|---|
  | `Kp1` | **ahead + starboard, below** | left + below flank (no fore/aft term) |
  | `Kp2` | **dead ahead, level** | straight below |
  | `Kp3` | **ahead + port, below** | right + below flank (no fore/aft term) |
  | `Kp4` | **starboard flank, level** | left flank |
  | `Kp5` | **unbound — confirmed, not assumed** | unbound ✓ |
  | `Kp6` | **port flank, level** | right flank |
  | `Kp7` | **astern + starboard, below** | left + *above* flank |
  | `Kp8` | **directly below (belly plan view)** | ahead of the nose, looking back |
  | `Kp9` | **astern + port, below** | right + *above* flank |

  Three structural corrections, not a symbol shuffle. (i) **The original has no above-the-aircraft
  view at all** — every non-level position is below; our 7 and 9 are the only above views and both
  are wrong. (ii) **The four corners carry a fore/aft term our table has none of**: bottom row
  (1,2,3) is the forward hemisphere, top row (7,9) is aft. Ours splits them above/below the flanks
  instead, so this is a different *shape* of layout. (iii) **4/6 and 2/8 are both swapped** — 4 shows
  the starboard side, and 8 is the belly while 2 is the nose-on view.

  *How it was measured.* Nose-in-image direction plus which surface is visible fixes the quadrant
  analytically: with image-right = `u × d`, the nose projects with horizontal component ∝ sin φ and
  vertical ∝ −sin ε · cos φ (φ = azimuth from dead astern toward starboard, ε = camera elevation
  *below*). The level side views calibrate the sign — a camera to starboard must show the nose
  pointing image-right, and `Kp4` does. The four corners all show belly, underwing ordnance and the
  ventral skull fin, so ε > 0 for each; their nose directions are up-right / up-left / down-right /
  down-left for 1 / 3 / 7 / 9, giving the four quadrants above.
  ⚠ **Read the limits.** These are *quadrants and signs, not degrees* — no azimuth or elevation has
  been solved numerically, because that needs a field-of-view calibration this clip has not yet been
  put through. `Kp8` is the weakest: at a near-vertical elevation the azimuth is degenerate, so
  "directly below" rests on the plan-form silhouette being unforeshortened plus visible underwing
  ordnance (occluded from above), not on the nose-direction solve. One take, one aircraft — the
  layout is a per-key constant so that is fine for the table, but do not read distances off it.
  ⚠ **Correction, 2026-08-04: `Kp0` is rudder-left, not a camera view.** The 2026-07-30 cockpit
  session read it as a second 45°-underside-front view alongside 7 and concluded "the original binds
  0; we bind none" — that was a misattribution, and the *camera* half of it is withdrawn. Our
  omission of `Kp0` from `Views[]` is therefore **correct** and needs no change; the camera set is
  `Kp1`–`Kp9`. (Whether `Kp0`/`Kp.` should drive rudder at all is a separate input question this
  entry does not own.) The underside-front position stands for 7 on its own.
  (b) **Motion is wrong in kind, not just speed.** The original eases to AND from each position
  holding cam distance/radius constant, and the ease reads linear, not smoothstepped; ours snaps both
  ways (`ApplyFixedView` has no smoothing branch at all).
  (c) ~~**Distance is a TUNE hand-copied from the chase camera.**~~ **Resolved** — the fixed views
  now take the per-plane shipped distance with the chase camera (`CameraController`); the Bloodhawk's
  radius is its own 18.5, not 16.62. Nothing left to do here.
  (d) **Mid-ease interrupt behaviour is unmodelled.** In the original, pressing another key while
  easing back from a position goes straight to the new position (no snap to base first); holding
  several keys at once yields further, blended positions. Ours has neither concept — `ActiveView` is a
  single-view selector, so "combined" positions don't exist in the current code at all; this is new
  behaviour to design, not a bug in existing logic. Screenshots owed (key-combination stills).
  (e) **No gamepad binding existed in the original** (a right-stick/right-stick+modifier scheme would
  be invention) and **5 is unbound — now measured, 2026-08-04, not assumed.** Held for 3.5 s in the
  `CAP-07` re-take, the frame deviation from its own pre-press baseline is **0.310**, *below* the
  0.348–0.360 a no-key stretch of the same length scores, and against 2.5–10.5 for every key that
  does move the camera. Kp5 does nothing. Matches today's deliberate omission
  (`CameraController.cs:51-61`), so no code change — this line is now evidence.
  (f) **Numpad + / − trim camera distance slightly** — wholly new, unimplemented; the user already has
  video evidence for this one.
  *Fix shape:* a rebuilt `Views` table (the layout in (a)), an eased position/orientation update on
  top of `CameraController`'s existing per-plane radius, a small state machine for the
  interrupt/combination behaviour in (d), and the +/− trim as a new input.
  *`CAP-07` is discharged (2026-08-04) — (a) and (e) are answered above. Still blocked on `CAP-08`
  for (d)* (`playtest.md` §0), recorded with `analysis/capture-rigs/NumpadComboSweep.ahk`, which
  **staggers** each combination — first key alone until it settles, then the rest added on top — so
  the footage answers (d)'s actual question, whether a second key blends, replaces, or is ignored,
  rather than only showing the end state.
  ⚠ **`CAP-07` took two takes; the first is rejected and must not be re-analysed.** In
  `CAP-07 Numpad 1,2,3,6,9,8,7,4.mp4` the presses overlap: 10 camera transitions for 8 keys in
  20.9 s, with direct position-to-position lerps that never pass through base, so only 6–7 of the 8
  holds come to rest and the filename's key order cannot be mapped onto them one-to-one. The usable
  take is `CAP-07 scripted Run.mp4`, driven by `analysis/capture-rigs/NumpadViewSweep.ahk` — one key
  held alone at a time with a return to base between, and a `sweep-log.txt` that timestamps every
  press, so key windows are read from the log rather than inferred from motion.
  **(b) is still open even on the good take.** The ease is now measurable in principle — every move
  does start from a settled base — but no ease law has been fitted, because a per-frame camera angle
  needs a field-of-view calibration this clip has not been put through. All nine holds *do* settle:
  frame-to-frame motion over the last 1.2 s of each is 0.055–0.278 against a 0.090 baseline.
  ⚠ **Traps.** (a) **Only `--view=` is machine-verifiable** — live held-key input cannot be scripted
  here, so any fix to (b)/(d) is correct-by-construction only until played; do not close this off a
  passing `--view=` capture alone. (b) ~~**The layout (a) cannot be fixed before the owed screenshots
  pin the exact mapping**~~ — **resolved**: (a)'s table is measured. What survives of this trap is
  the warning it carried: the fix is **not a symbol swap**. The corners gain a fore/aft term they do
  not have today and every above-the-aircraft view disappears, so recode the table from (a) rather
  than permuting the existing rows. And the table above is quadrants, not degrees — the exact
  azimuth/elevation still wants an FOV-calibrated solve. (c) Don't retune
  the chase radius here in isolation — the fixed views and the chase camera share one number in
  `CameraController` by design; a fix landing only in one place desyncs the two cameras again. (d) Combined/multi-key positions are UNDESIGNED,
  not merely unbuilt — resist inferring a formula (e.g. "average the two directions") from a single
  set of screenshots; get the key-combo captures first.

- `BL-165` **The sun renders no lens flare; the original does.** Confirmed absent: `Launcher.cs:569`
  builds only a plain `DirectionalLight3D` (`Sun`) + a `WorldEnvironment` with no glow/bloom
  configured. Grepping `CSVM/src` for `flare`/`glow`/`bloom` turns up only the wingtip nav-lights
  (`WingLights.cs`), world lamp/beacon glow sprites (`gen_flare_yellow`, `poleflare`,
  `docklight_flare` — `SceneBuilder.cs:771`, `WorldBuilder.cs:403`), and gun/rocket effects — nothing
  tied to the sun.
  *Candidate asset, engine-bound (upgraded 2026-08-04):* every chapter's texture archive ships a
  lens-flare-shaped set — `bigflare01`/`bigflare02` (large core discs) + `lflare1`..`lflare4`
  (small secondary rings) — and the interp boot scripts **register the small set by verb**:
  `support\c2\init.gw` and `support\c3\init.gw` each carry `LensFlareTexture 0 lflare1` …
  `LensFlareTexture 3 lflare4` (plus `LightMapTexture lightmap`), binding `lflare1`–`lflare4` to
  flare element slots 0–3 in that order. No gamez node, material, or cam_anim/zrdr def references
  them, consistent with a hardcoded screen-space effect fed by these registered textures. Open
  oddity: only C2/C3 carry the lines — the other chapters' `init.gw` scripts register nothing, yet
  the original presumably flares everywhere; `bigflare01`/`02` remain a naming-convention lead only.
  *Fix shape:* a screen-space flare rig keyed off the sun's view-space direction (sprites strung along
  the sun→screen-centre vector), textured from the candidate set above, gated by an occlusion check
  (terrain/plane in front of the sun kills it).
  *Blocked on `CAP-13`* (`playtest.md` §0).
  ⚠ **Traps.** (a) Do not confuse `gen_flare_yellow` (a town-lamp light-source glow node) with this —
  same texture-naming family, unrelated purpose. (b) No def confirms `bigflare01`/`lflare*` are
  actually the sun's flare set; verify against a reference capture before committing to it. (c) Needs
  a reference capture with the sun in frame before implementing — the streak count/layout is unknown
  without one.

- `BL-172` **Graze pushback is entirely unmodelled — and the shipped data already has the constant to
  bind it. Plan-sized — not a TUNE.** `FlightController.SurviveHit` (`FlightController.cs:1435-1535`)
  only ever does a friction-scaled tangential slide (`GrazeFriction`), a lever-arm attitude kick
  (`GrazeKick`), and a fixed `GrazePushOut` (0.15 m) off the surface — there is no
  restitution/repulsion term along the normal at all. Meanwhile `player.json`'s `crash` block ships
  **`bounce_factor 0.6`** alongside `armor_damage_range [50,300]` / `health_damage_range [50,300]`
  (`docs/formats/vehicle.md:65,90-149`), and nothing in `CSVM/src` reads any of the three (`BL-095`'s
  trap (a) already flags this — grep confirms zero hits for `bounce_factor` under `CSVM/src`).
  *Fix shape:* add a restitution impulse along the contact normal scaled by `bounce_factor`, alongside
  the existing tangential slide — this turns "invent a pushback mechanic" into "bind the shipped
  constant." *Blocks:* the collision-feel sign-off.
  *Playtest after fix:* grazes vs crashes should feel fair against the original, including behaviour
  against building corners (`CAP-14`).
  ⚠ **Traps.** (a) `bounce_factor`'s units are unverified — `BL-095` flags the whole `player.json`
  physics block as needing its own decode pass; do not assume it is a plain coefficient of restitution
  without checking the range against a measured graze. (b) This is coupled to the armour question
  (`BL-085`/`BL-173`): the same `crash` block also carries the `armor_damage_range`/
  `health_damage_range` pair that collision damage is supposed to spend, so a full fix likely lands
  both the pushback and the armour-aware crash/graze damage together rather than as two independent
  patches. (c) `GrazeStopSpeed`/`GrazeFriction`/`GrazeKick` were tuned against the *current* no-bounce
  slide — expect them to need re-tuning once a normal-direction impulse is added, not to survive
  unchanged.

- `BL-173` **The damage-gauge outer/inner rings are synced because there is only one value to drive
  them — splitting them has nothing to split yet. Plan-sized — not a TUNE.** `GaugeCluster.Draw`
  computes one `frac = PartFraction?.Invoke(z.Part)` per zone and feeds the **same** `color` index to
  both `z.Border` (outer ring) and `z.Fill` (inner hatch) (`GaugeCluster.cs:263-268`) — there is
  structurally one number per zone, not an armour/health pair. **The data does ship a real pair** —
  `destroyable_parts` is (hit points, armour), settled 2026-08-03 against the original's armory
  (`docs/formats/vehicle.md`, "The hp pair: armor + hit points"). So the two rings have something
  distinct to bind to; what is missing is the receiving model. **Blocked on `BL-085` landing a
  two-pool `PlaneDamage`** — not on an open question, and not an independent TUNE. *Fix shape:*
  drive `Border` from the armour pool's fraction and `Fill` from health's (or vice versa — confirm
  against `MSG_HUD_HEALTH` = "Armor: %1%% Health: %2%%").
  ⚠ **Traps.** (a) Do not fabricate a synthetic armour/health split from the single existing `Hp` pool
  (e.g. armour = first N%, health = remainder) — the real second number is in the data and
  `BL-085` is what surfaces it; a synthetic split would encode a balance guess instead. (b)
  `GaugeCluster.cs` is at its 3-⚠ cap in `docs/architecture.md` — this entry records the gap, it does
  not schedule a `GaugeCluster.cs` doc change.


### Combat-fidelity gaps found by a design cross-check (2026-07-25)

Systems whose data ships complete and whose engine half does not exist, found by reading the
original **pre-release** design spec against the code. Every claim below was re-verified against
`extracted/`, `CSVM/src` or a retail capture first, and each entry says which half it rests on.

⚠ **How much to trust that document, measured across this pass and earlier ones.** Its
**structural** claims have held up against our data — per-hardpoint cluster sizes, the 8-firepoint
rig, the zeppelin launch-altitude gate, the two-volume danger zones, the armour/health damage
split. Its **per-item art and balance numbers have repeatedly failed** — gun ranges, rocket speeds,
zone hit points, the crash fireball's timing, and shell ejection's calibre gate and mount position.
**So: take the mechanism from it, never the magnitudes or the art direction, and prefer extracted
data or an `OriginalScreenshots/` capture wherever either exists.** Where an entry below rests on
the document alone, it says so and marks the value TUNE.

- `BL-239` **Blast falloff measures to a body's transform ORIGIN, not to the geometry the blast
  actually went off against — so a big body soaks up less splash than a small one, or none
  (found 2026-08-02 while reading `ApplyDamage` for `BL-233`).** `ProjectilePool.ApplyDamage` sweeps
  a sphere of `IMPACT_PROXIMITY` around the detonation point, then scores each caught body by
  `BlastDamage(full, radius, zonePoint.DistanceTo(point))` where `zonePoint` is
  `DamageZonePosition(body, shapeIndex)` = `(GlobalTransform * ShapeOwnerGetTransform(owner)).Origin`
  — a single point, the shape owner's transform origin. For a compact damage zone that reads right.
  For a **large** body it does not: a rocket detonating against one end of a long mesh scores its
  distance from that mesh's origin, which can be tens of metres away, so the body takes a fraction
  of the damage it should — or falls outside the sphere entirely and takes **none**, despite the
  blast going off on its skin.
  **Two things keep it from being visible today**, and both are load-bearing to check before
  believing a fix: (a) the **directly struck** body is exempt — it takes full `HEALTH_DAMAGE` and is
  then excluded from the sweep (`body == struck` → `continue`), which is correct (the ray contact IS
  the detonation centre) and means a plain aimed hit is unaffected; only *splash onto neighbours*
  is wrong. (b) `MaxBlastBodies` is 4096 and the radii are 15-100 m, so nothing is being silently
  dropped for capacity.
  **Expected to bite on** zeppelin gasbags and the large chapter building/terrain meshes — exactly
  where splash matters most and where `DAMAGES_ZEPPELIN` (`wep_14`/`wep_28`) points. Not measured
  against a case yet: that is step one.
  *Fix shape:* score against the nearest point on the body's collision shape rather than its origin
  (Godot's `GetRestInfo`/`CollideShape` on the blast sphere returns contact points), or per damage
  **zone** where a rig has them. ⚠ Do **not** just widen the radius to compensate — `IMPACT_PROXIMITY`
  is authored data (and its falloff SHAPE is already the open TUNE `BL-227`); inflating it to paper
  over a distance-measurement bug would corrupt both.
  ⚠ Trap: the comment at `DamageZonePosition` explains why the *detonation centre* is the ray
  contact rather than a collider origin — that reasoning is about the blast's own position and is
  correct; it does not license using an origin for the RECEIVING side too.

- `BL-085` **The armour layer is unimplemented, so 18 of 48 weapon entries are mis-modelled.**
  `WeaponDef.ArmorDamage` (`WeaponDefs.cs:78,239`) has exactly two consumers and both are display
  strings — `--dump-weapons` (`Probes.Weapons`) and the weapon lab's panel readout
  (`WeaponLab.WeaponSummary`, the `dmg h…/a…` field). The
  receiving side is one pool: `PlaneDamage.PartState` is a single `float Hp` and `Apply` a flat
  subtract (`PlaneDamage.cs:21,54-60`). Consequence, measured: **18 `BALLISTICS` entries carry
  `ARMOR_DAMAGE != HEALTH_DAMAGE`**, and that split *is* the player ammo-type system
  (`docs/formats/weapons.md`, "The player damage matrix") — with only health modelled,
  armour-piercing is strictly the **worst** round in every calibre (`30 AP` 1.5 health vs `30 DD`
  4.5), so the tier is inverted, not merely simplified. Wanted: two pools per damage zone, **armour
  first, with overflow spilling into health 1:1 within the same shot** so a nearly-stripped zone
  never wastes damage. The armour-then-health *order* is settled twice over: C23's dominance
  argument (`docs/plans/PLAN-M3-weapons.md`) and, directly, retail string 3372 — "AP rounds tend to
  punch clean through unarmored surfaces, inflicting very little damage". The overflow rule is the
  spec's.
  **Unblocked 2026-08-03 — the pool's origin is now a finding.** The armour pool is
  `destroyable_parts`' second value. The original's **armory** settles what the shipped data could
  not: its per-zone allocation is in units that are armour points 1:1, and a **stock** airframe
  reads the same per-zone numbers the zrdr def carries (stock Bloodhawk ~20 per zone;
  `pbloodhawk` is 20/20/20/20). Observed at the controls. See
  `docs/formats/vehicle.md`, "The hp pair: armor + hit points".
  **`CAP-19` flown and discharged 2026-08-03** — the mechanism is now observed rather than inferred:
  **armor depletes before health**, the armory's **per-zone cap is 60 units** (uniform across a
  plane's four zones), and a stripped zone visibly falls faster than an armored one. The capture is
  retired. It also **refuted** the standing reading that `ARMOR: Standard (N/T/W)` is a per-zone cap
  — 60 is uniform and far smaller than any blurb triple — so what that triple is went back to open
  (`docs/formats/vehicle.md`); nothing in this item depends on the answer.
  ⚠ One sub-question of that capture went **unrecorded** and is not worth its own capture: whether
  the in-flight `Armor: %1%% Health: %2%%` readout reads 100 % regardless of how many units were
  bought, or scales against the 60-unit cap. It decides only how `BL-173` normalises the gauge's
  outer ring — settle it whenever the armory is next on screen.
  ⚠ **Traps.** (a) **Do not fold armour into hp.** The two are equal on all 88 shipped entries
  **at stock only** — armour is a *purchasable* quantity (`BL-067`'s configurator), so equality is
  a fact about default loadouts, not about the model. `PlaneStats` must read **both** floats
  (`PlaneStats.cs:249-256` currently takes the first and drops the second) and `PlaneDamage` must
  carry two independent pools, or this breaks the moment an armory exists. (b) **The 2× is
  intended, not a regression — and it is entailed, not a pending measurement.** A real armour pool
  doubles every zone's effective HP against a balanced round; that is what the original does —
  record it as faithful rather than tuning it away (decided 2026-08-03). ⚠ **Do not file a capture
  to "measure" the factor.** With armour equal to hp at stock (all 88 entries) and armour spent
  first with 1:1 overflow (observed, `CAP-19`), the 2× *follows arithmetically* from those two
  confirmed facts. A live sortie moves ammo type, hit distribution, graze damage and pilot skill at
  once and cannot isolate a time-to-kill figure; `CAP-19` confirmed the direction (a stripped zone
  falls far faster) and was discharged on that basis, 2026-08-03. (c) It **supersedes C23's
  model 1** ("player planes — per-part HP, no armour/health pair"), which is now wrong;
  `player.json`'s `crash` block spending **`armor_damage_range [50,300]` and
  `health_damage_range [50,300]`** on the player's own collision damage fits the corrected model.
  Nothing in `CSVM/src` reads that block. (d) World destructibles carry **health only** (C23,
  measured over 16,114 defs) — this entry does not touch them.
  **Collision damage bypasses armour too, not only guns.** `FlightController.SurviveHit`
  (`FlightController.cs:1455-1456`) computes a hand-authored `GrazeMaxDamage * (vn/CrashSpeed)²` and
  spends it through the same single-pool `PlaneDamage.Apply` — no armour concept exists on the
  collision path either. `Crash()` (`FlightController.cs:1309-1335`) doesn't consult `PlaneDamage` at
  all; a hard hit is a pure boolean destroy. So `player.json`'s `crash` block
  (`armor_damage_range`/`health_damage_range`/`bounce_factor`) is unconsumed on **every** axis —
  weapons, grazes, and crashes alike. See `BL-172` for the pushback half of the same block.

- `BL-226` **The incoming-fire cue set's other two halves are blocked on things that do not exist
  yet.** The near-miss third landed (`BL-087`, 2026-08-02); `bullet_hit_sg` (= `snd_ricochet1-4`,
  `player.json`'s `bullet_hit_sound`) and `window_hit_sg` (= `snd_windowhit1-3`, non-3D) did not.
  Both sit on the five `player_pfighter-bulletN` canopy-hole defs (the `bullethole_anims` of
  `docs/formats/vehicle.md`, 10 files per chapter × 8), so they are shipped and referenced, not
  orphans. The design's rule is that incoming-fire intensity is how the player reads a shooter's
  distance, calibre and ammo type; the accumulator that rates it is now decoded and running
  (`WarningShotCue`), so both cues can hang off it once their blockers clear.
  ⚠ **Traps.** (a) **The blocker for `bullet_hit_sg` is that nothing can shoot an aircraft:** a plane
  exists in physics only as a `CastMotion` query shape (`PlaneCollider`), never as a body, so a
  projectile raycast can never strike one — own plane or another player's. Giving aircraft real
  bodies is the prerequisite, and it is not a small change (every round currently passes through
  every plane, including the firer's own). (b) `window_hit_sg` additionally needs a cockpit view —
  the bullet defs are `PlayerFirstPerson`-gated. (c) **Do not fake either off our collision path**:
  firing the hit cue on a wall scrape conflates "I was shot" with "I hit something", the trap
  `BL-222` records. (d) Only `snd_warningshot1-3` are true orphans (in no `SOUND_GROUPS` entry and
  named nowhere) — do not conflate the four groups.

- `BL-088` **Danger Zone scoring uses one sphere where the original used two gate volumes.**
  `StuntMission.Update` tests one point against `DzRadius` 15 m, order-free
  (`StuntMission.cs:65,293-301`). The `dzpathN` mesh's two identically-materialled polygons are the
  zone's **entry and exit apertures** — the spec confirms both must be crossed, specifically so a
  tangential clip cannot score. A faithful test is therefore an **ordered pair of polygon-plane
  crossings**, not an extent-derived radius; that is the fix for the "you fly *around* the danger and
  still score it" half of the `DzRadius` TUNE (see "TUNE constants pending playtest" → Stunt mode for
  the three measured leads — do not re-derive them).
  **Data-confirmed across all 15 C4 `dzpathN` meshes** (model indices 863–877): every one is exactly
  3 polygons. The gate pair always shares one material (index 427, solid red 243/0/0); the
  route/approach-exit line is the odd one out on material 84 (solid white). ⚠ **The route is NOT
  reliably polygon index 0** — that holds for only 2 of the 15 C4 zones (`dzpath1`, `dzpath9`); in the
  other 13 the route is polygon index 2. **Identify by material class, not by index**, when writing
  the crossing test.
  ⚠ **Traps.** `DzRadius`'s docstring claims "the original has no gate geometry" — **false**;
  delete that line when this lands. Under a gate-pair test the `dzN` marker becomes a **HUD anchor
  only**, so the recorded marker↔gate-midpoint discrepancy (826 m on C1 dz2) **stops mattering** —
  it is not a blocker, and the marker must not be "fixed" onto the gates: `MarkerHud.cs:144,145,158,212`
  and the scoreboard still consume the marker point. Do not retune `DzRadius` here (hand-tuned by
  the user). **Ordering is a separate, unsupported case:** the spec says at least one original
  mission required its zones in strict order, but shipped `dzones` is a bare `[dzpathN, dzN]` pair
  list with no order field (checked C1/IA1), so any ordering was mission-scripted or engine-side —
  untestable until campaign missions exist, and our model is order-free.
  *Playtest after fix:* gates trigger where the danger is, no score on a clean miss, no zone that
  cannot be gated fairly. `./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`. *Blocks:* signing
  off the gate implementation.

- `BL-089` **Nitro booster — scoped, low priority (the user's standing call).** Recorded because the data is
  complete and waiting, not as a discovery. Shipped: `MSG_CMD_NITROUS` ("Use Nitro-Booster") is a
  bindable command and `MSG_HUD_NITRO` ("Nitrous: boost: %1 charge: %2") its two-value readout;
  `nitrogauge` is in `instruments.zrd.json`'s cockpit layout and all 11 planes carry
  `nitrogauge` / `nitro_backplate` / `nitro_boost` / `nitro_charge` gauge nodes in planes.zbd;
  `nitro_boost` / `nitro_decay` are ON_CALL anim defs in `plane_props.zrd.json` (with `ai_nitro_*`
  wrappers) firing `snd_nitrostart` / `snd_nitrostop` at `nitroprop1` and driving `nitropuff1-4`
  plus `spin_nitrorotor1-3`; `snd_nitro` is a LOOPED 3D loop (`RANGE [30,400]`). `PropParts`
  already classifies `nitropropN`, and both `PlaneBuilder` and each plane's own `RESET_STATE` ship
  it INACTIVE — so the visual half is one `Kind.Nitro` unhide away.
  ⚠ **The numeric tuning stats did not ship.** No nitro key exists in `vehicle.json` or
  `player.json` — boost magnitude, charge capacity, burn rate, recharge rate and the speed cap are
  executable-resident, so this needs a hand-tuned balance pass A/B'd against the original, not a
  data port. (`rof/ui_strings.json` carries "NITRO-BOOST: %4!s!" on the purchase screen and the
  buyable engines come in plain and "… nitro" variants, so the engine choice is what grants it.)

- `BL-090` **Small per-impact feedback gaps — three landed (item 4, shell ejection, C22
  2026-07-31; items 3 and 2, the glancing-collision reaction and the per-impact spark burst,
  2026-08-01; item 1, `damaged_engine_sound`, B5 2026-08-01) — one remains,
  with the data already shipped.** Grouped because each
  is a few lines of wiring against an authored def and they share one theme: the moment something
  hits the plane, or the plane touches something, is under-communicated.
  5. **`snd_dangerzone_camera` is a data-orphan with a ready trigger.** `dangerzone_camera.wav`,
     SFX, non-3D; in no `SOUND_GROUPS` entry and named by no world data. `StuntMission.Complete` is
     the obvious hook. ⚠ Confirm against the original that it is the zone-cleared cue and not a
     replay-camera sting — the name argues for the latter.
  ⚠ **Dropped from this group after checking — the "fireball leads the crash explosion by 0.5 s"
  claim does not survive the data.** In `player-player_crash_dirt.json` the `Sound snd_exp_ground_a`
  event is authored **before** the `large_fireball` calls (which cascade at +0, +0.25, +0.25,
  +0.25), and `large_fireball` carries no sound of its own. A lead in `FlightAudio.OnCrash` is a
  spec claim the shipped choreography contradicts — do not add one.

- `BL-091` **`sticky_bullet_*` — a shipped aim-assist system nothing reads.** `player.json` carries
  `sticky_bullet_catchup_rate 5.0`, `sticky_bullet_inaccuracy 1.0`,
  `sticky_bullet_forget_interval 1.5`, `sticky_bullet_dist_factor 0.0`; **zero references anywhere
  in the repo.** The names read as bullet magnetism toward a tracked target — how fast rounds catch
  up, a scatter term, and how long a round remembers its target — with distance attenuation shipped
  **off** (`dist_factor 0`).
  ⚠ **Traps.** `player.json` is the *global* tuning file, not a player-only one (it also holds
  `min_ai_active_dist`, `ai_groundblow`, `ai_skill_parameters`), so whether this assists the
  **player's** gunnery or the **AI's** is unsettled — settle that first, because "your bullets
  curve" and "their bullets curve" are opposite feel promises. Untestable either way until aircraft
  are hittable (see the incoming-fire entry). **The cheap first half is documentation:** no
  `docs/formats/` page covers `player.json`'s combat/AI globals — `vehicle.md` documents only
  `nom_gravity` and `sounds.md` the curve blocks — so `warning_shot_*`, `sticky_bullet_*`, the
  `crash` armour/health ranges and `respawn_rad`/`respawn_el` are all undocumented shipped tuning.

- `BL-141` **`shell1.png`/`shell2.png` — the doc's own listed "tracer" texture pair — are wired to
  nothing: not `gunshell`, not any reader def, not any engine code.**
  `docs/formats/weapon-effects.md:148` groups them under "Tracer" textures. Traced the actual
  consumer: `extracted/C1/gamez/textures.json` indices 104/105 → `materials.json` indices 108/109
  (`Textured`, `texture_index: 104`/`105`) → `models.json` model 60 → `nodes.json` node 205 (`g1`),
  whose parent chain is `rabbit_blur` (203) → `g11` (200) → `rabbit_blur` (198) → … — a recurring
  generic-named mesh chain with **no relation to `gunshell` or any weapon node by name or parentage**.
  Grepped `CSVM/src` and every `extracted/*/cam_anim/*.json` / `extracted/*/*/zrdr/*.json` for
  `shell1`/`shell2`/`rabbit_blur`/`gunshell`: only the texture files and this one material/model pair
  exist; nothing calls, anchors, or names them from any weapon-effect def.
  ⚠ **Correction (`BL-140`'s 8-chapter sweep, `analysis/weapon-effects-node-shape/`).** The node
  numbers above are off by the `+1` anim-def-ptr convention (`analysis/weapon-effects-node-shape/`) — raw
  `nodes.json` index 204, not 205, is the `g1`/model-60 node — and at the raw index, its
  `parent_indices` is `[203]` (`gunshell`) only, not the `rabbit_blur`/`g11`/`rabbit_blur` chain
  this entry describes (those names sit at nearby *list positions*, not as this node's actual
  parents). Model 60's node **is** `gunshell`'s own only child, contradicting "no relation to
  `gunshell` … by parentage" above. The `shell1`/`shell2` textures are still unmatched to it — that
  part of this entry stands — but "the data gives no mesh" is no longer true for `gunshell`
  specifically; see the corrected footnote in `docs/formats/weapon-effects.md`. Not re-investigated
  further here — whether `rabbit_blur` itself is real terrain-effect geometry, model 60's actual
  visual shape, and the `rabbit_blur`/`g11` chain's true relationship to model 60's node are still
  open.
  *Fix shape:* none — a "confirm before assuming" flag. `BL-013`/`BL-137` landed (C22, 2026-07-31)
  by **instancing the authored `gunshell` subtree**, so model 60 renders with its own materials (the
  ones whose texture indices are `shell1`/`shell2`) and no texture was hand-repurposed — the trap
  this entry guards never fired. Still open here: whether `rabbit_blur` is itself a real, unrelated
  visual effect (a motion-blur streak), model 60's actual visual shape, and the `rabbit_blur`/`g11`
  chain's true relationship to model 60's node.

### Flight-model gaps the video calibration measured (2026-07-25)

Found by decoding the original's cockpit gauges frame by frame
(`analysis/video-flight-calibration/FINDINGS.md` holds the numbers and the instrument). The thrust
scale that pass found has landed; these are what it left open. Each is **measured against the
retail game**, so unlike a TUNE none of them needs a playtest to confirm it is real — the
`--dump-flight` report prints ours beside the original's for the first three.

⚠ **Read before touching any of these: the flight constants are coupled and `--run-tests` guards
them.** Thrust sets speed, speed scales the yaw `eff`, so a change in one moves others; the
`flight-envelope` suite asserts six measured scenarios and will fail if a fix here breaks one.
`ThrustConst` and `PitchTune`/`YawTune`/`RollTune` are pinned measurements, not knobs — a fix that
needs one of them to move needs a new measurement first.

- `BL-092` **No induced drag: a hard pull costs us no speed.** Measured, `--dump-flight`'s `zoom-climb` row:
  from 300 mph level at full throttle our full-pull apex arrives still doing **266 mph** where the
  original bottomed at **104 mph**. The same signal appears in the loop the clock was measured from
   — the original's loop spans 120–280 mph, so it bleeds most of its speed round one. Altitude gained
  is close (1450 ft vs 1635), the energy is not. This is the sharp form of the older "the original
  visibly bled speed in a sustained full-pitch 360°" observation, which can now be retired as vague.
  Wanted: a load-factor term in the drag, i.e. drag rising with commanded pitch rate / lift.
  **Unblocked and the magnitude is now measured — `CAP-01` decoded 2026-08-03.** Full throttle,
  stick full back throughout (pilot-confirmed), Bloodhawk, ~3,200 ft. The clip holds a **+100 ± 4°
  banked turn for 15.9 sim s** and sweeps **449.8°** of heading, so it is a true sustained
  equilibrium, not a transient:

  | segment | speed | dV/dt | altitude |
  |---|---|---|---|
  | cruise, pre-pull (7.6 sim s) | **298.96 ± 0.20 mph** | +0.06 mph/sim-s | +5.7 ft/sim-s |
  | bleed-in (5.6 sim s) | 237.2 mph mean | **−7.50 mph/sim-s** | +6.6 ft/sim-s |
  | **sustained turn (15.9 sim s)** | **222.94 ± 1.77 mph** | −0.35 mph/sim-s | −1.85 ft/sim-s |

  So a max-pull turn costs the original **25% of its top speed**, held indefinitely. The pre-pull
  cruise re-measures the full-throttle level equilibrium at 298.96 mph in a *different session* from
  the 300.4 mph in `FINDINGS.md` — 0.5% apart, which is what makes the comparison a clean A/B.
  **The number to fit: our drag law needs a further `+0.38 × maxThrustAccel` at this load factor.**
  At the plateau `x = V/fd = 0.7457`, so `lerp(x², x, 0.35) = 0.6225 A` of level drag against `1.000 A`
  of thrust; since the turn is level the gravity-along-path term is ~0, so the deficit is real
  along-path force. The bleed-in transient gives **0.369 A** by a completely different route (from
  its deceleration, at a different speed) against the plateau's **0.380 A** — 2.9% apart.
  Turn geometry, for keying the term: **18.95 °/sim-s** (fit residual sd 0.39°) at 222.9 mph, i.e.
  `V·ω = 32.96 m/s²` lateral = **1.65 × `nom_gravity` 20.0** (3.36 g at 9.81).
  ⚠ **Traps.** (a) The candidate data ships: `player.json`'s `highGs [9,15]` / `lowGs [-6,-9]` /
  `maxAOA 46` / `liftAOAs [5,9]` / `lift_accel_rate 0.75` are an angle-of-attack model we have no
  equivalent of — decode that before inventing a term (see the `player.json` entry below).
  (b) **It must not slow the sustained pitch RATE**, which is measured flat across 120–280 mph and
  asserted by the suite: the original bleeds speed in a pull *without* losing pitch authority, so a
  naive "less speed ⇒ less pitch" coupling would break a passing check. **`CAP-01` sharpens this
  and may complicate it:** at max pull the *heading* rate is only 18.95 °/sim-s, well under the
  ~33 °/sim-s sustained pitch rate the loop gave and under the design ladder's bottom rung of 30.
  A compass reads the nose, not the flight path, so this is not simply path-lag — either pitch
  authority in a banked turn is lower than the loop implies, or bank/AoA geometry eats the
  difference. Do not assume the loop's flat pitch rate transfers to a banked turn.
  (c) **0.380 A is one point on the curve, not the curve.** Both segments sit at essentially the
  same load factor (`V·ω` 32.96 vs 34.02 m/s², 3%), so the clip pins the magnitude at max pull and
  says nothing about the exponent — a term in `n`, `n²` or `ω²` all fit it equally. A second
  capture at a *deliberately part-deflected* pull is what would separate them.
  (d) **The deficit is shape-dependent — quote the drag law with it.** Same plateau, other laws:
  pure linear needs +0.254 A, our β = 0.35 blend +0.378 A, pure quadratic +0.444 A, pure cubic
  +0.585 A (total drag 1.34× / 1.61× / 1.80× / 2.41× the level drag at that speed). This is the
  same fork `FINDINGS.md` flags on λ.
  (e) **Part of the 0.38 A may be thrust vectoring, not drag.** Thrust acts along the nose and drag
  opposes the path; a sustained AoA of α puts `1 − cos α` of it into the deficit (α = 25° ⇒ 0.09,
  a quarter of the total). The clip cannot measure AoA, so 0.38 A is honestly a bound on the
  *combined* along-path deficit — which is nonetheless exactly what a load-factor term must supply.
  (f) **A 100° bank that holds altitude is not something our lift model can do.** `liftFrac` scales
  by `wingVert = |Attitude.Y · Up|` ≈ 0.17 there, so we would shed ~83% of gravity across the path
  and drop; the original sinks at 1.85 ft/sim-s. Fixing drag without looking at this will not
  reproduce the manoeuvre — see `BL-247`.
  **Cockpit-confirmed 2026-07-30**, not just decoded from video: the user reports the original visibly
  slows through a sustained pitch pull, and climb bleed is stronger in the original than ours, from
  the controls — this is the felt form of the same gap, not a second finding.
  *Playtest after fix:* once a load-factor drag term lands, fly a full-pull 360° and a sustained climb
  and compare the bleed by feel before closing this.

- `BL-247` **The original holds altitude at 100° of bank; our `wingVert` lift model cannot** (decoded
  out of `CAP-01`, 2026-08-03). Measured off the ADI: the Bloodhawk rolls to **+100 ± 4°** — past
  vertical — and holds it for 15.9 sim s while altitude stays at 3217.5 ± 14.4 ft, sinking at only
  **1.85 ft/sim-s**. `FlightModel.cs:275` scales lift by `wingVert = |Attitude.Y · Up|`, which is
  **0.17** at that bank, so `gAcross * (1 - liftFrac)` would put ~83% of gravity across our flight
  path and drop the aircraft out of the manoeuvre entirely.
  This is the same `player.json` angle-of-attack block `BL-092` (a) points at — `maxAOA 46`,
  `liftAOAs [5,9]`, `lift_accel_rate 0.75` — seen from the lift side rather than the drag side, and
  the two should be decoded together: `CAP-01` is one clip that constrains both.
  **This entry also owns the knife-edge attitude terms** — `KnifeNoseSag` 0.07 rad, `KnifeNoseRate`
  0.2 rad/s and `KnifeAlignFloor` 0.35, all `FlightModel.cs` — inherited when `BL-124` closed
  answered on 2026-08-04 (`docs/HISTORY.md`). They are the same mechanism seen from the attitude
  side: `knife = 1 − |up·Y|` is `1 − wingVert`, so whatever replaces the lift keying has to replace
  these at the same time. The landed behaviour is nose −4°, path −10°, sink 19.4 m/s, 634 m lost in
  35 s, reached within a second of roll-in.
  ⚠ **Traps.** (a) **Do not "fix" this by flattening `wingVert`.** The same quantity drives the
  knife-edge nose-sag, whose *presence and direction* are proven by scripted test, so a
  bank-independent lift term would reproduce this turn and break that. Whatever carries the turn
  has to vanish by 90° *without* being a function of bank alone — pull/AoA is the obvious
  candidate, since knife-edge is flown near neutral stick and this turn at full back.
  **The knife-edge side had independently guessed the same fix** — "gate on actual bank instead of
  `1−wingVert`", because `knife` grows with pure pitch at *zero* bank and a full-pull zoom therefore
  loses ~11° of apex to a term that should not be firing at all. `CAP-01` is the first hard evidence
  that `wingVert` alone is wrong.
  **`CAP-05` (decoded 2026-08-04) now supplies the other end of the curve, and it confirms the
  hypothesis in (a).** Its two knife-edge takes sit at **+94…+104° of bank — the same bank as
  `CAP-01`, by the same ADI measure — but at near-neutral stick**, and the outcome is the opposite:
  the aircraft falls out of the sky (nose sagging without bound, sink reaching 93 ft/sim-s, 540 m
  lost in 38.9 sim s) while sweeping only **0.68 / 1.13 °/sim-s** of heading against `CAP-01`'s
  **18.95** — 17–28× slower, tracked off the compass tape at peak median 0.998.
  **So lift is not a function of bank.** A term keyed on bank alone must give these two clips the
  same answer, and they differ by a factor of 25 in turn rate and by everything in altitude. What
  carries the `CAP-01` turn has to be **pull / angle-of-attack**, exactly as (a) guessed — which
  also promotes `player.json`'s `maxAOA 46` / `liftAOAs [5,9]` / `lift_accel_rate 0.75` from
  "candidate data" to the most likely home for the term. Design the replacement against **both**
  clips: it must hold altitude at 100° bank under full pull, and must not at 100° bank with neutral
  stick.
  ⚠ One caveat on the pairing: the neutral-stick reading is inferred from the footage (the turn rate
  and the monotone nose sag both say no pull was held), not pilot-confirmed the way `CAP-01`'s "stick
  full back throughout" is. If it turns out the knife-edge takes carried some back pressure, the
  factor-of-25 gap narrows but does not close.
  **The knife-edge trajectory the replacement has to reproduce** (from `CAP-05`, both takes, at
  143 mph and 300 mph, agreeing to ~13% — so it is driven by time-since-roll-in, *not* airspeed):

  | time since roll-in | nose | path | sink |
  |---|---|---|---|
  | 0–3 s | −4° step, then drifting | ≈0° | **0.5 ft/sim-s — it genuinely holds altitude** |
  | +12 s | −12.0° | −6.0° | 24 ft/sim-s |
  | +24 s | −20.0° | −12.8° | 60 ft/sim-s |
  | +36 s | −27.0° | −18.7° | 93 ft/sim-s, still steepening |

  So the sag is an immediate **≈4° step** (fitted intercepts −3.4°/−4.2°, i.e. `KnifeNoseSag` 0.07 rad
  is the right *magnitude*) followed by an **unbounded linear drift of 0.69–0.89 °/sim-s**. ⚠ **Do
  not retune `KnifeNoseSag`/`KnifeNoseRate` to fit this — the shape is what is wrong.** A bounded sag
  cannot produce a linear 36-second drift, and raising the bound to 27° would destroy the first three
  seconds, which are the part we currently get *worst* and the original gets flat. Whatever replaces
  it must be near-flat at roll-in and unbounded after. Total altitude lost is nearly the same either
  way (540 m original vs our 634 m over ~35 s) — the shape is the whole difference, which is why a
  feel A/B on sink alone would have passed a wrong model.
  For `KnifeAlignFloor`: the observable is that the path lags the nose by **4.8° at +3 s, 7.2° at
  +24 s, 8.3° at +36 s**. ⚠ Do not back an align rate out of that — gravity is pulling the path down
  over the same interval and `CAP-05` cannot separate the two effects.
  *Playtest after fix:* inherited from `BL-124` and still owed, because the fix has not landed — (1)
  does the knife-edge sink feel like the original's; (2) does the full-pull zoom still feel nose-heavy
  (the `knife`-at-zero-bank leak above); (3) stall-into-knife-edge recovery should not feel "doubled".
  (b) The bank is read from the ADI sky-region centroid, which measured the 360° roll and is
  trusted for bank, but 100° is past vertical where the aircraft symbol painted on the ball is
  least helpful — treat "past vertical" as solid and the exact 100° as ±4°.
  Measured: the original settles at **137.9 mph** (0.459 × fd_speed) at 1/8 throttle and takes
  **7.04 sim s** to fall 290 → 150 mph. We settle at **93 mph** (0.309, and below lift speed, so
  ours is sinking rather than holding level) and decelerate in **2.47 s** — 2.8× too fast. Both are
  printed by `--dump-flight` as `(not asserted)`.
  ⚠ **Traps.** (a) **These two numbers cannot separate the two candidates.** We model thrust as
  linear in throttle; if the original's is not, the equilibrium moves with no drag change at all.
  Solving it as drag alone needs `x^2.67` at low speed, which contradicts the *other* reading in
  the same data (the acceleration's fall-off near fd_speed is steeper than a single power law fits,
  implying ~7.8 below fd against ~3.5 above — probably a soft governor near fd_speed). One more
  measurement discriminates: a level run at 1/4 and 1/2 throttle held to equilibrium. (b) The full
  throttle equilibrium is exactly fd_speed **for any drag blend** by construction, so it cannot
  detect a wrong shape here — the low-throttle end is the only place the shape is observable.
  (c) `LowSpeedDragBlend` 0.35 exists to answer a user report that a throttled-back plane barely
  decelerated; whatever lands here must not reintroduce that.
  **`CAP-05` (2026-08-04) breaks trap (a)'s deadlock from the drag side, with no thrust term in it
  at all.** `CAP-05 Stall 0% Thrust no input` is a zero-thrust deceleration from 159 to 70 mph, so
  `dV/dt` is drag plus gravity and nothing else. Measured drag is **0.36 / 1.11 / 2.82 / 3.74 m/s²**
  at x = 0.25 / 0.35 / 0.46 / 0.50, against our blend's **7.69 / 12.13 / 17.91 / 20.25** — 4–21×
  less, and the ratio holds across every `(g, climb-scale)` pair the fit tolerates. Model-free
  version: engine off at 152.6 mph in a +5° climb the original decelerates at **6.24 m/s²** where
  ours takes 21.6. Because the full-throttle equilibrium pins `D(fd) = A` (trap (b)), a curve this
  weak at x = 0.5 and equal to `A` at x = 1 **must be much steeper than quadratic below cruise** —
  which is `x^2.67`, arrived at here by a completely independent route. So the fork is resolved in
  favour of drag shape: the low-speed drag really is far weaker than ours, and thrust non-linearity
  is no longer needed to explain the 1/8-throttle equilibrium (137.9 mph ⇒ thrust(1/8) ≈ 2.8–4.5
  m/s², against 7.5 for a linear model — still sublinear, but only mildly).
  ⚠ Trap (c) is now the binding constraint, not a footnote: whatever replaces the blend cuts
  low-speed drag by a large factor, which is exactly the direction of the original user report. The
  fix has to come from the *shape* (a steeper exponent, so drag still bites approaching fd) and be
  re-playtested against that report specifically.

- `BL-094` **No altitude limit at all, and the original's is a hard altitude clamp — not a
  performance ceiling and not `flight_ceiling`.** Settled by `CAP-03` (four clips, decoded
  2026-08-03; all four gate rigid, dx corr +1.00). **There is no performance fade below the
  clamp:** level full-throttle equilibrium is **299.71 ± 0.32 mph at 5492 ft**, **299.80 ± 0.56 at
  6001 ft**, **300.00 ± 0.52 at 6520 ft** — flat to ±0.3 mph across 1674–1988 m, and equal to the
  298.96 ± 0.20 measured low down. The clip that goes higher (`CAP-03 Stall at max Alt.mp4`,
  Bloodhawk, C1B IA1) then shows the mechanism directly: the aircraft holds level flight at
  **6570.4 ± 1.04 ft at 297.3 mph**, and when the nose is pulled up ~22° (ADI sin θ −0.13 → +0.24,
  against −0.135 at level in all three reference clips) it **does not climb one foot** —
  6571.6 ± 0.39 ft over the last 5 s while airspeed bleeds at 13.0 mph/sim-s to a new equilibrium
  of **173.74 ± 0.60 mph** with the speedometer's stall window lit. Altitude held to sub-foot at a
  22° nose-up attitude is a **clamp**, not an energy limit; the low equilibrium is what full
  throttle buys against the induced drag of sitting pinned against it. Earlier zoom attempts in the
  same clip overshoot the clamp ballistically to **6712 ft (2046 m)** and sag back, which is what
  the old "five apexes at 2010–2109 m" reading was seeing. So: resting cap **6571.6 ft = 2003 m**;
  transient overshoot ~+140 ft; 80% of the data's `flight_ceiling` 2500, which
  `PlaneStats.FlightCeiling` parses and nothing reads (two hits: the field and the assignment).
  ⚠ **Traps.** (a) **Do not implement this as a thrust or lift fade under the ceiling** — the three
  level runs rule a fade out to within 0.3 mph right up to 1988 m, 15 m under the cap. What is
  needed is a clamp on *altitude* (or on climb rate, with enough lag to allow the measured ~140 ft
  of ballistic overshoot), leaving the aerodynamics untouched below it. (b) **The "auto stall" is a
  consequence, not the mechanism.** The aircraft is not stalled when it reaches the cap — it is at
  297 mph — it stalls because holding the nose up against the clamp bleeds it to 173 mph. Build the
  clamp and our existing stall model should produce the same symptom for free.
  (c) **6571.6 ft may be per-mission, and only one mission was flown.** This is C1B IA1; the
  2026-07 `Ceiling` clip touched 2109 m in a different session. Whether the cap is global, per
  chapter/zone, or per aircraft is untested — do not hardcode 2003 m as a world constant without
  one more mission's worth of evidence. (d) These are true altitudes: the altimeter was proved a
  straight feet conversion (λ = 1.000 ± 0.004) against four spawn-point readings, so do not
  re-open the scale. The three reference clips decode to 5492/6001/6520 ft against the user's own
  5500/6000/6500 ft targets, which independently confirms the 1,000 ft band pick (`anchor.py`
  margins are soft on level clips: 41×, 1.61×, 2.46×, 1.66×).

- `BL-095` **`player.json` ships a physics block we consume almost none of.** Alongside the used
  `nom_gravity 20.0` / `stall_mag 1.25`: `maxAOA 46.0`, `liftAOAs [5,9]`, `lift_accel_rate 0.75`,
  `highGs [9,15]`, `lowGs [-6,-9]`, `drag_factor 1.5`, `drag_fade_speed 40`, `turn_fade_in 10`,
  `turn_fade_out 50`, `high_speed_pitch_fade [1000,1001]`, `yaw_low_speed 0.0625`,
  `yaw_high_speed 0.17`, `yaw_fade_in 10`, `yaw_max 50`, `yaw_fade_out 400`, `groundblow_elev 400`,
  `groundblow_mag 10`, `ai_groundblow 0.5`, and the `crash` block's `bounce_factor 0.6`. Units are
  unverified; decoding it deserves its own pass, and it is the upstream of two other entries here.
  Two things it settles immediately: **ground blow ships** (`groundblow_elev`/`groundblow_mag`),
  where the M2 plan recorded it as absent with "magnitude would be a TUNE"; and the `yaw_*` fade set
  is the original's own speed-dependent yaw authority, which our hand-rolled `eff`
  (`1.4 − clamp(v/fd)`) stands in for and whose comment already admits is "still not same as
  original".
  *Blocked on `CAP-02`* (`playtest.md` §0).
  ⚠ **Traps.** (a) `yaw_max 50` and `yaw_fade_out 400` are not in the same units as our `eff`
  — do not map names onto our terms without deriving the units, because our yaw 360° currently
  matches the original to 4% and a mis-scaled substitution would break a passing suite check.
  (b) `drag_factor 1.5` here **collides with** `vehicle.json`'s per-plane `drag_factor` (0.37 on the
  Bloodhawk), so at least one of the two is not what its name suggests; our drag uses neither.

- `BL-096` **Angle of attack is now fittable and is not modelled.** The ADI shows hysteresis against
  vertical speed round the loop — expected, since the ball shows attitude while `dh/dt` follows the
  flight path, and AoA is exactly what separates them. That hysteresis *is* the AoA signal, and it
  became fittable when the clock factor was pinned. Pairs with the `maxAOA`/`liftAOAs` data above.

- `BL-097` **The roll's spin-up shape is untested.** Mid-roll steady rate reads 240 °/wall-s while the whole
  360° averages 246, so the original's roll was **still accelerating when it finished**. Our
  `1/damp` spin-up reproduces the total time (1.98 s vs 2.05) — whether it reproduces the curve is
  unknown, and only a per-frame bank trace would say.
  ⚠ **Re-read this as a held-key step question** (2026-08-03, out of `BL-147`/`CAP-04`): the
  original's controls are digital, so there is no partial aileron deflection to spin up and a
  "moderate roll input" clip cannot be flown. The only measurable transient is the leading edge of a
  *held* key from steady flight — and at 30 fps that edge is unresolvable, exactly as it was for
  pitch. Any roll-transient capture needs ≥60 fps constant frame rate.

- `BL-147` **Pitch's spin-up: the premise was wrong, and the original's response is NOT a single
  first-order lag — which is exactly what we implement. Measured 2026-08-03 from `CAP-04`; the
  capture is discharged and retired, the item stays open pending an A/B against our own build.**
  The item was written asking for a *moderate-deflection* pitch trace, on the assumption that a
  sub-full-deflection input exists to spin up. **It does not — the user flies the original's pitch on
  the keyboard, so every pitch command is full deflection gated on/off by the key** (confirmed by the
  user, 2026-08-03; note the numpad in the original is the *camera*, `CAP-07`/`CAP-08`, not the stick).
  A "~45° pull" is therefore a **tap cadence**, not a deflection, and there is no partial-deflection
  spin-up curve to fit.
  **What `CAP-04` actually measured** (both takes, `checkclip` `OK`, heading flat to 0.2° so the
  manoeuvre is genuinely wings-level pitch; decode noise 1.3–1.8 ft second-difference; altimeter band
  resolved 11.2× / 8.2×). Entry 299.4 / 300.4 mph level, then a tapped pull. Smoothed peak
  flight-path pitch rate **8.5 / 9.8 / 9.4 / 12.7 °/sim-s** over the four pull events = 26–30% of the
  33 °/sim-s full-deflection rate, i.e. the tap duty cycle; the instantaneous rate climbs to
  **20–24 °/sim-s** as the smoothing window shrinks, which is the individual taps showing through.
  Flight path peaked at **+33.5°** (take 1) and **+28.0°** (take 2) — the clip is *not* a 45° pull, and
  the pitch *attitude* is unreadable because the ADI ball saturates (sky fraction pinned at its 0.730
  ceiling) once the flight path passes ~+10°.
  **The step response was already in the 2026-07 loop clip**, which was flown by *holding* the key:
  from 4 s of dead-level 299.4 mph, key down at t = 4.10 s wall, the rate rises to an asymptote
  **R = 26–31 °/sim-s** (consistent with the published sustained 33) with a model-free 10–90% rise of
  **0.66 s sim**. The exponential **τ is not resolvable** — fitted τ tracks the smoothing window
  (0.73 → 0.19 s sim as the window tightens), so all the footage supports is an **upper bound
  τ ≲ 0.2 s sim**. Our model's held-stick spin-up is `1/ang_momentum_damp` = 1/5.0 = **0.2 s**
  (`FlightModel.cs`, `return_rate` adds only on release), which sits exactly at that bound.
  ⚠ **That "no mismatch is demonstrable" reading is SUPERSEDED by the cadence sweep below.** The
  bound above is only meaningful *if* the response is a single first-order lag, and the sweep shows it
  is not — so a τ derived from it describes a model the original does not obey. What survives from the
  loop clip is the asymptote R = 26–31 °/sim-s and the 0.66 s sim rise, both model-free.
  `PitchTune` 0.75 is still not implicated: it sets the steady rate, which continues to match.
  **The sluggishness is most likely the tap cadence, not the airframe.** At matched smoothing the held
  key reaches its rate in 0.66 s sim while `CAP-04`'s tapped pulls take 0.99 / 1.25 / 1.50 / 5.28 s —
  1.5× to 8× slower, and *not reproducible between takes*, which is the signature of a human hand
  rather than a flight model. Before touching any constant, check whether our key-to-input path
  ramps/filters where the original's is a bare on/off.
  **What remains open:** τ itself.
  **How to get it — a fixed-cadence key macro. 30 fps is a floor, not a target; record at whatever
  rate the recorder gives and keep the bitrate high.** A single step
  edge is ~4 frames and unresolvable, but a *periodic* input is not: drive the pitch keys as a square
  wave and τ shows up as the **ripple amplitude** at a known frequency, which averages down over
  hundreds of cycles instead of living or dying on one edge. Alternate pitch-**up** and pitch-**down**
  (not a single key) so the mean rate is zero — the aircraft porpoises about level, speed and the aero
  gain stay put, altitude stays in one band and the ADI never saturates.
  Modelled ripple at τ = 0.2 s sim, V = 440 ft/s, against the 1.75 ft second-difference noise
  (conservative: that statistic implies only ~0.7 ft of independent per-frame noise):

  | period (wall) | frames/cycle | alt ripple | ripple at τ = 0.1 / 0.2 / 0.3 |
  |---|---|---|---|
  | 0.25 s | 7.5 | 0.57 ft | 1.06 / 0.57 / 0.39 |
  | 0.40 s | 12 | 2.25 ft | 3.75 / 2.25 / 1.56 |
  | 0.60 s | 18 | 6.99 ft | 10.22 / 6.99 / 5.06 |
  | 1.00 s | 30 | 26.3 ft | 32.5 / 26.3 / 20.9 |

  **FLOWN 2026-08-03 — seven clips, and the result is a refutation, not a τ.** Six alternating cadences
  (1300 / 930 / 700 / 570 / 370 / 230 ms) plus a 230 ms duty control. All gate `checkclip` **OK** and
  are the cleanest footage in the corpus (dial translation literally 0 px on the six; second-difference
  noise 0.59–2.64 ft). Cadence logs measured **1300.0 / 930.0 / 700.0 / 570.0 / 370.0 / 230.0 ms** with
  jitter sd 0.002–0.535 ms, so the input is known, not assumed.

  | period | f₀ | ripple amplitude | operating point |
  |---|---|---|---|
  | 1300 ms | 0.769 Hz | **26.31 ± 2.37 ft** | 246 mph mean |
  | 930 ms | 1.075 Hz | **7.07 ± 0.21 ft** | 206 mph |
  | 700 ms | 1.429 Hz | **3.09 ± 0.09 ft** | 259 mph |
  | 570 ms | 1.754 Hz | **0.63 ± 0.13 ft** | 213 mph |
  | 370 ms | 2.703 Hz | 0.065 — at the floor | 221 mph |
  | 230 ms | 4.348 Hz | 0.037 — at the floor | 227 mph |

  **The headline: from 1300 → 570 ms the response falls 42×, over a frequency ratio of only 2.28.**
  Double integration (rate → attitude → altitude) accounts for 5.2× of that. A single first-order lag
  can add at most another 2.28× — that is the τ → ∞ limit, not a fit. So the steepest possible
  one-lag model gives 12×, and the original delivers **42×: 3.5× more roll-off than any single
  first-order lag permits.** That margin is far outside the ±20% amplitude systematics and the ±12%
  spread in mean airspeed. **Our rotation model is exactly one first-order lag**
  (`BodyRates += (cmd - BodyRates*damp)*dt`), so on this axis the original is not that shape.
  **The duty control settles the alternative explanation.** 50% duty on the pull key alone (measured
  duty fraction 0.507 from the log, mean press 116.6 ms against a nominal 115) produced a large
  sustained pull — the aircraft climbed 1775 → 4892 ft and went over the top, speed bleeding to
  109 mph. So **115 ms presses unquestionably reach the game**, and the 370/230 ms nulls are the
  aircraft's own roll-off, not dropped input. The sliding-window amplitude plot shows this directly:
  the four detections hold a flat amplitude across the whole clip while 370/230 sit in the noise
  throughout — the cadence ran in every clip.
  ⚠ **This is still not a fitted τ, and must not be quoted as one.** The clips are not at a common
  operating point: mean airspeed runs 206–259 mph and the ripple itself spans 0.6–26 ft, so the low
  frequencies are a large-amplitude manoeuvre and the high ones a small perturbation. A transfer
  function fitted across that mixes regimes.
  ⚠ **Trap, and it cost a wrong figure before it was caught.** High-passing altitude with a sliding
  quadratic *before* fitting the sinusoid has real gain at f₀ — the 930 ms amplitude moved 4.87 → 6.85
  → 7.07 ft as the span changed. The correct estimator fits **polynomial + sin + cos simultaneously**
  inside a window of ≥8 periods, where a cubic can absorb almost none of the fundamental; that is
  stable to 3% on the strong clips. Never detrend and then fit.
  **Next, and it needs no new footage from the original:** run the identical cadences through our
  build via `--hold`, decode altitude the same way, and compare amplitude-for-amplitude. Same input,
  same measurement, same nonlinearity and same operating points — so the operating-point objection
  above disappears and no transfer function has to be assumed. Plot: `playtest/CAP-04/cadence_response.png`.

  *(Design notes from before the flight, kept because they are what made the measurement work.)*
  Shorter periods discriminate τ harder, longer ones give more signal. **Fit the ratio
  across periods, not one period's absolute amplitude** — the ratio cancels the unknown gain from body
  rate to flight path, which is the one systematic we cannot otherwise pin.
  ⚠ Pick periods that are **not** an integer number of frames (0.40 s and 1.00 s are exactly 12 and 30
  at 30 fps — use e.g. 0.23 / 0.37 / 0.57 / 0.93 s, which stay non-integer at 30, 60 and 120 fps, so
  the set does not have to be re-chosen for the recorder). Integer periods resample the same phases
  every cycle; non-integer ones let the phase drift reconstruct the waveform *below* the frame
  interval. Frame rate otherwise does not matter — the pipeline reads real PTS and the existing clips
  are already VFR — but **resolution does**: `extract.py`'s `LAYOUTS` knows only 2560×720 and
  2560×1440 and raises on anything else, so declare a new geometry *before* recording.
  **The strongest form is a matched A/B, and then no absolute τ is needed at all:** run the identical
  macro cadence against our build and compare the γ ripple directly. The aero gain, the clock factor
  and the AoA coupling all cancel, and the comparison answers the feel question rather than a constant.
  Self-check that the pulses are actually landing: a symmetric ±cadence must hold mean rate ≈ 0, and a
  50%-duty single-key run must give mean 16.5 °/sim-s. If it doesn't, the game is quantising the key
  state and the input waveform is not what the macro thinks. Keyboard auto-repeat off, raw key
  down/up, fixed throttle.
  **The driver is written: `analysis/video-flight-calibration/pitch_cadence.ahk`** (AutoHotkey v2).
  **F13–F18** run the periods longest-first (1300/930/700/570/370/230), **F19/F20** the duty
  self-checks, **F21** panics and releases both keys. Everything is on F13–F24 deliberately: AHK
  hotkeys are global and *consume* the key, so a binding on any key a real keyboard emits would break
  that key everywhere while the script runs — F13+ are legal virtual keys nothing else claims, and
  Synapse can remap a physical key onto them. It schedules
  every edge against an absolute `QueryPerformanceCounter` clock (so error never accumulates), raises
  the Windows timer to 1 ms, busy-spins the last 2 ms, and sends **extended scan codes** `sc148`/`sc150`
  — *not* `sc048`/`sc050`, which are numpad 8/2 and would drive the camera instead of the elevator.
  It writes `cadence-logs/*.csv`, a timestamped log of every edge: **that log is the deliverable as
  much as the video is**, because it makes the input waveform measured rather than assumed, and it
  reports the run's true period and jitter sd when it stops. It beeps once at start and twice at stop —
  ⚠ **useless as an alignment marker in practice: Xbox Game Bar records game audio only, so the beep
  is in none of the clips.** Do not infer anything from its absence; find the cadence window by matched
  filter on altitude instead, which is what the analysis does.
  **Timing verified 2026-08-03** (sending stubbed so the test could not type into anything): 26 cycles
  at a nominal 230 ms measured **230.000 ms, sd 0.002 ms**, worst half-step 0.011 ms off, cumulative
  drift 0.004 ms — ~2 µs of jitter against a 33 ms frame interval. ⚠ The first cut slept to within
  2 ms of each edge and landed **±12 ms** out, because Windows' ~15.6 ms scheduler quantum survives
  `timeBeginPeriod(1)`; the fix is the 25 ms busy-spin window (`SPIN_MS`). Don't "optimise" it back.
  ⚠ **Traps.** (a) `PitchTune` **0.75 is a pinned measurement** (see this section's header warning) —
  and the cadence sweep leaves it untouched: it sets the **steady** rate, which still matches, while
  what the sweep refutes is the *transient shape*. It cannot move to fix a feel report — and it is
  emphatically not the knob for the roll-off mismatch. (b) **Do not quote a τ from `CAP-04`, from the
  loop clip, or from the cadence sweep.** The loop clip's fitted τ is smoothing-limited and falls
  monotonically as the window tightens (`FINDINGS.md`'s "a peak found by differentiating a smoothed
  signal is a smoothing artifact", in its exact form); the sweep's points are not at one operating
  point. The sweep refutes a *shape*; it does not fit a constant. (c) Don't fold this into `BL-097` — same shape of gap,
  different axis, and pitch's own coupling to speed (`BL-092`'s induced-drag gap) makes conflating the
  two easy to get wrong. (d) The digital-input finding is not pitch-specific — it means **`BL-097`'s
  roll question has the same defect**: there is no partial aileron deflection either, so a "moderate
  roll input" clip cannot be flown, and `BL-097` should be re-read as a held-key step question too.

- `BL-243` **The original carries destruction across missions in a state log; CSVM has no log, so a
  warm instant action starts clean where the original does not.** Decoded 2026-08-02 out of
  `BL-099` — the mechanism, the user's five-step A/B in the original, and the 62-definition
  `PERSIST_LOG` list are written up in `docs/formats/anim-definitions.md` ("`SAVE_LOG` /
  `PERSIST_LOG` are the cross-mission state log"); read that before touching this. The rule:
  **every mission load applies the log, only campaign missions write it, and it commits to the save
  and survives a process restart.** `SAVE_LOG ON` (568 defs) puts a definition in the log at all;
  `PERSIST_LOG ON` (62 defs, a strict subset, all fixed world scenery) additionally carries it
  across mission boundaries.
  **Scope of the divergence is narrow.** CSVM builds every session from the bootstrap, so we match
  the original for campaign missions and for a cold instant action; we differ only for an instant
  action loaded after a campaign mission **in the same process** — the exact case that produced
  `BL-099`'s burning fuel depot. There is no campaign flow yet, so today this is unobservable in
  practice.
  **What implementing it would take:** a chapter-scoped store of `PERSIST_LOG` definition states
  that outlives `Launcher.ReturnToMenu`'s teardown (which currently QueueFrees the whole session
  subtree), applied after the `RESET_STATE` bootstrap and before `zepstate`/`startanims`/the `.gw`,
  written only on a campaign-mission exit. Because `fuelboxconnect*` is a persisted def, the stored
  state cannot be just a destroyed/healthy flag — a *running* looping animation is part of what the
  original carries.
  ⚠ **Traps.** (a) **Do not use this to explain away a rendering difference.** It is the reason a
  screenshot of the original is not evidence about the shipped data, which makes it an equally good
  way to hand-wave a real bug; anything blamed on the log needs the mission sequence that produced
  it. (b) Two facts are still untested and would change the design: whether the commit happens at
  damage time or at mission completion, and a direct A/B separating the two flags (destroy a
  `PERSIST_LOG` object and a save-only one in one campaign mission, then load an IA — the first
  should carry, the second should not).

## Open fidelity questions (answerable by testing the original)

- `BL-221` **Which axis order does an anim-def `AT_NODE` *position* use? The graze reaction is the
  first def whose offsets are nonzero and visually judgeable, and they read wrong (user, 2026-08-01,
  `PT-24`).** Mesh coordinates are settled right-handed Y-up, nose at −Z (`docs/formats/gotchas.md`),
  and the engine applies `AT_NODE`/`PufferState` offsets in that frame. `touchdown_default`'s five
  `small_yellow_sparks` calls sit at `(0, 8, −2)`, `(±1.5, 8, 0)`, `(±4, 8, 0)`. Read as Y-up that is
  five sparks in a horizontal rake **8 m above** the plane, spread across the span — which is what
  the user saw ("sparks start above and inside the building"). Read as the game's Z-up world
  (x, y = forward, z = up) it is five sources **across the wing, 8 m ahead**, the centre one 2 m
  low — exactly a nose/leading-edge scrape. The ±1.5/±4 lateral spread matching a wingspan is the
  strongest single clue that the *first* component is spanwise and the constant 8.0 is not height.
  ⚠ **Traps.** (a) This is not a touchdown-only fix — every `AT_NODE` position in every def goes
  through the same read, so flipping it globally would move the rocket/crash effects that currently
  look right. Settle it by finding defs with nonzero offsets whose correct placement is already
  known (turret muzzle points, zeppelin nacelle fires) and testing both readings against them —
  a census in `analysis/`, not a guess. (b) `small_yellow_sparks` emits with
  `world_velocity (0, 10, 0)`, which reads plausibly under BOTH conventions (sparks fly up / stream
  forward), so it cannot break the tie — don't cite it as evidence. (c) The graze def's staging site
  (`graze.siteAtContact`) is a *different* question with the same symptom family — it decides where
  the offsets are measured FROM, not which axis each component is. Settling one does not settle the
  other, and the site currently defaults to the contact point on feel, against what the data argues.

- `BL-184` **The original DOES animate the ammo-gauge arrow — a constant-rate sweep at 157 °/sim-s.**
  Our gauge already draws the pointer (`gg`/`mgarrow`) rotated to the selected belt slot, but the
  rotation is applied instantly — `DrawWeaponGauge` recomputes `-(360°/Positions) * Selected` per
  frame with no tween (`GaugeCluster.cs:613-614`). The user's recollection that the original's
  pointer visibly *moves* (2026-07-30) is **confirmed and measured — `CAP-18` decoded 2026-08-04.**
  The clip is third-person, so it was decoded through the new `chase` HUD layout (`hud.py`); the
  gun gauge's geometry is confirmed two independent ways — its texture fit and a pivot solved from
  the arrow's own sweep lines agree to **1.8 px**.
  **The numbers, from 5 clean slot steps** (`analysis/video-flight-calibration/ammoarrow.py`):
  - **It sweeps, unambiguously.** Frame-by-frame at 15 fps the arrow passes through every
    intermediate angle: from 12 o'clock at 7.49 s wall through −7°, −18°, −33°, −48°, −67°, −82° to
    −90° at 7.96 s. Not a snap, and not a fast blend — a visible traverse.
  - **Rate 218.1 ± 3.9 °/wall-s = 156.9 ± 2.8 °/sim-s**, spread 1.8% across the five steps.
  - **One 90° slot step takes 413 ms wall = 574 ms sim** from the fitted rate (455 ± 15 ms wall
    end-to-end including the near-stationary frames at each end — quote the rate, not the duration,
    since the duration depends on where you threshold the start).
  - **The sweep is LINEAR, not eased.** A straight-line fit over a 90° traverse leaves a residual of
    **1.95°** — a smoothstep would leave many times that. Implement as a constant angular rate.
  - **The readout does not animate with it.** `40 SLUG`/`2400` → `30 SLUG`/`2800` flips on a single
    frame at the *start* of the sweep, while the arrow is still leaving the old slot. So the value
    snaps and only the pointer tweens.
  - The gun gauge has **4 slots at 90°**, marked on the face (green at 12 and 9, red at 3 and 6);
    every measured step was 89.8°.
  **The hardpoint (ROCKETS) gauge is answered too, and it is the one that reveals the routing rule.**
  It has **8 slots at 45°** — its three resting angles (359.50°, 180.17°, 314.36°, i.e. slots 0, 4
  and 7) fit a 45° lattice to **0.64° max / 0.44° mean**, against 14.4° for a 6-slot lattice, 35.8°
  for 5 and 44.4° for 4. The clip repeats one three-move cycle four times, identically:

  Slots are numbered as `GaugeCluster` already numbers them — **0 at the top, increasing
  counterclockwise** (`Indicators`, "0 = top, CCW"), which is also what our arrow rotation
  `-(360/Positions) * Selected` assumes. The three rests decode to slots **0** (359.50°), **4**
  (180.17°) and **1** (314.36° = 45° counterclockwise of top), and the clip cycles 0 → 4 → 1 → 0:

  | move | Δindex | measured | shortest way? |
  |---|---|---|---|
  | 0 → 4 | +4 = 180° | **−179.4°** (counterclockwise) | tie — both ways are 180° |
  | 4 → 1 | −3 | **+134.1°** clockwise | ✅ yes (135° cw against 225° ccw) |
  | 1 → 0 | −1 | **+45.2°** clockwise | ✅ yes (45° cw against 315° ccw) |

  **So the needle takes the shortest way round, and does not simply follow the index direction.** Two
  of the three moves discriminate — walking the indices the "long" way would have swept 225° and 315°
  where the original sweeps 135° and 45°. ⚠ **At the exact 180° antipode it goes counterclockwise**,
  consistently across all four repetitions. That needs no special case to reproduce: the standard
  shortest-path wrap `delta = ((target − current + 180) mod 360) − 180` returns **−180** at exactly
  +180, which is precisely the observed direction. Implement that one expression and all four move
  types fall out.
  **Both gauges sweep at the same rate.** Fitting only the interior of each sweep (dropping 2 frames
  at each end, where the run detector keeps barely-moving frames) the gun gauge gives 235.6 ± 1.8 and
  the hardpoint gauge 233.7 ± 2.2 °/wall-s — **0.8% apart**, so it is one constant:
  **234.5 ± 2.3 °/wall-s = 168.7 ± 1.6 °/sim-s**. On top of that each move carries about **70 ms
  wall (~97 ms sim) of ramp**, consistent across 90°/135°/180° moves, which is why the end-to-end
  average rate reads lower (218–226 °/wall-s) the more end frames you include. So: a constant-rate
  traverse with roughly two frames of ease at each end, *not* a smoothstep — the interior residual to
  a straight line is **1.1°** over traverses of 90–180°.
  End-to-end durations, for A/B: **90° = 455 ± 15 ms wall = 633 ms sim**, 135° = 634 ms wall =
  881 ms sim, 180° = 856 ms wall = 1190 ms sim.
  *Wanted:* tween the arrow angle at **168.7 °/sim-s**, routed by the shortest-way wrap above, with
  the ammo readout still snapping. Acceptance is a capture A/B, so it is deliberately **not** in
  `PLAN-m3-polish-quickwins` (code-verifiable-only criteria) — land it in a later look-and-feel
  pass, after `BL-024`/`BL-025` so the gauge draw path has stopped moving.
  ⚠ Rate quoted in **sim** seconds (k = 1.390); the wall figure is what the original showed on the
  recording machine, and implementing 234 °/s would run the sweep 39% fast.
  **The ring size and the slot mapping are already right in our code — only the tween is missing.**
  The hardpoint ring is a fixed **8** and does not follow the loadout (user-confirmed 2026-08-04: 8
  is the maximum any plane has and the loadout only decides which are *filled*; corroborated by the
  shipped marker rig, `docs/formats/markers.md` — `pylon1`…`pylon8`, "Every player plane has 8",
  extracted from `planes.zbd`). We do not derive it from the loadout: `geom.Positions =
  geom.Indicators.Count` (`GaugeCluster.cs:571`) counts the gauge model's own belt-indicator quads,
  so it is 4/8 by construction whatever is loaded. And the measured rest angles land exactly where
  `-(360/Positions) * Selected` puts them for slots 0/4/1, so the static mapping is confirmed
  against the original too. The whole delta for this item is the animation.

- `BL-099` **C1's fuel depot: what did you actually see, and in which mission? — ANSWERED
  2026-08-02, nothing to implement.** The report was: in the original, C1 IA1's depot sometimes
  spawns with tanks already alight and a pipeline tower blinking, while the shipped data says IA1's
  depot **cannot burn and cannot blink** (`ia1.gw` deactivates `fuel_truck01`/`02`, `nodes.json`
  ships both `active: false`, and the only caller of `trucktodepot1` is a C1/M02 script). That
  contradiction is now explained, and it is not about C1 at all.

  **The answer: the original carries destruction across mission boundaries in a state log**, and a
  mission's rendered world is `RESET_STATE` + `zepstate` + `startanims` + its `.gw` **plus that
  log**. Every mission load applies it; only campaign missions write it; it commits to the save and
  survives a process restart. The per-definition opt-in is the `SAVE_LOG` / `PERSIST_LOG` pair,
  previously documented as undecoded engine bookkeeping — **the full decode, the user's five-step
  A/B in the original, and the 62-definition `PERSIST_LOG` list are in
  `docs/formats/anim-definitions.md`** ("`SAVE_LOG` / `PERSIST_LOG` are the cross-mission state
  log"). So the depot the user saw was destroyed in **C1/M02**, where the whole route is reachable,
  and C1 IA1 inherited it.

  **Both of this item's loose ends fall out of that.** `ftank0*` *and* `fuelbox*` — for both
  `fuelboxconnect*` (the blinking pump) and `fuelboxbreaks*` — are on the `PERSIST_LOG` list, so a
  *running* pump animation is part of what carries, which is what the blinking needed and a static
  flag could not give. And the `{ftank02, ftank04}` "middle two" pair no longer has to be
  producible by a clean chain: IA1 shows whatever M02 was left in, including a burn crawl caught
  mid-flight, on top of the noted caveat that `ftank02` sits ~52 m north so the freecam "row of
  four" need not match the X ordering.

  **CSVM is not misbehaving, and this stays true — do not "fix" the depot.** Both `healthy` and
  `destroyed` ship `active: true` in the gamez, bootstrap pass 1 resolves them via each `ftank0N`
  def's `RESET_STATE`, and we render intact tanks, dark lights, static — every run. That is what
  the shipped data specifies for a cold IA1, and it is what the original renders for one too.

  **Do not re-derive the chain analysis** (traced 2026-07-22, and still correct): the only route to
  a destroyed tank is weapon damage on `fuel_truck01`-`truck_destroy01` (`WeaponHit`, `health 20`)
  → `chainreaction` → `StopAnimation fuelboxconnect1` → `CallAnimation chainreaction_fueldepot1` →
  burn crawl → `ftank_boom4` then `ftank_boom3`; `chainreaction_fueldepot2` mirrors it with
  `ftank_boom1`, `ftank_boom2`; each `ftank_boomN` flips `healthy`→inactive, `destroyed`→active. No
  randomness exists anywhere on that path (the authored-random idiom is `If {RandomWeight: w}` →
  state → `StopSequence`, and `fuel_tanks.zrd.json`, `ref_fueltanks.zrd.json`, `fueltruck.zrd.json`,
  all 47 C1 `cam_anim` files carrying `RandomWeight`, all 13 in `C1/IA1/mis_anim` and `ia1.gw` were
  all checked clean). The blinking is a *working* pump indicator (`flashthelights`: on → +0.1 s off
  → +0.2 s `Loop {-1}`, with `scale_hose` rocking the `rockerarm`), and `fuelboxbreaks1` kills those
  lights permanently — so blinking and destroyed tanks are anti-correlated **within a chain**, and
  seeing both means one chain ran and the other did not.

  **The general lesson, worth more than this item:** a screenshot of the original is not by itself
  evidence about the shipped data, because the world you are looking at may carry state no file
  describes. Reproducing the log itself is `BL-243`.

- `BL-100` **Which weather/sky zone do C1–C4 actually use?** **C5 is answered — `zone1`** (user A/B
  2026-07-22; landed as polish-3 item 2, see `docs/formats/weather.md` and `Weather.ResolveZone`).
  **Still open for C1–C4**, all of which define `zone2` and resolve to themselves, so they render a
  plausible answer either way and this is a fidelity question rather than a bug. **C1 is the one
  worth doing first:** it is the only chapter whose own scripts disagree (`load.gw` →
  `zone2_cloud_floor`, `tex_fx.gw` → `h_zone1scroll`), and its two zones are genuinely different
  skies (zone2 = moon/stars night, zone1 = day haze).
- `BL-101` **Fine-tune fog and environment** — method: record video from a spawn point flying straight for a
  fixed number of seconds, in both engines, and compare.

- `BL-102` **Patrol boat: which HP governs?** (from `docs/plans/PLAN-M3-weapons.md` C23, 2026-07-22.)
  Two systems **agree on the damage-stage fractions and disagree on total HP by exactly 2×**: the
  `patrolboat` vehicle def says `health 40` with stages at 0.60/0.30 firing
  `ptboat_50damage`/`ptboat_75damage`, while the `C1/patrol_boat` anim def says `HEALTH 20` with
  stages at `ANIM_HEALTH` 12/6 (also 0.60/0.30) firing the generic
  `sputter_black_smoke_obj`/`sputter_fire_smoke_obj`. Likely reading: the vehicle def governs the boat
  as an **AI combatant**, the anim def as **placed scenery** — so M3 (scenery only) wants 20.
  **Counting hits in play is impractical (user, 2026-07-30) — settle from data instead.**
  **Where they are:** a placed `patrolboat` gamez node exists in C1, C2, C3 and C5 (none in C4); it is
  wired to a destructible (`HEALTH 20`, 0.60/0.30 stages, `ptboat_50damage`/`75damage`) only in
  **C1/M05, C2/M01 and C5/M01** — `--node=patrolboat --viewer --chapter=C1` (or `C2`/`C5`) frames it
  directly. **C3's placed boat has no mission wiring at all** (grepped every C3 mission/IA folder for
  `patrolboat` — zero matches outside `gamez`/`textures`), so it is inert scenery there, not a target.
  Those same three missions' `aiv.zrd.json` (the **AI vehicle table**,
  `docs/formats/anim-definitions.md:564` — confirmed **not** a spawn roster, so entry count ≠
  spawned-boat count) also carries `patrolboat_N` behaviour entries (12 in C1/M05, 1 in C2/M01, 2 in
  C5/M01) with per-entry position/heading — consistent with patrol boats being AI-piloted there, but
  `aiv.zrd`'s numeric schema is undecoded, so which HP value a moving AI boat actually reads is not
  provable from this file alone.
  **The "exactly 2×" doubling does not generalize** (checked as asked): `t_truck`'s vehicle def
  (`armor 0`, `health 40`) vs. its own mis_anim def
  (`extracted/C5/M01/mis_anim/t_truck-t_truck.json`, `health 15`, stages at 12/8 = 80%/53%, not
  60%/30%) disagree by **2.67×, not 2×**, and with different stage fractions — a genuine
  counter-example to a fixed doubling rule. `armytruck_destruct` and `fueltruck` have **no vehicle-def
  entry at all** (grepped `extracted/zrdr/vehicle.zrd.json`) — anim-only, so there is nothing to
  duplicate; notably `armytruck_destruct` still uses the same 60/30% stage split as `patrolboat`,
  which is better read as a **shared authoring idiom for two-stage damage** than as evidence of a
  doubling bug.
  **Revised settle path:** decode `aiv.zrd`'s per-entry field layout (or find it already decoded
  upstream for MW/PM, which share the vehicle-table concept) far enough to confirm whether a moving AI
  patrol boat's hit points come from the vehicle def or a mis_anim-style def — a stronger, data-side
  argument than a cockpit count, and it doesn't need the original open.

- `BL-103` **Rocket firing cooldown (LANDED B17, 2026-07-24 — now a playtest A/B).** Every rocket entry
  has `FIRE_RATE 1.0` (vs 8.0–10.5 for guns), i.e. one launch per second. The user confirmed one
  trigger pull = one rocket from one hardpoint but was **not sure whether a cooldown exists**, so
  B17 uses 1.0 as the data's answer rather than an observed fact (`FlightController.UpdateRockets`).
  A/B the 1 s gate against the original.
- `BL-104` **Rocket fire binding (design choice, LANDED B17).** Rockets fire on **F** / gamepad **A** (guns
  are Space / pad-B), one per pull. Pad-A doubles as respawn but only from the crashed / run-complete
  screens, which the live-flight firing path never shares — so no collision. If the mapping feels
  wrong in the cockpit it is a one-line change in `FlightController.RocketFirePressed`.

- ⚠ **Never infer a damage threshold from an animation's name.** `ptboat_50damage` fires at
  **60 %** health remaining and `ptboat_75damage` at **30 %** — the names lag their trigger,
  the same way `docs/formats/hud.md` records for the cockpit damage dial ("the anim names lag
  their effect by one state"). Measured 2026-07-22 while planning M3.

- `BL-105` **Map-edge continuation**: ours alternately *reflects* the border tiles (seam-free by
  construction); the original likely plain-repeats them, possibly sharing the edge vertex row.
  **Not answerable from extracted resources (checked 2026-07-30):** `nodes.json`/the gamez area grid
  only describe the in-map 12×12 (C1) tile set; nothing in the shipped data encodes what the
  original's *own* engine does for cells outside it — that continuation is emergent runtime behaviour
  in the original executable, not a stored asset, so no amount of terrain/texture mining here can
  settle mirror-vs-repeat or the tile-count question. **User observation (2026-07-22): the tile
  borders do match, so it may not be mirrored — and the original may extend by more than one tile**;
  that check was almost certainly inconclusive because a symmetric border tile (open ocean) cannot
  discriminate mirror from repeat. The A/B needs a genuinely **asymmetric** border feature, filmed
  continuously for 4+ tile-crossings: the silhouette flips on a mirror and stays identical on a
  repeat, and counting tiles out to any change settles the extent (`CAP-17`). One-line swap in
  `MapEdgeExtender.MirrorAxis` once it answers.
- `BL-108` **The yaw `eff` speed shape** (`1.4 − clamp(v/fd)`) is an unvalidated interim model away from
  cruise: the 360° rudder turn matches the original to 4%, but that is one speed. The original's
  own version of this ships as `player.json`'s `yaw_*` fade set — see "Flight-model gaps the video
  calibration measured" below, which is also where the two closed halves of this entry went (the
  original's pitch rate is measured **flat** with speed, and it bleeds speed in a hard pull because
  of induced drag rather than a falling pitch rate).
- `BL-109` **Engine pitch behavior in dives**: the original's engine drops ~12% through a dive and
  overshoots ~1.05 at pull-out — not reproducible by the throttle-only pitch curve (cap 1.0).
  **Playtest 2026-07-30 narrows this**: the user confirms the original's note modulates with **climb
  rate and elevator input**, not throttle alone, and reads noticeably less constant than ours. Camera
  Doppler is ruled out as the mechanism — own-ship engine audio is a plain `AudioStreamPlayer`
  (`FlightAudio.cs`), non-positional by design, so no Doppler shift applies to it regardless of
  whether `AudioStreamPlayer3D.DopplerTracking` is ever enabled elsewhere (see `BL-160`). Left
  standing: a speed/RPM term, or wiring climb-rate/elevator directly into `EnginePitch.Eval`'s input
  instead of throttle. Needs a controlled full-throttle-dive recording (HISTORY 2026-07-19; recording
  still owed) (`CAP-10`).
- `BL-110` **`SunIncidence` 0.46** (item-6 world brightness) rests on a single overcast reference —
  a C1B-night and a bright-day original screenshot would confirm/refine the self-scaling (`CAP-11`).

## TUNE constants pending playtest

The live list (moved here from CLAUDE.md 2026-07-22). Each is a hand-tuned constant that is
plausible but unvalidated against the original — they need the user in the cockpit, not another
scripted screenshot. **Consolidated actionable index: [`playtest.md`](playtest.md).**

- `BL-231` **Effect-template pool sizes (D10, 2026-08-02).** `CSVM/data/effect_pools.json` — how many
  copies of each effect template the world-effects stage holds, so that many overlapping calls to one
  effect each keep their own (`BL-225`). **Invented, and the data cannot settle it**: the original
  copies its template per call and has no such number, so any finite pool is our approximation of
  "unbounded" — which is why it is an editable file and not a `const`. Shipped: default **4 base
  +1 per extra player**, `partial_damage_obj` **8 +1**, the three gun roots **1 +0**, ceiling
  **16**. The default came from rocket concurrency (`FIRE_RATE` 1/s against ~2.5 s of authored trail
  motion → at most 3 overlapping blasts) plus a spare; the sputter root from measurement (five
  simultaneous `ap_h2otwr` kills wrapped a 4-slot pool exactly once, and do not wrap an 8).
  Judge it where concurrency is highest — a rocket burst into a cluster of destructibles, and
  splitscreen/multiplayer, where each extra aircraft is another source. **The per-player term and
  the ceiling are the two knobs a many-player build should re-judge**: at 16 players the default
  root wants 19 and gets 16.
  The instrument is in the build: `AnimRuntime.PoolRecycles` counts every call that wrapped onto a
  still-live slot and the runtime names the first per effect (`anim: effect pool for '<name>'
  recycled slot …`); the world-effects build line prints the sizes actually staged. A scripted run
  that logs no recycle had enough pool — raise the root that logs one, not the default.
  ⚠ Traps: it is not free — each slot is one more copy of that root's subtree (1 player: 147
  templates; 4 players: 252), so raising the default multiplies world-build cost and memory for
  effects that are mostly not concurrent. The three gun-impact roots stay at **1** deliberately: C8
  throttles the gun family to one play per 0.1 s per name, so pooling them buys copies nothing uses;
  raising them belongs with removing that throttle (its own step, its own emitter-count check).
  Sizing a root **0** is not a way to disable pooling — it clamps to 1, because staging no template
  at all reads in-game as a broken effect.
- `BL-230` **Near-miss trigger distance (B7, 2026-08-02).** `WarningShotCue.PassRadius` = **15 m**,
  the distance a round's swept step must pass within to sound `bullet_warning_sg`. Chosen, not read:
  the shipped `warning_shot_*` block rates the cue but says nothing about how close is close, and the
  sound def's `RANGE [20,200]` is the 3D falloff window, not a trigger radius. Judge it at the
  controls — `--incoming=<metres>` walks a burst past at a chosen distance, and
  `weapons.warningShotRadius` moves the threshold without a rebuild (config.json, so **not** under
  `--det`).
  ⚠ Traps: `CANNON_SPREAD` scatters each round several metres over any real firing range, so the
  achieved distance is a distribution — judge over a burst, never off one pass. Raising it far enough
  that a round crossing the sky sounds is the failure mode, not a louder cue.
- `BL-227` **Rocket blast falloff + knockback magnitude (D10, 2026-08-01).** The radius and full
  health damage are authored (`IMPACT_PROXIMITY`, `HEALTH_DAMAGE`), but the shipped data does not
  encode a falloff curve or impulse. D10 uses linear falloff to zero at the edge and
  `BlastImpulsePerDamage = 1 N·s` on a directly struck rigid body. Judge clustered-object damage
  and physical push against the original before changing either.
  ⚠ Traps: do not retune the authored radius or fuse distance; `DAMAGE 0` specials carry large
  effect radii and are deliberately excluded from blast damage.
- `BL-218` **Puffer `NUMBER` default (2026-08-01)** — `NUMBER` is absent from 680 of C1's 721
  `PufferState` events, including `large_30sec_fire`'s `fire_n_smoke`, and `PufferState.FromAnimEvent`
  falls back to **1** sprite per `TIME_INTERVAL`. That fallback is a guess at the original engine's
  default, not decoded data: the sibling `large_10sec_fire` authors `NUMBER 3` from otherwise
  comparable values, so the real default may well be higher and every unnumbered emitter in the game
  correspondingly thin. Judge the density at the controls now that the fire's *shape* is right
  (`PT-22`) — it is a whole-effect multiplier, so a wrong value is visible on the destruction fires,
  the damage-stage sputters and the wreck smoke at once.
  ⚠ Traps: this is not the `puffer.*SizeScale` knobs — those scale sprite size, and trading
  count for size is exactly the substitution that makes a too-sparse plume read as "too small"
  instead. **That substitution has now partly happened**: `SizeScaleDefault` went 1 → **4** at the
  controls on 2026-08-01, which makes every unnumbered emitter read fuller without adding a sprite,
  so a density verdict taken today is measuring the two together. If `NUMBER` is later raised, 4 has
  to be re-judged in the same session, not left standing. Do not tune it from a single `--screenshot`: sprite count only reads over a time series
  (SHOT-19). And do not infer the default from the effects readers — the `NUMBER`-carrying states
  are a biased sample, since `PufferState.FindInReader` treats the presence of `NUMBER` as what
  makes a state "fully defined" in the first place.
- `BL-223` **Damaged-engine loop gain (B5, 2026-08-01)** — `vehicle.json`'s `damaged_engine_sound`
  entry (`["snd_damagedengine", 0.0, 1.0]`, shared by every plane via `basic_airplane`) has two
  undecoded trailing floats. `PlaneStats`/`FlightAudio` read them as a fade window over accumulated
  damage fraction (`1 - PlaneDamage.WorstFraction`): 0 gain at `f0`, full gain at `f1` — for this
  data that means "no blend until pristine, full blend the instant any part is scratched," since
  `f0=0.0`. `FlightAudio.DamagedEngineMixGain` (default 1.0, the def's own sounds.json volume,
  unattenuated) has no reference recording to derive an attenuation from, unlike `WhineMixGain`'s
  measured dive. Judge both — whether the loop should ramp in more gradually as damage *accumulates*
  rather than snapping in on first scratch, and whether 1.0 sits right against the healthy engine —
  at the controls. Config keys: `flightAudio.damagedEngineMixGain`.
- `BL-215` **Rocket-trail puff size (C21, 2026-07-31; was mis-tagged `BL-212`, an accidental ID
  collision with the landed STOP_SEQUENCE bug — renumbered 2026-08-01)** — the trail look and per-type character
  passed the cockpit A/B (PT-09), but the user flags the puff size as possibly needing more tuning.
  The authored FLYOUT values are verbatim; only render-side size/overlap is in play.
- `BL-200` **Casing-ejection look (C22, 2026-07-31)** — the authored halves are verbatim (the
  `gunshell` OBJECT_MOTION per `MotionRuntime`'s translation_range decode; the `muzzlepuffer`
  velocity/size/life/deviation; the muzzle-light range/colour variants), but four values are
  hand-picked: the **white puff cluster stand-in** (`EjectPuff*` in `Projectile.cs` — count 5,
  spread 0.4 m, drift ±1 m/s riding the casing's launch velocity, size 0.5–0.9 m, life 1.2–2.0 s;
  no shipped effect def matches it — only `muzzle_burst` references `gunshell`, and the `gunshell`
  def is motion-only), the **muzzle puff count per shot** (`MuzzleSmokePuffs` 3, glossing the
  authored 0.05 s-interval × 0.3 s window from a moving node), the **light flash energy**
  (`MuzzleLightEnergy` 2.5 — the def carries range/colour only) and its **2-frame life**
  (`MuzzleLightLife` 0.03 s, glossing the def's next-event-tick deactivate). Judge at the controls
  ⚠ Do not re-derive the calibre gate or underbelly mount from the design spec
  (SRC-3 — rejected against the reference captures), and do not shorten `gunshell`'s `RUN_TIME 2`
  to any gate — authored data. **PT-10 verdict (2026-07-31): casing + cluster confirmed working;
  the puffs render as stripes — that defect is `BL-209` (PLAN-m3-polish-3 C22). Re-judge these
  values only after it lands.**
- `BL-201` **Muzzle-flash shape (C24, 2026-07-31)** — the flash is now a triad of three quads 120°
  apart, the whole triad rotated by a shared random angle each shot (user direction, matching the
  reference captures' 3-lobed burst; the authored `mb_spinflame` node instead rotates one node to
  one of three discrete angles — 30°/80°/140° — `RANDOM_WEIGHT` 1/3 each, `muzzle_burst.zrd.json`).
  The per-ammo texture axis is now wired (`{slug,dum,ap,mag}_muzzle1`, resolved from the weapon's
  `FIRE` `ANIMATION` — `muzzle_burst_slug`/`_dum`/`_ap`/`_mag`; the base `muzzle_burst` and heavy-mount
  `muzzle_burst2` default to slug). Hand-picked: `MuzzleFlashCount` 3, `MuzzleSize` 0.5 m (unchanged
  waypoint), and the continuous (not 3-bucket discrete) per-shot rotation. Judge against
  `MuzzleFlash1..3.png` and shot 2 of `OriginalScreenshots/C1B IA1 Bloodhawk tracer and
  ejection.png`. Also re-confirm A10 muzzle placement now the flash shape changed:
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo --fire`.
  **PT-11 verdict (2026-07-31): unreadable — a red semi-transparent circle, rotation invisible;
  the quad-anchoring defect is `BL-208` (PLAN-m3-polish-3 C21). Re-judge these values only after
  it lands.**
- `BL-202` **Tracer look (C25, 2026-07-31)** — the tail artifact (a short line bleeding past the
  streak's end) was the engine texture default, not the streak geometry: `StandardMaterial3D`
  defaults to `TextureRepeat = true`, so bilinear filtering at the UV=0/1 edge blends in the
  *opposite* edge of the tracer texture; the quads never tile, so repeat is now off for every
  `ProjectilePool` sprite material. The per-ammo texture axis is now wired for tracers too
  (`tracer_slug`/`_dumdum`/`_armorpierce`/`_magnesium`, the same `FIRE`-binding resolution as the
  muzzle flash — `MuzzleAmmoIndex` reused; ordnance has no ammo-type `FIRE` binding and falls back
  to the generic `tracer1`). Hand-picked, per the user's "a lot brighter" direction: `TracerLength`
  1.0 m (was 3 m), `TracerWidth` 0.10 m (was `0.0782f * 2`), and a uniform overbright tint
  `TracerBrightness` 3.0 (additive blend with no bloom pass, so the only way to read brighter than
  the texture's own pixel value). Judge against `OriginalScreenshots/C1B
  IA1 Bloodhawk tracer and ejection.png`/`…ejection2.png`.
  **PT-12 verdict (2026-07-31): colour right; the user tunes length/width themselves — landed via
  `BL-210` (PLAN-m3-polish-3 C23): `weapons.tracerLength`/`tracerWidth`/`tracerBrightness` config
  knobs, plus a `weapons.tracerMinPixels` distance-visibility floor.** ⚠ The bullet is fast enough
  (1000 m/s at 60 fps ≈ 16.7 m/frame) that a frame-locked `--screenshot` capture almost never lands
  exactly on a round still at the muzzle — the near/bright look is easiest judged live, holding the
  trigger, not from a single scripted shot.
- `BL-113` **Compass tape** — `TileOverscan` / `RimGain` / the nearest-tick look remain TUNE
  (north = −Z is now confirmed against the original, 2026-07-30 — do not reopen).
- `BL-115` **Flight model** — `StallNoseRate`, `KnifeAlignFloor`, `ClimbGravityScale`, `LowSpeedDragBlend`.
  **`PitchTune` / `YawTune` / `RollTune` / `ThrustConst` have left this list**: all four are now
  measured against the original frame by frame and asserted by the `flight-envelope` suite, so they
  are not TUNE knobs and a feel A/B cannot overrule them. **The owed errand — flying the calibrated
  values at the new speeds — is done (2026-07-30): confirmed feeling like the original, no
  collision/stunt-zone/chase-camera regressions.** What remains open here is
  `StallNoseRate`/`KnifeAlignFloor`/`ClimbGravityScale`/`LowSpeedDragBlend` alone.
  **`CAP-05` decoded 2026-08-04** (four clips, all gating rigid) — three of the four are answered and
  one is not:
  - **`StallNoseRate` 1.0 rad/s is ~17× too fast.** In `CAP-05 Stall 0% Thrust no input` the nose
    holds **+4.2 ± 0.1°** through the whole deceleration, starts falling only at **76 mph = 0.25 fd**
    (minimum speed reached 69.8 mph = **0.232 fd**), then drops at **3.38 °/sim-s = 0.059 rad/sim-s**
    from +4.1° to −21.2°, and **stops at ≈−22°** once speed rebuilds past 0.40 fd. It does not chase
    world-down, so "rad/s toward world-down at full stall depth" is the wrong target as well as the
    wrong rate. The break is wings-level and clean: the compass turns **0.0°** across the whole
    24.8 sim s, no wing drop. ⚠ Note `StallSpeedFrac` **0.30** is implicated too — the original breaks
    at 0.25 fd, not 0.30 — but that constant is outside this entry; raise it with `BL-148`.
    **Resolved 2026-08-04 by `CAP-06`: the constant is `split`, not moved.** The original's *warning*
    lights at 0.299 fd (four clips) — 0.30 is correct there — while the *nose-drop* is at 0.25 fd,
    measured in the same frames of the same clip. So the nose-drop needs its own threshold and
    `StallSpeedFrac` 0.30 stays where it is for the warning; `BL-148` owns the split.
  - **`LowSpeedDragBlend` 0.35 gives 4–6× too much drag below cruise.** The same clip is a
    thrust-free drag probe: measured `D` is **0.36 / 1.11 / 2.82 / 3.74 m/s²** at x = 0.25 / 0.35 /
    0.46 / 0.50 against our **7.69 / 12.13 / 17.91 / 20.25**. The ratio survives every `(g, C)` pair
    the fit tolerates. The model-free form: engine off at 152.6 mph in a +5° climb the original
    decelerates at **6.24 m/s²** where ours would take 21.6. This is an independent confirmation of
    `BL-092`'s `x^2.67` — see that entry, which owns the drag-law rewrite.
  - **`KnifeAlignFloor` 0.35** — the footage gives the observable (path lags nose by 4.8° at +3 s,
    7.2° at +24 s, 8.3° at +36 s of knife-edge) but *not* the constant, because gravity is pulling
    the path down over the same interval and this clip cannot separate the two. **The constant now
    lives on `BL-247`**, which inherited the knife-edge attitude terms when `BL-124` closed.
  - **`ClimbGravityScale` 0.6 is NOT settled and `CAP-05` cannot settle it.** ⚠ The fit is
    degenerate: holding `g` and refitting leaves rms flat (0.218–0.280 m/s²) over `g` = 17…25 m/s²,
    with `C` = 1.18 / **0.59** / 0.00 at `g` = 17 / 20 / 25. That `nom_gravity` 20.0 lands on
    `C` ≈ 0.6 is a consistency, not a measurement — precisely the "confirms whatever you feed it"
    trap `FINDINGS.md` warns about. The 50%-throttle climb clip cannot help: its ADI **saturates**
    (sky fraction pinned at 0.730), so the nose angle is unreadable above ~+30° and the along-path
    thrust cannot be formed. *What would settle it:* an independent `g`, or a capture with a
    **readable** nose angle in a sustained climb — i.e. a shallow, held climb at fixed throttle
    rather than a zoom.
- `BL-116` **Numpad camera views — superseded 2026-07-30.** The "layout is settled" claim this entry
  made is now contradicted by cockpit testing (8/2 swapped, 7/0 both underside-front, 1/3 both rear,
  and the original binds 0 which we don't), and the "distance is user-recall, not data" claim is now
  false — `extracted/zrdr/camparam.zrd.json` ships real per-plane distances. Superseded by `BL-149`
  (the shipped camera data — landed; its residue is `BL-248`) and `BL-150` (the plan-sized rebuild
  covering layout, easing, interrupts, and the +/− trim). Kept as a retired ID, not deleted, per this
  file's permanent-ID rule.
- `BL-148` **Stall warning: the ramp is real, but it is a BLINK-RATE ramp and it needs a second
  threshold.** `GaugeCluster.cs:247` gates the blink on `Stalled && WarnPhaseOn` — `Stalled` is a hard
  boolean from `FlightModel.isStalled()` (`FlightModel.cs:328-332`, a single `Speed < stallSpeed`
  threshold with no margin state) and `WarnPhaseOn` is a fixed 50%-duty blink at `WarnBlinkPeriod`
  **0.4 s** (`GaugeCluster.cs:51,130`) — so the cue snaps on at one fixed rate the instant the boolean
  flips, and never changes again however deep the stall goes.
  **Measured 2026-08-04 from `CAP-06` (two clips) plus the two `CAP-05` stall clips — four clips, all
  gating rigid.** The `STALL` plate is the red window above the speedometer hub (game x 892–918,
  y 548–558); it was read in colour straight from the video, per frame.
  - **Brightness is BINARY — do not build an opacity ramp.** Lit R = **211.0 ± 0.2**, unlit
    **41.7 ± 0.2**, and those two levels are identical in every speed bin from 43 to 90 mph and in all
    four clips. There is no intermediate state at any speed. Duty cycle is **0.50** throughout.
  - **The RATE is the ramp.** The blink half-period shortens monotonically with stall depth:

    | speed | fd | half-period (game frames) | full period, sim |
    |---|---|---|---|
    | 88–91 mph | 0.30 | 13.9 | **1285 ms** |
    | 78–84 mph | 0.27 | 12.7 | 1182 ms |
    | 66–72 mph | 0.23 | 10.1 | 932 ms |
    | 60–66 mph | 0.21 | 9.0 | 834 ms |
    | 40–50 mph | 0.15 | 6.4 | **592 ms** |

    105 dwells pooled. **Every dwell is an integer number of 33.37 ms game frames** (lattice residual
    ≤ 8 ms), so the lamp toggles on a frame counter. Half-period ≈ **5.9 × V(mph) − 62 ms wall**, or
    **5.1 × V** through the origin; the residual is 36 ms either way — one frame — so the data cannot
    separate those two forms, and neither should be extrapolated below ~43 mph.
  - **It tracks speed, not time-since-onset.** In `CAP-06.mp4` the speed dips to 65 mph and recovers;
    the blink rate falls and then rises again symmetrically, and the on-threshold is the same
    decelerating (89.9/90.0 mph) as accelerating (90.0/89.9) — **no hysteresis**.
  - ⚠ **The warning and the nose-drop are TWO thresholds, and this is the answer to the question this
    entry was blocked on.** The lamp lights at **0.2992 / 0.2994 / 0.2989 / 0.2996 fd** across the
    four clips — i.e. exactly **0.30 fd, our `StallSpeedFrac`, which is therefore right for the
    warning**. The nose-drop is at **0.25 fd** (`CAP-05`). Verified *inside one clip*, so no
    cross-clip assumption is involved: in `CAP-05 Stall 0% Thrust no input` the lamp lights at
    t = 7.96 sim s / 89.9 mph while the nose is still held at +4.3°, and the nose only breaks at
    t = 10.60 sim s / 75.0 mph — the lamp **leads the stall by 2.64 sim s and 14.9 mph (0.050 fd)**.
    So `StallSpeedFrac` must be **split, not moved**: keep 0.30 for the warning, give the nose-drop
    its own 0.25. Our single `isStalled()` boolean currently drives both from one number.
  *Wanted:* a proximity fraction driving **blink rate** (not opacity), a warn threshold at 0.30 fd, a
  separate stall threshold at 0.25 fd, and `WarnBlinkPeriod` replaced by a speed-dependent period —
  ours is a fixed 400 ms against the original's 1285 ms at the threshold falling to ~590 ms deep in
  the stall, so we are **~3× too fast where it matters most** and flat where the original ramps.
  ⚠ **Traps.** (a) This needs a code change before it needs a magnitude — do not treat it as a retune
  of `WarnBlinkPeriod` alone. *Playtest after fix:* once the ramp lands, A/B its rate against
  `CAP-06`. (b) `isStalled()` currently returns only a boolean — adding a continuous margin changes
  its signature/call sites (`FlightController.cs:734,757`); don't bolt a second parallel margin
  calculation on top instead. (c) Don't reuse `LowAltAglM`'s pattern uncritically — the low-alt cue is
  legitimately binary (a fixed AGL gate, no spec claim of a ramp there), so a shared "warning"
  abstraction that ramps both would over-apply the fix. (d) ⚠ **The periods above are SIM ms** and the
  wall figures are 1/1.390 of them; the original's clock runs fast, so implementing the wall numbers
  would make our blink 39% quicker than the original was designed to be. (e) The lit plate also
  carries an orange bezel glow that the unlit state has not — if the blink is ever reproduced by
  swapping a texture rather than tinting, that glow is part of the lit art.
- `BL-248` **The original's chase distance is DYNAMIC and its easing rates are undecoded — only the
  static per-plane distance has landed.** `BL-149` shipped the reader and wired `dist` per airframe
  (`CamParams`/`CameraController`), which is the fixed part of the mechanism. Two pieces of the same
  file remain unread, both deliberately: (a) the **dynamic distance** — `dist_factor` 0.01,
  `dist_vary` 0.1, `dist_min`/`dist_max` — which implies the original's chase distance moves with
  something (speed? throttle? load factor?) rather than sitting at one number; and (b) the
  **catch-up triplet** `pos_catch_up` 2.0 / `look_catch_up` 3.0 / `dist_catch_up` 1.0, against our
  hand-picked `CamSmooth` 8 / `CamRotSmooth` 7. Also unread: `thirdp_height`/`thirdp_pitch` (which
  would replace the hand-picked offset DIRECTION, not just its radius) and the whole
  `back_*`/`death_*`/`crash_*`/`flyby_*` set, none of which have cameras to drive yet.
  *Fix shape:* decode the dynamic law from `CAP-21`, then drive the radius through it; separately
  measure a settling time to fix the catch-up units before touching the two smoothing constants.
  *Blocked on `CAP-21`* (`playtest.md` §0).
  ⚠ **Traps.** (a) **`dist_min` (15.7) is LARGER than `dist` (13.0) in the `default` block**, while
  the seven per-plane blocks have them equal — so the mechanism is NOT "clamp `dist` into
  `[dist_min, dist_max]`"; under that reading no plane could ever sit at the default's own 13.0.
  Whatever law lands has to explain that, not work around it. (b) **The catch-up units are
  undecoded** — 1/s, a frame count and a seconds-to-settle are all plausible and differ by ~4x in
  felt lag, and none of them looks broken on screen, so the by-eye test cannot decide it
  (`METHOD-14`, `docs/verification.md`). (c) The chase radius is the SAME number the numpad fixed
  views use (cross-ref `BL-150`); anything dynamic here moves both cameras, which is intended —
  do not "fix" that by giving the views their own copy. (d) `thirdp_pitch` 0.29 rad = 16.6° sits
  close to our hand-picked 15.7° elevation, which is suggestive and **not** a decode —
  `thirdp_height`'s units are unknown, so the pair cannot be wired on the strength of one
  near-match.
- `BL-118` **Cloud puffs** — opacity and density. **Cloud deck** — brightness reads ~40 units lighter
  than the original. **Playtest (2026-07-30), two new specifics + mechanism traced.** (1) The deck
  itself shows dense cloud puffs while flying through it. (2) In C1/IA1, puffs show around the plane
  at *all* height levels rather than a confined band. Root cause: `CloudPuffs.cs`'s
  `BandBelow`/`BandAbove` (120 m/280 m) plus `VertFull`/`VertFade` (200 m/560 m) together make the
  field seed visible puffs for any camera altitude in **[290 m, 1964 m]** for C1/IA1 — effectively the
  whole flight envelope (ceiling ~2003 m, `BL-094`) — while `weather.json`'s own `CLOUD_COVER` block
  defines a much narrower band: `TOP`/`BOTTOM` 1124/970 (clear at both edges) with a fully-opaque core
  of only 1032–1062 (`docs/formats/weather.md:190-192`, `Weather.cs:112-113`). No zrdr defines a
  separate ambient-puff emitter — reconfirmed against the newly-extracted `cam_anim` set; the only
  "cloud" hits are the static world-decoration `clouds.zrd.json` sprites and the unrelated
  `steamcloud` zeppelin-skin puffer.
  *Fix shape:* key the layer's vertical extent/falloff to each mission's own
  `CloudTop`/`CloudBottom`/core-`THICKNESS` instead of the two flat margins, so puffs read dense only
  near the real whiteout core and fade out near the data's own clear-at-970/1124 edges.
  *Blocked on `CAP-12`* (`playtest.md` §0).
  ⚠ **Traps.** (a) Don't just shrink the margins by feel — they vary per mission/zone; re-derive per
  chapter or the same bug reappears with a different band width elsewhere. (b) The "~40 units lighter"
  brightness reading is about the `CloudDeck` **mesh**, a different object from the sprite field the
  two new symptoms describe — do not read one as evidence for the other. (c) Some margin beyond the
  opaque core may be intentional (real cloud decks have wisps outside the solid layer, which is the
  whole reason this field exists) — a capture matched to `C1 IA1 Cloud Puffs and Moon.png`'s altitude
  would settle whether today's margins are too generous or roughly right.
- `BL-119` **Wing lights** — `FlashDuration`. **Resources re-check (2026-07-30):**
  `piratefighter-wing_lights_blink.json`/`brigand-wing_lights_brigand.json` confirm the blink sequence
  turns the flares + point lights on, then off again after **0.0001 s**, looping every **1.5 s**
  (already `WingLights.BlinkPeriod`, itself data-sourced). The period is data-exact; the on-duration
  is not a usable literal — 0.0001 s is imperceptible at any real frame rate — so `FlashDuration`
  0.08 s stays a deliberate hand-widening with no better data source to replace it. Only *how long* to
  widen it remains an open TUNE.
- `BL-120` **Collision feel** — behaviour against building corners.
- `BL-121` **Damage (Run-2 item 10)** — `CrashSpeed` 25, graze friction + attitude kick,
  `GrazeStopSpeed`, breakup scatter, and whether the 10c panel-flip and smoke-trail look right in
  real flight. ⚠ The 2026-07-31 "confirmed to render in real flight" claim (`BL-174`) was
  narrower than it read: every dive in that test flew the identity -Z heading near the world
  origin, the one pose where the emitter's world-anchoring bug was invisible — at any real
  mission spawn and heading the trail rendered kilometres away until the `TopLevel` anchor fix
  (`docs/HISTORY.md` 2026-08-03, `trail-world-anchor` suite). Rendering at real spawns is now
  verified; this item is back to a magnitude/feel judgement. Tree softness is retired dead code
  (`docs/HISTORY.md` 2026-07-23), not a TUNE — do not re-add it here.
- `BL-246` **Smoke/fire trail is effectively unreachable from organic gameplay** (found while
  fixing the trail-anchor bug, 2026-08-03). The whole-plane `player_smoketrail` needs a part at
  ≤ 0.10 HP fraction (`DamageVisuals.cs`), but the only in-game damage source is a terrain graze:
  `GrazeMaxDamage` 18 behind `_damageCooldown` (`FlightController.cs`) against 15–25 HP parts, and
  a critical part reaching 0 crashes the plane outright — so hitting the 0.10 window without dying
  takes several survivable grazes on the *same* part, which normal play never produces. The F5 lab
  (or `--damage=`) is currently the only practical way to see the trail. Design/tuning question,
  deliberately split from the render fix: candidate shapes are weapon fire damaging planes (no
  enemy-fire path exists at all today), a lower smoke threshold, or accepting it as a
  near-death-only effect like the original. Decide against the original at the controls
  (`CAP-15`'s look-half owes the same footage).
- `BL-122` **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)** — several playtest-gated TUNEs,
  all needing the original at the controls: `WreckMomentum` **0.4** (`FlightController.cs` — the
  fraction of impact velocity the wreck pieces inherit, so they scatter along travel vs. pop straight
  up); the **`forward_rotation.Time.initial` ÷ run_time** tumble-rate reading in `AnimRuntime`'s
  `MotionRuntime` (the pieces carry clean π multiples read as a *total* angle, not a rate); the
  **debris-arc trajectory** (`translation_range` read as travel distance over `run_time` in a fanned
  azimuth — the `fly_trailN` anchor is invisible, so only the arc's rough scale reads; `MotionRuntime`,
  `initial`/`delta` unmapped); the **overall crash intensity** (the fireball, the cluster, the debris
  fire and the wreck fire are all additive, so a dirt crash can read as one big fireball — judge the
  whole against the original); and `snd_exp_ground_a` mix level + whether it should layer over
  `plane_destroy_sg` (the dirt def's only Sound is `snd_exp_ground_a`; we keep both). The retired
  bespoke crash on branch `bespoke-crash-animation` is the A/B reference for these.
  *Blocked on `CAP-16`* (`playtest.md` §0).
- `BL-123` **Audio (Run-2 item 11)** — `WhineMixGain` 0.12; A/B'd against the original 2026-07-30:
  close, but "could be a bit louder." `flightAudio.whineMixGain` is now config-wired
  (`FlightAudio.cs:145`). *Playtest after fix:* nudge `flightAudio.whineMixGain` up from 0.12 via
  `config.json`, re-A/B a dive, then delete the override (`docs/verification.md` DET-8 applies here
  too).
- `BL-125` **Stunt mode** — `DzRadius` **15 m — user-tuned by hand 2026-07-22, and this is the current
  value** (an earlier "30 m, tightened from 60" note here was stale; the source is right).
  **Still wanted: a per-zone radius from the data, because one global constant does not fit** —
  the user reports 15 m is too tight at some zones while 30 m was too loose at others, the loose
  case being that you fly *around* the danger and still score it. **The leads were all checked
  2026-07-22 (polish-4 item 5) and the answer is `dzpathN`:**
  - ✅ **`dzpathN` carries real gate geometry — this is the route.** It is not the "AI route
    ribbon" it was documented as. Each is a 3-polygon model under `dzpaths`: **polygon 0 is the
    approach/exit polyline** (7–8 points: dive-in → thread → climb-out), **polygons 1 and 2 are the
    two gate outlines** — 3- to 18-point planar rings bracketing the thing you fly through.
    Measured across all 54 zones in C1/C1B/C2/C3/C4/C5. So the implementation is either "cleared
    when the plane crosses the prism between the two rings", or the cheaper "per-zone radius from
    each ring's own extent". **Settled 2026-07-25 in favour of the first** — the two rings are the
    entry and exit apertures and both must be crossed; the cheap radius reading does not stop the
    tangential clip. Mechanism, traps and what stops mattering: "Danger Zone scoring uses one
    sphere…" under Feature backlog. `DzRadius` stays a TUNE until that lands.
  - ❌ **`dzN`'s `RotateTranslateScale.scale` is a dead end** — measured **unit on all 53** markers.
  - ❌ **`node_bbox`/`child_bbox` are a dead end for `dzN`** — measured **all-zero on all 53** (they
    are `model_index -1` point nodes). They carry real values only on `sghangar`, the one
    geometry-node zone, which is why that item did not need them either. Still unparsed into
    `GameZNode`.
  - ⚠ **Do not derive a marker position from its dzpath.** The tempting rule "the `dzN` marker sits
    at the midpoint of the two gate centres" is **exact** on some zones (C2 dz7/8/9, C5 dz14/15, to
    ≤0.04 m) and wildly wrong on others (**C1 dz2 is 826 m off**; C5 dz2 266 m; C1 dz1 225 m). The
    markers are hand-placed. An *extent* is also a new concept for every consumer of the zone point
    (`MarkerHud.cs:144,145,158,212`, `StuntMission`'s completion test).
  - ⚠ **Do not retune `DzRadius` as part of this** — 15 m is the user's hand-tuned value.

  Marker-HUD placement, font and distance units, and scoreboard fonts and placement were playtested
  2026-07-30 and read fine for now — see `BL-181` for the provisional, pending-menu-hub caveat.
- `BL-126` **Splitscreen** — the `HudMetrics` sqrt pane damping, `MixGain`, `SpawnAbreast`, join/lock
  feel, tag-gutter widths.
- **Gun-impact looks (A2/`BL-203`, landed 2026-08-01)** — `Projectile.cs`: dirt chips
  `DirtDebrisSprites` **5**, `DirtDebrisSize` **0.45 m**, `DirtDebrisLife` **0.9 s** (authored bit
  RUN_TIME is 1–2 s), `DirtDebrisSpeed` **4 m/s**, `DirtDebrisSpreadDeg` **60°**, `DirtDebrisSpinMax`
  **25 rad/s**; building ricochet `RicochetSparks` **8**, `RicochetSparkSize` **0.55 m**,
  `RicochetSparkLife` **0.55 s**, `RicochetSparkSpeed` **22 m/s**, `RicochetSpreadDeg` **90°** (a
  stand-in — both authored assets are missing from the install); water-splash column width
  `SplashColumnWidthScale` **8×** (the authored quad is 5 cm wide — sub-pixel past ~30 m; the
  reference ticks measure ~0.35 m). A/B against `Dirt Splash.png` / `Water Splash.png` at the
  controls; the splash *height/timing* curves are authored data, not TUNE.

## Owed playtests (need hardware or a human at the controls)

> **The actionable, consolidated checklist is [`playtest.md`](playtest.md)** (root) — what to look
> for, the launch command, and what each blocks. This section keeps the deep evidence/traps; keep
> the two in step.


- `BL-129` **`--freecam` interactive feel** — look sensitivity and the speed curve have never been
  assessed by hand; the module was built entirely through scripted screenshots and
  `--debug-anim`.
- `BL-130` **The labs are mouse-driven** (`--viewer`: damage on F5, livery on L, mesh on M) and have had no
  interactive playtest beyond scripted verification. The damage lab now has a second, live host —
  F5 in `--fly` drives the flown plane's real HP while the sim runs — which widens the gap rather
  than closing it: a drag there competes with a per-frame read-back and nothing scripted can prove
  that feels right. `PT-29` is the owed test.

## C3 ships gamez references to textures its texture.zbd does not contain

**ID: `BL-133`**

Surfaced 2026-07-21 during the extraction cutover and deliberately left alone. C3's gamez
references `cloud1`/`cloud2`, which its own `texture.zbd` does not ship — a **retail-data gap**,
true in both the v0.6.1 and fork extraction trees, so not something the cutover caused.

The fix is a one-line addition to `TextureArchive.KnownAbsentFromGameData`, which would render
them neutral gray instead of magenta. It is left to the user because it is a **visible** change
and it deliberately gives up the magenta signal that means "our bug" for those two names — the
project's convention is that magenta is diagnostic, so suppressing it is a judgement call, not a
cleanup.

## Cutscene player — the missing consumer (M04's zeppelin, `letterbox`, `CALLBACK`)

**ID: `BL-134`**

**This is a missing subsystem, not a bug.** The cutscene defs run because nothing tells them they
are cutscenes. What is absent is a **cutscene player** owning camera control, the `letterbox`
bars, scene sequencing, and the end-of-cutscene handoff to gameplay. Same dependency as the
`CALLBACK` event kind (see "Animation event kinds that need weapons or cutscenes" above — all 8
dispatches are `def=camera1`, unanchored, triaged as intro-cutscene notifications). **Pick the two
up together.**

### ⚠ Traps — read before touching this

1. **Do NOT skip the cutscene defs at bootstrap.** That fix was proposed, scoped, and
   **REJECTED by the user 2026-07-22**: *"the M0x missions are campaign missions and we need those
   animations if we want to restore the campaign."* `generic_intro` ×12 and
   `mission_intro_animation` ×1 are decoded, working **assets** — the missions' authored intro
   movies. Deleting their bootstrap throws away campaign capability to suppress a cosmetic symptom.
2. **C1/M04's pirate zeppelin flying above the overcast is an ACCEPTED artifact**, not a defect to
   work around. Do not lower the zeppelin, do not suppress the def, do not "fix" `letterbox` bars
   if they appear. Lowering it is content invention — the same trap as the C3 palms.
3. **An 8-chapter regression is inert here by construction.** The default mission is IA1, which
   has no intro at all; no `IA1` and no `MP` mission bootstraps a cutscene def. Verify per-mission
   or not at all (`docs/verification.md` DIAG-10).
4. **Verify by what disappears, not by what looks right** — `generic_intro` is shared across 12
   missions and may currently be driving things nobody has looked at.

### M04 is the ready-made first test case

Its data is fully decoded, so it is an end-to-end exercise for free. The symptom that exposed all
of this: the pirate zeppelin builds, renders complete and flies — it is simply **above the
clouds**, at y 1505→1546 while C1's opaque `cloudlayer` deck sits at y = 960 and the player spawns
at y ≈ 110. Freecam onto it with `--freecam --chapter=C1 --mission=M04
"--pos=-5358,1505,-2200" "--lookat=-5358,1505,-1810" --no-fog`.

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
gameplay — and "then it's gone" is the last beat deactivating it. **User-confirmed 2026-07-22:**
*"Yeah those are the cut scenes."* The same def demonstrably drives `letterbox` too, so a useful
open check remains: **are we currently drawing letterbox bars in M04?** If so that is the same
missing consumer with a far more visible symptom, and a good first target for the player.

**Scope, surveyed across all 53 missions' `startanims.zrd.json`: 13 bootstrap a cutscene def.**

| def | missions |
|---|---|
| `generic_intro` | 12 — C1/M05, C1B/M03, C1C/M01, C2B/M04, C3/M01, C3/M02, C3/M05, C4/M01, C4/M02, C4/M04, C5/M01, C5/M04 |
| `mission_intro_animation` | 1 — C1/M04 (the bespoke one) |

**No `IA1` and no `MP` mission names one** — all 13 are `M0x` story missions, so this is invisible
in instant action and multiplayer, which is what the project defaults to. That bounds the blast
radius neatly and explains why it went unnoticed until someone flew `--mission=M04`.

**❌ The fix this evidence originally led to was REJECTED — see Trap 1.** For the record, it was:
skip those two def names when the animation bootstrap walks `startanims`. Small and data-driven, a
name check against the start-anim list rather than a new subsystem — and wrong, because it buys a
cosmetic fix with campaign capability. Kept here so nobody re-derives it and thinks it is new.

**Verification note that outlives the rejected fix:** verify *by what disappears, not by what
looks right*. `generic_intro` is shared across 12 missions and may currently be driving things
nobody has looked at, so a change here is checked by enumerating removed motion per mission. An
8-chapter regression cannot catch any of it: the default mission is IA1, which has no intro at
all, so the regression is inert here by construction (`docs/verification.md` DIAG-10).

## The one-frame `CallSequence` dispatch lag

**ID: `BL-135`**

**Found 2026-07-22 while fixing C1's police siren** (that fix landed; see `docs/HISTORY.md`). This
is the *other* defect that investigation turned up — real, engine-wide, and deliberately left
unfixed because it was not what silenced anything.

**The mechanism.** `CallSequence` appends to `AnimInstance.Runners` (`AnimRuntime.cs:998`) while
`AnimInstance.Advance` walks that list **descending** (`AnimRuntime.cs:1485`). An appended runner
therefore lands at an index the loop has already passed, so **every called sequence's first event
fires one frame late** — not just the siren's.

**Why it was not fixed with the siren.** Draining same-pass-appended runners was implemented and
measured **behaviour-neutral** (exactly one number moved across all 8 chapters). But it repairs
the siren only because the sound loader *happens* to still be alive at that instant, and leaves
the other 947 late `SOUND_NODE` events broken. The loader lifetime was the real defect and is
fixed; this lag is a separate question about dispatch timing fidelity.

### ⚠ Traps — read before touching this

1. **The descending walk is deliberate, not a bug.** `AnimRuntime.cs:250` records why: instances
   can be added *during* the walk. Do not "fix" it by iterating forwards.
2. **Any same-pass drain needs a bound.** A sequence that calls itself would spin within a single
   frame. (The siren's own `siren_police` is safe — 3 zero-delay events, no `Loop`, so its runner
   completes and is removed in one pass — but that is a property of that data, not a guarantee.)
3. **Do not measure this with the bootstrap emitter census.** `anim: N ambient sound emitter(s)`
   is printed inside `Bootstrap`, so it is a snapshot that cannot see anything created afterwards
   — which is exactly how the siren's real cause stayed hidden through a full investigation
   (`docs/verification.md` LOG-2). C1 legitimately reports 38 while 39 emitters exist.

**Open question this should answer:** does the original dispatch a called sequence in the same
tick? If yes, every `CallSequence` in the install is currently a frame late and the fix is a
fidelity improvement rather than a no-op. Nobody has checked; the measured behaviour-neutrality
above only says *our* observable output does not change.
