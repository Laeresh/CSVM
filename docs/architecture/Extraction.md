# Extraction

The in-engine extraction: finding the player's Crimson Skies install, reading its UI archives in
place, running the bundled unzbd over its ZBD archives, and stamping the tree. No type here touches the
engine except `RememberedInstall` through `OptionsStore.UserOptions`, which is what lets a plain
unit test run every decoder, and lets the same code run on any platform the engine exports to.

One `## src/...` entry per module, body at most 8 lines.

Traps do not live here; the rule is in `docs/architecture.md`. The output layout is in
[../formats/extraction.md](../formats/extraction.md), the archive and texture formats in
[../formats/rof.md](../formats/rof.md), the string table in
[../formats/strings.md](../formats/strings.md).

## src/Extraction/InstallLocator.cs
Finds the install and every file in it without regard to case. `ResolveDirectory`/`ResolveFile`
walk a path one segment at a time and answer the disk's spelling, never renaming. `IsInstall` is
the silent rule: `ZBD` plus `GOSDATA/ASSETS`. `Check` judges a pick, naming the install when the
pick sits inside or holds it. A `ZBD` with root archives alone is refused (`NoChapters`). The disc,
its `GAME` folder or an `.iso` is a `Disc` (chapter `.cab`s), laid out by `unzbd cs disc` first.
`Candidates` lists the remembered path, then `BesidePlaces` (CSVM's folder or its `GAME`, and each
`.iso`, where a disc counts), then Program Files and fixed drives on Windows, or Wine and every
Proton prefix on Linux, folded through links. Read `RememberedInstall.cs` next.

## src/Extraction/RememberedInstall.cs
The game files last picked (a folder, drive or disc image), stored as `OptionsDef.InstallPath` in
the options file. `Get` and `Set` take an `OptionsStore` for tests, and default to
`OptionsStore.UserOptions()`, which needs the engine. A save loads first and changes only this
field; the value is fully qualified, and the store drops one that is not.

## src/Extraction/GameFileSource.cs
`IGameFileSource` is how a loader reads the original's files: `Exists` and `Open` (a seekable
stream, or null) take a game-relative path such as `ZBD/C1/zrdr.zbd`, matched without regard to
case, and `ReadAllBytes` throws naming `Location`. `InstallFolderSource` reads an install folder or
mounted drive through `InstallLocator.ResolveFile`. `GameFileSource.Open` alone turns a path into a
source: an `.iso` or a raw CD image (`RawCdImage.Descriptor`) is an `IsoImage` and a disc folder
(`Check` gives `Disc`) its `GAME` folder, each wrapped in a `CabinetSource`. `Locate` takes the
`CSVM_INSTALL_ROOT` override, then `<dataRoot>/CrimsonSkiesGame`, then the remembered place opened
as it is (so a remembered image counts), then `InstallLocator.Candidates`. Engine-free; `CSVM.Tests/GameFilesData.cs` locates the tests' install.

## src/Extraction/GameFileCopy.cs
`Run(source, dataRoot, replace, progress, cancel)` copies `Files` (`strings.dll`, `ZBD/` plus each
`ZbdManifest.Expected` archive, `RofFiles.GamePaths`) into `Target(dataRoot)`, the folder `Locate`
takes as an install. A disc's chapters come out of their cabinets. The tree is written under
`CrimsonSkiesGame.partial` and renamed in last; a failure or cancel deletes it. An existing copy
throws unless `replace`, which renames it to `.old`, swaps the new one in and deletes the old whole.
A file the source lacks is named in `Missing`. `RunToLog` is `--copy-game-files=`, never replacing.
Coverage: `CSVM.Tests/GameFileCopyTests.cs`.

## src/Extraction/IsoImage.cs
The disc's `.iso` read in place ([../formats/disc-image.md](../formats/disc-image.md)), rooted at
its `GAME` folder. `Read` walks the primary volume once into a path-to-extent table: 8.3 names
without the `;1` version, case-insensitive, `..` matching nothing. A malformed image throws
`InvalidDataException`, which `GameFileSource.Open` turns into null. `Open` returns a seekable
window over a fresh handle per call, so readers never share a position. The chapter `.CAB` files
are plain files here; the cabinet layer wraps this source. Coverage: `CSVM.Tests/IsoImageTests.cs`.

