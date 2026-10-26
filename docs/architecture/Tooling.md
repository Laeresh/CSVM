# Tooling

The runtime tooling the game and the in-engine harness share: the `--dump-*` probes and their wrappers, the `--screenshot`/`--shots` capture, the golden-image hash, the glTF export, and the `--synthetic-data` tree. The game and `CSVM.Testing` both depend on this namespace; the one call from here into the harness is `--run-tests`' dispatch in `ProbeRunner.cs`.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Tooling/Probes.cs
The assertion cores behind the `--dump-markers` / `--dump-weapons` / `--dump-loadout` /
`--dump-flight` / `--dump-mips` / `--damage-test` / `--effects-test` reports. Each
probe does the work once and returns both halves: the report text a flag prints and writes, and the
structured verdict a `--run-tests` suite asserts on, so a dump and the suite reading it cannot
disagree. The envelope, effects, damage and debris-shading sweeps each carry their thresholds, row
order and sources at their own members. The reachability half of a flight row is
`EnvelopeMargins.cs`, the suites asserting on these verdicts are the `Testing/*Suites.cs` modules,
and the branch vocabulary is [../org/flightModel.md](../org/flightModel.md).

## src/Tooling/EnvelopeMargins.cs
The reachability half of the flight-envelope report. `Sample` reads one completed `FlightModel`
step's public state and keeps, per scenario, the distance to each term that could have bounded it
(the G clamp, the C_L ceiling, the AOA window, the stall speed, the altitude band, the dive cap);
`Take` formats that as the row's `margins:` line, and across an airframe it also records which of
`Branches` the scenarios reached. Nothing here feeds a force. Read `Probes.cs` for the report it
lands in; the branch names and which instrument drives each unreached one are in
[../org/flightModel.md](../org/flightModel.md), "Parity ledger".

## src/Tooling/GoldenShot.cs
The engine half of the golden-image tripwire: `PixelHash(Image)` (md5, lower-case hex) and
`Adapter()` (`"<gpu> / <api>"`). Called at the `--screenshot` save site, which prints
`[core] shot pixmd5=… size=… gpu=…` on every capture; `RunTests.ps1`'s `goldens` stage parses that
line and compares against `analysis/goldens/manifest.json`. The Launcher's `[perf] gpu=` line
reads `Adapter()` too.

## src/Tooling/MemoryAdmission.cs
The engine's side of the machine-wide memory ledger `MemoryLedgerCore.ps1` keeps in
`%TEMP%\csvm-mem` (docs/tooling.md, "The memory ledger"). `Refuses`, called once from
`Launcher._Ready` after the log opens (editor builds on Windows only), judges a non-interactive
launch: below the floor it logs the reason and the Launcher quits with `TripwireExitCode` (75);
started without `CSVM_MEM_RESERVATION` it holds a delete-on-close reservation file for the process
lifetime. `Kind`, `IsNonInteractive` and the floor mirror the script's rules.

## src/Tooling/ProbeRunner.cs
The `--dump-markers`/`--dump-weapons`/`--dump-flight`/`--dump-loadout`/`--dump-mips`/`--run-tests`/
`--effects-test`/`--damage-test`/`--destroy=` probe wrappers, constructed once in `Launcher._Ready`
after the base paths settle: the Launcher dispatches the early quits itself and hands the runner to
each session node. Each method reads a `SessionSpec` passed per call rather than storing one. It
also holds `TriggerDestroy` (`--destroy=`) and `ForceObjective` (`--debug-objective=`), the two
scripted world forces belonging to no session. `RunTestSuites` is the one call into `CSVM.Testing`
from outside it. Read `Probes.cs` for the work each wrapper calls into.

## src/Tooling/ShaderDiagnostics.cs
`--debug-shaders`: a census of the distinct shaders the running tree draws, by `SceneBuilder`
family or owning node type, at sim frame 240 and after every live switch; the wall time and
pipeline compilations of the twelve frames after a switch; and every frame over 33 ms with the
pipelines it compiled. The pipeline counts are published from the render thread, since reading
them on the main thread waits for the previous draw. Off unless the flag is given.

