# Playtest checklist

Everything that needs a human at the controls (or the original game open for A/B), consolidated.
**This file holds only what is actionable *today*.** Anything whose test is blocked on an unlanded
fix lives on its `backlog.md` entry as a `*Playtest after fix:*` line instead — so an empty section
here means the work is queued, not forgotten. Deep evidence and traps live in
[`backlog.md`](backlog.md); the two are kept in step.

**Every item carries a stable ID** — `CAP-nn` for an owed capture, `PT-nn` for something to fly.
Cite them from `backlog.md` and in conversation the way `BL-nnn` is cited. IDs are permanent: when
an item closes its ID retires with it and is never reused, so numbering gaps are expected.
Retired IDs disappear from this file, so never mint a new ID by scanning the entries below — take
it from this counter and bump it here: **next free IDs `CAP-25` and `PT-31`.** (`CAP-17` and
`CAP-24` retired 2026-08-04 — IDs are never reused.)

**Captures staged for an item live in `playtest/<ID>/`** — git-ignored (they are renders of the
player's own game files) and, unlike `.scratch/`, **not swept by `CleanScratch.ps1`**, so they
survive until the item that owns them closes. Delete the folder with the item.

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
or an `OriginalScreenshots/` capture wins wherever either exists). **It is silent on the rest, so do
not re-run this cross-check:** all of the splitscreen work (the original's multiplayer was
networked, so there is no splitscreen reference at all); every *tuning* question — pitch, stall
recovery, dive speed, camera, mix levels, weather — which it covers with qualitative rules and no
numbers; and the C3 spiderweb, patrol-boat hit points, map-edge continuation, which world axis is
north, and the crossed `pdpN_h` numbering.

⚠ **The spec's HUD and damage material is unreliable as a class — three of its claims were
overturned by direct observation in one sitting (2026-07-30).** It said the low-altitude warning
beeps (it does not), that the ammo gauge's yellow tier meant gun heat/jam rather than ammo (it is a
real ammo tier, on gun belts only), and that the damage gauge ramps from a blue full-health state
(there is no blue tier and never was). Treat any remaining `[spec]`-sourced HUD claim as a
hypothesis to check, not a target to build against.

---

## 0 · Owed captures (`CAP-nn`)

**One sit-down list.** These are recordings and screenshots of *the original game*, not our build.
Most of the items below and a large part of `backlog.md` are blocked on one of these, so filming
them in a batch unblocks far more than doing them one at a time.

⚠ **Validity gate for every cockpit-gauge clip: auto head turn must be OFF, or the clip is
unusable.** This already cost two takes. The capture spec and the clip-validity rules are in
`analysis/video-flight-calibration/FINDINGS.md`.

### Flight model — cockpit gauges in frame, head turn off

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-02` | Low pass along a canyon wall | A close pass down a canyon face; the only possible source of ground-blow magnitude | `BL-095` |
| `CAP-20` | Throttle equilibria + a shallow held climb | Two level runs held to equilibrium at **1/4** and **1/2** throttle (the thrust-vs-throttle curve), then a **shallow, steady climb** at fixed throttle — shallow enough that the ADI does **not** saturate, i.e. keep the nose under ~+25°, and hold it 10 s+. `CAP-05`'s 50%-throttle clip failed on exactly this: it was a zoom, the ADI pinned at sky fraction 0.730, and the nose angle became unreadable | `BL-115` (`ClimbGravityScale`), `BL-092` |

### HUD — ammo gauge in frame

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### Camera

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

*(The numpad +/− distance trim needs no capture — you already have video of it.)*

### Audio

| ID | Capture | What must be audible | Unblocks |
|---|---|---|---|

### Weather & visuals

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-11` | `SunIncidence` per chapter | World brightness framed like `OriginalScreenshots/C1 IA1 Zone1 environment Spawn3.png`, for every chapter, plus a few seconds of video each. **Priority pair: a C1B night mission and a C1C bright-day mission** — the two extremes the self-scaling model predicts (0.43 / clamp 1.0) and is riskiest on | `BL-110` |
| `CAP-12` | Cloud-deck pass-through | A climb from below the cloud band, through it, and out above, **altimeter visible throughout** so puff density can be correlated against altitude | `BL-118` |
| `CAP-13` | Lens flare | The sun at several screen positions — centred, near-edge, and partially occluded by terrain — to read the streak count, colour and fade | `BL-165` |
| `CAP-22` | C5 city building density | A low pass through C5's downtown city blocks (IA1), close enough to street level to tell whether buildings sit as one consistent skyline or visibly overlap/interpenetrate each other. Our build currently draws two disjoint building districts on the same footprint (`cb00a`–`cb11a` over `cb12a`–`cb24a`) — this settles whether the original shows only one | `BL-250` |
| `CAP-23` | Water vs shoreline order | A low pass over a C1B shoreline where surf meets open water (the recorded pose is around `-7700,49,-5798`), close enough to tell **which of the two draws on top** — does the surf/foam strip lie over the water, or does the water edge cover it? Any chapter's shore/water boundary answers it; C1B is where our build's contested pair sits | `BL-251` |

