# Clutter placement — reproduce the original's UV-space stamping

**ACTIVE PLAN** (written 2026-08-09). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan replaces the mechanism `ClutterBuilder` uses to decide *where* clutter goes. The original
engine stamps every decoration at a fixed **texture-UV** coordinate, repeated once per integer UV
repeat of the terrain texture across each polygon; the remake tiles each template on a fixed
**world-space X/Z grid** of the template ground quad's side (512 m for C1's `terpat02`). That
choice is recorded as deliberate in `Clutter.cs`'s class comment on the grounds that "the exact
original alignment is undecoded" — which is no longer true. The mechanism was read out of
`crimson.exe` on 2026-08-09 and is written down in *What the data actually ships* below. The
consequence is not confined to one chapter: the same code path places C1's firs, C2's suburbs and
C5's downtown, so this is a reimplementation of the placement half of `Clutter.cs`, not a C5 tweak.

Out of scope: **the rendering half of `Clutter.cs` does not change.** The sprite shader, the
billboard axis, the solid/sprite split, `SceneBuilder.SharedMesh` instancing, the shared-collision
scheme and `MapEdgeExtender`'s consumption of `KindExport` all stay exactly as they are — this plan
moves instance *positions*, and everything downstream of `Kind.Instances` is left alone.
`BL-070` (the `poleflare` billboard axis) and `BL-250` (the buried-district exemption, already
landed) are not reopened. `far_fade_range` — the original's per-instance distance cull — is
surveyed here but deliberately not implemented; see C23.

## Milestone goal

- Clutter instance positions are derived from the terrain polygon's own texture UVs, the way the
  original derives them, in every chapter — not from a global metric grid.
- The density of C1's forests and C5's downtown follows the terrain's texture parameterisation, so
  the packed-edge-to-edge C5 blocks of `BL-305` and the thin C1 tree cover both move for the same
  reason.
- The per-kind authored data in `templates.zrd` — today extracted and read by nothing — has a
  reader, a `docs/formats/` page, and at least `substitute` and `scale_range` in effect.
- Every claim in `Clutter.cs`'s class comment about placement is either true or deleted.

**This plan does not touch how clutter is drawn.** Placement and rendering are separable, the
rendering side has its own landed evidence (billboard axis, fullbright materials, shared shapes),
and mixing the two would make any A/B unreadable — a density change and a shading change look the
same in a screenshot.

## Decisions (2026-08-09)

| # | Question | Decision |
|---|---|---|
| 1 | Is this a C5 packing fix (`BL-305`) or a general placement rewrite? | **General rewrite** — user's call, on the evidence that one code path serves every chapter. `BL-305` becomes the C5-shaped verification of it, not the scope. |
| 2 | Measure first, or rewrite first? | **Measure first, as Wave A.** The decode changes the assumptions the current code is built on; a rewrite that lands before the UV-to-world orientation is verified would be untestable against its own hypothesis. |
| 3 | Adopt `far_fade_range` in this plan? | **No** — it *removes* distant clutter and would confound every density A/B. Survey it (C23), then hand it to `backlog.md`. |
| 4 | Where does the work happen? | **A worktree** (`worktree-clutter-uv-placement`), because the change is global and the C1/C5 A/Bs need a clean `main` build to compare against. |

## ⚠ Read this before implementing anything

