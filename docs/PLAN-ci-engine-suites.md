# CI engine suites without the extraction

**ACTIVE PLAN** (written 2026-10-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

CI (`.github/workflows/checks.yml`) runs the content gate, `dotnet format`, StyleCop and the xUnit
project, and nothing from `--run-tests`, because the in-engine suites read the player's
extraction. This plan gets as many of the 495 suites as possible running on CI without one: first
the suites that already read nothing from the install, then the suites a code-built or
hand-authored stand-in can feed. The stand-ins follow the rule `CSVM.Tests/fixtures/README.md`
states: invented content written from `docs/formats/`, never a trimmed or sampled copy of an
extraction. No item here draws on `backlog.md` or a GitHub issue.

Out of scope: the 266 suites whose point is the shipped data or a named chapter, mission or node
(bucket E in `analysis/ci-suite-survey/`). They stay with the local `RunTests.ps1` battery, which
remains the landing gate for every change under `CSVM/`. The golden images and the hitch check stay
local too.

## Milestone goal

- A CI job builds the project, runs the in-engine suites headless on Linux with no extraction, and
  fails the PR when any suite on a checked-in CI list fails **or skips**.
- The install-free suites (bucket A) and the three world-as-terrain suites (bucket B) are on that
  list.
- A synthetic data root, written at run time from invented records and code-generated PNG and WAV
  files, feeds a stand-in plane, so the plane-only suites (bucket C) join the list.
- Invented arena and shell fixtures (a multiplayer spawn table, the Original shell's remaining
  layout screens, anim, effect and weather records) bring the bucket D suites in.

**CI checks mechanisms, the local battery checks the game.** A suite on the CI list proves its code
path works on invented data; whether the real airframes, missions and menus behave is still the
local battery's question, and no item here weakens or replaces it.

## Decisions (2026-10-01)

| # | Question | Decision |
|---|---|---|
| 1 | What is the goal? | **As many in-engine suites as possible run on CI without the extraction**, the author's stated aim. |
| 2 | May a stand-in be derived from real data? | **No.** Every stand-in is invented or generated in code, per the repo's hard rule on game assets. |
| 3 | Is moving suites onto `--stage=empty` the route? | **Only for the three B suites.** The empty stage removes the chapter world but still loads the plane, its records, textures and sounds, so the plane stand-in is the real unlock. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "A suite on `--stage=empty` needs no install." | `GameSession.BuildFlightRigs` uses `planes.zbd` as the session gamez on the empty stage (`GameSession.cs:2608`), then reads `PlaneStats` from `zrdr.zip` (`GameSession.cs:2619`), weapon defs and shakes (`GameSession.cs:2698-2706`) and the chapter texture archive (`SessionPaths.ChapterTextures`). Each is fatal when absent. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2, A3, C21 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | A1, A4, B11, B14 | The mechanism is settled; how many suites each one moves is an estimate from the survey. |
| **Leads only, no mechanism yet** | B12, B13, B15, C22, C23, C24 | The survey names the inputs; nobody has built the fixture or run a suite on it. |

## What the data actually shows

The survey is `analysis/ci-suite-survey/` (`suites.tsv` per suite, `FINDINGS.md` for the method and
its caveats). Its rows are inferred from each suite's gates, helpers and assertions, not from a run,
so every count is good to about ten.

| Bucket | Count | What unblocks it |
|---|---|---|
| A, reads nothing from the install | 56 | A CI job, and dropping `RequireData` gates the body never uses. |
| B, chapter used only as ground or host | 3 | Moving onto `EmptyStage`. |
| C, plane only | 83 | The stand-in plane kit (Wave B). |
| D, a few authored records | 87 | Arena and shell fixtures (Wave C). |
| E, parity with the real data | 266 | Stays local. |

The install inputs a `--stage=empty --fly` session reads, all under `extracted/`. Every loader
takes a zip or the unzipped sibling folder (`SessionPaths.PreferUnzipped`, `SessionPaths.cs:28`),
so a synthetic root can be plain folders.

| Input | Read for | Absent |
|---|---|---|
| `planes/` (`nodes.json`, `models.json`, `materials.json`) | plane mesh, markers, cockpit, gauges | fatal |
| `C1/texture` or `C1/rtextureN` (PNGs + `manifest.json`) | skins, gauges, projectile and effect textures | fatal for the archive, degraded per PNG |
| `zrdr/vehicle.json`, `engines.json`, `player.json` | plane stats and flight constants | fatal |
| `zrdr/weapons.json`, `shakes.json` | weapon defs, camera shake | fatal |
| `zrdr/camparam.json`, `ai.json`, paint catalog | chase camera, turret gunners, livery | defaults or skipped |
| `soundsh/` + `zrdr/sounds.json` | engine, weapon, impact sound | archive absent: silent; archive without `sounds.json`: fatal |
| `messages.json`, `interp.json`, `rimage/`, `rof/` | strings, mip bias, HUD font and reticle, paint patterns | skipped or empty |

Existing pieces a stand-in can start from:
- **Hand-authored fixtures:** `CSVM.Tests/fixtures/` already holds a `gamez-plane/` marker tree (no
  meshes), `zrdr/weapons.json`, `sounds.json`, `shakes.json`, `camparam.json`, `ai.json`,
  `demo_anims.json`, `messages.json`, `ia/`, `ainets/` and `menu-layout-original/`. It has no
  `vehicle.json`, no `engines.json` and only a partial `player.json` (`zrdr-skills/`).
- **Archives generated in code:** `TextureArchiveTests.cs:189` and `SoundArchiveTests.cs:74` build
  texture and WAV archives at run time.
- **Headless Linux runs:** `sandbox/LinuxRelease.ps1` already runs the suites headless and keeps the
  list of the 11 suites that cannot pass without a real renderer or display (`$HeadlessOnly`), plus
  the headless-only engine error lines (`$HeadlessEngineErrors`).

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
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

### Wave A, CI runs the install-free suites

1. ☑ The `ci` tier: a checked-in list CI requires to pass, where a skip fails
2. ☑ Drop unused `RequireData` gates, and make the unguarded loads skip
3. ☑ The CI engine job: Linux Godot 4.7 .NET, headless, `--run-tests=tier:ci`
4. ☑ Move the three world-as-terrain suites onto `EmptyStage`

### Wave B, the synthetic data root and the stand-in plane

11. ☑ A synthetic data root written at run time
12. ☐ The stand-in plane: model, markers and plane records
13. ☐ Stand-in weapons, shakes and messages
14. ☐ Generated texture and sound archives
15. ☐ Bring the plane-only suites onto the tier

### Wave C, arena and shell fixtures

21. ☐ A code-built spawn table for a match on the empty stage
22. ☐ An invented multiplayer map for the team and flag suites
23. ☐ The Original shell's remaining layout screens
24. ☐ Anim, effect and weather records for the remaining D suites

## Dependency and parallelism notes

A1 blocks A3 (the job runs the tier) and every later item (each one ends by adding suites to the
tier). A2 and A4 run in parallel with A3. B11 blocks the rest of Wave B; B12, B13 and B14 are
independent of each other and can run in parallel worktrees, since each writes its own records into
the synthetic root, but all three add files under the same builder, so give each a stated
sub-folder. B15 needs B12 to B14. C21 needs B15's plane; C22 needs C21; C23 needs only A1 and B11
(it adds layout and art to the root); C24 needs B12. File contention: every item edits the tier
list in `SuiteCatalog.cs`, so tier additions land one item at a time.

---

# Wave A, CI runs the install-free suites

## A1 ☑ The `ci` tier: a checked-in list CI requires to pass, where a skip fails

**Landed.** `--run-tests=tier:ci` runs `SuiteCatalog.CiTier`, a sorted one-name-per-line list of
43 suites. `SuiteCatalog.Tier` now returns a `SuiteTier` (its names plus `SkipFails`), and only the
ci tier sets `SkipFails`. The harness reads it through the pure `TestHarness.SkipFailures(spec)`:
a selected suite that a `SkipFails` tier term lists and that SKIPs is reported FAIL, with the skip
reason as the row's detail, a `!!` line and an `ERROR [test]` line naming the tier, and the process
exits 1. Any other selector, `suite:<name>` on a listed suite included, keeps today's SKIP. The
fail-on-skip check lives in the harness, read off the tier, so CI needs no script of its own to
read `test-report.json`. Membership is every suite that passed headless with an empty data root
here (all 43 of the baseline's passes), none of which is on `$HeadlessOnly` or needs IPv6 or an
OS shell; `enet-transport`, `enet-load-stall` and `lan-discovery` (127.0.0.1 only) stayed in after
three clean runs. The rule is in `docs/tooling.md`. Verified on Linux with an empty data root:
`tier:ci` 43/0/0 and exit 0 three times; a `RequireData` probe on `target-ref` turned `tier:ci`
red (42/1/0, exit 1, naming the suite) while the full catalog and `suite:target-ref` still
reported it SKIP. The author's Windows run owes: the full `RunTests.ps1` battery, which should be
unchanged (same counts as before; no suite body changed), and one windowed `--run-tests=tier:ci`
on the real install, which should pass all 43.

**Original approach (kept for reference).**

**Goal.** `--run-tests=tier:ci` runs a checked-in list of suites, and the run fails when a listed
suite fails or skips, so a suite that silently loses its input is a red build, not a quieter
green one.

**Evidence (confidence: direction-sound).** `SuiteCatalog.Tier` resolves only `quick` today
(`SuiteCatalog.cs:37`). SKIP is not a failure, and an all-skip run exits 0 (`TestHarness.cs:144`),
so running the whole catalog on CI would report about 270 skips as green. The initial members are
bucket A from the survey, less the suites that cannot pass headless (`menu-screenshot-key` is on
`$HeadlessOnly`) and less the suites that skip by environment (`enet-stable-ipv6-reply` needs
stable global IPv6). About 54 suites.

**Approach.** Add a `CiTier` list beside `QuickTier` and route `tier:ci` through `Tier`. Make
skip-is-failure a property of the selector (only `tier:ci` gets it), not a global harness change,
so the local battery keeps its SKIP semantics. <TODO: whether the fail-on-skip check lives in the
harness or in the CI script reading `test-report.json`.> Write the selection rule into
`docs/tooling.md` beside the quick tier's.

**Model recommendation.** medium: a small, well-bounded harness change.

**Verify.** With `CSVM_DATA_ROOT` pointing at an empty folder, `--run-tests=tier:ci` passes. Then
show it able to fail: add a `RequireData` on a missing path to one listed suite and the run must go
red on the skip.

**⚠ Traps.** A suite on the list that needs an environment the runner lacks (IPv6, a display, a
GPU) belongs off the list, not on it with an exception. Keep the list a judgement about the set,
like the quick tier, rather than an attribute on each body.

## A2 ☑ Drop unused `RequireData` gates, and make the unguarded loads skip

**Landed.** The nine suites that loaded `zrdr.zip` with no gate (`campaign-danger-zones`, `campaign-mission-cash`, `campaign-mission-end`, `campaign-objectives-hud`, `campaign-persistence`, `hangar-door-wake`, `mission-radio`, `music-states`, `persist-chain-kill`) now `RequireData` every install input they read before the first read, so they SKIP with an empty data root instead of failing. Eight menu suites lost a `zrdr` gate their body never uses and now PASS headless with an empty data root: `menu-coop-door`, `menu-host-pointer`, `menu-host-tracer`, `menu-host-address`, `menu-net-door`, `menu-controls-seats`, `menu-player-setup-journey`, `menu-zone-layout`. Every assertion in them reads the same on the real install: stats lines, the langui labels and the instant action defs differ with and without `zrdr`, and none of the eight asserts on them. A full catalog run with an empty data root went from 43 PASS, 11 FAIL, 441 SKIP to 51 PASS, 2 FAIL, 442 SKIP; the two failures are `build-stamp-focus` and `enet-dual-stack`, both environment limits that are not this item.

Two suites keep their gate because the body asserts on install data: `menu-hangar-journey` (the paint screen composes a preview from the decal art, and the decal row shows its tile) and `menu-instant-action-journey` (the ace is the environment's own, which `InstantAction.Defaults()` cannot supply). Each is a split candidate: the rest of either journey reads nothing from the install. `render-thread-handoffs` had no data gate to drop; it skips headless for want of a rendering device and runs windowed. `net-team-deathmatch` already skipped with an empty root, because its `MatchScores.Load` argument tolerates a missing archive, so the earlier reading of it as a FAIL was wrong.

**Still owed to the Windows run.** The real-install battery should be unchanged: the nine now-guarded suites PASS as before, the eight ungated suites PASS as before, and the two gated journeys PASS. `menu-zone-layout` and the other ungated suites run without the decal tile art on CI, so the tile leg of the paint screen is still covered only by the real-install run. `menu-player-setup-journey` prints 21 headless text-server error lines (a zero font size), already on `HeadlessEngineErrors` in `sandbox/LinuxRelease.ps1`; the CI job needs the same allowance.

**Original approach (kept for reference).**

**Goal.** Every bucket A suite runs with no install, and no suite fails for want of an input it
does not check for.

**Evidence (confidence: traced).** `MenuZoneSuites.cs:29` gates `menu-zone-layout` on
`ctx.ZrdrPath` although the built-in menu degrades without it (stats read as "n/a",
`InstantAction.Defaults()`, stock loadouts from `res://`); the survey marks about 12 menu suites
the same way. `MusicSuites.cs:29-31` loads `ctx.ZrdrPath` and `ctx.SoundsPath` with no gate.
`NetTeamSuites.cs:54` evaluates `MatchScores.Load(ctx.ZrdrPath)` as an argument before
`MatchSpec` reaches its gates (`NetCombatSuites.cs:422-438`). The last two should FAIL rather
than SKIP with no install. <TODO: confirm both by running with an empty data root.>

**Approach.** For each A suite with a gate, delete the gate when the body reads nothing from that
path; keep it when a leg of the suite does. Give the two unguarded suites their `RequireData`
lines before the first load. Split a suite with a pure half and a data half only where the survey
names one (`display-*`, `instant-action-wrapup`, `menu-player-setup-seats`), and defer the split
to B15 if it needs the stand-in.

**Model recommendation.** medium, low effort: mechanical per suite, but each gate needs a read of
the body.

**Verify.** Every A suite passes with an empty data root, and the full local `RunTests.ps1` on the
real install is unchanged.

**⚠ Traps.** `menu-hangar-journey` may assert stock armour values that read as zeros without
`zrdr`; read its armour-page checks before ungating it.

## A3 ☑ The CI engine job: Linux Godot 4.7 .NET, headless, `--run-tests=tier:ci`

**Landed.** `.github/workflows/checks.yml` has an `engine` job on `ubuntu-latest` beside the
unchanged three-OS `checks` job: .NET 8, Godot 4.7-stable mono for Linux x86_64 from the official
release, checked against the release's `SHA512-SUMS.txt` and cached under a key naming the release
(so the key pins the version, and the checksum is checked once, before a copy is stored),
`libfontconfig1` installed only if the image lacks it, `dotnet build`, one headless import, then
`./RunCiSuites.ps1 -Godot <exe>`, with the report, Godot's streams, the screened engine log and
`.scratch/logs/` uploaded on every outcome. `RunCiSuites.ps1` is a root script a contributor runs
the same way on Linux or macOS: one headless Godot over a fresh empty `CSVM_DATA_ROOT` and fresh
XDG folders, judged from the report rather than the exit code. The two headless lists moved out of
`sandbox/LinuxRelease.ps1` into `analysis/headless-limits.json`, which both scripts read; the
Linux release check builds the same `$HeadlessOnly` and `$HeadlessEngineErrors` from it (keys,
values and order checked identical), so it behaves as before. `RunCiSuites.ps1` excuses an
unexpected engine line only when it matches a `headlessEngineErrors` pattern, as the release check
does, and the harness's in-process screen is not touched, so no allowance widened. A unit test
keeps every `headlessOnly` suite registered and off `CiTier`. With that allowance
`menu-player-setup-journey` runs clean and joined `CiTier` (51 suites): its 21 text-server lines
all match the existing `_shaped_text_add_string` pattern. A bare headless `--run-tests=tier:ci`
now exits 1 on those lines, so the script, not the exit code, is the gate. No budget line: the
budgets file covers `RunTests.ps1`'s stages, measured on the development machine. Verified on
Linux with an empty data root and the script's environment, judged by the script's verdict logic:
51/0/0 three times, the 21 lines excused, about 26 s wall per run; a broken `target-ref` input
gave 50/1/0 and a failing verdict. `pwsh` was refused in the agent sandbox, so the script itself
did not run there; its verdict was ported line for line and applied to the real reports. Still
owed: the first real GitHub run on a PR (green, then red on a deliberately broken listed suite),
which needs a push; a `RunCiSuites.ps1` run under `pwsh`; and the author's Windows battery, which
should be unchanged apart from `menu-player-setup-journey` passing as before, and one windowed
`--run-tests=tier:ci` on the real install, which should pass all 51.

**Original approach (kept for reference).**

**Goal.** Every PR runs the `ci` tier headless on `ubuntu-latest` and goes red on a failure or a
listed skip.

**Evidence (confidence: traced).** The project is `Godot.NET.Sdk/4.7.0` (`CSVM/CSVM.csproj:1`).
`sandbox/LinuxRelease.ps1` already runs the suites headless on Linux and holds the two lists a
headless run needs: `$HeadlessOnly` (11 suites that read back a renderer or display) and
`$HeadlessEngineErrors` (engine error lines only a headless process prints). `RunTests.ps1` runs
windowed on purpose, because shaders do not compile headless, so the windowed local battery stays
the authority on rendering.

**Approach.** A new job in `checks.yml` (Linux only): set up .NET 8, download and cache the Godot
4.7 stable mono Linux build, `dotnet build`, run `--headless --import` once, then
`--headless --path CSVM res://scenes/Main.tscn -- --run-tests=tier:ci`. Move the two headless
lists out of `sandbox/LinuxRelease.ps1` into one place both scripts read. Upload
`.scratch/test-report.json` and the engine log as artifacts. <TODO: the Godot download URL and
whether the cache key pins its checksum.>

**Model recommendation.** high: CI infrastructure with an engine download and an error allowlist,
where a wrong call either hides failures or makes the job flaky.

**Verify.** The job is green on a PR, and a deliberately broken assertion in a listed suite turns
it red. Wall time recorded against a budget in `analysis/verification-budgets.json`.

**⚠ Traps.** Godot's Linux build logs a fontconfig error per lookup (`sandbox/LinuxRelease.ps1:170`
handles it); a CI allowlist must match the existing one rather than grow its own. Never widen the
error allowlist to get the job green.

## A4 ☑ Move the three world-as-terrain suites onto `EmptyStage`

**Landed.** `forward-rotation` and `launch-direction-cache` no longer build a chapter world. Both
launch hand-built OBJECT_MOTION bodies with no gravity block from a probe node, and read only the
node's local transform back, so their host is now `WithMotionHost` in
`AnimationAndEffectsSuites.cs`: a plain parent node and a bare `new AnimRuntime()` bound to it over
an empty `AnimProgram`. That answers the open question: an `AnimRuntime` stands up without a
`WorldSession` through its existing internal constructor and `Bind`, the way `CoopCutsceneSuites`
and `TargetingSuites` already build one, so no production code changed and no chapter path moved.
Neither suite needs the grid or a collider, since neither is a contact case, so `EmptyStage` itself
is not used. Both PASS headless with an empty data root, and each FAILs when its launch input is
broken (azimuth 90 for the +X throw; a +X cruise for the +Z one). A full catalog run with an empty
data root went from 51 PASS, 2 FAIL, 442 SKIP to 53 PASS, 2 FAIL, 440 SKIP.

`ground-contact` stays install-bound. It reads the chapter's own program
(`world.Session.Program.ByAnimName("gunshell")`, `AnimationAndEffectsSuites.cs:382`) and asserts
that the extracted `gunshell` event still authors `no_altitude` (`:403`), which is a check on the
shipped data. It also takes `world.Runtime.Destructibles.All.First().Def` as the motion's owner
(`:264`, `:439`), though any hand-built `AnimDefinition` would serve there. With the `gunshell` leg
removed and a hand-built owner, every other leg PASSES on `EmptyStage.Build(collision: true)` with
an empty data root (a trial, reverted), so splitting that leg into its own suite would bring the rest
onto the tier. That split adds a suite to the catalog and is left for a decision.

**Still owed to the Windows run.** The real-install battery should be unchanged: `forward-rotation`
and `launch-direction-cache` PASS with the same notes (1.000 and 0.333 rad/s; the cruise ends at
1600 m along +Z), now without building or reusing C1, and `ground-contact` PASSES on C1 as before.
No coverage is lost: neither moved suite used C1's terrain (both built the world with
`collision: false`), their ranged launches draw from min = max ranges so the runtime's seed decides
nothing, and the chapter runtime's contact mask and inherited velocity never reach a body with no
gravity block.

**Original approach (kept for reference).**

**Goal.** `ground-contact`, `forward-rotation` and `launch-direction-cache` run on the code-built
stage and join the tier.

**Evidence (confidence: direction-sound).** The survey reads all three (`AnimationAndEffectsSuites.cs`)
as using `WithWorld(ctx.Chapter, …)` only for a collidable ground or an `AnimRuntime` host, with
hand-authored anim data on a probe node and no named node or count. `GroundShadowSuites.cs:278`
and `StaticCameraSuites.cs:69` already build `EmptyStage.Build(collision: true)` inside a suite.

**Approach.** Replace the chapter build with `EmptyStage.Build`, plus whatever `AnimRuntime` host
the two launch suites need. <TODO: whether an `AnimRuntime` can be stood up without a chapter
`WorldSession`; if not, A4 waits for a small runtime-only host.>

**Model recommendation.** medium.

**Verify.** Each suite passes on the empty stage with an empty data root, and fails when its
assertion is broken. The local battery is unchanged.

**⚠ Traps.** Do not move a suite that reads a surface class, a named node or a per-chapter count
off the world; those are bucket E even when they also use terrain.

# Wave B, the synthetic data root and the stand-in plane

## B11 ☑ A synthetic data root written at run time

**Landed.** `--synthetic-data` writes an invented `extracted/` tree into
`.scratch/synthetic-data/<pid>/` and reads it as the data root, for any launch, `--run-tests`
included. `Tooling/SyntheticData.cs` is the builder, engine-free so `CSVM.Tests/SyntheticDataTests.cs`
builds the same tree; `Tooling/SyntheticTextures.cs` is its one family so far, the C1 texture
archive (`extracted/C1/texture/`: the hand-authored `fixtures/synthetic/C1/texture/manifest.json`
plus one generated checker PNG per `texture_infos` entry, through the existing
`Extraction/PngWriter.cs`). The open questions, settled:

- **Opt-in, and what a real install gets.** Only the switch builds or selects the tree; nothing
  infers it from a tier or a missing install. With the switch, the tree replaces whatever root
  `--data-root=`, `CSVM_DATA_ROOT` or the repo root named, and the install there is not read: the
  author's checkout always has an install at the repo root, so refusing would make the CI run
  impossible to reproduce locally without hand-pointing `CSVM_DATA_ROOT` at an empty folder, and
  the replacement reads nothing from the install because every base path derives from the replaced
  root. The explicit `--zrdr=`-style overrides still win, as they always do. The switch is logged as
  `WARN [core] SYNTHETIC DATA` naming both roots, the harness adds a `WARN [test]` line and
  `data=SYNTHETIC` on its summary line, and `test-report.json` carries `"syntheticData": true` beside
  the `dataRoot` path. A data root pointed at a synthetic tree without the switch gets the same
  warning. A tree that cannot be written quits 1 and never falls back to the install. Beside
  `--extract` the switch is dropped with a note.
- **The stamp.** The tree is stamped through `ExtractionStampWriter.Merge` with the current
  `schema` and a `synthetic` field. `ExtractionStamp.Check` reads only `schema`, so a field it does
  not know is inert: the synthetic stamp passes the boot check silently, `Standing` reads
  `Current` (a menu launch reaches the menu) and `Behind`, the Original shell's gate (C23), is
  false. An unstamped tree would read the same everywhere except one boot warning, so the stamp is
  needed for the marker more than for the check: `Marks` reads the `synthetic` field, and `Build`
  deletes an existing `extracted/` only when it carries it, so a real extraction is never replaced.
- **Where the records live.** In `CSVM.Tests/fixtures/`, read from the repo checkout
  (`SyntheticData.FixturesUnder(repoRoot)`); nothing moved. The engine never reaches them through
  `res://`, and an export has no such folder, so it refuses the switch and ships nothing extra.
- **How the generators are shared.** They are engine code in `CSVM/src`, which `CSVM.Tests`
  references, so the unit tests call `SyntheticData.Build` directly. A generator must stay
  engine-free for that.
- **Adding a family** (B12 to B14): one `SyntheticFamily` entry in `SyntheticData.Families` and a
  writer that copies records with `SyntheticTree.CopyFixture` and writes bytes with `WriteBytes`,
  only under its own folder. B12 owns `planes/` and the plane records under `zrdr/`, B13 the weapon,
  shake and message records, B14 extends `C1/texture/` and adds `soundsh/`. A WAV generator moves
  from `WavFileTests.cs` into `CSVM/src` the way `PngWriter` already sits there.

Verified on Linux, headless, with an empty data root and port base 50000: the full catalog without
the switch is unchanged at 53 PASS, 2 FAIL (`build-stamp-focus`, `enet-dual-stack`), 440 SKIP and
`syntheticData: false`; `tier:ci` with the switch passes 52/0/0 with the synthetic root in the log
and report; a temporary suite (reverted) opened `SessionPaths.ChapterTextures(DataRoot, "C1")` over
the synthetic root and decoded all three textures (64x64, 32x32, 16x8, class `None`) with no
warning line and no miss; a fake install (`extracted/VERSION.json` alone, schema 3) without the
switch read only that root, wrote no synthetic tree and skipped the probe, while the same root with
the switch read the synthetic tree and named the install as unread; a missing manifest quit 1 with
the reason. The full catalog over the synthetic root reads 53 PASS, 7 FAIL, 435 SKIP: five suites
gate on the C1 textures alone and then need real names or an unguarded `planes.zip`
(`cockpit-panel-staging`, `damage-staging-pool`, `puffer-fire-glow`, `puffer-smoke-sun`,
`tex-dropin`); that is B15's input, and no ci tier member is among them. The author's Windows run
owes the full `RunTests.ps1` battery, which should be unchanged (no suite changed and nothing runs
without the switch), and optionally one `--run-tests=tier:ci --synthetic-data` on the real
checkout, which should pass the tier and name the repo root's install as unread.

**Original approach (kept for reference).**

**Goal.** With no install, the harness (or the CI script) writes an `extracted/` tree of invented
records and generated files into the scratch folder and points the run at it, so the loaders take
their normal paths.

**Evidence (confidence: direction-sound).** The data root resolves from `--data-root`, then
`CSVM_DATA_ROOT`, then the repo root (`Launcher.cs:380-384`), and every loader accepts an unzipped
folder (`SessionPaths.cs:28`). `ExtractionStamp.Check` is skipped when the tree is missing
(`Launcher.cs:399`). <TODO: whether a synthetic tree must carry an extraction stamp, and what the
stamp check does with one it does not recognise.>

**Approach.** A builder in `src/Testing/` that copies the hand-authored records and generates the
binary ones, run only for `tier:ci` or a `--synthetic-data` flag. <TODO: where the hand-authored
records live, shared with `CSVM.Tests/fixtures/` or a new engine-side folder, and how the
`res://`-packed build reaches them.>

**Model recommendation.** high: it shapes every later item.

**Verify.** A run over the synthetic root and a run over the real install select different data
and say so in the log; a suite on the real install never reads a synthetic file.

**⚠ Traps.** The synthetic root must never be picked up by a local run that has a real install, or
the local battery would pass on invented data.

## B12 ☐ The stand-in plane: model, markers and plane records

**Goal.** An invented aircraft builds through `PlaneBuilder`, flies, and carries the markers the
flight, camera, HUD and damage code reads.

**Evidence (confidence: lead-only).** `fixtures/gamez-plane/nodes.json` is a `probe_plane` marker
tree with empty `models.json`; a plane node missing from planes gamez throws at `PlaneBuilder.cs:227`.
No hand-authored `vehicle.json` or `engines.json` exists. The survey's C rows name exhaust markers,
`cockpit1` with panel nodes, `pdpN` damage panels and a seven-entry `vehicle_injure_anims` ladder
as inputs particular suites read.

**Approach.** Extend the marker tree with a simple mesh, then write `vehicle.json`, `engines.json`
and `player.json` records from `docs/formats/`. <TODO: the minimum marker and node set, taken from
`docs/formats/markers.md` and the C rows that name nodes.>

**Model recommendation.** high: reading the formats right decides whether the plane flies at all.

**Verify.** `--stage=empty` with the synthetic root flies the stand-in with zero engine errors.

**⚠ Traps.** Do not shape the stand-in to make a suite pass; shape it from the format pages, and
let a suite that needs a real value be retargeted in B15.

## B13 ☐ Stand-in weapons, shakes and messages

**Goal.** The stand-in carries one gun and one rocket the weapon, ordnance and AI suites can fire.

**Evidence (confidence: lead-only).** `fixtures/zrdr/weapons.json`, `shakes.json` and
`messages.json` exist for the unit tests. The ordnance suites pin `wep_04/11/12/14/24` ids and
their launch speeds.

**Approach.** Reuse or extend the unit fixtures into the synthetic root. <TODO: whether the
ordnance suites read their expected values from the loaded def (then any id works) or need the ids
kept.>

**Model recommendation.** medium.

**Verify.** A stand-in plane fires both weapons on the empty stage and the rounds hit the ground
collider.

**⚠ Traps.** An invented id that collides with a real one is harmless on CI and confusing in a log;
keep the `wep_probe_*` naming the fixtures use.

## B14 ☐ Generated texture and sound archives

**Goal.** A chapter texture archive and a sound archive exist in the synthetic root, generated in
code.

**Evidence (confidence: direction-sound).** The survey finds the texture archive is the most
common single blocker in bucket C: most suites open
`new TextureArchive(SessionPaths.ChapterTextures(DataRoot, "C1"))` only to build a projectile pool
or a plane. `TextureArchiveTests.cs:189` and `SoundArchiveTests.cs:74` already generate archives.

**Approach.** Move or share those generators so the engine side can call them. Sounds cover the
engine, weapon and impact sets the C rows name; music needs clips long enough for `music-states`.

**Model recommendation.** medium.

**Verify.** The archives open through `TextureArchive` and `SoundArchive` with no warnings.

**⚠ Traps.** Generated WAVs must be the PCM or ADPCM shapes `docs/formats/` describes, or the
loader's own checks become the thing under test.

## B15 ☐ Bring the plane-only suites onto the tier

**Goal.** Each bucket C suite either passes on the stand-in and joins the tier, or is split into a
logic half (on the tier) and a parity half (local).

**Evidence (confidence: lead-only).** 83 suites, by family: flight, camera and HUD; AI and
targeting; ordnance; engine and weapon sound. Several assert values of named real records (`aim-assist`
pins the shipped `sticky_bullet_*` values; `target-pool` ends on C1's real AA guns;
`damage-stage-slots` pins the real injure ladder).

**Approach.** One family per landing. A suite asserting a real value either compares against the
record it loaded or splits. <TODO: the family order and the per-family list, from `suites.tsv`.>

**Model recommendation.** medium per family.

**Verify.** Each family passes on CI and the local battery is unchanged on the real install.

**⚠ Traps.** Rewriting an assertion to be relational can make it unable to fail; show each
retargeted check failing on a broken input before adding it to the tier.

# Wave C, arena and shell fixtures

## C21 ☐ A code-built spawn table for a match on the empty stage

**Goal.** A Dogfight on `--stage=empty` places every seat from a spawn table the stage builds in
code, so the network match suites run without a multiplayer map.

**Evidence (confidence: traced).** `SpawnPicker.LoadSpawnList` returns null on the empty stage
(`SpawnPicker.cs:77`). `NetCombatSuites.MatchSpec` requires planes gamez, zrdr, the chapter
textures and gamez and the MP mission zrdr (`NetCombatSuites.cs:422-427`), and skips unless the
`net.zrd` table holds at least two spawns (`NetCombatSuites.cs:436`). The survey counts 11 net
suites that need only an arena.

**Approach.** Give `EmptyStage` a spawn ring, the way it already has `PatrolNet`, and let
`MatchSpec` launch on the empty stage when no MP map is present. <TODO: whether the ring follows
the `net.zrd` block layout (`docs/formats/net-spawns.md`) so the same seat-walk code runs.>

**Model recommendation.** high: it changes a production spawn path, not only a suite.

**Verify.** A two-seat `--vs --stage=empty` match spawns both seats apart and facing in; the 11
suites pass with the synthetic root.

**⚠ Traps.** The real MP-map suites must keep running on the real table locally; the empty-stage
path is a fallback, not a replacement.

## C22 ☐ An invented multiplayer map for the team and flag suites

**Goal.** The team, flag and network AI suites run on an invented mission zrdr with free-for-all,
team and flag blocks.

**Evidence (confidence: lead-only).** The survey counts 14 bucket D suites that need a `net.zrd`
with team blocks (`NetTeamSuites.cs:55` needs more than two blocks), flag records, or a standing
destructible.

**Approach.** <TODO: the record set, from `docs/formats/net-spawns.md` and the flag decode.>

**Model recommendation.** medium.

**Verify.** <TODO>

**⚠ Traps.** Some of these suites pin values from the real C2/MP2 table; those split, as in B15.

## C23 ☐ The Original shell's remaining layout screens

**Goal.** The Original-shell menu suites run on an invented layout and invented art.

**Evidence (confidence: lead-only).** `fixtures/menu-layout-original/` covers the main menu, the
flight check and the Instant Action section. The survey counts 24 suites that need the Options,
VIDEO, controls, keys, hangar, campaign cabin, lobby and connection, join board and wrap-up
screens. The loader checks an extraction stamp, a layout with `MainMenu` and an art manifest.

**Approach.** <TODO: per screen, the widget keys the suites read.>

**Model recommendation.** medium.

**Verify.** <TODO>

**⚠ Traps.** Several suites assert relative geometry ("centred on its plaque", "five-across
tiles"), so the invented widgets need the same relative positions as the format describes.

## C24 ☐ Anim, effect and weather records for the remaining D suites

**Goal.** The remaining bucket D suites (anim and effect defs, weather and sun, puffer records,
one-off records) run on invented records.

**Evidence (confidence: lead-only).** The survey groups them as about 13 anim and effect suites,
4 weather and sun suites, and a handful of one-offs. `fixtures/zrdr/demo_anims.json`,
`weather.json` and `fogvol.json` exist.

**Approach.** <TODO: the per-group record set, from `suites.tsv`.>

**Model recommendation.** medium.

**Verify.** <TODO>

**⚠ Traps.** <TODO>