### Damage & collision

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-14` | Graze vs crash | At least one shallow graze that survives and one outright crash; a building-corner clip if you can get one | `BL-172` |
| `CAP-15` | Visible damage stages | A graze sequence slow enough to watch a part cross each damage threshold and reach ≤10% HP **without dying** — the mechanism is confirmed working (`docs/HISTORY.md` 2026-07-31); this capture judges whether the panel-flip/smoke-trail look and timing feel right | `BL-121` |
| `CAP-16` | Crash puffs | A full crash sequence, close enough to judge sparks, fireball cluster, black smokeball, dirt burst and debris arcs | `BL-122` |

### World

*(none owed — `CAP-17` discharged 2026-08-04: the map-edge continuation **mirrors**, measured off
`CAP-17 C2 south.mp4`; evidence and method in `playtest/CAP-17/`, finding on `BL-105`. The residual
question there — the size of the mirrored unit, ~3 cells rather than the 1 our code clamps to — does
not need new footage of the original, so no capture is owed for it.)*

---

## 1 · Actionable now (`PT-nn`)

- `PT-01` **E37 pipper — at-the-controls only; the design question is already settled.** There is no
  gun convergence to validate: the fixed reticle is airframe-locked with every weapon aimed at that
  centre, and the floating one exists only because inertial forces make shots lag through a turn —
  velocity inheritance, already modelled. `GunConvergenceDist` 250 m is purely the distance the
  pipper is *drawn* at. *Look for:* it sits where the rounds actually land in a hard turn.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo --fire`. *Blocks:* retiring the
  TUNE.

- `PT-02` **Cloud puffs / cloud deck.** Puff opacity and density, and the in-cloud pass-through
  look. Two known symptoms with the mechanism already traced (`BL-118`): the deck reads with dense
  puffs while you fly through it, and in **C1/IA1** puffs surround the plane at *all* altitudes
  rather than in a band. The vertical extent is a code fix, not a TUNE — but *density* has no data
  source and stays a judgement call. *Look for:* how dense the field should read once it is confined
  to the real band. *Blocks:* cloud sign-off.

- `PT-03` **Wing-light `FlashDuration`** (0.08 s blink). The resource check is done and settled the
  period (1.5 s, data-exact, already shipped) but **not** the on-duration — the data's own 0.0001 s
  is a single-tick pulse, imperceptible at any real frame rate, so 0.08 s is a deliberate widening
  with no better source. *Look for:* whether the wingtip blink reads at the right visibility.

- `PT-04` **Data-driven crash.** `WreckMomentum` 0.4, the piece tumble rate, the debris-arc scale,
  overall crash intensity (fireball + cluster + debris fire are additive — judge the whole), and the
  `snd_exp_ground_a` mix. A/B against branch `bespoke-crash-animation`. *Blocks:* crash sign-off.

