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

## 1 · Milestone 3 — weapons (new, ready to try)

In-flight weapon keys: **Space** (pad B) guns · **F** (pad A) rockets, one per pull ·
**G** (D-pad L) select gun group · **H** (D-pad R) select ordnance · R respawn.

- **Gun firing feel.** Hold **Space** and hose the world. *Look for:* does the rate feel right, do
  the tracers read clearly, is the muzzle flash the right size/brightness at the wing/nose guns?
  Fire the Bloodhawk (40+30), the Fury (70+30), the Balmoral (twin .50s).
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1` (add `--infinite-ammo` to not run dry).
  *Blocks:* sign-off on B16 (gun firing) + tuning of tracer/flash/rate.

- **Rocket firing feel + model look.** Press **F** for one HE rocket per pull (hold does not
  auto-repeat; a 1 s cooldown gates it). *Look for:* does one-per-pull feel right, does the 1 s
  cooldown feel too slow or fine; and — the owed **B14** close-up — watch a launched rocket leave the
  pylon: it now flies the real `he_rocket` MODEL body nose-forward (verified in-engine at
  `nose·velocity = 1.000`, but never caught crisp in a chase-cam screenshot), with a slim exhaust
  streak. Does the body read at speed / the right size? (Its `MODEL_ANIMATION` smoke trail is still
  pending, D-wave.) Stock Bloodhawk carries 9 (3 pylons × 3).
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1`.
  *Blocks:* sign-off on B17 + B14; the 1 s cooldown is the data's `FIRE_RATE`, not measured feel (TUNE).

- **Impacts.** Shoot the **ground** and the **sea**. *Look for:* a splash + water hit sound over
  water, a spark + ground hit sound over dirt; the right sound on each. (The exact named impact
  animations — `gunhit`/`splash1`/fireballs — are still stand-in sprites, D30/D32.)
  *Blocks:* D30/D32 effect wiring priorities.

- **Empty-clip.** Hold fire until a group runs dry (guns carry 2000–2800 rounds; rockets 9).
  *Look for:* the empty-clip cue sounds once, not repeatedly.
  *Blocks:* confirms the B16/B17 dry-warning path under real (long) fire.

- **Weapon selectors — one group at a time.** On a multi-group plane press **G** to switch gun
  group and watch the HUD ammo line bracket move; only the bracketed group fires. Press **H** to
  cycle ordnance (stock is one HE type, so this is a no-op today — just confirm it does not break
  rocket fire). Best on the 4-group planes:
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo` (Devastator) or
  `--plane=player_peacemaker`. *Look for:* switching feels right; the intended default group is
  sensible. *Blocks:* B18 sign-off; the G/H + D-pad-L/R bindings are a design choice (see below).

- **Pad bindings (design choices, easily changed).** Rockets on **pad A**, gun-select on **D-pad
  Left**, ordnance-select on **D-pad Right**. *Look for:* do these sit right in the hand; does pad-A
  ever feel like it should be respawn (it still respawns from the crashed/finished screens only)?
  Does the D-pad register on your controller (some pads report it as a hat)?
  *Blocks:* nothing hard — one-line rebinds in `FlightController` if any feels wrong.

- **A10 — muzzle placement per airframe.** Fire each of the 11 planes and confirm the flashes appear
  on the mounts the loadout names. *Look for especially:* Bloodhawk (40-cal **inner** wing, 30-cal
  **outer**) and Brigand (W1+W2 **share the outer** mount) against your own earlier observations;
  Devastator and Peacemaker against their distinctive two-axis / left-right layouts; Firebrand and
  Kestrel W3 (the binding rule's two soft spots).
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo --fire` (auto-holds guns).
  *Blocks:* **plan item A10** (verify the binding rule + flash appearance).

- **C23 — shooting a destructible in flight (pairs with C24).** Fly at an airport structure (water
  tower, AA gun, hangar) and pour gun fire into it, then loose a rocket. The damage MODEL is verified
  headless (HE 1-hit / AP 2-hit a HEALTH-60 tower; 40-cal 14 hits; stages fire at 60/30 % of HEALTH),
  but no one has watched it happen at the controls — the airport positions were not decoded into
  scripted aim points, and the *visible* death is C24's healthy→destroyed swap, so watch for: HP
  actually falling (smoke then fire appear as you chip it), and — once **C24** lands — the object
  swapping to its wreck + debris at zero. `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`.
  *Blocks:* end-to-end sign-off on C23 + C24 (weapon damage → destruction).

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

---

## 9 · Interactive polish never hand-tested

- **`--freecam` feel** — look sensitivity + speed curve (built entirely via scripted screenshots).
- **The `--viewer` labs** — damage (H), livery (L), mesh (M) — mouse-driven, no interactive playtest
  beyond scripted verification. `./RunGame.ps1 --viewer --plane=player_kestrel`.
