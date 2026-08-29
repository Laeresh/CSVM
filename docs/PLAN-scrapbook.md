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

**A third screen surfaced after this plan was written.** `SCRAPBOOK_TOC.SCRIPT`, the VIEW ALL
MISSIONS list, is a screen of its own with a 25-row mission list, its own Replay and Current Mission
buttons, and its own entry and exit paths. A3 decodes it; **no item builds it**, and whether it
becomes one is an open scope call rather than an assumed inclusion.

**Backlog provenance.** `BL-622` and `BL-624` were both re-verified still-open in the session that
wrote this plan: `git log --grep=BL-622` returns only its minting commit and this session's three,
and the code path from `CampaignDirector.OnMissionEnded` to `Launcher.OpenCabin` was read end to
end. `BL-256` and `BL-463` are cross-referenced but were **not** re-verified; each carries a TODO
where it is used.

## Milestone goal

- A finished campaign mission, won or lost, ends on the scrapbook opened at that mission, with the
  outcome, the five results rows and the per-airframe kill stamps reading what the flight actually
  did.
- The scrapbook is navigable as a book: one to three spreads per mission, arrows that step page then mission, a
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
| 4 | Every mission has exactly two pages. | `SCRAPBOOK.CSV` (A1). A mission has one, two or three spreads: 8 have one, 10 have two, 6 have three. Nothing stores a count; the next-page helper probes the file. |
| 5 | The results block has five rows, one of them Rockets Expended. | `SCRAPBOOK.SCRIPT` binds four values, `uiData` 2406 returns four, and `SB_T_ROCKETS`' y macro is stranded between two others while the four drawn rows sit evenly spaced (A1). The row was cut before release; both results-page screenshots show four. |
| 6 | `SB_01_00_DZ.PNG` is the danger-zone slot and `SB_00_00_*` is a generic pool the results pages reuse. | `SCRAPBOOK.CSV` references neither. A capture is any name beginning `Snap_`, and the `SB_00_00_*` rows appear in mission slot 0 alone (A1). |
| 7 | A `…S` file is a scrap's small version. | `SB_00_00_MAG1S.PNG` and `SB_01_01_WANTED1S.PNG` are unreferenced. There is no small-version mechanism: one bitmap is scaled, and the detail view is the same base name under a second extension (A1). |

⚠ **Claims 1 and 2 were committed to `docs/org/debrief.md` before the screenshots arrived and were
withdrawn in `2361cfe8`.** The lesson is the one this repo keeps relearning: a decode of the
*writer* does not tell you what a field *means*; the screen that reads it does. Do not re-derive
either claim from `FUN_00419630` alone.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A3, B11, B12, B13, C15, C16, C17, D18, D19, D21 | Confirm the trace, then implement. A1 supplies the geometry the Wave D items needed; A2 supplies the counting rule B13 and C16 were waiting on; A3 supplies the entry paths C17 and D20 were waiting on. |
| **Direction sound, magnitude a judgement call** | D20 | The *what* is settled by the screenshots, A1's callback map and A3's entry-path split; the rest is a layout call. |
| **Leads only, no mechanism yet** | B14 | Budget for investigation. |

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

The `<page>` field of a filename is **not** the spread the scrap appears on: nearly every scrap is
named `_01_` whatever spread it sits on, so the names sort the art by mission and nothing more. What
draws where is `SCRAPBOOK.CSV` alone. Six `SB_*` files are referenced by nothing at all
(`SB_00_00_MAG1S`, `SB_01_01_WANTED1S`, `SB_01_00_DZ`, `SB_07_01_HANGAR COPY`,
`SB_12_01_BETTYSSCREENTEST2`, `SB_BG_BZOOMNEWS`), and the 11 unreferenced `MS_P_` files belong to
the cabin's picker rather than the book.

**The composition (A1).** `extracted/rof/ASSETS/SCRAPBOOK.CSV`, one `[SCRAPBOOK]` section of 461
rows keyed `<mission>_<spread>_<item>`, is the whole book: art name, extensions, position, region,
draw order, a per-objective visibility gate, and the three langui ids of the detail view. Spreads
are numbered from 1 and a mission has one to three; spread 1 is the results page. 167 rows are
`Snap_<mission>_<objective>` player captures. The widget geometry is `extracted/rof/ASSETS/LAYOUT.CSV`
as for every other screen, including the eleven kill-stamp slots and the 26 zoom text families.
Both are decoded in [`formats/campaign-screens.md`](formats/campaign-screens.md), "The scrapbook".

**The rows.** Every label is a langui `IDS_SB_*` row in `extracted/rof/ui_strings.json`:

| Row | Title | Value format | Record field |
|---|---|---|---|
| outcome | 1201 | 1213 `Mission Completed` / 1214 `Mission Failed` | mask bit 0 |
| heading | 1202 `Mission Results` | | |
| 1 | 1203 `Run Time` | 1208 `%1!02d!:%2!02d!` | `+0x04`, milliseconds as `mm:ss` |
| 2 | 1204 `Rockets Expended` | 1209 `%1!d!` | **authored, never drawn** (A1) |
| 3 | 1205 `Gun Hit Ratio` | 1210 `%1!d!%%` | `+0x22` over `+0x20` |
| 4 | 1206 `Cash Earned` | 1211 `$%1!d!` | `+0x28` |
| 5 | 1207 `Overall Planes Downed` | 1212 `%1!d!` | no field: the sum of both kill arrays (A2) |

