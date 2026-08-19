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
`.git/item-id-counters.json` and is safe under concurrent sessions. ⚠ **Run it for EVERY id, every
time**: it is not a once-per-session lookup, and deriving the next id by adding 1 leaves the counter
behind the file, so the number you invented gets handed out again later. `-Count n` reserves a block
in one call when you need several. Retired IDs' verdicts are in
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
| `CAP-20` | Throttle equilibria + a shallow held climb | Two level runs held to equilibrium at **1/4** and **1/2** throttle (the thrust-vs-throttle curve), then a **shallow, steady climb** at fixed throttle — shallow enough that the ADI does **not** saturate, i.e. keep the nose under ~+25°, and hold it 10 s+. `CAP-05`'s 50%-throttle clip failed on exactly this: it was a zoom, the ADI pinned at sky fraction 0.730, and the nose angle became unreadable. ⚠ Still owed after D32, and now the ONLY thing that can settle the climb residual: the 90° climb clip gives a clean speed plateau (163.05 mph at a 56.3° path) but its ADI saturates too, so the nose angle — and with it α, the leading candidate for the model's remaining +25% — is unreadable in every climb capture taken so far | `BL-410` (the sustained-climb residual; `ClimbGravityScale` itself is retired) |

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
| `CAP-27` | Does the original spark on the airframe at all? | Take damage in the original — a light scrape is enough — with the aircraft in frame (external/chase fine), and look for a **spark burst on the airframe itself**, distinct from smoke at the contact point. ⚠ This capture can **delete** a feature rather than tune one: our per-impact spark burst is driven by a 0.99 `injure_anims` entry that exists on **1 of 11** aircraft (the Devastator), plausibly an authoring leftover (was `BL-090` item 2, closed — `git log --grep=BL-090`). If the original never sparks, our implementation goes. If it does, `BL-281`'s ricochet mix can be judged | `BL-281` |
| `CAP-30` | Firing-wobble amplitude across calibers and airframes | Dead-astern external/chase clips, level flight, guns held 3 s+: **(a)** one plane with two well-separated calibers (30 vs 70), **(b)** one caliber on a light vs a heavy plane, **(c)** — added 2026-08-07 — a **Bloodhawk 40-cal** clip framed and fire-rate-matched to `Gun Wobble and animation.mp4`, giving a *second independent amplitude measurement* of the same case the law was derived from. (c) is what lets this capture serve as `BL-266`(a)'s fallback instrument: (a)/(b) alone ask only whether caliber and plane weight enter the law, and **cannot** settle the uniform ~2–4× shortfall our render shows against the reference clip. ⚠ Dead-astern framing is load-bearing: it makes the on-screen roll angle the world roll angle with no projection model (`analysis/gun-wobble-shake/FINDINGS.md`, capture spec there). Confirms or refutes the pure-caliber magnitude law (7e-5 × caliber, measured on one 40-cal clip) and whether plane model/weight enter; a being-hit clip on the same sortie also pins the impact sources' stand-in quantities | `BL-266` |
| `CAP-29` | Panel-damage semantics | Take controlled damage per part in the original, own aircraft in frame (external/chase), damage display visible if possible. **Reduced 2026-08-15 by the `BL-297` decode**, which answered all three questions out of `crimson.exe` (`docs/org/vehicleDamage.md`, "Damage staging"): (a) effects land at the node the def names, so a nose hit DOES spark wing sites; (b) nothing per-part fires at all while a part's armor absorbs; (c) each entry fires once per downward crossing, so a panel tears once until repaired. **What is still owed is the look:** watch one panel cross its tear threshold and judge whether the flung debris reads as a piece of that panel or as generic flakes, and what visibly changes on the airframe. The other three are now confirmation, worth capturing on the same take if the framing allows but not worth a dedicated sortie | `BL-297` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### AI flight — an AI aircraft flying itself, external view

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-37` | An AI aircraft flying a patrol/attack loop, unprompted by the player | An AI-controlled aircraft in external/chase view, held long enough to cover a sustained turn, a low-speed moment and a patrol leg's end, with the player's own aircraft in frame where possible for a same-shot comparison. Behavioural and comparative questions only, **no absolute distances or speeds read off this footage** (`docs/verification.md`; a decode is never contested with a footage-derived measurement): does it gain altitude through a sustained turn or hold it; is its turn tighter or wider than the player's in the same airframe; does it hold a speed through manoeuvres or bleed and recover like a lever-driven aircraft; does it wallow at low speed or stay crisp; what does it do at the end of a patrol leg | `docs/plans/PLAN-ai-flight.md` F52 (the AI-side at-the-controls verdict for waves C and E) |

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

### C1 · two pilots — Dogfight (splitscreen VS)

```powershell
./RunGame.ps1 --vs --players=2 --chapter=C1
```

- `PT-52` `[Own]` **The puffer distance fade in two panes (`BL-339` landed 2026-08-15, plan B11).**
  The fade now runs its bands against every pane's camera and each particle takes the most
  favourable pane's alpha, so a trail near player 2 draws in player 2's pane. What no instrument
  here can judge is the remaining divergence: one alpha per particle for the whole world, so a pane
  can see a puff its own camera would have faded further. A scripted shot cannot set this up —
  there is no per-player placement flag and no scripted fire, so both panes spawn near-coincident.
  *Launch:* `./RunGame.ps1 --fly --players=2 --chapter=C3` (plain 2-pane free flight, two pads or
  pad + keyboard) — the section's Dogfight launch above works too if a target is wanted.
  *Look for:* (a) the reported repro is gone — P2 astern of P1 fires a rocket past him and sees the
  whole trail, not just the stretch beside P1; (b) neither pane shows a puffer popping in or out as
  the OTHER player turns or flies away (the shared-alpha tell); (c) flying through an emitter still
  culls it in the pane that flew through it rather than filling that screen.
  *Blocks:* the fidelity verdict the plan's nearest/union boundary rule asks for
  (`docs/plans/PLAN-splitscreen-polish.md`'s Milestone goal) — per-pane alpha (one MultiMesh per pane) is
  reached for only if (b) visibly fails, and a fail mints its own `BL` item.
  *Variations:* C3 (`--chapter=C3`, the waterfalls' `spew_puffer` is the tightest authored band);
  `--players=4` for the same question with four alphas competing.
  *Also carries B12 (`BL-340` landed 2026-08-15):* the `FBFX_COLOR_FROM_TO` screen wash now paints
  only the panes whose camera is inside the burst's authored 100 m radius, and the same missing
  levers (no per-player placement, no scripted fire) keep it off the scripted path. In the same
  session: put P2 over the ground alone and have him rocket the terrain — P2's pane flashes
  white/violet and P1's, a few hundred metres off, does not; then fly the pair in together and both
  flash. A wash that still paints all panes, or one that paints none, is the failure.

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

- `PT-53` `[Own]` **Graze feel now that a graze bounces (`docs/plans/PLAN-ai-flight.md` `C25`, landed
  2026-08-15, closing `BL-172`).** A survivable scrape now rebounds along the contact normal off the
  shipped `bounce_factor` 0.6, where before it only slid. Three surfaces, at speed, in C1 or C5:
  - (a) **a shallow belly skim over flat ground** — the plane should come off the ground and fly on,
    not skip like a stone or bury itself. This is the case the suite measures at `e = 0.56`;
  - (b) **an oblique scrape along a building wall or a cliff face** — the rebound there is
    horizontal, so the altimeter should barely move; what to judge is whether being pushed off the
    wall reads as a graze rather than as a bounce off a trampoline;
  - (c) **a building corner** — `CAP-14` shows the original surviving a 144.5 mph graze on a C5
    skyscraper and dying at 144.2 against another, so survival there is geometry, not speed.
  ⚠ The three graze constants (`GrazeKick`, `GrazeFriction`, `GrazeStopSpeed`, `BL-271`) were tuned
  against the OLD no-bounce slide and were not re-tuned when the impulse landed. If a graze feels
  wrong, they are the first suspects — not `bounce_factor`, which is authored data.
  *Blocks:* `BL-271`'s re-tune, `BL-381`'s multi-tick scrape (a fail on (b) is evidence for it), and
  `docs/plans/PLAN-ai-flight.md` F52's player-side graze check (C25) — no separate PT item repeats it.

### AI flight — external view, own build (F52 AI arm)

- `PT-54` `[Own]` **AI plant A/B against the old plant (`docs/plans/PLAN-ai-flight.md` C21–C24, F52 AI
  arm).** Fly the new AI force path, then relaunch flipping AI aircraft back onto the player plant
  with `--no-ai-plant` and fly the same engagement again — the switch exists for exactly this
  comparison (`docs/cli.md` `--no-ai-plant`) and is temporary, removed once this verdict lands.
  ```powershell
  ./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury,player_avenger
  ./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury,player_avenger --no-ai-plant
  ```
  *Look for:* the divergences wave C/E ported onto the AI plant — nose-aligned airflow instead of
  weathervane centring, the AI's own speed floor, ground blow, and the authority ramp/reverse
  factor now shared with the player path (C24) — against a plant with none of them wired in. Does
  the new plant read as a distinct AI flight character, or as indistinguishable from the old one?
  *Blocks:* F52's AI-side verdict.

- `PT-55` `[Own]` **AI plant under `--ai-attack`, free flight (F52 AI arm).**
  ```powershell
  ./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury --ai-attack=9
  ```
  *Look for:* the same behavioural and comparative questions `CAP-37` asks of the original — does
  the AI gain altitude through a sustained turn or hold it, is its turn tighter or wider than the
  player's own in the same airframe, does it hold speed through manoeuvres or bleed and recover like
  a lever-driven aircraft, does it wallow at low speed or stay crisp — now under the hard
  maneuvering of pursuing and firing on a live target.
  *Blocks:* F52's AI-side verdict.

- `PT-56` `[Own]` **AI plant in a chapter mission with patrol nets running (F52 AI arm).**
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury:M4ReinfAce --ai-attack=9
  ```
  *Look for:* the same questions as `PT-55`, this time along a real net in a real mission context —
  what the AI does at a patrol leg's end, and whether the plant holds up once `AiModeMachine` is
  actually cycling patrol/pursue/lay off rather than idling in an empty stage.
  *Variations:* pair with `--debug-ainets=M4ReinfAce` to watch the drawn route alongside the flight.
  *Blocks:* F52's AI-side verdict.

