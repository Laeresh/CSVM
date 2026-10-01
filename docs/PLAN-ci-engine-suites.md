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
12. ☑ The stand-in plane: model, markers and plane records
13. ☑ Stand-in weapons, shakes and messages
14. ☑ Generated texture and sound archives
15. ☐ Bring the plane-only suites onto the tier

### Wave C, arena and shell fixtures

21. ☑ A code-built spawn table for a match on the empty stage
22. ☑ An invented multiplayer map for the team and flag suites
23. ☑ The Original shell's remaining layout screens
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

**Splits.** Three suites now run as an install-free `-core` half on the tier and a data half under
the original name and gate, and no check runs in both. `ground-contact-core` carries every
hand-built case of `ground-contact` over the empty stage's collider (`WithMotionHost` with
`ground: true`, a hand-built `AnimDefinition` as the owner); `ground-contact` keeps only the
extracted `gunshell` leg on C1. `menu-hangar-journey-core` is the hangar journey less its two art
checks, which `menu-hangar-journey` keeps (the paint preview on a walked Devastator, the decal tile
under `--menu=paint`). `menu-instant-action-journey-core` is the Instant Action journey less the ace
check, which `menu-instant-action-journey` keeps on a Girl Trouble launch from Sky Haven. The tier
holds 56 suites, all PASS headless with an empty data root, and the three originals SKIP there as
before. The author's run should show all six PASS; the core contact legs now land on the stage's
flat ground at y = 0 instead of C1's terrain at the origin.

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

## B12 ☑ The stand-in plane: model, markers and plane records

**Landed, with B13.** The two land together because a flight session cannot boot without
`weapons.json` and `shakes.json`. `Tooling/SyntheticPlane.cs` adds two families to the synthetic tree.
`plane` writes `planes/` for `probe_plane`: a legacy-shape `nodes.json` and `materials.json` from
`fixtures/synthetic/planes/`, and a `models.json` generated from `boxes.json` (one outward-wound box
per mesh index, fourteen in all). The tree is `geometry` → `healthy` → one LOD (fuselage with
a canopy child, wings, tailplane, fin, `player_damage_on` with `pdp1`..`pdp8`), the `markers` rig
(`firepoint1`..`8` in mirror pairs, `pylon1`..`8` odd to port with `|x|` falling, `target`,
`cockpit_camera`, `exhaust1`/`2`, `ground_level`), `dontmove` (`staticprop1`, `prop1`), `destroyed`
(`piece1`..`3`) and a `cockpit1` interior with `pcdp4`/`pcdp6`. It also writes `zrdr/vehicle.json`
(`player_airplane`, the `pprobe` player def with ten dynamics keys, four zones carrying `pdpanelN`
injure entries and six collision probes, and the `probe` AI def with skills and a `weapons` block),
`engines.json` (three rows, stock power 0.85), `player.json` (gravity 16, `crash`, every
`ai_skill_parameters` pair) and the unit tests' own `maneuvers.json`, plus an empty `C1/zrdr/` scope.
Every value is invented and chosen to fly: `--dump-flight=probe_plane` reads a level top speed of
232 mph against `fd_speed` 268 mph, a 2.9 s roll and a level eighth-throttle cruise. Two seams make
the stand-in the plane a synthetic run flies: `SessionSpec.DefaultPlane` (set by `Launcher` under the
switch, and `Parse` applies it when `--synthetic-data` names no `--plane=`) and
`StockLoadouts.Supplement` (the stand-in's fit, read in place from `fixtures/synthetic/`, whose guns
name `wep_probe_gun` through a new optional `guns[].weapon` key). A real install sets neither.

Verified here: `--stage=empty --fly --synthetic-data --det` boots headless with no `--plane=`,
builds 51 nodes and 19 mesh instances, arms two gun groups and two hardpoints, and logs zero engine
error lines; with `--fire --fire-rockets` nose down from 150 m every gun and rocket impact lands on
`ground/col`. The warnings left are the stand-in's honest gaps: no `camparam.json` (built-in
defaults), no `ai.json` turret table, no sound archive, no tracer, muzzle or compass textures (B14),
and no `plane_reset`/`pdpanelN` anim defs, so torn panels pair by geometry.

**Still owed to the Windows run.** The real-install battery should be unchanged: nothing reads the
new families without the switch, `DefaultPlane` and `Supplement` are only set under it, and a stock
file with no `weapon` key parses as before. The three texture gates added for B11's five suites
skip only under the switch, so on the install `puffer-fire-glow`, `puffer-smoke-sun` and
`tex-dropin` PASS as before. Over an empty root `tier:ci` now needs the switch: without it the 22
plane suites added here skip, which the tier counts as failures. On the real checkout both
`--run-tests=tier:ci` (the plane suites fly the shipped default plane) and `--run-tests=tier:ci
--synthetic-data` (the stand-in, the install named unread) should pass 75/0/0.

**Original approach (kept for reference).**

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

## B13 ☑ Stand-in weapons, shakes and messages

