# Cockpit view (BL-080) — the original's two first-person views

**✅ COMPLETE** (written 2026-08-21, completed 2026-08-23). Archived in `docs/plans/`; its row is
in [`plans.md`](plans.md).

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

11. ☑ Render the `cockpit1` interior in Cockpit only; per-mode node hiding (healthy body, `markers`/`dontmove`)
12. ☑ Drive the `pcdpN` cockpit damage panels in first-person

### Wave C — head-look

21. ☑ Head-look controller: snap + free-look (mouse / right stick) + center key, decoded rates and clamps
22. ☑ Autohead velocity-follow from `player.json` `autohead_*` (off in Nose)

### Wave D — audio

31. ☑ `cockpit_engine_sound` swap in both first-person views (closes `BL-161`)

### Wave E — close-out

41. ☑ File the deferred backlog items; close `BL-080`/`BL-161`; fold the head-look decode into `org/cameraViews.md`

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

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed,
engine suites 93 passed / 0 failed with errors clean (the `first-person-condition` suite among
them), goldens 15 unchanged plus `viewer-bhawk` re-pinned for the interior the viewer lab now
builds.

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

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed
(`CameraControllerFirstPersonTests` and the decoy-marker fixture among them), engine suites
93 passed / 0 failed with errors clean, goldens 15 unchanged plus the `viewer-bhawk` re-pin.

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

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed
(`CameraControllerFovTests` pinning 46.8°/64.4° at 16:9 and the 4:3 collapse among them), engine
suites 93 passed / 0 failed with errors clean, goldens 15 unchanged plus the `viewer-bhawk`
re-pin; the external views' goldens moving zero pixels is the Decision 3 boundary holding.

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

## B11 ☑ Render the `cockpit1` interior in Cockpit only; per-mode node hiding

**Landed.** Cockpit draws the player plane's `cockpit1` interior and hides its `healthy` body;
Nose hides the interior, the body and the `markers`/`dontmove` groups; every external pose renders
the aircraft exactly as it was built, and AI planes are byte-for-byte unchanged.

`CockpitVisibility` (`src/Flight/CockpitVisibility.cs`) holds the rule as a pure function —
`Rules(mode, firstPerson)` returns which of the four groups render — with `Bind`/`Apply` as the
thin write onto one built plane model, the same shape A2 gave `FirstPersonPose` and A3
`HorizontalToVerticalFovDeg`. `FlightController._Process` calls `Apply` once per frame beside the
camera write, keyed to **the pose that frame actually took**, not to the selection: a held numpad
key or the look-behind puts the camera outside the aircraft, and the original's gate is the live
camera mode, so the body comes back for as long as the key is held. That is exactly the
default-then-override shape A3 already gave the FOV.

**The interior builds only for a human rig.** `PlaneBuilder`'s new `cockpitInterior` flag takes
`cockpit1` back out of `SkipNames` for that one build and mounts it hidden as `CockpitInterior`;
`HumanFlightAdapter` is the only caller that passes it, so `FlightRoster`'s AI builds and every lab
and suite build are untouched. The subtree's `pcdp4`/`pcdp6` panels come with it, built hidden and
deliberately kept OUT of `DamagePanels` — that list is the exterior set `DamageVisuals` drives, and
the interior pair is B12's on its own seam. **B12's own TODO is answered on the way past:** every
one of the 11 airframes carries exactly `pcdp4` and `pcdp6` inside `cockpit1` and no other `pcdpN`,
and each is a mesh-bearing torn-skin panel — except the Hoplite (`player_autogyro`), whose two are
mesh-less anchors. Reach them with a name walk under `PlaneBuilder.CockpitInterior`.

**⚠ The interior's off-states ship ACTIVE, so building the subtree faithfully is not enough.**
`cockpit1` carries five windshield bullet-hole decal groups (`bullet1`…`bullet5`, each wrapping
3–4 `bulNx` meshes under a `gNNN` node) and two warning lamps (`lowalt_on`, `stallwarning_on`).
All seven are on all 11 airframes and all ship `active: true` — the original hides them
engine-side until something drives them, exactly the pattern the plan already records for the
`_h` nodes. Left as built, a pristine plane renders every bullet hole as a white splat across the
sky and holds both lamps lit. `PlaneBuilder.IsInteriorDrivenState` is the named set, parked hidden
by `ParkInteriorStates` alongside the gamez `active: false` hide. **Driving them is not this
item:** the bullet holes' driver is the `cockpit_bulletholes` def family (`two_bulletholes_a`
and its siblings — the same defs A1's `first-person-condition` suite runs) whose anim-side trigger
is `window_hit_sg`, recorded in `plans/PLAN-m3-polish-5.md:453` as needing a cockpit view; that is
future work beside B12's `pcdpN` drive. The lamps belong to E41's 3D-gauge-drive item, which is
what will light them.

