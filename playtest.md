# Playtest checklist

Everything that needs a human at the controls (or the original game open for A/B), consolidated.
Each item says **what to look for**, the **command** to get there (when it needs specific args), and
**what it blocks**. Deep evidence and traps live in [`backlog.md`](backlog.md) — this file is the
actionable index; the two are kept in step.

**How to launch.** `./RunGame.ps1` (no args) builds and opens the in-game launchscreen
(Mode → Chapter → Plane, keyboard or pad). Any args bypass the menu and drop you straight in, e.g.
`./RunGame.ps1 --plane=player_fury --chapter=C1`. Flight is the default. Full flag list:
[`docs/cli.md`](docs/cli.md).

**Plane model names** (for `--plane=`): `player_autogyro` Hoplite · `player_avenger` Hellhound ·
`player_balmoral` Balmoral · `player_bhawk` Bloodhawk · `player_brigand` Brigand ·
`player_pfighter` Devastator · `player_fbrand` Firebrand · `player_fury` Fury ·
`player_kestrel` Kestrel · `player_peacemaker` Peacemaker · `player_warhawk` Warhawk.

**Cross-checked against the original design documentation 2026-07-25.** A *pre-release* spec: it
settles **system shape**, never numbers or art direction (rebalanced before release — extracted data
or an `OriginalScreenshots/` capture wins wherever either exists). What it answered is tagged
**[spec]** and rewritten below — judge ours against the stated target rather than A/B-ing. **It is
silent on the rest, so do not re-run this cross-check:** all of §2 (the original's multiplayer was
networked, so there is no splitscreen reference at all); every *tuning* question in §3, §4 and §5 —
pitch, stall recovery, dive speed, camera, mix levels, weather — which it covers with qualitative
rules and no numbers; and in §8 the C3 spiderweb, patrol-boat hit points, map-edge continuation,
which world axis is north, and the crossed `pdpN_h` numbering.

---

## 1 · Milestone 3 — weapons (first pass 2026-07-25 — re-tests owed after fixes)

In-flight weapon keys: **Space** (pad B) guns · **F** (pad A) rockets, one per pull ·
**G** (D-pad L) select gun group · **H** (D-pad R) select ordnance · R respawn.

Each item below is a **re-test owed once its fix lands**; the numbered pointer is its
`backlog.md` "Playtest pass 2" entry, where the diagnosis lives. What already passed is recorded in
`docs/HISTORY.md`, not here.

- **Gun visuals — judge ours against the captures, don't A/B.**
  `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` + `…ejection2.png` settle it.
  *Look for:* tracers as **short yellow dashes**, not long glowing lines; a muzzle flash of yellow
  core + orange flame, elongated forward, **at the wing gun mount**, one wing at a time; each
  ejection one small brass casing plus a cluster of white smoke puffs that **persist and drift aft**
  (in shot 2 a cluster has fallen well back and below while a fresh casing is still leaving the wing
  — a trailing emitter, not a burst parented to the muzzle). No calibre gate: the Bloodhawk ejects on
  a 40/30-cal **wing** fit (`CSVM/data/stock_loadouts.json`), so the spec's 50/70-cal underbelly
  claim is rejected. → backlog 1–3. `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo`
- **A10 muzzle placement** — looked right but the oversized flash masked it; re-confirm each airframe's
  mounts once the flash shrinks. → backlog 1.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo --fire`
- **Rocket smoke trail [spec].** The trail is **per rocket type**, not one generic streak — HE
  intermittent white puffs, flak continuous black, incendiary continuous red-hued, sonic a sine wave
  — and our extraction ships those as distinct emitters, so the fix is binding the per-type trail,
  not thickening today's one slim exhaust. *Look for:* each type trailing its own. → backlog 5.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1`
- **Impacts over water / dirt.** *Look for:* small white splash sprites on the sea, and small
  randomly-rotated debris on dirt rather than one big spark. → backlog 7, 8.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1B` (over water) or `--chapter=C2`.
- **Destruction effects.** *Look for:* buildings giving a fireball and dirt a light flash — the two
  telling apart — and an oil-tank fire sitting at the wreck instead of floating. → backlog 6, 9, 10.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets`
- **Damage stages + debris in flight.** *Look for:* smoke then fire rendering as HP falls under
  guns alone, not just the death blast, and wreck pieces arcing correctly. → backlog 11, 12.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`
- **Killed door.** *Look for:* shooting `kkgate`'s propane tank deactivating the door — pieces fly
  and fade, collider gone. The `det==0` error in the same run is a separate bug; do not read one as
  the other. → backlog 13.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C2 --fire`
