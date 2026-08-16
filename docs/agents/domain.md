# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

Layout: **single-context** — one project, one shared vocabulary, no per-package split.

## Before exploring, read these

All binding:

- **`CONTEXT.md`** — the domain-terminology glossary: the term this project has fixed for a
  concept, and the words to avoid for it. Distinct from the project brief below. Read it before
  naming a domain concept in any output. `docs/adr/` does not exist and is not used; decisions
  live where "Flag decision conflicts" below says they do.
- **`PROJECT_CONTEXT.md`** — the compact authoritative project brief: charter, hard rules,
  module index, CLI flags, current status. `CLAUDE.md` / `AGENTS.md` are thin, tool-specific
  pointers into it — read whichever of those your tool uses, then this file.
- **`docs/architecture.md`** — per-module purpose + still-binding `⚠` constraints,
  one `##` entry per `CSVM/src` module. **Read a module's entry before changing it** —
  it opens with a routing index; find the module there, then
  `Grep "## src/<path>" -A 12`. Never read the file whole (~140 KB).
- **`docs/formats/`** — the validated reverse-engineered format reference;
  `gotchas.md` is the cross-cutting one, read before any reader/transform/shader work.
- **`docs/verification.md`** — how to verify a change, and how the instruments lie.
  Read before measuring anything.
- **`docs/plans/`** and **git log** — decision record. Completed plans keep their dead
  ends; commit messages carry what landed and how it was verified (before the 2026-08-06
  freeze, that record is `docs/HISTORY.md`'s dated entries).

## Use the project's vocabulary

When your output names a domain concept (in a backlog entry, a refactor proposal,
a hypothesis, a test name, a code comment), use the term as the project already
uses it. The vocabulary has three authorities, and all three bind:

- **`CONTEXT.md`** — the terms this project has explicitly fixed, each with the
  words to avoid for it. Where it has an entry, it decides. Add a term there when
  one gets resolved; `/domain-modeling` does this lazily, never speculatively.
- **The original game's own names** — `cs_name` node names, `zrdr` reader keys,
  `PUFFER_STATE` / `LIGHT_STATE` / `SOUND_NODE` and the rest of the anim-def
  vocabulary, chapter IDs `C1`–`C5`. These come from the shipped data. Never
  rename them to something tidier; the extraction and the docs both key off them.
- **The engine's own terms** — as fixed by the module index and entries in
  `docs/architecture.md` (destructible pool, gun group, hardpoint, pylon,
  firepoint, livery/paint scheme, danger zone, chapter world).

If the concept you need has no established term, that's a signal — either you're
inventing language the project doesn't use (reconsider) or there's a real gap
(note it for `/domain-modeling`, which adds it to `CONTEXT.md`).

Comment length and shape are capped; see `PROJECT_CONTEXT.md`'s coding conventions.

## Flag decision conflicts

Decisions here live in commit messages (pre-freeze: dated `docs/HISTORY.md` entries), `⚠` constraint lines in
`docs/architecture.md`, rules in `docs/verification.md`, and the archived plans in
`docs/plans/` (which deliberately keep their dead ends). If your output
contradicts one, surface it explicitly rather than silently overriding:

> _Contradicts the `⚠` constraint on `src/Mech3/SceneBuilder.cs` — but worth reopening because…_

A rejected approach recorded in an archived plan is evidence, not a suggestion:
check *why* it was rejected before proposing it again.