- `PT-29` **The damage lab in flight (F5) — the first interactive test any aircraft damage lab has
  had.** `BL-130` records that the labs have only ever been verified by scripted screenshot; this
  adds a live host, so it needs hands on it. `./RunDev.ps1 --fly --plane=player_bhawk` then **F5**.
  *Look for:* (a) dragging a slider changes the HUD `DMG` line, the damage dial and the engine
  rattle while the plane keeps flying; (b) a drag is smooth — the per-frame read-back must not fight
  the mouse; (c) take a graze off scenery and the slider drops on its own; (d) **R** returns the
  panel to 100 %; (e) "repair all" clears the torn panels and the fire trail without a visible
  stutter. Note that zeroing a critical part does **not** down the plane on its own — death is
  decided on the next impact (`FlightController.SurviveHit`), which is the intended behaviour, not a
  bug to report. *Blocks:* the interactive half of `BL-130`.

- `PT-30` **The weapon lab at the controls (`PLAN-weapon-lab`, landed 2026-08-03).** The whole plan
  was verified by scripted twin and log line — every interactive action has one — but nobody has
  flown it. `./RunGame.ps1 --weapon-lab --plane=player_bhawk --chapter=C2`, then **B** for the
  panel. *Look for:* (a) click the bay, fire guns (Space), watch the splash; click a warehouse, fire
  a rocket (`F`), watch it break — the impact should be the chapter's authored one, not a stand-in;
  (b) step the hardpoint bank and watch the wing models change with the selection; (c) the mount
  stepper's group is the one that flashes when you fire; (d) **V** out to the impact point and back
  — the view must not jump in either direction; (e) the stand-off slider and re-parking feel usable
  at both ends (15 m and 1100 m). *Blocks:* the interactive half of `PLAN-weapon-lab`'s end-to-end
  verification (step 2), the only step no script covers.

- `PT-13` **Damage-stage smoke/fire re-test (A1 / `BL-199` landed 2026-07-31).** Hold a
  destructible in its stages with gunfire: black smoke should sputter at ≤60 % HP, black smoke +
  climbing fire at ≤30 %, both anchored on the object, for as long as the stage holds (past 32 s),
  ending at death or reset. *Look for:* (a) the sputter reads as intermittent thickness, per the
  authored 50 %/0.1 s dice; (b) **size** — the authored puffs are 0.6–1 m growing ×3, which reads
  small from flight range; **tune it yourself** via `config.json`'s `puffer` block
  (`sustainSizeScale` is the damage-stage smoke; `burstSizeScale`/`trailSizeScale` cover the other
  spawn paths — `--dump-config` writes the template), and report the value that reads right.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire --infinite-ammo` (the water tower and
  the `m_build` hangars by the airfield are 60 HP two-stagers). *Blocks:* closing PLAN-m3-polish-3
  A1's cockpit half.

- `PT-14` **C2 blue-water re-test (A3 / `BL-204` landed 2026-07-31).** Fly the C2 coastline and
  fire on both water looks — the open turquoise water and the near-shore blue water that
  previously took no splash. *Look for:* every visible water surface answers with the splash +
  sound, with no readable difference between the two looks; the **C** collider overlay agrees
  (turquoise wireframe over both). The root cause was not a texture-name gap (`BL-041`'s patterns
  already covered every water texture C2 uses) but a mesh-granularity one: a coastal tile is
  mostly beach/cliff by area, so the whole-mesh area-quorum vote gave the real water polygons on it
  to `default` regardless of size — measured at 7.9% of C2's classified water area stranded this
  way, up to 86.7% for the same effect on `buildings` in C4. `SceneBuilder.CollidersForMesh` now
  builds one collider per surface class actually present in a mesh instead of one for the whole
  mesh, so there is nothing left to vote on.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire --infinite-ammo`. *Blocks:* closing
  PLAN-m3-polish-3 A3.

- `PT-15` **C3 spiderweb fly-through re-test (B11 / `BL-206` landed 2026-07-31).** Fly full speed
  dead-centre into the tikicave web (around x −4470, y 130, z −4839; approach from the west/−X
  side). *Look for:* the web starts its 0.7 s fade on approach and the plane passes through it
  unharmed — no crash, no damage, matching `OriginalScreenshots/Videos/C3 Spiderweb.mp4`. The fix
  honours the gamez `intersect_surface` flag install-wide, so also worth a feel pass: wreck debris
  (e.g. the kkgate pieces), spinning props and effect geometry no longer collide anywhere — but
  terrain, water and buildings are unchanged. `./RunGame.ps1 --chapter=C3 --plane=player_bhawk`.
  *Blocks:* closing PLAN-m3-polish-3 B11.

