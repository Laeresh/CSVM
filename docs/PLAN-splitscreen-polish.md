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

11. ☑ Puffer distance fade answers every pane (`BL-339`)
12. ☑ Screen wash routed to the hit pane(s) (`BL-340`)
13. ☑ World point lights budgeted against the nearest rig (`BL-366`)
14. ☑ Close out `BL-338`: residual sweep + fog verdict recorded

### Wave C — Gameplay "the player" rules

21. ☑ `PLAYER_RANGE` measures from the nearest human (`BL-365`)
22. ☑ AI `primary_target = "player"` resolves per attacker, not to P1 (`BL-367`)

### Wave D — Audio one-shots

31. ☑ Projectile one-shots get mix gain and a distance term (`BL-370`)
32. ☑ `FlightAudio` one-shots respect `MixGain` where they should (`BL-371`)

### Wave E — Seats and input

41. ☑ Pad assignment follows the phantom-device policy (`BL-374`)
42. ☑ Camera views and look-back for players 2–4 (`BL-372`)
43. ☑ Splitscreen pause (`BL-373`)
44. ☑ Per-seat spectator control (`BL-375`)

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

## B11 ☑ Puffer distance fade answers every pane (`BL-339`)

**Goal.** A smoke trail near any player is visible in that player's pane, whatever P1 is doing.

**Evidence (confidence: traced).** `WeatherRig.Tick` publishes one camera pose
(`_ambience.SetCamera(rigs[0].Camera…)`, `WeatherRig.cs:295`); `Puffer` computes `DistanceAlpha`
per particle against it and writes the alpha into the one `MultiMesh` every pane draws
(`Puffer.cs:711`, `:886`). The unauthored `NEAR_FADE (0,0)` default makes the near band a hard
cull at view-space depth 0, so everything behind P1's camera is dropped for everyone. Confirmed at
the controls 2026-08-15: shooting at P1 from behind, trail streaks appear only as the rocket
passes P1. Decode: [`docs/org/puffer.md`](../docs/org/puffer.md) — the fade is a DRAW rule in the
original, and a view-space depth, not a euclidean range.

**Approach (landed).** The nearest-viewer rule, matching the tracer precedent. `EffectAmbience`
carries a LIST of poses instead of one camera (`Viewers`, `SetViewers(ViewerSet)`; `SetCamera` stays
as the single-pose spelling for the suites and labs), `WeatherRig.Tick` publishes the session's
`ViewerSet` (constructor-injected beside the ambience) instead of `rigs[0]`'s transform, and
`Puffer.NearestViewerAlpha` runs the existing per-viewer `DistanceAlpha` against each pose and keeps
the highest alpha, discarding only when every pane discards. Per viewer rather than a viewer picked
up front, because the fade is a depth along each camera's own forward: the pane nearest in range can
be the one facing away, which the near band culls at depth 0. `ViewerSet.Poses(into)` fills a
caller-owned buffer, since this consumer republishes every frame. One `MultiMesh` stayed one
`MultiMesh`; per-pane alpha (N MultiMeshes) was not reached for, per `BL-338`'s rule, and the
verdict on whether the nearest rule suffices visually is the at-the-controls check below.

**Model recommendation.** high — per-particle hot path plus a fidelity judgement.

**Verify (done 2026-08-15).** `dotnet build` + `dotnet format --verify-no-changes` clean.
`.\RunTests.ps1`: 1289/1289 units, 60/60 engine suites, 14/14 goldens hash-identical, engine errors
clean, hitch clean. `puffer-distance-fade` keeps all four decoded-band cases and gains
`PufferFadeEveryPane`, which drives the real `ViewerSet` over two `Camera3D` nodes: a particle 20 m
ahead of P1 (inside P1's 40 m near cull) but 220 m ahead of P2 draws, the same particle against P1
alone is culled (able-to-fail control), a particle behind BOTH panes is still culled, the most
favourable of two panes' alphas is the one taken (1.0 over P1's 0.5, with the 0.5 pinned as its
control), and the one-viewer case reads exactly what it read before. 8-chapter `--freecam` sweep
(C1, C1B, C1C, C2, C2B, C3, C4, C5): all exit 0, zero errors, node/mesh counts unmoved.

**Scripted 2-pane assertion: no — the TODO's answer.** Splitscreen under `--screenshot=` does work
(`--fly --players=2 --chapter=C3 --screenshot= --frames=90` boots clean and captures both stacked
panes), so that half of the unknown is settled. But it cannot assert B11: the discriminating
geometry needs P2's camera placed independently of P1's and something emitting between them, and
there is no per-player placement flag (`--pos` places the flown plane) and no scripted fire — both
panes spawn near-coincident and co-aligned off the same mission spawn list, so they see the same
thing. The rule is pinned by the suite above instead, and the *visual* verdict (does one shared
alpha read right in a real 2-pane session) stays an at-the-controls verify: two players, P2 astern
of P1, P2 fires a rocket — P2 sees the full trail. Written up as `PT-52` in `playtest.md`, which is
also where `BL-338`'s per-pane-alpha verdict is decided.

**⚠ Traps.** Do not disable the fade — the near cull stops a camera inside an emitter from filling
the screen, and the suite pins the decoded bands against C3's own emitters. The fade is view-space
depth along the camera forward, not distance; "nearest" must compare per-viewer depths, not ranges.

## B12 ☑ Screen wash routed to the hit pane(s) (`BL-340`)

**Goal.** An `FBFX_COLOR_FROM_TO` wash paints the pane(s) of players near the burst, not all four.

**Evidence (confidence: traced).** `ScreenFlash` builds one `ColorRect` per view (correct) but
holds one ramp state, and `Play` paints every rect (`ScreenFlash.cs:28`, `:108`); fired from
`AnimRuntime` via `GameSession.cs:444`. The original keeps a single frame-buffer-effect object
whose second burst replaces the first (`crimson.exe` 0x9c8a98); replace-not-composite is decoded
and must survive per pane.

