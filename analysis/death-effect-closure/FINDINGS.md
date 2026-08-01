# Death-effect calls are closed; 22 other death calls are live choreography

Run from the repository root:

```
python analysis/death-effect-closure/census.py --dump .scratch/death-effect-closure.json
```

## Finding (2026-08-02)

The exhaustive compiled census read 14,963 definitions across 53 chapter/mission programs in
all eight chapters. The structurally distinct destruction slot on `health > 0` definitions
contains **2,360 `CallAnimation` events to 30 names**.

The world-effects runtime handles **2,196 calls to eight names** once
`AnimProgram.Subset`'s transitive closure is counted:

| handled target | calls |
|---|---:|
| `large_30sec_fire` | 1,035 |
| `great_balls_of_fire` | 432 |
| `large_fireball` | 307 |
| `large_black_smokeball` | 288 |
| `biggun_flying_parts` | 84 |
| `dblcannon_flying_parts` | 46 |
| `big_splash` | 3 |
| `big_ripple` | 1 |

`dblcannon_flying_parts` and `big_ripple` are not literal `EffectAnimNames` roots; they
are already members of its measured transitive call closure. Diffing only against the 33
root strings would therefore manufacture false gaps.

The remaining **164 calls to 22 names** are deliberately not routed. Their extracted
definitions manipulate live zeppelin/aircraft/vehicle nodes, run destruction motion, or are
wrappers whose child call already enters the handled effect set. Relocating those definitions
onto the effect stage would detach the choreography from the object being destroyed. The
fail-closed `LOCAL_CHOREOGRAPHY` set names every one; the report also records each resolved
`NAME`, `ANIMATION_ROOT_NAME`, health and event-kind set. A new or missing classification
makes the instrument exit nonzero.

No effect root or anchor root needed adding. `EffectAnimNames` remains 33 roots and the
effects-test baseline therefore remains 30 puffer-building names out of 33. The separately
censused progressive `DAMAGE_SEQUENCE` pair and C4 live-train exclusion are unchanged.

Relevant verification rules: `WORLD-12` and `WORLD-24`.

No game data is stored here; the script reads the player's own `extracted/` tree.
