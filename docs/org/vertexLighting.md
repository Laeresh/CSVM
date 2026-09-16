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
vertex, at draw time, inside the polygon loop**, not baked at load.

⚠ **The engine ships two model draws, and only one of them runs on a 3D card.** `FUN_0054dca0`
tests the hardware flag `DAT_009be708`: clear, it keeps the software draw `FUN_005524d0`
(`zrender\zrndr_draw.c`, a palette span rasteriser); set, it installs the hardware draw
`FUN_00554550`, which submits through Direct3D. Every retail capture this project measures against
is the hardware draw. The two agree on the per-model test and disagree on the per-texture one:

1. **Per model** (`FUN_00551d90` in software, `FUN_00552020` in hardware): bit 0 of the model
   record's flag word at `+0x08`, the flag the extractor spells `lighting`. Clear, and no light in
   the array is even considered for that model.
2. **Per texture, software draw only** (`FUN_005524d0`, textured branch): bit `0x02` of the texture
   object's storage flags byte at `+0x09`, set for exactly the textures carrying an alpha channel,
   skips the per-vertex lighting evaluation when palette shading is off. **The hardware draw has no
   per-texture test.** `FUN_00554550` lights an alpha-textured polygon exactly as it lights an
   opaque one; the alpha bit there decides sorting and blending and never touches the colour.

**There is no material-level, `soil`-id-level or node-level test anywhere on either path.** The
material record is read for its texture pointer, its colour and its cycle state and for nothing
else; `soil` never reaches the lighting code.

## Function map

| Address | Role |
|---|---|
| `FUN_0054dca0` | Render start-up: installs `FUN_00554550` as the model draw when `DAT_009be708` (hardware) is set, else keeps `FUN_005524d0` and arms the palette-shading global `DAT_00a07000` |
| `FUN_00566be0` | `zmodel\gmod_light.c`: gathers the scene's active lights into the 128-slot array, and caches the directional light's colour into the globals the software material setup reads |
| `FUN_00551d90` | Software draw, the **per-model** decision: builds a bitmask of "is this model lit / fogged / band-fogged" from the model's own flag word |
| `FUN_005524d0` | `zrender\zrndr_draw.c`: the **software** model draw. Calls `FUN_00551d90` once, then walks the polygons and applies the **per-texture** test inside the textured branch |
| `FUN_00554550` | The **hardware** model draw. Calls `FUN_00552020` once, seeds and evaluates the per-vertex light for every polygon the model mask allows, multiplies it onto the authored vertex colours and packs the result into the Direct3D vertex diffuse |
| `FUN_00552020` | Hardware draw, the per-model decision: the same three flag-word bits, plus `FUN_00567060` asking whether any gathered light reaches the model's bound sphere |
| `FUN_00568830` | Hardware draw: seeds every vertex's light accumulator to 1.0 before the evaluation |
| `FUN_005688a0` | Hardware draw: the per-vertex light evaluation. Adds each directional light's `ambient + diffuse × max(N·L, 0)` times its colour, minus one, into the accumulator. Reads positions, normals, the accumulator and the polygon flags; never the texture |
| `FUN_005a0e00` | Direct3D device set-up: stage 0 colour op MODULATE (texture × diffuse), alpha op SELECTARG1 (texture alpha), and `SPECULARENABLE` off for the life of the process |
| `FUN_005a2f70` | The fog enable, and the only kind of `SetLightState` the engine ever issues |
| `FUN_005a4430` | The model batch drain: `DrawPrimitive` with FVF `0x1c4`, the pre-transformed vertex every draw in the layer submits |
| `0x005a6160` | The hardware transparent-queue drain (no function boundary in the database): per polygon sets shade mode, alpha blend, z-write off, the alpha op and the destination blend, then draws with FVF `0x1c4`, which carries diffuse. Never touches the colour op |
| `FUN_00566e00` | Per model, selects which of the gathered lights reach it and writes each one's scalar intensity into the accumulator |
| `FUN_00567150` | The per-vertex light evaluation for a **textured** polygon: `N·L` against each selected light's direction, plus its ambient |
| `FUN_00569a10` | The same for an untextured (`Colored`) polygon |
| `FUN_004dbf70` / `FUN_004dbff0` | `zclass\Light.c`: make a light **directional** (`flags & ~0x10 \| 0x08`) or **point** (`flags & ~0x08 \| 0x10`). The two are mutually exclusive |
| `FUN_004dbdb0` / `FUN_004dbce0` | `zclass\Light.c`: write a light's diffuse and ambient scalars and pre-multiply them into its colour triples |
| `FUN_00472ea0` | The zone apply, which calls all three of the above on the `sunlight` node |
| `FUN_0054d9c0` | The render-module init: every global this page names, and the `GfxFlags_SW` lookup |
| `FUN_0054e040` | The `SetPaletteShading` GameGen command's setter |
| `FUN_00553700` | The model-type dispatch every draw opens with: `Default` draws as authored, `Facade` gets a camera-facing basis, `Points` draws its vertices as screen points |
| `FUN_00552180` | The mean of a model's polygon vertices, the pivot the centroid-rotating facades turn about |
| `FUN_0053cba0` | Transforms a model's normals by the current matrix's rotation, after the facade basis is installed; `FUN_00422690` renormalizes each one |
| `FUN_00568790` | Per model, sorts the lights that reach it into the directional, the `0x04`-flagged and the remaining lists `FUN_005688a0` walks |
| `FUN_0054e070` | The `SetVertexShading` GameGen setter (`DAT_00a05f08`), which boot sets to the hardware flag |
| `FUN_005669b0` / `FUN_00566ae0` | The `DirectedLightFluctuate` enable, and the per-frame scroll of its lookup offsets |

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
gate can still be exempted per polygon by gate 2 in the software draw, and by nothing in the
hardware draw.

