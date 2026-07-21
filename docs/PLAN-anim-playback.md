# Animation playback — consuming the compiled anim archives (revival-plan item 7)

Working plan for `docs/PLAN-mech3ax-cs-revival.md` **item 7** ("consume in this project"),
planned 2026-07-21 after a format-analysis pass over all 61 extracted archives. Same format
as the other plans: ordered items with goal / evidence / approach / verification, statuses
☐ open · ◐ in progress · ☑ done.

The user's framing for this run: **analyse the animation formats thoroughly and build as
generic a solution as possible** — not a one-off train mover — plus **a free-flight camera
for testing**, so verifying an animation doesn't mean scripting a flight past it each time.

## Framing — what the analysis actually found

**1. The extraction wiring is already done.** `ExtractAssets.ps1` already dispatches
`cam_anim`/`mis_anim` → `unzbd cs anim` → `.zip` (uncommitted working-tree change), and all
61 archives are extracted *and* unpacked under `extracted/` (`C1/cam_anim/`,
`C1/IA1/mis_anim/`, …). Item 7's first bullet is complete before this plan starts; it only
needs committing with the rest of the work.

**2. The fork's def JSON is fully semantic, and much richer than the zrdr source.** Each def
is one JSON file named `<name>-<anim_name>.json` carrying typed fields (`activation`,
`si_script_ids`, `objects`, `nodes`, `puffers`, `reset_state`, `sequences`) and — critically —
**typed events with node references already resolved back to names**, e.g.
`{"ObjectActiveState": {"node": "snd_train", "state": true}}`. This is a decoded AST, not a
key/value soup: it is a far better VM input than re-parsing the zrdr readers.

**3. But the compiled archives do NOT subsume the zrdr readers — the merge is forced.**
Measured: `C1/IA1/mis_anim` holds 160 defs, all of them the eight zeppelin
`ANIMATION_DEFINITION_FILE`s its `mis_anim.json` lists. `zepstate` (which deactivates
`dliner1`/`cargotrain`) and `startanims` are **never compiled into any archive** — they are
mission-zrdr readers the engine reads at runtime. So `MissionState`'s existing zrdr path
cannot simply be replaced; a generic engine has to merge two front-ends. This is a
constraint, not a design preference.

**4. Event vocabulary (surveyed across all 61 archives): 35 types, 14,963 defs, 1,090
scripts.** By frequency:

| count | event | count | event |
|---|---|---|---|
| 66,081 | ObjectActiveState | 3,389 | Loop |
| 54,390 | CallAnimation | 2,209 | ObjectScaleState |
| 22,391 | CallSequence | 1,468 | LightState |
| 9,917 | ObjectOpacityFromTo | 1,244 | SoundNode |
| 9,421 | ObjectMotionFromTo | 1,163 | ObjectAddChild |
| 7,442 | ObjectMotion | 1,143 | StopSequence / ObjectTranslateState |
| 6,668 | StopAnimation | 845 | **ObjectMotionSiScript** |
| 6,610 | ObjectRotateState | 736 | Callback |
| 6,091 | Sound | 535 | LightAnimation |
| 5,727/5,468/5,468/4,680 | Elseif / If / Endif / Else | 340 | ObjectOpacityState |
| 5,332 | InvalidateAnimation | 263 | ObjectDeleteChild |
| 4,387 | PufferState | 155/144 | FbfxColorFromTo / ObjectCycleTexture |
| | | 22/11/10/3/3/1 | MotionSiScriptAllNames / ResetAnimation / CameraState / SoundAdjust / DetonateWeapon / FogState |

Activation split: `OnCall` 11,773 · `WeaponHit` 2,565 · `OnStartup` 581 ·
`WeaponOrCollideHit` 44.

**5. It is a real timeline VM, not a state list.** Every event carries an optional
`start: {offset, time}` with `offset` ∈ `Animation` (2,122) · `Event` (32,534) ·
`Sequence` (434); absent (174,938) means "immediately after the previous event". Sequences
carry `seq_state` ∈ `Initial` (30,205, runs when the anim runs) / `OnCall` (20,074, only via
`CallSequence`). Control flow is `If`/`Elseif`/`Else`/`Endif` over typed conditions
(`AnimHealth`, `RandomWeight`, `NodeActive`, `NodeBelowAlt`, …) plus `Loop{Count}` with −1 =
forever. So the runtime is: schedule events on a clock, evaluate conditions, follow calls.