**⚠ The interior is authored in its own space, and the two spaces are not a similarity apart.**
The pilot's eye is `cockpit1`'s own origin looking down −Z: `extracted/zrdr/instruments.zrd.json`
places every instrument at z −17.5 straight ahead of it (speedometer `+9`, altimeter `−9`, compass
`+7` up, the panel ±9 wide). But the interior's own elevators sit at y −10.5 where the exterior's
sit at −0.40, and no uniform scale maps the two: fitting the ailerons gives 15.5, fitting the
elevators 17.6, and the Y term comes out negative. It is a stylised model built to be looked at
from one point, not a scaled copy of the aircraft, which is why the original draws it in its own
pass. The mount is therefore the `cockpit_camera` offset with a uniform `PlaneBuilder.InteriorScale`
and no rotation of its own (the −4.70° tilt is the head, and the head looks around inside a
plane-fixed interior). Since eye-at-origin geometry subtends the same angles at any scale, **the
framing does not depend on that constant** — only how the interior composites against world
geometry does, so it is a port TUNE, not decoded, and 0.04 puts the panel ~0.7 m ahead of the eye.

**Settled TODOs.**

- **`cockpit2` is not a thing.** `planes.zbd` ships 11 `cockpit1` subtrees (one per player
  airframe) and **zero** `cockpit2`; the eight chapter gamez files have neither. The skip entry
  stays as defence and now says so, in `PlaneBuilder` and here.
- **Nothing else in the subtree is a conditional state.** A census of all 210 distinct node names
  across the 11 `cockpit1` subtrees leaves exactly three classes beyond the seven above: the
  damage-dial zones (`nosedamage`/`taildamage`/`leftwingdamage`/`rightwingdamage`), the belt
  segments (`ggindicatorN`/`mgindicatorN`) and the needles. All three are **always drawn and
  recoloured** rather than shown and hidden — `GaugeCluster.ExtractDamageDial` and
  `DrawWeaponGauge` are CSVM's own decode of that, and the same code is what classifies `*_on` as
  an overlay. `nitrogauge` is the one node shipped `active: false`, on the Devastator only, and
  the build honours the flag.
- **The `gauges` child stays visible.** It is 39 meshes of authored instrument-panel geometry —
  the dial faces, bezels and the panel they are set into — not a needle overlay, so hiding it
  would cut a hole in the dashboard. A capture in Cockpit shows the panel reading correctly. The
  needles do not move (`GaugeCluster` stays the live screen-space HUD, Decision 1) and the two now
  double up: the HUD dials draw over the 3D panel. E41's 3D-gauge-drive item carries both halves —
  drive the authored needles, and decide whether the screen-space cluster then retires or moves
  (it already owes the `POSITION_1ST` layout question, filed item 6).
- **`markers` and `dontmove` are resolved.** `markers` is 25 nodes, 24 of them mesh-less
  reference points (`cockpit_camera`, `target`, `ground_level`, `pylon1-8`, `firepoint1-8`, `map`,
  `exhaust1/2`, `ladder_pos`, `cf_light`) plus exactly one mesh: `cockpit_light`, the lamp.
  `dontmove` is the propeller group — `wing_flare1/2`, `staticprop1`, `prop1`, `prop1b`,
  `nitroprop1`. So mode 7's extra hiding is the prop disc plus the cockpit lamp and whatever hangs
  on the marker nodes (mounted pylon ordnance, muzzle flashes), which is exactly "an unobstructed
  forward look". The Nose capture confirms it: no prop, no airframe, nothing in frame.
- **`WorldEffectsFactory.cs:41` does not double-register.** Its `CrashAnchorNodes` list builds
  FRESH mesh-less `Node3D`s under a synthetic `player` root for the crash lab (`BuildCrashAnchorSet`);
  it never walks a built plane, so an attached `cockpit1` adds no anchor and changes no effect-pool
  count. The production crash rig's own `CollectVisibility` snapshot now includes the hidden
  interior, which is the right pristine state for a respawn to restore.

**Splitscreen posture (Decision 5).** Visibility is a property of a node, not of a viewport, so a
pane whose pilot sits in the cockpit hides that aircraft's body in every pane. No per-viewport
render-layer machinery was built. Each rig owns its own plane model and its own
`CockpitVisibility`, so the rule is at least per-pilot rather than keyed to player 1 — the cheapest
thing that is single-player-correct and does not have to be unpicked when splitscreen is judged.

