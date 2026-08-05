# `BL-229` — may a host deactivation stop an emitter that started in the same instant?

**Question.** Our runtime ends every PUFFER_STATE emitter under a node when an
`OBJECT_ACTIVE_STATE … INACTIVE` names it (`EmitterDirector.EndOn`, landed for `BL-224`). The sea
dive's `plane_big_splash` writes three offset-less events — activate `sp_1`, call `hg_splasher` onto
it, switch `sp_1` off — so the rule kills the splash spray on the tick it starts, while `hg_splasher`
authors a 0.5 s run. Is the stop wrong, or is that one definition?

**Verdict — the stop is right and needs a date.** A deactivation must not reach an emitter that
started in the same runtime instant; it must still reach every other one. The two populations do not
overlap and nothing sits between them, so the carve-out costs `BL-224` nothing.

| | pairs | authored shapes |
|---|---|---|
| activate/emit/deactivate pairs on one node in one sequence | **414** | 291 |
| … the deactivation shares the emitter start's instant | **32** | **4** |
| … the deactivation is a later instant (`BL-224`'s population) | **382** | 287 |

Measured over **14,963** compiled definitions in all 8 chapters (3,649 distinct animation names),
`extracted/*/*/mis_anim/` + `extracted/*/cam_anim/`. Reproduce with

    python analysis/bl-229-emitter-host-deactivation/census.py extracted [--list]

## What an "instant" is

An event with no `START_TIME` is `EVENT_OFFSET 0`, and none of the three kinds in the idiom reports a
run time, so every zero-offset run of events fires inside ONE `AnimRuntime.Advance` pass
(`SequenceRunner.cs`). That is the granularity the census uses and the granularity the fix uses: the
director stamps each emitter with the `Advance` it started on. The census closes an instant on a
positive `START_TIME` **or** on a duration-bearing event (`OBJECT_MOTION`, the two `*_FROM_TO`s, the
SI scripts); a run time it cannot read counts as nonzero, which under-reports the idiom rather than
inventing it.

## 1. The idiom, and who is in it

The same-instant population is four authored shapes and nothing else — one family, one puffer:

| definition | sequence | node | puffer | started by | authored run |
|---|---|---|---|---|---|
| `big_splash` | `puff_splash1` | `sp_1` | `splasher` | `hg_splasher` | 0.6 s |
| `huge_splash` | `puff_splash1` | `sp_1` | `splasher` | `hg_splasher` | 0.6 s |
| `med_splash` | `puff_splash1` | `sp_1` | `splasher` | `hg_splasher2` | 0.6 s |
| `plane_big_splash` | `plane_puff_splash1` | `sp_1` | `splasher` | `hg_splasher` | 0.6 s |

The 0.6 s is the callee's own: `STOP_SEQUENCE splash` at `ANIMATION_OFFSET 0.5` and
`PUFFER_STATE splasher 0` an `EVENT_OFFSET 0.1` after it. **All 32 same-instant pairs author a run
greater than zero** — there is no case in the install where the data starts an emitter and means the
same-instant deactivation to be its stop. Had there been one, it would have been evidence for the
shipped rule, and the census reports that bucket separately for exactly that reason.

## 2. The two populations do not touch

Authored seconds from the emitter start to the host deactivation:

| population | n | min | median | max |
|---|---|---|---|---|
| same instant | 32 | 0.000 | 0.000 | 0.000 |
| later instant | 382 | **0.001** | 3.500 | 10.000 |

The later population is what `BL-224` built the stop for: `m_build*`, `box_gen_destroy`,
`ftank_boom*` and their kin activate a `partN`, give it a debris trail in the same instant, fly it
with a 3.5–5 s `OBJECT_MOTION`, and only then switch it off. That deactivation is the trail's only
authored stop. A rule keyed on the instant cannot reach them; a rule keyed on a time threshold would
have to pick a number, and the smallest gap in the data is one millisecond.

## 3. The reader source says it is hand-written

The authored reader corpus (1,533 definitions in `extracted/**/zrdr/`) carries **8** offset-less
activate/…/deactivate sequences under the same instant rule, and the four splash definitions are
among them — `big_splash`, `huge_splash`, `med_splash`, `plane_big_splash`, each wrapping a single
`CALL_ANIMATION` around `sp_1`. So the triple is an authoring idiom, not something the compiler
emitted. The other four (`flash_effect`/`lens_flash`, `litehouse_sparking`/`litehsflare`,
`muzzleburst_effects`/`muzzle_burst`, `pzep_launch_player`/`interior`) wrap a call that starts no
puffer on the node, which is why the compiled pass — which requires an emitter to actually start
there — keeps only the four.

## 4. What the rule is

> A host deactivation ends every emitter under that host **except** one that started in the same
> instant. The RESET_STATE path is exempt and stops unconditionally: there every op is a base state
> and "last write wins" is the whole semantics.

Landed in `EmitterDirector.EndOn` (`sparingSameInstant`), stamped in `Assert`, bumped in `Tick`.

## ⚠ The test that had to be rebuilt twice

The suite (`emitter-host-deactivation`) asserts BOTH halves, because either alone is satisfied by a
broken runtime: deleting the stop passes the splash half, shipping it unconditioned passes the
debris half. Running the deletion control is what exposed two dead assertions in the first draft:

1. **A name-only census read answers about the wrong emitter.** Puffer names are not unique across
   definitions — `small_fireball` declares a `trailpuffer2` of its own — so
   `Census.Any(r => r.Name == "trailpuffer2" && !r.Emitting)` was satisfied by the fireball's row
   while the building's trail ran on. Host-qualified now (`EmitterOn(runtime, name, host)`).
2. **A wall-clock assertion cannot separate two stops that land in the same second.** `part1`'s
   deactivation and the whole instance retiring both happen at 5.000 s, so "stopped by 6.5 s" is
   true with `EndOn` deleted. The suite watches `part3` instead and asserts against the DISPATCH
   MOMENT of its own `sparkout3` — with the stop deleted the trail is switched off at 3.667 s and
   still emitting until 5.000 s, which is the leak trap (a) warns about, measured.

A third artefact was real but not a runtime rule: with `flame_ball_02` missing from the synthetic
stage, `small_fireball`'s stop resolved onto the call anchor and ended the building's trail early.
A stage must carry the callees' template roots, not only the caller's own nodes (WORLD-12).

## What this does NOT settle

- **Whether the sea dive looks right.** There is no headless water crash: `--crash` forces
  `CrashSurface.Ground` because it passes no struck body, and the water variant needs a real dive
  into a `water`-tagged collider. The suite plays the real `plane_big_splash` on a runtime carrying
  the crash rig's own role flags, which is the mechanism, not the picture. A capture is owed.
- **The `trailpuffer2` key collision** seen in (1) above is shipped behaviour, not a test artefact:
  on a runtime with un-def-scoped keys, `small_fireball`'s no-AT_NODE stop falls to the call anchor
  and can land on a same-named emitter another definition hosts there. That is the `BL-242` family
  and is untouched here.
