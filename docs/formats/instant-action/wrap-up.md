# Instant Action wrap-up

Part of: [Instant Action](../instant-action.md).

## The wrap-up screen

`IA_WRAPUP.SCRIPT` and `LAYOUT.CSV` wire the post-mission board. The `langui` table carries **six**
consecutive title strings:

| Id | Symbol | Text |
|---|---|---|
| 1133 | `IDS_IAWU_TITLE` | Instant Action |
| 1134 | `IDS_IAWU_TIME_TITLE` | Time to Complete Mission |
| 1135 | `IDS_IAWU_DESTROYED_TITLE` | Enemies Shot Down |
| 1136 | `IDS_IAWU_ZONES_TITLE` | Danger Zones Completed |
| 1137 | `IDS_IAWU_SHOTS_TITLE` | Shot % |
| 1138 | `IDS_IAWU_KILLS_TITLE` | Total Kills |

**⚠ Only four of those six are actually wired to a row — "Total Kills" is not.** `LAYOUT.CSV`
declares exactly four `IAWU_LINE*` brushstroke panes and four value texts (`IAWU_T_TIME`,
`IAWU_T_DESTROYED`, `IAWU_T_ZONES`, `IAWU_T_SHOTS`), plus the screen's own title
(`IAWU_T_TITLE=T,IDS_IAWU_TITLE,…`); there is no fifth data line, no fifth value text, and no
`IAWU_T_KILLS`-shaped row anywhere in the CSV. `IA_WRAPUP.SCRIPT` matches: `gui_init` fills exactly
four strings from one callback (`callback($$E$$, 2352, GT, HT, IT, JT)`) and assigns them to the
four value texts; no fifth variable or object exists. `RESOURCE.H` confirms the format-string side:
there are only four `IDS_IAWU_*` **format** ids (1185 `TIME`, 1186 `KILLED`, 1187 `DANGERZONES`,
1188 `PERCENTAGE`) feeding those four rows — no `IDS_IAWU_KILLS` format id exists at all. Of the six
title strings, `IDS_IAWU_KILLS_TITLE` alone has no reference anywhere outside `RESOURCE.H`'s own
`#define` — every other title, including the screen heading `IDS_IAWU_TITLE`, is referenced by a
`LAYOUT.CSV` row.

So the shipped wrap-up screen shows **four** rows — Time to Complete Mission, Enemies Shot Down,
Danger Zones Completed, Shot % — not five. "Total Kills" is a defined-but-unwired string, the same
class of finding as `strings.md`'s spyglass/padlock case: present in the table, absent from the
screen that would have used it. **This corrects the milestone goal's and A5/G14's "five rows"
framing** — G14 should render four rows, and A5's counter decode only needs to answer for those
four (Shot %'s numerator/denominator remains the open question).

| Row | Format id | Format |
|---|---|---|
| Time to Complete Mission | `IDS_IAWU_TIME` (1185) | `%02d:%02d` |
| Enemies Shot Down | `IDS_IAWU_KILLED` (1186) | `%d` |
| Danger Zones Completed | `IDS_IAWU_DANGERZONES` (1187) | `%d` |
| Shot % | `IDS_IAWU_PERCENTAGE` (1188) | `%d%%` |

### What the four numbers count

 `gui_init` makes exactly one engine call, `callback($$E$$, 2352, GT, HT,
IT, JT)`, and its arm in `crimson.exe` is `0x0040c644`-`0x0040c749`. That arm formats all four
strings and writes them back through the four out-pointers, so the whole board is one function
reading one record.

**The wrap-up reads a frozen snapshot, not the live counters.** `FUN_00443090` is the mission-end
handler: when the mode global `DAT_0071bb80` is **3** (Instant Action, set by the launcher
`FUN_004174d0`) it calls `FUN_00419700`, which calls `FUN_00419630(&DAT_0064ad8c)` to copy the live
counters into the snapshot block at `0x0064ad8c`. Every other mode takes `FUN_004194e0` and a
different layout, which is why the snapshot has two shapes and only the mode-3 one is a set of four
scalars.

The live counters are all fields of **one object at `0x0071d2a0`**, zeroed together by
`FUN_004a22a0` off the mission-load path:

| Object offset | Snapshot offset | Field |
|---|---|---|
| `+0x00`…`+0x2b` | `+0x08` (summed) | kill table A, one dword per aircraft type 0 to 10 |
| `+0x2c` | not read | kills of anything that is not one of the eleven aircraft |
| `+0x30`…`+0x5b` | `+0x08` (summed) | kill table B, same indexing |
| `+0x5c` | `+0x20` | cannon rounds the local player fired |
| `+0x60` | `+0x22` | cannon rounds the local player hit with |
| `+0x88` | `+0x14` | danger zones completed |

**Time to Complete Mission** is snapshot `+4`, `ftol(clock × 1000.0)` over the mission clock at
`0x0071b468`, so it is a straight elapsed time in milliseconds. The row renders
`minutes = ms / 60000` and `seconds = (ms / 1000) % 60` (reciprocal multiplies `0x10624dd3 >> 6`
and `0x45e7b273 >> 14`), both truncating rather than rounding.

**Enemies Shot Down** is the sum of **both** per-aircraft kill tables over types 0 to 10. A kill is
recorded in `FUN_004b9bc0` only when the victim's team (`+0x08`) is **greater than 1**, which is the
decoded team space again, so a friendly or neutral kill never counts. Since the original applies
friendly damage (below), a wingman you shoot down is a kill that this row will not show.

⚠ **Two things are silently excluded.** The victim's category field `+0x67c` must be 0 or 4 to reach
the per-aircraft tables at all; everything else lands in the object's `+0x2c` bucket, and the
wrap-up never reads `+0x2c`. And `FUN_00426e30`, which turns the victim's def name into an aircraft
index, returns **11** for a name it does not recognise, which is one past the summed range. So
ground targets, zeppelins and anything off the eleven-aircraft list do not appear in "Enemies Shot
Down". Which of the two tables a kill lands in is decided by a byte on the victim (`+0x988`, copied
at spawn from roster-block byte `+0xa4`); because this row sums both, that split does not change it
and was not chased.