**Tests.** `CSVM.Tests/CockpitVisibilityTests.cs` covers the pure rule: the external pose, Cockpit,
Nose, the held-key override under a first-person selection, that the two first-person views agree
on the body and differ elsewhere, and `IsInteriorDrivenState` over both the seven driven states
and the panel geometry that must NOT be caught by it. The new in-engine `cockpit-interior` suite
covers what xunit cannot reach without an engine — that a default flight build gains nothing, that
the interior build mounts the subtree hidden at the marker at `InteriorScale`, that
`gauges`/`pcdp4`/`pcdp6` are present with the panels hidden, that all five `bulletN` groups and
both lamps are parked while the dashboard beside them still renders, that `DamagePanels` holds no
`pcdp` name, that `Bind` picks the airframe's `markers` group and not a gauge's, and that all
three visibility states land on the real nodes.

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed,
engine suites 93 passed / 0 failed with errors clean (`cockpit-interior` among them), goldens 15
unchanged plus the `viewer-bhawk` re-pin. Orchestrator-reviewed captures at one scripted pose:
Cockpit clean of the parked states after the bullet-hole fix, Nose unobstructed, Chase unchanged.
The interior's world-composite scale and look in motion are `BL-436`'s sitting.

**Original approach (kept for reference).**

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

## B12 ☑ Drive the `pcdpN` cockpit damage panels in first-person

**Landed.** `pcdp4`/`pcdp6` flip with zone damage off the SAME `pdpanel4`/`pdpanel6` injure
entries that already flip the exterior `pdp4`/`pdp6`, and clear together on respawn — no separate
cockpit rule, exactly the pattern the plan's Approach called for.

