# The scrapbook: the campaign's mission-end screen and the book behind it

**ACTIVE PLAN** (written 2026-08-29). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan delivers the screen a finished campaign mission ends on, and the book that screen is one
page of. Today a mission ends, holds its last flown frame for two seconds, and cuts to the cabin
with no statement of whether the player won (`BL-622`). The original ends on the **scrapbook**,
opened at the mission just flown: a two-page spread per mission with the results block on the first
page and a page of story scraps on the second, twenty-five mission slots, every scrap clickable into
its own detail view, and a bookmark back to the current mission. The decode of the mission-end pass
behind it is [`docs/org/debrief.md`](org/debrief.md), produced in the session that wrote this plan;
the record it fills is [`docs/formats/saved-games.md`](formats/saved-games.md), "The mission-result
array".

Two boundaries. The cabin's **Change Memento** picker (`BL-463`) is out of scope even though it
reads the same `assets\graphics\scrapbook\` directory, because it is a cabin control and not part of
this book. **No new art is authored**: every scrap this plan draws already ships, and a mission slot
with no shipped scrap gets an empty page, not an invented one. `BL-256`'s danger-zone screenshot
capture is in scope only as the slot that mounts it (D21); whether the capture itself lands here or
stays `BL-256` is D21's own first question.

**Backlog provenance.** `BL-622` and `BL-624` were both re-verified still-open in the session that
wrote this plan: `git log --grep=BL-622` returns only its minting commit and this session's three,
and the code path from `CampaignDirector.OnMissionEnded` to `Launcher.OpenCabin` was read end to
end. `BL-256` and `BL-463` are cross-referenced but were **not** re-verified; each carries a TODO
where it is used.

## Milestone goal

- A finished campaign mission, won or lost, ends on the scrapbook opened at that mission, with the
  outcome, the five results rows and the per-airframe kill stamps reading what the flight actually
  did.
- The scrapbook is navigable as a book: two pages per mission, arrows that step page then mission, a
  Current Mission bookmark when the reader has wandered, and Replay Mission on the results page.
- Every scrap is clickable and opens in its own detail view.
- A mission failed four times offers to skip it, and taking the offer advances the campaign the way
  the original does.
- A lost attempt records which objectives it met instead of recording nothing.

**No new board is created, and no scrap art is authored.** `CampaignPreviousMissionsPage` already is
the scrapbook; this plan grows it. Every asset the book draws is in the shipped install, and an
empty slot stays empty.

## Decisions (2026-08-29)

| # | Question | Decision |
|---|---|---|
| 1 | Is the debrief a new board or an existing one? | **An existing one.** Every label on it is an `IDS_SB_*` langui row, the Instant Action wrap-up uses a separate `IDS_IAWU_*` set, and `ASSETS/SCRIPTS/` has `SCRAPBOOK.SCRIPT` and `SCRAPBOOKZOOM.SCRIPT` but no debrief script. `CampaignPreviousMissionsPage` is that board. |
| 2 | Is `BL-622` one backlog item or a plan? | **A plan.** The screen is one page of a twenty-five-slot book with per-scrap zoom, navigation and a capture feature hanging off it. Called by the author at the controls. |
| 3 | Where does function-level decode prose live? | **`docs/org/`, not `docs/formats/`.** The format pages carry field layouts and point at the org page for the pass that fills them. Author's instruction; `docs/org/debrief.md` and the `saved-games.md` edits follow it. |
| 4 | Do the per-airframe tallies belong to `BL-622`? | **No, they are `BL-624`.** The screen cannot draw them until something counts them, and nothing in CSVM does. Split so the screen is not blocked on a decode that is still open. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The record's two twelve-byte arrays at `+0x08` and `+0x14` are per-weapon-class shots and hits. | `OriginalScreenshots/Campaign Mission End screen CM01.png` and `…CM02 Mission select after another Mission.png`. Shots and hits are the separate `+0x20`/`+0x22` pair that the Gun Hit Ratio row divides (16% and 18% on the two shots). CM02's stamps read `3 Peacemaker`, `2 Balmoral` and a starred `1 Peacemaker` over an Overall Planes Downed of 6, so the arrays sum per airframe to the total. |
| 2 | `FUN_00416de0` remaps eleven weapon classes into twelve record slots with slot 11 as a catch-all. | Reading its table at `0x0061f670`: eleven pairs, key equal to value for 0 to 10. It is the identity map, the fall-through to 11 is never taken on this path, and the function is a general-purpose lookup called elsewhere with a constant. It is not evidence about what the arrays index. |
| 3 | The debrief is a screen of its own and needs a new `CampaignScreen` and board. | The langui symbol prefixes (`IDS_SB_*` against `IDS_IAWU_*`) and the script listing. See Decision 1. |

⚠ **Claims 1 and 2 were committed to `docs/org/debrief.md` before the screenshots arrived and were
withdrawn in `2361cfe8`.** The lesson is the one this repo keeps relearning: a decode of the
*writer* does not tell you what a field *means*; the screen that reads it does. Do not re-derive
either claim from `FUN_00419630` alone.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | B11, B12, C15, C17 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | C16, D18, D20 | The *what* is settled by the screenshots; the exact geometry is not, and is A1's output. |
| **Leads only, no mechanism yet** | A1, A2, A3, B13, B14, D19, D21 | Budget for investigation; A1 and A2 may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

**The art.** `extracted/rof/ASSETS/GRAPHICS/SCRAPBOOK/`, 238 files, 213 once the `.TIF` masters are
dropped. Named `<kind>_<mission>_<page>_<name>`:

| Kind | Count | What it is |
|---|---|---|
| `SB_` | 163 | The scraps. Mission slots `00` to `24`: the 24 campaign missions plus slot `00` for the not-yet-started career (langui 1217 `Starting My Career`, 1218 `Above the clouds`). |
| `NT_` | 8 | Blueprint scraps, one per aircraft or system the story introduces (`NT_02_01_BPBALMORAL`, `NT_07_01_BPNITRO`, `NT_19_01_BPTORPEDO`). |
| `MS_P_` | 41 | The cabin memento photographs, not scrapbook pages. `UIData +0x344` names the current one. Out of scope (`BL-463`). |
| `DZ_GENERIC_CORNERS.PNG` | 1 | The photo-corner mount drawn over a danger-zone scrap. |

Page `01` is the story page and carries almost everything, from 2 scraps (missions 10 and 15) to 13
(mission 07). The `SB_00_00_*` set (`NEWS1`, `NEWS2`, `MAG1`, `MAG1S`, `DOC1`, `DOC2`, `FHLOGO`) are
the generic scraps the results pages reuse, which is why the Aloha Daily masthead appears on both
CM01's and CM02's. Only four scraps carry a distinct `…S` small version, so most are one bitmap
shown at two sizes.

**The rows.** Every label is a langui `IDS_SB_*` row in `extracted/rof/ui_strings.json`:

| Row | Title | Value format | Record field |
|---|---|---|---|
| outcome | 1201 | 1213 `Mission Completed` / 1214 `Mission Failed` | mask bit 0 |
| heading | 1202 `Mission Results` | | |
| 1 | 1203 `Run Time` | 1208 `%1!02d!:%2!02d!` | `+0x04`, milliseconds as `mm:ss` |
| 2 | 1204 `Rockets Expended` | 1209 `%1!d!` | not located (A2) |
| 3 | 1205 `Gun Hit Ratio` | 1210 `%1!d!%%` | `+0x22` over `+0x20` |
| 4 | 1206 `Cash Earned` | 1211 `$%1!d!` | `+0x28` |
| 5 | 1207 `Overall Planes Downed` | 1212 `%1!d!` | not located (A2) |

Tabs are 1159 `Best to Date` (the merged half at `+0x54`) and 1160 `Most Recent` (the attempt half
at `+0x00`); 1200 `Current Mission` is the bookmark. 1215 `%1!s! - %2!s!` is the page title (name
and area), 1216 `%1!s! - Scrapbook` the book's own, and 1219 `Not yet flown ` an unflown slot.

**The decode addresses**, all from [`docs/org/debrief.md`](org/debrief.md): the pass is
`FUN_004194e0`, reached from `FUN_00443090`; the record is `UIData +0x1868` indexed `seq + 1`; the
screen push is `FUN_0046fb60(0x0071d57c, …)`; the campaign win flag is `campaign +0xc58` through
`FUN_00463be0` / `FUN_00463c10`; the attempt counter is `[0x0071b494 + idx*0x10]` with
`idx = mission + 10*chapter`; the tally object is `0x0071d2a0`.

**Three screenshots** are the visual ground truth, all under `OriginalScreenshots/`:
`Campaign Mission End screen CM01.png` (the results page as a mission ends),
`Campaign Scrapbook CM01 Story Scraps.png` (a story page, with the danger-zone photograph in its
corners), `Campaign Scrapbook CM02 Mission select after another Mission.png` (the Current Mission
bookmark and three kill stamps).

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

### Wave A — decode what is still unread

1. ☐ Decode the page composition: which scrap sits where, at what coordinates, with what zoom text
2. ☐ Decode the two per-airframe tallies and the two unmapped rows
3. ☐ Decode the book's navigation and its entry paths

### Wave B — the record and the director

11. ☐ A lost attempt keeps its objective bits
12. ☐ Carry the mission result across the launcher's deferred hop
13. ☐ Count the per-airframe tallies and merge them per index
14. ☐ The per-mission attempt counter and the four-attempt skip offer

### Wave C — the results page

15. ☐ The results block: five rows, the outcome line and the two tabs
16. ☐ The per-airframe kill stamps
17. ☐ Enter the scrapbook at mission end, and Replay Mission

### Wave D — the book

18. ☐ The story page and its per-mission scrap composition
19. ☐ The per-scrap detail view
20. ☐ Navigation: page and mission arrows, and the Current Mission bookmark
21. ☐ The danger-zone scrap slot

## Dependency and parallelism notes

**A1 blocks D18, D19 and D20**; nothing in Wave D can be built against invented geometry, and A1 is
the census that supplies it. **A2 blocks B13 and C16**: the stamps cannot be drawn until something
counts them, and nothing should be counted until the decode says what the original counts. **A3
blocks C17 and D20.**

Wave B is independent of Wave A except for B13, so B11, B12 and B14 can run while Wave A is still
prospecting. **B12 blocks C17** (the mission-end entry needs the result carried across the frame
boundary), and **B11 blocks C15** (the row rendering must not be built against a mask that is zero
on a loss).

File contention, and these must not run in parallel worktrees:

- `CSVM/src/Session/CampaignDirector.cs`: B11 and B14.
- `CSVM/src/Session/Launcher.cs`: B12 and C17.
- `CSVM/src/UI/CampaignPreviousMissionsPage.cs`: C15, C16, D18, D19 and D20 all edit it. Run them
  in listed order in one worktree, or split the page into per-page classes as C15's first move and
  give each later item its own file.
- `CSVM/src/Session/CampaignProgression.cs` and `CampaignProfileStore.cs`: B13 alone.

---

# Wave A — decode what is still unread

## A1 ☐ Decode the page composition: which scrap sits where, at what coordinates, with what zoom text

**Goal.** A written record, per mission slot and page, of which scrap assets are drawn, where, at
what size, and which carry detail text, sufficient to lay out all twenty-five slots without opening
the original again.

**Evidence (confidence: lead-only).** The assets and their naming are censused above, so *what*
exists is settled; *where each goes* is not. `docs/formats/campaign-screens.md` is the model for how
this repo records an authored screen, and it already documents the cabin's `pc_memento` pane reading
`"assets\graphics\scrapbook\" + <name>` from `uiData` 2150, so the same callback family very likely
serves the book. The scripts are `extracted/rof/ASSETS/SCRIPTS/SCRAPBOOK.SCRIPT` and
`SCRAPBOOKZOOM.SCRIPT`. `docs/org/hangar.md` documents the two callback dispatchers those scripts
call and the id-resolution rule for the `2100`–`2413` range, which is the tool for reading any id
they use.

