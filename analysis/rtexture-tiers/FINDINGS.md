# Are the `rtextureN` archives just downscaled tiers of `texture.zbd`?

**No — they are the archives the shipped game renders, and their content differs from
`texture.zbd`** (2026-08-04, the BL-048 needle hunt; results cited by `docs/tooling.md` and
`docs/formats/hud.md`).

## Findings

- **`N` is a size budget in MB of video-card texture memory.** File sizes across every chapter:
  rtexture2 ≈ 1.95 MB, rtexture4 ≈ 3.85, rtexture6 ≈ 5.7, rtexture8 ≈ 7.65, plus one full-quality
  tier sized to whatever the chapter needs (C1 `rtexture15` 14.85 MB, C1B `11` 10.09, C1C `10`
  9.26, C2 `14` 13.25, C2B `9` 8.99, C3 `12` 11.37, C4 `14` 13.05, C5 `14` 13.12).
- **Same names, same resolutions, different pixels.** C1's top tier holds the identical 881-file
  set at identical dimensions as `texture.zbd` (per-file check, both directions), but per chapter
  209–328 files differ in pixel content and 3–12 in pixel format (`tier_diff_census.py`):
  C1 296+5, C1B 217+3, C1C 216+3, C2 310+3, C2B 209+3, C3 263+4, C4 328+12, C5 299+5.
- **The mode changers gain alpha**: `needle`, `smallneedle`, `steps`, `tarmac_lines`,
  `bal_taillogo` are RGBA in the tiers, RGB in `texture.zbd`. The needle's tapered-pointer
  silhouette exists only there (`needle_profile.py` prints the per-row alpha extents: pointed tip,
  tapering shaft, waist, two hub discs — every tier carries it, down to the 8×32 one).
- **Where tiers disagree with `texture.zbd`, the tiers agree with each other** (`sky_tiers.py`:
  C4's `c4sky2` mean 240.7–241.9 across all five tiers vs 182.4 in texture.zbd; `sky1` pure white
  in all tiers vs 195). The tier copies also have MORE color levels, not fewer (no 16-bit
  quantisation loss). So `texture.zbd` reads as an older build of the set that the shipped game —
  which picks a tier by card memory — never draws.
- interp.json only ever issues `SetTextureDirectory` (a directory, never an archive name); the
  tier choice is engine logic.

**Limit:** the tier-selection rule is inferred from the MB naming, not traced in the exe. All
tiers carry the needle alpha, so the gauge conclusion is tier-independent.

## Instruments

- `tier_diff_census.py` — per-chapter identical/pixels-differ/mode-differ counts, `texture` vs the
  top tier, over the unpacked `extracted/` trees.
- `needle_profile.py` — the top-tier needle.tif per-row opaque-alpha extents (the silhouette).
- `sky_tiers.py` — C4 sky-sheet means across all tiers (the all-tiers-agree check).

All three read `extracted/<chapter>/<tier>/` (run `ExtractAssets.ps1 -Unzip` first) and print to
stdout; no game data is written here.
