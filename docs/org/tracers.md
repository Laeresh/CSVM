# Projectile visuals and tracers, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-10. Every claim below names the function it
came from, or the extracted gamez record it was read from.

Everything here is a description of *behaviour and geometry*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side — the `FIRE`/`FLYOUT`/`IMPACT` bindings, the
prototype-root table, the per-ammo texture axis — is [`formats/weapons.md`](../formats/weapons.md)
and [`formats/weapon-effects.md`](../formats/weapon-effects.md), which name the tracer textures but
say nothing about how they are drawn. Our implementation is `CSVM/src/Flight/Projectile.cs`. This
page is the original's runtime: what the engine does with the `FLYOUT MODEL` a weapon names.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement, the
decode wins and the disagreement is a note (`PLAN-puffer-engine-deltas`'s standing rule on video
evidence, and [`video-measurements-unreliable`](../../backlog.md)'s worked case). Where CSVM
deliberately differs, that is listed at the bottom rather than hidden.

**The headline: there is no tracer code.** No string, symbol or branch in the executable mentions a
tracer. A tracer is a piece of *authored geometry* — a 4.5 m crossed-quad streak inside the round's
`FLYOUT MODEL` — that the weapon module aims once at spawn and thereafter only translates.

## Function map

All in `D:\zipper\gamez\zweapon\` unless noted.

| Address | Role |
|---|---|
| `FUN_005ad630` | ZWEP `BALLISTICS` reader (`zwep_init.c`) — parses every weapon key into the 0x214-byte def, and sets the billboard flag below |
| `FUN_005ae990` | The `FIRE`/`FLYOUT`/`IMPACT` binding-block parser — `MODEL`, `EFFECT`, `ANIMATION`, `MODEL_ANIMATION`, `SURFACE_ANIMATION`, `SOUND` |
| `FUN_005aeca0` | Projectile pool alloc — attaches the flyout model to the round's node |
| `FUN_005aef40` | Fire/spawn — places the round and applies its **one and only** orientation |
| `FUN_005af900` | Per-frame projectile pass over the live list |
| `FUN_005b0770` | Per-frame visual update of one live round — position, and conditional re-orientation |
| `FUN_005aed40` | Despawn — detaches the model, resets the node's scale/rotation/position |
| `FUN_005ad290` | Hands out a per-round *clone* of the flyout model (only for `MODEL_ANIMATION` carriers) |
| `FUN_005b0cb0` | The `INSTANT`/`MULTI_TARGET` beam renderer — **dead in this install**, see below |
| `FUN_004d8eb0` / `LAB_004d8e90` | `zclass/Object3d.c`: subtree predicate + the "is a plain camera-facing facade" test |
| `FUN_004cd610` | `zclass/Class.c` add-child — the multi-parent attach that makes model sharing work |
| `FUN_004d1a30` / `FUN_004d1d50` / `FUN_004d18d0` | Object3d set rotation / position / scale |

Weapon-def offsets used below: `+0x120` = `FLYOUT`'s `MODEL` node, `+0x110` = `FLYOUT`'s
`MODEL_ANIMATION`, `+0x54` = `GRAVITY`, `+0x74` = the flag word.

## The billboard flag — bit 9 of the def flag word

At parse time (`FUN_005ad630`, in the `FLYOUT`/`PROJECTILE_BBOX` block) the reader walks the whole
flyout-model subtree with `FUN_004d8eb0` and a predicate at `LAB_004d8e90`. The predicate reads the
node's `+0x3c` block and returns true for `type == 1 && [+4] == 0` — a plain camera-facing facade
with no reference node. The result is **inverted** into bit 9:

> **bit 9 (`0x200`) = "this flyout model contains no billboard".**

That is the whole decision. A model that faces the camera by itself is never rotated by the weapon
code; a model made of fixed geometry is aimed along the shot. Nothing else consults it.

## Spawn — the orientation is applied exactly once

`FUN_005aef40`, after the muzzle effects and before the node is made live:

- if `!(flags & 0x4000)` **and** (`(flags & 0x200)` with a flyout model, or a fire effect with no
  flyout model), the round's node is rotated to `yaw = atan2(-dir.x, -dir.z)`, pitch from the same
  direction vector — i.e. **aimed along the firing vector**;
- then `FUN_004d1d50` places the node at the muzzle point.

The negated `atan2` arguments are the engine stating that a model's local forward is **−Z**, which
is the same convention the prototype geometry is authored in.

No roll is applied. `FIXED_ROTATE` (bit 8) exists to suppress the per-frame update below, not this.

## Attach — one model node, many rounds

`FUN_005aeca0` pops a projectile from the free list and then:

- **no `MODEL_ANIMATION` on `FLYOUT`** (every gun) — the *prototype node itself* is attached under
  the pooled projectile node via `FUN_004d1390` → `FUN_004cd610`. No copy is made.
- **`MODEL_ANIMATION` present** (the rockets, for their trail defs) — `FUN_005ad290` pops a
  pre-made clone from a per-weapon free list at def `+0x160`, or deep-clones the prototype
  (`FUN_004d85a0`) when that list is empty. Each round then owns its subtree, which is what lets a
  per-round def animate it.

⚠ **The no-copy path is not a bug, and reading it as one gives "only the newest round is visible".**
`FUN_004cd610` pushes the new parent onto the *child's* parent array (`+0x54` count, `+0x58` array)
as well as the child onto the parent's, and calls `FUN_004cf7c0` as soon as a node has more than one
parent. The scene graph is a **DAG**: one shared model node is drawn once per parent path, at each
parent's transform. Every live gun round renders the same `slug.flt` at its own position,
allocating nothing. `PROJECTILE_BBOX` (def `+0x78` bit 0) is applied to the pooled node here, not to
the shared model.

`FUN_005aed40` unwinds it symmetrically on despawn — detach every parent, detach every child, then
reset the node's scale to 1,1,1 and its rotation and position to zero, because the node goes back to
a pool that other weapons will reuse.

## Per frame — a position write, and nothing else

`FUN_005af900` walks the live list and calls `FUN_005b0770` for each round in state 1. That
function re-orients the node from the current velocity **only if**

```
GRAVITY (+0x54) != 0   and   !(flags & 0x100 /* FIXED_ROTATE */)   and   !(flags & 0x4000)
```

Every weapon in this install ships `GRAVITY 0.0` ([`formats/weapons.md`](../formats/weapons.md),
Ballistics: 5 carriers, all zero), so **the branch never runs**: a round's orientation is frozen at
the spawn value for its whole flight, and the per-frame cost is one `FUN_004d1d50` position write.
The re-orientation exists for ballistic drop, which this install never authors.

The `0x4000` branch is the exception that proves it: those rounds are spun about Y at
`3.4906585 rad/s` (200°/s) and scaled `1 → 5` over their first second (`s = 1 + 4t`). Nothing scales
or fades an ordinary round — no per-frame opacity, no distance scaling, no billboarding.

## The dead beam renderer

`FUN_005b0cb0` is a second, much larger per-frame pass over the same live list that renders a shot
as a **chain of stretched segments**: each segment's node is placed at the segment start, aimed
along the ray, and Z-scaled to the segment's length (`FUN_004d18d0(node, 1, 1, len)`), with an
opacity pulse `lerp(min, max, (sin(2π·f·t)+1)/2)` and a per-segment raycast for bounces; a second
branch forks it into randomly-jittered sub-segments at 0.4× each. It is gated on `INSTANT` (bit 11)
and `MULTI_TARGET` (bit 17).

**No weapon in this install sets either flag** — neither key appears anywhere in the 48 weapon
entries. This is MechWarrior-3 laser/PPC code carried into the engine and never reached. Anyone
hunting "how does the engine draw a streak?" will find this function first; it is the wrong answer.

## The authored tracer — `slug.flt` and its three siblings

Read from `extracted/C1/gamez/{nodes,models,materials,textures}.json`. All four ammo prototypes are
structurally identical:

```
slug.flt              root, model_index -1, bbox_child, child_bbox z −4.71 … 0
├─ g11                model 48 — 0 vertices (placeholder)
└─ l5     LOD, range 0 … 600 m
   ├─ g10             model 49 — 8 v / ONE 8-gon poly: a regular octagon disc of
   │                  radius 0.1445 m lying in the XY plane (z = 0), i.e. facing
   │                  ALONG the flight axis, with radial UVs; node transform
   │                  translates it to z −4.5647      → texture slugtip.tif
   └─ rabbit_blur     model 50 — 8 v / 2 polys, both spanning z −4.5 … 0:
                      poly 0 in the y = 0 plane, poly 1 in the x = 0 plane, each
                      0.2 m wide — two PERPENDICULAR quads crossed along the flight
                      axis. show_backface, vertex colours full white, U runs 0 at
                      the front to 1 at the tail   → texture tracer_slug.tif
```

| Prototype | streak model | streak texture | tip texture |
|---|---|---|---|
| `slug.flt` | 50 | `tracer_slug.tif` | `slugtip.tif` |
| `dumdum.flt` | 53 | `tracer_dumdum.tif` | `dumdumtip.tif` |
| `armorpiercing.flt` | 56 | `tracer_armorpierce.tif` | `armourpiercetip.tif` |
| `magnesium.flt` | 59 | `tracer_magnesium.tif` | `magnesiumtip.tif` |

So a tracer is **two perpendicular quads, 0.2 m wide and 4.5 m long**, plus a 0.29 m octagonal disc
just past the leading end. Four things follow, and each is checkable:

- **The crossed pair is why no billboarding code is needed.** Two perpendicular quads read as a
  solid streak from any viewing angle, which is exactly why the subtree contains no facade node and
  therefore why bit 9 is set and the round *is* aimed along the shot.
- **The node position is the streak's TAIL.** Geometry runs from `z = 0` forward to `z = −4.5`
  (−Z is forward), and the tip quad sits at `z = −4.5647`, just past the leading end. The round's
  tracked point trails 4.5 m behind the bright tip the player sees.
- **The bright head is a head-on-only disc.** `g10` is perpendicular to the flight axis, so it
  presents its full 0.29 m face when the round flies toward or away from the viewer and goes
  edge-on — effectively invisible — from the side. A side-on tracer is the crossed streak *alone*;
  the "glowing head" reading only holds for rounds coming at you.
- **The `l5` LOD cuts off at 600 m.** Past that range both the streak and the tip stop drawing and
  all that remains is the zero-vertex `g11` — a round beyond 600 m is **invisible while still live
  and lethal** out to `RANGE` (900–10000 m). The disappearance is authored, not a draw-distance
  artefact.

`tracer1.tif` — the generic fifth texture — is bound by no gun prototype; the four ammo types cover
every gun in the install.

**Rockets carry no streak at all.** `he_rocket` and `ap_rocket` are LOD-wrapped missile *bodies*
(35 v / 8 polys, ~1.5–1.7 m long, on `missile_he`/`missile_ap` + `missil02`/`04`/`05`) with no
`rabbit_blur` sibling and no tip disc. An ordnance round's visible trail is its `MODEL_ANIMATION`
puffer smoke ([`formats/weapon-effects.md`](../formats/weapon-effects.md), "FLYOUT
`MODEL_ANIMATION`"), not a tracer quad. Anything drawing a streak behind a rocket is inventing it.

## Where CSVM differs

`CSVM/src/Flight/Projectile.cs` does not instance the prototype at all: it draws a hand-tuned sprite
per round. The decode settles the numbers that tuning was standing in for.

| | Original (measured) | CSVM (`RenderTracers`) | Note |
|---|---|---|---|
| Streak length | **4.5 m** | `TracerLength` 1.0 m | 4.5× short |
| Streak width | **0.2 m** | `TracerWidth` 0.10 m | 2× narrow |
| Where it sits | tail AT the round's position, geometry runs **forward**; head 4.56 m ahead | quad centred half a length **behind** the position | the drawn streak is on the wrong side of the simulated point — a ~5 m offset in the direction of travel |
| Geometry | **two perpendicular** quads, `show_backface`, fixed | **one** quad, rolled about the velocity axis to face the camera every frame | the original needs no camera at all; ours degenerates when a round flies at the eye |
| Leading head | a **second** mesh: 0.29 m octagonal disc on its own `*tip` texture, perpendicular to flight (head-on only) | none | ours has no head-on element at all |
| Colour | white vertex colours, texture unmodified | `TracerBrightness` ×3 overbright, additive | ours compensates for the missing tip disc and no bloom |
| Growth | full 4.5 m from the spawn frame; nothing ever scales a gun round | length capped by distance travelled, so it grows out of the muzzle | ours is an invention; harmless at 8 shots/s but not the original |
| Range cutoff | LOD **600 m**, then nothing draws | `TracerMinPixels` 2.0 floor *inflates* distant rounds | ⚠ direct conflict — ours deliberately shows what the original hides |
| Who the floor is for | n/a — the LOD is per-viewer, because each pane draws its own | one shared world mesh, floored for the **nearest** bound viewer | the original's per-camera LOD has no equivalent for a shared mesh; sizing for P1 alone was the splitscreen bug |
| Rockets | prototype body only, no streak; the trail is `MODEL_ANIMATION` puffer smoke | a `tracer1` streak at `RocketExhaustScale` 0.5 / `RocketStreakScale` 2.4 | ours invents a streak the data has no counterpart for |
| Per-frame work | one position write per round | per-round basis rebuild against the listener camera | |

None of these are changed by this page. `TracerMinPixels` in particular was introduced from the
reference captures showing distant fire as visible streaks; the 600 m LOD says the original stops
drawing them, so that reading needs re-checking against a shot fired at a *known* range before
either side is called wrong.
