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

**Structure.** Section 0 lists owed captures as themed tables — one table per filming batch, the
theme naming the capture setup (cockpit gauges in frame, external view, …) — with fixed columns
ID · Capture · What must be in frame · Unblocks. The theme sections are standing — an emptied
table stays, meaning nothing is currently owed in that batch. Section 1 groups flights by **flight
profile** — one section is one sortie (chapter + plane + situation), headed by a copyable launch
command. Sections sort by chapter then plane; items within a section by ascending ID. An item free
to choose its plane or chapter piggybacks on an existing profile — it never opens a section of its
own. Every PT item is one bullet:

    - `PT-nn` `[A/B: <ref>]`-or-`[Own]` **What to check (`BL-NNN`).** context… *Look for:* … *Blocks:* …

`[A/B: <ref>]` names the capture or `OriginalScreenshots/` shot to have open *before* launching;
`[Own]` is a judgement call on our own remake with no original reference. A mixed item takes
`[A/B]` — the per-check references stay in their bullets. *Look for:* holds one sub-bullet per
check, lettered `(a)(b)(c)` when there is more than one. *Blocks:* is mandatory — name what a pass
closes, or state outright that nothing tracks the outcome and a fail mints a new `BL` item.
Optional: context prose between title and *Look for:* (a few lines at most — deep evidence lives
in `backlog.md`), and *Variations:* for extra flags or re-runs beyond the section's command.

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
| `CAP-20` | Throttle equilibria + a shallow held climb | Two level runs held to equilibrium at **1/4** and **1/2** throttle (the thrust-vs-throttle curve), then a **shallow, steady climb** at fixed throttle — shallow enough that the ADI does **not** saturate, i.e. keep the nose under ~+25°, and hold it 10 s+. `CAP-05`'s 50%-throttle clip failed on exactly this: it was a zoom, the ADI pinned at sky fraction 0.730, and the nose angle became unreadable | `BL-115` (`ClimbGravityScale`) |
| `CAP-32` | A deliberately **part-deflected** pull, level entry | Full throttle, level cruise, then a held **partial** back-stick pull (clearly less than full deflection — a light, steady pull, not a tap), sustained long enough for speed to settle. Speedo, altimeter and ADI in frame. Gives a second load-factor point below `CAP-01`'s max-pull plateau, so the induced-drag exponent (`n`, `n²` or `ω²` in the pull) stops being a free choice | `BL-307` |
| `CAP-33` | A sustained turn at a bank other than ~100° | Full throttle, full back stick, banked turn held to a settled equilibrium (speed and heading rate both flat) at a bank clearly different from `CAP-01`'s ~100° — a ~60–70° bank is the useful target. ADI, speedo, altimeter, compass tape all in frame throughout | `BL-307` |

**`CAP-31` was flown and decoded on 2026-08-07, and its row is retired.** It was the 1/8-throttle
deceleration — the case `CAP-05`'s clip never covered, since 0/8 has no equilibrium to approach. The
prediction was recorded here before the capture so it could fail, and it did, in the direction
neither branch offered: **13.94 sim s over 290 → 150 mph against the model's 12.10**, so the original
coasts *longer* than we do rather than markedly shorter, and the standing unmodelled-airbrake
hypothesis has no support in the footage. Full numbers, the throttle-chop transient that makes 13.94
an upper bound, and the correction to the 137.9 mph equilibrium are on `BL-115` and in
`analysis/video-flight-calibration/FINDINGS.md`. `BL-115` stays open on `StallNoseRate`,
`ClimbGravityScale` and `KnifeAlignFloor`, none of which a level deceleration can reach.

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
| `CAP-23` | Water vs shoreline order | A low pass over a C1B shoreline where surf meets open water (the recorded pose is around `-7700,49,-5798`), close enough to tell **which of the two draws on top** — does the surf/foam strip lie over the water, or does the water edge cover it? Any chapter's shore/water boundary answers it; C1B is where our build's contested pair sits | `BL-251` |

