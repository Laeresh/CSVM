# How `crimson.exe` executes an animation definition

Question: CSVM's sequence interpreter was inferred entirely from the shipped data. Where does that
inference differ from the original's actual code?

The original's interpreter was located in `crimson.exe` (image base `0x400000`), compiled from
`D:\zipper\gamez\zEffect\zeff_ani*.c`. `anim_census.py` is the companion census over the extracted
install, sizing each divergence found. Run it with the install extracted to `Z:\CSVM\extracted`:

```
python analysis\anim-interpreter-decode\anim_census.py
python analysis\anim-interpreter-decode\start_origin_census.py
```

`start_origin_census.py` is the companion for the `START_TIME` origins specifically: it counts the
explicit `start` objects by origin and by the `seq_state` of the sequence holding them, which is
what sizes the `Animation`-vs-`Sequence` clock divergence. Both read only git-ignored install data
and print counts; neither writes anything.

## Where the code is

| Piece | Address |
|---|---|
| Sequence stepper (one sequence, one tick) | `FUN_004ecbb0` |
| Sequence reset (rewind + re-arm) | `FUN_004ebfa0` |
| Event dispatch table, 47 slots, index 1–0x2f | `DAT_00727de0`, populated by `FUN_004ee1a0` |
| `IF` / `ELSEIF` condition evaluator | `FUN_004ec080` |
| `ELSE` / `ELSEIF` fall-through (scan to `ENDIF`) | `004ec5a0` |
| `ENDIF` | `004ec5d0` (a bare `MOV EAX,2 / RET` — no-op) |
| `LOOP` | `004ebfd0` |
| `CALL_SEQUENCE` / `STOP_SEQUENCE` | `004eb570` / `004eb610` |
| `FBFX_COLOR_FROM_TO` | `FUN_004ec6a0` |

The table's slot ordering matches mech3ax's `EventType` enum exactly, **including the null slot 29**
that mech3ax marks as a gap — an independent confirmation the fork's event numbering is correct.
The exe additionally has real handlers at slots 38, 43, 44 and 45, which mech3ax leaves undecoded;
no shipped def uses them.

## The state machine

The live sequence is mech3ax's `SeqDefInfoC` (64 bytes), mutated in place:

| Offset | Meaning |
|---|---|
| `+0x20` | current state |
| `+0x21` | reset state (`SeqDefState`: `Initial`=0, `OnCall`=3) |
| `+0x24` | **sequence timer** — origin `SEQUENCE` compares against this |
| `+0x28` | **event timer** — origin `EVENT` compares against this |
| `+0x2c` | accumulated loop time |
| `+0x30` | u16 loop counter (counts up) |
| `+0x34` / `+0x38` / `+0x3c` | current event ptr / start ptr / byte size |

⚠ mech3ax's field-name guesses at offsets 36/40/44 (`loop_time`/`event_time`/`seq_time`) are
mis-ordered against this. The names are a guess in the fork and are not load-bearing for
round-tripping, so they were left alone.

Handler return values drive the state byte: **2** = event complete (advance to the next event),
**1** = still running (re-dispatch the *same* event next tick), **4** = sequence rewound by `LOOP`
(re-gate from the top and yield the rest of this tick). State **3** = parked, awaiting a call. The
start-time gate is evaluated only in states 0 and 4 — i.e. only once the previous event reported
completion.

An `ON_CALL` sequence that runs off its end is rewound and re-parked at state 3. An `Initial` one
stops at state 2.

## Confirmed inferences

Read from the data first, and the exe agrees:

- Three `START_TIME` origins, and an absent `start` firing as soon as the previous event completes.
  (The *encoding* of an absent `start` is `Animation + 0.0`, not `Event + 0` — but because the gate
  is only evaluated post-completion, the behaviour is the one we shipped.)
- A `start` gates the event that carries it, not its successor.
- A definition's sequences run concurrently.
- `LOOP` count `0` is infinite. The mechanism: a u16 counter counts up and terminates on
  `counter == authored`, with `-1` special-cased. `0` can only match after 65,536 passes.
- One loop pass per engine update, decoupled from rendering — `LOOP` returns state 4 and the stepper
  returns immediately.
- `ELSE` fall-through skips to `ENDIF`; `ENDIF` itself is inert.
- `ANIM_HEALTH` / `ANIM_HEALTH_RANGE` read the animation's own health (`anim+0xb8`).

## Divergences found, and their size in the data

