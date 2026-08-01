# .agents/

Entry point for agent tools that follow the `.agents/` convention rather than Claude Code's
`.claude/` layout. See [`../AGENTS.md`](../AGENTS.md) for the project pointer.

`.agents/skills/` is a local junction to `.claude/skills/` (the tracked source of truth) — it
isn't committed, so create it once per checkout:

```
New-Item -ItemType Junction -Path ".agents\skills" -Target ".claude\skills"
```
