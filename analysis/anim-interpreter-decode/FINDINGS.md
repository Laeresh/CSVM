# How `crimson.exe` executes an animation definition

Question: CSVM's sequence interpreter was inferred entirely from the shipped data. Where does that
inference differ from the original's actual code?

The original's interpreter was located in `crimson.exe` (image base `0x400000`), compiled from
`D:\zipper\gamez\zEffect\zeff_ani*.c`. `anim_census.py` is the companion census over the extracted
install, sizing each divergence found. Run it with the install extracted to `Z:\CSVM\extracted`:

```
python analysis\anim-interpreter-decode\anim_census.py
```

It reads only git-ignored install data and prints counts; it writes nothing.

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
| `START_TIME ANIMATION` | compares the *animation's* clock (`anim+0xb0`), shared across sequences | compares the sequence's own clock | every `ON_CALL` sequence started after t=0 |
| `LOOP` rewind | hard-zeroes both timers | carries the overshoot (`BL-237`) | deliberate — the original paces on its own fixed tick, we must pace on sim time at any step |
| `RANDOM_WEIGHT` | a fixed 200-entry table at `DAT_009fce20` with a **global** cursor, `(i+1) % 200` | per-evaluation RNG | 4,537 conditions |
| `PLAYER_RANGE` | compares `dist² * 4.0 <= value` — **unresolved** whether the transform chain (`FUN_0053f9b0`, `DAT_009fd190`) already halves the vector | `dist² <= value` | 1,052 conditions; if unhalved, every gate radius is 2× too large |

## Event kinds present in the data with no CSVM handler

`Callback` 288 · `FbfxColorFromTo` 152 · `ObjectCycleTexture` 96 · `ObjectDeleteChild` 48 ·
`CameraState` 8 — **592 events**. Everything else the exe's table can dispatch (`Effect`,
`FogState`, `SoundAdjust`, `CameraFromTo`, `ObjectConnector`, `CallObjectConnector`,
`FbfxCsinwaveFromTo`, `DetonateWeapon`, `ObjectMotionSiScriptAllNames`) has **zero** occurrences in
the compiled scope.

`FbfxColorFromTo` is the visible one: `he_ground_effect`'s `frame_buffer_effects1` is a six-step
white↔violet full-screen wash over 1.2 s, gated on `If PlayerRange`, and CSVM renders none of it.

## Disproven along the way

- **"`LOOP` has a run-time form we fail to handle."** The exe implements it (`004ebfd0`,
  `flags & 2`, accumulating into `+0x2c`), but **1,018 of 1,018** compiled `Loop` events carry
  `Count` and none carries `RunTime`; mech3ax notes it is absent from the reader scope too. Nothing
  ships it.
