# Tooling — the extraction pipeline, the launch scripts, and the fork

Everything *around* the project rather than in it: how game files become `extracted/`, how the
game gets launched, and how the mech3ax fork is maintained. None of it changes often, and none of
it needs to be in context to write engine code — which is why it lives here rather than in
CLAUDE.md.

For *which* archive types extract and how far each is validated, see
[formats/extraction.md](formats/extraction.md). For the engine's own CLI flags, see [cli.md](cli.md).

## `extracted/` — the extraction workdir (git-ignored)

Populated by `ExtractAssets.ps1`, mirroring the game's own ZBD folder structure: top-level
`planes.zip` (unzbd of `planes.zbd`), `zrdr.zip`, `soundsh.zip`/`soundsl.zip`, `interp.json`,
`rimage.zip`, plus per-chapter `C1/gamez.zip`, `C1/texture.zip`, `C1/rtexture*.zip`, `C1/zrdr.zip`,
and per-mission `C1/IA1/zrdr.zip`.

The viewer's defaults read `planes.zip`, `C1/gamez.zip`, `C1/texture.zip`, `zrdr.zip` and
`soundsh.zip` — but for each default it **prefers the unpacked sibling folder when present** (it
reads `extracted/C1/texture/` over `C1/texture.zip`). So running `ExtractAssets.ps1 -Unzip`, or
unzipping just the archives you want to grep in the editor, makes the viewer load loose files and
skip zip decompression. All four loaders (`GameZ`, `TextureArchive`, `Zrdr`, `SoundArchive`) accept
a zip or a directory; an explicit `--gamez=`/`--textures=`/`--zrdr=`/`--sounds=` is used verbatim.

**The `rtexture*`/`rimage` archives are NOT loaded, by design** (verified Run-2 item 2): each
`rtextureN.zip` is a **downscaled quality tier** of the same texture set, not a hi-res replacement.
Measured across all 896 C5 textures: `texture` == `rtexture14` (both max-res), `rtexture2` = ¼,
`rtexture4/6/8` = ½, and no rtexture file ever exceeds `texture` — so the base `texture.zip` the
viewer loads is already the highest resolution available. `rimage.zip` is the UI/HUD image set
(crosshairs, buttons, cursor, menu splash, briefing thumbnails), with no world geometry textures.

**`extracted/rof/`** is produced by the separate `ExtractRof.ps1`, not `ExtractAssets.ps1`, and
holds the unpacked `.rof` UI archives plus `ui_strings.json`. `PatternLibrary` reads the paint
patterns out of it (`--rof=`, default `extracted/rof`).

## `ExtractAssets.ps1` (repo root) — the ZBD bulk extractor

Walks `CrimsonSkiesGame/ZBD` and runs `unzbd cs <mode>` on every ZBD with the right mode for its
type, writing output to the mirrored relative path under `extracted/` (basename kept, extension →
`.zip`, or `.json` for interp):

| Source | Mode | Output |
|---|---|---|
| `interp.zbd` | `interp` | `.json` |
| `planes.zbd`, `gamez.zbd` | `gamez` | `.zip` |
| `soundsh`/`soundsl` | `sounds` | `.zip` |
| `zrdr.zbd` | `reader` | `.zip` |
| `rimage`, `texture`, `rtexture*` | `textures` | `.zip` |
| `cam_anim`, `mis_anim` | `anim` | `.zip` |

**Extracts with the fork build since 2026-07-21** (`tools/mech3ax/target/release/unzbd.exe`).
`-Unzbd <path>` overrides it — e.g. back to the pinned `tools/mech3ax-v0.6.1-.../unzbd.exe`, which
needs **no code change**, because the Godot loaders read either extraction shape (see
`GameZ.cs` in [architecture.md](architecture.md)).

Idempotent: skips outputs newer than their source unless `-Force`. `-Unzip` also expands each
`.zip` into a sibling folder; `-Source`/`-Dest` override the roots.

Two output-handling details worth knowing before touching the script:

- unzbd's stderr is captured and judged **by exit code**, because PowerShell 5.1 turns a native
  exe's stderr into terminating errors under `$ErrorActionPreference = "Stop"`.
- mech3ax's `object3d transform fail` notes — one per node whose euler angles don't recompose to
  the stored matrix bit-for-bit, informational since the matrix itself is preserved and preferred —
  are counted and summarised rather than printed (155 on a full run).

**`messages.json` is not produced by this script.** It comes from
`unzbd cs messages CrimsonSkiesGame/strings.dll`.

## `ExtractRof.ps1` (repo root) — the non-ZBD half

Added 2026-07-20 for everything `ExtractAssets.ps1` doesn't cover: the `.rof` UI resource archives
(`GOSDATA/ASSETS/crimson.rof` plus the `crimptch.rof` patch overlay) and the
`langui.dll`/`language.dll` Win32 string tables, all into `extracted/rof/`.

It writes every archive member at its archive path, decodes each custom `.BM` texture to
`<name>.png` (the greyscale shading map) and `<name>_mask.png` (**the paint region masks** — R/G/B
= paint slots 1/2/3), and emits `ui_strings.json`, every UI string joined to its `RESOURCE.H`
symbol. That last file is where the aircraft names and description text live.

`-Raw` skips the decoding and the string table; `-Force` re-runs an up-to-date extraction;
`-Source`/`-Dest` override the roots. The decode work is an inline C# type (`Add-Type`), so a full
run is ~1.5 s.

Formats: [formats/rof.md](formats/rof.md), [formats/strings.md](formats/strings.md).

## Launch scripts

