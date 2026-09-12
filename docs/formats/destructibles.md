# World destructibles

Part of the [format documentation](README.md). How an `ANIMATION_DEFINITION` doubles as a
destructible world object, a hit-point pool, a damage-threshold script, and a death sequence
layered onto the ordinary animation container.

A destructible object is not a separate file type and has no format of its own. It is an
`ANIMATION_DEFINITION`, the container, sequences, events, node binding and both source forms
(zrdr readers vs the compiled `cam_anim`/`mis_anim` archives) are all documented in
[anim-definitions.md](anim-definitions.md), and everything there applies unchanged. This page
covers only the extra layer that makes one destructible: the `HEALTH` pool, the `ACTIVATION`
mode, the `DAMAGE_SEQUENCE` threshold script, the death sequence, and the collide set.

## Contents

- [Destructible pieces](#destructible-pieces)
- [Activation](#activation)
- [Health thresholds](#health-thresholds)
- [Damage and death sequences](#damage-and-death-sequences)
- [Starting destroyed](#starting-destroyed)
- [Definition binding](#definition-binding)
- [Which node takes the hit](#which-node-takes-the-hit)
- [Water-tower example](#water-tower-example)
## Destructible pieces

A destructible is any animation definition whose header carries these:

| Field | Reader key | Compiled key | Meaning |
|---|---|---|---|
| Hit points | `HEALTH [n]` | `health` (float) | The object's HP pool. **Any def with `health > 0` is destructible**, this is the whole test. |
| Damage mode | `ACTIVATION` (usually absent) | `activation` | The plane's fate on contact: `WeaponHit` (solid, graze/crash) or `WeaponOrCollideHit` (breaks, plane flies through). Both take collision damage. |
| Damage script | `DAMAGE_SEQUENCE [ … ]` | a sequence named `DAMAGE_SEQUENCE` | A threshold script of `IF ANIM_HEALTH n` branches escalating visible damage as HP falls. |

Everything else is ordinary sequences and events. When the object dies, a normal named
sequence (the **death sequence**) runs the healthy→destroyed swap, debris, fireballs and smoke.

Across the compiled archives of this install, **2,603 defs carry `health > 0`**; of those,
**2,565 are `WeaponHit`** and **44 are `WeaponOrCollideHit`**. The `ANIM_HEALTH` conditions live
exclusively inside `DAMAGE_SEQUENCE` (4,131 of them, none anywhere else).

## Activation

The reader source usually omits `ACTIVATION` on a destructible entirely (the water-tower def
below has no `ACTIVATION` key); the compiler assigns the default `WeaponHit`. The compiled form
always states it explicitly. The two values:

- **`WeaponHit`**, the object is **solid**: a plane flying into it grazes or crashes. This is
  the overwhelming majority (2,565 defs): buildings, tanks, towers, refinery structures, AA guns.
- **`WeaponOrCollideHit`**, the object **breaks and the plane flies through unharmed**. Exactly
  **44 defs** in the whole install, and they are a deliberate, hand-picked set (below).

> ⚠ **The enum gates the PLANE's fate, not the object's.** Every destructible takes severity-scaled
> collision damage, whichever value it carries: ramming a `WeaponHit` building plays both the plane
> crash and the building's destruction, and a survivable graze advances its `DAMAGE_SEQUENCE` stages
> while the plane flies on. Reading it as "what can damage it" (`WeaponHit` = weapon fire only,
> ramming leaves the object intact) is the mistake to avoid; the census below does not support it.
> The crash explosion has no blast radius, so the damage is contact-borne: crashing beside a
> destructible damages it not at all.
>
> `activation` is the animation record's `+0xa1` byte. The animation-definition loader
> `FUN_005230d0` registers a damage handler per record keyed on it (0 registers slot 0 only, 1 slot 1
> only, 2 both), and the slot-0 handler `0x004e7220` re-checks the byte itself, accepting
> `+0xa1 ∈ {0, 2}` and refusing everything else (`0x004e7234`). A collision is delivered through slot
> 0 as an ordinary `wep_24` weapon hit, so `WeaponHit` (0) and `WeaponOrCollideHit` (2) both take it.
> The handler subtracts the object's own damage reduction (`+0xbc`) from the incoming health damage,
> applies the remainder to the pool at `+0xb8`, and re-runs the `DAMAGE_SEQUENCE` evaluation, which
> is where the severity scaling shows. See [`../org/flightModel.md`](../org/flightModel.md)'s
> "Collision damage" for the severity law and the handler's arithmetic.

`proximity_damage` is a **separate** header flag and is `false` on every case examined here,
including both collide members and plain `WeaponHit` destructibles, it does *not* encode the
collide mode. The collide behaviour is carried entirely by the `activation` enum.

### The 44 collide-destructibles

| Chapter | Objects | Health | What |
|---|---|---|---|
| C2 | `fcpan01`–`fcpan39` | 0.01 | Hollywood facade panels, fly-through set dressing (39 defs) |
| C5 | `w_win01`–`w_win04` | 0.01 | Warehouse windows (anim names `smash_me1`–`smash_me4`) |
| C5 | `agyrobus` | 70.0 | The autogyro bus, the only substantial collide-destructible |

39 + 4 + 1 = 44. All but the autogyro have `health 0.01`, i.e. they shatter on the lightest
touch: they exist to break when you fly through them, not to be a combat target. Everything
outside this set is solid on contact, but still takes collision damage (see the ⚠ above).

## Health thresholds

A `DAMAGE_SEQUENCE` is a single sequence whose body is an `IF`/`ELSEIF`/`ELSE`/`ENDIF` chain
(the condition and control-flow events themselves are documented in
[anim-definitions.md](anim-definitions.md)). Its condition is `ANIM_HEALTH n` (compiled:
`AnimHealth`), and:

> **`ANIM_HEALTH n` means `health <= n`**, "worn down to n or below", **not** "equal to n" and
> **not** a fraction. The value is an absolute HP number in the same units as the def's `HEALTH`.

Because the test is `health <= n`, the script **must list the lowest threshold first**: an
`IF health <= 18` has to precede `ELSEIF health <= 36`, or the `<= 36` branch would swallow every
value that also satisfies `<= 18` and the more-damaged effect would never be reached. So the
authored order runs *most-damaged branch first*, which reads backwards from the object's
timeline but is exactly right for a falling-through `<=` cascade.

**The fractions are a reading aid, not the encoding.** Damage stages are conventionally placed
at round fractions of a def's own `HEALTH`, the recurring thresholds are 0.85, 0.75, 0.60, 0.50,
0.30 and 0.25, but each is written out as the absolute product (`0.60 × 60 = 36`). Two
progressions dominate:

- **Two-stage `{0.60, 0.30}`**, smoke then fire. The standard escalation calls
  `sputter_black_smoke_obj` at the 0.60 stage and `sputter_fire_smoke_obj` at the 0.30 stage
  (48 defs use each).
- **Three-stage `{0.85, 0.50, 0.25}`**.

## Damage and death sequences

The two are different sequences with different jobs:

- **`DAMAGE_SEQUENCE`** is *progressive* damage, the smoke/fire escalation while the object is
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
`big_ripple` (1). The other 164 calls target 22 live-object choreography definitions,
zeppelin/aircraft/vehicle motion, node swaps, or wrappers into that handled set, and must
remain on the world runtime rather than be relocated as effect templates. The exhaustive
classification and per-target shapes are in the retired `analysis/death-effect-closure/`
(`git show analysis-archive:analysis/death-effect-closure/FINDINGS.md`).

**`unknown_seq` is not the death sequence.** The compiled def carries one structurally-distinct
trailing sequence slot the mech3ax fork surfaces as `unknown_seq` (see
[anim-definitions.md](anim-definitions.md) for the container detail); it is non-null on 1,544
defs, all of them `health > 0`. It holds destruction-time content, but *which* sequence lands
there varies, for the water tower it is the second puffer sequence (`h2twr_puffer2`), while the
actual death swap `destroy_h2twr` sits in the regular `sequences` array. Do not treat
`unknown_seq` as a reliable pointer to the death swap.

**The slot is loaded and dispatched at death.** `CompiledAnim` parses it
into `AnimDefinition.DeathSlot`, deliberately OFF `Sequences`, so bootstrap and the
sequence-walking derivations never see it, and `AnimRuntime.RunDeathSequence` runs it as an
extra runner on the death's own instance. Across all 12,693 compiled defs:
every non-empty slot sits on a `health > 0` destructible (none elsewhere), and 1,429 of the
1,430 mission-archive slots dispatch calls no listed sequence reaches, this block is where
~all of `large_30sec_fire`'s 1,035 death calls live, so before it dispatched, the game's
most-called death fire never played from a compiled death site at all.

## Starting destroyed

An object can read destroyed from the first frame without ever taking a hit: a start-state
script or an `ON_STARTUP` sequence can author the same healthy→destroyed
`OBJECT_ACTIVE_STATE` swap the death sequence runs, outside `DamageAt`. The engine's
`AnimRuntime.SyncDestructiblePool` mirrors that swap into the object's `DestructibleRegistry`
pool (`Health` to 0, `Status` to `Destroyed`) whenever the dispatched event's role name matches
the healthy/destroyed/`dbase` convention, so a later hit finds the pool already dead instead of
replaying the whole death choreography on an object that already looks wrecked. Bootstrap
registers each anchored destructible before dispatching its own `RESET_STATE`, so a def
authored to start destroyed in its `RESET_STATE` reaches the same sync; no shipped def in this
install uses that shape, so it is verified structurally rather than against authored data.
Regression: the `start-state-swap-pool` suite (`CSVM/src/Testing/DestroyChoreographySuites.cs`).

⚠ `dbase` ON is only half a death, and never a death in a `RESET_STATE`, which states what
stands rather than what has fallen. A sweep of every def in all eight chapters' mission programs
finds `dbase` switched ON in a `RESET_STATE` for three defs only: the two 8-inch cannons
(`8igun01` in C3, `8igun99` in C4, the mansion gun), where the node is the concrete plinth a live
gun stands on beside `healthy` ACTIVE, and C3's `susp_bridge`, whose baseline names no
healthy-role node at all. Every other `dbase` activation in the install is in a sequence or a
death slot, across 128 sequences. Three of those families carry no other destroyed-role switch in
the block (`litemast*`, `sboxes*`, `tugandbarge0*`), so the `dbase` rule cannot be dropped, only
kept out of the baseline. The one sequence-side activation that is not a death is the Barracuda's
`sub_movement`, which raises `dbase` with the hull as the submarine surfaces; it carries no
`HEALTH` and drives no pool, so the rule never reaches it. Regression: the `chapter-census`
suite asserts both cannons boot standing, and `called-death-chain` asserts the bridge does.

⚠ The same swap read backwards is a revival, and a def's own death choreography is never one. A
wreck that clears itself away switches its own destroyed-role nodes back off as a late step of
dying, and `dbase` is a destroyed-role name here, so taking those events for a repair hands a
killed pool full HP back after the visible death has played. The sweep finds fourteen defs
authoring a revival-shaped `OBJECT_ACTIVE_STATE` inside a non-ON_CALL sequence, all of them a
wreck removing itself: the Barracuda (`sub_destruction`, in both C3/M03 and C3/IA1) switches
`dbase` off in the same block that hides `subhealthy` and shows `subdestroyed`, then sinks
`subdestroyed` away 115 s later; C2/M01's four barges (`tugandbarge01` to `04`) drop `dbase1`
some ten seconds into `barge_destroy0n`, once the hull has settled; and C1/M05's nine lifesavers
(`lifesaver11` to `lifesaver33`) switch `healthy` and `destroyed` both off in one breath. No def
in the install authors a genuine repair there, so a revival can only arrive from a `RESET_STATE`,
an ON_CALL sequence, or another def's script. Regression: the `death-not-a-revival` suite.

The cross-mission state log is the other way an object starts destroyed. The original opens a
later mission of the chapter on the carried state itself, a destroyed pose (the `PERSIST_LOG`
reader defs such as `ucamp_dest` and `tower_dest` are the silent destroyed variants), never on a
replayed death; see [saved-games.md](saved-games.md). The engine's `CampaignPersistLog.ApplyTo`
therefore goes through `AnimRuntime.CarryState`, not `DamageAt`: the pool is written directly
(`Health` 0 and `Destroyed`, or the carried HP at its `DAMAGE_SEQUENCE` stage) and the nodes take
the pose the death ends in, read off the death's own sequences as the healthy-off and
destroyed/`dbase`-on switches. No fireball, debris, sound or stage puffer plays, and a later hit
on the object finds the pool dead and is a no-op. ⚠ That read follows `CALL_SEQUENCE` into the
def's own `ON_CALL` sequences, because nothing replays a call when the pose arrives without
choreography: C3's `susp_bridge` switches two of its spans off in `part1_fire_puffer` and
`part5_fire_puffer`, and a pose stopping at the main sequences drops the ropes and leaves those
two hanging in the air. Regression: the `carried-state-silent`, `carried-pose-called-sequence`
and `campaign-persistence` suites.

## Definition binding

A destructible def anchors to scene nodes exactly like any animation definition (full rules in
[anim-definitions.md](anim-definitions.md)), and two of those rules do the work:

- **`NAME` is a wildcard, so one authored def serves many instances.** The water tower's
  `NAME` is `ap_h2otwr*`; in C1 that one reader definition compiles into four independent
  instances (`ap_h2otwr1`, `ap_h2otwr2`, `ap_h2otwr4`, `ap_h2otwr5`), each with its own HP.
  - ⚠ **The compiler does NOT always give the expanded instances distinct names, so `NAME` alone
    cannot identify one.** C1's two airfield hangars are both `NAME air_gen` with
    `ANIMATION_ROOT_NAME healthy`; the only thing separating them is the symbol table, where
    `air_gen` names `eairg32`'s nodes and `air_gen#1` names `eairg31`'s. The same holds at scale
    for zeppelins: every mission's turret/engine defs are `ctur1`…`leng42` on every airship, and
    a chapter's gamez carries up to nine of them. **Bind a compiled def through its symbol table,
    never its `NAME`**, matching by name gives each twin all the anchors and they cross-bind.
- **`ANIMATION_ROOT_NAME` names the node the anim attaches to inside each instance**, and the
  destructible convention is a paired `healthy`/`destroyed` node group. The tower roots on
  `h2twr_healthy` and its death sequence toggles `h2twr_healthy`↔`h2twr_destroyed`. This
  `_healthy`/`_destroyed` (also bare `healthy`/`destroyed`, and object-specific spellings) pairing
  is pervasive: C1's gamez alone carries hundreds of such nodes.

## Which node takes the hit

A destructible's HP pool does not sit on its `NAME` anchor. The animation-definition loader
`FUN_005230d0` walks every record and, for `activation` 0 or 2, calls
`FUN_005abbf0(record, *(record + 0x6c), …, 0x004e7220)`. The slot-0 weapon-hit handler is therefore
registered on **`def+0x6c`, the animation-root node**, which is `ANIMATION_ROOT_NAME` resolved
inside the definition's own subtree, falling back to `def+0x48` (the `NAME` anchor) when the root
name is absent or resolves to nothing ([../org/sequences.md](../org/sequences.md), "The two roots are
different fields"). `FUN_005abb20` hangs the handler off a list on the node itself (`node+0xbc`), so
several definitions can register on one object without displacing each other, each on its own node.
The impact dispatcher `FUN_005abcf0` reads that list off the **struck** node alone: it takes the
hit record's node (`hit+0x24`, written by `FUN_004c7f50` for whichever node's model the ray met) and
returns 0 when `node+0xbc` is null. The original never walks a parent chain to find an owner, so a
round on a piece that registered no handler does nothing at all to the group around it.

> **A hit belongs to the definition whose animation root covers the piece that was struck**, not to
> the definition that happens to name the group. Two destructible defs anchored on one node are told
> apart by nothing else.

That matters wherever one object authors two independent deaths. Across the whole install, 10 of the
2,603 destructible defs' `NAME` groups carry defs with **different** animation roots:

| Where | `NAME` | The two pools |
|---|---|---|
| `C1/M05/mis_anim` | `lifesaver11`–`lifesaver33` (9 sites) | `lifefallNM` `HEALTH 60` rooting on `lifeballoon`, the balloon's own burst-and-fall; `lboat_destructionNM` `HEALTH 40` rooting on the `lifesaverNM` group, the lifeboat's explosion |
| `C2/cam_anim` | `sghangar` | `tbridg1_fire` `HEALTH 15` on `tarzan_bridg1`; `tbridg2_fire` `HEALTH 5` on `tarzan_bridg2` |

CM10's attack balloons are the worked case. Shooting the envelope has to reach `lifefallNM`, whose
eight `OBJECT_MOTION` events drop the `lifeboat` under `GRAVITY -10`, drop `lifeballoon` under
`GRAVITY -5` and fling `b_part1`–`b_part6` at 10–16 m/s, alongside seven `OBJECT_OPACITY_FROM_TO`
fades and fourteen puffer states. Reaching `lboat_destructionNM` instead leaves the envelope with
nothing but the RESET-derived healthy→destroyed swap, so the destroyed balloon hangs where it died.

## Water-tower example

`extracted/C1/zrdr/ap_h2otwr.zrd.json` is one reader definition, `NAME ap_h2otwr*`,
`HEALTH 60`, no `ACTIVATION` key (the compiler assigns `WeaponHit`). Its structure, with the
real values:

**`RESET_STATE`** (base state at load), `h2twr_healthy` ACTIVE, `h2twr_destroyed` INACTIVE.

**`DAMAGE_SEQUENCE`**, the progressive-damage script:

```
IF     ANIM_HEALTH 18 -> CALL_ANIMATION sputter_fire_smoke_obj  WITH_NODE h2twr_healthy
ELSEIF ANIM_HEALTH 36 -> CALL_ANIMATION sputter_black_smoke_obj WITH_NODE h2twr_healthy
ELSE ENDIF
```

With `HEALTH 60`, the thresholds are 36 (= 0.60) and 18 (= 0.30), the `{0.60, 0.30}` smoke-then-
fire progression: black smoke once worn to `≤ 36`, fire smoke once worn to `≤ 18`. Note the
lowest threshold is tested first, as the `<=` cascade requires. The compiled form delivers this
same block verbatim as a sequence literally named `DAMAGE_SEQUENCE`, with `AnimHealth 18.0` then
`36.0`.

**Death sequence** `destroy_h2twr`, `h2twr_healthy` INACTIVE, `h2twr_destroyed` ACTIVE (the swap).

**Ballistic debris**, two unnamed sequences fling the mid and upper tower sections as rigid
bodies via `OBJECT_MOTION` (the ballistic form; channels documented in
[anim-definitions.md](anim-definitions.md)). For `h2twr_middle`: `START_TIME` +2.2 s,
`GRAVITY LOCAL -2`, `TRANSLATION_RANGE_MIN [10, 30, 2.5, 0]` / `TRANSLATION_RANGE_MAX
[10, 70, 4.5, 0]`, `FORWARD_ROTATION [TIME 60, 0]`, `SCALE [-0.1, -0.1, -0.1]`, `RUN_TIME 5`, then
`OBJECT_ACTIVE_STATE h2twr_middle INACTIVE`. `h2twr_upper` is the same with `START_TIME` +2.0 s
and `FORWARD_ROTATION [TIME 70, 0]`. (The negative `SCALE` is the section shrinking to nothing as
it tumbles clear. `TIME 60` is 60°/s, a rate, not a total angle, turned about the horizontal
perpendicular of that draw's own 30–70° launch, so the section turns at 0.22–0.7 rad/s.)

**Puffers**, two `ON_CALL` sequences (`h2twr_puffer`, `h2twr_puffer2`), each a `PUFFER_STATE`
loop of 13 iterations on the `splashbase` texture (`PUFFER_STATE` schema in
[effects.md](effects.md)), invoked from the death sequences via `CALL_SEQUENCE`.

## Remake node resolution (`DestructibleRegistry`)

A raycast hit lands on a collider deep under an anchor's subtree, so resolving it climbs the whole
parent chain rather than stopping at the first registered anchor: a compiled def and a reader
wildcard can anchor to *different* nodes of one object. The water tower's compiled def roots on
`ap_h2otwr1` while its reader def's `*` also grabs the inner `ap_h2otwr.flt`, which sits nearer the
collider, and the compiled def is the authoritative one, since its `DAMAGE_SEQUENCE` and death
sequence are the real ones. The nearest **compiled** anchor up the chain wins; failing any compiled
anchor, the nearest reader one.

Each pool claims two nodes on the way in: its **damage node** (the animation root above) outright,
and its anchor only as a fallback, which is what separates the ten two-pool objects. The anchor
claim is kept rather than dropped for the original's one-node-only rule, because most defs root on a
node their own death then hides and a hit on the wreck must still find the pool that owns it; an
own-root claim outranks an anchor claim whichever order the two defs register in. `Instance.Anchor`
stays the animation anchor, so `ANIM_HEALTH` evaluation, the objective marker layer and the AI
target pool are unaffected by which node the hit resolves through.

The anchor claim reaches only as far as the pool's own piece being in the world. While a live pool's
damage node is switched off, a climb that arrives at that pool through its anchor alone answers with
nothing, and the climb stops there rather than continuing up. C2B/M04's Gemini is the case: the
deploy definition's `RESET_STATE` holds `gunback` and `gun1` inactive and `upper_br_door` shut, so
the hatch is the only solid geometry over a stowed broadside cannon, and a round on it would
otherwise drain the cannon's HP through `lbroad11`. Climbing on is worse than stopping, since the
next claim up is the airship's gasbag. A destroyed pool is exempt: its own death hid the same node,
and a hit on the wreck still belongs to it.

Two death-sequence shapes the registry's per-instance state has to track beyond the swap above:

- **A chained death**, where the healthy→destroyed swap is authored in a `CALL_ANIMATION` target
  rather than the def's own sequences (C1's `gate2` chains to `blockit2`). The registry remembers
  the chained def so a reset can stop it too and restore whatever it moved.
- **A death that dispatches `CALL_ANIMATION` onto its own anchor**, not a chained def, C2's facade
  panels each call the shared `facade_parts` template. The registry records the actual anchor the
  call used (a pooled library root uses its own copy, not the call site) so a reset can find and
  stop it.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
