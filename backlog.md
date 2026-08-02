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
fails loudly instead of silently pointing at the wrong item. **Next ID to assign: `BL-237`.**
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
  `MotionRuntime` (`AnimRuntime.cs`), and three things combine — **none of them the FROM_TO
  dropped-delta bug** (that is a different event kind; the debris `translation.delta` *is* mapped):
  1. **World destructibles inherit no momentum.** `InheritedWorldVelocity` is set **only** by the
     plane crash (`FlightController.cs:971`); the shared world `AnimRuntime` never assigns it, so a
     world piece gets only the small authored launch — a 5–10 m/s straight-up pop, which is exactly
     "the pieces barely drift."
  2. **Ground-rest / bounce is deferred.** `do_intersections` + `bounce_sequence` are not simulated,
     so a piece integrates freely over `run_time` then **holds its final pose** — translate a little,
     stop.
  3. **The magnitude decode is unsettled TUNE**, not settled data: `translation.initial` is read as a
     velocity and `translation_range` xz/y as distance ÷ run_time, with the range's own
     `initial`/`delta` sub-fields unmapped.
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
  call as `MissionSetup`'s unguessed `Object3DRotate` angle unit.

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
- `BL-051` **The gamez node `active` flag is never read.** `GameZ` parses no node flags at all (`flags` is
  touched only for *polygon* flags, `GameZ.cs:278`), so `flags.active` — the shipped on/off state
  each node was saved with — is ignored and every node is built visible. That flag is the gamez
  record of the build script's own `NodeSetActive off`: C2's `load.gw` switches `piratezep` off
  right after loading it, which is exactly why C2's `piratezep` is the one zeppelin in the install
  shipped `active: false`. **Measured population — small, which is why this has never been
  noticed:** nodes shipped inactive per chapter are C1 13, C1B 2, C1C 2, C2 3, C2B 2, C3 6, C4 6,
  C5 2, and of those only **four are world-build roots**: C1 `fuel_truck01`/`fuel_truck02`,
  C2 `piratezep`, C3 `barracuda`. All four are currently masked by something else (a mission setup
  script, or polish-4 item 4's unplaced sweep), so there is no *known* visible symptom — but the
  masking is coincidental, and C2/M01–M03 do not name `piratezep` in their setup scripts at all, so
  C2 is where a symptom would surface first.
  ⚠ **Traps.** **Do not fix this while the item-3 fidelity question is open** (C1 IA1 oil tanks
  already destroyed at spawn, under "Open fidelity questions"): C1's `fuel_truck01`/`fuel_truck02`
  are shipped inactive and that investigation turns on whether those trucks are present in IA1 —
  honouring the flag would hide them and silently change the very thing it is blocked on testing.
  The flag is **not** a fix for the item-4 symptom and must not be confused with it: C5's
  `piratezep` ships `active: true`. And do not extend this to other node flags without a survey —
  `terrain` in particular is **not** a visibility flag, it marks world-space map geometry, and is
  what separates C3's `zepbridge1/2` and C4/C5's `zepdock` (identity transform, geometry already in
  world coordinates, correctly drawn) from the zeppelin vehicles.

- `BL-053` **`node_bias` already spans 1.22–2.86 priority levels per chapter** (C1 1.77, C1B 1.40,
  C1C 1.41, C2 1.24, C2B 1.22, C3 1.35, C4 2.07, **C5 2.86**). `docs/architecture.md` recorded
  this as an accepted corner case ("a prio-0 node >~4000 indices later can out-bias a prio-1
  overlay; no such pair observed in C1") — measured, the real span is **up to nearly three levels
  and is the normal state of the chapter**, not a corner case. Consequence: today **2–16% of
  cross-node conflicting pairs already resolve the wrong way round**, because within-mesh surface
  rank can out-bid the cross-node term. A dense conflict rank would fix this as a side effect
  (28 × 5e-6 = 1.4e-4 = 0.7 levels). Measured 2026-07-22, `analysis/item9-depth-bias/`.
- `BL-056` **`materials` is a per-polygon LIST and `SceneBuilder` reads only `[0]`.** 352 C5 polygons carry
  a **second textured pass** with its own independent UVs: `z3_foggrad`/`foggrad8x64` fog gradients
  (142), `buildingspotlighted` (34), `fadedsign01-03`, `nypd`, `clock`, and plane logos. A whole
  second-texture-pass feature is silently dropped. Measured 2026-07-23, same report §3.
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

### World animation & effects

- `BL-235` **An un-respawned player crash burns forever: the crash rig's `fire_n_smoke` never stops
  (measured 2026-08-02, found while fixing the torpedo's).** Same family as the torpedo fireball,
  different runtime. A crash `AnimRuntime` (`ForCrashRig`) leaves `EffectTtl` **0** deliberately —
  "ambient/crash runtimes leave it 0 and are unbounded" (`AnimRuntime.cs:244`) — so it registers no
  `_effectTtls` entry, and `FinishEffectInstance` is scoped to instances that carry one. Nothing
  ends `player_crash_dirt`'s `large_10sec_fire`/`large_30sec_fire` emitters when their sequences do.
  **Measured:** `.\RunProbe.ps1 --chapter=C1 "--pos=-7600,150,-3150" "--direction=0,-0.75,-1"
  --rocket=wep_14 --fire-rockets --det --debug-anim --frames=2400` (**no `--hold`** — that is the
  whole trick: a `--hold` run auto-respawns, and the respawn's rig reset is what has been hiding
  this) → `1 active puffer(s), ~200 live particle(s): fire_n_smoke` steady at `sim_time=40`, long
  past the def's own 10 s/30 s name.
  ⚠ Do **not** conclude the authored stop is broken: the `stop-sequence` engine suite asserts
  `large_30sec_fire`'s 30 s halt and passes. Establish first whether the crash def's fire is reached
  by that halt at all here, or whether the `LOOP` that times it runs one instantaneous pass per
  *frame* (so a "30 sec" count is frame-rate-shaped, not 30 s) — the same loop semantics that make
  the torpedo's `LOOP 70` last ~1.2 s.
  *Fix shape:* the cheap version is to let `FinishEffectInstance` cover crash-rig instances too
  (they are the same "an effect def that ships no stop" case), which needs a way to mark such an
  instance other than "has a TTL entry". Weigh that against the deliberate unboundedness first —
  a crash fire is *meant* to outlive its sequence for some seconds.

- `BL-234` **No `PUFFER_STATE` the WORLD runtime reaches after the bootstrap ever builds — every
  destructible death loses its fire streaks and its long-running fire (measured 2026-08-02).**
  `WorldSession.Build` sets `animRuntime.PufferFactory = null` immediately after `Bind`
  (`WorldSession.cs:245`, inside `if (!o.KeepArchivesOpen)`), so from that line on every
  `PUFFER_STATE` the world runtime dispatches falls into the `PufferFactory == null` guard
  (`AnimRuntime.cs:2216`), counts a `PufferState(after build)` and returns. Only the emitters the
  bootstrap itself reaches — ON_STARTUP defs and startanims — ever exist. `--anim-lab` is the one
  mode that passes `KeepArchivesOpen` (`GameSession.cs:625`), which is why the effect looks fine
  there and nowhere else.
  **Reported as** C1's harbour refuel tanks losing their fire streaks and their sustained fire while
  the flying debris survives; debris is `OBJECT_MOTION`, which needs no texture archive, and the
  fireballs are the world-effects runtime's meshes, which keeps its own live factory — so exactly
  the puffer-shaped half of the death goes missing.
  **Measured A/B, same build, same def, same sim frame** (`refuel*`, `extracted/C1/zrdr/ref_fueltanks.zrd.json`):
  - `.\RunProbe.ps1 --freecam --chapter=C1 --destroy=refuel --debug-anim --frames=180 --screenshot=…`
    → `4 active puffer(s): splashpuffer1, splashpuffer2, splashpuffer3, steampuffer` — the four
    bootstrap emitters, unchanged 3 s after five kills. No fire in the shot, only the scorch decal
    and the debris.
  - `.\RunProbe.ps1 --anim-lab --chapter=C1 --play-anim=refuel1 --debug-anim --frames=180 --screenshot=…`
    → `4 active puffer(s): fire_n_smoke, trailpuffer1, trailpuffer2, trailpuffer3`, and the shot is a
    full fire column. Same runtime, same data; the only difference is the nulled factory.
  **Blast radius is the whole install, not this def.** C1's chapter zrdr alone carries 66
  `PUFFER_STATE` events over 25 distinct emitter names in 22 defs, of which exactly 4 build (the
  three `waterfalls` splash puffers + `train`'s `steampuffer`). Install-wide: C1 66/25, C1B 2/1,
  C1C 12/3, C2 99/35, C2B 14/3, C3 82/34, C4 84/52, C5 19/9 (events / distinct names). Everything
  runtime-reached is dark — the death trails and sustained fires of `fuel_tanks`, `ref_fueltanks`,
  `patrol_boat`, `fueltruck`, `lighthouse`, `barrel_fire`, `ap_h2otwr`, `ap_transmitter`,
  `ap_radiotwr`, plus the ON_CALL ambient set (`cars_moving`/`trucks_moving` dust, the six
  `firetruck*` squirts, `hauler1`'s exhaust, `train_smoke`, `speed_cue`).
  *Fix shape:* stop nulling it in the modes where the archive genuinely outlives the build. It
  already does in every game session — `GameSession.LoadArchives` hands the `TextureArchive` to
  `_sessionTextures`, disposed only by `ReturnToMenu` (`GameSession.cs:493`, `:324`) — so the
  `WorldSession` comment ("cleared right after the bootstrap so a later request is reported instead
  of hitting a closed zip") describes a lifetime that stopped being true when the session took
  ownership. The cheap version is for `GameSession` to re-arm `Runtime.PufferFactory` over
  `_sessionTextures` after `WorldSession.Build` returns; the honest version is for the flag to
  express *who owns the archive* rather than being an anim-lab special case. Either way the
  bootstrap's own nulling must keep covering the callers that really do dispose at build scope
  (`--run-tests`, the reader probes), so the fix is a lifetime decision per caller, not deleting
  the line.
  **The fix was prototyped and measured** (`if (false && !o.KeepArchivesOpen)`, reverted): the same
  five-tank freecam kill goes from 4 active puffers to **24 = 4 bootstrap + 5 tanks × 4 emitters**
  (`fire_n_smoke` + `trailpuffer1/2/3`), with the fire column rendering. So un-nulling is the whole
  visual fix; what it also exposes is the two lifetime facts below.
  *Sizing (measured over 40 s, `--frames=2400`):* 24 → 20 → 15 → a **plateau at 16 that never
  returns to 4**. Most of that plateau is legitimate — `dust_puffer`/`dust_puffer1`, the moving
  cars' dust, is ON_CALL ambient that should have been running since the world loaded. Two things
  in it are not, and each is its own follow-up rather than a reason to hold the fix:
  - `trailpuffer3` outlives its debris. `part3` ends on `BOUNCE_SEQUENCE sparkout3`, not the
    `RUN_TIME` its siblings use, so if the bounce never lands the `OBJECT_ACTIVE_STATE part3
    INACTIVE` that `BL-224`'s `EndSustainedOn` needs never fires. `trailpuffer1`/`2` do stop.
  - `fire_n_smoke` still burns at 40 s. The sequence is one `PUFFER_STATE` + `LOOP 200` and
    authors **no** `ACTIVE_STATE 0` — the loop count is the only bound, and 200 instantaneous
    passes are gone in seconds while the emitter keeps going. **This is `BL-235`'s bug on a third
    runtime** (same emitter name, same "effect def that ships no stop" shape, world runtime instead
    of the crash rig / effects runtime), so settle the loop-period question there — whether a
    `LOOP n` counts frames or seconds — before treating this one as separate work.
  Re-measure with `--debug-anim`'s once-a-second `N active puffer(s), M live particle(s)` line —
  and read the emitter NAMES, not just the count: that line is printed by every runtime in the
  session, so a world-runtime census and an effects-runtime one (`fierypuffer`) interleave and a
  count read alone looks like a collapse that never happened.
  ⚠ Traps:
  - **`--anim-lab` cannot see this bug** — it is the one mode with the factory alive. Every
    verification has to run in `--fly`/`--freecam`. The same trap runs the other way for a fix:
    proving it in the lab proves nothing.
  - **The engine already counts the misses and you will never see the number.**
    `Count("PufferState(after build)")` lands in the "event kind(s) not yet acted on" census, which
    is printed once at the end of the bootstrap — i.e. always before the first death. The log looks
    clean. Read the live `anim/debug: N active puffer(s)` line instead, or move the census.
  - **Do not chase this in the world-effects runtime or the `BL-225` pool.** Both were suspects and
    both are innocent: the effects runtime keeps its own live `PufferFactory`
    (`WorldEffectsFactory`), and a build at `fa62408` (pre-polish-5) reproduces the missing trails
    identically, so nothing in waves A–D caused it. `git log -S "PufferFactory = null"` shows the
    line unchanged since the project rename — this has never worked, it is not a regression from
    recent work.
  - **`WorldSounds.Loader` is nulled by the same block** and would have the same hole, except the
    prewarm pass ahead of it decodes every `SOUND_NODE` and one-shot name the program can ask for.
    That mitigation has no puffer equivalent (a puffer bakes an atlas per authored state, not per
    name), so don't "fix" this by prewarming.
  *Playtest after fix:* shoot C1's harbour refuel tanks and the airfield fuel tanks from the
  cockpit and confirm the streaked debris trails and the sustained ground fire; then fly a full C1
  sortie watching the dust behind the cars/trucks and the firetruck squirts, which should appear at
  the same time and are the ON_CALL half of the same fault.

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

- `BL-232` **A flight session plus `--destroy` builds the world-effects runtime TWICE (found while
  landing `BL-225`, 2026-08-02).** `GameSession.cs:1217` calls `BuildWorldEffectsRuntime` directly and
  does **not** populate the factory's `_worldEffects` cache, so `ApplyDestroyOverride`'s
  `EnsureWorldEffects` (`:1364`) finds it null and builds a second one — two stages, two runtimes,
  both live (`world-effects runtime: …` prints twice in `--plane=… --destroy=…` logs). Harmless to
  the picture today (`c1-destroy-effects` is hash-identical either way, and only the wired runtime
  receives calls), but it is a debug-path waste that the template pool multiplies: each stage is now
  `EffectPoolSlots` × ~38 template subtrees. *Fix shape:* route `:1217` through `EnsureWorldEffects`
  so there is one cache and one runtime, as `docs/architecture.md` already says there is.
  ⚠ Trap: `:1217` wires the projectile pool's `EffectSink` and the world runtime's `ExternalEffect`
  in one place and `EnsureWorldEffects` only wires the latter (and only if unset) — check the
  ordering against a `--fly --destroy` run before assuming the two are interchangeable.

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
- `BL-073` **An altitude limit** — none is modelled, and the original's is measured at ~2065 m rather than
  the data's `flight_ceiling` 2500; see "Flight-model gaps the video calibration measured" below.
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
  (a) **Layout is wrong.** 8 and 2 are swapped; 7 and **0** are both 45°-underside-*front*; 1 and 3
  are 45°-*back* (not the above/below-flank split the code assumes). **The original binds 0**; we bind
  none. Screenshots owed (numbered stills of each key) before recoding the table.
  (b) **Motion is wrong in kind, not just speed.** The original eases to AND from each position
  holding cam distance/radius constant, and the ease reads linear, not smoothstepped; ours snaps both
  ways (`ApplyFixedView` has no smoothing branch at all).
  (c) **Distance is a TUNE hand-copied from the chase camera** (`ViewDist`, shared with the chase
  camera per `BL-149` — real per-plane data exists there, e.g. Bloodhawk 18.5 vs our 16.62).
  (d) **Mid-ease interrupt behaviour is unmodelled.** In the original, pressing another key while
  easing back from a position goes straight to the new position (no snap to base first); holding
  several keys at once yields further, blended positions. Ours has neither concept — `ActiveView` is a
  single-view selector, so "combined" positions don't exist in the current code at all; this is new
  behaviour to design, not a bug in existing logic. Screenshots owed (key-combination stills).
  (e) **No gamepad binding existed in the original** (a right-stick/right-stick+modifier scheme would
  be invention) and **5 is confirmed unbound** (matches today's deliberate omission,
  `FlightController.cs:288`, so this one needs no code change — just recording it as confirmed rather
  than assumed).
  (f) **Numpad + / − trim camera distance slightly** — wholly new, unimplemented; the user already has
  video evidence for this one.
  *Fix shape:* a rebuilt `Views` table (order + the missing 0), an eased position/orientation update
  sharing `BL-149`'s distance data instead of `ViewDist`, a small state machine for the
  interrupt/combination behaviour in (d), and the +/− trim as a new input.
  *Blocked on `CAP-07`/`CAP-08`* (`playtest.md` §0).
  ⚠ **Traps.** (a) **Only `--view=` is machine-verifiable** — live held-key input cannot be scripted
  here, so any fix to (b)/(d) is correct-by-construction only until played; do not close this off a
  passing `--view=` capture alone. (b) **The layout (a) cannot be fixed before the owed screenshots pin
  the exact mapping** — do not guess-swap 8/2 and ship it; the front/back split for 1/3/7/0 is a
  different shape of layout than today's above/below split, not a two-symbol swap. (c) Don't retune
  `ViewDist`/`CamBack`/`CamUp` here in isolation — it's the same number `BL-149` owns; a fix landing
  only in one place desyncs the two cameras again. (d) Combined/multi-key positions are UNDESIGNED,
  not merely unbuilt — resist inferring a formula (e.g. "average the two directions") from a single
  set of screenshots; get the key-combo captures first.

- `BL-165` **The sun renders no lens flare; the original does.** Confirmed absent: `Launcher.cs:569`
  builds only a plain `DirectionalLight3D` (`Sun`) + a `WorldEnvironment` with no glow/bloom
  configured. Grepping `CSVM/src` for `flare`/`glow`/`bloom` turns up only the wingtip nav-lights
  (`WingLights.cs`), world lamp/beacon glow sprites (`gen_flare_yellow`, `poleflare`,
  `docklight_flare` — `SceneBuilder.cs:771`, `WorldBuilder.cs:403`), and gun/rocket effects — nothing
  tied to the sun.
  *Candidate asset, unverified:* every chapter's texture archive ships a lens-flare-shaped set —
  `bigflare01`/`bigflare02` (large core discs) + `lflare1`..`lflare4` (small secondary rings) — but
  **zero non-manifest references** turn up for them anywhere in `extracted/**/*.json` (no gamez node,
  no material, no cam_anim/zrdr def binds them), consistent with the original driving them as a
  hardcoded screen-space effect rather than a scene-graph object. This is a lead from naming + shape
  convention only — a starting guess, not a spec.
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
  structurally one number per zone, not an armour/health pair. Checked whether the data ships a real
  pair to drive a split: `destroyable_parts`' two `hp` values are **measured equal on all 88 entries**
  across all 22 defs that carry them (`docs/formats/vehicle.md:85-89,145-148`) — consistent with an
  (armor, hp) pair *or* a duplicate, unresolved (the `BL-085` hypothesis). **Until that hypothesis is
  settled, a split gauge has nothing distinct to bind its two rings to** — this is blocked on
  `BL-085`, not an independent TUNE. *Fix shape:* once `BL-085` lands a real two-pool `PlaneDamage`,
  drive `Border` from the armour pool's fraction and `Fill` from health's (or vice versa — confirm
  against `MSG_HUD_HEALTH` = "Armor: %1%% Health: %2%%", `docs/formats/vehicle.md:129-130`).
  ⚠ **Traps.** (a) Do not fabricate a synthetic armour/health split from the single existing `Hp` pool
  (e.g. armour = first N%, health = remainder) — that would look like a fix but encodes a balance
  guess the shipped data does not support; wait for `BL-085`'s falsification test. (b)
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

- `BL-236` **Blast falloff measures to a body's transform ORIGIN, not to the geometry the blast
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
  strings — `--dump-weapons` (`PlaneViewer.cs:2792`) and the weapon lab (`WeaponLab.cs:572`). The
  receiving side is one pool: `PlaneDamage.PartState` is a single `float Hp` and `Apply` a flat
  subtract (`PlaneDamage.cs:21,54-60`). Consequence, measured: **18 `BALLISTICS` entries carry
  `ARMOR_DAMAGE != HEALTH_DAMAGE`**, and that split *is* the player ammo-type system
  (`docs/formats/weapons.md`, "The player damage matrix") — with only health modelled,
  armour-piercing is strictly the **worst** round in every calibre (`30 AP` 1.5 health vs `30 DD`
  4.5), so the tier is inverted, not merely simplified. Wanted: two pools per damage zone, **armour
  first, with overflow spilling into health 1:1 within the same shot** so a nearly-stripped zone
  never wastes damage. The armour-then-health *order* is already settled
  (`docs/plans/PLAN-M3-weapons.md` C23's dominance argument); the overflow rule is the spec's.
  ⚠ **Traps.** (a) **Where the armour pool comes from is a hypothesis, not a finding.** The
  candidate is `destroyable_parts`' undecoded second value — measured 2026-07-25 it is **equal to
  the first on all 22 defs** (11 `p*` + 11 `r*`), which is consistent with an armour pool *and*
  with a duplicate. Adopting it **doubles** every part's effective HP against a balanced round: a
  balance change, not a drop-in. (b) It **contradicts C23's model 1** ("player planes — per-part HP,
  no armour/health pair"). The new evidence against that reading is `player.json`'s `crash` block,
  which spends **`armor_damage_range [50,300]` and `health_damage_range [50,300]`** (plus
  `bounce_factor 0.6`) on the *player's own* collision damage — hard to explain if player planes
  have no armour pool. Nothing in `CSVM/src` reads that block either. Settle this before writing
  code. (c) World destructibles carry **health only** (C23, measured over 16,114 defs) — this entry
  does not touch them.
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
  *Blocked on `CAP-01`* (`playtest.md` §0).
  ⚠ **Traps.** (a) The candidate data ships: `player.json`'s `highGs [9,15]` / `lowGs [-6,-9]` /
  `maxAOA 46` / `liftAOAs [5,9]` / `lift_accel_rate 0.75` are an angle-of-attack model we have no
  equivalent of — decode that before inventing a term (see the `player.json` entry below).
  (b) **It must not slow the sustained pitch RATE**, which is measured flat across 120–280 mph and
  asserted by the suite: the original bleeds speed in a pull *without* losing pitch authority, so a
  naive "less speed ⇒ less pitch" coupling would break a passing check. (c) The clip that would
  measure this directly — a sustained banked max-pull turn — was never recorded; the yaw clip is a
  verified wings-level *rudder* turn. Any fitted magnitude is inference until it is.
  **Cockpit-confirmed 2026-07-30**, not just decoded from video: the user reports the original visibly
  slows through a sustained pitch pull, and climb bleed is stronger in the original than ours, from
  the controls — this is the felt form of the same gap, not a second finding.
  *Playtest after fix:* once a load-factor drag term lands, fly a full-pull 360° and a sustained climb
  and compare the bleed by feel before closing this.

- `BL-093` **The throttle→thrust curve is undecoded, and 1/8 throttle is wrong in both directions.**
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

- `BL-094` **No altitude limit at all, and the original's is not `flight_ceiling`.** Measured: the Bloodhawk
  cannot exceed **~2065 m**, level top speed is flat ~300 mph from 714 m to 1909 m and then
  collapses (283.5 mph at 2009 m, ~234 mph at 2066 m), so whatever enforces it is concentrated in
  **1909–2066 m** and invisible below. That is 76–83% of the data's `flight_ceiling` 2500, which
  `PlaneStats.FlightCeiling` parses and nothing reads (two hits: the field and the assignment).
  A thrust fade confined to the last few percent under a hard ceiling fits the shape.
  *Blocked on `CAP-03`* (`playtest.md` §0).
  ⚠ **Traps.** (a) **Neither signature is clean, and which two points you quote decides the
  answer.** Across five apexes altitude trades smoothly against apex speed — a *performance* limit
  — but the 2065/2066 pair reaches the same altitude at 183 and 234 mph, which a pure energy limit
  cannot do. (b) The measurement that settles it is a **level full-throttle run held to equilibrium
  at 5500 / 6000 / 6500 / 6800 ft**, which no clip covers; it is the one owed capture that would
  close a whole mechanism. (c) These are true altitudes: the altimeter was proved a straight feet
  conversion (λ = 1.000 ± 0.004) against four spawn-point readings, so do not re-open the scale.

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

- `BL-147` **Pitch's transient/spin-up shape is untested — the exact twin of `BL-097`'s roll question.**
  The video calibration only ever pinned pitch's *sustained* rate (33°/s original vs 33.5 ours,
  asserted by `flight-envelope`); it says nothing about the curve on the way there or at
  sub-full-deflection inputs (the user still reads ~45° pitch inputs as sluggish at the controls even
  while accepting the sustained-rate match). Wanted: a per-frame pitch trace from a moderate (~45°)
  input, the same shape of evidence `BL-097` wants for roll.
  *Blocked on `CAP-04`* (`playtest.md` §0).
  ⚠ **Traps.** (a) `PitchTune` **0.75 is a pinned measurement** (see this section's header warning) —
  it cannot move to fix a spin-up complaint without a new frame-by-frame measurement first, exactly as
  `BL-097` cannot retune `RollTune` off a feel report alone. (b) The existing clip proves only the
  sustained 120–280 mph plateau; a moderate-deflection clip has never been shot, so any spin-up curve
  fitted today is invention, not decode. (c) Don't fold this into `BL-097` — same shape of gap,
  different axis, and pitch's own coupling to speed (`BL-092`'s induced-drag gap) makes conflating the
  two easy to get wrong.

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

- `BL-184` **Does the original animate the ammo-gauge arrow on weapon switch?** Our gauge already
  draws the pointer (`gg`/`mgarrow`) rotated to the selected belt slot, but the rotation is applied
  instantly — `DrawWeaponGauge` recomputes `-(360°/Positions) * Selected` per frame with no tween
  (`GaugeCluster.cs:613-614`). The user recalls the original's pointer visibly *moving* to the
  next/previous gun/hardpoint slot when switching (2026-07-30). Blocked on `CAP-18`
  (`playtest.md`): film G/H switching in the original with the gauge readable — sweep vs snap, and
  if a sweep, roughly how long it takes. If confirmed, the fix is a short tween of the arrow angle;
  acceptance is a capture A/B, so it is deliberately **not** in `PLAN-m3-polish-quickwins`
  (code-verifiable-only criteria) — land it in a later look-and-feel pass, after `BL-024`/`BL-025`
  so the gauge draw path has stopped moving.

- `BL-099` **C1's fuel depot: what did you actually see, and in which mission?** **⚠ NEEDS A FURTHER
  TEST BY THE USER — scheduled into polish run 4 as item 3 on 2026-07-22, then moved back here the
  same day when the investigation could not close it.** Report: in the original, the depot's state
  varies between loads — sometimes all tanks intact, sometimes **the two middle (east–west) tanks
  already destroyed at spawn** plus **blinking lights on a pipeline tower**. Original capture:
  `OriginalScreenshots/C1 IA1 Burning Fuel Tanks.png` (user-confirmed the tanks were **already
  alight at mission start, not shot**). Ours, annotated: `Screenshots/C1 IA1 Fuel tanks
  annotated.png` (freecam `x -4709 y 270 z -5809`; **yellow** = the varying tanks, **red** = the
  blinking element). Both folders are gitignored, so those citations resolve only locally.

  **CSVM is not misbehaving — do not "fix" this.** Verified from code: both `healthy` and
  `destroyed` ship `active: true` in the gamez, bootstrap pass 1 resolves them correctly via each
  `ftank0N` def's `RESET_STATE`, and we render **intact tanks, dark lights, static — every run**.
  That is what the shipped data specifies for IA1. Forcing any other state would harden a guess.

  **The mechanism, fully traced (2026-07-22) — do not re-derive it:**
  - **The ONLY route to a destroyed tank is weapon damage.** `fuel_truck01-truck_destroy01.json`
    (`activation: WeaponHit`, `health: 20.0`) → `chainreaction` → `StopAnimation fuelboxconnect1`
    → `CallAnimation chainreaction_fueldepot1` → burn crawl → `ftank_boom4`, then `ftank_boom3`.
    `chainreaction_fueldepot2` mirrors it with `ftank_boom1`, `ftank_boom2`. Each `ftank_boomN`
    flips `healthy`→inactive, `destroyed`→active.
  - **No randomness exists on that path.** The authored-random idiom in this data is
    `If {RandomWeight: w}` → state → `StopSequence` (see `gen_zep-random_prop.json`, event kind
    `If`, opcode 4537). Checked clean: `fuel_tanks.zrd.json`, `ref_fueltanks.zrd.json`,
    `fueltruck.zrd.json` (its only `RandomWeight` picks how a truck *chassis tumbles*), all 47 C1
    `cam_anim` files carrying `RandomWeight`, all 13 in `C1/IA1/mis_anim`, and `ia1.gw` — **which
    has no conditional or branching construct at all.**
  - **The blinking is a WORKING indicator, not damage.** `fuelbox1-fuelboxconnect1.json` `OnCall`,
    sequence `flashthelights`: lights on → +0.1 s off → +0.2 s `Loop {-1}`, alongside `scale_hose`
    rocking the `rockerarm` — a pump station refuelling. Its damaged counterpart `fuelboxbreaks1`
    turns those lights off permanently. **So blinking and destroyed tanks are anti-correlated
    within a chain**; seeing both means one chain ran and the other did not (one side wrecked while
    the other pump still works).
  - **⚠ The anomaly that blocks closure.** "The two middle, east–west" = `{ftank02, ftank04}` by
    world X (01 −4722.8, 02 −4691.2, 04 −4682.8, 03 −4618.2). **No chain produces that pair** —
    the chains partition `{01,02}` and `{04,03}`, and no partial-timing cut gives it either. Caveat:
    `ftank02` sits ~52 m north of the other three, so the visual "row of four" from that freecam
    pose may not match the X ordering.

  **Why it cannot be actioned yet.** In IA1 the whole route is unreachable: `ia1.gw` deactivates
  `fuel_truck01`/`02`, `nodes.json` ships both `active: false`, and the only caller of
  `trucktodepot1` anywhere in the tree is a **C1/M02** script. **User-confirmed 2026-07-22: the
  trucks are only in M02.** So per the shipped data C1 IA1's depot **cannot burn and cannot blink**
  — which contradicts the report and means one of these is true, undiscriminated by the files:
  1. **The observation was C1/M02, not IA1** — the one mission where trucks drive, pumps blink and
     the trucks are shootable. **Now the leading explanation**, given the trucks are M02-only.
  2. **IA scenario selection lives in the exe** — `ia.zrd.json` carries `disallow_missions` and
     `dogfight_ace`/`dogfight_squadron` spawn sets, so instant action has multiple scenario types
     and per-scenario setup could re-activate the trucks. Nothing in the extraction shows this.
  3. **The exe drives it directly** — the `fire2`-trigger class, i.e. not recoverable from files.

  **What the user needs to test, in order:** (a) re-run **C1/M02** in the original and see whether
  that is where the burning depot and blinking pumps appear; (b) if it really is IA1, note whether
  the **fuel trucks are visible** — our data says they are off there, so seeing them breaks the
  model at its root; (c) if IA1 and no trucks, record **which** tanks burn across several loads, to
  test the `{ftank02, ftank04}` anomaly against the chains' `{01,02}` / `{04,03}` partition.
  Until (a)–(c) come back, **there is nothing to implement** — and if the answer is (2) or (3) this
  closes as blocked, alongside the `fire2` trigger and the weather-zone selection.

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
  `StallNoseRate`/`KnifeAlignFloor`/`ClimbGravityScale`/`LowSpeedDragBlend` alone — needs video
  (`CAP-05`).
- `BL-116` **Numpad camera views — superseded 2026-07-30.** The "layout is settled" claim this entry
  made is now contradicted by cockpit testing (8/2 swapped, 7/0 both underside-front, 1/3 both rear,
  and the original binds 0 which we don't), and the "distance is user-recall, not data" claim is now
  false — `extracted/zrdr/camparam.zrd.json` ships real per-plane distances. Superseded by `BL-149`
  (the shipped camera data) and `BL-150` (the plan-sized rebuild covering layout, easing, interrupts,
  and the +/− trim). Kept as a retired ID, not deleted, per this file's permanent-ID rule.
- `BL-148` **Stall warning is binary, not graded — and the ramp itself doesn't exist yet.**
  `GaugeCluster.cs:247` gates the blink on `Stalled && WarnPhaseOn` — `Stalled` is a hard boolean from
  `FlightModel.isStalled()` (`FlightModel.cs:328-332`, a single `Speed < stallSpeed` threshold with no
  margin state) and `WarnPhaseOn` is a fixed 50%-duty blink at `WarnBlinkPeriod` **0.4 s**
  (`GaugeCluster.cs:51,130`) — so the cue snaps on at full intensity the instant the boolean flips,
  never rising as the stall approaches. The **[spec]** target wants a rising ramp. *Wanted:* a
  proximity fraction (e.g. margin above `stallSpeed`) driving blink rate or opacity before `Stalled`
  itself goes true.
  *Blocked on `CAP-06`* (`playtest.md` §0).
  ⚠ **Traps.** (a) This needs a code change before it needs a magnitude — do not treat it as a retune
  of `WarnBlinkPeriod` alone. *Playtest after fix:* once a ramp lands, A/B its rate against the
  original's stall-warning video (the same capture as the stall/knife-edge recording) before calling
  it graded. (b) `isStalled()` currently returns only a boolean — adding a continuous margin changes
  its signature/call sites (`FlightController.cs:734,757`); don't bolt a second parallel margin
  calculation on top instead. (c) Don't reuse `LowAltAglM`'s pattern uncritically — the low-alt cue is
  legitimately binary (a fixed AGL gate, no spec claim of a ramp there), so a shared "warning"
  abstraction that ramps both would over-apply the fix.
- `BL-149` **`camparam.zrd.json` ships full chase/third-person camera tuning and nothing reads it.**
  Full `default` block: `dist` **13.0**, `dist_factor` **0.01**, `dist_vary` **0.1**, `dist_catch_up`
  **1.0**, `dist_min`/`dist_max` **15.7 / 25.0**, `pos_catch_up` **2.0**, `thirdp_height` **0.138**,
  `thirdp_pitch` **0.29**, `look_catch_up` **3.0**, plus `back_dist_min`/`max` (15.5/55, presumably a
  look-behind view), a `death_*` block (interval 2.0, z 0, x 80, alt 5, min_alt 15.1), a `crash_*`
  block (horiz 30, y 45, chord_y 1000, elev 40), and a `flyby_*` block (watch time 3.8–4.3 s, radius
  5.5–7.0, interval 1.9–2.3 s, switch dist 70–85). Seven planes override `dist`/`dist_min`/`dist_max`
  individually — **Bloodhawk 18.5**, Fury 17.0, Peacemaker 18.0, Kestrel 14.5, Firebrand 20.5, Warhawk
  20.0, Balmoral 25.0 (Balmoral also overrides `thirdp_height`/`thirdp_pitch` to 0.2/0.2). Our chase
  camera is airframe-*independent* hand-picked constants (`CamBack` 16, `CamUp` 4.5 → `ViewDist`≈16.62
  for every plane, `FlightController.cs:240,284`) — 10% off the Bloodhawk's own shipped 18.5.
  `CamRotSmooth` 7/s (`FlightController.cs:242`) is exactly the kind of number
  `pos_catch_up`/`look_catch_up`/`dist_catch_up` may already answer. *Wanted:* a `camparam` reader +
  wiring `dist` per plane into `CamBack`/`CamUp`'s magnitude (keeping the behind-and-above direction)
  and mapping the catch-up triplet onto `CamSmooth`/`CamRotSmooth` once their units are derived.
  ⚠ **Traps.** (a) **Units of the catch-up fields are undecoded** —
  `pos_catch_up`/`look_catch_up`/`dist_catch_up` read plausibly as our existing `1/s`
  exponential-smoothing rate, but could as easily be a frame count, a seconds-to-settle, or something
  else; do not wire a number in without confirming the shape (a settling-time capture, or the same
  math-consistency method `METHOD-14` in `docs/verification.md` warns can fool itself). (b) This is
  the SAME distance the numpad fixed views borrow (`ViewDist`, `FlightController.cs:281-284` —
  cross-ref `BL-150`), so a `dist` fix here moves both cameras at once; land them together, not
  separately. (c) `dist_min`/`dist_max`/`dist_vary` suggest the ORIGINAL's chase distance is itself
  dynamic (varies with speed/situation, not fixed) — swapping in a single static `dist` number is a
  partial fix, not the full mechanism; say so rather than declaring this closed once `dist` lands.
- `BL-118` **Cloud puffs** — opacity and density. **Cloud deck** — brightness reads ~40 units lighter
  than the original. **Playtest (2026-07-30), two new specifics + mechanism traced.** (1) The deck
  itself shows dense cloud puffs while flying through it. (2) In C1/IA1, puffs show around the plane
  at *all* height levels rather than a confined band. Root cause: `CloudPuffs.cs`'s
  `BandBelow`/`BandAbove` (120 m/280 m) plus `VertFull`/`VertFade` (200 m/560 m) together make the
  field seed visible puffs for any camera altitude in **[290 m, 1964 m]** for C1/IA1 — effectively the
  whole flight envelope (ceiling ~2065 m, `BL-094`) — while `weather.json`'s own `CLOUD_COVER` block
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
  real flight (both the panel-flip and the smoke/fire trail have been confirmed to render in real
  flight, `docs/HISTORY.md` 2026-07-31 — this item is a magnitude/feel judgement, not a mechanism
  question). Tree softness is retired dead code (`docs/HISTORY.md` 2026-07-23), not a TUNE — do
  not re-add it here.
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
- `BL-124` **Knife-edge nose sag (polish-4 item 6, landed 2026-07-23)** — `KnifeNoseSag` **0.07 rad (≈4°)**,
  the bound the nose settles to at full knife-edge, and `KnifeNoseRate` **0.2 rad/s**, how fast it
  gets there. Both `FlightModel.cs`. Presence and direction are proven by scripted test; **magnitude
  is not and cannot be** — it needs the original at the controls. Measured at the landed values:
  nose −0° → **−4°**, settled path −6° → **−10°**, sink 11.8 → **19.4 m/s**, 398 → **634 m** lost in
  35 s. Three things to judge in the cockpit: (1) does a −4° nose / −10° path / ~19 m/s sink feel
  like the original's knife-edge; (2) **steep wings-level climbs are also affected** — a full-pull
  zoom loses ~11° of apex — because `knife = 1 − |up·Y|` grows with pure pitch at zero bank, so if
  that nose-heaviness feels wrong the fix is **gating on actual bank instead of `1−wingVert`**, a
  code change rather than a retune; (3) stall-into-knife-edge recovery should not feel "doubled".
  *Blocked on `CAP-05`* (`playtest.md` §0).
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
- `BL-130` **The labs are mouse-driven** (`--viewer`: damage on H, livery on L, mesh on M) and have had no
  interactive playtest beyond scripted verification.

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
