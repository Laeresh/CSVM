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
the retiring commit's message (`git log --grep=<ID>`); earlier retirements are in
the archived development log.

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

### Menus and front end

Nothing here is a flight question, and none of it can be answered from `extracted/rof/`: a layout
row and a button bitmap state composition, never what a press, a rollover or a screen change does.
The screen and asset inventory these entries hang off is
[`docs/org/menu-inventory.md`](docs/org/menu-inventory.md); film in 4:3 windowed, where the original
draws its authored 800x600 space one-to-one.

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-49` | The **main menu**, from the splash through to Quit | Film the original's top level from launch: the flag movie behind the buttons (`CrimFlag.MPG`, which the extraction does not carry), the button frame, the logo, and all six rows (Campaign, Instant Action, Multiplayer, Preferences, Credits, Quit). Move the pointer slowly across every button and press one and back out, so the rollover and depressed frames are both seen; then press Quit. Clean audio on the take. *Look for:* whether the movie loops or plays once and holds, whether anything but the button under the pointer changes, what the version text at `mm_t_title` reads and where it sits (that widget has **no `LAYOUT.CSV` row**, so its position cannot be decoded), when the splash music starts and that Quit ends the game on the press with no confirm (`MAINMENU.SCRIPT` terminates on `mm_b_quit`, which is what CSVM's Original menu does; the film confirms the decode); also whether a press fires on the button-down or on the release, and whether any keyboard or pad input moves a focus among the buttons at all (CSVM's Original menu fires on the button-down and walks the buttons from the keyboard and pad as a remake equivalence, both unconfirmed). CSVM's Original top level draws Multiplayer in its disabled frame, a state the original never shows, since network play has no local counterpart | `E41` (the Original top level), `E42` (the main menu's required-asset rules), `BL-654` |
| `CAP-50` | The **Instant Action setup screen**, driven | The one screen behind CSVM's whole five-step wizard, and no capture of it exists. Film: the page as it opens; every drop-down opened in turn (player plane, wingman count, wingman plane, mission type, environment, and one wave's enemies/militia/aircraft/skill); the up/down buttons paging wave 0 against waves 1-3; picking **Dogfight an Ace** and watching every enemy control disappear; the Player/Wingman radio pair; and the Table of Contents list with VIEW STORY pressed on one preset. *Look for:* what a rollover previews, whether a changed militia visibly resets its aircraft field, what BUILD CUSTOM PLANE does from here, and how the screen reads before anything is chosen. Composition is unknown too, so hold each state long enough to read. ⚠ The option sets themselves are decoded (`docs/formats/instant-action.md`); do not re-derive them from the film. CSVM's Original presentation already draws this screen from the layout and must be checked against the take: it shows the contents list and the dropdowns together on the one page, changes the fields when a contents row is selected and writes the preset's name over the right page only on VIEW STORY, opens a dropdown as a list directly under its box with every item listed, hides the enemy rows, the up/down buttons and the "[continued ...]" text under Dogfight an Ace while keeping the Wingmen dropdown, keeps the Player/Wingman radio at zero wingmen, draws BUILD CUSTOM PLANE and WEAPON LOADOUT disabled, previews nothing on a rollover, and steps a dropdown's value from the keyboard and pad; each of these is a remake reading for the film to confirm or correct (`docs/org/menu-inventory.md`, Part 4) | `C21`, `B13`, and the Original wizard's whole fidelity target |
| `CAP-51` | **Preferences and its four pages** | Open Preferences from the main menu and visit Game Options, Audio, Video and Controls, then Keys from Controls. On each page change one setting and press Accept, then re-enter and change one and press Cancel, so both exits are seen. On Audio, move each of the three volume sliders and let the preview loop play. *Look for:* what Preferences itself shows (it has 14 widget rows and no capture), whether a page's description text follows the pointer or the selection, what the difficulty row offers and reads, and whether Preferences is reachable in flight as well as from the top level (`PF_B_RETURNTOGAME` suggests it is; `PREFERENCES.SCRIPT` picks it over `pf_b_mainmenu` by one flag). CSVM's Original menu composes its Options screen over this page and must be checked against the take: it draws the logo, the panel, the PREFERENCES title, the four page doors with all four descriptions standing at once in their authored colour, and RETURN TO MAIN MENU, with its own presentation chooser (an ORIGINAL/BUILT-IN plaque and APPLY) in the slot under the doors; the four doors draw in their disabled frame because no shared option exists behind them, a state the original never shows, and nothing here plays the movie. Each composition claim is a remake reading for the film to confirm or correct (`docs/org/menu-inventory.md`, Part 4) | `A3` (the options store), `E41` (the Options route), `BL-570`, `BL-455` |
| `CAP-52` | **Menu audio and pointer behaviour**, across screens | One continuous take walking main menu, Instant Action, back, Campaign, cabin, flight check, back out, with clean audio and the pointer visible throughout. *Look for:* (a) which sound a rollover makes and which a press makes, and whether a disabled button makes either; (b) what the edit-box keystroke and reject sounds are attached to; (c) where the splash music starts, stops and resumes across those screens; (d) which of the three cursor bitmaps is showing where, and whether it changes over a button, a list or a scrap, and where each bitmap's hotspot sits (CSVM's Original menu draws `ACTIVEPOINTERZ` over a live button and `PASSIVEPOINTERZ` elsewhere, top-left at the pointer, plays `MOUSEOVER` on entering a button and `MOUSECLICK` on a press, and plays nothing over a list row, all unconfirmed). On the campaign screens CSVM's Original menu must be checked against the take too: it plays `MOUSEOVER` on entering a campaign plaque (a cabin button, a briefing plaque, the flight check's paper buttons) and `MOUSECLICK` on pressing one, plays nothing on entering a roster row, a mission row of the table of contents or a scrap, plays `ENTERTEXT` per character the profile screen's name box takes and `ENTERTEXT_ERROR` per character it refuses, draws the active pointer over a plaque and the passive one over a list row and a scrap, restarts the briefing's narration from its start on REPLAY BRIEFING, ends it (and lifts the music duck) on RETURN TO CABIN and GO TO FLIGHT CHECK, and starts no narration when a flown mission returns to the scrapbook; hit-tests a scrap on its `SCRAPBOOK.CSV` region column, or on the picture's own bounds where the row authors `0,0,0,0`; opens the delete confirm as the two-button messagebox reading Yes and No (langui 102 and 103, the words `MESSAGEBOX.SCRIPT` gives a `0x4` box, decoded), focused on Yes as that script focuses its left button; asks before a sale in the hangar's inventory with the same box over langui 700 and refuses one with the one-button box; and lands the focus on the plaque that opened a screen when Back returns to it. Each of these is a remake reading for the film to confirm or correct (`docs/org/menu-inventory.md`, Part 4). CSVM's Built-in menu plays **no** click, rollover or keystroke sound and draws no pointer at all, so this is the whole of the evidence for both | `A4` (the audio and menu-input contracts), `D33` |
| `CAP-53` | The **Plane Construction tab bar**, driven | The original's plane customisation is a hub with six sibling tabs (Airframe, Engine, Armor, Guns, HardPoints, Paint) plus READY TO PURCHASE, not the linear walk CSVM implements. Film entering from the cabin's PLANE CONSTRUCTION (which goes to the **name** screen first), then moving between all six tabs out of order, then Purchase and Cancel. *Look for:* whether a tab is ever disabled, what the running total shows and where, whether leaving a tab commits or the whole build commits at Purchase, and what the SELL PLANES button does. The stills already cover composition (`OriginalScreenshots/Campaign CAP-40 Plane Construction 1/2.png` and the eight `CustomPlane Paint*.png`); this is the interaction half. CSVM's Original presentation already composes these screens from the layout and must be checked against the take: it enters through the name screen, whose Load Default Configuration box (checked as authored) starts the build on the Devastator with its stock engine, guns, hardpoints and armour and, cleared, on a bare airframe; it enables every tab but the standing one, which draws in its disabled frame; its running total is the PLANE COST line over the blueprint, following every pick; nothing commits when a tab is left, only Purchase Now on the totals page; SELL PLANES opens the `[@Hangar@]` INVENTORY (a plane dropdown, Sell, Export drawn disabled, Done back to the tab); a pick that changes the airframe raises the langui 206 defaults ask as a dialog over the page; CANCEL drops the build; an opened dropdown lists under its box in the layout's `TotalDisplayed` window with the list's own arrows; and the wallet-free door wears the READY TO EXPORT and CANCEL EXPORT strips the Instant Action stills show, without their $50000 figure. Each of these is a remake reading for the film to confirm or correct (`docs/org/menu-inventory.md`, Part 4) | `C23` |

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
| `CAP-38` | Beeper and seeker hits ON an aircraft, **with audio** | In the original, fire the beeper (`wep_10`) and the seeker (`wep_11`) at an aircraft and film the hit itself, external/chase, close enough to read the burst on the airframe. Both weapons author `ANIMATION large_fireball` on their aircraft `IMPACT` row and ours plays exactly that on a struck plane; a fused burst reads the `default` row in the original and in ours (`docs/org/ordnanceTypes.md` "Which row a burst reads"), so the beeper's near miss draws nothing and the seeker's draws its white flare at the round. The ground-side look is already signed off, so this clip is only the on-plane half. *Look for:* whether a direct strike shows the large fireball on the plane, something smaller, or nothing beyond the paint; whether a near miss shows nothing (beeper) or the flare (seeker); and the per-type impact sound on the same take (`snd_missile_beeper` / `snd_missile_seeker`) | Nothing tracks the outcome; a mismatch with our on-plane burst mints a new `BL` |
| `CAP-29` | Panel-damage semantics | Take controlled damage per part in the original, own aircraft in frame (external/chase), damage display visible if possible. **Reduced 2026-08-15 by the `BL-297` decode**, which answered all three questions out of `crimson.exe` (`docs/org/vehicleDamage.md`, "Damage staging"): (a) effects land at the node the def names, so a nose hit DOES spark wing sites; (b) nothing per-part fires at all while a part's armor absorbs; (c) each entry fires once per downward crossing, so a panel tears once until repaired. **What is still owed is the look:** watch one panel cross its tear threshold and judge whether the flung debris reads as a piece of that panel or as generic flakes, and what visibly changes on the airframe. The other three are now confirmation, worth capturing on the same take if the framing allows but not worth a dedicated sortie | `BL-297` |
| `CAP-47` | A gasbag burning out, CM14 (C2B/M04) | In the original, fly Clash of Dreadnaughts and set the Gemini's gasbags alight, holding one bag in frame from ignition through to whatever ends it, close enough to read both the fire and the envelope under it. *Look for:* what a finished gasbag looks like (a collapsed or missing envelope, a scorched one that stays, or a fire that simply stops), and what the zeppelin does once **three of its five** have finished, which is the authored death gate (`all_gmzep_gasbags` carries `MINIMUM_TO_SATISFY 3` over the five `finish_gmzepgasbagN` animations). Qualitative only: no burn duration is read off this clip as a constant | `BL-639` |
| `CAP-55` | The Gemini's own death and where its wreck rests, CM14 (C2B/M04) | In the original, fly Clash of Dreadnaughts and torpedo three of the Gemini's five gasbags down **without destroying any left broadside bay**, then hold the hull in frame from the first bag falling through to where the wreck settles and stops moving. *Look for:* (a) how many gasbags separate and fall, counted against the hull (ours sheds five, and the eye agrees with the data), and whether every one of them stays on the water, since ours drops the front and back bags through it (`BL-698`); (b) whether the gondola/underside disappears as it settles (`breakunder` switches `underneath` inactive); (c) the waterline against the three left broadside bays and their hatches once it is at rest, since ours puts at least one bay under the sea; (d) a gun burst fired into whichever bay sits nearest the water, held long enough to see whether it takes damage; (e) the pause-screen objectives readout before and after that burst. Qualitative only: no distance or rest height is read off this clip as a constant. | `BL-698`, `BL-695`, and `PT-119` (b) and (d): whether the original leaves a bay unreachable at all, where ours no longer needs one, since the hull's own death demolishes every bay |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### AI flight — an AI aircraft flying itself, external view

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-54` | The **same aircraft lit by night and by day** in the original, one airframe and one livery | Two matched-pose chase-view stills of **one** aircraft in **one** livery: a night mission and a day mission, framed the same way, with the plane large enough in frame to read its lit and shaded sides and with terrain visible beside it for a reference level. C1B IA1 by night against a C1C campaign mission by day is the pair the remake needs, and **C1C has no capture at all today**. The existing stills cannot answer it: `playtest/CAP-11/`'s frames and `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` show a red Bloodhawk at night and a red plane over C2 by day, but CAP-11's measurement boxes deliberately avoid the aircraft, and our own build flies a grey and yellow paint at those poses, so nothing is comparable. Pin the same paint in our shot when the take exists. *Look for:* how much darker the airframe reads at night than by day relative to the terrain under it, and whether the plane's shaded side goes to black or keeps a fill | `BL-332`'s two TUNE constants, landed but unjudged, and `BL-683`'s ambient question |

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
- `PT-120` `[Own]` **The rebinding screen's three axis-capture constants, judged with a real pad in
  hand (`BL-693`).** `ControlCapture.RestBand` 0.25, `MoveThreshold` 0.6 and `CapturedDeadzone` 0.5
  decide whether a stick or a trigger a player pushes becomes a binding, and none of them has a
  decode behind it: the original cannot bind an axis to a command at all, so there is nothing to
  match. Reach the screen through Options, bind a flight action to a stick direction and to a
  trigger, then fly the result. *Look for:*
  - (a) 0.6 (`MoveThreshold`) reads as a decisive push rather than a nudge: a deliberate deflection
    captures, a brush past does not;
  - (b) 0.25 (`RestBand`) forgives the stick your pad actually rests at, so a drifting centre never
    latches a binding on its own;
  - (c) in flight, the bound half of the stick at 0.5 (`CapturedDeadzone`) feels like a button, on
    and off, with no dead patch that reads as a broken binding.
  *Blocks:* `BL-693`. ⚠ Capturing the right trigger takes it from both Camera Boost and Camera
  Dolly Out rather than stacking a third reading (`ActionMap.SameControl` ignores the deadzone on
  purpose): that is not a fault of these three numbers and must not be tuned against.

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

