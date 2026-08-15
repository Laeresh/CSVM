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
unusable.** This already cost two takes. The capture spec and the clip-validity rules were in
`analysis/video-flight-calibration/FINDINGS.md`, deleted 2026-08-14; recover them with
`git log -p -- analysis/video-flight-calibration/FINDINGS.md`.

### Flight model — cockpit gauges in frame, head turn off

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-20` | Throttle equilibria + a shallow held climb | Two level runs held to equilibrium at **1/4** and **1/2** throttle (the thrust-vs-throttle curve), then a **shallow, steady climb** at fixed throttle — shallow enough that the ADI does **not** saturate, i.e. keep the nose under ~+25°, and hold it 10 s+. `CAP-05`'s 50%-throttle clip failed on exactly this: it was a zoom, the ADI pinned at sky fraction 0.730, and the nose angle became unreadable. ⚠ Still owed after D32, and now the ONLY thing that can settle the climb residual: the 90° climb clip gives a clean speed plateau (163.05 mph at a 56.3° path) but its ADI saturates too, so the nose angle — and with it α, the leading candidate for the model's remaining +25% — is unreadable in every climb capture taken so far | `BL-115` (the sustained-climb residual; `ClimbGravityScale` itself is retired) |

⚠ **Partial STICK deflection cannot be captured: the controls are keyboard, so pitch, roll and
yaw are 100 % or 0 %.** Any capture asking for "a light, steady pull" or any other intermediate
*axis* position is unfilmable by construction, not merely unflown — do not file one, ask the binary
instead. This retired `CAP-32` (2026-08-15). **Throttle is not affected**: it is a stepped setting
and every eighth is reachable from the keyboard, which is how `CAP-31` flew 1/8 and `CAP-05` flew
50 %, so `CAP-20`'s 1/4 and 1/2 runs above remain perfectly filmable.

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
| `CAP-34` | Wing-light flare shape + view-dependence | Any player plane except the Bloodhawk (the one airframe with no wing-light anim or flare nodes) with wing lights on, one continuous orbit from front through side to tail. Close enough to read whether the flare shows sharp radiating star points (vs a soft round glow) and whether it stays visible across the orbit or only from a narrow chase-view cone | `BL-284` |

### Damage & collision

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-26` | Rocket impacts, one clip per type, **with audio** | Fire each rocket type at open ground and film it close enough to count and orient the rings, with clean audio on the same take: `wep_04` (9M/incendiary), `wep_05` (ARMOR), `wep_06` (BOOM/HE), `wep_08` (SONIC). Two playtests point here: `PT-17` found HE's second ring present but its orientation "kinda random", and `PT-20` judged the sounds "a lot better" but not settleable by ear alone. *Look for:* ring count, ring orientation and how fast the burst reads (`BL-016`'s open "faster than the original" half), plus the launch bark and the per-type impact sound | `BL-016` (open half), `BL-211` |
| `CAP-27` | Does the original spark on the airframe at all? | Take damage in the original — a light scrape is enough — with the aircraft in frame (external/chase fine), and look for a **spark burst on the airframe itself**, distinct from smoke at the contact point. ⚠ This capture can **delete** a feature rather than tune one: `BL-090`'s per-impact spark burst is driven by a 0.99 `injure_anims` entry that exists on **1 of 11** aircraft (the Devastator), which the backlog already calls "plausibly an authoring leftover". If the original never sparks, our implementation goes. If it does, `BL-281`'s ricochet mix can be judged | `BL-281`, `BL-090` (item 2) |
| `CAP-28` | Torpedo flight dynamics | The aerial torpedo (`TORPDO`) released in level flight, ideally at high speed, filmed external/chase with the surface in frame and held from release to impact — long enough to read the speed decay the user saw at the controls (a max/cruise speed, slowing after launch). HUD in frame lets `analysis/video-flight-calibration` decode speed over time; without it the decay is still readable against fixed terrain | `BL-290` |
| `CAP-30` | Firing-wobble amplitude across calibers and airframes | Dead-astern external/chase clips, level flight, guns held 3 s+: **(a)** one plane with two well-separated calibers (30 vs 70), **(b)** one caliber on a light vs a heavy plane, **(c)** — added 2026-08-07 — a **Bloodhawk 40-cal** clip framed and fire-rate-matched to `Gun Wobble and animation.mp4`, giving a *second independent amplitude measurement* of the same case the law was derived from. (c) is what lets this capture serve as `BL-266`(a)'s fallback instrument: (a)/(b) alone ask only whether caliber and plane weight enter the law, and **cannot** settle the uniform ~2–4× shortfall our render shows against the reference clip. ⚠ Dead-astern framing is load-bearing: it makes the on-screen roll angle the world roll angle with no projection model (`analysis/gun-wobble-shake/FINDINGS.md`, capture spec there). Confirms or refutes the pure-caliber magnitude law (7e-5 × caliber, measured on one 40-cal clip) and whether plane model/weight enter; a being-hit clip on the same sortie also pins the impact sources' stand-in quantities | `BL-266` |
| `CAP-29` | Panel-damage semantics | Take controlled damage per part in the original, own aircraft in frame (external/chase), damage display visible if possible. Three questions: **(a) location** — take fire ONLY on the nose: do sparks/debris/fuel vapor ever appear at the WINGS, or does everything stay at the struck part? **(b) armor gate** — on a fresh plane with armor still absorbing, do any skin panels tear, or only once a part's armor is gone (health damage)? **(c) repetition & look** — watch one panel cross its tear threshold: how many debris bursts fire, does that panel's debris ever repeat later in the same flight, does the flung debris read as a piece of that panel or as generic flakes, and what visibly changes on the airframe | `BL-297` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

