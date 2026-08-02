# CLAUDE.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first — it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to Claude Code as a tool.

## Claude Code specifics

- **Skills** live in `.claude/skills/` (mirrored at `.agents/skills/` via a local junction for
  other `.agents/`-aware tools — see [`AGENTS.md`](AGENTS.md)). Written by
  `/setup-matt-pocock-skills`; edit the files directly. Invoke with their slash commands, e.g.
  `/domain-modeling`, `/commit-next`, `/new-plan`.
- **Hooks:** `.claude/settings.json` runs two `PreToolUse` hooks. (1) A shell-syntax guard that
  rejects a PowerShell here-string (`@'…'@`) sent to the **Bash** tool, and a heredoc or
  `/dev/null` sent to the **PowerShell** tool. (2) `dotnet format` / `dotnet build` before
  `RunTests.ps1`, `dotnet test`, and `git commit`, blocking on remaining StyleCop warnings.
- ⚠ **Multi-line commit messages: `Write` the message to a file, then `git commit -F <file>`.**
  This repo is Windows-primary, so the reflex is PowerShell's `@'…'@` — and the Bash tool does not
  *fail* on it, it accepts the `@` lines as literal text and commits a corrupted message. The `-F`
  form has no shell quoting at all, so it cannot go wrong in either tool. Hook (1) above is the
  backstop, not the plan.
- Agent worktrees/workspaces created with `isolation: worktree` land in
  `.claude/worktrees/` / `.claude/workspaces/` — both gitignored, swept by
  `CleanScratch.ps1`.
