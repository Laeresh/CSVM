# Alpha classification: blend vs scissor, decided by how binary the ink is

**Question.** The engine must decide per texture whether alpha means BLEND (soft
translucency, transparent pass, no depth write) or SCISSOR (1-bit cutout at 0.5, opaque
pass). The old `TextureArchive.AlphaIsSoft` rule — soft when max alpha < 140, or when
opaque < 30% of *all* texels and partial > 35% — let semi-transparent art through to
scissor: C2's `eiffel2` lattice (2.8% opaque, 25% partial, most of the sheet drawn at
40–50% alpha) scissored down to 7.7% of its texels, which is the "Eiffel replica is
nearly invisible" report. What is the right pixel rule, measured over the whole install?

**Instrument.** `alpha_census.py` — reads the same tier the engine loads (the chapter's
top `rtextureN` set, per `SessionPaths.ChapterTextures`), skips the authored `_1`/`_2`
mip siblings, and for every texture with a real alpha channel computes: max alpha, opaque
share (a ≥ 200), partial share (32–199), ink share (a ≥ 32), scissor survival
(a > 127, as a fraction of texels), plus the old rule's verdict. Writes
`alpha_census.json` (604 unique alpha textures across 8 chapters; 135 old-soft).

**Finding 1 — survival alone is the wrong metric.** The first candidate rule, "soft when
under T of the ink survives the 0.5 cutoff", correctly catches erased art but is blind to
the opposite failure: scissor *solidifies* partial alpha above the threshold. `falls`
(the C1/C4 waterfall sheet) has 100% ink survival — every texel sits at ~55–80% alpha —
so that rule would scissor it into a solid opaque wall. Same for `thin_shadow` (0.857),
`shipwreck8` (0.910), the C1B surf/turbulence sets, and the clouds themselves (0.65).

**Finding 2 — binary-ness separates the populations cleanly.** Metric: `opaque / ink` —
the fraction of ink texels that are truly opaque. A faithful cutout is opaque fill plus
AA edges (high); authored translucency is mostly partial (low). Distribution measured
install-wide:

| population | 5% | 25% | median | 75% | 95% |
|---|---|---|---|---|---|
| old-soft (135) | 0.00 | 0.00 | 0.03 | 0.24 | 0.34 |
| old-hard (469) | 0.24 | 0.64 | 0.83 | 0.94 | 1.00 |

Known families: trees 0.74–0.94, rails 0.76–1.0, bushes 0.49–0.88 (bush2 = 0.49 is the
tightest genuine cutout), `eiffel1` 0.64 (a real cutout — its distance problem is mip
alpha decay, a separate fix), `eiffel2` 0.10, clouds 0.25, falls 0.04, glow flares
0.07–0.33, cockpit indicator lights 0.03.

**Decision — soft ⇔ ink == 0 or opaque/ink < 0.45.** At 0.45: **zero** currently-blended
textures flip back to scissor (no regressions on the working set), and 67 flip
scissor → blend, overwhelmingly authored translucents: `eiffel2`, the `fire101–112`
refinery flame sprites, every `*_flare`/`*indicator` glow, hotel neon letters (`hotel_e/l/t`),
`sldhneon`, `ripple`, `spiderweb`, the `tarmac_planespot`/`tarmac_skid01` decals, the 11
plane `*_noselogo` decals (0.348 — soft-edged paint), zeppelin window skins
(`dxzepcab9`, `zep08/09`), `depot04`/`dockhouse02` (62% partial walls). At 0.50 `bush2`
et al. would flip wrongly; 0.40 misses `fadedsign02/03` and `shipwreck7`. The old
max < 140 rule is subsumed (max < 140 ⇒ opaque = 0 ⇒ ratio 0).

**Finding 3 — the coastline sheets need a name, not a threshold.** `beach1` (binary 0.685),
`shore1` (0.539), `shore1_end` (0.608), `shore2` (0.542) and `shore_trans` (0.555) are the
waterline art: a feathered ramp from land to water, 33–40% partial texels. The ratio is a
whole-sheet vote and each sheet's dry-land half is solid, so it reads them as cutouts and the
waterline scissors to a 1-bit sawtooth. No threshold fixes this — 0.6 catches three of the five
and drags in unrelated art, and the inland transition sheets that share their role
(`cliff01_trans1` 0.814, `terpat01_trans1` 0.928, and the rest of those two families) really are
binary. They are named in `TextureArchive`'s soft-alpha family table instead, which also keeps them
out of the scissor-coverage mip boost that would re-harden the ramp at distance.

