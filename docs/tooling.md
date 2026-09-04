# Tooling — the extraction pipeline, the launch scripts, and the fork

How game files become `extracted/`, how the game gets launched, and how the mech3ax fork is
maintained. For *which* archive types extract and how far each is validated, see
[extraction](formats/extraction.md); for the engine's own flags, [cli.md](cli.md).

## `extracted/` — the extraction workdir (git-ignored)

Populated by `ExtractAssets.ps1`, mirroring the game's own ZBD folder structure: top-level
`planes.zip` (unzbd of `planes.zbd`), `zrdr.zip`, `soundsh.zip`/`soundsl.zip`, `interp.json`,
`rimage.zip`, plus per-chapter `C1/gamez.zip`, `C1/texture.zip`, `C1/rtexture*.zip`, `C1/zrdr.zip`,
and per-mission `C1/IA1/zrdr.zip`.

The viewer's defaults read `planes.zip`, `C1/gamez.zip`, the chapter's top texture tier (below),
`zrdr.zip` and `soundsh.zip`, **preferring the unpacked sibling folder when present**
(`extracted/C1/rtexture15/` over `C1/rtexture15.zip`). Each loader takes a zip or a directory, and
an explicit `--gamez=`/`--textures=`/`--zrdr=`/`--sounds=` wins.

**Chapter textures load from the top `rtextureN` tier, falling back to `texture.zip`**
(`SessionPaths.ChapterTextures`), where `N` is a **size budget in MB of video-card texture
memory**. ⚠ **The tiers are not mere downscales of `texture.zbd`, which a resolution comparison
misses**: the top tier holds the same file set at the same dimensions, but hundreds of those files
differ in pixel content, and the tier copies are richer, never worse (see
[formats/hud.md](formats/hud.md) on the gauge needle). `rimage.zip` is not loaded at all.

**`extracted/rof/`** is produced by the separate `ExtractRof.ps1` and holds the unpacked `.rof` UI
archives plus `ui_strings.json`. `PatternLibrary` reads the paint patterns out of it (`--rof=`,
default `extracted/rof`).

## `ExtractAssets.ps1` (repo root) — the ZBD bulk extractor

Walks `CrimsonSkiesGame/ZBD` and runs `unzbd cs <mode>` on every ZBD with the right mode for its
type, writing to the mirrored relative path under `extracted/`, basename kept:

| Source | Mode | Output |
|---|---|---|
| `interp.zbd` | `interp` | `.json` |
| `planes.zbd`, `gamez.zbd` | `gamez` | `.zip` |
| `soundsh`/`soundsl` | `sounds` | `.zip` |
| `zrdr.zbd` | `reader` | `.zip` |
| `rimage`, `texture`, `rtexture*` | `textures` | `.zip` |
| `cam_anim`, `mis_anim` | `anim` | `.zip` |

**Extracts with the fork build** (`tools/mech3ax/target/release/unzbd.exe`); `-Unzbd <path>`
overrides it, back to the pinned `tools/mech3ax-v0.6.1-.../unzbd.exe` for instance, which needs
**no code change**, because the loaders read either extraction shape. Idempotent: skips outputs
newer than their source unless `-Force`. `-Unzip` also expands each `.zip` into a sibling folder,
which is what makes the viewer read loose files; `-Source`/`-Dest` override the roots.

Every failure-free run stamps `<Dest>/VERSION.json` with the `unzbd --version` line verbatim, the
exe's SHA-256, the fork HEAD, the date, and a hand-bumped schema integer the engine
compares at boot (`src/Session/ExtractionStamp.cs` warns, never blocks), bumped by any reader
change that invalidates old extractions.

**`messages.json`** comes from a step after the walk, since `strings.dll` sits at the install root
outside it: `unzbd cs messages` into `<Dest>\messages.json`, skipped with a note when the DLL is
absent, in which case the engine falls back to raw `MSG_*` keys.

## `ExtractRof.ps1` (repo root) — the non-ZBD half

Covers the `.rof` UI archives (`GOSDATA/ASSETS/crimson.rof` plus the `crimptch.rof` patch overlay)
and the `langui.dll`/`language.dll` string tables, all into `extracted/rof/`. It writes each member
at its archive path, decodes each `.BM` texture to `<name>.png` (the greyscale
shading map) and `<name>_mask.png` (**the paint region masks**, R/G/B = paint slots 1/2/3), and
emits `ui_strings.json`, every UI string joined to its `RESOURCE.H` symbol.

