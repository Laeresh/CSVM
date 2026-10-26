# C# game-file readers, replacing unzbd

**ACTIVE PLAN** (written 2026-10-25). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The engine reads the original game's files itself, in C#, from an install folder, a mounted drive
or an `.iso`, with no extraction step and no `unzbd` child process. The mech3ax fork that `unzbd`
is built from has been made private at its original developer's request, and upstream mech3ax now
holds only a README, so a CSVM release can no longer ship a binary whose source is available. This
plan replaces every `unzbd` mode and the C# ROF half with readers that build the loaders' existing
models straight from the game bytes, then deletes the extraction pipeline, the `extracted/` folder
and the Rust toolchain. An optional copy writes the needed game files (CABs unpacked) into the data
folder as an install tree, so a disc or ISO need not stay mounted.

Out of scope: byte-identical reproduction of `unzbd`'s JSON/zip output, repacking (`rezbd`), and
any change to what the engine does with the data. The plan draws no `backlog` issues. No release
ships until this plan closes; v0.4.0 stays published with its bundled `unzbd.exe`, which the
original developer accepted.

## Milestone goal

- The game runs from an install folder, a mounted drive or an `.iso` file, read in place.
- An optional copy lays out the files the engine reads as an install tree under the data root,
  found afterwards by the same locator.
- Every loader builds its model from the game bytes behind one game-file source; the JSON/zip
  loaders, the v0.6.1 shape support and `extracted/` are gone.
- A dev-only headless `--dump` writes CSVM-shaped JSON/PNG/WAV into `.scratch` for analysis
  scripts and agents.
- An upgrading player is sent to a source picker when no source is found, and is offered once to
  delete the old `extracted/`.
- Releases bundle no `unzbd`, no `LICENSE-unzbd`, and no Rust build; mech3ax is credited by one
  linked line.
- The complete `RunTests.ps1` passes with every golden unchanged, finally from the ISO alone.

**Same game, not the same files.** Equivalence is judged on the loader models and the goldens,
never on the bytes `unzbd` wrote. Reproducing serde's JSON text, zip layout or the repack pointer
tables is work this plan does not do.

## Decisions (2026-10-25)

| # | Question | Decision |
|---|---|---|
| 1 | What "functionally identical" means | **Same in-memory data, same goldens.** Extraction output may differ from `unzbd`'s bytes; today's output is an oracle compared by parsed value. |
| 2 | What the optional extract produces | **A raw copy of game files**, not decoded files. One load path, wherever the files live. |
| 3 | Provenance of the C# code | **Mixed.** Written from `docs/formats/` and the existing C# loaders, checked against today's output; agents may read the local Rust (`tools/mech3ax`) to settle edge cases. No line-by-line translation. |
| 4 | An ISO source | **Read in place.** Own ISO9660 reader and LZX CAB decoder; the copy is a convenience. |
| 5 | Where the readers hand off | **Straight into the loader models**, behind one game-file source. Each format's JSON/zip path is deleted once its model diff passes. |
| 6 | The ROF half | **Read directly too.** `extracted/rof/` goes, `extracted/` disappears entirely. |
| 7 | Data for analysis scripts and agents | **A dev-only headless `--dump`** into `.scratch`, CSVM's own shape, never read by the engine. Old scripts move to it as they are next touched; no sweep. |
| 8 | First launch of an upgraded install | **Find the source, offer cleanup.** Remembered install or locator scan, else a source picker replacing the Extract screen; once a source works, offer once to delete `extracted/`. The old folder is never read. |
| 9 | Copy scope | **The files the engine reads**, CABs unpacked, as an install tree under the data root. |
| 10 | Proof a format is done | **Model-vs-model diff over all real archives** (floats bitwise), then the complete battery with goldens unchanged; the final run from the ISO alone. |
| 11 | Releases until the port lands | **None.** v0.4.0 stays up; the port release bundles no `unzbd`. |
| 12 | Credit | **One line naming TerranMechworks/mech3ax, with the upstream link**, in README and the third-party notices. Every link to the fork goes. Upstream holds only a README, so `docs/formats/` citations into mech3ax code are rewritten to cite the C# readers. |
| 13 | Order | Small formats first (interp, messages, sounds, reader, textures), then anim, then gamez/planes, then ROF and ISO/CAB, then the cutover. |
| 14 | Per-chapter image-pointer tables (~4,000 lines in `gamez/src/gamez/cs/data`) | **Not ported**, on the reading that they serve only the byte-identical repack. The gamez model diff tests that reading. |
| 15 | CAB decompression from an ISO | **In memory, per session.** A disk cache only if a measured launch cost is too high. |
| 16 | The local `tools/mech3ax` checkout | **Stays local and git-ignored** as the read-only reference for decision 3. Nothing from it is pushed or mirrored. |
| 17 | What feeds the CI engine suites, which run on `--synthetic-data` with no game files | **The synthetic tool builds the loader models in memory** and hands them to the session through a game-data seam (A3); no synthetic files, JSON or binary. A synthetic run has no game-file source. |

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | none yet | The pipeline facts in the data survey are traced; no port approach is. |
| **Direction sound, magnitude a judgement call** | A1, A2 | The seam and oracle shape follow from the decisions; the interface details are yours. |
| **Leads only, no mechanism yet** | every B, C, D, E item | Each rests on discussion and a code survey. Budget for the format's surprises; the model diff is the instrument that settles them. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

**⚠ The local `tools/mech3ax` checkout is the only remaining copy of the Rust.** Never delete,
clean or re-clone it while this plan is live, and never push it anywhere.

## What the data actually ships

The current pipeline, from a code survey in the writing session. File references are
`CSVM/src/Extraction/` unless stated.

**Sources.** `InstallLocator.Check` (`InstallLocator.cs:185`) treats a `.iso`, or a folder whose
`ZBD` or `GAME/ZBD` holds `.CAB` files, as a disc; an install is a folder holding `ZBD` plus
`GOSDATA/ASSETS`, case-insensitive. `Candidates` scans the remembered path
(`RememberedInstall.cs`), Program Files and fixed drives on Windows, and Wine/Proton prefixes on
Linux. A disc goes through `unzbd cs disc` (`ExtractionRun.LayOutDisc`), which walks ISO9660 8.3
names (`tools/mech3ax/crates/unzbd/src/disc.rs:87-155`), unpacks the LZX chapter CABs through the
`cab` crate, and restores the long `rtextureN` names from a per-chapter table (`disc.rs:177-203`).

**`unzbd` modes** (`ZbdPlan.cs:30-45`, archive list in `ZbdManifest.cs`, 184 archives):

| Mode | Inputs | Output | Count |
|---|---|---|---|
| `interp` | `interp.zbd` | `.json` | 1 |
| `gamez` | `gamez.zbd` per chapter, root `planes.zbd` | `.zip` | 8 + 1 |
| `sounds` | `soundsh.zbd`, `soundsl.zbd` | `.zip` | 2 |
| `reader` | `zrdr.zbd`: root, chapter, mission | `.zip` | about 54 |
| `textures` | `texture.zbd`, `rtexture{2,4,6,8,top}.zbd` per chapter, root `rimage.zbd` | `.zip` of PNGs + `manifest.json` | 8 + 40 + 1 |
| `anim` | `cam_anim.zbd` per chapter, `mis_anim.zbd` per mission | `.zip` | 61 |
| `messages` | `strings.dll` | `messages.json` | 1 |

**C# ROF half** (into `extracted/rof/`): `RofArchive.cs`, `RofExtraction.cs` (`crimson.rof`,
`crimptch.rof`), `BmTexture.cs` + `PngWriter.cs` (.BM to PNG and `_MASK.PNG`), `PeStringTable.cs` +
`UiStringTable.cs` (`langui.dll`, `language.dll`), `MenuLayoutDecoder.cs`, `MovieCopy.cs` (10
`.mpg`). Cinemas play through CSVM's own decoder (`CSVM/src/Video/MpegMovie.cs`,
`MpegSystemStream.cs`).

**Consumers** (`CSVM/src/Mech3/`): no single seam. `GameZ.cs:108-123`, `CompiledAnim.cs:81,120,147-151`,
`TextureArchive.cs:759-766` and `:904-910` (interp), `SoundArchive.cs:39,126`, `Zrdr.cs:20-179`,
`Messages.cs:27` and `UiStrings.cs:49` each open their own zip or folder and parse with
`JsonDocument` into their own models (`GameZNode`, `AnimDefinition`, `ZrdrDict`).
`SessionArchives.OpenFor` opens a chapter's gamez, textures, sounds and zrdr together.
`SessionPaths.PreferUnzipped` picks a sibling folder over a zip. `GameZ`, `TextureArchive` and
`Zrdr` accept both the v0.6.1 and the fork's unified shape (`docs/formats/extraction.md`).
Textures are PNG-decoded at load (`PngImage.cs`); the zip's `manifest.json` carries each texture's
alpha class (`TextureArchive.cs:1306-1315`).

**Rust sizes** (`tools/mech3ax/crates`, `.rs` lines including inline tests): `interp` 221,
`messages` 1,244, `reader` 176, `archive` 458, `image` 776 + `pixel-ops` 524, `gamez` 20,228 (CS
part 5,127, mostly the pointer tables), `nodes` 7,626, `anim` 8,369 (CS part 2,111), `anim-events`
11,598, `anim-names` 3,897; shared `api-types` 5,178, `common` 1,644, `types` 2,983. The fork's diff
against upstream is 125 files, +10,842 / -150.

**Codecs.** The named-entry `.zbd` archive is uncompressed and carries no checksum in the shipped
version 1, and only `zrdr.zbd`, `soundsh.zbd` and `soundsl.zbd` (64 files) use it; every other
`.zbd` is a format of its own (A1, `docs/formats/zbd-archive.md`). Textures use per-texture and global palettes, RGB565 to 888 and a simple
alpha; no RLE or LZ. Only the disc CABs are compressed (LZX), with no native .NET decoder. `reader`
floats are `f32` widened to `f64` for JSON (`reader/src/read.rs`); `Zrdr.cs` reads them back as
single precision. Six NaN anim deltas are carried raw (`delta_raw`). Texture name collisions are
resolved by a `Rename` rule, recorded as `rename` in the manifest.

**Today's oracles.** `ExtractedGoldenTests.cs` pins counts and structure over a real `extracted/`
(skips when absent). The CI engine job runs headless with no extraction and no fork access
(`.github/workflows/checks.yml:2-4`). Freshness is timestamp-only (`ZbdPlan.UpToDate`), with a
hand-bumped schema in `extracted/VERSION.json` (`ExtractionStamp.Schema = 3`).

**Local scale.** `extracted/` is 1,640 MB in 61,021 files (183 zips, 459 MB, plus unzipped
folders and `rof/`). A full extraction takes about half a minute
(`docs/release-notes-v0.1.0.md:28`).

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, foundation

1. ☑ Game-file source and the ZBD archive reader
2. ☑ Model-diff oracle harness
3. ☑ Session game-data seam and in-memory synthetic models