- `PT-58` `[Own]` **Crash avoidance over a ridge that sits above the net's authored altitude.**
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury:M4ReinfAce --ai-attack=9 --debug-markers
  ```
  *Look for:* fly out over the high ground east of the spawn with F13 up and watch a netted enemy
  cross ground that stands above the net's authored 400 m. It should pitch up and climb out on its
  own rather than fly into the slope, and it should rejoin the graph afterwards rather than hold the
  climb. The mode transitions print as `patrol -> avoid crash` and back, naming what the ray struck.
  *Look for also:* the climb-out is a 45° break up and to the right of the aircraft's own track, not
  a vertical pull-up, and the state releases as soon as the line is clear rather than dwelling.
  *Blocks:* the cockpit half of crash avoidance; the mechanism itself is decoded and measured
  (`docs/org/aiPilot.md`, "Crash avoidance is a STATE, not an altitude rule").

### Autogyro, Balmoral, Fury — low-speed authority ramp (F52 player arm)

- `PT-57` `[Own]` **Low-speed handling across `BL-330`'s authority-ramp extremes
  (`docs/plans/PLAN-ai-flight.md` C24, F52 player arm, judged against `BL-330`'s existing corroboration).**
  The ramp fades roll and pitch to nothing at 10 mph and back to full at 50; `BL-330`'s own decode
  picked out the two airframes furthest apart on it — the autogyro (18.5 mph stall, ~21% of
  authority left there) and the Balmoral (45.5 mph stall, ~89% left) — plus a mid-pack airframe for
  the common case (Fury, in the 52–57 mph band nine of the eleven share).
  ```powershell
  ./RunGame.ps1 --plane=player_autogyro --chapter=C1
  ./RunGame.ps1 --plane=player_balmoral --chapter=C1
  ./RunGame.ps1 --plane=player_fury --chapter=C1
  ```
  *Look for:* controls going progressively mushy on the approach to stall and gone outright at
  10 mph, roll and pitch only (yaw is unaffected — C21); the autogyro's fade should be felt hard and
  early relative to its own stall, the Balmoral barely at all, and the Fury somewhere between.
  *Blocks:* F52's player-side verdict.

### C1 · Bloodhawk — ordnance in ordinary flight (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1 --rocket=wep_14 --infinite-ammo
```

