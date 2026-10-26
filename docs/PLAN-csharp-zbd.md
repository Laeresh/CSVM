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
3. ☐ Session game-data seam and in-memory synthetic models

### Wave B, small formats

11. ☑ `interp.zbd`
12. ◐ `strings.dll` messages
13. ☐ Sound archives
14. ☐ Reader archives (`zrdr.zbd`)
15. ◐ Texture archives

### Wave C, large formats

21. ☐ Anim archives (`cam_anim.zbd`, `mis_anim.zbd`)
22. ☐ GameZ and `planes.zbd`

### Wave D, ROF and disc sources

31. ☐ ROF half read in place
32. ☐ ISO9660 reader
33. ☐ LZX CAB decoder and the `rtextureN` long names
34. ☐ Optional copy into the data root

### Wave E, cutover

41. ☐ Source picker and upgrade cleanup
42. ☐ Dev-only `--dump`
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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

## A3 ☐ Session game-data seam and in-memory synthetic models

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
<TODO: whether the synthetic fixtures under `CSVM.Tests/fixtures/` stay as JSON read by the
legacy oracles into models, or become code.>

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

**Verified.** <pending orchestrator run>

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

## B12 ◐ `strings.dll` messages

**Reader landed, engine switch held.** `Messages.Load(IGameFileSource?)` and
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

**Verified.** <pending orchestrator run>

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

## B13 ☐ Sound archives

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

## B14 ☐ Reader archives (`zrdr.zbd`)

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

## B15 ◐ Texture archives

**Reader landed, engine switch pending.** `TextureZbd.Read(byte[])` and
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

**Remaining switch work (after A3).**

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

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

## C21 ☐ Anim archives (`cam_anim.zbd`, `mis_anim.zbd`)

**Goal.** `CompiledAnim` builds `AnimDefinition`s from the 61 anim archives directly.

**Evidence (confidence: lead-only).** `CompiledAnim.cs:81,120,147-151`; Rust `anim` 8,369 (CS 2,111),
`anim-events` 11,598, `anim-names` 3,897; six NaN deltas carried raw; `docs/formats/anim-definitions.md`
and `compiled-archives.md`.

**Approach.** Split by event family if the size demands it. <TODO: a sub-item split, decided by
the implementer after reading `anim-definitions.md`.>

**Model recommendation.** high.

**Verify.** A2 diff clean over all 61 archives; complete battery; 8-chapter regression.

**⚠ Traps.** NaN deltas compare by bits, not by value. The six NaN deltas of C5/M02
`carneypkup_cam.zan;camera1` reach today's model as `+0.0`, because `SiVectorChannel.Parse` reads
`delta` and never `delta_raw`; under Decision 1 the reader reproduces `+0.0` there (A2's finding).
Stderr notes such as "anim def duplicate
anim ref" (`ZbdPlan.Classify`) mark data quirks the reader must tolerate the same way.

## C22 ☐ GameZ and `planes.zbd`

**Goal.** `GameZ` builds its node graph for every chapter and for `planes.zbd` from the game bytes.

**Evidence (confidence: lead-only).** `GameZ.cs:108-123`; Rust `gamez` 20,228 (CS 5,127) and
`nodes` 7,626; `docs/formats/gamez.md`, `world-structure.md`. The per-chapter image-pointer tables
are taken to serve only repack (decision 14), not verified.

**Approach.** Readers into `GameZNode` and its children; skip the pointer tables; model diff;
delete the JSON and v0.6.1 shape paths. <TODO: a sub-item split (nodes, meshes, materials,
planes).>

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

## D32 ☐ ISO9660 reader

**Goal.** An `.iso` file is a game-file source.

**Evidence (confidence: lead-only).** The Rust walks the primary volume with 8.3 names only
(`disc.rs:87-155`).

**Approach.** Primary-volume directory walk into the A1 source interface.

**Model recommendation.** medium.

**Verify.** Every non-CAB file the engine reads opens from the ISO with the same bytes as from a
copy.

**⚠ Traps.** Names are 8.3 on the disc; the long `rtextureN` names come from D33's table.

## D33 ☐ LZX CAB decoder and the `rtextureN` long names

**Goal.** Chapter `.CAB` files on a disc or ISO unpack in memory per session, and their files
appear under the names an install uses.

**Evidence (confidence: lead-only).** CABs are LZX (`cab` crate, `crates/unzbd/Cargo.toml`); the
long-name table is per chapter (`disc.rs:177-203`); no native .NET LZX decoder.

**Approach.** Own CAB + LZX decoder in C#. In-memory per session (decision 15). <TODO: whether a
compatible-licence C# LZX implementation exists to draw on; measure decompression time per
chapter.>

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

## E42 ☐ Dev-only `--dump`

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
