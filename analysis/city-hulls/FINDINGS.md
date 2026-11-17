# C5 building hull census

**Question.** Which building hulls does C5's Manhattan place, at what size, with which textures and
colours, and how often; whether a `b`, `c` or `d` substitute differs from its `a` model in shape or
only in texture; how a `det` half combines with its building; and what the clutter collision note's
"about 2,300 distinct" figure counts. Asked by `docs/PLAN-city-buildings.md` item A1.

**Instrument.** `census.py` reuses `../bl-305-clutter-uv/eligibility.py`'s loader and template
resolver over `extracted/C5/gamez` and `extracted/C5/zrdr/templates.zrd.json`. A decoration's
geometry is every mesh in its node chain, in the decoration's frame, nearest LOD only, as
`Clutter.ExtraMeshes` collects it. Two models match in shape when every vertex agrees to 5 cm.
Colour is each texture's mean RGB weighted by the surface area that texture covers on the model;
L is Rec. 601 luma (0 to 255). With `--log`, the per-kind placed counts come from the engine's
`[world] clutter:` line (`CSVM/src/Mech3/WorldSession.cs:208-211`). `render.py` draws contact
sheets of every model and texture with a numpy rasteriser; its output holds original pixels and
stays out of the repo.

## Verdict

- **Per tile the templates place 28, 24, 27, 17, 23, 25 and 25 building decorations**
  (`cblock1` to `cblock7`). A `det` half stands at its building's origin as a separate
  decoration, so the tiles hold **16, 16, 17, 17, 17, 19 and 25 buildings**.
- **C5 has 71 building models with geometry, in 61 distinct shapes**: 45 building shapes in 32
  distinct boxes (W x D x H to 0.5 m), and 16 `det` shapes. `cb05det01` and `cb16det01` have
  blocks and no gamez model; `cb06det03` has a model and no block; nothing reaches `cb05det02`.
- **`cblock7` draws 7 models in 6 shapes and 4 boxes**: `cb12a` (20 x 16 x 14) and `cb12b`
  (20 x 16 x 22) differ in height; `cb13a` and `cb13b` are one shape (16 x 20 x 26); `cb14a`,
  `cb14b` and `cb14c` share one box (16 x 12 x 17) with different roofs.
- **A substitute is its source's shape in 10 of 39 pairs** (`cb00b`, `cb01b`, `cb06b`, `cb06c`,
  `cb08b`, `cb09b`, `cb11b`, `cb11c`, `cb13b`, `cb21b`); every other pair differs in geometry,
  and every substitute differs from its source in UVs or textures.
- **A `det` half is exterior dressing, not volume**: rails, wires, fire escapes, scaffolding and
  roof boxes, 0 to 3 m outside the building's box and up to 3.7 m above it, fading at 75 to 250 m
  against the building's 200 to 900 m, on its own independent roll. `cb03det02` and `cb08det02`
  are 1 m stubs, so half the `cb03` and `cb08` stamps carry no visible detail.
- **The further meshes are part of their own building**: `o2` walls (`cb15`, `cb16`, `cb24`), a
  `g8` cornice ring (`cb15a`, `cb24a`) or ground-floor awning at 2 to 4 m reaching 1.6 m past the
  walls (`cb12a`, `cb13a`, `cb21a`), and `cb04a`'s clock setback (20 to 69 m). 9 placed models
  carry them (12 template decorations, 15 mesh nodes), and their `b` substitutes carry their own.
- **Placed (engine log): 66,763 3D decorations, of which 54,194 buildings and 12,569 `det`
  halves, and 110,528 sprites.** They hold 2,383,387 triangles, 464,968 in `det` halves; the
  `cb12`, `cb13` and `cb14` models are 32,907 stamps and 976,848 triangles.
- **The "about 2,300" in `docs/formats/clutter.md:189-204` counts triangles**: the 57 template
  kinds' first meshes hold 2,240. `Clutter.BuildSolidCollision` (`Clutter.cs:1894-1946`) keys one
  shape per mesh index over every kind's parts, 57 template kinds and 39 minted substitution
  targets, giving **123 shapes and 4,224 triangles**, reproduced exactly here and in the engine log.

## Hulls