### Wave B, small formats

11. ☑ `interp.zbd`
12. ☑ `strings.dll` messages
13. ◐ Sound archives
14. ◐ Reader archives (`zrdr.zbd`)
15. ☑ Texture archives

### Wave C, large formats

21. ◐ Anim archives (`cam_anim.zbd`, `mis_anim.zbd`)
22. ◐ GameZ and `planes.zbd`

### Wave D, ROF and disc sources

31. ☐ ROF half read in place
32. ☑ ISO9660 reader (the original disc's image is still to be read: `CSVM_TEST_ISO`)
33. ☑ LZX CAB decoder and the `rtextureN` long names
34. ☐ Optional copy into the data root
35. ☐ Raw CD images: `.cue`/`.bin`/`.img` and CloneCD `.img`/`.ccd`

### Wave E, cutover

41. ☐ Source picker and upgrade cleanup
42. ☑ Dev-only `--dump`
43. ☐ Delete the extraction pipeline
44. ☐ Packaging, licences, credit and docs
45. ☐ Final ISO-only run

## Dependency and parallelism notes

A1 and A2 block everything. A3 blocks every engine switch from B12 on; a format item's reader and
its model diff can run beside A3, and its switch (game-data slot, synthetic builder) follows A3.
The Wave B items are independent of each other once A lands, but each edits
`SessionArchives.cs`/`SessionPaths.cs` when it switches its loader, so run at most two in
parallel and give each its own loader file plus a serialized merge of the shared two. B15 before C22
(gamez references textures by name). C21 and C22 are independent but both large; run them in
parallel only with separate file ownership (`CompiledAnim.cs` + anim readers vs `GameZ.cs` + node
readers). D31 needs only A1. D32 → D33 is a chain; D34 needs D33. Wave E needs every B, C and D
item; E41 and E42 can run in parallel; E43 after E41 and E42; E44 after E43; E45 last.

---

# Wave A, foundation

## A1 ☑ Game-file source and the ZBD archive reader

**Landed.** `CSVM/src/Extraction/GameFileSource.cs` holds `IGameFileSource` (`Location`,
`Exists(path)`, `Open(path)` returning a seekable stream or null), the `ReadAllBytes` extension,
`InstallFolderSource` over `InstallLocator.ResolveFile`, and `GameFileSource.Open(root)` /
`Locate(installOverride, dataRoot, remembered, roots)`. Paths are game-relative
(`ZBD/C1/zrdr.zbd`, `GOSDATA/ASSETS/crimson.rof`), split on either slash and matched without
regard to case, so readers need no `ZbdTree` case mapping. `Locate` tries `CSVM_INSTALL_ROOT`, then
`<dataRoot>/CrimsonSkiesGame`, then the remembered install, then `InstallLocator.Candidates`.
`GameFileSource.Open` is the one factory, which D32 and D33 extend for an `.iso` and a cabinet
disc. `CSVM/src/Extraction/ZbdArchive.cs` reads the named-entry container
([formats/zbd-archive.md](formats/zbd-archive.md)): the table only on open, each payload on demand
under a lock, repeats kept in table order. The namespace is `Extraction`, beside `InstallLocator`,
because after the cutover that namespace is exactly the layer reading the original's files.
`CSVM.Tests/GameFilesData.cs` gives `[GameFilesFact]`/`[GameFilesTheory]` (with `AlsoExtracted`
for a comparison against `extracted/`), located through `Locate` with no remembered install.
`SessionPaths` keeps its role only for the `extracted/` paths each Wave B item still reads; a
switched loader takes the source and a game-relative path instead, and E43 deletes the rest.

**Two premise corrections.** The Crimson Skies archive (version 1) has **no CRC**; the checksum
lives only in a version-2 header no shipped file uses, so the reader checks that payloads tile
from offset 0 to the table instead. And only **64 of the 184** `ZbdManifest` files are this
container (every `zrdr.zbd`, `soundsh.zbd`, `soundsl.zbd`); `interp`, `gamez`, `planes`, the
textures and the anim archives are formats of their own, so B11, B15, C21 and C22 get no help from
`ZbdArchive`. **Names repeat**: 44 repeats in each sound archive plus one pair differing only by
case, and `player.zrd` twice in the root `zrdr.zbd`; today's zips hold a repeat as `stem-N.ext`, a
rule B13 and B14 must reproduce wherever a loader keys by name.

**Verified.** complete `RunTests.ps1` on the merged plan tree at `485f7fcf6` (battery 2), PASS: units 7,612 passed, 0 failed, 3 skipped; engine 614 passed, 2 skipped, engine errors clean; 38 goldens hash-identical. The data theories ran with the install found: all 184 manifest files open, all 64 container archives tile and match today's zips by entry name.

**Original approach (kept for reference).**

**Goal.** One interface hands any loader a readable stream for a game path (case-insensitive),
whether the game lives in an install folder, on a mounted drive or (after D32) in an ISO; the outer
`.zbd` archive format is readable through it.

**Evidence (confidence: direction-sound).** Loaders open paths themselves today (data survey,
Consumers). The outer archive is uncompressed with CRC32-checked entries
(`tools/mech3ax/crates/archive/src/read.rs`). `InstallLocator` already finds installs and discs.

**Approach.** A game-file source abstraction beside `InstallLocator`, fed by the located install;
an archive reader returning named entries as spans/streams. Reuse `RofArchive.cs`'s shape for
naming and error handling. The interface's members and `SessionPaths`' role are in **Landed**.

**Model recommendation.** high, the seam every later item builds on.

**Verify.** Unit tests on a hand-made archive fixture (CI-safe); every archive in `ZbdManifest.cs`
opens on a real install, and every archive of the named-entry layout lists entries whose names and
sound payload CRC32s match today's zips. The attribute that skips without an install is
`[GameFilesFact]`/`[GameFilesTheory]`.

**⚠ Traps.** Install folders are matched case-insensitively and output paths take case from
`ZbdTree`; Linux installs under Wine/Proton are case-sensitive file systems.

## A2 ☑ Model-diff oracle harness

**Landed.** `CSVM.Tests/ModelDiff.cs` is one reflective comparer for every loader model:
`ModelDiff.AssertEqual(expected, actual, params string[] skip)` fails with the first difference
as `path: expected …, actual …` and otherwise returns the number of leaf values compared;
`ModelDiff.FirstDifference` returns the same text, or null. `CSVM.Tests/ModelDiffTests.cs` seeds
one difference per test into real model types (`GameZ`, `AnimDefinition`, `SiScript`, `TgaImage`)
and pins the reported path; `ModelDiffDataTests` builds five real models from `extracted/` twice
and finds no difference, then flips one bit in C1's world and finds exactly that path.

**Verified.** complete `RunTests.ps1` on the merged plan tree at `485f7fcf6` (battery 2), PASS: units 7,612 passed, 0 failed, 3 skipped; engine 614 passed, 2 skipped, engine errors clean; 38 goldens hash-identical. The harness reports a planted single-field difference at its path in every unit test, and each format's model diff in Waves B and C runs through it.

**The rules.**

- **Reflective, not per-model.** The walk reads every instance field, public or private, down the
  base-type chain, and shows an auto-property's backing field under the property's name. That
  covers the state the models keep behind accessors (`AnimData._props`, `ZrdrDict._props`,
  `Messages._byKey`), records (`AnimNodePrereq.Path`), and Godot structs (`Vector3`, `Color`,
  `Quaternion`, `Transform3D`) through their own fields. Computed properties are not read; they
  derive from fields. A per-model comparer would have to be kept in step with every field a later
  item adds, and a forgotten field passes silently.
- **Leaves.** `float` and `double` compare by their bits: a NaN equals only the same NaN payload,
  and `-0` differs from `0`. Other primitives, enums, strings (ordinal) and `decimal` compare by
  `Equals`. A boxed value of a different type is a difference (`1f` against `1.0` in a reader
  list), reported with both type names.
- **Collections.** Lists and arrays: count first (`….Count`), then element by element in order.
  Dictionaries and sets: their `Comparer` first, then each key must exist with exactly the same
  spelling (even under `OrdinalIgnoreCase`, since `ZrdrDict.Keys` and the anim symbol tables expose
  the spelling), then the values; key order is not compared. Any other `IEnumerable` compares in
  order. `byte[]` takes a span fast path that names the first differing index. `Memory<T>` and
  `ReadOnlyMemory<T>` compare by content.
- **Cycles and shared nodes.** A reference pair met again counts as already compared, so
  `AnimDefinition.Archive` pointing back at its `AnimArchive` terminates.
- **Godot objects.** An `Image` compares by format, size, mipmap count and `GetData()`, which
  needs a running engine, so that branch serves an in-engine suite only. Any other `GodotObject`
  (`ImageTexture`, `AudioStreamWav`) throws: compare the engine-free bytes it is built from
  (`TgaImage.Rgba`, the WAV bytes). `TextureArchive` and `SoundArchive` hold zip handles and
  engine caches, so B13 and B15 compare per entry, not the archive object.
- **Skips.** `GameZ._parent`, `_placed` and `_markerGizmo` are lazy caches and always skipped. A
  caller adds a skip as `Type.member` (`"AnimArchive._path"`) or as one exact path
  (`"SiScript.Frames[3].Translate.Delta.X"`). A lazily loaded part must be loaded on both sides
  first: the `AnimArchive` theory calls `Script(i)` for every index before comparing.

**NaN and `delta_raw`.** The extraction writes the six NaN deltas of `carneypkup_cam.zan;camera1`
(C5/M02) as `delta: {x: 0.0, …}` beside `delta_raw: "AADA/…"`, and `SiVectorChannel.Parse` reads
only `delta`, so today's model holds `+0.0` (`0x00000000`) where the file holds `0xFFC00000`. The
same frame's spline blocks reach the model as base64, so they keep their NaN bits. The harness
makes no exception for either: the C5/M02 archive self-compares clean with its NaN splines, and a
reader that yields the file's NaN deltas is reported at
`…Frames[k].Translate.Delta.X: expected 0 (0x00000000), actual NaN (0xFFC00000)`. C21 settles it
one of two ways: read those deltas the way today's model holds them (decision 1, same in-memory
data), or keep the file's NaN and name the six paths as skips with the reason beside them. The
harness has no NaN tolerance switch, so neither choice can hide a seventh NaN.

**Wiring contract (Wave B, C).** One theory per format, rows from `ZbdManifest`, the old model
from the extraction and the new one from A1's source. `GameDataTheory`, `GameFiles.Open`,
`OldLoader` and `NewReader` stand in for A1's names and the item's own:

```csharp
[GameDataTheory] // A1's install-skipping attribute
[MemberData(nameof(InterpArchives))]
public void TheReaderBuildsTheModelTheExtractionBuilds(string archive)
{
    var expected = OldLoader(SessionPaths.PreferUnzipped(...archive...));
    var actual = NewReader(GameFiles.Open(archive));
    Assert.True(ModelDiff.AssertEqual(expected, actual) > 0);
}
```

