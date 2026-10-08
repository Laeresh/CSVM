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
`--menu --screenshot` captures the launchscreen with no session alive; camera/orbit/rigs/clock are
parameters. That capture reads synchronously, since the process exits on its file. `SaveScreenshot`,
the F12 save every screen shares, writes `PaneReadback`'s frame into `ShotDir()` (`ShotDirFor`: the
repo's `Screenshots/`, or one beside an export's executable) on the worker it lands on. F11 reads each
pane's own camera (chase, cockpit, photo mode, free camera) through the pure `PlacementLines`: a
`--freecam` line, `--fov=` off the external base, and each flight aircraft's `--fly` line.

## src/Tooling/GltfExporter.cs
Exports any `Node3D` subtree to glTF: mesh, live material state, no animation and no emitters.
`Export(node, path)` works on a throwaway `node.Duplicate()`, frees hidden `Node3D`s (the panel/flare `Visible` toggles
are how damage is baked) and the point-sprite `"lights"` instances, and converts every shader skin to a
`StandardMaterial3D` over geometry with its winding reversed (`docs/formats/gotchas.md`: unreversed,
this data exports inside out). Format is extension-driven (`.glb` default). `ExportSet` writes several
subtrees under one root at their world transforms, dropping any an ancestor in the list carries.
`ExportToExports`/`ExportSetToExports` name a timestamped `Exports/` GLB. The viewer's `--export-gltf=`
one-shot and F10, and NodeLab's selection and export set actions, share that writer.

## src/Tooling/SyntheticData.cs
`--synthetic-data`'s tree: an `extracted/` of invented records and code-generated files written into
`.scratch/synthetic-data/<pid>/`, which `Launcher._Ready` then reads as the data root. Engine-free,
so `CSVM.Tests/SyntheticDataTests.cs` builds the same tree. The records are `CSVM.Tests/fixtures/`
files read from the checkout, so an export has none and refuses the switch. A record family is one `SyntheticFamily` in `Families` (the Original shell's, which reads UI types, joins in `UI/Menu/Original/SyntheticShell.cs`'s `TreeFamilies`, the list `Build` is handed): its writer copies records with `SyntheticTree.CopyFixture` and
writes generated bytes with `WriteBytes`, under its own folder. The stamp carries `schema` and a
`synthetic` field, which `Marks` reads for the log and `test-report.json`'s `syntheticData`.
Read `SyntheticTextures.cs`, `SyntheticPlane.cs`, `SyntheticSounds.cs` and `SyntheticMission.cs` for the families, `Extraction/PngWriter.cs` and `WavWriter.cs` for the encoders.

## src/Tooling/SyntheticTextures.cs
The synthetic tree's C1 texture archive, `extracted/C1/texture/`: the hand-authored
`fixtures/synthetic/C1/texture/manifest.json` copied as it stands, and one checker PNG per
`texture_infos` entry at that entry's size, so a texture is added by adding a manifest entry. Every
name is `probe_*` except `smoke101`..`103`, the exhaust trail's pool, which `ExhaustSmoke` names in
code. The shape is what `Mech3/TextureArchive.cs` reads, from the header fields in
[../org/textures.md](../org/textures.md).

## src/Tooling/SyntheticPlane.cs
The synthetic tree's stand-in aircraft: `probe_plane`, the default, and three under the code's names,
every record invented: `player_pfighter` (the lobby door's starter), `player_fbrand` and `player_avenger`
(the custom-plane suites' airframes). `plane` writes `planes/` (`nodes.json`, `materials.json`, and a
`models.json` generated from `boxes.json`, one outward-wound box per entry) and `vehicle.json`,
`engines.json`, `player.json` and `maneuvers.json` under `zrdr/`. `armament` writes the invented weapons
(the ids a custom build composes among them), `shakes.json` and `messages.json`. `Launcher` sets the
fits as `StockLoadouts.Supplement` and the probe as `SessionSpec.DefaultPlane` under the switch. Shapes:
[gamez](../formats/gamez.md), [markers](../formats/markers.md), [vehicle](../formats/vehicle.md), [weapons](../formats/weapons.md).

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
sound is added by a definition and a manifest entry together. Every definition is `snd_probe_*` except the music cues `MusicPlayer` looks up and the voice sets, named `snd_id<N>_*` as `CombatVoice` builds them for the ids `PilotVoices` seats.
The `voice` family copies `zrdr/voice.json`, the accent table dealing those ids. Shapes:
[../formats/sounds.md](../formats/sounds.md), [../formats/combat-voice.md](../formats/combat-voice.md).

## src/Tooling/SyntheticMission.cs
The synthetic tree's `mission` family, one invented mission scope `extracted/C1/PROBE1/zrdr/`: a
`weather.json` whose two zones author different sun bearings under a cloud band inside the flight
envelope, and a `net.json` of one 16-entry free-for-all block. The weather, sun and net-table suites
read it where an install reads a shipped mission. No shipped mission name enters the tree, so a suite
gated on one still skips. Shapes: [../formats/weather.md](../formats/weather.md),
[../formats/net-spawns.md](../formats/net-spawns.md).

## src/Tooling/WavWriter.cs
Encodes mono 16-bit samples as a RIFF/WAVE file, plain PCM or MS ADPCM (the seven standard
coefficient pairs, 256-byte blocks, a `fact` chunk with the true length), the layout
`Mech3/WavFile.cs` decodes. Each ADPCM block is encoded under every coefficient pair and the closest
kept. Engine-free, so `CSVM.Tests/SyntheticSoundsTests.cs` round-trips it through the reader.
