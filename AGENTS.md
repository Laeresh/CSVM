# AGENTS.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first — it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to agent tooling that isn't
Claude Code (see [`CLAUDE.md`](CLAUDE.md) for that).

## Agent tooling specifics

- **Skills** are authored under `.claude/skills/` and mirrored at `.agents/skills/` via a local
  junction, so any tool that reads the `.agents/` convention sees the same skill set. Create the
  mirror once per checkout (it's a local link, not tracked in git):

  ```
  New-Item -ItemType Junction -Path ".agents\skills" -Target ".claude\skills"
  ```