**Landed, with B12** (the record above has the shared part). The `armament` family writes
`zrdr/weapons.json` (`wep_probe_gun`, a 30-calibre `CANNON` at 9 rounds a second and 700 m/s, and
`wep_probe_rocket`, a `HIGH_EXPLOSIVE` rocket with `ACCELERATION`, `LOCK_ON` and an 8 m fuse inside
a 20 m blast), `zrdr/shakes.json` (all six sources, the `high_speed` gate at 1.15 of `fd_speed`) and
`messages.json` (the `MSG_PROBE_*` names and invented wordings of the HUD's crash, kill and
auto-dock keys). New records rather than the unit fixtures, whose tripwire keys and unresolved
effect names are the point of their own tests. The settled question: the ordnance suites need the
ids, not the values. `launch-velocity-decay`, `motor-acceleration`, `ordnance-*`, `disabling-hits`,
`blast-*`, `scorch-decals` and `shootable-flyout` fail with "`wep_NN` … all resolve", so B15
retargets them to a weapon picked by class from the loaded catalogue.

`RunCiSuites.ps1` now passes `--synthetic-data` (`-NoSyntheticData` opts out) and accepts a report
only when it names the tree written for its own Godot process id and `syntheticData: true`; every
other check stands. The guard on this container refused `pwsh`, so the script was not run here:
the same `--run-tests=tier:ci --net-port-base=50000 --synthetic-data` over an empty root reported
75/0/0, its 21 unexpected lines all the text-server pattern the script excuses.

The tier, run with the switch, went from 53 to 75 members, each passing by name with zero
unexpected engine errors and each shown to go red on one broken input in the tree, then restored:
`ai-actor`, `ai-pursues-structure`, `death-camera`, `empty-stage-net`, `remote-airframe` (no control
authority and a 12 m/s `fd_speed`); `flyby-camera` (ten times the drag, mass and gravity);
`inert-aircraft`, `target-input`, `team-model` (zero weapon damage); `cockpit-overlay-pass`,
`targeting-candidates` (marker rig and `cockpit1` renamed away); `cockpit-panel-staging`,
`damage-staging-pool` (no `pdpanelN` entries); `gltf-export`, `hostile-marker-hud` (every box a
point); `plane-shader-reuse` (skin textures absent); `scene-build-throw-frees` (no meshed node with
a child); `flight-roster-transaction`, `flight-telemetry-gate` (no AI def); `flight-live-respawn-gate`
(no fuel tank, so it skips, a tier failure); `bindings-prompt-device`, `hud-auto-dock-line` (an empty
message table). Ten more pass with the kit but no input break turned them red, so they wait for a
code-mutation check in B15: `ai-far-field-plant`, `ai-spawn-jitter`, `cutscene-handoff-speed`,
`debug-kill-target`, `look-stick`, `look-stick-edge`, `muzzle-flash-cockpit-hidden`,
`muzzle-light-first-person-point-term`, `spyglass-marker-hud`, `menu-player-setup-seats`.

Plane-only suites that fail on the stand-in, for B15. Asserting a real record's value:
`ai-gunnery` ("the same bearing is taken at rating 9 (89°)", the shipped `quick_draw_angle`),
`ai-modes` ("player.json min_ai_active_dist is the decoded 2000 m"), `air-to-air` (its fuse
precondition `blastRadius >= 60f`), `graze-bounce` (`IsEqualApprox(stats.BounceFactor, 0.6f)`),
`plane-wobble-walk` (swing gaps of 8 to 11 ticks, "the authored 4 Hz" nitro), `cockpit-interior` (the
shipped interior's named set: `gauges`, `structure`, `nosedamage`, `ggindicator0`, `lowalt_on`,
`stallwarning_on`, `bullet1`..`5` with three or more quads each), `hud-kill-line`
(`player_kestrel`/`medkestrel` and the shipped rows), `menu-free-flight-journey` (the Top Speed of
`player_autogyro`). Naming a shipped airframe: `ai-plane-defs`, `ai-target-rescore`,
`airframe-collider-hit-rate`, `airframe-hull-coverage`, `damage-stage-slots`, `engine-note`,
`flight-mouse-capture`, `flight-mouse-scheme`, `gasbag-ordnance-gate`, `instant-action`,
`warhawk-torpedo-run`, `wing-flare-pose`, `wingman-station`, `loadout-bind`, `loadout-forrig`,
`weapons-fire` (`wep_30` by calibre). Missing a family: the smoke textures (`exhaust-smoke`,
`exhaust-smoke-ai`) and the sound archive (`ai-engine-*`, `engine-*`, `audio-buses`, which skip),
both B14; the `ai.json` turret table (`carried-turrets`, `aircraft-first-targeting`,
`ranked-pool-carried-turret-dedup`, `target-pool` and seven `turret-*`/`world-turrets` suites); a
chapter gamez or `MP1` scope (the `net-*` suites, which skip; C21, C22).

B11's five texture-gated suites: `cockpit-panel-staging` and `damage-staging-pool` now pass and
joined the tier. `puffer-fire-glow` (`fire_f01`), `puffer-smoke-sun` (`smoke101`) and `tex-dropin`
(shipped names) read textures by name that the synthetic archive lacks, so each now calls the new
`TestContext.RequireTexture`, which skips only under the switch, and they SKIP with it.

