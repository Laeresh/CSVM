# Completed plans

The index of every plan that has run to completion, oldest first. The plan files live in
this directory, each banner-marked `COMPLETE` with its date; they are kept for their
evidence and recorded dead ends, and read as history, not live work. **A plan sitting in
`docs/` rather than in here is live** — CLAUDE.md's "Current status" names the active one
when more than one is present.

**Starting a new plan?** Run **`/new-plan`** — it scaffolds `docs/PLAN-<name>.md` from
[`TEMPLATE.md`](TEMPLATE.md), the shared plan skeleton (status banner, ground rules, checklist,
per-item shape), with the right sections stubbed for you to fill in.

**When a plan completes:** move its file into this directory and append its row here.

| Plan | Scope | Completed |
|---|---|---|
| [PLAN-M2-polish.md](PLAN-M2-polish.md) | M2 polish run 1 (8 items) | 2026-07-17 |
| [PLAN-M2-polish-2.md](PLAN-M2-polish-2.md) | M2 polish run 2 (13 items) | 2026-07-19 |
| [PLAN-M2.5-prototype.md](PLAN-M2.5-prototype.md) | Stunt mode, launchscreen, splitscreen (7 items) | 2026-07-19 |
| [PLAN-mech3ax-cs-revival.md](PLAN-mech3ax-cs-revival.md) | The fork: anim + gamez/planes support (14 items) | 2026-07-21 |
| [PLAN-anim-playback.md](PLAN-anim-playback.md) | The animation engine (7 items) | 2026-07-21 |
| [PLAN-anim-rendering-followups.md](PLAN-anim-rendering-followups.md) | Conditions, lights, world setup, UV scroll, audio (4 items) | 2026-07-22 |
| [PLAN-docs-cleanup.md](PLAN-docs-cleanup.md) | Shrink CLAUDE.md back to an index (10 items) | 2026-07-22 |
| [PLAN-M2-polish-3.md](PLAN-M2-polish-3.md) | M2 polish run 3 (10 items; 3 and 11 closed as disproven) | 2026-07-22 |
| [PLAN-M2-polish-4.md](PLAN-M2-polish-4.md) | M2/2.5 polish run 4 (10 items; item 3 returned to backlog) | 2026-07-23 |
| [PLAN-anim-debugger.md](PLAN-anim-debugger.md) | The `--anim-lab` animation debugger (Waves 1–5) | 2026-07-23 |
| [PLAN-data-driven-crash.md](PLAN-data-driven-crash.md) | Data-driven crash: generic anim handlers + the crash plays its def (4 waves) | 2026-07-23 |
| [PLAN-doc-prune.md](PLAN-doc-prune.md) | Prune architecture.md + verification.md, comment sweep, doc-routing contract (4 waves) | 2026-07-23 |
| [PLAN-M3-weapons.md](PLAN-M3-weapons.md) | M3 weapons & destruction — guns/rockets, world destructibles, effects, HUD (Waves A–F) | 2026-07-25 |
| [PLAN-testing.md](PLAN-testing.md) | Deterministic test infrastructure + the inspect layer (18 items) | 2026-07-25 |
| [PLAN-sessionspec.md](PLAN-sessionspec.md) | `SessionSpec`: the launch args as one parsed, resolved, engine-free value (8 items, Waves A–B) | 2026-07-30 |
| [PLAN-planeviewer-split.md](PLAN-planeviewer-split.md) | PlaneViewer god-class breakup: Launcher / GameSession split (11 items, Waves A–C) | 2026-07-30 |
| [PLAN-sequencerunner-seam.md](PLAN-sequencerunner-seam.md) | Extract the sequence interpreter behind `ISequenceHost` + headless xUnit charter (2 items, Wave A) | 2026-07-30 |
| [PLAN-animruntime-role-factories.md](PLAN-animruntime-role-factories.md) | `AnimRuntime` role factories (`ForEffects`/`ForCrashRig`) + delete dead `Apply` (5 items, Waves A–B) | 2026-07-30 |
| [PLAN-m3-polish-quickwins.md](PLAN-m3-polish-quickwins.md) | M3 polish quick wins: gauges/cues, spurious errors, weapon/audio plumbing, doc drift (11 items, Waves A–D) | 2026-07-30 |
| [PLAN-m3-polish-2.md](PLAN-m3-polish-2.md) | M3 combat look & feel: anim triggers, impacts/surfaces, weapon secondary visuals, destruction bugs (14 items, Waves A–D; cockpit follow-ups → PLAN-m3-polish-3) | 2026-07-31 |
| [PLAN-m3-polish-3.md](PLAN-m3-polish-3.md) | M3 polish run 3: the 2026-07-31 cockpit-pass follow-ups — imperceptible feedback, destructible behaviour, weapon visuals, rockets (11 items, Waves A–D) | 2026-08-01 |
| [PLAN-m3-polish-4.md](PLAN-m3-polish-4.md) | M3 polish run 4: impact feedback, test affordances, blast radius (10 items; 1 disproven) | 2026-08-01 |
| [PLAN-m3-polish-5.md](PLAN-m3-polish-5.md) | M3 polish run 5: effect reachability, world-render fidelity, the instruments (10 items; 1 disproven) | 2026-08-02 |
| [PLAN-bounce-launch.md](PLAN-bounce-launch.md) | Bounce-terminated launches: solve + fly + land the 150 upward `BL-240` debris pieces (6 items, Wave A; falls split out as `BL-245`) | 2026-08-03 |
| [PLAN-deepening.md](PLAN-deepening.md) | Seven-module deepening: extract seams, move invariant enforcement from caller to module (Waves A–G; G18/G19 as design decisions) | 2026-08-03 |
| [PLAN-weapon-lab.md](PLAN-weapon-lab.md) | Weapon lab as a flight mode: held aircraft in a real world, full-rig loadout, click-to-place targeting, camera switch, `WeaponBench` (10 items, Waves A–D) | 2026-08-03 |
| [PLAN-m3-polish-6.md](PLAN-m3-polish-6.md) | M3 polish run 6: surfaces (node `active`, second material pass, `zone_set`, depth-bias rank), the anim runtime (dead delta channels, dispatch-lag re-deferral), the instruments (golden exit-1, collider probe), and the C3 magenta decision (10 items, Waves A–D) | 2026-08-04 |
