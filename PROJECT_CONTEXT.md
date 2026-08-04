# PROJECT_CONTEXT.md

Tool-neutral project brief for CSVM. Any coding agent working in this repo should read this
file first — `CLAUDE.md` and `AGENTS.md` are thin, tool-specific pointers into it.

## ⚠ Keep the index files + `docs/` up to date

**This file is the compact, authoritative index of project context; deep detail lives in
`docs/`.** Update documentation in the same turn as the change it describes:

- Module purpose + still-binding constraints, as `⚠` one-liners → the module's `## src/...` entry in `docs/architecture.md` (body ≤ ~8 lines, ~12 for the heaviest; **max 3 `⚠` per module** — a 4th means an existing one merges, moves to a code comment, or dies). **Read a module's entry there before modifying that module.** Diagnosis narratives do NOT go there — they get a short dated `docs/HISTORY.md` entry; what survives of one is a `⚠` line or a verification.md rule.
- A new, renamed or deleted module → **`docs/architecture.md` only**, updating its index line and its `##` entry in the same edit. This file carries the namespace map, never a per-module list; keeping both was 13 KB of duplication and had already drifted.
- Format / reverse-engineering knowledge → `docs/formats/`.
- The extraction pipeline, the launch scripts, or the mech3ax fork → `docs/tooling.md`.
- A way a MEASUREMENT can mislead (non-determinism, an instrument that manufactures its own answer, a masked effect) → `docs/verification.md`, as a transferable rule. A dated `HISTORY.md` entry alone buries it — nobody reads a chronological log before starting work. Shape: a bold 1–2-line imperative + at most one sentence of measured evidence — no narrative.
- Landed work → a dated entry appended to `docs/HISTORY.md` — a few lines: what landed, how verified, outcome — plus tick the active plan's checklist and **swap** the "Current status" next-step pointer. The status section must be no longer after the landing edit than before it; the description of what landed lives in those two files, never here.
- Pure refactors with no external effect → usually no update needed. When in doubt, update.

- If a task is delegated to a subagent use cheaper models when appropriate. For example a cheaper/faster model tier for exploration.

**Budget and shape — this file is an index, not a narrative.** `CLAUDE.md` once grew to 162 KB because every session appended while nobody owned the total; three rules keep that from happening again:

- **Budget: this file stays under ~35 KB.** If a change would push it over, the content belongs in `docs/` and this file gets a *pointer* instead. Check with `(Get-Item PROJECT_CONTEXT.md).Length` before adding a paragraph, not after.
- **Shape: never restate a list another file already indexes — point at that file.** If a section here starts growing one line per *thing*, that list belongs in `docs/` with a pointer in its place.
- **"Current status" holds pointers and item IDs — never a description of landed work, in any tense.** The tripwire is size — over ~15 lines / ~2 KB means history crept in.

**Standing rule — AI-assistance disclosure (decided 2026-07-21).** Every outward-facing communication about this work discloses that it was done with the help of an AI coding agent: PR bodies, issues, discussion posts, comments, and any community writeup — not only an initial submission. Commits carry a `Co-Authored-By:` trailer naming the acting agent and model (e.g. `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`) — whichever agent actually did the work, not a fixed name. The user owns all upstream/community communication, so this is a constraint on what gets *drafted* for them, not an instruction to post anything.

## Project Description

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive, Microsoft): a modern engine that plays the original game using the player's own legally-owned game files.

**Public home: <https://github.com/Laeresh/CSVM>** (`origin`, branch `main`). The repo is named **CSVM**; use that name in outward-facing text and as the CC-BY attribution target for `docs/formats/`.

## 🚫 Hard rule: no game assets in version control — ever

Public open-source project under the **XWVM legal model**: the repo ships **code and format documentation only**. Game files, extracted assets, ZBD contents, screenshots of hexdumps containing bulk asset data — none of it gets committed or published. The importer reads the player's own install at runtime. `.gitignore` enforces this for `CrimsonSkiesGame/`, `extracted/`, and `tools/`; keep it that way when adding directories.

