# Testing

The in-engine assertion harness: `--run-tests` and the `--dump-*` probes, plus the `CSVM.Tests/` xUnit project's engine-free reader units and golden invariants.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## CSVM.Tests/
The xUnit project `dotnet test` runs (net8.0, `ProjectReference` to `CSVM.csproj`): engine-free
reader units (`Zrdr`, `WavFile`, `SoundDefs`, `WeaponDefs`, `Messages`, `SessionPaths`, `GameZ`
transform arithmetic, `MarkerRig`, `AnimDefs`, …) plus golden invariants over `extracted/`.
⚠ **The `SessionSpec*Tests` trio is the launch surface's only per-rule coverage** (84 facts); two
  deliberate defects are asserted as-is and labelled at the fact — do not "fix" one to make a test
  read better.
⚠ `fixtures/` is hand-authored with invented `probe_*` names; **a trimmed piece of a real
  extraction is still a game asset and never gets committed**.
⚠ Golden invariants resolve data via `CSVM_DATA_ROOT`; when absent, `[ExtractedDataFact]` sets
  xUnit `Skip` (never a silent pass). Golden *numbers* commit; golden *content* never does.

## src/Testing/Probes.cs
The assertion cores behind the `--dump-markers` / `--dump-weapons` / `--dump-loadout` /
`--dump-flight` / `--dump-mips` / `--damage-test` / `--effects-test` inspection reports. Each probe
does the work once and returns both halves: the report text the flag prints and writes, and a
structured verdict (counts, per-row booleans, failure strings) a `--run-tests` suite asserts on.
`FlightEnvelopeAll` is the whole-plant instrument `--dump-flight=all` and the parity ledger share, so
a dump diffed against an older one and the ledger published from it cannot disagree. A flight row's
`Target` is always decoded or a named product exception; a footage figure lives in the row's text as
a discarded annotation and gates nothing. Angular rows read `PhysicalBodyRates`, never the original's
stored quaternion half-angle state. Every scenario also carries `EnvelopeMargins`: its distance
from each bounding term (the G clamp, the C_L ceiling, the AOA window, the stall flag, the altitude
band, the dive cap) and which decoded branches it drove.
`Effects` owns the effects sweep — the puffer half and the template-MESH half (`BL-061`), the
latter counted only through `MeshCensus` (per-root per-tick PEAK + distance-to-play-point +
post-stop residual; a final-sample census misses meshes the data turns off inside the window,
INSTR-11). The `effect-template-mesh` suite counts through `MeshCensus` too, so the sweep's
verdicts and the suite's assertions cannot drift apart.

## src/Testing/EnvelopeMargins.cs
The reachability half of the flight-envelope report. `Sample` reads one completed `FlightModel`
step's public state and keeps, per scenario, the peak load-factor demand against the ±5/9 clamp, the
peak demand over the aerodynamic C_L ceiling, the deepest opposing-command limit, the closest
approach to the stall speed, the peak altitude against the 2000 m band boundary, the delivered
body-up G against the authored `lowGs`/`highGs` starts, and the peak speed against the dive cap;
`Take` formats that as the row's `margins:` line. Across the whole airframe it also records which of
`Branches` the scenarios reached. Nothing here feeds a force. ⚠ One instance per airframe and not
thread-safe: the probe owns it for one report. Which instrument drives each unreached branch is in
[`org/flightModel.md`](org/flightModel.md), "Parity ledger".

