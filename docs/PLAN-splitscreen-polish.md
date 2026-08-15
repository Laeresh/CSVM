# Splitscreen polish — retire the single-player assumptions

**ACTIVE PLAN** (written 2026-08-15). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan schedules the entire Splitscreen backlog theme created 2026-08-15: the four
single-viewer draw bugs (`BL-338` class: `BL-339`, `BL-340`, `BL-366`), the gameplay and audio
player-one singletons (`BL-365`, `BL-367`, `BL-370`, `BL-371`), the input gaps (`BL-372`,
`BL-373`, `BL-374`, `BL-375`), the four user decisions embedded in them (`BL-368`, `BL-369`,
`BL-372`, `BL-373`), the debug-tooling cleanup (`BL-376`), and the owed chrome playtest
(`BL-126`). Provenance: every item except `BL-126`, `BL-338`, `BL-339` and `BL-340` was minted
2026-08-15 from a same-day code sweep (three parallel read agents over the tree plus spot checks),
so re-verification against the code is fresh by construction. Of the older four: `BL-339` was
re-confirmed at the controls 2026-08-15 (shooting at P1 from behind, the trail streaks appear only
as the rocket passes P1); `BL-338`/`BL-340`/`BL-126` were re-confirmed open by the same sweep
reading their cited code sites.

**Out of scope, deliberately:** per-pane fog (`BL-338` names fog-zone selection, but per-instance
fog uniforms are a shader-architecture change; B14 documents the verdict instead of fixing it),
the MP map decode (`BL-299`), Dogfight match options (`BL-301`), the race countdown (`BL-314`),
the per-player ActionMap (`BL-296` — E-wave items add bindings through today's polling seam and
note the ActionMap as the eventual home), and everything in later milestones (cockpit view,
campaign).

## Milestone goal

- A 2–4 player session shows each pane its own correct picture: smoke trails, world lights and
  screen washes answer to that pane's camera and player, not player 1's.
- The world plays the same for every human: proximity-triggered animations and AI attention
  respond to the nearest human, not the first one assembled.
- The audio mix survives four players: one-shots respect the per-player gain and a deliberately
  chosen listener model, instead of engine defaults and raw def volumes.
- Every player can control their own seat: working pad assignment, camera views, look-back,
  spectating, and a decided pause behaviour.