---

## 1 · Actionable now (`PT-nn`)

### C1 · Bloodhawk — the overcast sky, ground to above the deck

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1
```

- `PT-47` `[A/B: both C1 IA1 Fog stills + CAP-12]` **The overcast-match plan's exit verdict**
  (`OriginalScreenshots/C1 IA1 Fog river.png`, `.../C1 IA1 Fog above clouddeck.png`,
  `playtest/CAP-12/`; the plan is `docs/plans/PLAN-overcast-match.md`,
  completed 2026-08-09; it closed `BL-118`, `BL-312`, `BL-303`, `BL-100`, `BL-101`, whose records
  are in that plan and in `git log --grep=<ID>`). Three waves rebuilt this sky — the sprite
  scatter, the fog model and the deck's brightness — and every number in the plan's final tables
  is a still. This is the one sortie that judges it **in motion**, which is the half no box can
  reach: climb from the river up through the whiteout and out above the deck, then do it again
  looking back down.

  Both reference stills are freecam-reproducible if you want the exact frames beside you:
  `--pos=-7323,192,-3829 --direction=-0.997,-0.1,0.070` (river) and
  `--pos=-7323,1192,-3829 --direction=0,0,-1` (above deck). ⚠ Both originals are chase frames at
  a slightly different pitch from ours, so judge *character* against them, not the horizon's
  height in frame (the plan's `SHOT-23` and its C24 tables have the numbers).
  *Look for:*
  - (a) **below the deck, and while climbing** — the ceiling is one mottled sheet that reaches the
    horizon and dies into the fog wall with no sky stripe and no visible rim. The 13-px bright
    strip at the deck's edge that used to appear on a climb should be gone (it is now ~4 px, hidden
    inside the dome wall's own gradient). Climb slowly from 200 m to 900 m watching the horizon:
    nothing should slide, step or brighten as you go;
  - (b) **the whiteout crossing** — entering the band at ~970 m the world should ramp to a total
    whiteout in the core (1032–1062 m) and clear again by ~1124 m, with no snap and no hard edge
    at either end, and no cloud card visibly punching through the pane;
  - (c) **above the deck, the tops and the floor are ONE tone** — the straight-edged wedges the
    deck floor used to cut through the near cards are gone (measured: floor↔card gap +49 → −4).
    Look down and forward: no hard colour cut anywhere along the mesh↔sprite boundary, and nothing
    darker than the fog colour showing between cards;
  - (d) **C1C above its band** (`--chapter=C1C`, climb past 1082 m) — this one is **expected to
    look WORSE than C1**, and judging how much worse is the point. Its `fvol` cards author
    `lighting: true` where C1's do not, so its frame holds three cloud tones at once: placed cloud
    facades 235, deck floor 196, `fvol` cards 164. `BL-327` asks whether that flag is a
    `WorldLight` gate at all; your verdict on how bad it reads is what prioritises it;
  - (e) **density and character against the original** — cloud spacing and size versus `CAP-12`'s
    own climb, at grazing angles along the tops and along the base: no lattice or comb at any
    angle, no visible field edge over the base map, and the sheet's mottling reading as
    multi-scale rather than smooth broad bands (ours is measurably blurrier than the original's —
    high-pass RMS ≈ 0.2 against ≈ 0.9 — so say whether that is visible in motion).
  - (f) **the in-band flicker** (`BL-329`, `PLAN-weather-decompile-match` D32) — hold still a few
    seconds in the whiteout RAMP, not the opaque core (~970–1032 m or ~1062–1124 m; the fully
    white core in between is a flat colour by design and never flickers): the pane should shimmer
    subtly rather than sit dead flat, on a pace of roughly a couple to several seconds per swing.
    The rate is a declared TUNE (`BandFlicker.DefaultRate`), not a decoded figure, so judge
    whether it reads as "clouds breathing" at all — too fast reads as a strobe, too slow reads as
    nothing happening.

  *Blocks:* this is the plan's own exit verdict — a pass confirms it. A fail on (a), (b), (c) or
  (e) is fresh evidence on the closed item's successor, not a reopening: name which check failed
  and mint against the mechanism it belongs to (`A7`/`C25`/`C26` for (a), `CLOUD_COVER` for (b),
  `C23`'s fork for (c), `BL-312`'s scatter or `BL-327`'s far-field half for (e)). (d) has its
  item already: it feeds `BL-327`. (f) feeds `BL-329`.
  *Variations:* `--chapter=C4` for the one deck chapter whose `WorldLight` clamps to 1.0 — its
  deck must look exactly as it did, and its cards must still be *there* above the band.

### C1 · Devastator — debris arc and ground contact

```powershell
./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire --infinite-ammo
```

- `PT-46` `[A/B: original C1 Devastator destruction]` **Re-opened check (d): debris after the
  `OBJECT_MOTION` decode (`BL-346`).** The earlier observation stands — some original pieces pass
  through terrain or vanish rather than resting on it — but its mechanism attribution does not: it
  was read as evidence the original ground-tests nothing, and `PLAN-object-motion-decode` found the
  opposite, a default column-tier contact test that `NO_ALTITUDE` opts out of and
  `DO_INTERSECTIONS` upgrades to a full sweep. *Look for:* the debris arc at its authored speed now
  that the 0.65 launch multiplier is deleted with nothing put back in its place, and whether pieces
  land under the decoded default tier; distinguish an unflagged pass-through (faithful — the
  original sinks those through too) from a flagged body that should be landing and is not. Shell
  casings stay the `NO_ALTITUDE` exception and should arc away and vanish, never land. *Blocks:*
  `BL-346`; a mismatch mints a new follow-up rather than restoring the deleted scalar.

### C1 · two pilots — Dogfight (splitscreen VS)

```powershell
./RunGame.ps1 --vs --players=2 --chapter=C1
```

- `PT-43` `[Own]` **Dogfight v1 feel (PLAN-vs-mode landed 2026-08-06).** The invented splitscreen
  deathmatch — no original splitscreen reference exists, so every call here is a judgement on our
  own remake. Two pads (or pad + keyboard); menu path: Dogfight → any chapter → both press Start.
  **(a)/(b)/(e)/(f) flown 2026-08-13 (`BL-342`'s C7) — settled, see below. (c) confirmed a real
  problem, folded into `BL-301`. Still owed: (d) at 4 players.**
  *Look for:*
  - (a) ~~damage balance plane-vs-plane~~ — settled 2026-08-13: good, 70-cal shreds planes;
  - (b) ~~hitting at all without the original's aim assistance~~ — settled 2026-08-13: `BL-342`
    built the decoded sticky-bullet assist and landing guns is markedly easier now, even on a
    straight-flying target; `BL-301`'s strength call is closed too (no retune);
  - (c) spawn camping viability after the 3 s auto-respawn (no invulnerability by design) —
    **confirmed 2026-08-13: every player has a fixed spawn point and camping one is very much
    viable.** Tracked as `BL-301`'s existing spawn-protection deferral, now evidenced rather than
    speculative;
  - (d) opponent edge-arrows + the status line: readable at 2- and 4-player pane sizes, arrows
    flip to the right edge, marker vanishes while the opponent is down — **2-player confirmed
    2026-08-13 (small but readable); 4-player still owed, no second controller pair on hand that
    session;**
  - (e) ~~kill banners, the end board's rows/winner/draw, and the R-rematch flow~~ — settled
    2026-08-13: all good;
  - (f) ~~draw frequency at the 5-kills / 5-minutes defaults~~ — settled 2026-08-13: good.

  *Blocks:* the `BL-301` tuning decisions; a structural fail mints its own `BL` item.
  *Variations:* `--players=4` for pane-size readability; `--vs-kills=1` for a fast board check;
  `--scenario=zeppelin_run` to judge whether `dogfight_ace` spawns are actually the better pick.

- `PT-49` `[Own]` **`PerfHud` legibility in a 4-player pane (PLAN-perf-hitches D10/D11).** The
  frame-cost readout (F14, `--debug-fps=`) sizes off the whole window's height, not the pane it
  happens to be drawn over — it draws once for the window, not once per pane — so a full-window
  screenshot at 1P cannot say whether it is still readable once the window is quartered. Full's
  five-line panel plus the frame-time strip below it is taller than Compact's single line, so it
  is the one more likely to run into a pane edge. No original reference; this is a judgement call
  on our own instrument.
  *Look for:* Compact's `perf [F14]: compact — … fps  frame … ms  worst … ms` line, and Full's
  five lines plus its bar-graph strip, top-left of the window, both stay legible (not too small to
  read at a glance, not clipped by a pane edge) with `--players=4`.
  *Blocks:* nothing existing tracks this; a fail mints a new `BL` item against `PerfHud`'s
  `ReferenceFontSize`/`WindowScale`/strip sizing.
  *Variations:* `--debug-fps --players=4 --vs --chapter=C1`; `--debug-fps=full --players=4` for the
  taller panel specifically; also worth a glance at `--players=2`.

### C1 · two pilots — stunt race (splitscreen starting grid)

```powershell
./RunGame.ps1 --stunt --players=2 --chapter=C1
```

- `PT-45` `[Own]` **The abreast race starting grid** (`docs/PLAN-race-grid.md`, landed 2026-08-08;
  it closed `BL-084`, whose record is in that commit — `git log --grep=BL-084`).
  Two pads (or pad + keyboard); menu path: Stunt → C1 → both press Start. Splitscreen stunt racing is
  our invention — the original had no splitscreen at all — so every call here is a judgement on our
  own remake, with no reference to A/B against.

  **Why this sitting is the only evidence there will ever be.** The grid is selected only when a
  session is an actual race, and a `--det` run is explicitly given the old per-player spawn walk
  instead, so no scripted run, screenshot or golden can exercise this path — by design, since that
  bypass is what keeps every scripted spawn byte-identical. The grid geometry is also not
  photographable: the panes are chase-cam only, so at the default 60 m spacing your neighbour sits
  outside your own frustum. **Read the geometry off the console instead** — every launch logs one
  line per slot, e.g. `spawn [P1 grid slot 1 of 4] pos=(-4974,260,-3771) heading=90° spacing=60m
  lift=81m`, with the anchor's own line above them. On C1 with `--spawn=0` the field lifts 81 m.

  **Both numbers are live config, and settling them is the point of this sitting.** `slotSpacing`
  (default **60 m** between neighbouring slots) and `groundClearance` (default **100 m** of air the
  lowest slot must have under it) are read from `config.json` as `raceGrid.slotSpacing` and
  `raceGrid.groundClearance`, listed by `--dump-config`, and take effect on the next launch with no
  rebuild. Neither is a finding — 60 m is just the figure already in the tree — so dial them between
  launches until the start looks right and record what you landed on.
  *Look for:*
  - (a) **does it read as a starting line** — at the moment of spawn, does the field feel like a
    grid you are lined up on, at 2 panes and at `--players=4`;
  - (b) **spacing at the wingtips** — 60 m: too far apart to feel like a race start, or too close
    for comfort in the first seconds of manoeuvring? Try 30 m and 100 m before deciding;
  - (c) **the uniform lift** — the whole field rises together by whatever its worst slot needs, so
    over broken ground it can look absurd (the field hovering high over a valley) or, if clearance
    is dialled too low, too tight (an outer wingtip in a hillside). Watch an outer slot, not P1;
  - (d) **a felt end-of-grid advantage** — do the outer slots feel meaningfully better or worse than
    the middle for reaching the first Danger Zone? Slots are fixed by player index today; a *felt*
    bias is the trigger to randomise the slot order per race (not to rotate it per rematch);
  - (e) **the anchor still varies** — relaunch a few times without `--spawn=`: the whole grid should
    sit somewhere else each time (the anchor is a random pick from the mission's spawn list), not on
    the same point every launch;
  - (f) **`--pos` still wins** — `--pos=x,y,z` must still place the field where you asked, grid or
    no grid, since the override is resolved beneath the grid rather than beside it.

  *Blocks:* the two config values in (b)/(c) hardening from fallbacks into decisions; the
  slot-rotation call in (d); and `BL-314`, the race countdown, which must not be started until the
  grid it counts down over has been flown. A structural fail — a plane in terrain, a field that is
  not level or not on one heading — mints its own `BL` item.
  *Variations:* `--players=4` for the case (a)/(b)/(d) are really about; `--chapter=C2` for a
  different terrain profile under (c).

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
  *Blocks:* nothing open — `BL-100`'s remaining four chapters were settled by render evidence
  instead (`PLAN-overcast-match` `B12`: C1/C2B/C4/C1C = `zone2`), not by a fresh flight.
  *Variations:* one flight each — repeat with `--chapter=C3`, then `--chapter=C2`; (d)'s four
  untouched chapters need only a glance in each.

### C1 · Bloodhawk — Instant Action wingmen (`--ia=`)

Save this as `playtest/PT-50/ia-wingmen-test.json` (git-ignored, kept until the item closes) and
launch it by ABSOLUTE path:

```powershell
./RunGame.ps1 --ia=Z:\CSVM\playtest\PT-50\ia-wingmen-test.json --chapter=C1
```
```json
{
  "mission_type": "dogfight_squadron",
  "player_plane": "Bloodhawk",
  "num_wingmen": 3,
  "wingman_plane": "Fury"
}
```

⚠ **A relative `--ia=` path resolves against the GODOT PROJECT directory** (`<repo>/CSVM/`), not
the repo root and not your shell's directory: Godot chdirs there for `--path`, and the file is
read with plain `File.ReadAllText`. Measured 2026-08-15, after this block spent a while telling
you to put the file "next to the repo", where nothing would find it. Absolute paths always work.

- `PT-50` `[Own]` **D9's wingman flight (`docs/plans/PLAN-instant-action.md` D9, landed 2026-08-14).**
  Three Fury wingmen spawn on team 1 alongside the player's Bloodhawk, fanned 100/100/200 m off
  its spawn heading at ±45°, escorting per the decoded `primary_target` chain (0/1 escort the
  player directly; 2 escorts wingman 1). No original splitscreen/Instant Action reference exists
  for this — every call here is a judgement on our own remake, and this is not a thing a suite can
  answer (the suite only proves the count/team/airframe/pure math). *Look for:*
  - (a) **does it read as a flight** — do the three Fury wingmen sit in a plausible formation
    around the Bloodhawk at spawn, or does the fan look wrong (too tight, too spread, overlapping,
    behind rather than beside);
  - (b) **do they actually fly** — watch a minute or two: do the wingmen hold a sensible course
    near the player rather than drifting off alone or diving into terrain (the D11 mode machine is
    driving them, not a scripted formation, so some independent movement is expected and correct);
  - (c) **A2's decoded friendly fire** — shoot a wingman down. This is supposed to be possible
    (Decision 3/A2: the original applies friendly damage) — confirm it feels like a bug in the
    HUD/feedback (no distinguishing "friendly" cue) rather than in the mechanic itself, since no
    gate was added on purpose.

  *Blocks:* (b) is now answered in the negative and filed as `BL-362` (no station-keeping exists,
  and the `primary_target` escort chain is unreachable because same-team candidates are skipped
  before it is tested), so fly (b) as a judgement on how badly it reads, not as an open question.
  (a) and (c) stand: a fail on (a) is fresh evidence for the placeholder `AiPilot` law (Wave D/E's
  own open scope, not this item's decode) or a new `BL` item; (c) reads confirm Decision 3/A2 rather
  than opening anything.
  *Variations:* `"num_wingmen": 5` with 1–2 `--players=` to see decision 8a's clamp in the spawn
  log; any other `mission_type` besides `dogfight_ace` (which forces wingmen to 0).

- `PT-51` `[Own]` **Every Instant Action actor now patrols the chapter's first net (`BL-364`,
  landed 2026-08-15).** The symptom that raised it was enemies flying straight in one direction and
  a wave out of engagement range never turning back. The ace, the wingmen and every wave member are
  now given C1's net 10 (`M4ReinfAce`, a ring at 400 m), which the launch log names as
  `ia: actors patrol 'M4ReinfAce' (net 10), the chapter's first`. The steering law underneath is
  still the placeholder, so this is a judgement on whether the behaviour reads right, not on
  whether the path is exact. *Look for:*
  - (a) **do they come back**: let a wave lose you and watch. It should turn and circle rather
    than shrink to a dot on the horizon;
  - (b) **after a fight**: break off an engagement and watch an enemy return to patrol. It should
    settle back onto the ring, not orbit one waypoint forever (that limit cycle is the invented
    `PatrolThrottle`'s known failure, `BL-364` trap (a));
  - (c) **the wave teleport**: when wave 2 arrives, does it patrol from where it appeared rather
    than turning back toward the map origin;
  - (d) **the ring is shared**: every actor walks the SAME net, so some clustering is expected and
    correct. Judge whether it reads as a busy patrol or as a conga line.

  *Blocks:* a pass closes `BL-364`'s landed half, leaving only its campaign-roster remainder. A
  fail on (b) is evidence for the placeholder law (Wave D/E scope), not for the net data; a fail on
  (a) or (c) is a fresh `BL` item.
  ⚠ **Fly this from the worktree, not the main checkout**, until `worktree-ia-patrol-nets` is
  merged: the fix is on that branch. `$env:CSVM_DATA_ROOT` already points at `Z:\CSVM`, so
  `./RunGame.ps1` there finds Godot and the game data by itself. Its own mission file (the
  section's has no enemies, and (a)–(d) need some) is staged at
  `playtest/PT-51/ia-patrol-test.json`:

  ```powershell
  cd Z:\CSVM\.claude\worktrees\ia-patrol-nets
  ./RunGame.ps1 --ia=Z:\CSVM\.claude\worktrees\ia-patrol-nets\playtest\PT-51\ia-patrol-test.json --chapter=C1 --debug-markers --debug-spectate
  ```

  `--debug-spectate` takes you out of the mission entirely (your plane is pinned, inert and
  untargetable, the pane becomes the free camera following an AI), because a player in the air
  pulls the whole first wave onto themselves within seconds and there is then no patrolling left
  to watch. `--debug-markers` marks every plane at once, red hostile / blue own side, with its
  range. **F13** draws the nets themselves, which is what turns (a) into a direct read: the
  markers should be walking the drawn graph. Confirm the launch log says
  `ia: actors patrol 'M4ReinfAce' (net 10), the chapter's first` before judging anything: without
  that line the actors have no net and (a)–(d) are moot.
  *Variations:* drop both debug flags and fly it yourself for the (b) judgement, which needs you
  to engage and break off; `"num_wingmen": 0` leaves the enemies nothing to chase at all, the
  cleanest look at pure patrol; another chapter to see a different first net (C1B walks
  `Patrolboat3`, C1C `M1Defense`).

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