The founding fact of this plan is that a load-bearing comment in the code we are about to change is
wrong. Assume the neighbouring ones are suspect too until checked.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "The exact original alignment is undecoded" (`Clutter.cs:23`) | `FUN_004dd230` stores each decoration as a wrapped texture UV; `FUN_004dd6e0` stamps it per integer UV cell of each world triangle. Decoded, in full, 2026-08-09. |
| 2 | "UV-space placement would stretch the clutter with it" — offered as the reason to reject UV placement (`Clutter.cs:23-25`) | Factually true and *irrelevant as an objection*: stretching with the UV is what the original does. The remake's "keeps the authored density everywhere" is therefore a deviation, not a fidelity choice. |
| 3 | The ground quad's size "is the tiling period" (`Clutter.cs:17-18`, `GroundInfo` at `:581`) | The quad is the **domain the decoration positions are normalised against**, not a metric spacing. The original never uses it as a distance. A "-128" template variant is an authoring convenience, not a 128 m period. |
| 4 | `MinSlopeCos = 0.25f` — "steeper than ~75° grows no trees" (`Clutter.cs:78`) | An invention. The original's slope cull is authored per kind (`min_slope`/`max_slope` → cosines at kind+0x58/+0x5c, tested against the triangle normal's Y in `FUN_004dd6e0`) and **defaults to ±1.0, i.e. no cull at all**. No chapter's `templates.zrd` authors either key. ⚠ **The "silently deleting hillside trees today" half of this row is itself wrong** — B13 censused every chapter and the constant culls **zero** triangles: the steepest clutter-eligible face in the install is C1's at slope cos 0.4598 against a 0.25 threshold. Deleted as an inert invention, not as a density fix. |
| 5 | `BL-305`'s "prime suspect: `ClutterBuilder` tiles each template on a fixed world-space X/Z grid" | No longer a suspect — confirmed as the mechanism. The entry's own wording predates the decode. |
| 6 | (Mine, earlier this session) "C3 has the suburbs" | C3 registers exactly one template, `cliff1_sandtrans`. **C2** carries the suburbs (`resblock1-6`, `filmblock1-5`, `parklot1/2`, `parkpat`). Corrected against `extracted/interp.json`; the full census is below. |
| 7 | (Mine, from A1's ratios) "√2 in the quad-vs-world ratios means a 45°-rotated UV mapping", and "the exact-2 cases are quads spanning two texture repeats" | A2 read the UV coordinates themselves: **no 45° mapping exists anywhere in the install** (every bearing on those templates is 0° or 90°) and **every quad spans exactly 0..1**. The √2 was an artifact of A1's own statistic — a max-extent `Period` compared against a geometric mean, on a 2:1 quad. Passed to A2 as a flagged lead, not a finding, and killed there. |
| 8 | "B11 can land as a provably inert, behaviour-preserving step" (this plan's own B11, as written) | True for 28 of the 32 templates, false for four: the current scalar `Period` genuinely misplaces `filmblock1`, `cliff1_sandtrans`, `parklot1` and `parklot2` by up to 0.74 UV. B11 is amended to predict exactly which four move. |
| 9 | "Zero coplanar pairs → delete the `seen` dedup set" (this plan's own B13, as written) | A3 measured zero, but the zero is the *original's*: flag `0x800` **is** the subface mark, so the original never considers a subface polygon. The remake's `PlaceOnMesh` reads no such flag, so `seen` is the only thing suppressing a real double-stamp — 449 subface polygons in C5. B13 is amended: add the subface skip first, *then* delete the dedup. ⚠ **B13 ran that order and both of its predictions failed** — the gate removes 25 % of C5's clutter (not 0), deleting the dedup after it still adds duplicates (C1 +38, C5 +1,072 solids, from an inclusive edge test), and the gate alone **empties C5's downtown** because `BL-250`'s exemption already removed the base layer it would leave behind. Neither landed in B13; **both landed together in B15**, once the user's flyover showed why the gate needs the base layer restored rather than exempted. |
| 10 | (This plan's own working assumption, through A3 and all of B13) "`0x800` means the original stamps nothing on this polygon" | It means *this layer* stamps nothing. Where two coplanar layers are painted over each other the bit selects which one decorates, and the layer beneath does the work — so a flagged polygon is usually decorated, just not by the district you were looking at. Killed by the user at the controls and confirmed at odds ratio 1,037× (`FINDINGS-layer-pairing.md`). This is the row that cost the most: it made the gate look like a fidelity fix that "deletes the skyline", when the deletion was entirely an artifact of `BuriedClutterDistricts` having already removed the replacement. |
| 11 | "`unk3`/`0x800` is the OpenFlight `SUBFACE` mark" (`docs/formats/gamez.md`, decoded 2026-07-23) | It is `no_clutter`, set from a node-name substring in `gg_load.c`, single-writer at `005654f6`. The bit's *measurements* survive; what does not is the reason `SceneBuilder.SubfaceBias` gives for layering it. Corrected in `gamez.md` 2026-08-10, with the depth-order justification explicitly reopened rather than re-asserted. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2, B11, B12, C21, C22 | Confirm the trace against the addresses cited below, then implement. |
| **Direction sound, magnitude a judgement call** | A1, A3, B13 | The *what* is settled; the numbers are what Wave A exists to produce. Do not pre-commit to a magnitude. |
| **Leads only — no mechanism yet** | B14's C2/C4 expectations, C23 | Budget for investigation; either may end in a disproof or a backlog hand-off. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. This plan runs in
`.claude/worktrees/clutter-uv-placement`.

**⚠ Do not link media into the worktree.** `OriginalScreenshots\` and `playtest\` are absent here by
design; read them by absolute path (`Z:\CSVM\playtest\CAP-22\...`), never via a junction — see
CLAUDE.md.

## What the data actually ships

**The original's clutter code.** `D:\zipper\gamez\zclass\cls_clutter.cpp`, at roughly
`0x004dd1b0`–`0x004df200` in `crimson.exe` (read via the Ghidra MCP, 2026-08-09). The chain:

| Address | Role |
|---|---|
| `FUN_00463f40` | Startup. Loads **`templates.zrd`** (`FUN_004de7d0`), then `FUN_004df170` (`ClutterLoadTemplates`), then `FUN_004df1d0`. |
| `FUN_004de7d0` / `FUN_004deab0` | Parse `templates.zrd` into per-model-name property blocks (0x88 bytes each), keyed by the decoration's node name. |
| `FUN_004df170` | For each registered template name, resolve the gamez node and call `LoadTemplate`. |
| `FUN_004dd230` (`LoadTemplate`) | **Per decoration:** ray-cast its local position ±5 along the ground quad's normal onto the quad (`FUN_0055db90`, given the quad's UV array), take the interpolated **texture UV** at the hit, wrap both components into `[0,1)` with `fmod`, and store `{u, v, node, kindBlock}`. Failure path is the `LoadTemplate(): template %s clutter %s does not project to polygon.` string at `0x0062e120`. |
| `FUN_004df1d0` | `srand(0x8EA91836)` → build the whole world's clutter → `srand(time(0))`. **The placement is seeded and deterministic.** |
| `FUN_004de4d0` | Walks the terrain partition grid (world+0x38: `0x98` X count, `0x9c` Z count, `0xa0` rows; each cell `0x3a`/`0x3c` mesh array) plus the world node's own children. |
| `FUN_004de460` | Per node: requires flag bit 2 at +0x24, requires *not* `0x4000000` at +0x2c, dispatches on node type 5 / 6. |
| `FUN_004de2c0` | Per polygon (stride 0x28): skip if flag `0x800`, skip if the UV array pointer at +0x18 is null. |
| `FUN_004de190` | Per **texture layer** of the polygon (`+0x10` count, `+0x14` list): look the template up **by texture name**; triangulate fan or strip; call the stamper per triangle. |
| `FUN_004dd6e0` | **The stamper.** Detailed below. |

**`FUN_004dd6e0`, step by step** — this is the function B12 reimplements:

1. Read the triangle's three UV pairs from the polygon's UV array for this texture layer.
2. Transform the three world vertices; compute the plane normal.
3. If the kind block authors slope limits, reject when `normal.Y` (clamped to `[-1,1]`) falls
   outside `[kind+0x58, kind+0x5c]`. **Defaults are −1.0 and +1.0 — no cull.**
4. Take the **UV** bounding box of the triangle and `floor` it to integers.
5. For every integer cell `(uInt, vInt)` in that box: candidate `= (uInt + entry.u, vInt + entry.v)`,
   plus an independent uniform jitter per axis from `translate_uv_range` (kind+0x1c/+0x20 for u,
   +0x24/+0x28 for v).
6. Point-in-triangle test **in UV space** (three edge cross-products, all negative).
7. Recover world XYZ through the affine UV→world map built from the triangle.
8. Height/occupancy probe via `FUN_004c76e0`; bucket into the terrain grid by X.
9. Pick the model: the entry's own node, or a weighted `substitute` roll.
10. Build the transform: random rotation about three axes from `rotation_range`, **or** align to the
    polygon normal when `align_normal` is set; then a uniform scale from `scale_range`.
11. Compute the squared near/far fade distances from `far_fade_range`; register the instance.

**`templates.zrd` — the per-kind config, extracted and read by nothing.** Keys the parser accepts:
`scale_range`, `translate_uv_range`, `far_fade_range`, `rotation_range`, `min_slope`, `max_slope`,
`align_normal`, `substitute` (a weight/model list), and `OnWeaponHit` / `OnCrater` / `OnCollide`
(each a health + debris model). What the shipped files use:

| Chapter | `extracted/<C>/zrdr/templates.zrd.json` | Notable |
|---|---|---|
| C1 | 1,657 B — `firtree1`, `firtree2`, `dougfirtree1`, `bush1`, `bush2` | `firtree1` substitutes 9:1 to `firtree2`; bushes 1:1; `scale_range` 0.9–1.5 |
| C1B | 795 B | — |
| C1C | 4 B (empty) | C1C registers no templates |
| C2 | 12,250 B — palms, `brush1-4`, `filmbuild01-09`, `resbuild01-27`, `c_studebaker1-6` | 50 kinds; `spruce` substitutes to three brush models; `scale_range` up to 1.0–3.0 |
| C2B | 4 B (empty) | but its boot script registers six templates |
| C3 | 992 B — `palmtree1/2/3` | three-way even substitution, `scale_range` 0.7–1.3 |
| C4 | 1,148 B — `firtree1/2`, `dougfirtree1`, `spruce` | — |
| C5 | 22,956 B — `lightpole`, `w_lightglow`, `hotelsign0-2`, `cb00a/b`, `cb00det01-03`, … | `cb00a` substitutes 1:1 with `cb00b`; `far_fade_range` [[200,300],[300,350]] |

**No chapter authors `min_slope`, `max_slope`, `align_normal`, `rotation_range` or
`translate_uv_range`** — all five default everywhere. ✅ **Settled 2026-08-10**: C2's and C5's files
were the open case here and they were checked, so C21 does *not* need to re-run this. Full per-chapter
counts are in B14's landed section; the consequence — that `FUN_004dd6e0`'s step 5 and the
rotate/align half of step 10 are **inert on retail data**, so the original's placement has no random
input affecting position or orientation, which is why C1's tree positions come out *exactly* the
original's — is written up there and in C23. What IS authored and unapplied: `scale_range` (143
blocks), `far_fade_range` (143), `substitute` (41). ⚠ **143, not 148** — C21 counted the shipped
files and the per-chapter table below sums to 143; the 148 first written here was arithmetic.

**Which templates each chapter registers** (`extracted/interp.json`, `AddClutterTemplates` lines —
this is the census that corrects claim 6 above):

| Chapter | Registered templates |
|---|---|
| C1 | `terpat02`, `river1`, `river2` |
| C1B | `rockclut` |
| C1C | *(none)* |
| C2 | `terpat01`, `terpat04`, `terpat04-128`, `filmblock1-5`, `parklot1`, `parklot2`, `parkpat`, `resblock1-6` |
| C2B | `terpat01`, `terpat03`, `terpat04`, `resblock2`, `filmblock1`, `filmblock2` |
| C3 | `cliff1_sandtrans` |
| C4 | `terpat01`, `terpat01-128`, `river5` |
| C5 | `cblock1-7` (minus `cblock4/5/6`, `BL-250`'s exemption) |

Note the mismatch worth explaining before trusting either file: C3 registers only a cliff template
yet its `templates.zrd` describes palms, and C2B's `templates.zrd` is empty while it registers six
templates. Neither is a bug in the extraction; both are facts the reader must tolerate.

**What the remake does today.** `CSVM/src/Mech3/Clutter.cs`:
`GroundInfo` (`:569-583`) reduces the ground quad to a scalar `Period` = the larger of its X/Z
extents; `ParseTemplate` (`:498-502`) stores each decoration's origin as metres relative to the
quad's min corner; `PlaceOnTriangle` (`:324-371`) walks the world-space grid `gx*p + cell.Origin.X`,
tests containment in XZ, and interpolates the surface Y barycentrically. `PlaceOnMesh` (`:669-699`)
never reads `poly.UvCoords` at all. There is no randomness anywhere in the file — grepping
`rand|scale|fade` returns zero hits — so every instance is the same model at the same scale.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Ground truth (measure before changing anything)

1. ☑ Measure C1's `terpat02` UV repeat in world metres against the 512 m grid constant
2. ☑ Verify the template ground quad's UV parameterisation and the UV→world orientation
3. ☑ Census the world's clutter-eligible polygons: layers, UV coverage, coplanar overlaps

### Wave B — The placement rewrite

11. ☑ Store decoration positions as template-quad UVs, not metres
12. ☑ Replace the world-space grid stamp with the per-triangle UV-lattice walk
13. ☑ Settle the two remake-only rules: `MinSlopeCos` and the `seen` dedup
14. ☑ Chapter A/Bs + the 8-chapter regression, and rewrite the class comment
15. ☑ **Land `BL-305`'s fix: the `no_clutter` gate + retire `BuriedClutterDistricts`** (added
    2026-08-10, after the mechanism was confirmed at the controls; runs *before* 14, which is the
    close-out item and now has this build to regress)

### Wave C — The authored per-kind data

21. ☑ `templates.zrd` reader + `docs/formats/templates.md`
22. ☑ Apply `substitute` and `scale_range`
23. ☐ Survey `far_fade_range`, `rotation_range`, `translate_uv_range` → decide or hand to backlog

## Dependency and parallelism notes

**A2 blocks Wave B outright** — it is the gate `BL-305` and `analysis/bl-058-clutter-doubling`'s
`match_footprints.py` dead end both name, and B11/B12 are unverifiable without it. A1 and A3 are
independent of each other and of A2, so Wave A's three items can run concurrently; all three are
read-only analysis and touch no source file.

B11 → B12 is a strict chain on the same file. B13 depends on B12 (it removes code B12 rewrites).
B14 is last and needs B11–B13 landed. **File contention: B11, B12 and B13 all edit
`CSVM/src/Mech3/Clutter.cs` — never run them in parallel worktrees.**

Wave C is independent of Wave B's placement change *in code* (it adds a reader and consumes it at
kind-construction time), but must land **after** B14, because running both at once makes the A/B
unreadable — a density change and a model-variety change are indistinguishable in a screenshot.
C21 → C22 is a chain; C23 is analysis only and can run any time after C21.

---

# Wave A — Ground truth

## A1 ☑ Measure C1's `terpat02` UV repeat in world metres against the 512 m grid constant

**Landed** (`analysis/bl-305-clutter-uv/uv_repeat.py` + `FINDINGS-A1.md`). C1's `terpat02` measures
min 184.7 m, area-weighted median **259.6 m**, max 461.6 m per UV repeat. **100 % of that terrain's
world area falls below the remake's 512 m constant and 0 % above** — so the original plants between
1.2× and 7.7× as many trees as we do, typically **3.9×**. The user's report that C1's trees are thin
is confirmed with a mechanism and a number.

**Verified.** Two self-checks run on every invocation and were both shown able to fail (METHOD-9): a
synthetic 1:1-UV triangle over a 256 m span reports exactly 256.0000 m, and the same triangle with
its UV span halved reports 512.0000 m. Plus a per-texture area-reconstruction identity
(`sum uvArea × rate²` = `sum worldArea`), passing on every row within float tolerance. Exit code
carries the verdict (LOG-12). 1,617 of 4,596 `terpat02` candidate triangles were dropped for zero
**world** area — repeated-vertex slivers at LOD stitches, traced to node `g16333`'s fan
triangulation, reported rather than skipped (DIAG-15). Zero triangles anywhere were dropped for a
missing or malformed UV array.

**Three findings the item did not ask for.**

1. **The `256..1280 m` claim in `Clutter.cs:23-25` is dead.** Measured across every terrain texture
   in C1/C1B/C2/C3/C4 the floor is 61.8 m (below the claimed 256) and the ceiling 461.6 m (against a
   claimed 1280 — off by more than 2.7×). The comment's *shape* — real non-uniformity over a
   several-hundred-metre range — holds; its bracket does not. Re-derived, not inherited (DIAG-8).
   This goes into B14's comment rewrite.
2. **C5's `cblock1/2/3/7` measure 256.0 m against a 256 m quad period — a ~1 % match.** The spacing
   the remake already uses for C5 is *correct*. `BL-305`'s packing defect therefore cannot be a
   density error, and must live in the alignment A2 is measuring. This is a disproof of the reading
   this plan was expected to confirm for C5, and it makes A2 load-bearing rather than merely
   gating.
3. **The quad-period-vs-world-repeat ratios cluster at 1.00, 1.41 and 2.00.** C2's and C4's terrain
   templates and all of C5's city blocks are correct; **every C1 template is off by exactly 2**
   (`terpat02` 512/259.6, `river1` 256/128.2, `river2` 256/128.0, and C1B's `rockclut` 256/126.6) —
   a clean 4× density loss, which is why C1 is the chapter the user noticed. Three unrelated
   templates in two chapters sit at √2 (`filmblock1`, `parklot1`, `cliff1_sandtrans`); one outlier,
   C2's `parkpat`, at 3.20 (512/159.9) — a 10.2× loss.
   **⚠ Corrected by A2.** This item read the √2 as a 45°-rotated UV mapping and the exact 2 as a quad
   spanning two texture repeats. Both were handed to A2 as leads and **both are disproven**: there is
   no 45° mapping anywhere in the install, and every quad spans exactly 0..1. The √2 was an artifact
   of this item's own statistic (`Period` = max extent, compared against a geometric mean, on a 2:1
   quad). What survives is the C1 factor-2 density loss, which is real and is a world-vs-template
   scale mismatch.

**⚠ For Wave B.** The remake's error is **not uniform across the game** and cannot be corrected by
changing one constant — C2, C4 and C5 are already right and must not move. Any Wave B change that
shifts C5's instance count is a regression, not progress.

### Original approach (kept for reference)

**Goal.** A number, with a distribution: across C1's `terpat02`-textured world polygons, how many
world metres does one full UV repeat span, and how does that compare to the 512 m the remake
currently uses? That number predicts the density ratio between the original and us, per region.

**Evidence (confidence: direction-sound, magnitude unknown — that is the point of the item).**
The original's instance count over a patch is (decorations in the template) × (UV repeats over the
polygon); ours is (decorations) × (area / 512²). `Clutter.cs:23-25` asserts the world's UV tiling
runs **256–1280 m per repeat**, which brackets 512 on both sides — so the current constant is
under-dense in some regions and over-dense in others, and the user's report ("C1 trees are not as
dense as the original") says the places they fly are the under-dense ones. That assertion's own
provenance is not cited in the comment; **re-derive it, don't inherit it** (DIAG-8).

**Approach.** Static analysis over `extracted/`, no engine run — an `analysis/bl-305-clutter-uv/`
script in the style of `analysis/collider-probe/probe.py`. For every world polygon in C1's gamez
whose material texture is `terpat02.tif`: compute the triangle's world-space area and its UV-space
area, and report `sqrt(worldArea / uvArea)` as the metres-per-repeat for that triangle. Output an
area-weighted histogram, the min/median/max, and the fraction of world area on each side of 512 m.
Repeat for C5's `cblock1/2/3` and `cblock7` textures — the same number is what predicts whether
`BL-305`'s packing is a spacing error or an alignment error, and it costs nothing extra here.

Do **not** modify `Clutter.cs` in this item.

**Model recommendation.** medium. Mechanical measurement over already-extracted data with a clearly
specified statistic; the judgement is in reading the histogram, not producing it.

**Verify.** The script's own self-check: the summed UV-space areas times the metres-per-repeat
should reconstruct the summed world area to within float tolerance, and a synthetic triangle with a
known 1:1 UV mapping over a 256 m span must report 256. METHOD-9 — show the instrument can fail
before trusting its answer.

**⚠ Traps.** (a) **WORLD-15** — establish each subtree's coordinate frame before applying
transforms. Clutter polygons come from both `world.Children` and `world.PartitionNodes`, walked with
accumulated local transforms; a triangle measured in the wrong frame gives a plausible wrong metre
figure with nothing to flag it. (b) A polygon can carry more than one texture layer; measure the
layer whose texture matches the template, not layer 0 unconditionally. (c) UV area is signed and can
be near zero on degenerate polygons — clamp and report the count you dropped rather than skipping
silently (DIAG-15). (d) This number is not by itself the density ratio: the remake's `seen` dedup and
`MinSlopeCos` also remove instances. Report the UV figure alone and let A3/B13 account for the rest.

## A2 ☑ Verify the template ground quad's UV parameterisation and the UV→world orientation

**Landed** (`analysis/bl-305-clutter-uv/uv_orient.py` + `FINDINGS-A2.md`). **Wave B is not blocked.**

**(i) Every template ground quad spans exactly 0..1 in both UV axes — all 32 that resolve,
install-wide.** So `FUN_004dd230`'s `fmod` wrap folds nothing and does mean "fractional position
across the quad". Trap (b) of this item is closed: it does not bite. Every quad is flat, one polygon,
four corners, normal +Y — but the corner *winding* differs between templates, so anything keying off
"corner 0" rather than off the UVs reads two quads inconsistently.

**(ii) A world polygon's UV axes are axis-aligned (~95 %) but NOT consistently oriented, and it does
not matter.** C1's `terpat02` uses eight frames, dominated by +U → world **+Z**, +V → world **−X** at
69.6 % — a 90° rotation from the template quad — with handedness split 1,441 mirrored / 1,538 not.
C5's `cblock1` is 97.7 % identity and 100 % one handedness. **No rule predicts the frame, and none is
needed:** `FUN_004dd6e0` steps 4–7 never reference a world axis. The clutter rotates, mirrors and
stretches *with the ground texture*, which is the whole intent — a building sits in its painted block
wherever that block lands.

That is also why `match_footprints.py` could not have succeeded: it compared decoration positions to
the painted texture in **world** metres, and the world frame is not the frame the positions live in.
The mapping was never missing; it is per triangle, and there are 2,979 of them for `terpat02` alone.

**Verified.** METHOD-9 on the instrument itself: a synthetic quad is fed three ways — plain, U/V
swapped, V-flipped — and the three must report different answers, so a script blind to the UVs would
fail its own check. METHOD-11 honoured: the flat/gentle/sloped split changes the answer (a flat-only
sample of `terpat02` reads 73.6 % for the dominant frame; the sloped population reads 53.6 % with
12.5 % off-axis), so concluding "consistent" from either alone would have been wrong. METHOD-1 worked
example below, hand-computed and matched.

**The worked example, for B12's test case.** C1 node 5909 `firtree1.flt` at local
(106.862, 0, 66.780) → quad UV (0.708715, 0.630430), wrap a no-op. Stamped on C1 node 2911 `g777`,
model 953, poly 3, tri 5: affine **A = (0, 0, 256)** per +1 U, **B = (−256, 0, 0)** per +1 V; lattice
uInt ∈ {0,1}, vInt ∈ {0,1} → 4 candidates, 1 inside → world **(−9377.390, 128.000, −3402.569)**. Hand
arithmetic and script agree; the affine route and an independent barycentric route agree to 1.8e-12 m.

**⚠ Two findings that change Wave B — both folded into B11 and B12 below.**

1. **`GroundInfo`'s scalar `Period` is wrong on 4 of the 32 templates**, so **B11 is not a pure
   refactor**. On 28 templates `(origin − min corner) / Period` is an exact relabelling of the quad UV
   (float noise, 1.1e-16). On `filmblock1` (64×128), `cliff1_sandtrans` (128×64), `parklot1` (16×32)
   and `parklot2` (32×16) it is not — `Period` = max extent applies the long side's scale to both
   axes, and `parklot1/2` are additionally **UV-mirrored**. Worst error 0.74 UV. `Period` must be
   replaced by the quad's own two-axis **signed** UV→local map, not a scalar.
2. **A zero-area *world* triangle can carry a nonzero *UV* area** — fan artifacts of n-gons with
   repeated or collinear corners. Their affine map is finite but meaningless (both axes collapse onto
   a line); leaving them in inflated C1's metres-per-U maximum from 561 m to 32,768 m. A UV-area guard
   alone does not catch them.

**Also settled:** not one decoration in the whole install fails to project onto its quad, so
`FUN_004dd230`'s "does not project to polygon" error path is never exercised by retail data — still
implement it as skip-and-log, but no chapter tests it. And **there is no global "the clutter grid
starts here" origin** in the original; `Clutter.cs:339-348`'s `gx * period` world grid is a fiction
with no counterpart.

**Not determined.** That mech3ax's `uv_coords` are byte-identical to the array `FUN_004de2c0` reads
is inherited from `docs/formats/gamez.md:9`, strongly corroborated but not re-derived. Whether the
four mis-parameterised templates visibly misplace anything today needs a build — B11's A/B shows it.

### Original approach (kept for reference)

**Goal.** Establish, for at least one `terpat` template and one `cblock` template, (i) what UV range
the ground quad's own vertices span, and (ii) whether a world polygon's UV axes correspond to its
world axes in a consistent, discoverable way. Without this the UV rewrite cannot be checked against
anything.

**Evidence (confidence: traced — the mechanism is known; only the shipped values are open.)**
`FUN_004dd230` projects each decoration onto the ground quad and reads the **interpolated UV** at the
hit point, then wraps to `[0,1)`. That is only equivalent to "fractional position across the quad"
if the quad's UVs actually span 0..1 over its extent — which is likely but unverified. The matching
world-side question is the one that killed the earlier footprint attempt:
`analysis/bl-058-clutter-doubling/FINDINGS.md:121-127` records `match_footprints.py` as
**inconclusive** precisely because "this would need the ground quad's UV-to-world orientation
verified before trusting a positional match". This item is that verification, finally done.

**Approach.** Extend A1's script. For each template root resolved by `ClutterBuilder.FindTemplateRoot`
(C1 `terpat02`, C5 `cblock1`, C2 `resblock1`): dump the ground quad's vertices with their UVs, and
report the affine map between them. Then, for a sample of the world polygons those templates
decorate, report the UV axes as world-space vectors (the same affine map `FUN_004dd6e0` builds at
step 7) — is +U consistently world +X, is it rotated per polygon, does it flip?

The deliverable is a short `analysis/bl-305-clutter-uv/FINDINGS.md` section stating the answer
plainly, because B12 is written directly against it.

**Model recommendation.** high. This is the item the whole plan is gated on and the one where a
plausible-but-wrong reading propagates furthest; it needs the judgement to notice when the data does
*not* say what the hypothesis wants.

**Verify.** Pick one decoration in one template, compute by hand where the original would place it on
one named world triangle, and check that against the script's output. One worked example, written
into FINDINGS.md, is worth more than an aggregate here (METHOD-1).

**⚠ Traps.** (a) A V-flip between the extraction's UV convention and Godot's is exactly the kind of
error that produces a *self-consistent but mirrored* city; state the convention explicitly and test
it, don't assume it. (b) If the quad's UVs do **not** span 0..1, the `fmod` wrap in `FUN_004dd230`
changes meaning — a quad spanning 0..2 folds two decoration groups onto each other. Report what the
data actually spans. (c) Resist concluding "orientation is consistent" from a sample of flat ground;
sample hillsides and the C5 street grid too (METHOD-11). (d) Do not use this item to start fixing
anything.

## A3 ☑ Census the world's clutter-eligible polygons: layers, UV coverage, coplanar overlaps

**Landed** (`analysis/bl-305-clutter-uv/eligibility.py`, `_raw_output.txt`, `FINDINGS-A3.md`).

| chapter | polys | layer-0 | layer-1+ | remake l0 | coplanar (real) | sim sprites | sim solids |
|---|---|---|---|---|---|---|---|
| C1 | 23,959 | 883 | 0 | 883 | 0 | 9,303 | 0 |
| C1B | 12,790 | 9 | 0 | 9 | 0 | 60 | 0 |
| C1C | 11,991 | 0 | 0 | 0 | 0 | 0 | 0 |
| C2 | 15,037 | 964 | 0 | **1,034** | 0 | 37,167 | 10,261 |
| C2B | 10,197 | 0 | 0 | 0 | 0 | 0 | 0 |
| C3 | 18,127 | 101 | 0 | 101 | 0 | 371 | 0 |
| C4 | 25,793 | 1,068 | 0 | **1,143** | 0 | 88,630 | 0 |
| C5 | 34,551 | 1,761 | 0 | **2,049** | 0 | 124,074 | 71,326 |

**Verified (METHOD-15).** The script ports `ParseTemplate`/`PlaceOnWorld`/`PlaceOnMesh`/
`PlaceOnTriangle` in full and was diffed against headless `--freecam` builds. C1 exact (9,303
sprites, all five kinds). C5 exact on solids (71,326); **off by one** on `lightpole.tif` and
`poleflare.tif` (live 62,036 vs sim 62,037 each) — a float32-vs-float64 boundary case where one
candidate sits exactly on a triangle edge. 0.0016 % of C5's instances, reported rather than rounded
away, and the script's C5 verdict is a deliberate FAIL rather than a laundered pass.

**Two checked zeros.** (a) **No polygon in the install lacks a UV array**, so `FUN_004de2c0`'s null-UV
gate never fires. (b) **No polygon's layer 1+ ever names a registered template**, so the
"two-layer polygon stamped twice" mechanism never fires on retail data. Wave B must still iterate
layers to match the original, but **no chapter exercises that path, so an A/B cannot verify it and no
observed change may be credited to it.**

**⚠ The headline finding — B13's rule is inverted, and B13 is amended below.** The coplanar
double-stamp count is 0 in every chapter, but *structurally*: polygon flag `0x800`, the bit
`FUN_004de2c0` skips on, **is the already-decoded subface mark** (`docs/formats/gamez.md`). The
original excludes every subface polygon before texture matching, so it cannot double-stamp. A
diagnostic ignoring that gate finds the overlap is real and large — C5 787,546 raw pairs from 449
subface polygons, C2 876 from 18, C4 69 from 33 — which proves the zero is the flag working, not a
broken script. **But `ClutterBuilder.PlaceOnMesh` never reads the subface flag at all**, so the
remake *does* reach both members, and the `seen` set is what suppresses the double-stamp today. That
is also the `remake l0` − `layer-0` delta in the table: +70 C2, +75 C4, **+288 C5**.

**One doc gap.** `node+0x2c` / `0x4000000` from `FUN_004de460` is not named in `docs/formats/gamez.md`;
it maps to `nodes.json`'s `update_flags` (checked against `tools/mech3ax`'s node struct) and is set on
3 retail nodes, all C2, where it is always redundant with the decoded `flags.active`. Worth a line in
`gamez.md` at B14; not worth a behaviour change.

**⚠ For Wave B.** The remake's gate is **looser** than the original's on two axes (no subface
exclusion, no active/node-type check), so it **over**-stamps — the *opposite sign* to A1's finding
that it **under**-stamps by up to 4× in C1. These are different mechanisms on different chapters:
C1's deficit is spacing on 883 subface-free polygons; C5's surplus is gating on 288 polygons whose
spacing is already correct. **Do not net them against each other**, and do not accept "the instance
count moved the right way" as a check — it would hide both.

### Original approach (kept for reference)

**Goal.** Know how many polygons the original would stamp that we currently do not, and vice versa —
before the rewrite, so B14 has a baseline that can be compared.

**Evidence (confidence: direction-sound).** The original's eligibility gates are explicit:
`FUN_004de2c0` skips a polygon with flag `0x800` or a null UV array; `FUN_004de190` iterates **every
texture layer**, so a two-layer polygon whose layers both name templates is stamped twice; and
`FUN_004de460` gates the node on flag bit 2 at +0x24 and the absence of `0x4000000`. The remake's
`PlaceOnMesh` matches only the material's single `TextureName`, and `PlaceOnWorld` gates on
`WorldBuilder.SkipWorldNode` plus an LOD rule. These are different sets; nobody has measured by how
much.

**Approach.** Same analysis script. Per chapter, report: polygons whose texture names a registered
template, split by texture-layer index; polygons with no UV array; coplanar/subface pairs where both
members would stamp (the `BL-250` shape, but counted rather than assumed); and the count the remake's
current gate produces, for the difference. Cross-check the gamez flag bits against
`docs/formats/gamez.md` — if `0x800` and `0x4000000` already have decoded names there, use them; if
not, say so rather than inventing one.

**Model recommendation.** medium. Counting against a specified rule set, with the interpretation
deferred to the FINDINGS write-up.

**Verify.** The remake-side count the script computes must equal the `Summary` line's per-kind totals
from a real `--freecam --chapter=C1` build. If the static census and the live build disagree, the
census is wrong and nothing downstream can use it (METHOD-15).

**⚠ Traps.** (a) **WORLD-25** — a bigger census is not proof of better coverage; a layer-2 match may
be a decal the original also declines to decorate. (b) The coplanar count is *not* a licence to
change `BuriedClutterDistricts`; `BL-250` is closed on capture evidence and stays closed. (c)
LOG-5 — report any cap or truncation in the census; an absent category must be distinguishable from
a truncated one.

# Wave B — The placement rewrite

## B11 ☑ Store decoration positions as template-quad UVs, not metres

**Landed** (`CSVM/src/Mech3/Clutter.cs`, `CSVM.Tests/ClutterQuadUvTests.cs`). `GroundInfo` no
longer returns a scalar `Period`: it returns the ground quad's **two-axis signed UV map** — the
polygon's plane plus the affine gradients `d(u)`, `d(v)` per metre of local displacement — and
`ParseTemplate` projects each decoration onto that plane along the quad normal, reads the
interpolated texture UV, and wraps it with `FUN_004dd230`'s `fmod` (both branches, including the
exact-1.0 collapse). `Kind.CellPlacements` now carries `(u, authored Y, v)`; the basis and Y are
untouched. `Template.Period` is gone; `PlaceOnTriangle`'s grid survives until B12 but is now
**per axis** — step `extent`, offset `uv × extent`. `FirstWithMesh`/`GroundInfo` became public
statics so a test can read the stored UV; nothing in the rendering half was touched.

**Verified.** Prediction stated first (METHOD-12): C1/C1B/C4/C5 byte-identical, C2 and C3 change,
and the changed instances exactly `filmblock1`, `parklot1`, `parklot2`, `cliff1_sandtrans`. The
baseline was taken on the unchanged tree first and re-measured (METHOD-3); the pre-change goldens
were **all 13 identical**, so no mover is inherited (GOLD-8).

| chapter | sprites base → now | solids base → now | per-kind entries moved |
|---|---|---|---|
| C1 | 9,303 → 9,303 | — | 0 of 7 |
| C1B | 60 → 60 | — | 0 of 3 |
| C2 | 37,167 → 37,167 | 10,261 → **10,328** | **8 of 87** |
| C3 | 371 → **678** | — | **1 of 1** |
| C4 | 88,630 → 88,630 | — | 0 of 9 |
| C5 | 124,072 → 124,072 | 71,326 → 71,326 | 0 of 39 |

**The 28/4 split held exactly.** The eight moved C2 entries are `filmbuild01` (6→10) and
`filmbuild02` (12→20) — `filmblock1`'s only two kinds, its other four film lots untouched — and
all six `c_studebaker*` kinds, which exist only under `parklot1`/`parklot2` and occupy one
contiguous run in the build order. C3's single kind is `cliff1_sandtrans`'s. Nothing else moved
anywhere.

**Two goldens moved: `c2-city` and `c3-island`, and only those.** Both are chapters where a named
template was predicted to move; left un-re-blessed for the orchestrator. Deliberate control
(METHOD-9/METHOD-10): perturbing one stored UV by +0.25 moved **8** goldens, including
`c1-waterfall`, `c1b-night-sea`, `c1-flight`, `c1-crash`, `c4-snow` and `c5-city-night` — so every
"identical" above is a check that could have failed. Perturbation reverted and the tree proved
clean with `git diff` (METHOD-17). `.\RunTests.ps1`: build PASS, 840 units PASS (3 new), 29
in-engine suites PASS with engine errors clean, goldens FAIL on those two shots alone.

**⚠ One finding the item did not ask for: the quad map must be evaluated in DOUBLE.** Written in
float it moved `c1-flight` — a chapter that must not move. The cause is not the algorithm but the
rounding: on `terpat02` the map is anchored at the corner carrying UV (1,1), so `u` comes out as
`1 + (x − 256)/512`, and that intermediate rounds separately from the old `x + 256`. The error is
one ulp, ~0.03 mm, invisible in every instance count and enough to flip pixels in a forest shot.
Computed in double and rounded **once**, `u × extent` reproduces the old metres bit for bit,
because every shipped quad extent is a power of two and that scaling is exact. `c1-flight` came
back identical on the next run, which is also the proof the fix was the thing that mattered
(METHOD-15). This is a trap for B12: any further arithmetic on these UVs is one rounding away
from moving a chapter that should not move.

**Two things this could not verify.** (a) `FUN_004dd230`'s "does not project to polygon" path is
implemented (skip + one summary log line, never a silent (0,0)) but **no chapter exercises it** —
0 decorations off-quad install-wide, matching A2's `notproj` column, and the log sink demonstrably
carries `[world]` lines, so the zero is a measurement and not a blind spot. (b) The `fmod` negative
branch is likewise untaken by retail data; it is covered by a unit test instead.

### Original approach (kept for reference)

**Goal.** `Kind.CellPlacements` carries each decoration's position as a `[0,1)` UV pair (plus the
authored basis and Y that the solid path still needs), derived the way `LoadTemplate` derives it.
Behaviour is unchanged in this item — the grid stamp is still in place, just fed from UVs.

**Evidence (confidence: traced).** `FUN_004dd230`: project the decoration's local position onto the
ground quad along the quad normal, read the interpolated UV, wrap with `fmod` into `[0,1)` (including
the negative branch, which maps to `1 − frac`). The remake's equivalent is `ParseTemplate` at
`Clutter.cs:498-502`, which subtracts the quad's min corner in metres, and `GroundInfo` at `:569-583`,
which produces the scalar `Period` that becomes meaningless here.

**Approach.** Rewrite `GroundInfo` to return the quad's **two-axis signed UV→local affine map**
instead of `(Period, Min)` — A2 established that a scalar cannot describe four of the 32 templates —
and rewrite the `cell` construction in `ParseTemplate` to project and wrap. **Delete
`Template.Period` and the comment at `:928`**: A2 found no remaining use for it.

**⚠ Amended after A2 — this item is NOT behaviour-preserving.** The plan originally called for
landing it as a provably inert step with byte-identical output. That is achievable on 28 of the 32
templates and *impossible* on the other four, because the current scalar rule genuinely misplaces
them: `filmblock1` (64×128 quad), `cliff1_sandtrans` (128×64), `parklot1` (16×32, UV-mirrored) and
`parklot2` (32×16, UV-mirrored), worst error 0.74 UV. So the shape of the step becomes: **the
28 square, unmirrored templates must be byte-identical, and exactly four templates — C2's parked
Studebakers and film-lot buildings, C3's palms — must move.** Anything else moving is a bug. State
that split before running the A/B, not after (METHOD-12).

**Model recommendation.** high. Small diff, high blast radius: this is the coordinate-system change,
and a sign or transpose error here surfaces as a plausible-looking city three items later. The
mirrored quads make the sign load-bearing.

**Verify.** METHOD-10 applies with force — a check that passes a no-op does not verify the change.
So: (i) per-kind `Summary` counts and goldens **identical** to `main` for C1, C4 and C5, whose
templates are all in the unaffected 28; (ii) C2 and C3 change, and the changed instances must be
exactly the four templates named above and no others; and (iii) deliberately perturb one
decoration's stored UV by 0.25 and show a golden *does* move, proving the check could have failed
(METHOD-9). A2's worked example (C1 node 5909 on node 2911 `g777` poly 3 tri 5 → world
(−9377.390, 128.000, −3402.569)) is the unit test.

**⚠ Traps.** (a) The `fmod` negative branch matters: `FUN_004dd230` maps a negative coordinate to
`1 − frac`, and additionally collapses the exact-1.0 result to 0.0. Reproduce both, including that
degenerate case. (b) The solid path still needs the decoration's authored **Y and basis** (`Clutter.cs:366-368`) — UV
replaces XZ only. (c) A decoration that does not project onto the quad is an error in the original
(it logs and skips); do not silently place it at (0,0).

## B12 ☑ Replace the world-space grid stamp with the per-triangle UV-lattice walk

**Landed** (`CSVM/src/Mech3/Clutter.cs`, `CSVM.Tests/ClutterQuadUvTests.cs`). **The world grid is
gone.** `PlaceOnTriangle` builds a new public `ClutterBuilder.UvTriangle` per triangle — the UV
bounding box floored to an integer lattice (step 4), a containment test in **UV space** (step 6),
and the affine UV→world map (step 7) — and stamps each decoration once per lattice cell it lands
in. `PlaceOnMesh` now passes `poly.UvCoords` through, indexed by **corner position**, under the
same fan/strip split it already applied to vertices. `Template.ExtentX`/`ExtentZ` are deleted:
after this item a template carries no metric size at all. The affine map supplies **Y** as well as
XZ, per A2 — on a planar triangle it is the same number the old barycentric height gave, and a
triangle cannot be non-planar. `MinSlopeCos` and the `seen` dedup are untouched (B13 owns both).

**Predicted before measuring** (METHOD-12), from A1's per-template metres-per-repeat against each
quad's per-axis extent, with A2's correction that the four non-square/mirrored quads match their
world tiling **per axis** so their factor is 1.0, not A1's √2 artifact:

| chapter | predicted | measured sprites | measured solids | factor |
|---|---|---|---|---|
| C1 | **3.9–4.0×** | 9,303 → **37,510** | — | **4.03×** ✔ |
| C1B | ~4.1× | 60 → **339** | — | **5.65×** ⚠ over |
| C1C | 0 | 0 → 0 | — | — ✔ |
| C2 | ~1.0×, small rise | 37,167 → 37,254 | 10,328 → 10,346 | 1.002× ✔ |
| C2B | 0 | 0 → 0 | — | — ✔ |
| C3 | 1.0× | 678 → 707 | — | 1.043× ✔ |
| C4 | ~1.0× | 88,630 → 88,705 | — | 1.001× ✔ |
| C5 | 1.0× | 124,072 → **127,744** | 71,326 → **75,148** | 1.030× / 1.054× ✔ |

**No chapter moved in a direction A1 did not predict** — every factor is ≥ 1.0, and C1 is the
chapter that moves, by the clean factor of 4 A1 measured. Two magnitude mismatches, both the same
error in the *predictor* rather than in the model: it used A1's **area-weighted median**
metres-per-repeat, which under-weights the fast-stretching patches, so a template with a wide
spread over few triangles comes out low. C1B's `rockclut` is 14 usable triangles spanning 65–163 m
per repeat against a 256 m quad (predicted 4.1×, measured 5.65×), and C2's `parkpat` — 4 triangles,
which A1 itself flagged as thin — went 2 → 70 against a predicted 10.2×. The correct predictor is
Σ(uvArea) / Σ(xzArea/extent²), not (extent/median)².

**C5's change is entirely `cblock7`.** `cblock1`'s `lightpole` came out at **33,682, identical to
the grid's**, and `cblock3`'s at 7,309, likewise identical; `cblock2` moved by 3. `cblock7` rose
12.6 % across the board (`cb12a` 12,069 → 13,595, `cb14a` 13,248 → 14,932, `cb13a` 4,824 → 5,436).
So where A1 measured 256.0 m against a 256 m quad **and** A2 measured a 100 %-identity UV frame,
the lattice reduces to the grid exactly — which is the strongest available check that the two
mechanisms agree where they should. `cblock7` is the family with 2,544 triangles and A1's 5.0 m
sliver, and it is the one that gains.

**Verified.** Baseline taken on `HEAD` (B11) with the same instrument immediately before the
changed run (METHOD-3) and reproduced B11's table exactly. Four new counters, logged as one line
per build (LOG-5, DIAG-15):

| chapter | zero WORLD area | zero UV area | no UV array | over-large lattice | outside source |
|---|---|---|---|---|---|
| C1 | 1,773 | 0 | 0 | 0 | **0** |
| C1B | 6 | 0 | 0 | 0 | **0** |
| C2 | 1,144 | 1 | 0 | 0 | **0** |
| C3 | 100 | 0 | 0 | 0 | **0** |
| C4 | 2,458 | 0 | 0 | 0 | **0** |
| C5 | 8 | 0 | 0 | 0 | **0** |

The zero-**world**-area counts reproduce A1's independently-derived `dropped_degenerate` figures
**exactly** on four chapters — C1 1,617 + 38 + 118 = 1,773; C3 100; C4 50 + 1,981 + 427 = 2,458;
C5 6 + 2 = 8 — which is a cross-check between a Python static analysis and the live engine walk
that neither could fake. C2 reads 1,144 against A1's condensed 1,133, the difference sitting inside
the per-template lines A1 abbreviated. **Every instance landed inside its own source triangle in
every chapter** (asserted per placement, in the triangle's own plane rather than an XZ projection),
and the generous 4,096-cell lattice bound never fired anywhere, which is what A1's data said should
happen. One C2 triangle has world area and no invertible UV map — the first of its kind found in
the install; A2's sample had none.

**Eight goldens moved and are deliberately left red** (GOLD-8; not re-pinned, not regenerated —
the user decides against images, which are in `.scratch/goldens/`): `c1-waterfall`, `c1-flight`,
`c1-crash`, `c1b-night-sea`, `c2-city`, `c3-island`, `c4-snow`, `c5-city-night`. Five held:
`c1c-rain` and `c2b-rain` (neither chapter places clutter — the two structural controls),
`viewer-bhawk`, `empty-stage`, and **`c1-destroy-effects`**, which was predicted to move and did
not. That is the one golden prediction that missed; the shot is a close-in explosion pose in a
chapter whose forest quadrupled, so "no clutter in frame" is the likely reason, but it was not
confirmed. `.\RunTests.ps1`: build PASS, **843 units PASS** (3 new), 29 in-engine suites PASS with
engine errors clean, goldens FAIL on those eight alone.

**Even at factor 1.0 a golden moves**, and that is the point: the grid was phase-locked to the
world origin and the lattice is phase-locked to the painted texture. C5's near-flat count with a
moved image is a phase change of exactly the kind A1 predicted when it found C5's spacing already
correct.

### ⚠ B12 does not fix `BL-305`. Measured, not assumed.

The orchestrator re-shot `BL-305`'s **own founding pose** — CAP-22's scale-matched nadir,
`--freecam --chapter=C5 --pos=-9490,230,-3300 --direction=0,-1,0.001 --no-fog`, the pose behind
`playtest/CAP-22/ours-nadir-230-scale-matched.png` — on B11 and on B12. The two frames are
**byte-identical (md5 8820B741…)**, while the two binaries are provably different (clutter build
phase 103.0 ms vs 157.5 ms in the same runs). Over the `cblock1/2/3` downtown the UV lattice
reduces *exactly* to the old grid, which is the same agreement B12 found in the counts:
`cblock1`'s `lightpole` came out at 33,682, identical to the grid's, because A1 measured 256.0 m
against a 256 m quad and A2 measured a 100 %-identity UV frame there. C5's whole instance rise is
`cblock7`, elsewhere in the map.

So the C5 golden moved and `BL-305`'s pose did not, and both are consistent: **the packing defect is
neither a spacing error (A1) nor an alignment error (A2/B12).** It is still open, and this plan has
now eliminated the two mechanisms it was built to test for C5. Two candidates remain, in order of
strength:

1. **The missing subface gate (A3).** `PlaceOnMesh` reads no subface flag where `FUN_004de2c0`
   skips one, and A3 measured **+288 extra eligible polygons in C5** because of it. That is
   `B13`'s work, which is therefore no longer a cleanup item — **it is the leading `BL-305`
   candidate** and should be verified against this nadir pose, not just against instance counts.
   **⚠ B13 measured it: the gate DOES move the pose** (`8820B741…` → `A504DC7C…`), the first
   change in this plan that does — but alone it empties C5's downtown, and the state that looks
   like CAP-22 is the gate **coupled with retiring `BL-250`'s `cblock4/5/6` exemption**
   (`1050CEEE…`). Not landed: that coupling is a user decision. Read B13 before touching this.
2. **`far_fade_range` (C23, currently deferred by Decision 3).** `cb00a` fades at 200–300 → 300–350 m.
   At 230 m altitude the frame edges sit 400 m+ in slant range and would be **gone in the original**
   while present in ours, which would make some of "pavement between buildings" a fade artifact
   rather than a placement difference. Decision 3 deferred this because it confounds density A/Bs;
   that reasoning still holds for Wave B, but it is now a live `BL-305` hypothesis rather than a
   tidy-up, and C23 must weigh it as one.

**The pose is the instrument for both.** Any future claim to have fixed `BL-305` must move
`8820B741E6CFB29A5E82CFED048711E0`.

**Build cost.** The clutter phase grows with the instances it places and nothing else: C1
46 → 66 ms, C2 53 → 72 ms, C3 20 → 22 ms, C4 55 → 85 ms, C5 96 → 158 ms, C1B 20 → 22 ms. Total
startup on C5 3,812 → 4,079 ms. No cliff — C1 quadrupled its trees for 20 ms.

**Three things this could not verify.** (a) The original's **per-texture-layer** loop
(`FUN_004de190`) is still not reproduced: `PlaceOnMesh` reads `materials[0]` only. A3 measured that
no polygon in the install names a registered template on layer 1+, so the path is unreachable on
retail data and an A/B could not tell the difference — but it is a deviation, and B14 should say so
in the class comment. (b) Whether the new positions are *right* against the original, as opposed to
right against the decode, needs footage; that is B14. (c) `c1-destroy-effects` not moving is
unexplained, above.

### Original approach (kept for reference)

**Goal.** `PlaceOnTriangle` stamps at the UV lattice, exactly as `FUN_004dd6e0` steps 4–7 do, and
`PlaceOnMesh` feeds it the polygon's per-vertex UVs for the matching texture layer.

**Evidence (confidence: traced).** The 11-step breakdown of `FUN_004dd6e0` above; steps 4, 5, 6 and 7
are this item. Step 5's jitter belongs to C23 (`translate_uv_range`), step 9's substitution to C22,
step 10's rotation/scale to C22, step 11's fade to C23 — **implement steps 4/6/7 only** and leave
hooks, or this item becomes the whole plan.

**Approach.** Replace the `gx/gz` loops at `Clutter.cs:339-348` with: UV bounding box of the triangle
→ `floor` to integers → for each `(uInt, vInt)` and each kind/cell, candidate UV = `(uInt + u, vInt + v)`
→ UV-space edge test → affine UV→world for the position. `PlaceOnMesh` must pass
`poly.UvCoords` through (it currently ignores them) and must handle the fan/strip split for UVs the
same way it does for vertices. Keep the existing barycentric surface-height behaviour only if A2
shows the affine UV→world map does not already give the correct Y; the original uses the affine map
(step 7), so prefer that and say why if you deviate.

Do **not** touch `BuildKindInstance`, `BuildSolidInstance`, `BuildSolidCollision`, or `KindExport`.

**Model recommendation.** high. The core rewrite, with a degenerate-geometry surface (zero-area UV
triangles, wrapped UVs, strips) that rewards care.

**Verify.** Per chapter, the instance count and a fixed-pose golden, against a `main` baseline taken
first (METHOD-3). Expect counts to **change** — state before running which direction you predict per
chapter from A1's histogram, then check the prediction (METHOD-12). Plus: no instance may land
outside its source triangle — assert it in the build and log the count, because a UV-space
containment test that is subtly wrong produces trees in the sea.

**⚠ Traps.** (a) A triangle whose UV span is huge (a stretched hillside) makes the integer lattice
loop enormous; the original has the same exposure, but bound it and **log** what you bounded rather
than truncating silently (LOG-5). (b) A zero-area UV triangle must be skipped, not divided by —
**and, per A2, that guard is not sufficient on its own: a zero-area WORLD triangle can carry a
nonzero UV area** (fan artifacts of n-gons with repeated or collinear corners; 1,655 of them on C1's
`terpat02` alone). Its affine map is finite but meaningless — both axes collapse onto a line, which
is what inflated A2's first metres-per-U maximum from 561 m to 32,768 m. **Test both areas, and
count what you skip.** (c)
The affine UV→world map is only valid within the triangle — do not reuse one triangle's map for a
neighbour. (d) `ExtraCullMargin`, `node_bias` and the shared collision shapes all read from
`kind.Instances` and keep working unchanged; if any of them breaks, you have touched the rendering
half and should back it out.

## B13 ☑ Settle the two remake-only rules: `MinSlopeCos` and the `seen` dedup

**Landed** (`CSVM/src/Mech3/Clutter.cs`). One of the two rules is deleted and one is kept with the
measurement that justifies it. **`MinSlopeCos` is gone.** The `seen` dedup **stays**, because B13
measured that it is doing two jobs and neither is optional today. And the item's headline: **the
subface gate moves `BL-305`'s pose — the first change in this plan that does — but it cannot land
on its own, and the coupling that makes it right is a user decision about `BL-250`.**

### The `BL-305` pose, after each step

B12 established `--freecam --chapter=C5 --pos=-9490,230,-3300 --direction=0,-1,0.001 --no-fog`
as the instrument; on B11 and B12 it renders md5 `8820B741E6CFB29A5E82CFED048711E0`.

| state | C5 sprites | C5 solids | `BL-305` pose md5 | moved? |
|---|---|---|---|---|
| baseline (`4a33ea4`) | 127,744 | 75,148 | `8820B741E6CFB29A5E82CFED048711E0` | — |
| + subface gate | 95,356 | 59,861 | **`A504DC7C3F950BAED11316213E5CB678`** | **YES** |
| + gate, − `seen` dedup | 95,366 | 60,933 | *(not shot — see below)* | — |
| + gate, `cblock4/5/6` restored | 110,692 | 68,908 | **`1050CEEE6CF51EE6F50ECADD1B8A0FB7`** | **YES** |
| **landed** (`MinSlopeCos` deleted only) | 127,744 | 75,148 | `8820B741E6CFB29A5E82CFED048711E0` | no |

The baseline was re-measured on a restored `HEAD` tree after all the experiments and came back at
the same md5 with the same 154.6 ms clutter phase (METHOD-3, and the proof the file round-trip and
the rebuilds were clean). Clutter build phase per state: 154.7 → 123.3 → 131.6 → 152.5 ms, so every
screenshot above is provably a different binary from its neighbour.

### Part 1 — the subface gate: the prediction failed, twice, and that is the finding

**Predicted first** (METHOD-12): the gate skips 288 C5 / 75 C4 / 70 C2 polygons (A3's
`remake l0` − `layer-0` deltas) and **no chapter's instance count moves**, because the `seen` set
was already hiding those; then deleting `seen` moves nothing either, because the gate now is.

**Measured:**

| chapter | baseline sprites | + gate | + gate, − dedup | `skipped_subface` (A3 predicted) |
|---|---|---|---|---|
| C1 | 37,510 | 37,510 | **37,548** | 0 (0) |
| C1B | 339 | 339 | 339 | 0 (0) |
| C2 | 37,254 / 10,346 solid | **36,406** / 10,346 | 36,406 / 10,346 | **67** (70) |
| C3 | 707 | 707 | 707 | 0 (0) |
| C4 | 88,705 | **87,239** | **87,378** | **75** (75) |
| C5 | 127,744 / 75,148 solid | **95,356 / 59,861** | **95,366 / 60,933** | **452** (288) |

Both halves of the prediction are wrong, in different ways, and both differences are real:

1. **The gate and the dedup are not covering the same set — not remotely.** The gate removes
   25 % of C5's clutter; the dedup was hiding a tiny fraction of that. The dedup is keyed to a
   quarter metre, so it only ever merged instances that *coincided*; the gate removes a whole
   polygon's stamp, including every instance on it that coincided with nothing. A3's "most produce
   no extra instance today" does not hold — most of them produce a distinct instance a quarter of
   a metre or more away from its coplanar twin.
2. **`skipped_subface` = 452 in C5, against A3's 288.** A3's delta was measured with a static port
   of the walk; the live counter counts template-textured subface polygons as the engine's own
   `PlaceOnWorld` reaches them. The two walks disagree by 164 polygons in C5 and by 3 in C2 (67 vs
   70) while agreeing exactly in C4 (75). **Not chased down** — it does not change any decision
   here, but it means one of the two walks visits a set of nodes the other does not, and B14
   should not treat A3's census as interchangeable with the live build's.
3. **Deleting `seen` after the gate still moves counts**: C1 +38, C4 +139, C5 +10 sprites and
   **+1,072 solids**. These are not subface duplicates — C1 has no subfaces at all. `UvTriangle`'s
   containment test is **inclusive** on the edge, so a lattice candidate landing exactly on the
   diagonal two triangles share is claimed by both. Solid buildings dominate because their authored
   quad UVs sit on tidy fractions and hit the diagonal exactly. **The original's step-6 test is
   strict** (three edge cross-products all negative), so it claims such a point in *neither*
   triangle. Matching that is a change to B12's containment rule, not to this set — flagged for
   B14, not done here.

### ⚠ Why the gate did not land: it empties C5's downtown, and `BL-250` is why

With the subface gate alone, the `BL-305` nadir loses almost every tower
(`.scratch/b13/bl305-step1.png`). The mechanism is in this file's own comment: in C5 the *visible*
ground layer **is** the subface layer — `cblock1/2/3`'s subface polygons cover `cblock4/5/6`'s base
polygons at 97.0/99.9/100.0 %. `ClutterBuilder.BuriedClutterDistricts` (`BL-250`, closed 2026-08-07
on CAP-22 evidence) removes `cblock4/5/6` because stamping both layers doubled the city. Add the
real gate and the base layer is all that is left eligible — and it has been exempted. The district
ends up with nothing.

**That exemption is a curated stand-in for exactly this missing gate**, and the code says so: "the
base polygon carries no flag of its own (Subface marks the OVERLAY), so a live rule would need
CBLOCK-LOD.md's coplanar-overlap computation at every load." The decoded gate *is* the live rule.

**So the two were measured coupled** — subface gate on, `cblock4/5/6` restored — and that state is
the closest this plan has come to CAP-22: an open crossroads with towers around it, no
interpenetration in a low oblique, and `cblock1/2/3`'s own towers still present (`cb00a` ×3,121,
`cb01a` ×2,350 …) on their non-subface polygons, with `cblock4/5/6`'s `cb15a`–`cb24a` added on the
bases. The gate makes the two sets disjoint **by construction**, which is the doubling `BL-250`
rejected, removed by mechanism rather than by a curated list.

**Not landed, deliberately.** This plan's scope says `BL-250` is not reopened, and its exemption was
a user decision taken on capture evidence. The change is three strings plus the gate; the images are
in `.scratch/b13/` (swept by `CleanScratch.ps1` — copy them if they are wanted):

| file | what it is |
|---|---|
| `bl305-base.png` | today's build at `BL-305`'s pose — packed edge to edge |
| `bl305-step1.png` | subface gate alone — downtown empty, a clear over-correction |
| `bl305-expt-bl250.png` | gate + `cblock4/5/6` restored — the candidate |
| `c5-oblique-base.png` / `c5-oblique-expt-bl250.png` | a 140 m oblique over the same crossroads, for the interpenetration check `BL-250` used |

Compare against `Z:\CSVM\playtest\CAP-22\orig-c-t4-nadir-crossroads.png`.

### ⚠ The user looked, and the candidate is disqualified as it stands (2026-08-10)

The orchestrator recommended adopting the coupled change. **The user rejected it on sight, and was
right:** in `bl305-expt-bl250.png` **buildings sit across the avenues** — visible at the crossroads
once cropped, and absent from both the original and the gate-only shot. That is a defect the
instrument used (whole-frame md5 moved / interpenetration check clean) could not express, and it is
the second time in this project that the user's eyes have caught what the numbers reported as
progress.

The user's question — whether the edge-inclusive containment bug causes it — is answered **no**:
that bug produces exact *duplicates* at one point (both triangles sharing a diagonal claim the same
candidate), and cannot displace anything onto a street.

The standing explanation, which is a **hypothesis and not yet measured**: `cblock4/5/6`'s
decorations are authored against their own 64² low-res art, whose painted street layout is
*correlated* with `cblock1/2/3`'s 256² art but not identical — `CBLOCK-LOD.md` §1a measured the
pairing at r ≈ 0.70, not 1.0. Placing buildings by the buried layer's UV therefore lands them in
approximately the right blocks and, wherever the two paintings disagree, in the street.

**What that implies is uncomfortable and must not be skipped:** if the original really did stamp
from the base layer, it would show the same defect, and it does not. So at least one of these is
wrong — (a) that `0x800` is the subface mark, (b) that C5's *visible* ground is the subface layer,
or (c) that the original's downtown buildings come from the `cblock4/5/6` templates at all. A3
cross-checked (a) against `docs/formats/gamez.md` but nothing has re-derived it, and it is now
load-bearing for a district decision.

**Neither candidate configuration is correct.** Coupled puts buildings in the streets. And
gate-only is worse than "almost no buildings" — see below.

### ⚠ Gate-only is disqualified too: it deletes C5's skyline entirely (2026-08-10)

The user asked the question the nadir pose cannot answer — *"are there even buildings in 3, or is it
only the ground texture?"* — because at nadir a painted rooftop and an extruded building are
indistinguishable. Re-shot as a **180 m low oblique** over the same crossroads
(`--pos=-9700,180,-3500 --direction=0.7,-0.22,0.68 --no-fog`), where a real building has visible
sides:

| build | oblique md5 | what it shows |
|---|---|---|
| today (`0f6330b`) | `4891EEAAB4432B697490618BC90C3CC5` | a real skyline — extruded towers, lit sides |
| + subface gate | `35984FFADB311E42D44E737CFCCB87BE` | **completely flat**: painted ground, zero 3D buildings |

The gate-only nadir reproduced B13's `A504DC7C3F950BAED11316213E5CB678` exactly, so this is the same
experiment, re-run from a temporary patch that was reverted and the tree proved clean.

**So the "clear streets" of the gate-only shot were clear because the entire downtown was gone.**
The original unambiguously has a downtown skyline — CAP-22's own clips include a rooftop-height
chase pass — so a gate that deletes it cannot be what the original does.

**That falsifies the reading this branch of the investigation was built on.** At least one of these
is wrong, and the next step is to find out which rather than to try a third configuration:

1. **`0x800` is the polygon gate `FUN_004de2c0` skips on.** Re-read the decompilation. The bit may
   be tested with the opposite sense, or the gate may sit somewhere other than the per-polygon loop.
2. **`0x800` means SUBFACE in the extraction.** `GameZ.cs:427-431` maps it to mech3ax's `unk3` and
   `docs/formats/gamez.md` documents it as the OpenFlight SUBFACE bit. A3 cross-checked this but
   nothing re-derived it, and it is now load-bearing.
3. **C5's visible ground is the subface layer.** `CBLOCK-LOD.md` says `cblock1/2/3`'s subfaces cover
   `cblock4/5/6`'s bases at 97–100 %. If that pairing is the right way round, the original — which
   skips subfaces — would be stamping its downtown from the *buried* layer, which is exactly the
   configuration that puts buildings in the streets.

Options 1 and 2 are cheap: one is a re-read of `FUN_004de2c0` and `FUN_004de460` in Ghidra, the
other a check of the flag's meaning in the extraction against a polygon whose layer is known
independently. Do those before touching `BuriedClutterDistricts` again.

**Instrument note for whoever picks this up: a nadir shot cannot distinguish painted rooftops from
buildings. Every C5 clutter claim needs an oblique beside it.** This one cost a recommendation that
was wrong and would have been landed on a whole-frame md5 and an interpenetration check, both of
which the gate-only and coupled configurations passed. Landed as `SHOT-28` in
[`docs/verification.md`](verification.md).

### The Ghidra re-read (2026-08-10): no second entry point, and `0x800` is probably not subface

Prompted by the question "is there another entry point into this?". Three results, in descending
confidence.

**1. There is exactly one entry point, and clutter is built once. Certain.** Every caller edge was
enumerated:

```
FUN_00463f40 (startup, after templates.zrd)
  └─ FUN_004df1d0        srand(0x8EA91836) … srand(time)
       └─ FUN_004de4d0   the terrain partition grid + world children
            └─ FUN_004de460            (node gate; type 5 / 6 dispatch)
                 ├─ FUN_004de370  ──┐  each passes node+0x3c, the node's own mesh
                 └─ FUN_004de310  ──┤  and recurses through FUN_004de460
                        └─ FUN_004de2c0   per polygon: skip 0x800, require UVs
                             └─ FUN_004de190   per texture layer, template by name
                                  └─ FUN_004dd6e0   the stamper
```

`FUN_004de4d0` has one caller, `FUN_004df1d0` has one caller, `FUN_004dd6e0` has one caller. **There
is no second walk, no per-mission rebuild and no runtime re-stamp.** Anything the original's clutter
does, it does through this chain.

**2. `GameGenSetSubfacePriorityOffset` is not a polygon test — it is a load-time draw-priority
accumulator. Certain.** Its script handler stores the operand in `DAT_0062b86c`
(`FUN_004c1210`), and the only readers are two arms of a **jump-table dispatch** inside
`FUN_004c2610` — `D:\zipper\gamez\zgamegen\gg_load.c` — which do
`DAT_0071e828 += offset` / `-= offset` around a record. So "subface" in the engine is a
**record/group** concept that bumps draw order while loading. It says nothing about polygon bit
`0x800`, and it is *not* evidence that subface polygons are skipped by anything.

**3. The original ships an explicit per-face `no_clutter` attribute. Certain that it exists;
hypothesis that it is `0x800`.** `FUN_004c5580` (`gg_load.c`) takes a string, `strstr`s it for
`no_clutter`, tokenises, and sets `DAT_0071e814 = 1`. That global is then pushed as an argument
into the polygon-construction calls in `FUN_004c5ba0` (`004c5e6f`) and `FUN_004c6250` (`004c64d4`),
both of which land in `D:\zipper\gamez\zmodel\gmod_cons.c`'s polygon builder
(`FUN_00565510` → `FUN_005652b0`). A bare `noclutter` string at `0x0062c068` is referenced once more
from `FUN_004c2610`.

**So the artists could mark a face "put no clutter here" — which is a far better fit for the bit
`FUN_004de2c0` tests than "subface" is.** It explains what the subface reading could not: the
original's streets are clear because the street faces are flagged, while the buildings still stamp
from the same polygons we stamp from. It also explains why skipping our `Subface` set deleted the
skyline — `subface ≠ no_clutter`, and mech3ax's `unk3` name for bit `0x800` is a guess nobody has
tied to engine behaviour.

**Not closed:** the exact bit. The `no_clutter` flag travels as a *parameter* into `gmod_cons.c`,
and neither `FUN_00565060` nor `FUN_005652b0` contains a literal `0x800`, so the packing happens
deeper in that chain. **This is the one thing left to nail**, and it decides everything:

- If `0x800` is `no_clutter`, then `BuriedClutterDistricts` stays, `PlaceOnMesh` gains a
  `no_clutter` skip instead of a subface skip, and `BL-305` plausibly closes without touching
  `BL-250` at all.
- If `0x800` really is subface, the contradiction in §"Gate-only is disqualified" stands and
  something else is wrong.

### ✅ CLOSED (2026-08-10): polygon bit `0x800` is `no_clutter`, not subface

Chased down `gmod_cons.c`. **Measured, single-writer, with the argument mapping cross-checked.**

1. `FUN_004c5580` (`gg_load.c`) does `strstr(name, "no_clutter")` → `DAT_0071e814 = 1`.
2. `FUN_004c5ba0` pushes `[0x0071e814]` at `004c5e7b`. Counting pushes backwards from the
   `CALL 0x00565510` at `004c5ead`, that is **argument 13**.
3. `FUN_00565510` forwards arguments 3–15 unchanged to `FUN_005652b0`.
4. `FUN_005652b0` ends with
   `*puVar1 = (in_stack_00000034 & 1) << 0xb | *puVar1 & 0xfffff7ff;`
   — `in_stack_00000034` is argument 13 (`0x34/4 = 13`), `<< 0xb` is bit 11 = **`0x800`**, and
   `puVar1` is the freshly built polygon at `model+0x34 + count*0x28`: the same word
   `FUN_004de2c0` tests, whose low 10 bits are the vertex count.
5. **`AND EAX, 0xfffff7ff` at `005654f6` is the only instruction in the entire binary that writes
   this bit.** Program-wide search; the other three hits are unrelated structures.

Mapping cross-check: `in_stack_00000024` = argument 9 = the literal `0x1` pushed at `004c5e88`, and
the body assigns it to `puVar1[4]`, the polygon's material/texture-layer count. A one-material
polygon, exactly as `FUN_004de190` reads it. The offset→argument mapping holds.

**Consequences.**

- **`docs/formats/gamez.md` is wrong and `GameZ.cs`'s `poly.Subface` is misnamed.** mech3ax's `unk3`
  at bit `0x800` is a **no-clutter** flag. The extraction *does* carry the attribute — the remake has
  simply been reading it under the wrong name and using it for the wrong purpose.
- **Where subface actually lives is now a hypothesis worth testing:**
  `GameGenSetSubfacePriorityOffset` is a load-time draw-priority accumulator, so subface is probably
  baked into the polygon's **priority** field (mech3ax `unk04`/`priority`) and is not a flag bit at
  all. If so, `unk3` and `priority` are two different things this project has been conflating.
- **`SceneBuilder.SubfaceBias` is keyed off `unk3`**, i.e. off no-clutter. Whether it is wrong *in
  effect* is a separate question — the two sets may overlap heavily in practice — but its
  justification is void and it needs re-deriving.
- **`analysis/item9-depth-bias/CBLOCK-LOD.md` measured C5's "subface covers base at 97–100 %" using
  this flag.** The coplanar coverage it measured is real geometry and stands; the *label* on it does
  not. `BL-250` rests on that reading.

**What this does and does not resolve for `BL-305`.** It explains the naming, and it means our
"subface skip" experiment was really a *no-clutter* skip — which is what the original does. But that
experiment emptied C5's downtown viewpoint, so the contradiction has moved rather than gone.

### The census confirms the decode from the data side, and corrects me (2026-08-10)

Full write-up: [`analysis/bl-305-clutter-uv/FINDINGS-noclutter.md`](../analysis/bl-305-clutter-uv/FINDINGS-noclutter.md).
Headlines, all measured:

- **`no_clutter` reaches the shipped data as a bit, never as text.** A byte-level scan of the whole
  install finds the string only in `crimson.exe` — and the same scanner finds `clutter` inside five
  `.zbd` files, so the negative is real.
- **mech3ax discards no bit.** The `pm` reader's flag word is exactly
  `VERTEX_COUNT 0x3FF | SHOW_BACKFACE 0x400 | UNK3 0x800 | NORMALS 0x1000 | TRI_STRIP 0x2000 |
  IN_OUT 0x4000`, and its `bitflags` check *errors* on any bit outside that set — so extraction
  succeeding across all eight chapters proves no CS polygon sets anything higher. **The remake
  already parses the attribute**, as `GameZPolygon.Subface`. Nothing needs re-extracting; the field
  needs renaming and a consumer.
- **`unk3 == SUBFACE` was never measured, and mech3ax never claimed it** — the word "subface" does
  not appear anywhere in the mech3ax tree. The name is this repo's 2026-07-23 inference from a
  z-fight fix that worked.
- **The flagged population is "nothing grows here", not "overlay".** C5's 658: water 21.6 %,
  `cblock1/2/3` pavement 49.3 %, untextured 15.5 %, `cblock7` 11.9 %, pier decking 1.5 % — 86 %
  horizontal, sitting on the ground plane.
- **The `fvol` result kills SUBFACE outright.** Every invisible fog-volume box carries the flag on
  its floor and all four walls and **not** on its lid, every time. A box floating in the sky has no
  face beneath it and its own faces are not coplanar with each other, so "subface" is nonsense
  there; "don't scatter on these faces, only on the lid" is exactly what `FogVolumeClutter` does.
- `CBLOCK-LOD.md`'s coplanar geometry stands; its *label* does not. Its own falsification table
  already recorded 251 of the flagged C5 polygons with **no coplanar partner at all** and explained
  them away as source-file leftovers. That bimodality was always evidence against SUBFACE.

**⚠ And it caught an overstatement of mine.** The census argued the gate *cannot* empty C5, since the
flag covers only 18.7 / 53.3 / 63.0 / 5.1 % of `cblock1/2/3/7` area. I re-ran it capturing per-kind
counts, and **both are true**:

| kind | base | gate | | kind | base | gate |
|---|---|---|---|---|---|---|
| `cb00a` | 3,933 | 3,121 | | `cb12a` | 13,595 | 13,009 |
| `cb02a` | 2,976 | 2,351 | | `cb14a` | 14,932 | 14,289 |

**Nothing goes to zero — 79–96 % of every downtown kind survives — and the near-field skyline is
still completely gone**, confirmed on a 3.2×-brightened foreground crop of the same md5
(`35984FFA…`, reproduced by two independently written patches). The flagged quads are *concentrated
at this viewpoint*; the surviving instances are elsewhere on the map. So a placement change
**relocates** a population as well as thinning it, and a global count is blind to that. `SHOT-28` is
rewritten to say so — three instruments, two reassuring, one right.

**Where that leaves `BL-305`: a sharper open question, not an answer.** The gate is what the original
does, and applying it empties the very viewpoint the original fills with towers. So either the
unflagged `cblock1/2/3` quads that should carry this downtown are somewhere our walk is not
reaching, or the buildings here come from a template/layer this plan has not considered. **Next
step is spatial, not statistical:** plot the flagged and unflagged `cblock1/2/3/7` quads around
`-9700, -3500` and find out what the original would have left to stamp on. Do not run another
whole-frame A/B until that map exists.

### ⚠ At the controls, 2026-08-10: the original places clutter on FLAGGED ground, and it is smaller

The user flew C5 with `--debug-clutterflag` (landed `b9e6025`) against their own recordings and
reported: **north of the bridge our ground is entirely red — flagged `no_clutter` — yet the
original has clutter there, and its buildings are SMALLER than the ones our build draws.**

Two things follow, and the second is the promising one.

1. **A flagged polygon is not simply "no clutter here" in the original.** Either the flag gates
   something narrower than we assume, or the original's buildings at that spot are stamped from a
   surface we are not looking at. Do not weaken the `0x800 = no_clutter` decode over this — that is
   code-level and single-writer — but the *consequence* we drew from it is not safe.
2. **"Smaller" points straight at `cblock4/5/6`.** `playtest/CAP-22/README.md`'s own counterfactual
   recorded that suppressing `cblock1/2/3` collapses the city to "a uniform low-rise field", which
   is exactly what a low-rise district looks like from above. `ClutterBuilder.BuriedClutterDistricts`
   suppresses `cblock4/5/6` everywhere, on the strength of a whole-map coverage argument. **If the
   original draws the low-rise district north of the bridge, that exemption is wrong there** — and
   `BL-250` was closed on evidence (`CAP-22`) that never examined this location.

This is a controls report against the user's own footage, so by this project's standing rule it
outranks the instrument readings above until an instrument disproves it.

**Instrument being built for exactly this:** `--clutter-templates=<names>`, which replaces the
chapter's template set and **bypasses `BuriedClutterDistricts`**, so `cblock4/5/6` can be loaded
alone and compared against the recordings district by district. That is the cheapest way to test
(2), and it tests it where the user saw the problem rather than at `BL-305`'s unlocatable pose.

### ✅ BL-305's mechanism, found by the user at the controls and confirmed 1,037× over

Flying with both new flags, the user reported the rule:

> **on the red areas cblock4, 5 and 6 are active, and on green cblock1, 2, 3 and 7**

`analysis/bl-305-clutter-uv/FINDINGS-layer-pairing.md` tested it and it holds, in all four overlay
textures, with no counter-example in either direction. **`no_clutter` is not "no clutter here". It
is "not from this layer" — a per-district switch selecting which of two coplanar districts
decorates that ground.**

| | `cblock4/5/6` base beneath | no base beneath |
|---|---|---|
| overlay **FLAGGED** | **25,524,629 m² (64.9 %)** | 13,831,347 m² (35.1 %) |
| overlay **CLEAR** | **7,517 m² (0.02 %)** | 141,652,679 m² (99.98 %) |

Odds ratio **1,036.8×**. Exactly **one** clear overlay polygon in the whole chapter has a base
under it. And two column facts that settle the shape of the thing:

- **Every square metre of the `cblock4/5/6` base lies under a `cblock1/2/3/7` overlay** — exposed
  base area is **0 m² of 25.5 M**. The base is never the visible ground anywhere.
- **99.97 % of that base sits under a FLAGGED overlay.** The base layer and the flag are, to three
  significant figures, the same region of the map.

The 1↔4, 2↔5, 3↔6 pairing `CBLOCK-LOD.md` measured is reproduced from the other direction. The two
layers are **coincident, not stacked** — dY is `+0.000` for all 183 paired polygons, both at
Y = 5.0, with draw order decided by `SubfaceBias`; "beneath" is a statement about render order, not
geometry. `cblock7` is the weakest case and still does not break the rule: its *clear* half is
perfect (0 of 1,198 polygons, 0 of 75.8 M m² have a base), while only 26 % of its flagged area has
one — so on `cblock7`'s flagged ground the original stamps *nothing*, which is why it sits in the
user's green group.

**So `BL-305` is explained.** Our build ignores the flag **and** exempts `cblock4/5/6`, so on
flagged ground we stamp the tall district (`cblock1/2/3`, models to 108 m, median 62.6 m) where the
original stamps the short one (`cblock4/5/6`, to 52 m, median 20–30 m). Too big and too dense,
swallowing the pavement — the reported symptom, exactly. **`BL-305`'s own pose cell is 100 % `L`**:
under the original's rule it stamps the low-rise district and nothing else.

**The bridge is located**, which the footage never was: C5 carries a node named `brooklynbridge`
(index 2299) spanning X[−10148, −10076] Z[−3700, −1423], rising to 122.6 m. North is −Z. The rule
predicts **low-rise out to ~1.5 km north of it, towers again beyond** — the user's report, at a
landmark that can be flown to and matched.

**Consequences for `BL-250`.** Its conclusion — the original draws the `cblock1/2/3` city — holds
*where CAP-22 looked*, and 78.3 % of C5's ground by area is indeed tower country. But
`BuriedClutterDistricts` suppresses `cblock4/5/6` **map-wide**, and on 14.1 % of the ground that is
wrong. The exemption must become conditional on the flag rather than absolute.

**✅ Confirmed at the controls (2026-08-10).** The prediction above was tested the way it asked to
be: `--clutter-templates=cblock4,cblock5,cblock6` at `-10112,400,-3562`, ~1 km north of
`brooklynbridge`. The user's verdict was *"yes this matches my recording"*. That closes the last gap
between the static-geometry measurement and the original's own frames, and is what authorised B15.

**Still open.** **35 % of flagged overlay area (13.8 M m², 7.6 % of C5) has no base at all**, where
this rule says the original stamps nothing; whether that is right or whether a third mechanism fills
it is untested. B15 ships that behaviour — flagged with no base is now bare ground — so if a later
look finds the original decorating it, that is the next thread, not a regression of the gate.

### Part 2 — `MinSlopeCos` is deleted, and it never culled anything

`MinSlopeCos = 0.25f` is gone. It is an invention — `FUN_004deab0` initialises the kind block's
slope bounds to ±1.0 (no cull) and no shipped `templates.zrd` authors `min_slope` or `max_slope` —
**and it was inert**. The cull is `xzArea < trueArea * MinSlopeCos`, and `xzArea / trueArea` is
exactly `|Ny|` of the unit plane normal, so it drops triangles steeper than ~75.5°. Censused over
every chapter's registered template textures, walked the way `PlaceOnWorld` walks:

| chapter | eligible tris | shallowest slope cos | culled by 0.25 | culled at 0.50 (control) |
|---|---|---|---|---|
| C1 | 3,828 | **0.4598** (~62.6°) | **0** | 3 |
| C1B | 20 | 0.8747 | 0 | 0 |
| C2 | 3,091 | 0.6737 | 0 | 0 |
| C3 | 319 | 0.7997 | 0 | 0 |
| C4 | 4,646 | 0.5446 | 0 | 0 |
| C5 | 3,971 | 0.9988 | 0 | 0 |

**There is no terrain in this install steep enough to trip it.** The steepest clutter-eligible
triangle anywhere is a single C1 face at 0.4598, nearly twice the threshold. So the deletion is a
provable no-op on retail data: every chapter's instance count is byte-identical to the baseline, all
13 goldens are hash-identical, and a shot of that steepest hillside
(`--pos=-10176,300,-3060 --lookat=-10176,140,-3445`) renders the same md5
`14466614F73D388FAA1808FFA7418763` before and after. **No new trees appear, so none can float** —
the concern the constant was invented for is real but has nothing in this install to act on. Nothing
was replaced with another constant; a cull belongs in `templates.zrd`'s `min_slope`, which no
chapter authors.

The instrument was shown able to fail twice (METHOD-9): its first run reported "0 triangles tested"
because it read `resolve_templates`'s keys instead of its textures — caught and fixed rather than
banked — and the same script at a 0.50 threshold culls 3 triangles in C1.

**One neighbour left alone.** `xzArea < 0.5f`, the sliver rule beside it, is also remake-only and
**does** fire — 14 triangles in C5, 0 elsewhere. B13 does not own it; it is noted here so nobody
reads "the slope rules were settled" as covering it.

**Verified.** Baseline taken on the unchanged tree immediately before the changed runs and
re-measured after (METHOD-3), both times `8820B741…` at 154.6/154.7 ms. `.\RunTests.ps1`: build
PASS, **843 units PASS**, 29 in-engine suites PASS with engine errors clean, **13 goldens
hash-identical — none moved.** METHOD-12 honoured and its prediction is recorded above as failed,
not rewritten. The `.cs`/`.dll` timestamps were checked after the file round-trip, because a
`Copy-Item` restore preserves `LastWriteTime` and MSBuild will silently skip the rebuild.

**What B13 could not verify.** (a) Whether the coupled gate + `BL-250` state matches the original —
that needs the user's eye on the three images and, if adopted, B14's proper A/B. (b) The 452-vs-288
walk discrepancy above. (c) Whether making `UvTriangle.Contains` strict, per the original's step 6,
is right — it would delete the shared-diagonal candidates rather than duplicate them, and no capture
distinguishes the two.

### Original approach (kept for reference)

**Goal.** Each of the two invented rules in `Clutter.cs` is either justified against the original's
behaviour and documented as a deliberate deviation, or deleted.

**Evidence (confidence: traced for the slope rule; direction-sound for the dedup).**
*Slope:* `MinSlopeCos = 0.25f` (`Clutter.cs:78`, applied at `:333`) has no counterpart in the
original's defaults. `FUN_004deab0` initialises the kind block's slope bounds to −1.0/+1.0 and only
`min_slope`/`max_slope` narrow them; no shipped `templates.zrd` authors either. So the original grows
trees on cliffs and we do not — a density loss concentrated exactly where A1 predicts the UV repeat
is shortest.
*Dedup:* the `seen` set (`:357-359`, keyed to quarter-metre) exists to stop decal-layered coplanar
polygons double-stamping. The original has no such set: `FUN_004de190` iterates texture layers and
stamps each, so it **does** double-stamp in that case. Whether that is visible, and whether A3 found
any such pairs outside the already-exempted districts, decides this.

**Approach.** Delete `MinSlopeCos` and its use unless A1/A3 show it is load-bearing for something
other than hiding the grid's artifacts.

**⚠ Amended after A3 — the dedup rule as written is inverted and must not be followed.** This item
originally said: zero coplanar pairs → delete the `seen` set. A3 measured zero, and the zero is
*structural* — polygon flag `0x800`, the bit `FUN_004de2c0` gates on, **is the already-decoded
subface mark**, so the original excludes every subface polygon before texture matching and cannot
double-stamp. But `ClutterBuilder.PlaceOnMesh` never reads that flag, so **the remake does reach both
members of a coplanar pair, and the `seen` set is the only thing suppressing the double-stamp
today** — 449 subface polygons' worth in C5 (787,546 raw pairs with the gate ignored), 18 in C2, 33
in C4. Deleting the set on the strength of the original's zero would visibly double-stamp C5's city
blocks, a regression neither the original nor the current build shows.

**The correct order: add the subface (`unk3`) skip to `PlaceOnMesh` first** — reproducing
`FUN_004de2c0`'s actual gate, which is what the original's code justifies — **then delete `seen` as
redundant.** Verify the two steps separately: after the skip alone, C5's instance count must be
unchanged (the dedup was already hiding these); after the deletion, still unchanged (now the gate is).
If either moves, the gate and the dedup are not covering the same set and the difference is the
finding.

**Model recommendation.** medium. The analysis is done by then; this is applying it, with a
judgement call on the dedup that A3's number should largely make for you.

**Verify.** Instance counts and a hillside golden in C1 before/after removing the slope cull —
the count must rise and the new trees must be *on* the terrain, not floating off a cliff face
(which is the concern the constant was invented for, and the thing to actually look at). If they
float, the constant was hiding a real problem and this item ends by saying so rather than by landing
the deletion.

**⚠ Traps.** (a) A billboard on a near-vertical face genuinely does float — that is why the constant
exists. Removing it is only correct if the original also places them there; check a steep C1 hillside
against `OriginalScreenshots/` before committing to the deletion. (b) Do not replace one invented
constant with another; if a cull is needed, it belongs in `templates.zrd`'s `min_slope`, and no
chapter authors it — which would make the honest answer "we deviate, here is the count". (c) The
dedup's quarter-metre quantisation is itself arbitrary; do not "tune" it, delete it or keep it.

## B14 ☑ Chapter A/Bs + the 8-chapter regression, and rewrite the class comment

### ✅ Landed 2026-08-10

**The 8-chapter regression, against the plan's own merge-base `99f3b9b`** (not `main`'s tip — main
has moved under concurrent sessions, and the merge-base is the only stable "before this plan"). Zero
engine errors in all eight. Predictions were stated before measuring (METHOD-12): C1 up ~4× from
A1's UV-repeat finding, C2/C4 down slightly from the gate alone, C5 down with a substitution,
C1C/C2B unchanged at zero.

| chapter | `99f3b9b` | now | change | gate skipped |
|---|---|---|---|---|
| C1 | 9,303 spr | 37,510 spr | **×4.03** | 0 |
| C1B | 60 spr | 339 spr | ×5.65 | 0 |
| C1C | no templates | no templates | — | — |
| C2 | 37,167 spr + 10,261 3D | 36,406 spr + 10,346 3D | −761 spr, **+85 3D** | 67 |
| C2B | none | none | — | — |
| C3 | 371 spr | 707 spr | ×1.91 | 0 |
| C4 | 88,630 spr | 87,239 spr | −1,391 | 75 |
| C5 | 124,072 spr + 71,326 3D | 110,668 spr + 67,836 3D | −13,404 spr, −3,490 3D | 453 |

**DIAG-11 — what moved, and why, before anything is called clean.** C1's ×4.03 is A1's prediction
(3.9–4.0×) landing on the nose. C1B ×5.65 and C3 ×1.91 are the same mechanism at different painted
scales, and both are *uniform over an unchanged kind set* — C1B is `dougfirtree1` 50→273, `bush1`
2→21, `bush2` 8→45, i.e. one density change, not a redistribution. C2's **+85 solid decorations
against −761 sprites** is the one mixed sign: the increase is B11/B12's (the B15-only A/B had C2's
3D count flat at 10,346 on both sides), so the lattice found building placements the grid missed
while the gate removed sprites. C4 and C5 are the gate and the layer swap. **Nothing moved that has
no account**, and the two chapters registering no templates moved not at all.

**The C5 nadir A/B is still unscoreable, and that is the honest result.** Re-shot at CAP-22's
`-9490,230,-3300`. Against `ours-nadir-230-scale-matched.png` — same pose, same build lineage — the
change is unambiguous: towers crowding the frame and burying the streets become low blocks with the
crossroads and the diagonal avenue plainly legible, which is `BL-305`'s reported symptom resolved.
Against `orig-c-t4-nadir-crossroads.png` it cannot be scored at all, because that frame is somewhere
else — established earlier in this plan and by the user directly ("could it be that the screenshot
from the video and your position aren't the same?"). Do not read our low-rise result at this pose as
"too short" versus the original's towers; the original frame is tower country and our pose cell is
100 % low-rise under the decoded rule. **The confirmation that counts came from a located landmark**
— the user's own flyover 1 km north of `brooklynbridge` — which is strictly better evidence than
this pose could ever produce.

**C1's forest was recorded here as UNCONFIRMED and is now CONFIRMED (2026-08-10), at the controls.**
The user, against their own recordings: **"C1 positions are exactly the same as the original."** That
is a positional match, not a density one — it confirms `FUN_004dd6e0`'s steps 4/6/7 as B12
implements them, which no instrument in this plan could have established. C2's suburbs and C4 remain
unconfirmed for want of footage.

**And the data predicts exactly that match, independently.** Censusing every shipped
`templates.zrd` for the keys that would move a decoration off its lattice point:

| chapter | `translate_uv_range` | `rotation_range` | `align_normal` | `substitute` | `scale_range` | `far_fade_range` |
|---|---|---|---|---|---|---|
| C1 | 0 | 0 | 0 | 3 | 5 | 5 |
| C1B | 0 | 0 | 0 | 0 | 3 | 3 |
| C1C | 0 | 0 | 0 | 0 | 0 | 0 |
| C2 | 0 | 0 | 0 | 2 | 50 | 50 |
| C2B | 0 | 0 | 0 | 0 | 0 | 0 |
| C3 | 0 | 0 | 0 | 1 | 3 | 3 |
| C4 | 0 | 0 | 0 | 1 | 4 | 4 |
| C5 | 0 | 0 | 0 | 34 | 78 | 78 |

**Nothing in the install authors `translate_uv_range`, `rotation_range` or `align_normal`.** Every
one of them defaults, so step 5's jitter and step 10's rotate/align halves are not "unimplemented
here" — they are **inert on retail data**, and cannot move a decoration in any chapter. The
original's placement therefore has *no random input that affects position or orientation at all*,
which is the structural reason an unseeded remake reproduces its positions exactly rather than
approximately. This is an able-to-fail check that agreed with the user's report: had C1 authored a
jitter, an exact match would have been impossible and one of the two readings would have had to be
wrong.

**What this predicts the user WILL still see wrong**, and it is worth saying before they find it:
`scale_range` is authored on 143 blocks (C21's count; 148 as first written here) and `substitute` on
41, and neither is applied. So every tree
is exactly its authored size where the original varies it 0.9–1.5×, and C1's `firtree1` stands
should be one-in-ten `firtree2` but are pure `firtree1`. Right positions, wrong sizes, wrong species
mix.

**Docs rewritten.** `Clutter.cs`'s class comment gains an explicit *what this does not do* section
(the five unimplemented `FUN_004dd6e0` steps and the unseeded build, with the warning that the first
of them to land must bring `srand(0x8EA91836)` with it); `docs/architecture.md`'s entry gets the
`no_clutter` gate and the deviation list, and loses the `BuriedClutterDistricts` description;
`docs/formats/gamez.md`'s `unk3` bullet is corrected from SUBFACE to `no_clutter` **without**
re-asserting a depth-order story it can no longer justify (rows 10 and 11 of the disproven table).

**Deliberately NOT done here.** Renaming `GameZPolygon.Subface` → `NoClutter`, because
`SceneBuilder.SubfaceBias` reads the same field for draw order and its justification has to be
re-derived rather than assumed; and the `UvTriangle.Contains` edge-strictness fix, which is a
behaviour change to B12's containment rule and would move goldens again. Both are handed to the
backlog rather than smuggled into a close-out item.

### Original approach (kept for reference)

**Goal.** The change is confirmed at the controls, the regression is clean, and `Clutter.cs`'s class
comment describes what the code now does — with every claim in the disproven table above removed.

**Evidence (confidence: the C5 case is traced to a capture; the others are lead-only).**
`BL-305`'s founding A/B is `playtest/CAP-22/ours-nadir-230-scale-matched.png` vs
`orig-c-t4-nadir-crossroads.png`. C1's density claim is the user's report at the controls and has no
capture behind it yet. C2's suburbs and C4's `terpat01`/`river5` have neither.

**Approach.** Full 8-chapter `--freecam --chapter=<X>` regression: zero errors, and a per-chapter
instance-count table against the `main` baseline with the predicted direction beside each measured
one. Then the C5 nadir A/B, and a C1 forest pose. Then rewrite the class comment (`Clutter.cs:10-61`)
and `Clutter.cs`'s entry in `docs/architecture.md:42`; add the decode to `docs/formats/` (it may
belong with the new `templates.md` from C21 — one page for the clutter system is better than two).
Close `BL-305` with `/close-backlog-item` if the C5 A/B confirms it; if the packing improved but did
not match, retitle the entry rather than closing it.

**Model recommendation.** high. Judgement-heavy: deciding whether an A/B *matched* is the whole
item, and this is where a "close enough" call would quietly bank a wrong result.

**Verify.** This item is the verification. Note that the C5 scale match is by eye, ±20 % —
re-shoot with a decoded altitude before treating any residual difference as signal (METHOD-20).

**⚠ Traps.** (a) `BL-305`'s trap (b): the founding scale match is ±20 %; do not tune to it. (b)
`BL-305`'s trap (c): `cblock7` places `cb12a`/`13a`/`14a`, names in the exempted district's range —
count per template root, never by name range. (c) A C1 A/B against the original needs footage that
does not exist yet; if the user cannot supply it, say the C1 result is unconfirmed rather than
inferring it from the C5 one. (d) DIAG-11 — identify what moved before calling any chapter's change a
regression; several chapters *should* change.

# Wave C — The authored per-kind data

## C21 ☑ `templates.zrd` reader + `docs/formats/templates.md`

### ✅ Landed 2026-08-10

`Mech3/ClutterTemplates.cs` (`ClutterTemplateSpec` / `ClutterKindProps` /
`ClutterSubstitute` / `ClutterDamageResponse`) reads all eleven keys `FUN_004deab0` accepts,
including the eight no chapter authors; `docs/formats/templates.md` is the format page;
`CSVM.Tests/ClutterTemplatesTests.cs` pins all eight chapters. **Nothing consumes it** — trap (c)
held.

**Two corrections to this plan's own text, both from re-reading the parser rather than from new
data:**

1. **`far_fade_range`'s nested pairs group by BOUND, not by band.** `FUN_004dd6e0` lerps the near
   distance between `kind+0x2c` and `+0x30` — the two file pairs' *first* components — and the far
   distance between `+0x34` and `+0x38`, their seconds. So the file is
   `[[nearMin, farMin], [nearMax, farMax]]`, and C1's `firtree2` (`[[300,600],[1000,2000]]`) fades
   starting somewhere in 300–1000 m, not 300–600. The two readings agree on every chained quad
   (`[[200,300],[300,350]]`, which is most of C5) and differ only where the numbers are not
   chained — which is what would have let a wrong grouping survive C22. Same grouping for
   `translate_uv_range` and `rotation_range`. **And both distances come from ONE `rand()` draw**:
   near and far are correlated per instance.
2. **The install ships 143 blocks, not 148.** B14's own per-chapter table sums to 143
   (5+3+0+50+0+3+4+78); "148 kinds" in this plan's prose and in `architecture.md` was arithmetic,
   not a measurement, and is corrected. `substitute`'s 41 was right.

**Five keys unauthored is now eight.** `min_slope`, `max_slope`, `align_normal`, `rotation_range`,
`translate_uv_range` — plus **`OnWeaponHit`, `OnCrater` and `OnCollide`**, the health/anim/model
damage blocks, which no chapter authors either. Every shipped block is `node` + `scale_range` +
`far_fade_range`, with or without `substitute`: 102 blocks of the first shape and 41 of the second,
install-wide, and **no other combination exists**.

**Both anomalies resolved, and neither is one.**

- **C3 registering `cliff1_sandtrans` while its file describes palms** — the file is keyed by
  DECORATION MODEL, not by template. C3's cliff template root → ground quad `g4` → six
  `palmtree1.flt` decorations (`extracted/C3/gamez/nodes.json`). The palms are what its file is
  about; `palmtree2/3` are there because `palmtree1` rolls three ways evenly.
- **C2B shipping an empty file against six registered templates** — its gamez carries **none of the
  six roots** (`terpat01`, `terpat03`, `terpat04`, `resblock2`, `filmblock1`, `filmblock2`), so the
  original logs `ClutterLoadTemplates(): cannot find node for template %s` six times and places
  nothing. An empty properties file is the consistent outcome. Both empty files are literally the
  four bytes `null`, which `Zrdr.LoadFile` rejected as "not a reader list" — hence
  `Zrdr.LoadFileOrEmpty`, so "authors nothing" stays distinguishable from "file is broken".

**A structural fact the survey turned up, and C22 needs it.** Cross-checking every block against
the decorations its chapter's registered templates actually carry: **every placed decoration has a
block** (no chapter has a decoration the file misses), and every *surplus* block is a substitution
target — C3's `palmtree2/3`, C4's `firtree2`, and 43 of C5's 78. That is the file telling you the
engine resolves a substituted stamp's properties from the TARGET, which is C22's trap (b) confirmed
from the data side. Two C5 targets (`cb05det02.flt`, `cb06det03.flt`) have no block at all: that
means defaults, never "skip".

**C5 ships one duplicate**, `cb05det01.flt`. The first block carries a 50/50 substitute, the second
carries none. `FUN_004dd230`'s lookup is a linear scan of the load order stopping at the first
`strcmp` match, and `FUN_004de7d0` appends — so the FIRST block wins and the substitute survives.
`Find` reproduces that; `DuplicateNodes` surfaces it rather than collapsing it silently.

**Verify — WORLD-23, over the whole install and asserted in the tests.** `scale_range` spans
0.5–3.0 with every pair low→high (multipliers, not radians); `far_fade_range` spans 50–2000 m over
27 distinct quads, and all 143 satisfy near ≤ far on both bounds — which is the independent
corroboration of correction 1's grouping. Per-chapter census against the byte counts in the table
above: C1 5 blocks/1,657 B, C1B 3/795, C1C 0/4, C2 50/12,250, C2B 0/4, C3 3/992, C4 4/1,148,
C5 78 blocks over 77 distinct models/22,956 B. The eight-chapter rows carry
`ExtractedDataTheory`, so an unset `CSVM_DATA_ROOT` skips them rather than passing on no evidence.

`.\RunTests.ps1` with `CSVM_DATA_ROOT=Z:\CSVM`, 132 s, exit 0: build clean (0 warnings), **867 units
passed / 0 skipped**, **29 engine suites passed, engine errors clean**, **13 goldens
hash-identical**. The goldens holding is the prediction, stated before the run (METHOD-12): nothing
consumes the reader, so a moved pixel would have meant an accidental behaviour change, not a
finding.

**Also corrected here:** `docs/formats/clutter.md` still described the world-space grid, "quad size
= the tiling period", the `BuriedClutterDistricts` exemption and an "**Undecoded:** exact alignment"
section — rows 1, 2, 3 and 9 of this plan's disproven table, in the sibling page of the one C21 was
adding. B14 rewrote `architecture.md` and the class comment but not this file. Fixed, with the
pre-rewrite counts kept as history.

### Original approach (kept for reference)

**Goal.** A reader over `extracted/<chapter>/zrdr/templates.zrd.json` producing per-model-name
property blocks, plus the format page that documents every key and what the engine does with it.

**Evidence (confidence: traced).** `FUN_004deab0` is the parser, key by key, with the field offsets
and the unit conversions (`rotation_range` is degrees → radians; `min_slope`/`max_slope` are degrees
→ **cosines**, and note the inversion: `min_slope`'s cosine becomes the *upper* bound). Defaults from
`FUN_004de7d0`'s initialiser: scale 1.0/1.0, slope bounds −1.0/+1.0, `align_normal` false. The eight
shipped files are surveyed in *What the data actually ships*, and **which keys they author is already
settled** — the per-chapter count of every key is in B14's landed section (2026-08-10). Five of them
(`min_slope`, `max_slope`, `align_normal`, `rotation_range`, `translate_uv_range`) are authored by no
chapter at all, so the format page can state that as fact rather than as an open question. Do not
re-run that census; **do** still read all five keys in the reader, since "unused in retail data" is a
thing to document, not a reason to drop parsing.

**Approach.** Follow the existing `Zrdr.cs` / `ZrdrDict` pattern — this is the same reader family as
`fogvol.zrd` and `weather.zrd`, so there is a shape to copy rather than invent. Read every key the
parser accepts, including the ones no chapter uses, and record in the format page which are unused
in the retail data. Resolve the two anomalies the survey found: C3 registering `cliff1_sandtrans`
while its `templates.zrd` describes palms, and C2B shipping an empty file against six registered
templates.

**Model recommendation.** medium. A reader against a decoded spec, with the format page as the real
deliverable.

**Verify.** WORLD-23 — range-test every decoded field and corroborate its units. A parsed
`scale_range` of 0.9–1.5 is plausible; 0.9–1.5 *radians* would not be. Print a census per chapter and
check it against the byte counts in the table above.

**⚠ Traps.** (a) `substitute` weights are **not** normalised in the file — `FUN_004deab0` sums them
and divides. A 9.0/1.0 pair is 90/10, not 9:1 of something else. (b) `translate_uv_range` and
`far_fade_range` are nested pairs-of-pairs in the reader; `scale_range` is a flat pair. Do not
flatten one into the other. (c) Do not consume anything in this item — reader and docs only.

## C22 ☑ Apply `substitute` and `scale_range`

### ✅ Landed 2026-08-10

`ClutterBuilder` takes a `ClutterTemplateSpec` at construction (null = the pre-C22 monoculture at
authored size, which is what the A/B below is measured against). Per accepted stamp, after the
dedup: roll the model against the kind's cumulative `substitute` table, then draw a uniform scale
from `scale_range` and compound it onto whatever basis the placement keeps.

**⚠ Trap (b) is DISPROVEN, and it is the opposite of what this item predicted.** The plan said
"resolve properties from the *target*, not the source". `FUN_004dd6e0` says the reverse: the
decoration entry's own kind block is held in `fVar4` for the whole function — the slope gate, the
jitter, the rotation, the scale AND the fade all read it — and the substitute roll rewrites only
`local_110`, the model pointer. So a C1 `firtree2` that came from a `firtree1` roll is scaled by
*firtree1's* 0.9–1.1, while a `firtree2` the template placed itself is scaled by its own 0.9–1.5.
Implemented as the decompile has it.

**⚠ The first implementation failed DET-9, and the assertion is what caught it.** Seeding from
`Rng.IntSeedFor(Rng.Clutter)` makes the stream a function of the session master, which is
time-derived on any unpinned run — two `--freecam --chapter=C1` runs gave firtree1 15,154 then
15,148. But the original re-seeds with `time(0)` only *after* the world build
(`FUN_004df1d0`), so its forest is the same forest on every launch. The stream now runs off a
fixed constant, deliberately independent of `--seed=`; two runs are now identical to the instance.

**The measured A/B, in-suite rather than pasted** (`clutter-determinism`, three builds over the
same gamez differing only in whether a spec was handed in): C1 bare `firtree1` 16,846 /
`firtree2` 10,469 → dressed 15,182 / 12,133. 1,664 stamps moved, **9.88 % of firtree1's** against
an authored 9:1. Scales span exactly 0.900–1.500, uniform on all three axes, and the bare build's
are all 1.0. Two dressed builds in one process, no reseed between them: identical transform for
transform.

**Eight-chapter regression, zero engine errors, and the instance TOTAL is unchanged everywhere** —
substitution moves a stamp between kinds, it never adds or drops one:

| chapter | total | substituted | what changed |
|---|---|---|---|
| C1 | 37,510 (=) | 2,250 | firtree1 → firtree2 1-in-10; bush1 ↔ bush2 half each |
| C1B | 339 (=) | 0 | scale only (dougfir 0.8–1.3, bushes 0.9–1.1/0.9–1.3) |
| C1C | none | — | registers no templates |
| C2 | 46,752 (=) | 12,457 | spruce → brush1/3/4; palm1 → palm2/palm3 |
| C2B | none | — | its gamez carries none of its six registered roots |
| C3 | 707 (=) | 456 | palm1 → 251/238/218 three ways, was 707 palm1 |
| C4 | 87,239 (=) | 6,432 | firtree1 → firtree2 (a kind C4 never placed directly) |
| C5 | 178,504 (=) | 38,451 | 40 minted kinds: `cb00b`, `cb01c/d`, `cb14b/c`, `cb00det02/03`, … |

`substitute_nothing=0` in every chapter, which was predicted from the data before the run: the
install's four unresolvable targets (`hotelsign0/1/2`, `cb05det01`) all belong to C5 blocks whose
own model is never scattered, so the engine's `cannot find clutter substitution node` path cannot
fire on retail data. It is implemented anyway, and counted.

**Two design points the data settled, both recorded in `architecture.md`:**

1. **A target no template scatters is minted as a kind** — the engine resolves targets through its
   global model table, so `cb00b` and friends must be placeable without being any template's
   decoration. 40 in C5, 2 in C3, 1 in C4; every target of every *placed* decoration resolves.
2. **An entry naming the kind's own model stays in that kind.** Resolving it globally instead
   shuffled instances between the mesh duplicates one model has across templates (C5 ships the
   same building as up to four gamez meshes) — visible as C1's bush counts merging 331+255 → 586,
   with 2,662 "substitutions" where the data predicts 2,275. The original cannot express the
   distinction (it has one model per name); inventing it only moves goldens for nothing.

**`.\RunTests.ps1`: build clean, 867 units, 30 engine suites** (incl. the new
`clutter-determinism`), engine errors clean, 13 goldens hash-identical after the re-pin.

**Seven goldens moved and were re-pinned on the user's call against the images** (2026-08-10):
`c1-waterfall`, `c1-flight`, `c1-crash`, `c2-city`, `c3-island`, `c4-snow`, `c5-city-night`. The
six that held are exactly the shots with no clutter in frame (`c1b-night-sea`, `c1c-rain`,
`c2b-rain`, `c1-destroy-effects`, `viewer-bhawk`, `empty-stage`) — the able-to-fail control on
"only clutter changed". Old → new hashes are in the landing commit.

### Original approach (kept for reference)

**Goal.** C1's firs are 90 % `firtree1` / 10 % `firtree2` with per-instance scale 0.9–1.5×; C5's
`cb00a` blocks are half `cb00b`; C2's spruce mixes in brush. Reproducibly.

**Evidence (confidence: traced).** `FUN_004dd6e0` step 9 (weighted roll against a normalised
cumulative list) and step 10's final `FUN_0053a820(s, s)` uniform scale. Seeded by
`FUN_004df1d0`'s `srand(0x8EA91836)` before the world build, so the original's result is the same
every run.

**Approach.** Substitution changes which `Kind` an instance belongs to, so it must happen at
placement time, before `Kind.Instances` is filled — a substituted instance moves to the target
mesh's kind. Scale is a per-instance basis scale on the `Transform3D`, which both the sprite and
solid MultiMesh paths already carry. Seed a dedicated PRNG at build start; do **not** reach for
`Math.random`.

**Model recommendation.** high. Determinism plus a cross-kind data flow (an instance changing kind
mid-placement) is where this goes wrong quietly.

**Verify.** DET-7 and DET-9 — the result must depend only on committed inputs and be identical
across two runs; assert that in a test. Then the visual: a C1 forest golden showing mixed species
and varied heights where `main` shows a monoculture.

**⚠ Traps.** (a) The seeded stream's *order* is part of the result. Ours will not match the
original's byte for byte (different PRNG, different traversal), and chasing that is not worth it —
what matters is that ours is stable run to run. Say so in the comment so nobody later tries to match
`0x8EA91836` exactly. (b) A substitute target may be a model that is itself a registered kind with
its own properties (`cb00b`, `firtree2` both appear as their own entries) — resolve properties from
the *target*, not the source. (c) Scale on a solid decoration compounds with its authored basis;
apply it as a uniform scale, not a basis replacement. (d) Sprite kinds get their extents from
`SpriteInfo`, which feeds `ExtraCullMargin` — a 1.5× scaled tree needs the margin to grow with it or
it will pop at the screen edge.

## C23 ☐ Survey `far_fade_range`, `rotation_range`, `translate_uv_range` → decide or hand to backlog

### ⚠ Half of this item is already answered (2026-08-10) — census run under B14

`rotation_range`, `align_normal` **and `translate_uv_range` are authored by NO chapter**, C2 and C5
included (the two this item flagged as "need checking"). Counts are in B14's table above. So all
three are **decodes to document, not features to build**, and `FUN_004dd6e0`'s step 5 and the
rotate/align half of step 10 are inert on retail data — they cannot move a decoration anywhere in
the install. That is what makes the user's *"C1 positions are exactly the same as the original"*
structurally possible, and it is the strongest single piece of evidence this plan produced for B12.

What is left of C23 is therefore **`far_fade_range` alone** (authored on all 143 blocks — C21's
count, and C21 also settled its pair grouping: `[[nearMin, farMin], [nearMax, farMax]]`), and the
`CameraSetClutterFadeScaleSq` question below stands unchanged.

**Goal.** A written decision on each of the three remaining authored behaviours: implement, defer
with a `backlog.md` entry, or record as a deliberate deviation.

**Evidence (confidence: lead-only).** `far_fade_range` is authored by every chapter and is a
per-instance random near/far pair, squared and compared against camera distance, scaled at runtime
by the `CameraSetClutterFadeScaleSq` script command (string at `0x0063f5bc` — its consumers are
unread). C5's city blocks fade at 200–350 m, C1's firs at 500–2000 m. `rotation_range` and
`align_normal` are authored by **no** chapter in the shipped data, per the survey — which makes them
a decode to document, not a feature to build. `translate_uv_range` appears in none of the small
files; C2's and C5's need checking (C21 will have).

**Approach.** Read `CameraSetClutterFadeScaleSq`'s consumers to find whether the fade scale is
per-mission or a detail setting, because that changes whether the authored metres are literal.
Then write the decision. Expect the outcome to be: fade → `backlog.md` (it is a rendering-side
change, needs a distance-fade path in two shaders, and it interacts with `MapEdgeExtender`);
rotation → documented as unused; jitter → implement in `PlaceOnTriangle` if any chapter authors it,
since it is three lines at the point B12 already touches.

**Model recommendation.** medium. Survey and decision; the implementation, if any, is small.

**Verify.** The deliverable is prose plus, if a `backlog.md` entry results, that entry. Nothing to
measure unless the jitter lands, in which case: instance positions must change and the count must
not.

**⚠ Traps.** (a) Implementing `far_fade_range` mid-plan would confound every Wave B A/B — that is
Decision 3 and it stands. (b) A backlog entry for the fade must carry the authored ranges and the
`CameraSetClutterFadeScaleSq` finding, or the next session re-derives them. (c) "No chapter authors
`rotation_range`" is a statement about the retail install — say which files were checked, per
LOG-2.