The full catalog over an empty root without the switch is unchanged at 53 PASS, 2 FAIL
(`build-stamp-focus`, `enet-dual-stack`), 440 SKIP. With the switch it reads 85 PASS, 136 FAIL,
274 SKIP: a `zrdr/` folder now exists, so every suite gated only on it runs, and 63 then miss
`cm_sequence.json`, 11 miss `ai.json`, 13 name a shipped airframe and 49 fail an assertion (shipped
data or names, plus the two environment failures above and two headless-only suites). No tier
member is among them, and CI runs only the tier.

**Original approach (kept for reference).**

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

## B14 ☑ Generated texture and sound archives

**Landed.** The texture half landed with B11 (`Tooling/SyntheticTextures.cs`); this item adds the
sound half and the exhaust trail's sprites. `Tooling/SyntheticSounds.cs` is the `sounds` family, one
`Families` line. It copies `fixtures/synthetic/zrdr/sounds.json` (`SETS` and `SOUND_GROUPS`) to
`zrdr/sounds.json` and writes `soundsh/`, the unpacked sibling of `soundsh.zip`, with one generated
WAV per entry of `fixtures/synthetic/soundsh/manifest.json` (a length, and a tone or seeded noise).
Each WAV is the shape `docs/formats/sounds.md` gives the shipped archive: MS ADPCM, mono, 22050 Hz,
the seven standard coefficient pairs, 256-byte blocks and a `fact` chunk. The encoder is the new
engine-free `Tooling/WavWriter.cs` (PCM16 and ADPCM), which `CSVM.Tests/SyntheticSoundsTests.cs`
round-trips through `WavFile`. The build throws when the manifest and the `SETS` WAV names differ.
The texture manifest gains `smoke101`..`103`, the pool `ExhaustSmoke` names in code.

The stand-in's records now name their sounds: the base def's `cockpit_engine_sound` and
`damaged_engine_sound` (neither carries `FREQUENCY`, the engine loop does), `player.json`'s
`warning_shot_sound` and `bullet_hit_sound` groups and its `rattle` block, the gun's
`LOOPED_SOUND_NAME` and the rocket's `FIRE` sound. Every definition is `snd_probe_*` except the music
cues `MusicPlayer` looks up by literal name (`snd_music_splash` and the seven `music_*_sg` groups),
the move B13 made for the HUD's message keys. Their members are `snd_probe_music_*` over invented
`music_<family>_probe*.wav` files, since the format routes a `mu`-prefixed WAV to the music channel and
`music-states` reads the family off the WAV name. Two settled questions: the generators are shared by
living in `CSVM/src`, as B11 planned; music clips run 2 to 3 s, since `music-states` drives its
hold and fade through `Tick` rather than playback and a clip only has to outlast the suite's frames.

Verified on Linux, headless, empty data root, port base 53000. Nine suites pass with the switch and
joined the tier, each shown red on one broken input, then restored: `engine-damage-phases` (no
`damaged_engine_sound`), `engine-cockpit-pitch` (`FREQUENCY` on the cockpit loop),
`ai-engine-listeners` and `audio-buses` (`engine_sound` naming no definition), `ai-weapon-emitters`
(no `LOOPED_SOUND_NAME`), `music-states` (one primary stinger take), `world-sound-falloff` (no `3D`
definition), `exhaust-smoke` and `exhaust-smoke-ai` (`smoke102` absent). `tier:ci` with the switch
passes 87/0/0; its 37 unexpected lines are all the text-server pattern `RunCiSuites.ps1` excuses.
The guard on this container refused `pwsh`, so the script itself was not run here. The full catalog
with the switch went from 88 PASS, 136 FAIL, 274 SKIP to 97, 141, 260: the nine above, two of them
`exhaust-*` turning FAIL to PASS, and seven suites that used to skip on the missing archive now run
and fail. Without the switch it reads 56 PASS, 2 FAIL (`build-stamp-focus`, `enet-dual-stack`),
440 SKIP, as before.

The seven, for B15 or another family. Asserting a shipped name or value: `ai-engine-rearm` (its log
filter `line.Contains("snd_damagedengine")`; retarget to `stats.DamagedEngineSound`),
`engine-voice-duck` (`Mathf.IsEqualApprox(limit, 0.4f)`, the shipped `voiceover_volume_limiter`, and
the combat line picked by `d.Name.StartsWith("snd_id")`; the tree carries a 3.5 s `QUEUE [45]` line
and a 1 s `QUEUE [0.5]` bark for a pick by `QueueSeconds`), `incoming-fire-cues`
(`stats.BulletHitSound == "bullet_hit_sg"`, every cue line containing `snd=snd_bulletpass` or
`snd=snd_ricochet`, and the canopy line `snd=snd_windowhit` through `window_hit_sg`, the
`CanopyHoleCue.WindowHitSound` constant the tree does not carry). Headless only: `puffer-smoke-sun`
now finds `smoke101`, so `RequireTexture` no longer skips it, and it fails reading MultiMesh instance
custom data back, which the dummy renderer answers with a default colour of alpha 1; it belongs in
`analysis/headless-limits.json` once a Windows-export headless run confirms it, per that file's rule.
Missing a family: `ai-voice` and `voice-runtime` (`zrdr/voice.json`, the combat-voice records),
`campaign-capture-silences-guns` (`cm_sequence.json`).

