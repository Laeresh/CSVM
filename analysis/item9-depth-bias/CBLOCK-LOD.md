# The C5 `cblock` family — and the fifth mechanism, which is the right one

**Read-only data analysis, 2026-07-22/23.** Every number below comes from `extracted/**` JSON +
PNG plus the committed software depth probe. One Godot run was made at the end, purely to
confirm (`Get-Process *Godot*` was checked first and was empty). **No engine code was changed
and no bias constant was touched.**

Claims are labelled **[measured]** (computed from shipped data), **[inferred]** (measured values
plus code reading), **[assumed]**.

---

## Verdict

**The C5 z-fight is not a depth-precision problem and never was. It is a missing feature: the
original's per-polygon *subface* flag, which our engine does not parse.**

The polygon flag mech3ax calls `unk3` (raw bit `0x0800`) marks a coplanar overlay face. The
original applies `GameGenSetSubfacePriorityOffset 1` to it — one full priority level, from
`support\init.gw`, applied globally to every mission in the game. We ignore the flag entirely
(`grep unk3 CSVM/src` → **zero hits**), so a subface and the face it sits on separate only by
`SurfaceRankBias` (2e-6) and their tie is decided by whichever material happens to appear first
in the polygon list.

**We therefore render the authored layering backwards on 66.5% of the affected C5 ground.**
At the recorded repro pose, the base layer `cblock4` wins **144,817 px (62.8% of frame)** and the
subface `cblock2` that should be covering it loses. Applying the original's own offset to the
original's own flag drops `cblock4` to **16 px** and takes the flicker metric **78.14% → 0.00%**.

This is not "drive the metric to zero by drawing something the original never draws"
(`verification.md` rules 4 and 8). The geometry stays; it just goes where the data says. The
metric reaching zero is a *consequence* of the authored rule, not the goal.

**The user's report is reproduced exactly.** "I could not find `cblock[4-6].png` in C5 IA1 in the
original" — because in the original they are completely covered: **100.000%** of `cblock6` and
**99.88%** of `cblock5` sit under a subface. The user was not seeing an LOD switch away from
them; they were seeing them correctly buried.

---

## 1. What the `cblock` family is

### 1a. Seven ground materials, two roles — **[measured]**

`extracted/C5/gamez/materials.json` references exactly seven: `cblock1`…`cblock7`. None of them
carries a `cycle` block (`cycle: null` on all seven), so **`TextureCycler` is not the mechanism** —
ruled out.

They pair up 1↔4, 2↔5, 3↔6 by three independent measurements:

| | `cblock1`/`2`/`3` | `cblock4`/`5`/`6` | `cblock7` |
|---|---|---|---|
| size | **256×256** | **64×64** | 128×128 |
| mean luminance | 15.6 / 9.6 / 8.2 (night, median 0) | **40.2 / 36.3 / 34.5** (daylit grey) | 16.1 |
| authored `_1`/`_2` levels | yes (128, 64) | **none** | `_1` only |
| carries `unk3` | 102 / 121 / 98 polys | **1 / 0 / 0** | 78 |
| structural correlation of the pair | `1↔4` r=+0.704, `2↔5` r=+0.720, `3↔6` r=+0.702 (cross-pairs 0.39–0.53) | | `1↔7` r=**+0.983** |

`cblock7` is *the same artwork as `cblock1`* at 128² (r=0.983) with its own clutter template — a
fourth district variant, not part of the pairing.

The brightness gap is the trap: `cblock4/5/6` look like daylight photos next to near-black
`cblock1/2/3`, which invites a "day set / night set" reading. That reading is wrong — see §1c.

### 1b. `cblock1` / `cblock1_1` / `cblock1_2` is a hand-authored mip chain, and we throw it away — **[measured]**

The user's observed progression is real and is a **separate finding from the z-fight**.

- Every chapter ships 52–91 base textures with `_1` (half res) and sometimes `_2` (quarter res).
  **Zero of these `_N` names are referenced by any gamez material, in any chapter** — they are
  resolved by the texture system at load time, exactly like a mip chain.
