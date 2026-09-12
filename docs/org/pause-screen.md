# The in-flight pause screen, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim below names the function it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side is `extracted/zrdr/escape.zrd.json` and its
Instant Action twin `ia_escape.zrd.json`, whose container format is
[`formats/zrdr.md`](../formats/zrdr.md); the artwork is `extracted/rimage/`. The objectives
parchment and its check are decoded in [`formats/objectives.md`](../formats/objectives.md), which
this page does not repeat. How the 800x600 source artwork meets a modern window is settled in
[`campaign-board.md`](campaign-board.md). The sibling screen, which shares this one's map drawer, is
[`loading-screen.md`](loading-screen.md).

## Function map

| Address | Role |
|---|---|
| `FUN_004a0d20` | The pause-dialog constructor: picks the definition file, resolves the dialog by name, binds `MAP`, `OWNSHIP`, `MYZEP`, `OBJECTIVESLIST` and `MEMENTO`, binds the buttons, runs `ESC_SCRIPT` |
| `0x004a1428` | The pause screen's dialog-name builder, `sprintf` over the two format strings below, and the only caller of `FUN_004a0d20` (at `0x004a14ba`) |
| `0x004a14f0` | The Instant Action mission-type letter table the name builder jumps through |
| `FUN_0045fde0` | The `MAP` control's reader (its vtable slot `+0x78`): `CLIP`, `WORLD`, then the base reader, then the derived screen rectangle |
| `FUN_0045ff20` | The `MAP` control's world-to-screen projection, and the in-window test |
| `FUN_00460000` | Places one icon control through the map's window: project, set position, or turn the icon off |
| `FUN_0041a820` | The profile's memento image name, with its extension stripped |
| `FUN_005c4b30` / `FUN_005c4a70` | Bind a named `PRIMITIVES` entry / a named `BUTTONS` entry into a dialog member |
| `FUN_00470c40` / `FUN_00429280` | The objectives-list control's constructor and its name binding |
| `FUN_0042ab40` | The script interpreter the screen's `ESC_SCRIPT` runs through |
| `FUN_004a15d0` / `FUN_004a1630` / `FUN_004a1620` / `FUN_004a15f0` | RESUME, RESTART, PREFERENCES and QUIT |
| `FUN_004a1650` / `FUN_004a1660` | LOAD GAME and SAVE GAME, which the shipped data never draws |
| `FUN_004639b0` | The is-Instant-Action predicate, which picks the definition file |
| `PTR_FUN_006086b0` | The `MAP` control's vtable, the same class the load screen's map member takes |

⚠ **The row for `0x004a1428` in [`loading-screen.md`](loading-screen.md)'s function map named the
load screen.** That block is this screen's builder: `0x0062950c` (`loading_i%d%c`) and `0x0062951c`
(`loading_c%d%d`) are read only from `0x004a1459` and `0x004a1472` inside it, and it calls
`FUN_004a0d20`. The load screen's builder is `0x004a1eb0`, which reads its own copies of the same
three format strings at `0x00629648`, `0x00629658` and `0x00629668` and calls `FUN_004a1910`.

## The pause screen and the load screen are one drawer with two bindings

`FUN_004a0d20` and `FUN_004a1910` are separate constructors that reach the same classes through the
same binders. What they share:

- the definition-file opener `FUN_00579c60` and the by-name entry lookup `FUN_0057a090`, with a
  lookup that misses retrying the literal `"default"` entry;
- the dialog loader `FUN_005c4250` (background images and cursor) and the primitive binder
  `FUN_005c4b30`;
- **the same `MAP` control class at the same member offset**, `+0x68f40` of the dialog, with the
  vtable `PTR_FUN_006086b0` whose reader is `FUN_0045fde0`;
- the same objectives-list class (`FUN_00470c40`, 0x43a4 bytes) at the same member, bound from the
  shared `LOADINGDIALOG` block and registered under the name `OBJECTIVESLIST` by `FUN_00429280`;
- the same `MEMENTO` member, filled by the same `FUN_0041a820` name and `FUN_005c51a0` load, and
  set to the same `-1.0` through vtable slot `+0x60`;
