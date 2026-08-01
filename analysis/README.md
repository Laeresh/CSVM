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

Each subdirectory carries its own `FINDINGS.md` with the verdict, the numbers, and — just as
importantly — the instrument bugs hit on the way, labelled measured / inferred / assumed.

| Directory | Question | Verdict |
|---|---|---|
| `item9-depth-bias/` | Can a conflict-local depth bias fix the C1B/C5 z-fighting? | Viable for C1B, structurally incapable for C5 — they are two different bugs. See `FINDINGS.md`. |
| `anim-debugger-verification/` | Do the anim-debugger waves leave the game byte-identical, and is `--anim-lab` playback deterministic and live-faithful? | Yes on both; `verify.ps1` re-runs every scripted check (incl. the 2×-clock regression test). See `FINDINGS.md`. |
| `gdd-cross-check/` | Which of the design document's corrections to `docs/` survive the shipped data, and what do the undocumented mission readers carry? | Four corrections held, one failed (the `destroyable_parts` 25/20 claim — all 88 entries are equal). Six probes. See `FINDINGS.md`. |
| `m4-ai-data/` | Where is the per-pilot skill vector in the AI vehicle tables (`aiv.zrd.json`), and how many stats did the game actually ship? | Slots 22–30 — **nine** stats on a 1–9 scale, authored only for named pilots (29 blocks); `ia.zrd.json`'s `ace_stats` independently fixes the count at nine. The slot→stat *order* is unresolved. See `FINDINGS.md`. |
| `session-baseline/` | What does each command line actually resolve to, and can it be pinned tightly enough to prove an argument-handling refactor behaviour-neutral? | Yes — 50 command lines x 124 settings, md5-stable across captures, able-to-fail proven; reproducing its md5 is what proved the arg-field deletion behaviour-neutral, and it exposed two latent defects. **Now a RECORD, not a tripwire:** `--dump-session` and `capture.ps1` are deleted, so `baseline.txt` can no longer be regenerated or fail. `CSVM.Tests/SessionSpec*Tests.cs` (83 facts) checks the launch surface instead. See `FINDINGS.md`. |
| `weapon-effects-node-shape/` | Does `gunshell`/`muzzle_burst`'s C1-only "meshless root" shape hold in all 8 chapters? | Same shape everywhere, but "zero real children" was wrong: each root has exactly one child, and `gunshell`'s child (`g1`, model 60) carries a real mesh — `muzzle_burst`'s (`dummy`) does not. See `FINDINGS.md`. |
| `death-effect-closure/` | Does every install-wide destruction-slot effect call enter the live world-effects closure? | Yes: 2,196 of 2,360 calls enter the eight-name handled closure; the other 164 calls are 22 named live-object choreography targets that must remain local. See `FINDINGS.md`. |
| `intersect-surface/` | Is there a data signal marking the C3 spiderweb (and friends) as not collision-participating? | Yes — the gamez node flag `intersect_surface`: false on all 419 distinct non-solid names (props, debris, effects, glows, the web), never on terrain/water/buildings, safely subtree-inheritable. Honoured by `WorldBuilder.NoCollisionNode` since 2026-07-31. See `FINDINGS.md`. |
