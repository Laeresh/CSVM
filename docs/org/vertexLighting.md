# What decides whether a surface is lit by `SUNLIGHT`, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function or the shipped byte it
came from. No decompiler output is reproduced; the addresses are given so any claim can be
re-checked at source.

This page answers one question: **what in the original decides whether a scene node's material is
modulated by the mission `SUNLIGHT`, and what does it key on?** The sun's own orientation, colour
and the zone apply that writes them are [`weather.md`](weather.md); the texture header this page
reads two bits of is [`textures.md`](textures.md); the model and material fields it reads are
[`../formats/gamez.md`](../formats/gamez.md).

## The answer in one paragraph

`SUNLIGHT` is not a separate path. The `sunlight` gamez node is an ordinary Light-class node that
enters the same 128-slot light array as every `LIGHT_STATE` point light, and its one distinction is
a **directional** flag that exempts it from the range test. Its contribution is computed **per
vertex, at draw time, inside the polygon loop** (`FUN_005524d0`, `zrender\zrndr_draw.c`), not baked
at load. **Two per-surface tests decide whether a given polygon receives it**, and they are checked
in this order:

1. **Per model** (`FUN_00551d90`): bit 0 of the model record's flag word at `+0x08`, the flag the
   extractor spells `lighting`. Clear, and no light in the array is even considered for that model.
2. **Per texture** (`FUN_005524d0`, textured branch): bit `0x02` of the texture object's storage
   flags byte at `+0x09`, which is set for exactly the textures carrying an alpha channel. Set, and
   the whole per-vertex lighting evaluation is skipped and the polygon is submitted through the
   unlit path.

**There is no material-level, `soil`-id-level or node-level test anywhere on this path.** The
material record is read for its texture pointer, its colour and its cycle state and for nothing
else; `soil` never reaches the lighting code.

## Function map

| Address | Role |
|---|---|
| `FUN_00566be0` | `zmodel\gmod_light.c`: gathers the scene's active lights into the 128-slot array, and caches the directional light's colour into the globals the material setup reads |
| `FUN_00551d90` | The **per-model** decision: builds a bitmask of "is this model lit / fogged / band-fogged" from the model's own flag word |
| `FUN_005524d0` | `zrender\zrndr_draw.c`: the model draw. Calls `FUN_00551d90` once, then walks the polygons and applies the **per-texture** test inside the textured branch |
| `FUN_00566e00` | Per model, selects which of the gathered lights reach it and writes each one's scalar intensity into the accumulator |
| `FUN_00567150` | The per-vertex light evaluation for a **textured** polygon: `N·L` against each selected light's direction, plus its ambient |
| `FUN_00569a10` | The same for an untextured (`Colored`) polygon |
| `FUN_004dbf70` / `FUN_004dbff0` | `zclass\Light.c`: make a light **directional** (`flags & ~0x10 \| 0x08`) or **point** (`flags & ~0x08 \| 0x10`). The two are mutually exclusive |
| `FUN_004dbdb0` / `FUN_004dbce0` | `zclass\Light.c`: write a light's diffuse and ambient scalars and pre-multiply them into its colour triples |
| `FUN_00472ea0` | The zone apply, which calls all three of the above on the `sunlight` node |
| `FUN_0054d9c0` | The render-module init: every global this page names, and the `GfxFlags_SW` lookup |
| `FUN_0054e040` | The `SetPaletteShading` GameGen command's setter |

## The light array (`FUN_00566be0`)

The engine walks the scene's light nodes once and copies those carrying node-flag bit `0x04` into a
**128-slot** array (`MAX_LIGHTS`; overflow raises a `gmod_light.c` error and truncates). Each slot
holds the light's class-data pointer and its node. The index of the **directional** light, if the
scene has one, is remembered separately, and its diffuse triple is cached into the globals the
textured material setup writes as the surface's light colour.

A Light node's class data is a `0x100`-byte block hung off `node+0x38` (`FUN_004dba40` allocates and
defaults it). The fields this page uses:

| Offset | Meaning | Default |
|---|---|---|
| `+0x90`…`+0x98` | direction vector, dotted against the vertex normal | |
| `+0x9c` | diffuse scalar (`SUNLIGHT_COLOR_DIFFUSE`) | 0 |
| `+0xa0` | ambient scalar (`SUNLIGHT_COLOR_AMBIENT`) | 1.0 |
| `+0xa4`…`+0xac` | diffuse colour | 1,1,1 |
| `+0xb0`…`+0xb8` | ambient colour | 1,1,1 |
| `+0xe0` | flags: `0x08` directional, `0x10` point, `0x800` enabled | point, enabled |
| `+0xe4` / `+0xe8` | near / far range | 32 / 64 |
| `+0xf4` | attenuation reciprocal over that span | 1/32 |