- the same script interpreter `FUN_0042ab40` and the same finishing sequence (`FUN_005c4c90`,
  `FUN_005c0d00`, the dialog's own slot `+0x8`, `FUN_005c4c40`, `FUN_0042ad60`).

What differs:

| | pause (`FUN_004a0d20`) | load (`FUN_004a1910`) |
|---|---|---|
| definition file | `ia_escape.zrd`, else `escape.zrd` | `ia_loading.zrd` / `mp_loading.zrd`, else `loading.zrd` |
| the campaign gate | none: `MAP`, `OBJECTIVESLIST` and `MEMENTO` are bound whatever the mode | `FUN_004639a0` must hold, or none of the three is bound |
| `OWNSHIP`, `MYZEP` | bound from the shared block and placed through the map window | not bound |
| buttons | `RESUME_MISSION_BTN`, `RESTART_MISSION_BTN`, `CONFIGURE_BTN`, `MAINMENU_BTN`, plus `LOADGAME_BTN` and `SAVEGAME_BTN` when `DAT_0071bb80` is 1 | `PROGRESS` |
| the script | `ESC_SCRIPT`, else `SCRIPT` | `LOADING_SCRIPT`, else `SCRIPT` |
| after building | returns | pumps `FUN_004a18a0` three times |

So one map drawer serves both screens, and the pause screen is the one that also places things by
world position.

## Which dialog is shown

`FUN_004a0d20` opens `ia_escape.zrd` when `FUN_004639b0` (is-Instant-Action) holds and falls back to
`escape.zrd` when that predicate is false or the file fails to open. Both files ship.

The dialog name is built at `0x004a1428` from the same globals the load screen reads, and there are
only two branches, so **a multiplayer pause takes the Instant Action path's format string**:

| Format string | Address | Used when |
|---|---|---|
| `loading_i%d%c` | `0x0062950c` | `FUN_004639b0` holds: `%d` is the environment number `0x0071c09c`, `%c` the mission-type letter |
| `loading_c%d%d` | `0x0062951c` | otherwise: `%d%d` is `0x0071c09c` then the mission number `0x0071c0a0` |

The letter comes from the jump table at `0x004a14f0`, five entries wide over the Instant Action
mission-type index `0x00718cd8` with an index above 4 falling to the default: `0` to `a`, `1` to `d`,
`2` to `z`, `3` to `d`, `4` to `s`. This agrees entry for entry with the load screen's own copy.

⚠ **The two digits of `loading_c%d%d` are the ZBD world folder and its `M0n` number, not the act and
its position.** `0x0071c09c` is 1 for `C1`, 2 for `C1B`, 3 for `C1C`, 4 for `C2`, 5 for `C2B`, 6 for
`C3`, 7 for `C4` and 8 for `C5`, which is the same pair the save id `campaign * 100 + mission` and
the briefing state `brief_c<NN>` are built from
([`formats/campaign-missions.md`](../formats/campaign-missions.md)). `CM01` (`C3/M01`) is therefore
`loading_c61`, and `loading_c31` is `CM06` (`C1C/M01`). The 24 campaign dialogs in `escape.zrd` are
exactly the 24 the sequence names.

## The map is a source crop, and its window is a world-to-screen map

`FUN_0045fde0` reads the `MAP` primitive in three steps.

1. `CLIP`'s `topleft` and `bottomright` land at the control's `+0xf4`, `+0xf8`, `+0xfc` and `+0x100`
   as **integers**, and `+0x44` is pointed at that rectangle, which is what the base draw crops with.
2. `WORLD`'s `topleft` and `bottomright` land at `+0x104`, `+0x108`, `+0x10c` and `+0x110`, also as
   integers. The reader (`FUN_0057a5b0` into `FUN_0057a2d0`) converts an authored float with `ftol`,
   so **every `WORLD` bound is truncated toward zero**: `-1571.470947` is stored as `-1571`.
3. The base reader `FUN_005c4fd0` takes `POSITION` and `BITMAP`, and then the derived screen
   rectangle is computed from the control's own on-screen position (vtable slots `+0x68` and `+0x6c`)
   plus the clip's size:

   ```
   screenX0 = POSITION.x                      -> +0x114
   screenY0 = POSITION.y                      -> +0x118
   screenX1 = POSITION.x + (CLIP.x1 - CLIP.x0) -> +0x11c
   screenY1 = POSITION.y + (CLIP.y1 - CLIP.y0) -> +0x120
   ```

**`CLIP` is a rectangle in the map bitmap, not on the screen.** The bitmap is drawn so that
`CLIP.topleft` lands on `POSITION`, and the visible map therefore occupies `POSITION` to
`POSITION + CLIP size`. That is the only reading the derived rectangle above supports, and it is what
the reference stills show: every campaign dialog puts the map at `POSITION [16, 19]` with
`CLIP [211, 51]..[784, 551]`, which is the 573x500 chart area of the 800x600 sheet landing in
`loadframe`'s own 573x500 window. The four Manhattan dialogs crop `[211, 30]..[784, 530]` instead,
the same size two rows higher up the sheet.

`FUN_0045ff20` is the projection. Given a world position `p`:

```
u = (p.x  - WORLD.x0) / (WORLD.x1 - WORLD.x0)
v = (-p.z - WORLD.y0) / (WORLD.y1 - WORLD.y0)
screen.x = ftol(screenX0 + (screenX1 - screenX0) * u)
screen.y = ftol(screenY0 + (screenY1 - screenY0) * v)
```

and it answers "on the map" only when `0 <= u <= 1` **and** `0 <= v <= 1` (the two constants
compared against are `0.0` at `0x00603398` and `1.0` at `0x006032e8`). The map's vertical axis is
**negated world Z**, and world Y (altitude) is not read at all, so the chart is a plan view and a
climb moves nothing. Neither `WORLD` bound is required to be the smaller one: the campaign dialogs
author x ascending and y descending, so `v` grows as `-z` shrinks.

`FUN_00460000` is the only caller of the projection. It projects the icon's world position; when the
answer is off the map it calls the icon's vtable slot `+0x64` and the icon **does not draw**;
otherwise it sets the icon's position through slot `+0xc` and finishes with slot `+0x60` at `-1.0`,
the same value the memento takes.

## What the pause screen places by world position

`FUN_004a0d20` binds two icons from the shared `LOADINGDIALOG` block, neither of which carries a
`POSITION`, because both get one at runtime:

- **`OWNSHIP`** (`singledev`, `CENTER [1]`) is the player. Its world position is read from
  `DAT_0071c298 + 0x204` and its rotation from
  `atan2(-*(float *)(DAT_0071c298 + 0x1e0), -*(float *)(DAT_0071c298 + 0x1e8))`, which is the
  player transform's own forward vector, so the icon turns with the nose.
- **`MYZEP`** (`NW-m1wv_icon`, `CENTER [1]`) is the zeppelin named `piratezep`. `FUN_004bd3e0`
  looks that object up by name and takes its transform's translation and its own stored angle;
  failing that, `FUN_004d0280(7, "piratezep")` is tried and the object's position is taken with a
  zero angle; failing both, the icon is turned off. A mission with no `piratezep` therefore draws
  no zeppelin icon.

## The flags are authored art, not a runtime reveal

Which numbered flag stands at each objective point, and which reads `?`, is **per-dialog authored
data in the `ESC_SCRIPT`'s own `Pict` opcode**, and the executable has no part in it.

- The bitmap set is `pin1` to `pin8` for the numbers, except that **`pin6` is the question-mark flag
  and `pin6_1` is the numbered six**. A dialog that wants a concealed point names `pin6`.
- The executable holds no `OBJPIN` string and no `pin%d` format string, so no code path substitutes
  one pin bitmap for another; `FUN_0042ab40` places the name the script gives it.
- `CM01` (`loading_c61`) authors `pin6` at `[506, 149]`, `pin6` at `[396, 254]`, `pin6` at
  `[181, 244]` and `pin4` at `[106, 319]`, which is the three question marks and the one `4` the
  reference stills show. `loading_c31` (`CM06`) authors `pin1` to `pin4` in order, and
  `loading_c43` (`CM13`) authors eight pins including `pin6_1` for its sixth.

So the answer to "which flags read `?` while rows 1 to 3 complete" is that none of them ever
changes: the sheet is a printed chart, and `CM01`'s first three treasure sites are drawn unknown on
it whatever the mission does. A `Pict`'s `center [true]` is the bitmap's middle landing on the
authored point, in both axes; measured against the reference still, `pin6` at `[506, 149]` and
`pin4` at `[106, 319]` each match to the pixel, as do the parchment at `[555, 6]` and the button
strips at their own positions.

## The four buttons, and the two that never draw

`FUN_004a0d20` binds four buttons unconditionally and two more when `DAT_0071bb80` is 1, which is
the same flag that distinguishes a campaign session. Each is its own one-off control class whose
vtable slot `+0x30` is the click handler; every handler opens with `FUN_005c2b90`, the button sound.

| Widget | Label | Handler | Does |
|---|---|---|---|
| `RESUME_MISSION_BTN` | `MSG_BTN_RESUME` | `FUN_004a15d0` | pops the screen off the state machine at `DAT_0071d3a0` and nothing else |
| `RESTART_MISSION_BTN` | `MSG_BTN_RESTART` | `FUN_004a1630` | asks the campaign state `DAT_0071d57c` for mission `-1`, the current one, through `FUN_00416f40`, then pops the screen |
| `CONFIGURE_BTN` | `MSG_BTN_PREFERENCES` | `FUN_004a1620` | tail-calls `FUN_00471660`, the preferences dialog |
| `MAINMENU_BTN` | `MSG_BTN_QUIT` | `FUN_004a15f0` | tears the mission down (`FUN_00455200`), runs `FUN_004a0a30`, and asks for `FUN_00419700` in a campaign or `FUN_00419440` otherwise |
| `LOADGAME_BTN` | | `FUN_004a1650` | `FUN_00419860` |
| `SAVEGAME_BTN` | | `FUN_004a1660` | `FUN_00419840` |

⚠ **`LOADGAME_BTN` and `SAVEGAME_BTN` have no entry in the shipped `escape.zrd`.** The shared
`LOADINGDIALOG` `BUTTONS` block defines exactly the first four, so `FUN_005c4a70` finds nothing for
the other two and they are never drawn or hit even in a campaign, which is why the reference stills
show four strips. The executable's own gate is live; the data simply does not answer it.

Each of the four is a three-bitmap strip with three faces: `escape_button1` with
`BtnEscapeNormal`, `escape_button2` with `BtnEscapeRollover` under the pointer and `escape_button3`
with `BtnEscapeActivate` while held, the label centred at the strip's own `offset [66, 7]` over a
132x28 plate. There is no disabled frame, so a strip with nothing behind it is still drawn live.

## The memento is the profile's own image name

`FUN_0041a820` copies the 0x104-byte string at `DAT_0064b684` into a scratch buffer and truncates it
at its last `.`; `FUN_004a0d20` hands the result to `FUN_005c51a0` on the `MEMENTO` control, so the
image the primitive draws is **the profile's memento file name without its extension**, and
`momento_temp` in the data is a placeholder the runtime always replaces. `FUN_004113b0` seeds
`DAT_0064b684` with `MS_P_InitialPinup1.jpg` when a profile is reset, and the 22 names the campaign
can award are a 12-byte-record table at `0x0061af6c` pointing into the string block at `0x0061e12c`.
Which mission awards which is the campaign's business and not this screen's; this screen draws
whatever the profile holds.

`FUN_005c4b30` binds the control from the **dialog's own** `PRIMITIVES`, so the position (`[533, 326]`
in every campaign dialog) is authored while the picture is not. The script then places the shadow
`momento_shad` centred on `[661, 454]` as an ordinary `Pict`.