**Decision (2026-08-15, with the radius data in hand): every player within the burst's own radius,
and the radius is the wash def's OWN authored gate, not the weapon's.** Read out of the extraction:
each of the three ordnance wash defs carries exactly one `If`, and it is `PlayerRange 10000` — metres
squared in the compiled convention, so **100 m** — guarding the `CallSequence frame_buffer_effects1`
that holds the ramp chain. Identical in all 8 chapters, 24 defs, no exceptions. The intro cutscene's
`gi_scene1` is the fourth carrier and gates on nothing, so its wash is ungated and paints
everything. So the routing rule is not a new rule at all: it is the original's own gate, asked once
per player instead of once for player 1.

"The hit player only" is refused on the same data. Two of the three carriers are GROUND effects —
they play at terrain and water impacts, where no aircraft was struck at all — so a hit-player rule
would silence the wash in its commonest case, and the decoded gate measures a range from the burst,
not a victim.

**That also moved the dispatch site.** The plan expected `ProjectilePool`'s blast/impact path to own
the routing because it knows the aircraft and the distance. It knows neither of the things this
actually needs: the radius is authored in the anim def, and the burst point is the effect instance's
own anchor, both of which live in `AnimRuntime`, which already evaluates that very gate for the
`If`. `ProjectilePool` is untouched by this item.

**Approach (landed).** The `ScreenFlash` sink grows two arguments — the burst's world point and the
def's gate radius squared (`AnimRuntime.WashGateRadiusSquared`, a cached per-def scan for the single
`If PlayerRange` operand; 0 = ungated). `UI.ScreenFlash` holds one `Ramp` per pane instead of one
set of fields, takes the session's `ViewerSet` at `Build` (index-aligned with the panes, since
`GameSession` builds both from the same rig list), and paints the panes whose own camera is inside
that radius. Replace-not-composite is unchanged, held within each pane. One floor: if no pane's
camera is in range the nearest pane still washes, because the def's gate already fired (it measures
rig 0's camera, this measures each pane's own, and the two can disagree at the margin). In single
player both measure the same camera, so the routing is a no-op there and the goldens are
hash-identical.

**Model recommendation.** high — the dispatch-site derivation crosses `ProjectilePool`,
`AnimRuntime` and `GameSession`.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings. `.\RunTests.ps1`:
1289/1289 units, 60/60 engine suites, 14/14 goldens hash-identical, engine errors clean, hitch
clean. `fbfx-flash` keeps every timing check (the six authored run times, the gaps between fires,
the 1.1 s span) and gains both halves of the routing: at the sink, every step reports the burst's
own point (0.000 m off the play point) and the def's authored 10000 m² gate; at the overlay,
`WashPaintsOnlyThePanesItReached` drives a real two-pane `ScreenFlash` over two `Camera3D` nodes
120 m apart and asserts one pane (burst 50 m from P1, 170 m from P2), the other pane (the mirror
case, while P1's ramp is still running and must not move), both panes (60 m from each — the design
call, which a nearest-only rule would fail), the ungated case, and the nearest-pane floor. The
able-to-fail control is the same overlay with no `ViewerSet` bound, which paints both panes;
disabling the routing outright was run and fails three of the checks. 8-chapter `--freecam` sweep
(C1, C1B, C1C, C2, C2B, C3, C4, C5): all exit 0, zero errors, node/mesh counts unmoved.

**At the controls: still owed, and it is B11's blocker, not a new one.** Two panes cannot be given
independent positions from the CLI (no per-player placement flag, no scripted fire — both panes
spawn near-coincident off the same spawn list), so the discriminating geometry for "a burst near P2
only" is not reachable in a scripted session; the suite pins the rule instead. Folded into `PT-52`
in `playtest.md`, beside B11's: 2 players apart, rocket hit near P2, only P2's pane washes.