**Licensing (decided 2026-07-22).** Code is **GPL-3.0-or-later** (root `LICENSE`); `docs/formats/` is **CC BY 4.0** (`docs/formats/LICENSE`) so the format knowledge is reusable by permissively-licensed projects. New code files need no per-file header — the root `LICENSE` plus the `README.md` notice covers the repo. The mech3ax fork is **EUPL-1.2** (upstream's license, not a choice) and is a separate tool, not linked into the engine; GPL-3.0 was chosen partly because the EUPL Appendix lists it as compatible, so fork code *could* later be vendored in. Do not relicense any part permissively without checking that chain. `README.md` also carries the not-affiliated-with-Microsoft disclaimer and the AI-assistance disclosure.

## Scratch / probe output

When generating temporary artifacts — probe images, screenshots, rendered
previews, debug dumps, golden-test captures, etc. — always write them into
`./.scratch/` inside this workspace. Create the folder if it doesn't exist.

- Do NOT write scratch output to the OS temp directory
  (e.g. AppData\Local\Temp\claude\...). Files there aren't reachable from
  VS Code's Explorer and clickable links to them don't resolve.
- Use descriptive filenames (e.g. `probe_full.png`, `zbd_texture_dump_01.png`)
  rather than UUIDs, so they're easy to find in the sidebar.
- When you produce such a file, print its workspace-relative path
  (e.g. `./.scratch/probe_full.png`) so it can be opened directly.
- `./.scratch/` is a tmp folder. If anything needs to stay as evidence move it into `./analyis`

## Architecture & key decisions

- **Engine:** Godot 4 .NET (C#). Consumes extraction output via mech3ax / Mech3DotNet (strongly-typed C# wrapper).
- **Format reverse engineering:** happens in a fork of [mech3ax](https://github.com/TerranMechworks/mech3ax) (Rust) at `tools/mech3ax/`. Their byte-identical round-trip test harness (extract→repack) is the correctness standard. **The CS work stays in the fork, not upstream** (upstream dropped CS for maintenance reasons and is dormant) — remotes, branch roles and the sync procedure are in `docs/tooling.md`.
- **Flight model:** data-driven approximation — parameterized by plane stats from extracted zrdr reader files, hand-tuned against the original game. No exe decompilation.
- **Division of labor:** the AI agent writes the Rust parsers, Godot code, and docs; the user reviews, playtests flight feel, and owns upstream/community communication.
- **Git: commit to `main`.** This is a single-developer repo with no PR workflow, so **do not create a branch** when asked to commit — commit straight to `main`. The user will say so explicitly if a particular change should go on its own branch. Pushing is still never automatic: commit when asked, push only when asked.

## Coding conventions
- Comments state what and why, briefly — never provenance (dates, plan/milestone/item references), never history, never instructions to a reviewer. If a comment's only content is where a change came from, it should not exist.

## Repo layout

One line each — **the extraction pipeline, the launch scripts and the mech3ax fork are in `docs/tooling.md`**; none of it is needed to write engine code.

- `CSVM/` — the Godot 4 .NET project (the actual remake; committed). See "Godot project" below.
- `CSVM/shaders/` — shared `.gdshaderinc` blocks `#include`d by the generated shaders (instance-uniform order, sRGB, fog, lights, clock).
- `CSVM/data/` — **committed** hand-authored engine config (not extracted assets), loaded via `res://`. Holds `stock_loadouts.json` (the 11 planes' stock weapon fit; see `docs/formats/loadouts.md`) and `effect_pools.json` (per-root effect-template pool sizes; see `docs/architecture.md` on `EffectPools`).
- `CSVM.Tests/` — the xUnit project (`dotnet test`): reader units on hand-authored fixtures + `extracted/` golden counts, skipped when absent.
- `CrimsonSkiesGame/` — the user's retail install (git-ignored): ZBD archives in `CrimsonSkiesGame/ZBD/` as chapters `C1`–`C5`, each with `IA1` / `M0x` / `MP1`–`3`; cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored), mirroring the game's ZBD structure. Loaders prefer an unpacked sibling folder over its `.zip`. Layout + what is deliberately not loaded: `docs/tooling.md`.
- `ExtractAssets.ps1` — bulk ZBD extractor (`unzbd cs <mode>` per type, fork build, idempotent). Details: `docs/tooling.md`.
- `ExtractRof.ps1` — extractor for the non-ZBD half: the `.rof` UI archives + DLL string tables → `extracted/rof/`. Details: `docs/tooling.md`.
- `RunGame.ps1` / `RunDev.ps1` — play and dev launch scripts (build + Godot; dev one prompts). Details: `docs/tooling.md`.
- `RunTests.ps1` — one command, one exit code: build → `dotnet test` → `--run-tests` → goldens → perf (`-Perf`, A/B'd via the git-ignored `perf-history.jsonl`). Details: `docs/tooling.md`.
- `RunProbe.ps1` — **every ad-hoc scripted Godot launch goes through this** (`--screenshot=`, `--dump-*`, one-off `--run-tests=`): hidden desktop + streams redirected to files, so nothing flashes on screen or prints over the calling terminal. Never invoke the Godot exe directly for a probe. Details: `docs/tooling.md`.
- `HiddenDesktop.ps1` — dot-sourced by `RunTests.ps1`: runs every launch on a separate Windows desktop so no test window ever appears on screen. Details: `docs/tooling.md`.
- `CleanScratch.ps1` — sweeps `.scratch/` artifacts **and finished `.claude/worktrees/` agent worktrees** (`-?` lists its switches). Spares backups and dirty worktrees; leaves branches alone by default.
- `tools/` — downloaded binaries (git-ignored): pinned mech3ax v0.6.1, the mech3ax fork, the Godot 4.7 .NET editor.
- `analysis/` — **committed** read-only analysis scripts + their `FINDINGS.md`, one dir per question. For instruments whose result `docs/` cites, because `.scratch/` is swept. No game data in them, ever.
- `analysis/goldens/manifest.json` — the golden-image tripwire: 11 pinned `--det` shots as command line + raw-pixel md5. Hashes only, never pixels.
- `docs/tooling.md` — the extraction pipeline, the launch scripts, and the fork's remotes/branches/sync procedure.
- `docs/architecture.md` — per-module purpose + still-binding constraints for `CSVM/src`, one `##` entry each. **Read a module's entry before changing it.**
- `docs/formats/` — the public reader-format reference, one page per format family; `README.md` is the index + shared reader conventions.
- `docs/cli.md` — the full per-flag CLI reference (this file keeps only the day-to-day table).
- `docs/verification.md` — how to verify a change here, and how the instruments lie. Read before measuring anything.
- `docs/HISTORY.md` — chronological development log: every landed change with its verification details. Append a dated entry when work lands.
- `docs/plans/` — completed plans, indexed in `plans.md` there; each banner-marked `COMPLETE`, kept for evidence and dead ends, read as history. **A plan sitting in `docs/` rather than in here is live** — see "Current status" for which is active.
- `backlog.md` — unscheduled work: blocked/deferred items, feature backlog, open fidelity questions, and the TUNE list. Move items into a plan when scheduled; **delete when landed — a `FIXED`/closed entry does not stay here.** Its record belongs in `docs/HISTORY.md`; its traps in `docs/verification.md` or `docs/architecture.md`. **If closing it leaves follow-up work, that follow-up becomes its own new entry with a `⚠ Traps` section** naming the rejected fixes and the misleading instruments — an open thread buried inside a section headed `FIXED` is invisible to anyone scanning for work.
- `playtest.md` — the at-the-controls checklist, holding **only what is actionable today** (`PT-nn`) plus one consolidated list of owed captures of the original (`CAP-nn`), each naming the `BL-` it unblocks. IDs are permanent and cited like `BL-nnn`. A test blocked on an unlanded fix does not live here — it rides its `backlog.md` entry as a `*Playtest after fix:*` line and comes back when the fix does. Deep evidence stays in `backlog.md`; keep the two in step.
- `playtest/` — captures staged for a `playtest.md` item, one subfolder per ID (`playtest/PT-23/`). Git-ignored, and unlike `.scratch/` **not swept by `CleanScratch.ps1`** — they survive until the item closes, then the folder goes with it.
- `OriginalScreenshots/` — user-captured reference shots + videos from the original game (git-ignored). `docs/` cites these by filename as evidence, so those citations resolve only in the user's local tree — **ask the user if a referenced capture is missing.**

## Godot project (`CSVM/`)

Godot 4.7 .NET, C# / net8.0. Build & run:

```
dotnet build CSVM/CSVM.sln
GODOT --path CSVM res://scenes/Main.tscn -- --plane=player_bhawk
```

(First time only: run with `--headless --import` once before running scenes.)

**Module map — the per-module index lives in [`docs/architecture.md`](docs/architecture.md), which now opens with it.** Find the module in that index, then read only its `##` entry: `Grep "## src/Flight/FlightModel.cs" -A 12` returns the whole entry. **Never read architecture.md whole** — it is ~110 KB. Read a module's entry before changing it.

- `src/Mech3/` (40) — extraction readers, the GameZ→Godot builders, and the animation runtime: install → live world.
- `src/Flight/` (33) — the aircraft as a flying, shooting, damageable thing, plus its HUD and stunt mode.
- `src/Effects/` (3) — particle systems: puffers, the ambient cloud field, precipitation.
- `src/UI/` (16) — launchscreen, splitscreen rig, and the inspection labs (each with a scripted `--debug-*` twin).
- `src/Utils/` (6) — session-wide services: clock, log, seed, shader time, config, startup profile. Determinism lives here.
- `src/Testing/` (6) — the in-engine assertion harness behind `--run-tests` and the `--dump-*` probes.
- `src/Session/` (8) — `Launcher.cs` (Main.tscn root: bootstrap, launchscreen, persistent camera/lighting) and `GameSession.cs` (the per-launch session node it instantiates), plus livery/spawn/plane-roster resolution, the per-player flight-rig assembler, the effect/crash stage factory, and the weather rig (PLAN-planeviewer-split).
- `src/` root (3) — `SessionSpec.cs`, `SessionPaths.cs`, `Pads.cs`.
- `CSVM.Tests/` — the xUnit project: engine-free reader units. Anything reaching `GD.*` or a live `Node` belongs in `src/Testing/` instead.

Highest-traffic modules, so the common cases skip the index: `GameSession.cs` (session build), `FlightController.cs` (the flying node), `FlightModel.cs` (physics), `SceneBuilder.cs` (every mesh), `WorldBuilder.cs` (chapter worlds), `AnimRuntime.cs` (world animation), `Projectile.cs` (weapon fire), `Suites.cs` (golden counts).

### User args (after `--`)

**Flight is the default.** Any content arg builds a *flight* unless `--viewer` is present: `--plane=player_fury` flies the Fury and `--chapter=C4` flies over C4. `--viewer` gives the static inspection view, where the livery / mesh labs live. The damage lab now lives in both — F5 in `--viewer` drives a parked plane's visuals, F5 in `--fly` drives the flown plane's real HP — so `--fly` is redundant except with `--damage=`, which picks the parked viewer unless flight was asked for by name. A bare launch (no content arg) shows the launchscreen.

The day-to-day 29 of 100 — 100 is both the parser's accepted-flag count and `docs/cli.md`'s flag-index count, kept equal on purpose. **[`docs/cli.md`](docs/cli.md) opens with an index of all of them, grouped**, and each flag's bullet there is the **description of record** — the whole `--debug-*` family, the paint overrides, spawn/mission selection, scripted `--hold` input, the data-path overrides, and the deprecated `--campos`/`--spawn-at`/`--spawn-dir` spellings of the placement pair.

⚠ **These rows are glosses, not the spec: a behaviour change edits the `cli.md` bullet, and a row here only when the gloss went wrong.**  Adding a row is rarely right — the index is one file away.

| Flag | Does |
|---|---|
| `--chapter[=C1]` | which chapter world (`C1`/`C1B`/`C1C`/`C2`/`C2B`/`C3`/`C4`/`C5`); flown by default |
| `--stage=empty` | no *chapter* gamez: a collidable grid ground plane + the plane, booting in ~2 s — the flight/ballistics test stage |
| `--node=<cs_name>` | `--viewer`/`--anim-lab` build only that gamez subtree, auto-framed; multiple matches build the first, a miss lists candidates |
| `--plane=` | which aircraft; comma-separated gives one per splitscreen player |
| `--fly` | free flight (the default): world + skydome + plane + arcade controls; also hosts the damage lab (F5) on the flown plane |
| `--stunt` | flight + the mission's Danger Zones as timed fly-through objectives; a race with `--players` |
| `--viewer` | the static inspection view; hosts the damage (F5), livery (L) and mesh (M) labs |
| `--freecam` | spectator mode: the live animated world, no aircraft, free-flying camera, click-selection |
| `--anim-lab` | the animation debugger: quiet world stage + def playback (`--play-anim=`, `--seed=`) on a fixed-dt clock; a transport button panel, the freecam camera, and click-to-follow the selection |
| `--players=N` | splitscreen 1–4 in one shared world, one pane/camera/HUD/pad each |
| `--pos=x,y,z` | place the mode's **subject**: the camera in `--freecam`/`--viewer`/`--anim-lab`, the plane in `--fly`/`--stunt` (bypassing the mission spawn list) |
| `--direction=x,y,z` | which way it faces there — view direction or nose. `--lookat=x,y,z` is the point form (and the `--viewer` orbit pivot). Quote comma args in PowerShell |
| `--view=1-9` | hold a numpad flight-camera perspective for the run (2 belly, 4/6 flanks, 8 ahead); `--fly`/`--stunt` only. Distance is the plane's shipped one; the layout is disputed (`BL-150`) — [`docs/cli.md`](docs/cli.md) |
| `--screenshot=<path>` | render a few frames, save PNG, quit — the automated-verification workhorse |
| `--frames=N` / `--shots=N` | which sim frame the shot lands on (default 15) — **a sim coordinate, not a wall-clock delay** / capture N consecutive frames |
| `--debug-anim` | log every live animation's pose and sound emitters once a second; conditions only when a verdict **flips** (a repeat line means a change) |
| `--perf` | log the frame-cost/draw-count split every 60 frames (the headless profiler stand-in) |
| `--run-tests[=filter]` | run the in-engine assertion suites, print the PASS/FAIL/SKIP table + `.scratch/test-report.json`, **exit nonzero on any failure** |
| `--log=` | console log filter, `cat[:level],…` over `anim`/`world`/`flight`/`weapons`/`sound`/`perf`/`test`/`ui`/`core`; every run always writes **everything** to `.scratch/logs/` regardless |
| `--det` | the determinism bundle: fixed-dt sim clock + master seed 1 + `--spawn=0` + pinned liveries + `--no-pads` + `--jitter=0`; **implied by every flag that drives and ends a session by itself** — `--screenshot=`, the `--dump-*` reports, `--damage-test`, `--effects-test`, `--weapon-test`, `--run-tests` — and announced as a `det …` log line |
| `--no-det` | opt back out — wall-clock sim and live randomness, **beating both the implication and an explicit `--det`** (`--det --no-det` runs on the wall clock) |
| `--seed=N` | the master seed every subsystem RNG derives from (spread, crash sound, spawn, liveries, anim dice, particles); pinned to 1 by `--det` |
| `--tex-override=<name>[=<color>]` | the named texture resolves flat magenta (or your colour) everywhere it is used — "is this thing drawing at all?" |
| `--tex-census[=names]` | every texture resolves to its own flat colour; map to `.scratch/tex_census.json`, per-texture pixel counts for a `--screenshot` beside it. **Pair with `--no-fog`**; counts are lower bounds — see [`docs/cli.md`](docs/cli.md) |
| `--collision[=show]` | build the world's colliders in a mode that builds none (freecam/anim-lab/viewer); `=show` opens the **C** wireframe overlay — but only in freecam/anim-lab, since C in `--viewer` is the mesh lab's cull cycler |
| `--no-pads` | ignore every gamepad — a drifting stick silently ruins a scripted run |
| `--mute` | skip flight audio — a **load-time** switch, so nothing plays *and nothing is counted or logged*; a muted baseline is blind to sound errors |
| `--volume=N` | master gain 0–1 (default 1). `--volume=0` is silent but **not** blind: audio still loads, plays, counts and logs, so a run is testable from `.scratch/logs/`. Also the `audio.volume` config key, which the flag beats |

the player controls during development are in `docs/controls.md`. **change them if the player input changes**

### Format gotchas

**The cross-cutting gotchas that bite constantly live in [`docs/formats/gotchas.md`](docs/formats/gotchas.md)** —  **Read it before writing any reader, transform, or shader code.**

Full validated format documentation lives in **`docs/formats/`** — one page per format family. **`README.md` there is the index + the shared reader conventions; start there** rather than duplicating its table here. **Rule: new decodes land with their docs page in the same change.**

## Agent skills

Config the installed engineering skills read. Written by `/setup-matt-pocock-skills`; edit the files directly. The skills live at `.claude/skills/`, mirrored at `.agents/skills/` (a local junction — see `AGENTS.md`) so agents that follow the `.agents/` convention find the same set.

### Issue tracker

This repo's own markdown — `backlog.md`, a live `docs/PLAN-*.md`, `playtest.md`. No GitHub Issues yet. See [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md).

### Triage labels

The five canonical roles, unrenamed. See [`docs/agents/triage-labels.md`](docs/agents/triage-labels.md).

### Domain docs

Single-context; this repo's glossary and decisions live in `docs/`, not `CONTEXT.md`/`docs/adr/`. See [`docs/agents/domain.md`](docs/agents/domain.md).

## Current status / next step

**Fixed shape — five short paragraphs, ~15 lines / ~2 KB: this rule, where the project is, active plan + wave position, next items, pointers.** When work lands, the only edits allowed here are: advance the wave-position clause, swap the "Next" IDs, delete text. **Adding a sentence about the landed item is forbidden in every tense** —  If this section is longer after your edit than before it, the edit was wrong.

**Where the project is.** Milestones 1, 2 and 2.5 are delivered (plans indexed in [`docs/plans/plans.md`](docs/plans/plans.md)): 11 flyable aircraft over 8 animated chapter worlds — free flight, stunt mode, or 2–4-player splitscreen, launched from the in-game menu, with original liveries, weather, world animation and sound; extraction is complete and round-trips byte-identically. M3 has since added firing guns and rockets, and world destructibles that take damage, die, lose collision, throw debris and reset. The owed at-the-controls playtests are in [`playtest.md`](playtest.md).

**Active plan:** [`docs/PLAN-armour-layer.md`](docs/PLAN-armour-layer.md) (2026-08-04) — Wave A,
next item A1 (`BL-085`).
**Next candidates:** the plan's checklist. Owed cockpit re-tests
(`PT-13`–`PT-30`) remain in [`playtest.md`](playtest.md). Verify any change with
**`.\RunTests.ps1`** (build → units → in-engine suites → golden hashes → one exit code); read
[`docs/verification.md`](docs/verification.md) first.

Everything else unscheduled — known issues, deferred items, fidelity questions, the TUNE list — is in `backlog.md`; keep it updated as items land or get scheduled.