## An Instant Action pause draws the blackboard, not a map

This is the unfilmed case, and the data settles it. `ia_escape.zrd` opens for an Instant Action
session, and its `loading_i<env><letter>` dialogs carry the load screen's own blackboard: the
`loadframempt2` background, `MP-shotdown` at `[197, 157]`, `MP-crash` at `[197, 307]` and
`mp-dangerzone2` at `[197, 457]`, the four texts `HEAD1`, `HEAD2`, `OBJ5` and `OBJ6`, and an
explicit `Off [OBJECTIVESLIST]`. They carry no `PRIMITIVES` block at all, so no map, no memento and
no parchment, and `FUN_004a0d20`'s unconditional binds find nothing to bind. The four escape strips
come from the shared block and are the only difference from the Instant Action load screen.

`escape.zrd`'s own `loading_i*` dialogs do carry a map and a memento, but they are unreachable:
`ia_escape.zrd` ships and opens, so the fallback is never taken for an Instant Action session.

A multiplayer pause takes the same Instant Action format string (the builder has no multiplayer
branch), so it reads an `loading_i<env><letter>` dialog too, with the multiplayer environment number
never reaching it.

## The script is a beat sheet, and seven dialogs place their pins past a wait

`ESC_SCRIPT` is the same twelve-opcode vocabulary the briefing's own script uses
([`../formats/briefing.md`](../formats/briefing.md)), and a campaign dialog's is `ToBack`, `On`,
`Off`, `Objective`, `Pict`, `Spin` and `Wait`. The dialogs carry no `PlaySound` and no
`WaitForMarker`, so nothing waits on a narration; the only blocking beat is `Wait`, which seven of
the 24 campaign dialogs author at `0.1` seconds, always after the icons it turns off and before the
flag pins.

