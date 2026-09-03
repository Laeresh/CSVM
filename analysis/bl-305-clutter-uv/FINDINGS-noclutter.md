# `no_clutter` vs SUBFACE: the extraction side

Companion to the plan's §"The Ghidra re-read (2026-08-10)". The orchestrator chased the bit
assignment inside `gmod_cons.c`; this is the data-and-extractor half. Instrument:
[`noclutter_census.py`](noclutter_census.py).

Short version: **`no_clutter` never reaches the shipped data as text — it reaches it as a bit, and
that bit is the one mech3ax exposes as `unk3`. The extractor drops nothing.** The remake can
already see the attribute; it has simply been reading it under the wrong name.

## 1. The strings are in the executable only — **[measured]**

A byte-level ASCII scan of the whole install (`Z:\CSVM\CrimsonSkiesGame`, every file, not ripgrep —
ripgrep quits at the first NUL in a binary and reports nothing):

| needle | hits |
|---|---|
| `no_clutter` | `crimson.exe` @ 0x22C0A8, **1**. Nowhere else. |
| `noclutter` | `crimson.exe` @ 0x22C068, **1**. Nowhere else. |
| `clutter` | `crimson.exe` 7, `ZBD\interp.zbd` 48, `ZBD\{C1,C1C,C2B,C4,C5}\zrdr.zbd` 1 each |

The scanner is proven able to find things — it found `clutter` inside five `.zbd` files. So the
negative on `no_clutter` is a real negative, not a broken instrument. **No `.zbd` in this install
contains the attribute as text.** That is exactly what the Ghidra chain predicts: `gg_load.c`'s
`strstr` runs over `.flt`/gamegen *source* text at content-build time and packs the result into a
bit; the retail `gamez.zbd` ships only the packed polygon.

## 2. mech3ax discards no bit of the polygon flags word — **[measured]**

Crimson Skies gamez models are read by the `pm` model reader
(`crates/gamez/src/gamez/cs/models.rs` imports `crate::model::pm`). The raw word is
`crates/gamez/src/model/pm/mod.rs:72-81`:

```rust
struct PolygonBitFlags: u32 {
    static VERTEX_COUNT = 0x03FF;
    const SHOW_BACKFACE = 1 << 10;   // 0x0400
    const UNK3          = 1 << 11;   // 0x0800  "not in mechlib"
    const NORMALS       = 1 << 12;   // 0x1000
    const TRI_STRIP     = 1 << 13;   // 0x2000
    const IN_OUT        = 1 << 14;   // 0x4000  "not in mechlib"
}
```

- **Named and exposed to JSON:** `SHOW_BACKFACE`, `TRI_STRIP`, `UNK3`, `IN_OUT`
  (`read.rs:168-180`). `UNK3` and `IN_OUT` are `skip_serializing_if bool_false`, i.e. absent when
  false.
- **Consumed, not exposed:** the low 10 bits (vertex count) and `NORMALS` — both reconstructed from
  array lengths on write, so nothing is lost.
- **Silently discarded: nothing.** The `bitflags!` macro's `check`
  (`crates/types/src/bitflags/mod.rs:164`) returns `Err` for any bit outside `_VALID`, and the
  reader calls it as `chk!(offset, ?poly.flags)?`. Extraction of all eight chapters succeeded, which
  **proves** no CS polygon in this install sets a bit above `0x4000`.
- The rest of the 40-byte `PolygonPmC` is fully accounted for: `priority` (asserted -50..=50), five
  pointers passed through verbatim, and `zone_set`, whose all four bytes are decomposed by
  `assert_zone_set`. **There is no hidden per-polygon field.**

**So there is no dropped bit to surface.** If `no_clutter` survives into retail data at all, it must
be `0x800` or `0x4000` — and `0x4000` is ruled out by §5.

## 3. `unk3 == SUBFACE` was never measured — **[inherited]**

mech3ax does not claim it. The word "subface" **does not appear anywhere in the mech3ax tree**
(case-insensitive search of `Z:\CSVM\tools\mech3ax`: zero hits). `UNK3` is literally "unknown flag
#3" — an ordinal, not a meaning; the only comment on it is `// 1, 0x0800 not in mechlib`.

The SUBFACE reading is this project's own, from the archived development log, 2026-07-23 entry ("Polish-4 item 9"). It
was reasoned, not measured: `support\init.gw` applies `GameGenSetSubfacePriorityOffset 1` globally,
the OpenFlight-derived engine has a subface concept, and applying one priority level to the `unk3`
set fixed C5's ground z-fight. That the fix *worked* is real, but it only shows the `unk3` set draws
on top — it does not show the bit *means* subface. The orchestrator's Ghidra read (single writer of
bit 11 in the whole binary, from the `no_clutter` argument) shows it does not.

