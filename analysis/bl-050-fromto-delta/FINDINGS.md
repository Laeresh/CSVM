# `OBJECT_MOTION_FROM_TO`'s `*_delta` channels are the sibling channel's RATE

Measured 2026-08-04 for `BL-050` / PLAN-m3-polish-6 B11. **Verdict: no motion
semantics were missing. The channels are redundant derived data and reviving them as a
composed offset would have run every affected motion at double speed.**

Scripts here are read-only and carry no game data; they read the local `extracted/` tree and
write to `.scratch/bl-050/`.

| Script | Does |
|---|---|
| `census.ps1` | scans all compiled `mis_anim`/`cam_anim` JSON, dumps every FROM_TO event with a non-null `*_delta` + its neighbouring events |
| `dump-detail.ps1` | the same events deduplicated to distinct authored signatures, with the absolute channels printed alongside |
| `verify-rate.ps1` | tests `*_delta == (to − from) / run_time` against every one |

## 1. Census — 51 events, not 26

Scanned all **16,114** compiled anim files in the install. **51** `ObjectMotionFromTo` events
carry a non-null `*_delta`, in 39 files; each carries exactly one delta channel.

| Channel | Events | in `cam_anim` | in `mis_anim` |
|---|---:|---:|---:|
| `translate_delta` | 15 | 15 | 0 |
| `rotate_delta` | 17 | 6 | 11 |
| `scale_delta` | 19 | 8 | 11 |
| **total** | **51** | **29** | **22** |

Per chapter: C1 20, C5 15, C2 7, C3 5, C1B/C1C/C2B/C4 1 each. Deduplicated to distinct
authored signatures (`anim`/`sequence`/`node`/values) there are **33** — the `chuteman`
parachutist's `deactivate_chuteman` is one definition shipped in all 8 chapters' `cam_anim`,
and C5's `man_cranes` `gerter_move` is one up/down pair repeated 6× down a row of cranes.

`BL-050`'s inherited figure of "15 translate, 6 rotate, 5 scale = 26 install-wide" reproduces
the translate and rotate counts of the `cam_anim` half exactly and undercounts scale; the
numbers above come from a full scan and supersede it.

The reader (zrdr) sources spell **no `*_DELTA` token at all** — 0 of 1,355 reader JSON files
contain the substring `_DELTA`. So the field is compiled-form-only, which is the first hint
that it is something the compiler *computes* rather than something an author *writes*.

## 2. The decode

**`*_delta` == `(channel.to − channel.from) / run_time` — the sibling absolute channel's
per-second rate.**

`verify-rate.ps1` over all 51:

```
delta channels checked : 51
without a sibling      : 0
mismatches (>1e-3 rel) : 0
worst absolute residual: 0.000001
worst relative residual: 0.000004
```

Zero mismatches, and the worst relative residual is 4e-6 — float32 rounding. **Every one of
the 51 ships the absolute channel it is the rate of**, so there is no case where the delta is
the only description of a motion.

Four of the 33 signatures, worked by hand, because the strength of this is that it holds on
non-axis-aligned and mixed-sign vectors, not only on the easy ones:

| Def / node | `run_time` | absolute channel `from → to` | `(to−from)/rt` | shipped delta |
|---|---:|---|---|---|
| `studebaker4` (C3/M02) | 0.35 | rotate (−0.7156, 0.8552, 0) → (−0.5236, −0.5236, 0) | (0.5486, −3.9394, 0) | (0.5485, −3.9395, 0) |
| `litemast_dest.flt`/`wire2` (C5/M01) | 2.2 | scale (1,1,1) → (0.7, 0.1, 8) | (−0.1364, −0.4091, 3.1818) | (−0.1364, −0.4091, 3.1818) |
| `barracuda`/`subdestroyed` (C3) | 40.0 | rotate (0, 0, 0.2618) → (1.1345, 0.1745, 0.0175) | (0.0284, 0.0044, −0.0061) | (0.0284, 0.0044, −0.0061) |
| `man_cranes`/`m_gerter` (C5) | 6.0 | translate (21.3, 19.9, −20.2) → (21.3, 7.9, −20.2) | (0, −2, 0) | (0, −2, 0) |

## 3. What that means for the code

The reading the backlog entry proposed as "most plausible" — a bare vector is the `to` with an
implied zero `from`, composed on top of the held pose — is **disproved**. Under it,
`man_cranes`' hook would tween 12 m down *and* accumulate another 12 m of offset over the same
6 s; `chuteman` would shrink to 0.1 and then be scaled by (0.7, 0.83, 0.7) on top of that.

So `FromToMotion` reads the three absolute channels and nothing else, and the dead delta
plumbing (six fields, three `Channel` calls, three composition lines in `Seek`) is **deleted**
rather than left as a landmine for the next reader who notices `Channel` returns `(null, null)`
on a bare vector. Deleting it is provably behaviour-neutral: every one of those six fields was
unconditionally null, including in `HasAnyChannel`.

The `FromToMotion` docstring's claim that "deltas compose on the HELD pose" — flagged in
`BL-050` as never observed — is **corrected**, not confirmed.

## 4. Reachability — 13 of the 51 run in an ordinary ambient build

`cam_anim` is the **chapter-level** compiled archive, loaded for every mission in that chapter
alongside its `mis_anim` (`AnimProgram.Load`); it is not a cutscene-only source. So these events
are not dormant. By activation:

| Activation | Events | Where |
|---|---:|---|
| `OnStartup` | 15 | C5 `man_cranes`/`m_gerter` ×12, C1 `police_car`/`start_walkin` ×1, C3/M02 `studebaker4` ×2 |
| `OnCall` | 34 | C1/M04 `lkztailgasbag`, C1/M05's nine `lifesaver*`, C2/M02's six police/security cars, C5/M01 `litemast_dest`, C5/M04 `dantezep`, the 8 `chuteman`, the firetruck hoses, `depot_burnnode*` |
| `WeaponHit` | 2 | C3's `barracuda` |

**13 of the 15 `OnStartup` ones run in a plain `--freecam` build**, so the 8-chapter regression
and the C1/C5 goldens exercise them — which is what makes the deletion's behaviour-neutrality
*measured* rather than only argued. Verified live with `--debug-anim`:

- C5: six `m_gerter` crane hooks, all `visible`, Y advancing between samples (297.5 → 299.6,
  345.7 → 347.8, 305.9 → 308.0) — the `gerter_move` tween running.
- C1: `police_car` at (-6868.2, 128.0, -5930.3) rot 45°, position changing across samples —
  the `start_walkin` sequence, the delta-bearing event's own sequence.

The two `studebaker4` events are `OnStartup` only inside C3's **M02** scope, and the ambient C3
build loads IA1, so those two are not reached; the 36 `OnCall`/`WeaponHit` ones need their
trigger.

None of this changes the verdict — and note what was *never* broken: the absolute channels
always drove all 51 of these motions correctly. The dead field was the redundant rate beside
them, so there was never a missing animation to capture, only a field to explain.