⚠ **`GfxFlags_SW` bit 0 is a global kill switch.** `FUN_0054d9c0` resolves `GfxFlags_SW` as a
GameGen variable and falls back to all-bits-set when it is absent, so lighting is on unless the
boot data turns it off.

## Gate 2: the texture's alpha bit, software draw only (`FUN_005524d0`)

⚠ **This gate exists in the software draw and nowhere else, and CSVM does not reproduce it.** A
renderer that does exempts exactly the alpha-textured surfaces from the world light, and at C1B's
0.426 world light a blended coastline sheet at full brightness over dark water reads as a hard
bright band, where the original's hardware draw dims it with the water. The hardware rule is in
the next section; this one is kept as the record of what the software draw does. Even there it is
narrower than it looks: the skip applies only while the palette-shading
global `DAT_00a07000` is 0, and `FUN_0054dca0` sets it to 1 whenever the software draw is chosen
(`FUN_0054e040`, the `SetPaletteShading` setter, is the only other writer), so an alpha polygon
goes through the lighting block in software as well, with a flat-shade flag instead of a per-vertex
one. What the palette shade tables then make of it is not traced.

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
**237 of 583 textured world materials** carry the bit off `texture.zbd`, and 239 off the
`rtexture14` tier the engine actually loads, which classes the two gauge needles as alpha where the
base archive does not. The per-chapter join is in "Where CSVM stands" below.

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

## The hardware draw: one gate, and the colour it writes (`FUN_00554550`)

`FUN_00554550` calls `FUN_00552020` once per model for the same mask `FUN_00551d90` builds, with
one addition: the lighting bit also needs `FUN_00567060` to find at least one gathered light
reaching the model's bound sphere. Then, for every polygon the mask allows:

- `FUN_00568830` seeds each vertex's light accumulator to 1.0.
- `FUN_005688a0` adds, per directional light, `(ambient + diffuse × max(N·L, 0)) × colour − 1`, so a
  scene with the sun alone leaves exactly `ambient + diffuse × max(N·L, 0)` times the sun colour.
  `N` is the vertex normal when the polygon carries them and the face normal otherwise. The
  function reads positions, normals, the accumulator and the polygon flags; **it never reads the
  texture**.
- The draw multiplies the polygon's authored per-vertex colours (the polygon record's colour
  array, copied to the colour buffer's `+0x14` slot) by the accumulator, clamps to 0..255, and
  packs the result into the Direct3D vertex diffuse. A polygon no light reached gets the authored
  colours times 1.0.
- The device runs stage 0 as MODULATE of texture by diffuse with the alpha taken from the texture
  (`FUN_005a0e00`), so the diffuse above is what dims the texel. Only `FUN_005a7130`, the
  multitexture path, ever changes the colour op.

The texture alpha bit (`+0x09 & 0x02`) is tested four times in this draw, and every use is
routing: it sends the polygon through the sorted transparent queue (`0x005a6160`) with alpha
blending on and z-write off, and it excludes the polygon from the projected-shadow and lightmap
passes. The two globals the submit reads beside it, the opacity `DAT_00a06f98` (default 1.0, set by
`FUN_0054e0e0`) and the overwrite flag `DAT_00a06f94`, are routing too and never a brightness term.