`--rocket=<wep_id>` swaps every pylon to the named type, which with the weapon lab is the only way
to fly a type a stock loadout does not carry: all 11 loadouts fit HE `wep_06`, and the Weapon
Loadout screen that would let a pilot fit the rest is `BL-353`, unbuilt. The rocket trigger is
**F**, one round per pull.

⚠ **Every figure quoted in this section and the four below is a decoded constant or an authored
value.** A clip that disagrees with one is evidence about our implementation, never a correction to
the constant: a decode is not contested with a measurement read off a running picture
(`docs/verification.md` DET-12).

- `PT-62` `[Own]` **Torpedo launch inheritance and the launch look (`PLAN-ordnance-types` `A2`,
  `A4`; closes `BL-290`'s at-the-controls half).** `wep_14` authors `LOCK_ON [2.5]` and
  `VELOCITY [60]`, so a torpedo leaves at the launching aircraft's speed and blends that inherited
  vector out linearly across 2.5 s onto its own 60 m/s. `RANGE_MINIMUM` is a hittability gate, not
  an arming or a visibility one: the body is drawn from launch, wings folded and prop absent, with
  the orange flame ribbon, and at 3.5 s on its def's clock the flame stops, the wings swing out over
  5 s, the prop appears, the white puffs start and the arming beeps sound.
  *Look for:*
  - (a) **the decay** with the sea or a shoreline in frame for scale: launched flat out, the round
    pulls ahead fast and visibly settles over about two and a half seconds, then holds a steady
    cruise. Launched slow (throttle back to a speed near 60 m/s first) it should show no settling at
    all, because there is almost nothing to blend out;
  - (b) **the switch** with nothing selected: the round is drawn from the rail with the flame, and
    about 3.5 s out the flame gives way to white puffs while the wings unfold and the beeps sound,
    with no moment at which the round is missing;
  - (c) **no target selected still decays** (the one deliberate divergence from the original, `B6`):
    with `T`/`O` pressed to clear the selection, the round must still settle to 60 m/s rather than
    hold launcher speed the whole way out.

  *Blocks:* `A2`'s and `A4`'s owed clips, and with `PT-63`–`PT-76` the F22 sign-off that completes
  `docs/PLAN-ordnance-types.md`. A fail on (a) or (c) is evidence against
  `ProjectilePool.SteeringStepRuns`, not against the 2.5 s.
  *Variations:* `--view=2` and `--view=4` hold the belly and flank cameras, which is where the
  launch look in (b) reads best; `--target=nearest` with an `--ai=player_fury` up gives (c) its
  positive control.

- `PT-63` `[Own]` **The motor round leaves at launcher speed and climbs above it
  (`PLAN-ordnance-types` `A3`).** `wep_04` authors `ACCELERATION`, so its cap is `VELOCITY 450`
  **plus** the launcher's own speed and it starts at that launcher speed rather than from rest. A
  type without a motor is seeded at its cap and is never accelerated at all. Nothing anywhere
  applies drag, which is why rounds carry so far.
  *Launch:* `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --rocket=wep_04 --infinite-ammo`
  *Look for:*
  - (a) fired from a dive at high speed the round pulls away hard and keeps gaining, with no
    moment where it hangs at the muzzle and then spools up;
  - (b) fired from near-stall it still gains, and reaches its cruise over a visibly longer stretch;
  - (c) side by side with `--rocket=wep_06` (HE, no motor) in the same pass: the HE round leaves at
    its own speed immediately and never gains on itself.

  *Blocks:* `A3`'s owed clip, and F22. The flak consequence `A3` records is the same mechanism seen
  from the ground and rides the variation below.
  *Variations:* `./RunGame.ps1 --fly --chapter=C1 --wake-turrets --pos=-6650,300,-6300
  "--direction=-1,0,-0.2"` parks you over C1's aagun fort, whose `wep_27` flak needs 5.7 s to reach
  its authored 850 m/s and expires at its 900 m range before it gets there. Judge whether the flak
  now reads as too slow to threaten anything, which is a behaviour question for `A3` and a separate
  one from the mix (`BL-389`).

### C1 · Bloodhawk — the weapon lab, one ordnance type at a time (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --weapon-lab=wep_15 --plane=player_bhawk --chapter=C1
```

The lab holds the aircraft in place inside a real C1 flight session and fires through the session's
own projectile pool at the chapter's real surfaces, so impacts play the authored dirt, water and
building rows. `B` hides the panel, the weapon stepper re-arms every pylon without a relaunch,
left-click re-parks the held aircraft facing what you clicked, and `--weapon-camera=free` hands the
view to the spectator camera so a burst can be watched from a few metres away.

- `PT-64` `[Own]` **The three end conditions, and which of them detonates
  (`PLAN-ordnance-types` `A4`).** A round ends on distance travelled reaching `RANGE`, on age
  passing `DETONATION_TIME`, or on closing inside `DETONATION_DISTANCE` of **its own** target, and
  reaching `RANGE` only detonates for a type carrying `LOCK_ON`. So the choker, the cannonball and
  the fake weapon vanish at their range where a torpedo bursts.
  *Look for:*
  - (a) **the timed fuse**: `wep_15` released with nothing near it bursts 2.0 s after launch, every
    time, at whatever distance that puts it;
  - (b) **the range end**: a `wep_12` fired over open water out to its range simply disappears, with
    no burst, no effect and no sound;
  - (c) **the target fuse is a second path**: with `--ai=` up and one aircraft selected, a
    `wep_14` fused by its own target while a different aircraft sits nearer proves the round's own
    target check and the proximity sweep are distinct;
  - (d) nothing bursts at the muzzle: an unauthored `DETONATION_TIME` is off, not instant.

  *Blocks:* `A4`'s four owed clips (all four are asserted in the `ordnance-end-conditions` suite and
  none has been looked at), and F22.
  *Variations:* `--weapon-lab=wep_12` and `--weapon-lab=wep_14` step through (b) and (c) without a
  relaunch; `--weapon-surface=water` parks the lab facing open sea for (b).

- `PT-65` `[Own]` **The blast: quadratic falloff, cover, and the 32-object cap
  (`PLAN-ordnance-types` `C10`, `C11`; closes `BL-227`'s falloff half).** Splash damage is
  `1 − d²/R²` of the authored damage, measured to the nearest point of the target's collision shape,
  so at **half the radius a target takes 0.75** of full damage where the old linear curve gave 0.5.
  A ray from the burst to each candidate drops anything with world geometry in the way, and at most
  32 objects take damage from one burst.
  *Look for:*
  - (a) **the curve reads harder than it did**: HE `wep_06` bursting near a cluster of C1 huts kills
    or stages things at ranges that used to leave them alone. Judge whether the new reach feels like
    an area weapon or like a much bigger one;
  - (b) **cover works**: put a building between the burst and a destructible. The shielded object
    must take nothing at all, and the wall itself must take the burst. Then fire the same burst from
    the open side, where it must stage as in (a);
  - (c) **the limit announces itself**: a burst inside a dense cluster logs one `blast limit:` line
    naming the weapon, the burst and the dropped count. Watch the console rather than the screen,
    and say whether a limited burst reads as visibly unfair from the cockpit. ⚠ The `damage:` lines
    stop after the twelfth of a session (`AnimRuntime.DamageAt`), so a quiet console late in a
    flight is the log budget, not a missing splash.

  *Blocks:* `C10`'s and `C11`'s at-the-controls half, and F22. A fail on (b) that is really the
  wall's own health is not a cover failure; check the wall took the damage before minting anything.

- `PT-66` `[Own]` **`SURFACE_ANIMATION` lies on the slope it struck
  (`PLAN-ordnance-types` `C12`; closes `BL-293`'s actionable half).** The `IMPACT` row's
  `SURFACE_ANIMATION` is spawned with its up axis rotated onto the struck surface normal, while the
  row's plain `ANIMATION` keeps its fixed axis. ⚠ **Do not judge this on flat ground**, where the
  rule changes nothing by construction.
  *Look for:* fire `wep_06` into a real chapter slope, the steeper the better, from the free
  camera: the ground effect must lie along the hillside rather than stand upright out of it, and
  the same weapon into flat terrain must look exactly as it did. Then `wep_12`, whose
  `scatter_effect` is a plain `ANIMATION` and must keep its fixed axis on the same slope.
  *Blocks:* `C12`'s owed slope clip, and F22. `BL-293`'s parked half stays parked whatever this
  shows: the fixed-axis upper ring is a plain `ANIMATION` and is correct as it is.
  *Variations:* `--chapter=C4` or `--chapter=C5` for steeper ground than C1 offers;
  `--weapon-camera=free` to get the eye down onto the surface.

- `PT-67` `[Own]` **The beeper and the seeker as one weapon system (`PLAN-ordnance-types` `B6`,
  `B8`, `B9`).** `wep_10` paints an aircraft for its authored `TIME` and deals no damage at all;
  `wep_11` is the only `BEEPER_SEEKER` and the only type with a real `TURN_RATE`, and it retargets
  every frame onto whatever the tag list holds, ignoring both the shooter's own selection and any
  unpainted aircraft. Turning costs speed on every steering frame.
  *Launch:* `./RunGame.ps1 --weapon-lab=wep_10 --plane=player_bhawk --chapter=C1
  --ai=player_fury,player_avenger --ai-attack=9 --target=ai1_player_fury`
  *Look for:*
  - (a) **the paint is free**: `wep_10` into a hostile aircraft deals nothing, moves no damage
    gauge and no longer reads as a hit;
  - (b) **the seeker turns**: stepped to `wep_11` and fired at the painted aircraft while it
    manoeuvres, the round visibly turns after it and visibly bleeds speed while turning hard. That
    bleed is the original's behaviour and is not to be damped;
  - (c) **it follows the paint, not your selection**: with the second aircraft selected and the
    first one painted, the round still goes for the painted one, and with nothing painted it flies
    straight;
  - (d) a dumbfire type fired at a selected aircraft flies effectively straight, which is what
    proves the gate is on the `LOCK_ON` flag rather than on the turn rate.

  *Blocks:* `B6`'s, `B8`'s and `B9`'s owed clips, and F22. `B7`'s lead blend has no shipped carrier
  that ever reaches its onset time, so nothing at the controls can show it and no row asks for it.

### Empty stage · Bloodhawk against AI — the four no-damage types (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury --ai-attack=9 --rocket=wep_08 --infinite-ammo
```

`--stage=empty` is a collidable ground plane and nothing else, which is what makes an AI's recovery
from a stun readable. The same rows are worth a second pass in a chapter (`--chapter=C1`) once each
one has been judged in the clean case.

- `PT-68` `[Own]` **The sonic and the flash: red and white washes, and an AI going limp
  (`PLAN-ordnance-types` `D14`, `D15`, `D16`).** A sonic or flash burst deals no damage and instead
  washes every human it catches and stuns every AI it catches, out to `IMPACT_PROXIMITY` and through
  the blast's own gather, cover test and 32 cap. Intensity is full strength out to about 77% of the
  radius and fades over the last quarter. The wash is red `(1,0,0)` for `SONIC` and white for
  `FLASH`, at a weight equal to the intensity, for five times the intensity in seconds, after a
  **1.0 s start delay**. `FLASH` also requires the victim to be facing it; `SONIC` does not.
  Nothing touches a human's controls, ever.
  *Look for:*
  - (a) **the player's wash**: fly into a `wep_08` burst **someone else fired** (a vs session is
    the easy rig). The pane goes red about a second after the burst rather than instantly, holds
    for about five seconds at point blank, then clears. Judge whether the five seconds reads as
    punishing or as broken. ⚠ Your **own** burst never washes you: the splash gather excludes the
    round's owner (`FUN_005aca30`), so a firer immune to their own sonic is the original's
    behaviour, not a routing bug;
  - (b) **the facing test**: `--rocket=wep_09` bursting ahead of you washes white, and the same
    burst behind you washes nothing at all;
  - (c) **two hits overlap rather than replace**: two bursts a second apart must neither saturate
    the pane to flat colour nor restart from nothing;
  - (d) **the AI stun**: a sonic burst near the Fury leaves it limp for about five seconds, falling
    with its momentum on the flight model rather than freezing or snapping level, then flying
    again. Hit it again while it is still limp: it must take the new stun;
  - (e) **no ledger moves**: neither the AI's damage nor yours changes from any of it.

  *Blocks:* `D14`, `D15` and `D16`'s owed clips, and F22.
  *Variations:* `--rocket=wep_15`, the flare, whose `IMPACT_PROXIMITY [500]` puts full strength out
  to about 387 m and makes (a) reachable without flying into the burst.