- They are **not** downsamples. Against a box filter of the base:

  | | authored | box-filtered | authored px >128 | box px >128 |
  |---|---|---|---|---|
  | `cblock1_1` 128² | mean 4.41 | mean 15.64 | 0.38% | **0.00%** |
  | `cblock1_2` 64² | mean 6.77 | mean 15.72 | 1.12% | **0.00%** |
  | `cblock2_1` 128² | mean 0.83 | mean 9.63 | 0.35% | **0.00%** |

  The artists dropped the overall level *and kept the street lights punchy*. A box filter averages
  every single light away (0.00% bright pixels at every level). The chain is also non-monotone
  (15.62 → 4.41 → 6.77), which no filter produces.
- `TextureArchive` calls `img.GenerateMipmaps()` on the base PNG and never looks for the
  `_N` siblings. So we render box-filtered mips where the original renders authored ones.
  **[inferred]** That is precisely "completely dark with some points" (authored far level) versus
  our grey mush. (The line number this paragraph originally carried, `:100`, was already stale when
  written and misdirected `BL-055` once — cite the call, not a line.)

> **Closed 2026-08-02 by `BL-055`.** `TextureArchive` now installs the authored levels
> (`--mips=authored`, the default; `--mips=generated` restores this box filter). The measurement
> above is repeatable two ways: `mip_census.py` in this directory censuses the shipped PNGs per
> chapter, and `--dump-mips` reports the chain the engine installed. See `docs/HISTORY.md`
> 2026-08-02 and the `_1`/`_2` bullet in `docs/formats/gamez.md`.
- Name resolution is safe: `Retrieve` takes the exact match first, so `cblock1.tif` can never
  resolve to `cblock1_1.png`. **[measured, code]**

`rtexture2/4/6/8/14` are the whole archive at reduced resolution — the game's texture-detail
setting, orthogonal to all of this. **[measured]**

### 1c. `cblock4/5/6` are the *base* ground; `cblock1/2/3` are subfaces laid on top — **[measured]**

Not a day set. Not a distance LOD. Every cblock polygon — both layers — has the **identical
world-space texture scale, 256 m per tile**, and both layers sit at **exactly y = 5.0**. Node 1847
`g4664` is the clean case: poly 1 is `cblock1` over a 1024×1024 quad with `u[0,4] v[0,4]`, and
poly 4 is `cblock4` over **the same quad with the same UVs**.

The two layers have their own **distinct clutter template building sets** (`support\c5\load.gw`
loads `clutterblok1.flt`…`clutterblok7.flt`; `support\c5\adjust.gw` registers all seven). Our
current C5 build stamps them all: `cblock1/2/3` give `cb00a`–`cb11a`, `cblock4/5/6` give
`cb12a`–`cb24a`. Nothing overlaps in the building sets. **[measured]** So the base layer exists to
carry a district's building set; the subface is the pavement you actually see between them.
**[inferred]** That explains why a fully-buried ground texture is worth authoring at all.

---

## 2. Do the variants overlap? (the load-bearing question) — **YES, exactly**

True triangle∩triangle clipping via the committed `item9_lib.clip_area`, triangulated exactly as
`SceneBuilder.EmitPolygon` does. **No AABB test anywhere** — `verification.md` rule 9 has already
cost this bug two wrong diagnoses. **[all measured]**

| pair | true overlap | as % of the base's own area |
|---|---|---|
| `cblock1` × `cblock4` | 9,792,228 m² | 88.5% of `cblock4` |
| `cblock2` × `cblock5` | 8,379,974 m² | **99.88%** of `cblock5` |
| `cblock3` × `cblock6` | 6,099,148 m² | **100.000%** of `cblock6` |
| `cblock7` × `cblock4` | 799,868 m² | |
| `cblock2` × `cblock4` | 131,072 m² | (node 1777 `g4683`, the repro node) |
| every cross pair (`1×5`, `1×6`, `2×4`, `2×6`, `3×4`, `3×5`) | **0 m²** | |

The pairing is strict — a `cblock4` quad is only ever overlapped by `cblock1` (and by `cblock7`
or `cblock2` where districts abut). Residual *exposed* base: `cblock4` ~336,000 m² (3.0%),
`cblock5` ~10,000 m² (0.1%), `cblock6` **0 m²**.

### The falsifiable prediction, and it holds

A subface must lie **entirely inside** its parent face. Coverage of each `unk3` polygon by
coplanar non-`unk3` polygons (same-node *and* cross-node) is perfectly bimodal: **[measured]**

| coverage | polygons | area |
|---|---|---|
| 0–10% | 251 | 12,749,722 m² |
| 20–30% | **1** | 59,264 m² |
| **100–100%** | **196** | **26,330,410 m²** |