One row per shape class. `cbN xK` is K placements per tile, `(roll)` a substitute target. Fade is
the source block's, since a substitute fades like its source (`docs/formats/templates.md`).
Placed is the engine log's count per model, `+` between models of one class.

| Hull | Models | W x D x H (m) | Tris | Textures | RGB, L | Templates (per tile) | Fade near, far (m) | Placed |
|---|---|---|---|---|---|---|---|---|
| cb00a | cb00a = cb00b | 40.0 x 24.0 x 16.0 | 14 | roof01 smbldg1 smbldg2 | 49,50,49 L50; 43,44,36 L43 | cb1 x4 | 200-300, 300-350 | 1606 + 1515 |
| cb00det01 | cb00det01 | 34.8 x 28.3 x 16.0 | 28 | genbldg3 nosegun5 scafolding1 | 77,75,74 L75 | cb1 x4 | 100-150, 150-200 | 1050 |
| cb00det02 | cb00det02 | 36.8 x 28.3 x 17.7 | 48 | genbldg3 nosegun5 oildock1 roof01 scafolding1 | 69,68,68 L68 | cb1 (roll) | 100-150, 150-200 | 1077 |
| cb00det03 | cb00det03 | 44.0 x 28.0 x 17.7 | 38 | aluminum_border2 nosegun5 oildock1 roof01 scafolding1 | 60,61,60 L61 | cb1 (roll) | 100-150, 150-200 | 994 |
| cb01a | cb01a = cb01b | 32.0 x 32.0 x 24.0 | 16 | roof01 smbldg1 smbldg2 | 51,52,51 L52; 43,44,35 L43 | cb1 x3 | 200-300, 300-350 | 601 + 607 |
| cb01c | cb01c | 32.0 x 32.0 x 24.0 | 16 | roof01 smbldg3 | 44,41,37 L41 | cb1 (roll) | 200-300, 300-350 | 577 |
| cb01d | cb01d | 32.0 x 32.0 x 24.0 | 22 | roof01 smbldg4 | 46,38,29 L39 | cb1 (roll) | 200-300, 300-350 | 565 |
| cb01det01 | cb01det01 | 36.0 x 36.0 x 25.7 | 46 | aluminum_border2 nosegun5 oildock1 roof01 scafolding1 | 61,62,61 L62 | cb1 x3 | 75-100, 100-150 | 1171 |
| cb01det02 | cb01det02 | 28.8 x 36.3 x 25.7 | 48 | genbldg3 nosegun5 oildock1 roof01 scafolding1 | 67,68,67 L68 | cb1 (roll) | 75-100, 100-150 | 1179 |
| cb02a | cb02a | 31.8 x 31.8 x 65.2 | 50 | bldg3 roof01 | 50,51,55 L51 | cb1 x3 | 700-800, 800-900 | 1166 |
| cb02b | cb02b | 32.0 x 32.0 x 64.0 | 54 | bldg4 roof01 | 65,62,54 L62 | cb1 (roll) | 700-800, 800-900 | 1185 |
| cb02det01 | cb02det01 | 23.3 x 36.1 x 63.2 | 36 | genbldg3 oildock1 roof01 | 77,76,76 L76 | cb1 x3 | 100-150, 125-200 | 1144 |
| cb02det02 | cb02det02 | 27.6 x 24.6 x 24.2 | 28 | genbldg3 oildock1 roof01 scafolding1 | 60,62,62 L61 | cb1 (roll) | 100-150, 125-200 | 1207 |
| cb03a | cb03a | 32.0 x 32.0 x 64.0 | 107 | bldg1 roof01 | 72,65,57 L66 | cb1 x2 | 700-800, 800-900 | 770 |
| cb03b | cb03b | 32.0 x 32.0 x 64.0 | 112 | bldg2 roof01 | 43,44,37 L43 | cb1 (roll) | 700-800, 800-900 | 796 |
| cb03det01 | cb03det01 | 36.1 x 21.1 x 62.6 | 26 | genbldg3 oildock1 | 91,88,87 L89 | cb1 x2 | 100-150, 125-200 | 819 |
| cb03det02 | cb03det02 | 1.1 x 0.9 x 1.0 | 10 | genbldg3 oildock1 | 81,82,83 L82 | cb1 (roll) | 100-150, 125-200 | 747 |
| cb04a | cb04a | 32.0 x 32.0 x 80.0 | 72 | aphagar05 bldg2 clock gyrobuild_motif3 roof01 | 43,44,37 L43 | cb1 x2 | 700-800, 800-900 | 805 |
| cb04b | cb04b | 32.0 x 32.0 x 80.0 | 50 | bldg3 roof01 roof02 | 54,52,54 L53 | cb1 (roll) | 700-800, 800-900 | 766 |
| cb05a | cb05a | 23.7 x 23.7 x 58.9 | 19 | bldg4 genbldg3 zterminal02 | 68,68,60 L67 | cb1 x2 | 500-600, 600-700 | 794 |
| cb05b | cb05b | 24.0 x 24.0 x 92.0 | 31 | bldg1 roof01 | 73,65,57 L66 | cb1 (roll) | 500-600, 600-700 | 776 |
| cb06a | cb06a = cb06b = cb06c | 45.3 x 45.3 x 16.0 | 14 | roof01 smbldg1 smbldg2 smbldg5 | 48,41,35 L42; 49,50,49 L50; 43,44,36 L43 | cb2 x3, cb3 x4, cb5 x3, cb6 x3 | 200-300, 300-350 | 472 + 468 + 431 |
| cb06det01 | cb06det01 | 50.9 x 50.9 x 17.7 | 38 | aluminum_border2 nosegun5 oildock1 roof01 scafolding1 | 60,61,60 L61 | cb2 x3, cb3 x4, cb5 x3, cb6 x3 | 100-150, 200-200 | 463 |
| cb06det02 | cb06det02 | 43.0 x 43.0 x 17.7 | 48 | genbldg3 nosegun5 oildock1 roof01 scafolding1 | 69,68,68 L68 | cb2, cb3, cb5, cb6 (roll) | 100-150, 200-200 | 481 |
| cb06det03 | cb06det03 | 43.0 x 43.0 x 16.0 | 28 | genbldg3 nosegun5 scafolding1 | 77,75,74 L75 | cb2, cb3, cb5, cb6 (roll) | 100-150, 200-200 | 427 |
| cb07a | cb07a | 45.3 x 45.3 x 80.0 | 50 | bldg1 roof01 | 71,64,56 L65 | cb2 x3, cb3 x3 | 700-800, 800-900 | 341 |
| cb07b | cb07b | 45.3 x 45.3 x 73.6 | 98 | aluminum_border2 bldg4 roof01 | 65,62,54 L62 | cb2, cb3 (roll) | 700-800, 800-900 | 317 |
| cb08a | cb08a = cb08b | 45.3 x 45.3 x 64.0 | 112 | bldg1 bldg2 roof01 | 71,64,56 L65; 43,44,37 L43 | cb2 x2, cb3 x3 | 700-800, 800-900 | 232 + 276 |
| cb08det01 | cb08det01 | 40.5 x 40.5 x 62.6 | 26 | genbldg3 oildock1 | 91,88,87 L89 | cb2 x2, cb3 x3 | 100-150, 200-200 | 242 |
| cb08det02 | cb08det02 | 1.4 x 1.4 x 1.0 | 10 | genbldg3 oildock1 | 81,82,83 L82 | cb2, cb3 (roll) | 100-150, 200-200 | 266 |
| cb09a | cb09a = cb09b | 45.3 x 45.3 x 108.0 | 54 | bldg1 bldg2 roof01 | 42,44,37 L43; 70,63,55 L64 | cb2 x2, cb3 x1 | 700-800, 800-900 | 183 + 181 |
| cb10a | cb10a | 45.3 x 45.3 x 108.0 | 54 | bldg3 roof01 | 50,51,56 L51 | cb2 x2, cb3 x2 | 700-800, 800-900 | 218 |
| cb10b | cb10b | 45.3 x 45.3 x 108.0 | 50 | bldg3 roof01 | 50,51,56 L51 | cb2, cb3 (roll) | 700-800, 800-900 | 221 |
| cb11a | cb11a = cb11b = cb11c | 45.3 x 45.3 x 32.0 | 16 | roof01 smbldg1 smbldg2 smbldg3 | 51,52,51 L52; 43,45,35 L43; 44,41,36 L41 | cb2 x4, cb3 x4, cb5 x3, cb6 x4 | 200-300, 300-400 | 512 + 533 + 568 |
| cb11det01 | cb11det01 | 50.9 x 50.9 x 33.7 | 54 | aluminum_border2 nosegun5 oildock1 roof01 scafolding1 | 59,60,59 L60 | cb2 x3, cb3 x3, cb5 x3, cb6 x3 | 100-150, 200-250 | 664 |
| cb11det02 | cb11det02 | 42.3 x 42.3 x 33.7 | 54 | genbldg3 nosegun5 oildock1 roof01 scafolding1 | 65,65,64 L65 | cb2, cb3, cb5, cb6 (roll) | 100-150, 200-250 | 638 |
| cb12a | cb12a | 20.0 x 16.0 x 14.0 | 36 | buildingtrim02 roof01 roof02 smbldg3 | 59,51,45 L53 | cb4 x2, cb7 x10 | 200-300, 300-350 | 6065 |
| cb12b | cb12b | 20.0 x 16.0 x 22.0 | 48 | aphagar05 buildingtrim01 roof01 roof02 smbldg4 | 60,47,36 L50 | cb4, cb7 (roll) | 200-300, 300-350 | 6201 |
| cb13a | cb13a = cb13b | 16.0 x 20.0 x 26.0 | 36 | buildingtrim02 roof01 roof02 smbldg1 smbldg5 | 58,48,39 L50; 60,57,54 L58 | cb4 x2, cb7 x4 | 200-300, 300-350 | 2696 + 2827 |
| cb14a | cb14a | 16.0 x 12.0 x 17.0 | 18 | roof01 smbldg1 | 50,51,50 L51 | cb4 x5, cb7 x11 | 200-300, 300-350 | 5013 |
| cb14b | cb14b | 16.0 x 12.0 x 17.0 | 16 | roof01 smbldg2 | 43,44,35 L43 | cb4, cb7 (roll) | 200-300, 300-350 | 5046 |
| cb14c | cb14c | 16.0 x 12.0 x 17.0 | 18 | aphagar05 roof01 smbldg3 | 47,42,37 L43 | cb4, cb7 (roll) | 200-300, 300-350 | 5059 |
| cb15a | cb15a | 57.1 x 24.5 x 30.8 | 68 | buildingtrim01 roof02 smbldg1 smbldg3 smbldg5 | 64,50,40 L53 | cb4 x4 | 200-300, 300-350 | 329 |
| cb15b | cb15b | 57.1 x 26.0 x 24.0 | 96 | buildingtrim01 buildingtrim02 roof02 smbldg1 smbldg3 | 66,51,42 L54 | cb4 (roll) | 200-300, 300-350 | 325 |
| cb16a | cb16a | 28.3 x 57.1 x 30.0 | 60 | buildingtrim01 genbldg3 roof02 smbldg1 smbldg3 | 63,51,43 L54 | cb4 x4 | 200-300, 300-350 | 330 |
| cb16b | cb16b | 28.3 x 60.3 x 26.0 | 66 | buildingtrim01 genbldg3 roof02 smbldg1 smbldg4 smbldg5 | 64,52,44 L55 | cb4 (roll) | 200-300, 300-350 | 325 |
| cb17a | cb17a | 45.3 x 45.3 x 32.0 | 56 | aphagar05 bldg1 roof01 | 68,62,54 L63 | cb5 x2, cb6 x1 | 700-800, 800-900 | 178 |
| cb17b | cb17b | 45.3 x 45.3 x 36.0 | 56 | bldg3 roof01 | 50,51,56 L51 | cb5, cb6 (roll) | 700-800, 800-900 | 165 |
| cb18a | cb18a | 45.3 x 45.3 x 24.0 | 18 | roof01 smbldg1 | 50,51,50 L51 | cb5 x2, cb6 x1 | 200-300, 300-350 | 182 |
| cb18b | cb18b | 45.3 x 45.3 x 24.0 | 12 | roof01 smbldg5 | 48,41,35 L42 | cb5, cb6 (roll) | 200-300, 300-350 | 167 |
| cb19a | cb19a | 44.9 x 44.9 x 30.1 | 88 | bldg4 roof01 | 65,62,54 L62 | cb5 x3, cb6 x2 | 700-800, 800-900 | 280 |
| cb19b | cb19b | 45.3 x 45.3 x 40.0 | 96 | bldg3 roof01 | 49,50,54 L50 | cb5, cb6 (roll) | 700-800, 800-900 | 278 |
| cb20a | cb20a | 45.3 x 45.3 x 52.0 | 60 | bldg4 roof01 | 62,60,53 L60 | cb5 x1, cb6 x2 | 700-800, 800-900 | 168 |
| cb20b | cb20b | 62.2 x 45.3 x 52.0 | 107 | bldg1 roof01 | 68,62,54 L63 | cb5, cb6 (roll) | 700-800, 800-900 | 151 |
| cb21a | cb21a = cb21b | 25.5 x 25.5 x 26.0 | 36 | buildingtrim02 roof01 roof02 smbldg1 smbldg3 | 60,57,54 L58; 53,47,41 L48 | cb5 x2, cb6 x4 | 200-300, 300-350 | 285 + 336 |
| cb22a | cb22a | 20.4 x 20.4 x 16.0 | 50 | buildingtrim02 poster04 roof01 roof02 smbldg1 | 73,67,64 L68 | cb5 x1 | 200-300, 300-350 | 59 |
| cb22b | cb22b | 20.4 x 20.4 x 18.9 | 48 | buildingtrim01 poster03 roof01 roof02 smbldg5 | 70,58,46 L60 | cb5 (roll) | 200-300, 300-350 | 65 |
| cb23a | cb23a | 20.4 x 20.4 x 16.0 | 46 | buildingtrim02 poster02 roof01 roof02 smbldg2 | 74,67,55 L68 | cb6 x1 | 200-300, 300-350 | 47 |
| cb23b | cb23b | 20.4 x 20.4 x 16.0 | 48 | buildingtrim02 poster04 roof01 roof02 smbldg1 | 72,69,68 L70 | cb6 (roll) | 200-300, 300-350 | 45 |
| cb24a | cb24a | 57.7 x 57.7 x 30.8 | 52 | buildingtrim01 roof02 smbldg1 smbldg3 | 64,49,41 L53 | cb6 x1 | 200-300, 300-350 | 51 |
| cb24b | cb24b | 57.4 x 57.4 x 24.0 | 74 | buildingtrim01 buildingtrim02 roof02 smbldg1 smbldg3 | 66,51,42 L54 | cb6 (roll) | 200-300, 300-350 | 39 |

