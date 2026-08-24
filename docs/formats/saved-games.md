# Saved games and player profiles

Part of the [format documentation](README.md). This page describes the original's
`CrimsonSkiesGame\SavedGames\<Profile>\` files: the one container format they all share, and what
the campaign profile keeps in it (funds, owned planes with their ammunition and ordnance picks,
per-mission results, and how far through the campaign the player is). It also states what
`Persist.NNN` and `Mission.NNN` actually carry.

The decode stops where the structural questions are answered. It is deliberately not
byte-complete, there is no writer, and importing an original profile is out of scope
(`PLAN-M5-campaign.md` decision 1). [Evidence and limits](#evidence-and-limits) lists every place
the decode stops and why.

## Contents

- [Conceptual model](#conceptual-model)
- [Container layout](#container-layout)
- [Save categories](#save-categories)
- [`Status.dat`: the profile](#statusdat-the-profile)
  - [The `UIData` block](#the-uidata-block)
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
| `+0x314` | pilot name, 32 bytes | `FUN_004113b0` fills it from langui string 500 for a new profile |
| `+0x338` | missions completed, which is also the sequence index of the next mission | raised to the current mission index only inside the primary-objective-complete branch of `FUN_00405ce0`; zeroed for a new profile |
| `+0x33c` | selected plane, an index into the plane array | `FUN_00405ce0` reads the flown plane's name and airframe from this slot |
| `+0x344` | the current memento image file name | `FUN_004113b0` seeds it with `MS_P_InitialPinup1.jpg` |
| `+0x448` | **funds** | see below |
| `+0x44c` | the plane array, 26 slots of 204 bytes | [paint.md](paint.md), "Saved custom planes" |
| `+0x1868` | the mission-result array, indexed from 1 | see below |
| `+0x28d4` | default savegame file name | seeded from langui string 507 |

The pilot-name buffer is not cleared before a shorter name is written into it: the sample profile
reads `Zachary\0achary\0`, the tail of a longer previous name. A reader must stop at the first NUL.

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
204-byte plane record, in two field groups that [paint.md](paint.md) describes only in terms of the
values the hangar's own commit writes:

| Offset | Field |
|---|---|
| `0x88`, `0x8c`, `0x90`, `0x94` | four gun-slot ids, `5` = no gun |
| `0x98`, `0x9c`, `0xa0`, `0xa4` | **per-gun ammunition index**, `4` = no gun |
| `0xa8` .. `0xc4` | **per-pylon ordnance id**, eight cells, four per wing |

The ammunition claim rests on a cross-record observation over the nine planes in the sample
profile, not on offsets. `ammo[i] == 4` holds in exactly the slots where `gun[i] == 5`, in all
nine records with no exception, so the group is per-gun-slot and `4` is its no-gun marker. The
remaining values are `0`, `1`, `2` and `3`, which is exactly the ammunition index range
[loadouts.md](loadouts.md) decodes for the `wep_{caliber+k}` matrix (`slug` 0, `dumdum` 1, `ap` 2,
`magnesium` 3), and they vary per plane and per slot within one plane. The hangar's commit writes
`0` or `4` here, which is the same field seen before the campaign's Ammo Selection screen has
touched it.

The pylon cells vary the same way: values `1`, `2`, `5`, `10` and `11` appear across the nine
records, with `11` on every cell past a wing's hardpoint count. The hangar commit writes `1` or
`11`, so the other values are the campaign's per-pylon ordnance choice. **The id-to-ordnance
mapping is not decoded**, so a CSVM implementation must resolve these against
[loadouts.md](loadouts.md)'s hardpoint stock ids rather than assume the numbering matches.

### The mission-result array

24 records of 168 bytes at `+0x1868`, indexed **from 1**. Index 0 overlaps the plane array's
scratch slot and is never used; the new-profile reset clears exactly 24 x 168 bytes starting at
record 1. 24 is also the number of campaign missions (see below).

Each record is two halves of `0x54` bytes: the most recent attempt at `+0x00`, and the merged
best/cumulative result at `+0x54`. `FUN_00405ce0` performs the merge on mission completion, and
each field's merge rule is what identifies it:

| Offset | Best at | Merge | Field |
|---|---|---|---|
| `+0x00` | `+0x54` | bitwise OR | completed-objective mask; bit 0 is the primary objective and gates the whole merge |
| `+0x04` | `+0x58` | keep the smaller, ignoring 0 | mission time in milliseconds (unit inferred from magnitude; the merge proves it is a lower-is-better measure) |
| `+0x08` | `+0x5c` | per-index maximum | twelve single-byte counters |
| `+0x14` | `+0x68` | per-index maximum | twelve more single-byte counters |
| `+0x20` | `+0x74` | keep the pair with the larger second/first ratio | a `ushort` pair, shots and hits |
| `+0x28` | `+0x7c` | accumulate | money paid for this mission, the same amount added to funds |
| `+0x2c` | `+0x80` | copied when the attempt completed more objectives | airframe id of the plane flown |
| `+0x30` | `+0x84` | copied with the airframe id | name of the plane flown, 36 bytes |

What the two twelve-byte counter arrays count is not decoded.

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

**The per-section payloads are not decoded.** Only the container, the section names and the
per-owner section lengths are established here.

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
- **The ordnance id vocabulary** for the per-pylon cells, and the payout table's values.
- **`UIData +0x00` and `+0x08`.** Located and typed, not interpreted. `+0x340` is the wingman's
  selected-plane index: the flight check resets its wingman slot from it the way the pilot slot
  reads `+0x33c` ([campaign-screens.md](campaign-screens.md)).
- **No writer.** Nothing here is sufficient to produce a file the original would load, and the
  60 scratch bytes per directory entry mean a byte-identical round trip is not defined.
