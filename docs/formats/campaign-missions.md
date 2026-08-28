# Campaign mission lookup

Part of the [format documentation](README.md). This page is the lookup table between the three
numberings that name the same 24 campaign missions, so that resolving `CM17` to a folder on disk is
one read rather than a decode. The reader that produces it, the mission-id formula, the progression
rule and the per-mission string lookups are in
[campaign-sequence.md](campaign-sequence.md).

## The three numberings

- **`CM01` to `CM24`**, the campaign ordinal, one-based. This is the notation to use in reports,
  backlog entries, commit messages and at the controls. `CM01` is the first mission flown and
  `CM24` the last.
- **`seq`**, 0 to 23, the engine's own story index. It is what `cm_sequence.zrd` is keyed by, what
  the profile's progress counter counts in, and what `--campaign=<profile>:<seq>` takes.
- **`C<n>/M0<n>`**, the storage address: which ZBD world folder and which mission subfolder hold
  the data. Mission data is `zbd\<campaign>\<mission>\zrdr.zbd`, the world is
  `support\<campaign>\<mission>.gw`, and the save id is `campaign * 100 + mission`.

⚠ **`CM` is one-based and `seq` is zero-based, so `CM02` is `seq` 1.** Convert once, at the edge
that reads a human's number, and never carry both conventions in the same function.

⚠ **The folder number is a world folder, not an act, and the folders are not visited in numeric
order.** Hawaii is act 1 but lives under `C3` and carries the 6xx save ids; act 2 (Northwest) is
spread across `C1`, `C1B` and `C1C`, and act 3 (Hollywood) across `C2` and `C2B`. A bare `M02` is
a folder name inside an act, not the second mission of the campaign.

## `CM` to storage address

`Long name` is `IDS_MISSIONLONGNAME + seq` and `Short name` is `IDS_MISSIONSHORTNAME + seq`; the
act prefix shown in the long name is part of the string. `Wing` is the `wingman` flag, which says
whether the flight-check screen offers a wingman row.

| CM | seq | Folder | Save id | Act | Act mission | Briefing state | Narration wav | Long name (3450+seq) | Short name (3480+seq) | Wing |
|---|---:|---|---:|---|---:|---|---|---|---|---|
| `CM01` | 0 | `C3/M01` | 601 | Hawaii | 1 | `brief_c61` | `c1-HA-m1` | Hawaii - The Lost Treasure of Sir Francis Drake | The Lost Treasure | yes |
| `CM02` | 1 | `C3/M05` | 605 | Hawaii | 2 | `brief_c65` | `c1-HA-m5` | Hawaii - The Great British Bomber Heist | The Bomber Heist | no |
| `CM03` | 2 | `C3/M02` | 602 | Hawaii | 3 | `brief_c62` | `c1-HA-m2` | Hawaii - Nathan Zachary & The Secret Invasion | The Secret Invasion | yes |
| `CM04` | 3 | `C3/M03` | 603 | Hawaii | 4 | `brief_c63` | `c1-HA-m3` | Hawaii - Nathan Zachary & The Sinister Sub | The Sinister Sub | yes |
| `CM05` | 4 | `C3/M04` | 604 | Hawaii | 5 | `brief_c64` | `c1-HA-m4` | Hawaii - The Union Jack's Revenge | Union Jack's Revenge | yes |
| `CM06` | 5 | `C1C/M01` | 301 | Northwest | 1 | `brief_c31` | `c2-NW-m1` | Northwest - Nathan Zachary & The Red Menace | The Red Menace | no |
| `CM07` | 6 | `C1/M02` | 102 | Northwest | 2 | `brief_c12` | `c2-NW-m2` | Northwest - Nathan Zachary & The Pilfered Prototype | The Pilfered Prototype | no |
| `CM08` | 7 | `C1B/M03` | 203 | Northwest | 3 | `brief_c23` | `c2-NW-m3` | Northwest - Nathan Zachary & The Petrol Pit | The Petrol Plot | yes |
| `CM09` | 8 | `C1/M04` | 104 | Northwest | 4 | `brief_c14` | `c2-NW-m4` | Northwest - Peril for Paladin Blake | Peril for Blake | yes |
| `CM10` | 9 | `C1/M05` | 105 | Northwest | 5 | `brief_c15` | `c2-NW-m5` | Northwest - Nathan Zachary & Mercy's Errand | Mercy's Errand | yes |
| `CM11` | 10 | `C2/M02` | 402 | Hollywood | 1 | `brief_c42` | `c3-HW-m1` | Hollywood - Nathan Zachary & The Stolen Starlet | The Stolen Starlet | yes |
| `CM12` | 11 | `C2/M01` | 401 | Hollywood | 2 | `brief_c41` | `c3-HW-m2` | Hollywood - The Great Plane Robbery | The Great Plane Robbery | yes |
| `CM13` | 12 | `C2/M03` | 403 | Hollywood | 3 | `brief_c43` | `c3-HW-m3` | Hollywood - Nathan Zachary & The Nefarious Trap | The Nefarious Trap | yes |
| `CM14` | 13 | `C2B/M04` | 504 | Hollywood | 4 | `brief_c54` | `c3-HW-m4` | Hollywood - Nathan Zachary & The Clash of Dreadnaughts | Clash of Dreadnaughts | yes |
| `CM15` | 14 | `C2/M05` | 405 | Hollywood | 5 | `brief_c45` | `c3-HW-m5` | Hollywood - The Fight for the FIGAROA | Fight for the FIGAROA | yes |
| `CM16` | 15 | `C4/M01` | 701 | Colorado | 1 | `brief_c71` | `c4-RM-m1` | Rocky Mountains - Raid on the Rocky Express | Raid on the Rocky Express | no |
| `CM17` | 16 | `C4/M02` | 702 | Colorado | 2 | `brief_c72` | `c4-RM-m2` | Rocky Mountains - Nathan Zachary & The Pirate's Duel | The Pirate's Duel | no |
| `CM18` | 17 | `C4/M03` | 703 | Colorado | 3 | `brief_c73` | `c4-RM-m3` | Rocky Mountains - Deceit at Devil's Horn | Deceit at Devil's Horn | no |
| `CM19` | 18 | `C4/M04` | 704 | Colorado | 4 | `brief_c74` | `c4-RM-m4` | Rocky Mountains - Rescue the Black Swan | Rescue the Black Swan | no |
| `CM20` | 19 | `C4/M05` | 705 | Colorado | 5 | `brief_c75` | `c4-RM-m5` | Rocky Mountains - Nathan Zachary & The Unholy Alliance | The Unholy Alliance | yes |
| `CM21` | 20 | `C5/M01` | 801 | Manhattan | 1 | `brief_c81` | `c5-MH-m1` | Manhattan - Death on the Docks | Death on the Docks | no |
| `CM22` | 21 | `C5/M02` | 802 | Manhattan | 2 | `brief_c82` | `c5-MH-m2` | Manhattan - Nathan Zachary & The Runaway Witness | The Runaway Witness | yes |
| `CM23` | 22 | `C5/M03` | 803 | Manhattan | 3 | `brief_c83` | `c5-MH-m3` | Manhattan - Nathan Zachary & The Criminal Exodus | The Criminal Exodus | yes |
| `CM24` | 23 | `C5/M04` | 804 | Manhattan | 4 | `brief_c84` | `c5-MH-m4` | Manhattan - Battle over Broadway | Battle over Broadway | yes |

