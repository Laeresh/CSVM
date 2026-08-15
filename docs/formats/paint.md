# Paint schemes, colours and decals

How the original colours an aircraft. Shipped skin textures are **not** the final paint: they
are neutral shading maps carrying region keys, and the engine writes the scheme's colours into
them at load time. The scheme itself (pattern + three colours + three decals) comes from
`vehicle.json` for AI aircraft, `ia.json` for instant-action aces, and a saved `.pln` file for
the player's customised plane.

This is why a naively-rendered Bloodhawk comes out **desaturated blue-gray** while the original
flies a **red** one: the remake draws the unpainted key texture.

Extracted data and the user's reference screenshots show
(`OriginalScreenshots/CustomPlane Paint1 Bloodhawk.png` = the in-game paint UI,
`OriginalScreenshots/Kestrel.png` = a painted plane in flight). **Implemented 2026-07-20** —
see "Implementing this in the remake" at the bottom for what the remake actually does and
where it knowingly diverges.

## At a glance

This page is the current reference for its documented format family.

## At a glance

This page is the current reference for its documented format family.

## The scheme record

Seven fields, appearing under three different key prefixes depending on where the scheme lives.
Same schema each time:

| Field | Type | Meaning |
|---|---|---|
| `paint_pattern` | string | Named scheme (`hughes`, `blackhat`, `medusas`, …) |
| `paint_color1` .. `3` | RGB triple, **integer 0–255** | The three paint colours |
| `paint_decal1` .. `3` | integer | Index into the numbered decal set (below) |

Prefixes:

- **`paint_*`** — in `vehicle.json`, on a vehicle def. Carried through `kind_of` inheritance
  like every other property (see [vehicle.md](vehicle.md)).
- **`ace_*`** — in a mission's `ia.json` (`ace_pattern`, `ace_color1`, `ace_decal2`, …),
  alongside `ace_name` / `ace_plane` / `ace_stats`. The instant-action ace's personal scheme.
- The player's customised plane stores the same thing binary — see "Saved custom planes".