**Approach.** Read the two scripts first; they are pure layout, as the hangar page records for its
own four. Resolve every `uiData` callback id they invoke through the dispatchers in
`docs/org/hangar.md`. Write the result into `docs/formats/campaign-screens.md` as a scrapbook
section, matching that page's existing per-screen shape, and keep function-level prose in
`docs/org/debrief.md` per Decision 3.

**Model recommendation.** high. A reverse-engineering pass over an unread screen family whose output
every Wave D item is built on, so a wrong reading here is expensive downstream.

**Verify.** The written layout reproduces all three reference screenshots: CM01's results page,
CM01's story page and CM02's results page, scrap for scrap and position for position.

**⚠ Traps.** The `…S` suffix is a small version, not a separate scrap; four exist, and treating
every scrap as having one will send you looking for 159 missing files. Slot `00` is the
not-yet-started career and is not mission 1. Do not assume the results page's mementos are authored
per mission: the generic `SB_00_00_*` set appears on more than one.

## A2 ☐ Decode the two per-airframe tallies and the two unmapped rows

**Goal.** A statement of what each of the record's two twelve-byte arrays counts, what distinguishes
the starred stamp from the plain one, and what feeds the Rockets Expended and Overall Planes Downed
rows, each with the address it came from.

**Evidence (confidence: lead-only).** `docs/org/debrief.md`: the arrays are filled from the tally
object at `0x0071d2a0` (`+0x00 + 4i` through `FUN_004a23a0`, `+0x30 + 4i` through `FUN_004a23c0`,
`i` in 0 to 10, each dword truncated to a byte). The screen's arithmetic says they are two
per-airframe kill tallies: CM02 draws `3 Peacemaker`, `2 Balmoral` and a starred `1 Peacemaker` over
a total of 6. Neither increment site has been traced. Already ruled out: the per-weapon shots/hits
reading (see the disproven table) and `FUN_00416de0` as a meaningful remap.

