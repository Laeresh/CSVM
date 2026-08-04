# The one-frame `CALL_SEQUENCE` dispatch lag: implemented, measured, re-deferred

Measured 2026-08-04 for `BL-135` / `PLAN-m3-polish-6.md` item B12. **Verdict: the bounded
same-pass drain works and is bounded by a number the data itself sizes — but it is no longer
behaviour-neutral. It moves 4 of the 13 golden shots, one of them by 79.7 % of its pixels, and it
fixes nothing observable: no content appears, disappears or ends up anywhere different. It is
re-deferred on that measurement, per this item's own pre-registered stop rule.**

Scripts here are read-only and carry no game data; they read the local `extracted/` tree, launch
probes through `RunProbe.ps1`, and write to `.scratch/bl-135/`.

| Script | Does |
|---|---|
| `census.ps1` | the install-wide CALL/STOP_SEQUENCE call graph per definition: longest chain, every cycle |
| `same-tick.ps1` | how many sequences one definition can start inside ONE tick — the number the bound is sized from |
| `sweep.ps1` | the 8-chapter `--freecam --det --debug-anim` sweep, normalized into diffable per-chapter logs |
| `compare-shots.ps1` | pixel delta (count, %, max channel delta, row span) between two directories of captures |

## 1. The mechanism, re-confirmed

`AnimInstance.CallSequence` appends to `Runners` (`SequenceRunner.cs:84`) while
`AnimInstance.Advance` walks that list **descending** (`SequenceRunner.cs:67`). An appended runner
lands at an index the walk has already passed, so every called sequence's first event fires on the
next tick. (`BL-135` cites `AnimRuntime.cs:998`/`:1485`; the code has since moved into
`SequenceRunner.cs` — the mechanism is unchanged.)

The descending walk is deliberate (`AnimRuntime.cs:250`) and was not touched.

Scale: **22,391** `CallSequence` events across 3,434 compiled definitions, plus 1,143
`StopSequence` (which calls when nothing by that name is running), plus 1,865 `CALL_SEQUENCE`
occurrences in 211 of the 1,355 reader files. Every one of them is currently a tick late.

## 2. Sizing the bound — the data answers it

`same-tick.ps1` walks each definition statically, following only calls that fire before the clock
moves (no positive start offset, no preceding event the dispatch table gives a duration, and
stopping at a `LOOP`, which costs one `AnimFrame`), with every IF branch assumed taken.

| Sequences started in one tick | Entry sequences |
|---:|---:|
| 1 | 4,059 |
| 2–4 | 648 |
| 5–8 | 77 |
| 10–13 | 79 |
| **15** | **59** |

The authored worst case is **15**: every zeppelin's `main_altitude_check` → `rotatezep` →
`breakupzep` → its 13 `break*` pieces, all instantaneous. So a cap of **64** is four times the
deepest thing the install asks for.

The cap is **load-bearing, not defensive**. Restricted to CALL edges the call graph cycles in
exactly one definition — C2/M02's `marypickford`:

```
randomloop --(If RandomWeight 0.8)--> mpickford_bob --> randomloop
           --(Else)-----------------> mpickford_kiss -> randomloop
```

