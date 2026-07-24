# World destructibles

Part of the [format documentation](README.md). How an `ANIMATION_DEFINITION` doubles as a
destructible world object — a hit-point pool, a damage-threshold script, and a death sequence
layered onto the ordinary animation container.

A destructible object is not a separate file type and has no format of its own. It is an
`ANIMATION_DEFINITION` — the container, sequences, events, node binding and both source forms
(zrdr readers vs the compiled `cam_anim`/`mis_anim` archives) are all documented in
[anim-definitions.md](anim-definitions.md), and everything there applies unchanged. This page
covers only the extra layer that makes one destructible: the `HEALTH` pool, the `ACTIVATION`
mode, the `DAMAGE_SEQUENCE` threshold script, the death sequence, and the collide set.

## The three extra pieces

A destructible is any animation definition whose header carries these:

| Field | Reader key | Compiled key | Meaning |
|---|---|---|---|
| Hit points | `HEALTH [n]` | `health` (float) | The object's HP pool. **Any def with `health > 0` is destructible** — this is the whole test. |
| Damage mode | `ACTIVATION` (usually absent) | `activation` | What can damage it: `WeaponHit` (weapon fire only) or `WeaponOrCollideHit` (weapon **or** a plane ramming it). |
| Damage script | `DAMAGE_SEQUENCE [ … ]` | a sequence named `DAMAGE_SEQUENCE` | A threshold script of `IF ANIM_HEALTH n` branches escalating visible damage as HP falls. |

Everything else is ordinary sequences and events. When the object dies, a normal named
sequence (the **death sequence**) runs the healthy→destroyed swap, debris, fireballs and smoke.

Across the compiled archives of this install, **2,603 defs carry `health > 0`**; of those,
**2,565 are `WeaponHit`** and **44 are `WeaponOrCollideHit`**. The `ANIM_HEALTH` conditions live
exclusively inside `DAMAGE_SEQUENCE` (4,131 of them, none anywhere else).

## `ACTIVATION` — weapon vs collide

The reader source usually omits `ACTIVATION` on a destructible entirely (the water-tower def
below has no `ACTIVATION` key); the compiler assigns the default `WeaponHit`. The compiled form
always states it explicitly. The two values:

- **`WeaponHit`** — damaged only by weapon fire. Ramming the object with a plane damages the
  *plane*, not the object. This is the overwhelming majority (2,565 defs): buildings, tanks,
  towers, refinery structures, AA guns.
- **`WeaponOrCollideHit`** — also destroyed by a plane flying into it. Exactly **44 defs** in the
  whole install, and they are a deliberate, hand-picked set (below).

`proximity_damage` is a **separate** header flag and is `false` on every case examined here,
including both collide members and plain `WeaponHit` destructibles — it does *not* encode the
collide mode. The collide behaviour is carried entirely by the `activation` enum.

### The 44 collide-destructibles

| Chapter | Objects | Health | What |
|---|---|---|---|
| C2 | `fcpan01`–`fcpan39` | 0.01 | Hollywood facade panels — fly-through set dressing (39 defs) |
| C5 | `w_win01`–`w_win04` | 0.01 | Warehouse windows (anim names `smash_me1`–`smash_me4`) |
| C5 | `agyrobus` | 70.0 | The autogyro bus — the only substantial collide-destructible |

39 + 4 + 1 = 44. All but the autogyro have `health 0.01`, i.e. they shatter on the lightest
touch: they exist to break when you fly through them, not to be a combat target. Everything
outside this set is weapon-only.

## `ANIM_HEALTH` is an absolute threshold, and the order matters

A `DAMAGE_SEQUENCE` is a single sequence whose body is an `IF`/`ELSEIF`/`ELSE`/`ENDIF` chain
(the condition and control-flow events themselves are documented in
[anim-definitions.md](anim-definitions.md)). Its condition is `ANIM_HEALTH n` (compiled:
`AnimHealth`), and:

> **`ANIM_HEALTH n` means `health <= n`** — "worn down to n or below", **not** "equal to n" and
> **not** a fraction. The value is an absolute HP number in the same units as the def's `HEALTH`.

Because the test is `health <= n`, the script **must list the lowest threshold first**: an
`IF health <= 18` has to precede `ELSEIF health <= 36`, or the `<= 36` branch would swallow every
value that also satisfies `<= 18` and the more-damaged effect would never be reached. So the
authored order runs *most-damaged branch first*, which reads backwards from the object's
timeline but is exactly right for a falling-through `<=` cascade.

