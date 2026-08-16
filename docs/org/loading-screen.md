# The mission load screen, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-16. Every claim below names the function it
came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side is `extracted/zrdr/Loading.zrd.json`, whose
container format is [`formats/zrdr.md`](../formats/zrdr.md); the artwork is `extracted/rimage/`,
already read by `HudFont` and `ImpactReticle`. Our own load screen is `CSVM/src/UI/LoadBoard.cs`,
whose entry in [`architecture.md`](../architecture.md) carries the plumbing and the traps. This page
is the original's runtime: how it picks a screen, what fills its bar, and how it keeps drawing while
the mission loads.

⚠ **This page is a decode, not a proposal.** Whether CSVM should match this screen at all is an open
question in `BL-409`, not a conclusion of this page: the source artwork is 800x600 and the frame is
painted into the image, so meeting a modern window is a taste call the executable cannot settle.

## Function map

| Address | Role |
|---|---|
| `FUN_004a1910` | Load-dialog constructor: picks the definition file, resolves the dialog by name, binds `PROGRESS`, runs the screen's script block |
| `0x004a1428` | The name builder for Instant Action and campaign, `sprintf` over the format strings below |
| `0x004a1eb0` | The name builder for multiplayer, falling through to the Instant Action branch |
| `FUN_004a2100` | The progress setter: monotonic clamp, store, repaint |
| `0x005c3d40` | The `PROGRESS` control's repaint (its vtable slot `+0x8c`), which computes the fill width |
| `FUN_004a18a0` | The redraw pump, wall-clock throttled |
| `FUN_004a1db0` | The engine-init load steps, the clearest example of the milestone/pump pattern |
| `FUN_00463f40`, `FUN_00464680`, `FUN_00465370` | The mission-load steps carrying the remaining milestones |
| `FUN_004639b0` / `FUN_004639c0` / `FUN_004639a0` | The mode predicates: is-Instant-Action, is-multiplayer, and the one gating the campaign-only widgets |

## Which screen is shown

`FUN_004a1910` chooses its definition file by mode: `ia_loading.zrd` when `FUN_004639b0` holds,
`mp_loading.zrd` when `FUN_004639c0` holds, and `loading.zrd` when neither holds or when the
mode-specific file fails to open. This install ships only `loading.zrd`, which carries all three
families itself, so the fallback is the path actually taken.

The dialog is then looked up by a name built with `sprintf` from one of three format strings, and a
lookup that misses retries with the literal `"default"` entry:

| Format string | Address | Used for |
|---|---|---|
| `loading_i%d%c` | `0x0062950c` | Instant Action |
| `loading_c%d%d` | `0x0062951c` | campaign |
| `loading_m%d%c` | `0x00629648` | multiplayer |

The globals feeding them:

| Global | Meaning |
|---|---|
| `0x0071c09c` | environment number, the `%d` for Instant Action and campaign |
| `0x0071c0a0` | campaign mission number, the second `%d` |
| `0x00718cd8` | Instant Action mission-type index |
| `0x0071c194` | multiplayer game mode, 1-based |
| `0x0071c1a8` | multiplayer environment number |

The mission-type letter comes from a jump table at `0x004a14f0`, five entries wide with an index
above 4 falling to the default: `0` to `a`, `1` to `d`, `2` to `z`, `3` to `d`, `4` to `s`. The
multiplayer table at `0x004a201c` maps the mode minus one: `0` to `d`, `1` to `t`, `2` to `c`,
`3` to `z`. A second copy of the Instant Action table sits at `0x004a202c` for the fall-through
inside the multiplayer builder, and agrees entry for entry.

`FUN_004639a0` gates the campaign-only widgets. When it holds, `FUN_004a1910` additionally binds
`OBJECTIVESLIST` and `MEMENTO`; Instant Action and multiplayer screens carry neither, which is why
their definitions turn `OBJECTIVESLIST` off explicitly.

## The Instant Action screens are all the same screen

`Loading.zrd` defines 81 dialogs. Grouping the 28 `loading_i*` bodies by content yields five
distinct definitions, and they differ only by the mission-type letter: the environment digit selects
nothing (`loading_i6a` is the one exception, and differs from its siblings outside the script block).
Every Instant Action dialog carries the same three photographs at the same positions, `MP-shotdown`
at `197,157`, `MP-crash` at `197,307` and `mp-dangerzone2` at `197,457`, each centred. The pictures
are authored artwork, not a runtime choice and not a capture of the player's own flying.

Multiplayer is the only family whose pictures vary, and they vary by game mode rather than by map:
the `c` dialogs carry `MP-flagcapture`, `MP-flagreturn`, `MP-shotdown` and `MP-crash`; the `z`
dialogs carry `MP-gasbag`, `MP-cannon`, `MP-torpedo` and `MP-zepdown`; `d` and `t` carry the same
three as Instant Action. Campaign dialogs carry no pictures at all, use the `loadframe` background
with the parchment objectives list, and place their bar at `90,548` with `prog_blkload` over
`prog_redload`.

