---
name: new-plan
description: Scaffold a new plan file from the repo's plan template — writes docs/PLAN-<name>.md with the right sections stubbed. Use when starting a new milestone/feature/polish plan. Structure only; the thinking comes from you, the Plan agent, or milestone-polish.
---

You scaffold a new plan, then hand back an empty-but-structured file for the thinking to go into. You do **not** fill in items, evidence, or decisions — that is the author's job (them, the `Plan` agent, or the `milestone-polish` skill). Your job is structure.

## 1. Settle name and shape

`docs/plans/TEMPLATE.md` is the canonical structure — read it first; it is the source of truth for every section, the `[core]`/`[situational]` split, and the wave-letter + number item IDs (`A1`, `B11`, …). Do not restate its content here; follow it.

From the argument and, if needed, one question to the author:
- **Name / path.** A milestone plan → `docs/PLAN-M<n>-<name>.md`; a feature or refactor plan → `docs/PLAN-<name>.md`. Always `docs/` root (a plan there is *live*), never `docs/plans/` (that is for completed plans only). Kebab-case the `<name>`.
- **Weight.** Is this **routine** (core sections only) or **research-heavy** (include the `[situational]` sections too — Decisions, the ⚠ disproven-claims/confidence tables, the data survey)? If the argument doesn't make it obvious, ask once. Default routine.

## 2. Write the file

Create `docs/PLAN-<name>.md` from the template, and:
- Include every `[core]` section always; include `[situational]` sections only if research-heavy.
- Strip the top meta banner and every `<!-- guidance -->` comment — the written plan is clean.
- Fill in only what you actually know: the title, today's date in the ACTIVE PLAN banner (ask the author for the date or have them supply it — do not invent one), and the scope paragraph if the author gave you scope. Leave all item content, decisions, and evidence as the template's `<…>` placeholders for the author to complete.
- Keep the **Ground rules** section verbatim from the template — plans are self-contained, so it is inlined, not referenced.
- Seed the Checklist with one empty wave (`### Wave A — <name>`) and one placeholder item (`A1 ☐ <title>`) plus a matching per-item detail stub, so the shape is obvious.

Then tell the author the file is ready, and point them at how to fill it (their own drafting, the `Plan` agent, or `milestone-polish` for a backlog-driven polish plan).

## 3. Remind them of the lifecycle (don't do it — just point)

The process of *running* a plan lives in other tools; name them so the author knows where to go:
- **Landing each item** — flip its checklist status ☐/◐ → ☑ (or ❌ if disproven), rewrite its per-item section to lead with **Landed.** / **Verified.** and keep the pre-landing text under **Original approach (kept for reference).**, then commit. The commit ritual (docs/HISTORY.md entry, PROJECT_CONTEXT.md "Current status" refresh, message + `Co-Authored-By` trailer naming the acting agent, main only) is the **`commit-next`** skill — use it per item.
- **Completing the plan** — swap the ACTIVE PLAN banner for a `✅ COMPLETE` banner, move the file to `docs/plans/`, and append its row to `docs/plans/plans.md` (that file documents the archive step).