The box is the union over a model's meshes in the decoration frame, base at y = 0 for every
building. The `a` model with its `det01` half spans 41.0 x 28.3 x 16.0 (`cb00`), 36.0 x 36.0 x 25.7
(`cb01`), 31.8 x 36.1 x 66.9 (`cb02`), 36.1 x 32.0 x 66.2 (`cb03`), 50.9 x 50.9 x 17.7 (`cb06`),
45.3 x 45.3 x 66.2 (`cb08`) and 50.9 x 50.9 x 33.7 (`cb11`). `cb11a` also stands without a `det`
half on three lots.

Estimated buildings per template, from each source's own count divided by its own share:
`cblock1` about 12,686, `cblock2` 2,270, `cblock3` 1,265, `cblock4` 2,802, `cblock5` 2,115,
`cblock6` 1,750, `cblock7` 31,077. The `cblock4` plus `cblock7` total, 34,216, is exact.

Every `cblock2` and `cblock3` building (`cb06` to `cb11`) stands turned 45 degrees in its own mesh
frame: `cb06` is `cb00`'s 40 x 24 m slab, `cb08` is `cb03`'s notched tower, and the others are
32 m squares, so their 45.3 m boxes are a square's diagonal. `tiers.py --turn=45` measures them
turned back. The `cblock4` and `cblock7` buildings (`cb12` to `cb16`) stand square in their frames.
Every `cblock5` and `cblock6` building (`cb17` to `cb24`, and `cb06` and `cb11`) stands turned 45
degrees as well, so their boxes are diagonals too: `cb18` is a 32 m square, not 45.3 m, `cb21` is
`cb13`'s 12 x 20 m block, `cb22` and `cb23` are 16 x 12 m, and `cb24a` and `cb24b` are `cb15a` and
`cb15b` turned a quarter (24 x 56 m).