- **Empty-clip** — couldn't reach a dry gun group by hand (2000+ rounds); needs the low-ammo debug knob,
  and no rocket dry cue was heard. After: one empty cue per group, once. → backlog 16, 18.
- **Hardpoint selection** — H should select an individual pylon (each counts for itself), auto-advancing
  only when the selected one empties. → backlog 15. `./RunGame.ps1 --plane=player_bhawk --chapter=C1`
- **Ammo-gauge yellow tier** — the original is green→red only (no yellow); re-check after it's removed.
  → backlog 14. **[spec]** yellow's intended axis was gun *heat* / jam risk (white → yellow → red,
  black = empty), not ammo left — so it returns with a different meaning if jamming is ever
  implemented. Removal stands. `./RunGame.ps1 --plane=player_warhawk --chapter=C1`
- **Weapon-switch sound (owed).** Does G / H play a select click like the original's ammo-selector UI?
  A/B to decide whether to add one — a one-line cue in `FlightController` if wanted.
- **E37 pipper (standing TUNE) [spec].** There is **no gun convergence to validate**: the fixed
  reticle is airframe-locked with every weapon aimed at that centre, and the floating one exists only
  because inertial forces make shots lag through a turn — velocity inheritance, already modelled.
  `GunConvergenceDist` 250 m is purely the distance the pipper is *drawn* at. *Look for:* it sits
  where the rounds go in a hard turn. *Blocks:* retiring the TUNE.

---

## 2 · Milestone 2.5 sign-off (needs two controllers / hardware this machine lacks)

- **Splitscreen join + race.** `docs/plans/PLAN-M2.5-prototype.md` items 6–7. *Look for:* join /
  un-join, the lock race, each pad flying only its own plane, Esc from splitscreen back to the menu,
  the results board fonts/placement, rematch feel. Open **design** question: should a finished pilot
  keep flying rather than freeze at the finish?
  `./RunGame.ps1 --stunt --players=2 --chapter=C4 --plane=player_fury,player_bhawk`.
  *Blocks:* calling **Milestone 2.5 done**.

- **Splitscreen ambient audio mix.** With no `AudioListener3D`, each pane's camera is a listener, so
  2P/4P may mix every 3D emitter 2–4×. *Look for:* if ambient audio sounds loud or doubled in
  splitscreen but fine solo, that is the cause — the fix is an explicit listener, not a gain tweak.
  *Blocks:* Milestone 2.5 audio sign-off (needs the A/B, hence two controllers).

- **Pad-read-on-focus gate (confirm or veto).** A landed change stops pads being read while the
  window is unfocused. *Look for:* does a pad still work as you expect; if you rely on background pad
  input, this is the toggle to veto (it shipped as its own commit and reverts cleanly).
  *Blocks:* nothing — a standing user-vetoable decision.

- **Menu → flight re-entry, after the args refactor.** The launchscreen's pick now builds a
  `SessionSpec` instead of writing nine fields, and this is the one path no automated instrument
  reaches: it needs a keypress, so neither the goldens nor the resolution baseline cover it.
  *Look for:* launch bare, pick Free Flight → a chapter → a plane and fly; then Esc back and launch
  a **different** chapter and plane, and a **Stunt** run after a Free Flight one — the second launch
  must take the new chapter/plane and must not inherit the first run's spawn scenario.
  `./RunGame.ps1` (no args). *Blocks:* B6 (`SessionSpec.FromMenu`), which changes this path again.

---

## 3 · Flight feel & camera (TUNE — calibrate against the original)

- **⚠ Before re-adding any `CSVM/config.json`, know that it silently overrules everything in §3.**
  Interactive runs honour that git-ignored file while `--det` runs drop it, so an override there
  makes a playtest measure a constant the tests never see. It was deleted when the calibration
  landed — it had been pinning `thrustConst` to the pre-calibration 40 and `pitchTune` to 1.5,
  double the calibrated 0.75. `./RunGame.ps1 --dump-config` writes a fresh full template if you
  want one back. *Blocks:* nothing now — a standing warning.
