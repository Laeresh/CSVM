# The mission debrief, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-29, settling the decode half of `BL-622`.
Every claim below names the function or address it came from. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source. The screen's own layout and rows
are additionally read off three screenshots (`OriginalScreenshots/Campaign Mission End screen
CM01.png`, `Campaign Scrapbook CM01 Story Scraps.png`, `Campaign Scrapbook CM02 Mission select after
another Mission.png`), the extracted langui table (`extracted/rof/ui_strings.json`), a census of
`extracted/rof/ASSETS/GRAPHICS/SCRAPBOOK/`, and the navigation as reported at the controls; the
lines that came from those say so.

**Where the other halves live.** The record this pass writes is the mission-result array documented
in [`formats/saved-games.md`](../formats/saved-games.md), "The mission-result array"; that page
holds the record's layout, its two halves and the best-of merge rule. The objective data the mask
is built from is [`formats/objectives.md`](../formats/objectives.md). The campaign position the
record is indexed by is [`formats/campaign-sequence.md`](../formats/campaign-sequence.md).

## Contents

- [The pass, in order](#the-pass-in-order)
- [The completed-objective mask has two sources](#the-completed-objective-mask-has-two-sources)
- [The four-attempt skip offer](#the-four-attempt-skip-offer)
- [Time and the shooting statistics](#time-and-the-shooting-statistics)
- [The screen is the scrapbook](#the-screen-is-the-scrapbook)
  - [The stamps are a per-airframe kill tally](#the-stamps-are-a-per-airframe-kill-tally)
  - [Navigation: two pages per mission, twenty-five mission slots](#navigation-two-pages-per-mission-twenty-five-mission-slots)
  - [So this item reuses a board CSVM already has](#so-this-item-reuses-a-board-csvm-already-has)
- [What the debrief does not write](#what-the-debrief-does-not-write)
- [Where CSVM stands](#where-csvm-stands)
- [What is not decoded](#what-is-not-decoded)

## The pass, in order

`FUN_004194e0` is the debrief. It is reached from the mission-end path `FUN_00443090`, and once
from itself (see the skip offer below). It runs to completion before the screen it ends on is
shown, so everything the screen displays is already in the record by then.

1. **It picks the record.** `ESI = 0x0064cc50 + 168 * DAT_0071c098`. `0x0064cc50` is one stride
   past the mission-result array's base, so the record index is `DAT_0071c098 + 1`, which is the
   `seq + 1` indexing the format page states from the reader side. `DAT_0071c098` holds the
   `cm_sequence` position of the mission just flown.
2. **It stamps the profile's screen id.** `UIData +0x08` (`0x0064b348`) is set to `2`.
3. **It clears the attempt half and only the attempt half.** A `REP STOSD` of 21 dwords at
   `0x00419511` zeroes `0x54` bytes from the record's base. The merged best half at `+0x54` is
   never touched here.
4. **It reads the campaign win flag.** `FUN_00463be0` with `ECX = 0x0071b480` returns
   `campaign +0xc58`. A won mission sets bit 0 of the attempt's mask (`OR dword ptr [ESI],0x1` at
   `0x00419521`), which is the primary-objective bit. A lost one takes the skip-offer path instead.
5. **It sets the objective bits**, from two independent sources, both scanned regardless of whether
   the mission was won.
6. **It fills the time and the shooting statistics** through `FUN_00419630`.
7. **It shows the screen**, with `FUN_005a9ea0` and then
   `FUN_0046fb60(0x0071d57c, 0, 1, 0, [0x006272b8], 0, 0, 0)`.

## The completed-objective mask has two sources

Bit 0 is the primary objective and comes from the campaign win flag alone. The rest of the mask is
built by two loops that read two different structures, and neither loop is gated on the outcome.

**The objective text list, at `DAT_0071d92c`.** `FUN_004acc20` builds it from the mission's
`objectives` archive, one `0x1c`-byte record per `OBJECTIVE_%d` entry: a description pointer at
`+0x04` (`FUN_004ad1b0` serves it), a completed byte at `+0x10`, and the objective's **number** at
`+0x14`. A priority word of `1`, `2` or `3` is parsed from the entry's `primary` / `secondary` /
`tertiary` keyword. The list is cleared at mission start by `FUN_004ad160` and an objective is
marked done by `FUN_004ad240`, which finds the record by number. The debrief walks it through
`FUN_004ad180` (the count), `FUN_004ad1e0` (the completed byte) and `FUN_004ad200` (the number),
and sets bit `number` for every completed entry whose number is greater than zero.

**The objectives module's own vector, at `0x0064fb60`.** Records are `0x50` bytes with the
objective id at `+0x4c`, the completed byte at `+0x40`, and the condition sub-vector at `+0x34` /
`+0x38` (`FUN_00445ca0` resets all three per mission). The debrief looks up ids **18 through 30**
by `FUN_00445d50` and sets bit `id` for each one that is complete.

So bits 1 to 17 come from the objectives that carry a debrief text line, and bits 18 to 30 from
module objectives that have none.

## The four-attempt skip offer

On a lost mission the debrief offers to skip it, and the offer is on a counter, not on a single
failure.

The per-mission index is `DAT_0071c0a0 + 10 * DAT_0071c09c`, the mission number plus ten times the
chapter. If `[0x0071b488 + index*0x10]` is zero, meaning this mission has never been completed,
then the attempt counter at `[0x0071b494 + index*0x10]` is incremented, and **every fourth
increment** opens the offer. The modulo is the `AND 0x80000003` sign-fixup idiom at `0x00419553`,
so the test is `counter % 4 == 0` on a counter that is never reset while the mission stands
uncompleted.

The offer is a `dialog.zrd` `MESSAGEBOX` (`FUN_004af930`) carrying langui string id **191**
(`0xbf`, fetched through `FUN_0059ce40`), formatted with a runtime string buffer at `0x0064e848`
and the literal `4`. A return of `6`, the Yes answer, calls `FUN_00463c10(campaign, 1)` to **set
the win flag** and then re-enters `FUN_004194e0`.

The skip is therefore implemented as a synthetic win. The second pass takes the won branch, sets
bit 0, and reads the same objective state, which is also why taking the offer makes the world-state
save gate in `FUN_0046b450` pass where the failed attempt itself wrote nothing.

## Time and the shooting statistics

`FUN_00419630` fills the numeric half of the record.

**Time is milliseconds.** `FUN_0046c590` with `ECX = 0x0071b468` returns the mission clock as a
float, `FMUL [0x00603464]` multiplies it by the `1000.0f` stored there, and `ftol` writes the
result to record `+0x04`.

**The two twelve-byte arrays come from an eleven-slot pair of tallies.** The mission tally object is
at `0x0071d2a0`, and the debrief reads two arrays off it: `+0x00 + 4i` through `FUN_004a23a0` and
`+0x30 + 4i` through `FUN_004a23c0`, for `i` in 0 to 10. Each dword is truncated to a byte into the
record, the first array into `+0x08` and the second into `+0x14`.

The array slot is `FUN_00416de0(i)`, a linear lookup over the pair table at `0x0061f670` (stride 8:
key, then value) that falls through to `11` when the key is absent. **The shipped table is the
identity map**, eleven pairs with key equal to value for 0 to 10, so the slot is `i` today and the
twelfth slot is never written from this path. The function is a general-purpose lookup used
elsewhere in the executable, so it says nothing about what the arrays index.

⚠ **What the two arrays count is not decoded.** Eleven is also the number of airframes, and the
screen shows a per-airframe kill stamp beside its total (see below), which makes a per-airframe
tally the obvious candidate for one of them. That is a candidate, not a reading: neither array's
increment site has been traced. `BL-624` carries the question.

**The shot and hit totals** at record `+0x20` and `+0x22` are `[0x0071d2fc]` and `[0x0071d300]`,
read as words out of the same object and stored as a pair. `0x0071d300` is incremented in three
damage-resolution paths (`FUN_004b9bc0`, `FUN_004bab50`, `FUN_004c0880`), each gated on
`[target + 0x210] & 0x40`; `0x0071d2fc` is incremented in a per-gun loop in `FUN_004b6820`. The
screen divides the pair and shows it as **Gun Hit Ratio**, which is the same reading the format
page reached from the merge rule alone.

**Mode 3 reuses the same two offsets as scalars.** When `DAT_0071bb80` is `3`, neither array is
written: `+0x08` becomes a single `ushort` holding the sum of all eleven entries of both arrays, and
`+0x14` becomes `[0x0071d328]`, a further counter on the same object which the danger-zone module
`FUN_00446990` increments. The arrays are therefore a campaign-only shape.

## The screen is the scrapbook

`FUN_0046fb60(0x0071d57c, …)` opens the **scrapbook**, not a screen of its own. The evidence is the
screen's own strings: every label on it is an `IDS_SB_*` langui row, and the Instant Action wrap-up
uses a separate `IDS_IAWU_*` set. `extracted/rof/ASSETS/SCRIPTS/` has a `SCRAPBOOK.SCRIPT` and no
debrief script. Measured off `OriginalScreenshots/Campaign Mission End screen CM01.png`, which is
the screen as the original draws it at the end of CM01.

It is a two-page book spread. The left page carries the mission title through langui 1215
(`%1!s! - %2!s!`, name and area) over the mission's mementos, and **each memento is an interactive
element with a display of its own**: the newspaper clippings, the magazine and the coin are
individually selectable and open their own view rather than being one painted collage. That is
`SCRAPBOOKZOOM.SCRIPT`, the second of the two scrapbook scripts. The right page carries a
per-airframe kill stamp (a count over the airframe's name), the two tabs, and the results block:

| Row | Title | Value format | Record field |
|---|---|---|---|
| outcome | 1201 `Mission Completed` | 1213 `Mission Completed` / 1214 `Mission Failed` | mask bit 0 |
| heading | 1202 `Mission Results` | | |
| 1 | 1203 `Run Time` | 1208 `%1!02d!:%2!02d!` | `+0x04`, milliseconds rendered as `mm:ss` |
| 2 | 1204 `Rockets Expended` | 1209 `%1!d!` | not located |
| 3 | 1205 `Gun Hit Ratio` | 1210 `%1!d!%%` | `+0x22` over `+0x20` |
| 4 | 1206 `Cash Earned` | 1211 `$%1!d!` | `+0x28` |
| 5 | 1207 `Overall Planes Downed` | 1212 `%1!d!` | not located |

**The two tabs are the record's two halves.** Langui 1159 `Best to Date` selects the merged half at
`+0x54` and 1160 `Most Recent` the attempt half at `+0x00`, which is the same two-halves layout the
format page reaches from the merge rules. Langui 1200 `Current Mission` is the third tab the
scrapbook carries when it is opened from the cabin rather than from a mission end, and 1217 to 1219
(`Starting My Career`, `Above the clouds`, `Not yet flown `) are what an unflown mission's page
shows.

The buttons are Replay Mission, View All Missions and Return to Cabin, with page arrows on both
outer edges. Replay Mission sits inside the results block and is on the results page only.

### The stamps are a per-airframe kill tally

Read off `OriginalScreenshots/Campaign Scrapbook CM02 Mission select after another Mission.png`: the
right page carries three stamps, `3 Peacemaker`, `2 Balmoral` and a starred `1 Peacemaker`, over an
Overall Planes Downed of **6**. The stamps sum to the total, and the same airframe appears twice
with the second occurrence starred, so the page draws two per-airframe tallies and totals both.

That is the shape of the record's two twelve-byte arrays at `+0x08` and `+0x14`, indexed 0 to 10
over the eleven airframes, and it is the best available account of them. It is still an account
from the screen's arithmetic and not a decode: neither array's increment site has been traced, and
what distinguishes the starred tally from the plain one (an ace, a named pilot, a kill by a
particular means) is unread. `BL-624` carries it.

### Navigation: two pages per mission, twenty-five mission slots

Reported at the controls and corroborated by the shipped art. Each mission has a **results page**
(the mission's title and mementos on the left, the stamps, tabs and results block on the right) and
a **story page** of scraps filling both leaves. The right arrow steps to the next page, and from a
mission's story page to the next mission; the left arrow steps back. Opening a mission other than
the current one raises a **Current Mission** bookmark (langui 1200) at the top of the right page,
which jumps back to it.

Every scrap is clickable and opens in detail, sometimes with text the small version does not show.
`SCRAPBOOKZOOM.SCRIPT` is that view.

The art is `assets\graphics\scrapbook\`, named `<kind>_<mission>_<page>_<name>`, 213 non-TIF files:

- **`SB_`** (163) the scraps themselves. Missions run `00` to `24`, which is the 24 campaign
  missions plus slot `00` for the not-yet-started career (langui 1217/1218). Page `01` is the story
  page and carries almost everything, from 2 scraps (missions 10, 15) to 13 (mission 07). The
  `SB_00_00_*` set (`NEWS1`, `NEWS2`, `MAG1`, `DOC1`, `DOC2`, `FHLOGO`) are the generic scraps the
  results pages reuse, which is why the Aloha Daily masthead appears on both CM01's and CM02's.
- **`NT_`** (8) blueprint scraps, one per aircraft or system the story introduces
  (`NT_02_01_BPBALMORAL`, `NT_07_01_BPNITRO`, `NT_19_01_BPTORPEDO`).
- **`MS_P_`** (41) the cabin memento photographs, not scrapbook pages; `UIData +0x344` names the
  current one.
- A `…S` suffix is the small version of a scrap that has a distinct zoom asset. Only four exist, so
  most scraps are one bitmap shown at two sizes.

**The danger-zone scrap is the player's own screenshot.** `SB_01_00_DZ.PNG` is a per-mission slot
and `DZ_GENERIC_CORNERS.PNG` is the photo-corner mount drawn over it; the corners being generic and
the content being per-mission is what makes this a captured image rather than authored art. CM01's
story page shows exactly that, a dark photograph in mounted corners. `BL-256` is the capture half.

**Which scrap sits where is not decoded.** The per-page composition (which assets, at which
coordinates, with which zoom text) is authored somewhere this page has not read.

### So this item reuses a board CSVM already has

`CampaignPreviousMissionsPage` is the scrapbook's finished-missions list. The debrief is that same
screen opened on the mission just flown, so the work is the rest of the scrapbook rather than a new
board: the second page, the per-scrap zoom, the navigation and bookmark, the results block, the
mission-end entry path and the skip offer.

## What the debrief does not write

Money, the airframe id and the plane name (`+0x28`, `+0x2c`, `+0x30`) and the whole merged best
half at `+0x54` belong to `FUN_00405ce0`, the completion recorder. The debrief is a reader of that
merge, not its author, and the two run in the same mission-end pass.

## Where CSVM stands

- **A lost attempt keeps its objective bits in the original and loses them in CSVM.** Both loops
  above run whatever the outcome, so a failed attempt records exactly which objectives were met
  with only bit 0 clear. `CampaignDirector.OnMissionEnded` writes
  `outcome == MissionOutcome.Won ? graph.CompletedMask : 0`, which discards them. A debrief drawn
  from today's mask would report every objective failed on a loss.
- **The two twelve-byte arrays have no CSVM counterpart.** `MissionAttempt` carries the record's
  `+0x00`, `+0x04`, `+0x20`, `+0x22`, `+0x28`, `+0x2c` and `+0x30` fields and nothing at `+0x08` or
  `+0x14`. Whatever they count is tracked nowhere, and neither is the Rockets Expended row nor the
  Overall Planes Downed total (`BL-624`).
- **The board exists.** `CampaignPreviousMissionsPage` is already the scrapbook; what is missing is
  the mission-end entry into it, the Most Recent tab and the Replay Mission button.
- **The skip offer is unimplemented.** Four failed attempts at an uncompleted mission is a
  campaign-advancing decision the player is given, not a cosmetic prompt.
- **`ObjectiveGraph.CompletedMask` is one of the original's two mask sources.** It is the
  objective-number half; the id 18 to 30 half comes from the module vector.

## What is not decoded

- **The wording of string 191.** `extracted/rof/ui_strings.json` jumps from id 136 to id 200, so
  the skip offer's text is absent from the extraction. Its shape is known (a Yes/No message box
  taking a string and the number 4 as its two format arguments) and its wording is not.
- **The `0x0071b480` overlap.** The campaign object's base and the `PilotStatus` save section's
  destination are the same address, and the attempt counters this page reads sit inside the
  1448-byte range that section covers, yet the only available profile's `PilotStatus` is all zeros
  after twenty completed missions. Either a second global shares the address or the counters are
  cleared before the section is written. Nothing here depends on the answer.