## src/Testing/TestHarness.cs
`--run-tests[=filter]`: the suite registry, `TestContext` (assert verbs, resolved data paths, a
scene-tree host, and `WithWorld` — the chapter-world builder over `WorldSession`; the
mission-override form `WithWorld(chapter, collision, mission, body)` builds a chapter at another
mission and never caches it, since the cache is keyed by chapter alone — `zeppelin-damage` wants
C1 at M04; a collidable build evicts every cached collidable world first, since one physics space
holds them all and a cached chapter's scenery would stand inside the new world's airspace, which is
how C1's scenery met C2/M03's racers), the
PASS/FAIL/SKIP table, `test-report.json` in `TestContext.ScratchDir`, and the process exit code.
`Select` is the pure selector over the flag's value — comma-separated terms, `suite:` exact, `tier:`
a `SuiteCatalog` tier, anything else a substring — returning registry order and reporting every term
that matched nothing, which `Run` refuses before any suite starts. `SuiteShards` handles the one
term that divides rather than selects; `Run` applies it AFTER the miss checks, so an empty shard is
a legitimate division and an empty selector is still a typo. A sharded run's `ScratchDir` moves
beside its `--log-file`, which is what keeps concurrent shards (and concurrent runs) off each
other's report and artifacts. The options a `--run-tests` process reads and writes go to a
per-process scratch directory instead of the player's file; that rule lives on `OptionsStore`, which
owns the override. `TestContext.
EmitterFactory` (mutable, default null) forwards straight into `WorldSession.Options.EmitterFactory`
for the next `WithWorld` build. `WithPrivateWorld` is what a suite installing one uses: never read
from the shared cache and never written to it, freed when the body returns, so no build option a
suite chose can ride into a later suite's world. `BuildWorld` opens its
archives through `SessionArchives.OpenFor(ArchiveIntent.Suite, …)`, then `using`s the returned
`Textures`/`Sounds` itself — `OpenFor` states the (both-false) lifetime flags, it does not own the
disposal. One `DecodeCache` per run rides both that call and `WorldSession.Options.Decode`, so the
58 builds of a full catalog decode 8 chapter gamez and 16 programs rather than 58 of each; every
`Gamez`/`Program` a suite reads is therefore shared and read-only. `TestWorld` also carries the
parsed `Gamez` past the build (not disposable, unlike the texture archive) so a suite can build
real geometry of its own from it — the effect-template stage `effect-template-mesh` needs.
Engine-error allowlisting and pass/fail policy: `ErrorAllowlist`, in code. Windowed-run rule:
verification.md LOG-8.

