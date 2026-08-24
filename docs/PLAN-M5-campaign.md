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
  hand off to gameplay (`BL-134`).
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
| **Traced to an exact mechanism, with the data that proves it** | A1 (landed: `docs/formats/objectives.md`), A2 (landed: `docs/formats/saved-games.md`), A3 (landed: `docs/formats/campaign-sequence.md`), A4 (landed: `docs/formats/briefing.md`), A5 (landed: `docs/org/hangar.md` "The campaign wallet", on-screen cross-check done via `CAP-40`), A6 (landed: `docs/formats/campaign-screens.md`), A7 (landed: `docs/formats/anim-definitions/cutscenes.md`), C25 (ammo/loadout base), D34 (station-keeping constants), B13 (threshold field) | Confirm the trace, then implement. |
| **Direction sound, magnitude or details a judgement call** | B11, B12, C21–C24, D31, D32, D33 | The shape is settled by the original's screens/data; layout metrics, timings and exact behaviours come from captures and decode, not invention. |
| **Leads only — no mechanism yet** | D35 partials (BL-037/038 wiring points), D37 (music selection logic) | Budget for investigation; may end in a disproof. |

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
- **Cutscene defs** — `BL-134`: `generic_intro` ×12 + `mission_intro_animation` ×1 across 13 of 53
  missions (all `M0x` story missions), fully decoded and playable by `AnimRuntime`; the missing
  piece is the consumer (camera, letterbox, sequencing, `CALLBACK` dispatch).
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
  `spicyairtales`. The data is fully extracted; the selection/transition logic (which of the six
  numbered variants when, what triggers prebattle → battle) is undecoded.

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
22. ☐ Campaign cabin screen (Next Mission, Previous Missions, Plane Construction, Return to Main Menu)
23. ☐ Mission briefing screen (map, flags, objectives list, narration; replay / return / flight check)
24. ☐ Flight check screen (pilot + wingmen planes and loadouts, objectives note, plane change, fly mission)
25. ☐ Ammo selection screen (per gun caliber group, per hardpoint, descriptions) for self and wingmen

### Wave D — in-mission campaign machinery

31. ☐ Campaign mission director: the objectives runtime (from A1) + mission end/return flow
32. ☐ Cutscene player: intro animations, letterbox, camera control, handoff (`BL-134`)
33. ☐ In-flight objectives display + objective sound cues
34. ☐ Campaign wingmen: named rosters + netless station-keeping (`BL-362`, `BL-364`)
35. ☐ Mid-mission world fidelity: `WAKE_ANIM` doors (`BL-350`), scripted-path vehicles (`BL-361`), `WorldPartitionSetActive` (`BL-037`), `FogState` (`BL-038`)
36. ☐ AI targeting candidates beyond aircraft (`BL-363`)
37. ☐ Music: playback subsystem + the state-driven track selection

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

**⚠ Traps.** `BL-134` records the user's ruling: the M0x intro defs must play, never be suppressed.
Any interim change that silences them regresses that ruling.

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

**Verify.** Three of the five CAPs are delivered and closed: `CAP-40` (plane-construction
screenshots, consumed by A5's price cross-check), `CAP-41` (previous-missions screenshots, C22's
layout evidence), `CAP-44` (answered at the controls: the cabin has no idle behaviour, only
background music over static art). `CAP-42` and `CAP-43` stay owed in `playtest.md`, each naming
its blocked items; the duplicate-ID pre-commit hook passes.

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
campaign. **Verified.** <pending orchestrator run>

**⚠ Traps.** Deletion is destructive: confirm, and honor B11's ownership rule for hangar planes.
Both hold here, and `DeletingIsConfirmedAndTakesOnlyThatProfile` is the proof, with a sentinel file
beside the profile store standing in for a hangar plane.

## C22 ☐ Campaign cabin screen

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

**Model recommendation.** medium.

**Verify.** Scripted screenshots of each route landing on the right screen; Previous Missions
shows exactly the B12 record.