The colour triples are **always integer 0–255** here, never the normalized-float encoding
that `weather.json` mixes in (see the README's dual-encoding note) — no `>1` test needed.

### Schemes shipped in `vehicle.json`

Twelve named patterns across the AI roster. `devastator` and `wingman` carry a `paint_pattern`
with **no colours or decals** — the Fortune Hunters player scheme, whose colours evidently come
from elsewhere (the pattern's own defaults; see "Open").

| Pattern | Used by | color1 | color2 | color3 | decal1/2/3 |
|---|---|---|---|---|---|
| `player_fortune` | `devastator`, `wingman` | — | — | — | — |
| `hughes` | `habloodhawk`, `hakestrel`, `hafury` | 243,194,0 | 0,0,0 | 255,255,255 | 21/11/11 |
| `blackhat` | `bhatbrigand`, `bhatwarhawk`, `bhatgyro` | 177,130,66 | 119,74,43 | 66,39,15 | 21/2/2 |
| `blake` | `blakebloodhawk`, `blakepeace` | 149,163,195 | 89,114,159 | 233,228,240 | 21/3/3 |
| `british` | `britpeace`, `britbalmoral` | 177,130,66 | 48,47,39 | 255,255,255 | 21/4/4 |
| `blckswan` | `bsfury` | 23,23,21 | 48,47,39 | 196,193,186 | 21/5/5 |
| `cccp` | `rusdevastator` | 57,64,68 | 223,0,41 | 245,211,0 | 21/6/6 |
| `hollywd` | `hkfirebrand` | 108,102,169 | 67,36,121 | 212,202,225 | 21/10/9 |
| `medusas` | `medkestrel`, `medbrigand` | 95,125,143 | 41,14,21 | 141,137,93 | 21/14/14 |
| `sactrust` | `stihellhound` | 52,38,107 | 243,194,0 | 23,23,21 | 21/15/15 |
| `german` | `germanhellhound` | 96,115,126 | 0,0,0 | 48,47,39 | 21/13/13 |
| `studio` | `secfury`, `secgyro` | 32,90,167 | 255,255,255 | 0,0,0 | 21/11/11 |

Note `_2`/`_3`/`_5` suffixed defs (`blakepeace_2`, `bhatbrigand_5`) repeat their base def's
scheme verbatim — they are per-chapter roster duplicates, not scheme variants.

## The decal set

Each chapter's `texture.zbd` carries a **gapless numbered set 00–49** (identical in every
chapter, verified C1–C5), 64×64 with `Full` alpha, each with a half-size 32×32 `_1` LOD twin
(index 49 has none). **`paint_decalN` indexes this set by the texture's zero-padded numeric
prefix** — that is the whole mapping.

| Range | Contents |
|---|---|
| 00–20 | Squadron / militia / nation logos: `00bbomber_logo1`, `02blackhat_logo1`, `03blake_logo1`, `04british_logo1`, `05bswan_logo1`, `06cccp_logo1`, `07fhunter_logo1`, `09hknights_logo1`, `11hughes_logo1`, `13luftwaffe_logo1`, `14medusa_logo1`, `15sacredtrust_logo1`, `16colorado_logo1` … `20dixie_logo1` |
| 21–49 | Nose art: `21ace_star`, `22aintyerhoney`, `31fury`, `36joker`, `43skypirate`, `48gypsy`, `49ace_star2`, … |

The mapping cross-checks exactly against every pattern's name in the table above:
`blackhat`→2 = `02blackhat_logo1`, `blake`→3, `british`→4, `blckswan`→5 = `05bswan_logo1`,
`cccp`→6, `hughes`→11, `german`→13 = `13luftwaffe_logo1`, `medusas`→14, `sactrust`→15 =
`15sacredtrust_logo1`, `hollywd`→10/9 = the two `hknights` (Hollywood Knights) logos.

Every AI def uses `paint_decal1 = 21` (`21ace_star`), consistent with slot 1 = **nose** drawing
from the 21–49 nose-art range while slots 2/3 = **tail** and **wing** draw from the 00–20 logo
range. The paint UI's three dropdowns are labelled Nose / Tail / Wing in that order.

### Where a decal lands on the model

Every player aircraft in `planes.zbd` carries **16×16 placeholder** decal textures named
`<prefix>_noselogo`, `<prefix>_taillogo`, `<prefix>_winglogo` — one material each, on dedicated
polygons — across the eleven aircraft prefixes `blo` `kes` `fur` `pea` `war` `bri` `bal` `hel`
`agyro` `fir` `dev`. The engine swaps the chosen 64×64 decal texture onto these slots at load
time; the shipped 16×16 image is a placeholder, not artwork to render. Placeholder and decal
are both `alpha=Full`, so the swap does not disturb a renderer's alpha classification.

**Not every aircraft has all three.** The Firebrand ships no `fir_noselogo` (only
`fir_taillogo` / `fir_winglogo`) — corrected 2026-07-20, after keying the remake's aircraft
detection on the nose slot alone left the Firebrand unpainted. Detect on any of the three.

Index → texture is unambiguous: in both C1 and C5 exactly 50 textures match "two digits
followed by a non-digit, not an `_1` LOD twin", one per index 00–49, with no collisions
anywhere else in the archive (verified 2026-07-20).

## Base skins are unpainted key textures

The per-plane skin textures (`blo_wing`, `blo_fin`, `blo_fusalagetop`, `kes_fuselage`, …) are
**shading/luminance maps with paint-region keys**, not finished paint.

Evidence, on the Bloodhawk:

- `blo_fusalagetop`'s 234-entry palette is a near-monotone luminance ramp from `(0,0,0)` to
  `(189,186,197)` with a slight blue cast. Its dominant pixel colour is `(74,81,99)` — a
  desaturated blue-gray. There is no red anywhere in the file, yet the original's Fortune
  Hunters Bloodhawk is red.
- `blo_fin` and `blo_wing` split into **two visually coherent regions**: a blue-gray body and a
  yellow-olive sweeping stripe. Rendering the texture region-coloured reproduces exactly the
  body / swoosh-flash split visible on the painted plane in the reference screenshots — the
  white swoosh on the red Bloodhawk, the black-and-white wing striping on the red Kestrel.
- Neutral (unsaturated) areas are unpainted structure — cowl metal, canopy frames, panel lines.

### The palette is not organised into reserved ramps

An earlier reading of this page claimed the palette holds "contiguous index ramps, one per
paint region", based on `blo_fin`'s indices **0–31** being a clean 32-step blue-gray ramp
covering the fin body. **That generalisation is wrong**, and an implementation must not key
on palette index:

- On `blo_wing` — the same aircraft — the blue body region is scattered across the palette
  (indices 0, 6–7, 9, 13–14, 16–17, 20, 24–25, 28–31, …), not banded.
- Every plane palette inspected is simply **sorted by luminance**, the ordinary output of a
  median-cut/octree quantizer. `blo_fin`'s tidy 0–31 run is a coincidence of that sort: the
  blue body happens to be the darkest thing in that particular texture.
- `manifest.json` carries only `Local` palettes; there are **no `global_palettes`** in any
  chapter's `texture.zbd` or in `rimage.zbd` (all eight chapters checked), so there is no
  shared paint palette either.

So the engine cannot be doing an index-range palette swap. Whatever table it uses to decide
"this texel is paint slot 2" is keyed on something else and is not in the ZBD data.

> **Found, 2026-07-20 — it is in `crimson.rof`.** The region table exists after all, in the UI
> resource archive rather than the ZBD set: each `ASSETS/GRAPHICS/<PATTERN>/<SKIN>.BM` carries
> a greyscale shading map plus **three 8-bit per-pixel weight masks, one per paint colour slot,
> summing to 255**. That is a direct answer to "how the engine identifies a region", and it is
> per *pattern* — which also answers "what a pattern actually varies" below. Full decode in
> [rof.md](rof.md); `ExtractRof.ps1` writes each mask out as `<SKIN>_mask.png` (R/G/B = slots
> 1/2/3). **The remake was reworked onto these masks on 2026-07-20** — the hue-window sections
> below describe the approach they replaced; see "Superseded" at the bottom.

### What the regions actually look like

Region hue is **not** a stable slot key across aircraft — each aircraft's skins are authored
around **one dominant body hue**, and it differs per plane. Measured over every skin of each
aircraft (saturated-texel hue histogram, C1; the skins are byte-identical in all eight
chapters, verified, so this is chapter-independent):

| Prefix | Aircraft | Body hue | Secondary | Saturated |
|---|---|---|---|---|
| `blo` | Bloodhawk | 217° blue | 58° olive swoosh | 54% |
| `kes` | Kestrel | 199° blue | 58° olive flap | 43% |
| `pea` | Peacemaker | 219° blue | 55° olive | 44% |
| `bri` | Brigand | 200° blue | — | 43% |
| `war` | Warhawk | 31° orange | — | 94% |
| `bal` | Balmoral | 35° orange | — | 80% |
| `hel` | Hellhound | 250° purple | 48° amber | 83% |
| `fir` | Firebrand | 252° purple (wide, 230–270°) | — | 90% |
| `dev` | Devastator | 1° red | — | 52% |
| `agyro` | Autogyro | 58° olive | — | 88% |
| `fur` | Fury | **none** | — | 3% |

The Devastator shipping red and the Warhawk orange suggests some skins were authored at their
story colour. **The Fury is the outlier that disproves any pure hue-keying theory**: its skins
are a near-black greyscale shading map (median value 0.00 on `fur_wing` and `fur_fusalage1`)
with no saturated texels at all, yet `secfury` flies a studio-blue one in the original. So the
engine's region table cannot be derived from the texture's colours alone — it is external data
we do not have.

Note the yellow/olive secondary hue is partly **propeller spinners**, not paint: `dev_spinner`,
`fir_spinner`, `hel_spinner` and `pea_spinner` are ~100% saturated at 50–60°.

## Saved custom planes

The player's customised aircraft are plain files (no archive) in the install's `Planes/`
directory, named by the plane's in-game name (`Blue Streak`, `Jumping Jane`), **204 bytes**
each. Little-endian 32-bit fields with the name as a NUL-padded string at offset 0x04; the
three paint colours sit at **0x68 as RGBA bytes** (`df 00 29 00` = `(223,0,41)`, `19 19 19 00`,
`ff ff ff 00`), preceded by what appear to be pattern and decal indices. Fully decoding this
record is only needed to *import* a player's saved planes, which nothing depends on yet.

## Implementing this in the remake

**Reworked onto the original's own masks 2026-07-20**, replacing the hue-window workaround
below. `src/Mech3/PatternLibrary.cs` reads the `.BM` region masks out of the extracted UI
archive, `src/Mech3/PlanePainter.cs` composites them, `src/Mech3/PaintScheme.cs` holds the
record, and the result reaches the renderer through a `SceneBuilder` texture-substitution hook
that `PlaneBuilder` drives per aircraft.

Per texel, exactly the formula in [rof.md](rof.md):

```
shaded_paint = shading * (w1*colour1 + w2*colour2 + w3*colour3) / 255
```

then the pattern's overlay composited over it (4th channel as alpha). Two details are
load-bearing: the shading map is the `.BM`'s own base plane, **not** the ZBD skin of the same
name; and `.BM` rows are stored **bottom-up**, so each source row is read from `h-1-y` (see
[rof.md](rof.md) — without the flip every livery is mirrored along V, which is how the bug
first surfaced in-game). Parts a pattern ships no `.BM`
for keep their shipped ZBD texture; the ZBD skin's alpha channel is carried onto the composite
where the two agree on dimensions, so `SceneBuilder`'s blend-vs-scissor choice stays valid.

Decals are unchanged: `<prefix>_noselogo`/`_taillogo`/`_winglogo` swap for the numbered decal
via `TextureArchive.FindByDecalIndex`.

### Patterns are per aircraft

A pattern covers only the planes it ships skins for, which is why the original's paint UI
offers a different list per aircraft. Measured over the extraction:

| Aircraft | Patterns |
|---|---|
| Bloodhawk `blo` | FORTUNE, BLAKE, HUGHES |
| Kestrel `kes` | FORTUNE, HUGHES, MEDUSAS |
| Fury `fur` | FORTUNE, BLCKSWAN, HUGHES, STUDIO |
| Peacemaker `pea` | FORTUNE, BLAKE, BRITISH, BROADWAY |
| Warhawk `war` | FORTUNE, BLACKHAT, SACTRUST |
| Brigand `bri` | FORTUNE, BLACKHAT, MEDUSAS |
| Balmoral `bal` | FORTUNE, BRITISH |
| Hellhound `hel` | FORTUNE, GERMAN, SACTRUST |
| Autogyro `agyro` | FORTUNE, BLACKHAT, ITSTAXI, STUDIO |
| Firebrand `fir` | FORTUNE, HOLLYWD |
| Devastator `dev` | FORTUNE, CCCP |

`FORTUNE` is the only pattern covering all eleven. The four the Fury carries match the user's
four `CustomPlane Paint Fury Pattern *.png` reference screenshots exactly, which is the
confirmation that this list *is* what the paint UI offers.

`PatternLibrary.PatternsFor(prefix)` is that list; random liveries draw from it, the livery
lab steps it, and `--paint=` validates against it (naming a pattern the aircraft lacks logs
the aircraft's own set and paints decals only).

### Slot order, confirmed

Slot 1 = identity/body, slot 2 = dark trim, slot 3 = light trim — the order the masks ship in
matches `paint_color1..3`. Settled by rendering the Fortune Hunters Bloodhawk with all three
plausible assignments against `CustomPlane Paint1 Bloodhawk.png`: only *(red, black, white)*
puts black on the outer wing panels and the white swoosh between them, as the reference shows.
White in both trims loses the black wing entirely; black in slot 3 paints the swoosh instead
of the panel.

That also pins down **`player_fortune`'s missing colours** as `223,0,41 / 0,0,0 / 255,255,255`
— the same shape every shipped scheme has (`hughes` is yellow/black/white). The red comes from
the paint UI's swatch and a saved `.pln` at 0x68; the trims are read off the reference. Still
an inference from artwork, not a value found in a file.

### What the rework fixed

Every divergence the hue-window version carried was a consequence of guessing regions from
colour, and all of them are gone:

- **Achromatic regions paint.** The Bloodhawk's outer wing panels are black under Fortune
  Hunters, as in the original — a hue window could not see a region with no hue.
- **The Fury paints.** Its ZBD skin is featureless near-black, but its `.BM` masks are
  complete; all four of its patterns render correctly, including the Studio Security
  blue-and-white checkerboard.
- **Slot order is data, not area rank.**
- **Borders come antialiased** in the masks themselves, so the soft hue falloff that existed
  only to stop borders speckling is gone.
- **It is faster.** A flat multiply-add per texel replaced per-texel HSV conversion plus a
  percentile pass; painting four aircraft is now within measurement noise of not painting.

### Superseded — the hue-window approach (2026-07-19 → 2026-07-20)

Before the masks were found, the remake inferred regions from a hand-authored per-aircraft
table of hue windows, keyed on the "What the regions actually look like" table above. It
produced a correct-looking red Bloodhawk and twelve distinguishable liveries, but could not
express a colourless region, could not paint the Fury at all, and had to guess slot order. The
table and its measurements are kept above because they remain a true description of the ZBD
skins; the code is gone.

## Open

- ~~**The "Shade" column.**~~ *Answered 2026-07-20 (user): Shade is simply the **brightness** of
  the chosen colour.* The UI's Colour dropdown picks the hue family and Shade picks how light
  or dark it is; their product is the single RGB that ends up in `paint_colorN`. There is no
  fourth stored field and nothing extra to model — a scheme really is three colours, and the
  masks giving each slot exactly one colour is consistent, not a contradiction. The remake
  exposes **RGB sliders** instead of the original's two dropdowns, which spans the same space
  and more, so any original livery is reachable by matching its colour directly.

  This also **independently confirms `player_fortune`'s colours**, which had been derived only
  by rendering. `CustomPlane Paint1 Bloodhawk.png` shows Colour/Shade of *red/red*,
  *white/**black***, *white/white* — resolving to **red, black, white**, exactly the triple
  that the three-way render test singled out. Two unrelated routes, same answer.
- **Overlay channel order** is still unconfirmed (see rof.md); treating the 4th channel as
  alpha over RGB renders correctly on the content inspected, which is greyscale and so cannot
  distinguish RGBA from BGRA.
- **`player_fortune`'s colours** are inferred from artwork (above), not located in a file.
- **`BROADWAY` and `ITSTAXI`** ship masks (Peacemaker and Autogyro) but no `paint_pattern`
  names them, so they have no canonical colours; the remake offers them with whatever colours
  are current.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