**Approach.** Take the xrefs to `0x0071d2a0` that write rather than read (`FUN_0046a490` at two
sites, `FUN_004b9bc0` at two, `FUN_00464680`, `FUN_00495310`, `FUN_004a2210`) and read what indexes
them. The Ghidra project is **read-only**: no renames, no comments, no `save_program`. Land the
answer in `docs/org/debrief.md`, replacing its ⚠ "not decoded" note, and amend `BL-624`.

**Model recommendation.** high. A decode whose wrong answer already cost this plan one withdrawn
commit.

**Verify.** The decoded rule reproduces CM02's stamps and its total of 6 from the same flight's
events, and reproduces CM01's single `3 Kestrel` stamp with a total of 3.

**⚠ Traps.** Do not re-reach for the per-weapon reading. The tally object's own field boundaries are
ambiguous between an eleven-wide array with trailing scalars and a twelve-wide one: `FUN_004a22a0`'s
reset loop fits both, so settle it from an indexed write site and not from the reset. `0x0071d2fc`
and `0x0071d300` sit immediately after the arrays and are the Gun Hit Ratio pair; do not fold them
into the arrays.

## A3 ☐ Decode the book's navigation and its entry paths

**Goal.** A statement of how the book is entered from a mission end and from the cabin, what the
arrows step, when the Current Mission bookmark appears, and what Replay Mission and View All
Missions do.