The table accounts for every `M0n` folder that ships: `C1` (3), `C1B` (1), `C1C` (1), `C2` (4),
`C2B` (1), `C3` (5), `C4` (5), `C5` (4) sum to 24, each used exactly once. The `IA1` and `MP1`-`MP3`
folders alongside them belong to Instant Action and multiplayer and are not part of this sequence.

## Storage address to `CM`

| Folder | CM | Folder | CM | Folder | CM |
|---|---|---|---|---|---|
| `C1/M02` | `CM07` | `C2/M05` | `CM15` | `C4/M02` | `CM17` |
| `C1/M04` | `CM09` | `C2B/M04` | `CM14` | `C4/M03` | `CM18` |
| `C1/M05` | `CM10` | `C3/M01` | `CM01` | `C4/M04` | `CM19` |
| `C1B/M03` | `CM08` | `C3/M02` | `CM03` | `C4/M05` | `CM20` |
| `C1C/M01` | `CM06` | `C3/M03` | `CM04` | `C5/M01` | `CM21` |
| `C2/M01` | `CM12` | `C3/M04` | `CM05` | `C5/M02` | `CM22` |
| `C2/M02` | `CM11` | `C3/M05` | `CM02` | `C5/M03` | `CM23` |
| `C2/M03` | `CM13` | `C4/M01` | `CM16` | `C5/M04` | `CM24` |

## Naming a mission in a report

Use `CM01` to `CM24`, and give the storage address alongside it the first time a piece of work
names a mission, as `CM01 (C3/M01)`, so the files it points at are one lookup away.

Three collisions this avoids, which is why the prefix is `CM` and not something shorter. A bare
`M02` is the mission folder inside an act, and those folders are not in play order, so `M02` and
the second mission of the campaign are different missions. `C3` is a world folder, not an act. And
a bare number in prose reads as whichever numbering the reader has in mind.

## Evidence & limits

Every column is read from `extracted\zrdr\cm_sequence.zrd.json` and the `crimson.exe` loader that
parses it, cross-checked against the 24 `M0n` folders on disk, the 24 `brief_c<NN>` states in
`Briefing.zrd.json` and the `Persist.NNN` ids in a real save.
[campaign-sequence.md](campaign-sequence.md) carries that evidence in full, along with the record
layout and the traps around deriving an act or a narration filename from a number.
