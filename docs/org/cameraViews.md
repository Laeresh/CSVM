# Per-view cameras and base field of view, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-18. The player's first-person camera
positions are additionally read from the decoded plane scene graphs under
`extracted/…/planes/nodes.json`. Every claim below names the function or address it came from.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the
addresses are given so any claim can be re-checked at source.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a remembered behaviour, the
decode wins and the disagreement is a note.

**Where the neighbours live.** The engine's *external-camera tuning* (distance, catch-up, third-person
eye height and pitch, look-behind, death/crash/flyby placement) is the shared zrdr reader
[`../formats/camparam.md`](../formats/camparam.md) — that page carries the field rules but
no FOV and no first-person data. The death and flyby geometry and lifecycle are decoded under
`BL-260`; their field-level formulas live on the camparam page.

## The headline

The original has **exactly two base horizontal field-of-view numbers, 60° and 80°**, and which one
applies is decided **per camera mode** (the live mode at `camera + 0x14c`). The wide 80° belongs to
**exactly one view — the Cockpit view (mode 6)**; every other mode, including the Nose view (mode 7)
and all 3rd-person/chase modes, uses 60°.

Two findings will not be guessed correctly:

- **The Nose view and the Cockpit view are the same camera point.** Both place the camera at the
  plane's **`cockpit_camera` marker** — there is **no separate nose camera marker and no per-mode
  camera offset** anywhere. Mode 7 (nose) is the same physical position as mode 6 (cockpit); it
  differs in *rendering* (the cockpit interior and some plane nodes are hidden), runs player
  head-look without the Cockpit-only autohead, and uses the narrower 60° FOV.
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
| `0` | `FUN_0042c7f0` | **Chase / 3rd-person** (smoothed, `camparam` distance) | 60° | 46.8° | — | look-around, floor `−π/2` |
| `1` | `FUN_0042ca50` | Chase behind (fixed world angle) | 60° | 46.8° | — | — |
| `2` | `FUN_0042c7f0` +flag | Chase variant (fixed-scale) | 60° | 46.8° | — | — |
| `3` | `FUN_0042cb70` | Chase behind, turn-flippable (+side) | 60° | 46.8° | — | — |
| `4` | `FUN_0042cb70` +flag | Chase, same base, −side offset | 60° | 46.8° | — | — |
| `5` | `FUN_0042ce00` | External — camera at the plane, aimed along the flight-velocity direction | 60° | 46.8° | — | — |
| **`6`** | `FUN_0042d980` | **Cockpit** | **80°** | 64.4° | **drawn** | look-around + autohead, floor `0` |
| **`7`** | `FUN_0042d980` | **Nose** | 60° | 46.8° | hidden | look-around, floor `0` (autohead off) |
| `8` | `FUN_0042cf10` | **Death** — one fixed world point chosen on callback event `0x0f`, continuously aimed at the destroyed player | 60° | 46.8° | — | — |
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
| `7` | **Nose** (no interior, 60°, player head-look with autohead off) |

Modes `1`, `2`, `3`, `4`, `5`, `8`, `9` are **not** accepted by the player's three-view cycle —
they are internal or context camera modes, which is why `FUN_0042c210` rejects them. Mode 9 has a
separate direct input path: the alive-player F7 handler enters it at `00489430`–`00489447`. The
controls data names F7 **"Access Chase View"** (`MSG_LOOK_FLYBY`) and the cycle binding **"Cycle
Cockpit Views"** (`MSG_LOOK_FORWARD`), which walks Cockpit → Nose → Chase.

**"Access Chase View" is the flyby — a world-fixed, re-siting camera**, not the ordinary following
chase. F7 enters mode **9** through the alive-player input path at `00489430`–`00489447`. Its
handlers are `FUN_0042db40` (per-frame hold/aim/transition) and `FUN_0042e1f0` (re-site).
`FUN_0042e1f0` chooses a fresh world position from the authored random radius and
velocity-projected longitudinal ranges, applies the shared static-camera world-collision clearance, and
stashes it in `DAT_0064ef08/0c/10`. `FUN_0042db40` holds that position and re-aims at the aircraft
each frame. Once the randomized watch deadline has passed, exceeding the randomized switch distance
requests a re-site on the next frame; every re-site redraws the placement, watch, and threshold.
The exact field formulas and provenance are in [`camparam.md`](../formats/camparam.md). The options menu labels the
positions "external" (`MSG_OPT_3RD_PERSON`), "cockpit" (`MSG_OPT_COCKPIT`) and "default view"
(`MSG_OPT_DEF_VIEW`).