**Evidence (confidence: lead-only).** Reported at the controls: the right arrow steps to the next
page and, from a mission's story page, to the next mission; a mission other than the current one
raises the Current Mission bookmark, which jumps back. The screenshots show Replay Mission inside
the results block on the results page only, and View All Missions plus Return to Cabin on both
pages. `docs/formats/campaign-screens.md` records that "the scrapbook screens mail `11003`", so the
screens are already partly touched there.

**Approach.** The same script-and-callback read as A1, plus `FUN_0046fb60`'s argument at
`0x0071d57c` for the entry, and `FUN_004194e0`'s tail for what the mission end sets before it.

**Model recommendation.** medium. Bounded reading, with A1's method already established.

**Verify.** <TODO: state the check once A1 has established how a screen's flow is recorded here.>

**⚠ Traps.** Entering from the cabin and entering from a mission end differ by at least the tab
opened on and the presence of Replay Mission; do not assume one path. <TODO: confirm from a
cabin-entry screenshot whether anything else differs.>

# Wave B — the record and the director

## B11 ☐ A lost attempt keeps its objective bits

**Goal.** A failed mission records exactly which objectives it met, with only the primary bit clear,
so the results page can show per-objective state on a loss.

**Evidence (confidence: traced).** `CSVM/src/Session/CampaignDirector.cs:650` writes
`outcome == MissionOutcome.Won ? graph.CompletedMask : 0`. The original runs both of its
mask-building loops regardless of outcome and sets bit 0 from the campaign win flag alone
(`docs/org/debrief.md`, "The pass, in order" and "The completed-objective mask has two sources").

