# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. **The active plan is
`docs/PLAN-testing.md`** (testing & verification infrastructure); M3 (weapons) and the M2/2.5 polish
runs plus the anim-debugger and data-driven-crash plans are complete and archived in `docs/plans/`. Per-item history/diagnosis
detail is in `docs/HISTORY.md` (dated entries) and `docs/architecture.md` (module bullets); how to
verify a change without fooling yourself is `docs/verification.md`. **The live list of hand-tuned
constants awaiting playtest lives here** (see "TUNE constants pending playtest" below) — it moved
out of CLAUDE.md on 2026-07-22, since CLAUDE.md's status section is current-state-and-next-step
only. When an item gets scheduled into a plan, move it there; when it lands, delete it here.

## Milestone 3 Polishing (playtest findings, 2026-07-24)

A pass of at-the-controls findings from the user (M3 weapons/destruction). **Six localized fixes are
now merged into `m3-polishing`** (built clean together), each still owing a cockpit playtest; one
attempt **failed and is documented under "Still open" below**; the **larger items** follow. The
fixes were code-only, so this section is their documentation trail. **When `m3-polishing` lands on
`main`, give each merged fix a `docs/HISTORY.md` entry and delete its row here** — do not let this
table outlive the landing.

### Merged into `m3-polishing` — pending playtest

| Finding | Branch | Change | Confirm in the cockpit |
|---|---|---|---|
| Rocket advances to the next pylon after one shot (round-robin) instead of draining the selected pylon | `fix/rocket-drain-pylon` | `FlightController.NextArmedHardpoint`: keep the cursor on the just-fired pylon (`_nextPylon = idx`) instead of `+1`; the scan skips it once dry | wing empties one pylon fully, in order, before the next starts (see "Rocket firing order" under Feature backlog) |
| A rocket that reaches max range vanishes silently | `fix/rocket-detonate-at-range` | `Projectile._PhysicsProcess`: at range-expiry a **rocket** calls `Impact(weapon, pos, null)` (default-surface effect+sound); guns still expire silently | the `default` IMPACT effect reads acceptably as a mid-air self-destruct (not a ground/water splash floating in the sky) |
| Gun tracers appear behind the plane | `fix/tracer-grow-from-muzzle` | `RenderTracers`: cap the drawn streak to distance travelled (`min(TracerLength, Range−DistLeft)`) so it grows out of the muzzle | tracers start at the muzzle; steady-state tracers (round >14 m out) unchanged |
| Rockets feel too fast | `fix/rocket-speed-tune-hook` | adds a `Config` knob `weapons.rocketSpeedScale` (default **1.0 = data speed, no change**); when set, scales rocket velocity **and** accel so it still despawns at the same range | **value is a TUNE** — set e.g. `0.7` in `config.json` and A/B vs the original (see TUNE list) |
| `WARNING: … SOUND 'snd_exp_ground_a' … no audio session` on every ground crash | `fix/crash-sound-warning` | `PlaneViewer.BuildFlightCrashRuntime`: crash runtime gets `SoundHandledElsewhere = true` (matches the world-effects runtime) | warning gone; ground boom still plays (via `FlightAudio.OnGroundExplosion`) |
| Flying into C3's (invisible) spiderweb crashes/damages the plane | `fix/opacity-fade-collider` | `AnimRuntime.SetSubtreeOpacity`: a fade to ~0 now **disables the subtree's colliders** (edge-triggered, reuses `SetCollidersEnabled`); data-driven, no node name hard-coded | fly at C3's faded web — no invisible wall, no damage. ⚠ **Premise unconfirmed:** the user doubts the web is faded at start — verify in the original (`playtest.md` §8): is the C3 spiderweb visible / faded / solid there? If **visible and solid** in the original, our *fade* is the bug, not the collider. ⚠ couldn't confirm from code that `spiderweb` has a `col` body (assets gitignored); the fix generalizes to **all** fade-to-0 subtrees |

### Still open — the attempt did not work

- **`det == 0` invert error when destroying things — the scale-floor did NOT fix it (user-tested
  2026-07-24).** `ERROR: Condition "det == 0" is true. at: invert (core/math/basis.cpp:47)` still
  appears on some kills. The attempted fix (branch `fix/motion-scale-zero-guard`, **not merged**)
  floored `MotionRuntime.Seek`'s ramped scale off zero (sign-preserving, ε=1e-3), the same guard
  `PoseScale`/`ScriptPlayback` carry, on the theory a scale ramp reaching `(0,0,0)` made
  `basis.Scaled(scale)` singular. It builds clean but the error persists, so **that is not (or not
  the only) singular-basis source.** Unguarded suspects to check next:
  - **`InheritedLocal`'s direct `parentBasis.Inverse()` (`AnimRuntime.cs` ~2655, the crash/launch
    path)** — inverts a singular basis if any *ancestor* node carries a zero scale; the agent that
    wrote the scale-floor flagged this as the spot it deliberately left alone.
  - other `Inverse()` / `AffineInverse()` / `look_at` / zero-scale sites in the destruction,
    puffer/effect, and `PlaceTemplateAt` / `GlobalTransform`-relative paths.
  ⚠ **Do not re-apply the `MotionRuntime.Seek` scale-floor — it is tried and insufficient** (the
  branch keeps it for reference). Find the real source by reproducing the throwing kill and adding a
  temporary det-check log at each inverse in the destruction path to point at the culprit.
  **⚠ Pass 2 (2026-07-25) sharpened the stakes:** on the `kkgate` door the throw coincides with the door
  **not moving and keeping its collider** — the leading hypothesis is now that the exception *aborts the
  death sequence* before its hide/collider-removal/piece-launch events run, making det==0 and the "door
  won't destroy" symptom one bug. See "Playtest pass 2" finding 13.

