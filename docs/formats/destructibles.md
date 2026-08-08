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
| Damage mode | `ACTIVATION` (usually absent) | `activation` | The plane's fate on contact: `WeaponHit` (solid — graze/crash) or `WeaponOrCollideHit` (breaks, plane flies through). Both take collision damage (`BL-302`). |
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

- **`WeaponHit`** — the object is **solid**: a plane flying into it grazes or crashes. This is
  the overwhelming majority (2,565 defs): buildings, tanks, towers, refinery structures, AA guns.
- **`WeaponOrCollideHit`** — the object **breaks and the plane flies through unharmed**. Exactly
  **44 defs** in the whole install, and they are a deliberate, hand-picked set (below).

> ⚠ **Superseded reading (corrected 2026-08-07, `BL-302`).** This page originally read the enum as
> "what can damage it" — `WeaponHit` = weapon fire only, ramming damages the plane and leaves the
> object intact. Original-game tests refute that: ramming a C1 hangar (a plain `WeaponHit` def)
> plays both the plane crash and the hangar's destruction, and a survivable graze advances its
> `DAMAGE_SEQUENCE` stages. **Every destructible takes severity-scaled collision damage; the enum
> gates the *plane's* fate (solid vs fly-through), not the object's.** The crash explosion has no
> blast radius (crashing beside a destructible damages nothing), so the damage is contact-borne.

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
outside this set is solid on contact — but still takes collision damage (see the ⚠ above).

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

The structurally distinct `unknown_seq` destruction slot carries **2,360 `CallAnimation`
events to 30 names**. The live world-effects runtime handles 2,196 of them through eight names
once its transitive call closure is counted: `large_30sec_fire` (1,035),
`great_balls_of_fire` (432), `large_fireball` (307), `large_black_smokeball` (288),
`biggun_flying_parts` (84), `dblcannon_flying_parts` (46), `big_splash` (3), and
`big_ripple` (1). The other 164 calls target 22 live-object choreography definitions —
zeppelin/aircraft/vehicle motion, node swaps, or wrappers into that handled set — and must
remain on the world runtime rather than be relocated as effect templates. The exhaustive
classification and per-target shapes are in `analysis/death-effect-closure/`.

**`unknown_seq` is not the death sequence.** The compiled def carries one structurally-distinct
trailing sequence slot the mech3ax fork surfaces as `unknown_seq` (see
[anim-definitions.md](anim-definitions.md) for the container detail); it is non-null on 1,544
defs, all of them `health > 0`. It holds destruction-time content, but *which* sequence lands
there varies — for the water tower it is the second puffer sequence (`h2twr_puffer2`), while the
actual death swap `destroy_h2twr` sits in the regular `sequences` array. Do not treat
`unknown_seq` as a reliable pointer to the death swap.

**The slot is loaded and dispatched at death (`BL-276`, 2026-08-05).** `CompiledAnim` parses it
into `AnimDefinition.DeathSlot` — deliberately OFF `Sequences`, so bootstrap and the
sequence-walking derivations never see it — and `AnimRuntime.RunDeathSequence` runs it as an
extra runner on the death's own instance. The 2026-08-05 census over all 12,693 compiled defs:
every non-empty slot sits on a `health > 0` destructible (none elsewhere), and 1,429 of the
1,430 mission-archive slots dispatch calls no listed sequence reaches — this block is where
~all of `large_30sec_fire`'s 1,035 death calls live, so before it dispatched, the game's
most-called death fire never played from a compiled death site at all.

## Binding a def to its object(s)

A destructible def anchors to scene nodes exactly like any animation definition (full rules in
[anim-definitions.md](anim-definitions.md)), and two of those rules do the load-bearing work:

- **`NAME` is a wildcard, so one authored def serves many instances.** The water tower's
  `NAME` is `ap_h2otwr*`; in C1 that one reader definition compiles into four independent
  instances (`ap_h2otwr1`, `ap_h2otwr2`, `ap_h2otwr4`, `ap_h2otwr5`), each with its own HP.
  - ⚠ **The compiler does NOT always give the expanded instances distinct names, so `NAME` alone
    cannot identify one.** C1's two airfield hangars are both `NAME air_gen` with
    `ANIMATION_ROOT_NAME healthy`; the only thing separating them is the symbol table, where
    `air_gen` names `eairg32`'s nodes and `air_gen#1` names `eairg31`'s. The same holds at scale
    for zeppelins: every mission's turret/engine defs are `ctur1`…`leng42` on every airship, and
    a chapter's gamez carries up to nine of them. **Bind a compiled def through its symbol table,
    never its `NAME`** — matching by name gives each twin all the anchors and they cross-bind.
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