## The original has no cutout path, so no flag selects one

Decoded from `crimson.exe` (Ghidra). The renderer is `D:\zipper\gamez\zvideo\zvid_ddd3d.c` over
`IDirect3DDevice3`, whose vtable puts `SetRenderState` at `+0x58`, `SetTexture` at `+0x98` and
`SetTextureStageState` at `+0xa0`.

**`D3DRENDERSTATE_ALPHATESTENABLE` (15) is never set, anywhere.** The whole `0x0059e000–0x005ab000`
D3D layer contains three `push 0xf` instructions and all three are 4-bit channel masks in the
ARGB4444 blit, sitting between `push 0xf0`, `push 0xf00` and `push 0xf000`. `ALPHAREF` and
`ALPHAFUNC` are likewise never touched, so alpha test stays at its Direct3D default of FALSE for
the whole run. **Every surface the original draws is either opaque or alpha-BLENDED. There is no
scissor, so nothing in the data selects one, and the coastline sheets differ from the trees in the
pixels only.**

Device init (`FUN_005a0e00`) fixes `SRCBLEND` = `SRCALPHA` and `DESTBLEND` = `INVSRCALPHA`, and
caches `ALPHABLENDENABLE` in a global so the draw only re-issues it on change. The draw
(`FUN_005a4210`) enables blending from one per-texture render-mode field, `tex+0x10 == 4`, which
also selects `D3DTSS_ALPHAOP` = MODULATE; the sorted pass (`FUN_005a6160`) forces blending on,
`ZWRITEENABLE` off, and promotes any primitive whose own colour alpha is below 255 to that mode. A
per-primitive bit picks additive `ONE/ONE` over the normal `SRCALPHA/INVSRCALPHA`, matching the
script vocabulary `am_alpha_onezero` / `am_alpha_oneone` / `am_alpha_alphainvalpha` /
`am_alpha_oneinvalpha`.

**What the archive's alpha class actually decides is precision, not blending.** In the texel blit
`FUN_005a27e0`, a texture with no alpha plane (`TextureAlpha::Simple`) gets its alpha from a colour
key — `alpha = (texel == key) ? 0 : 0x8000` in ARGB1555 — while a texture with a plane
(`TextureAlpha::Full`, `TexFlags::FULL_ALPHA` = `0x08`, tested as `flags & 8`) takes RGBA8888 where
the card supports it, else ARGB4444, else ARGB1555 with the 8-bit alpha thresholded at 128. So a
1-bit look in the original is either colour-keyed art or a period video card without a 4444/8888
texture format, never a render state — and on the hardware this project targets, the full ramp
survives.

The other candidates were checked and are not it. The material `flag` bit
(`MaterialFlags::UNKNOWN`) is the decal-receiving surface (`docs/org/weaponRay.md`), 0 on every
coastline and every soft texture. Polygon `in_out` is zero across C2/C3/C4; polygon `unk3` is
105/12743, 1/16024 and 92/19476 polygons and lands on opaque terrain (`terpat01`, `river1`), zero
on every coastline, cloud, waterfall and shadow polygon. `ALPHA_GRADIENT` is a `weather.cpp` key
for precipitation particles, and `keyed`/`alpha` belong to the 2D UI pane vocabulary.

## The families that blend by name

Three families blend whatever their pixels measure, for the reason above: the cutout has no
original to reproduce. They are the tree, bush and brush cards (13), the fence, railing, ladder,
stair and grate cards (20), and the tower lattice, girder, support and cable cards (23), each list
being the family's currently-scissored members at the 0.45 threshold, so a name the census already
calls soft is absent because naming it would decide nothing. `TextureArchive` holds them as one
list per family, and `Clutter`'s sprite shader reads the same verdict rather than cutting at 0.5 of
its own. A scissored texture this census names outside the three keeps the pixel rule.

**Landed** in `TextureArchive.AlphaIsSoft` (same census date). Re-run
`python alpha_census.py` after any classifier change; it prints the flip lists at the
landed 0.45 binary-ness threshold, and `alpha_census.json` carries the raw stats
(including the rejected survival metric) for any re-derivation.