- `PT-16` **kkgate debris fade re-test (B12 / `BL-207` landed 2026-07-31).** Kill the propane tank
  and watch the gate pieces through their ride. *Look for:* the 12 wreck pieces visibly turn
  transparent over their authored 3–6 s fades while still tumbling — mid-fade you should see the
  deck/water through the wood, not opaque-then-gone — and the gate is flyable right behind the
  blast (the pieces build no colliders since B11). The fix: the opaque world shader had no runtime
  alpha path, so the authored fade was a silent no-op; a fading piece now swaps to a translucent
  twin material for the fade's duration. `./RunGame.ps1 --plane=player_pfighter --chapter=C2 --fire`.
  *Blocks:* closing PLAN-m3-polish-3 B12.

- `PT-17` **Rocket explosion A/B against the original (D31 / `BL-016` landed 2026-07-31).** Fire
  each rocket type at open ground and compare against the original, one type at a time:
  `--rocket=wep_05` (ARMOR), the stock `wep_06` (BOOM), `--rocket=wep_08` (SONIC). *Look for:* a
  per-type ring now expanding from the impact — yellow-green cracks for AP, a violet-fringed white
  disc plus a second larger ring above it for HE, a stack of five pale-cyan rings rising for SONIC —
  plus the smoke-trail columns that never had a host node before. The rings' scale/fade timings are
  the data's and were not retuned, so **the remaining "faster than the original" half of `BL-016`
  is the open question**: if the burst still reads too fast after this, that is a new finding, not
  this fix failing. `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets`.
  *Blocks:* closing PLAN-m3-polish-3 D31.

- `PT-18` **Gun-impact looks per surface class re-test (A2 / `BL-203` + `BL-186` landed
  2026-08-01).** Fire on all three classes at normal flight speed, no frame-stepping. *Look for:*
  (a) **water** — each round raises a small white column that persists ~2 s, so a burst walks a
  field of ticks across the surface (A/B `Water Splash.png`; the height/timing is authored data,
  the ×8 column width is TUNE — say if it reads too thin/fat); (b) **dirt** — small textured
  chips tumbling outward for ~1 s (A/B `Dirt Splash.png`), no more flame-sprite look; (c)
  **buildings** — a spark flash plus fast white-hot ricochet sparks flying off the wall (a judged
  stand-in, both authored assets missing from the install — all magnitudes TUNE); the C2/C5
  film-set skyscrapers (`empire`/`chrysler` walls) now class as buildings too.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire --infinite-ammo` (nycity + the coast in
  one flight). *Blocks:* closing PLAN-m3-polish-3 A2.

- `PT-20` **Rocket sound re-test (D32 / `BL-211` landed 2026-08-01).** Fire a mix of rocket types at
  open ground and at water: `--rocket=wep_04` (9M/incendiary — the one whose explosion sound was
  previously silent on land), plus a couple of others (`wep_06` BOOM, `wep_08` SONIC). *Look for:*
  (a) a launch bark now plays the instant each rocket leaves the rail; (b) 9M's ground/building
  impact now plays an explosion sound (previously silent — only its water splash sound worked);
  (c) whether what you now hear still "differs from the original" in some other way the survey
  didn't find (no rocket has an authored in-flight/flyout loop sound in the data, so there is
  nothing more to wire without a new lead — say what's still off, or request a capture).
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1` (F fires). *Blocks:* closing
  PLAN-m3-polish-3 D32.

- `PT-21` **Gun-loop switch/crash re-test (`BL-216`/`BL-217` landed 2026-08-01).** (a) Hold the
  trigger, then cycle gun groups (G) without releasing it — the firing loop sound should switch to
  the newly-selected group's caliber immediately, not keep playing the old one's. (b) Hold the
  trigger into a crash — the firing loop should cut the instant the plane crashes, not keep
  looping under the wreck until respawn. `./RunGame.ps1 --plane=player_pfighter --chapter=C1
  --infinite-ammo --fire` (multiple gun groups to cycle between; fly into terrain for (b)).