**⚠ Traps.** `BL-181` defers HUD/scoreboard chrome "pending the menu hub"; this screen is that
hub, so expect that item to reopen against the styling landed here. Do not silently restyle HUD
chrome in this item.

## C23 ☐ Mission briefing screen

**Goal.** The briefing per `Campaign Briefing.png`: the mission map with flags, the objectives
note filled from `MSG_BRF_*`, the briefing animation (progressive reveal) with narration audio,
and REPLAY BRIEFING / RETURN TO CABIN / GO TO FLIGHT CHECK.

**Evidence (confidence: direction-sound).** A4's decode (layout + map/flag data + wav mapping);
the A8 in-motion capture for sequence/timing fidelity; narration wavs already extracted.

**Approach.** Board-idiom page consuming A4's page. Play narration through the existing sound
plumbing (`SoundArchive` loads WAVs; check whether a 2D/UI channel exists or needs a small
addition, distinct from the 3D `WorldSounds` path). Reveal sequencing driven by the decoded data;
where only the capture attests it, mark timings TUNE.

**Model recommendation.** medium; escalate to high only if A4 leaves the reveal mechanism open.

**Verify.** Scripted screenshot per reveal stage on one C1 mission; audio verified from
`.scratch/logs/` (not by muting, per the `--mute` blindness note); all three buttons route
correctly.

**⚠ Traps.** ⚠ Capture-derived timings are TUNE, never fact (A8 trap). String fallback: without
`messages.json` extracted the screen must show raw `MSG_*` keys, not crash.

## C24 ☐ Flight check screen

**Goal.** Per `Campaign Flight Check.png`: mission title, PILOT and WINGMAN rows (plane name,
silhouette, numbered gun list with ammo types, numbered rocket list), Change Ammo per row (into
C25), the objectives note, RETURN TO BRIEFING / FLY MISSION; plane change offered when the
mission allows it.

**Evidence (confidence: direction-sound; loadout base traced).** Reference PNG;
`Loadout.cs`/`stock_loadouts.json` bind the guns/pylons (traced, landed); wingman rosters come
from `aiv.zrd` (`docs/formats/ai-rosters.md`). Which missions allow a plane change:
<TODO: source it in A6/A3; the user states it is mission-dependent, the data home is unknown.>

**Approach.** Board-idiom page reading the mission's roster + the profile's plane/loadout; FLY
MISSION launches the campaign session (B11) with the chosen fits carried into `Loadout.Bind` for
player and wingmen.

**Model recommendation.** medium.

**Verify.** Scripted screenshot; then launch and assert (via the in-engine test harness) that the
flown loadout matches the screen for player and one wingman.

**⚠ Traps.** The silhouette per airframe: reuse the hangar's plane preview path rather than new
art. Ammo lists number to 8 slots with blanks; blanks are data (absent guns), not padding to
invent.

## C25 ☐ Ammo selection screen

**Goal.** Per `Campaign Ammo Selection.png`: ammunition pick per gun caliber group, rocket pick
per underwing hardpoint pair, the description text panel, top/bottom plane views, ACCEPT/CANCEL
LOADOUT; usable for the pilot's and each wingman's plane; the choice persists in the profile and
binds in flight.