`GameZ.cs:427-431` and `docs/formats/gamez.md` rule 23 inherit the name from that entry. **Nothing
measured `0x800 == subface`.**

## 4. Census: the flagged set is streets, water and invisible boxes — **[measured]**

| chapter | polygons | `unk3` | `in_out` | `unk3` % |
|---|---|---|---|---|
| C1 | 18,277 | 49 | 18 | 0.27 % |
| C1B | 8,323 | **0** | 21 | 0.00 % |
| C1C | 8,040 | 71 | 0 | 0.88 % |
| C2 | 12,645 | 71 | 39 | 0.56 % |
| C2B | 7,008 | 45 | 0 | 0.64 % |
| C3 | 16,087 | 1 | 15 | 0.01 % |
| C4 | 19,661 | 141 | 45 | 0.72 % |
| C5 | 22,493 | **658** | 102 | 2.93 % |

`gamez.md`'s "658 in C5, none in C1B" reproduces exactly.

**C5's flagged set, by first-material texture** (658 polygons, 86.1 % horizontal, world Y clustered
dead on the ground plane — p50 and p95 both 5.0, against p95 264 for the unflagged set):

| texture | polys | share |
|---|---|---|
| `wtr00000` (water) | 142 | 21.6 % |
| `cblock2` | 122 | 18.5 % |
| `cblock1` | 102 | 15.5 % |
| *(untextured)* | 102 | 15.5 % |
| `cblock3` | 101 | 15.3 % |
| `cblock7` | 78 | 11.9 % |
| `pier` | 10 | 1.5 % |
| `cblock4` | 1 | 0.2 % |

Water, city pavement, pier decking, all horizontal, all at ground level. That is a *"nothing grows
here"* population, not an overlay population. C4's flagged set is the same idea in countryside
terms: `terpat01` terrain patches 34 %, `terpat01-128` 19 %, `cliff01/02/03-trans*` 8 %,
`river1`/`river3` 4 %.

### The `fvol` result kills SUBFACE outright

The 102 untextured C5 flagged polygons — and **45 of C1's 49** — belong to `fvol*` nodes: the
invisible fog/flight volumes (`WorldBuilder.IsFogVolumeNode` skips them from the render entirely).
Per box:

```
C1 fvol1..fvol9   6 polygons each, exactly 5 flagged
C5 fvol1..fvol34  6-14 polygons each, 5-9 flagged
```

Which face is the unflagged one? **The top, every time**, by Newell normal:

```
C1 fvol1  poly0 unk3=True   Ny=-1.00  y= 970.0   (floor)
          poly1..4 unk3=True Ny= 0.00  y=1030.3  (walls)
          poly5 unk3=False  Ny=+1.00  y=1090.5  (lid)
C5 fvol2  poly5 unk3=False  Ny=+0.80  y= 110.0   (sloped lid)
          poly6 unk3=False  Ny=+1.00  y= 183.0   (lid)
```

A closed box floating in the sky. Its floor and walls **cannot** be "coplanar with and contained in
the face beneath it" — there is no face beneath, and a box's faces are not coplanar with each other.
Under SUBFACE this is nonsense. Under `no_clutter` it is an artist marking a volume so the scatter
lands on its upward-facing lid and nowhere else — which is precisely what the ambient cloud field
does (`FogVolumeClutter`, `docs/formats/fogvol.md`). **[measured]** for the geometry, **[inferred]**
for the intent.

### Does `CBLOCK-LOD.md` survive? Partly — **[measured]**

The **geometry survives; the label does not.** `cblock1/2/3` really do sit coplanar on
`cblock4/5/6` at 97.0/99.9/100.0 % — that is an area measurement of real quads and is unaffected by
what the bit is called. But `CBLOCK-LOD.md`'s own falsification table already recorded the crack: of
the C5 flagged polygons it tested, **196 are covered at exactly 100 %, 251 at 0 % with no coplanar
partner at all**, and one in between. It explained the 251 away as harmless leftovers from the
source `.flt`. Under `no_clutter` there is nothing to explain — water surfaces, pier decks and the
floor of a sky box are *supposed* to have nothing beneath them. **The bimodality was always evidence
against SUBFACE and was read as noise.**

What does *not* survive is using the flag to identify the overlay layer. `BL-250`'s conclusion
("the original draws the `cblock1/2/3` city") rests on the coverage measurement plus the CAP-22
footage, both of which stand on their own. `SubfaceBias` also keeps working for the reason it always
worked — the flagged set happens to be the layer that must draw on top — but it is now a coincidence
of authoring, not a derivation, and the doc should say so.

