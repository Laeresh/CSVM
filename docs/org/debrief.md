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
  - [What the tallies count](#what-the-tallies-count)
- [The screen is the scrapbook](#the-screen-is-the-scrapbook)
  - [The stamps and the total](#the-stamps-and-the-total)
  - [Navigation and page composition: a data file, not code](#navigation-and-page-composition-a-data-file-not-code)
  - [Entering the book, Replay Mission and the table of contents](#entering-the-book-replay-mission-and-the-table-of-contents)
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

**The two twelve-byte arrays are per-airframe kill tallies, one for ordinary kills and one for
aces.** The mission tally object is at `0x0071d2a0`, and the debrief reads two arrays off it:
`+0x00 + 4i` through `FUN_004a23a0` and `+0x30 + 4i` through `FUN_004a23c0`, for `i` in 0 to 10.
Each dword is truncated to a byte into the record, the first array into `+0x08` and the second into
`+0x14`. What fills them is [below](#what-the-tallies-count).

The array slot is `FUN_00416de0(i)`, a linear lookup over the pair table at `0x0061f670` (stride 8:
key, then value) that falls through to `11` when the key is absent. **The shipped table is the
identity map**, eleven pairs with key equal to value for 0 to 10, so the slot is `i` today and the
twelfth slot is never written from this path. The function is a general-purpose lookup used
elsewhere in the executable, so it says nothing about what the arrays index.

**The shot and hit totals** at record `+0x20` and `+0x22` are `[0x0071d2fc]` and `[0x0071d300]`,
read as words out of the same object and stored as a pair. `0x0071d300` is incremented in three
damage-resolution paths (`FUN_004b9bc0`, `FUN_004bab50`, `FUN_004c0880`), each gated on
`[target + 0x210] & 0x40`; `0x0071d2fc` is incremented in a per-gun loop in `FUN_004b6820`. The
screen divides the pair and shows it as **Gun Hit Ratio**, which is the same reading the format
page reached from the merge rule alone.

**Mode 3 reuses the same two offsets as scalars.** When `DAT_0071bb80` is `3`, neither array is
written: `+0x08` becomes a single `ushort` holding the sum of all eleven entries of both arrays, and
`+0x14` becomes `[0x0071d328]`, a further counter on the same object which the danger-zone module
`FUN_00446990` increments. The arrays are therefore a campaign-only shape. That sum is the same
quantity the campaign screen computes at draw time out of the two arrays, so Instant Action's
wrap-up and the scrapbook's Overall Planes Downed are the same number reached two ways.

### What the tallies count

**One function credits every kill, `FUN_004b9bc0`**, the damage resolver, on the branch it takes
when the victim's health at `+0x2d0` has reached zero. It is the only caller of all three
incrementers, so there is no second path to account for.

A kill is credited only when all of these hold:

- **The player did it.** Either the shooter object is the player (`param_5 == [0x0071c298]`) or,
  when it is not, the call's originator argument is (`param_6`), which is how a player-launched
  weapon that outlives its launcher still scores.
- **The victim is hostile.** `[victim + 8]`, the side, must be 2 or more; the player's own side and
  side 1 fall out. Wingmen and neutrals therefore never reach a tally.
- **It is not the player's own death**, and not a remote kill in a networked session
  (`FUN_00440ad0`).

The victim's class at `[victim + 0x67c]` then forks the count three ways:

| Class | Incrementer | Where it lands |
|---|---|---|
| 0 or 4 (an aircraft), ace flag clear | `FUN_004a2320(i)` | tally `+0x00 + 4i`, the record's `+0x08` array |
| 0 or 4 (an aircraft), ace flag set | `FUN_004a2340(i)` | tally `+0x30 + 4i`, the record's `+0x14` array |
| anything else | `FUN_004a2330()` | tally `+0x2c`, a single counter |

**The third counter is never shown.** `FUN_00419630` copies only `i` in 0 to 10 out of each array,
so `+0x2c` reaches neither the record nor the screen: a mission spent destroying ground targets and
shipping reads zero planes downed. It is reset with everything else by `FUN_004a22a0`.

**The airframe index `i` is a nodename lookup.** `FUN_00426e30` takes the victim's model root node
name (`[[victim + 0x64] + 4]`, `vehicle.json`'s `nodename`) and walks an eleven-record table at
`0x00620c70`, 7 dwords each, comparing the record's second and sixth strings, which are the
airframe's player model node and its AI model node. The eleven records in order:

| i | Airframe | player node | AI node |
|---|---|---|---|
| 0 | Hoplite (`Autogyro`) | `player_autogyro` | `autogyro` |
| 1 | Hellhound | `player_avenger` | `avenger` |
| 2 | Balmoral | `player_balmoral` | `balmoral` |
| 3 | Bloodhawk | `player_bhawk` | `bloodhawk` |
| 4 | Brigand | `player_brigand` | `brigand` |
| 5 | Devastator | `player_pfighter` | `piratefighter` |
| 6 | Firebrand | `player_fbrand` | `firebrand` |
| 7 | Fury | `player_fury` | `fury` |
| 8 | Kestrel | `player_kestrel` | `kestrel` |
| 9 | Peacemaker | `player_peacemaker` | `peacemaker` |
| 10 | Warhawk | `player_warhawk` | `warhawk` |

Those 22 names are exactly the 22 distinct `nodename` values the shipped `vehicle.zrd` resolves to,
so the function's fall-through to `11` cannot fire on any aircraft this install can spawn. That
matters because slot 11 is out of bounds in both arrays: it would land on the non-aircraft counter at
`+0x2c` for the first and on the gun-shots word at `+0x5c` for the second.

**The ace flag is the mission roster's**, slot 67, the one 26 of the 414 shipped blocks author. The
block reader stores it at the block's `+0xa4`, the spawn path `FUN_0047c210` copies it to the AI
entity's `+0x988` (`0x0047ca42`–`0x0047ca4b`), and `0x004ba23a` is where the debrief reads it. See
[`formats/ai-rosters.md`](../formats/ai-rosters.md#field-table); the other read of the same flag,
at `0x0047cde2` on the skill path, is still undecoded.

**The tally object's field boundaries.** The arrays are eleven wide, not twelve: `+0x2c` has its own
incrementer and its own meaning, and `+0x5c` and `+0x60` are the gun shot and hit words. Reading the
reset in `FUN_004a22a0` alone cannot tell those apart from a twelfth slot, because it zeroes an
eleven-iteration stride and then names `+0x2c`, `+0x5c` and `+0x60` one by one; the increment sites
are what settle it. Three linked lists live at `+0x64`, `+0x70` and `+0x7c`, appended through
`FUN_004a2350`, and the debrief reads none of them.


## The screen is the scrapbook

`FUN_0046fb60(0x0071d57c, …)` opens the **scrapbook**, not a screen of its own. The evidence is the
screen's own strings: every label on it is an `IDS_SB_*` langui row, and the Instant Action wrap-up
uses a separate `IDS_IAWU_*` set. `extracted/rof/ASSETS/SCRIPTS/` has a `SCRAPBOOK.SCRIPT` and no
debrief script. Measured off `OriginalScreenshots/Campaign Mission End screen CM01.png`, which is
the screen as the original draws it at the end of CM01, and corroborated by
`Campaign Scrapbook CM02 Mission select after another Mission.png`.

**The mission end opens the mission's first spread**, the one carrying the results block. CM01's end
screen is mission 1 spread 1 (rows `1_1_1` to `1_1_3` of the composition table), and it shows no
Current Mission bookmark because the open mission is the campaign's current one. CM02's is that same
first spread for mission 2, reached later, so the bookmark is up.
`Campaign Scrapbook CM01 Story Scraps.png` is the second spread of the same mission, rows `1_2_1` to
`1_2_7`, and has no results block at all.

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
| 2 | 1204 `Rockets Expended` | 1209 `%1!d!` | **not drawn**, below |
| 3 | 1205 `Gun Hit Ratio` | 1210 `%1!d!%%` | `+0x22` over `+0x20` |
| 4 | 1206 `Cash Earned` | 1211 `$%1!d!` | `+0x28` |
| 5 | 1207 `Overall Planes Downed` | 1212 `%1!d!` | no field: the sum of both arrays, below |

**Rockets Expended is authored and never drawn.** `uiData` 2406 returns five strings, the outcome
line and four values, and `SCRAPBOOK.SCRIPT` binds exactly those five. `SB_T_ROCKETSTITLE` and
`SB_T_ROCKETS` sit unused in `LAYOUT.CSV`, and their y macro `SLINE3` is stranded at 388 between
`SLINE1` at 369 and `SLINE2` at 412 while the four drawn values run 412, 436, 460, 484 at an even
24 apart. The row was cut and the rest re-spaced over it, which is why both results-page screenshots
show four. This is the same shape as the Instant Action wrap-up's own dead row, recorded in
[`formats/instant-action/wrap-up.md`](../formats/instant-action/wrap-up.md).

**The two tabs are the record's two halves.** Langui 1159 `Best to Date` selects the merged half at
`+0x54` and 1160 `Most Recent` the attempt half at `+0x00`, which is the same two-halves layout the
format page reaches from the merge rules. ⚠ The outcome line is the one row that does not read the
same offset in both: `0x0040a7e6` takes the mask from the half's `+0x00` for one tab and from its
`+0x24` for the other. Every other row and both kill arrays are read at the same offset in whichever
half the tab picked. Langui 1200 `Current Mission` is the third tab the
scrapbook carries when it is opened from the cabin rather than from a mission end, and 1217 to 1219
(`Starting My Career`, `Above the clouds`, `Not yet flown `) are what an unflown mission's page
shows.

The buttons are Replay Mission, View All Missions and Return to Cabin, with page arrows on both
outer edges. Replay Mission sits inside the results block and is on the results page only.

### The stamps and the total

Both come out of the record half the open tab selects. `uiData` 2406 and 2404 index the record the
same way: `0x0064cba8 + (tab + mission * 2) * 84`, so tab 0 lands on the attempt half at `+0x00` and
tab 1 on the merged half at `+0x54`.

**Overall Planes Downed is computed, not stored.** `0x0040a8df` runs `i` from 0 to 10 and sums
`record[+0x08 + i] + record[+0x14 + i]`, truncating the result to sixteen bits before formatting it
through langui 1212. There is no total field anywhere in the record, and the non-aircraft counter is
not part of it, so the row counts aircraft the player shot down and nothing else.

**The stamps are an enumeration, not a per-airframe grid.** `uiData` 2404 takes an ordinal 0 to 10
and the tab, walks the first array's slots 0 to 10 and then the second's, skips every zero slot, and
returns the ordinal'th non-zero one as a frame index and a count. The frame is `i` for the first
array and `i + 11` for the second, which is why `SB_killMARKERcombined.png` carries 22 frames for 11
airframes: the second eleven are the same airframes over a star. It returns -1 once the tab is
exhausted, and `SCRAPBOOK.SCRIPT` stops asking at the first -1, so the eleven `SB_KILL` slots fill
densely from slot 0 in ascending airframe order, plain kills before ace kills.

⚠ **`SB_KILL0` to `SB_KILL10` are not in reading order.** `SB_KILL1` at `467,93` is left of and
above `SB_KILL0` at `560,109`, so the leftmost stamp on a page is the second one the engine
reported, not the first. Reading a screenshot left to right gives the wrong slot order.

Read off `OriginalScreenshots/Campaign Scrapbook CM02 Mission select after another Mission.png`, the
right page carries `2 Balmoral` in slot 0, `3 Peacemaker` in slot 1 and a starred `1 Peacemaker` in
slot 2, over an Overall Planes Downed of **6**. That is Balmoral (`i` 2) and Peacemaker (`i` 9) in
the plain array and Peacemaker again in the ace array, ascending as the enumerator requires, and the
ace is `britpeace_7`, the one block in that mission's roster carrying slot 67
([`formats/ai-rosters.md`](../formats/ai-rosters.md#what-binds-a-block-to-the-ace-role)).
`Campaign Mission End screen CM01.png` is the one-stamp case: `3 Kestrel` in slot 0 over a total
of 3.

### Navigation and page composition: a data file, not code

**Which scrap sits where is authored in `extracted\rof\ASSETS\SCRAPBOOK.CSV`**, one `[SCRAPBOOK]`
section keyed `<mission>_<spread>_<item>`. The full column semantics, the filename rules, the
objective gate and the grime generator are in
[`formats/campaign-screens.md`](../formats/campaign-screens.md), "The scrapbook", because they are a
data format rather than a decode. What belongs here is the code that reads it.

`FUN_004061d0(mission, spread, item, existsOnly, forZoom)` is the whole composition lookup. It
formats the key, fetches the row through the table reader at `FUN_00411bb0`, splits it on commas
with `FUN_00404830`, and writes the results into the engine globals the script reads back:
`0x0064b33c` the resolved image path, `0x0064b328` and `0x0064b32c` the page and zoom extensions,
`0x0064b2a4`/`0x0064b2a8` the position, `0x0064b2b0` to `0x0064b2bc` the clickable region,
`0x0064b2ac` the z, `0x0064b324` the zoom layout letter, and `0x0064b2d0`, `0x0064b31c`,
`0x0064b320` the caption, title and text string ids. It returns 1 for a drawable item, `-0x66` for
one the gate suppressed, and `-0x65` when the key does not exist.

Four callers use it, and between them they are the entire book:

- **`uiData` 2407** (`0x0040aa52`) walks the items of the open spread, skipping `-0x66` and stopping
  at `-0x65`. It reports type 5 when `0x0064b33c` holds a name and type 6 when it does not, which is
  how the script tells an image slot from a text slot.
- **`uiData` 2410** (`0x0040a935`) repeats the lookup with `forZoom` set, which suppresses the 25%
  downscale a capture gets on the page, and hands the zoom view its background, inset image,
  position, layout letter and three string ids.
- **`FUN_00406170`** and **`FUN_00406100`**, the next and previous helpers behind `uiData` 2402,
  probe item 1 of a neighbouring spread with `existsOnly` set. This is why no spread count is stored
  anywhere: the book ends where the file does.

The current position is two globals, `0x00647b78` the mission and `0x00647b7c` the spread. `uiData`
2405 mode 1 sets them, taking -1 to mean the campaign's current mission at `0x0064b340`, and always
opens at spread 1. `uiData` 2403 returns the spread, and the script draws the results block only
when it is 1.

The readable range is guarded at the top of `FUN_004061d0`: a mission past both `0x0064b678` and
the campaign position is refused with `-0x65` unless the unlock flag at `0x00647b80` is set, in
which case the limit is mission 24. That refusal is what leaves an unreached slot showing langui
1219 `Not yet flown`.

**Three readings of the shipped art did not survive the table.**

- `SB_01_00_DZ.PNG` is not the danger-zone slot and no row references it. A capture is any name
  beginning `Snap_`, tested by `FUN_00406db0`, and the rows are `Snap_<mission>_<objective>` gated
  on that objective, with a `DZ_generic_corners` row at the same coordinates for the mount.
- The `SB_00_00_*` set is not a generic pool the results pages reuse. Its six rows appear in mission
  slot 0 alone. The Aloha Daily masthead recurs because it is drawn into each mission's own
  newspaper art, `SB_02_01_news4` and `SB_02_01_news5` on CM02's page.
- There is no small-version mechanism. `SB_00_00_MAG1S.PNG` and `SB_01_01_WANTED1S.PNG` are
  unreferenced; a scrap is one bitmap, scaled on hover, and its detail view is the same base name
  under the second extension of the `ImageType` field.

The `MS_P_` photographs are the cabin memento pool (`UIData +0x344`), and the book draws from the
same directory: 15 rows name an `MS_P_*` scrap, 13 of them on a results page.

### Entering the book, Replay Mission and the table of contents

**The mission-end path is native and already traced**, in ["The pass, in
order"](#the-pass-in-order) above: step 7's `FUN_0046fb60(0x0071d57c, …)` runs while
`DAT_0071c098` still holds the mission the player just flew, opening `SCRAPBOOK.SCRIPT` directly
at that mission's spread 1.

**The cabin path does not reach the book at all.** `LAYOUT.CSV`'s `PC_B_PREVIOUS` row carries
`ScriptToExe = ScrapBook_TOC`, one of the layout-declared transitions
[`formats/campaign-screens.md`](../formats/campaign-screens.md) already established for this
screen family: pressing PREVIOUS MISSIONS opens `SCRAPBOOK_TOC.SCRIPT`, the 25-row mission list,
and `SCRAPBOOK.SCRIPT` itself does not run until a row is picked from there. `SBTOC_B_RETURN` and
`SB_B_RETURNPC` both carry `ScriptToExe = PassengerCabin` the same way, which is why neither
appears in either script's `gui_mailbox`: this whole family of transitions is authored in
`LAYOUT.CSV`, and the scripts are silent about the buttons that carry one.

**The two entry paths land on different screens, not on the same screen in different states.**
Mission end opens the book; the cabin opens the table of contents. `SCRAPBOOK_TOC.SCRIPT`'s
`sbtoc_b_view` (commit the picked row) and `sbtoc_b_current` (jump to the campaign's current
mission) are what reach the book from there, both through `uiData` 2405 mode 1, the same call the
book's own bookmark uses.

**`$$SR$$` is a session flag the scripts only ever read**, and it is what makes Replay Mission
behave differently depending on how the book was reached, not whether it is offered (`uiData` 2411
gates that on the shown mission's own record alone, regardless of entry path).
`PASSENGERCABIN.SCRIPT` zeroes it at its own `gui_create`; nothing in the three scrapbook scripts
sets it, so only the native mission-end path can leave it set. `SCRAPBOOK.SCRIPT`'s `sb_b_replay`
handler:

```
FRA = callback($$E$$, 2405, 0)        // the mission the open page shows
if (FRA == $$KP$$ && $$SR$$)          // still the mission just flown, same session
    NSA = 1
callback($$E$$, 2104, (FRA))
script_end @scrapbook@
if (NSA)
    callback($$E$$, 2013, -1, ...); callback($$A$$, 1, 0)     // restart in place
else
    mail(11003, @globals@RR); callback($$A$$, 31); callback($$A$$, 6)   // the cabin's New Mission path
```

`$$KP$$` is the campaign's current mission, 1-based
([`formats/campaign-screens.md`](../formats/campaign-screens.md), "Evidence and limits"). Replay
Mission restarts in place only on the mission just flown, in the session that flew it; browsed to
from the cabin, or paged away from, it falls through to the identical three calls
`PASSENGERCABIN.SCRIPT`'s own New Mission button makes (`mail` 11003, `gosCallback` 31,
`gosCallback` 6). `SCRAPBOOK_TOC.SCRIPT`'s `sbtoc_b_replay` runs the same logic over its own
selected row.

**What is still unread.** Which native function recognises `PC_B_PREVIOUS`'s `ScriptToExe` and
where `$$SR$$` is set are both in `crimson.exe`, and the `ghidra-mcp` bridge was not reachable this
session to trace either. Neither claim above depends on the address: the screen each path reaches,
and the behavioural fork Replay Mission takes, are both read off the scripts' and `LAYOUT.CSV`'s
own text.

**Every `script_run`, `script_pause`, `script_continue` and `script_end` in the three scripts**, so
the flow above is exhaustive and not a sample of it:

| Script | Trigger | Does |
|---|---|---|
| `SCRAPBOOK.SCRIPT` | a scrap clicked (the mailbox default case) | pause, run `scrapbookzoom.script` |
| `SCRAPBOOK.SCRIPT` | `sb_b_toc`, or `sb_b_prev` returning 0 at the front of the book | pause, run or continue `scrapbook_toc.script` |
| `SCRAPBOOK.SCRIPT` | `sb_b_replay` | end itself, then the Replay Mission fork above |
| `SCRAPBOOK.SCRIPT` | `gui_destroy` | end `scrapbook_toc.script` if it is still paused |
| `SCRAPBOOKZOOM.SCRIPT` | Esc, or `sbz_b_return` | continue `scrapbook.script`, end itself |
| `SCRAPBOOKZOOM.SCRIPT` | `sbz_b_export` | run `messagebox.script` (an export confirmation; does not return to the book) |
| `SCRAPBOOK_TOC.SCRIPT` | `sbtoc_b_view`, `sbtoc_b_current`, or a double-clicked row | pause, run or continue `scrapbook.script` |
| `SCRAPBOOK_TOC.SCRIPT` | `sbtoc_b_replay` | end itself, then the Replay Mission fork above |
| `SCRAPBOOK_TOC.SCRIPT` | `gui_destroy` | end `scrapbook.script` if it is still paused |

### So this item reuses a board CSVM already has

`CampaignPreviousMissionsPage` is not quite either original screen, but closer in shape to
`SCRAPBOOK_TOC.SCRIPT` than to the book: a flat list with a pick-then-act pair of buttons and a
Return, which is the table of contents' VIEW SELECTED / REPLAY MISSION / CURRENT MISSION / RETURN
over its 25-row list. The original keeps the two screens apart: the cabin's PREVIOUS MISSIONS
button opens the table of contents, and only a picked row or the Current Mission jump reaches the
two-page book. CSVM's one class currently stands in for both. The debrief itself is the
book opened on the mission just flown, and nothing in CSVM draws that yet: the results block, the
per-scrap pages, the zoom, the page and mission arrows and the bookmark are still to build, and
where the original's book/table-of-contents split lands in CSVM's own page structure is a call for
whichever Wave C/D item builds it, not settled here.

## What the debrief does not write

Money, the airframe id and the plane name (`+0x28`, `+0x2c`, `+0x30`) and the whole merged best
half at `+0x54` belong to `FUN_00405ce0`, the completion recorder. The debrief is a reader of that
merge, not its author, and the two run in the same mission-end pass.

## Where CSVM stands

- **A lost attempt keeps its objective bits (B11).** `CampaignDirector.OnMissionEnded` banks
  `graph.CompletedMask` with only bit 0 forced clear on a loss, matching the original: both loops
  above run whatever the outcome, and only the primary bit comes from the win flag rather than the
  graph. `CampaignProgression.Record` already gated its best-of merge and the position advance on
  bit 0 alone, so a non-zero mask on a loss records statistics without completing the primary or
  advancing the campaign.
- **The two per-airframe kill arrays are counted (B13).** `MissionAttempt` and `MissionRun` carry
  `Kills`/`AceKills`, eleven-slot per-airframe tallies mirroring the record's `+0x08`/`+0x14`, and
  `CampaignDirector.CreditKill` fills them off each roster aircraft's own `Downed` report: the
  player did it, the victim's roster-authored side is hostile, and the roster's `ace` flag (slot 67)
  picks plain or starred. `CampaignProgression.MergeBest` merges both per index by maximum. The
  screen side (the stamps and the Overall Planes Downed sum) still draws nothing yet; Rockets
  Expended needs no source, since the original does not draw it.
- **A board exists, but it is shaped like the wrong original screen.** `CampaignPreviousMissionsPage`
  is a flat mission list, closer to `SCRAPBOOK_TOC.SCRIPT` than to the two-page book the debrief
  actually is; the mission-end entry, the two tabs, the results block and the Replay Mission button
  all belong to the book, which nothing in CSVM draws yet.
- **The skip offer is unimplemented.** Four failed attempts at an uncompleted mission is a
  campaign-advancing decision the player is given, not a cosmetic prompt.
- **`ObjectiveGraph.CompletedMask` is one of the original's two mask sources.** It is the
  objective-number half; the id 18 to 30 half comes from the module vector.

## What is not decoded

- **The native side of the cabin entry and the `$$SR$$` flag.** Which function answers
  `PC_B_PREVIOUS`'s `ScriptToExe = ScrapBook_TOC` and where `$$SR$$` is set before the mission-end
  path opens the book are both unread; the `ghidra-mcp` bridge was not reachable in the session
  that wrote the entry-path decode. Nothing above depends on either address (A3).
- **The wording of string 191.** `extracted/rof/ui_strings.json` jumps from id 136 to id 200, so
  the skip offer's text is absent from the extraction. Its shape is known (a Yes/No message box
  taking a string and the number 4 as its two format arguments) and its wording is not.
- **The `0x0071b480` overlap.** The campaign object's base and the `PilotStatus` save section's
  destination are the same address, and the attempt counters this page reads sit inside the
  1448-byte range that section covers, yet the only available profile's `PilotStatus` is all zeros
  after twenty completed missions. Either a second global shares the address or the counters are
  cleared before the section is written. Nothing here depends on the answer.
