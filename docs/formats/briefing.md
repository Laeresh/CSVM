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

The reader's root list holds `SHARED_IMAGE_PATH`, `BRIEFINGDIALOG`, and then the 25 states as
further top-level keys:

- **`BRIEFINGDIALOG`** is the fixed screen chrome, identical for every mission, and has exactly two
  keys of its own. **`PRIMITIVES`** holds one element, the parchment objectives-note panel
  (`OBJECTIVESLIST`). **`BUTTONS`** is its sibling, not a member of `PRIMITIVES`, and holds the
  three buttons (`REPLAY`, `RETURNTOCABIN`, `FLIGHTCHECK`). No mission state overrides either.
- **The states are top-level siblings of `BRIEFINGDIALOG`, not a `STATES` list inside it.** There
  is no `STATES` key anywhere in the file: `"default"` and the 24 `"brief_cNN"` blocks sit directly
  in the root list, each as a key followed by its own body. A state supplies that mission's own
  `IMAGE_PATH`/`BACKGROUND_IMAGES` (the parchment map art), `CURSOR`, and, for every state but
  `default`, a `SCRIPT`, an ordered list of drawing and timing opcodes that is the map's
  step-by-step reveal, synced to that mission's narration wav. A reader walks the root list for
  keys matching `brief_c*` rather than descending into the dialog.

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

Both are keys of `BRIEFINGDIALOG`. `PRIMITIVES` carries `OBJECTIVESLIST` alone; `BUTTONS` sits
beside it.

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

The root list carries 25 named states after `BRIEFINGDIALOG`: `"default"` and 24 `"brief_c<NN>"`
blocks (`brief_c31`, `brief_c12`, `brief_c23`, ... `brief_c84`).

`"default"` is the loading placeholder shown before a mission state is selected:
`BACKGROUND_IMAGES [["loading", 0, 0]]`, `BUTTONS null` (the one place a state touches the
chrome's buttons at all), a plain hourglass-style cursor (`daglove`/`dafinger`), and
**no `SCRIPT`**. Having no script is what separates it from a mission state.

Each of the 24 mission states carries:

- `IMAGE_PATH`, the mission's own art search path (per-chapter, e.g.
  `..\data\c1\images` for the `HA` states).
- `BACKGROUND_IMAGES`, one entry, the parchment map bitmap (e.g. `"NW-m1MAP"`).
- `CURSOR`, the same pointer/rollover bitmap pair as `default`.
- `SCRIPT`, the reveal script (next section).

⚠ **The `brief_cNN` state key encodes the ZBD world folder and folder mission, not the story
chapter.** The engine builds the key as `sprintf("brief_c%d%d", campaign, mission)` from the
mission's `cm_sequence.zrd` entry ([campaign-sequence.md](campaign-sequence.md)): the first digit
is the world-folder number (1 = `C1`, 2 = `C1B`, 3 = `C1C`, 4 = `C2`, 5 = `C2B`, 6 = `C3`,
7 = `C4`, 8 = `C5`), the second the folder's `M0n` number. `brief_c31` is `C1C/M01`, the
Northwest act's first mission, which is why its script plays `briefing_c2m1` (`c2` in the wav
naming is the second act) and draws `NW-m1MAP`. Read the key as chapter/mission and every act
but Colorado and Manhattan comes out wrong. The same folder-keyed name appears, key for key, in
`sounds.zrd.json`'s `SETS` wrapping the same sound file.

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

