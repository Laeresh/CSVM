# Friends Release — minimal shareable build

**ACTIVE PLAN** (written 2026-08-05). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

A minimal release build to hand to friends who own Crimson Skies: a zipped Godot release export
(self-contained .NET) plus a one-command extraction step they run against their own install. No
installer, no code signing, no in-engine setup wizard, no CI release automation — those wait for
the public release, which is itself gated on all instant-action modes being playable. The build
must contain zero game assets (the XWVM legal model); recipients extract from their own retail
install. Engine-side the surface is smaller than it first looks: `--data-root=` and
`CSVM_DATA_ROOT` already exist (added so git worktrees can run the game), so the work is making
the *defaults* survive outside the repo — plus a cache-version stamp so a stale extraction is
detected at boot rather than debugged over chat.

## Milestone goal

- A friend with a retail Crimson Skies install and no dev tools can unzip one archive, run one
  extraction command against their install, and fly: launchscreen → free flight / stunt /
  splitscreen, all 11 planes, all 8 chapter worlds.
- The zip contains only CSVM code, the extraction tool, and licenses/README — zero game data, by
  construction (the export cannot reach `extracted/`, which lives outside the Godot project dir).
- A wrong-version or missing extraction announces itself at boot with a message naming the fix.
- The dev workflow is untouched: `RunGame.ps1`, `RunTests.ps1`, probes, and golden hashes behave
  exactly as before.

**This plan ships a zip to people we talk to weekly — nothing more.** No installer, no signing, no
setup wizard, no CI, no public announcement, no support for non-retail installs. Each of those is
public-release work and waits for the instant-action gate.

## Decisions (2026-08-05)

| # | Question | Decision |
|---|---|---|
| 1 | Who gets this build? | **Friends who own Crimson Skies** — the XWVM model holds only when assets come from the recipient's own install; no copy for anyone without the game. |
| 2 | Extraction UX | **Bundled script + fork `unzbd.exe`, one command** — the in-engine wizard is public-release work; friends tolerate a README. |
| 3 | Where extracted data lives for a release build | **Next to the exe** (`extracted/` beside it), not `user://` — trivially inspectable over chat ("is there a C1 folder next to the exe?"), and it reuses the existing default-root code path unchanged. |
| 4 | Packaging | **Plain zip on hand-off** (no installer, unsigned) — SmartScreen warning is acceptable when we personally tell the recipient to expect it. |
| 5 | Which unzbd to bundle | **The fork build**, not pinned v0.6.1 — the fork is the round-trip-verified extractor; v0.6.1 is the rollback for dev only. EUPL-1.2 text + source link ship alongside (mere aggregation next to the GPL game). |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the engine runs outside the repo

1. ☑ Export-aware root resolution: exe-adjacent defaults when not running from the editor
2. ☑ Extraction version stamp: scripts write it, the engine checks it at boot

### Wave B — the package

11. ☐ Export preset + first hand export, smoke-tested from a bare folder
12. ☑ Friend extraction kit: one-command `Extract.ps1`, bundled fork `unzbd.exe`, README, licenses
13. ☐ Assemble the zip, clean-machine test, first hand-off

## Dependency and parallelism notes

A1 blocks B11 and B13 (an export is unusable until the roots resolve). A2 blocks B12 (the script
writes the stamp A2 defines) but not B11. B11 → B13 and B12 → B13 both chain into the final
assembly. A1 and A2 touch different files (`Launcher.cs`/`Log.cs` vs the extract scripts + a small
boot check) and could run in parallel, but the plan is small enough to just run in listed order.
File contention: none between items except B13, which only consumes the others' outputs.

---

# Wave A — the engine runs outside the repo

## A1 ☑ Export-aware root resolution: exe-adjacent defaults when not running from the editor