**Still owed to the Windows run.** No suite body changed and nothing reads the new files without the
switch, so the full `RunTests.ps1` battery should be unchanged. On the real checkout
`--run-tests=tier:ci` (the shipped plane and archive) and `--run-tests=tier:ci --synthetic-data`
(the stand-in, the install named unread) should both pass 87/0/0, since the nine added suites run
the same bodies the battery runs on the install.

**Original approach (kept for reference).**

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

**B15a landed: precise gates under `--synthetic-data`.** A suite that needs an input the invented
tree lacks now SKIPs naming it, so a FAIL with the switch means code or a shipped-value assertion,
never "the tree lacks X". Two new `TestContext` gates sit beside `RequireData` and `RequireTexture`.
`RequireZrdrEntry(zrdrPath, file)` skips when a zrdr ZIP or unpacked folder lacks a reader file
(through the new engine-free `Zrdr.HasFile`, the names `LoadFile` accepts); it is checked on every
tree, since a suite names only entries every install ships. `RequirePlane(nodes)` skips when the
planes gamez lacks a shipped airframe node, only under the switch, the `RequireTexture` kind: every
install carries every airframe, so the real battery never reads it. Gated: 69 suites on
`cm_sequence.json` (the campaign, landings, co-op, cutscene, pause and briefing suites; the gate
sits in `DriveMission` for 13 landings suites and in four mission helpers for 5 co-op suites), 11 on `ai.json`, 2 on `voice.json`, 19 on a
shipped airframe, 4 on the effect readers (`flame_ball.json`, `pufftrails.json`, `fire101`), and
one each on C1's `neindex.json`, C1/IA1's `weather.json`, C4/IA1's `ia.json`, `ia_escape.json`,
`rimage/prog_red.png` and `PX_P_DECALS.TGA`.

Full catalog over an empty root, Linux headless: with the switch 113 PASS, 154 FAIL, 231 SKIP before
and 113, 43, 342 after (111 FAIL to SKIP, no PASS changed); without it 57, 2, 439, unchanged suite
by suite. `tier:ci` with the switch passes 104/0/0. The 43 FAILs, by class:

- **Asserting a shipped value on invented data**, the B15b candidates. AI: `ai-engine-rearm` (log
  filter on `snd_damagedengine`), `ai-gunnery` (rating 9 takes the bearing at the shipped 89°
  `quick_draw_angle`), `ai-modes` (`min_ai_active_dist` is 2000 m, got 1800). Combat and flight:
  `aim-assist` (`sticky_bullet_catchup_rate` 5.0, `forget_interval` 1.5, `dist_factor` 0.0),
  `air-to-air` (a fused rocket with `blastRadius >= 60f`), `graze-bounce`
  (`IsEqualApprox(stats.BounceFactor, 0.6f)`), `plane-wobble-walk` (swing gaps of 8 to 11 ticks),
  `cockpit-interior` (the shipped interior's `gauges`, `lowalt_on`, `stallwarning_on`, `bullet1`..`5`),
  `ground-shadow` (`mask.TriangleCount > 100`, nose-to-tail asymmetry, wing span; it also needs
  `player_autogyro` later). Sound and HUD: `engine-voice-duck` (`voiceover_volume_limiter` 0.4 and a
  `snd_id` combat line), `incoming-fire-cues` (`BulletHitSound == "bullet_hit_sg"`, the
  `snd_bulletpass`/`snd_ricochet`/`snd_windowhit` cues), `hud-kill-line` (the shipped string-table
  wordings; it also needs `player_kestrel` later). Ordnance by shipped weapon id: `blast-curve-cover-cap`
  and `blast-neighbor-shape` (`wep_14`), `burst-light-envelope`, `heat-shimmer`, `scorch-decals`
  (`wep_06`), `impact-orientation` (`wep_06`, `wep_12`), `disabling-hits` (`wep_08/09/12`),
  `flyout-rack-pose` (`wep_15/14`), `launch-velocity-decay` (`wep_14/12/00`), `motor-acceleration`
  (`wep_04/26/12/00`), `ordnance-end-conditions` (`wep_24/12/15/08/14`), `ordnance-guidance`
  (`wep_11/14/10`), `ordnance-impact-effects` (`wep_07/10/11`), `ordnance-launch-axis` and
  `smoke-screen` (`wep_13` with a `SMOKE_SCREEN` time), `shootable-flyout` (`wep_14/06`). Original
  shell layout: `campaign-layout-parity` (the decoded layout against the hardcoded chrome, `CM_B_START`
  pinned to `CM_B_Start.png` and the six measured rows), `menu-original-hangar` (the name pane at
  268,211, the tab bar at 23,524 and 662, `PX_BackGround.jpg`, a `PX_ICON_` composite, the $50000
  note), `menu-original-instant-action` (fourteen contents rows, the arrows and thumb on the authored
  column; its first check also wants the first environment's `IA1/ia.json`).
- **Headless only:** `build-stamp-focus`, `display-mode`, `display-monitor`, `display-resolution`,
  `display-vsync`, `menu-original-tracer`, `menu-screenshot-key`, `muzzle-flash-rides-muzzle`,
  `puffer-smoke-sun`.
- **Environment (no IPv6 loopback):** `enet-dual-stack`, `menu-original-ipv6-address`.
- **A bug the invented data exposes:** `weapons-fire`. `Loadout.ForRig` synthesizes each gun slot
  from the stock spec's `Caliber` and `Ammo` and drops its `WeaponId`, so a stock fit that names its
  weapon outright (the format allows it, `docs/formats/loadouts.md`) binds `wep_30` instead of
  `wep_probe_gun` and throws, against the method's own "a slot the stock fit names keeps its weapon".
  No shipped stock fit names a weapon, so the real battery cannot see it. The suite then pins the
  shipped 48-weapon count, a B15b retarget.

**Still owed to the Windows run.** No assertion changed. On the real extraction every
`RequireZrdrEntry` names a shipped entry and `RequirePlane` does nothing, so the full `RunTests.ps1`
battery should show the same PASS/FAIL/SKIP per suite as before, and `tier:ci` with and without the
switch 104/0/0.

**PFIGHTER landed: `player_pfighter` as an invented stand-in.** The synthetic tree carries a second
airframe under the name the lobby door seats from `PlanePickerRoster.StockAirframes`; its boxes,
markers, dynamics, engine row and sound, AI flavour, `wingman` def and fit are invented
(`fixtures/README.md`). Under the switch its supplement fit replaces the committed def on that model
(`StockLoadouts.Overlay`). `Loadout.ForRig` now keeps a stock slot's named `weapon` (`RigDef`, unit
tested), so `weapons-fire` mounts and fires both invented weapons and fails only on the shipped
48-weapon count, a B15b retarget; a slot naming no weapon binds as before. `net-versus-host-left`
joins the tier (116): three passes with the switch, and red when a released Dogfight guest stops
watching its host. Still red with the switch: `wingman-station` (`settled < baseline + NitroReformM`,
608 m against 312 m of 100 m; the nitro leg's recovery rests on the shipped airframe's speed margin),
`wing-flare-pose` (headless only: its pixel reads return -1 on the dummy renderer, its structure checks
pass), `net-custom-planes` (needs `player_fbrand` and `player_avenger`, the host's and guest's custom
airframes) and `net-kill-line` (its named-host leg now builds and fails on the shipped
`MSG_DESTROYED_BY_X` wording). `net-two-session` still SKIPs on `C1/MP1`. Full catalog with the switch
125 PASS, 50 FAIL, 324 SKIP against 124, 49, 326; without it 58, 2, 439, unchanged suite by suite.
The Windows battery should be unchanged: no shipped fit names a weapon, and `Overlay` runs only under
the switch.

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

## C21 ☑ A code-built spawn table for a match on the empty stage

**Landed.** A `--vs` launch on `--stage=empty` walks `EmptyStage.SpawnRing`, a code-built
free-for-all block of `net.zrd` records: 16 entries (one whole block, so all `NetSeats.MaxPlayers`
seats open apart) on a 600 m ring about the origin at the stage's 300 m spawn altitude. Each nose is
aimed 10 degrees right of the origin, so two opposite seats pass about 208 m abeam; aimed dead at the
origin, a hands-off two-seat run rammed nose to nose. Entry i takes the bit-reversed slot, so
entries 0 and 1 are opposite and 0 to 3 a compass cross. The records are `(position, heading)`
tuples, since `Mech3` names no `Flight` type; `SpawnPicker.LoadSpawnList` maps them to `SpawnPoint`
and sets `NetSpawns`, so `StartState` (the multiplayer throttle and speed), the seat walk,
`PlanTeams` (every team on block 0, the fallback for a map with no team block) and the respawn
rotation run unchanged. `SessionSpec` no longer defaults the `--pos` override over the origin for
`--vs` on the stage, and an explicit `--pos` still wins. The empty stage's log lines read
`[stage=empty ... spawn ring]`; a chapter's read as before, string for string, and every chapter
path is untouched. The single-player stage keeps its one pose over the origin.

In the suites, `NetCombatSuites.Arena` launches a match on `C1/MP1` when the data root carries its
mission zrdr (with the chapter gamez gate as before) and on `--stage=empty` when it does not, and
notes `arena: ...` in the report either way. `MatchSpec`, `net-enet-join`, `net-soak` and
`net-guest-keymap` use it; `net-aircraft-replication` and `net-pause-overlay` now build through
`MatchSpec`. `AirframesFor` seats both roster entries on the spec's plane in the empty arena (the
stand-in under the switch) and the shipped `player_pfighter`/`player_fbrand` pair on `MP1`;
`Roster` takes the spec. The synthetic `player.json` authors an invented `score_suicide` of -3, so
`net-combat-events`' authored-scores control has a value unlike the fallback to read.
`net-two-session` stays on `MP1` alone, since its airframe-crossing check needs two airframes.
The new install-free `versus-spawn-empty-stage` asserts the ring, the two- and four-seat openings,
the team walk, the `--pos` override and the flight control.