**The `WaitForMarker` cue points are the narration wav's own RIFF `cue ` chunk, and the extraction
preserves it.** Every one of the 24 `*_briefing.wav` files under `extracted\soundsh\` still carries
that chunk, and the counts agree exactly: a state waits on markers `0`..`n-1` for a wav holding
exactly `n` cue points, on 23 of the 24. The one exception uses fewer, not more (`brief_c81` waits
on 9 of `c5-MH-m1`'s 10 points and waits on marker 8 twice). `markers: "true"` in `PlaySound` is
what selects that source; no value in `Briefing.zrd` carries a marker time.

⚠ **A marker number indexes the cue points sorted by sample offset, never by cue id.** The ids run
`1..n` in file order but the offsets do not: 13 of the 24 wavs store their points out of time
order. In `c1-HA-m1_briefing.wav` the ids 1 to 8 sit at sample offsets 983430, 1600830, 1918350,
3144330, 4167450, 635040, 2266740 and 2879730, so marker 0 is id 6 and the reveal's beats come out
backwards if the id is read as the index. Within a point, `dwPosition` and `dwSampleOffset` are
equal in all 24 files, `dwBlockStart` is always 0, and no offset exceeds the decoded sample count.
A marker's time is `dwSampleOffset / sampleRate` against the 44100 Hz the `fmt ` chunk declares;
the sample data itself is MS ADPCM, so the offsets are decoded-sample counts, not byte positions.
The `CAP-42` capture (a C1 briefing in motion) shows `brief_c61`'s script executed literally in
file order with its beats at the sorted offsets, which is the on-screen confirmation of both.

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

- **A mission's state is picked by the `brief_c%d%d` formula over its `cm_sequence.zrd`
  `campaign`/`mission` pair** ([campaign-sequence.md](campaign-sequence.md)); the state's own
  `PlaySound` sound name and `BACKGROUND_IMAGES` bitmap agree with it and with `sounds.zrd`'s
  `SETS` entry of the same key. Do not read the key's digits as story chapter and act position.
- **The per-mission `objectives.zrd`'s own `MSG_BRF_*` id prefix must still not be used to
  cross-reference a mission folder against a `Briefing.zrd` state.** The abbreviation is the act
  name (`NW` on the `C1` folder family is correct: those folders are the Northwest act, not
  "chapter 1"), and the digit equals the folder's `M0n` number in every act except Hollywood,
  where `C2/M01` is tagged `MSG_BRF_HWM2_OBJ*` and `C2/M02` `MSG_BRF_HWM1_OBJ*`, each other's
  digits. No single rule covers all 24 missions, so the prefix is not a binding key.
- **An `Objective id index` opcode indexes the mission's `objectives.zrd` `IDENTITY` entries that
  carry a `MSG_BRF_*` key, ordered by priority ascending, 0-based.** The count is exact and total:
  on all 24 missions the state's `Objective` count equals the length of that list. Two other
  readings are disproven by the data. Counting keyless `IDENTITY` entries as list members puts an
  empty line first on `C5/M04`, whose single bound line is `MSG_BRF_NYM4_OBJ1` ("1) Payback
  time!"), because that mission's priority-1 entry carries no key. Taking the keyed entries in file
  order instead of priority order reads `C3/M02`'s "2) Dock with the PANDORA" before its target of
  opportunity, and scrambles `C2/M03` into "9)", "10)", "1-8)", while priority order reproduces the
  numbering the strings themselves carry. Note this binds an index to a line; it is still not
  a way to pair a folder with a state, which only `cm_sequence` does.
- ⚠ **One `OBJECTIVEn` block may author more than one `IDENTITY`.** `C4/M05`'s `OBJECTIVE23`
  carries both `["PRIMARY", 3, "MSG_BRF_RMM5_OBJ3"]` and `["SECONDARY", 11]`. A reader that keeps
  one `IDENTITY` per block (a key-to-value view collapses duplicates, last one winning) silently
  loses that mission's third note line and leaves it one short of the four its state binds. Collect
  every entry in the block, not the block's entry.
- ⚠ **A reader list is not strictly alternating key/value.** A bare flag (a string with no value
  list after it, `MISSION_TIMER` and several `OBJECTIVEn` blocks in `objectives.zrd`) shifts the
  parity of everything following it, so a walk that steps by two starts reading bodies as keys
  partway through the file. Step by what is actually there: take a pair only when a string is
  followed by a list. This applies to `Briefing.zrd`'s root list and its `SCRIPT` lists as well.
- **`WaitForMarker`'s argument indexes the narration's cue points sorted by sample offset, never
  by cue id.** See the reveal-script section above for the evidence and the field layout.
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

**The folder-to-state binding is the `brief_c%d%d` formula** over the mission's `cm_sequence.zrd`
`campaign`/`mission` pair, traced to an exe `sprintf` and documented with the full 24-row order in
[campaign-sequence.md](campaign-sequence.md). C23 resolves a mission's state through that formula
and takes the narration wav from the chosen state's own `PlaySound`, never by computing a wav name
from the story position (the Hawaii act's wav numbering does not follow play order).

The objectives binding is settled by a census across all 24 missions: the `Objective` count of each
state against the `MSG_BRF_*`-keyed `IDENTITY` entries of the mission its `cm_sequence` entry names,
equal on every one. The two disproven readings each break on a mission named in the reader rules,
and the shipped strings' own numbering is the independent check on the order.

The marker source is settled against the files themselves: the RIFF chunk walk over all 24
`extracted\soundsh\*_briefing.wav` supplies the cue counts, the sample offsets and the field
equalities quoted above, and the per-state `WaitForMarker` census supplies the marker numbers they
are matched against. Nothing there needs `crimson.exe`.

`fonts.zrd.json` is referenced by name only; its own field layout is undocumented and out of this
page's scope.
