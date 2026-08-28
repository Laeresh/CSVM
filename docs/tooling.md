# Tooling — the extraction pipeline, the launch scripts, and the fork

Everything *around* the project rather than in it: how game files become `extracted/`, how the
game gets launched, and how the mech3ax fork is maintained. None of it changes often, and none of
it needs to be in context to write engine code — which is why it lives here rather than in
PROJECT_CONTEXT.md.

For *which* archive types extract and how far each is validated, see
[formats/extraction.md](formats/extraction.md). For the engine's own CLI flags, see [cli.md](cli.md).

## `extracted/` — the extraction workdir (git-ignored)

Populated by `ExtractAssets.ps1`, mirroring the game's own ZBD folder structure: top-level
`planes.zip` (unzbd of `planes.zbd`), `zrdr.zip`, `soundsh.zip`/`soundsl.zip`, `interp.json`,
`rimage.zip`, plus per-chapter `C1/gamez.zip`, `C1/texture.zip`, `C1/rtexture*.zip`, `C1/zrdr.zip`,
and per-mission `C1/IA1/zrdr.zip`.

The viewer's defaults read `planes.zip`, `C1/gamez.zip`, the chapter's top texture tier (below),
`zrdr.zip` and
`soundsh.zip` — but for each default it **prefers the unpacked sibling folder when present** (it
reads `extracted/C1/rtexture15/` over `C1/rtexture15.zip`). So running `ExtractAssets.ps1 -Unzip`,
or
unzipping just the archives you want to grep in the editor, makes the viewer load loose files and
skip zip decompression. All four loaders (`GameZ`, `TextureArchive`, `Zrdr`, `SoundArchive`) accept
a zip or a directory; an explicit `--gamez=`/`--textures=`/`--zrdr=`/`--sounds=` is used verbatim.

**Chapter textures load from the top `rtextureN` tier, falling back to `texture.zip`**
(`SessionPaths.ChapterTextures`, 2026-08-04). The `N` in `rtextureN` is a **size budget in MB of
video-card texture memory** (the file sizes give it away: rtexture2 ≈ 1.95 MB, rtexture4 ≈ 3.85,
rtexture6 ≈ 5.7, rtexture8 ≈ 7.65 in every chapter); each chapter ships the 2/4/6/8 tiers plus one
full-quality tier sized to whatever it needs (`rtexture15`/`11`/`10`/`14`/`9`/`12`/`14`/`14` for
C1…C5). ⚠ **The tiers are not mere downscales of `texture.zbd`, and resolution comparison misses
that**: the top tier holds the identical 881-file set at identical dimensions (C1, verified
per-file), but **301 of them differ in pixel content and five in pixel format** — `needle`,
`smallneedle`, `steps`, `tarmac_lines`, `bal_taillogo` are RGBA there and RGB in `texture.zbd` —
and the tier copies are richer (more color levels, painted alpha), never worse. The gauge needle's
tapered-pointer silhouette exists **only** in the tier copies' alpha (see `docs/formats/hud.md`);
the original engine picks a tier by texture memory and plainly renders the tier art, so
`texture.zbd` looks like an older build of the set that the shipped game never draws. An earlier
note here said the rtextures are "not loaded, by design" after measuring C5 *dimensions*
(`texture` == `rtexture14` max-res, `rtexture2` = ¼, `rtexture4/6/8` = ½) — true of resolution,
wrong about content. `rimage.zip` is still not loaded: it is the UI/HUD image set (crosshairs,
buttons, cursor, menu splash, briefing thumbnails), with no world geometry textures.

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

Every failure-free run (including an all-up-to-date one) stamps `<Dest>/VERSION.json` with its
provenance: the `unzbd --version` line verbatim (the fork's version number is frozen — the build
timestamp is what distinguishes binaries), the exe's SHA-256, the fork checkout's HEAD when the
exe sits inside one, the date, and a hand-bumped schema integer the engine compares at boot
(`src/Session/ExtractionStamp.cs` — one warning line on stale/missing/unreadable, never a block).
The schema bumps in the same commit as any reader change that invalidates old extractions; the
extraction output itself is never hashed (gigabytes).