**Approach.** Bank `graph.CompletedMask` with bit 0 forced clear on a loss instead of banking zero.
Check `CampaignProgression.Record`'s best-of merge still gates the advance on bit 0 alone, since it
will now see non-zero masks on failed attempts.

**Model recommendation.** medium. A one-line behaviour change with a merge rule downstream of it
that must be re-read, not a mechanical edit.

**Verify.** `.\RunTests.ps1 -Suite campaign-mission-end -SkipUnits -SkipGoldens` plus the campaign
suites that assert the recorded mask (`CSVM/src/Testing/CampaignSuites.cs:324`). Take a baseline
first: the assertion currently passes against a zero mask on the loss path, so an unchanged pass
proves nothing until it has been seen able to fail.

**⚠ Traps.** `CampaignProgression`'s advance and its best-of merge are both gated on bit 0; a
non-zero mask on a loss must not advance the campaign or overwrite the best record. The persist-log
commit (`CampaignPersistLog.CommitsOn`) is a separate outcome test and is not part of this change.

## B12 ☐ Carry the mission result across the launcher's deferred hop

**Goal.** The `CampaignMissionResult` reaches the launchscreen side intact, alongside the profile
name, so a page can be opened on it.

**Evidence (confidence: traced).** `Launcher.cs:812` passes `profile => _pendingCabin = profile`, so
only the profile name survives. `Launcher.cs:683` acts on `_pendingCabin` at the top of the next
frame and `Launcher.cs:1006`'s `OpenCabin` opens the cabin. `GameSession.cs:788` has the full result
in hand and drops everything but the profile name.

**Approach.** Widen the deferred field from a profile name to the pair, and leave the timing exactly
as it is; the frame deferral is why the `QueueFree` is safe.

**Model recommendation.** medium. Small, but it touches the session-to-launcher boundary where a
mistimed free is a crash.

**Verify.** `.\RunTests.ps1 -Suite campaign-mission-end -SkipUnits -SkipGoldens`, then a flown CM01
to a win and to a loss with the result logged on arrival at the launchscreen side.

**⚠ Traps.** The world stays up for the rest of the frame after the end is raised. Do not move the
free earlier to simplify the carry, and do not read the world from the launchscreen side afterwards.

## B13 ☐ Count the per-airframe tallies and merge them per index

**Goal.** `MissionAttempt` carries whatever A2 decodes the two arrays to be, and
`CampaignProgression` merges them per index by maximum the way the original does.

**Evidence (confidence: lead-only, and blocked on A2).** `MissionAttempt`
(`CSVM/src/Session/CampaignProgression.cs:11`) has scalar `Shots` and `Hits` only, off
`CampaignDirector`'s world view (`CampaignDirector.cs:811`, `ProjectilePool.CannonRoundsFired`). The
record's merge rule for both arrays is per-index maximum
(`docs/formats/saved-games.md`, "The mission-result array").

**Approach.** <TODO: name the count sites once A2 says what is counted.> Then extend
`MissionAttempt`, the merge in `CampaignProgression`, and the profile serialisation in
`CampaignProfileStore`.

**Model recommendation.** medium. Mechanical once A2 has answered, with a serialisation format to
keep compatible.

**Verify.** <TODO: a flown mission whose kills are known, with the tallies read back off the saved
profile.>

**⚠ Traps.** The arrays are written on the campaign path only; in game mode 3 the same two record
offsets hold a summed `ushort` and the danger-zone count, so an Instant Action or multiplayer path
must not fill them as arrays. Changing the profile record's shape is a save-compatibility change:
<TODO: confirm how `CampaignProfileStore` versions its JSON.>

## B14 ☐ The per-mission attempt counter and the four-attempt skip offer

**Goal.** A mission failed four times without ever having been completed offers to skip it, and
accepting advances the campaign.