Not one partial case. When an `unk3` polygon has a coplanar partner at all, that partner contains
it **exactly**. The 251 with no partner carry the flag but have no base in the built world — the
flag survives from the source `.flt` face; harmless, since the offset only matters where there is
something to sort against. **[inferred]**

And the converse holds too, per texture: **[measured]**

| texture | `unk3` | polys | own area | overlapped | % |
|---|---|---|---|---|---|
| `cblock1` | **off** | 236 | 32,435,893 | 0 | **0.0%** |
| `cblock1` | on | 96 | 11,281,871 | 9,792,228 | 86.8% |
| `cblock2` | **off** | 91 | 8,441,749 | 0 | **0.0%** |
| `cblock2` | on | 121 | 10,891,558 | 8,511,046 | 78.1% |
| `cblock3` | **off** | 52 | 4,921,666 | 77,149 | 1.6% |
| `cblock3` | on | 98 | 8,636,430 | 6,161,263 | 71.3% |
| `cblock4` | off | 63 | 11,042,657 | 10,706,785 | **97.0%** |
| `cblock5` | off | 61 | 8,390,342 | 8,379,974 | **99.9%** |
| `cblock6` | off | 40 | 6,099,148 | 6,099,148 | **100.0%** |

An unflagged `cblock1/2/3` polygon is essentially *never* overlapped; a `cblock4/5/6` polygon is
essentially *always* overlapped and essentially never flagged.

---

## 3. What selects between them: `unk3` + `GameGenSetSubfacePriorityOffset 1`

### The evidence chain

1. **`support\init.gw` line 22: `GameGenSetSubfacePriorityOffset 1`.** Global, once, for the whole
   game — sitting beside `SetCoplanarTolerance 0.10005`, `SetBFETolerance 0.00005` and
   `SetInverseZTolerance .02`. This is a coplanar-face-sorting block, and it names *subfaces*
   explicitly. **[measured]**
2. **The world is loaded from OpenFlight**: `LoadGameGen c5\terrain\c5.flt`,
   `LoadGameGen c5\templates\clutterblok1.flt cblock1`, etc. OpenFlight has a first-class
   **subface** concept — a face coplanar with and contained in its parent, drawn after it with a
   depth offset, used for exactly this (road markings, terrain patches on terrain). "GameGen" is
   the `.flt` loader. **[inferred — external format knowledge, corroborated by 1 and by §2]**
3. **`unk3` is that bit**, per §2's bimodal containment and the per-texture table.
4. **The textures that carry it say so.** Across chapters, `unk3` lands on `terpat01`/`terpat03`/
   `terpat04` (**ter**rain **pat**ch), `cliff01-trans1`, `cliff02_trans1`, `river1`/`river3`,
   `wtr00000`, `filmlot5`, `pier`, and the `cblock*` set. Every one is an overlay-shaped thing.
   **[measured]**

Per-chapter `unk3` population: **[measured]**

```
C1   49   COLORED×45, cliff02_trans1×2, terpat03×2
C1B   0   <- none at all
C1C  71   COLORED×71
C2   71   terpat01×50, terpat04×14, terpat04-128×3, terpat01_trans2×2, filmlot5, pisa2
C2B  45   COLORED×45
C3    1   forest1-128
C4  141   COLORED×49, terpat01×48, terpat01-128×27, cliff01-trans1×9, river1×4, river3×2, ...
C5  658   wtr00000×142, cblock2×122, cblock1×102, COLORED×102, cblock3×101, cblock7×78, pier×10, cblock4×1
```

> **C1B has zero subface polygons in the entire chapter.** That is an independent, data-side
> corroboration of the user's ruling that C1B's z-fighting is authentic: the original had **no
> authored resolution** for C1B's `wtr00000`/`srf0001` pair either, so it z-fought there too.
> **[measured]** And the subface change provably cannot touch it — see §5.

### What it does at the repro pose — **[measured]**

Software depth probe (`cblock_probe8.py`, built on the committed `item9_depthprobe.py`, near-plane
clipping and all), C5 `--campos=-9533.178,76.319,-3367.413 --lookat=-9451.281,28.148,-3398.597`:

| | today (`unk3` ignored) | subface offset **+1 level** |
|---|---|---|
| frame within 3e-6 of another surface | **78.14%** | **0.00%** |
| `cblock4` (base) pixels won | **144,817** | **16** |
| `cblock2` (subface) pixels won | 38,304 | **183,105** |
| `cblock1` (subface) pixels won | 35,227 | 35,227 |

