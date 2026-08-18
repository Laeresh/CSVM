# Per-view cameras and base field of view, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-18. The player's first-person camera
positions are additionally read from the decoded plane scene graphs under
`extracted/…/planes/nodes.json`. Every claim below names the function or address it came from.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the
addresses are given so any claim can be re-checked at source.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a remembered behaviour, the
decode wins and the disagreement is a note.

**Where the neighbours live.** The engine's *chase-camera tuning* (distance, catch-up, third-person
eye height and pitch, look-behind, death/crash/flyby placement) is the shared zrdr reader
[`../formats/camparam.md`](../formats/camparam.md) — that page is the chase camera only and carries
no FOV and no first-person data. The two captures that owe death/flyby numbers are
`BL-260`: their geometries remain capture-gated.

## The headline

The original has **exactly two base horizontal field-of-view numbers, 60° and 80°**, and which one
applies is decided **per camera mode** (the live mode at `camera + 0x14c`). The wide 80° belongs to
**exactly one view — the Cockpit view (mode 6)**; every other mode, including the Nose view (mode 7)
and all 3rd-person/chase modes, uses 60°.

Two findings will not be guessed correctly:

- **The Nose view and the Cockpit view are the same camera point.** Both place the camera at the
  plane's **`cockpit_camera` marker** — there is **no separate nose camera marker and no per-mode
  camera offset** anywhere. Mode 7 (nose) is the same physical position as mode 6 (cockpit); it
  differs only in *rendering* (the cockpit interior and some plane nodes are hidden) and in
  *head-look* (locked straight ahead) — and it runs the narrower 60° FOV.
- **The 62° vertical FOV the engine currently assumes does not exist in the binary.** The
  62°-in-radians constant `1.082104` (`63 82 8a 3f`) is absent. The correct base is **60° horizontal
  FOV**, with the single **80°** first-person cockpit exception.

## How the per-view FOV is gated

The live camera mode lives at **`camera + 0x14c`** (a byte of the camera object
`DAT_0064ef78`; the same register that decides first-person placement). The per-frame FOV getter is
`FUN_0042b660`:

| Gate | FOV function | Constant |
|---|---|---|
| `camera + 0x150 != 0` **and** `camera + 0x14c == 6` | `FUN_006024d9` | **80°** horizontal (`1.3962634` rad) |
| any other mode | `FUN_00602508` | **60°** horizontal (`1.0471976` rad) |