- `PT-69` `[Own]` **The choker cuts the engine and leaves a cloud (`PLAN-ordnance-types` `D17`).**
  `wep_12` leaves a 2 s cloud at the burst holding its `RADIUS` squared, and every frame an
  aircraft's origin sits inside that radius its engine-dead timer is refreshed. The duration mixes a
  squared distance against a raw radius exactly as the original does, so `RADIUS [35]` gives about
  **13 s at the centre and the 5 s floor beyond about 4.6 m**, while the catch radius stays a full
  35 m. It cuts thrust and nothing else, and it applies to a human exactly as to an AI.
  *Launch:* `./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury --ai-attack=9
  --rocket=wep_12 --infinite-ammo`
  *Look for:*
  - (a) a direct hit on the Fury: it holds the choke for roughly thirteen seconds, and a burst
    caught out near the edge of the cloud for about five;
  - (b) **it bleeds, it does not stall**: the choked aircraft loses speed on drag and keeps flying
    ballistically. Anything that reads as an instant stall is our bug, not the original, whose
    instant-stall recollection is recorded as disproven;
  - (c) **the AI does not react**: no evasion, no call, no change of mode. That is correct, because
    no AI code reads the disabled-systems mask;
  - (d) **the missing sound**: nothing in our audio chain reads the engine-dead timer, so a choked
    aircraft still sounds like it is running; what the original plays over the cut is undecoded
    (`BL-423`). Judge how badly that reads before anyone builds it;
  - (e) fly into your own choker: a human is choked the same way, with no wash and no input
    lockout.

  *Blocks:* `D17`'s owed clip, and F22. (d)'s gap is `BL-423`.

