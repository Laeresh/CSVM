# Cockpit view (BL-080) — the original's two first-person views

**ACTIVE PLAN** (written 2026-08-21). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan delivers the original's two first-person views: **Cockpit (mode 6)** with the interior
model rendered, 80° horizontal FOV and head-look, and **Nose (mode 7)** with the interior hidden,
60° horizontal FOV and the head held forward, both sitting at the per-plane `cockpit_camera`
marker. It also lands the data consumers BL-080 inventories: the `pcdpN` cockpit damage panels and
the `cockpit_engine_sound` swap (which closes `BL-161`). The camera behaviour is built from
[`org/cameraViews.md`](org/cameraViews.md) plus a head-look decode of `FUN_0042d010` /
`FUN_0042d980` done in the session that wrote this plan (constants below, in "What the data
actually ships").

Deliberately out of scope, each becoming a filed `backlog.md` item at close-out (E41): the in-3D
gauge drive (the screen-space `GaugeCluster` stays the HUD), the padlock look state, the zoom/lean
axis, the engine-wide FOV migration from 62° vertical to the decoded 60° horizontal base, and
splitscreen cockpit behaviour. `BL-150` (the numpad fixed-view rebuild) stays its own future plan:
this plan adds camera modes beside the held-key `Views[]` table and does not touch it.

Backlog provenance: `BL-080` and `BL-161` were re-verified still-open in this session against the
code (`CameraController.cs` has no first-person mode; `PlaneBuilder.cs:26` skips `cockpit1`;
`PlaneStats.cs:490` parses `cockpit_engine_sound` into `CockpitEngineSound` with nothing consuming
it). `BL-150` is referenced but not worked here, so it was not re-verified.

## Milestone goal

- Cycling the cockpit-view key toggles Cockpit ↔ Nose; the chase view remains selectable; the
  held-key numpad views keep overriding exactly as today.
- Both first-person views sit at the plane's authored `cockpit_camera` offset read from the model,
  inherit the plane's wobble, and carry the fixed −4.70° head-pitch offset the original applies.
- Cockpit renders the `cockpit1` interior with its `pcdpN` damage panels driven; Nose hides the
  interior and the `markers`/`dontmove` nodes; both hide the own-plane healthy body.
- Cockpit runs 80° horizontal FOV and Nose 60°; head-look (snap, free-look on mouse/right stick,
  center key) is active in both first-person views, elevation floored at level; autohead is
  Cockpit-only.
- `cockpit_engine_sound` swaps onto the engine slot while in either first-person view.

**The external views keep their current 62° vertical FOV and the `Views[]` numpad table keeps its
current poses.** Both are known-wrong against the binary, and both are deliberately untouched:
their fixes (the global FOV migration, `BL-150`) each unsettle screenshot- or capture-judged work
and get their own change.

## Decisions (2026-08-21)

| # | Question | Decision |
|---|---|---|
| 1 | Finish line | **Camera + interior + head-look** — interior rendered and `pcdpN` driven; gauges stay screen-space (`GaugeCluster` untouched); every deferred point gets a backlog item at close-out |
| 2 | `BL-150` sequencing | **Build beside it** — cockpit/nose are camera modes, not `Views[]` rows; the numpad rebuild stays a separate plan; held-key override behaviour preserved |
| 3 | Base FOV | **New modes only** — 80°/60° horizontal for cockpit/nose; the engine-wide 62°V → 60°H migration is a filed backlog item carrying the calibration warning (overcast, tracers) |
| 4 | Head-look composition | **Snap + free-look + autohead + center key land; padlock and the zoom axis deferred** to backlog items; free-look binds to mouse and the right gamepad stick |
| 5 | Splitscreen | **Verify single-player only** — no artificial gate in splitscreen, but per-viewport interior cost and the per-pilot engine-sound swap against `MixGain` get a filed item and their own later judgement |
| 6 | Execution | **Worktree** — camera/audio touches cross files concurrent sessions edit, and B11 can leave the build mid-broken between commits |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The engine's base FOV is 62° vertical | The 62°-in-radians constant is absent from `crimson.exe`; the real base is 60° horizontal with an 80° cockpit exception (`org/cameraViews.md`, "The headline") |
| 2 | "In `FUN_0042d980` the head-look controller `FUN_0042d010` is forced off for mode 7" (`org/cameraViews.md:150-151`), and this plan's first draft carried it as "Nose: head fixed" | Dead per the decompiles: `FUN_0042d980` calls `FUN_0042d010` unconditionally in both first-person modes, gating off only the **autohead** flag for mode 7, and the chase handler `FUN_0042c7f0` calls the same controller with pitch floor **−π/2** (`0xbfc90fdb`) where first person passes 0. Head-look is one shared system: chase (full range), Cockpit and Nose (elevation floored at level); autohead is the only Cockpit-exclusive. Build Nose with head-look; the at-the-controls sitting confirms, since the page's old claim may have been a live impression |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism, with the data that proves it** | A2, A3, C22, D31 | Confirm the trace, then implement. |
| **Traced mechanism, presentation details still open** | A1, B11, B12, C21 | The decode pins the behaviour; bindings, node wiring and gating details are named TODOs. |
| **Leads only** | — | none in this plan. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

**FOV** (`org/cameraViews.md`): two horizontal half-angle constants, 60° (`1.0471976` rad) and 80°
(`1.3962634` rad), table at `0060409c`; the 80° applies only when `camera+0x14c == 6`. Vertical is
derived at runtime: `vertical = atan(tan(H/2) · aspect_ratio_factor)`.

**Camera placement** (`org/cameraViews.md`): both modes place at the plane-local `cockpit_camera`
marker read from the model (`player_pfighter`: `(0, +0.75, −0.2)`; fallback `(0,0,0)`); no separate
nose marker, no per-mode offset; wobble is inherited from the plane node, no camera-side shake.

**Head-look controller** `FUN_0042d010` (decoded 2026-08-21, this plan's session). Two callers:
the first-person placement `FUN_0042d980` passes pitch-floor `0` and the autohead flag (cleared in
mode 7); the chase handler `FUN_0042c7f0` passes pitch-floor **−π/2** (`0xbfc90fdb`) and autohead
off, so the same look system serves the chase view with a full elevation range (out of scope here,
filed by E41):

- The look state lives at `DAT_0064ef68`: `0` snap, `1` free-look, `2` padlock (deferred).
- Angles: `DAT_0064ef60` is elevation above level (0 = level, π/2 = straight up), `DAT_0064ef64`
  azimuth. Elevation is clamped to `[0, π/2]` — the original's head never looks below level.
- **Snap (state 0):** the POV/keyboard direction becomes an angle in hundredths of degrees;
  azimuth = that angle (negated, × 0.01 × π/180). Elevation: angle within 0.1° of forward →
  **straight up** (π/2); within ±0.09° of a 45°/135°/225°/315° diagonal → **45° up**
  (`0.7853982`); any other direction → **level**. Keyboard uses nine key slots (indices
  `0x3a`–`0x42`) composed into an (x, y) direction; `0x3e` is the **center** key (zeroes
  elevation, azimuth and the zoom value in free-look).
- **Free-look (state 1):** elevation += `2·dt·cos(hat angle)`, azimuth += `2·dt·sin(hat angle)`
  per frame — a pan rate of **2 rad/s** (~114.6°/s) whichever way the stick points (`DAT_009ad744`
  is the per-frame dt; it appears as `dt + dt`).
- **Smoothing:** the displayed angles (`DAT_0064ef58/5c`) approach the targets exponentially,
  `shown = target + (shown − target) · e^(−rate·dt)` (`FUN_00460490`; `FUN_00460410` is a cubic
  Taylor `e^(−x)` for x < 0.1). **Elevation rate 3.0/s, azimuth rate 5.0/s** (τ ≈ 0.33 s / 0.20 s).
  The zoom/lean value (deferred) smooths at 1.5/s, moves at `2·dt` on keys `0x43`/`0x44`, clamps
  `[0, 1]`.
- **Autohead** (idle velocity-follow, the gated block at the function's tail): when the flag is set
  and there is no look input, the plane's velocity is transformed into the plane frame, scaled by
  `autohead_turn_time`, magnitude-capped at `autohead_turn_max`, and the head aims along it,
  elevation floored at `autohead_turn_min_pitch`. The three are **`player.json` keys** (loader
  `FUN_004735b0`, globals `0071c464/468/46c`): shipped values `0.75`, `2.86°` (stored ×π/180×2 =
  0.0998 rad), `−3.0°` (`extracted/zrdr/player.zrd.json:35-46`; compiled-in defaults 0.75, 0.1 rad,
  −3°). The flag is an engine option byte (`DAT_0071dacc`) AND mode ≠ 7.
- **Fixed head-pitch offset:** `FUN_0042d980` applies a constant extra rotation of
  **−0.08203 rad = −4.70°** (`0xbda7ff58`) about the same axis as elevation when building the view
  basis, in both modes.

**Rendering gates** (`org/cameraViews.md`): `cockpit1` drawn only in mode 6 (`FUN_0049fb00`);
mode 7 additionally hides `markers` and `dontmove`; both modes hide the plane's healthy body
during the first-person render (`FUN_0042e5e0`).

**What CSVM already has:** the `cockpit1`/`cockpit2` subtrees ship in planes.zbd and
`PlaneBuilder.cs:26` skips them by name; `pcdpN` nodes are recognised (`PlaneBuilder.cs:152-163`)
and skipped by `DamageVisuals` (`DamageVisuals.cs:365`); the `*_damage_green/yellow/red` injure
thresholds are parsed and already consumed by `GaugeCluster.cs:501-509`; `player_fuelleak` is
parsed and handled (`WorldEffectsFactory.cs:455`); `cockpit_engine_sound` is parsed into
`PlaneStats.CockpitEngineSound` (`PlaneStats.cs:490`) with nothing consuming it.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — camera modes and placement

1. ☑ View-mode architecture: Cockpit/Nose as camera modes beside `Views[]`, cycle key, selector
2. ☑ First-person placement: `cockpit_camera` marker, −4.70° head-pitch offset, wobble inheritance
3. ☑ Per-mode FOV: 80° H cockpit / 60° H nose, horizontal-base aspect correction (new modes only)

### Wave B — rendering

11. ☐ Render the `cockpit1` interior in Cockpit only; per-mode node hiding (healthy body, `markers`/`dontmove`)
12. ☐ Drive the `pcdpN` cockpit damage panels in first-person

### Wave C — head-look

21. ☐ Head-look controller: snap + free-look (mouse / right stick) + center key, decoded rates and clamps
22. ☐ Autohead velocity-follow from `player.json` `autohead_*` (off in Nose)

### Wave D — audio

31. ☐ `cockpit_engine_sound` swap in both first-person views (closes `BL-161`)

### Wave E — close-out

41. ☐ File the deferred backlog items; close `BL-080`/`BL-161`; fold the head-look decode into `org/cameraViews.md`

## Dependency and parallelism notes

A1 blocks everything (every other item hangs off the mode concept). A2 and A3 follow A1 and edit
the same camera files — run Wave A sequentially. B11 → B12 is a chain (panels need the interior in
the tree). C21 → C22 is a chain (autohead is the idle branch of the controller C21 builds). D31
needs only A1 (the mode signal) and can run parallel to Waves B/C from a different session, but it
touches `FlightAudio` which no other item edits. E41 is last. File contention: A1/A2/A3 and C21/C22
share `CameraController.cs`/`FlightController.cs` — never in parallel worktrees.

---

# Wave A — camera modes and placement

## A1 ☑ View-mode architecture: Cockpit/Nose as camera modes beside `Views[]`

**Landed.** A pilot selects one of three views and nothing else, the set the original's selector
accepts: `PilotViewMode` (`src/Flight/PilotViewMode.cs`) is Chase/Cockpit/Nose valued as the
engine's own camera modes 0/6/7, and `PilotView` beside it holds the rules as pure functions —
`Cycle` (Cockpit ↔ Nose, entering Cockpit from Chase, the original's fallback), `IsFirstPerson`,
`Effective` (a held view key overrides the selection without changing it) and the `--view=`
spelling. `CameraController` owns the live selection (`ViewMode`, `FirstPerson`,
`CycleCockpitViews`, `SelectChase`) and defers every decision to those functions, so the decisions
unit-test without an engine. `Views[]` and `ActiveView()` are untouched.

**The keys are F8 and F6.** F8 cycles the first-person pair, the binding `PLAN-targeting.md:141`
reserved for "Cycle Cockpit Views"; it was still free (`docs/controls.md` binds F5 and F10–F16 and
F18, and no `Key.F8` existed in the tree). Cycling never returns to Chase, so F6 (also free)
selects the chase view back. The original selects each of its three views separately rather than
cycling all three, and which key it used for chase is not in the decoded data, so F6 is this port's
choice and is documented as such. Both are edge-detected in `FlightController.PollViewModeKeys`,
one slot each, the same shape the targeting keys use; no pad binding.

**Scripted selection** extends `--view=` with the three mode names (`chase`/`cockpit`/`nose`),
held on `SessionSpec.ViewMode` apart from the numpad `View` digit because the two are different
concepts; it is dropped with a warning outside `--fly`/`--stunt` exactly as a digit is, and reaches
the rig through `HumanFlightAdapter` → `FlightController.PinnedViewMode`. The selected mode is
named on the existing `view n=…` breadcrumb (`view n=cockpit`), which is what makes a scripted
selection machine-verifiable before A2/A3 give the modes a distinct pose.

**`PlayerFirstPerson` (condition 120) now reads the mode.** `AnimRuntime.FirstPersonView` replaced
the hardwired `false` field, polled per evaluation and fed by `GameSession.AnyPilotFirstPerson`
over the rigs' `FlightController.FirstPersonView`, on both the world runtime (through
`WorldSession.Options`) and the world-effects runtime (through `WorldEffectsFactory`). A runtime
with no seam still reads false, so every lab and test keeps the pre-A1 answer.

**Not yet different to look through:** Cockpit and Nose take the chase camera's placement until A2
sits them on `cockpit_camera` and A3 gives each its FOV.

**Tests.** `CSVM.Tests/PilotViewTests.cs` covers the cycle, the first-person test, the held-key
override and the flag spelling; `SessionSpecTests` covers `--view=cockpit|nose` and its
outside-flight drop; the new in-engine `first-person-condition` suite runs the shipped `bullet1`
def (whose Initial sequence is `IF PLAYER_1ST_PERSON / ELSE CALL_ANIMATION two_bulletholes_a`) and
asserts the call happens in Chase, is skipped in Cockpit and Nose, and returns on Chase again, with
the def's ungated second sequence as the control.

**⚠ The plan's own evidence was wrong on one point.** It said the condition-120 coverage lives in
the sequences suites and only needed extending: there was none — condition 120 had no test
anywhere, and `bullet1` is the first def in this project ever to take that branch.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The player can select Chase, Cockpit, or Nose; one key cycles Cockpit ↔ Nose (the
original's "Cycle Cockpit Views"); a held numpad key still overrides the selected view exactly as
`FlightController.ActiveView()` does today; the anim runtime's `PlayerFirstPerson` condition
(currently hardwired `false`) reads true in both first-person modes.

**Evidence (confidence: traced).** The original's selector accepts exactly `{0, 6, 7}` and falls
back to 6 (`org/cameraViews.md`, "Only three views are player-selectable", `FUN_0042c210` /
`FUN_004414a0`); modes 6/7 share one placement handler and differ by a flag. CSVM's current view
plumbing: `FlightController.cs:286-303` (`Views[]`), `:1653-1676` (`ActiveView()`), `:1685-1690`
(`ApplyFixedView`); `CameraController.cs:53-60` holds the fixed poses. `PlayerFirstPerson` is
condition 120 (`docs/formats/anim-definitions.md:370`).

**Approach.** Introduce a per-pilot view-mode enum (Chase / Cockpit / Nose) consulted before the
chase math; keep `Views[]` and `ActiveView()` untouched, with a held key winning over the mode as
it wins over `PinnedView` today. Wire the cycle key (`PLAN-targeting.md:141` already reserves F8
for "Cycle Cockpit Views" — confirm it is still free) and a `--view=` scripted twin for
machine-verifiable poses. Flip the `PlayerFirstPerson` condition source to the mode.
<TODO: pick the actual key binding at build time against the current ActionMap; F8 per
PLAN-targeting is the candidate, not a decision.>

**Model recommendation.** high — architecture with a wide blast radius (every later item hangs off
this seam, and it must not disturb `Views[]`/`BL-150`).

**Verify.** `--view=` scripted captures show three distinct poses; holding a numpad key while in
Cockpit snaps to the flank view and releases back. Full 8-chapter `--freecam` regression unchanged.
A sequences test asserting `PlayerFirstPerson` flips with the mode.
<TODO: name the exact test suite to extend (the condition-120 coverage lives in the sequences
suites; find the right one at build time).>

**⚠ Traps.** Do not add cockpit/nose as `Views[]` rows — Decision 2 exists precisely so `BL-150`'s
rebuild later replaces that table without touching these modes. The original's F7 "Access Chase
View" is the flyby (mode 9), not the following chase — do not wire anything to it here.

## A2 ☑ First-person placement: `cockpit_camera`, −4.70° offset, wobble inheritance

**Landed.** Both first-person views sit at the plane's authored `cockpit_camera` marker, rigidly
mounted with the fixed −4.70° head-pitch offset, and inherit the plane's wobble automatically by
riding the DRAWN pose one-to-one — no camera-side shake was added.

`CameraController.FirstPersonPose(planePos, attitude, offset)` is the pure placement law
(`camera_world = plane_pos + plane_rotation × offset`, plus the fixed pitch tilt as a rotation
about the plane's own right axis), static and engine-free so it unit-tests without a `Camera3D`;
`FirstPersonView` is the thin write of that pose onto the owned camera. The offset arrives through
the constructor (`cockpitCameraOffset`, default the origin) — `FlightController.Setup` takes the
same parameter and forwards it, and `HumanFlightAdapter` supplies it from the newly-added
`PlaneBuilder.CockpitCameraOffset`. Two camera sites needed the new arm, both in the plan's own
wiring contract: `FlightController._Process`'s `_cam.FirstPerson` branch (replacing its A1
placeholder that called `Chase`) and `CameraController.Snap` (held-view → back-view → **first
person** → chase fallback), so a spawn/respawn into Cockpit or Nose no longer shows one frame of
the chase pose before the next `_Process` tick corrects it.

**Where the offset lives, and how it's read.** `cockpit_camera` is a mesh-less node inside the
plane's top-level `markers` group (a sibling of `healthy`/`cockpit1`/`destroyed`/`dontmove`,
confirmed against `extracted/planes/nodes.json`: `player_pfighter`'s copy under `markers` carries
local translate `(0, 0.75, −0.2)`, matching the plan's cited value exactly with an identity
parent chain below the root). The interior/wreck subtrees (`cockpit1`, `cockpit2`) carry their
own same-named decoy nodes with different values, so a naive whole-subtree, first-match walk can
resolve to the wrong one depending on child order. `MarkerRig.FindNamedMarker` (new: the
non-weapon sibling of `MarkerRig.Extract`'s weapon-marker walk) skips those alternate-state
subtrees explicitly, the same list `PlaneBuilder.SkipNames` already excludes from the exterior
model; `PlaneBuilder.Build` calls it once per build and exposes the result as
`CockpitCameraOffset`, falling back to `(0,0,0)` for a plane with no such node, as the original
does.

**Axis mapping.** No swap is applied. `docs/org/cameraViews.md`'s "Axis convention" section reads
`nodes.json`'s raw z-signs as "+Z forward," but that reading is a description of the ORIGINAL
BINARY's own internal convention, and it disagrees with this codebase's own, far more broadly
established one: `docs/formats/gotchas.md` (censused over 6,728 `AT_NODE`/`PUFFER_STATE` uses)
states the extracted frame has the nose at **−Z**, "Godot's frame exactly — no mirroring, no axis
swap," and every existing camera site already builds on exactly that (`CameraController.Chase`'s
`nose = -attitude.Z`, `FixedView`'s Kp8 "ahead" direction `(0,0,-1)`, the compass heading in
`FlightController._Process`). Since `PlaneBuilder`/`SceneBuilder` never axis-flip a GameZ node's
local transform when building the Godot tree, the resolving move is to do what every other marker
already does: take `cockpit_camera`'s raw local translate as-is and feed it straight into
`plane_rotation × offset` in Godot's own frame (`attitude * cockpitCameraOffset`), which is
exactly `FirstPersonPose`'s implementation. This sidesteps needing to settle which convention the
*original* binary used internally — CSVM is not re-deriving that engine's math, only placing a
Godot camera relative to a Godot-space plane using Godot-space marker data, the same as every
firepoint and pylon already does. `org/cameraViews.md`'s "Axis convention" section is not amended
here (E41 is this plan's close-out item for `org/cameraViews.md` corrections).

**Tests.** `CSVM.Tests/MarkerRigTests.cs` covers `FindNamedMarker`'s accumulation, its
alternate-state skip (a decoy `cockpit_camera` nested first under a fixture `cockpit1`), and the
absent-node/absent-plane fallback. `CSVM.Tests/CameraControllerFirstPersonTests.cs` covers
`FirstPersonPose` against the plan's known value (`player_pfighter`'s `(0, +0.75, −0.2)`), the
fixed pitch tilt's sign (it looks down, revealing the plane's own nose, matching the plan's Verify
text), and that the plane's attitude carries both the position offset and the aim together, not
independently.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Both first-person views sit at the plane's authored `cockpit_camera` marker (read from
the model per plane, no hardcoded offset), carry the fixed −4.70° head-pitch offset, and rock with
the plane's wobble with no camera-side shake.

**Evidence (confidence: traced).** `org/cameraViews.md`, "Where the first-person camera sits":
placement is `plane_pos + plane_rotation · cockpit_camera_offset` (`FUN_0042d980`), offset loaded
from the `cockpit_camera` scene node with `(0,0,0)` fallback; `player_pfighter` uses
`(0, +0.75, −0.2)`; +Z forward, +Y up. The −0.08203 rad (−4.70°) constant rotation is in this
plan's decompile of `FUN_0042d980` (literal `0xbda7ff58`), applied about the same axis as
elevation in both modes. No wobble is added at placement; the camera inherits the plane node's
rocking (`org/shakes.md`).

**Approach.** Read the `cockpit_camera` node per plane at build (the node→display map is in
`docs/formats/markers.md`); mount the first-person camera on the same rendered plane node the
wobble rocks, so inheritance is free; apply the −4.70° as a fixed rotation in the view build, not
as a change to the marker data.

**Model recommendation.** medium — mechanical once A1's seam exists; the conventions are all
written down.

**Verify.** A scripted capture straight ahead from `player_pfighter` matches the marker: horizon
placement consistent with a camera 0.75 up / 0.2 aft of origin, nose visible per the −4.70° tilt.
Cite `docs/verification.md` before measuring anything in-frame.
Resolved (⚠ table row 2): head-look input is honoured in Nose — `FUN_0042d980` runs the controller
unconditionally in both modes and only autohead is mode-6-gated. Nose gets head-look; the
at-the-controls sitting carries the confirm line.

**⚠ Traps.** Do not add any camera-side shake to the first-person views; `damage_shakes` authors a
separate half only for the chase camera because it sits outside the rocking node (`org/shakes.md`,
"two cockpit views need no separate handling"). The `pdpN`↔`pdpN_h` numbering is crossed on three
plane models; if any placement work touches node pairing, pair by mesh position, not name
(`docs/formats/vehicle.md:236`).

## A3 ☑ Per-mode FOV: 80° H cockpit / 60° H nose

**Landed.** Cockpit renders at 80° horizontal FOV, Nose at 60°, both derived to a vertical angle
at the OWNED camera's own live viewport aspect every time the pose is written; every external view
(chase, fixed numpad, back, pad-look, crash) keeps GameSession's 62° vertical global untouched.

`CameraController.HorizontalToVerticalFovDeg(horizontalDeg, liveAspect)` is the pure conversion
law — `vertical = 2·atan(tan(H/2) · assumedAspect/liveAspect)` with `assumedAspect = 4/3` (the
engine's own reference) — static and engine-free so it unit-tests without a `Camera3D`/`Viewport`,
the same shape A2 gave `FirstPersonPose`. `ApplyFirstPersonFov()` is the thin write: it reads the
owned camera's OWN `GetViewport().GetVisibleRect().Size` (the per-pane `SubViewport` in
splitscreen, the window otherwise), picks 80°/60° off `ViewMode`, and sets `Camera3D.Fov`. This
project sets no `keep_aspect` anywhere in the tree, so `Fov` stays Godot's default Keep-Height
vertical angle everywhere, which is exactly what the conversion law produces.

**Where the 62° global lives, and how this stays off it.** `GameSession.cs:475`/`:2624` and
`Launcher.cs:490` are the only three places `Fov = 62` is written, one per owned `Camera3D` (the
single-player main camera, each splitscreen pane's camera). `CameraController` never touches those
sites: it captures `camera.Fov` once at construction (`_externalFovDeg`) and only ever restores
that captured value (`RestoreExternalFov()`) or overrides it with the derived first-person value —
it does not know 62 is the number, and the migration E41 files stays a change to those three call
sites alone.

**Wiring covers every path through the camera chain.** `FlightController._Process`'s main branch
now calls `RestoreExternalFov()` unconditionally before deciding the frame's pose, then
`ApplyFirstPersonFov()` only in the `FirstPerson` arm (alongside `FirstPersonView`) — so a held
numpad key or look-behind while SELECTED Cockpit/Nose gets the external FOV for as long as it is
held and the first-person FOV back on release, matching how those keys already override the pose.
`CameraController.Snap` (spawn/respawn/weapon-lab re-park) got the same default-then-override
shape on its own first-person arm. `CameraController.CrashView` restores the external FOV
unconditionally on the crash cut, since that framing is always external regardless of the view
that was selected when the crash happened.

**Splitscreen needs no special-casing.** Each pilot's rig already owns its own `Camera3D`
(`GameSession.BuildRigs`, one per pane), so `ApplyFirstPersonFov()` reading `_camera.GetViewport()`
naturally picks up that pane's own size and aspect — no session-wide FOV variable to fight over,
per Decision 5.

**Tests.** `CSVM.Tests/CameraControllerFovTests.cs` asserts the plan's two pinned 16:9 results
(46.8°/64.4°) and checks the law at a second aspect ratio (4:3, the engine's own reference, where
the aspect factor collapses to 1 and the derived vertical equals the stored horizontal exactly) —
confirming the law is live-aspect-driven rather than a hardcoded 16:9 table.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Cockpit renders at 80° horizontal FOV, Nose at 60° horizontal, both derived from the
horizontal half-angle with aspect correction the original's way; external views are untouched.

**Evidence (confidence: traced).** `org/cameraViews.md`, "How the per-view FOV is gated" and "FOV
constants and aspect correction": the gate is purely mode 6; constants `1.0471976` / `1.3962634`
rad; `vertical = atan(tan(H/2) · aspect)`; at 16:9 that is 46.8° V and 64.4° V. CSVM's current 62°
vertical global lives in `GameSession.cs`.

**Approach.** Per-mode FOV on the first-person camera only, computed from the horizontal
half-angle at the live viewport aspect (do not hardcode the 16:9 results). Leave `GameSession.cs`'s
62° for every external view; the migration is E41's filed item.

**Model recommendation.** medium, low effort — a bounded formula with the constants pinned.

**Verify.** Screenshot the same scene in Cockpit / Nose / Chase: Cockpit visibly wider than Nose;
Nose vs Chase differ only by position/nodes, not FOV, until the global migration lands. Assert the
computed vertical angles at 16:9 (46.8° / 64.4°) in a unit test.

**⚠ Traps.** The `.ani` `H_FOV`/`V_FOV` events (`CAMERA_STATE`/`CAMERA_FROM_TO`) are in-script FOV
changes only and must not be wired to the base FOV (`org/cameraViews.md:183-187`). Store and port
horizontal; the vertical is derived, never authored.

# Wave B — rendering

## B11 ☐ Render the `cockpit1` interior in Cockpit only; per-mode node hiding

**Goal.** Cockpit shows the plane's `cockpit1` interior; Nose hides it plus the `markers` and
`dontmove` nodes; both first-person views hide the own-plane healthy body; external views and
other planes are unchanged.

**Evidence (confidence: traced for the gates, open for the wiring).** The original draws
`cockpit1` only in mode 6 (`FUN_0049fb00` via `org/cameraViews.md:138-145`); mode 7's extra hiding
and the healthy-body hide are `FUN_0042e5e0` (`org/cameraViews.md:147-155`). CSVM skips the subtree
at build (`PlaneBuilder.cs:24-27` `SkipNames`); the `gauges` subtree inside `cockpit1` is already
mined by `GaugeCluster` for the screen-space HUD.

**Approach.** Stop skipping `cockpit1` for the player plane (keep skipping `cockpit2`,
`destroyed`-state interiors and AI planes — read `docs/architecture.md`'s PlaneBuilder entry
first); attach it hidden, and gate visibility per pilot view mode. Implement per-mode hiding as
visibility toggles scoped to the first-person render, not node removal.
<TODO: what `cockpit2` is (second cockpit LOD? co-op seat?) — check the trees and
`docs/formats/gamez.md` before deciding whether it stays skipped.>
<TODO: the interior's `gauges` child renders as static geometry in-3D while `GaugeCluster` stays
the live HUD (Decision 1); confirm the static needles don't read as broken, else hide the `gauges`
child and note it in the filed 3D-gauge-drive item.>

**Model recommendation.** high — rendering-pipeline change in the most shared builder; the
regression surface is every plane in every chapter.

**Verify.** Full 8-chapter `--freecam` regression: unchanged node/mesh counts for AI planes,
player plane gains the interior subtree only. In Cockpit: interior visible; in Nose: not, and the
forward view unobstructed; own healthy body absent in both; chase view unchanged. Splitscreen not
judged (Decision 5).

**⚠ Traps.** `WorldEffectsFactory.cs:41` names `cockpit1` in its own skip/anchor list — check that
adding the subtree does not double-register effect anchors. The `markers`/`dontmove` nodes' exact
visual role is unresolved in the decode (`org/cameraViews.md`, "Not resolved") — hide them in Nose
because the original does, and record what they turn out to contain rather than guessing.

## B12 ☐ Drive the `pcdpN` cockpit damage panels in first-person

**Goal.** Cockpit-interior torn-skin panels flip with zone damage the way the exterior `pdpN`
panels already do, and reset on respawn.

**Evidence (confidence: traced for the data, open for the engine-side rule).** `pcdpN` nodes are
recognised (`PlaneBuilder.cs:152-163`) and deliberately skipped by `DamageVisuals`
(`DamageVisuals.cs:21`, `:365`). The reset anim re-ACTIVEs healthy twins and sets every
`pdpN`/`pcdpN` INACTIVE (`docs/formats/vehicle.md:232-235`), and no zrdr data ever deactivates an
`_h` node, so the original hides healthy skins at damage time by an engine-side rule.

**Approach.** Extend `DamageVisuals` to stop skipping `pcdpN` when the pilot has a first-person
view available, driving them from the same injure entries as the exterior panels; reuse the
existing healthy-twin handling rather than inventing a cockpit-specific rule.
<TODO: which anims call `pdpanelN`-equivalents for the cockpit panels — the known calls are
`pdpanel4/6` at `pcdp4/pcdp6` (`data/effect_pools.json:59`); confirm whether other `pcdpN` exist
per airframe or only 4/6.>

**Model recommendation.** medium — pattern reuse inside one module.

**Verify.** In the damage lab (`DamageLab.cs` drives the injure cycle), wound the relevant zones
and watch the interior panels flip in Cockpit; respawn resets them. Exterior panel behaviour
unchanged in the chase view.

**⚠ Traps.** The crossed `pdpN`↔`pdpN_h` numbering on three models (`docs/formats/vehicle.md:236`)
may extend to `pcdpN` — pair by mesh position. Do not touch the effect-pool budgets: `pcdp4/pcdp6`
already participate in `effect_pools.json` counts.

# Wave C — head-look

## C21 ☐ Head-look controller: snap + free-look + center key

**Goal.** In both first-person views, the head pans at the decoded rates on mouse or right stick,
snaps on the snap keys with the original's direction mapping (forward = straight up, forward-diagonals = 45°
up, others level), never looks below level, approaches targets with the decoded smoothing, and
recenters on the center key.

**Evidence (confidence: traced).** This plan's decode of `FUN_0042d010` (all constants in "What
the data actually ships"): elevation clamp `[0, π/2]` with caller floor 0; free-look pan 2 rad/s;
smoothing `e^(−rate·dt)` with elevation 3.0/s, azimuth 5.0/s; snap windows forward < 0.1°,
diagonals ±0.09°; azimuth wraps to ±π (`FUN_00460ab0`).

**Approach.** A small head-look state on the first-person camera: target elevation/azimuth from
input (snap sets targets, free-look integrates them), displayed angles chasing targets with the
exponential law, composed with A2's fixed offset. Mouse and right-stick free-look both feed the
same integrate path (Decision 4).
<TODO: snap-key bindings — the original's nine key slots (indices 0x3a–0x42) are engine key
indices, not physical keys; pick a CSVM cluster at build time against the ActionMap and the
Tartarus layout (take the user's numbering, per the debug-keys memory).>
Resolved (⚠ table row 2): head-look runs in both first-person views with the elevation floor at
level; only autohead (C22) is Cockpit-only. The original also runs this controller for the chase
view with a −π/2 floor; that stays out of scope and is E41's filed item (7).

**Model recommendation.** high — feel-adjacent behaviour with several interacting constants; wrong
composition order (offset vs elevation vs azimuth) would read as subtly broken.

**Verify.** Unit-test the smoothing law and clamp against the decoded constants (rates 3.0/5.0,
clamp `[0, π/2]`, 2 rad/s integration). At the controls: pan feel, snap directions, center key. The
user's eyes outrank instruments on feel.

**⚠ Traps.** The elevation convention is "0 = level, π/2 = up" — do not re-derive it as a signed
pitch; the original cannot look below level in first person and faithfulness wins over comfort
here (file a taste item later if it feels wrong, do not silently widen the clamp). `DAT_009ad744`
was read as the frame dt from usage; if rates feel double or half, re-check that assumption first.

## C22 ☐ Autohead velocity-follow

**Goal.** With no look input in Cockpit, the head leans along the velocity vector per the
`player.json` `autohead_*` values; Nose never does this; an options-style toggle mirrors the
original's enable flag.

**Evidence (confidence: traced).** The gated tail block of `FUN_0042d010` plus the loader
`FUN_004735b0`: velocity into plane frame, × `autohead_turn_time` (0.75), magnitude cap
`autohead_turn_max` (2.86° authored → 0.0998 rad stored, note the loader doubles it), elevation
floor `autohead_turn_min_pitch` (−3°); enabled only when the option byte is set and mode ≠ 7.
Shipped values `extracted/zrdr/player.zrd.json:35-46`.

**Approach.** Parse the three keys in `PlaneStats`' player-globals reader (they sit beside the
already-parsed `sticky_bullet_*` family, `docs/formats/vehicle/player-globals.md:31`), apply the
rule in C21's idle branch, reproducing the ×2 on `turn_max` and the degree→radian conversions
exactly as the loader does.

**Model recommendation.** medium — a bounded rule with pinned constants on C21's seam.

**Verify.** Unit-test the parse (0.75 / 0.0998 rad / −0.0524 rad from the shipped file, defaults
when absent). At the controls: a hard turn leans the view into the turn, capped small (~5.7° at
the doubled cap); goes still in Nose.

**⚠ Traps.** The −3° floor is *below* C21's [0, π/2] clamp floor — the autohead path sets
elevation directly with its own floor (the caller's 0 floor applies to the input paths, not this
block); keep the two floors distinct or the lean-down disappears.

# Wave D — audio

## D31 ☐ `cockpit_engine_sound` swap (closes `BL-161`)

**Goal.** Entering either first-person view swaps the own-ship engine loop def to the plane's
`cockpit_engine_sound` (`snd_*_cp`); leaving swaps back; nothing else about the engine audio chain
changes.

**Evidence (confidence: traced).** `docs/formats/vehicle.md:94`: swapped in "while the camera is
in either of the original's two cockpit modes, swapped back on leaving them". Parsed and waiting:
`PlaneStats.cs:250-254` (`CockpitEngineSound`, with its own warning not to bind to numpad views).
`BL-161` records the data shape (per-plane defs alongside `engine_sound`).

**Approach.** In `FlightAudio`, select the loop def off the pilot's view mode from A1; keep
throttle/damage behaviour identical across the swap (same curves, different def). Delete `BL-161`
and update the `PlaneStats.cs:250` comment when it lands.

**Model recommendation.** medium — one selection seam in a well-documented chain.

**Verify.** Listen A/B at the controls: enter Cockpit, loop timbre changes to the `_cp` def;
throttle response unchanged; back to chase restores. Note `BL-391` (base engine loop too hot) is
open — judge the swap's presence, not its absolute level.

**⚠ Traps.** Do not resolve `BL-391`'s level here, and do not add splitscreen `MixGain` handling —
that is E41's filed splitscreen item (Decision 5). The swap is a def change on the engine slot,
not a second voice; stacking both loops would be inventing content.

# Wave E — close-out

## E41 ☐ File the deferred items; close `BL-080`/`BL-161`; fold the decode into `org/cameraViews.md`

**Goal.** Every deliberately deferred point exists as a `backlog.md` item; `BL-080` and `BL-161`
are closed per the close-backlog-item ritual; the head-look decode (constants, addresses, the
mode-7 nuance) lives in `docs/org/cameraViews.md` so no future session re-runs it.

**Evidence (confidence: n/a — bookkeeping).** The filed-item list, from Decisions 1/3/4/5 plus
session findings: (1) in-3D gauge drive (and whatever B11's static-gauges TODO decided); (2)
padlock look state (needs targeting's current-target plumbing); (3) zoom/lean axis (keys
`0x43`/`0x44`, rate 2·dt, smoothing 1.5/s — constants ready in the plan); (4) engine-wide FOV
migration 62°V → 60°H base, carrying the overcast/tracer calibration warning; (5) splitscreen
cockpit behaviour (per-viewport interior cost, per-pilot engine-sound swap under `MixGain`);
(6) HUD `POSITION_1ST` layout variant (the original places gauges differently in first person,
`hud_v2.zrd` keys `POSITION_1ST`/`POSITION_3RD`, `org/cameraViews.md:120-128`) — whether
`GaugeCluster` should reposition in cockpit views; (7) chase-view look-around — the original runs
the same head-look controller for the chase camera with elevation floor −π/2 (`FUN_0042c7f0` →
`FUN_0042d010(0xbfc90fdb, 0)`), which CSVM's chase view lacks entirely.

**Approach.** Use `/close-backlog-item` for `BL-080`/`BL-161` (evidence and dates go in the
closing commit message, not the files). Amend `org/cameraViews.md` per its own contract (behaviour
and constants, every claim naming its function, no decompiler output), including correcting its
mode-7 head-look line to the autohead-flag reading (⚠ table row 2), or to whatever A2's TODO
settled.

**Model recommendation.** medium, low effort — mechanical, but the backlog entries must be written
to stand alone months later.

**Verify.** The duplicate-ID and prose hooks pass on commit; a later `/backlog` on any filed item
explains cold.

**⚠ Traps.** The `org/cameraViews.md` correction must not delete the page's existing claim history
convention — a superseded reading there gets the dated RETIRED treatment only if the page's org
conventions call for it; otherwise rewrite in place and let the commit message carry the event.
