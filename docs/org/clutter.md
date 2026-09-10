# The clutter/decoration system, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-09/10. The decompile names its own source
file, `D:\zipper\gamez\zclass\cls_clutter.cpp`. Every claim below names the function it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side is two files and two pages: the boot script's
`AddClutterTemplates` registration and the template subtree's shape are
[`formats/clutter.md`](../formats/clutter.md); the per-decoration properties file `templates.zrd`
— its schema, its offsets and the per-chapter census of what the retail data actually authors — is
[`formats/templates.md`](../formats/templates.md). Our implementation is
`CSVM/src/Mech3/Clutter.cs` and `CSVM/src/Mech3/ClutterTemplates.cs`, whose entries in
[`architecture.md`](../architecture.md) carry the plumbing and the remake-only rules;
`CSVM/src/Effects/FogVolumeClutter.cs` is a *second* consumer of the same template-root lookup
(the `fogvol.zrd` cloud field) and shares nothing else. The measurements this page leans on are
`analysis/bl-305-clutter-uv/FINDINGS-A1.md` (the tiling census), `-A2.md` (the ground-quad UV
parameterisation, the world-side UV frames, and the hand-computed worked example),
`-A3.md` (the per-layer walk) and `-layer-pairing.md` (the `no_clutter` layer pairing).
This page is the original's runtime: what the engine does with those files.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement or a
screenshot count, the decode wins and the disagreement is a note. Where CSVM deliberately differs,
that is listed at the bottom rather than hidden.

## Function map

| Address | Role |
|---|---|
| `FUN_00463f40` | Loads `templates.zrd` at startup, before templates are resolved or the world is built |
| `FUN_004de7d0` | Per-block initialiser: allocate, **write the defaults**, `strdup` the name. A block with no `node` key allocates nothing |
| `FUN_004deab0` | Per-key parser: the substitute weight list (sums and divides as it stores), degrees→radians, the slope cosines, the damage blocks |
| `FUN_004dd230` | Template resolution: matches a `templates.zrd` block to a decoration by `strcmp` on the gamez node name, and projects each decoration onto the template's ground quad to store its **interpolated texture UV**, `fmod`-wrapped into [0, 1) |
| `FUN_004de190` | The per-polygon **texture-layer** loop — every layer is examined, not just layer 0 |
| `FUN_004de2c0` | The per-polygon gate: the `no_clutter` bit `0x800`, and the null-UV-array skip |
| `FUN_004dd6e0` | **The stamper** — eleven steps, from the slope gate through the UV lattice to the per-instance fade |
| `FUN_004df1d0` | Wraps the whole world build in `srand(0x8EA91836)` … `srand(time(0))` |
| `FUN_004d0280` | The engine's global model lookup — how a `substitute` target name becomes a model |
| `FUN_0053a820` | The uniform-scale setter step 10 ends on (`s, s`) |
| `FUN_0057a130` | The reader's "was this key present?" probe — what arms a damage block on `health` |
| `FUN_00523820` | Animation-definition resolution for a damage block's `anim` key, at parse time |
| `0x005654f6` | `gg_load.c`'s single writer of the `no_clutter` polygon bit, from `strstr(name, "no_clutter")` on the node name |
| `CameraSetClutterFadeScaleSq` (`0x0063f5bc`) | One global scaling *every* type-5 scene node's distance fade; defaulted per graphics detail level (×1/×2/×3, `FUN_00440750`) and overridable per mission |

Two source-level facts worth carrying: the block store is a flat global list
(`DAT_00727c44`…`DAT_00727c48`) appended to in load order, and the block is `0x88` bytes.

## Looking a decoration up (`FUN_004dd230`)

Two jobs live in this one function, and both are load-bearing.

**1. Which `templates.zrd` block describes this decoration.** A plain `strcmp` against the gamez
node's own name — with the `.flt` suffix, which is part of the name in both the file and the gamez
(exact across all 143 shipped blocks). The scan is **linear over the load order and stops at the
first match**, and the loader appends.

⚠ **A duplicated name's later blocks are unreachable.** The install ships exactly one duplicate,
C5's `cb05det01.flt`: the *first* block carries a 50/50 `substitute`, the second carries only
`scale_range` and `far_fade_range`. The first wins, so the substitute survives and the second block
is dead data. A reader that collapses duplicates by last-write-wins gets C5's detail blocks wrong,
which is why `ClutterTemplateSpec` surfaces `DuplicateNodes` instead of silently merging.