## Specular: the device turns it off and no material exists to turn it back on

⚠ **Nothing in the original carries a specular term, on an aircraft or on anything else.** Four
independent readings agree, and the first is the whole answer on its own:

1. **The device disables it at creation and nothing re-enables it.** `FUN_005a0e00` calls
   `SetRenderState(D3DRENDERSTATE_SPECULARENABLE, 0)` at `0x005a12f8`-`0x005a12fc` (the call
   reports through `zvid_ddd3d.c` line 523 on failure), and that is the **only** write of render
   state `0x1d` in the shipped image: one `push 0x1d` in the whole 0x0059e000-0x005ab000 video
   layer, against 110 `SetRenderState` call sites.
2. **The D3D lighting pipeline never runs, so a material could not apply anyway.** Every submit in
   the layer draws with FVF `0x1c4`, which is `XYZRHW | DIFFUSE | SPECULAR | TEX1` (23 sites; the
   model batch drains through `FUN_005a4430` at `0x005a4530`). `XYZRHW` is a pre-transformed,
   pre-lit vertex, so the device's own transform and lighting stages are bypassed entirely.
3. **No material is ever set.** The interface is `IDirect3DDevice3`, where a material is installed
   with `SetLightState(D3DLIGHTSTATE_MATERIAL, handle)` at vtable `+0x60`. Every `+0x60` call in
   the video layer passes a fog state instead, `4` `FOGMODE` (`FUN_005a2f70`), `5` `FOGSTART`
   (`0x005a2fe5`, `0x005a30a4`) and `6` `FOGEND` (`0x005a30ba`); light state `1` appears nowhere.
4. **The specular slot the FVF declares is a fog carrier, not a colour.** The vertex is 32 bytes,
   diffuse at `+0x10` and specular at `+0x14`, and every writer stores the constant `0xFF000000`
   into `+0x14`: the model polygon packer at `0x005a3ff8` (one instruction after it assembles the
   diffuse from the per-vertex colour bytes at `0x005a3ff1`), and the sprite, clutter and UI
   packers at `0x00558da7`, `0x0056baca`, `0x005a5b34` and `0x005c1432`. RGB zero, alpha 255,
   which is the no-fog end of the fog-factor slot pre-transformed vertices carry.

The per-vertex evaluation above agrees: `FUN_005688a0` accumulates `ambient + diffuse × max(N·L, 0)`
per light and has no half-vector, no view direction and no exponent. An aircraft in the original is
its texture times a Lambert term, and nothing else.

### What CSVM does with that

The remake shades aircraft through Godot's PBR material, which has no zero-gloss setting that also
keeps the diffuse response, so the faithful reading is a floor rather than a value to copy. The
shaded arm of `SceneBuilder.GetBiasShader` therefore carries `SPECULAR = 0.25` (`AircraftSpecular`)
at roughness 0.85 and metallic 0.0, which is the remake's own choice: the value the user picked by
eye off a sweep against `OriginalScreenshots/Fury from above.png`, low enough that sunlight reads as
a sheen instead of gloss. The decode is the record behind it, not its source.

## Facades: the same gate, a different `N`

A billboard is not a separate renderer. A facade node reaches the draw by the same route as any
other geometry (`FUN_004d4a20` dispatches on the node's render type, `FUN_004d39c0` handles the
Object3d case and calls the installed model draw through `DAT_00a07020`), and a `fvol` clutter
instance reaches that same function through the per-instance clutter draw `FUN_004d5de0`. So gate 1
above applies to a facade unchanged: `FUN_00552020` reads bit `0x01` of the model's flag word at
`+0x08`, and with it clear no light is even considered.

What the facade path changes is the **geometry the gate admits**, in three steps:

1. `FUN_00553700` runs first, before any vertex is transformed. It switches on the model's type
   word at `+0x00` (`0` Default, `1` Facade, `2` Points, the values mech3ax spells `model_type`;
   the GameGen command `Object3DSetFacade` takes a node, reads its model pointer at node `+0x3c`
   through `FUN_004cccf0` and writes a `1` into that word through `FUN_00562140`). For a facade
   it replaces the model's basis with a camera-facing one chosen by the mode word at `+0x04`
   (`0` CylindricalY through `FUN_005408e0`, `1` SphericalY through `FUN_00539390`, which caches
   its quaternion on the node at `+0xc8`, `2` and `3` through `FUN_005398b0` / `FUN_00539c70`).
   When the model carries flag bit `0x80` the rotation is wrapped in a translate to and from the
   centroid `FUN_00552180` averages out of the polygon vertices.
