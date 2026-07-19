# HUD: compass tape + cockpit gauges

Validated 2026-07-18 against `OriginalScreenshots/HUD.png` (2556×1440, dgVoodoo) by
pixel-probing every tick and label; gauges decoded 2026-07-19 from the planes.zbd
`gauges` subtrees + `OriginalScreenshots/HUD with dmg.png`. Remake implementations:
`src/Flight/CompassTape.cs`, `src/Flight/GaugeCluster.cs`.

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

## The cockpit gauges (altimeter / speedometer / damage display)

**The gauge dials are 3D models inside each player plane's tree in planes.zbd** — a
`gauges` subtree under the (otherwise skipped) cockpit, one per plane, with the same
child names everywhere: `altimeter`, `speedometer`, `damageindicator`, plus `comp`,
`horizn`, `gungauge`, `missilegauge`, `nitrogauge`. All dial meshes are flat polygons
in dial-local coordinates (x right, y up, **bezel radius = 1**, z ≈ 0); the interp
`support\cockpit.gw` boot script wires their dynamic behavior via `FindSubNode` +
`CycleTextureSet` texture swaps. Texture pixels live in **every chapter's
`texture.zbd`** (not rimage.zbd).

- **Face**: each dial's face is a 12-gon (radius 1) mapping the full face texture —
  `altimeter.tif` / `speedometer.tif` / the per-plane `<short>_damage.tif`
  (`bldhwk_damage`, `ke_damage`, `pm_damage`, `agyro_damage`, `avenger_damage`,
  `bal_damage`, `fury_damage`, + AI planes). The face texture includes the bezel ring
  and the *unlit* (dark) LOW ALT / STALL windows; 12-gon corners cut the texture's
  square corners. Draw priority 1.
- **Needles are single textured quads — the taper and the hub are painted in
  `needle.tif` (32×128, no alpha, drawn opaque), not meshed.** Quad x −0.055…0.052,
  y −0.245…0.510 (pivot at the origin, tip +y = texture top; the texture's top 60 %
  is the light shaft with a notch, the bottom 40 % the dark hub box with two black
  discs). The altimeter has two: `hundreds` (long, priority 9, z 0.05) and
  `thousands` (short/wider: x ±0.07, y −0.181…0.368, priority 8, z 0.025 — same
  texture); the speedometer one (`speed`, priority 8). The nodes' modeled rest
  rotations are arbitrary; the engine sets absolute angles. **Note:** the shaft in
  the texture is a flat full-width slab (rows 0–75 all constant, verified by full
  sampling), yet the original's rendered needle is a slim lance tapering to a point
  — that shape is applied engine-side, in neither the texture nor the mesh/UVs (the
  remake replicates it with a load-time alpha taper).
- **Warning overlays** `lowalt_on` / `stallwarning_on` (priority 7 — *under* the
  needles): the lit window quad (`lowalt.tif` / `stall.tif`, 64×32, red) **plus two
  red bezel slashes** (`redhilite.tif` quads at the dial edge, left+right of the
  window's side). The whole node toggles/blinks.
- **Damage display**: the `damageindicator` node's own mesh is the silhouette face;
  its four children `nosedamage` / `taildamage` / `leftwingdamage` / `rightwingdamage`
  each carry exactly two polygons (priority 7): a **border bar** at the bezel edge
  (`greenhilite.tif`; nose = top bar, tail = bottom, wings = left/right slanted bars)
  and a **part-shaped hatch fill** tracing that part on this plane's silhouette
  (`grn_hatchptrn.tif`, 8×8, tiled UVs up to ~5×). `cockpit.gw` gives each zone a
  4-map texture cycle — green/yellow/**orange**/red `*hilite` + `*_hatchptrn` — the
  color change IS a texture swap. **So part positions are per-plane mesh data,
  nothing is computed from the silhouette texture.**
- **Thresholds**: every player part's vehicle.json `injure_anims` carry
  `*_damage_green` at 0.72, `*_damage_yellow` at 0.46, `*_damage_red` at 0.20 (the
  anims themselves live in the undecoded cam_anim.zbd). The display uses **all four
  cycle colors** (orange user-confirmed in the original, 2026-07-19) — each anim
  threshold steps to the *next* color: green > 0.72, yellow ≤ 0.72, orange ≤ 0.46,
  red ≤ 0.20 (red on a still-flying plane matches the damage reference shot; the
  anim names lag their effect by one state). Blink: the original blinks a zone
  (fill + border) for ~5 s after it takes a hit, even inside green (user-observed).
- **Scales** (measured off the face textures): altimeter 0–9 clockwise from top, 36°
  per digit — long needle 360°/1,000 ft, short 360°/10,000 ft; speedometer labels
  0/100/200/300 at ≈0°/69°/143°/216° clockwise → **≈0.72°/mph** linear.
- **Screen layout** (HUD.png, 2556×1440, bezel dark-span scans): all three dials
  share **radius ≈ 85 px**; altimeter center (425.5, 1108.5), damage dial
  (426.5, 1299), speedometer mirrored ≈ 420 px from the right edge, same height as
  the altimeter. (The two reference screenshots place the cluster slightly
  differently — HUD.png is the canonical one, matching the compass metrics.)
- Also in the subtree, unwired in the remake: `gungauge`/`missilegauge` (ammo
  counters via letter/digit texture cycles, `ggindicatorN`/`mgindicatorN` belt
  lights), `nitrogauge`, the artificial-horizon `horizn` and drum `comp` compass.

## Open question

Which world axis is compass **north**: the remake assumes **−Z** (consistent with the
map layout and motion), but the original's convention has not been verified in-game.
If it differs, the fix is the one heading line in `FlightController`.