### Larger items — documented, not fixed

- **Break-apart debris barely moves — "parts only move a short way."** The piece launch is
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

- **C2 SeaHangar doors don't despawn and stay collidable after shooting the propane tank.** The
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

- **Hardpoint ordnance won't cycle by key or pad (H / D-pad Right) — not a code defect.** The input is
  wired and does mutate state (`FlightController.RocketSelectPressed` → `CycleWeaponSelectors`), but it
  is gated `_ordnanceTypes.Length > 1`, and all 11 stock loadouts carry a single hardpoint type
  (`wep_06`), so after dedup there is exactly one ordnance type and nothing to cycle. (Gun-group select
  **G** works because several planes have 2+ firable gun groups.) The real question is **design**, the
  same one flagged in "Rocket firing order": does the original let the player select an individual
  **hardpoint**, or only drain them in pylon order? Meaningful mixed-ordnance cycling arrives with mixed
  loadouts (the M4 gun/hardpoint configurator, Feature backlog). Nothing to fix in M3.
  **⚠ Reopened by pass 2 (2026-07-25):** the user says per-hardpoint *selection* should work (H selects
  a pylon; auto-advance only when the selected one empties) — so this is no longer "nothing to fix." See
  "Playtest pass 2" finding 15 below.

### Playtest pass 2 (2026-07-25) — new findings + verdicts

A second at-the-controls pass (user, answers "by feeling"; the four ambiguous points were grilled and
settled). Reference shots are in `OriginalScreenshots/` (gitignored — cited by filename). Root causes
were traced to code before writing. Verdicts on pass-1 items are noted on their rows above; the new
work is below.

**Gun visuals (finding 1).**
1. **Muzzle flash too large + never rotates.** `MuzzleSize = 2.2 m` (`Projectile.cs:64`) reads far bigger
   than the original's compact flash, and the billboard has a fixed orientation (`Projectile.cs:638`) —
   the original randomises the flash angle every shot. Ref: `MuzzleFlash1..3.png` (small textured
   starburst, random roll) and shot 2 of `C1B IA1 Bloodhawk tracer and ejection.png` (one compact forward
   flash on a single wing). *Fix shape:* shrink `MuzzleSize`; add a per-sprite random Z-roll (needs a
   rotation field on `Sprite`, and the billboard to honour it). ⚠ This flash also masks A10 muzzle-
   placement verification (finding 11: "hard to see with the large flash") — re-check A10 after it shrinks.
2. **Tracers read as long glowing streaks, not short yellow dashes.** `TracerLength = 14 m`, additive,
   warm-yellow (`Projectile.cs:58,83`). Original = small discrete yellow streaks (`…tracer and
   ejection*.png`). *Fix shape:* shorten/narrow the streak and reconsider the additive bloom — a look
   TUNE against the two ref shots. (Distinct from the pass-1 `fix/tracer-grow-from-muzzle` position fix.)
3. **Casing (brass) ejection is missing entirely.** The original ejects shells with a **white smoke puff**
   below/behind the plane (clusters of white puffs + tiny yellow shells in `…tracer and ejection*.png`
   and `Water Splash.png`). Nothing in `Projectile.cs` emits casings. *Blocked on data:* check whether a
   brass/shell-eject effect def or puffer exists in the weapons/effects data, or whether it is engine-side
   (then it becomes a synthetic effect, a fidelity guess). Medium priority — visible, adds life to gunfire.
4. **Weapon lab fires a group's muzzles synchronously (flight is correct).** `WeaponLab.FireVolley`
   (`WeaponLab.cs:411-421`) spawns from *every* mount node at once; flight alternates. Lab-only fidelity
   nit — low priority (the lab arguably wants to show all muzzles). Recorded so it is not re-diagnosed as
   a flight bug.

**Rocket visuals (findings 2, 4).**
5. **The fat orange→grey smoke trail is missing — the biggest rocket gap.** A rocket with a MODEL body
   gets only a slim `RocketExhaustScale = 0.5` exhaust streak (`Projectile.cs:62`); the original's
   dominant visual is a **thick smoke trail fading orange→grey** (`Rocket Streak 1..3.png`). This is the
   deferred `MODEL_ANIMATION` trail — it is why the round "looks completely different" and "too fast to
   see." *Schedule it.* ⚠ Reframes the pass-1 "rockets feel too fast" TUNE (`weapons.rocketSpeedScale`):
   the user now attributes the speed impression to the **missing trail**, not the velocity — treat the
   speed scale as probably-neutral and fix the trail first.
6. **Rocket explosion looks different + faster than the original.** Goes through the D32 world-effects
   puffer (`EffectSink`). Tuning gap — cross-ref the "World-effects runtime follow-ups" (M3 D32) item.

