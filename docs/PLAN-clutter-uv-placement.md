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
| 4 | `MinSlopeCos = 0.25f` — "steeper than ~75° grows no trees" (`Clutter.cs:78`) | An invention. The original's slope cull is authored per kind (`min_slope`/`max_slope` → cosines at kind+0x58/+0x5c, tested against the triangle normal's Y in `FUN_004dd6e0`) and **defaults to ±1.0, i.e. no cull at all**. No chapter's `templates.zrd` authors either key. This constant is silently deleting hillside trees today. |
| 5 | `BL-305`'s "prime suspect: `ClutterBuilder` tiles each template on a fixed world-space X/Z grid" | No longer a suspect — confirmed as the mechanism. The entry's own wording predates the decode. |
| 6 | (Mine, earlier this session) "C3 has the suburbs" | C3 registers exactly one template, `cliff1_sandtrans`. **C2** carries the suburbs (`resblock1-6`, `filmblock1-5`, `parklot1/2`, `parkpat`). Corrected against `extracted/interp.json`; the full census is below. |
| 7 | (Mine, from A1's ratios) "√2 in the quad-vs-world ratios means a 45°-rotated UV mapping", and "the exact-2 cases are quads spanning two texture repeats" | A2 read the UV coordinates themselves: **no 45° mapping exists anywhere in the install** (every bearing on those templates is 0° or 90°) and **every quad spans exactly 0..1**. The √2 was an artifact of A1's own statistic — a max-extent `Period` compared against a geometric mean, on a 2:1 quad. Passed to A2 as a flagged lead, not a finding, and killed there. |
| 8 | "B11 can land as a provably inert, behaviour-preserving step" (this plan's own B11, as written) | True for 28 of the 32 templates, false for four: the current scalar `Period` genuinely misplaces `filmblock1`, `cliff1_sandtrans`, `parklot1` and `parklot2` by up to 0.74 UV. B11 is amended to predict exactly which four move. |
| 9 | "Zero coplanar pairs → delete the `seen` dedup set" (this plan's own B13, as written) | A3 measured zero, but the zero is the *original's*: flag `0x800` **is** the subface mark, so the original never considers a subface polygon. The remake's `PlaceOnMesh` reads no such flag, so `seen` is the only thing suppressing a real double-stamp — 449 subface polygons in C5. B13 is amended: add the subface skip first, *then* delete the dedup. |

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

No chapter authors `min_slope`, `max_slope`, `align_normal` or `rotation_range`. `translate_uv_range`
appears in none of the small files; **check C2's and C5's before assuming that holds** (C21).

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
12. ☐ Replace the world-space grid stamp with the per-triangle UV-lattice walk
13. ☐ Settle the two remake-only rules: `MinSlopeCos` and the `seen` dedup
14. ☐ Chapter A/Bs + the 8-chapter regression, and rewrite the class comment

### Wave C — The authored per-kind data

21. ☐ `templates.zrd` reader + `docs/formats/templates.md`
22. ☐ Apply `substitute` and `scale_range`
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

## B12 ☐ Replace the world-space grid stamp with the per-triangle UV-lattice walk

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

## B13 ☐ Settle the two remake-only rules: `MinSlopeCos` and the `seen` dedup

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

## B14 ☐ Chapter A/Bs + the 8-chapter regression, and rewrite the class comment

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

## C21 ☐ `templates.zrd` reader + `docs/formats/templates.md`

**Goal.** A reader over `extracted/<chapter>/zrdr/templates.zrd.json` producing per-model-name
property blocks, plus the format page that documents every key and what the engine does with it.

**Evidence (confidence: traced).** `FUN_004deab0` is the parser, key by key, with the field offsets
and the unit conversions (`rotation_range` is degrees → radians; `min_slope`/`max_slope` are degrees
→ **cosines**, and note the inversion: `min_slope`'s cosine becomes the *upper* bound). Defaults from
`FUN_004de7d0`'s initialiser: scale 1.0/1.0, slope bounds −1.0/+1.0, `align_normal` false. The eight
shipped files are surveyed in *What the data actually ships*.

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

## C22 ☐ Apply `substitute` and `scale_range`

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
