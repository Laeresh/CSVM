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

⚠ **No capture here answers a flight-model question, and none may be filed.** A number off the
cockpit panel does not settle a flight quantity: footage cannot confirm a decode, it only ranks
readings, and it will rank a reading nobody has thought of (`docs/verification.md` DET-12, and
DET-11 on the sim clock that makes every wall-clock rate wrong by 39 %). Flight questions go to
`crimson.exe` — see [`docs/org/flightModel.md`](docs/org/flightModel.md). The captures below are **qualitative**: what a thing looks and
sounds like, watched and frame-sampled, never measured into a constant.

### HUD — ammo gauge in frame

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-45` | The **pause screen's** objectives panel, with an objective already completed | Mostly answered by `Complete Mission M02.mkv` (see `BL-466`), which shows the pause screen's whole composition. One question it does not answer: nothing had completed yet when the screen was opened, so **how a completed objective is marked, and whether it stays listed or drops, is still unknown**. What is owed is one pause screen opened *after* at least one objective has completed, held long enough to read. Nothing else needs filming. | `BL-466`, whose presentation is now settled apart from the completed-row marking. |

### Camera

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### Audio

| ID | Capture | What must be audible | Unblocks |
|---|---|---|---|
| `CAP-48` | Does the original play the train-destroyed line during CM16's Sparks pickup? | Fly `CM16` (`C4/M01`, Rocky Mountains - Raid on the Rocky Express) in the original to the Sparks pickup, and film the pickup cutscene from the moment it takes the screen through to the mission ending, clean audio on the take. The film destroys the Rocky Express itself about 16.7 s in (large fireball at the engine, all four cars sliding to a stop) and wins the mission about 5 s after that, so the whole clip is roughly 25 s. **What is owed is the audio over that window**: whether Zachary's `VO_c4-RM-m1_Zachary_59.wav` plays over the explosion and the fade to black. Play `extracted/soundsh/VO_c4-RM-m1_Zachary_59.wav` first, 4.3 s, so the line is known by ear before the sortie. Note the debrief's primary rows on the same take if the ending reaches them, in particular whether "Rescue Sparks" is marked. Our build plays that line there and leaves that row unmarked, and both follow from the decode: the film's own `CALL_ANIMATION [destroy_car01]` completes the objective watching `train01`'s healthy subtree, which plays the line and kills the rescue objective 4.85 s before `got_sparks` can reach EXECUTED (`git log --grep=CAP-48`). The gates that could have made this ours alone are closed in the exe: `FUN_0046a490` suspends the objectives runtime only on the code-20 slot, which this film never raises, and `FUN_004ed8c0` refuses a call by runtime state, never by authored activation, so a `WeaponHit` destructible starts on a `CALL_ANIMATION` | Nothing tracks the outcome. A match closes the question as faithful; a silent original, or one that marks the rescue, mints a new `BL` for the objective the film's own explosion retires |

### Weather & visuals

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-34` | Wing-light flare shape + view-dependence | Any player plane except the Bloodhawk (the one airframe with no wing-light anim or flare nodes) with wing lights on, one continuous orbit from front through side to tail. Close enough to read whether the flare shows sharp radiating star points (vs a soft round glow) and whether it stays visible across the orbit or only from a narrow chase-view cone | `BL-284` |

### Damage & collision

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-39` | Cockpit view while firing | In the original, hold the guns for a second or two IN the cockpit view (mode 6), ideally at dusk or against a dark cliff so a light flash reads. A sweep of all 130 existing clips found no cockpit-view firing footage: every gun clip is nose or chase view. *Look for:* how bright and how warm the flash reads inside the canopy, and where it lands — CSVM implements `muzzle_burst`'s authored first-person lights (`testfp`'s `PLAYER_1ST_PERSON` branch: `bigmuzzle_lt` + `muzzle_lt` at the authored offsets, ranges and colour), so the clip CALIBRATES their look rather than settling whether the interior lights at all. Energy is a declared TUNE (the data carries none, so both lights start at the third-person stand-in's 2.5), and the emphasis to match is the canopy struts above the head, which is where the original puts it at the controls; the windshield bullet-hole decals on taking window hits ride the same sortie if one happens | `BL-436` (the muzzle-light energy TUNE), `BL-431` |
| `CAP-26` | Rocket impacts, one clip per type, **with audio** | Fire each rocket type at open ground and film it close enough to count and orient the rings, with clean audio on the same take: `wep_04` (9M/incendiary), `wep_05` (ARMOR), `wep_06` (BOOM/HE), `wep_08` (SONIC). Two playtests point here: `PT-17` found HE's second ring present but its orientation "kinda random", and `PT-20` judged the sounds "a lot better" but not settleable by ear alone. *Look for:* ring count, ring orientation and how fast the burst reads (`BL-016`'s open "faster than the original" half), plus the launch bark and the per-type impact sound | `BL-016` (open half), `BL-211` |
| `CAP-27` | Does the original spark on the airframe at all? | Take damage in the original — a light scrape is enough — with the aircraft in frame (external/chase fine), and look for a **spark burst on the airframe itself**, distinct from smoke at the contact point. ⚠ This capture can **delete** a feature rather than tune one: our per-impact spark burst is driven by a 0.99 `injure_anims` entry that exists on **1 of 11** aircraft (the Devastator), plausibly an authoring leftover (was `BL-090` item 2, closed — `git log --grep=BL-090`). If the original never sparks, our implementation goes. If it does, `BL-281`'s ricochet mix can be judged | `BL-281` |
| `CAP-30` | Firing-wobble amplitude across calibers and airframes | Dead-astern external/chase clips, level flight, guns held 3 s+: **(a)** one plane with two well-separated calibers (30 vs 70), **(b)** one caliber on a light vs a heavy plane, **(c)** — added 2026-08-07 — a **Bloodhawk 40-cal** clip framed and fire-rate-matched to `Gun Wobble and animation.mp4`, giving a *second independent amplitude measurement* of the same case the law was derived from. (c) is what lets this capture serve as `BL-266`(a)'s fallback instrument: (a)/(b) alone ask only whether caliber and plane weight enter the law, and **cannot** settle the uniform ~2–4× shortfall our render shows against the reference clip. ⚠ Dead-astern framing is load-bearing: it makes the on-screen roll angle the world roll angle with no projection model (`analysis/gun-wobble-shake/FINDINGS.md`, capture spec there). Confirms or refutes the pure-caliber magnitude law (7e-5 × caliber, measured on one 40-cal clip) and whether plane model/weight enter; a being-hit clip on the same sortie also pins the impact sources' stand-in quantities | `BL-266` |
| `CAP-38` | Beeper and seeker hits ON an aircraft, **with audio** | In the original, fire the beeper (`wep_10`) and the seeker (`wep_11`) at an aircraft and film the hit itself, external/chase, close enough to read the burst on the airframe. Both weapons author `ANIMATION large_fireball` on their aircraft `IMPACT` row and ours plays exactly that on a fused or struck plane; the ground-side look is already signed off, so this clip is only the on-plane half. *Look for:* whether the original shows the large fireball on the plane, something smaller, or nothing beyond the paint, and the per-type impact sound on the same take (`snd_missile_beeper` / `snd_missile_seeker`) | Nothing tracks the outcome; a mismatch with our on-plane burst mints a new `BL` |
| `CAP-29` | Panel-damage semantics | Take controlled damage per part in the original, own aircraft in frame (external/chase), damage display visible if possible. **Reduced 2026-08-15 by the `BL-297` decode**, which answered all three questions out of `crimson.exe` (`docs/org/vehicleDamage.md`, "Damage staging"): (a) effects land at the node the def names, so a nose hit DOES spark wing sites; (b) nothing per-part fires at all while a part's armor absorbs; (c) each entry fires once per downward crossing, so a panel tears once until repaired. **What is still owed is the look:** watch one panel cross its tear threshold and judge whether the flung debris reads as a piece of that panel or as generic flakes, and what visibly changes on the airframe. The other three are now confirmation, worth capturing on the same take if the framing allows but not worth a dedicated sortie | `BL-297` |
| `CAP-47` | A gasbag burning out, CM14 (C2B/M04) | In the original, fly Clash of Dreadnaughts and set the Gemini's gasbags alight, holding one bag in frame from ignition through to whatever ends it, close enough to read both the fire and the envelope under it. *Look for:* what a finished gasbag looks like (a collapsed or missing envelope, a scorched one that stays, or a fire that simply stops), and what the zeppelin does once **three of its five** have finished, which is the authored death gate (`all_gmzep_gasbags` carries `MINIMUM_TO_SATISFY 3` over the five `finish_gmzepgasbagN` animations). Qualitative only: no burn duration is read off this clip as a constant | `BL-639` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### AI flight — an AI aircraft flying itself, external view

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-37` | An AI aircraft flying a patrol/attack loop, unprompted by the player | An AI-controlled aircraft in external/chase view, held long enough to cover a sustained turn, a low-speed moment and a patrol leg's end, with the player's own aircraft in frame where possible for a same-shot comparison. Behavioural and comparative questions only, **no absolute distances or speeds read off this footage** (`docs/verification.md`; a decode is never contested with a footage-derived measurement): does it gain altitude through a sustained turn or hold it; is its turn tighter or wider than the player's in the same airframe; does it hold a speed through manoeuvres or bleed and recover like a lever-driven aircraft; does it wallow at low speed or stay crisp; what does it do at the end of a patrol leg | The AI-side at-the-controls verdict for waves C and E |