### Damage & collision

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-26` | Rocket impacts, one clip per type, **with audio** | Fire each rocket type at open ground and film it close enough to count and orient the rings, with clean audio on the same take: `wep_04` (9M/incendiary), `wep_05` (ARMOR), `wep_06` (BOOM/HE), `wep_08` (SONIC). Two playtests point here: `PT-17` found HE's second ring present but its orientation "kinda random", and `PT-20` judged the sounds "a lot better" but not settleable by ear alone. *Look for:* ring count, ring orientation and how fast the burst reads (`BL-016`'s open "faster than the original" half), plus the launch bark and the per-type impact sound | `BL-016` (open half), `BL-211` |
| `CAP-27` | Does the original spark on the airframe at all? | Take damage in the original — a light scrape is enough — with the aircraft in frame (external/chase fine), and look for a **spark burst on the airframe itself**, distinct from smoke at the contact point. ⚠ This capture can **delete** a feature rather than tune one: `BL-090`'s per-impact spark burst is driven by a 0.99 `injure_anims` entry that exists on **1 of 11** aircraft (the Devastator), which the backlog already calls "plausibly an authoring leftover". If the original never sparks, our implementation goes. If it does, `BL-281`'s ricochet mix can be judged | `BL-281`, `BL-090` (item 2) |
| `CAP-25` | The `chunk` debris quad at a slug dirt hit | Sustained slug fire into flat dirt, camera as close to the impacts as the original allows (external/chase view fine — no gauges needed), slow-motion or high frame rate if possible. Decides whether the original ever shows the `gunhit` def's `chunk` debris node — a ~0.5 m quad textured with the perforated `gun_barrel` shroud band (dark dot grid on tan; gamez model 27 → material 4, UVs u 1→2), flung by the three **slug** defs only (`3040`/`5060`/`70slug_gunhit`, all chapters; ap/dum/mag have no debris nodes). Our build draws it faithfully from the data (`Screenshots/Mystery debris.png`, 2026-08-04, freeze-frame zoom); we keep it unless the original provably suppresses it. *Look for:* any small tumbling textured scrap distinct from smoke/chips in the ~2–4 s after each hit — presence or absence both settle it | `BL-203` (landed — this judges a leftover) |
| `CAP-28` | Torpedo flight dynamics | The aerial torpedo (`TORPDO`) released in level flight, ideally at high speed, filmed external/chase with the surface in frame and held from release to impact — long enough to read the speed decay the user saw at the controls (a max/cruise speed, slowing after launch). HUD in frame lets `analysis/video-flight-calibration` decode speed over time; without it the decay is still readable against fixed terrain | `BL-290` |
| `CAP-30` | Firing-wobble amplitude across calibers and airframes | Dead-astern external/chase clips, level flight, guns held 3 s+: **(a)** one plane with two well-separated calibers (30 vs 70), **(b)** one caliber on a light vs a heavy plane. ⚠ Dead-astern framing is load-bearing: it makes the on-screen roll angle the world roll angle with no projection model (`analysis/gun-wobble-shake/FINDINGS.md`, capture spec there). Confirms or refutes the pure-caliber magnitude law (7e-5 × caliber, measured on one 40-cal clip) and whether plane model/weight enter; a being-hit clip on the same sortie also pins the impact sources' stand-in quantities | `BL-266` |
| `CAP-29` | Panel-damage semantics | Take controlled damage per part in the original, own aircraft in frame (external/chase), damage display visible if possible. Three questions: **(a) location** — take fire ONLY on the nose: do sparks/debris/fuel vapor ever appear at the WINGS, or does everything stay at the struck part? **(b) armor gate** — on a fresh plane with armor still absorbing, do any skin panels tear, or only once a part's armor is gone (health damage)? **(c) repetition & look** — watch one panel cross its tear threshold: how many debris bursts fire, does that panel's debris ever repeat later in the same flight, does the flung debris read as a piece of that panel or as generic flakes, and what visibly changes on the airframe | `BL-297` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

---

## 1 · Actionable now (`PT-nn`)

### C1 · Bloodhawk — climbing through the overcast

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1
```

- `PT-44` `[A/B: OriginalScreenshots/Videos/Gun Wobble and animation.mp4]` **The plane wobble
  (`BL-266`).** The authored shake oscillators are wired as visual-only roll on the plane node:
  gunfire buzz (measured amplitude law: 7e-5 × caliber, radians), overspeed rattle (gated at
  rated max speed), and being-hit rocks. Fire a long burst in chase view with the reference clip
  open, dive past rated max, then take hits in `--vs`. *Look for:*
  - (a) firing: a subtle fast roll buzz while the trigger is held, matching the clip's
    character — visible against the world, small, stops with the trigger. The engine A/B read
    ~2–4× weaker than the clip per frame (`analysis/gun-wobble-shake/FINDINGS.md` "Engine A/B");
    judge whether that reads too tame at the controls.
  - (b) overspeed: no rattle in level cruise at any throttle; sets in only past rated max in a
    dive and grows with speed.
  - (c) being hit (`--vs`, second player): a short rock on gun hits, a harder one on a rocket.
  - (d) view coupling: in chase view the **plane** wobbles against the world (the camera holds);
    the wobble also moves muzzle flashes/tracer origins with the wings.
  *Blocks:* nothing tracks a pass — a fail (too tame / wrong character) re-opens the amplitude
  half of `BL-266` with `CAP-30`'s captures as the instrument.