## src/Tooling/CaptureDirector.cs
The `--screenshot=`/`--shots=`/`--frames=` capture state machine plus F11's pose print and F12's
ad-hoc save, built in `Launcher._Ready` from the spec and `Tick()`ed from its `_Process`, so
`--menu --screenshot` captures the launchscreen with no session alive. That capture reads
synchronously, since the process exits on its file; under `--xr-sim` it reads each eye's layer and
saves them side by side or alone. `SaveScreenshot`, the F12 save every screen shares, writes
`PaneReadback`'s frame into `ShotDir()` (`ShotDirFor`: the repo's `Screenshots/`, or one beside an
export's executable) on the worker it lands on. F11 reads each pane's own camera through the pure
`PlacementLines`: a `--freecam` line, `--fov=` off the external base, each aircraft's `--fly` line.

## src/Tooling/GltfExporter.cs
Exports any `Node3D` subtree to glTF: mesh, live material state, no animation and no emitters.
`Export(node, path)` works on a throwaway `Utils/SceneCopy`, frees hidden `Node3D`s (damage is baked by
`Visible`) and the point-sprite `"lights"`, and converts every shader skin to a `StandardMaterial3D` over
reversed winding (`docs/formats/gotchas.md`: unreversed, this data exports inside out). File nodes and
materials take their gamez and texture names past the writer's renaming, so a model pack matches back.
`allStates` (the `--node=` one-shot) keeps hidden gamez nodes, written hidden, and shows the subject, so a
destructible's states reach the file. `.glb` by default. `ExportSet` writes several subtrees under one root at world transforms; `Export*ToExports` names a timestamped `Exports/` GLB, the writer `--export-gltf=`, F10 and NodeLab's export actions share.
A plane's mounted `cockpit1` (`PlaneBuilder`'s direct child) never rides in its file; given `cockpitPath`, it is written there all states, root at identity, in interior units (`CockpitPathBeside`; F10 suffixes `_cockpit1`). An untextured `lighting: false` skin (`DrawsUnlit`) is written `KHR_materials_unlit`.

## src/Tooling/SyntheticData.cs
`--synthetic-data`'s tree: an `extracted/` of invented records and code-generated files written into
`.scratch/synthetic-data/<pid>/`, which `Launcher._Ready` then reads as the data root. Engine-free,
so `CSVM.Tests/SyntheticDataTests.cs` builds the same tree. The records are `CSVM.Tests/fixtures/`
files read from the checkout, so an export has none and refuses the switch. A record family is one `SyntheticFamily` in `Families` (the Original shell's, which reads UI types, joins in `UI/Menu/Original/SyntheticShell.cs`'s `TreeFamilies`, the list `Build` is handed): its writer copies records with `SyntheticTree.CopyFixture` and
writes generated bytes with `WriteBytes`, under its own folder. The stamp carries `schema` and a
`synthetic` field, which `Marks` reads for the log and `test-report.json`'s `syntheticData`.
Read `SyntheticTextures.cs`, `SyntheticPlane.cs`, `SyntheticSounds.cs`, `SyntheticMission.cs` and `SyntheticEffects.cs` for the families, `Extraction/PngWriter.cs` and `WavWriter.cs` for the encoders.

## src/Tooling/SyntheticTextures.cs
The synthetic tree's C1 texture archive, `extracted/C1/texture/`: the hand-authored
`fixtures/synthetic/C1/texture/manifest.json` copied as it stands, and one checker PNG per
`texture_infos` entry at that entry's size, so a texture is added by adding a manifest entry. Every
name is `probe_*` except `smoke101`..`103`, the exhaust trail's pool, which `ExhaustSmoke` names in
code. The shape is what `Mech3/TextureArchive.cs` reads, from the header fields in
[../org/textures.md](../org/textures.md).

## src/Tooling/SyntheticPlane.cs
The synthetic tree's stand-in aircraft, every record invented: `probe_plane`, the default, and nine under
shipped airframe names the suites fly (`player_pfighter`, the lobby door's starter, the custom-plane
pair and six more), each carrying what the code keys on (`is_autogyro`, a turret mount). `plane` writes
`planes/` (`nodes.json`, `materials.json`, and a `models.json` generated from `boxes.json`, one
outward-wound box per entry) and `vehicle.json`, `engines.json`, `player.json` and `maneuvers.json`.
`armament` writes the weapons, the `ai.json` turret table and `shakes.json`; their names are in `SyntheticMessages.cs`. `Launcher`
sets the fits as `StockLoadouts.Supplement` and the probe as `SessionSpec.DefaultPlane` under the switch.
Shapes: [gamez](../formats/gamez.md), [markers](../formats/markers.md), [vehicle](../formats/vehicle.md), [weapons](../formats/weapons.md), [turrets](../formats/turrets.md).