Each hop's only other event is `OBJECT_MOTION_SI_SCRIPT_ALL_NAMES`, which **this engine has no
handler for** — it falls to `Dispatch`'s `default`, is counted, and reports duration 0. So in our
engine that ring is instantaneous, and an unbounded drain spins it until memory runs out. (The 58
definitions the full call+stop graph flags as cyclic are almost all the `StopSequence` self-halt
idiom — `gen_zep`'s `setprop` breaking out of its own IF chain — which halts and never calls.)

Hitting the cap degrades to exactly the current behaviour: the surplus runners fire next frame.
That is what makes capping the safe direction.

## 3. The implementation

Roughly 40 lines, all in `AnimInstance`, plus one reporting helper on `AnimRuntime`:

* `CallSequence` records a runner it appends **during** an advance pass into a reused
  `_startedThisPass` list (a call from outside a pass — a `BOUNCE_SEQUENCE` landing, which
  dispatches between the motion sweep and the instance walk — needs nothing; the walk that follows
  reaches it normally).
* `Advance`, after the descending walk, iterates that list **by index** (draining can append more)
  and advances each with `0f`, not `dt` — the sequence did not exist for that slice of time, so it
  gets whatever is due at its own t=0 and nothing more, the same contract `AnimRuntime.Start`
  already gives a freshly started definition. Runners that finish immediately are removed;
  a runner a later `STOP_SEQUENCE` already halted is skipped.
* Past `MaxSameTickCalls` it sets `DrainCapped` and stops. `AnimRuntime` counts that
  (`CallSequence(same-tick cap)`) and names the definition once — never per frame, since a cyclic
  def would hit it every frame.

It compiled clean, the 22 in-engine suites passed and the engine-error check stayed clean. One
unit test failed, and it failed by asserting the defect:
`SequenceRunnerTests.StopSequenceWithNoRunningTargetCallsItLikeCallSequence` expected
`["on", "trail"]` and got `["on", "trail", "emit"]` — the called sequence's event, now in the same
pass.

## 4. What it actually changes — measured

### 4.1 The 8-chapter sweep: censuses move, the world does not

Every chapter's diff is the same shape. The bootstrap census now **sees** what it previously
missed, because the events happen inside the bootstrap window instead of one tick after it:

| Chapter | bootstrap census, before → after | live at frame 120, before → after |
|---|---|---|
| C1 | 35 → 53 point lights; 2 → 3 sound emitters; 3,070 → 3,102 state ops | unchanged |
| C3 | 1 → 11 puffer emitters; 1,439 → 1,449 state ops | 11 → 11 active |
| C4 | 9 → 15 puffer emitters; 1,061 → 1,067 state ops | 15 → 15 active |
| C5 | 9 → 37 puffer emitters; 1,483 → 1,511 state ops | 36 → 36 active |

This is `docs/verification.md` LOG-2 exactly — the trap `BL-135`'s own notes name ("C1
legitimately reports 38 while 39 emitters exist"). The censuses become **more honest**, but they
were never the behaviour.

The behaviour is the frame-120 capture, and 7 of the 8 chapters are **pixel-identical**
(`--det`, 120 frames): C1, C1B, C1C, C2, C2B, C4, C5. C1's identity is the important one — its 18
extra `bluish_light*` were already alive by frame 120 in the baseline; only the census missed them.

The one moving chapter shot is C3 (`33dfba6a…` vs `faf914d5…`), a waterfall-puffer frame.

The `NodeUndercover`/`NodeBelowAlt` condition evaluations move from the first rendered frame to the
bootstrap (player at the origin rather than at spawn) with the same verdicts, so
`anim: conditions evaluated` gains two kinds in every chapter.

### 4.2 The goldens: 4 of 13 move, deterministically

Both hash sets reproduce exactly across repeated runs, so this is a deterministic change, not
noise:

| Shot | Pixels changed | Max channel delta | Rows |
|---|---:|---:|---|
| `c1-crash` | 734,448 (**79.693 %**) | 179 | 0–719 |
| `c1-destroy-effects` | 2,103 (0.228 %) | 193 | 441–512 |
| `c3-island` | 271 (0.029 %) | 48 | 142–219 |
| `c5-city-night` | 137 (0.015 %) | 31 | 251–286 |

The other nine are identical, including `c1-flight` — the manifest's strongest tripwire.

Every mover is a particle shot, and the difference is phase, not content. Compared side by side,
`c1-crash` has the same camera, terrain, HUD and gauges; its crash fireball and spark cluster are
simply one tick further along, and because that effect covers most of the frame, "one tick further
along" is 79.7 % of the pixels. Particle counts confirm it: C4 114 → 120 and C5 877 → 927 live
particles at frame 120, i.e. one extra frame of emission.

## 5. Why this is re-deferred rather than landed

The item pre-registered its own stop rule: *"If the re-measurement shows real movement beyond the
known one number, stop and re-defer with the measurement written to the backlog entry instead of
landing."* The earlier measurement — "exactly one number moved across all 8 chapters" — **no
longer holds**, and it is superseded by section 4.

Landing would mean re-baselining 4 of 13 goldens, one of them nearly the whole frame, in exchange
for **no observable repair**: nothing appears, disappears, or ends up in a different place; every
runtime total is unchanged; the only difference is that things happen 1/60 s earlier. The golden
manifest's own rule — "a landed visual change updates this file in the SAME commit and names the
shots it moved" — is satisfiable here, but spending the tripwire buys a change whose *direction* is
unverified.

**And the direction is the whole question.** Does the original dispatch a called sequence in the
same tick? Nobody knows. Behaviour-neutrality — then or now — says only that *our* output does not
change; it is not evidence about the original.

**No `CAP-nn` is minted, deliberately.** The observable difference is one tick per hop, and the
authored chains are shallow: the deepest is 3 hops (`main_altitude_check` → `rotatezep` →
`breakupzep`, then a 13-wide fan that costs one more), so ≈50 ms, against a trigger moment that is
not visible on screen. That is below what a video capture of the original can resolve, so filing
an owed capture would file one nobody can satisfy. If this is ever settled it will be from the
original's code, not from film.

The patch is not kept in the tree — a disabled drain is a landmine for the next reader (the
`BL-050` lesson). Section 3 is the implementation, section 2 is the bound and why it is 64, and
section 4 is what to expect the moment it goes back in.