A format whose loader is a bag of files (zrdr, sounds) compares a `Dictionary<string, …>` keyed by
entry name, so a missing or extra entry is reported by name (`Dictionary["vehicle.zrd.json"]`).
Assert the returned count is above zero, so a model whose every field was skipped cannot pass.

**Measured.** 1,327,100 leaf values for C1's world in about one second; C1 `cam_anim` 236,657;
C5/M02 `mis_anim` 239,193; shared zrdr 304,837; messages 1,024.

**Original approach (kept for reference).**

**Goal.** For any format, a test builds the loader model from today's `extracted/` and from the
game bytes and reports the first differing field, floats compared bitwise.

**Evidence (confidence: direction-sound).** Decision 10. `ExtractedGoldenTests.cs` already skips
cleanly without data.

**Approach.** A reflective or hand-written deep comparer over the loader models, run as a theory
over every archive of a mode. Reflective, as above; NaN and `delta_raw` as above.

**Model recommendation.** high, every format's proof depends on its strictness.

**Verify.** Seed a deliberate one-field difference and see the harness report it with its path.

**⚠ Traps.** An unchanged result is not evidence until the harness has been seen to fail.

## A3 ☑ Session game-data seam and in-memory synthetic models

**Landed.** `CSVM/src/Spec/GameData.cs` is the one game-data value: `Interp` and `Messages` (the
chapter-independent part) and `LoadChapter(chapter, mission)`, which builds the per-chapter part,
`ChapterData`; it names its chapter and mission and holds no model yet. `BasePaths.Resolve` loads
it once per launch into `BasePaths.Data`: `GameData.Load(game)` from the install, or
`Tooling/SyntheticGameData.Build()` under `--synthetic-data`, where `Game` stays null. Everything
that holds a `BasePaths` reaches it: sessions through `LauncherContext.Paths`, `GameSession` into
`BuildState.Data` (B11's `BuildState.Interp` folded in) and `BuildState.Chapter`, the probes
through `ProbeRunner`'s paths, the suites through the new required `TestContext.Data`
(`RequireInterp`, and `RequireMessages`, which SKIPs on an empty table), `LaunchAudio` for the
boot card, and the Original menu through `OriginalPresentation`'s `data` argument, which the
shell exposes as `IOriginalScreenHost.Data`, and `CampaignFeature.Data` (set where `ProcessMenu`
builds it; `CampaignFlow`'s store-first constructor takes `data:`). Messages is fully switched:
every engine signature that took a messages path takes a `Messages` (`LoadScreens`, `PauseSheet`,
`LoadBoard`, `SeatFields`, `NetFlight`, `Probes`, `SessionBoards.Inputs`,
`InstantActionDirector.ActorBuildInputs`), the four data-root sites read the game data
(`BootSequence.Card(Messages)`, `CampaignBriefing.Load(root, messages, seq)`,
`CampaignFlightCheckPage` through `Flow.Feature.Data`, and the bot rows through
`BotSeats.CallsignPool(host.Data.Messages)`, `LoadCallsignPool` deleted), and a session shares one
table where each consumer used to parse its own. The JSON read moved to
`CSVM.Tests/Oracle/MessagesLegacy.cs` (`Load`, `Parse`, and `Data(dataRoot)` for units that hand a
screen an extracted data root); the B12 model diff's expected side and the units that state a table
as JSON read through it. `Messages.Of(pairs)` builds a table in code.
`Tooling/SyntheticMessages.cs` holds the invented table, and the tree no longer writes
`messages.json` (`fixtures/synthetic/messages.json` deleted). Unused now, for E43:
`BasePaths.MessagesPath`, `TestContext.MessagesPath` (still filled into the five suites' own
`BasePaths`), `--messages=` (`SessionSpec.Messages`) and the `messages` step of `ZbdExtraction`.

**Synthetic fixtures become code.** Every synthetic model is built by code in `Tooling/`, never
read from a JSON fixture, because the synthetic run is an engine run: a legacy JSON parser lives in
the test project only, so the engine has no reader for a fixture once its format switches, and
Decision 17 rules out synthetic files. The hand-authored records under `CSVM.Tests/fixtures/synthetic/`
stay JSON only until their own format switches. The unit fixtures (`fixtures/*.json` outside
`synthetic/`) stay JSON, read by the oracle. The recipe, with messages as the worked example:

1. **Slot.** A chapter-independent model is an init property on `GameData` (`Messages`), a
   per-chapter one on `ChapterData`.
2. **Real loader.** The reader's `Load(IGameFileSource?)`, called in `GameData.Load` (or in the
   `LoadChapter` delegate `GameData.Load` sets), null or empty without a source.
3. **Synthetic builder.** `Tooling/Synthetic<Format>.cs` returns the model built in code (records as
   C# literals or generated, `SyntheticMessages.Table()` over `Messages.Of`), assigned in
   `SyntheticGameData.Build` (or its `LoadChapter`). The format's `SyntheticFamily` entry and its
   fixture are deleted in the same change, and a unit pins what the suites rely on
   (`SyntheticDataTests.ASyntheticLaunchBuildsItsGameDataInMemoryWithNoSource`).
4. **Consumers.** Every engine path that took the format's path takes the model, reached through
   `BasePaths.Data`, `BuildState.Data`/`Chapter`, `TestContext.Data`, `IOriginalScreenHost.Data` or
   `CampaignFeature.Data`; a suite that skipped on a missing file skips on an empty model.
5. **Oracle.** The JSON parse moves to `CSVM.Tests/Oracle/<Format>Legacy.cs`, which the model diff
   and the JSON-literal units read.

**Verified.** complete `RunTests.ps1` on the merged plan tree at `485f7fcf6` (battery 2), PASS: units 7,612 passed, 0 failed, 3 skipped; engine 614 passed, 2 skipped, engine errors clean; 38 goldens hash-identical. The CI tier's synthetic suites ran on in-memory messages with no game source; battery 2's first run on `8c3943d4a` failed one unit, an upward Spec-to-Tooling reference, fixed in `485f7fcf6`.

**Original approach (kept for reference).**

**Goal.** A session receives its loaded models (interp scripts, messages, and the slots later
items fill: root and chapter readers, sounds, planes, textures, gamez, anim) through one game-data
value, built either from the game-file source or by the synthetic tool in memory; no engine path
below the launcher takes an extracted file path for a format that has switched.

**Evidence (confidence: direction-sound).** Decision 17. B11 already passes a loaded model
(`InterpScripts`) through `BuildState`/`WorldSession.Options` instead of a path. B12 found the
messages path threaded through about 85 engine files (`BuildState`, `TestContext`,
`SessionBoards`, `InstantActionDirector` inputs, screens and seats), and four sites that build
`extracted/messages.json` from the data root alone (`BootSequence`, `CampaignBriefing`,
`CampaignFlightCheckPage`, `BotSeats`). `Tooling/SyntheticData.cs` and its siblings write an
`extracted/`-shaped tree from fixtures (`planes/`, `zrdr/` records, `messages.json`, sounds,
textures, one mission), which the CI engine suites read.

**Approach.** A game-data type beside `BasePaths` with a chapter-independent part (loaded once per
launch) and a per-chapter part (loaded per session), a factory from `BasePaths.Game`, and a
synthetic factory in `Tooling/`. Fold B11's `Interp` into it, then switch messages (B12's switch)
as the first full migration: one `Messages` object in place of the path, the four data-root sites
included, and the synthetic tool building its invented messages in memory instead of writing
`messages.json`. Later B and C items add their slot and their synthetic builder the same way.
The synthetic fixtures become code (**Landed**, the recipe).

**Model recommendation.** high, it reshapes how every session gets its data.

**Verify.** Complete battery on synthetic data (the CI engine tier) and on the real install, both
green; the B12 model diff clean; a grep shows no engine use of `MessagesPath` left.

**⚠ Traps.** A synthetic run has no game source (`BasePaths.Resolve`), so nothing in it may fall
back to a real install. The messages switch touches many files: merge it alone, not beside another
Wave B switch.

# Wave B, small formats

## B11 ☑ `interp.zbd`

**Landed.** `CSVM/src/Mech3/InterpScripts.cs` reads `ZBD/interp.zbd` through `BasePaths.Game` into
one model, `InterpScripts` (a list of `InterpScript(Name, Lines)` in file order, `Lines(name)`
matched without regard to case); `Load(game)` is null without a source or the file. The layout is
in [formats/interp.md](formats/interp.md): a 12-byte header (signature `0x08971119`, version 7,
count), a 128-byte entry per script (120-byte name, timestamp, start), then null-separated lines
each with a size and an argument count equal to its nulls. The reader refuses a file whose bodies
do not tile it. All four consumers take the model in place of the path: `MissionSetup.Load`,
`ClutterBuilder.TemplateNames`, `LensFlareRig.FlareTextureNames` (and both flare rigs' `Build`)
and `TextureArchive.MipBias`. `GameSession` loads it once per build into `BuildState.Interp`, which
feeds `WorldSession.Options.Interp` and `SkyStage`; the launcher's mip bias and `--dump-mips` load
it themselves. Suites call `TestContext.RequireInterp()` (SKIP without an install), the harness
world build loads it from the new `TestContext.Game`, and the five suites that build their own
`BasePaths` pass `Game = ctx.Game` on. The JSON read moved to `CSVM.Tests/Oracle/InterpLegacy.cs`;
`CSVM.Tests/InterpScriptsTests.cs` holds the CI-safe units over a hand-encoded file and the model
diff. Unused now, for E43: `BasePaths.InterpPath`, `TestContext.InterpPath`, `--interp=`
(`SessionSpec.Interp`) and the `interp` `ZbdExtraction` mode.

**Verified.** complete `RunTests.ps1` on the merged plan tree at `485f7fcf6` (battery 2), PASS: units 7,612 passed, 0 failed, 3 skipped; engine 614 passed, 2 skipped, engine errors clean; 38 goldens hash-identical. Every interp consumer reads `interp.zbd`; the goldens, which draw C5's mip bias, clutter and lens flares, are unchanged.

**Premise correction.** `TextureArchive.MipBias` is one of **four** readers of the interp data,
each parsing `interp.json` on its own; `MissionSetup`, the clutter registry and the lens flare
slots are the other three. All four moved, so no engine path reads the JSON.

**Original approach (kept for reference).**

**Goal.** `TextureArchive`'s interp read comes from `interp.zbd` directly.

**Evidence (confidence: lead-only).** One input; consumed at `TextureArchive.cs:904-910`; Rust
`interp` crate 221 lines.

**Approach.** Reader into the model `TextureArchive` builds today; model diff; delete the JSON path.

**Model recommendation.** medium.

**Verify.** A2 diff clean; complete `RunTests.ps1` with goldens unchanged.

**⚠ Traps.** The extraction's timestamp (`datetime`) has no consumer, so the model leaves it out
and the diff compares names and lines only. A run without an install now reads no scripts, where
before it read `extracted/interp.json`: mission setup, clutter, flares and the C5 mip bias then
fall back to their "nothing authored" behaviour, as they did without an extraction.

## B12 ☑ `strings.dll` messages

**Engine switch landed with A3**, which carries the record; the gaps below are closed there.

**Reader landed.** `Messages.Load(IGameFileSource?)` and
`Messages.FromStringsDll(byte[])` (`CSVM/src/Mech3/Messages.cs`) build the existing model from
`strings.dll`; `PeStringTable.Section` (`CSVM/src/Extraction/PeStringTable.cs`) gives the `.data`
bytes and load address the key table's pointers need, and `Read` is unchanged in behaviour.
`CSVM.Tests/MessagesDllTests.cs` holds CI-safe units on a DLL built in code, the model diff
against `messages.json` (1,023 keys, 1,024 leaves, clean) and a one-character corruption reported
under `MSG_GAME_NAME_VER`. The layout is in
[formats/missions.md](formats/missions.md#message-table).

**Premise correction.** `PeStringTable` alone cannot build the model: the `MSG_*` keys are not in
the resources. They are a table of name-pointer and id pairs in the DLL's `.data` section, joined
to the `STRINGTABLE` by id. The Rust crate's 1,244 lines are mostly generality CSVM does not need:
PE header structures (384), a resource-directory walk with assertions for two layouts (373), the
`RT_MESSAGETABLE` decoder the MechWarrior 3 family uses (77), and glue (213); the Crimson Skies
part is the string-table block (49) and the `.data` key table (80).

**Why the switch is held.** Two gaps the plan does not cover:

1. The synthetic tree (`Tooling/SyntheticPlane.cs`) writes an invented `extracted/messages.json`
   that the CI engine suites read through `ctx.MessagesPath`. Read from the install only, a
   synthetic run gets an empty table in CI, and on a machine with an install `GameFileSource.Locate`
   can hand it the real `strings.dll`, so its labels depend on the machine. Every Wave B and C
   format the synthetic tree writes (zrdr, planes) meets the same question.
2. The table is threaded as a path through about 220 references in 85 engine files
   (`BuildState`, `TestContext`, `SessionBoards`, `InstantActionDirector` inputs, screen and seat
   parameters), and four sites build `extracted/messages.json` from the data root alone
   (`BootSequence`, `CampaignBriefing`, `CampaignFlightCheckPage`, `BotSeats`).

**Verified.** complete `RunTests.ps1` on the merged plan tree at `485f7fcf6` (battery 2), PASS: units 7,612 passed, 0 failed, 3 skipped; engine 614 passed, 2 skipped, engine errors clean; 38 goldens hash-identical. Every message the engine shows comes from `strings.dll`; the menu, HUD and campaign goldens are unchanged.

**Original approach (kept for reference).**

**Goal.** `Messages` reads `strings.dll` directly.

**Evidence (confidence: lead-only).** `Messages.cs:27`; Rust `messages` crate 1,244 lines. CSVM
already reads PE string tables (`PeStringTable.cs`).

**Approach.** Reuse `PeStringTable.cs`; model diff; delete the `messages.json` path.

**Model recommendation.** medium.

**Verify.** A2 diff clean; complete battery.

**⚠ Traps.** The text is UTF-16LE, so no code page applies; the resource entries' code page
field reads 1252 and is ignored. The Windows-1252 decode in the Rust crate belongs to the
`RT_MESSAGETABLE` games, not to Crimson Skies.

## B13 ◐ Sound archives

**Reader landed, engine switch pending.** `SoundZbd.Load(IGameFileSource?, path)` and
`SoundZbd.Read(stream, name)` (`CSVM/src/Mech3/SoundZbd.cs`) open `soundsh.zbd` or `soundsl.zbd`
through A1's `ZbdArchive` and list its `SoundFile`s in table order (the entry and its collision
rename; `Key` is today's zip entry name). `ReadWavBytes(name)` reads one WAV on demand, so the
128 MB archive is never held whole; `ReadWavBytes(SoundFile)` reads a listed one. The object owns
the archive's stream. No edit to `SoundArchive.cs` or any engine file. The archive and lookup rule
are in [formats/sounds.md](formats/sounds.md#the-sound-archives). `CSVM.Tests/SoundZbdTests.cs`
holds four CI-safe cases (the archive count, the rename and first-occurrence lookup, the case-only
pair, no source) and the model diff over both archives against today's zips (a `Dictionary` of
zip name to bytes on each side): `soundsh` 2,521 sounds, 127,977,676 leaf values, `soundsl`
2,520 sounds, 62,207,362 leaf values, clean. The same theory asks `SoundArchive.ReadWavBytes`
over the zip and the reader for every zip name, in its own spelling and upper-cased (5,042 and
5,040 lookups), and gets the same bytes. A byte flipped in the middle of one `soundsl` payload is
found as `Dictionary["VO_id38_GL-AllyDwn-C.wav"][7079]: expected 0, actual 255`.

**One home for the rename rule.** The three copies (`TextureZbd`, `ZrdrZbd`'s internal `Rename`,
`ZbdArchiveTests`) agreed exactly: an ordinal set, a split at the last period, `name-N` without
one, `N` counting from 1, and a rename itself taken. The test copy returned the name instead of
null for a first occurrence and wrote the suffix as `name[dot..]`, which is the same string. The
rule is now `ZbdArchive.Rename(seen, name)` (`CSVM/src/Extraction/ZbdArchive.cs`), and all three
call it; [formats/zbd-archive.md](formats/zbd-archive.md#reader-rules-and-edge-cases) states it,
and the texture and zrdr pages point there. `TextureZbdTests`, `ZrdrZbdTests` and `ZbdArchiveTests`
pass unchanged. This closes B14's remaining step 4.

**How a sound is looked up (decided).** By `Key`, case ignored, the first in table order winning
when two keys differ only by case. Each archive's 44 repeats are byte-identical copies of their
first occurrence, so the bare name loses nothing. The one case-only pair,
`VO_c4-RM-m3_Blacke_9.wav` and `VO_c4-RM-m3_blacke_9.wav`, holds the same 16,462 bytes (zip CRC32
`119681842`) in both archives. Today's `SoundArchive` map, case-ignoring and filled in zip order,
answers the later of the pair, so the two rules differ in which entry they pick and not in the
bytes. `soundsl.zbd` lacks `chaingun.wav`, the one name `soundsh.zbd` has beyond it; the engine
reads `soundsh` only.

**Read cost** (Debug build, archive in the OS file cache): opening `soundsh.zbd` (table only) 0.9
to 2 ms; reading all 2,521 WAVs 41 to 46 ms, about 0.02 ms each, the slowest single read 14 to
16 ms. `soundsl` opens in 1.2 to 2 ms and reads in 19 to 31 ms.

**Remaining switch work (after A3).**

1. **The game-data slot.** `soundsh.zbd` is a root archive, so the slot is chapter-independent:
   `GameData.Sounds`, a `SoundZbd` from `SoundZbd.Load(game, "ZBD/soundsh.zbd")`, opened once per
   launch and owned by the game data. `SoundArchive` takes it in place of the path and keeps its
   decode and `(wav, looped)` cache; its `Dispose` then leaves the shared archive open, so the
   reopen-after-close path and the `SoundsOutliveBuild` distinction for sounds lose their reason
   (`SessionArchives.SoundsOutliveBuild`, `WorldSession` line 446). The consumers are
   `SessionArchives.OpenFor` (`soundsPath`), `LaunchAudio` (`paths.SoundsPath`), the directory
   archive `GameSession` opens near line 2761, `CampaignBriefing`'s `WavCues.ReadFrom` (to the
   bytes form `WavCues` already has), and about 20 suites calling `new SoundArchive(ctx.SoundsPath)`
   after `ctx.RequireData(ctx.SoundsPath, …)`, which then read `TestContext.Data` and skip on a
   missing archive. `BuildState.SoundsPath`, `ProbeRunner` and the net suites' `SoundsPath:`
   arguments carry the path today.
2. **The synthetic builder**, per the A3 recipe. `Tooling/SyntheticSounds.cs` writes one generated
   MS ADPCM WAV per entry of `fixtures/synthetic/soundsh/manifest.json` into `extracted/soundsh/`.
   It becomes a builder of the same WAVs in memory, packed into the container of
   `docs/formats/zbd-archive.md` on a `MemoryStream` and opened with `SoundZbd.Read`, so the engine
   keeps one sound reader. The manifest's lengths and tones become C# literals, the fixture and its
   `SyntheticFamily` entry go, and `SyntheticSoundsTests` pins the in-memory set.
3. **The legacy move.** `SoundArchive`'s zip and directory branches (`_zipPath`, `_dir`,
   `_entryPaths`, the reopen and `ReportReopen`) go to `CSVM.Tests/Oracle/SoundArchiveLegacy.cs`
   as the zip map the theory compares against; `SoundArchiveTests`' reopen contract goes with them
   or is replaced by a unit over `SoundZbd`. Unused afterwards, for E43: `BasePaths.SoundsPath`,
   `TestContext.SoundsPath`, `BuildState.SoundsPath`, `--sounds=` (`SessionSpec.Sounds`),
   `PreferUnzipped` for sounds, `--zip-assets` for sounds, and the `sounds` `ZbdExtraction` mode.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** `SoundArchive` reads `soundsh.zbd`/`soundsl.zbd` directly.

**Evidence (confidence: lead-only).** `SoundArchive.cs:39,126`; entries are stored raw in the
archive (`archive` crate).

**Approach.** Entries through the A1 archive reader into `SoundArchive`'s model; model diff; delete
the zip path.

**Model recommendation.** medium.

**Verify.** A2 diff clean; complete battery.

**⚠ Traps.** Each sound archive repeats 44 entry names, one pair differing only by case
(`VO_c4-RM-m3_Blacke_9.wav`). Today's zips store a repeat as `stem-N.ext`, counting from 1;
reproduce that wherever a loader looks a sound up by name (A1's finding).

## B14 ◐ Reader archives (`zrdr.zbd`)

**Reader landed, engine switch pending.** `ZrdrZbd.Read(byte[])` and
`ZrdrZbd.Load(IGameFileSource?, path)` (`CSVM/src/Mech3/ZrdrZbd.cs`) decode a root, chapter or
mission `zrdr.zbd` through A1's `ZbdArchive` into `ZrdrFile` records in table order: the stored
name, the collision rename (`Key` is the name a lookup uses, today's zip entry name without
`.json`), and the root list in exactly the shape `Zrdr.LoadFile` returns (strings, `float`s,
nested lists, `null` for an empty nested list, an empty root for an empty reader). `ReadReader`
decodes one payload; `Find(files, name)` is the lookup rule. No edit to `Zrdr.cs` or any engine
file. The value encoding is new in [formats/zrdr.md](formats/zrdr.md#binary-encoding), and the
README's everything-is-float bullet is corrected (the file tags `i32` and `f32` apart; the model
reads both as `float`). `CSVM.Tests/ZrdrZbdTests.cs` holds 13 CI-safe cases (value decode with
`-0` and a subnormal, an empty reader, eight refusals, the rename and lookup rule over an archive
built in code, no source) and the model diff over all 62 archives against today's zips
(`Zrdr.LoadFileOrEmpty` per zip entry, a `Dictionary` keyed by zip name): 1,293 readers, 576,469
leaf values, every float bit equal. A one-bit flip of a stored float in the root archive is found
as `Dictionary["camparam.zrd.json"][1][1][0]: expected 13 (0x41500000), actual 13.000001
(0x41500001)`.

**The f32 trap does not bite.** The JSON detour widened each `f32` to `f64` and `Zrdr.Convert`
narrowed it back with `GetSingle`; for every shipped float that returns the stored bits, so the
direct reader's raw bits match today's model with no special case. Integers (tag `1`) reach the
model as `(float)value`, which equals `GetSingle` on the integer's JSON text.

**How consumers look entries up (decided).** By `Key`, ignoring case, with today's spellings
accepted: `vehicle.json`, `vehicle.zrd.json` and `vehicle.zrd` all find `vehicle.zrd`, and the
root's repeat is `player-1.zrd` (`player-1.json`, as `InspectionLabs` and `EffectCatalogueTests`
name it). The bare name finds the first occurrence, as the zip lookup does today. The two
`player.zrd` are different files: the first holds the player-plane globals every `player.json`
consumer reads, the second the plane's damage `ANIMATION_DEFINITIONS`, which `AnimDefs` reaches by
content scan. It is the only repeat in the 62 archives, and no archive holds two names differing
only by case. Five readers are empty (root `dlgMessage`, `mp_dialog`; C1C and C2B `templates`;
C2B/MP1 `targets`).

**A2's figure explained.** The shared archive compares 304,617 leaves here against A2's 304,837:
A2 read the unzipped folder through `LoadFilesNamed`, which takes `manifest.json` as a reader (221
null values plus its count, 222 leaves) and skips the two empty readers (one count leaf each).

**Decode cost** (Debug build, one thread, from bytes in memory): root `zrdr.zbd` (3.99 MB, 221
readers) 28 to 43 ms; a chapter 0.1 to 1.7 ms (C2 1.65); a mission 0.06 to 1.1 ms (C1/M02 0.7,
C5/M02 0.4), apart from a single 3.7 ms reading of C4/M03. A session's three archives
cost under 50 ms.

**Remaining switch work (after A3).**

1. **The game-data slots.** `GameData` carries the root files (`ZBD/zrdr.zbd`), `ChapterData` the
   chapter's (`ZBD/<chapter>/zrdr.zbd`) and the mission's (`ZBD/<chapter>/<mission>/zrdr.zbd`).
   A lookup type over `IReadOnlyList<ZrdrFile>` replaces the path in the four `Zrdr` entry points
   with the same contracts: `LoadFile` through `Find`, throwing on an absent entry and on an empty
   root; `LoadFileOrEmpty` returning the empty root; `HasFile` as `Find != null`;
   `LoadMatchingFiles` as a walk for a string containing the filter (today a substring sniff of the
   raw JSON); `LoadFilesNamed` with the predicate seeing `Key + ".json"`, today's names. Both
   enumerations skip empty readers, as today. The path is threaded far: `ZrdrPath` alone has 1,548
   references in 217 engine files (most in `Testing/` suites), beside `SessionPaths.ChapterZrdr` and
   `MissionZrdr` and the mission-path fields (`MissionZrdrPath`, `chapterZrdrPath`), so the switch
   merges alone, as A3's did.
2. **The synthetic builder**, per the A3 recipe: `Tooling/Synthetic{Plane,Effects,Mission,Sounds}.cs`
   copy reader records from `fixtures/synthetic/` today (`vehicle`, `engines`, `player`,
   `maneuvers`, `weapons`, `shakes`, `ai`, `probe_world`, `flame_ball`, `pufftrails`,
   `cm_sequence`, `Briefing`, the mission scope's records, each IA chapter's `ia`, `sounds`,
   `voice`). A `Tooling/SyntheticZrdr.cs` builds them as `ZrdrFile` lists in code for the three
   scopes, and the fixtures and their `SyntheticFamily` entries go.
3. **The legacy move**: `Zrdr`'s zip/folder reading (`ReadEntry`, `CandidateNames`, `Convert`, and
   the four path entry points) goes to `CSVM.Tests/Oracle/ZrdrLegacy.cs`, dropping the v0.6.1 zips
   that store `vehicle.json` as an entry name; `ZrdrDict` stays in the engine. The theory then compares that against
   the reader. Unused afterwards, for E43: `BasePaths.ZrdrPath`, `SessionPaths.ChapterZrdr`,
   `SessionPaths.MissionZrdr`, `PreferUnzipped` for zrdr, `--zrdr=` and the `reader`
   `ZbdExtraction` mode.
4. ~~One rename rule.~~ Done in B13: `ZbdArchive.Rename` is the one home, and every reader calls it.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** `Zrdr` builds its dictionaries from `zrdr.zbd` directly, for root, chapter and mission.

**Evidence (confidence: lead-only).** `Zrdr.cs:20-179`; about 54 archives; Rust `reader` 176 lines;
floats are `f32` (`reader/src/read.rs`) and `Zrdr.cs` reads single precision.

**Approach.** Reader into `ZrdrDict`; model diff; delete the JSON and v0.6.1 shape paths.

**Model recommendation.** medium.

**Verify.** A2 diff clean over every reader archive; complete battery; 8-chapter regression.

**⚠ Traps.** The JSON detour widened floats to `f64`; the direct path must keep the `f32` bits, and
the diff compares bitwise. Entry names are `.zrd`, renamed `.json`/`.zrd.json` in today's zip.
The root `zrdr.zbd` holds `player.zrd` twice; today's zip keeps the repeat as `stem-N.ext` (A1's
finding).