**Evidence (confidence: traced for the base).** `Loadout.cs`/`WeaponDefs.cs`/`WeaponBench.cs` and
the hangar's guns/hardpoints pages already model calibers, pylons and fills;
`docs/formats/loadouts.md` documents resolution rules and confirms no on-disk player-loadout
format (persistence goes through B11's store, informed by A2). Ammo descriptions:
<TODO: locate the description strings (likely langui ids) during A6.>

**Approach.** Extend the hangar's guns/hardpoints page pattern into a campaign ammo page keyed by
caliber group; write the selection into the profile store; bind on launch via the existing
`Loadout.Bind`.

**Model recommendation.** medium.

**Verify.** Scripted flow: change one caliber's ammo and one hardpoint, ACCEPT, launch, assert the
bound weapons via the in-engine harness; CANCEL leaves the stored fit untouched.

**⚠ Traps.** Which ammo types exist per caliber is data (`WeaponDefs`), not a list to author.

# Wave D — in-mission campaign machinery

## D31 ☐ Campaign mission director: the objectives runtime

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

## D32 ☐ Cutscene player (`BL-134`)

**Goal.** The 13 story-mission intro defs play as cutscenes: letterbox bars, camera driven by the
decoded `CALLBACK` codes, player input suspended, then a clean handoff to gameplay. C1/M04 is the
first worked case.

**Evidence (confidence: traced for the defs and for the consumer's semantics).** `BL-134`: the defs
are decoded, playable by `AnimRuntime` today; the missing consumer is enumerated there. A7 delivers
the letterbox mechanism and the `CALLBACK` code table
(`docs/formats/anim-definitions/cutscenes.md`). <TODO: re-verify BL-134 still-open against git log +
code.>

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

## D33 ☐ In-flight objectives display + objective sound cues

**Goal.** The current objectives are visible in flight (the original's presentation:
<TODO: capture how the original shows objectives in flight; not covered by the five PNGs>),
updates fire on wake/complete, and `WAKEUP_SOUND_GROUP`/`COMPLETED_SOUND_GROUP` audio plays.

**Evidence (confidence: direction-sound, pending A1 + the TODO capture).** The sound groups are in
the data; `CombatVoice`/`WorldSounds` are the playback paths; `MissionTargets.cs` exists and must
be read before scoping.

**Approach.** HUD element following the existing per-mode readout pattern
(`WeaponReadout`/`MarkerHud` family), fed by D31's objective state; sound via the existing
sound-group resolution.

**Model recommendation.** medium.

**Verify.** In-engine assertions on cue firing (audio counted in logs at `--volume=0`, never
`--mute`); screenshot of the HUD element.

**⚠ Traps.** Extend A8 with this capture if the original's in-flight objectives UI is unknown when
this item starts.

## D34 ☐ Campaign wingmen: named rosters + netless station-keeping (`BL-362`, `BL-364`)

**Goal.** Campaign missions spawn wingmen from their named rosters (`aiv.zrd`), flying the decoded
netless `mode wingman` station-keeping (join, hold station, speed-ramped trail), commanded by the
existing AI machine; campaign patrol nets get their spawner plumbing (`BL-364`'s open half).

**Evidence (confidence: traced constants).** `BL-362` carries the decoded stations (6 m out /
18 m astern of the player leader, 8/−2/−8 of an AI leader, 700 m join, trail 106.68–259.08 m);
`docs/formats/ai-rosters.md` and `ai-nets.md` are landed. <TODO: re-verify BL-362/BL-364 open
halves against git log + code.>

**Approach.** Implement the wingman mode in `AiModeMachine`/`AiControlLaw` from the decoded
constants; the campaign spawner (D31/B11 session build) feeds rosters where Instant Action feeds
waves.

**Model recommendation.** high — touches the AI control law.

**Verify.** In-engine test: wingman joins within threshold and holds the decoded station within
tolerance; the Instant Action wingman suite stays green.

**⚠ Traps.** File contention with `PLAN-flight-model-parity.md` on `AiPilot`/`AiControlLaw`;
sequence, never parallel worktrees. The station constants are decoded facts, not TUNE.

## D35 ☐ Mid-mission world fidelity: `WAKE_ANIM` doors, scripted-path vehicles, partition switching, fog events

**Goal.** Four decoded-but-unbuilt world behaviours land: hangar doors open via `WAKE_ANIM` before
generator spawns (`BL-350`), scripted-path vehicles follow their authored waypoint paths
(`BL-361`), `WorldPartitionSetActive` switches C3 story-mission areas (`BL-037`), and `FogState`
events change fog mid-mission (`BL-038`, shipped once, on C1/M04's intro).

**Evidence (confidence: mixed; each BL carries its own decode).** `BL-361` cites the decoded
waypoint-follower (`docs/org/flightModel.md`, "Ground blow"); `BL-037` counts all 25 uses in
`C3/*.gw`; `BL-038` is a decoded anim event. <TODO: re-verify all four against git log + code;
BL-350's exact `WAKE_ANIM` trigger point needs its entry re-read.>

**Approach.** Four independent sub-changes behind one item; each lands separately with its BL
closed. Order: BL-350 first (it blocks visible C1 behaviour), then BL-361, BL-037, BL-038.

**Model recommendation.** medium per sub-change; BL-361's second movement law is high.

**Verify.** Per sub-change: the citing mission's scripted capture (`--screenshot`/`--freecam`) at
the affected site plus the 8-chapter freecam regression with unchanged counts elsewhere.

**⚠ Traps.** BL-038 fires inside D32's cutscene on C1/M04; land D32 first or verify on a
non-cutscene fog use if one exists.

## D36 ☐ AI targeting candidates beyond aircraft (`BL-363`)

**Goal.** AI target selection considers zeppelins, turrets and structures where the mission's
data says so, making escort/defend campaign objectives functional.

**Evidence (confidence: decoded per the BL).** `BL-363` records the decode; today only aircraft
are candidates. <TODO: re-verify against git log + code, and re-read the BL for the decoded
candidate rules.>

**Approach.** Read `docs/org/targeting.md` and the BL entry; widen the candidate set in the
targeting module per the decoded rules.

**Model recommendation.** medium.

**Verify.** In-engine test: an AI ordered against a zeppelin target engages it; existing combat
suites green.

**⚠ Traps.** <TODO: take from BL-363's entry when re-verified.>

## D37 ☐ Music: playback subsystem + the state-driven track selection

**Goal.** A music player exists (streaming 2D playback, crossfade/stop, `audio.volume` respected)
and plays the right track for the game state: `splash` on the main menu, the cabin/briefing music,
`prebattle` in-mission until combat, `battle` during combat, the `primaryobj`/`secondaryobj`/
`tertiaryobj` stingers on objective completion, and `battlesuccess`/`missionsuccess` at mission
end; Instant Action gets `instantaction`.

**Evidence (confidence: traced for the data, lead-only for the logic).** The 33 `music_*` tracks
are in `extracted\soundsh\` (low-quality twins in `soundsl\`); the state vocabulary is in the
filenames. Undecoded: which of the six numbered variants plays when (per chapter/act? random?),
the prebattle → battle trigger, where `spicyairtales` plays (cabin radio is the guess, not a
fact, and A6 found no script support for it: the out-of-mission screens drive one shared sound
object playing `music_splash`, per `docs/formats/campaign-screens.md`), and the loop/crossfade
behaviour. No engine music subsystem exists. The cabin does play background music in the original
(the at-the-controls answer that closed `CAP-44`); which track is still the trace's to settle.

**Approach.** Decode the selection logic from `crimson.exe` (the track-name string references are
the entry point) before wiring states; build the player as a session-level service beside
`WorldSounds` (2D, not pooled 3D). Subsystem + menu/splash usage can land early; the in-mission
state hooks ride D31's director events. If the trace stalls, land the player with the decoded
subset and file the selection gaps as named backlog items rather than inventing a scheme.

**Model recommendation.** medium for the subsystem; high for the exe trace of the selection logic.

**Verify.** Audio asserted from `.scratch/logs/` at `--volume=0` (never `--mute`): the expected
track name plays on each scripted state transition (menu, mission start, objective complete,
mission end); an A8-adjacent capture of the original confirms the cabin and briefing tracks.

**⚠ Traps.** Variant choice (1–6) and crossfade timings are facts to decode or capture, not TUNE
to invent. `--mute` is load-time blind (nothing counted or logged); use `--volume=0` for tests.

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
