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
| `CAP-28` | Torpedo flight dynamics | The aerial torpedo (`TORPDO`) released in level flight, ideally at high speed, filmed external/chase with the surface in frame and held from release to impact — long enough to read the speed decay the user saw at the controls (a max/cruise speed, slowing after launch). HUD in frame lets `analysis/video-flight-calibration` decode speed over time; without it the decay is still readable against fixed terrain | `BL-290` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

---

## 1 · Actionable now (`PT-nn`)


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

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