- `PT-22` **Destruction-fire shape re-test (the `TEXTURE_SEQUENCE`/blend fix, landed 2026-08-01).**
  Destroy a building and watch its fire for the full 30 s — this is `large_30sec_fire`, the effect
  behind ~1,035 death call sites, so it is worth a long look. *Look for:* (a) flames **climbing**
  from the base and giving way to a rising dark plume, not the stationary ball you reported;
  (b) the fire still **ending at 30 s** (the `BL-212` halt must not have regressed); (c) whether the
  plume now reads too **thin** — `NUMBER` is absent from this puffer's data and defaults to 1 sprite
  per 0.1 s, a guess at the original engine's default, so density is the one number still open and
  is a `config.json` `puffer` tune either way. Also worth a glance in the same flight: the **crash
  fireball** (fly into terrain) now holds its mid flipbook frames rather than washing out white —
  say if that reads better or worse. `./RunGame.ps1 --plane=player_bhawk --chapter=C1
  --fire-rockets`. A/B against the original if you have or can take a capture of a burning wreck.

- `PT-23` **Backface-culling A/B (landed 2026-08-01).** The world now backface-culls like the
  original. The reported symptom is settled — Hollywood's studio screens no longer z-fight — but
  the change also stopped the camera-anchored skydome's near wall drawing over things inside the
  dome, which **revealed distant geometry that was previously hidden**, and that half is unconfirmed
  against the real game. Staged for you in **`playtest/PT-23/`**: 64 spawn shots
  (`<chapter>-<scenario>-spawn<N>.png` — 8 instant-action spawns × the 6 chapters with a
  `stunt_flying` scenario, plus C1C/C2B on `dogfight_ace`, since those two ship no stunt
  scenario), plus `c1-above-cloud-deck-zone1-day.png` and `-zone2-night.png`. *Look for:* (a) C4 — a far mountain range and the Chandler mesa are now
  visible from the spawn; does the original show them or is the horizon meant to close there?
  (b) C1 above the deck — cloud banks and towers over the deck top; right density and draw
  distance? (c) any surface that is now **see-through from the wrong side**, which is what culling
  costs if a polygon's winding disagrees with its data: terrain seen from below, water from
  underneath, hangar/tunnel interiors, the inside of the backlot ring. (c) is the one that would
  send the fix back. `./RunGame.ps1 --chapter=C4` and `--chapter=C1`.

