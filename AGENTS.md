# AGENTS.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first — it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to agent tooling that isn't
Claude Code (see [`CLAUDE.md`](CLAUDE.md) for that).

Follow PROJECT_CONTEXT.md's **Development verification loop**: run the exact affected suite or unit
while editing, use `.\RunTests.ps1 -Quick` for broad development confidence, and run the complete
`.\RunTests.ps1` before landing a change under `CSVM/`.
Quick and targeted runs never satisfy that landing gate; `CSVM.Tests/`-only changes do not require it.

## Agent tooling specifics

- **Skills** are authored under `.claude/skills/` and mirrored at `.agents/skills/` via a local
  junction, so any tool that reads the `.agents/` convention sees the same skill set. Create the
  mirror once per checkout (it's a local link, not tracked in git):

  ```
  New-Item -ItemType Junction -Path ".agents\skills" -Target ".claude\skills"
  ```