`PlaneBuilder.CockpitDamagePanels` is `CollectWingFlares`'s cockpit half of the same walk that
already built `DamagePanels`: both torn-skin lists get `Visible = false` at build time, but the
cockpit pair (found only when `cockpitInterior: true`, B11's seam) lands on its own list rather
than `DamagePanels`, since that list is the exterior set `DamageVisuals`' pairing walk measures
mesh-AABB centers over. `DamageVisuals`'s constructor takes the new list as an optional
`cockpitPanels` param and folds it into the SAME `_panels` table `DamagePanels` already populates
— one dictionary, keyed by node name, so `pdp4` and `pcdp4` sit side by side with no collision.
`ApplyPartStage`/`Retract` (the two places `pdpanelN` already flips `pdp`+n) now also look up
`pcdp`+n in that table and flip it the same instant, off the identical threshold crossing; `Reset()`
needed no new code at all; its existing loop over `_panels` sets `Visible = name.EndsWith("_h")`,
which is already `false` for a name with no `_h` suffix.

**The crossed-numbering trap does not extend to `pcdpN` — there is nothing to pair.** A full-text
search of `extracted/planes/nodes.json` for `pcdp4_h`/`pcdp6_h`/`pcdp1`/`pcdp2`/`pcdp3`/`pcdp5`
returns zero matches on all 11 airframes: `pcdp4`/`pcdp6` are the only cockpit damage nodes that
exist, and neither carries a healthy twin the way `pdpN`/`pdpN_h` do. `PairHealthySkins`'s
pairing walk still iterates over the cockpit pair (they share `_panels`), but `PanelPairingSets`'s
`TornTargets` set is built from node names starting with `pdp` — `pcdp4` fails that prefix test
(`p`-`c`-`d`-`p`, not `p`-`d`-`p`) — so the pairing walk excludes them with a log line and pairs
nothing; the exterior set's own pairing is untouched.

**The B11 visibility interplay needed no new code.** `CockpitVisibility.Apply` (B11) only ever
writes the four top-level groups it binds (`_interior`, `_body`, `_markers`, `_dontmove`) — never
a descendant's own `Visible` — so a torn `pcdp4`/`pcdp6` keeps its own state exactly as
`DamageVisuals` set it across any Cockpit↔Nose↔external switch. `ParkInteriorStates` (B11) parks
only the seven interior-driven states (`bulletN`, the two lamps); `pcdp4`/`pcdp6` are not in that
set and never were — they get their pristine hidden state from `CollectWingFlares`'s unconditional
`Visible = false` on every damage-panel node, the same line that already hides the exterior pair.

**No effect-pool budget changed.** `pdpanel4`/`pdpanel6`'s own SEQUENCE_DEFINITION already sets
BOTH `pdp4`/`pcdp4` (or `pdp6`/`pcdp6`) `ACTIVE` in one authored event
(`extracted/zrdr/player-1.zrd.json`), and already calls `gimme_bigflakes`/`large_firetrail` WITH
`pcdp4`/`pcdp6` in first person — the calls `effect_pools.json:59`'s `planeflakes2` budget already
counts. `DamageVisuals`'s new lookup only sets `Node3D.Visible`; it plays no new anim and calls no
new template, so no pool count moves.

**The parked `--viewer`/`--damage` lab exercises the pair too.** `GameSession.BuildStaticStage`
passes `cockpitInterior: _spec.Viewer` on the same gate as `damagePanels`, and its `DamageVisuals`
construction now passes `builder.CockpitDamagePanels` through — the interior stays built hidden,
like the exterior panels, so the HP sliders drive `pcdp4`/`pcdp6` identically to flight; the node
lab can bring the interior into view for a look while a slider is dragged.

**Tests.** The new in-engine `cockpit-panel-staging` suite (`DamageSuites.CockpitPanelStaging`)
builds a plane with `cockpitInterior: true`, crosses whichever of `pdpanel4`/`pdpanel6` the
airframe's own data authors, and checks: the cockpit panel starts hidden; it tears the instant the
exterior panel does, off the one `OnPartDamage` call; it stays torn across a `CockpitVisibility`
Nose↔Cockpit switch; and `Reset()` clears the exterior panel and its cockpit twin together. Godot
`Node3D`/`MeshInstance3D` types are not exercised in headless xunit anywhere in this codebase, so
this is the honest check, the same family B11's `cockpit-interior` suite is in.

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed,
engine suites 93 passed / 0 failed with errors clean; `cockpit-panel-staging` also ran standalone
in the worktree ahead of the item's commit (1 passed, errors clean). Goldens 15 unchanged plus
the `viewer-bhawk` re-pin, which is this item's own viewer-lab wiring made visible.

**Original approach (kept for reference).**

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

## C21 ☑ Head-look controller: snap + free-look + center key

**Landed.** In both first-person views the head snaps to a direction, free-looks on the mouse or
the right stick at the decoded 2 rad/s, recenters on its own key, never looks below level, and
reaches the eye through the decoded exponential smoothing whichever path set its target.

`HeadLook` (`src/Flight/HeadLook.cs`) is the whole behaviour as one engine-free class, the shape
A2 and A3 already gave the camera math. It holds two pairs of angles — the TARGETS the input sets
and the SHOWN angles that chase them — and `Step(dt, HeadLookInput)` picks this frame's target and
then always chases it. That "always" is the design: snap, free-look, the center key and C22's
autohead all reach the eye through the same law, so none of them can drift into its own feel. The
statics (`SnapTargets`, `Approach`, `Wrap`) are the decoded laws on their own, testable without an
instance.

**The angle conventions are the original's, taken literally.** Elevation is 0 at level and +π/2
straight up, never a signed pitch; azimuth is 0 dead ahead, positive to the LEFT, wrapped to ±π.
The elevation clamp floor is a constructor parameter rather than a constant, because that is the
only thing the original's two callers differ in: first person passes 0, the chase handler −π/2, and
the chase look-around E41 files is this same class with the other floor.

**⚠ The decode's two input paths disagree on the azimuth sign, and CSVM picks one.** Snap sets
azimuth to the direction's angle NEGATED, while free-look adds `2·dt·sin(hat angle)` — read
literally, snapping right and panning right move the azimuth opposite ways. A port cannot ship
that: pushing right and pressing the right key must both look right. Both paths here use one
convention (positive azimuth = left), which is the snap path's sign; the free-look path's is
mirrored to match. Only the sign is a port decision, not the rates, the windows or the law.

**⚠ The plan contradicts itself on the diagonals, and the data survey is right.** The C21 Goal
below says "forward-diagonals = 45° up", but the survey's own decode of `FUN_0042d010` lists all
four windows — 45°, 135°, 225° and 315°. The original's key-binding menu settles it in words:
`Kp1` is **Look Up/Left/Rear** and `Kp3` **Look Up/Right/Rear** (`OriginalScreenshots/Keybinds
Views 2.png`), so an aft diagonal lifts the head exactly as a forward one does. `SnapTargets`
implements it and a labelled test pins all eight slots against those labels.

**The composition order is azimuth, then elevation about the axis azimuth just produced.**
`CameraController.FirstPersonPose` grew two optional angle parameters and builds
`attitude · R(up, azimuth) · R(right, elevation + fixed tilt)`. Post-multiplying is what makes the
elevation intrinsic: looking 90° left and then up pitches through the head's own horizon rather
than rolling the view, which is what a neck does. The fixed −4.70° offset rides the elevation axis
because that is the axis the original applies it about, so at zero head angles the pose is A2's
exactly, and a test pins that. `Snap` (spawn, respawn, the lab's re-park) recenters the head, so a
settle-immediately pose never frames itself over the pilot's shoulder.

**Input is read in `FlightController` and nowhere else** (`HeadLookRead` beside `PadLookInput`),
the same rule `OrbitInput` follows: the camera never learns about pads, mice or key layouts. It is
gathered and stepped inside the first-person arm only, on the SIM dt — a held numpad key freezes
the head where it was and releasing resumes it, and the wall-clock mistake the chase transient
records is avoided by construction.

**Bindings.** The snap cluster is **the original's own**, read off its key-binding menu
(`OriginalScreenshots/Keybinds Views 2.png`) rather than chosen here; only the free-look devices
are this port's call.

- **Snap: `Kp1`–`Kp9`, the original's bindings exactly.** `Kp8` Look Up, `Kp4`/`Kp6` Look
  Left/Right, `Kp2` Look Back, `Kp7`/`Kp9` Look Up/Left and Up/Right, `Kp1`/`Kp3` Look Up/Left/Rear
  and Up/Right/Rear. Each key is its own 2-D offset from `Kp5` and the offsets are summed, which is
  the same composition the decode describes for the engine's nine key slots.
- **Center: `Kp5`, the original's "Look Forward".** The middle of the cluster, and the slot the
  decode puts in the middle of its own nine (`0x3e` of `0x3a`–`0x42`).
- **⚠ The numpad therefore holds no fixed view in first person** — that is what makes the binding
  possible rather than a collision. `PilotView.HoldsFixedViews` is the rule and
  `CameraController.ActiveView` returns −1 under it, so both callers (the per-frame chain and
  `Snap`) obey it and no `Views[]` row moved. Numpad 0's look-behind is untouched and still
  overrides every mode: it is outside the cluster, and it frames the aircraft from ahead rather
  than turning the pilot's head. Outside first person the numpad keeps today's held-view behaviour
  exactly, which is `BL-150`'s to rebuild (Decision 2).
- **Free-look: the right stick, and the mouse while its right button is held (⚠ reviewable).**
  This half is a port choice: the original reaches its two look modes through key selectors
  (`K` Access Snap Look Mode, `J` Access Smooth Look Mode, `OriginalScreenshots/Keybinds Views
  1.png`) rather than by which device moved, and those selectors are a filed E41 item. There is no mouse
  input at all in flight today, so nothing had to move; hold-to-look rather than always-on because
  RMB-held IS this project's look posture already (the freecam and the spectator both use it) and
  because an always-on mouse would pan the head every time the pilot nudged a mouse they are not
  using to fly. The stick's click keeps its E42 look-behind, which is checked before the
  first-person arm, so a click in the cockpit leaves it for as long as it is held.
- Direction only, on both devices: the decoded rate is fixed, so a light stick deflection pans
  exactly as fast as a hard one. That is the original's hat-switch input, and it is also what makes
  polled mouse deltas safe at a screen edge.

**Tests.** `CSVM.Tests/HeadLookTests.cs` covers the snap table as a labelled theory over all eight
of the original's own numpad slots (each case named for the menu label it must reproduce) plus no
direction at all, the release-to-ahead rule, 2 rad/s integration and its direction-only reading,
the clamp at both ends, the parameterised floor at the chase caller's −π/2, the wrap under a full
turn, the center key beating a held snap, the smoothing at the decoded rates and its frame-rate
independence, the azimuth chase taking the short arc across the wrap, the idle hook's silence under
input and its deliberate bypass of the floor, and the composition order against A2's unchanged
zero-angle pose. `PilotViewTests` carries the precedence half: a held view key overrides the chase
selection and does NOT override a first-person one, and the look-behind overrides all three.

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed
(`HeadLookTests` and the split `PilotViewTests` precedence cases among them), engine suites 93
passed / 0 failed with errors clean, goldens 15 unchanged plus the `viewer-bhawk` re-pin. The
feel half (pan rate, smoothing, snap directions at the controls) is `BL-436`'s sitting.

**Original approach (kept for reference).**

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

## C22 ☑ Autohead velocity-follow

**Landed.** With no look input in Cockpit, the head leans toward the plane's own sideways and
vertical velocity, scaled and capped per the shipped `player.json` `autohead_*` values; Nose never
does this (mode-gated, not just input-gated); an options-style toggle (default ON) mirrors the
original's engine enable flag.

`PlaneStats.Load` parses the three keys beside the already-parsed `sticky_bullet_*` family
(`docs/formats/vehicle/player-globals.md`'s autohead row), reproducing the loader's own asymmetric
arithmetic exactly: `autohead_turn_time` carries no conversion (shipped 0.75 s, equal to its
compiled default); `autohead_turn_max` is DEGREES in the file, converted ×π/180 THEN DOUBLED
(shipped 2.86° → 0.0998 rad stored) where the compiled fallback (0.1 rad) is *already* the doubled
value and is not doubled again; `autohead_turn_min_pitch` converts once, no doubling (shipped
−3.0° → −0.0524 rad, which happens to equal its own compiled fallback).

`HeadLook.AutoheadTarget` (static, engine-free, the same shape `SnapTargets` already has) is the
rule: only the plane-LOCAL-frame sideways (X) and vertical (Y) velocity components drive the lean.
**Port decision, evidence-gapped:** the forward (Z) component, the plane's own cruise speed (tens
of m/s even unaccelerated), is dropped before scaling. Keeping it in the magnitude cap would let
cruise speed swamp the cap on every ordinary flight, pinning the head dead ahead whether the plane
is flying straight or hard-turning alike, which contradicts this item's own Verify line below (a
bounded lean specifically during a turn/climb/dive). The (X, Y) pair is scaled by `turn_time`,
capped in MAGNITUDE at `turn_max`, and the capped components become (elevation, azimuth)
**directly, not through an arctangent** — an arctangent of a uniformly-scaled vector returns the
same angle at any scale, so capping before one would have no effect on the visible result.
Elevation floors at `turn_min_pitch` directly, which sits below `HeadLook`'s own input-path floor
(level): `HeadLook.Step`'s idle branch (built by C21) already bypasses that clamp for exactly this
reason, so no change was needed there. The method returns null when the lean is negligible or when
the plane is flying dead straight (forward-only velocity reads as no lean at all, since only X/Y
drive it).

`FlightController.Setup` wires `_cam.Head.IdleAim` to a private `AutoheadTarget` method once, at
construction (`Head` lives for the controller's whole life; `_model` is reassigned by every
respawn, not replaced, so the closure stays valid). That method gates on `_cam.ViewMode ==
PilotViewMode.Cockpit` (mode-gated, matching the original's `mode ≠ 7` condition rather than
relying on Nose simply never reaching an idle frame) and a `Config.GetBool("headLook.autohead",
true)` toggle mirroring the original's engine option byte (`DAT_0071dacc`); no decoded evidence
pins that byte's own default state, so ON is a port choice matching the shipped behaviour every
other autohead constant already assumes is live. The local velocity itself is
`_model.Attitude.Inverse() * (_model.VelocityDir * _model.Speed)`, the same
`Basis.Inverse()`-as-world-to-local pattern `FlightModel`/`CameraController`/`Projectile` already
use.

**Tests.** `CSVM.Tests/PlaneStatsFlightGlobalsTests.cs` gained two cases: the loader's asymmetric
arithmetic against the shipped file (pinning 0.75 / 0.0998 rad / −0.0524 rad, and that turn_max
reads back doubled rather than at its un-doubled fallback), and a bare `PlaneStats()`'s
compiled defaults in isolation, no file involved. `CSVM.Tests/HeadLookTests.cs` gained cases for
`AutoheadTarget`: null on negligible or pure-forward velocity, the −3° floor pinned on a hard dive
(the trap this item names — the floor sitting below C21's `[0, π/2]` input floor), the magnitude
cap exercised on a climb well past it, an uncapped small lean passed through unchanged, and the
azimuth sign matching `SnapTargets`' own mirrored convention (drifting right reads negative,
positive is left).

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed (the
loader-arithmetic parse against the shipped file and the −0.0524 rad floor bypass among them),
engine suites 93 passed / 0 failed with errors clean, goldens 15 unchanged plus the `viewer-bhawk`
re-pin. The sub-cap lean divergence is `BL-436`'s sitting.

**Original approach (kept for reference).**

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

## D31 ☑ `cockpit_engine_sound` swap (closes `BL-161`)

**Landed.** The engine slot now swaps onto the plane's `cockpit_engine_sound` (`snd_*_cp`) while
the pilot's SELECTED view is Cockpit or Nose, and back on leaving either; throttle/damage curves
and behaviour are unchanged, only the definition the slot holds differs.

`EngineAudioCurves.EngineDefFor` (now `public`, alongside its containing class, so the rule
headless-tests without an engine) gained a `firstPerson` parameter and became the one place both
of the engine slot's swap conditions are decided: damaged wins when both are live, cockpit applies
otherwise, and either falling through lands on the plain `engine_sound`. **Port decision, evidence-
gapped:** `docs/formats/vehicle.md`'s slot table decodes the damaged swap and the cockpit swap as
two independent rows and does not say which wins when both are live — no shipped def authors a
damaged cockpit variant, so there is nothing to select instead of one or the other, and damage
feedback (already the more load-bearing cue) keeps priority over the cosmetic view timbre change.

`FlightAudio.UpdateEngineSlot` (renamed from `SetEngineDamaged`, generalised to the same shape)
resolves a third candidate stream, `_cockpitStream`, at `Setup` exactly like `_damagedStream`
already was, and re-evaluates the pair whenever EITHER input changes — entering a first-person
view mid-repair, or taking damage mid-cockpit-view, both land on the right def. `FlightAudio.Update`
gained a `bool firstPerson = false` parameter, defaulted so no other call site had to change; its
one live call site,
`FlightController._Process`, feeds `FirstPersonView` — the SELECTED-mode property A1 built for
condition 120 — not the per-frame camera pose, so a held numpad key or look-behind does not
retrigger the swap. `AiEngineAudio` is unreachable in first person by construction (it has no
selected view at all) and needed no change beyond a stale comment fix.

**The transition is a hard cut**, the same shape the damaged swap already used (`Stop`, reassign
`Stream`, `Play` if it was playing): no crossfade is decoded anywhere in this chain, and
`vehicle.md`'s wording ("swapped in… swapped back on leaving") names a swap, not a blend.

**Per-plane authorship.** All 11 player airframes author `cockpit_engine_sound`, either their own
`snd_<name>_cp` or `basic_airplane`'s inherited default (`extracted/zrdr/vehicle.zrd.json`,
confirmed against every `player`/`ai` load in the new data test below); `PlaneStats.CockpitEngineSound`
being `null` is a defensive branch for a plane the shipped install does not actually contain, not a
live case any of the 11 hit. The stale `PlaneStats.cs:261` warning ("nothing selects it here") is
rewritten to name the current wiring.