- `PT-24` **Graze reaction re-test at 4× puff size (`BL-090` item 3, first playtest 2026-08-01).**
  Round 1 confirmed the sounds and the 1.5 s cadence, and found the smoke "mostly hidden in the
  surfaces". Both verdicts are now **code defaults**, so the goldens and every scripted run agree
  with what you see: `Puffer.SizeScaleDefault` 1 → **4**, and the graze staged at the **contact
  point** (`graze.siteAtContact` true — set it false in `CSVM/config.json` to A/B the aircraft
  staging the def's authored offsets argue for, though `--det` drops that file). *Look for:*
  (a) whether 4× is now too **big** for the graze specifically — the same scale drives the rocket
  trails and the 30 s destruction fire (`PT-22`), so a "right for fire, wrong for grazes" verdict
  means the graze needs its own scale rather than sharing this one — and `PT-22`'s destruction-fire
  density verdict is now measuring size and count together (`BL-218`); (b) that dirt and a building
  wall give the *same* smoke is correct and needs no report — the two puffers are byte-identical in
  the data, and only the building adds the yellow sparks; (c) the sparks are **still expected to
  read high/delayed** — that is `BL-221`, an unsettled axis-order question covering every def's
  `AT_NODE` offsets, so a "still floating" verdict just confirms it and is not a new bug.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1` (water and dirt both within reach of the C1
  spawn), `--chapter=C2` for walls. ⚠ The `C` collider overlay you wanted for picking surfaces is
  broken — `BL-220`.

- `PT-25` **Per-impact spark burst — Devastator only (`BL-090` item 2 landed 2026-08-01).** Taking
  any damage now sparks at a `pdpN` panel. ⚠ **Fly `--plane=player_pfighter`**: measured
  install-wide, it is the *only* aircraft whose data carries the 0.99 `injure_anims` entry, so on
  the other ten this is correctly silent and testing them proves nothing. Scrape something lightly —
  the threshold is 0.99, so the first scratch fires it. *Look for:* (a) sparks visible **on the
  airframe** at a panel, distinct from the graze reaction's smoke at the contact point (`PT-24`) —
  the two fire together on a scrape and a flank view (numpad 4/6) separates them; (b) whether it
  reads as **sparks** at all — the emitters are `trailpuffer2`/`chippuffer1` at the new 4× size
  scale, and a scripted flank capture reads more like a pale plume than a bright spark shower, which
  would mean the graze needs a smaller scale than the destruction fires (same open question as
  `PT-24` (a)); (c) the ricochet sounds under it (`snd_ricochet1–4`, a 50/50 pick between two
  sequences). *Not a bug:* one hit lighting **two** panels — the data always sparks `pdp4` on top of
  its 40/40 pick between `pdp1` and `pdp2`. `./RunGame.ps1 --plane=player_pfighter --chapter=C1`.

- `PT-26` **Damaged-engine loop (B5 / `BL-090` item 1 landed 2026-08-01).** A second engine loop
  (`snd_damagedengine`) now blends in the moment any part takes damage — every plane carries this
  data, so any of the 11 works. Scrape something lightly and listen for the loop rising under the
  healthy engine sound; it should stay audible (not swamp the healthy loop) and fade back out on
  respawn. *TUNE, not a bug either way* (`BL-223`): the shipped data reads as "full blend on first
  scratch, no ramp," and the mix gain (`flightAudio.damagedEngineMixGain`, default 1.0) has no
  reference recording behind it — judge whether it should ramp in more gradually as damage
  *accumulates*, and whether 1.0 sits right against the healthy engine loop.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1`.

- `PT-27` **Gun-impact smoke (C8 / `BL-061` item 1 landed 2026-08-01).** Strafe **terrain** — not a
  building; a gun's `buildings` entry is the install-missing `bld_damage.flt` — and get inside 500 m
  of where the rounds land, which is the effect's own `PLAYER_RANGE` gate. Each hit should leave one
  small black smoke puff that drifts and fades, with nothing left parked at the last hit once you
  stop firing. What to judge: (a) is one puff per hit the right density at gun rates, or does the
  0.1 s per-group throttle read as gaps; (b) does 0.3 s of emission read as too brief; (c) the
  authored puff is 0.1–0.5 m and black — against dark terrain it is subtle by design, so the call is
  whether the original reads more strongly at the same range. Try the other ammo too if you fit it:
  `dum`/`ap` give a white-hot flash and `mag` adds fire (a different look, not a different bug).
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo`.

- `PT-28` **Night self-lit art (C9 / `BL-214` landed 2026-08-02).** The model `lighting` flag is now
  honoured, so on a night map the cloud sprite cards, water splashes, beacons and effect meshes draw
  at full brightness while the terrain and sea still dim with the mission SUNLIGHT. Fly C1B at night
  and judge **the clouds specifically** — that is the one part with no matched capture of the
  original. What to judge: (a) do the cloud cards read as moonlit at the right level, or as
  blown-out white cut-outs against the dark sea; (b) do the gun splashes on the water read like
  `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` (they measure the same); (c) does
  the skydome still meet the terrain in a grey band — the dome deliberately keeps its fog even
  though the data says otherwise, and a hard horizon edge would mean that call is wrong.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1B --infinite-ammo`.

---

## Everything else

Blocked on an unlanded fix, and tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line — the weapons re-tests (`BL-017`–`BL-028`), the inspect-tool
follow-ups (`BL-042`–`BL-046`), the danger-zone gates (`BL-088`), the numpad camera rebuild
(`BL-150`), graze pushback (`BL-172`) and the whole 2026-07-31 pass (`PT-05`–`PT-12`, retired —
their re-tests ride `BL-203`–`BL-211`, scheduled in `docs/plans/PLAN-m3-polish-3.md` (now
`COMPLETE`); `BL-199`/`BL-204`/
`BL-206`/`BL-207`/`BL-016`/`BL-203`/`BL-212` landed and came back as `PT-13`–`PT-19`). Do not
re-add them here; the entry brings its own test when the fix lands.
