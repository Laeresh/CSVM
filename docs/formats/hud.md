# HUD: the compass tape

Validated 2026-07-18 against `OriginalScreenshots/HUD.png` (2556×1440, dgVoodoo) by
pixel-probing every tick and label. Remake implementation: `src/Flight/CompassTape.cs`.

## Textures

The compass ships as two small textures in **every chapter's `texture.zbd`** (not in
`rimage.zbd`, which holds only menu/briefing UI):

- **`compassticks2`** (64×16, opaque, black background) — one repeating tick segment
  covering **15° of heading**:
  - The **tall tick** straddles the tile seam: solid-255 core at columns 62–63,
    soft falloff continuing at columns 0–3 of the next tile. Vertical span rows 6–15.
  - Four **minor ticks** (one per 3°), 255 cores at columns 11–12 / 25–26 / 38–39 /
    50–51, visible only in rows ~11–15.
  - Tiling segments edge-to-edge automatically produces a tall tick at every 15°
    multiple. The pattern is mirror-symmetric, so flip orientation is irrelevant.
- **`compasstxt`** (128×32, soft alpha) — cream (~197,194,150) letter atlas laid out
  as the four pre-kerned intercardinal pairs, **"NE SE SW NW"**, at x 1–25 / 27–51 /
  52–83 / 84–115 (letters within: N 84–95, E 40–51, S 52–63, W 64–83; singles are cut
  from the pairs). A 5 px white→black vertical gradient block sits at x 123–127 —
  purpose unknown; it does not appear in the in-flight compass.

## Rendering model (measured, not decompiled)

- The tape is a **cylindrical drum viewed edge-on** showing exactly 180° of heading:
  a mark Δ° from the current heading renders at `x = center − R·sin(Δ)`. At 1440p the
  fit over every visible tall tick gives **R ≈ 127.6 px**; bar rect ≈ 263×40 px with
  its top at y=35, horizontally centered (scales with screen height).
- **Headings increase to the left** (W renders left of SW, S right — a real
  whiskey-compass card, mirrored vs a modern heading tape).
- Brightness falls off as **cos(Δ)** for ticks and labels alike (center tick peaks
  ~245 = the 255 texel cores through AA; talls at Δ=40.5° ≈ 194 = 255·cos; labels
  207 → 152 → 105 at Δ 4.5°/40.5°/49.5°). No gain/MODULATE2X — the cores are
  already full-white in the texture.
- The tick tile is drawn **~25% taller than the bar, bottom-aligned** (empty top rows
  clip): talls reach 77% of bar height, minors 42%. Mapping the 16 px tile to the bar
  height exactly leaves them visibly stubby.
- Tick filtering is effectively **point-sampled** (the comb's hard 1–2 px edges and
  per-tick brightness lottery are minification aliasing); the labels are drawn
  smooth (bilinear), **billboarded upright** at their drum x — label width does not
  compress with the drum (verified: edge W same width as center letters), scaled
  0.625× of the atlas (20 px tall at 1440p) with the box top ~3 px below the bar top.
- Both bar ends are capped by a **bright tall rim tick** (~192 at the very edge — the
  drum's silhouette); regular ticks fade out ~20 px before reaching the rim.
- Labels every 45° (octants), no numeric readout, no lubber line — the current
  heading is read from the centered, brightest label.

## Open question

Which world axis is compass **north**: the remake assumes **−Z** (consistent with the
map layout and motion), but the original's convention has not been verified in-game.
If it differs, the fix is the one heading line in `FlightController`.
