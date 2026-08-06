# Playtest checklist

Everything that needs a human at the controls (or the original game open for A/B), consolidated.
**This file holds only what is actionable *today*.** Anything whose test is blocked on an unlanded
fix lives on its `backlog.md` entry as a `*Playtest after fix:*` line instead — so an empty section
here means the work is queued, not forgotten. Deep evidence and traps live in
[`backlog.md`](backlog.md); the two are kept in step.

**Every item carries a stable ID** — `CAP-nn` for an owed capture, `PT-nn` for something to fly.
Cite them from `backlog.md` and in conversation the way `BL-nnn` is cited. IDs are permanent: when
an item closes its ID retires with it and is never reused, so numbering gaps are expected.
Retired IDs disappear from this file, so never mint a new ID by scanning the entries below — run
**`./New-ItemId.ps1 -Kind CAP`** (or `-Kind PT`), which increments a shared locked counter in
`.git/item-id-counters.json` and is safe under concurrent sessions. Retired IDs' verdicts are in
the retiring commit's message (`git log --grep=<ID>`); pre-2026-08-06 retirements are in
`docs/HISTORY.md` (frozen).

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
| `CAP-26` | Rocket impacts, one clip per type, **with audio** | Fire each rocket type at open ground and film it close enough to count and orient the rings, with clean audio on the same take: `wep_04` (9M/incendiary), `wep_05` (ARMOR), `wep_06` (BOOM/HE), `wep_08` (SONIC). Two playtests point here: `PT-17` found HE's second ring present but its orientation "kinda random", and `PT-20` judged the sounds "a lot better" but not settleable by ear alone. *Look for:* ring count, ring orientation and how fast the burst reads (`BL-016`'s open "faster than the original" half), plus the launch bark and the per-type impact sound | `BL-016` (open half), `BL-211` |
| `CAP-27` | Does the original spark on the airframe at all? | Take damage in the original — a light scrape is enough — with the aircraft in frame (external/chase fine), and look for a **spark burst on the airframe itself**, distinct from smoke at the contact point. ⚠ This capture can **delete** a feature rather than tune one: `BL-090`'s per-impact spark burst is driven by a 0.99 `injure_anims` entry that exists on **1 of 11** aircraft (the Devastator), which the backlog already calls "plausibly an authoring leftover". If the original never sparks, our implementation goes. If it does, `BL-281`'s ricochet mix can be judged | `BL-281`, `BL-090` (item 2) |
| `CAP-25` | The `chunk` debris quad at a slug dirt hit | Sustained slug fire into flat dirt, camera as close to the impacts as the original allows (external/chase view fine — no gauges needed), slow-motion or high frame rate if possible. Decides whether the original ever shows the `gunhit` def's `chunk` debris node — a ~0.5 m quad textured with the perforated `gun_barrel` shroud band (dark dot grid on tan; gamez model 27 → material 4, UVs u 1→2), flung by the three **slug** defs only (`3040`/`5060`/`70slug_gunhit`, all chapters; ap/dum/mag have no debris nodes). Our build draws it faithfully from the data (`Screenshots/Mystery debris.png`, 2026-08-04, freeze-frame zoom); we keep it unless the original provably suppresses it. *Look for:* any small tumbling textured scrap distinct from smoke/chips in the ~2–4 s after each hit — presence or absence both settle it | `BL-203` (landed — this judges a leftover) |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

---

## 1 · Actionable now (`PT-nn`)