The `cblock4` and `cblock7` shapes, from their sections (all meshes of the chain):

- `cb12a` and `cb12b`: 20 x 12 m, walls to 12 m and a 2 m cornice band that leans 0.42 m out on
  the long sides to a flat tile roof at 14 m; a street awning on each long side, 2 m out, from 4 m
  at the wall to 2 m at its edge (`cb12b` 1.6 m higher). `cb12b` adds a 2 m square chimney at
  x 4 to 6, z -0.58 to 1.42, up to 22 m, which is its box's height.
- `cb13a` (= `cb13b`): 12 x 20 m, walls to 24 m, the same cornice to 26 m, awnings on the long sides.
- `cb14a` and `cb14b`: one geometry (they differ in triangulation only): 16 x 12 m, walls to 12 m,
  a hipped roof rising to a point at 17 m. `cb14c`: the same block under a gable along its length,
  ridge at 17 m.
- `cb15a`: 56 x 24 m, walls to 24 m, a cornice to 26 m flaring 0.55 m out, a flat roof with a
  pyramid roof light (8 m square, apex 30.83 m). `cb15b`: 56 x 24 m to a cornice at 22 m, with a
  20 m wide middle (x -8 to 12) to 24 m, and 1 m brackets on the long walls.
- `cb16a` and `cb16b`: 24 x 56 m, walls to 20 m, a cornice to 22 m, a hipped roof to a point at
  30 m (`cb16a`) or 26 m (`cb16b`); awnings on the long walls (and, on `cb16b`, two on the ends)
  over spans a half turn apart.

