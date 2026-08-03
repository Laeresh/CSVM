# Weapon lab C6 — click to place, on a real surface

Evidence for `PLAN-weapon-lab` C6 (2026-08-03). The captures are renders of the game's own
geometry, so they are **not committed** — they live in the git-ignored `Screenshots/`
(`c6_water.png`, `c6_buildings.png`, `c6_dirt.png`). Every run below is reproducible as written.

All three are `--weapon-lab=wep_06 --plane=player_bhawk --weapon-fire --frames=400
--log=ui:debug,weapons:debug`, differing only in chapter and click point. The pick line and the
impact line are quoted verbatim; the impact record is `ProjectilePool`'s own breadcrumb, so the
panel's class and the round's behaviour are the same read.

| Click | Pick | Impact |
|---|---|---|
| `--chapter=C3 --weapon-click=700,640` | `name=g28469 body=col_water surface=water at=(-4229.0,0.0,-3496.7) range=1761 standoff=90` | `-> Water … fx=bsplsh.flt snd=snd_bsplash standin=None` |
| `--chapter=C5 --weapon-click=780,280` | `name=g14321 body=col_buildings surface=buildings at=(-10478.4,225.8,-6052.3) range=3165` | `-> Buildings … fx=large_fireball snd=snd_missile_explode standin=Spark` |
| `--chapter=C2 --weapon-click=500,360` | `name=healthy body=col surface=default at=(-6046.4,46.7,-3848.2) range=527` | `-> Default … fx=he_ground_effect standin=DirtDebris`, then `damage: -60 on ramses HP 20→0 DESTROYED` |

The water shot is the one to look at: no stand-in anywhere in the chain (`standin=None` — the
authored `bsplsh.flt` splash model plays), which is only reachable because the lab now shoots a real
chapter's real colliders.

## What the click points were found with

Pixel coordinates are per-chapter and per-camera, so they were read off a
`--weapon-lab --chapter=CX --debug-colliders` capture, where the collider overlay draws water cyan
and buildings orange, and then clicked. Two follow-ups worth keeping:

- **A buildings-classed collider is much rarer than a building.** C2 has 80 buildings colliders
  against 14187 clutter and 1362 world; C5 has 679. Six clicks on C2's orange-outlined warehouse
  complex all returned `body=col surface=default` — the orange box is the *mesh's* bounding box,
  and the polygons under the cursor were in the untagged bucket. This is `BL-204`'s split working
  as designed, not a pick bug: the class belongs to the polygon's texture, not to the object.
- **Adjacent picks legitimately disagree.** C5 `(780,280)` → `col_buildings`/buildings and
  `(800,270)` → `col`/default, ten pixels apart on the same skyline.

## Two things not observed, deliberately recorded

- **The stand-off clamp never fired** in any capture, and by construction it can only fire when the
  requested stand-off *exceeds* the picked range: the camera→hit segment is empty (the hit is the
  ray's FIRST intersection), so any park point inside that segment is already in free air. The guard
  earns its keep only for the far end of the 15–1100 m slider.
- **The buildings capture struck a plain skyline mesh**, which has no HP, so the destructible half
  of the check is the C2 row instead (`ramses`, HP 20→0) — a rocket fired from the lab does damage
  the world, because the session's `DamageSink` is live in this mode.