**Tests.** `CSVM.Tests/EngineAudioModelTests.cs` gained three cases: every one of the 11 airframes
(player and AI loaders both) authors a `cockpit_engine_sound` ending `_cp`; the pure precedence
rule (`EngineDefFor`) picks damaged over cockpit over normal across all four flag combinations, with
`DamagedEnginePitchRandom` held false so the random-draw branch (needing a live
`RandomNumberGenerator`) is untouched by this test; and an airframe missing a cockpit definition
falls back to the normal loop rather than going silent. No new in-engine suite: the wiring at
`FlightAudio.Update`'s one call site is a single-argument pass of an already-tested property
(`FirstPersonView`), and no existing suite exercises the sibling damaged-engine swap end to end
through `FlightAudio.Setup`/`Update` either — this item does not carry new obligation past the
precedent its neighbour already set.

**Verified.** Full battery on the final plan tree: build clean, units 1730 passed / 0 failed
(`EngineAudioModelTests`' all-11-airframes authorship, precedence table and null fallback among
them), engine suites 93 passed / 0 failed with errors clean, goldens 15 unchanged plus the
`viewer-bhawk` re-pin. The listen (presence of the `_cp` def on entering first person, against
`BL-391`'s open level question) is `BL-436`'s sitting.

**Original approach (kept for reference).**

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

## E41 ☑ File the deferred items; close `BL-080`/`BL-161`; fold the decode into `org/cameraViews.md`

**Landed.** Every point this plan deliberately deferred now exists as its own `backlog.md` item —
six new ones plus two updates to entries that already covered part of the same ground, so nothing
duplicates: `BL-431` (in-3D gauge drive, folding in the `POSITION_1ST` HUD-layout question), an
update to `BL-399` (padlock/Track Target, tying the look-state byte's third value to the new
selector item), `BL-432` (the `K`/`J` snap-vs-smooth look-mode selectors), `BL-433` (the numpad
`+`/`−` External Camera Zoom axis), an update to `BL-420` (the engine-wide 62°V→60°H FOV migration,
recording that A3 already landed the mode-6/7 half and narrowing the remaining scope to the three
external-FOV call sites, with the overcast/tracer warning carried forward), `BL-434` (splitscreen
cockpit behaviour), `BL-435` (chase-view look-around, with a pointer added to `BL-150` since its
numpad "fixed views" measurement is this same controller), and `BL-436` (one `[Owed-playtest]` item
bundling the whole cockpit sitting: `InteriorScale`, head-look feel, the azimuth-sign and
autohead-sub-cap port decisions, the damaged-over-cockpit precedence, the `cockpit_engine_sound`
listen, and the Nose head-look confirm). `BL-080` and `BL-161` are deleted per the
close-backlog-item ritual; the closing commit's message carries what settled them, not a doc line.

`docs/org/cameraViews.md` gained the head-look controller's decoded behaviour (states, elevation
convention, snap windows, smoothing rates, the autohead trio and the fixed offset, all three
callers including the chase floor `−π/2`), corrected the mode-7 head-look claim (only autohead is
mode-gated, not player-driven look), fixed the aspect-correction ratio (`(4/3)/liveAspect`, not the
inverse), added the axis-convention caveat that a raw node translate lands in Godot's frame
unswapped, resolved the `markers`/`dontmove` contents in the "Not resolved" list, added the F7
menu-label conflict to the flyby correction block, and updated the "What this means for CSVM" table
rows this plan's own waves now represent.