**Evidence (confidence: lead-only).** `docs/org/debrief.md`, "The four-attempt skip offer": the
counter is `[0x0071b494 + idx*0x10]` with `idx = mission + 10*chapter`, gated on
`[0x0071b488 + idx*0x10]` being zero (never completed), and every fourth increment opens langui
string **191** as a `dialog.zrd` `MESSAGEBOX`; a Yes sets the campaign win flag and re-enters the
debrief, so the skip is a synthetic win. CSVM keeps no per-mission attempt count.

**Approach.** Add the counter to the profile beside the mission-result array, increment it on the
loss path in `CampaignDirector.OnMissionEnded`, and raise the offer from the results page rather
than the director, since it is part of the screen.

**Model recommendation.** high. It can advance a player's campaign, so the gate conditions and the
"never completed" test have to be exactly right.

**Verify.** Fail one mission four times on a fresh profile and confirm the offer appears on the
fourth and not the third, that declining leaves the campaign where it was, and that accepting
advances it and writes the world state the way a win does.

**⚠ Traps.** The counter is not reset while the mission stands uncompleted, so it is not a
consecutive-failure count. The wording of string 191 is **absent from
`extracted/rof/ui_strings.json`**, whose ids jump from 136 to 200; write a placeholder and mark it,
do not invent original wording. Accepting sets the win flag, which makes the world-state save gate
pass, so this interacts with what a skipped mission leaves behind for the next one.

# Wave C — the results page

## C15 ☐ The results block: five rows, the outcome line and the two tabs

**Goal.** The results page shows the outcome line, the five rows and the Best to Date / Most Recent
tabs, reading the flown mission's record.

**Evidence (confidence: traced for the field mapping, blocked on A2 for two rows).** The row and
format table is in "What the data actually ships" above. Run Time, Gun Hit Ratio and Cash Earned map
onto fields CSVM already carries; Rockets Expended and Overall Planes Downed do not (A2). The tabs
are the record's two halves. `CampaignPreviousMissionsPage` is the page to grow, and
`CampaignBriefingPage` is the nearest example of a text-heavy composed board.

**Approach.** Follow `docs/org/campaign-board.md`'s authored-space rule: place at the original's
coordinates from A1 over the page's own background. Use the langui ids as literal strings the way
`IaWrapupBoard` does, since `ui_strings.json` is a build-time extraction artifact and not one of the
archives `SessionArchives.OpenFor` loads.

**Model recommendation.** medium. Board work with an established pattern in the same directory.

**Verify.** A flown CM01 reproduces `Campaign Mission End screen CM01.png` row for row, including
`03:35` from the milliseconds field and `16%` from the shot/hit pair.

**⚠ Traps.** Do not read `ui_strings.json` at runtime. `Gun Hit Ratio` is a ratio of the `+0x22`
pair member over `+0x20` and not either one alone. Two rows are placeholders until A2 lands; mark
them visibly rather than showing a plausible zero.

## C16 ☐ The per-airframe kill stamps

**Goal.** The results page draws one stamp per airframe with a kill count, and the starred variant
where the original draws it.

**Evidence (confidence: direction-sound, magnitude blocked on A2).** CM02's page draws three stamps
summing to its Overall Planes Downed of 6; CM01's draws one `3 Kestrel` with a total of 3. What the
star means is A2's.

**Approach.** <TODO: name the stamp art once A1 has found it; it is not in the `SB_`/`NT_` census, so
it is either drawn from the airframe icon set or lives outside `SCRAPBOOK/`.>

**Model recommendation.** medium.

**Verify.** A flight downing a known mix of airframes reproduces the stamp set and the total.

**⚠ Traps.** The same airframe can appear twice, once plain and once starred; a stamp layout keyed
uniquely on airframe will drop one of CM02's three.

## C17 ☐ Enter the scrapbook at mission end, and Replay Mission

**Goal.** A finished mission opens the scrapbook at that mission on the far side of the two-second
hold, with Replay Mission working, and Return to Cabin leading where the cabin return leads today.