- `PT-42` `[A/B: OriginalScreenshots/C1 IA1 Cloud Puffs and Moon.png]` **The `fogvol.zrd` cloud
  field (C10 / `BL-273` landed 2026-08-06).** The hand-tuned `CloudPuffs` field is deleted; what
  draws now is the chapter's own clutter table scattered through its `fvol*` volumes, with no
  tuning constant anywhere in it ([`docs/formats/fogvol.md`](docs/formats/fogvol.md)). C1 places
  9,025 sprites in a 120 m slab at 970–1090 m — climb to ~3,500 ft and back down through it.
  *Look for:*
  - (a) does it read as a real overcast with depth — base, interior, tops — or as a flat sheet;
  - (b) **the grid.** At a grazing angle the 130 m scatter lattice shows as a faint comb. If the
    original has no such structure that is evidence against the reading of `distance`, and the fix
    is in fogvol.md's inference list — *not* a new tuning constant;
  - (c) density and opacity against the reference shot;
  - (d) C1B, C2 and C3 must show **no** ambient field at all — they ship no fog volumes. C1B still
    has its 70 placed `cloudparent` sprites; C2 and C3 have nothing. That is the data, not a
    regression.

  *Blocks:* `BL-118`'s reopened density judgement is exactly this. `CAP-12` is delivered and
  analysed (2026-08-07, `playtest/CAP-12/`): the original's deck band is 3290–3560 ft — the
  authored slab — its base reads luma 167 vs our 221, and it shows **no** grazing-angle comb, so
  (b) above now has its reference: if our field combs, the original doesn't. (The C4 take's last
  third — due-north over the river — was reused 2026-08-07 for `BL-105`'s map-edge unit size:
  `playtest/CAP-12/c4-mapedge/`.)
  *Variations:* `--chapter=C1C` for the twelve authored build-up towers above the deck, and
  `--chapter=C5` at street level for its low night haze between the skyscrapers.

### C1 · Devastator — strafing terrain

```powershell
./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo
```

- `PT-27` `[Own]` **Gun-impact smoke (C8 / `BL-061` item 1 landed 2026-08-01).** Strafe
  **terrain** — not a building; a gun's `buildings` entry is the install-missing `bld_damage.flt` —
  and get inside 500 m of where the rounds land, which is the effect's own `PLAYER_RANGE` gate. Each
  hit should leave one small black smoke puff that drifts and fades, with nothing left parked at the
  last hit once you stop firing. *Look for:*
  - (a) is one puff per hit the right density at gun rates, or does the 0.1 s per-group throttle
    read as gaps;
  - (b) does 0.3 s of emission read as too brief;
  - (c) the authored puff is 0.1–0.5 m and black — against dark terrain it is subtle by design, so
    the call is whether the original reads more strongly at the same range.

  *Blocks:* nothing open — `BL-061` is closed; a fail mints a new `BL` item.
  *Variations:* try the other ammo too if you fit it: `dum`/`ap` give a white-hot flash and `mag`
  adds fire (a different look, not a different bug).

### C1 · two pilots — Dogfight (splitscreen VS)

```powershell
./RunGame.ps1 --vs --players=2 --chapter=C1
```

