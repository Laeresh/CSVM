# Which ground could carry vegetation the original leaves bare?

**Question.** Per chapter, which ground textures cover real area, which of them a clutter template
dresses, and which undressed ones look like vegetation-bearing ground (grass, fields, jungle,
scrub). Also: which decoration cards each chapter carries, placed or not. Asked for
PLAN-enhanced-vegetation's density and near waves.

**Instrument.** `survey.py` reuses `../bl-305-clutter-uv/eligibility.py`'s loader, template
resolver and world walk over `extracted/<C>/gamez`, takes the registrations from `extracted/interp.json`
(`AddClutterTemplates`), and writes `survey.json` (derived from the game's bytes, never committed).
Area is the XZ projection of faces up to ~75° of slope, layer 0 and the nearest LOD only, as the
remake reads them. `green.py` filters it. `montage.py out.png C3:forest1 C1:crop04 ...` draws a
labelled texture contact sheet. Colour is the texture's mean RGB.

## Verdict

- **C3 carries about 26 km² of jungle-green ground and none of it is dressed** (`forest1` 10.5,
  `forest1-128` 8.1, `cliff1` 3.4, `cliff1_watertrans2` 2.9, `cliff1_toptrans` 2.5, smaller
  transitions). The texture is a top-down canopy photograph. Its only template is
  `cliff1_sandtrans` (palm1). `palm2` and `palm3` are in the gamez and no template places them.
- **C1 carries about 11 km² of crop fields** (`crop04` 6.8, `crop05` 4.2) **and about 13 km² of
  grassland** (`terpat03` 10.7, `terpat03b` 2.5, `grass1` 0.7), all undressed. Its forest texture
  `terpat02` (84.9 km²) is the templated one.
- **C4** has about 8 km² of undressed dry grass and rock transitions (`cliff01-trans1` 3.9,
  `cliff01-trans3` 3.3, smaller). **C2's** undressed green is two sports fields (0.05 km² each).
  **C1B's** undressed ground is grey-green rock. **C1C and C2B** have no ground; **C5** is city
  blocks, all templated.
- **There are no grass, reed or crop cards anywhere in the install.** The smallest plant cards are
  C2's `brush1`/`brush2` at about 2.4 × 2.1 m. Every sprite card is CylindricalY. C4 carries an
  unplaced second `firtree2` card (15 × 13 m); C3 carries a solid `fallen_trees` model.

Card sizes (width × height, m): C1 firtree1 10×18, dougfirtree1 10×22, firtree2 11×21, bush1
5.6×5.1, bush2 3.7×3.4; C2 spruce 5.6×9.0, palm1 7.6×13.2, palm2/palm3 5.7×10.0, brush1/2
2.4×2.1, brush3/4 5.7×5.0; C4 dougfirtree1 10×17, firtree1 10×13.

## Caveats (measured / inferred / assumed)

- Measured: areas, polygon counts, mean colours, the card inventory.
- Inferred: the green / field / other verdicts, read from names and the texture images. They are
  not a look judgement; put the textures in front of the user before acting on one.
- Assumed: the world walk's transform chain matches the engine's; it was not reconciled against a
  live build. Second-layer transition textures are not counted. Water, cloud and zeppelin
  polygons inflate each chapter's total and are left out of the verdicts.