Tabs are 1159 `Best to Date` (the merged half at `+0x54`) and 1160 `Most Recent` (the attempt half
at `+0x00`); 1200 `Current Mission` is the bookmark. 1215 `%1!s! - %2!s!` is the page title (name
and area), 1216 `%1!s! - Scrapbook` the book's own, and 1219 `Not yet flown ` an unflown slot.

**The decode addresses**, all from [`docs/org/debrief.md`](org/debrief.md): the pass is
`FUN_004194e0`, reached from `FUN_00443090`; the record is `UIData +0x1868` indexed `seq + 1`; the
screen push is `FUN_0046fb60(0x0071d57c, …)`; the campaign win flag is `campaign +0xc58` through
`FUN_00463be0` / `FUN_00463c10`; the attempt counter is `[0x0071b494 + idx*0x10]` with
`idx = mission + 10*chapter`; the tally object is `0x0071d2a0`.

**Three screenshots** are the visual ground truth, all under `OriginalScreenshots/`.
`Campaign Mission End screen CM01.png` is the screen a finished mission ends on, mission 1's **first**
spread (rows `1_1_1` to `1_1_3`), with the results block, one `3 Kestrel` stamp and no Current
Mission bookmark. `Campaign Scrapbook CM01 Story Scraps.png` is mission 1's second spread (rows
`1_2_1` to `1_2_7`), with the danger-zone photograph in its corners and no results block.
`Campaign Scrapbook CM02 Mission select after another Mission.png` is mission 2's first spread (rows
`2_1_1` to `2_1_4`), reached from a later mission, so it carries the Current Mission bookmark and
three kill stamps.

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

1. ☑ Decode the page composition: which scrap sits where, at what coordinates, with what zoom text
2. ☑ Decode the two per-airframe tallies and the Overall Planes Downed row
3. ☑ Decode the book's navigation and its entry paths

### Wave B — the record and the director

11. ☑ A lost attempt keeps its objective bits
12. ☑ Carry the mission result across the launcher's deferred hop
13. ☐ Count the per-airframe tallies and merge them per index
14. ☐ The per-mission attempt counter and the four-attempt skip offer

### Wave C — the results page

15. ☐ The results block: four rows, the outcome line and the two tabs
16. ☐ The per-airframe kill stamps
17. ☐ Enter the scrapbook at mission end, and Replay Mission

### Wave D — the book

18. ☐ The story page and its per-mission scrap composition
19. ☐ The per-scrap detail view
20. ☐ Navigation: page and mission arrows, and the Current Mission bookmark
21. ☐ The danger-zone scrap slot

## Dependency and parallelism notes

**A1 has landed**, so D18, D19 and D20 are unblocked and build against `SCRAPBOOK.CSV` and
`LAYOUT.CSV` rather than invented geometry. **A2 has landed**, so B13 and C16 are unblocked: the
counting rule and the stamp enumeration are both written down, and B13 should run before C16 since
the stamps have nothing to draw until something counts. **A3 has landed**, so C17 and D20 are
unblocked: the mission-end entry is fully traced, and the arrows, bookmark and Replay Mission fork
are all written down. Whether Wave D also builds `SCRAPBOOK_TOC.SCRIPT`'s table of contents as its
own screen, given CSVM's `CampaignPreviousMissionsPage` is already shaped like it rather than like
the book, is still an open scope call and blocks nothing.

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

## A1 ☑ Decode the page composition: which scrap sits where, at what coordinates, with what zoom text

**Landed.** The composition is not in the executable and not in the scripts. It is a shipped data
file, `extracted\rof\ASSETS\SCRAPBOOK.CSV`: one `[SCRAPBOOK]` section of 461 rows keyed
`<mission>_<spread>_<item>`, carrying per scrap the art name, the extensions, the position, the
clickable region, the draw order, a visibility gate and the three langui ids its detail view shows.
The file states its own column header. The record is
[`formats/campaign-screens.md`](formats/campaign-screens.md), "The scrapbook"; the code that reads
it, `FUN_004061d0` and the four callers that are between them the whole book, is in
[`org/debrief.md`](org/debrief.md).

**Verified.** Every row resolved against the shipped install: 294 page images, 43 zoom images and
all 26 `SB_BG_*.jpg` zoom backgrounds are present, none missing. Item numbering is contiguous from 1
in all 47 populated spreads, which is what the enumerator requires. All three reference spreads
reproduce exactly, scrap for scrap and in the draw order the rows give:

- **CM01's mission-end screen** is mission 1 spread 1, rows `1_1_1` to `1_1_3`: the coin at
  `49,250` order 100 over the magazine at `135,197` order 90 over the Aloha Daily at `49,70`
  order 60, stacked in the image in exactly that order.
- **CM01's story page** is rows `1_2_1` to `1_2_7`, including both photo-corner mounts and the
  danger-zone capture in the lower one.
- **CM02's results page** is rows `2_1_1` to `2_1_4`, blueprint behind clippings behind medal.