- `PT-41` **The per-chapter sky/fog zone in C1B, C2 and C3 (C9 / `BL-277` landed 2026-08-06).**
  Those three define `ZONE2` fog but ship no `zone2` dome at all, so they rendered the engine's
  clear colour with a hard horizon cut; they now build `zone1` — sky and fog together — and the
  dome scale is fitted inside the far plane (C1B's zone1 dome is 21.8 km and clipped open at the
  2.5× anchor). Headless goldens cover the three poses; what they cannot judge is whether the
  chosen sky is the *right* one and how it reads in flight. One flight each
  (`./RunGame.ps1 --plane=player_bhawk --chapter=C1B`, then `--chapter=C3`, then `--chapter=C2`).
  *Look for:*
  - (a) a real dome in all three, from the deck up to the ceiling and looking straight up — no
    grey wedge, no hard cut, at any altitude or heading;
  - (b) C3's haze reads as daylight grey on a sunlit mission (it used to be night-blue), and C2's
    as the pale sky-blue its `ZONE1` authors;
  - (c) C1B stays fogged above ~1.2 km, where the old `ZONE2` band stopped;
  - (d) the four chapters the rule deliberately leaves alone — C1, C1C, C2B, C4 — look exactly as
    they did.

  *Blocks:* `BL-100`'s remaining four chapters are the A/B this sets up; `CAP-11` still judges C1B's
  night brightness separately.

- `PT-39` **The gun line after plan-8 A2/A3: smoke at the gun line, bare casings, and pick the
  muzzle-flash form (`BL-263`).** The invented eject-puff cluster is deleted and the authored
  `muzzlepuffer` renders (6 puffs / 0.3 s, drifting aft); the muzzle flash defaults to the authored
  single rolled node with the `_muzzle1`→`_muzzle2` frame flip, the old triad reachable by setting
  `Projectile.cs`'s `MuzzleFlashCount` back to 3. One flight covers it
  (`./RunGame.ps1 --plane=player_bhawk --chapter=C1B --infinite-ammo`, sustained fire in chase
  view). *Look for:*
  - (a) smoke sits on the gun line and drifts aft, casings tumble bare — A/B vs
    `C1B IA1 Bloodhawk tracer and ejection.png`;
  - (b) the flash: the pick is MADE (triad kept, 2026-08-05, `BL-286`) and the flash is now
    anchored to the muzzle — confirm it rides the plane at speed and reads like it used to.
  - (c) While judging, the muzzle-light magnitudes (`MuzzleLightEnergy` 2.5, `MuzzleLightLife`
    0.03 s) are stand-ins — the def authors range/colour only; flag if the light reads wrong
    (`BL-286`).
  - (d) Fire into the water: the splash now plays its authored fade + flipbook and defaults to
    the authored 1× column width — judge 1× vs the old 8× (`water_1x_close_*` /
    `water_8x_close_*` here, or flip `SplashColumnWidthScale`) against `Water Splash.png`; the
    8× survives only as a config TUNE if 1× still reads wrong (`BL-265`).

  *Blocks:* `BL-263` and `BL-265` close.

- `PT-38` **Rocket/gun quick checks left over from the closed 2026-07-24 m3-polishing fixes**
  (`BL-002`/`BL-003`/`BL-005`/`BL-103`, all landed and code-verified; their entries are closed, so
  this is the one place these look-checks survive). One flight covers all four
  (`./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo`). *Look for:*
  - (a) a rocket fired out to max range self-destructs with the `default` IMPACT effect reading
    acceptably as a mid-air burst, not a ground/water splash floating in the sky;
  - (b) gun tracers start at the muzzle and grow out of it, never appearing behind the plane;
  - (c) a ground crash plays the boom with no `SOUND 'snd_exp_ground_a'` warning in the log;
  - (d) rockets gate at one launch per second (`FIRE_RATE 1.0` is the data's answer; the user was
    never sure whether the original has a cooldown — A/B it if the original is open anyway,
    otherwise judge on its own merits).

- `PT-36` **Splash falloff onto a large neighbour, e.g. a zeppelin gasbag (`BL-239`).** Blast damage
  onto a body other than the one struck now scores to the nearest point on that body's own collision
  shape instead of its transform origin, so a large body — a zeppelin gasbag, a long building mesh —
  no longer soaks less splash than a small one, or none at all when its origin happens to sit outside
  the blast radius entirely. Landed against a controlled synthetic repro (the `blast-neighbor-shape`
  suite); no chapter mission is known to place a rocket-class blast near one end of a real large body,
  so the in-game picture is unverified. Fly at the C1 zeppelin (`hk_zep`) and put a rocket into one
  end of a gasbag, away from dead centre: `./RunGame.ps1 --fly --chapter=C1 --infinite-ammo`.
  *Look for:* the gasbag takes damage from a hit that lands well off its centre, not only from a hit
  near the middle. *Blocks:* `BL-239` sign-off.

