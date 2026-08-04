# HUD: compass tape + cockpit gauges

Part of the [format documentation](README.md). Validated 2026-07-18 against
`OriginalScreenshots/HUD.png` (2556×1440, dgVoodoo) by pixel-probing every tick and
label; gauges decoded 2026-07-19 from the planes.zbd `gauges` subtrees +
`OriginalScreenshots/HUD with dmg.png`. Remake implementations:
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
  `needle.tif`, not meshed.** Quad x −0.055…0.052,
  y −0.245…0.510 (pivot at the origin, tip +y = texture top). The altimeter has
  two: `hundreds` (long, priority 9, z 0.05) and
  `thousands` (short/wider: x ±0.07, y −0.181…0.368, priority 8, z 0.025 — same
  texture); the speedometer one (`speed`, priority 8). The nodes' modeled rest
  rotations are arbitrary; the engine sets absolute angles. ⚠ **The pointer shape
  lives only in the `rtexture*` tiers' copy of `needle.tif`** (decoded 2026-08-04):
  the base `texture.zbd` copy is a 32×128 RGB flat full-width slab with no alpha,
  but every `rtextureN` tier ships a same-size **RGBA** copy with different art
  (beveled lance, rimmed hub discs) whose alpha channel is the complete antialiased
  silhouette — pointed tip, tapering shaft, waist, two hub discs. An earlier note
  here claimed the shape was applied engine-side; it is simply in the archives the
  engine actually renders from (see `docs/tooling.md` on the tiers).
- **Warning overlays** `lowalt_on` / `stallwarning_on` (priority 7 — *under* the
  needles): the lit window quad (`lowalt.tif` / `stall.tif`, 64×32, red) **plus two
  red bezel slashes** (`redhilite.tif` quads at the dial edge, left+right of the
  window's side). The whole node toggles/blinks.
- **Damage display**: the dial's face is a single untextured 12-gon (the dark backing
  disc). ⚠ **Where it is parented differs per aircraft** — verified across the whole
  roster 2026-07-19: on `player_bhawk` it is the `damageindicator` node's *own* mesh,
  but on **every other player plane** that node is mesh-less (`mesh_index` −1) and the
  identical 12-gon hangs off an extra generically-named child instead (`g951` on the
  Fury, `g927` Kestrel, `g1156` Balmoral, `g992` Warhawk, `g843` Devastator, …). A
  reader that only looks at the dial node's own mesh therefore draws a backing disc for
  the Bloodhawk and bare floating zone shapes for all ten other aircraft. The safe rule
  is the one the other two dials already need: **anything under the dial that is not a
  recognised functional child is face geometry.** Its
  four children `nosedamage` / `taildamage` / `leftwingdamage` / `rightwingdamage`
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
- Also in the subtree, still unwired in the remake: `nitrogauge`, the
  artificial-horizon `horizn` and drum `comp` compass. The `gungauge` /
  `missilegauge` are decoded below.

## The weapon gauges (gun / missile)

Decoded 2026-07-24 from the planes.zbd `gungauge` / `missilegauge` subtrees +
`support\cockpit.gw` (the interp boot script that wires their texture cycles);
remake implementation extends `src/Flight/GaugeCluster.cs`. Screen placement is
ours (measured off `OriginalScreenshots/HUD.png`, the Warhawk): the **ROCKETS**
dial sits one dial-pitch (190.5 px, the alt→damage spacing) above the altimeter,
the **GUNS** dial the same above the speedometer; both share the other dials'
radius (≈85 px at 1440p).

Both are circular dials laid out **identically on all 11 flyable models** (unlike
the damage dial, there is no per-plane parenting quirk — verified across the whole
roster): the `gungauge`/`missilegauge` node is always mesh-less (`model_index` −1)
and the labelled face (`gungauge.tif` / `missilegauge.tif`, a 12-gon, priority 1,
carrying the baked **GUNS** / **ROCKETS** legend) hangs off a generically-named
child (`g815` / `g819`). The safe reader rule is the damage dial's:
**anything under the dial that is not a recognised functional child is face.**

The functional children, and how `cockpit.gw` drives each:

- **`4char_ammo`** — a row of **4 digit quads** (priority 7, lower centre). Each
  carries `CycleTextureSet` 11 mapping `zero.tif`…`nine.tif` then `SPACE.tif`
  (frames 0–10); the engine sets each cell's frame to spell the count. The remake
  shows it **right-aligned, space-padded**. **Guns: the *selected* gun group's own
  rounds** (per group — the Balmoral's two .50s count independently). **Rockets:
  the *per-pylon* rounds of the pylon the arrow points at (the next to fire) — NOT
  the sum across pylons** (a full HE pylon reads `3`; the original's Warhawk shows
  `BOOM 3`, not `24`).
- **`6char_type`** — a row of **6 glyph quads** (priority 7, upper centre).
  `CycleTextureSet` 37 maps `A.tif`…`Z.tif`, then `zero.tif`…`nine.tif`, then
  `SPACE.tif`. Shows the selected weapon's short **`NAME`** from `weapons.json`,
  upper-cased and left-aligned (`30slug`→`30SLUG`, `BOOM`, `SONIC`).
- **`ggindicator0..3`** (gun, 4) / **`mgindicator0..7`** (missile, 8) — the **belt
  lights**, one per gun slot / pylon, arranged around the ring: index 0 at the top
  (90°) and running **counter-clockwise** (gun 90° apart, missile 45°). Each is a
  `Xhilite.tif` bezel bar + a `Xindicator.tif` light, both carrying a **3-frame**
  cycle green→yellow→red (`CycleTextureSet` 3). The remake lights only the slots the
  airframe actually has (turret gun groups are inert, so a 2-gun plane lights 2) and
  **steps the colour by that slot's remaining fraction** — green healthy, yellow low,
  red empty; a per-pylon HE rocket (3 rounds) steps green(3/2)→yellow(1)→red(0). The
  green/yellow/red **thresholds are a TUNE** pending an original playtest.
- **`ggarrow`/`mgarrow`** — a `smallneedle.tif` pointer (priority 49, rest points
  up at slot 0) rotated about the dial centre to the selected slot: the gun arrow to
  the **selected gun group**, the missile arrow to the **next pylon that will fire**.
  Unlike the dial needles, the arrow's shape IS its mesh: a single 5-vertex polygon
  (pointed tip at +y, two shoulders, a base) whose wrapping UVs (u 0.98–2.02,
  v 0.50–3.10) smear the tiny 16×16 texture across it.

⚠ The digit/letter/indicator textures (`zero.tif`…, `A.tif`…, `greenindicator.tif`,
`greenhilite.tif`, `smallneedle.tif`, `gungauge.tif`, `missilegauge.tif`) live in
**every chapter's `texture.zbd`**, like the other gauge art — not in `rimage.zbd`.

### The text readout (`MSG_HUD_GUNGAUGE` / `MSG_HUD_MISSLES`)

The message table (`extracted/messages.json`) carries a parallel **text** form of the gauges,
alongside `MSG_HUD_AIRSPEED` / `MSG_HUD_ALTIMETER` / `MSG_HUD_HEALTH` (the "hudSWGauges" software
gauges):

- **`MSG_HUD_GUNGAUGE`** (id 188) = `"GUNS: %1: %2!d!"`
- **`MSG_HUD_MISSLES`** (id 189) = `"MISSILES: %1: %2!d!"` (the table's own misspelling)

`%1` names the **gun group / rocket type** — which is *why* these strings exist: the counters are
per gun group and per pylon, so the readout has to say *which* one. `%2!d!` is the integer count.
The remake (E36, `src/Flight/WeaponReadout.cs`) resolves both through `Messages`, fills `%1` with the
gun group's **mount name** (`Inner Wing Guns`, from `IDS_AIRFRAMEGUNGROUPNAMES`) or the rocket's
resolved **display name** (`High-explosive rocket`, from its `MSG_WEAP_*` `DESC`), and `%2` with the
selected group's per-group rounds / the next-to-fire pylon's per-pylon rounds. It draws in the
`5pointhud` font at the pane's bottom centre. The placeholder grammar (`%N`, a trailing `!spec!`
consumed, `%%` → literal `%`) is handled by `Messages.Fill`.

## The HUD bitmap font (`5pointhud`)

Decoded 2026-07-24 by pixel-probing the atlas; remake reader `src/Flight/HudFont.cs`.

Two textures in **`extracted/rimage/`** (the menu/UI image set — *not* the chapter texture
archives that carry the compass/gauge art):

- **`5pointhud.png`** — the normal font.
- **`5pointhudbrite.png`** — the brighter highlight variant, **pixel-for-pixel the same
  geometry**, differing only in green level.

Both are **463×6**, a proportional **1-bit** font. Glyphs occupy **rows 0–4** (five pixels tall —
hence "5point"); row 5 is blank spacing. Colours are exactly two green levels plus a dim outline
on black: normal core **(0,150,0)**, highlight core **(0,255,0)**, both edged with **(0,32,0)**;
the background is pure black.

**Character range: printable ASCII `0x20`–`0x7e`.** Space (`0x20`) is a blank leading cell, so the
atlas holds **94 ink glyphs, one per code `0x21`–`0x7e` laid left-to-right in code order** — i.e.
`glyph(code)` is the `(code − 0x21)`-th maximal run of inked columns. (The PLAN's shorthand
"`0123456789:;<=>?@A…z`" understates it: the set begins at `!` and runs through `~`, digits and
punctuation included.) **Letters are uppercase-only** — the `a`–`z` cells carry the `A`–`Z`
shapes. No glyph has a fully-blank interior column, so the run-per-code segmentation is exact
(94 runs = 94 codes, verified); glyph widths vary **1–6 px**, inter-glyph gaps **1–3 px**.

The reader segments the source rects at load (one per inked-column run, assigned from `0x21` up),
keys the black background to transparent (every non-black texel kept as-is, so a white modulate
reproduces the original green), and draws each glyph with `DrawTextureRectRegion` under a
**Nearest** filter (a pixel font). It inserts a **1 px tracking** gap after each glyph and treats
space / unrepresented codes as a **3 px** advance. Sizing routes through `HudMetrics` like every
other HUD element, so a splitscreen pane damps the text the same way the dials do.

## The gun aiming reticle (`impact_point.png`)

The aiming pipper is a single image in **`extracted/rimage/`** (the UI set, alongside the
`5pointhud` font — *not* the chapter archives): **`impact_point.png`**, a **32×32 RGBA**
sprite. It is a filled warm-white disc — core `(255,247,222)`, ring `(247,227,181)` — with a
**cross-shaped transparent notch** cut through the centre (the PLAN's "four tick marks around an
open centre"). The alpha channel is authored (transparent background, anti-aliased edges), so it
draws directly with no colour-keying — unlike the black-backed font atlas.

**Behaviour (remake E37, `src/Flight/ImpactReticle.cs`).** The reticle is **not pinned to screen
centre.** It marks the **projected ballistic impact point of the selected gun group's rounds at a
fixed convergence distance**, computed with the *same* `VELOCITY`/`ACCELERATION`/`GRAVITY`
integration `ProjectilePool` fires each round with (dropping only the random `CANNON_SPREAD` — the
pipper marks the cone centre), from the averaged muzzle pose. Because the rounds inherit the
plane's velocity — which lags the nose during a hard roll or pull — the reticle **trails the nose**
in a hard manoeuvre and sits on the rounds in steady flight (measured: `nose→reticle = 0.00°`
level, up to `~0.77°` below the nose toward the velocity vector at ~15° angle-of-attack; the small
angle is physics — bullets travel ~900 m/s against a ~55 m/s plane). It is a fixed-screen-size HUD
element (drawn via `Camera3D.UnprojectPosition` at `_Draw` time so it never lags the chase camera),
scaled through `HudMetrics` like every other widget, one per player pane.

⚠ **The convergence distance is a TUNE, not in the data.** `weapons.json` carries no
harmonisation/convergence field (player guns are `RANGE 1000`, `VELOCITY 750–1000`, no
`ACCELERATION`/`GRAVITY`). The remake uses **250 m** (`GunConvergenceDist` in `FlightController`),
pending an original-game playtest. Note the on-screen *trailing angle* is set by the
velocity/bullet-speed ratio and is essentially independent of this distance; the distance mainly
sets where a toed-in mount would harmonise and the pipper's parallax off screen-centre.

## Open question

Which world axis is compass **north**: the remake assumes **−Z** (consistent with the
map layout and motion), but the original's convention has not been verified in-game.
If it differs, the fix is the one heading line in `FlightController`.