⚠ **The sun is one of these and nothing more.** `FUN_00472ea0` (the zone apply) calls
`FUN_004dbf70` to mark the `sunlight` node directional, then `FUN_004dbdb0` and `FUN_004dbce0` to
write the mission's diffuse and ambient. A directional light skips the range test in `FUN_00566e00`
and always contributes; that exemption is the only special case the sun gets. Any code that treats
`SUNLIGHT` as a separate global scalar applied outside the light array is describing the remake, not
the original.

## Gate 1: the model's `lighting` flag (`FUN_00551d90`)

`FUN_005524d0` calls `FUN_00551d90` once per model, before the polygon walk, and it returns a
bitmask that the polygon walk then keys on. The mask is built from three independent tests, each of
which is a global armed-or-not check ANDed with one bit of the model record's flag word at `+0x08`:

| Model flag bit | Global | Sets mask bit | Meaning |
|---|---|---|---|
| `0x01` | `GfxFlags_SW` bit 0, and "the array holds at least one light" | `0x02` | **lighting** |
| `0x02` | the distance-fog mode | `0x01` | distance fog |
| `0x04` | the band/altitude fog enable | `0x800` | the second fog term |

Only when the model's bit `0x01` is set does the engine call `FUN_00566e00` to work out which lights
reach this model. With it clear, nothing from the array (the sun included) is written into the
accumulator, so no polygon of that model can be modulated.

The bit is the one the extractor names `lighting`, first in the model `flags` object; the bit
immediately above it gates the distance fog and is the one the extractor names `fog`, second in the
same object. `../formats/gamez.md` already records the data side of both, install-wide populations
included: **3,003 models are `lighting: false`** and 225 are `fog: false`.

⚠ **A model's flag is not the whole story, in both directions.** `FUN_00551d90` sets the mask's
ambient bit from a global that the model flag does not gate, so an unlit model still enters
`FUN_00567150` when that global is armed. It is 0 by default (`FUN_0054d9c0`) and no shipped data
arms it, so in practice an unlit model is unlit. In the other direction, a model that passes this
gate can still be exempted per polygon by gate 2.

⚠ **`GfxFlags_SW` bit 0 is a global kill switch.** `FUN_0054d9c0` resolves `GfxFlags_SW` as a
GameGen variable and falls back to all-bits-set when it is absent, so lighting is on unless the
boot data turns it off.

## Gate 2: the texture's alpha bit (`FUN_005524d0`)

Inside the polygon walk, a polygon's material is classified by its own bit `0x100`: clear is a
`Colored` material and set is a `Textured` one. The two branches differ in exactly the way that
matters here.

- **`Colored`** polygons carry no exemption. They go straight to `FUN_00569a10` whenever the
  model-level mask allows it.
- **`Textured`** polygons dereference the material's texture pointer at `material+0x10` (the same
  field `FUN_0055b1a0` rewrites each frame for a cycling material) through the gamez texture
  directory record to the image object, and test **bit `0x02` of the storage flags byte at
  `+0x09`**:
  - **clear**: the accumulator is zeroed, `FUN_00567150` evaluates every selected light per vertex,
    and the polygon is submitted through the lit path.
  - **set**: the lighting evaluation is skipped entirely and the polygon is submitted through the
    unlit path, which takes no per-vertex light array at all. The two fog terms are skipped with it,
    because they are applied in the same block.

⚠ **This is the per-surface exemption, and it keys on the texture, not on the material and not on
the `soil` id.** Two polygons of the same model, same `soil`, same priority, one skinned with an
alpha texture and one without, are lit differently.

### What bit `0x02` is

[`textures.md`](textures.md) lists `+0x09` bits `0x02` and `0x04` as "not traced; part of the
extractor's alpha class". They are now pinned, from the shipped `texture.zbd` headers read against
the extractor's own `alpha` field:

| Bit | Meaning |
|---|---|
| `0x02` | the texture has an alpha channel (the extractor's `Simple` and `Full`) |
| `0x04` | the texture has none (the extractor's `None`) |
| `0x08` | a separate alpha plane follows the pixel data (`Full` rather than `Simple`) |

The two are complementary in every shipped header: `0xa5` is a no-alpha texture, `0xab` a
full-alpha one, `0xa3` a simple-alpha one. Population of bit `0x02`, per chapter's `texture.zbd`,
counted directly off the headers and matching the extractor's `alpha` classes exactly:

| Chapter | Textures | Alpha (unlit) | of which `Full` / `Simple` |
|---|---|---|---|
| C1 | 881 | 434 | 431 / 3 |
| C1B | 667 | 368 | 367 / 1 |
| C1C | 593 | 332 | 331 / 1 |
| C2 | 820 | 380 | 379 / 1 |
| C2B | 601 | 323 | 322 / 1 |
| C3 | 732 | 363 | 361 / 2 |
| C4 | 935 | 415 | 405 / 10 |
| C5 | 896 | 421 | 418 / 3 |

⚠ **The count is of the archive, not of the world.** Half of each archive is the hand-authored
`_1`/`_2` mip levels and the cockpit, plane and effect art. Joined against C5's gamez materials,
**234 of 583 textured world materials** carry the bit.

### What the exempted set looks like

The C5 join is the readable statement of the rule. Exempted (bit set, drawn unlit): the flares and
glows (`poleflare`, `light_flare`, `bigflare01/02`, `lflare1`–`4`, `oil_liteflare`, `beflare5`),
the lit-window and signage overlays (`buildingspotlighted`, `nypd`, `clock`, `fadedsign01`–`03`,
`lightpole`, `lite_out`, `bliteon`/`bliteoff`, `traffic_sign1`), the fog gradients (`z3_foggrad`,
`foggrad8x64`), the baked shadow decals (`bldgshadow`, `agyro_shadow`, `firetruck_shadow`), the
cloud sprites (`cloud1`, `cloud2`), fire, smoke, tracers, muzzle flashes, splashes, the cockpit
gauges, the cable and scaffold cutouts, and the plane and zeppelin logo decals.

Not exempted (bit clear, lit): the city block skins `cblock1`–`7`, the building skins `bldg1`–`4`
and `bldgtrim1`, the water `wtr00000`–`wtr00015`, and the terrain and cliff families.

## Where CSVM stands

The remake renders the world fullbright and dims it by one `csky_world_light` scalar, the
data-driven collapse of the original's per-vertex `N·L` (see [`weather.md`](weather.md)). Gate 1 is
reproduced: `SceneBuilder` reads the model's `lighting` flag and builds an unlit shader variant per
model. **Gate 2 is not reproduced at all.** The per-model `lit` decision is applied to every
surface of the model, overlay passes included, and nothing in the pipeline reads the texture
storage-flags byte. `TextureArchive` classifies alpha from the decoded pixels rather than from the
header, and the deployed texture tree is PNGs without the extractor's `manifest.json`, so the bit is
not currently available at build time.

Reproducing gate 2 means: carrying the extractor's `alpha` field (or the raw storage byte) through
to the material build, and treating `alpha != None` on a textured surface as "does not take
`csky_world_light`", independently of the model's own flag. Note that the PNG alpha channel is not
a substitute for the field: it distinguishes `Full` from `None` but loses the 1 to 10 `Simple`
textures per chapter, which carry the bit too.

The field is available where the extraction output is: each chapter's `texture.zip` and its
unpacked sibling carry a `manifest.json` listing every texture's `alpha` as `None` / `Simple` /
`Full`. Nothing in CSVM reads that file, so plumbing gate 2 is a reader to write, not a decode to
find.

### What the model's `lighting` bit selects, and what it does not

The bit is a lighting exemption, and the shipped data uses it for two disjoint reasons: a surface
that is self-luminous, and a surface authored at a fixed brightness for some other reason. Counted
over each chapter's placed models, joined to the textures they draw:

| Chapter | `lighting: false` models | of which the original's camera-facing light-source class |
|---|---|---|
| C5 | 232 of 2,851 | 99 models, 448 nodes |
| C1 | 421 of 2,237 | 124 models, 760 nodes |

The light-source class (model type `Facade`, facade mode `Spherical`, minus the cloud sprites) holds
only flares, lamps, railway signals, muzzle tips, explosion sprites and the moon in both chapters.
The remainder of the `lighting: false` population does not: it also holds C1's cloud deck (144
nodes of `cloudlayer`), both skydome textures, the baked ground-shadow decals (`lkshad3`, `lkshad6`,
`sootstn`), the tree and bush cards, hangar interior skins and the zeppelin's passenger figures,
beside genuinely self-lit signs (`bowlsign`, the `hotel_*` letters, `rasign`, `lite_out`,
`flaglite01`).

⚠ **Neither gate identifies "the surfaces that emit light".** Gate 1's population is half
non-luminous, as above; gate 2's exempted set (listed earlier on this page) mixes the lit-window and
signage overlays with the baked shadow decals, the fog gradients and the cloud sprites. C5's lit
windows are also not separable surfaces at all where they matter most: they are bright texels inside
the `lighting: true` wall textures, so nothing per-surface can hold them at their authored
brightness.

⚠ **The rule cuts both ways and is not a brightness knob.** It exempts a large, named family, and
the surfaces the world's calibration was measured on (terrain, building skins, water) are *not* in
it. Applying it cannot raise a `cblock` or a `wtr` surface, and anything that does is a different
mechanism.