**Evidence (confidence: traced).** `CampaignDirector.Leave` raises `MissionEnded` after
`LeavingHoldS`, which is the hold `BL-623` added (`git log --grep=BL-623`). `Launcher.OpenCabin`
currently calls `ReturnToMenu` then `_menu.OpenCampaignCabin(profile)`; the debrief entry replaces
that second call. B12 supplies the carried result.

**Approach.** Add the screen to `CampaignFlow`'s enum and registry, opened from the launcher's
deferred slot with the result, with the cabin on its far side.

**Model recommendation.** medium.

**Verify.** Fly CM01 to a win and to a loss: the hold runs, the scrapbook opens on that mission, and
Return to Cabin reaches the cabin re-read from the profile the director just wrote.

**⚠ Traps.** The page belongs to the launchscreen side, not the session's. Replay Mission must
re-enter the mission the player just flew and not the campaign's current position, which are
different after a win.

# Wave D — the book

## D18 ☐ The story page and its per-mission scrap composition

**Goal.** Every mission slot's second page draws its shipped scraps at the original's positions.

**Evidence (confidence: direction-sound, geometry from A1).** The census above: 163 `SB_` and 8 `NT_`
assets over slots `00` to `24`, page `01` carrying 2 to 13 scraps.
`Campaign Scrapbook CM01 Story Scraps.png` is the worked example.

**Approach.** Drive the page from A1's composition record. A slot with no shipped scrap draws an
empty page.

**Model recommendation.** medium.

**Verify.** All twenty-five slots render without a missing-asset error, and CM01's page matches its
screenshot.

**⚠ Traps.** No art is authored. A slot with nothing shipped is not a bug.

## D19 ☐ The per-scrap detail view

**Goal.** Clicking a scrap opens it in detail, with the text the small version does not carry.

**Evidence (confidence: lead-only).** Reported at the controls, and `SCRAPBOOKZOOM.SCRIPT` is the
second scrapbook script. Only four scraps ship a distinct `…S` small version, so most are one bitmap
shown at two sizes. Where the detail text comes from is A1's.

**Approach.** <TODO: after A1.>

**Model recommendation.** medium.

**Verify.** <TODO: name a scrap with detail text once A1 has found where the text lives.>

**⚠ Traps.** Do not assume every scrap has a zoom asset; four do.

## D20 ☐ Navigation: page and mission arrows, and the Current Mission bookmark

**Goal.** The arrows step page then mission in both directions, and a mission other than the current
one shows the bookmark that jumps back to it.

**Evidence (confidence: direction-sound).** Reported at the controls; the bookmark is langui 1200
and is visible at the top right of
`Campaign Scrapbook CM02 Mission select after another Mission.png`. The exact behaviour at the ends
of the book is A3's.

**Approach.** <TODO: after A3.>

**Model recommendation.** medium.

**Verify.** Walk the whole book from slot 00 to slot 24 and back, and confirm the bookmark appears
exactly when the shown mission is not the campaign's current one.

**⚠ Traps.** Unflown missions are still pages (langui 1219 `Not yet flown `), so navigation is over
all twenty-five slots and not only the completed ones.

## D21 ☐ The danger-zone scrap slot

**Goal.** A flown danger zone leaves a photograph on that mission's story page, mounted in the
original's photo corners.

**Evidence (confidence: lead-only).** `SB_01_00_DZ.PNG` is a per-mission slot and
`DZ_GENERIC_CORNERS.PNG` is the mount drawn over it; generic corners over per-mission content is
what makes the content captured rather than authored, and `Campaign Scrapbook CM01 Story Scraps.png`
shows a dark photograph in exactly that mount. `BL-256` is the capture half, recorded as intent with
no design, and it names `snd_dangerzone_camera` as the shutter sting.
<TODO: re-verify `BL-256` still-open against `git log --grep=BL-256` and the code.>

**Approach.** First question, and it is a scope call rather than a technical one: whether the
capture itself lands here or stays `BL-256`. This item's floor is the slot and the mount reading a
capture that already exists; its ceiling is the capture too.

**Model recommendation.** medium.

**Verify.** Fly a danger zone and find its photograph on the mission's story page.

**⚠ Traps.** `DzRadius` is 15 m and hand-tuned, and `BL-256` records it as the marker-centre radius
whose screenshot-trigger role is intent and not decode. Do not treat the trigger as settled.