`camera + 0x150` is the plane bound to the camera (non-zero in normal play on the player's plane),
so in practice the gate is purely "is the mode 6".

## The camera modes

Every mode `0`–`9` is dispatched by `FUN_0042c5c0` (the camera-object position dispatcher;
the scene-object twin `FUN_0042dc20` mirrors it) to a per-mode placement, then hands to the
shared render-camera `FUN_0042ba70`. Only **modes `6` and `7` are first-person**, and only
**mode `6` is 80°**.

| Mode | Position dispatcher | View | FOV (H) | ~V @ 16:9 | Interior | Head-look |
|---|---|---|---|---|---|---|
| `0` | `FUN_0042c7f0` | **Chase / 3rd-person** (smoothed, `camparam` distance) | 60° | 46.8° | — | — |
| `1` | `FUN_0042ca50` | Chase behind (fixed world angle) | 60° | 46.8° | — | — |
| `2` | `FUN_0042c7f0` +flag | Chase variant (fixed-scale) | 60° | 46.8° | — | — |
| `3` | `FUN_0042cb70` | Chase behind, turn-flippable (+side) | 60° | 46.8° | — | — |
| `4` | `FUN_0042cb70` +flag | Chase, same base, −side offset | 60° | 46.8° | — | — |
| `5` | `FUN_0042ce00` | External — camera at the plane, aimed along the flight-velocity direction | 60° | 46.8° | — | — |
| **`6`** | `FUN_0042d980` | **Cockpit** | **80°** | 64.4° | **drawn** | free-look |
| **`7`** | `FUN_0042d980` | **Nose** | 60° | 46.8° | hidden | locked forward |
| `8` | `FUN_0042cf10` | External — camera at the plane, aim built from the plane's transform | 60° | 46.8° | — | — |
| `9` | `FUN_0042db40` | **Flyby** — camera holds a **world** position, re-aims at the plane, re-sites to a new spot on the `camparam` flyby trigger | 60° | 46.8° | — | — |

The modes pair up around shared handlers: `(0,2)`, `(3,4)` and `(6,7)` share a placement
function and differ only by the boolean flag each passes in — in `(6,7)`'s case the difference is
the cockpit-vs-nose split below. Modes `1`, `5`, `8`, `9` each have a handler of their own. All of
the non-first-person handlers read distance/eye geometry from the `camparam.json` chase table
(`DAT_0064efd0`), so they are all chase/external poses rather than first-person ones.

### Only three views are player-selectable: Chase `0`, Cockpit `6`, Nose `7`

The player-facing view selector falls back to exactly three valid modes. `FUN_0042c210` rejects
any candidate that is not `0`, `6` or `7` (it stores the accepted value in `DAT_0064ef94`);
`FUN_0042c1f0()` just reads that stored value back. The selector key handler `FUN_004414a0` takes
the requested view and, if it is not one of `{0, 6, 7}`, falls back to **6** (cockpit) before
applying it through the setter `FUN_0042c280`. `FUN_00443de0` applies the stored desired view at
game start / on the cycle key. So the selectable set is:

| Mode | Player-selected view |
|---|---|
| `0` | **Chase** (3rd person) |
| `6` | **Cockpit** (interior, 80°, free-look) |
| `7` | **Nose** (no interior, 60°, head locked forward) |

Modes `1`, `2`, `3`, `4`, `5`, `8`, `9` are **not** reachable as player-selected views — they are
internal / context camera modes (e.g. other aircraft, cut-scene or context poses), which is why
`FUN_0042c210` rejects them. The controls data in `extracted/messages.json` names the two view
bindings as **"Access Chase View"** (`MSG_LOOK_FLYBY`, the F7 flyby — see the correction below, it
is **not** the following chase) and **"Cycle Cockpit Views"** (`MSG_LOOK_FORWARD` → cycles the 6/7
pair). The command dispatcher `FUN_0047e080` (case `0x3`, **inferred** to be `MSG_LOOK_FLYBY` —
the input keymap isn't in the decoded data, and `0x3` is my best-match reading, not a read from a
config file) forces mode `0` when the current mode is non-zero (a mode-reset), but that tells us
nothing about which camera the F7 key actually shows; your live run settles it — the flyby. See
the correction block that follows.

⚠ **Correction (2026-08 later): "Access Chase View" IS the flyby — a world-fixed, re-siting
camera**, not the ordinary following chase (an earlier draft of this page said otherwise; a live
run of the original with F7 showed the camera hold a fixed world position that the plane flies
through, re-aiming at the centre, then re-site after a beat). That behaviour is mode **9**: its
handlers are `FUN_0042db40` (camera object) and `FUN_0042e1f0` (scene object). `FUN_0042e1f0` only
runs on a re-site — it picks a fresh **world** position from a random azimuth and a random radius
drawn between `camparam.json`'s `flyby_min_radius`/`flyby_max_radius` (table `DAT_0064efd0`, and the
default block's re-site/watch/switch fields), and stashes it in `DAT_0064ef08/0c/10`.
`FUN_0042db40` then, on every frame the re-site trigger is idle, sets the camera to that **held**
position (`FUN_004d2710`) and only **re-aims** at the plane (`FUN_004d2490`) — i.e. a camera anchored
in world space that watches the plane fly past, re-siting (via `FUN_0042e1f0`) when the
watch/intervals/switch-distance fields say so. This is the same "re-siting roadside pass" the
`camparam.json` flyby fields describe (`docs/formats/camparam.md`). The options menu labels the
positions "external" (`MSG_OPT_3RD_PERSON`), "cockpit" (`MSG_OPT_COCKPIT`) and "default view"
(`MSG_OPT_DEF_VIEW`).

### The in-binary strings expose no view-name tokens

Beyond those binding labels, the executable holds **no friendly view-name strings** for the modes
(`Chase`, `Cockpit`, `Nose`, … used as display text). The HUD initializer `FUN_00454e70` reads only
two **layout keys** from the HUD data archive (`hud_v2.zrd`): `POSITION_1ST` (`00624f28`) and
`POSITION_3RD` (`00624f38`), used to place the gauges differently for the first-person and
third-person HUD variants. So the small per-view display names seen in-game come from the
HUD/video-menu **data files**, not the ship binary — but the *selection wiring* above pins which
mode is which player view.

### Modes 6 and 7 are the only first-person views

- Both route through the first-person placement `FUN_0042d980`; `FUN_0042dc20`/`FUN_0042c5c0`
  dispatch themselves, neither uses chase-position math.
- Both set the first-person flag `DAT_009fd17c` via `FUN_004e7100`.
- The mode setter `FUN_0042c280` treats exactly `6` and `7` as the first-person pair (both reach
  `FUN_004e7100` with a set arg; the other modes branch elsewhere).

### Mode 6 = Cockpit (interior rendered)

The decisive signal is a **render gate in the per-frame player draw** `FUN_0049fb00` (its sole
caller is the main game tick `FUN_004a0220`): the **cockpit interior model `cockpit1`
(`DAT_0071c314`) is drawn only when `camera + 0x14c == 6`**. The interior node is shown via
`FUN_004cca30(..., 1)` before the interior draw and hidden again via `FUN_004cca30(..., 0)` after
it. So mode 6 is the interior cockpit view, and it is the only view that draws it. Mode 6 also runs
the wider 80° FOV.

### Mode 7 = Nose (no interior, fixed head)

- The interior is **not** drawn (the `FUN_0049fb00` gate requires mode 6).
- In `FUN_0042d980` the head-look controller `FUN_0042d010` is forced off for mode 7 — the view is
  **fixed straight ahead**; mode 6 allows mouse free-look.
- In the per-view object handle `FUN_0042e5e0`, mode 7 additionally hides the `dontmove`
  (`DAT_0071c30c`) and `markers` (`DAT_0071c310`) scene nodes during the render (and, like mode 6,
  hides the plane's `healthy` body `DAT_0071c308` during the render), leaving an unobstructed
  forward look.

## FOV constants and aspect correction

The two base numbers are stored **in radians as horizontal half-angle constants**, side by side in
a data table at `0060409c` (`60°`, `80°`, `50.0`, `2.5`):

| Constant | Float | Bytes | Degrees (H) |
|---|---|---|---|
| `1.0471976` | half of 60° | `92 0a 86 3f` | **60°** |
| `1.3962634` | half of 80° | (table at `0060409c`) | **80°** |

The `0x3f860a92` 60° literal is also used by the player aim-camera `FUN_0049d940` and the main
tick `FUN_004a0220`; `FUN_0042b570` (frustum/projection setup) carries `0.5235987755982` = 30° =
60°/2. FOV is stored in radians — the animated-in-script loader `FUN_00502da0` converts degrees to
radians via `0.017453292`.

The two FOV functions aspect-correct the stored **horizontal** angle to the stored **vertical**
value:

```
vertical = atan( tan(H/2) · (16:9) / (4:3), 1 )
```

The `(16:9)/(4:3)` ratio is the 2560×1440 over the engine's assumed 4:3, i.e. ≈ 1.7778. Applying it:
60° H → **46.8° V**; 80° H → **64.4° V**. (The projection is built out of the horizontal
half-angle, so the *base* number to store or port is horizontal.)

⚠ **The two `CAMERA_STATE` / `CAMERA_FROM_TO` functions (`FUN_00502da0`, `FUN_00503e70`) are
animated/in-script FOV changes only** (`.ani` `H_FOV`/`V_FOV` events). They are not the base
per-view FOV and must not be wired to the engine's base FOV. The `.ani` cockpit sequence
(`player-gi_1stperson`) carries **no** FOV — it only shows/hides the interior and plane — so the
base FOV is not authored in animation data.

## Where the first-person camera sits

Both modes 6 and 7 run the same placement `FUN_0042d980`:

```
camera_world = plane_world_pos + plane_rotation · (DAT_0071c328, DAT_0071c32c, DAT_0071c330)
```

`(DAT_0071c328, 32c, 330)` is the plane-local offset of its **`cockpit_camera` marker**, read out
by the cockpit loader `FUN_00473480` from the `cockpit_camera` scene node (via `FUN_004d1cc0`),
with a fallback of `DAT_0075d1b8/bc/c0` = `(0,0,0)` (the plane origin) when a plane has no such
node.

⚠ **No wobble is added at placement — the first-person camera inherits it from the plane.**
`FUN_0042d980` reads the plane's raw orientation basis directly and applies zero shake of its own;
the random-walk wobble state is written onto the **camera object** (component blocks via
`FUN_0042c070`, e.g. high_speed block 4 at `camera+0xd4/+0xd8/+0xdc`) and consumed in the render
layer to rock the **plane node's** rendered rotation. So both 6 and 7, being plane-mounted, inherit
the wobble automatically and are not handled differently from each other — see
`docs/formats/shakes.md`. The chase/3rd-person modes read the plane position/attitude but sit
outside the rocking node, which is why `damage_shakes` gives the chase camera its own authored
half.

### Per-plane `cockpit_camera` offsets (local body frame)

Decoded from the plane scene graphs (`extracted/…/planes/nodes.json`). The marker sits on the
fuselage centerline, a bit above the local origin; the exact number is authored per plane. The
current player fighter `player_pfighter` uses `(0, +0.75, −0.2)` — ~0.75 up, ~0.2 aft of the local
origin. Selected values:

| Plane node | `cockpit_camera` translate (x, y, z) |
|---|---|
| `player_pfighter` (default fighter) | `(0, +0.75, −0.2)` |
| (another) | `(0, +0.70, −0.34)` |
| (another) | `(0, +0.83, +0.72)` |
| (another) | `(1.98, +0.75, −1.21)` |
| (another) | `(−1.8, +0.95, −0.44)` |
| (another) | `(0, +2.31, +3.30)` |

### Axis convention (fixed from the default fighter's cockpit sub-nodes)

- **+Z = forward (nose)** — the elevators `rt_elev2`/`lft_elev2` sit at **z = −37** (the tail) and
  the pilot seat `pass_st` at **z = +37.86** (forward).
- **±X = wingspan** — the ailerons sit at **x = ±63**.
- **+Y = up**.

So a cockpit camera at `(0, 0.75, −0.2)` is on the fuselage centerline, 0.75 units above the local
origin and marginally toward the tail. There is **no `nose_camera` node and no per-mode offset** —
mode 7 reuses the exact `cockpit_camera` point.

## What this means for CSVM

| | Original | CSVM today |
|---|---|---|
| Per-view base FOV | two horizontal constants: **60°** base and **80°** only for cockpit mode 6 | a single **62° vertical** assumption (`GameSession.cs`, `PLAN-overcast-match.md:1463`, `docs/org/tracers.md:258`) |
| FOV axis | stored/ported as **horizontal** half-angle, aspect-corrected at runtime | assumed vertical |
| First-person pair | modes 6/7 share the `cockpit_camera` position; differ in interior render, head-look, FOV | `CameraController.cs` has no cockpit/nose/per-view FOV or position split |
| Cockpit interior gate | drawer (`FUN_0049fb00`) draws `cockpit1` only in mode 6 | not represented |
| **Flyby** ("Access Chase View") | world-fixed, re-siting camera (mode `9`): holds a world point, re-aims at the plane, re-sites on `camparam` flyby trigger | not represented (no world-fixed / re-siting camera concept) |
| Camera position | per-plane authored `cockpit_camera` offset, read from the model (`player_pfighter` `(0,0.75,−0.2)`) | not represented |

To place the virtual camera faithfully: read the plane's `cockpit_camera` offset from the model
(no hardcoded 0.75), sit **both** first-person views at it, draw the interior + free-look + 80° for
the cockpit view, hide the interior + lock the head + 60° for the nose view, and use 60° for all
3rd-person modes.

## Not resolved

- **The non-selectable modes (`1`, `2`, `3`, `4`, `5`, `8`) still have no friendly name or
  identity.** The player-view selector (`FUN_0042c210`) rejects all of them, so none is a
  player-selected view; they are internal/context camera poses. Modes `5`/`8` are confirmed
  "external camera at the plane" but their precise triggers/poses are capture-gated to tell apart.
  Mode `9` is now pinned as the **flyby** (world-fixed re-siting camera — see the "Access Chase
  View" note above); the same long-range/fixed re-siting pose likely underlies `5`/`8` too, but
  that remains capture-gated.
- The `markers`/`dontmove` nodes' exact visual role (what mode 7 strips beyond the interior) —
  visible in-game, not traced to a named object.
- The remaining `camparam.json` death and flyby geometries — capture-gated on `BL-260`.
- Which of the 22 `cockpit_camera` offsets corresponds to each named player airframe by display
  name (the node→display map lives in `../formats/markers.md`); only `player_pfighter`'s is
  pinned here.