## Engine status — Wave C complete: objects damage, die, throw debris, break on contact, and reset

**Wave C (destruction) is complete.** The engine drives the whole destructible model: live per-instance
HP (C21), the progressive damage stages (C22), weapon fire that spends that HP (C23), the death
sequence at zero (C24), the collision that goes with it (C25), the debris **tumble** (C26), the
**`WeaponOrCollideHit` collision path** (C27), and **reset/restore** (C28). Two pieces are deliberately
deferred out of Wave C: the death **audio** (the one-shot `Sound` events — D31) and the **no-apex
falls** that author no collider test (`BL-245`). Two slices have since closed:
`PLAN-bounce-launch` made a bounce-terminated launch with an apex fly its own parabola and land for
real, and `PLAN-ground-contact` (2026-08-08) gave the 166 `do_intersections: true` bodies the
original's own collider test instead of letting them run their clock out below the terrain — see
the "Debris tumbles" bullet below for which pieces each covers. A format reader should know the current wiring:

- **Both source forms of `DAMAGE_SEQUENCE` are read.** The compiled archives deliver it as an
  ordinary sequence literally named `DAMAGE_SEQUENCE`; `AnimDefs.cs`'s reader front-end now parses
  the reader `DAMAGE_SEQUENCE` block into the same-named sequence (it once dropped it silently —
  its `ParseDef` switch had no case for it). So a reader-only destructible keeps its damage script,
  identical in shape to its compiled twin.
- **`HEALTH` is a mutable per-instance value, and the stages escalate against it.** At world build
  a `DestructibleRegistry` records one live HP pool per destructible node group, keyed by the
  `(def, anchor)` pair and seeded from the def's authored `HEALTH`; `AnimRuntime`'s
  `EvaluateCondition` reads that live value (`AnimHealth => registry HP <= num`) instead of the
  static `def.Health`, and `AnimRuntime.ApplyDamageStages(instance)` runs the cascade so the one
  effect for the stage the live HP now sits in fires (the water tower: black smoke at ≤36, fire
  smoke at ≤18; a three-stage object at ≤0.85/≤0.50/≤0.25 of its `HEALTH`). It escalates only when
  HP crosses a new, deeper threshold, so each stage's effect fires exactly once. Because one def's
  `NAME` is a wildcard, a single def can bind several node groups; each is an independent pool, so
  one tower's damage will not break its siblings.
- **Weapon fire reaches it (C23).** A projectile's raycast reports the struck collider; the runtime
  walks up from it to the owning destructible (`DestructibleRegistry.Resolve`), spends the weapon's
  `HEALTH_DAMAGE` — world destructibles carry HEALTH only, so there is **no armour pool** and
  `ARMOR_DAMAGE` does nothing to them — runs the damage stages, and marks the instance destroyed at
  zero. HE does 60 health damage and AP 40, so a HEALTH-60 tower dies to **one** HE rocket but
  **two** AP (AP is the worse building-buster, exactly inverting its anti-armour advantage); a
  40-cal gun at 4.5 each takes 14.
  - ⚠ **`Resolve` prefers the compiled def.** A wildcard reader `NAME` can grab an inner node the
    compiled def does not (the tower's `ap_h2otwr*` also matches `ap_h2otwr.flt`, which sits
    *between* the collider and the compiled `ap_h2otwr1` root), so the walk-up climbs the whole
    chain and takes the nearest **compiled** anchor — the def whose `DAMAGE_SEQUENCE` and death
    sequence are the real ones — not simply the first anchor it meets.
  - The patrol boat and truck are the only world objects also described by an AI-**vehicle** def
    (armour+health). M3 sees them only as scenery, so they are damaged through their **anim** def
    (`patrolboat` HEALTH 20), not the vehicle def (HP 40); the armour+health combatant model is M4.
