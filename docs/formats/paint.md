# Paint schemes, colours and decals

How the original colours an aircraft. Shipped skin textures are **not** the final paint: they
are neutral shading maps carrying region keys, and the engine writes the scheme's colours into
them at load time. The scheme itself (pattern + three colours + three decals) comes from
`vehicle.json` for AI aircraft, `ia.json` for instant-action aces, and a saved `.pln` file for
the player's customised plane.

This is why a naively-rendered Bloodhawk comes out **desaturated blue-gray** while the original
flies a **red** one: the remake draws the unpainted key texture.

Decoded 2026-07-19 by inspecting extracted data + the user's reference screenshots
(`OriginalScreenshots/CustomPlane Paint1 Bloodhawk.png` = the in-game paint UI,
`OriginalScreenshots/Kestrel.png` = a painted plane in flight). Not yet implemented.

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

Every player aircraft in `planes.zbd` carries three **16×16 placeholder** decal textures named
`<prefix>_noselogo`, `<prefix>_taillogo`, `<prefix>_winglogo` — one material each, on dedicated
polygons. Confirmed present for all eleven aircraft prefixes: `blo` `kes` `fur` `pea` `war`
`bri` `bal` `hel` `agyro` `fir` `dev`. The engine swaps the chosen 64×64 decal texture onto
these slots at load time; the shipped 16×16 image is a placeholder, not artwork to render.

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

**The palette is organised as contiguous index ramps, one per paint region.** On `blo_fin`,
palette indices **0–31** are a 32-step blue-gray ramp covering the entire fin body; a further
band covers the stripe. Colouring the image by palette-index band reproduces the paint regions
cleanly. This is the classic 2000-era paletted recolour: the engine writes the chosen colour's
32-step ramp into a reserved palette range, and every texel in that region recolours at once —
cheap, and it preserves the baked shading exactly.

Region hue is **not** a stable slot key across aircraft — the dominant body hue differs per
plane (Bloodhawk 210°, Kestrel 195°, Warhawk 15–30° orange, Devastator 0° red, Fury fully
neutral), so an implementation must key on the palette index range, not on hue. The Devastator
and Warhawk shipping already-warm suggests some skins were authored at their story colour.

## Saved custom planes

The player's customised aircraft are plain files (no archive) in the install's `Planes/`
directory, named by the plane's in-game name (`Blue Streak`, `Jumping Jane`), **204 bytes**
each. Little-endian 32-bit fields with the name as a NUL-padded string at offset 0x04; the
three paint colours sit at **0x68 as RGBA bytes** (`df 00 29 00` = `(223,0,41)`, `19 19 19 00`,
`ff ff ff 00`), preceded by what appear to be pattern and decal indices. Fully decoding this
record is only needed to *import* a player's saved planes, which nothing depends on yet.

## Open

- **Where the pattern table lives.** The pattern names (`player_fortune`, `blckswan`, `hughes`,
  …) appear only as *references* — in `vehicle.json` and `ia.json`. They are in no zrdr reader
  and no plaintext string in `crimson.exe`, `strings.dll`, `CrimsonSkies.gpr` or the resource
  files (`crimson.icd` is the SafeDisc-wrapped real image). The pattern presumably selects the
  palette-range→colour-slot mapping engine-side, and carries the default colours that
  `player_fortune` omits.
- **What a pattern actually varies.** Colours are explicit in the data, so a pattern is
  something else — likely which regions exist and which slot each maps to, possibly a stripe
  geometry/UV variant. Untested.
- **The "Shade" column.** The paint UI offers three *Colour* dropdowns and three *Shade*
  dropdowns; only three colours are stored per scheme. Shade may be a UI-side ramp-endpoint
  choice folded into the stored RGB, or a fourth stored field not yet identified.
- **Exact ramp ranges per texture.** Only `blo_fin`'s 0–31 body ramp has been read off
  directly. A general rule needs the same pass over every skin texture.

## Implementing this in the remake

Not started. The tractable path, in order:

1. Recover palette indices — mech3ax writes RGB PNGs, but `manifest.json` carries each
   texture's original 256-entry `Local` palette, and the RGB→index lookup is lossless
   (verified: 0 unmatched pixels on `blo_fin`).
2. Map palette index ranges → paint slots per skin texture (one pass, hand-checked against the
   reference screenshots).
3. Recolour at texture load: substitute each slot's ramp with the scheme colour at matching
   luminance, then upload. `PlaneBuilder` already owns the only path that loads plane skins.
4. Swap the three `*_noselogo`/`*_taillogo`/`*_winglogo` materials to the indexed decal
   textures.

Steps 1–3 give the red Bloodhawk; step 4 the squadron markings. Ideally driven by the same
`vehicle.json` def `PlaneStats` already parses (see [vehicle.md](vehicle.md)).
