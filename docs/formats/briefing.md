# Mission briefing

Part of the [format documentation](README.md). The campaign briefing screen (map, red objective
flags, the parchment objectives note, narration audio, REPLAY BRIEFING / RETURN TO CABIN / GO TO
FLIGHT CHECK) is built from one shared dialog, `zrdr\Briefing.zrd.json` (top-level scope, not a
mission folder). The dialog carries its own fixed chrome plus 24 mission-specific **states**, each
of which is a small script that draws that mission's map art and narrates its objectives in step.
No separate per-mission "flag placement" file exists: the flags, their screen positions, and their
reveal order are all authored inside `Briefing.zrd` itself.

## Contents

- [Conceptual model](#conceptual-model)
- [Dialog chrome: `PRIMITIVES` and `BUTTONS`](#dialog-chrome-primitives-and-buttons)
- [The 24 mission states](#the-24-mission-states)
- [The reveal script vocabulary](#the-reveal-script-vocabulary)
- [State, wav and flag-count census](#state-wav-and-flag-count-census)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence & limits](#evidence--limits)

## Conceptual model

`BRIEFINGDIALOG` has two parts:

- **`PRIMITIVES`**: the fixed screen chrome, identical for every mission, namely the parchment
  objectives-note panel (`OBJECTIVESLIST`) and the three buttons (`BUTTONS`). Neither is
  overridden by any mission state.
- **A named list of `STATES`**: `"default"` plus 24 `"brief_cNN"` blocks, one per campaign
  mission. A state supplies that mission's own `IMAGE_PATH`/`BACKGROUND_IMAGES` (the parchment map
  art), `CURSOR`, and, for every state but `default`, a `SCRIPT`, an ordered list of drawing and
  timing opcodes that is the map's step-by-step reveal, synced to that mission's narration wav.

`map.zrd.json` and `location.zrd.json`, which exist in every mission folder, are **not** this
screen's data. `map.zrd` is the in-flight cockpit map (a `MAP` node: world-to-map transform,
open/close anim names, and a `player_icon`/`DB_NAME player` marker), the overlay the pilot opens
in flight, not the paper map shown at briefing. `location.zrd` is a per-mission list of named world
points with a position and a rotation pair (`Lighthouse`, `Airport_terminal`, etc.); several
missions in different chapters ship byte-identical lists (compare `C1/M02` and `C2/M01`, both
`Airport_terminal`/`Passenger_hangar`/`Crops`/`Coast` at identical coordinates) and one ships
`null` (`C5/M04`), which is template reuse and an unauthored case respectively, not per-mission
red-flag data. Neither file is read by anything in `Briefing.zrd`, `sounds.zrd`'s briefing `SETS`,
or the per-mission `objectives.zrd`; the map and flags the briefing screen shows come entirely
from the shared dialog's own per-state `SCRIPT`.

## Dialog chrome: `PRIMITIVES` and `BUTTONS`

`OBJECTIVESLIST` (the parchment note, upper-left in the reference screenshot):

| Element | Fields |
|---|---|
| `BACKGROUND` | `POSITION [0, 295]`, `BITMAP "parchment"` |
| `TITLE` | `TEXT "MSG_BRF_DLG_OBJECTIVES"`, `FONT "ObjListTitle"`, `POSITION [35, 315]` |
| `LIST` | `FONT "ObjList"`, `POSITION [35, 335]`, `WORDWRAP [185, 240]`, `SPACING [5]` |

`BUTTONS` (bottom edge, all sharing bitmap `"brief_button1"` with a normal/rollover/activate label
triad):

| Button | Position | Label message |
|---|---|---|
| `REPLAY` | `[197, 560]` | `MSG_BTN_REPLAY_BRIEFING` |
| `RETURNTOCABIN` | `[397, 560]` | `MSG_BTN_RETURN_TO_CABIN` |
| `FLIGHTCHECK` | `[597, 560]` | `MSG_BTN_GO_TO_FLIGHT_CHECK` |

Each label carries an `offset` (text baseline nudge, `[98,4]`/`[100,4]`/`[98,4]`) and a `font` that
switches between `BtnLabelNormal`, `BtnLabelRollover` and `BtnLabelActivate` per pointer state.
These three positions and the objectives-note position reproduce the reference screenshot's layout:
the three buttons along the bottom edge, the parchment note upper-left, the map filling the rest of
the frame. Font faces themselves (`ObjListTitle`, `ObjList`, `BtnLabelNormal/Rollover/Activate`)
resolve by name against the shared `zrdr\fonts.zrd.json` table (`Andy Bold` / `Trebuchet MS Bold`
faces); that reader is otherwise undocumented and out of this page's scope.

## The 24 mission states

Besides `PRIMITIVES`, `BRIEFINGDIALOG` lists 25 named states: `"default"` and 24
`"brief_c<NN>"` blocks (`brief_c31`, `brief_c12`, `brief_c23`, ... `brief_c84`).

`"default"` is the loading placeholder shown before a mission state is selected:
`BACKGROUND_IMAGES [["loading", 0, 0]]`, `BUTTONS null`, a plain hourglass-style cursor
(`daglove`/`dafinger`), and **no `SCRIPT`**.

Each of the 24 mission states carries:

- `IMAGE_PATH`, the mission's own art search path (per-chapter, e.g.
  `..\data\c1\images` for the `HA` states).
- `BACKGROUND_IMAGES`, one entry, the parchment map bitmap (e.g. `"NW-m1MAP"`).
- `CURSOR`, the same pointer/rollover bitmap pair as `default`.
- `SCRIPT`, the reveal script (next section).

⚠ **The `brief_cNN` state key does not encode "chapter N, mission N".** `brief_c31`'s script plays
narration `briefing_c2m1` (chapter 2, mission 1) and draws `NW-m1MAP` (`NW` is chapter 2's
abbreviation); `brief_c12`'s script plays `briefing_c2m2`. The state names are internal
identifiers assigned in some other order (the same order also appears, key for key, in
`sounds.zrd.json`'s `SETS`: `"brief_c31"` there wraps the same `"briefing_c2m1"` sound file, which
independently confirms the naming is a shared identifier, not a chapter/mission-number encoding).
**A state's real chapter and mission are read from its `SCRIPT`'s `PlaySound` sound name and its
`BACKGROUND_IMAGES` bitmap name, never from the state key itself.**

## The reveal script vocabulary

A mission state's `SCRIPT` is a flat, ordered opcode list. Every opcode observed across all 24
scripts (censused; no others occur):

| Opcode | Arguments | Meaning |
|---|---|---|
| `PlaySound` | `sound`, `vol`, `markers`, `startmarker` | Starts the mission's narration wav (`sound`, e.g. `"briefing_c2m1"`). `markers: "true"` tells the engine to honor the wav's own embedded cue points; see the marker-source note below. |
| `WaitForMarker` | one integer | Blocks the script until playback reaches that numbered cue point in the currently playing wav. |
| `Pict` | id, `bitmap`, `at [x, y]`, `center` | Loads a named picture element at a fixed pixel position on the dialog. |
| `Fade` | id, `start`, `end`, `duration` | Opacity tween from `start` to `end` over `duration` seconds. |
| `Spin` | id, `startrevs`/`revs`, `duration` | Rotation tween, in revolutions, over `duration` seconds (used for a brief "flourish" spin-in on newly placed elements). |
| `Move` | id, `path` (a list of `[x, y]` points), `duration` | Position tween along the given path over `duration` seconds. |
| `Line` | id, `points` (two `[x, y]`), `color` (RGB 0-255) | Draws a straight connector line between two points (a route line; observed once per mission at most). |
| `On` / `Off` | id | Show/hide an element. |
| `Objective` | id, `index` | Binds a screen text element (an `OBJPINn`'s caption, `ZEPTEXTn`) to entry `index` (0-based) of the mission's runtime objectives list, the same list the mission's own `objectives.zrd` `IDENTITY [..., MSG_BRF_*]` blocks populate, in their file order (see [objectives.md](objectives.md)). This is the flag-to-text link: the pin's position comes from that flag's own `Pict`/`Move`, and `Objective` is what makes the parchment note's matching line appear alongside it. |
| `Wait` | one float, seconds | A fixed-time pause independent of the narration's markers. |
| `ToBack` | id | Sends an element (observed on `OBJECTIVESLIST`) behind everything else in draw order. |

**The reveal order and its mechanism are fully authored in the data; no `crimson.exe` trace was
needed.** A mission's script is a linear beat sheet: `PlaySound` starts the narration, then the
script alternates `WaitForMarker` (pause for the voice to reach a cue point) with `Pict`/`Fade`/
`Spin`/`Move` calls that bring in map flourishes (an intro icon flying onto the map, a dock or
device appearing), and `Pict`+`Fade`+`Objective`+`On` groups that reveal each objective's flag pin
and its objectives-note line together, one `WaitForMarker` apart. `Fade`/`Spin`/`Move`/`Wait`
durations are authored constants in seconds (`0.5`, `0.75`, `8.0`, and so on) already in the data;
they are not a TUNE gap for C23 to invent.

**⚠ What is still open is where the `WaitForMarker` cue points themselves come from.** `markers:
"true"` in `PlaySound` says the engine reads them off the currently-playing wav (a RIFF `cue`
chunk is the natural place), not off any value in `Briefing.zrd`. Whether this project's
extraction pipeline preserves that chunk on the extracted `soundsh`/`soundsl` wavs is unverified;
check it before wiring `WaitForMarker` to real audio. This is a data-location question, not a
timing-magnitude one, and does not need `crimson.exe`. The A8 capture (one C1 mission's briefing
in motion) is still the right fidelity reference for how each beat looks and reads, per the
existing plan.

## State, wav and flag-count census

The state to narration-wav pairing is exact and total: **every one of the 24 mission states plays
exactly one narration wav, and no wav is shared or skipped.** Chapter abbreviations, from the
narration filenames: C1 `HA`, C2 `NW`, C3 `HW`, C4 `RM`, C5 `MH`.

| State | Narration (`briefing_c<N>m<M>`) | Wav file | Map bitmap | Flags (`Objective` count) |
|---|---|---|---|---|
| `brief_c61` | c1m1 | `c1-HA-m1_briefing.wav` | `HA-m1MAP` | 4 |
| `brief_c62` | c1m2 | `c1-HA-m2_briefing.wav` | `HA-m2MAP` | 3 |
| `brief_c63` | c1m3 | `c1-HA-m3_briefing.wav` | `HA-m2MAP` (reused) | 2 |
| `brief_c64` | c1m4 | `c1-HA-m4_briefing.wav` | `HA-m1MAP` (reused) | 2 |
| `brief_c65` | c1m5 | `c1-HA-m5_briefing.wav` | `HA-m1MAP` (reused) | 4 |
| `brief_c31` | c2m1 | `c2-NW-m1_briefing.wav` | `NW-m1MAP` | 4 |
| `brief_c12` | c2m2 | `c2-NW-m2_briefing.wav` | `NW-m2MAP` | 4 |
| `brief_c23` | c2m3 | `c2-NW-m3_briefing.wav` | `NW-m3MAP` | 5 |
| `brief_c14` | c2m4 | `c2-NW-m4_briefing.wav` | `NW-m2MAP` (reused) | 3 |
| `brief_c15` | c2m5 | `c2-NW-m5_briefing.wav` | `NW-m5MAP` | 2 |
| `brief_c42` | c3m1 | `c3-HW-m1_briefing.wav` | `HW-m1MAP` | 3 |
| `brief_c41` | c3m2 | `c3-HW-m2_briefing.wav` | `HW-m1MAP` (reused) | 3 |
| `brief_c43` | c3m3 | `c3-HW-m3_briefing.wav` | `HW-m1MAP` (reused) | 3 |
| `brief_c54` | c3m4 | `c3-HW-m4_briefing.wav` | `HW-m4MAP` | 4 |
| `brief_c45` | c3m5 | `c3-HW-m5_briefing.wav` | `HW-m5MAP` | 5 |
| `brief_c71` | c4m1 | `c4-RM-m1_briefing.wav` | `RM-m1map` | 3 |
| `brief_c72` | c4m2 | `c4-RM-m2_briefing.wav` | `RM-m1map` (reused) | 2 |
| `brief_c73` | c4m3 | `c4-RM-m3_briefing.wav` | `RM-m1map` (reused) | 5 |
| `brief_c74` | c4m4 | `c4-RM-m4_briefing.wav` | `RM-m1map` (reused) | 5 |
| `brief_c75` | c4m5 | `c4-RM-m5_briefing.wav` | `RM-m5map` | 4 |
| `brief_c81` | c5m1 | `c5-MH-m1_briefing.wav` | `MH-m1map` | 5 |
| `brief_c82` | c5m2 | `c5-MH-m2_briefing.wav` | `MH-m1map` (reused) | 2 |
| `brief_c83` | c5m3 | `c5-MH-m3_briefing.wav` | `MH-m1map` (reused) | 1 |
| `brief_c84` | c5m4 | `c5-MH-m4_briefing.wav` | `MH-m1map` (reused) | 1 |

**Map art is reused across missions, and this is shipped, not a gap.** Only 13 map bitmaps exist
in `extracted\rimage\` (`ha-m1map`, `ha-m2map`, `hw-m1map`, `hw-m4map`, `hw-m5map`, `mh-m1map`,
`nw-m1map`, `nw-m2map`, `nw-m3map`, `nw-m5map`, `rm-m1map`, `rm-m5map`, plus `ha-m1treasuremap`)
for 24 states; every state whose table row says "reused" points at one of these instead of a
bitmap of its own, and no missing file exists for any of them. A reader must take the background
bitmap from each state's own `BACKGROUND_IMAGES`, never assume a `<abbrev>-m<N>map` name exists
for every `N`.

## Reader rules and edge cases

- **Never derive a mission's chapter/mission number from the `brief_cNN` state key.** Use the
  `PlaySound` sound name (`briefing_c<N>m<M>`) or the `BACKGROUND_IMAGES` bitmap name instead; both
  agree with each other and with `sounds.zrd`'s `SETS` entry of the same key.
- **The per-mission `objectives.zrd`'s own `MSG_BRF_*` id prefix is not reliable evidence for
  which chapter or mission a file belongs to, and must not be used to cross-reference a mission
  folder against a `Briefing.zrd` state.** Every one of C1's five story missions (`C1/M02`,
  `C1/M04`, `C1/M05`, `C1B/M03`, `C1C/M01`) authors `MSG_BRF_NWM<n>_OBJ*`, and `NW` is chapter 2's
  abbreviation, not chapter 1's. C2's own missions are internally inconsistent with themselves
  too: `C2/M01`'s objectives are tagged `MSG_BRF_HWM2_OBJ*` while `C2/M02`'s are tagged
  `MSG_BRF_HWM1_OBJ*` (`HW` is chapter 3's abbreviation), so the trailing digit is swapped
  between the two folders. These prefixes read as leftover authoring codenames, most likely
  copy-pasted from another mission's file as a starting point and only partly renamed, not a
  parallel numbering scheme worth decoding.
- **`objectives.zrd`'s objective count does not reliably match its state's `Objective` count
  either.** See the open question below; do not use it to pair a folder with a state.
- `map.zrd`/`location.zrd` belong to the in-flight cockpit map, not this screen (see "Conceptual
  model"); do not route briefing map/flag work through them.
- A `Line` element (`TLINE`, seen in `brief_c12`) is rare, at most one per mission; do not assume
  every mission draws a connector line between two of its pins.

## Evidence & limits

Structural claims (dialog chrome, state list, script opcode census, wav pairing, map-art reuse)
come from a full read of `Briefing.zrd.json` (20,470 lines) and `sounds.zrd.json`'s briefing
`SETS`, cross-checked against the wav files present in `extracted\soundsh\` and the map bitmaps in
`extracted\rimage\`. The reference screenshot (`OriginalScreenshots\Campaign Briefing.png`) matches
the decoded button/panel positions.

**Open: which shipped mission folder each state belongs to is not decodable from this data.** The
five `C1`-family folders (`C1/M02`, `C1/M04`, `C1/M05`, `C1B/M03`, `C1C/M01`) match the five `c1m1`
through `c1m5` states in *count* only. Matching them by each folder's own `MSG_BRF` trailing digit
(`C1C/M01` to 1, `C1/M02` to 2, `C1B/M03` to 3, `C1/M04` to 4, `C1/M05` to 5, a clean 1:1) looked
promising until checked against the flag count: the `c1mN` states show 4, 3, 2, 2, 4 flags in that
order, while the folders' own objective counts are 4, 4, 5, 3, 2; only `C1C/M01`'s 4 lines up with
`c1m1`'s 4. C2's folders show the same non-alignment. So neither the `MSG_BRF` digit nor the
objective count is proof of the folder to state binding; it needs the campaign mission tree (this
plan's item A3, or a `crimson.exe`/`CAMPAIGN.SCRIPT` trace of wherever the briefing launch selects
a state) to close. This does not block documenting `Briefing.zrd` itself, but it does block C23
from picking a mission's state by any naming convention: C23 needs an explicit lookup, not a
formula, and A3 is where that lookup is expected to come from.

`fonts.zrd.json` is referenced by name only; its own field layout is undocumented and out of this
page's scope.
