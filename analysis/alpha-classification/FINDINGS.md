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

**Landed** in `TextureArchive.AlphaIsSoft` (same census date). Re-run
`python alpha_census.py` after any classifier change; it prints the flip lists at the
landed 0.45 binary-ness threshold, and `alpha_census.json` carries the raw stats
(including the rejected survival metric) for any re-derivation.