**What it changed elsewhere.** Four readings this plan carried did not survive the table; they are
rows 4 to 7 of the disproven list above, and the milestone goal, the data survey and C15's title
were corrected with them. D18, D19 and D20 are unblocked and now have a source to build from rather
than geometry to invent.

**Original approach (kept for reference).**

> **Goal.** A written record, per mission slot and page, of which scrap assets are drawn, where, at
> what size, and which carry detail text, sufficient to lay out all twenty-five slots without opening
> the original again.
>
> **Evidence (confidence: lead-only).** The assets and their naming are censused above, so *what*
> exists is settled; *where each goes* is not. `docs/formats/campaign-screens.md` is the model for how
> this repo records an authored screen, and it already documents the cabin's `pc_memento` pane reading
> `"assets\graphics\scrapbook\" + <name>` from `uiData` 2150, so the same callback family very likely
> serves the book. The scripts are `extracted/rof/ASSETS/SCRIPTS/SCRAPBOOK.SCRIPT` and
> `SCRAPBOOKZOOM.SCRIPT`. `docs/org/hangar.md` documents the two callback dispatchers those scripts
> call and the id-resolution rule for the `2100`–`2413` range, which is the tool for reading any id
> they use.
>
> **Approach.** Read the two scripts first; they are pure layout, as the hangar page records for its
> own four. Resolve every `uiData` callback id they invoke through the dispatchers in
> `docs/org/hangar.md`. Write the result into `docs/formats/campaign-screens.md` as a scrapbook
> section, matching that page's existing per-screen shape, and keep function-level prose in
> `docs/org/debrief.md` per Decision 3.
>
> **Model recommendation.** high. A reverse-engineering pass over an unread screen family whose output
> every Wave D item is built on, so a wrong reading here is expensive downstream.
>
> **Verify.** The written layout reproduces all three reference screenshots: CM01's results page,
> CM01's story page and CM02's results page, scrap for scrap and position for position.
>
> **⚠ Traps.** The `…S` suffix is a small version, not a separate scrap; four exist, and treating
> every scrap as having one will send you looking for 159 missing files. Slot `00` is the
> not-yet-started career and is not mission 1. Do not assume the results page's mementos are authored
> per mission: the generic `SB_00_00_*` set appears on more than one.

The approach's premise was wrong in a way worth keeping: the scripts are **not** pure layout the way
the hangar's four are. They declare empty slots and ask the engine to fill them item by item, so
reading them settles the protocol and none of the content. The id-resolution rule from
`org/hangar.md` was the tool that worked. Two of that approach's three stated traps were themselves
false (the `…S` suffix and the shared `SB_00_00_*` set); only "slot `00` is not mission 1" held. Its
Verify line was met in full, on all three screenshots.

## A2 ☑ Decode the two per-airframe tallies and the Overall Planes Downed row

**Landed.** Both arrays are per-airframe kill tallies over the same eleven airframes, and what
separates them is the mission roster's own `ace` flag. `FUN_004b9bc0`, the damage resolver, is the
single credit site: on the branch where the victim's health reaches zero it checks that the player
did it, that the victim's side is 2 or more, and that the victim's class is an aircraft, then
increments the plain array through `FUN_004a2320` or the ace array through `FUN_004a2340` according
to the victim's `+0x988`. Anything that is not an aircraft goes to a third counter at tally `+0x2c`
that the debrief never copies, so a mission spent on ground targets reads zero planes downed. The
airframe index is a nodename lookup, `FUN_00426e30` over an eleven-record table at `0x00620c70`.