## src/Extraction/CabinetSource.cs
The cabinet layer over a disc's source ([../formats/cabinet.md](../formats/cabinet.md)): a path
below `ZBD/<chapter>/` is answered from `ZBD/<chapter>.CAB` when the inner source holds one, every
other path by `Inner`. The file table is read on the first lookup into a chapter, its folder
unpacked into memory on the first `Open`, and only the chapter opened last keeps them (`UnpackedBytes`). The 8.3
`RTEXTU~n` aliases get their `rtextureN` names, paired with `ZbdManifest`'s tiers sorted
ordinally. Coverage: `CSVM.Tests/CabinetTests.cs`, and `IsoImageDataTests` over a real disc.

## src/Extraction/Cabinet.cs
A Microsoft cabinet's header, folder and file tables (`Read`), and one folder's whole output
(`Unpack`). Stored and LZX folders unpack; MSZIP, Quantum, cabinet sets and continued files throw
`InvalidDataException`. Read `LzxDecoder.cs` next.

## src/Extraction/LzxDecoder.cs
The LZX decompressor, written from Microsoft's published specification: verbatim, aligned-offset
and uncompressed blocks, 32,768-byte frames, and the E8 call translation run over the finished
output. Internal; `Cabinet.Unpack` is its one caller. A malformed stream throws
`InvalidDataException`.