- `PT-37` **The sea dive's splash-then-steam ordering (`BL-228`).** `WAIT_FOR_COMPLETION` is
  implemented and `player_crash_water`'s flagged `plane_big_splash` now holds `large_steam_spray`
  for the splash's authored 3.0 s (measured 3.050 s in the `wait-for-completion` suite, on real
  gamez data) instead of both retargeting on the same tick. **No headless run can photograph it** —
  `--crash` resolves `Ground`, so no scripted water crash exists (the same gap `PT-34`/`BL-229`
  left), and every golden stayed hash-identical because none of them dives into water. Fly out over
  the C1 sea and put the plane into it: `./RunGame.ps1 --fly --chapter=C1`, nose down into open
  water well clear of the shoreline. *Look for:* the white splash column rises and fades **first**,
  and only as it finishes does the steam plume start — not both at once. A stopwatch is not needed;
  the question is purely whether the two are sequential or simultaneous. *Blocks:* `BL-228` sign-off
  — and, on the same dive, `PT-34`'s splash-emitter check, since it is the same repro.

- `PT-35` **The rocket rings' template meshes (`BL-061`).** The mesh half landed and is asserted
  in-engine and in `--effects-test`'s census, but no golden sees it — the rings only appear on a
  live rocket impact, and all 13 shots stayed hash-identical through the fix. Fly a rocket into
  terrain: `./RunGame.ps1 --fly --chapter=C1 --infinite-ammo`, and again with
  `--rocket=wep_08` (the sonic, whose IMPACT names `sonic_ground_effect`) and `--rocket=wep_14`
  (the torpedo, over water — `torpedo_water_effect`); stock HE is `wep_06`. *Look for:*
  - (a) HE — a SECOND ring above the ground ring (`he_ring1`, ~12 m up), not just the one on the
    deck;
  - (b) sonic — four rings rising off the impact, which showed nothing at all before;
  - (c) torpedo on water — the splash model and its ripple rings at the hit;
  - (d) after every one of them, nothing left behind: no ring or chunk frozen at the impact point
    once the effect is over.

  *Blocks:* `BL-061` sign-off.

