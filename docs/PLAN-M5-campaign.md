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
| **Traced to an exact mechanism, with the data that proves it** | A1 (landed: `docs/formats/objectives.md`), A2 (landed: `docs/formats/saved-games.md`), A3 (landed: `docs/formats/campaign-sequence.md`), A4 (landed: `docs/formats/briefing.md`), A5 (landed: `docs/org/hangar.md` "The campaign wallet", pending only the `CAP-40` on-screen cross-check), A7 (landed: `docs/formats/anim-definitions/cutscenes.md`), C25 (ammo/loadout base), D34 (station-keeping constants), B13 (threshold field) | Confirm the trace, then implement. |
| **Data present and located, vocabulary not yet decoded** | A6 | Decode first; the docs page is the deliverable, the engine item consumes it. |
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
6. ☐ Behavioral decode of the campaign GUI scripts (cabin, flight check, ammo, campaign intro)
7. ☑ Decode the `letterbox` node mechanics and the cutscene `CALLBACK` codes
8. ☑ Mint and file the owed captures (plane construction screen, previous-missions list, briefing animation, a C1 mission intro, cabin ambience)

### Wave B — campaign model and persistence

11. ☐ Profile store + campaign session model (`user://Profiles/<name>/`, SessionSpec/CLI entry)
12. ☐ Campaign progression + cross-mission persistence (`BL-243`, mission tree from A3)
13. ☐ Wallet and campaign availability wired into the hangar (buy/sell, thresholds)

### Wave C — the out-of-mission screens

21. ☐ Player profile screen (create, select, delete, text entry)
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

**Verify.** Every price shown in the capture matches the decoded constant. **Not yet done:
`CAP-40` is filed but unfilmed.**

**⚠ Traps.** ⚠ Do not tune prices to "feel right" if the trace stalls; a missing constant stays a
named gap, per the invented-content ground rule.

## A6 ☐ Behavioral decode of the campaign GUI scripts (cabin, flight check, ammo, campaign intro)

**Goal.** A decode of what `PASSENGERCABIN.SCRIPT`, `FLIGHTCHECK.SCRIPT`,
`ORDINANCELAYOUT.SCRIPT`, `CAMPAIGN.SCRIPT` and `CAMPAIGNINTRO.SCRIPT` actually do: widget
wiring, mailbox/callback flow, screen transitions, and which engine calls they make, at the depth
`docs/formats/instant-action.md` reached for `INSTANTACTION.SCRIPT`.

**Evidence (confidence: data located, logic undecoded).** The scripts exist as raw obfuscated text
in `extracted\rof\ASSETS\SCRIPTS\`; `docs/formats/rof.md` documents the container and widget keys
only, and names the precedent decode.

**Approach.** Follow the `instant-action.md` method: pair the script text with the `crimson.exe`
GUI-mailbox handlers. Prioritize what C21–C24 need (transition targets, which widget triggers
what, list population); full obfuscated-identifier recovery is not the goal.

**Model recommendation.** high — the obfuscation makes this judgement-heavy.

**Verify.** Each documented transition is consistent with the observable original (screenshots +
A8 captures).

**⚠ Traps.** This item informs the screens but must not gate them; if a behaviour is directly
observable from captures, the screens may land on capture evidence while the script decode
catches up.

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

**Verify.** `playtest.md` lists the five CAPs (`CAP-40` plane construction, `CAP-41` previous
missions, `CAP-42` briefing animation, `CAP-43` C1 intro cutscene, `CAP-44` cabin ambience), each
naming its blocked items; the duplicate-ID pre-commit hook passes.

**⚠ Traps.** ⚠ Footage-derived *measurements* are inadmissible in this repo
(`docs/verification.md`); the captures are for layout, sequence and on-screen values (prices,
labels), not distances or timings used as constants.

# Wave B — campaign model and persistence

## B11 ☐ Profile store + campaign session model

**Goal.** A named profile can be created, listed, selected and deleted, persisted under
`user://Profiles/<name>/`; a selected profile plus a mission id defines a campaign session the
engine can launch (a `--campaign` CLI entry for scripted testing, added to `docs/cli.md`).

**Evidence (confidence: direction-sound).** Persistence precedent: `ScoreStore.cs`
(`user://stunt_scores.json`) and `CustomPlaneStore` (`user://Planes/`). `SessionSpec.cs` is the
closed immutable launch spec with `FromMenu`; `docs/cli.md` has no campaign concept today (checked
2026-08-24). Store schema fields come from A2's structural answers.

**Approach.** New store class following the two precedents; extend `SessionSpec`/`SessionMode`
minimally (read `docs/architecture.md` on `src/Session/` first). Keep the schema versioned from
day one. <TODO: settle the exact schema after A2/A3 land.>