**Verified.** Docs-only item; the commit hooks are its checks (duplicate-ID, encoding, prose and
comment-cap tripwires all passed on the closing commit), and the full battery on the final plan
tree passed around it: build clean, units 1730 passed / 0 failed, engine suites 93 passed / 0
failed with errors clean, goldens 15 unchanged plus the `viewer-bhawk` re-pin.

**Original approach (kept for reference).**

**Goal.** Every deliberately deferred point exists as a `backlog.md` item; `BL-080` and `BL-161`
are closed per the close-backlog-item ritual; the head-look decode (constants, addresses, the
mode-7 nuance) lives in `docs/org/cameraViews.md` so no future session re-runs it.

**Evidence (confidence: n/a — bookkeeping).** The filed-item list, from Decisions 1/3/4/5 plus
session findings: (1) in-3D gauge drive (and whatever B11's static-gauges TODO decided); (2)
padlock look state (needs targeting's current-target plumbing) — the original binds it as **Track
Target = `L`** (`OriginalScreenshots/Keybinds Views 1.png`), which is the key `docs/controls.md`
already reserves for it under `BL-399`; (3) zoom/lean axis (keys `0x43`/`0x44`, rate 2·dt,
smoothing 1.5/s — constants ready in the plan), and with it the original's **numpad `+`/`−` =
External Camera Zoom In/Out** (`Keybinds Views 2.png`): that pair is the CHASE camera's zoom, not
the cockpit lean, so the filed item carries both and must not confuse them; (4) engine-wide FOV
migration 62°V → 60°H base, carrying the overcast/tracer calibration warning; (5) splitscreen
cockpit behaviour (per-viewport interior cost, per-pilot engine-sound swap under `MixGain`);
(6) HUD `POSITION_1ST` layout variant (the original places gauges differently in first person,
`hud_v2.zrd` keys `POSITION_1ST`/`POSITION_3RD`, `org/cameraViews.md:120-128`) — whether
`GaugeCluster` should reposition in cockpit views; (7) chase-view look-around — the original runs
the same head-look controller for the chase camera with elevation floor −π/2 (`FUN_0042c7f0` →
`FUN_0042d010(0xbfc90fdb, 0)`), which CSVM's chase view lacks entirely; (8) the two **look-mode
selectors**, `K` Access Snap Look Mode and `J` Access Smooth Look Mode (`Keybinds Views 1.png`) —
the original picks snap or free-look by key, where C21 picks by which device moved, so the state
byte's third value (padlock, item 2) and these two selectors are one item's worth of the same
mechanism; both keys are free in CSVM's flight scheme today.

**⚠ `BL-150`'s "fixed numpad views" and this look cluster are the same control.** `BL-150` measured
the original's numpad from `CAP-07` and derived: treat each key as its 2-D offset from `Kp5` on the
numpad grid, sum the offsets of the held keys, and the resultant's DIRECTION selects the camera
position, with a zero resultant giving the default chase pose and `Kp5` alone doing nothing. That
is this item's snap composition exactly, one axis at a time, and `Kp5` "does nothing" because it is
**Look Forward**, the default. The evidence is `Keybinds Views 2.png` plus the data survey's second
caller: `FUN_0042c7f0` runs this same controller for the chase camera with the −π/2 floor. So the
`Views[]` table is not a table at all — it is the head-look direction cluster driving the chase
camera, which is why its measured law is a sum of offsets rather than nine authored poses. Item (7)
and `BL-150` should be read together before either is built.

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
