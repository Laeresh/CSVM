# AGENTS.override.md — pi agent instructions (CSVM)

> This file **replaces** AGENTS.md/CLAUDE.md for pi. `AGENTS.md` and `CLAUDE.md` stay in
> the repo — **other agents read them** — so never merge their content or delete them. The
> rules below are the pi-relevant core, made self-contained so nothing depends on you choosing
> to open another file.

## Non-negotiables (in effect every session — no reading required)

- **Commit to `main`, never a branch.** Single-developer repo, no PR workflow. Push only when
  explicitly asked.
- **Every landed commit carries a `Co-Authored-By:` trailer naming the acting agent + model**
  (e.g. `Co-Authored-By: DeepSeek V4 Flash <noreply@deepseek.com>`, taken from `PI_MODEL`).
  Body = what landed, how it was verified, the outcome — brief; not a narrative.
- **Before touching any module, read it first:** its entry in `docs/architecture/<Namespace>.md`
  (the index in `docs/architecture.md` routes there; grep `docs/architecture/` for the module
  path), `docs/verification.md` before measuring anything, and `docs/formats/gotchas.md` before writing
  any reader/transform/shader. Read `docs/cli.md` before touching a flag's behaviour.
- **Never commit game assets** (game files, extracted assets, ZBD contents, hexdumps with bulk
  asset data). Code + format docs only.
- **Probes/scratch output → `./.scratch/`**, never the OS temp. Print the workspace-relative
  path. `CleanScratch.ps1` sweeps it; `playtest/` and `analysis/` are the durable homes.
- **Launch Godot through `RunProbe.ps1` with sandbox escalation on the first attempt.** Godot's
  `user://logs` directory is outside the workspace; a denied log write can crash Godot 4.7 with
  `-1073741819` before the CLI command runs. Do not waste a sandboxed first launch reproducing it.
- **Update docs in the same change as the code it describes.** New formats land with their
  `docs/formats/` page. Diagnosis narratives go in the commit body, not the docs.
- **`AGENTS.md` / `CLAUDE.md` are not yours to simplify** — they are the pointer files other
  agents consume.

## First step of any task

Open `PROJECT_CONTEXT.md` (the compact, authoritative index) and locate the **active plan**
(`docs/PLAN-*.md`) and its **Current status** section before changing code. Also read the
matching **[skill](.agents/skills/)** before using it (backlog, plan-item, analyse-capture,
commit-next, close-backlog-item, …).

**Use the layered verification loop for `CSVM/` or `CSVM.Tests/` code.** While editing, run the
smallest affected surface: `.\RunTests.ps1 -Suite <suite> -SkipUnits -SkipGoldens` for one engine
suite, or `dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build --filter
"FullyQualifiedName~<test>"` for a unit. The hitch check is opt-in (`-Hitch`), so nothing has to be
passed to keep it out. Use `.\RunTests.ps1 -Quick` for broad development
confidence: it runs the checked-in quick unit and engine tiers and names every surface it did not
check. Before landing code, run the complete `.\RunTests.ps1` (build → units → in-engine suites →
golden hashes → one exit code) only when code under `CSVM/` changed. Quick and targeted runs never
satisfy that landing gate. `CSVM.Tests/`-only, doc, and tooling changes (this file, `.pi/`,
`.claude/`, `docs/`, scripts) do not need the full run. Each stage prints its wall time against a
budget from `analysis/verification-budgets.json`; an `over budget` marker is awareness only and
never changes the exit code.