- `PT-70` `[Own]` **The smoke screen: no projectile, and a 600 m trap behind the layer
  (`PLAN-ordnance-types` `D18`, launch hook with `A5`).** A `SMOKE_SCREEN` pylon spawns **no
  round**: it lays a screen that tracks the laying aircraft's live pose, spends a round of ammo, and
  plays no `FIRE` sound or animation because both live inside the spawn the branch skips. Every
  frame the screen runs it stuns every AI and washes every human, alive and not the layer, inside
  `smokescreen_stun_range` **600 m** and inside the cone `smokescreen_stun_angle` **170°** about the
  layer's backward axis. ⚠ 170° is a **half**-angle, so the cone opens 85° either side of dead
  astern, which is close to everything behind the layer. That reach is the authored value and the
  instinct that it is a bug is wrong.
  *Launch:* `./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury --ai-attack=9
  --rocket=wep_13 --infinite-ammo`
  *Look for:*
  - (a) **nothing is fired**: no body leaves the pylon, no trail, no impact anywhere, and the ammo
    counter still steps down by one;
  - (b) **the cloud**: it starts at the aircraft, follows it as it manoeuvres, and stops when the
    screen's `TIME` runs out or the layer dies;
  - (c) **the trap**: a pursuing AI inside the cone goes limp and is re-stunned every step it stays
    there, and recovers once it is out. An AI off to the side beyond 85° from dead astern is
    untouched. Say whether 600 m across that cone makes the smoker unbeatable in play;
  - (d) **the layer is immune**, and so is anything ahead of it.

  *Blocks:* `D18`'s owed 1v1, and F22. A verdict that the weapon is too strong is a fidelity note
  against the authored tunables, not a licence to change them.