**The fractions are a reading aid, not the encoding.** Damage stages are conventionally placed
at round fractions of a def's own `HEALTH` — the recurring thresholds are 0.85, 0.75, 0.60, 0.50,
0.30 and 0.25 — but each is written out as the absolute product (`0.60 × 60 = 36`). Two
progressions dominate:

- **Two-stage `{0.60, 0.30}`** — smoke then fire. The standard escalation calls
  `sputter_black_smoke_obj` at the 0.60 stage and `sputter_fire_smoke_obj` at the 0.30 stage
  (48 defs use each).
- **Three-stage `{0.85, 0.50, 0.25}`**.

## Death sequence vs damage sequence

The two are different sequences with different jobs:

- **`DAMAGE_SEQUENCE`** is *progressive* damage — the smoke/fire escalation while the object is
  still standing. It is re-evaluated as HP drops.
- The **death sequence** is what runs once when HP reaches zero. It is an ordinary named
  sequence (the water tower's is `destroy_h2twr`), and its first job is the healthy→destroyed
  node swap (`OBJECT_ACTIVE_STATE healthy INACTIVE` + `destroyed ACTIVE`), followed by debris
  and effect calls.

Across the death sequences of the install, the call mix (2,360 `CallAnimation`s) is dominated by
`ObjectActiveState` (2,028 — the swaps and hide-the-wreckage-pieces), `InvalidateAnimation`
(1,286) and `StopAnimation` (1,260), with `Sound` (307), `PufferState` (148), `Loop` (86) and
`ObjectMotion` (16) behind them. The most-called death effects are `large_30sec_fire` (1,035),
`great_balls_of_fire` (432), `large_fireball` (307), `large_black_smokeball` (288) and
`biggun_flying_parts` (84).

**`unknown_seq` is not the death sequence.** The compiled def carries one structurally-distinct
trailing sequence slot the mech3ax fork surfaces as `unknown_seq` (see
[anim-definitions.md](anim-definitions.md) for the container detail); it is non-null on 1,544
defs, all of them `health > 0`. It holds destruction-time content, but *which* sequence lands
there varies — for the water tower it is the second puffer sequence (`h2twr_puffer2`), while the
actual death swap `destroy_h2twr` sits in the regular `sequences` array. Do not treat
`unknown_seq` as a reliable pointer to the death swap.

## Binding a def to its object(s)

A destructible def anchors to scene nodes exactly like any animation definition (full rules in
[anim-definitions.md](anim-definitions.md)), and two of those rules do the load-bearing work:

- **`NAME` is a wildcard, so one authored def serves many instances.** The water tower's
  `NAME` is `ap_h2otwr*`; in C1 that one reader definition compiles into four independent
  instances (`ap_h2otwr1`, `ap_h2otwr2`, `ap_h2otwr4`, `ap_h2otwr5`), each with its own HP.
- **`ANIMATION_ROOT_NAME` names the node the anim attaches to inside each instance**, and the
  destructible convention is a paired `healthy`/`destroyed` node group. The tower roots on
  `h2twr_healthy` and its death sequence toggles `h2twr_healthy`↔`h2twr_destroyed`. This
  `_healthy`/`_destroyed` (also bare `healthy`/`destroyed`, and object-specific spellings) pairing
  is pervasive: C1's gamez alone carries hundreds of such nodes.

## Worked example — the water tower (`ap_h2otwr`)

`extracted/C1/zrdr/ap_h2otwr.zrd.json` is one reader definition, `NAME ap_h2otwr*`,
`HEALTH 60`, no `ACTIVATION` key (the compiler assigns `WeaponHit`). Its structure, with the
real values:

**`RESET_STATE`** (base state at load) — `h2twr_healthy` ACTIVE, `h2twr_destroyed` INACTIVE.

**`DAMAGE_SEQUENCE`** — the progressive-damage script:

```
IF     ANIM_HEALTH 18 -> CALL_ANIMATION sputter_fire_smoke_obj  WITH_NODE h2twr_healthy
ELSEIF ANIM_HEALTH 36 -> CALL_ANIMATION sputter_black_smoke_obj WITH_NODE h2twr_healthy
ELSE ENDIF
```

With `HEALTH 60`, the thresholds are 36 (= 0.60) and 18 (= 0.30) — the `{0.60, 0.30}` smoke-then-
fire progression: black smoke once worn to `≤ 36`, fire smoke once worn to `≤ 18`. Note the
lowest threshold is tested first, as the `<=` cascade requires. The compiled form delivers this
same block verbatim as a sequence literally named `DAMAGE_SEQUENCE`, with `AnimHealth 18.0` then
`36.0`.

**Death sequence** `destroy_h2twr` — `h2twr_healthy` INACTIVE, `h2twr_destroyed` ACTIVE (the swap).

**Ballistic debris** — two unnamed sequences fling the mid and upper tower sections as rigid
bodies via `OBJECT_MOTION` (the ballistic form; channels documented in
[anim-definitions.md](anim-definitions.md)). For `h2twr_middle`: `START_TIME` +2.2 s,
`GRAVITY LOCAL -2`, `TRANSLATION_RANGE_MIN [10, 30, 2.5, 0]` / `TRANSLATION_RANGE_MAX
[10, 70, 4.5, 0]`, `FORWARD_ROTATION [TIME 60, 0]`, `SCALE [-0.1, -0.1, -0.1]`, `RUN_TIME 5`, then
`OBJECT_ACTIVE_STATE h2twr_middle INACTIVE`. `h2twr_upper` is the same with `START_TIME` +2.0 s
and `FORWARD_ROTATION [TIME 70, 0]`. (The negative `SCALE` is the section shrinking to nothing as
it tumbles clear.)

**Puffers** — two `ON_CALL` sequences (`h2twr_puffer`, `h2twr_puffer2`), each a `PUFFER_STATE`
loop of 13 iterations on the `splashbase` texture (`PUFFER_STATE` schema in
[effects.md](effects.md)), invoked from the death sequences via `CALL_SEQUENCE`.

## Engine status — live HP exists now; damage does not yet flow

The destructible model is complete in the data, and the engine now holds live per-instance HP for
it, but nothing damages that HP yet. A format reader should know why the branches still look dead
in the live world:

- **The reader front-end drops `DAMAGE_SEQUENCE` silently.** `AnimDefs.cs`'s `ParseDef` switch
  handles `NAME`/`ANIMATION_NAME`/`ANIMATION_ROOT_NAME`/`ACTIVATION`/`HEALTH`/`RESET_STATE`/
  `SEQUENCE_DEFINITION` but has **no `DAMAGE_SEQUENCE` case**, so a reader-sourced damage script
  is discarded. The compiled front-end delivers it fine (as the ordinary `DAMAGE_SEQUENCE`-named
  sequence above), so a compiled destructible keeps its script.
- **`HEALTH` is now a mutable per-instance value — but nothing decrements it yet.** At world build
  a `DestructibleRegistry` records one live HP pool per destructible node group, keyed by the
  `(def, anchor)` pair and seeded from the def's authored `HEALTH`; `AnimRuntime`'s
  `EvaluateCondition` reads that live value (`AnimHealth => registry HP <= num`) instead of the
  static `def.Health`. No weapon fire touches it yet, so every instance still sits at full health
  and every `ANIM_HEALTH` branch is **uniformly false** — an undamaged object never smokes, which
  is correct for a world nothing has shot at. Because one def's `NAME` is a wildcard, a single def
  can bind several node groups; each is an independent pool, so one tower's damage will not break
  its siblings. (It is also moot for `WeaponHit` defs at world build: they never bootstrap — only
  `ON_STARTUP` defs do.)

Engine internals belong in `docs/architecture.md` (see the `AnimRuntime`, `AnimDefs` and
`DestructibleRegistry` entries); this page states the fact, not the wiring.

### ⚠ Trap: never key the healthy↔destroyed convention on a substring

The `_healthy`/`_destroyed` naming is tempting to sweep with a substring or suffix match — and
that is a bug. A pass that hid every still-visible node whose name *contains* `"destroyed"` (in
`AnimRuntime`'s `HideUncoveredDestroyed`) once wiped C1's `ref_tank_dest`, which is the parent
GROUP of the *healthy* harbour refuel tanks — "destructible", not "destroyed". A `_dest` suffix
rule fails the same way. The healthy/destroyed pairing is a convention on node *roles*, not a
guarantee about substrings; resolve it through the definition's own `ANIMATION_ROOT_NAME` and its
sequences' explicit `OBJECT_ACTIVE_STATE` targets, never by scanning names.