It also emits **`menu_layout.json`**, the decoded `LAYOUT.CSV` screens, widgets, navigation edges,
script-created widget keys, out-of-layout art, `SCRAPBOOK.CSV` and patch-overlay precedence, which
runtime reads instead of the originals. Its decoder, **`ExtractRof.MenuLayout.cs`**, is `Add-Type`d
from disk and compiled by `CSVM.Tests` as well, so it stays inside the C# 5 subset.

`-Raw` skips the decoding, the string table and the menu layout; `-Force` re-runs an up-to-date
extraction; `-Source`/`-Dest` override the roots. Each run merges its own `rof` field into the
shared `VERSION.json` one level above `-Dest`, and skips that stamp with a note unless `-Dest`
follows the canonical `…\extracted\rof` layout. Formats: [rof](formats/rof.md),
[strings](formats/strings.md), [menu layout](formats/menu-layout.md).

## `packaging/Extract.ps1` — the friend-facing dispatcher

Ships in the release zip (`packaging/MANIFEST.md`), never used in the dev tree. It takes the
recipient's install root, checks `ZBD` and `GOSDATA\ASSETS` exist with a friendly error, and
dispatches to the two UNMODIFIED scripts above with `.\` paths anchored to `$PSScriptRoot`. **Keep
all extraction logic in the two scripts only.** Its one post-step unpacks `rimage.zip` into
`extracted\rimage\`, because the HUD-font and reticle loaders read loose PNGs there.

## Launch scripts

**`RunGame.ps1` — the play entry point.** `dotnet build`, then Godot with **no user args**, so the
launchscreen (`src/UI/LaunchMenu.cs`, Mode → Chapter → Plane) shows. Args are forwarded verbatim,
so a content arg (`--fly`/`--stunt`/`--plane=`/`--chapter=`/`--screenshot=`) bypasses it.

**`RunDev.ps1` — the dev helper**, same build step but with console prompts: no args gives
interactive menus (plane roster, chapter) and then `--fly`, flight flags prompt for what is
missing, and the static views (`--plane=`, `--chapter=`, `--damage=`) pass through promptless
apart from `--damage=`'s own plane prompt.

**`RunTests.ps1` — the verification entry point.** One command, one summary block, one exit code.
Stages, in order, each reported `PASS` / `FAIL` / `SKIP` / `TODO`:

| Stage | Runs / reads its verdict from / fails on |
|---|---|
| `build` | `dotnet build CSVM/CSVM.sln`; its exit code; a compile error, which also stops the run |
| `units` | `dotnet test --no-build`; the TRX log in `.scratch/testresults/`, never the console summary; any failed test |
| `engine` | Godot `--run-tests` in `-Shards` processes, windowed (LOG-8); each shard's JSON report; a failed suite, a missing report, a watchdog timeout (exit 124) |
| `goldens` | One `--det` Godot per manifest shot; the raw-pixel md5 on its `[core] shot pixmd5=…` line, not the PNG bytes (SHOT-6); a hash, frame or size off the manifest |
| `perf` | `-Perf` only: `analysis/perf/scenarios.json`; the `[perf]` lines, medianed into `perf-history.jsonl`; nothing, it records only |
| `hitch` | `-Hitch` only, last: two scripted launches; each launch's `.hitches.jsonl` sidecar; nothing, awareness only |

Switches: **`-Suite <name>[,<name>]`** (exact in-engine suite names), **`-Filter <substring>`**
(engine suite names), **`-UnitFilter <expr>`** (into `dotnet test --filter`), **`-Shards <n>`**,
**`-Quick`**, **`-SkipUnits`**, **`-SkipEngine`**, **`-SkipGoldens`**, **`-RegenGoldens`**,
**`-GoldenWorkers <n>`**, **`-Hitch`**, **`-SkipHitch`**, **`-Perf`** (+ `-PerfLabel`,
`-PerfCompare`, `-PerfFilter`, `-PerfIterations`, `-PerfFrames`), and **`-Graphics
original|enhanced`** (default `original`, which appends nothing; `enhanced` appends
`--graphics=enhanced` to the perf and hitch launches only).

**Every stage prints its wall time against a budget, and a budget never fails a run** (PERF-18).
The numbers live in `analysis/verification-budgets.json`, one lane for the complete gate and one
for `-Quick`, and live nowhere else so they cannot drift; each is the slowest of three
back-to-back warm runs plus 50 %. A skipped stage is compared against nothing, and the total only
when its lane's stages all ran.

**A selection that matches nothing is a failure**: `-Suite`, `-Filter` and `-UnitFilter` each fail
their stage naming the term, rather than reporting a green zero.

**`-Quick` is the broad partial gate**: build, the quick unit tier (`--filter Tier=Quick`), the
quick engine tier (`--run-tests=tier:quick`), no goldens and no hitch. Membership of both tiers is
checked in, as the `[Trait("Tier", "Quick")]` classes and `SuiteCatalog.QuickTier`. An explicit
`-Suite`/`-Filter` unions with the engine tier and a `-UnitFilter` replaces the unit tier. Quick
prints a `not checked:` line per omitted surface, and never satisfies the landing gate.

**The engine stage runs the full catalog in concurrent Godot processes.** `-Shards <n>` sets how
many; the default is 4 for a full run and 1 whenever `-Suite`/`-Filter`/`-Quick` names a selection,
and `-Shards 1` is the serial reference path. Membership comes from the harness's
`shard:<index>/<count>` term over the per-suite weights at `analysis/engine-suite-weights.json`, so
the same tree always divides the same way; an unweighted suite is charged the default and printed
as `not checked:`. Regenerate that file from a warm `-Shards 1` run's report.

Each shard gets its own log, streams, report and artifacts under `.scratch/engine/owner-<pid>/`;
what that does not isolate is a suite whose store sits outside `.scratch/`, so overlapping runs are
not safe (LOG-13). **The stage's verdict is the merge of every shard's report**, in registry order
via each suite's `index`: counts sum, the error allowlist's caps are re-checked against the SUMMED
counts, and **a shard exiting 0 with no report FAILS the stage**.

**`test-report.json`'s schema is versioned** (`"schema"`, bumped when a field changes meaning or
goes). Besides the per-suite rows and their registry `index`, it holds a `binary` block
(the loaded `CSVM.dll`'s path and MD5), the run's `selector`, a `shard` block and a `phaseTotals`
block splitting wall time into world build, disposal and the rest, so a report can be matched to
its build and the shard merge proved complete. Read the current field list from a report.

**The golden stage is scripted, not an in-engine suite**, because the `--run-tests` harness runs
every suite inside one `_Ready` call without yielding a frame, so no suite there can photograph
anything. A mismatch leaves the actual PNG and that shot's log in
`.scratch/goldens/<shot>.{png,log}`. **`-RegenGoldens`** re-renders every shot and rewrites
`manifest.json` in place, byte-identically apart from the hash lines that moved; GOLD-1 says when
that is the right answer.

**Golden shots launch concurrently, `-GoldenWorkers` of them at a time (default 4)**, in
registry-order batches, each with its own watchdog, process, log and PNG. The default is
the fastest worker count that stayed bit-identical on the one machine measured, and
**`-GoldenWorkers 1`** is the serial reference path.

**The hitch stage is opt-in (`-Hitch`), not part of the landing gate**, because it never changes
the exit code: run it when landing a change to `HitchMonitor.cs`, `HitchSidecar.cs` or the hitch
tick in `Launcher.cs`. **`-SkipHitch`** forces it off even when `-Hitch` is given, and `-Quick`
never runs it. A clean `--frames=180` launch should
stay silent and `--hitch-inject=50@300 --frames=310` should trip once on frame 300; it runs last so
its evidence is never taken beside another stage's load (LOG-13, PERF-12/13/14).

### The perf stage (`-Perf`)

**A duration here is a count of SIM frames, never wall seconds.** Each scenario runs
`--det --perf --no-vsync --mute` plus `--frames=N --screenshot=`, and the saved-shot line prints
`sim_frame=N` so the count is proved rather than assumed. `--perf` reports
a `[perf] window …` line per 60 rendered frames and a `[perf] startup …` line that closes
arithmetically over the build; the script parses both.

**Scenario set** (`analysis/perf/scenarios.json`, whose `args` are the literal command a human
re-runs): `empty-stage` (no gamez, the control a world or clutter change must leave alone),
`c1-flight` (the only moving camera), `c2b-water`, `c4-terrain` and `c5-city`. Defaults are 300 sim
frames × 3 launches, overridden by `-PerfFrames` and `-PerfIterations`; `-PerfFilter` narrows the
set.

**Protocol and verdict.** The first launch of each scenario is discarded, because a cold file cache
*reshapes* a startup profile instead of scaling it (PERF-7), and each kept launch drops its first
window, which carries shader compilation. The record stores medians plus commit, `--dirty` flag,
GPU string, the launch's `"vsync"` mode and the **md5 of the loaded `CSVM.dll`** (METHOD-6).
**A paired A/B is the only verdict**: `-PerfLabel base`, flip the one line under test (METHOD-5),
rebuild, then `-PerfLabel change -PerfCompare base` prints the ratios, calling identical hashes a
noise floor.

**What is read and what is refused.** The verdict metrics are `render_cpu_ms`, `gpu_ms`, the counts
`draws` / `prims` / `nodes` and the startup phases, and a row is marked `*` only when it clears
**both** a relative band and an absolute floor, each this machine's own noise (PERF-9…PERF-11).
Every other recorded metric prints as awareness only, each because its own instrument is unfit for
a ratio (PERF-1, PERF-2, PERF-21), with the reason in the manifest's `notes`.

**Exit-code contract: 1 if any stage FAILED, 0 otherwise, and a skip is not a failure.** A missing
dependency and `-SkipUnits`/`-SkipEngine` report `SKIP` at 0 with a `not checked:` line, so "the
data was not there" never reads as "the check held". `-RegenGoldens` reports `REGEN`, never `PASS`.

**Worktrees.** Extracted data and Godot are both found through `CSVM_DATA_ROOT`, so
`$env:CSVM_DATA_ROOT = 'Z:\CSVM'; .\RunTests.ps1` from a worktree behaves identically to the
primary tree; without it the engine stage reports `SKIP` with the path it looked at. The unit tests
fall back to their own checkout, so a root holding no extraction skips the engine suites but *not*
the data-dependent units. The other variable a scripted run may want is
`CSVM_TRACE_SISCRIPT=<node name>`, which makes `ScriptPlayback` log a `sitrace` line in the `anim`
category with that node's per-step pose, `dt` and step distance every tick.

Stray Godots are killed before the engine, golden, hitch and perf stages, **filtered to this tree's
project dir AND an argument only that stage's own launches carry** (SHELL-2): `--run-tests`, or the
`.scratch\goldens\` / `.scratch\hitchcheck\` / `.scratch\perf\` output paths. All quit by
themselves, so one still alive is stuck and ours; any other Godot here is left alone.

**All three scripts set `SDL_JOYSTICK_DIRECTINPUT=0`**, respecting a pre-set value, as the
controller-disconnect freeze workaround: Godot's bundled SDL hangs the main thread forever when a
>255-button DirectInput device disconnects, and disabling that backend removes the phantom views
while real pads keep working via XInput/HIDAPI. Removal conditions are in `backlog.md` under "Drop
the `SDL_JOYSTICK_DIRECTINPUT=0` workaround".

## Exporting a release build

`CSVM/export_presets.cfg` (committed) holds one preset, **"Windows Desktop"**: release export,
x86_64, `embed_pck=true`, so a single `CSVM.exe` with the pck inside, plus the **self-contained**
.NET publish output beside it as `data_CSVM_windows_x86_64/`. The exported build reads `extracted/`
and writes `.scratch/logs/` beside the exe, resolving every root to the exe's own folder.

**One-time template install.** The Godot export templates are user-global, not part of the repo's
pinned editor: extract the inner `templates/` FILES of `tools/godot-4.7-mono-export-templates.tpz`
into `%APPDATA%\Godot\export_templates\4.7.stable.mono\` (create the dir; do not keep the
`templates/` level).

**`ExportRelease.ps1` (repo root)** takes no parameters and runs the whole sequence: it checks the
export templates, the fork-built `tools\mech3ax\target\release\unzbd.exe` and the rest of the
payload exist (named errors up front), empties `.scratch\export\`,
builds, imports headless, exports, copies the payload in beside the output, and zips the folder to
`.scratch\CSVM.zip`. That folder is cleared because all of it is zipped, and the clear refuses to
run if it holds a junction, since PowerShell 5.1's recursive delete follows one into its target.
The zip is built through `System.IO.Compression`, since `Compress-Archive` reports success after
writing nothing when a single file is locked.

The payload is `packaging/MANIFEST.md`'s table, copied from its repo sources on every export, which
keeps it byte-identical. Two filters in the preset are required.
`include_filter="data/*.json"` keeps `stock_loadouts.json`/`effect_pools.json`, non-imported
resources the default export silently drops, without which every plane flies unarmed; their loaders
read them through `Godot.FileAccess` rather than `GlobalizePath` plus System.IO, which is what
makes the pck copies reachable. ⚠ `exclude_filter="config.json"` keeps the dev box's personal
tuning override out of every build. Smoke-test an export from a **bare folder** with a **foreign
CWD** and no `CSVM_DATA_ROOT`; either can mask a broken default root.

## `tools/` (git-ignored)

Downloaded binaries: mech3ax v0.6.1 (pinned pre-fork extractor, for rollback), the fork checkout
(below), and the Godot 4.7 .NET editor at `tools/godot/Godot_v4.7-stable_mono_win64/`; use
`*_console.exe` for CLI runs.

## The mech3ax fork (`tools/mech3ax/`)

**Crimson Skies support lives in the fork, not upstream.** Upstream removed it, so the fork is its
home and upstream commits get merged *into* the fork. Upstream is dormant, so syncing is a
check-occasionally rather than a routine.

- **Remotes:** `origin` = `git@github.com:Laeresh/mech3ax.git`,
  `upstream` = `https://github.com/TerranMechworks/mech3ax.git`.
