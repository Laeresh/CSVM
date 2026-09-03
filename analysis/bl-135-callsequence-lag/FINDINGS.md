# The one-frame `CALL_SEQUENCE` dispatch lag: measured, decoded, resolved

⚠ **Read section 6 first.** The engine now reproduces the original's ascending slot walk, so the
lag is gone and the drain measured in sections 2-5 was never the right mechanism. Those sections
are kept as the record of what the drain cost, not as a description of the engine.

Measured 2026-08-04 for `BL-135` / PLAN-m3-polish-6 B12. **Verdict: the bounded
same-pass drain works and is bounded by a number the data itself sizes — but it is no longer
behaviour-neutral. It moves 4 of the 13 golden shots, one of them by 79.7 % of its pixels, and it
fixes nothing observable: no content appears, disappears or ends up anywhere different. It is
re-deferred on that measurement, per this item's own pre-registered stop rule.**

⚠ **Sections 2, 3 and 5 are superseded by the decode.** `crimson.exe` walks a definition's
sequences as one ascending pass over a fixed array and `CALL_SEQUENCE` writes state into the
callee's own slot, so a call is same-tick exactly when the callee's index is higher than the
caller's ([`docs/org/sequences.md`](../../docs/org/sequences.md), "The tick walk is ascending").
Three consequences for what is written below: the drain measured here made **every** call same-tick,
which is not what the original does; the sized cap of 64 (§2) guards a spin the original's walk
makes impossible, so it is an artefact of our runner-list model; and §5's "the direction is the
whole question" is answered, which unblocks the item. Section 4's measurement stands as a record of
what the drain cost, not as a preview of what matching would cost.

Scripts here are read-only and carry no game data; they read the local `extracted/` tree, launch
probes through `RunProbe.ps1`, and write to `.scratch/bl-135/`.

| Script | Does |
|---|---|
| `census.ps1` | the install-wide CALL/STOP_SEQUENCE call graph per definition: longest chain, every cycle |
| `same-tick.ps1` | how many sequences one definition can start inside ONE tick — the number the bound is sized from |
| `sweep.ps1` | the 8-chapter `--freecam --det --debug-anim` sweep, normalized into diffable per-chapter logs |
| `compare-shots.ps1` | pixel delta (count, %, max channel delta, row span) between two directories of captures |
| `call-index-order.ps1` | per CALL edge, whether the callee's array index is forward or backward of the caller's — the share of the install our uniform lag gets wrong |

## 1. The mechanism, re-confirmed

`AnimInstance.CallSequence` appends to `Runners` (`SequenceRunner.cs:84`) while
`AnimInstance.Advance` walks that list **descending** (`SequenceRunner.cs:67`). An appended runner
lands at an index the walk has already passed, so every called sequence's first event fires on the
next tick. (The mechanism was first traced at `AnimRuntime.cs:998`/`:1485`, before the interpreter
moved into `SequenceRunner.cs`; it was unchanged by the move.)

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

This is `docs/verification.md` LOG-2 exactly: the bootstrap census is printed inside `Bootstrap`
and so cannot see anything created after it, which is why C1 legitimately reports 38 while 39
emitters exist. The censuses become **more honest**, but they were never the behaviour.

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

## 6. The decode, and how much of the install our lag gets wrong

`crimson.exe` walks a definition's sequences as one **ascending** pass over a fixed array (base
`anim+0xcc`, count `anim+0xd8`, stride `0x40`; loop `004ecedc`–`004ecf53`), stepping each slot at
most once and re-reading its state byte each iteration. `CALL_SEQUENCE` (`004eb570`) writes state 0
into the callee's own slot at `base + index*0x40` (`004eb5fa`–`004eb605`). A call therefore runs in
the same tick exactly when the callee's index is **higher** than the caller's, and defers when it is
lower or equal. Full decode: [`docs/org/sequences.md`](../../docs/org/sequences.md), "The tick walk
is ascending".

`call-index-order.ps1` classifies every compiled CALL edge on that test:

| Bucket | Events | Share | The original |
|---|---:|---:|---|
| forward (callee index higher) | 22,057 | 98.51 % | same tick |
| backward | 116 | 0.52 % | next tick |
| self | 0 | 0 % | next tick |
| unresolved (name not in the def) | 218 | 0.97 % | no-op |

Of the 22,173 edges the walk actually sees, **99.48 % are forward**, so the original dispatches
essentially every call in the same tick and CSVM defers all of them. This is not one outlier
inflating a total: the six `gasbag*` definitions contribute about 15,000 edges, and excluding them
the forward share is still **98.29 %** (6,663 of 6,779). By definition rather than by edge, **398 of
the 403** definitions carrying a call have at least one forward edge, 15 have any backward edge, and
exactly **one** is backward-only.

**The index the census compares is the original's index.** The array is append-only, one slot per
`SEQUENCE_DEFINITION` in loader-encounter order: `FUN_0051c350` reallocs to `(count+1) * 0x40`,
returns the record at the old count and bumps the count byte, refusing past 255 with
`Sequence list overflow`. Its only caller is the definition loader `FUN_0051dcf0`, which calls it
from a linear scan of the body, so nothing sorts or reorders. The compiled archives hold that same
64-byte `SeqDefInfoC` record in array order and `CompiledAnim.Parse` appends them in file order, so
the JSON position this census reads is the slot index the walk uses. `RESET_STATE` and
`DAMAGE_SEQUENCE` are standalone `calloc`ed records at `anim+0xd0` / `anim+0xd4`, outside the array
and outside the walk, which is why they are excluded here.

Two of the 116 backward edges settle arguments this file already had:

* **`marypickford` behaves exactly as the walk predicts.** Its ring is `randomloop` (idx 2) →
  `mpickford_bob` (idx 3) → back to `randomloop`, so the outbound hops are forward and the return
  hop is backward. The original breaks the ring at that back edge once per tick, which is why it
  needs no cap and cannot spin. Section 2's cap of 64 guards a hazard only the runner-list model
  has.
* **The police siren is a backward call.** `police_car` runs `start_walkin` (idx 2) →
  `siren_police` (idx 1), so the original defers it too, and our behaviour there already matches.
  The drain would have made that call same-tick, which is *further* from the original, not closer.
  The item was born from this call; it turns out to be one of the 0.52 %.

### What matching it actually cost

`AnimInstance` holds a slot per `Def.Sequences` entry and walks them ascending, with the death slot
and the damage-stage host unslotted and stepped after. The bound, the drain and the
same-pass-append list are all gone; no cap is possible to need, since no slot is stepped twice in a
pass. One latent bug surfaced: `SequenceRunner.Advance` added `dt` unconditionally, which for a
runner born inside the pass desynced its clock from the instance's and fired `SetDue`'s ANIMATION
restatement a tick early. It now withholds that first `dt`, which is also the original's rule.

Seven of the 15 goldens moved, all of them phase and none of them content, measured against a
baseline build of the same tree:

| Shot | Pixels changed | Max channel delta | Rows |
|---|---:|---:|---|
| `c1-debris-rest` | 26,973 (2.927 %) | 189 | 149-360 |
| `c1-crash` | 13,721 (1.489 %) | 198 | 241-450 |
| `c1-targeting-hud` | 592 (0.064 %) | 26 | 314-467 |
| `c1-destroy-effects` | 289 (0.031 %) | 14 | 462-523 |
| `c5-city-night` | 61 (0.007 %) | 33 | 252-286 |
| `c3-island` | 53 (0.006 %) | 21 | 149-218 |
| `c2-city` | 25 (0.003 %) | 93 | 342-488 |

`c1-flight`, the manifest's strongest tripwire, is unchanged. Overlaying the changed pixels on each
shot puts every one of them inside a smoke plume or a muzzle puff: terrain, structures, debris,
aircraft, HUD text and gauges are untouched. Note `c1-crash` at **1.5 %** against the drain's
**79.7 %** on the same shot, which is the difference between "every call same-tick" and "same-tick
only when the callee is declared later".