⚠ **A decoration with no block at all is legal and means "every default"**, never "do not place" —
the stamper guards each kind-driven step on a null kind pointer. In the shipped data the surplus
runs the other way: every *placed* decoration has a block, and the extra blocks belong to models
reachable only by substitution — which, by the source-properties rule below, are never read.

**2. Where the decoration sits on the ground quad.** Not metres. The engine ray-casts the
decoration's local position **±5 along the quad normal**, reads the ground polygon's own
*interpolated texture UV* at the foot of that projection, and wraps it into [0, 1). That stored UV
pair is the decoration's position; the quad is the domain the position is normalised against.
A decoration that projects onto nothing logs
`template %s clutter %s does not project to polygon.` and is skipped.

⚠ **No decoration in the retail install takes the miss path** (A2's `notproj` column is 0 for all
32 resolving templates), so implementing it is fidelity rather than a case any chapter exercises —
but a miss must skip and say so, never quietly land at UV (0, 0).

**The `fmod` wrap has a negative branch.** A negative coordinate maps to `1 − frac`, and an exact
`1.0` (a fraction that rounded away) collapses back to `0`. ⚠ **No retail decoration reaches the
negative branch** — all 32 resolving ground quads span exactly 0..1 in both axes, install-wide, so
the wrap folds nothing on shipped data. The branch is reproduced anyway because the original has it.

### ⚠ The quad's UV parameterisation is two signed axes, not one scalar period

The tempting shortcut is `max(extentX, extentZ)` as a tiling period and `(origin − minCorner) /
period` as the fraction. It is an **exact relabelling on 28 of the 32** templates (agreement at
float noise, 1.1e-16) and **wrong on the other four**:

| template | why | worst error |
|---|---|---|
| C2 `filmblock1` | quad is 64 × 128 m; the long side's scale is applied to U as well | 0.26 UV |
| C3 `cliff1_sandtrans` | quad is 128 × 64 m; the same, on V | 0.30 UV |
| C2 `parklot1` | 16 × 32 m **and UV-mirrored** — u = 0 at max X | **0.74 UV** |
| C2 `parklot2` | 32 × 16 m, same mirroring | 0.72 UV |

Two axes carrying signs are the least that describes them. Nothing keys off corner order either:
the winding differs between templates (`terpat02` and `cblock1` start at different corners and run
opposite ways) while the parameterisation does not, so anything reading "corner 0" reads those two
inconsistently.

## The kind block, and the defaults nothing authors