**`RunGame.ps1` — the play entry point.** `dotnet build`, then Godot with **no user args**, so the
in-game launchscreen (Mode → Chapter → Plane; `src/UI/LaunchMenu.cs`) shows. Any args you pass are
forwarded verbatim, so an explicit content arg (`--fly`/`--stunt`/`--plane=`/`--chapter=`/
`--screenshot=`) bypasses the launchscreen and builds directly. No console prompts.

**`RunDev.ps1` — the dev helper**, same build step but with console prompts. No args = interactive
console menus (plane roster + chapter), then `--fly`. `--fly` or extra flight flags prompt for
whatever is missing; bare `--plane=X` / `--chapter[=X]` static views pass through promptless;
`--damage[=…]` is its own static flow, prompting only for the plane and never adding
`--fly`/`--chapter` (combined with an explicit one it passes through verbatim and the viewer
ignores it with a note).

**`RunTests.ps1` — the verification entry point.** One command, one summary block, one exit code.
Stages, in order, each reported `PASS` / `FAIL` / `SKIP` / `TODO`:

| Stage | What it runs |
|---|---|
| `build` | `dotnet build CSVM/CSVM.sln`. A failure stops the run — nothing downstream can say anything about a tree that does not compile |
| `units` | `dotnet test CSVM/CSVM.sln` (the `CSVM.Tests` xUnit project), `--no-build` since the build stage just produced the binaries. Counts are read from a TRX log in `.scratch/testresults/`, never scraped from the localized console summary |
| `engine` | Godot with `--run-tests` — windowed (never `--headless`: no shaders compile there, so a clean error screen would prove nothing — rule 82) and with `--log-file .scratch/run-tests-engine.log`, which is what lets the harness screen native engine `ERROR:` lines. `--run-tests` implies `--det` by itself. Verdict from the process exit code; counts and the failing suite names from `.scratch/test-report.json`, which is deleted before the run so a dead run cannot be scored from the last one's numbers |
| `goldens` | Not implemented — reports `TODO`, so the missing golden-image tripwire is visible rather than assumed |
| `perf` | `-Perf` only, and also `TODO` |

Switches: **`-Filter <substring>`** (engine suite names only — `-Filter weapons` runs `weapons-defs`
+ `weapons-fire`; the unit tests are unaffected), **`-SkipUnits`**, **`-SkipEngine`**, **`-Perf`**.

**Exit-code contract: 1 if any stage FAILED, 0 otherwise — and a skip is not a failure.** No game
data, no Godot, `-SkipUnits`/`-SkipEngine` all report `SKIP` and keep the run at 0, but every one of
them prints a `not checked:` line and the summary names what went unmeasured: "the data was not
there" must never read as "the check held". The `TODO` rows do the same for the two unwritten
stages.

**Worktrees.** Extracted data is found through `CSVM_DATA_ROOT` by the engine and the unit tests
alike, and Godot is resolved this tree first then `CSVM_DATA_ROOT` (as `RunGame.ps1` does), so
`$env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'; .\RunTests.ps1` from a git worktree behaves identically
to the primary tree. Without it a worktree still builds and runs the units; the engine stage reports
`SKIP` with the Godot path it looked at. Note the unit tests fall back to their own checkout when
`CSVM_DATA_ROOT` names a directory holding no extraction, so pointing it at an empty folder skips
the engine suites but *not* the data-dependent units — from the primary tree they still find
`extracted/`.

Stray Godots are killed before the engine stage, **filtered to this tree's project dir AND
`--run-tests`** (rule 66): a `--run-tests` run always quits by itself, so one still alive is stuck
and ours, while any other Godot on this tree — a live playtest, another agent — is reported and left
alone.

**All three scripts set `SDL_JOYSTICK_DIRECTINPUT=0`**, respecting a pre-set value — the
controller-disconnect freeze workaround (2026-07-19). Godot's bundled SDL hangs the main thread
forever when a >255-button DirectInput device disconnects (the 8BitDo Ultimate 2 dongle is one);
disabling the dinput backend removes those phantom views, and real pads keep working via
XInput/HIDAPI. Direct editor or exe launches don't get the workaround. Removal conditions are in
`backlog.md` under "Drop the `SDL_JOYSTICK_DIRECTINPUT=0` workaround".

## `tools/` (git-ignored)

Downloaded binaries: mech3ax v0.6.1 (the pinned pre-fork extractor, kept for rollback), the
mech3ax fork checkout (below), and the Godot 4.7 .NET editor at
`tools/godot/Godot_v4.7-stable_mono_win64/` — use `*_console.exe` for CLI runs.

## The mech3ax fork (`tools/mech3ax/`)

**Crimson Skies support lives in the fork, not upstream** (decided 2026-07-22). Upstream removed it
because they could not maintain it — bandwidth, not architecture — so the fork is its home, and
upstream commits get merged *into* the fork if they appear.

Upstream is **dormant**: its tip is `cbb838f` (rc3, 2025-11-17), and `upstream/main` was 0 commits
ahead of the fork's `main` when last checked (2026-07-22). Syncing is a check-occasionally, not a
routine.

- **Remotes:** `origin` = `git@github.com:Laeresh/mech3ax.git`,
  `upstream` = `https://github.com/TerranMechworks/mech3ax.git`.
- **Branches, all three pushed to `origin`** — the fork is the authoritative copy, not this
  workstation: `cs-anim` = integration, and what the release binary is built from;
  `pr-cs-anim` / `pr-cs-gamez` = the same work split by concern; `main` = an upstream mirror.
- **Sync:** `git fetch upstream && git merge upstream/main` into the fork, then rebuild the release
  binary `ExtractAssets.ps1` uses (`target/release/unzbd.exe`) and re-extract if anything in the
  output shape moved.

The shelved upstream-contribution package is archived at [plans/upstream-pr/](plans/upstream-pr/) —
kept because its PR bodies are the best description of what each branch actually contains.
