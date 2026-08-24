# Milestone 5 — the Campaign

**ACTIVE PLAN** (written 2026-08-24). It sits in `docs/`, which by this repo's convention makes it
a live plan. `PROJECT_CONTEXT.md`'s "Current status" currently names `PLAN-flight-model-parity.md`
as the active plan; this plan starts when that pointer swaps to it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan delivers the playable single-player campaign: the out-of-mission flow (player profile,
campaign cabin, mission briefing, flight check with ammunition selection) and the in-mission
campaign machinery (the objectives choreography engine, intro cutscenes with letterbox, sound
cues, campaign wingmen), with progression persisted per profile. The reference screens are
`OriginalScreenshots\Campaign *.png` (five shots: Player Profile, Cabin, Briefing, Flight Check,
Ammo Selection). It absorbs the open campaign-scoped backlog items: `BL-134` (cutscene player),
`BL-243` (cross-mission persistence), `BL-350` (hangar door `WAKE_ANIM`), `BL-361` (scripted-path
vehicles), `BL-362` (campaign wingman station-keeping, the open half), `BL-363` (non-aircraft AI
targeting candidates), `BL-364` (campaign patrol-net plumbing), `BL-037`
(`WorldPartitionSetActive`), `BL-038` (`FogState`). Each of these was confirmed present and open in
`backlog.md` on 2026-08-24; none has yet been re-verified against `git log --grep` and the code.
<TODO: re-verify each absorbed BL still-open against git log --grep + the code before starting its
item.>