- **Thrust is 4.6× stronger than it was — fly it.** Not a TUNE: the original's own acceleration was
  measured off cockpit-gauge video (150 → 290 mph in 3.76 s) and `ThrustConst` now reproduces it, and
  the terminal dive agrees to 0.3% on the same constant. *Look for:* does the aircraft accelerate and
  dive like the original now — and does anything *else* break at the higher speeds it now reaches
  routinely (collision margins at low level, the overspeed whine and rattle curves, how far a stunt
  zone overshoots, whether the chase camera keeps up). Top speed is unchanged by construction.
  *Blocks:* flight-feel sign-off. *Answered, do not re-litigate:* **pitch authority is not sluggish**
  (sustained rate measured 33 °/s, ours 33.5) and the original's pitch rate does **not** fall off
  with speed — `PitchTune` 0.75 / `YawTune` 1.32 / `RollTune` 2.12 are all confirmed within a few
  percent, and `--run-tests=flight-envelope` fails if they move.
- **Stall & knife-edge.** `StallNoseRate`, `KnifeAlignFloor`, `KnifeNoseSag` (~4°) / `KnifeNoseRate`,
  `ClimbGravityScale`, `LowSpeedDragBlend`. *Look for:* stall recovery, knife-edge sink, and whether
  steep wings-level zoom climbs feel nose-heavy (if so the fix is gating on real bank — a code change,
  not a retune). *Blocks:* flight-feel sign-off.
- **Stall warning should be graded [spec].** The airspeed dial's stall blink should *rise in
  intensity* as the stall approaches; ours is binary — it starts only once `Stalled` is true, at a
  fixed phase. *Look for:* does a graded ramp read as useful warning or as noise. *Blocks:* stall-cue
  fidelity.
- **A hard pull should cost you speed, and ours does not.** Measured: from 300 mph the original's
  full pull bottoms at **104 mph**, ours arrives at the apex still doing **266 mph** — we model no
  induced drag (`backlog.md`, "Flight-model gaps the video calibration measured"). *Look for:* how
  badly this reads in normal flying, i.e. whether an energy-free turn makes combat and stunt runs
  feel wrong enough to schedule the fix. *Blocks:* nothing yet — it is a scoping judgement, not a
  TUNE. Dive terminal speed is **settled and needs no A/B**: it is emergent now and lands within
  0.3% of the original's measured 355 mph.
- **Chase camera.** `CamRotSmooth` 7 /s — the roll-follow lag. *Look for:* fast rolls read dynamic,
  not glued or lagging. *Blocks:* camera sign-off.
- **Control surfaces.** Deflection angles + slew rate. *Look for:* ailerons/elevators/rudder track the
  stick believably. *Blocks:* control-surface sign-off.