**Goal.** An exported release build, sitting in a bare folder with an `extracted/` directory beside
the exe, finds all its data and writes its logs somewhere valid — with `--data-root=` /
`CSVM_DATA_ROOT` still beating the default, and the editor/dev behaviour byte-identical to today.

**Evidence (confidence: traced for the repo paths, direction-sound for export behaviour).**
`Launcher.cs:164-165` derives everything from
`_repoRoot = GlobalizePath("res://") + "/.."` — correct in the editor, where `res://` is the
`CSVM/` project dir on disk. In an exported build `res://` lives inside the pck and
`GlobalizePath("res://")` does not return a usable on-disk directory (Godot documents it as
editor-only for `res://`), so `_repoRoot` degenerates to a CWD-relative path. Confirm the exact
exported return value empirically in B11's first export before trusting any workaround. Consumers
of `_repoRoot` that must survive: `_dataRoot` default (`Launcher.cs:188`), the base extraction
paths (`Launcher.cs:196-201`), `Log.cs:123` (`.scratch/logs/`), `TestHarness.cs:481` +
`TextureArchive.SetScratchDir` + `ProbeRunner` (`.scratch/` writers), and the config dump at
`Launcher.cs:415`. `Config.Load` reads optional `res://config.json` with per-key fall-through
(`Config.cs:11-16`) — absent from the pck it silently uses defaults, no work needed.
`SessionPaths` takes the root as a parameter and needs no change.

**Approach.** Branch on `OS.HasFeature("editor")` at the one spot `_repoRoot` is computed: editor →
today's `res://../`; exported → `OS.GetExecutablePath()`'s directory. Keep the existing override
chain (default → `CSVM_DATA_ROOT` → `--data-root=`) untouched — it already does the right thing
once the default is sane. Scratch/log output in an exported build lands in `.scratch/` beside the
exe (same relative shape, valid absolute base). Do not rename `_repoRoot` project-wide or refactor
the LauncherContext plumbing — this item is the default value, not the architecture.

**Model recommendation.** high — one small edit with project-wide blast radius (every path in the
game flows from this root), and export-vs-editor semantics are easy to get subtly wrong.

**Verify.** Dev side first (the export doesn't exist until B11): full `.\RunTests.ps1` green, and
one `--det --screenshot` golden compared against a baseline taken before the change — the edit is
load-time only, so any pixel drift means it leaked into behaviour. Simulate the exported default
by running from a foreign CWD with `CSVM_DATA_ROOT` pointing at the real `extracted/` (the
worktree case that motivated the env var). Final proof rides on B11: the exported exe in a bare
folder boots to the launchscreen and flies `--stage=empty`.

**⚠ Traps.** `GlobalizePath("res://")` returning *something* non-empty in an export does not make
it right — verify against the exe location, not against "didn't crash". The CWD is a lying
instrument here: launched from Explorer it's the exe dir, so a CWD-relative bug hides; test with a
deliberately foreign CWD. Don't move the dev default off repo-root — `RunProbe.ps1`, the hooks,
and `.scratch/` conventions all assume it.

## A2 ☑ Extraction version stamp: scripts write it, the engine checks it at boot

**Goal.** Every extraction run stamps `extracted/` with what produced it; the engine compares the
stamp at boot and logs a clear one-line warning (stale, missing, or unknown) naming the fix
("re-run Extract.ps1"). A friend's "it looks wrong" report starts from a known extractor version.

**Evidence (confidence: traced).** Nothing like this exists: `ExtractAssets.ps1` /
`ExtractRof.ps1` write only the extraction output, and no loader records provenance. The failure
mode is real — the loaders prefer an unpacked sibling dir over its `.zip`
(`SessionPaths.PreferUnzipped`), so a partially re-extracted tree can silently mix vintages.

**Approach.** `ExtractAssets.ps1` writes `extracted/VERSION.json` at the end of a successful run:
the unzbd binary's version/commit (from `unzbd --version` output — check what the fork actually
prints), a schema integer bumped by hand when readers change incompatibly, the extraction date,
and which script wrote it. `ExtractRof.ps1` adds its own field to the same file rather than a
second file. Engine side: one check where `_dataRoot` is settled in `Launcher._Ready` — read the
stamp, compare the schema integer against a const, `Log` a warning on mismatch/absence. Warn,
don't block: the dev tree has years of valid extractions that predate the stamp.