`FUN_004de7d0` writes the defaults before any key is parsed, so its initialiser — not a convention
— is the answer to "what does an unauthored key do?". The full offset table is in
[`formats/templates.md`](../formats/templates.md#keys); what belongs here is which of those values
the whole install actually runs on:

| Field | Engine default | Authored by |
|---|---|---|
| `scale_range` (+0x14/+0x18) | 1.0, 1.0 | all 143 blocks |
| `far_fade_range` (+0x2c…+0x38) | 0 — and a zero `farMax` is the **"never fades" sentinel** | all 143 blocks |
| `substitute` (+0x08…+0x10) | empty (always stamps itself) | 41 blocks |
| `translate_uv_range` (+0x1c…+0x28) | 0 | **no chapter** |
| `rotation_range` (+0x40…+0x54) | 0 | **no chapter** |
| `align_normal` (+0x3c) | false | **no chapter** |
| `min_slope` / `max_slope` (+0x5c / +0x58) | **+1.0 / −1.0**, i.e. no cull | **no chapter** |
| `OnWeaponHit` / `OnCrater` / `OnCollide` | absent | **no chapter** (and `OnCrater` has no reader either: [`craters.md`](craters.md)) |

⚠ **This is the single most consequential negative in the whole system.** The three keys that could
move or turn a decoration — `translate_uv_range` (step 5's jitter), `rotation_range` and
`align_normal` (half of step 10) — are authored by nobody, so **the original's placement has no
random input affecting position or orientation at all**. Its forests are a deterministic function
of the terrain UVs and the template. That is the structural reason an unseeded reimplementation of
the lattice reproduces C1's tree positions *exactly* rather than approximately, and why
implementing step 5 would change nothing on retail data. Treat those three as decoded and closed.

⚠ **The slope keys invert.** Cosine decreases with angle, so `min_slope` (the minimum slope *angle*)
becomes the **maximum** admissible normal Y and `max_slope` the minimum. Naming a field after the
key it came from is how this gets implemented backwards; name it after the thing it bounds. Since
neither key is authored, the cull is off everywhere — and a remake-side invention in its place
(CSVM once carried `MinSlopeCos = 0.25`, "steeper than ~75° grows no trees") has no counterpart
here. It never fired either: the steepest clutter-eligible triangle in the install is C1's at
|Ny| = 0.4598 (~62.6°), against a 0.25 (~75.5°) threshold, so the zero it culled is a measurement,
not a coincidence. The same instrument at 0.50 culls 3 triangles in C1.

⚠ **A damage block is armed by its `health` key, not by its presence.** `FUN_004deab0` reads
`model` and `anim` only when `FUN_0057a130` says `health` was found, and takes the "is destructible"
bit from that same test. All three blocks are unauthored install-wide.

## Which polygons get dressed (`FUN_004de190`, `FUN_004de2c0`)

The engine dresses every world polygon whose texture names a registered template's ground texture.
`FUN_004de190` iterates **every texture layer** of the polygon, not just layer 0 — though A3
measured that no polygon in the install names a registered template on layer 1 or above, so the
extra layers have nothing to find. `FUN_004de2c0` then gates the polygon on two things: a **null UV
array** (no UVs, no lattice, skip) and the polygon flag bit **`0x800`, `no_clutter`**, authored by
node name via `strstr(name, "no_clutter")` in `gg_load.c` and written at exactly one site
(`0x005654f6`).

⚠ **`no_clutter` does NOT mean "leave this ground bare".** Where two **coplanar** layers are painted
over each other — as the whole of C5's city is — the flag selects *which layer decorates*: flagged
means skip the overlay, so the layer beneath stamps instead. C5's flagged ground is dressed by
`cblock4/5/6` (low-rise, ≤52 m) and its clear ground by `cblock1/2/3/7` (towers, ≤108 m). Measured
over every `cblock1/2/3/7` polygon by exact XZ polygon-intersection area, odds ratio **1,036.8×**,
and confirmed at the controls. Getting it backwards buries the pavement between the blocks under
oversized towers.

⚠ **The gate and any "these districts are buried, suppress them" exemption are ONE COUPLED
CHANGE.** The gate alone empties C5's downtown (its ground *is* the flagged layer, whose dresser
was the suppressed one); the un-suppression alone doubles the city and interpenetrates it. Neither
is correct without the other.

## The stamp (`FUN_004dd6e0`)

Eleven steps. What the harvested material characterises:

| Step | What it does |
|---|---|
| 3 | **Slope gate** — clamp the triangle's plane normal Y to [−1, 1], reject outside `[cos(max_slope), cos(min_slope)]` |
| 4 | **The lattice** — take the triangle's UV bounding box and **floor it to integers**, inclusive at both ends |
| 5 | **UV jitter** — add `translate_uv_range` per axis to the candidate. Unauthored, therefore inert |
| 6 | **Containment, in UV space** — the three edge cross-products, **strict** on the edge |
| 7 | **Recovery** — the world XYZ (including Y) through the triangle's own affine UV→world map |
| 9 | **The substitute roll** — one uniform draw walked against the cumulative normalised shares |
| 10 | **Rotation and scale** — `rotation_range` / `align_normal` (unauthored), then a uniform scale drawn from `scale_range` and set via `FUN_0053a820(s, s)` |
| 11 | **`far_fade_range`** — the per-instance near/far fade distances |

Steps 1, 2 and 8 are not characterised by the material this page was harvested from; the numbering
is the decode's own and is kept so the cross-references in the code and in
[`formats/templates.md`](../formats/templates.md) stay meaningful.

### The lattice rule, and why it cannot be done in world space

A decoration is stamped **once per integer repeat of the ground texture across the triangle**.
Concretely: floor the triangle's UV bbox (step 4); for each integer cell `(uInt, vInt)` and each
decoration, the candidate texture coordinate is `(uInt + u_deco, vInt + v_deco)` where `(u_deco,
v_deco)` is the quad UV `FUN_004dd230` stored; test that candidate for containment **in UV space**
(step 6); recover its world position through the triangle's affine map (step 7).

Nothing in that path references a world axis. **The clutter therefore rotates, mirrors and
stretches with the painted ground texture** — which is the intent: the decorations were authored
against the painted texture, so a building sits in its painted block wherever that block lands.

That is not a stylistic point; the data forbids the alternative. A2 measured the world-side UV
frames per triangle and found **eight distinct axis-aligned frames with no rule choosing between
them**, varying *within a single mesh*. On C1's `terpat02` the dominant frame is **+U → world +Z,
+V → world −X** — a 90° rotation from the template quad's own +U → +X, +V → +Z — at 69.6 % of 2,979
triangles, with 5.7 % off-axis entirely; and **handedness is split almost evenly** (1,441 mirrored
against 1,538 not), so a mirrored UV frame is ordinary shipped data rather than corruption. C5's
`cblock1` is the opposite extreme at 97.7 % identity. A world-space stamp would have to pick one
frame and would be wrong on most of C1.

⚠ **The map is valid only inside its own triangle.** Neighbouring triangles of the same polygon
routinely carry different UV frames, so reusing one triangle's map for the next is not an
optimisation, it is a wrong answer.

⚠ **UVs are indexed by CORNER POSITION, not by vertex id.** C1 model 953's polygon 3 lists vertex 5
at two different corners with two different UVs; reading the UV through the vertex id stamps the
second corner's lattice in the first corner's texture frame. The same polygon is a `tri_strip`, not
a fan, so the corner triples differ too.

⚠ **A zero-area *world* triangle can carry a perfectly healthy *UV* area.** Fan/strip artifacts of
n-gons with repeated or collinear corners — 1,655 of them on C1's `terpat02` alone — pass a UV-area
guard and hand back a finite but meaningless affine map with both axes collapsed onto a line, which
plants a row of trees along that line. Guard on **both** areas independently and count them apart.

**The worked example is not repeated here.** One decoration (C1 `terpat02`'s `firtree1.flt`, node
5909) on one named world triangle (C1 node 2911 `g777`, model 953, polygon 3, triangle 5), computed
by hand and re-derived by script — the quad UV `u = (x + 256)/512 = 0.708715`, `v = (z + 256)/512 =
0.630430`, the affine axes `A = (0, 0, 256)` per +1 U and `B = (−256, 0, 0)` per +1 V, the four
candidate cells of which exactly one is contained, and the resulting world point — lives in
[`analysis/bl-305-clutter-uv/FINDINGS-A2.md`](../../analysis/bl-305-clutter-uv/FINDINGS-A2.md)
§ "Worked example", and is pinned as a test in `CSVM.Tests/ClutterQuadUvTests.cs`. The affine
recovery agrees with an independent barycentric interpolation to 1.8e-12 m, which is also why step
7's Y needs no second computation: on a planar triangle — and a triangle cannot be anything else —
the two routes are the same number.

⚠ **There is no world-space grid and no global "the clutter grid starts here" origin anywhere in
the original.** A2 looked. A fixed X/Z grid is a fiction with no counterpart, and the density error
it causes is not subtle: C1's terrain is painted with `terpat02` at **half** the template quad's
scale, so one repeat spans ~260 m where a 512 m grid stepped — roughly **4× too few trees**. The
factor-2 templates are C1's, which is precisely where the forest reads sparse; it is a real density
term, not an artifact of the measurement.

⚠ **Step 6 is strict on the edge.** A candidate landing exactly on the diagonal that two triangles
of one fan or strip share is claimed by **neither**, not both. An inclusive test double-stamps that
diagonal — measured, it is where the bulk of C1B/C2/C3/C5's duplicate placements came from.

### Step 9, the substitute roll

One uniform draw walked against the kind's cumulative shares; the engine subtracts each normalised
share and takes the first entry that sends the draw negative. A kind with no table always stamps
itself, and a draw falling past the last entry (float error only, since the shares sum to 1) does
too. A target that the global model lookup (`FUN_004d0280`) cannot resolve logs
`%s: cannot find clutter substitution node, interpreting it as nothing.`, is stored as a null
model, and **keeps its share of the roll while placing nothing**.

⚠ **A substituted stamp keeps the SOURCE kind's properties.** The stamper holds the decoration
entry's own kind block (`fVar4`) throughout, and the roll rewrites only the model pointer — so
scale, fade and the slope gate all come from the model the *template authored*, not from the one it
became. A C1 `firtree2` that arrived via a `firtree1` roll is scaled by firtree1's 0.9–1.1, while a
`firtree2` the template placed directly is scaled by its own 0.9–1.5. The consequence for the file
is that a block reachable *only* as a substitution target is never read at all;
[`formats/templates.md`](../formats/templates.md) states the same rule from the authored side.

⚠ **The target resolves through the engine's global model table, i.e. to ONE model however many
templates mention it.** C5 ships the same building as up to four gamez meshes, one per template
that uses it; the original cannot express "which copy", so neither should a remake.

⚠ **A stamp is rolled ONCE.** The engine rolls the source's list and places the result; it does not
then re-roll the target's own list.

### Step 11, the fade — and the nested-pair grouping

⚠ **The two file pairs are the MIN and MAX of the two distances, not the two distances
themselves.** `FUN_004dd6e0` lerps the *near* distance between `kind+0x2c` and `+0x30` — the two
pairs' **first** components — and the *far* distance between `+0x34` and `+0x38`, their **second**.
So C1's `firtree2` at `[[300,600],[1000,2000]]` fades starting somewhere in 300–1000 m and is gone
somewhere in 600–2000 m. Reading the pairs as two bands gives 300–600 and 1000–2000: a different
rule that *happens to agree* on the many kinds whose numbers are chained (`[[200,300],[300,350]]`),
which is exactly what makes the mistake survivable long enough to matter. The same min-pair /
max-pair grouping applies to `translate_uv_range` and `rotation_range`. `scale_range` is **flat**
(`[min, max]`) and must not go through the same helper.

⚠ **Both fade distances come from ONE `rand()` draw** — near and far are perfectly correlated per
instance, not rolled independently.

The stamp stores the two distances SQUARED, plus `1/(far² − near²)` (0 when they coincide), and a
`farMax` of 0 stores `FLT_MAX` for both: never fades. The consumer (`FUN_004d5de0`) multiplies
`_DAT_0062d170` into the node's squared camera distance, draws opaque inside near², skips the node
at or beyond far², and blends `(far² − scaled) × recip` between, dropping anything under 0.004 and
treating anything over 0.996 as opaque. So the ramp is linear in squared distance.

⚠ **The authored metres are literal at HIGH and SHORTEN as detail drops.**
`CameraSetClutterFadeScaleSq` (`0x0063f5bc`) writes that one global for every type-5 scene node,
and the graphics EffectsLevel setter (`FUN_00440750`) defaults it: 1.0/4.0/9.0 for levels 0/1/2,
where `FUN_0043f6d0`'s word table maps HIGH/MEDIUM/LOW to 0/1/2. Because the scale multiplies the
distance rather than the thresholds, MEDIUM fades at half the authored metres and LOW at a third.
The earlier reading of "×1/×2/×3 with detail" had the direction backwards. No shipped mission
script issues the command. The remake's global is `csky_clutter_fade_scale_sq`
(`CSVM.Utils.EffectsLevel`, default HIGH as `detail.zrd` selects on any modern CPU).

The fade is a performance measure in a 1999 engine, so the remake can decline it: the
`graphics.clutterFarFade` config key (bool, default `true`, the decoded behaviour) resolves the
same global to 0 when set `false`. Zero is a never-fades scale, not a fade-everything one, because
the scale multiplies the distance rather than the thresholds: the scaled distance never reaches any
near², so every stamp keeps full alpha and its full card, exactly the arm the engine itself takes
for a `farMax` of 0. Clutter then draws out to the map-edge window under the mission's fog.

## The weight list (`FUN_004deab0`)

The per-key parser, and three of its conversions are traps.

⚠ **`substitute` weights are relative, and the file never normalises them.** `FUN_004deab0` sums
the list and stores each entry as `w / total`. So C1's `firtree1` at `[[9.0, firtree1], [1.0,
firtree2]]` is **90 % / 10 %** — not "nine of something", not 9:1 out of some other total. Weight
sums in the shipped data run 8.5 to 20. The extreme case is C5's `hotelsign0`, which weights
*itself* 0.1 against two alternatives at 5.0 each: 1 % / 49.5 % / 49.5 %, i.e. it is replaced 99 %
of the time. Every shipped list names its own model as one of the alternatives — that is how
"usually stays itself" is expressed. A zero total makes the engine skip the list entirely.

⚠ **`rotation_range` is DEGREES in the file and RADIANS in the block** — `FUN_004deab0` multiplies
by `0.01745329251994` as it stores. And the engine still draws **three `rand()` values per stamp**
for it even when the key is absent, so it is inert in *effect* rather than skipped in the *stream*.
That only matters to somebody trying to match the original's draw order, which is not achievable
(see below) and not worth chasing.

⚠ **`min_slope` / `max_slope` are angles in the file and COSINES in the block** (`kind+0x5c` /
`+0x58`), which is where the inversion documented above comes from.

## The seed (`FUN_004df1d0`)

The original wraps its **whole world build** in `srand(0x8EA91836)` … `srand(time(0))`. A chapter's
forest is therefore the same forest on every launch: variety across launches is a property this
system deliberately does not have. Since neither the position nor the orientation of a decoration
has any random input on retail data (see the defaults above), the only things the stream feeds are
the substitute roll and the uniform scale.

⚠ **The original's *stream* cannot be reproduced and matching it is not worth attempting.** It
would take the same PRNG, the same traversal order **and** the same number of draws per stamp — and
the engine spends three rotation draws per instance that retail data renders inert. Borrow
`0x8EA91836` as a label if you like; what is reproducible, and what matters, is the *property*: the
placement is a function of the data alone, identical on every launch.

## Where CSVM deliberately differs

Everything here is a known, deliberate divergence — not a gap waiting to be closed.

| Divergence | Why |
|---|---|
| **Step 11's fade draw is off its own stream** | The engine draws the fade in the same `rand()` stream as the substitute and scale; the remake draws it from a second generator on the same seed, so landing the fade left every species and scale draw where it was. The stream is not the original's either way |
| **Step 11's ramp is dithered, not alpha-blended** | The original blends the node's alpha; the remake keeps the sprites and blocks in the cutout pass and dithers the ramp (`csky_clutter_dither_keep`), because moving C5's 139k sprites into the transparent pass is a frame-time risk |
| **Step 5's jitter, and step 10's rotation / `align_normal`** | INERT, not missing: no chapter authors any of the three, so they cannot move a decoration on retail data. Implementing them would change nothing |
| **A quarter-metre `seen` dedup per (kind, x, z)** | Remake-only; the original has no such set. It once stood in for the missing `no_clutter` gate (retired) and for an inclusive step-6 edge test (fixed 2026-08-10). ⚠ It is still earning its keep: after the strict-edge fix C1B/C2/C3/C5 drop to exactly 0 rejections but **C1 only drops 38→36 and C4 is unchanged at 139**, so most of *their* duplicates come from a source nobody has found. Do not remove it on the strength of the edge fix |
| **A `MaxLatticeCells = 4096` tripwire** | The original has the same exposure to a hugely stretched triangle and no bound. A1 measured every shipped span as modest (`terpat02` runs 134–561 m per U), so this should never fire; if it does, the count is a finding |
| **A 0.5 m² XZ-footprint floor per triangle** | Remake-only, and it does fire — 14 triangles in C5, 0 everywhere else |
| **Sprites are not collidable; 3D decorations are** | Both user decisions. A billboard has no solid side to hit — its collider would be a phantom wall wherever the card happens to face — while a city block has real sides and does not turn. ⚠ Do **not** re-argue tree collision from the `spruce_destroy` anims: those two files live under `planes\`, define `g_engine*` / `prop_part` / `spin` / `snd_propstart`, and are the **Spruce Goose**. There is no tree-destruction animation of any kind in the install |
| **A fixed placement seed distinct from the session master** | Matches the original's *property* (a chapter's forest is fixed) without pretending to match its stream |