**Danger Zones Completed** is the object's `+0x88`, incremented by `FUN_00446990`. A zone completes
when **more than one** of its gates has been flown (gate records from `+0x34` to `+0x38`, stride
`0x14`, with a passed byte at `+0x10`), and the increment then happens only if the zone's own latch
byte `+0x40` is still clear. The latch is set immediately afterwards.

⚠ **So this counts distinct zones completed, once each. Flying the same zone a second time does not
increment it.** The row is also present on every mission type; on a mission with no zones nothing
completes one, so it renders `0` rather than being hidden.

**Shot %** is `ftol(100.0 × snapshot+0x22 / snapshot+0x20)`, that is **hits over rounds fired**, and
both halves carry the **same** filter:

- The denominator increments in `FUN_004b6820`, the per-station fire loop, once per round that is
  actually created (`FUN_005aef40` returned non-zero) and only when the firing vehicle is the local
  player (`DAT_0071c298`).
- The numerator increments at three sites (`0x004ba04e` in `FUN_004b9bc0`, `0x004bad0c` in
  `FUN_004bab50`, `0x004c08c1` in `FUN_004c0880`, covering three kinds of thing hit), each guarded
  by the shooter being the local player and by `TEST byte ptr [weaponDef], 0x40`.
- Bit `0x40` is the **`CANNON`** flag. `FUN_004ba6f0` is the weapon-flags parser and its
  `OR dword ptr [ESI], 0x40` at `0x004ba9ba` follows the `CANNON` key string at `0x0062b320`
  ([weapons.md](../weapons.md) lists `CANNON` on 31 entries).
- The fire side carries the same filter even though no `0x40` immediate appears in `FUN_004b6820`:
  the function hoists the bit to `(weaponFlags >> 6) & 1` at the top of each station and the
  increment sits inside that arm. The ordnance arms of the same function create their rounds
  through `FUN_005aef40` without ever touching the counter.

⚠ **So Shot % is cannon hits over cannon rounds fired, both by the local player, and ordnance is
excluded from both halves.** Of the two readings this could have had, it is the one that excludes
ordnance, and it is symmetric. Corroboration that the pair belongs together: `FUN_00499490` packs
exactly these two globals into one network message.

Two edges G14 has to decide about rather than inherit. **Nothing guards a zero denominator**: fire
no cannon round and the x87 divide yields infinity, which `ftol` turns into `0x80000000`, so the row
would print a large negative number. That is read from the instructions, not observed in the
original, and it should be handled deliberately. And **both counters are incremented as 32-bit
dwords but snapshotted as 16-bit words**, so past 65535 the row wraps.

### What the launcher maps

`FUN_004174d0` is the Instant Action launcher and carries two dropdown-to-internal maps worth
having. The mission-type dropdown index becomes the internal id **0 → 0, 1 → 1, 2 → 4, 3 → 2**,
which reproduces the ace / squadron / stunt / zeppelin ordering settled from the parser side above,
now confirmed from the launcher as well. The environment dropdown index becomes a chapter id
**0 → 1, 1 → 5, 2 → 6, 3 → 8, 4 → 2, 5 → 7, 6 → 4**, and the chapter is then loaded as mission **1**
(`FUN_004638f0(chapterId, 1)`), matching `<chapter>/IA1/`.

No chapter folder name exists anywhere in `crimson.exe` (searched: no `c1b`, `c1c` or `c2b`
string), so the executable never spells out which folder an id names. The ids are the eight chapter
folders in alphabetical order, C1 = 1 through C5 = 8, which resolves the map to **C1, C2B, C3, C5,
C1B, C4, C2** and leaves id 3 (C1C) unreferenced. That is the mapping `CSVM/src/UI/LaunchMenu.cs`
has carried all along, decoded before this plan and restated in its `Chapters` comment, so this
read is a second source for it rather than a new finding. It corrects the "Environment → chapter"
table above, which A5 had wrong on two rows.

## Friendly fire

**The original applies friendly damage.** A round from one aircraft damages another whatever the
two teams are: the path from impact to the drained pool carries no team test, and the team ids gate
the target scan and the radio lines instead. A wingman on the player's side can therefore be shot
down by the player or by another wingman, and an Instant Action flight needs no damage gate of its
own. The decode is [`org/vehicleDamage.md`](../../org/vehicleDamage.md)'s "Teams and friendly fire"
section; the team space it reads (0 neutral, 1 the player's side, 2 and up enemy) is
on [turrets.md](../turrets.md).