The `cblock5` and `cblock6` shapes, turned back (all meshes of the chain). `tiers.py` refuses most
of them, since their roofs are bands between unlike sections; these are read off the sections.

- `cb17a`: a U of 8 m wings round a court open on one side, walls to 24 m, ridged roofs to 32 m
  along each wing's middle, mitred at the two corners, with gable ends (the brick-red
  `aphagar05` triangles, mean (99, 60, 35) over their texels at or below `UNLIT_LUMA`) on the wing
  ends.
- `cb17b`: a U of 8 m wings round a court open on the other side, flat at 24 m, with an 8 m stair
  tower at one outer corner to 32 m under a pyramid to 36 m. The court's back wall is the further
  mesh `o2`.
- `cb18a` and `cb18b`: a 32 m square, walls to 20 m, a low pyramid to 24 m.
- `cb19a`: a 31.78 m square with a 7.94 m wide, 3.18 m deep notch in the middle of each side to
  25.22 m, and a 25.42 m square top floor to 30.09 m. The pale vertical stripes on its upper walls
  are its window texture stretched over short faces, not a crown.
- `cb19b`: a 32 m square with an 8 m wide, 4 m deep notch in the middle of each side to 20 m, a
  hipped skirt rising 4 m to the 24 m square of an upper block (the notches' back walls run on up
  through it), the block to 32 m and a pyramid to 40 m.