**Impacts & surfaces (findings 3, 4).**
7. **Water impacts produce NOTHING — the sea has no collider.** Hits fire only on a raycast collider
   strike (`Projectile.cs:416`); the sea isn't collidable, so gun/rocket rounds pass straight through →
   no hit, no splash, no sound (user, grilled: "nothing at all"). **This supersedes the pass-1 claim that
   the D30 water splash worked in flight** — that reading was headless/forced, not at-the-controls. *Fix
   shape:* give the sea surface a `water`-tagged collider (the `SurfaceMeta` classifier already keys off
   it, `Projectile.cs:544`) so raycasts register; then the splash instances. Target = small white
   `Splash0N`-style sprites walking across the surface (`Water Splash.png`). ⚠ A sea collider interacts
   with the crash system — a water crash today falls through to the under-map backstop (see the water-
   crash-variant feature-backlog item). Scope the collider so it feeds *impacts* without turning every
   sea-skim into a crash (e.g. a projectile-only collision layer).
8. **Dirt impact too prominent — should be small tumbling debris.** Dirt falls to the stand-in spark: one
   big `ImpactSize = 3 m` orange billboard (`Projectile.cs:66,515`). Original = small, **randomly-rotated
   tumbling debris** sprites (`Dirt Splash.png`: "not billboards — rotating randomly"). *Fix shape:* a
   small dirt-debris burst (a few sprites, random roll + short arc) in place of the single fat spark.
9. **Building vs dirt impacts are identical (both a fireball puff).** Original: buildings → `large_fireball`;
   dirt (HE) → `he_ground_effect` (a light flash, no puff). The per-surface lookup exists
   (`Projectile.cs:490`) but isn't differentiating. *Investigate:* does the HE rocket's `weapon.Impact`
   carry distinct `buildings` vs `default` entries, and does terrain tag so dirt classifies `Default`
   while a building classifies `buildings` (`ClassifySurface`, `Projectile.cs:544`)?
10. **Oil-tank death fire puff floats too high and lingers far too long.** One puff climbs well above the
    wreck and stays (the authored `large_30sec_fire` is literally a 30 s fire). The original oil-tank kill
    is a rich ground-level fireball + smoke column (`Oil Tank Explosion1..3.png`), not a lone high floating
    puff. *Investigate:* the puffer's anchor (it should sit at the wreck, not climb) and its mode/lifetime.
    Cross-ref the D32 world-effects follow-ups.

**Destruction & doors (findings 12, 13).**
11. **Damage stages (smoke→fire) never render in flight.** Guns-only into a tower, HP visibly falling,
    **no intermediate smoke or fire — only the final blast** (user, grilled: "never showed"). The stages
    fire headless (`--damage-hd` proves the 60 %/30 % stages), so this is a **render gap**: the
    `DAMAGE_SEQUENCE` stage effects aren't reaching the flight world-effects runtime (same class as the
    D32 gun-smoke follow-up — the puffer factory is torn down post-build outside the labs). *Investigate:*
    does the flight damage path (`AnimRuntime.DamageAt` → stage sequences) route stage effects through the
    flight `EffectSink`/world-effects runtime, or only the death effect?
12. **Debris trajectory is wrong, not merely slow.** In-flight kills throw pieces "but not in the correct
    trajectory." Fold into the "Break-apart debris barely moves" larger item above (world objects inherit
    no launch momentum + `bounce_sequence` ground-rest unsimulated + magnitude decode is TUNE) — this pass
    adds the *trajectory-shape* symptom, not just the *distance* one.
13. **⚠ Strong lead — the `det == 0` throw likely ABORTS the door death sequence (same bug as "door never
    moves / colliders remain").** Re-confirmed 2026-07-25: shooting `kkgate`'s propane tank throws
    `ERROR: Condition "det == 0" is true` (invert of a singular basis) and the door then does **not** move
    and **keeps its collider**; in the original the door deactivates, its pieces fly + fade, and it loses
    collision. Hypothesis to test first: the inversion exception thrown mid-sequence unwinds the death
    sequence **before** the hide + collider-removal + piece-launch events run — so det==0 and the "door
    won't destroy" symptom are **one** bug, not two. Cross-ref the "Still open — det==0" item above (its
    unguarded-inverse suspect list) and the "C2 SeaHangar doors" larger item. *Verify:* wrap the throwing
    kill, log which inverse throws, and confirm the sequence aborts at that point.

**Gauges, selectors & cues (findings 6, 7, 9).**
14. **Ammo gauge: remove the yellow tier — the original is green→red only** (finding 9a, settled against
    the original). Retires the `IndicatorLowFrac` TUNE's *existence*: the belt lights step green→red, no
    yellow. The red-onset fraction remains a smaller TUNE. `GaugeCluster` — drop the yellow state.
15. **Hardpoint selection should work — user correction (finding 7).** Each pylon counts for itself; **H
    should select an individual hardpoint**, with auto-advance only when the selected one empties. H is
    currently gated `_ordnanceTypes.Length > 1` and dedups to one type → nothing to cycle. *Redesign:* H
    cycles **pylons**, not ordnance types; the selected pylon drains, then auto-advances. Composes with the
    `fix/rocket-drain-pylon` order fix. ⚠ Fidelity: confirm the original truly offers per-hardpoint
    *selection* (vs fixed-order draining) before building any indicator — the user believes it does.
16. **Rocket empty-clip cue never heard (finding 6b).** The cue plays on a dry pull
    (`FlightController.cs:789-793`), but the dry branch is only reached when `_rocketCooldown <= 0`
    (`:782`) — a pull within 1 s of the last shot returns early and stays silent. *Investigate:* is the
    cue simply inaudible, or is the cooldown swallowing the pull that would sound it?