2. `FUN_00554550` then skips, for a facade whose mode is `0` or `1`, the per-polygon block that
   builds a face normal from the first three transformed vertices and rejects back-facing polygons.
   Such a model therefore has **no face normal of its own** and is never backface-culled.
3. The normals it does have are its authored per-vertex ones. `FUN_0053cba0` transforms the model's
   normal array by the current matrix's rotation, which by then is the billboard basis, and
   `FUN_00422690` renormalizes each one; the polygon gets that array when it carries the normals bit
   `0x1000` and the global `DAT_00a05f08` is armed. That global is the `SetVertexShading` GameGen
   variable (`FUN_0054e070`), and boot sets it to the hardware flag, so it is on for every retail
   capture.

`FUN_005688a0` reads the face normal at the submit record's `+0x08` and the per-vertex array at
`+0x0c`, preferring the array. A lit facade is therefore shaded per vertex from normals that
**rotate with the camera**, which is a directional term that tracks where the player is looking, not
a constant per-mission multiply.

### The ambient-only bit

Model flag bit `0x40` is the one exemption inside the lit path. `FUN_00554550` turns it into the
submit record's flag `0x80` at `+0x1c`, and `FUN_005688a0` then drops the `max(N·L, 0)` term
outright, leaving each directional light contributing exactly `ambient × colour`. The same flag
suppresses the fluctuation term below. A model carrying it is lit, flatly.

⚠ **Two mech3ax flag names mislead here, and the bit values are what to trust.** The extractor's
`facade_centroid` is bit `0x40`, the ambient-only bit above, and its `hardware_render` is bit
`0x80`, the bit that actually makes `FUN_00553700` rotate about the centroid. The shipped
populations agree with the binary rather than with the names: **477 models carry `0x80` and every
one of them is a `Facade`** (and every one `lighting: false`), while **369 carry `0x40`, 161 of them
`Default`-typed**, and every one of those is `lighting: true`, which is the only state in which the
bit does anything. `planes.zbd` carries neither.

### The fluctuation term, which no shipped file turns on

Between the dot product and the accumulator, `FUN_005688a0` multiplies the directional term by a
lookup into a 128-entry table at `0x006091b4`, indexed by the vertex's world X and Z through a pair
of scrolling offsets that `FUN_00566ae0` advances and wraps each frame. The whole term is gated on
`DAT_00639818`, whose only writer is the `DirectedLightFluctuate` GameGen setter `FUN_005669b0`.
That command name appears in **no shipped archive** (searched over `interp.zbd` and all eight
chapters' `gamez.zbd`, where the neighbouring `GameGenSetSubfacePriorityOffset` does hit), so the
factor is 1 in play and the drifting light-dapple the code can do never runs.

### The cloud sprites, which is what the flag decides for

Everything else the engine applies to one of these cards, all of it alpha, is
[`cloudCards.md`](cloudCards.md): the authored colour reaches the diffuse unscaled, and the card's
draw distance is scaled by the cosine of the viewing angle against its `fvol` polygon's normal.


The `fvol` sprite card is one tri-strip quad lying in the model's XY plane (132.27 m square in C1,
C1C, C2B and C4, with its corners wobbling between `z = −24.2` and `z = +17.6`, and 70 m square in
C5), vertex colours 240, and every chapter's copy carries the same three authored normals under the
same `normal_indices [1, 1, 0, 2]`. The **top two corners share the normal that points along the
card's own `+Y`** (0.999 of it) and the **bottom two carry the two that point out of the card along
`+Z`** (0.947 and 0.989). Under the
SphericalY basis that is a normal running up the screen against two running at the viewer, so a lit
card is not dimmed, it is Gouraud-shaded top to bottom with the bottom half swinging as the heading
changes.

Which cards are lit is a per-chapter authoring decision, and the two cloud populations differ:

| Population | Chapters | `lighting` | normals |
|---|---|---|---|
| `fvol` clutter cards (`cloudsprite1`/`cloudsprite2`) | C1, C4 | false | per vertex, unused |
| `fvol` clutter cards | C1C, C2B, C5 | **true** | per vertex, used |
| placed `cloudparent` facades | C1, C1B, C1C, C4 | false | none at all |

The placed clusters are unanimous: **every one of the 626 / 1,620 / 1,056 / 1,453 card nodes under
C1's 28, C1B's 70, C1C's 30 and C4's 45 `cloudparent` parents is `lighting: false`**, carries no
normal array, and is authored `fog: true` at vertex colour 255. The sun never touches them in the
original, so a directional look on that population cannot come from this mechanism.