- **The object dies at zero (C24).** When `DamageAt` empties an instance's HP the engine plays the
  def's death via `Start(def)` — its Initial sequences: the healthy→destroyed `OBJECT_ACTIVE_STATE`
  swap, the debris sequences and the puffer calls. Those sequences ARE the destruction (the def's
  `anim_name` is `h2twr_destruction1`/`destroy_mp1zreng11`), and the swap lives in a sequence whose
  name varies wildly (`destroyit`, `destroy_h2twr`, or unnamed — and never reliably `unknown_seq`)
  but is always `Initial`, so playing them all reaches every case without keying on a name.
  - **~90 % author their own swap; the rest don't.** Of the ~100 C1/C5 destructible defs surveyed,
    ~90 carry the healthy→destroyed swap in a sequence; ~10 (the C1 AA guns) declare the pair but
    author no swap. For those a generic fallback derives it from the def's own **RESET_STATE** —
    flipping the healthy/destroyed/dbase roles that base state named — applied only when RESET
    declares a `destroyed` node, so an object with no destroyed variant (a mission gun that dies by
    effect alone, `noseballgun`) is left intact rather than blanked. It reads the def's explicit
    RESET targets, never a world-wide name scan (the `ref_tank_dest` trap below).
    - **The fallback yields when the death CHAIN authors the swap one level down, in a
      `CALL_ANIMATION` target.** C2's `gate2` is the example: `gate2_doorblast` authors no swap
      itself and ends with `CALL_ANIMATION blockit2 START_TIME EVENT_OFFSET 28.5`, and `blockit2`
      (an OnCall def) is where the swap, `large_fireball` and the seven flying archway pieces
      actually live. Firing the RESET-derived fallback at t=0 there blanked the wreck 28.5 s before
      the authored explosion got to run against it (`BL-254`); `AnimRuntime` now resolves one level
      of `CALL_ANIMATION` targets the same way the dispatcher itself does and skips the fallback
      when a target authors the swap, so it arrives with the rest of `blockit2`'s effects instead.
    - **The fallback also yields when the def's own death already authors a visible destruction
      that simply omits the swap — the rescue is for a death that would otherwise show NOTHING.**
      `gate1` looked exactly like the AA-gun shape the fallback exists for (RESET declares
      `destroyed`, no swap authored anywhere, no chained def), but in the original gate1's archway
      is not destructible at all — only its doors are (`BL-254`, 2026-08-04, user recall). The two
      cases are told apart by what the def's OWN Initial sequences author: the AA guns'
      (`aagun32`–`36`) only sequence is a `DAMAGE_SEQUENCE` of pure `If`/`CallAnimation` puffer
      calls — no `ObjectMotionFromTo`/`ObjectMotion`/opacity/active-state event at all, so without
      the fallback they die invisibly. `gate1_doorblast` authors `door1`/`door2`'s own rotate,
      fade and deactivate directly — a deliberate, complete death that happens to leave the
      archway alone. `AnimRuntime.AuthorsVisibleDeath` scans for that shape (a move/fade/deactivate
      on a node outside the healthy/destroyed/dbase role set) and withholds the fallback when it
      finds one, so `gate1`'s archway now stays visible and solid permanently while its doors still
      fall and fade; `gate2` (chained swap, handled above), `kkgate`, `m_build01`, the C1 water
      tower, `agyrobus` and the C5 facade/window family all measured unchanged by this rule.
  - `reng11`'s wreck is a separately-`CALL_ANIMATION`'d template (`mp1reng_destroyed.flt`), not a
    child of the anchor — it stages correctly, alongside its `large_fireball`.