Deliberately out of scope: MPG movie playback (the between-chapter cinemas in
`GOSDATA\ASSETS\GRAPHICS\MPG\` and `FINALCINEMA.SCRIPT`), the memento/scrapbook system
(`MOMENTOSELECTION.SCRIPT`, `SCRAPBOOK*.SCRIPT`, the `Snap_*.png` saves, `BL-256`), multiplayer
readers (`BL-299`), and Instant Action fixes (`BL-426`). Decisions 2 and 3 below say why.

## Milestone goal

- A player can create, select and delete a named profile; all campaign state is per profile.
- The cabin hub offers Next Mission, Previous Missions (replay any finished mission), Plane
  Construction (the existing hangar, now with a wallet: buy/sell gated by funds and campaign
  availability), and Return to Main Menu.
- The briefing screen plays the mission briefing (map, flag animation, narration audio, objectives
  list) with REPLAY BRIEFING / RETURN TO CABIN / GO TO FLIGHT CHECK.
- The flight check screen shows the pilot's and each wingman's plane and loadout, the objectives
  note, Change Ammo per aircraft (the Ammo Selection screen), plane change where the mission
  allows it, and FLY MISSION.
- Campaign missions run their decoded `objectives.zrd` choreography: dormant/wakeup timers, sound
  cues, objective chaining, target-list edits, completion audio, and an in-flight objectives
  display; mission end returns to the cabin and advances or records progress.
- Story-mission intro animations play as cutscenes with letterbox bars and camera control, then
  hand off to gameplay.
- The state-driven score plays: menu splash, cabin, prebattle → battle transitions, objective
  stingers, and the success music, from the extracted `music_*` tracks.
- Campaign wingmen fly the decoded netless station-keeping and are spawned from the mission's
  named rosters.

**The mechanisms are data-driven for all 5 chapters; the fidelity sign-off gate is Chapter 1
only.** Per-mission choreography polish beyond C1 becomes follow-up backlog items, because 53
missions cannot be individually verified inside one milestone (decision 6).

## Decisions (2026-08-24)

| # | Question | Decision |
|---|---|---|
| 1 | Save format: decode and write the original `SavedGames\` binary, or CSVM-native saves? | **CSVM-native JSON per profile** (`user://Profiles/<name>/`, following `ScoreStore.cs` / `CustomPlaneStore` precedent). The original format (`Status.dat`, `AutoSave.sav`, `Persist.NNN`, `Mission.NNN`) is decoded only as far as it answers structural questions (mission ids, progression, wallet, loadout persistence); writing it back and importing original saves are out of scope. |
| 2 | MPG movie cinemas? | **Out of scope** — plain MPG playback is a codec/container problem orthogonal to the campaign flow; file a backlog item when M5 closes. |
| 3 | Memento / scrapbook? | **Deferred to backlog** — the cabin ships without the Change Memento function; it is cosmetic and rests on the undecoded snapshot flow (`BL-256` adjacent). |
| 4 | Briefing audio vs music | **Both ship.** Narration is the extracted `*_briefing.wav` files; the score is the 33 extracted `music_*` tracks in `soundsh`/`soundsl` (state-named: prebattle/battle/battlesuccess/missionsuccess ×6, objective stingers, splash, instantaction, spicyairtales). The engine has no music subsystem and the track-selection logic is undecoded; D37 owns both. |
| 5 | Hangar economy | **The wallet lands in M5.** `PLAN-hangar.md` explicitly deferred funds and buy/sell economy to "campaign property"; the hangar's 11-airframe progress-threshold field (its decision 9) gets wired here. |
| 6 | Fidelity scope | **Mechanisms for all chapters, sign-off on C1.** The choreography engine reads every mission's data; the at-the-controls verdict wave flies the C1 story missions only. |
| 7 | Where campaign screens live | **New screens follow the existing `src/UI/` board/menu idiom** (`BoardMenu`/`IHangarPage`-style pages, langui string ids, decoded `rimage`/`rof` art) rather than a new UI framework. |

## ⚠ Read this before implementing anything

**Disproven so far:**

| Claim | Status |
|---|---|
| "The mission tree branches at `C1B`/`C1C`/`C2B`, which fork and rejoin" | **Disproven (A3).** `cm_sequence.zrd` is a flat 24-entry list with no predicate and no alternates. `C1B`/`C1C`/`C2B` are terrain splits inside an act, sitting in the linear order. Model campaign position as one integer. |
| "Strings 3500–3599 are act titles and 3600–3699 mission names" | **Disproven (A3).** Mission names are 3450–3473 (long) and 3480–3503 (short), indexed by story position; act names are 1220–1224. 3600+ is Instant Action content. `docs/formats/strings.md` is corrected. |
| "Ammo has prices, planes depreciate, the campaign starts with a purse" | **Disproven (A5).** Ammunition and rockets are free, selling refunds the full build cost, and the campaign starts with $0 and two aircraft. The $250,000 belongs to the `fAllowAll` unlock mode. |
| "The `MSG_BRF_*` prefixes in `objectives.zrd` are copy-paste leftovers naming the wrong chapter" (A4's reading) | **Corrected (A3 + a full census).** The abbreviation is the act name, not a wrong chapter. The digit equals the folder's `M0n` number everywhere except the Hollywood pair `C2/M01`/`C2/M02`, which carry each other's digits. Still the wrong key for picking a briefing state; `cm_sequence` is the key. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism, with the data that proves it** | A1 (landed: `docs/formats/objectives.md`), A2 (landed: `docs/formats/saved-games.md`), A3 (landed: `docs/formats/campaign-sequence.md`), A4 (landed: `docs/formats/briefing.md`), A5 (landed: `docs/org/hangar.md` "The campaign wallet", on-screen cross-check done via `CAP-40`), A6 (landed: `docs/formats/campaign-screens.md`), A7 (landed: `docs/formats/anim-definitions/cutscenes.md`), C25 (ammo/loadout base), D34 (station-keeping constants), B13 (threshold field), D37's selection logic (landed: `docs/org/music.md`, with two disproofs) | Confirm the trace, then implement. |
| **Direction sound, magnitude or details a judgement call** | B11, B12, C21–C24, D31, D32, D33 | The shape is settled by the original's screens/data; layout metrics, timings and exact behaviours come from captures and decode, not invention. |
| **Leads only — no mechanism yet** | D35's `BL-038` wiring point | Budget for investigation; may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

Everything below was located on disk in this planning session (2026-08-24 survey).

- **Campaign screens' GUI scripts** — `extracted\rof\ASSETS\SCRIPTS\`: `CAMPAIGN.SCRIPT`,
  `CAMPAIGNINTRO.SCRIPT`, `PASSENGERCABIN.SCRIPT` (the cabin), `FLIGHTCHECK.SCRIPT`,
  `ORDINANCELAYOUT.SCRIPT` (ammo selection), plus the hangar set already consumed by
  `PLAN-hangar.md`. Raw obfuscated text; only widget keys are documented (`docs/formats/rof.md`).
- **Briefing dialog layout** — `extracted\zrdr\Briefing.zrd.json` (316 KB, shared scope):
  `BRIEFINGDIALOG` → `PRIMITIVES` (`OBJECTIVESLIST` with `BACKGROUND`/`TITLE`/`LIST`) and
  `BUTTONS` (`REPLAY`, `RETURNTOCABIN`, …), keyed to `MSG_BRF_*`/`MSG_BTN_*`. No formats page.
- **Briefing narration** — `extracted\soundsh\` / `soundsl\`: 24 high + 24 low
  `<chapter>-<abbrev>-mN_briefing.wav` files, one per campaign mission, already extracted.
- **Mission choreography** — per-mission `objectives.zrd.json` (worked example
  `extracted\C2\M01\zrdr\objectives.zrd.json`, 24,693 B): `MISSION_TIMER`, `PLAYER_INIT`,
  `RESTORE_ANIMS`/`EXECUTE_ANIMS`/`INVALIDATE_ANIMS`, then `OBJECTIVEn` blocks with
  `BEGIN_DORMANT`, `WAKEUP_SOUND_GROUP`, `NAP_OBJECTIVE_WHEN_I_COMPLETE`,
  `KILL_OBJECTIVE_WHEN_I_COMPLETE`, `IDENTITY [class, priority, MSG_BRF_*]`,
  `ADD/REMOVE_OBJECTIVE_TARGET`, `COMPLETED_SOUND_GROUP`, `INACTIVE1`. No formats page documents
  this vocabulary. Mission folders also carry mission-specific scripted readers (`bmanrun.zrd`,
  `hangar_drop.zrd`, `barge.zrd`, …).
- **Original saves** — `CrimsonSkiesGame\SavedGames\<Profile>\`: `Status.dat` (12,732 B),
  `AutoSave.sav` (11,136 B, embedded absolute path string), `Persist.NNN` (~320–472 B, one per
  mission id, ASCII tag `zSaveHeader`), `Mission.NNN` (24–148 KB), `Snap_*.png` scrapbook shots.
  Decoded to the structural depth A2 asked for:
  [`docs/formats/saved-games.md`](formats/saved-games.md).
- **Mission order** — `extracted\zrdr\cm_sequence.zrd.json`, a flat 24-entry list binding each story
  position to a `ZBD\` world folder and `M0n` subfolder. The folders are sparse because an act's
  missions are split across terrain folders (`C1`/`C1B`/`C1C` are all act 2), not because the
  campaign branches. Decoded in A3, see [`docs/formats/campaign-sequence.md`](formats/campaign-sequence.md).
- **UI art** — `extracted\rimage\` (255 PNGs, e.g. `brief_button1.png`) extracted but consumed by
  nothing; `.BM` paint masks and `ui_strings.json` come from `ExtractRof.ps1`.
- **Strings** — `docs/formats/strings.md`: ids 700–799 purchase/sell prompts, 1200–1219
  mission-results UI, 1220–1224 act names, 3450–3473 and 3480–3503 the campaign mission names
  (long and short, indexed by story position).
- **Letterbox** — a `letterbox` node exists in every chapter's gamez root, one of only two nodes
  shipped `active:false`. Decoded by A7: two opaque black quads pinned to `camera1` by the shared
  `zrdr/letterbox.zrd` definition, off until a cutscene calls it
  (`docs/formats/anim-definitions/cutscenes.md`).
- **Cutscene defs** — `generic_intro` ×12 + `mission_intro_animation` ×1 across 13 of 53
  missions (all `M0x` story missions), fully decoded and playable by `AnimRuntime`. D32 landed the
  consumer (camera, letterbox, sequencing, `CALLBACK` dispatch) as `CutsceneController`.
- **Wingman constants** — `BL-362`: decoded body-frame stations (6 m out / 18 m astern of the
  player leader; 8/−2/−8 of an AI leader), 700 m join threshold, speed-ramped trail
  106.68–259.08 m.
- **Economy** — decoded out of `crimson.exe` by A5 and written up in `docs/org/hangar.md`, "The
  economy" and "The campaign wallet": the airframe/engine/gun price tables, armour at $4 and 4 lb
  per unit with a 60-unit per-zone cap, hardpoints at $410, sell at full build cost, $0 starting
  funds with two starting Devastators, free ammunition, and the ten-record mission reward table at
  `0x0061ae80` worth $140,900 plus five named aircraft. No reader ships any of it.
- **Engine bases to extend** — hangar Build/Buy/Sell flow (`HangarFlow.cs` + pages, `IHangarPage`,
  204-byte import, `user://Planes/`), `LaunchMenu.cs` state machine + board chrome
  (`BoardMenu.cs`), `InstantActionDirector` (the sibling mission director),
  `AnimRuntime` + `Anim/` family, `Loadout.cs`/`stock_loadouts.json`/`WeaponBench.cs`,
  `AiPilot`/`AiModeMachine`, `ScoreStore.cs` persistence precedent, `MissionTargets.cs`
  (read before scoping the objectives HUD). No music/BGM subsystem exists anywhere.
- **Music** — `extracted\soundsh\music_*.wav` (33 tracks, matching low-quality set in `soundsl\`):
  `prebattle1–6`, `battle1–6`, `battlesuccess1–6`, `missionsuccess1–6`, `primaryobj1–2`,
  `secondaryobj1–2`, `tertiaryobj1–2` (objective-completion stingers), `splash`, `instantaction`,
  `spicyairtales`. The data is fully extracted and the selection/transition logic is decoded in
  D37 ([`docs/org/music.md`](org/music.md)): the numbered variant is the sound group's own weighted
  random pick, the prebattle → battle transition is a 20-second battle timer pinged by a proximity
  scan and by player damage, and `instantaction`/`spicyairtales` are cued by nothing at all.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — decodes and captures

1. ☑ Decode the `objectives.zrd` choreography vocabulary → `docs/formats/objectives.md`
2. ☑ Decode the original save/profile format far enough to answer the structural questions
3. ☑ Decode the campaign mission tree (order, branching, unlocks)
4. ☑ Decode the briefing: `Briefing.zrd` dialog layout + the briefing map/flag animation
5. ☑ Decode the economy constants: plane buy/sell prices, armor cost, starting funds
6. ☑ Behavioral decode of the campaign GUI scripts (cabin, flight check, ammo, campaign intro)
7. ☑ Decode the `letterbox` node mechanics and the cutscene `CALLBACK` codes
8. ☑ Mint and file the owed captures (plane construction screen, previous-missions list, briefing animation, a C1 mission intro, cabin ambience)

### Wave B — campaign model and persistence

11. ☑ Profile store + campaign session model (`user://Profiles/<name>/`, SessionSpec/CLI entry)
12. ☑ Campaign progression + cross-mission persistence (`BL-243`, mission tree from A3)
13. ☑ Wallet and campaign availability wired into the hangar (buy/sell, thresholds)

### Wave C — the out-of-mission screens

21. ☑ Player profile screen (create, select, delete, text entry)
22. ☑ Campaign cabin screen (Next Mission, Previous Missions, Plane Construction, Return to Main Menu)
23. ☑ Mission briefing screen (map, flags, objectives list, narration; replay / return / flight check)
24. ☑ Flight check screen (pilot + wingmen planes and loadouts, objectives note, plane change, fly mission)
25. ☑ Ammo selection screen (per gun caliber group, per hardpoint, descriptions) for self and wingmen

### Wave D — in-mission campaign machinery

31. ☑ Campaign mission director: the objectives runtime (from A1) + mission end/return flow
32. ☑ Cutscene player: intro animations, letterbox, camera control, handoff (`BL-134`)
33. ☑ In-flight objectives display + objective sound cues
34. ☑ Campaign wingmen: named rosters + netless station-keeping (`BL-362`, `BL-364`)
35. ☑ Mid-mission world fidelity: `WAKE_ANIM` doors (`BL-350`), scripted-path vehicles (`BL-361`), `WorldPartitionSetActive` (`BL-037`), `FogState` (`BL-038`)
36. ☑ AI targeting candidates beyond aircraft (`BL-363`)
37. ☑ Music: playback subsystem + the state-driven track selection

### Wave E — end to end and sign-off

41. ☐ The full loop on C1: profile → cabin → briefing → flight check → mission → cabin, state persisted
42. ☐ At-the-controls verdict pass against the original (C1 story missions + all five screens)

## Dependency and parallelism notes

A1 blocks D31/D33; A2 blocks B11/B12 (structural questions only); A3 blocks B12 and C22's Next
Mission logic; A4 blocks C23; A5 blocks B13; A6 informs C21–C24 (layout/behaviour, not a hard
block); A7 blocks D32; A8 (captures) gates the fidelity halves of C22/C23 and E42 but not the
mechanism work. B11 blocks everything in C and D that touches campaign state (all screens, D31).
Wave C items are mutually parallel after B11 but all touch `src/UI/` navigation seams; give each
a page-file boundary and keep `LaunchMenu.cs` edits to one item at a time. D32 and D31 both touch
`GameSession.cs` session build; do not run them in parallel worktrees. D34 touches
`AiPilot`/`AiControlLaw`, which `PLAN-flight-model-parity.md` may also touch while live; sequence,
don't parallelize, if both plans are active at once. D37's subsystem half is independent (it can
land against the menu/cabin screens early); its in-mission state hooks depend on D31. E41/E42 are
strictly last.

---

# Wave A — decodes and captures

## A1 ☑ Decode the `objectives.zrd` choreography vocabulary → `docs/formats/objectives.md`

**Goal.** A formats page documenting every opcode the campaign missions' `objectives.zrd.json`
uses (`BEGIN_DORMANT`, `WAKEUP_SOUND_GROUP`, `NAP_OBJECTIVE_WHEN_I_COMPLETE`,
`KILL_OBJECTIVE_WHEN_I_COMPLETE`, `IDENTITY`, `ADD/REMOVE_OBJECTIVE_TARGET`,
`COMPLETED_SOUND_GROUP`, `INACTIVE1`, `RESTORE/EXECUTE/INVALIDATE_ANIMS`, plus everything a full
census surfaces), with semantics traced against `crimson.exe`, not inferred from names.

**Evidence (confidence: exe-traced).** `docs/formats/objectives.md` is landed. All 53
`objectives.zrd.json` files censused (24 campaign, 8 IA, 21 MP; 1,338 `OBJECTIVEn` blocks);
every directive traced through `crimson.exe`: parser `FUN_00466b70`, per-frame tick
`FUN_0046a490`, wake executor `FUN_00469af0`, plus every condition test and completion
executor, cited on the page per row. The C2/M01 worked example (82 objectives) is walked end
to end on the page. Named gaps kept as gaps: `PLAYER_INIT`'s non-position fields' consumers,
the reader of zeppelin byte +0xc (`COMPLETED_ZEPCANNONS`), `WIN_ANIM`/`LOSS_ANIM`'s consumer,
radio-queue fade internals.

**What the traps warned about, resolved.** `INACTIVE1..n` is a gamez node-path inactivity test
(destroy detection via the node active bit), not a status flag; the `IDENTITY` priority is the
objectives-display row key, sort key and per-class completion report value, unique per mission,
and never a weight. Findings D31/D33 must honor: objectives are a four-state machine
(dormant/awake/napping/retired) with at most ONE completion per tick on a rotating scan;
`NAP_OBJECTIVE_WHEN_I_COMPLETE` clears the target's completed flag (the repeat mechanism);
`TICK_DEPENDS_ON_OBJ` gates on the dependency being AWAKE, not complete; the wake executor
truncates its list at an already-awake target; `WAKEUP_OBJECTIVE_WHEN_I_COMPLETE` (C1B/M03)
and `SET_AI_` (C5/M04) are dead misspellings the runtime must not fix; danger zones completed
while an objective sleeps do not count for it; `DEDG` counting force-widens the watched
group's engagement volumes every tick. For D37: the prebattle-to-battle music transition is
data-driven through sound groups (`player.zrd` `in_battle_sound` = `music_battle_sg`, latched
once by `FUN_0046cc70`), not an engine combat detector.

**Verify (met).** The page's tables cover 100 % of the census inventory, state the census
count, and the worked example is walked on paper, surfacing one authoring race (the OBJ29-32
convoy lines) the page documents rather than smooths over.

## A2 ☑ Decode the original save/profile format far enough to answer the structural questions

**Goal.** Answers, written into a `docs/formats/` page, to: what a profile stores (wallet, owned
planes, mission results, current position in the tree), where the player's ammo/loadout selection
persists, and what `Persist.NNN` carries per mission (the `BL-243` state log). Not a byte-complete
decode and no writer.

**Evidence (confidence: decoded, traced to the save/load callbacks in `crimson.exe`).** The answers
are in [`docs/formats/saved-games.md`](formats/saved-games.md). Every file in `SavedGames\` is one
named-section container (payloads, then a 148-byte-per-entry directory, then a count); which
sections a file holds is decided by a category mask in its 12-byte `zSaveHeader`.

- **The profile is `Status.dat`.** Its `UIData` section (10,820 B) is a verbatim image of the
  global at `0x0064b340` and holds funds (`+0x448`), the 26-slot 204-byte plane array (`+0x44c`,
  the same record [`docs/formats/paint.md`](formats/paint.md) already documents), the pilot name,
  the selected plane, the memento image, and a 24-entry mission-result array (`+0x1868`, 168 B per
  record, best-of merge on completion: objective mask, time, shots/hits, money, plane flown).
  Campaign position is `+0x338`, the count of completed missions. Its sibling `PilotStatus`
  section is 1,448 zero bytes in the only profile available and stays undecoded.
- **The ammunition and ordnance picks live in the 204-byte plane record**, not in a separate
  loadout: per-gun ammunition index at `0x98`–`0xa4` (`4` = no gun, correlating perfectly with
  gun id `5` across all nine planes in the save) and per-pylon ordnance ids at `0xa8`–`0xc4`.
  B11/C25 persist those two groups; the ordnance id vocabulary is not decoded.
- **`Persist.NNN` carries no state.** Its only payload is `PlayerVehicle/player` (and
  `/wingman_1`), four bytes that are the payload's own length, and the load callback is an empty
  stub. The cross-mission state `BL-243` wants is in `Mission.NNN` (mask `0x20000`: anim
  activation, running anims, turrets, weapons, world), which the engine reloads from the previous
  mission **in the same chapter**. B12 should read the page's `Persist.NNN` and `Mission.NNN`
  sections before choosing its seam.
- **⚠ The mission tree is not in the save, and it is not a tree.** It is
  `extracted\zrdr\cm_sequence.zrd.json`: 24 flat entries, `seq` 0..23, each naming a `campaign`
  (a `ZBD` world-folder index, 1 = `C1`, 2 = `C1B`, 3 = `C1C`, 4 = `C2`, 5 = `C2B`, 6 = `C3`,
  7 = `C4`, 8 = `C5`), a `mission`, an `area` and a `wingman` flag. `Persist`/`Mission` ids are
  `%1d%02d` of `campaign`,`mission`. A3's graph section belongs on its own page, not on A2's; A3's
  Evidence paragraph above is stale where it says no reader holds the graph and where it assumes
  branching.

**Approach.** Done. The page states where the decode stops (`PilotStatus`, the `Mission.NNN`
payloads, the settings block, the counter arrays, the ordnance vocabulary) and why.

**Model recommendation.** high — open-ended reverse engineering with a defined stopping rule.

**Verify.** Each claimed field is backed by either a diff experiment or an exe trace, cited on the
page. No claim from file offsets alone.

**⚠ Traps.** ⚠ Do not commit hexdumps containing bulk asset data (repo hard rule). The user's real
save (`Zachary`) is irreplaceable evidence: read-only, never write into `SavedGames\`. The sample
is a single profile and the original cannot be run here, so "play, change one thing, diff" was
never available; the page's diffs are across the 20 `Persist` files, the 9 plane records and the 21
populated mission records instead.

## A3 ☑ Decode the campaign mission order and unlocks → `docs/formats/campaign-sequence.md`

**Goal.** The full mission order, what unlocks Next Mission, and each mission's id ↔ chapter folder
↔ display-name string id. **Delivered** as [`docs/formats/campaign-sequence.md`](formats/campaign-sequence.md).

**Evidence (confidence: traced to an exact mechanism, with the data that proves it).** The order is
`extracted\zrdr\cm_sequence.zrd.json`: 24 flat entries, `seq` 0…23, each carrying `campaign`
(ZBD world-folder number 1…8), `mission` (`M0n` number), `area` and a `wingman` flag. `crimson.exe`
supplies the folder-name switches (`1 = c1 … 8 = c5`), the `Persist.%1d%02d` / `Mission.%1d%02d` id
format (`campaign * 100 + mission`), the `brief_c%d%d` briefing-state key, and the progression rule.
Cross-checked with no orphan on any side against the 24 `M0n` folders on disk, the 24 `brief_c<NN>`
states in `Briefing.zrd.json`, and the 20 save ids in the user's profile (exactly `seq` 0…19).

**⚠ Disproof landed: the campaign does not branch.** `cm_sequence.zrd` is a flat list with no
predicate and no alternates. `C1B`, `C1C` and `C2B` are terrain splits of an act's map area sitting
in the linear sequence (act 2 spans `C1`/`C1B`/`C1C`, act 3 spans `C2`/`C2B`), not story forks.
B12 and C22 should model a single integer position, not a graph.

**⚠ Disproof landed: the string ids in this plan's survey were wrong.** Mission names are
`IDS_MISSIONLONGNAME` 3450–3473 and `IDS_MISSIONSHORTNAME` 3480–3503, both indexed by `seq`; act
names are `IDS_MISSIONAREA` 1220–1224. Ids 3500–3599 hold the tail of the short-name block and
3600–3699 are Instant Action content, not act titles and mission names.
[`docs/formats/strings.md`](formats/strings.md)'s ID map is corrected.

**Also delivered.** A4's open folder-to-briefing-state question: the state key is
`"brief_c" + campaign + mission`, formatted from the two `cm_sequence` fields. The narration wav is
**not** derivable by formula (the Hawaii act's wav numbers follow the folder, not the story
position, so `seq/5`, `seq%5` is right for 20 of 24 and wrong for 4); take the wav from the state's
own `PlaySound`.

**Verify (done).** Every mission folder on disk and every `Persist.NNN` id in the save is accounted
for, in both directions, with the table on the page.

**Reconciled with A5.** The campaign reward table is keyed by the 1-based mission ordinal
(`seq + 1`), settled by the completion handler and corroborated by record 24 landing on the final
mission. C24's plane-change rule falls out of the same reading: the two missions that bar CHANGE
PLANE, ordinals 13 and 17, are `seq` 12 (`C2/M03`) and `seq` 16 (`C4/M02`), which are exactly the
two reward records that award an aircraft instead of cash.

**⚠ Traps.** The user's save reflects one play-through; it is consistent with the decoded order but
does not by itself prove the order. The three-way cross-check on the page is what does.

## A4 ☑ Decode the briefing: `Briefing.zrd` dialog layout + the briefing map/flag animation

**Goal.** A formats page for the shared `Briefing.zrd` dialog definition (panels, fonts,
positions, buttons, `MSG_BRF_*` linkage) and for how a mission's briefing is assembled: which data
places the map, the red flags, and the step-by-step reveal the original animates, and which
narration wav belongs to which mission.

**Evidence (confidence: traced).** Landed as [`docs/formats/briefing.md`](formats/briefing.md).
`Briefing.zrd.json` (20,470 lines) holds `BRIEFINGDIALOG` with fixed `PRIMITIVES`/`BUTTONS` chrome
plus 25 named `STATES` (`default` and 24 `brief_cNN` mission states), each carrying its own
background map art and, for every mission state, a `SCRIPT`: an ordered opcode list
(`PlaySound`/`WaitForMarker`/`Pict`/`Fade`/`Spin`/`Move`/`Line`/`On`/`Off`/`Objective`/`Wait`/
`ToBack`, 12 opcodes total, fully censused) that is itself the map's step-by-step reveal, synced to
that mission's narration wav via `WaitForMarker`. `map.zrd.json` and `location.zrd.json` turned out
to be unrelated: `map.zrd` is the in-flight cockpit map overlay (a `MAP` node with a `player_icon`
marker) and `location.zrd` is a per-mission list of named world points (some byte-identical across
chapters, one authored `null`); neither is read by `Briefing.zrd`, `sounds.zrd`'s briefing `SETS`,
or any mission's `objectives.zrd`. The reveal mechanism (the SCRIPT itself) and the wav pairing
(each state's `PlaySound` sound name, corroborated by `sounds.zrd.json`'s `SETS` carrying the same
state key over the same sound file) were both fully recoverable from the data, so no
`crimson.exe` trace was needed for either.

**Approach.** Done as scoped: `Briefing.zrd` documented structurally from the JSON census;
`map.zrd`/`location.zrd` read and found not to be the briefing's data; reveal order came straight
out of the SCRIPT vocabulary, so the `crimson.exe` escalation condition never triggered.

**Model recommendation.** medium, as scoped; the exe trace was not needed.

**Verify.** The documented chrome positions (buttons at `y=560`, the objectives note at
`[0,295]`/`[35,315]`/`[35,335]`) reproduce `Campaign Briefing.png`'s layout. Every one of the 24
mission states maps to exactly one narration wav with no sharing or gaps (censused table on the
page); which *shipped mission folder* each state belongs to is not decodable from this item's
sources and is called out as an open dependency on A3 below, rather than closed here.

**⚠ Traps.** The `brief_cNN` state key looks like a chapter/mission encoding and is not one:
`brief_c31` plays chapter 2 mission 1's narration and map art, `brief_c12` plays chapter 2 mission
2's. C23 must key off the `SCRIPT`'s `PlaySound` sound name or the `BACKGROUND_IMAGES` bitmap name,
never the state key. A second, independent trap sits inside each mission's own `objectives.zrd`:
its `MSG_BRF_*` prefix names a *different* chapter's abbreviation than the one it ships in (all
five C1 story missions are tagged `NWM<n>`, `NW` being chapter 2's abbreviation) and is not even
internally consistent within one chapter (`C2/M01` is tagged `HWM2`, `C2/M02` is tagged `HWM1`,
swapped). Do not use that prefix, or its objective count, to pair a mission folder with a
`Briefing.zrd` state; neither lines up (worked out in full on the formats page's "Evidence &
limits"). C23 needs an explicit folder-to-state lookup from A3's mission tree, not a formula
derived from any of these internal names.

## A5 ☑ Decode the economy constants: plane buy/sell prices, armor cost, starting funds

**Goal.** The numbers the wallet needs: each airframe's buy and sell price, armor per-unit cost
and per-zone caps, weapon/ammo prices if the original charges for them, and the campaign's
starting funds, each with its `crimson.exe` provenance.

**Evidence (confidence: traced to code, pending the on-screen cross-check).** The trace landed.
Every number below has an address, and the wallet's writer set is closed rather than sampled.
The write-up is `docs/org/hangar.md`, "The campaign wallet" (the economy tables it extends were
already there from `PLAN-hangar.md`); `docs/formats/vehicle.md`'s armory paragraph is corrected to
match. ⚠ **Every recovered price is exe-traced only. The on-screen confirmation is still owed**
through A8's plane-construction capture (`CAP-40`), which is filed but unfilmed; until it exists,
no price here has been seen in the original's UI.

What was recovered:

- **Airframe, engine, gun and hardpoint prices** were already decoded (stat table `0x00619bb0`,
  engine base table `0x00619d98` with its six tier offsets, gun table `0x00619e68`,
  hardpoints $410 / 480 lb). Confirmed against `FUN_00405680` this pass, not re-derived.
- **Armour: $4 and 4 lb per unit, per-zone cap 60 units** (13 dropdown rows labelled `r × 5`).
  This closes `vehicle.md`'s standing "executable-resident, undecoded" note and independently
  confirms `CAP-19`'s observed 60.
- **Sell price is the full build cost, no depreciation** (`0x0040a1e6` prompt, `0x0040a052`
  credit, both `FUN_00405680` through an identity `FILD`/`ftol`). Special planes (class dword 2)
  cannot be sold; two planes must always remain.
- **Starting funds are $0.** `FUN_004113b0` zeroes `nPlayerCash` (`0x0064b788`). The campaign
  starts with two aircraft instead: prebuilt templates 11 and 12, both Devastators, named
  `langui` 511 "Gypsy Magic" and 512 "The Knave". The $250,000 in the image belongs to the
  `fAllowAll` unlock mode, not to the campaign.
- **Ammunition and rockets are free** (disproof). `ORDINANCELAYOUT.SCRIPT` invokes no cost
  callback and no writer of `nPlayerCash` sits on its path.
- **Income, which A5 did not ask for and B13 needs**: a ten-record reward table at `0x0061ae80`
  pays cash and awards five named aircraft on specific mission/objective pairs, once per profile.
  Total campaign cash income is **$140,900**.

**Named gaps.** (1) The reward table's fifth field, a pointer per record into `0x00646384`, is
read by no code; left uninterpreted. (2) The airframe blurbs' `ARMOR: Standard (N/T/W)` triples
still map to nothing, and the decoded flat 60-unit cap rules out the last reading that fit them.
(3) The reward table's mission ids (1, 2, 5, 6, 7, 12, 13, 17, 19, 24) are in the `nMission`
numbering, which A3 must reconcile with the chapter folders and the save's `Persist.NNN` ids.

**Approach.** Ghidra trace from the purchase-prompt string references (700–799) back to the
constants; cross-check any recovered price against the A8 plane-construction capture, which shows
real prices on screen. Extend `docs/formats/vehicle.md` rather than opening a new page.

**Model recommendation.** high — exe tracing; the cross-check against the capture keeps it honest.

**Verify.** Done via the `CAP-40` screenshots (`OriginalScreenshots\Campaign CAP-40 Plane
Construction *.png`): the funds note shows $21,840, the exact decoded `Status.dat` value; the
armor blurb prints $20/5 units and 20 lbs./5 units, the traced $4 and 4 lb per unit; the
Devastator's engine line (Tornado G450) shows $1,700 / 2,000 lbs / 251 mph, matching airframe 5's
decoded base row in all three fields; both airframes' weight capacities (10,100 and 4,160 lbs)
match their stat rows. Every decoded constant visible in the shots agrees.

**⚠ Traps.** ⚠ Do not tune prices to "feel right" if the trace stalls; a missing constant stays a
named gap, per the invented-content ground rule.

## A6 ☑ Behavioral decode of the campaign GUI scripts (cabin, flight check, ammo, campaign intro)

**Goal.** A decode of what `PASSENGERCABIN.SCRIPT`, `FLIGHTCHECK.SCRIPT`,
`ORDINANCELAYOUT.SCRIPT`, `CAMPAIGN.SCRIPT` and `CAMPAIGNINTRO.SCRIPT` actually do: widget
wiring, mailbox/callback flow, screen transitions, and which engine calls they make, at the depth
`docs/formats/instant-action.md` reached for `INSTANTACTION.SCRIPT`.

**Evidence (confidence: traced to code).** Landed as
[`docs/formats/campaign-screens.md`](formats/campaign-screens.md). Every widget, list fill,
transition and engine call of the five scripts is traced to either the script text, its
`LAYOUT.CSV` row, or a `crimson.exe` handler, and checked against the five reference PNGs.

**Approach (as executed).** The script text was paired with the three callback handlers
`crimson.exe` registers for it (`uiData` at `0x004093a0`, `gosCallback` `FUN_00407670`,
`uiControl` `FUN_00404960`). Obfuscated identifiers were deliberately not recovered: widget keys,
callback ids, message ids and langui ids carry the meaning.

**Model recommendation.** high — the obfuscation makes this judgement-heavy.

**Verify.** Done. Per-screen, what is script-proven against what the screenshots corroborate is
tabulated in the page's Evidence section.

**What it settles for Wave C.**

- **C22's cabin-art TODO is answered: it is flat 2D UI art, not a rendered 3D set.**
  `PC_BackGround.png` at `0,0` is colour-keyed (`AlphaType 2`) with the hangar window transparent,
  and one of eleven `PC_P_HANGAR<airframe>.JPG` photographs is drawn behind it at `46,69`. The
  memento is `pc_memento` + `PC_Mementopicframe.png`; the map pins are frames of `PC_mappins.png`,
  one per story chapter reached.
- **C24's plane-change TODO is answered.** The change is disabled on campaign missions **13 and
  17** (hard-coded in `FLIGHTCHECK.SCRIPT` and independently in `uiData` 2018's own test), and on
  any mission while the profile owns fewer than three planes. Those two missions instead **grant** a
  story aircraft from a table at `0x0061ae80`, which also carries the campaign's per-objective
  payouts (input for A5).
- **C25's ammo-description TODO is answered.** Descriptions are two langui strings each, title plus
  body: ammunition `3350`/`3370` with list rows at `3360`, ordnance `3380`/`3410` with list rows at
  `3395`. The ordnance list is **filtered by campaign progress** (a 12-entry availability table at
  `0x00619efc`), so a dropdown row is not an ordnance id.
- **C21** gets the roster capacity (24 profiles, 32-character names) and the original's own name
  validation rule.
- **A4/C23 bonus:** the `brief_c<NN>` briefing state key is `<campaign folder index><mission
  number>`, which reproduces all 24 keys from `cm_sequence.zrd` and closes `briefing.md`'s open
  question about which mission folder each briefing state belongs to.
- **D37 bonus:** the out-of-mission screens drive one shared sound object holding
  `music_splash.wav`, with mailbox `11003`/`11004` as stop/start. No script assigns the cabin a
  track of its own.

**⚠ Traps.** This item informs the screens but does not gate them. What stays for captures: whether
anything in the cabin animates or loops, how the pin frames read on screen, and the disabled-state
art of the Next Mission button when the campaign is finished.

## A7 ☑ Decode the `letterbox` node mechanics and the cutscene `CALLBACK` codes

**Goal.** How the original drives the `letterbox` gamez node during intro cutscenes (who
activates it, what it draws, when it retracts) and what the 8 unanchored `CALLBACK` dispatches in
the intro defs mean, written into the anim-definitions docs.

**Evidence (confidence: traced to an exact mechanism, with the data that proves it).** Landed in
[`docs/formats/anim-definitions/cutscenes.md`](formats/anim-definitions/cutscenes.md).

- **Letterbox is entirely data.** The string `letterbox` occurs nowhere in `crimson.exe`. The node
  is a parentless library root carrying one model of two opaque black quads (material 0, `Colored`
  `rgb(0,0,0)`, alpha 255) at `z = −7.5` in the camera's frame, bit-identical in all eight
  chapters. `zrdr/letterbox.zrd` sets it `ACTIVE` and then re-copies `camera1`'s position and
  orientation matrix onto it every tick under `LOOP{-1}`. It ships `active:false` because that
  definition's `RESET_STATE` asserts `INACTIVE` as its base state, so the gamez simply bakes in
  what the def says. 27 call sites in 18 readers, only one of them an intro: the bars are the
  engine's general cutscene idiom, shared with every mid-mission pickup and hookup.
- **`CALLBACK` is a host notification, and the host is the `landings.zrd` trigger.** Slot 35
  (`FUN_004ec5e0`) calls whatever native function is registered at `anim+0x74`; `FUN_004ee160` is
  the only setter, and exactly one of its 13 call sites installs the mission-script interpreter
  `FUN_0047e080`. All ten codes the intro defs author are read off that interpreter's switch,
  including 20 (suspends the world and objectives updates), 913/914 (park and reveal every AI
  vehicle), 2/1 (chrome off / control and chrome back) and 666/667 (the camera-parameter gate).
- **The intro defs run without a host,** because `StartAnims.zrd`'s loader `FUN_0046c370` starts
  each def with no registration, so their callbacks are no-ops in the original too; the engine
  performs the equivalent state changes imperatively in `FUN_004654e0` immediately before that
  call. The codes remain the authoritative description of the cutscene's shape for D32.

**Verify (done).** All eight `camera1-generic_intro` dispatches are named (`RESET_STATE`: 1, 914,
10, 667; `callback_sequence`: 20, 2, 11, 14), plus C1/M04's ninth (913) and the `gi_1stperson` 666.
Codes **14** and **123** are recorded as named gaps: they reach no case in the interpreter and no
other installable host takes them. The `active:false` question is answered by the def's own
`RESET_STATE`.

**Open questions for D32.** `RESET_TIME`'s detail stays undecoded, so *when* a running letterbox
returns to `INACTIVE` inside a mission is not established (nothing stops the def by name). How the
original's draw traversal reaches a parentless active root was not traced; CSVM must attach its
bars to the camera itself.

**⚠ Traps.** The user's ruling, now recorded in
[`cutscenes.md`](formats/anim-definitions/cutscenes.md): the M0x intro defs must play, never be
suppressed. Any interim change that silences them regresses that ruling.

## A8 ☑ Mint and file the owed captures

**Goal.** `playtest.md` carries CAP items (ids minted with `New-ItemId.ps1 -Kind CAP -Count 5`)
for: the Plane Construction screen with visible prices, the Previous Missions list, the briefing
animation in motion (one C1 mission, full reveal + narration), a C1 story-mission intro cutscene
(letterbox visible, through the handoff), and the cabin screen ambience/idle behaviour. Each CAP
names the plan items it unblocks (A5, C22, C23, D32, E42).

**Evidence (confidence: traced).** Only five static `Campaign *.png` shots exist in
`OriginalScreenshots\`; no capture shows plane construction, the previous-missions list, or any
campaign animation in motion (checked 2026-08-24).

**Approach.** Follow `playtest.md`'s CAP conventions; the user films the original. This item is
filing the requests, not the footage.

**Model recommendation.** medium, low effort — mechanical filing.

**Verify.** All five CAPs are delivered and closed: `CAP-40` (plane-construction screenshots,
consumed by A5's price cross-check), `CAP-41` (previous-missions screenshots, C22's layout
evidence), `CAP-42` (the briefing in motion, the `brief_c61` reveal script on film beat for beat,
which settled the cue-marker ordering C23 reads), `CAP-43` (a story intro through the handoff,
consumed by D32, with the user's verdict that the bars are simply present when the load ends),
`CAP-44` (answered at the controls: the cabin has no idle behaviour, only background music over
static art). `playtest.md` carries none of them any longer.

**⚠ Traps.** ⚠ Footage-derived *measurements* are inadmissible in this repo
(`docs/verification.md`); the captures are for layout, sequence and on-screen values (prices,
labels), not distances or timings used as constants.

# Wave B — campaign model and persistence

## B11 ☑ Profile store + campaign session model

**Goal.** A named profile can be created, listed, selected and deleted, persisted under
`user://Profiles/<name>/`; a selected profile plus a mission id defines a campaign session the
engine can launch (a `--campaign` CLI entry for scripted testing, added to `docs/cli.md`).

**Evidence (confidence: direction-sound).** Persistence precedent: `ScoreStore.cs`
(`user://stunt_scores.json`) and `CustomPlaneStore` (`user://Planes/`). `SessionSpec.cs` is the
closed immutable launch spec with `FromMenu`; `docs/cli.md` has no campaign concept today (checked
2026-08-24). Store schema fields come from A2's structural answers.

**Approach.** Done. `src/Session/CampaignProfileStore.cs` follows the two precedents: plain
System.IO over an absolute directory (engine-free, unit-testable) plus a `UserProfiles()` Godot
resolver, one JSON file per profile at `user://Profiles/<name>/profile.json`. The schema (version
1) carries funds, an owned-plane list (name-referenced into the global `CustomPlaneStore`, never a
copy of it, per the ownership trap below) each with its per-gun ammo and per-pylon ordnance picks,
the mission-result list (A2's best-of-merge fields, minus the two undecoded twelve-byte counter
arrays) and the completed-mission count as the tree position. `CampaignProfileDef.NewProfile`
seeds the traced reset: $0 and the two prebuilt Devastators. `SessionSpec` gained two plain-value
fields, `CampaignProfile`/`CampaignMissionSeq`, parsed from `--campaign=<profile>:<seq>`; no
`SessionMode` change was needed: a bare `--campaign=` rides the existing "content arg with no
other mode vote resolves to Fly" branch, the same way `--stunt`/`--vs` ride it as modifiers.
Building the mission's world from the selection is D31's job, not this store's.

**Model recommendation.** medium — pattern-following with one design seam (the schema).

**Verify.** `dotnet build`/`dotnet format` clean, comment caps clean. The round-trip (create →
write → relaunch → read on a clean `user://`) is
`CampaignProfileStoreTests.RoundTrip_SurvivesASimulatedRelaunch`: a second `CampaignProfileStore`
instance over the same directory
(standing in for a process restart) reads back funds, an owned plane's ammo/ordnance edit and a
recorded mission result exactly as a first instance wrote them. This is an engine-free `dotnet
test` unit, not an in-engine suite: the store is pure file I/O with no world/session dependency,
the same class of check `CustomPlaneStoreTests.cs` already establishes for `CustomPlaneStore`.
`dotnet test` passes 2005/2005, including 20 new store tests and 2 new `SessionSpec` parse tests
for `--campaign=`. **Verified.** `RunTests.ps1` on the merged plan branch: build clean, units
2009/2009, engine suites 93/93 with errors clean, all 16 golden shots hash-identical.

**⚠ Traps.** Profile names are user text entry: they become directory names, so sanitize.
`CampaignProfileStore.DirFor` replaces every character `Path.GetInvalidFileNameChars` rejects,
`Delete_SanitisesTheNameLikeSave`-style. The original's roster screen (reference PNG) allows
deletion, so deleting must not orphan hangar planes: `Delete` removes only the profile's own
directory (`Directory.Delete(dir, recursive: true)`), never `user://Planes/`, proven by
`Delete_RemovesOnlyThatProfilesDirectory`'s sentinel file outside the deleted directory.

## B12 ☑ Campaign progression + cross-mission persistence (`BL-243`)

**Goal.** Completing a mission records its result in the profile, advances the campaign position
(A3's single monotonic counter, no branches), and carries the `PERSIST_LOG` destruction subset into
later missions in the sense `BL-243` describes. Previous Missions replays any recorded mission
without advancing.

**Evidence (confidence: traced, for every rule implemented).** The merge rules are
`saved-games.md`'s mission-result table, field by field (`FUN_00405ce0`); the advance rule and the
`progress + 1` selection bound are `campaign-sequence.md`'s "Progression"; the same-chapter reload
is `saved-games.md`'s `Mission.NNN` section (the engine's backwards walk over `cm_sequence`'s
`campaign` field, which is the world folder, not the act); the 62-def subset and its
"apply on every load, write only on a campaign mission, commit to the save" rule are `BL-243` and
`anim-definitions.md`'s `SAVE_LOG`/`PERSIST_LOG` section; the five aircraft awards and their
per-airframe once-per-profile marker are `hangar.md`'s reward table.

**Approach as built.** Progression is `src/Session/CampaignProgression.cs` over the B11 store,
which gained the fields it needs (schema version 2): mission results as the original's **two
halves** (`MissionRun Latest` and `MissionRun Best`, so a mission attempted but never completed has
the shape the sample profile's last record has), `GrantedAircraft`, `OwnedPlane.Special` and the
persist log. `src/Mech3/CampaignSequence.cs` is A3's decode in code: the 24 entries, each one's
storage address, and `PreviousInSameChapter`, the engine's own backwards walk.

**Seam chosen, and why.** The persist log sits at `AnimRuntime.Destructibles`, and nothing in world
build changed. Capture reads the live `DestructibleRegistry`; apply damages each recorded object
back down through `AnimRuntime.DamageAt`, the same entry a weapon hit takes, so its damage stages
and death sequence run exactly as they did the first time. Two facts forced that shape. First, a
persisted node normally carries **two** destructible pools, the reader's wildcard def and the
compiler's per-instance twin, and only the reader def carries `PERSIST_LOG`: the compiled
extraction keeps `save_log` and drops the other flag. So persistence is a property of the *node*
(any def bound to it carries the flag), read from the pool the hit path resolves to, and the def
name is diagnostic only. Second, the key is the flat gamez node index (`AnimRuntime.IndexMeta`),
which is stable across every build of one chapter's world and unambiguous where names repeat.
Keying the log by chapter *is* the backwards walk: every earlier mission of a chapter has already
merged into that chapter's entry, so "the most recent earlier mission in the same world folder" and
"this chapter's log" name the same state. The merge escalates only, so a replay can never heal what
an earlier mission wrecked.

**Not wired into a session, deliberately.** There is no campaign mission director yet (D31), so
nothing calls capture or apply during a normal launch and the 8-chapter freecam regression cannot
move. D31 calls `CampaignPersistLog.Capture` + `Merge` at mission end and `ApplyTo` after the
bootstrap; the cash half of the reward table is B13's, and this class only banks the money an
attempt reports.

**Model recommendation.** high — the persist-log seam has blast radius into world build.

**Verify.** `dotnet build` clean, `dotnet test` 2022/2022 (16 new units over the progression rules,
the log's chapter scoping and escalate-only merge, and the store round-trip; 3 more over the
shipped `cm_sequence`). The plan's two-mission sequence is the `campaign-persistence` engine suite:
in C1 it destroys three `PERSIST_LOG` objects in `M04` (`aagun33/35/36`) plus one save-only control
(`air_gen`), captures the log, writes it to a profile file, reads it back through a **second** store
instance, builds `M05` from the bootstrap, asserts all three read healthy there, applies the log and
asserts all three are destroyed while the control is untouched and no other chapter's log moved.
Previous Missions' no-advance half is a unit
(`CampaignProgressionTests.ReplayingAFinishedMissionNeverAdvances`), since replaying is a rule over
the profile and needs no world. **Deferred to D31/E41:** an actual mission-to-mission launch through
the director, the commit point (see Traps), and anything in `Mission.NNN` beyond destruction.
**Verified.** `RunTests.ps1` on the merged plan branch (B12 + B13 together): build clean, units
2029/2029, engine suites 94/94 with errors clean (`campaign-persistence` included), all 16 golden
shots hash-identical, so the persist-log seam moved nothing in world build.

**⚠ Traps.** The 62 `PERSIST_LOG` defs are a curated list of fixed world scenery and the flag is a
strict subset of `SAVE_LOG`; the 506 save-only defs (zeppelin turrets, gasbags, cockpit panels) must
not cross a mission boundary, which is what the suite's `air_gen` control proves. `fuelboxconnect*`
is a persisted def, so the original also carries a *running* looping animation across the boundary:
this log carries destruction only, and that gap is recorded rather than papered over. Two facts stay
untested here as `BL-243` says: whether the original commits at damage time or at mission
completion, and a direct A/B separating the two flags. One reading is this item's own: the whole
best-of merge, the money, the advance and the awards are gated on objective bit 0, because the
cumulative mask an award tests against is the best half, which itself only merges behind that gate.

## B13 ☑ Wallet and campaign availability wired into the hangar

**Goal.** Plane Construction opened from the cabin shows the profile's funds; buying is gated by
funds and by the campaign availability threshold; selling credits the wallet at the decoded sell
price; the Instant Action hangar path stays wallet-free.

**Evidence (confidence: traced, seam and numbers both).** `PLAN-hangar.md` decision 2 deferred
economy to campaign; its decision 9 shipped the 11-airframe progress-threshold field unwired.
A5 landed the numbers: `docs/org/hangar.md`, "The economy" and "The campaign wallet". Four of
them change this item's shape. The wallet starts at **$0** with two owned Devastators, so the
first purchase cannot happen before mission 1 pays out. Selling refunds the **full** build cost,
so there is no depreciation rule to write, and a sell/rebuy loop is free by design. Ammunition
and rockets cost nothing, so C25 never touches the wallet. Income is a decoded table, not a
formula: ten mission/objective pairs paying $140,900 in total, plus named unsellable aircraft,
each granted once per profile, which B12's progression record must track alongside the tree
position. The award count is settled at five by A6's independent read of the same table through
`uiData` 2021: missions 2, 7, 13, 17, 19 grant Balmoral, Bloodhawk, Fury, Hoplite, Warhawk
(langui 513-517), with the langui symbol names matching the missions' chapter/mission under the
1-based ordinal (details in `docs/formats/campaign-screens.md`).

**Approach.** Read `PLAN-hangar.md` (completed, in `docs/plans/`) and the `HangarFlow.cs`
architecture entry; add the wallet gate at the existing `Purchase` page seam, parameterized by an
optional campaign context so the IA path is untouched.

**Implementation.** `CSVM/src/UI/HangarCampaignContext.cs` (new) wraps a loaded
`CampaignProfileDef`, reading and writing it through `CampaignProfileStore`'s existing public API
only (`Funds`, `Planes`, `MissionsCompleted`, `Save`); no change to that file. `HangarFlow` gained
one optional constructor parameter, `HangarCampaignContext? campaign = null`, exposed as
`Flow.Campaign`; every existing call site (the IA Build button, the top-level entry) passes
nothing and stays wallet-free by construction. The gate sits at `HangarFlow.Commit()`: after the
existing overweight/no-engine verdict, a non-null `Campaign` additionally refuses an unavailable
airframe or an unaffordable total (in the original's own words for funds, `langui` 1226
"INSUFFICIENT FUNDS"), and on success debits the total and records ownership
(`HangarCampaignContext.Purchase`). `HangarPurchasePage.BuildEnabled`/`Detail` read the same two
checks so the Purchase Now row greys out and states the reason before the press, mirroring how the
original's own gate is the button, not the commit. `HangarFlow.DeleteSaved` (the plane-selection
screen's sell gesture) routes through `Campaign.Sell` when present: it credits the wallet at the
decoded full build cost (no depreciation) and refuses, leaving `Message` set and nothing changed,
for one of the five decoded reward aircraft or when fewer than two planes would remain afterward.

**Threshold semantics.** `HangarCampaignContext.IsAirframeAvailable(airframe)` is
`Profile.MissionsCompleted + 1 >= HangarEconomy.Airframes[airframe].Availability` — the same
comparison `FUN_00410120` makes (`DAT_0064b678 + 1` against the stat table's `+0x14` field),
with `MissionsCompleted` standing in for that global (both are the save's `UIData +0x338`
progress counter, per B11's own field mapping). This is the wire PLAN-hangar Decision 9 left
unconnected outside Instant Action.

**Sell price.** `HangarCampaignContext.SellPrice` prices an owned plane through
`HangarEconomy.Price` over its build in the global `CustomPlaneStore` (the same total the
Purchase screen shows, no depreciation, per A5). The two profile-seeded starters ("Gypsy Magic",
"The Knave") never go through the hangar flow, so they carry no `CustomPlaneStore` entry; `SellPrice`
falls back to the decoded starting spec (airframe from the ownership record, engine 1, two
hardpoints per wing, no armour or guns) for those.

**Wiring contract for `CampaignProfileStore` (not applied — file owned elsewhere).** `OwnedPlane`
has no "this plane cannot be sold" flag. Until B12 lands per-plane ownership tracking, the five
decoded reward-aircraft names (`docs/org/hangar.md`, the mission reward table: Jumping Jane, Blue
Streak, Red Hot Spender, Minx, Accipiter Annie) are recognised by name in
`HangarCampaignContext.SpecialPlaneNames`. The clean fix once B12 owns the file: add a `bool
Special` (or similar) to `OwnedPlane`, set it when a reward plane is granted, and have
`HangarCampaignContext.IsSpecial` read that field instead of the name table.

**Model recommendation.** medium — the seam was designed for this.

**Verify.** `RunTests.ps1` green; scripted assertions: buy refused under-funds, refused
under-threshold, sell credits exactly the decoded price; IA hangar unchanged (golden manifest
untouched). `dotnet build`/`dotnet format` clean, comment caps clean. Unit coverage:
`CSVM.Tests/HangarCampaignContextTests.cs` (`BuyIsRefusedUnderFunds`, `BuyIsRefusedUnderThreshold`,
`BuyWithFundsAndThresholdMetSucceeds`, `SellCreditsExactlyTheDecodedPrice`,
`SellIsRefusedAtTheTwoPlaneFloor`, `SpecialPlanesCannotBeSold`, `InstantActionHangarStaysWalletFree`).
Foreground `dotnet test CSVM.Tests/CSVM.Tests.csproj`: 2016/2016 passed, 0 failed. **Verified.**
`RunTests.ps1` on the merged plan branch (B12 + B13 together, `IsSpecial` reading the record's
`Special` flag): build clean, units 2029/2029, engine suites 94/94 with errors clean, all 16
golden shots hash-identical, so the Instant Action hangar is unchanged.

**⚠ Traps.** Engine power/weight stayed hangar-only by PLAN-hangar's explicit decision; do not
re-open that here.

**Open questions.** (1) The refusal text for an unavailable airframe ("That airframe is not
available yet.") is not a decoded `langui` string; none of the surveyed purchase/sell prompt ids
(700-799) named this case, so the message is plain text rather than an invented string id — a
real `langui` id should replace it if one turns up. (2) The original's dropdown (`FUN_00410120`)
filters an unavailable airframe out of the AIRFRAME screen's list entirely; this item gates only
at commit (and the Purchase row), so an unavailable airframe still appears and can be built up to
the refusal — a smaller, later change if the row-filtering behaviour is wanted too. (3) The
per-plane "special" flag named in the wiring contract above is B12's to add.

# Wave C — the out-of-mission screens

## C21 ☑ Player profile screen

**Goal.** The roster screen per `Campaign Player Profile.png`: name text entry, CONTINUE, roster
list with selection, DELETE PLAYER (confirmed), CANCEL; selecting enters the cabin.

**Evidence (confidence: direction-sound for the layout, traced for the rules).** The reference PNG
gives the arrangement: a name box with CONTINUE beside it, the roster list under it, DELETE PLAYER
and CANCEL along the bottom. A6 (`docs/formats/campaign-screens.md`, "Player profile") gives the
behaviour, and four of its readings are implemented literally: the roster holds 24 profiles of 32
characters; the commit path is one and the same from the START button, from Enter in the name box
and from a double-click on a roster row; DELETE PLAYER raises a confirm before it acts; and the
edit box's own validation is langui 707 (letters, digits and spaces), 212 (the length limit), 200
(an empty name) and 202 (a full roster), all four looked up in `extracted\rof\ui_strings.json` this
pass. The two branches A6 marks unreachable (the load-a-savegame arm, the `crashcheat!` name) are
not reproduced. B11's `CampaignProfileStore` is the store; decision 7's board idiom is the chrome.

**Approach as built.** Two pieces, per the item's two halves.

*The lane.* `CSVM/src/UI/CampaignFlow.cs` mirrors `HangarFlow`'s split (engine-free flow + pages,
launchscreen as renderer and input source) with one difference: campaign screens are a **stack**,
not a fixed order, because the cabin opens a briefing which opens a flight check and each returns
to what opened it. `CampaignPage` carries the defaults, `CampaignPlaceholderPage` covers any screen
not yet registered, and `CampaignCabinPlaceholderPage` is where a selected profile lands until C22
replaces it. The launchscreen edit is one door and its plumbing (below).

*The screen.* `CampaignRosterPage` draws the name field, one row per stored profile, then CONTINUE,
DELETE PLAYER and CANCEL. CONTINUE creates the named profile (`CampaignProfileDef.NewProfile`, the
traced $0 + two Devastators) or continues the one that exists, and opens the cabin; a roster row
selects on the first confirm and continues on a second, the launchscreen's own double-enter idiom,
so no press does two things. DELETE PLAYER is a second stage that opens on the **keep** answer and
deletes through `CampaignProfileStore.Delete`, which takes the profile's directory alone.

*Text entry* is `CampaignTextEntry`, shared with the later screens. `MenuInput` gained `Typed` and
`Erase` (letters, digits and space edge-detected per key, upper case under Shift; Backspace) and
`PadMove`/`PadMoveX`, the pad's own halves of the two cursor axes. While the field is armed the
shell hands the flow the pad axes instead of the combined ones, because W, A, S and D are letters
there; the pad then adds a character with up, removes one with down and steps the last one with
←→, which is `HangarNamePage`'s per-character stepper collapsed onto a single row.

*Art.* `extracted\rimage\` carries no roster-panel art (255 PNGs censused: mission stills, HUD
fonts, main-menu and splash backdrops, no `cm_*`), so the screen wears the existing board styling.
Nothing was drawn and labelled as original. The art seam itself is wired: `ICampaignPage.Art` and
`RowArt` reuse the hangar's own art column through the launchscreen's `PageArt`/`PageRowArt`, so
C22's cabin art needs no launchscreen edit.

**How a page plugs in (the wiring contract for C22-C25).** Follow this verbatim; none of it touches
`LaunchMenu.cs`.

1. Add one file, `CSVM/src/UI/Campaign<Screen>Page.cs`, with
   `public sealed class Campaign<Screen>Page : CampaignPage`. Override `Screen`, `Title`,
   `RowCount` and `RowText(int)`; override `Detail(int)`, `Footer`, `OpeningRow`, `Step(int,int)`,
   `Accept(int)`, `Back()`, `Art`, `RowArt(int)` and `TextEntry` as the screen needs them. The
   constructor takes the flow: `public Campaign<Screen>Page(CampaignFlow flow) : base(flow) { }`.
2. Add one line to `CampaignFlow.Registry`:
   `[CampaignScreen.<Screen>] = flow => new Campaign<Screen>Page(flow),`. C22 **replaces** the
   `[CampaignScreen.Cabin]` line and deletes `CampaignCabinPlaceholderPage.cs`.
3. Navigate with the flow's API from inside `Accept`: `Flow.GoTo(CampaignScreen.X)` opens a screen
   (returning to it if it is already open, so RETURN TO CABIN never stacks a second cabin),
   `Flow.Cancel()` ends the flow for the launchscreen, `Flow.SelectProfile(def)` seats the profile
   and opens the cabin, `Flow.FocusRow(n)` moves the cursor, `Flow.SetMessage(text)` puts a refusal
   on the screen's error line. Returning `false` from `Back()` leaves the screen; returning `true`
   means the page consumed the press (a confirm stage closing, a text field disarming).
4. Read state through `Flow.Profile` (the selected `CampaignProfileDef`, null only on the roster),
   `Flow.Store`, `Flow.Roster` and `Flow.Strings` (langui, always with a fallback string).
5. A screen that types takes a `CampaignTextEntry`, returns it from `TextEntry`, and arms it on the
   press that enters the field. The flow routes the keyboard and the pad into it; the page never
   reads input itself.
6. Screenshot coverage: add a value to `LaunchMenu.OpenCampaignAid`'s list only if the screen needs
   a scripted state; a page reached by walking the flow needs no new flag at all.

**Integration (the shell step that applied C22-C25, D31 and D37's wiring contracts).** Every
contract those items left is now wired in `LaunchMenu.cs`, `Launcher.cs` and `GameSession.cs`'s
campaign mission-end path, and none of them needed a new seam:

- **The hangar door.** `CampaignExit.OpenHangar` opens `HangarFlow` over a `HangarCampaignContext`
  on the flow's own profile with `_hangarReturn = Screen.Campaign`; `CloseHangar` calls
  `Flow.Resume()`, built or cancelled alike, which re-reads the profile onto the cabin.
- **FLY MISSION.** `CampaignExit.FlyMission` saves the profile, stops the score, and hands the host
  a `LaunchMenu.CampaignLaunch` (profile, seq, the pilot's stock node, its `CustomPlaneStore` build
  where it has one, and `CampaignLoadout.For`'s fit). `SessionSpec.FromCampaign` turns that into a
  campaign spec the way `FromMenu` turns a pick into a flight one; the chapter and mission stay
  `CampaignDirector.ResolveSpec`'s to settle, so a cabin launch and a `--campaign=` command line
  resolve a story position in the same place.
- **The return.** `CampaignDirector.MissionEnded` reaches `Launcher` through
  `LauncherContext.ReturnToCabin`, which queues the profile name; the next `_Process` frees the
  session and calls `LaunchMenu.OpenCampaignCabin`, which re-reads the profile the director just
  wrote and enters the menu score.
- **The briefing.** The launchscreen advances `page.Advance(delta)` per frame while the briefing
  shows and plays `NarrationWav` through a board-owned `AudioStreamPlayer` on the Master bus,
  restarting on a `NarrationStarts` change and stopping when the screen leaves.
- **Music.** `Launcher` owns the player and a process-lifetime `SoundArchive`, enters
  `MusicState.Menu` on every board show and on a cabin return, stops in `BeginLaunch`, and hands
  the channel to `CampaignDirector`, whose sound-group executor routes a `mu*` name to `Cue` and
  whose step runs the decoded proximity scan and player-damage ping. Instant Action names no track
  and so stays silent.
- **Art.** The cabin draws `PC_BACKGROUND.PNG` and the ammo screen the airframe's own frame of
  `OL_PLANEDIAGRAMSTOP.PNG` / `OL_PLANEDIAGRAMSFRONT.PNG`, all three through `PngImage`. The
  `PC_P_HANGAR<n>.JPG` photographs remain the named gap C22 recorded: no engine-free JPG decoder
  exists and this step did not write one.
- **The store seed.** `NewProfile` now seeds `WingmanPlane` at 1, so a fresh profile's wingman
  flies The Knave rather than the pilot's own aircraft. C24's second open question (the ammo and
  ordnance arrays defaulting to C# zero rather than the traced stock fit) is deliberately left
  alone: `CampaignLoadout` reads an unset pylon as "leave the base's fit", which is the traced
  universal high-explosive load, so the arrays as they stand already fly correctly.

**Model recommendation.** medium.

**Verify.** `dotnet build` clean (0 warnings), `dotnet test` **2053/2053** (24 new units over the
flow's stack navigation, the roster screen's create/select/refuse/delete rules and the text field's
alphabet and caps). Scripted screenshots of the three states through the existing `--menu=` +
`--screenshot=` pattern, run windowed per `docs/verification.md`: `--menu=campaign-empty` (no
players), `--menu=campaign-roster` (two), `--menu=campaign-entry` (a name mid-entry, caret showing),
plus `--menu=mode` for the new door. **No golden was added or re-pinned:** `analysis/goldens/`
holds world/flight shots only, no menu has ever been pinned there, and a menu shot's hash would
move on every unrelated label edit. The three campaign aids run over a scratch profile directory
rather than `user://Profiles`, so the shots are machine-independent and cannot touch a real
campaign. **Verified.** `RunTests.ps1` on the plan branch (C21 with the shared flow seam and
D37): build clean, units 2053/2053, engine suites 95/95 with errors clean, all 16 golden shots
hash-identical.

**⚠ Traps.** Deletion is destructive: confirm, and honor B11's ownership rule for hangar planes.
Both hold here, and `DeletingIsConfirmedAndTakesOnlyThatProfile` is the proof, with a sentinel file
beside the profile store standing in for a hangar plane.

## C22 ☑ Campaign cabin screen

**Goal.** The cabin hub per `Campaign Cabin.png`: Next Mission (jumps to briefing of the tree's
current mission), Previous Missions (list of finished missions, pick one → its briefing),
Plane Construction (into B13's hangar), Return to Main Menu. Change Memento is not shipped
(decision 3).

**Evidence (confidence: direction-sound).** Reference PNG; A3 gives Next Mission's source of
truth; B12 gives the finished-missions list; A6/`PASSENGERCABIN.SCRIPT` gives the original's
wiring (`docs/formats/campaign-screens.md`). The ambience question is answered at the controls
(`CAP-44`, closed): the cabin has NO idle behaviour, only background music over static art, which
matches A6's flat-art decode; the only ambience work this item owes is whatever music D37 routes
to the screen. `CAP-41` (screenshots in `OriginalScreenshots\Campaign CAP-41 Previous Mission
*.png`) gives the Previous Missions layout: a scrapbook-style page titled by the pilot, a
scrollable list with one row per finished mission (airframe silhouette, mission long name, area
name, plane-flown name), and VIEW SELECTED / REPLAY MISSION / RETURN TO CABIN buttons; the shots
show the decoded award names (Minx, Accipiter Annie) and act naming (Rocky Mountains) in use.

**Approach.** Board-idiom page; the four buttons route into existing flows (briefing C23, hangar
B13, launchscreen). The cabin scene is flat rof art per A6: `PC_BackGround.png` with the
airframe photograph behind the window hole, the memento frame, and the chapter-count map pins; no
3D set exists, so no fallback decision is needed.

**Approach as built.** Two page files, following C21's plug-in contract verbatim: no
`LaunchMenu.cs` edit beyond registering them.

- **`CSVM/src/UI/CampaignCabinPage.cs`** replaces the placeholder at `[CampaignScreen.Cabin]`.
  Four rows, the original's own order minus the unshipped CHANGE MEMENTO/deactivated SAVE GAME:
  **NEXT MISSION** calls `Flow.SetMission(CampaignProgression.NextMissionSeq(profile))` then
  `Flow.GoTo(CampaignScreen.Briefing)`, or, once `CampaignProgression.Complete(profile)` (the
  decoded `MissionsCompleted >= 24` gate behind `uiData` 2600), refuses in a plain sentence and
  leaves the screen where it is, since no `docs/formats/strings.md` id covers that reason.
  **PREVIOUS MISSIONS** is `Flow.GoTo(CampaignScreen.PreviousMissions)`. **PLANE CONSTRUCTION** is
  `Flow.Request(CampaignExit.OpenHangar)`, the shell wiring contract below. **RETURN TO MAIN
  MENU** is `Flow.Cancel()`.
- **`CSVM/src/UI/CampaignPreviousMissionsPage.cs`** is the new `[CampaignScreen.PreviousMissions]`
  page and the enum's one new member. Rows are `CampaignProgression.CompletedSeqs(profile)`
  (already `seq`-ordered) plus VIEW SELECTED, REPLAY MISSION, RETURN TO CABIN. A mission row's own
  press marks it the one the two buttons act on (defaulting to the first finished mission when
  nothing has been picked, so a press before ever selecting still does something). VIEW SELECTED
  is a no-op per the goal statement: the detail line under the focused row already carries the
  CAP-41 layout (`{area}   ·   {plane flown}`, area from `IDS_MISSIONAREA` 1220+seq/5, plane from
  the mission's `Best.PlaneName`), so a second surface would only restate it. REPLAY MISSION is
  `Flow.SetMission(seq)` + `Flow.GoTo(CampaignScreen.Briefing)`, which records a new attempt
  through `CampaignProgression.Record` without advancing (that class's own no-advance-on-replay
  rule, already covered by `CampaignProgressionTests.ReplayingAFinishedMissionNeverAdvances`).
  RETURN TO CABIN is `Flow.GoTo(CampaignScreen.Cabin)`, which lands on the cabin already on the
  stack rather than a second copy, per the flow's own `GoTo` contract.
- **Airframe silhouette and the CAP-41 fan-of-cards "Starting My Career" first row are not
  reproduced.** The task scope is "one row per finished mission, ordered by seq"; the silhouette
  needs `FC_PlaneIcons.png` framed by airframe, which the art gap below also blocks, and the
  pre-career placeholder row is scrapbook furniture outside this item's four-function goal.

**Cabin art: a confirmed, named gap, not an invented fallback.** Every file A6 named exists on
disk (`Z:\CSVM\extracted\rof\ASSETS\GRAPHICS\`: `PC_BACKGROUND.PNG`, `PC_MEMENTOPICFRAME.PNG`,
`PC_MAPPINS.PNG`, `PC_P_HANGAR0.JPG`..`PC_P_HANGAR10.JPG`), but every one of them is PNG or JPG,
and the engine-free `HangarArt`/`TgaImage` seam C21 generalised decodes TGA only
(`TgaImage.Decode`'s own header check). `CampaignCabinPage.Art` resolves the pilot's own airframe
photo path exactly the way `HangarAirframePage.BlueprintFor` resolves a blueprint's, and calls
`TgaImage.TryLoad` on it; the call is correct and the seam works, but decode returns null for
every cabin file today, so no picture draws. `CampaignCabinPage.MapPinCount` (one per story
chapter reached, `NextMissionSeq(profile) / 5 + 1`) is kept as a pure, unit-tested stand-in for
the pixel composition (background + window-hole photo + memento frame + pins) that a PNG/JPG
decoder would need to actually draw; extending `TgaImage` or adding a sibling decoder is follow-up
work this item does not do. The memento frame's own picture cannot be resolved at all yet:
`CampaignProfileDef` carries no memento-filename field (`uiData` 2150's `UIData +0x344`,
`docs/formats/saved-games.md`), which is consistent with decision 3 deferring Change Memento
whole, but means the frame has no source image even once a decoder exists.

**Wiring contract for the shell (not applied here; `LaunchMenu.cs` is out of bounds for this
item).**

1. **Opening the hangar from PLANE CONSTRUCTION.** `Flow.Exit == CampaignExit.OpenHangar` is the
   whole signal. The shell builds a `HangarCampaignContext` (B13) over `Flow.Profile` and
   `Flow.Store`, opens `HangarFlow` with it, and on the hangar's own exit calls `Flow.Resume()`,
   which re-reads the profile so a purchase or sale shows on the cabin immediately. This mirrors
   how `OpenCampaignAid` already builds `CampaignFlow` itself in `LaunchMenu.cs`.
2. **A screenshot aid for the cabin and the previous-missions list.** `LaunchMenu.OpenCampaignAid`
   already covers `campaign`, `campaign-empty`, `campaign-roster`, `campaign-entry` over a scratch
   profile store. Two more values in that same list would reach this item's screens without a new
   mechanism: `campaign-cabin` (seed one scratch profile with a few missions completed, land on
   `CampaignScreen.Cabin`) and `campaign-previous` (the same profile, `Flow.GoTo
   (CampaignScreen.PreviousMissions)`). Neither exists yet; this item verified through the
   `CampaignCabinPageTests`/`CampaignPreviousMissionsPageTests` units instead, per this item's
   verify plan below.
3. **The JPG/PNG art decoder.** Not this item's job (`LaunchMenu.cs`/`HangarFlow.cs` are both out
   of bounds), and not attempted: a real decoder is enough work to be its own item. Filed as an
   open question below rather than a backlog ID, since C23-C25 will hit the same gap on their own
   `rimage`/`rof` art and a single decoder should serve all of them.

**Model recommendation.** medium.

**Verify.** `dotnet build`/`dotnet format --verify-no-changes` clean (0 warnings), comment caps
clean (`CheckCommentCaps.ps1 -Summary`). `dotnet test` **2073/2073** (21 new units:
`CSVM.Tests/CampaignCabinPageTests.cs` covers the four routes, the finished-campaign refusal, the
next-mission seq under progress, and `MapPinCount`'s chapter arithmetic across every act boundary;
`CSVM.Tests/CampaignPreviousMissionsPageTests.cs` covers the seq-ordered finished list excluding
an unfinished attempt, REPLAY MISSION on an explicit pick and on the no-pick fallback, the refusal
with nothing finished, VIEW SELECTED's no-op, and RETURN TO CABIN not stacking a second cabin).
One pre-existing unit, `CampaignFlowTests.TheCabinPlaceholderReturnsToTheMainMenu`, tested the
placeholder's `OpeningRow` convention directly and was removed as superseded by
`CampaignCabinPageTests.ReturnToMainMenuCancelsTheFlow`;
`CampaignFlowTests.UnregisteredScreensDrawThePlaceholder` gained the `PreviousMissions`
registration assertion. **Scripted screenshots were not taken**: the aid values that would reach
these two screens don't exist yet (wiring contract item 2, a `LaunchMenu.cs` edit out of bounds
for this item), so the route/gating/list-content rules above are covered by the unit tests
instead, per this item's own verify plan. **Verified.** `RunTests.ps1` on the plan branch (D31,
D36 and C22 together): build clean, units 2092/2092, engine suites 98/98 with errors clean, all
16 golden shots hash-identical (one `c3-island` timeout under machine contention re-ran clean).

**⚠ Traps.** `BL-181` defers HUD/scoreboard chrome "pending the menu hub"; this screen is that
hub, so expect that item to reopen against the styling landed here. Do not silently restyle HUD
chrome in this item.

**Open questions for follow-up.** (1) A JPG/PNG decoder so `CampaignCabinPage.Art` and a future
`RowArt` silhouette actually draw; candidates are extending `TgaImage` or a sibling class beside
it. (2) The memento-filename field `CampaignProfileDef` would need if Change Memento is ever
un-deferred. (3) The `campaign-cabin`/`campaign-previous` `--menu=` aid values named above, for
whoever next edits `LaunchMenu.cs`.

## C23 ☑ Mission briefing screen

**Goal.** The briefing per `Campaign Briefing.png`: the mission map with flags, the objectives
note filled from `MSG_BRF_*`, the briefing animation (progressive reveal) with narration audio,
and REPLAY BRIEFING / RETURN TO CABIN / GO TO FLIGHT CHECK.

**Evidence (confidence: traced to the data for the mechanism, direction-sound for the chrome).**
A4's decode gives the layout, the 12-opcode vocabulary and the state-to-wav pairing. Two questions
A4 left open are settled here against the files, and neither needed `crimson.exe`.

*The marker source.* `WaitForMarker`'s cue points are the narration wav's own RIFF `cue ` chunk,
and the extraction preserves it: all 24 `extracted\soundsh\*_briefing.wav` carry one, and a state
waits on markers `0`..`n-1` for a wav with exactly `n` points on 23 of the 24 (`brief_c81` uses 9
of `c5-MH-m1`'s 10, waiting on marker 8 twice). ⚠ **The number indexes the points sorted by sample
offset, not by cue id**: the ids run `1..n` in file order but 13 of the 24 wavs store their offsets
out of time order, so reading the id as the index runs a reveal backwards. `CAP-42` confirms both
on screen, `brief_c61` executed literally in file order with its beats at the sorted offsets.
There is therefore **no degraded mode in practice and no invented timing anywhere**: every
duration is an authored constant and every marker is a measured cue point. The degraded path is
built and tested regardless (a wav with no cue chunk releases every marker at once, so the map
finishes and the narration plays over it), because it is what a partial extraction earns.

*The objectives note.* An `Objective id index` opcode indexes the mission's `objectives.zrd`
`IDENTITY` entries **that carry a `MSG_BRF_*` key, ordered by priority**, 0-based. The check is
exact: on all 24 missions the state's `Objective` count equals that list's length. Two other
readings are disproven. Counting keyless `IDENTITY` entries as lines puts an empty line first on
`C5/M04`, whose one bound line is "1) Payback time!". File order instead of priority order reads
"Dock with the PANDORA" before a mission's middle objectives, while priority order reproduces the
numbering the strings themselves carry ("1)", "9)", "10)"). ⚠ One block authors **two** `IDENTITY`
entries (`C4/M05`'s `OBJECTIVE23`), so a keyed view of a block silently drops one; collect every
entry.

**Approach as built.** One page file plus one `Registry` line, per C21's contract, and three
engine-free helpers.

*The page.* `CSVM/src/UI/CampaignBriefingPage.cs` resolves everything from
`CampaignFlow.MissionSeq` alone: `cm_sequence` gives the storage address, `brief_c%d%d` gives the
state, the state gives the map bitmap and the narration name, `sounds.zrd`'s `SETS` turns that name
into the wav file, and the mission's own `objectives.zrd` gives the note. Nothing is computed from
the story position. Rows 0 to 2 are REPLAY BRIEFING, RETURN TO CABIN and GO TO FLIGHT CHECK, with
the revealed note lines under them, so the buttons' indices never move under the cursor while the
note fills in. Labels come from `messages.json`'s own `MSG_BTN_*`, falling back to the literal;
an unresolved objective key shows as the raw `MSG_*` key. A null `DataRoot` or a half-written
extraction leaves the screen on its three buttons rather than throwing.

*The reveal.* `CSVM/src/UI/BriefingScript.cs` is the reader (`BriefingDialog`, `BriefingState`,
`BriefingStep`) and the interpreter (`BriefingReveal`), both engine-free. The interpreter runs the
beat sheet against a clock the caller advances, blocking on `Wait`'s authored seconds and on
`WaitForMarker`'s cue times, and keeps each element's opacity, rotation and position as its tweens
land. Elements are exposed in placement order, which is the draw order `CAP-42` shows (photographs
are never turned off and stack newest over oldest), and a `Move`'s own first path point wins over
its `Pict` position where the two disagree. ⚠ The states sit at the reader's **top level**, beside
`BRIEFINGDIALOG` rather than inside it, and neither that list nor an objective block is strictly
key/value: a bare flag between two entries shifts every pair after it, so both walks step by what
is there rather than by two.

*The note.* `CSVM/src/UI/BriefingObjectives.cs` is the `IDENTITY` reading above, taking the reader
list rather than a path so it tests without an extraction.

*Audio.* **No 2D/UI playback channel exists.** `SoundArchive` decodes a WAV to an
`AudioStreamWav`, but the only players are `WorldSounds`' 3D emitters and `FlightAudio`, both
session-owned and both wrong for a menu. The page therefore names what it needs and plays nothing:
`NarrationWav` is the file to play and `NarrationStarts` counts how many times the script has asked
for it. The shell wiring is the contract below.

*Art.* Map and pin art is `rimage` PNG, and nothing decoded PNG off engine. `CSVM/src/Mech3/PngImage.cs`
covers exactly what that extraction ships (8-bit, non-interlaced, colour types 2 and 6, which is
all 254 files) and hands back a `TgaImage` through a new `TgaImage.FromRgba`, so the art reaches
the screen through the same `ICampaignPage.Art`/`RowArt` seam C21 wired and **no launchscreen edit
was needed**. The page's `Art` is the state's own `BACKGROUND_IMAGES` bitmap; a note line's
`RowArt` is the `OBJPIN<n>` picture matching its `ZEPTEXT<n>`, which is how all 24 states pair
them. `CSVM/src/Mech3/WavCues.cs` is the cue reader, separate from `SoundArchive` because a menu
page that only needs timings must stay engine-free.

**Shell wiring contract (owed by whoever next edits `LaunchMenu.cs`).** Three lines, no new
seams; nothing in this item touched the launchscreen.

1. *Clock.* While `Screen.Campaign` is showing and `_campaign?.Page` is a `CampaignBriefingPage`,
   call `page.Advance(delta)` once per frame from the menu's `_Process`. Without it the reveal
   stands at its first marker, which is the honest state of a screen with no clock.
2. *Narration.* Watch `page.NarrationStarts`. When it changes and `page.NarrationWav` is not empty,
   restart playback: `SoundArchive.Find(page.NarrationWav, looped: false)` into a menu-owned
   `AudioStreamPlayer` on the Master bus, stopping the previous one. Stop it when the screen
   closes. This is the one piece of the item a page cannot own, because a page holds no Godot node.
3. *Screenshot aid.* `OpenCampaignAid`'s list needs one more value, `campaign-briefing`: build the
   aid store as `campaign-roster` does, `SelectProfile` the seeded profile, `SetMission(0)`,
   `GoTo(CampaignScreen.Briefing)`, then advance the page by a `--menu-at=<seconds>` amount before
   the shot so a stage can be framed. One `--menu=` value plus a seconds argument covers every
   reveal stage; without it the shot is always the opening frame.

**Model recommendation.** medium.

**Verify.** `dotnet build` clean (0 warnings, solution-wide), `dotnet test` **2096/2096** in the
foreground, 43 of them new: the interpreter's beat order, marker gating, `Wait` blocking, tween
values and the move-path rule; the reader over a hand-authored dialog; the note's priority order,
its keyless-entry and double-`IDENTITY` rules and its raw-key fallback; the cue reader's ascending
order and its empty cases; the PNG decoder's filters and its refusals; and the page's state,
narration, map, note, pin art and three button routes, including a census over all 24 missions
asserting the `Objective`-count invariant. Data-backed tests run under `CSVM_DATA_ROOT` and report
**9 skipped** without an install rather than passing on nothing. `CampaignFlowTests`'
unregistered-screen case moved to `Ammo`, since `Briefing` now has a page. Audio was **not**
verified from `.scratch/logs/`: with no playback path built (see the contract), there is nothing to
log yet, and the reveal is asserted directly instead, which is silent but not blind. No screenshot
was taken: the aid needs the `--menu=` value above and `LaunchMenu.cs` is out of this item's
boundary. **Verified.** `RunTests.ps1` on the plan branch with all of Wave C merged: build clean,
units 2157/2157, engine suites 98/98 with errors clean, all 16 golden shots hash-identical.

**⚠ Traps.** ⚠ Capture-derived timings would be TUNE, but none are used: every duration is an
authored constant and every cue time is measured off the wav. String fallback holds: without
`messages.json` the screen shows raw `MSG_*` keys, tested. The reveal takes about two minutes of
mission time and blocks on authored `Wait`s, so a caller cannot jump its clock in one step and
expect a finished map; advance it in frames.

## C24 ☑ Flight check screen

**Goal.** Per `Campaign Flight Check.png`: mission title, PILOT and WINGMAN rows (plane name,
silhouette, numbered gun list with ammo types, numbered rocket list), Change Ammo per row (into
C25), the objectives note, RETURN TO BRIEFING / FLY MISSION; plane change offered when the
mission allows it.

**Evidence (confidence: traced).** Reference PNG; `docs/formats/campaign-screens.md`'s "Flight
check" section (landed by A6) traces every row this item draws: the two slots (`-1` pilot, `-2`
wingman), the title (`uiData` 2009, langui `3450 + seq`), the plane line (`uiData` 2011), the eight
gun rows (group `row>>1`, blank when the group has no gun or, on the odd row, is not twinned) and
eight rocket rows (pylon cell `row`, blank when the cell holds ordnance id 11 or the cell does not
exist), the wingman half gated by `cm_sequence`'s per-entry wingman flag, and the plane-change gate.
**The plane-change TODO is resolved:** the two rules are `docs/formats/campaign-screens.md`'s
"Plane change" paragraph, cross-checked against A3/A5's reward-table reading — barred on ordinals
13 and 17 (the two story-aircraft grants) and whenever the profile owns fewer than three planes;
the data home is the same table A5 already decoded (`docs/org/hangar.md`, the mission reward
table), not a new lookup. Ammunition/ordnance short names are `docs/formats/loadouts.md`'s own
`selectable.gun_ammo`/`selectable.pylon_ordnance` labels, addressed by the same langui ids
campaign-screens.md cites (`3360 + ammo`, `3395 + ordnance`); the objectives note follows
`docs/formats/objectives.md`'s "IDENTITY and the objectives display" rule (one row per unique
priority, sorted, `MSG_key` resolved through `messages.json`).

**Approach as built.** One file, `CSVM/src/UI/CampaignFlightCheckPage.cs`, plus the
`CampaignFlow.Registry` line, per C21's wiring contract. The row list carries only the screen's
actionable items (PILOT/WINGMAN heading, CHANGE AMMO, CHANGE PLANE, RETURN TO BRIEFING, FLY
MISSION); each heading row's `Detail` carries its plane's dense eight-row gun/rocket block, and
every row's `Detail` also carries the objectives note, so it stays visible regardless of focus —
the same row/Detail split `CampaignRosterPage` uses for its own descriptive text.

A plane's guns and hardpoints resolve with one precedence rule, used for both the gun list and the
pylon-existence test: a hangar build under `CustomPlaneStore` by the plane's name wins when one
exists; otherwise the airframe's `stock_loadouts.json` fit stands in. This covers all three cases
the profile can hold a plane in: a player-built aircraft, one of the two seeded starters (`NewProfile`
gives them no `CustomPlaneStore` entry), and a granted reward aircraft (copied from the stock
airframe record per campaign-screens.md's "What 2021 does there"), none of which own a hangar
build. Hardpoint left/right counts reuse `HangarFlow.StockWingCounts` rather than re-deriving the
fill-order-to-wing split.

**CHANGE PLANE's own design.** `CampaignScreen` carries no picker-screen slot, and this item's
boundary does not add one (it cannot edit `CampaignFlow.cs` beyond the Registry line). CHANGE
PLANE therefore cycles the slot's plane through `Profile.Planes` on the row's stepper (←→),
writing `SelectedPlane`/`WingmanPlane` and saving immediately through `Flow.Store`. This is a
judgement call, not a decoded behaviour: the original opens a distinct `PlaneSelection` screen
(campaign-screens.md's screen-flow table). A future item can promote this to a full picker
(silhouettes via `PlanePickerRoster`, precedent in C21) without changing this row's contract
(`Flow.Profile.SelectedPlane`/`WingmanPlane` plus a `Flow.Store.Save`).

**Wiring contract: FLY MISSION's handoff (not applied — the shell/director's job).** FLY MISSION
calls `Flow.Request(CampaignExit.FlyMission)` and leaves the flow standing, exactly like
`CampaignExit.OpenHangar`'s existing contract. The shell that reads `Exit == FlyMission` must:

1. Launch a campaign session for `Flow.Profile.Name` and `Flow.MissionSeq`, the two fields
   `SessionSpec`'s `--campaign=<profile>:<seq>` already parses (B11).
2. Bind the pilot's loadout through `Loadout.Bind`, sourced from `Flow.Profile.Planes[SelectedPlane]`:
   its `Airframe` picks the built model (via a `CustomPlaneStore` lookup by name, falling back to
   the stock `LoadoutDef` exactly as this page's own `ResolveGuns`/`ResolveHardpoints` do), and its
   `Ammo`/`Ordnance` arrays are the per-slot/per-pylon picks to carry into the bound `GunGroup`/
   `Hardpoint` records in place of the stock ammo/ordnance defaults.
3. When the mission's wingman flag is set, bind the wingman's aircraft the same way from
   `Flow.Profile.Planes[WingmanPlane]`, registered under the `wingman_1` name `saved-games.md` and
   campaign-screens.md's FLY MISSION paragraph both name.
4. `CampaignPersistLog.ApplyTo` (B12) runs after the bootstrap and before either bind, per D31's own
   contract; this item does not touch it.

**Open questions, recorded rather than patched here.** (1) `CampaignProfileDef.NewProfile`
(B11) seeds `WingmanPlane` at 0, the same index `SelectedPlane` starts at, rather than 1 ("The
Knave"); a fresh profile's WINGMAN row therefore reads the pilot's own plane until the player
changes it. (2) `NewProfile`'s `OwnedPlane.Ammo`/`Ordnance` arrays default to C# zero (`slug`,
`Armor-piercing rockets`) rather than the traced stock fit (`slug`, `wep_06`/High explosive
everywhere); this page renders whatever the profile stores, so a never-touched starter's rocket
row currently shows "Armor-piercing" rather than "High explosive" until C25 (or a B11 revisit)
seeds it correctly. Neither is this item's file to fix (`CampaignProfileStore.cs` is "the store").

**Model recommendation.** medium.

**Verify.** `CSVM.Tests/CampaignFlightCheckPageTests.cs`, 16 tests: row composition with and
without a wingman (`ThePilotRowShowsTheSelectedPlaneAndItsChangeAmmoRow`,
`WithNoWingmanFlagTheWingmanRowsAreAbsent`, `WithTheWingmanFlagSetTheWingmanBlockAndItsChangeAmmoRowAppear`),
the eight-row blank-is-data rule (`EightRowGunAndRocketListsBlankRatherThanPad`), a custom build
overriding the airframe's stock fit (`ACustomBuildsOwnGunsOverrideTheAirframesStockFit`), the
plane-change gate on ordinals 13/17 and under three planes
(`ChangePlaneIsBarredOnOrdinals13And17AndUnderThreePlanes`, four cases), the stepper writing and
saving the pick (`ChangePlaneStepsThroughTheOwnedPlanesAndSaves`), the ammo route
(`ChangeAmmoSetsTheFlowsSlotAndOpensAmmo`), RETURN TO BRIEFING and FLY MISSION
(`ReturnToBriefingNavigatesBack`, `FlyMissionRequestsTheExit`), plus two `[ExtractedDataFact]`
tests against the real install (`TheWingmanFlagAgreesWithCampaignSequenceForARealMission`,
`TheObjectivesNoteListsNumberedLinesForARealMission`). Foreground `dotnet test
CSVM.Tests/CSVM.Tests.csproj`: 2069/2069 passed, 0 failed, 0 skipped. `dotnet build`/`dotnet
format` clean, comment caps clean.

No scripted screenshot was taken: `LaunchMenu.OpenCampaignAid`'s aid list
(`"campaign"`/`"campaign-empty"`/`"campaign-roster"`/`"campaign-entry"`) is a literal set inside
`LaunchMenu.cs`, off limits to this item's boundary, and none of the four existing aids reaches
the flight check screen (they all stop at the roster). **Described for the orchestrator instead:**
a `"campaign-flightcheck"` aid, added the same way `"campaign-roster"` is, that seeds one profile
via `AidProfileStore`, calls `flow.SelectProfile` then `flow.SetMission(0)` then
`flow.GoTo(CampaignScreen.FlightCheck)`, over a scratch profile directory exactly like the other
three aids use, so the shot stays machine-independent.

**Verified.** `RunTests.ps1` on the plan branch with all of Wave C merged: build clean, units
2157/2157, engine suites 98/98 with errors clean, all 16 golden shots hash-identical.

**⚠ Traps.** The silhouette per airframe: reuse the hangar's plane preview path rather than new
art — done via the same `PX_<n>_BLUEPRINT.TGA` path `HangarAirframePage.BlueprintFor` reads,
loaded independently rather than by importing that page. Ammo lists number to 8 slots with blanks;
blanks are data (absent guns / a pylon past the hardpoint count / ordnance id 11), never padding to
invent — the eight-row test above (`EightRowGunAndRocketListsBlankRatherThanPad`) is the proof.

## C25 ☑ Ammo selection screen

**Goal.** Per `Campaign Ammo Selection.png`: ammunition pick per gun caliber group, rocket pick
per underwing hardpoint pair, the description text panel, top/bottom plane views, ACCEPT/CANCEL
LOADOUT; usable for the pilot's and each wingman's plane; the choice persists in the profile and
binds in flight.

**Evidence (confidence: traced for the base).** `Loadout.cs`/`WeaponDefs.cs`/`WeaponBench.cs` and
the hangar's guns/hardpoints pages already model calibers, pylons and fills;
`docs/formats/loadouts.md` documents resolution rules and confirms no on-disk player-loadout
format (persistence goes through B11's store, informed by A2). Ammo descriptions: **resolved by
A6.** `docs/formats/campaign-screens.md` "Ammo selection": ammunition is per gun group (four
dropdowns, one per `CustomPlaneDef.Guns` slot, greyed when the slot's gun id is 5); ordnance is
per pylon (eight cells, four per wing, deactivated past that wing's hardpoint count); description
strings are ammunition title/body `3350`/`3370` with list rows `3360`, ordnance title/body
`3380`/`3410` with list rows `3395`; the ordnance dropdown row is a position in a 12-row table
filtered by campaign progress against a per-row mission threshold (AP/HE 1, Flak 2, Sonic 8,
Flash/Smoke/Choker 7, Rear flash 12, Beeper/Seeker 17, Torpedo 20, None 1), not an ordnance id.

**Implementation.** `CSVM/src/UI/CampaignAmmoPage.cs` (new), one `Registry` line in
`CampaignFlow.cs`. Fourteen rows: four ammo groups, eight pylon cells (0-3 the left wing, 4-7 the
right, campaign-screens.md's own cell model), then ACCEPT LOADOUT / CANCEL LOADOUT. A gun group's
build (which slots mount a gun, at what calibre) and a wing's hardpoint count come from the target
plane's own `CustomPlaneDef` (`CustomPlaneStore.Load(plane.Name)`) when it went through the
hangar, else from the airframe's plain stock fit (`StockLoadouts.ForModel` over
`PlanePickerRoster.AirframeNode(airframe)`) for the two profile-seeded starters, which never touch
`CustomPlaneStore` (B13's own `HangarCampaignContext.SellPrice` fallback makes the same call). A
starter's per-wing hardpoint split is derived from `Loadout.PylonFillOrder`'s first `Count`
entries (1-4 left, 5-8 right), the same split `CustomPlaneBuild.HardpointsFor` uses for a built
plane, so both plane kinds resolve through one `SlotBuild` shape. The page edits a **working copy**
(local `_ammo`/`_ordnance` arrays), the original's own `uiData` 2035/2034 model: nothing reaches
`Flow.Profile` until ACCEPT (`Flow.Store.Save`), and CANCEL or backing out drops the copy. A
re-entry onto the same (profile, slot, plane) after a cancel re-reads the still-unedited stored
fit rather than the discarded edits (`EnsureLoaded`'s own identity check, forced to reload whenever
`Discard` last ran). Which aircraft: `Flow.AmmoSlot` 0 reads/writes `Profile.Planes[SelectedPlane]`,
1 reads/writes `Profile.Planes[WingmanPlane]`, per the shared field C24 also reads.

**Ordnance encoding (CSVM-side, not the save's).** `saved-games.md` states the original's per-pylon
ordnance id is undecoded. `OwnedPlane.Ordnance[cell]` here instead holds a **table index into
`stock_loadouts.json`'s `pylon_ordnance` list**, which is already ordered row-for-row identically
to campaign-screens.md's threshold table (AP, HE, Flak, Sonic, Flash, Rear flash, Smoke, Choker,
Beeper, Seeker, Torpedo, None): `0` means unset, defaulting to the documented universal stock fit
(HE, table index 1, `wep_06`, per loadouts.md's Stock table note that "every pylon carries
`wep_06` in stock fit"); `1..12` is table index `0..11` plus one. `OwnedPlane.Ammo[slot]` keeps B11's
already-shipped convention unchanged: `0..3` the ammunition index (slug/dumdum/ap/magnesium),
`4` no gun. **Candidate backlog item:** decode the original's own per-pylon ordnance id (the
CSVM-side table index above is a deliberate stand-in, not a recovery of it), which would let a
future writer round-trip an original `SavedGames\` plane record's ordnance field losslessly.

**Art.** `extracted\rimage\OL_PLANEDIAGRAMSTOP.PNG` / `OL_PLANEDIAGRAMSFRONT.PNG` are the top/front
plane views `ol_p_planetopicon`/`ol_p_planefrticon` draw (frame = airframe index, the same
multi-frame idiom `FC_PlaneIcons.png` uses). Both ship as PNG; `TgaImage` (the hangar art seam's
only decoder) covers TGA alone. Rather than invent a PNG decoder outside this item's file
boundary, `Art`/`RowArt` return null (never invented). **Candidate backlog item:** a PNG decoder
for `HangarArt`'s art seam, which would also unblock any other `rimage\*.PNG` art no page draws
yet.

**Wiring contract for flight (not applied; `Loadout.Bind`/C24/D31 own it).** The fields C24/D31
bind into a flying loadout are exactly `OwnedPlane.Ammo`/`OwnedPlane.Ordnance` on
`Profile.Planes[SelectedPlane]` (pilot) and `Profile.Planes[WingmanPlane]` (wingman), read the same
way this page's `EnsureLoaded` reads them. Turning an ammo index into a `wep_*` id is
`StockLoadouts.GunWeaponId(caliber, ammoName)`, already shipped; turning an ordnance table index
into a `wep_*` id is a lookup into `StockLoadouts.Load().Options.PylonOrdnance[index].Id`, the same
list this page reads its labels from.

**Model recommendation.** medium.

**Verify.** `dotnet build`/`dotnet format` clean (0 warnings), comment caps clean. `dotnet test`
**2059/2059**, six new units in `CampaignAmmoPageTests.cs`: group/hardpoint derivation from a
hangar-built plane and from a starter's stock fit, a greyed no-gun group's Step being a no-op, the
ordnance filter honouring the mission-threshold table at ordinal 1 (only AP/HE/None reachable) and
ordinal 20 (torpedoes reachable), ACCEPT persisting into the store while CANCEL (and a re-entry
after it) leaves the stored fit untouched, and the wingman slot editing `Profile.Planes[WingmanPlane]`
rather than the pilot's plane. **Verified.** `RunTests.ps1` on the plan branch with all of Wave C
merged: build clean, units 2157/2157, engine suites 98/98 with errors clean, all 16 golden shots
hash-identical.

**⚠ Traps.** Which ammo types exist per caliber is data (`WeaponDefs`), not a list to author. A
rocket dropdown's row is a position in the campaign-progress-filtered table, never an ordnance id
straight off the row index (A6's trap, `campaign-screens.md`). Ammunition and rockets are free
(A5's disproof): this screen never touches `Flow.Profile.Funds` or any wallet path.

# Wave D — in-mission campaign machinery

## D31 ☑ Campaign mission director: the objectives runtime

**Goal.** A campaign session runs its mission's `objectives.zrd` graph per A1's decoded
semantics: objectives sleep, wake, chain, edit target lists, fire their sound groups and complete;
mission success/failure is derived from the graph; mission end records the result (B12) and
returns to the cabin.

**Evidence (confidence: direction-sound, pending A1).** `InstantActionDirector` is the sibling
pattern (wave sequencing, end conditions, wrap-up); the graph vocabulary is A1's deliverable.

**Approach.** A new director alongside `InstantActionDirector` (read the `src/Session/`
architecture entries first), instantiated by `GameSession` for campaign sessions; it owns
objective state machines and talks to `MissionTargets`/the anim runtime through existing seams.

**Model recommendation.** high — the milestone's core runtime.

**Verify.** In-engine test suite driving one decoded mission's graph headless (scripted kills via
the test harness), asserting wake order, chaining, and end state; plus `RunTests.ps1` full.

**⚠ Traps.** Absence of a log line is not evidence a behaviour never ran (file sink and
`--debug-anim` gating; see memory/verification docs) — assert through the test harness, not log
greps alone.

**Landed.** Three new files: `src/Session/ObjectiveScript.cs` (the parse), `ObjectiveGraph.cs` (the
engine-free state machine behind an `IObjectiveWorld` seam) and `CampaignDirector.cs` (the engine
side). `GameSession` resolves a `--campaign=<profile>:<seq>` launch to its chapter/mission in its
constructor, builds the director beside `InstantActionDirector`, attaches the graph after the
emplacement block and steps it from both drive paths. Every decoded quirk is implemented as
decoded: the four states, one completion per tick from a rotating scan, the wake list's truncation
at an already-awake target, `NAP` as the only re-run path, the AWAKE gate of
`TICK_DEPENDS_ON_OBJ`, and the shipped misspellings staying dead by exact-name lookup.

Reaching the engine today: `INACTIVEn` (node visibility), `ANIM_STATE` (a new
`AnimRuntime.AnimStateOf`), the node form of `TRAVELERS`, `WAKEUP_TURRETS`/`WAKEUP_ZEP_TURRETS`,
`WAKEUP_GENERATOR`, `WAKE_ANIM`, and both sound groups through `WorldSounds`' existing group
resolution. Named no-ops, each logged once: everything needing a spawned `aiv` roster (`DEDG`, the
group form of `TRAVELERS`, `WAKEUP_ENEMIES`, `SET_AI_*`, `WARP_VEHICLE`,
`COMPLETED_STOPPOINT`), the untraced `COMPLETED_ZEPCANNONS` reader, and `STOP_QUEUED_SOUNDS`.
(`START_TAXI` has a consumer, D35's `ScriptedPathVehicles`, empty for the same reason.) ⚠ An
unanswerable condition reports FALSE, never true: reading an empty world as "the group is wiped
out" would win every `DEDG` mission on its first tick. The `aiv` roster spawn is the one thing
between those no-ops and a mission that plays through, and it is D34's neighbourhood.

D33 consumes `ObjectiveGraph.Rows` (one row per unique `IDENTITY` priority, ascending, with its
`MSG_` key and awake/completed flags), `ObjectiveTargets`/`OtherTargets`/`HelpLabels`, and the
`Woke`/`Completed`/`TargetsChanged` events; the wake/complete events carry the sound-group names,
which is also where D37 sees `music_prebattle_sg` and kin. Mission end raises
`CampaignDirector.ReturnToCabin` and `MissionEnded` after recording through `CampaignProgression`
and capturing the persist log; C22 owns the screen it returns to.

**Verified.** `RunTests.ps1` on the plan branch (D31, D36 and C22 together): build clean, units
2092/2092, engine suites 98/98 with errors clean, all 16 golden shots hash-identical, so the
director's session hooks moved nothing in world build. Foreground: `dotnet build` clean (0 warnings,
StyleCop and comment caps clean), `RunTests.ps1 -SkipGoldens -SkipHitch` PASS — 2047 unit tests
(18 new in `CSVM.Tests/ObjectiveGraphTests.cs`) and all 96 in-engine suites, engine errors clean.
Two new suites: `campaign-objectives` drives C1/M02's own 50-objective graph headless to BOTH
endings it authors (the primary completing off its `INACTIVE` node, its `KILL`/`WAKE`/`NAP` chains,
the target-list edits, the display rows and mask bit 0; then the 300 s reminder fuse that naps the
`INSTANTLOSS` objective, losing the mission at 325 s), and `campaign-mission-end` runs the director
against a BUILT world where 15 real weapon kills through `DamageAt` drive `OBJECTIVE16`'s
`INACTIVE_COMPLETION_COUNT 10`, then asserts the graph's own end reaches the profile.
World-build changes: zero.

**Open.** The completed-objective mask's bit-to-objective mapping is not decoded; CSVM defines bit
n as display row n, which makes bit 0 the lowest priority and therefore the primary objective, the
one thing `docs/formats/saved-games.md` does state. The order the chaining lists run in
(wake, kill, nap, sleep) is not pinned by the decode either. Which flag a `MISSION_TIMER` expiry
sets is untraced; CSVM ends the mission lost.

## D32 ☑ Cutscene player (`BL-134`)

**Goal.** The 13 story-mission intro defs play as cutscenes: letterbox bars, camera driven by the
decoded `CALLBACK` codes, player input suspended, then a clean handoff to gameplay. C1/M04 is the
first worked case.

**Evidence (confidence: traced for the defs and for the consumer's semantics).** The defs are
decoded and were already playable by `AnimRuntime`; what was missing was the consumer. A7 delivers
the letterbox mechanism and the `CALLBACK` code table
(`docs/formats/anim-definitions/cutscenes.md`). `BL-134` is closed by this item and deleted from
`backlog.md`; its two standing caveats (the M0x defs must play, and C1/M04's pirate zeppelin above
the overcast is an accepted artifact) are restated on that page, as is its "verify by what
disappears" rule.

**Approach.** A cutscene controller in the session layer that arms before the mission director
starts: it installs itself as the `CALLBACK` host on the def it plays (the original registers a
host per animation; nothing is registered on a `StartAnims` def, so CSVM supplies the listener the
codes were written for), drives a cutscene camera, draws the bars, and hands off on the def's end.

Scope A7 fixes:
- **The bars are the gamez `letterbox` node**, not a UI overlay: two opaque black quads whose
  transform is copied from the cutscene camera every tick. The node is a parentless library root,
  so the builder must reach it outside the `world1` walk.
- **Code 20 suspends the world**, not just the camera: in the original the vehicle/AI update and
  the objectives update both stop while the cutscene animation holds the active slot. Code 0
  releases it.
- **Codes 913/914 park and reveal every AI vehicle**; 2/11 hide the chrome and take the player out
  of flight; 1/10 give control, chrome and in-flight systems back; 666/667 gate the camera-parameter
  profile so the def's own `CAMERA_STATE` is not overwritten.
- **Codes 14 and 123 are named gaps.** Do not invent behaviour for them.
- The mid-mission pickup/hookup cutscenes (`landings.zrd`, 27 letterbox call sites across 18
  readers) use the same machinery, so the controller should not be intro-only.

**Model recommendation.** high — camera plus sequencing with visible fidelity stakes.

**Verify.** Scripted `--screenshot` mid-cutscene on C1/M04 (letterbox visible, camera off the
plane) and post-handoff (controls live); the A8 intro capture is the fidelity reference.

**⚠ Traps.** The user's ruling: never suppress the M0x intro defs. Skipping a cutscene
(user input) must still run the def's world side effects, or the mission starts in a wrong state.
Note what A7 found the original does on skip: `FUN_004a0220` clears the active-cutscene slot and
force-stops the animation outright, and the handoff `FUN_00480480` does the same, so the original
does *not* replay the remaining beats. Whatever CSVM does about the leftover state is a design
decision here, not a fact to copy.

**User's verdict on the bars (CAP-43's one gap).** There is **no letterbox bars-in animation**: the
bars are simply there the moment the mission load ends, before anything else, and the intro then
fades from black behind them. That matches the data (`letterbox.zrd`'s first sequence sets the node
`ACTIVE` outright, with no tween) and is what shipped: the bars are up on the first rendered frame.

**Landed.** One new file, `src/Session/CutsceneController.cs`, plus the seams it needs.

- **The bars and the camera are the definition's own data.** `WorldSession` builds the two roots the
  `world1` walk never reaches (`camera1`, a bodiless marker, and the `letterbox` card, switched off
  by its own definition's base state) and installs the callback host on the runtime BEFORE the bind,
  because a bootstrapped intro raises its codes the instant `startanims` starts it. The bars then
  need no engine support at all: the shared `letterbox` definition switches them on and re-asserts
  `camera1`'s frame onto them every tick, which is the `AT_NODE` pose form this landed with
  (`OBJECT_TRANSLATE_STATE at_node` / `OBJECT_ROTATE_STATE basis.AtNodeMatrix`, previously parsed
  away, so the node was posed to its parent's origin). The controller mirrors `camera1` onto every
  rig camera, ticking last in the frame so the bars and the view never disagree by a frame.
- **Every code is hosted where the decode puts it**: 20 holds the world (both of `GameSession`'s
  drive paths early-out, and `CampaignDirector.HoldForCutscene` stops the objectives update with
  it, so no dormancy timer or reminder fuse burns down behind the movie), 2 takes the chrome off
  and the view off the aircraft, 11 takes the player out of flight, 913/914 park and reveal the AI,
  666/667 the camera-parameter gate, 1/10 the handoff and the in-flight systems. 14 and 123 stay
  named gaps with one log line each.
- **Handoff and skip are one code path.** The definition's end raises the gameplay state its own
  `RESET_STATE` asserts (1, 10, 914, 667), retracts the bars and parks `camera1`; a skip force-stops
  the definition first, exactly as `FUN_004a0220` does, and then runs the same restore. That is how
  dropping the remaining beats cannot leave the mission held, hidden or unflyable. Skip is any key
  but Escape, or any pad button where the session reads pads.
- **⚠ The authored codes do not identify a cutscene.** Instant Action's own `camera1-player_setup`
  raises the same nine, and every IA mission bootstraps it. Hosting by code gave every flight
  session a letterbox and a suspended world, and moved five goldens; the scope is the two decoded
  intro definition names, and what `player_setup` is for is recorded as undecoded.

**Verified (this item's own foreground run).** `dotnet build` clean (0 warnings, StyleCop and
comment caps clean). Foreground: in-engine suites 100/100 with errors clean, all 16 golden shots hash-identical (so the cutscene
roots reach no session without an intro), units 2092/2092. Two new suites: `campaign-cutscene`
drives C1/M04's shipped intro through the runtime's own `CALLBACK` dispatch and asserts the code
sequence, the world hold reaching a real objectives graph, the declined vehicle-death codes and the
handoff state; `cutscene-letterbox` asserts the shipped node's base state and the bars taking the
cutscene camera's whole frame, tick after tick. Two scripted shots, both
`RunProbe.ps1 --chapter=C1 --mission=M04 --fly --plane=player_bhawk --mute --screenshot=<abs>`: at
`--frames=60` the bars are up, the camera is off the plane along the pirate zeppelin and no HUD is
drawn; at `--frames=3260`, 1.1 s after the definition ends at t=53.2 s, the bars are gone, the full
HUD is back, the chase camera is behind the player and the plane is flying at 128 mph.
`generic_intro` was checked the same way on C1/M05. No golden re-pin was needed and none was taken. ⚠ The units stage needs a filter to run at
all on this branch: `CampaignFlightCheckPageTests` and `CampaignAmmoPageTests` crash the test host
in `CustomPlaneStore.UserPlanes` → Godot `ProjectSettings` outside the engine, which predates this
item and belongs to C24/C25.

**Verified.** `RunTests.ps1` on the plan branch with D32, D33 and D34 merged: build clean, units
2164/2164, engine suites 104/104 with errors clean (`campaign-cutscene` and `cutscene-letterbox`
included), all 16 golden shots hash-identical.

**Open.**
- **The mid-mission `landings.zrd` cutscenes are not reachable yet.** The controller is not
  intro-only by construction (it plays whatever definition it is armed for), but the trigger is:
  `FUN_0045df60` tests the player against an approach node's *condition object* and a speed band
  every frame, and that condition object is not decoded. The codes those cutscenes add (951's
  teleport-to-camera, 965–967's airframe swap, 3/12/13/86/701/702/800–803) are unhosted, and
  `BL-035` now carries that remainder.
- **A definition that never ends never hands off.** The end test is `AnimStateOf`, so a cutscene
  whose sequences do not complete would hold the session until the player skips. C1/M04's runs
  53.2 s and ends on its own.
- **The frame the bars letterbox is inferred, not decoded.** The card's own height is taken as the
  frame height (the only reading under which the geometry is a letterbox), which fixes the cutscene
  field of view at 56.7° vertical; on a frame wider than the card's 5:3 the width becomes the
  binding dimension instead, so the bars still meet every edge. `camera1`'s gamez record carries no
  field of view.
- **`camera1` now exists in a story mission's session, and the bullethole definitions pose against
  it too.** They are parked back at the origin on the handoff so a stale cutscene pose cannot place
  them; what the original does with that node between cutscenes is not decoded.

## D33 ☑ In-flight objectives display + objective sound cues

**Goal.** The current objectives are visible in flight, one row per unique `IDENTITY` priority,
text through the messages table with raw-key fallback, updating on the graph's own `Woke`/
`Completed` events; the original's exact in-flight layout is a capture-owed fidelity question
(below), not something this item invents. `WAKEUP_SOUND_GROUP`/`COMPLETED_SOUND_GROUP` audio
plays, verified in engine rather than assumed from D31's routing.

**Evidence (confidence: traced for the display; a real gap found and closed for the cues).**
`docs/formats/objectives.md`'s "IDENTITY and the objectives display" traces the original's own
mechanism (`FUN_004acc20`/`FUN_004ad240`): every `IDENTITY` is collected into one row per unique
priority and shown unconditionally, sorted; completion marks the matching row. Nothing in the
decode gates a row on being "awake": `ObjectiveGraph.Rows`' own `Awake` flag is D31's addition
for other purposes and is a trap here: an objective authored with no `BEGIN_DORMANT` starts awake
without ever running a wake action, so its row's `Awake` flag never turns on even though it is
live from the mission's first tick (C1/M02's own primary, OBJECTIVE3). A readout that hides
`!Awake` rows would hide exactly the objective a player needs to see first; `ObjectivesHud` shows
every row instead, matching the traced original.

D31's claim that the cues already play does **not** hold as shipped, checked here against a real
built world rather than the headless `ScriptedWorld` `campaign-objectives` uses. `CampaignDirector.
World.PlaySoundGroup` does call `WorldSounds.PlayOneShot` correctly, but a campaign mission's
`WAKEUP_SOUND_GROUP`/`COMPLETED_SOUND_GROUP` names are `objectives.zrd`'s own vocabulary, never
referenced by the mission's `AnimProgram`, so nothing prewarmed them, and `WorldSounds.Loader`
closes over the build's `SoundArchive` the moment the `using` scope that build ran in returns
(`ArchiveIntent.Session`/`Suite` both scope the archive to the build). Any such cue whose name was
never independently prewarmed by something else therefore decoded to nothing, silently, in a real
running session exactly as in the suite. Fixed by `ObjectiveScript.SoundGroupNames()` (every
class/won/lost sound plus every objective's wake/complete group, de-duplicated) handed to a new
`WorldSession.Options.ExtraPrewarmNames`, prewarmed alongside the anim program's own names before
the archive closes (the same shape `Options.VoiceClipNames` already established for combat
voice). `WorldSounds.OneShotsStarted` (new) counts every one-shot that actually started an
`AudioStreamPlayer3D`, so a suite counts a cue firing rather than grepping a `Debug`-gated log
line, matching `Anim.SoundChannel.OneShotSoundsPlayed`'s own precedent.

A second, independent gap surfaced and is left as a gap, not built: several of a mission's own
`WAKEUP_SOUND_GROUP`/`COMPLETED_SOUND_GROUP` names resolve to **VO dialogue chains**
(`docs/formats/sounds.md`, `[name, [dialogueRoot, [line], [line], …]]`, "a VO chain, NOT a
weighted group"), and nothing in this engine plays a chain: `WorldSounds.Prewarm` decodes a
chain's lines but `PlayOneShot`/`Spawn` only ever calls `SoundGroup.Pick`, which returns null for
a chain (zero weighted members) and plays nothing. `docs/formats/sounds.md` already names this as
kept "so the comms/mission layer can consume them", a consumer that does not exist. Building a
chain player is a new capability (sequencing, timing between lines, whatever "the comms/mission
layer" means operationally), not "verify the existing cues fire," so it stays a named gap for a
follow-up backlog item rather than something improvised here. C1/M02's own data has both shapes:
OBJECTIVE8's `WAKEUP_SOUND_GROUP` and OBJECTIVE16's `COMPLETED_SOUND_GROUP` are plain/weighted and
play for real; OBJECTIVE1's `WAKEUP_SOUND_GROUP snd_NW2Start` and OBJECTIVE10's
`COMPLETED_SOUND_GROUP snd_NW2Prim2Suc` are chains and play nothing, in the original's own data
shape, not a CSVM regression.

**Approach.** `src/UI/ObjectivesHud.cs` (new): a self-mounting `Node` in `PerfHud`'s own shape (it
owns its `CanvasLayer` on `HudLayers.Hud`), built from a `CampaignDirector` and the shared
`Messages` table, polling for the director's `Graph` in `_Process` (the wiring contract below adds
it to the tree before `CampaignDirector.Attach` runs) and redrawing on `Woke`/`Completed`.
`BuildLines()` is exposed separately from drawing so an in-engine suite can assert the readout's
own content, not just the graph's. Sound: no new routing, D31's `PlaySoundGroup` call is reused
exactly as landed; this item's only sound-side change is the prewarm fix above, which is a build
plumbing gap, not a duplicate of the routing.

**Landed.** `GameSession.BuildWorldStage`'s `WorldSession.Options` carries
`ExtraPrewarmNames = _campaign?.Script.SoundGroupNames()` beside `VoiceClipNames`; `_campaign` is
already built earlier in `StartSession`, ahead of the world build, so no reordering was needed.
`BuildFlightRigs` mounts `UI.ObjectivesHud.Build(campaign, Messages.Load(state.MessagesPath))` on
`_worldRoot` right after the `TargetingOverlay` `AddChild` beside it, guarded on `_campaign is {
} campaign`, the same shape that overlay uses; the messages table is reloaded at the call site
rather than cached on a field, matching how the weapons/stunt-zone loads elsewhere in
`GameSession` already read `state.MessagesPath`. A real `--campaign=` flight (C1/M02) confirms
both halves: the readout draws every display row over the flight HUD, and the game log's `anim:
prewarmed N sound stream(s) before the archive closed` line covers the objective sound groups
alongside the combat-voice roster.

**⚠ Capture owed.** No reference screenshot covers the original's in-flight objectives
presentation: all five `Campaign *.png` shots are out-of-mission screens. `ObjectivesHud`'s
styling (position, font size, checkmark glyph, whether completed rows stay listed or drop) is
therefore a plain HUD-idiom placeholder, every metric marked TUNE in the source. The capture the
orchestrator should file: one C1 story mission flown far enough to wake and complete at least one
objective, showing the original's own in-flight objectives UI (or its absence, the original may
show nothing in flight and rely on the briefing note alone, which the capture would also settle).
Filed as `CAP-45` in `playtest.md`, naming D33 as the item it unblocks.

**Model recommendation.** medium.

**Verify.** `dotnet build`/`dotnet format` clean (0 warnings), comment caps clean.
`dotnet test CSVM.Tests/CSVM.Tests.csproj`: 2164/2164 (the pre-existing `SuiteCatalogTests` count/
last-name assertions updated for the new suite; they were already stale by two suites before this
item touched them). New suite `campaign-objectives-hud`, registered at the end of
`SuiteCatalog.Names`: against a BUILT C1/M02 world, `ObjectivesHud.BuildLines()` carries one line
per `ObjectiveGraph.Rows` row before anything happens; every `WAKEUP_SOUND_GROUP`-authoring
objective is woken in turn until one starts a real `AudioStreamPlayer3D`
(`WorldSounds.OneShotsStarted` counted, not logs); every `IDENTITY`+`INACTIVEn` objective is
driven in turn (resolve, damage/deactivate its nodes, wake, step) until one completes, proving the
readout (not only the graph) marks the completed row, and separately until one whose
`COMPLETED_SOUND_GROUP` also starts a real one-shot (the two proofs are not required to land on
the same objective, since a completing objective's own group can be a VO chain). No `--screenshot`
was taken; see the wiring contract and capture above. Foreground `RunTests.ps1` on this worktree
(D33 alone, ahead of the plan branch merge): build clean, units 2164/2164, engine suites 101/101
with errors clean, all 16 golden shots hash-identical, so the prewarm plumbing and the new suite
moved nothing in world build. **Verified.** `RunTests.ps1` on the plan branch with D32, D33 and
D34 merged: build clean, units 2164/2164, engine suites 104/104 with errors clean, all 16 golden
shots hash-identical.

The `GameSession.cs` wiring landed on the `worktree-m5-d33` branch: `dotnet build CSVM/CSVM.sln`
0 warnings/0 errors; `dotnet test CSVM.Tests/CSVM.Tests.csproj` 2324/2324; `RunTests.ps1
-SkipGoldens -SkipHitch` on this worktree: build clean, units 2324/2324, engine suites 104/105
with errors clean and the sole failure `wingman-station` (a pre-existing D34-side failure on this
worktree's base commit, unrelated to this item's files). A real `--campaign=<profile>:6`
(C1/M02) `--screenshot` flight shows the objectives readout (four rows) drawn over the flight HUD,
and its log carries `anim: prewarmed 375 sound stream(s) before the archive closed` (263 of them
the combat-voice roster, the rest the anim program's own sound names plus the objective
wake/complete groups this item's prewarm fix adds). **Verified.** `RunTests.ps1` on the plan
branch with the mount, D34's spawner and D35's two sub-changes merged over main's flight-model
parity: build clean, units 2334/2334, engine suites 108/108 with errors clean, all 16 golden shots
hash-identical.

**⚠ Traps.** Do not filter the readout on `ObjectiveGraph.Rows[i].Awake`; see Evidence. Do not
add a second `PlaySoundGroup` path for "D33's own" cues; the routing is D31's and stays
untouched. `ExtraPrewarmNames`/`SoundGroupNames()` prewarm chain members too (`Prewarm` already
expands a group to its members), so the chain-vs-weighted distinction only matters at *playback*
(`SoundGroup.Pick`), not at prewarm time.

## D34 ☑ Campaign wingmen: named rosters + netless station-keeping (`BL-362`, `BL-364`)

**Goal.** Campaign missions spawn wingmen from their named rosters (`aiv.zrd`), flying the decoded
netless `mode wingman` station-keeping (join, hold station, speed-ramped trail), commanded by the
existing AI machine; campaign patrol nets get their spawner plumbing (`BL-364`'s open half).

**Evidence (confidence: traced constants, re-read out of `crimson.exe` for this item).**
`FUN_0041e760` was decompiled again before any code was written, and it confirms every constant
`BL-362` carries plus three corrections now in `docs/org/aiPilot.md`: state 2's break-off measures
its `(3 × altitude error)² + range²` to the LEADER with a HORIZONTAL range (`FUN_00538920`, not the
3-D `FUN_00538880` every other test uses); state 0's 1800 m test is the wingman's distance to its
leader, not to its target; and the 106.68–259.08 m station on a selected target is placed along the
NEGATION of the target's backward axis, so it sits that far AHEAD of the target rather than behind
it. Nothing in `src/` read `aiv` as a spawn roster before this item; the spawner below is the first
reader, and it is what closes `BL-364`'s campaign half.

**Approach (landed).** The law is a module of its own, `src/Flight/AiEscort.cs`: the engine's own
five-state machine, both body-frame stations, the speed-ramped target station, the break-off test
and the 80 m separation push, pure over a leader/target snapshot. `AiPilot.Escort` holds it and,
whenever the leader is in play, dispatches to it in place of pursue, lay off, patrol, evade and a
running maneuver, keeping only stunned and avoid crash ahead of it, which is the original's own
`mode wingman` fork order (`FUN_0041c270` step 4). The station is flown through the existing
`AiControlLaw` on `AiLawParams.Wingman`, the table the original passes there, and the climb-out an
escorting pilot flies now uses that table too. **No tenth `AiMode` was invented**: the escort state
is the engine's separate `+0xd8` byte and is named as ours (`EscortState`), never printed in the
mode readout's vocabulary.

**Landed: the spawner.** A campaign session spawns the mission's `aiv` roster in two halves.
`src/Session/CampaignRoster.cs` is the engine-free plan: `AiSkills.LoadRoster` gives the blocks,
`src/Mech3/VehicleDefs.cs` resolves each block name to its def (`blakepeace_2_1` → `blakepeace_2`),
the def's `mode` through `kind_of`, and the player airframe its model is built from (`wingman` →
`devastator` → `pdevastator` → `player_pfighter`); the new `AiSkills` readers cover `netids` (0, a
scalar or the exe's list, a multi-entry list drawn `rand() % count`), the spawn pose (1, 2), team,
group, `title`, `deactivated` (21), `pref_engage_alt` (31), the signature mask (32), `taxiPath` (40)
and the accent (65), and `src/Mech3/AiVolumes.cs` reads the twelve volume slots (8–19) and the nine
volume floats a net record carries at elements 2–10. **The fork is applied per plan and nowhere
else:** `Escorts` is `mode wingman` AND no authored net; `Net` is any authored net the chapter
carries (`AiNets.ById`, never Instant Action's chapter-first rule); a plan never has both. The
volumes are the net's overlaid by the block's own, field by field on the engine's non-zero test.
`CampaignDirector.BuildRoster` is the thin Godot half, called by `GameSession` right after
`InstantActionDirector.BuildActors`, where the human rigs exist: one `SpawnAiAircraft` per plan (the
block's own skill slots outranking the def's inside the spawner, its representative rating arming
the gunner and machine, the block's team and `nitro`, `deactivated` built inert), the merged volumes
through `CampaignRosterPlan.ApplyVolumes` with the `min_ai_active_dist` floor, the signature
maneuvers, the rating biases, `primary_target` as the gunner's assignment on a jet, the accent
into the voice runtime, and a `taxiPath` block placed held on its path (`ScriptedPathVehicles.Place`
re-pinning the aircraft through `PlaceHeld` each tick, `Activate` at the handoff speed). The leader
pass runs SECOND, once every rig exists: `primary_target` resolves to the player rig on the literal
`player` or to a block by name, and `pilot.Escort = new AiEscort { Leader = … }` is set only then;
a leader that is not spawned leaves the wingman holding its course, and a leader that dies later is
`AiPilot`'s own fallback (a 2/4 follower is not re-pointed at its dead leader's leader, the record
`BL-362` trap (c) kept). The spawn pose is the block's own coordinates and yaw; a world node of the
block's name, where the chapter's anim data carries one, is preferred over them, so a mission that
animates its parked aircraft into place agrees with the roster. The profile's wingman airframe
(`WingmanNode`/`WingmanFit`, bound by the director from the profile) replaces the `wingman_1`
block's own def, its stats down the `w<plane>` twin so the mode stays `wingman`. Over the spawned
roster the objectives world now answers `DEDG` (`GroupLiveCount`) and `WAKEUP_ENEMIES` (an inert
named aircraft re-activated at its spawn pose). Not spawned: the `player` block (the human rig) and
a surface vehicle (`mode ship`, no player airframe), both reported in the log rather than guessed.

**Model recommendation.** high — touches the AI control law.

**Verify.** New suite `wingman-station` (`src/Testing/WingmanSuites.cs`, registered last in
`SuiteCatalog`): the station geometry with no engine state (both offsets, an offset that rolls with
a banked leader, the 106.68/259.08 m endpoints, the 80 m push and its stand-down, the 700 m and
20.576 m/s join gates), then two flown legs against a scripted leader (no pilot, stick centred, a
cruise lever) with a live wingman spawned 1200 m abeam. A player leader's wingman crosses the join
gate at 697 m and never falls back out; an AI leader's reads the formation station through the
forced engaging state; both then stay with their leader (mean 214 m / 165 m, worst 471 m / 394 m
over the last minute of a two-minute run), the player-led one riding right and aft of its leader in
the leader's own frame, and the A/B the two decoded stations predict holds: the player-led wingman
rides 82 m farther aft than the AI-led one.

The tolerance is read off the decode rather than off the run: the commanded point is never more
than 99 m from the leader (the 18.97 m station plus the 80 m push), and because that push always
fires at the player station the commanded point alternates between the two, so the law weaves
around the leader instead of settling. The suite's leash (600 m worst, 250 m mean) allows that
weave and still fails the behaviour `BL-362` reports, a wingman that simply leaves.

**Verified (the spawner).** New suite `campaign-roster` (`src/Testing/CampaignRosterSuites.cs`,
registered last): C1/M04's shipped roster (21 blocks) in its built world, the player a scripted
leader at the `player` block's pose. 20 of 21 blocks get a rig (the player's is the human), 3
escorts and 16 netted with no block carrying both, `wingman_1`'s escort leader is the player rig
and it has no net, `wingman_3`'s leader resolves to `devastator_3`'s rig, `blakepeace_2_1` walks
net 15 with no escort, the 9 `deactivated` blocks are inert and out of play, the 4 `taxiPath`
blocks sit frozen on `pp1`–`pp4`, and `devastator_2` reads its net's authored 700 m return radius
under a 2000 m activation floor. Over the two-minute flown run `wingman_1` joins the formation
state and holds the player at mean 213 m, worst 396 m over the last minute, inside the
`wingman-station` leash (600 m worst, 250 m mean). The flown leg lifts its pair 800 m above the
roster's own poses first: the authored 110 m start is the spawner's business, and a scripted
leader with no pilot flies into the terrain from there. The engine-free half is pinned by
`CSVM.Tests/CampaignRosterPlanTests.cs` (the block-name-to-def resolution, the airframe lookup,
the fork on a scalar and a list `netids`, the profile-airframe override, the volume overlay and
floor, the spawn-slot readers, the net record's volume order).
The real session path: `--menu=campaign-fly --screenshot` logs one `campaign: roster '<name>'`
line per spawned block of the seeded profile's mission and the `campaign: roster spawned N of M
block(s)` summary. `dotnet build` clean (0 warnings), `dotnet test` 2334/2334, `RunTests.ps1` PASS
with the 106 in-engine suites and all 16 golden shots hash-identical (a campaign roster spawns in a
campaign session only, so Instant Action and freecam shots do not move), `CheckCommentCaps.ps1`
clean. `SuiteCatalogTests` took the new count and last name. **Verified.** `RunTests.ps1` on the
plan branch with the spawner, D33's mount and D35's two sub-changes merged over main's
flight-model parity: build clean, units 2334/2334, engine suites 108/108 with errors clean, all 16
golden shots hash-identical.

**The one `wingman-station` assertion that changed, and why.** Its leashes, its join gates, its
geometry checks and the A/B between the two stations are untouched and still green (player leader:
joins at 699 m, mean 153 m, worst 340 m; AI leader: mean 145 m, worst 325 m). What was replaced is
the check that the FLOWN mean sat right and astern of the leader in the leader's frame. That number
is a property of the limit cycle the 80 m push drives, not of the decode, and the flight model's
own parity retune moved it (the flown mean now reads out 91 m, along −22 m). The decode pins the
COMMANDED point, so the suite now pins that instead, on two checks: the law never leaves the
formation state during the hold, and on every step outside the 80 m push the commanded point equals
`AiEscort.FormationStation` for that leader kind, measured at 0.00 m of error over 2468 (player) and
2725 (AI) sampled steps. That is the same equality the geometry block asserts statically, so a
regression in the station offsets or in the leader frame still fails the suite; a step the escort
did not fly is excluded, since avoid crash runs AHEAD of the escort in the decoded fork order and
commands the climb-out rather than a station.

**Closure.** `BL-362` and `BL-364` are closed by this spawner: the campaign's netless `wingman_N`
blocks fly the escort on their `primary_target`, and every netted block walks its own authored net
with the volume order the original uses.

**⚠ Traps.** File contention with `PLAN-flight-model-parity.md` on `AiPilot`/`AiControlLaw`;
sequence, never parallel worktrees. The station constants are decoded facts, not TUNE.

**⚠ Open question this item did not chase.** Pursue (`FUN_0041d9f0`) applies the SAME
106.68–259.08 m ramp along the victim's backward axis with a POSITIVE sign, so the original's
pursuit station sits that far BEHIND its victim, while `AiPilot.FlyPursuit` puts our aim point that
far AHEAD of it (`quarry.NoseDirection`). One of the two is a sign error in the landed D31 port and
the other is the escort law's own deliberate mirror. Both decompiles are quoted in
`docs/org/aiPilot.md`; changing pursuit would move every dogfight and belongs to its own item.

## D35 ☑ Mid-mission world fidelity: `WAKE_ANIM` doors, scripted-path vehicles, partition switching, fog events

**Goal.** Four decoded-but-unbuilt world behaviours land: hangar doors open via `WAKE_ANIM` before
generator spawns (`BL-350`), scripted-path vehicles follow their authored waypoint paths
(`BL-361`), `WorldPartitionSetActive` switches C3 story-mission areas (`BL-037`), and `FogState`
events change fog mid-mission (`BL-038`, shipped once, on C1/M04's intro).

**Evidence (confidence: mixed; each BL carries its own decode).** `BL-361` cites the decoded
waypoint-follower (`docs/org/flightModel.md`); `BL-037` counts all 25 uses in
`C3/*.gw`; `BL-038` is a decoded anim event. `BL-350` and `BL-038` are re-verified open before
being built: `git log --grep` finds only their filing and this plan's scoping commits; nothing in
`CSVM/src` reads a `FogState` event (`AnimRuntime.Dispatch` falls to its counted default and
`docs/formats/anim-definitions.md` says so); the `WAKE_ANIM` `BL-350` names is C1/M04's
`OBJECTIVE1` (`BEGIN_DORMANT 2.0`, `WAKE_ANIM hangar3_doors`), which D31's director already
dispatches through `AnimRuntime.Play`, but the hangar it opens is `hangar3`, 2.8 km from the
generator `eairg31` the crash report came from, so the door that matters to `BL-350` is the
generator's own, and `AiGeneratorRuntime` ran it log-only because the loader's node-name default
was documented under the wrong pattern. `BL-361` and `BL-037` were
re-verified open before they were built: nothing in `CSVM/src` read `taxiPath`, `ppN_aipath` or a
partition rectangle, `MissionSetup` counted the verb unapplied, and `CampaignDirector.StartTaxi` was
a named no-op.

**Approach.** Four independent sub-changes behind one item; each lands separately with its BL
closed. Order: BL-350 first (it blocks visible C1 behaviour), then BL-361, BL-037, BL-038.

**Landed: `BL-037`, the area-selected toggle.** `GameZ` keeps the World node's per-cell membership
(`PartitionCellNodes`) beside the flat `PartitionNodes` the placement walks use, plus the grid origin
and cell size read off the first cell's own bounds. `WorldPartitionGrid` answers "which nodes does
this XZ rectangle cover", `MissionSetup.BindPartitions(gamez)` resolves the script's rectangles while
the gamez is in hand, and `Apply` switches them through one new `AnimRuntime` hook
(`SetSubtreeActiveByIndex`, over a new `NameResolver.ByGamezIndex`). The verb names no node, so an
index is the only way to reach its selection. Both decoded traps are honoured and provable: corner
order is normalised, and the rectangle is half-open in cell space. A third trap was found while
building it and is now in `docs/formats/interp.md` and `world-structure.md`: **the grid's two axes
index in opposite directions**, the x origin being the low edge and the z origin the HIGH edge with a
negative authored cell size, so a grid indexed off `area` alone mirrors the selection onto the wrong
half of the map. C3's three rectangles resolve to 71, 36 and 131 of the chapter's 439 partition
roots, and they are terrain and scenery, so a story mission really swaps its map.

**Landed: `BL-361`, the second movement law.** `Flight/PathFollower.cs` is the law with every decoded
constant (40 mph taxi, the 3/π heading-error normaliser, the final leg's `4.0302024` acceleration,
the 300 m overshoot and the `83.3` climb gain over 0.4 of 110 mph), holding the path flag and the
freeze flag separately so "placed and waiting" is expressible. `Mech3/ScriptedPath.cs` is the path
source, found while building this: the roster's `taxiPath` slot names `ppN` and the chapter gamez
carries it as the transform-only subtree `ppN_aipath` whose `ppN_aipM` children are the waypoints.
Ten vehicles carry one, in C1/M04, C2/M02 and C5/M01. `Session/ScriptedPathVehicles.cs` owns the
lifecycle and `CampaignDirector`'s `START_TAXI` releases it. Write-up:
`docs/org/flightModel.md`, "The scripted-path follower", and `docs/formats/ai-rosters.md`'s slot 40.

**Named gaps, not guesses.** (1) Nothing spawns the `aiv` roster, so no session places a vehicle on a
path and the registry is empty at run time; that is D34's neighbourhood and the last piece between
this law and a visible takeoff. (2) The ride height for movement classes 0 and 4 (the aircraft
classes, which is every shipped path vehicle) reads a vehicle-type field at `type+0x218` that is not
identified in `vehicle.json`; the port leaves it at zero rather than reusing the 0.2 m the other
classes take. (3) `4.0302024` and `83.3` are used exactly as read but remain unidentified as
authored quantities. (4) The altitude between waypoints and the leg-advance test are this port's own
readings where the decode is silent; both are stated as such on the docs page.

**Model recommendation.** medium per sub-change; BL-361's second movement law is high.

**Verify.** Per sub-change: the citing mission's scripted capture (`--screenshot`/`--freecam`) at
the affected site plus the 8-chapter freecam regression with unchanged counts elsewhere.

**Verified (`BL-037`, `BL-361`).** `dotnet build` clean (0 warnings, StyleCop and comment caps).
Two new suites at the end of `SuiteCatalog`, both PASS with zero engine errors: `partition-areas`
(C3/M01's built world, the three rectangles' cell spans and node counts, the single-cell rectangle
selecting nothing, corner order, 126 of 131 area-3 nodes really switched off with 70 of 71 area-1
nodes left standing) and `scripted-path` (C1/M04's real `pp1`: frozen for 2 s of steps without
moving, released, rolling at or under the taxi speed, then a final leg peaking at 64.6 m/s and
climbing 32.6 m before the handoff at 63.96 m/s, which is what `v² = v₀² + 2·4.0302024·464 m` over
that leg predicts). Captures in `.scratch/`: `bl037-c3-M02-area3.png` versus
`bl037-c3-M01-area3.png` (the same camera over C3's third area, the landmass, docks and zeppelin
hangar present in M02 and open sea in M01) and `bl037-c3-M01-area1.png` versus
`bl037-c3-M02-area1.png` (the opposite transition on the first area, so neither direction is a net
that could hide the other); `bl361-c1-m04-pp1-strip.png` shows `pp1`'s waypoints run down the middle
runway of C1/M04's airfield, which corroborates the takeoff-run reading. `dotnet test` in this
worktree crashes the test host with an `AccessViolationException` at a different, randomly varying
test each run and zero assertion failures; **the same crash reproduces on this worktree at HEAD with
none of these changes applied**, and the main checkout passes all 2003. Pre-existing, and not this
item's (on the plan branch the same suite passes in full). **Verified.** `RunTests.ps1` on the plan
branch with these two sub-changes and the shell integration merged: build clean, units 2164/2164
once the suite-count test learned the two new suites, engine suites 100/100 with errors clean
(`partition-areas` and `scripted-path` included), all 16 golden shots across the 8 chapters
hash-identical, so the partition grid and the path follower moved nothing in world build.

**Landed: `BL-350`, the generator's own door.** The mission script's half was already there:
`OBJECTIVE1`'s `WAKE_ANIM hangar3_doors` reaches `AnimRuntime.Play` through the director and slides
`hangar3`'s four panels their authored 50 m. The generator's half was not: C1/M04's `eairg31` and
`eairg32` author no `open_anim`, and the original's loader (`FUN_00452850`) then formats
`sprintf("%.5s_open%.2s", node, node + len - 2)`, so `eairg31` asks for `eairg_open31`, a C1
`cam_anim` definition rooted on the host that slides its `ldoor`/`rdoor` 8 m over 4 s, the decoded
door lead. The close default is formatted into a second buffer and never looked up (the loader
re-reads the first), so an unauthored close resolves the open definition. `EnemyGenerators` now
applies both defaults (`DefaultDoorAnim`), `AiGeneratorRuntime` skips its self-stop when the two
names coincide, and the door cycle that was running log-only drives the real panels. The pattern in
`docs/formats/mission-entities/enemy-generators.md` was `<node>_open_<nn>`, which matched nothing
in the install; three unauthored hosts ship the def the real pattern names (C1's two, C2/M01's
`eshipg31`), and C3/M03's `barracuda` gets nothing. Named gap: a `--campaign=` session still loads
its generators only with `--generators`, the capacity source for campaign missions being the open
question that page records.

**Landed: `BL-038`, the inline fog.** The handler is dispatch slot 28 (`FUN_004e8540`): per flag
bit it calls the four fog setters the zone apply (`FUN_00472ea0`) itself calls on the same fog
record, raising a dirty bit per field, and it fires from a reset walk exactly as from a sequence,
which is the only way the shipped `drop_fog` ever fires. `AnimRuntime` raises it through a
`FogStateSink` (`FogStateChange`, one nullable field per bit), `WorldSession.Options` installs the
sink before the bootstrap, `GameSession` holds an event raised inside the world build until the
weather rig has written its zone, and `WeatherRig.ApplyFogState` writes only the carried fields
onto the `csky_fog_*` globals; the next fog-zone edge writes the zone back, the original's
last-writer order. `WeatherRig.FogGlobals` mirrors the last writes, since the renderer refuses
`GlobalShaderParameterGet` outside the editor. Decode: `docs/org/weather.md` "The `FOG_STATE`
animation event" and `docs/formats/anim-definitions.md`. There is no non-cutscene use in the data
(one event in 16,114 compiled definitions), so it is verified on the intro definition itself.

**Verified (`BL-350`, `BL-038`).** `dotnet build` clean (0 warnings). Two new suites at the end of
`SuiteCatalog`, both PASS with zero engine errors: `hangar-door-wake` (C1/M04's built world with the
director and the mission's two generators attached: `hangar3_doors` wakes at 2.03 s, its panels are
all 50 m out at 12.00 s; `eairg31`'s door starts moving at 16.03 s and the first spawn comes at
20.00 s, the decoded 4 s lead, at the generator's own hangar with both panels 8.0 m out) and
`fog-state` (a real `WeatherRig` over C1/M04 writes zone2's 1000–4000 m / 4000–5000 m at build;
playing the intro raises one `FogState` through the dispatch and the globals read 1000–1500 m,
10000–11000 m and 0.69 gray in linear; an event handed to a rig before its build lands after the
zone; a range-only event leaves the altitude alone). Captures in `.scratch/`:
`bl350-c1-hangar3-closed.png` versus `bl350-c1-hangar3-open.png` (the script's hangar, its end
panels slid out) and `bl350-c1-eairg31-closed.png` versus `bl350-c1-eairg31-open.png` (the
generator's hangar, its door pair slid out, end-on); `bl038-c1-m04-intro-drop-fog.png` (a
`--fly --mission=M04` session 4 s into its intro cutscene, the log carrying the rig's
`FOG_STATE 'drop_fog'` line, the zeppelin at 1600 m fogged inside the event's 1000–1500 m range
where zone2's 1000–4000 m would leave it clear) beside `bl038-c1-m04-zone-fog-before.png` (the
zone fog from an anim-lab camera at 1500 m over the same mission). ⚠ The animation lab's
`--play-anim` runs the definition on the lab's own runtime, which has no fog sink, so a lab A/B of
the intro shows zone fog on both sides; the flown session is where the event lands.

**Verified.** `RunTests.ps1` on the plan branch with these two sub-changes, D33's mount and D34's
spawner merged over main's flight-model parity: build clean, units 2334/2334, engine suites 108/108
with errors clean (`hangar-door-wake` and `fog-state` included), all 16 golden shots
hash-identical. On the merge the two suites' names were missing from `SuiteCatalog.Names` (they
were registered in the harness alone, so the catalog test failed on collection equality); the list
carries both.

**⚠ Traps.** BL-038 fires inside D32's cutscene on C1/M04, and nowhere else in the data. ⚠ C3/M01
and C3/M04 BOTH switch area 3 off, so the A/B for that area is M01 against M02; picking M04 reads as
"the verb does nothing". ⚠ `hangar3` is not a generator's hangar: a door test that watches it and
the spawn together is watching two unrelated buildings.

## D36 ☑ AI targeting candidates beyond aircraft (`BL-363`)

**Goal.** AI target selection considers zeppelins, turrets and structures where the mission's
data says so, making escort/defend campaign objectives functional.

**Evidence (confidence: decoded, docs/org/aiPilot.md "Target acquisition").** The candidate pool
is four typed lists (`TargetVehicle`/`TargetTurret`/`TargetStruct`/`TargetProjectile`), swept for
one global minimum; a turret carries a flat `+37.5` rank-unit handicap on top of its
`rating_biases` match; the struct list is always swept, and only its zeppelin-gasbag members are
gated on the shooter carrying loaded `DAMAGES_ZEPPELIN` ordnance.

**Landed.** `FlightController.SelectRankedTarget` now sweeps aircraft, turrets and structures
(mirroring the human aim assist's own three lists) for one global minimum;
`AiTargetRanking.ObjectiveBiasFor` carries the turret's flat `+37.5`. The winner routes into a new
`AiGunner.GroundTarget` field rather than the aircraft-only `Target` field, so `AiPilot`'s flight
law (D34's file, untouched) never sees a turret or structure as a pursuit quarry — it keeps flying
its assigned course while the gunner independently aims and fires. Verified in-engine
(`targeting-candidates` suite): a same-team structure is refused, a real team's AI routes a
winning structure into `GroundTarget`, and the gunner fires real rounds at it with no aircraft in
the scan at all.

**Left unmodelled, named rather than guessed** (docs/org/aiPilot.md "What CSVM ports of this"):
the `wingman` `+0.4` de-prioritisation and the zeppelin-gasbag `+0x65` ordnance gate both need a
`mode` field and a gasbag identity that do not reach `FlightController` without new session-level
wiring (`GameSession.cs`, out of this item's scope); the ahead/behind deadband, altitude-sign and
facing `±0.2` terms stay the pre-existing cone/sign reading rather than the decoded geometry; and
`TargetProjectile` is not part of the AI sweep.

**Model recommendation.** medium.

**Verify.** In-engine test: an AI ordered against a zeppelin target engages it; existing combat
suites green.

**Verified.** `RunTests.ps1` on the plan branch (D31, D36 and C22 together): build clean, units
2092/2092, engine suites 98/98 with errors clean (`targeting-candidates` included), all 16 golden
shots hash-identical.

**⚠ Traps.** (a) The ranking is MINIMISED, so a large nearby structure can outrank a distant
fighter — settled by the decoded turret/bias arithmetic already shipped (`BiasScale`/`AlwaysTarget`
match the decode), not invented here. (b) A wingman's stock loadout has no `DAMAGES_ZEPPELIN`
ordnance, so it can acquire a gasbag structure but never spend a round on it profitably; the
ordnance gate that would prevent that admission is the named unmodelled gap above. (c) Widening
the pool must not turn every AI into a zeppelin attacker on its own — that stays governed by
`primary_target`/`rating_biases`, unchanged by this item.

## D37 ☑ Music: playback subsystem + the state-driven track selection

**Goal.** A music player exists (streaming 2D playback, crossfade/stop, `audio.volume` respected)
and plays the right track for the game state: `splash` on the main menu, the cabin/briefing music,
`prebattle` in-mission until combat, `battle` during combat, the `primaryobj`/`secondaryobj`/
`tertiaryobj` stingers on objective completion, and `battlesuccess`/`missionsuccess` at mission
end; Instant Action gets `instantaction`.

**Evidence (confidence: traced to an exact mechanism, with the data that proves it).** The decode
landed as [`docs/org/music.md`](org/music.md), with a "The music channel" section on
[`docs/formats/sounds.md`](formats/sounds.md) as its data-side landing. Every selection rule below
has an address on that page.

- **The music channel is a routing flag, not a subsystem the data asks for.** `FUN_00593590` sends
  any `SETS` definition carrying `MUSIC` (or whose WAV name starts `mu`), and any `SOUND_GROUPS`
  group whose name starts `mu`, to one streaming channel; everything else takes the positional
  path. That is why no `SOUND` animation event ever names a `music_*_sg` group.
- **Variant choice is the group's own weighted random with recency, not a chapter, act or
  sequence.** `FUN_0059a440` draws the member, `FUN_0059ab60` carries the ordinal across and
  `FUN_005954f0` maps it with `ordinal % 6`. Each family draws independently, so prebattle 3 and
  battle 6 in one mission is normal and the six tracks are not a matched score set.
- **The objective stingers are NOT random.** `FUN_005954f0` overrides the group's pick with a
  per-family counter (`counter & 1`, incremented), so each two-take stinger family alternates
  strictly from the first cue of a process.
- **No crossfade, and no restart of the playing track.** `FUN_00595140` returns immediately when the
  requested WAV is the one already streaming, and otherwise stops the old stream and starts the
  new one. Track changes are hard cuts.
- **Loop:** bit 0 of the definition's flag word (the data's `LOOPED`), with `FUN_005954f0` forcing
  it on for `music_battle_sg` and `music_prebattle_sg`. Only `battle1-6` carry `LOOPED` in the
  data, so prebattle loops by the override; everything else plays once into silence.
- **Fades belong to three groups only,** the ones `player.zrd` binds. `FUN_0046cdf0`'s rates are
  +4.0/s in (0.25 s), −0.25/s out (4 s) and −4.0/s fast out. The shipped `player.zrd` binds only
  `in_battle_sound = music_battle_sg`; `pre_battle_sound` and `won_battle_sound` hold the
  placeholder `your_sound_here`. **Battle music is the only track in the game that fades.**
- **The prebattle → battle trigger is an engine detector, refining A1's note.** The binding is data,
  the trigger is code: `FUN_0046c870` runs a battle timer off the objectives tick; `FUN_0046c850`
  refreshes it to **20 s** and never downward; `FUN_0046c700` pings it from a 5-second proximity
  scan when **more than 2** other vehicles sit within **1000 m** of the player (and only while
  battle music is silent); `FUN_004b9bc0`'s player branch pings it on damage taken. The timer runs
  down only while battle music actually plays, then fades out over 4 s into silence, not back to
  prebattle.
- **The cabin track is `music_splash.wav`,** which settles `CAP-44`. `GLOBALS.SCRIPT`'s one shared
  sound object at loop count 5 is the whole out-of-mission score; no screen has a track of its own.
- **Census.** All 24 campaign missions cue music; none of the 8 Instant Action or 21 multiplayer
  missions do. `prebattle` 24 missions / 37 cues, `missionsuccess` 22/22, `primaryobj` 20/29,
  `secondaryobj` 20/24, `battlesuccess` 16/19, `tertiaryobj` 1/2 (`C5/M03`). `music_battle_sg` is
  cued by no mission at all.

**Disproofs landed.** `music_instantaction.wav` and `music_spicyairtales.wav` have **no trigger
anywhere**: neither definition is named by any mission, animation definition or ROF script, and the
executable references their WAV names only in the resolver and the registration table. Instant
Action ships silent, and "spicyairtales as the cabin radio" is dead. `music_airtales.wav` (an exe
branch and handle slot) and `music_loop.wav` (`AUDIO.SCRIPT`'s options-page preview) ship in neither
sound archive.

**Named gaps.** (1) What stops music on the return to the cabin is not traced; the success tracks
are one-shots, so the channel falls silent on its own. (2) `FUN_0046c700`'s skip predicate is not
identified, so "more than 2 vehicles within 1000 m" may be narrower (wrecks or friendlies may be
excluded). (3) `snd_music`/`snd_music1..3` is a `MUSIC` group over three definitions that do not
exist and is cued by nothing. (4) `SoundDefs.SoundGroup.Pick` implements the simple recency reading;
the exe's decay persists and renormalizes to 100 across picks. That is a distribution difference,
not a wrong-member difference, and changing the shared picker would move every existing
weighted-sound suite, so it is recorded rather than applied.

**Implementation.** `CSVM/src/Mech3/MusicPlayer.cs` (new): a session-level `Node` holding one
`AudioStreamPlayer` (2D, non-positional), beside `WorldSounds` rather than inside it. Track loading
goes through the existing `SoundArchive` path via a `Loader` delegate, the same seam
`WorldSounds.Loader` uses; `audio.volume` needs nothing here, since `Launcher` applies it on the
master bus and this player is on it. API:

| Member | What it does |
|---|---|
| `Enter(MusicState, Random)` | Cues the state's group/definition; returns the WAV now playing, or null when nothing changed |
| `Cue(name, rng, loops, forceLoop)` | The raw form, for a name the data supplies directly |
| `Stop()` | Cuts the channel silent |
| `NoteCombat()` | The combat ping: refresh the battle hold to 20 s, never downward |
| `ScanPings(nearbyVehicles)` | Static: whether the decoded proximity scan would ping |
| `Tick(dt, rng)` | Battle hold, fade and the loop restart |
| `Current` / `State` / `Looping` / `Gain` / `BattleHold` | Read-only state for assertions and the HUD |

`MusicState` is `Silent, Menu, Prebattle, Battle, BattleSuccess, MissionSuccess,
PrimaryObjective, SecondaryObjective, TertiaryObjective`, each mapping to the one name the
original's data cues. The decoded constants are public: `BattleHoldSeconds` 20, `BattleScanSeconds`
5, `BattleScanRadiusM` 1000, `BattleScanMinNearby` 3, `FadeInPerSecond` 4, `FadeOutPerSecond` 0.25,
`MenuLoopCount` 5.

**Wiring contract (not applied here: C21 owns `LaunchMenu.cs`, D31 owns `GameSession.cs`, and the
campaign screens are Wave C's).** The orchestrator applies this after C21/D31 land.

1. **Construction, once per process, in `Launcher`** beside the launch-screen build, so one channel
   outlives every session and a mission launch does not restart the cabin track mid-fade:
   `_music = new MusicPlayer(soundDefs, soundGroups) { Loader = (def, looped) =>
   archive.Find(def.WavName, looped, warn: false) };` then `AddChild(_music);`. The defs and groups
   are `SoundDefs.Load(zrdrPath)` / `LoadGroups(zrdrPath)` over the shared `zrdr` path. The
   `SoundArchive` must be a process-lifetime one, not the build-scoped `SessionArchives.Sounds`
   (`SoundsOutliveBuild = false`); open a second `SoundArchive` over `SessionPaths` sounds for this
   player alone. Call `_music.Tick(delta, _rng)` from `Launcher._Process`.
2. **Menu and cabin**, in `LaunchMenu.cs` where the board becomes visible, and again in the cabin
   page's enter: `Music.Enter(MusicState.Menu, rng);`. It is safe to call on every screen entry,
   because a cue for the playing track is a no-op, which is exactly how the original's
   `mail(11004)` behaves. `mail(11003)`'s counterpart is `Music.Stop()`, called once where the
   session leaves the boards for a mission launch.
3. **Mission start**, in the campaign director's bootstrap after the objectives runtime exists:
   nothing. Prebattle is cued by the mission's own `WAKEUP_SOUND_GROUP music_prebattle_sg`, so the
   director's `WAKEUP_SOUND_GROUP` executor routes a name that `MusicPlayer` recognises to
   `Music.Cue(name, rng)` instead of `WorldSounds.PlayOneShot`. The test is the group name starting
   with `mu` or the resolved definition carrying `MUSIC`, matching `FUN_00593590`.
4. **Battle**, in the director's per-frame tick: count other live vehicles within
   `MusicPlayer.BattleScanRadiusM` of the player at most every `MusicPlayer.BattleScanSeconds`, and
   only while `Music.State != MusicState.Battle`; call `Music.NoteCombat()` when
   `MusicPlayer.ScanPings(count)`. Call `Music.NoteCombat()` again from the player-damage path.
   `MusicPlayer.Tick` does the rest.
5. **Objective completion and mission end**: rule 3 already covers them, since the stingers and both
   success families are cued by `WAKEUP_SOUND_GROUP` from the mission data. Nothing hard-codes a
   state at mission end.
6. **Instant Action**: nothing. The decode says Instant Action ships silent.

**Verify.** `music-states` (`CSVM/src/Testing/MusicSuites.cs`, registered last in `SuiteCatalog`)
asserts the track name from `.scratch/logs/` at `--volume=0`: each of the eight states cues its
family and carries the right loop flag, re-entering the playing state returns null, the primary
stingers read `2,1,2,1` (alternating, not drawn), and a combat ping cuts prebattle to battle on the
fade ramp, reaches full gain within a quarter second, holds through the 20 s and fades to silence
over four. `dotnet build` clean (0 warnings), `dotnet test` 2029/2029, engine suite
`music-states` PASS with engine errors clean, all 16 golden shots hash-identical.
**Verified.** `RunTests.ps1` on the plan branch (C21, the shared flow seam and D37 together):
build clean, units 2053/2053, engine suites 95/95 with errors clean, all 16 golden shots
hash-identical.

**⚠ Traps.** ⚠ Do not add a crossfade; the original hard-cuts, and the only ramp in the game is
battle music's. ⚠ Do not give Instant Action or the cabin a track the data does not name: the
cabin is the splash track, and `instantaction`/`spicyairtales` are cued by nothing. `--mute` is
load-time blind (nothing counted or logged); use `--volume=0` for tests.

# Wave E — end to end and sign-off

## E41 ☐ The full loop on C1

**Goal.** On a clean `user://`: create a profile, enter the cabin, brief, flight-check, change an
ammo fit, fly the first C1 mission through its intro cutscene and objectives to completion,
return to the cabin, see Next Mission advanced and the result recorded; quit, relaunch, and find
the state intact.

**Evidence (confidence: traced once B/C/D land).** This is the integration of every prior item.

**Approach.** A scripted in-engine suite covering the loop headless where possible, plus one
manual pass; fix what it finds under this item only when the fix is glue, otherwise reopen the
owning item.

**Model recommendation.** medium.

**Verify.** The suite in `RunTests.ps1` green twice in a row (persistence must survive the second
run); full golden manifest unchanged.

**Groundwork already landed (the shell integration step, not this item's tick).** The loop is
walkable end to end and the pieces below are measured; what E41 still owes is the whole loop in one
scripted suite, run twice.

- **Screens.** Five scripted shots, windowed, one per screen, over the seeded scratch profile:
  `--menu=campaign-cabin`, `campaign-previous`, `campaign-briefing:24`, `campaign-flightcheck`,
  `campaign-ammo`. All five render their art: the cabin's `PC_BACKGROUND.PNG`, the briefing's own
  mission map with two objective lines uncovered at 24 s, and the ammo screen's top and front
  diagrams framed on the pilot's Devastator.
- **The hangar door.** `--menu=campaign-hangar` makes the cabin's PLANE CONSTRUCTION press and
  lands on the Build Custom Plane flow's plane selection, opened over the profile's own wallet.
- **The launch.** `--menu=campaign-fly` walks the first real profile to its next mission's flight
  check and presses FLY MISSION. The console carries the whole handoff: the launchscreen's own
  line, `campaign: seq 0 'Hawaii mission 1' -> c3/m01`, the director armed with 39 objectives and
  6 display rows, and `campaign: wingman_1 flies 'The Knave' as player_pfighter`. The wingman
  binding is resolved and reported; nothing spawns that aircraft yet, which is D34's.
- **Audio at `--volume=0`, from `.scratch/logs/`.** `music play cue=snd_music_splash
  wav=music_splash.wav loop=5` on the board, `music stop wav=music_splash.wav` on the launch, and
  `briefing narration start=1 wav=c1-HA-m3_briefing.wav stream=yes` on the briefing.
- **Not yet observed: a track cued by mission data.** C3/M01 cues `music_prebattle_sg` from
  `OBJECTIVE29`, which is `BEGIN_DORMANT` and fires its group on completion, so 40 s of unpiloted
  straight flight never reaches it. The routing itself sits in the director's one sound-group
  executor, which serves both `WAKEUP_SOUND_GROUP` and `COMPLETED_SOUND_GROUP`.
- **Not yet observed: the return to the cabin.** The path is wired end to end
  (`MissionEnded` → `LauncherContext.ReturnToCabin` → `OpenCampaignCabin`) but reaching it needs a
  mission actually flown to its end, which is exactly what this item's own suite is for.

**⚠ Traps.** A green first run does not prove persistence; the second, state-carrying run is the
test.

## E42 ☐ At-the-controls verdict pass

**Goal.** The user plays the C1 campaign start to finish against the A8 captures and the five
reference PNGs and signs off screen fidelity, briefing flow, cutscenes, objectives and wingmen,
or files the corrective items.

**Evidence (confidence: n/a — this is the gate).** The user's eyes outrank instruments (repo
memory); every prior plan closed through such a wave.

**Approach.** Stage the build, hand over a short what-to-check list keyed to plan items, collect
verdicts into corrective items (a Wave F if needed, as PLAN-hangar's Wave E was).

**Model recommendation.** medium, low effort — orchestration only.

**Verify.** The user's verdict, recorded in the closing commits.

**⚠ Traps.** Findings become items with their own traps sections, not silent re-edits.