- `cb20a`: a 32 m podium stepping in at 12, 16 and 20 m round a 20 m tower in one corner, the
  tower to 44 m under a pyramid to 52 m whose apex stands over the podium's inner corner, 6 m off
  the tower's centre. `cb20b`: the podium widened round two towers, 24 m and 20 m, each under its
  own off-centre pyramid to 52 m.
- `cb21a` (= `cb21b`): `cb13`'s block, walls to 24 m, a cornice leaning 0.42 m out to 26 m, awnings
  on the long walls.
- `cb22a` and `cb22b`: 16 x 12 m off the lot's centre by (0.59, 1.41) m, walls to 8 m, a cornice
  leaning 0.41 m out on the long walls to a flat tile roof at 10 m, an awning on each long wall,
  and billboards standing on the roof as single planes: `cb22a` two 12 x 6 m boards across the
  roof, 8 m apart; `cb22b` two 5.9 x 8.86 m boards, reaching 18.86 m. Each board's poster faces
  away from the roof's middle; in game its other face shows plain brown planks with a thin pale
  rim round the poster.
- `cb23a` and `cb23b`: the same block turned a quarter, with one 12 x 6 m board along its middle,
  its poster on both faces.
- `cb24a`: `cb15a` turned a quarter, its cornice leaning 0.24 m out on the long walls and 0.56 m on
  the ends, its roof light at the same place. `cb24b`: `cb15b` turned a quarter, walls to 20 m, a
  cornice to 22 m and a raised middle 20 m long to 24 m.