⚠ **Those seven place their pins after the wait**, so a reader that runs the script once and stops
draws their charts with no flags on them at all: `loading_c15`, `loading_c23`, `loading_c43`,
`loading_c45`, `loading_c54`, `loading_c65` and `loading_c82`. The pattern is always the same, a
`Pict`/`Spin`/`Off` group setting up an icon, the wait, then the `On` that shows it and the pins
after; the original's own redraw pump advances past it within a frame or two of the screen
appearing. Across the 24 dialogs the sequence authors 71 pins, and all 71 stand once the waits have
released.

## Where CSVM differs

`UI/PauseScreens.cs` composes the campaign pause sheet at its authored coordinates and
`Flight/OriginalPauseBoard.cs` hangs it over the flown world in the Original presentation; the
Built-in presentation keeps `Flight/PauseBoard.cs`. `UI/Menu/EscapeDialog.cs` is the `escape.zrd`
reader and `UI/Menu/MissionMap.cs` the one map drawer this screen shares with the campaign briefing,
which is where the world window and the pin placement live.

**The three authored faces meet one of ours.** The extraction ships no menu typeface, so
`BtnEscapeNormal`, `BtnEscapeRollover` and `BtnEscapeActivate` become one face in three palette
inks rather than three faces, which is the same mapping every other composed board takes
([`campaign-board.md`](campaign-board.md)).