What the Instant Action letter does select is the text:

| Letter | String family | Heading |
|---|---|---|
| `a` | `MSG_BRF_IAS_*` | DOGFIGHT AN ACE. |
| `d` | `MSG_BRF_IAT_*` | SQUADRON |
| `s` | `MSG_BRF_IASF_*` | STUNT FLYING |
| `z` | `MSG_BRF_IAZ_*` | ZEPPELIN RUN |

Each family supplies `HEAD1` (always "INSTANT ACTION"), `HEAD2` (the heading above), and `OBJ1` and
`OBJ2`, the blurb and the win condition.

Instant Action and multiplayer screens use the `loadframempt2` and `loadframempt` backgrounds
respectively, both 800x600, with the bar at `564,546` drawing `prog_red` over `prog_blk`. An
animated propeller sits beside it: a bitmap cycle at `506,549` stepping `prp0` to `prp7`, `prp15` to
`prp22` and `prp30` to `prp37` at **6.0 fps**.

## The progress bar is a hand-authored milestone table

The engine does not measure its own load. `FUN_004a2100` takes a literal fraction and a debug label,
and is called at sixteen fixed points:

| Fraction | Label | Call site |
|---|---|---|
| 0.01 | `gModSetVertexShading` | `004a1ddd` |
| 0.02 | `Initialize global path to search zbd dir` | `004a1e08` |
| 0.04 | `zSndOpen(sounds.zrd)` | `004a1e38` |
| 0.07 | `Load Common Sounds` | `004a1e61` |
| 0.10 | `Begin CZMission::Load` | `004653a4` |
| 0.10 | `After zImgOpen()` | `00464005` |
| 0.20 | `InterpFilename` | `004640ee` |
| 0.30 | `Texture Load` | `00464107` |
| 0.40 | `Update database` | `00464125` |
| 0.50 | `Load Mission Sounds` | `00464702` |
| 0.60 | `Mission InterpFilename()` | `00464782` |
| 0.70 | `anim, objectives, weather, & weapons` | `00464824` |
| 0.71 | `StructsCampaignInit` | `0046484c` |
| 0.72 | `StructsMissionInit` | `00464865` |
| 0.80 | `PlayerOpen()` | `00464888` |
| 0.90 | `Finish LoadMissionData` | `00464960` |

The setter is **monotonic**: a fraction below the stored one is ignored, so no step can drag the bar
backwards, and the duplicated `0.10` is harmless because the comparison admits equality. Nothing
ever sets `1.0`. The highest milestone is `0.90`, and the screen is torn down from there, so a full
bar is never drawn.

The repaint at `0x005c3d40` clamps the fraction to `[0, 1]` and sets the fill to
**`floor(fillBitmapWidth * fraction)`** pixels of the fill bitmap drawn over the base one, left to
right. It is a pixel clip against the bitmap's own width, not a count of lamps, so the boundary cuts
through a lamp part-way. In `prog_red` the six lamps occupy x `37-54`, `66-83`, `95-112`, `124-140`,
`153-169` and `182-198` of a 236-pixel strip, which puts roughly two lamps lit at the `0.40`
milestone and all six lit at `0.90`.

`FUN_004a1910` also stores `8` in the dialog at `+0x69238`, and `FUN_004a2100` computes
`floor(8 * fraction)` from it. That value does not reach the fill width, which comes from the pixel
clip above; its role is unconfirmed and is most likely a gate so the repaint runs only when the
visible bucket changes.

## How the screen keeps drawing during a blocking load

`FUN_004a18a0` is the redraw pump. It is wall-clock throttled: it draws only when
`GetTickCount() * 0.001` exceeds the last draw's timestamp by more than **0.1 seconds**, so the load
screen repaints at most **ten times per second**.

The load code calls it immediately after each milestone. `FUN_004a1db0` is the pattern at its
clearest: do a unit of work, set the fraction, pump, do the next unit, set the fraction, pump. The
engine therefore neither threads the load nor rebuilds it as an incremental state machine. It runs
the load straight through on one thread and yields a rate-limited repaint from inside it.

## Where CSVM differs

`UI/LoadBoard.cs` draws a plain opaque panel in the shared board style, naming the chapter and the
flight, with no artwork, no propeller and no bar. The build is one synchronous block, so nothing can
be drawn during it at all: `Launcher.BeginLaunch` shows the board, lets one frame render, and builds
on the next tick. Closing that gap is `BL-409`, and this page supplies the two pieces it was missing,
namely that the milestone fractions are authored rather than measured, and that the original's own
answer to a blocking load is a throttled pump rather than an incremental build.