- `PT-121` `[A/B: HUD.png]` **The compass tape's overscan, rim and nearest-tick look
  (`BL-113`).** `TileOverscan`, `RimGain` and the nearest-tick treatment are all still TUNE. North
  = −Z is confirmed against the original and is not in question here. *Look for:*
  - (a) the tick spacing, and how much of the tape shows at once, read like
    `OriginalScreenshots/HUD.png` rather than a wider or narrower window;
  - (b) the rim's brightness against the tape body at the same gauge size;
  - (c) the nearest tick under the pointer: picked and drawn the way the original's is, holding
    steady through a slow turn instead of stepping or flickering between neighbours.
  *Blocks:* `BL-113`.

- `PT-122` `[Own]` **Photo mode hands the aeroplane back to the view it borrowed the camera from
  (`BL-649`).** Three callers took the camera without restoring the airframe's visibility, leaving
  a pilot in cockpit view with the interior drawn over an outside vantage. All three now call
  `FlightController.SetViewedFromOutside` at both edges; what is owed is the flight. *Look for:*
  - (a) from `--view=cockpit`, pause and enter photo mode: the aeroplane is in the shot, not hidden
    behind its own panel;
  - (b) leaving photo mode returns the cockpit over a world that is still halted, with no frame of
    the exterior model showing through the panel on the way back;
  - (c) the same both ways from an outside view, where nothing should change at all.
  *Blocks:* `BL-649`. The two debug callers (`--debug-spectate` and the weapon lab's free camera)
  share the seam and are worth a glance in the same sitting: no automated check reaches them.
  *Variations:* `--view=cockpit` is the case the item is about.

### C1 · Bloodhawk vs AI — the kill sequence, sound on

```powershell
./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury --ai-attack=9 --volume=1.0 --no-det
```

⚠ `--volume=1.0` is not optional: the default master volume is 0, so a run without it is silent for
reasons that have nothing to do with any of these checks.

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

- `PT-123` `[A/B: OriginalScreenshots/Videos/CAP-14 Graze and CAP 15 wing to red.mp4]` **Breakup
  scatter magnitude in flight (`BL-121`).** The contact constants this item used to carry are
  retired against the decoded response, and the staged panel burn landed with `BL-259`. One feel
  judgement is left: how far the pieces throw when an airframe comes apart. *Look for:*
  - (a) the scatter reads as the original's, pieces leaving the airframe at a believable spread
    rather than pluming out or dropping straight down;
  - (b) the panel flip stays a skin swap at chase distance (charred outer wing, no large flapping
    geometry), which is what the original's own footage shows;
  - (c) the trail off a damaged panel stages fire, then black smoke, then a sputter, rather than one
    continuous flame.
  *Blocks:* `BL-121`.

- `PT-124` `[A/B: OriginalScreenshots/Videos/CAP-16.mp4 + CAP-14 Building crash Balmoral.mp4]`
  **The data-driven crash, judged whole against the original (`BL-122`).** The arc decode is
  settled and no scalar is left to fit, so what is owed is the look of the whole crash on both
  surfaces, plus its sound. ⚠ The two surfaces are deliberately different and must not be tuned to
  one look. *Look for:*
  - (a) a dirt crash reads as one big fireball at the original's intensity, its smokeball dark
    red-brown rather than black;
  - (b) the dirt burst reads as a separate, lower, ground-coloured cluster under it;
  - (c) the pieces hold their orientation and stay where they blew rather than scattering along
    travel, which is what the decode says and what an additive stack can easily disguise;
  - (d) a building strike against `CAP-14 Building crash Balmoral.mp4`: a spread of small discrete
    orange puffs, no large fireball, no dark halo;
  - (e) the sound, `snd_exp_ground_a`'s level and whether layering it over `plane_destroy_sg` reads
    as one impact or two.
  *Blocks:* `BL-122`.

- `PT-125` `[Own]` **How close a round has to pass before the near-miss cue sounds (`BL-230`).**
  `WarningShotCue.PassRadius` is 15 m, chosen rather than read: the shipped `warning_shot_*` block
  rates the cue but says nothing about distance, and the sound def's `RANGE [20,200]` is a 3D
  falloff window, not a trigger radius. *Look for:*
  - (a) at 15 m the cue fires for rounds that read as near misses and stays quiet for the rest;
  - (b) walking the radius in and out, where it starts to feel late or trigger-happy;
  - (c) the failure mode while raising it: a round crossing the sky nowhere near you sounding at all.
  *Blocks:* `BL-230`. ⚠ `CANNON_SPREAD` scatters each round several metres over any real firing
  range, so the achieved distance is a distribution: judge over a burst, never off one pass.
  *Variations:* `--incoming=<metres>` walks a burst past at a chosen distance;
  `weapons.warningShotRadius` in `config.json` moves the threshold without a rebuild, so it is
  **not** available under `--det`.

- `PT-126` `[A/B: OriginalScreenshots/Videos/CAP-10.mp4 + Bloodhawk Dive Sound.mp4]` **What plays
  past the plane's own top speed, and how loud (`BL-252`).** The gating is settled and needs no
  change: something starts exactly at `1.0× fd_speed`, the plane's own maximum level speed. Two
  things are not. There is no whine (no shipped def names `prop_sound`), so the open candidate is
  the RATTLE, `snd_planeshake`, whose curve runs 0 to 1 over `1.0` to `1.2× fd_speed`. ⚠ Settle
  what the sound is before judging any level: this item has already tuned the wrong slot once.
  *Look for:*
  - (a) hold straight and level at 100% throttle to settle at max speed, then dive: name what our
    build plays, and whether it is the same sound the original plays at that foot;
  - (b) the level, matched by ear against the original rather than measured, in the same view;
  - (c) the cockpit case separately, since the original's cockpit engine is damped where ours is
    not, so the ratio cannot be read across from the outside view.
  *Blocks:* `BL-252`. ⚠ Do not read the threshold off the airspeed dial: the gauge art, its red arc
  and its `300` mark are the same for every plane and cannot express a per-plane limit.

- `PT-127` `[A/B: OriginalScreenshots/Videos/CAP-21 0 to 100 Full.mp4 + CAP-21 100 to 0 Full.mp4]`
  **The engine start ramp, the wind-down, and the throttle-slam smoke gate (`BL-285`).** Two
  constants pending one cockpit sitting. `EngineStartRamp` is now `startprops`'s authored 2.0 s and
  the crash wind-down plays `snd_propstop`; `ThrottleSlamSmoke.SlamThreshold` is 0.25, the smallest
  value consistent with footage whose 2/8 to 4/8 band is unobserved. *Look for:*
  - (a) the start ramp and the wind-down by ear against the two clips above;
  - (b) the slam gate walked by hand: a single 1/8 step must not fire, idle to 5/8 must, and where
    in between it starts is the judgement;
  - (c) whether the smoke reads as a slam response at all, rather than a puff on any throttle move.
  *Blocks:* `BL-285`. ⚠ Judge against the current unscaled own-ship mix, not the old ×0.2 one
  `BL-268` removed. In splitscreen `snd_propstop` carries `MixGain` and `snd_propstart` does not, so
  judge each at the pane count being tested.

- `PT-128` `[A/B: OriginalScreenshots/Dirt Splash.png + Videos/30 Slu building.mp4]` **The building
  ricochet spark burst (`BL-289`).** The dirt-chip constants left this item with `BL-313`: dirt now
  takes the single spark, and the water column width is settled. What remains is the building
  ricochet, whose five constants are stand-ins because both authored assets are missing from the
  install: `RicochetSparks` 8, `RicochetSparkSize` 0.55 m, `RicochetSparkLife` 0.55 s,
  `RicochetSparkSpeed` 22 m/s, `RicochetSpreadDeg` 90°. *Look for:*
  - (a) strafe a building: the burst's count and spark size against the original's own impact spray;
  - (b) how long a spark lives and how far it travels before it goes;
  - (c) the spread, where 90° is a stand-in: whether the burst reads as coming off the surface or as
    a sphere around the hit.
  *Blocks:* `BL-289`. The splash height and timing curves are authored data rather than TUNE and are
  not on trial here.

- `PT-133` `[Own]` **The HUD's reading box is the right width at the controls on a 32:9 screen.**
  The dials, the SPD/ALT/THR block and the pause screen's objectives panel measure from a 16:9 box
  centred in the pane rather than from the pane's own edges — that box is the frame every one of
  those offsets was measured in (`HUD.png`, 2556x1440). Whether the width reads right on a
  5120-wide screen, where the two columns end up 1280 px in from each edge, is a judgement no
  instrument makes. Fly it with the window fullscreen (Options → VIDEO → Display Mode) so the pane
  is the whole screen.
  *Look for:*
  - (a) the dials and the status block sit within comfortable reading width, neither out at the
    edges of peripheral vision nor huddled into the middle;
  - (b) an off-screen target's edge arrow still comes from the TRUE screen edge and points
    usefully — that element deliberately keeps the pane, since the box is not where the target
    left;
  - (c) nothing has moved at 16:9: the same sortie windowed looks exactly as it did.
  *Blocks:* nothing tracks the outcome; a fail mints a new `BL`. The per-element choice and the
  one-pixel tolerance are in the landing commit (`git log --grep=BL-778`).

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

- `PT-134` `[Own]` **A 2-player split on an ultrawide window reads as two normal views, and the HUD
  size the flip brings with it is right.** The split stands the panes side by side once each half
  would still be at least as wide as it is tall, a window 2:1 or wider, so a 32:9 screen gives two
  16:9 panes. The pane share reaches `HudMetrics.PaneFactor` through the HEIGHT ratio, so a
  full-height pane's factor is 1 and the HUD draws at single-player size rather than the damped
  splitscreen size. That size change is the part to judge; run it fullscreen on a 2:1-or-wider
  screen.
  *Look for:*
  - (a) two panes side by side, each reading like an ordinary single-player view;
  - (b) the full-size HUD in each pane reads as correct rather than oversized;
  - (c) dragging the window back under 2:1 flips it to stacked while the sortie runs, and back;
  - (d) the plane select before the sortie splits the same way the flight then does.
  *Blocks:* nothing tracks the outcome; a fail mints a new `BL`. The threshold's reasoning is in the
  landing commit (`git log --grep=BL-777`). A stacked pane's first-person views hold their vertical
  angle and widen horizontally like any other viewport, so a cropped cockpit in one is a new fault
  rather than a known one.

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

- `PT-129` `[Own]` **Effect pools with four pilots all firing (`BL-537`).** The single-player half
  reads right at the controls: four fireballs burn out in place, and the seven-death wrap is not
  visible under the debris. Owed is the four-player case, where the default root wants more slots
  than it gets. *Look for:*
  - (a) anything that reads as shared between panes: an effect cut short in one pane as another
    starts, or a burst that appears in the wrong one;
  - (b) simultaneous deaths across panes, which is where `flame_ball_01` wraps;
  - (c) the instrument alongside the flight: the `anim: effect pool for '<name>' recycled slot`
    DEBUG line in the log file sink names any root that actually wrapped.
  *Blocks:* `BL-537`. ⚠ Raise only a root that logs a recycle, never the default; the three gun
  roots stay at 1, and a root sized 0 clamps to 1.

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

- `PT-130` `[A/B: OriginalScreenshots/Videos/CM02.mkv]` **The fixed landing pose, watched again
  (`BL-545`).** The report was CM02's auto-land seen with no hook deployed, the aeroplane too high
  on the trapeze, and a Balmoral's wings unfolded where the original folds them. The fix landed:
  the hookup definition now reaches the flown airframe's own subtree, so its per-airframe hook
  extend and wing fold run instead of every `IF NODE_ACTIVE` arm reading false. Watch the auto-land
  from outside. *Look for:*
  - (a) the hook extends before the catch;
  - (b) the aeroplane sits on the trapeze rather than above it;
  - (c) a Balmoral folds its wings, which is the airframe the original's own footage shows.
  *Blocks:* `BL-545`.

### CM14 (C2B/M04) · the Gemini, both kill orders

```powershell
./RunGame.ps1 --campaign=<profile>:13
```

- `PT-119` `[Own]` **The mission reaches an end whichever way the Gemini goes (`BL-694`).** The
  third primary is the only route to a win, and it counts five of the six `deploy_gmzep_lbroadNN`
  animations INVALID, which only a cannon bay's own destruction writes. Two authored chains supply
  them: a torpedoed gasbag demolishes its section's four bays, and the hull death demolishes every
  section that was not torpedoed. `gemini-gasbag-bays` proves both headless over three orderings,
  so this row is the confirmation at the controls that they arrive in a flown sortie. *Look for:*
  - (a) torpedo three gasbags with no bay shot at all: the Gemini dies and the third primary
    completes with it, rather than the sortie running on with nothing left that can happen;
  - (b) the reverse order, bays first and the hull afterwards, reaching the same end;
  - (c) the pause-screen objectives readout ticking primaries 1 to 3 as the bays go, and the
    mission then reaching its win through the docking;
  - (d) whether a bay under the water is still reachable, and whether it needs to be, since the
    hull death should already have taken it.
  *Blocks:* `BL-694`'s landing commit (`git log --grep=BL-694`). A sortie that reaches no end
  state mints a new `BL`; `CAP-55` is what the original owes against (b) and (d).


### The exported package · a recipient's first run, no arguments

```powershell
./ExportRelease.ps1   # then unzip .scratch\CSVM-v<version>-win64.zip into a bare folder
```

- `PT-131` `[Own]` **An exported build started by double-clicking `CSVM.exe` sounds, and the
  folder with no `extracted\` says so on screen.** The repo's silent master default is what keeps
  scripted runs quiet, so the export carries its own audible one; nothing in the payload passes a
  flag. Run the unzipped `CSVM.exe` from Explorer, with the extraction done and again with the
  `extracted\` folder renamed away. ⚠ Clear `CSVM_DATA_ROOT` from the environment first, or the
  exported build reads the development tree and neither case is what a recipient sees. *Look for:*
  - (a) with game data present, the menu comes up with its music and the menu cues audible at a
    sensible level, without a `--volume=` argument anywhere;
  - (b) with no `extracted\`, the no-game-data screen names `Extract.cmd` and stays up until Esc,
    rather than a menu over a world that cannot build.
  *Blocks:* nothing tracks the outcome; a fail mints a new `BL`. The level itself is untunable
  in game (`BL-455`).

### The VIDEO page · a second monitor plugged in

```powershell
./RunGame.ps1 --presentation=original --menu=video
```

- `PT-132` `[Own]` **The Monitor row actually moves the window, and a saved index naming a screen
  that is gone falls back instead of opening nowhere.** The display settings landed on a
  single-screen machine, so the enumerated list, the resolve and the precedence ladder are proved by
  suite while the move itself has never been seen. ⚠ Launch without `--screenshot` or `--det`: the
  startup display apply stands behind `!_spec.IsScripted` and a scripted run reads no saved display
  setting at all, so either flag hides exactly what this item checks. *Look for:*
  - (a) the Monitor row lists both screens, one label per screen, each naming an index and a size
    that match the monitors actually attached;
  - (b) picking the other screen and pressing ACCEPT CHANGES moves the window to it, and the page
    redraws composed correctly at that screen's size rather than clipped or letterboxed;
  - (c) with the window on the second screen, Display Mode and Resolution still apply there:
    exclusive fullscreen fills the screen the window moved to, not the one it started on, and the
    Resolution row now offers that screen's own modes;
  - (d) the choice survives a restart, the window opening on the chosen screen with no arguments;
  - (e) with that monitor then unplugged or disabled, the next launch comes up on the remaining
    screen rather than erroring or opening off the desktop, since the saved index is the one
    setting that can name something absent.
  *Blocks:* nothing tracks the outcome; a fail mints a new `BL`. The reasoning behind the fallback
  and the apply order is in the landing commits (`git log --grep=BL-768`).

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