- **Branches, all three pushed to `origin`**, since the fork and not this workstation is the
  authoritative copy: `cs-anim` = integration, and what the release binary is built from;
  `pr-cs-anim` / `pr-cs-gamez` = the same work split by concern; `main` = an upstream mirror.
- **Sync:** `git fetch upstream && git merge upstream/main` into the fork, then rebuild
  `target/release/unzbd.exe` and re-extract if the output shape moved.

The shelved upstream-contribution package is archived at [upstream-pr/](upstream-pr/).

### Window focus: scripted runs stay out of your way

The game window is **created without focus** (`display/window/size/no_focus` in `project.godot`),
and a scripted session also **hides its window** through `ScriptedWindow.Hide` once
`Launcher._Ready` knows the flags, which leaves rendering untouched where minimizing would stop it
(SHELL-13, SHOT-16). A hand launch still shows a brief flash, because the window exists before
`_Ready` can run, so `RunTests.ps1` instead runs **every launch on a separate Windows desktop**
(`HiddenDesktop.ps1`: `CreateDesktop`, then `CreateProcess` with `STARTUPINFO.lpDesktop`), which a
window can only join through the process that started it, so placement is decided *before* the
process runs. The summary line names the desktop used, and a refused desktop continues
visibly rather than failing; the rejected alternatives are in the retired `analysis/hidden-desktop/`
(`git show analysis-archive:analysis/hidden-desktop/FINDINGS.md`).

`RunGame.ps1` and `RunDev.ps1` hand the foreground to the new window themselves, finding it by pid
via `EnumWindows` (SHELL-13). Every `RunTests.ps1` stage launches through its private
`Invoke-Godot` helper, which uses the non-console binary and redirects both streams to
`<its --log-file>.out` / `.err` (SHELL-10).

**`RunProbe.ps1` — the same launch for ad-hoc runs.** A hand-launched probe would otherwise inherit
both the window flash and the console scribble a bare `& $GodotExe …` sprays over the calling
terminal (SHELL-10): **never invoke the Godot binary directly for a scripted run, go through
`.\RunProbe.ps1 <user args>`.** It forwards every argument verbatim (no build step, so build
first), runs on its own hidden desktop (`csvm-probe`, falling back to a visible but still
redirected run if the OS refuses one), parks the streams beside the run's `--log-file` or in
`.scratch/logs/probe-<stamp>.out/.err`, and exits with Godot's code. Its one switch is
**`-TimeoutSec`** (default **300**, `0` = wait forever), which kills the run and exits **124**, so
a probe that never quits cannot hang a session.