### Mode 8 is the death camera

Both death-effect callback paths consume event `0x0f`, require the armed entity to be the player,
and enter mode 8 (`00470912`–`0047093c`, `0048072a`–`00480794`). `FUN_0042e0b0` chooses one fixed
world point from the `death_*` fields when the mode is entered. `FUN_0042cf10` then holds that point
and re-aims at the destroyed player every frame; there is no periodic re-frame. Reset/respawn leaves
mode 8 for mode 6 (`004804de`–`00480508`). The friendly semantic name of callback event `0x0f`
remains unknown; its gate and effect do not.

⚠ **This label collides with CSVM's own numpad chase look-around key set.** `PLAN-cockpit-view.md`
(E41) found the chase camera runs the same head-look controller decoded below through
`FUN_0042c7f0`, driven by the same numpad snap cluster and menu-labelled `F9`-`F12` **External
Camera** keys — and the menu's `F7` **Access Chase View** binding is exactly this section's
"Access Chase View", i.e. the flyby (mode 9), not the look-around. CSVM's `docs/controls.md`
already leaves `F7` unbound in the flight scheme for this reason. Filed as `BL-435`.

### The in-binary strings expose no view-name tokens

Beyond those binding labels, the executable holds **no friendly view-name strings** for the modes
(`Chase`, `Cockpit`, `Nose`, … used as display text). The HUD initializer `FUN_00454e70` reads only
two keys from the HUD data archive (`hud_v2.zrd`): `POSITION_1ST` (`00624f28`) and
`POSITION_3RD` (`00624f38`). ⚠ **Despite the names, these are not a per-view gauge layout.**
`FUN_00454e70` reads them into a text widget built per section (`AIR_SPEED`, `ALTIMETER`, `GUNS`,
`MISSILES`, `HEALTH`, `NITRO`), the six values share one x at 0.02 spacing in y, and the column is
written only under `DAT_00624df0`: a debug text readout, not dial placement
(`docs/formats/hud.md`, "Cockpit gauges"). So the small per-view display names seen in-game come
from the HUD/video-menu **data files**, not the ship binary — but the *selection wiring* above pins
which mode is which player view.

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

### Mode 7 = Nose (no interior, autohead off)

