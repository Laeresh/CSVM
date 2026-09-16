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
| `FUN_005ac7a0` | the impact performer: reads the struck surface id, runs the hook, plays the row |
| `FUN_005abcf0` | the damage path called from it; a damageable struck object plays its own arm |
| `FUN_005acf60` | the hit handler: resolves what was struck, reads its surface id, dispatches |
| `FUN_005ad100` | plays the row's `SOUND` |
| `FUN_005ad160` | plays the row's `BOUNCE_SOUND` |
| `FUN_005ad330` | the hit resolve; also tests `material + 0x20 == 1` (water) as a special case |
| `FUN_004edc10` | spawns an animation definition at a point, with an optional rotation |
| `FUN_00525d90` | spawns the row's `EFFECT` at a point |

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

## What one hit plays, `FUN_005ac7a0` slot by slot

This is the performer, and it is the whole answer to "what does the original spawn when a round
hits a building". There is no building arm and no structure table. A building is whatever `soil`
its material carries, and the same three slots run for every surface.

| Address | What it does |
|---|---|
| `0x005ac7a9`, `0x005ac7bc` | `surfaceId = hit->material ? *(int *)(material + 0x20) : 0`. Nothing else feeds the index: not the struck node's name, not its model, not whether it is a structure. |
| `0x005ac7c7`–`0x005ac7dd` | calls the weapon's impact hook `*(weapon + 0x20c)` and keeps its return as the suppression mask (1 `Sound`, 2 `Effects`, 4 `Animation`). |
| `0x005ac80f` | `CALL FUN_005abcf0`, the damage path, whose return is kept and gates the `ANIMATION` slot below. |
| `0x005ac910`, `0x005ac91f` | mask bit 1 clear, then `FUN_005ad100(weapon, hit, surfaceId, 1.0f)` plays the row's `SOUND`. |
| `0x005ac927` | the blast arm's own flag non-zero skips both animation slots. |
| `0x005ac933` | mask bit 4 set skips the `ANIMATION` slot only. |
| `0x005ac93a` | `FUN_005abcf0` having returned non-zero skips it too: the struck object already played it (below). |
| `0x005ac942`–`0x005ac94e` | `*(int *)(*(weapon + 0x15c) + surfaceId*100 + 0x4)`, the row's `ANIMATION`. The address arithmetic is `LEA EAX,[EBX+EBX*4]` then `LEA ECX,[EAX+EAX*4]` then `[EDX + ECX*4 + 4]`, which is the 100-byte stride and the `+0x04` offset in the open. |
| `0x005ac956`–`0x005ac95e` | the caller's own override animation (`param_4`) wins over the row's when non-null. |
| `0x005ac97f` | `CALL FUN_004edc10(anim, 0, hit.x, hit.y, hit.z, 0,0,0,0,0,0)`: spawned at the hit point with **no rotation**. |
| `0x005ac987`–`0x005ac996` | `*(...surfaceId*100 + 0x1c)`, the row's `SURFACE_ANIMATION`, under no mask bit. |
| `0x005ac9b1`, `0x005ac9c0` | `FUN_0053fd40`/`FUN_00540260` build the rotation that lays world up onto the struck normal. |
| `0x005ac9f3` | `CALL FUN_004edc10` again, this time with that rotation. This is the only orientation difference between the two slots. |
| `0x005ac9fb`–`0x005aca07` | `*(...surfaceId*100 + 0x24)`, the row's `EFFECT`. |
| `0x005aca0f`, `0x005aca1b` | mask bit 2 clear, then `CALL FUN_00525d90(effect, hit + 0xc)`. |

Inside `FUN_005abcf0` the struck object gets first refusal on the same animation:

- `0x005abd27`, `EDI = *(struck + 0xbc)`, the object's hit-handler vtable; a null there returns 0
  and the performer plays the row itself.
- `0x005abf1a`, `EDX = *(weapon + 0x15c)`: the handler is passed the same
  `row[+0x4]` `ANIMATION`, gated on `weapon + 0x74 & 2` and the global `DAT_00a1d7dc`. The
  alternatives it can substitute are the `(float, handle)` pair table at `weapon + 0x164` (count) /
  `weapon + 0x168`, keyed by `_DAT_00a1e178` (initialised `1.0`), and `weapon + 0x18c` when
  `DAT_00a1e174` is non-zero.

So a damageable building plays the row's `ANIMATION` **once**, from whichever of the two sites ran,
never twice, and never anything the row does not name.

## A building is a soil byte, and Hollywood's buildings are not `buildings`

⚠ **`buildings`(11) is not "geometry that looks like a building".** It is the `soil` byte on the
struck material, and the shipped chapters spend it sparingly:

- **C2/C2B (Hollywood, the film lot)** carries **no collider with id 11 at all**. Scanning the
  built chapter finds ids 0 (`default`) 1380, 1 (`water`) 82, 13 (`dirt`) 81, across 1,544 bodies.
  Every `filmlot*`, `chrysler*` and `empire*` material carries `Default`. The film-lot walls, the
  studio blocks and the `nycity` skyscraper are all id 0, so a gun round on them takes the guns'
  `default` row and plays the authored `<caliber><ammo>_gunhit`.
- **C1** carries 61 bodies with id 11, all of them the `aphagar01/02/04/05` airport-hangar
  materials (4 materials). The hangars are the `buildings` surface in the shipped data.

`SceneBuilder.ClassifySurface`'s `buildings` string is a separate, cosmetic classification and does
not select the impact; `AttachCollision` writes the bucket's dominant `SoilId` into
`SurfaceIdMeta`, and that is what `ProjectilePool.SurfaceIdOf` reads.

## What this decides in the shipped data

`dirt`(13) is named by no weapon at all, and the HE/AP rockets name only
`default`/`water`/`buildings`, so `player`(6) is unnamed for them too, and a struck aircraft reads
as `player`(6). Both ids copy `default`, and `wep_06`'s `default` row is
`SURFACE_ANIMATION he_ground_effect` + `SOUND snd_missile_explode`. A rocket therefore throws its
full ground burst on a dirt tile and on another plane, from one arm. A 30 cal round on that same
plane draws nothing, because the guns name `player` and leave it empty.
