# `analysis/` — durable analysis scripts

Read-only scripts that answer a question about the game data, kept **in version control on
purpose**. They read `extracted/**` at runtime and embed no game data themselves, so the hard
"no game assets in VCS" rule does not reach them — but check that again before adding anything
here, and never commit a script with data pasted into it.

**Why this directory exists.** `.scratch/` is git-ignored *and* swept by `CleanScratch.ps1`.
That is correct for probe images and one-off dumps, and wrong for an instrument that took a day
to build and whose conclusion the project still depends on. It has already cost us once:
`.scratch/probe_exempt.py` — the static-collider probe that reproduced the runtime collider
counts in 6 of 8 chapters — no longer exists anywhere, so its backlog item now means "rewrite
the probe first". Anything that is *evidence* rather than *output* belongs here instead.

**What belongs here:** an instrument whose result is cited in `docs/`, or that a backlog item
would have to rebuild before it could be picked up.
**What does not:** rendered frames, hexdumps, screenshots, extraction output, anything derived
from the game's own bytes.
**When it leaves:** a directory that no live file (`docs/`, `backlog.md`, `playtest.md`, source
or test comments) cites any more is deleted. Git keeps every version, and the tag
`analysis-archive` marks the tree before the bulk retirement, so
`git show analysis-archive:analysis/<dir>/FINDINGS.md` recovers a retired one and
`git log -p -- analysis/<dir>` its history.

Each subdirectory carries its own `FINDINGS.md` with the verdict, the numbers, and — just as
importantly — the instrument bugs hit on the way, labelled measured / inferred / assumed.

| Directory | Question | Verdict |
|---|---|---|
| `item9-depth-bias/` | Can a conflict-local depth bias fix the C1B/C5 z-fighting? | Viable for C1B, structurally incapable for C5 — they are two different bugs. See `FINDINGS.md`. |
| `m4-ai-data/` | Where is the per-pilot skill vector in the AI vehicle tables (`aiv.zrd.json`), and how many stats did the game actually ship? | Slots 22–30 — **nine** stats on a 1–9 scale, authored only for named pilots (29 blocks); `ia.zrd.json`'s `ace_stats` independently fixes the count at nine. The slot→stat *order* is unresolved. See `FINDINGS.md`. |
| `bl-229-emitter-host-deactivation/` | May an `OBJECT_ACTIVE_STATE … INACTIVE` stop a puffer emitter that started in the same instant? | No — and it must still stop every other one. The 414 activate/emit/deactivate pairs split 32 same-instant (4 shapes, the splash family, every one authoring a 0.6 s run) against 382 a median 3.5 s later (`BL-224`'s population), with the smallest later gap at 1 ms. See `FINDINGS.md`. |
| `campaign-coop-4p-perf/` | What is the frame cost and hitch behaviour of four panes over the heaviest shipped campaign mission (PLAN-campaign-coop D33)? | `CM18` (C4/M03) at 1P/4P x external/cockpit: draws grow 5.7x from 1P to 4P (faster than the 4x pane count) while render/GPU ms stay inside noise; cockpit view adds ~5% draws at either player count. The mission's one dominant frame-time event, a ~300 ms `ai_spawn` hitch at two fixed sim frames, is identical at 1P and 4P, so it is not a splitscreen cost (filed as `BL-641`; the still-open per-viewport question stays on `BL-434`). See `FINDINGS.md`. |