- **The boundary: no new per-pane geometry unless a nearest/union rule was tried and visibly
  fails.** Per-pane state multiplies instance cost by pane count (`BL-338`'s ⚠); the tracer fix
  proved nearest is usually enough.

## Decisions (2026-08-15)

| # | Question | Decision |
|---|---|---|
| 1 | Where do splitscreen fixes read "the player" / "the camera" from? | **The two existing seams, never `_rigs[0]` or `GetViewport().GetCamera3D()`** — `GameSession.PlayerPositions` (nearest human) for gameplay rules, the viewer set behind `ProjectilePool.Viewers` / `ScreenSize.NearestFloor` for draw rules. A3 promotes the latter to a session service. |
| 2 | Do sim-state singletons get the same treatment? | **No — sim state stays global** (mission wind steps once per frame; per-rig stepping doubles gust speed). Only draw rules and per-player gameplay/audio owe each pane its own answer. |
| 3 | Are the four open design questions pre-decided by this plan? | **No — each is a plan item whose first step is the user's call** (A1 team model, A2 listener, E42 view binding, E43 pause). The plan records the option space and a recommended default, not the decision. |
| 4 | Where do these items live in the backlog? | **A dedicated Splitscreen theme** (created 2026-08-15), the one cross-cutting exception to the straddler rule, because several items are decisions rather than bugs and the class would have blown up `BL-338`. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Billboards are all oriented to player 1 and need per-player copies" | Code sweep 2026-08-15: every world billboard orients in the shader (`INV_VIEW_MATRIX` / `CAMERA_POSITION_WORLD`, per-view built-ins evaluated once per camera) or via Godot `BillboardMode`; a repo-wide grep found no CPU-side rotate-toward-camera in the world render path. The at-the-controls symptom was `BL-339`'s puffer cull, confirmed same day. Do not add per-player billboard nodes. |
| 2 | "The whole-window `GetViewport().GetCamera3D()` fallback works in splitscreen" | In splitscreen the main viewport's camera has `Current = false` (`GameSession.cs:2979`), so the call returns null or a non-rendering camera. `MarkerOverlay.Relayout` silently never runs because of it (F51). Treat any `GetViewport()` in a draw path as a bug signal (`BL-338`). |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code** | B11, B12, B13, C21, C22, D31, D32, E41, E44, F51 | Cited file:line came from same-day agent reads plus spot checks; confirm the line still matches, then implement. |
| **Direction sound, magnitude a judgement call** | A3 (seam shape), F52 (all TUNE constants) | The *what* is settled; constants and API shape need judgement at implementation. |
| **Decision first, then small implementation** | A1, A2, E42, E43 | Budget a user round-trip before code; the implementation is small once decided. |
| **Documentation verdict, not code** | B14 (fog + residual sweep) | May legitimately end in "recorded as deliberate" rather than a fix. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

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

### Wave A — Decisions and the seam

1. ☑ Decide the team model for plain splitscreen free flight (`BL-368`)
2. ☑ Decide and pin the 3D audio listener model (`BL-369`)
3. ☑ Promote the viewer set to a session service

### Wave B — Draw rules (`BL-338`'s class)

11. ☐ Puffer distance fade answers every pane (`BL-339`)
12. ☐ Screen wash routed to the hit pane(s) (`BL-340`)
13. ☐ World point lights budgeted against the nearest rig (`BL-366`)
14. ☐ Close out `BL-338`: residual sweep + fog verdict recorded

### Wave C — Gameplay "the player" rules

21. ☐ `PLAYER_RANGE` measures from the nearest human (`BL-365`)
22. ☐ AI `primary_target = "player"` resolves per attacker, not to P1 (`BL-367`)

### Wave D — Audio one-shots

31. ☐ Projectile one-shots get mix gain and a distance term (`BL-370`)
32. ☐ `FlightAudio` one-shots respect `MixGain` where they should (`BL-371`)

### Wave E — Seats and input

41. ☐ Pad assignment follows the phantom-device policy (`BL-374`)
42. ☐ Camera views and look-back for players 2–4 (`BL-372`)
43. ☐ Splitscreen pause (`BL-373`)
44. ☐ Per-seat spectator control (`BL-375`)

### Wave F — Cleanup and playtest

51. ☐ Debug tooling binds the right pane or documents P1-only (`BL-376`)
52. ☐ Splitscreen chrome playtest (`BL-126`)

## Dependency and parallelism notes

A1 and A2 are user decisions and block nothing else in their wave; A2 blocks D31/D32 (the one-shot
policy depends on the listener model). A3 blocks B11 and B13 (both consume the viewer-set service).
B12 is independent of A3 (its routing question is dispatch, not cameras). C21/C22, E41, E44 and F51
are independent of everything and can run in parallel worktrees. E42 and E43 need their decisions
(inside the items) first and both touch `FlightController`/`FlightRigAssembler` input plumbing —
run them sequentially, not in parallel. File contention: B11 and B13 both consume A3's service but
edit different modules (`Puffer`/`WeatherRig` vs `WorldLights`/`AnimRuntime`); D31 and D32 both
touch the new one-shot path — keep D31 → D32 sequential. F52 is last: it playtests the landed
state.

---

# Wave A — Decisions and the seam

## A1 ☑ Decide the team model for plain splitscreen free flight (`BL-368`)

**Goal.** A decided, wired default for who is hostile to whom in a plain `--players=N --fly`
session, plus a way to opt into the other mode.

**Evidence (confidence: traced).** `AimAssist.TeamOfPilot` defaults each pilot to their own team
(`AimAssist.cs:317`); `FlightController.Team` falls back to it (`FlightController.cs:507`); only
Instant Action overrides via `AimAssist.PlayerTeam` (`FlightRigAssembler.cs:114`). Consequence
today: aim assist snaps onto your friend, world turrets acquire every human, AI gunners treat the
humans as separate enemies.

**Decision (2026-08-15, user call).** FFA stays the plain-flight default — no behaviour change to
`AimAssist.TeamOfPilot`'s per-pilot fallback. `--coop` is the opt-in: a new `SessionSpec` flag that
gives every human `AimAssist.PlayerTeam` in a plain `--fly`/`--stunt` session, wired at the same
site Instant Action already uses (`FlightRigAssembler.Assemble`: `if (_in.InstantActionActive ||
_in.Coop) controller.Team = AimAssist.PlayerTeam;`). `--vs`'s FFA is explicit and outranks it —
`SessionSpec.Resolve` drops `Coop` with a warning when `Versus` is also set, so the two flags never
race at the assembler.

**Approach (landed).** `SessionSpec._coopArg`/`--coop` parses directly (not a mode vote, so it
carries no `HasContentArg`); `Resolve()` sets `Coop` alongside `Stunt`/`Versus` and clears it with a
`Warn("core", …)` when `Versus` is also true. `GameSession.BuildFlightRigs` copies `_spec.Coop` into
`FlightRigAssembler.Inputs.Coop`; `Assemble` ORs it into the existing `InstantActionActive` branch
that sets `controller.Team`.

**Model recommendation.** medium — small wiring once decided.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean. Added three `SessionSpecTests`
cases (`--coop` parses, defaults false, and `--vs` drops it with a warning in either flag order).
`.\RunTests.ps1`: 59/59 engine suites green (`team-model`/`aim-assist`/`world-turrets` unchanged —
this item reuses their proven `controller.Team` seam, not a new mechanism), 1289/1289 unit tests,
14/14 goldens hash-identical. Scripted probes via `.\RunProbe.ps1` (no hardware controllers on
hand, so the plan's at-the-controls repro is substituted with a scripted boot + log check):
`--players=2 --fly --coop`
boots clean, no new warnings/errors beyond the pre-existing `pir_spinner.tif` texture-absent line;
`--players=2 --vs --coop` logs `WARN [core] --coop has no effect with --vs (its FFA is explicit);
ignoring --coop` and boots clean, confirming the precedence rule fires. Aim-assist/turret behaviour
itself was not re-verified live (that's what `team-model`/`aim-assist`/`world-turrets` already pin
for the `Team` field this change writes to) — an at-the-controls pass with two pads is still owed
and folds into `F52`'s playtest.

**⚠ Traps.** Do not gate aim assist on `PlayerIndex == 0` while in there — the assist follows
whichever human the AI engages, decided M4 (architecture.md's AimAssist ⚠).

## A2 ☑ Decide and pin the 3D audio listener model (`BL-369`)

**Goal.** The session sets its listener(s) explicitly; who hears the 3D world in splitscreen is a
recorded decision, not an engine default.

**Evidence (confidence: traced).** No `AudioListener3D` exists in the repo; `SplitScreen.cs:158`
builds the SubViewports without `AudioListenerEnable3D`; in splitscreen the main camera is not
current either (`GameSession.cs:2979`). `WorldSounds.cs:226` records "the listener is still player
one only"; M2.5 decided "no positional audio" for player one-shots; PLAN-M4-ai lists the listener
as a known player-one singleton.

**Engine behaviour (verified 2026-08-15, before the decision round).** Read verbatim off the Godot
`4.7` tag (our binary is `Godot_v4.7-stable_mono_win64`); the class docs cover none of it.
`AudioStreamPlayer3D::_update_panning` builds its listener set as `World3D::get_cameras()` ∪
`get_viewport()->get_camera_3d()`, keeps only those whose viewport `is_audio_listener_3d()`, and
combines them with `_apply_max_volume_from_vector` — the per-channel **MAXIMUM**, not a sum, so
there is no N-fold buildup and the plan's "1/√N mix" premise was wrong. `max_distance` culling is
per listener (`continue` skips that listener alone) and the attenuation lowpass takes the max too.
With no listener the volume vector stays `AudioFrame(0,0)` and `bus_volumes` is cleared: silence.
`Camera3D` joins the `World3D` set on `NOTIFICATION_BECAME_CURRENT` and leaves it on
`NOTIFICATION_LOST_CURRENT`. Consequence for the rig as it stood: the main camera stands down
(`GameSession.cs:2980`) and the panes never set `AudioListenerEnable3D`, so a 2–4 player session had
**no listener at all** and every `AudioStreamPlayer3D` was silent — not P1-pinned, as
`WorldSounds.cs:226` assumed. Option (a) was therefore never "today's accident".

**Decision (2026-08-15, user call).** **Per-pane listeners**: every pane `SubViewport` sets
`AudioListenerEnable3D`. The engine's max rule then means an emitter is heard at its NEAREST pane's
volume with no manual attenuation, which is the plan's own nearest/union boundary rule applied to
audio. Accepted cost: panning is unioned across panes (a sound to P1's left and P2's right comes out
of both sides), the panes sharing one stereo out.

**Approach (landed).** One property in `SplitScreen.Init`, plus the record in
`docs/architecture.md` (`UI/SplitScreen` carries the model, `Mech3/WorldSounds` points at it) and a
`splitscreen-listeners` suite that pins all three pane counts and proves the default it overrides
(a fresh `SubViewport` is not a listener). `WorldSounds.SetListener(Func<Vector3>)` became
`SetListeners(Func<IReadOnlyList<Vector3>>)`, fed by a new `WorldSession.Options.ListenerPositions`
off the rig cameras, so the `--debug-anim` sound line reports range to the NEAREST listener and
names its pane. That column was F51's to relabel; it is done here because A2's own verify needs it.

**Model recommendation.** high — the option space needs engine-behaviour research and the outcome
shapes D31/D32.

**Verify (done 2026-08-15).** `.\RunTests.ps1`: 1289/1289 units, 60/60 engine suites (the new
`splitscreen-listeners` included), 14/14 goldens hash-identical — the 1P path is untouched by
construction (it never builds this rig). Scripted probes through `.\RunProbe.ps1` on C1, exit
condition per SHELL-12 (`--screenshot=` + `--frames=150`, never a bare `--debug-anim`):
`--players=4 --fly --chapter=C1 --debug-anim` logs `sound: 4 listeners at (-7034, 331, -5548),
(-7776, 376, -5879), (-6576, 266, -4138), (-2465, 205, -4028)` and resolves `snd_waterfall` to
**1488 m (P3)** against its own 1200/1500 m RANGE while P1 sits 2282 m away — past the emitter's
`max_distance`, so under a P1-pinned listener that waterfall is culled for everyone and under the
pinned model P3 hears it. `snd_train`/`snd_police` resolve to P1 in the same block, so the column
is discriminating, not a constant. The 2P run shows the same shape with two listeners.
**What this cannot show:** the log reads our own `Play()` state, never audibility — the mix itself
is the user's to judge (verification.md's "what this project cannot verify itself"), and the
2026-07-30 playtest's "per-pane audio mix is fine by ear" was almost certainly about the
non-positional `FlightAudio` loops, which no listener change touches. F52 settles it at the controls.

**⚠ Traps.** `--mute` is load-time and blind (nothing is counted or logged); use `--volume=0` for
baselines. Do not read the `--debug-anim` sound `dist` column as a per-pane audio *level*: it is
range to the nearest listener, and the engine's own attenuation curve sits between it and loudness.

## A3 ☑ Promote the viewer set to a session service

**Goal.** One session-owned "all pane cameras" service answering nearest-distance / nearest-depth
queries, so draw rules stop caring how many panes exist.

**Evidence (confidence: traced).** The pattern exists and is proven: `ProjectilePool.Viewers` +
`ScreenSize.NearestFloor` (bound at `GameSession.cs:1747`, pinned by `TracerScreenSizeTests`),
chosen over per-pane geometry when the tracer floor was fixed 2026-08-10
([`docs/org/tracers.md`](../docs/org/tracers.md)).

**Approach (landed).** `src/Flight/ViewerSet.cs`: a small sealed class holding a bound
`List<Camera3D>`. `Bind(IEnumerable<Camera3D>)` replaces the set wholesale (never incremental).
Three read shapes, resolving the TODO by building only what the two named consumers need: `Cameras`
(the raw unfiltered list — `ProjectilePool.TracerFloor`'s shape today, since it already needs each
viewer's own FOV/pane-height alongside position and already skips a freed instance per sample),
`Positions()` (position only — B13), `Poses()` (position + `-Basis.Z` forward — B11's view-space
depth). `GameSession` owns one instance (`_viewers`), `Bind`s it once right after `BuildRigs`
returns (mirroring the exact `_rigs.Count > 0 ? … : _camera != null ? … : Array.Empty<Camera3D>()`
fallback the old per-pool loop had), and assigns it to `ProjectilePool.Viewers` — now a
`ViewerSet { get; set; }` instead of an owned `List<Camera3D>` — in place of the per-rig `.Add` loop
at the pool's build site. The standalone weapon-bench pool (`GameSession.cs`'s `--weapon-test`
path) and every `Suites.cs` lab pool get a fresh unbound `ViewerSet` by construction (the property's
default), so their "no viewers ⇒ no floor" behaviour is untouched. `PlayerPositions` (the gameplay
seam) is untouched, per the ⚠ below.

**Model recommendation.** medium — refactor with an existing test pinning the behaviour.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, `dotnet format --verify-no-changes`
clean. `.\RunTests.ps1`: 1289/1289 units, 60/60 engine suites (`TracerScreenSizeTests` untouched —
it pins `ScreenSize`'s statics only, which did not move — and `puffer-distance-fade`/
`splitscreen-listeners` unaffected), 14/14 goldens hash-identical, engine errors clean — a pure
refactor by construction (`TracerFloor` reads `Viewers.Cameras` instead of `Viewers` directly, same
filter, same `ScreenSize.NearestFloor` call). Scripted probe: `--players=2 --fly --chapter=C1
--screenshot=… --frames=60` boots clean, no warnings or errors in the log at all (not even the
pre-existing `pir_spinner.tif` line, which this run's asset selection didn't hit).

**⚠ Traps.** Do not fold `PlayerPositions` (gameplay seam) into it — one answers "where are the
humans", the other "what do the cameras see"; C21 keeps consuming `PlayerPositions`.

# Wave B — Draw rules (`BL-338`'s class)

## B11 ☐ Puffer distance fade answers every pane (`BL-339`)

**Goal.** A smoke trail near any player is visible in that player's pane, whatever P1 is doing.

**Evidence (confidence: traced).** `WeatherRig.Tick` publishes one camera pose
(`_ambience.SetCamera(rigs[0].Camera…)`, `WeatherRig.cs:295`); `Puffer` computes `DistanceAlpha`
per particle against it and writes the alpha into the one `MultiMesh` every pane draws
(`Puffer.cs:711`, `:886`). The unauthored `NEAR_FADE (0,0)` default makes the near band a hard
cull at view-space depth 0, so everything behind P1's camera is dropped for everyone. Confirmed at
the controls 2026-08-15: shooting at P1 from behind, trail streaks appear only as the rocket
passes P1. Decode: [`docs/org/puffer.md`](../docs/org/puffer.md) — the fade is a DRAW rule in the
original, and a view-space depth, not a euclidean range.

**Approach.** Take the nearest-viewer rule, matching the tracer precedent: per particle, evaluate
`DistanceAlpha` against the viewer (from A3) whose view-space depth is most favourable, i.e. a
particle is drawn if any pane should see it, with the alpha of its nearest viewer. One `MultiMesh`
stays one `MultiMesh`. Per-pane alpha would need N MultiMeshes (the true-fidelity option the
backlog entry names); reach for it only if the nearest rule visibly fails at the controls — record
the verdict either way (`BL-338`'s rule).

**Model recommendation.** high — per-particle hot path plus a fidelity judgement.

**Verify.** The `puffer-distance-fade` suite still pins the decoded bands (single-viewer case must
be bit-identical). At the controls, the `BL-339` repro: two players, P2 astern of P1, P2 fires a
rocket — P2 sees the full trail. <TODO: whether a scripted 2-pane screenshot can assert this
(splitscreen under `--screenshot=` is untested territory); if not, it is an at-the-controls
verify.>

**⚠ Traps.** Do not disable the fade — the near cull stops a camera inside an emitter from filling
the screen, and the suite pins the decoded bands against C3's own emitters. The fade is view-space
depth along the camera forward, not distance; "nearest" must compare per-viewer depths, not ranges.

## B12 ☐ Screen wash routed to the hit pane(s) (`BL-340`)

**Goal.** An `FBFX_COLOR_FROM_TO` wash paints the pane(s) of players near the burst, not all four.

**Evidence (confidence: traced).** `ScreenFlash` builds one `ColorRect` per view (correct) but
holds one ramp state, and `Play` paints every rect (`ScreenFlash.cs:28`, `:108`); fired from
`AnimRuntime` via `GameSession.cs:444`. The original keeps a single frame-buffer-effect object
whose second burst replaces the first (`crimson.exe` 0x9c8a98); replace-not-composite is decoded
and must survive per pane.

**Approach.** Per-pane ramp state (the fields become one struct per view) plus targeting on the
play call. The routing is the substance: the dispatch site that knows the aircraft and distance is
`ProjectilePool`'s blast/impact path, not `AnimRuntime`. Decide there between "the hit player
only" and "every player within the burst's own radius" — the second is truer to what the effect
is, the first is what was reported; the second needs a distance term. <TODO: that design call, at
implementation time, with the burst radius data in hand.>

**Model recommendation.** high — the dispatch-site derivation crosses `ProjectilePool`,
`AnimRuntime` and `GameSession`.

**Verify.** Extend the `fbfx-flash` suite to assert the right pane's ramp through the new per-pane
seam (its current `ScreenFlash.Current` readout changes; extend, do not delete the timing checks).
At the controls: 2 players apart, rocket hit near P2 — only P2's pane washes.

**⚠ Traps.** Keep replace-not-composite within each pane. The gutters and the empty 3P quadrant
must stay out of the wash (the per-view rect build already guarantees this; don't regress it).

## B13 ☐ World point lights budgeted against the nearest rig (`BL-366`)

**Goal.** A burning refinery next to P4 spills light in P4's pane even with P1 far away.

**Evidence (confidence: traced).** `AnimRuntime.cs:3211` calls `WorldLights.Commit(PlayerPos())`;
`WorldLights.cs:94-118` fades 900–1500 m against that single position and `Significance`-ranks
into the 16-slot `csky_light_data`/`csky_light_count` globals every pane reads.

**Approach.** Fade and rank each light against its nearest viewer (A3's service, positions
suffice). The uniform stays global — a light lit for one pane is lit for all, which is correct
(light exists in the world); only the selection was wrong. `MaxActive` is rarely binding, so the
union costs little; if slot pressure appears with 4 spread-out players, that is a new TUNE item,
not this one.

**Model recommendation.** medium — contained change with a clear rule.

**Verify.** Scripted: a 2-player-position `Commit` unit case if `WorldLights` is testable
engine-free, else a `--debug-anim` run asserting a far-from-P1 light stays committed. <TODO: check
whether `WorldLights` has an existing test seam before choosing.> Golden hashes: single-player
shots unchanged (nearest-of-one is identical).

**⚠ Traps.** Keep the fade curve and `Significance` arithmetic untouched; only the reference
position generalises.

## B14 ☐ Close out `BL-338`: residual sweep + fog verdict recorded

**Goal.** `BL-338`'s sweep list is finished: every named site has a verdict (fixed here, recorded
as deliberate, or split into its own item), and the umbrella item closes.

**Evidence (confidence: traced for the surveyed sites).** The 2026-08-15 sweep already gave
verdicts for most of the list: precipitation, cloud clutter, `FogVolumeClutter` far fade,
`EmitterRenderer`, lens flare, whiteout, deck regime, zone cull masks are per-pane-correct;
`SelectionService` and the labs are single-camera by design (unreachable in splitscreen). Fog-zone
selection remains genuinely wrong (`WeatherRig.cs:457` drives global fog uniforms from rig 0,
self-documented) and per-pane fog needs per-instance uniforms.

**Approach.** Walk `BL-338`'s to-audit list once more against the landed B11/B12/B13: LOD bands,
the cloud whiteout, the lens flare sun wash, `SelectionService`, any remaining `GetViewport()` in
a draw path. Record each verdict in `docs/architecture.md` module entries (one line each, per the
⚠ budget). For fog: record "shared, P1-driven, deliberate until per-instance fog uniforms" as the
verdict and mint a new blocked backlog item for per-pane fog so the thread stays visible. Then
close `BL-338`.

**Model recommendation.** medium, low effort — mechanical audit over an existing checklist.

**Verify.** The closing commit's message carries the site-by-site verdict table; `.\RunTests.ps1`
green (this item should land no behaviour change beyond docs unless the audit finds a straggler).

**⚠ Traps.** The instinct to fix fog while in there — resist it; per-instance fog uniforms have a
recorded hazard (`csky_instance_uniforms.gdshaderinc` ordering) and deserve their own plan.

# Wave C — Gameplay "the player" rules

## C21 ☐ `PLAYER_RANGE` measures from the nearest human (`BL-365`)

**Goal.** Proximity-triggered world animations respond to whichever human is near them.

**Evidence (confidence: traced).** `AnimRuntime.cs:3399` evaluates `PLAYER_RANGE` via
`PlayerPos()` (`:3632`), fed `_rigs[0].Camera.GlobalPosition` (`GameSession.cs:863`). The sibling
`EXECUTION_BY_RANGE` gate already uses the nearest-human `PlayerPositions` seam
(`AnimRuntime.cs:2025`, `GameSession.cs:869`); this condition was left behind. The rig-0 closure
also feeds `WorldEffectsFactory` (`GameSession.cs:417`).

**Approach.** Route `PlayerRange` (and the `WorldEffectsFactory` copy) through `PlayerPositions`.
Run/don't-run is shared world state visible in every pane, so nearest is the right rule. While
there, check the other player-singular conditions the original defines
([`docs/org/sequences.md`](../docs/org/sequences.md): `PLAYER_UNDERCOVER`, the `0x200` angular
condition) — generalise any that are implemented, note the rest on their decode page.

**Model recommendation.** medium — the seam exists and has a worked example one function over.

**Verify.** `--run-tests` anim suites green; a `--debug-anim` 2-position check that a
`PLAYER_RANGE` def flips its condition when only the second position is in range. Single-player
behaviour identical (nearest-of-one).

**⚠ Traps.** `AnimRuntime.PlayerPosition` stays a P1 singleton for other consumers until F51/B14
account for them — do not delete it here, just stop `PlayerRange` reading it.

## C22 ☐ AI `primary_target = "player"` resolves per attacker, not to P1 (`BL-367`)

**Goal.** A splitscreen Instant Action wave spreads across the humans instead of converging on P1.

**Evidence (confidence: traced).** `FlightController.cs:2300-2307` takes the first
`IsHumanPiloted` match in `_gunnerScan.Vehicles` when `primary_target == "player"`; the ranked
target score is computed but not consulted on that branch (`:2330`). Set for every IA wingman
(`GameSession.cs:2175`) and wave enemy (`:2244`).

**Approach.** Resolve `"player"` to the nearest human to the attacker (or fold the humans into
the existing ranked score — prefer whichever keeps the AI code's current retarget cadence), so
different attackers naturally pick different humans. Depends on A1 only in so far as "human" vs
"hostile human" — in IA all humans are one team, so no interaction; in plain flight the team
decision governs who counts as a target at all.

**Model recommendation.** high — AI behaviour change, judged partly at the controls.

**Verify.** `--ia` with `--players=2` (or `--debug-join=2` for a scripted stand-in): the wave's
target assignments split across both humans (log the resolution). Single-player IA unchanged.
`PT-50`'s wingman flight behaviour must not regress.

**⚠ Traps.** Do not change the activation-range filter or the scan order itself — only the
resolution of the `"player"` token. The score-not-consulted observation is a lead about mechanism,
not necessarily a bug to "fix" by consulting it; keep the change minimal.

# Wave D — Audio one-shots

## D31 ☐ Projectile one-shots get mix gain and a distance term (`BL-370`)

**Goal.** Four players firing does not quadruple point-blank impact chatter; far impacts sound
far, per the A2 listener model.

**Evidence (confidence: traced).** `Projectile.cs:2173-2190`: an 8-voice pool of non-positional
`AudioStreamPlayer`s at `def.Volume * 0.2f`, no distance term, no per-player gain; round-robin
voice stealing cuts samples in a 4-player firefight.

**Approach.** A2 pinned per-pane listeners, so a positional player now attenuates against whichever
pane is nearest it with no work of ours — the choice below is real, not blocked. Either route
through a shared one-shot helper that applies
`MixGain` and a distance term against the nearest human, or convert the pool to positional
players. Keep the 8-voice pool and its stealing policy unless A2 chose positional (then re-judge
the pool size as TUNE). D32 reuses whatever helper this creates.

**Model recommendation.** medium — mechanical once A2 is decided.

**Verify.** `--volume=0` scripted run: `.scratch/logs/` shows the computed gains for a near and a
far impact. At the controls (2 players): a firefight near P2 is loud for P2, attenuated for P1
per the chosen model. The `snd_*` counting in existing audio assertions stays green.

**⚠ Traps.** The `* 0.2f` factor is the current tuned balance — carry it into the new path, do
not silently drop or double-apply it.

## D32 ☐ `FlightAudio` one-shots respect `MixGain` where they should (`BL-371`)

**Goal.** A splitscreen pile-up does not stack N full-volume crashes over a mix tuned quieter;
your own pane's crash stays prominent.

**Evidence (confidence: traced).** `OnCrash` (`FlightAudio.cs:261`), `OnGroundExplosion`
(`:284`), `OnWaterExplosion` (`:290`), `OnEngineStop` (`:330`), `StartEngine` (`:406`) pass raw
def volume; `Update`, `OnGraze`, `OnWarningShot`, `PlayEmptyClip`, `StartGunLoop` multiply by
`MixGain`. M2.5 recorded "crash one-shots global" as a decision, so this is a re-judgement, not a
bug hunt.

**Approach.** Per event: own-player events (your crash, your prop) may deliberately stay loud;
other-player events take `MixGain` (and D31's distance term if A2 chose one). Write the per-event
verdict into the code as the one-line why. Reuse D31's helper.

**Model recommendation.** medium.

**Verify.** Scripted `--volume=0` crash run logging gains; at the controls, a 2-player mutual
shootdown: neither pane's mix clips, each pilot's own crash reads loudest in their pane. `BL-126`'s
`MixGain` TUNE note is re-checked in F52.

**⚠ Traps.** The ramp/stop cue tuning in the Audio backlog theme was judged against the current
unscaled levels — if this item changes crash levels, note it there rather than re-tuning blind.

# Wave E — Seats and input

## E41 ☐ Pad assignment follows the phantom-device policy (`BL-374`)

**Goal.** Flight pad assignment survives phantom devices; no player is bound to a dead pad slot.

**Evidence (confidence: traced).** `Pads.cs:76-86` assigns `pads[i]` in raw roster order;
`Pads.cs:11-15` documents why that is untrustworthy (8BitDo dongle enumerates twice asleep, Razer
HID exposes a joypad interface); `MenuInput.cs:27` keeps the claim/filter policy the flight path
abandons. Assignment is taken once at session build.

**Approach.** Reuse the menu's claim/filter logic for flight assignment (launchscreen sessions
already know the claimed pads — carry the claims through `SessionSpec` into `AssignPads` instead
of re-enumerating raw). Mid-session reconnect is out of scope; note it in the landing commit.

**Model recommendation.** medium.

**Verify.** `SessionSpecTests`/menu tests green; at the controls with the actual 8BitDo dongle
asleep: P1's pad works in flight. (This hardware repro is the reason the policy exists — the user
has the devices.)

**⚠ Traps.** `--no-pads` and `--debug-join` paths must keep working (scripted runs rely on them).

## E42 ☐ Camera views and look-back for players 2–4 (`BL-372`)

**Goal.** Every player can check their six and use fixed views from the pad.

**Evidence (confidence: traced; binding is a decision).** `CameraController.cs:139-167` reads
`_keyDown` for `BackActive`/`ActiveView`; `FlightController.KeyDown` requires `UseKeyboard`
(`FlightController.cs:2153`), granted only to player 1 (`FlightRigAssembler.cs:105`). The D-pad
is taken by the weapon selectors (class comment), so the binding is a real design question.
`--view=` also pins one view on every pane.

**Approach.** First the decision: which pad control gets look-back and view select (candidates:
click-stick, a shoulder+D-pad chord, or none-for-now with only look-back added). Then wire it
through today's polling seam per player. Make `--view=` per-pane-capable only if trivial;
otherwise document it as all-panes in `docs/cli.md`. Update `docs/controls.md` (mandatory when
player input changes). <TODO: user decision on the binding.>

**Model recommendation.** medium.

**Verify.** At the controls, 2 players: each seat switches its own view and looks back
independently. `--hold` scripted input still drives P1 unchanged.

**⚠ Traps.** `BL-296`'s ActionMap is the eventual home for the binding — add it through the
polling seam consistently with the existing style so the later ActionMap migration lifts it
cleanly; do not build a half-ActionMap here.

## E43 ☐ Splitscreen pause (`BL-373`)

**Goal.** A decided pause behaviour exists in 2–4 player sessions.

**Evidence (confidence: traced; scope is a decision).** `FlightRigAssembler.cs:108` hard-codes
`AllowPause = _in.RigCount == 1`; `FlightController.cs:2165` consumes it; Esc tears the session
down. M2.5 explicitly decided "P debug pause stays single-player-only", so this is a revisit.

**Approach.** Decision first: any player's Start/P pauses everyone (splitscreen pause is
inherently global); who may unpause (recommended: anyone). Then lift the `RigCount == 1` gate and
route the pause input per player. <TODO: user decision, including whether pause stays a debug
facility or becomes a real pause menu hook later.>

**Model recommendation.** medium.

**Verify.** At the controls, 2 players: P2's Start pauses both panes, unpause resumes cleanly;
the sim clock and `--det` scripted runs are unaffected (pause is wall-clock territory).

**⚠ Traps.** The pause path must not desync the fixed-tick sim from the anim clock — check how
single-player pause handles `Clock` before copying it wider.

## E44 ☐ Per-seat spectator control (`BL-375`)

**Goal.** Two downed IA pilots spectate independently.

**Evidence (confidence: traced).** `SpectatorCamera.cs:128, 203, 324` uses raw
`Input.IsKeyPressed`/any-pad reads with no `PadDevices`/`UseKeyboard` split; architecture.md's
entry records the lockstep symptom.

**Approach.** Give the spectator the same per-player device filter the flying panes have
(`PadDevices`/`UseKeyboard` from the downed pilot's rig), one spectator instance per downed
pilot.

**Model recommendation.** medium, low effort.

**Verify.** At the controls, 2 players both out of lives: each moves their own spectator view.

**⚠ Traps.** None known beyond keeping the single-player spectator identical.

# Wave F — Cleanup and playtest

## F51 ☐ Debug tooling binds the right pane or documents P1-only (`BL-376`)

**Goal.** Each in-flight lab/overlay either acts on a stated pane or says P1-only in
`docs/cli.md`; nothing silently no-ops.

**Evidence (confidence: traced).** F5 damage lab and weapon lab take `_rigs[0]`
(`GameSession.cs:1901`, `:1926`); `NodeLabels` projects through P1's camera onto a whole-window
layer (`GameSession.cs:2889`); `MarkerOverlay.Relayout` never runs in splitscreen
(`GetViewport().GetCamera3D()` resolves to the non-current main camera, `MarkerOverlay.cs:243`);
the F15 targeting overlay's text roll-call is whole-window (`TargetingOverlay.cs:236`). (The
`--debug-anim` sound distance column was the sixth site; A2 landed it — the column now reports the
range to the nearest listener and names its pane, and `docs/cli.md` says so.)

**Approach.** Per site, the cheap verdict: labs stay P1-only (document in `docs/cli.md`);
`MarkerOverlay` takes an explicit camera instead of `GetViewport()` (fixes the silent no-op);
`NodeLabels` documents P1-only.

**Model recommendation.** medium, low effort — mechanical, tool-only.

**Verify.** `--screenshot=` probe runs unchanged (these overlays are all debug-flag-gated);
`docs/cli.md` bullets updated for each documented-P1-only flag.

**⚠ Traps.** `docs/cli.md` is the description of record — the flag bullets change there, not in
PROJECT_CONTEXT's gloss table.

## F52 ☐ Splitscreen chrome playtest (`BL-126`)

**Goal.** The splitscreen chrome constants get their owed at-the-controls verdict on the
post-plan build: `HudMetrics` sqrt pane damping, `MixGain` (as re-shaped by D31/D32),
`SpawnAbreast`, join/lock feel, tag-gutter widths.

**Evidence (confidence: direction-sound, all TUNE).** `BL-126` is `[Owed-playtest]`; the
constants exist and work, the judgement is what is owed. Related owed playtests to fold into the
same session: `PT-43(d)` (VS HUD at 4-player panes), `PT-49` (PerfHud legibility in a 4-player
pane).

**Approach.** One at-the-controls session after the other waves land, 2 and 4 players, using
`playtest.md`'s PT-43/PT-49 scripts plus the `BL-126` checklist. Outcomes: constants confirmed
(delete the tag) or new TUNE items minted.

**Model recommendation.** n/a — user at the controls; agent prepares the checklist and launch
commands.

**Verify.** The playtest verdict itself; `playtest.md` and `backlog.md` updated in step.

**⚠ Traps.** Needs a second controller pair for the 4-player cases (PT-43(d) stalled on exactly
that); schedule when the hardware is on hand.
