# Shore water: what the surf ring and the coast are made of

The measurements `docs/PLAN-shore-water.md` rests on. Both scripts read unzbd's gamez output
(`extracted/<chapter>/gamez.zip`, unpacked to a scratch folder) and print their figures; no game
data is stored here.

```
python -I analysis/shore-water/coast_detail.py <unpacked gamez dir>
python -I analysis/shore-water/seabed.py <unpacked gamez dir>
```

Node transforms: a node whose `transform` is `"Initial"` is in world coordinates; a
`RotateTranslateScale` node is placed through its Euler rotation, translation and scale. Every
node carrying `srf*` or `wtr*` polygons is `Initial` in both chapters (150 nodes in C1B).

## The surf ring

| | C1B | C2 |
|---|---|---|
| Material | 198, `srf0001.tif`, a 16-frame cycle at 9 fps | 225, `srf0001.tif`, no cycle |
| Polygons | 375 (373 triangles, 2 quads), priority 1 | 86 |
| y | 0.000 at every vertex | 0.000 |
| Width, u = 0 edge to u = 1 edge | min 12.7, p10 31.5, median 43.8, p90 66.3, max 91.5 m | min 5.9, p10 7.6, median 10.0, p90 14.9, max 20.8 m |
| u = 0 vertices to the nearest land vertex within 1 m of y = 0 | median 0.0 m, 90th percentile 0.0 m | same construction |
| u = 1 vertices to the nearest such land vertex | median 27.8 m, 90th percentile 49.6 m | median 7.3 m, 90th percentile 12.6 m |
| UV range | u 0..1, v 0..3.91 | same |
| Vertex colours (r = g = b) | 255 on 576 vertices, 97 on 506, 94 on 19, 178 on 18; both values appear at u = 0 and at u = 1 alike | 255 and 97, both sides alike |

So the ring's inner edge is the land edge, U runs across the band, and the band's fade is the
texture's alpha, not the vertex colour.

## The textures

| Texture | Size | Alpha |
|---|---|---|
| `srf0001`..`srf0016` (C1B), `srf0001` (C2) | 32x64 RGBA | a gradient along U: opaque at u = 0, clear at u = 1 (9 % of texels under 16, 12 % at 240 or above) |
| `wtr00000`..`wtr00015` | 64x64 RGB | none |
| `water1` (C1) | 64x64 RGB | none |
| `water1_trans1/2` (C1) | 256x64 RGB | none; a colour gradient from the water's blue to the land's green, so the coast is painted inside the opaque tile |

## Below the waterline

Of the 557 ring vertices on the seaward edge in C1B, every one has land geometry somewhere below
it in XZ, and in every case that geometry is node `g1165` at y = -11,680 m, the world floor.
Nothing else lies below y = 0 under the ring's seaward edge, and the lowest land vertex within 6 m
of a ring inner vertex is at y = 0.00. A wave trough at the coast therefore shows the floor, which
is why the plan holds the swell at zero inside the surf zone.

## The sea sheet

C1B: 695 `wtr00000` polygons at y = 0, median 512 m across, priority 0. The engine's mask bake
over C1B (`[world] ocean: built grid`): 1536x1536 texels of 8 m from (-12288, -12288), base_tris
1360, edge_tris 377, solid_tris 20911, about 1.2 s.

The land-slope figures `coast_detail.py` prints are not used: a land triangle touching sea level
can reach any depth elsewhere, so the slope distribution mixes the coast with the floor.