## B15 ☑ Texture archives

**Landed.** The engine reads no extracted texture PNG, zip or `manifest.json`. `TextureSet`
(`Mech3/TextureZbd.cs`) holds one archive's `TextureImage` records by `Key` without case, with its
`Archive` name (`c1/rtexture15`, `rimage`, the install path below `ZBD/` without the extension);
`TextureSet.ForChapter` picks the highest `rtextureN` tier, else `texture.zbd`, as
`SessionPaths.ChapterTextures` did. `GameData.Rimage` holds `rimage.zbd`, loaded in `GameData.Load`,
and `ChapterData.Textures` the chapter tier; the install's chapter loader holds the last chapter's
set, so suites and rebuilt sessions decode a chapter once. `TextureArchive(TextureSet)` keys its
records by `Key` and its alpha classes and render flags by `Name`, builds every image (base and the
authored `_1`/`_2` levels) with `TextureArchive.ImageOf`, `Image.CreateFromData` in RGBA8 or RGB8 as
the record carries it, and returns the raw render-flags word; `Lookup` replaces `ReadPngBytes` and
keeps the after-`Dispose` refusal. HD packs key by archive name and texture name:
`HdTextures.ForWorld(archive, name)`, and `ForRimage(name)` for the menu art (`rimage/<name>.png`
first). The rimage readers take the set: `HudFont.Load`, `ImpactReticle.LoadTexture`,
`ObjectivesHud.LoadMark` (through `HdArt.Rimage`), `CampaignBriefingPage` (`Rimage.Rgba` from
`CampaignFeature.Data`), and `ComposedBoardView.Build(dataRoot, rimage)`, whose six boards
(`LoadBoard`, `BootCard`, `PausePreferences`, `OriginalPauseBoard`, `OriginalRaceBoard` through
`SessionBoards.Inputs.Rimage`, `OriginalPresentation` through its `Data`) are handed it. Sessions
open the chapter set through `SessionArchives.OpenFor(..., BuildState.Chapter.Textures, ...)`
(`BuildState.TexturesPath` deleted); the probes take a set (`Probes.Loadouts`, `MipChains`); the
suites read `TestContext.ChapterTextures(chapter)` and `ctx.Data.Rimage`, and skip through a
`RequireData(TextureSet, ...)` overload on an empty set. `Tooling/SyntheticTextures.cs` builds the
C1 set in memory (`Entries`, the checker pixels), handed out by `SyntheticGameData`'s chapter loader;
the `chapter-textures` family and `fixtures/synthetic/C1/texture/manifest.json` are deleted. The
legacy read is `CSVM.Tests/Oracle/TexturesLegacy.cs` (`Load(zipOrFolder)`, and `Rimage(dataRoot)` for
units that hand a screen an extracted data root), which the model diff's expected side reads.

