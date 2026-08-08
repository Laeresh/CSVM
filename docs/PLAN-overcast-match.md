# Overcast match — the C1 IA1 sky as one picture

**ACTIVE PLAN** (written 2026-08-08). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

The goal is one picture, twice: our matched-pose renders of
`OriginalScreenshots\C1 IA1 Fog river.png` and `OriginalScreenshots\C1 IA1 Fog above clouddeck.png`
read as the same scene as the originals. Both already have CSVM twins in `Z:\CSVM\Screenshots\`
(same filenames, freecam, no HUD/plane, exact pose in the overlay), and the twins show the three
defects this plan removes: white sprite bottoms hanging below the deck where the original has a flat
mottled sheet, a flat gray fog band wedged between dome and cloud tops where the original's sky is
clear, and a deck underside +54 too bright. Three waves — scatter, fog, brightness — each
research → A/B → solution, each handing the next a cleaner measurement.

**Absorbed items, each re-verified still-open on 2026-08-08 against both the record and the code:**
`BL-312` (the lattice comb is still the shipped mechanism — `FogVolumeClutter.cs` scatters on the
`distance` grid with ±15 % jitter), `BL-303` (the fragment-altitude fade is still what
`csky_atmosphere.gdshaderinc:27` computes), `BL-100` (C1/C1C/C2B/C4 zone choice still open;
`--sky-zone` default still `zone2`), `BL-101` (no fog A/B has landed since it was filed), and
`BL-118` itself — **partially landed**: the whiteout's hardcoded white became the authored
`CLOUD_COVER` colour in commit `9b69568` (2026-08-08, merged); the deck **mesh** underside
brightness, the mesh↔sprite cut, and the night-moonlit finding remain. Out of scope: the
night-moonlit puffs (split to a new BL at close, decision 2) and any chapter's fine-tuning this
plan does not measure (a later mismatch mints a fresh, specific item — decision 10).

## Milestone goal

- The `fvol` cloud field shows no lattice at any grazing angle, no visible field edge over the base
  map, CAP-12-like density — and its cards sit top-anchored, so the deck field is visible only from
  above while the underside view shows the deck mesh.
- The fog model reproduces both reference stills: valley haze below the deck, clear sky above it —
  and fixes `BL-303`'s three broken scenes (C3 murk, C2B gray dome, C5 black sky) with the same
  mechanism, without regressing the scenes CAP-11 confirmed healthy.
- The deck mesh underside measures within ±10 of the original's 167, tops and interior stay matched,
  and the mesh↔sprite boundary blends the way the original does.
- `BL-118`, `BL-312`, `BL-303`, `BL-101` closed; `BL-100` closed or narrowed to what footage cannot
  settle; every verdict cited to evidence.

**This plan matches C1 IA1 (plus BL-303's three poses) and nothing else.** Other chapters get the
mechanism fixes for free but no per-chapter tuning pass — that is deliberate: a chapter-specific
mismatch found later becomes its own item with its own evidence, not scope creep here.

## Decisions (2026-08-08, grilling session)

| # | Question | Decision |
|---|---|---|
| 1 | Which related items does the plan absorb? | **BL-312, BL-100 (C1 slice), BL-303 — all in, aiming to close** — they are the same three defects seen from the backlog's side. |
| 2 | Night-moonlit puffs (CAP-11 C1B) in scope? | **No — split out as a new BL when BL-118 closes** — a lighting feature, not needed for two daytime stills. |
| 3 | Wave order | **Scatter → Fog → Brightness** — each wave hands the next a cleaner measurement (sprites pollute the underside view; fog pollutes luminance). |
| 4 | Scatter mechanism | **Criteria fixed, wave picks** (random in-volume vs unbounded tiling, likely hybrid per volume shape). |
| 5 | How zone verdicts land | **No per-chapter table.** User hypothesis: zones are altitude-dependent, not per-map — find the switch point; re-examine whether `FOG_ALTITUDE` was misread. |
| 6 | Binary RE if footage can't discriminate? | **Footage-first; pause and ask the user again if stuck.** Not pre-authorized. |
| 7 | May the wave reopen the force-fogged dome? | **Yes.** Constraint corrected by user: the river still shows no skybox at all — dome regression checks need poses where the dome is visible (the C3/C2B BL-303 poses). |
| 8 | Match bar | **Region luminance boxes ±10, per region and per population (mesh vs sprites), plus a PT verdict** for what numbers can't judge. HUD/plane regions excluded on the original side. |
| 9 | New original captures | **"Whatever it takes"** — waves mint CAP items freely as questions arise. |
| 10 | BL-101 fate | **Closes with this plan.** Later chapter-specific fog mismatches mint fresh items. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The `fvol` field fills its volume **uniformly** in Y | CAP-12 C4 take: clear air at 1135 m *inside* the 1060–1180.5 volume; uniform fill hangs 132.3 m cards to ~956–990 m — no gap expressible. Top-anchoring predicts both C4's gap and C1's whiteout onset (982/1003 m). |
| 2 | `distance` is a **regular grid** period | PT-42 + CAP-12 (2026-08-07): ours combs on the 130 m lattice at grazing angles; the original shows none at any angle. The density corroboration only ever supported the *spacing* — mean spacing is invariant under randomisation. |
| 3 | `TOP_COLOR`/`BOTTOM_COLOR` tint the deck **mesh** | Three of the four authoring chapters ship no deck mesh at all; they colour the whiteout band. Fixed in `weather.md` 2026-08-08 and landed as commit `9b69568` — do not re-fix. |
| 4 | The deck is "~40 units lighter" overall | CAP-12 matched-box A/B: **+54, underside only** — tops (211/214) and interior (248/243) already match. Any fix that dims the whole deck is wrong. |
| 5 | "C1's own scripts disagree about the zone" | interp.json re-read 2026-08-06: one string names a node C1's gamez doesn't contain, the other is a UV scroll. Nothing in interp.json bears on zone choice. |
| 6 | "No chapter's fogvol has a `fog_zone` key" (HISTORY 2026-08-05) | Five chapters do — but the values select nothing identified. Treat `fog_zone` as an index into something unidentified; do not re-open the zone decode on it. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2, A3, B15 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | C22, C23 | The underside is too bright by +54 at one pose; the *mechanism* producing 167 is still open. |
| **Leads only — no mechanism yet** | B11–B14, B16, C21 | Hypothesis-driven; each may end in a disproof, which is a success if evidenced. |

**⚠ Concurrent-session note.** `worktree-bl-118-deck-brightness` (`.claude/worktrees/`) is clean and
fully merged at `73d6e21`, but its `.scratch/bl118/` holds probes newer than its last commit —
check it before Wave B/C work and fold its probes in as evidence, not surprises. Never `git stash`
in any worktree here.

## What the data actually ships

- **C1 IA1's two zones differ only in fog geometry** (`extracted/C1/IA1/zrdr/weather.zrd.json`):
  identical `FOG_COLOR` 0.69-gray and identical `SUNLIGHT` (diffuse 1.2 / ambient 0.25 →
  WorldLight 0.80). `ZONE1`: ranges 1000→1750, altitude 970→1047. `ZONE2`: ranges 1000→4000,
  altitude 4000→5000. `CLOUD_COVER` 970–1124, thickness 30, no colours.
- **The deck**: 144 `cloudlayer.tif` tiles at y=960 covering the map (`WorldBuilder.FindCloudDeck`);
  the `fvol` slab sits at 970–1090.5 with 130 m `distance`, cards 132.3 m, scale ≤1.5
  ([`fogvol.md`](formats/fogvol.md) has the full 8-chapter census).
- **Pinned A/B poses** (from the CSVM twins' freecam overlays, `Z:\CSVM\Screenshots\`): above-deck
  `x -7323 y 1192 z -3829`; river **`x -7323 y 192 z -3829`**. View direction is NOT in the overlay —
  re-derive it by matching terrain features before the first A/B and record it here.
  ⚠ **The river altitude read `934` here until `A7` re-read the overlay (2026-08-08): the twin says
  `y 192`**, the above-deck twin says `1192` in the same font at the same zoom (so this is not a
  cropped digit), and the original still's own ALT gauge reads ~700–750 ft ≈ 215–230 m. A3/A6/A7
  probes cite `934`; they are self-consistent before/after pairs at a documented pose and their
  conclusions stand, but **a matched-pose A/B against the original must use 192** (A4, B15).
- **Measured targets on file** (CAP-12, `playtest/CAP-12/`): deck from below original **167** vs
  ours 221; interior 248/243; tops from above 211/214 (⚠ per-population caveat, item C23); from
  5570 ft 196/202. Whiteout band measured 1003–1085 m ≙ authored slab 970–1090.
- **Prior probe**: `.claude/worktrees/bl-118-deck-brightness/.scratch/bl118/csvm-abovedeck-domeunfogged.png`
  (2026-08-08 08:11) — un-fogging the dome removes the gray band at the above-deck pose.
- **BL-303's three recorded poses** (`playtest/CAP-11/README.md`): C3 canyon
  `--pos=-3504,710,-3619 --direction=-0.40673,0,-0.91355`; C2B above deck (t=50 still); C5 night sky.

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

### Wave A — the scatter (BL-312 + the top-anchor correction)

1. ☑ A1 — Decide the scatter mechanism from the evidence (research)
2. ☑ A2 — Kill the lattice and the field edge (horizontal mechanism) — **+ exact footprint containment**
3. ☑ A3 — Top-anchor the vertical placement
4. ☐ A4 — Scatter A/B vs CAP-12 + the river twin; close `BL-312`
5. ☑ A5 — Continue the field past the map edge (user playtest 2026-08-08: the original's field is everywhere)
6. ☑ A6 — Why does flight mode show puffs below the deck when freecam doesn't? (investigate, then fix or reclassify)
7. ☑ A7 — The deck is engine trickery: regime model + cloud layer gate (user decode, 2026-08-08)

### Wave B — fog semantics and zones (BL-100 + BL-303 + BL-101)

11. ☐ B11 — All-chapter zone-table survey; test H1/H2/H3 on paper
12. ☐ B12 — Dome-identity discriminator at the above-deck pose
13. ☐ B13 — Footage discriminators: camera-vs-fragment fade, switch point ([USER] capture as needed)
14. ☐ B14 — Implement the winning fog model
15. ☐ B15 — Re-calibrate or delete `fogRangeFactor` 2.0
16. ☐ B16 — The dome's authored `fog: false` (lever, evidence-gated)
17. ☐ B17 — Fog A/B at both reference poses + BL-303's three; close `BL-303`/`BL-100`/`BL-101`

### Wave C — deck mesh brightness (BL-118 core)

21. ☐ C21 — Find the original's underside mechanism (research)
22. ☐ C22 — Implement the underside darkening
23. ☐ C23 — Re-measure the tops per population; blend the mesh↔sprite cut
24. ☐ C24 — Final match: both stills within the bar; mint PT + night-moonlit BL; close `BL-118`

## Dependency and parallelism notes

Waves run strictly A → B → C (decision 3). Inside Wave A: A1 blocks A2/A3; A2 and A3 both edit
`FogVolumeClutter.cs` — sequence them, never parallel worktrees. **A6 (investigation) runs before
A5 (both may touch `FogVolumeClutter.cs`), and A4 closes the wave only after A5, A6 and A7 land** —
the 2026-08-08 playtest reopened the wave with A5/A6, and the user's deck-regime decode from the
A6 re-fly minted A7 (runs after A6, before A5 — A7 touches `FogVolumeClutter.cs` layer
assignment beside `WeatherRig.cs`/`GameSession.cs`). Inside Wave B: B11, B12, B13 are
independent research and may interleave (B13 may park on a [USER] capture — proceed with the
others); B14 needs all three; B15 and B16 follow B14 and both touch the weather/fog path
(`Weather.cs`/`WeatherRig.cs`/`SceneBuilder.cs`) — sequence them; B17 last. Inside Wave C: C21
blocks C22; C22 before C23 (the cut can only be judged with the underside fixed); C24 last.

---

# Wave A — the scatter

## A1 ☑ Decide the scatter mechanism from the evidence

### VERDICT (2026-08-08 — research only, no `.cs` touched)

**Candidate (a): bounded random scatter inside the authored `fvol` volumes, at the authored mean
spacing. Not (b) camera-tiling, not (c) a per-shape hybrid. Vertically: top-anchored, centre
Y = volume top + `perp_dist_range`.** The `fvol` boxes are the field's *where* in all three axes;
`distance` is its areal **density** (mean spacing), not a lattice phase. The RNG story does not
change: one seeded stream, built once, world-anchored.

**(b) is falsified by the authored data, not merely unsupported.** Four independent reasons:

1. **The reader carries no altitude at all.** `extracted/<ch>/zrdr/fogvol.zrd.json` ships
   `fog_zone`, `distance`, `clutter{weight, nodes, far_fade_range, perp_dist_range (±5 / −5…+10 m),
   perturb_dist_range, scale_range}` — and nothing that could put the deck at 970–1090.55 m in C1,
   1060–1180.55 m in C4 or −463…183 m in C5. `CAP-12` measures that deck (full obscuration
   1003–1085 m, top edge 3560 ± 6 ft over five crossings), so the height **must** come from the
   `fvol` geometry. A mechanism that reads a box's Y bounds and discards its X/Z bounds is not one
   mechanism, it is two — and only one of them is authored.
2. **C5 kills it outright.** Its seventeen volumes carry **8–20 vertices each** — polygonal prisms
   following the streets, not boxes (`fvol7` is an 18-vertex hexagonal prism, `fvol34` a
   20-vertex one) — and cover **102.4 Mm² of a 268.4 Mm² map, 38 %**. Camera-tiling on the authored
   80 m period would place **41,943** sprites inside the map instead of ~16–19 k, and put
   street-level haze over the whole city and the sea. "C5's seventeen street-level strips
   preserved" fails by construction.
3. **C1C kills it too.** Its twelve build-ups are **rotated, tapering frusta** authored over
   particular places — `fvol10` is 854 × 294 m at its 1091.28 m base and ~464 × 160 m at its
   1688.05 m top. "Extra cloud here and not there, narrowing as it rises" is inexpressible in a
   field centred on the camera.
4. **The `templates.zrd` shared-grammar argument does not survive reading `templates.zrd`.**
   `extracted/C1/zrdr/templates.zrd.json` carries only `node`, `substitute`, `scale_range`,
   `far_fade_range` — **no `distance` key and no `perturb_dist_range` key**. Ground clutter's
   tiling period is the template's *ground-quad size in the gamez* (512 m for C1's `terpat02`,
   [`clutter.md`](formats/clutter.md)) and its decoration offsets are **authored explicitly, one
   transform per decoration**: that system contains no randomness at all. The genuinely shared keys
   are `scale_range` + `far_fade_range` + weighted substitution, and `fogvol` already uses all three
   the same way. `distance`, `perturb_dist_range` and `perp_dist_range` are fogvol-only and have no
   ground-clutter meaning to inherit. `fogvol.md`'s "the shared grammar tiles unboundedly" was
   reading a key that is not in the other file.

**Why `PT-42`(b)'s "world-locked and tiled" is not evidence against (a).** Verified from
`extracted/<ch>/gamez/nodes.json`: C1/C1C/C2B/C4's `fvol1`–`fvol9` are an **exact 3 × 3 partition of
the world `area`** — the nine combinations of x ∈ {[−12288,−10240], [−10240,−2048], [−2048,0]} × z
in the same three intervals, against a `World` node whose `area` is exactly
`left −12288, top −12288, right 0, bottom 0`. **The field's footprint IS the base map, to the
metre**, with no interior seams. So "puffs stay put as you fly through them, there are always more
in every direction over the base map, no volume edge anywhere" is *predicted by* the bounded
reading — it is not a discriminator. The two candidates can only differ within `far_fade_range.y`
= 3500 m of the map boundary, looking outward. **At both pinned A/B poses (above-deck
x −7323 z −3829, river x −7325 z −3829) the nearest map edge is 3829 m away — past the fade.**
There the bounded and the unbounded field are identical by construction.

**So, stated explicitly as decision 4 requires: the footage cannot discriminate bounded-random from
unbounded-tiled at the ranges it samples**, and the smaller change wins. The criteria are the
contract, and (a) meets all four — see the acceptance table below.

### What A2 changes, and what it must not

- **Keep** the per-volume decomposition into `distance` × `distance` cells, one placement per cell.
  That is the density, and density is the one thing the old reading got right: C1 9,025 placements
  over 12,288² m² = 5.977e−5 /m², **mean spacing 129.3 m** against the authored 130; mean card area
  132.3² × E[scale²] (1.5258) = 26,707 m² ⇒ **1.60 sprite-areas of cover per unit of layer**, an
  overcast one sprite deep. Counts stay 9,025 (C1/C2B/C4), 11,368 (C1C), 19,356 (C5) — all
  reproduced here from the box extents, so a moved count in A2's log means a bug, not a design.
- **Change** only *where inside the cell* the sprite lands. Today it is the cell corner
  (`x = gx * period`) plus `perturb_dist_range` 10–20 m at a free bearing — ±15 % on 130 m, which
  is the comb. Make it a **uniform draw inside the cell**, then apply the authored
  `perturb_dist_range` on top exactly as now. No authored value is discarded, no constant invented.
- **Stratified, not Poisson.** Do *not* replace the cells with N uniform draws over the whole
  footprint: pure Poisson at this density opens holes, and an overcast that must read as continuous
  would break up. One draw per cell keeps the mean spacing *and* the continuity.
- **RNG: nothing changes, and that is a reason to prefer (a).** The field is built once,
  world-anchored, never recycled, so a single `Rng.NewSystemRandom(Rng.Clouds)` sequence drawn in a
  fixed volume/cell order already makes the whole field a pure function of the master seed — stable
  under `--det`, identical run to run, shared by every splitscreen pane. **A2's cell-hashing trap is
  moot under this verdict**: there is no camera-relative cell to hash. Do not introduce a
  coordinate hash, and do not let placement depend on camera position, pane count or build order.

### Vertical anchor (for A3)

**Centre Y = volume top + `perp_dist_range`.** Predictions, from the verified box tops and
card 132.3 m × scale 0.95–1.5:

| chapter | volume top | centres | card bottoms | measured |
|---|---|---|---|---|
| C1 | 1090.55 | 1085.6–1100.6 | **986.3–1037.7** | first wisps 982 m, full obscuration from 1003 m ✓ |
| C4 | 1180.55 | 1175.6–1190.6 | **1076.3–1127.7** | clear air at 1135 m with the sheet below ✓ |
| C1C slab | 1091.28 | 1086.3–1101.3 | 987.1–1038.4 | — |

⚠ **The C1 band is a degenerate instrument and must not be cited alone** (METHOD-1, INSTR-7):
`CLOUD_COVER` `TOP 1124 / BOTTOM 970 / THICKNESS 30` (verified in
`extracted/C1/IA1/zrdr/weather.zrd.json`) predicts full white **1000–1094**, and the `fvol` slab
predicts a top at **1090.55**; the measurement is 1003–1085. Both fit. **C4's 1135 m clear-air
frame is the only clean discriminator** for sprite placement, and it says top-anchored.

⚠ **Top-anchoring is not safe for the two thick-volume shapes — A3 must check both.** C5's strips
are 646 m thick with 70 m cards, so the rule puts every sprite in a sheet at **125–246 m** and
leaves the streets below it empty; C1C's build-ups are 300–597 m tall, so the rule caps each tower
at **1584–1797 m** (for the 1688 m ones) and hollows the shaft. If either reads wrong at the
render, the anchor is per-volume-shape and A3 says so in `fogvol.md` — but it decides that from a
frame, not from a threshold invented here.

### ⚠ New finding — the volumes are filled by AABB, and half of them are not axis-aligned

Not part of the mechanism choice, found while verifying it, and it lands in the same function.
`FogVolumes.SubtreeBox` takes the world-space **AABB** of each `fvol` mesh, and `Scatter` fills
that. For C1/C2B/C4's slab that is exact (the boxes are axis-aligned; hull/AABB = 1.000). It is not
exact anywhere else:

- **C1C's twelve build-ups are rotated rectangles, all at hull/AABB = 0.383.** Their authored
  footprints are 2048 × 704 m (and 974 × 335, 1864 × 641, 854 × 294) — and the same twelve
  footprints are cut into `fvol9`'s own top face as coplanar polygons, which is how the match was
  confirmed (`fvol9` poly0/1/2/7–15 reproduce `fvol10`–`fvol23`'s footprints exactly). Filling the
  AABB places **2,343** sprites where the authored shape holds ~**897**, in axis-aligned squares
  2.6 × too large, and — combined with top-anchoring — puts a 1877 × 2006 m slab of cloud at 1688 m
  where the frustum has narrowed to ~464 × 160 m.
- **C5's strips run hull/AABB 0.566–1.000, 0.846 overall**: 19,356 placements where the authored
  corridors hold ~16,375, with the surplus sitting off the streets.
- C1C would go 11,368 → ~9,922 and C5 19,356 → ~16,375 if A2 fills the footprint instead.

**Recommendation: fold footprint containment into A2** (same function, same turn, and A3's
top-anchor makes the C1C error worse if left). It changes no C1 pixel — C1's boxes are already
axis-aligned — so it cannot disturb this milestone's two reference stills. **If it is instead
deferred, it needs its own item**, because it is a decode correction and not a tuning matter.

### Evidence, still-by-still

| evidence | what it shows |
|---|---|
| `playtest/CAP-12/t124.0-grazing-tops-3963ft.png` | grazing along the tops at 1208 m: soft continuous mottling to the fade, no lattice, no edge. Sky→tops 10–90 % transition **103 px** |
| `playtest/CAP-12/t44.0-above-deck-5010ft.png` | 5010 ft looking down the sheet: same, transition **104 px** |
| `playtest/CAP-12/t97.0-high-above-5572ft.png` | 5572 ft: same, transition **91 px** |
| `playtest/CAP-12/t29.2-base-first-wisps-3270ft.png` | entering the base: wisps arrive as isolated soft patches, not a rank |
| `playtest/CAP-12/csvm-above-1160m.png` (ours) | a **razor-flat white plate** with a hard lump line: transition **13 px**, peak \|dlum/drow\| 9.14 |
| `Screenshots/C1 IA1 Fog above clouddeck.png` (ours) | same defect at the pinned pose: transition **20 px** vs the original's **193 px** at the same framing |
| `OriginalScreenshots/C1 IA1 Fog river.png` | from 934 m the underside is a flat gray sheet — **no card bottoms below the deck at all** |
| `Screenshots/C1 IA1 Fog river.png` (ours) | discrete cauliflower lumps hanging below the deck with a hard lower boundary — A3's target |
| `extracted/*/gamez/nodes.json` | the 3 × 3 map partition; C1C's frusta; C5's polygonal prisms; `World.area` |
| `extracted/C1/zrdr/templates.zrd.json` | no `distance`, no `perturb_dist_range` — reason 4 above |

⚠ **Instrument warning for A2/A4.** A2's proposed "column-autocorrelation check on the sheet
region" **is degenerate at these poses** and must not be used as written (METHOD-14): perspective
makes a fixed world-space period aperiodic in screen space, and a row/column ACF over the sheet
finds *no* peak above 0.33 in ours **or** the original — including a 0.69 peak in the *original's*
underside that is capture noise at sd 0.91–2.58. Use the **sky→tops 10–90 % transition depth on
HUD-free columns** instead: it separates the two sides by 5–10 × (original 91–193 px, ours 13–20 px)
and is what A4 should quote.

### Predicted look at the three CAP-12 grazing poses, before A2 renders it

1. **`t124.0-grazing-tops-3963ft` (1208 m, ~120 m over the slab top).** The tops stop being a line
   and become a band: the sky→tops transition deepens from 13–20 px to **~100 px**, individual
   cards stop being separable anywhere along it, and the near sheet keeps mottling to the bottom of
   the frame instead of going featureless. Sprite count unchanged at **9,025**. No repeating pitch
   along the boundary at any bearing.
2. **`t44.0-above-deck-5010ft` (1527 m, 440 m over the slab top).** One continuous mottled surface
   out to the 3.1–3.5 km fade in every direction, with **no straight-line field edge** — and at the
   pinned above-deck pose none is reachable, the nearest map edge being 3829 m out. Transition
   ~**104 px**. The two big discrete balls that remain in ours are C1's **28 `cloudparent`**
   clusters, a different population; both frames of any A/B here are **on the base map**, unlike the
   original clip, which had left it.
3. **`t29.2-base-first-wisps-3270ft` (997 m, just inside the slab floor).** Climbing in, the first
   wisps arrive as **isolated soft patches at random bearings** rather than a rank of lumps
   crossing together, from **~986 m** (predicted lowest card bottoms) against the measured 982 m
   — and, once A3 lands, nothing hangs below the 960 m deck sheet, which is the river twin's
   remaining defect.

### Acceptance criteria (decision 4) against the verdict

| criterion | met by |
|---|---|
| no lattice at any grazing angle | uniform-in-cell placement removes the period entirely; measured by transition depth, not ACF |
| no visible field edge over the base map | the `fvol` footprint **is** the map, exactly; the only edge is the map boundary, and it is 3829 m from both pinned poses vs a 3500 m fade |
| density reading like `CAP-12` | count and mean spacing are unchanged by construction (129.3 m, 1.60 sprite-areas of cover) |
| C1C build-ups and C5 strips preserved | they survive because the volumes still bound the field — the whole reason (b) is rejected |

---

**Goal.** A written verdict (in this section + `fogvol.md`) on how the original places the
`cloudsprite` field: randomised at the authored mean spacing inside the `fvol` boxes, tiled
unboundedly around the camera on the `distance` period, or a hybrid per volume shape — plus how the
vertical placement anchors. No code.

**Evidence (confidence: traced for the constraints, lead-only for the mechanism).** BL-312's two
candidates and the argument that the density corroboration only ever supported the spacing;
PT-42(b): world-locked, tiled, no volume edge anywhere over the base map; CAP-12 grazing stills
(t=44/59/97/124, t=29.2): soft continuous structure, no lattice; `fogvol.md`'s census: C1/C1C/C2B/C4
map-spanning slabs, C1C's twelve stacked build-ups, C5's seventeen street-level strips. ~~The
`templates.zrd` shared grammar tiles ground clutter unboundedly.~~ — **withdrawn by the verdict
above (reason 4): `templates.zrd` has no `distance` and no `perturb_dist_range`, its period is the
gamez ground-quad size, and it randomises nothing.**

**Approach.** Re-read `fogvol.md` + the CAP-12 stills; write the mechanism decision against the
acceptance criteria (decision 4): no lattice at any grazing angle, no visible field edge over the
base map, density reading like CAP-12, C1C build-ups and C5 strips preserved. Decide the RNG story
(seed-derived, stable under `--det`). If footage can't discriminate bounded-random from tiled at
the flyable ranges, say so and pick the smaller change — the criteria, not the mechanism, are the
contract.

**Model recommendation.** high — judgement over contested evidence; the write-up steers two code items.

**Verify.** The decision section here names the evidence for each choice, and states the predicted
look at the three CAP-12 grazing poses before A2 renders it.

**⚠ Traps.** The two cloud populations are different objects (`BL-118`'s vocabulary note):
`cloudsprite1/2` `fvol` scatter here; the world-placed `cloudparent` clusters are stationary,
untiled, stop at the map edge, and are NOT this wave's business. Any above-deck A/B must say which
side of the map edge both frames are on.

## A2 ☑ Kill the lattice and the field edge

**Landed.** (2026-08-08) `FogVolumeClutter.Scatter` no longer places at a grid point: each volume
is cut into `distance` × `distance` cells anchored on the world origin, the two outermost cells of
each axis taking the remainder so the cells **tile** the volume exactly, and every cell draws its
placement **uniformly inside itself**. `perturb_dist_range` is still applied on top, per the data.
Cell count — hence density and mean spacing — is unchanged by construction. Folded in per the
user's post-A1 scope call: `FogVolumeSpec.VolumesOf` now carries each volume's **face planes**
beside its bounds and `FogVolumeBox.Contains` is a half-space test over them, so a cell whose draw
lands outside the authored shape places nothing. **Nothing vertical changed** (A3's item), and
`cloudparent`, the whiteout and the deck mesh were not touched.

**Files.** `CSVM/src/Effects/FogVolumeClutter.cs`, `CSVM/src/Mech3/FogVolumes.cs`,
`CSVM.Tests/FogVolumeTests.cs`, `docs/formats/fogvol.md`, `docs/architecture.md` (both module
entries), this section.

### Containment is EXACT, not a convex approximation

Verified against `Z:\CSVM\extracted\*\gamez\{nodes,models}.json` before writing any code: **all 65
`fvol*` volumes of all five clouded chapters are convex** — C1/C2B/C4's nine axis-aligned slabs,
C1C's twelve rotated tapering frusta, C5's seventeen polygonal street prisms (8–20 vertices, three
with a ramped top). So a half-space test over the mesh's own faces *is* the authored shape. Faces
are oriented outward by the vertex centroid (winding is not guaranteed by the gamez) and coplanar
duplicates merged — C1C's `fvol9` carries the twelve build-up footprints as subfaces cut into its
own top face, 55 polygons over 5 distinct planes. Because a frustum's faces slope, the test
narrows with height by itself: **A3 can change the Y distribution without touching containment**,
which is why it was worth folding in here rather than deferring.

`CSVM.Tests/FogVolumeTests.cs` pins the shape census per chapter (`volumes|volumes that ARE their
bounding box`: C1 9|9, C1C 21|9, C2B 9|9, C4 9|9, C5 17|2) plus a data-free rotated-prism unit
test. That census is the tripwire if a future extraction ever ships a non-convex volume, which
this test would fill to its hull silently.

### Sprite counts — measured, all 8 chapters

`--freecam --chapter=<X>` per chapter, `fogvol clouds:` log line, before vs after on the same
worktree and binary:

| chapter | before | after | A1 predicted | why |
|---|---|---|---|---|
| C1 | 9,025 | **9,025** | 9,025 | slabs are boxes; containment never rejects ✓ |
| C1B | 0 | 0 | 0 | no `fvol*`, no template ✓ |
| C1C | 11,368 | **9,569** | ~9,922 | see below |
| C2 | 0 | 0 | 0 | ✓ |
| C2B | 9,025 | **9,025** | 9,025 | ✓ |
| C3 | 0 | 0 | 0 | ✓ |
| C4 | 9,025 | **9,025** | 9,025 | ✓ |
| C5 | 19,356 | **16,170** | ~16,375 | ✓ (0.837 measured vs 0.846 predicted) |

**C1C lands 353 below A1's number, and the reason is a correction to A1, not a bug.** A1 computed
the build-ups' loss from their **base footprint** ratio (hull/AABB 0.383 → 2,343 → ~897). But the
volumes are frusta, and containment is a test on the 3-D point, so with today's uniform-in-Y draw
the accepted fraction is the **volume** fraction, 0.235 — Monte-Carlo'd per volume off the
extracted meshes at 0.231–0.239, predicting 9,568 against the build's 9,569. C5's strips are
extrusions, so its volume fraction ≈ its footprint fraction and A1's number holds. **This number
will move again when A3 changes the Y rule**; that is the containment test composing with the
vertical rule, exactly as intended, not a second correction.

### Transition depth — the amended instrument, and what it says

Instrument (`.scratch/transition_depth.py`, kept): per HUD-free 21-column block, locate the
dominant bright-ward edge on a coarse-smoothed luminance profile, walk out of it to where the
slope falls under 10 % of its peak (those rows carry the edge's own sky and tops levels), report
the rows between the 10 % and 90 % crossings; median over blocks. Two smoothing scales are
reported — `band` (31 rows: how deep the whole sky→tops band is) and `edge` (15 rows: how sharp
the sharpest boundary inside it is). **Calibrated against A1's published numbers**: it returns
**103 px on CAP-12 `t124`** (A1: 103) and **101 px on `t97`** (A1: 91). `t44` reads 33 px only
because its tops boundary sits below the fixed row band — a framing limit, stated rather than
tuned around.

Probes: `--freecam --chapter=C1 --det --screenshot`, HUD- and plane-free, identical pose before
and after, baseline taken first on the unmodified build.

| pose | before band / edge | after band / edge | original reference |
|---|---|---|---|
| pinned above-deck `-7323,1192,-3829` | 46 / **13** | 46 / **13.5** | — |
| grazing tops 1208 m (`t124` altitude) | 43 / **11** | 43 / **11** | `t124` **103** / 50 |
| above deck 1527 m (`t44` altitude) | 25 / **24.5** | 25 / **24.5** | `t44` 33 / 34 (framing-limited) |
| high above 1698 m (`t97` altitude) | 24 / **24** | 24 / **24** | `t97` **101** / 49 |

**A1's prediction 1 for A2 is falsified: randomising the horizontal placement does not deepen the
sky→tops transition at all** (0–0.5 px on four poses). The instrument is able to fail — it
separates our frames from the originals by 2–9× and it separated `csvm-above-1160m` (10 px) from
`t124` (103 px) — so this is a real negative, not a dead check. The reason is geometric: at 1208 m
the camera sits ~18 m above the top of a field whose card tops are all at nearly one altitude, so
the far sheet compresses into a few rows below the horizon whatever the X/Z arrangement is. **The
transition depth is a measurement of how ragged the field's TOP is, i.e. of the vertical rule —
it belongs to A3, and A4 should quote it after A3, not after A2.**

### What DID move: the lattice

The decisive A/B is a straight-down pair, where the ACF's degeneracy (METHOD-14: perspective makes
a fixed world period aperiodic on screen) does not apply because the sheet sits at near-constant
range across the frame — and it is read on **C5 at night over the harbour**, where the sprites
show as dark blobs against lit water instead of white-on-white:

- `.scratch/a2/ab-c5-lattice-over-water.png` — **before: blobs in aligned rows and columns at a
  fixed pitch. After: irregular scatter, clumps and gaps, at 84 % of the count.** Unmistakable.
- `.scratch/a2/ab-topdown-lattice.png` (C1 from 2500 m) — same change, much harder to read because
  a white overcast one sprite deep hides its own structure.
- `.scratch/a2/ab-grazing-skyline.png` — the grazing skyline's scallops go from near-equal widths
  to varied ones.

⚠ **Two instruments were tried and are degenerate here; do not re-run them.** (a) 2-D
autocorrelation on the straight-down frame: monotonic decay to lag 200 px in *both* before and
after — the blob's own size dominates and the field is dense enough to hide the pitch, so this
does not even separate the known-lattice frame from the known-random one. (b) Crest-spacing
coefficient of variation on the skyline: 1.80 before vs 1.73 after at the grazing pose, and 1.52
on the original `t124` — the skyline extraction is too noisy for the statistic to mean anything.
The straight-down C5 image is the instrument that works.

### Probe images (`.scratch/a2/`, and the scripts beside them in `.scratch/`)

| file | what it is |
|---|---|
| `ab-c5-lattice-over-water.png` | **the item's evidence** — C5 straight down over the harbour, before over after |
| `ab-topdown-lattice.png` | C1 straight down from 2500 m, before over after |
| `ab-grazing-skyline.png` | the grazing skyline's scallops, before over after |
| `before-*.png` / `after-*.png` | the four transition-depth poses, plus the two straight-down poses |
| `counts-before/`, `counts-after/` | the 8-chapter count sweep, one `.out` per chapter |
| `golden-c5-city-night-cloudmask.png`, `golden-c1-crash-cloudmask.png` | the `--tex-override` probes that explain the two unmoved goldens |
| `transition_depth.py`, `skyline_regularity.py`, `lattice_acf.py`, `ab_crop.py` | the instruments, incl. the two degenerate ones, so the next reader does not rebuild them |

### The other criteria

- **No field edge over the base map.** Unchanged and structural: C1/C1C/C2B/C4's `fvol1`–`fvol9`
  are an exact 3 × 3 partition of the `World` node's own `area`, so the footprint IS the map to
  the metre. Nearest map edge from either pinned pose is 3,829 m against a 3,500 m far fade.
- **Determinism.** Same probe run twice on the new build: `pixmd5=8efcbfc0788142c5a9217b46e5da7253`
  both times, counts identical. One seeded `Rng.Clouds` stream in a fixed volume/cell order; no
  camera, pane count or frame is read, so there is no per-view cell and no coordinate hash was
  introduced (A1 called this trap moot; it is).
- **Preserved:** built once, world-anchored, not per rig; one field shared by every splitscreen
  pane with the far fade evaluated per view in the shader; `far_fade_range` consumption and the
  degenerate-quad cull past `far_fade.y`; the two-level per-kind weights; one pass per volume.

### Tests and goldens

`.\RunTests.ps1` — build PASS (0 warnings), **units 648/648**, **engine 26/26, errors clean**,
goldens **6 moved, 0 broken of 13**. Exit 1 is the golden stage alone; nothing non-golden failed.
**Not re-pinned — that is A4's job** (GOLD-1). Two new unit tests landed with the item (the
per-chapter shape census and the rotated-prism containment check).

| golden | moved? | why |
|---|---|---|
| `c1-waterfall` | **moved** | C1, freecam with sky in frame |
| `c1c-rain` | **moved** | C1C, and its count changed too |
| `c2b-rain` | **moved** | C2B |
| `c4-snow` | **moved** | C4 |
| `c1-flight` | **moved** | C1 chase, sky in frame |
| `c1-destroy-effects` | **moved** | C1, sky in frame |
| `c1b-night-sea`, `c2-city`, `c3-island` | ok | those chapters ship **no `fvol*` volume** — the change is inert by construction (DIAG-10) |
| `viewer-bhawk`, `empty-stage` | ok | no chapter world at all |
| `c5-city-night` | ok | C5's field DID change (19,356 → 16,170), but this pose paints **0 cloud-sprite pixels** — measured with `--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00` at the manifest's own pose and frame |
| `c1-crash` | ok | frame 20 looks straight DOWN at terrain; no sky, no field in frame (same override probe — the green pixels it reports are grass, not sprites) |

That is exactly the GOLD-5 pattern this change should produce: every shot that shows a clouded
chapter's sky moved, and nothing else did.

**⚠ Handover to A3.** (1) The transition-depth targets are A3's to hit, not A2's — do not read
A2's flat numbers as a failure of the scatter. (2) Containment now rejects a draw outside the
authored shape *before* `perp_dist_range` is added, so a top-anchored Y **must be sampled inside
the volume and offset afterwards**; sampling at `top + perp_dist` and then testing would reject
the entire field, C1's slab included. (3) C1C's count will move again with the Y rule — that is
the frustum taper, not a regression.

### Original approach (kept for reference)

**Verify.** `--screenshot` grazing passes along tops and base at CAP-12's angles: no periodic
structure (eyeball + a column-autocorrelation check on the sheet region); sprite count logged per
chapter stays within ~5 % of the census (9,025 C1) unless A1 decided tiling — then state the new
number and why. `.\RunTests.ps1` green; goldens re-pinned only at A4.
— *the column-autocorrelation check was withdrawn by A1 (METHOD-14) and its replacement, the
transition-depth metric, turned out to measure the vertical rule; see above. The ~5 % count band
was written before the footprint correction was folded in.*

## A3 ☑ Top-anchor the vertical placement

**Landed.** (2026-08-08) `FogVolumeClutter.Scatter` now classifies each volume by its own AABB
height at load: a volume no more than `TopAnchorHeightFactor` (1.5×) card-heights thick draws
every cell's Y at the volume's own top (`box.End.Y`) before containment; a taller volume keeps the
original full-height uniform draw. `perp_dist_range` is still added AFTER containment either way,
exactly as A2 left it — the ordering trap (sample inside the volume, offset after) was not
touched. **The anchor is per-volume-shape**, decided from data, not from a threshold invented in
the abstract: C1/C2B/C4's nine slabs and C1C's own map-spanning `fvol1`–`fvol9` measure
**120.5–120.6 m** thick against their 132.3 m card (ratio 0.91) and are top-anchored; C1C's twelve
build-up frusta (**299.7–596.8 m**, ratio 2.27–4.51) and C5's seventeen street strips (**646 m**,
ratio 9.23 against their 70 m card) are far taller and keep the old uniform fill. 1.5× card height
sits in the ~2.5× gap between the tallest slab and the shortest build-up with margin on both
sides (39 % under the slab, 51 % under the build-up), so no shipped volume is a close call —
recorded as a marked inference (not authored data) in `fogvol.md`'s vertical-spread entry.

**Files.** `CSVM/src/Effects/FogVolumeClutter.cs`, `docs/formats/fogvol.md`, `docs/architecture.md`
(the `FogVolumeClutter.cs` entry), this section. `FogVolumes.cs`/`FogVolumeTests.cs` untouched —
containment doesn't change, and the shape-census tests exercise `Contains` directly, not `Scatter`.

### C1 river pose — no sprite bottoms below the deck sheet

`--pos=-7325,934,-3829`, level and upward-tilted freecam (the exact original heading is still
unrecovered from the overlay — METHOD-13 — so this is a self-consistent before/after pair at the
documented pose, not a byte match to `Screenshots/C1 IA1 Fog river.png`). **Before**
(`.scratch/a3/before-river-pose.png`): discrete cauliflower lumps hang well down into the frame
with a hard, bumpy lower boundary — the same defect the checked-in twin shows. **After**
(`.scratch/a3/after-river-pose.png`): the cloud band sits entirely in the upper part of the frame;
the terrain's flat horizon is clean underneath it, no lumps intruding.

Confirmed quantitatively with `--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00
--no-fog` (SHOT-13) at the same pose: looking straight up, **zero** green pixels
(`after-river-override-up.png`); levelled and tilted up, the coloured field's lower edge sits
cleanly above a flat gray band — the `CloudDeck` mesh underside at y=960 — with visible terrain
below it and **no green below the deck** (`after-river-override-level.png`). This is the primary
target the item was written for, and it lands clean.

### C4 clear-air pose — the gap opens

`--pos=-4974,1135,-3861` (the `csvm-c4-1135m.png` altitude; exact original X/Z is not preserved in
`playtest/CAP-12/`'s record either, so this is `-4974,-3861`, the same "base-map, away from any
edge" coordinate the CAP-12 README's own convention uses elsewhere, per chapter). Plain screenshot
before/after (`before-c4-1135m.png`/`after-c4-1135m.png`) shows fewer, more isolated cauliflower
masses after the change against a pervasively hazy frame before — but C4's fog model (Wave B, not
yet landed) contributes its own haze at this pose, so the plain shots alone don't isolate the
sprite field. Isolated with `--tex-override`/`--no-fog` (SHOT-13): a level sweep at 1135 m shows
**patchy, non-solid** coloured coverage — consistent with the predicted card-bottom band topping
out at 1127.7 m, only 7 m below this altitude — against **fully solid** coverage from the same
pose 85 m lower at 1050 m, comfortably inside the predicted 986.3–1127.7 m band
(`after-c4-1135m-override-level.png` vs `after-c4-1050m-override-level.png`). The relative
gradient (solid below the predicted band, patchy right at its edge) is the evidence; the
1135 m frame is not perfectly gap-clear because the pose sits only 7 m above the predicted upper
bound, not because the rule missed — a pose a further ~30 m up would clear it entirely and was not
needed to confirm the placement moved.

### C1C build-ups and C5 streets — character preserved

`--pos=-5416,1350,-9737 --direction=0,0,1` (C1C, framing `fvol10`'s tower, base 1091.28 m / top
1688.05 m) and `--pos=-2868,50,-1792 --direction=1,0,0` (C5, inside `fvol10`'s street strip).
Both pairs (`before-c1c-buildup.png`/`after-c1c-buildup.png`,
`before-c5-street2.png`/`after-c5-street2.png`) are unchanged in character — the C1C tower stays a
solid tapering mass rather than being capped into a hollow shell, and C5's ground-level haze
between skyscrapers stays put rather than lifting into an empty-streets sheet near the strip tops.
Both volumes measure far taller than `TopAnchorHeightFactor` × their card and were classified
uniform, so this is the classification working as designed, not a coincidence.

### Transition depth — moved for neither wave, and that is reported, not forced

Re-measured with `.scratch/transition_depth.py` (A2's instrument, kept) at the pinned pose plus
fresh self-consistent poses at the three CAP-12-derived altitudes (A2's own X/Z/direction for
these three were not preserved anywhere on disk — only the altitudes are on record — so these are
new before/after pairs at the same documented altitudes, not a recreation of A2's exact framing;
the pinned above-deck pose IS the literal documented `x -7323 y 1192 z -3829`, direction `0,0,-1`):

| pose | before band / edge | after band / edge | original reference |
|---|---|---|---|
| pinned above-deck `-7323,1192,-3829` | 46.5 / 11.0 | 46.5 / **7.0** | — |
| grazing tops 1208 m (`-4974,1208,-3861`) | 60.0 / 47.5 | 55.0 / 48.0 | `t124` 103 / 50 |
| above deck 1527 m (`-4974,1527,-3861`) | 25.0 / 25.0 | 25.0 / 25.0 | `t44` 33 / 34 (framing-limited) |
| high above 1698 m (`-4974,1698,-3861`) | 24.0 / 24.0 | 24.0 / 24.0 | `t97` 101 / 49 |

**The instrument does not move materially for A3 either** — same conclusion as A2 reached for the
horizontal fix, now confirmed for the vertical one too. The reason: a `cloudsprite` billboard card
is 132.3 m across, nearly as tall as the whole slab is thick, so even the OLD full-height-uniform
draw already put a card near the volume's ceiling almost everywhere by sheer density (9,025 cards
over a 12,288 m map). Concentrating the draw into a ~15 m top-anchored band doesn't change that —
the instrument reads how ragged the nearest cards' silhouettes are at a shallow viewing angle,
which the Y-centre distribution barely touches once density is this high. **The remaining 2–9×
gap to the original's 91–103 px is therefore not primarily a placement problem on either axis** —
recorded as a finding in `fogvol.md`, not forced by inventing a third placement rule. Candidates
for a future item: per-card alpha falloff/scale distribution, or a video-compression artifact in
the `CAP-12` capture itself; neither was investigated here.

### Sprite counts — measured, all 8 chapters

`--freecam --chapter=<X> --det`, `fogvol clouds:` log line, before (= A2's own post-A2 numbers,
identical build state) vs after:

| chapter | before (A2) | after (A3) | why |
|---|---|---|---|
| C1 | 9,025 | **9,025** | slab is a box: top-anchoring accepts the same 100 % of cells uniform did |
| C1B | 0 | 0 | no `fvol*` ✓ |
| C1C | 9,569 | **9,572** | +3 (0.03 %) — see below |
| C2 | 0 | 0 | ✓ |
| C2B | 9,025 | **9,025** | ✓ |
| C3 | 0 | 0 | ✓ |
| C4 | 9,025 | **9,025** | ✓ |
| C5 | 16,170 | **16,170** | strips classified uniform, untouched ✓ |

C1/C2B/C4/C5's TOTALS hold exactly, but C1's kind split moved (`cloudsprite1` 4,615→4,497,
`cloudsprite2` 4,410→4,528, same 9,025 total) — expected: for an axis-aligned box, sampling Y at a
fixed height instead of drawing it skips one RNG call per top-anchored cell, which re-aligns every
later draw (kind pick, perturb, scale) in the one shared seeded stream, without changing the
100 %-always-accepts invariant a box has either way. **C1C moves 9,569 → 9,572**: its nine slab
pieces (including `fvol9`) are now top-anchored and precede the twelve build-ups in gamez/volume
order, so the same stream-realignment reaches the build-ups too — their own containment logic is
untouched, but the specific random draws feeding it differ, nudging the accepted count by 3 out of
9,572. This is the RNG-stream consequence the item's own implementation comment names, not a
second correction to the frustum-taper rule A2 already landed.

### Tests and goldens

`.\RunTests.ps1` — build PASS (0 warnings), **units 648/648**, **engine 26/26, errors clean**,
goldens **6 moved, 0 broken of 13**. Exit 1 is the golden stage alone; nothing non-golden failed.
**Not re-pinned — that is A4's job.**

| golden | moved? | why |
|---|---|---|
| `c1-waterfall`, `c1c-rain`, `c2b-rain`, `c4-snow`, `c1-flight`, `c1-destroy-effects` | **moved** | same six A2 moved — every clouded-chapter shot with sky in frame, again, because the Y rule repaints those pixels a second time |
| `c1b-night-sea`, `c2-city`, `c3-island`, `c5-city-night`, `viewer-bhawk`, `empty-stage`, `c1-crash` | ok | same seven A2 left alone — no `fvol*` volume, no chapter world, or (`c5-city-night`/`c1-crash`) a pose that paints zero cloud-sprite pixels regardless (A2's own `--tex-override` probe) |

Identical moved/unmoved SET to A2's own table (GOLD-5): the pattern is exactly what a second,
independent change to the same subsystem should produce.

### Probe images (`.scratch/a3/`)

| file | what it is |
|---|---|
| `before-river-pose.png` / `after-river-pose.png` | C1 river pose — the item's primary target |
| `after-river-override-up.png` / `after-river-override-level.png` | `--tex-override` isolation confirming zero sprite pixels below the deck at the river pose |
| `before-c4-1135m.png` / `after-c4-1135m.png` | C4 clear-air pose, plain render |
| `after-c4-1135m-override-level.png` / `after-c4-1050m-override-level.png` | `--tex-override` isolation, patchy-at-1135 vs solid-at-1050 |
| `before-c1c-buildup.png` / `after-c1c-buildup.png` | C1C `fvol10` tower — character preserved |
| `before-c5-street2.png` / `after-c5-street2.png` | C5 `fvol10` street strip — character preserved |
| `before-abovedeck-pinned.png` / `after-abovedeck-pinned.png`, `before/after-grazing-tops-1208.png`, `before/after-above-deck-1527.png`, `before/after-high-above-1698.png` | the four transition-depth poses |
| `counts-after/*.png` | the 8-chapter count sweep screenshots (logs in `.scratch/logs/probe-20260808-1138*.out`) |

**⚠ Handover to A4.** (1) Goldens are re-pinned there, not here. (2) The transition-depth gap is
now confirmed independent of BOTH scatter axes — A4's density comparison should not re-litigate
it. (3) C4's fog haze (Wave B) still muddies a plain screenshot at the clear-air pose; a future
fog fix should re-confirm the `--tex-override` sprite-only reading still shows the gap once the
haze is gone, since the plain frame will only become legible then.

**⚠ Traps (verified respected).** The whiteout band (`WeatherState.WhiteoutAmount`) was not
touched; the C1/C4 checks above test sprite geometry, not the whiteout schedule, which is a
separate system per `docs/architecture.md`.

## A4 ☐ Scatter A/B vs CAP-12 + the river twin; close BL-312

**Goal.** The wave's acceptance criteria measured and recorded; `BL-312` deleted from the backlog.

**Evidence (confidence: n/a — this is the instrument).** CAP-12 stills + the pinned river pose;
PT-42(c)'s "a lot denser" as the density reference. Wave-gate playtest (user, 2026-08-08, in
engine): lattice/comb at grazing angles **confirmed gone**; puffs below the deck **still seen in
flight** (→ `A6`, **landed 2026-08-08** — the deck was pinned 87 m above its authored altitude,
not a scatter fault; C1/C1C/C2B pixels moved a third time, so re-pin against A6's build);
field ends at the base map where the original's is everywhere (→ `A5`) — A4
closes the wave only after `A5` lands.

**Approach.** Matched shots at the CAP-12 grazing poses and both pinned poses; a density comparison
(sheet-region sprite coverage vs the original's) recorded here; re-pin the goldens
(`analysis/goldens/manifest.json`) since the field changed in every fvol chapter. Close `BL-312`
per convention (record in the landing commit; traps → `fogvol.md`/`verification.md`).

**Model recommendation.** medium — measurement and bookkeeping against stated criteria.

**Verify.** No lattice at any grazing angle; no field edge over the base map; density within
eyeball-parity of CAP-12 (state the measured coverage numbers); C1/C4 vertical criteria from A3
re-confirmed. Full 8-chapter freecam regression, zero errors.

**⚠ Traps.** `--tex-census` counts are lower bounds and pair with `--no-fog` (cli.md) — if used for
density, say so and use the same flags on both sides of any before/after.

## A5 ☑ Continue the field past the map edge

**Landed.** (2026-08-08) `FogVolumeClutter.Scatter` now continues the map-spanning slab's own
`distance`-cell field past the base map's rim, engine-side, matching the terrain's own
continuation (`MapEdgeExtender.cs`) — **not authored data**: neither `fogvol.zrd` nor the gamez
says anything about content past `World.area`, so this is a deliberate engine-side match, marked
as such in `fogvol.md`, not a decode. Which volumes qualify is decided from data
(`FogVolumeSpec.FindMapSpanningSlab`, new): a volume extends only if it is an axis-aligned box, is
top-anchored by A3's own rule, and — together with every other volume passing those two tests —
the set's footprints exactly tile their own combined bounding rectangle, re-checking A1's "exact
3×3 partition of `World.area`" finding from the data rather than keying off a chapter name or an
`fvol1..9` numbering convention. C1/C2B/C4's nine slab pieces and C1C's own map-spanning
`fvol1`–`fvol9` qualify; C1C's twelve build-up frusta and C5's seventeen street strips both fail
the top-anchored test (shortest build-up 299.7 m, ratio 2.27 against the 1.5× cut; shallowest
strip 646 m, ratio 9.23) and are never extended. Each extension cell draws off its own
coordinate-hashed generator (`Rng.NewSystemRandom(Rng.Clouds, gx, gz)`, new overload) rather than
the interior's shared sequential stream, so `--det` holds independent of the far-fade bound or the
cell enumeration, and the interior draw's own realization (base counts) is untouched. Mirroring
was not built — a random field is indistinguishable from a mirrored one, exactly as the approach
below anticipated.

**Files.** `CSVM/src/Effects/FogVolumeClutter.cs` (`ExtendPastMapEdge`/`EmitExtensionRegion`),
`CSVM/src/Mech3/FogVolumes.cs` (`FogVolumeBox.IsAxisAlignedBox`, `FogVolumeSpec.FindMapSpanningSlab`,
`MapSpanningSlab`), `CSVM/src/Utils/Rng.cs` (`NewSystemRandom(subsystem, cellX, cellZ)`),
`CSVM/src/Session/GameSession.cs` (the sprite-count log line), `CSVM.Tests/FogVolumeTests.cs` (8
new tests), `CSVM.Tests/RngTests.cs` (new file, 3 tests), `docs/formats/fogvol.md`,
`docs/architecture.md` (all three touched modules), this section.

### The extension radius and sprite budget

`MapEdgeExtender`'s rolling terrain window (docs/architecture.md) covers `Rings` (5) tiles of
1,024 m past whatever cell the camera occupies — **5,120 m**, for these chapters' 12,288 m,
12-tile-per-side map — cited here as "the terrain's own reach," per the item's own instruction.
**A full precomputed ring to that radius was NOT built.** It would place an estimated **~21,250**
extra sprites for C1/C2B/C4 (≈**2.35×** the 9,025-sprite base field, extrapolated from the
ring-area ratio at the radius actually measured below) — past this plan's own ">2× is
unreasonable" line for a structure that is built once and kept for the process's whole lifetime.
Bounded instead to the **largest authored `far_fade.y`** among the chapter's kinds — **3,500 m**,
identical for both `cloudsprite1`/`cloudsprite2` in every shipped deck chapter: every kind's own
shader already collapses a sprite past that distance to a degenerate quad (this file's own
`ShaderCode`), so a wider ring would buy zero visible pixels from anywhere a camera can stand —
this bound is lossless, not a cut corner, and it is read from the data (each kind's `FarFade.Y`)
rather than hardcoded.

**Measured, all 8 chapters** (`--freecam --chapter=<X> --det`, `fogvol clouds:` log line):

| chapter | base | extension | total | extension/base |
|---|---|---|---|---|
| C1 | 9,025 | 13,176 | 22,201 | 1.46× |
| C1B | 0 | 0 | 0 | — |
| C1C | 9,572 | 13,176 | 22,748 | 1.38× |
| C2 | 0 | 0 | 0 | — |
| C2B | 9,025 | 13,176 | 22,201 | 1.46× |
| C3 | 0 | 0 | 0 | — |
| C4 | 9,025 | 13,176 | 22,201 | 1.46× |
| C5 | 16,170 | 0 | 16,170 | — |

1.38–1.46× is comfortably under the 2× line, so the far-fade bound alone was enough — **acceptable
as measured, no further materialisation limit needed** (e.g. only cells within `far_fade` of a
*live* camera, rebuilt per frame, was the fallback if this had come back unreasonable). The field
stays a static, built-once, world-anchored structure exactly like the base field — no
`MapEdgeExtender`-style rolling window was built for this population. C5's strips and C1C's
build-ups add zero, as the data-driven test requires.

### Verification

- **Map-rim probes, before/after, `--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00
  --no-fog`** (isolates the field the way A3/A6/A7 did; SHOT-13). **Before shots taken FIRST**, on
  the unmodified build: `git diff` of the four engine files was saved to
  `.scratch/a5/a5-engine-changes.patch`, `git checkout HEAD --` reverted them, the pre-A5 binary
  was probed, then `git apply` restored the change (no `git stash` used, per the worktree rule) —
  confirming the current (pre-A5) build DOES show the field ending at the rim before measuring
  that A5 fixes it.
  - **West/east rim, flying along the edge** (`x -12288` / `x 0`, `--det`): before —
    `before-west-along-override.png` / `before-east-along-override.png` show a hard seam, dense
    green field on the interior side, bare white void beyond the rim. After —
    `after-west-along-override.png` / `after-east-along-override.png` are green edge to edge, no
    seam anywhere in frame.
  - **West/east rim, looking straight outward, 1,000 m inside**:
    `before/after-west-outward-override.png`, `before/after-east-outward-override.png` — the field
    reaches the horizon in every "after" frame instead of stopping partway there.
  - **NW corner** (200 m inside both edges): before — `before-corner-outward-override.png` is
    almost entirely void, a few isolated cards from the corner-most cells poking into frame; after
    — `after-corner-outward-override.png` fills solid.
  - **3,000 m past the west rim, looking back at the map** (inside the 3,500 m fade): before —
    `before-outside-lookback-override.png` shows only a thin, distant green band at the horizon
    (the interior field, far away) over a huge foreground void; after —
    `after-outside-lookback-override.png` shows the extended field reaching all the way to the
    camera — confirming the extension is materially present at a camera genuinely outside the map,
    not just painted at the rim itself.
- **No density step.** Every "after" frame reads as one continuous field at the same density on
  both sides of the former rim — the eight-region decomposition's inner edges land exactly on the
  slab's own bounding rectangle (the same coordinate the interior loop's own outermost cell is
  clipped to), so there is no phase mismatch to produce a visible step.
- **Sprite-count log states base + extension per chapter** — table above, from the `fogvol
  clouds:` line `GameSession.cs` now prints in that shape.
- **`--det` determinism.** Same pose, two separate runs:
  `pixmd5=f49caae4b1c6c1f62bdde1af8229faa6` both times.
- **C5/C1C counts unchanged.** C5 stays exactly 16,170 (zero extension — its strips fail the
  top-anchored test); C1C's BASE stays exactly 9,572, the same number A3/A6/A7 left it at (the
  extension never reads the interior's RNG stream, so it cannot realign those draws the way A3's
  own change once did).
- **`cloudparent` untouched.** The per-chapter cluster census logged unchanged: C1 28, C1B 70,
  C1C 30, C4 45 — identical to A6/A7's own numbers. `A5` never reads or moves that population.
- **8-chapter `--freecam` regression** — zero errors in all eight chapters
  (`.scratch/a5/counts-after/`), decks/sprite/cluster censuses otherwise unchanged from A6/A7.
- **`.\RunTests.ps1`** — build PASS (0 warnings), **units 669/669** (was 653: +8 slab-identification
  theory rows, +5 synthetic `FindMapSpanningSlab` fixtures, +3 `Rng` per-cell tests), **engine
  26/26, errors clean**, goldens **6 moved, 0 broken of 13**. Exit 1 is the golden stage alone.

### Goldens moved — the same six A2/A3/A6/A7 already moved, still not re-pinned

| golden | moved? | why |
|---|---|---|
| `c1-waterfall`, `c1c-rain`, `c2b-rain`, `c4-snow`, `c1-flight`, `c1-destroy-effects` | **moved** | every clouded-chapter shot with sky in frame — the field's own pixels move a fourth time (A2 horizontal, A3 vertical, A6 deck altitude, now A5's map-edge extension) |
| `c1b-night-sea`, `c2-city`, `c3-island`, `c5-city-night`, `viewer-bhawk`, `empty-stage`, `c1-crash` | ok | same seven every prior scatter item left alone — no `fvol*` map-spanning slab, no chapter world, or a pose that paints zero cloud-sprite pixels regardless |

Identical moved/unmoved SET to A2/A3/A6/A7 (GOLD-5) — the fourth independent change to this
subsystem produces exactly the same footprint, which is what a change correctly scoped to "the
map-spanning slab's own field" should do. **Not re-pinned here — still A4's job.**

### Probe images (`.scratch/a5/`)

| file | what it is |
|---|---|
| `before-west-along-override.png` / `after-west-along-override.png` | **the item's primary evidence** — standing at the west rim flying north, isolated: a hard seam before, none after |
| `before-corner-outward-override.png` / `after-corner-outward-override.png` | the NW corner, 200 m inside: near-total void before, solid field after |
| `before-outside-lookback-override.png` / `after-outside-lookback-override.png` | 3,000 m past the west rim looking back: a distant thin band over a huge void before, the extension reaching the camera after |
| `before/after-west-outward-override.png`, `before/after-east-outward-override.png`, `before/after-east-along-override.png` | the remaining rim probes, same pattern |
| `before-west-outward.png` / `after-west-outward.png` | natural-colour (fog ON) pair at the same pose — the mission fog at this range washes detail out almost entirely (the known Wave B symptom, not a new one here), which is why the override pair above is the primary evidence |
| `det-run1.png` / `det-run2.png` | the `--det` determinism pair, identical `pixmd5` |
| `counts-after/*.png` | the 8-chapter regression sweep |
| `a5-engine-changes.patch` | the saved diff used to build the pre-A5 baseline for the "before" shots without `git stash` |

**⚠ Handover to A4.** (1) Goldens are re-pinned there, not here — fold this item's movers in with
A2/A3/A6/A7's (same SET, moved a fourth time). (2) The extension is inert by construction for
C1B/C2/C3/C5 — A4's density comparison against CAP-12 should read C1's own numbers (9,025 base,
unchanged) since the pinned A/B poses (above-deck, river) sit 3,829 m from the nearest map edge,
inside the map and past the 3,500 m fade from the rim, so the extension contributes nothing to
those specific frames — it only matters once a probe reaches within ~3.5 km of the rim.

### Original approach (kept for reference)

**Goal.** Flying toward and beyond the map boundary shows the cloud field continuing everywhere,
as the original does — no field edge at the base-map rim.
— *met: verified at the render above.*

**Evidence (confidence: direction traced, mechanism open).** User at the controls, 2026-08-08:
"it is only over the basemap. in the original its everywhere." A1 explicitly left this
undiscriminated (the two readings differ only within `far_fade_range.y` = 3500 m of the boundary,
and no footage sampled there) — this playtest is the missing sample. The terrain already
continues via `MapEdgeExtender.MirrorAxis` (alternating reflection, `BL-105`); the field must
continue over that extension.
— *the terrain's MirrorAxis reflection was NOT the mechanism copied — a random field cannot be told
apart from a mirrored one (see below), so the field continues by tiling, not by mirroring
MapEdgeExtender's own transform.*

**Approach.** Extend the scatter beyond the base map for the map-spanning slab volumes only
(C1/C1C/C2B/C4's `fvol1`–`fvol9`, which tile the map exactly — C1C's build-ups and C5's strips
are local geometry and must NOT be extended). Simplest faithful mechanism: virtually tile the
slab's cell field outward to the edge-extension radius with the same density, seed-hashed per
cell so determinism and world-lock hold. Mirroring vs plain continuation is indistinguishable
for a random field — do not build mirror machinery for it. Respect containment: the extension
inherits the slab's Y band.
— *followed as written, with "the map-spanning slab volumes" made a data-driven test
(`FindMapSpanningSlab`) rather than the literal `fvol1`-`fvol9` name pattern, and "the
edge-extension radius" resolved to the far_fade bound (above) once the terrain's own 5,120 m
reach priced out at ~2.35x base.*

**Model recommendation.** medium — a bounded generalisation of the landed scatter, with the
determinism constraint.

**Verify.** Probe at the map rim (e.g. x near −12288 and 0) looking outward and along the edge:
field continues with no seam and no density step; sprite-count log states the new total and the
extension radius; `--det` md5 stable across runs; C5/C1C counts unchanged.
— *all done, see Verification above (a corner and a well-outside-looking-back probe were added
beyond what was written here).*

**⚠ Traps.** `cloudparent` stops at the map edge in the original (BL-118's note) — extending the
wrong population would invent content. The far fade (3500 m) must keep the working set bounded;
state the extension's sprite budget.
— *both respected: `cloudparent`'s census is bit-identical to A6/A7's own, and the sprite budget is
stated and measured above (1.38-1.46x base, bounded by far_fade rather than the terrain's own
5,120 m reach).*

## A6 ☑ Why does flight mode show puffs below the deck when freecam doesn't?

**Landed.** (2026-08-08) **It was never flight-vs-freecam, and it was never `cloudparent`. The
deck was in the wrong place in BOTH modes, and A3's probe misread which surface was the deck.**
`WeatherRig.Tick` re-pinned the deck's Y to the `CLOUD_COVER` band centre every frame — C1's
**1047.0 m**, 87 m above the deck's own authored 960 m — while A3 had just top-anchored the
`fvol` cards to the slab top, putting their bottoms at 986.3–1037.7 m. Every one of C1's 9,025
sprites therefore hung **9.3–60.7 m below the deck sheet**, at every camera altitude, in flight
and in freecam alike. The fix deletes the Y re-pin: the deck follows the camera in X/Z only and
keeps the altitude its chapter's gamez authors. One line.

**The user's "the fix only engages above the whiteout" hypothesis is REFUTED** — there is no
altitude gate anywhere in the path, and the instrumented deck altitude is a flat 1047.0 m at
cam_y 300 / 900 / 1000 / 1100 / 1200 m. What varies with altitude is only whether the defect is
*visible*: below ~986 m every card is above you and the deck's underside fills the sky, so
nothing projects below the horizon and the frame looks clean; from ~986 m up the cards hang past
the deck plane and read as lumps on the underside. A3 sampled 934 m — inside the blind spot —
which is why its probe and the user's controls disagreed.

**Files.** `CSVM/src/Session/WeatherRig.cs` (the fix), `CSVM/src/Mech3/WorldBuilder.cs` (a
comment that cited the removed pin), `docs/architecture.md` (`WeatherRig.cs` entry),
`docs/formats/fogvol.md` (the authored deck/slab-floor invariant + a correction of A3's probe
reading), `backlog.md` (`BL-118`'s two `cloudparent` bullets), this section.

### The authored invariant that decides it

Read from each chapter's `extracted/<ch>/gamez/nodes.json` `model_bbox` before any code changed:

| chapter | deck tiles | `fvol1`–`fvol9` floor | gap | `CLOUD_COVER` centre | deck was pinned to |
|---|---|---|---|---|---|
| C1 | 960.0 | 970.00 | 10.00 | 1047.0 | 1047.0 (**+87.0**) |
| C1C | 960.0 | 970.73 | 10.73 | 1082.5 | 1082.5 (**+122.5**) |
| C2B | 960.0 | 970.00 | 10.00 | 1024.0 | 1024.0 (**+64.0**) |
| C4 | 1050.0 | 1060.00 | 10.00 | **1050.0** | 1050.0 (**+0.0**) |

**All four deck chapters ship the deck mesh exactly ~10 m below their slab floor** — the mesh and
the sprite field are one sheet in the data, mesh underneath. The band-centre pin breaks that in
three of four; in C4 it happens to *be* the authored altitude, which is both why C4 never showed
the defect and the corroboration that the deck altitude and the cover band are one authored
thing. The pin predates the `fvol` field entirely (it is older than the project's rename commit,
written when a hand-tuned per-rig `CloudPuffs` was the only cloud population), and its stated
purpose — hiding the ceiling→floor crossing inside the opaque whiteout core — is a cost this item
knowingly gives up: the crossing now happens at the altitude the original's own static tiles sit
at, which is the crossing the original renders.

### Reproduced, then measured

Temporary `GD.Print` in `Tick` (removed; `git diff` clean, METHOD-17) logging the deck's live Y
against the camera's, over a `--fly --chapter=C1 "--pos=-7325,<alt>,-3829" "--direction=0,0,-1"
"--hold=0,0,0,0"` ladder and the identical `--freecam` ladder:

```
A6 deck: cam_y=305.2  deck_authored_y=960.0 deck_live_y=1047.0 band=970-1124 mid=1047.0 whiteout=0.00
A6 deck: cam_y=905.2  … deck_live_y=1047.0 … whiteout=0.00
A6 deck: cam_y=1005.2 … deck_live_y=1047.0 … whiteout=0.57
A6 deck: cam_y=1105.2 … deck_live_y=1047.0 … whiteout=0.30
A6 deck: cam_y=1205.2 … deck_live_y=1047.0 … whiteout=0.00
```

Identical to the metre in `--freecam` at 300/900/1000/1100/1200 m. **The deck does not track the
plane in Y and does not flip at the band** — suspect (a) as the plan phrased it was wrong about
the mechanism but right about the module.

Pixel measure, `--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00
--tex-override=cloudlayer.tif=ff0000 --no-fog` (the deck flattened too, which is what A3 did not
do — SHOT-13, DIAG-12):

| pose | sprite px before | sprite px after |
|---|---|---|
| freecam river `-7325,934,-3829` (A3's own pose) | **85,507** | **0** |
| `--fly` 900 m | 98,485 | 3,611 (all HUD text — the HUD is green) |
| `--fly` 1000 m | 275,457 | 395,420 (correctly *above* the deck now: camera is inside the field) |

### Why A3's probe read clean — both of its readings measured something else

- **"Looking straight up, zero green pixels."** A `cloudsprite` is a `Facade`/`SphericalY`
  billboard, so from *directly* below it is **edge-on**. That number is a fact about billboard
  orientation and carries no altitude information (INSTR-11: a probe that reports one half of a
  compound thing reads as a full pass on the half it can see).
- **"The field's lower edge sits cleanly above a flat gray band — the `CloudDeck` mesh underside
  at y=960."** The deck was at 1047 m, and the big pale surface filling the top of
  `.scratch/a3/after-river-override-level.png` **is** that deck — the green is painted *over* it,
  which is exactly "below the deck". The flat gray band A3 identified as the deck is the dome seen
  under the deck's **far edge** (the deck follows the camera in X/Z, so its rim sits 6,144 m out,
  1.05° above the horizon from 934 m). `.scratch/a6/before-a3riverpose-deck-red.png` is the same
  frame with `cloudlayer.tif` flattened red and settles it at a glance.

### `cloudparent` is cleared, with data — and BL-118 gains two corrections

Read from `extracted/C1/gamez/nodes.json` before any probe, per the item's own instruction:
C1 ships **28** `cloudparent` clusters, world pose on the **grandparent** g-node
(`world1 → g0|g27816 → l2586 (Lod) → cloudparent`, the `cloudparent` node itself being identity),
all at **Y = 1107.2379** on a 1024 m X/Z grid; their 626 child facades span **1069.7–1875.6 m** of
world Y. **Every one of them is above both the pinned deck (1047 m) and the authored deck
(960 m)**, so `cloudparent` cannot produce a puff below the deck at all and is not what the user
saw. Two facts went to `BL-118`:

1. **The two populations share their textures** — all 626 `cloudparent` facades are skinned
   `cloud1.tif` (645 material refs) / `cloud2.tif` (362), the same two the `cloudsprite1/2`
   templates use. `--tex-override` **cannot** separate them; separate by altitude or position.
   (This is why the probes above are still sound: nothing of `cloudparent` reaches below 1069.7 m.)
2. **`BL-118`'s "C4's 45 `cloudparent` parked at the world origin, runtime-placed by mission
   setup, altitude not in `nodes.json`" is a misreading and is corrected there.** The identity
   transform is on the `cloudparent` node; the pose is on its grandparent. C4's 45 are authored in
   the gamez at Y 1382.815 (×25) / 1400.0 (×20), and nothing places them at runtime. C1's only
   runtime touch is `extracted/C1/zrdr/clouds.zrd.json`, an `ON_STARTUP` `OBJECT_OPACITY_STATE`
   holding every `cloudparent` at **0.6** within 1900 m forever — it never translates anything.

Suspect (c), a transparency/draw-order path, was never reached: the ordering was correct
throughout, the deck was simply at the wrong altitude.

### Verification

- **`.\RunTests.ps1`** — build PASS (0 warnings), **units 648/648**, **engine 26/26, errors
  clean**, goldens **6 moved, 0 broken of 13**. Exit 1 is the golden stage alone. **The six are
  the same six A2 and A3 moved and are not re-pinned — that is still A4's job** (GOLD-1).
- **`c4-snow` is inert to THIS change and that was measured, not assumed** (GOLD-4/GOLD-5): its
  own manifest args at its own `--frames=120` give `pixmd5=e83de4bc10177238f8d6040e6ba5e21b` on
  the fixed build *and* on a temporarily-restored baseline build — **bit-identical**. Its MOVED
  line is inherited from A2/A3, since the manifest still carries pre-A2 hashes. The A4 fix's
  own footprint is therefore exactly the C1/C1C/C2B deck chapters, as the table above predicts.
  A3's C4 clear-air probe is likewise bit-identical (`7E137451FADD05CB1AEB44916BF5247E`).
- **8-chapter `--freecam` regression** — zero errors in all eight; deck census unchanged (C1/C1C/
  C2B 144 tiles y=960, C4 144 tiles y=1050, four chapters no deck); sprite counts unchanged from
  A3 (C1 9,025 · C1C 9,572 · C2B 9,025 · C4 9,025 · C5 16,170).
- **A3's freecam river probe does NOT re-run unchanged, and must not** — it is the probe whose
  reading was wrong. Its *conclusion* ("no sprite bottoms below the deck sheet") is now true for
  the first time, and `fogvol.md`'s A3 entry carries the correction inline rather than silently.

### Probe images (`.scratch/a6/`)

| file | what it is |
|---|---|
| `before-a3riverpose-deck-red.png` / `after-a3riverpose-deck-red.png` | **the item's evidence** — A3's own pose with the deck flattened red: green all over the red before, none after |
| `before-natural-river-934.png` / `after-natural-river-934.png` | the same pose in natural colours: cauliflower lumps hanging below the sheet → the flat gray underside the original still shows |
| `before-fly-{300,900,1000,1100,1200}m-level.png` / `after-…` | the flight-mode ladder, override-isolated |
| `before-freecam-…` / `ctrl-freecam-1000m-level.png` | the freecam control ladder — identical behaviour, which is what refuted the premise |
| `before-natural-abovedeck-1192.png` / `after-natural-abovedeck-1192.png` | the pinned above-deck pose: the hard-edged rectangular plates in the near sheet (deck tiles cutting through the field at 1047) are gone |
| `golden-c4-snow-baseline.png` / `golden-c4-snow-after.png` | the bit-identical C4 golden pair |
| `after-c4-1135-override-level.png` / `before-…` | A3's C4 clear-air probe, bit-identical |
| `regress-C{1,1B,1C,2,2B,3,4,5}.png` | the 8-chapter regression sweep |

**⚠ Handover.** (1) **A4 re-pins the goldens and must fold this item's movers in with A2's and
A3's** — the moved SET is unchanged, but C1/C1C/C2B pixels moved a third time. (2) **Wave C's
underside measurements are now taken against a deck at a different altitude than CAP-12's
matched-box A/B assumed** — re-take the +54 box at the new geometry before implementing C22, and
do not carry the old row over. (3) The deck crossing at 960 m is no longer masked by the
whiteout; if that reads badly at the controls it is a **new** item about the whiteout band's
altitudes, not a reason to put the pin back. (4) The `--tex-override` texture-sharing trap
(`cloudparent` = `cloud1/cloud2`) applies to every remaining item in this plan that isolates the
field by texture.

### Original approach (kept for reference)

**Goal.** The user's at-the-controls report ("still showing below the deck") reproduced,
mechanism named, and either fixed or reclassified with evidence.

**Evidence (confidence: lead-only).** Contradiction on file: A3's freecam tex-override probe
counts zero sprite pixels below 960 m at the river pose, but the user flying C1 (RunGame,
2026-08-08) still sees puffs below the deck. Their own hypothesis: the fix may only engage
above/inside the whiteout. Prime suspects, in order: (a) the deck-follow behaviour — in flight
the deck tracks the plane and flips above/below at the cloud band (`GameSession`/`WeatherRig.Tick`),
so the deck sheet's live altitude in flight is not freecam's 960 m; (b) the `cloudparent`
population — C1 ships 28 stationary clusters, untouched by Wave A, indistinguishable from fvol
sprites at the controls (the BL-118 vocabulary trap in both directions); (c) a transparency/draw
order path that lets sprites read through the deck sheet.
— *(a) named the right module and the wrong behaviour (the deck is pinned to a constant, it does
not track or flip); (b) is cleared by data; (c) was never reached. The premise itself — "flight
shows it, freecam doesn't" — is false: both modes showed it, and A3's freecam probe was misread.*

**Approach.** Reproduce in FLIGHT mode (not freecam): fly the river area below the deck with
`--tex-override` isolating cloud1/cloud2 vs the deck texture vs cloudparent's textures (check
what cloudparent instances actually skin — read the template in the gamez data first). Log the
deck node's live Y while flying. Classify what is visible below 960 m; fix if it is fvol scatter
or the deck-follow logic, reclassify to the correct item/backlog entry if it is cloudparent
(that population is BL-118's business, not this wave's).
— *followed as written; "isolating … vs cloudparent's textures" turned out to be impossible —
they are the same two textures — which is itself a finding and went to `BL-118`.*

**⚠ Traps.** Do not "fix" cloudparent placement here — if the sighting is cloudparent, the
verdict lands as evidence on the appropriate item and this item closes as reclassification.
The whiteout band is a separate system; a sprite seen through whiteout murk is not "below the
deck". — *both respected: `cloudparent` was not touched, and every probe above ran `--no-fog`
(whiteout off) or measured flattened colours, so no reading is a whiteout artifact.*

## A7 ☑ The deck is engine trickery: regime model + cloud layer gate

**User verdict at the controls (2026-08-08, post-landing re-fly): "it looks a lot better.
approved."**

**Landed.** (2026-08-08) **All three of the user's observations reproduce, and the below-band
half is now provably exact: with the ceiling carried at `camera.y + K`, the sky is
BIT-IDENTICAL at 192 / 300 / 600 / 900 m** — the same frame to the pixel, which is the strongest
form the "the texture's look is exactly the same at every altitude" report can take. The old
world-fixed sheet moves 87 % of those pixels over the same climb, so the check can fail.
`K = 400 m`, a **TUNE matched to the river still** by apparent mottling scale (derivation below);
above the band the deck is a world-fixed floor at the `CLOUD_COVER` centre, and both cloud
populations are gated per camera by cull mask on one shared visual layer.

**Files.** `CSVM/src/Session/WeatherRig.cs` (the regime + the gate + `K`),
`CSVM/src/Flight/Weather.cs` (`CloudBandCentre`, one spelling for whiteout core and regime flip),
`CSVM/src/UI/SplitScreen.cs` (`CloudFieldLayer`), `CSVM/src/Mech3/WorldBuilder.cs`
(`CloudClusters` — the `cloudparent` census), `CSVM/src/Session/GameSession.cs` (moves both
populations onto the layer), `CSVM.Tests/DeckRegimeTests.cs` (new, 5 tests),
`docs/architecture.md`, `docs/formats/weather.md`, this section.

### What it does

| camera | deck | fvol clutter + `cloudparent` |
|---|---|---|
| **below** the `CLOUD_COVER` band centre | ceiling at `camera.y + 400 m`, following in **all three** axes | **culled** for that camera |
| **at/above** the centre | world-fixed floor **at the band centre**, following in X/Z | **rendered** for that camera |
| chapter with **no deck mesh** | — | **rendered, always** — the gate is never armed |

⚠ **The deckless guard has a second half, found while building this and easy to miss.** The
single-player camera is the *Launcher's* and outlives the session; `Tick` only ever CLEARS the
cloud bit, and a chapter that never arms the gate never sets it back. So quitting a C1 flight from
under the deck and launching C5 would have left C5's street haze culled for the whole flight.
`GameSession.BuildRigs` re-adds the layer to that camera's mask at session start (splitscreen
cameras are built fresh and need no reset).

The rule is one pure function, `WeatherRig.DeckRegime(cameraY, bandCentre)`, so what has to be
asserted can be: `Tick` only applies it once per rig. The gate is a per-camera **cull mask** over
`SplitScreen.CloudFieldLayer` (bit 15, layer 16 — taken below the per-player band so every cull
mask starts with it *included*; the gate is something that switches OFF, never something a new
camera must remember to switch on). Both populations are **moved** onto that layer, off the
default layer 1, in `GameSession`: the `fvol` MultiMeshes and every `cloudparent` subtree
`WorldBuilder.CloudClusters` censuses. That census matches on the node's **gamez** name
(`AnimRuntime.NameMeta`) because all 28 of C1's are literally named `cloudparent` and Godot's
duplicate-sibling renaming is free to have touched `Node.Name` (WORLD-8). New per-chapter line:
`cloud clusters: N placed 'cloudparent' subtree(s)` — C1 **28**, C1B **70**, C1C **30**, C4
**45**, and C2/C2B/C3/C5 none.

### Deriving K = 400 m — apparent mottling scale, and it validates itself first

A ceiling `h` metres above the camera projects a world point at horizontal distance `d` to
elevation `e = f·h/d` px above the horizon row. So resampling the sky's luminance profile against
`u = 1/e` turns the deck texture into a **strictly periodic** signal of period `P/(f·h)`, `P`
being the texture's own world period. Two consequences: on a render where `f` and `h` are known
it measures `P`; on the original still, with `P` known, it measures `f·h`.

**Calibrated on our own renders first** (`.scratch/a7/ceiling_scale.py`), deck at its authored
960 m and the camera at 560 / 760 / 160 m ⇒ h = 400 / 200 / 800 m, level camera so the horizon
row is exactly the image centre: the recovered world period comes back **1062 m against the
authored 1024 m tile** (3.7 %). The method reproduces the tile it was never told about, and
confirms one texture repeat per tile.

**Run on `OriginalScreenshots/C1 IA1 Fog river.png`** (`.scratch/a7/ceiling_zcr.py`, mottling
crossings per unit `u`, horizon row 356 = the image centre — the gunsight sits there and the
aircraft is wings-level):

| frame | left half | right half |
|---|---|---|
| **the original** | **504 /u** | **798 /u** |
| ours, ceiling at K = 400 m | 566 /u | 669 /u |
| ours BEFORE, deck world-fixed at 960 m (h = 60 m at this camera) | 78 /u | 78 /u |

Our K = 400 m sits **inside the original's own left/right spread**; the pre-A7 sheet is an order
of magnitude off, which is the able-to-fail control (METHOD-9). The estimators bracket the
original at **260–590 m**, and K scales with the assumed vertical FOV (K ∝ tan(FOV_v/2); 62° is
what our matched-pose twins render at). So: **TUNE, matched to that still**, not a decoded
constant — and one constant for every deck chapter, because C1's river still is the only original
frame that can measure one.

⚠ **The plan's "river ≈ `x -7325 y 934 z -3829`" is wrong about the altitude, and it is on
record wrong in `fogvol.md:199` too.** The checked-in twin `Screenshots/C1 IA1 Fog river.png`
reads **`x -7323 y 192 z -3829`** in its own freecam overlay (the above-deck twin reads `1192`,
so this is not a cropped digit), and the original's own ALT gauge reads ~700–750 ft ≈ 215–230 m.
**K is unaffected** — it is derived from mottling scale, which never uses the camera's altitude —
and so is every A3/A6 conclusion, which were self-consistent before/after pairs at a documented
pose. But "the river pose" as an altitude is not 934 m, and A4/B15's matched-pose A/Bs need the
right one. Both were shot here (below, and `after-river-192*.png`); their skies are bit-identical,
which is the item's own point.

### Verification

- **Texture-look constancy below the band — bit-identical.** `--no-fog`, level, sky rows 0–250:
  192 / 300 / 600 / 900 m all `mean|d| = 0.000, max = 0, 0 px changed`. The same rows on the
  **pre-A7** build move **87.1 % / 87.5 %** of pixels (mean |d| 8.0) over 300→600→900.
- **Climb ladder 900→1250 m** (`.scratch/a7/after-ladder-*.png`, 25 m steps plus a 1 m bracket on
  the flip). The four frames spanning the 1047 m flip — **1035, 1046, 1048, 1060** — are
  **bit-identical to each other** (`step mean|d| = 0.000, max = 0`), frame std 1.41 on a mean of
  243: a flat whiteout pane. The flip is unobservable, measured rather than argued. The largest
  steps in the ladder are the whiteout's own ramps (975→1000 = 25.2, 1000→1025 = 24.8) and its
  fade-out, all monotone.
- **The gate, isolated** (`--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00
  --no-fog` — both populations share those two textures, A6, which is exactly what makes one
  pixel count the right instrument for a gate covering both):

  | pose | cloud px |
  |---|---|
  | C1 900 m level / **looking straight up** | **0 / 0** |
  | C1 1046 m looking up (1 m under the flip) | **0** |
  | C1 1048 m looking up (1 m over the flip) | **921,600** (the whole frame) |
  | C1 1192 m level | 530,742 |
  | C4 900 m up (below its 1050 centre) / C4 1200 m level | **0** / 921,600 |
  | C1C 1350 m (build-ups, above its 1082.5 centre) | 468,639 |
  | **C5 street level, band at 9950–10150** | **161,541** — the guard |
  | **C1B 500 m up, no deck, 70 `cloudparent`** | **473,915** — the guard |

- **Above-band floor parallaxes.** Below-horizon rows across 1150 / 1200 / 1250 m move 32.5 % /
  27.3 % of pixels — the sheet recedes as the camera climbs, the exact opposite of the below-band
  invariant measured above.
- **Splitscreen.** `--fly --players=2` instrumented (temporary `GD.Print`, removed, `git diff`
  clean — METHOD-17): the two panes' masks are written independently, `P1 cull=0x17FFF /
  P2 cull=0x27FFF` below the band (bit 15 cleared in each) and `0x1FFFF / 0x2FFFF` above. A
  two-pane probe **cannot** put the panes on opposite sides — `SpawnPicker` fans players
  `dir.Cross(Vector3.Up)`, which is horizontal for every direction — so the opposite-sides case is
  a code-level assertion: `DeckRegimeTests.TwoCamerasOnOppositeSidesOfTheBandGetOppositeRegimes`.
- **`CSVM.Tests/DeckRegimeTests.cs`**, 5 tests, including one on the **authored** C1/IA1
  `CLOUD_COVER` asserting the flip altitude is inside the fully-opaque core
  (`WhiteoutAmount(centre ± 1 m) == 1`), with its own able-to-fail leg (the core does end).
- **8-chapter `--freecam` regression** — zero errors in all eight; every census unchanged from A6
  (decks C1/C1C/C2B 144 @ 960, C4 144 @ 1050, four chapters none; sprites C1 9,025 · C1C 9,572 ·
  C2B 9,025 · C4 9,025 · C5 16,170).
- **`.\RunTests.ps1`** — build PASS (0 warnings), **units 653/653** (648 + this item's 5),
  **engine 26/26, errors clean**, goldens **6 moved, 0 broken of 13**. Exit 1 is the golden stage
  alone. **The six are the same six A2/A3/A6 moved and are NOT re-pinned — still A4's job**
  (GOLD-1): `c1-waterfall`, `c1c-rain`, `c2b-rain`, `c4-snow`, `c1-flight`, `c1-destroy-effects`.
- **The moved set is the predicted footprint** (GOLD-5): every mover is a **deck** chapter
  (C1/C1C/C2B/C4); every deckless chapter's golden is untouched (`c1b-night-sea`, `c2-city`,
  `c3-island`, `c5-city-night`), as are `viewer-bhawk`, `empty-stage` and `c1-crash` (a C1 shot
  whose frame holds no sky). **`c4-snow` is A7's own, and that is measured**: A6 recorded it
  bit-identical either side of its change at `pixmd5=e83de4bc10177238f8d6040e6ba5e21b`; it now
  renders `fb631da4df2dfe2d6a2057ae2ae21d6f`. Its camera is at y = 958, **below** C4's 1050 band
  centre, so A7 moves the chapter A6 could not — C4's coincidence (band centre = authored deck
  altitude) only holds in the ABOVE regime.

### Two findings for whoever takes the next item

1. **The above-band regime is bit-identical to the pre-A6 pin at the pinned above-deck pose** —
   `.scratch/a6/before-natural-abovedeck-1192.png` vs `.scratch/a7/after-abovedeck-1192.png`:
   `mean|d| = 0.000, max = 0`. That is by design (the user's observation 2 says the old pin was
   the above-band half of the trick), but it means A6's cited improvement at that pose — "the
   hard-edged rectangular plates in the near sheet (deck tiles cutting through the field at 1047)
   are gone" — **is reverted for cameras above the band**. If those plates read badly at the
   controls, the item is the deck-mesh↔sprite intersection (C23 / `BL-118`'s mesh↔sprite cut),
   not the regime.
2. **Our fog still eats the ceiling the original shows.** With K = 400 m the geometry now matches
   (table above), but at the river pose with fog on, our mottling dies ~140 px above the horizon
   against the original's ~330 px. That is `fogRangeFactor` 2.0 halving the authored range
   (B15) and/or the fade model (B14) — measured here, not fixed here, and it is the same
   "our fog hides more of the clouddeck" symptom B15 already owns.

### Probe images (`.scratch/a7/`)

| file | what it is |
|---|---|
| `AB-river-ceiling.png` | **the item's evidence** — original / pre-A7 / A7, `--no-fog`: no ceiling → a matched one |
| `AB-river-fogged.png` | the same three with fog on: geometry fixed, fog still eating it (finding 2) |
| `AB-abovedeck.png` | the pinned above-deck pose, original / before / after |
| `AB-ladder-flip.png` | 1025 / 1046 / 1048 / 1075 m — the flip inside the whiteout core |
| `after-ladder-{0900…1250}.png` | the climb ladder, 25 m steps + the 1 m bracket |
| `after-gate-*.png` | the `--tex-override` gate table above, one file per row |
| `before-/after-const-{300,600,900}[-nofog].png` | the constancy pair |
| `regress-C{1,1B,1C,2,2B,3,4,5}.png` | the 8-chapter regression sweep |
| `ceiling_scale.py` / `ceiling_zcr.py` / `compare.py` / `green_px.py` | the instruments, kept |

### Original approach (kept for reference)

**Goal.** The deck behaves as the original's does, per regime: below the band a camera-following
ceiling whose texture look never changes while climbing; the above/below flip hidden inside the
opaque whiteout core; above the band a world-fixed floor at the band centre — and BOTH cloud
populations (fvol clutter and `cloudparent`) hidden below the band, visible above, per camera.

**Evidence (confidence: direction traced — user at the controls of the original, 2026-08-08;
magnitudes open).** Three observations from the A6 re-fly: (1) climbing below the deck, the
texture's look is *exactly the same* at every altitude — a world-fixed sheet would grow and
parallax, so the below-band ceiling follows the camera vertically; (2) after the whiteout the
sheet lies below at a **fixed height ≈ the whiteout centre** (C1: 1047 m) — which is exactly the
altitude A6 found the old code pinning at, i.e. the old pin was the *above-band half* of the
original's trick applied in both regimes; (3) from below, the view is the pure sheet — **"not
even the cloud groups"** (`cloudparent`) show, so the gate covers both populations. CAP-12's
"first wisps at ~982 m" are reattributed to the always-present plane-local wisp population
(`BL-317`), not the field appearing.
— *all three reproduced; (1) is now an exact pixel identity rather than an impression.*

**Approach.** `WeatherRig.Tick` deck regime: camera below band centre → ceiling at
`camera.y + K` (K constant — derive from the original stills: texture tiles are 1024 m, so the
apparent mottling scale in `C1 IA1 Fog river.png` at its known 934 m camera fixes the ceiling
distance; if underdetermined, K is a marked TUNE matched to that still, which is the plan's own
target); camera at/above band centre → floor at the band centre, world-fixed. The flip happens
at the centre crossing, inside the opaque core (C1: 1032–1062), so it cannot be seen. Cloud
layer gate: put the fvol MultiMeshes and the `cloudparent` subtrees on a dedicated visual layer;
toggle each camera's **cull mask** by that camera's own altitude vs the band centre — never node
visibility, which would leak across splitscreen panes. Read how `cloudparent` instances are
built (WorldBuilder/SceneBuilder) to tag the instances, not just a template root.
— *followed as written. The mottling-scale derivation worked and did NOT need the camera's
altitude, which is what saved it from the 934/192 error above. `cloudparent` instances are tagged
by gamez name off a post-walk census in `WorldBuilder`, not by template root.*

**Model recommendation.** high — camera/layer machinery with splitscreen and per-chapter data
variation; the flip masking is easy to get subtly wrong.

**Verify.** Climb ladder 900→1250 m every ~25 m: no visible pop anywhere (frames inside the core
are near-uniform white, so the flip is unobservable); texture-look constancy below (mottling
feature scale identical at 300/600/900 m); above-band floor parallaxes normally from 1100+;
both pinned poses re-shot; C4 regime check (band centre = its authored 1050); **C5's street haze
still visible at street level** (see Traps); C1C build-ups visible from above the band;
splitscreen `--players=2` with panes on opposite sides of the band each render their own regime;
`.\RunTests.ps1` (goldens will move again — list, don't re-pin).
— *all done; the splitscreen half became a code-level assertion for the reason recorded above.*

**⚠ Traps.** **Chapters with no deck mesh must bypass the gate entirely** — C5 has clutter, no
deck, and an unreachable band at 9950–10150 m: an unguarded gate hides its street haze forever.
The gate is per-camera cull mask, per view. Do not touch the whiteout band's own altitudes or
opacity — if the core doesn't fully mask the flip somewhere, that is a finding about the
whiteout (new item), not a licence to move the flip altitude. K is TUNE-marked with its
derivation cited. `cloudparent` stays untouched apart from layer assignment.
— *all respected: the guard is measured on C5 (161,541 cloud px at street level) and C1B
(473,915); the whiteout's altitudes and opacity are untouched and the core is asserted to mask
the flip by a test on the authored band; `cloudparent` gained a visual layer and nothing else.*

# Wave B — fog semantics and zones

## B11 ☐ All-chapter zone-table survey; test H1/H2/H3 on paper

**Goal.** One table: every chapter × zone's `FOG_RANGES`/`FOG_ALTITUDE`/`FOG_COLOR`/`CLIP_RANGES` +
sunlight, and against it each hypothesis' predictions for (a) the settled zone verdicts
(C1B/C2/C3/C5 = zone1), (b) BL-303's three broken scenes, (c) the CAP-11-confirmed healthy scenes,
(d) the two C1 stills. Hypotheses: **H1** `FOG_ALTITUDE` fades the whole fog effect by *camera*
altitude; **H2** per-fragment fade (current shader); **H3** altitude-triggered zone *switching*
(user hypothesis, decision 5; candidate switch point = zone1's band top, 1047 in C1 — note C5's
identical bands make a switch unreachable there, which is consistent with its zone1 verdict);
**H4** *positional* zone switching — inside vs outside a fog volume (added 2026-08-08 from the
user's C5 sighting: flying into a street volume drops visibility to ~10 % of the mission fog,
clip `OriginalScreenshots/Videos/CAP-11 C5 Flying into fog zone.mp4`; C5 `ZONE3` authors 50–250
vs `ZONE1` 1500–2250 — 250/2250 ≈ the estimate — and C5's fogvol.zrd is the only one authoring
interior-fog keys; `BL-315` holds the render feature, B11 owns whether zones and interior fog
are one mechanism. Note C1's `fvol` nodes carry `zone_id: 2` — check the sign of that
correlation per chapter).

**Evidence (confidence: lead-only).** The C1 IA1 numbers above; `weather.md`'s zone survey; the
BL-303 case notes. H1 and H3 are not exclusive.

**Approach.** Script the survey over `extracted/*/*/zrdr/weather.zrd.json` (read-only, into
`analysis/` with a FINDINGS.md if it earns citation); fill the prediction matrix in this section.
Any hypothesis that contradicts a settled verdict or a healthy scene dies here, before code.

**Model recommendation.** high — the prediction matrix is the wave's steering document.

**Verify.** Each hypothesis has an explicit pass/fail against (a)–(d), with the discriminating
scene named for every fail.

**⚠ Traps.** Colour triples are dual-encoded (`ParseColor`, strict >1 rule) — read values through
the reader, not by eye. `VIEWING_RANGE` `FOG_SCALE` (HIGH = 1.0) is data the remake ignores; note
it in the survey since it bears on B15's factor.

## B12 ☐ Dome-identity discriminator at the above-deck pose

**Goal.** Which zone's dome the original renders above C1's deck, settled by comparison.

**Evidence (confidence: traced for the instrument).** `--sky-zone=zone1`/`=zone2` render a named
zone literally (weather.md); the above-deck original shows a smooth blue gradient and no stars;
our current zone2 render shows stars. The worktree probe shows un-fogging the dome removes the
gray band.

**Approach.** Render both domes at the pinned above-deck pose (fog neutralised the same way on
both shots); compare gradient shape and star field against the original still. If neither dome
matches alone, that is evidence for H3's below/above split or for fog-on-dome participation —
record, don't force a verdict.

**Model recommendation.** medium — two renders and a careful comparison.

**Verify.** A side-by-side in `.scratch/` cited here, with the verdict and what it rules out.

**⚠ Traps.** An explicit `--sky-zone=` skips `PreferPopulatedHorizonZone` — that's the point, but
remember the fog follows the *requested* zone; keep fog identical across the comparison or you're
comparing fog, not domes.

## B13 ☐ Footage discriminators: camera-vs-fragment fade, switch point

**Goal.** H1 vs H2 settled by what the original renders through/above the deck; if H3 survives B11,
the switch point bracketed.

**Evidence (confidence: lead-only).** CAP-12's six deck crossings may already contain
valley-through-gap frames (fragment fade predicts the valley stays hazed from above; camera fade
predicts it clears). The sky-character flip across the deck, if any, brackets the switch point.

**Approach.** Re-read CAP-12 via the `analyse-capture` pipeline for the discriminating frames. For
anything missing, mint CAP item(s) (`New-ItemId.ps1 -Kind CAP`, decision 9): e.g. a slow climb
3000→4200 ft holding a valley view through a deck gap, then level flight above the deck panning
ground↔sky. **[USER]** flies them. If all footage is exhausted and the rule still isn't
discriminable, STOP this thread and put the binary-RE question to the user (decision 6) — do not
guess.

**Model recommendation.** high — frame-level evidence reading with a stop condition.

**Verify.** Each discriminator's verdict cited to clip + timestamp here; open questions named
explicitly.

**⚠ Traps.** `docs/verification.md` first — video luminance is compressed and graded; compare
regions within one frame, not absolute values across clips. Game DVR swallowed 1-px rain streaks
once (BL-302's calibration note) — thin wisps may be capture-invisible too.

## B14 ☐ Implement the winning fog model

**Goal.** The fog our shader computes is the model B11–B13 evidenced, and the above-deck still's
clear sky falls out of it.

**Evidence (confidence: lead-only until B11–B13 land).** Today: `csky_fog_amount` fades by
fragment altitude (`csky_atmosphere.gdshaderinc:27`); `WeatherRig` picks one zone per flight.

**Approach.** Whichever won: camera-altitude fade → move the fade term to camera y (one global,
computed rig-side per view — splitscreen: per-camera, like the far fade); zone switching → the
switch rule in `WeatherState`/`WeatherRig.Tick` (fog + dome + whiteout swap together — the
weather.md rule that sky and fog must never come from different zones now applies *per altitude
regime*), with `--sky-zone=` becoming an explicit override that pins the zone (cli.md bullet
updated). Update `weather.md`'s `FOG_ALTITUDE` semantics + the zone-selection section in the same
turn.

**Model recommendation.** high — shader + state-machine change with splitscreen and determinism
surface.

**Verify.** Both pinned poses; a climb-through sequence (`--shots` ladder 900→1250 m) matching the
original's transition; BL-303's three poses re-shot (expected fixed — final call is B17's);
CAP-11's healthy poses unchanged (baseline first — an unchanged number must be able to fail);
8-chapter freecam regression; `.\RunTests.ps1`.

**⚠ Traps.** The whiteout trapezoid and the fog handover are tuned to meet (zone1 970→1047 =
band-bottom→whiteout-centre) — if zones switch, the handover must still be seamless while climbing.
Splitscreen: two players on opposite sides of the switch altitude must each get their own fog —
globals that are per-mission today may become per-view; the far-fade-per-view pattern in
`FogVolumeClutter` is the precedent.

## B15 ☐ Re-calibrate or delete `fogRangeFactor` 2.0

**Goal.** The factor is either 1.0 (authored ranges, matching `VIEWING_RANGE` HIGH `FOG_SCALE` 1.0)
or a value defended by a fresh A/B — no stale TUNE.

**Evidence (confidence: traced).** `WeatherRig.cs:182`: its own comment says the 2.0 calibration
predates the sRGB fog-colour fix and owes a re-check; the data ships `FOG_SCALE` 1.0 at HIGH detail.

**Approach.** After B14 lands, A/B the river pose at factor 1.0 vs current: the original's
deck-into-haze distance is the yardstick ("our fog hides more of the clouddeck" is this item's
symptom as much as B14's). Land the winning value with the evidence in the commit; delete the
stale comment.

**Model recommendation.** medium — one constant, one matched A/B.

**Verify.** River-pose region boxes for valley haze + deck-visibility distance against the
original still; C4 valley haze (CAP-12 `t8.0` terrain still) not regressed.

**⚠ Traps.** Test only after B14 — the factor compensating for wrong semantics is exactly how it
went stale last time.

## B16 ☐ The dome's authored `fog: false` (lever, evidence-gated)

**Goal.** The dome fogs, or doesn't, per evidence — not per the 2026-07 deviation surviving by
default.

**Evidence (confidence: lead-only).** Every horizon model in every chapter authors `fog: false`
(`SceneBuilder.ForceFogged` doc); the worktree probe shows un-fogging removes the above-deck gray
band; but B14's semantics may already clear the above-deck sky with the dome still fogged below.

**Approach.** After B14/B15: if the above-deck pose still shows dome fog, honour the authored flag
(drop `ForceFogged` in `BuildHorizon`) and A/B at poses where the dome is *visible*: the C3 canyon
pose (blue-gradient sky) and C2B above-deck — per decision 7 the river still can't judge this. If
B14 already cleared it, record that and leave `ForceFogged` with an updated comment naming this
item's verdict.

**Model recommendation.** medium — one flag, but the regression surface is every chapter's horizon.

**Verify.** C3/C2B/C5 poses + above-deck pose; 8-chapter freecam sweep eyeballing the horizon band
for the crisp-edge regression the deviation was guarding against.

**⚠ Traps.** The dome's below-horizon skirt already opts out per instance (`csky_fog_on = 0`) — do
not write that instance uniform globally; the index-mismatch hazard is documented at
`WeatherRig.cs:183`.

## B17 ☐ Fog A/B at both reference poses + BL-303's three; close BL-303/BL-100/BL-101

**Goal.** The fog half of the match bar met and recorded; three backlog items closed.

**Evidence (confidence: n/a — the instrument).** Region boxes per decision 8: valley haze, terrain,
deck-into-fog distance (river pose); sky-above-deck, horizon band (above-deck pose); plus BL-303's
three poses against their CAP-11 originals.

**Approach.** Shoot, measure, tabulate here (±10 per region, HUD/plane masked on the original
side). Close `BL-303` (all three scenes), `BL-101` (decision 10), and `BL-100` — fully if the
B-wave's model covers C1C/C2B/C4's choice, else narrowed to exactly what footage couldn't settle,
with the residue stated in the new/edited entry. Re-pin goldens.

**Model recommendation.** medium — measurement and bookkeeping.

**Verify.** The table in this section; `.\RunTests.ps1` green; backlog deletions land with the
commit per convention.

**⚠ Traps.** C5's black-sky case has an adjacent NOT-fog finding (lit facades ×0.58–0.66,
WorldLight already at clamp) — fixing the sky must not silently absorb that; it stays in BL-303's
closing record as still-open if unfixed, minted fresh.

# Wave C — deck mesh brightness

## C21 ☐ Find the original's underside mechanism (research)

**Goal.** A named mechanism for the original's dark (167) mottled underside vs matched tops —
directional lighting on the sheet, authored per-face data, or something else — with the evidence.

**Evidence (confidence: lead-only).** CAP-12: original base is a flat dark-gray sheet with soft
mottling; ours is white and top-lit. Ours renders the deck fullbright × WorldLight 0.80 (the deck
tiles' authored flags + vertex colours: check `nodes.json` — that's the first read). Ratio
167/221 ≈ 0.755; ambient-only lighting of a down-facing sheet under SUNLIGHT 1.2/0.25 predicts far
darker — do the arithmetic against candidate models before coding.

**Approach.** Read the deck tiles' gamez data (vertex colours, `lighting` flag, material);
compute each candidate's predicted underside/top values; pick the one that hits 167 AND keeps tops
at 211/214 and interior at 248/243. With Waves A+B landed, re-shoot the underside box first — the
+54 was measured through the old sprite field.

**Model recommendation.** high — the mechanism choice decides C22 and risks the WorldLight
calibration.

**Verify.** Predicted numbers for all four CAP-12 boxes under the chosen mechanism, written here
before C22 starts.

**⚠ Traps.** WorldLight's terrain calibration is CAP-11-confirmed — any mechanism that changes
`csky_world_light` globally is wrong; the fix is deck-local. Per-population measurement (decision
8): `--tex-override` on `cloudlayer.tif`/`cloud1`/`cloud2` isolates mesh from sprites cheaply.

## C22 ☐ Implement the underside darkening

**Goal.** The deck underside measures 167 ± 10 at the CAP-12 pose; tops and interior stay within
their matched values.

**Evidence (confidence: direction sound, magnitude from C21).** Wrong-claim #4: underside only.

**Approach.** Per C21's mechanism — plausibly a two-sided material treatment on the deck tiles
(down-facing term dark, up-facing unchanged) applied where `WorldBuilder` builds `CloudDeck`.
Touch only the deck path.

**Model recommendation.** high — shader/material work with a tight numeric target.

**Verify.** The four CAP-12 boxes re-measured (underside/interior/tops/5570 ft); river-pose
underside region vs the original still; C4's deck (Sky1.tif) gets the same treatment — verify at
the CAP-12 C4 poses; 8-chapter regression.

**⚠ Traps.** The deck flips above/below at the cloud band and follows the player
(`GameSession`/`WeatherRig.Tick`) — test both sides of the flip; a fix keyed to face normals must
survive the deck's follow behaviour.

## C23 ☐ Re-measure the tops per population; blend the mesh↔sprite cut

**Goal.** The CAP-12 tops caveat resolved (mesh and sprite tops measured separately), and the
from-above boundary between deck mesh and sprite field blending as the original does — no hard
colour cut.

**Evidence (confidence: direction sound).** PT-42(a): ours shows a hard cut; the original blends.
The 211-vs-214 tops match sampled an unknown mix of the two populations (BL-118's ⚠).
Re-confirmed post-Wave-A at the controls (user, 2026-08-08, C1C): "the color difference between
fogvol puffs and the cloud deck texture is jarring. many hard lines" — the scatter fixes did not
touch it, as expected; it is this item's target.

**Approach.** Per-population boxes via `--tex-override` isolation at the above-deck pose; whichever
population is off gets the correction (sprite vertex colours are authored 240/240/240 — data first,
per ground rules). If the cut survives matched brightnesses, the blend is geometric (sprite bases
vs sheet altitude — Wave A's anchor) — re-check A3's numbers before inventing a shader blend.

**Model recommendation.** high — two interacting populations, easy to fix the wrong one.

**Verify.** Above-deck pose: per-population boxes within ±10 of the original's respective regions;
the boundary region shows no step in a horizontal luminance profile.

**⚠ Traps.** A claim about one population is not evidence about the other — keep the vocabulary
(`BL-118`'s note) in every measurement label.

## C24 ☐ Final match: both stills within the bar; mint PT + night-moonlit BL; close BL-118

**Goal.** The plan's exit: both reference views reproduced at the pinned poses and measured within
the match bar; the follow-ups minted; `BL-118` closed and the plan archived.

**Evidence (confidence: n/a — the instrument).** Everything above.

**Approach.** Shoot both poses (fly-mode framing near the pinned freecam coords is fine — HUD/plane
excluded by the boxes); tabulate every region pair here. Mint via `New-ItemId.ps1`: one PT item
(at-the-controls verdict: mottling character, no comb, no cut, deck-follow behaviour in motion) and
one BL for the night-moonlit puffs (CAP-11 C1B evidence pointer, decision 2). Delete `BL-118` from
the backlog; re-pin goldens; complete-banner + archive the plan per `plans.md`.

**Model recommendation.** medium — measurement, minting, bookkeeping.

**Verify.** The final region table (every row within ±10 or explained); `.\RunTests.ps1` green;
PROJECT_CONTEXT "Current status" swapped to the next work with this plan archived.

**⚠ Traps.** If any region can't reach the bar without touching another wave's landed mechanism,
that's a finding — record it and mint an item; do not re-open a landed wave inside the close-out
commit.