**Preferences has no target in flight.** The original opens its own preferences dialog over the
paused mission; this port's options live in the menu presentations and no in-flight leaf stands
behind the strip, so the strip is drawn and its action is unbound.

**Photo mode is not on this screen.** It is this port's own feature and the original authors no
fifth strip, so it stays on the Built-in board rather than being added to the original's four.

**The progress bar in a campaign dialog is not drawn.** Every campaign `escape.zrd` dialog carries a
`PROGRESS` entry at `[90, 548]` copied from its `Loading.zrd` sibling, and `FUN_004a0d20` binds no
`PROGRESS`, so the original never draws it here either.

**The script is run out, not played.** `PauseSheet` advances its reveal until nothing is blocked and
no tween is live, so a `Wait` releases and a `Spin` lands at its end revolutions. The screen is a
still and the original's pump reaches the same state within a frame or two, so nothing here animates
what the original animates once.

**The ownship icon needs a position inside the window.** Nothing draws it where the flown position
falls off the chart, which is the original's own answer and is what a mission's authored spawn hits:
`CM01`'s `PLAYER_INIT` is at world `[-1426, 150, -1813]` and its window's x runs `-10818` to
`-2204`, so the `--menu=pauseboard` door draws no plane. On the reference stills the icon stands
mid-flight instead. The window itself is cross-checked against real mission geometry: `CM01`'s two
literal `TRAVELERS` waypoints, `[-5458, 120, -5390]` and `[-6311, -27, -4611]`, project to
`[372, 237]` and `[315, 282]`, inside the island the chart draws and within 30 pixels of the pin the
first of them belongs to.