**6. Load-bearing gotcha — the `.zan` rotate quaternion fields are mislabelled.** mech3ax
reads the file's `(w, x, y, z)` float order straight into a `#[repr(C)] struct Quaternion
{x, y, z, w}`, so in the emitted JSON **real `w` = json `x`, real `x` = json `y`, real `y` =
json `z`, real `z` = json `w`**. Harmless for mech3ax (the bytes round-trip either way),
fatal for playback. Verified on the C1 train: under that remap every base quaternion is
unit-norm and the yaw tracks the frame-to-frame chord heading to ~1° (frame 1 quat-yaw
−33.11° vs chord −43.12° with the frame's own −0.0505 rad/s rate spanning it; read literally
the values are not even normalised). Also confirms the docs' `q(t) = exp(f(t)) ⊗ base`
composition end-to-end.

**7. Positions are world-space and match the gamez nodes.** The C1 passenger engine's first
frame base is `(−6943.27, 128.00, −5456.80)` — the parked consist position already recorded
in `docs/formats/anim-definitions.md`. No frame conversion needed beyond the quaternion
remap; the project's coordinate frame is already the game's.

## Architecture — the generic solution

Three layers, so that "add another event type" never means touching the engine, and so the
two sources stay interchangeable:

```
  zrdr readers ──► ZrdrAnimSource ──┐
                                    ├──►  AnimProgram  ──►  AnimRuntime  ──►  world nodes
  cam_anim/mis_anim ──► CompiledAnimSource ──┘   (defs, sequences,        (binding, clock,
                       (+ SiScript pool)         typed events)             dispatch table)