- **Numpad camera views — the whole held-key half is unverified here.** Hold numpad 1/2/3/4/6/7/8/9
  in flight (NumLock on): each snaps the camera around the plane, releasing returns to the chase view.
  *Look for:* (a) does the key **layout** match the original — 2 belly, 4/6 flanks, 8 head-on, the
  diagonals in between; (b) is the **distance** right (ours is the chase camera's own 16.62 m) and the
  **elevation** of 1/3 and 7/9 right (ours is 45°); (c) does the original **snap instantly** or ease,
  and does releasing ease back (ours snaps both ways); (d) does releasing a key while holding another
  behave sanely; (e) did the original bind any of this to the **gamepad**, and does **5** do anything?
  *Command:* `./RunGame.ps1 --plane=player_bhawk --chapter=C1`. *Blocks:* camera sign-off; anything
  off goes to the `backlog.md` TUNE entry. Only the scripted `--view=` twin is machine-verified —
  live keypresses are not scriptable here, so the held-key path is correct by construction only.

---

## 4 · Audio (TUNE — listen)

- **Ambient world sounds (never listened to).** `SOUND_NODE` emitters, mix + `RANGE` curve.
  *Where:* **C1 free flight** — the waterfall (~ -7868,0,-3449, 1500 m range), the moving train, and
  `snd_police` on the police car (both move, so should pan/fade). **C4** — three waterfalls.
  **C1/M04** — `snd_zepengine` should now play (a "host pose degenerate" line there is a regression).
  `./RunGame.ps1 --freecam --chapter=C1` · `--chapter=C4` · `--freecam --chapter=C1 --mission=M04`.
  `--debug-anim` prints each emitter's host/distance/range/playing once a second.
  *Blocks:* WorldSounds mix sign-off.
- **Low-altitude warning should beep [spec].** It flashes **and beeps** in the original; ours only
  flashes (`GaugeCluster` `lowalt_on`, below 50 m AGL). *Look for:* once a cue is picked from the
  extracted sounds, does it warn or just nag during low-level flying. *Blocks:* nothing — a missing cue.
- **Overspeed whine.** `WhineMixGain` 0.12 — A/B a dive against the original. *Blocks:* audio sign-off.
- **Engine pitch in dives.** The original's engine drops ~12 % through a dive and overshoots ~1.05 at
  pull-out; ours (throttle-only pitch) cannot. *Look for:* engine note through a full-throttle dive.
  *Blocks:* engine-audio fidelity (needs a controlled dive recording).

---

## 5 · Weather & visuals (TUNE)

- **Fog feel + `fogRangeFactor` 2.** The halving predates the sRGB fog-colour fix — re-A/B in game.
  `--sky-zone=` gives deterministic weather shots in `--chapter` mode. *Blocks:* weather sign-off.
- **Whiteout colour** (near-white) and the cloud-band pass-through. *Look for:* matches the original's
  in-cloud whiteout. *Blocks:* weather sign-off.
- **Cloud puffs / cloud deck.** Puff opacity + density; the deck reads ~40 units lighter than the
  original. *Blocks:* cloud sign-off.
- **Wing-light `FlashDuration`** (0.08 s blink). *Look for:* the wingtip flare blink cadence.
- **Aircraft brightness.** Every plane got much brighter after the inverted-normal fix — is flight
  lighting now over-bright vs the reference videos? *Blocks:* lighting sign-off.
- **`SunIncidence` 0.46** — rests on one overcast reference; a night + bright-day shot would refine it.

---

## 6 · Damage & collision (TUNE — needs states normal play reaches)

- **Collision feel [spec].** `CrashSpeed` 25, graze friction + attitude kick, `GrazeStopSpeed`, tree
  softness, behaviour against **building corners**. *Written target, so "fair" is judgeable:* a plane
  bounced off a canyon wall may survive it, and one flown through a billboard should destroy the
  billboard and come away barely scratched (C27 confirmed the pass-through half; whether the facade
  is *destroyed* is not). Damage should scale with weight × relative speed × angle of attack and
  spread over the struck zone **and its neighbours** — a clipped wing damages wing, nose and tail;
  ours is single-zone with neither term. *Look for:* grazes vs crashes feel fair against that.
- **Visible damage.** Do the torn-panel flips (`pdpN`) and the low-HP smoke/fire trail look right in
  real flight (the thresholds need HP states normal play actually reaches). *Blocks:* damage-visual
  sign-off. **[spec]** the gauge ramps Blue (100 %) → Green → Yellow → Red above 20 %, and that 20 %
  matches our shipped `*_damage_red` exactly (0.20 on all 44 zone entries) — so only the blue
  full-health end and the step order need an eyeball.
- **Data-driven crash.** `WreckMomentum` 0.4, the piece tumble-rate, the debris-arc scale, overall
  crash intensity (fireball + cluster + debris fire are additive — judge the whole), and the
  `snd_exp_ground_a` mix. *Blocks:* crash sign-off. A/B against branch `bespoke-crash-animation`.

---

## 7 · Stunt mode (TUNE)

- **Danger-zone gates — post-fix verification, not an investigation [spec].** The mechanism is
  settled: each zone has an **entry volume and an exit volume, both of which must be crossed** — two
  volumes exist precisely so clipping one cannot score, which is the "fly *around* the danger and
  still score" failure of the single 15 m `DzRadius` sphere, and it explains the two gate polygons
  each `dzpathN` mesh carries besides its route polyline. *After the ordered crossing lands, look
  for:* gates triggering where the danger is, no score on a clean miss, no zone that cannot be gated
  fairly. *Blocks:* signing off the gate implementation (see backlog).
- **Marker HUD + scoreboard.** Placement, fonts, distance units.
  `./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`.

---

## 8 · Original-game fidelity questions (need the original open, not just the cockpit)

- **Patrol-boat HP — 20 or 40?** The `patrolboat` vehicle def says HP 40, the anim def says HP 20
  (both 60 %/30 % stages); M3 settled on the anim def's 20 and left the HP-40 armour model to M4
  (backlog). *Look for:* **shoot one in the original with a known weapon and count hits**, and check
  `t_truck`/`fueltruck`/`armytruck_destruct` for the same duplication. *Blocks:* M4 AI-vehicle combat.
- **Map-edge continuation.** Ours mirrors the border tiles; the original may plain-repeat, and may
  extend more than one tile. *Look for:* an asymmetric border feature (settles mirror vs repeat) and
  how many tiles out the world continues. *Blocks:* `MapEdgeExtender` fidelity (one-line swap).
- **Compass north convention.** North = −Z is assumed. *Look for:* does the heading tape read correct
  cardinal directions vs the original? (The spec fixes cardinal *letters* over degrees, which we
  already do, but names no world axis.) *Blocks:* compass sign-off (one-line flip if wrong).
- **Crossed `pdpN_h` numbering.** Does the *original* amputate the wrong wingtip on wing damage too
  (bloodhawk/firebrand/brigand data quirk)? *Blocks:* damage-visual fidelity confirmation.
- **C3 spiderweb — is it faded at start, and is it solid?** Our engine fades C3's `spiderweb` mesh to
  invisible at mission start (the `spiderweb_gone` `ON_STARTUP` opacity fade), and a merged fix
  (`fix/opacity-fade-collider`) now also drops its collider when it fades — so you no longer crash
  into an invisible wall. **But the premise is unconfirmed and you doubt it**, and the spec cannot
  settle it — its Hawaii mission-visuals list names fog over the ruins, the primitive-bridge
  collapse, waterfalls, tunnel torches and seagulls, and no web at all. In the *original*, at C3
  start, is the spiderweb **(a) visible**, **(b) faded/gone**, **(c) solid** (does the plane hit it
  or pass through)? If the original shows it **visible and solid**, then *our fade is the bug*
  (we should not fade it) and the collider change is masking a deeper problem — flag that. *Where:*
  fly low near the web in C3, ours vs the original. `./RunGame.ps1 --chapter=C3 --plane=player_bhawk`.
  *Blocks:* confirming `fix/opacity-fade-collider` is the right fix vs. a "why do we fade it at all"
  question (see backlog "Milestone 3 Polishing").

---
## 9 · Inspect-tool follow-ups (re-tests owed once each fix lands)

The Wave D tools themselves passed and are retired; what remains is the re-test each follow-up
will need. Diagnosis and traps live in [`backlog.md`](backlog.md)'s "Surfaces, colliders and
inspect tools" section.

- **Impact sound and effect on a mis-tagged surface.** The surface classifier tags some buildings
  `water` and some water `buildings`, and the same tag picks the impact sound and effect — so a
  building can answer a hit with a splash. **This is the ear's job: the wireframe colour is only
  the symptom, the sound is the thing to judge.** *Look for:* gunfire into a C2 building giving a
  ricochet and debris, not a splash; gunfire into water giving the splash; no building that sounds
  wet. *Where:* `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire`, and press **C** with
  `--collision` to see which surfaces the engine believes are which.
  *Blocks:* closing the surface-classification item.

- **Collider wireframes line up with their meshes.** They currently sit offset on one shared axis
  while the colliders themselves are correct. *Look for:* the wireframe hugging the geometry it
  belongs to, on world nodes, clutter and the plane's own boxes alike — those are three different
  code paths and the offset may not be in all of them. *Where:*
  `./RunGame.ps1 --freecam --chapter=C2 --collision=show`.

- **Mesh-lab lighting on the player plane.** The plane cannot currently be selected in
  `--anim-lab`, so the light-steering controls went unexercised on it. *Look for:* selecting the
  plane, **M**, and the light sliders moving the shading on the aircraft rather than on the world.
  *Where:* `./RunGame.ps1 --anim-lab --chapter=C1 --plane=player_bhawk`.

- **The node lab's hide affordance reads from the tree.** Hiding works and the button text flips,
  but the row itself does not change. *Look for:* a hidden subtree being obvious in the tree
  without clicking it, and a node an animation re-shows going back to looking visible on its own.
  *Where:* `./RunGame.ps1 --freecam --chapter=C1`, **N**.

- **The world damage panel's size.** Currently larger than its content, with a gap between the
  no-controls notice and the debris line. *Look for:* it reading as one compact block.
  *Where:* `./RunGame.ps1 --freecam --chapter=C1`, click a destructible, **H**.

- **Destruction stages visible in the world, not just the panel.** A destroyed building burns, but
  the water tower shows no smoke or fire — its stage puffers are outside the world-effects
  runtime's fixed name set, so they start, log, and draw nothing. *Look for:* smoke at the damaged
  stage and fire at the destroyed one, on the object, for every destructible you can kill.
  *Where:* `./RunGame.ps1 --freecam --chapter=C1`, **H**, slide HP down and kill.
  *Blocks:* nothing here — it is an animation/visuals item, not a damage-lab one.