Unused now, for E43: `SessionPaths.ChapterTextures`, `PreferUnzipped` for textures,
`SessionSpec.Textures` (`--textures=`), `HdTextures.ForExtracted` for `rimage` paths (still the rof
art's), `ZbdPlan.AlwaysUnzipped` and `ExtractionFlow`'s always-unpacked `rimage` rule, and the
`textures` `ZbdExtraction` mode. No `BasePaths` field was texture-specific.

**Verified.** <pending orchestrator run>

**Reader (kept from the reader landing).** `TextureZbd.Read(byte[])` and
`TextureZbd.Load(IGameFileSource?, path)` (`CSVM/src/Mech3/TextureZbd.cs`) decode a texture
archive into `TextureImage` records in file order: the stored name, the collision rename (null for
a first occurrence; `Key` is the name a lookup uses, today's PNG base name), width, height,
`TextureArchive.AlphaClass`, the raw render-flags word, and the pixels as RGBA8 when the texture
has an alpha channel and RGB8 otherwise, the two layouts today's PNGs carry, so the switch can
build the same `Image` format the PNG decode gives. Engine-free, no Godot type in the decode. The
layout is the new [formats/texture-archive.md](formats/texture-archive.md).
`CSVM.Tests/TextureZbdTests.cs` holds nine CI-safe units over archives encoded in code (16-bit and
paletted, local and global palette, the three alpha classes, the rename rule, rounding, three
refusals), the model diff over all 49 archives, and a one-pixel corruption found as
`[k].Pixels`. The diff builds today's model from each zip's `manifest.json` (name, rename, alpha,
width, height, `stretch`) and its PNGs decoded engine-free with `PngImage` (colour type deciding RGB
or RGBA, no `gAMA` asserted), and compares field for field and pixel for pixel: 37,004 textures,
166,547,780 pixels, all equal.

**Where the alpha class comes from (the TODO).** The header, never the pixels. The Rust computes it
in `crates/image/src/read.rs` (`convert_info_from_c`) from the storage flags byte: `0x04` is
`None`, else `0x02` with `0x08` is `Full` and without it `Simple`. The pixels then follow the class:
`Full` takes the alpha plane, a 16-bit `Simple` texture is clear only where the texel is `0x0000`
(`pixel-ops` `simple_alpha`), and a paletted `Simple` texture gets no alpha channel at all, which is
why 22 `texture.zbd` textures read `Simple` with an opaque PNG.

**Findings.**

- **No rename fires on shipped data.** No name repeats within any of the 49 archives, with or
  without regard to case, and no manifest carries `rename`. The rule is reproduced exactly
  (case-sensitive, `stem-N.suffix` split at the last period, a rename itself taken) and unit-tested
  only, so HD packs and gamez see exactly the stored names.
- **No archive ships a global palette**, and every paletted texture (6,125, local palette) is in a
  `texture.zbd`; the five tiers and `rimage` are all 16-bit.
- **The render-flags spelling is swapped in CSVM.** The extraction's `Vertical` is 1 and
  `Horizontal` 2 (C1 `sky1` raw 2, `tracer1` raw 1), but `TextureArchive.RenderFlagsOf` reads
  `Horizontal` as 1, and [org/textures.md](org/textures.md) listed the two bits' textures under each
  other's bit (corrected there). Nothing reads bits 0 or 1 (`IsAdditive` is bit 2, the upload depth
  bit 3), so the switch, which carries the raw word, changes no behaviour; `RenderFlags()` returns
  the raw word from then on.
- **Decode cost** (Debug build, one thread, from bytes in memory): 7 to 21 ms for a `rtexture2` tier,
  32 to 56 ms for a chapter's top tier, 27 to 49 ms for a `texture.zbd`, 142 ms for `rimage.zbd` (37.9 MB,
  16.0 M pixels). A session needs one chapter tier plus `rimage`, about 200 ms.

