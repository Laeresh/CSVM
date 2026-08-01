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

## Runtime decision

No scheduler code lands with this decode. All flagged compiled owners are `OnCall` (2,844) or
`WeaponHit` (887), not `OnStartup`, and the clearest supported-data timing case is the water-crash
chain (`plane_big_splash` must finish before `large_steam_spray`), which is not reachable until
surface-aware crash selection lands. Implementing a dynamic cross-animation completion wait
without an observable current path would fail the plan's code guard; the runtime continues to
treat `CallAnimation` as instantaneous for scheduling.

Relevant verification rules: `DIAG-1`, `DIAG-8`, `METHOD-1`, `LOG-5`, `DIAG-15`, and `SHELL-7`.