Front/behind pairs today:

```
144,801 px  FRONT g4683/cblock4 pri0 rank1 unk3=False   BEHIND g4683/cblock2 pri0 rank0 unk3=True   <- INVERTED
 35,227 px  FRONT g4683/cblock1 pri0 rank2 unk3=True    BEHIND g4683/cblock4 pri0 rank1 unk3=False  <- accidentally right
```

The 62.8% half is inverted *because* rank happens to favour the base; the 15.3% half is right by
luck. Same mechanism, opposite outcomes — which is why no uniform bias could ever fix both.

### Chapter-wide, how wrong we are today — **[measured]**

Every coplanar overlapping subface/base pair, and where our current `depth_bias + node_bias`
actually puts the subface:

| chapter | subface/base overlap | inverted today | tied (guaranteed z-fight) | correct today |
|---|---|---|---|---|
| **C5** | 25,853,145 m² | **17,186,723 (66.5%)** | 1,448,853 (5.6%) | 7,217,569 (27.9%) |
| C4 | 500,644 m² (cross-node) | 381,080 (76.1%) | 0 | 119,564 |
| C2 | 681,437 m² | 558,557 (82.0%) | 0 | 122,880 |
| C1 | 26,636 m² | 26,636 (100%) | 0 | 0 |
| C3 | 8,139 m² | 8,139 | 0 | 0 |
| C1C, C2B | COLORED-vs-COLORED only | — | — | — |
| **C1B** | **none** | — | — | — |

### Verified against the original

`OriginalScreenshots/C5 IA1 Terrain.png` and `…Terrain3.png` **are present** in this tree and were
read. Both show C5 IA1 ground as near-black blocks with sparse white street lights across the
entire visible city — including `Terrain3`'s high-altitude view covering many blocks. The daylit
mid-grey `cblock4/5/6` (mean luminance 34–40 against 8–16) would be unmissable and appears
nowhere. **[measured, visual]**

> **This upgrades those captures.** `FINDINGS.md` recorded them as "not conclusive on its own —
> a still cannot show z-fighting". True, but a still *can* settle **which layer wins**, and that
> is the question that turned out to matter. The two layers differ ~3× in mean luminance.

Our own engine at the repro pose (`.scratch/c5_repro_current.png`, brightened 5× as
`c5_repro_current_x5.png`) shows the right-hand ground as a **mottled multicoloured speckle** —
the visual signature of two textures interleaving per pixel. **[measured, visual]**

### What was ruled out, and stays ruled out

| candidate | verdict |
|---|---|
| **Partition visibility** (`WorldPartitionSetActive`) | **DEAD.** All 25 uses are `support\c3\*.gw` — **C3 only**. It never appears in any C5 script, and it takes rectangle coordinates, not node names. **[measured]** The `backlog.md` claim that this is "the real runtime system the original uses to pick between coarse and fine ground" is **wrong for C5**, which is where the bug is. |
| **`Lod` nodes** | **DEAD**, as the plan predicted. C5 has 1201 `Lod` nodes covering 3250 nodes; **none** of the cblock ground nodes (1777, 1813, 1814, 1822, 1823, 1847, 2278) is under one. **[measured]** |
| **Material `cycle` flipbooks** | **DEAD.** `cycle: null` on all seven cblock materials. **[measured]** |
| **`zone_set`** | **DEAD** as a selector here. It is a per-polygon weather-zone list (C5 uses 1 and 3), and *both* layers are `zone_set [1]` at the repro node. **[measured]** Still a real unparsed field — see §7. |
| **Day/night variant sets** | **DEAD.** Same 256 m tiling, same UVs, same y-plane, all vertex colours 255 — and the pairing survives only as base/subface. |

### Also found, not the mechanism

`materials` is a **list** per polygon and 352 C5 polygons carry **two** entries with independent
UVs — a second texture pass: `z3_foggrad`/`foggrad8x64` fog gradients (142), `buildingspotlighted`
(34), `fadedsign01-03`, `nypd`, `clock`, plane logos. `SceneBuilder` reads only `materials[0]`.
**[measured]** Separate, unrelated missing feature; worth a backlog entry.

---

## 4. Does the pattern exist in other chapters? — yes, small