## src/Extraction/RawCdImage.cs
A raw CD image read as an `IsoImage` ([../formats/disc-image.md](../formats/disc-image.md#raw-cd-images)).
`Descriptor` answers the sheet a path stands for: a `.cue` or `.ccd`, or the same-named one beside
a `.bin`/`.img`. `Read` takes the first track from `CueSheet` or the CloneCD sheet, refusing audio
first, and hands `IsoImage` a sector stream that skips each 2,352-byte sector's header and error
correction (16 or 24 bytes in). Each opened file gets its own stream and handle. The image file
is found beside the sheet without regard to case (`Beside`). Coverage:
`CSVM.Tests/RawCdImageTests.cs`.

## src/Extraction/CueSheet.cs
The `.cue` sheet's data track for `RawCdImage`: `FILE`, `TRACK` and `INDEX` read in any case,
every other command skipped. The first track must be data in a `BINARY` file; it starts at its
`INDEX 01` and ends at the next track's first index in the same file. A per-track dump reads its
first file. A malformed line throws `InvalidDataException` naming the sheet and line.

## src/Extraction/ZbdArchive.cs
`Open(source, path)` or `Read(stream, name)` reads the table at the end of a `zrdr.zbd`,
`soundsh.zbd` or `soundsl.zbd` ([../formats/zbd-archive.md](../formats/zbd-archive.md)) into
`Entries`, in table order under their stored names, repeats kept. The format has no checksum, so a
wrong version or a table whose payloads do not tile from 0 throws `InvalidDataException`.
`ReadEntry` reads one payload under a lock, so a 128 MB sound archive is never held whole. The
archive owns its stream. `Rename(seen, name)` is today's extraction's collision rule, the one
place it lives: the sound, reader and texture readers key a repeated name through it. Coverage:
`CSVM.Tests/ZbdArchiveTests.cs`.

## src/Extraction/RofFiles.cs
The UI half read in place, under the paths `extracted/rof/` had: `crimson.rof`'s members, a PNG
pair per `.BM` (encoded on read), `crimptch.rof`'s under `_crimptch/`, the ten cinemas from the
install, and `ui_strings.json` and `menu_layout.json`, each made once on first read. `Open(game)`
reads and walks the archive on the first lookup (about 40-80 ms); `Exists`, `Read`, `Head` and
`Text` take a path in any case, and a file that does not read is logged and null. A launch `Bind`s
its view to its data root (`Launch/Launcher.cs`; the synthetic shell binds its own), which `For`
answers. `Of` and `Live` hold files in memory or follow a test's folder. `GamePaths` lists the
install files it reads, for `GameFileCopy`. Coverage: `CSVM.Tests/RofFilesTests.cs`.

## src/Extraction/RofTree.cs
The one case every `RofFiles` path is held in, upper, and the mapping a reader puts a game-data
name through (`Canonical`). The archive stores its members upper case; the layout, the scripts and
the install spell the same files in any case.

## src/Extraction/RofArchive.cs
`Walk` lists a `.rof` held in memory as `RofEntry` values, a directory before its contents, paths
joined with `/`, without inflating anything. `ReadMember` inflates one entry to its declared size
and throws when the payload falls short; `ReadHead` inflates no further than a prefix. An implausible node, a directory looping back to an ancestor or an
unknown entry kind throws `InvalidDataException` rather than reading garbage. Read `BmTexture.cs` next.

## src/Extraction/BmTexture.cs
`TryDecode` splits a `.BM` into its shading plane and its three paint-slot masks packed as RGB,
or answers null when the bytes are shorter than the planes the header declares. Rows stay in
stored order. `RofFiles` encodes the two as `<stem>.PNG` and `<stem>_MASK.PNG` through
`PngWriter`. The runtime paint code reads the `.BM` itself (`Mech3/PatternLibrary.cs`), so these
PNGs are for inspection.

## src/Extraction/PngWriter.cs
`EncodeRgb` and `EncodeRgba` (for `Tooling/DumpGameFiles.cs`'s textures) write 8-bit RGB or RGBA
as a PNG: signature, `IHDR`, one deflated `IDAT` of unfiltered rows,
`IEND`, each chunk with its CRC. `Crc32` is public so a test can check a chunk. It replaces
`System.Drawing`, which is Windows-only, and Godot's `Image`, which needs a running engine.
`Mech3/PngImage.cs` is the decoder the tests read its output back with.

## src/Extraction/PeStringTable.cs
`Read` walks a PE file's resource directory (type, block id, language) to each `STRINGTABLE`
block and returns every used slot by id, ascending. Pure byte parsing over the section table's
RVA map, so the original's DLLs read the same on every platform. A file that is not a PE throws.
`Section` returns one named section's bytes and its load address (image base included), which
`Mech3/Messages.cs` needs to follow the pointers in `strings.dll`'s key table.

## src/Extraction/UiStringTable.cs
`ParseSymbols` reads `RESOURCE.H`'s `#define IDS_...` lines (first definition of an id wins),
`Rows` turns one DLL's table into `UiStringRow` values with the symbol joined and a single-line
`[FONTID]` tag split into `Font`, and `ToJson` writes the array without a BOM. `TextById` is the
first-row-wins text map `MenuLayoutDecoder` joins widget strings against. The runtime reader is
`Mech3/UiStrings.cs`.

## src/Extraction/MenuLayoutDecoder.cs
`ReadTree` gathers the decode's inputs from a `RofFiles` view, and `Decode` and `ToJson` are the
pure steps a test drives on fixtures; `RofFiles` runs all three for its `menu_layout.json`. The format and every field order are in
[../formats/menu-layout.md](../formats/menu-layout.md); `UI/Menu/MenuLayout.cs` reads the output
at runtime.

## src/Extraction/ZbdExtraction.cs
`Run(installRoot, extractedDir, unzbd, options, progress, cancel)` walks `ZBD` (found through
`InstallLocator`) for `.zbd` in any case, runs `unzbd cs <mode> <in> <out>` on each archive in turn,
then `unzbd cs messages` on the install root's `strings.dll`. An archive fails on a non-zero exit
only, is counted, and the run goes on; a run with no failure stamps the tree. Output keeps the
install's relative path in `ZbdTree`'s case. Unzip (always for the root `rimage.zip`) deletes and re-expands a folder older than its
zip, writing entry by entry so the later of two entries differing only by case wins, as
`Expand-Archive` does. Blocks its thread; the caller runs it off the main thread.

## src/Extraction/ZbdPlan.cs
The rules `ZbdExtraction` applies, each testable alone. `ModeFor` maps a lower-cased base name:
`interp` to `interp` (`.json`); `planes`, `gamez` to `gamez`; `soundsh`, `soundsl` to `sounds`;
`zrdr` to `reader`; `rimage`, `texture`, `rtexture<n>` to `textures`; `cam_anim`, `mis_anim` to
`anim` (all `.zip`); anything else is an unknown type, skipped. `OutputRelativePath` writes the
path through `ZbdTree.Canonical`. An output as new as its source is up to date. `AlwaysUnzipped` names the root `rimage.zip`, whose PNGs the HUD reads loose. `Classify` counts stderr notes: "object3d transform fail" (matrix recomposed inexactly, the
stored one is kept) and "VAL FAIL" or "anim def duplicate anim ref" (anim fields kept as read); every
other line is a warning.

## src/Extraction/ZbdManifest.cs
`Expected` lists the 184 ZBD archives a complete install holds (the six root archives, then per
chapter its `cam_anim`, `gamez`, `texture`, `zrdr` and texture tiers, and per mission its
`mis_anim` and `zrdr`), the same on the English install and the German disc. `Missing(zbdRoot)`
looks each one up without regard to case and is checked before the ZBD half runs, since the walk
extracts whatever it finds and a missing archive would otherwise fail only when its chapter loads.

## src/Extraction/ZbdTree.cs
The one case the ZBD half writes under `extracted/`, the retail install's: folders upper case
(`C3/M01`), file names lower case (`zrdr.zip`). `Canonical` maps a relative path, `Under` a file
path under a data root and `Folder` a chapter or mission folder. `ZbdPlan.OutputRelativePath`
writes through it, and `SessionPaths`' chapter and mission paths, `AnimProgram.ArchivePaths` and
the probes' mission discovery read through it, so the campaign's `c3`/`m01` finds `C3/M01` on a
case-sensitive disk. Coverage: `CSVM.Tests/ZbdExtractionTests.cs` and `SessionPathsTests.cs`.

## src/Extraction/ZbdProgress.cs
The types around the runner. `ZbdExtractionOptions` (`Force`, `Unzip`); the player's button uses the
defaults. `ZbdProgress` is one report per step (index of total, output path, `ZbdStep`, mode, notes,
detail), and `Lines()` gives the console lines `--extract` prints. `ZbdExtractionResult` holds the
counts (extracted, up to date, skipped, unzipped, both note kinds), failures, unknown archives,
warnings, the stamp path, `Fatal` when nothing could start, and `Summary(unzip)`.

## src/Extraction/UnzbdTool.cs
unzbd stays a child process, never linked: it is EUPL-1.2 and the engine GPL-3. `FileName` and
`DefaultPath(executableFolder)` give `tools/unzbd.exe` on Windows and `tools/unzbd` elsewhere, the
release layout. `Run` passes arguments as a list and drains stdout and stderr concurrently, since anim
archives write thousands of lines; cancelling kills the process tree. `Identify` reads the
`--version` first line, the SHA-256, and the fork commit when the exe sits at
`<checkout>/target/release/` in a git checkout.

## src/Extraction/ExtractionStampWriter.cs
Writes `extracted/VERSION.json`: `schema` (always `ExtractionStamp.Schema`), `assets` (`script`,
`date` UTC, `unzbdVersion`, `unzbdSha256`, optional `unzbdCommit`) from `WriteAssets`. `WriteRof`
(`rof`: `script`, `date`, `movies`) has no caller since the UI half is read in place. `Merge` keeps every other field, reads an existing file
as text so a BOM from the retired PowerShell extractors parses, rewrites an unreadable one, and writes UTF-8
without a BOM. `script` is `CSVM`.

## src/Extraction/ExtractionRun.cs
The whole extraction as one call, shared by `--extract` and the extraction screen. `Run` checks the
install with `InstallLocator.Check` and refuses a non-install before writing; then the ZBD half.
The `.rof` half is read in place (`RofFiles.cs`) and never written. A ZBD failure (including a
missing unzbd) fails the run. `ExtractionProgress` carries a phase, a fraction (ZBD 0 to 0.85)
and console lines; cancel throws. `RunToConsole` prints header, lines and `Summary`, answering 0 or 1.
Blocks its thread. Read `ZbdExtraction.cs` next.

## src/Extraction/SessionPaths.cs
Static resolver for the extracted-data paths (`ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr`) under a data root, plus `PreferUnzipped` (an unpacked sibling dir beats its `.zip`)
and the `--zip-assets` switch that inverts it. A chapter or mission in either case maps through
`Extraction/ZbdTree.cs` to the case the extraction wrote. The `rtextureN` tier decode is on `docs/tooling.md`;
the `--gamez=`/`--textures=` override policy stays in `GameSession`, not here. `HasAnyChapterWorld`
asks the game-file source for any chapter's `gamez.zbd`, which picks the failed-build box's words.

## src/Extraction/PackIndex.cs
The enumeration both HD pack folders share: every zip and subfolder under the folder read in place,
in name order, each with its own top folder dropped and filtered to the caller's extensions. Files
within one pack also load in name order, and a later file wins its key. The caller's key function
names or refuses each file, and the index owns the open zips and the locked read. A zip entry named
with backslashes reads as one named with slashes, and a damaged entry or locked file reads as null
with a warning, never an exception. Engine-free; read
`HdTextures.cs` and `HdModels.cs` next. Coverage: `CSVM.Tests/PackIndexTests.cs`.

## src/Extraction/HdTextures.cs
The HD texture packs a player drops into `<dataRoot>/HD` (`textures.hd`, `--hd=`; a `--det` run reads none unless `--hd=` names one), over `PackIndex.cs`. A path left with a folder replaces
that texture of that archive (`ForWorld` by the `TextureSet.Archive` name, `ForRimage` for the menu art) or that UI archive file under `rof/` (`ForRof`, by its `RofFiles` path), matched without case or
extension, and a bare name replaces that world texture in every archive. `C3/rtexture12/cliff01.png` therefore beats `cliff01.png` in C3 alone,
the way a pack carries a chapter's own version of a name chapters share. A pack file carrying the
original's own extension beats one that does not (`PackIndex.ReadPath`), so a pack can ship both
`SB_03_01_BASE.png` (the scrapbook page) and `SB_03_01_BASE.jpg` (its zoom). Engine-free; `Mech3/HdArt.cs` decodes and sizes what it returns. Coverage: `CSVM.Tests/HdTexturesTests.cs`.

## src/Extraction/HdModels.cs
The HD model packs in `<dataRoot>/HDModels` (`models.hd`, `--hd-models=`, off with
`--no-hd-models`; a `--det` run reads none unless the flag names one), over `PackIndex.cs`. It maps a
gamez node name to a `.glb` from a zip or subfolder, or a loose `.gltf` with its path, by airframe
(`planes/<airframe>/<node>`), chapter (`C3/<node>`) or bare name, the more specific winning; a gamez ancestor's file comes first (`Mech3/SceneBuilder.cs`). Names
match without case or a trailing `.flt`. `KeyFor` names the pack key a node resolves to without
reading it, which `Mech3/HdModelMeshes.cs` caches on. An airframe is its model node
(`player_bhawk`), and `RigFor` returns `planes/<airframe>/rig.json`, the one `.json` read, for
`Mech3/PlaneRigPatch.cs`. Engine-free, apart from the texture folder. Coverage: `CSVM.Tests/HdModelsTests.cs`.

## src/Extraction/ExtractionStamp.cs
Reads the provenance stamp the extraction scripts leave at `extracted/VERSION.json` (unzbd version
line, exe hash, fork commit, schema integer) and compares its schema against this class's own
`Schema` const, in `Launcher._Ready` right after the data root settles, with at most one warning line
per boot naming the fix. An unstamped tree only warns, since a dev tree holds valid extractions older
than the stamp. `Standing` (unstamped, current, older, newer) no longer stops a launch: the game
files are read in place, and a stale tree is only the upgrade question's to offer; `Behind` is the read for a
caller that blocks, true only for a stamped schema under the one asked for. `Schema` also lives in both
extraction scripts, and `CSVM.Tests/ExtractionStampTests.cs` refuses a bump that moves fewer than all three.