## The `det` dressing

Sixteen shapes, 12,569 stamps, each a separate solid decoration at its building's origin. Every
one is a few kinds of piece, measured per piece (triangles joined by shared vertices) in the
decoration frame, `cb06`, `cb08` and `cb11`'s turned back by 45 degrees:

- **Street awning** (`genbldg3`): a 7.5 m slab 2.17 m off a long wall, sloping 1.25 m from about
  4.9 m (7.1 m on `cb01` and `cb11`) at the wall; 4 triangles. Four per shape on the walk-ups
  and on `cb02` and `cb03`.
- **Fire escape** (`nosegun5` landings, `scafolding1` lattice): a zigzag stair 6 m wide and 1 m off
  a wall, from the ground to the roof (16, 24 or 32 m); 12 to 28 triangles.
- **Roof box** (`oildock1`, `roof01`): 5 x 11 m and 1.72 m tall (3.44 m on `cb01`), on the roof;
  10 triangles.
- **Vent stub** (`genbldg3`, `oildock1`): a 1 m cube on the roof; 10 triangles. `cb03det02` and
  `cb08det02` are this alone.
- **Ledge ring** (`aluminum_border2`, `roof01`): a band 0.45 m deep at 3.55 to 4 m, 2 m out round
  the whole footprint (44 x 28 m on `cb00` and `cb06`, 36 m square on `cb01` and `cb11`);
  16 triangles.
- **Mast** (`scafolding1`): a lattice cone 2 m square and 4 m tall on `cb02det02`'s roof;
  8 triangles.

| Shape | On | Placed | Tris | Pieces |
|---|---|---:|---:|---|
| `cb00det01` | `cb00` | 1,050 | 28 | 4 awnings, fire escape |
| `cb00det02` | `cb00` | 1,077 | 48 | 4 awnings, fire escape, vent, roof box |
| `cb00det03` | `cb00` | 994 | 38 | fire escape, roof box, ledge ring |
| `cb01det01` | `cb01` | 1,171 | 46 | fire escape, roof box, ledge ring |
| `cb01det02` | `cb01` | 1,179 | 48 | 4 awnings, fire escape, roof box |
| `cb02det01` | `cb02` | 1,144 | 36 | 4 awnings, vent, roof box |
| `cb02det02` | `cb02` | 1,207 | 28 | vent, roof box, mast |
| `cb03det01` | `cb03` | 819 | 26 | 4 awnings, vent |
| `cb03det02` | `cb03` | 747 | 10 | vent |
| `cb06det01` | `cb06` | 463 | 38 | fire escape, roof box, ledge ring |
| `cb06det02` | `cb06` | 481 | 48 | 4 awnings, fire escape, vent, roof box |
| `cb06det03` | `cb06` | 427 | 28 | 4 awnings, fire escape |
| `cb08det01` | `cb08` | 242 | 26 | 4 awnings, vent |
| `cb08det02` | `cb08` | 266 | 10 | vent |
| `cb11det01` | `cb11` | 664 | 54 | fire escape, roof box, ledge ring |
| `cb11det02` | `cb11` | 638 | 54 | 4 awnings, fire escape, roof box |

`cb06det0n` repeats `cb00det0n` and `cb08det0n` `cb03det0n` in their turned frames. Only
`scafolding1` cuts texels out by alpha (55 % of them), so the fire escapes and the mast are
see-through lattices; every other dressing texture is opaque.

**How a dressing stamp is drawn.** A `det` decoration is a solid kind like a building: its own
roll among `det01` to `det03`, its own MultiMesh and cells, its own collision shape, and its
source block's fade (75 to 250 m). It is stamped at its building's point, so its custom data
carries the building's seed (`CityModels.Seed`). `Clutter.CityOf` looks a hull up by the
decoration's name, so a `cbNNdetMM.glb.bin` model file takes the kind into the city path like a
building, its original MultiMesh suppressed by layout and its collider kept. The dressing fades
out before the 3D Scenery band's hand-over starts (400 m at Near), so the far node is drawn only
on the map edge's copies and in a spyglass disc.