C5 carries 78% of the subface polygons in the game. C4 (500K m², cross-node `terpat01` over
`shore1`), C2 (681K m², `terpat04` over `beach1`) and C1 (27K m²) are two orders of magnitude
smaller. C1B has none; C1C/C2B have only untextured `COLORED` ones; C3 has one polygon.

C2's `terpat04` over `beach1` is worth flagging: it is the **same shape as the C3 beach case
that `verification.md` rule 8 is written from** — a terrain patch losing to the surface under it.
Rule 8's warning was that the flicker metric can license the wrong winner. Here the data names the
winner explicitly, which is exactly what rule 8 said was missing.

---

## 5. What implementing it would take

**Three edits, ~10 lines, no new parser.**

1. **`GameZ.cs:278-289`** — one line beside the existing `unk2`/`show_backface` read:
   `poly.Subface = pf.TryGetProperty("unk3", out var sf) && sf.GetBoolean();`
   Add `public bool Subface;` to `GameZPolygon` (`:499-515`). The fork serialises `unk3` with
   `skip_serializing_if bool_false`, so it is **absent when false** — `TryGetProperty` with a
   `false` default is required, and is also correct for the legacy v0.6.1 tree. Both the fork and
   `tools/mech3ax-cs-ref` use the same name and the same raw bit (`1 << 11`, `0x0800`).
2. **`SceneBuilder.cs:386`** — the surface group key becomes
   `(poly.MaterialIndex, poly.Priority, poly.Subface, doubleSided)`, and `Subface` joins the
   `_materialCache` key (`:38`) and the `GetMaterial`/`BuildMaterial`/`BiasMaterial` chain
   (`:700`, `:766`, `:831`).
3. **`SceneBuilder.cs:841`** — `bias += subface ? SubfaceBias : 0f;`

### Sizing `SubfaceBias` — **[measured]**

`DepthBiasPerLevel` is 2e-4. Outcome over every subface/base overlap in C5:

| offset | same-node front / behind | cross-node front / behind |
|---|---|---|
| 0 (today) | 7,217,569 / 17,186,723 (+1,448,853 tied) | 215,350 / 280,296 |
| **0.5 level (1e-4)** | **25,732,146 / 120,999** | **491,052 / 4,594** |
| 1.0 level (2e-4) | 25,732,146 / 120,999 | 492,513 / 3,133 |

**0.5 of a level is measurably as good as 1.0 and strictly safer.** Priority 1 is a real authored
value (955 C5 polygons, 2207 in C1), so a full +1 level makes a subface *tie* with a genuine
priority-1 overlay; 1e-4 stays inside the hierarchy while being 50× `SurfaceRankBias` and 2000×
`NodeOrderBias`. Record that the original's literal value is 1 whole level — this is a deliberate,
documented deviation, not an accident.

The residual 120,999 m² still "behind" at either offset is where the **base has a higher authored
priority** than its subface (base 0 vs subface −10). That is an authored decision and the offset
correctly does not override it.

### Blast radius

- **C1B and C3 repro poses are byte-identical with and without the change** — verified by running
  the probe at both. Zero `unk3` polygons participate at either pose. **[measured]** The user's
  "C1B is not a bug" ruling is structurally safe: it cannot be perturbed.
- **Do not touch `SurfaceRankBias`, `NodeOrderBias` or `DepthBiasPerLevel`.** The whole
  conflict-local-bias direction stays retired; this is orthogonal to it.
- ⚠ **Splitting a group changes existing ranks.** Adding `Subface` to the group key means a mesh
  that previously had one `(material, priority)` group may now have two, shifting the *rank* of
  every group after it and therefore its bias. Worst case in C5 that is +176 surfaces (658 flagged
  polys across 176 models), but the rank shift is the real regression risk, not the surface count.
  Verify with the full 8-chapter `--freecam` regression on mesh/surface counts.
