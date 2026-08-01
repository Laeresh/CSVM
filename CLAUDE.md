# CLAUDE.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first — it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to Claude Code as a tool.

## Claude Code specifics

- **Skills** live in `.claude/skills/` (mirrored at `.agents/skills/` via a local junction for
  other `.agents/`-aware tools — see [`AGENTS.md`](AGENTS.md)). Written by
  `/setup-matt-pocock-skills`; edit the files directly. Invoke with their slash commands, e.g.
  `/domain-modeling`, `/commit-next`, `/new-plan`.
- **Hooks:** `.claude/settings.json` runs a `PreToolUse` hook (`dotnet format` / `dotnet build`)
  before `RunTests.ps1`, `dotnet test`, and `git commit`, and blocks on remaining StyleCop
  warnings.
- Agent worktrees/workspaces created with `isolation: worktree` land in
  `.claude/worktrees/` / `.claude/workspaces/` — both gitignored, swept by
  `CleanScratch.ps1`.