- **The debris tumbles (C26).** The wreck pieces fly: on death the def's `OBJECT_MOTION` events —
  gravity, a `translation_range` ballistic arc, a `forward_rotation` tumble and a `scale` ramp over a
  `run_time` — launch the pieces, driven by the generic `MotionRuntime` the M2 crash work already
  built (this was **not** new code for M3; the ballistic path was already implemented and only needed
  to be *reached* by a weapon-hit death, which C24's `Start` does). **It fires from the death, not the
  hit:** the launch is *scheduled* mid-sequence (the water tower's at t=2.2 s), so it only appears
  once the death animation plays out — a synchronous kill-and-check that never advances the clock sees
  no debris (which is why C24 wrongly recorded it "stubbed"). Measured by advancing the death: the
  water tower launches **2** visible pieces (`h2twr_middle` arcs from y≈5 to y≈19 in 0.8 s, tumbling,
  `run_time` 5 s), C1 buildings **7** each, passenger planes **2**; deaths that author no
  `OBJECT_MOTION` (the AA guns, `air_gen`) correctly launch **0**.
  - **Ground-rest is split, `PLAN-bounce-launch` (2026-08-03).** A census over all 17,568 extracted
    defs found 733 `OBJECT_MOTION` events naming a `bounce_sequence`, 529 of those with no authored
    `RUN_TIME` (217 def files) — the shape that means "fly until you land." Those 529 are two
    populations, not one. ⚠ **A third population omits the `bounce_sequence` too** and was found
    only by `BL-257`'s later census — see the "no `RUN_TIME`, no `BOUNCE_SEQUENCE`" bullet below;
    what admits a launch to the solve is the **apex**, not the bounce.
    - **152 (150 reachable in an executed `sequences`) are upward launches** — positive launch speed,
      negative gravity, so the parabola has an apex. For these, `MotionRuntime.FlightToLaunchHeight`
      solves `t = 2·v0.y / |accel.y|` and ends the flight there instead of holding the final pose: a
      **⚠ CHOICE, not a decode** — the original tested real collision via `do_intersections`;
      a down-ray would only approximate it, and agrees with the launch-height solve wherever the
      ground under the piece is flat, which is every one of the 150 measured (debris off a
      ground-sitting structure). **All 120 of this shape author `do_intersections: false`, so the
      original was not collision-testing them either** — the launch-height solve stands, and
      `PT-46` (d) confirmed at the controls that the original's debris sinks through terrain the
      same way. Landing then dispatches the named `BOUNCE_SEQUENCE` (`default` only — none of the
      150 carry a `water`/`lava` branch), which runs the piece's own `OBJECT_ACTIVE_STATE …
      INACTIVE` and stops its trail puffer.
    - **Where `do_intersections` IS authored, the engine now runs the original's own test
      (`PLAN-ground-contact`, 2026-08-08).** 166 events install-wide carry it — 150
      `RUN_TIME`+bounce, 16 `RUN_TIME` — and they are exactly the bodies that used to run their
      clock out and finish below the terrain: `player_crash_dirt`'s `piece1`–`4`, the eleven C1B
      airframes' `MAIN_ROOT_NODE`, and every `agyrobus` motion. `MotionRuntime.TryContact` sweeps a
      **segment along the trajectory** (last origin → next origin, in world space) each frame and
      ends the body at the first collider, arming the `BOUNCE_SEQUENCE` there. A segment, not a
      down-ray, and the difference is the point: it can rest a piece on a rooftop or stop it
      against a wall. `RUN_TIME` becomes a ceiling rather than a duration.
      - The branch is chosen from the **struck surface** — water where the block authors one
        (`agyrobus`' `pN_wtr_hit` vs `pNgrndhit`), `default` otherwise, through the same
        `ProjectilePool.ClassifySurface` a round's impact reads. ⚠ **`lava` is dead data**: 0 of the
        install's 324 `BOUNCE_SEQUENCE` blocks name a lava branch. It is engine baggage from
        another title — the only lava here is C3's volcano, far too small to throw debris into.
      - ⚠ Arming is a **launch-relative epsilon** (2 m or 0.1 s, whichever comes first, both TUNE),
        not an apex test: a piece starts inside the wreck it left and an airframe inside its own
        hull, and the airframes fall from rest, so an apex gate would never arm them.
      - A session that builds no colliders hands the runtime no mask (`AnimRuntime.ContactMask`)
        and keeps the flag-free behaviour exactly — which is why every golden capture is unchanged.
    - **The other 379 have no apex and are still deferred (Layer-1.5, `BL-245`).** 335 free-falling
      zeppelin `gasbag1`/`crashnode1` pieces start from rest, ~17 lifeboats
      and turret parts are thrown downward, and 8 zero-gravity `chuteman` descents fall at a constant
      rate — none has a parabola to solve. ⚠ **Not blocked on a ground ray, whatever the older
      framing said**: the ray exists now, and these author `do_intersections: false` (8 for 8 across
      `gasbag`/`cargozep`/`chuteman`/`lifesaver`), so the data says do not test them. What they are
      blocked on is a *decision* to diverge from that — a zeppelin hanging in the air looks broken
      to a player, which is a playability argument, not a faithfulness one. `MotionRuntime` still integrates these freely over the run time and then
      holds the final pose; the pieces arc/fall and are then hidden by the sequence's own
      `OBJECT_ACTIVE_STATE`, so they read fine without it.
  - **No `RUN_TIME`, no `BOUNCE_SEQUENCE` — the third launch shape, `BL-257` (2026-08-06).** A
    census of every ballistic `ObjectMotion` in all 8 chapters' `cam_anim`
    (`analysis/bl-257-nulled-launch/`) found **167 events / 119 distinct defs** that name *neither*
    field: the piece is launched and then switched off by **its own null-start `ACTIVE_STATE 0`**
    downstream, with no bounce to end the flight. Every `m_stuff_blowup`/`m_gens_blowup`, both
    `loading_crane`s, `destroy_jsign`, `col_tower_destroy`, `mineshack_destroy`, `shaft_destroy`,
    `sluice_destroy`, `switchhouse_destroy`, `collapse_platform` — and `dblcannon_flying_parts`, the
    zeppelin cannon's eight parts, which is the reachable repro (`--effects-test`, via
    `biggun_flying_parts`).
    - Read as duration 0 — which is what a bounce-gated solve left them as — the deactivation lands
      **on the launch tick** and the piece is hidden before it moves. The solve gate is therefore
      the **absent `RUN_TIME`**, not the bounce; `PendingBounce` still arms only where a
      `BOUNCE_SEQUENCE` is named, so this shape flies and owes nothing.
    - All 167 carry gravity (−9.8 on 161, −10 on 6) and author `do_intersections` **false**. **159
      always launch upward** and solve. The other **8 are `BL-245` falls wearing this shape** and
      are declined by the same no-apex guard: `bridge_destroy01`'s truck (level), `rope1burn`'s five
      burning rope ends (−0.44…−1.0 m/s ± ~1), and both `fuelboxbreaks` rockerarms (elevation 90°
      but speed **−45…45**, so half the draws point down). ⚠ For the spherical `translation_range`
      form the vertical speed is `sin(elevation)·speed` — **a negative speed inverts an upward
      elevation**, which is the only reason the rockerarms are not in the solved 159.
    - 159 of the 167 are switched off downstream with every intervening event null-start
      (`destroy_pwr_station` puts three `CALL_ANIMATION`s between launch and hide); the other 8 are
      the last event of their sequence and nothing hides them at all.
- **Death audio (D31) is still stubbed.** The explosion is silent; the one-shot `Sound` events are
  not yet played.
- **Flying into a collide-destructible breaks it (C27).** `ACTIVATION` decides what a plane
  *collision* does. The **44** `WeaponOrCollideHit` objects — the C2 Hollywood facades
  (`fcpan01`–`39`), the C5 warehouse windows (`w_win01`–`04`, all health 0.01) and the lone
  substantial `agyrobus` (health 70) — take collision damage and shatter, and the plane flies
  **through** them (they are set dressing). Every `WeaponHit` object (water towers, gates, signs) is
  **untouched** by a collision — ram one and it kills the plane and stands (decision 6; the 0.01
  health is the tell). `FlightController` resolves the struck collider (`Registry.Resolve`) and calls
  `AnimRuntime.CollideDamageAt`, which gates on `ACTIVATION` and, when it matches, spends a
  severity-scaled `HEALTH_DAMAGE` (`vn × 8`, so a real flight-speed hit breaks even `agyrobus`) through
  the same `DamageAt` a weapon uses — so the object's death (swap, debris, collider removal) is
  identical whether shot or rammed. Verified headlessly: the facades/windows/`agyrobus`
  `collide[✓ broke]`, the signs and `kkgate` `collide[✗ ignored]`. The **owed playtest** is the
  in-flight feel — flying through a facade cleanly vs. crashing into a tower.
- **A destroyed object can be reset to healthy (C28).** `AnimRuntime.ResetDestructible` is the inverse
  of the death, for the debug tools (F40/F41) and respawn: it `Stop`s the def's live death, restores
  the authored pose of any node the death physically MOVED (the ballistic debris — `Stop` removes the
  motion but leaves the piece where it flew), re-applies the def's `RESET_STATE` (healthy visible +
  collidable, destroyed hidden — undoing both the swap and the `ApplyDeathSwap` fallback), and restores
  the instance's HP/status/stage. It is idempotent: **destroy → reset → destroy produces identical
  results.** Verified across C1/C2/C5 — every type (buildings, towers, the AA gun's RESET-derived swap,
  the doors' rotated leaves, the propane gate, agyrobus, the facades, and `gate1`'s permanently-solid
  archway) returns `healthy=✓` and re-kills in the same hit count.
- **Collision follows the swap for free (C25).** The `OBJECT_ACTIVE_STATE` swap toggles
  `SetSubtreeActive`, which disables/enables the subtree's *colliders* alongside its visibility — so
  the death that hides the healthy geometry also stops it blocking flight, and the wreck it shows
  becomes solid, with **no separate collider code**. Measured on the C2 (Hollywood) gates: killing
  `kkgate` switches its healthy collider **off** and the wreck ones **on** immediately (it also
  chains the bridge fires). C1 buildings match (`m_build01`). The studio gates are each an
  exception, in opposite directions, since `BL-254` (2026-08-04): **`gate2`'s** swap — and so its
  collider flip — is deferred to `blockit2`, the `CALL_ANIMATION` `gate2_doorblast` schedules
  28.5 s into the death; a census taken at kill time reads no change at all (`off 0, on 0`), by
  design. **`gate1`'s** archway never swaps at all — its collider census reads `off 0, on 0` at
  every kill, permanently — only its doors' own collider drop (on their own ~1–6.7 s rotate/fade
  timeline, unrelated to the swap) removes anything solid; see the RESET_STATE-fallback bullet
  above for both. The **in-flight playtest is now run, and a blown-open door does leave a clear
  passage**: shooting the propane tank at the controls throws the door pieces, and they lose their
  colliders as they begin to fade. The destroyed variant re-adds colliders of its own, so "clear
  passage" was never something the swap alone could decide — what clears it is the fade path, which
  is independent of the swap and of the registry: `ObjectOpacityState`/`ObjectOpacityFromTo` reach
  `AnimRuntime.SetSubtreeOpacity` (`AnimRuntime.cs:1393`), and below `OpacityCollisionEpsilon`
  (0.01) that calls `WorldCollision.SetFaded` (`WorldCollision.cs:52`) on the subtree. A fading
  wreck piece is therefore non-solid whether or not anything up its chain has `HEALTH > 0`.
  - **The propane→door chain works end to end.** Hollywood's `kkgate` is a WeaponHit destructible
    whose `ANIMATION_ROOT_NAME` is the **`propane` tank** (a collidable, therefore shootable node),
    HEALTH 10; shooting *it* runs the gate's death — the healthy→destroyed swap plus `CallAnimation`
    to `genx12`, `tbridg1_fire`/`tbridg2_fire` (the bridges catch fire) and `free_the_goose`. The
    door itself is not directly damageable; the propane tank is the trigger, exactly as the original
    plays it. Confirmed at the controls: the tank's kill throws the door pieces and they go
    non-solid as they fade, leaving the gate flyable. ⚠ **The propane tank belongs to `kkgate`, and
    its chain reaches nothing on the Seaplane Hangar.** `sghangar-opensgdoors` is a *different*,
    HEALTH-0 OnStartup animation on `sgh_door1`/`sgh_door2` — those doors open and stay as solid
    set-dressing by design, and no propane→`sghangar` chain is authored. **A/B'd against the
    original (Instant Action, 2026-08-08): the SeaHangar doors are not destructible there either**,
    so the remake matches and there is nothing to author. A "the SeaHangar doors didn't despawn"
    report from a propane kill is the Hollywood gate being misnamed.
  - **`genx12` is a parameterized exploder template — its `pt1..pt12` are placeholder nodes, not
    world pieces (D31).** The def is a parentless root (`genx12`, C2 gamez 39) whose own `pt*`
    children are meshless; a death calls it with `operand_node=<wreck node>` (kkgate:
    `destroyed`, whose 12 children are ALSO named `pt1..pt12`, authored in the closed-gate pose),
    and the template's `ObjectMotion` launches / opacity fades / `ObjectActiveState` offs are
    meant to bind to the *call-site's* same-named pieces — resolved via `AnimRuntime.Targets`'
    narrow `genx12` rescue (`FindAll` scoped strictly inside the caller's own anchor subtree, never
    a world-wide name scan), so `genx12`'s own template root never needs to exist as a real,
    built node at all. The pieces fly (6 s ballistic), fade over 3 s, then deactivate — which is
    what finally drops their colliders and opens the passage; for the first ~3 s the tumbling
    wreck is still solid, by the data. C1's `air_gen` chain uses the same `operand_node` idiom.
    `facade_parts` (below) is a DIFFERENT idiom — a genuinely separate template, resolved and
    relocated rather than redirected onto the caller's own subtree; do not conflate the two.
  - **`facade_parts` needed its template BUILT, pooled, and resolved data-driven, not by name
    (`BL-253`, 2026-08-04, extended same day).** The 39 `fcpanNN` deaths each `CALL_ANIMATION
    facade_parts AT_NODE fcpanNN` — no `operand_node`, so unlike `genx12` this is a real, separate
    template (`facdsticks`, four wooden "log" sticks) that needs to exist and be moved. It is
    parentless and outside the world's spatial-partition grid — the same shape `genx12` and (also
    found via this bug) `mp1reng_destroyed.flt` share — so `WorldBuilder`'s own gamez walk never
    reaches it. `GameZ.IsLibraryRoot` tests exactly that shape (empty `Children` membership, absent
    from the World node's spatial partition) — a data-driven rule, not a curated name list — and a
    death-triggered `CALL_ANIMATION` (`_deathCallDepth > 0`, never the ambient world boot, and
    never an `operand_node` redirect) to any node passing that test builds it lazily on first use
    (`WorldSession.ResolveLibraryRoot`) and relocates it onto the call site. It is also POOLED, not
    single-shared: the original shows several panels' debris flying in parallel (confirmed
    same-day, original-game A/B), so each caller gets its own built copy, up to
    `EffectPools.LocalCallPoolSize("facdsticks")` (6, `CSVM/data/effect_pools.json`'s
    `localCallRoots`, an invented `BL-231` TUNE number — `facdsticks` ships only one node record, so
    unlike a few effect templates that ship an authored N-copy set, its pool size is not decodable
    from the data). Pooled copies are indexed via `AnimRuntime.IndexPooledCopy`, which skips
    `_byIndex` registration so multiple copies of the same source node don't collide on the first
    copy's binding — each is instead disambiguated via the same anchor-scoped name rescue
    `genx12`'s own `Targets()` fallback uses.
  - ⚠ **Colliders exist only in the flight build.** `WorldSession.Options.Collision` is `_fly`
    (plus `_damageTest`); `--freecam` builds the world with **no** collision at all, so any collider
    census run there reads zero and lies. See `docs/verification.md`.

Engine internals belong in `docs/architecture.md` (the `AnimRuntime`, `AnimDefs`,
`DestructibleRegistry` and `Projectile` entries); this page states the fact, not the wiring.

### ⚠ Trap: never key the healthy↔destroyed convention on a substring

The `_healthy`/`_destroyed` naming is tempting to sweep with a substring or suffix match — and
that is a bug. A pass that hid every still-visible node whose name *contains* `"destroyed"` (in
`AnimRuntime`'s `HideUncoveredDestroyed`) once wiped C1's `ref_tank_dest`, which is the parent
GROUP of the *healthy* harbour refuel tanks — "destructible", not "destroyed". A `_dest` suffix
rule fails the same way. The healthy/destroyed pairing is a convention on node *roles*, not a
guarantee about substrings; resolve it through the definition's own `ANIMATION_ROOT_NAME` and its
sequences' explicit `OBJECT_ACTIVE_STATE` targets, never by scanning names.
