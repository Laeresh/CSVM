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

---

## 1 · Milestone 3 — weapons (first pass 2026-07-25 — re-tests owed after fixes)

In-flight weapon keys: **Space** (pad B) guns · **F** (pad A) rockets, one per pull ·
**G** (D-pad L) select gun group · **H** (D-pad R) select ordnance · R respawn.

First at-the-controls pass done **2026-07-25**. Items that **passed** are retired here (recorded in
`backlog.md` → "Playtest pass 2" → confirmed working). Items that produced findings point to their
pass-2 backlog number and are **re-scoped to re-test once the fix lands** — the diagnosis lives in
`backlog.md`; this stays the owed-list.

**Passed 2026-07-25 (retired):** destruction sound (D31, incl. secondary oil-tank explosions) · pad
bindings · gun rate/cadence/sound + in-flight muzzle alternation · rocket one-per-pull + 1 s cooldown
feel · weapon-selector feel + default group · E35 gauge readouts · E37 reticle (pipper trails the
nose) · C27 (crash into a building = plane crashes / building stands; fly through a filmset facade =
pass through unharmed).

**Re-test after the fix lands** (the fix is the numbered `backlog.md` "Playtest pass 2" item):

- **Gun visuals** — tracers should read as short yellow streaks (not long glowing lines); the muzzle
  flash smaller + a random angle each shot; brass should eject with a white puff (missing today).
  → backlog 1–3. `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo`
- **A10 muzzle placement** — looked right but the oversized flash masked it; re-confirm each airframe's
  mounts once the flash shrinks. → backlog 1.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo --fire`
- **Rocket smoke trail** — a launched rocket should trail a fat orange→grey smoke streak (today only a
  slim exhaust; the round reads "too fast" because of it). → backlog 5.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1`
- **Impacts over water / dirt (was D30 — FAILED).** Water shows **nothing** (the sea has no collider,
  rounds pass through); dirt shows one big spark instead of small tumbling debris. After the fix: small
  white splash sprites on the sea, small randomly-rotated debris on dirt. → backlog 7, 8.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1B` (over water) or `--chapter=C2`.
- **Destruction effects (was D32 — FAILED).** Building and dirt rocket impacts look identical (both a
  fireball puff); an oil-tank kill throws one puff that floats too high and lingers. After the fix:
  buildings → fireball, dirt → light flash; the oil-tank fire sits at the wreck. → backlog 6, 9, 10.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets`
- **Damage stages + debris in flight (C23/C24/C26 — FAILED).** Guns-only into a tower showed **no
  smoke→fire stages** (only the death blast), and wreck pieces fly on the wrong trajectory. After the
  fix: stages render as HP falls; debris arcs correctly. → backlog 11, 12.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`
- **Killed door (C25 — FAILED ❌).** Shooting `kkgate`'s propane tank leaves the door in place with its
  collider (original: door deactivates, pieces fly + fade, no collider). The `det==0` error in the same
  run is a **separate** bug — it prints after the sequence has already completed and reported its swap,
  so do not expect fixing one to fix the other. → backlog 13.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C2 --fire`
- **Empty-clip** — couldn't reach a dry gun group by hand (2000+ rounds); needs the low-ammo debug knob,
  and no rocket dry cue was heard. After: one empty cue per group, once. → backlog 16, 18.
- **Hardpoint selection** — H should select an individual pylon (each counts for itself), auto-advancing
  only when the selected one empties. → backlog 15. `./RunGame.ps1 --plane=player_bhawk --chapter=C1`
- **Ammo-gauge yellow tier** — the original is green→red only (no yellow); re-check after it's removed.
  → backlog 14. `./RunGame.ps1 --plane=player_warhawk --chapter=C1`
- **Weapon-switch sound (owed).** Does G / H play a select click like the original's ammo-selector UI?
  A/B to decide whether to add one — a one-line cue in `FlightController` if wanted.
- **E37 convergence (standing TUNE).** The pipper projects to 250 m (`GunConvergenceDist`); the trail
  felt right but may differ in a dogfight — A/B against the original. *Blocks:* retiring the TUNE.

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

---