The tier goes from 95 to 106: `versus-spawn-empty-stage` and ten network suites, each passing three
consecutive runs with `--synthetic-data` and shown red on a broken input. A ring of radius 0 failed
`net-combat-events`, `net-match-state`, `net-pause-overlay`, `net-relay-star`,
`net-spawn-rotation` and `versus-spawn-empty-stage`; a two-entry ring with seat 1 on an airframe
the tree lacks failed `net-enet-join`, `net-flight-chat`, `net-soak` and `net-start-together` and
skipped `net-guest-keymap` (a tier failure). `net-flight-chat`, `net-match-state` and
`net-spawn-rotation` were not in the survey's eleven and pass on the ring too.

Verified on Linux, headless, empty data root: `--vs --stage=empty --synthetic-data --players=2 --det`
spawns P1 at (0,300,-600) heading 170 and P2 at (0,300,600) heading -10, 1200 m apart and facing
in. The full catalog without the switch reads 58 PASS, 2 FAIL (`build-stamp-focus`,
`enet-dual-stack`), 439 SKIP against 57/2/439 before, the one change being the new suite; every
network suite still skips on `planes gamez not found`. `--run-tests=tier:ci --synthetic-data` passes
106/0/0, its 37 engine error lines all the excused text-server pattern, and the full catalog with
the switch reads 115 PASS, 155 FAIL, 229 SKIP. The guard on this container
refused `pwsh`, so `RunCiSuites.ps1` and the content gate were not run here.

Still off the tier. Asserting a shipped value, for B15: `net-versus-lives`
(`first.StartsWith("guest1 / Destroyed by host / You Have ONE Life Left!")` and
`lines.StartsWith("Game Over: / No Enemies Left / guest1 / Destroyed by host")`), `net-kill-line`
(`host / Destroyed by guest1`, the shipped wording), and `net-aircraft-replication`
(`flight.Flying && flown > 200f && turn > 30f`: the stand-in turns 28 degrees in 240 steps on the
held stick 0.6,0.9,0,1). Needing something else: `net-custom-planes` and `net-versus-host-left`
build through the lobby door, which seats `PlanePickerRoster`'s starter airframe
(`player_pfighter`) over `StockAirframes`, neither in the synthetic tree; `net-rearm-deathmatch`
needs an MP map's `rearm_node_1`/`rearm_node_2`, and `net-team-deathmatch` its team blocks (C22).
These now FAIL with the switch where they skipped, off the tier.

**Still owed to the Windows run.** The full `RunTests.ps1` battery should be unchanged plus one
PASS (`versus-spawn-empty-stage`): the install carries `C1/MP1`, so every touched network suite flies
the real table as before, noting `arena: C1/MP1 and its net.zrd table`. Optionally,
`--run-tests=tier:ci --synthetic-data` on the real checkout should pass all 106.

**Original approach (kept for reference).**

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

## C22 ☑ An invented multiplayer map for the team and flag suites

**Landed.** No real-named map entered the synthetic tree: the empty stage carries the records itself,
in the shapes the real loaders return. A team Dogfight on `--stage=empty` walks
`EmptyStage.SpawnTable`, the free-for-all ring as block 0 and then four 16-entry team blocks in the
staging-stack shape `net.zrd` authors (`docs/formats/net-spawns.md`). Block N stands at team N's base
1.5 km out (team 1 north, 2 south, 3 east, 4 west): a square of four positions 100 m apart on four
rungs 100 m apart from 300 m, the whole block facing the origin. `SpawnPicker.LoadSpawnList` answers
a teamed launch there with the whole 80-entry table, so `PlanTeams`, `SpawnPoints.TeamBlocks`, the
respawn blocks and the rotation run unchanged; an un-teamed launch still walks the 16-entry ring. A
`--vs` build of the stage also stands `EmptyStage.ArenaNodes` (`Build(arena: true)`): two bases, each
a `cs_flag_n` on the ground, its hidden carried `cs_flg_lightn`, and a `rearm_node_n` 300 m inward
and 50 m up, bare nodes carrying the gamez name meta. `FlagRuntime` finds them through the session
tree search it already makes. `RearmRuntime` takes the arena as `Arena` after its world lookup, and
`GameSession.WireRearmBases` opens on either. Every chapter path is unchanged: the arena field is
null there, and the rearm lookup reads the world first.

In the suites, `NetCombatSuites.Arena` takes the mission, so `net-capture-the-flag` flies `C1/MP2`
when the data root carries it (gating the chapter gamez as before) and the stage otherwise.
`net-team-deathmatch` reads its table through a `SpawnPicker` seated on its teams, which on the
install is the same `LoadNetTable` of `MP1`. `versus-spawn-empty-stage`'s team leg now asserts that
four seats on teams 1, 2, 1, 2 open on entries 16, 32, 17 and 33, each at its own team's base. The
synthetic `player.json` adds an invented `score_kill` of 2. With the fallback kill of 1, its
`score_suicide` of -3 made the team suite's target (four kills and a teamkill) 1, so the first kill
ended the match; a kill k serves with any suicide between -2k and 0.

