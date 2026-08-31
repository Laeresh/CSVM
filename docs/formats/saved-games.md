# Saved games and player profiles

Part of the [format documentation](README.md). This page describes the original's
`CrimsonSkiesGame\SavedGames\<Profile>\` files: the one container format they all share, and what
the campaign profile keeps in it (funds, owned planes with their ammunition and ordnance picks,
per-mission results, and how far through the campaign the player is). It also states what
`Persist.NNN` and `Mission.NNN` actually carry.

The decode stops where the structural questions are answered. It is deliberately not
byte-complete, there is no writer, and importing an original profile is out of scope
. [Evidence and limits](#evidence-and-limits) lists every place
the decode stops and why.

## Contents

- [Conceptual model](#conceptual-model)
- [Container layout](#container-layout)
- [Save categories](#save-categories)
- [`Status.dat`: the profile](#statusdat-the-profile)
  - [The `UIData` block](#the-uidata-block)
  - [Which player is current, across runs](#which-player-is-current-across-runs)
  - [Funds and the plane list](#funds-and-the-plane-list)
  - [Where the ammunition and ordnance picks live](#where-the-ammunition-and-ordnance-picks-live)
  - [The mission-result array](#the-mission-result-array)
- [`Persist.NNN`](#persistnnn)
- [`Mission.NNN`](#missionnnn)
- [`AutoSave.sav`](#autosavesav)
- [Mission ids and the campaign sequence](#mission-ids-and-the-campaign-sequence)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

Every file in `SavedGames\` is the same thing: a **named-section archive**. Engine subsystems
register themselves as saveable objects under a name and a **category mask**; a save is a request
to write every registered object whose mask intersects the requested category. The file is the
payloads back to back, then a directory of fixed-size entries naming them, then a two-word tail.

A section's name is `Owner/Key`. `Owner` matches a registered object; `Key` is handed to that
object's load callback so one owner can hold many sections (`TurretList/ltur1`,
`AnimActivation/Activation0010`, `PlayerVehicle/wingman_1`). The loader splits on `/` and skips any
section whose owner is not registered in the running build, which is why the same container serves
four unrelated purposes without a per-file format.

The category mask is what makes `Status.dat`, `Persist.NNN`, `Mission.NNN` and `*.sav` different
files rather than different formats. Nothing in the container is campaign-specific.

## Container layout

| Region | Contents |
|---|---|
| `0x00` .. *D* | section payloads, in directory order, no padding or alignment between them |
| *D* .. *D* + 148 x *N* | the section directory, *N* entries of 148 bytes |
| last 8 bytes | a constant `1`, then the entry count *N* |

Read it back to front: the final dword is *N*, the directory starts at `filesize - 8 - 148*N`.

Each 148-byte directory entry:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 4 | payload offset from the start of file |
| `0x04` | 4 | payload length in bytes |
| `0x08` | 80 | section name `Owner/Key`, NUL-terminated |
| `0x58` | 60 | runtime scratch, not part of the format |

The trailing 60 bytes are uninitialised stack: two entries with identical name, offset and length
in `Persist.102` and `Persist.104` carry different bytes there, and the values read as live
pointers into the writing process. A reader must ignore them; a writer would have to reproduce
nothing.

The first section of every file is `zSaveHeader`, 12 bytes at offset 0, which the loader checks
before doing anything else:

| Offset | Value observed | Meaning |
|---|---|---|
| `0x00` | `12` | the header's own byte length, checked twice by the loader |
| `0x04` | `2` | format version, a constant the writer passes in |
| `0x08` | varies | the category mask this file was written for |

**A payload's first dword is its own byte length only where the owning object says so.** It holds
for `zSaveHeader` (12), `PlayerVehicle` (4) and `VehicleList` (0x198), whose loaders reject a
payload whose first dword does not match. `PilotStatus` and `UIData` instead `memcpy` the whole
payload over a fixed global, so their first dword is data.

## Save categories

The registration call takes a name, a save callback, a load callback, a capacity and the category
mask. The masks partition the files:

| Mask | File | Objects registered for it |
|---|---|---|
| `1` | `Persist.NNN` | `PlayerVehicle` |
| `2` | `Status.dat` | `PilotStatus`, `UIData` |
| `0x10000` | `*.sav` savegames | `VehicleList`, `Player`, `Persistence`, `Mission`, `Objective`, `InMenu`, `ZeppelinList`, `DZPathList`, `MStructList`, `Voices` |
| `0xffff0000` | `*.sav` **and** `Mission.NNN` | `AnimActivation`, `RunningAnim`, `Anim`, `TurretList`, `Weapons`, `GWWorld`, `zDEClient` |

`Mission.NNN` uses mask `0x20000`, which only the `0xffff0000` group intersects. That is why a
`Mission.NNN` contains world state and never contains the player's aircraft.

`Status.dat` is written unconditionally at mission end; `Mission.NNN` and `Persist.NNN` are written
in the same pass, guarded by a chapter in 1..9 and a mission in 1..10, from
`sprintf("%s\\Mission.%1d%02d")` and `sprintf("%s\\Persist.%1d%02d")` (`FUN_0046b490`,
`FUN_0046b450`). The profile directory is baked in as an absolute path (see
[the `UIData` block](#the-uidata-block)).

Two further gates sit in front of that pass, and both matter to what carries across missions:

- **A campaign object must exist.** `FUN_0046b450` tests the global campaign pointer `DAT_0071bb7c`
  before writing anything. Nothing outside a campaign has a mission id to write under.
- **The mission must have been won.** The same function then reads the campaign object's win flag
  (`+0xc58`, through `FUN_00463be0`) and skips the pass when it is clear. That flag is the one the
  debrief `FUN_004194e0` branches on to set the mission-result record's completion bit, and the one
  `FUN_0046ba10` picks the success cinema with; the debrief's failure branch offers to skip the
  mission after repeated attempts and sets the flag when the offer is taken.

So a lost or abandoned attempt writes no world state. The chapter's world stays as the last **won**
mission left it, and a retry starts from that state rather than from the failed attempt's wreckage.

## `Status.dat`: the profile

Three sections, in this order:

| Section | Length | Contents |
|---|---|---|
| `zSaveHeader` | 12 | as above, mask `2` |
| `PilotStatus/PilotStatus` | 1448 | a flat 362-dword struct, copied verbatim over the global at `0x0071b480` |
| `UIData/UIData` | 10820 | a flat 2705-dword struct, copied verbatim over the global at `0x0064b340` |

**`PilotStatus` is 1448 zero bytes in the only profile available**, so nothing can be said about
its fields. Its loader is a plain `memcpy` with no field handling, and its capacity is registered
at 100.

Everything the campaign screens need is in `UIData`. Because the block is a verbatim image of the
global at `0x0064b340`, a `UIData` offset and an executable address differ by a constant: field
`+X` lives at `0x0064b340 + X`, and at file offset `0x5b4 + X` in a `Status.dat` whose directory
places `UIData` at `0x5b4`.

### The `UIData` block

| Offset | Field | Evidence |
|---|---|---|
| `+0x00` | mission index the result recorder writes to | the array index in `FUN_00405ce0`; zeroed for a new profile in `FUN_004113b0` |
| `+0x08` | a screen/state id | compared against 6 in `FUN_00416ad0` |
| `+0x0c` | the profile directory as an absolute path, NUL-padded | zeroed for a new profile; `FUN_0046b1e0` returns it and every save path is `sprintf("%s\\...")` on it |
| `+0x110` .. `+0x310` | video and detail settings, including the renderer name buffers and a `640`,`480` resolution pair at `+0x15c` | the two device-name strings are the shipped renderer names |
| `+0x314` | pilot name, 32 bytes | `FUN_004113b0` fills it from the registry, falling back to langui string 500; see "Which player is current" below |
| `+0x338` | missions completed, which is also the sequence index of the next mission | raised to the current mission index only inside the primary-objective-complete branch of `FUN_00405ce0`; zeroed for a new profile |
| `+0x33c` | selected plane, an index into the plane array | `FUN_00405ce0` reads the flown plane's name and airframe from this slot |
| `+0x344` | the current memento image file name | `FUN_004113b0` seeds it with `MS_P_InitialPinup1.jpg` |
| `+0x448` | **funds** | see below |
| `+0x44c` | the plane array, 26 slots of 204 bytes | [paint.md](paint.md), "Saved custom planes" |
| `+0x1868` | the mission-result array, indexed from 1 | see below |
| `+0x28d4` | default savegame file name | seeded from langui string 507 |

The pilot-name buffer is not cleared before a shorter name is written into it: the sample profile
reads `Zachary\0achary\0`, the tail of a longer previous name. A reader must stop at the first NUL.

### Which player is current, across runs

**The current player is remembered in the registry, by name, outside every save file.** The key is
`HKEY_CURRENT_USER\SOFTWARE\Microsoft\Microsoft Games\Crimson Skies\1.0` and the value is
`UIPlayerName`, a REG_SZ. `FUN_00404960`'s UI-string-save message (`0x85d`, index 0) writes
`+0x314` there through the generic string writer `FUN_00407440`, and `FUN_004113b0` reads it back
through `FUN_004073d0` when `+0x314` is empty, using langui string 500 only as the fallback for a
machine that has never played. The same key and the same two helpers carry the multiplayer
callsign, game name, team name and connection settings (`UICallsign`, `UIGameName`, `UITeamName`,
`UIMPVoice`, `UIMPAutoRefresh`, `UIIPAddress`, `UIPhn`, `UIConType`).

So the record is a **name**, it lives outside `SavedGames\`, and nothing in the container format
carries it. A directory timestamp is not the mechanism and never was.

### Funds and the plane list

`+0x448` is the player's money. Two independent statements in `FUN_004113b0`, the new-profile
reset, settle it: the field is set to `0` for a fresh profile, and to `250000` on the branch that
also fills eleven plane slots with the eleven stock airframes. `FUN_00405ce0`, the
mission-completion recorder, adds a per-objective payout to it from a table at `0x0061ae80`
(stride 20 bytes: mission ordinal, objective bit, amount, an airframe id for the five records
that award an aircraft, a trailing pointer; [campaign-screens.md](campaign-screens.md)
"Flight check") and adds the same amount to the flown mission's result record. The sample
profile holds 21840.

Immediately after it, `+0x44c` begins the plane array already documented in
[paint.md](paint.md): 204-byte records, stride `0xcc`, name at `+0x04`, airframe at `+0x2c`. That
page's array address `0x0064b78c` is this offset. The array is 26 slots (the new-profile reset
clears exactly 26 x 204 bytes); slot 25 is the hangar's build-in-progress scratch copy, so at most
25 planes are ownable. Slot `+0x00` is the owned flag, `0` on an empty slot. The sample profile
holds nine planes and a selection index of 4, whose slot name and airframe match the plane recorded
against the most recent mission attempt.

### Where the ammunition and ordnance picks live

`docs/formats/loadouts.md` states there is no player loadout anywhere in the ZBD data. It is in the
204-byte plane record, in the field groups that [paint.md](paint.md) describes only in terms of the
values the hangar's own commit writes:

| Offset | Field |
|---|---|
| `0x34`, `0x38` | hardpoints on the left and the right wing, which bound the pylon cells |
| `0x88`, `0x8c`, `0x90`, `0x94` | four **gun-slot ids**, `0` to `4` selecting 30, 40, 50, 60 and 70 calibre, `5` = no gun |
| `0x98`, `0x9c`, `0xa0`, `0xa4` | **per-gun ammunition index**, `4` = no gun |
| `0xa8` .. `0xc4` | **per-pylon ordnance id**, eight cells, four per wing |

`FUN_00443de0` is the mission-start applier and reads all three groups off the profile's plane
record (or off a scratch record on the Instant Action path). It resolves each gun through
`FUN_00443d70`, which takes the gun-slot id and the ammunition index and returns the weapon
number: ids `0` to `4` give 30, 40, 50, 60 and 70, and the ammunition index is added to that, so a
gun slot resolves to `wep_{caliber + ammo}`, the rule [loadouts.md](loadouts.md) states for stock
guns. An ammunition index of `4`, or a gun id the switch does not name, returns -1 and the slot
carries no weapon. That confirms from the executable what a cross-record observation over the nine
planes in the sample profile also shows, that `ammo[i] == 4` holds in exactly the slots where
`gun[i] == 5`; the remaining ammunition values are `0` to `3` (`slug`, `dumdum`, `ap`,
`magnesium`). The hangar's commit writes `0` or `4` here, which is the same field seen before the
campaign's Ammo Selection screen has touched it.

**The per-pylon ordnance id is the rocket table's index**, the twelve-row table
[campaign-screens.md](campaign-screens.md) decodes for the Ammo Selection screen, and not the
dropdown row that screen displays. Two independent paths in the executable read the field as that
index. `FUN_00443de0` walks the eight cells from `+0xa8` and passes each through `FUN_004440f0`, a
switch whose result is formatted `wep_%02d` and looked up by name in the ZWEP catalog
(`FUN_004bad90` into `FUN_005abfd0`, an exact name match). The Ammo Selection callback for
`uiData` 2031 at `0x00409aec` reads `+0xa8 + 4 * cell` off the same record and adds `0xd43`, which
is 3395, the `IDS_ROCKETSHORTNAME` base, so the stored value indexes the string block directly.

| Id | Ordnance | Weapon | In the sample profile |
|---|---|---|---|
| 0 | Armor-piercing rockets | `wep_05` | not used |
| 1 | High-explosive rockets | `wep_06` | Jumping Jane on all eight pylons, Gypsy Magic and Mk III on one each |
| 2 | Flak rockets | `wep_07` | eight of the nine planes |
| 3 | Sonic rockets | `wep_08` | not used |
| 4 | Flash rockets | `wep_09` | not used |
| 5 | Rear flash rockets | `wep_15` | Red Hot Spender, on its second left pylon |
| 6 | Smoke screen | `wep_13` | not used |
| 7 | Choker rockets | `wep_12` | not used |
| 8 | Beeper rockets | `wep_10` | not used |
| 9 | Seeker rockets | `wep_11` | not used |
| 10 | Aerial torpedoes | `wep_14` | Accipiter Annie, on its two outer pylons per wing |
| 11 | None | none | every cell past a wing's hardpoint count |

Ids `0` to `4` run in step with `wep_05` to `wep_09`, and the rest do not, so the mapping has to be
read off `FUN_004440f0` rather than derived from an offset. `FUN_004440f0` names ids `0` to `10`
and returns -1 for everything else, so `11` and any out-of-range value leave the pylon empty; the
vocabulary is closed at those twelve values.

Cells `0` to `3` are the left wing and `4` to `7` the right, each half bounded by the hardpoint
count at `+0x34` and `+0x38`. In all nine planes of the sample profile, `11` sits on exactly the
cells past that count and never inside it, and the eight-pylon planes (Jumping Jane, Accipiter
Annie) carry no `11` at all. The hangar's commit writes `1` or `11`, so any other value is the
campaign's own per-pylon pick. Red Hot Spender carries Flak on one pylon and a rear flash rocket on
another, and Accipiter Annie carries Flak inboard and torpedoes outboard, which is a mixed load
observed in shipped data rather than inferred from the design
([loadouts.md](loadouts.md), "Schema limit"). The torpedo also respects the table's availability
gate: it is offered from mission 20, and this profile has 20 missions completed.

**CSVM stores a different vocabulary in the same-named field.** `CampaignProfileStore`'s
`Ordnance[8]` holds a one-based index into `stock_loadouts.json`'s `PylonOrdnance` option list,
with `0` meaning unset, which `CampaignLoadout.For` reads as `Ordnance[cell] - 1`. That is a
CSVM-side stand-in, not this decode. Reconciling the two would migrate every existing CSVM
profile, so the two numbering schemes are deliberately separate and neither is derived from the
other.

One asymmetry in `FUN_00443de0` is traced but not confirmed in play: the pilot's pylon results are
stored for the `wep_%02d` path, while the wingman's go to `FUN_00444300`, which formats
`wep_%2d`. Ids `0` to `4` resolve to weapon numbers 5 to 9, which that format renders with a
leading space, and the catalog lookup is an exact name match, so those five ordnance types would
find nothing on the wingman's pylons. The original cannot be run here to see what that looks like.

### The mission-result array

24 records of 168 bytes at `+0x1868`, indexed **from 1**. Index 0 overlaps the plane array's
scratch slot and is never used; the new-profile reset clears exactly 24 x 168 bytes starting at
record 1. 24 is also the number of campaign missions (see below).

Each record is two halves of `0x54` bytes: the most recent attempt at `+0x00`, and the merged
best/cumulative result at `+0x54`. `FUN_00405ce0` performs the merge on mission completion, and
each field's merge rule is what identifies it:

| Offset | Best at | Merge | Field |
|---|---|---|---|
| `+0x00` | `+0x54` | bitwise OR | completed-objective mask; bit 0 is the mission-won flag and gates the whole merge, every other bit an `IDENTITY` priority (see below) |
| `+0x04` | `+0x58` | keep the smaller, ignoring 0 | mission time in **milliseconds**, the writer multiplying the mission clock in seconds by a stored `1000.0f` |
| `+0x08` | `+0x5c` | per-index maximum | twelve single-byte counters, eleven of them written: per-airframe kill tallies, plain kills |
| `+0x14` | `+0x68` | per-index maximum | twelve more single-byte counters on the same indexing: the same eleven airframes' ace kills |
| `+0x20` | `+0x74` | keep the pair with the larger second/first ratio | a `ushort` pair, shots and hits |
| `+0x28` | `+0x7c` | accumulate | money paid for this mission, the same amount added to funds |
| `+0x2c` | `+0x80` | copied when the attempt completed more objectives | airframe id of the plane flown |
| `+0x30` | `+0x84` | copied with the airframe id | name of the plane flown, 36 bytes |

Eleven sources feed twelve slots through an identity lookup, so eleven bytes of each array are
written and the twelfth is a never-taken fall-through slot. The two arrays are per-airframe kill
tallies over the eleven stock airframes, plain kills and ace kills, credited by the single damage
resolver that reaches every kill in the game
([`org/debrief.md`](../org/debrief.md#what-the-tallies-count)). The two arrays are written only on
the campaign path; in game mode 3 the same two offsets hold scalars instead, `+0x08` a single
`ushort` summing all twenty-two counters and `+0x14` the count of danger zones completed.

The attempt half is written at mission end by the debrief, which zeroes the whole `0x54` bytes
first and leaves the merged half alone; money, the airframe and the plane name are the completion
recorder's, and they are the only attempt-half fields the debrief does not fill. The mask's bits
above bit 0 are set whether the mission was won or lost, so a failed attempt still records which
objectives it met.

⚠ **A mask bit is an `IDENTITY` priority, not a display-row index.** The writer (`FUN_004194e0`)
sets bit 0 from the mission-won flag, then walks the objectives display and, for each completed
row, ORs `1 << row.priority` from the row's own stored priority (row stride `0x1c`, completed flag
at `+0x10`, priority at `+0x14`), skipping a priority of 0. Priorities are the numbers authored in
`IDENTITY` and are neither contiguous nor row-ordered, so a mission whose six rows are priorities
1, 2, 3, 4, 11 and 12 records bits 1, 2, 3, 4, 11 and 12, never bits 0 to 5. This is what the
mission-reward table's objective-bit column names ([`../org/hangar.md`](../org/hangar.md), "The
mission reward table"), and reading it as a row index pays the wrong missions. A second loop in the
same writer ORs bits 18 to 30 from another subsystem, one flag per index; which subsystem is **not
decoded here**, and the shipped data's highest authored priority is 17, so the two ranges do not
overlap.

The screen that displays the record is the **scrapbook**, whose Best to Date and
Most Recent tabs are this record's merged and attempt halves. That pass, the bit numbering, the
per-mission retry counter behind the original's skip-this-mission offer and the screen's rows are
[`org/debrief.md`](../org/debrief.md).

The sample profile's records 1 to 20 are complete, record 21 has an attempt with no merged result
(its objective mask lacks bit 0), and records 22 to 24 are zero.

## `Persist.NNN`

Two or three sections, 320 or 472 bytes:

| Section | Length | Payload |
|---|---|---|
| `zSaveHeader` | 12 | mask `1` |
| `PlayerVehicle/player` | 4 | the dword `4` |
| `PlayerVehicle/wingman_1` | 4 | the dword `4`, present only in the 472-byte files |

**`Persist.NNN` carries no state.** The four bytes are the payload's own length, which the loader
checks and nothing else; the load callback behind that check is a single `RET 0x4` at `0x004b4370`,
an empty stub. All 20 files in the sample profile have this shape, differing only in whether a
wingman section is present. The file records which player-side vehicles existed in that mission and
nothing about them.

The loader is reached from mission start: the campaign object walks its mission list backwards from
the current entry to the previous entry whose per-entry flag is set, and loads that mission's
`Persist.NNN` (`FUN_0046b840` into `FUN_0046b5f0`). So the mechanism for carrying player-vehicle
state across missions exists and is wired, and in the shipped build it moves nothing.

For `BL-243`, the cross-mission state that does move is in `Mission.NNN`, not here.

## `Mission.NNN`

24 KB to 148 KB, one per flown mission, mask `0x20000`, so it holds exactly the objects registered
with `0xffff0000`: `AnimActivation/ActivationNNNN`, `RunningAnim/RunningNNN`, `Anim/AnimNNNN`,
`TurretList/<turret node name>`, one `Weapons/WeaponData`, one `GWWorld/world1` and one
`zDEClient/Dummy`. The sample profile's `Mission.102` has 143 sections and `Mission.203` has 60.

This is the world-state carrier. At mission start the campaign object searches its mission list
backwards for the most recent earlier entry **in the same chapter** and loads that mission's
`Mission.NNN` (`FUN_0046b7e0` into `FUN_0046b560`), which restores animation activation state,
running animations, turret state and world state into the new mission. Turret sections are 280
bytes, `AnimActivation` sections are 88, 328, 568, 628 or 700 bytes depending on the definition.

**The walk starts strictly before the mission being opened**, so a mission never loads its own
`Mission.NNN`: re-flying one opens on the world the mission before it left, and re-flying a
chapter's FIRST mission (`FUN_0046b7e0` returns nothing) opens on the bootstrap alone. Chapter 1's
first mission is `seq` 6, `c1/m02`, whose fort would otherwise come up with the AA guns the last
sortie shot down.

**The per-section payloads are not decoded.** Only the container, the section names and the
per-owner section lengths are established here.

**The backwards walk is keyed on the campaign sequence, so Instant Action never picks a
`Mission.NNN` up.** `FUN_0046b7e0` starts from the campaign object's current `cm_sequence` index
(`+0xc18`) and steps backwards through the mission list `FUN_0046bf40` built from `cm_sequence.zrd`,
returning the first earlier entry whose `campaign` field matches. `cm_sequence.zrd` holds exactly 24
entries, the 24 campaign missions, so an Instant Action or multiplayer mission has no index to walk
back from and `FUN_00463e80` loads nothing. ⚠ `+0xc18` is set to -1 only when the mission list is
read and is set again only by the campaign launch path `FUN_0046c140`; the other paths that set the
current chapter and mission (`FUN_00417090`, `FUN_004174d0`, `FUN_0041a880`, `FUN_00427b9d`,
`FUN_00496c60`) leave it alone. Whether a stale index can therefore survive into a later
non-campaign launch in the same process is not settled statically, and would be a defect of the
original rather than behaviour to reproduce.

## `AutoSave.sav`

Two sections: `zSaveHeader` with mask `0x10000`, and `InMenu/UIData`, 10820 bytes. `InMenu` and
`UIData` register the same load callback over the same global, so the autosave is a second copy of
the profile's `UIData` block taken while in the menus. A `*.sav` written in flight would instead
carry `VehicleList`, `Player`, `Persistence`, `Mission`, `Objective` and the `0xffff0000` world
group; no such file exists in the sample profile.

`VehicleList` sections are 408 bytes and carry full flight state (position at `+0x04`, orientation
at `+0x10`, velocity at `+0x1c`, then damage and per-weapon-slot fields). That is the shape a
`Persist.NNN` entry does **not** have.

## Mission ids and the campaign sequence

The `NNN` suffix is `sprintf("%1d%02d", campaign, mission)`: a one-digit campaign number and a
two-digit mission number, validated as campaign 1..9 and mission 1..10. The campaign number is a
**world folder index**, not a story chapter: 1 = `ZBD\C1`, 2 = `C1B`, 3 = `C1C`, 4 = `C2`,
5 = `C2B`, 6 = `C3`, 7 = `C4`, 8 = `C5`. Every id in the sample profile resolves to an existing
`ZBD\<folder>\M<NN>` directory, and every mission directory in those folders is named by exactly
one id, with no orphan on either side.

**The campaign order is not in the save.** It is in `extracted\zrdr\cm_sequence.zrd.json`: 24
entries with `seq` 0..23 and, per entry, `campaign`, `mission`, `area` and a `wingman` flag. The
list is a flat sequence with no branch or predicate of any kind. The mission-result array index is
`seq + 1`, and `UIData +0x338` is the count of completed missions, so the next mission is
`seq == +0x338`.

Three independent sets agree on this and pin the index:

- the 20 `Persist.NNN`/`Mission.NNN` ids in the sample profile are exactly the ids of `seq` 0..19,
  as a set;
- mission-result records 1..20 are complete, record 21 has an attempt without a completion, and
  `seq` 20 (`801`) is the one id with no file on disk;
- the 24 briefing narration wavs are `c1-HA-m1` .. `c5-MH-m4`, whose chapter and mission numbering
  is `seq / 5` and `seq % 5`, and whose area abbreviations follow `cm_sequence`'s `area` field in
  sequence order (Hawaii, Northwest, Hollywood, Rocky Mountains, Manhattan).

The `C1B`, `C1C` and `C2B` folders are therefore not branches. Each story chapter's five missions
are split across a main folder and its lettered siblings, and the five ids of one chapter reassemble
into `M01`..`M05`.

## Reader rules and edge cases

- **Parse the directory from the end.** There is no header count and no magic at offset 0; the
  first 12 bytes are the `zSaveHeader` payload, which starts with its own length.
- **Ignore the last 60 bytes of every directory entry.** They are live pointers from the writing
  process and differ between files with identical section content.
- **Do not assume a payload starts with its length.** Only owners whose loader checks it do
  (`zSaveHeader`, `PlayerVehicle`, `VehicleList`).
- **Section names repeat.** `Mission.102` holds `TurretList/ctur1` six times and
  `TurretList/rtur1` four times. The directory is a list, not a map, and a map view silently drops
  sections.
- **`UIData` and `PilotStatus` are raw images of executable globals.** Their field offsets are
  build-specific by construction and mean nothing without the addresses they came from.
- **Strings in fixed buffers are not zero-filled.** Stop at the first NUL and ignore the tail.
- **The profile directory is stored as an absolute path**, so a profile copied to another install
  carries a stale path in `UIData +0x0c`.

## Evidence and limits

Every field claim above is backed either by a callback traced in `crimson.exe` or by a variation
observed across the sample set, and the table entries name which. Claims that rest only on where a
value sits in the file are not made.

**The sample is one profile.** `CrimsonSkiesGame\SavedGames\Zachary\` holds one `Status.dat`, one
`AutoSave.sav`, 20 `Persist.NNN` and 20 `Mission.NNN`. The original cannot be run in this project,
so "change one thing and diff" was not available; the diffs used are across the 20 `Persist` files,
the 9 plane records, the 21 populated mission records and the 143 sections of one `Mission.NNN`,
plus the cross-checks against `cm_sequence.zrd`, the `ZBD` folders and the briefing wav set.

Where the decode stops:

- **`PilotStatus`.** 1448 bytes, all zero in the only profile. No field is claimed.
- **`Mission.NNN` payloads.** The container, the section names and the per-owner lengths are
  decoded; what an `AnimActivation`, `RunningAnim`, `Anim`, `TurretList`, `Weapons`, `GWWorld` or
  `zDEClient` section contains is not.
- **`UIData`'s settings block** (`+0x110` .. `+0x310`). Only the resolution pair and the renderer
  name buffers are identified.
- **The two twelve-byte counter arrays** in a mission-result record.
- **The payout table's values.** The ordnance id vocabulary for the per-pylon cells is decoded
  above; the second dword of each rocket table record still has no reader.
- **`UIData +0x00` and `+0x08`.** Located and typed, not interpreted. `+0x340` is the wingman's
  selected-plane index: the flight check resets its wingman slot from it the way the pilot slot
  reads `+0x33c` ([campaign-screens.md](campaign-screens.md)).
- **No writer.** Nothing here is sufficient to produce a file the original would load, and the
  60 scratch bytes per directory entry mean a byte-identical round trip is not defined.