- `PT-34` **The sea dive's splash spray (`BL-229`).** The rule landed and is asserted in-engine, but
  the picture is unverified: there is no headless water crash (`--crash` passes no struck body, so
  the surface classifies `Ground` and plays the dirt variant), so `plane_big_splash` has never been
  seen since the fix. Fly into the water: `./RunGame.ps1 --fly --chapter=C1B` and dive into the sea.
  *Look for:*
  - (a) the white `watersquirt` spray fires ON impact and runs about half a second — the bug was
    it never appearing at all;
  - (b) it reads as a column thrown up from the impact point, not a puff at the plane's last
    position;
  - (c) the splash polys' 3 s scale/fade and the ripples still play underneath it, unchanged.

  *Blocks:* `BL-229` sign-off, and the `D9`/`BL-228` question of whether the steam spray should
  wait for the splash (both retarget on the same tick today).

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
  stop firing. What to judge:
  - (a) is one puff per hit the right density at gun rates, or does the 0.1 s per-group throttle
    read as gaps;
  - (b) does 0.3 s of emission read as too brief;
  - (c) the authored puff is 0.1–0.5 m and black — against dark terrain it is subtle by design, so
    the call is whether the original reads more strongly at the same range.

  Try the other ammo too if you fit it:
  `dum`/`ap` give a white-hot flash and `mag` adds fire (a different look, not a different bug).
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo`.

- `PT-28` **Night self-lit art (C9 / `BL-214` landed 2026-08-02).** The model `lighting` flag is now
  honoured, so on a night map the cloud sprite cards, water splashes, beacons and effect meshes draw
  at full brightness while the terrain and sea still dim with the mission SUNLIGHT. Fly C1B at night
  and judge **the clouds specifically** — that is the one part with no matched capture of the
  original. What to judge:
  - (a) do the cloud cards read as moonlit at the right level, or as blown-out white cut-outs
    against the dark sea;
  - (b) do the gun splashes on the water read like
    `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` (they measure the same);
  - (c) does the skydome still meet the terrain in a grey band — the dome deliberately keeps its
    fog even though the data says otherwise, and a hard horizon edge would mean that call is
    wrong.

  `./RunGame.ps1 --plane=player_bhawk --chapter=C1B --infinite-ammo`.

- `PT-31` **Weapon-gauge arrow sweep A/B against `CAP-18` (A1 / `BL-184` landed 2026-08-04).** The
  gun/missile pointer now tweens to the selected slot at 168.7 °/sim-s instead of snapping — this
  is the item's own acceptance criterion, since the sweep can only be judged against the reference
  clip. Cycle weapons in the cockpit (`./RunGame.ps1 --plane=player_bhawk --chapter=C1
  --infinite-ammo`) and compare against `CAP-18`. *Look for:*
  - (a) the arrow visibly sweeps rather than snapping;
  - (b) the readout (digits/type name) still flips instantly at the start of the move, not
    tweened;
  - (c) a step near the ±180° antipode goes the short way (the counterclockwise choice CAP-18
    measured there);
  - (d) whether the sweep's start/end read as abrupt — the landed version has **no** ease
    (CAP-18's own ~97 ms sim ease at each end was left unimplemented, its shape unmeasured beyond
    "not a smoothstep"; see `docs/formats/hud.md`) — if that reads wrong at the controls, the
    ease is the follow-up, not a re-tune of the rate.

- `PT-32` **Stall-warning blink A/B against `CAP-06` (A2 / `BL-148` landed 2026-08-04).** The `STALL`
  plate now blinks at a speed-dependent rate — 643 ms sim half-period at the 0.30 fd threshold, 296
  at 0.15 — and lights 0.05 fd before the nose breaks. Fly a level deceleration to the stall and back
  out (`./RunGame.ps1 --plane=player_bhawk --chapter=C1`) beside `CAP-06 2.mp4`. *Look for:*
  - (a) the lamp lights while the nose is still flying, and the break comes noticeably later;
  - (b) the blink visibly speeds up as the stall deepens and slows again on recovery;
  - (c) the plate is fully lit or fully dark, never dim — brightness is binary and any fade is a
    bug;
  - (d) whether the rate at the threshold reads right, since the sim/wall conversion is the live
    trap (a wall implementation would blink 39% fast).

  The deep end below 0.15 fd is asserted in the `stall-warning` suite but was never flown in a
  scripted run — that band is the one worth watching.

- `PT-33` **Gun belt low-ammo colour step, own-merits judgement (A3 / `BL-142` landed 2026-08-04).**
  The gun gauge's belt light now turns yellow at 15% of the group's ammo remaining, down from 34% —
  there is no original capture to A/B against (the thresholds were never measured from the original,
  `docs/formats/hud.md`), so this is a judgement call on our own remake, not a fidelity check. Fire
  one gun group down from full (`./RunGame.ps1 --plane=player_bhawk --chapter=C1 --gun-select=0
  --fire`, or hold the trigger manually) and watch the belt light. *Look for:*
  - (a) does yellow still feel too early or too late against a real magazine's length of
    sustained fire;
  - (b) does the light sit yellow for a satisfying "getting low, plan around it" stretch rather
    than flickering on right before empty;
  - (c) whether red-only-at-literal-zero (no separate "critical" tier) reads as a gap — that
    would be a new item, not a re-tune of this constant.

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
