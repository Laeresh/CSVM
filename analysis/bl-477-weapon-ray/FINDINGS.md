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

What killed the tanks in the original was a rocket's splash, not a round through the truss: the
rocket struck the hull underside above them, and from there the tanks are in the open. Our splash
occlusion agrees, so nothing about the report is a divergence in either half.

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

5. **Nothing stands between the hull underside and the tank tops.** A ray straight up from each
   tank's top centre meets `g482`, the belly plate under `underneath` (`cargoskin2`, alpha None),
   at y = -48.9 to -52.5 in `cargozep1`-local coordinates, against tank tops at y = -61, and
   meets no polygon in between. The front truss `g469` spans y = -81.0 to -28.3 but its cards
   stand out to x = +/-48.9 as a lateral screen around the tanks, so no vertical ray over a tank
   crosses one. That is why a rocket that strikes the belly plate above the tanks has line of
   sight down to them: it is above the screen, not through a hole in it.

## The engine half

The `alpha-cutout-ray-census` in-engine suite casts 180 rays at `hydrogentank1`'s mesh centre from
36 azimuths at five elevations, over a built C3/M01 with the zeppelins at their authored pose. 5 of
180 aspects reach the tank, all at level elevation on the aft quarter; `g469` takes 26 of the 36
level azimuths, the hull's gasbag panels everything above 30° and the terrain everything below −30°.
So the reported fore/aft asymmetry is real and its occluders are named; it is simply not a
divergence from the original.

The same suite's splash half places the burst by measurement rather than assumption: it casts up
off `hydrogentank1`'s top, strikes `g482` 14.5 m above the tank's centre, and runs
`ProjectilePool.BlastCoverBetween`, the production cover ray, from that surface down to each of
`hydrogentank1..4`. All four are clear. Its able-to-fail control, the same ray from 120 m abeam at
tank height with no struck surface and so no `CoverRayLift`, is stopped by `g469`. Through the
production gather at the HE rocket's `IMPACT_PROXIMITY` of 15 m, the burst reaches `hydrogentank1`
(8.5 m to its nearest surface) uncovered, while `g469` and `panelrightb2` are covered by `g482`
itself, which is the lift working as intended: the plate the round struck shields what is behind it
and is transparent to its own side. The other three tanks sit 33 to 56 m from that burst and so are
outside one rocket's radius, which is a radius result and not an occlusion one.

⚠ The suite hit the instrument trap now recorded as `INSTR-24`: a raycast taken in the same call
that moved a static body reads the collider at its pre-move pose, and the first run reported 108
clean misses through 140 live collider bodies.