The tier goes from 115 to 116 with `net-team-deathmatch`, which passed three consecutive runs with
`--synthetic-data` beside `versus-spawn-empty-stage`. Both went red with the team blocks replaced by
copies of the ring (the team suite's "no team opening stands on any free-for-all entry's ground"
control, and the stage suite's own-base check), and the team suite went red with `score_kill`
removed, the match ending on the first kill.

Verified on Linux, headless, empty data root, port base 56000: build 0 errors, `dotnet format` clean,
the `-t:Rebuild` StyleCop step reports no `SA` warning, units 5498 pass, 1 fail, 305 skip (the one
failure is `StickRosterTests.ALinuxBuildWithNoSystemSdl2RunsWithoutSticksAndNamesTheSoname`, which
finds this container's system SDL2). `--run-tests=tier:ci --synthetic-data` passes 116/0/0, its 37
engine error lines all the allowed text-server pattern. The full catalog without the switch reads
58 PASS, 2 FAIL (`build-stamp-focus`, `enet-dual-stack`), 439 SKIP before and after, suite by suite.
The worktree guard refused `pwsh`, so `RunCiSuites.ps1` and the content gate were not run here.

Still off the tier, for B15b, with the exact need:
- `net-rearm-deathmatch` runs on the stage: both machines list the arena's two bases under
  `AnyBase` at 25 m, and the restore, the host's full-hull report, once per entry and the re-entry
  all pass. It fails only `Restored`'s `own == Rearmed` (`const string Rearmed = "Rearmed!"`): the
  synthetic `messages.json` has no `MSG_MP_REARMED`, so the pane reads the key.
- `net-capture-the-flag` runs on the stage and every flag rule passes: both flags built at distinct
  homes, the take, the flag hung on the aeroplane, the voice lines, the flag lines, capture and
  return scoring, the float on a death, the catch, the `ejectflag` relay, the throw running out and
  the second capture with its board title. Eight marker checks fail. The `Reads` on `ctf_1`,
  `ctf_2`, `cs_flag_1`, `cs_flag_2` and `cs_flg_light1` expect the shipped "Your Base"/"Enemy Base",
  "Your Flag At Base"/"Enemy Flag At Base", "{Your|Enemy} Flag Captured by {carrier}" and
  "Your Flag Floating"/"Enemy Flag Floating"; the carrier's tag expects
  `$"{carrier}  Holds Your flag"` and reads `MSG_HOLDS_FLAG`; and the away marker's `near is < 30f`
  finds no marker. Two needs: the wording, and a target-site feed on the stage.
  `GameSession.BindsMissionTargetTable` excludes the empty stage and `ObjectiveSites` resolves a
  site through an `AnimRuntime`, so every marker reads "off" there.
- Needing a second airframe or `player_pfighter`: `net-two-session` (the `player_pfighter` and
  `player_fbrand` crossing), `net-seats` (`RemotePlane = "player_fbrand"`, beside its `MP1` and
  chapter zrdr gates), `net-ai-world` and `net-ai-voice` (`--ai=player_pfighter:n=2` over a standing
  destructible pool, the second also shipped voice sets), `net-lobby-deathmatch-mp1` (the menu
  door's `player_pfighter` on `MP1`), `net-custom-planes` and `net-versus-host-left`.
- Shipped wording or data: `net-kill-line` (as C21 lists), `net-player-voice` (the "Jack" and
  "Gruff Male" voice sets).
- `versus-spawn-net-table`: its subject is `SpawnPicker` reading a map's `net.zrd` file. The stage
  reads no file and `versus-spawn-empty-stage` covers its walk, so it would need an invented mission
  zrdr, which this item's arena decision keeps out of the synthetic tree.
- `net-rearm-zeppelins` flies `MP3`'s two hulls, which is bucket E.

**Still owed to the Windows run.** The full `RunTests.ps1` battery should be unchanged: on the
install `net-team-deathmatch` reads the same `MP1` table through the picker, `net-capture-the-flag`
flies `C1/MP2` behind the same gates, the rearm runtime reads the world before an arena that is
null there, and `versus-spawn-empty-stage` (install-free) passes with its new team leg. Optionally,
`--run-tests=tier:ci --synthetic-data` on the real checkout should pass all 116.

**Original approach (kept for reference).**

**Goal.** The team, flag and network AI suites run on an invented mission zrdr with free-for-all,
team and flag blocks.

**Evidence (confidence: lead-only).** The survey counts 14 bucket D suites that need a `net.zrd`
with team blocks (`NetTeamSuites.cs:55` needs more than two blocks), flag records, or a standing
destructible.

**Approach.** <TODO: the record set, from `docs/formats/net-spawns.md` and the flag decode.>

**Model recommendation.** medium.

**Verify.** <TODO>

**⚠ Traps.** Some of these suites pin values from the real C2/MP2 table; those split, as in B15.

## C23 ☑ The Original shell's remaining layout screens

**Landed.** `--synthetic-data` writes the Original shell under `extracted/rof/` as the
`original-shell` family (`Tooling/SyntheticShell.cs`), and 17 Original-shell suites join the tier,
which holds 73. `OriginalAvailability.Load` needs the stamp at the current schema (B11 writes it),
a decoded layout with a `[MainMenu]` section, and every file the asset manifest classes required on
disk with a readable PNG header. The family decodes `fixtures/menu-layout-original/LAYOUT.CSV`
through the extraction's own `MenuLayoutDecoder` against the invented rows of
`fixtures/synthetic/rof/ui_strings.json`, and generates 137 pictures at the invented sizes of
`fixtures/synthetic/rof/art.json`: PNG through `PngWriter`, JPEG and TGA through the new
`Tooling/SyntheticImages.cs`, since Godot picks a decoder by extension and the manifest requires
`.JPG` and `.tga` names. The two backdrop movies stay absent, which the manifest classes optional.
The fixture layout already carried the Options, VIDEO, CONTROLS, KEYS, hangar, campaign, Instant
Action and wrap-up sections; it gains the ammunition screen's backdrop, plane diagrams and text rows
(`OL_T_PLANEINFO` names the seat whose loadout it is). The lobby, connection, join board and team
screens have no layout section: code composes them from script-named `MP_*` art, which `art.json`
sizes.

The joined suites are `display-render-scale`, `menu-backdrop`, `menu-join-board`,
`menu-launch-return`, `menu-player-setup-seats` and twelve `menu-original-*` suites (`boot`,
`builtin-host`, `cheats`, `connection`, `controls`, `coop-boot`, `coop-film`, `coop-flow`, `lobby`,
`lobby-teams`, `outlaw-list`, `version`). Sixteen lost a `zrdr` gate whose only use is the Built-in
launch menu's tolerant stats read, as A2 did for the Built-in menu suites. Three checks that pinned
shipped words now read them from the data root, so they read the same on the install:
`menu-original-controls` takes the KEYS column heads from `[@Keys@]`'s three `KB_T_` rows,
`menu-original-cheats` names the cabin painting by `PC_BACKGROUND`'s art, and `menu-original-lobby`
reads langui 10123 to 10125 from the string table after checking the three are present and distinct
(the synthetic table carries invented rows at those ids). Off synthetic data each of the three
also keeps its old literal check on the shipped words, so a decode that garbles them still fails on
the install. Each joined suite can fail: the other 16
failed with `[@MainMenu@]` renamed, the controls and lobby checks failed on a dropped
`KB_T_CONTTITLEB` and on a 10124 row holding 10123's words, and `menu-player-setup-seats`, whose
Original leg notes an absent layout rather than failing, failed before `OL_T_PLANEINFO` existed.

Verified on Linux, headless, empty data root, port base 52000: the 17 by name with
`--synthetic-data` pass 17/0/0 with no engine error line; `--run-tests=tier:ci --synthetic-data`
passes 73/0/0, its 37 engine error lines all text-server lines `analysis/headless-limits.json`
allows. The full catalog with the switch reads 73 PASS, 10 FAIL, 415 SKIP: B11's five
texture-gated suites, `build-stamp-focus`, `enet-dual-stack`, and `display-mode`, `display-monitor`
and `display-vsync`, which are on `headlessOnly` and now reach their window legs over the synthetic
layout. Without the switch it reads 57 PASS, 2 FAIL, 439 SKIP; the one change is
`menu-player-setup-seats`, whose Built-in leg now runs while its Original leg notes the missing
layout. **CI needs the switch:** `RunCiSuites.ps1` on this base does not pass `--synthetic-data`, so
its tier run reports the 17 as skips until the script passes it.

Still off the tier, with what each lacks:
- `menu-original-hangar` asserts the shipped name pane `PX_PlaneNameBackground.Png` centred at
  268,211, the shipped hub background and the `PX_ICON_` composites. It splits: the walk on the
  tier, the shipped-geometry checks local.
- `menu-original-instant-action` asserts the shipped `ia.zrd`'s ace, the shipped fourteen-row
  contents and the Hellhound's slots. It splits, and its def legs need an invented `ia.zrd` per
  environment.
- `menu-original-campaign`: GO TO FLIGHT CHECK expects a narration to end, which needs a
  `cm_sequence` entry for seq 0, its `brief_c` dialog state, sound and objectives
  (`CampaignBriefing.Load`), all under `zrdr`.
- `menu-original-wrapup`: a C4 gamez with Danger Zone nodes, a C4/IA1 zrdr with stunt zones, and
  `messages.json`.
- `flight-mouse-scheme-live`: `player_bhawk`'s `vehicle.json` and the plane records (B12).
- `menu-original-ipv6-address`: this container has no IPv6 loopback; its gate stays.
- `display-mode`, `display-monitor`, `display-resolution`, `display-vsync` and
  `menu-original-tracer` are `headlessOnly` by construction.

The author's Windows run owes the full `RunTests.ps1` battery, where each of the 17 should PASS as
before: a dropped gate changes nothing where the archive exists, and the three retargeted checks
read the shipped words they pinned. Optionally, `--run-tests=tier:ci --synthetic-data` on the real
checkout should pass all 73.

**Original approach (kept for reference).**

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