## 3 · Flight feel & camera (TUNE — calibrate against the original)

- **Pitch authority — the big one.** The Run-2 calibration cut pitch authority ~2.7× (`PitchTune`
  0.75). *Look for:* does the aircraft still turn/loop like the original, or does it now feel sluggish
  in pitch? Also `YawTune` 1.32 / `RollTune` 2.12. *Blocks:* flight-feel sign-off.
- **Stall & knife-edge.** `StallNoseRate`, `KnifeAlignFloor`, `KnifeNoseSag` (~4°) / `KnifeNoseRate`,
  `ClimbGravityScale`, `LowSpeedDragBlend`. *Look for:* stall recovery, knife-edge sink, and whether
  steep wings-level zoom climbs feel nose-heavy (if so the fix is gating on real bank — a code change,
  not a retune). *Blocks:* flight-feel sign-off.
- **Dive terminal speed.** Ours runs to ~1.7×fd_speed; the original's near-vertical dive pins ~1.27×
  (~385 mph). *Look for:* top-end dive speed vs a reference dive. *Blocks:* overspeed drag/cap tuning
  (interacts with the whine/rattle curves).
- **Pitch rate vs speed.** The original visibly bled speed during a sustained full-pitch 360°; ours is
  constant-rate. *Look for:* does a hard sustained pull slow you down like the original? *Blocks:*
  flight-model fidelity.
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

- **Collision feel.** `CrashSpeed` 25, graze friction + attitude kick, `GrazeStopSpeed`, tree
  softness, behaviour against **building corners**. *Look for:* grazes vs outright crashes feel fair.
- **Visible damage.** Do the torn-panel flips (`pdpN`) and the low-HP smoke/fire trail look right in
  real flight (the thresholds need HP states normal play actually reaches). *Blocks:* damage-visual
  sign-off.
- **Data-driven crash.** `WreckMomentum` 0.4, the piece tumble-rate, the debris-arc scale, overall
  crash intensity (fireball + cluster + debris fire are additive — judge the whole), and the
  `snd_exp_ground_a` mix. *Blocks:* crash sign-off. A/B against branch `bespoke-crash-animation`.

---

## 7 · Stunt mode (TUNE)

- **Danger-zone radius.** `DzRadius` 15 m is your hand-tuned value, but one global constant does not
  fit — too tight at some zones, too loose at others (you can fly *around* the danger and still
  score). The route forward is a **per-zone radius/prism from `dzpathN`** (real gate geometry — see
  backlog). *Look for:* which zones feel wrong. *Blocks:* a per-zone stunt-radius implementation.
- **Marker HUD + scoreboard.** Placement, fonts, distance units.
  `./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`.

---

## 8 · Original-game fidelity questions (need the original open, not just the cockpit)

- **Patrol-boat HP — 20 or 40?** The `patrolboat` vehicle def says HP 40, the anim def says HP 20
  (both 60 %/30 % stages). **Settled for M3 (C23):** the boat is scenery, damaged through its anim
  def (HP 20); the AI-vehicle armour+health model (HP 40) is M4. Still a nice fidelity check —
  **shoot one in the original with a known weapon and count hits** to confirm 20, and check
  `t_truck`/`fueltruck`/`armytruck_destruct` for the same duplication. *Blocks:* nothing (M3);
  M4 AI-vehicle combat.
- **Map-edge continuation.** Ours mirrors the border tiles; the original may plain-repeat, and may
  extend more than one tile. *Look for:* an asymmetric border feature (settles mirror vs repeat) and
  how many tiles out the world continues. *Blocks:* `MapEdgeExtender` fidelity (one-line swap).
- **Compass north convention.** North = −Z is assumed. *Look for:* does the heading tape read correct
  cardinal directions vs the original? *Blocks:* compass sign-off (one-line flip if wrong).
- **Crossed `pdpN_h` numbering.** Does the *original* amputate the wrong wingtip on wing damage too
  (bloodhawk/firebrand/brigand data quirk)? *Blocks:* damage-visual fidelity confirmation.