### One thing the census does **not** support: SHOT-28

Gating clutter on the flag cannot delete C5's skyline. Area of every clutter-bearing C5 surface,
split by the flag: **[measured]**

| texture | flagged polys / area | unflagged polys / area | flagged share of area |
|---|---|---|---|
| `cblock1` | 102 / 11,871,695 | 256 / 51,539,637 | 18.7 % |
| `cblock2` | 122 / 10,916,248 | 93 / 9,555,861 | 53.3 % |
| `cblock3` | 101 / 8,478,413 | 53 / 4,987,202 | 63.0 % |
| `cblock7` | 78 / 4,089,784 | 1,199 / 75,798,830 | 5.1 % |
| `wtr00000` | 142 / 6,377,386 | 561 / 81,899,317 | 7.2 % |

The two sets are also spatially interleaved, not separated — around the downtown crossroads
(-9700, -3500) ± 1500 m there are 24 flagged and 10 unflagged `cblock1/2/3/7` quads. A gate on the
flag removes roughly half the downtown ground quads and 5 % of `cblock7`; **it leaves the majority
of every clutter-eligible texture standing.** The plan's SHOT-28 records the gate-only build as
"completely flat: painted ground, zero 3D buildings", which this data cannot produce.

**Recommendation: re-run SHOT-28 before treating it as disqualifying.** The likely candidates are an
inverted sense in the temporary patch, or a whole-*model* skip where a per-*polygon* skip was
intended. This is a claim about a reverted patch nobody can inspect, so it is flagged, not resolved.
**[inferred]**

## 5. The other unknown polygon field: `in_out`, and it is unrelated — **[measured]**

`in_out` (raw `0x4000`) is the only other unnamed polygon bit mech3ax exposes. It is entirely
accounted for:

| chapter | polys | textures | owners |
|---|---|---|---|
| C5 | 102 | 100 % untextured | `dzpath*`, 3 polygons each |
| C4 | 45 | 100 % untextured | `dzpath*`, 3 each |
| C1 | 18 | 100 % untextured | `dzpath*`, 3 each |

Every `in_out` polygon in the install lives on a `dzpath*` node — the drop-zone/path volumes the
world walk skips — and **not one of them also carries `unk3`** (0 overlap in all three chapters). It
is a path-volume attribute, not a clutter one, and it is not the `no_clutter` bit.

No other per-polygon unknown exists: `priority` is decoded and shipping, `zone_set` is fully
decomposed, and the pointers are passed through. (`unk8` and `unk00`/`unk04`/`unk24`/`unk48`… are
*model* and *point-light* fields, not per-polygon.)

## Bottom line

- **Can the remake see a no-clutter attribute? Yes — it already parses it, as
  `GameZPolygon.Subface`.** `GameZ.cs:431` reads `flags.unk3`, which is raw bit `0x800`, which is
  the bit `gmod_cons.c` writes from `gg_load.c`'s `no_clutter` global. Nothing needs to be
  re-extracted; the field needs renaming and a second consumer in `ClutterBuilder.PlaceOnMesh`.
  **[measured]**
- **Is `0x800 == subface` supported by anything other than mech3ax's naming? No — and not even by
  that.** mech3ax never says "subface"; it says `UNK3`, meaning unknown. The subface reading is this
  repo's 2026-07-23 inference from a rendering fix, and the data contradicts it in two independent
  places: 251 of C5's flagged polygons have no face beneath them, and 5 of the 6 faces of every
  `fvol` sky box carry the flag. **[measured]**
- **What it would take to surface the attribute: nothing at the extractor.** No bit is dropped. The
  work is a rename (`Subface` → `NoClutter` in `GameZ.cs`, `docs/formats/gamez.md` rule 23,
  `SceneBuilder`'s `SubfaceBias`) plus deciding what `SubfaceBias` is now justified by, since its
  *effect* is measured and correct while its *name* and rationale are not.
- **Where subface actually lives:** untested here. The orchestrator's hypothesis — that
  `GameGenSetSubfacePriorityOffset` bakes into the polygon `priority` field at load time, so subface
  is not a flag bit at all — is consistent with everything above but is not something this census
  can confirm or deny. **[inferred]**

---

*Authored by the census subagent; saved to disk by the orchestrator, verbatim apart from decoding
the HTML entities its transport introduced into the Rust snippet. The subagent's own Write access
was refused. See the plan for the SHOT-28 re-check this document triggered.*