**⚠ Traps.** Keep replace-not-composite within each pane. The gutters and the empty 3P quadrant
must stay out of the wash (the per-view rect build already guarantees this; don't regress it).
⚠ The wash still FIRES on player 1's camera alone — the def's `If PlayerRange` gate reads
`AnimRuntime.PlayerPos()`, which is C21 (`BL-365`)'s item, not this one. Until C21 lands, a burst
100 m from P4 and 2 km from P1 plays no wash for anyone to route. B12 decides who sees a wash that
happened; C21 decides that it happens.

## B13 ☑ World point lights budgeted against the nearest rig (`BL-366`)

**Goal.** A burning refinery next to P4 spills light in P4's pane even with P1 far away.

**Evidence (confidence: traced).** `AnimRuntime.cs:3211` calls `WorldLights.Commit(PlayerPos())`;
`WorldLights.cs:94-118` fades 900–1500 m against that single position and `Significance`-ranks
into the 16-slot `csky_light_data`/`csky_light_count` globals every pane reads.

**Approach (landed).** `WorldLights.Commit` takes `IReadOnlyList<Vector3> viewerPositions` instead
of one `Vector3`; both the fade (`NearestDistance`) and `Significance`'s ranking now measure to the
nearest of the set, one small helper shared by both, so a light beside player 4 no longer fades or
loses its budget slot for being far from player 1. The uniform stays global exactly as scoped — a
light lit for one pane stays lit for all. `AnimRuntime` grows `LightViewerPositions`
(`Func<IReadOnlyList<Vector3>>?`), the same resolved-per-call shape as `PlayerPosition`/
`PlayerPositions`/A2's `ListenerPositions`, threaded through `WorldSession.Options` and fed by
`GameSession`'s `() => _viewers.Positions()` — A3's service, read fresh every frame since a pane's
camera moves. Null/empty falls back to `PlayerPos()` alone (a lab or test runtime with no viewer
seam), matching the pre-existing fallback shape. `MaxActive` stayed unbinding in every measurement
taken here, as expected — no new TUNE item was needed.

**Model recommendation.** medium — contained change with a clear rule.

**TODO resolved.** `WorldLights` has no `CSVM.Tests` seam and cannot get one: `Commit` calls
`RenderingServer.GlobalShaderParameterSet`/`ImageTexture.CreateFromImage`, engine calls that only
run inside a live Godot process (`CSVM.Tests` is engine-free by the repo's own split — anything
reaching `GD.*` belongs in `src/Testing/` instead). The fitting seam is the one B11/B12 already
used for this exact shape (a splitscreen draw-rule class needing real engine state): a `--run-tests`
engine suite. A `--debug-anim` scripted run was also tried and rejected for the same reason B11/B12
hit — there is no per-player placement flag, so two scripted panes spawn near-coincident off the
same spawn list and can't discriminate "near P1, far from P2" geometry; the CLI run only proves the
wiring boots (below), the rule itself is pinned by the suite.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, `dotnet format
--verify-no-changes` clean. `.\RunTests.ps1`: 1289/1289 units, 61/61 engine suites (60 prior +
`world-lights-nearest-viewer`), 14/14 goldens hash-identical (single-player: one viewer position is
the pre-B13 rule exactly, so the shots don't move), hitch clean. The new suite drives a real
`WorldLights` instance directly: a light 2000 m from a lone P1 (past the 1500 m `FadeEnd`) is
dropped — able-to-fail control — and stays committed once a second viewer sits 100 m from it; a
16-light `MaxActive` budget with a 17th light beside P2 drops that 17th against P1 alone (past
`FadeEnd`, never reaching the budget) but wins a slot over the farthest filler once P2 is a viewer,
proving `Significance`'s ranking uses the same nearest-viewer distance, not just the fade; and a
lone viewer reads exactly the pre-B13 result. 8-chapter `--freecam` sweep (C1, C1B, C1C, C2, C2B,
C3, C4, C5): all exit 0, no errors beyond the pre-existing `pir_spinner.tif` texture-absent line.
Scripted `--players=2 --fly --chapter=C1 --debug-anim --frames=90` boots clean, exit 0, logs `world
lights 15 rendered of 35 live` — the wired path fires under a real session; the visual verdict
(does a far light near P2 actually spill in P2's pane) needs independently-placed panes and is not
reachable from the CLI, same gap B11/B12 hit — folded into `PT-52` alongside theirs.

**⚠ Traps.** Keep the fade curve and `Significance` arithmetic untouched; only the reference
position generalises. Do not fold `LightViewerPositions` into `PlayerPositions` (the gameplay seam,
C21's) — this is A3's draw-rule seam, the same separation `ViewerSet`'s own trap and B12's dispatch
already keep.

## B14 ☑ Close out `BL-338`: residual sweep + fog verdict recorded

**Goal.** `BL-338`'s sweep list is finished: every named site has a verdict (fixed here, recorded
as deliberate, or split into its own item), and the umbrella item closes.

**Evidence (confidence: traced for the surveyed sites).** The 2026-08-15 sweep already gave
verdicts for most of the list: precipitation, cloud clutter, `FogVolumeClutter` far fade,
`EmitterRenderer`, lens flare, whiteout, deck regime, zone cull masks are per-pane-correct;
`SelectionService` and the labs are single-camera by design (unreachable in splitscreen). Fog-zone
selection remains genuinely wrong (`WeatherRig.cs:457` drives global fog uniforms from rig 0,
self-documented) and per-pane fog needs per-instance uniforms.

**Approach (landed).** Walked `BL-338`'s to-audit list once more against the landed B11/B12/B13,
reading each site's current code rather than trusting the sweep's prior notes:

| Site | Verdict | Where it's decided |
|---|---|---|
| LOD bands | Per-pane-correct — not camera-dependent at all | `SceneBuilder.BuildSubtree` keeps only `LodRangeMin == 0` at BUILD time, once, never a per-frame distance check |
| Cloud whiteout | Per-pane-correct — already per rig | `WeatherRig.Tick`'s per-rig loop (`rig.Whiteout.Color`, one call per rig) |
| Deck regime / zone cull masks | Per-pane-correct — already per rig | Same `Tick` loop (`DeckRegime`, `rig.Camera.CullMask`) |
| `FogVolumeClutter` far fade / visibility | Per-pane-correct — shader `CAMERA_POSITION_WORLD` + per-camera cull-mask zone bit | `Effects/FogVolumeClutter.cs`'s own ⚠ |
| Precipitation | Per-pane-correct — same `CAMERA_POSITION_WORLD` shader mechanism as the world billboards | `Effects/Precipitation.cs`'s wrap shader |
| Lens flare sun wash | Per-pane-correct — already one `LensFlareRig` instance per pane | `Session/LensFlareRig.cs`'s own module note |
| `SelectionService` | Unreachable in splitscreen, by construction | `SessionSpec.Resolve` clamps `Players` to 1 whenever `Players > 1 && !Fly`, and `--freecam`/`--anim-lab` are never `Fly` |
| `AnimRuntime.PlayerPos()`'s `GetViewport()` fallback | Dead in every real session | `GameSession.cs:875` always wires `PlayerPosition`; the branch only runs for a standalone `AnimRuntime` (lab/test) — the live P1-singleton concern is `PlayerPosition` itself, `BL-365`/C21's |
| `MarkerOverlay`'s `GetViewport()` | Genuinely wrong, already tracked | F51 (`BL-376`), not re-opened here |
| Fog-zone selection | **Genuinely wrong, deliberate** — global shader uniforms, rig 0 only | `WeatherRig.cs:459-466`'s own ⚠, confirmed unchanged; per-instance fog uniforms needed, out of scope (Milestone goal) |

No new code follows: every site is either already correct (by a mechanism read directly off the
current source, not assumed) or already tracked by a live item (`F51`, `C21`). The one genuinely
open thread — fog — was split into a new blocked item, `BL-380`, rather than left as a dangling
BL-338 sub-claim: `[Bug]` `[Blocked: per-instance fog shader uniforms]` in `backlog.md`'s
Splitscreen theme, citing `WeatherRig.cs:459` and the `csky_instance_uniforms.gdshaderinc`
ordering-contract hazard any fix would have to respect. Each surviving verdict also got a one-line
⚠ at its own site in `docs/architecture.md` (`SceneBuilder.cs`, `SelectionService.cs`,
`AnimRuntime.cs`) rather than only in this table, so a future reader hits it locally. Two stray
narrative citations of the closing `BL-338` ID (a Puffer.cs doc comment, one architecture.md line)
were repointed at the plan's own nearest/union boundary rule instead of a soon-nonexistent ID;
`playtest.md`'s `PT-52` likewise. `BL-338` itself is deleted from `backlog.md`'s Splitscreen theme
intro (it was never its own bullet there — a scheduled item's detail lives here) and
`PROJECT_CONTEXT.md`'s "Current status" advances past it.

**Model recommendation.** medium, low effort — mechanical audit over an existing checklist.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings;
`dotnet format --verify-no-changes` clean. `.\RunTests.ps1`: 1289/1289 units, 61/61 engine suites,
14/14 goldens hash-identical, engine errors clean, hitch clean — unchanged from B13's landing,
confirming the audit found no straggler needing a code fix. 8-chapter `--freecam` sweep (C1, C1B,
C1C, C2, C2B, C3, C4, C5): all exit 0, zero errors.

**⚠ Traps.** The instinct to fix fog while in there — resist it; per-instance fog uniforms have a
recorded hazard (`csky_instance_uniforms.gdshaderinc` ordering) and deserve their own plan.

# Wave C — Gameplay "the player" rules

## C21 ☑ `PLAYER_RANGE` measures from the nearest human (`BL-365`)

**Goal.** Proximity-triggered world animations respond to whichever human is near them.

**Evidence (confidence: traced).** `AnimRuntime.cs:3399` evaluates `PLAYER_RANGE` via
`PlayerPos()` (`:3632`), fed `_rigs[0].Camera.GlobalPosition` (`GameSession.cs:863`). The sibling
`EXECUTION_BY_RANGE` gate already uses the nearest-human `PlayerPositions` seam
(`AnimRuntime.cs:2025`, `GameSession.cs:869`); this condition was left behind. The rig-0 closure
also feeds `WorldEffectsFactory` (`GameSession.cs:417`).

**Approach (landed).** Routed `PlayerRange` through `PlayerPositions`: `EvaluateCondition`'s
`"PlayerRange"` arm now calls a new `NearestPlayerDistanceSquared`, the same
min-over-`PlayerPositions` pattern `TickDeferredByRange`'s `EXECUTION_BY_RANGE` gate already used,
falling back to the old single `PlayerPos()` only when no `PlayerPositions` seam is wired (a lab,
a unit test). `PlayerPosition`/`PlayerPos()` survive unchanged as that fallback — the trap below
held. `WorldEffectsFactory` grew its own `_playerPositions` (optional, defaulting null) and now
sets `PlayerPositions` on the world-effects `AnimRuntime` it builds, so an ordnance wash's own
`If PlayerRange` gate answers the same nearest-human question the world runtime's does; `GameSession`
feeds both `WorldSession.Options.PlayerPositions` and the factory from one new
`PlayerPositionsSnapshot()` method (previously two copies of the same closure), so the two seams
can never disagree about who is nearest. Checked the other player-singular conditions
([`docs/org/sequences.md`](../docs/org/sequences.md)): `PLAYER_UNDERCOVER` (`0x8`) and
`PLAYER_LINED_UP` (`0x200`) are both in the doc's "never authored, 0 occurrences" set and neither
has a case in `EvaluateCondition`'s switch (confirmed by grep) — nothing to generalise, the
decode page already carries the note.

**Model recommendation.** medium — the seam exists and has a worked example one function over.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, `dotnet format --verify-no-changes`
clean. `.\RunTests.ps1`: 1289/1289 units, 61/61 engine suites (`fbfx-flash` gains
`PlayerRangeNearestHuman`, chained after B12's `WashPaintsOnlyThePanesItReached`: a burst plays no
wash at all while every `PlayerPositions` entry sits 5 km off the def's own 100 m gate — 0 fired
over 2.5 s of `Advance` — then fires its full six-step chain once a second `PlayerPositions` entry
is placed at the burst, the NEAREST-not-first entry deciding it), 14/14 goldens hash-identical,
engine errors clean, hitch clean. 8-chapter `--freecam --chapter=<X> --screenshot= --frames=60`
sweep (C1, C1B, C1C, C2, C2B, C3, C4, C5): all exit 0; each log's one `ERROR` line is the same
benign screenshot-path artifact of the probe's relative output path, not an engine or anim error
(freecam has no `_rigs`, so `PlayerPositionsSnapshot` falls back to the single spectator camera —
this item is a no-op there by construction, single-camera nearest-of-one). Scripted 2-player
sanity: `--fly --players=2 --chapter=C1 --debug-anim --screenshot= --frames=90` boots clean, 0
`ERROR` lines, screenshot saved — no per-player placement flag exists to force a discriminating
"P4 near, P1 far" geometry from the CLI (the same CLI gap B11/B12 hit), so the discriminating
check is the unit suite above instead, which drives real distinct `PlayerPositions` entries the
CLI cannot.

**⚠ Traps.** `AnimRuntime.PlayerPosition` stays a P1 singleton for other consumers until F51/B14
account for them — do not delete it here, just stop `PlayerRange` reading it.

## C22 ☑ AI `primary_target = "player"` resolves per attacker, not to P1 (`BL-367`)

**Goal.** A splitscreen Instant Action wave spreads across the humans instead of converging on P1.

**Evidence (confidence: traced).** `FlightController.cs:2300-2307` takes the first
`IsHumanPiloted` match in `_gunnerScan.Vehicles` when `primary_target == "player"`; the ranked
target score is computed but not consulted on that branch (`:2330`). Set for every IA wingman
(`GameSession.cs:2175`) and wave enemy (`:2244`).

**Approach (landed).** `SelectRankedTarget`'s `primary_target` arm now splits the two readings of
the assignment string. A by-NAME assignment still names one aircraft, so the first match in the
scan is it, unchanged; the `"player"` token names a ROLE, and the loop keeps the nearest human to
this attacker (`nearestHuman` / `nearestHumanDistSq`, the min-over-candidates pattern the rest of
the wave items use) instead of the first `IsHumanPiloted` match. Nearest was chosen over folding
the humans into the ranked score because the score is not consulted on this branch at all — the
trap's "score-not-consulted is a lead, not a bug" — and because resolution runs ONCE per
acquisition (`DriveAiGunner` re-acquires only when the standing target dies or leaves), so the
retarget cadence is untouched and there is no flapping between two near-equidistant humans. The
activation gate moved to `DistanceSquaredTo` alongside it (same predicate, one sqrt fewer per
candidate). The breadcrumb log distinguishes the two: `how` reads `primary target: nearest human`
on the role arm, `primary target` on the by-name arm. No interaction with A1 in IA (all humans are
one team); in plain flight the team decision still governs who is a candidate at all, upstream of
this branch.

**Model recommendation.** high — AI behaviour change, judged partly at the controls.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings;
`dotnet format --verify-no-changes` clean. `.\RunTests.ps1`: 1289/1289 units, 61/61 engine suites,
14/14 goldens hash-identical, engine errors clean, hitch clean. The `ai-gunnery` suite gains two
chained checks after its existing `primary_target 'player'` one: the AI rival is flagged human and
pulled to 300 m while the first-registered human sits at 800 m (the pick moves to the rival), then
the first-registered human is pulled to 100 m (the pick moves back) — scan order and distance
disagree in the first half and agree in the second, so only distance can produce both. **Both
halves were run against their own able-to-fail control** (METHOD-9/10, the companion to INSTR-10):
with the old first-registered rule restored in place, half 1 fails and half 2 passes; with a
last-registered rule, half 1 passes and half 2 fails. Neither half is an invariant this geometry
cannot discriminate. Scripted probes (`.\RunProbe.ps1`, a 4-enemy `dogfight_squadron` `--ia=` file,
900 frames, C1): `--players=2` logs all four wave enemies resolving to **P2** — `ai gunner: shooter
100 targets P2 at 2439 m (primary target: nearest human: …)` and the same for 101/102/103 — which
is the nearest human to the wave's spawn and is exactly the assignment the old first-match rule
could never produce (it returned P1 by construction); the same file with one player logs all four
on P1, single-player unchanged. `PT-50`'s `--ia=ia-wingmen-test.json` recipe re-run scripted: three
Fury wingmen on team 1, 0 `ERROR` lines, escort chain untouched (the by-name arm this item did not
change). 8-chapter `--freecam --chapter=<X> --frames=60 --screenshot=` sweep (C1, C1B, C1C, C2,
C2B, C3, C4, C5): all exit 0, 0 `ERROR` lines each, node/mesh counts unchanged. The wave enemies
converging on one human rather than splitting is geometry, not the rule: they activate together at
one spawn while both humans are still on their shared start line, and no CLI flag can place P1 and
P2 kilometres apart (the same gap B11/B12/C21 hit) — the discriminating check is the suite above,
which drives real distinct human positions the CLI cannot. A two-pad pass at the controls stays
owed and folds into `F52`.

**⚠ Traps.** Do not change the activation-range filter or the scan order itself — only the
resolution of the `"player"` token. The score-not-consulted observation is a lead about mechanism,
not necessarily a bug to "fix" by consulting it; keep the change minimal.

# Wave D — Audio one-shots

## D31 ☑ Projectile one-shots get mix gain and a distance term (`BL-370`)

**Goal.** Four players firing does not quadruple point-blank impact chatter; far impacts sound
far, per the A2 listener model.

**Evidence (confidence: traced).** `Projectile.cs:2173-2190`: an 8-voice pool of non-positional
`AudioStreamPlayer`s at `def.Volume * 0.2f`, no distance term, no per-player gain; round-robin
voice stealing cuts samples in a 4-player firefight.

**Approach (landed).** A2 pinned per-pane listeners for `AudioStreamPlayer3D` emitters, but this
pool's 8 voices are plain `AudioStreamPlayer`s and never join that engine rule, so the chosen shape
is the shared helper, not a conversion to positional players (the pool and its round-robin
stealing stay exactly as they were). `ProjectilePool` gains two seams: `MixGain` (the same
equal-power `1/√N` figure `GameSession` already hands `FlightAudio`) and `PlayerPositions`
(`GameSession.PlayerPositionsSnapshot`, C21's `PLAYER_RANGE` seam — nearest human, not nearest
pane camera, per the plan's own seam-choice decision). `PlaySound` takes a `worldPos` now (the
FIRE muzzle's origin or the IMPACT point) and multiplies the pre-existing `def.Volume * 0.2f` by
`MixGain` and a new `DistanceGain(distance, rangeMin, rangeMax)` — a `public static` linear falloff
between the sound's own `RANGE` (1 inside the full-volume distance, 0 past the audible one), so
D32 can reuse the exact same term rather than a second implementation. `PlayShotSound` (turret
gunners' launch bark) takes the firepoint's position through the same path. A throttled breadcrumb
(first 8 one-shots, the same convention as the impact `fx=`/`snd=` log) prints the resolved sound,
`MixGain`, the nearest-human distance, the RANGE pair, `distGain` and the final linear gain.

**Model recommendation.** medium — mechanical once A2 was decided.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, `dotnet format --verify-no-changes`
clean. `.\RunTests.ps1`: 1292/1292 unit tests (3 new — `WeaponBlastTests`'s
`OneShotDistanceGain*`, pinning `DistanceGain`'s full-inside/zero-past-RANGE edges, its linear
midpoint, and the `float.MaxValue`/no-seam skip), 61/61 engine suites, 14/14 goldens
hash-identical — the existing `snd_*`-counting audio assertions are part of that 61 and stayed
green. Scripted probe (`--det --ia=<empty JSON, defaults to dogfight_ace> --chapter=C1 --volume=0
--frames=3600 --screenshot=`, SHELL-12's exit condition): a real 1-vs-1 duel produced both readings
the plan asked for in one run — two direct hits on the player's own airframe at 5-6 m (well inside
`snd_ricochet*`'s `[20, 200]` m RANGE) logged `distGain=1,00 vol=0,200`, and six ground `snd_grnd_bullet`
hits at 221-333 m (inside its `[80, 800]` m RANGE) logged `distGain=0,65..0,80 vol=0,130..0,161` —
the term discriminates near from far using real RANGE data, not a synthetic case. **What this
cannot show:** a live multi-player mix is the user's own call (`verification.md`'s "what this
project cannot verify itself"), so the at-the-controls two-pad firefight (P2 loud for P2, attenuated
for P1) stays owed and folds into `F52`, the same deferral A1/A2 already recorded.

**⚠ Traps.** The `* 0.2f` factor is the current tuned balance — carry it into the new path, do
not silently drop or double-apply it.

## D32 ☑ `FlightAudio` one-shots respect `MixGain` where they should (`BL-371`)

**Goal.** A splitscreen pile-up does not stack N full-volume crashes over a mix tuned quieter;
your own pane's crash stays prominent.

**Evidence (confidence: traced).** `OnCrash` (`FlightAudio.cs:261`), `OnGroundExplosion`
(`:284`), `OnWaterExplosion` (`:290`), `OnEngineStop` (`:330`), `StartEngine` (`:406`) pass raw
def volume; `Update`, `OnGraze`, `OnWarningShot`, `PlayEmptyClip`, `StartGunLoop` multiply by
`MixGain`. M2.5 recorded "crash one-shots global" as a decision, so this is a re-judgement, not a
bug hunt.

**Approach (landed).** Per-event verdict, written into the code as a one-line why at each site:
`OnCrash`, `OnGroundExplosion`, `OnWaterExplosion` and `OnEngineStop`'s `snd_propstop` all fire in
the SAME instant on a downed rig (`OnEngineStop`'s own doc: "right after the boom") — exactly the
"N full-volume crashes" pile-up the Goal names, so all four now take `MixGain`. `StartEngine`'s
`snd_propstart` is the one exception, deliberately kept raw: a respawn is this pilot's own moment
and does not naturally coincide with N other rigs' at the same physics frame the way a crash does.
No `ProjectilePool.DistanceGain` (D31's helper) anywhere here — that term measures distance to the
nearest human, and this class is non-positional own-ship audio, always heard at "distance 0" from
whichever pilot it is, so a distance term is meaningless for it; D31's *reused piece* is the
`MixGain` convention itself (`FlightAudio.MixGain` already existed, unchanged in shape), not the
distance math. Both the crash-family multiply calls and their existing `GD.Print` breadcrumbs
(`crash sound: …`, `engine stop: …`) now report the computed `MixGain`/`vol`, D31's logging
convention, satisfying this item's own scripted-gain verify.

**Model recommendation.** medium.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, `dotnet format --verify-no-changes`
clean. `.\RunTests.ps1`: 1292/1292 unit tests, 61/61 engine suites, 14/14 goldens hash-identical —
unchanged from D31's numbers, since this item adds no new pure-function surface (a re-judged
multiply at 5 existing Node-bound call sites, not a novel algorithm; the class has never had unit
coverage — `MixGain` was already TUNE, judged at the controls). Scripted `--det --ia=<same
dogfight_ace fixture as D31> --chapter=C1 --volume=0 --frames=3600` crash run logging gains, both
player counts: 1P (`--players=1`, the default) shows `MixGain=1,00 vol=1,000` for
`snd_exp_plane4`/`snd_exp_ground_a`/`snd_propstop` — unchanged, as the item requires; 2P
(`--players=2`) shows `MixGain=0,71 vol=0,707` for the same three sounds in the same instant on P1's
death, confirming the crash-family scales together rather than one layer slipping through unscaled.
**What this cannot show:** a live multi-player mix is the user's own call
(`verification.md`'s "what this project cannot verify itself"), so the at-the-controls 2-player
mutual shootdown (neither pane's mix clips) stays owed and folds into `F52`, the same deferral
A1/A2/D31 already recorded. `BL-285` (the `snd_propstop`/`EngineStartRamp` A/B) now carries a note
that its splitscreen listen must judge `snd_propstop` at whatever `N` is under test, not the 1P
level — `snd_propstart` is unaffected.

**⚠ Traps.** The ramp/stop cue tuning in the Audio backlog theme was judged against the current
unscaled levels — if this item changes crash levels, note it there rather than re-tuning blind.

# Wave E — Seats and input

## E41 ☑ Pad assignment follows the phantom-device policy (`BL-374`)

**Goal.** Flight pad assignment survives phantom devices; no player is bound to a dead pad slot.

**Evidence (confidence: traced).** `Pads.cs:76-86` assigns `pads[i]` in raw roster order;
`Pads.cs:11-15` documents why that is untrustworthy (8BitDo dongle enumerates twice asleep, Razer
HID exposes a joypad interface); `MenuInput.cs:27` keeps the claim/filter policy the flight path
abandons. Assignment is taken once at session build.

**Approach (landed).** The proposed route — carrying the menu's *interactive* claims through
`SessionSpec` — turned out not to fit: menu-driven sessions already bypass `AssignPads` entirely
(`GameSession.cs:1703`, `_menuPads ?? Pads.AssignPads(...)`), and `SessionSpec` deliberately keeps
pad bindings out of itself (`Launcher.cs:827-830`, "they come from the join flow rather than from
args"). The only path still hitting raw `AssignPads` is a **direct CLI multiplayer launch**
(`--fly --players=N` with no `--menu`), which has no join screen to claim pads interactively in the
first place — `StartSession()` builds synchronously, so there is no frame loop to wait on a Start
press. Reused what *is* interaction-free in `LaunchMenu`'s policy instead: `SyncDevices` hands
player 1 "every pad nobody else has claimed" rather than `pads[0]`
(`LaunchMenu.cs:617-621`) — a set-difference, computable in one synchronous pass. `AssignPads` now
does the same: P2–P4 still take one raw-roster slot each (unchanged, still a raw-index guess), and
P1 gets every connected pad none of them claimed, unioned through the same `Pads.For` any-pad read
FlightController already gives a null-bound single player. Split into a pure
`AssignPads(players, IReadOnlyList<int>)` overload (no `Input.*` calls) so the rule is unit-testable
without a running engine (`PadsTests.cs`), with `AssignPads(players)` supplying `Connected()` and
logging. P2–P4 stay exposed to a phantom device at their specific slot — fixing that needs the
menu's interactive claim, which needs a join screen this launch path doesn't have; out of scope
here, same as mid-session reconnect.

**Model recommendation.** medium.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings; `dotnet format
--verify-no-changes` clean. `.\RunTests.ps1`: units/engine/goldens/hitch all PASS (six new
`PadsTests` cover the leftover-pool rule, including the exact repro shape — a phantom pad at slot 0
plus an unclaimed real pad at a later slot — asserting P1's read set contains the real one).
`SessionSpecTests`/`SessionSpecMenuTests` green (untouched by this change — menu launches never hit
`AssignPads`). **The hardware repro (the actual 8BitDo dongle asleep, `--fly --players=N` at the
controls) is still owed** — this environment has no gamepad to reproduce it with; the fix is
reasoned from the roster the dongle is documented to present (`Pads.cs:11-15`), not measured against
it. Folds into F52's playtest pass.

**⚠ Traps.** `--no-pads` and `--debug-join` paths must keep working (scripted runs rely on them).
`--no-pads`: `Connected()` returns empty, so every assignment (P1's included) is empty — unchanged.
`--debug-join`: menu-only, never reaches `AssignPads` at all — unchanged.

## E42 ☑ Camera views and look-back for players 2–4 (`BL-372`)

**Goal.** Every player can check their six and use fixed views from the pad.

**Evidence (confidence: traced; binding is a decision).** `CameraController.cs:139-167` reads
`_keyDown` for `BackActive`/`ActiveView`; `FlightController.KeyDown` requires `UseKeyboard`
(`FlightController.cs:2153`), granted only to player 1 (`FlightRigAssembler.cs:105`). The D-pad
is taken by the weapon selectors (class comment), so the binding is a real design question.
`--view=` also pins one view on every pane.

**Approach (landed).** Decision: the right stick, unused by the flying pane (left stick is
roll/pitch, shoulders are yaw, triggers are throttle, A/B/X/Y/Start/D-pad-left/right are all taken
— confirmed by grepping every `JoyButton`/`JoyAxis` read in `src/Flight`). Deflecting it
(`CameraController.PadLook`) swings the external view around the plane at the SAME dynamic radius
the chase camera and numpad views share — a continuous twin of the fixed views rather than a
discrete list — and clicking it (`JoyButton.RightStick`) holds the look-behind view, the pad twin
of holding numpad 0 (`CameraController.BackActive`'s new `padClick` parameter). Both are read in
`FlightController` (`PadLookInput`, alongside the view-selection block), never in
`CameraController`, the same "no pad devices in the camera" rule `OrbitInput` already follows —
stick values arrive pre-curved through the existing `StickCurve` deadzone, so centring the stick
reads as exactly `(0, 0)` and the view snaps back to the ordinary chase pose with nothing to ease.
This is a genuinely new binding — the original names no such control — so the yaw/pitch range and
which stick/button own it are a UX call for this port, not a decode. `--view=` stays all-panes
(documented already in `docs/cli.md`); making it per-pane wasn't trivial and is out of scope here.
`docs/controls.md` updated (mandatory for a player-input change).

**Model recommendation.** medium.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings; `dotnet format
--verify-no-changes` clean. `.\RunTests.ps1`: 1298/1298 units, 61/61 engine suites, 14/14 goldens
hash-identical, engine errors clean, hitch clean — none of the new code is exercised by an
automated suite (no pad-axis fixture exists, same gap E41 hit), so this is "did not break
anything" evidence, not a positive check of the new behaviour. A scripted `--fly --players=2
--chapter=C1 --mission=IA1 --no-pads --det --frames=120 --screenshot=` probe exits 0 with 0 ERROR
lines and no `padlook`/back-view log activity (no pads attached, both stick axes read 0 —
confirms the baseline chase path is unaffected, the `--hold`-equivalent check for this item). An
8-chapter `--freecam --chapter=<X> --frames=60 --screenshot=` sweep (C1, C1B, C1C, C2, C2B, C3, C4,
C5) all exit 0 with 0 ERROR lines each. **The at-the-controls pass — 2 players each switching their
own look-around/look-back independently on a real pad — is still owed**: this environment has no
gamepad to reproduce it with. Folds into F52's playtest pass, same as E41.

**⚠ Traps.** `BL-296`'s ActionMap is the eventual home for the binding — it is wired through
`PadPressed`/`PadAxis`, the same per-player polling seam every other pad control uses, so that
migration stays mechanical; no half-ActionMap was built here.

## E43 ☑ Splitscreen pause (`BL-373`)

**Goal.** A decided pause behaviour exists in 2–4 player sessions.

**Evidence (confidence: traced; scope is a decision).** `FlightRigAssembler.cs:108` hard-coded
`AllowPause = _in.RigCount == 1`; `FlightController.cs:2179` consumed it; Esc tears the session
down (unaffected here). M2.5 explicitly decided "P debug pause stays single-player-only", so this
was a revisit.

**Approach (landed).** Decision: pause becomes a real pause-menu hook, not just the debug freeze
left as-is — a shared full-window "PAUSED" board (`PauseBoard`, `VersusBoard`'s construction),
and only the player who paused may resume (not "anyone"). `PauseState` (engine-free, `VersusMatch`'s
shape) is the new seam: `TryToggle(playerIndex)` pauses unconditionally from running, but only lets
`OwnerPlayerIndex` resume it — a rejected attempt from another player is a silent no-op. One
instance per session (`GameSession.BuildFlightRigs`, right after the rig loop), assigned to every
rig's `FlightController.PauseState`, single player included, so there is one pause path rather than
a solo one plus a splitscreen one. `FlightRigAssembler`'s `AllowPause` gate is now unconditionally
`true` for every human rig (it only decides whether THIS rig's P/Start reads at all — AI rigs and
the suites' bare test rigs still pass `false`); `FlightController._Process` still owns the actual
halt, mirroring `PauseState.Paused` into `GameClock.Halted` every frame exactly as the pre-E43
toggle did, so the fixed-tick/anim-clock relationship this item's trap warned about is untouched —
only *who* may flip the bit changed, not how a halt behaves once flipped. `docs/controls.md`
updated (mandatory for a player-input change).

**Model recommendation.** medium.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings; `dotnet format
--verify-no-changes` clean. `.\RunTests.ps1`: 1302/1302 units (4 new — `PauseStateTests`: any
player pauses, only the owner resumes, a rejected attempt changes and fires nothing), 61/61 engine
suites, 14/14 goldens hash-identical, engine errors clean, hitch clean. A scripted `--fly
--players=2 --chapter=C1 --mission=IA1 --no-pads --det --frames=120 --screenshot=` probe exits 0
with 0 ERROR lines (no pad/key input in a `--det` run, so pause never triggers — the
`--hold`-equivalent confirmation that the baseline flight/sim path is unaffected). An 8-chapter
`--freecam --chapter=<X> --frames=60 --screenshot=` sweep (C1, C1B, C1C, C2, C2B, C3, C4, C5) all
exit 0 with 0 ERROR lines each. **The at-the-controls pass — 2 players, P2's Start pauses both
panes, only P2's Start resumes — is still owed**: this environment has no gamepad to reproduce it
with, and no `--det` hook exists to script a mid-run key/pad press. Folds into F52's playtest pass,
same as E41/E42.

**⚠ Traps.** The pause path must not desync the fixed-tick sim from the anim clock — resolved by
leaving `GameClock.Halted` the single source of truth every halt-aware consumer already read;
`PauseState` only gates who may write it, never how the write behaves.

## E44 ☑ Per-seat spectator control (`BL-375`)

**Goal.** Two downed IA pilots spectate independently.

**Evidence (confidence: traced).** `SpectatorCamera.cs:128, 203, 324` uses raw
`Input.IsKeyPressed`/any-pad reads with no `PadDevices`/`UseKeyboard` split; architecture.md's
entry records the lockstep symptom.

**Approach (landed).** `SpectatorCamera`'s constructor takes optional `padDevices`/`useKeyboard`
params (default `null`/`true` — every connected pad plus the keyboard, so every pre-existing call
site — `--freecam`, the anim lab, the weapon lab — is unaffected). The four raw-read sites the
Evidence named (`Axis`, `PadAxis`, `PadTrigger`, `PadButtonAxis`, plus the `Shift`/`Ctrl` boost
checks in `Move`) became instance methods gated on those fields.
`GameSession.BeginInstantActionSpectate` passes the downed pilot's own
`FlightController.PadDevices`/`UseKeyboard` — the same filter the flying panes use — when it
constructs that pilot's spectator; one instance per downed pilot already existed (the method runs
once per rig crossing into `Spectating`), so no new fan-out was needed there. Mouse look (RMB
drag) has no per-seat equivalent — one physical mouse — and stays shared, same as before; noted
in the class doc comment rather than worked around. `docs/controls.md` updated (mandatory for a
player-input change).

**Model recommendation.** medium, low effort.

**Verify (done 2026-08-15).** `dotnet build CSVM/CSVM.sln` clean, 0 warnings; `dotnet format
CSVM/CSVM.csproj` (the pre-commit hook's own command) leaves both changed files unmodified.
`.\RunTests.ps1`: 1302/1302 units, 61/61 engine suites (including `instant-action-end`, which
exercises `BeginInstantActionSpectate`'s lives-ledger → spectate transition end to end and stayed
green, unchanged), 14/14 goldens hash-identical, engine errors clean, hitch clean — no suite
covers pad/key device filtering (no pad-axis fixture exists, same gap E41/E42 hit), so this is
"did not break the spectate transition" evidence, not a positive check of the new split. An
8-chapter `--freecam --chapter=<X> --frames=60 --screenshot=` sweep (C1, C1B, C1C, C2, C2B, C3,
C4, C5) all exit 0 with 0 ERROR lines each — the default-filter path (every pre-existing call
site) is unaffected. **The at-the-controls pass — 2 players both out of lives, each moving their
own spectator view independently on real pads — is still owed**: this environment has no gamepad
to reproduce it with, and no `--det` hook exists to script a mid-run key/pad press (same gap
E41/E42/E43 hit). Folds into F52's playtest pass.

**⚠ Traps.** None known beyond keeping the single-player spectator identical (confirmed: default
params reproduce the pre-E44 `Pads.For(null)`/always-on-keyboard behaviour exactly).

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