Two output-handling details worth knowing before touching the script:

- unzbd's stderr is captured and judged **by exit code**, because PowerShell 5.1 turns a native
  exe's stderr into terminating errors under `$ErrorActionPreference = "Stop"`.
- mech3ax's `object3d transform fail` notes — one per node whose euler angles don't recompose to
  the stored matrix bit-for-bit, informational since the matrix itself is preserved and preferred —
  are counted and summarised rather than printed (155 on a full run).

**`messages.json`** is produced by a dedicated step after the ZBD walk: `strings.dll` sits at
the install root (the ZBD tree's parent), so the walk never sees it — the script extracts it
with `unzbd cs messages` into `<Dest>\messages.json`, skipping with a note when the DLL is
absent. Without it the engine falls back to raw `MSG_*` keys on the HUD and briefings (how the
gap was found in the first sandbox clean-machine run).

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

Each run also merges its own `rof` field (script, date, `-Raw`) into the shared
`VERSION.json` one level above `-Dest` — read-merge-write, so `ExtractAssets.ps1`'s fields
survive — when `-Dest` follows the canonical `…\extracted\rof` layout; any other `-Dest` skips
the stamp with a note rather than guessing where the shared file lives.

Formats: [formats/rof.md](formats/rof.md), [formats/strings.md](formats/strings.md).

## `packaging/Extract.ps1` — the friend-facing dispatcher

Ships in the release zip (see `packaging/MANIFEST.md`), never used in the dev tree. It takes
one argument — the recipient's Crimson Skies install root — validates `ZBD` and
`GOSDATA\ASSETS` exist with a friendly error, and dispatches to the two UNMODIFIED scripts
above, shipped next to it: `ExtractAssets.ps1 -Source <install>\ZBD -Dest .\extracted
-Unzbd .\tools\unzbd.exe`, then `ExtractRof.ps1 -Source <install>\GOSDATA\ASSETS -Dest
.\extracted\rof` (all `.\` anchored to `$PSScriptRoot`, so the CWD never matters, and the
`rof` dest keeps the canonical shape the VERSION.json stamp requires). **Keep all extraction
logic in the two scripts only** — the dispatcher is path plumbing; a package-only extraction
variant is the divergence trap the plan forbids. Its one post-step: it unpacks the produced
`rimage.zip` into `extracted\rimage\` (`Expand-Archive`, idempotent) — the HUD-font and
gun-reticle loaders read loose PNGs from that folder and have no zip fallback, and the dev
tree only has it unpacked because of a historical `-Unzip` run.

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
| `units` | `dotnet test CSVM/CSVM.sln` (the `CSVM.Tests` xUnit project), `--no-build` since the build stage just produced the binaries. Counts are read from a TRX log in `.scratch/testresults/`, never scraped from the localized console summary. A FAILED units stage does not stop the run — only a failed `build` does — so `engine`, `goldens` and `hitch` still launch and are scored from their own reports; the summary row for `units` still reads `FAIL` |
| `engine` | Godot with `--run-tests` — windowed (never `--headless`: no shaders compile there, so a clean error screen would prove nothing — LOG-8) and with `--log-file`, which is what lets the harness screen native engine `ERROR:` lines. `--run-tests` implies `--det` by itself. The full catalog runs in `-Shards` concurrent processes (below); each launch has its own five-minute watchdog, and a timeout kills that launch, fails the stage with exit 124 and leaves the partial log while the other shards still report. Counts and failing suite names come from the shard reports, each deleted before the run so a dead run cannot be scored from the last one's numbers |
| `goldens` | The golden-image tripwire: one Godot per shot in `analysis/goldens/manifest.json`, each a pinned `--det` capture with `--screenshot=` and `--log-file=` appended, compared as **md5 of the raw pixel buffer** the engine prints on its `[core] shot pixmd5=… size=… gpu=…` line (never the PNG's encoded bytes — SHOT-6). ~53 s for 11 shots |
| `perf` | `-Perf` only: every scenario in `analysis/perf/scenarios.json` under `--det --perf --no-vsync --mute`, medians appended to the git-ignored `perf-history.jsonl`. ~88 s for 5 scenarios. It measures and records; it never judges (below) |
| `hitch` | `-Hitch` only, and always last: two scripted Godot launches reporting `HitchMonitor`/`HitchSidecar` health — a clean `--frames=180` run should stay silent, and `--hitch-inject=50@300 --frames=310` should trip once on frame 300 with a full 120-entry ring and matching sidecar record. Results are awareness-only and never fail the run; off by default even in a full run, and a run without `-Hitch` names its cadence in `not checked:` |

Switches: **`-Suite <name>[,<name>]`** (exact in-engine suite names), **`-Filter <substring>`**
(engine suite names only — `-Filter weapons` runs `weapons-defs` + `weapons-fire`),
**`-UnitFilter <expr>`** (straight into `dotnet test --filter`), **`-Shards <n>`**, **`-Quick`**, **`-SkipUnits`**,
**`-SkipEngine`**, **`-SkipGoldens`**, **`-RegenGoldens`**, **`-Hitch`**, **`-SkipHitch`**, **`-Perf`**
(+ `-PerfLabel`, `-PerfCompare`, `-PerfFilter`, `-PerfIterations`, `-PerfFrames`).

**Selection is exact or substring, and a miss is a failure.** `-Suite` and `-Filter` compose into
the harness's own term grammar on `--run-tests=` (`suite:<name>` exact, `tier:<name>` a checked-in
tier, anything else a substring; terms are comma separated and unioned, and the run keeps registry
order). A term that selects no suite ends the harness before anything runs and fails the stage,
naming the term — an empty selection is never an empty pass. `-UnitFilter` holds the same rule from
the other side: a filter matching zero tests fails the units stage rather than reporting a green
zero. The targeted loops are `.\RunTests.ps1 -Suite <name> -SkipUnits -SkipGoldens -SkipHitch`
(measured ~4 s warm) and `.\RunTests.ps1 -UnitFilter "FullyQualifiedName~<test>" -SkipEngine
-SkipGoldens -SkipHitch` (~2 s).

**`-Quick` is the broad partial gate**: build, the quick unit tier (`--filter Tier=Quick`), the
quick engine tier (`--run-tests=tier:quick`), no goldens and no hitch. Measured 36–40 s warm on the
development machine against a ≤60 s budget. Membership of both tiers is checked in — the
`[Trait("Tier", "Quick")]` classes in `CSVM.Tests` and `SuiteCatalog.QuickTier` — and chosen by the
failure surface each representative can catch, never inferred from a diff or from elapsed time.
`emitter-lifetime` is outside the engine tier because `puffer-modes` covers the emitter runtime end
to end for a fraction of its wall time. An explicit `-Suite`/`-Filter` is unioned with the engine tier, so "the quick lane
plus the suite I am editing" is one command; an explicit `-UnitFilter` replaces the unit tier,
since the VSTest grammar can express a union itself. Quick prints its declared scope before
it starts and a `not checked:` line for every omitted surface; it is partial by construction and
never satisfies the landing rule, which stays the complete run.

**The engine stage runs the full catalog in concurrent Godot processes.** `-Shards <n>` sets how
many; the default is 4 for a full run and 1 whenever `-Suite`/`-Filter`/`-Quick` names a selection,
which is faster started once than started N times. `-Shards 1` is the serial reference path and
stays selectable. Membership comes from the harness's own `shard:<index>/<count>` term over the
measured per-suite weights checked in at `analysis/engine-suite-weights.json` (longest unit first
onto the lightest shard, ties broken on registry position), so the same tree divides the same way
every run and no membership list has to be maintained by hand. The two seven-world censuses are
pinned into one shard by that file's `groups`, because whichever runs second reads its eight
chapters out of the process `DecodeCache` instead of decoding them again. A suite the file does not
name is charged the default weight and printed as a `not checked:` line, so an unmeasured suite
skews the balance visibly rather than silently. Regenerate the file from a warm `-Shards 1` run's
`test-report.json`.

Each shard is isolated by construction: its own `--log-file`, its own `.out`/`.err`, and its own
report and per-suite artifacts in a `shard<i>of<n>` directory beside that log, all under
`.scratch/engine/owner-<pid>/`. The owner pid in that path is also the stray-Godot rule: a
`--run-tests` Godot whose owning PowerShell is gone is a leftover and is killed, while one whose
owner is alive belongs to another run and is reported and left alone (SHELL-2), so a shard can
never kill a sibling. **The stage's verdict is the merge of every shard's report**, in registry
order via each suite's own `index`: counts sum, failed names sort back into registry order, the
error allowlist's caps are re-checked against the SUMMED counts (N processes each under the cap can
still sum past it), every shard must report the same `CSVM.dll` hash, and the shards' suite counts
must add up to the selection each of them reports. **A shard exiting 0 with no report FAILS the
stage** — a process that ran nothing must never read as a green one.

**Two concurrent `RunTests.ps1` invocations isolate everything except a suite's own `user://`
store.** Measured with `-SkipUnits -SkipGoldens -SkipHitch`: logs, reports, artifacts and the
stray-Godot sweep all held, and the second run failed exactly one suite, `campaign-loop`, which
proves persistence ACROSS processes and therefore keeps its profile under `user://Testing/` where
`.scratch/` isolation cannot reach it. The goldens, hitch and perf stages still identify their
strays by output path alone, so they are not concurrency-safe either.

**`test-report.json`'s schema is versioned** (`"schema"`, currently 3 — bumped when a field's
meaning changes or one is removed, the same rule `analysis/goldens/manifest.json`'s own `schema`
follows). Besides the per-suite PASS/FAIL/SKIP rows, it carries a `binary` block (the loaded
`CSVM.dll`'s own path and MD5, the same `$PerfDll` identity `RunTests.ps1`'s perf stage records),
the run's own `selector`, and a `shard` block (this shard's index and count, the whole selection's
size before the division, and any suite the weights file did not name), so a report can be matched
to the exact build and suite set that produced it and a merger can prove the shards covered the
selection exactly once. Each suite row carries its registry `index`, which is what merges the shard
reports back into registry order. A `phaseTotals` block and a matching set of per-suite fields (`worldsBuilt`, `buildSeconds`,
`archiveDecodeSeconds`, `soundPrepSeconds`, `runtimeConstructionSeconds`, `otherBuildSeconds`,
`disposalSeconds`, `restSeconds`, `overrunSeconds`) split every suite's wall time into world-build
(further split into the three phase categories `PLAN-fast-verification.md`'s B11 needs), disposing a
built world, and what is left over (manual simulation plus assertion work) — `docs/architecture.md`'s
`src/Testing/TestHarness.cs` entry has the boundary detail. `overrunSeconds` is nonzero only on a
measurement anomaly (the independent stopwatches summing past the suite's own wall clock); it is
never a correctness verdict.

**The golden stage is a scripted pass, not an in-engine suite, and that is structural**: the
`--run-tests` harness runs every suite to completion inside one `_Ready` call and never yields a
frame, so no suite there can photograph anything. Driving it from the script also makes each shot's
manifest entry the *literal* command a human re-runs by hand. A mismatch fails the stage naming the
shot, and leaves the actual PNG plus that shot's own engine log in `.scratch/goldens/<shot>.{png,log}`
for eyeballing — the shot's frame number and render size are checked separately from its hash, so a
clock or window-size regression reads as itself rather than as "pixels moved". **`-RegenGoldens`**
re-renders every shot and rewrites `manifest.json` in place; the emitter round-trips the file
byte-identically, so the diff is exactly the hash lines that moved. Regeneration is deliberate and
never automatic — see `docs/verification.md` GOLD-1 for when it is the right answer and when it is
covering up a defect, and `analysis/goldens/README.md` for the shot set.

**The hitch stage is opt-in (`-Hitch`), not part of the retained landing gate.** The milestone that
shortened this run names what the full gate retains — build, units, all engine suites, goldens —
and hitch is not on that list, because it never changes the exit code and its own subject changes
rarely: `HitchMonitor.cs`/`HitchSidecar.cs` landed once and have taken exactly one substantive
change since, a queue-size tune found by a controls capture rather than by this stage. Run it
explicitly with `-Hitch` when landing a change that touches `HitchMonitor.cs`, `HitchSidecar.cs`,
or the hitch tick in `Launcher.cs`, or periodically otherwise; a run without `-Hitch` skips the
stage and the summary names that same cadence in its `not checked:` lines. `-Quick` never runs it.
`-SkipHitch` forces it off even when `-Hitch` is given, for a caller that always passes `-Hitch`
and needs to suppress it for one run.

It stays a scripted measurement for the same structural reason the golden stage is one:
`HitchMonitor` only trips on a real rendered frame measured over wall time (ticked from
`Launcher._Process`), and `--run-tests` runs every suite to completion inside one `_Ready` call
without ever yielding a frame, which is not a new exception. Each launch's sidecar path is recovered
from the `"[core] log file=…"` line every session prints once at `Log.Open` (`Log.SinkPath`, the
PROJECT's own log — a different file from Godot's own `--log-file` this stage also passes), then
read back as `<that path minus .log>.hitches.jsonl`. It prints detector failures as awareness items
and never changes the verifier's exit code: workstation contention makes frame-time evidence too
variable to gate unrelated work. The injected record's C8 attribution is checked as
an identity — `attributed_ms + unattributed_ms` must close over `frame_ms`, with no scope violations
— rather than as "no samples": the injected stall is deliberately unscoped, and C9 seeding a
site that fires during this launch must not turn the check red. It is always the LAST stage in a
run, after perf, so its wall-time evidence is never taken beside engine, golden, or perf load
(`docs/verification.md` LOG-13, PERF-12/13/14).

### The perf stage (`-Perf`)

**A duration here is a count of SIM frames, never wall seconds.** Each scenario runs
`--det --perf --no-vsync --mute` plus `--frames=N --screenshot=`, which is what ends the run at
exactly N sim frames — the fixed clock advances one sim step per rendered frame, and the saved-shot
line prints `sim_frame=N` so the count is *proved* rather than assumed. `--perf` reports one
`[perf] window …` line per 60 rendered frames, and C21's `[perf] startup …` line closes
arithmetically over the build; the script parses both.

**Scenario set** (`analysis/perf/scenarios.json` — each entry's `args` are the literal command a
human re-runs): `empty-stage` (no gamez at all — the control: a world, clutter or animation change
must leave it alone), `c1-flight` (the only moving camera: chase cam, animators, HUD, collision
raycasts), `c2b-water` (rain over open water, the lightest world), `c4-terrain` (the heaviest
draw-call pose, ~2180 calls), `c5-city` (the largest clutter build and the `LIGHT_STATE` data
texture). Defaults: 300 sim frames × 3 launches per scenario.

**Protocol.** The first launch of each scenario is discarded — a cold file cache *reshapes* a
startup profile instead of scaling it (PERF-7) — and each kept launch also drops its first perf
window, which carries the first draw's shader compilation. The record stores **medians**: over
every kept window for the frame metrics, over the kept launches for the startup phases, plus commit,
`--dirty` flag, GPU string and the **md5 of the `CSVM.dll` Godot loaded** (METHOD-6: a dirty tree
gives A and B the same commit, so the assembly hash is the only proof the new build ran).

**A/B is the only verdict.** `-PerfLabel base` … flip the one line under test (METHOD-5), rebuild …
`-PerfLabel change -PerfCompare base` pairs each scenario against the most recent `base` record and
prints the ratios. Identical dll hashes are called out as "SAME BINARY — a noise floor, not an A/B".
Nothing in this stage can fail a build, and **a history trend is awareness, not evidence**.

**What is read and what is refused.** Verdict metrics: `render_cpu_ms`, `gpu_ms` and the counts
`draws` / `prims` / `nodes`, plus the startup phases. Recorded but printed as *awareness only*, each
with its reason: `fps` and `frame_ms` (paced — floors, PERF-2), `script_ms` (`TIME_PROCESS`,
~2.2× real per PERF-1, and it collapses onto the frame cap when the loop is paced), `physics_ms`
(`--det` makes the clock parent-driven, so `_PhysicsProcess` consumers no-op and the term is empty),
`mem_mb` (managed-heap high-water, monotonic inside a run), `max_ms`/`p95_ms` (the worst frame and
the 95th percentile within each 60-frame window, then MEDIANED across windows — a population too
small to hold a ratio, and the median actively hides a single bad window: a real 50 ms injected
stall moved one window's own `max_ms` from 8.33 to 48.96 while the scenario's reported `max_ms`
stayed 8.33, three clean windows outvoting the hit one), `hitch_count` (below). A row is marked `*`
only when it clears **both** a relative band and an absolute floor, both measured as this machine's same-build noise — see `docs/verification.md` PERF-9…PERF-11 and the manifest's `notes`.

**`hitch_count`** is `HitchMonitor`'s own trip count for the launch — the
`[perf] hitch …` lines B6 writes — median over the same kept-launch population as every other metric
here, riding inside the record's `metrics` object rather than a section of its own so it flows
through the existing ratio machinery for free. It is what actually survives a single hitching frame:
`max_ms`/`p95_ms` above are a median of per-window extremes and a lone spike gets outvoted, but a trip
either happened or it did not. **Awareness only, never a verdict** — the run-to-run spread of hitch
count on an unchanged build is not yet measured, and a count this bursty is the last thing that
should gate anything (`docs/verification.md` PERF-5, METHOD-3). Every record also carries `"vsync"`
(`"off"`/`"on"`, read back from each launch's own `[perf] vsync …` line): hitch counts, and `max_ms`/
`p95_ms`, are not comparable across vsync modes, since a padded frame changes what a hitch even means
(PERF-13). Every scenario here runs `--no-vsync`, so today every record reads `"vsync":"off"`; the
field is carried (not yet checked — `-PerfCompare` does not refuse a mismatched pair) so a future
vsync-on scenario at least leaves the mode visible in both records being read side by side.

**Exit-code contract: 1 if any stage FAILED, 0 otherwise — and a skip is not a failure.** No game
data, no Godot, `-SkipUnits`/`-SkipEngine` all report `SKIP` and keep the run at 0, but every one of
them prints a `not checked:` line and the summary names what went unmeasured: "the data was not
there" must never read as "the check held". The `TODO` row does the same for the hitch stage, which
never resolves to `PASS`/`FAIL` because it is awareness-only, and `-RegenGoldens` reports its stage
as `REGEN` — never `PASS` — with a `not checked:` line saying the goldens were rewritten rather than
verified.

**Worktrees.** Extracted data is found through `CSVM_DATA_ROOT` by the engine and the unit tests
alike, and Godot is resolved this tree first then `CSVM_DATA_ROOT` (as `RunGame.ps1` does), so
`$env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'; .\RunTests.ps1` from a git worktree behaves identically
to the primary tree. Without it a worktree still builds and runs the units; the engine stage reports
`SKIP` with the Godot path it looked at. Note the unit tests fall back to their own checkout when
`CSVM_DATA_ROOT` names a directory holding no extraction, so pointing it at an empty folder skips
the engine suites but *not* the data-dependent units — from the primary tree they still find
`extracted/`.

Stray Godots are killed before the engine, golden, hitch and perf stages, **filtered to this tree's
project dir AND an argument only that stage's own launches carry** (SHELL-2) — `--run-tests` for the
engine stage, the `.scratch\goldens\` / `.scratch\hitchcheck\` / `.scratch\perf\` output paths for
the other three. All always quit by themselves, so one still alive is stuck and ours, while any
other Godot on this tree — a live playtest, another agent, a hand-run capture to any other path —
is reported and left alone.

**All three scripts set `SDL_JOYSTICK_DIRECTINPUT=0`**, respecting a pre-set value — the
controller-disconnect freeze workaround (2026-07-19). Godot's bundled SDL hangs the main thread
forever when a >255-button DirectInput device disconnects (the 8BitDo Ultimate 2 dongle is one);
disabling the dinput backend removes those phantom views, and real pads keep working via
XInput/HIDAPI. Direct editor or exe launches don't get the workaround. Removal conditions are in
`backlog.md` under "Drop the `SDL_JOYSTICK_DIRECTINPUT=0` workaround".

## Exporting a release build

`CSVM/export_presets.cfg` (committed, added 2026-08-05, friends-release B11) holds one preset,
**"Windows Desktop"**: release export, x86_64, `embed_pck=true` — a single `CSVM.exe` with the
pck inside, plus the .NET publish output beside it as `data_CSVM_windows_x86_64/`
(**self-contained**: `coreclr.dll`/`hostfxr.dll` ship in it, so a recipient installs no .NET
runtime). The exported build resolves every root to the exe's own folder: it reads
`extracted/` beside the exe and writes its logs to `.scratch/logs/` beside the exe.

**One-time template install.** The Godot export templates are user-global, not part of the
repo's pinned editor: extract the inner `templates/` FILES of
`tools/godot-4.7-mono-export-templates.tpz` (an ordinary zip) directly into
`%APPDATA%\Godot\export_templates\4.7.stable.mono\` (create the version dir; do not keep the
`templates/` folder level).

**`ExportRelease.ps1` (repo root)** does the whole sequence: checks the export templates are
installed at `%APPDATA%\Godot\export_templates\4.7.stable.mono\` (throwing a named error if not,
rather than letting the export itself fail partway through), creates `.scratch\export\` if it's
missing, builds, imports headless, then exports. Equivalent by hand (a fresh tree needs the
build + one import pass first):

```powershell
dotnet build CSVM/CSVM.sln
& tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe `
    --path CSVM --headless --import
& tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe `
    --path CSVM --headless --export-release "Windows Desktop" Z:\CSVM\.scratch\export\CSVM.exe
```

Output lands at the path given on the command line (the preset's own `export_path` is
`../.scratch/export/CSVM.exe`, git-ignored, used when exporting from the editor GUI).

Two filters in the preset are load-bearing:

- `include_filter="data/*.json"` — `stock_loadouts.json`/`effect_pools.json` are non-imported
  resources the default export silently drops; without this every plane flies unarmed. Their
  loaders read `res://data/*.json` through `Godot.FileAccess` (not `GlobalizePath` + System.IO),
  which is what makes the pck copies reachable in an export — keep it that way.
- `exclude_filter="config.json"` — ⚠ the dev box keeps a personal tuning override at
  `CSVM/config.json` (git-ignored). The exclude guarantees it is never baked into a build even
  when exporting from the main tree; an exported build logs `config absent … using in-code
  defaults`, which is correct.

Smoke-test an export from a **bare folder** (exe + data dir + `extracted/` beside it) launched
with a **foreign CWD** and no `CSVM_DATA_ROOT` — the CWD and the env var can both mask a broken
default root (verification: A1's traps).

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

### Window focus: scripted runs stay out of your way

The game window is **created without focus** (`display/window/size/no_focus` in `project.godot`), so
a scripted run never takes the desktop from whoever is using the machine — a full `RunTests.ps1`
launches the engine about twenty times, and before this it grabbed the foreground on most of them.

Not taking focus is not the same as staying out of sight: an unfocused window still *opens in front*
of what you are reading. So a scripted session also **hides its window** —
`ScriptedWindow.Hide` calls `ShowWindow(SW_HIDE)` once `Launcher._Ready` knows the flags. Rendering
is unaffected (all 11 goldens hash-identical); hiding is deliberately not *minimizing*, which stops
rendering and blanks the captures (verification SHOT-16).

That still leaves a **~1 s flash** for anything the engine launches by hand, because the window
exists from ~180 ms and `_Ready` cannot run before ~1180 ms. So `RunTests.ps1` does not rely on it:
it runs **every launch on a separate Windows desktop** (`HiddenDesktop.ps1` — `CreateDesktop`, then
`CreateProcess` with `STARTUPINFO.lpDesktop`). A window belongs to the desktop its process was
started on and only one desktop is ever displayed, so this is decided *before* the process runs,
which is the only kind of placement that works (SHELL-9). The summary line says which desktop was
used, because a silent fallback to the visible one looks exactly like success. If the OS refuses the
desktop, the run continues visibly rather than failing.

Measured: a full run passed 152 units, 9/9 suites and 11/11 goldens hash-identical while a probe
sampling our own desktop every 50 ms saw a Godot window in **0 of 700 samples**, Godot alive in 697
of them; perf draw counts are identical to a visible run. Evidence and the rejected alternatives:
[`analysis/hidden-desktop/`](../analysis/hidden-desktop/FINDINGS.md).

`RunGame.ps1` and `RunDev.ps1` hand the foreground to the new window themselves, so playing is
unchanged. That grab lives in the launcher and not in the engine because Windows' foreground lock
no-ops `SetForegroundWindow` from a process the user is not interacting with; the console you typed
into is that process, so it is allowed to give the foreground away (verification SHELL-5, SHELL-9).
The window is found by pid via `EnumWindows`, not `Process.MainWindowHandle` — that property is
zero for a hidden window, and a launcher whose own window is hidden (an agent shell, a scheduled
task) passes `SW_HIDE` down via `STARTUPINFO`, which is also why the launch asks for
`-WindowStyle Normal` explicitly. Godot's stdout/stderr go to `.scratch/logs/game-<stamp>.out`/
`.err`: with no stdout handle the plain exe attaches the launcher's console and prints its whole
engine chatter over it (the engine's categorized log lands in `.scratch/logs/` regardless).

`RunTests.ps1` also uses the **non-console** Godot binary, whose console twin opens its own
`Godot Engine (Console)` window. A GUI-subsystem binary does not block PowerShell and, started
without std handles, reattaches to the parent console and prints straight onto the terminal the run
came from (verification SHELL-10) — so every stage launches through the script's `Invoke-Godot`
helper, which waits for the process and redirects both streams to `<its --log-file>.out` / `.err`.
Stages still read their results from `--log-file` and the JSON reports rather than from console
text; the suite table you see live is replayed from the log. `--no-focus` remains as the manual
lever that marks any ad-hoc run as scripted.

**`RunProbe.ps1` — the same launch for ad-hoc runs.** `Invoke-Godot` is private to `RunTests.ps1`,
so every hand-launched probe (`--screenshot=`, `--dump-*`, a single `--run-tests=` suite) used to
inherit both problems: the ~1 s window flash *and* the console scribble — a bare `& $GodotExe …`
from any shell reattaches to the calling terminal and prints the whole world-build chatter over it
(SHELL-10), which is exactly what an agent-driven session sprays across the user's screen. So:
**never invoke the Godot binary directly for a scripted run — go through `.\RunProbe.ps1 <user
args>`.** It forwards every argument verbatim (no build step — build first), runs on its own hidden
desktop (`csvm-probe`, falling back to a *visible but still redirected* run if the OS refuses one),
parks the streams beside the run's `--log-file` when one is passed (else
`.scratch/logs/probe-<stamp>.out/.err`), prints where they went, and exits with Godot's exit code.
The wait is bounded: `-TimeoutSec` (default **300**, `0` = wait forever) kills the run when it
expires and exits **124** (the GNU timeout convention), so a probe that never quits — a flag
combination with no auto-quit, a stuck boot — cannot hang an agent session; the partial
`.out`/`.err` streams survive the kill and show where it hung. A deliberately long run
(`--frames=` beyond ~5 min of sim) needs an explicit larger value. `RunTests.ps1` gives its
in-engine phase the same five-minute bound; its other scripted stages retain their own policies.
