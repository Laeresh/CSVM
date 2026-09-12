# The weapon `IMPACT` table, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function it came from, so any of
them can be re-checked at source. No decompiler output is reproduced.

**Where the other halves live.** The authored side, what the `IMPACT` block looks like in
`weapons.zrd.json`, which surface names the shipped weapons use, and what each effect name resolves
to, is [`formats/weapons.md`](../formats/weapons.md) and
[`formats/weapon-effects.md`](../formats/weapon-effects.md). The surface-id registry those names
index is [`../../analysis/surface-classification/FINDINGS.md`](../../analysis/surface-classification/FINDINGS.md).
Our implementation is `WeaponDefs.ParseImpact` / `ImpactOutcome.Resolve`.

## Function map

| Address | Role |
|---|---|
| `FUN_005ad630` | the `.zrd` token dispatcher; its `IMPACT` branch builds the table |
| `FUN_005ae990` | parses one `IMPACT` block into one row |
| `FUN_005acf60` | the hit handler: resolves what was struck, reads its surface id, dispatches |
| `FUN_005ad100` | plays the row's `SOUND` |
| `FUN_005ad160` | plays the row's `BOUNCE_SOUND` |
| `FUN_005ad330` | the hit resolve; also tests `material + 0x20 == 1` (water) as a special case |

## The table is an array indexed by surface id, stride 100 bytes

A weapon's table base is `weapon + 0x15c`, and row `i` is `base + i*100`. It is built by walking the
global surface-name registry, not by walking the authored block:

- `0x005ae1ea`, row 0 is parsed from the block named `registry[0]` (`default`).
- `0x005ae216`–`0x005ae29b`, the loop: `EDI` walks `&registry[1]`, `ESI` steps the row stride
  `0x64`, so iteration `i` is registry id `i`.
- `0x005ae22d`, look up a block named `registry[i]`.

A block whose name is not a registry name is never looked up and so is parsed into nothing. The
order of names inside the authored block means nothing; only their spelling does.

## An id the weapon names no block for inherits the `default` row whole

The loop's two arms are the mechanism:

- **Found** (`0x005ae239`–`0x005ae266`), parse the block into row `i`. Then one field-level
  inherit: if the parsed row's `+0x20` (`ANIMATION_MIN_RANGE`) is zero, take row 0's.
- **Not found** (`0x005ae268`), `ESI` = row 0, `EDI` = row `i`, `ECX = 0x19`, `REP MOVSD`. That is
  25 dwords: **the entire 100-byte row, copied from `default` onto row `i`**, effect names and
  sound list together.

So the table is never sparse. Every id the weapon says nothing about answers `default`'s bindings.

⚠ **Naming an id and binding nothing on it is the opposite case and stays empty.** The block is
found, so it is parsed rather than copied over, and it yields a row with no bindings, which plays
nothing. Only the miss arm copies. In the shipped data this separates `dirt`(13), which no weapon
names at all, from the guns' own `player`(6) blocks, whose `ANIMATION`/`EFFECT`/`SOUND` slots are
authored empty, and from `enemy`(7), whose value is null on 28 entries. A reader that collapses
"named and empty" into "absent" gets one of the two cases wrong whichever way it collapses them,
which is why `WeaponDefs.ParseImpact` tracks presence apart from what parsed.

## Row layout

Written by `FUN_005ae990`, which zeroes only `+0x5c` on entry, a row is otherwise whatever the
allocation or the copy left in it.

| Offset | Field |
|---|---|
| `+0x04`, `+0x08` | `ANIMATION` (up to two variants) |
| `+0x14` | `ANIMATION_ATTACHED` |
| `+0x18` | `MODEL_ANIMATION` |
| `+0x1c` | `SURFACE_ANIMATION` |
| `+0x20` | `ANIMATION_MIN_RANGE` |
| `+0x24` | `EFFECT` |
| `+0x28` | `MODEL` |
| `+0x2c`, `+0x30[]` | `SOUND` count, then the handles |
| `+0x40`, `+0x44[]` | `BOUNCE_SOUND` count, then the handles |

## The runtime index is the struck material's surface id

`FUN_005acf60` resolves the hit, then takes the id off the struck material and dispatches:

```
surfaceId = hit->material ? *(int *)(material + 0x20) : 0
FUN_005ad100(weapon, hit, surfaceId, 1.0f)      // row = weapon[0x15c] + surfaceId * 100
```

`material + 0x20` is the same dword the crash (`FUN_0048b920`) and touchdown (`FUN_0048d2c0`)
cascades index their own vectors with, the `soil` field mech3ax extracts. A null material reads as
id 0.

⚠ **`FUN_005ad100` is the SOUND path, not the effect path.** It gates on `row[0x2c] != 0`, the
`SOUND` count, and picks one of `+0x30[]` by `rand()`. The effect half of the row is played from
`FUN_005acf60`'s other call. The gate is a real "this row is silent" arm, but it cannot fire for an
id the weapon never named, because the copy already filled that row; it fires for a named-and-empty
row, and for a weapon with no `IMPACT` block at all (`wep_26`).

⚠ **The index is unchecked.** `FUN_005ad100` computes `base + id*100` with no bounds test, so
nothing but the registry's own range keeps it in the table. Every id reaching it comes from a
material's `soil` field or, in our build, from `ProjectilePool.SurfaceIdOf`.

## What this decides in the shipped data

`dirt`(13) is named by no weapon at all, and the HE/AP rockets name only
`default`/`water`/`buildings`, so `player`(6) is unnamed for them too, and a struck aircraft reads
as `player`(6). Both ids copy `default`, and `wep_06`'s `default` row is
`SURFACE_ANIMATION he_ground_effect` + `SOUND snd_missile_explode`. A rocket therefore throws its
full ground burst on a dirt tile and on another plane, from one arm. A 30 cal round on that same
plane draws nothing, because the guns name `player` and leave it empty.