**Overall Planes Downed is computed, not stored.** `0x0040a8df` sums
`record[+0x08 + i] + record[+0x14 + i]` for `i` 0 to 10 and truncates to sixteen bits; there is no
total field. The stamps are an enumeration rather than a grid: `uiData` 2404 walks the plain array
then the ace array, skips zeros, and answers the nth non-zero slot with a frame index (`i`, or
`i + 11` for an ace) and a count, returning -1 when the tab is exhausted. That is where the art's 22
frames for 11 airframes come from. The record is in
[`org/debrief.md`](org/debrief.md#what-the-tallies-count) and the screen side in
[`formats/campaign-screens.md`](formats/campaign-screens.md), "The kill stamps".

**Verified.**

- **The frame order is the art's own.** `SB_KILLMARKERCOMBINED.PNG` is a strip of 22 frames of
  70x100 reading Hoplite, Hellhound, Balmoral, Bloodhawk, Brigand, Devastator, Firebrand, Fury,
  Kestrel, Peacemaker, Warhawk and then the same eleven starred, which is exactly the order of the
  eleven records in the executable's table.
- **The lookup cannot miss.** The 22 strings the table compares are exactly the 22 distinct
  `nodename` values the shipped `vehicle.zrd` resolves to, so `FUN_00426e30`'s fall-through to slot
  11 (out of bounds in both arrays) cannot fire on any aircraft this install can spawn.
- **CM02 reproduces slot for slot.** Balmoral (`i` 2) and Peacemaker (`i` 9) in the plain array and
  Peacemaker again in the ace array fill `SB_KILL0`, `SB_KILL1` and `SB_KILL2`, whose scattered
  coordinates put them where the screenshot puts them, and sum to the drawn total of 6. The ace is
  `britpeace_7`, the one block in that mission's roster carrying slot 67. CM01 is the one-stamp
  case: `3 Kestrel` in slot 0 over a total of 3.

**What it changed elsewhere.** B13 and C16 are unblocked and both move to traced; C15 gains the
sum rule for its last row. `formats/ai-rosters.md`'s slot-67 row said the read at `0x004ba23a` was
undecoded and now names it, and its CM02 ace case gains a fourth confirming witness. The A2 trap
about the tally object's field boundaries is settled: the arrays are eleven wide, `+0x2c` is the
non-aircraft counter with its own incrementer, and `+0x5c`/`+0x60` are the Gun Hit Ratio pair.

**Original approach (kept for reference).**

> **Goal.** A statement of what each of the record's two twelve-byte arrays counts, what
> distinguishes the starred stamp from the plain one, and what feeds the Overall Planes Downed row,
> each with the address it came from. **Rockets Expended is no longer part of this item**: A1
> established that the original does not draw it. `uiData` 2404 (`0x0040a714`) is the stamp callback
> and 2406 (`0x0040a7d4`) the results-row callback, both unread, and the stamps' shared art declares
> 22 frames for 11 airframes, which is where the starred variant comes from.
>
> **Evidence (confidence: lead-only).** `docs/org/debrief.md`: the arrays are filled from the tally
> object at `0x0071d2a0` (`+0x00 + 4i` through `FUN_004a23a0`, `+0x30 + 4i` through `FUN_004a23c0`,
> `i` in 0 to 10, each dword truncated to a byte). The screen's arithmetic says they are two
> per-airframe kill tallies: CM02 draws `3 Peacemaker`, `2 Balmoral` and a starred `1 Peacemaker`
> over a total of 6. Neither increment site has been traced. Already ruled out: the per-weapon
> shots/hits reading (see the disproven table) and `FUN_00416de0` as a meaningful remap.
>
> **Approach.** Take the xrefs to `0x0071d2a0` that write rather than read (`FUN_0046a490` at two
> sites, `FUN_004b9bc0` at two, `FUN_00464680`, `FUN_00495310`, `FUN_004a2210`) and read what
> indexes them. The Ghidra project is **read-only**: no renames, no comments, no `save_program`.
> Land the answer in `docs/org/debrief.md`, replacing its ⚠ "not decoded" note, and amend `BL-624`.
>
> **Model recommendation.** high. A decode whose wrong answer already cost this plan one withdrawn
> commit.
>
> **Verify.** The decoded rule reproduces CM02's stamps and its total of 6 from the same flight's
> events, and reproduces CM01's single `3 Kestrel` stamp with a total of 3.
>
> **⚠ Traps.** Do not re-reach for the per-weapon reading. The tally object's own field boundaries
> are ambiguous between an eleven-wide array with trailing scalars and a twelve-wide one:
> `FUN_004a22a0`'s reset loop fits both, so settle it from an indexed write site and not from the
> reset. `0x0071d2fc` and `0x0071d300` sit immediately after the arrays and are the Gun Hit Ratio
> pair; do not fold them into the arrays.

⚠ The listed write xrefs to `0x0071d2a0` were a dead end: all but `FUN_004b9bc0`'s two are
`FUN_004a22a0` reset calls with the object as `this`. The tallies are reached through methods, so
the callers of `FUN_004a2320` / `FUN_004a2340` / `FUN_004a2330` are the question, and there is
exactly one.

## A3 ☑ Decode the book's navigation and its entry paths

**Landed.** The two entry paths do not converge on the same screen: a mission ending opens
`SCRAPBOOK.SCRIPT` directly (`FUN_0046fb60(0x0071d57c, …)`, already traced in
[`org/debrief.md`](org/debrief.md#the-pass-in-order)), but the cabin's PREVIOUS MISSIONS button
opens `SCRAPBOOK_TOC.SCRIPT` instead (`LAYOUT.CSV`'s `PC_B_PREVIOUS` row carries
`ScriptToExe = ScrapBook_TOC`), and the book is reached from there only by picking a row or by the
Current Mission jump. A1's arrow-stepping and bookmark rule stand unchanged. Replay Mission forks
on a session flag, `$$SR$$`, that only the mission-end path can leave set: restarting in place on
the mission just flown in the same session, or otherwise falling through to the cabin's own New
Mission call sequence. The trace, the full `script_run`/`pause`/`continue`/`end` inventory across
all three scripts, and what remains unread are in
[`org/debrief.md`](org/debrief.md#entering-the-book-replay-mission-and-the-table-of-contents); the
transitions and the two previously-undecoded callbacks (2408, 2409) are in
[`formats/campaign-screens.md`](formats/campaign-screens.md#entry-exit-and-the-table-of-contents).

**Verified.** Every `script_run`, `script_pause`, `script_continue` and `script_end` in
`SCRAPBOOK.SCRIPT`, `SCRAPBOOKZOOM.SCRIPT` and `SCRAPBOOK_TOC.SCRIPT` is accounted for (nine
transitions, tabulated in both pages above), and both entry paths are stated: mission end into the
book, cabin into the table of contents. `LAYOUT.CSV`'s `ScriptToExe` column and the scripts'
`gui_mailbox` cases were cross-checked against each other rather than read alone, the same method
the format page's "Reader rules" already prescribes for this screen family.

**What it changed elsewhere.** The plan's own trap below is corrected rather than confirmed: the
two paths do not differ by "the tab opened on" (the tab resets to Most Recent on every entry
regardless of path); they differ by which *screen* opens and by Replay Mission's restart-in-place
fork. `docs/org/debrief.md#so-this-item-reuses-a-board-csvm-already-has` is rewritten: CSVM's
`CampaignPreviousMissionsPage`, a flat pick-then-act list, is shaped like `SCRAPBOOK_TOC.SCRIPT`,
not like the two-page book the debrief itself is, which changes what "the same board" means for
Wave C and D. C17 (mission-end entry) is unblocked outright; D20 (bookmark and arrows) was already
direction-sound and gains the entry-path split. Whether Wave D should build the table of contents
as its own screen, matching the original's split, is left to whichever item builds it, per the
plan's front-matter note that no item currently claims it.

**Not decoded.** Which native function answers `PC_B_PREVIOUS`'s layout-declared transition, and
where `$$SR$$` is set before the mission-end path opens the book, are both in `crimson.exe` and
unread: the `ghidra-mcp` bridge did not connect this session. Neither claim above depends on
either address; both were read off `LAYOUT.CSV` and the scripts' own text, cross-checked against
each other.

**Original approach (kept for reference).**

> **Goal.** A statement of how the book is entered from a mission end and from the cabin, what the
> arrows step, when the Current Mission bookmark appears, and what Replay Mission and View All
> Missions do.
>
> **Evidence (confidence: direction-sound; A1 settled the stepping, the entry paths are unread).** A1
> resolved the mechanics of the arrows. Position is two globals, `0x00647b78` the mission and
> `0x00647b7c` the spread. `uiData` 2402 takes 100 for forward and 101 for back, delegating to
> `FUN_00406170` and `FUN_00406100`, which probe item 1 of the neighbouring spread and roll to the
> next mission when the key is absent; 101 returning 0 at the front of the book is what opens the
> table of contents. 2402 with 0, and 2405 mode 1 with -1, both jump to the campaign's current mission
> at spread 1, which is the Current Mission bookmark. 2401 (`0x0040a682`, `FUN_004060a0`) reports
> which of next and bookmark to enable. Replay Mission is offered when `uiData` 2411 finds a time in
> either half of the record.
>
> **There is a third script this plan did not know about.** `SCRAPBOOK_TOC.SCRIPT` is the VIEW ALL
> MISSIONS screen: a 25-row list filled by `uiData` 2409 with a plane icon and three text lines per
> row, its own Replay and Current Mission buttons, and an `ispy` cheat that turns on the debug overlay
> `SCRAPBOOK.SCRIPT` draws in `gui_draw`. It is reached from `SB_B_TOC` and from stepping back off the
> front of the book, and the two scripts pause rather than end each other.
>
> **Approach.** Read `FUN_0046fb60`'s argument at `0x0071d57c` for the entry, and `FUN_004194e0`'s
> tail for what the mission end sets before it. `uiData` 2409 (`0x0040a4b8`) is the remaining unread
> callback.
>
> **Model recommendation.** medium. Bounded reading, with A1's method already established.
>
> **Verify.** The written flow accounts for every `script_run`, `script_pause`, `script_continue` and
> `script_end` in the three scripts, and for both entry paths.
>
> **⚠ Traps.** Entering from the cabin and entering from a mission end differ by at least the tab
> opened on and the presence of Replay Mission; do not assume one path. <TODO: confirm from a
> cabin-entry screenshot whether anything else differs.>

The approach's premise about *what* differs between the two entry paths was wrong: the tab does not
differ (both reset to Most Recent), and Replay Mission's *availability* does not either. What
differs is which screen opens, and Replay Mission's restart-in-place fork on `$$SR$$` once you are
looking at the book. `FUN_0046fb60`'s argument and `FUN_004194e0`'s tail were not re-read (the
mission-end call was already traced in a prior session); `LAYOUT.CSV`'s `ScriptToExe` column, not a
decompile, is what settled the cabin path. Its Verify line was met without the Ghidra reading the
approach assumed it would need.

# Wave B — the record and the director

## B11 ☑ A lost attempt keeps its objective bits

**Landed.** `CampaignDirector.OnMissionEnded` (`CSVM/src/Session/CampaignDirector.cs:648`) now banks
`graph.CompletedMask` with only bit 0 forced clear on a loss, instead of banking zero outright:
`outcome == MissionOutcome.Won ? graph.CompletedMask : graph.CompletedMask &
~CampaignProgression.PrimaryObjectiveMask`. This matches the original, whose two mask-building loops
run regardless of outcome and whose bit 0 comes from the campaign win flag alone
(`docs/org/debrief.md`, "The pass, in order" and "The completed-objective mask has two sources").
`CampaignProgression.Record` needed no change: it already gates its best-of merge and the position
advance on bit 0 alone (`CampaignProgression.cs:145`-`150`), so a non-zero mask on a loss records
statistics into `Latest` and nothing else.

**Verified.** `CSVM/src/Testing/CampaignSuites.cs`'s `campaign-mission-end` suite gained a second,
independent leg (`CampaignMissionLossKeepsObjectiveBits`) on its own fresh world: it drives a
non-primary objective to completion, ends the mission through the player-death path
(`ObjectiveGraph.NotifyPlayerLost`/`EndAfterPlayerLost`) rather than the graph's own win ending, and
asserts the recorded mask keeps the driven objective's bit while bit 0 stays clear and neither
`MissionRecorded.PrimaryCompleted` nor `.Advanced` fires. Taken as a baseline against the pre-fix
code (`outcome == MissionOutcome.Won ? graph.CompletedMask : 0`), this leg fails with a zero mask,
so the pass on the fixed code is not vacuous. `.\RunTests.ps1 -Suite campaign-mission-end -SkipUnits
-SkipGoldens` and the `CampaignProgressionTests` unit suite both pass.

**⚠ Traps, held.** `CampaignProgression`'s advance and its best-of merge are both gated on bit 0
alone (unchanged), so a non-zero mask on a loss neither advances the campaign nor overwrites the
best record. The persist-log commit (`CampaignPersistLog.CommitsOn`) is untouched, a separate
outcome test.

## B12 ☑ Carry the mission result across the launcher's deferred hop

**Landed.** `LauncherContext.ReturnToCabin` widened from `Action<string>` to
`Action<string, CampaignMissionResult>`, threaded through `Launcher._pendingCabin` (now
`(string Profile, CampaignMissionResult Result)?` instead of a bare `string?`) and
`GameSession._returnToCabin` (`Launcher.cs:131,683-687,812-814,1006-1013`;
`GameSession.cs:112,788-797`). Timing is unchanged: `GameSession.OnCampaignMissionEnded` still
calls `_returnToCabin` synchronously inside the session's own `_Process`, `Launcher._Process` still
frees the session and calls `OpenCabin` from `_pendingCabin` one frame later, and `OpenCabin` still
does nothing but `ReturnToMenu()` then `_menu!.OpenCampaignCabin(profile)` — it now also has the
result in hand (logged, for C17 to build on) but does not read the world from it.

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

**Verified.** `dotnet build` is clean and `.\RunTests.ps1 -Suite campaign-mission-end -SkipUnits
-SkipGoldens` passes (that suite drives `CampaignDirector` against a built world directly and does
not cross the `Launcher`/`GameSession` boundary this item widens, so it is unaffected by
construction rather than a positive check of the carry). `PT-88` is the at-the-controls leg of this
item's Verify (a flown CM01 to a win and to a loss, console line for console line at both ends of
the hop) and is owed, not exercised this session.

## B13 ☐ Count the per-airframe tallies and merge them per index

**Goal.** `MissionAttempt` carries two eleven-slot per-airframe kill tallies, plain and ace, and
`CampaignProgression` merges them per index by maximum the way the original does.

**Evidence (confidence: traced).** A2 decoded both arrays and the credit rule, in
[`org/debrief.md`](org/debrief.md#what-the-tallies-count): the player kills a hostile aircraft, its
`vehicle.json` `nodename` gives the airframe index 0 to 10, and the roster's `ace` flag (slot 67)
chooses which of the two arrays is incremented. `MissionAttempt`
(`CSVM/src/Session/CampaignProgression.cs:11`) has scalar `Shots` and `Hits` only, off
`CampaignDirector`'s world view (`CampaignDirector.cs:811`, `ProjectilePool.CannonRoundsFired`). The
record's merge rule for both arrays is per-index maximum
(`docs/formats/saved-games.md`, "The mission-result array").

**Approach.** Credit a kill where CSVM already resolves a destroyed aircraft to its killer; the
index is the airframe, the array is chosen by the roster's ace flag, and a non-aircraft kill is
counted nowhere the screen can see it. Then extend `MissionAttempt`, the merge in
`CampaignProgression`, and the profile serialisation in `CampaignProfileStore`. Overall Planes Downed
is not stored: the screen sums both arrays at draw time, so C15 computes it rather than reading a
field.

**Model recommendation.** medium. Mechanical now A2 has answered, with a serialisation format to
keep compatible.

**Verify.** A flown mission whose kills are known, with the tallies read back off the saved profile:
a mission downing two Balmorals and three Peacemakers plus the mission's ace reproduces CM02's
`2 / 3 / starred 1` and a total of 6.

**⚠ Traps.** The arrays are written on the campaign path only; in game mode 3 the same two record
offsets hold a summed `ushort` and the danger-zone count, so an Instant Action or multiplayer path
must not fill them as arrays. Only hostile aircraft count: the original tests the victim's side is
2 or more, so a downed wingman scores nothing, and ground and shipping kills go to a counter the
screen never reads. Changing the profile record's shape is a save-compatibility change:
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

## C15 ☐ The results block: four rows, the outcome line and the two tabs

**Goal.** The results page shows the outcome line, the four rows and the Best to Date / Most Recent
tabs, reading the flown mission's record.

**Evidence (confidence: traced).** The row and format table is in "What the data actually ships"
above. **Four rows, not five**: A1 established that Rockets Expended is authored in langui and
`LAYOUT.CSV` and drawn by nothing. Run Time, Gun Hit Ratio and Cash Earned map onto fields CSVM
already carries. **Overall Planes Downed maps onto no field at all**: A2 established that the screen
sums the two per-airframe kill arrays over their eleven slots at draw time, so the row is computed
from what B13 counts. The tabs are the record's two halves.
`CampaignPreviousMissionsPage` is the page to grow, and `CampaignBriefingPage` is the nearest
example of a text-heavy composed board.

**Approach.** Follow `docs/org/campaign-board.md`'s authored-space rule: place at the original's
coordinates, which are `LAYOUT.CSV`'s `SB_*` rows, over the page's own background. The stat card is
`SB_STATCARD` at `403,297`; the value column is x 642 and the title column x 417, with the four rows
at y 412, 436, 460 and 484 and the outcome and heading lines at 339 and 369. Use the langui ids as
literal strings the way `IaWrapupBoard` does, since `ui_strings.json` is a build-time extraction
artifact and not one of the archives `SessionArchives.OpenFor` loads.

**Model recommendation.** medium. Board work with an established pattern in the same directory.

**Verify.** A flown CM01 reproduces `Campaign Mission End screen CM01.png` row for row, including
`03:35` from the milliseconds field, `16%` from the shot/hit pair, `$0` cash and 3 planes; CM02
gives the second case at `03:19`, `18%` and 6.

**⚠ Traps.** Do not read `ui_strings.json` at runtime. `Gun Hit Ratio` is a ratio of the `+0x22`
pair member over `+0x20` and not either one alone. Do not restore the Rockets row because langui and
`LAYOUT.CSV` carry it; the original cut it, and `SLINE3` at 388 would collide with the heading. Do
not add an Overall Planes Downed field to the record to make the row easy: the original has none and
a stored total would drift from the stamps it has to agree with. ⚠ The outcome line is the one row
whose source offset differs between the two tabs (`0x0040a7e6`, `+0x00` against `+0x24` inside the
half); every other row and both kill arrays sit at the same offset in whichever half is selected.

## C16 ☐ The per-airframe kill stamps

**Goal.** The results page draws one stamp per airframe with a kill count, and the starred variant
where the original draws it.

**Evidence (confidence: traced).** The art is `extracted/rof/ASSETS/GRAPHICS/SB_KILLMARKERCOMBINED.PNG`,
a vertical strip of 22 frames of 70x100: frames 0 to 10 are the eleven airframes in the engine's
order (Hoplite, Hellhound, Balmoral, Bloodhawk, Brigand, Devastator, Firebrand, Fury, Kestrel,
Peacemaker, Warhawk) with the name drawn into the stamp, frames 11 to 21 the same eleven over a
star. The eleven slots are `SB_KILL0` to `SB_KILL10` in `LAYOUT.CSV` with `SB_KILLTEXT0` to
`SB_KILLTEXT10` for the counts, and the enumeration rule is A2's, in
[`org/debrief.md`](org/debrief.md#the-stamps-and-the-total).

**Approach.** Draw the strip frame the enumeration names into the slot the ordinal names. Fill slots
densely from `SB_KILL0`, walking the plain tally's airframes in ascending index order and then the
ace tally's, skipping zeros and stopping at eleven. The count is langui 520 `IDS_KILLCOUNT` in the
matching `SB_KILLTEXT` slot.

**Model recommendation.** medium.

**Verify.** A flight downing a known mix of airframes reproduces the stamp set and the total. CM02
is the shipped case: slot 0 `2 Balmoral`, slot 1 `3 Peacemaker`, slot 2 a starred `1 Peacemaker`,
over 6.

**⚠ Traps.** The same airframe can appear twice, once plain and once starred; a stamp layout keyed
uniquely on airframe will drop one of CM02's three. **The slot positions are not in reading order**:
`SB_KILL1` at `467,93` is left of and above `SB_KILL0` at `560,109`, so a layout that assigns slots
by where they look on the screenshot will place CM02's first two the wrong way round.

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

**The mission end opens on spread 1**, confirmed by `Campaign Mission End screen CM01.png`: mission
1's first spread with the results block, Replay Mission, and **no** Current Mission bookmark, which
is the bookmark behaving as `uiData` 2401 says it should when the open mission is the current one.
The entry is therefore the same position the bookmark jumps to, and A3 traced the call that performs
it, `FUN_0046fb60(0x0071d57c, …)` off the end of `FUN_004194e0`
([`org/debrief.md`](org/debrief.md#the-pass-in-order)). The tab is Most Recent: it resets to that on
every entry regardless of path, mission end included
([`org/debrief.md`](org/debrief.md#entering-the-book-replay-mission-and-the-table-of-contents)).
Replay Mission on this page restarts the just-flown mission in place, on the `$$SR$$` fork A3 found;
the cabin never sets that flag, so `CSVM`'s Replay Mission need not distinguish the two once this
page's own entry is the only caller that can leave it set.

# Wave D — the book

## D18 ☐ The story page and its per-mission scrap composition

**Goal.** Every mission slot's second page draws its shipped scraps at the original's positions.

**Evidence (confidence: traced, geometry from A1).** `SCRAPBOOK.CSV` is the composition, one row per
scrap with its position, region and draw order. 16 missions have a second spread carrying 5 to 24
items, and 6 of those have a third carrying 12 to 24. `Campaign Scrapbook CM01 Story Scraps.png` is
the worked example and is mission 1's rows `1_2_1` to `1_2_7`.

**Approach.** Parse `SCRAPBOOK.CSV` into the composition model and drive every spread from it,
results pages included, since spread 1 draws scraps too. Honour `DrawOrder`, the `Objective` gate
and the `Snap_` capture rule. Enumerate items upward from 1 and stop at the first absent key, the
way the original does, so the spread count stays data-driven.

**Model recommendation.** medium.

**Verify.** All 47 populated spreads render without a missing-asset error, and mission 1's second
spread matches its screenshot scrap for scrap.

**⚠ Traps.** No art is authored, and a slot with nothing shipped is not a bug. A mission has one,
two or three spreads, so nothing may assume two. The `<page>` field in an art filename is not the
spread the scrap appears on.

## D19 ☐ The per-scrap detail view

**Goal.** Clicking a scrap opens it in detail, with the text the small version does not carry.

**Evidence (confidence: traced).** A1: the `ImageType` field's second letter says whether a scrap
opens at all and under which extension, and `Zoom`, `ZoomX`, `ZoomY`, `ResourceID`, `TitleResID` and
`TextResID` carry the rest. 210 of the 461 rows open (43 to a separate zoom image, 167 captures);
the other 245 are `P0` and do not. The `Zoom` letter picks one of 26 text-layout families,
`SBZ_T_TITLE<letter>` and its caption and text siblings in `LAYOUT.CSV`, and the background is
`SB_BG_<letter>.jpg`.

**Approach.** Build the detail view from those six columns plus the chosen family's boxes. The
background is the family's, the inset image is the scrap's own second extension placed at
`ZoomX`/`ZoomY`, and the three langui ids fill title, caption and body.

**Model recommendation.** medium.

**Verify.** `1_1_3` (`SB_01_02_mag2`, family `M`) opens with `IDS_SB_01_01_mag2_t` as its title and
`IDS_SB_01_01_mag2_b` as its body; a `P0` row such as `1_2_4` does not open at all.

**⚠ Traps.** Do not assume every scrap opens: over half do not, and the tell is the `ImageType`
second letter, not the presence of a second file. Two `LAYOUT.CSV` colour fields are typo'd
(`xff000000` in family `A`'s caption, `oxff1E283C` throughout family `J`) and will not parse.

## D20 ☐ Navigation: page and mission arrows, and the Current Mission bookmark

**Goal.** The arrows step page then mission in both directions, and a mission other than the current
one shows the bookmark that jumps back to it.

**Evidence (confidence: direction-sound).** The bookmark is langui 1200 and is visible at the top
right of `Campaign Scrapbook CM02 Mission select after another Mission.png`. A1 settled the stepping
rule: forward and back probe the neighbouring spread and roll to the next or previous mission, and
the bookmark jumps to the campaign's current mission at spread 1. A3 settled the back arrow's fall
into the table of contents at the front of the book (`SB_B_PREV` returning 0 from `uiData` 2402
mode 101) and traced the table of contents itself, `SCRAPBOOK_TOC.SCRIPT`
([`org/debrief.md`](org/debrief.md#entering-the-book-replay-mission-and-the-table-of-contents)),
including its own View/Current Mission jump back into the book. Whether this item also builds the
table of contents as its own screen, given CSVM's `CampaignPreviousMissionsPage` is already shaped
like it rather than like the book, is an open scope call A3 raised but did not settle.

**Approach.** Step by probing `SCRAPBOOK.CSV` for the neighbouring spread's item 1, exactly as
`FUN_00406170` does, rather than storing a per-mission page count.

**Model recommendation.** medium.

**Verify.** Walk the whole book from slot 00 to slot 24 and back, and confirm the bookmark appears
exactly when the shown mission is not the campaign's current one.

**⚠ Traps.** Unflown missions are still pages (langui 1219 `Not yet flown `), so navigation is over
all twenty-five slots and not only the completed ones.

## D21 ☐ The danger-zone scrap slot

**Goal.** A flown danger zone leaves a photograph on that mission's story page, mounted in the
original's photo corners.

**Evidence (confidence: traced).** A1: 167 rows name a `Snap_<mission>_<objective>` capture, each
gated on that objective and paired with a `DZ_generic_corners` row at identical coordinates one
step higher in draw order. A capture is any name beginning `Snap_`, tested by `FUN_00406db0`; it
resolves against the profile directory rather than `assets\graphics\`, is **skipped when the file is
absent**, is forced to a 164×123 region and is drawn at 25% on the page and full size in the zoom.
The danger-zone objective ids run 18 to 31. `SB_01_00_DZ.PNG` is not the slot and is referenced by
nothing, and captures appear on results pages too, not only story pages (`10_1_5`).
`BL-256` is the capture half, recorded as intent with no design, and it names
`snd_dangerzone_camera` as the shutter sting.
<TODO: re-verify `BL-256` still-open against `git log --grep=BL-256` and the code.>

**Approach.** First question, and it is a scope call rather than a technical one: whether the
capture itself lands here or stays `BL-256`. This item's floor is the slot and the mount reading a
capture that already exists; its ceiling is the capture too.

**Model recommendation.** medium.

**Verify.** Fly a danger zone and find its photograph on the mission's story page.

**⚠ Traps.** `DzRadius` is 15 m and hand-tuned, and `BL-256` records it as the marker-centre radius
whose screenshot-trigger role is intent and not decode. Do not treat the trigger as settled.
