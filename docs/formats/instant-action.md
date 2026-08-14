# Instant Action — the configurable mission surface

Part of the [format documentation](README.md). The description of record for what an Instant
Action mission can be configured to: the seven environments and the chapter each names, the four
mission types the UI offers (of five the data defines), the thirteen militias and their aircraft
lists, the wingman/wave/skill ranges, and the ace and wrap-up records. Anyone implementing
`PLAN-instant-action.md`'s waves B onward reads this page instead of re-deriving it.

Sourced from `ASSETS/SCRIPTS/INSTANTACTION.SCRIPT`, `ASSETS/SCRIPTS/IA_WRAPUP.SCRIPT` and
`ASSETS/LAYOUT.CSV` inside `crimson.rof` (see [rof.md](rof.md)), `ui_strings.json`'s `langui`
table (see [strings.md](strings.md)), and the per-chapter `<chapter>/IA1/zrdr/ia.zrd.json`. The
latter's full key census — every field an `ia.json` carries, including the ace's livery keys —
already lives in [spawns.md](spawns.md); this page does not restate it, only what feeds the
**setup UI** and the **wrap-up UI** around that data. Decoded 2026-08-14.

## The screen's controls

`INSTANTACTION.SCRIPT` declares every widget and the engine callback that fills and reads it.
`LAYOUT.CSV`'s last column on a `D` (dropdown) row is the **visible-row count**, which for these
short lists equals the item count — except `ia_d_planep`, see the trap below.