- **C3 spiderweb — is it faded at start, and is it solid?** Our engine fades C3's `spiderweb` mesh to
  invisible at mission start (the `spiderweb_gone` `ON_STARTUP` opacity fade), and a merged fix
  (`fix/opacity-fade-collider`) now also drops its collider when it fades — so you no longer crash
  into an invisible wall. **But the premise is unconfirmed and you doubt it:** in the *original*, at
  C3 start, is the spiderweb **(a) visible**, **(b) faded/gone**, and **(c) solid** (does the plane
  hit it or pass through)? If the original shows it **visible and solid**, then *our fade is the bug*
  (we should not fade it) and the collider change is masking a deeper problem — flag that. *Where:*
  fly low near the web in C3, ours vs the original. `./RunGame.ps1 --chapter=C3 --plane=player_bhawk`.
  *Blocks:* confirming `fix/opacity-fade-collider` is the right fix vs. a "why do we fade it at all"
  question (see backlog "Milestone 3 Polishing").

---

## 9 · Inspect tools (PLAN-testing Wave D — the tools are yours to judge)

- **The zeppelin case — the acceptance test for the shared selection (D31).** The complaint this
  exists to kill: clicking the zeppelin selects one of its motors with no way up. *Where:*
  `./RunGame.ps1 --freecam --chapter=C1 --mission=M04 "--pos=-4848,200,-5165" "--direction=-1,0,0"`
  puts the moored zeppelin broadside in front of the camera. **Click one of the engine nacelles**
  along the hull, then **PgUp** repeatedly (**Home** jumps straight to the outermost rung, **End**
  back to the leaf). *Look for:* the yellow breadcrumb line reading the ladder leaf-first with the
  current rung bracketed; the wireframe box growing from the nacelle to the whole airship as you
  walk up; the box staying on the object rather than lagging or floating. The scripted run says the
  ladder is nine rungs — `g15 < l5 < healthy < lk_rightengine01 < lkgasbag01 < zfronthalf <
  rock_zeppelin < noserotate < hk_zep` — so **the question is whether nine rungs plus Home feels
  like "a way up", or whether it wants something smarter.** Also try a building, a truck and the
  moving train, and try clicking terrain (by design nothing is selected — say if that reads as
  broken rather than as a rule). In `--anim-lab` the camera should frame what you clicked and then
  re-aim, without re-framing, as you walk the ladder. *Blocks:* D31 sign-off, and the shape of
  D32–D35, which all act on this selection.

- **The node lab at the controls (D32).** Everything about this panel except its readouts is
  unverified: live keys and mouse are unscriptable here, so N, the expand arrows, the search field,
  the buttons and the two-way click sync ran only through `--debug-nodelab` and by construction.
  *Where:* the same zeppelin launch as above, plus `./RunGame.ps1 --freecam --chapter=C5` for the
  big-world case (8,897 named nodes, 557 directly under the world root). **Press N.** *Look for:*
  (a) **does the tree open where you are?** — click the zeppelin, the tree should scroll to that
  node; click a tree row, the world highlight should follow. (b) **Expanding.** A branch fills only
  when opened; a branch over 500 rows stops with a "… N more — use the search box" row. Does that
  read as a limit or as a bug? (c) **Search.** Type a fragment; results are a flat list,
  double-click frames the camera. Is filtering as you type fast enough in C5? (d) **Frame and
  Hide.** Frame should put the camera on the thing and orbit it; Hide should grey the row and add
  `(hidden)`. **Hide something an animation drives (a hangar door, the train) and watch it come
  back** — that is the data re-showing it and is correct; the panel's `visible=`/`in_tree=` line is
  how you should be able to tell. (e) **Destructibles.** Flip the switch: C1 should read
  `defs 132 · instances 267 · node groups 196 · 12 unresolved def(s)`, red ⚠ rows for the twelve,
  `2/2` root coverage and `9 ok` event coverage on the bound ones; expand one and click an instance
  to jump to it. (f) **Layout.** Checked at 1280×720 only — in `--anim-lab` the panel is squeezed
  between the breadcrumb and the timeline, and at other window sizes nothing has been looked at.
  *Blocks:* D32 sign-off; the panel is also the reach-around for anything the click pick refuses
  (terrain), so say if that path is discoverable.

---
