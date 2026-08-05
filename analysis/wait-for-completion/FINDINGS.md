# `WAIT_FOR_COMPLETION` is a blocking call flag; its number is symbol-table bookkeeping

`census.py` walks every extracted JSON file, separates the compiled semantic field from the
unflagged raw slot, and dumps the 92 nonzero flagged calls with their owning definition and
`anim_refs` table. Run it from the repository root:

```
python analysis/wait-for-completion/census.py --dump .scratch/wait-for-completion.json
```

The dump is temporary because it contains one row per extracted event; only this aggregate finding
and the reusable instrument belong in version control.

## Finding (2026-08-01)

The reader's bare `WAIT_FOR_COMPLETION` token makes `CALL_ANIMATION` synchronous: the caller does
not advance past that event until the named callee completes. In compiled e24, flag `0x10` says the
16-bit wait slot is live, and the value is a zero-based index into the caller's `anim_refs` table.
It is not an alternate animation target and does not select some unrelated connector.

The exhaustive compiled census reproduced the inherited 56,750-event distribution exactly:

| semantic `wait_for_completion` | events |
|---|---:|
| absent (`null`) | 53,019 |
| index 0 | 3,639 |
| indices 1–6 | 92 |

All **3,731** flagged indices are in range, and all **3,731** indexed refs name the same callee as
their `CallAnimation`; the requested 92 nonzero rows are 92/92 on both checks. Therefore zero and
absent are different authored states: zero means “wait for the first ref,” while absent means “do
not wait.” The reader sources corroborate the flag directly: 147 `CALL_ANIMATION` bodies and 33
local `CALL_SEQUENCE` bodies carry the bare token.

The adjacent unflagged slot is a separate concern. The fork preserves its 525 non-`-1` values as
`wait_for_raw` because Crimson Skies leaves stale small integers there without flag `0x10`; those
values must never be interpreted as waits.

## Runtime decision (2026-08-01) — SUPERSEDED 2026-08-05, see below

No scheduler code landed with the decode. All flagged compiled owners are `OnCall` (2,844) or
`WeaponHit` (887), not `OnStartup`, and the clearest supported-data timing case is the water-crash
chain (`plane_big_splash` must finish before `large_steam_spray`), which was not reachable until
surface-aware crash selection landed. Implementing a dynamic cross-animation completion wait
without an observable current path would have failed the plan's code guard; the runtime continued
to treat `CallAnimation` as instantaneous for scheduling.

That case became reachable 2026-08-02, and the wait is implemented as of 2026-08-05 (`BL-228`).

# What a wait is asked to wait FOR (2026-08-05)

`callee_shapes.py` is the second instrument. `census.py` settled the FIELD; this one settles what
"completes" means, from the data rather than from what makes one crash look right. It asks three
questions of every flagged call, in both front-ends:

- does the flagged callee **terminate at all** (a definition whose sequences carry an infinite
  `LOOP` never finishes, so a caller that waits on one waits forever);
- is the flagged call the **last event** of its block (then there is nothing behind it to hold);
- how long would the hold be, in authored seconds.

```
python analysis/wait-for-completion/callee_shapes.py --dump .scratch/wfc-shapes.json
```

Termination is decided over the **instance closure** — every non-`OnCall` sequence plus everything
they reach by `CALL_SEQUENCE`/`STOP_SEQUENCE` — because that is what `AnimInstance.Finished` is.

## The finding: one cross-tab, and it is one-sided

Compiled `cam_anim`/`mis_anim`, over the blocks the runtime executes (`reset_state` + `sequences`):
**2,852 flagged calls**, every callee resolved.

| | callee never terminates | callee terminates |
|---|---:|---:|
| flagged call is the LAST event of its block | 2,662 | 25 |
| flagged call has events behind it | **0** | **165** |

The reader front-end reproduces it independently — 147 flagged `CALL_ANIMATION` bodies, split
108 / 9 / **0** / 29 across the same four cells.

**Zero, in both front-ends.** Every flagged call naming a never-terminating callee is the last
event of its block, and there are only four such callees: `sputter_fire`, `sputter_black_smoke`,
`sputter_fire_smoke`, `gen_drop_ladder` — the `LOOP{-1}` sustain idiom.

So the rule follows from the data with no judgement call: **the wait gates the caller's NEXT
event, and nothing else.** Read instead as a lifetime hold on the runner or the instance, 2,770
authored calls would wedge their sequence open for the session on a callee that by construction
never ends, and the data would contradict itself. Read as a next-event gate it is consistent with
the whole install, zero exceptions.

That also bounds the blast radius: only **165 of 2,852** compiled flagged calls (5.8 %) can shift
any timing at all — 29 distinct (caller, callee) pairs, median authored hold 3.0 s, longest 36.01 s
(`start_gb3` → `cg1zepright_gasbag3`). 16 of the 165 name a callee that completes inside its own
t=0 burst, so they hold for nothing; 28 end in an SI script whose length lives in the `.zan` pool,
so their spans here are lower bounds.

## Two things counted and deliberately not implemented

- **879 flagged calls live in `unknown_seq`**, a third full sequence block (`seq_state: "Initial"`,
  its own pointer, not a duplicate of any listed sequence) present on 1,544 compiled defs — which
  `AnimDefinition.Parse` does not read at all. They are therefore out of this item's scope in the
  strict sense: our runtime never executes that block, for waits or anything else. That is a
  separate gap and is filed as `BL-258`, not fixed here. It is also why this census reports 2,852
  flagged calls where `census.py` reports 3,731: 3,731 − 2,852 = 879, exactly.
- **33 reader `CALL_SEQUENCE` bodies carry the bare token.** Not honoured: the compiled form
  carries the field on `CallAnimation` and on nothing else (56,750/56,750), so a same-instance
  sequence wait has no compiled counterpart to decode its semantics from. Counted in the report and
  in `AnimDefs`' own `CallSequence` case.

## Measured runtime scope

The mechanism is inert everywhere a headless probe currently reaches except the one case it was
opened on. Measured on the landed build: an 8-chapter `--freecam --det` regression arms **zero**
holds (correct by construction — no flagged owner is `OnStartup`) and leaves every bootstrap count
identical; `--effects-test` on C1 arms zero; `--destroy=m_build` on C1 and `--destroy=bont` on
C3/M02 arm zero. The 165 effective holds sit in zeppelin crane/hookup choreography (`pzhomebase`,
`pzep_crane`, `wv_initiate_hookup`), cockpit ejection (`cpeject1`/`cpeject2`), AI remote damage
(`random_remote_damage`) and balloon deaths — content no headless probe drives today. The runtime
names each of the four outcomes as it happens (armed / abandoned at the ceiling / routed to the
effects runtime / nothing live to hold), because "never dispatched" and "dispatched but inert" are
different facts and a probe that cannot tell them apart reports an untested mechanism as a working
one (LOG-16, DIAG-15).

Relevant verification rules: `DIAG-1`, `DIAG-8`, `DIAG-11`, `DIAG-15`, `METHOD-1`, `METHOD-9`,
`METHOD-16`, `LOG-5`, `LOG-16`, and `SHELL-7`.