**Model recommendation.** medium — pattern-following with one design seam (the schema).

**Verify.** `RunTests.ps1` green; a scripted `--campaign` launch round-trips create → write →
relaunch → read on a clean `user://`.

**⚠ Traps.** Profile names are user text entry: they become directory names, so sanitize; the
original's roster screen (reference PNG) allows deletion, so deleting must not orphan hangar
planes (decide ownership: hangar saves stay global in `user://Planes/`, per PLAN-hangar; only
wallet/ownership is per profile).

## B12 ☐ Campaign progression + cross-mission persistence (`BL-243`)

**Goal.** Completing a mission records its result in the profile, advances the tree position (per
A3's graph, branches included), and carries the `PERSIST_LOG` destruction subset into later
missions in the sense `BL-243` describes. Previous Missions replays any recorded mission without
advancing the tree.

**Evidence (confidence: direction-sound).** `BL-243` documents the original's
`SAVE_LOG`/`PERSIST_LOG` 62-def subset; A2 tells what per-mission state the original keeps; A3
gives the graph. <TODO: re-verify BL-243 still-open against git log --grep + the code.>

**Approach.** Progression state lives in the B11 store; the persist-log hook sits where the anim
runtime already knows the 62-def subset (read `BL-243`'s entry and `AnimRuntime`'s architecture
entry before choosing the seam).

**Model recommendation.** high — the persist-log seam has blast radius into world build.

**Verify.** Scripted two-mission sequence: destroy a persisted object in mission 1, relaunch
mission 2, assert its state; replay via Previous Missions asserts no tree advance.

**⚠ Traps.** <TODO: the exact 62-def subset semantics; take them from BL-243's write-up, not from
the def names.>

## B13 ☐ Wallet and campaign availability wired into the hangar

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
formula: ten mission/objective pairs paying $140,900 in total, plus five named unsellable
aircraft, each granted once per profile, which B12's progression record must track alongside the
tree position.

**Approach.** Read `PLAN-hangar.md` (completed, in `docs/plans/`) and the `HangarFlow.cs`
architecture entry; add the wallet gate at the existing `Purchase` page seam, parameterized by an
optional campaign context so the IA path is untouched.

**Model recommendation.** medium — the seam was designed for this.

**Verify.** `RunTests.ps1` green; scripted assertions: buy refused under-funds, refused
under-threshold, sell credits exactly the decoded price; IA hangar unchanged (golden manifest
untouched).

**⚠ Traps.** Engine power/weight stayed hangar-only by PLAN-hangar's explicit decision; do not
re-open that here.

# Wave C — the out-of-mission screens

## C21 ☐ Player profile screen

**Goal.** The roster screen per `Campaign Player Profile.png`: name text entry, CONTINUE, roster
list with selection, DELETE PLAYER (confirmed), CANCEL; selecting enters the cabin.

**Evidence (confidence: direction-sound).** Reference PNG reviewed this session; B11 provides the
store; A6 provides the original's screen wiring. Board/menu idiom per decision 7.

**Approach.** New `src/UI/` page on the board chrome; text entry is the one novel widget (keyboard
and pad; the launchscreen's `MenuInput` per-player polling is the input seam). Art from
`extracted\rimage\`/`rof` where identifiable, else the existing board styling; no invented art
labeled as original.

**Model recommendation.** medium.

**Verify.** Scripted `--screenshot` of the screen states (empty roster, filled roster, entry in
progress) plus a manual pass; `RunTests.ps1` green.

**⚠ Traps.** Deletion is destructive: confirm, and honor B11's ownership rule for hangar planes.

## C22 ☐ Campaign cabin screen

**Goal.** The cabin hub per `Campaign Cabin.png`: Next Mission (jumps to briefing of the tree's
current mission), Previous Missions (list of finished missions, pick one → its briefing),
Plane Construction (into B13's hangar), Return to Main Menu. Change Memento is not shipped
(decision 3).

**Evidence (confidence: direction-sound).** Reference PNG; A3 gives Next Mission's source of
truth; B12 gives the finished-missions list; A6/`PASSENGERCABIN.SCRIPT` gives the original's
wiring; the A8 cabin capture gives idle/ambience fidelity.

**Approach.** Board-idiom page; the four buttons route into existing flows (briefing C23, hangar
B13, launchscreen). The painted cabin scene: <TODO: locate the cabin background art in
`extracted\rof`/`rimage` during A6; if it is a rendered 3D set rather than art, decide a static
fallback.>

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
fact), and the loop/crossfade behaviour. No engine music subsystem exists.

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