- The interior is **not** drawn (the `FUN_0049fb00` gate requires mode 6).
- `FUN_0042d980` calls the head-look controller `FUN_0042d010` **unconditionally in both
  first-person modes** — mode 7 does not disable head-look. What mode 7 gates off is only the
  **autohead** (idle velocity-follow) branch inside that controller; player-driven look (snap,
  free-look, center key) is identical in Cockpit and Nose. A third caller runs the same controller
  for the chase camera: `FUN_0042c7f0` calls `FUN_0042d010(0xbfc90fdb, 0)` — the literal
  `0xbfc90fdb` is `−π/2`, so chase gets a full elevation range where first person is floored at
  level (0). Decoded fully, with the states/constants/smoothing law, in
  ["Head-look controller"](#head-look-controller) below (`PLAN-cockpit-view.md`).
- In the per-view object handle `FUN_0042e5e0`, mode 7 additionally hides the `dontmove`
  (`DAT_0071c30c`) and `markers` (`DAT_0071c310`) scene nodes during the render (and, like mode 6,
  hides the plane's `healthy` body `DAT_0071c308` during the render), leaving an unobstructed
  forward look. **Contents, now known** (`PLAN-cockpit-view.md` B11, census over all 11 airframes):
  `markers` is 25 nodes, 24 mesh-less reference points (`cockpit_camera`, `target`, `ground_level`,
  `pylon1-8`, `firepoint1-8`, `map`, `exhaust1/2`, `ladder_pos`, `cf_light`) plus one mesh,
  `cockpit_light`, the cockpit lamp; `dontmove` is the propeller group (`wing_flare1/2`,
  `staticprop1`, `prop1`, `prop1b`, `nitroprop1`). So mode 7's extra hiding is the prop disc plus
  the cockpit lamp and whatever hangs on the marker nodes (mounted pylon ordnance, muzzle flashes).

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
vertical = 2 · atan( tan(H/2) · (4/3) / liveAspect )
```

The multiplier is the engine's **assumed 4:3 reference OVER the live aspect** — at the 2560×1440
capture's 16:9 that is `(4/3)/(16/9) ≈ 0.75`, not `(16:9)/(4:3)`: reproducing this page's own
pinned numbers needs the smaller factor, since 46.8°/60° and 64.4°/80° are both below 1. Applying
it: 60° H → **46.8° V**; 80° H → **64.4° V**. (The projection is built out of the horizontal
half-angle, so the *base* number to store or port is horizontal; CSVM's own port of this law is
`CameraController.HorizontalToVerticalFovDeg`, `PLAN-cockpit-view.md` A3.)

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
`FUN_0042c070`, e.g. high_speed block 4 at `camera+0xd4/+0xd8/+0xdc`) and consumed by
`FUN_0042c0e0` (in the render layer) to rock the **plane node's** rendered rotation. So both 6 and
7, being plane-mounted, inherit the wobble automatically and are not handled differently from each
other — see
[`shakes.md`](shakes.md). The chase/3rd-person modes read the plane position/attitude but sit
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

⚠ **This is the ORIGINAL BINARY's own internal convention, not Godot's.** It disagrees with this
codebase's own, far more broadly established one: `docs/formats/gotchas.md` (censused over 6,728
`AT_NODE`/`PUFFER_STATE` uses) puts the extracted frame's nose at **−Z**. `PlaneBuilder`/
`SceneBuilder` never axis-flip a GameZ node's local transform when building the Godot tree, so a
raw translate like `cockpit_camera`'s lands in Godot's frame unswapped and is taken as-is — CSVM
places a Godot camera relative to a Godot-space plane using Godot-space marker data, the same as
every firepoint and pylon, without ever needing to resolve which convention the original binary
used internally (`PLAN-cockpit-view.md` A2).

## Head-look controller

Decoded from `FUN_0042d010` (`PLAN-cockpit-view.md`). Three callers share it: the
first-person placement `FUN_0042d980` (both Cockpit and Nose, elevation floor `0`, autohead flag
cleared for Nose); the chase placement `FUN_0042c7f0` (floor `−π/2` via the literal `0xbfc90fdb`,
autohead off). CSVM's port is `HeadLook` (`src/Flight/HeadLook.cs`), landing the first-person half;
the chase caller is `BL-435`, filed and not yet built.

- **State byte** `DAT_0064ef68`: `0` snap, `1` free-look, `2` padlock (`BL-399`, `BL-432`).
- **Angles.** `DAT_0064ef60` is elevation above level (`0` = level, `π/2` = straight up, clamped to
  `[0, π/2]` in the input paths — the original's head never looks below level in front of the
  clamp; the caller-supplied floor above is a SEPARATE, per-caller bound); `DAT_0064ef64` is
  azimuth, wrapped to `±π` (`FUN_00460ab0`).
- **Snap (state 0).** The POV/keyboard direction becomes an angle in hundredths of degrees;
  azimuth is that angle negated, ×0.01×π/180. Elevation: within 0.1° of forward → straight up
  (`π/2`); within ±0.09° of a 45°/135°/225°/315° diagonal → 45° up (`0.7853982`) — all four
  diagonals, fore and aft alike; any other direction → level. Nine key slots (`0x3a`-`0x42`)
  compose an (x, y) direction; `0x3e` is the center slot (zeroes elevation, azimuth and the zoom
  value in free-look).
- **Free-look (state 1).** Elevation `+= 2·dt·cos(hat angle)`, azimuth `+= 2·dt·sin(hat angle)` per
  frame — a pan rate of **2 rad/s** (~114.6°/s), `DAT_009ad744` the per-frame dt (appears as
  `dt + dt`).
- **Smoothing.** The displayed angles (`DAT_0064ef58/5c`) approach their targets exponentially,
  `shown = target + (shown − target)·e^(−rate·dt)` (`FUN_00460490`; `FUN_00460410` a cubic Taylor
  `e^(−x)` for `x < 0.1`): elevation rate **3.0/s**, azimuth rate **5.0/s** (τ ≈ 0.33 s / 0.20 s).
  The zoom/lean value (`BL-433`) smooths at 1.5/s, moves at `2·dt` on keys `0x43`/`0x44`, clamped
  `[0, 1]`.
- **Autohead** (idle velocity-follow, the gated tail block): with the option byte `DAT_0071dacc`
  set and no look input, the plane's velocity transforms into the plane frame, scales by
  `autohead_turn_time`, caps in magnitude at `autohead_turn_max`, and the head aims along it,
  elevation floored at `autohead_turn_min_pitch` (below the input paths' own `0` floor). All three
  are `player.json` keys (loader `FUN_004735b0`, globals `0071c464/468/46c`): shipped `0.75`,
  `2.86°` (stored ×π/180×2 = 0.0998 rad — the loader doubles the authored value), `−3.0°` (stored
  −0.0524 rad). The flag is `DAT_0071dacc` AND mode ≠ 7 — Nose gates the whole branch off, not just
  the input.
- **Fixed head-pitch offset.** `FUN_0042d980` applies a constant extra rotation of
  **−0.08203 rad = −4.70°** (`0xbda7ff58`) about the elevation axis when building the view basis,
  in both first-person modes.

## What this means for CSVM

| | Original | CSVM today |
|---|---|---|
| Per-view base FOV | two horizontal constants: **60°** base and **80°** only for cockpit mode 6 | Cockpit/Nose land the decoded 60°/80° base (`CameraController.HorizontalToVerticalFovDeg`, `PLAN-cockpit-view.md` A3); every external view still assumes a single **62° vertical** (`GameSession.cs`, `PLAN-overcast-match.md:1463`, `docs/org/tracers.md:258` — the migration is filed, `BL-420`) |
| FOV axis | stored/ported as **horizontal** half-angle, aspect-corrected at runtime | matches for Cockpit/Nose; external views still store/assume vertical |
| First-person pair | modes 6/7 share the `cockpit_camera` position; differ in interior render, head-look, FOV | landed: `PilotViewMode` Cockpit/Nose as camera modes (`PLAN-cockpit-view.md` A1-A3) |
| Cockpit interior gate | drawer (`FUN_0049fb00`) draws `cockpit1` only in mode 6 | landed: `CockpitVisibility` (B11) |
| **Flyby** ("Access Chase View") | world-fixed, re-siting camera (mode `9`): holds a world point, re-aims at the plane, re-sites on `camparam` flyby trigger | not represented (no world-fixed / re-siting camera concept) |
| Camera position | per-plane authored `cockpit_camera` offset, read from the model (`player_pfighter` `(0,0.75,−0.2)`) | landed: `MarkerRig.FindNamedMarker` / `PlaneBuilder.CockpitCameraOffset` (A2) |
| Head-look controller | snap, free-look, center key, autohead — one shared state machine, three callers (first person + chase) | landed for first person as `HeadLook` (C21-C22); the chase caller is not represented (`BL-435`) |

The camera is placed faithfully today: the plane's `cockpit_camera` offset read from the model (no
hardcoded 0.75), both first-person views sitting at it, the interior drawn + head-look + 80° for
Cockpit, the interior hidden + head-look with autohead off + 60° for Nose, and 60° for every
3rd-person mode except the still-unmigrated external 62° global.

## Not resolved

- **The non-selectable modes (`1`, `2`, `3`, `4`, `5`) still have no friendly name or
  identity.** The player-view selector (`FUN_0042c210`) rejects all of them, so none is a
  player-selected view; they are internal/context camera poses. Mode `5` is confirmed as an
  "external camera at the plane", but its precise trigger/pose remains capture-gated. Mode `9` is
  the **flyby** and mode `8` the **death camera**.
- Which of the 22 `cockpit_camera` offsets corresponds to each named player airframe by display
  name (the node→display map lives in `../formats/markers.md`); only `player_pfighter`'s is
  pinned here.
