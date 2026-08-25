# Campaign sequence

Part of the [format documentation](README.md). The single-player campaign is a **flat, ordered list
of 24 missions**. The list lives in one shared reader, `zrdr\cm_sequence.zrd.json`, which binds each
position in the story to the ZBD world folder and mission folder that hold its data. This page
documents that reader, the mission-id scheme derived from it, the progression rule, and the
per-mission lookups (display name, briefing state, narration audio) that every campaign screen
needs.

## Contents

- [Conceptual model](#conceptual-model)
- [`cm_sequence.zrd` record layout](#cm_sequencezrd-record-layout)
- [The 24-mission table](#the-24-mission-table)
- [Mission ids](#mission-ids)
- [Progression: what advances Next Mission](#progression-what-advances-next-mission)
- [Per-mission lookups](#per-mission-lookups)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence & limits](#evidence--limits)

## Conceptual model

Three different numberings describe the same 24 missions, and confusing them is the main hazard on
this surface:

- **`seq`**, 0 to 23, the story order. This is the campaign's own index: `cm_sequence.zrd` is keyed
  by it, the profile's progress counter counts in it, and the mission display-name string ids are
  based at it.
- **The 1-based mission ordinal**, `seq + 1`, 1 to 24. The scrapbook, the per-mission statistics
  records and the `Snap_<mission>_<objective>.png` files count in this, with 0 reserved for the
  pre-career state (`IDS_SB_MISSIONZERO`, "Starting My Career").
- **(`campaign`, `mission`)**, the storage address: which ZBD world folder and which `M0n`
  subfolder hold the mission's data. This is what names files on disk and in the save.

The eight ZBD world folders are numbered 1 to 8 in directory order, and the engine maps that number
to the folder name with a fixed switch: `1 = c1`, `2 = c1b`, `3 = c1c`, `4 = c2`, `5 = c2b`,
`6 = c3`, `7 = c4`, `8 = c5`. A second switch maps the `mission` field to the subfolder name, in
one of three families selected by the session mode: `1..5 = m01..m05` (campaign), `1..5 =
mp1..mp5` (multiplayer), `1 = ia1` (Instant Action). Mission data is then opened as
`zbd\<campaign>\<mission>\zrdr.zbd` and the world as `support\<campaign>\<mission>.gw`.

⚠ **The five story acts do not correspond one-to-one with the eight world folders, and the folders
are not visited in numeric order.** Act 2 (Northwest) is spread across `C1`, `C1B` and `C1C`; act 3
(Hollywood) across `C2` and `C2B`; act 1 (Hawaii) is `C3` alone. `C1B`, `C1C` and `C2B` are terrain
splits of an act's map area, not alternative story paths.

⚠ **The campaign does not branch.** `cm_sequence.zrd` is a flat list with no predicate, no
successor field and no alternates: every entry has exactly one `seq`, and the engine's only
selection rule is "the next `seq`". There is no data anywhere that forks or rejoins the sequence.

## `cm_sequence.zrd` record layout

The reader is a single list of 24 entries. Each entry has six fields; the engine copies them into a
52-byte runtime record indexed by `seq`:

| Field | Type | Runtime offset | Meaning |
|---|---|---|---|
| `seq` | int | (the record index) | Story position, 0 to 23. The engine reads this field rather than trusting list order, defaulting to the list position if absent. |
| `desc` | string | +0x00 (32 bytes) | Authoring label, e.g. `"Hawaii mission 1"`. Not displayed; the player-facing name comes from the string table. |
| `campaign` | int | +0x20 | ZBD world-folder number, 1 to 8 (see the switch above). |
| `mission` | int | +0x24 | Mission subfolder number, 1 to 5 (`m01`..`m05`). |
| `area` | string | +0x2C (as an int) | Act name, one of `HAWAII`, `NORTHWEST`, `HOLLYWOOD`, `COLORADO`, `MANHATTAN`. The engine converts it to an enum: `NORTHWEST = 1`, `HOLLYWOOD = 2`, `HAWAII = 3`, `COLORADO = 4`, `MANHATTAN = 5`. An unrecognised string leaves the field at whatever it held. |
| `wingman` | bool | +0x30 | Whether this mission flies with a wingman. The flight-check screen shows its WINGMAN row, plane and ammunition controls only when this is set. |

Offset +0x28 is not a file field: the engine sets it to 1 for every entry it loads, marking the
record as populated. Two engine helpers walk backwards from a given `seq` over that flag and the
`campaign` field, to find the most recent earlier mission in the same world folder; that is how
cross-mission persistence finds the `Persist.NNN` file it should carry forward.

⚠ **`area` is a separate enum from every other act numbering on this surface.** Its values are not
the act's story order (Hawaii is act 1 but `area` 3) and not the `IDS_MISSIONAREA` string offsets.
Derive an act's story position from `seq / 5`, never from `area`.

## The 24-mission table

`Long name` is `IDS_MISSIONLONGNAME + seq`; the act prefix shown there is part of the string.
`Wing` is the `wingman` flag. `Save id` is the `Persist.NNN` / `Mission.NNN` suffix
(see [Mission ids](#mission-ids)).

| seq | Act | Act mission | Folder | Save id | Briefing state | Narration wav | Long name (3450+seq) | Short name (3480+seq) | Wing |
|---:|---|---:|---|---:|---|---|---|---|---|
| 0 | Hawaii | 1 | `C3/M01` | 601 | `brief_c61` | `c1-HA-m1` | Hawaii - The Lost Treasure of Sir Francis Drake | The Lost Treasure | yes |
| 1 | Hawaii | 2 | `C3/M05` | 605 | `brief_c65` | `c1-HA-m5` | Hawaii - The Great British Bomber Heist | The Bomber Heist | no |
| 2 | Hawaii | 3 | `C3/M02` | 602 | `brief_c62` | `c1-HA-m2` | Hawaii - Nathan Zachary & The Secret Invasion | The Secret Invasion | yes |
| 3 | Hawaii | 4 | `C3/M03` | 603 | `brief_c63` | `c1-HA-m3` | Hawaii - Nathan Zachary & The Sinister Sub | The Sinister Sub | yes |
| 4 | Hawaii | 5 | `C3/M04` | 604 | `brief_c64` | `c1-HA-m4` | Hawaii - The Union Jack's Revenge | Union Jack's Revenge | yes |
| 5 | Northwest | 1 | `C1C/M01` | 301 | `brief_c31` | `c2-NW-m1` | Northwest - Nathan Zachary & The Red Menace | The Red Menace | no |
| 6 | Northwest | 2 | `C1/M02` | 102 | `brief_c12` | `c2-NW-m2` | Northwest - Nathan Zachary & The Pilfered Prototype | The Pilfered Prototype | no |
| 7 | Northwest | 3 | `C1B/M03` | 203 | `brief_c23` | `c2-NW-m3` | Northwest - Nathan Zachary & The Petrol Pit | The Petrol Plot | yes |
| 8 | Northwest | 4 | `C1/M04` | 104 | `brief_c14` | `c2-NW-m4` | Northwest - Peril for Paladin Blake | Peril for Blake | yes |
| 9 | Northwest | 5 | `C1/M05` | 105 | `brief_c15` | `c2-NW-m5` | Northwest - Nathan Zachary & Mercy's Errand | Mercy's Errand | yes |
| 10 | Hollywood | 1 | `C2/M02` | 402 | `brief_c42` | `c3-HW-m1` | Hollywood - Nathan Zachary & The Stolen Starlet | The Stolen Starlet | yes |
| 11 | Hollywood | 2 | `C2/M01` | 401 | `brief_c41` | `c3-HW-m2` | Hollywood - The Great Plane Robbery | The Great Plane Robbery | yes |
| 12 | Hollywood | 3 | `C2/M03` | 403 | `brief_c43` | `c3-HW-m3` | Hollywood - Nathan Zachary & The Nefarious Trap | The Nefarious Trap | yes |
| 13 | Hollywood | 4 | `C2B/M04` | 504 | `brief_c54` | `c3-HW-m4` | Hollywood - Nathan Zachary & The Clash of Dreadnaughts | Clash of Dreadnaughts | yes |
| 14 | Hollywood | 5 | `C2/M05` | 405 | `brief_c45` | `c3-HW-m5` | Hollywood - The Fight for the FIGAROA | Fight for the FIGAROA | yes |
| 15 | Colorado | 1 | `C4/M01` | 701 | `brief_c71` | `c4-RM-m1` | Rocky Mountains - Raid on the Rocky Express | Raid on the Rocky Express | no |
| 16 | Colorado | 2 | `C4/M02` | 702 | `brief_c72` | `c4-RM-m2` | Rocky Mountains - Nathan Zachary & The Pirate's Duel | The Pirate's Duel | no |
| 17 | Colorado | 3 | `C4/M03` | 703 | `brief_c73` | `c4-RM-m3` | Rocky Mountains - Deceit at Devil's Horn | Deceit at Devil's Horn | no |
| 18 | Colorado | 4 | `C4/M04` | 704 | `brief_c74` | `c4-RM-m4` | Rocky Mountains - Rescue the Black Swan | Rescue the Black Swan | no |
| 19 | Colorado | 5 | `C4/M05` | 705 | `brief_c75` | `c4-RM-m5` | Rocky Mountains - Nathan Zachary & The Unholy Alliance | The Unholy Alliance | yes |
| 20 | Manhattan | 1 | `C5/M01` | 801 | `brief_c81` | `c5-MH-m1` | Manhattan - Death on the Docks | Death on the Docks | no |
| 21 | Manhattan | 2 | `C5/M02` | 802 | `brief_c82` | `c5-MH-m2` | Manhattan - Nathan Zachary & The Runaway Witness | The Runaway Witness | yes |
| 22 | Manhattan | 3 | `C5/M03` | 803 | `brief_c83` | `c5-MH-m3` | Manhattan - Nathan Zachary & The Criminal Exodus | The Criminal Exodus | yes |
| 23 | Manhattan | 4 | `C5/M04` | 804 | `brief_c84` | `c5-MH-m4` | Manhattan - Battle over Broadway | Battle over Broadway | yes |

The table accounts for every `M0n` folder that ships: `C1` (3), `C1B` (1), `C1C` (1), `C2` (4),
`C2B` (1), `C3` (5), `C4` (5), `C5` (4) sum to 24, each used exactly once. The `IA1` and `MP1`-`MP3`
folders alongside them belong to Instant Action and multiplayer and are not part of this sequence.

`area` names the act as `COLORADO`, the scrapbook data calls it "Skyhaven", and the player-facing
string (`IDS_MISSIONAREA`, id 1223) calls it "Rocky Mountains". All three name act 4.

## Mission ids

The save files are named by the storage address, not by `seq`:

```
Persist.%1d%02d   Mission.%1d%02d      with (campaign, mission)
```

So the id is `campaign * 100 + mission`: `C3/M01` is 601, `C1B/M03` is 203, `C2B/M04` is 504. The
leading digit is the world-folder number, which is why the ids reach 8xx although there are only
five acts, and why act 1 (Hawaii) carries the 6xx ids. `Mission.NNN` is the in-progress mission
save; `Persist.NNN` is the per-mission persistence log. See `saved-games.md` for their contents.

The briefing dialog's per-mission state key is built from the same pair, as `brief_c%d%d`. That is
the folder-to-state binding [briefing.md](briefing.md) left open: **`brief_c<campaign><mission>`,
formatted from the two `cm_sequence` fields**, with no reference to `seq`, the act, or the
narration filename.

## Naming a mission in a report

**`CM01` to `CM24`, the campaign ordinal, one-based.** `CM01` is the first mission flown and `CM24`
the last. Use it in reports, backlog entries, commit messages and at the controls; give the storage
address alongside it the first time a piece of work names a mission, as `CM01 (C3/M01)`, so the
files it points at are one lookup away.

Three collisions this avoids, which is why the prefix is `CM` and not something shorter. A bare
`M02` is the mission folder inside an act, and those folders are not in play order, so `M02` and the
second mission of the campaign are different missions. `C3` is a world folder, not an act. And a
bare number in prose reads as whichever numbering the reader has in mind.

⚠ **`CM` is one-based and `seq` is zero-based, so `CM02` is `seq` 1.** They differ by one everywhere
they meet: the profile's stored position, `--campaign=<profile>:<seq>`, and the table above are all
`seq`. Convert once, at the edge that reads a human's number, and never carry both conventions in
the same function.

## Progression: what advances Next Mission

The profile stores one number: how many missions have been completed. It is the `seq` of the next
mission, so it runs 0 (nothing flown) to 24 (campaign finished).

- **Advance rule.** When a mission ends, the engine takes that run's completed-objective bitmask.
  If **bit 0 is set** (the mission's first objective, its primary), and the mission's ordinal is
  greater than the stored progress, progress is raised to that ordinal. Completing a mission
  whose objectives leave bit 0 clear records statistics but does not advance the campaign, and
  replaying an already-finished mission cannot lower progress.
- **Next Mission.** The cabin asks for "the next one" with the sentinel `-2`; the engine resolves
  it to `progress + 1` (1-based ordinal), clamping at 24. Selecting an explicit ordinal at or
  below `progress + 1` is accepted and simply replays that mission, which is what Previous
  Missions does. Selecting an ordinal beyond `progress + 1` is the cheat path (the cabin's `idaho`
  keyword reveals a 24-entry mission dropdown that is otherwise deactivated).
- **Campaign complete.** With progress at 24 the cabin clamps its dropdown selection to the last
  entry, and the New Mission button is disabled by a separate availability callback.

`CAMPAIGN.SCRIPT`, the profile screen, carries the same 24 as its own bound: a profile whose
progress reaches 24 is sent to a message box instead of the cabin. In the shipped script the
counter it tests is zeroed immediately beforehand, so the branch is unreachable as written; it is
quoted here only as a second, independent statement that the campaign is 24 missions long.

### Which numbering the reward table uses

The campaign's mission-reward table (10 records, documented in `docs/org/hangar.md`) is keyed by
the **1-based mission ordinal**, not by `seq` and not by the save id: the completion handler
compares each record's mission field against the same variable the scrapbook indexes by. Its
entries therefore land as:

| Reward record | `seq` | Folder |
|---:|---:|---|
| 1 | 0 | `C3/M01` |
| 2 | 1 | `C3/M05` |
| 5 | 4 | `C3/M04` |
| 6 | 5 | `C1C/M01` |
| 7 | 6 | `C1/M02` |
| 12 | 11 | `C2/M01` |
| 13 | 12 | `C2/M03` |
| 17 | 16 | `C4/M02` |
| 19 | 18 | `C4/M04` |
| 24 | 23 | `C5/M04` |

Record 24 landing on the final mission is the independent check that the numbering runs 1 to 24
over the sequence. Each record names an objective bit as well as a mission, and the handler pays it
only when that bit is newly set, so a reward is once per profile even if the mission is replayed.

## Per-mission lookups

Given a `seq`, everything a campaign screen needs resolves without a search:

| Wanted | Source |
|---|---|
| ZBD folder and mission folder | `cm_sequence[seq].campaign` / `.mission`, through the folder-name switches |
| Save-file id | `campaign * 100 + mission` |
| Briefing dialog state | `"brief_c" + campaign + mission` |
| Briefing narration wav | the chosen state's own `PlaySound` name, **not** a formula (see below) |
| Long display name | `langui` id `3450 + seq` (`IDS_MISSIONLONGNAME`) |
| Short display name | `langui` id `3480 + seq` (`IDS_MISSIONSHORTNAME`) |
| Act name | `langui` id `1220 + (seq / 5)` (`IDS_MISSIONAREA`) |
| Wingman present | `cm_sequence[seq].wingman` |
| Scrapbook / snapshot index | `seq + 1` |

## Reader rules and edge cases

- ⚠ **The narration filename's `c<N>m<M>` numbers are not a reliable index.** For 20 of the 24
  missions they happen to equal `(seq / 5) + 1` and `(seq % 5) + 1`, but the Hawaii act breaks it:
  `c1-HA-m5` is the act's **second** mission and `c1-HA-m2` its third. Resolve the wav through the
  briefing state (which is built from `campaign`/`mission`, an exact key), never by computing a
  name from `seq`.
- **The `MSG_BRF_<ABBREV>M<n>_OBJ*` prefixes inside a mission's `objectives.zrd` are mostly, but
  not fully, systematic.** The abbreviation is always the act (`HA`, `NW`, `HW`, `RM`, and `NY`
  for Manhattan, where the narration files use `MH`), never a folder name. The digit equals the
  folder's `M0n` number in every act except Hollywood, where `C2/M01` carries `MSG_BRF_HWM2_OBJ*`
  and `C2/M02` carries `MSG_BRF_HWM1_OBJ*` (each other's digits, which in that act matches the
  play order instead; full census across all 24 missions). Because no single rule covers all 24,
  the prefixes must not be used to pick a briefing state or bind a folder; use `cm_sequence`
  instead.
- **Do not derive the act from the world-folder number.** Folder 3 (`C1C`) is act 2 and folder 6
  (`C3`) is act 1.
- **Plane change is not offered on every mission.** The flight-check screen deactivates CHANGE
  PLANE, and the engine's available-plane count is reduced by one, on **mission ordinals 13 and
  17**, which are `seq` 12 (`C2/M03`, Hollywood 3) and `seq` 16 (`C4/M02`, Colorado 2). Those are
  also the two missions whose reward record awards an aircraft rather than cash, which is the
  reason one plane is held back from the count. The same screen additionally hides CHANGE PLANE
  whenever the player owns fewer than three planes.
- **Wingman rosters are still per-mission data.** The `wingman` flag only says whether the flight
  check offers a wingman row; who flies it comes from the mission's own `aiv.zrd`
  ([ai-rosters.md](ai-rosters.md)).

## Evidence & limits

- The sequence table, the record layout, the `area` enum values and the `wingman` field come from
  `extracted\zrdr\cm_sequence.zrd.json` read in full, and from the `crimson.exe` loader that
  parses it (reached from the literal `cm_sequence.zrd`), which supplies the runtime offsets and
  the `area` string-to-enum conversion.
- The world-folder and mission-folder name switches, the `Persist.%1d%02d` / `Mission.%1d%02d` id
  format and the `brief_c%d%d` state-key format are `crimson.exe` literals with their surrounding
  switch tables; no code is reproduced here.
- The progression rule (objective bit 0, the monotonic raise, the `-2` sentinel and the clamp at
  24) is traced to the mission-completion and mission-selection handlers in `crimson.exe`, and is
  consistent with `PASSENGERCABIN.SCRIPT`'s cabin wiring and `CAMPAIGN.SCRIPT`'s 24-mission check.
- The id set is cross-checked three ways with no orphan on any side: the 24 `M0n` folders on disk,
  the 24 `brief_c<NN>` states in `Briefing.zrd.json`, and the 20 `Persist.NNN`/`Mission.NNN` ids
  in a real save, which are exactly `seq` 0 through 19.
- Act boundaries are independently confirmed by `ASSETS\SCRAPBOOK.CSV`, whose `<mission>_<spread>_
  <item>` keys start a new act at missions 1, 6, 11, 16 and 21, and by the `IDS_MISSIONLONGNAME`
  block's own act prefixes.
- The reward-table numbering is settled by the completion handler comparing the record's mission
  field against the scrapbook's 1-based ordinal, and corroborated by record 24 landing on the last
  mission and by records 13 and 17 being the two aircraft awards that match the flight-check
  plane-change block. The rewards' own contents (amounts, the named aircraft) belong to
  `docs/org/hangar.md`.
- **Not decoded here:** the availability callback that enables or disables the cabin's New Mission
  button beyond the progress counter it must read; the plane-change ambiguity noted above; and the
  runtime mission-statistics arrays the save populates, which belong to `saved-games.md`.