**B11's phase attribution** (`test-report.json` schema 2). `BuildWorld` installs a private
`StartupProfile` as `Current` for the span of one build (restored in a `finally`, so a thrown build
cannot leak it onto a later suite or a real session), so `WorldSession.Build`'s and
`SessionArchives.OpenFor`'s own `Mark`/`Record` calls — the same ones a real session's `[perf]
startup` line reads — land on it with no second instrumentation invented for the harness.
`PhaseAttribution.Categorize` buckets those phases into archive/decode, sound preparation and
runtime/world construction against the *outer* wall-clock stopwatch `BuildWorld` keeps of its own
(not the profile's internal clock), so the four figures it returns always sum to exactly what
`TestContext.WorldBuildSeconds` attributes to the suite — no second clock to drift against the
first. `TestContext.ResetForSuite` clears that accumulator, and the three build knobs
(`EmitterFactory`, `ExtraPrewarmSoundNames`, `CutsceneRoots`), once per suite in `Run`, the same
lifetime `Failures`/`Notes`/`Counts` already have; disposing a world a suite built (not the shared
cache's own end-of-run teardown, reported only as the run's `finalDisposalSeconds`) is timed the
same way. What is left of a suite's wall time once build and disposal are subtracted is `rest`:
manual simulation plus assertion work, floored at zero, with any stopwatch overrun reported
separately rather than folded silently into a healthy-looking zero. The console line stays one line
per suite (a no-world suite prints no phase suffix at all) plus one totals line after the loop;
`test-report.json` carries the same figures per suite and as run totals, plus the run's `selector`
and the loaded `CSVM.dll`'s own path/MD5 (`binary`, read from the fixed
`<repo>/CSVM/.godot/mono/temp/bin/Debug/CSVM.dll` `RunTests.ps1`'s own perf stage hashes as
`$PerfDll` — not `Assembly.GetExecutingAssembly().Location`, which Godot's Mono host returns empty),
so a report can be matched to the exact build and suite set that produced it.

## src/Testing/SuiteShards.cs
Godot-free and pure (`CSVM.Tests` proves it without the engine): the `shard:<index>/<count>` term
and the division behind it. `Parse` lifts that term out of a `--run-tests=` value and hands the rest
back as the selector, treating a malformed or out-of-range one as an error rather than as a full
run. `Plan` divides an already-selected list longest-unit-first onto the lightest shard, ties broken
on the item's own position, and returns each shard in the input's order, so one tree always divides
the same way. `SuiteWeights` is `analysis/engine-suite-weights.json`: per-suite measured seconds, a
default for a suite the file does not name (`Unweighted` reports those), and `Groups`, the sets a
shard may not split. A missing or unreadable file weighs every suite the same, because an even
division is still a correct one.

## src/Testing/PhaseAttribution.cs
Godot-free and pure (`CSVM.Tests` proves it without the engine): buckets a `StartupProfile`'s raw
phase names — `gamez`/`textures`/`anim`/`zrdr` as archive/decode, `sounds`/`prewarm` as sound
preparation, `world`/`clutter`/`bind` as runtime/world construction — into `Categorized`, with
whatever a build's own wall-clock time does not cover landing in `OtherMs` rather than vanishing.
`Rest`/`Overrun` do the suite-level arithmetic: `wall − build − disposal`, clamped at zero, with the
clamped shortfall reported by `Overrun` instead of a falsely healthy zero. Both `zrdr` phases (a
mission's `MissionSetup.Load` inside `WorldSession.Build`, and the sound-def/group load inside
`SessionArchives.OpenFor`) share one name by `StartupProfile`'s own same-name-accumulates rule; both
are decode, so the shared archive/decode bucket is correct either way.

## src/Testing/CountingEmitterFactory.cs
`IEmitterFactory` for a suite: `Create` always succeeds and hands back a `CountingEmitter` — no
`TextureArchive`, no `MultiMesh`, no Godot type anywhere in its own state, just `Started`/`Stopped`
counts and whether it is sustaining now. Reached by installing it on `TestContext.EmitterFactory`
before a `WithWorld` build (`WorldSession.cs`); `CountingEmitterFactory.Built` is the list a suite
reads to confirm the fake was actually reached rather than a real `Puffer` — the seam `BL-241`'s
own fix note asked for.

## src/Testing/RecordingEmitterRenderer.cs
`IEmitterRenderer` for a suite: it keeps the particles a `Puffer` hands it (`LastFrame`, `Shown`,
`MaxShown`, `MaxIndex`, `MaxFrame`, plus the `Capacity` the emitter sized) instead of drawing them,
so `puffer-modes` asserts on burst / distance-trail / sustain with no atlas, `TextureArchive` or
`MultiMesh` in the path. The mirror of `CountingEmitterFactory` one seam lower: that fake replaces
the whole emitter so `EmitterDirector`'s LIFETIME is assertable, this one replaces the draw so the
emitter's own MODES are. Neither covers the other's job.

## src/Testing/SuiteCatalog.cs
The registry of the in-engine assertion suites, discovered from the `[Suite("name", "what")]`
attribute each body carries in the `*Suites.cs` modules; the catalog keeps no per-suite table, so
adding a suite touches one file. Registry order is alphabetical by name, and it is not
presentation: `SuiteShards.Plan` breaks balancer ties on registry position and sorts each shard by
it, so a rerun divides the same way only while that order is fixed, and reflection order is
unspecified. No suite depends on running after another. A malformed declaration throws at discovery
rather than being skipped, because a suite that quietly fails to register runs nowhere and is
reported by nothing. `QuickTier` is the checked-in membership of `--run-tests=tier:quick`, resolved
through `Tier(name)`, and holds one representative per failure surface rather than the cheapest
rows.

## src/Testing/*Suites.cs
Seventeen domain modules hold the in-engine scenario bodies, each named for the whole of what it
files: `PufferSuites` (the emitter model's modes, wind, fades and fire column), `CombatSuites`
(loadouts, live fire, aim assist and the hit chain), `OrdnanceSuites` (a round's flight, guidance
and ends), `InstantActionSuites` (the mission runtime from spawn to wrap-up), `AiSuites` (how a
computer-controlled combatant behaves: pilots, mounted gunners, combat voice, and the inert state
they wait in), `TargetingSuites` (the `TargetRef` abstraction, candidate pool, sticky selection,
input decoding and marker HUD), `TargetingCandidateSuites` (D36's widened AI acquisition,
`BL-363`: the team gate over a registered structure and the win routed to `GroundTarget`, never
`Target`), `WingmanSuites` (D34's netless `mode wingman` station-keeping, as geometry and flown
against a scripted leader), `CampaignSuites` (profile persistence, the objectives runtime and the
mission-end flow), `MusicSuites` (the state-driven score), `ZeppelinSuites` (motion, fighter
launch, multi-zone damage,
broadsides), `DamageSuites` (spending armor and health, and the injure staging those ledgers
fire), `DestroyChoreographySuites` (the choreography a death dispatches: destroy defs, wreck
flights, crash rigs, callbacks and stops), `AnimationAndEffectsSuites` (anim launches, effect
templates, washes and burst timelines), `WorldAndToolSuites` (the built world's data gates
and censuses, lighting and viewers, and the lab surfaces), and `WorldFidelitySuites` (the
mid-mission world behaviours the shipped data drives: the area-selected node toggle over C3's three
story rectangles, the scripted-path follower over C1's own takeoff path, the mission script's and
the generator's hangar doors over C1/M04, and the `FOG_STATE` event over its intro), plus
`AlphaCutoutRaySuites` (the BL-477 census: what actually stops a weapon ray short of C3/M01's cargo
zeppelin's slung tanks, as first-collider node names over a sphere of aspects) and
`AirframeColliderSuites` (the collision hulls measured against the mesh they came from) and
`CampaignRacerSuites` (CM13's six racers spawned from C2/M03's roster into its collidable world,
flying `dzpath1` and `dzpath2` on rails end to end with the mission's opening stepped through the
director, so the propane tanks hung in `dzpath1`'s gate are blown before anyone reaches them, and
nobody rams the `dbase` arch). They depend on
`TestHarness` through
`TestContext`; shared fixtures are separate focused modules, not an all-purpose suite helper.

The suites cover plane/loadout bindings (stock and, since M3 B4,
the full-rig `Loadout.ForRig`), live weapon fire, the carried turret gunners (`carried-turrets`:
build from ai.zrd + the thirdp mount, arc-centre rest pose, track/fire/hit under the host's
shooter id, bored-window fire suppression with live tracking, the nearer-end-stop park, YAW [0,0]
as unrestricted, a crashed host going quiet), the air-to-air hit chain (`air-to-air`: two real
flight rigs on manual sim steps — body strike, struck-shape→part mapping, armor-first data-value
damage, the whole-vehicle kill rule (a dead critical nose alone does NOT crash — the retired
divergence's own pin — and exhausting the fourth zone does), crashed-plane immunity, the
zero-self-hits negative case, which
must stay non-optional, `Downed`-into-`VersusMatch` attribution: the weapon kill scores
exactly the shooter, killer-less and unowned-round deaths score nobody, and the VS respawn loop:
`AutoRespawnAfter` 3 s respawns at that mark in sim frames, respawn reports nothing, null waits
for R),
the AI actor seam (`ai-actor`: an `AiPilot`-driven plane spawned into an already-stepped sim —
present, flying its orders, retargetable mid-flight, damageable and killable with the kill
attributed),
the far-field plant's session plumbing (`ai-far-field-plant`: two AI rigs at 100 m and 1200 m from
a human, so the branch is watched selecting in both directions: the range horizontal, the NEAREST
of several humans deciding it, an unbound seam staying near-field, and the far rig holding
throttle × fd_speed + 5 m/s where the near rig on identical orders does not), the AI gunnery
(`ai-gunnery`: held rigs firing through the real fire-control path — nearest-hostile
acquisition as mutable state, the quick-draw and ±11° cone gates, dead-eye skill 1 vs 9 hit
rates on a fixed seed, the kill under the AI's shooter id, and the IsHumanPiloted assist
exclusion A/B'd on one rig),
the inert aircraft state (`inert-aircraft`: four REAL instruments — a
physics raycast, `CollectAircraft` into an `AimAssist.Scan`, a round fired through the pool, and
`SimStep` — run over a live control, an aircraft built inert, and that same aircraft after
`Activate`, so every observation is watched flipping in both directions rather than only being
absent; plus the roster check that an inert plane is listed as a not-live candidate),
the Instant Action zeppelin run (`instant-action-zeppelin`: the
`zeppelin_type` selection with its cargo fallback, a real generator on the wave-credit budget
launching nothing uncredited and exactly one wave's members once credited — from the same bay drop
point the plane arm uses, never a fresh spawn — the parked-counts-as-present trigger, and
`ZeppelinRuntime.Hold`; plus, against C1/IA1's own built world, the objective starting hidden with
every one of its gasbag's collision shapes off and the decoded activation bringing both back),
the Instant Action mission end (`instant-action-end`: one mission type at
a time, each driven to its end through the SAME signal `GameSession` subscribes to — a spawned
ace's own `Downed`, `InstantActionWaves` stepped over real aircraft, `StuntRace`'s all-finished
path over C1/IA1's authored zones, and C1/M04's piratezep really destroyed through the F18 damage
path — every win check paired with a SECOND runtime of another mission type on the same signal that
must stay Running, plus the lives ledger's two ends on one real `FlightController`: with a life
left the armed 3 s crash cam respawns it, with `Spectating` set it is still a wreck 10 s later.
Its M04 world is mission-overridden and therefore never cached, so the gasbag kills cannot reach
another suite),
destructible stages/death/census, animation
stops and bounce-terminated launches, the full effects sweep (`effects-census`: every effect
resolves, template meshes peak at the CALL SITE not the stage origin, none stays lit after its
stop — `Probes.Effects` rows asserted; its puffer/mesh tallies are golden counts under the
suite's own conditions, literal seed 1 + the counting factory, pinned separately from the probe's;
plus the staged-root derivation tripwire at both binds, the crash half on two airframes),
emitter lifetime and the emitter's own modes, texture
flattening, and glTF/collision/node visibility. `emitter-lifetime` is the only suite installing a
fake `IEmitterFactory`, so it builds a private world through `WithPrivateWorld` and unwinds the
factory in a `finally`: a cached world would hand that fake to every later suite on its chapter,
which is a hazard no registry order can answer. Per-suite traps
(one-frame physics limits, the shared-world read-only rule, golden-count provenance, the
loadout-bind split decision) live as comments on the suites themselves, in code.
`fbfx-flash` reuses `effect-template-mesh`'s `WithEffectStage` host to play `he_ground_effect` at the
camera (so its own `PLAYER_RANGE` gate passes) with the runtime's `ScreenFlash` sink recording:
it asserts the six wash steps' authored run times AND the gaps between their fires, since the
per-step run time alone is reported correctly even by a handler that returns 0 as its duration and
fires all six in one instant. Shown able to fail exactly that way. The chain's total gets one step
of headroom per gap — the authored run times are exact multiples of the step but not of binary
float, and three of the five gaps land one step late. It then asserts B12's routing on both sides of
the sink: every step reports the burst's own point and the def's authored `10000` m² gate, and a
real two-pane `ScreenFlash` over two `Camera3D` nodes 120 m apart paints one pane, the other pane, or
both, purely by where the burst is. The able-to-fail control is the same overlay with no `ViewerSet`
bound, which paints both — the un-routed behaviour; disabling the routing fails three of the checks.
Its blend-channel half puts both cameras at ONE point, so the ramp's proximity gate cannot tell the
panes apart and any difference is the victim routing alone: `PlayBlend(1, …)` stepped through its
attack paints pane 2 red and leaves pane 1 clear, an HE ramp then reaching both paints pane 1 exactly
as before the channel existed and pane 2 the wash composited over it, the wash is gone at its
duration, and an AI's player index addresses no pane.
`ordnance-burst-timeline` proves the ordnance burst timeline: it plays `he_ground_effect`,
`flash_effect` and `sonic_ground_effect` on its own miniature world-effects stage and matches the
WHOLE recorded `OnEventDispatched` log of each — every sequence, every event, in its sequence's
order, at its authored instant — against a table read off the def JSON by hand. Order is asserted
by CONSUMPTION (a row is claimed by the first lane whose next unconsumed step it matches on
sequence/index/kind/name, so an early or duplicated row matches nothing and is reported stray),
which is what a membership check cannot do and what every Wave B item needs to be measured at all:
`large_fireball`'s parked `stop_p1trail` must dispatch nothing (B12 — the shown-able-to-fail case),
`sonic_light_seq` must run exactly twice, the second pass restarting at 1.2 s, and the
`START_TIME ANIMATION`/`SEQUENCE` gates must land on 1.2/1.5 s. It drives at **1/240 s**, four
times finer than `SequenceRunner.AnimFrame`, because the authored gaps go down to 0.01 s and none
of the three defs carries a `LOOP` for the AnimFrame floor to matter to. Two traps are closed by
construction rather than by assertion order alone: the staged template roots are DERIVED
(`EffectCatalogue.StageRootsFor` against the chapter gamez, which throws on an anchor that resolves
nowhere) instead of hand-listed, and the TTL is 32 s — inheriting `--effects-test`'s 0.3 s would
truncate the 1.2 s wash while everything else still read green. The full log of all three lands in
`.scratch/ordnance-burst-timeline.txt`.
`effect-pool-reset` proves the checkout re-reset (`AnimRuntime.ResetCheckedOutCopies`): the same
derived sonic stage built over FOUR pool slots, `sonic_ground_effect` played five times to completion
(so `PoolRecycles` stays 0 and the fifth play lands on the first's copy), and every ring mesh under
slot 0's `sonic_ring1..5` read three frames into play 1 and play 5: visible-in-tree, scale and the
per-instance opacity must agree, and both plays must be drawing at least one ring. Without the
re-reset the fifth play's rings read INACTIVE at opacity 0, which is the sortie-long dead-burst
symptom this suite exists to hold shut.
`airframe-hull-coverage` builds all eleven player airframes and measures `PlaneCollider.Layout`
against each one's own triangles: every hull inside the box it replaces and above the thickness
floor, the fuselage leading the part order with only the four names `PlaneDamage.MapStruckPart`
knows, and no more than 0.5 % of the silhouette's triangle area outside every hull; its artifact
lists the per-part box and hull volumes, which is the overhang the sweep no longer bridges.

## src/Testing/SuiteConstants.cs
The shared golden inputs used by more than one scenario module: airframe and weapon counts, puffer
timing, the destructible census, and texture samples.

## src/Testing/BurstTimeline.cs
The three value types describing an authored ordnance-burst timeline and its observed dispatches.

## src/Testing/SuiteViewers.cs
Builds a test pane camera at a supplied world position for suites that exercise `ViewerSet`.

## src/Testing/EffectStageSuiteHelper.cs
Builds and frees a production-shaped, pooled effect-template stage for mesh-visibility suites.
## src/Testing/GoldenShot.cs
The engine half of the golden-image tripwire: `PixelHash(Image)` (md5, lower-case hex) and
`Adapter()` (`"<gpu> / <api>"`). Called at the `--screenshot` save site, which prints
`[core] shot pixmd5=… size=… gpu=…` on every capture; `RunTests.ps1`'s `goldens` stage parses that
line and compares against `analysis/goldens/manifest.json`.

## src/Testing/ProbeRunner.cs
The `--dump-markers`/`--dump-weapons`/`--dump-flight`/`--dump-loadout`/`--dump-mips`/`--run-tests`/
`--effects-test`/`--damage-test`/`--destroy=` probe wrappers,
constructed once in `Launcher._Ready` after the base paths settle — the Launcher dispatches
the `--dump-*`/`--run-tests` early quits itself and hands the runner to each session node.
Each method reads a `SessionSpec` passed **per call**, not stored — a menu launch can replace the
caller's spec between calls, so a cached one would silently answer with a stale launch's flags.
`TriggerDestroy` (`--destroy=`) and `ForceObjective` (`--debug-objective=`) are the two scripted
world forces that live here rather than in a session: `ForceObjective` wakes one campaign objective
and drives the nodes its `INACTIVEn` conditions name inactive, so the graph completes it off its
own conditions on the next step instead of a mark being faked into the display. The objectives
suite drives its completions through the same `DriveInactive`.

## src/Testing/CaptureDirector.cs
The `--screenshot=`/`--shots=`/`--frames=` state machine plus F11/F12's placement print and ad-hoc
save, constructed once in `Launcher._Ready` from the launch spec
(process-scoped, never re-armed by a menu relaunch); `Tick()` runs from the Launcher's `_Process`
, which is what keeps `--menu --screenshot` capturing the launchscreen with no session node
alive. No back-reference to the host node — `Tick`/`PrintPlacement` take the
camera/orbit/rigs/clock/plane/menu-visible they need as parameters.
`SaveScreenshot` is the ad-hoc save every screen shares, and `ShotDir()` is the single folder they
all write into (`Screenshots/` at the repo root, git-ignored). It returns the file it wrote, which is
how a caller or a suite says where a shot landed rather than re-deriving the path.

## src/Testing/GltfExporter.cs
Exports the viewer plane's `Node3D` subtree to a glTF file — mesh + the currently painted livery
texture, current damage state baked in, no animation. `Export(plane, path)` works on a throwaway
`plane.Duplicate()`: it frees every hidden `Node3D` (the panel/flare `Visible` toggles are how damage
is baked) and the point-sprite `"lights"` instances, converts each surface's custom `ShaderMaterial`
skin to a `StandardMaterial3D` (painted `albedo_tex` + vertex-colour-as-albedo, mirroring
`PlaneBuilder.FlareMaterial`), then `GltfDocument.AppendFromScene` + `WriteToFilesystem`. Format is
extension-driven (`.glb` default). Two triggers: the `--export-gltf=` one-shot via the frame-stepped
`Tick()` (waits for the plane, exports, quits with the write's success as the exit code), and F10 in
the Launcher.