**Model recommendation.** medium — two well-scoped edits with an existing pattern on each side;
the only judgement is what goes in the stamp.

**Verify.** Re-run `.\ExtractAssets.ps1` (idempotent skip path — confirm the stamp still writes
when nothing re-extracts), boot the game and see no warning; edit the stamp's schema number by
hand and see exactly one warning line naming the remedy; delete it and see the missing-stamp
variant. `.\RunTests.ps1` green.

**⚠ Traps.** The schema integer is a hand-maintained promise, not automation — its bump belongs in
the same commit as any reader change that invalidates old extractions, and that rule needs a line
in the ExtractAssets header comment or it will be forgotten. Don't try to hash the extraction
output for the stamp: gigabytes, and `-Unzip` doubles every payload.

# Wave B — the package

## B11 ☐ Export preset + first hand export, smoke-tested from a bare folder

**Goal.** `CSVM/export_presets.cfg` exists (committed), and a hand-run Windows export produces a
folder that — copied to a bare directory with only `extracted/` beside it — boots to the
launchscreen, flies, and runs splitscreen, on the dev machine but outside the repo.

**Evidence (confidence: traced for what's missing, lead-only for the export mechanics).** No
`export_presets.cfg` exists (globbed 2026-08-05), so no one has ever exported this project; the
export templates for the pinned Godot 4.7 .NET editor in `tools/` are presumably also not
installed. Godot .NET exports need matching templates plus the dotnet publish step the editor
drives; expect first-export friction (missing templates, .NET target settings) rather than code
changes. This item also empirically settles A1's open question: what `GlobalizePath("res://")`
returns in the exported binary.

**Approach.** One Windows Desktop preset: release mode, embedded pck (single exe + the .NET data
dir keeps the zip simple), self-contained .NET so friends install no runtime. Export from the
pinned editor (GUI or `--export-release` — whichever cooperates first; automation is explicitly
out of scope). Smoke-test from a bare folder *and* a foreign CWD with the real `extracted/` copied
or junctioned beside the exe. Commit the preset; document the by-hand export steps briefly in
`docs/tooling.md`.

**Model recommendation.** medium — mostly editor mechanics and checking outcomes; escalate only if
the .NET export fights back.

**Verify.** From the bare folder: bare launch reaches the launchscreen; `-- --plane=player_bhawk
--stage=empty` flies; `--players=2` splitscreen renders both panes; a `--det --screenshot` lands
and its hash matches the same shot from the dev tree (same data, same seed — the export must not
change rendering). Logs appear in `.scratch/` beside the exe, not in the repo.

**⚠ Traps.** The legal guarantee is structural — `extracted/`, `CrimsonSkiesGame/`, `tools/` sit
*outside* the `CSVM/` project dir, so the pck cannot swallow them — but `CSVM/data/*.json` (engine
config, committed) must be IN the pck; if the export filters exclude non-imported `.json`, the
stock loadouts silently vanish and every plane flies unarmed. Check that first if weapons are
missing. The first-run `--headless --import` ritual is editor-only; exported builds carry imported
resources and must not need it.

## B12 ☑ Friend extraction kit: one-command `Extract.ps1`, bundled fork `unzbd.exe`, README, licenses

**Goal.** A friend runs `.\Extract.ps1 "C:\...\Crimson Skies"` in the unzipped folder and ends up
with a complete, stamped `extracted/` beside the exe — ZBDs and rof/strings both — with a README
that covers ownership, the steps, and the SmartScreen warning.

**Evidence (confidence: traced).** Both scripts are already fully parameterized —
`ExtractAssets.ps1 -Source -Unzbd -Dest` and `ExtractRof.ps1 -Source -Dest` — with repo-relative
defaults; the wrapper is argument plumbing, not new extraction logic. `ExtractAssets.ps1` expects
the ZBD tree (`<install>\ZBD`), `ExtractRof.ps1` the `GOSDATA\ASSETS` tree, both under one install
root (layout per `PROJECT_CONTEXT.md`). The engine reads zips directly, so `-Unzip` is optional
polish, not required.

**Approach.** A new thin `Extract.ps1` intended to ship in the zip: takes the install root as its
one argument (help text shows where to find it), validates the two expected subtrees exist with a
friendly error, then calls both repo scripts' logic with `-Source` mapped and `-Dest`/`-Unzbd`
pointed at the package layout (`.\extracted`, `.\tools\unzbd.exe`). Decide at implementation
whether it embeds trimmed copies or the package carries all three scripts and the wrapper just
dispatches — bias toward shipping the existing scripts unmodified plus a small dispatcher, so
there is one extraction code path to maintain. README.md for the package: you must own the game,
the two commands (extract, run), expected disk/time, the SmartScreen paragraph, where logs are
for bug reports. Licenses: GPL-3 text for the game, EUPL-1.2 text + fork source URL for
`unzbd.exe`, the not-affiliated disclaimer and AI-assistance disclosure carried over from the
repo README.

**Model recommendation.** medium, low effort — PowerShell plumbing and prose against settled
decisions.

**Verify.** Point `Extract.ps1` at the real install from a scratch copy of the package layout:
full `extracted/` appears beside the exe, `VERSION.json` stamp present (A2), and the B11 export
boots from it warning-free. Then break it on purpose: a wrong path gets the friendly error, not a
stack trace.

**⚠ Traps.** PowerShell execution policy on a friend's stock Windows blocks `.\Extract.ps1` —
the README must give the `powershell -ExecutionPolicy Bypass -File Extract.ps1 <path>` spelling,
or the very first command fails cryptically. Don't fold the extraction logic into the wrapper "to
simplify" — two diverging extractors is the maintenance trap. The user owns hand-off
communication (standing rule): the README is drafted for them to review, not published.

## B13 ☐ Assemble the zip, clean-machine test, first hand-off

**Goal.** One zip — export + `Extract.ps1` + `tools\unzbd.exe` + README + licenses — proven on an
environment without dev tools, and handed to the first friend with a way for their problems to
reach us.

**Evidence (confidence: traced).** Pure consumption of A1/A2/B11/B12. The only untested claim at
this point is "no dev-machine crutch": the dev box has the .NET SDK, the VC runtime, Godot, and a
warm GPU driver cache — any of which could be masking a missing dependency in the export.

**Approach.** Assemble the layout by hand (a throwaway checklist in the plan is enough — a build
script is public-release work). Test on the cleanest environment available, in order of
preference: another physical machine, a fresh Windows VM, or at minimum a new local user account
with the SDK-less PATH. Run the full friend journey verbatim from the README — unzip, extract,
fly — following only the README's words. Fix what fails, re-zip, hand off, and note the friend's
first-session report (it seeds the public-release backlog).

**Model recommendation.** medium — checklist execution; the value is in honestly following the
README, not in cleverness.

**Verify.** The clean-environment run itself is the verification: every README step works as
written, the game flies all three modes, and `extracted/` was produced by the package's own tools.
A failure here closes the loop back to whichever item lied.

**⚠ Traps.** Self-contained .NET does not cover the VC++ runtime the Godot native side may want —
a clean machine is the only instrument that can catch this class of miss; the dev box cannot fail
this way. Don't "quickly fix" a clean-machine failure by editing files inside the test
environment — fix the package, re-zip, rerun, or the shipped zip and the tested zip diverge.
Hand-off itself is the user's (standing communication rule).
