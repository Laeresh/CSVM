# GameZ archives

The file layout of every chapter's `gamez.zbd` (the world) and the root `planes.zbd` (the player
aircraft), as `CSVM/src/Mech3/GameZZbd.cs` reads it into the `GameZ` model. What the fields mean to
the engine is in [gamez.md](gamez.md) and [world-structure.md](world-structure.md); this page is
the layout and the reader's rules. It is not the named-entry container of
[zbd-archive.md](zbd-archive.md).

## Contents

- [Conceptual model](#conceptual-model)
- [Layout](#layout)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

Four regions back to back: a texture table (names only, the pixels are in the chapter's
[texture archives](texture-archive.md)), a material array, a model array, and a node array. Each
array is a run of fixed-size records followed by the variable data they own, in record order. A
node references a model by its slot in the model array, a material by its position, and other
nodes by their position in the node array.

## Layout

All values are little-endian. Offsets inside a record are from the record's start.

| Region | Contents |
|---|---|
| `0x00` | file header, 40 bytes |
| `0x28` | texture table, *T* entries of 44 bytes |
| material offset | material array header (16), then the array's records, then each cycled material's flipbook |
| model offset | model array header (12), then the array's records, then each live model's data |
| node offset | *N* node records, then each node's data, parent and child indices; the last ends at the end of the file |

File header:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 4 | signature `0x02971222` |
| `0x04` | 4 | version `42` |
| `0x08` | 4 | build timestamp |
| `0x0C` | 4 | texture count *T* |
| `0x10` | 4 | texture table offset, `0x28` |
| `0x14` | 4 | material array offset |
| `0x18` | 4 | model array offset |
| `0x1C` | 4 | node count *N* |
| `0x20` | 4 | the `sunlight` node's tagged index |
| `0x24` | 4 | node array offset |

Texture entry: the name is 20 bytes at `0x0C`, stored `prefix\0suffix\0` with the period dropped.
The other words are a runtime image pointer, a state (1 assigned, 2 used) and a mip index; none
of them reaches the model.

Material array: `array size`, `count`, first free index, last used index, then *array size*
records of 40 bytes, each followed by two 16-bit free-list links. The first *count* are live:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 1 | alpha, 0..255 (coloured materials) |
| `0x01` | 1 | flags: `0x01` textured, `0x02` decal-receiving, `0x04` cycled |
| `0x04` | 12 | colour, three floats 0..255 (coloured materials) |
| `0x10` | 4 | texture table index (textured materials) |
| `0x20` | 4 | soil id |
| `0x24` | 4 | non-zero where the material has a flipbook |

A flipbook is 28 bytes (looping at `0x00`, speed in frames per second at `0x0C`, frame count at
`0x10`) and the frame count's texture table indices.

Model array: `array size`, `count`, last live slot plus one, then *array size* records of 100
bytes, each followed by a 4-byte word: the model data's file offset for a live slot, a free-list
link for a free one. A slot is live when its parent count is above zero.

| Offset | Size | Field |
|---|---|---|
| `0x00` | 4 | model type: 0 `Default`, 1 `Facade`, 2 `Points` |
| `0x04` | 4 | facade mode: 0 `CylindricalY`, 1 `SphericalY`, 2 `CylindricalX`, 3 `CylindricalZ` |
| `0x08` | 4 | flags: `0x01` lit, `0x02` fogged; the rest are in [gamez.md](gamez.md) |
| `0x0C` | 4 | parent count |
| `0x10` | 20 | polygon, vertex, normal, morph and point-light counts |
| `0x28` | 8 | texture scroll, *u* and *v* per second |
| `0x5C` | 4 | material reference count |

Model data, in order: the vertices and the normals (three floats each), the morph vectors, the
point lights (76-byte records, laid out in [world-structure.md](world-structure.md#mesh-point-lights),
then each light's vertices), the polygon records (40 bytes each), each polygon's data, and the
material references (12 bytes each, derivable from the polygons).

| Polygon offset | Size | Field |
|---|---|---|
| `0x00` | 4 | low 10 bits the corner count; `0x0400` show backface, `0x0800` no clutter, `0x1000` has normals, `0x2000` triangle strip |
| `0x04` | 4 | draw priority, signed |
| `0x0C` | 4 | non-zero where normal indices follow |
| `0x10` | 4 | material count, at least 1 |
| `0x18` | 4 | non-zero where UVs follow |
| `0x24` | 4 | zone set: low byte the count (0 to 3), then up to three signed zone bytes |

A polygon's data: one vertex index per corner, the normal indices when present, one material index
per material, then for each material its UVs (two floats per corner) when present, then one vertex
colour per corner (three floats, 0..255).

Node record, 208 bytes, then a 4-byte index whose top byte is `0x02`:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 36 | name, NUL-terminated |
| `0x24` | 4 | flags (`0x04` active, `0x10` intersect surface, `0x10000` can modify; the rest in [gamez.md](gamez.md)) |
| `0x28` | 4 | mission-target word, `field040` |
| `0x30` | 1 | zone id, signed |
| `0x34` | 4 | class: 1 Camera, 2 World, 3 Window, 4 Display, 5 Object3d, 6 Lod, 9 Light |
| `0x3C` | 4 | model slot, or `-1` |
| `0x54` | 4 | parent count and child count, 16 bits each |

Node data by class: Camera 488 bytes, Display 28, Window 248, Light 256 plus one parent index, Lod
92 (the near range squared at `0x04`), Object3d 144, World 204 plus one child value plus its
partition cells. Then the node's parent indices and child indices, 4 bytes each.

Object3d data: flags at `0x00` (`0x08` no transform, `0x10` bare matrix), rotation at `0x18`, scale
at `0x24`, then the 4x3 matrix at `0x30`, stored transposed with the translation last.

World data: the area's left, bottom, right and top edges as floats at `0x34`, `0x38`, `0x44` and
`0x48`, and the partition column and row counts at `0x98` and `0x9C`. The cells follow in rows from
the area's high z edge down, each row from its low x edge: 88 bytes (`min` at `0x10`, `max` at
`0x1C`, value count at `0x3A`), then per value a node index and the node's y range (12 bytes).

## Reader rules and edge cases

- **A mesh index is dense.** The model array is sparse, live and free slots interleaved, and a node
  stores the slot. The reader keeps only the live models, in slot order, and gives a node the
  position of its slot among them. A node naming a free slot is refused.
- **The matrix or the rotation.** An Object3d with neither the no-transform flag nor the bare-matrix
  flag stores a rotation, a scale and a matrix. When the matrix is exactly the one the rotation and
  scale recompose to in single precision (`R = Ry * Rx * Rz`, each row of three scaled by its scale
  component only where that component is not 1), the model takes the rotation through
  `Basis.FromEuler(rotate, Yxz)`; otherwise it keeps the stored matrix. That disagreement is what
  the extraction's stderr note "object3d transform fail" marks. The bare-matrix form ships nowhere
  and is refused.
- **Texture names** decode with the period restored at the first NUL when a suffix follows it, so a
  stored suffix that starts with a period gives the doubled period of [gamez.md](gamez.md).
- **The soil id is stored as the id.** The extraction's labels map to the same numbers, and only
  0, 1, 5, 8, 11, 12 and 13 occur; anything else throws, as an unknown label does.
- **The area reads through an integer**, as the extraction writes it, so an edge of `-0` reads
  `+0`.
- **Not read**: the build timestamp, runtime pointers, the texture state and mip index, the
  bounding boxes, the camera, window, display and light records, morphs and material references.
  The per-chapter image-pointer tables a byte-identical repack needs are runtime pointers too, and
  no part of the model comes from them.
- **Integrity** is the regions meeting where the header says: the material array ends at the model
  offset, each model's data starts at its recorded offset, the last model ends at the node offset,
  and the last node ends at the end of the file.

## Evidence and limits

`CSVM.Tests/GameZZbdTests.cs` reads all nine archives from the install and compares each model
with the one today's extraction loads, field for field and floats by their bits: C1 1,327,100
leaves, C1B 642,096, C1C 629,280, C2 928,179, C2B 543,068, C3 1,089,113, C4 1,561,538, C5
1,690,471, planes 1,365,159, all equal. 172 Object3d nodes keep their stored matrix (C1 17, C1B 1,
C2 4, C3 8, C4 10, C5 5, planes 127). The recompose test calls the platform's single-precision
sine and cosine; it is verified on Windows, where the extraction ran, and a C runtime whose `sinf`
rounds differently could flip a borderline node between the two forms.