| Widget | Fill / select callback | Rows | Contents |
|---|---|---|---|
| `ia_d_planep` | 2312 / 2313 | 20 | the 11 airframes, plus the player's saved custom planes |
| `ia_d_nwing` | 2314 / 2315 | 6 | 0 to 5 wingmen |
| `ia_d_planew` | 2316 / 2317 | 11 | the wingman aircraft, hidden entirely when the wingman count is 0 |
| `ia_d_misstype` | 2318 / 2319 | 4 | the four mission types |
| `ia_d_environment` | 2320 / 2321 | 7 | the seven environments |
| `ia_d_nenemyN` (N in 0..3) | 2322 | 7 | 0 to 6 enemies, one per wave |
| `ia_d_egroupN` | 2324 | 13 | the militia flying that wave |
| `ia_d_planeeN` | 2330 / 2331 / 2332 / 2333 | 11 | that militia's aircraft. Each wave has its **own** list id, and selecting a militia resets the wave's plane index to 0 (`ZU[BA]`'s handler sets `AV[BA].QG = 0`), which is the script's own statement that the list depends on the militia |
| `ia_d_difficultyN` | 2326 | 4 | three skills in a four-row box |
| `ia_tl_contents` | 2300 / 2302 | 14 visible | the Table of Contents, 19 preset scenarios (`BL-352`, out of scope) |

Two behaviours matter beyond the option sets, both confirmed directly in the script. Selecting
mission type 0 (dogfighting an ace) hides every enemy control — `gui_init`'s per-wave loop tests
`0 == WT` and deactivates `ia_d_nenemyN`/`ia_d_egroupN`/`ia_d_planeeN`/`ia_d_difficultyN`, and the
`20001` mailbox handler (fired on a mission-type change) repeats the same test to re-hide or
re-show them — which is the script's own statement that **dogfighting an ace takes no wave
configuration**. And the enemy rows are paged: mailbox `20002` shows wave 0's row alone on page 1
and waves 1 to 3 on page 2, keyed off which of the `ia_b_up`/`ia_b_down` buttons was pressed.

**⚠ `ia_d_planep`'s 20 is not twenty aircraft.** The script sizes that list from
`callback($$A$$, 1024)` (the player's saved-plane count) `+ 11`, and `gui_continue` re-checks the
same count each frame to decide whether the default selection is index 0 (no custom planes) or
index 11 (the first custom plane). 20 is `LAYOUT.CSV`'s visible-row window for that variable-length
list, not an item count — the only row in this table where the two differ.

## The option strings

From the `langui` table, read out of `extracted/rof/ui_strings.json`.

| Block | Base id | Values |
|---|---|---|
| Environments (`IDS_IA_ENVIRONMENT`) | 3650 | an airfield, the clouds, Hawaii, Manhattan, the ocean, Sky Haven, a movie studio |
| Mission types (`IDS_IA_MISSIONTYPE`) | 3660 | dogfighting an ace, dogfighting a squadron, stunt flying, attacking a zeppelin |
| Militias (`IDS_IA_MILITIAS`) | 3670 | Black Hat, Black Swan, Blake Aviation, British, Fortune Hunter, Hollywood Knight, Hughes Aviation, Medusa, Russian, Sacred Trust, German, Studio Security, Broadway Bomber |
| Skills (`IDS_IA_DIFFICULTY`) | 3695 | novice, veteran, ace |
| Aircraft, plural forms (`IDS_IA_PLANES`) | 3700 | Hoplites, Hellhounds, Balmorals, Bloodhawks, Brigands, Devastators, Firebrands, Furys, Kestrels, Peacemakers, Warhawks |

Every block is contiguous and was walked id-by-id to confirm the count and order above; no gaps.

**The autogyro has two names in the shipped data.** The UI's plural list (id 3700) calls it a
**Hoplite**; `ia.json`'s `enemy_plane`/`player_plane`/`ace_plane` values use the singular vehicle
display names and call it **Autogyro** (see [spawns.md](spawns.md)). A def carries the `ia.json`
spelling; the setup and wrap-up UI render the plural. Treating these as two different aircraft
would make the plane list look like it has twelve entries.

`ground_target` is a fifth mission type every chapter's `ia.json` disallows (see the table below),
which is why the UI offers only four.

## Environment → chapter

Seven strings, eight chapters. `mission_type` and `disallow_missions` are the two `ia.json` keys
that decide what a chapter's Instant Action can host; they are reproduced here (not duplicating
[spawns.md](spawns.md)'s key-meaning census, which does not carry per-chapter values) because
they are direct evidence for the mapping below.

| Environment | Chapter | `mission_type` | `disallow_missions` | Why the chapter |
|---|---|---|---|---|
| an airfield | C1 | dogfight_squadron | ground_target | the Sea Haven airfield nodes (`ap_radiotwr`, `aphngr01.flt`) live only in C1 |
| the clouds | C1C | zeppelin_run | ground_target, stunt_flying | high-altitude spawns (y 1230 to 1677), no `dz*` markers |
| Hawaii | C3 | dogfight_squadron | ground_target | region code `HA` |
| Manhattan | C5 | stunt_flying | ground_target | region code `NY` |
| the ocean | C1B | stunt_flying | ground_target | the coast of natural arches and sea caves (Rock Archway, Mermaid's/Pirate's Tunnel) |
| Sky Haven | C4 | stunt_flying | ground_target | confirmed by the user 2026-08-14; the `RM` Rockies map |
| a movie studio | C2 | stunt_flying | ground_target | the studio backlot landmarks `ramses` and `sghangar` |
| *(none — excluded)* | C2B | zeppelin_run | ground_target, stunt_flying | the only chapter whose `ia.json` omits `player_plane` and `num_wingmen` |

Six of the seven environments are settled by world content; Sky Haven was reached by elimination
(the one environment string left once the other six were placed) and confirmed by the user as C4
on 2026-08-14. C2B is excluded independently by its missing `player_plane`/`num_wingmen` keys, not
only by being the leftover chapter.

**Open — the dropdown's *order* is assumed, not decoded.** The table above lists environments in
the `langui` string-id order (3650 to 3656) and pairs each with its chapter by content, but
nothing in the data states which chapter the *first* dropdown row launches. The cheap instrument
that settles it is flying each chapter's Instant Action from the menu and reading the label
against what is out the window; it needs no further decode.

`num_wingmen` is `3` in all seven chapters that carry it (the eighth, C2B, carries neither
`player_plane` nor `num_wingmen`); the wingman range is 0 to 5 per `ia_d_nwing`'s row count above.

## The thirteen militias and their aircraft

Inverted from [paint.md](paint.md)'s "Patterns are per aircraft" table (measured over the `.BM`
skins each pattern ships in `crimson.rof`), matched to the militia strings above by their pattern
name.

| Militia | Pattern | Aircraft |
|---|---|---|
| Black Hat | `blackhat` | Warhawk, Brigand, Autogyro |
| Black Swan | `blckswan` | Fury |
| Blake Aviation | `blake` | Bloodhawk, Peacemaker |
| British | `british` | Peacemaker, Balmoral |
| Fortune Hunter | `player_fortune` | all eleven |
| Hollywood Knight | `hollywd` | Firebrand |
| Hughes Aviation | `hughes` | Bloodhawk, Kestrel, Fury |
| Medusa | `medusas` | Kestrel, Brigand |
| Russian | `cccp` | Devastator |
| Sacred Trust | `sactrust` | Warhawk, Hellhound |
| German | `german` | Hellhound |
| Studio Security | `studio` | Fury, Autogyro |
| Broadway Bomber | `BROADWAY` | Peacemaker |

`ITSTAXI` is a fourteenth pattern folder and covers the Autogyro, but no militia string names it,
so it is not an Instant Action militia. `BROADWAY` and `ITSTAXI` ship no `paint_pattern` in
`vehicle.json` and therefore have no canonical colours; `PatternLibrary` already offers them with
whatever colours are current, which is the behaviour a Broadway Bomber wave inherits.

**⚠ This reading, not `vehicle.json`'s `paint_pattern` coverage, is the one to build against.**
Under the def reading the largest militia has three aircraft; under this `.BM` pattern-coverage
reading Fortune Hunter covers all eleven, which is what `ia_d_planeeN`'s eleven-row dropdown
requires (`LAYOUT.CSV`, above). The two readings also disagree on Sacred Trust (defs give
Hellhound alone, coverage gives Warhawk and Hellhound).

## The ace

Every chapter's `ia.zrd.json` names one ace in full, not a random draw: `ace_name`, `ace_plane`,
`ace_skill` (always `"ace"`), `ace_stats` (always `[9,9,9,9,9,9,9,9,9]`), `ace_accentID`, and a
complete `ace_pattern`/`ace_colorN`/`ace_decalN` livery — authored in all 8 of 8 chapters, censused
2026-08-14. The full field table, the `PaintScheme` mapping and the `ace_stats` order inference are
already on [spawns.md](spawns.md); this page does not repeat them.

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

## Open

- **The environment dropdown's order** is assumed to be the `langui` string-id order; settled
  cheaply in-engine, not by further decode (see "Environment → chapter" above).
- **"Total Kills" is defined but unwired** in the shipped UI (see "The wrap-up screen" above); G14
  should not build a fifth row for it.