```

- **`AnimProgram`** — the single in-memory model: defs (name / anim name / root / activation /
  flags), each with a reset state and named sequences of typed `AnimEvent`s, plus the SI-script
  pool. Source-agnostic.
- **Two front-ends.** `CompiledAnimSource` reads the fork's extraction (the primary, richer
  source, and the only one with scripts). `ZrdrAnimSource` is today's `AnimDefs.cs` parser
  re-targeted at the same model, kept because `zepstate`/`startanims` exist nowhere else.
  Merge rule: compiled wins on name collision (it carries script ids and resolved refs);
  zrdr-only defs are added.
- **`AnimRuntime`** — the VM: binds defs to built world nodes (reusing `MissionState`'s
  proven `cs_name`/wildcard resolution), keeps a per-instance clock, schedules events off
  their `start` offsets, evaluates conditions, and dispatches each event through a **handler
  table keyed by event kind**. Unhandled kinds are counted and logged once per kind, never
  fatal — that is what makes partial implementation honest and incremental.

**Scope of this landing (user decision):** the full architecture, with live handlers for the
**state and motion** events — `ObjectActiveState`, `ObjectTranslate/Rotate/ScaleState`,
`ObjectMotionFromTo`, `ObjectMotion`, `ObjectMotionSiScript`, plus the control flow
(`If`/`Else`/`Loop`/`CallSequence`/`CallAnimation`/`StopAnimation`). Sound, puffer, light,
opacity, texture-cycle, FBFX and camera events dispatch through the same table as logged
no-ops; they get real handlers in later runs without the engine changing shape.

**Relationship to `MissionState`:** it is absorbed, not deleted. Its passes 1–3 become the
runtime's "apply base states, then run `ON_STARTUP` and `startanims` animations" bootstrap,
and its pass-4 safety net stays as-is. The `cs_name` index, wildcard matcher and
`SetSubtreeActive` move into the runtime unchanged — they are the parts that already work
and are user-verified.

## Checklist

1. ☑ **Spectator free camera** (`--freecam`) — the testing enabler, built first and
   independent of everything else. A world build with no aircraft: free-flying camera
   (WASD + mouse look, speed control, vertical), the world and its animation clock running.
   *(`src/Flight/SpectatorCamera.cs`; starts at the mission spawn, `--campos`/`--lookat`
   override. Static viewer verified byte-identical after the camera-framing gate changed.)*
2. ☑ **`CompiledAnim.cs`** — reader for the fork's anim extraction (zip or unpacked dir, like
   every other loader): defs, the SI-script pool, `metadata.json`. Includes the quaternion
   remap at the parse boundary so nothing downstream can get it wrong. *(Scripts load lazily;
   payloads stay a generic property bag, so a new event type costs the reader nothing.)*
3. ☑ **`AnimProgram`** — the unified model + both front-ends + the merge, with the zrdr parser
   re-targeted onto it. *(Compiled wins on collision; `AnimDefs.cs` normalizes reader ops into
   the same event vocabulary via SNAKE_CASE→PascalCase.)*
4. ☑ **`AnimRuntime`** — binding, clock, scheduling, control flow, dispatch table; state
   handlers live; `MissionState` absorbed and deleted.
5. ☑ **Motion handlers** — `ObjectMotionFromTo`, and `ObjectMotionSiScript` frame playback
   (cubic interpolation for translate, `exp(f(t)) ⊗ base` for rotate).
   **Scope correction against the plan's original wording:** `ObjectMotion` (7,442 uses) was
   listed here but is *not* implemented. It is the debris-scatter primitive (gravity, bounce,
   ranges, morph) and every one of its uses sits in a destruction sequence reached only by
   `WeaponHit`/`ON_CALL` — unreachable at mission start, so implementing it now would have been
   substantial work with no observable effect. It dispatches and is counted like the other
   deferred kinds.
6. ☑ **Wire into the world build** + regression pass across all 8 chapters.
7. ☑ **Docs** — `docs/formats/anim-definitions.md` gained a "Consuming the extraction" section,
   CLAUDE.md module index + args + status, `backlog.md` entry closed, `docs/HISTORY.md` entry.

### Open / follow-ups

- **`PufferState` landed 2026-07-21** (user request, same day): `Puffer` gained a third emission
  mode (`SustainAt` — continuous `TIME_INTERVAL` emission at a moving node) and the runtime keys
  one emitter per (puffer name, host node). User-confirmed in-game: the C1 train trails steam.
- **The still-deferred event kinds** (one `case` each in the dispatch table): `LightState`/
  `LightAnimation` (518 in C1), `ObjectOpacityState`/`ObjectOpacityFromTo` (58), `Sound`/
  `SoundNode` (39), `ObjectAddChild` (39), `Callback` (8), `ObjectCycleTexture`,
  `FbfxColorFromTo`, `CameraState`, `ObjectMotion`. Recorded in `backlog.md`. (These counts are
  an order of magnitude below the first measurement because the zero-duration `Loop` busy-spin
  was fixed — the waterfall's `[PufferState ×3, Loop{-1}]` had been re-running every frame.)
- **`If`/`Elseif` branches are skipped, not evaluated** (3,258 in C1). Their conditions are
  gameplay state (`ANIM_HEALTH`, `RANDOM_WEIGHT`, `NODE_ACTIVE`, …) that an at-rest world build
  has no value for; guessing would silently pose objects wrongly, so the branch body is skipped
  and counted. `RANDOM_WEIGHT` and `NODE_ACTIVE` are both evaluable and would be the place to
  start.
  **SUPERSEDED 2026-07-21** — this reading was wrong. A survey of all 16,195 conditions in the
  install found ten kinds and every one of them answerable; all ten now evaluate. See
  `docs/PLAN-anim-rendering-followups.md` item 1 and `docs/formats/anim-definitions.md`.
- **The safety net hides 54 uncovered `destroyed` subtrees, where the old code hid 53.** One
  more definition's coverage moved when compiled defs replaced their reader twins. No visible
  difference (the net exists for exactly this and hides it either way), but it is one
  unexplained delta in a coverage number, so it is written down rather than rounded off.
- **596 live instances persist in C1** (looping or long-scheduled sequences). No measurable
  frame cost was observed, but this has not been profiled on a weak GPU/CPU.
- Spectator camera playtested 2026-07-21 (user: "feels workable"). One bug found and fixed: F11
  printed a look-at of the world origin in `--freecam`, because `PrintCameraPose` branched on
  `_fly` alone and fell into the orbit branch, whose `_orbitCenter` freecam never sets.
- **Open question for the user: should gamepad reads be gated on window focus?** Godot polls pads
  regardless of focus (unlike keyboard/mouse), so a controller moved while the game is alt-tabbed
  still drives it — this was mistaken for a camera drift bug before the user identified the cause.
  It affects `FlightController` and `MenuInput` as much as the spectator camera, so it is a
  project-wide input-policy decision, not a local fix.

## Verification bar

- C1/IA1's train visibly runs its track loop in `--freecam` and `--fly`; the loop period
  matches the surveyed ~327 s and the consist starts at the parked position.
- The two fueltrucks move; `cpilot_eject` is not regressed into view.
- **Regression is the real risk**: the runtime replaces the code path that currently hides
  destroyed variants and phantom zeppelins. A bad decode mis-poses or reveals objects that
  were fine. So: `--fly` smoke across all 8 chapters, zero errors, and a before/after
  screenshot pair at the C1 airfield (no destroyed-overlay flicker) and the C1/IA1 zeppelin
  shed (empty).
- Static `--viewer` screenshots stay byte-identical (md5) — the runtime must not touch the
  parked-plane path at all.
