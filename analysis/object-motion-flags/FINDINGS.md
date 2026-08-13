# `OBJECT_MOTION`'s flag word, named from the parser — and the census re-derived

`census.py` walks every compiled `ObjectMotion` event in all 8 chapters' `cam_anim` and re-derives
the `gravity` flag cross-tab. Run it from the repo root (the one holding `extracted/`):

```
python analysis/object-motion-flags/census.py
python analysis/object-motion-flags/census.py gunshell     # drill into one def
```

## The flag word at `motion+0xc`, every bit named (2026-08-10)

**Decoded from the executable.** Every row below is the `OR` the parser `FUN_00508590`
(`crimson.exe`) executes when it recognises the token, at the address given. Nothing here is
inferred from how the update uses the bit.

| Bit | Token that sets it | Parser address |
|---|---|---|
| `0x1` | `GRAVITY` (the block being present at all) | `0050868b` |
| `0x2` | `IMPACT_FORCE` | `00508d03` |
| `0x4` | `TRANSLATION` (the vector form) | `00508d27` |
| `0x8` | `TRANSLATION_RANGE_MIN` | `005095de` |
| `0x10` | `TRANSLATION_RANGE_MAX` | `00509df0` |
| `0x20` | `XYZ_ROTATION` | `0050a60f` |
| `0x40` | `FORWARD_ROTATION DISTANCE` | `0050b309` |
| `0x80` | `FORWARD_ROTATION TIME` | `0050b34b` |
| `0x100` | `SCALE` | `0050b7ac` |
| `0x200` | `MORPH` | `0050c21a` |
| `0x400` | `RUN_TIME` | `0050ca4e` |
| `0x800` | `BOUNCE_SEQUENCE` (and its `_WATER`/`_LAVA` spellings) | `0050c678` |
| `0x1000` | `BOUNCE_SOUND` | `0050c732` |
| **`0x2000`** | **`GRAVITY COMPLEX`** | **`0050899c`** |
| `0x4000` | `GRAVITY NO_ALTITUDE` | `00508bf8` |
| `0x8000` | `GRAVITY DO_INTERSECTIONS` | `00508c41` |

Three corrections to the reading this project carried before:

- **`0x2000` is `COMPLEX`.** It had been the leading candidate on population grounds; it is now the
  parser's own answer. The bit is set by the `COMPLEX` token inside the `GRAVITY` block and by
  nothing else.
- **`0x200` is `MORPH`**, a named token with its own parse, not an opacity/fade channel inferred
  from the update writing `node+0x3c` into `+0x24`.
- **`TRANSLATION_RANGE` is two bits, not one** — `MIN` (`0x8`) and `MAX` (`0x10`) are separate
  tokens with separate parses. The mech3ax reader notes the engine does not require `MAX` to use
  the ranges.

Independently corroborated: the fork's own `ObjectMotionFlags`
(`tools/mech3ax/crates/anim-events/src/events/e10_object_motion/mod.rs`) carries the same 16 bits
with the same token names, derived from the reader files rather than from the executable.

## The `GRAVITY` block parses five tokens, and only three of them survive compilation

**Decoded from the executable**, `FUN_00508590` lines around `00508680`–`00508c60`:

```
GRAVITY                    -> flags |= 0x1 ; value = the global default (DAT_009fd164)
  DEFAULT                  -> value = the global default
  LOCAL <number>           -> value = <number>                       (sets NO bit)
  COMPLEX [<number>]       -> flags |= 0x2000 ; value = <number> if one follows, else the default
  NO_ALTITUDE              -> flags |= 0x4000
  DO_INTERSECTIONS         -> flags |= 0x8000
  anything else            -> "OBJECT_MOTION: GRAVITY syntax error" and the token is skipped
```

**`GRAVITY LOCAL` cannot be surfaced by the extractor, and this is not a gap in the extractor.**
`DEFAULT` and `LOCAL` are value selectors, not modes: both end with a single float in the motion's
gravity slot, and neither sets a bit. The compiled record keeps one `f32` plus the flag word
(`ObjectMotionNgC.gravity`), so at compile time the two tokens become indistinguishable. The
extractor's three booleans are therefore exactly the three that exist in the data — the earlier
framing ("three booleans ship where the original parses four") counted a token that leaves no
trace. `COMPLEX` is the one token that both sets a bit *and* may carry the value.

