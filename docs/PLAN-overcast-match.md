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
  `x -7323 y 1192 z -3829`; river **`x -7323 y 192 z -3829`**. View direction was NOT in the
  overlay; re-derived by `A4` by matching terrain features (cliff, river S-curve, ridge treeline)
  against the checked-in twin: river **`-0.997,-0.1,0.070`**, converged via an 18-shot sweep and a
  terrain-only pixel-diff metric (`A4`'s own section has the full derivation and probe set).
  Above-deck has no terrain in either frame to match against, so `A2`/`A3`'s own `0,0,-1` stands.
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
4. ☑ A4 — Scatter A/B vs CAP-12 + the river twin; close `BL-312`
5. ☑ A5 — Continue the field past the map edge (user playtest 2026-08-08: the original's field is everywhere)
6. ☑ A6 — Why does flight mode show puffs below the deck when freecam doesn't? (investigate, then fix or reclassify)
7. ☑ A7 — The deck is engine trickery: regime model + cloud layer gate (user decode, 2026-08-08) **Wave A complete.**

### Wave B — fog semantics and zones (BL-100 + BL-303 + BL-101)

11. ☑ B11 — All-chapter zone-table survey; test H1–H4 on paper
12. ☑ B12 — Dome-identity discriminator at the above-deck pose (**C1/C1C/C2B/C4 = `zone2`**; the `zone_id` census retired as evidence)
13. ☑ B13 — Footage discriminators — settled at the controls: H2 (fragment fade) confirmed, no capture needed
14. ☑ B14 — Implement the winning fog model — **nothing to implement**; the shipped fragment fade IS the original's, landed as the `weather.md` corrections it owed
15. ☑ B15 — `fogRangeFactor` **deleted** (authored ranges unscaled) and the range fade is a **linear** ramp, per the gamez's own `fog_state == 1`; the river-pose residual is measured to be the deck's own brightness, not the fog
16. ☑ B16 — The dome's authored `fog: false` — landed, the primary fix for BL-303
17. ☐ B17 — Fog A/B at both reference poses + BL-303's three; close `BL-303`/`BL-100`/`BL-101`
18. ☑ B18 — The dome's cap/skirt seam: the skirt is painted `FOG_COLOR`, and we were applying it twice

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
assignment beside `WeatherRig.cs`/`GameSession.cs`). Inside Wave B — **re-ordered by B11's verdict (2026-08-08)**: B11 ☑ → **B12** (rules the
zone1-vs-zone2 data conflict; runs BEFORE B13's CAP is minted, because it decides whether two of
B13's three discriminators exist) → **B16 promoted** (the dome's authored `fog: false` is the
PRIMARY fix for BL-303 — B11's arithmetic: every authored dome tops out +982…+4108 m above the
camera while the broken scenes' fog bands sit at 9000–11000 m, so the altitude term is 1.0 on
every dome fragment under H1 AND H2; runs before B14, and **B14 must not be tuned to make
BL-303's skies come out right**) → B13 (footage + CAP) → B14 → B15 → B17. B15 note from B11:
`VIEWING_RANGE` `FOG_SCALE`/`CLIP_SCALE` are 1.0 in all eight chapters — `fogRangeFactor` 2.0
has no data support; and the original's `World` nodes declare `fog_type: "Linear"` (low
confidence) against our smoothstep. *(Both landed in B15; the "low confidence" was raised —
`fog_state` is a written `1` that the reader asserts, and `0` there would be OFF.)* ⚠ **B15 hands
Wave C a re-measured target and a warning**: the deck's underside is **213.9 against the
original's 167.7** on the landed build (`CAP-12`'s own box, re-shot), and removing the range factor
made the ceiling *look* worse (205.6 → 214.8 at `CAP-12` `t8.0`) because that factor had been
hiding the deck's brightness — so C21/C22 own the river pose's remaining fog symptom too. Inside Wave C: C21
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

## A4 ☑ Scatter A/B vs CAP-12 + the river twin; close BL-312