- **Clutter is untouched and probably correct as-is.** `cblock4/5/6` keep their own building
  templates (`cb12a`–`cb24a`, disjoint from `cblock1/2/3`'s `cb00a`–`cb11a`), which is most likely
  the *reason* the buried base layer exists. Do not "fix" the doubled clutter on the strength of
  this report — **[open question]**, not a finding.

---

## 6. Proposed deltas to the files this analysis must not edit

**`docs/plans/PLAN-M2-polish-4.md` item 9** — the fifth mechanism, and the one that survives:

> The C5 half is **not a depth-precision problem**. The polygon flag `unk3` (raw `0x0800`) is the
> original's OpenFlight **subface** mark, and `support\init.gw` applies
> `GameGenSetSubfacePriorityOffset 1` to it globally. `CSVM/src` parses it nowhere. Measured:
> 25.85M m² of C5 ground is a subface over a coplanar base, we render **66.5% of it inverted and
> 5.6% exactly tied**, and at the repro pose the base `cblock4` wins 144,817 px where the original
> shows the subface. Applying the flag takes the pose 78.14% → 0.00% and `cblock4` to 16 px, with
> the C1B and C3 poses **byte-identical**. Fix is 3 edits, ~10 lines; recommended offset **0.5 of
> a priority level (1e-4)**, not the original's literal 1 level, because priority 1 is authored
> (955 C5 polygons). **Do not change any bias constant.**

**`backlog.md`** — three corrections and two new entries:

- The entry claiming `WorldPartitionSetActive` is "the real runtime system the original uses to
  pick between coarse and fine ground, and we draw both unconditionally" is **wrong for C5**.
  All 25 uses are C3-only and coordinate-based. Rewrite or drop.
- New: **authored `_1`/`_2` mip levels are ignored.** 52–91 base textures per chapter ship
  hand-authored half/quarter-res levels, referenced by no gamez material, and `TextureArchive.cs:100`
  box-generates its own instead. The authored levels keep 0.35–1.12% of pixels above luminance 128
  where a box filter keeps **0.00%** — this is the user's "dark with some points → brighter
  illuminated street" observation.
- New: **`materials` is a per-polygon list and we read only `[0]`.** 352 C5 polygons carry a
  second textured pass (fog gradients, spotlights, signs, logos) with its own UVs.
- New: **`zone_set` is parsed by nothing** (`grep zone_set CSVM/src` → 0 hits). Per-polygon
  weather-zone membership; C5 uses 1 and 3.

**`docs/verification.md`** — a new rule, in the shape of rules 4/8/9:

> **A brightness or resolution difference between two coplanar layers is not evidence of a
> day/night or LOD variant pair.** C5's `cblock4/5/6` are 4× lower resolution and 3× brighter than
> `cblock1/2/3` and look exactly like a daylit LOD set. They are neither: they are the *base*
> ground under an authored subface, and the appearance difference is a red herring that had to be
> disproved by geometry (identical 256 m UV scale, identical y-plane, 100.000% containment) before
> the real mechanism became visible.

**`docs/formats/`** — the `unk3` decode lands with a docs page in the same change, per the standing
rule. It belongs beside the existing polygon-priority documentation.

**`docs/HISTORY.md`** — dated entry when the fix lands, not for this analysis.

---

## 7. Scripts

All in `analysis/item9-depth-bias/`, read-only, reusing the committed `item9_lib.py`.

| file | what it does |
|---|---|
| `cblock_probe1.py` | which built nodes use which `cblock` material, and how much area each |
| `cblock_probe2.py` | true overlap + exact-duplicate-triangle test between `cblock` variants |
| `cblock_probe3.py` | the `unk3`-as-subface hypothesis, coplanar pairs within one model |
| `cblock_probe4.py` | per-texture coverage vs `unk3` (the table in §2) |
| `cblock_probe5.py` | the same sweep, all textures, all 8 chapters |
| `cblock_probe6.py` | directionality: where our current bias puts the subface (`cblock` only) |
| `cblock_probe7.py` | the same, all textures, all chapters (the table in §3) |
| `cblock_probe8.py` | **the repro-pose test** — the depth probe with a subface offset |
| `cblock_probe9.py` | cross-node subface overlaps (the within-model probe cannot see these) |
| `cblock_probe10.py` | sizing `SubfaceBias` against `node_bias` and the authored priorities |
| `cblock_probe11.py` | the containment prediction (the bimodal table in §2) |

⚠ **One change to a committed file:** `item9_lib.py`'s `ROOT` was `dirname(dirname(__file__))`,
which was correct when these scripts lived in `.scratch/` and broke when they were committed one
level deeper. It now walks up until it finds `extracted/`. Every previously committed script was
non-runnable in its current location before this; they all run now.

Scratch output (git-ignored, `.scratch/`): `cblock_contact_sheet.png`, `c5_repro_current.png`,
`c5_repro_current_x5.png`, `c5_ground_down_current.png`.