The closest the extract can get is the value distribution itself (report Q5): `-15` and `-20`
appear **only** under `complex`, and nothing outside `complex` is authored stronger than `-10`.

## The census, re-derived (2026-08-10, corrected 2026-08-12 at C7)

Over **3,066** `ObjectMotion` events in all 8 chapters (1,068 distinct file/anim/node): **1,640**
carry a `gravity` block, **1,983** are ballistic (a `translation` or `translation_range` block).
Every gravity-carrying motion is ballistic, so the cross-tab is the same either way.

| `complex` | `no_altitude` | `do_intersections` | events | distinct shapes |
|---|---|---|---|---|
| false | false | false | **1,378** | 867 |
| **true** | false | **true** | **166** | 25 |
| true | false | false | **88** | 11 |
| false | **true** | false | **8** | 1 |

**⚠ The 2026-08-10 version of this table read 1,363 in the all-false row, and that was this
census's own scan error, not a correction of the published 1,378.** `scan()` walked `sequences`
only. mech3ax's **`unknown_seq` is the compiled destruction slot** (`AnimDefinition.DeathSlot`),
which a real kill dispatches — `BL-276` landed exactly that path, after ~1,035 of
`large_30sec_fire`'s death calls had sat there unread for the project's whole life. It holds
**15 more `ObjectMotion` events**, every one ballistic with an all-false gravity block: three
`t_truck0N` `part3` in C3, seven C4 defs (`col_tower`, `mineshack`, `shaft`, `sluice`, two
`switchhouse`, `t_truck01/02`, `train01`), and two C5 `loading_craneN` `part5`. So the runtime
population is **1,378**, the number that was published before, and the 2026-08-10 arithmetic
(1,459 − 88 − 8 = 1,363) inherited the same narrow scan through its sibling table. `census.py`
now walks both blocks.

`do_intersections: true` reproduces **166** exactly under the wider scan too — the figure that has
now been checked four times and should not move again. **The default-combination bodies that
should be landing are 1,378 + 88 = 1,466.**

**The `no_altitude` row was never wrong.** A grep of `extracted/` for `"no_altitude": true` returns
**8** files, one per chapter, all `gunshell-gunshell.json` — the "5 chapter files vs 8 events"
discrepancy the plan set out to resolve does not reproduce. `gunshell` is the only def in the
install that authors it, it is the only `NO_ALTITUDE` carrier, and it authors `RUN_TIME 2.0`, so
the parser's `OBJECT_MOTION: NO_ALTITUDE fall lacks RUN_TIME` diagnostic never fires on this
install.

## What authors `COMPLEX` — 254 events, and they are all aircraft wreckage

The 166 + 88 that carry `complex` are **25 distinct shapes**, and the list is narrow enough to name
in full: the eleven airframes' `MAIN_ROOT_NODE` fall, `player`'s and `player_crash_default`'s and
`player_crash_dirt`'s four pieces each, `agyrobus`, `autogyro_loserotor`'s `staticrotor1`, and
`drop_smokescreen_canister`'s `smoker`. Nothing authored on a *world* destructible carries it —
not one of the building, tower, bridge or zeppelin defs.

`complex` **without** `do_intersections` is the 88: `player` pieces 2/3/4 (gravity **−15, −15,
−20**), both crash defs' first pieces, the lost rotor and the smoke canister.

⚠ **That was the shape of the argument, and it was answered: `COMPLEX` does not remove gravity, it
transforms it.** Three of these bodies ask for gravity *stronger* than Earth's while the update
declines the constant add for exactly them — so "no constant gravity add" could not mean "no
gravity", and the other path is forty lines below in the same function: the world-down vector
`(0, value, 0)` run through the node's matrix into the body's frame, which is identical to the plain
form under a world-aligned parent and is why only wreckage authors it. Full write-up in
[`docs/org/objectMotion.md`](../../docs/org/objectMotion.md).

## `impact_force` (bit `0x2`) — censused, filed, not built

Out of scope for this plan, censused here only so the next reader does not re-derive it: **182
events / 25 distinct shapes**, and the list is the same aircraft-wreckage family as `complex` (the
eleven airframes, `player`, both `player_crash_*`, `agyrobus`, the smoke canister). The update
gates the parent-velocity add on this bit at `004e925e`. `BL-008` was closed on the finding that
the original does not inherit velocity into world debris — which the census confirms for *world*
debris and leaves open for aircraft wreckage, where every carrier of this flag lives. Filed as
`BL-343`.