**Original approach (kept for reference).**

**Switch plan (after A3).**

1. **The game-data slot.** The chapter part of A3's game-data value carries the chapter's top
   `rtextureN` tier (what `SessionPaths.ChapterTextures` picks today: the highest `N` present,
   else `texture`), and the chapter-independent part carries `rimage`. `TextureArchive` takes the
   records in place of the path: `_byBaseName` keyed by `Key`, `_alphaClasses` and `_renderFlags`
   keyed by `Name` (as the manifest keys them today), and `Build` makes its `Image` with
   `Image.CreateFromData(w, h, false, HasAlphaChannel ? Rgba8 : Rgb8, Pixels)` where it now calls
   `LoadPngFromBuffer`; `InstallAuthoredMips` the same for the `_1`/`_2` siblings. `ReadPngBytes`,
   `HdTextures.ForWorld` (keyed by the archive's folder path) and `TextureDropIn.CheckNames` need a
   key that is not a path.
2. **`rimage` is not read through `TextureArchive`.** It is opened as `extracted/rimage/*.png` by
   path in ten engine files (`HudFont`, `ImpactReticle`, `GameSession`, `ObjectiveReadouts`,
   `ObjectivesHud`, `CampaignBriefingPage`, `ComposedBoard`, `ComposedBoardView`,
   `OriginalWidgets`, `ExtractionFlow`'s always-unpacked rule), plus six suites and the HD pack's
   `rimage/` entries (`HdTextures`, `PackIndex`). Each moves to a lookup by name in the `rimage` records, returning a
   `TgaImage` (`TgaImage.FromRgba`, expanding RGB to RGBA) where it decodes a PNG today.
3. **The synthetic builder**, per the A3 recipe: `Tooling/SyntheticTextures.cs` builds
   `TextureImage` records in memory instead of writing the unpacked `C1/texture` tree.
4. **The legacy path moves**: `TextureArchive`'s PNG/zip/folder reading and `ReadTextureManifest`
   go to `CSVM.Tests/Oracle/TexturesLegacy.cs`, and the theory compares that against the reader.
   Unused afterwards, for E43: `SessionPaths.ChapterTextures`, `PreferUnzipped` for textures, the
   `textures` `ZbdExtraction` mode.

**Goal.** `TextureArchive` decodes `texture.zbd`, `rtextureN.zbd` and `rimage.zbd` into images
directly, with the same names and alpha classes.

**Evidence (confidence: lead-only).** `TextureArchive.cs:759-766` reads PNGs; alpha class from the
manifest (`:1306-1315`); Rust `image` + `pixel-ops` 1,300 lines; palettes, RGB565, simple alpha;
the `Rename` collision rule.

**Approach.** Decode to the image model `TextureArchive` builds after PNG decode; reproduce the
alpha class and rename rules; model diff on pixels and names; delete the PNG/zip path.

**Model recommendation.** high, names feed HD packs and gamez.

**Verify.** A2 diff clean, pixel for pixel, over all 49 archives; HD texture pack still resolves
(`HdTextures.cs`); complete battery.

**⚠ Traps.** HD packs and gamez reference textures by name, so the rename rule must match exactly.
<TODO: where the alpha class is computed in the Rust, and whether it reads the pixels or a header.>

# Wave C, large formats

## C21 ◐ Anim archives (`cam_anim.zbd`, `mis_anim.zbd`)

**Reader landed, engine switch pending.** `AnimZbd.Read(byte[], label)` and
`AnimZbd.Load(IGameFileSource?, path, label)` (`CSVM/src/Mech3/AnimZbd.cs`) decode a chapter's
`cam_anim.zbd` or a mission's `mis_anim.zbd` into the existing `AnimArchive` model: every
definition, its support arrays, reset sequence, sequences and destruction slot, and every SI
script, decoded up front. `CSVM/src/Mech3/AnimZbdEvents.cs` holds the 35 shipped event payloads.
The reader rebuilds each definition and script as the property tree the model's own `Parse`
members take (`AnimData`), so the model cannot drift from the JSON path's: `AnimEvent.Data` keeps
the whole payload bag, every field present and every option null where absent. The one edit to
`CompiledAnim.cs` is the additive `internal static AnimArchive.FromDecoded`, which fills the
script cache so `Script(i)` reads no file. The layout facts the reader relies on are the new
**C# reader** section of [formats/anim-definitions/compiled-archives.md](formats/anim-definitions/compiled-archives.md).
`CSVM.Tests/AnimZbdTests.cs` holds seven CI-safe tests, eight cases (the archive count, the rename
rule, two script pool names, a hand-encoded archive with a repeated node name, a node prerequisite, a reset sequence,
six event kinds and a NaN script delta, and two refusals) and the model diff over all 61 archives
against `AnimArchive.Load` on today's extraction with every script loaded on both sides, skipping
only `AnimArchive._path` and `AnimArchive._isDir`: every leaf equal, 5,350,835 in all (C1
`cam_anim` 236,655 and C5/M02 `mis_anim` 239,191, A2's figures less the two skipped fields). A
changed script frame time and a changed event payload in the direct model are both found.

**The split.** Two files by layer, both landed together because the diff settled every family in
one pass: the container, definitions, support arrays, sequences and script pool in `AnimZbd`, and
the event payloads in `AnimZbdEvents` (sound and light, object state and motion, camera and fog,
control flow, puffer, and the two raw-byte CS events). The six event types no archive ships
(EFFECT, OBJECT_CONNECTOR, CALL_OBJECT_CONNECTOR, CAMERA_FROM_TO, FBFX_CSINWAVE_FROM_TO,
ANIM_VERBOSE) are refused rather than written blind.

**What the stderr notes mark.**

- **The six NaN deltas** read as `+0`: where any component of a frame channel's `delta` is not
  finite, the extraction moved the vector to `delta_raw` and zeroed it, and the reader zeroes it
  the same way. The C5/M02 diff is clean with no skip.
- **"VAL FAIL"** marks a stored value the compiler's formula does not reproduce, which the model
  keeps beside the inputs: `DELTA VAL FAIL` is an opacity or FBFX-colour from-to whose stored rate
  is not `(to - from) / run_time` (8 alpha events over the 61 archives, no opacity event), and `INTERVAL VAL FAIL` a
  puffer state with only one of its two interval flags set, carried as `interval_garbage`. The
  reader recomputes the rate and the flags and keeps the same fields.
- **"anim def duplicate anim ref"** marks a def whose anim-ref list names one animation twice in
  the LOCAL_NAME form (171 repeats in 115 defs over the 61 archives). Nothing in the model reads that list, so the
  reader skips it.

**Decode cost** (Debug build, one thread, from bytes in memory): at most about 60 ms per archive,
C1 `cam_anim` 40 ms, about 0.85 s for all 61. A session decodes one chapter and one mission
archive, under 100 ms.

**Remaining switch work (after A3).**

1. **The game-data slot.** The chapter part of A3's value carries the chapter's `AnimArchive`
   (`ZBD/<chapter>/cam_anim.zbd`) and the mission's (`ZBD/<chapter>/<mission>/mis_anim.zbd`,
   absent for some missions, which `Load` returns as null as today). ⚠ The labels stay `cam_anim`
   and `mis_anim`: `AnimProgram.Load` decides the mission scope by the label. Its two path
   parameters become the two models; the other `AnimArchive.Load` sites are `OrdnanceSuites`,
   `ZeppelinSuites` and `EffectStageSuiteHelper`.
2. **The synthetic builder**, per the A3 recipe: `Tooling/SyntheticEffects.cs` writes an anim
   extraction today and builds the `AnimArchive` in memory instead (`FromDecoded` takes the
   parsed definitions and scripts).
3. **The legacy path moves**: `AnimArchive.Load`, `ReadAll`, `ReadEntry` and `JsonConvert` go to
   `CSVM.Tests/Oracle/AnimLegacy.cs`; the `Parse(AnimData)` members stay, since the reader builds
   through them. Unused afterwards, for E43: the `anim` `ZbdExtraction` mode and the anim paths
   in `SessionPaths`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** `CompiledAnim` builds `AnimDefinition`s from the 61 anim archives directly.

**Evidence (confidence: lead-only).** `CompiledAnim.cs:81,120,147-151`; Rust `anim` 8,369 (CS 2,111),
`anim-events` 11,598, `anim-names` 3,897; six NaN deltas carried raw; `docs/formats/anim-definitions.md`
and `compiled-archives.md`.

**Approach.** Split by event family if the size demands it; the split is under **The split** above.

**Model recommendation.** high.

**Verify.** A2 diff clean over all 61 archives; complete battery; 8-chapter regression.

**⚠ Traps.** NaN deltas compare by bits, not by value. The six NaN deltas of C5/M02
`carneypkup_cam.zan;camera1` reach today's model as `+0.0`, because `SiVectorChannel.Parse` reads
`delta` and never `delta_raw`; under Decision 1 the reader reproduces `+0.0` there (A2's finding).
Stderr notes such as "anim def duplicate
anim ref" (`ZbdPlan.Classify`) mark data quirks the reader must tolerate the same way.

## C22 ◐ GameZ and `planes.zbd`

**Reader landed, engine switch pending.** `GameZZbd.Read(byte[])` and
`GameZZbd.Load(IGameFileSource?, path)` (`CSVM/src/Mech3/GameZZbd.cs`) decode a chapter's
`gamez.zbd` or the root `planes.zbd` into the existing `GameZ` model: nodes, meshes, materials and
the texture table, which `GameZ` now exposes to the reader as the internal `TextureNames` (the one
edit to `GameZ.cs`, additive). Engine-free apart from the Godot math structs the model already
holds. The layout is the new [formats/gamez-zbd.md](formats/gamez-zbd.md).
`CSVM.Tests/GameZZbdTests.cs` holds seven CI-safe tests, eleven cases (a hand-encoded archive with a cycled and
a coloured material, a sparse model array and three node kinds; the texture-name rule; the
matrix-recompose test; two refusals) and the model diff over all nine archives against
`GameZ.Load` on today's extraction, every leaf equal: C1 1,327,100 (A2's figure), C1B 642,096, C1C
629,280, C2 928,179, C2B 543,068, C3 1,089,113, C4 1,561,538, C5 1,690,471, planes 1,365,159. A
one-field change to the direct model is found as `GameZ.Meshes[m].Polygons[2].Priority`.

**The split.** One reader file in four families, all landed together because each is small:
materials (with the texture table and flipbooks), models (the sparse array, lights, polygons),
nodes (the seven classes, the Object3d transform, the world's partition grid), and planes, which
is the same reader with no special case.

**Decision 14 holds.** The model needs nothing from the per-chapter image-pointer tables. Every
material texture name comes from the texture table's own name field; the image pointer only
distinguishes two runtime states that no part of the model reads, and the diff is clean without
it. The C4 and planes model-slot remap tables in the Rust (`fixup.rs`) are repack-only as well:
they record the free-list links of free slots, which the reader skips.

**What the stderr notes mark.**

- **"object3d transform fail"** is the extraction finding that an Object3d's stored matrix is not
  bitwise the one its rotation and scale recompose to (`R = Ry * Rx * Rz` in single precision).
  The extraction then writes the stored matrix as `original`, and `GameZ` uses it; otherwise
  `GameZ` takes the rotation through `Basis.FromEuler`. The reader repeats the recompose test
  (`GameZZbd.Recomposes`) and makes the same choice: 172 nodes keep their matrix (C1 17, C1B 1, C2
  4, C3 8, C4 10, C5 5, planes 127). ⚠ The test calls `MathF.Sin`/`MathF.Cos`, which on Windows
  match the extraction's single-precision sine and cosine for every node of the nine archives. A C
  runtime whose `sinf` rounds differently (glibc on Linux) could flip a borderline node between the
  two forms, a float-noise difference in one node's basis; unmeasured, since CI has no game files.
- **"VAL FAIL"** is not a gamez note. It is printed only by three anim event readers
  (`DELTA VAL FAIL` in object-opacity and fbfx-colour from-to events, `INTERVAL VAL FAIL` in the
  puffer state), so it belongs to C21.

**Decode cost** (Debug build, one thread, from bytes in memory, after the first row's JIT): C1 47
ms, C1B 24, C1C 17, C2 32, C2B 14, C3 35, C4 43, C5 56, planes 39. A session decodes its chapter
plus planes, under 100 ms.

**Remaining switch work (after A3).**

1. **The game-data slot.** The chapter part of A3's value carries the chapter's `GameZ`
   (`ZBD/<chapter>/gamez.zbd`), the chapter-independent part the planes `GameZ`
   (`ZBD/planes.zbd`). `SessionArchives.OpenFor` takes the model in place of `gamezPath`, and
   `DecodeCache.Gamez`, keyed by path today, keys by chapter. The engine's other `GameZ.Load`
   sites are `GameSession`, `WorldSession`, `InspectionLabs`, `Probes` and `TestHarness`; about 120
   engine suites load a gamez by `SessionPaths.ChapterGamez` and move to the slot.
2. **The synthetic builder**, per the A3 recipe: `Tooling/SyntheticPlane.cs` and
   `Tooling/SyntheticEffects.cs` write gamez JSON today and build the `GameZ` in memory instead.
   The `gamez-plane` and `gamez-lights` test fixtures stay JSON read by the legacy oracle.
3. **The legacy path moves**: `GameZ.Load` and its `Parse*` members go to
   `CSVM.Tests/Oracle/GameZLegacy.cs`, the v0.6.1 shape dropped, filling the model through
   `TextureNames` as the reader does, and the theory compares that against the reader. Unused
   afterwards, for E43: `BasePaths.PlanesGamezPath`, `SessionPaths.ChapterGamez`, the `gamez`
   `ZbdExtraction` mode.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** `GameZ` builds its node graph for every chapter and for `planes.zbd` from the game bytes.

**Evidence (confidence: lead-only).** `GameZ.cs:108-123`; Rust `gamez` 20,228 (CS 5,127) and
`nodes` 7,626; `docs/formats/gamez.md`, `world-structure.md`. The per-chapter image-pointer tables
are taken to serve only repack (decision 14), not verified.

**Approach.** Readers into `GameZNode` and its children; skip the pointer tables; model diff;
delete the JSON and v0.6.1 shape paths. The sub-item split is under **The split** above.

**Model recommendation.** high.

**Verify.** A2 diff clean over 8 chapters plus planes; complete battery; 8-chapter `--freecam`
regression with identical mesh/node counts.

**⚠ Traps.** If the diff shows a texture reference the engine needs coming only from the pointer
tables, decision 14 is wrong; record it in this item, do not quietly port the tables. Stderr notes
"object3d transform fail" and "VAL FAIL" mark tolerated quirks.

# Wave D, ROF and disc sources

## D31 ☐ ROF half read in place

**Goal.** `crimson.rof`/`crimptch.rof`, `.BM` textures, `langui.dll`/`language.dll`, the menu
layout and the cinemas are read from the game-file source; `extracted/rof/` is no longer written.

**Evidence (confidence: lead-only).** The C# decoders already exist (data survey, C# ROF half);
`MpegSystemStream.cs` decodes from a stream.

**Approach.** Point the existing decoders' consumers at the source; drop the PNG/JSON writes and
`MovieCopy`. <TODO: whether the cinema decoder needs a seekable stream, which matters for D33's
in-memory CABs.>

**Model recommendation.** medium.

**Verify.** Menus, UI strings and all 10 cinemas unchanged in the complete battery's goldens.

**⚠ Traps.** <TODO: none known.>

## D32 ☑ ISO9660 reader

**Landed.** `CSVM/src/Extraction/IsoImage.cs` reads an ISO 9660 image in place as an
`IGameFileSource` ([formats/disc-image.md](formats/disc-image.md)). `Read` walks the primary
volume once into a path-to-extent table rooted at the disc's `GAME` folder (the image root when
there is none): 8.3 names with the `;1` version and a trailing period dropped, matched without
regard to case, `.`/`..` segments matching nothing. `Open` hands out a seekable window onto the
image over a fresh handle per call, so no file is loaded whole and readers never share a position.
A malformed image (no primary descriptor, a bad block size, a short or overrunning record, an
extent past the end, a multi-extent file) throws `InvalidDataException`; a directory loop is cut by
a visited set. `GameFileSource.Open` takes a path ending `.iso` as an image and holds it to the
install rule (files below `ZBD` and `GOSDATA/ASSETS`), answering null for an image that does not
read, so `Locate` and `CSVM_INSTALL_ROOT` accept an `.iso` unchanged. `InstallLocator.Check`
already classed an `.iso` as a `Disc` and is untouched. The chapter `.CAB` files come out as plain
files.

**How D33 plugs in.** D33 wraps a source rather than changing this one: a cabinet source over an
inner `IGameFileSource` (the `IsoImage`, or the disc's `GAME` folder as an `InstallFolderSource`)
answers `ZBD/<chapter>/...` from that chapter's cabinet, unpacked in memory on first use, and
delegates every other path. It finds the cabinets by path through the inner source
(`ZBD/C1.CAB`); `IsoImage.Files` lists every file if a walk is wanted. Only
`GameFileSource.Open` changes: it returns the wrapper for an `.iso` and for a disc folder (a
`Check` of `Disc`), and the two `Assert.IsType<IsoImage>` in `IsoImageTests.cs` become checks on
the wrapper. Neither `IGameFileSource` nor any caller changes.

**The only gap is the real disc.** No image of the original disc is on this machine. The data
test `IsoImageDataTests` runs when `CSVM_TEST_ISO` names an `.iso` and skips otherwise: it checks
that the root archives are present, that `ZBD` holds a `.CAB`, and that every non-cabinet file the
image and the install both hold matches the install's bytes (SHA-256). The image and the install
must be the same release for the byte comparison. As a stand-in, a 300 MB image written by
Windows' IMAPI2 (ISO 9660 only) of the install's root archives, both `.rof` files and
`strings.dll` laid out as `GAME/` passed it: 9 files compared, all identical. That image came from
`IMAPI2FS.MsftFileSystemImage` with `FileSystemsToCreate = 1` and `Root.AddTree` over the staged
folder.

**The user's disc is the German release, with SafeDisc** (`SECDRV.SYS`, `DRVMGT.DLL`, `CLCD*`;
`00000407` locale files), which is why AnyBurn and ImgBurn fail to image it. Its 112 files copy
cleanly, and `Z:\CSVM\tmp\CrimsonSkies-DE.iso` (612 MB) is an IMAPI2 image of that copy: not the
pressed disc's bytes, but its files and layout. `IsoImage` opens it and finds the install layout
under `GAME`; the byte comparison then fails on the localised files (`CRIMSON.EXE`,
`STRINGS.DLL`, `ZBD/SOUNDSH.ZBD`, ...), so the data test takes the parse-and-count form (D33):
every root archive parses with its reader, and the count of files byte-identical to the English
install is reported, not required. E45's golden run uses an ISO built from the English install,
since the goldens are English.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** An `.iso` file is a game-file source.

**Evidence (confidence: lead-only).** The Rust walks the primary volume with 8.3 names only
(`disc.rs:87-155`).

**Approach.** Primary-volume directory walk into the A1 source interface.

**Model recommendation.** medium.

**Verify.** Every non-CAB file the engine reads opens from the ISO with the same bytes as from a
copy.

**⚠ Traps.** Names are 8.3 on the disc; the long `rtextureN` names come from D33's table.

## D33 ☑ LZX CAB decoder and the `rtextureN` long names

**Landed.** `CSVM/src/Extraction/Cabinet.cs` reads a Microsoft cabinet's tables and unpacks one
folder, stored or LZX, through `LzxDecoder.cs`, CSVM's own LZX decompressor
([formats/cabinet.md](formats/cabinet.md)). `CabinetSource.cs` wraps an inner source per D32's
contract: a path below `ZBD/<chapter>/` is answered from `ZBD/<chapter>.CAB` when the inner
source holds one, every other path by the inner source. The cabinet's file table is read on the
first lookup into a chapter (so `Exists` never decompresses); the folder unpacks into one array
on the first `Open`, each file a read-only `MemoryStream` over its range; only the chapter
opened last keeps its unpacked bytes (the orchestrator's change: all eight hold 396 MB, and a
switch back costs one 140-250 ms Release unpack at a load screen). The 8.3 tier aliases (`RTEXTU~1.ZBD`, `RT60AC~1.ZBD`) are paired in cabinet order with the
chapter's `rtextureN` names from `ZbdManifest`, sorted ordinally. `GameFileSource.Open` returns
the wrapper for an `.iso` and for a disc folder (`InstallLocator.Check` gives `Disc`), rooted at
its `GAME` folder when it has one. `IsoImageDataTests` takes the parse-and-count form, over an
image (`CSVM_TEST_ISO`) and over a disc folder (`CSVM_TEST_DISC`).

All eight German cabinets hold one LZX folder each (2^21-byte window, E8 translation on), so
MSZIP and Quantum are refused rather than implemented. Through the wrapper, over
`Z:\CSVM\tmp\CrimsonSkies-DE.iso` and over `Z:\CSVM\tmp\CSDisc`, all 184 `ZbdManifest` archives
parse with their readers, and 182 are byte-identical to the English install: every one of the
176 chapter archives, renamed tiers included. Only the localised `soundsh.zbd` and `soundsl.zbd`
differ. A chapter unpacks in 140 to 250 ms in Release (up to 460 ms cold) and 430 to 1,080 ms in
Debug, and holds 40 to 57 MB; all eight hold 396 MB. Decision 15 stands: no disk cache.

No existing C# LZX code was drawn on. The one found, MonoGame's
`MonoGame.Framework/Content/LzxDecoder.cs`, is a port of libmspack's `lzxd.c` under LGPL-2.1 or
MS-PL at the user's choice; its LGPL-2.1 option would be compatible with CSVM's GPLv3, but the
decoder is written from Microsoft's published cabinet and LZX specifications instead.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Chapter `.CAB` files on a disc or ISO unpack in memory per session, and their files
appear under the names an install uses.

**Evidence (confidence: lead-only).** CABs are LZX (`cab` crate, `crates/unzbd/Cargo.toml`); the
long-name table is per chapter (`disc.rs:177-203`); no native .NET LZX decoder.

**Approach.** Own CAB + LZX decoder in C#. In-memory per session (decision 15). The licence check
and the per-chapter timing are in **Landed** above.

**Model recommendation.** high, a codec with no runtime fallback.

**Verify.** Every CAB file's bytes match a known install's copy (hash compare); launch time from
ISO measured against an install.

**⚠ Traps.** If the measured cost is too high, the answer is a disk cache (decision 15), not a
return to extraction.

## D34 ☐ Optional copy into the data root

**Goal.** A player can copy the needed game files, CABs unpacked, into an install tree under the
data root, which the locator then finds.

**Evidence (confidence: lead-only).** Decisions 2 and 9.

**Approach.** Walk the file list the engine reads (the A1 source plus `ZbdManifest.cs` and the ROF
list) and write it out to `<data root>/CrimsonSkiesGame` (`GameFileSource.DataRootFolder`), which
`GameFileSource.Locate` ranks above the remembered install. <TODO: the UI entry point (from E41's
picker).>

**Model recommendation.** medium.

**Verify.** Copy from the ISO, unmount it, complete battery from the copy.

**⚠ Traps.** A partial copy must not be found as a valid install; write to a temporary name and
rename at the end.

## D35 ☐ Raw CD images: `.cue`/`.bin`/`.img` and CloneCD `.img`/`.ccd`

**Goal.** A raw CD image is a game-file source like an `.iso`: a `.cue` with its `.bin`/`.img`, or
a CloneCD `.img` with its `.ccd`, read in place.

**Evidence (confidence: lead-only).** The user asked for it. A raw image stores 2352-byte sectors;
the `.cue` names the image file and the data track's mode (`MODE1/2352`: user data 16 bytes into
each sector, `MODE2/2352` form 1: 24; `MODE1/2048` is an ISO). CloneCD images of SafeDisc discs
are common because SafeDisc defeats a plain 2048-byte read.

**Approach.** A sector-translating stream under D32's `IsoImage`, chosen in
`GameFileSource.Open` by extension; the ISO reader stays unchanged. <TODO: whether the data track
is always track 1 on the game's disc; `.ccd` fields the reader needs.>

**Model recommendation.** medium.

**Verify.** Units over a raw image built in code from an ISO (both modes, a `.cue` and a `.ccd`);
the real-disc check when a raw image of the disc exists.

**⚠ Traps.** A `.cue` may name its file in another case or with a path; resolve it beside the
`.cue`, case-insensitively.

# Wave E, cutover

## E41 ☐ Source picker and upgrade cleanup

**Goal.** With no source found, the game opens a picker (folder, drive or `.iso`) in place of the
Extract screen; once a source works, it offers once to delete an old `extracted/`.

**Evidence (confidence: lead-only).** Decision 8. `ExtractionStamp.Standing` sends the player to
the Extract screen today.

**Approach.** Reuse the Extract screen's locator UI; replace the extraction run with source
validation. <TODO: where "offered once" is remembered.>

**Model recommendation.** medium.

**Verify.** <TODO: a scripted check of the picker; the deletion prompt on a machine with an old
`extracted/`.>

**⚠ Traps.** The delete offer touches 1.6 GB of player data; it deletes only `extracted/`, after
confirmation, and never follows reparse points (CLAUDE.md's junction warning).

## E42 ☑ Dev-only `--dump`

**Landed.** `--dump[=folder]` (`SessionSpec.DumpPath`; the default folder is `.scratch/dump/`
under the run tree, a relative folder resolves against it) reads `strings.dll` and every
`ZbdManifest.Expected` archive through `BasePaths.Game` with the C# readers and writes the
models, then quits. `--chapter=<C>` is the chapter filter: the root archives plus that chapter's
and its missions'. The writer is `CSVM/src/Tooling/DumpGameFiles.cs`, engine-free (`Run`, `Write`,
`Files`); the launcher only parses the flag and calls `Run`, right after `BasePaths.Resolve`. Each archive lands where
`extracted/` kept it, so a script's path change is a root change: `messages.json` (key to text),
`interp.json`, `zrdr/<key>.json` and `<C>/<M>/zrdr/<key>.json` (the root list as `Zrdr` reads it),
`<C>/gamez/` and `planes/` as `nodes`, `meshes`, `materials` and `textures.json`, `cam_anim/` and
`mis_anim/` as `definitions.json` plus `scripts.json` in pool order, a texture archive as
`<key>.png` plus `manifest.json`, a sound archive as its WAVs. The JSON uses the models' own field
names; Godot vectors, colours and quaternions are arrays, a `Transform3D` twelve floats, NaN the
string `"NaN"`. `PngWriter.EncodeRgba` writes the textures with an alpha channel. Without a source the flag logs "no game files" and
exits 1; a chapter no archive names is refused. Shape: `docs/architecture/Tooling.md`, flag:
`docs/cli.md`. `CSVM.Tests/DumpGameFilesTests.cs` holds four CI-safe units (the file list and the
chapter filter, the no-source and unknown-chapter refusals, the JSON of a vector, a transform, NaN,
an event bag, a definition's dropped archive back reference and the message table) and one install
test: a small set (messages, interp, root zrdr, `soundsl`, C1B gamez, `cam_anim`, M03 `mis_anim`
and zrdr, `rtexture2`) dumped to a temp folder and read back against the readers, counts, names,
the PNG pixels of an RGB and an RGBA texture, and every WAV.

**A dump of C1B through the engine** (`RunProbe.ps1 -EngineArgs '--headless' --dump --chapter=C1B`,
exit 0) writes 9,628 files, 280 MB. `analysis/ai-ordnance-census/census.py`, copied to scratch
with its default path changed to `.scratch/dump/zrdr/vehicle.zrd.json`, prints the same 40 lines
as the original over `extracted/zrdr/vehicle.zrd.json`.

**Two private fields are read by reflection**: `Messages._byKey` and `AnimData._props` have no
enumerating member, and the dump is their only outside reader. A rename fails the units (the
lookup throws at type initialisation). An internal accessor on each model would replace it once
`Mech3/` is free to edit.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A headless flag writes the loader models as JSON/PNG/WAV into `.scratch` for analysis
scripts and agents.

**Evidence (confidence: lead-only).** Decision 7; 19 `analysis/` scripts read `extracted/`.

**Approach.** Serialize the models with `System.Text.Json`; images through the existing
`PngWriter.cs`. No player UI.

**Model recommendation.** medium.

**Verify.** A dump of one chapter loads in one of the existing analysis scripts after a path change.

**⚠ Traps.** The engine never reads the dump; nothing may grow a fallback onto it.

## E43 ☐ Delete the extraction pipeline

**Goal.** `UnzbdTool`, `ZbdExtraction`, `ZbdPlan`, `ExtractionRun`, `ExtractionStamp`,
`Extract.ps1`, the `--extract*` flags and their tests are gone.

**Evidence (confidence: lead-only).** Data survey, Sources and `unzbd` modes.

**Approach.** Delete, then fix callers. <TODO: the full caller list.>

**Model recommendation.** medium.

**Verify.** Build, complete battery, and a grep for `unzbd` outside history-only docs returns
nothing in code.

**⚠ Traps.** `ExtractionStampTests` guards the schema in three places; it goes with the stamp.

## E44 ☐ Packaging, licences, credit and docs

**Goal.** Releases carry no `unzbd`, `LICENSE-unzbd` or Rust build; README and the third-party
notices credit TerranMechworks/mech3ax in one linked line; no link to the fork remains.

**Evidence (confidence: lead-only).** `packaging/MANIFEST.md:23,32,36,54`, `ExportRelease.ps1`,
`PublishRelease.ps1`, `packaging/BuildThirdPartyNotices.ps1`, `README.md:76-77,125,203-204`,
`docs/tooling.md:916-930`, `PROJECT_CONTEXT.md:57,125`, `docs/upstream-pr/`.

**Approach.** Remove the unzbd and Linux musl steps from the export and publish scripts and
BUILD-INFO; rewrite `docs/formats/` citations into mech3ax code to cite the C# readers; drop the
fork section of `docs/tooling.md`. <TODO: whether `docs/upstream-pr/` is deleted.>

**Model recommendation.** medium.

**Verify.** `ExportRelease.ps1` and the Linux export build; `CheckDocEntries.ps1` passes; a grep
for `Laeresh/mech3ax` returns nothing.

**⚠ Traps.** <TODO: none known.>

## E45 ☐ Final ISO-only run

**Goal.** The game runs from an `.iso` with no install, no copy and no `extracted/` present.

**Evidence (confidence: lead-only).** Decision 10.

**Approach.** Move `extracted/` aside, point the data root at an empty folder and the source at
the ISO.

**Model recommendation.** medium.

**Verify.** Complete `RunTests.ps1` with every golden unchanged; 8-chapter `--freecam` regression.

**⚠ Traps.** The suites must not find an old `extracted/` through `CSVM_DATA_ROOT`; check the
environment before trusting a green run.