- `PT-71` `[A/B: CAP-23]` **The smoke cloud's look against the reference footage
  (`PLAN-ordnance-types` `D18`; the decode is `docs/org/ordnanceTypes.md`, "What the cloud is, from
  the numbers").** The two gaps that had left our screen a thin pale ribbon are closed: the trail
  path now spawns the authored four puffs per 0.65 m with the 10 m/s astern `LOCAL_VELOCITY`, and
  the `COLORS` ramp is linearised, so the `53,74,37` green renders on the reference's `50,68,35`
  instead of a washed `109,126,92`. `CAP-23` is the Balmoral's smoker from the rear cockpit plus two
  external poses.
  *Look for:*
  - (a) **density and colour** at a matched pose: our cloud should read as the same weight and the
    same dark green, not paler and not thinner;
  - (b) **the known residual**: our puffs are thousands of stacked `splashbase` quads whose rims
    carry 2–5% alpha that no single sprite shows and a thousand do, so the silhouette closes toward
    a rounded square where the reference reads as round soft blobs. Judge how visible that is in
    motion, at distance and up close;
  - (c) **the whole-`TIME` emission**: the reference is still laying cloud seven seconds after one
    launch, which is what our reading reproduces. The screen must not stop emitting a quarter of the
    way in.

  *Blocks:* the look half of `D18`, and F22. A fail on (b) is fresh evidence for a new `BL` against
  the sprite rim (an alpha test, or a different puff texture), not against the puffer counts, which
  are authored; whether the original's rasteriser dropped that rim is undecoded and nothing
  authored says so.

- `PT-72` `[Own]` **The torpedo as a target: cyclable, shootable, and its destruction effect
  (`PLAN-ordnance-types` `E19`, `E20`).** `TARGETABLE` admits a round to the target list and
  `FLYOUT_HEALTH` makes it destructible; `wep_14` carries both. Its armour pool is always zero, so
  the first hit spends health directly, and health reaching zero destroys the round and plays
  `torpedo_destroy_effect`. **This row cannot be flown until `E19` and `E20` land** (Wave E is open);
  it is written here so the sign-off set is complete.
  *Launch:* `./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury --rocket=wep_14
  --infinite-ammo --target=next`
  *Look for:*
  - (a) a torpedo in flight appears in the target cycle (`T`/`U`) and can be pinned, while an
    ordinary `--rocket=wep_06` round never does;
  - (b) it takes 10 points of gunfire and then dies playing `torpedo_destroy_effect`, rather than
    vanishing or bursting as if it had reached a target;
  - (c) an ordinary rocket is unaffected by the same gunfire.

  *Blocks:* `E19` and `E20`'s owed clips, and F22.

- `PT-73` `[Own]` **The `DAMAGES_ZEPPELIN` gate, player side only (`PLAN-ordnance-types` `E21`).**
  A weapon without `DAMAGES_ZEPPELIN` cannot hurt a gasbag and, on the **AI** side, is refused as a
  shot at a zeppelin at all, while a weapon carrying it is refused against anything else. Only
  `wep_14` and `wep_28` carry it in this install. **The AI half cannot be flown today:** an AI's
  candidate list is aircraft only (`BL-363`), so no AI ever aims at a zeppelin, and no stock fit
  carries a torpedo because the vehicle def's `weapons` tuple is unparsed (`BL-394`), so the Black
  Hat Warhawk's eight torpedoes never reach a pylon. What is flyable is the player half, which
  skips the aim gate by construction.
  *Launch:* `./RunGame.ps1 --chapter=C1 --plane=player_bhawk --zeppelins --rocket=wep_14
  --infinite-ammo --target=gasbag1`
  *Look for:* a torpedo into a gasbag damages it and an HE round (`--rocket=wep_06`) into the same
  gasbag does not, while both still hurt the engines. The engines are the only winning path a
  menu-launched zeppelin run has until `BL-353` fits a torpedo from the loadout screen.
  *Blocks:* the flyable half of `E21`, and F22. The AI half stays owed on `BL-363` and `BL-394` and
  does not block the plan.

### C1 · two pilots — the victim-routed screen wash (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --coop --players=2 --chapter=C1 --debug-wash=2
```

- `PT-74` `[Own]` **The wash is addressed to the viewer who was hit, and blends
  (`PLAN-ordnance-types` `D13`, Decision 2).** The original holds one wash state for the whole
  machine, which would blind viewer 1 when viewer 3 is flashed; ours routes by the victim's own pane
  and composites over the existing proximity ramp instead of replacing it. `--debug-wash=N` fires
  two overlapping scripted washes at viewer N (red at weight 1 for 5 s on the first frame, then
  white at weight 0.5 two seconds in).
  *Look for:*
  - (a) **routing**: pane 2 washes red, then pink as the white lands on it, and pane 1 stays clean
    throughout;
  - (b) **the ramp is untouched**: with `--rocket=wep_06` and no `--debug-wash`, an HE burst still
    washes by camera proximity exactly as it did, both panes flashing when both are near and only
    the near one when they are apart. Take that baseline **before** judging anything else in this
    section;
  - (c) **a real hit routes the same way**: with `--rocket=wep_08`, a pilot caught in the **other**
    pilot's sonic burst washes their own pane alone. (A pilot's own burst never washes them: the
    gather excludes the round's owner, `FUN_005aca30`.)

  *Blocks:* `D13`'s owed two-pane look, and F22.
  *Variations:* `--debug-wash=3` in a two-pane session, which answers to no pane and must paint
  nothing at all.

### C1 · four pilots — the four-viewer ordnance pass (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --coop --players=4 --chapter=C1 --rocket=wep_08 --infinite-ammo
```

Four viewers is the case the whole plan is written against: the original routes its wash through a
single global and that does not survive four panes. ⚠ `BL-389` already reports that the splitscreen
weapon mix wants a retune, with rockets too quiet against guns and worst with four guns firing at
once. **A mix problem is not a behaviour problem**: judge what happens, and file loudness against
`BL-389` rather than against any item in this plan.

- `PT-75` `[Own]` **The disabling types with four viewers on one team (`PLAN-ordnance-types` F22,
  `D13`, `D15`, `D18`).**
  *Look for:*
  - (a) **two viewers washed in the same second** carry their own wash each, with the other two
    panes clean, and neither washed pane is brighter or shorter for having a neighbour;
  - (b) **a smoke screen laid by one pilot** stuns and washes only the pilots actually behind it,
    and the layer's own pane stays clear;
  - (c) **`--rocket=wep_12`**: a choked pilot's own pane shows nothing at all (the choker has no
    wash), and only their thrust goes;
  - (d) **the blast still reads**: HE bursts near two panes at once still stage destructibles the
    way `PT-65` judged them with one viewer;
  - (e) frame cost holds up with four panes and a dense burst (`--debug-fps`), since every splash
    candidate now costs a cover ray.

  *Blocks:* F22's four-viewer half, and with it the plan's completion. A fail here on routing is a
  `D13` regression; a fail on loudness is `BL-389`.

- `PT-76` `[Own]` **The same pass under Dogfight rules, four viewers hostile
  (`PLAN-ordnance-types` F22).** `--coop` puts every human on one team, so the beeper's hostility
  gate and the AI-side effects never fire between players there. Dogfight makes them mutually
  hostile, which is the only way to judge the tag gate and a human-on-human paint.
  *Launch:* `./RunGame.ps1 --vs --players=4 --chapter=C1 --rocket=wep_10 --infinite-ammo`
  *Look for:*
  - (a) a beeper into a hostile human paints them and deals nothing, and a second beeper into an
    already-painted pilot neither re-tags nor refreshes the first;
  - (b) stepped to `wep_11`, a seeker follows the painted pilot across all four panes and its trail
    draws in every pane it passes;
  - (c) `wep_08` and `wep_09` between hostile humans wash the struck pilot's pane alone, with the
    facing rule still holding for the flash;
  - (d) the match keeps scoring normally: none of the no-damage types registers a hit or a kill.

  *Blocks:* F22's Dogfight half, and with it the plan's completion.
  *Variations:* `--players=2` and `--players=3` for the intermediate pane counts, which is where a
  routing off-by-one would show.

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