## Where CSVM stands

The remake renders the world fullbright and dims it by one `csky_world_light` scalar, the
data-driven collapse of the original's per-vertex `N·L` (see [`weather.md`](weather.md)). Gate 1 is
reproduced: `SceneBuilder` reads the model's `lighting` flag and builds an unlit shader variant per
model. **Gate 2 must not be reproduced**, since the hardware draw does not have it: a textured world
surface takes the `csky_world_light` term whatever its texture's alpha class, and so does a clutter
sprite card. `TextureArchive` still reads the extractor's `alpha` field out of the archive's
`manifest.json` and publishes it as an alpha class, which is the right reader for anything that
needs the texture's own bit rather than its pixels.

⚠ **On a facade the gate is reproduced and the term behind it is not.** `csky_world_light` is the
collapse `AMBIENT + DIFFUSE × 0.46` calibrated on the predominantly up-facing world
([`weather.md`](weather.md)), and a camera-facing card is the one surface that averaging does not
describe: the original shades it per vertex from normals that turn with the camera, between
`AMBIENT` where a corner faces away from the sun and `AMBIENT + DIFFUSE` where one faces it. So
honouring the flag with a flat multiply, which is what `SceneBuilder` does today, is right about
*which* cards are lit (C1C, C2B and C5's `fvol` field, and nothing in C1, C4 or any placed
`cloudparent` cluster) and wrong about the value on all of them. Acting on that is a look change on
a visible population and is owed a verdict at the controls, not a luminance distance.

⚠ **A city wall is the same miss as a card, and C5 is where it is measurable.** The collapse's 0.46
stands for the world's mean `N·L`, so any surface whose own `N·L` is far from it is rendered at the
wrong value, and a vertical facade turned toward the sun is as far from the mean as a billboard is.
C5 authors `SUNLIGHT_DIFFUSE` 1.5 with `SUNLIGHT_AMBIENT` 0.5 in both zones, so `FUN_005688a0`
computes 0.5 to 2.0 per vertex where `WorldLightFactor` returns `clamp(0.5 + 1.5 × 0.46, 0.15, 1)`,
a flat 1.0 that also throws 0.19 away at the clamp. The ratio ours/theirs on a lit wall is then
`1 / (0.5 + 1.5 N·L)`, which is 1.0 at `N·L` = 1/3, 0.88 on a roof under C5's sun 25° up, and 0.545
at the most a vertical wall can reach. `CAP-11`'s C5 night facades measure in that range, at 0.66
and 0.58. The capture is not the cause: the five HUD gauge discs, the same 2D art at the same
pixels in both frames, read 0.97. C4 authors the identical pair and shows no such deficit on the
surfaces `CAP-12` measured, which are the cloud deck, `lighting: false` there and so lit by neither
engine. What a reproduction of the term needs decided first is on `BL-322`.

⚠ **The PNG alpha channel is not a substitute for the field.** It distinguishes `Full` from `None`
but loses the one to ten `Simple` textures per chapter, which carry the bit too, so the pixel
classification `TextureArchive` already had for the blend/scissor choice cannot answer that
question. The pixel test is the fallback for a deployed tree that ships PNGs with no manifest, and
it is a degradation rather than an equivalent.

⚠ **An exemption would be invisible wherever the mission's `WorldLight` is already 1.** C4 and C5
author `world_light=1` in every zone, so a multiply by one changes no pixel; it is C1 (0.802), C1B
(0.426), C1C and C2B (0.784) and C3 (0.99) where a wrongly exempted surface shows, C1B most.

Materials the software gate would take, counted over each chapter's whole gamez material table
joined to its top-tier `rtextureN` manifest (the archive the engine loads, which classes `needle`
and `smallneedle` as alpha where the base `texture.zbd` does not), kept as the census of the alpha
class rather than of anything the hardware draw treats differently:

| Chapter | Textured materials | Carrying the alpha bit | Unresolved names |
|---|---|---|---|
| C1 | 552 | 224 | 2 |
| C1B | 310 | 145 | 2 |
| C1C | 276 | 132 | 3 |
| C2 | 480 | 176 | 2 |
| C2B | 267 | 123 | 2 |
| C3 | 456 | 189 | 4 |
| C4 | 662 | 250 | 3 |
| C5 | 583 | 239 | 3 |

The unresolved names are the textures no archive ships (`pir_spinner`, `barngrill`,
`cloud1`/`cloud2`, `snow16x16`).

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