---

## 1 · Actionable now (`PT-nn`)

### C1 · Campaign — the mission-end result carry (`BL-622` `B12`)

```powershell
./RunGame.ps1
```

- `PT-88` `[Own]` **B12: the mission's result reaches the launchscreen side intact.** From the
  menu, start or continue a campaign profile and fly CM01 (C1's first mission) once to a win and
  once to a loss (bail or let the wingman/objectives fail it). *Look for:* the console prints
  `campaign: <Outcome> — returning '<profile>' to the cabin` when the world leaves, then
  `campaign: <Outcome> — arrived at the debrief with '<profile>'` one frame later at the
  launchscreen side, with the same outcome in both lines. A mismatch or a missing second line
  means the mission-end result carry is broken; what the debrief screen itself shows is `PT-89`.
- `PT-89` `[Own]` **C17/D18/D19/D20: the scrapbook opens on the flown mission, not the cabin, with
  its own shipped scraps under the stat card, each one opening into its own detail view, and the
  page/mission arrows and Current Mission bookmark browsing the rest of the book.**
  Continuing the same flights, after each ending's hold the screen that opens is the scrapbook
  (`SB_BackGround` behind a stat card), not the cabin. *Look for:* the outcome line and the four
  rows read the just-flown attempt (win → Mission Completed, loss → Mission Failed, with that
  flight's own time/hit-ratio/cash), any airframe downed carries a kill stamp with its name and
  count, mission-specific scraps (CM01: a coin, a magazine and a newspaper masthead) show behind the
  stat card at their own positions, stepping the cursor onto one of those scraps and confirming
  opens a full-screen detail view (a family background, the scrap's own picture, and up to three
  lines of raw `IDS_SB_...` text since the original English was never shipped) that RETURN closes
  back to the same page, the forward arrow steps onto CM01's second spread (its story page) and
  keeps stepping into later missions' own pages, the back arrow returns the same way and offers
  nothing at the very front of the book, stepping away from the flown mission's page shows a Current
  Mission bookmark that jumps straight back to it and disappears once there, REPLAY MISSION acts on
  whichever page is currently shown rather than always the flight just flown — fly the win case, let
  Next Mission's position advance on the cabin, browse back to the flown mission with the bookmark,
  then confirm Replay Mission still targets it and not the new current one — and RETURN TO CABIN
  lands on the cabin with that session's own state (funds, Next Mission). A wrong mission on Replay,
  stale numbers, a missing stamp or scrap, a scrap that will not open, an arrow or the bookmark
  appearing where it should not (or not appearing where it should), or landing straight on the cabin
  means the debrief screen is broken.

### C1 · Bloodhawk — the overcast sky, ground to above the deck

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1
```

- `PT-47` `[A/B: both C1 IA1 Fog stills + CAP-12]` **The overcast-match plan's exit verdict**
  (`OriginalScreenshots/C1 IA1 Fog river.png`, `.../C1 IA1 Fog above clouddeck.png`,
  `playtest/CAP-12/`; the matching work is complete,
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
  - (f) **the in-band flicker** (`BL-329`) — hold still a few
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

### C1 · Bloodhawk vs AI — the kill sequence, sound on

```powershell
./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury --ai-attack=9 --volume=1.0 --no-det
```

⚠ `--volume=1.0` is not optional: the default master volume is 0, so a run without it is silent for
reasons that have nothing to do with any of these checks.

- `PT-77` `[A/B: OriginalScreenshots/Videos/Enemy AI Shotdown.mp4]` **A downed AI's wreck flies its
  FROZEN commands.** The wreck flies itself for three seconds after the airburst, until the destroy
  def's `Callback 15` releases it to the anim. What it flies with changed: the commands now freeze at
  the pilot's last values rather than being neutralised, which is what the decode says and which
  measures 344 m downrange against the 143 m it used to travel. That number deliberately moves *away*
  from the ~175 m a reference recording gave, because a footage-derived distance may not contest a
  decode (`docs/org/flightModel.md`); this sortie is the judgement that number cannot supply.
  *Look for:*
  - (a) the three-second fall reads as a wounded aircraft carrying its momentum, not as a hull
    being flung. It is faster and travels further than it did;
  - (b) ⚠ the one way freezing could be wrong: an aircraft killed **mid-turn** keeps its stick
    deflected, so it keeps turning. The recordings show the wreck holding its `x`/`z` heading. Kill
    one in a hard turn and one in level flight and compare;
  - (c) the second, smokier explosion reads as a separate beat downrange rather than landing on top
    of the airburst.
  *Blocks:* the at-the-controls AI-damage verdict. A
  fail on (b) is not a reason to reinstate neutralised commands: it points at whether the original
  zeroes an AI's stick on death, which is undecoded and would be a fresh item.

- `PT-78` `[A/B: OriginalScreenshots/Videos/Enemy AI Shotdown.mp4]` **The bailed pilot hangs level
  for the whole descent.** The chute is placed at 3.0 s and used to freeze at whatever attitude the
  wreck held at that instant, leaving the canopy edge-on and standing on its side. It is now placed
  level, which is what the airframe data authors (`chuteman`/`chutemanparent` are `transform:
  "Initial"`). The golden pins one frame of this; the descent is what it cannot see.
  *Look for:*
  - (a) the canopy is a dome over the man from the moment it appears until the ground, never
    edge-on and never rotating into a wrong pose a few seconds in;
  - (b) the authored 4 s drive and the sway still read as a drift, so it does not look pinned;
  - (c) a fidelity question rather than a defect: **every chute now has identical yaw**, because the
    authored template is identity. Shoot down two or three and see whether a formation of canopies
    all facing one way reads wrong.
  *Blocks:* the at-the-controls half of `D26`. (c) failing is a decode question to reopen, not a
  bug: the identical yaw is what the data says.

- `PT-79` `[Own]` **A shot-down player leaves a wreck, and its hull flies your last stick.**
  Get yourself killed rather than killing. `player-player` is the one destroy def that does not fall
  as an intact hull: it breaks into four separately flown pieces with a cockpit eject.
  ⚠ Decoded for an AI, assumed for a human: the decode shows an AI's commands stop being written
  because the AI think loop is what death skips, but whether the original also stops reading a
  human's stick is not decoded, and both paths share `StepWreckFall`.
  *Variations:* to be shot at rather than shoot, fly into a wing of them —
  `--ai=player_pfighter,player_pfighter,player_pfighter --ai-attack=9`.
  *Look for:*
  - (a) other pilots see a wreck at all, rather than the aircraft vanishing or hanging in the air;
  - (b) all four pieces appear and fly their own arcs, and the pilot ejects;
  - (c) the case the assumption bears on: die while **holding full deflection** and watch whether
    the hull's motion reads plausibly or as a spiral nothing authored.
  *Blocks:* nothing tracks (c) — a fail mints a fresh item against whether the original keeps
  polling a dead player's input.

- `PT-80` `[Own]` **Damage stages read HEALTH only, and armour hides nothing behind it.** The
  original divides health alone at both the def level and the per-part level, armour never entering
  either quotient, and a part whose armour still covers the hit takes no health damage at all, so an
  armoured zone should cross no threshold whatever.
  *Variations:* take the fire rather than give it —
  `--ai=player_pfighter,player_pfighter --ai-attack=9`.
  *Look for:*
  - (a) nothing at all shows on a zone while its armour is still absorbing, the 0.99 spark shim
    included; that shim going quiet on early hits is the most visible change here;
  - (b) a panel tears only once that zone's own health crosses its threshold, not before;
  - (c) `player_damage_trail` starts when the hull gauge reads about 10%, not earlier.
  *Blocks:* the owed flight A/B for the staging scope and pool fixes. ⚠ The gauge dial is a separate
  scale and is *correct* on combined armour+health, so a dial that disagrees with the staging here
  is not a fault.

- `PT-84` `[Own]` **A graze no longer drops the engine to a sputter.** The engine slot swaps to
  `snd_damagedengine` only once the worst zone falls below a quarter of its health, which is the
  original's own threshold and not a tuned one, so light damage should sound completely healthy and
  the swap when it comes should be a hard cut rather than a fade.
  *Variations:* take a beating rather than give one, with
  `--ai=player_pfighter,player_pfighter --ai-attack=9 --volume=1.0`. To hear the two states back to
  back without combat, `--fly --damage=nose:0.30` then `--fly --damage=nose:0.20`.
  *Look for:*
  - (a) the first hits, and a scrape along a cliff, leave the engine note untouched;
  - (b) once a zone is deep into the red the note drops and stays dropped, at one pitch rather than
    drifting;
  - (c) the drawn pitch is not always the same on a fresh flight, and can land anywhere from a
    near-normal note to a barely-there rumble.
  ⚠ The pitch is a uniform draw with nothing about the damage in it, so a run that lands a mild
  multiplier is not evidence the gate is wrong. Judge (c) across several flights.
  *Blocks:* nothing; a fail on (a) says the gate reads the wrong pool, a fail on (b) says something
  is re-evaluating the swap per frame.

- `PT-81` `[Own]` **A repair retracts the stage it lifted back over (F5 damage lab).** Staging used
  to latch one way; the original stops an entry's anim and clears its handle on the upward crossing.
  This one needs no AI, so it flies on the bare command:
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk
  ```
  *Look for:* open the F5 damage lab, drag a zone down past a threshold to start its stage, then
  repair back above it and watch the stage stop. Un-staging on repair is faithful, not a regression.
  *Look for also:* it must fire ONCE per downward crossing. A stage that re-fires while the fraction
  merely stays below its threshold is a different bug, not this fix working.
  *Blocks:* the owed lab check for the staging lifetime fix.

- `PT-82` `[A/B: OriginalScreenshots/Videos/Bloodhawk Dive Sound.mp4]` **The engine note moves with
  the stick and with the climb, not with the throttle alone.** Two decoded terms landed on the engine
  slot: an unsigned transient on pitch and yaw rate, and a level that falls through a dive and rises
  in a climb. The note was previously a function of the throttle lever and nothing else, so at a
  fixed throttle it was flat whatever the aircraft did. This one needs no AI:
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk --volume=1.0 --no-det
  ```
  *Look for:*
  - (a) hold the throttle fixed in level flight and pull, then push. The note rises **both** times.
    That it rises on a push is the surprising half and is what the decode says;
  - (b) a sustained vertical dive at fixed throttle sits about 6% below level, and returns to level
    when you pull out rather than staying down;
  - (c) roll hard at fixed throttle. The note must **not** move at all: the original drops the
    nose-axis rate, so a barrel roll is silent where a pitch input is not.
  *Blocks:* nothing tracks this; it is the last thing owed on the engine note now that the mechanism
  is decoded and pinned (`git log --grep=BL-109`, `--grep=BL-423`). The numbers already match the
  original to within a third of a point, so a fail here is about whether it READS right, not whether
  it matches. ⚠ Do not ask for a re-tune of the coefficients on a listen: they are read from the
  executable, and `docs/verification.md`'s rule that a recording may not contest a decode applies to
  the ear too.
  ⚠ **This is probably what the retired `BL-123` was hearing** (`git log --grep=BL-123`). That item
  A/B'd our dive whine against the original and reported it "close, but could be a bit louder",
  which read as a mix problem. The original has no whine at all, so what the ear was matching in
  that dive was the engine slot's own movement, and these terms are what produces it.

### C1 · two pilots — Dogfight (splitscreen VS)

```powershell
./RunGame.ps1 --vs --players=2 --chapter=C1
```

- `PT-52` `[Own]` **The puffer distance fade in two panes (`BL-339` landed 2026-08-15).**
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
  *Blocks:* the fidelity verdict for the nearest/union boundary rule — per-pane alpha (one MultiMesh per pane) is
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

- `PT-45` `[Own]` **The abreast race starting grid** (landed 2026-08-08;
  it closed `BL-084`, whose record is in that commit — `git log --grep=BL-084`).
  Two pads (or pad + keyboard); menu path: Stunt → C1 → both press Start. Splitscreen stunt racing is
  our invention — the original had no splitscreen at all — so every call here is a judgement on our
  own remake, with no reference to A/B against.

  **Why this sitting is the only evidence there will ever be for a race.** On the race path the grid
  is selected only when a session is an actual race, and a `--det` run is explicitly given the old
  per-player spawn walk instead, so no scripted run, screenshot or golden can exercise the race
  path. That is by design: the bypass is what keeps every scripted race spawn byte-identical. (A
  multiplayer campaign mission takes the same grid under `--det` as well, so a scripted campaign run
  does place a field with it; that is a different caller and settles nothing about a race start.)
  The grid geometry is also not
  photographable: the panes are chase-cam only, so at the default 60 m spacing your neighbour sits
  outside your own frustum. **Read the geometry off the console instead** — every launch logs one
  line per slot, e.g. `spawn [P1 grid slot 1 of 4] pos=(-4974,260,-3771) heading=90° spacing=60m
  lift=81m`, with the anchor's own line above them. On C1 with `--spawn=0` the field lifts 81 m.

  **Both numbers are live config, and settling them is the point of this sitting.** `slotSpacing`
  (default **60 m** between neighbouring slots) and `groundClearance` (default **100 m** of air the
  lowest slot must have under it) are read from `config.json` as `startGrid.slotSpacing` and
  `startGrid.groundClearance`, listed by `--dump-config`, and take effect on the next launch with no
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
  instead (C1/C2B/C4/C1C = `zone2`), not by a fresh flight.
  *Variations:* one flight each — repeat with `--chapter=C3`, then `--chapter=C2`; (d)'s four
  untouched chapters need only a glance in each.

### C5 · freecam — the downtown mip band

```powershell
./RunProbe.ps1 --freecam --chapter=C5 "--pos=-9491,140,-3479" "--direction=-0.588,-0.03,-0.809"
```

- `PT-85` `[Own]` **At what distance C5's downtown is meant to drop to mip level 1** (`BL-538`).
  The dark band across the large buildings is **not** the clutter fade, and the fade is not on
  trial here: the pair proves the band is the shipped hand-authored mip chain, whose level 1 is a
  non-monotone dip (`cblock1` mean luminance 15.62 at L0, 4.41 at L1, 6.77 at L2; `cblock2` 9.60,
  0.83, 1.84), so the original's own art draws a darker ring at the distance L1 takes over.
  `--mips=generated` removes the band and is the A/B to flip against.
  What to judge, in motion rather than in a still: whether the ring sits at a distance that reads
  as the original's downtown falling away, or too near the camera. Fly the city rather than hover,
  since the band moves with you and a hover cannot show whether it tracks convincingly.
  ⚠ Do not judge this against a capture of the original: the mip levels themselves are the
  original's art and are not in question, only the distance at which the remake reaches them.
  Two sub-questions ride along: whether the original point-selected a mip where we trilinearly
  blend L0 into L1 across a range, and whether the 640×480 to 1280×720 change moves the band.
  *Blocks:* `BL-538`.

### Any chapter · a nitro engage, external view

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1
```

- `PT-86` `[Own]` **A nitro engage bursts exhaust smoke and kicks the camera** (`BL-546`, closed).
  The engage edge was cleared by the tank update before anything read it, so the boost
  accelerated with no smoke, no shake and no loop sound. The edge now survives the step, and a
  suite on a rig built the production way sees all four `nitropuffN` emitters live for the
  authored second. Fly an airframe with a nitrous engine (hangar engine ids 3 to 5), press N from
  a full tank.
  *Look for:*
  - (a) a puff at all four exhausts on the engage, reading as a one-second burst rather than a
    continuous trail (each puffer is authored ACTIVE with its own INACTIVE one second later);
  - (b) the engage shakes the camera; the decode says the player's engage shakes (shake block 6
    with the nitro magnitude) but not how hard, so judge the magnitude;
  - (c) an AI aircraft on a nitro maneuver now shows the same burst, since the same edge was dead
    for it.
  ⚠ **Do not judge the propeller discs.** The original resolves the anchor per vehicle
  (`LOCAL_NODES_ONLY` on both nitro defs), so the `nitropropN` discs never showed on a flyable
  airframe there either; their absence is not a regression.
  *Blocks:* nothing tracks the outcome; a wrong shake magnitude or a missing burst mints a new
  `BL`.

### CM08 (C1B/M03) · the patrol boats, targeting on

```powershell
./RunGame.ps1 --campaign=<profile>:7
```

- `PT-87` `[Own]` **A patrol boat brackets on the HUD and takes a gun lock** (`BL-598`).
  The four boats drove their nets and took hits but could not be targeted, because a surface
  vehicle reached none of the candidate lists. The decode puts it on `VehicleList`, the same list
  the aircraft roster feeds, and a suite asserts a woken hull is live on the candidate set, lands
  on the Enemy cycle and wins the aim-assist scan. Nobody has seen it on screen. Fly CM08 past
  the 181 s wake and cycle targets: a boat should bracket like any other target and the gun
  should snap to it.
  The candidate carries the hull's own velocity, so the gun line should sit AHEAD of a driving
  boat by about its speed times the round's flight time, not on the hull. A lead that sits on the
  hull is a fault; a boat mid-turn is led less, which is not one.
  *Blocks:* nothing; it confirms a landed item on screen.

- `PT-109` `[Own]` **The Pandora halts over the beached freighter and cranes its cargo**
  (`BL-597`). Klondike1's node 7 is the cargo point, and the hull now settles on the node itself
  rather than a hold distance short of it. Getting there is the mission's own chain: destroy the
  power hut so the freighter runs aground instead of sailing on, then sink all four patrol boats,
  which completes the second primary and releases the Pandora from its first stop.
  *Look for:* the airship arriving over the wreck and stopping with the ship under it rather than
  short of or past it, and about 70 s after the last boat goes down the freighter's hold doors
  swinging open, the Pandora's cargo doors following, and the crane riding its chain down to the
  deck and back up. Judge the hover point by eye against the hull below it.
  ⚠ Nothing starts until group 3 is empty. A Pandora sitting 2 km up the route with no sequence is
  the authored wait, not a fault; check the boats first.
  *Blocks:* `BL-597`'s acceptance.

### AI flight — external view, own build (F52 AI arm)

- `PT-54` `[Own]` **AI plant A/B against the old plant.** Fly the new AI force path, then relaunch flipping AI aircraft back onto the player plant
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

- `PT-57` `[Own]` **Low-speed handling across `BL-330`'s authority-ramp extremes, judged against
  `BL-330`'s existing corroboration.**
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

### C1 · two pilots — the victim-routed screen wash

```powershell
./RunGame.ps1 --coop --players=2 --chapter=C1 --debug-wash=2
```

`--rocket=<wep_id>` (used here and below) swaps every pylon to the named type. All 11 stock
loadouts fit HE `wep_06`, so a flag is what pins a type here rather than the menu's own Ammo
Selection screen: the flag beats a menu-chosen fit deliberately, so a row that names a type gets it
whatever was clicked on the way in. The rocket trigger is **F**, one round per pull.

⚠ **Every figure quoted in this section and the ones below is a decoded constant or an authored
value.** A clip that disagrees with one is evidence about our implementation, never a correction to
the constant: a decode is not contested with a measurement read off a running picture
(`docs/verification.md` DET-12).

- `PT-74` `[Own]` **The wash is addressed to the viewer who was hit, and blends
  (the routed wash contract).** The original holds one wash state for the whole
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

  *Blocks:* nothing tracks the outcome; (a)/(b) passed at the controls and only (c) is still owed.
  a fail on routing mints a new
  `BL`.
  *Variations:* `--debug-wash=3` in a two-pane session, which answers to no pane and must paint
  nothing at all.

### C1 · four pilots — the four-viewer ordnance pass

```powershell
./RunGame.ps1 --coop --players=4 --chapter=C1 --rocket=wep_08 --infinite-ammo
```

Four viewers is the case the wash channel was designed against: the original routes its wash
through a single global and that does not survive four panes. ⚠ `BL-389` already reports that the
splitscreen weapon mix wants a retune, with rockets too quiet against guns and worst with four guns
firing at once. **A mix problem is not a behaviour problem**: judge what happens, and file loudness
against `BL-389` rather than against the wash routing.

- `PT-75` `[Own]` **The disabling types with four viewers on one team.**
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

  *Blocks:* nothing tracks the outcome: a fail on routing mints a new `BL`; a fail on loudness is
  `BL-389`.

- `PT-76` `[Own]` **The same pass under Dogfight rules, four viewers hostile.** `--coop` puts every human on one team, so the beeper's hostility
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

  *Blocks:* nothing tracks the outcome: a fail mints a new
  `BL`.
  *Variations:* `--players=2` and `--players=3` for the intermediate pane counts, which is where a
  routing off-by-one would show.

### CM01 (C3/M01) · two to four pilots — join, flight check, death and skip

```powershell
./RunGame.ps1
```

Menu path: Mode → Campaign → create or select a profile → join two to four humans → the seated
pilot's own briefing and flight check, then `FLIGHT CHECK P2`/`P3`/`P4` in the same window → FLY
MISSION. A fresh profile's first entry is CM01, so this is also where the sortie itself is flown.
Splitscreen co-op is our own invention (no networked original to A/B against), so every call here
is a judgement on our own remake.

- `PT-90` `[Own]` **Joining from the roster screen and the sequential flight check.**
  *Look for:*
  - (a) Start on an unclaimed pad joins a guest from the roster, the briefing and the flight check
    alike, with the player chip strip naming everyone who has joined;
  - (b) FLY MISSION on the seated pilot's own check opens `FLIGHT CHECK P2` rather than launching,
    and does the same for P3 and P4 as each joins — one window, one player at a time;
  - (c) FLY MISSION on the LAST joined player's check is what actually launches the mission;
  - (d) B on a guest's pad drops that guest back out (roster, briefing or flight check alike)
    without disturbing P1's own flow, which keeps its ordinary Back/Next meaning throughout.
  *Blocks:* nothing tracks the outcome (`C21`/`C22` landed on screenshots and code review alone,
  with no scripted-input driver for a pad press on a menu screen): a fail mints a new `BL`.

- `PT-92` `[Own]` **A downed human spectates, and the last one lost ends the mission.**
  Fly two humans into CM01 and crash one — into the sea or a hillside, `R` to restart the pane if
  the first attempt is too gentle to register as a loss.
  *Look for:*
  - (a) the downed human's pane switches to an orbiting spectator camera on their own wreck, not a
    frozen frame or a black pane, and the other pane keeps flying with its own HUD untouched;
  - (b) that spectating pane takes its own pad's input for the orbit, so a second downed human (at
    three or four players) does not orbit in lockstep with the first;
  - (c) crashing the LAST human still flying ends the mission on a loss the instant no wreck is
    still falling — that human is not handed a camera, since there is nothing left to watch;
  - (d) `--no-crash-loss` (`--campaign=<profile>:0 --players=2 --no-crash-loss`) turns all of this
    off together: a crashed human keeps flying, no pane is taken and nothing ends.
  *Blocks:* nothing tracks the outcome (`B13` landed on an engine suite alone, with no scripted
  input to force a specific human's crash headlessly): a fail mints a new `BL`.

- `PT-93` `[Own]` **A cutscene fills the window, and the skipper is named.**
  CM01's own mission intro plays fullscreen on launch. Two pads (or pad + keyboard); have the
  SECOND player skip it, then relaunch and have the first player skip instead.
  *Look for:*
  - (a) the intro collapses to one pane filling the whole window, letterboxed, with no gutter and
    no second pane drawn behind it;
  - (b) any key or pad button (not Escape) skips it, and the on-screen name is the SKIPPER'S,
    in that player's own colour — P2's skip must never read as P1's;
  - (c) the skip (or the intro's own end) restores every pane and HUD exactly as they were before
    the collapse, with nobody's pad input lost in the process.
  *Blocks:* nothing tracks the outcome (`B14` landed on an engine suite and a code-review pass at
  the controls, with no scripted-input driver for a pad-button skip): a fail mints a new `BL`.

### CM02 (C3/M05) · guest capture — the Balmoral wing-walk

```powershell
./RunGame.ps1 --campaign=<profile>:1 --players=2
```

- `PT-91` `[Own]` **Whichever human triggers the capture ends up in the captured aeroplane.** Fly both humans to CM02's wing-walk rescue, and have
  the GUEST (not P1) be the one to fly into the trigger.
  *Look for:*
  - (a) the guest, not P1, is the one re-flown into the Balmoral once the cutscene ends — the swap
    follows whoever triggered it rather than always landing on P1;
  - (b) the OTHER human's aircraft and position are undisturbed for the whole cutscene, not stacked
    onto the capture point;
  - (c) the newly-captured Balmoral shows its own hook and wing-fold choreography rather than
    Bloodhawk/Fury geometry left over from the airframe the guest flew in;
  - (d) relaunching and letting P1 trigger it instead puts P1 in the Balmoral and leaves the guest's
    own airframe alone — the same behaviour, the other human.
  *Blocks:* nothing tracks the outcome (`A4` landed on an engine suite alone, with no scripted-input
  driver to fly a human into a world trigger headlessly): a fail mints a new `BL`.

- `PT-97` `[A/B: OriginalScreenshots/Videos/CM02.mkv, 276 s to the cut]` **The docking's opening
  shot shows one hook swing from a parked start** (`BL-628`, closed). The arms are parked by the
  airframe's own retract RESET_STATE, which the flown aircraft never received; the montage of film
  against before and after is in `.scratch/m5p8-a5-evidence/`. Fly the mission to its docking in
  one pilot and watch the opening shot alone.
  *Look for:*
  - (a) no arms visible above the mount for the first second while the doors open;
  - (b) one growth from nothing to full length over the next second, then one outward swing;
  - (c) no second swing and no collapse-then-regrow; if a repeat is still visible it is on the
    shared fork rather than the airframe branch, which the suite does not cover;
  - (d) in a Balmoral, whether the arms' authored step from full to half scale at one second reads
    like the original (its retract authors no scale, and no Balmoral docking is on film).
  *Blocks:* nothing tracks the outcome; a fail reopens the symptom as a new `BL` against the fork.

### Any chapter · the campaign export, then Instant Action

```powershell
./RunGame.ps1
```

Menu path: Mode → Campaign → a profile with a mission open → its flight check → CHANGE AMMO, fit
something unmistakable (a distinct round on gun 1, a distinct rocket on a pylon) → back → CHANGE
PLANE → EXPORT on the pilot's slot → ACCEPT SELECTIONS → leave to the main menu → Instant Action →
pick that plane by name, after the eleven stock airframes. The export writes ammunition and
ordnance that only the campaign fits, so the round trip is the only place the two sides meet.

- `PT-96` `[Own]` **An exported campaign plane flies Instant Action with the loadout the campaign
  fitted.**
  ⚠ Do not open the Instant Action loadout screen: an explicit pick there overrides the exported
  one by design, so opening it hides exactly what this checks.
  *Look for:*
  - (a) EXPORT answers with a dialog naming the airframe, not the plane ("Your Devastator has been
    exported"), which is what langui 702's placeholder carries;
  - (b) the plane appears in the Instant Action picker under its campaign name;
  - (c) the gun rounds and the underwing ordnance in flight are the ones the campaign fitted, not
    the airframe's stock fit;
  - (d) a plane exported twice keeps its paint, armour and engine, and only its loadout moves.
  *Blocks:* nothing tracks the outcome. The write and the read are pinned by unit tests either
  side, but no scripted run crosses the menu boundary between them, so a fail mints a new `BL`.

### CM05 (C3/M04) and CM07 (C1/M02) · the escorts after a leader is lost

```powershell
./RunGame.ps1 --campaign=<profile>:4
./RunGame.ps1 --campaign=<profile>:6
```

- `PT-113` `[Own]` **A wingman whose leader is shot down picks up that leader's patrol route**
  (`BL-524`, closed). CM05 pairs `wingman_2` with `devastator_1` on `M4Bravo#11` and `wingman_3`
  with `devastator_2` on `M4Charlie#12`; CM07 pairs `wingman_2` and `wingman_3` with
  `devastator_2` and `devastator_3` on `M2Bravo#26` and `M2Charlie#25`. Fly until one of those
  leaders is destroyed, then keep the surviving wingman in sight, above all after its own target
  dies, which is the moment it used to leave on a fixed bearing.
  *Look for:*
  - (a) the survivor turning onto its lost leader's circuit and staying in the mission area,
    rather than shrinking into the distance on one heading at one altitude;
  - (b) `campaign: 'wingman_2' lost its leader 'devastator_1' and takes its net 'M4Bravo#11'` in
    `.scratch/logs/fly-*.log`, once per lost leader rather than every frame;
  - (c) the survivor still fighting from the inherited route: it engages what comes into range
    instead of flying a patrol past a fight.
  *Blocks:* nothing tracks the outcome; a wingman that still flies out of the mission reopens
  `BL-524`, and one that holds its net while enemies shoot at it is `BL-523`, not this.

### CM09 (C1/M04) · to the docking

```powershell
./RunGame.ps1 --campaign=<profile>:8
```

- `PT-98` `[Own]` **CM09 goes on to the docking once the tower is down and the aircraft are
  killed** (`BL-581`, disproved on the world side). The objective chain closes headless to
  OBJECTIVE31 and the `campaign-cm09-docking` suite pins it, so what remains is whether a flown
  mission reaches the step the chain needs: OBJECTIVE28 wants three of the five `M4ZepAttack`
  bloodhawks down before Blake's squad arrives, and they wake 2.2 km north heading for the
  Klondike rather than for the player. Fly it on the tower-down route and kill everything you
  meet, then read `.scratch/logs/fly-*.log`.
  *Look for:*
  - (a) `[campaign] objective 28 completed` in the log; present means the chain moved and the
    earlier stall is not in this build, absent means the five bloodhawks were never cut to two;
  - (b) the Klondike drawn and intact after the intro hands off, since the whole endgame gates on
    that hull being switched on;
  - (c) the docking cutscene starting on its own once the last hostile dies.
  *Blocks:* an absent line reopens `BL-581` as an AI-reachability question, not an objectives one.

- `PT-115` `[Own]` **A gasbag-only kill of the pirate zeppelin ends CM09, after about half a
  minute of nothing.** Shooting the hull the briefing says to defend reaches OBJECTIVE41's
  `INSTANTLOSS` rather than leaving the mission running: the burning bays and `killpzep`'s
  water-gated engine destroys switch off all twelve engine `healthy` models, which is what
  OBJECTIVE26 and OBJECTIVE27 count. The `campaign-cm09-gasbag-kill` suite measures 32.6 s from
  the kill to the loss, and 30 s of that is the two authored naps (26 naps 27 for 10 s, 27 naps 41
  for 20 s), so the wait is the design rather than a stall. Those are sim seconds, so a rig whose
  sim clock lags wall time waits longer. What no headless run can say is whether that wait reads
  as a hung mission at the controls. Kill three gasbags on the `piratezep`, then keep flying and
  watch the clock instead of quitting.
  *Look for:*
  - (a) the mission-failed screen arriving about 33 s after the hull dies;
  - (b) whether those 30 s of silence read as a hang, which would be a pacing item rather than a
    chain one;
  - (c) the wreck's own breakup still playing through the wait, since it is the only thing on
    screen that says the mission is still running.
  *Blocks:* nothing tracks the outcome today. An ending that never arrives mints a new `BL` for
  the chain; an ending that arrives but reads as a hang mints one for the wait.

### CM10 (C1/M05) · the docking's walkway and the hospital ship

```powershell
./RunGame.ps1 --campaign=<profile>:9
```

- `PT-99` `[Own]` **The letterbox bars stay on top of the walkway** (`BL-631`, closed). The card
  is drawn with no depth test at the top render priority, unshaded in its authored black. The
  film ends before the walkway crosses, so this is the only check. Watch the closing docking as
  the walkway passes between the camera and the bars.
  *Look for:*
  - (a) the bars stay solid black over the walkway and every other thing the shot flies past;
  - (b) the bars themselves are the same black as before, with no lighting or fog on them and no
    thin line where the card's two faces meet.
  *Blocks:* nothing tracks the outcome; a fail mints a new `BL`.

- `PT-100` `[A/B: OriginalScreenshots/Videos/CM10.mkv, the patrol boats shortly after the start]`
  **Enemy guns engage the hospital ship, and the player's own do not** (`BL-626`, `BL-664`). The
  Red Cross ship is a mission structure on the player's side, so the decode predicts the balloon
  turrets and a dropped lifeboat's gun fire on it while a player-team gun never does. Shoot an
  attack balloon down over the water so its lifeboat reaches it, with the ship in frame.
  *Look for:*
  - (a) balloon turrets and the lifeboat's gun tracking and firing on the ship, as the film shows
    the patrol boats doing;
  - (b) a patrol boat that closes on the ship and does not fire is the AI mode machine
    (`BL-523`), not this item, so note it without minting;
  - (c) the player's own rear gunner or an escort never firing on the ship, and the hospital ship
    never appearing on the Enemy target cycle.
  *Blocks:* nothing tracks the outcome; a gun firing on the wrong side mints a new `BL` against
  the structure-team decode.

- `PT-111` `[Own]` **A wave-1 attack balloon's marker stays legible as the wave arrives, never
  reading as parked on the water** (`BL-656`, disproven). The marker already tracks the group's
  live geometry with no staleness; the wave's own scripted entrance dives from a hidden altitude
  to a low pass over the water, roughly 11-14 m under the marker, before climbing to attack
  height, and that low pass is the one moment worth eyeballing. Watch OBJECTIVE10 wake (the first
  attack-balloon site to appear) through its whole entrance.
  *Look for:*
  - (a) the marker sits visibly above the water throughout, including at the low pass;
  - (b) nothing reads as the marker resting on or under the water surface;
  - (c) the marker's motion looks continuous through the dive and the climb, with no snap or jump.
  *Blocks:* nothing tracks the outcome; a marker that does read as resting on the water reopens
  `BL-656` against the entrance flight's own authored path rather than against `ObjectiveSites.cs`.

### CM11 (C2/M02) · the stunt planes' objective marker

```powershell
./RunGame.ps1 --campaign=<profile>:10
```

- `PT-117` `[Own]` **Both stunt planes carry the objective marker and its Follow label from
  mission start** (`BL-635`). `secfury_5` and `secfury_6` author the roster's own objective flag
  and label (aiv slots 37/39) rather than a `targets.zrd` entry, and each marker now reads off the
  aircraft's own live position instead of a world node. Fly the mission's opening over the studio
  lot with both stunt planes in view, then continue to wherever the studio gate objective
  completes.
  *Look for:*
  - (a) both planes carry a marker labelled Follow as they perform their routine;
  - (b) the marker moves with each plane rather than sitting fixed at a spawn point;
  - (c) once the gate objective completes, both markers clear rather than lingering on a plane
    that has flown off or been shot down.
  *Blocks:* nothing tracks the outcome; a missing marker, a wrong label, or one that lingers past
  the gate objective reopens `BL-635`.

### CM12 (C2/M01) · the patrol boats and the Spruce Goose

```powershell
./RunGame.ps1 --campaign=<profile>:11
```

- `PT-101` `[Own]` **The patrol boats engage the Goose as it passes** (`BL-626`, `BL-664`). The
  Goose's engines are structures on the player's side, so an enemy boat's turret now acquires
  them at detection range. Fly beside the Goose as it passes the `eshipg31` boats.
  *Look for:*
  - (a) the boats' turrets slewing onto the Goose and firing as it passes;
  - (b) the Goose taking damage on its engines rather than nowhere;
  - (c) a boat that never fires at all, which is `BL-523`'s gunnery and not this item.
  *Blocks:* nothing tracks the outcome; a fail mints a new `BL`.

- `PT-107` `[Own]` **The ace `hkfirebrand_9` after `OBJECTIVE67` wakes it** (`BL-566`). The ace is
  authored at `(-4518, 150, -6233)`, which is 78.9 m below the terrain surface there, so it starts
  flying inside the hill. Nothing was changed, so this row records what the build does rather than
  checking a fix. Fly on until the Hollywood Knight ambush wakes, then watch the ace on the map and
  in the air.
  *Look for:*
  - (a) whether the ace is ever visible in the air at all, or only ever inside the hill east of the
    Goose's harbour leg;
  - (b) the mode readout alternating pursue and avoid crash while it is in there;
  - (c) it dying against the terrain without the player firing, and whether the mission still reads
    correctly afterwards (the secondary counts group 2 as wiped).
  *Blocks:* what the original does with an aircraft authored inside terrain, which decides whether
  the fix is a placement rule or a collision one.

### CM13 (C2/M03) · the Pandora's dock

```powershell
./RunGame.ps1 --campaign=<profile>:12
```

- `PT-108` `[Own]` **Both landing cones draw once the dock finishes** (`BL-618`, disproven as
  filed). `pzhomebase` already binds each cone to its own gamez node and switches both on; the
  suite's earlier `cones 0/2` reading was its own drive window ending before the choreography's
  13 s completion, not a resolver defect. Win the race so OBJECTIVE8 wakes the dock, then give it
  time to finish deploying the hook.
  *Look for:*
  - (a) both cones under the Pandora's hull visibly drawing once the hook and hangar bay have
    deployed;
  - (b) either cone staying invisible, which means the disproof above does not hold at the
    controls.
  *Blocks:* nothing tracks the outcome; a missing cone reopens `BL-618`.

### CM14 (C2B/M04) · the Gemini's broadside cannons

```powershell
./RunGame.ps1 --campaign=<profile>:13
```

- `PT-110` `[Own]` **A stowed broadside cannon takes no weapon damage, and the same cannon takes it
  once deployed** (`BL-640`). The Gemini keeps all six left cannons behind shut hatches until the
  two airships close to 1000 m and its `COMPLETED_ZEPCANNONS` arms them, and only then does a hatch
  swing and the gun slide out. Fly abeam the Gemini's port side early, pick one `lbroad` hatch and
  empty a long gun burst into it; then hold off until that cannon deploys against you and put the
  same burst into the gun itself.
  *Look for:*
  - (a) the shut hatch taking hits with no smoke, no fire and no cannon death, so the objective
    counter does not move;
  - (b) the deployed gun dying to the same burst, with its fireball and the objective counter
    stepping on;
  - (c) how long the wait for the arming is, since the cannons are now immune until they deploy and
    the mission's third primary wants five of the six.
  *Blocks:* a stowed cannon that still dies reopens `BL-640`; a deployed one that will not die is a
  new item.

### CM15 (C2/M05) · the paratrooper drop

```powershell
./RunGame.ps1 --campaign=<profile>:14
```

- `PT-112` `[Own]` **The capture cutscene shows the Balmoral it is filmed around** (`BL-632`,
  closed). The archive's `balmoral` node was never staged, so the drop played with the shot
  composed on empty air; `AircraftStage` now stages it and the suite reads it framed through the
  shot, but the suite drives the definition directly rather than through the mission's own
  objective chain. Fly the mission through to the paratrooper drop (past `cargozep2`, the cargo
  zeppelin the fighters patrol around).
  *Look for:*
  - (a) the Balmoral itself visible in the shot, not an empty frame over the zeppelin;
  - (b) it moving through the shot rather than sitting parked;
  - (c) the paratroopers still dropping and the letterbox/handoff unchanged from before.
  *Blocks:* nothing tracks the outcome; a fail reopens `BL-632`.

- `PT-114` `[Own]` **The Spruce Goose reads smooth from the harbour leg** (`BL-627`). The authored
  path and the runtime that plays it both measure clean, so what is left to judge is the picture:
  the Goose's pose is written once per 60 Hz physics tick while the player's own aircraft, and
  therefore the camera, is drawn between its last two sim poses every rendered frame. Blow the
  Kowloon gate, then formate on the Goose and hold station beside it from the harbour leg out.
  *Look for:*
  - (a) the Goose stepping rather than gliding while the camera moves smoothly, worst when you are
    close enough for the hull to fill the view;
  - (b) whether capping the render rate to the physics rate removes it, which is the discriminator:
    fly the same leg again under `--max-fps 60` and say whether it reads better, the same or worse;
  - (c) the Goose slowing almost to a stop at the end of the first leg and picking up again, which
    is the authored `path2_decelerate` waiting on the barge and not a defect.
  *Blocks:* `BL-627`. A "smooth at 60, stepping at 120" answer is the confirmation that the fix is
  render interpolation for scripted world nodes; "stepping at both" reopens the diagnosis.

### CM18 (C4/M03) · the generator launches

```powershell
./RunGame.ps1 --campaign=<profile>:17
```

- `PT-102` `[Own]` **A generator launch no longer hitches, and an early kill still leaves a wreck**
  (`BL-641`, partial). The crash rig is built on the frames after the launch instead of on it,
  which halves the launch frame but does not reach the threshold. Fly the first minute, where
  `cargozep1` launches a Black Swan every four seconds.
  *Look for:*
  - (a) the launches felt at the controls: a lighter hitch than before, and whether what remains is
    still noticeable at the stick;
  - (b) a launched Black Swan appearing, flying and taking a lock exactly as before;
  - (c) a Black Swan shot down within its first quarter-second still showing its wreck and crash
    effects, which is the forcing guard no suite watches at the controls.
  *Blocks:* nothing; `BL-641` stays partial with its remaining path recorded in the plan. A missing
  wreck on an early kill mints a new `BL`.

### Any chapter · Instant Action zeppelin run

```powershell
./RunGame.ps1
```

Menu path: Mode → Instant Action → the zeppelin run, with a torpedo (`wep_14`) on a pylon; the
`BL-291` harness (`git log --grep=BL-291`) is the same rig headless.

- `PT-103` `[Own]` **A downed zeppelin pitches over, sheds six gasbags with splashes, and drops the
  gondola** (`BL-440`, closed). `NodeUndercover` is now a real vertical probe, so `killpzep`'s
  breakup runs once the hull is 65 m over the water. Torpedo the hull down over water and follow
  it.
  *Look for:*
  - (a) the hull pitching nose-down over about eight seconds, then easing as the break starts;
  - (b) six gasbags falling free with a splash and a ripple where each meets the water;
  - (c) the gondola dropping away;
  - (d) the wreck's rest: it comes to rest 400 m under the sea and four gasbags fall through the
    water (`BL-668`), which is already filed, so note how it reads rather than minting.
  *Blocks:* nothing tracks the outcome; a breakup that does not play mints a new `BL`.

### Any chapter · cockpit view, a circle against the tape

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1
```

- `PT-104` `[Own]` **The cockpit compass drum turns with the heading tape** (`BL-663`, closed).
  The drum takes the engine's own `-heading` about its Y axis, the sign settled by reasoning
  rather than on the panel. Switch to the cockpit view and fly a full circle each way.
  *Look for:*
  - (a) the drum and the tape at the top of the screen showing the same card and turning
    together, headings increasing to the left;
  - (b) the drum at rest reading north with the nose on the tape's north;
  - (c) both readouts agreeing with the world (the decoded compass north as world −Z is still
    unverified, and a wrong axis moves both together).
  *Blocks:* a drum turning the wrong way reopens `BL-663`; both readouts wrong together is
  `docs/formats/hud.md`'s open compass-north question and mints a new `BL`.

### Any chapter · Instant Action stunt run, failed then completed

```powershell
./RunGame.ps1
```

Menu path: Mode → Instant Action → a stunt mission.

- `PT-105` `[Own]` **A failed stunt run records nothing and announces no best** (`BL-426`,
  closed). Fail a run deliberately (miss a zone and let the run end), then complete one.
  *Look for:*
  - (a) the failed run's wrap-up shows no NEW BEST and `user://stunt_scores.json` is unchanged;
  - (b) the completed run records and the next wrap-up shows it as the best;
  - (c) two author calls: whether a failed run should show its elapsed time at all (it shows it
    now, with no flag), and whether existing `stunt_scores.json` entries poisoned by the old
    behaviour are to be invalidated, which no code change repairs.
  *Blocks:* the two calls in (c); each answer that changes behaviour mints a new `BL`.

### C1 · a named ace at a known difficulty

```powershell
./RunGame.ps1 --campaign=<profile>:8 --difficulty=1
```

- `PT-106` `[Own]` **A named ace fights at its authored durability** (`BL-557`, closed). Roster
  `init_health` and `armor` now reach the spawn ahead of the difficulty scale, and every shipped
  override raises the hostile above its airframe default (armour 90 to 132). Engage a named
  Blake-squad bloodhawk and an unnamed one at the same difficulty.
  *Look for:*
  - (a) the named ace taking noticeably longer to kill than an unnamed aircraft of the same type;
  - (b) the time-to-kill still feeling like a fight rather than a wall, which is what `BL-561`'s
    open research reads next at a known `--difficulty=`.
  *Blocks:* nothing; a wall mints a `[Tuning]` `BL`.

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
