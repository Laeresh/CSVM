# BL-477: alpha-cutout geometry and the weapon ray

The question was whether the original's weapon-ray polygon test consults a hit texel's alpha, which
would make our fully solid alpha-cutout cards a divergence. It does not, and no collision rule
follows. The decode is [`docs/org/weaponRay.md`](../../docs/org/weaponRay.md); this directory holds
the shipped-data evidence beside it, produced by `census.py` (read-only, prints, writes nothing;
run from the repo root as `python analysis/bl-477-weapon-ray/census.py [extracted-dir]`).

## The answer

`crimson.exe`'s ray-vs-polygon test reads no texture data at any point, neither a hit UV's alpha nor
any bit of the texture header word `BL-335` located. It is geometry against the polygon's vertices,
gated only on the per-node `ACTIVE` and `INTERSECT_SURFACE` flags. So the original polygon-tests the
cargo zeppelin's see-through truss exactly as we do, and `docs/formats/gamez.md`'s literal reading of
`intersect_surface` is correct.

## What the shipped data shows

1. **The material `flag` bit is the decal-receiving mark, not a transparency mark.** It selects the
   ray routine that also interpolates the hit UV, and gates the bullet-hole stamper that blits a
   decal at that UV. 21 of C3's 483 materials carry it and every one is an aircraft or cockpit skin
   (`bal_fuslage`, `bldhwk_cowling`, `cphead`, `cockpit10`, `damage1`, `gun_barrel`). No world
   material does, so on a zeppelin no hit UV is ever computed.

2. **The truss is alpha, and so is the tank's own destroyed variant.** The front truss's high level
   `g469` is 57 polygons, 39 of them `cgcable1` (alpha Full); its low level `g503` is 10, 6 of them
   `cargotex1` (Full). The tank's `dbase` is 5 polygons of `hydrotank4`, Full throughout. A blanket
   "skip every alpha polygon" collision rule would therefore delete a destructible's own hit volume,
   quite apart from diverging from the original.

3. **Backface does not explain the asymmetry.** The original's test honours `show_backface`, so a
   polygon without it is solid from its front only, where our colliders are two-sided. But 33 of
   `g469`'s 39 `cgcable1` cards set it, so the truss is double-sided there as well.

4. **The LOD bands say our static level choice is right at gun range and wrong past it.** `f_hi`
   covers [0, 800), `f_mid` [800, 1400), `f_lo` [1400, inf), with the rear truss the same on a 770 m
   first break. `SceneBuilder` keeps the `range.min == 0` level and collides it at every distance, so
   inside 800 m we present exactly the level the original intersects; beyond it the original narrows
   to ±28.3 m of local x while we stay on `f_hi`'s ±48.9 m. That gap is one of the two open
   candidates for why the tanks read differently at the controls.

## The engine half

The `alpha-cutout-ray-census` in-engine suite casts 180 rays at `hydrogentank1`'s mesh centre from
36 azimuths at five elevations, over a built C3/M01 with the zeppelins at their authored pose. 5 of
180 aspects reach the tank, all at level elevation on the aft quarter; `g469` takes 26 of the 36
level azimuths, the hull's gasbag panels everything above 30° and the terrain everything below −30°.
So the reported fore/aft asymmetry is real and its occluders are named; it is simply not a
divergence from the original.

⚠ The suite hit the instrument trap now recorded as `INSTR-24`: a raycast taken in the same call
that moved a static body reads the collider at its pre-move pose, and the first run reported 108
clean misses through 140 live collider bodies.