**Landed.** (2026-08-08) Matched shots at CAP-12's three grazing altitudes plus both pinned
reference poses, all on the post-`A7` build (the wave's last landed item). No lattice at any
grazing angle, no field edge within reach of either pinned pose, density now in eyeball-parity
with CAP-12 (measured below), and the transition-depth gap A3 found stands **reconfirmed
independent of A5/A6/A7 too** — not a placement property, carried forward as a lead. The river
pose's view direction is re-derived by terrain matching and recorded below for reuse. The six
cloud-field goldens are re-pinned, `.\RunTests.ps1` exits 0, `verification.md` gains three
instrument rules, and `BL-312` is closed. No engine-code file was touched — this item is pure
measurement and bookkeeping, as scoped.

**Files.** `docs/PLAN-overcast-match.md` (this section), `docs/verification.md` (SHOT-20, SHOT-21,
INSTR-12), `backlog.md` (`BL-312` deleted), `analysis/goldens/manifest.json` (6 hashes re-pinned).

### Derived view directions — for every later item to reuse

The plan's own data (`What the data actually ships`) flagged that the pinned poses' altitudes were
corrected in `A7` but that view direction was never recovered from either freecam overlay. Both
twins in `Z:\CSVM\Screenshots\` are OUR OWN prior freecam captures at these exact positions (same
engine, same terrain, unaffected by anything this plan changes), so re-deriving direction is a
same-engine matching problem, not a cross-renderer one: render candidate directions at the pinned
position and converge on the one whose terrain silhouette matches the checked-in twin.

- **River pose (`x -7323 y 192 z -3829`) — direction ≈ `-0.997,-0.1,0.070`** (yaw ≈ 94° off due
  `-Z`, leaning toward `-X` with a slight `+Z` component; pitch ≈ 5.7° down). Found by an 18-shot
  sweep (`.scratch/a4/river-*.png`) against `Screenshots/C1 IA1 Fog river.png`'s cliff, river
  S-curve and ridge treeline; converged via a terrain-only pixel-diff metric (rows 330–720, mean
  abs diff over RGB) minimised at yaw ≈ −94°: −90° 34.5, −93° 30.0, **−94° 29.6**, −95° 31.1, −97°
  35.3, −100° 41.6, with pitch 0.06/0.14 both worse than 0.10 at fixed yaw. At the converged
  direction the residual diff is confined to a few pixels of silhouette antialiasing on tree/cliff
  edges — terrain pixels away from an edge are at or near zero difference
  (`.scratch/a4/river-T-neg94.png` vs the twin, `.scratch/a4/diff_N.png` for the intermediate
  −95° check showing the same pattern). See `.scratch/a4/compare_N.png` for the stacked
  side-by-side.
- **Above-deck pose (`x -7323 y 1192 z -3829`) — direction kept at `0,0,-1`** (A2/A3's own
  convention). Terrain matching is **not possible** here: both the twin and the original still
  show nothing but cloud tops/dome — no landmark on either side of the fog to lock onto. Since
  both poses share the exact same `x/z` (only `y` differs — the freecam moved straight up between
  shots), reusing the river pose's derived azimuth was considered, but `0,0,-1` is already the
  literal, unambiguous value A2/A3 used and re-confirmed (`A3`: "the pinned above-deck pose IS the
  literal documented ... direction 0,0,-1"), and nothing about this pose's content (dome gradient,
  star field, whiteout band) is azimuth-sensitive in a way that would prefer one heading over
  another. Kept as-is rather than replaced with an unverifiable guess.

### Matched shots — no lattice at any grazing angle, on the final build

`--freecam --chapter=C1 --det --mute`, HUD-free rows for the CAP-12-altitude set:

| pose | pos | direction | file |
|---|---|---|---|
| grazing tops (t124, 1208 m) | `-4974,1208,-3861` | `-1,0,0` (CAP-12 README's own convention) | `a4-grazing-tops-1208.png` |
| above deck (t44, 1527 m) | `-4974,1527,-3861` | `-1,0,0` | `a4-above-deck-1527.png` |
| high above (t97, 1698 m) | `-4974,1698,-3861` | `-1,0,0` | `a4-high-above-1698.png` |
| base first wisps (t29.2, 997 m) | `-4974,997,-3861` | `-1,0,0` | `a4-base-first-wisps-997.png` |
| pinned above-deck | `-7323,1192,-3829` | `0,0,-1` | `a4-pinned-abovedeck-1192.png` |
| pinned river | `-7323,192,-3829` | `-0.997,-0.1,0.070` (derived above) | `a4-pinned-river-192.png` |

All three grazing/above shots (1208/1527/1698 m) read as soft continuous mottling with no
periodic structure at any angle — the only discrete round shapes are the same two `cloudparent`
clusters A2 already identified as a different, untouched population, not a scatter artifact.
**No field edge is reachable from either pinned pose**: A5's map-edge extension only matters
within `far_fade` (3,500 m) of the rim, and both pinned poses sit ~3,829–3,861 m from the nearest
edge — past the fade, exactly as A2/A5 predicted, so this criterion is structural, not something
this item had to re-derive.

**The at-the-controls half of the verdict, quoted alongside the measured numbers above, per
decision 8's "plus a PT verdict for what numbers can't judge":** the wave-gate playtest (user,
2026-08-08, in engine) confirmed the lattice/comb **gone** at grazing angles — matching every
matched shot above showing no periodic structure — and, after `A7` landed, the user's re-fly
verdict was **"it looks a lot better. approved."** PT-42(c)'s **"a lot denser"** is the qualitative
density reference the 39.8 %/45.5 % coverage numbers below answer directly.

**The river pose is the headline result.** `a4-pinned-river-192.png` shows a flat gray ceiling
with **no cauliflower lumps hanging below the deck at all** — A3's original target, now landed by
a *different* mechanism than A3 built: at `y=192`, well under C1's 1047 m band centre, `A7`'s
below-band regime culls the entire `fvol` field (plus `cloudparent`) for this camera and carries a
camera-following ceiling instead, so there is nothing left to hang below the sheet. This matches
the original still's flat mottled underside far better than a "fixed" scatter placement ever could
on its own.

### Density vs CAP-12 — same instrument both sides

Sheet-region coverage, adaptive per-image threshold (`.scratch/a4/density.py`, `frac=0.55` of each
box's own P2–P98 range) rather than an absolute luminance cut — chosen specifically because
CAP-12 is a compressed, graded video capture and ours is a raw pixel-buffer screenshot, and
comparing the two on an absolute scale would read the *grading*, not the *cloud* (verification.md
**SHOT-1**, and the same caveat B13's trap already states for luminance across capture sources).
Box = rows 320–405, cols 280–1000 (HUD-free, below the compass, above the plane/gauges), the same
box on both sides for the CAP-12-altitude set:

| pose | CAP-12 original | ours | note |
|---|---|---|---|
| t124 / grazing tops 1208 m | **39.8 %** (mean 191.4, sd 12.0) | **45.5 %** (mean 199.4, sd 20.9) | eyeball-parity — see below |
| t97 / high above 1698 m | **80.2 %** (mean 171.6, sd 6.4) | **100.0 %** (mean 176.0, **sd 0.0**) | ours is a flat plate here, not a density deficit |
| t44 / above deck 1527 m | 15.3 % (mean 73.9) | 3.4 % (mean 176.8) | **framing-limited, not comparable** — A1 already flagged this pose's tops boundary sits below the fixed row band on both sides; the two boxes sample different features (dome/sky, not the sheet) |

**t124 is the clean read: 39.8 % vs 45.5 % is the same order of magnitude — eyeball parity.**
This is the direct answer to PT-42(c)'s "a lot denser," measured on the *pre-A2* lattice build:
after A2/A3, sprite count and mean spacing were never touched (129.3 m against the authored 130),
so this result is exactly what the wave's own acceptance table always predicted — density was
never the defect, regularity was.

**t97's 100.0 %/sd 0.0 is not a second density problem — it is the transition-depth finding,
restated in coverage terms.** Moving the same box deeper into our own established plate (rows
420–500) at 1208 m drops sd to 1.4 and coverage to 11.9 % against a much narrower dynamic range
(p2=220.1, p98=226.9) — i.e. once you are past our razor-sharp edge there is almost nothing left
to threshold. The original never fully flattens this way inside the visible frame at either
altitude. This is the same 91–104 px transition-depth gap already on file, seen through a
different instrument, not an independent finding.

### Transition depth — final numbers on the post-A7 build

`.scratch/transition_depth.py` (A2's instrument, unmodified), median band(31)/edge(15) px over
HUD-free 21-column blocks:

| pose | band / edge (px) | original reference |
|---|---|---|
| pinned above-deck `-7323,1192,-3829` | 46.5 / **7.0** | — |
| grazing tops 1208 m (`t124`) | 43.0 / **7.0** | `t124` **103** |
| above deck 1527 m (`t44`) | 25.0 / 24.5 | `t44` 33 (framing-limited, A1) |
| high above 1698 m (`t97`) | 24.0 / **24.0** | `t97` **101** |

The pinned above-deck reading (46.5/7.0) is bit-for-bit the same as `A3`'s own post-number at the
literal recorded pose — confirming nothing has moved this metric there since `A3` landed, through
`A5`/`A6`/`A7`. The other three poses use `-1,0,0` (CAP-12's own convention) rather than `A2`'s or
`A3`'s untraceable directions (`A3`: "A2's own X/Z/direction for these three were not preserved
anywhere on disk"), so they are a fresh, internally consistent matched set, not a row-for-row diff
against either predecessor — and they land in the same range A2 itself reported for the same
altitudes.

**The 91–104 px gap between the original and ours (13–25 px at these poses) is a KNOWN OPEN
DELTA, reconfirmed here as independent of every Wave A mechanism** — horizontal placement (`A2`),
vertical placement (`A3`), map-edge extension (`A5`, inert at both pinned poses per its own
handover note), the deck-altitude fix (`A6`), and the regime/gate model (`A7`) all left this number
in the same ballpark. **It is NOT a Wave A acceptance failure** — the wave's own criteria never
asked for a byte-match transition depth, only no lattice, no field edge, and CAP-12-like density,
all three of which are met above. Per `A3`'s own handover, the gap is carried forward as a lead
for a future item — per-card alpha falloff/scale distribution, or Wave B's fog participation in
the sheet's apparent softness — and is judged next whenever that item is minted and picked up, not
here.

### 8-chapter freecam regression

`--freecam --det --mute --chapter=<X>` for all eight chapters (`.scratch/a4/regress-*.png`,
logs under `.scratch/logs/`): **zero errors in all eight** (`ERROR` greps clean across every
`.log`/`.out`). Census lines match `A5`'s own final table exactly, confirming nothing drifted
since: `fogvol clouds:` C1/C2B/C4 22,201 (9,025 base + 13,176 extension), C1C 22,748 (9,572 +
13,176), C5 16,170 (16,170 + 0); `cloud clusters:` C1 28, C1B 70, C1C 30, C4 45; `cloud deck:` 144
tiles at y=960 (C1/C1C/C2B) / y=1050 (C4). C1/C4's vertical criteria from `A3` are reconfirmed by
the river-pose result above (nothing hangs below the deck) and by this census holding steady
through the full sweep; a dedicated C4 clear-air re-shoot was not repeated here since `A5`/`A6`/`A7`
each already reconfirmed it inert to their own changes and nothing in `A4` touches placement.

### Goldens re-pinned

Ran `.\RunTests.ps1` unregenerated first (per `analysis/goldens/README.md`'s own procedure) to
confirm the moved set before touching anything: **exactly the same six goldens every prior Wave A
item moved — `c1-waterfall`, `c1c-rain`, `c2b-rain`, `c4-snow`, `c1-flight`, `c1-destroy-effects`
— 0 broken of 13, 0 unexpected movers.** Build PASS (0 warnings), units 669/669, engine 26/26
clean. Only then ran `.\RunTests.ps1 -SkipUnits -SkipEngine -RegenGoldens`; `git diff
analysis/goldens/manifest.json` confirms **only those six `hash` lines changed** — no `args`,
`exercises`, `frame`, or `gpu` field touched, no golden outside the six moved. Re-ran
`.\RunTests.ps1` in full afterward: **build PASS, units 669/669, engine 26/26 clean, goldens
13/13 hash-identical, exit 0.**

### `verification.md` — three instrument rules from the wave

Checked none of the three already existed (grepped the wording, not just an ID) before adding:

- **SHOT-20** — the sky→tops transition-depth metric replaces column-autocorrelation/crest-spacing
  for cloud-sheet structure (perspective aliases a fixed world period on screen — `A2`'s finding).
- **SHOT-21** — `--tex-override` cannot separate `cloudsprite` from `cloudparent` — both skin
  `cloud1.tif`/`cloud2.tif`; separate by altitude or position instead (`A6`'s finding).
- **INSTR-12** — a straight-up billboard probe reads edge-on and carries no altitude information
  (`A6`'s correction of `A3`'s own probe, an `INSTR-11` compound-thing narrowness).

### `BL-312` closed

Deleted from `backlog.md` per convention (the record lives in this section and the closing
commit's message, drafted at `.scratch/commit-msg-a4.txt`). Its three threads: the lattice comb
(landed `A2`), the "reads far denser" verdict (landed `A2`/`A3`, reconfirmed above — density is now
eyeball-parity, not "a lot denser"), and unbounded tiling over the base map (landed `A5`). **No
thread is left dangling**: the one open question this item surfaces — the transition-depth gap —
was never part of `BL-312`'s own claim (density and lattice, not sheet sharpness); it is `A3`'s
finding, already carried in this plan, not a new backlog item. `grep`ping `BL-312` across the repo
after deletion returns only narrative mentions in `backlog.md`'s `BL-118` entry ("Both halves moved
to `BL-312`") and this plan's own history sections — both are history, not live claims, per the
`close-backlog-item` convention (compare `BL-273`, referenced the same way after its own closure).

### Probe images (`.scratch/a4/`)

| file | what it is |
|---|---|
| `river-*.png` (A–V, yaw000, neg80/90/91/92/93/94/95/96/97/100, 180) | the 18-shot direction-derivation sweep |
| `river-T-neg94.png` | **the converged river direction** — compare directly to `Screenshots/C1 IA1 Fog river.png` |
| `compare_N.png`, `diff_N.png`, `J_cliff_zoom.png` | stacked and diffed comparisons used to converge the direction |
| `a4-grazing-tops-1208.png`, `a4-above-deck-1527.png`, `a4-high-above-1698.png`, `a4-base-first-wisps-997.png` | the CAP-12-altitude matched set |
| `a4-pinned-abovedeck-1192.png`, `a4-pinned-river-192.png` | the two pinned reference poses on the final build — the river one is the item's headline evidence |
| `regress-C{1,1B,1C,2,2B,3,4,5}.png` | the 8-chapter regression sweep |
| `density.py`, `transition_depth.py` | the instruments (density.py new to this item; transition_depth.py is A2's, unmodified) |
| `runtests-pre-regen.log`, `runtests-regen.log`, `runtests-post-regen.log` | the three `RunTests.ps1` runs bracketing the golden re-pin |

### Original approach (kept for reference)

**Goal.** The wave's acceptance criteria measured and recorded; `BL-312` deleted from the backlog.
— *met, see Landed above.*

**Evidence (confidence: n/a — this is the instrument).** CAP-12 stills + the pinned river pose;
PT-42(c)'s "a lot denser" as the density reference. Wave-gate playtest (user, 2026-08-08, in
engine): lattice/comb at grazing angles **confirmed gone**; puffs below the deck **still seen in
flight** (→ `A6`, **landed 2026-08-08** — the deck was pinned 87 m above its authored altitude,
not a scatter fault; C1/C1C/C2B pixels moved a third time, so re-pin against A6's build);
field ends at the base map where the original's is everywhere (→ `A5`) — A4
closes the wave only after `A5` lands.
— *A5, A6 and A7 all landed before this item; the wave-gate playtest's density half ("a lot
denser") is answered above with a number, not just a qualitative callback.*

**Approach.** Matched shots at the CAP-12 grazing poses and both pinned poses; a density comparison
(sheet-region sprite coverage vs the original's) recorded here; re-pin the goldens
(`analysis/goldens/manifest.json`) since the field changed in every fvol chapter. Close `BL-312`
per convention (record in the landing commit; traps → `fogvol.md`/`verification.md`).
— *followed as written; "the CAP-12 grazing poses" resolved to CAP-12's own README convention
(`-1,0,0` at `-4974,-3861`) rather than A2's/A3's untraceable directions, and the density
comparison used an adaptive per-image threshold rather than a fixed luminance cut, for the
video-vs-engine reason SHOT-1 states.*

**Model recommendation.** medium — measurement and bookkeeping against stated criteria.
— *held; no engine code was touched.*

**Verify.** No lattice at any grazing angle; no field edge over the base map; density within
eyeball-parity of CAP-12 (state the measured coverage numbers); C1/C4 vertical criteria from A3
re-confirmed. Full 8-chapter freecam regression, zero errors.
— *all met, see the sections above.*

**⚠ Traps.** `--tex-census` counts are lower bounds and pair with `--no-fog` (cli.md) — if used for
density, say so and use the same flags on both sides of any before/after.
— *not used here — the density comparison used pixel-luminance coverage instead, precisely because
`--tex-census` has no equivalent on the CAP-12 (video) side, so it could not have been the
same-instrument-both-sides measure the trap itself asks for.*

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
   — *`B15`'s verdict (2026-08-08): **half right.** Deleting the factor moved the reach from row
   165 to **236** (with the same instrument re-run; the curve change is worth ~3 rows), but the
   rest is **not fog at all** — the original's ceiling reads 166–175 in that still while ours
   renders 200–220 with `--no-fog`, so the remainder is the deck's own +54 underside brightness
   (`BL-118`, Wave C). B15 also found the raw px numbers compare two differently-pitched frames:
   at this pose terrain ends our sky ~17 px above the true horizon, so row 330 is unreachable
   whatever the fog does — see `SHOT-23`.*

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

## B11 ☑ All-chapter zone-table survey; test H1–H4 on paper

**Landed.** (2026-08-08) Paper only — no engine code, no `docs/formats/` page and no `backlog.md`
entry touched. The survey instrument is `analysis/fog-zone-survey/survey_zones.py` (+ its
`FINDINGS.md`); every number below is reproducible from it and from
`extracted/*/gamez/nodes.json`.

### VERDICT

**H3 dies. H4-as-a-mission-zone-selector dies. H1 and H2 both survive — and the survey shows they
are observationally IDENTICAL in seven of the eight chapters, so the wave's real fork is not H1 vs
H2 at all.**

Three results, in order of how much they change the wave:

1. **BL-303's three scenes are not a fog-semantics bug. They are the dome's authored `fog: false`
   (item `B16`).** Every authored skydome tops out **+982 m to +4108 m above the camera** (measured
   from the gamez, table 3); every one of those three scenes flies a zone whose `FOG_ALTITUDE` band
   is **9000–11000 m**. So the altitude term is 1.0 on *every* dome fragment under H1 **and** under
   H2, the range term saturates at the dome's 6.4–21.8 km radius, and both hypotheses predict a sky
   painted flat in the authored fog colour: C3 → 201, C2B `zone2` → 176, C5 → 0. Our renders measure
   **201.0 / 176.0 / 0.1** (`CAP-11`) — the model reproduces our own bug to the unit. The originals
   measure **194.9 blue-gradient / 82.7 / 15.3**. No altitude fade can close that gap at the
   original's own dome size, so the dome must not fog. `ForceFogged` is the single cause of all
   three, and of the C1 above-deck gray band as well.
2. **`FOG_ALTITUDE` is inert in seven of the eight flown zones** (INSTR-7 — a degenerate census is
   a fact about the instrument, not the question). The only reachable band in a *settled* zone in
   the whole install is **C2 `ZONE1`, 256–1024 m**. That is the one and only place H1 and H2 can
   differ from each other on evidence, and C2 has no cloud deck to block the view down. **C2 is the
   H1/H2 discriminator; nothing else is.**
3. **`fogvol.zrd`'s `fog_zone` is a 0-based index into the weather file's own zone list, naming the
   zone a volume's INTERIOR uses.** C5's `fog_zone` 1 → `ZONE3`, and C5's `fogvol.zrd` `fog_color`
   `[16,16,16]` is **byte-identical to `ZONE3`'s `FOG_COLOR`**. H4 survives — not as a competitor to
   H1/H2, but as an **additive local override** that composes with either, and it is the only
   mechanism in the matrix that explains the user's C5 sighting at all. Wrong-claim #6 is
   **unchanged and confirmed**: no chapter contradicts "`fog_zone` is not the mission-zone selector"
   (see the sign check below) — the decode is not re-opened, only the thing the index points *into*
   is now named.

⚠ **The survey also turned up a hard conflict about which zone C1/C1C/C2B/C4 fly, and B12 must
resolve it** — see "The zone-choice conflict" below. It changes B12's brief and it is the one thing
here that could still move B14.

### Table 1 — every chapter × zone (IA1; `IA1` exists for all eight, nothing substituted)

Colours through the reader (`Weather.ParseColor`, divide by 255 **iff any component > 1**), shown
`raw → byte`. `WorldLight = clamp(AMBIENT + DIFFUSE·0.46, 0.15, 1)`.

| chapter | zone | `FOG_COLOR` | `FOG_RANGES` | `FOG_ALTITUDE` | `CLIP_RANGES` | DIFFUSE / AMBIENT (WorldLight) |
|---|---|---|---|---|---|---|
| C1 | ZONE1 | `0.69³` → **176³** | 1000 – 1750 | **970 – 1047** | 5 – 2050 | 1.2 / 0.25 (0.802) |
| C1 | ZONE2 | `0.69³` → **176³** | 1000 – 4000 | 4000 – 5000 | 5 – 4500 | 1.2 / 0.25 (0.802) |
| C1B | ZONE1 | `[.063,.094,.188]` → **(16,24,48)** | 1000 – 4700 | 10000 – 11000 | 5 – 5000 | 0.6 / 0.15 (0.426) |
| C1B | ZONE2 | `[.063,.094,.188]` → **(16,24,48)** | 1000 – 4500 | **1128 – 1256** | 5 – 5000 | 0.6 / 0.15 (0.426) |
| C1C | ZONE1 | `0.69³` → **176³** | 1000 – 1750 | **1055 – 1082.5** | 5 – 2050 | 0.4 / 0.6 (0.784) |
| C1C | ZONE2 | `0.69³` → **176³** | 1000 – 4000 | 4000 – 5000 | 5 – 4300 | 0.4 / 0.6 (0.784) |
| C2 | ZONE1 | `[.804,.843,1.0]` → **(205,215,255)** | 2100 – 2400 | **256 – 1024** | 5 – 2600 | **1.1 / 0.5 (1.000)** |
| C2 | ZONE2 | `0.69³` → **176³** | 1000 – 4000 | 9000 – 10000 | 5 – 4500 | **0.4 / 0.6 (0.784)** |
| C2B | ZONE1 | `0.69³` → **176³** | 1000 – 1700 | **924 – 1024** | 5 – 2050 | 0.4 / 0.6 (0.784) |
| C2B | ZONE2 | `0.69³` → **176³** | 1000 – 4000 | 9000 – 10000 | 5 – 4300 | 0.4 / 0.6 (0.784) |
| C3 | ZONE1 | `0.79³` → **201³** | 1000 – 4500 | 9000 – 10000 | 5 – 4800 | 1.5 / 0.3 (0.990) |
| C3 | ZONE2 | `[.063,.094,.188]` → **(16,24,48)** | 1000 – 4500 | 9000 – 10000 | 5 – 5000 | 1.5 / 0.3 (0.990) |
| C4 | ZONE1 | `192³` → **192³** | 500 – 4500 | 10000 – 11000 | 5 – 4800 | 1.5 / 0.5 (1.000) |
| C4 | ZONE2 | `192³` → **192³** | 1000 – 4500 | 10000 – 11000 | 5 – 4800 | 1.5 / 0.5 (1.000) |
| C5 | ZONE1 | `[0,0,0]` → **0³** | 1500 – 2250 | 9000 – 10000 | 5 – 2500 | 1.5 / 0.5 (1.000) |
| C5 | ZONE3 | `[16,16,16]` → **16³** | **50 – 250** | 9000 – 10000 | **5 – 300** | 1.5 / 0.5 (1.000) |

`VIEWING_RANGE` is identical in all eight: **HIGH `CLIP_SCALE` 1.0 / `FOG_SCALE` 1.0** (MED 0.85,
LOW 0.7) — so at HIGH detail the authored ranges are the ranges, and `B15`'s `fogRangeFactor` 2.0
has no support anywhere in the data. `SUNLIGHT_ORIENTATION` and every other `SUNLIGHT_*` key are
**identical between a chapter's two zones in seven of eight chapters**; C2 is the sole exception
(above), which is why brightness can only ever test the zone choice in C2 — and `CAP-11`'s C2 clamp
point (suburb 1.05–1.15 at WorldLight 1.0) already matches `ZONE1`, not `ZONE2`'s 0.784.

**Cross-mission check** (all 53 files): only **C1** and **C4** vary anything, and both vary
**`ZONE1`'s `FOG_RANGES` per mission** (C1: IA1 1000–1750, M02/M04 1700–2000, M05 1900–3000, MP
900–1550; C4: IA1/MP 500–4500, M01–M05 750–3000) while leaving `ZONE2` at a constant 1000–4000 /
1000–4500. `FOG_ALTITUDE` never varies within a chapter except C2's `ZONE2` (9000–10000 in six
missions, 4000–5000 in MP2/MP3). **`ZONE1` is the per-mission-tuned zone; `ZONE2` is boilerplate.**

### Table 2 — per chapter: what we fly, what is settled, the bands and the volumes

| chapter | zone flown today | settled verdict | `CLOUD_COVER` (centre) | `fvol` slab band | `fvol` `zone_id` | `fogvol` `fog_zone` | horizon meshes z1 / z2 (z3) |
|---|---|---|---|---|---|---|---|
| C1 | `zone2` (default) | **open** (BL-100) | 970 – 1124 (**1047**) | 970 – 1090.55, 9 vols | **2** | **0** | 2 / 4 |
| C1B | `zone1` (PreferPopulated) | **zone1** | 10000 – 11000 (10500) | *none* | — | *absent* | 4 / **0** |
| C1C | `zone2` (default) | **open** | 1055 – 1110 (**1082.5**) | 970.73 – 1688.05, 21 vols | **2** | **0** | 1 / 4 |
| C2 | `zone1` (PreferPopulated) | **zone1** | 19024 – 20124 (19574) | *none* | — | *absent* | 3 / **0** |
| C2B | `zone2` (default) | **open** | 924 – 1124 (**1024**) | 970 – 1090.55, 9 vols | **−1** | **0** | 1 / 2 |
| C3 | `zone1` (PreferPopulated) | **zone1** | 10000 – 11000 (10500) | *none* | — | *absent* | 3 / **0** |
| C4 | `zone2` (default) | **open** | 1000 – 1100 (**1050**) | 1060 – 1180.55, 9 vols | **2** | **0** | 1 / 4 |
| C5 | `zone1` (ResolveZone fallback) | **zone1** (user A/B) | 9950 – 10150 (10050) | −463 – 183, 17 vols | **1** | **1** | 2 / — (1) |

C5's `fogvol.zrd` additionally authors `fog_fade_dist` **16.0**, `interior_fog_fade_dist` **16.0**,
`fog_color` **`[16,16,16]`** (and `distance` 80; the four deck chapters carry `distance` 130, the
three deckless ones a vestigial 206.25 with no `fog_zone` at all).

⚠ **`zone1`'s `FOG_ALTITUDE` equals `[CLOUD_COVER BOTTOM, CLOUD_COVER centre]` in all three
chapters whose band is reachable** — C1 **970/1047**, C1C **1055/1082.5**, C2B **924/1024** — an
exact three-for-three identity against `WeatherState.CloudBandCentre`, the same midpoint `A7` made
load-bearing for the deck regime. C4 breaks the pattern (both its zones sit at 10000–11000 while its
band is at 1000–1100), and C2's 256–1024 is a low-level inversion layer with no deck to tie to.

### Table 3 — the domes, measured (why BL-303 is not about `FOG_ALTITUDE`)

`horizon/<zone>` subtree meshes, from each gamez's `model_bbox` (local, camera-anchored, before any
scale). "top" is the highest fragment the sky can put over the camera.

| chapter | zone | dome meshes | radius | local top | contents that identify it |
|---|---|---|---|---|---|
| C1 | zone1 | 2 | 8.8 km | **+2793** | `h_zone1scroll` + `o28` — a scrolling day band |
| C1 | zone2 | 4 | 8.74 km | **+2155** | `moon`, `stars`, `g1155`, `h_zone2scroll` — **night** |
| C1B | zone1 | 4 | **21.8 km** | **+4108** | `g1163`–`g1166`, incl. a ±5605 m moon sphere |
| C1B | zone2 | **0** | — | — | bare marker |
| C1C | zone2 | 4 | 8.74 km | **+2155** | `moon`, `stars`, `g1155`, `h_zone2scroll` — **night** |
| C1C | zone1 | 1 | 7.6 km | **+2375** | `g1164` (same model as C2B zone1) |
| C2 | zone1 | 3 | 8.49 km | **+2287** | `g1155`, `h_zone2scroll`, **`sun`** — day |
| C2 | zone2 | **0** | — | — | bare marker |
| C2B | zone2 | 2 | 8.74 km | **+1646** | `g1167`, `g1168` — the night shell, no moon/stars |
| C2B | zone1 | 1 | 7.6 km | **+2375** | `g1166` |
| C3 | zone1 | 3 | 8.74 km | **+1901** | **`sun`**, `g1155`, `h_zone2scroll` — day |
| C3 | zone2 | **0** | — | — | bare marker |
| C4 | zone2 | 4 | 8.74 km | **+2155** | moon sphere + star plane + shell — **night** |
| C4 | zone1 | 1 | **6.4 km** | **+982** | `h_zone2scroll` |
| C5 | zone1 | 2 | **12.0 km** | **+3767** | `moon` + `g1171` — night city |
| C5 | zone3 | 1 | — | — | one mesh on the zone node itself |

**The arithmetic that kills the fog-semantics reading of BL-303.** At the original's own (1×)
anchor the highest dome fragment anywhere in the install is +4108 m over the camera; at C3's
canyon pose (710 m) its dome tops out at **2611 m** against a 9000 m band floor — a factor of 3.4
short. Nothing in H1 or H2 can reach it. At our 2.5× scale the numbers reproduce our three broken
renders exactly: C3 1901×2.5 = +4753 → 5463 m at the pose, still under 9000 → **whole dome fogged →
flat 201** (measured 201.0); C2B zone2 1646×2.5 = +4116 → 5346 m at 1230 m, under 9000 → **flat 176**
(measured 176.0, and identical at 1230/1350/1500 m exactly as `CAP-11` reports); C5 3767×2.5 = +9417
→ only the apex clips into the 9000–10000 band, the visible near-horizon sky stays fully fogged →
**≈0** (measured 0.1). And C1's above-deck gray band is the same mechanism one zone over: at 1192 m
under `zone2`'s 4000–5000 band, 2155×2.5 = +5387 → the apex escapes above 5000 (the dome and stars
we render) while everything toward the horizon sits under 4000 and paints **flat 176** — *the gray
band wedged between dome and cloud tops is our fogged lower dome*, which is why the worktree's
un-fogged-dome probe removes it.

⚠ **So all four sky defects are one defect**, and `WorldBuilder.BuildHorizon`'s own comment names
the assumption that fails: "high dome fragments stay clear via the `FOG_ALTITUDE` fade" is only true
when the band sits *below* the dome, and it never does.

### The zone-choice conflict (B12 must resolve this; B11 does not)

Four independent data arguments, and they **disagree** for C1C/C2B/C4:

| argument | says | strength |
|---|---|---|
| **Dome content** — C1/C1C/C4's `zone2` carries `moon` + `stars`; all three IA1s are daylit (C1 sun −25°, C1C −65°, C4 −45° + SNOW), and both zones author identical `SUNLIGHT`, so the mission is lit as day either way | C1, C1C, C4 = **zone1** | strong, but a still could hide faint stars |
| **`ZONE1` is per-mission-tuned, `ZONE2` is boilerplate** (cross-mission check above) | the tuned zone is the flown one = **zone1** | suggestive |
| **`zone1`'s `FOG_ALTITUDE` == `[CC BOTTOM, CC centre]`**, 3/3, otherwise dead data | **zone1** | suggestive |
| **`zone_id` world census** — `-1` = always, `1`/`2`/`3` = that zone only, "alternative world variants" (`docs/HISTORY.md`); weather.md already settles C1B/C2/C3 on exactly this evidence | **zone2** for C1C (146 vs 1317 nodes), C2B (149 vs 1414), C4 (802 vs 2157) — and for C1 too, see below | strong for C1C/C2B/C4 |

**The `zone_id` census is new here and it is the sharpest of the four.** Meshed-node counts and what
they contain:

- **C1**: `zone_id 1` = 1,427 meshed nodes, all between −200 and +600 m — the ground world, its
  destroyables, lightpoles and guns. `zone_id 2` = 783 meshed nodes including **all 145 nodes at
  deck altitude (800–1000 m), all 28 `cloudparent` clusters, all 9 `fvol` volumes**, plus the
  moon/stars dome. **`zone_id 1` contains no node at deck altitude at all.** If `zone_id` gates
  content the way HISTORY records, a C1 mission flying `zone1` has **no cloud deck, no `fvol` field
  and no `cloudparent`** — which both C1 IA1 reference stills refute outright.
- **C1C** 145 meshed in zone1 vs 1,225 in zone2 (1,057 at ground, 145 at deck altitude, 13 above
  1200 m = the build-ups); **C2B** 146 vs 1,344 (1,198 ground + 145 deck); **C4** 479 vs 1,880
  (1,583 ground + 170 at 1000–1200 = deck + `fvol`). In C1C and C2B `zone1` is a near-empty variant.
- The four **settled** chapters agree with the census 4/4 (C1B 2,101 vs 2; C2 766 vs 1; C3 1,647 vs
  2; C5 1,555 vs 149 — flown zone always holds the world).

**Consequence if `zone2` wins:** C1's `FOG_ALTITUDE` becomes 4000–5000, unreachable under the
2500 m ceiling — and `weather.md`'s *only* corroboration of the `FOG_ALTITUDE` semantics ("zone1
970→1047 is exactly cloud-band-bottom → whiteout-centre") belongs to a zone C1 never flies. Then
**every** flown band in the install except C2's is unreachable and B14's H1-vs-H2 choice is a
C2-only question. **Consequence if `zone1` wins:** C1's band is reachable, the whiteout handover is
real, and H1 and H2 diverge at the milestone's own above-deck pose. Either way B16 still owns the
sky. DIAG-6 applies to the census (correlation is not a mechanism — nothing in `CSVM/src` reads
`zone_id`, so we have never observed it doing anything); B12 decides it at the render.

### The prediction matrix

Read "✓" as *reproduces the original*, "✗" as *contradicts it — the hypothesis dies here*, "=" as
*predicts the same picture as every rival, so the scene discriminates nothing*.

| scene | H1 camera fade | H2 fragment fade (current) | H3 altitude zone switch | H4 positional switch (exclusive) | H1+H3 | H1+H4 (additive) | H3+H4 |
|---|---|---|---|---|---|---|---|
| **(a) C5 = zone1** (user A/B, sees across the city) | = mult 1 at street level (band 9000) | = same | = switch at 10000 m, unreachable → zone1 | ✓ *outside* the strips → zone1 | = | ✓ | = |
| **(a) C5 street-volume collapse** (`CAP-11 C5 Flying into fog zone.mp4`) | ✗ no mechanism | ✗ no mechanism | ✗ both C5 zones share `FOG_ALTITUDE` 9000–10000 → **no switch point exists** | ✓ inside → `ZONE3` 50–250, `fog_color` == `ZONE3` colour | ✗ | ✓ | ✓ |
| **(a) C1B = zone1** | = band 10000–11000 | = | = switch at 11000, unreachable | ✗ **C1B ships no `fvol` at all** — H4 has no "inside" to select with | = | ✓ | ✗ |
| **(a) C2 = zone1** | = at every measured pose (see (c)) | = | ✗ **switch at `zone1`'s top 1024 m is REACHABLE, and C2's `horizon/zone2` has ZERO meshes** — above 1024 m the sky becomes a hole onto the clear colour | ✗ no `fvol` in C2 | ✗ | ✓ | ✗ |
| **(a) C3 = zone1** | = band 9000–10000 | = | = switch at 10000, unreachable | ✗ no `fvol` in C3 | = | ✓ | ✗ |
| **(b) C3 canyon murk** (orig near 36.5 / hills 19.9–60.3 / sky 194.9-blue; ours 130.4 / flat 201 / 201) | terrain: over-fogged by `fogRangeFactor` 2.0 + our smoothstep, **not** by the altitude term (mult = 1 either way) · sky: ✗ **flat 201 unless the dome is exempt** | identical to H1 — dome tops at +1901 m never reach the 9000 m band | ✗ same flat sky, and no reachable switch | ✗ no volumes | ✗ | ✗ (sky) | ✗ |
| **(b) C2B above-deck gray dome** (orig 82.7; ours flat 176.0 at 1230/1350/1500) | ✗ flat 176 unless the dome is exempt | ✗ same | ✗ **and worse**: above a 1024 m switch H3 hands C2B `ZONE2` (1000–4000, 176 gray, band 9000–10000) → **flat 176 at every altitude — literally our broken render** | ✗ above the 1090.55 m slab → the other zone → still 176 | ✗ | ✗ (sky) | ✗ |
| **(b) C5 black sky** (orig 15.3; ours 0.1) | ✗ unless exempt | ✗ unless exempt | ✗ | ✗ | ✗ | ✗ (sky) | ✗ |
| **(c) CAP-11 healthy: C1B spawn 55 m, C2 `dogfight_ace[4]` **160 m**, C2B spawn 200 m, C3 710 m, C4 deck, C5 street** | = **every measured pose sits below its zone's `FOG_ALTITUDE.low`, so the altitude term is exactly 1.0 at all of them** | = identical | = below every switch → the same zone we already fly | = / ✗ per the (a) rows | = | = | ✗ |
| **(d) C1 above-deck still — clear dark sky** | ✓ **iff C1 = zone1** (camera 1192 > 1047 → fog off entirely) · ✗ if zone2 | ✓ **iff C1 = zone1** (dome fragments all > 1047) · ✗ if zone2 | ✗ **unconditionally**: `ZONE1` and `ZONE2` carry the *same* 176 gray and far ranges 1750/4000, both ≪ the 8.7 km dome radius, so **no zone switch can change the sky's colour** | ✗ same reason | ✗ | ✓ iff zone1 | ✗ |
| **(d) C1 river still — fogged valley** | = camera 192 < 970 → full | = terrain < 970 → full | = | = | = | = | = |

**Named kills.**

- **H3 dies on C2** — settled `zone1`, switch point 1024 m is routinely flown, and `horizon/zone2`
  is a bare marker with **0** meshes, so H3 predicts a hole in the sky above 1024 m in a chapter
  `CAP-11` filmed. This kill is *unconditional*: it needs only that the dome exists, not that it
  fogs. Corroborated by **BL-303's own C2B case**, where H3's above-the-switch prediction (flat 176
  from `ZONE2`) is exactly the render the item was filed against. And H3 has no explanatory power
  where it survives: in C1/C1C/C2B/C4 both zones ship identical `FOG_COLOR`, so switching cannot
  change the sky at all.
- **H4-as-exclusive-selector dies on C1B, C2 and C3** — three chapters, two of them settled, ship
  **zero** `fvol` volumes, so "inside vs outside" has no inside anywhere and cannot select a zone.
  It also dies on **(d)**: above C1's 1090.55 m slab top it hands the above-deck pose the other
  zone's identical gray.
- **H1+H3 and H3+H4 inherit H3's kill.** Dead.
- **H1 and H2 survive**, and **H1+H4 / H2+H4 (additive) are the two live candidates.**

⚠ **No CAP-11 healthy scene discriminates anything** — verified rather than assumed: C2's
`dogfight_ace[4]` spawn is `(-6862, **160**, -4335)` (`extracted/C2/IA1/zrdr/ia.zrd.json`), below
`ZONE1`'s `low` of 256, so H1's camera term is 1.0 there and cannot have been falsified by the
matching suburb numbers. Do not cite the healthy set against H1.

⚠ **BL-303's three scenes discriminate H1 from H2 not at all** — they discriminate whether the
*dome* participates in fog. Re-classify them onto `B16`.

### What settles the survivors — the exact discriminators

The universal statement: **under H2 nothing about the fog can change when only the camera's
altitude changes; under H1 everything does.** So any frame pair of the *same subject at the same
horizontal distance* from two camera altitudes straddling a reachable band settles it.

- **B13, discriminator 1 (unconditional, and the only one that needs no zone verdict): C2 above
  1024 m looking down.** Settled `zone1`, band **256–1024**, ranges 2100–2400, colour
  **(205,215,255)**, and no cloud deck to block the view. H1 → above 1024 m the fog is **off**:
  ground reads its unfogged luminance out to the 2600 m clip. H2 → ground fragments sit below 256 m
  → multiplier 1 → everything past ~2400 m horizontal washes to pale blue. The two differ by
  ~100+ luminance units on distant ground. **Frames needed:** `CAP-11 C2.mp4` (0:59.36) with the
  altimeter above **≈3360 ft**, holding a forward-and-down city view. If the clip never climbs that
  high (all eight C2 `dogfight_ace` spawns are 55–277 m, so it may not), **mint a CAP**: climb over
  the LA basin from 500 ft to 5000 ft holding one ground feature in frame, then level and pan
  ground↔horizon. That single take settles H1 vs H2 for the whole install.
- **B13, discriminator 2 (conditional on B12 landing `zone1` for C1): C1 valley through a deck gap
  from above 1047 m.** H1 → the valley clears; H2 → it stays washed to 176. `CAP-12`'s six deck
  crossings may already hold it. **If B12 lands `zone2`, this discriminator does not exist** —
  C1's band moves to 4000–5000 and both hypotheses predict full fog at every flyable altitude.
- **B13, discriminator 3 (same shape, C2B): the already-captured `t=50` above-deck frame** — is any
  ocean or terrain visible past the deck's edge, and is it hazed? Conditional on C2B = `zone1`.
- **B12's brief changes.** Do not judge the gradient alone: C1's `zone2` dome is the only one
  carrying **`moon` + `stars`** while `zone1` is a single scrolling band, so the render must be
  read for *the moon and the star field*, which are unmistakable. B12 can settle **C1C and C4 in the
  same pass** (their `zone2` carries the same moon+stars pair on daylit missions) and it must
  explicitly rule on the `zone_id` census above, which points the other way for C1C/C2B/C4. **And
  B12 must neutralise fog on both shots** (`--no-fog`), because with `ForceFogged` on, both domes
  render as the same flat gray below the band and the comparison is degenerate (METHOD-1).
- **B16 is promoted from "lever" to the primary fix for BL-303.** The evidence is here and it is
  arithmetic, not a render: the dome cannot be un-fogged by any altitude rule at its authored size.
  B14 must **not** be tuned to make BL-303's skies come out right.
- **B15 gains a second candidate beside `fogRangeFactor`:** the shader computes
  `smoothstep(near, far, d)` while every chapter's `World` node declares `fog_type: "Linear"` and
  `FOG_RANGES` is a D3D `FOGSTART`/`FOGEND` pair. (Low confidence — the rest of that struct is
  zeroed, so "Linear" may be a default — but the S-curve costs ±0.10 of fog fraction at t≈0.21/0.79
  against a linear ramp, and C3's terrain is the scene where it shows.)

### `fog_zone`: the sign check, and C5's interior numbers

**Wrong-claim #6 holds; the decode is not re-opened.** Per-chapter signs:

| chapter | `fogvol.zrd` `fog_zone` | as a zone *number* | as a 0-based *index* into the file's zone list | mission zone |
|---|---|---|---|---|
| C1, C1C, C2B, C4 | **0** | names no zone (files carry ZONE1/ZONE2) ✗ | `ZONE1` | open |
| C5 | **1** | `zone1` — matches, but only here | **`ZONE3`** | **zone1** (settled) |
| C1B, C2, C3 | *absent* | — | — | zone1 (settled) |

Neither reading selects the mission zone in every chapter: the *number* reading fails C1 (there is
no zone 0), and the *index* reading fails C5 (it names `ZONE3` while the mission is settled
`zone1`). **No chapter's data contradicts the negative.** What is new is that the index reading is
now *corroborated* and its target *named*: C5's `fogvol.zrd` `fog_color` is `[16,16,16]`,
**byte-identical to `ZONE3`'s `FOG_COLOR`**, and C5 is the only chapter that authors `fog_color` at
all — so `fog_zone` indexes **the zone a volume's interior fog uses**, which is precisely why it is
not the mission selector. Hand that to `BL-315`/`B14`; do not re-derive the sky zone from it.

**The `fvol` node `zone_id` correlation has no consistent sign** (C1 **2**, C1C **2**, C4 **2**,
C2B **−1** = always, C5 **1**). It does not track `fog_zone` (0,0,0,0,1) and it does not track the
settled zone. It is the world-variant tag of the zone-choice conflict above, not a fog selector.

**C5's `ZONE3` numbers are consistent with H4's inside-a-volume reading on four counts:**

1. `FOG_RANGES` far **250** against `ZONE1`'s **2250** = **11.1 %** — the user's "roughly a tenth".
2. `FOG_COLOR` `[16,16,16]` == `fogvol.zrd`'s `fog_color` `[16,16,16]`, exactly.
3. The 17 volumes sit at **−463 … 183 m** — street level, where the user flew in and hit a building.
4. C5 is the only chapter with *both* an otherwise-unreachable third zone *and* interior-fog keys.

⚠ **`fog_fade_dist` and `interior_fog_fade_dist` are `16.0`, not 50** (the 50 in B11's own brief is
`ZONE3`'s `FOG_RANGES` *near*, a different key). At 16 m they are far shorter than the 250 m fog
range, so they read as the **boundary blend width** — how fast the interior fog takes over as the
wall is crossed, ~0.16 s at flight speed — not as the fog itself. `CLIP_RANGES` 5–300 is the one
number that is *not* explicable as an override alone: a 300 m hard clip inside a street volume is a
render behaviour `BL-315` must decide on, and it is what makes the collapse lethal.

### Files

`analysis/fog-zone-survey/survey_zones.py` (the instrument), `analysis/fog-zone-survey/FINDINGS.md`,
this section, and the checklist line. No engine code, no `docs/formats/` page, no `backlog.md`,
no `PROJECT_CONTEXT.md`.

### Original brief (kept for reference)

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

## B12 ☑ Dome-identity discriminator at the above-deck pose

**Landed.** (2026-08-08) Renders + `extracted/` reads only — no engine code, no `docs/formats/`
page, no `backlog.md`, no `PROJECT_CONTEXT.md`. **No code change falls out of this item**: the
`--sky-zone` default is already `zone2` and `PreferPopulatedHorizonZone` is untouched.

### VERDICT

**C1, C1C and C4 fly `zone2` — and so does C2B, shot with the same instrument because `B13`'s
third discriminator hangs on it. All four open chapters land on the zone we already fly.**

The conflict was never a tie. All three zone1 arguments rest on one unstated premise — *a daylit
mission cannot render a moon and a star field* — and the original's own footage shows C1 IA1 and
C4 IA1 doing exactly that. The fourth argument (the `zone_id` census) reached the right answer for
the wrong reason and must **not** be carried forward: its visibility reading is self-contradictory
(below), so it is retired as evidence even though it agreed. The verdict rests on the renders.

| chapter | verdict | confidence | what settled it |
|---|---|---|---|
| **C1** | **`zone2`** | **verified against two original stills** | `C1 IA1 Cloud Puffs and Moon.png` shows a **moon**; the above-deck still shows a **faint star field**; only `horizon/zone2` has either |
| **C4** | **`zone2`** | **verified against original footage** | `CAP-12` `c4/t21.5-1230m-above.png` shows the **moon** top-left over the deck; C4 `zone1` is one flat grey mesh |
| **C2B** | **`zone2`** | **verified against original footage** | `CAP-11` `t50-c2b-above-deck.png` sky `(71.7, 77.6, 110.3)` vs our `zone2` `(64.3, 72.3, 100.5)` and `zone1` flat `176³` |
| **C1C** | **`zone2`** | **asset identity + parity — no original C1C footage exists** | its `horizon/zone2` is the *same four meshes with the same bboxes* as C1's; its `zone1` is one mesh rendering a featureless field |

### The renders (METHOD-5: fog neutralised identically on both sides)

`.\RunProbe.ps1 --freecam --chapter=<X> --det --mute --no-fog --sky-zone=zone1|zone2 --pos=… --direction=… --screenshot=…`,
all in `.scratch/b12/`. Both sides logged `weather [zoneN] --no-fog: fog + whiteout OFF, world
light unchanged` (METHOD-15/METHOD-6 — the probe streams are in `.scratch/logs/`), so the
comparison is of **domes**, not of fog. C1 at the pinned above-deck pose `-7323,1192,-3829`
`0,0,-1`; C1C at `1750` (above its 1688 m `fvol` top), C4 at `1250`, C2B at `1600` pitched `+0.30`
(at `1230` the deck and its `cloudparent` build-ups fill the frame — that pair was discarded).

Sky-region mean RGB, and **isolated bright points** = pixels brighter than all eight neighbours by
≥ 3 (the star metric; the gradient was deliberately *not* the test, per B11's amendment):

| scene | sky mean RGB | luminance | star points |
|---|---|---|---|
| **ORIGINAL** C1 above-deck still | **(64.9, 73.6, 103.1)** | 74.3 | **8** |
| **ORIGINAL** `C1 IA1 Cloud Puffs and Moon.png` | **(66.8, 74.7, 101.6)** | 75.6 | **5** (lum 197–216 over a 73 sky) |
| ours C1 **`zone1`** `--no-fog` | (169.4, 169.6, 170.7) | 169.8 | **0** |
| ours C1 **`zone2`** `--no-fog` | **(66.1, 74.6, 105.0)** | 75.5 | **13** |
| ours C1 `zone1`, moon-framed heading | (173.7, 173.6, 174.0) | 173.7 | **0** |
| ours C1 `zone2`, moon-framed heading | **(64.0, 72.0, 100.0)** | 72.8 | 3 + **the moon disc** |
| ours C1C `zone1` | (176.0, 176.0, 176.0) sd **0.0** | 176.0 | **0** |
| ours C1C `zone2` | (65.8, 74.7, 105.8) | 75.6 | **13** |
| **ORIGINAL** C4 `CAP-12` t21.5, 1230 m | (194.4, 195.9, 210.0) **B−R +15.6** | 197.0 | 17 |
| **ORIGINAL** C4 `CAP-12` t41.0, 1567 m | (210.2, 211.2, 218.8) **B−R +8.6** | 211.4 | 1 |
| ours C4 `zone1` | (201.5, 200.6, 201.5) **B−R 0.0** | 200.9 | **0** |
| ours C4 `zone2` | (159.8, 163.0, 189.5) **B−R +29.7** | 165.0 | **10** + the moon |
| **ORIGINAL** C2B `CAP-11` t=50 | **(71.7, 77.6, 110.3)** | 79.5 | **0** |
| ours C2B `zone1` | (176.0, 176.0, 176.0) | 176.0 | **0** |
| ours C2B `zone2` | **(64.3, 72.3, 100.5)** | 73.1 | **0** |

**C1 `zone2` reproduces the original to within (1.2, 1.0, 1.9) on the above-deck still and
(2.8, 2.7, 1.6) on the moon still — inside decision 8's ±10 bar on every channel. C1 `zone1` misses
by (+104, +96, +68) and is neutral grey where the original is blue.** C2B `zone2` is inside ±10 as
well; C2B `zone1` misses by (+104, +98, +66). C4's originals are fogged and video-graded so the
absolute levels are not comparable (verification.md — compare *within* a frame): the within-frame
statistic is the blue shift, and the original is blue-shifted `B−R +9…+16` while our `zone1` is
**exactly neutral, B−R 0.0** and our `zone2` is `+29.7`.

**The moon is the unmistakable half, exactly as B11 asked.** `OriginalScreenshots\C1 IA1 Cloud
Puffs and Moon.png` is a C1 IA1 flight frame with a crater-textured moon disc over the cloud deck;
`playtest\CAP-12\c4\t21.5-1230m-above.png` is a C4 original with the same disc cut by the top-left
frame edge. C1's `horizon/zone1` is `h_zone1scroll` + `o28` and C4's is a single `h_zone2scroll` —
**neither contains a moon or a star mesh anywhere in the gamez.** A mission whose sky shows a moon
is not flying a dome that has none.

**The star field is real in the original, not a capture artifact.** The above-deck still's 8 points
are 1–2 px, isolated, blue-white, `+13…+29` over a sky whose sd is 3.4; the higher-resolution moon
still resolves the same thing unambiguously at `+122…+143` over its local median. Zoomed,
contrast-stretched crops of both, beside ours, are in
`.scratch/b12/b12-stars-zoom-original-vs-zone2.png`. **B11's caveat "a still could hide faint
stars" is answered: the stills do not hide them, they show them.**

### The `zone_id` census: right answer, dead argument (and it must not be re-cited)

B11 called this the sharpest of the four and "4/4 against the settled verdicts". Both halves fail.

**1. The horizon's own zones carry the same numbering — so there is nothing to decouple.** Every
chapter's `horizon/zone<N>` node *and every dome mesh under it* carries `zone_id == N`: C1
`zone1`→1 (`h_zone1scroll`, `o28`), `zone2`→2 (`moon`, `g1155`, `stars`, `h_zone2scroll`); likewise
C1B/C1C/C2/C2B/C3/C4, and C5 `zone1`→1 / `zone3`→3. `zone_id` is not a layer or a render pass with
its own private numbering — **it is the same zone concept the weather file names.** What decouples
is `zone_id` from *render visibility*, and that is what points 2–4 show.

**2. The 4/4 agreement is degenerate (INSTR-7).** Three of the four settled chapters have **no
zone-2 world at all**: C1B `zone_id 2` = **2 nodes, 0 meshed**; C2 = **1 node, 0 meshed**; C3 =
**2 nodes, 0 meshed**. "The flown zone holds the world" is *trivially* true where the other zone is
empty — and "the other zone is empty" is the very same fact `weather.md` already used to settle
those three from the horizon geometry. It is one datum counted twice, not two agreeing instruments.
The one settled chapter where the census could have spoken is **C5, and it disagrees**: C5 flies
`zone1` and carries **135 meshed nodes at `zone_id 3`** (127 `g*` city meshes at y 0–24 and 7
`ap_lightpole.flt`), so the non-flown zone holds real ground geometry. Worse for a "world variant"
reading, the *same class* of object is split across all three buckets there — `ap_lightpole.flt`
×72 in `zone1`, ×7 in `zone3`, and 164 `lightpole` + 164 `w_lightglow` at `−1`.

**3. In C1 the visibility reading is self-contradictory — neither zone contains a playable
mission.** C1 IA1's own `targets.zrd.json` names `ap_transmitter` (`MSG_OBJ_RADIOTOWER`) and
`dz1`–`dz5` (the fly-through passenger hangar, three train tunnels and Bloodhawk hangar).
`ap_transmitter` is a 14-mesh radio tower with `rtwr_healthy`/`rtwr_destroyed` damage states and
**every node in it is `zone_id 1`**; so are `dz1`–`dz5` and `dzpath1`–`dzpath5`. The cloud deck
(145 nodes, 823–960 m), all 9 `fvol`, all 28 `cloudparent` and the moon/stars dome are **`zone_id
2`**. Under a "draw only the flown zone" gate, C1 flying `zone2` has no radio tower and no
fly-through structures, and C1 flying `zone1` has no deck. Generalised over the install: **every
mission-named node that carries a zone at all is `zone_id 1`** — C1 29 name hits, C1B 16, C2 28,
C3 13, C4 43, C5 1, and **zero** in `zone_id 2` (the lone C4 `zone_id 2` hit is the generic
sub-node name `healthy` reached from `zeppelins.zrd.json`, an INSTR-10 name collision). The render
says C1 flies `zone2` — the bucket *without* its own targets. **So `zone_id` cannot be a
render-visibility gate.** `DIAG-6` is now settled positively: the correlation had no mechanism, and
it now has a contradiction.

**4. "Two complete alternative world variants" dies too.** C1's `zone_id 1` and `zone_id 2` meshed
buckets share **0 node names** (451 vs 216 distinct). Two variants of one world would repeat their
objects; these are complementary halves of a single world whose union is the mission. *(A 5 m
co-location test looked like variant pairing at 880/1427 — its own control killed it, METHOD-14:
zone1→zone1 excluding self scores 1200/1427. Do not re-run it.)*

**5. One replacement reading was checked and dismissed, so it is not re-chased:** "`zone_id` picks
the fog *zone* each node uses" would hand C1's entire deck, `fvol` field and `cloudparent`
population `ZONE2`'s 1000–4000 range under a 4000–5000 altitude band — a flat `176` grey deck at
every flyable altitude, which both C1 reference stills refute outright.

**Bottom line:** `zone_id` is a partition tag of one world whose runtime meaning is still unknown,
nothing in `CSVM/src` reads it, and **it is no longer admissible evidence about which weather zone
a mission flies.** B11's zone-choice-conflict table should be read with that row struck.

### What this changes downstream

- **B13's discriminator 2 (C1 valley through a deck gap) DOES NOT EXIST.** C1's flown band is
  `zone2`'s 4000–5000 m, unreachable under the 2500 m ceiling, so H1 and H2 predict full fog at
  every flyable altitude. **B13's discriminator 3 (C2B t=50) DOES NOT EXIST** either — C2B is
  `zone2`, band 9000–10000. **B13, discriminator 1 (C2 above 1024 m looking down) is the only one
  left, and H1-vs-H2 is now a C2-only question**, exactly the branch B11 predicted for a `zone2`
  verdict. B13's CAP should be minted for the C2 climb and nothing else.
- **`weather.md`'s corroboration of the `FOG_ALTITUDE` semantics belongs to a zone C1 never flies.**
  "C1/IA1 `zone1` 970→1047 is exactly cloud-band-bottom → whiteout-centre" is still an exact 3/3
  identity in the data (C1 970/1047, C1C 1055/1082.5, C2B 924/1024 vs `CloudBandCentre`) but it is
  an identity in the **unflown** zone, so it can no longer be cited as evidence for what
  `FOG_ALTITUDE` *does*. **B14 owns the correction** (this item does not touch `weather.md`); the
  identity itself is real and unexplained and must be recorded, not deleted.
- **Every flown `FOG_ALTITUDE` band in the install except C2's is now unreachable**, which is
  INSTR-7 for the whole altitude question: B14 must not tune the altitude term against a scene
  that cannot exercise it.
- **`B16` is unaffected** — the dome still must not fog, and this item's `--no-fog` renders are
  another demonstration of it: the same `zone2` dome that paints a flat `176` band under
  `ForceFogged` is a ±2-per-channel match to the original with fog off.
- **`BL-100`'s four open chapters are settled** (C1/C1C/C2B/C4 = `zone2`, the shipped default), with
  C1C the only one resting on asset identity rather than footage. `B17` owns the close.

### Files

`.scratch/b12/` (10 renders + 6 comparison sheets, listed below), this section, and the checklist
line. Nothing else — no engine code, no `docs/formats/`, no `backlog.md`, no `PROJECT_CONTEXT.md`.

- `b12-sxs-C1-abovedeck-original-vs-zone1-zone2.png` — the pinned pose, original vs both domes
- `b12-sxs-C1-moon-original-vs-zone1-zone2.png` — the moon, original vs `zone2` vs `zone1`
- `b12-sxs-C4-original-vs-zone1-zone2.png` — `CAP-12` t21.5 vs both C4 domes
- `b12-sxs-C2B-original-vs-zone1-zone2.png` — `CAP-11` t=50 vs both C2B domes
- `b12-sxs-C1C-zone1-vs-zone2.png` — C1C's two domes (no original exists)
- `b12-stars-zoom-original-vs-zone2.png` — contrast-stretched 13× star crops, both originals + ours

### Original brief (kept for reference)

⚠ **Two claims below are now known false and are kept only as the record of what was believed:**
"the above-deck original shows a smooth blue gradient and **no stars**" (it shows 8 star points,
and a second C1 IA1 still shows the moon), and the `zone_id` census being "the sharpest" argument
(it is not admissible at all — see the ruling above).

**⚠ Amended by B11 (2026-08-08) — this is now a four-way data-conflict ruling, not a gradient
comparison.** Three arguments point to zone1 for C1/C1C/C4 (their `zone2` carries `moon` +
`stars` meshes in daylit missions with identical SUNLIGHT both zones; only `ZONE1` is
per-mission-tuned across all 53 weather files; `zone1`'s `FOG_ALTITUDE` equals
`[CLOUD_COVER BOTTOM, band centre]` exactly 3/3). One argument — the sharpest, and it agrees
with every settled verdict 4/4 — points to zone2: the `zone_id` world census puts **all**
deck-altitude nodes, all `cloudparent` clusters and all `fvol` volumes in `zone_id 2` for C1
(145/28/9), with `zone_id 1` holding only ground nodes — under HISTORY's zone_id reading, a C1
mission flying zone1 has no cloud deck at all, refuted by both reference stills. Rule on this
explicitly. Method constraints from B11: read the render for the **moon and star field**, not
the gradient; shoot with **fog neutralised on both sides** (with `ForceFogged` on, both domes
render the same flat gray below the band — the gradient test is degenerate); settle **C1C and
C4 in the same pass**; and land this before B13's CAP is minted — B12's verdict decides whether
two of B13's three discriminators exist (if the open four land zone2, H1-vs-H2 collapses to a
C2-only question).

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

## B13 ☑ Footage discriminators: camera-vs-fragment fade, switch point

**Landed (2026-08-08) — settled at the controls, no capture flown.** After B11 killed H3 and B12's
zone2 ruling killed discriminators 2 and 3, the single surviving discriminator (C2 above its
1024 m band top — the only flown fog band in the install inside the flight envelope) was answered
by direct user observation in the original: **"Still dissolves into haze"** — the distant city
keeps fading into the pale wash no matter how high the camera climbs. That is **H2: fog fades by
FRAGMENT altitude**, exactly what our shader ships. H1 (camera fade) is dead. The two hypotheses
were ~100 luminance units apart at that pose (B11's matrix), so the observation is categorical,
the same evidence class that settled C5 = zone1 in 2026-07. `CAP-35` was minted for this and is
retired unflown (removed from playtest.md the same turn); the user's attempt found the original's
FOV too narrow to hold a single feature through a climb, and the feature-hold turned out to be
unnecessary — the discriminator was always scene-level, not feature-level.

**Consequence for B14:** there is no fog model to implement — the semantics we ship are the
original's. B14 collapses to the documentation landing it already owed (weather.md's dead zone1
corroboration, the C2 observation as the semantics' evidence) plus the fade-shape/range questions,
which live in B15.

**Original goal (kept for reference).** H1 vs H2 settled by what the original renders
through/above the deck; if H3 survives B11, the switch point bracketed.

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

## B14 ☑ Implement the winning fog model — there was nothing to implement

**Landed.** (2026-08-08) **A disproof-of-change landing: the model is confirmed as shipped.**
`B13` settled the semantics at the controls of the original — fog fades by **FRAGMENT** altitude —
and that is precisely what `csky_fog_amount` has always computed
(`CSVM/shaders/csky_atmosphere.gdshaderinc:27`). H1 (camera fade) is dead, H3 died on C2 in `B11`,
and `B16` already owns the dome. So no fog model was left to write, and the item collapses to the
documentation it owed. **No engine code, no shader, no zone-resolution code, no whiteout, no
`backlog.md`, no `PROJECT_CONTEXT.md`** — the fade *shape* and the fade *range* are `B15`'s, and
they are the only render questions the wave has left.

⚠ **Do not read this as "the fog is right".** Three things are settled — which zone (B12), whether
the dome fogs (B16), and what the altitude term fades by (B13) — and the two that are not are
exactly the two `B15` measures: the range factor and the ramp shape.

### What changed in `docs/formats/weather.md`

1. **The `FOG_ALTITUDE` semantics keep their description and swap their evidence.** The old
   corroboration — "C1/IA1 `zone1` 970→1047 is exactly cloud-band-bottom → whiteout-centre" — is
   **retired**, because `B12` proved C1 flies `zone2`, whose band is 4000–5000 m: it is an identity
   in a zone C1 never flies, so it cannot say what `FOG_ALTITUDE` *does*. The identity is kept as a
   recorded, unexplained property of the authoring, with the full 3/3 (C1 970/1047, C1C
   1055/1082.5, C2B 924/1024 against `WeatherState.CloudBandCentre`) — deleting it would lose a
   real fact. The **new** evidence is `B13`'s C2 at-the-controls observation, cited with the reason
   it is categorical rather than a judgement of degree (~100 luminance units on distant ground).
2. **A new ⚠ on the flight envelope.** Every flown `FOG_ALTITUDE` band in the install except C2
   `ZONE1`'s 256–1024 m sits above the 2500 m ceiling — 4000–5000 (C1, C1C), 9000–10000 (C2B, C3,
   C5), 10000–11000 (C1B, C4) — so the altitude term is a constant 1.0 everywhere else and the
   whole install exercises it in exactly **one** chapter. Anything that touches that term is
   measured in C2 or it is not measured (`INSTR-7`).
3. **The zone-selection section records `B12`'s four verdicts** in place of the "still open for C1,
   C1C, C2B and C4" paragraph: C1/C1C/C2B/C4 = `zone2`, the shipped default. Cited to the moon in
   `C1 IA1 Cloud Puffs and Moon.png` and `CAP-12 c4/t21.5`, the star field in the above-deck still,
   and the sky-colour numbers (C1 ours `(66.1, 74.6, 105.0)` vs original `(64.9, 73.6, 103.1)`;
   C2B `(64.3, 72.3, 100.5)` vs `(71.7, 77.6, 110.3)`; C4's B−R shift), with **C1C flagged as
   asset-parity only**. The `--no-fog` method constraint and the `zone_id`-census retirement travel
   with it, so the dead argument cannot be re-cited from the format page. The per-chapter "flown"
   table and the intro's "settling the rest needs an A/B" line are updated to match.
4. **`B18`'s skirt/`FOG_COLOR` cross-check is recorded as agreeing — and as unable to discriminate
   here**, which is the honest form: all four chapters ship the *same* `FOG_COLOR` in both zones
   (C1/C1C/C2B `0.69³`, C4 `192³`), so the skirt looks right under either verdict. It corroborates,
   it does not vote.
5. **Two stale H3 restatements fixed** in passing, both of which said `FOG_ALTITUDE` reads as a
   zone-selecting altitude band: the C5 `ZONE1`/`ZONE3` comparison note, and (by the new ⚠) the
   `FOG_ALTITUDE` row's own surroundings. H3 died unconditionally on C2 in `B11`.

### Files

`docs/formats/weather.md`, this section, and the checklist line. Nothing else.

### Original brief (kept for reference)

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

## B15 ☑ Re-calibrate or delete `fogRangeFactor` 2.0 — deleted, and the fade is linear

**Landed.** (2026-08-08) **`fogRangeFactor` is gone — the authored `FOG_RANGES` are the ranges —
and `csky_fog_amount`'s range term is a LINEAR ramp instead of a `smoothstep`.** Both replace an
invented value with an authored one. Every measured pose improves; two healthy scenes are exactly
unchanged and the rest move by ≤ 1 unit. **The river pose is much better and still not matched, and
the reason is now measured rather than guessed: it is not the fog** — see "What this does not fix".

### The 2×2, at the calibrated poses

Four cells, `factor ∈ {2.0, 1.0} × curve ∈ {smoothstep, linear}`, nine poses each, all in
`.scratch/b15/`. **METHOD-15 on both levers:** `SetupWeather` now logs the APPLIED range beside the
authored one (`fog 0.69 gray 1000–4000 m (authored 1000–4000)`), and every cell's line is kept at
`.scratch/b15/logs/<cell>-<pose>.weather.txt`; the shader swap was confirmed by pixel diff before
any of it was read (river 27.8 % of pixels moved on the curve alone). The landed build re-shot all
nine poses **bit-identical** to the `f1-linear` cell (`mean|d| = 0.000, max 0`), so what was chosen
is what shipped (METHOD-16).

**1. C1 river pose** (`-7323,192,-3829` / `-0.997,-0.1,0.070`) vs
`OriginalScreenshots/C1 IA1 Fog river.png`. The instrument (`.scratch/b15/reach.py`) is a per-row
horizontal HIGH-PASS RMS over a HUD-free column band — fog washes texture out while leaving a
smooth gradient, so a plain row sd measures the gradient — thresholded at 25 % of each image's own
top-of-frame plateau, because the original's mottling runs at hp ≈ 0.59 and ours at ≈ 0.26 and an
absolute cut would measure the deck texture's CONTRAST rather than how far the fog lets it live.
Reach is reported three ways: A7's own depth-from-top row, the elevation above the TRUE horizon
(framing-independent — the two frames' cameras differ in pitch), and the saturation distance that
elevation implies through A7's own `f·h = 2.40e5 px·m`.

| cell | mottling dies at row | elevation above horizon | ⇒ saturation distance |
|---|---|---|---|
| **ORIGINAL** | **337** | **19 px** | **12.6 km** |
| `factor 2.0` + smoothstep (**before**) | 165 | 135 px | 1.8 km |
| `factor 2.0` + linear | 173 | 127 px | 1.9 km |
| `factor 1.0` + smoothstep | 233 | 67 px | 3.6 km |
| **`factor 1.0` + linear (LANDED)** | **236** | **64 px** | **3.7 km** |
| *control:* `--no-fog` | 299 | 1 px | — (nothing left to saturate) |

⚠ **A7's "~140 px vs ~330 px" reproduces (165 vs 337) but the two frames are not framing-matched**,
and the difference is not small: the original's camera is level with its terrain horizon 41 px
BELOW the true horizon, ours pitches 5.7° down with terrain rising to 17 px ABOVE it. So the raw
330 target is unreachable at our pose *whatever the fog does* — the `--no-fog` control tops out at
row 299 because terrain, not fog, ends the sky there. The honest target is the elevation: the
original's ceiling survives to 19 px above the horizon, our own sky ends at ~17 px, and the landed
build reaches 64 px.

**2. C3 canyon** (`-3504,710,-3619` / `-0.40673,0,-0.91355`), region means, plus a framing-robust
vegetation fraction (`G − R > 8` over the lower frame) because the original is a chase frame and a
fixed box lands on different terrain on the two sides:

| region | f2+smooth (before) | f2+linear | f1+smooth | **f1+linear (landed)** | ORIGINAL (`CAP-11`) |
|---|---|---|---|---|---|
| near slope | 177.8 | 170.8 | **85.6** | 106.1 | **36.5** |
| mid ridge | 200.8 | 200.2 | **139.9** | 143.7 | 19.9 |
| far ridge | 201.0 flat | 201.0 flat | 187.2 | **182.4** | 60.3 |
| vegetation, lower frame | 8.8 % | 8.7 % | **29.2 %** | 22.1 % | **47.8 %** |
| sky | 202.9 | 202.9 | 202.9 | 202.9 | 194.9 |

`B16` left C3's near slope at **162.0, byte-identical before/after**, and named B14/B15 as its
owner. This item moves it to **106.1** and the frame from 8.8 % to 22.1 % vegetation — the "murk"
half of `BL-303`'s C3 case is materially better and still not closed (see below). The sky is B16's
already-fixed dome and does not move.

**3. The other pinned pose (C1 above deck, `-7323,1192,-3829`).** Apex rows 0–150 **75.5 →
75.5, byte-identical** (original 74.3, `B12`) — the dome is unfogged, so it cannot move. The
horizon band (B16's rows 265–355) goes 153.4 → 164.8 with sd 28.1 → 38.1: more distant cloud tops
survive instead of washing to 176. 13.4 % of the frame moved, all of it below the apex.

### Why LINEAR, when the renders split

**The render evidence does not decide the curve, and this record does not pretend it does.** A
smoothstep sits *below* the linear ramp over the near half of the range and *above* it over the far
half, so it trades one end for the other, and every pose splits exactly that way: smoothstep wins
C3's near slope (85.6 vs 106.1) and its vegetation fraction (29.2 % vs 22.1 %); linear wins the
river reach (236 vs 233), C3's far ridge (182.4 vs 187.2) and C1B's moonlit cloud tops (p90 187.5
vs 193.5 against originals at 155.4/181.9). Both cells read "our fog is still too strong", at
opposite ends. That is a statement about a **residual**, not evidence for a curve.

So the tie goes to the data, and the data is stronger than `B11` could see: every chapter's `world1`
node carries a `World` struct whose `fog_state` field is the raw **u32 1 = LINEAR**, and mech3ax
**asserts** it (`crates/nodes/src/cs/world/data.rs:97,234` — `const FOG_STATE_LINEAR: u32 = 1`).
`0` there would be OFF and `2` EXPONENTIAL. B11 marked this low-confidence because "the rest of
that struct is zeroed" — but that is what makes a *written* 1 significant rather than doubtful: the
node declares the fog MODE and nothing else (its `fog_color`/`fog_range`/`fog_altitude`/
`fog_density` are all 0, because the parameters live in weather.json). The `smoothstep` was ours
and rested on nothing.

⚠ **Only the RANGE term changed.** The ALTITUDE term keeps its `smoothstep`: `fog_state` is D3D's
distance-fog mode and says nothing about a vertical fade, the cylinder's altitude fade is the
remake's own model, and exactly one flown band in the install (C2 `ZONE1`, 256–1024 m) is inside
the flight envelope, so seven of eight chapters cannot tell one altitude curve from another
(`INSTR-7`, `B14`). Making it "symmetric" would be a guess dressed as tidiness.

### What this does NOT fix — and it is not the fog

**At the river pose the landed build reaches 64 px above the horizon against the original's 19 px,
and no cell of the 2×2 closes that.** It cannot: at the authored 1000–4000 m the fog is fully
saturated by 4000 m, which is 60 px in this frame, so the whole family of (factor, curve) choices
is bounded away from 19 px. Per the item's own instruction, that is stated rather than tuned
around — and the measurement that explains it was taken:

⚠ **The original's overcast ceiling is ALREADY at the fog colour, and ours is 40–50 units above
it.** Row-mean luminance down the original river still's sky runs **166.5 → 175.0** from the top of
frame to the horizon — a total dynamic range of ~9 units against a `FOG_COLOR` of 176 — while our
`--no-fog` control renders the same ceiling at **200–220**. Re-measured on the landed build at
`CAP-12`'s own underside box (`-4974,670,-3861`, its 2200 ft still): original **167.7**, ours
**213.9** (CAP-12 recorded 220.4 pre-wave). So the original's fog is nearly invisible on its deck
because the deck is already 170; ours produces a visible wash from 220 down to 176 *however* the
fog is calibrated. **The river-pose fog symptom and `BL-118`'s +54 underside are the same defect
seen from two sides**, and it is Wave C's `C21`/`C22`, not a fog range. That also predicts a
short-term worsening, and it is measured: at `CAP-12`'s `t8.0` below-deck pose our ceiling goes
205.6 → **214.8** against the original's **167.3** — removing the factor removed a compensation
that had been hiding the deck's brightness. Exactly the trap this item's own brief names.

**Two candidates for the terrain residual (C3's 106.1 against 36.5), neither implemented here:**

1. **The fog mix happens in LINEAR space; the DX7 chain blended in FRAMEBUFFER (gamma) space.**
   Every consumer does `ALBEDO = mix(ALBEDO, csky_fog_color, fog_amt)` with both operands already
   linearised. At half fog between a dark slope (40 sRGB) and 176 the two spaces differ by **21
   units** — gamma 108, linear 129 — with the linear result always the washier one, and the effect
   peaks in the mid-range where C3's slope sits. This is the same class of bug as the two already
   landed here (`csky_world_light`'s gamma-space dimming and `SceneBuilder`'s gamma-space vertex
   modulate), and it is a one-line-per-shader experiment. **This is the first thing to try.**
2. **A7's `f·h ≈ 2.4e5` (hence `DeckCeilingHeight` 400 m) may be ~3× too large.** The original's
   ceiling saturates at 19 px; if that is its authored 4000 m far range — which is what every other
   chapter's fog does — then `f·h ≈ 7.6e4` and `K ≈ 128 m`. A7 derived 400 m from apparent mottling
   *period*, assuming one texture repeat per 1024 m tile. The two readings disagree by the same
   factor of ~3 and cannot both be right; the fogged still now offers a second, independent
   estimator of the same product, which A7 did not have.

Neither is a `B15` change and neither is guessed at here.

### Regression — nothing healthy moved

Before shots were taken FIRST, in the `f2-smooth` cell, at the poses the `BL-110` record names
(`playtest/CAP-11/README.md`), each at its spawn's own heading (`dir = (sin h, 0, −cos h)` from the
chapter's `ia.zrd.json`):

| pose | region | before | **landed** | original |
|---|---|---|---|---|
| **C2B** spawn `-3843,200,-1101` | sky | 170.8 | **169.9** | 177.0 (`t0.5-c2b-spawn-ocean.png`) |
| **C2B** spawn | ocean near | 41.4 | **41.0** | 52.1 (the gap is `BL-304`'s water/WorldLight exemption, untouched) |
| **C1B** spawn `-5406,55,-7200` | island/sea | 23.4 | **23.4 — byte-identical** | 26.8 / 27.4 (`CAP-11`) |
| **C1B** spawn | moonlit cloud tops, p90 | 43.4 | **187.5** | 155.4 (`t5`) / 181.9 (`t16`) |
| **C2** `dogfight_ace[4]` `-6862,160,-4335` | ground near | 105.7 | **105.7 — byte-identical** | 89.1 / 88.0 |
| **C2** | ground mid | 165.3 | **109.4** | ≈ 88 |
| **C5** `dogfight_ace[3]` `-9187,90,-2037` | street | 30.2 | **30.2 — byte-identical** | — |
| **C5** | sky | 16.6 | **16.6 — byte-identical** | 15.3 (`BL-303`) |
| **C4** `-4974,300,-3861` | every box | 191.5–191.9 | **191.5–191.9 — unchanged** | — (degenerate, below) |
| **C1** `CAP-12` `t8.0` `-4974,255,-3861` | near ground | 74.5 | **74.5 — byte-identical** | 108.2 |

**The three healthy anchors `CAP-11` actually pinned are byte-identical or within 1 unit**: C1B's
island terrain, C2's near suburb grid, C5's street and sky, C2B's ocean and sky. What moved at those
chapters moved toward the original — C2's mid-distance ground 165.3 → 109.4 against ≈ 88, and
C1B's moonlit cloud tops from 43.4 to 187.5 against the original's own 155–182, which is the
`CAP-11` residual "our night clouds are ~2× dark against the moonlit side" partly answered as a
side effect. (It is not a healthy-scene regression: `CAP-11` recorded those clouds as a *miss*, not
a match.)

⚠ **The C4 pose is a degenerate instrument, and it is reported as one** (`INSTR-7`). C4 authors
`FOG_COLOR` `192³` and flies over snow, so its terrain and its fog are the same luminance: every
box reads 191.5–191.9 in all four cells and 4.2 % of the frame moves at all. C4's valley haze is
not *unchanged-and-therefore-safe* here; it is **unmeasurable at this pose**, and a C4 fog claim
needs a pose with dark timber or rock in frame.

⚠ **One consumer had the factor baked into its sizing argument, and it survives — checked, not
assumed.** `MapEdgeExtender.Rings = 5` (5 × 1,024 m = **5,120 m** window radius) is sized so the
terrain's void edge never appears before the fog has saturated, and its comment said "regardless of
the `fogRangeFactor` TUNE" because it was deliberately sized against the **raw** authored far. That
is exactly the case that is now live, and it still holds with margin: the longest authored
`FOG_RANGES` far in any **flown** zone in the install is **C1B's 4,700 m** (then C3/C4 4,500,
C1/C1C/C2B 4,000, C2 2,400, C5 2,250), all inside 5,120 m, so the void starts where fog is already
1.0 along every axis. Comment corrected to say so; `Rings` untouched.

### Tests, goldens and the 8-chapter regression

- **8-chapter `--freecam` regression: zero errors in all eight**, every census identical to `A7`'s
  (decks C1/C1C/C2B 144 @ 960, C4 144 @ 1050; clusters C1 28 · C1B 70 · C1C 30 · C4 45; sprites
  C1/C2B/C4 22,201 · C1C 22,748 · C5 16,170). `.scratch/b15/regress-C*.png`.
- **`.\RunTests.ps1`** — build PASS (0 warnings), **units 682/682**, **engine 26/26, errors clean**,
  goldens **11 moved, 0 broken of 13**. Exit 1 is the golden stage alone.

| golden | moved? | why |
|---|---|---|
| `c1-waterfall`, `c1b-night-sea`, `c1c-rain`, `c2-city`, `c2b-rain`, `c3-island`, `c4-snow`, `c5-city-night`, `c1-flight`, `c1-destroy-effects`, `c1-crash` | **moved** (11) | **every shot that builds a chapter world.** The fog globals are read by the world, the clutter sprites, the solid city blocks and the aircraft alike, so any framing containing anything past `near` moves |
| `viewer-bhawk`, `empty-stage` | **ok** | the only two shots with **no chapter world**: `--viewer` builds one aircraft and `--stage=empty` has no gamez, so `WeatherRig` never runs and the fog globals keep their registered no-op range. Inert by construction (`DIAG-10`) |

**That partition — 11 world shots moved, 2 worldless shots did not — is the `GOLD-5` pattern for a
global fog change, and it is sharper than the dome items' was.** Two shots are the able-to-fail
controls for it: `c1-waterfall` and `c1-crash` were **unchanged** through both `B16` and `B18`
(no dome in frame — a top-of-frame strip and a straight-down fireball) and both move here, which
is exactly right for a change that fogs terrain rather than sky. **Not re-pinned — `B17` re-pins
once**, per the wave's own convention (`GOLD-1`/`GOLD-8`).

⚠ **Unrelated flake seen twice, recorded so the next runner does not chase it:** the `viewer-bhawk`
golden **renders and reports its hash** (`71d5c3bf…`, identical to its pin) and then never exits;
`RunTests.ps1`'s `Invoke-Godot` uses an unbounded `WaitForExit()`, so the stage blocks forever.
It reproduced on two consecutive runs and it cannot be this item's doing — `--viewer` builds no
chapter world, so nothing in `WeatherRig` runs at all. Killing that one process lets the stage
finish and it records `ok`. Worth a `BL` on `RunTests.ps1` (a per-shot timeout, the way
`RunProbe.ps1` already has one).

### Files

`CSVM/src/Session/WeatherRig.cs` (`fogRangeFactor` deleted with its stale comment; the applied
range added to the weather log line, which is what makes `METHOD-15` possible on this at all),
`CSVM/shaders/csky_atmosphere.gdshaderinc` (the linear range ramp, and the header's retired
`zone1 970→1047` corroboration struck per `B14`), `CSVM/src/Flight/Weather.cs` (`ZoneFog`'s doc
restated the same retired corroboration — corrected, not deleted),
`CSVM/src/Mech3/MapEdgeExtender.cs` (its `Rings` comment named the now-deleted factor; the sizing
is unchanged and re-checked above), `docs/formats/weather.md` (the `FOG_RANGES` linear-ramp decode
+ the `VIEWING_RANGE` note), `docs/architecture.md` (the `WeatherRig.cs` entry),
`docs/verification.md` (`SHOT-23`), this section and the checklist line. `PROJECT_CONTEXT.md` and
`backlog.md` untouched — `BL-303`/`BL-101` close at `B17`. Probes, instruments (`reach.py`,
`boxes.py`, `shoot.ps1`, `regress.ps1`) and the four cells' 36 renders in `.scratch/b15/`.

**Probe pairs worth human eyes** (`.scratch/b15/`): `f2-smooth-c3canyon.png` vs
`landed-c3canyon.png` (the biggest visible change in the wave — a white-out wash becomes a green
canyon with a visible coastline), `f2-smooth-c1bspawn.png` vs `landed-c1bspawn.png` (C1B's moonlit
cloud tops going from grey to white), and `f2-smooth-river.png` vs `landed-river.png` vs
`ctl-river-nofog.png` beside `OriginalScreenshots/C1 IA1 Fog river.png` (the ceiling's reach, and
how much of the residual the `--no-fog` control shows is not fog).

### Original brief (kept for reference)

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

## B16 ☑ The dome's authored `fog: false` (PROMOTED by B11: the primary fix for BL-303)

**⚠ Amended by B11 (2026-08-08) — no longer a conditional lever.** The evidence is arithmetic,
not a render: every authored skydome tops out +982 m (C4 zone1) to +4,108 m (C1B zone1) above
the camera, while BL-303's three broken scenes fly zones whose `FOG_ALTITUDE` band is
9,000–11,000 m — the altitude term is 1.0 on every dome fragment under H1 *and* H2, and the
range term saturates at the 6.4–21.8 km dome radius. Both surviving hypotheses predict exactly
our broken renders (C3 predicted 201 / measured 201.0; C2B 176 / 176.0; C5 0 / 0.1 — against
originals 194.9-gradient / 82.7 / 15.3). At C3's canyon pose the original's own dome tops at
2,611 m against a 9,000 m band floor — no altitude fade of any kind can un-fog it. Runs BEFORE
B14; B14 must not be tuned to make BL-303's skies come out right. The C1 above-deck gray band
is the same mechanism one zone over, which is why the worktree's un-fogged-dome probe removed it.

**Landed.** (2026-08-08) `WorldBuilder.BuildHorizon` no longer sets `SceneBuilder.ForceFogged`
around its `BuildSubtree` call — dome materials now build exactly as authored (`fog: false` on
every horizon mesh in every chapter, `lighting: false` already honoured). `ForceFogged` itself is
**deleted**: grepped first (`docs/HISTORY.md`/`docs/PLAN-overcast-match.md` references aside,
`WorldBuilder.BuildHorizon` was its only setter and `SceneBuilder.BuildModel`'s `fogged` test —
`mesh.Fog || ForceFogged` — its only reader), so nothing else needed updating to drop it. The
skirt's documented per-instance `csky_fog_on = 0` opt-out (`SceneBuilder.cs`, near the ordered
instance-uniform block) turned out to be **stale even before this item**: grepping the whole
`CSVM/src` tree for a `SetInstanceShaderParameter("csky_fog_on", …)` call finds none — nothing
ever wrote it, so the comment described an opt-out that was never implemented. Reworded rather
than deleted outright (the uniform itself is real, shared, general-purpose, and still declared):
it now says plainly that nothing sets it today, and that the skirt in particular takes the
UNFOGGED shader variant now (authored `fog: false`, honoured), so it never reaches the fog-mix
line this uniform would have gated even if something did.

### Pose-by-pose numbers (ours-before / ours-after / original)

Every pose shot with `.\RunProbe.ps1 --freecam --chapter=<X> --det --mute --pos=… --direction=…
--screenshot=…`, `--det` for reproducibility, fog **on** (unlike B12's `--no-fog` dome-identity
renders — this item is about what fog does to the dome, not the dome's identity). Region-mean
luminance (ITU-R BT.601), `.scratch/b16/`.

| pose | region | ours before | ours after | original |
|---|---|---|---|---|
| C3 canyon (`-3504,710,-3619` / `-0.40673,0,-0.91355`) | sky | **201.0 flat** (full `c9c9c9` fog) | **198.1**, RGB (174.6,203.3,232.9) — blue-gradient | 194.9 blue-gradient, B=221 (`CAP-11`) |
| C3 canyon | near-slope murk (**not** this item's fix) | 162.0 | **162.0 — byte-identical** | 36.5 (BL-303) — still open, B14/B15 |
| C2B above-deck (`-3843,1500,-1101` / `-0.391,0,-0.921`) | sky | **176.0 flat** | **75.2**, RGB (65.9,74.2,104.2) | 79.5–82.7 (`CAP-11`/B12 t=50) |
| C5 night (`-9187,90,-2037` / `-0.1219,0,-0.9925`, `dogfight_ace[3]`) | clear sky patch | **0.0 flat black** | **9.1–16.9** (region-dependent, dark-blue gradient) | 15.3 (BL-303) |
| C1 above-deck pinned (`-7323,1192,-3829` / `0,0,-1`) | apex/zenith (rows 0–150) | 75.3 | **75.3 — byte-identical** | 74.3 (B12's own above-deck still) |
| C1 above-deck pinned | gray band (rows 265–355, full width) | mean 180.0, **sd 5.92** (near-flat) | mean 153.3, **sd 30.71** (textured — cloud/dome detail, min 77) | — (qualitative: band gone, replaced by real gradient) |

**C3's near-slope figure is reported, not fixed, on purpose** (item step 1): the murk is
fog-on-terrain, untouched by this item because `BuildHorizon`'s subtree is the only thing this
change touches — the exact-byte match before/after on that box is the proof it's inert here,
consistent with `DIAG-10`. B14/B15 own it.

**C1's apex being byte-identical before/after is the mechanism confirming itself**, not a null
result: B11's own arithmetic put the apex (local +2155 m × 2.5 scale ⇒ world ≈ +5387 m over the
1192 m camera) above `zone2`'s 5000 m ceiling, where the *fragment-altitude* fog term was already
0 under the old code — `mix(ALBEDO, fog_color, 0)` is a no-op regardless of `ForceFogged`. Only
the **lower** dome (the part between the horizon and the ~5000 m ceiling, i.e. the gray band) was
ever actually painted by `ForceFogged`, and that is exactly the region that moved.

### ⚠ New finding — the dome's own "unfinished" cap/skirt geometry, previously invisible, is now a hard seam in five of eight chapters

Not part of this item's fix, found doing the item 5 crisp-edge sweep it asks for. `WorldBuilder`'s
own (now-removed) comment called the zone1 day-dome's flat cap "likely never player-visible" —
true only because `ForceFogged` painted it the *same* uniform fog colour as everything around it.
Honouring `fog: false` removes that camouflage and exposes it as a genuinely flat, hard-edged
disc or ring, at a colour distinct from both the sky texture above and the terrain fog below:

| chapter (low-altitude sweep pose) | what shows | edge character |
|---|---|---|
| C1B (`-5692,168,-6895` / `0.0872,0,-0.9962`) | full-width band, **RGB (0,0,3) — near-black**, ~17 px tall, between the (now-correct) dome gradient above and the terrain/water fog below | hard: 358→360 row jump `(16,24,48)→(0,0,3)`, `(0,0,3)→(16,24,48)` at 377→378 — see `.scratch/b16/finding-crisp-edge-c1b-horizon.png` (before/after stack) |
| C1C (`-3843,200,-1101` / `0.0523,0,-0.9986`) | full-width flat **(120,120,120)** band, ~9 px | hard: 357→360 `174→120`, 369→372 `120→176` |
| C2B (`-3843,200,-1101` / `-0.391,0,-0.921`) | same (120,120,120) band, same shape | same |
| C2 (`-4808,277,-5496` / `0.8572,0,0.5150`) | **two** nested flat bands, **(164,181,255)** inside **(205,215,255)** (the latter is C2 `zone1`'s own authored `FOG_COLOR`, byte-exact) | hard: 338→341 `216→184`, 380→383 `184→217` |
| C1 (`-7066,326,-5519` / `-0.7431,0,-0.6691`) | dome visible only through gaps in the ridge silhouette (not full-width) | moderate: 310→315 `176→137` over the gap |
| C3 (canyon primary pose, not the sweep pose) | **same defect as C1B/C1C**, a flat **(156,156,156)** band, ~60 px | hard: row 320→325 `199→156`, 388→395 `156→201` |
| C4, C5 (sweep poses) | not visibly seamed | C4: terrain fills nearly the whole frame at this pose (5,175/921,600 px changed, most of it terrain-fog-adjacent, not this artifact); C5: the dome fades *smoothly* to near-black near the horizon (no jump — `7.3→0.0` continuously over the same span other chapters jump in 1–3 rows), a gentler case worth re-checking at a different pose but not evidenced as a hard seam here |

**This is the exact class of regression the 2026-07 `ForceFogged` deviation was written to
guard against**, and it is real and broad (5 of 8 chapters show a hard version of it, plus C3's
primary pose). Recorded here as a finding with evidence, per the item's own instruction — **not
fixed, and `ForceFogged` is not re-added.** The worst-case shot is
`.scratch/b16/finding-crisp-edge-c1b-horizon.png`. Candidate follow-up (not decided here): the
dome mesh likely needs its own small-scale fix — either texturing/hiding the unfinished cap/skirt
faces, or a narrow blend at the join — which is a `WorldBuilder`/dome-geometry item, not a fog-flag
item; left for the user to triage into a fresh BL at wave close.

### Tests and goldens

`.\RunTests.ps1` — build PASS (0 warnings), **units 669/669**, **engine 26/26, errors clean**,
goldens **9 moved, 0 broken of 13**. A bigger set than Wave A's six, exactly as predicted: deckless
chapters' skies (C1B, C2, C3, C5 — none of which ship an `fvol` field, so A2/A3 were inert on them)
now change too, on top of Wave A's own movers.

| golden | moved? | sanity check (sky in frame?) |
|---|---|---|
| `c1b-night-sea` | **moved** | manifest's own `exercises` text names "night lighting, **skydome**, islands…" explicitly; matches the C1B sweep finding above (its zone1 dome is the same one showing the black band) |
| `c1c-rain` | **moved** | pose pitches 25° down from 700 m — dome visible in the upper frame above the cloud deck/fog it also exercises |
| `c2-city` | **moved** | pose pitches 15° down over the city — matches the C2 sweep finding (the nested flat-band artifact) directly |
| `c2b-rain` | **moved** | pose is `-3843,200,-1101`, **the same position as this item's own C2B sweep shot** (direction differs only by a 0.1 pitch) — direct corroboration |
| `c3-island` | **moved** | pitches 35° down at 400 m near open water; even a thin sky sliver at the top edge now shows the zone1 gradient instead of flat fog (this chapter's dome changes dramatically at the canyon pose, confirmed above) |
| `c4-snow` | **moved** | pitches 20° down; C4 flies `zone2` (B12), whose dome (moon+stars) is now unfogged where visible |
| `c5-city-night` | **moved** | manifest's own `exercises` text names "…**moon and stars**" explicitly; matches the primary C5 pose's 0.0→9–17 fix directly |
| `c1-flight` | **moved** | chase camera on a flying plane — sky routinely in frame; was also one of A2's six movers ("sky in frame") |
| `c1-destroy-effects` | **moved** | near-level view (`direction=1,-0.1,0`) of a radio-tower kill — sky visible in the upper frame; was also one of A2's six movers |
| `c1-waterfall` | ok | a thin (~35 px) gray strip is visible at the very top of frame (`.scratch/b16/check-c1-waterfall.png`), measured (188.5,189.5,190.5) — close to but not exactly either chapter fog colour; `RunTests` confirms the raw pixel hash is unchanged, so whatever this strip is, it renders identically before/after. Not independently re-verified against the pre-B16 binary beyond RunTests' own hash compare (rebuilding the old code was judged not worth it for one non-mover); flagged rather than asserted |
| `viewer-bhawk` | ok | no chapter world at all (`--viewer`) — `BuildHorizon` never runs; inert by construction (`DIAG-10`), same reasoning as A2 |
| `empty-stage` | ok | `--stage=empty`, no gamez — its sky is a different, code-drawn default, not `BuildHorizon`; inert by construction |
| `c1-crash` | ok | frame 20 looks straight down at the fireball/ground rig — no sky in frame (same reasoning A2 gave for this same golden) |

That is the `GOLD-5` pattern this change should produce: every shot whose framing shows a chapter's
sky moved, and nothing else did — now with the four deckless/no-`fvol` chapters (C1B, C2, C3, C5)
joining Wave A's movers, since their skies were inert to the *scatter* fix but not to the *fog* fix.
**Not re-pinned — B17 re-pins after the wave's remaining render changes land**, per this item's
brief.

### Files

`CSVM/src/Mech3/WorldBuilder.cs` (`BuildHorizon`), `CSVM/src/Mech3/SceneBuilder.cs` (`ForceFogged`
deleted, its one reader simplified, the stale skirt-opt-out comment reworded), `docs/architecture.md`
(the `WorldBuilder.cs` entry — `SceneBuilder.cs`'s own entry never mentioned `ForceFogged`, only
its now-deleted XML doc comment did, so it needed no change), `backlog.md` (`BL-303` — a dated line
appended, not closed), this section and the checklist line. `weather.md` was grepped for the
dome-fogging deviation and does not
document it (its dome discussion is zone selection and far-plane clipping, unrelated) — left
untouched, per the item's own instruction not to touch B14's `FOG_ALTITUDE` correction either.
`PROJECT_CONTEXT.md` untouched. `.scratch/b16/` holds 24 before/after screenshots (12 poses × 2)
plus the crisp-edge crop and the `c1-waterfall` sanity check.

### Original brief (kept for reference)

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
— *superseded by B11's amendment: the arithmetic already proved the dome needed unfogging
regardless of B14/B15's semantics, so this item ran BEFORE them instead of after, per the
Dependency and parallelism notes' re-ordering.*

**Model recommendation.** medium — one flag, but the regression surface is every chapter's horizon.

**Verify.** C3/C2B/C5 poses + above-deck pose; 8-chapter freecam sweep eyeballing the horizon band
for the crisp-edge regression the deviation was guarding against.

**⚠ Traps.** The dome's below-horizon skirt already opts out per instance (`csky_fog_on = 0`) — do
not write that instance uniform globally; the index-mismatch hazard is documented at
`WeatherRig.cs:183`.
— *the skirt opt-out premise was checked and found stale — see "Landed" above: nothing ever wrote
that uniform, so there was nothing to avoid writing globally. The index-mismatch hazard itself is
real and unrelated to this item (it is about `csky_fog_on`/`csky_light_fade`/`node_bias` ordering
across shaders in general); still documented at `WeatherRig.cs` and `csky_instance_uniforms.gdshaderinc`.*

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

## B18 ☑ The dome's cap/skirt seam: how does the original hide it?

**Landed.** (2026-08-08) **The band's pixels are the dome's own skirt** — not a gap, not the clear
colour, not the cap. Every chapter's dome is a *textured wall* from local Y = 0 up (Y = 0 is the
camera's altitude, so that ring IS the horizon line) plus an *untextured skirt* cone from Y = 0
down to −3.0…−11.7 km, closed by a flat disc. **The skirt's one `Colored` material is authored in
the flown zone's own `FOG_COLOR`** — byte-exact in six of seven chapters (C3 is 200 against
0.79 × 255 = 201) — so below the horizon the dome simply *is* the fog wall the terrain fades into,
and there is nothing to blend. That is the original's answer, and it needs no gradient, no fog on
the dome and no scaling change.

**We were applying that one authored colour twice.** The shader's `col = srgb_to_linear(COLOR.rgb)
* albedo_color` is the right modulate for the install at large, but on these polygons the vertex
colours *restate* the material colour, so the product squares it in linear space:
176 → **120**, 200 → **156**, (205,215,255) → **(164,181,255)**, (16,24,48) → **(0,0,3)**. Those
four are exactly B16's four measured band colours, to the byte — the arithmetic was checked against
the measurement before a line of code was written. Fix: `GameZ.VertexColorsRestateMaterialColor`
identifies the redundantly-duplicated value (SRC-5's inverse — a field authored twice, not a field
missing) and `SceneBuilder.EmitPolygon` emits white corners for it, so the colour lands once. **The
product is kept everywhere else**, because everywhere else it is right (below).

**Files.** `CSVM/src/Mech3/GameZ.cs` (the predicate), `CSVM/src/Mech3/SceneBuilder.cs`
(`EmitPolygon`/`EmitTriangle` + the group loop), `CSVM/src/Mech3/WorldBuilder.cs` (`BuildHorizon`'s
doc — the dome's two-piece shape, replacing the "unfinished flat-gray cap, likely never
player-visible" reading), `CSVM.Tests/FlatColorTests.cs` (new), `docs/architecture.md`
(`GameZ.cs`, `SceneBuilder.cs`, `WorldBuilder.cs`), `docs/formats/weather.md` (the skirt/`FOG_COLOR`
decode), `docs/verification.md` (`GOLD-8`, `SHOT-22`), this section and the checklist line.
`PROJECT_CONTEXT.md` untouched. Probes and instruments in `.scratch/b18/`.

### How the pixels were identified — one probe, no theorising past it

At C1B's B16 sweep pose (`-5692,168,-6895` / `0.0872,0,-0.9962`), where the band is worst:

| probe | rows 340–359 (above) | rows 360–376 (the band) | verdict |
|---|---|---|---|
| baseline | (16,24,48) | **(0,0,3)**, sd 0.00 | reproduces B16's numbers exactly |
| `--tex-override=Sky1.tif=00ff00` | **green** | **(0,0,3) — unchanged** | the band is NOT the textured wall |
| `--sky-zone=zone2` (C1B's zone2 is a bare marker ⇒ empty dome) | procedural-sky ≈(160,163,168) | **≈(160,163,168) — the same** | the band is NOT the background either: with no dome there is no band |

So the band is dome geometry that carries no texture — which the gamez says is the skirt, and only
the skirt. The band's angular extent corroborates it: 17 px ≈ 1.65° below the horizon at 720 rows,
against `atan(168 m / 5393 m)` = **1.78°** to the top of the water at the map edge. **The `(0,0,3)`
value then settles the rest by arithmetic**: `linear_to_srgb(srgb_to_linear(16/255)²) = 0.08 → 0`,
`(24)² → 0`, `(48)² → 3`. Not a gap, not a scale artifact — a squared colour.

### Why the product is right everywhere else (the rule's blast radius)

Install-wide cross-tab of every polygon using a `Colored` material (`.scratch/b18/colored_census.py`):

| material colour | vertex colours | polygons | what the product does |
|---|---|---|---|
| coloured | white | 677 | applies the material colour once — correct |
| white | coloured | 99 | applies the vertex gradient once — correct (C1's `part3` debris runs 94→255) |
| white | white | 366 | identity |
| **coloured** | **restates it** | **87** | **squares it — the defect** |

That kills the two tempting "simpler" rules outright: *vertex colour wins* would blank 677
polygons to white, *material colour wins* would blank 99. Only the duplicate case is wrong, and
only it is changed. Of the 87, **85 are the seven chapters' dome skirts** (C1 14, C1B 13, C1C 15,
C2 13, C2B 15, C3 13, C4 2) and the other 2 are C4 `g206` polygons authored black on black, where
`0 × 0 = 0` makes the change inert. C5 has **none** — its skirt is textured — which is why every C5
render below is bit-identical. `CSVM.Tests/FlatColorTests.cs` pins that per-chapter census (plus
five hand-authored unit cases, including the debris gradient and a textured material) so a future
extraction growing the number is a failure, not a silent widening.

### The five affected chapters' horizon poses, before → after

Instrument (`SHOT-22`, `.scratch/b18/seamcheck.py`): the largest row-to-row luminance jump in
rows 250–500 **whose two rows are both flat across the frame** (mean per-channel column sd < 2.0).
Terrain silhouettes jump too, so an unrestricted jump statistic is degenerate here. Poses 1–6 are
B16's own documented sweep poses; C4/C5's were not preserved on disk, so those two are fresh and
documented in `.scratch/b18/shoot.ps1` with the rest.

| pose | flat-band edge before | after | band colour before → after | changed px |
|---|---|---|---|---|
| C1B sweep `-5692,168,-6895` | **24.0** at row 359 | **0.5** | (0,0,3) → (16,24,48) | 24,498 (2.7 %) |
| C1C sweep `-3843,200,-1101` | **55.7** at row 359 | **1.0** | (120,120,120) → (176,176,176) | 17,806 (1.9 %) |
| C2B sweep `-3843,200,-1101` | **55.7** at row 359 | **1.0** | (120,120,120) → (176,176,176) | 16,772 (1.8 %) |
| C2 sweep `-4808,277,-5496` | nested bands, rows 340–379 | gone | (164,181,255) → (205,215,255) | 68,106 (7.4 %) |
| C3 canyon `-3504,710,-3619` | 60-row flat band, rows 326–385 | gone | (156,156,156) → (200,200,200) | 99,352 (10.8 %) |
| C1 sweep `-7066,326,-5519` | visible only through ridge gaps | gone | (124,124,124) → (176,176,176) | 1,706 (0.2 %) |
| **C4 sweep** `-4974,300,-3861` | **48.1** at row 359 | **0.2** | (144,144,144) → (192,192,192) | 450,293 (48.9 %) |
| C5 sweep `-9187,90,-2037` | none | none | — | **0** |

C2's and C3's edges are multi-row ramps, so the single-row statistic reads them low on both sides;
the row scans in the table's colour column are the evidence there, and both now sit at one constant
colour from the dome's lowest textured row all the way down into the terrain fog.

⚠ **B16's "C4, C5 not visibly seamed" was a pose artifact, not a chapter fact** — at B16's C4 sweep
pose terrain filled the frame. At a pose with open horizon, C4 was the *worst* case in the install:
the squared skirt covered the entire lower half of the frame as a flat (144,144,144) plate with a
razor edge at row 359 (`.scratch/b18/before-sweep-c4.png` vs `after-sweep-c4.png`). C5 genuinely is
clean, for the reason the census gives.

### The two pinned C1 poses — unchanged above the horizon band

Per-row diff (`.scratch/b18/diffrows.py`), and both come out to a single contiguous run starting at
the horizon line:

| pose | changed rows | above the band | changed pixels |
|---|---|---|---|
| C1 river `-7323,192,-3829` / `-0.997,-0.1,0.070` | **300–322 only** — the pose pitches 0.1 down, putting the horizon at row 301, so this IS the horizon band | byte-identical | 8,166 (0.9 %), (122,122,122) → (176,176,176) |
| C1 above-deck `-7323,1192,-3829` / `0,0,-1` | **360–374 only** (level pose ⇒ horizon at row 360) | byte-identical, including B16's own rows 265–355 gray-band box | 13,608 (1.5 %) |

C2B above-deck (`-3843,1500,-1101`) behaves the same: rows 360–423 only. **No pose in the set
changes a single pixel above its own horizon line** — which is the structural guarantee, since the
skirt is the only geometry below Y = 0 and the wall is the only geometry above it.

### Tests and goldens

`.\RunTests.ps1` — build PASS (0 warnings), **units 682/682** (was 669; +13 from
`FlatColorTests`), **engine 26/26, errors clean**, goldens **9 moved, 0 broken of 13**. Exit 1 is
the golden stage alone. **Not re-pinned — B17 re-pins once**, per this item's brief.

⚠ **B16 also left 9 movers unpinned, so the raw list is about both items** (`GOLD-8`). The hashes
were A/B'd against a build with the one fix line temporarily neutralised (`git diff` clean
afterwards, METHOD-17), which separates them:

| golden | B18 moved it? | why |
|---|---|---|
| `c1b-night-sea`, `c1c-rain`, `c2-city`, `c2b-rain`, `c3-island`, `c4-snow` | **yes** | the six chapters whose flown zone has a non-white restated skirt and whose framing reaches the horizon |
| `c1-flight`, `c1-destroy-effects` | **yes** | C1 chase / near-level kill shot — horizon in frame, and C1's skirt is 176 |
| `c5-city-night` | **no — byte-identical** (`88c6df54…` both sides) | C5's skirt is textured, so the census says 0 restated non-white polygons; the shot still appears in the list because **B16** moved it. The able-to-fail control for the whole census |
| `c1-waterfall`, `c1-crash`, `viewer-bhawk`, `empty-stage` | no | `c1-crash` looks straight down; `c1-waterfall`'s only sky is a strip at the top of frame, above the horizon; the other two build no chapter dome at all (`DIAG-10`) |

### What was NOT the answer (the three candidates in the brief, each disproved)

- **(a) "at the original's scale/anchor the skirt sits below the terrain horizon."** No: the skirt
  starts exactly AT the horizon by construction (its top ring is the wall's bottom ring at local
  Y = 0, and the dome is camera-anchored, so that ring is always at eye level). It is *meant* to be
  seen there.
- **(b) "our 40 km far plane renders past where the dome was meant to be seen."** No: at C1B's
  1.65× the skirt's bottom ring sits ~32 km out, inside the far plane, and the `--sky-zone=zone2`
  probe shows the band region is dome-covered, not clipped open. **The C1B scale-cap/worst-seam
  coincidence the brief flagged is a coincidence**: C1B looked worst only because its `FOG_COLOR`
  is the darkest in the install (16,24,48), so squaring it lands on near-black against a visible
  night sky. `HorizonScaleFor` was not touched.
- **(c) "a bottom cap we don't build."** No: the bottom disc is authored and built (poly 14 of
  C1C's `g1155`, at Y = −4682) and never enters frame at flight altitudes.

### Original brief (kept for reference)

**Goal.** No hard band where the dome meets the horizon in any chapter, with the mechanism
decoded rather than painted over — the dome stays authored-unfogged (B16 is not reopened).

**Evidence (confidence: finding traced, mechanism lead-only).** B16's sweep: honouring
`fog: false` exposes the dome's own flat cap/skirt geometry as a hard-edged band in **5 of 8
chapters** (C1B a black gap — worst; C1C/C2B/C2/C3 milder; C4/C5 clean at the sweep poses;
worst-case shot `.scratch/b16/finding-crisp-edge-c1b-horizon.png`). The original renders the
same authored meshes unfogged with no band anywhere in CAP-11/12 footage. Our dome is a
remodel, not the original's shape: camera-anchored, 2.5×-scaled (`HorizonScaleFor` caps to the
far-plane fit — C1B lands at ~1.65×), wall ~16–22 km out. Note the coincidence worth chasing:
**C1B is both the scale-cap outlier and the worst seam.** Candidate mechanisms: (a) at the
original's scale/anchor the skirt sits below the terrain horizon at flight altitudes; (b) the
original hard-clips terrain at `CLIP_RANGES.far` (2,050–4,500 m) with fog covering the last
stretch — our 40 km far plane renders past where its dome geometry was ever meant to be seen
against; (c) a bottom cap we don't build.

**Approach.** First identify what the band's pixels actually are (dome skirt face? a gap onto
the clear colour between terrain edge and dome wall?) — a `--tex-override`/flat-colour probe
per candidate surface settles it in one shot. Then read the dome meshes' authored bounds and
compute where the skirt's bottom edge lands at authored 1× anchoring vs our scaled anchor, per
chapter, at typical flight altitude. Test the surviving candidate with a probe before writing
the fix. The fix must colour or cover the seam from the dome's own authored data (its skirt
vertex colours) or from geometry the data implies — never an invented gradient, never fog.

**Model recommendation.** high — a geometry decode with an easy-to-fake-and-wrong fix.

**Verify.** The five affected chapters' horizon poses before/after; B16's 8-chapter crisp-edge
sweep repeated clean; the two pinned C1 poses unchanged above the horizon band;
`.\RunTests.ps1` (goldens may move again — list, don't re-pin; B17 re-pins once).

**⚠ Traps.** Do not reintroduce fog on the dome in any form — that is B16's landed verdict.
`HorizonScaleFor`'s cap and the C1B 1.65× special case are documented in weather.md — read
that section before touching any dome scaling. If the evidenced fix is a real geometry change
to the dome build, take a golden baseline first.
— *all three held: no fog was added, `HorizonScaleFor` was not touched (weather.md's dome-scale
section was re-read and then extended rather than edited), and the fix turned out not to be a
geometry change at all, so the geometry baseline was not needed — the before/after probe set in
`.scratch/b18/` is the baseline that was taken.*

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
— **already done, by `B15`:** `CAP-12`'s own underside box re-shot on the landed Wave-B build
(`.\RunProbe.ps1 --freecam --chapter=C1 --det --mute --pos=-4974,670,-3861 --direction=-1,0,0`,
`.scratch/b15/landed-deck-below-670.png`) reads **213.9 against the original's 167.7** — so the
delta is **+46**, not +54, and the item stands. Two `B15` findings feed this one: (a) the deck
tiles author `lighting: false` and `fog: true` (checked in `models.json`, all 144 in C1), so the
underside is fullbright × `WorldLight` and nothing about the deck's own shading is currently
data-driven; (b) **the original's whole overcast ceiling reads 166–175 in `C1 IA1 Fog river.png`,
within ~9 units of its own `FOG_COLOR` 176 across the entire visible sky** — which is why the
original's deck barely fogs and ours washes out, and it means fixing 167 also fixes the river
pose's fog symptom. Expect the ceiling to look *worse* until it lands: removing `fogRangeFactor`
took away the compensation that was hiding this (205.6 → 214.8 at `CAP-12` `t8.0`).

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