## src/Tooling/SyntheticGameData.cs
The game data (`Spec/GameData.cs`) `BasePaths.Resolve` hands a `--synthetic-data` run: every model
built in memory from records written in code, with no game-file source, so nothing in the run can
reach an install. A format switched to `GameData` adds its builder here and drops its tree family
and fixture. Engine-free, so the units build the same models. Read `SyntheticMessages.cs`.

## src/Tooling/SyntheticMessages.cs
The synthetic run's message table: the `MSG_*` keys the stand-in records name and the ones the HUD,
the match lines and the dock prompt read directly, every value invented, built through
`Messages.Of`.

## src/Tooling/SyntheticImages.cs
The synthetic tree's JPEG and TGA writers, for menu art a layout or a script names by those
extensions, since a loader picks its decoder by the name: a baseline greyscale JPEG whose 8x8
blocks are flat, so each is one DC term, and an uncompressed top-down 24-bit TGA. Both draw a
checker shaded from the name. PNGs stay with `Extraction/PngWriter.cs`.

## src/Tooling/SyntheticSounds.cs
The synthetic tree's `sounds` family: `zrdr/sounds.json`, the fixture `SETS` and `SOUND_GROUPS`
copied as they stand, and `soundsh/`, the unpacked sibling of `soundsh.zip`, holding one generated
mono MS ADPCM WAV at 22050 Hz per entry of `fixtures/synthetic/soundsh/manifest.json` (a length and
a tone, or seeded noise). The build throws when the manifest and the `SETS` WAV names differ, so a
sound is added by a definition and a manifest entry together. Every definition is `snd_probe_*` except the music cues `MusicPlayer` looks up, the propeller start the spawn suite reads, and the voice sets, named `snd_id<N>_*` as `CombatVoice` builds them for the ids `PilotVoices` seats.
The `voice` family copies `zrdr/voice.json`, the accent table dealing those ids. Shapes:
[../formats/sounds.md](../formats/sounds.md), [../formats/combat-voice.md](../formats/combat-voice.md).

## src/Tooling/SyntheticMission.cs
The synthetic tree's `mission` family, `extracted/C1/PROBE1/zrdr/`: a `weather.json` of two zones
with different sun bearings under a cloud band inside the flight envelope, and a `net.json` of one
16-entry free-for-all block. Its `campaign` family is one story position: a `cm_sequence.json`
entry for seq 0 at `C1/M09`, the `Briefing.json` state that address keys, and its `objectives.json`.
Its `instant-action` family is an `ia.json` alone in `IA1/zrdr/` of the chapters the menu's presets
fly; C1 has none, since a suite reads `C1/IA1` as an install's spawn lists. Otherwise no shipped
mission address enters the tree, so a suite gated on one still skips. Shapes are in
[../formats/](../formats/README.md): `weather.md`, `net-spawns.md`, `campaign-sequence.md`.

## src/Tooling/SyntheticEffects.cs
The synthetic tree's `effects` family. Under `extracted/probe_effects/` it writes `gamez/`, one
legacy-shape node per entry of `fixtures/synthetic/probe_effects/templates.json` with a box per meshed
node, and `cam_anim/`, the compiled archive shape (`metadata.json` and one file per definition) split
out of `cam_anim.json`. It copies the reader destructibles, `flame_ball.json` and `pufftrails.json`
into `zrdr/`. No session loader reads `probe_effects/`; `Testing/EffectStageSuiteHelper.cs` does,
under the switch. Names the engine or a suite looks up are kept over invented content.

## src/Tooling/WavWriter.cs
Encodes mono 16-bit samples as a RIFF/WAVE file, plain PCM or MS ADPCM (the seven standard
coefficient pairs, 256-byte blocks, a `fact` chunk with the true length), the layout
`Mech3/WavFile.cs` decodes. Each ADPCM block is encoded under every coefficient pair and the closest
kept. Engine-free, so `CSVM.Tests/SyntheticSoundsTests.cs` round-trips it through the reader.