17. **Weapon-switch sound not verified (finding 7).** User didn't check whether G/H should play a select
    click like the original's ammo-selector UI (we likely play none). Added to `playtest.md` as an owed
    check.

**Test / debug affordances (findings 6, 14).**
18. **Debug low-ammo knob.** Empty-clip can't be tested without firing 2000+ rounds. Add a start-with-low-
    ammo switch (e.g. `--ammo=N`, or per-group). Fits `docs/PLAN-testing.md`'s test-affordance theme.
19. **Debug: colour world objects by type / class.** The user couldn't locate a C2 water tower or the
    storefront facades (found filmset panels instead). A "colour by object class" overlay (destructible /
    facade / tower / clutter) would make targets findable at the controls. Cross-ref `docs/PLAN-testing.md`
    **D32 Node Lab** (destructibles view) — extend it with a colour-by-class mode, or a standalone overlay.

## Blocked / deferred

- **Pad-read-on-focus gate — user may veto (shipped 2026-07-22 alongside polish-4 item 7's focus
  mute).** Pads are read only while the window has focus. It is its own commit, so reverting it
  alone keeps the mute; the cost while active is that an unfocused window ignores the controller.
  Waiting on the user's verdict from normal play — delete this entry if it feels right, revert
  that commit if it does not (see `docs/HISTORY.md` 2026-07-22 "Focus mute, and the
  pad-read-on-focus question closed").

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
- **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

- **`SpinMotion` re-seeds its rest pose from an already-spun pose (found 2026-07-22, deliberately
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
  | one-shot `Sound` | — | **LANDED M3 D31 (2026-07-24).** `AnimRuntime.HandleSound` fires it as a fire-and-forget `WorldSounds.PlayOneShot`; the 4,378 `OnCall` + 1,650 `WeaponHit` combat audio now sound on deaths/hits, and the 21 `DYNAMIC_WEIGHTS` groups are decoded (`SoundDefs.LoadGroups`). No longer in the report. |

  **Pick these up when the thing they depend on exists** — a cutscene player for `Callback` — not
  before. `ObjectCycleTexture` needs neither; it needs a mission that
  actually builds a `taildamage` node, which none of the ones this project defaults to do.
  The one kind from that list that *was* reachable, `OBJECT_OPACITY_STATE`, landed 2026-07-22
  (`docs/HISTORY.md`) — which is why it is not in this table.

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
  guess deletes visible world content, which is strictly worse than drawing both. The user's
  zone A/B has since answered **C5 = zone1** (2026-07-22), which would mean hiding C5's 149
  `zone3` nodes — but that is exactly the guess-what-to-hide risk, and the *fog* answer does
  not license a *geometry* change. C1–C4 are still unanswered. Do not act on this until the
  remaining chapters are settled and there is a visible artifact it demonstrably fixes.

- **`WorldPartitionSetActive` — ⛔ CORRECTED 2026-07-23. This entry used to claim it was "the real
  runtime system the original uses to pick between coarse and fine ground", and that claim is
  FALSE.** Measured: **all 25 uses are `support\c3\*.gw` — C3 only** — and it takes rectangle
  coordinates, not node names. It never appears in any C5 script, so it cannot be the mechanism in
  the chapter that actually has the bug. It was briefly promoted to "prime suspect" for the C5
  ground z-fight on 2026-07-22 and that promotion was wrong; the real mechanism is the **subface
  flag** (next entry). Kept as a correction rather than deleted because the wrong claim was
  repeated across three documents. Whatever `WorldPartitionSetActive` does for C3 is still
  unimplemented and still undescribed — but it is a C3 question, not a ground-LOD one.

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

### HUD & audio

- **Crash damage display blinks fully red.** `GaugeCluster.cs` blinks a zone for `DamageBlinkTime`
  on `OnPartDamage` and picks the red variant at `frac <= RedAt`, but the only caller is a graze
  hit — `FlightController.Crash()` touches audio, fireball, breakup and visibility and never calls
  into `Gauges`. So the all-red state is not a crash behaviour being mis-fired; it is the ordinary
  damage path left latched. Check what the original shows on a crash before wiring anything.
- **Gauge needles are the wrong shape** — they come from the game's own HUD textures. Could be
  drawn procedurally instead in a future Hi-Def mode.
- **`--headless` + `--screenshot` NREs forever instead of capturing.** `PlaneViewer._Process`'s
  capture block calls `GetViewport().GetTexture().GetImage()`, which returns **null under the dummy
  renderer** (`texture_2d_get: Parameter "t" is null`,
  `servers/rendering/dummy/storage/texture_storage.h:110`). The NRE is caught and logged by Godot's
  C# bridge, so the frame counter never advances past the capture and **the process never quits** —
  one C1 run produced a **206 MB** stderr log in ~10 minutes and was still going. Found 2026-07-22
  while running the polish-4 item 7 regression; pre-existing, unrelated to that change.
  **Screenshot runs must be windowed.** Worth either making the capture path fail loudly and quit
  (null check + `GetTree().Quit()`), or rejecting `--headless` together with `--screenshot` at
  arg-parse time with a clear message.
  ⚠ **Traps.** The failure is silent from the *caller's* side: `Start-Process -Wait` returns exit 0
  because the console wrapper exits while the real Godot child keeps running, so a harness that
  trusts the exit code reports a clean pass and leaves an orphan writing to the log file. That
  orphan then **contaminates the next run's logs** — it holds a handle on the log path and keeps
  appending, which manufactured a bogus "16,576 errors in C1B" reading before it was spotted (the
  same class as `docs/verification.md`'s foreign-Godot rule, self-inflicted).

- **`OBJECT_MOTION_FROM_TO`'s `*_delta` channels are silently dropped — all 26 of them.**
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
- **The gamez node `active` flag is never read.** `GameZ` parses no node flags at all (`flags` is
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

- **`SurfaceRankCap` = 5 produces genuine zero-separation coplanar pairs.** 0.0–4.1% of built
  meshes per chapter have more than 6 (material, priority) groups, so ranks 5+ all collapse onto
  the same bias; the worst mesh has **32** groups (C4). It is a real hole in the tie-break, and it
  is **invisible to any node-level scheme** because both sides share a node. Measured 2026-07-22,
  `analysis/item9-depth-bias/`.
  **⚠ Partly overtaken 2026-07-23 — re-measure before acting.** The four C5 pairs this entry used
  to cite as its evidence (774,152 m² across `g4642` — `cblock3` vs `cblock6`, `cblock1` vs
  `cblock4` — plus `g4622` and `g4674`) are all **subface-over-base pairs**, and the subface fix
  now separates them by half a priority level, which is 50× the rank step and cannot be collapsed
  by the cap. The *cap* is unchanged and the structural hole is unchanged, but **this entry no
  longer has a measured example**: the surviving cases are whatever zero-separation pairs remain
  once subfaces are excluded, and that number has not been re-measured. Do not quote the old
  774,152 m² figure — it is now the area the fix resolved, not the area at risk.
- **`node_bias` already spans 1.22–2.86 priority levels per chapter** (C1 1.77, C1B 1.40,
  C1C 1.41, C2 1.24, C2B 1.22, C3 1.35, C4 2.07, **C5 2.86**). `docs/architecture.md` recorded
  this as an accepted corner case ("a prio-0 node >~4000 indices later can out-bias a prio-1
  overlay; no such pair observed in C1") — measured, the real span is **up to nearly three levels
  and is the normal state of the chapter**, not a corner case. Consequence: today **2–16% of
  cross-node conflicting pairs already resolve the wrong way round**, because within-mesh surface
  rank can out-bid the cross-node term. A dense conflict rank would fix this as a side effect
  (28 × 5e-6 = 1.4e-4 = 0.7 levels). Measured 2026-07-22, `analysis/item9-depth-bias/`.
- **✅ CLOSED 2026-07-22 by user ruling — C1B z-fighting is NOT a bug: "it's in the original."**
  The C1B repro pose reproduces the original game's own z-fighting, so our replication there is
  already correct and **must not be "fixed"**. This retires the open question that sat here (does
  surf draw over water at that pose — moot: the original z-fights, so neither surface stably
  wins), and it retires the **dense conflict-rank scheme entirely**, because C1B was the only
  half that scheme could fix (C5's conflict is within one node, where `node_bias` cancels).
  **Do not raise `NodeOrderBias`** — now for a stronger reason than before: the measured
  5e-8 → 2e-6 control taking C1B 30.95% → 2.35% was never an improvement, it was 1.4 points from
  erasing a faithful artifact. Kept as a closed entry rather than deleted because three separate
  sessions have tried to fix this pose. Evidence: `analysis/item9-depth-bias/`.

- **Authored `_1`/`_2` mip levels are ignored; we box-filter our own instead.** 52–91 base textures
  per chapter ship hand-authored half- and quarter-resolution levels (`cblock1`, `cblock1_1`,
  `cblock1_2`), referenced by **no gamez material**, and `TextureArchive.cs:100` generates its own
  mips by box filter. The authored levels keep **0.35–1.12% of pixels above luminance 128** where a
  box filter keeps **0.00%** — i.e. the artist preserved the street lights at distance and our
  filter averages them into the dark. This is precisely the user's observation of the original
  going "dark with some points → brighter illuminated street" with distance (2026-07-22).
  Measured 2026-07-23, `analysis/item9-depth-bias/CBLOCK-LOD.md` §1b.
- **`materials` is a per-polygon LIST and `SceneBuilder` reads only `[0]`.** 352 C5 polygons carry
  a **second textured pass** with its own independent UVs: `z3_foggrad`/`foggrad8x64` fog gradients
  (142), `buildingspotlighted` (34), `fadedsign01-03`, `nypd`, `clock`, and plane logos. A whole
  second-texture-pass feature is silently dropped. Measured 2026-07-23, same report §3.
- **`zone_set` is parsed by nothing** (`grep zone_set CSVM/src` → 0 hits). It is a **per-polygon**
  weather-zone membership list (C5 uses 1 and 3). Not a ground selector — that was checked and
  ruled out — but a real unparsed field, and the per-polygon granularity is interesting given
  weather zones are otherwise handled per chapter.
- **Open question, do NOT act on it from the subface report:** `cblock4/5/6` carry their own
  **disjoint** clutter building templates (`cb12a`–`cb24a`) versus `cblock1/2/3`'s
  (`cb00a`–`cb11a`). That is most likely *why* a fully-buried base ground layer exists at all. Once
  the subface fix lands, the base layer will be correctly hidden while **its clutter presumably
  still spawns** — check whether C5 draws doubled buildings, but treat this as a question, not a
  finding (the report flags it as an open question, not a measurement).

## Feature backlog

- **Data-driven crash — the remaining variants/follow-ups (PLAN-data-driven-crash COMPLETE for the
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
  2. **The water and air variants** themselves. ⚠ Both are currently **unreachable**: `ClassifySurface`
     (`FlightController.cs`) returns `CrashSurface.Ground` for every crash. **Water**
     (`player_crash_water`: `plane_big_splash` + `large_steam_spray`, the `destroy_crash` sequence)
     needs a sea-surface signal the collision system does not expose (a water crash today falls
     through the collider-less sea to the under-map backstop, which respawns rather than crashing);
     note `snd_exp_water_a`. **Air** (`player_crash_default`, no-impact destruct, `destroyed=false`,
     pieces arc away, per-piece `large_firetrail`) has **no trigger until weapons (M3)** can down a
     plane mid-flight — a building crash is `_dirt`, not air. Data: `extracted/C1/cam_anim/`
     (`player-player_crash_*.json` + the effect defs); full decode in `docs/HISTORY.md` (2026-07-23).
- **Improve on the original crash — the bespoke "breaking apart" (branch `bespoke-crash-animation`).**
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

- **World-effects runtime follow-ups (from M3 D32, 2026-07-24).** The world-effects runtime
  (`PlaneViewer.BuildWorldEffectsRuntime`) renders the impact/destruction **puffers**; three threads
  it left open:
  1. **Gun-impact `gunhit` smoke.** Not wired — `ProjectilePool.EffectSink` is gated `!weapon.IsGun`.
     The plan assumed the shared per-round emitter would *collapse onto one puff*; the real blocker is
     that `gunhit`'s `blacksmokepuffer` has **no `ACTIVE_STATE 0` stop**, so one shared emitter
     (`_puffers` key `(name, host)`, one `gunhit` template root) emits **forever** at the last hit.
     A guns pass needs a per-hit emitter that copies (not relocates) the template and self-expires —
     either a pool of `gunhit` roots or a burst-mode puffer with a bounded life.
  2. **The template MESH half.** The effects stage is hidden, so only the puffers render; the
     `gunhit` debris bits (`bit1`/`bit2`/`chunk` + their `OBJECT_MOTION`), the `he_ring` ground
     shockwave, and the `huge_splash_model`/`zep_ng_dstry1.flt` models do **not** show. Rendering them
     needs the mesh visible-at-the-site without flashing at the stage origin (per-def visibility, or a
     copied instance per call rather than a hidden shared template).
  3. **`biggun_flying_parts`** (the zeppelin-destruction container) builds no puffer in `--effects-test`
     — its `spurtpuffer1..5` ride `fly_trail1..5` sub-trail roots that are gamez nodes but **not in
     `EffectStageRoots`**. Stage the `fly_trail*`/`*_trails` sub-templates (and check whether it needs
     the `OPERAND_NODE` call path, not a bare point) if zeppelin kills are ever wanted to smoke.
  ⚠ **Traps.** The `--effects-test` census is only reproducible **seeded** — several gun `*_gunhit`
  variants gate their puffer behind `RANDOM_WEIGHT`, so an unseeded run reports a different set each
  time (a manufactured answer). And these effects **share puffer names** (`trailpuffer2` across
  `small_fireball`/`great_balls_of_fire`/`large_black_smokeball`): a per-name build count is only clean
  if the previous effect is fully `StopAll`'d first, or the shared `(name, host)` key masks the build.

- **Rocket firing order — drain the selected hardpoint, not round-robin (M3 polish, user 2026-07-24) — the round-robin fix is on branch `fix/rocket-drain-pylon`, pending merge + playtest.**
  The original fires **only the selected/current hardpoint, draining it fully before advancing** to the
  next. Current code (`FlightController.NextArmedHardpoint`, B17) instead spreads pulls **round-robin**
  across the pylons. On a stock Bloodhawk (3 pylons × 3 HE) the depletion order differs:
  - **Original:** pylon1 empties on the **3rd** pull, pylon2 on the **6th**, pylon3 on the **9th**.
  - **Current:** pylon1 on the **7th**, pylon2 on the **8th**, pylon3 on the **9th** (only the last pull
    agrees). Confirmed by the D44 hide-breadcrumb (`pylon ordnance: pylonN dry`).

  The fix (on `fix/rocket-drain-pylon`) is the localized change to `NextArmedHardpoint`: keep the
  cursor on the just-fired pylon (`_nextPylon = idx`) so it keeps firing until dry, then the scan
  advances — rather than advancing every pull. **D44's pylon-ordnance visual needs no rework** — it
  already hides each pylon's mounted rocket the instant *that pylon* hits zero, so the wing simply
  empties one rocket at a time in the correct order once the firing order is fixed. Total capacity and
  the one-rocket-per-pull cadence are unchanged.

  **Still open — the H-selector design question (the "hardpoints won't cycle" finding).** H / D-pad
  Right *is* wired (`RocketSelectPressed` → `CycleWeaponSelectors`) but gated `_ordnanceTypes.Length >
  1`, and every stock loadout carries one hardpoint type (`wep_06`), so there is nothing to cycle —
  working as designed, not a bug. Settle from the original whether the player selects an individual
  **hardpoint** or the game just drains them in pylon order; meaningful mixed-ordnance cycling arrives
  with the M4 configurator (mixed loadouts). See "Milestone 3 Polishing".

- **`wait_for_completion` is decoded and read by nothing** (found 2026-07-22 while fixing the
  sequence scheduler, polish-4 item 1; deliberately not folded into that fix — different
  mechanism). It appears on **56,750 `CallAnimation` events** across the install and on **no other
  event kind**. Value distribution: `null` ×53,019, `0` ×3,639, then `1` ×32, `2` ×19, `5` ×16,
  `3` ×9, `4` ×8, `6` ×8. **That shape says index, not boolean** — the same family as the
  `wait_for_raw` connector slots seen on `player_plane_destruct`'s per-piece
  `CallAnimation large_firetrail WithNode pieceN` (`wait_for_raw` 0/1/2 = the three `local_lft`
  `CallObjectConnector` refs). Working hypothesis to test first: it selects which of the def's
  `anim_refs` connectors the call blocks on. **Nothing in `CSVM/src` references the field at all.**
  ⚠ **This is the `spline_interp` shape** — a field the extraction decodes faithfully and the
  runtime silently ignores — and that one was inert-looking right up until it was traced to a 1e29
  transform blowup. Not evidence this one is harmful, but it is a reason to decode it rather than
  leave it. Start by dumping the defs where the value is non-null and non-zero (92 events total,
  small enough to read by hand) and checking whether their `anim_refs` arrays are long enough to
  index. Note the scheduler now honours event offsets correctly, so any *timing* symptom this field
  causes should be cleaner to see than it was before 2026-07-22.

- **Better mission states.** There is still a lot of difference between our maps and the
  original's. May need a pipeline to diff them, or to crack the mission loading states properly.
  (`MissionSetup`'s interp boot script, landed 2026-07-22, closed the largest single gap and
  incidentally fixed the C3 coast z-fight — but it is a boot script, not the full state model.)

- **M3-deferred gun mechanics — firing heat and cannon jam** (scoped out of
  `docs/plans/PLAN-M3-weapons.md` 2026-07-22, decision 4: friction with no combat pressure to justify
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
    (see `docs/plans/PLAN-M3-weapons.md` C23 for the dominance argument that settles it).

- **Residuals from polish-3 item 5 (2026-07-22) — all small, all deliberate.** *(Three of the
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

- **Skybox colour grading.** No tint, grade or tonemap is applied to the skydome anywhere —
  `WorldBuilder.BuildHorizon` only disables shadows, billboards the moon and disables light
  range-fade, and the `WorldEnvironment` sets background/ambient only. The dome does get the shared
  per-mission scalar dim `csky_world_light`, which is brightness, not grading. The decoded
  per-mission cloud tints are parsed and deliberately parked (`Weather.cs`, "unused this
  milestone") — they are the obvious input if this is picked up.
- **Paint scheme follow-ups** (the core landed 2026-07-20 — see `docs/formats/paint.md`
  "Known divergences"; these are the leftovers):
  - **The paint UI's "Shade" column** is unmodelled — three Colour *and* three Shade
    dropdowns exist in the UI, only three colours in the data. We ramp black → colour.
  - **A livery picker in the launchscreen.** Selection is CLI-only (`--paint=`); flight
    randomizes per player. Decide from playtest whether the menu should offer it.
  - **AI/ace liveries.** `ia.json` `ace_*` and the AI defs' own `paint_*` are parsed into the
    catalog but nothing flies them — there are no AI aircraft yet.
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

- **C1's fuel depot: what did you actually see, and in which mission?** **⚠ NEEDS A FURTHER
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

- **Which weather/sky zone do C1–C4 actually use?** **C5 is answered — `zone1`** (user A/B
  2026-07-22; landed as polish-3 item 2, see `docs/formats/weather.md` and `Weather.ResolveZone`).
  **Still open for C1–C4**, all of which define `zone2` and resolve to themselves, so they render a
  plausible answer either way and this is a fidelity question rather than a bug. **C1 is the one
  worth doing first:** it is the only chapter whose own scripts disagree (`load.gw` →
  `zone2_cloud_floor`, `tex_fx.gw` → `h_zone1scroll`), and its two zones are genuinely different
  skies (zone2 = moon/stars night, zone1 = day haze).
- **Fine-tune fog and environment** — method: record video from a spawn point flying straight for a
  fixed number of seconds, in both engines, and compare.

- **Patrol boat: which HP governs?** (from `docs/plans/PLAN-M3-weapons.md` C23, 2026-07-22.) The boat
  is described by two systems that **agree on the damage-stage fractions and disagree on total
  HP by exactly 2×**: the `patrolboat` vehicle def says `health 40` with stages at 0.60/0.30
  firing `ptboat_50damage`/`ptboat_75damage`, while the `C1/patrol_boat` anim def says
  `HEALTH 20` with stages at `ANIM_HEALTH` 12/6 (also 0.60/0.30) firing the generic
  `sputter_black_smoke_obj`/`sputter_fire_smoke_obj`. Likely reading: the vehicle def governs
  the boat as an **AI combatant**, the anim def as **placed scenery** — so M3 (scenery only)
  wants 20. **Settle by shooting one in the original with a known weapon and counting hits.**
  Also check whether `t_truck` (`armor 0 / health 40`, no injure_anims), `fueltruck` and
  `armytruck_destruct` show the same duplication.

- **Rocket firing cooldown (LANDED B17, 2026-07-24 — now a playtest A/B).** Every rocket entry
  has `FIRE_RATE 1.0` (vs 8.0–10.5 for guns), i.e. one launch per second. The user confirmed one
  trigger pull = one rocket from one hardpoint but was **not sure whether a cooldown exists**, so
  B17 uses 1.0 as the data's answer rather than an observed fact (`FlightController.UpdateRockets`).
  A/B the 1 s gate against the original.
- **Rocket fire binding (design choice, LANDED B17).** Rockets fire on **F** / gamepad **A** (guns
  are Space / pad-B), one per pull. Pad-A doubles as respawn but only from the crashed / run-complete
  screens, which the live-flight firing path never shares — so no collision. If the mapping feels
  wrong in the cockpit it is a one-line change in `FlightController.RocketFirePressed`.

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
scripted screenshot. **Consolidated actionable index: [`playtest.md`](playtest.md).**

- **Rocket flyout speed** — the user reports rockets feel too fast. A `Config` knob
  `weapons.rocketSpeedScale` was added (default **1.0 = data speed, byte-identical**); it scales a
  rocket's flyout velocity **and** acceleration together, so the round still despawns at its `Range`,
  just slower. Set a smaller value (try `0.7`) in `config.json` and A/B against the original. Hook on
  branch `fix/rocket-speed-tune-hook` (see "Milestone 3 Polishing").
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
- **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)** — several playtest-gated TUNEs,
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
- **Audio (Run-2 item 11)** — `WhineMixGain` 0.12; A/B a dive against the original.
- **Knife-edge nose sag (polish-4 item 6, landed 2026-07-23)** — `KnifeNoseSag` **0.07 rad (≈4°)**,
  the bound the nose settles to at full knife-edge, and `KnifeNoseRate` **0.2 rad/s**, how fast it
  gets there. Both `FlightModel.cs`. Presence and direction are proven by scripted test; **magnitude
  is not and cannot be** — it needs the original at the controls. Measured at the landed values:
  nose −0° → **−4°**, settled path −6° → **−10°**, sink 11.8 → **19.4 m/s**, 398 → **634 m** lost in
  35 s. Three things to judge in the cockpit: (1) does a −4° nose / −10° path / ~19 m/s sink feel
  like the original's knife-edge; (2) **steep wings-level climbs are also affected** — a full-pull
  zoom loses ~11° of apex — because `knife = 1 − |up·Y|` grows with pure pitch at zero bank, so if
  that nose-heaviness feels wrong the fix is **gating on actual bank instead of `1−wingVert`**, a
  code change rather than a retune; (3) stall-into-knife-edge recovery should not feel "doubled".
- **Stunt mode** — `DzRadius` **15 m — user-tuned by hand 2026-07-22, and this is the current
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
    each ring's own extent".
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

  Also: marker-HUD placement, font and distance units; scoreboard fonts and placement.
- **Splitscreen** — the `HudMetrics` sqrt pane damping, `MixGain`, `SpawnAbreast`, join/lock
  feel, tag-gutter widths.
- **Aircraft brightness** — every aircraft got substantially brighter when the inverted-normal
  bug was fixed (2026-07-20); whether flight lighting now wants re-tuning against the reference
  videos is unassessed.

## Owed playtests (need hardware or a human at the controls)

> **The actionable, consolidated checklist is [`playtest.md`](playtest.md)** (root) — what to look
> for, the launch command, and what each blocks. This section keeps the deep evidence/traps; keep
> the two in step.


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
  1. **C1 free flight** — the waterfall, the train, and (since the loader-lifetime fix of
     2026-07-22) `snd_police` riding the police car along its route. The waterfall is at roughly
     (-7868, 0, -3449) with a 1500 m range; the train and the police car both move, so both should
     pan and fade as they run their loops.
  2. **C4** — three waterfalls.
  3. **C1/M04** (`--freecam --chapter=C1 --mission=M04`) is where a zeppelin engine actually plays.
     It was silenced by the degenerate-transform bug until 2026-07-22; since that fix
     `snd_zepengine`'s host reports a finite pose and **plays**, so a `sound: … silenced — its host
     node's world pose is degenerate` line there is now a **regression**, not the expected state.

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

## Cutscene player — the missing consumer (M04's zeppelin, `letterbox`, `CALLBACK`)

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
   or not at all (`docs/verification.md` §3).
4. **Verify by what disappears, not by what looks right** — `generic_intro` is shared across 12
   missions and may currently be driving things nobody has looked at.

### M04 is the ready-made first test case

Its data is fully decoded, so it is an end-to-end exercise for free. The symptom that exposed all
of this: the pirate zeppelin builds, renders complete and flies — it is simply **above the
clouds**, at y 1505→1546 while C1's opaque `cloudlayer` deck sits at y = 960 and the player spawns
at y ≈ 110. Freecam onto it with `--freecam --chapter=C1 --mission=M04
--campos=-5358,1505,-2200 --lookat=-5358,1505,-1810 --no-fog`.

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
all, so the regression is inert here by construction (`docs/verification.md` §3).

## The one-frame `CallSequence` dispatch lag

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
   (`docs/verification.md` §4). C1 legitimately reports 38 while 39 emitters exist.

**Open question this should answer:** does the original dispatch a called sequence in the same
tick? If yes, every `CallSequence` in the install is currently a frame late and the fix is a
fidelity improvement rather than a no-op. Nobody has checked; the measured behaviour-neutrality
above only says *our* observable output does not change.