**The rebuild** (`c5.py`'s `DRESSING`) puts each piece at its measured place with one near node
and a far node; no shape takes a second near variant, since every piece repeats with its building
already. Each far node holds its original's triangles exactly (566 over the sixteen). The near node
adds the undersides of the flat pieces (flights, landings, awnings, the ledge ring), so they do not
vanish from below. A fire escape's flights and landings are dark plates of the building family's
roof surface, as the original's are; its lattice bands and rails, and the mast, are flat panels of
the board format 4, our own truss of light metal bars on black, which the cut-out shader discards.
Awnings and the ledge ring's edge are light metal, roof boxes, vents and the ring's top the roof
surface. Every node stays within its original's box grown by 0.05 m (`margin.py`, at most
0.006 m past it). The roof boxes and vents stand where the original's do: on `cb00`'s fourth
near variant a tank of its own reaches into the `det02` and `det03` roof box (box overlap
0.62 m³), on `cb02b`'s first a vent stands inside the `det01` roof box (1.67 m³), and on `cb01d`
the box pierces the hipped roof as the original's does.

## Variants

The near variants per shape follow the placed counts above, summed over the models of one shape:
a shape placed 1,000 times or more takes a third variant, and one placed 2,000 times or more a
fourth. The cut sits in the largest gap of the dressed shapes' counts (1,166 to 805).

| Shape | Placed | Near variants |
|---|---:|---:|
| `cb12b` | 6,201 | 4 |
| `cb12a` | 6,065 | 4 |
| `cb13` (`cb13a`, `cb13b`) | 5,523 | 4 |
| `cb14c` | 5,059 | 4 |
| `cb14b` | 5,046 | 4 |
| `cb14a` | 5,013 | 4 |
| `cb00` (`cb00a`, `cb00b`) | 3,121 | 4 |
| `cb11` (`cb11a`, `cb11b`, `cb11c`) | 1,613 | 3 |
| `cb06` (`cb06a`, `cb06b`, `cb06c`) | 1,371 | 3 |
| `cb01a` (`cb01a`, `cb01b`) | 1,208 | 3 |
| `cb02b` | 1,185 | 3 |
| `cb02a` | 1,166 | 3 |
| `cb04a`, `cb03b`, `cb05a`, `cb05b`, `cb03a`, `cb04b` | 805 to 766 each | 2 |
| `cb01c`, `cb01d`, `cb08` (`a` and `b`), `cb09` (`a` and `b`), `cb07a`, `cb07b`, `cb10b`, `cb10a` | 577 to 218 each | 2 |
| `cb16a`, `cb15a`, `cb15b`, `cb16b` | 330 to 325 each | 2 |
| `cb21` (`cb21a`, `cb21b`) | 621 | 2 |
| `cb19a`, `cb19b`, `cb18a`, `cb17a`, `cb20a`, `cb18b`, `cb17b`, `cb20b` | 280 to 151 each | 2 |
| `cb22b`, `cb22a`, `cb24a`, `cb23a`, `cb23b`, `cb24b` | 65 to 39 each | 2 |

`cb14a` and `cb14b` share one geometry and draw two different sets of four, sharing two (the flat
walk with a tank and the set-back top floor), so that lot shows six massings. `cb21a` and `cb21b`
likewise draw two different pairs of `cb13`'s massings, so that lot shows four. No `cblock5` or
`cblock6` shape passes the cut.

## Caveats (measured / inferred / assumed)

- Measured: boxes, triangles, textures, colours, the shape comparisons, the template census, the
  collision shape set (reproduced against the engine's own figure).
- Measured, one run: the placed counts are one C5 load's engine log. The per-template split of
  stamps is not exact, because a substitution target is one kind shared by every template that
  rolls it; the per-model and grand totals are exact.
- Inferred: what a `det` half and a further mesh are, read from the contact sheets and part boxes.
- Assumed: polygons are triangulated as `Clutter.AppendTriangles` does (fan, or strip when
  flagged), which is the engine's own count.