Census over **3,015 compiled defs** (all 8 chapters' `cam_anim` + `mis_anim`):

| Divergence | Original | CSVM | Shipped occurrences |
|---|---|---|---|
| `CALL_SEQUENCE` re-entrancy | one state per sequence; a call is `if (state == 3) state = 0` — a call into a running or `Initial` sequence is a silent no-op | appends a second concurrent runner | **77** defs call a sequence from >1 site (incl. `sonic_ground_effect → sonic_light_seq ×2`); **6** call an `Initial` sequence |
| `STOP_SEQUENCE` on a parked sequence | unconditionally `state = 2`; since a call needs state 3, the sequence is disabled until the def resets | starts it (the "stopper idiom") | **16** — `flame_ball_01/02 → stop_p1trail`, all chapters, inside the HE explosion's chain |
| `IF` / `ELSE` forward skip | breaks at the **first** `ELSE`/`ELSEIF`/`ENDIF` byte; no depth counter | nesting-aware | **96** nested `If`s, all in `*_gunhit` / `mag_gunhit` — played on every gun impact |
| `START_TIME ANIMATION` | compares the *animation instance's* clock (`anim+0xb0`), shared across sequences | *(fixed — CSVM now resolves it against `AnimInstance.Clock`)* | **191** events name `Animation` with a non-zero time inside an `OnCall` sequence, the only case where the two clocks can disagree; 900 more sit in `Initial` sequences, where the bootstrap starts the sequence with the instance |
| `LOOP` rewind | hard-zeroes both timers | carries the overshoot (`BL-237`) | deliberate — the original paces on its own fixed tick, we must pace on sim time at any step |
| `RANDOM_WEIGHT` comparison sense | `draw <= threshold` | *(fixed — CSVM now uses `<=`, was `<`)* | 4,537 conditions |
| `PLAYER_RANGE` | *(no divergence — the `dist² * 4.0` read here was a misattribution: that comparison is the `0x200` `PLAYER_LINED_UP` branch, an angle test where the 4 cancels a quaternion half-angle. `PLAYER_RANGE` is the `0x2` branch and compares `dist² <= m²`. Traced 2026-08-13; flag-word table in `docs/formats/anim-definitions.md`)* | `dist² <= value` | 1,052 conditions, all gating at the authored radius |

## Event kinds present in the data with no CSVM handler

`Callback` 288 · `ObjectCycleTexture` 96 · `ObjectDeleteChild` 48 · `CameraState` 8 — **440
events** (`FbfxColorFromTo`'s 152 are handled — see below). Everything else the exe's table can
dispatch (`Effect`, `FogState`, `SoundAdjust`, `CameraFromTo`, `ObjectConnector`,
`CallObjectConnector`, `FbfxCsinwaveFromTo`, `DetonateWeapon`, `ObjectMotionSiScriptAllNames`) has
**zero** occurrences in the compiled scope.

## `FBFX_COLOR_FROM_TO` — decoded and implemented

`FUN_004ec6a0`, slot 36, read in full. The 152 shipped events sit in exactly four definitions:
`he_ground_effect`, `ap_ground_effect` and `flak_effect` (6 each × 8 chapters) plus the intro
cutscene's `gi_scene1` (1 × 8). The worked example is `he_ground_effect`'s `frame_buffer_effects1`,
a six-step white↔violet wash over 1.2 s reached through `If PlayerRange 10000 → CallSequence`.

- **Linear in RGBA.** The compiled event interleaves `from`/`to`/`delta` per channel from `+0x0c`,
  `run_time` at `+0x3c`; the handler evaluates `from + t * delta` with `t` = the sequence's event
  timer (`seq+0x28`), `delta` being the compiled `(to - from) / run_time`.
- **Alpha blend.** Past `run_time` the value snaps to `to`; each channel is clamped to `0…1`, RGB
  packed into one frame-buffer pixel and the **alpha handed over separately as a scalar weight**.
  Colour + weight is `dst·(1-a) + colour·a`; a multiply would render `he_ground_effect`'s opening
  white-at-α-0.3 step invisible.
- **One global state, last writer wins.** `FUN_005ca1f0` → `FUN_005ca0e0`, `this` hard-coded to
  `0x9c8a98`: colour at `+0x60`, alpha at `+0x68`, then a virtual that arms it for the current
  frame only. Nothing else writes those fields, so overlapping bursts overwrite rather than
  composite, and a completed chain vanishes on the next frame instead of holding its `to`.
- **Return contract.** 1 (still running) until the run time is up, then 2. CSVM's equivalent is the
  handler reporting `run_time` as the event's duration, which is what spaces the six steps.
- **`alpha_delta` is a round-trip artefact.** mech3ax emits it only when a file's stored delta
  differs bit-for-bit from the recomputed `(to - from) / run_time`; all 8 non-null values are
  `flak_effect`'s `-0.99999994` against `-1.0`, one ulp. Not a parameter, not read.

Full record in `docs/formats/anim-definitions.md`; CSVM's overlay is `UI.ScreenFlash` and the
`fbfx-flash` `--run-tests` suite asserts the six authored run times.

## Disproven along the way

- **"`LOOP` has a run-time form we fail to handle."** The exe implements it (`004ebfd0`,
  `flags & 2`, accumulating into `+0x2c`), but **1,018 of 1,018** compiled `Loop` events carry
  `Count` and none carries `RunTime`; mech3ax notes it is absent from the reader scope too. Nothing
  ships it.

- **"`RANDOM_WEIGHT`'s 200-entry table (`DAT_009fce20`) is a compiled constant worth
  reproducing."** It reads as all zero in the static image (confirmed with `read_memory`) because
  it isn't one: `FUN_004ee380` fills it once per process start with `rand() * (1.0/32767.0)` — 200
  calls to the CRT PRNG, `_DAT_00728358 = 3.051851e-05 ≈ 1/32767` matching MSVC's `RAND_MAX`. And
  the CRT stream itself is not run-stable in the original either — `FUN_004df1d0` and `FUN_0044e010`
  both end with `srand((uint)time(NULL))`, reseeding `rand()` from wall-clock time during ordinary
  startup/level-load. So there is no byte sequence to embed: two runs of the original draw two
  different tables, and the "shared 200-slot cursor" is cycling through whichever draw happened to
  land that session. CSVM's session-seeded `_rng` (`GameSession.cs`, routed through
  `AnimRuntime._rng`) is already the correct-shape equivalent — a PRNG stream pinned by the master
  seed — it just doesn't share the original's specific generator or its cross-condition global
  cursor, and nothing in the evidence says that coupling is worth building. The one real, free fix
  was the comparison sense: the exe's branch is `draw <= threshold`; CSVM had `<` and now matches.
