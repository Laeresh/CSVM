# Weapon effect readers & projectile prototypes

Part of the [format documentation](README.md). The muzzle, flyout and impact assets that a
weapon's [`FIRE` / `FLYOUT` / `IMPACT`](weapons.md#fire--flyout--impact-bindings) bindings name:
the effect-reader files (`ON_CALL` `ANIMATION_DEFINITION`s) and the gamez node prototypes each
binding resolves to.

Each weapon in [weapons.md](weapons.md) points at its effects by name — `FIRE` names a muzzle
animation, `FLYOUT` a projectile `MODEL`, `IMPACT` a per-surface hit animation. Those names
resolve to one of two places: an **effect reader** in `extracted/zrdr/` (a named animation
built from `PUFFER_STATE`/`LIGHT_STATE`/`OBJECT_MOTION` events — the event vocabulary is in
[anim-definitions.md](anim-definitions.md), the particle system in [effects.md](effects.md)),
or a **gamez node prototype** (a model root under a chapter's `nodes.json`, instanced at the
firepoint/impact point). This page maps every binding target to its source.

## At a glance

This page is the current reference for its documented format family.

## At a glance

This page is the current reference for its documented format family.


## Contents

- [Muzzle flashes — `muzzle_burst.zrd.json`](#muzzle-flashes-�-muzzleburstzrdjson)
- [Bullet impacts — `gunhit.zrd.json`](#bullet-impacts-�-gunhitzrdjson)
- [Ordnance effects and projectile prototypes](weapon-effects/ordnance.md)
- [Muzzle & tracer textures — the ammo-type axis](#muzzle-tracer-textures-�-the-ammo-type-axis)
- [Binding resolution — 5 unresolved names](#binding-resolution-�-5-unresolved-names)
## Muzzle flashes — `muzzle_burst.zrd.json`

`FIRE`'s `ANIMATION` slot names one of these. All are `ON_CALL`, `EXECUTION_PRIORITY 6`, and
toggle a gamez node of the same name active for one frame:

| Animation | Bound by | What |
|---|---|---|
| `muzzle_burst_slug` / `_dum` / `_ap` / `_mag` | the four ammo types (`wep_X0`–`X3`) | LOD-gated `CALL_ANIMATION muzzleburst_effects AT_NODE` (offset y −0.2), then a random `mb_spinflame` roll (30° / 80° / 140°) |
| `muzzle_burst` | the base guns / turret guns | the fuller flash: ejects `gunshell`, runs the `muzzlepuffer` `PUFFER_STATE` (smoke101–103, 0.05 s interval), and picks 1st- vs 3rd-person muzzle lights (`muzzle_lt`, `bigmuzzle_lt`) by `PLAYER_1ST_PERSON` |
| `muzzle_burst2` | the heavy mounts | a larger flash with `muzzle_lt2` (range up to 16 m) |

The flash animation and the flash *node* share a name; the reader animates the prototype node
listed under [Projectile prototypes](#projectile-prototypes-gamez-roots).

### Engine wiring (M3, C22) — casing, muzzle smoke, muzzle light

`ProjectilePool` renders `muzzle_burst`'s three secondaries per gun shot, each from the def's own
values, none through a shared `gunshell` anchor (whose `RUN_TIME 2` under `CallAnimation`'s
already-live gate would drop every ejection but one per 2 s window):

- **Casing** — a pooled instance of the `gunshell` gamez subtree (the `g1` child carries the
  mesh) per shot, flying the gunshell def's `OBJECT_MOTION` verbatim under `MotionRuntime`'s
  semantics: `TRANSLATION_RANGE` xz `[10,−10]` / y `[−75,−85]` as distances travelled over
  `RUN_TIME 2` (random azimuth, `GRAVITY −3` folded), and `FORWARD_ROTATION TIME 20.94` rad/s
  (1200°/s) as a **rate**, turned about the launch's own horizontal perpendicular — whose length at
  gunshell's −75…−85° of elevation is 0.06–0.17, so the casing turns at 1.3–3.6 rad/s.
- **Muzzle smoke** — the `muzzlepuffer` values (aft 20 m/s in the muzzle frame, ±0.8 random,
  size 0.3–0.6 m, life 0.1–0.2 s, deviation 0.05 m, `smoke101`) as oriented sprites on the
  pool's sprite path; the per-shot count glosses the authored 0.05 s × 0.3 s emission window.
- **Muzzle light** — a pooled `OmniLight3D` per shot using the `3rdperson_lts` 3-way
  `RANDOM_WEIGHT` variants' range/colour verbatim (1–2 / 1.25–3.25 / 2–3.75 m; 0.88–0.93,
  0.78, 0.36); the def deactivates it on the next event tick, rendered as a ~2-frame flash.
  The `PLAYER_1ST_PERSON` `bigmuzzle_lt` branch (range up to 21 m, ±11 offsets) is unbuilt —
  there is no first-person view yet.

The **white puff cluster** the retail captures show riding each ejected casing matches **no
shipped effect def** (only `muzzle_burst` references `gunshell`, and the `gunshell` def carries
no puffer) — the "cluster" in the captures is the muzzlepuffer's own smoke misread as a casing
effect  .
(`BL-261`, A2): the casing now flies bare and the muzzlepuffer plays at its authored 6-puffs/0.3 s
window instead.

## Bullet impacts — `gunhit.zrd.json`

`IMPACT`'s `default` `ANIMATION` for a gun names a `<caliber><ammo>_gunhit` — `3040` (30/40-cal),
`5060` (50/60-cal) or `70`, crossed with `slug` / `dum` / `ap` / `mag`, i.e. the same
caliber×ammo axis as the weapon matrix. Each is one `ON_CALL` def whose `ANIMATION_NAME` is the
bound name (`3040slug_gunhit`, …) and whose body is distance-gated:

- **`PLAYER_RANGE 500`** gates a `PUFFER_STATE`, one per ammo family: `blacksmokepuffer` for
  **slug** (`smoke101`–`103`, black→clear, **`TIME_INTERVAL` 1.1 s** — one puff per hit, not a
  plume), `whitehotpuffer` for **ap**/**dum** (`magnesiumtip`), `firepuffer` + `whitehotpuffer` for
  **mag** (`fire_f01`–`04`). Beyond 500 m the impact is silent visual-wise.
- **`PLAYER_RANGE 200`** adds flung debris via ballistic `OBJECT_MOTION` (`bit1`/`bit2`/`bit3`/
  `chunk`, `RUN_TIME` 1–4 s). Four bodies fly, but only one is visible: `chunk` has one 4-vertex
  quad, while `bit1`–`bit3` carry **0 vertices and 0 polygons** in this install (measured C1/C2)
  and each renders as **one 1-pixel, near-black point**. That matches the original 2026-08-07:
  `OriginalScreenshots/Videos/70 DD Dirt.mp4` shows a 70-slug dirt hit producing only the `chunk`
  and one faint black `blacksmokepuffer` puff.
  ⚠ **They draw a point, not nothing** (decoded 2026-08-15, `BL-313`). Models 24–26 carry
  `lights: 1` with a non-null light array, and both draw functions gate the light block on the light
  **count** alone, not on vertices or polygons — software `FUN_005524d0` at `00552be2`, D3D
  `FUN_00554550` at `005445f1`, the latter ending in `DrawPrimitive(D3DPT_POINTLIST, …)` via
  `FUN_005a5800`. The record decodes to size field 0 (point size 1 px, `size × 0.02 + 1.0`,
  constants set in `FUN_0054d9c0`), colour word `0x020B` unpacked by `FUN_0059e0e0` to R=0 G=16 B=11
  of 255, and one position at the node origin. There is no sprite, billboard or texture-quad
  substitution anywhere in either draw function.
  ⚠ **RETRACTED 2026-08-07 (`BL-313`):** this line used to read *"the debris art is the `bit01`–
  `bit04` textures every chapter archive ships"* — i.e. the zero-vertex nodes were taken as
  pointers to those textures. The binary refutes it outright: no string `bit01`–`bit04` or
  `bit1`–`bit3` exists in `crimson.exe`, neither draw function resolves anything by name, and no
  polygon of any of C1's 2,237 models uses materials 19–22 (the four `bit0N` textures). Their only
  consumer is `zep_skin_fire3`/`zepskinfire_3`, which names `bit01` in its own data ([gamez.md](gamez.md)).
- **`PLAYER_RANGE 1000` + `ANIMATION_LOD HIGH`** randomly flashes a short `gunhit_lt` light.

Puffer definitions are shared with [effects.md](effects.md); the range gates are the reason a
distant hit shows nothing.

⚠ **Only the ap/dum/mag defs stop their own emitter — the three `*slug_gunhit` defs ship no
`ACTIVE_STATE 0` at all.** Measured across all 12 defs (C1 `cam_anim`): ap and dum turn
`whitehotpuffer` off at `EVENT_OFFSET` **+0.1 s**, mag turns both of its puffers off at **+0.3 s**,
and slug — the **stock ammo on every gun weapon** (`wep_00`–`wep_03`, `30`–`70slug`, `MPTUR`,
`TURRET`) — never turns `blacksmokepuffer` off. So "the gunhit smoke has no stop" is true of the
common case and false of three quarters of the family; a reader that generalises from either half
gets the other one wrong. The slug def's only other authored bound is its debris `OBJECT_MOTION`
`RUN_TIME` (1–2 s), which does not gate emission.

⚠ **A gun's `buildings` entry is not a `gunhit`.** Every gun but `wep_02` (50slug) routes
`buildings` → `bld_damage.flt`, which is absent from the install, so a gun round on a building
draws the engine's ricochet stand-in and no smoke. The `gunhit` family is reached through
`default` — i.e. terrain. Verify gun-impact work by strafing **dirt**, not a hangar.

### Engine wiring (M3, D30)

On a projectile impact `ProjectilePool` plays the struck surface's `IMPACT` sound and, for the
effect **animation**, splits by what the bound name resolves to:

- **A gamez model prototype** (the name IS a `nodes.json` root) → the model is instanced at the
  hit point. In practice this is the **water splash**: `splash1.flt` (guns) and
  `bsplsh.flt` (HE) each instance 2 meshes (`splash1_base`/`splash1_splash`, …) — verified on
  C1B/C2B. Since A2 the instance also **plays its authored def** (below) instead of standing
  statically for 0.4 s.
- **A reader/control def** (`3040slug_gunhit`, `he_ground_effect`, `large_fireball`) or an
  **undefined** name (`bld_damage.flt`, …) names no root, so nothing instances and a stand-in
  spark shows. The authored **puffer/particle** half of these (the `blacksmokepuffer` smoke, the
  fireball puffs) does **not** render at runtime in flight: the puffer factory + `TextureArchive`
  are torn down after the world build (`KeepArchivesOpen` is lab-only), so a runtime `PUFFER_STATE`
  builds nothing. Rendering them needs a dedicated world-effects runtime that keeps textures open
  and relocates the effect templates onto the hit point — the same machinery the per-player crash
  runtime already proves (`BuildFlightCrashRuntime`) and that **destruction effects (D32)** share,
  so the impact-puffer wiring folds into D32. The per-class stand-ins these names fall to (A2):
  dirt → the single spark, i.e. no arm of its own (the tumbling chips on the `bit01–04` textures
  were **deleted 2026-08-15, `BL-313`**, see the `PLAYER_RANGE 200` note above; ground still
  resolves to a non-`None` stand-in because the world-effects sink is gated on it); a gun round on a
  buildings-classed surface → a ricochet spark burst + flash (judged by eye — `bld_damage.flt`
  and the `rcochet1` `EFFECT` are both install-missing, see the unresolved-names table).

### Water splash defs — `splash1.zrd.json` / `bsplsh.zrd.json`

The two water-hit models each ship an `ON_CALL` def of the same name with an identical shape:
activate → opacity 0→1 over 0.05 s, hold, then 1→0 over 1.0 s starting at 0.05 + `EVENT_OFFSET`
0.95 = **1.0 s** (`OBJECT_OPACITY_FROM_TO` targets the model ROOT `splash1.flt`/`bsplsh.flt`, i.e.
base disc AND column together, not the column alone); the `*_base` disc scales xz 1→2 over 0.2 s
(`OBJECT_MOTION_FROM_TO`), then 2→1.8 over 1 s from `EVENT_OFFSET` 0.8; the `*_splash` column runs
`OBJECT_MOTION SCALE [1,100,1, 0,-100,0]` over `RUN_TIME 2` — under MotionRuntime's decode
(`scale = initial + delta·u`) the column **starts at ×100 Y and collapses to 0 over 2 s** — plus an
`OBJECT_CYCLE_TEXTURE` reset on the column (the `splash01/02/03` flipbook, 3 frames @4 fps per the
gamez `cycle` block). The models are authored `lighting: false`, `fog: false` (self-lit — the
retail captures show white splashes at night) with full-white vertex colors. Geometry is tiny:
`splash1_splash` is a **5 cm × 1.4 cm** quad (`Facade` SphericalY — camera-Y-billboarded), the
base disc 24 cm across.

**Engine wiring (A2, C6).** `ProjectilePool` drives the scale curves procedurally on each per-hit
instance (`AdvanceSplash`, values verbatim; per-hit instances rather than def playback so 8
rounds/s give concurrent walking splashes) and honours `lighting/fog: false` with unshaded
override materials (the shared world materials multiply mission SUNLIGHT in). C6 (`BL-265`) adds
the fade and the flipbook. The fade drives each instance's own `csky_opacity` through a
per-instance translucent twin installed as a surface override (`EnsureSplashFade`, reusing
`SceneBuilder.FadeShaderFor` — the same derivation `AnimRuntime`'s opacity-twin path uses, applied
locally since these are transient per-hit instances, not persistent world nodes) — never editing
the shared cached material in place, since concurrent splashes reuse it. The flipbook reuses
`TextureCycler`'s frame-swap machinery (`EnsureSplashFlipbook`), registered manually the first time
each splash's built material is seen: it cannot rely on `SceneBuilder`'s automatic per-polygon
registration because the gun splash's own `splash1_splash` polygon binds to a non-cycling sibling
material carrying the identical `splash01.tif` texture (confirmed against C1B's own `materials.json`
— material 136, texture 131, `cycle: null` — vs. the cycling material 135 at the same texture index;
`bsplsh_splash`'s polygon binds directly to 135, so its automatic registration already worked). Once
registered a flipbook runs globally and continuously like every other world cycle, not reset per
hit — concurrent splashes share one synced frame. The column's **width** plays at 8×
(`SplashColumnWidthScale`), with the fades in: the authored
quad is 5 cm wide — sub-pixel past ~30 m — while the reference ticks measure ~0.35 m, which 8×
matches. The authored 1× stays reachable, same `static readonly` pattern as A3's
`MuzzleFlashCount`.

### Engine wiring (M3, D32) — the world-effects runtime

That dedicated runtime is now built (`WorldEffectsFactory.BuildWorldEffectsRuntime`): one per session, a
world-scoped `AnimRuntime` bound to the closure of every impact/destruction effect name, over a
stage of their gamez template roots (`gunhit`, `flame_ball_01`, `he_ring`, …) — each root hidden
until an effect plays on it — with a
live puffer factory (the session textures stay open for the crash runtime already).
`PlayEffectAt(name, worldPoint)` relocates the effect's template root onto the point and starts the
def — its puffers ride that root and parent at world level, so they render. Two callers:

- **Weapon impact** → `ProjectilePool.EffectSink`. The rocket ground/building bursts
  (`large_fireball`, `he_ground_effect`, `ap_ground_effect`, `flak_effect`, …) render their smoke/
  fire at the hit, and since C8 so do gun rounds, under two bounds the ordnance path does not need:
  a **0.3 s instance TTL** (`ProjectilePool.GunEffectTtl`, passed through `PlayEffectAt`) and a
  **0.1 s per-effect-name throttle** (`GunEffectInterval` — one name per firing group). The TTL is
  the family's own longest authored stop, and it is what bounds the slug defs, which ship none; it
  sits below `blacksmokepuffer`'s 1.1 s `TIME_INTERVAL`, so a hit is one puff. The throttle exists
  because the templates are shared and relocated (`BL-225`): two plays inside one emission window
  only move a single emitter.
- **Destructible death** → the world runtime's `ExternalEffect` routes a death sequence's
  `CALL_ANIMATION` of a curated effect (`large_30sec_fire`, `great_balls_of_fire`, …) here.

`--effects-test` is the headless verify: it plays each of the 33 bound names at the camera point and
reports which resolve and which actually **build a puffer** (`verification.md` WORLD-12 — a started
def whose factory/textures are absent renders nothing). Deterministic (seeded, `StopAll` between
names): **30 build a puffer** — including all 12 `*_gunhit` variants, the three `mag` ones with two
each; the remaining 3 (`flash_effect`, `rear_flash_effect`,
`biggun_flying_parts`) are point-light / model-only effects with no particles of their own.
A `PUFFER_STATE` whose `AT_NODE` is `INPUT_NODE`/`MAIN_ROOT_NODE` emits on the effect's own
relocated root (the sentinel = "the node this def was invoked on"; see
[anim-definitions.md](anim-definitions.md)).

**The staged set must be the closure's WHOLE anchor-root set** (D31, `analysis/effect-anchor-roots/`).
A definition anchors on the gamez node its `NAME` names, so a root the stage omits leaves every def
anchored on it unanchored — it plays nothing, silently. Staging only 19 of the 28 roots the rocket
IMPACT closure needs cost the per-type explosion rings below, all four smoke-trail columns
(`ap_trails`/`he_trails`/`flak_trails`/`carnage_trails`), the sonic puff clusters and the torpedo
ripple; `--effects-test` reported it only as `PufferState(no host node)×20`.

### The HE rocket's dirt burst CONTAINS its building burst — they are not alternatives

`BL-019` reported that an HE rocket (`wep_06`/`wep_24` BOOM, the stock rocket on all 11 planes)
gives "identical fireball puffs" on a building and on dirt, on the belief that the original pairs
`large_fireball` (buildings) against a light flash with no puff (dirt). **The data says the
opposite, and the shared fireball is authored, not a lookup collapse.** Measured, in order:

1. The `IMPACT` table **does** carry distinct entries — `buildings` → `ANIMATION large_fireball`,
   `default` → `SURFACE_ANIMATION he_ground_effect` (`--dump-weapons=wep_06`). The `default` entry
   uses the `SURFACE_ANIMATION` slot, not `ANIMATION`; a reader that reads only `ANIMATION` would
   see nothing there, which is why `WeaponDef` keeps both slots.
2. The engine's per-surface lookup **does** differentiate, though not where this measurement
   expected. It selects on the struck material's surface **id**, not on the texture name (corrected
   2026-08-13, see the ⚠ below), and the C1 `g306` hangar's colliding polygons carry `default`(0),
   not `buildings`(11) — so the same shot now logs `-> 0/default … fx=he_ground_effect`, measured.
   The `buildings` row (`large_fireball`) needs a material actually tagged the `buildings` soil
   type, which almost nothing in this install is. The two rows differ; what changed is which
   geometry reaches which row.
3. `he_ground_effect` (`he_control.zrd.json`) `CALL_ANIMATION`s **`large_fireball` itself**, at
   `AT_NODE he_ring, 0, 12, 0` — plus `call_he_ring`, `call_he_ring1`, two `call_hetrails_up`
   columns, a `he_light`/`he_light1` flash pair and a `FBFX_COLOR_FROM_TO` screen flash.

So dirt is a **superset** of buildings: same fireball, plus the ring stack, the trail columns and
the light flashes. Both surfaces are *supposed* to show the same fireball, and it is the dominant
visual — which is exactly what makes them read as "identical" in the air. Making dirt a light flash
would mean deleting the fireball the data calls, i.e. inventing content.

⚠ **The material `soil` field IS what the IMPACT lookup keys on** — corrected **2026-08-13**, and
the reverse of what this page said until then. The `IMPACT` block's names are the game's global
surface registry, the block is an array indexed by surface id, and the struck material's `soil` id
picks the row (`analysis/surface-classification/FINDINGS.md`; the
`weapons.md` `IMPACT` section has the shipped per-id counts). The polygon's **texture name**
(`SceneBuilder.ClassifySurface`) decides nothing about a weapon impact any more. The earlier
"MechWarrior-3 leftover" gloss on the field is not used; the same field
turned out to drive the crash/touchdown choreography vectors; it drives both families.

⚠ **An id no weapon NAMES plays the `default` row** ([weapons.md](weapons.md#an-id-the-weapon-never-names-inherits-the-default-row)).
So a hit on `dirt`(13), `fire`(5), `airstrip`(8) or `dzone`(12) material — 0–10.6 % of a chapter's
collidable area, worst in C2 — draws and sounds the weapon's ordinary ground impact, and an HE
rocket's fireball reaches a struck aircraft the same way, since the rockets name no `player`(6) row
either. An id a weapon does name and leaves empty is the silent case.

⚠ **Material tagged the `buildings` soil type is vanishingly rare, so building hits are
legitimately `default`.** Only C1 carries any measurable `buildings`(11) area at all (273 polygons)
and the other seven chapters carry none — C2's `nycity` towers and C1's `g306` hangar walls are
100 % / dominantly `default`(0). A run that never reaches the `buildings` row is the map's content,
not a bug. To photograph the `default` row's rocket burst on wall geometry, aim at the hangar
(C1 `g306` ≈ `(-4258, 172, -6405)`).

### Explosion rings — the per-type ground decals

Each ordnance type's ground burst carries a **ring mesh**, not a particle: a flat 8.4 m quad the def
activates, scales up and fades out. This is the visual signature that tells the types apart.

| Rocket | `IMPACT` anim | ring def (`NAME` → `ANIMATION_NAME`) | ring node | texture |
|---|---|---|---|---|
| ARMOR (`wep_05`) | `ap_ground_effect` | `ap_effect` → `call_cracks` | `ap_cracks` | `ring_ap` (yellow-green) |
| BOOM (`wep_06`) | `he_ground_effect` | `he_ring` → `call_he_ring`; `he_ring1` → `call_he_ring1` | `he_ringer`; `he_ringer1` | `ring_he` (violet-fringed white) |
| SONIC (`wep_08`) | `sonic_ground_effect` | `sonic_ring1..5` → `ring_up1..4`, `ring_down1` | `sonic_ring` (one per root) | `ring_sonic` (pale cyan) |

Timings are authored and must not be retuned: HE's ground ring scales 1→7 over 1.6 s behind a 0.2 s
opacity fade-in and a 0.6 s fade-out; its upper ring (called `AT_NODE he_ring, 0, 12, 0`) scales
1→20 over 2.0 s. The sonic rings are five staggered copies with authored start scales
(`sonic_ring2/3/4` reset to 0.6/0.7/0.8) so they read as an expanding stack.

⚠ A ring is reset **`INACTIVE`** (HE) or **`ACTIVE` with `OPACITY_STATE OFF`** (AP, sonic) — two
different ways of starting invisible. Anything that decides "does this template show?" must honour
both, and must never assume an idle template is inactive.

⚠ Ring nodes carry `intersect_surface`, so a naively built template gets colliders — and the
authored scale (up to ×20 on a 8.4 m quad) would leave an invisible ~170 m plate at the blast site.
Effect templates are presentation only and are built with collision suppressed.

## Ordnance effects and projectile prototypes

See [ordnance effects and projectile prototypes](weapon-effects/ordnance.md).

## Muzzle & tracer textures — the ammo-type axis

The per-ammo appearance is texture-driven, on the **same four-way axis as the `wep_X0`–`X3`
ammo types**. In each chapter's `texture/`:

- **Muzzle flash:** `{slug,dum,ap,mag}_muzzle1` / `_muzzle2` — a two-frame flipbook per ammo
  (the `1`/`2` frame pair, see [anim-definitions.md](anim-definitions.md)).
- **Tracer:** `tracer_slug` / `tracer_dumdum` / `tracer_armorpierce` / `tracer_magnesium` on the
  crossed streak quads, and `slugtip` / `dumdumtip` / `armourpiercetip` / `magnesiumtip` on the tip
  disc just past the streak's leading end. The generic `tracer1` is bound by no gun prototype (the
  four ammo types cover every gun in the install). Per-prototype model indices and the streak's
  measured geometry are decoded in [`org/tracers.md`](../org/tracers.md).
- **Not tracer textures**, despite sitting in the same texture range: `shell1` / `shell2` are the
  **ejected casing's**, and `atorp` is the aerial torpedo's, neither on the ammo axis. The casing
  pair traces `textures.json` 104/105 → `materials.json` 108/109 (`Textured`) → `models.json`
  model 60 → the `g1` node, whose `parent_indices` names `gunshell` as its only parent, so they are
  the skin of the very casing mesh the C22 ejection wiring instances per shot (see the footnote
  under [Projectile prototypes](#projectile-prototypes-gamez-roots)). The `rabbit_blur` nodes near
  `g1` in the node list are neighbouring ammo prototypes' streak children, not its parents.

### Engine wiring (M3, C24; A3) — flash shape + ammo texture

`ProjectilePool` resolves each weapon's ammo index from its `FIRE` `ANIMATION` binding
(`MuzzleAmmoIndex`: `muzzle_burst_slug`/`_dum`/`_ap`/`_mag` name the type directly; the base
`muzzle_burst`/heavy-mount `muzzle_burst2` carry no suffix and default to slug). The rendered
flash is the **triad** — three `_muzzle1` quads 120° apart sharing one continuous per-shot roll,
matched to the reference stills' 3-lobed burst — **anchored to the firing muzzle node**, riding
the plane as the def's `AT_NODE` placement implies (a world-fixed flash is flown through at
speed). The def authors ONE `mb_spinflame` node with a 3-way `RANDOM_WEIGHT` roll (30°/80°/140°);
what the original engine renders from that — one picked branch or all three at once — is not
recoverable from the data. The pick-one reading (one rolled quad playing the
`_muzzle1`→`_muzzle2` flipbook) was implemented and **rejected at the controls (2026-08-05,
`BL-263`)**: it does not reproduce the stills, and the deviation is recorded here as deliberate.
The `_muzzle2` frame is not played by the flash; the impact stand-in spark keeps reusing it
through its own separate pool.

## Binding resolution — 5 unresolved names

Of the **57** distinct asset names referenced across all 48 weapons' `FIRE`/`FLYOUT`/`IMPACT`
bindings, **52 resolve** to a reader above or a gamez prototype root, and all **23** referenced
sound defs resolve in [sounds.md](sounds.md). **Five names have no locatable definition** — they
appear only as references inside `weapons.json`, with no matching reader animation or gamez node
in any of the 8 chapters:

| Unresolved | Referenced by | Slot | Likely |
|---|---|---|---|
| `bld_damage.flt` | gun `IMPACT` `buildings` | IMPACT | an external `.flt` model, not extracted as a node |
| `rcochet1` | gun `IMPACT` | IMPACT | a ricochet spark effect |
| `call_small_flash` | `FIRE` | FIRE | a small muzzle-flash variant |
| `f18sparks2` | `IMPACT` | IMPACT | an impact-spark effect (dev-named) |
| `flak_effectplayer` | flak `IMPACT` `player` | IMPACT | a player-surface flak variant (cf. `flak_effect`) |

These are **leads for Wave D**, not confirmed content: each is a named binding whose asset was
not found in this install's extraction. **D30 confirmed all five inert for impacts:** none names
a gamez model root, so `ProjectilePool` instances nothing for them and the stand-in spark shows
(no crash) — measured on C4/C5 building hits (`bld_damage.flt`, `large_fireball`).

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