- `PT-43` `[Own]` **Dogfight v1 feel (PLAN-vs-mode landed 2026-08-06).** The invented splitscreen
  deathmatch — no original splitscreen reference exists, so every call here is a judgement on our
  own remake. Two pads (or pad + keyboard); menu path: Dogfight → any chapter → both press Start.
  *Look for:*
  - (a) **damage balance plane-vs-plane** — a gun kill measured 18 rounds of `wep_00` in the suite;
    does that read as right at the controls, and do rockets (fuse + falloff blast) feel like the
    practical weapon they were in the original;
  - (b) **hitting at all without the original's aim assistance** — if landing guns feels hopeless,
    that is `BL-301`'s bullet-magnetism line, not a damage tune;
  - (c) spawn camping viability after the 3 s auto-respawn (no invulnerability by design);
  - (d) opponent edge-arrows + the status line: readable at 2- and 4-player pane sizes, arrows
    flip to the right edge, marker vanishes while the opponent is down;
  - (e) kill banners, the end board's rows/winner/draw, and the R-rematch flow (R must still be
    respawn while the board is hidden);
  - (f) draw frequency at the 5-kills / 5-minutes defaults.

  *Blocks:* the `BL-301` tuning decisions; a structural fail mints its own `BL` item.
  *Variations:* `--players=4` for pane-size readability; `--vs-kills=1` for a fast board check;
  `--scenario=zeppelin_run` to judge whether `dogfight_ace` spawns are actually the better pick.

### C1B · Bloodhawk, night — sky, clouds, self-lit art

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1B --infinite-ammo
```

- `PT-28` `[A/B: C1B IA1 Bloodhawk tracer and ejection.png]` **Night self-lit art (C9 / `BL-214`
  landed 2026-08-02).** The model `lighting` flag is now honoured, so on a night map the cloud
  sprite cards, water splashes, beacons and effect meshes draw at full brightness while the terrain
  and sea still dim with the mission SUNLIGHT. Fly C1B at night and judge **the clouds
  specifically** — that is the one part with no matched capture of the original. *Look for:*
  - (a) do the cloud cards read as moonlit at the right level, or as blown-out white cut-outs
    against the dark sea;
  - (b) do the gun splashes on the water read like
    `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` (they measure the same);
  - (c) does the skydome still meet the terrain in a grey band — the dome deliberately keeps its
    fog even though the data says otherwise, and a hard horizon edge would mean that call is
    wrong.

  *Blocks:* nothing open — `BL-214` is closed; a fail mints a new `BL` item.

- `PT-41` `[Own]` **The per-chapter sky/fog zone in C1B, C2 and C3 (C9 / `BL-277` landed
  2026-08-06).** Those three define `ZONE2` fog but ship no `zone2` dome at all, so they rendered
  the engine's clear colour with a hard horizon cut; they now build `zone1` — sky and fog
  together — and the dome scale is fitted inside the far plane (C1B's zone1 dome is 21.8 km and
  clipped open at the 2.5× anchor). Headless goldens cover the three poses; what they cannot judge
  is whether the chosen sky is the *right* one and how it reads in flight. *Look for:*
  - (a) a real dome in all three, from the deck up to the ceiling and looking straight up — no
    grey wedge, no hard cut, at any altitude or heading;
  - (b) C3's haze reads as daylight grey on a sunlit mission (it used to be night-blue), and C2's
    as the pale sky-blue its `ZONE1` authors;
  - (c) C1B stays fogged above ~1.2 km, where the old `ZONE2` band stopped;
  - (d) the four chapters the rule deliberately leaves alone — C1, C1C, C2B, C4 — look exactly as
    they did.

  *CAP-11 evidence (2026-08-07, `playtest/CAP-11/`; the capture is retired, `BL-110` closed):*
  three of the look-fors already have numbers. (b) is **failing** — at the matched canyon pose
  (`--pos=-3504,710,-3619`, 2329 ft) the original is nearly clear (near slope 36, far hills
  20–60, blue-gradient sky 195) while our C3 renders full `c9c9c9` murk (near slope 130, far
  hills flat 201, sky = fog): the "daylight grey haze" is far too dense and the sky the wrong
  colour. (d) fails for **C2B above the deck** — original dark-blue dome 82.7, ours flat b0b0b0
  fog 176 at 1230–1500 m. Both, plus C5's black sky, are now `BL-303` (shared 9000–10000 m
  fog-band suspect). And C1B's night brightness is judged: terrain at the matched spawn −12%
  (`WorldLight` 0.426 confirmed, `git log --grep=BL-110`), but our zone1 dome is **−26%** vs the
  original's night sky and **no moon renders** where the original shows a large one
  (`t0.5`/`t5` stills) — `WorldBuilder.BuildHorizon` knows how to billboard a moon, so the (a)
  sweep should check whether the built zone1 subtree simply lacks the node.
  *Blocks:* `BL-100`'s remaining four chapters are the A/B this sets up.
  *Variations:* one flight each — repeat with `--chapter=C3`, then `--chapter=C2`; (d)'s four
  untouched chapters need only a glance in each.

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
